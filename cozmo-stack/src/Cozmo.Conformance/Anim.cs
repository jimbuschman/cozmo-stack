using System.Globalization;
using System.Net;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;

namespace Cozmo.Conformance;

/// <summary>
/// Hardware acceptance for the animation system and the procedural face.
///
/// Both are things only a person in the room can judge, so these report what was scheduled and leave the
/// verdict to the operator, as the other device commands do.
/// </summary>
public static class Anim
{
    private static (IPAddress ip, int port)? Target(string[] a)
    {
        if (a.Length < 2 || !IPAddress.TryParse(a[1], out var ip)) return null;
        int port = 5551;
        for (int i = 2; i < a.Length - 1; i++) if (a[i] == "--port") port = int.Parse(a[i + 1]);
        return (ip, port);
    }

    private static string? Arg(string[] a, string flag)
    {
        for (int i = 2; i < a.Length - 1; i++) if (a[i] == flag) return a[i + 1];
        return null;
    }

    private static double Num(string[] a, string flag, double fallback)
    {
        var v = Arg(a, flag);
        return v is null ? fallback : double.Parse(v, CultureInfo.InvariantCulture);
    }

    /// <summary>Lists what is in an asset tree without needing a robot.</summary>
    public static int List(string[] a)
    {
        var root = a.Length > 1 ? a[1] : null;
        if (root is null) { Console.WriteLine("usage: animlist <assets-dir> [name-filter]"); return 1; }
        var lib = AnimationLibrary.Open(root);
        var filter = a.Length > 2 ? a[2] : null;

        var names = lib.ClipNames.Where(n => filter is null || n.Contains(filter, StringComparison.OrdinalIgnoreCase))
                                 .OrderBy(n => n).ToList();
        Console.WriteLine($"{lib.ClipNames.Count} clips, {lib.GroupNames.Count} groups in {root}");
        Console.WriteLine($"showing {names.Count}:\n");
        foreach (var n in names)
        {
            var c = lib.GetClip(n);
            Console.WriteLine($"  {c.Name,-44} {c.DurationMs,6} ms  {c.Keyframes.Count,4} kf  {c.Tracks}");
        }
        if (filter is null)
        {
            Console.WriteLine($"\ngroups: {string.Join(", ", lib.GroupNames.OrderBy(g => g).Take(40))}");
        }
        return 0;
    }

