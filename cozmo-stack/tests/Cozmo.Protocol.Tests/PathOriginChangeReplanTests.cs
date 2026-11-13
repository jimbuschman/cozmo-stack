using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

/// <summary>
/// M12-032, PathComponent's reaction to a change of the robot's current origin (PATH-ORIGIN-CHANGE-REPLAN-1 census rows H1..H7, R1..R8, O3).
/// Expected values come from the census rows: HandlePossibleOriginChanges 0x0064980C..0x006498EE, RejiggerTargetsAndReplan 0x006499D0..0x00649B44,
/// StartPlanner(const Pose3d&amp;) 0x0064B090, ExecuteCustomPath's planner clear 0x0064B1E6, StartDrivingToPose's params-origin write 0x0064AD6A.
/// </summary>
// Shares the process-wide MISSING statics and the engine fixture with the other fixture classes, so it runs in their non-parallel collection (verifier T1:
// a one-off NullReferenceException in the fixture constructor under parallel load).
[Collection("SteppedBehavior missing-report statics")]
public sealed class PathOriginChangeReplanTests
{
    private static Pose3d At(Fixture f,float x,float y=0,float yaw=0)=>new(Rotation3d.FromAngleAxis(yaw,new(0,0,1)),new(x,y,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));

    /// <summary>A recording stand-in for the selected planner (pc+0x20). Substitutes the planner only; the dispatch and the Rejigger are the stack's.</summary>
    private sealed class RecordingPlanner:INativePathPlanner
    {
        internal readonly List<string> Calls=new();
        internal Pose3d? Start;internal IReadOnlyList<Pose3d>? Goals;internal int ComputeResult=1,RestartResult=1;
        public string Name=>"Recording";
        public bool SkipsSafetyCheck=>true;
        public int ComputePath(Pose3d start,IReadOnlyList<Pose3d> goals){Calls.Add("ComputePath");Start=start;Goals=goals;return ComputeResult;}
        public int ComputeNewPathIfNeeded(Pose3d start,bool force){Calls.Add($"ComputeNewPathIfNeeded({force})");Start=start;return RestartResult;}
        internal int Status=1;public int CheckPlanningStatus=>Status; // 1, still running: UpdatePlanning leaves the status alone
        public bool GetCompletePath(Pose3d start,PathMotionProfile profile,out IReadOnlyList<PathSegment> path,out byte goal){path=Array.Empty<PathSegment>();goal=0;return false;}
        public void StopPlanning()=>Calls.Add("StopPlanning");
    }

    private sealed class Logs:IDisposable
    {
        private readonly Fixture _f;
        internal readonly System.Collections.Concurrent.ConcurrentQueue<EngineLogEntry> Entries=new();
        internal readonly System.Collections.Concurrent.ConcurrentQueue<string> Stack=new();
        internal Logs(Fixture f){_f=f;f.Rig.Engine.LogEntry+=Entries.Enqueue;f.Rig.Engine.StackLogLine+=Stack.Enqueue;}
        public void Dispose(){_f.Rig.Engine.LogEntry-=Entries.Enqueue;_f.Rig.Engine.StackLogLine-=Stack.Enqueue;}
        internal List<string?> Names=>Entries.Select(e=>e.Name).ToList();
    }

    private static bool IsPathMessage(RobotMessage m)=>m is ClearPath or ExecutePath or AppendPathSegmentLine or AppendPathSegmentArc or AppendPathSegmentPointTurn;

    /// <summary>Starts a native DriveToPose to a near goal (short planner) and brings it to FollowingPath through the real tick and the C6 handler.</summary>
    private static void FollowNative(Fixture f,float goalX=30)
    {
        var path=f.Rig.Robot.Motion.Path;
        using var goal=At(f,goalX);
        Assert.Equal(0,path.StartNativeDrivingToPose(f.M,new[]{goal},new PathGoalIndex(),false));
        Assert.Equal(1,path.DriveToPoseStatus);
        Assert.Equal(f.Robot.CurrentOriginId,path.ParamsOrigin); // O3 0x0064AD6A
        for(int i=0;i<3&&path.DriveToPoseStatus!=2;i++)f.Rig.Tick(); // UpdatePlanning -> HandlePlanComplete -> ExecutePath
        Assert.Equal(2,path.DriveToPoseStatus);
        f.Rig.Data(new PathFollowingEvent{EventId=path.Field42,EventType=(byte)PathEventType.Started});f.Rig.Tick();
        Assert.Equal(3,path.DriveToPoseStatus);
    }

