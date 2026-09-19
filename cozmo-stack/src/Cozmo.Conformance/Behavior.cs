using System.Net;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;

namespace Cozmo.Conformance;

/// <summary>
/// Hardware acceptance for M7: the reactive and idle layers running on a real robot.
///
/// Prints every decision — played, suppressed, on cooldown, unresolved — so what the robot does can be
/// compared against what it decided, rather than guessed at from the outside.
/// </summary>
public static class BehaviorTool
{
    /// <summary>
    /// <c>behavior &lt;robot-ip&gt; --obb &lt;dir&gt; [--seconds 60] [--no-idle] [--no-react] [--allow-motion]</c>
    /// </summary>
    public static async Task<int> Run(string[] a)
    {
        if (a.Length < 2) { Console.WriteLine("need the robot's address"); return 1; }
        var obb = Arg(a, "--obb");
        if (obb is null) { Console.WriteLine("need --obb <unpacked obb dir>"); return 1; }
        int seconds = int.TryParse(Arg(a, "--seconds"), out var s) ? s : 60;
        bool idleOn = !a.Contains("--no-idle");
        bool reactOn = !a.Contains("--no-react");
        bool allowMotion = a.Contains("--allow-motion");

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
            var src = Cozmo.Robot.Animation.Wwise.WwiseAudioSource.Load(
                Directory.Exists(meta) ? new[] { sound, meta } : new[] { sound });
            robot.Animations.AudioSource = src;
            Console.WriteLine($"sound: {src.Library.EventIds.Count} events, " +
                              $"Vorbis {(src.CanDecodeVorbis ? "available" : "UNAVAILABLE")}");
        }

        var map = AnimationTriggerMap.Load(obb);
        Console.WriteLine($"triggers: {map.Count} mapped");

        var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
        arbiter.Decided += d => Console.WriteLine($"  [arbiter] {d}");

        using var reactive = new ReactiveBehavior(robot, map, arbiter: arbiter);
        var idle = new IdleBehavior(robot, arbiter) { Execute = true };

        // Head and lift movements are small, but they are still movement, so they stay opt-in. The face
        // is not: blinking and eye darts are the only visible sign that keep-alive is running, and
        // switching them off with the motors left a no-motion acceptance run with nothing to watch.
        idle.Execute = true;
        idle.ExecuteMotors = allowMotion;
        idle.Acted += e =>
        {
            if (e.Suppressed is not null) return;
            bool motor = e.Action is IdleAction.HeadMove or IdleAction.LiftMove or IdleAction.BodyMove;
            Console.WriteLine(motor && !allowMotion
                ? $"  [idle] {e.Action} decided but not driven (--allow-motion is off)"
                : $"  [idle] {e.Action} {e.Amount:F1} over {e.DurationMs:F0} ms");
        };

        if (reactOn) reactive.Start();
        Console.WriteLine($"\nwatching for {seconds}s. " +
                          (reactOn ? "Pick the robot up, put it on a cliff edge, place it on the charger. " : "") +
                          (idleOn ? "Otherwise leave it alone and watch it blink." : ""));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        int idleActions = 0;
        idle.Acted += e => { if (e.Suppressed is null) idleActions++; };
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            if (idleOn) idle.Advance(sw.Elapsed.TotalMilliseconds);
            await Task.Delay(50);
        }

        reactive.Stop();
        Console.WriteLine($"\nidle actions taken: {idleActions}");
        Console.WriteLine("reaction cooldowns and suppressions are in the [arbiter] lines above.");
        robot.Disconnect();
        return 0;
    }

    private static string? Arg(string[] a, string name)
    {
        for (int i = 1; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }
}
