using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M12PathMachineBuilderTests
{
    private static PathSegment[] Lines(int n)=>Enumerable.Range(0,n).Select(i=>(PathSegment)new PathSegment.Line(i,0,i+1,0,100,200,500)).ToArray();
    [Fact] public void P1P2ConstructorFieldsAndChangeOnlyLogBeforeStore()
    {
        using var r=new M4ControlTests.Rig();var p=r.Robot.Motion.Path;
        Assert.Equal(4,p.DriveToPoseStatus);Assert.Equal(255,p.Field40);Assert.Equal(0,p.Field42);Assert.Equal(0,p.Field44);Assert.Equal(0,p.Field4A);Assert.False(p.Field47);
        var entries=new List<EngineLogEntry>();int atLog=-1;r.Engine.LogEntry+=e=>{if(e.Name=="PathComponent.TransitionStatus"){entries.Add(e);atLog=p.DriveToPoseStatus;}};
        p.SetStatus(1);p.SetStatus(1);var one=Assert.Single(entries);Assert.Equal(4,atLog);Assert.Equal((EngineLogLevel.Info,"Planner","Ready -> ComputingPath"),(one.Level,one.Channel,one.Text));
    }
    [Theory] [InlineData(0,false,false,true,false,false)] [InlineData(1,true,false,false,false,false)] [InlineData(2,true,true,false,false,true)] [InlineData(3,true,true,false,false,false)] [InlineData(4,false,false,false,false,false)] [InlineData(5,false,false,false,true,true)] [InlineData(6,false,false,true,true,true)]
    public void P3PredicatesUseNativeMasks(int s,bool active,bool follow,bool failed,bool waiting,bool response)
    {using var r=new M4ControlTests.Rig();var p=r.Robot.Motion.Path;p.DriveToPoseStatus=s;Assert.Equal(active,p.IsActive);Assert.Equal(follow,p.HasPathToFollow);Assert.Equal(failed,p.LastPathFailed);Assert.Equal(waiting,p.IsWaitingToCancelPath);Assert.Equal(response,p.IsWaitingForRobotResponse);}
    [Fact] public void P12P19OneCounterInitialSixThenSegmentGatedDole()
    {
        using var r=new M4ControlTests.Rig();r.ToSynced();r.State();var sender=new PathSender(r.Robot);var p=r.Robot.Motion.Path;
        Assert.Equal(1,sender.Execute(Lines(12)));Assert.Equal(2,p.DriveToPoseStatus);Assert.Equal(6,sender.Sent.OfType<AppendPathSegmentLine>().Count());Assert.IsType<ClearPath>(sender.Sent[0]);Assert.IsType<ExecutePath>(sender.Sent.Last());
        p.UpdateCurrentPathSegment(2);Assert.Equal(255,p.Field40);Assert.Equal(6,sender.Sent.OfType<AppendPathSegmentLine>().Count());
        r.Data(new PathFollowingEvent{EventId=1,EventType=0});r.Tick();var state=r.MakeState();state.CurrPathSegment=2;r.Data(state);r.Tick();
        Assert.Equal(1,p.Field44);Assert.Equal(2,p.Field40);Assert.Equal(8,sender.Sent.OfType<AppendPathSegmentLine>().Count());
        p.Field42=65535;Assert.Equal(0,sender.Execute(Lines(1)));Assert.Equal(65535,p.Field4A);
    }
    [Fact] public void P12EmptyPathReadsTheNativeByteCountBeforeAnySend()
    {
        using var r=new M4ControlTests.Rig();var p=r.Robot.Motion.Path;int sends=0;p.SendPathMessage=_=>{sends++;return true;};p.Field42=19;
        // 0x0064A35A ldrb [path+4]: the model's segment count truncates to the same u8 field.
        Assert.Equal(0,p.ExecutePath(Lines(256),false));Assert.Equal(0,sends);Assert.Equal(19,p.Field42);Assert.Equal(4,p.DriveToPoseStatus);
    }
    [Fact] public void P20P21P22ActualC6SubscriptionKeepsMismatchRules()
    {
        using var r=new M4ControlTests.Rig();r.ToSynced();r.State();var p=r.Robot.Motion.Path;p.Field42=7;p.SetStatus(2);
        r.Data(new PathFollowingEvent{EventId=8,EventType=0});r.Tick();Assert.Equal(8,p.Field44);Assert.Equal(3,p.DriveToPoseStatus);
        r.Data(new PathFollowingEvent{EventId=8,EventType=1});r.Tick();Assert.Equal(3,p.DriveToPoseStatus);
        r.Data(new PathFollowingEvent{EventId=9,EventType=2});r.Tick();Assert.Equal(4,p.DriveToPoseStatus);
    }
    [Fact] public void P23CancellationUsesOneCancelIdAndSwallowsDifferentCompletions()
    {
        using var r=new M4ControlTests.Rig();var p=r.Robot.Motion.Path;p.Field42=10;p.Field4A=9;p.SetStatus(5);
        Assert.True(p.SwallowCanceled(new PathFollowingEvent{EventId=10,EventType=2}));Assert.Equal(5,p.DriveToPoseStatus);
        Assert.True(p.SwallowCanceled(new PathFollowingEvent{EventId=9,EventType=1}));Assert.Equal(4,p.DriveToPoseStatus);
        p.SetStatus(6);p.SwallowCanceled(new PathFollowingEvent{EventId=9,EventType=2});Assert.Equal(0,p.DriveToPoseStatus);
        p.SetStatus(3);Assert.True(p.SwallowCanceled(new PathFollowingEvent{EventId=9,EventType=2}));Assert.False(p.SwallowCanceled(new PathFollowingEvent{EventId=10,EventType=2}));
    }
    [Fact] public void P8ResponseTimeoutIsStrictOneSecondAndSetsFailureWaitingState()
    {
        using var r=new M4ControlTests.Rig();r.ToSynced();r.State();var p=r.Robot.Motion.Path;p.Field42=3;p.SetStatus(2);p.Field3C=r.Engine.Timer.SecondsF;
        r.Tick(1000);Assert.Equal(2,p.DriveToPoseStatus);r.Tick(1);Assert.Equal(6,p.DriveToPoseStatus);Assert.Equal(3,p.Field4A);
    }
    /// <summary>A plan stand-in for the selected planner (pc+0x20): status 2 and a fixed complete path/goal index. Substitutes the planner only.</summary>
    private sealed class PlanStub:INativePathPlanner
    {
        private readonly IReadOnlyList<PathSegment> _path;private readonly byte _goal;
        internal PlanStub(IReadOnlyList<PathSegment> path,byte goal){_path=path;_goal=goal;}
        public string Name=>"PlanStub";
        public bool SkipsSafetyCheck=>true;
        public int ComputePath(Cozmo.Robot.Vision.Pose3d start,IReadOnlyList<Cozmo.Robot.Vision.Pose3d> goals)=>1;
        public int ComputeNewPathIfNeeded(Cozmo.Robot.Vision.Pose3d start,bool force)=>1;
        public int CheckPlanningStatus=>2;
        public bool GetCompletePath(Cozmo.Robot.Vision.Pose3d start,PathMotionProfile profile,out IReadOnlyList<PathSegment> path,out byte goal){path=_path;goal=_goal;return true;}
        public void StopPlanning(){}
    }
    // Ported from the retired legacy StartDrivingToPose(int, ...) entry (RETIRE-LEGACY-DRIVE-API-1): the drive starts through the native entry
    // StartNativeDrivingToPose and the selected planner (pc+0x20) is then substituted by a stub that reports a complete plan with goal index 3.
    // reach: unit — direct native entry call and a substituted planner; HandlePlanComplete, ExecutePath and the goal-index write are the stack's.
    // P11 (HandlePlanComplete): the goal-index byte is written from the plan after ExecutePath whatever its outcome, and not for an empty plan. Expected
    // values unchanged from the legacy test.
    private static Cozmo.Robot.Vision.Pose3d Goal(PickupPyramidNativeExecutionTests.Fixture f)=>new(Cozmo.Robot.Vision.Rotation3d.Identity,new Cozmo.Robot.Vision.Vec3(30,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
    [Theory] [InlineData(false)] [InlineData(true)]
    public void P11GoalIndexWrittenOnBothExecuteOutcomes(bool failure)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var p=f.Rig.Robot.Motion.Path;p.SendPathMessage=m=>!failure||m is not ExecutePath;var idx=new PathGoalIndex();
        using(var goal=Goal(f))Assert.Equal(0,p.StartNativeDrivingToPose(f.M,new[]{goal},idx,false));
        f.M.GetNativePlanners().Current=new PlanStub(Lines(1),3);
        p.Update();Assert.Equal(3,idx.Value);Assert.Equal(failure?0:2,p.DriveToPoseStatus);
    }
    [Fact] public void P11EmptyPlanDoesNotWriteGoalIndex()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();var p=f.Rig.Robot.Motion.Path;var idx=new PathGoalIndex{Value=2};p.SendPathMessage=_=>true;
        using(var goal=Goal(f))Assert.Equal(0,p.StartNativeDrivingToPose(f.M,new[]{goal},idx,false));
        // StartNativeDrivingToPose resets the index byte to the caller's shared slot only through Init (I4); the entry itself leaves it at 2.
        f.M.GetNativePlanners().Current=new PlanStub(Array.Empty<PathSegment>(),3);
        p.Update();Assert.Equal(2,idx.Value);Assert.Equal(4,p.DriveToPoseStatus);Assert.False(p.Field47);
    }
}
