using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class BuildPyramidBaseNativeTests
{
    private static NativeBuildPyramidBaseBehavior Base(Fixture f)=>Assert.IsType<NativeBuildPyramidBaseBehavior>(f.Runtime.Bound["BuildPyramidBase"]);
    private static void Inputs(Fixture f,uint basis=7,uint stat=8,uint top=uint.MaxValue)
    {
        var b=Base(f);f.Robot.Progression!.Unlocked.Add(b.NativeBaseInputs!.RequiredUnlock);
        f.Runtime.PyramidTargetInput=i=>i switch{9=>basis,10=>stat,11=>top,_=>throw new InvalidOperationException()};
        f.Runtime.PyramidBaseStrongInput=()=>Array.Empty<NativePyramidBase>();
        f.Runtime.PyramidFullStrongInput=()=>Array.Empty<NativePyramidConfiguration>();
    }
    private static NativeBuildPyramidBaseBehavior Start(Fixture f)
    {
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync("BuildPyramidBase",f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());return Base(f);
    }
    private static void CancelOwned(Fixture f,NativeBuildPyramidBaseBehavior b)
    {
        // Explicit unit cancellation input through actual ActionList/watcher. Pickup cancellation
        // can first enqueue its real failure-response animation; cancel that owned tag too.
        // No completion/result/helper status is injected; excess work fails this fixture.
        for(int i=0;i<2&&b.HasCurrentAction;i++)
        {Assert.True(f.Robot.ActionList.Cancel(b.CurrentRunnerTag));f.Robot.ActionList.Watcher.Update();}
        Assert.False(b.HasCurrentAction);f.Runtime.UpdateAI();Assert.False(b.HasCurrentAction);Assert.Empty(f.Runtime.AI.Helpers.StackHelpers);
    }    [Fact]
    public void ConfiguredFactoryConstructsIndependentBaseAndAbsentProducersNeverMeanEmpty()
    {
        // reach: production factory/runtime; supplied unlock only. E1/R3:0059C9AC,005DCAAC..CAEC.
        using var f=new Fixture();var b=Base(f);f.Robot.Progression!.Unlocked.Add(b.NativeBaseInputs!.RequiredUnlock);
        Assert.Equal("BuildPyramidBase",b.Class);var missing=new List<string>();SteppedBehavior.MissingReported+=missing.Add;
        try
        {
            Assert.False(b.IsRunnable(f.Runtime.Context));Assert.Contains(missing,s=>s.Contains("intention9/10/11"));
            f.Runtime.PyramidTargetInput=i=>i==11?uint.MaxValue:(uint)i;
            Assert.False(b.IsRunnable(f.Runtime.Context));Assert.Contains(missing,s=>s.Contains("cache+48"));
            f.Runtime.PyramidBaseStrongInput=()=>Array.Empty<NativePyramidBase>();Assert.True(b.IsRunnable(f.Runtime.Context));
        }
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact]
    public void TargetRefreshOrderStoresInvalidsAndGettersPreserveOutput()
    {
        // reach: unit, supplied intention results; R1/G1:005DCAFA..CB4E,005DD5F4..D64C.
        using var f=new Fixture();var b=Base(f);var order=new List<int>();uint top=uint.MaxValue;
        f.Runtime.PyramidTargetInput=i=>{order.Add(i);return i switch{9=>0x80000001,10=>0xFFFFFFFE,_=>top};};
        Assert.True(b.RefreshTargets());Assert.Equal(new[]{9,10,11},order);order.Clear();Assert.False(b.RefreshTargets());
        uint value=123;Assert.False(b.GetTop(ref value));Assert.Equal(123u,value);
        Assert.True(b.GetBase(ref value));Assert.Equal(0x80000001u,value);Assert.True(b.GetStatic(ref value));Assert.Equal(0xFFFFFFFEu,value);
        top=19;Assert.True(b.RefreshTargets());Assert.True(b.GetTop(ref value));Assert.Equal(19u,value);
        f.Runtime.PyramidTargetInput=_=>uint.MaxValue;Assert.True(b.RefreshTargets());Assert.False(b.GetStatic(ref value));Assert.Equal(19u,value);
    }
    [Theory]
    [InlineData(7u,7u,uint.MaxValue,false,true)]
    [InlineData(7u,8u,uint.MaxValue,true,true)]
    [InlineData(7u,8u,7u,false,false)]
    [InlineData(uint.MaxValue,8u,8u,true,false)]
    public void UniquenessUtilityDoesNotAddARunnableGate(uint basis,uint stat,uint top,bool unique,bool runnable)
    {
        // reach: unit supplied native inputs. R3/G2:005DCAAC..CAEC,005DDA60..DAB6.
        using var f=new Fixture();Inputs(f,basis,stat,top);var b=Base(f);b.RefreshTargets();
        Assert.Equal(unique,b.AreAllBlockIdsUnique());Assert.Equal(runnable,b.IsRunnable(f.Runtime.Context));
        f.Runtime.PyramidBaseStrongInput=()=>new[]{new NativePyramidBase(7,8)};Assert.False(b.IsRunnable(f.Runtime.Context));
    }
    [Fact]
    public void InheritedAdmissionRejectsOffTreadsAllowsAnyCarriedIdAndRetainsUnlock()
    {
        // reach: unit inputs, actual common admission. Admission addendum005BD864..D89C slots0/0/1.
        using var f=new Fixture();Inputs(f);var b=Base(f);Assert.True(b.IsRunnable(f.Runtime.Context));
        b.RobotState355=()=>true;Assert.False(b.IsRunnable(f.Runtime.Context));b.RobotState355=()=>false;
        f.Robot.CarryingOwner!.SetCarrying(99);Assert.True(b.IsRunnable(f.Runtime.Context));
        f.Robot.Progression!.Unlocked.Remove(b.NativeBaseInputs!.RequiredUnlock);Assert.False(b.IsRunnable(f.Runtime.Context));
    }
    [Fact]
    public void AnyCarryEntersRealPlacementAndMissingStaticInvalidatesOnlyStatic()
    {
        // reach: configured manager with supplied9/10/11/caches/carry, actual helper owner.
        // I1/P2:005DCB58..B7E,005DCE88..CECA. No success/result override.
        using var f=new Fixture();Inputs(f);f.Robot.CarryingOwner!.SetCarrying(99);var b=Start(f);
        Assert.Equal(1,b.State);Assert.Equal(uint.MaxValue,b.StaticId);Assert.Equal(7u,b.BaseId);Assert.Equal(uint.MaxValue,b.TopId);
        Assert.Null(b.PlacementHelper);Assert.Empty(f.Robot.BehaviorManagerOwner!.InspectStateLights().Entries);
    }
    [Fact]
    public void ActualPlaceHelperRetainsNonzeroGeometryAndExposesUnbuiltDriveTuple()
    {
        // reach: configured manager with explicit native target/cache inputs; lower nonzero Drive1 remains MISSING.
        // P1/P3/B2:005DCE0E..CE44,005DD1B6..D2C0. No fabricated placement success.
        using var f=new Fixture();Inputs(f);f.Add(7,150);f.Add(8,400);f.Robot.CarryingOwner!.SetCarrying(7);
        var missing=new List<string>();SteppedBehavior.MissingReported+=missing.Add;
        try
        {
            var b=Start(f);var helper=Assert.IsType<NativePlaceRelObjectHelper>(b.PlacementHelper);
            Assert.Equal(8u,helper.Target);Assert.True(helper.Low);Assert.False(helper.SkipTransform);
            Assert.Equal(-56f,helper.A);Assert.Equal(0f,helper.B);
            Assert.Contains(missing,s=>s.Contains("nonzero placement Drive1 tuple"));
            Assert.Null(helper.Runner);Assert.Equal(1,b.State);
        }
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact]
    public void ResetPreservesTargetsFlagAndOptionalCallback()
    {
        // reach: unit reset; I1:005DCB80..B90, constructor005DCA50..CA96.
        using var f=new Fixture();Inputs(f,7,8,9);var b=Base(f);b.RefreshTargets();Action<EngineRobot> callback=_=>{};
        Assert.False(b.StopWhenFull);Assert.Null(b.ObservationFinished);b.StopWhenFull=true;b.ObservationFinished=callback;b.ResetBehavior();
        Assert.Equal((8u,7u,9u),(b.StaticId,b.BaseId,b.TopId));Assert.True(b.StopWhenFull);Assert.Same(callback,b.ObservationFinished);
        Assert.Equal(0,b.PreviousBaseCount);Assert.Equal(-1f,b.Appeared);Assert.Equal(-1f,b.Lost);
    }
    [Fact]
    public void ObservationOwnsRealDriveThenLiftSafeActionAndCancellationDispatchesActualRecipient()
    {
        // reach: unit supplied Observe entry, actual ActionList/watcher/callback; native result-ignoring O3.
        // O1..O3:005DD4AC..D56C,005DDC62..C74. Success through missing placement Drive1 is not claimed.
        using var f=new Fixture();Inputs(f);f.Add(7);var b=Start(f);CancelOwned(f,b);int calls=0;
        b.ObservationFinished=r=>{Assert.Same(f.Robot,r);calls++;};b.Observe();
        var sequence=Assert.IsType<CompoundActionSequential>(b.OwnedRunner);Assert.Equal(2,sequence.Children.Count);
        var drive=Assert.IsType<DriveStraightAction>(sequence.Children[0]);Assert.Equal(-40,drive.DistanceMm);Assert.Equal(40f,drive.SpeedMmps);Assert.True(drive.ShouldPlayAnimation);
        var anim=Assert.IsType<TriggerLiftSafeAnimationAction>(sequence.Children[1]);Assert.Equal((AnimationTrigger)20,anim.Request.Trigger);Assert.Equal(1,anim.Request.Argument2);Assert.True(anim.Request.Argument3);Assert.Equal(0,anim.Request.Argument4);Assert.Equal(60f,anim.Request.Argument5);Assert.False(anim.Request.Argument6);
        Assert.True(f.Robot.ActionList.Cancel(sequence.Tag));f.Robot.ActionList.Watcher.Update();
        b.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF*1000.0);Assert.Equal(1,calls);Assert.Equal(2,b.State);
    }
    [Fact]
    public void BasePlacementCandidateOrderAndFailedDistanceRetentionUseExistingNativeGeometry()
    {
        // reach: unit geometry with supplied disconnected Robot/object origins. B2/B7:005DD302..D370.
        using var f=new Fixture();var stat=f.Add(8,400);var moving=f.Add(7,150);
        using var disconnected=new Pose3d();stat.Pose=new Pose3d(Rotation3d.Identity,new(400,0,22),parent:disconnected);
        moving.Pose=new Pose3d(Rotation3d.Identity,new(150,0,22),parent:disconnected);
        float a=123,b=456;PlacementProducerGeometry.UpdateOffsets(f.M,8,7,ref a,ref b);
        Assert.Equal(-56f,a);Assert.Equal(0f,b); // retained candidate.x becomes distance; last -56 is strictly smallest.
        a=123;b=456;PlacementProducerGeometry.UpdateOffsets(f.M,1234,7,ref a,ref b);Assert.Equal(123f,a);Assert.Equal(456f,b);
    }
    private static void HoldActualAction(Fixture f,NativeBuildPyramidBaseBehavior b)
    {
        // Unit substitutes the current +84 action input with a real owned runner, never a result/status.
        CancelOwned(f,b);b.StartActing(new NativeTimedWaitRunner(f.M,100),null);
    }
    [Fact]
    public void PresentBaseStopsOnlyStrictlyAfterOrderedFloatDeadlineAndTailNeedsActualAction()
    {
        // reach: unit native cache vector/current action inputs, actual manager/timer/ActionList.
        // U1/U2/U5:005DD026..D0BA,005DD10E..D12C; shared005BDA56..DA62.
        using var f=new Fixture();Inputs(f);f.Add(7);var b=Start(f);HoldActualAction(f,b);
        f.Runtime.PyramidBaseStrongInput=()=>new[]{new NativePyramidBase(7,8)};f.Rig.Tick(0);
        float stamp=f.Rig.Engine.Timer.SecondsF;Assert.Equal(stamp,b.Appeared);Assert.Equal(1,b.PreviousBaseCount);
        float deadline=(stamp+2f)+BitConverter.UInt32BitsToSingle(0x3727C5AC);
        f.Rig.Tick((deadline-f.Rig.Engine.Timer.SecondsF)*1000.0);Assert.Equal(deadline,f.Rig.Engine.Timer.SecondsF);Assert.True(b.EngineRunning);
        f.Rig.Tick(.001);Assert.False(b.EngineRunning);Assert.False(b.HasCurrentAction);Assert.Equal(stamp,b.Appeared);
    }
    [Fact]
    public void LostTimerRequiresPreviouslyPresentBaseAndStrictDeadline()
    {
        // reach: unit explicit cache vectors/current runner, actual source clocks and stop path.
        // U1/U3/U4/U5:005DD046..D12C.
        using var f=new Fixture();Inputs(f);f.Add(7);var b=Start(f);HoldActualAction(f,b);
        f.Rig.Tick(0);Assert.Equal(-1f,b.Lost);
        f.Runtime.PyramidBaseStrongInput=()=>new[]{new NativePyramidBase(7,8)};CancelOwned(f,b);b.Observe();f.Rig.Tick(0);
        f.Runtime.PyramidBaseStrongInput=()=>Array.Empty<NativePyramidBase>();f.Rig.Tick(0);float lost=b.Lost;Assert.True(lost>0);
        f.Rig.Tick(2000);Assert.True(b.EngineRunning);f.Rig.Tick(.1);Assert.False(b.EngineRunning);
    }
    [Fact]
    public void CacheAvailabilityDuringActualRunStopsVisiblyRatherThanManufacturingCompletion()
    {
        // reach: configured manager with supplied native target/cache inputs, actual runtime producer loss.
        using var f=new Fixture();Inputs(f);f.Add(7);var b=Start(f);HoldActualAction(f,b);
        var missing=new List<string>();SteppedBehavior.MissingReported+=missing.Add;
        try{f.Runtime.PyramidFullStrongInput=null;f.Rig.Tick();Assert.False(b.EngineRunning);Assert.Contains(missing,s=>s.Contains("cache+64"));}
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }    [Fact]
    public void FullCacheFlagStopsImmediatelyWithoutInventedCacheGoal()
    {
        // reach: unit explicit derived flag/cache/action inputs; U4/U5:005DD100..D12C,005BDA56..DA62.
        using var f=new Fixture();Inputs(f);f.Add(7);var b=Start(f);HoldActualAction(f,b);
        f.Runtime.PyramidFullStrongInput=()=>new[]{new NativePyramidConfiguration(new NativePyramidBase(7,8),9)};
        f.Rig.Tick(0);Assert.True(b.EngineRunning);b.StopWhenFull=true;f.Rig.Tick(0);Assert.False(b.EngineRunning);
    }    [Fact]
    public void ConfiguredNoCarryDelegatesActualPickupAndFailureAddsNoPlaceTransition()
    {
        // reach: configured manager; explicit target/cache and raw helper failure inputs, actual shared delegate.
        // D1..D3:005DCBBC..CC78; failure callable empty. No synthetic success.
        using var f=new Fixture();Inputs(f);f.Add(7);var b=Start(f);var helper=Assert.IsType<NativePickupBlockHelper>(b.PickupHelper);
        Assert.Equal(0,b.State);Assert.Equal(7u,helper.Target);Assert.Equal(0f,helper.MaxTurn);Assert.False(helper.Flag104);Assert.False(helper.Flag105);
        var light=Assert.Single(f.Robot.BehaviorManagerOwner!.InspectStateLights().Entries);Assert.Equal(7u,light.ObjectId);Assert.Equal(0x20u,light.Trigger);Assert.False(light.Modifier);
        helper.Respond(new RobotCompletedAction(123,0x16,0x03000004,Array.Empty<uint>(),0));
        f.Rig.Tick();Assert.Equal(0,b.State);Assert.Null(b.PlacementHelper);Assert.Equal(7u,b.BaseId);Assert.Equal(8u,b.StaticId);
    }
    [Fact]
    public void PickupPreAnimationIsOrdinaryFirstChildAndConsumedBeforeExecutionOnlyOnce()
    {
        // reach: unit explicit helper constructor/compound entry, real animation/pickup children.
        // B1c..f:005B7AB8..ABC,005B7CFC..7D4E,005B7E8C; no result/status injection.
        using var f=new Fixture();var b=Base(f);
        var helper=new NativePickupBlockHelper(f.M,b,f.Runtime.AI.Helpers.Factory,7,0f,false,false,0x16);
        var pickup=new NativePickupObjectRunner(f.M,7);var first=helper.BuildPickupCompound(pickup,null,null);
        try
        {
            Assert.Equal(2,first.Children.Count);var animation=Assert.IsType<TriggerAnimationAction>(first.Children[0]);
            Assert.Equal((AnimationTrigger)0x16,animation.Request.Trigger);Assert.Equal(1,animation.Request.Argument2);Assert.True(animation.Request.Argument3);
            Assert.Equal(0,animation.Request.Argument4);Assert.Equal(60f,animation.Request.Argument5);Assert.False(animation.Request.Argument6);
            Assert.Same(pickup,first.Children[1]);Assert.Equal(0x23F,helper.PreAnimationTrigger);
            var retryPickup=new NativePickupObjectRunner(f.M,7);var retry=helper.BuildPickupCompound(retryPickup,null,null);
            try{Assert.Same(retryPickup,Assert.Single(retry.Children));}finally{retry.WatcherEnding();}
            var ordinary=new NativePickupBlockHelper(f.M,b,f.Runtime.AI.Helpers.Factory,7,0f,false,false);
            var ordinaryPickup=new NativePickupObjectRunner(f.M,7);var compound=ordinary.BuildPickupCompound(ordinaryPickup,null,null);
            try{Assert.Same(ordinaryPickup,Assert.Single(compound.Children));}finally{compound.WatcherEnding();}
        }
        finally{first.WatcherEnding();}
    }}
