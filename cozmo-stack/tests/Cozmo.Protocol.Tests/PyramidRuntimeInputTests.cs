using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PyramidRuntimeInputTests
{
    [Fact]
    public void CanonicalWorldFeedsIntentionDependenciesAndConfiguredBody()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var cache=f.Runtime.PickupTargets!;
        Assert.Equal(uint.MaxValue,cache.GetBest(11));
        f.Add(7,100);f.Add(8,300);f.Add(9,500);f.Rig.Tick();
        // 00570988..00570B40: distinct ground cubes, nearest valid at each dependent stage.
        Assert.Equal(7u,cache.GetBest(9));Assert.Equal(8u,cache.GetBest(10));Assert.Equal(9u,cache.GetBest(11));
        var body=Assert.IsType<NativeBuildPyramidBehavior>(f.Runtime.Bound["BuildPyramid"]);
        Assert.True(body.RefreshTargets());uint basis=0,stat=0,top=0;
        Assert.True(body.GetBase(ref basis));Assert.True(body.GetStatic(ref stat));Assert.True(body.GetTop(ref top));
        Assert.Equal((7u,8u,9u),(basis,stat,top));
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(body.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.True(body.EngineRunning);Assert.Equal(0,body.State);
        Assert.Equal(7u,Assert.IsType<NativePickupBlockHelper>(body.PickupHelper).Target);
    }

    [Fact]
    public void PublishedBaseAndFullUseSignedPairAndStrongReferences()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(7,100);var second=f.Add(8,152);f.Add(9,300);
        var config=f.Runtime.Configurations;config.ForceUpdate();config.Update();f.Rig.Tick();
        var saved=f.Runtime.PyramidBaseStrongInput!().ToArray();
        Assert.Contains(saved,b=>b.MinId==7&&b.MaxId==8);
        var cache=f.Runtime.PickupTargets!;Assert.Equal(7u,cache.GetBest(9));Assert.Equal(8u,cache.GetBest(10));Assert.Equal(9u,cache.GetBest(11));
        second.Pose=new Pose3d(Rotation3d.Identity,new Vec3(300,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        config.SetObjectPoseChanged(8,PoseState.Known);config.ForceUpdate();config.Update();
        Assert.DoesNotContain(f.Runtime.PyramidBaseStrongInput!(),b=>ReferenceEquals(b,saved[0]));
        Assert.Contains(saved,b=>b.MinId==7&&b.MaxId==8);
    }

    [Fact]
    public void TwoHighStackSelectsItsTopAndExcludesUnrelatedGroundCube()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(7,100);f.Add(8,100,66);f.Add(9,300);
        var config=f.Runtime.Configurations;config.ForceUpdate();config.Update();f.Rig.Tick();
        Assert.Contains(config.Snapshot.Stacks,s=>s.BlockIds.Count==2&&s.Bottom==7&&s.Top==8);
        var cache=f.Runtime.PickupTargets!;
        Assert.Equal(new uint[]{8},cache.GetValid(9));
        Assert.Equal(8u,cache.GetBest(9));
    }

    [Fact]
    public void TwoHighTopStillRequiresCanPickWhenItIsCarried()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(7,100);f.Add(8,100,66);f.Add(9,300);
        var config=f.Runtime.Configurations;config.ForceUpdate();config.Update();
        Assert.Contains(config.Snapshot.Stacks,s=>s.BlockIds.Count==2&&s.Top==8);
        f.M.Docking.Carrying.SetCarrying(8);f.Rig.Tick();
        Assert.DoesNotContain(8u,f.Runtime.PickupTargets!.GetValid(9));
    }

    [Fact]
    public void CarriedTopRemainsEligibleWithPublishedBase()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(7,100);f.Add(8,152);f.Add(9,300);
        var config=f.Runtime.Configurations;config.ForceUpdate();config.Update();
        Assert.Contains(config.Snapshot.Bases,b=>b.MinId==7&&b.MaxId==8);
        f.M.Docking.Carrying.SetCarrying(9);f.Rig.Tick();var cache=f.Runtime.PickupTargets!;
        Assert.Equal(7u,cache.GetBest(9));Assert.Equal(8u,cache.GetBest(10));Assert.Contains(9u,cache.GetValid(11));
    }

    [Fact]
    public void StaticIntentionUsesThirtyToleranceThroughActualCache()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(7,100);var candidate=f.Add(8,300,51.99f);f.Rig.Tick();
        var cache=f.Runtime.PickupTargets!;Assert.Equal(7u,cache.GetBest(9));
        Assert.Contains(8u,cache.GetValid(10));
        candidate.Pose=new Pose3d(Rotation3d.Identity,new Vec3(300,0,52),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        f.Rig.Tick();Assert.DoesNotContain(8u,cache.GetValid(10));
    }

    [Fact]
    public void CarriedCubeIsBaseWhenNoConfigurationsExist()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(7,100);f.Add(8,300);f.Add(9,500);
        f.M.Docking.Carrying.SetCarrying(9);f.Rig.Tick();
        var cache=f.Runtime.PickupTargets!;
        Assert.Equal(9u,cache.GetBest(9));Assert.Equal(7u,cache.GetBest(10));Assert.Equal(8u,cache.GetBest(11));
    }

    [Fact]
    public void FullPublicationTakesPrecedenceAndCachedWinnerRemainsUntilRebuild()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(7,100);f.Add(8,152);f.Add(9,126,66);f.Add(10,500);f.Add(11,552);
        var config=f.Runtime.Configurations;config.ForceUpdate();config.Update();f.Rig.Tick();
        Assert.Contains(f.Runtime.PyramidFullStrongInput!(),p=>p.Base.MinId==7&&p.Base.MaxId==8);
        Assert.Contains(f.Runtime.PyramidBaseStrongInput!(),b=>b.MinId==10&&b.MaxId==11);
        var cache=f.Runtime.PickupTargets!;
        Assert.Equal(new uint[]{7},cache.GetValid(9));Assert.Equal(7u,cache.GetBest(9));Assert.Equal(8u,cache.GetBest(10));
        var first=cache.Inspect(9);Assert.Equal(7u,cache.GetBest(9));
        Assert.Equal(first.Rebuilds,cache.Inspect(9).Rebuilds);
        var dep10=cache.Inspect(10);f.Rig.Tick();cache.GetBest(11);
        Assert.True(cache.Inspect(9).Rebuilds>first.Rebuilds);
        Assert.True(cache.Inspect(10).Rebuilds>dep10.Rebuilds);
    }

    [Fact]
    public void ValidBestIsRetainedWhenCloserCubeAppears()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(8,300);f.Rig.Tick();var cache=f.Runtime.PickupTargets!;
        Assert.Equal(8u,cache.GetBest(9));f.Add(7,100);f.Rig.Tick();
        Assert.Contains(7u,cache.GetValid(9));Assert.Equal(8u,cache.GetBest(9));
    }

    [Fact]
    public void RetiredAndReplacedConfigurationOwnersAreUnavailable()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var original=f.Runtime.Configurations;
        using(var replacement=new NativeConfigurationManager(f.M.World,f.Robot))
        {
            f.M.World.NativeConfigurations=replacement;
            Assert.Throws<NativeConfigurationUnavailable>(()=>f.Runtime.PyramidBaseStrongInput!());
            Assert.Throws<NativeConfigurationUnavailable>(()=>f.Runtime.PickupTargets!.GetBest(9));
        }
        f.M.World.NativeConfigurations=original;original.Dispose();
        Assert.Throws<NativeConfigurationUnavailable>(()=>f.Runtime.PyramidFullStrongInput!());
    }

    [Fact]
    public void UnavailableOrReplacedOwnerCannotReadRetainedPublication()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(7,100);f.Add(8,152);var config=f.Runtime.Configurations;
        config.ForceUpdate();config.Update();Assert.NotEmpty(f.Runtime.PyramidBaseStrongInput!());
        var carry=f.Robot.CarryingOwner;
        try
        {
            f.Robot.CarryingOwner=null;config.ForceUpdate();config.Update();Assert.False(config.InputsAvailable);
            Assert.Throws<NativeConfigurationUnavailable>(()=>f.Runtime.PyramidBaseStrongInput!());
            Assert.Throws<NativeConfigurationUnavailable>(()=>f.Runtime.PickupTargets!.GetBest(9));
            f.Robot.CarryingOwner=carry;config.Update();Assert.True(config.InputsAvailable);
            f.M.World.NativeConfigurations=null;
            Assert.Throws<NativeConfigurationUnavailable>(()=>f.Runtime.PyramidFullStrongInput!());
        }
        finally{f.Robot.CarryingOwner=carry;f.M.World.NativeConfigurations=config;}
    }

    [Fact]
    public void StaticHeightUsesStrictThirtyMillimeterTolerance()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var obj=f.Add(7,100,51.99f);
        Assert.True(NativeConfigurationGeometry.RestingAtHeight(obj,0,30));
        obj.Pose=new Pose3d(Rotation3d.Identity,new Vec3(100,0,52),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        Assert.False(NativeConfigurationGeometry.RestingAtHeight(obj,0,30));
    }
}
