using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

// Expected values: OV69 checked by 20261016-ov69-74-75-77-check, M8-026.
[Collection("M5 process statics")]
public sealed class WhiteboardClearQuadTests
{
    private static readonly WhiteboardClearQuad Quad = new(new(-1,-1), new(-1,1), new(1,-1), new(1,1));
    [Theory]
    [InlineData(0,0,true)] [InlineData(-1,-1,true)] [InlineData(1,1,true)]
    [InlineData(-1,0,true)] [InlineData(0,1,true)] [InlineData(2,0,false)]
    public void ContainsUsesBothTrianglesAndIncludesEdges(double x,double y,bool expected)
        => Assert.Equal(expected,Quad.Contains(new(x,y))); // 4DF764; 4E03B8..047E

    [Fact]
    public void StrictBoundsRejectEqualityNaNAndDegenerateTriangle()
    {
        var a=new Vec2(1,0); var b=new Vec2(0,1); var c=new Vec2(0,0);
        float lower=BitConverter.UInt32BitsToSingle(0xB4000000);
        float upper=BitConverter.UInt32BitsToSingle(0x3F800001);
        Assert.False(WhiteboardClearQuad.Triangle(a,b,c,new(lower,0.5)));
        Assert.True(WhiteboardClearQuad.Triangle(a,b,c,new(float.BitIncrement(lower),0.5)));
        Assert.False(WhiteboardClearQuad.Triangle(a,b,c,new(upper,0)));
        Assert.False(Quad.Contains(new(double.NaN,0)));
        Assert.False(WhiteboardClearQuad.Triangle(c,c,c,c)); // 1/0 is not guarded
    }

    private static AIWhiteboard Board(out Pose3d origin)
    {
        var root=new Pose3d("base"); origin=new Pose3d("origin",root);
        var result=new AIWhiteboard(new BlockWorld(()=>Array.Empty<(uint,Cozmo.Protocol.ObjectType)>()),()=>0);
        var input=origin; result.ClearQuadWorldOrigin=()=>input; return result;
    }
    private static void Add(AIWhiteboard b,Pose3d origin,float x,float z=0)
    {
        using var p=new Pose3d(Rotation3d.Identity,new Vec3(x,0,z),"entry",origin);
        b.PossibleObjectStorage.Add(new(p,default));
    }

    [Fact]
    public void RunsUnlinkBeforeDestructionAndNeverRetestTheFirstNonmatch()
    {
        var b=Board(out var origin);
        foreach(float x in new[]{0f,0.5f,4f,0.25f,5f}) Add(b,origin,x);
        var visits=new List<double>(); var destruction=new List<(int,double[])>();
        b.ClearQuadPredicateForTest=p=>visits.Add(p.Translation.X);
        b.PossibleObjectStorage.BeforeDestroyForTest=_=>destruction.Add((b.PossibleObjects.Count,b.PossibleObjects.Select(p=>p.Pose.Translation.X).ToArray()));
        b.ProcessClearQuad(Quad);
        Assert.Equal(new double[]{0,0.5,4,0.25,5},visits); // 56ACDE..AD1C
        Assert.Equal(new double[]{4,5},b.PossibleObjects.Select(p=>p.Pose.Translation.X));
        Assert.Equal(new[]{4,3,2},destruction.Select(x=>x.Item1));
        Assert.Equal(new double[]{4,0.25,5},destruction[0].Item2); // whole first run already unlinked
    }

    [Fact]
    public void CenterPredicateIgnoresZButFailedOriginConversionKeepsItsNode()
    {
        var b=Board(out var origin); Add(b,origin,0,9000);
        using var other=new Pose3d("unrelated"); Add(b,other,0);
        b.ProcessClearQuad(Quad);
        Assert.Single(b.PossibleObjects);
        Assert.Same(other.Node,b.PossibleObjects[0].Pose.Node!.Parent);
    }

