using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
namespace Cozmo.Robot.Animation;

// fidelity: M5-063, M8-020, M8-021
// ActionList runner for the checked driving calls. Playback uses the existing streamer.
// The unported concrete-action callback/union/DAS portions stay visible, never source-complete.
internal class TriggerAnimationAction : ActionRunner
{
    private readonly CozmoRobot _robot;
    internal DrivingAnimationRequest Request { get; private set; }
    private string? _group;
    protected bool HasStarted => _started;
    protected bool HasEnded => _ended;
    protected bool Failed => _failed;
    private AnimationTicket? _ticket;
    private string _clip="";
    private AnimationClip? _cachedClip;
    private AnimationClip? _directClip;
    private Func<bool>? _directOwnerCurrent;
    private bool _directDiagnosticReported;
    private bool _destroyed;
    private bool _started, _ended, _failed;
    private uint _loops;
    private byte _animationTag;
    private RetainedSubscription? _ca, _cb, _d5, _aborted;
    private readonly Action<string> _missing;
    internal TriggerAnimationAction(CozmoRobot robot, DrivingAnimationRequest request, Action<string>? missing = null, bool liftSafe = false, AnimationClip? directClip = null) : base(0x1A, (uint)request.Argument4)
    {
        _robot=robot;Request=request;_missing=missing??Behavior.SteppedBehavior.ReportMissing;
        _loops = unchecked((uint)request.Argument2);
        // 0x00543C96..0x00543CB2: only forever loops with the default 60.0f timeout.
        TimeoutSeconds=request.Argument2==0 && request.Argument5==BitConverter.Int32BitsToSingle(0x42700000)
            ? BitConverter.Int32BitsToSingle(0x7F7FFFFF) : request.Argument5;
        SourceLog=(level,channel,key,text)=>robot.Engine.EmitSourceLog(level,channel,key,text);Log=robot.Engine.EmitStackLog;
        // Checked M8-005/007 lift-safe ctor gate, 0x00544714..0x00544732.
        if(liftSafe && robot.Engine.Robot is {} owner && owner.CarryingOwner is {} carrying &&
            (carrying.CarriedObjectId??uint.MaxValue)!=uint.MaxValue && robot.Sensors.OffTreadsState==OffTreadsState.OnTreads)RequiredTrackMask|=2;
        _directClip=directClip;
        if (directClip is null)
        {
            SetAnimGroupFromTrigger(request.Trigger);
            Missing("M8-021 trigger DAS blacklist/event diagnostic coverage remains incomplete; concrete CA/CB/D5/5F callbacks, selected completion name and retained playback clip are independent inputs");
        }
    }
    // SetFaceAction's child is PlayAnimationAction with an owned live clip, not a trigger lookup.
    internal TriggerAnimationAction(CozmoRobot robot, AnimationClip clip, Func<bool> ownerCurrent, Action<string>? missing = null, bool diagnosticReported = false)
        : this(robot, new DrivingAnimationRequest(default, 1, true, 0, 60f, false), missing, directClip: clip)
    { _directOwnerCurrent=ownerCurrent;_directDiagnosticReported=diagnosticReported; }
    // TriggerAnimationAction::SetAnimGroupFromTrigger 0x0054432C; shared by constructor and get-out Init.
    protected void SetAnimGroupFromTrigger(AnimationTrigger trigger)
    {
        Request = Request with { Trigger = trigger };
        _group=_robot.Animations.DrivingTriggerMap?.GroupFor(trigger);
        if(_group is not null)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Animations","GetResponseForAnimationTrigger.Found",$"{trigger} -> {_group}");
            if(_group.Length==0)_robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"TriggerAnimationAction.EmptyAnimGroupNameForTrigger",$"Event: {trigger}");
        }
    }
    private void Missing(string text)=>_missing(text);
    internal bool RetireStaleConstructor(Func<bool> current)
    {
        if(current() || _ticket is not null || _cachedClip is not null || _ca is not null || _cb is not null || _d5 is not null || _aborted is not null)
        { Missing("M8-020 stale constructor retirement invariants failed; action retained"); return false; }
        if(!RetireNeverAdmitted())return false;
        _destroyed=true;return true;
    }
    protected override string ActionName=>"PlayAnimation"+(_group??"");
    protected override float EngineClockSeconds=>_robot.Engine.Timer.SecondsF;
    public override bool CanInterrupt()=>false;
    protected override bool AreAnyTracksLocked(uint mask)=>_robot.Motion.AreAnyTracksLocked((byte)mask);
    protected override void LockTracks(uint mask,string owner)=>_robot.Motion.LockTracks((byte)mask,owner,ActionName);
    protected override void UnlockTracksInternal(uint mask,string owner)=>_robot.Motion.UnlockTracks((byte)mask,owner);
    protected override bool AreAllTracksLockedBy(uint mask,string owner)=>_robot.Motion.AreAllTracksLockedBy((byte)mask,owner);
    public override int Init()
    {
        if(_directClip is not null)
        {
            if(!_directDiagnosticReported)Missing("M8-020 direct PlayAnimationAction: completion union and cached Animation pointer/destructor lifecycle remain incomplete");
            if(_destroyed || _directOwnerCurrent?.Invoke()!=true)return 0x03000000;
        }
        if(_directClip is null && string.IsNullOrEmpty(_group))
        {
            if(_robot.Animations.DrivingTriggerMap is null)Missing("M8-021 trigger-map input unavailable; no animation name can be resolved");
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"TriggerAnimationAction.NoAnimationForTrigger",$"Event: {Request.Trigger}");
            return 0x0300000C;
        }
        var library=_robot.Animations.Library;
        if(_directClip is null && library is null){Missing("M8-021 animation-library input unavailable; no playback can be started");return 0x0300000C;}
        _clip=_directClip?.Name ?? library!.GetAnimationNameFromGroup(_group!,Request.Argument6);
        if(_directClip is null && _clip.Length==0)
        {
            return 0x0300000C;
        }
        // fidelity: M8-015, M8-020 — PlayAnimation::Init 0x00543F20..0x00544180.
        _started = _ended = _failed = false;
        _cachedClip??=_directClip ?? library!.GetClip(_clip); // direct live clip or first lookup survives retries.
        _ticket=_robot.Animations.PlayTracked(_cachedClip,Request.Argument3,numLoops:Request.Argument2);
        if (_ticket is null) return 0x03000001;
        if(_directClip is not null && _directOwnerCurrent?.Invoke()!=true)
        {
            _robot.Animations.ClearStreamIfClipMatches(_cachedClip);
            return 0x03000000;
        }
        _animationTag = _ticket.Tag;
        var owner = _robot.Engine.Robot!;
        RetainedSubscription.Replace(ref _ca, owner.SubscribeMessage(RobotMessageId.AnimStarted, m => Started(((AnimationStarted)m).AnimId), "PlayAnimation"));
        RetainedSubscription.Replace(ref _cb, owner.SubscribeMessage(RobotMessageId.AnimEnded, m => Ended(((AnimationEnded)m).AnimId), "PlayAnimation"));
        RetainedSubscription.Replace(ref _d5, owner.SubscribeMessage(RobotMessageId.AnimEventFromRobot, m => AnimationEvent((Cozmo.Protocol.AnimationEvent)m), "PlayAnimation"));
        RetainedSubscription.Replace(ref _aborted, _robot.Engine.AnimationMessages.SubscribeMessage(0x5F, true, p => Aborted((uint)p!)));
        return 0;
    }
    // fidelity: M8-020 — 0x005452F0..0x0054534E.
    private void Started(byte tag)
    {
        if (tag != _animationTag) return;
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "Unnamed", "PlayAnimation.StartAnimationHandler", $"Animation tag {tag} started");
        _started = true;
    }
    // 0x0054541C..0x005454F4: zero loops never sets ended; unsigned decrement.
    private void Ended(byte tag)
    {
        if (!_started || tag != _animationTag) return;
        if (_loops == 0)
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "Unnamed", "PlayAnimation.EndAnimationHandler.LoopingForever", $"Animation tag {tag} finished a loop, continuing until timeout or cancel");
        else if (--_loops != 0)
            _robot.Engine.EmitSourceLog(EngineLogLevel.Debug, "Unnamed", "PlayAnimation.FinishedLoop", $"Animation tag {tag} finished a loop, {unchecked((int)_loops)} left");
        else
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "Unnamed", "PlayAnimation.EndAnimationHandler", $"Animation tag {tag} ended");
            _ended = true;
        }
    }
    // 0x0054561C..0x0054569E. Generated legacy field names do not describe native payload semantics:
    // EventId is the animation tag (+4); Field2 is AnimEvent (+5), not the reverse.
    private void AnimationEvent(Cozmo.Protocol.AnimationEvent message)
    {
        if (!_started || (byte)message.EventId != _animationTag) return;
        byte eventId = message.Field2;
        string name = eventId <= 3 ? ((AnimEvent)eventId).ToString() : "(null)";
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "Unnamed", "PlayAnimation.AnimationEventHandler",
            $"Event {name} received at time {unchecked((int)message.Timestamp)} while playing animation tag {_animationTag}");
        _robot.Engine.BroadcastAnimationEvent(new(message.Timestamp, eventId));
    }
    // E2G 0x5F: 0x005457D8..0x00545862, no started gate and no byte truncation of the incoming tag.
    private void Aborted(uint tag)
    {
        if (tag != _animationTag) return;
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "Unnamed", "PlayAnimation.AbortAnimationHandler", $"Animation tag {tag} was aborted from running");
        _failed = true;
    }
    public override uint CheckIfDone() => _ended ? 0u : _failed ? 0x03000001u : EngineActionResult.Running;
    public override string? GetAnimationCompletionName()=>_clip; // current +78, not cached clip+90.
    public override byte? GetCompletionVariant()=>1;
    public override void WatcherEnding()
    {
        if(_destroyed)return;_destroyed=true;
        if(_robot.Animations.ClearStreamIfClipMatches(_cachedClip))
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","PlayAnimationAction.Destructor.StillStreaming",$"Action destructing, but AnimationStreamer is still streaming this animation({_clip}). TellingAnimationStreamer to stream null.");
        }
        // 0x00543E6A..0x00543E9E: UI 5F then D5, CB, CA before the base.
        _aborted?.Dispose(); _aborted = null;
        _d5?.Dispose(); _d5 = null;
        _cb?.Dispose(); _cb = null;
        _ca?.Dispose(); _ca = null;
        base.WatcherEnding();
    }
}

// Preserve the driving-only lift-safe constructor gate; the general trigger action has no extra lift mask.
internal sealed class TriggerLiftSafeAnimationAction : TriggerAnimationAction
{
    internal TriggerLiftSafeAnimationAction(CozmoRobot robot, DrivingAnimationRequest request, Action<string>? missing = null)
        : base(robot, request, missing, liftSafe: true) { }
}
