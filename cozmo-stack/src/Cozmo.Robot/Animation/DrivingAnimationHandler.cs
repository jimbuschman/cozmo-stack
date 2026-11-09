using System.Globalization;
using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
namespace Cozmo.Robot.Animation;
// fidelity: M5-063
internal readonly record struct DrivingAnimations(AnimationTrigger Start, AnimationTrigger Loop, AnimationTrigger End);
internal sealed record PushDrivingAnimationMessage(DrivingAnimations Animations, string LockName);
internal sealed record DrivingAnimationRequest(AnimationTrigger Trigger, int Argument2, bool Argument3, int Argument4, float Argument5, bool Argument6);
// The sole per-Robot handler; DriveActions uses its existing IDrivingAnimationHandler interface.
internal sealed class DrivingAnimationHandler(CozmoRobot robot) : IDrivingAnimationHandler
{
    private readonly List<(DrivingAnimations Animations, string LockName, object? Owner)> _entries = new();
    private HashSet<string> _missingMessages=new();
    private EngineRobot? _owner;
    private long _generation;
    private byte _tracks;
    // Native constructor never writes +0x44 (tag); this managed default is latent until Init assigns it.
    private uint _tag;
    private bool _unlock = true, _repeat;
    internal Func<int>? GetSimpleMoodForTest;
    internal Func<bool>? AreNeedsMetForTest;
    internal Func<bool>? AreNeedsMet;
    internal Func<bool>? HasPathToFollowForTest;
    internal Func<DrivingAnimationRequest, IActionRunner>? CreateActionForTest;
    internal Action<string, uint>? CallObserved;
    internal Action? RemoveWarningObservedForTest;
    internal DrivingAnimations? Current { get; private set; } = new((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D);
    public int State { get; private set; } = 5;
    internal uint StartId { get; private set; }
    internal uint LoopId { get; private set; }
    internal uint EndId { get; private set; }
    internal IReadOnlyList<(DrivingAnimations Animations, string LockName)> Entries => _entries.Select(e=>(e.Animations,e.LockName)).ToArray();
    internal void Bind(EngineRobot owner)
    {
        _generation++;
        _owner = owner; State = 5;
        Current = new((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D); // checked ctor 0x004F0724..0x004F0728.
        StartId = LoopId = EndId = 0; _tracks = 0; _unlock = true; _repeat = false; _entries.Clear();
    }
    private void Missing(string text) => robot.Engine.EmitStackLog("MISSING: M5-063 " + text);
    private void DriveMissing(string text) { if(_missingMessages.Add(text))Missing(text); }
    private void Warning(string key, string text) => robot.Engine.EmitSourceLog(EngineLogLevel.Warning, null, key, text);
    internal void Push(DrivingAnimations animations, string lockName)
    {
        if (State != 5) Warning("DrivingAnimationHandler.PushDrivingAnimations", "Pushing new animations while currently playing");
        _entries.Add((animations, lockName, null)); // does not change Current.
    }
    // Scoped host acquisition for an activity that can be replaced by a callback.
    internal void PushOwned(DrivingAnimations animations,string lockName,Behavior.ScopedAcquisition lease)
    {
        if(State!=5)Warning("DrivingAnimationHandler.PushDrivingAnimations","Pushing new animations while currently playing");
        if(lease.Active)_entries.Add((animations,lockName,lease));
    }
    internal void RemoveOwned(Behavior.ScopedAcquisition lease)
    {
        int index=_entries.FindIndex(e=>ReferenceEquals(e.Owner,lease));
        if(index>=0)_entries.RemoveAt(index);
    }
    internal void Remove(string lockName) => RemoveScoped(lockName, null);
    internal (bool Completed, object? Owner) RemoveScoped(string lockName, Func<bool>? current)
    {
        if (State != 5)
        {
            Warning("DrivingAnimationHandler.PopDrivingAnimations", "Popping animations while currently playing");
            if(current is not null)RemoveWarningObservedForTest?.Invoke(); // unit seam at warning continuation
        }
        if (current is not null && !current()) return (false,null);
        if (_entries.Count == 0) { Warning("DrivingAnimationHandler.PopDrivingAnimations", "Tried to pop animations but the stack is empty!"); return (true,null); }
        for (int i = 0; i < _entries.Count; ++i)
        {
            if (string.Equals(_entries[i].LockName, lockName, StringComparison.Ordinal)) { var owner=_entries[i].Owner; _entries.RemoveAt(i); return (true,owner); }
            if (i + 1 == 1000)
            {
                robot.Engine.EmitSourceLog(EngineLogLevel.Error, null, "LoopBoundOverflow", "../../../../engine/drivingAnimationHandler.cpp:108");
                EngineErrorState.StoreAndMaybeBreak(); return (true,null);
            }
        }
        return (true,null);
    }
    public bool Init(byte tracks, uint tag, bool argument3, bool repeatWhilePath)
    { _generation++;return InitCore(tracks,tag,argument3,repeatWhilePath,null); }
    internal bool InitOwned(EngineRobot owner,Func<bool> current,byte tracks,uint tag,bool argument3,bool repeatWhilePath)
    {
        if(!ReferenceEquals(_owner,owner)||!current())return false;
        long generation=++_generation;
        return InitCore(tracks,tag,argument3,repeatWhilePath,()=>generation==_generation&&current());
    }
    private bool InitCore(byte tracks,uint tag,bool argument3,bool repeatWhilePath,Func<bool>? current)
    {
        // UpdateCurrDrivingAnimations is called only here, before the Init stores.
        _missingMessages=new();
        bool selected=UpdateCurrent(current);
        if(current is not null&&!current())return false;
        State = 0; StartId = LoopId = EndId = 0;
        _tracks = tracks; _tag = tag; _unlock = !argument3; _repeat = repeatWhilePath; // Init's fourth C++ parameter (not counting this).
        return selected;
    }
    public bool InputsReady => Current is not null;
    private bool UpdateCurrent(Func<bool>? current=null)
    {
        if (_entries.Count != 0) { Current = _entries[^1].Animations; return true; }
        // M7-026: empty override stack unconditionally reads the BOUND Robot's
        // Mood+440 at004F0D3C/3E/42. Raw1 is Sad (not Happy).
        int mood;
        if(GetSimpleMoodForTest is {} substitute)mood=substitute();
        else if(_owner?.MoodOwner is {} actualMood)mood=(int)actualMood.GetSimpleMood();
        else
        {
            DriveMissing("M1-034/M7-026 skip null bound Robot/Mood selection at0x004F0D3C..42; prior triplet retained, no needs/default fallback invented");
            return false;
        }
        if(current is not null&&!current())return false;
        if (mood == 1) { Current = new((AnimationTrigger)0x94, (AnimationTrigger)0x90, (AnimationTrigger)0x8C); return true; }
        bool needsMet;
        if(AreNeedsMetForTest is {} substituteNeeds)needsMet=substituteNeeds();
        else if(_owner?.Engine.Needs is {} actualNeeds)needsMet=actualNeeds.State.AreNeedsMet(); //004F0D50..66→6960A8→69CB4C.
        else
        {
            DriveMissing("M1-034/M15-001 skip null bound Needs owner selection at0x004F0D50..5A; prior triplet retained, no default fallback invented");
            return false;
        }
        if(current is not null&&!current())return false;
        Current = needsMet ? new((AnimationTrigger)0x96, (AnimationTrigger)0x92, (AnimationTrigger)0x8E)
            : new((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D);
        return true;
    }
    internal void HandleActionCompleted(uint id, uint result)
    {
        if (id == StartId && result == 0)
        {
            if (Current?.Loop == (AnimationTrigger)0x23F) return;
            PlayDrivingLoopAnim(); return;
        }
        if (id == LoopId)
        {
            bool follow;
            if (_repeat) follow = true;
            else if (HasPathToFollowForTest is { } hasPath) follow = hasPath();
            else follow=robot.Motion.Path.HasPathToFollow;
            if (follow && result == 0) { PlayDrivingLoopAnim(); return; }
            if (_unlock) UnlockTracks();
            PlayEndAnim(); return;
        }
        if (id != EndId) return;
        State = 4;
        if (_unlock)
        {
            CallObserved?.Invoke("LockTracks", _tag);
            robot.Motion.LockTracks(_tracks, unchecked((int)_tag).ToString(CultureInfo.InvariantCulture), "DrivingAnimations");
        }
    }
    private void Cancel(uint id,EngineRobot? owner=null,Func<bool>? current=null)
    {
        CallObserved?.Invoke("Cancel", id);
        if(current is not null&&(!current()||!ReferenceEquals(_owner,owner)))return;
        if (_owner?.ActionListOwner is { } actions) actions.Cancel(id);
        else Missing("M8-015 ActionList.Cancel recipient absent");
    }
    private void UnlockTracks(EngineRobot? owner=null,Func<bool>? current=null)
    {
        CallObserved?.Invoke("UnlockTracks", _tag);
        if(current is not null&&(!current()||!ReferenceEquals(_owner,owner)))return;
        robot.Motion.UnlockTracks(_tracks, unchecked((int)_tag).ToString(CultureInfo.InvariantCulture));
    }
    public void PlayStartAnim()
        => PlayStartAnimCore(null,null);
    internal void PlayStartAnimOwned(EngineRobot owner,Func<bool> current)
    {long generation=_generation;PlayStartAnimCore(owner,()=>generation==_generation&&current());}
    private void PlayStartAnimCore(EngineRobot? owner,Func<bool>? guard)
    {
        if(guard is not null&&(!guard()||!ReferenceEquals(_owner,owner)))return;
        if(State!=0)return;
        if(Current is not { } current) { Missing("current start/loop selection is unknown");return; }
        if(current.Start!=(AnimationTrigger)0x23F)PlayDrivingStartAnim(owner,guard);
        else if(current.Loop!=(AnimationTrigger)0x23F)PlayDrivingLoopAnim(owner,guard);
    }
    internal void PlayDrivingStartAnim(EngineRobot? owner=null,Func<bool>? guard=null)
    {
        if(guard is not null&&(!guard()||!ReferenceEquals(_owner,owner)))return;
        State=1;
        if(Current is not { } current){Missing("current start selection is unknown");return;}
        if(CreateAction(current.Start) is { } action){if(guard is not null&&!guard()){Retire(action);return;}StartId=action.Tag;Queue(action,owner,guard);}
    }
    public uint PlayEndAnim()
        => PlayEndAnimCore(null,null);
    internal uint PlayEndAnimOwned(EngineRobot owner,Func<bool> current)
    {long generation=_generation;return PlayEndAnimCore(owner,()=>generation==_generation&&current());}
    private uint PlayEndAnimCore(EngineRobot? owner,Func<bool>? current)
    {
        if(current is not null&&(!current()||!ReferenceEquals(_owner,owner)))return 0;
        if (State is 3 or 4 or 5) return 0;
        Cancel(StartId,owner,current);if(current is not null&&!current())return 0;
        Cancel(LoopId,owner,current);if(current is not null&&!current())return 0;
        if (Current?.End == (AnimationTrigger)0x23F) return 0;
        if (_unlock) UnlockTracks(owner,current);
        if(current is not null&&!current())return 0;
        PlayDrivingEndAnim(owner,current);
        return 1;
    }
    internal void PlayDrivingLoopAnim(EngineRobot? owner=null,Func<bool>? guard=null)
    {
        if(guard is not null&&(!guard()||!ReferenceEquals(_owner,owner)))return;
        State = 2;
        if (Current is not { } current) { Missing("current loop trigger is unset because Init was not built at its M12-024 caller"); return; }
        if (CreateAction(current.Loop) is { } action)
        {
            if(guard is not null&&!guard()){Retire(action);return;}
            LoopId = action.Tag;
            Queue(action,owner,guard);
        }
    }
    internal void PlayDrivingEndAnim(EngineRobot? owner=null,Func<bool>? guard=null)
    {
        if(guard is not null&&(!guard()||!ReferenceEquals(_owner,owner)))return;
        if (State is 3 or 4 or 5) return;
        State = 3;
        if (Current is not { } current) { Missing("current end trigger is unset because Init was not built at its M12-024 caller"); return; }
        if (CreateAction(current.End) is { } action)
        {
            if(guard is not null&&!guard()){Retire(action);return;}
            EndId = action.Tag;
            Queue(action,owner,guard);
        }
    }
    private IActionRunner? CreateAction(AnimationTrigger trigger)
    {
        var request = new DrivingAnimationRequest(trigger, 1, true, 0, BitConverter.Int32BitsToSingle(0x42700000), false);
        if (CreateActionForTest is { } factory) return factory(request);
        var driveScope=_missingMessages;
        return new TriggerLiftSafeAnimationAction(robot, request,text=>SteppedBehavior.ReportMissing(text,driveScope));
    }
    private static void Retire(IActionRunner action)
    {if(action is ActionRunner runner && runner.CanRetireNeverAdmittedTree())runner.RetireNeverAdmittedTree();else if(action is IDisposable disposable)disposable.Dispose();}
    private void Queue(IActionRunner action,EngineRobot? owner=null,Func<bool>? current=null)
    {
        if(current is not null&&(!current()||!ReferenceEquals(_owner,owner))){Retire(action);return;}
        if (_owner?.ActionListOwner is { } actions)
        {
            if(current is null)actions.QueueAction(QueueActionPosition.InParallel,action,0);
            else actions.QueueActionGuardedInParallel(action,0,current);
        }
        else { Missing("M8-015 ActionList.QueueAction(IN_PARALLEL, action, 0) recipient absent"); if (action is IDisposable disposable) disposable.Dispose(); }
    }
    public void ActionIsBeingDestroyed()
    {
        State = 5; Cancel(StartId); Cancel(LoopId); Cancel(EndId);
    }
    internal void ActionIsBeingDestroyed(EngineRobot owner, Func<bool> current)
    {
        if (!ReferenceEquals(_owner, owner) || !current()) return;
        long generation=_generation;
        var actions = owner.ActionListOwner;
        State = 5;
        void CancelOwned(uint id)
        {
            if (generation!=_generation || !current() || !ReferenceEquals(_owner, owner)) return;
            CallObserved?.Invoke("Cancel", id);
            if (generation!=_generation || !current() || !ReferenceEquals(_owner, owner)) return;
            if (actions is not null) actions.Cancel(id);
            else Missing("M8-015 ActionList.Cancel recipient absent");
        }
        CancelOwned(StartId); CancelOwned(LoopId); CancelOwned(EndId);
    }
    internal void ClearEntriesForDestroy() => _entries.Clear();
    // The host facade survives removal; the native handler is deleted/reconstructed with its Robot.
    internal void ResetToConstructed()
    {
        _generation++;
        _owner = null; State = 5;
        Current = new((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D);
        StartId = LoopId = EndId = _tag = 0; _tracks = 0; _unlock = true; _repeat = false;
        _entries.Clear();
    }
}
