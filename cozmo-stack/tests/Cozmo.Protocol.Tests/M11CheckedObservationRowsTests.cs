using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class M11CheckedObservationRowsTests
{
    private static KnownMarker Marker(MarkerType code=default) => new(code,BlockFace.Front,Pose3d.Identity,20){HeightMm=27};
    private static ObservableObject Object(uint id,ObjectType type=ObjectType.Block_LIGHTCUBE1,params KnownMarker[] markers)=>new(id,type,markers);
    private static Pose3d At(float x,float y=0,float z=0)=>new(Rotation3d.Identity,new Vec3(x,y,z));

    [Fact]
    public void FactoryAdmitsChargerAndRejectsUnsupportedTypesWithExactError()
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            var world=new BlockWorld();var logs=new List<(EngineLogLevel L,string? C,string K,string T)>();
            world.SourceLog=(l,c,k,t)=>logs.Add((l,c,k,t));
            int id=world.HandleActiveObjectConnectionState(4,0xAA,ObjectType.Charger_Basic,true);
            Assert.NotEqual(-1,id);var charger=Assert.Single(world.ConnectedObjects);Assert.True(charger.IsActive);
            Assert.Equal((EngineLogLevel.Info,"Unnamed","Robot.HandleActiveObjectConnectionState.Connected",$"Object {id} (activeID 4, factoryID 0xaa, objectType 'Charger_Basic')"),Assert.Single(logs));
            logs.Clear();Assert.Equal(-1,world.HandleActiveObjectConnectionState(5,0,ObjectType.Charger_Basic,true));Assert.Empty(logs);
            Assert.Equal(-1,world.HandleActiveObjectConnectionState(3,0,ObjectType.Block_LIGHTCUBE_GHOST,true));
            Assert.Equal((EngineLogLevel.Error,(string?)null,"CreateActiveObjectByType.UnsupportedObjectType","Cannot create active object for given 'Block_LIGHTCUBE_GHOST' object type"),Assert.Single(logs));
            Assert.True(EngineErrorState.ErrorFlagSet);Assert.Single(world.ConnectedObjects);
        }
        finally {EngineErrorState.ErrorFlagSet=old;}
    }

    [Fact]
    public void RemovalReturnsTheIDClearsAllOriginsAndRetainsLocatedPoses()
    {
        var world=new BlockWorld();int id=world.AddConnectedActiveObject(1,0xABC,ObjectType.Block_LIGHTCUBE1);
        var one=Object(unchecked((uint)id));one.OriginId=1;one.InitPose(At(100),PoseState.Known);world.AddLocatedObject(one);
        var two=Object(unchecked((uint)id));two.OriginId=2;two.InitPose(At(200),PoseState.Dirty);world.AddLocatedObject(two);
        Assert.Equal(1,one.ActiveId);Assert.Equal(1,two.ActiveId);
        Pose3d p1=one.NativePose,p2=two.NativePose;
        Assert.Equal(id,world.RemoveConnectedActiveObject(1));
        Assert.Empty(world.ConnectedObjects);Assert.Equal(2,world.LocatedObjects.Count);
        Assert.Equal((-1,0u,PoseState.Known),(one.ActiveId,one.FactoryId,one.PoseState));
        Assert.Equal((-1,0u,PoseState.Dirty),(two.ActiveId,two.FactoryId,two.PoseState));
        Assert.Same(p1,one.NativePose);Assert.Same(p2,two.NativePose);Assert.Equal(-1,world.RemoveConnectedActiveObject(1));
    }

    [Fact]
    public void ErasedUnassignedIDSkipsTheRestOfItsTypeButContinuesTheNextType()
    {
        var world=new BlockWorld();
        var map=(SortedDictionary<(int,int,int),ObservableObject>)typeof(BlockWorld).GetField("_connectedObjects",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(world)!;
        var first=Object(uint.MaxValue);first.ActiveId=2;
        var skipped=Object(7);skipped.ActiveId=2;
        var next=Object(8,ObjectType.Block_LIGHTCUBE2);next.ActiveId=2;
        map.Add((2,1,-1),first);map.Add((2,1,7),skipped);map.Add((2,2,8),next);
        Assert.Equal(8,world.RemoveConnectedActiveObject(2));Assert.Same(skipped,Assert.Single(world.ConnectedObjects));
    }

    [Fact]
    public void MarkerMergeUsesUnsignedMaxByPositionWithoutAppendingOrMatchingCodes()
    {
        var destination=Object(1,ObjectType.Charger_Basic,Marker((MarkerType)1),Marker((MarkerType)2),Marker((MarkerType)3));
        var source=Object(2,ObjectType.Charger_Basic,Marker((MarkerType)99),Marker((MarkerType)98));
        destination.LastObservedTimestamp=7;source.LastObservedTimestamp=1000;
        destination.LastImageRect=(1,2,3,4);source.LastImageRect=(5,6,7,8);destination.TimesObserved=9;source.LastObservedMarkers=new[]{(MarkerType)99};
        destination.Markers[0].LastObservedTimestamp=0x80000001;destination.Markers[1].LastObservedTimestamp=4;destination.Markers[2].LastObservedTimestamp=77;
        source.Markers[0].LastObservedTimestamp=5;source.Markers[1].LastObservedTimestamp=0xFFFFFFFE;
        BlockWorld.UpdateMarkerObservationTimes(destination,source);
        Assert.Equal(new uint[]{0x80000001,0xFFFFFFFE,77},destination.Markers.Select(m=>m.LastObservedTimestamp!.Value));
        Assert.Equal(7u,destination.LastObservedTimestamp);Assert.Equal(3,destination.Markers.Count);
        Assert.Equal((1d,2d,3d,4d),destination.LastImageRect);Assert.Equal(9,destination.TimesObserved);Assert.Empty(destination.LastObservedMarkers);
        var clone=destination.Clone();Assert.True(clone.IsActive);Assert.Equal(uint.MaxValue,clone.ObjectId);Assert.Equal(-1,clone.ActiveId);Assert.Equal(0u,clone.FactoryId);
        Assert.All(clone.Markers,m=>Assert.Null(m.LastObservedTimestamp)); // Unknown constructor input, never borrowed history.Assert.NotSame(destination.Markers,clone.Markers);
    }

    [Theory]
    [InlineData(0,160,216)]
    [InlineData(250,80,108)]
    [InlineData(500,0,0)]
    [InlineData(600,0,0)]
    public void PadsUse250mmAndSaturateAfterTheF32Arithmetic(float distance,uint x,uint y)
    {
        var calibration=CameraCalibration.Nominal() with {FocalLengthX=1000,FocalLengthY=1000};
        Assert.Equal((x,y),BlockWorld.ObservationPads(new Vec3(distance,0,0),calibration,Marker()));
        Assert.Equal((0u,0u),BlockWorld.ObservationPads(new Vec3(float.NaN,0,0),calibration,Marker()));
    }

    [Fact]
    public void FieldOfViewPreservesWordPadsAndUsesStrictUpperBounds()
    {
        Assert.True(ObservableObject.IsWithinFieldOfView(10,20,10,20,640,480));
        Assert.False(ObservableObject.IsWithinFieldOfView(630,20,10,20,640,480));
        Assert.False(ObservableObject.IsWithinFieldOfView(10,460,10,20,640,480));
        Assert.False(ObservableObject.IsWithinFieldOfView(1,1,65536,0,640,480));
        Assert.False(ObservableObject.IsWithinFieldOfView(float.NaN,1,0,0,640,480));
    }

    [Fact]
    public void LocalizationKeepsAnEarlierMinimumWhenALaterMarkerFails()
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            var world=new BlockWorld();world.DelocalizeRobotFields();
            var obj=Object(123,ObjectType.Block_LIGHTCUBE1,Marker(),Marker(),Marker());
            obj.Markers[0].LastObservedTimestamp=100;obj.Markers[1].LastObservedTimestamp=99;obj.Markers[2].LastObservedTimestamp=200;
            var seen=new List<int>();var logs=new List<(EngineLogLevel L,string? C,string K,string T)>();world.SourceLog=(l,c,k,t)=>logs.Add((l,c,k,t));
            world.MarkerPoseInCamera=(_,m)=>{int i=Array.IndexOf(obj.Markers.ToArray(),m);seen.Add(i);return i==0?At(3,4):null;};
            Assert.False(world.SetLocalizedTo(obj));Assert.Equal(new[]{0,2},seen);Assert.Equal(25f,world.Robot2C8);Assert.Null(world.LocalizedToObjectId);
            Assert.Equal((EngineLogLevel.Error,(string?)null,"Robot.SetLocalizedTo.MarkerOriginProblem","Could not get pose of marker w.r.t. robot camera"),Assert.Single(logs));Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally {EngineErrorState.ErrorFlagSet=old;}
    }

    [Fact]
    public void LocalizationThresholdAdvancesOnlyOnReplacementAndPersistsAcrossCalls()
    {
        var world=new BlockWorld();world.DelocalizeRobotFields();
        var obj=Object(123,ObjectType.Block_LIGHTCUBE1,Marker((MarkerType)1),Marker((MarkerType)2),Marker((MarkerType)3),Marker((MarkerType)4));
        uint[] timestamps={100,200,150,149};for(int i=0;i<4;i++)obj.Markers[i].LastObservedTimestamp=timestamps[i];
        var seen=new List<int>();world.MarkerPoseInCamera=(_,m)=>{int i=Array.IndexOf(obj.Markers.ToArray(),m);seen.Add(i);return At(i switch {0=>10,1=>20,_=>5});};
        Assert.True(world.SetLocalizedTo(obj));Assert.Equal(new[]{0,1,2},seen);Assert.Equal(25f,world.Robot2C8);
        world.MarkerPoseInCamera=(_,_)=>At(30);Assert.True(world.SetLocalizedTo(obj));Assert.Equal(25f,world.Robot2C8);
        Assert.True(world.SetLocalizedTo(null));Assert.Equal(25f,world.Robot2C8);
    }

    [Fact]
    public void PyramidPruningIsOrderedRejectsDegenerateLeftAndRemovesConsecutiveMatches()
    {
        var normal=new PyramidBase(1,2,Vec3.Zero);var reverse=new PyramidBase(2,1,Vec3.Zero);var equal=new PyramidBase(3,3,Vec3.Zero);
        var bases=new List<BlockConfiguration>{normal,normal,reverse,equal};
        var pyramids=new List<BlockConfiguration>{new Pyramid(normal,4),new Pyramid(equal,5)};
        BlockConfigurationManager.PruneFullPyramids(bases,pyramids);
        Assert.Equal(new BlockConfiguration[]{reverse,equal},bases);Assert.Equal(2,pyramids.Count);
        Assert.True(new StackOfCubes(new uint[]{1,uint.MaxValue,2}).ContainsBlock(uint.MaxValue));
    }
    [Fact]
    public void IntersectingDeletionHasNoImplicitSelfExclusionAndCopiesTheFilter()
    {
        var world=new BlockWorld();var obj=Object(123);obj.OriginId=1;obj.InitPose(At(100),PoseState.Known);world.AddLocatedObject(obj);
        var filter=new BlockWorldFilter();filter.AllowedIds.Add(123);
        var deleted=world.DeleteIntersectingObjects(obj,0,filter);
        Assert.Same(obj,Assert.Single(deleted));Assert.Empty(world.LocatedObjects);Assert.Empty(filter.Predicates);
    }

    [Fact]
    public void UnknownTimestampInputsAreNotReplacedWithPlausibleZeroes()
    {
        var target=Object(1,ObjectType.Charger_Basic,Marker());var source=Object(2,ObjectType.Charger_Basic,Marker());
        target.Markers[0].LastObservedTimestamp=123;
        BlockWorld.UpdateMarkerObservationTimes(target,source);
        Assert.Equal(123u,target.Markers[0].LastObservedTimestamp);Assert.Null(source.Markers[0].LastObservedTimestamp);
    }

    [Fact]
    public void TallestStackKeepsTheFirstEqualHeightAndEmptyReturnsNull()
    {
        var manager=new BlockConfigurationManager(new BlockWorld(),()=>0);
        Assert.Null(manager.GetTallestStack());
        var first=new StackOfCubes(new uint[]{1,2});var second=new StackOfCubes(new uint[]{3,4});
        var cache=new Dictionary<BlockConfigurationType,IReadOnlyList<BlockConfiguration>>
            {[BlockConfigurationType.StackOfCubes]=new BlockConfiguration[]{first,second}};
        var snapshot=typeof(BlockConfigurationManager).GetNestedType("Snapshot",BindingFlags.NonPublic)!;
        var value=Activator.CreateInstance(snapshot,new object[]{cache,new Dictionary<string,double>()})!;
        typeof(BlockConfigurationManager).GetField("_snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(manager,value);
        Assert.Same(first,manager.GetTallestStack());
    }

}
