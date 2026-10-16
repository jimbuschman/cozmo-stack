using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Behavior;
namespace Cozmo.Robot.Vision;

// fidelity: M11-089
public sealed partial class MemoryMap
{
    internal Func<OverheadEdgeFrame,Pose3d,IReadOnlyList<(Point2f A,Point2f B)>?>? NativePreparedInterestingEdges;
    internal Action<Vec2[],SourceMapData>? InterestingEdgeInsertObserved; // observation, not a substitute recipient
    internal void InsertPreparedInterestingEdges(IReadOnlyList<(Point2f A,Point2f B)> segments,uint frameTimestamp)
    {
        foreach(var segment in segments)
        {
            var data=new SourceMapData(MemoryMapContentType.InterestingEdge,frameTimestamp,frameTimestamp);
            Vec2[] polygon=[new(segment.A.X,segment.A.Y),new(segment.B.X,segment.B.Y)];
            InterestingEdgeInsertObserved?.Invoke(polygon,data);
            Tree.Insert(polygon,data,1);
        }
        if(segments.Count!=0)SteppedBehavior.ReportMissing("M11-089/M11-046 post-insertion ROI transform5E8120 and map+0x4C at 0x0068030A..0x006803AC are separate recipients; no fabricated fill-border success");
    }
}
