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
        var drive=new DriveToPoseAction(rig.M){Goal=At(200,0,MathF.PI/2)};
        var result=SignalTestContext.Result(drive.RunAsync(default),()=>rig.Pump());
        Assert.Equal(ActionResult.Success,result);var turn=rig.Sent.OfType<AppendPathSegmentPointTurn>().Last();
        Assert.Equal(MathF.PI/2,turn.TargetAngleRad);Assert.Equal(2,MathF.Abs(turn.Speed.SpeedMmps));Assert.Equal(10,turn.Speed.AccelMmps2);
        Assert.InRange(MathF.Abs((float)rig.Angle-MathF.PI/2),0,0.05f);
    }
    [Fact]
    public void SelectionUsesClosestGoalAndConversionSaturates()
    {
        using var rig=new Rig();Assert.True(rig.M.LoadPlanner(Obb()));var p=rig.M.GetNativePlanners();var entries=new List<EngineLogEntry>();rig.Robot.Engine.LogEntry+=entries.Add;
        p.Configure(Pose3d.Identity,new[]{At(500,500),At(0,0)},PathMotionProfile.Default,null);p.Select();Assert.Same(p.Face,p.Current);
        p.Configure(Pose3d.Identity,new[]{At(float.NaN,0)},PathMotionProfile.Default,null);p.Select();Assert.Equal("0",entries.Last(e=>e.Name=="robot.planner_selected").Data["$data"]);
        p.Configure(Pose3d.Identity,new[]{At(float.PositiveInfinity,0)},PathMotionProfile.Default,null);p.Select();Assert.Equal("2147483647",entries.Last(e=>e.Name=="robot.planner_selected").Data["$data"]);
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
}
