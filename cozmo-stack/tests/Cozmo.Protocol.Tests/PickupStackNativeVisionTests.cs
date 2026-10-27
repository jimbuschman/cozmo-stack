using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PickupStackNativeVisionTests
{
    private sealed class Fixture:IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new();
        internal EngineRobot Robot=>Rig.Engine.Robot!;
        internal ManipulationSystem M=>Robot.NativeBehaviorRuntime!.Manipulation!;
        internal Fixture(bool nvCalibration=false)
        {
            Rig.ToSynced();Rig.Calibrate();Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos);
            if(nvCalibration)
            {
                for(int i=0;i<30&&Rig.Robot.CameraSettings.Calibration is null;i++)
                {
                    Rig.Tick();var tag=Rig.Engine.NvStorage!.InFlightTag;if(tag is null)continue;
                    var writer=new CladWriter();
                    if(tag==CameraCalibration.NvEntryTag)
                    {foreach(float v in new[]{290f,291f,160f,120f,0f})writer.F32(v);writer.U16(240);writer.U16(320);for(int k=0;k<8;k++)writer.F32(0);}
                    Rig.Data(new NVOpResult{Tag=tag.Value,Op=0,Result=tag==CameraCalibration.NvEntryTag?(sbyte)0:(sbyte)1,Length=0,Data=writer.ToArray()});Rig.Tick();
                }
                Assert.NotNull(Rig.Robot.CameraSettings.Calibration);Assert.Same(Rig.Robot.CameraSettings.Calibration,M.Vision.Calibration);
            }
            else M.Vision.UpdateCameraCalibration(CameraCalibration.Nominal());
        }
        internal ObservableObject Add(uint id,Vec3 position)
        {
            var obj=new ObservableObject(id,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {Pose=new Pose3d(Rotation3d.Identity,position,parent:Robot.GetNativeOrigin(Robot.CurrentOriginId)),OriginId=Robot.CurrentOriginId,PoseState=PoseState.Known,LastObservedTimestamp=Robot.StoredState!.Timestamp};
            M.World.AddLocatedObject(obj);return obj;
        }
        internal void PumpMotors()
        {
            var sent=Rig.SentSince(0);float head=Rig.Robot.Motion.HeadAngleRad,lift=0,angle=0;
            foreach(var message in sent)
            {
                switch(message)
                {
                    case SetHeadAngle h:head=h.AngleRad;Rig.Data(new MotorActionAck{ActionId=h.ActionId});break;
                    case SetLiftHeight l:lift=(float)Math.Asin((l.HeightMm-45f)/66f);Rig.Data(new MotorActionAck{ActionId=l.ActionId});break;
                    case SetBodyAngle b:angle=b.AngleRad;Rig.Data(new MotorActionAck{ActionId=b.ActionId});break;
                }
            }
            var state=Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,head:head,liftAngle:lift);
            state.Pose.Angle=angle;Rig.Data(state);Rig.Tick();
        }
        public void Dispose()=>Rig.Dispose();
    }
    [Fact]
    public void QueuedTurnUsesLiveMainCalibratedHeadAndActualVisionCalibrationWithoutHistory()
    {
        using var f=new Fixture();using var goal=new Pose3d(Rotation3d.Identity,new Vec3(100,100,60),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        Assert.Null(f.Rig.Robot.CameraSettings.Calibration);Assert.NotNull(f.M.Vision.Calibration);
        f.M.Vision.History.ResetToConstructed();
        var runner=new NativeTurnTowardsPoseRunner(f.M,goal,Radians.Pi);Assert.True(runner.SetTag(987654));
        int mark=f.Rig.Mark();f.Robot.ActionList.QueueAction(QueueActionPosition.Now,runner,0);f.Rig.Tick();
        var body=Assert.Single(f.Rig.SentSince(mark).OfType<SetBodyAngle>());
        Assert.InRange(body.AngleRad,MathF.PI/4-.00001f,MathF.PI/4+.00001f);
        Assert.Contains(f.Rig.SentSince(mark),m=>m is SetHeadAngle);Assert.False(runner.TurnSkipped);
        for(int i=0;i<10&&f.Robot.ActionList.GetCurrentAction() is not null;i++)f.PumpMotors();
        Assert.Equal(EngineActionResult.Success,runner.State);Assert.True(runner.DestructionObserved);
        Assert.False(ActionRunnerTagCounter.Global.IsInUse(987654));
    }
    [Fact]
    public void ReinitializationKeepsTheActualLiveMainParentAfterRobotMovement()
    {
        using var f=new Fixture();using var goal=new Pose3d(Rotation3d.Identity,new Vec3(100,100,60),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        var runner=new NativeTurnTowardsPoseRunner(f.M,goal,Radians.Pi);
        try
        {
            Assert.Equal(0,runner.Init());Assert.Same(f.Robot.GetCheckedMainPose().Node,runner.GoalPose.Node!.Parent);
            f.Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,x:50);
            Assert.Equal(0,runner.Init());Assert.Same(f.Robot.GetCheckedMainPose().Node,runner.GoalPose.Node!.Parent);
            Assert.Equal(100,runner.GoalPose.Translation.X);Assert.Equal(100,runner.GoalPose.Translation.Y);
            Assert.InRange(runner.Parameters.PanAngleRad,MathF.PI/4-.00001f,MathF.PI/4+.00001f);
        }
        finally{runner.Prep();runner.WatcherEnding();}
    }
    [Fact]
    public void NativeFactoryPositioningTransitionsBeforeWaitAndActualProcessedImagesCompleteIt()
    {
        // Reach: actual native-owner factory, queued runner, motor wire/acks/status, real worker/result drain.
        // Images/calibration and located target are admitted fixture inputs, not robot discovery/hardware proof.
        using var f=new Fixture(true);f.Add(7,new Vec3(120,0,22));
        var runner=f.Robot.NativeBehaviorRuntime!.CreatePickupVisualVerification(7);
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,runner,0);
        for(int i=0;i<12&&(runner.Positioning||runner.State==EngineActionResult.NotStarted);i++)
        {f.PumpMotors();if(i>12)throw new InvalidOperationException("positioning did not finish");}
        Assert.False(runner.Positioning);Assert.NotNull(runner.Wait);Assert.False(runner.Wait.Subscribed);Assert.Equal(EngineActionResult.Running,runner.State);
        f.Rig.Tick();Assert.True(runner.Wait.Subscribed);
        f.M.Vision.Enabled=true;f.M.Vision.ModeEnableMask=1<<1;
        for(int i=0;i<30&&runner.State==EngineActionResult.Running;i++)
        {
            f.Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos);
            uint stamp=f.Robot.StoredState!.Timestamp;
            f.M.Vision.HandOverFrame(new CameraFrame{ImageId=(uint)i,Timestamp=stamp,Width=320,Height=240,Encoding=1,RawPayload=new byte[320*240]});
            f.M.Vision.WaitForProcessorIdle();f.Rig.Engine.VisionUpdateAllResultsFailed!();f.Rig.Tick();
        }
        Assert.Equal(EngineActionResult.Success,runner.State);Assert.True(runner.DestructionObserved);
        Assert.Contains(f.Rig.SentSince(0),m=>m is SetLiftHeight);Assert.False(f.Robot.NativeBehaviorRuntime.Bound.Values.OfType<Cozmo.Robot.Behavior.NativePickUpCubeBehavior>().Any(p=>p.HasCurrentAction));
    }
    [Fact]
    public void WaitHasStrictTimestampModeCountAndRetainsSubscriptionUntilDestructionOnCancel()
    {
        // Explicit local-message unit for boundary/repeated timestamps; separate actual producer test above.
        using var f=new Fixture();var wait=new NativeWaitForImagesRunner(f.M,2,VisionMode.DetectingMarkers,100);
        try
        {
            Assert.Equal(0,wait.Init());
            void Send(uint time,uint mode)=>f.Rig.Engine.AnimationMessages.Deliver(0x43,true,new ProcessedImageReport(time,new uint[]{mode},0));
            Send(100,1);Send(101,2);Assert.Equal(0,wait.Count);Send(101,1);Send(101,1);
            Assert.Equal(2,wait.Count);Assert.Equal(0u,wait.CheckIfDone());Assert.False(wait.Subscribed);
            Assert.Equal(0,wait.Init());Assert.Equal(0,wait.Count);wait.State=EngineActionResult.Running;wait.Cancel();Assert.True(wait.Subscribed);
        }
        finally{wait.Prep();wait.WatcherEnding();}
        Assert.False(wait.Subscribed);
    }
    [Fact]
    public void QueryRunsWhileWaitingAndMatchOverridesTheWaitResult()
    {
        using var f=new Fixture();var target=f.Add(7,new Vec3(120,0,22));
        var runner=f.Robot.NativeBehaviorRuntime!.CreatePickupVisualVerification(7);
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,runner,0);
        for(int i=0;i<12&&(runner.State!=EngineActionResult.Running||runner.Positioning);i++){f.PumpMotors();if(i>12)throw new InvalidOperationException();}
        f.Rig.Tick();Assert.True(runner.Wait!.Subscribed);Assert.Equal(0,runner.Wait.Count);
        var obstacle=f.Add(8,new Vec3(120,0,66));obstacle.LastObservedTimestamp=uint.MaxValue;
        f.Rig.Tick();Assert.Equal(0x0300001Du,runner.State);Assert.True(runner.DestructionObserved);Assert.False(runner.Wait?.Subscribed??false);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void RadiansClampUsesEpsilonAndCopiesTheAlreadyConstructedOutput(bool lower)
    {
        // Supplied ComputeHead output unit: exercises the real checked clamp, not a replacement runner.
        using var root=new Pose3d("root");using var main=new Pose3d("main",root);
        using var goal=new Pose3d(Rotation3d.Identity,new Vec3(100,0,0),parent:root);
        float limit=lower?TurnTowardsPoseCompound.HeadMinRad:TurnTowardsPoseCompound.HeadMaxRad;
        float output=lower?limit+Radians.Epsilon*.5f:limit-Radians.Epsilon*.5f;
        var spec=new TurnTowardsPoseCompound(goal,Radians.Pi);
        try
        {
            var env=new TurnTowardsPoseEnv(main,0,_=>output,null);
            Assert.Equal(0u,spec.InitPose(env,out bool build));Assert.True(build);
            Assert.Equal(BitConverter.SingleToUInt32Bits(limit),BitConverter.SingleToUInt32Bits((float)spec.HeadAngleRad));
        }
        finally{spec.Pose?.Dispose();}
    }
    [Fact]
    public void TurnRunnerUsesItsCurrentSingleTagAndRetiresBothOriginalAndChangedReservations()
    {
        using var f=new Fixture();
        var runner=new NativeTurnInPlaceRunner(f.M,new TurnInPlaceSpec(.5,false,.05,true,false,0,10,false));
        uint original=runner.Tag;Assert.True(runner.SetTag(987655));
        try
        {
            Assert.Equal(0,runner.Init());
            var core=(TurnInPlaceAction)typeof(NativeTurnInPlaceRunner).GetField("_turn",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(runner)!;
            Assert.Equal(runner.Tag,core.Tag);Assert.True(ActionRunnerTagCounter.Global.IsInUse(original));Assert.True(ActionRunnerTagCounter.Global.IsInUse(987655));
        }
        finally{runner.Prep();runner.WatcherEnding();}
        Assert.False(ActionRunnerTagCounter.Global.IsInUse(original));Assert.False(ActionRunnerTagCounter.Global.IsInUse(987655));
    }

    [Fact]
    public void NativeForeignRootFailsConversionWithoutEnteringThePublicFlatAdapter()
    {
        using var foreignRoot=new Pose3d("foreign");using var foreignGoal=new Pose3d(Rotation3d.Identity,new Vec3(100,100,60),parent:foreignRoot);
        using var f=new Fixture();var runner=new NativeTurnTowardsPoseRunner(f.M,foreignGoal,Radians.Pi);
        try
        {
            int mark=f.Rig.Mark();
            Assert.Equal(unchecked((int)TurnTowardsPoseCompound.BadPose),runner.Init());
            Assert.Equal(0x06000000u,TurnTowardsPose.ComputeHeadAngleToSeePose(f.M.Vision.Calibration,foreignGoal,.01f,out _,null,f.Robot.GetCheckedMainPose()));
            Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is SetBodyAngle or SetHeadAngle);
        }
        finally{runner.Prep();runner.WatcherEnding();}
    }

}