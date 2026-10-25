using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
namespace Cozmo.Robot.Vision;

internal sealed record OverheadHistorySample(uint Result,Pose3d? Pose,uint Oldest=0,uint Newest=0) : IDisposable
{
    public void Dispose()=>Pose?.Dispose();
    internal static OverheadHistorySample Read(RobotStateHistory history,uint timestamp)
    {
        using var value=new HistRobotState();uint time=0;
        uint result=history.ComputeStateAt(timestamp,ref time,value,true);
        if(result!=0)return new(result,null,history.OldestTimeStamp,history.NewestTimeStamp);
        history.InsertComputedStateAt(timestamp,value.Pose);
        SteppedBehavior.ReportMissing("M11-044 ComputeAndInsertStateAt 0x0067F8A2 -> auxiliary key/index ownership after 0x00531BAA remains unbuilt; existing checked timestamp/pose cache is used");
        return new(0,new Pose3d(value.Pose),history.OldestTimeStamp,history.NewestTimeStamp);
    }
}
internal sealed class OverheadMapContext
{
    internal required Func<uint,OverheadHistorySample> History;
    internal required Func<Pose3d?> Origin;
    internal Func<uint?>? ImageTimestamp;
    internal Action<WhiteboardClearQuad>? Clear;
    internal Action<float,float>? WhiteboardState;
}

