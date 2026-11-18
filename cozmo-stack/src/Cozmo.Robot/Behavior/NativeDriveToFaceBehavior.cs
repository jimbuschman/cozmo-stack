using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Factory 0x0059CB00, ctor 0x005DA550: IBehavior(config), two zero fields and a default SmartFaceID.
// VC_ComeHere.json has no derived parameters; its vtable inherits the common config loader and Resume.
internal sealed class NativeDriveToFaceBehavior : SteppedBehavior, IDisposable
{
    private readonly EngineRobot _robot;
    private readonly ManipulationSystem _m;
    private readonly NativeSmartFaceReference _target;
    private long _generation;
    private bool _retired;
    internal int NativeState { get; private set; }
    internal int TargetId => _target.Id;
    internal float TrackingDeadline { get; private set; }

    internal NativeDriveToFaceBehavior(BehaviorFactoryContext source, JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(), "DriveToFace")
    {
        _robot=source.NativeRobot!;
        _m=source.Manipulation!;
        _target=new(_m.Robot,owner:_robot);
    }
    private bool OwnerCurrent => !_retired && !_robot.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot,_robot)
        && _robot.NativeBehaviorRuntime is {} runtime && ReferenceEquals(runtime.Manipulation,_m)
        && runtime.Bound.TryGetValue(Id,out var actual) && ReferenceEquals(actual,this);
    private bool Current(long version)=>OwnerCurrent && Interlocked.Read(ref _generation)==version;
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version)=>Current(version);
    protected override bool HostStopContinuationCurrent(long version)=>!_retired && Interlocked.Read(ref _generation)==unchecked(version+1);
    protected override bool HostResumeContinuationCurrent(long version)=>OwnerCurrent &&
        (Interlocked.Read(ref _generation)==version || Interlocked.Read(ref _generation)==unchecked(version+1));
    // Vtable 0x01028AD8: false gates 0x005BF04C, 0x0059EC12, 0x005DAD54.
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;

    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);
        long version=CaptureHostExecutionVersion();
        return Current(version) && IsRunnableBase(context) && Current(version) && IsRunnableInternal(context);
    }
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        // 0x005DA5BC..0x005DA616: latest cached pose/current-origin check, then the first ascending
        // ID observed since that stamp. A foreign cached pose leaves the prior smart ID unchanged.
        long version=CaptureHostExecutionVersion();
        try
        {
            using var mutation=_m.World.EnterPoseMutation();
            var origin=_robot.GetNativeOrigin(_robot.CurrentOriginId);
            using var cached=new Pose3d("");
            uint stamp=_m.Vision.Faces.CopyNativeLastFaceTimestamp(origin,true,cached);
            if (PoseTreeNode.HasSameRoot(cached.Node,origin.Node))
            {
                var ids=_m.Vision.Faces.GetNativeFaceIds(_robot,stamp);
                if (!Current(version)) return false;
                if (ids.Count==0) _target.Reset();
                else
                {
                    using var selected=new NativeSmartFaceReference(_m.Robot,ids[0],_robot);
                    _target.CopyFrom(selected);
                }
            }
            return Current(version) && _target.IsValid;
        }
        catch(InvalidOperationException error)
        {
            MarkReadinessUnavailable();
            ReportMissing("DriveToFace native face readiness unavailable: "+error.Message);
            return false;
        }
    }

    protected override void OnStart()
    {
        if(!OwnerCurrent || !_target.IsValid)
        {
            InitFailed=true;
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorDriveToFace.InitInternal.NoValidFace",
                "Attempted to init behavior without a vaild face to drive to"); // 0x005DA688..0x005DA6BC.
            return;
        }
        Turn(CaptureHostExecutionVersion());
    }
    private bool State(int value,string name,long version)
    {
        if(!Current(version))return false;
        NativeState=value;
        return SetStateName(name,()=>Current(version));
    }

    private static void PreserveUnusedConstructorTags()
    {
        // 0x005DA7B0..0x005DA822 constructs a sequential group, turn and visual verifier, but
        // 0x005DA82C..0x005DA86E starts a SEPARATE turn. No sequence/child is queued.
        // Their constructors (0x0054F4C8/0x0054B754/0x00568EB8) only initialize private state,
        // apart from IActionRunner's global tag reservations (0x0053FE54..0x0053FEC8).
        // Keep those reservations, including in-use occupancy, without retaining unused objects.
        for(int i=0;i<3;i++)
        {
            uint tag;
            do{tag=ActionRunnerTagCounter.Global.NextIdTag();}while(!ActionRunnerTagCounter.Global.TryReserve(tag));
        }
    }
    private void Turn(long version)
    {
        if(!State(0,"TurnTowardsFace",version) || _m.Vision.Faces.GetNativeFace(_robot,_target.Id) is null)return;
        PreserveUnusedConstructorTags();
        if(!Current(version))return;
        // 0x005DA82C..0x005DA850: TurnTowardsFaceAction(robot, smartID, Pi=0x40490FDB, false).
        // The runner itself waits for images/fine-turns; the unused explicit verifier is not executed.
        var turn=new NativeLastFaceRunner(_m,_robot,_target.Id,false);
        if(!Current(version)){turn.RetireNeverAdmittedTree();return;}
        StartActing(turn,result=>
        {
            // Callback 0x005DADA8..0x005DADE6: only SUCCESS advances; failure runs out of actions.
            if(!Current(version) || result.Result!=0)return;
            if(TryDistance(out float distance) && distance<=200f) AlreadyClose(version);
            else Drive(version);
        },()=>Current(version));
    }

    internal bool TryDistance(out float distance)
    {
        // 0x005DAA20..0x005DAA96: copy entry+F4, replace its local Z with Robot::GetPose Z,
        // then ComputeDistanceBetween (0x00847780), including the same-parent vector shortcut.
        distance=0;
        using var mutation=_m.World.EnterPoseMutation();
        var face=_m.Vision.Faces.GetNativeFace(_robot,_target.Id);
        if(face is null)return false;
        if(face.NativeTrackingTranslation is null || face.HeadPose.Node is null)
        {ReportMissing("M14-001 DriveToFace native face pose unavailable at 0x005DAA36");return false;}
        using var flattened=new Pose3d(face.HeadPose);
        var main=_robot.GetCheckedMainPose();
        var point=flattened.Translation;
        flattened.Node!.Transform=new Transform3d(flattened.Quaternion,new Vec3(point.X,point.Y,main.Translation.Z));
        float squared=0;
        if(!PlacementProducerGeometry.TryDistanceSquared(flattened,main,ref squared))return false;
        distance=MathF.Sqrt(squared);
        return true;
    }
    private void Drive(long version)
    {
        if(!State(1,"DriveToFace",version) || _m.Vision.Faces.GetNativeFace(_robot,_target.Id) is null ||
            !TryDistance(out float distance) || !Current(version))return;
        // 0x005DA9A2..0x005DA9D6: distance-200f, speed220f, playAnimation=true, decel bits4326AAAB.
        var drive=new DriveStraightAction(_m,_robot,distance-200f,220f,true);
        drive.SetDecel(BitConverter.UInt32BitsToSingle(0x4326AAAB));
        StartActing(drive,_=>{if(Current(version))Track(version);},()=>Current(version));
    }
    private void AlreadyClose(long version)
    {
        if(!State(2,"AlreadyCloseEnough",version))return;
        // 0x005DAC6C..0x005DAC9E: TriggerAnimationAction(robot, trigger64, 1, true, tracks0, 60f, false).
        var animation=new TriggerAnimationAction(_m.Robot,new(AnimationTrigger.ComeHere_AlreadyHere,1,true,0,60f,false));
        StartActing(animation,_=>{if(Current(version))Track(version);},()=>Current(version));
    }
    private void Track(long version)
    {
        if(!State(3,"TrackFace",version))return;
        // 0x005DAB76..0x005DABBA: f32 now+5, resolve smartID, TrackFaceAction(robot, rawID), empty callback.
        TrackingDeadline=_robot.Engine.Timer.SecondsF+5f;
        var face=_m.Vision.Faces.GetNativeFace(_robot,_target.Id);
        if(face is null || !Current(version))return;
        var track=new NativeTrackFaceRunner(_m,_robot,face.Id);
        StartActing(track,null,()=>Current(version));
    }
    protected override void OnUpdate()
    {
        // 0x005DA8FC..0x005DA93C: strict deadline, StopActing(false,false), then DONE; otherwise base UpdateInternal.
        if(NativeState==3 && _robot.Engine.Timer.SecondsF>TrackingDeadline)StopActing(false,false);
    }
    protected override void OnStop(BehaviorStopReason reason)
    {
        Interlocked.Increment(ref _generation);
        _target.Reset(); // StopInternal 0x005DA93E.
    }
    public void Dispose(){_retired=true;Interlocked.Increment(ref _generation);_target.Dispose();}
}
