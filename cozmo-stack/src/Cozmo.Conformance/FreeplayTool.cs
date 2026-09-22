using System.Net;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Conformance;

/// <summary>
/// M15: the autonomy layer. <c>freeplay --tree --obb &lt;dir&gt;</c> prints the shipped activity tree with which
/// behaviours this stack binds; <c>freeplay --simulate --obb &lt;dir&gt; [--ticks 200]</c> runs the freeplay
/// decision loop offline on a disconnected robot (most behaviours are not runnable without a robot, so it
/// shows the selection reasons); <c>freeplay &lt;robot-ip&gt; --obb &lt;dir&gt; [--seconds 300] [--acceptance [file]]</c>
/// runs the whole stack on the robot: vision, manipulation, reactions, needs and freeplay, printing every
/// activity and behaviour decision (hardware item Z).
/// </summary>
public static class FreeplayTool
{
    public static async Task<int> Run(string[] a)
    {
        var obb = Arg(a, "--obb");
        if (a.Length < 2 || obb is null) { Console.WriteLine("freeplay --tree --obb <dir> | freeplay --simulate --obb <dir> [--ticks 200] | freeplay <robot-ip> --obb <dir> [--seconds 300] [--acceptance [file]]"); return 1; }
        if (a[1] == "--tree") return Tree(obb);
        if (a[1] == "--simulate") return Simulate(obb, int.TryParse(Arg(a, "--ticks"), out var t) ? t : 200);
        return await OnRobot(a, obb);
    }

