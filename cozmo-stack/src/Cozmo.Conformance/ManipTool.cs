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
        using var robot = await CozmoRobot.ConnectAsync(IPAddress.Parse(a[1]));
        using var vision = new VisionSystem(robot);
        using var m = new ManipulationSystem(robot, vision);
        var log = new List<string>();
        void Say(string line) { Console.WriteLine(line); log.Add(line); }
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

        var cal = await vision.ReadCalibrationAsync(TimeSpan.FromSeconds(3));
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

        switch (mode)
        {
            case "--flip":
            {
                var f = new DriveAndFlipBlockAction(m, cube.ObjectId);
                var r = await f.RunAsync(cts.Token);
                foreach (var l in f.Trace) Say("  " + l);
                succeeded = r == ActionResult.Success;
                outcome = $"DriveAndFlipBlock success={(succeeded ? "yes" : "no")}; action={r}; lift raised: {f.Flip?.LiftRaised}; cube now {vision.World.GetObjectById(cube.ObjectId)?.PoseState}";
                break;
            }
            case "--wheelie":
            {
                var ctx = new BehaviorContext { Robot = robot, Triggers = obb is null ? new AnimationTriggerMap() : AnimationTriggerMap.Load(obb) };
                var b = new PopAWheelieBehavior(m);
                b.Step += l => Say("  wheelie: " + l);
                string detail = await RunBehavior(b, ctx, cts.Token, "an upright located cube and the animation library");
                bool cliffStopRestored = b.Trace.Any(x => x.Contains("EnableStopOnCliff(true)", StringComparison.Ordinal));
                succeeded = b.Succeeded && cliffStopRestored;
                outcome = $"PopAWheelie success={(succeeded ? "yes" : "no")}; PoppedWheelie={b.Succeeded}; retries={b.Retries}; "
                        + $"cliff stop restored={(cliffStopRestored ? "yes" : "no")}; {detail}";
                break;
            }
            case "--knockover":
            {
                var ctx = new BehaviorContext { Robot = robot, Triggers = obb is null ? new AnimationTriggerMap() : AnimationTriggerMap.Load(obb) };
                var b = new KnockOverCubesBehavior(m, "SparksKnockOverCubes", 2);
                b.Step += l => Say("  knockover: " + l);
                Say($"stacks: {string.Join("; ", m.Configurations.Stacks.Select(s => string.Join("/", s.BlockIds)))}");
                string detail = await RunBehavior(b, ctx, cts.Token, "a located stack of two and the animation library");
                succeeded = b.KnockedOver == true;
                outcome = $"KnockOver success={(succeeded ? "yes" : "no")}; knockedOver={b.KnockedOver}; {detail}";
                break;
            }
            case "--driveto":
            {
                var d = new DriveToObjectAction(m, cube.ObjectId, PreActionType.Docking);
                var r = await d.RunAsync(cts.Token);
                foreach (var l in d.Trace) Say("  " + l);
                succeeded = r == ActionResult.Success;
                outcome = $"DriveToObject success={(succeeded ? "yes" : "no")}; action={r}; chosen pre-dock pose {d.Chosen}";
                break;
            }
            case "--pickup":
            {
                var h = new DockHelper(m);
                var r = await h.RunAsync(cube.ObjectId, PreActionType.Docking, () => new PickupObjectAction(m, cube.ObjectId), cts.Token);
                foreach (var l in h.Trace) Say("  " + l);
                succeeded = r == ActionResult.Success && m.Docking.Carrying.IsCarrying(cube.ObjectId);
                outcome = $"Pickup success={(succeeded ? "yes" : "no")}; action={r}; attempts={h.Attempts}; carrying={m.Docking.Carrying.CarriedObjectId}; error signals sent={m.Docking.ErrorSignalsSent}";
                break;
            }
            case "--roll":
            {
                var before = cube.UpAxisFromPose();
                var h = new DockHelper(m);
                var r = await h.RunAsync(cube.ObjectId, PreActionType.Rolling, () => new RollObjectAction(m, cube.ObjectId), cts.Token);
                foreach (var l in h.Trace) Say("  " + l);
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
                var r = await new PlaceObjectOnGroundAction(m).RunAsync(cts.Token);
                succeeded = r == ActionResult.Success && !m.Docking.Carrying.IsCarryingObject;
                outcome = $"PlaceObjectOnGround success={(succeeded ? "yes" : "no")}; action={r}; carrying={m.Docking.Carrying.IsCarryingObject}";
                break;
            }
            default:
            {
                var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap() };
                var b = new StackBlocksBehavior(m);
                b.Step += l => Say("  stack: " + l);
                if (!b.IsRunnable(ctx)) { succeeded = false; outcome = "StackBlocks success=no; not runnable (needs two located upright cubes and the animation library)"; break; }
                await b.StartAsync(ctx, new BehaviorScope(), cts.Token);
                double t = 0;
                while (b.Update(ctx, t) && !cts.IsCancellationRequested) { await Task.Delay(33); t += 33; }
                b.Stop(cts.IsCancellationRequested ? BehaviorStopReason.Cancelled : BehaviorStopReason.Completed);
                succeeded = b.StackedSuccessfully && !m.Docking.Carrying.IsCarryingObject;
                outcome = $"StackBlocks success={(succeeded ? "yes" : "no")}; ended in phase {b.CurrentPhase}; "
                        + $"carrying={m.Docking.Carrying.IsCarryingObject}; top={b.TopObjectId}; bottom={b.BottomObjectId}";
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
                    new { mode = what, outcome = result, messagesSent = m.Paths.Sent.Count + m.Docking.Sent.Count, log }, what is "--flip" or "--knockover" or "--wheelie" or "--mount" or "--driveoff" ? "M13" : "M12");
                Console.WriteLine($"acceptance record: {record}");
            }
            robot.Disconnect();
            return success ? 0 : 3;
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

    private const string Usage = "manip <robot-ip> --driveto|--pickup|--putdown|--roll|--stack|--flip|--knockover|--wheelie|--mount|--driveoff [--obb <dir>] [--seconds 120] [--nominal] [--acceptance [file]]  |  manip --plan <x> <y> <angleDeg> [--obb <dir>] [--obstacle <x> <y>]";

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
            for (int i = 2; i < a.Length - 2; i++)
                if (a[i] == "--obstacle") env.AddRectangleObstacle(new Pose3d(Mat3.Identity, new Vec3(double.Parse(a[i + 1]), double.Parse(a[i + 2]), 0)), CubeGeometry.CubeSizeMm, CubeGeometry.CubeSizeMm, "cube");
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
