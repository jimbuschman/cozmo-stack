using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
namespace Cozmo.Robot.Vision;

// fidelity: M11-079
// GetBorders' output half, 0x006894E0..0x00689AA6, after RefreshBorderCombination (M11-047) has produced the
// cached waypoints. Output regions are rebuilt on every call; the cache stores waypoints only (0x006894EE..0x00689518).
internal static class SourceBorderRegions
{
    private static readonly float Epsilon=BitConverter.UInt32BitsToSingle(0x3727C5AC);     // 0x0068989C
    private static readonly float SameLine=BitConverter.UInt32BitsToSingle(0x3F7C1BDA);    // 0x006898A8
    private static readonly float Milli=BitConverter.UInt32BitsToSingle(0x3A83126F);       // 0x006898A4
    private static readonly float Unfinished=BitConverter.UInt32BitsToSingle(0xBF800000);  // 0x00689814..0x00689816

    private sealed class Region
    {
        internal float Area;
        internal readonly List<InterestingEdgeSegment> Segments=new();
    }
    private static Point3f Sub(Point3f a,Point3f b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    private static float SquaredLength(Point3f p){float value=p.X*p.X+p.Y*p.Y;value=value+p.Z*p.Z;return value;}
    private static float Dot(Point3f a,Point3f b){float value=a.X*b.X+a.Y*b.Y;value=value+a.Z*b.Z;return value;}

    // Normal tables 0x0068C56C (Y) and 0x0068C57C (X), selector 0x0068C538..0x0068C566. Invalid is the zero vector.
    internal static Point3f Normal(int direction)=>direction switch
    {
        0=>new(1f,0f,0f),
        1=>new(0f,-1f,0f),
        2=>new(-1f,0f,0f),
        3=>new(0f,1f,0f),
        _=>new(0f,0f,0f),
    };

    // CalculateBorderWaypointCenter 0x00689AF0..0x00689C06: unsigned level bytes +0x80, tie takes FROM. Not an average.
    internal static Point3f Center(SourceBorderWaypoint waypoint)
    {
        var normal=Normal(waypoint.Direction);
        if(waypoint.From.Depth>waypoint.To.Depth)
        {
            var to=waypoint.To;float side=to.Side;
            float x=side*normal.X,y=side*normal.Y,z=side*normal.Z;
            x=x*.5f;y=y*.5f;z=z*.5f;
            return new(to.X-x,to.Y-y,to.Z-z);
        }
        else
        {
            var from=waypoint.From;float side=from.Side;
            float x=side*normal.X,y=side*normal.Y,z=side*normal.Z;
            x=x*.5f;y=y*.5f;z=z*.5f;
            return new(x+from.X,y+from.Y,z+from.Z);
        }
    }

    // MakeUnitLength 0x0050E0C0..0x0050E130. BLE at 0x0050E0EC returns +0 with the vector unchanged for q<=0 or unordered.
    internal static float MakeUnitLength(ref Point3f value)
    {
        float q=value.X*value.X+value.Y*value.Y;q=q+value.Z*value.Z;
        if(!(q>0f))return 0f;
        float length=MathF.Sqrt(q),inverse=1f/length;
        value=new(value.X*inverse,value.Y*inverse,value.Z*inverse);
        return length;
    }

    // MakeBorderSegment 0x00689C08..0x00689D76. A negative dot, or dot 0, negates X and Y only (BHI at 0x00689D30).
    internal static InterestingEdgeSegment Segment(Point3f start,Point3f end,SourceNodeContent content,int startDirection,int endDirection)
    {
        var first=Normal(startDirection);
        var tangent=Sub(end,start);
        float length=MakeUnitLength(ref tangent);
        if(length<0f)length=-length;
        Point3f normal;
        if(length<Epsilon)
        {
            var second=Normal(endDirection);
            float x=first.X+second.X,y=first.Y+second.Y,z=first.Z+second.Z;
            normal=new(x*.5f,y*.5f,z*.5f);
        }
        else
        {
            var candidate=new Point3f(-tangent.Y,tangent.X,tangent.Z);
            float dot=Dot(candidate,first);
            normal=dot>0f||float.IsNaN(dot)?candidate:new(-candidate.X,-candidate.Y,candidate.Z);
        }
        return new(start,end,normal){Content=content.Data};
    }

    // 0x006897FA..0x00689826, 0x00689902..0x00689912: reuse the last region only while its area is negative or unordered.
    private static Region Unfinish(List<Region> output)
    {
        if(output.Count!=0 && !(output[^1].Area>=0f))return output[^1];
        var region=new Region{Area=Unfinished};output.Add(region);return region;
    }

    internal static List<InterestingEdgeRegion> Build(IReadOnlyList<SourceBorderWaypoint> waypoints)
    {
        var output=new List<Region>();
        if(waypoints.Count!=0)
        {
            var seen=new HashSet<SourceQuadNode>(ReferenceEqualityComparer.Instance);
            var order=new List<SourceQuadNode>(); // M11-047 approved extension: insertion order, not pointer-hash order
            var first=waypoints[0];
            Point3f runStart=Center(first),previous=runStart;
            var content=first.From.Content;int runDirection=first.Direction;
            for(int i=0;i<waypoints.Count;i++)
            {
                var waypoint=waypoints[i];
                if(seen.Add(waypoint.From))order.Add(waypoint.From); // 0x00689598..0x006896E4, before geometry and split
                var current=Center(waypoint);
                bool split=false;
                var run=Sub(previous,runStart);
                float q=SquaredLength(run);
                if(q<0f)q=-q; // 0x0068974A..0x00689758
                if(!(q<Epsilon)) // 0x00689766 BMI: ordered less skips the angle gate; equality and NaN enter it
                {
                    MakeUnitLength(ref run);
                    var step=Sub(current,previous);
                    MakeUnitLength(ref step);
                    split=!(Dot(run,step)>=SameLine); // 0x006897CC BLT: less or unordered
                }
                if(!split)split=!content.Same(waypoint.From.Content); // 0x006897DC, previous run against current FROM
                if(split)
                {
                    if(i==0)StackDiagnostics.Write("M1-034 M11-079 skip split before the first waypoint at 0x006897F6; run retained");
                    else
                    {
                        int direction=waypoints[i-1].Direction; // 0x006897F6
                        Unfinish(output).Segments.Add(Segment(runStart,previous,content,runDirection,direction));
                        runStart=previous;content=waypoint.From.Content;runDirection=direction; // 0x006898CC..0x006898E2
                    }
                }
                previous=current; // 0x006898E4..0x006898EC
                if(!waypoint.End)continue; // reaching the vector's end alone flushes nothing (0x0068993E, 0x00689A42)
                var ended=Unfinish(output);
                ended.Segments.Add(Segment(runStart,previous,content,runDirection,waypoint.Direction)); // 0x0068997A, even at coincident ends
                float area=0f; // 0x006899D0..0x006899FC
                foreach(var node in order)
                {
                    float scaled=node.Side*Milli;
                    float term=scaled*scaled;
                    area=area+term;
                }
                seen.Clear();order.Clear();ended.Area=area;
                if(i+1<waypoints.Count) // 0x00689A04..0x00689A3A
                {
                    var next=waypoints[i+1];
                    runStart=previous=Center(next);content=next.From.Content;runDirection=next.Direction;
                }
            }
        }
        return output.Select(region=>new InterestingEdgeRegion(region.Area,region.Segments)).ToList();
    }
}
