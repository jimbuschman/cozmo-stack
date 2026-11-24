using System.Collections.Concurrent;
using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M13PlannerOwnershipTests
{
    private static string Obb()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {
            var path=Path.Combine(d.FullName,"re-analysis/obb");
            if(MotionPrimitiveSet.FromObb(path) is not null)return path;
        }
        throw new InvalidOperationException("Shipped motion primitives required");
    }
    private static Pose3d At(float x,float y=0,float yaw=0,Pose3d? parent=null)
        =>new(Rotation3d.FromAngleAxis(yaw,new Vec3(0,0,1)),new Vec3(x,y,0),parent:parent);
    private static void Move(Pose3d pose,float x,float yaw=0)
    {using var value=At(x,0,yaw);pose.Assign(value);}
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static IReadOnlyList<Pose3d> Owned(NativeLatticePlannerRuntime r)=>Field<IReadOnlyList<Pose3d>>(r,"_goals");
    private static IReadOnlyList<(byte Id,StateC C)> Context(NativeLatticePlannerRuntime r)=>Field<IReadOnlyList<(byte,StateC)>>(r,"_goalContext");
    private static LatticePlanner Backend()=>new(new LatticeEnvironment(MotionPrimitiveSet.FromObb(Obb())!));
    private static NativeLatticePlannerRuntime Runtime(LatticePlanner backend,M4ControlTests.Rig rig,SourceLogSink? sink=null)
        =>new(backend,rig.Robot.Vision.World,()=>null,sink??((_,_,_,_)=>{}),_=>{});
    private static void Wait(NativeLatticePlannerRuntime runtime,int status=2)
    {Assert.True(SpinWait.SpinUntil(()=>runtime.CheckPlanningStatus==status,TimeSpan.FromSeconds(10)),"Planner did not publish expected status");lock(runtime.ContextMutex){} }
    private static void HardPoint(LatticeEnvironment env,float x,float half=1)
    {
        // Unit substitution: inject an already expanded hard C-space polygon into every heading.
        var polygon=new FastPolygonF(new P2f[]{new(x-half,-half),new(x+half,-half),new(x+half,half),new(x-half,half)});
        for(int i=0;i<env.Primitives.NumAngles;i++)((List<(FastPolygonF Poly,float Penalty)>)env.Bucket(i)).Add((polygon,1000));
    }

    [Fact]
    public void ActualDispatchCapturesGoalAndNumericStartBeforeCallerMutation()
    {
        // reach: production runtime dispatch with admitted robot states and host action Init.
        // Upstream action/Path conversion remains explicit MISSING; this asserts runtime capture only.
        // P2/P4: own full goals and scalar selected start are captured before notification under E0.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        using var start=At(0);using var goal=At(200,0,.3f);
        using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);Assert.True(m.LoadPlanner(Obb()));
        var runtime=m.GetNativePlanners().Lattice!;
        using var drive=NativeDriveTestGoals.To(m, goal);
        lock(runtime.ContextMutex)
        {
            Assert.Equal(0,drive.Init());Assert.Same(runtime,m.NativePlanners!.Current);
            Assert.True(runtime.F0);Move(goal,500,1);
            drive.Dispose(); // source T4: action Abort/Stop does not release the runtime-owned admitted goal/context
            rig.State(origin:rig.Engine.Robot!.CurrentOriginId,x:50);
        }
        Wait(runtime);
        lock(runtime.ContextMutex)
        {
            var plan=Assert.IsType<LatticePlan>(runtime.TotalPlan);
            // Native entry (the legacy main-pose start and unconverted goal were retired, RETIRE-LEGACY-DRIVE-API-1): the start is the drive-center pose
            // (P6, GetDriveCenterPose 0x005477A0: -20 mm along the heading with nothing carried, x = -20 for a robot at 0 with yaw 0) and the goal is moved
            // the same -20 mm along its own heading (P9 0x005135D0 offset -20.0f, P10 0x00517EC0 x + cosf(a)*d: 200 - 20*cos(0.3) = 180.89). In the
            // 10 mm lattice cells the old 200 mm -> 20 used, that is start -2 and end 18.
            Assert.Equal((short)-2,plan.Start.X);Assert.Equal((short)18,plan.States().Last().X);
            Assert.True(runtime.GetCompletePath(start,PathMotionProfile.Default,out var path,out var chosen));
            Assert.Equal(0,chosen);Assert.Equal(.3f,Assert.IsType<PathSegment.PointTurn>(path.Last()).TargetAngleRad);
        }
    }

    [Fact]
    public void InitialFilteringKeepsOriginalIdsAndRawValuesForWorkerRevalidation()
    {
        // reach: unit (substitutes missing-map policy and direct hard C-space cells to make rejection reachable;
        // real ComputePath, worker, repeated validation and GetCompletePath; no camera/map admission claim).
        // P2/P3/P5: full vector includes rejected0, raw context keeps ID1 and101.25, and worker re-rounds raw.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.Robot.Vision.World.Maps.CurrentOrigin=uint.MaxValue;
        using var start=At(0);using var rejected=At(500);using var target=At(101.25f,0,.2f);
        var backend=Backend();HardPoint(backend.Env,500);
        using var runtime=Runtime(backend,rig);
        lock(runtime.ContextMutex)
        {
            Assert.Equal(1,runtime.ComputePath(start,new[]{rejected,target}));
            Assert.Equal(2,Owned(runtime).Count);var raw=Assert.Single(Context(runtime));
            Assert.Equal((byte)1,raw.Id);Assert.Equal(101.25f,raw.C.X);
            Move(target,700,1);
            // Changed environment after initial GoalIsValid: continuous101.25 is free but rounded100 collides.
            // Revalidating rounded persistent100 would fail; raw101.25 must RoundSafe to110.
            backend.Env.ClearObstacles();HardPoint(backend.Env,100,.25f);
        }
        Wait(runtime);
        lock(runtime.ContextMutex)
        {
            Assert.Equal(1,backend.ChosenGoalId);Assert.Equal((short)11,runtime.TotalPlan!.States().Last().X);
            Assert.True(runtime.GetCompletePath(start,PathMotionProfile.Default,out var path,out var goal));
            Assert.Equal(1,goal);Assert.Equal(.2f,Assert.IsType<PathSegment.PointTurn>(path.Last()).TargetAngleRad);
            Assert.Equal(101.25f,Assert.Single(Context(runtime)).C.X);
        }
    }

    [Fact]
    public void CapturedChargerPlanEndsWithinDriveToPoseTranslationAndYawTolerance()
    {
        // Skills capture 20261116-194237-M15-CUBE-HW-0cbdb7. Use the confirmed pre-dock
        // observation (stamp 134775) and the last pose before planning (frame 16770).
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        rig.State();
        var origin = rig.Engine.Robot!.GetNativeOrigin(rig.Engine.Robot.CurrentOriginId);
        using var chargerPose = new Pose3d(new Rotation3d(.99249953f, .008334444f, .0908289f, -.0813962f),
            new Vec3(200.08899f, -12.36417f, -.49952316f), parent: origin);
        using var goal = ChargerGeometry.PreDockPose(chargerPose);
        using var rootedGoal = new Pose3d(goal.Quaternion, goal.Translation, parent: origin);

        using var start = At(-19.9931f, .523519f, -.026178956f, origin);
        float yaw = (float)rootedGoal.AngleAroundZ;
        using var driveGoal = new Pose3d(rootedGoal.Quaternion,
            new Vec3((float)rootedGoal.Translation.X - 20 * MathF.Cos(yaw),
                (float)rootedGoal.Translation.Y - 20 * MathF.Sin(yaw), 0), parent: origin);
        Assert.Equal(-.16381736f, yaw, precision: 5);
        var displacement = driveGoal.Translation - start.Translation;
        Assert.InRange(displacement.X * displacement.X + displacement.Y * displacement.Y,
            2066.688, 2066.709);
        var backend = Backend();
        using var runtime = Runtime(backend, rig);
        Assert.Equal(1, runtime.ComputePath(start, new[]{driveGoal}));
        Wait(runtime);
        lock (runtime.ContextMutex)
        {
            var profile = PathMotionProfile.Default with
            {
                SpeedMmps = 167.584213f, AccelMmps2 = 82.6039047f, DecelMmps2 = 97.3960953f
            };
            Assert.True(runtime.GetCompletePath(start, profile, out var path, out byte picked));
            Assert.Equal(0, picked);
            var turn = Assert.IsType<PathSegment.PointTurn>(path[^1]);
            var end = NativePlanDependencies.GetEndPose(path[^1]);
            var beforeTurn = NativePlanDependencies.GetEndPose(path[^2]);
            Assert.Equal((beforeTurn.X, beforeTurn.Y), (end.X, end.Y));
            var latticeEnd = runtime.TotalPlan!.States().Last();
            Assert.InRange(Math.Abs(end.X - latticeEnd.X * 10), 0, .001);
            Assert.InRange(Math.Abs(end.Y - latticeEnd.Y * 10), 0, .001);
            Assert.Equal(yaw, turn.TargetAngleRad);

            // Invert PathComponent's -20 mm planner offset before comparing in the main-pose frame.
            using var predictedBody = At(end.X + 20 * MathF.Cos(end.Theta), end.Y + 20 * MathF.Sin(end.Theta),
                end.Theta, origin);
            CubePreActionPoses.DistanceThresholdMm(rootedGoal, chargerPose, DriveToPoseAction.GoalAngleToleranceRad,
                out double xTolerance, out double yTolerance);
            Assert.InRange(Math.Abs(predictedBody.Translation.X - rootedGoal.Translation.X), 0, xTolerance);
            Assert.InRange(Math.Abs(predictedBody.Translation.Y - rootedGoal.Translation.Y), 0, yTolerance);
            Assert.InRange(Math.Abs(predictedBody.AngleAroundZ - rootedGoal.AngleAroundZ), 0,
                DriveToPoseAction.GoalAngleToleranceRad);
            using var manipulation = new ManipulationSystem(rig.Robot, rig.Robot.Vision);
            using var drive = NativeDriveTestGoals.To(manipulation, rootedGoal);
            drive.PreActionObjectPose = new Pose3d(chargerPose);
            float height = manipulation.NativeHeight(rig.Engine.Robot);
            // The charger observation was tilted. Pose3d::IsSameAs compares full rotation,
            // even though the path can only change yaw. Compare with both planned and measured arrival.
            Assert.Equal(ActionResult.FailedTraversingPath, drive.ReadyState(rootedGoal, predictedBody, height, 3, 3));
            using var measuredBody = At(46.298416f, 18.426844f, -.16211045f, origin); // frame 17238
            Assert.InRange(Math.Abs(measuredBody.Translation.X - rootedGoal.Translation.X), 0, xTolerance);
            Assert.InRange(Math.Abs(measuredBody.Translation.Y - rootedGoal.Translation.Y), 0, yTolerance);
            Assert.Equal(ActionResult.FailedTraversingPath, drive.ReadyState(rootedGoal, measuredBody, height, 3, 3));
            using var uprightGoal = new Pose3d(predictedBody.Quaternion, rootedGoal.Translation, parent: origin);
            Assert.Equal(ActionResult.Success, drive.ReadyState(uprightGoal, predictedBody, height, 3, 3));
        }
    }

    [Fact]
    public void CapturedChargerMarkerKeepsNativeTiltThroughPreDockArrival()
    {
        // Shipped ARM detector replay, image 134775 of 20261116-194237-M15-CUBE-HW-0cbdb7.
        var corners = new Vec2[]
        {
            new(149.5237274f, 65.2415924f), new(148.9995117f, 85.9845810f),
            new(177.6782532f, 65.5837631f), new(176.8351593f, 86.5737610f),
        };
        // Captured NV calibration. Robot::SetCameraCalibration clears distortion for this body (0x0065AD54..0x0065AD9C).
        var calibration = new CameraCalibration
        {
            FocalLengthX = 294.7164306640625, FocalLengthY = 294.89935302734375,
            CenterX = 143.1629180908203, CenterY = 117.45443725585938, Rows = 240, Columns = 320,
        };
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        rig.State();
        var robot = rig.Engine.Robot!;
        var origin = robot.GetNativeOrigin(robot.CurrentOriginId);
        robot.PoseHistory.Clear();
        using var initialPose = At(0, 0, parent: origin);
        robot.SetPose(initialPose);
        Assert.Equal(0u, robot.AddIncomingStateToHistory(Sample(134770, -.0261034966f, .0311201494f)));
        Assert.Equal(0u, robot.AddIncomingStateToHistory(Sample(134800, -.0261121988f, .0311829373f)));
        using var atImage = new HistRobotState();
        Assert.Equal(0u, robot.PoseHistory.ComputeAndInsertKeyedState(134775, atImage, out uint resolved));
        Assert.Equal(134775u, resolved);
        Assert.Equal(-.1834026575f, atImage.State.HeadAngle);
        Assert.Equal(-.026104948f, atImage.Pose.AngleAroundZ, precision: 7);

        // Native interpolation and camera pose, including the parent chain flattened by 0x004DF628.
        using var cameraInBody = robot.CopyHistoricalCameraPose(atImage);
        using var cameraInWorld = cameraInBody.GetWithRespectToRoot();
        Assert.InRange((cameraInBody.Translation - new Vec3(2.7671585, 0, 37.9389381)).Length, 0, .0001);
        Assert.InRange((cameraInWorld.Translation - new Vec3(2.7662158, -.0722283, 37.9389381)).Length, 0, .0001);
        Assert.True(cameraInWorld.Quaternion.IsNearlyEqual(
            new Rotation3d(.4271650673, -.5517961913, .5663921495, -.4384642535), .000001f));
        var camera = new CameraModel(calibration, cameraInWorld);
        var marker = Assert.Single(ChargerGeometry.Markers);
        var estimate = Assert.IsType<PoseEstimation.Result>(PoseEstimation.Solve(camera, marker.CornersOnObject(), corners));
        using var objectInCamera = estimate.ObjectInCamera;
        using var objectInWorld = cameraInWorld.Compose(objectInCamera);

        // Compare with the shipped ARM PnP output at 0x0085D77E..0x0085D8E2 composed with the capture's camera pose.
        Assert.InRange(objectInWorld.Translation.X, 200.079, 200.100);
        Assert.InRange(objectInWorld.Translation.Y, -12.377, -12.356);
        Assert.InRange(objectInWorld.Translation.Z, -.515, -.494);
        double tiltDeg = Math.Acos(objectInWorld.Rotation[2, 2]) * 180 / Math.PI;
        Assert.InRange(tiltDeg, 10.44, 10.49);

        // Creation's 5 degree gate (0x00625548..0x00625578) retains this rotation.
        using var placed = BlockWorld.ClampPoseToFlat(objectInWorld, BlockWorld.CreationFlatClampAngleRad);
        Assert.Equal(objectInWorld.Quaternion, placed.Quaternion);
        using var chargerPose = new Pose3d(placed.Quaternion, placed.Translation, parent: origin);
        using var goal = ChargerGeometry.PreDockPose(chargerPose);
        using var rootedGoal = new Pose3d(goal.Quaternion, goal.Translation, parent: origin);
        Assert.InRange(rootedGoal.Translation.X, 42.10, 42.15);
        Assert.InRange(rootedGoal.Translation.Y, 13.64, 13.70);
        Assert.InRange(rootedGoal.Translation.Z, 35.62, 35.73);
        using var arrival = At(46.298416f, 18.426844f, -.16211045f, origin); // frame 17238
        using var manipulation = new ManipulationSystem(rig.Robot, rig.Robot.Vision);
        using var drive = NativeDriveTestGoals.To(manipulation, rootedGoal);
        drive.PreActionObjectPose = new Pose3d(chargerPose);
        float height = manipulation.NativeHeight(robot);

        Assert.Equal(ActionResult.FailedTraversingPath, drive.ReadyState(rootedGoal, arrival, height, 3, 3));
        using var uprightGoal = new Pose3d(
            Rotation3d.FromAngleAxis((float)rootedGoal.AngleAroundZ, new Vec3(0, 0, 1)),
            rootedGoal.Translation, parent: origin);
        Assert.Equal(ActionResult.Success, drive.ReadyState(uprightGoal, arrival, height, 3, 3));

        RobotState Sample(uint timestamp, float yaw, float pitch) => new()
        {
            Timestamp = timestamp,
            PoseOriginId = robot.CurrentOriginId,
            PoseFrameId = robot.PoseFrameId,
            Pose = new RobotPose { Angle = yaw, Pitch = pitch },
            HeadAngle = -.1834026575f,
            LiftAngle = -.1800776124f,
        };
    }

    [Fact]
    public void ReplanSnapshotsNewStartAndReusesAdmittedRawGoalsWithoutRecapturingWrappers()
    {
        // reach: unit (direct runtime replan; substituted input transforms, actual worker and search).
        // P4/T2: no goal recopy; refreshed start is numeric before the replan worker is signalled.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        using var start=At(0);using var target=At(200);using var movedStart=At(100,100);
        var backend=Backend();using var runtime=Runtime(backend,rig);
        Assert.Equal(1,runtime.ComputePath(start,new[]{target}));Wait(runtime);
        lock(runtime.ContextMutex)
        {
            Move(target,500);
            Assert.Equal(1,runtime.ComputeNewPathIfNeeded(movedStart,false));
            Move(movedStart,400);
        }
        Wait(runtime);
        Assert.Equal(new LatticeState(10,10,0),runtime.TotalPlan!.Start);
        Assert.Equal((short)20,runtime.TotalPlan.States().Last().X);
    }

    [Fact]
    public void UnforcedReplanSelectsRetainedPrefixEndpointBeforeWorkerSignal()
    {
        // reach: unit (missing map and injected hard C-space cell; real retained-prefix dispatch and worker).
        // P4 0x004FEED6/DA selects the prefix endpoint as scalar context, not the supplied start wrapper.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.Robot.Vision.World.Maps.CurrentOrigin=uint.MaxValue;
        using var start=At(0);using var target=At(200);using var current=At(0);
        var backend=Backend();using var runtime=Runtime(backend,rig);
        Assert.Equal(1,runtime.ComputePath(start,new[]{target}));Wait(runtime);
        lock(runtime.ContextMutex)
        {
            HardPoint(backend.Env,150);
            Assert.Equal(1,runtime.ComputeNewPathIfNeeded(current,false));
            var retained=runtime.TotalPlan!;Assert.NotEmpty(retained.Actions);
            var endpoint=retained.States().Last();var captured=Field<StateC>(runtime,"_start");
            Assert.Equal(backend.Env.Primitives.ResolutionMm*endpoint.X,captured.X);
            Assert.Equal(backend.Env.Primitives.ResolutionMm*endpoint.Y,captured.Y);
            Assert.Equal(backend.Env.Primitives.Angles[endpoint.Theta],captured.Theta);
            Assert.NotEqual(0,captured.X);Move(current,1000);
        }
        Wait(runtime);Assert.Equal((short)20,runtime.TotalPlan!.States().Last().X);
    }

    [Fact]
    public void MismatchingRetainedPrefixLogsBothPlansAndPreservesStatusWrites()
    {
        using var rig = new M4ControlTests.Rig();
        using var start = At(0);
        using var goal = At(200);
        var backend = Backend();
        var logs = new List<(EngineLogLevel Level, string Key, string Text)>();
        using var runtime = Runtime(backend, rig, (level, _, key, text) => logs.Add((level, key, text)));
        var statuses = new List<int>();
        runtime.StatusPublished += statuses.Add;
        var prims = backend.Env.Primitives;
        var action = prims.ByAngle[0].First(p => p.EndX > 0 && p.EndY == 0 && p.EndTheta == 0);
        var prefix = new LatticePlan(new(10, 0, 0), [action], 0, 0) { Penalties = [0.25f] };
        bool priorError = Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            Cozmo.Transport.EngineErrorState.ErrorFlagSet = false;
            lock (runtime.ContextMutex)
            {
                Assert.Equal(1, runtime.ComputePath(start, [goal]));
                // Supply a mismatching retained prefix before the real asynchronous search consumes the start.
                typeof(NativeLatticePlannerRuntime).GetField("_total", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(runtime, prefix);
            }
            Wait(runtime);
            var log = Assert.Single(logs, line => line.Key == "LatticePlanner.PlanMismatch");
            Assert.Equal(EngineLogLevel.Error, log.Level);
            Assert.Equal($"trying to append a plan with a mismatching state!\nendState = [{10 + action.EndX},0,0]\nnext plan start = [0,0,0]\n",
                log.Text);
            Assert.Equal(new[] { "plan start: [10,0,0]", "plan start: [0,0,0]", "plan start: [10,0,0]", "plan start: [0,0,0]" },
                logs.Where(line => line.Key == "xythetaEnvironment.PrintPlan" && line.Text.StartsWith("plan start:")).Select(line => line.Text));
            Assert.Contains(logs, line => line is (EngineLogLevel.Debug, "xythetaEnvironment.PrintPlan", _) &&
                line.Text == $" 0: (100.000000, 0.000000, 0.000000 [0]) --> {prims.Actions[action.ActionIndex].Name} (penalty = 0.250000)");
            Assert.Equal(new[] { 1, 1, 0, 2 }, statuses);
            Assert.Equal(prefix.Start, runtime.TotalPlan!.Start);
            Assert.False(Cozmo.Transport.EngineErrorState.ErrorFlagSet);
        }
        finally { Cozmo.Transport.EngineErrorState.ErrorFlagSet = priorError; }
    }

    [Fact]
    public void PendingAndRunningRequestsRejectBeforeReplacingOwnedInputs()
    {
        // reach: unit (actual mutex and a deterministic worker observation barrier; direct runtime requests).
        // P1: pending/F3 failed-lock gates reject before replacement. Barrier released before join on every exit.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        using var start=At(0);using var first=At(200);using var rejected=At(500);
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var backend=Backend();backend.PlanningStarted+=()=>{entered.Set();release.Wait();};
        using var runtime=Runtime(backend,rig);
        try
        {
            lock(runtime.ContextMutex)
            {
                Assert.Equal(1,runtime.ComputePath(start,new[]{first}));
                Assert.Equal(0,runtime.ComputePath(start,new[]{rejected}));
            }
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));Assert.True(runtime.F3);
            Assert.Equal(0,runtime.ComputePath(start,new[]{rejected}));
            release.Set();Wait(runtime);
            Assert.Equal((short)20,runtime.TotalPlan!.States().Last().X);
        }
        finally{release.Set();}
    }

    [Fact]
    public void PlannerRunModeMessageSwitchesTheNextPlanBetweenCallerAndWorker()
    {
        using var rig = new M4ControlTests.Rig();
        rig.ToSynced();
        using var start = At(0);
        using var goal = At(200);
        var modes = new List<string>();
        using var runtime = new NativeLatticePlannerRuntime(Backend(), rig.Robot.Vision.World, () => null,
            (_, channel, key, text) => { if (channel == "Planner" && key == "LatticePlanner.IsSync") modes.Add(text); },
            _ => { }, rig.Engine);

        rig.Engine.AnimationMessages.Deliver(0x7B, false, new byte[] { 1 });
        Assert.Equal(1, runtime.ComputePath(start, new[] { goal }));
        Assert.Equal(2, runtime.CheckPlanningStatus);
        Assert.False(runtime.F0);

        rig.Engine.AnimationMessages.Deliver(0x7B, false, new byte[] { 0 });
        lock (runtime.ContextMutex)
        {
            Assert.Equal(1, runtime.ComputePath(start, new[] { goal }));
            Assert.True(runtime.F0);
        }
        Wait(runtime);
        Assert.Equal(new[] { "true", "false" }, modes);
    }

    [Fact]
    public void FullCopiesShareParentsAndReplacementAssignsAppendsShrinksAndSkipsSelfVector()
    {
        // reach: unit (substitutes parented inputs and synchronous mode, real ComputePath/pose lifetimes).
        // P2/T3: separate nodes with shared parent; common prefix Assign, append copy, reverse shrink; self skip.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        using var parent=At(0);using var start=At(0);using var a=At(200,parent:parent);using var b=At(300,parent:parent);
        using var c=At(400,parent:parent);using var d=At(500,parent:parent);
        var backend=Backend();using var runtime=Runtime(backend,rig);runtime.F5=true;
        Assert.Equal(1,runtime.ComputePath(start,new[]{a,b,c}));var initial=Owned(runtime).ToArray();
        Assert.NotSame(a.Node,initial[0].Node);Assert.Same(parent.Node,initial[0].Node!.Parent);
        Assert.Equal(1,runtime.ComputePath(start,new[]{b}));
        Assert.Same(initial[0],Owned(runtime)[0]);Assert.Equal(300,Owned(runtime)[0].Translation.X);
        Assert.Null(initial[1].Node);Assert.Null(initial[2].Node);
        Assert.Equal(1,runtime.ComputePath(start,new[]{a,d}));var expanded=Owned(runtime).ToArray();
        Assert.Same(initial[0],expanded[0]);Assert.NotSame(d.Node,expanded[1].Node);
        Assert.Equal(1,runtime.ComputePath(start,Owned(runtime)));Assert.Same(expanded[0],Owned(runtime)[0]);Assert.Same(expanded[1],Owned(runtime)[1]);
        Assert.Equal((short)20,runtime.TotalPlan!.States().Last().X);
        Assert.Equal(1,runtime.ComputePath(start,new[]{a,b,c,d})); // exceeds retained capacity3: reverse-release then exact-count allocation
        Assert.All(expanded,p=>Assert.Null(p.Node));Assert.Equal(4,Owned(runtime).Count);
        Assert.NotSame(a.Node,Owned(runtime)[0].Node);Assert.Equal((short)20,runtime.TotalPlan!.States().Last().X);
    }

    [Fact]
    public void AllInvalidAcceptedRequestReplacesFullVectorAndClearsNumericContextAndOldPlan()
    {
        // reach: unit (missing-map/hard-environment substitution; actual admitted runtime replacement timing P2).
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.Robot.Vision.World.Maps.CurrentOrigin=uint.MaxValue;
        using var start=At(0);using var first=At(200);using var invalid=At(500);
        var backend=Backend();using var runtime=Runtime(backend,rig);runtime.F5=true;
        Assert.Equal(1,runtime.ComputePath(start,new[]{first}));Assert.NotEmpty(runtime.TotalPlan!.Actions);
        HardPoint(backend.Env,500);
        Assert.Equal(0,runtime.ComputePath(start,new[]{invalid}));
        Assert.Equal(500,Assert.Single(Owned(runtime)).Translation.X);Assert.Empty(Context(runtime));Assert.Empty(runtime.TotalPlan!.Actions);
    }

    [Fact]
    public void StopKeepsOwnedInputsAndTeardownJoinsBeforeReleasingThem()
    {
        // reach: unit (direct runtime, deterministic pending work under actual mutex; observes join log, not a mock join).
        // T1/T5: Stop does not release; joined0x00501954 precedes full-vector release0x005019D8/DC.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();using var start=At(0);using var target=At(200);
        var backend=Backend();IReadOnlyList<Pose3d>? owned=null;var observations=new ConcurrentQueue<bool>();
        using var runtime=Runtime(backend,rig,(_,_,key,_)=>{if(key=="LatticePlanner.DestroyThread.Joined")observations.Enqueue(owned is not null&&owned.All(p=>p.Node is not null));});
        lock(runtime.ContextMutex)
        {
            Assert.Equal(1,runtime.ComputePath(start,new[]{target}));owned=Owned(runtime).ToArray();runtime.StopPlanning();
            Assert.True(runtime.F0);Assert.All(owned,p=>Assert.NotNull(p.Node));
        }
        runtime.Dispose();Assert.True(Assert.Single(observations));Assert.All(owned,p=>Assert.Null(p.Node));
    }
}
