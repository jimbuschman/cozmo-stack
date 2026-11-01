using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PyramidStrategyBindingTests
{
    [Fact]
    public void ConfiguredStrategyReadsLocatedWorldAndNativeBasePublication()
    {
        // Canonical configured strategy and actual world/cache; unlock and RNG are controlled inputs.
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var strategy=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="BuildPyramid").Strategy;
        Assert.NotNull(strategy.PyramidCandidates);Assert.NotNull(strategy.PyramidBases);Assert.NotNull(strategy.PyramidFull);
        strategy.PyramidUnlocked=()=>true;strategy.Draw=_=>0; // source-boundary unlock and RNG inputs
        var first=f.Add(7,100);first.LastObservedTimestamp=0;first.IsMoving=true;
        f.M.Docking.Carrying.SetCarrying(7);
        f.Add(8,152);
        var charger=new ObservableObject(30,ObjectType.Charger_Basic,Array.Empty<KnownMarker>())
            {OriginId=f.Robot.CurrentOriginId,PoseState=PoseState.Known};
        var other=new ObservableObject(31,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {OriginId=f.Robot.CurrentOriginId+1,PoseState=PoseState.Known};
        f.M.World.AddLocatedObject(charger);f.M.World.AddLocatedObject(other);
        Assert.Equal(2,strategy.PyramidCandidates!());
        Assert.False(strategy.WantsToStart(new FreeplayInputs(),1000,out _));
        f.Add(9,300);
        Assert.Equal(3,strategy.PyramidCandidates!());
        Assert.True(strategy.WantsToStart(new FreeplayInputs(),1000,out _));
        f.M.Docking.Carrying.UnsetCarrying();
        f.Runtime.Configurations.ForceUpdate();f.Runtime.Configurations.Update();
        Assert.True(strategy.PyramidBases!()>0);
        Assert.Equal(f.M.NativePyramidBases.Count,strategy.PyramidBases());
        Assert.Equal(f.M.NativePyramids.Count,strategy.PyramidFull!());
    }

    [Fact]
    public void ConfiguredObjectiveDeliveryAndSuccessfulStartReset()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var strategy=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="BuildPyramid").Strategy;
        Assert.True(f.Robot.HasExternalInterface);
        strategy.PyramidUnlocked=()=>true;strategy.Draw=_=>0; // source-boundary unlock and RNG inputs
        var inputs=new FreeplayInputs();
        // Shipped activities/freeplay/buildPyramid.json: can-end 5 s, should-end 300 s.
        Assert.False(strategy.WantsToEnd(inputs,6,out _));
        f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,(uint)4);
        Assert.False(strategy.WantsToEnd(inputs,6,out _));
        f.Rig.Engine.AnimationMessages.Deliver(0x8B,true,(uint)3);
        Assert.True(strategy.WantsToEnd(inputs,6,out _));
        f.Add(7,100);f.Add(8,300);f.Add(9,500);
        Assert.True(strategy.WantsToStart(inputs,1000,out _));
        Assert.False(strategy.WantsToEnd(inputs,6,out _));
    }

    [Fact]
    public void BasePublicationAndRefreshChangeDecisionWithoutAnotherRoll()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var strategy=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="BuildPyramid").Strategy;
        strategy.PyramidUnlocked=()=>true; // source-boundary unlock input
        int words=0;
        f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return uint.MaxValue;}; // native RNG word boundary: roll near 1
        try
        {
            f.Add(7,100);var second=f.Add(8,300);f.Add(9,500);
            var config=f.Runtime.Configurations;config.ForceUpdate();config.Update();
            Assert.Equal(3,strategy.PyramidCandidates!());Assert.Equal(0,strategy.PyramidBases!());
            Assert.False(strategy.WantsToStart(new FreeplayInputs(),1000,out _));
            int afterRoll=words;Assert.True(afterRoll>=2);
            second.Pose=new Pose3d(Rotation3d.Identity,new Vec3(152,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
            config.SetObjectPoseChanged(8,PoseState.Known);config.ForceUpdate();config.Update();
            Assert.True(strategy.PyramidBases()>0);
            Assert.True(strategy.WantsToStart(new FreeplayInputs(),1000,out _));Assert.Equal(afterRoll,words);
            second.Pose=new Pose3d(Rotation3d.Identity,new Vec3(300,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
            config.SetObjectPoseChanged(8,PoseState.Known);config.ForceUpdate();config.Update();
            Assert.Equal(0,strategy.PyramidBases());
            Assert.False(strategy.WantsToStart(new FreeplayInputs(),1000,out _));Assert.Equal(afterRoll,words);
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }

    [Fact]
    public void FullPyramidProviderReadsActualPublishedFullVector()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var strategy=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="BuildPyramid").Strategy;
        strategy.PyramidUnlocked=()=>true;strategy.Draw=_=>0;
        f.Add(7,100);f.Add(8,152);f.Add(9,126,66);
        f.Runtime.Configurations.ForceUpdate();f.Runtime.Configurations.Update();
        Assert.True(strategy.PyramidFull!()>0);
        Assert.Equal(f.M.NativePyramids.Count,strategy.PyramidFull());
        Assert.False(strategy.WantsToStart(new FreeplayInputs(),1000,out _));
    }

    [Fact]
    public void FailedRebuildAndAbsentOwnerAreUnavailableRatherThanRetainedOrEmpty()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var strategy=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="BuildPyramid").Strategy;
        f.Add(7,100);f.Add(8,152);var config=f.Runtime.Configurations;
        config.ForceUpdate();config.Update();Assert.True(strategy.PyramidBases!()>0);
        var prior=config.Snapshot;var carry=f.Robot.CarryingOwner;
        var messages=new List<string>();void Missing(string value)=>messages.Add(value);
        SteppedBehavior.MissingReported+=Missing;
        try
        {
            f.Robot.CarryingOwner=null;config.ForceUpdate();config.Update();
            Assert.False(config.InputsAvailable);Assert.Same(prior,config.Snapshot);
            Assert.Null(strategy.PyramidBases());Assert.Null(strategy.PyramidFull!());
            f.Robot.CarryingOwner=carry;config.Update();Assert.True(config.InputsAvailable);
            f.M.World.NativeConfigurations=null;
            Assert.Null(strategy.PyramidCandidates!());Assert.Null(strategy.PyramidBases());Assert.Null(strategy.PyramidFull());
            Assert.Contains(messages,m=>m.Contains("M15-097/M12-049"));
        }
        finally {f.Robot.CarryingOwner=carry;f.M.World.NativeConfigurations=config;SteppedBehavior.MissingReported-=Missing;}
    }
}