    [Fact]
    public void RootCaptureIsAValueCopyRatherThanAConvenientAlias()
    {
        var b=Board(out _); using var root=new Pose3d("root");
        b.ClearQuadWorldOrigin=()=>root; Add(b,root,0);
        b.ProcessClearQuad(Quad);
        Assert.Single(b.PossibleObjects); // M11-067 copies a root into a distinct root; M11-066 fails conversion
    }

    [Fact]
    public void EmptyResultErasesGroupOnceAndUnchangedListNeverRenders()
    {
        var b=Board(out var origin); Add(b,origin,0);
        int origins=0; b.ClearQuadWorldOrigin=()=>{origins++;return origin;};
        var trace=new List<string>(); var segments=new List<WhiteboardSegment>();
        b.ClearQuadRenderForTest=trace.Add; b.SegmentConstructedForTest=segments.Add;
        b.ProcessClearQuad(Quad);
        Assert.Equal(new[]{"erase"},trace); Assert.Empty(segments); Assert.Equal(2,origins);
        b.ProcessClearQuad(Quad);
        Assert.Equal(new[]{"erase"},trace); Assert.Equal(3,origins); // 56AD24 size gate
    }

    [Fact]
    public void RenderUsesPhysicalApplyOrderOrangePerimeterAndYellowHalfAxis()
    {
        var b=Board(out var origin); Add(b,origin,0); Add(b,origin,100);
        var trace=new List<string>(); var segments=new List<WhiteboardSegment>();
        b.ClearQuadRenderForTest=trace.Add; b.SegmentConstructedForTest=segments.Add;
        b.ProcessClearQuad(Quad);
        Assert.Equal(new[]{"erase","apply0","apply2","apply1","apply3"},trace);
        Assert.Equal(5,segments.Count);
        Assert.All(segments,s=>{Assert.Equal("AIWhiteboard.PossibleObjects",s.Group);Assert.False(s.ClearPrevious);});
        Assert.Equal(new uint[]{0xFF7F00FF,0xFF7F00FF,0xFF7F00FF,0xFF7F00FF,0xFFFF00FF},segments.Select(s=>s.Color));
        // 56A7C6..A970: 0->2->3->1->0, +35 lift; 56C90C scale bits3A83126F.
        Assert.Equal(0.15f,segments[0].Start.X); Assert.Equal(0.05f,segments[0].Start.Y);
        Assert.Equal(-0.05f,segments[0].End.Y); Assert.Equal(0.035f,segments[0].Start.Z);
        Assert.Equal(segments[0].End,segments[1].Start); Assert.Equal(segments[1].End,segments[2].Start);
        Assert.Equal(segments[2].End,segments[3].Start); Assert.Equal(segments[3].End,segments[0].Start);
        Assert.Equal(0.1f,segments[4].Start.X); Assert.Equal(0.125f,segments[4].End.X);
    }

    [Fact]
    public void PredicateAcceptsAnyNonzeroResultButRenderRequiresExactlyOne()
    {
        var b=Board(out var origin); Add(b,origin,0); Add(b,origin,100);
        b.RelativePoseForTest=(from,frame,to)=>{to.AssignInputValue(from);return 2;};
        var trace=new List<string>(); var segments=new List<WhiteboardSegment>();
        b.ClearQuadRenderForTest=trace.Add;b.SegmentConstructedForTest=segments.Add;
        b.ProcessClearQuad(Quad);
        Assert.Single(b.PossibleObjects);Assert.Equal(new[]{"erase"},trace);Assert.Empty(segments);
    }

    [Fact]
    public void ProductionWhiteboardAndExistingHandlersShareTheClearStorage()
    {
        using var rig=new Rig();
        var b=rig.M.Whiteboard;
        using var root=new Pose3d("base"); using var origin=new Pose3d("origin",root);
        b.ClearQuadWorldOrigin=()=>origin;
        Add(b,origin,0);
        var handlers=new AIWhiteboardHandlers(b,()=>0);
        Assert.Single(handlers.PossibleObjects);
        b.ProcessClearQuad(Quad);
        Assert.Empty(handlers.PossibleObjects);
    }
}
