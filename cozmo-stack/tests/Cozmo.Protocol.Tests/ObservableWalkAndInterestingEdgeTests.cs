using System.Reflection;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class ObservableWalkAndInterestingEdgeTests
{
    private static ObservableObject Cube(uint id)=>new(id,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>());
    private static SortedDictionary<(uint,int,int,int),ObservableObject> Located(BlockWorld w)
        =>(SortedDictionary<(uint,int,int,int),ObservableObject>)typeof(BlockWorld).GetField("_located",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(w)!;
    private static SortedDictionary<(int,int,int),ObservableObject> Connected(BlockWorld w)
        =>(SortedDictionary<(int,int,int),ObservableObject>)typeof(BlockWorld).GetField("_connectedObjects",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(w)!;
    [Fact]
    public void LocatedWalkerReadsCurrentOriginOncePerBucketIncludingCallbackChanges()
    {
        var w=new BlockWorld {CurrentOriginId=1};var map=Located(w);var a=Cube(1);var b=Cube(2);var c=Cube(3);
        map.Add((1,2,1,1),a);map.Add((1,2,1,2),b);map.Add((2,2,1,3),c);
        var seen=new List<uint>();
        w.FindLocatedObjectHelper(new(),o=>{seen.Add(o.ObjectId);w.CurrentOriginId=2;},false);
        Assert.Equal(new uint[]{1,2,3},seen);
    }
    [Fact]
    public void ConnectedWalkerCopiesBothSetsAndPredicateListBeforeCallbacks()
    {
        var w=new BlockWorld();Connected(w).Add((2,1,1),Cube(1));Connected(w).Add((2,1,2),Cube(2));
        var filter=new BlockWorldFilter();filter.Predicates.Add(_=>true);var seen=new List<uint>();
        var last=w.FindConnectedObjectHelper(filter,o=>{seen.Add(o.ObjectId);filter.IgnoreIds.Add(2);filter.Predicates.Add(_=>false);},false);
        Assert.Equal(new uint[]{1,2},seen);Assert.Equal(2u,last!.ObjectId);
        Assert.Contains(2u,filter.IgnoreIds);Assert.Equal(3,filter.Predicates.Count);
    }
    [Fact]
    public void NullLocatedSlotWarnsAndContinuesWithoutReplacingThePreviousResult()
    {
        var w=new BlockWorld {CurrentOriginId=7};w.OriginNode=_=>new("custom origin");
        var logs=new List<(EngineLogLevel,string?,string,string)>();w.SourceLog=(l,c,k,t)=>logs.Add((l,c,k,t));
        var a=Cube(1);Located(w).Add((7,2,1,1),a);Located(w).Add((7,2,1,2),null!);
        int calls=0;Assert.Same(a,w.FindLocatedObjectHelper(new(),_=>calls++,false));Assert.Equal(1,calls);
        Assert.Equal((EngineLogLevel.Warning,(string?)null,"BlockWorld.FindObjectHelper.NullExistingObject","Origin:custom origin Family:LightCube Type:Block_LIGHTCUBE1 ID:2 is NULL"),Assert.Single(logs));
    }
    [Fact]
    public void NullConnectedSlotUsesSignedMapIdAndNeverCallsPredicates()
    {
        var w=new BlockWorld();Connected(w).Add((2,1,-1),null!);var a=Cube(1);Connected(w).Add((2,1,1),a);
        var logs=new List<string>();w.SourceLog=(l,c,k,t)=>{Assert.Equal(EngineLogLevel.Warning,l);Assert.Null(c);logs.Add(k+"|"+t);};
        var f=new BlockWorldFilter();int calls=0;f.Predicates.Add(_=>{calls++;return true;});
        Assert.Same(a,w.FindConnectedObjectHelper(f,null,false));Assert.Equal(1,calls);
        Assert.Equal("BlockWorld.FindConnectedObjectHelper.NullObject|Family:LightCube Type:Block_LIGHTCUBE1 ID:-1 is NULL",Assert.Single(logs));
        logs.Clear();f.IgnoreFamilies.Add(ObjectFamily.LightCube);Assert.Null(w.FindConnectedObjectHelper(f,null,false));Assert.Empty(logs);
    }
    [Theory]
    [InlineData(ObjectType.Block_LIGHTCUBE1,1)] [InlineData(ObjectType.Block_LIGHTCUBE2,1)]
    [InlineData(ObjectType.Block_LIGHTCUBE3,1)] [InlineData(ObjectType.Charger_Basic,1)] [InlineData(ObjectType.ProxObstacle,0)]
    public void ObservableSecondarySlot5CMatchesActiveCubeChargerAndMarkerless(ObjectType type,byte expected)
    {var o=new ObservableObject(1,type,Array.Empty<KnownMarker>());Assert.Equal(expected,o.ObservableMapSlot5C);Assert.Equal(expected==1,o.IsUnique);}
    [Fact]
    public void CubeAndChargerQuadSlotsUseTheirDifferentCanonicalProvidersAndFull3DRotation()
    {
        var cube=Cube(1);using var flat=new Pose3d(Rotation3d.Identity,new Vec3(100,200,0));
        Assert.True(cube.TryObservableBoundingQuad50(flat,0,out var cq));
        Assert.Equal(new[]{78f,78f,122f,122f},Enumerable.Range(0,4).Select(i=>cq![i].X).OrderBy(x=>x));
        var charger=new ObservableObject(2,ObjectType.Charger_Basic,Array.Empty<KnownMarker>());
        using var tilted=new Pose3d(new Rotation3d(Math.Sqrt(0.5),Math.Sqrt(0.5),0,0),new Vec3(10,20,0));
        Assert.True(charger.TryObservableBoundingQuad50(tilted,0,out var q));
        Assert.InRange(Enumerable.Range(0,4).Min(i=>q![i].X),9.9999f,10.0001f);
        Assert.InRange(Enumerable.Range(0,4).Max(i=>q![i].X),105.9999f,106.0001f);
        Assert.InRange(Enumerable.Range(0,4).Min(i=>q![i].Y),-11.0001f,-10.9999f);
        Assert.InRange(Enumerable.Range(0,4).Max(i=>q![i].Y),19.9999f,20.0001f); // 31-high physical box, not a yaw-only 80-wide rectangle
    }
    [Fact]
    public void ValidToInvalidRemovalReceivesOldPoseRootNotCurrentOriginOrNewPose()
    {
        var m=new SourceMapComponent {CurrentOrigin=99};var obj=Cube(1);obj.OriginId=99;
        using var oldRoot=new Pose3d {Id=77};using var oldPose=new Pose3d(Rotation3d.Identity,new Vec3(1,2,3),parent:oldRoot);
        m.ReadOldPoseRootId=p=>{Assert.Same(oldPose,p);return 77;}; // double for the separately unbuilt M11-066 getter
        uint got=0;m.RemoveObservableObject67F2C4=(o,id)=>{Assert.Same(obj,o);got=id;};
        m.UpdateObjectPose(obj,oldPose,PoseState.Known);Assert.Equal(77u,got);
    }
    [Fact]
    public void InvalidToValidCallsAddWithTheNativeObjectPose()
    {
        var m=new SourceMapComponent();var obj=Cube(1);obj.PoseState=PoseState.Known;int calls=0;
        m.AddObservableObject67ECFC=(o,p)=>{Assert.Same(obj,o);Assert.Same(obj.NativePose,p);calls++;};
        m.UpdateObjectPose(obj,null,PoseState.Unknown);Assert.Equal(1,calls);
    }
    [Fact]
    public void HeightCheckUsesRobotRelativePoseAndPassesOriginalNewPoseToContinuation()
    {
        var m=new SourceMapComponent();var obj=Cube(1);using var world=new Pose3d(Rotation3d.Identity,new Vec3(0,0,1000));
        var seen=new List<bool>();float relativeZ=59;
        m.ObjectPoseRelativeToRobot=(_,_)=>new Pose3d(Rotation3d.Identity,new Vec3(0,0,relativeZ));
        m.AddObjectAfterHeight=(o,p,high)=>{Assert.Same(world,p);seen.Add(high);};
        m.AddObjectCheckedHeight(obj,world);relativeZ=60;m.AddObjectCheckedHeight(obj,world);Assert.Equal(new[]{false,true},seen);
    }

    private sealed class Map : IInterestingEdgeMap
    {
        public float Scalar;
        public List<(Point2f A,Point2f B,IReadOnlyList<bool> Mask)> Rays=new();
        public Queue<uint> Answers=new();
        public float Scalar24()=>Scalar;
        public uint Ray30(Point2f a,Point2f b,IReadOnlyList<bool> mask){Rays.Add((a,b,mask));return Answers.Count==0?0:Answers.Dequeue();}
    }
    private static InterestingEdgeRegion Region(float area,float x,float y=0)=>new(area,new[]{new InterestingEdgeSegment(new(x,-100,0),new(x,100,0),new(1,0,0))});
    private static InterestingEdgeInputs Input(IReadOnlyList<InterestingEdgeRegion> regions,Map map,EngineRandom? rng=null)
        =>new(){Regions=regions,Map=map,RobotPose=new Pose3d(Rotation3d.Identity,Vec3.Zero),WorldOrigin=new Pose3d(),GetRng=()=>rng??new EngineRandom(1),EraseDebugSegments=_=>{}};
    [Fact]
    public void PickGoalsUsesStrictAreaLengthAndDistanceToFiniteSegment()
    {
        var selector=new InterestingEdgeGoalSelector();var map=new Map {Scalar=10};
        var region=new InterestingEdgeRegion(1,new[]{new InterestingEdgeSegment(new(3,-100,0),new(3,100,0),new(1,0,0)),new(new(10,0,0),new(110,0,0),new(1,0,0))});
        var output=new List<InterestingEdgeGoal>();Assert.True(selector.PickGoals(Input(new[]{region},map),output));
        var goal=Assert.Single(output);Assert.Equal(0u,goal.Segment);Assert.Equal(9f,goal.Score); // projection onto segment, not midpoint/end point score
        map.Scalar=200;Assert.True(selector.PickGoals(Input(new[]{region},map),output));Assert.Empty(output);
    }
    [Fact]
    public void PickGoalsNearTieKeepsEarlierSegmentAndEmptyInputClearsOutput()
    {
        var a=new InterestingEdgeSegment(new(1,-100,0),new(1,100,0),new(1,0,0));
        var b=new InterestingEdgeSegment(new(1,-200,0),new(1,200,0),new(1,0,0));
        var region=new InterestingEdgeRegion(1,new[]{a,b});var output=new List<InterestingEdgeGoal>();var selector=new InterestingEdgeGoalSelector();
        selector.PickGoals(Input(new[]{region},new()),output);Assert.Equal(0u,Assert.Single(output).Segment);
        selector.PickGoals(Input(Array.Empty<InterestingEdgeRegion>(),new()),output);Assert.Empty(output);
    }
    [Fact]
    public void TwoGoalSortIsAscendingAndUnknownSmallSortStopsVisibly()
    {
        var r=Region(1,1);var list=new List<InterestingEdgeGoal>{new(r,0,9),new(r,0,1)};var sort=new InterestingEdgeGoalSort();
        Assert.True(sort.TrySort(list));Assert.Equal(new[]{1f,9f},list.Select(x=>x.Score));
        list.Add(new(r,0,4));Assert.False(sort.TrySort(list)); // reaches unread native sort3, never stable-sort guess
    }
    [Fact]
    public void VantageRetriesZeroThenNegativeThenPositiveAndUsesBehindEndpointFirst()
    {
        var selector=new InterestingEdgeGoalSelector {Config=InterestingEdgeConfig.Shipped with {DistanceMin=0,DistanceMax=0}};
        var region=Region(1,1);var map=new Map();map.Answers.Enqueue(1);map.Answers.Enqueue(1);map.Answers.Enqueue(0);
        int words=0;var rng=new EngineRandom(1){WordSourceForTest=()=>{words++;return 0;}};
        var output=new List<Pose3d>();Assert.True(selector.GenerateVantagePoints(Input(new[]{region},map,rng),new(region,0,1),new(0,0,0),output));
        Assert.Equal(3,map.Rays.Count);Assert.Equal(6,words);Assert.Single(output);
        Assert.Equal(new Point2f(BitConverter.UInt32BitsToSingle(0x4283CCCD),0),map.Rays[0].A); // 10+55.9 in f32
        Assert.Equal(new Point2f(-BitConverter.UInt32BitsToSingle(0x42006666),0),map.Rays[0].B);
        Assert.True(map.Rays[1].A.Y<0);Assert.True(map.Rays[2].A.Y>0);Assert.All(map.Rays,r=>Assert.Equal(new[]{false,false,false,true,true,false,true,true,true,true,true},r.Mask));
    }
    [Fact]
    public void RunnableUsesNearestFirstReachabilityBeforeVantageAndStopsAfterFirstPose()
    {
        var selector=new InterestingEdgeGoalSelector();var near=Region(1,3);var far=Region(1,9);var map=new Map();
        map.Answers.Enqueue(0);map.Answers.Enqueue(0);int erases=0;
        var input=Input(new[]{far,near},map);input=new(){Regions=input.Regions,Map=map,RobotPose=input.RobotPose,WorldOrigin=input.WorldOrigin,GetRng=input.GetRng,EraseDebugSegments=_=>erases++};
        Assert.True(selector.IsRunnable(input));Assert.Equal(2,map.Rays.Count);Assert.Equal(new Point2f(3,0),map.Rays[0].B);Assert.Single(selector.Vantage168);Assert.Equal(1,erases);
        Assert.Equal(new[]{false,false,false,false,false,false,false,true,true,false,true},map.Rays[0].Mask);
    }
    [Fact]
    public void EmptyRunnableErasesDebugTwiceAndDisposesStoredPoses()
    {
        var selector=new InterestingEdgeGoalSelector();var old=new Pose3d();selector.Vantage168.Add(old);int erases=0;
        var input=new InterestingEdgeInputs {Regions=Array.Empty<InterestingEdgeRegion>(),Map=new Map(),RobotPose=new Pose3d(),WorldOrigin=new Pose3d(),EraseDebugSegments=_=>erases++};
        Assert.False(selector.IsRunnable(input));Assert.Equal(2,erases);Assert.Empty(selector.Vantage168);Assert.Null(old.Node);
    }
    [Fact]
    public void FactoryConstructsTheCheckedConsumerAndConfigButLeavesActivationExplicitlyUnread()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir is not null && !File.Exists(Path.Combine(dir.FullName,"re-analysis","fidelity_manifest.json")))dir=dir.Parent;
        Assert.NotNull(dir);
        string path=Path.Combine(dir!.FullName,"re-analysis","obb","assets","cozmo_resources","config","engine","behaviorSystem","behaviors","freeplay","hiking","Hiking_visitInterestingEdge.json");
        using var json=JsonDocument.Parse(File.ReadAllText(path),new JsonDocumentOptions {CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true});
        var container=new BehaviorContainer(new(),new(),new[]{new KeyValuePair<BehaviorID,JsonElement>(BehaviorID.Hiking_VisitInterestingEdge,json.RootElement)});
        var behavior=Assert.IsType<VisitInterestingEdgeBehavior>(container.Behaviors[BehaviorID.Hiking_VisitInterestingEdge]);
        Assert.Equal(1,behavior.RequiredProcessId);Assert.Equal(new InterestingEdgeConfig(false,105,110,15,0,10,20,1),behavior.Goals.Config);Assert.Null(behavior.NativeInputs);
    }
    [Fact]
    public void InterestingEdgeInsertionUsesBaseDataWithBothFrameStampsAndTypeOnlyEquality()
    {
        var map=new MemoryMap();var seen=new List<SourceMapData>();map.InterestingEdgeInsertObserved=(p,d)=>{Assert.Equal(2,p.Length);seen.Add(d);};
        map.InsertPreparedInterestingEdges(new[]{(new Point2f(-10,0),new Point2f(10,0)),(new Point2f(0,-10),new Point2f(0,10))},17);
        Assert.Equal(2,seen.Count);Assert.All(seen,d=>{Assert.IsType<SourceMapData>(d);Assert.Equal((MemoryMapContentType.InterestingEdge,17u,17u),(d.Type,d.First,d.Last));});
        var clone=seen[0].Clone();Assert.NotSame(seen[0],clone);clone.Last=99;Assert.True(seen[0].Same(clone));
    }
    [Fact]
    public void SortMainPartitionsAndRecursesWithExplicitUnreadLeafDoubles()
    {
        var region=Region(1,1);var input=Enumerable.Range(0,39).Select(i=>new InterestingEdgeGoal(region,(uint)i,(i*17)%39)).ToList();
        var sort=new InterestingEdgeGoalSort();
        sort.Sort3_5E8714=(array,indexes)=>
        {
            uint count=0;
            void SwapAt(int a,int b){var t=array[indexes[a]];array[indexes[a]]=array[indexes[b]];array[indexes[b]]=t;count++;}
            if(array[indexes[1]].Score<array[indexes[0]].Score)SwapAt(0,1);
            if(array[indexes[2]].Score<array[indexes[1]].Score)SwapAt(1,2);
            if(array[indexes[1]].Score<array[indexes[0]].Score)SwapAt(0,1);
            return count;
        };
        sort.Incomplete5E89DC=(array,first,end)=>
        {
            // Explicit fixture recipient, not claimed to implement libc++'s unread bounded helper.
            var sorted=Enumerable.Range(first,end-first).Select(i=>array[i]).OrderBy(g=>g.Score).ToArray();
            for(int i=0;i<sorted.Length;i++)array[first+i]=sorted[i];return true;
        };
        Assert.True(sort.TrySort(input));Assert.Equal(Enumerable.Range(0,39).Select(x=>(float)x),input.Select(g=>g.Score));
    }

}
