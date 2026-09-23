using System.Net;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Conformance;

/// <summary>
/// The core-review corrections, on the robot.
///
/// Every one of these was reproduced, corrected and covered by an offline regression. What hardware adds is
/// the half the offline tests cannot reach: whether the wheels actually stop, whether the link really does
/// drop that way, whether a real camera over a real floor puts an edge in the map. Where hardware adds
/// nothing - a disposed component's callbacks, an event fan-out - there is deliberately no case here, because
/// a physical test that cannot observe the thing it is named after is worse than no test at all.
///
/// <code>
/// core &lt;robot-ip&gt; --case CORE-001 [--obb &lt;dir&gt;] [--seconds N] [--acceptance &lt;file&gt;]
/// </code>
/// </summary>
public static class CoreChecks
{
    public const string Usage =
        "core <robot-ip> --case <CORE-001|CORE-002|CORE-003|CORE-004|CORE-007|CORE-008> [--obb <dir>] [--seconds N] [--acceptance <file>]";

    public static async Task<int> Run(string[] a)
    {
        if (a.Length < 2 || Arg(a, "--case") is not { } which) { Console.WriteLine(Usage); return 1; }
        var obb = Arg(a, "--obb");
        int seconds = int.TryParse(Arg(a, "--seconds"), out var s) ? s : 30;
        string? acceptance = Arg(a, "--acceptance");
        var ip = IPAddress.Parse(a[1]);

        var log = new List<string>();
        void Say(string line) { Console.WriteLine(line); log.Add(line); }

        Say($"connecting to {a[1]}...");
        using var robot = await CozmoRobot.ConnectAsync(ip);
        await robot.WaitForMotorCalibrationAsync(TimeSpan.FromSeconds(10));
        Say($"connected: firmware {robot.State.FirmwareVersionNumber}, telemetry {robot.State.StateRateHz:F1} Hz, "
          + $"calibration {(robot.State.CalibrationComplete ? "complete" : "NOT complete")}");

        (bool pass, string detail) outcome;
        try
        {
            outcome = which.ToUpperInvariant() switch
            {
                "CORE-001" => await Core001(robot, Say),
                "CORE-002" => await Core002(robot, obb, Say),
                "CORE-003" => await Core003(robot, obb, Say),
                "CORE-004" => await Core004(robot, Say),
                "CORE-007" => await Core007(robot, seconds, Say),
                "CORE-008" => await Core008(robot, seconds, Say),
                _ => (false, $"no case called '{which}'; {Usage}"),
            };
        }
        catch (Exception e)
        {
            outcome = (false, $"{e.GetType().Name}: {e.Message}");
            Say($"the case threw: {outcome.detail}");
        }

        Say("");
        Say($"automated: {(outcome.pass ? "PASS" : "FAIL")} - {outcome.detail}");

        if (acceptance is not null)
        {
            var record = new
            {
                tool = "core",
                which,
                timestampUtc = DateTime.UtcNow,
                firmware = robot.State.FirmwareVersionNumber,
                serial = robot.State.SerialNumber,
                automated = new { pass = outcome.pass, detail = outcome.detail, log },
                human = new { verdict = "PENDING - the person watching the robot decides this" },
            };
            File.WriteAllText(acceptance, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"acceptance record: {Path.GetFullPath(acceptance)}");
        }

        try { robot.EmergencyStop(); } catch { /* the link may already be gone: CORE-002 drops it on purpose */ }
        try { robot.Disconnect(); } catch { }
        return outcome.pass ? 0 : 2;
    }

    // ================================================================ CORE-001

