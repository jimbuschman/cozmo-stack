using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M11MapMergeTests : IDisposable
{
    private readonly bool _error=EngineErrorState.ErrorFlagSet;
    public void Dispose()=>EngineErrorState.ErrorFlagSet=_error;
    private sealed class Fixture : IDisposable
    {
        internal readonly Pose3d Root=new("root"),OldOrigin,NewOrigin;
        internal readonly SourceMapComponent Maps=new(){CurrentOrigin=1};
        internal readonly MemoryMap Source=new();
        internal MemoryMap Destination=>Maps.Maps[2];
        internal readonly List<string> Logs=new(),Missing=new();
        internal Fixture(Rotation3d? rotation=null,Vec3? translation=null,bool destination=true)
        {
            OldOrigin=new(rotation??Rotation3d.Identity,translation??Vec3.Zero,"old",Root);
            NewOrigin=new("new",Root);
            Maps.OriginPose=id=>id==1?OldOrigin:NewOrigin;
            Maps.Maps[1]=Source;if(destination)Maps.Maps[2]=new MemoryMap();
            Maps.SourceLog=(_,_,key,_)=>Logs.Add(key);Maps.Missing=Missing.Add;
            if(destination)Destination.SourceLog=Maps.SourceLog;
            Maps.ImageTimestamp=()=>throw new Exception("merge must not query a new timestamp");
        }
        internal void Merge(Action? update=null)=>Maps.UpdateMapOrigins(1,2,update??(()=>{}));
        public void Dispose(){Maps.Dispose();NewOrigin.Dispose();OldOrigin.Dispose();Root.Dispose();}
    }
    private static void Known(SourceQuadTree tree,SourceMapData data)=>tree.Root.ForceSet(new SourceNodeContent(data),tree.Processor);
    private static void Geometry(SourceQuadTree tree,float x,float y,float side,byte depth)
    {tree.Root.X=x;tree.Root.Y=y;tree.Root.Side=side;tree.Root.Depth=depth;tree.Root.Bounds=new(x,y,side);}
    private static SourceQuadNode At(SourceQuadNode node,float x,float y)
    {
        if(node.Children.Count==0)return node;
        return At(node.Children.First(c=>c!.Bounds.Contains(new P2f(x,y)))!,x,y);
    }
    private static bool Ray(MemoryMap map,float x,float y,MemoryMapContentType type)=>map.HasCollisionRayWithTypes(new(x,y),new(x,y),t=>t==type);

    [Fact]
    public void IdentityMergeKeepsNonUnknownCellsAndClonesConcretePayload()
    {
        // Live Rejigger join MISSING at 0x0051590C; supplied inputs stop before that join.
        // reach: unit (explicit origin nodes and cells; real UpdateMapOrigins/merge/Insert/ray).
        // Old: destination empty, all four interior rays=false. Live Rejigger join remains MISSING at 0x0051590C.
        using var f=new Fixture();f.Source.Tree.Root.Subdivide(f.Source.Tree.Processor);
        SourceMapData[] data={new ObservableMapData(37,new[]{new P2f(7,8),new P2f(9,10)},15),
            new SourceMapData(MemoryMapContentType.ObstacleCharger,16,16),new CliffMapData(new(1,0),17),new SourceMapData(MemoryMapContentType.InterestingEdge,18,18)};
        for(int i=0;i<4;i++)f.Source.Tree.Root.Children[i]!.ForceSet(new SourceNodeContent(data[i]),f.Source.Tree.Processor);
        var original=Assert.IsType<ObservableMapData>(f.Source.Tree.Root.Children[0]!.Content.Data);
        f.Merge();
        Assert.True(Ray(f.Destination,40,40,MemoryMapContentType.ObstacleObservable));
        Assert.True(Ray(f.Destination,40,-40,MemoryMapContentType.ObstacleCharger));
        Assert.True(Ray(f.Destination,-40,40,MemoryMapContentType.Cliff));
        Assert.True(Ray(f.Destination,-40,-40,MemoryMapContentType.InterestingEdge));
        var copied=Assert.IsType<ObservableMapData>(At(f.Destination.Tree.Root,40,40).Content.Data);
        Assert.Equal(37u,copied.ObjectId);Assert.Equal(original.Polygon,copied.Polygon);Assert.NotSame(original.Polygon,copied.Polygon);
        Assert.NotSame(original,copied);Assert.Equal(15u,original.Last); // normal destination stamp refresh must not mutate source
        Assert.Empty(f.Missing);
    }

    [Fact]
    public void TranslatedYawMergeUsesTheCheckedPlanarTransform()
    {
        // Live Rejigger join MISSING at 0x0051590C; supplied inputs stop before that join.
        // reach: unit (owned parented origin poses, supplied source cell); old ray at(30,50)=false.
        using var f=new Fixture(Rotation3d.FromAngleAxis(Radians.Pi*.5f,new(0,0,1)),new Vec3(30,10,0));
        Geometry(f.Source.Tree,40,0,10,0);Known(f.Source.Tree,new CliffMapData(new(1,0),21));
        f.Merge();Assert.True(Ray(f.Destination,30,50,MemoryMapContentType.Cliff));
        Assert.False(Ray(f.Destination,40,0,MemoryMapContentType.Cliff));
        var copied=Assert.IsType<CliffMapData>(At(f.Destination.Tree.Root,30,50).Content.Data);
        Assert.Equal(new Vec2(1,0),copied.Normal);Assert.Equal((21u,21u),(copied.First,copied.Last)); // clone payload, no normal rotation or new clock
    }

    [Fact]
    public void YawTieSelectsTheSecondProjection()
    {
        // Live Rejigger join MISSING at 0x0051590C; supplied inputs stop before that join.
        // reach: unit (supplied unit half-turn around(1,1,0)); PR R14 tie A=B=1.
        // Old: no merged cell. Checked tie yaw=-pi/2, not the first projection's +pi/2.
        using var f=new Fixture(new Rotation3d(0,Math.Sqrt(.5),Math.Sqrt(.5),0));
        Geometry(f.Source.Tree,40,0,10,0);Known(f.Source.Tree,new CliffMapData(new(1,0),21));
        f.Merge();Assert.True(Ray(f.Destination,0,-40,MemoryMapContentType.Cliff));
        Assert.False(Ray(f.Destination,0,40,MemoryMapContentType.Cliff));
    }

    [Fact]
    public void NegativePiYawIsRescaledBeforePlanarReinsertion()
    {
        // Live Rejigger join MISSING at 0x0051590C; supplied inputs stop before that join.
        // reach: unit (raw quaternion gives atan2f(-0,-1)); R2 maps -pi to +pi. Old merged ray=false.
        using var f=new Fixture(new Rotation3d(0,0,0,-1));
        Assert.Equal(unchecked((int)0x40490FDB),BitConverter.SingleToInt32Bits(new Pose2d(f.OldOrigin).Transform.Angle.Value));
        Geometry(f.Source.Tree,40,0,10,0);Known(f.Source.Tree,new CliffMapData(new(1,0),21));
        f.Merge();Assert.True(Ray(f.Destination,-40,0,MemoryMapContentType.Cliff));
    }

    [Theory]
    [InlineData(0,0)] [InlineData(1,1)] [InlineData(1280,1)] [InlineData(1281,2)]
    public void BudgetUsesFullThreeDimensionalCentreDistance(float z,int budget)
    {
        // Live Rejigger join MISSING at 0x0051590C; supplied inputs stop before that join.
        // reach: unit (max-level roots, explicit transform; overlap=2 isolates the one normal insert).
        // Old: zero inserts. Raw half-turn keeps source centre Z=1 exactly; planar pi trig expands corners by1 ULP.
        // Coincident centre budget=0; Z-only distance contributes ceilf(z/1280), despite planar geometry ignoring Z.
        using var f=new Fixture(new Rotation3d(0,0,0,-1),new Vec3(0,0,z));
        Geometry(f.Source.Tree,0,0,2560,8);Geometry(f.Destination.Tree,0,0,2560,8);
        Known(f.Source.Tree,new CliffMapData(new(1,0),21));int inserts=0;
        f.Destination.Tree.OverlapForTest=(_,_)=>{inserts++;return 2;};
        f.Merge();Assert.Equal(1,inserts);
        Assert.Equal(budget,f.Logs.Count(k=>k=="QuadTreeNode.ShiftRoot.CantShiftPMx"));
        Assert.Single(f.Logs.Where(k=>k=="QuadTree.Expand.InsufficientExpansion"));
    }

    [Fact]
    public void BudgetIsComputedOnceBeforeAnyLeafMovesTheDestination()
    {
        // Live Rejigger join MISSING at 0x0051590C; supplied inputs stop before that join.
        // reach: unit (three source leaves, real transform/Expand/Shift; overlap=2 keeps each insertion a leaf).
        // Old: zero shifts/inserts. Checked initial centre delta=(1280,1280,0) -> budget2 for ALL leaves.
        // Q0 uses two shifts, Q1 one, Q2 two. Recomputing before Q2 would allow only one and warn insufficient.
        using var f=new Fixture(Rotation3d.FromAngleAxis(Radians.Pi*.25f,new(0,0,1)),new Vec3(1280,1280,0));
        Geometry(f.Source.Tree,0,0,2560,8);Geometry(f.Destination.Tree,0,0,2560,8);
        f.Source.Tree.Root.Subdivide(f.Source.Tree.Processor);
        for(int i=0;i<3;i++)f.Source.Tree.Root.Children[i]!.ForceSet(new SourceNodeContent(new CliffMapData(new(i+1,0),21)),f.Source.Tree.Processor);
        int inserts=0;f.Destination.Tree.OverlapForTest=(_,_)=>{inserts++;return 2;};
        f.Merge();Assert.Equal(3,inserts);Assert.Equal(5,f.Logs.Count(k=>k=="QuadTree.ShiftRoot"));
        Assert.DoesNotContain("QuadTree.Expand.InsufficientExpansion",f.Logs);
    }

    [Fact]
    public void LaterDfsLeafWinsAtSharedQuantizedBoundary()
    {
        // Live Rejigger join MISSING at 0x0051590C; supplied inputs stop before that join.
        // reach: unit (four source leaves, real normal override/quantization); old boundary is Unknown.
        // Native DFS child order0,1,2,3; all four touch destination cell(5,5); incoming cliff always overrides.
        using var f=new Fixture();f.Source.Tree.Root.Subdivide(f.Source.Tree.Processor);
        for(int i=0;i<4;i++)f.Source.Tree.Root.Children[i]!.ForceSet(new SourceNodeContent(new CliffMapData(new(i+1,0),21)),f.Source.Tree.Processor);
        f.Merge();Assert.Equal(new Vec2(4,0),Assert.IsType<CliffMapData>(At(f.Destination.Tree.Root,5,5).Content.Data).Normal);
    }

    [Fact]
    public void UnknownLeavesAndNonleafContentAreNotReinserted()
    {
        // Live Rejigger join MISSING at 0x0051590C; supplied inputs stop before that join.
        // reach: unit (explicit source nodes); old reports merge MISSING; new issues zero inserts without MISSING.
        using var f=new Fixture();f.Source.Tree.Root.Subdivide(f.Source.Tree.Processor);
        Known(f.Source.Tree,new CliffMapData(new(1,0),1)); // internal content must be ignored by leaf snapshot
        int inserts=0;f.Destination.Tree.OverlapForTest=(_,_)=>{inserts++;return 2;};
        f.Merge();Assert.Equal(0,inserts);Assert.Empty(f.Missing);Assert.False(f.Destination.Tree.Dirty);
    }

    [Fact]
    public void NullDestinationIsCreatedAfterObjectUpdateAndReceivesTheLeafRoot()
    {
        // Live Rejigger join MISSING at 0x0051590C; supplied inputs stop before that join.
        // reach: unit (component lifecycle with supplied origin poses); old created an EMPTY destination.
        using var f=new Fixture(destination:false);Known(f.Source.Tree,new CliffMapData(new(.25,.75),9));
        f.Merge(()=>Assert.False(f.Maps.Maps.ContainsKey(2)));
        Assert.Equal("MapComponent.UpdateObjectOrigins.NullMapFound",Assert.Single(f.Logs));
        Assert.True(EngineErrorState.ErrorFlagSet);Assert.True(Ray(f.Destination,30,30,MemoryMapContentType.Cliff));
        Assert.Equal(new Vec2(.25,.75),Assert.IsType<CliffMapData>(f.Destination.Tree.Root.Content.Data).Normal);
        Assert.Equal(2u,f.Maps.CurrentOrigin);Assert.False(f.Maps.Maps.ContainsKey(1));
    }

    [Fact]
    public void NullSourceSkipsOnlyMergeAndLogsTheStandingPolicy()
    {
        // Live Rejigger join MISSING at 0x0051590C; supplied inputs stop before that join.
        // reach: unit (explicit absent old map); old: only generic merge MISSING, no M1-034 diagnostic.
        using var f=new Fixture();f.Maps.Maps.Remove(1);Known(f.Destination.Tree,new CliffMapData(new(1,0),9));
        var prior=f.Destination.Tree.Root.Content;var messages=new List<string>();void Log(string s)=>messages.Add(s);
        StackDiagnostics.Reported+=Log;
        try{f.Merge();}
        finally{StackDiagnostics.Reported-=Log;}
        Assert.Contains(messages,s=>s.Contains("M1-034 M13-032")&&s.Contains("0x00688254"));
        Assert.Same(prior,f.Destination.Tree.Root.Content);Assert.Equal(2u,f.Maps.CurrentOrigin);Assert.False(f.Maps.Maps.ContainsKey(1));
    }

    [Fact]
    public void OldOwnerIsErasedOnlyAfterAllInsertions()
    {
        // Live Rejigger join MISSING at 0x0051590C; supplied inputs stop before that join.
        // reach: unit (component lifecycle; overlap observation delegates to actual native overlap math).
        // Old: zero insert callbacks. Source/current must still be old during insertion, then current=new/erase old.
        using var f=new Fixture();Known(f.Source.Tree,new CliffMapData(new(1,0),9));bool objects=false;int inserts=0;
        f.Destination.Tree.OverlapForTest=(node,p)=>
        {
            inserts++;Assert.True(objects);Assert.True(f.Maps.Maps.ContainsKey(1));Assert.Equal(1u,f.Maps.CurrentOrigin);
            return new SourceFastPolygon(p).GetOverlapType(node);
        };
        f.Merge(()=>objects=true);Assert.True(inserts>0);Assert.Equal(2u,f.Maps.CurrentOrigin);Assert.False(f.Maps.Maps.ContainsKey(1));
    }

    [Fact]
    public void FailedRelativePoseStillUsesTheFreshOutputAsTheNativeCallerDoes()
    {
        // Live Rejigger join MISSING at 0x0051590C; supplied inputs stop before that join.
        // reach: unit (supplied unrelated roots); R35 returns false without changing fresh output; caller ignores it.
        // Old: no destination cell; new identity reinsertion despite GetWithRespectTo=false, not an invented guard.
        using var f=new Fixture();using var oldRoot=new Pose3d("unrelated old");using var nextRoot=new Pose3d("unrelated next");
        f.Maps.OriginPose=id=>id==1?oldRoot:nextRoot;
        Known(f.Source.Tree,new CliffMapData(new(1,0),7));
        f.Merge();Assert.True(Ray(f.Destination,30,30,MemoryMapContentType.Cliff));
    }
}
