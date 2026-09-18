using System.Globalization;
using System.Net;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>
/// Hardware acceptance runs for the M3 device layer: one command per stateful pipeline.
///
/// Each one connects to a real robot with nothing from the Android app or libcozmoEngine in the path,
/// exercises a single device, and prints a PASS or FAIL line plus the evidence a person can check by
/// looking at the robot or the saved files.
/// </summary>
public static class Devices
{
    private static (string log, StreamWriter writer) OpenLog(string? path, string prefix)
    {
        path = Path.GetFullPath(path ?? $"cozmo-{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        return (path, new StreamWriter(path, false, Encoding.UTF8));
    }

    private static async Task<CozmoRobot> ConnectAsync(IPAddress ip, int port, StreamWriter log)
    {
        var robot = await CozmoRobot.ConnectAsync(ip, port);
        robot.Transport.FrameTrace += e =>
        {
            lock (log) log.WriteLine($"{e.Utc:O} {(e.Outbound ? "TX" : "RX")} {Hex.Dump(e.Raw)}{(e.Error is null ? "" : "  !! " + e.Error)}");
        };
        robot.Transport.Warning += w => Console.WriteLine("  warn: " + w);
        robot.Transport.Disconnected += r => Console.WriteLine($"  disconnected: {r}");
        Console.WriteLine($"connected: firmware v{robot.State.FirmwareVersionNumber?.ToString() ?? "?"} " +
                          $"serial 0x{robot.State.SerialNumber:x8} states={robot.State.StateCount}");
        Console.WriteLine(await robot.WaitForAnimationsAsync()
            ? $"animation controller running (enabled tracks 0x{robot.State.Animation!.EnabledAnimTracks:x2})"
            : "WARNING: the robot never answered the animation-controller init; faces and audio will be ignored");
        return robot;
    }

    private static (IPAddress ip, int port, string? log)? Common(string[] a, out string[] rest)
    {
        rest = a;
        if (a.Length < 2 || !IPAddress.TryParse(a[1], out var ip)) return null;
        int port = 5551; string? log = null;
        for (int i = 2; i < a.Length - 1; i++)
        {
            if (a[i] == "--port") port = int.Parse(a[i + 1]);
            else if (a[i] == "--log") log = a[i + 1];
        }
        return (ip, port, log);
    }

    /// <summary>
    /// Camera acceptance: stream frames from the real camera and save them as JPEG files.
    /// Success means the files open in any image viewer and show the room.
    /// </summary>
    public static async Task<int> Camera(string[] a)
    {
        var c = Common(a, out _);
        if (c is null) return 1;
        int count = 10;
        string outDir = ".";
        bool color = a.Contains("--color");
        for (int i = 2; i < a.Length - 1; i++)
        {
            if (a[i] == "--count") count = int.Parse(a[i + 1]);
            else if (a[i] == "--out") outDir = a[i + 1];
        }
        outDir = Path.GetFullPath(outDir);
        Directory.CreateDirectory(outDir);

        var (logPath, log) = OpenLog(c.Value.log, "camera");
        Console.WriteLine($"frame log: {logPath}");
        Console.WriteLine($"images to: {outDir}");
        using var robot = await ConnectAsync(c.Value.ip, c.Value.port, log);

        var saved = new List<string>();
        robot.Camera.FrameDropped += (id, why) => Console.WriteLine($"  dropped image {id}: {why}");
        robot.Camera.FrameReceived += f =>
        {
            if (f.IsWarmUp)
            {
                Console.WriteLine($"  {f}");
                return;
            }
            if (saved.Count >= count) return;
            var path = Path.Combine(outDir, $"cozmo-{f.ImageId:D5}.jpg");
            f.Save(path);
            saved.Add(path);
            Console.WriteLine($"  {f}  -> {Path.GetFileName(path)}");
        };

        Console.WriteLine($"starting camera ({(color ? "colour" : "grayscale")} stream) ...");
        robot.StartCamera(color);
        var end = DateTime.UtcNow.AddSeconds(30);
        while (saved.Count < count && DateTime.UtcNow < end) await Task.Delay(50);
        robot.StopCamera();
        await Task.Delay(200);
        robot.Disconnect();
        log.Dispose();

        Console.WriteLine();
        Console.WriteLine($"chunks={robot.Camera.ChunksReceived} complete={robot.Camera.FramesCompleted} dropped={robot.Camera.FramesDropped} saved={saved.Count}");
        Console.WriteLine($"({robot.Camera.WarmUpFrames} warm-up frames were discarded: the sensor is still locking and those pictures are torn.)");
        bool ok = saved.Count >= Math.Min(count, 1) && robot.Camera.FramesCompleted > 0;
        foreach (var p in saved.Take(3)) Console.WriteLine($"  {p}");
        Console.WriteLine(ok
            ? "PASS camera: open the saved files; each should be a photograph from Cozmo's point of view."
            : "FAIL camera: no complete frame arrived.");
        return ok ? 0 : 20;
    }

    /// <summary>
    /// Display acceptance: draw a known image on the OLED and hold it long enough to photograph.
    /// Success means the pattern on the robot's face matches the one printed on the console.
    /// </summary>
    public static async Task<int> Face(string[] a)
    {
        var c = Common(a, out _);
        if (c is null) return 1;
        double seconds = 5;
        string pattern = "test";
        string? artFile = null;
        for (int i = 2; i < a.Length - 1; i++)
        {
            if (a[i] == "--seconds") seconds = double.Parse(a[i + 1], CultureInfo.InvariantCulture);
            else if (a[i] == "--pattern") pattern = a[i + 1];
            else if (a[i] == "--file") artFile = a[i + 1];
        }

        var image = artFile is not null
            ? FaceBitmap.FromText(File.ReadAllText(artFile))
            : pattern switch
            {
                "blank" => new FaceBitmap(),
                "full" => Filled(),
                "eyes" => Eyes(),
                _ => FaceBitmap.TestPattern(),
            };
        var payload = FaceBitmapCodec.Encode(image);
        Console.WriteLine($"face image '{artFile ?? pattern}' encodes to {payload.Length} bytes; it should look like:");
        Console.WriteLine(image.ToText());
        Console.WriteLine("decoded back from the payload (must be identical):");
        Console.WriteLine(FaceBitmapCodec.Decode(payload).ToText());
        if (image.ToText() != FaceBitmapCodec.Decode(payload).ToText())
        {
            Console.WriteLine("FAIL display: the payload does not round-trip; refusing to send.");
            return 21;
        }

        var (logPath, log) = OpenLog(c.Value.log, "face");
        Console.WriteLine($"frame log: {logPath}");
        using var robot = await ConnectAsync(c.Value.ip, c.Value.port, log);

        Console.WriteLine($"holding the image on the face for {seconds:F1}s ...");
        robot.Display.Hold(image, TimeSpan.FromSeconds(seconds));
        Console.WriteLine($"sent {robot.Display.FramesSent} face frames; " +
                          $"robot reports {robot.State.Animation?.NumAnimBytesPlayed ?? -1} animation bytes played");
        robot.Display.Clear();
        await Task.Delay(200);
        robot.Disconnect();
        log.Dispose();

        bool ok = robot.Display.FramesSent > 10;
        Console.WriteLine(ok
            ? "PASS display: compare the robot's face with the pattern printed above."
            : "FAIL display: too few frames went out.");
        return ok ? 0 : 21;

        static FaceBitmap Filled() { var f = new FaceBitmap(); f.Fill(); return f; }
        static FaceBitmap Eyes()
        {
            var f = new FaceBitmap();
            f.DrawRect(24, 6, 48, 25, filled: true);
            f.DrawRect(80, 6, 104, 25, filled: true);
            return f;
        }
    }

    /// <summary>
    /// Audio acceptance: play a generated sine tone through the speaker.
    /// Success means a clean, steady note with no clicks or stutter.
    /// </summary>
    public static async Task<int> Tone(string[] a)
    {
        var c = Common(a, out _);
        if (c is null) return 1;
        double hz = 440, seconds = 2, amplitude = 0.5;
        int? volume = null;
        string? wav = null;
        for (int i = 2; i < a.Length - 1; i++)
        {
            if (a[i] == "--hz") hz = double.Parse(a[i + 1], CultureInfo.InvariantCulture);
            else if (a[i] == "--seconds") seconds = double.Parse(a[i + 1], CultureInfo.InvariantCulture);
            else if (a[i] == "--amplitude") amplitude = double.Parse(a[i + 1], CultureInfo.InvariantCulture);
            else if (a[i] == "--volume") volume = int.Parse(a[i + 1]);
            else if (a[i] == "--save") wav = a[i + 1];
        }
        bool unreliable = a.Contains("--unreliable");
        int prime = -1;
        for (int i = 2; i < a.Length - 1; i++) if (a[i] == "--prime") prime = int.Parse(a[i + 1]);

        string sound = "steady", codecName = "anki";
        for (int i = 2; i < a.Length - 1; i++)
        {
            if (a[i] == "--sound") sound = a[i + 1];
            else if (a[i] == "--codec") codecName = a[i + 1];
        }
        var codec = codecName switch
        {
            "anki" => AudioCodec.AnkiMuLaw,
            "mulaw" => AudioCodec.StandardMuLaw,
            "pcm8u" => AudioCodec.UnsignedPcm8,
            "pcm8s" => AudioCodec.SignedPcm8,
            _ => throw new ArgumentException($"unknown codec '{codecName}'; use anki, mulaw, pcm8u or pcm8s"),
        };
        int beeps = Math.Max(1, (int)Math.Round(seconds / 0.5));

        var pcm = sound switch
        {
            "beeps" => CozmoAudio.Beeps(beeps, hz, amplitude: amplitude),
            "sweep" => CozmoAudio.Sweep(220, 880, TimeSpan.FromSeconds(seconds), amplitude),
            _ => CozmoAudio.Tone(hz, TimeSpan.FromSeconds(seconds), amplitude),
        };
        var frames = CozmoAudio.ToFrames(pcm, codec);
        Console.WriteLine($"sound '{sound}' for {seconds:F1}s as {codecName}: {pcm.Length} samples at " +
                          $"{CozmoAudio.SampleRate} Hz = {frames.Count} frames of {CozmoAudio.SamplesPerFrame} bytes");
        Console.WriteLine(sound switch
        {
            "beeps" => $"WHAT TO LISTEN FOR: exactly {beeps} separate beeps, evenly spaced, each about a quarter " +
                       "second long. More than that, or uneven spacing, means the stream is breaking up.",
            "sweep" => "WHAT TO LISTEN FOR: one smooth rise in pitch, low to high. Steps or stalls mean the " +
                       "stream is breaking up.",
            _ => $"WHAT TO LISTEN FOR: one unbroken {hz:F0} Hz note for {seconds:F1}s. It will sound buzzy, " +
                 "because the robot's speaker is small and the audio is 8-bit companded; that is normal. " +
                 "What matters is that it does not cut in and out.",
        });
        Console.WriteLine("Compare with the file from --save, played on this machine: that is exactly what the " +
                          "robot is being sent, so it is the reference for how it should sound.");
        if (codec != AudioCodec.AnkiMuLaw)
            Console.WriteLine("NOTE: --codec anki is the format the engine itself uses; the others are for comparison.");
        if (wav is not null)
        {
            // Write what the robot will actually hear, so it can be listened to on the machine first.
            WriteWav(Path.GetFullPath(wav),
                     frames.SelectMany(f => f).Select(b => CozmoAudio.Unpack(b, codec)).ToArray());
            Console.WriteLine($"companded tone written to {Path.GetFullPath(wav)}");
        }

        var (logPath, log) = OpenLog(c.Value.log, "tone");
        Console.WriteLine($"frame log: {logPath}");
        using var robot = await ConnectAsync(c.Value.ip, c.Value.port, log);
        if (volume is { } v) { Console.WriteLine($"SetAudioVolume {v}"); robot.Audio.SetVolume((ushort)v); }
        robot.Audio.Codec = codec;
        if (unreliable) { robot.AudioReliable = false; Console.WriteLine("sending audio frames unreliably"); }
        if (prime >= 0) { robot.Audio.PrimeFrames = prime; Console.WriteLine($"priming {prime} frames"); }

        int before = robot.State.Animation?.NumAudioFramesPlayed ?? 0;

        // Record what the robot says it is doing while we play, so a single run explains itself.
        var trace = new List<(long ms, int played, int sent)>();
        var timeline = System.Diagnostics.Stopwatch.StartNew();
        int handed = 0;
        robot.Audio.OnFrameSent += () => handed++;
        void watch(RobotMessage m)
        {
            if (m is AnimationState a) lock (trace) trace.Add((timeline.ElapsedMilliseconds, a.NumAudioFramesPlayed, handed));
        }
        robot.Message += watch;

        Console.WriteLine("playing ...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        robot.Audio.Play(pcm);            // paced at the animation tick; the robot buffers only ~14 frames
        robot.Audio.SendSilence();
        sw.Stop();

        // let the robot finish what is still in its buffer before dropping the connection
        int last = -1;
        for (int i = 0; i < 40; i++)
        {
            await Task.Delay(50);
            int now = robot.State.Animation?.NumAudioFramesPlayed ?? 0;
            if (now == last) break;
            last = now;
        }
        robot.Disconnect();
        log.Dispose();

        double expected = seconds * 1000;
        robot.Message -= watch;
        int played = (robot.State.Animation?.NumAudioFramesPlayed ?? 0) - before;

        // A steady stream shows the played count rising about 3 frames per 100 ms and staying a little
        // behind what we handed over. Gaps or a flat stretch say where the sound broke up.
        Console.WriteLine();
        Console.WriteLine("  time ms   sent   played   in flight");
        lock (trace)
            foreach (var t in trace.Where((_, i) => i % 3 == 0))
                Console.WriteLine($"  {t.ms,7}   {t.sent,4}   {t.played - before,6}   {t.sent - (t.played - before),9}");
        var conn = robot.Transport.Connection;
        if (conn is not null)
            Console.WriteLine($"transport: {conn.FramesSent} frames sent, {conn.ResendFrames} resends, {conn.PendingCount} still unacked");
        Console.WriteLine();
        Console.WriteLine($"sent {robot.Audio.FramesSent} frames in {sw.ElapsedMilliseconds} ms (the audio itself is {expected:F0} ms)");
        Console.WriteLine($"robot reports {played} audio frames played, drop count {robot.State.Animation?.ClientDropCount ?? 0}");
        bool ok = robot.Audio.FramesSent == frames.Count + 1 && played > frames.Count / 2;
        Console.WriteLine(ok
            ? "PASS audio: every frame was sent and the robot reports playing them. Judge the sound against " +
              "the guidance above."
            : played <= 0
                ? "FAIL audio: the robot played none of the frames we sent."
                : "FAIL audio: not every frame was sent or played.");
        return ok ? 0 : 22;
    }

    /// <summary>Writes 16-bit mono PCM as a WAV file, for checking the tone on the machine.</summary>
    private static void WriteWav(string path, short[] pcm)
    {
        using var w = new BinaryWriter(File.Create(path));
        int dataBytes = pcm.Length * 2;
        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + dataBytes);
        w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(CozmoAudio.SampleRate); w.Write(CozmoAudio.SampleRate * 2); w.Write((short)2); w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("data")); w.Write(dataBytes);
        foreach (var s in pcm) w.Write(s);
    }
}