// fidelity: M11-017, M11-089, M11-074, M8-026
public sealed partial class MemoryMap
{
    private long _overheadWriteCount;
    /// <summary>Host observation: insert/border calls issued by this overhead writer only, not stored cell counts.</summary>
    public long OverheadWriteCount => Interlocked.Read(ref _overheadWriteCount);
    internal OverheadMapContext? OverheadContext;
    private double? AddOverheadFromHistory(OverheadEdgeFrame frame,Pose3d currentPose)
    {
        if(OverheadContext is not {} context)
        {SteppedBehavior.ReportMissing("M11-017 AddVisionOverheadEdges 0x0067F8A2/0x0067F8F0: timestamped history/current-origin inputs are unbound; current pose is not substituted");return null;}
        if(context.Origin() is not {} origin)return null; // native unknown-origin/missing-map admission
        using var sample=context.History(frame.Timestamp);
        if(sample.Result==0x06000000)return null;
        if(sample.Result!=0 || sample.Pose is null)
        {
            SourceLog?.Invoke(EngineLogLevel.Info,"MapComponent","MapComponent.AddVisionOverheadEdges.HistoricalPoseNotFound",
                $"Pose not found for timestamp {frame.Timestamp} (hist: {sample.Oldest} to {sample.Newest}). Edges ignored for this timestamp.");
            return null;
        }
        using var historical=new Pose3d();
        if(!sample.Pose.TryWithRespectTo(origin,historical))
        {
            SourceLog?.Invoke(EngineLogLevel.Info,"MapComponent","MapComponent.AddVisionOverheadEdges.NotInThisWorld",
                $"Received timestamp {unchecked((int)frame.Timestamp)}, but could not translate that timestamp into current origin.");
            return null;
        }
        return PrepareAndInsertOverhead(frame,historical,currentPose,context);
    }
    private double? PrepareAndInsertOverhead(OverheadEdgeFrame frame,Pose3d historical,Pose3d current,OverheadMapContext context)
    {
        Vec3 W(Vec2 p)=>historical.Apply(new Vec3((float)p.X,(float)p.Y,0));
        Vec2 XY(Vec3 p)=>new((float)p.X,(float)p.Y);
        var robot=XY(historical.Translation);
        var left=XY(W(frame.GroundQuad[1]));var right=XY(W(frame.GroundQuad[3]));
        var quads=new List<WhiteboardClearQuad>();var segments=new List<(Point2f,Point2f)>();
        float minimum=BitConverter.UInt32BitsToSingle(0x7FC00000);
        foreach(var chain in frame.Chains)
        {
            Vec3 start=default,end=default,direction=default;bool hasStart=false,hasDirection=false;
            void Flush()
            {
                if(!hasStart || !hasDirection || !(NormSq(Sub(start,end))>(float)OverheadEdgeMinRunLengthSq))return;
                var q=new WhiteboardClearQuad(XY(start),robot,XY(end),robot);
                if(ClampOverheadQuad(q,left,right) is {} clamped)quads.Add(clamped);
                if(chain.IsBorder)segments.Add((new((float)start.X,(float)start.Y),new((float)end.X,(float)end.Y)));
            }
            for(int i=0;i<chain.Points.Count;i++)
            {
                var p=W(chain.Points[i].Ground);var point=XY(p);
                bool blocked;
                if(OverheadIntersection(robot,point,left,right) is {} hit)
                    blocked=HasCollisionRayWithTypes(robot,hit,OverheadEdgeBlocksAll) || HasCollisionRayWithTypes(hit,point,OverheadEdgeBlocksObstacles);
                else blocked=HasCollisionRayWithTypes(robot,point,OverheadEdgeBlocksObstacles);
                if(blocked){Flush();hasStart=false;hasDirection=false;continue;}
                bool broke=false;
                if(!hasStart){start=p;hasStart=true;}
                else if(!hasDirection){end=p;direction=Unit(Sub(p,start));hasDirection=true;}
                else
                {
                    var next=Unit(Sub(p,end));
                    if(!(Dot(next,direction)>=(float)OverheadEdgeRunDirectionCos))broke=true;
                    else{end=p;direction=next;}
                }
                if(chain.IsBorder)
                {
                    float squared=NormSq(Sub(current.Translation,p));
                    float adjusted=minimum+BitConverter.UInt32BitsToSingle(0xB727C5AC);
                    if(float.IsNaN(minimum)||adjusted>squared)minimum=squared;
                }
                if(broke)
                {
                    Flush();start=end;end=p;direction=Unit(Sub(p,start));hasStart=hasDirection=true;
                    continue; // 0x0067FBD0: a last-point direction break loses the restarted tail
                }
                if(i==chain.Points.Count-1)Flush();
            }
        }
        foreach(var quad in quads)
        {
            InsertOverheadClear(quad,frame.Timestamp);
            Interlocked.Increment(ref _overheadWriteCount); // 0x0067FFE0/0x006800C8/0x0068017C
            if(context.Clear is {} clear)clear(quad);
            else SteppedBehavior.ReportMissing("M8-026 overhead ProcessClearQuad 0x006801B0: AIWhiteboard owner is unbound; full clamped quad retained by the checked producer");
        }
        InsertPreparedInterestingEdges(segments,frame.Timestamp);
        Interlocked.Add(ref _overheadWriteCount,segments.Count); // 0x006802E0
        if(segments.Count!=0)
        {
            // 0x00680312..38E transforms a temporary ROI quad, which no recipient reads.
            foreach(var p in frame.GroundQuad)_=W(p);
            if(context.ImageTimestamp?.Invoke() is {} stamp)
            {FillObjectBorder(stamp);Interlocked.Increment(ref _overheadWriteCount);} // 0x006803AA
            else SteppedBehavior.ReportMissing("M11-017 Robot::GetLastImageTimeStamp 0x00516EC0 at 0x00680396: fresh image clock is unbound; border fill not guessed from frame.ts");
        }
        float distance=float.IsNaN(minimum)?BitConverter.UInt32BitsToSingle(0x7FC00000):MathF.Sqrt(minimum);
        float timestamp=frame.Timestamp;
        if(context.WhiteboardState is {} state)state(timestamp,distance);
        else SteppedBehavior.ReportMissing("M11-017 AIWhiteboard state recipient +0x4C/+0x50 at 0x006803E8/0x006803EC is unbound");
        return float.IsNaN(distance)?null:distance;
    }
    private static Vec3 Sub(Vec3 a,Vec3 b)=>new((float)a.X-(float)b.X,(float)a.Y-(float)b.Y,(float)a.Z-(float)b.Z);
    private static float Dot(Vec3 a,Vec3 b)
    {float v=(float)a.X*(float)b.X;v+=(float)a.Y*(float)b.Y;v+=(float)a.Z*(float)b.Z;return v;}
    private static float NormSq(Vec3 p)=>Dot(p,p);
    private static Vec3 Unit(Vec3 p)
    {float sq=NormSq(p);if(!(sq>0))return p;float inv=1f/MathF.Sqrt(sq);return new((float)p.X*inv,(float)p.Y*inv,(float)p.Z*inv);}
    internal static Vec2? OverheadIntersection(Vec2 a,Vec2 b,Vec2 c,Vec2 d)
    {
        var hit=Quadrilateral.SegmentIntersection(new((float)a.X,(float)a.Y),new((float)b.X,(float)b.Y),new((float)c.X,(float)c.Y),new((float)d.X,(float)d.Y));
        return hit is {} p?new Vec2(p.X,p.Y):null;
    }
    internal static WhiteboardClearQuad? ClampOverheadQuad(WhiteboardClearQuad q,Vec2 left,Vec2 right)
    {
        float dx=(float)left.X-(float)right.X,dy=(float)left.Y-(float)right.Y;
        var a=new Vec2((float)left.X+dx,(float)left.Y+dy);
        var b=new Vec2((float)right.X-dx,(float)right.Y-dy);
        var l=OverheadIntersection(a,b,q.P1,q.P0);var r=OverheadIntersection(a,b,q.P3,q.P2);
        return l is {} lh && r is {} rh?new WhiteboardClearQuad(q.P0,lh,q.P2,rh):null;
    }
    internal void InsertOverheadClear(WhiteboardClearQuad q,uint timestamp)
    {
        float Width(Vec2 a,Vec2 b){float x=(float)a.X-(float)b.X,y=(float)a.Y-(float)b.Y;return x*x+y*y;}
        Vec2 Mid(Vec2 a,Vec2 b)=>new(((float)a.X+(float)b.X)*0.5f,((float)a.Y+(float)b.Y)*0.5f);
        float threshold=BitConverter.UInt32BitsToSingle(0x43610001);
        Vec2[] polygon=!(Width(q.P0,q.P2)>threshold)?[Mid(q.P1,q.P3),Mid(q.P0,q.P2)]
            :!(Width(q.P1,q.P3)>threshold)?[Mid(q.P1,q.P3),q.P0,q.P2]:[q.P0,q.P1,q.P3,q.P2];
        InsertObject(polygon,new SourceMapData(MemoryMapContentType.ClearOfObstacle,timestamp,timestamp));
    }
}