    /// <summary>
    /// A live keep-alive body shuffle has to stop at its duration with nothing else driving the scheduler.
    ///
    /// The bug was that nothing in production called <c>Advance</c> while only a live keyframe was running,
    /// so the wheels kept the speed they were given. Here the keyframe is streamed exactly as idle streams
    /// it, and the robot's own reported wheel speeds are the evidence: they have to go non-zero and then
    /// return to zero within a frame or two of the duration, with nobody sending a stop by hand.
    /// </summary>
    private static async Task<(bool, string)> Core001(CozmoRobot robot, Action<string> say)
    {
        const uint durationMs = 1200;
        say($"streaming one live body keyframe: {durationMs} ms at 40 mm/s, straight, the way idle streams it.");
        say("nothing else will drive the scheduler and nothing will send a stop by hand.");

        var samples = new List<(double AtMs, float L, float R)>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        void Sample()
        {
            if (robot.State.Latest is { } st) samples.Add((sw.Elapsed.TotalMilliseconds, st.LwheelSpeedMmps, st.RwheelSpeedMmps));
        }

        if (!robot.Animations.StreamLive(new BodyKeyframe(0, durationMs, "STRAIGHT", 40)))
            return (false, "the scheduler refused the live keyframe (a clip owns the body track)");

        while (sw.Elapsed.TotalMilliseconds < durationMs + 2500)
        {
            Sample();
            await Task.Delay(33);
        }

        var moving = samples.Where(x => Math.Abs(x.L) > 5 || Math.Abs(x.R) > 5).ToList();
        double? firstMove = moving.Count > 0 ? moving.First().AtMs : null;
        double? lastMove = moving.Count > 0 ? moving.Last().AtMs : null;
        var after = samples.Where(x => x.AtMs > durationMs + 500).ToList();
        bool stillMoving = after.Any(x => Math.Abs(x.L) > 5 || Math.Abs(x.R) > 5);

        say($"wheels reported moving from {firstMove?.ToString("F0") ?? "-"} ms to {lastMove?.ToString("F0") ?? "-"} ms "
          + $"(the keyframe asked for {durationMs} ms)");
        say($"after the duration plus half a second: {(stillMoving ? "STILL MOVING" : "stopped")}"
          + $" ({after.Count} state samples, max |speed| {(after.Count > 0 ? after.Max(x => Math.Max(Math.Abs(x.L), Math.Abs(x.R))) : 0):F1} mm/s)");
        say("evidence: wheel-speed samples at 33 ms, from the robot's own telemetry");
        foreach (var x in samples.Where((_, i) => i % 3 == 0))
            say($"  t={x.AtMs,6:F0} ms  L={x.L,7:F1}  R={x.R,7:F1}");

        if (moving.Count == 0) return (false, "the wheels never moved: the live keyframe did not reach the robot");
        if (stillMoving) return (false, "the wheels were still turning after the keyframe's duration: the stop never went out");
        return (true, $"the shuffle ran about {lastMove - firstMove:F0} ms and stopped on its own");
    }

    // ================================================================ CORE-002

