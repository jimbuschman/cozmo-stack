using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;

namespace Cozmo.Conformance;

/// <summary>
/// M11 tools: the marker detector, the world model and the hardware checks that depend on them.
///
/// <c>vision --synthetic</c> renders cubes from the engine's own marker library into frames and runs the whole
/// pipeline, printing the recovered pose against the truth (the offline conformance evidence).
/// <c>vision --replay &lt;frame-log&gt;</c> runs the detector over the camera frames in a committed capture.
/// <c>vision --images &lt;jpeg...&gt;</c> runs it over JPEG files from the camera tool.
/// <c>vision &lt;robot-ip&gt;</c> is the hardware acceptance: read the camera calibration from NV storage, stream the
/// camera, print every marker and object, and record the run.
/// <c>bodyangle &lt;robot-ip&gt; --deg 45</c> checks the SetBodyAngle semantics the turn action relies on.
/// </summary>
public static class VisionTool
{
    private static readonly Regex LogLine = new(@"^(\S+) (TX|RX) ((?:[0-9a-f]{2} ?)+)$", RegexOptions.Compiled);

    public static async Task<int> Run(string[] a)
    {
        if (!MarkerLibrary.IsAvailable) Console.WriteLine("note: the marker library is not available (Anki's data, extracted locally from libcozmoEngine.so by the build); markers cannot be decoded");
        if (a.Length < 2) { Console.WriteLine("vision --synthetic | --replay <frame-log> | --images <jpeg...> | <robot-ip> [--seconds 60] [--nominal] [--unconnected] [--acceptance [file]] [--out <dir>]"); return 1; }
        if (a[1] == "--synthetic") return Synthetic(Arg(a, "--out"));
        if (a[1] == "--replay") return a.Length > 2 ? Replay(a[2], Arg(a, "--out")) : 1;
        if (a[1] == "--images") return Images(a.Skip(2).Where(x => !x.StartsWith("--")).ToArray(), Arg(a, "--out"));
        return await Live(a);
    }

    // ------------------------------------------------------------------ synthetic

    private static int Synthetic(string? outDir)
    {
        if (!MarkerLibrary.IsAvailable) { Console.WriteLine("the marker library is not available on this machine: run re-analysis/tools/extract_marker_library.py against your libcozmoEngine.so and rebuild"); return 1; }
        var lib = MarkerLibrary.Embedded;
        Console.WriteLine($"marker library: {lib.NumImages} images, {lib.NumLabels} labels, codes 0..{lib.LabelToCode.Max(c => (int)c)}");
        var cal = CameraCalibration.Nominal();
        Console.WriteLine($"calibration: {cal}");
        using var robot = CozmoRobot.CreateOffline();
        using var vision = new VisionSystem(robot, cal) { Enabled = false };
        vision.World.AllowUnconnectedObjects = true;
        vision.World.Log += l => Console.WriteLine("  world: " + l);
        int failures = 0, cases = 0;
        var rnd = new Random(11);
        foreach (var (dist, y, yaw, head) in new[] { (120.0, 0.0, 0.0, -0.15), (200.0, 30.0, 0.4, -0.1), (150.0, -20.0, -0.7, -0.2), (300.0, 0.0, 0.2, 0.0), (90.0, 10.0, 0.8, -0.3) })
        {
            cases++;
            var robotPose = HeadGeometry.RobotPose(new RobotPose { X = 0, Y = 0, Angle = 0 });
            var pd = new VisionPoseData(1000 + (uint)cases, robotPose, head, 0, false, false);
            var cube = new Pose3d(Mat3.AboutZ(yaw), new Vec3(dist, y, CubeGeometry.CubeSizeMm / 2));
            var frame = new GrayImage(cal.Columns, cal.Rows);
            for (int i = 0; i < frame.Pixels.Length; i++) frame.Pixels[i] = (byte)(140 + rnd.Next(-6, 6));
            var drawn = MarkerRenderer.DrawCube(frame, lib, new CameraModel(cal, pd.CameraPose), ObjectType.Block_LIGHTCUBE1, cube);
            var r = vision.ProcessImage(frame, (uint)cases, pd.Timestamp, pd);
            var o = r.Objects.FirstOrDefault()?.Object;
            double err = o is null ? double.NaN : (o.Pose.Translation - cube.Translation).Length;
            double ang = o is null ? double.NaN : o.Pose.Rotation.AngularDistance(cube.Rotation) * 180 / Math.PI;
            // a lone 25 mm marker seen at 300 mm is about 24 px wide: 2 % of range and 5 degrees is what a single-marker PnP can promise
            bool ok = o is not null && err < Math.Max(3, 0.02 * dist) && ang < 5;
            if (!ok) failures++;
            Console.WriteLine($"cube at {cube.Translation} yaw={yaw * 180 / Math.PI:F0}deg head={head * 180 / Math.PI:F0}deg: drawn [{string.Join(",", drawn)}] " +
                              $"detected [{string.Join(",", r.Markers.Select(m => m.Code))}] -> {(o is null ? "NO OBJECT" : $"pose error {err:F1} mm, {ang:F1} deg")} {(ok ? "OK" : "FAIL")} " +
                              $"({r.Elapsed.TotalMilliseconds:F0} ms; {vision.Detector.Quads.LastStats})");
            if (outDir is not null) { Directory.CreateDirectory(outDir); frame.SavePgm(Path.Combine(outDir, $"synthetic-{cases}.pgm")); vision.Detector.Quads.LastMask?.SavePgm(Path.Combine(outDir, $"synthetic-{cases}-mask.pgm")); }
            vision.World.MarkUnknown(1);
        }
        Console.WriteLine(failures == 0 ? $"all {cases} synthetic cases OK" : $"{failures} of {cases} synthetic cases FAILED");
        return failures == 0 ? 0 : 2;
    }