    // reach: unit — the drive is started by a direct StartNativeDrivingToPose call (the DriveToPoseAction Init is not run); the dispatch runs in the
    // real engine tick (PathComponentTick) with the production OriginOwnerInput binding, and the origin change is the production Robot::Delocalize.
    [Fact]
    public void M12_032_H5_DelocalizeWhileFollowingAbortsWithStatus6AndNotPossible()
    {
        using var f=new Fixture();using var logs=new Logs(f);var path=f.Rig.Robot.Motion.Path;
        FollowNative(f);
        uint o1=f.Robot.CurrentOriginId;
        f.Robot.Delocalize(false);
        Assert.NotEqual(o1,f.Robot.CurrentOriginId);
        Assert.Equal(3,path.DriveToPoseStatus); // Robot::Delocalize 0x00510A24 calls no PathComponent function (H5)
        int mark=f.Rig.Mark();logs.Entries.Clear();
        f.Rig.Tick();
        // H5 0x00649870..0x006498BE: params != current and the params origin is a separate root -> Abort (ClearPath sent), status 5 then 6, info.
        Assert.Contains(f.Rig.SentSince(mark),m=>m is ClearPath);
        Assert.Equal(6,path.DriveToPoseStatus);
        Assert.Equal(PathComponent.UnknownOriginId,path.ParamsOrigin); // O2 0x006491B4
        var names=logs.Names;
        int abort=names.IndexOf("PathComponent.Abort"),notPossible=names.IndexOf("PathComponent.Update.Replan.NotPossible");
        Assert.True(abort>=0&&notPossible>abort);
        var info=logs.Entries.First(e=>e.Name=="PathComponent.Update.Replan.NotPossible");
        Assert.Equal((EngineLogLevel.Info,"Planner","Our goal is in another coordinate frame that we can't get wrt current, we can't replan"),(info.Level,info.Channel,info.Text));
        Assert.Contains(logs.Entries,e=>e.Name=="PathComponent.TransitionStatus"&&e.Text=="FollowingPath -> WaitingToCancelPath");
        Assert.Contains(logs.Entries,e=>e.Name=="PathComponent.TransitionStatus"&&e.Text=="WaitingToCancelPath -> WaitingToCancelPathAndSetFailure");
        Assert.DoesNotContain(logs.Names,n=>n=="PathComponent.Update.Replan.RejiggeringPlanner");
    }