    /// <summary>
    /// A robot that goes away mid-animation ends the animation, not the process.
    ///
    /// The tick loop runs on a background thread and every frame it streams reaches the transport; the
    /// failure being tested for was an exception escaping that thread. Nothing offline can prove the real
    /// link drops the way a flat battery or a walked-out-of-range robot does, which is what this does: play
    /// a long clip, cut the link underneath it, and see the animation end, the ticker stop and the process
    /// still be here to say so.
    /// </summary>
    private static async Task<(bool, string)> Core002(CozmoRobot robot, string? obb, Action<string> say)
    {
        if (LoadAssets(robot, obb, say) is not { } clip) return (false, "no animation assets: pass --obb");

        Exception? faulted = null;
        robot.Animations.Faulted += e => faulted = e;

        say("");
        say("  WATCH HIM: he will start moving, and part way through the animation this tool cuts the link on");
        say("  purpose. What you are judging is the second or two after that: he should stop where he is and");
        say("  stay stopped - no twitch, no carrying on - and this tool should print a verdict instead of dying.");
        say("");
        int interruptAtMs = InterruptAt(clip);
        say($"playing '{clip.Name}' ({clip.DurationMs} ms); the link goes {interruptAtMs / 1000.0:F2} s in.");
        var playing = robot.Animations.Play(clip);
        if (playing is null) return (false, "the animation did not start");
        await Task.Delay(interruptAtMs);
        bool activeBefore = robot.Animations.IsTicking && robot.Animations.IsPlaying && !playing.IsCompleted;
        say($"playback active immediately before disconnect: {activeBefore}. DROPPING THE LINK NOW - watch him stop.");
        robot.Transport.Disconnect("core-002: the robot goes away mid-animation");

        var finished = await Task.WhenAny(playing, Task.Delay(TimeSpan.FromSeconds(5)));
        bool ended = ReferenceEquals(finished, playing);
        var settle = System.Diagnostics.Stopwatch.StartNew();
        while (robot.Animations.IsTicking && settle.Elapsed.TotalSeconds < 3) await Task.Delay(50);

        say($"the animation task {(ended ? "completed" : "DID NOT complete within 5 s")}");
        say($"the ticker {(robot.Animations.IsTicking ? "IS STILL RUNNING" : "stopped")}");
        say($"the failure was reported through Faulted: {(faulted is null ? "no" : faulted.GetType().Name + ": " + faulted.Message)}");
        say("the process is still running, which is the other half of the claim");

        if (!activeBefore) return (false, "playback was not active immediately before disconnect, so the drop proved nothing");
        if (!ended) return (false, "the animation task never completed after the link dropped");
        if (robot.Animations.IsTicking) return (false, "the ticker was still running after the link dropped");
        if (faulted is null) return (false, "the link failure did not reach the animation Faulted event");
        return (true, "the animation ended, the ticker stopped, the fault was reported and the process lived");
    }

    // ================================================================ CORE-003

    /// <summary>
    /// A cancelled animation stops sending commands.
    ///
    /// The emission gate is what keeps a command of the old playback from going out after a replacement has
    /// taken over. On the robot that shows as movement: a clip full of motion is stopped part way, and from
    /// that moment nothing more may reach the motors. The robot's own reported wheel, head and lift values
    /// are the evidence, because they are what a leaked command would move.
    /// </summary>
    private static async Task<(bool, string)> Core003(CozmoRobot robot, string? obb, Action<string> say)
    {
        if (LoadAssets(robot, obb, say) is not { } clip) return (false, "no animation assets: pass --obb");

        int interruptAtMs = InterruptAt(clip);
        say($"playing '{clip.Name}' ({clip.DurationMs} ms) and stopping it {interruptAtMs} ms in; after the stop nothing more may reach the motors.");
        var playing = robot.Animations.Play(clip);
        if (playing is null) return (false, "the animation did not start");
        await Task.Delay(interruptAtMs);

        bool activeBefore = robot.Animations.IsPlaying && robot.Animations.IsTicking && !playing.IsCompleted;
        int firedAtStop = robot.Animations.Scheduler.KeyframesFired;
        say($"playback active immediately before cancellation: {activeBefore}; keyframes fired: {firedAtStop}");
        if (!activeBefore) return (false, "playback was not active immediately before cancellation, so this was not a cancellation test");

        robot.Animations.Stop();
        say("stopped. Watching the robot's own telemetry for 2.5 s.");

        var samples = new List<(double AtMs, float L, float R, float Head, float Lift)>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < 2500)
        {
            if (robot.State.Latest is { } st)
                samples.Add((sw.Elapsed.TotalMilliseconds, st.LwheelSpeedMmps, st.RwheelSpeedMmps, st.HeadAngle, st.LiftAngle));
            await Task.Delay(33);
        }

        // the first 300 ms are the robot coming to rest from what was already in flight
        var settled = samples.Where(x => x.AtMs > 400).ToList();
        if (settled.Count < 5) return (false, "not enough telemetry after the stop to judge it");
        float wheels = settled.Max(x => Math.Max(Math.Abs(x.L), Math.Abs(x.R)));
        float headSpread = settled.Max(x => x.Head) - settled.Min(x => x.Head);
        float liftSpread = settled.Max(x => x.Lift) - settled.Min(x => x.Lift);
        int firedAfterSettle = robot.Animations.Scheduler.KeyframesFired;

