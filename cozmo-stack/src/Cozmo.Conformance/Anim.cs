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
        if (assets is null || (name is null && group is null))
        {
            Console.WriteLine("need --assets <dir> and one of --name <clip> or --group <group>");
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

        var lib = robot.Animations.LoadFrom(assets);
        Console.WriteLine($"{lib.ClipNames.Count} clips, {lib.GroupNames.Count} groups loaded");

        var events = new List<string>();
        var skipped = new List<string>();
        robot.Animations.Event += e => { Console.WriteLine($"  event: {e}"); events.Add(e); };
        robot.Animations.NotImplemented += w => { if (skipped.Count < 20) skipped.Add(w); };

        string clipName = name ?? "";
        AnimationClip clip;
        if (group is not null)
        {
            var g = lib.GetGroup(group);
            if (g is null) { Console.WriteLine($"no group '{group}'"); robot.Disconnect(); return 1; }
            var pick = g.Choose(new Random());
            clipName = pick!.Name;
            Console.WriteLine($"group '{group}' chose '{clipName}'");
        }
        if (!lib.HasClip(clipName)) { Console.WriteLine($"no clip '{clipName}'"); robot.Disconnect(); return 1; }
        clip = lib.GetClip(clipName);

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