    // ------------------------------------------------------------------ replay and images

    private static int Replay(string path, string? outDir)
    {
        using var robot = CozmoRobot.CreateOffline();
        var frames = new List<CameraFrame>();
        robot.Camera.FrameReceived += frames.Add;
        int states = 0;
        robot.Message += m => { if (m is RobotState) states++; };
        foreach (var raw in File.ReadLines(path))
        {
            var m = LogLine.Match(raw.Trim('﻿'));
            if (!m.Success || m.Groups[2].Value != "RX") continue;
            try { robot.Transport.ProcessIncoming(Hex.Parse(m.Groups[3].Value)); } catch (FormatException) { }
        }
        Console.WriteLine($"{path}: {frames.Count} camera frames, {states} robot states");
        var det = new MarkerDetector();
        int total = 0;
        foreach (var f in frames)
        {
            var gray = GrayImage.FromFrame(f);
            var markers = det.Detect(gray, f.Timestamp);
            total += markers.Count;
            Console.WriteLine($"  image {f.ImageId} t={f.Timestamp} {gray.Width}x{gray.Height}: {markers.Count} marker(s) {string.Join(" ", markers)}  [{det.Quads.LastStats}]");
            if (outDir is not null) { Directory.CreateDirectory(outDir); gray.SavePgm(Path.Combine(outDir, $"frame-{f.ImageId}.pgm")); }
        }
        Console.WriteLine($"{total} marker(s) in {frames.Count} frames");
        return 0;
    }

