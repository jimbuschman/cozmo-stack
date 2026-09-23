using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;

namespace Cozmo.Conformance;

/// <summary>
/// M10 tools: the engine's derived robot state and the reactions built on it.
///
/// <c>offtreads</c> runs the off-treads classifier and the unexpected-movement detector, either live against a
/// robot or over a committed frame log, and prints every state change with the inputs that caused it.
/// <c>reactions</c> is the M10 hardware acceptance: the reaction strategies and behaviours running under the
/// behaviour manager on a real robot, every decision printed and recorded.
/// </summary>
public static class ReactionsTool
{
    private static readonly Regex LogLine = new(@"^(\S+) (TX|RX) ((?:[0-9a-f]{2} ?)+)$", RegexOptions.Compiled);

    /// <summary>
    /// <c>offtreads &lt;robot-ip&gt; [--seconds 60] [--acceptance [file]]</c> or
    /// <c>offtreads --replay &lt;frame-log&gt;</c>
    /// </summary>
    public static async Task<int> OffTreads(string[] a)
    {
        if (a.Length < 2) { Console.WriteLine("need the robot's address, or --replay <frame-log>"); return 1; }
        var replay = Arg(a, "--replay");
        if (replay is not null) return Replay(replay);

        int seconds = int.TryParse(Arg(a, "--seconds"), out var s) ? s : 60;
        string? acceptance = AcceptancePath(a);

        Console.WriteLine($"connecting to {a[1]}...");
        using var robot = await CozmoRobot.ConnectAsync(IPAddress.Parse(a[1]));
        var transitions = new List<object>();
        var movements = new List<object>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sensors = robot.Sensors;

        sensors.OffTreadsStateChanged += (from, to) =>
        {
            var line = $"[{sw.Elapsed.TotalSeconds,7:F2}s] off-treads {from} -> {to}  " +
                       $"pitch={Deg(sensors.PitchRad),6:F1}deg  accel={sensors.OffTreads.FilteredAccel}  |accel|={sensors.FilteredAccelMagnitude:F0}  " +
                       $"flags={(sensors.PickedUp ? "pickedUp " : "")}{(sensors.Falling ? "falling" : "")}";
            Console.WriteLine(line);
            transitions.Add(new { t = sw.Elapsed.TotalSeconds, from = from.ToString(), to = to.ToString(),
                                  pitchDeg = Deg(sensors.PitchRad), accelY = sensors.OffTreads.FilteredAccel.Y,
                                  accelMag = sensors.FilteredAccelMagnitude, pickedUp = sensors.PickedUp, falling = sensors.Falling });
        };
        sensors.UnexpectedMovementDetected += r =>
        {
            Console.WriteLine($"[{sw.Elapsed.TotalSeconds,7:F2}s] {r}");
            movements.Add(new { t = sw.Elapsed.TotalSeconds, type = r.Type.ToString(), side = r.Side.ToString(), r.AverageLeftMmps, r.AverageRightMmps, r.Count });
        };
        sensors.OffTreads.FallingStarted += () => Console.WriteLine($"[{sw.Elapsed.TotalSeconds,7:F2}s] falling started (classifier)");
        sensors.OffTreads.FallingStopped += d => Console.WriteLine($"[{sw.Elapsed.TotalSeconds,7:F2}s] falling stopped after {d} ms (classifier)");

        Console.WriteLine($"classifier {(sensors.OffTreadsClassifierEnabled ? "enabled (head calibration seen)" : "WAITING for the head calibration report")}; " +
                          $"on-back centre {Deg(sensors.OffTreads.OnBackCentreRad):F1} deg (physical robot)");
        Console.WriteLine($"watching for {seconds}s: pick him up, put him down, lay him on his back, each side and his face; " +
                          "then hold him still on a slope, shake him, and push him while he drives.");
        double nextStatus = 1;
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            await Task.Delay(50);
            if (sw.Elapsed.TotalSeconds >= nextStatus)
            {
                nextStatus += 5;
                Console.WriteLine($"[{sw.Elapsed.TotalSeconds,7:F2}s] state {sensors.OffTreadsState} candidate {sensors.OffTreads.Candidate}  " +
                                  $"pitch={Deg(sensors.PitchRad),6:F1}deg  |accel|={sensors.FilteredAccelMagnitude:F0}  " +
                                  $"classifier {(sensors.OffTreadsClassifierEnabled ? "on" : "off")}");
            }
        }

