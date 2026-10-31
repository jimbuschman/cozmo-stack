using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using System.Reflection;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PickupChildrenNativeTests
{
    private static Pose3d At(Fixture f,float x,float y=0,float yaw=0)=>new(Rotation3d.FromAngleAxis(yaw,new(0,0,1)),new(x,y,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
    private sealed class Parent:SteppedBehavior
    {
        internal Action<Parent>? Entry;
        internal Parent():base("pickup-children-probe","Probe"){}
        public override bool IsRunnable(BehaviorContext context)=>true;
        protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
        protected override void OnStart()=>Entry?.Invoke(this);
        internal bool Delegate(HelperRef helper,Action<CozmoRobot>? success=null,Action<CozmoRobot>? fail=null)=>SmartDelegateToHelper(helper,success,fail);
    }
    [Fact]
    public void PanTiltSettersKeepOverrideFlagsOnZeroAndReachActualMotorChildren()
    {
        // reach: production native owned runner, supplied offline robot state. S1–S6 5498A0..549BDA.
        using var f=new Fixture();var p=new PanAndTiltAction{PanAngleRad=.5,HeadAngleRad=.1,HeadIsAbsolute=true};
        p.SetMaxPanSpeed(-100);p.SetPanAccel(3);p.SetMaxPanSpeed(-0f);
        p.SetMaxTiltSpeed(2);p.SetTiltAccel(4);p.SetMaxTiltSpeed(0);
        Assert.True(p.HasMaxSpeed);Assert.True(p.HasHeadSpeedAccel);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x40A78D36),p.MaxSpeedRadPerSec);
        Assert.Equal(0x41700000u,p.HeadSpeedAccelWord0);
        p.SetPanTolerance(.001f);p.SetTiltTolerance(.001f);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x3D0EFA35),p.PanToleranceRad);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x3D0EFA35),p.HeadToleranceRad);
        var runner=new NativePanTiltRunner(f.M,p);f.Robot.ActionList.QueueAction(QueueActionPosition.Now,runner);
        try
        {
            f.Rig.Tick();
            var body=Assert.Single(f.Rig.SentSince(0).OfType<SetBodyAngle>());var head=Assert.Single(f.Rig.SentSince(0).OfType<SetHeadAngle>());
            Assert.Equal(BitConverter.UInt32BitsToSingle(0x40A78D36),body.MaxSpeedRadPerSec);Assert.Equal(3f,body.AccelRadPerSec2);
            Assert.Equal(15f,head.MaxSpeedRadPerSec);Assert.Equal(4f,head.AccelRadPerSec2);
        }
        finally{f.Robot.ActionList.Clear();}
    }
    [Fact]
    public void SetterRawAndClampBranchesAreIndependentPerAxis()
    {
        // reach: unit, parameter owner only; S1–S6 exact bits/NaN/negative/default branches.
        var p=new PanAndTiltAction();p.SetMaxPanSpeed(0);p.SetPanAccel(-0f);p.SetMaxTiltSpeed(0);p.SetTiltAccel(-0f);
        Assert.False(p.HasMaxSpeed);Assert.False(p.HasHeadSpeedAccel);
        p.SetMaxPanSpeed(float.NegativeInfinity);Assert.Equal(0xC0A78D36u,BitConverter.SingleToUInt32Bits((float)p.MaxSpeedRadPerSec));
        p.SetMaxPanSpeed(float.NaN);Assert.True(float.IsNaN((float)p.MaxSpeedRadPerSec));
        p.SetPanAccel(-3);Assert.Equal(-3,p.MaxAccelRadPerSec2);Assert.False(p.HasHeadSpeedAccel);
        p.SetMaxTiltSpeed(-100);p.SetTiltAccel(float.NaN);Assert.Equal(-100f,BitConverter.UInt32BitsToSingle(p.HeadSpeedAccelWord0));Assert.True(float.IsNaN(BitConverter.UInt32BitsToSingle(p.HeadSpeedAccelWord1)));
        p.SetPanTolerance(-.25f);Assert.Equal(.25f,p.PanToleranceRad);p.SetPanTolerance(0);Assert.Equal(BitConverter.UInt32BitsToSingle(0x3DB2B8C2),p.PanToleranceRad);
        p.SetTiltTolerance(float.NaN);Assert.True(double.IsNaN(p.HeadToleranceRad));
    }
    [Fact]
    public void NativePoseFunctionSetterRejectsOnlyExactlyRunningAndCompletionIsDefinedBeforeInit()
    {
        // reach: unit, source G1 5586AC and concrete completion559CAC; supplied action state.
        using var f=new Fixture();var runner=new NativeDriveToObjectRunner(f.M,7);
        PosesFunction replacement=(ObservableObject _,out IReadOnlyList<PreActionPose> poses,ref bool inPosition)=>{poses=Array.Empty<PreActionPose>();return ActionResult.Success;};
        try
        {
            Assert.Equal(new ObjectInteractionCompletion(7,uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,1,false),runner.GetObjectInteractionCompletion());
            runner.State=EngineActionResult.Running;Assert.False(runner.SetPosesFunction(replacement));Assert.NotSame(replacement,runner.Action.PosesFunction);
            foreach(uint state in new[]{EngineActionResult.NotStarted,0u,0x02000000u,0x03000004u}){runner.State=state;Assert.True(runner.SetPosesFunction(replacement));Assert.Same(replacement,runner.Action.PosesFunction);}
        }
        finally{runner.State=EngineActionResult.NotStarted;runner.Prep();runner.WatcherEnding();}
    }
    [Fact]
    public void NativeSetGoalsCopiesFullRotationOrderAndEmptyAdmissionBeforePathMutation()
    {
        // reach: unit, SetGoals55A6D0 and native Init55A86C; supplied goals/handler inputs, real Path state.
        using var f=new Fixture();using var g0=At(f,130,20,.7f);using var g1=At(f,150,50,-.5f);
        var drive=new DriveToPoseAction(f.M,false,false);
        try
        {
            Assert.Equal(0x0300000F,drive.Init()); //unset goals are distinct from an admitted empty vector.
            Assert.True(drive.SetNativeGoals(new[]{g0,g1}));Assert.NotSame(g0,drive.Goals![0]);
            Assert.Equal(g0.Quaternion,drive.Goals[0].Quaternion);Assert.Equal(g1.Quaternion,drive.Goals[1].Quaternion);
            g0.Node!.Transform=new(g0.Quaternion,new(999,0,0));Assert.Equal(130,drive.Goals[0].Translation.X);
            drive.State=EngineActionResult.Running;Assert.False(drive.SetNativeGoals(Array.Empty<Pose3d>()));drive.State=EngineActionResult.NotStarted;
            Assert.True(drive.SetNativeGoals(Array.Empty<Pose3d>()));var path=f.Rig.Robot.Motion.Path;path.SetStatus(3);
            Assert.Equal(0x03000013,drive.Init());Assert.Equal(3,path.DriveToPoseStatus);Assert.Empty(f.Rig.SentSince(0).OfType<ClearPath>());
        }
        finally{drive.Dispose();}
    }
    [Fact]
    public void NativePathRejectsKnownNoncurrentRootAndEmptyVectorDoesNotConsumeRngOrChangeState()
    {
        // reach: unit, direct Path input (normal DriveToPose Init converts first);64AE84 requires CURRENT root.
        using var f=new Fixture();var path=f.Rig.Robot.Motion.Path;var index=new PathGoalIndex();int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        Assert.True(f.Robot.AddOriginWithId(77));using var foreign=new Pose3d(Rotation3d.Identity,new(100,0,0),parent:f.Robot.GetNativeOrigin(77));
        path.SetStatus(4);Assert.Equal(1,path.StartNativeDrivingToPose(f.M,new[]{foreign},index,false,null));Assert.Equal(0,path.DriveToPoseStatus);Assert.Equal(0,words);
        path.SetStatus(3);Assert.Equal(1,path.StartNativeDrivingToPose(f.M,Array.Empty<Pose3d>(),index,false,null));Assert.Equal(3,path.DriveToPoseStatus);Assert.Equal(0,words);
        f.Rig.Engine.ContextRandom.WordSourceForTest=null;
    }
    [Fact]
    public void PreactionClosestComparisonConvertsToMainParentAndPreservesOriginalOrdinal()
    {
        // reach: unit actual preaction Finish recipient. Q10.6 550A52/550CB6, mixed same-root parents and skip failure.
        using var f=new Fixture();var obj=f.Add();using var current=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).GetWithRespectToRoot();
        using var parent=new Pose3d(Rotation3d.FromAngleAxis(Radians.Pi/2f,new(0,0,1)),new(100,50,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        using var main=new Pose3d(Rotation3d.Identity,new(10,20,0),parent:parent);
        Assert.True(f.Robot.AddOriginWithId(88));using var unrelated=new Pose3d(Rotation3d.Identity,new(10,20,0),parent:f.Robot.GetNativeOrigin(88));
        using var nearestLocal=new Pose3d(Rotation3d.Identity,new(11,21,0),parent:parent);using var nearest=nearestLocal.GetWithRespectToRoot();
        using var misleadingLocal=At(f,10,20);var output=new PreActionPoseOutput();var marker=obj.Markers[0];
        output.Poses.Add(new(PreActionType.Docking,marker,unrelated,75));output.Poses.Add(new(PreActionType.Docking,marker,nearest,75));output.Poses.Add(new(PreActionType.Docking,marker,misleadingLocal,75));
        var result=DockPreActionPoses.Finish(output,obj.NativePose,main,BitConverter.UInt32BitsToSingle(0x3E060A92),false);
        Assert.Same(output,result);Assert.Equal(1,result.ClosestIndex);Assert.Equal(1f,result.OffsetX);Assert.Equal(1f,result.OffsetY);Assert.Equal(3,result.Poses.Count);Assert.Same(nearest,result.Poses[1].WorldPose);
    }
    [Fact]
    public void ChooserUsesOriginalMainRelativeThreeDimensionalNearestAndOneSharedDraw()
    {
        // reach: unit chooser recipient, original native pose owners;53B798/53B9C0. u=.5 => s90, complementary90.
        using var f=new Fixture();var path=f.Rig.Robot.Motion.Path;int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>++words%2==1?0u:0x80000000u;
        using var far=At(f,250);using var near=At(f,150);
        var profile=path.ChooseNativeProfile(f.Robot,new[]{far,near});
        Assert.Equal(2,words);Assert.Equal(180f,profile.SpeedMmps);Assert.Equal(90f,profile.AccelMmps2);Assert.Equal(90f,profile.DecelMmps2);Assert.Equal(135f,profile.ReverseSpeedMmps);Assert.Equal(60f,profile.DockSpeedMmps);
        Assert.Equal(PathMotionProfile.Default,path.ChooseNativeProfile(f.Robot,Array.Empty<Pose3d>()));Assert.Equal(2,words);
        f.Rig.Engine.AnimationMessages.Deliver(0xE1,false,new SetEnableSpeedChooserMessage(false));Assert.False(path.NativeSpeedChooserEnabled);
        Assert.Equal(PathMotionProfile.Default,path.ChooseNativeProfile(f.Robot,new[]{near}));Assert.Equal(2,words);
        Assert.Equal(new byte[]{0xE1,0,1},new SetEnableSpeedChooserMessage(true).ToUnionBytes());
        Assert.False(SetEnableSpeedChooserMessage.UnpackBody(new byte[]{0}).Enable);
        f.Rig.Engine.AnimationMessages.Deliver(0xE1,false,new SetEnableSpeedChooserMessage(true));Assert.True(path.NativeSpeedChooserEnabled);f.Rig.Engine.ContextRandom.WordSourceForTest=null;
    }
    [Fact]
    public void E1TypedBodyRoutesToOneRobotPathChooserAcrossHostViewsAndRetiresWithOwner()
    {
        // reach: unit UI-hub input, replacing app transport only; J5/J6 native typed bool and Path lifetime.
        using var f=new Fixture();var hub=f.Rig.Engine.AnimationMessages;var path=f.Rig.Robot.Motion.Path;
        Assert.Equal(1,hub.Count(0xE1,false));var bytes=new SetEnableSpeedChooserMessage(false).ToUnionBytes();
        hub.Deliver(0xE1,false,SetEnableSpeedChooserMessage.UnpackBody(bytes.AsMemory(2)));Assert.False(path.NativeSpeedChooserEnabled);
        using(var view=new ManipulationSystem(f.Rig.Robot,f.M.Vision))
        using(var standalone=ManipulationSystem.CreateStandalone(f.Rig.Robot,f.M.Vision,()=>7))
        {
            Assert.False(path.NativeSpeedChooserEnabled);Assert.Equal(1,hub.Count(0xE1,false));
            using var goal=At(f,150);Assert.Equal(PathMotionProfile.Default,path.ChooseNativeProfile(f.Robot,new[]{goal}));
        }
        Assert.False(path.NativeSpeedChooserEnabled);Assert.Equal(1,hub.Count(0xE1,false));
        path.DestroyRawOwner(f.Robot);Assert.Equal(0,hub.Count(0xE1,false));
    }
    [Fact]
    public void NativePathCustomProfileAndFailedStartDoNotDrawWhileNormalSuccessDrawsAfterStatusOne()
    {
        // reach: unit actual native Path start/short planner, explicit custom profile and invalid root input; J2.
        using var f=new Fixture();var path=f.Rig.Robot.Motion.Path;int words=0;
        f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{Assert.Equal(1,path.DriveToPoseStatus);return ++words%2==1?0u:0x80000000u;};
        using var goal=At(f,30);var custom=PathMotionProfile.Default with{SpeedMmps=123,AccelMmps2=321};
        path.Field48=true;path.NativeMotionProfile=custom;
        Assert.Equal(0,path.StartNativeDrivingToPose(f.M,new[]{goal},new PathGoalIndex(),false,null));Assert.Equal(0,words);Assert.Same(custom,f.M.GetNativePlanners().Profile);
        path.Abort(f.Robot.SendChecked);path.Field48=false;
        Assert.Equal(0,path.StartNativeDrivingToPose(f.M,new[]{goal},new PathGoalIndex(),false,null));Assert.Equal(2,words);Assert.Equal(164f,path.NativeMotionProfile.SpeedMmps);
        f.Rig.Engine.ContextRandom.WordSourceForTest=null;
    }
    [Fact]
    public void DriveInitFailureStillPlaysDrivingToAndRetirementStopsItsActualCubeLightTrigger()
    {
        // reach: unit custom pose-function failure, actual connected-cube message/World/light-map/asset recipients.
        // 559650 preserves InitHelper result then light0B;558950 stops0B before child prep/destruction.
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);var connected=Assert.Single(f.M.World.ConnectedObjects);
        uint id=connected.ObjectId;f.Add(id,300);var lights=f.Rig.Robot.Lights.Cubes;
        Assert.True(lights.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Flash",CubeLightComponent.EngineLayer));
        var previous=lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,CubeLightComponent.EngineLayer);Assert.NotNull(previous);
        var runner=new NativeDriveToObjectRunner(f.M,id);
        runner.SetPosesFunction((ObservableObject _,out IReadOnlyList<PreActionPose> poses,ref bool inPosition)=>{poses=Array.Empty<PreActionPose>();return ActionResult.BadPose;});
        try
        {
            Assert.Equal(0x03000005,runner.Init());
            var driving=lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,CubeLightComponent.EngineLayer);Assert.NotNull(driving);Assert.Equal(1,driving!.Value.Count);Assert.Equal("interacting",driving.Value.Name); // shipped DrivingTo->interacting duration0 clears the prior layer.
            runner.State=0x03000005;runner.Prep();runner.WatcherEnding();f.Rig.Tick();
            Assert.Null(lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,CubeLightComponent.EngineLayer));
            Assert.NotNull(lights.PlayingForTest(ObjectType.Block_LIGHTCUBE1,CubeLightComponent.StateLayer));
            Assert.NotEmpty(f.Rig.SentSince(0).OfType<CubeLights>());
        }
        finally{if(!runner.DestructionObserved){runner.Prep();runner.WatcherEnding();}}
    }
    [Fact]
    public void NativeHeightUsesRawSyncedLiftEvenWhenOriginIsRejected()
    {
        // reach: production raw RobotState dispatch; J3/51296A before origin gate,516F0C ordered f32 height.
        using var f=new Fixture();var state=f.Rig.MakeState(origin:999,liftAngle:.7f);f.Rig.Data(state);f.Rig.Tick();
        Assert.Null(f.Robot.AcceptedState);float h=66f*MathF.Sin(.7f);h=h+45f;h=h+0f;h=h+5f;
        Assert.Equal(h,f.M.NativeHeight());
    }
    [Fact]
    public void ObservedObjectKeepsExposureHistoryCameraParentUntilRootCreationBoundary()
    {
        // reach: unit solved-PnP input and explicit camera-local transform replace unported PnP/readonly camera geometry.
        // Actual native historical pose owner + new live consumer helper;85D902/90C,625540/44 ->4DF628.
        // Source frame equation: Rz180 * ((3,4,5)+(7,8,9)) + (100,200,30) = (90,188,44), exact representable integers.
        using var f=new Fixture();uint stamp=f.Robot.StoredState!.Timestamp;
        using var exposurePose=new Pose3d(new Rotation3d(0,0,0,1),new(100,200,30),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        using var history=HistRobotState.From(exposurePose,f.Robot.StoredState);
        using var cameraPose=new Pose3d(Rotation3d.Identity,new(3,4,5),parent:history.Pose);
        using var solved=new Pose3d(Rotation3d.Identity,new(7,8,9));var camera=new CameraModel(f.M.Vision.Calibration!,cameraPose);
        using var child=BlockWorld.CreateObservedCameraChild(camera,solved);
        Assert.Same(cameraPose.Node,child.Node!.Parent);Assert.Equal(stamp,history.State.Timestamp);
        f.X=900;f.Y=800;f.Motors();Assert.Equal(stamp,history.State.Timestamp);Assert.NotEqual(stamp,f.Robot.StoredState!.Timestamp);
        using var world=child.GetWithRespectToRoot();Assert.Equal(new Vec3(90,188,44),world.Translation);
        Assert.Equal(f.Robot.CurrentOriginId,world.FindRoot().Id);
    }
    [Fact]
    public void ObservedClusterConsumerKeepsLocalBinary32SortKeyWhileProducingRootPose()
    {
        // reach: unit actual cluster consumer; solved camera-local poses replace unported detector/PnP.
        // J12: local keys10100<10400, whereas root positions1010/980 reverse the ordering; no implementation-derived oracle.
        using var f=new Fixture();using var historical=At(f,1000);
        using var cameraPose=new Pose3d(Rotation3d.Identity,Vec3.Zero,parent:historical);
        var camera=new CameraModel(f.M.Vision.Calibration!,cameraPose);
        using var localA=new Pose3d(Rotation3d.Identity,new(10,0,100));using var localB=new Pose3d(Rotation3d.Identity,new(-20,0,100));
        using var a=BlockWorld.CreateObservedCameraChild(camera,localA);using var b=BlockWorld.CreateObservedCameraChild(camera,localB);
        var preparedA=BlockWorld.PrepareObservedCluster(a);var preparedB=BlockWorld.PrepareObservedCluster(b);
        using var rootA=preparedA.RootPose;using var rootB=preparedB.RootPose;
        Assert.Equal(10100f,preparedA.Key);Assert.Equal(10400f,preparedB.Key);
        Assert.Equal(new Vec3(1010,0,100),rootA.Translation);Assert.Equal(new Vec3(980,0,100),rootB.Translation);
        Assert.Equal(new[]{"A","B"},new[]{("B",preparedB.Key),("A",preparedA.Key)}.OrderBy(p=>p.Key).Select(p=>p.Item1));
        using var large=new Pose3d(Rotation3d.Identity,new(4097,4096,4096),parent:cameraPose);
        var precise=BlockWorld.PrepareObservedCluster(large);using var largeRoot=precise.RootPose;
        Assert.Equal(0x4C400800u,BitConverter.SingleToUInt32Bits(precise.Key)); // (f32(4097²)+4096²)+4096² =50339840, not double50339841.
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableInitialPreactionStopsOnlyCapturedHelperAndDoesNotStopReusedOwner(bool reuse)
    {
        // reach: unit manually constructed unsupported Charger_Basic/empty-marker scene; actual native helper/manager/precheck entry.
        // Host availability handling must emit no invented native report or failure and must not stop a replacement run.
        using var f=new Fixture();f.Add(x:300);
        var obstacle=new ObservableObject(9,ObjectType.Charger_Basic,Array.Empty<KnownMarker>())
        {Pose=new Pose3d(Rotation3d.Identity,new(100,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known};
        f.M.World.AddLocatedObject(obstacle);
        var parent=new Parent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);
        var helper=new NativePickupBlockHelper(f.M,parent,f.Runtime.AI.Helpers.Factory,7,0,false,true);IHelper? raw=helper;
        var handle=f.Runtime.AI.Helpers.AddHelperToComponent(ref raw);HelperRef? replacementHandle=null;bool callback=false;
        parent.Entry=p=>p.Delegate(handle,_=>callback=true,_=>callback=true);
        var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);var missing=new List<string>();SteppedBehavior.MissingReported+=missing.Add;
        var local=new List<RobotCompletedAction>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)local.Add(r);});
        try
        {
            SteppedBehavior.ResetMissingForTests(); // each serialized theory case must observe its own diagnostic, despite process-wide deduplication.
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Assert.Contains(missing,s=>s.Contains("M12-036")&&s.Contains("footprint"));Assert.Equal(0u,helper.Attempts);Assert.False(callback);
            if(reuse)
            {
                manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
                var removeObstacle=new BlockWorldFilter();removeObstacle.AllowedIds.Add(9);
                Assert.Single(f.M.World.DeleteLocatedObjects(removeObstacle)); // supplied next-run scene removes only test obstacle9.
                // The real behavior creates a new helper on each Init; retain the old handle only to exercise queued cleanup.
                var next=new NativePickupBlockHelper(f.M,parent,f.Runtime.AI.Helpers.Factory,7,0,false,true);IHelper? nextRaw=next;
                replacementHandle=f.Runtime.AI.Helpers.AddHelperToComponent(ref nextRaw);
                parent.Entry=p=>Assert.True(p.Delegate(replacementHandle,_=>callback=true,_=>callback=true));
                Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
                var replacement=Assert.IsType<NativeDriveToObjectRunner>(next.DriveHelper!.Runner);f.Rig.Tick();
                Assert.Same(parent,manager.Current);Assert.False(replacement.DestructionObserved);Assert.True(parent.HasCurrentAction);
            }
            else
            {f.Rig.Tick();Assert.NotSame(parent,manager.Current);Assert.Equal(0,f.Runtime.AI.Helpers.StackCount);Assert.Empty(local);}
            Assert.False(callback);Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);Assert.DoesNotContain(f.Rig.SentSince(0),m=>m is DockWithObject);
        }
        finally{SteppedBehavior.MissingReported-=missing.Add;manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();replacementHandle?.Release();handle.Release();}
    }
    [Fact]
    public void ForceHelperCapturesMainOnceAndExcludesOnlyThatOriginalPredockPose()
    {
        // reach: native factory/helper/actual pose callback, supplied scene/initial predock and no fabricated result.
        using var f=new Fixture();var cube=f.Add(x:300);var poses=new DriveToObjectAction(f.M,7,PreActionType.Docking){NativeInputs=true};bool inPosition=false;
        Assert.Equal(ActionResult.Success,poses.GetPossiblePoses(cube,out var all,ref inPosition));Assert.True(all.Count>1);
        using var initial=all[0].WorldPose.GetWithRespectToRoot();f.X=(float)initial.Translation.X;f.Y=(float)initial.Translation.Y;f.Yaw=(float)initial.AngleAroundZ;f.Motors();
        var parent=new Parent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);NativeDriveToHelper? helper=null;HelperRef? held=null;
        parent.Entry=p=>{var h=f.Runtime.AI.Helpers.Factory.CreateNativeDriveTo(f.M,p,7,true);held=h.Copy();helper=(NativeDriveToHelper)h.Pointer;try{p.Delegate(h);}finally{h.Release();}};
        var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);
        try
        {
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            for(int i=0;i<10&&helper?.Runner?.Drive is null;i++)f.Rig.Tick();
            var runner=Assert.IsType<NativeDriveToObjectRunner>(helper!.Runner);Assert.Equal(1u,helper.Attempts);
            Assert.Equal(all.Count-1,runner.Drive!.Goals!.Count);
            Assert.DoesNotContain(runner.Drive.Goals,p=>p.IsSameAs(initial,new Vec3(1,1,1),.001,out _));
            // Later MAIN motion cannot change the force functor's original114 snapshot.
            f.X+=50;f.Motors();bool after=false;Assert.Equal(ActionResult.Success,runner.Action.PosesFunction(cube,out var filtered,ref after));Assert.Equal(all.Count-1,filtered.Count);
        }
        finally{manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();held?.Release();}
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeDriveNoPreactionFailureMarksWhiteboardOnlyWithoutForce(bool force)
    {
        // reach: unit response input, D03/5B5C94. Other helper lifecycle covered by configured test.
        using var f=new Fixture();var parent=new Parent();using var helper=new NativeDriveToHelper(f.M,parent,f.Runtime.AI.Helpers.Factory,7,force);
        helper.InitializeOnStack();helper.Respond(new RobotCompletedAction(1,10,0x03000013,Array.Empty<uint>(),0));Assert.Equal(BehaviorStatus.Running,helper.Status);
        helper.Respond(new RobotCompletedAction(1,10,0x03000010,Array.Empty<uint>(),0));Assert.Equal(BehaviorStatus.Failure,helper.Status);
        Assert.Equal(force?null:7u,f.M.Whiteboard.NoPreDockPosesObjectId);
    }
    [Fact]
    public void DriveHelperCategoryFourResponseRetriesOnlyThreeConstructedAttempts()
    {
        // reach: unit source response reports substitute firmware/action results; actual helper factory/owner/tag cancellation.
        // 5B5838 checks>=3 before increment/construction, category04 response reenters the same method.
        using var f=new Fixture();f.Add(x:300);var parent=new Parent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);
        NativeDriveToHelper? helper=null;HelperRef? held=null;
        parent.Entry=p=>{var h=f.Runtime.AI.Helpers.Factory.CreateNativeDriveTo(f.M,p,7,false);held=h.Copy();helper=(NativeDriveToHelper)h.Pointer;try{p.Delegate(h);}finally{h.Release();}};
        var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);
        try
        {
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());Assert.Equal(1u,helper!.Attempts);
            helper.Respond(new RobotCompletedAction(1,10,0x04000002,Array.Empty<uint>(),0));Assert.Equal(2u,helper.Attempts);
            helper.Respond(new RobotCompletedAction(1,10,0x04000002,Array.Empty<uint>(),0));Assert.Equal(3u,helper.Attempts);
            var third=helper.Runner;helper.Respond(new RobotCompletedAction(1,10,0x04000002,Array.Empty<uint>(),0));Assert.Equal(3u,helper.Attempts);Assert.Same(third,helper.Runner);Assert.Equal(BehaviorStatus.Failure,helper.Status);
        }
        finally{manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();held?.Release();}
    }
    [Fact]
    public void StoppingNativeApproachRetiresOwnedPlanAndChildrenWhileUnrelatedParallelSurvives()
    {
        // reach: real factory/helper/parent84 cancellation/ActionList and source cleanup, offline scene/firmware.
        using var f=new Fixture();f.Add(x:300);var parent=new Parent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);
        NativeDriveToHelper? helper=null;HelperRef? held=null;bool callback=false;
        parent.Entry=p=>{var h=f.Runtime.AI.Helpers.Factory.CreateNativeDriveTo(f.M,p,7,false);held=h.Copy();helper=(NativeDriveToHelper)h.Pointer;try{p.Delegate(h,_=>callback=true,_=>callback=true);}finally{h.Release();}};
        var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);var unrelated=new NativeTimedWaitRunner(f.M,1000);
        try
        {
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            for(int i=0;i<10&&helper?.Runner?.Drive is null;i++)f.Rig.Tick();
            var runner=Assert.IsType<NativeDriveToObjectRunner>(helper!.Runner);Assert.NotNull(runner.Drive);
            f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,unrelated);f.Rig.Tick();int mark=f.Rig.Mark();
            manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
            Assert.True(runner.DestructionObserved);Assert.True(runner.Drive!.DestructionObserved);Assert.False(unrelated.DestructionObserved);Assert.False(callback);
            Assert.False(f.Rig.Robot.Motion.Path.IsActive);Assert.Contains(f.Rig.SentSince(mark),m=>m is ClearPath);
            for(int i=0;i<5;i++)f.Motors();Assert.False(unrelated.DestructionObserved);Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is DockWithObject);
        }
        finally{manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();held?.Release();}
    }
    [Fact]
    public void NativePathOwnedAllGoalProducerRetainsOriginalIdAfterRejectionAndRevalidation()
    {
        // reach: unit, actual Path producer->planner->worker->chosen-index consumer; substitutes unavailable map
        // and hard C-space cells to force invalidation (same CM13-03 oracle). Inherited path geometry is candidate.
        using var f=new Fixture();f.M.World.Maps.CurrentOrigin=uint.MaxValue;
        var planners=f.M.GetNativePlanners();var runtime=Assert.IsType<NativeLatticePlannerRuntime>(planners.Lattice);var backend=runtime.Backend;
        void Hard(float x,float half)
        {
            var polygon=new FastPolygonF(new P2f[]{new(x-half,-half),new(x+half,-half),new(x+half,half),new(x-half,half)});
            for(int i=0;i<backend.Env.Primitives.NumAngles;i++)((List<(FastPolygonF Poly,float Penalty)>)backend.Env.Bucket(i)).Add((polygon,1000));
        }
        Hard(500,1);using var rejected=At(f,520);using var target=At(f,121.25f,0,.2f);
        var index=new PathGoalIndex();var path=f.Rig.Robot.Motion.Path;
        lock(runtime.ContextMutex)
        {
            Assert.Equal(0,path.StartNativeDrivingToPose(f.M,new[]{rejected,target},index,false,null));
            Assert.Equal(2,planners.Goals.Count);Assert.Equal(500f,(float)planners.Goals[0].Translation.X);
            float offsetX=121.25f+MathF.Cos(.2f)*-20f;
            Assert.Equal(offsetX,(float)planners.Goals[1].Translation.X);
            var raw=(List<(byte Id,StateC C)>)typeof(NativeLatticePlannerRuntime).GetField("_goalContext",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(runtime)!;
            Assert.Equal((byte)1,Assert.Single(raw).Id);
            target.Node!.Transform=new(target.Quaternion,new(900,0,0));Assert.NotEqual(900,planners.Goals[1].Translation.X);
            backend.Env.ClearObstacles();Hard(100,.25f); // raw goal remains free; its rounded100 cell now collides.
        }
        Assert.True(SpinWait.SpinUntil(()=>runtime.CheckPlanningStatus!=1,TimeSpan.FromSeconds(10)));
        Assert.Equal(2,runtime.CheckPlanningStatus);f.Rig.Tick();Assert.Equal((byte)1,index.Value);
        Assert.NotEmpty(f.Rig.SentSince(0).OfType<ExecutePath>());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StageOneRunsAllEightChildrenAndKnownTargetExhaustionClearsDefaultInMemoryWorld(bool target44DuringDeletion)
    {
        // reach: native helper factory/manager/parent84/ActionList with explicit offline firmware and no observations.
        // SH7/A21 5BB1AA..500: any-result reentry and actual default-filter deletion, not a count stub.
        using var f=new Fixture();f.Add();f.Add(8,600);var parent=new Parent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);
        NativeSearchForBlockHelper? search=null;HelperRef? held=null;ActionRunner[]? stageOne=null;bool failed=false,succeeded=false;int words=0;
        f.Rig.Engine.ContextRandom.WordSourceForTest=()=>++words%2==1?0u:0x80000000u;
        parent.Entry=p=>
        {
            var handle=f.Runtime.AI.Helpers.Factory.CreateNativeSearchForBlock(f.M,p,7,1);held=handle.Copy();search=(NativeSearchForBlockHelper)handle.Pointer;
            search.BeforeActionPublicationForTest=()=>{if(search.Stage==1)stageOne=search.Sequence!.Children.ToArray();};
            try{p.Delegate(handle,_=>succeeded=true,_=>failed=true);}finally{handle.Release();}
        };
        var deleted=new List<uint>();f.M.World.LocatedObjectDeleted+=deleted.Add;
        // reach: unit recipient interleaving only in the true case: supplies a queued source44 during real deletion.
        if(target44DuringDeletion)f.M.World.LocatedObjectDeleted+=_=>f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(1,2,1,7));
        var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);int mark=0;ExecutePath? pending=null;
        try
        {
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            for(int i=0;i<800&&!failed&&!succeeded;i++)
            {
                f.Motors();
                if(pending is {} done){f.Rig.Data(new PathFollowingEvent{EventId=done.EventId,EventType=(byte)PathEventType.Completed});pending=null;f.Motors();}
                foreach(var message in f.Rig.SentSince(mark))
                {
                    if(message is AppendPathSegmentLine line){f.X=line.XEndMm;f.Y=line.YEndMm;}
                    if(message is ExecutePath execute){f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=(byte)PathEventType.Started});pending=execute;f.Motors();}
                }
                mark=f.Rig.Mark();
            }
            Assert.True(failed);Assert.False(succeeded);Assert.NotNull(stageOne);Assert.Equal(new[]{8,0x28,0x28,0x21,8,0x28,0x28,0x21},stageOne!.Select(c=>c.Type));
            Assert.All(stageOne,c=>Assert.True(c.DestructionObserved));Assert.Equal(BehaviorStatus.Failure,search!.Status);Assert.Equal(2,search.Stage);
            Assert.Contains(7u,deleted);Assert.Contains(8u,deleted);Assert.Null(f.M.World.GetLocatedObjectByIdHelper(7));Assert.Null(f.M.World.GetLocatedObjectByIdHelper(8));
            Assert.True(words>=36);Assert.Equal(5,f.Rig.SentSince(0).OfType<AppendPathSegmentLine>().Count()); // one stage0 + four stage1 drives.
        }
        finally{manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();held?.Release();f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }
    [Fact]
    public void ShippedConfiguredPickupApproachesFromOutsidePredockThenTurnsDocksAndDeliversOriginalReports()
    {
        // reach: production configured factory/manager/helper/ActionList and actual planner/path/status recipients.
        // Supplied offline scene/calibration/firmware/raw pose/C6 + animation acks. Inherited lattice geometry remains candidate.
        using var f=new Fixture();var cube=f.Add(x:300);f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        f.Robot.Progression!.Unlocked.Add(6);var pickup=Assert.IsType<NativePickUpCubeBehavior>(f.Runtime.Bound["SparksPickupSingleCubeForPyramid"]);
        Assert.True(pickup.IsRunnable(f.Runtime.Context));Assert.True(pickup.CheckExecutionAvailability());
        var local=new List<RobotCompletedAction>();var app=new List<ActionCompletionReport>();var logs=new List<string>();
        f.Rig.Engine.ActionCompleted+=app.Add;f.Rig.Engine.LogEntry+=e=>logs.Add(e.Name+":"+e.Text);
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction value)local.Add(value);});
        var manager=f.Robot.BehaviorManagerOwner!;manager.DisableReactionsWithLock("pickup-approach-offline",Enumerable.Range(0,21).Select(i=>i==(int)ReactionTrigger.ObjectPositionUpdated).ToArray(),false);
        int mark=0;ExecutePath? pending=null;NativeDriveToObjectRunner? originalDrive=null;DriveToPoseAction? poseDrive=null;
        bool dock=false,allGoals=false;uint frame=0;var anims=new HashSet<byte>();var startedPaths=new HashSet<ushort>();var firmwareTrace=new List<string>();
        try
        {
            Assert.True(manager.StartAsync(pickup.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            for(int i=0;i<350&&ReferenceEquals(manager.Current,pickup);i++)
            {
                var helper=pickup.Helper;
                if(helper?.DriveHelper?.Runner is {} drive)
                {
                    originalDrive??=drive;poseDrive=drive.Drive??poseDrive;
                    if(drive.Drive?.Goals is {Count:>1} goals)
                    {allGoals=true;Assert.Equal(goals.Count,f.M.GetNativePlanners().Goals.Count);}
                }
                if(f.M.GetNativePlanners().Lattice is {} runtime&&runtime.CheckPlanningStatus==1)
                    Assert.True(SpinWait.SpinUntil(()=>runtime.CheckPlanningStatus!=1,TimeSpan.FromSeconds(10)),"offline planner watchdog");
                f.Motors();
                if(pending is {} completed)
                {
                    Assert.True(poseDrive?.Goals is {Count:>0},"pending="+completed.EventId+" reports="+string.Join(";",local.Select(r=>$"{r.Tag}/{r.ActionType:X}/{r.Result:X}"))+" FIRMWARE="+string.Join(";",firmwareTrace)+" TRACE="+string.Join(";",originalDrive?.Action.Trace??Array.Empty<string>())+"\n"+string.Join("\n",logs.TakeLast(40)));var selected=poseDrive!.Goals![poseDrive.GoalIndex];
                    using var goal=selected.GetWithRespectToRoot();f.X=(float)goal.Translation.X;f.Y=(float)goal.Translation.Y;f.Yaw=(float)goal.AngleAroundZ;f.Motors();
                    firmwareTrace.Add($"path{completed.EventId} drive{poseDrive.Tag} original{poseDrive.GoalIndex} MAIN({f.X},{f.Y},{f.Yaw}) target{cube.NativePose.Translation}");
                    f.Rig.Data(new PathFollowingEvent{EventId=completed.EventId,EventType=(byte)PathEventType.Completed});pending=null;f.Motors();
                }
                foreach(var message in f.Rig.SentSince(mark))if(message is ExecutePath execute&&startedPaths.Add(execute.EventId))
                {f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=(byte)PathEventType.Started});pending=execute;}
                mark=f.Rig.Mark();
                byte tag=f.Rig.Robot.Animations.CurrentTag;
                if(tag!=0&&anims.Add(tag)){f.Rig.Data(new AnimationStarted{AnimId=tag});f.Rig.Data(new AnimationEnded{AnimId=tag});f.Rig.Tick();}
                if(!dock&&f.Rig.SentSince(0).OfType<DockWithObject>().Any())
                {f.Motors(RobotStatusFlag.IsPickingOrPlacing);f.Rig.Data(new LiftLoad{Field0=true});f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=2});f.Motors();dock=true;}
                if(!dock)
                {
                    var current=f.Robot.ActionList.GetCurrentAction();
                    bool visible=current is NativeDriveToObjectRunner native&&native.Sequence.Children.OfType<NativeTurnTowardsObjectRunner>().Any(c=>c.Initialized);
                    if(current is CompoundActionSequential seq)
                        visible=seq.Children.OfType<NativePickupObjectRunner>().Any(p=>p.Prepared?.Children.OfType<NativeTurnTowardsObjectRunner>().Any(c=>c.Initialized)==true);
                    if(visible)firmwareTrace.Add("BEFOREIMAGE MAIN="+f.Robot.GetCheckedMainPose().Translation+" "+f.Robot.GetCheckedMainPose().GetNamedPathToRoot(false)+" obj="+cube.NativePose.Translation+" "+cube.NativePose.GetNamedPathToRoot(false));
                    f.Image(++frame,visible,cubeX:300);
                    if(visible)firmwareTrace.Add("AFTERIMAGE MAIN="+f.Robot.GetCheckedMainPose().Translation+" "+f.Robot.GetCheckedMainPose().GetNamedPathToRoot(false)+" obj="+cube.NativePose.Translation+" "+cube.NativePose.GetNamedPathToRoot(false));
                }
            }
            f.Rig.Engine.DrainAppDelivery();
            Assert.True(allGoals,string.Join("\n",logs.TakeLast(30)));Assert.True(dock,string.Join("\n",logs.TakeLast(30)));
            Assert.NotNull(originalDrive);Assert.Equal(0u,originalDrive!.State);Assert.True(originalDrive.DestructionObserved);
            Assert.Equal(BehaviorStatus.Complete,pickup.Helper!.Status);Assert.Equal(7u,f.Robot.CarryingOwner!.CarriedObjectId);
            var report=Assert.Single(local.Where(r=>r.Tag==originalDrive.Tag));Assert.Equal((byte)0,report.CompletionVariant);
            Assert.Equal(new ObjectInteractionCompletion(7,uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,1,false),report.ObjectInteraction);
            Assert.Equal(36+4*report.SubActionResults.Count,report.PackBody().Length);Assert.Contains(app,r=>r.Tag==report.Tag&&r.ObjectInteraction==report.ObjectInteraction);
            Assert.Contains(f.Rig.SentSince(0),m=>m is AppendPathSegmentLine or AppendPathSegmentArc);
            Assert.Contains(f.Rig.SentSince(0).OfType<SetHeadAngle>(),h=>BitConverter.SingleToUInt32Bits(h.AngleRad)==0xBE860A92&&h.MaxSpeedRadPerSec==2f&&h.AccelRadPerSec2==5f&&h.DurationSec==0f);
            Assert.Contains(local,r=>r.ActionType is 0x16 or 0x17&&r.Result==0&&r.ObjectInteraction is {IsPayloadDefined:true});
        }
        finally{manager.RemoveDisableReactionsLock("pickup-approach-offline");manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Robot.ActionList.Clear();}
    }
}
