using System.Net;
using Cozmo.Robot;
using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Behavior;

namespace Cozmo.Conformance;

/// <summary>
/// Hardware acceptance for M9: a shipped Singing behaviour on a real robot.
///
/// Runs exactly what the engine's BehaviorSinging runs (switch, get-in, song, get-out) through the M8
/// framework, prints every step, and writes an acceptance record separating what was measured here
/// (the animations completed; the song rendered with so many notes) from what only a person can judge
/// (Cozmo sang, in tune, for the length of the song). This is the one M9 check that needs hardware.
/// </summary>
public static class SingTool
{
    /// <summary>
    /// <c>sing &lt;robot-ip&gt; --obb &lt;dir&gt; (--behavior &lt;Singing_X&gt; | --group &lt;G&gt; --switch &lt;S&gt;) [--seconds 45] [--acceptance [file]]</c>
    /// </summary>
    public static async Task<int> Run(string[] a)
    {
        if (a.Length < 2) { Console.WriteLine("need the robot's address"); return 1; }
        var obb = Arg(a, "--obb");
        if (obb is null) { Console.WriteLine("need --obb <unpacked obb dir>"); return 1; }
        int seconds = int.TryParse(Arg(a, "--seconds"), out var s) ? s : 45;
        string? acceptance = null;
        for (int i = 2; i < a.Length; i++)
            if (a[i] == "--acceptance") acceptance = i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[i + 1] : "";

        var behaviours = SingingBehavior.LoadShipped(obb);
        SingingBehavior? chosen = null;
        if (Arg(a, "--behavior") is { } id) chosen = behaviours.FirstOrDefault(b => b.Id == id);
        else if (Arg(a, "--group") is { } g && Arg(a, "--switch") is { } sw) chosen = new SingingBehavior($"Singing({sw})", g, sw);
        if (chosen is null)
        {
            Console.WriteLine($"name a shipped behaviour with --behavior, one of: {string.Join(", ", behaviours.Select(b => b.Id))}");
            return 1;
        }

        var assets = TriggersTool.FindAssetsRoot(obb);
        if (assets is null) { Console.WriteLine($"no animation assets under '{obb}'"); return 1; }
        var sound = Path.Combine(obb, "assets", "cozmo_resources", "sound");
        if (!Directory.Exists(sound)) { Console.WriteLine($"no sound directory at '{sound}'"); return 1; }

        Console.WriteLine($"connecting to {a[1]}...");
        using var robot = await CozmoRobot.ConnectAsync(IPAddress.Parse(a[1]));
        var lib = robot.Animations.LoadFrom(assets);
        var meta = Path.Combine(obb, "sound_meta");
        var source = WwiseAudioSource.Load(Directory.Exists(meta) ? new[] { sound, meta } : new[] { sound });
        robot.Animations.AudioSource = source;
        Console.WriteLine($"assets: {lib.ClipNames.Count} clips; sound: {source.Library.Banks.Count} banks, " +
                          $"Vorbis {(source.CanDecodeVorbis ? "available" : "UNAVAILABLE - the songs are Vorbis recordings and will be silent")}");

        // The song this behaviour will select, rendered here first so the record says what should be heard.
        var eventName = "Play__Robot_VO__Cozmo_Singing_" + chosen.SwitchGroupName["Cozmo_Sings_".Length..].ToLowerInvariant();
        var eventId = source.Library.IdOf(eventName);
        WwiseRenderedMusic? preview = null;
        if (eventId is { } ev)
        {
            preview = source.RenderMusic(ev, new Dictionary<uint, uint> { [chosen.SwitchGroupId] = chosen.SwitchId });
            Console.WriteLine($"song: {eventName} under {chosen.SwitchGroupName}={chosen.SwitchName}: {preview.DurationMs:F0} ms, " +
                              $"{preview.NotesPlayed} notes sung, {preview.NotesSilent} outside the voice's range, {preview.NoteOffsPlayed} note-offs" +
                              (preview.Problems.Count > 0 ? $"; problems: {string.Join("; ", preview.Problems)}" : ""));
        }

        var arbiter = new BehaviorArbiter { AutonomyEnabled = true };
        var ctx = new BehaviorContext { Robot = robot, Triggers = AnimationTriggerMap.Load(obb), Arbiter = arbiter };
        using var manager = new BehaviorManager(ctx);
        manager.Add(chosen);
        chosen.Trace += t => Console.WriteLine($"  [sing] {t}");

        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        bool started = await manager.StartAsync(chosen.Id, 0);
        if (!started) { Console.WriteLine("the behaviour was not runnable"); robot.Disconnect(); return 2; }
        while (manager.Current is not null && sw2.Elapsed.TotalSeconds < seconds)
        {
            manager.Update(sw2.Elapsed.TotalMilliseconds, sw2.Elapsed.TotalSeconds);
            await Task.Delay(50);
        }
        bool completed = manager.Current is null;
        if (!completed) manager.Stop(BehaviorStopReason.Cancelled, sw2.Elapsed.TotalSeconds);
        var steps = chosen.Steps;
        var misses = source.Misses;
        Console.WriteLine($"\nsteps played: {string.Join(" -> ", steps)}");
        foreach (var m in misses) Console.WriteLine($"  not produced: {m.EventId} {m.Name}: {m.Reason}");

        bool pass = completed && steps.Count == 3 && steps.Any(x => x.Contains("_song_")) &&
                    preview is { NotesPlayed: > 0 } && source.LastMusicRender is { NotesPlayed: > 0 };
        const string human = "Cozmo sang a tune for the length of the song (8 to 12 s) between a get-in and a get-out animation, " +
                             "in his own voice, one note per note; no note-length bursts of get-in phrases";
        var record = Path.GetFullPath(string.IsNullOrEmpty(acceptance) ? $"cozmo-acceptance-sing-{DateTime.Now:yyyyMMdd-HHmmss}.json" : acceptance);
        File.WriteAllText(record, System.Text.Json.JsonSerializer.Serialize(new
        {
            utc = DateTime.UtcNow,
            device = "sing",
            automatedChecksPassed = pass,
            humanCheckRequired = human,
            humanVerdict = "not recorded: set to pass or fail after listening to the robot",
            robot = new
            {
                serial = robot.State.SerialNumber is { } sn ? $"0x{sn:x8}" : null,
                firmware = robot.State.FirmwareVersionNumber,
                batteryVolts = robot.Sensors.BatteryVolts,
            },
            detail = new
            {
                behavior = chosen.Id, chosen.SwitchGroupName, chosen.SwitchName, chosen.SwitchGroupId, chosen.SwitchId,
                tempoTrigger = chosen.TempoTrigger.ToString(),
                steps, completed, elapsedSeconds = sw2.Elapsed.TotalSeconds,
                song = preview is null ? null : new
                {
                    eventName, preview.DurationMs, preview.NotesInWindow, preview.NotesPlayed, preview.NotesSilent,
                    preview.NoteOffsPlayed, preview.ClippedSamples, preview.Peak, preview.Problems,
                },
                notProduced = misses.Select(m => $"{m.EventId} {m.Name}: {m.Reason}"),
                vibrato = "not driven: this stack has no cube accelerometer stream, and the vibrato LFO is not rendered",
            },
            handlerFaults = robot.Transport.HandlerFaults,
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine();
        Console.WriteLine(pass ? "AUTOMATED CHECKS PASSED (sing): switch posted, three animations completed, the song rendered."
                               : "AUTOMATED CHECKS FAILED (sing).");
        Console.WriteLine($"HUMAN CHECK REQUIRED: {human}");
        Console.WriteLine($"acceptance record: {record}");
        robot.Disconnect();
        return pass ? 0 : 3;
    }

    private static string? Arg(string[] a, string name)
    {
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }
}