        say($"after the stop settled: wheels at most {wheels:F1} mm/s, head moved {headSpread:F3} rad, lift moved {liftSpread:F3} rad");
        say($"the scheduler reports playing: {robot.Animations.Playing ?? "nothing"}");
        say($"keyframes fired after cancellation: {firedAfterSettle} (at cancellation: {firedAtStop})");
        foreach (var x in settled.Where((_, i) => i % 4 == 0))
            say($"  t={x.AtMs,6:F0} ms  L={x.L,7:F1}  R={x.R,7:F1}  head={x.Head,6:F3}  lift={x.Lift,6:F3}");

        bool quiet = wheels <= 5 && headSpread <= 0.05 && liftSpread <= 0.05;
        if (robot.Animations.IsPlaying) return (false, "the scheduler still says it is playing after Stop");
        if (robot.Animations.IsTicking) return (false, "the scheduler ticker was still running after Stop settled");
        if (firedAfterSettle != firedAtStop) return (false, "keyframes from the cancelled clip fired after Stop");
        return (quiet, quiet
            ? "nothing reached the motors after the stop"
            : "the robot was still moving after the stop: a command of the stopped animation got out");
    }

    // ================================================================ CORE-004

    /// <summary>
    /// A late abort clears its own path and nobody else's.
    ///
    /// The case that matters is the one that arrives late: a run that was cancelled or timed out cleaning up
    /// after another action has already installed its own path. Offline this is asserted against the message
    /// log; here the second path is a real drive, and the question is whether the robot finishes it.
    /// </summary>
    private static async Task<(bool, string)> Core004(CozmoRobot robot, Action<string> say)
    {
        using var vision = new VisionSystem(robot, CameraCalibration.Nominal());
        using var m = new ManipulationSystem(robot, vision);

        if (robot.State.Latest is not { } initial) return (false, "no RobotState pose for path A");
        var first = ForwardLine(initial.Pose.X, initial.Pose.Y, initial.Pose.Angle, 60, 40);

        say($"installing path A 60 mm forward from the reported pose ({initial.Pose.X:F1}, {initial.Pose.Y:F1}, {initial.Pose.Angle:F3}), then replacing it with B and aborting A late.");
        var runA = m.StartPath(first);
        say($"  path A is id {runA.PathId}");
        await Task.Delay(400);
        if (robot.State.Latest is not { } atReplacement) return (false, "no RobotState pose for path B");
        var second = ForwardLine(atReplacement.Pose.X, atReplacement.Pose.Y, atReplacement.Pose.Angle, 120, 60);
        say($"  path B starts at the current reported pose ({atReplacement.Pose.X:F1}, {atReplacement.Pose.Y:F1}, {atReplacement.Pose.Angle:F3})");
        var runB = m.StartPath(second);
        say($"  path B is id {runB.PathId}");

        runA.Abort();                       // the late cleanup: A no longer owns the robot's path
        say($"  A's abort sent a clear to the robot: {runA.ClearedRobotPath} (it must be False)");

        var evt = await runB.WaitAsync(TimeSpan.FromSeconds(20), default);
        say($"  path B finished as: {evt?.ToString() ?? "no terminal event within 20 s"}");
        if (robot.State.Latest is { } st) say($"  robot pose now: x={st.Pose.X:F1} y={st.Pose.Y:F1} angle={st.Pose.Angle:F3}");

        if (runA.ClearedRobotPath) return (false, "A's late abort cleared the robot's path, which by then was B's");
        if (evt is null) return (false, "path B never reported a terminal event: it may have been cleared out from under it");
        return (evt == PathEventType.Completed, evt == PathEventType.Completed
            ? "B ran to completion with A's late abort sending nothing"
            : $"B ended as {evt} rather than completing");
    }

    // ================================================================ CORE-007

    /// <summary>
    /// The ground the robot looks at reaches the map.
    ///
    /// The detector and the map's side of it were both built and nothing joined them. The join is now what
    /// the engine does, and this is the only way to see it work on a real floor: real lighting, a real
    /// camera, a real edge in front of him. The map's own region counts are the evidence.
    /// </summary>
    private static async Task<(bool, string)> Core007(CozmoRobot robot, int seconds, Action<string> say)
    {
        using var vision = new VisionSystem(robot);
        var cal = await vision.ReadCalibrationAsync(TimeSpan.FromSeconds(3));
        if (cal is null)
        {
            say("camera calibration NOT READ from the robot: an edge put in the map from a made-up geometry would be in the wrong place.");
            return (false, "no camera calibration; run the vision check (K) first");
        }
        say($"camera calibration read from the robot: fx={cal.FocalLengthX:F1} fy={cal.FocalLengthY:F1}");

        var map = new MemoryMap();
        vision.OverheadEdges = new OverheadEdgesDetector();
        int framesWithEdges = 0, framesSeen = 0;
        vision.FrameProcessed += r =>
        {
            framesSeen++;
            if (r.OverheadEdges is not { } edges) return;
            if (edges.Chains.Sum(c => c.Points.Count) > 0) framesWithEdges++;
            map.AddVisionOverheadEdges(edges, r.PoseData.RobotPose);
        };

        robot.StartCamera();
        say($"watching the ground for {seconds} s. Keep the edge in front of him.");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            await Task.Delay(500);
            var regions = map.Regions;
            say($"  [{sw.Elapsed.TotalSeconds,5:F1}s] frames {framesSeen}, frames with edge points {framesWithEdges}, map regions {regions.Count}");
        }
        robot.StopCamera();

        var byType = map.Regions.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (type, count) in byType.OrderByDescending(x => x.Value)) say($"  map holds {count} region(s) of {type}");

        bool obstacles = map.HasContentType(MemoryMapContentType.ObstacleUnrecognized);
        bool clear = map.HasContentType(MemoryMapContentType.ClearOfObstacle);
        say($"the map holds an unrecognised obstacle: {obstacles}; ground marked clear: {clear}");
        if (framesSeen == 0) return (false, "no frames were processed at all");
        if (map.Regions.Count == 0) return (false, "the detector ran but nothing reached the map");
        return (true, $"{framesWithEdges} of {framesSeen} frames carried edge points and the map holds {map.Regions.Count} region(s)");
    }

    // ================================================================ CORE-008

    /// <summary>
    /// The origin a pose was measured in is part of the pose.
    ///
    /// Picking the robot up and putting it down is exactly what the engine calls a delocalization: a new
    /// origin, carried objects brought across, everything else no longer located, and a fresh map. It needs
    /// hands, so it cannot be provoked offline against a real robot's own state stream.
    /// </summary>
    private static async Task<(bool, string)> Core008(CozmoRobot robot, int seconds, Action<string> say)
    {
        using var vision = new VisionSystem(robot);
        var cal = await vision.ReadCalibrationAsync(TimeSpan.FromSeconds(3));
        if (cal is null) return (false, "no camera calibration; run the vision check (K) first");

        var map = new MemoryMap();
        var origins = new List<uint>();
        int delocalizations = 0;
        var forgotten = new List<string>();
        var forgottenIds = new HashSet<uint>();
        var locatedBeforeDelocalization = new HashSet<uint>();

        vision.RobotDelocalized += origin =>
        {
            delocalizations++;
            origins.Add(origin);
            map.Clear();
            say($"  DELOCALIZED: the robot is now in origin {origin}; the map has been cleared");
        };
        vision.World.PoseStateChanged += (o, was, now) =>
        {
            if (now == PoseState.Unknown && was != PoseState.Unknown)
            {
                forgotten.Add($"{o.ObjectId} {o.Type} ({was} -> Unknown)");
                forgottenIds.Add(o.ObjectId);
                say($"  object {o.ObjectId} {o.Type} stopped being located ({was} -> Unknown)");
            }
        };

        robot.StartCamera();
        say($"show him a cube until it is located, then pick him up and put him down somewhere else. {seconds} s.");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        uint lastOrigin = 0;
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            await Task.Delay(500);
            var located = vision.World.Objects.Where(o => o.IsLocated).ToList();
            if (delocalizations == 0)
                foreach (var o in located.Where(o => CubeGeometry.IsCube(o.Type))) locatedBeforeDelocalization.Add(o.ObjectId);
            if (vision.OriginId != lastOrigin)
            {
                lastOrigin = vision.OriginId;
                say($"  [{sw.Elapsed.TotalSeconds,5:F1}s] the robot reports pose origin {lastOrigin}");
            }
            if (sw.Elapsed.TotalSeconds % 5 < 0.5)
                say($"  [{sw.Elapsed.TotalSeconds,5:F1}s] located objects: {(located.Count == 0 ? "none" : string.Join(", ", located.Select(o => $"{o.ObjectId} {o.Type}")))}");
        }
        robot.StopCamera();

        say($"origin changes seen: {delocalizations} ({string.Join(" -> ", origins)})");
        say($"cubes located before the origin change: {(locatedBeforeDelocalization.Count == 0 ? "none" : string.Join(", ", locatedBeforeDelocalization))}");
        say($"objects that stopped being located: {(forgotten.Count == 0 ? "none" : string.Join("; ", forgotten))}");
        if (delocalizations == 0) return (false, "the robot never reported a new origin: he may not have been lifted far enough");
        if (locatedBeforeDelocalization.Count == 0) return (false, "no cube was located before the origin changed, so object invalidation was not exercised");
        int invalidated = locatedBeforeDelocalization.Count(forgottenIds.Contains);
        if (invalidated == 0) return (false, "the origin changed but the previously located cube did not become unlocated");
        return (true, $"{delocalizations} origin change(s); {invalidated} previously located cube(s) became unlocated");
    }

    // ------------------------------------------------------------------ plumbing

    /// <summary>Loads the animation library and picks a clip with plenty of motion in it.</summary>
    private static AnimationClip? LoadAssets(CozmoRobot robot, string? obb, Action<string> say)
    {
        if (obb is null) return null;
        var assets = TriggersTool.FindAssetsRoot(obb);
        if (assets is null) { say($"no animation assets under '{obb}'"); return null; }
        var lib = robot.Animations.LoadFrom(assets);
        foreach (var name in new[] { "anim_bored_01", "anim_reacttoblock_success_01", "anim_poked_giggle" })
            if (lib.ClipNames.Contains(name))
            {
                var clip = lib.GetClip(name);
                say($"using the clip '{name}' ({clip.DurationMs} ms)");
                return clip;
            }
        var any = lib.ClipNames.FirstOrDefault();
        if (any is null) return null;
        var fallback = lib.GetClip(any);
        say($"using the clip '{any}' ({fallback.DurationMs} ms)");
        return fallback;
    }

    private static int InterruptAt(AnimationClip clip) =>
        Math.Max(100, (int)Math.Min(clip.DurationMs * 0.4, clip.DurationMs > 300 ? clip.DurationMs - 250 : clip.DurationMs / 2));

    private static IReadOnlyList<PathSegment> ForwardLine(float x, float y, float angle, double distanceMm, float speedMmps)
    {
        double endX = x + Math.Cos(angle) * distanceMm;
        double endY = y + Math.Sin(angle) * distanceMm;
        return new[] { new PathSegment.Line(x, y, endX, endY, speedMmps, 200, 200) };
    }

    private static string? Arg(string[] a, string name)
    {
        for (int i = 1; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }
}
