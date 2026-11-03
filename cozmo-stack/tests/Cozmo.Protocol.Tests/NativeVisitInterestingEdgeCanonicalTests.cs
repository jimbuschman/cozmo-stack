using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

// reach: production. The robot comes from the production factory with options from HardwareEngineOptions (locale, clock and paths
// the only overrides), a stored needs blob, and ticks through the engine; map content arrives only through the running stack.
// Fixture inputs, named: (1) the motor, path, animation and NV echoes of an offline firmware; (2) the camera image. It is a floor of
// brightness 150 with, when PaintedCircleRadius is set, a disc of that radius round the origin and darker floor (60) beyond it, projected through
// the robot's real camera pose, so the map's type 9 comes out of VisionSystem, the detector (still the M11-017 equivalent) and
// AddVisionOverheadEdges. Type 1 and 2 content comes from the robot pose writer. (3) SuppliedCloseEdgeMm replaces the detector's
// output while gathering with a border chain that far ahead, entered at its production recipient ApplyOverhead 0x006553FC. The
// close branch needs an edge within 60 mm. With the camera at the engine's z (M11-054, POSE-SOLVEPNP-1) the bottom image row at the
// behaviour's head sees floor from about 43.5 mm, so the window is on screen, but a real-frame run is not yet shown to reach the branch:
// a 1/15 s attempt gave state 3 on one run and state 4 (the reading consumed was NaN) on others. Until that is traced,
// CloseEdgeFlagsTheVisitedConeAndObserves substitutes the chain and proves the branch only.
[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeVisitInterestingEdgeCanonicalTests(ITestOutputHelper output)
{
    private sealed record Scene(float? PaintedCircleRadius,float? SuppliedCloseEdgeMm=null,double Seconds=300);
    private sealed record Run(List<(double At,string Id)> Chosen,List<string> Holds,double Idle,double LongestIdle,double LongestHikingIdle,int HikingVisits,
        List<(double At,int State)> States,int Starts,int Completions,IReadOnlyDictionary<MemoryMapContentType,int> Content,double GatherDistanceToVantage,
        bool DroveToVantage,List<float> Distances,List<string> Trace,int Paths);

    // Firmware scene as in BringBeaconFirmwareScene, with straight segments followed over time so the far branch can see the edge approach.
    private sealed class Firmware(Fixture f)
    {
        private readonly List<RobotMessage> _wire=new();
        private readonly HashSet<ushort> _paths=new();
        private ExecutePath? _pending;
        private int _mark=f.Rig.Mark(),_next;
        internal int Paths=>_paths.Count;
        internal float LineSpeed=100f;
        internal void Receive()
        {
            var sent=f.Rig.SentSince(_mark);_mark=f.Rig.Mark();
            foreach(var message in sent)
            {
                if(message is ClearPath){_wire.Clear();_pending=null;_next=0;}
                else if(message is AppendPathSegmentLine or AppendPathSegmentArc or AppendPathSegmentPointTurn)_wire.Add(message);
                else if(message is ExecutePath execute && _paths.Add(execute.EventId))
                {_pending=execute;_next=0;f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=(byte)PathEventType.Started});}
            }
        }
        internal void Step(float seconds)
        {
            if(f.M.GetNativePlanners().Lattice is {} runtime && runtime.CheckPlanningStatus==1)
                Assert.True(SpinWait.SpinUntil(()=>runtime.CheckPlanningStatus!=1,TimeSpan.FromSeconds(10)),"offline planner watchdog");
            Receive();
            if(_pending is not {} path)return;
            if(_next>=_wire.Count)
            {
                if(_next==f.Rig.Robot.Motion.Path.DoleSegmentCount){f.Rig.Data(new PathFollowingEvent{EventId=path.EventId,EventType=(byte)PathEventType.Completed});_pending=null;}
                return;
            }
            float x=f.X-20f*MathF.Cos(f.Yaw),y=f.Y-20f*MathF.Sin(f.Yaw); // drive centre, the frame the path is in
            bool done=true;
            switch(_wire[_next])
            {
                case AppendPathSegmentLine line:
                    float dx=line.XEndMm-x,dy=line.YEndMm-y,left=MathF.Sqrt(dx*dx+dy*dy),move=LineSpeed*seconds;
                    if(left>move){x+=dx/left*move;y+=dy/left*move;done=false;}else{x=line.XEndMm;y=line.YEndMm;}
                    break;
                case AppendPathSegmentArc arc:float a=arc.StartRad+arc.SweepRad;x=arc.XCenterMm+arc.RadiusMm*MathF.Cos(a);y=arc.YCenterMm+arc.RadiusMm*MathF.Sin(a);f.Yaw=a+MathF.Sign(arc.SweepRad)*Radians.Pi*.5f;break;
                case AppendPathSegmentPointTurn turn:x=turn.XMm;y=turn.YMm;f.Yaw=turn.TargetAngleRad;break;
            }
            f.X=x+20f*MathF.Cos(f.Yaw);f.Y=y+20f*MathF.Sin(f.Yaw);
            if(done)_next++;
        }
    }
    private static byte[] Floor(Fixture f,float? edgeRadius)
    {
        var cal=f.M.Vision.Calibration!;var pixels=new byte[cal.Columns*cal.Rows];Array.Fill(pixels,(byte)150);
        if(edgeRadius is not {} radius)return pixels;
        // The camera pose the frame's own VisionPoseData gives the detector.
        if(f.M.Vision.History.Latest is not {} pose)return pixels;
        using var flat=pose.CameraPose;var model=new CameraModel(cal,flat);
        // The tangent of the circle where the robot's heading meets it; near the robot the arc is this straight line.
        float cos=MathF.Cos(f.Yaw),sin=MathF.Sin(f.Yaw),along=f.X*cos+f.Y*sin,across=f.X*-sin+f.Y*cos,reach=radius*radius-across*across;
        if(reach<=0)return pixels;
        bool inside=f.X*f.X+f.Y*f.Y<radius*radius;
        float t=-along+(inside?1:-1)*MathF.Sqrt(reach);
        if(t<=0)return pixels;
        float ex=f.X+t*cos,ey=f.Y+t*sin,nx=ex/radius,ny=ey/radius;
        if(model.Project(new Vec3(ex-ny*60,ey+nx*60,0)) is not {} a || model.Project(new Vec3(ex+ny*60,ey-nx*60,0)) is not {} b || Math.Abs(b.X-a.X)<1)return pixels;
        for(int column=0;column<cal.Columns;column++)
        {
            double line=a.Y+(b.Y-a.Y)*(column-a.X)/(b.X-a.X);
            for(int row=0;row<cal.Rows;row++)pixels[row*cal.Columns+column]=row<line==inside?(byte)60:(byte)150;
        }
        return pixels;
    }
    private Run RunFreeplay(Scene scene)
    {
        string directory=Directory.CreateTempSubdirectory("cozmo-visit-edge-").FullName;
        try
        {
            long initial=NativeRewardCanonicalTests.InitialSeconds()+20;long wall=initial*1_000_000;
            var options=NativeRewardCanonicalTests.SourceOptions(directory,()=>wall,1f,0);
            byte[] stored=NativeRewardCanonicalTests.StoredBlob(.8f,.8f,.9f,2,1,NativeRewardCanonicalTests.InitialSeconds());
            using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,engineOptions:options,sourceNvEmpty:true,sourceNvStored:tag=>tag==NeedsManager.NeedsNvKey?stored:null);
            var engine=f.Rig.Engine;var manager=f.Robot.BehaviorManagerOwner!;var firmware=new Firmware(f);
            var edge=Assert.IsType<VisitInterestingEdgeBehavior>(f.Runtime.Bound["Hiking_VisitInterestingEdge"]);
            double start=engine.Timer.Seconds,last=start,idle=0,run=0,longest=0,hikingIdle=0,longestHiking=0,nextCamera=start+.5;
            int mark=f.Rig.Mark(),state=0,starts=0,completions=0,visits=0;byte active=0;uint camera=1;string? activity=null;bool gathered=false,drove=false;double vantage=double.NaN;
            var chosen=new List<(double,string)>();var holds=new List<string>();var states=new List<(double,int)>();var distances=new List<float>();var trace=new List<string>();
            var prior=manager.DasTransition;manager.DasTransition=next=>{prior?.Invoke(next);chosen.Add((engine.Timer.Seconds-start,next.Id));if(next.Id==edge.Id)starts++;};
            // Runs ahead of the wait's own 0x43 subscription, after the frame's map tail, so the gather reads this chain's distance.
            using var supplied=engine.AnimationMessages.SubscribeMessage(0x43,true,payload=>
            {
                if(edge.NativeState!=2 || scene.SuppliedCloseEdgeMm is not {} close || payload is not ProcessedImageReport report || !report.Modes.Contains(4u))return;
                var chain=new OverheadEdgePointChain{IsBorder=true};
                for(int y=-20;y<=20;y+=5)chain.Points.Add(new(new Vec2(close,y),60));
                f.Runtime.ApplyOverhead(new OverheadEdgeFrame(report.Timestamp,true,GroundPlaneROI.GroundQuad(),new[]{chain}),f.M.Vision);
            });
            void Record(string text){if(text.Contains("M8-013")||text.Contains("M11-079")||text.Contains("M11-089")||text.Contains("M11-092"))lock(holds)holds.Add(text);}
            SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Record;
            try
            {
                f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
                while(engine.Timer.Seconds-start<scene.Seconds)
                {
                    f.AnswerInFlightNv();f.Motors();double elapsed=engine.Timer.Seconds-start;wall=initial*1_000_000+(long)(elapsed*1_000_000);
                    firmware.LineSpeed=state==2?50f:100f; // borderApproachSpeed_mmps 50 while gathering
                    firmware.Step((float)(engine.Timer.Seconds-last));
                    if(engine.Timer.Seconds>=nextCamera){f.NativeCameraFrame(camera,Floor(f,scene.PaintedCircleRadius));camera++;nextCamera=engine.Timer.Seconds+(state==2?.1:.5);
                    }
                    foreach(var message in f.Rig.SentSince(mark))
                    {
                        if(message is StartOfAnimation begin){active=begin.AnimId;f.Rig.Data(new AnimationStarted{AnimId=active});}
                        else if(message is EndOfAnimation)f.Rig.Data(new AnimationEnded{AnimId=active});
                    }
                    mark=f.Rig.Mark();
                    double now=engine.Timer.Seconds;
                    string? current=f.Runtime.FreeplayObserver.Current?.Id;
                    if(current!=activity){activity=current;trace.Add($"{now-start:F1} activity {activity}");if(activity=="Hiking")visits++;hikingIdle=0;}
                    if(manager.Current is null)
                    {
                        idle+=now-last;run+=now-last;longest=Math.Max(longest,run);
                        if(activity=="Hiking"){hikingIdle+=now-last;longestHiking=Math.Max(longestHiking,hikingIdle);}
                    }
                    else{run=0;hikingIdle=0;}
                    if(edge.NativeState!=state)
                    {
                        if(state is 3 or 4 && edge.NativeState==1)completions++;
                        state=edge.NativeState;states.Add((now-start,state));
                        if(state==2&&!gathered)
                        {
                            gathered=true;
                            if(edge.Goals.Vantage168.FirstOrDefault() is {} goal)
                            {using var mutation=f.M.World.EnterPoseMutation();float dx=(float)goal.Translation.X-f.X,dy=(float)goal.Translation.Y-f.Y;vantage=Math.Sqrt(dx*dx+dy*dy);drove=firmware.Paths>0;}
                        }
                    }
                    if(state==2)distances.Add(f.M.Whiteboard.OverheadEdgeDistance);
                    last=now;
                }
                if(state is 3 or 4 && !edge.EngineRunning)completions++;
                var content=f.M.World.Maps.Current!.NativeContentCounts;
                output.WriteLine($"chosen: {string.Join(", ",chosen.Select(c=>$"{c.Item1:F1}:{c.Item2}"))}");
                output.WriteLine($"idle={idle:F1}s longest={longest:F1}s hikingLongest={longestHiking:F1}s visits={visits} visitStarts={starts} completions={completions} frames={camera-1} paths={firmware.Paths} vantageError={vantage:F1} pose=({f.X:F1},{f.Y:F1},{f.Yaw:F2})");
                output.WriteLine($"states: {string.Join(" ",states.Select(s=>$"{s.Item1:F1}:{s.Item2}"))}");
                output.WriteLine($"content: {string.Join(" ",content.Select(c=>$"{c.Key}={c.Value}"))}");
                output.WriteLine($"distances: {string.Join(" ",distances.Distinct().Take(20))}");
                foreach(string line in trace)output.WriteLine(line);foreach(string line in holds.Distinct())output.WriteLine("missing: "+line);
                return new(chosen,holds,idle,longest,longestHiking,visits,states,starts,completions,content,vantage,drove,distances,trace,firmware.Paths);
            }
            finally{SteppedBehavior.MissingReported-=Record;manager.DasTransition=prior;f.Rig.Robot.ActivateHighLevelActivity("Selection");}
        }
        finally{Directory.Delete(directory,true);}
    }

    private static void AssertNoHold(Run run)
    {
        Assert.Empty(run.Holds);
        Assert.True(run.HikingVisits>=1,"no Hiking visit");
        Assert.True(run.LongestHikingIdle<5,"longest idle inside a Hiking visit "+run.LongestHikingIdle); // harness bound, not an engine value
    }

    // Content with no border: pose writers and clear reports only. Seed sets 9 and 6 stay empty, 0x0068AC24/28 -> 0x0068B066,
    // so PickGoals returns at 0x005E6880 and the behaviour scores 0 with no RNG or map read.
    [Fact]
    public void MapContentWithNoBorderGivesAKnownAnswerAndHikingKeepsRunning()
    {
        var run=RunFreeplay(new(null));
        AssertNoHold(run);
        Assert.True(run.Content.GetValueOrDefault(MemoryMapContentType.ClearOfObstacle)>0);
        Assert.Equal(0,run.Content.GetValueOrDefault(MemoryMapContentType.InterestingEdge));
        Assert.Equal(0,run.Starts);
    }

    // A painted edge ahead. S1 0x005E7044 drives to the vantage and S2 waits five mode 4 images (0x005E7B26). A far distance creeps
    // on at 50 mm/s (0x005E777E, the threshold is 40+20+1e-5 mm at 0x005E775A) and waits again, so the distance falls. When the edge
    // leaves the image the reached map tail writes NaN (0x006803AC..0x006803EC), and 0x005E7564 plays squint end and not-found: state 4.
    [Fact]
    public void PaintedEdgeRunsVisitToTheVantageCreepsAndCompletes()
    {
        var run=RunFreeplay(new(150f));
        AssertNoHold(run);
        Assert.True(run.Starts>=1,"VisitInterestingEdge never started");
        Assert.True(run.DroveToVantage,"no path executed before gathering");
        Assert.True(run.GatherDistanceToVantage<=DriveToPoseAction.DefaultGoalDistanceToleranceMm+20f,"vantage error "+run.GatherDistanceToVantage); // 10 mm goal tolerance plus the rig's 20 mm centre offset
        Assert.Contains(run.States,s=>s.State==2);Assert.Contains(run.States,s=>s.State==4);
        Assert.True(run.Completions>=1,"VisitInterestingEdge never completed");
        var finite=run.Distances.Where(d=>!float.IsNaN(d)).Distinct().ToList();
        Assert.True(finite.Count>=2 && finite[1]<finite[0],"far branch did not approach: "+string.Join(",",finite));
    }

    // An edge 55 mm ahead is within the 60 mm limit, so S3 0x005E7D78 flags the visited cone (0x005E8038, type 10 through the override
    // table: only type 9 changes), drives F(F(55-32.1)-18) mm and observes. State 3 ends the run with no state 4.
    [Fact]
    public void CloseEdgeFlagsTheVisitedConeAndObserves()
    {
        var run=RunFreeplay(new(150f,SuppliedCloseEdgeMm:55f));
        AssertNoHold(run);
        Assert.Contains(run.States,s=>s.State==3);
        Assert.DoesNotContain(run.States,s=>s.State==4);
        Assert.True(run.Completions>=1,"VisitInterestingEdge never completed");
        Assert.True(run.Content.GetValueOrDefault(MemoryMapContentType.NotInterestingEdge)>0);
    }
}
