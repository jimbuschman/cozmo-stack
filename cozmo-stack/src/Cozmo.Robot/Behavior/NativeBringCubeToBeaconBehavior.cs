using System.Text.Json;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// Checked Hiking own lifecycle, 005DEF B0..005E1D50; shared children retain their records.
internal sealed class NativeBringCubeToBeaconBehavior:SteppedBehavior,IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly float _cooldown;
    private readonly NativeBringBeaconCandidates _candidates;
    private readonly NativeBringBeaconGeometry _geometry;
    private readonly BringCubeToBeaconBehavior _floor;
    private long _generation;
    private int _retired;
    private Pose3d? _retryPose;
    internal Action<IActionRunner>? ActionConstructedForTest;
    internal uint Target {get;private set;}=uint.MaxValue;
    internal NativeBringCubeToBeaconBehavior(BehaviorFactoryContext source,JsonElement config,EngineRobot robot)
        :base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(),"BringCubeToBeacon")
    {
        _m=source.Manipulation!;_robot=robot;_cooldown=config.GetProperty("params").GetProperty("recentFailureCooldown_sec").GetSingle();
        _candidates=new(_m,robot,_cooldown);_geometry=new(_m,robot);_floor=new(_m,Id,_cooldown);
    }
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>_m.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version)=>Volatile.Read(ref _retired)==0&&Interlocked.Read(ref _generation)==version;
    protected override bool HostStopContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0&&Interlocked.Read(ref _generation)==unchecked(version+1);
    protected override bool HostResumeContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0&&(Interlocked.Read(ref _generation)==version||Interlocked.Read(ref _generation)==unchecked(version+1));
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>true;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);long epoch=CaptureHostExecutionVersion();
        if(!HostExecutionVersionCurrent(epoch)||!IsRunnableBase(context)||!HostExecutionVersionCurrent(epoch))return false;
        return RunnableCore(epoch);
    }
    protected override bool IsRunnableInternal(BehaviorContext context)=>RunnableCore(CaptureHostExecutionVersion());
    private bool RunnableCore(long epoch)
    {
        try{return _candidates.Refresh(()=>HostExecutionVersionCurrent(epoch))&&HostExecutionVersionCurrent(epoch);}
        catch(NativeConfigurationUnavailable error){if(HostExecutionVersionCurrent(epoch))ReportMissing("BringCubeToBeacon candidate input unavailable: "+error.Message);return false;}
    }
    protected override void OnStart()
    {
        long epoch=CaptureHostExecutionVersion();if(!HostExecutionVersionCurrent(epoch))return;
        Target=uint.MaxValue;
        try
        {
            if(_robot.CarryingOwner!.CarriedObjectId is not null)
            {
                if(_candidates.Entries.Count==0){ReportMissing("M1-034 M15-026 carried Init vector[0] at 005DF36A; empty cache, skip Init");if(HostExecutionVersionCurrent(epoch))InitFailed=true;return;}
                Target=_candidates.Entries[0].Id;PickedUp(epoch);
            }
            else PickUp(1,epoch);
            if(!HostExecutionVersionCurrent(epoch))return;
            if(!HasCurrentAction)
            {
                var beacon=_m.Whiteboard.GetActiveBeacon();
                if(beacon is null){ReportMissing("M1-034 M15-026 null active beacon at 005DF396; skip Init tail");if(HostExecutionVersionCurrent(epoch))InitFailed=true;return;}
                if(!(MathF.Abs(_robot.Engine.Timer.SecondsF-beacon.FailedToFindLocationTimeSec)<BitConverter.UInt32BitsToSingle(0x3727C5AC)))InitFailed=true;
            }
        }
        catch(NativeActionUnavailable error){if(HostExecutionVersionCurrent(epoch)){Unavailable(error,epoch);if(HostExecutionVersionCurrent(epoch))InitFailed=true;}}
        catch(NativeConfigurationUnavailable error){if(HostExecutionVersionCurrent(epoch)){Unavailable(new NativeActionUnavailable(error.Message),epoch);if(HostExecutionVersionCurrent(epoch))InitFailed=true;}}
    }
    private void Unavailable(NativeActionUnavailable error,long epoch)
    {
        if(!HostExecutionVersionCurrent(epoch))return;uint tag=CurrentRunnerTag;
        ReportMissing("BringCubeToBeacon unavailable: "+error.Message);
        if(!HostExecutionVersionCurrent(epoch))return;
        _robot.ActionList.DeferHostCleanup(()=>
        {
            if(!HostExecutionVersionCurrent(epoch)||CurrentRunnerTag!=tag)return;
            var manager=_robot.BehaviorManagerOwner;
            if(ReferenceEquals(manager?.Current,this))manager.Stop(BehaviorStopReason.Interrupted,_robot.Engine.Timer.SecondsF);
            else Stop(BehaviorStopReason.Interrupted);
        });
    }
    private void PickUp(int attempt,long epoch)
    {
        if(!HostExecutionVersionCurrent(epoch)||_candidates.Entries.Count==0)return;
        if(Target==uint.MaxValue){uint selected=_candidates.Select(()=>HostExecutionVersionCurrent(epoch));if(!HostExecutionVersionCurrent(epoch))return;Target=selected;}
        if(Target==uint.MaxValue)return;
        var runner=new NativeDriveToPickupBeaconRunner(_m,Target,()=>HostExecutionVersionCurrent(epoch));runner.Unavailable=e=>Unavailable(e,epoch);
        if(!HostExecutionVersionCurrent(epoch)){runner.RetireNeverAdmittedTree();return;}
        ActionConstructedForTest?.Invoke(runner);
        if(!HostExecutionVersionCurrent(epoch)){runner.RetireNeverAdmittedTree();return;}
        StartActing(runner,done=>InvokeAvailable(epoch,()=>PickupDone(done.Result,attempt,epoch)),()=>HostExecutionVersionCurrent(epoch));
    }
    private void PickupDone(uint result,int attempt,long epoch)
    {
        uint category=result>>24;
        if(category==0||(category==4&&_robot.CarryingOwner!.CarriedObjectId==Target)){PickedUp(epoch);return;}
        if(category==4&&attempt<=2){PickUp(attempt+1,epoch);return;}
        if(category is not (3 or 4))return;
        var obj=_m.World.GetLocatedObjectByIdHelper(Target);
        if(HostExecutionVersionCurrent(epoch)&&obj is not null)_m.Whiteboard.SetFailedToUse(obj,ObjectActionFailure.PickUpObject);
    }
    private void PickedUp(long epoch)
    {
        if(!HostExecutionVersionCurrent(epoch))return;
        using var mutation=_m.World.EnterPoseMutation();
        if(_robot.CarryingOwner!.CarriedObjectId!=Target)return;
        var carried=_m.World.GetLocatedObjectByIdHelper(Target);var beacon=_m.Whiteboard.GetActiveBeacon();
        if(!HostExecutionVersionCurrent(epoch)||carried is null||beacon is null)return;
        if(_robot.Progression is not {} progression||!progression.TryIsUnlocked(10,out bool unlocked))
            throw new NativeActionUnavailable("M15-008 actual StackTwoCubes unlock10 unavailable");
        if(!HostExecutionVersionCurrent(epoch))return;
        if(unlocked)
        {
            using var flat=carried.NativePose.GetWithRespectToRoot();
            float margin=(float)NativeConfigurationGeometry.Dimensions(carried,flat).X+10f;
            var filter=new BlockWorldFilter();filter.AllowedFamilies.Add(ObjectFamily.Block);filter.AllowedFamilies.Add(ObjectFamily.LightCube);
            var cache=_robot.NativeBehaviorRuntime?.PickupTargets??throw new NativeActionUnavailable("M15-020 actual CanStack owner unavailable");
            filter.Predicates.Add(o=>{
                if(!HostExecutionVersionCurrent(epoch)||ReferenceEquals(o,carried)||o.PoseState!=PoseState.Known)return false;
                bool failed=_m.Whiteboard.DidFailToUse(unchecked((int)o.ObjectId),ObjectActionFailure.StackOnObject,_cooldown,o.NativePose,20f,BitConverter.UInt32BitsToSingle(0x3EC90FDB));
                if(!HostExecutionVersionCurrent(epoch)||failed)return false;
                bool within=beacon.IsLocWithinBeacon(o.NativePose,margin);
                if(!HostExecutionVersionCurrent(epoch)||!within)return false;
                bool can=cache.CanStack(o);return HostExecutionVersionCurrent(epoch)&&can;
            });
            var stack=_m.World.FindLocatedMatchingObjects(filter).FirstOrDefault();
            if(!HostExecutionVersionCurrent(epoch))return;
            if(stack is not null){Stack(stack.ObjectId,1,epoch);return;}
        }
        if(!HostExecutionVersionCurrent(epoch))return;
        _robot.Engine.Needs.RegisterNeedsActionCompleted("PickupCube");
        if(!HostExecutionVersionCurrent(epoch))return;
        if(!CubeGeometry.IsCube(carried.Type))throw new NativeActionUnavailable("M15-019 native carried-object virtual dimensions unavailable");
        if(_floor.FindFreePoseInBeacon(carried,beacon,_robot.GetCheckedMainPose(),_cooldown,out var pose,b=>_geometry.Cubes(b,()=>HostExecutionVersionCurrent(epoch)),(o,p)=>_geometry.Free(o,p,()=>HostExecutionVersionCurrent(epoch)),()=>HostExecutionVersionCurrent(epoch)))
        {if(HostExecutionVersionCurrent(epoch))Place(pose,1,epoch);return;}
        if(!HostExecutionVersionCurrent(epoch))return;
        _m.Whiteboard.FailedToFindLocationInBeacon(beacon);
        if(HostExecutionVersionCurrent(epoch))Mood("HikingNoLocationAtBeacon");
    }
    private void Stack(uint stack,int attempt,long epoch)
    {
        if(!HostExecutionVersionCurrent(epoch))return;
        var runner=new NativeDriveToPlaceOnBeaconRunner(_m,stack,()=>HostExecutionVersionCurrent(epoch)){Unavailable=e=>Unavailable(e,epoch)};
        if(!HostExecutionVersionCurrent(epoch)){runner.RetireNeverAdmittedTree();return;}
        ActionConstructedForTest?.Invoke(runner);
        if(!HostExecutionVersionCurrent(epoch)){runner.RetireNeverAdmittedTree();return;}
        StartActing(runner,done=>InvokeAvailable(epoch,()=>
        {
            uint c=done.Result>>24;
            if(c==0){_robot.Engine.Needs.RegisterNeedsActionCompleted("StackCube");if(HostExecutionVersionCurrent(epoch))Emotion(epoch);return;}
            if(c==4&&_robot.CarryingOwner!.CarriedObjectId==Target&&attempt<=2){Stack(stack,attempt+1,epoch);return;}
            if(c is 3 or 4){var o=_m.World.GetLocatedObjectByIdHelper(stack);if(HostExecutionVersionCurrent(epoch)&&o is not null)_m.Whiteboard.SetFailedToUse(o,ObjectActionFailure.StackOnObject);}
        }),()=>HostExecutionVersionCurrent(epoch));
    }
    private void Place(Pose3d pose,int attempt,long epoch)
    {
        if(!HostExecutionVersionCurrent(epoch))return;
        var next=new Pose3d(pose);_retryPose?.Dispose();_retryPose=next;
        var runner=new NativePlaceGroundAtBeaconRunner(_m,next,()=>HostExecutionVersionCurrent(epoch));
        if(!HostExecutionVersionCurrent(epoch)){runner.RetireNeverAdmittedTree();return;}
        ActionConstructedForTest?.Invoke(runner);
        if(!HostExecutionVersionCurrent(epoch)){runner.RetireNeverAdmittedTree();return;}
        runner.Unavailable=e=>Unavailable(e,epoch);
        runner.Drive.Unavailable=e=>Unavailable(e,epoch);
        if(!HostExecutionVersionCurrent(epoch)){runner.RetireNeverAdmittedTree();return;}
        StartActing(runner,done=>InvokeAvailable(epoch,()=>
        {
            uint c=done.Result>>24;
            if(c==0){Emotion(epoch);return;}
            if(c==4&&_robot.CarryingOwner!.CarriedObjectId==Target&&attempt<=2){Place(next,attempt+1,epoch);return;}
            var o=_m.World.GetLocatedObjectByIdHelper(Target);if(HostExecutionVersionCurrent(epoch)&&o is not null)_m.Whiteboard.SetFailedToUse(o,ObjectActionFailure.PlaceObjectAt,next);
        }),()=>HostExecutionVersionCurrent(epoch));
    }
    private void InvokeAvailable(long epoch,Action action)
    {
        try{if(HostExecutionVersionCurrent(epoch))action();}
        catch(NativeActionUnavailable error){if(HostExecutionVersionCurrent(epoch))Unavailable(error,epoch);}
        catch(NativeConfigurationUnavailable error){if(HostExecutionVersionCurrent(epoch))Unavailable(new NativeActionUnavailable(error.Message),epoch);}
    }
    private void Mood(string name)
    {if(Context.Mood is {} mood)mood.Trigger(name,_robot.Engine.Timer.SecondsF);else ReportMissing("BringCubeToBeacon actual MoodManager unavailable");}
    private void Emotion(long epoch)
    {
        bool all=_m.Whiteboard.AreAllCubesInBeacons(()=>HostExecutionVersionCurrent(epoch));
        if(HostExecutionVersionCurrent(epoch))Mood(all?"HikingBroughtLastCubeToBeacon":"HikingBroughtCubeToBeacon");
    }
    protected override void OnStop(BehaviorStopReason reason){Interlocked.Increment(ref _generation);_retryPose?.Dispose();_retryPose=null;}
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);_retryPose?.Dispose();_retryPose=null;RetireActionCompletionOwner();}
}