    // reach: unit — direct StartNativeDrivingToPose, then the selected planner (pc+0x20) is substituted by a recorder after FollowingPath so the
    // planner's inputs can be observed; the origin change is the production PoseOriginList Rejigger (EngineRobot.RejiggerOrigins, census X7) called
    // directly instead of through LocalizeToObject. The dispatch and the Rejigger run in the real engine tick.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void M12_032_H6_R1_R6_RejiggerReexpressesGoalsAndReplansFromAFreshDriveCenterWithoutSending(bool replanRunning)
    {
        using var f=new Fixture();using var logs=new Logs(f);var path=f.Rig.Robot.Motion.Path;
        uint o1=f.Robot.CurrentOriginId;
        f.Robot.Delocalize(false);uint o2=f.Robot.CurrentOriginId;Assert.NotEqual(o1,o2);
        FollowNative(f,25);
        Assert.Equal(o2,path.ParamsOrigin);
        var planners=f.M.GetNativePlanners();
        var goal=Assert.Single(planners.Goals);
        Assert.Same(f.Robot.GetNativeOrigin(o2).Node,goal.Node!.Parent);
        float gx=(float)goal.Translation.X,gy=(float)goal.Translation.Y;double gyaw=goal.AngleAroundZ;
        var recorder=new RecordingPlanner();planners.Current=recorder;
        if(replanRunning)path.Field47=true; // R3: +0x47 can be 1 in status 3 after RestartPlannerIfNeeded (S1)
        // o2 becomes a child of o1 at (100, 50) turned 0.5 rad; o1 is current again (PoseOriginList::Rejigger, 0x00847E1A..0x00847E7E).
        const float yaw=.5f,tx=100,ty=50;
        Assert.Equal(0u,f.Robot.RejiggerOrigins(f.Robot.GetNativeOrigin(o1),new Transform3d(Rotation3d.FromAngleAxis(yaw,new(0,0,1)),new(tx,ty,0))));
        Assert.Equal(o1,f.Robot.CurrentOriginId);
        int mark=f.Rig.Mark();logs.Entries.Clear();
        f.Rig.Tick();
        // H6 0x006498C0..0x006498D2 -> R2 0x006499DE..0x006499E8: params origin := current.
        Assert.Equal(o1,path.ParamsOrigin);
        // R3 0x006499EA..0x006499FA: a running planner is stopped first; then R6's ComputePath.
        Assert.Equal(replanRunning?new[]{"StopPlanning","ComputePath"}:new[]{"ComputePath"},recorder.Calls);
        // R4 0x006499FE..0x00649A12
        var info=logs.Entries.First(e=>e.Name=="PathComponent.Update.Replan.RejiggeringPlanner");
        Assert.Equal((EngineLogLevel.Info,"Planner","Our goal is in another coordinate frame, but we are updating to current frame"),(info.Level,info.Channel,info.Text));
        // R5 0x00649A38..0x00649A5C: the goal the planner reads is re-expressed in place w.r.t. the world origin. Invariant: its parent is now the current
        // origin and its pose in that origin is the old o2-relative pose carried through o2's placement in o1 (the world pose is unchanged).
        Assert.Same(planners.Goals,recorder.Goals);
        Assert.Same(goal,recorder.Goals![0]);
        Assert.Same(f.Robot.GetNativeOrigin(o1).Node,goal.Node!.Parent);
        Assert.Equal(tx+Math.Cos(yaw)*gx-Math.Sin(yaw)*gy,goal.Translation.X,3);
        Assert.Equal(ty+Math.Sin(yaw)*gx+Math.Cos(yaw)*gy,goal.Translation.Y,3);
        Assert.Equal(gyaw+yaw,goal.AngleAroundZ,4);
        // R6 0x00649A5E..0x00649A68: the start is the live drive-center pose (Robot+0x2A4, GetDriveCenterPose 0x005477A0), not the Start captured at configuration.
        Assert.Same(f.Robot.ForwardPose,recorder.Start);
        Assert.NotSame(planners.Start,recorder.Start);
        // R6 0x00649A74/0x00649B3E: StartPlanner success -> +0x47 = 1, status 3 -> 1.
        Assert.True(path.Field47);
        Assert.Equal(1,path.DriveToPoseStatus);
        Assert.Contains(logs.Entries,e=>e.Name=="PathComponent.TransitionStatus"&&e.Text=="FollowingPath -> ComputingPath");
        // R6: no ClearPath, no Abort, no robot message at rejigger time.
        Assert.DoesNotContain(f.Rig.SentSince(mark),IsPathMessage);
        Assert.DoesNotContain(logs.Names,n=>n=="PathComponent.Abort"||n=="PathComponent.Update.Replan.NotPossible");
        Assert.DoesNotContain(logs.Stack,l=>l.Contains("RejiggerTargetsAndReplan"));
    }

    // reach: unit — as above (direct start, substituted planner, direct PoseOriginList Rejigger). R7: ComputePath returns 0, StartPlanner's own fallback
    // finds no fallback planner (the short planner's fallback is the lattice, removed here), and the second ReplanWithFallbackPlanner finds none either.
    [Fact]
    public void M12_032_R7_RejiggerStartFailureWarnsAndSetsStatus0()
    {
        using var f=new Fixture();using var logs=new Logs(f);var path=f.Rig.Robot.Motion.Path;
        uint o1=f.Robot.CurrentOriginId;
        f.Robot.Delocalize(false);uint o2=f.Robot.CurrentOriginId;
        FollowNative(f,25);
        var planners=f.M.GetNativePlanners();
        var recorder=new RecordingPlanner{ComputeResult=0};planners.Current=recorder;planners.Fallback=null;
        Assert.Equal(0u,f.Robot.RejiggerOrigins(f.Robot.GetNativeOrigin(o1),new Transform3d(Rotation3d.Identity,new(10,0,0))));
        int mark=f.Rig.Mark();logs.Entries.Clear();
        f.Rig.Tick();
        Assert.Equal(o1,path.ParamsOrigin); // R2 happens before the failure
        var warning=logs.Entries.First(e=>e.Name=="PathComponent.Update.Replan.RejiggeringPlanner"&&e.Level==EngineLogLevel.Warning);
        Assert.EndsWith("We could not start driving to rejiggered pose.",warning.Text); // 0x00649B04..0x00649B14 (the sink prefixes the warning tag)
        Assert.Equal(0,path.DriveToPoseStatus);                                       // 0x00649B3A..0x00649B3E
        Assert.DoesNotContain(logs.Names,n=>n=="PathComponent.Update.Rejigger.Fallback");
        Assert.DoesNotContain(f.Rig.SentSince(mark),IsPathMessage);                    // no Abort or ClearPath here either
        _=o2;
    }

