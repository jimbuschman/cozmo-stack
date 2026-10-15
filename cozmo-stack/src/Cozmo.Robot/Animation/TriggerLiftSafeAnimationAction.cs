using Cozmo.Protocol;
namespace Cozmo.Robot.Animation;

// fidelity: M5-063, M8-020, M8-021
// ActionList runner for the checked driving calls. Playback uses the existing streamer.
// The unported concrete-action callback/union/DAS portions stay visible, never source-complete.
internal sealed class TriggerLiftSafeAnimationAction : ActionRunner
{
    private readonly CozmoRobot _robot;
    internal readonly DrivingAnimationRequest Request;
    private readonly string? _group;
    private AnimationTicket? _ticket;
    private string _clip="";
    private bool _destroyed;
    private readonly Action<string> _missing;
    internal TriggerLiftSafeAnimationAction(CozmoRobot robot, DrivingAnimationRequest request, Action<string>? missing = null) : base(0x1A, (uint)request.Argument4)
    {
        _robot=robot;Request=request;_missing=missing??Behavior.SteppedBehavior.ReportMissing;
        // 0x00543C96..0x00543CB2: only forever loops with the default 60.0f timeout.
        TimeoutSeconds=request.Argument2==0 && request.Argument5==BitConverter.Int32BitsToSingle(0x42700000)
            ? BitConverter.Int32BitsToSingle(0x7F7FFFFF) : request.Argument5;
        SourceLog=(level,channel,key,text)=>robot.Engine.EmitSourceLog(level,channel,key,text);Log=robot.Engine.EmitStackLog;
        // Checked M8-005/007 lift-safe ctor gate, 0x00544714..0x00544732.
        if(robot.Motion.IsCarryingObject?.Invoke()==true && robot.Sensors.OffTreadsState==OffTreadsState.OnTreads)RequiredTrackMask|=2;
        _group=robot.Animations.DrivingTriggerMap?.GroupFor(request.Trigger);
        if(_group is not null)
        {
            robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Animations","GetResponseForAnimationTrigger.Found",$"{request.Trigger} -> {_group}");
            if(_group.Length==0)robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"TriggerAnimationAction.EmptyAnimGroupNameForTrigger",$"Event: {request.Trigger}");
        }
        Missing("M8-021/M8-005 concrete TriggerLiftSafeAnimationAction callback lifecycle is incomplete: CA/CB/D5/5F subscriptions, completion string union, trigger DAS blacklist/event and cached-pointer retry/destruction semantics; existing playback ticket supplies completion so driving does not remain RUNNING forever");
    }
    private void Missing(string text)=>_missing(text);
    protected override string ActionName=>"PlayAnimation"+(_group??"");
    protected override float EngineClockSeconds=>_robot.Engine.Timer.SecondsF;
    public override bool CanInterrupt()=>false;
    protected override bool AreAnyTracksLocked(uint mask)=>_robot.Motion.AreAnyTracksLocked((byte)mask);
    protected override void LockTracks(uint mask,string owner)=>_robot.Motion.LockTracks((byte)mask,owner,ActionName);
    protected override void UnlockTracksInternal(uint mask,string owner)=>_robot.Motion.UnlockTracks((byte)mask,owner);
    protected override bool AreAllTracksLockedBy(uint mask,string owner)=>_robot.Motion.AreAllTracksLockedBy((byte)mask,owner);
    public override int Init()
    {
        if(string.IsNullOrEmpty(_group))
        {
            if(_robot.Animations.DrivingTriggerMap is null)Missing("M8-021 trigger-map input unavailable; no animation name can be resolved");
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"TriggerAnimationAction.NoAnimationForTrigger",$"Event: {Request.Trigger}");
            return 0x0300000C;
        }
        var library=_robot.Animations.Library;
        if(library is null){Missing("M8-021 animation-library input unavailable; no playback can be started");return 0x0300000C;}
        _clip=library.GetAnimationNameFromGroup(_group,Request.Argument6);
        if(_clip.Length==0)
        {
            return 0x0300000C;
        }
        _ticket=_robot.Animations.PlayTracked(_clip,Request.Argument3,numLoops:Request.Argument2);
        return _ticket is null?0x03000001:0;
    }
    public override uint CheckIfDone()
    {
        if(_ticket is null)return 0x03000001;
        if(!_ticket.Completion.IsCompleted)return EngineActionResult.Running;
        return _ticket.Completion.IsCompletedSuccessfully && _ticket.Completion.Result==AnimationEndReason.Completed?0u:0x03000001;
    }
    public override void WatcherEnding()
    {
        if(_destroyed)return;_destroyed=true;
        if(_ticket is {} ticket && !ticket.Completion.IsCompleted && _robot.Animations.Generation==ticket.Generation)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","PlayAnimationAction.Destructor.StillStreaming",$"Action destructing, but AnimationStreamer is still streaming this animation({_clip}). TellingAnimationStreamer to stream null.");
            _robot.Animations.StopIfCurrent(ticket.Generation);
        }
        else if(_ticket is null)Missing("M8-020 null cached Animation pointer destructor/idle Abort path 0x00543DAC..0x00543E6A is not represented by a playback ticket");
        base.WatcherEnding();
    }
}
