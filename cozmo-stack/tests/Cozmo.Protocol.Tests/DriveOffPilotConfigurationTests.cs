using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class DriveOffPilotConfigurationTests
{
    private static ObservableObject Admit(M4ControlTests.Rig rig,uint id,double x,double y,double z)
    {
        var world=rig.Robot.Vision.World;
        var obj=new ObservableObject(ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {ObjectId=id,OriginId=world.CurrentOriginId};
        using var pose=new Pose3d(Mat3.Identity,new Vec3(x,y,z));
        pose.SetParent(rig.Engine.Robot!.GetNativeOrigin(world.CurrentOriginId));
        obj.InitPose(pose,PoseState.Known);world.AddLocatedObject(obj);return obj;
    }
    [Fact]
    public void ActualRenderedMarkerWorldTailPublishesNativeBeforeHostExtensionEvent()
    {
        Assert.NotNull(MarkerLibrary.EmbeddedOrNull); // required real detector fixture, no silent skip.
        using var first=new Pose3d(Mat3.Identity,new Vec3(260,40,22));
        using var second=new Pose3d(Mat3.Identity,new Vec3(260,-12,22));
        using var rig=new Rig();rig.Cube=first;rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2,second));
        bool delivered=false;
        rig.M.Configurations.ConfigurationSeen+=(configuration,_)=>
        {
            if(configuration.Type!=BlockConfigurationType.PyramidBase)return;
            delivered=true;
            Assert.NotEmpty(rig.Vision.World.NativeConfigurations!.Snapshot.Bases);
        };
        var frame=rig.Frame(); // actual renderer/detector/confirmer/world/frame tail.
        Assert.Equal(2,frame.Objects.Count);Assert.True(delivered);
        Assert.NotEmpty(rig.Vision.World.NativeConfigurations!.Snapshot.Bases);
        Assert.True(rig.Vision.World.NativeConfigurations.NotifyCount>0);
    }

    [Fact]
    public void ActualWorldPublisherUsesProcessedIdsInsteadOfLegacyAllPairs()
    {
        // Explicit located-object admission, actual world/native manager publication.
        // Not camera discovery. Source row16 and base processed-ID ordering oracle.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var objects=new[]{Admit(rig,10,0,0,22),Admit(rig,20,52,0,22),Admit(rig,30,26,45,22)};
        try
        {
            var native=rig.Robot.Vision.World.NativeConfigurations!;
            native.ForceUpdate();native.Update();
            Assert.Equal(new[]{"10,20","10,30"},native.Snapshot.Bases.Select(b=>string.Join(",",b.BlockIds)));
            var held=native.Snapshot;int rebuilds=native.RebuildCount;
            using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);
            m.Configurations.ForceUpdate=true;m.Configurations.Update();
            Assert.Same(held,native.Snapshot);Assert.Equal(rebuilds,native.RebuildCount);
            Assert.Contains(m.Configurations.PyramidBases,b=>b.ContainsBlock(20)&&b.ContainsBlock(30));
        }
        finally{rig.Dispose();foreach(var obj in objects)obj.NativePose.Dispose();}
    }
    [Fact]
    public void NativeTruncationPreservesDuplicatesOldFirstIdentityAndReadOnlyIds()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var objects=new[]{Admit(rig,10,0,0,22),Admit(rig,20,0,0,66),Admit(rig,30,0,0,110),Admit(rig,40,0,0,154)};
        try
        {
            var native=rig.Robot.Vision.World.NativeConfigurations!;native.ForceUpdate();native.Update();
            var held=native.Snapshot;Assert.Equal(2,held.Stacks.Count);
            Assert.All(held.Stacks,s=>Assert.Equal(new uint[]{10,20,30},s.BlockIds));
            Assert.Equal(3,native.Tallest()!.BlockIds.Count);
            var ids=Assert.IsAssignableFrom<IList<uint>>(held.Stacks[0].View.BlockIds);
            Assert.Throws<NotSupportedException>(()=>ids[0]=999);
            native.ForceUpdate();native.Update();
            Assert.All(native.Snapshot.Stacks,s=>Assert.Same(held.Stacks[0],s));
            Assert.Equal(new uint[]{10,20,30},held.Stacks[0].BlockIds);
        }
        finally{rig.Dispose();foreach(var obj in objects)obj.NativePose.Dispose();}
    }
    [Theory]
    [InlineData(10u,true)] [InlineData(20u,false)]
    public void CarriedDownNeighborIsAllowedButUpAndSeedAreExcluded(uint carried,bool hasStack)
    {
        // Explicit admitted located cubes and real raw carrying owner input.
        // Row16 source asymmetry: DOWN619358..378 has no carrying comparison.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var objects=new[]{Admit(rig,10,0,0,22),Admit(rig,20,0,0,66)};
        try
        {
            var owner=rig.Engine.Robot!;owner.CarryingOwner!.SetCarrying(carried);
            var native=rig.Robot.Vision.World.NativeConfigurations!;native.ForceUpdate();native.Update();
            if(hasStack)Assert.Equal(new uint[]{10,20},Assert.Single(native.Snapshot.Stacks).BlockIds);
            else Assert.Empty(native.Snapshot.Stacks);
        }
        finally{rig.Dispose();foreach(var obj in objects)obj.NativePose.Dispose();}
    }

    [Fact]
    public void MissingNativeOwnerRetainsPublicationAndRetryWhileHostExtensionWorks()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var objects=new[]{Admit(rig,10,0,0,22),Admit(rig,20,0,0,66)};
        var owner=rig.Engine.Robot!;var carry=owner.CarryingOwner;
        try
        {
            var native=rig.Robot.Vision.World.NativeConfigurations!;native.ForceUpdate();native.Update();
            var prior=native.Snapshot;int rebuilt=native.RebuildCount,notified=native.NotifyCount;
            owner.CarryingOwner=null;native.SetObjectPoseChanged(10,PoseState.Known);native.ForceUpdate();native.Update();
            Assert.False(native.InputsAvailable);Assert.Same(prior,native.Snapshot);
            Assert.Equal(rebuilt,native.RebuildCount);Assert.Equal(notified,native.NotifyCount);
            Assert.True(owner.BlockConfigurationForceUpdate);Assert.Contains(10u,native.Pending);
            using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);
            m.Configurations.ForceUpdate=true;m.Configurations.Update();Assert.NotEmpty(m.Configurations.Stacks);
            Assert.Equal(new uint[]{10,20},m.NativeTallestStack()!.BlockIds);
            Assert.True(m.NativeIsPartOfConfiguration(10,BlockConfigurationType.StackOfCubes));
            Assert.Same(prior,native.Snapshot);Assert.True(owner.BlockConfigurationForceUpdate);
            owner.CarryingOwner=carry;native.Update();Assert.True(native.InputsAvailable);
            Assert.False(owner.BlockConfigurationForceUpdate);Assert.Empty(native.Pending);
        }
        finally{owner.CarryingOwner=carry;rig.Dispose();foreach(var obj in objects)obj.NativePose.Dispose();}
    }
}