    private static int Images(string[] files, string? outDir)
    {
        var det = new MarkerDetector();
        foreach (var file in files)
        {
            var gray = GrayImage.FromJpeg(File.ReadAllBytes(file));
            var markers = det.Detect(gray, 0);
            Console.WriteLine($"{file}: {gray.Width}x{gray.Height}: {markers.Count} marker(s) {string.Join(" ", markers)}  [{det.Quads.LastStats}]");
            foreach (var (q, m, reason) in det.LastQuads) Console.WriteLine($"    quad [{string.Join(" ", q.Corners)}] -> {(m is null ? "rejected: " + reason : m.Code.ToString())}");
            if (outDir is not null) { Directory.CreateDirectory(outDir); det.Quads.LastMask?.SavePgm(Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + "-mask.pgm")); }
        }
        return 0;
    }

    // ------------------------------------------------------------------ live

    private static async Task<int> Live(string[] a)
    {
        int seconds = int.TryParse(Arg(a, "--seconds"), out var s) ? s : 60;
        bool nominal = a.Contains("--nominal"), unconnected = a.Contains("--unconnected");
        string? acceptance = AcceptancePath(a), outDir = Arg(a, "--out");
        Console.WriteLine($"connecting to {a[1]}...");
        using var robot = await CozmoRobot.ConnectAsync(IPAddress.Parse(a[1]));
        using var vision = new VisionSystem(robot);
        var log = new List<string>();
        void Say(string line) { Console.WriteLine(line); log.Add(line); }
        vision.Log += l => Say("  vision: " + l);
        vision.World.Log += l => Say("  world: " + l);

        // 1. the calibration, as the engine reads it on connection
        var cal = await vision.ReadCalibrationAsync(TimeSpan.FromSeconds(3));
        Say(cal is null ? "camera calibration: NOT READ from NV storage (see vision log above)" : $"camera calibration from robot: {cal}");
        if (cal is null && nominal) { vision.Calibration = CameraCalibration.Nominal(); Say($"using the nominal stand-in: {vision.Calibration} (LOCAL_POLICY; poses will be approximate)"); }
        if (vision.Calibration is null) { Say("no calibration: markers will still be listed, but no object can be localised"); }
        if (unconnected) { vision.World.AllowUnconnectedObjects = true; Say("objects will be created for unconnected cubes (LOCAL_POLICY switch)"); }

        // 2. cubes and camera
        robot.StartCamera();
        int markers = 0, objects = 0, frames = 0;
        var seenCodes = new HashSet<MarkerType>();
        var detectorOnly = new MarkerDetector();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        vision.FrameProcessed += r =>
        {
            frames++;
            markers += r.Markers.Count;
            foreach (var m in r.Markers) seenCodes.Add(m.Code);
            if (r.Markers.Count > 0 || r.Objects.Count > 0 || r.Forgotten.Count > 0)
                Say($"[{sw.Elapsed.TotalSeconds,7:F2}s] {r}");
            foreach (var o in r.Objects) Say($"           {o.Object} (rms {o.RmsPx:F2} px, up {o.Object.UpAxisFromPose()})");
        };
        vision.World.PoseStateChanged += (o, from, to) => Say($"[{sw.Elapsed.TotalSeconds,7:F2}s] object {o.ObjectId} {from} -> {to}");
        vision.World.ObjectObserved += _ => objects++;
        if (vision.Calibration is null)
        {
            // still show markers so the detector can be judged without a calibration
            robot.Camera.FrameReceived += f =>
            {
                frames++;
                var found = detectorOnly.Detect(GrayImage.FromFrame(f), f.Timestamp);
                markers += found.Count;
                foreach (var m in found) seenCodes.Add(m.Code);
                if (found.Count > 0) Say($"[{sw.Elapsed.TotalSeconds,7:F2}s] image {f.ImageId}: {string.Join(" ", found)}");
                if (outDir is not null && found.Count > 0) { Directory.CreateDirectory(outDir); f.Save(Path.Combine(outDir, $"marker-{f.ImageId}.jpg")); }
            };
        }
        Say($"streaming for {seconds}s: show Cozmo a cube, move it, hide it. connected cubes: {robot.Cubes.ConnectedCubes.Count}");
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end) await Task.Delay(500);
        robot.StopCamera();
        Say($"\n{frames} frames, {markers} markers ({string.Join(",", seenCodes)}), {objects} object observations, {vision.FramesDropped} frames dropped; " +
            $"located now: {string.Join(", ", vision.World.LocatedObjects)}");
        if (acceptance is not null)
        {
            var record = WriteAcceptance("vision", acceptance, cal is not null && markers > 0, robot,
                "the printed marker codes match the cube faces shown; the object's pose (mm, in the robot's frame) matches where the cube stands; " +
                "hiding the cube in view forgets it after two frames; a cube moved out of view stays located",
                new { calibration = cal?.ToString(), calibrationRead = cal is not null, frames, markers, codes = seenCodes.Select(c => c.ToString()), objects, log });
            Console.WriteLine($"acceptance record: {record}");
        }
        robot.Disconnect();
        return 0;
    }

    /// <summary><c>bodyangle &lt;robot-ip&gt; --deg 45 [--acceptance [file]]</c>: does SetBodyAngle take an absolute body angle?</summary>
    public static async Task<int> BodyAngle(string[] a)
    {
        if (a.Length < 2) { Console.WriteLine("bodyangle <robot-ip> [--deg 45] [--acceptance [file]]"); return 1; }
        double deg = double.TryParse(Arg(a, "--deg"), out var d) ? d : 45;
        string? acceptance = AcceptancePath(a);
        Console.WriteLine($"connecting to {a[1]}...");
        using var robot = await CozmoRobot.ConnectAsync(IPAddress.Parse(a[1]));
        await robot.WaitForMotorCalibrationAsync(TimeSpan.FromSeconds(10));
        var start = robot.State.Latest!.Pose.Angle;
        double target = start + deg * Math.PI / 180;
        var msg = TurnTowardsPose.Message(target, TurnTowardsPose.MaxSpeedRadPerSec, TurnTowardsPose.AccelRadPerSec2, TurnTowardsPose.ToleranceRad, 0, true, 9);
        Console.WriteLine($"pose angle now {start * 180 / Math.PI:F1} deg; sending SetBodyAngle(absolute {target * 180 / Math.PI:F1} deg): {Convert.ToHexString(msg.ToBytes())}");
        robot.Transport.Send(msg, flush: true);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double last = start;
        var samples = new List<object>();
        while (sw.Elapsed < TimeSpan.FromSeconds(6))
        {
            await Task.Delay(200);
            var st = robot.State.Latest!;
            last = st.Pose.Angle;
            samples.Add(new { t = sw.Elapsed.TotalSeconds, angleDeg = last * 180 / Math.PI, moving = st.Has(RobotStatusFlag.IsMoving) });
            Console.WriteLine($"[{sw.Elapsed.TotalSeconds,5:F1}s] pose angle {last * 180 / Math.PI,7:F1} deg {(st.Has(RobotStatusFlag.IsMoving) ? "moving" : "")}");
        }
        double turned = Math.Atan2(Math.Sin(last - start), Math.Cos(last - start)) * 180 / Math.PI;
        string verdict = Math.Abs(turned - deg) < 5 ? "ABSOLUTE body angle confirmed" :
                         Math.Abs(turned - 2 * deg) < 5 ? "the robot turned twice the request: the field is RELATIVE" :
                         Math.Abs(turned) < 2 ? "the robot did not turn: the message or fields are wrong" : $"turned {turned:F1} deg: inconclusive";
        Console.WriteLine($"turned {turned:F1} deg for a {deg:F1} deg request: {verdict}");
        if (acceptance is not null)
        {
            var record = WriteAcceptance("bodyangle", acceptance, Math.Abs(turned - deg) < 5, robot,
                $"the robot turned {deg:F0} degrees in place, smoothly, and stopped", new { requestDeg = deg, turnedDeg = turned, verdict, message = Convert.ToHexString(msg.ToBytes()), samples });
            Console.WriteLine($"acceptance record: {record}");
        }
        robot.Disconnect();
        return 0;
    }

    // ------------------------------------------------------------------ helpers

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
            tool,
            milestone = "M11",
            timestampUtc = DateTime.UtcNow,
            firmware = robot.State.FirmwareVersionNumber,
            serial = robot.State.SerialNumber,
            automated = new { pass = automatedPass, detail },
            human = new { check = humanCheck, verdict = "PENDING - fill in after watching the robot" },
        };
        File.WriteAllText(file, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
        return file;
    }
}
