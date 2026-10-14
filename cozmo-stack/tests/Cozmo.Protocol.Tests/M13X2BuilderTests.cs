using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M13X2BuilderTests
{
    private static string Obb(){for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent){var p=Path.Combine(d.FullName,"re-analysis/obb");if(MotionPrimitiveSet.FromObb(p) is not null)return p;}throw new InvalidOperationException("Shipped primitives required");}
    private static Pose3d At(float x,float y,float a=0)=>new(Mat3.AboutZ(a),new(x,y,0));
    [Fact] [Trait("Fidelity","M13-035 HARDWARE_ONLY scheduling")]
    public void ActualDriveAppendsFinalGoalTurnAndSucceeds()
    {
        using var signals=SignalTestContext.Install();using var rig=new Rig();Assert.True(rig.M.LoadPlanner(Obb()));
        var events=new System.Collections.Concurrent.ConcurrentQueue<EngineLogEntry>();rig.Robot.Engine.LogEntry+=events.Enqueue;
        var drive=new DriveToPoseAction(rig.M){Goal=At(200,0,MathF.PI/2)};
        var result=SignalTestContext.Result(drive.RunAsync(default),()=>rig.Pump());
        Assert.Equal(ActionResult.Success,result);var turn=rig.Sent.OfType<AppendPathSegmentPointTurn>().Last();
        Assert.Equal(MathF.PI/2,turn.TargetAngleRad);Assert.Equal(2,turn.Speed.SpeedMmps);Assert.Equal(10,turn.Speed.AccelMmps2);
        Assert.InRange(MathF.Abs((float)rig.Angle-MathF.PI/2),0,0.05f);
        Assert.Equal(EngineLogLevel.Event,events.First(e=>e.Name=="robot.planner_selected").Level);
        Assert.Equal(EngineLogLevel.Event,events.Last(e=>e.Name=="robot.plan_complete").Level);
    }
    [Fact]
    public void SelectionUsesClosestGoalAndConversionSaturates()
    {
        using var rig=new Rig();Assert.True(rig.M.LoadPlanner(Obb()));var p=rig.M.GetNativePlanners();var entries=new System.Collections.Concurrent.ConcurrentQueue<EngineLogEntry>();rig.Robot.Engine.LogEntry+=entries.Enqueue;
        p.Configure(Pose3d.Identity,new[]{At(500,500),At(0,0)},PathMotionProfile.Default,null);p.Select();Assert.Same(p.Face,p.Current);
        p.Configure(Pose3d.Identity,new[]{At(float.NaN,0)},PathMotionProfile.Default,null);p.Select();Assert.Equal("0",entries.Last(e=>e.Name=="robot.planner_selected").Data["$data"]);
        p.Configure(Pose3d.Identity,new[]{At(float.PositiveInfinity,0)},PathMotionProfile.Default,null);p.Select();Assert.Equal("2147483647",entries.Last(e=>e.Name=="robot.planner_selected").Data["$data"]);
        p.Configure(Pose3d.Identity,new[]{At(20,0)},PathMotionProfile.Default,null);p.Select();Assert.True(p.UseFallback());
        Assert.Equal(EngineLogLevel.Event,entries.Last(e=>e.Name=="robot.fallback_planner_used").Level);
    }
    [Fact] [Trait("Fidelity","M13-035 HARDWARE_ONLY scheduling")]
    public void FailedRetrievalAfterPreviousPlanReturnsFreshEmptyPath()
    {
        using var rig=new Rig();Assert.True(rig.M.LoadPlanner(Obb()));var p=rig.M.GetNativePlanners();p.Configure(Pose3d.Identity,new[]{At(200,0)},PathMotionProfile.Default,null);p.Select();Assert.Equal(1,p.StartPlanning());
        Assert.True(SpinWait.SpinUntil(()=>p.Current!.CheckPlanningStatus==2,TimeSpan.FromSeconds(10)));Assert.NotEmpty(p.Read().Path);
        rig.X=1000;rig.State();Assert.Empty(p.Read().Path);
    }
    [Fact] [Trait("Fidelity","M13-035 HARDWARE_ONLY scheduling")]
    public void TurnOnlyGeneratedPathStillGetsCorrectionAfterPop()
    {
        using var rig=new Rig();Assert.True(rig.M.LoadPlanner(Obb()));var runtime=rig.M.GetNativePlanners().Lattice!;
        Assert.Equal(1,runtime.ComputePath(Pose3d.Identity,new[]{At(0,0,MathF.PI/2)}));
        Assert.True(SpinWait.SpinUntil(()=>runtime.CheckPlanningStatus==2,TimeSpan.FromSeconds(10)));
        Assert.True(runtime.GetCompletePath(Pose3d.Identity,PathMotionProfile.Default,out var path,out _));
        var turn=Assert.IsType<PathSegment.PointTurn>(Assert.Single(path));Assert.Equal(MathF.PI/2,turn.TargetAngleRad);Assert.Equal(-2,turn.SpeedRadPerSec);
    }
    [Fact]
    public void MinimalNaNGeneratesNoTravelAndAdvancePacksFourteenBits()
    {
        var p=new NativeShortPlanner(true,(_,_,_,_)=>{},_=>{});p.ComputePath(Pose3d.Identity,new[]{At(float.NaN,0)});p.GetCompletePath(Pose3d.Identity,PathMotionProfile.Default,out var path,out _);Assert.Empty(path);
        var action=new MotionPrimitive(0,0,1,0,19,Array.Empty<IntermediatePose>(),1);var state=NativePlanDependencies.Advance(new(8191,0,0),action);Assert.Equal(-8192,state.X);Assert.Equal(3,state.Theta);
    }
    [Fact]
    public void CheckedApplyActionSumsHitsBySampleBucketAndStopsBeforeWritingState()
    {
        var prims=MotionPrimitiveSet.FromObb(Obb())!;var env=new LatticeEnvironment(prims);
        // Controlled native-layout input: two weights (1/2,1/4), distinct buckets, and two hits in bucket zero.
        var samples=new[]{new IntermediatePose(0,0,0,0,0.5f),new IntermediatePose(0,0,0,1,0.25f)};
        var primitive=new MotionPrimitive(0,0,1,0,1,samples,1);
        ((List<MotionPrimitive>)prims.ByAngle[0])[0]=primitive;
        var square=new FastPolygonF(new[]{new P2f(-10,-10),new P2f(-10,10),new P2f(10,10),new P2f(10,-10)});
        var b0=(List<(FastPolygonF Poly,float Penalty)>)env.Bucket(0);
        var b1=(List<(FastPolygonF Poly,float Penalty)>)env.Bucket(1);
        b0.Add((square,4));b0.Add((square,8));b1.Add((square,16));
        var step=env.ApplyAction(new(0,0,0),0,true);
        Assert.Equal(10,step.Penalty);Assert.Equal(new LatticeState(1,0,1),step.State); // (4+8)/2 + 16/4.
        b0.Clear();b1.Clear();b0.Add((square,2000));b0.Add((square,20));
        step=env.ApplyAction(new(0,0,0),0,true);
        Assert.Equal(1000,step.Penalty);Assert.Equal(new LatticeState(0,0,0),step.State);
        step=env.ApplyAction(new(0,0,0),255,true);
        Assert.Equal(1001,step.Penalty);Assert.Equal(new LatticeState(0,0,0),step.State);
        step=env.ApplyAction(new(0,0,0),0,false);Assert.Equal(0,step.Penalty);Assert.Equal(new LatticeState(1,0,1),step.State);
    }
    [Fact] [Trait("Fidelity","M13-035 HARDWARE_ONLY scheduling")]
    public void FreshPlanLogsStateAndBothPlanLabelsBeforeReady()
    {
        using var rig=new Rig();Assert.True(rig.M.LoadPlanner(Obb()));var runtime=rig.M.GetNativePlanners().Lattice!;
        var entries=new List<(EngineLogLevel Level,string? Channel,string Key,string Text)>();
        // Production source sink is the engine; the native context mutex orders observation with the worker.
        rig.Robot.Engine.LogEntry+=e=>entries.Add((e.Level,e.Channel,e.Name,e.Text));
        Assert.Equal(1,runtime.ComputePath(Pose3d.Identity,new[]{At(200,0)}));
        Assert.True(SpinWait.SpinUntil(()=>runtime.CheckPlanningStatus==2,TimeSpan.FromSeconds(10)));
        lock(runtime.ContextMutex)
        {
            int old=entries.FindIndex(e=>e.Key=="LatticePlannerImpl"&&e.Text=="old plan");
            int next=entries.FindIndex(e=>e.Key=="LatticePlannerImpl"&&e.Text=="new plan");
            Assert.True(old>=0&&next>old);
            Assert.Equal(EngineLogLevel.Debug,entries[old].Level);Assert.Equal("Planner",entries[old].Channel);
            // Identity's tied magnitude branch is atan2f(-m01,m11): negative zero (pose R14), retained by ostream.
            Assert.Contains(entries,e=>e.Level==EngineLogLevel.Info&&e.Channel=="Unnamed"&&e.Key=="LatticePlanner"&&e.Text=="currentRobotState:(0,0,-0)");
            Assert.Contains(entries,e=>e.Key=="LatticePlanner.GetPlan.OldPlanUnsafe"&&e.Text=="old plan unsafe! Will replan, starting from 0, keeping 0 actions from oldPlan.");
        }
    }
}
