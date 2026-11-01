using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using System.Reflection;
using System.Text.Json;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class LookForFaceAndCubeNativeTests
{
    private static NativeLookForFaceAndCubeBehavior Look(PickupPyramidNativeExecutionTests.Fixture f)
        =>Assert.IsType<NativeLookForFaceAndCubeBehavior>(f.Runtime.Bound["PutDownDispatch_LookForFaceAndCube"]);
    private static void Start(PickupPyramidNativeExecutionTests.Fixture f,NativeLookForFaceAndCubeBehavior look)
    {
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(look.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Same(look,f.Robot.BehaviorManagerOwner.Current);
    }
    [Fact]
    public void CanonicalFreeplayNaturallySelectsLookOnFloor()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        f.Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsPickedUp);
        f.Rig.State(RobotStatusFlag.IsBodyAccMode);
        var look=Look(f);int mark=f.Rig.Mark();
        f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
        for(int i=0;i<40 && !ReferenceEquals(f.Robot.BehaviorManagerOwner!.Current,look);i++)f.Rig.Tick(50);
        Assert.Same(f.Runtime.FreeplayObserver,f.Robot.BehaviorManagerOwner!.Activity);
        Assert.Equal("PutDownDispatch",f.Runtime.FreeplayObserver.Current?.Id);
        Assert.Same(look,f.Robot.BehaviorManagerOwner.Current);
        Assert.NotNull(look.OwnedRunner);
        Assert.Contains(f.Rig.SentSince(mark),m=>m is SetHeadAngle or SetLiftHeight);
    }
    [Fact]
    public void ShippedInitialActionUsesActualLiftAndTwoRngDraws()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var look=Look(f);int words=0;
        f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        try
        {
            Start(f,look);Assert.Equal(0,look.NativeState);Assert.Equal(4,words);
            var initial=Assert.IsType<CompoundActionParallel>(look.OwnedRunner);Assert.Equal(2,initial.Children.Count);
            Assert.Equal(32f,Assert.IsType<float>(initial.Children[0].GetType().GetField("Target")!.GetValue(initial.Children[0])));
            var pan=Assert.IsType<NativePanTiltRunner>(initial.Children[1]);
            float scale=BitConverter.UInt32BitsToSingle(0x3C8EFA35);
            Assert.Equal(30f*scale,(float)pan.Parameters.HeadAngleRad);
            Assert.Equal(180f*scale,(float)pan.Parameters.MaxSpeedRadPerSec);
            Assert.Equal(BitConverter.SingleToUInt32Bits(180f*scale),pan.Parameters.HeadSpeedAccelWord0);
            Assert.True(pan.Parameters.PanIsAbsolute);Assert.True(pan.Parameters.HeadIsAbsolute);
        }
        finally { f.Rig.Engine.ContextRandom.WordSourceForTest=null; }
    }
    [Fact]
    public void FaceEventReplacesMotionWithRawIdTurnAndOuterWait()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var look=Look(f);Start(f,look);
        // Supplied native observation, negative raw ID: source adds the one-image outer wait.
        f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(-2,1));
        Assert.True(look.Verifying);
        var sequence=Assert.IsType<CompoundActionSequential>(look.OwnedRunner);
        Assert.Collection(sequence.Children,turn=>Assert.Equal(-2,Assert.IsType<NativeLastFaceRunner>(turn).RequestedId),
            wait=>{var images=Assert.IsType<NativeWaitForImagesRunner>(wait);Assert.Equal(1,images.RequiredFrames);Assert.Equal((VisionMode)0x10,images.Mode);});
        uint tag=look.CurrentRunnerTag;
        f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(-3,2));Assert.Equal(tag,look.CurrentRunnerTag);
    }
    [Fact]
    public void DeliveredFaceAfterStopAndDisposeCannotResurrectAction()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var look=Look(f);Start(f,look);
        f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        look.Dispose();
        f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(-2,1));
        Assert.False(look.EngineRunning);Assert.Null(look.OwnedRunner);Assert.Equal(0u,look.CurrentRunnerTag);
    }
    [Fact]
    public void OldCompletionAfterReplacementCannotAdvanceNewRun()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var look=Look(f);Start(f,look);
        uint old=look.CurrentRunnerTag;f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        Start(f,look);uint current=look.CurrentRunnerTag;Assert.NotEqual(old,current);
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(old,0x14,0,Array.Empty<uint>(),0));
        Assert.Equal(current,look.CurrentRunnerTag);Assert.Equal(0,look.NativeState);
    }
    [Fact]
    public void RawMissingFaceDoesNotFallBackToUnrelatedLastFace()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var tracked=new TrackedFace(new DetectedFace(31,new FaceRect(0,0,10,10),Name:"Ada"),123)
        {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(200,0,100),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId))};
        Assert.NotNull(f.M.Vision.Faces.AddOrUpdateFace(tracked,f.Robot.GetCheckedMainPose(),false));
        var turn=new NativeLastFaceRunner(f.M,f.Robot,99);
        try {Assert.Equal(0,turn.Init());Assert.Equal(99,turn.RequestedId);Assert.Equal(3,turn.FaceState);Assert.Equal(0,turn.VerifiedId);}
        finally {turn.Prep();turn.WatcherEnding();}
    }
    [Fact]
    public void NamedAndMissingPositiveHaveNoOuterWaitNegativeFailureRetries()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var look=Look(f);Start(f,look);
        var tracked=new TrackedFace(new DetectedFace(31,new FaceRect(0,0,10,10),Name:"Ada"),123)
        {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(200,0,100),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId))};
        Assert.NotNull(f.M.Vision.Faces.AddOrUpdateFace(tracked,f.Robot.GetCheckedMainPose(),false));
        f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(31,123));
        Assert.Single(Assert.IsType<CompoundActionSequential>(look.OwnedRunner).Children);
        f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);Start(f,look);
        f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(99,124));
        Assert.Single(Assert.IsType<CompoundActionSequential>(look.OwnedRunner).Children);
        var missing=Assert.IsType<CompoundActionSequential>(look.OwnedRunner);missing.State=0x03000018;f.Robot.ActionList.Update();
        f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(-2,125));
        var negative=Assert.IsType<CompoundActionSequential>(look.OwnedRunner);negative.State=0x03000018;f.Robot.ActionList.Update();
        Assert.DoesNotContain(-2,look.Verified);
        f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(-2,126));Assert.True(look.Verifying);
    }
    [Fact]
    public void FailedActionCompletionsAdvanceEightPanSequenceAndWriteDesiredAtSix()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var look=Look(f);int words=0;
        f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        try
        {
            Start(f,look);var parent=f.Runtime.FreeplayObserver;
            Assert.Null(parent.DesiredActivityCacheForTest);
            int[] next={1,1,2,2,3,4,5,6};
            float[] body={0,10,10,-10,-10,10,-10,0};
            float[] head={30,30,30,30,30,-5,-5,-5};
            float scale=BitConverter.UInt32BitsToSingle(0x3C8EFA35);
            for(int i=0;i<next.Length;i++)
            {
                var runner=Assert.IsAssignableFrom<ActionRunner>(look.OwnedRunner);
                var pan=i==0?Assert.IsType<NativePanTiltRunner>(Assert.IsType<CompoundActionParallel>(runner).Children[1]):Assert.IsType<NativePanTiltRunner>(runner);
                Assert.Equal(body[i]*scale,(float)pan.Parameters.PanAngleRad);
                Assert.Equal(head[i]*scale,(float)pan.Parameters.HeadAngleRad);
                runner.State=0x03000018;f.Robot.ActionList.Update();
                Assert.Equal(next[i],look.NativeState);
                if(next[i]<6)Assert.IsType<NativePanTiltRunner>(look.OwnedRunner);
            }
            Assert.Equal(32,words);Assert.Equal("Hiking",parent.DesiredActivityCacheForTest);
            Assert.Null(look.OwnedRunner);
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }
    [Theory]
    [InlineData(0,1)] [InlineData(1,2)] [InlineData(2,3)] [InlineData(3,4)] [InlineData(4,5)]
    [InlineData(5,5)] [InlineData(6,6)]
    public void ResumeUsesNativeNextStateJumpTable(int prior,int expected)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var look=Look(f);Start(f,look);
        f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        typeof(NativeLookForFaceAndCubeBehavior).GetField("_state",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(look,prior);
        using var scope=new BehaviorScope();look.AttachRun(f.Runtime.Context,scope);
        bool failed=look.Resume(null,f.Rig.Engine.Timer.SecondsF);
        Assert.Equal(prior==6,failed);Assert.Equal(expected,look.NativeState);
        if(prior<5)Assert.IsType<NativePanTiltRunner>(look.OwnedRunner);else Assert.Null(look.OwnedRunner);
    }
    [Fact]
    public void SmartFaceCopiesAreIndependentAndFollowActualIdEvents()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        using var original=new NativeSmartFaceReference(f.Rig.Robot,21,f.Robot);
        using var copy=new NativeSmartFaceReference(f.Rig.Robot,owner:f.Robot);copy.CopyFrom(original);
        original.Reset();Assert.Equal(21,copy.Id);original.UpdateToId(21);
        f.Rig.Engine.AnimationMessages.Deliver(0x47,true,new NativeChangedFace(21,22));
        Assert.Equal(22,original.Id);Assert.Equal(22,copy.Id);
        original.Dispose();f.Rig.Engine.AnimationMessages.Deliver(0x4B,true,new NativeDeletedFace(22));Assert.Equal(0,copy.Id);
    }
    [Fact]
    public void FailedVerificationStillChecksOriginalRawFaceNameAfterSmartIdChanges()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var look=Look(f);Start(f,look);
        // Supplied tracker result in actual FaceWorld; outer callback retains raw ID 31.
        var tracked=new TrackedFace(new DetectedFace(31,new FaceRect(0,0,10,10)),123)
        {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(200,0,100),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId))};
        var face=Assert.IsType<FaceObservation>(f.M.Vision.Faces.AddOrUpdateFace(tracked,f.Robot.GetCheckedMainPose(),false)).Face;
        f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(31,123));
        var sequence=Assert.IsType<CompoundActionSequential>(look.OwnedRunner);
        var turn=Assert.IsType<NativeLastFaceRunner>(sequence.Children[0]);Assert.Equal(31,turn.RequestedId);
        var wait=Assert.IsType<NativeWaitForImagesRunner>(sequence.Children[1]);Assert.Equal(3,wait.RequiredFrames);Assert.Equal((VisionMode)0x10,wait.Mode);
        f.Rig.Engine.AnimationMessages.Deliver(0x47,true,new NativeChangedFace(31,32));Assert.Equal(32,turn.RequestedId);
        face.Name="Ada"; // supplied name update before terminal action result.
        sequence.State=0x03000018;f.Robot.ActionList.Update();
        Assert.False(look.Verifying);Assert.DoesNotContain(31,look.Verified);Assert.Equal(6,look.NativeState);
        Assert.Null(look.OwnedRunner);
    }
    [Fact]
    public void SuccessfulUnnamedFaceIsStoredAndRepeatedEventDoesNotInterrupt()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var look=Look(f);Start(f,look);
        var tracked=new TrackedFace(new DetectedFace(31,new FaceRect(0,0,10,10)),123)
        {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(200,0,100),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId))};
        Assert.NotNull(f.M.Vision.Faces.AddOrUpdateFace(tracked,f.Robot.GetCheckedMainPose(),false));
        f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(31,123));
        var sequence=Assert.IsType<CompoundActionSequential>(look.OwnedRunner);sequence.State=0;f.Robot.ActionList.Update();
        Assert.Contains(31,look.Verified);Assert.False(look.Verifying);Assert.Equal(1,look.NativeState);
        uint tag=look.CurrentRunnerTag;f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(31,124));
        Assert.Equal(tag,look.CurrentRunnerTag);
    }
    [Fact]
    public void RetainedFreeplayCalculationUsesCurrentLocatedFamilyAndDoesNotSelect()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var look=Look(f);var parent=f.Runtime.FreeplayObserver;
        Assert.Null(f.Robot.BehaviorManagerOwner!.Activity);
        Assert.True(f.Runtime.CalculateLookDesired(look));Assert.Equal("Hiking",parent.DesiredActivityCacheForTest);
        f.Add(7);Assert.True(f.Runtime.CalculateLookDesired(look));Assert.Equal("PlayAlone",parent.DesiredActivityCacheForTest);
        // Supplied named face observation goes through the actual FaceWorld last-pose cache.
        var face=new TrackedFace(new DetectedFace(31,new FaceRect(0,0,10,10),Name:"Ada"),123)
        {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(200,0,100),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId))};
        Assert.NotNull(f.M.Vision.Faces.AddOrUpdateFace(face,f.Robot.GetCheckedMainPose(),false));
        Assert.True(f.Runtime.CalculateLookDesired(look));Assert.Equal("Socialize",parent.DesiredActivityCacheForTest);
        Assert.Null(f.Robot.BehaviorManagerOwner.Activity);
        var world=f.M.World;var prior=f.Rig.Robot.LocalizationWorld.NativeConfigurations;
        try { f.Rig.Robot.LocalizationWorld.NativeConfigurations=null; /* configuration cache is not the queried owner */
            Assert.True(f.Runtime.CalculateLookDesired(look));Assert.Equal("Socialize",parent.DesiredActivityCacheForTest); }
        finally { world.NativeConfigurations=prior; }
    }
    [Theory]
    [InlineData(false,false,"Hiking")] [InlineData(false,true,"PlayAlone")]
    [InlineData(true,false,"Socialize")] [InlineData(true,true,"Socialize")]
    public void ActualRetainedCacheUsesFourShippedDesiredNames(bool face,bool cube,string expected)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var look=Look(f);
        if(face)
        {
            var tracked=new TrackedFace(new DetectedFace(31,new FaceRect(0,0,10,10),Name:"Ada"),123)
            {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(200,0,100),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId))};
            Assert.NotNull(f.M.Vision.Faces.AddOrUpdateFace(tracked,f.Robot.GetCheckedMainPose(),false));
        }
        if(cube){var item=f.Add(7);item.PoseState=PoseState.Dirty;}
        Assert.True(f.Runtime.CalculateLookDesired(look));Assert.Equal(expected,f.Runtime.FreeplayObserver.DesiredActivityCacheForTest);
        if(cube){f.Add(8,300);Assert.True(f.Runtime.CalculateLookDesired(look));Assert.Equal(expected,f.Runtime.FreeplayObserver.DesiredActivityCacheForTest);}
        Assert.Null(f.Robot.BehaviorManagerOwner!.Activity);
    }
    [Fact]
    public void NonCubeAndPriorOriginDoNotCountAsCurrentCube()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var look=Look(f);
        using var obstaclePose=new Pose3d(Rotation3d.Identity,new Vec3(400,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        f.M.World.AddCollisionObstacle(obstaclePose);
        Assert.True(f.Runtime.CalculateLookDesired(look));Assert.Equal("Hiking",f.Runtime.FreeplayObserver.DesiredActivityCacheForTest);
        f.Add(7);Assert.True(f.Runtime.CalculateLookDesired(look));Assert.Equal("PlayAlone",f.Runtime.FreeplayObserver.DesiredActivityCacheForTest);
        f.Robot.Delocalize(false);
        Assert.True(f.Runtime.CalculateLookDesired(look));Assert.Equal("Hiking",f.Runtime.FreeplayObserver.DesiredActivityCacheForTest);
    }
    [Fact]
    public void ReplacedRuntimeCannotOverwritePriorDesiredCache()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var runtime=f.Runtime;var look=Look(f);
        Assert.True(runtime.CalculateLookDesired(look));Assert.Equal("Hiking",runtime.FreeplayObserver.DesiredActivityCacheForTest);
        f.Add(7);f.Robot.NativeBehaviorRuntime=null;
        try { Assert.False(runtime.CalculateLookDesired(look));Assert.Equal("Hiking",runtime.FreeplayObserver.DesiredActivityCacheForTest);
            var readiness=look.EvaluateHostReadiness(runtime.Context);Assert.False(readiness.Available);Assert.False(readiness.Runnable); }
        finally {f.Robot.NativeBehaviorRuntime=runtime;}
    }
    [Fact]
    public void MissingChangedParametersAreUnavailableWithoutInventedDefaults()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        using var config=JsonDocument.Parse("{\"behaviorID\":\"PutDownDispatch_LookForFaceAndCube\",\"params\":{}}");
        using var look=new NativeLookForFaceAndCubeBehavior(new BehaviorFactoryContext{NativeRobot=f.Robot,Manipulation=f.M},config.RootElement);
        Assert.False(look.ParametersAvailable);Assert.False(look.ExecutionAvailable);var readiness=look.EvaluateHostReadiness(f.Runtime.Context);
        Assert.False(readiness.Available);Assert.False(readiness.Runnable);
    }
    [Fact]
    public void MissingActualOriginPreservesDesiredAndReportsUnavailable()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var runtime=f.Runtime;var look=Look(f);
        Assert.True(runtime.CalculateLookDesired(look));var prior=runtime.FreeplayObserver.DesiredActivityCacheForTest;
        var origins=(Dictionary<uint,Pose3d>)typeof(EngineRobot).GetField("_nativeOrigins",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(f.Robot)!;
        uint id=f.Robot.CurrentOriginId;var saved=origins[id];origins.Remove(id);
        try {Assert.False(runtime.CalculateLookDesired(look));Assert.Equal(prior,runtime.FreeplayObserver.DesiredActivityCacheForTest);
            Assert.False(look.EvaluateHostReadiness(runtime.Context).Available);}
        finally {origins.Add(id,saved);}
    }
}
