using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M13OverheadMapWriterTests
{
    internal sealed class Fixture : IDisposable
    {
        internal readonly Pose3d Root=new("origin"){Id=1};
        internal readonly Pose3d Historical,Current;
        internal readonly MemoryMap Map=new();
        internal readonly List<(Vec2[] Polygon,SourceMapData Data)> Writes=new();
        internal readonly List<WhiteboardClearQuad> Clears=new();
        internal readonly List<(MemoryMapContentType From,uint Mask,SourceMapData Data)> Borders=new();
        internal readonly List<(float Time,float Distance)> States=new();
        internal readonly List<string> Order=new();
        internal uint ImageTime=777,Result;
        internal Fixture()
        {
            Historical=new("historical",Root);Current=new("current",Root);
            Map.OverheadContext=new OverheadMapContext
            {
                History=ts=>new(Result,Result==0?new Pose3d(Historical):null,10,1000),
                Origin=()=>Root,ImageTimestamp=()=>{Order.Add("clock");return ImageTime;},
                Clear=q=>{Order.Add("clear-notify");Clears.Add(q);},
                WhiteboardState=(t,d)=>{Order.Add("state");States.Add((t,d));}
            };
            Map.ObjectInsertForTest=(p,d)=>{Order.Add("clear-insert");Writes.Add((p,d));};
            Map.InterestingEdgeInsertObserved=(p,d)=>{Order.Add("edge-insert");Writes.Add((p,d));};
            Map.ObjectBorderForTest=(t,m,d)=>{Order.Add("border");Borders.Add((t,m,d));};
        }
        internal static OverheadEdgeFrame Frame(bool border,params Vec2[] points)
        {
            var chain=new OverheadEdgePointChain{IsBorder=border};
            foreach(var p in points)chain.Points.Add(new(p,border?60:0));
            return new(4242,true,GroundPlaneROI.GroundQuad(),new[]{chain});
        }
        internal double? Run(bool border,params Vec2[] points)=>Map.AddVisionOverheadEdges(Frame(border,points),Current);
        public void Dispose(){Map.OverheadContext=null;Map.ReleaseRobotPose();Current.Dispose();Historical.Dispose();Root.Dispose();}
    }
    // reach: production (public capture entry with real stored robot history, Freeplay frame subscription and whiteboard owner).
    // Old writer: no AIWhiteboard +0x4C/+0x50 update, regardless of legacy regions.
    [Fact]
    public void CapturedFrameReachesExistingHistoryAndWhiteboardOwners()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State(head:(float)HeadGeometry.MinHeadAngleRad);
        var vision=rig.Robot.Vision;vision.Calibration=CameraCalibration.Nominal();vision.Enabled=true;
        vision.ModeEnableMask=1<<(int)VisionMode.DetectingOverheadEdges;
        using var manipulation=new ManipulationSystem(rig.Robot,vision);
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !Directory.Exists(Path.Combine(directory.FullName,"re-analysis","obb","assets")))directory=directory.Parent;
        Assert.NotNull(directory);string obb=Path.Combine(directory!.FullName,"re-analysis","obb");
        var context=new Cozmo.Robot.Behavior.BehaviorContext{Robot=rig.Robot,Triggers=new AnimationTriggerMap()};
        using var stack=Cozmo.Robot.Behavior.FreeplayStack.Create(obb,rig.Robot,context,()=>rig.Engine.Timer.Seconds,vision,manipulation,withReactions:false);
        var frame=new GrayImage(vision.Calibration.Columns,vision.Calibration.Rows);frame.Fill(150);
        uint timestamp=rig.Engine.Robot!.PoseHistory.NewestTimeStamp;
        Assert.NotNull(vision.ProcessCapture(frame,1,timestamp));
        Assert.True(manipulation.Whiteboard.OverheadEdgeState.HasValue);
        Assert.Equal((float)timestamp,manipulation.Whiteboard.OverheadEdgeState!.Value.Timestamp);
    }
    // reach: unit (supplies history/origin/image clock and whiteboard callbacks; actual native inserts/ray).
    // Old code: 0 observed native clear writes and no notification; legacy origin-to-midpoint/triangle writes instead.
    [Theory] [InlineData(0,2)] [InlineData(1,3)] [InlineData(2,4)]
    public void ThreeShapesUseStrictBinaryThresholdAndPreserveNotificationQuad(int shape,int count)
    {
        using var f=new Fixture();
        float farY=shape==0?1f/256:1f/128;
        float nearY=shape==2?1f/128:1f/256;
        var q=new WhiteboardClearQuad(new(60,0),new(40,0),new(45,farY),new(25,nearY));
        f.Map.InsertOverheadClear(q,17);
        Assert.Equal(count,Assert.Single(f.Writes).Polygon.Length);
        Assert.All(f.Writes,w=>{Assert.IsType<SourceMapData>(w.Data);Assert.Equal((MemoryMapContentType.ClearOfObstacle,17u,17u),(w.Data.Type,w.Data.First,w.Data.Last));});
        f.Writes.Clear();f.Run(false,new(60,-20),new(60,20));
        Assert.Single(f.Writes);var notified=Assert.Single(f.Clears);
        Assert.InRange(notified.P1.X,39.99999,40.00001); // clamp at near edge, not robot origin
        Assert.InRange(notified.P3.X,39.99999,40.00001);
        Assert.Empty(f.Map.Regions);
    }
    // reach: unit (owned history/current poses and callback inputs; native type9 tree geometry).
    // Old code: adds both a legacy clear triangle and an edge; no native type9 ray hit.
    [Fact]
    public void TypeNineSurvivesClampFailureAndUsesFreshBorderClockAfterFrameStampedInsert()
    {
        using var f=new Fixture();f.Run(true,new(20,-10),new(20,10));
        var write=Assert.Single(f.Writes);Assert.Equal(MemoryMapContentType.InterestingEdge,write.Data.Type);
        Assert.Equal((4242u,4242u),(write.Data.First,write.Data.Last));Assert.Empty(f.Clears);
        var border=Assert.Single(f.Borders);Assert.Equal(MemoryMapContentType.InterestingEdge,border.From);
        Assert.Equal((1u<<3)|(1u<<4)|(1u<<5)|(1u<<6)|(1u<<7)|(1u<<10),border.Mask);
        Assert.Equal((MemoryMapContentType.NotInterestingEdge,777u,777u),(border.Data.Type,border.Data.First,border.Data.Last));
        Assert.Equal(new[]{"edge-insert","clock","border","state"},f.Order);
        Assert.True(f.Map.HasCollisionRayWithTypes(new(20,-10),new(20,10),t=>t==MemoryMapContentType.InterestingEdge));
        Assert.Empty(f.Map.Regions);
    }
    // reach: unit (supplied edge frame/history/callbacks; actual run state machine and native insertion).
    // Old code: two legacy edges including the restarted final tail, zero native edge inserts.
    [Fact]
    public void LastDirectionBreakLosesOnlyTheRestartedTail()
    {
        using var f=new Fixture();f.Run(true,new(60,-20),new(60,0),new(70,0));
        var edge=Assert.Single(f.Writes.Where(w=>w.Data.Type==MemoryMapContentType.InterestingEdge));
        Assert.Equal(new[]{new Vec2(60,-20),new Vec2(60,0)},edge.Polygon);
        Assert.DoesNotContain(f.Writes,w=>w.Polygon.Contains(new Vec2(70,0)));Assert.Empty(f.Map.Regions);
    }
    // reach: unit (controlled history result; no replacement insert or ray).
    // Old code: ignores history result, emits legacy regions and lacks the historical failure log.
    [Theory] [InlineData(1u)] [InlineData(0x06000000u)]
    public void FailedHistoryNeverFallsBackToCurrentPose(uint result)
    {
        using var f=new Fixture();f.Result=result;var logs=new List<(string Key,string Text)>();f.Map.SourceLog=(_,_,k,t)=>logs.Add((k,t));
        f.Run(true,new(60,-20),new(60,20));Assert.Empty(f.Writes);Assert.Empty(f.Map.Regions);Assert.Empty(f.States);
        if(result==1)Assert.Contains(logs,l=>l.Key=="MapComponent.AddVisionOverheadEdges.HistoricalPoseNotFound" && l.Text=="Pose not found for timestamp 4242 (hist: 10 to 1000). Edges ignored for this timestamp.");
        else Assert.Empty(logs);
    }
    // reach: unit (distinct owned current and historical inputs; native full transform, f32 minimum/state stores).
    // Old code: uses CURRENT pose as geometry transform and never writes whiteboard state.
    [Fact]
    public void GeometryUsesHistoryWhileMinimumUsesCurrentPoseAndEmptyChainsStillWriteState()
    {
        using var f=new Fixture();f.Current.Node!.Transform=new(Rotation3d.Identity,new Vec3(10,0,0));
        f.Run(true,new(60,-20),new(60,0),new(60,20));
        var edge=Assert.Single(f.Writes.Where(w=>w.Data.Type==MemoryMapContentType.InterestingEdge));Assert.Equal(60,edge.Polygon[0].X);
        var state=Assert.Single(f.States);Assert.Equal(4242f,state.Time);Assert.Equal(50f,state.Distance);
        var empty=new OverheadEdgeFrame(16777217,true,GroundPlaneROI.GroundQuad(),Array.Empty<OverheadEdgePointChain>());
        f.Map.AddVisionOverheadEdges(empty,f.Current);Assert.Equal(2,f.States.Count);
        Assert.Equal(16777216f,f.States[^1].Time);Assert.True(float.IsNaN(f.States[^1].Distance));
    }
    // reach: unit (native obstacle insertion and supplied historical frame; actual tree ray admission).
    // Old native ray already blocks this obstacle; old writer has 0 final whiteboard state writes (new: 1).
    [Fact]
    public void NativeObstacleStopsClearPreparation()
    {
        using var f=new Fixture();f.Map.InsertObject(new[]{new Vec2(45,-20),new Vec2(45,20),new Vec2(55,20),new Vec2(55,-20)},new SourceMapData(MemoryMapContentType.ObstacleCharger,1,1));
        f.Writes.Clear();f.Order.Clear();f.Run(false,new(70,-10),new(70,10));
        Assert.Empty(f.Writes);Assert.Empty(f.Map.Regions);Assert.Empty(f.Clears);Assert.Single(f.States);
    }
}