    private static int Tree(string obb)
    {
        using var robot = CozmoRobot.CreateOffline();
        using var vision = new VisionSystem(robot, CameraCalibration.Nominal());
        using var m = new ManipulationSystem(robot, vision);
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap() };
        using var stack = FreeplayStack.Create(obb, robot, ctx, () => 0, vision, m, withReactions: false);
        foreach (var act in stack.Tree) Print(act, stack, 0);
        Console.WriteLine($"\nbound behaviours: {stack.Bound.Count}; named by the tree but not built: {stack.UnboundIds.Count}");
        foreach (var id in stack.UnboundIds) Console.WriteLine($"  - {id}");
        if (stack.Problems.Count > 0) { Console.WriteLine("problems:"); foreach (var p in stack.Problems) Console.WriteLine("  " + p); }
        return 0;
    }

    private static void Print(Activity act, FreeplayStack stack, int depth)
    {
        string pad = new(' ', depth * 2);
        Console.WriteLine($"{pad}{act.Id} [{act.Type}] priority {act.Priority}, strategy {act.Strategy.Type}" +
                          (act.Strategy.ShouldEndDurationSec >= 0 ? $" ends after {act.Strategy.ShouldEndDurationSec}s" : "") +
                          (act.Strategy.CooldownBaseSec > 0 ? $" cooldown {act.Strategy.CooldownBaseSec}s" : "") +
                          (act.RequireSpark is not null ? $" spark {act.RequireSpark}" : ""));
        if (act.Chooser is not null)
        {
            Console.WriteLine($"{pad}  chooser {act.Chooser.Type}:");
            foreach (var id in act.Chooser.BehaviorIds)
            {
                string score = act.Chooser is ScoringChooser sc ? $" ({sc.Entries.First(e => e.BehaviorId == id).FlatScore})" : "";
                Console.WriteLine($"{pad}    {(stack.Bound.ContainsKey(id) ? "+" : "-")} {id}{score}{(stack.Bound.ContainsKey(id) ? "" : "  (not built)")}");
            }
        }
        if (act.InterludeChooser is not null) Console.WriteLine($"{pad}  interludes: {string.Join(", ", act.InterludeChooser.BehaviorIds.Select(i => (stack.Bound.ContainsKey(i) ? "+" : "-") + i))}");
        foreach (var s in act.SubActivities) Print(s, stack, depth + 1);
    }

    private static int Simulate(string obb, int ticks)
    {
        using var robot = CozmoRobot.CreateOffline();
        var assets = TriggersTool.FindAssetsRoot(obb);
        if (assets is not null) robot.Animations.LoadFrom(assets);
        using var vision = new VisionSystem(robot, CameraCalibration.Nominal());
        using var m = new ManipulationSystem(robot, vision);
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Mood = new MoodState(MoodModel.Load(obb)) };
        double clock = 0;
        using var stack = FreeplayStack.Create(obb, robot, ctx, () => clock, vision, m, withReactions: false);
        stack.Freeplay.Log += l => Console.WriteLine("  " + l);
        stack.Needs.Log += l => Console.WriteLine("  needs: " + l);
        Console.WriteLine($"offline robot, {stack.Bound.Count} behaviours bound; ticking {ticks} times at 0.5 s");
        FreeplayDecision? last = null;
        for (int i = 0; i < ticks; i++)
        {
            clock = i * 0.5;
            if (i == ticks / 2) { Console.WriteLine("  -> energy set critical"); stack.Needs.SetLevel(NeedId.Energy, 0); }
            var d = stack.Tick(clock, clock * 1000, robot, vision, m);
            if (last is null || d.Activity != last.Activity || d.Behavior != last.Behavior) Console.WriteLine($"[{d.AtSec,6:F1}s] activity {d.Activity ?? "-"} behaviour {d.Behavior ?? "-"}: {Trim(d.Reason)}");
            last = d;
        }
        return 0;
    }

    private static string Trim(string s) => s.Length > 300 ? s[..300] + "…" : s;

    private static async Task<int> OnRobot(string[] a, string obb)
    {
        int seconds = int.TryParse(Arg(a, "--seconds"), out var s) ? s : 300;
        string? acceptance = AcceptancePath(a);
        var assets = TriggersTool.FindAssetsRoot(obb);
        if (assets is null) { Console.WriteLine($"no animation assets under '{obb}'"); return 1; }
        Console.WriteLine($"connecting to {a[1]}...");
        using var robot = await CozmoRobot.ConnectAsync(IPAddress.Parse(a[1]));
        robot.Animations.LoadFrom(assets);
        var sound = Path.Combine(obb, "assets", "cozmo_resources", "sound");
        if (Directory.Exists(sound)) robot.Animations.AudioSource = Cozmo.Robot.Animation.Wwise.WwiseAudioSource.Load(new[] { sound });
        using var vision = new VisionSystem(robot);
        using var m = new ManipulationSystem(robot, vision);
        var log = new List<string>();
        bool nominal = false;
        void Say(string line) { Console.WriteLine(line); log.Add(line); }
        var cal = await vision.ReadCalibrationAsync(TimeSpan.FromSeconds(3));
        if (cal is null)
        {
            // Freeplay drives and manipulates cubes on its own. Substituting a made-up camera geometry here
            // would put every object in the wrong place while the robot acts on it, so this fails closed the
            // way the engine refuses to run vision without a calibration. --nominal is an explicit,
            // labelled diagnostic override, as it already is for the manipulation tool.
            if (!a.Contains("--nominal"))
            {
                Say("camera calibration NOT READ from NV storage: refusing to run autonomous freeplay on a made-up geometry.");
                Say("re-run with --nominal to force the LOCAL nominal stand-in (diagnostics only; he will misjudge where cubes are).");
                robot.Disconnect();
                return 2;
            }
            vision.Calibration = CameraCalibration.Nominal();
            Say("UNSAFE: camera calibration not read; using the nominal stand-in because --nominal was given (LOCAL_POLICY)");
            nominal = true;
        }
        var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
        var ctx = new BehaviorContext { Robot = robot, Triggers = AnimationTriggerMap.Load(obb), Arbiter = arbiter, Mood = new MoodState(MoodModel.Load(obb)) };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var stack = FreeplayStack.Create(obb, robot, ctx, () => sw.Elapsed.TotalSeconds, vision, m);
        stack.Freeplay.Log += l => Say("  " + l);
        stack.Manager.Selected += sel => Say($"  [manager] {sel.Chosen ?? "-"}: {sel.Reason}");
        stack.Manager.ReactionTriggered += r => Say($"  REACTION {r.Trigger} -> {r.Behavior}");
        // the put-down re-pick is wired by FreeplayStack itself; nothing to install here
        robot.Cubes.SetDiscovery(true);
        await robot.WaitForMotorCalibrationAsync(TimeSpan.FromSeconds(10));
        robot.StartCamera();
        Say($"freeplay for {seconds} s: {stack.Bound.Count} behaviours bound, {stack.UnboundIds.Count} named but not built");
        FreeplayDecision? last = null;
        var activities = new HashSet<string>(); var behaviours = new HashSet<string>();
        double lastMood = -1;
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            var d = stack.Tick(sw.Elapsed.TotalSeconds, sw.Elapsed.TotalMilliseconds, robot, vision, m);
            if (last is null || d.Activity != last.Activity || d.Behavior != last.Behavior) Say($"[{d.AtSec,6:F1}s] activity {d.Activity ?? "-"} behaviour {d.Behavior ?? "-"}: {Trim(d.Reason)}");
            if (d.Activity is not null) activities.Add(d.Activity);
            if (d.Behavior is not null) behaviours.Add(d.Behavior);
            last = d;

            // The mood, every half minute. It decays on every tick and the behaviour scoring reads it, so a
            // long run is where holding still instead of decaying shows: the emotions should drift back
            // towards neutral between the events that move them.
            if (ctx.Mood is { } mood && sw.Elapsed.TotalSeconds - lastMood >= 30)
            {
                lastMood = sw.Elapsed.TotalSeconds;
                Say($"[{sw.Elapsed.TotalSeconds,6:F1}s] mood: " + string.Join(", ",
                    Enum.GetValues<EmotionType>().Select(e => $"{e}={mood[e]:F3}")));
            }
            await Task.Delay(50);
        }
        stack.Manager.Stop(BehaviorStopReason.Cancelled, sw.Elapsed.TotalSeconds);
        robot.StopCamera();
        Say($"\nactivities run: {string.Join(", ", activities)}; behaviours run: {string.Join(", ", behaviours)}");
        if (acceptance is not null)
        {
            var file = Path.GetFullPath(string.IsNullOrEmpty(acceptance) ? $"cozmo-acceptance-freeplay-{DateTime.Now:yyyyMMdd-HHmmss}.json" : acceptance);
            var record = new
            {
                tool = "freeplay", milestone = "M15", timestampUtc = DateTime.UtcNow, firmware = robot.State.FirmwareVersionNumber, serial = robot.State.SerialNumber,
                cameraCalibration = nominal ? "NOMINAL STAND-IN (--nominal; not the robot's)" : "read from the robot's NV storage",
                automated = new { pass = behaviours.Count >= 2 && activities.Count >= 1, detail = new { activities, behaviours, log } },
                human = new { check = "left alone with a cube he chose an activity from what he saw, ran several behaviours in turn (drove off the charger first if he was on it), reacted to being handled, and expressed a need when one ran low; nothing looked stuck or repeated back to back", verdict = "PENDING - fill in after watching the robot" },
            };
            File.WriteAllText(file, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"acceptance record: {file}");
        }
        robot.Disconnect();
        return 0;
    }

    private static string? Arg(string[] a, string name) { for (int i = 1; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1]; return null; }
    private static string? AcceptancePath(string[] a) { for (int i = 1; i < a.Length; i++) if (a[i] == "--acceptance") return i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[i + 1] : ""; return null; }
}
