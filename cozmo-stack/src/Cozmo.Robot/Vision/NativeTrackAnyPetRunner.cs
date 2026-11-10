using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

// TrackPetFaceAction type-0 profile, 0x56833C..0x5686C8.
internal sealed class NativeTrackAnyPetRunner : NativeVisionRunner
{
    private readonly EngineRobot _owner;
    private readonly object _stopGate=new();
    private bool _ending;
    private long _endingGeneration;
    private uint _timestamp;
    private float _lastUpdate, _savedIdle=-1;
    private static readonly float Tolerance=BitConverter.UInt32BitsToSingle(0x3D0EFA35);
    private bool OwnerCurrent=>!_owner.Lifetime.Destroying && ReferenceEquals(_owner.Engine.Robot,_owner);
    private bool Current=>OwnerCurrent && ScopedCurrent && !_ending && !DestructionObserved;
    private bool CleanupCurrent=>OwnerCurrent && (!_ending || Interlocked.Read(ref _owner.NativeTrackingFaceGeneration)==_endingGeneration);
    protected override string ActionName=>"TrackAnyPetFace";
    internal uint LastImageTimestamp=>_timestamp;
    internal NativeTrackAnyPetRunner(ManipulationSystem m,EngineRobot owner):base(m,0x26,5){_owner=owner;ScopedOwner=owner;}
    public override int Init()
    {
        if(!Current)return unchecked((int)EngineActionResult.Cancelled);
        M.Robot.Animations.WithTrackLayers(_=>
        {
            var p=M.Robot.Animations.Scheduler.LiveIdleParameters;
            _savedIdle=p[LiveIdleParam.EyeDartMaxDistance_pix];p.SetParam(LiveIdleParam.EyeDartMaxDistance_pix,1f);
        });
        if(!Current)return unchecked((int)EngineActionResult.Cancelled);
        Interlocked.Increment(ref _owner.NativeTrackingFaceGeneration);
        _timestamp=0;_lastUpdate=_owner.Engine.Timer.SecondsF;
        return 0;
    }
    public override uint CheckIfDone()
    {
        if(!Current)return EngineActionResult.Cancelled;
        float now=_owner.Engine.Timer.SecondsF;
        var pets=M.Vision.Pets.GetNativePets(_owner);
        if(pets is null){Behavior.SteppedBehavior.ReportMissing("M14-004 TrackAnyPetFace native pet owner unavailable");return NoInformation(now);}
        var pet=pets.FirstOrDefault();
        if(pet is null)
        {
            _owner.Engine.EmitSourceLog(EngineLogLevel.Info,"Actions","TrackPetFaceAction.UpdateTracking.NoPetsWithType","Type=Unknown");
            return Current?NoInformation(now):EngineActionResult.Cancelled;
        }
        if(pet.LastObservedTimestamp<=_timestamp)return NoInformation(now);
        _timestamp=pet.LastObservedTimestamp;
        float x=(float)pet.Rect.X+(float)pet.Rect.Width*.5f;
        float y=(float)pet.Rect.Y+(float)pet.Rect.Height*.5f;
        if(!NativeImagePointRunner.TryNativeAngles(M,_owner,x,y,_timestamp,()=>Current,out float pan,out float tilt))
        {
            if(!Current)return EngineActionResult.Cancelled;
            _owner.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"TrackpetFaceAction.UpdateTracking.ComputeTurnTowardsImagePointAnglesFailed",$"t={_timestamp}");
            return Current?NoInformation(now):EngineActionResult.Cancelled;
        }
        if(!Current)return EngineActionResult.Cancelled;
        float maxTilt=BitConverter.UInt32BitsToSingle(0x3F46D3F2);
        if(new Radians(tilt)>new Radians(maxTilt))tilt=maxTilt;
        _lastUpdate=now;
        float delta=Radians.Rescale(tilt-M.Robot.Motion.HeadAngleRad);
        if(MathF.Abs(delta)>=Tolerance-Radians.Epsilon)
        {
            uint result=M.Robot.Motion.MoveHeadToAngle(tilt,MathF.Abs(delta)/.15f,10000f,0,out _,_owner,()=>Current);
            if(!Current)return EngineActionResult.Cancelled;
            if(result!=0)return 0x03000016;
        }
        float yaw;
        using(LogCallbackDeferral.EnterGated(M.World.FrameGate))
        using(LogCallbackDeferral.EnterGated(_owner.PoseTreeGate))yaw=(float)_owner.GetCheckedMainPose().AngleAroundZ;
        if(!Current)return EngineActionResult.Cancelled;
        float panDelta=Radians.Rescale(pan-yaw);
        if(MathF.Abs(panDelta)<Tolerance-Radians.Epsilon)return EngineActionResult.Running;
        if(M.Robot.Sensors.OffTreadsState!=OffTreadsState.OnTreads)
        {
            _owner.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"ITrackAction.CheckIfDone.OffTreadsStateInvalid",
                $"[{Tag}] Off tread state {M.Robot.Sensors.OffTreadsState} is invalid for turning in place");
            return Current?0x0300000A:EngineActionResult.Cancelled;
        }
        float target;
        using(LogCallbackDeferral.EnterGated(M.World.FrameGate))
        using(LogCallbackDeferral.EnterGated(_owner.PoseTreeGate))
        { _=_owner.GetCheckedForwardPose();target=Rotation3d.FromAngleAxis(pan,new Vec3(0,0,1)).AngleAroundZ; }
        if(!Current)return EngineActionResult.Cancelled;
        uint sent=M.Robot.Motion.TurnInPlace(target,MathF.Min(MathF.Abs(panDelta)/.4f,BitConverter.UInt32BitsToSingle(0x40A78D36)),
            BitConverter.UInt32BitsToSingle(0x436DFFDB),Tolerance,0,true,out _,_owner,()=>Current);
        if(!Current)return EngineActionResult.Cancelled;
        return sent==0?EngineActionResult.Running:0x03000016;
    }
    private uint NoInformation(float now)=>_lastUpdate>0 && now-_lastUpdate>3f?EngineActionResult.Success:EngineActionResult.Running;
    public override bool CanInterrupt()=>true;
    public override byte? GetCompletionVariant()=>3;
    public override uint GetCompletionUnion()=>0;
    protected override void LockTracks(uint mask,string owner)=>LogCallbackDeferral.RunGated(_stopGate,()=>{if(Current)M.Robot.Motion.LockTracksOwned((byte)mask,owner,ActionName,_owner);});
    protected override void UnlockTracksInternal(uint mask,string owner)=>LogCallbackDeferral.RunGated(_stopGate,()=>{if(OwnerCurrent)M.Robot.Motion.UnlockTracksOwned((byte)mask,owner,_owner);});
    protected override void StopHeadTrack()=>LogCallbackDeferral.RunGated(_stopGate,()=>{if(CleanupCurrent)M.Robot.Motion.StopHeadOwned(_owner,()=>CleanupCurrent);});
    protected override void StopBodyTrack()=>LogCallbackDeferral.RunGated(_stopGate,()=>{if(CleanupCurrent)M.Robot.Motion.StopBodyOwned(_owner,()=>CleanupCurrent);});
    public override void WatcherEnding()
    {
        if(_ending)return;_ending=true;_endingGeneration=Interlocked.Read(ref _owner.NativeTrackingFaceGeneration);
        LogCallbackDeferral.RunGated(_stopGate,()=>
        {
            if(CleanupCurrent && _savedIdle>=0)M.Robot.Animations.WithTrackLayers(_=>M.Robot.Animations.Scheduler.LiveIdleParameters.SetParam(LiveIdleParam.EyeDartMaxDistance_pix,_savedIdle));
            if(CleanupCurrent && State!=EngineActionResult.NotStarted){StopBodyTrack();if(CleanupCurrent)StopHeadTrack();}
            if(CleanupCurrent)M.Robot.Animations.DrivingAnimations.ActionIsBeingDestroyed(_owner,()=>CleanupCurrent);
            base.WatcherEnding();
        });
    }
}