    /// <summary>Plays one of Cozmo's own animations on the robot.</summary>
    public static async Task<int> Play(string[] a)
    {
        if (Target(a) is not var (ip, port)) { Console.WriteLine("usage: anim <robot-ip> --assets <dir> --name <clip>|--group <group>"); return 1; }
        var assets = Arg(a, "--assets");
        var name = Arg(a, "--name");
        var group = Arg(a, "--group");
        bool arc = a.Contains("--arc");
        float arcRadius = (float)Num(a, "--arc-radius", 60);
        float arcSpeed = (float)Num(a, "--arc-speed", 30);
        double arcSeconds = Num(a, "--arc-seconds", 1.0);
        var audio = ParseAudioMappings(a);

        if (!arc && assets is null)
        {
            Console.WriteLine("need --assets <dir> and one of --name <clip> or --group <group>, or --arc");
            return 1;
        }
        if (!arc && name is null && group is null)
        {
            Console.WriteLine("need one of --name <clip> or --group <group>, or --arc");
            return 1;
        }

        var logPath = Path.GetFullPath($"cozmo-anim-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        using var log = new StreamWriter(logPath, false, Encoding.UTF8);
        Console.WriteLine($"frame log: {logPath}");

        CozmoRobot robot;
        try { robot = await CozmoRobot.ConnectAsync(ip, port); }
        catch (Exception e) { Console.WriteLine($"FAIL connect: {e.Message}"); return 10; }
        using var _r = robot;
        robot.Transport.FrameTrace += e => { lock (log) log.WriteLine($"{e.Utc:O} {(e.Outbound ? "TX" : "RX")} {Hex.Dump(e.Raw)}"); };

        var (ready, why) = await robot.WaitUntilReadyAsync(TimeSpan.FromSeconds(20), describe: true);
        Console.WriteLine(ready ? $"ready: {why}" : $"NOT READY: {why}");
        if (!ready) { robot.Disconnect(); return 10; }

        AnimationLibrary? lib = null;
        if (assets is not null)
        {
            lib = robot.Animations.LoadFrom(assets);
            Console.WriteLine($"{lib.ClipNames.Count} clips, {lib.GroupNames.Count} groups loaded");
            var names = robot.Animations.LoadSoundNames(Path.Combine(assets, "..", "sound"))
                        ?? robot.Animations.LoadSoundNames(assets);
            if (names is not null)
                Console.WriteLine($"sound metadata: {names.EventCount} events, {names.FileCount} files (names only)");
        }

        if (audio.Count > 0)
        {
            var source = new WavAudioSource(robot.Animations.SoundNames);
            foreach (var (id, path) in audio)
            {
                source.Add(id, path);
                var evName = robot.Animations.SoundNames?.NameOf(id);
                Console.WriteLine($"audio: event {id}{(evName is null ? "" : $" ({evName})")} -> {Path.GetFileName(path)}");
            }
            robot.Animations.AudioSource = source;
        }

        var events = new List<string>();
        var skipped = new List<string>();
        robot.Animations.Event += e => { Console.WriteLine($"  event: {e}"); events.Add(e); };
        robot.Animations.NotImplemented += w => { if (skipped.Count < 20) skipped.Add(w); };

        AnimationClip clip;
        if (arc)
        {
            clip = BuildArcClip(arcRadius, arcSpeed, arcSeconds);
            Console.WriteLine($"synthetic arc clip: radius {arcRadius:F0} mm, speed {arcSpeed:F0} mm/s, " +
                              $"{arcSeconds:F1}s, then an equal arc back the other way");
        }
        else
        {
            string clipName = name ?? "";
            if (group is not null)
            {
                var g = lib!.GetGroup(group);
                if (g is null) { Console.WriteLine($"no group '{group}'"); robot.Disconnect(); return 1; }
                var pick = g.Choose(new Random());
                clipName = pick!.Name;
                Console.WriteLine($"group '{group}' chose '{clipName}'");
            }
            if (!lib!.HasClip(clipName)) { Console.WriteLine($"no clip '{clipName}'"); robot.Disconnect(); return 1; }
            clip = lib.GetClip(clipName);
        }

        // Anything that moves the body gets the robot's own cliff reflex switched on first, exactly as the
        // drive acceptance command does. This is never skipped, including for the synthetic arc.
        if ((clip.Tracks & AnimationTrack.Body) != 0)
        {
            robot.Sensors.SetStopOnCliff(true);
            Console.WriteLine("stop-on-cliff enabled before any body motion");
            await Task.Delay(100);
        }

        Console.WriteLine($"playing {clip}");
        Console.WriteLine($"  tracks: {clip.Tracks}");
        foreach (var kind in clip.Keyframes.GroupBy(k => k.GetType().Name).OrderBy(g2 => g2.Key))
            Console.WriteLine($"    {kind.Key,-28} {kind.Count()}");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var done = robot.Animations.Play(clip);
        var reason = done is null ? AnimationEndReason.Error : await done;
        sw.Stop();

        Console.WriteLine($"\nfinished: {reason} after {sw.ElapsedMilliseconds} ms (clip is {clip.DurationMs} ms)");
        Console.WriteLine($"keyframes fired: {robot.Animations.Scheduler.KeyframesFired} of {clip.Keyframes.Count}");
        if ((clip.Tracks & AnimationTrack.Audio) != 0)
            Console.WriteLine($"audio frames streamed: {robot.Animations.Scheduler.AudioFramesSent}" +
                              (audio.Count == 0 ? " (all silent: no --audio mapping was given)" : ""));
        if (events.Count > 0) Console.WriteLine($"events raised: {string.Join(", ", events.Distinct())}");
        foreach (var s in skipped.Distinct()) Console.WriteLine($"  not implemented: {s}");

        robot.EmergencyStop();
        await Task.Delay(200);

        bool pass = reason == AnimationEndReason.Completed
                    && robot.Animations.Scheduler.KeyframesFired == clip.Keyframes.Count
                    && Math.Abs(sw.ElapsedMilliseconds - clip.DurationMs) < Math.Max(500, clip.DurationMs * 0.25);

        Console.WriteLine();
        Console.WriteLine(pass
            ? "AUTOMATED CHECKS PASSED (animation): every keyframe fired and the timeline ran to length."
            : "AUTOMATED CHECKS FAILED (animation).");
        Console.WriteLine("HUMAN CHECK REQUIRED: the robot should have moved its head and lift and changed " +
                          "expression in time with each other. Audio and backpack lights are not implemented yet, " +
                          "so their keyframes pass silently.");
        robot.Disconnect();
        return pass ? 0 : 30;
    }


    /// <summary>Parses repeated <c>--audio &lt;eventId&gt;=&lt;file.wav&gt;</c> options.</summary>
    private static List<(long Id, string Path)> ParseAudioMappings(string[] a)
    {
        var list = new List<(long, string)>();
        for (int i = 2; i < a.Length - 1; i++)
        {
            if (a[i] != "--audio") continue;
            var spec = a[i + 1];
            int eq = spec.IndexOf('=');
            if (eq <= 0) { Console.WriteLine($"ignoring --audio '{spec}': expected <eventId>=<file.wav>"); continue; }
            if (!long.TryParse(spec[..eq], out var id))
            { Console.WriteLine($"ignoring --audio '{spec}': '{spec[..eq]}' is not an event id"); continue; }
            list.Add((id, spec[(eq + 1)..]));
        }
        return list;
    }

    /// <summary>
    /// A synthetic clip that exercises arc body motion and nothing else.
    ///
    /// Deliberately conservative: it arcs one way for the given time, pauses, arcs back the other way by
    /// the same amount, and ends stopped, so the robot finishes roughly where it started. Speed and
    /// duration default well below the 200 mm/s the robot accepts. No shipped clip in this build uses an
    /// arc, which is why this has to be built rather than chosen.
    /// </summary>
    public static AnimationClip BuildArcClip(float radiusMm, float speedMmps, double seconds)
    {
        uint dur = (uint)Math.Clamp(seconds * 1000, 100, 5000);
        short speed = (short)Math.Clamp(speedMmps, -100, 100);
        short radius = (short)Math.Clamp(radiusMm, 1, short.MaxValue - 1);
        uint gap = 500;

        var frames = new List<Keyframe>
        {
            new BodyKeyframe(0, dur, radius.ToString(), speed),
            new BodyKeyframe(dur + gap, dur, (-radius).ToString(), speed),
        };
        return new AnimationClip
        {
            Name = "synthetic_arc_test",
            Keyframes = frames,
            Tracks = AnimationTrack.Body,
            DurationMs = dur + gap + dur,
        };
    }

    /// <summary>Shows each built-in procedural expression in turn.</summary>
    public static async Task<int> Face(string[] a)
    {
        if (Target(a) is not var (ip, port)) { Console.WriteLine("usage: face-expressions <robot-ip> [--seconds 2]"); return 1; }
        double hold = Num(a, "--seconds", 2);

        CozmoRobot robot;
        try { robot = await CozmoRobot.ConnectAsync(ip, port); }
        catch (Exception e) { Console.WriteLine($"FAIL connect: {e.Message}"); return 10; }
        using var _r = robot;

        var (ready, why) = await robot.WaitUntilReadyAsync(TimeSpan.FromSeconds(20), describe: true);
        Console.WriteLine(ready ? $"ready: {why}" : $"NOT READY: {why}");
        if (!ready) { robot.Disconnect(); return 10; }

        var shown = new List<string>();
        foreach (var e in Enum.GetValues<Expression>())
        {
            var bmp = ProceduralFaceRenderer.Render(Expressions.Get(e));
            int lit = 0;
            for (int y = 0; y < FaceBitmap.Height; y++)
                for (int x = 0; x < FaceBitmap.Width; x++) if (bmp[x, y] != 0) lit++;
            Console.WriteLine($"\n{e} ({lit} lit pixels):");
            Console.WriteLine(bmp.ToText());
            robot.Display.Hold(bmp, TimeSpan.FromSeconds(hold));
            shown.Add(e.ToString());
        }
        robot.Display.Clear();

        Console.WriteLine();
        Console.WriteLine($"AUTOMATED CHECKS PASSED (face): {shown.Count} expressions rendered and sent.");
        Console.WriteLine("HUMAN CHECK REQUIRED: each face on the robot should match the art printed above it. " +
                          "These expressions are our own construction from the engine's parameter names, not " +
                          "Anki's originals, so they will not match the retail robot exactly.");
        robot.Disconnect();
        return 0;
    }
}
