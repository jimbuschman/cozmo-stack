using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class BuildPyramidNativeTests
{
    private static NativeBuildPyramidBehavior Derived(Fixture f)=>Assert.IsType<NativeBuildPyramidBehavior>(f.Runtime.Bound["BuildPyramid"]);
    private static void Inputs(Fixture f,uint basis=7,uint stat=8,uint top=9)
    {
        var b=Derived(f);f.Robot.Progression!.Unlocked.Add(b.NativeBaseInputs!.RequiredUnlock);
        f.Runtime.PyramidTargetInput=i=>i switch{9=>basis,10=>stat,11=>top,_=>throw new InvalidOperationException()};
        f.Runtime.PyramidBaseStrongInput=()=>Array.Empty<NativePyramidBase>();
        f.Runtime.PyramidFullStrongInput=()=>Array.Empty<NativePyramidConfiguration>();
    }
    private static NativeBuildPyramidBehavior Start(Fixture f)
    {
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync("BuildPyramid",f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());return Derived(f);
    }
    [Fact]
    public void ConfiguredDerivedFactoryUsesOwnPredicateAndKeepsUnavailableInputsVisible()
    {
        // reach: actual configured factory; unit supplied unlock/intention/cache inputs.
        // E1/R1:0059C98A,005DBED4..BEFC: no cache gate and all three IDs required.
        using var f=new Fixture();var b=Derived(f);Assert.Equal("BuildPyramid",b.Class);
        f.Robot.Progression!.Unlocked.Add(b.NativeBaseInputs!.RequiredUnlock);
        Assert.False(b.IsRunnable(f.Runtime.Context));Inputs(f);
        f.Runtime.PyramidBaseStrongInput=null;f.Runtime.PyramidFullStrongInput=null;Assert.True(b.IsRunnable(f.Runtime.Context));
        foreach(int absent in new[]{9,10,11})
        {f.Runtime.PyramidTargetInput=i=>i==absent?uint.MaxValue:(uint)i;Assert.False(b.IsRunnable(f.Runtime.Context));}
        f.Runtime.PyramidTargetInput=_=>7;Assert.True(b.IsRunnable(f.Runtime.Context)); // no invented uniqueness check
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void EmptyCachesEnterInheritedBaseWithActualDerivedClassIdentity(bool carried)
    {
        // reach: actual manager and helpers; unit supplied native9/10/11/caches/carry.
        // I3:005DBF66..BF84. Empty caches are supplied, not manufactured by production.
        using var f=new Fixture();Inputs(f);f.Add(7);f.Add(8,400);if(carried)f.Robot.CarryingOwner!.SetCarrying(99);
        var b=Start(f);Assert.Equal(carried?1:0,b.State);
        Assert.NotNull(b.ObservationFinished);Assert.Equal("BuildPyramid",b.Class);
        Assert.Equal((byte)2,f.Robot.BehaviorManagerOwner!.InspectStateLights().Class); // B2:this+64,005BEE3E
        if(carried)Assert.NotNull(b.PlacementHelper);else Assert.Equal(7u,Assert.IsType<NativePickupBlockHelper>(b.PickupHelper).Target);
    }
    [Fact]
    public void ExistingBaseWithoutCarryDelegatesStoredTopWithOneUseTrigger()
    {
        // reach: configured manager/helper, supplied strong cache and intentions; D1/D2:005DBD46..BD9C.
        using var f=new Fixture();Inputs(f);f.Add(9,400);
        f.Runtime.PyramidBaseStrongInput=()=>new[]{new NativePyramidBase(7,8)};
        var b=Start(f);Assert.Equal(3,b.State);var helper=Assert.IsType<NativePickupBlockHelper>(b.PickupHelper);
        Assert.Equal(9u,helper.Target);Assert.Equal(0f,helper.MaxTurn);Assert.False(helper.Flag104);Assert.False(helper.Flag105);
        var pickup=new NativePickupObjectRunner(f.M,9);var compound=helper.BuildPickupCompound(pickup,null,null);
        try{Assert.Equal((AnimationTrigger)0x1A,Assert.IsType<TriggerAnimationAction>(compound.Children[0]).Request.Trigger);Assert.Same(pickup,compound.Children[1]);Assert.Equal(0x23F,helper.PreAnimationTrigger);}
        finally{compound.WatcherEnding();}
    }
    [Fact]
    public void AnyCarryRoutesToTopAndNullLookupInvalidatesOnlyStatic()
    {
        // reach: configured manager; unit cache/intentions/carry. P2:005DC0DA..DC148.
        using var f=new Fixture();Inputs(f);f.Robot.CarryingOwner!.SetCarrying(99);
        f.Runtime.PyramidBaseStrongInput=()=>new[]{new NativePyramidBase(7,8)};
        var b=Start(f);Assert.Equal(5,b.State);Assert.Equal(uint.MaxValue,b.StaticId);Assert.Equal(7u,b.BaseId);Assert.Equal(9u,b.TopId);
        Assert.Null(b.PlacementHelper);Assert.False(b.StopWhenFull);
        var manager=f.Robot.BehaviorManagerOwner!;
        // P1:00C6C691 and005BCE3C..84: only enum8 and exact own-name suffix; Stop owns removal.
        for(int i=0;i<21;i++)Assert.Equal(i==8,manager.HasDisableLock((ReactionTrigger)i,"BuildPyramid_behaviorLock"));
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        for(int i=0;i<21;i++)Assert.False(manager.HasDisableLock((ReactionTrigger)i,"BuildPyramid_behaviorLock"));
    }
    [Fact]
    public void TopPlacementFeedsActualMidpointRelativeToStaticIntoRealHelper()
    {
        // reach: configured manager/geometry/helper; explicit cache/intentions/carry boundary.
        // P4/P5:005DC1D4..DC266. Nonzero PlaceRel Drive1 is still MISSING, no success injected.
        using var f=new Fixture();Inputs(f);f.Add(7,150);f.Add(8,400);f.Robot.CarryingOwner!.SetCarrying(99);
        f.Runtime.PyramidBaseStrongInput=()=>new[]{new NativePyramidBase(7,8)};
        var b=Start(f);var helper=Assert.IsType<NativePlaceRelObjectHelper>(b.PlacementHelper);
        Assert.Equal(5,b.State);Assert.Equal(8u,helper.Target);Assert.False(helper.Low);Assert.Equal(-22f,helper.A);Assert.Equal(0f,helper.B);Assert.False(helper.SkipTransform);
    }
    [Fact]
    public void BaseObservationCompletionUsesInstalledDerivedRecipient()
    {
        // reach: unit Observe entry with actual ActionList cancellation/watcher result-ignoring dispatch.
        // E1/O3:005DBCA4..BCB2,005DDC62..C74. No synthetic placement/pickup success.
        using var f=new Fixture();Inputs(f);f.Add(7);f.Add(9,400);var b=Start(f);
        for(int i=0;i<2&&b.HasCurrentAction;i++)
        {Assert.True(f.Robot.ActionList.Cancel(b.CurrentRunnerTag));f.Robot.ActionList.Watcher.Update();}
        Assert.False(b.HasCurrentAction);f.Runtime.UpdateAI();Assert.Empty(f.Runtime.AI.Helpers.StackHelpers);
        b.Observe();var sequence=Assert.IsType<CompoundActionSequential>(b.OwnedRunner);
        Assert.True(f.Robot.ActionList.Cancel(sequence.Tag));f.Robot.ActionList.Watcher.Update();
        b.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF*1000.0);
        Assert.Equal(3,b.State);Assert.Equal(9u,Assert.IsType<NativePickupBlockHelper>(b.PickupHelper).Target);
    }
    [Fact]
    public void FullCachePersistentFlagReactsThroughRealExternalInterfaceBeforeLiftSafe()
    {
        // reach: actual manager/report/action; explicit cache/persistent flag boundary.
        // I1/A1..A4:005DBF12..BF26,005DBFB0..DC00A; ExternalInterface union007285FE.
        using var f=new Fixture();Inputs(f);var b=Derived(f);b.StopWhenFull=true;
        f.Runtime.PyramidFullStrongInput=()=>new[]{new NativePyramidConfiguration(new NativePyramidBase(7,8),9)};
        var payloads=new List<byte[]>();f.Rig.Engine.EngineToGameMessage+=(tag,p)=>{if(tag==0x8B)payloads.Add(p.ToArray());};
        using var local=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>
        {Assert.Equal(3u,Assert.IsType<uint>(p));Assert.Equal(6,b.State);Assert.False(b.HasCurrentAction);});
        Start(f);Assert.False(b.StopWhenFull);Assert.Equal(6,b.State);
        var anim=Assert.IsType<TriggerLiftSafeAnimationAction>(b.OwnedRunner);
        Assert.Equal((AnimationTrigger)0x17,anim.Request.Trigger);Assert.Equal(1,anim.Request.Argument2);Assert.True(anim.Request.Argument3);
        Assert.Equal(0,anim.Request.Argument4);Assert.Equal(60f,anim.Request.Argument5);Assert.False(anim.Request.Argument6);
        f.Rig.Engine.DrainAppDelivery();Assert.Equal(new byte[]{3,0,0,0},Assert.Single(payloads));
        Assert.True(f.Robot.ActionList.Cancel(anim.Tag));f.Robot.ActionList.Watcher.Update();
        b.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF*1000.0);Assert.Equal(6,b.State); // no Idle callback
    }
    [Fact]
    public void RegisteredFailureRecipientWhileStillCarryingDoesNothing()
    {
        // reach: unit direct invocation of same registered PlacementFailed recipient; no helper success/result injected.
        // C2:005DC802..80A. Full placement Drive1 remains MISSING; this is not full helper-pipeline coverage.
        using var f=new Fixture();Inputs(f);f.Add(7,150);f.Add(8,400);f.Robot.CarryingOwner!.SetCarrying(99);
        f.Runtime.PyramidBaseStrongInput=()=>new[]{new NativePyramidBase(7,8)};var b=Start(f);
        Assert.NotNull(b.PlacementHelper); // actual helper construction registered the recipient under test
        b.PlacementFailed();Assert.Equal(5,b.State);Assert.False(b.StopWhenFull);Assert.False(b.HasCurrentAction);
    }
    [Fact]
    public void RegisteredFailureWithoutCarryBuildsExactRetreatAndKeepsStateAndPersistentFlag()
    {
        // reach: unit invocation of the registered recipient, raw carried-ID=-1 boundary.
        // C3..C5:005DC80E..DC976; B1 constructors547EF0..7F28,548A50..A78.
        // This inspects actual compound/native children, not a successful placement pipeline.
        using var f=new Fixture();Inputs(f);f.Add(7,150);f.Add(8,400);f.Robot.CarryingOwner!.SetCarrying(99);
        f.Runtime.PyramidBaseStrongInput=()=>new[]{new NativePyramidBase(7,8)};var b=Start(f);Assert.NotNull(b.PlacementHelper);
        f.Robot.CarryingOwner!.SetCarrying(uint.MaxValue);b.PlacementFailed();
        Assert.Equal(5,b.State);Assert.True(b.StopWhenFull);
        var sequence=Assert.IsType<CompoundActionSequential>(b.OwnedRunner);Assert.Equal(3,sequence.Children.Count);
        var parallel=Assert.IsType<CompoundActionParallel>(sequence.Children[0]);Assert.Equal(3,parallel.Children.Count);
        var drive=Assert.IsType<DriveStraightAction>(parallel.Children[0]);Assert.Equal(-20,drive.DistanceMm);Assert.Equal(20f,drive.SpeedMmps);Assert.False(drive.ShouldPlayAnimation);
        static float Field(object action,string name)=>(float)action.GetType().GetField(name)!.GetValue(action)!;
        Assert.Equal(0x3EDF66F3u,BitConverter.SingleToUInt32Bits(Field(parallel.Children[1],"Target")));
        Assert.Equal(0x3D0EFA35u,BitConverter.SingleToUInt32Bits(Field(parallel.Children[1],"Tolerance")));
        Assert.Equal(0f,Field(parallel.Children[2],"RequestedHeight"));Assert.Equal(5f,Field(parallel.Children[2],"Tolerance"));
        Assert.Equal(1f,Assert.IsType<NativeTimedWaitRunner>(sequence.Children[1]).Duration);
        Assert.Equal(0x3E860A92u,BitConverter.SingleToUInt32Bits(Field(sequence.Children[2],"Target")));
        Assert.Equal(0x3D0EFA35u,BitConverter.SingleToUInt32Bits(Field(sequence.Children[2],"Tolerance")));
        Assert.True(f.Robot.ActionList.Cancel(sequence.Tag));f.Robot.ActionList.Watcher.Update();
        b.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF*1000.0);Assert.Equal(5,b.State);Assert.True(b.StopWhenFull);
    }
    [Fact]
    public void SharedCornersUsePhysicalTieOrderAndMidpointUsesFullHeight()
    {
        // reach: unit actual native geometry with supplied object poses, G1..G5:00617818..00617C18.
        // Shared hull/sort/catch M13-024 uncertainty is not certified by this yaw-only fixture.
        using var f=new Fixture();var a=f.Add(8,0);var b=f.Add(7,0);
        var geometry=new NativeConfigurationGeometry(f.M.World,f.Robot);using var first=new Pose3d();using var second=new Pose3d();using var midpoint=new Pose3d();
        Assert.True(geometry.InteriorCorners(a,b,first,second));
        Assert.Equal(new Vec3(22,22,44),first.Translation);Assert.Equal(new Vec3(22,-22,44),second.Translation);
        Assert.True(geometry.InteriorMidpoint(a,b,midpoint));Assert.Equal(new Vec3(22,0,44),midpoint.Translation);
        using var root=f.Robot.GetCheckedMainPose().FindRoot();using var expected=new Pose3d(parent:root);Assert.True(midpoint.Node!.HasSameParent(expected.Node!));
    }
    [Fact]
    public void DisconnectedCornerDistanceFailurePreservesMidpointOutput()
    {
        // reach: unit supplied disconnected base pose; G3/G5:0061793A..A08,00617AFE..B06.
        using var f=new Fixture();var a=f.Add(8,0);var b=f.Add(7,0);using var other=new Pose3d("disconnected");
        b.Pose=new Pose3d(Rotation3d.Identity,new Vec3(0,0,22),parent:other);
        using var output=new Pose3d(Rotation3d.Identity,new Vec3(123,456,789));
        Assert.False(new NativeConfigurationGeometry(f.M.World,f.Robot).InteriorMidpoint(a,b,output));
        Assert.Equal(new Vec3(123,456,789),output.Translation);
    }
}
