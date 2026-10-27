using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Vision;

// fidelity: M13-020
// Owned engine-ticked recipients. Public async/spec adapters remain separate.
internal abstract class NativeVisionRunner : ActionRunner
{
    protected readonly ManipulationSystem M;
    protected NativeVisionRunner(ManipulationSystem m,int type,uint tracks):base(type,tracks)
    {M=m;SourceLog=(level,channel,key,text)=>m.Robot.Engine.EmitSourceLog(level,channel,key,text);}
    protected override float EngineClockSeconds=>M.Robot.Engine.Timer.SecondsF;
    protected override bool AreAnyTracksLocked(uint mask)=>M.Robot.Motion.AreAnyTracksLocked((byte)mask);
    protected override void LockTracks(uint mask,string owner)=>M.Robot.Motion.LockTracks((byte)mask,owner,ActionName);
    protected override void UnlockTracksInternal(uint mask,string owner)=>M.Robot.Motion.UnlockTracks((byte)mask,owner);
    protected override bool AreAllTracksLockedBy(uint mask,string owner)=>M.Robot.Motion.AreAllTracksLockedBy((byte)mask,owner);
    protected override void StopHeadTrack()=>M.Robot.Motion.StopHead();
    protected override void StopLiftTrack()=>M.Robot.Motion.StopLift();
    protected override void StopBodyTrack()=>M.Robot.Motion.StopBody();
    protected override bool HeadTrackMoving=>M.Robot.Motion.HeadMoving;
    protected override bool LiftTrackMoving=>M.Robot.Motion.LiftMoving;
    protected override bool BodyTrackMoving=>M.Robot.Motion.BodyMoving;
    public override bool CanInterrupt()=>false; // five actual +14 slots ->0052B0B2 MOV0/BXLR.
}

internal sealed class NativeTurnInPlaceRunner : NativeVisionRunner
{
    private readonly TurnInPlaceAction _turn;
    protected override string ActionName=>"TurnInPlace";
    internal NativeTurnInPlaceRunner(ManipulationSystem m,TurnInPlaceSpec spec):base(m,0x28,4)
    {
        _turn=new TurnInPlaceAction(new MountChargerAction.LivePanTiltRobot(m,true),(float)spec.AngleRad,spec.IsAbsolute,Tag);
        _turn.SetTolerance((float)spec.ToleranceRad);_turn.Byte0xD8=spec.Byte0xD8;
        if(spec.MaxSpeedSet){_turn.SetMaxSpeed((float)spec.MaxSpeedRadPerSec);_turn.SetAccel((float)spec.AccelRadPerSec2);}
    }
    public override int Init(){_turn.BindOwnedRunnerTag(Tag);return unchecked((int)_turn.Init());}
    public override uint CheckIfDone()=>_turn.CheckIfDone();
    public override void WatcherEnding(){_turn.Dispose();base.WatcherEnding();}
}

internal class NativePanTiltRunner : NativeVisionRunner
{
    protected readonly CompoundActionParallel Compound;
    internal PanAndTiltAction Parameters {get;}
    protected override string ActionName=>"PanAndTilt";
    internal NativePanTiltRunner(ManipulationSystem m,PanAndTiltAction parameters,int type=0x14):base(m,type,5)
    {
        Parameters=parameters;
        Compound=new CompoundActionParallel(()=>m.Robot.Engine.Timer.SecondsF){SourceLog=SourceLog};
    }
    internal override ActionWatcher? Watcher
    {get=>base.Watcher;set{base.Watcher=value;Compound.Watcher=value;}}
    public override int Init()
    {
        Compound.ClearActions();
        Compound.TimeoutWarningEnabled=TimeoutWarningEnabled; //549C58..5C outer57 -> embeddedCF.
        var p=Parameters;
        var turn=new NativeTurnInPlaceRunner(M,new TurnInPlaceSpec(p.PanAngleRad,p.PanIsAbsolute,p.PanToleranceRad,p.Byte0x126,
            p.HasMaxSpeed,p.MaxSpeedRadPerSec,p.MaxAccelRadPerSec2,p.HasMaxSpeed&&p.MaxAccelRadPerSec2!=0));
        Compound.AddAction(turn,false,false);
        float head=p.HeadIsAbsolute?new Radians((float)p.HeadAngleRad).Value:new Radians(M.Robot.Motion.HeadAngleRad+(float)p.HeadAngleRad).Value;
        float speed=p.HasHeadSpeedAccel?BitConverter.UInt32BitsToSingle(p.HeadSpeedAccelWord0):15f;
        float accel=p.HasHeadSpeedAccel?BitConverter.UInt32BitsToSingle(p.HeadSpeedAccelWord1):20f;
        Compound.AddAction(M.Robot.Motion.CreateNativeHeadChild(head,(float)p.HeadToleranceRad,speed,accel),false,false);
        Compound.SuppressTrackLocking=true;
        uint result=Compound.Update();
        return (result|EngineActionResult.Running)==EngineActionResult.Running?0:unchecked((int)result);
    }
    public override uint CheckIfDone()=>Compound.Update();
    public override void Prep(){Compound.Prep();base.Prep();}
    public override void WatcherEnding(){Compound.WatcherEnding();base.WatcherEnding();}
}

