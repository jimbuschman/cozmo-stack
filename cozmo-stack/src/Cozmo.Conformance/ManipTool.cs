using System.Net;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Conformance;

/// <summary>
/// M12 hardware acceptance: <c>manip &lt;robot-ip&gt; --driveto | --pickup | --putdown | --roll | --stack [--seconds 120] [--nominal] [--acceptance [file]]</c>.
/// Reads the calibration, streams the camera, waits for a located cube, then runs the requested action or
/// behaviour with every message and decision printed. <c>manip --plan x y angleDeg</c> prints the path segments
/// and wire messages the planner would send from the origin, offline.
/// </summary>
public static class ManipTool
{
    public static async Task<int> Run(string[] a)
    {
        if (a.Length < 2) { Console.WriteLine(Usage); return 1; }
        if (a[1] == "--plan") return Plan(a);
        string mode = a.Skip(2).FirstOrDefault(x => x is "--driveto" or "--pickup" or "--putdown" or "--roll" or "--stack" or "--flip" or "--knockover" or "--wheelie" or "--mount" or "--driveoff") ?? "--driveto";
        string? obb = Arg(a, "--obb");
        int seconds = int.TryParse(Arg(a, "--seconds"), out var s) ? s : 120;
        string? acceptance = AcceptancePath(a);
        Console.WriteLine($"connecting to {a[1]}...");
        using var robot = await CozmoRobot.ConnectAsync(IPAddress.Parse(a[1]),engineOptions:EngineStartupResources.Options(a));
        var vision = robot.Vision;
        using var m = new ManipulationSystem(robot, vision);
        var log = new DiagnosticJournal<string>();
        void Say(string line) => log.Write(line, line);
        if (obb is not null)
        {
            Say(m.LoadPlanner(obb) ? "lattice planner loaded from cozmo_mprim.json (M13)" : "no cozmo_mprim.json under --obb: straight-line planner");
            m.Workouts = WorkoutComponent.FromObb(obb);
        }
        else Say("no --obb: straight-line planner (LOCAL); pass --obb <dir> for the engine's lattice planner");
        // The behaviour commands are animation-driven: SteppedBehavior.IsRunnable refuses without a library,
        // so without the assets they could only ever report "not runnable".
        if (obb is not null && TriggersTool.FindAssetsRoot(obb) is { } assets)
        {
            robot.Animations.LoadFrom(assets);
            Say($"animation library loaded from {assets}");
        }
        vision.Log += l => Say("  vision: " + l);
        vision.World.Log += l => Say("  world: " + l);
        m.Log += l => Say("  manip: " + l);
        m.Follower.Event += (id, t) => Say($"  path {id}: {t}");
        robot.Message += msg => { if (msg is PickAndPlaceResult or DockingStatus or MovingLiftPostDock or PathFollowingEvent) Say($"  <- {msg}"); };

        var cal = await vision.WaitForConnectionCalibrationAsync(TimeSpan.FromSeconds(3));
        Say(cal is null ? "camera calibration: NOT READ from NV storage" : $"camera calibration from robot: {cal}");
        if (cal is null && a.Contains("--nominal")) { vision.Calibration = CameraCalibration.Nominal(); Say("using the nominal stand-in calibration (LOCAL_POLICY)"); }
        if (vision.Calibration is null) { Say("no calibration: cannot localise a cube, stopping"); robot.Disconnect(); return 2; }
        await robot.WaitForMotorCalibrationAsync(TimeSpan.FromSeconds(10));
        robot.StartCamera();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        string outcome;
        bool succeeded;
        if (mode == "--driveoff")
        {
            Say($"on charger: {robot.Sensors.OnCharger}");
            var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap() };
            var b = new DriveOffChargerBehavior(m, "DriveOffCharger", 60);
            b.Step += l => Say("  driveoff: " + l);
            bool startedOnCharger = robot.Sensors.OnCharger;
            await RunBehavior(b, ctx, cts.Token, "on the charger");
            succeeded = startedOnCharger && b.DriveResult == ActionResult.Success && b.LeftChargerOnTreads;
            outcome = $"DriveOffCharger success={(succeeded ? "yes" : "no")}; "
                    + $"startedOnCharger={startedOnCharger}; action={b.DriveResult}; onTreadsAndOffCharger={b.LeftChargerOnTreads}; on charger: {robot.Sensors.OnCharger}";
            return Finish(outcome, "he drives forward off the charger about 156 mm at 20 mm/s and stops on his treads; IS_ON_CHARGER clears", mode, succeeded);
        }
        if (mode == "--mount")
        {
            Say("waiting for a located charger (show Cozmo the charger's back-wall marker)...");
            var deadlineC = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadlineC && vision.World.GetLocatedObjectById(ChargerGeometry.ObjectId) is null) await Task.Delay(200);
            var charger = vision.World.GetLocatedObjectById(ChargerGeometry.ObjectId);
            if (charger is null) { Say("no charger located in 30 s"); robot.StopCamera(); robot.Disconnect(); return 2; }
            Say($"charger: {charger}; docked robot pose {ChargerGeometry.DockedRobotPose(charger.Pose)}");
            var act = new MountChargerAction(m, ChargerGeometry.ObjectId);
            var r = await act.RunAsync(cts.Token);
            foreach (var l in act.Trace) Say("  " + l);
            succeeded = r == ActionResult.Success && robot.Sensors.OnCharger;
            outcome = $"MountCharger success={(succeeded ? "yes" : "no")}; action={r}; attempts={act.Attempts}; on charger: {robot.Sensors.OnCharger}";
            return Finish(outcome, "he aligns 120 mm in front of the charger's marker, turns around, backs onto the charger and the contacts report; a miss drives forward 120 mm and retries", mode, succeeded);
        }

        // What each command actually needs before it can do anything, rather than "one cube and hope".
        int cubesNeeded = mode switch { "--stack" => 2, "--knockover" => 2, _ => 1 };
        bool needsAnimations = mode is "--stack" or "--knockover" or "--wheelie" or "--putdown";
        bool needsStack = mode == "--knockover";
        Say($"waiting for {cubesNeeded} located cube(s) (show Cozmo the connected cube(s))...");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && vision.World.LocatedObjects.Count(o => CubeGeometry.IsCube(o.Type)) < cubesNeeded) await Task.Delay(200);
        var cubes = vision.World.LocatedObjects.Where(o => CubeGeometry.IsCube(o.Type)).ToList();
        var cube = cubes.FirstOrDefault();
        if (cube is null || cubes.Count < cubesNeeded)
        {
            Say($"{mode} needs {cubesNeeded} located cube(s); {cubes.Count} located in 30 s. Nothing to run.");
            robot.StopCamera(); robot.Disconnect(); return 2;
        }
        Say($"target: {cube}" + (cubes.Count > 1 ? $" (+{cubes.Count - 1} more located)" : ""));
        if (needsAnimations && robot.Animations.Library is null)
        {
            Say($"{mode} runs an animation-driven behaviour and no animation library is loaded: pass --obb <dir> with cozmo_resources/assets. Nothing to run.");
            robot.StopCamera(); robot.Disconnect(); return 2;
        }
        if (needsStack)
        {
            m.Configurations.Update();
            if (m.Configurations.Stacks.Count == 0)
            {
                Say($"{mode} needs a located stack of {cubesNeeded}; the world model has {cubes.Count} cube(s) and no stack. Build the stack in view and re-run.");
                robot.StopCamera(); robot.Disconnect(); return 2;
            }
        }

        // RETIRE-LEGACY-DRIVE-API-1 (operator 2026-11-13): the drive modes run the native runners on the canonical robot's ActionList, and the
        // behaviour modes run the configured native behaviour through the Selection chooser's ExecuteBehaviorByID (0x0060AA58), the public input.
        robot.Engine.LogLine += l => { if (l.Contains("PathComponent.SelectPlanner", StringComparison.Ordinal) || l.Contains("robot.planner_selected", StringComparison.Ordinal)) Say("  engine: " + l); };
        // Observed only: every completed action (engine ActionCompleted) and every outbound EnableStopOnCliff on the wire (transport frame trace, as M5Hw reads it).
        var completed = new System.Collections.Concurrent.ConcurrentQueue<ActionCompletionReport>();
        robot.Engine.ActionCompleted += report => { completed.Enqueue(report); Say($"  action completed: tag {report.Tag} type 0x{report.ActionType:X} result 0x{report.Result:X8}"); };
        var cliffSends = new System.Collections.Concurrent.ConcurrentQueue<bool>();
        robot.Transport.FrameTrace += frame =>
        {
            if (!frame.Outbound || frame.Frame is not { } f) return;
            foreach (var message in f.Messages)
                if (message.Payload.Length >= 2 && message.Payload[0] == (byte)RobotMessageId.EnableStopOnCliff)
                {
                    bool enable = EnableStopOnCliff.Read(new CladReader(message.Payload.AsMemory(1))).Enable;
                    cliffSends.Enqueue(enable); Say($"  -> EnableStopOnCliff({(enable ? "true" : "false")}) on the wire");
                }
        };
        string Completions(int from) => string.Join(", ", completed.Skip(from).Select(c => $"type 0x{c.ActionType:X}=0x{c.Result:X8}"));
        switch (mode)
        {
            case "--flip":
            {
                // NativeDriveAndFlipRunner (K15-K19): the 9-arg IDriveToInteractWithObject compound; the face-turn maximum is Radians 0, as the legacy tool's default.
                var f = new NativeDriveAndFlipRunner(m, cube.ObjectId, 0f);
                var r = await RunOnActionList(f, cts.Token);
                succeeded = r == ActionResult.Success;
                outcome = $"DriveAndFlipBlock success={(succeeded ? "yes" : "no")}; action={r}; flip result={(ActionResult)f.Flip.State}; cube now {vision.World.GetObjectById(cube.ObjectId)?.PoseState}";
                break;
            }
            case "--wheelie":
            {
                // Configured NativePopAWheelieBehavior ("PopAWheelie"). Every signal below is observed: the engine's PoppedWheelie objective broadcast (22, tag 0x8B),
                // and the outbound EnableStopOnCliff messages on the wire (false while it rears, true afterwards).
                bool popped = false; int actionsBefore = completed.Count, cliffBefore = cliffSends.Count;
                using var objective = robot.Engine.AnimationMessages.SubscribeMessage(0x8B, true, p => { if (p is uint id && id == (uint)BehaviorObjective.PoppedWheelie) popped = true; });
                string detail = await RunConfigured("PopAWheelie", cts.Token, null);
                var sends = cliffSends.Skip(cliffBefore).ToList();
                int disabledAt = sends.IndexOf(false);
                bool cliffStopRestored = disabledAt >= 0 && sends.Skip(disabledAt + 1).Contains(true);
                succeeded = popped && cliffStopRestored;
                outcome = $"PopAWheelie success={(succeeded ? "yes" : "no")}; PoppedWheelie={popped}; "
                        + $"cliff stop restored={(cliffStopRestored ? "yes" : "no")}; EnableStopOnCliff sends observed: [{string.Join(", ", sends.Select(e => e ? "EnableStopOnCliff(true)" : "EnableStopOnCliff(false)"))}]; "
                        + $"actions completed: [{Completions(actionsBefore)}]; {detail}";
                break;
            }
            case "--knockover":
            {
                // Configured NativeKnockOverCubesBehavior ("SparksKnockOverCubes", minimumStackHeight 2 in its shipped config).
                // Judged signal: the engine's KnockedOverBlocks objective broadcast (13, tag 0x8B). M13-014 PlayingReaction 0x005C3950..0x005C395C calls
                // BehaviorObjectiveAchieved(13, true) only when the tipped set is non-empty; M7-019 broadcasts it (0x005BE22E..0x005BE246, tag 0x007285FE).
                // Everything else printed here is an observation, not judged.
                Say($"observation: stacks: {string.Join("; ", m.Configurations.Stacks.Select(s => string.Join("/", s.BlockIds)))}");
                bool knockedOver = false; int actionsBefore = completed.Count;
                var needsActions = new System.Collections.Concurrent.ConcurrentQueue<int>();
                using var objective = robot.Engine.AnimationMessages.SubscribeMessage(0x8B, true, p => { if (p is uint id && id == (uint)BehaviorObjective.KnockedOverBlocks) knockedOver = true; });
                using var needs = robot.Engine.AnimationMessages.SubscribeMessage(0x62, true, p => { if (p is NativeNeedsStateReport n) needsActions.Enqueue(n.Action); });
                string detail = await RunConfigured("SparksKnockOverCubes", cts.Token, null);
                var native = robot.Engine.Robot?.NativeBehaviorRuntime?.Bound.GetValueOrDefault("SparksKnockOverCubes") as NativeKnockOverCubesBehavior;
                succeeded = knockedOver;
                outcome = $"KnockedOverBlocks objective observed={(knockedOver ? "yes" : "no")}; KnockOver: {detail}; observation: actions completed: [{Completions(actionsBefore)}]; "
                        + $"observation: blocks whose up axis changed during the run = [{string.Join(",", native?.Tipped ?? (IReadOnlyCollection<uint>)Array.Empty<uint>())}], attempts={native?.Attempts}; "
                        + $"observation: NeedsState (0x62) actions = [{string.Join(",", needsActions)}]";
                break;
            }
            case "--driveto":
            {
                int sentBeforeDrive = m.Paths.Sent.Count;
                // NativeDriveToObjectRunner (D1..D9): DriveToObjectAction(Docking), DriveToPoseAction(head true, manual +0x8C) to ALL the pre-action poses, then the turn.
                var d = new NativeDriveToObjectRunner(m, cube.ObjectId);
                var r = await RunOnActionList(d, cts.Token);
                foreach (var l in d.Action.Trace) Say("  " + l);
                var sentByDrive = m.Paths.Sent.Skip(sentBeforeDrive).ToList();
                int lines = sentByDrive.OfType<AppendPathSegmentLine>().Count();
                int arcs = sentByDrive.OfType<AppendPathSegmentArc>().Count();
                int pointTurns = sentByDrive.OfType<AppendPathSegmentPointTurn>().Count();
                var planners = m.GetNativePlanners();
                if (planners.Lattice is { } lattice && ReferenceEquals(planners.Current, lattice))
                    Say($"lattice plan: planner '{lattice.Name}', {lattice.Backend.Env.ObstacleCount} obstacle(s)");
                Say($"path segments sent: lines={lines} arcs={arcs} pointTurns={pointTurns}");
                succeeded = r == ActionResult.Success;
                outcome = $"DriveToObject success={(succeeded ? "yes" : "no")}; action={r}; chosen pre-dock goal index {d.Drive?.GoalIndex.ToString() ?? "none (already in position)"}";
                break;
            }
            case "--pickup":
            {
                // DriveToPickupObjectAction (NativeDriveToPickupBeaconRunner, 0x0055C464..548): drive to the Docking pose, then PickupObjectAction. One action; the
                // PickupBlockHelper retries (M12-009) belong to the behaviours that own the helper.
                var p = new NativeDriveToPickupBeaconRunner(m, cube.ObjectId);
                var r = await RunOnActionList(p, cts.Token);
                succeeded = r == ActionResult.Success && m.Docking.Carrying.IsCarrying(cube.ObjectId);
                outcome = $"Pickup success={(succeeded ? "yes" : "no")}; action={r}; carrying={m.Docking.Carrying.CarriedObjectId}; error signals sent={m.Docking.ErrorSignalsSent}";
                break;
            }
            case "--roll":
            {
                var before = cube.UpAxisFromPose();
                // DriveToRollObjectAction (NativeDriveToRollRunner, R9 0x0055C9E6..0x0055CB26). One action; the RollBlockHelper's three attempts (M12-016)
                // belong to the behaviours that own the helper.
                var roll = new NativeDriveToRollRunner(m, cube.ObjectId);
                var r = await RunOnActionList(roll, cts.Token);
                await Task.Delay(1500);
                var after = vision.World.GetObjectById(cube.ObjectId)?.UpAxisFromPose();
                bool axisChanged = after is not null && after.Value != before;
                succeeded = r == ActionResult.Success && axisChanged;
                outcome = $"Roll success={(succeeded ? "yes" : "no")}; action={r}; up axis {before} -> {after}; up axis changed={(axisChanged ? "yes" : "no")}";
                break;
            }
            case "--putdown":
            {
                if (!m.Docking.Carrying.IsCarryingObject) { m.Docking.Carrying.SetCarrying(cube.ObjectId); Say("assuming the cube is on the lift (place it there by hand first)"); }
                var r = await m.PlaceObjectOnGroundAsync(cts.Token);
                succeeded = r == ActionResult.Success && !m.Docking.Carrying.IsCarryingObject;
                outcome = $"PlaceObjectOnGround success={(succeeded ? "yes" : "no")}; action={r}; carrying={m.Docking.Carrying.IsCarryingObject}";
                break;
            }
            default:
            {
                // Configured NativeStackBlocksBehavior ("StackBlocks"). Judged signal: the engine's StackedBlock objective broadcast (40, tag 0x8B).
                // M15-076 PlayingFinalAnim 0x005C9EA2..0x005C9EA8 calls BehaviorObjectiveAchieved(40) first, reached only from the PlaceRel helper's success
                // callback (0x005C959C..0x005C95B4); M7-019 broadcasts it (0x005BE22E..0x005BE246, tag 0x007285FE). The behaviour's success never reads the world
                // model; carrying clears only through PlaceRel's own Verify (0x03000017 while carrying, 0x005555BA..0x005555E0). Both print as observations.
                bool stackedObjective = false; int actionsBefore = completed.Count;
                var needsActions = new System.Collections.Concurrent.ConcurrentQueue<int>();
                using var objective = robot.Engine.AnimationMessages.SubscribeMessage(0x8B, true, p => { if (p is uint id && id == (uint)BehaviorObjective.StackedBlock) stackedObjective = true; });
                using var needs = robot.Engine.AnimationMessages.SubscribeMessage(0x62, true, p => { if (p is NativeNeedsStateReport n) needsActions.Enqueue(n.Action); });
                string detail = await RunConfigured("StackBlocks", cts.Token, null);
                var native = robot.Engine.Robot?.NativeBehaviorRuntime?.Bound.GetValueOrDefault("StackBlocks") as NativeStackBlocksBehavior;
                uint top = native?.Top ?? uint.MaxValue, bottom = native?.Bottom ?? uint.MaxValue;
                m.Configurations.Update();
                bool stacked = m.Configurations.Stacks.Any(s => s.BlockIds.Contains(top) && s.BlockIds.Contains(bottom));
                succeeded = stackedObjective;
                outcome = $"StackedBlock objective observed={(stackedObjective ? "yes" : "no")}; StackBlocks: {detail}; observation: actions completed: [{Completions(actionsBefore)}]; "
                        + $"observation: carrying={m.Docking.Carrying.IsCarryingObject}; "
                        + $"observation: world model stack holding top {top} and bottom {bottom} = {(stacked ? "yes" : "no")}; "
                        + $"observation: NeedsState (0x62) actions = [{string.Join(",", needsActions)}]";
                break;
            }
        }
        return Finish(outcome, "the robot drove to the pre-dock pose in front of the cube's face, docked smoothly using the marker, and the lift/cube did what the action says; " +
                              "no path or dock message was rejected; the printed carrying state matches reality", mode, succeeded);

        int Finish(string result, string humanCheck, string what, bool success)
        {
            Say($"\n[{sw.Elapsed.TotalSeconds:F1}s] {result}");
            robot.StopCamera();
            if (acceptance is not null)
            {
                var record = WriteAcceptance("manip", acceptance, success, robot, humanCheck,
                    new { mode = what, outcome = result, messagesSent = m.Paths.Sent.Count + m.Docking.Sent.Count, log = log.Snapshot() }, what is "--flip" or "--knockover" or "--wheelie" or "--mount" or "--driveoff" ? "M13" : "M12");
                Console.WriteLine($"acceptance record: {record}");
            }
            robot.Disconnect();
            return success ? 0 : 3;
        }

        // Queues a native runner on the canonical robot's ActionList (as DriveToPoseAction.RunAsync did) and waits for its completion callback.
        async Task<ActionResult> RunOnActionList(ActionRunner runner, CancellationToken ct)
        {
            var owner = robot.Engine.Robot ?? throw new InvalidOperationException("MISSING: no native Robot owns an ActionList");
            var done = new TaskCompletionSource<ActionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            runner.AddCompletionCallback(result => { if (result != EngineActionResult.Running) done.TrySetResult((ActionResult)result); });
            owner.ActionList.QueueAction(QueueActionPosition.Now, runner, 0);
            using var cancellation = ct.Register(() => { owner.ActionList.Cancel(runner.Tag); done.TrySetResult(ActionResult.CancelledWhileRunning); });
            return await done.Task;
        }

        // Selects the configured native behaviour through the Selection chooser (ExecuteBehaviorByID, numRuns 1) and waits until it has run and stopped.
        async Task<string> RunConfigured(string id, CancellationToken ct, Action<IBehavior>? observe)
        {
            var owner = robot.Engine.Robot;
            if (owner?.NativeBehaviorRuntime?.Bound.GetValueOrDefault(id) is not { } behavior) return $"{id} is not bound by the native runtime";
            robot.ActivateHighLevelActivity("Selection");
            await Task.Delay(200, ct).ContinueWith(_ => { });
            robot.ExecuteBehaviorById((byte)Enum.Parse<BehaviorID>(id), 1);
            bool started = false;
            while (!ct.IsCancellationRequested)
            {
                bool current = robot.Engine.InvokeEngineRequest(() => ReferenceEquals(owner.BehaviorManagerOwner?.Current, behavior));
                if (current) { started = true; observe?.Invoke(behavior); }
                else if (started) break;
                await Task.Delay(33, ct).ContinueWith(_ => { });
            }
            return started ? $"{id} ran and stopped" : $"{id} was not started by the Selection chooser (not runnable)";
        }

        async Task<string> RunBehavior(SteppedBehavior b, BehaviorContext ctx, CancellationToken ct, string needs)
        {
            if (!b.IsRunnable(ctx)) return $"{b.Id} not runnable (needs {needs})";
            await b.StartAsync(ctx, new BehaviorScope(), ct);
            double t = 0;
            while (b.Update(ctx, t) && !ct.IsCancellationRequested) { await Task.Delay(33); t += 33; }
            b.Stop(ct.IsCancellationRequested ? BehaviorStopReason.Cancelled : BehaviorStopReason.Completed);
            return $"{b.Id} ended; last steps: {string.Join(" | ", b.Trace.TakeLast(3))}";
        }
    }

    private const string Usage = "manip <robot-ip> --driveto|--pickup|--putdown|--roll|--stack|--flip|--knockover|--wheelie|--mount|--driveoff [--obb <dir>] [--seconds 120] [--nominal] [--acceptance [file]]  |  manip --plan <x> <y> <angleDeg> [--obb <dir>] [--obstacle <x> <y> --carried-object-id none|<uint>]";

    private static int Plan(string[] a)
    {
        if (a.Length < 5) { Console.WriteLine(Usage); return 1; }
        var goal = new Pose3d(Mat3.AboutZ(double.Parse(a[4]) * Math.PI / 180), new Vec3(double.Parse(a[2]), double.Parse(a[3]), 0));
        IReadOnlyList<PathSegment> path;
        string? obb = Arg(a, "--obb");
        var prims = obb is null ? null : MotionPrimitiveSet.FromObb(obb);
        if (prims is not null)
        {
            var env = new LatticeEnvironment(prims);
            string? carryingInput = Arg(a, "--carried-object-id");
            bool explicitNone = string.Equals(carryingInput, "none", StringComparison.OrdinalIgnoreCase);
            if (a.Contains("--obstacle") && !explicitNone && !uint.TryParse(carryingInput, out _))
            { Console.WriteLine("--obstacle requires --carried-object-id none|<uint> for the standalone planner's carrying context"); return 1; }
            uint.TryParse(carryingInput, out uint rawCarriedObjectId);
            if (explicitNone) rawCarriedObjectId = uint.MaxValue;
            for (int i = 2; i < a.Length - 2; i++)
                if (a[i] == "--obstacle") env.AddRectangleObstacle(new Pose3d(Mat3.Identity, new Vec3(double.Parse(a[i + 1]), double.Parse(a[i + 2]), 0)), CubeGeometry.CubeSizeMm, CubeGeometry.CubeSizeMm, rawCarriedObjectId, "cube");
            var planner = new LatticePlanner(env);
            var res = planner.PlanTo(Pose3d.Identity, new[] { goal }, PathMotionProfile.Default);
            if (res is null) { Console.WriteLine("the lattice planner found no plan"); return 2; }
            Console.WriteLine($"lattice plan: {res.Value.Plan.Actions.Count} primitive(s), cost {res.Value.Plan.Cost:F0}, {res.Value.Plan.Expansions} expansions, {env.ObstacleCount} obstacle(s)");
            foreach (var s in res.Value.Plan.States()) Console.Write($" ({s.X},{s.Y},{s.Theta})");
            Console.WriteLine();
            path = res.Value.Path;
        }
        else path = StraightLinePlanner.Plan(Pose3d.Identity, goal);
        Console.WriteLine($"{path.Count} segment(s) from the origin to {goal}:");
        foreach (var s in path) Console.WriteLine("  " + s);
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var sender = new PathSender(robot);
        sender.Execute(path);
        foreach (var msg in sender.Sent) Console.WriteLine($"  -> {msg.Id} {Convert.ToHexString(msg.ToBytes())}");
        return 0;
    }

    private static string? Arg(string[] a, string name)
    {
        for (int i = 1; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }

    private static string? AcceptancePath(string[] a)
    {
        for (int i = 1; i < a.Length; i++)
            if (a[i] == "--acceptance") return i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[i + 1] : "";
        return null;
    }

    private static string WriteAcceptance(string tool, string path, bool automatedPass, CozmoRobot robot, string humanCheck, object detail, string milestone = "M12")
    {
        var file = Path.GetFullPath(string.IsNullOrEmpty(path) ? $"cozmo-acceptance-{tool}-{DateTime.Now:yyyyMMdd-HHmmss}.json" : path);
        var record = new
        {
            tool, milestone, timestampUtc = DateTime.UtcNow, firmware = robot.State.FirmwareVersionNumber, serial = robot.State.SerialNumber,
            automated = new { pass = automatedPass, detail },
            human = new { check = humanCheck, verdict = "PENDING - fill in after watching the robot" },
        };
        File.WriteAllText(file, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
        return file;
    }
}