        bool sawStates = transitions.Count > 0;
        Console.WriteLine($"\n{transitions.Count} off-treads transitions, {movements.Count} unexpected movements, " +
                          $"{sensors.OffTreads.Updates} states classified");
        if (acceptance is not null)
        {
            var record = WriteAcceptance("offtreads", acceptance, sawStates, robot,
                "the printed transitions match what was done to him: picked up -> InAir at once, put down -> OnTreads at once, " +
                "on back about a second after being laid down, sides and face after a quarter second, and a push while driving reports the side it came from",
                new { transitions, unexpectedMovements = movements, statesClassified = sensors.OffTreads.Updates,
                      classifierEnabled = sensors.OffTreadsClassifierEnabled, onBackCentreDeg = Deg(sensors.OffTreads.OnBackCentreRad) });
            Console.WriteLine($"acceptance record: {record}");
        }
        robot.Disconnect();
        return 0;
    }

    /// <summary>Runs the classifier and detector over a committed frame log, offline.</summary>
    private static int Replay(string path)
    {
        if (!File.Exists(path)) { Console.WriteLine($"no such log: {path}"); return 1; }
        using var robot = CozmoRobot.CreateOffline();
        var sensors = robot.Sensors;
        int states = 0, changes = 0, movements = 0;
        DateTime? first = null, current = null;
        sensors.OffTreadsStateChanged += (from, to) =>
        {
            changes++;
            Console.WriteLine($"[{(current - first)?.TotalSeconds,7:F2}s] off-treads {from} -> {to}  pitch={Deg(sensors.PitchRad):F1}deg  " +
                              $"accel={sensors.OffTreads.FilteredAccel}  |accel|={sensors.FilteredAccelMagnitude:F0}");
        };
        sensors.UnexpectedMovementDetected += r => { movements++; Console.WriteLine($"[{(current - first)?.TotalSeconds,7:F2}s] {r}"); };
        robot.Message += m => { if (m is RobotState) states++; };

        float minPitch = float.MaxValue, maxPitch = float.MinValue, maxMag = 0;
        foreach (var line in File.ReadLines(path))
        {
            var m = LogLine.Match(line.Trim('﻿'));
            if (!m.Success || m.Groups[2].Value != "RX") continue;
            current = DateTime.Parse(m.Groups[1].Value, null, System.Globalization.DateTimeStyles.RoundtripKind);
            first ??= current;
            var raw = Hex.Parse(m.Groups[3].Value);
            try { robot.Transport.ProcessIncoming(raw); } catch (FormatException) { }
            if (sensors.PitchRad is { } p) { minPitch = Math.Min(minPitch, p); maxPitch = Math.Max(maxPitch, p); }
            maxMag = Math.Max(maxMag, sensors.FilteredAccelMagnitude);
        }
        Console.WriteLine($"\n{path}");
        Console.WriteLine($"{states} robot states, classifier {(sensors.OffTreadsClassifierEnabled ? "enabled" : "never enabled (no head calibration report in the log)")}, " +
                          $"{sensors.OffTreads.Updates} classified, {changes} transitions, {movements} unexpected movements");
        Console.WriteLine($"final state {sensors.OffTreadsState}; pitch range {Deg((float?)minPitch):F1}..{Deg((float?)maxPitch):F1} deg; " +
                          $"filtered |accel| {sensors.FilteredAccelMagnitude:F0} (peak {maxMag:F0}); filtered accel {sensors.OffTreads.FilteredAccel}");
        return 0;
    }

    /// <summary>
    /// <c>reactions &lt;robot-ip&gt; --obb &lt;dir&gt; [--seconds 120] [--acceptance [file]]</c>: the M10 reactions
    /// live, under the behaviour manager. Every trigger, switch and behaviour step is printed.
    /// </summary>
    public static async Task<int> Run(string[] a)
    {
        if (a.Length < 2) { Console.WriteLine("need the robot's address"); return 1; }
        var obb = Arg(a, "--obb");
        if (obb is null) { Console.WriteLine("need --obb <unpacked obb dir>"); return 1; }
        int seconds = int.TryParse(Arg(a, "--seconds"), out var s) ? s : 120;
        string? acceptance = AcceptancePath(a);

        var assets = TriggersTool.FindAssetsRoot(obb);
        if (assets is null) { Console.WriteLine($"no animation assets under '{obb}'"); return 1; }

        Console.WriteLine($"connecting to {a[1]}...");
        using var robot = await CozmoRobot.ConnectAsync(IPAddress.Parse(a[1]));
        var lib = robot.Animations.LoadFrom(assets);
        Console.WriteLine($"assets: {lib.ClipNames.Count} clips, {lib.GroupNames.Count} groups");
        var sound = Path.Combine(obb, "assets", "cozmo_resources", "sound");
        if (Directory.Exists(sound))
        {
            var meta = Path.Combine(obb, "sound_meta");
            robot.Animations.AudioSource = Cozmo.Robot.Animation.Wwise.WwiseAudioSource.Load(
                Directory.Exists(meta) ? new[] { sound, meta } : new[] { sound });
        }
        var map = AnimationTriggerMap.Load(obb);
        var mood = new MoodState(MoodModel.Load(obb));
        var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
        var ctx = new BehaviorContext { Robot = robot, Triggers = map, Arbiter = arbiter, Mood = mood };
        var manager = new BehaviorManager(ctx);
        using var vision = new VisionSystem(robot);
        var calibration = await vision.ReadCalibrationAsync(TimeSpan.FromSeconds(3));
        var expect = ParseExpected(Arg(a, "--expect"));
        bool needsCubeVision = expect.Any(x => x is ReactionTrigger.CubeMoved or ReactionTrigger.ObjectPositionUpdated);
        if (needsCubeVision && calibration is null)
        {
            Console.WriteLine("camera calibration was not read from the robot; cube/world reactions cannot be exercised");
            robot.Disconnect();
            return 2;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var log = new List<object>();
        void Line(string text)
        {
            Console.WriteLine($"[{sw.Elapsed.TotalSeconds,7:F2}s] {text}");
            log.Add(new { t = sw.Elapsed.TotalSeconds, text });
        }

        foreach (var reg in ShippedBehaviors.Reactions(robot, vision.Locator, () => sw.Elapsed.TotalSeconds, vision))
        {
            manager.AddReaction(reg.Strategy, reg.Behavior, reg.ResumeLast);
            if (reg.Behavior is SteppedBehavior stepped) stepped.Step += line => Line($"    {reg.Behavior.Id}: {line}");
            if (reg.Behavior is ReactBehavior) { /* M7-style single animation; the switch line is its trace */ }
        }
        manager.ReactionTriggered += r => Line($"REACTION {r.Trigger} -> {r.Behavior}" +
                                               (r.Interrupted is not null ? $" (interrupted {r.Interrupted}{(r.WillResume ? ", will resume" : "")})" : ""));
        manager.Selected += sel => { if (sel.Reason.StartsWith("resumed")) Line($"resumed {sel.Chosen}"); };
        robot.Sensors.OffTreadsStateChanged += (from, to) => Line($"off-treads {from} -> {to}");
        robot.Sensors.UnexpectedMovementDetected += r => Line(r.ToString());

        Console.WriteLine($"classifier {(robot.Sensors.OffTreadsClassifierEnabled ? "enabled" : "waiting for the head calibration report")}");
        Console.WriteLine($"\n{manager.Reactions.Count} reactions registered:");
        foreach (var r in manager.Reactions) Console.WriteLine($"  {r.Strategy.Trigger,-20} -> {r.Behavior.Id}{(r.ResumeLast ? " (resumes last)" : "")}");
        bool provoke = a.Contains("--provoke-movement");
        if (calibration is not null)
        {
            robot.StartCamera();
            Line($"vision pipeline active with robot camera calibration; face detector available={vision.FaceDetector.IsAvailable}");
        }

        var fired = new HashSet<ReactionTrigger>();
        var firedInWindow = new HashSet<ReactionTrigger>();
        manager.ReactionTriggered += r => { fired.Add(r.Trigger); firedInWindow.Add(r.Trigger); };

        async Task Tick(double untilSec)
        {
            while (sw.Elapsed.TotalSeconds < untilSec)
            {
                double nowMs = sw.Elapsed.TotalMilliseconds, nowSec = sw.Elapsed.TotalSeconds;
                mood.Advance(nowSec);
                manager.CheckReactions(nowSec);
                manager.Update(nowMs, nowSec);
                await Task.Delay(33);
            }
        }

        var seen = new Dictionary<ReactionTrigger, bool>();
        if (expect.Count == 0)
        {
            Console.WriteLine($"\nrunning for {seconds}s. Lay him on his back, on a side, on his face; hold him on a slope; shake him; " +
                              "push him sideways while he drives (he only drives if a behaviour drives him). Reactions print as they fire.\n");
            await Tick(seconds);
        }
        else
        {
            // One handling at a time, each in its own window, so what fired can be attributed to what was
            // done. A long open-ended watch cannot tell the reaction it is named after from any other, which
            // is how a check could pass on a reaction nobody asked for.
            double each = Math.Max(15, (double)seconds / expect.Count);
            Console.WriteLine($"\n{expect.Count} thing(s) to try, about {each:F0} s each. Do what each prompt says, when it says it.\n");
            foreach (var want in expect)
            {
                firedInWindow.Clear();
                double until = Math.Min(seconds, sw.Elapsed.TotalSeconds + each);
                Console.WriteLine();
                Console.WriteLine(new string('=', 72));
                Console.WriteLine($"  NOW: {Instruction(want)}");
                Console.WriteLine($"  (watching for {want} for the next {until - sw.Elapsed.TotalSeconds:F0} s)");
                Console.WriteLine(new string('=', 72));

                if (provoke && want == ReactionTrigger.UnexpectedMovement)
                    await ProvokeMovement(robot, manager, mood, sw, until);
                else
                    await Tick(until);

                bool got = firedInWindow.Contains(want);
                seen[want] = got;
                Console.WriteLine($"  window {want}: {(got ? "fired" : "NOT SEEN")}"
                                + (firedInWindow.Count > 0 ? $"   (in this window: {string.Join(", ", firedInWindow.OrderBy(x => x))})" : ""));
            }
        }
        manager.Stop(BehaviorStopReason.Cancelled, sw.Elapsed.TotalSeconds);
        if (calibration is not null) robot.StopCamera();
        await robot.Motion.StopAllAsync();

        Console.WriteLine($"\nreactions fired: {(fired.Count == 0 ? "none" : string.Join(", ", fired.OrderBy(x => x)))}");
        if (expect.Count > 0)
        {
            // The line the acceptance runner judges on. Only the expected reactions count, and only when they
            // fired in their own window: nothing unrelated can satisfy this check.
            Console.WriteLine("expected reactions: " + string.Join(" ", expect.Select(e => $"{e}={(seen.GetValueOrDefault(e) ? "yes" : "no")}")));
            var missing = expect.Where(e => !seen.GetValueOrDefault(e)).ToList();
            Console.WriteLine(missing.Count == 0
                ? "expected reactions: all seen"
                : "expected reactions missing: " + string.Join(", ", missing));
            if (missing.Count > 0)
            {
                robot.Disconnect();
                return 2;
            }
        }
        if (acceptance is not null)
        {
            var record = WriteAcceptance("reactions", acceptance, fired.Count > 0, robot,
                "each reaction did what the engine's does: flip down from the back, roll off the face, ask to be righted on a side, " +
                "react then check the pitch on a slope, act dizzy after a shake, react to a push; no reaction fired for a state he was not in",
                new { fired = fired.Select(f => f.ToString()).OrderBy(x => x), log,
                      classifierEnabled = robot.Sensors.OffTreadsClassifierEnabled });
            Console.WriteLine($"acceptance record: {record}");
        }
        robot.Disconnect();
        return 0;
    }

    /// <summary>The reactions a check says it is about, from <c>--expect Trigger[,Trigger]</c>.</summary>
    private static List<ReactionTrigger> ParseExpected(string? arg)
    {
        var list = new List<ReactionTrigger>();
        if (string.IsNullOrWhiteSpace(arg)) return list;
        foreach (var name in arg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<ReactionTrigger>(name, ignoreCase: true, out var t)) list.Add(t);
            else Console.WriteLine($"  (no reaction trigger called '{name}'; ignoring it)");
        return list;
    }

    /// <summary>
    /// What the person has to do to cause one reaction, in the words they need while holding the robot.
    /// A check that cannot say this is a check nobody can carry out.
    /// </summary>
    private static string Instruction(ReactionTrigger t) => t switch
    {
        ReactionTrigger.RobotOnBack => "lay him on his BACK, flat, and let go",
        ReactionTrigger.RobotOnFace => "lay him FACE DOWN and let go",
        ReactionTrigger.RobotOnSide => "lay him on one SIDE and let go",
        ReactionTrigger.RobotShaken => "pick him up and SHAKE him for a second or two, then set him down on his treads",
        ReactionTrigger.RobotPlacedOnSlope => "put him down on a SLOPE - a book under one end - and let go",
        ReactionTrigger.ReturnedToTreads => "set him back on his treads, the right way up, and let go",
        ReactionTrigger.RobotPickedUp => "pick him straight up and hold him in the air",
        ReactionTrigger.RobotFalling => "hold him a few centimetres above a cushion and DROP him onto it",
        ReactionTrigger.UnexpectedMovement => "when his wheels start turning, HOLD him so he cannot turn - do not lift him",
        ReactionTrigger.CliffDetected => "slide him forward so one front sensor overhangs the edge",
        ReactionTrigger.PlacedOnCharger => "put him down on the charger contacts",
        ReactionTrigger.CubeMoved => "move the cube while he is not looking at it",
        ReactionTrigger.ObjectPositionUpdated => "slide the cube about 10 cm while he is looking at it",
        _ => $"cause {t}",
    };

    /// <summary>
    /// Drives the wheels in short bursts so the person has something to hold against. The detector compares
    /// what the wheels are doing with what the gyro says the body did, so it needs the wheels turning: an
    /// open-ended watch for a push that never happens is not a test of anything.
    /// </summary>
    private static async Task ProvokeMovement(CozmoRobot robot, BehaviorManager manager, MoodState mood,
                                              System.Diagnostics.Stopwatch sw, double untilSec)
    {
        bool driving = false;
        double nextSwitch = sw.Elapsed.TotalSeconds;
        while (sw.Elapsed.TotalSeconds < untilSec)
        {
            if (sw.Elapsed.TotalSeconds >= nextSwitch)
            {
                driving = !driving;
                nextSwitch = sw.Elapsed.TotalSeconds + (driving ? 2.5 : 1.5);
                if (driving)
                {
                    Console.WriteLine($"  [{sw.Elapsed.TotalSeconds,7:F2}s] turning on the spot - HOLD HIM NOW");
                    await robot.Motion.DriveWheelsAsync(40f, -40f, confirmWithin: TimeSpan.FromMilliseconds(400));
                }
                else
                {
                    await robot.Motion.StopAllAsync();
                }
            }
            double nowMs = sw.Elapsed.TotalMilliseconds, nowSec = sw.Elapsed.TotalSeconds;
            mood.Advance(nowSec);
            manager.CheckReactions(nowSec);
            manager.Update(nowMs, nowSec);
            await Task.Delay(33);
        }
        await robot.Motion.StopAllAsync();
    }

    private static double Deg(float? rad) => (rad ?? 0f) * 180.0 / Math.PI;
    private static double Deg(double rad) => rad * 180.0 / Math.PI;

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
            milestone = "M10",
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
