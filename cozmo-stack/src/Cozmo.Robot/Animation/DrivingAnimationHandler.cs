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
    private readonly List<(DrivingAnimations Animations, string LockName)> _entries = new();
    private EngineRobot? _owner;
    private byte _tracks;
    // Native constructor never writes +0x44 (tag); this managed default is latent until Init assigns it.
    private uint _tag;
    private bool _unlock = true, _repeat;
    internal Func<int>? GetSimpleMoodForTest;
    internal Func<bool>? AreNeedsMetForTest;
    internal Func<bool>? HasPathToFollowForTest;
    internal Func<DrivingAnimationRequest, IActionRunner>? CreateActionForTest;
    internal Action<string, uint>? CallObserved;
    internal DrivingAnimations? Current { get; private set; } = new((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D);
    public int State { get; private set; } = 5;
    internal uint StartId { get; private set; }
    internal uint LoopId { get; private set; }
    internal uint EndId { get; private set; }
    internal IReadOnlyList<(DrivingAnimations Animations, string LockName)> Entries => _entries.ToArray();
    internal void Bind(EngineRobot owner)
    {
        _owner = owner; State = 5;
        Current = new((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D); // checked ctor 0x004F0724..0x004F0728.
        StartId = LoopId = EndId = 0; _tracks = 0; _unlock = true; _repeat = false; _entries.Clear();
    }
    private void Missing(string text) => robot.Engine.EmitStackLog("MISSING: M5-063 " + text);
    private void Warning(string key, string text) => robot.Engine.EmitSourceLog(EngineLogLevel.Warning, null, key, text);
    internal void Push(DrivingAnimations animations, string lockName)
    {
        if (State != 5) Warning("DrivingAnimationHandler.PushDrivingAnimations", "Pushing new animations while currently playing");
        _entries.Add((animations, lockName)); // does not change Current.
    }
    internal void Remove(string lockName)
    {
        if (State != 5) Warning("DrivingAnimationHandler.PopDrivingAnimations", "Popping animations while currently playing");
        if (_entries.Count == 0) { Warning("DrivingAnimationHandler.PopDrivingAnimations", "Tried to pop animations but the stack is empty!"); return; }
        for (int i = 0; i < _entries.Count; ++i)
        {
            if (string.Equals(_entries[i].LockName, lockName, StringComparison.Ordinal)) { _entries.RemoveAt(i); return; }
            if (i + 1 == 1000)
            {
                robot.Engine.EmitSourceLog(EngineLogLevel.Error, null, "LoopBoundOverflow", "../../../../engine/drivingAnimationHandler.cpp:108");
                EngineErrorState.StoreAndMaybeBreak(); return;
            }
        }
    }
    public bool Init(byte tracks, uint tag, bool argument3, bool repeatWhilePath)
    {
        // UpdateCurrDrivingAnimations is called only here, before the Init stores.
        bool selected=UpdateCurrent();
        if(!selected)Current=null; // Unknown selection is not the constructor triplet.
        State = 0; StartId = LoopId = EndId = 0;
        _tracks = tracks; _tag = tag; _unlock = !argument3; _repeat = repeatWhilePath; // Init's fourth C++ parameter (not counting this).
        return selected;
    }
    public bool InputsReady => Current is not null;
    private bool UpdateCurrent()
    {
        if (_entries.Count != 0) { Current = _entries[^1].Animations; return true; }
        if (GetSimpleMoodForTest is null)
        {
            Missing("M7-026 GetSimpleMood (0x004F0D42); selection stopped before mood/needs/default choice"); return false;
        }
        if (GetSimpleMoodForTest() == 1) { Current = new((AnimationTrigger)0x94, (AnimationTrigger)0x90, (AnimationTrigger)0x8C); return true; }
        if (AreNeedsMetForTest is null)
        {
            Missing("M15-001 AreNeedsMet (0x004F0D5A); selection stopped before needs/default choice"); return false;
        }
        Current = AreNeedsMetForTest() ? new((AnimationTrigger)0x96, (AnimationTrigger)0x92, (AnimationTrigger)0x8E)
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
            else { Missing("M12-032 PathComponent.HasPathToFollow (0x004F0D9E); loop completion decision stopped"); return; }
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
    private void Cancel(uint id)
    {
        CallObserved?.Invoke("Cancel", id);
        if (_owner?.ActionListOwner is { } actions) actions.Cancel(id);
        else Missing("M8-015 ActionList.Cancel recipient absent");
    }
    private void UnlockTracks()
    {
        CallObserved?.Invoke("UnlockTracks", _tag);
        robot.Motion.UnlockTracks(_tracks, unchecked((int)_tag).ToString(CultureInfo.InvariantCulture));
    }
    public void PlayStartAnim()
    {
        if(State!=0)return;
        if(Current is not { } current) { Missing("current start/loop selection is unknown");return; }
        if(current.Start!=(AnimationTrigger)0x23F)PlayDrivingStartAnim();
        else if(current.Loop!=(AnimationTrigger)0x23F)PlayDrivingLoopAnim();
    }
    internal void PlayDrivingStartAnim()
    {
        State=1;
        if(Current is not { } current){Missing("current start selection is unknown");return;}
        if(CreateAction(current.Start) is { } action){StartId=action.Tag;Queue(action);}
    }
    public uint PlayEndAnim()
    {
        if (State is 3 or 4 or 5) return 0;
        Cancel(StartId); Cancel(LoopId);
        if (Current?.End == (AnimationTrigger)0x23F) return 0;
        if (_unlock) UnlockTracks();
        PlayDrivingEndAnim();
        return 1;
    }
    internal void PlayDrivingLoopAnim()
    {
        State = 2;
        if (Current is not { } current) { Missing("current loop trigger is unset because Init was not built at its M12-024 caller"); return; }
        if (CreateAction(current.Loop) is { } action)
        {
            LoopId = action.Tag;
            Queue(action);
        }
    }
    internal void PlayDrivingEndAnim()
    {
        if (State is 3 or 4 or 5) return;
        State = 3;
        if (Current is not { } current) { Missing("current end trigger is unset because Init was not built at its M12-024 caller"); return; }
        if (CreateAction(current.End) is { } action)
        {
            EndId = action.Tag;
            Queue(action);
        }
    }
    private IActionRunner? CreateAction(AnimationTrigger trigger)
    {
        var request = new DrivingAnimationRequest(trigger, 1, true, 0, BitConverter.Int32BitsToSingle(0x42700000), false);
        if (CreateActionForTest is { } factory) return factory(request);
        Missing("M8-005 TriggerLiftSafeAnimationAction queued runner constructor 0x004F0E86/0x004F10A4; existing SteppedBehavior task helper is not an ActionList runner, so no ID or queued action is fabricated");
        return null;
    }
    private void Queue(IActionRunner action)
    {
        if (_owner?.ActionListOwner is { } actions) actions.QueueAction(QueueActionPosition.InParallel, action, 0);
        else { Missing("M8-015 ActionList.QueueAction(IN_PARALLEL, action, 0) recipient absent"); if (action is IDisposable disposable) disposable.Dispose(); }
    }
    public void ActionIsBeingDestroyed()
    {
        State = 5; Cancel(StartId); Cancel(LoopId); Cancel(EndId);
    }
    internal void ClearEntriesForDestroy() => _entries.Clear();
    // The host facade survives removal; the native handler is deleted/reconstructed with its Robot.
    internal void ResetToConstructed()
    {
        _owner = null; State = 5;
        Current = new((AnimationTrigger)0x95, (AnimationTrigger)0x91, (AnimationTrigger)0x8D);
        StartId = LoopId = EndId = _tag = 0; _tracks = 0; _unlock = true; _repeat = false;
        _entries.Clear();
    }
}