internal class NativeTurnTowardsPoseRunner : NativePanTiltRunner
{
    private readonly TurnTowardsPoseCompound _pose;
    protected override string ActionName=>"TurnTowardsPose";
    internal NativeTurnTowardsPoseRunner(ManipulationSystem m,Pose3d pose,float maximumTurn,int type=0x2D,uint tracks=5)
        :base(m,new TurnTowardsPoseCompound(pose,maximumTurn),type){_pose=(TurnTowardsPoseCompound)Parameters;RequiredTrackMask=tracks;}
    protected void AssignGoal(Pose3d pose)=>_pose.Pose!.Assign(pose);
    internal bool TurnSkipped=>_pose.Byte0x179;
    internal Pose3d GoalPose=>_pose.Pose!;
    public override int Init()
    {
        using var mutation=M.World.EnterPoseMutation();
        var robot=M.Robot.Engine.Robot??throw new InvalidOperationException("MISSING: native TurnPose Robot owner unavailable");
        var main=robot.GetCheckedMainPose(); // borrowed live MAIN; stored relative pose retains this exact parent.
        var calibration=M.Vision.Calibration; // actual Camera retained calibration (API/NV install both write this owner).
        using var relative=new Pose3d("");
        var env=new TurnTowardsPoseEnv(main,M.Robot.Motion.HeadAngleRad,
            pose=>TurnTowardsPose.ComputeHeadAngleToSeePose(calibration,pose,TurnTowardsPoseCompound.SeePoseToleranceRad,out var angle,M.Vision.LogLine,main)==0?angle:null,
            null,pose=>pose.TryWithRespectTo(main,relative)?relative:null,
            M.Vision.LogLine,SourceLog,robot.GetNativeOrigin(robot.CurrentOriginId),M.Robot.Engine.Das.ForceFlushNow);
        uint result=_pose.InitPose(env,out bool build);
        return build?base.Init():unchecked((int)result);
    }
    public override uint CheckIfDone()=>_pose.Byte0x179?EngineActionResult.Success:base.CheckIfDone();
    public override void WatcherEnding(){_pose.Pose?.Dispose();base.WatcherEnding();}
}

internal sealed class NativeWaitForImagesRunner : NativeVisionRunner
{
    private readonly WaitForImagesAction _wait;
    private RetainedSubscription? _subscription;
    protected override string ActionName=>"WaitForImages";
    internal NativeWaitForImagesRunner(ManipulationSystem m,int count,VisionMode mode,uint after):base(m,0x32,0)
    {_wait=new(count,mode,after);}
    internal int Count=>_wait.Count;
    internal bool Subscribed=>_subscription is not null;
    public override int Init()
    {
        _wait.Init();
        var replacement=M.Robot.Engine.AnimationMessages.SubscribeMessage(0x43,true,payload=>
        {if(payload is ProcessedImageReport report)_wait.HandleRobotProcessedImage(report.Timestamp,report.Modes.Select(mode=>(VisionMode)mode).ToArray());});
        var previous=_subscription;_subscription=replacement;previous?.Dispose();
        return 0;
    }
    public override uint CheckIfDone()
    {uint result=_wait.CheckIfDone();if(result==0){_subscription?.Dispose();_subscription=null;}return result;}
    public override void WatcherEnding(){_subscription?.Dispose();_subscription=null;base.WatcherEnding();}
}

internal sealed class NativeVisuallyVerifyNoObjectRunner : NativeVisionRunner
{
    private readonly Pose3d _pose;
    private readonly Vec3 _halfSize;
    private readonly BlockWorldFilter _filter=new();
    private CompoundActionParallel? _position;
    private NativeWaitForImagesRunner? _wait;
    protected override string ActionName=>"VisuallyVerifyNoObjectAtPose";
    internal NativeVisuallyVerifyNoObjectRunner(ManipulationSystem m,Pose3d pose,Vec3 halfSize):base(m,0x30,5)
    {
        _pose=new Pose3d(pose);_halfSize=halfSize;
        _filter.IgnoreFamilies.Add((ObjectFamily)6);
        _filter.Predicates.Add(obj=>obj.LastObservedTimestamp>=M.Robot.CameraSettings.LastProcessedImageTimestamp);
    }
    internal void AddIgnoreId(uint id){if(State!=EngineActionResult.NotStarted)throw new InvalidOperationException("Ignore ID requires NOT_STARTED");_filter.IgnoreIds.Add(id);}
    internal bool Positioning=>_position is not null;
    internal NativeWaitForImagesRunner? Wait=>_wait;
    internal override ActionWatcher? Watcher
    {get=>base.Watcher;set{base.Watcher=value;if(_position is not null)_position.Watcher=value;if(_wait is not null)_wait.Watcher=value;}}
    private void RetireChildren()
    {
        _position?.Prep();_wait?.Prep();
        _wait?.WatcherEnding();_wait=null;
        _position?.WatcherEnding();_position=null;
    }
    public override int Init()
    {
        RetireChildren();
        _position=new CompoundActionParallel(()=>M.Robot.Engine.Timer.SecondsF){SourceLog=SourceLog,SuppressTrackLocking=true};
        _position.AddAction(new NativeTurnTowardsPoseRunner(M,_pose,Radians.Pi),false,false);
        _position.AddAction(M.Robot.Motion.CreateNativeLiftChild(CozmoMotion.OutOfFovHeightMm,5f),false,false);
        _position.Watcher=Watcher;
        _wait=new NativeWaitForImagesRunner(M,10,(VisionMode)1,0){Watcher=Watcher};
        return 0;
    }
    public override uint CheckIfDone()
    {
        if(_position is not null)
        {
            uint result=_position.Update();if(result!=0)return result;
            _position.Prep();_position.WatcherEnding();_position=null;
            return EngineActionResult.Running; // distinct positioning-success transition tick.
        }
        if(_wait is null)return 0x03000012;
        uint waited=_wait.Update();
        using var mutation=M.World.EnterPoseMutation();
        var match=NativeClosestSpatialQuery.Find(M.World,_pose,_halfSize,_filter);
        return match is null?waited:0x0300001D;
    }
    public override void Prep(){_position?.Prep();_wait?.Prep();base.Prep();}
    public override void WatcherEnding(){RetireChildren();_pose.Dispose();base.WatcherEnding();}
}