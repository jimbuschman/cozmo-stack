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
        // The idle is the streamer's own: AnimationStreamer::Update runs UpdateLiveAnimation and the face keep-alive when
        // ProceduralLive is the top of the idle stack. The engine has no pusher of ProceduralLive (R-BEH2 check 2 section
        // 1.8, a RECOVERABLE_GAP), so this tool pushes it through the streamer's PushIdleAnimation seam, as a tool must.
        // --allow-motion is this TOOL's safety opt-in, not an engine gate: the engine has no such switch. Once ProceduralLive is the
        // idle, UpdateLiveAnimation wiggles the head, lift and wheels as it always does, and the face blinks once anything has streamed
        // (+0x88 > 0, 0x0057CF6A; an empty live animation never streams, so the first keyframes come about a second in).
        const string IdleLock = "BehaviorTool";
        if (idleOn && allowMotion)
            robot.Animations.Scheduler.PushIdleAnimation(AnimationTrigger.ProceduralLive, IdleLock);
        else if (idleOn)
            Console.WriteLine("idle: not started (this tool starts the engine's live idle only with --allow-motion, because it moves the head, lift and wheels)");

        if (reactOn) reactive.Start();
        Console.WriteLine($"\nwatching for {seconds}s. " +
                          (reactOn ? "Pick the robot up, put it on a cliff edge, place it on the charger. " : "") +
                          (idleOn && allowMotion ? "Otherwise leave it alone and watch it blink." : ""));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < seconds) await Task.Delay(50);

        reactive.Stop();
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
