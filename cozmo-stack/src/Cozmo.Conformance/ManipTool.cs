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
        if (a.Length < 2) { Console.WriteLine("manip <robot-ip> --driveto|--pickup|--putdown|--roll|--stack [--seconds 120] [--nominal] [--acceptance [file]]  |  manip --plan <x> <y> <angleDeg>"); return 1; }
        if (a[1] == "--plan") return Plan(a);
        string mode = a.Skip(2).FirstOrDefault(x => x is "--driveto" or "--pickup" or "--putdown" or "--roll" or "--stack") ?? "--driveto";
        int seconds = int.TryParse(Arg(a, "--seconds"), out var s) ? s : 120;
        string? acceptance = AcceptancePath(a);
        Console.WriteLine($"connecting to {a[1]}...");
        using var robot = await CozmoRobot.ConnectAsync(IPAddress.Parse(a[1]));
        using var vision = new VisionSystem(robot);
        using var m = new ManipulationSystem(robot, vision);
        var log = new List<string>();
        void Say(string line) { Console.WriteLine(line); log.Add(line); }
        vision.Log += l => Say("  vision: " + l);
        vision.World.Log += l => Say("  world: " + l);
        m.Log += l => Say("  manip: " + l);
        m.Follower.Event += (id, t) => Say($"  path {id}: {t}");
        robot.Message += msg => { if (msg is PickAndPlaceResult or DockingStatus or MovingLiftPostDock or PathFollowingEvent) Say($"  <- {msg}"); };

        var cal = await vision.ReadCalibrationAsync(TimeSpan.FromSeconds(3));
        Say(cal is null ? "camera calibration: NOT READ from NV storage" : $"camera calibration from robot: {cal}");
        if (cal is null && a.Contains("--nominal")) { vision.Calibration = CameraCalibration.Nominal(); Say("using the nominal stand-in calibration (LOCAL_POLICY)"); }
        if (vision.Calibration is null) { Say("no calibration: cannot localise a cube, stopping"); robot.Disconnect(); return 2; }
        robot.Cubes.SetDiscovery(true);
        await robot.WaitForMotorCalibrationAsync(TimeSpan.FromSeconds(10));
        robot.StartCamera();

        Say("waiting for a located cube (show Cozmo a connected cube)...");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && vision.World.LocatedObjects.Count == 0) await Task.Delay(200);
        var cube = vision.World.LocatedObjects.FirstOrDefault();
        if (cube is null) { Say("no cube located in 30 s"); robot.StopCamera(); robot.Disconnect(); return 2; }
        Say($"target: {cube}");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        string outcome;
        switch (mode)
        {
            case "--driveto":
            {
                var d = new DriveToObjectAction(m, cube.ObjectId, PreActionType.Docking);
                var r = await d.RunAsync(cts.Token);
                foreach (var l in d.Trace) Say("  " + l);
                outcome = $"DriveToObject -> {r}; chosen pre-dock pose {d.Chosen}";
                break;
            }
            case "--pickup":
            {
                var h = new DockHelper(m);
                var r = await h.RunAsync(cube.ObjectId, PreActionType.Docking, () => new PickupObjectAction(m, cube.ObjectId), cts.Token);
                foreach (var l in h.Trace) Say("  " + l);
                outcome = $"Pickup -> {r} after {h.Attempts} attempt(s); carrying={m.Docking.Carrying.CarriedObjectId}; error signals sent={m.Docking.ErrorSignalsSent}";
                break;
            }
            case "--roll":
            {
                var before = cube.UpAxisFromPose();
                var h = new DockHelper(m);
                var r = await h.RunAsync(cube.ObjectId, PreActionType.Rolling, () => new RollObjectAction(m, cube.ObjectId), cts.Token);
                foreach (var l in h.Trace) Say("  " + l);
                await Task.Delay(1500);
                outcome = $"Roll -> {r}; up axis {before} -> {vision.World.GetObjectById(cube.ObjectId)?.UpAxisFromPose()}";
                break;
            }
            case "--putdown":
            {
                if (!m.Docking.Carrying.IsCarryingObject) { m.Docking.Carrying.SetCarrying(cube.ObjectId); Say("assuming the cube is on the lift (place it there by hand first)"); }
                var r = await new PlaceObjectOnGroundAction(m).RunAsync(cts.Token);
                outcome = $"PlaceObjectOnGround -> {r}; carrying={m.Docking.Carrying.IsCarryingObject}";
                break;
            }
            default:
            {
                var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap() };
                var b = new StackBlocksBehavior(m);
                b.Step += l => Say("  stack: " + l);
                if (!b.IsRunnable(ctx)) { outcome = "StackBlocks not runnable (needs two located upright cubes and the animation library)"; break; }
                await b.StartAsync(ctx, new BehaviorScope(), cts.Token);
                double t = 0;
                while (b.Update(ctx, t) && !cts.IsCancellationRequested) { await Task.Delay(33); t += 33; }
                outcome = $"StackBlocks ended in phase {b.CurrentPhase}";
                break;
            }
        }
        Say($"\n[{sw.Elapsed.TotalSeconds:F1}s] {outcome}");
        robot.StopCamera();
        if (acceptance is not null)
        {
            var record = WriteAcceptance("manip", acceptance, !outcome.Contains("Abort") && !outcome.Contains("Timeout"), robot,
                "the robot drove to the pre-dock pose in front of the cube's face, docked smoothly using the marker, and the lift/cube did what the action says; " +
                "no path or dock message was rejected; the printed carrying state matches reality",
                new { mode, outcome, messagesSent = m.Paths.Sent.Count + m.Docking.Sent.Count, log });
            Console.WriteLine($"acceptance record: {record}");
        }
        robot.Disconnect();
        return 0;
    }

    private static int Plan(string[] a)
    {
        if (a.Length < 5) { Console.WriteLine("manip --plan <x> <y> <angleDeg>"); return 1; }
        var goal = new Pose3d(Mat3.AboutZ(double.Parse(a[4]) * Math.PI / 180), new Vec3(double.Parse(a[2]), double.Parse(a[3]), 0));
        var path = StraightLinePlanner.Plan(Pose3d.Identity, goal);
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

    private static string WriteAcceptance(string tool, string path, bool automatedPass, CozmoRobot robot, string humanCheck, object detail)
    {
        var file = Path.GetFullPath(string.IsNullOrEmpty(path) ? $"cozmo-acceptance-{tool}-{DateTime.Now:yyyyMMdd-HHmmss}.json" : path);
        var record = new
        {
            tool, milestone = "M12", timestampUtc = DateTime.UtcNow, firmware = robot.State.FirmwareVersionNumber, serial = robot.State.SerialNumber,
            automated = new { pass = automatedPass, detail },
            human = new { check = humanCheck, verdict = "PENDING - fill in after watching the robot" },
        };
        File.WriteAllText(file, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
        return file;
    }
}
