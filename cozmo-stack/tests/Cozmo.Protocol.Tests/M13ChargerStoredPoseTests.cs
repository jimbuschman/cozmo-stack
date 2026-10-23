using System.Reflection;
using System.Runtime.CompilerServices;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M13ChargerStoredPoseTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly Pose3d Root=new("root"){Id=22};
        internal readonly Pose3d Pose;
        internal readonly ObservableObject Charger;
        private readonly List<Pose3d> _generated=new();
        internal Fixture()
        {
            Pose=new(Rotation3d.Identity,new Vec3(100,200,0),"charger",Root);
            Charger=new(901,ObjectType.Charger_Basic,ChargerGeometry.Markers);
            Charger.InitPose(Pose,PoseState.Known);
        }
        internal List<CubePreActionPoses.StoredElement> Generate(PreActionType type)
        {var values=CubePreActionPoses.GenerateStored(Charger,type);_generated.AddRange(values.Select(e=>e.Stored));return values;}
        public void Dispose()
        {
            CubePreActionPoses.ClearForSetPose(Charger);
            foreach(var p in _generated)p.Dispose();
            Charger.ChargerMarker?.PoseOnObject.Dispose();Charger.NativePose.Dispose();Pose.Dispose();Root.Dispose();
        }
    }
    private static Dictionary<PreActionType,List<CubePreActionPoses.StoredElement>> Cache(ObservableObject obj)
    {
        var field=typeof(CubePreActionPoses).GetField("Caches",BindingFlags.NonPublic|BindingFlags.Static)!;
        return ((ConditionalWeakTable<ObservableObject,Dictionary<PreActionType,List<CubePreActionPoses.StoredElement>>>)field.GetValue(null)!).GetOrCreateValue(obj);
    }
    // reach: unit (owned charger/root inputs; real constructor, marker identity, shared pose math and GenerateStored).
    // Old code: types0/1 return 0 elements. Types2..5 were already empty; their positive type0 control fails old code.
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void OnlyTypesZeroAndOneProduceOneCorrectlyTypedZeroDistanceElement(int type)
    {
        using var f=new Fixture();var values=f.Generate((PreActionType)type);
        if(type>1){Assert.Empty(values);values=f.Generate(PreActionType.Docking);type=0;}
        var value=Assert.Single(values);
        Assert.Equal((PreActionType)type,value.Type);Assert.Equal(0u,BitConverter.SingleToUInt32Bits(value.DistanceMm));
        Assert.Same(f.Charger.ChargerMarker,value.Marker);Assert.Same(f.Charger.NativePose.Node,value.Stored.Node!.Parent);
        Assert.Equal("PreActionPose",value.Stored.Name);
        Assert.Equal(0u,BitConverter.SingleToUInt32Bits((float)value.BuiltTranslation.X));
        Assert.Equal(0xC37A0000u,BitConverter.SingleToUInt32Bits((float)value.BuiltTranslation.Y));
        Assert.Equal(0xC1780000u,BitConverter.SingleToUInt32Bits((float)value.BuiltTranslation.Z));
        Assert.Equal(0x3FC90FDBu,BitConverter.SingleToUInt32Bits((float)value.BuiltAngle));
        Assert.InRange(value.Stored.Translation.X,-164.001,-163.999);Assert.InRange(value.Stored.Translation.Z,6.499,6.501);
        Assert.InRange(value.HeightToleranceMm,94f,95f); // source formula applied after marker translation, not Q's 250
    }
    // reach: unit (substitutes marker-list contents after construction; actual retained Charger+0xFC identity).
    // Old code: no generated element, and its record clones share the unparented static marker pose.
    [Fact]
    public void GeneratorUsesTheOwnedMarkerPointerRatherThanReselectingTheMarkerList()
    {
        using var f=new Fixture();using var other=new Fixture();
        var marker=Assert.IsType<KnownMarker>(f.Charger.ChargerMarker);
        Assert.NotSame(marker,other.Charger.ChargerMarker);Assert.NotSame(marker.PoseOnObject,ChargerGeometry.Markers[0].PoseOnObject);
        Assert.Same(f.Charger.NativePose.Node,marker.PoseOnObject.Node!.Parent);Assert.Equal(MarkerType.Charger,marker.Code);
        using var decoyPose=new Pose3d("decoy");
        var list=(KnownMarker[])f.Charger.Markers;list[0]=new KnownMarker((MarkerType)77,BlockFace.Back,decoyPose,1);
        Assert.Same(marker,Assert.Single(f.Generate(PreActionType.PlaceRelative)).Marker);
    }
    // reach: unit (substitutes the marker-local transform only; actual pose-ctor re-root and height writer).
    // Old code: 0 elements. A tolerance computed from Q before re-root gives 0x4310566B, not 0x440E19AF.
    [Fact]
    public void HeightToleranceUsesStoredTranslationAfterReRoot()
    {
        using var f=new Fixture();var marker=Assert.IsType<KnownMarker>(f.Charger.ChargerMarker);
        marker.PoseOnObject.Node!.Transform=new(Rotation3d.Identity,new Vec3(0,0,1000));
        var value=Assert.Single(f.Generate(PreActionType.Docking));
        Assert.Equal(new Vec3(0,-250,984.5),value.Stored.Translation);
        // 984.5f * raw 0x3F13CD3A, independently narrowed from the native multiply.
        Assert.Equal(0x440E19AFu,BitConverter.SingleToUInt32Bits(value.HeightToleranceMm));
        Assert.Same(f.Charger.NativePose.Node,value.Stored.Node!.Parent);
    }
    // reach: unit (public pre-action helper with owned poses; downstream world-parent adapter remains MISSING at 0x0050DF00).
    // Old code: 0 outputs; using the old cube scalar would produce75/40 instead of positive zero.
    [Theory] [InlineData(0)] [InlineData(1)]
    public void ExportCarriesTheChargerElementScalarRatherThanTheCubeDistance(int type)
    {
        using var f=new Fixture();var output=CubePreActionPoses.For(f.Charger,(PreActionType)type,f.Pose);
        try
        {
            var value=Assert.Single(output);Assert.Equal((PreActionType)type,value.Type);
            Assert.Equal(0u,BitConverter.SingleToUInt32Bits((float)value.DistanceMm));
            Assert.Same(f.Charger.ChargerMarker,value.Marker);
        }
        finally{foreach(var value in output)value.WorldPose.Dispose();}
    }
    // reach: unit (supplies a stored-vector/cache container; actual native copy and SetPose clear contracts).
    // Old generator: 0 stored entries. A shared-reference snapshot would lose its pose when the cache clears.
    [Fact]
    public void CollectionSnapshotKeepsItsOwnedPoseWhenTheCacheClears()
    {
        using var f=new Fixture();var generated=f.Generate(PreActionType.Docking);
        var original=Assert.Single(generated);Cache(f.Charger)[PreActionType.Docking]=generated;
        var snapshot=CubePreActionPoses.SnapshotForCollection(generated);
        try
        {
            var copy=Assert.Single(snapshot);Assert.NotSame(original.Stored.Node,copy.Stored.Node);
            f.Charger.SetPose(f.Pose,-1,PoseState.Known);Assert.Null(original.Stored.Node);
            Assert.NotNull(copy.Stored.Node);Assert.InRange(copy.Stored.Translation.X,-164.001,-163.999);
            Assert.Equal(0f,copy.DistanceMm);Assert.Equal(PreActionType.Docking,copy.Type);
        }
        finally{foreach(var value in snapshot)value.Stored.Dispose();}
    }
    // reach: unit (owned object/pose inputs; real SetPose assignment, marker graph and generator).
    // Old generator: 0 elements. The old borrowed object-pose setter also lost its wrapper when an input was disposed.
    [Fact]
    public void SetPoseRetainsTheChargerNodeAndItsMarkerWhenTheInputIsReleased()
    {
        using var f=new Fixture();var node=f.Charger.NativePose.Node;
        using(var input=new Pose3d(Rotation3d.Identity,new Vec3(300,400,0),parent:f.Root))
            f.Charger.SetPose(input,-1,PoseState.Known);
        Assert.Same(node,f.Charger.NativePose.Node);Assert.Equal(new Vec3(300,400,0),f.Charger.NativePose.Translation);
        var stored=Assert.Single(f.Generate(PreActionType.PlaceRelative));
        Assert.Same(node,stored.Stored.Node!.Parent);Assert.Same(node,stored.Marker.PoseOnObject.Node!.Parent);
    }
    // reach: production (contact state dispatch -> live known charger; public collection/cache; repeated real SetPose clear).
    // Old code: first cache is empty for types0/1, so Assert.Single fails. No cache replacement or synthetic marker/pose input.
    // This asserts stored vectors; world-result ctor/charger-validity gaps at 0x0050DF00/0x004EA648 are not claimed covered.
    [Fact]
    public void ContactChargerRegeneratesBothStoredVectorsAfterSetPoseClear()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger);
        var charger=Assert.Single(rig.Robot.LocalizationWorld.LocatedObjects.Where(o=>o.Type==ObjectType.Charger_Basic));
        var outputs=new List<PreActionPose>();
        try
        {
            bool Query()=>CubePreActionPoses.GetCurrentPreActionPoses(charger,new[]{PreActionType.PlaceRelative,PreActionType.Docking},
                new[]{MarkerType.Charger},rig.Engine.Robot!.MainPose,0,Array.Empty<PreActionObstacle>(),outputs);
            Assert.True(Query());var cache=Cache(charger);
            var first=Assert.Single(cache[PreActionType.Docking]);var relative=Assert.Single(cache[PreActionType.PlaceRelative]);
            Assert.Equal(PreActionType.Docking,first.Type);Assert.Equal(PreActionType.PlaceRelative,relative.Type);
            Assert.Equal(0f,first.DistanceMm);Assert.Equal(0f,relative.DistanceMm);Assert.False(Query());
            rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger);
            Assert.Empty(cache[PreActionType.Docking]);Assert.Empty(cache[PreActionType.PlaceRelative]);
            Assert.Null(first.Stored.Node);Assert.True(Query());
            var regenerated=Assert.Single(cache[PreActionType.Docking]);Assert.NotSame(first,regenerated);
            Assert.Same(charger.ChargerMarker,regenerated.Marker);Assert.Same(charger.NativePose.Node,regenerated.Stored.Node!.Parent);
        }
        finally
        {
            foreach(var output in outputs)output.WorldPose.Dispose();
            CubePreActionPoses.ClearForSetPose(charger);
        }
    }
}