    // reach: unit — direct start, then ObjectsChangedInput (BlockWorld::DidObjectsChange, 0x00623020) and the selected planner are substituted; the
    // dispatch runs in the real engine tick with no origin change.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void M12_032_H7_S1_SameOriginRestartsOnlyWhenObjectsChanged(bool changed)
    {
        using var f=new Fixture();using var logs=new Logs(f);var path=f.Rig.Robot.Motion.Path;
        FollowNative(f);
        var recorder=new RecordingPlanner();f.M.GetNativePlanners().Current=recorder;
        path.ObjectsChangedInput=()=>changed;logs.Entries.Clear();
        f.Rig.Tick();
        if(changed)
        {
            // S1 0x00649C28..0x00649C3A: ComputeNewPathIfNeeded(GetDriveCenterPose(), false); 1 -> debug "PathComponent.Replan.Running", +0x47 = 1, status stays 3.
            Assert.Equal(new[]{"ComputeNewPathIfNeeded(False)"},recorder.Calls);
            Assert.Same(f.Robot.ForwardPose,recorder.Start);
            Assert.Contains(logs.Entries,e=>e.Name=="PathComponent.Replan.Running"&&e.Level==EngineLogLevel.Debug&&e.Channel=="Planner");
            Assert.True(path.Field47);
        }
        else Assert.Empty(recorder.Calls); // H7: DidObjectsChange() != 1 -> return
        Assert.Equal(3,path.DriveToPoseStatus);
        Assert.DoesNotContain(logs.Names,n=>n=="PathComponent.Update.Replan.RejiggeringPlanner"||n=="PathComponent.Update.Replan.NotPossible");
    }

    // reach: unit — the native path's restart, as M12_032_H7_S1 above, with the vision history pose present and different from the drive-center pose.
    [Fact]
    public void M12_032_S1_NativeRestartPlansFromTheDriveCenterPoseNotTheHistoryPose()
    {
        using var f=new Fixture();var path=f.Rig.Robot.Motion.Path;
        FollowNative(f);
        var recorder=new RecordingPlanner();f.M.GetNativePlanners().Current=recorder;
        path.ObjectsChangedInput=()=>true;
        f.Rig.Tick();
        Assert.Same(f.Robot.ForwardPose,recorder.Start);
        Assert.NotSame(f.M.RobotPose(),recorder.Start);
    }

    // reach: unit — ExecuteCustomPath is called directly (production callers: RobotPath.ExecuteCustomPath, DriveStraightAction); the dispatch runs in the
    // real engine tick and the origin change is the production Robot::Delocalize.
    [Fact]
    public void M12_032_H1_ACustomPathNeverReactsToAnOriginChange()
    {
        using var f=new Fixture();using var logs=new Logs(f);var path=f.Rig.Robot.Motion.Path;
        FollowNative(f); // a planner was selected for the earlier drive
        f.Rig.Data(new PathFollowingEvent{EventId=path.Field42,EventType=(byte)PathEventType.Completed});f.Rig.Tick();
        Assert.Equal(4,path.DriveToPoseStatus);
        Assert.Equal(0,path.ExecuteCustomPath(new PathSegment[]{new PathSegment.Line(0,0,50,0,100,200,500)},false));
        Assert.Null(f.M.GetNativePlanners().Current); // 0x0064B1E6: pc+0x20 cleared
        f.Rig.Data(new PathFollowingEvent{EventId=path.Field42,EventType=(byte)PathEventType.Started});f.Rig.Tick();
        Assert.Equal(3,path.DriveToPoseStatus);
        f.Robot.Delocalize(false);
        int mark=f.Rig.Mark();logs.Entries.Clear();
        f.Rig.Tick();
        // H1 0x00649812..0x00649816: no planner -> return; no Abort, no ClearPath, status stays FollowingPath.
        Assert.Equal(3,path.DriveToPoseStatus);
        Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is ClearPath);
        Assert.DoesNotContain(logs.Names,n=>n=="PathComponent.Abort"||n=="PathComponent.Update.Replan.NotPossible"||n=="PathComponent.Update.Replan.RejiggeringPlanner");
    }

}
