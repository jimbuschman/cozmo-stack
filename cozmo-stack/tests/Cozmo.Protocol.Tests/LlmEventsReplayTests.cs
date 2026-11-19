using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using System.Security.Cryptography;
using System.Diagnostics;
using Cozmo.Robot.Emulation;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
using Xunit.Abstractions;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class LlmEventsReplayTests(ITestOutputHelper output) : IDisposable
{
    // The recording starts a fresh process: ObjectID bss=0 and the unique-type
    // map empty (0x0105DFF0 / SetID 0x004EF468). The nonparallel collection keeps
    // this initial-state seam from resetting another fixture's process statics.
    public void Dispose() => ObjectIdSpace.ResetForTests();

    private sealed record Packet(double At, byte[] Raw);
    private sealed record Call(double At, string Name, JsonElement Arguments);
    private static readonly HashSet<string> ScopeKinds = ["face-seen", "face-recognised", "face-id-changed", "face-lost", "cube-seen", "charger-seen", "cube-moved", "cube-tapped", "object-pose-lost"];
    private static readonly HashSet<ushort> ScopeTags = [0x46, 0x47, 0x4B, 0x44, 0x0F, 0x10, 0x13, 0x52];
    private static StreamReader Open(string file) => new(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));

    [Fact]
    public async Task AvailableSessionFramesRetainEveryEngineEmissionInMcp()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_LLM_EVENTS_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "llm_session_events.json")));
        var reference = fixture.RootElement;
        using (var frames = File.OpenRead(Path.Combine(bundle, "frames.jsonl")))
            Assert.Equal(reference.GetProperty("framesSha256").GetString(),
                Convert.ToHexString(SHA256.HashData(frames)).ToLowerInvariant());
        double end = double.TryParse(Environment.GetEnvironmentVariable("COZMO_LLM_EVENTS_END_MS"),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cutoff) ? cutoff : reference.GetProperty("frameEndMs").GetDouble();
        var packets = new List<Packet>();
        using (var input = Open(Path.Combine(bundle, "frames.jsonl")))
            while (input.ReadLine() is { } line)
            {
                using var doc = JsonDocument.Parse(line); var r = doc.RootElement;
                double at = r.GetProperty("atMs").GetDouble();
                if (at > end) break;
                if (!r.GetProperty("outbound").GetBoolean()) packets.Add(new(at, Convert.FromBase64String(r.GetProperty("rawBase64").GetString()!)));
            }
        var calls = new List<Call>();
        var recordedTicks = new List<double>();
        uint seed = 0;
        using (var input = Open(Path.Combine(bundle, "events.jsonl")))
            while (input.ReadLine() is { } line)
            {
                using var doc = JsonDocument.Parse(line); var r = doc.RootElement;
                double at = r.GetProperty("atMs").GetDouble();
                if (at > end) break;
                if (r.GetProperty("kind").GetString() == "engine-to-game" && r.GetProperty("tag").GetString() == "0x61") recordedTicks.Add(at);
                if (r.GetProperty("kind").GetString() == "engine-log" && r.GetProperty("name").GetString() == "app.random_seed")
                    seed = uint.Parse(r.GetProperty("text").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                if (r.GetProperty("kind").GetString() == "mcp-input" && r.GetProperty("method").GetString() == "tools/call")
                {
                    var p = r.GetProperty("params");
                    calls.Add(new(at, p.GetProperty("name").GetString()!, p.GetProperty("arguments").Clone()));
                }
            }
        Assert.NotEmpty(packets);
        Assert.Equal(reference.GetProperty("seed").GetUInt32(), seed);
        Assert.Equal(reference.GetProperty("recordedMoodTicksMs").EnumerateArray()
            .Select(v => v.GetDouble()).Where(t => t <= end), recordedTicks);
        // Use the frozen numeric capture's host clock. Its events are a recorded
        // stack cross-check, not an independent native occurrence oracle.
        recordedTicks = reference.GetProperty("recordedMoodTicksMs").EnumerateArray()
            .Select(v => v.GetDouble()).Where(t => t <= end).ToList();
        ObjectIdSpace.ResetForTests();
        Cozmo.Robot.Behavior.SteppedBehavior.ResetMissingForTests();
        var options = new CozmoEngineOptions
        {
            ResourcesPath = PickupPyramidNativeExecutionTests.Resources(), BlockPoolPath = "",
            SpeechSoundBankDirectory = GraphRig.FindMeta(), LabLocaleLanguage = "en", LabLocaleCountry = "US",
            KeepNeedsFull = true, AnimationStreamBudgetBytes = 8192,
        };
        using var sink = new SilentSink();
        using var rig = new M4ControlTests.Rig(engineOptions: options, keepNeedsFull: true,
            engineCreated: e => e.RuntimeAudioSinkForTest = () => sink);
        rig.Robot.Animations.LoadFrom(options.ResourcesPath!);
        rig.Engine.ContextRandom.SetSeed(seed);
        rig.Robot.Animations.ManualTicking = true;
        rig.Robot.Animations.ClockMs = () => rig.Engine.Timer.TimeStampMs;
        rig.Engine.AnimationStreamerUpdate = () => rig.Robot.Animations.Scheduler.Advance(rig.Engine.Timer.TimeStampMs, rig.Engine.Timer.Nanoseconds);
        using var bridge = new CozmoBridge(rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        using var decoder = ReliableTransport.CreateOffline();
        decoder.OfflineConnect();
        var receivedTags = new Dictionary<byte, int>();
        decoder.Received += e =>
        {
            if (e.Data is { Length: > 0 } data) receivedTags[data[0]] = receivedTags.GetValueOrDefault(data[0]) + 1;
            rig.Port.Raise(e.Marker, M4ControlTests.Rig.RobotEp, e.Data);
        };
        int cameraFrames = 0, processedFrames = 0;
        var processedReports = new ConcurrentQueue<object>();
        rig.Robot.Vision.ProcessedImage += p => processedReports.Enqueue(new { p.Timestamp, p.Modes, p.ImageMean });
        rig.Robot.Camera.FrameReceived += _ => cameraFrames++;
        rig.Robot.Vision.FrameProcessed += _ => Interlocked.Increment(ref processedFrames);
        var nvLogs = new List<string>();
        rig.Engine.LogEntry += e => { if ((e.Name?.Contains("NV", StringComparison.OrdinalIgnoreCase) ?? false) || (e.Name?.Contains("calib", StringComparison.OrdinalIgnoreCase) ?? false)) nvLogs.Add(e.Name + ": " + e.Text); };
        var emissions = new List<object>();
        var expectedEvents = new List<(string Kind, uint? Timestamp, byte[] Body)>();
        var wireCounts = new Dictionary<ushort, int>();
        double replayAt = 0;
        rig.Engine.EngineToGameMessage += (tag, body) =>
        {
            if (!ScopeTags.Contains(tag)) return;
            wireCounts[tag] = wireCounts.GetValueOrDefault(tag) + 1;
            emissions.Add(new { at = replayAt, tag, body = Convert.ToHexString(body.Span) });
            string? kind = tag switch
            {
                0x46 => "face-seen", 0x47 => "face-id-changed", 0x4B => "face-lost",
                0x44 when BinaryPrimitives.ReadUInt32LittleEndian(body.Span[4..]) == 2 => "cube-seen",
                0x44 when BinaryPrimitives.ReadUInt32LittleEndian(body.Span[4..]) == 4 => "charger-seen",
                0x0F or 0x10 => "cube-moved", 0x13 => "cube-tapped", 0x52 => "object-pose-lost", _ => null,
            };
            if (kind is not null)
            {
                uint? timestamp = tag switch
                { 0x46 => BinaryPrimitives.ReadUInt32LittleEndian(body.Span[4..]), 0x44 or 0x0F or 0x10 or 0x13 => BinaryPrimitives.ReadUInt32LittleEndian(body.Span), _ => null };
                expectedEvents.Add((kind, timestamp, body.ToArray()));
                if (tag == 0x46 && body.Span[56] != 0) expectedEvents.Add(("face-recognised", timestamp, body.ToArray()));
            }
        };
        var faults = new ConcurrentQueue<string>();
        rig.Robot.HandlerFaulted += e => faults.Enqueue(e.ToString());
        var missing = new ConcurrentDictionary<string, int>();
        rig.Engine.StackLogLine += s => { if (s.Contains("MISSING", StringComparison.Ordinal)) missing.AddOrUpdate(s, 1, (_, n) => n + 1); };
        var events = new List<JsonElement>();
        var requests = new List<int>();
        var actionReplies = new List<JsonElement>();
        long cursor = 0;
        int packetIndex = 0, callIndex = 0;
        bool connected = false;
        // MoodState is emitted once at the native Robot::Update mood phase
        // (0x00513E84..8A -> 0x0067B6A4, union 0x61 at 0x0072928C).
        // Its recorded host times retain the startup catch-up ticks. A guessed
        // regular phase merges NV replies ahead of the next request.
        double firstState = packets.First(p => FrameCodec.TryDecode(p.Raw, out var frame, out _) &&
            frame!.Messages.Any(m => m.Payload is { Length: > 0 } b && b[0] == 0xF0)).At;
        var ticks = Enumerable.Range(0, (int)(firstState / 60) + 1).Select(i => i * 60.0)
            .Concat(recordedTicks).Where(t => t <= end).Distinct().Order().ToArray();
        double previousTick = 0;
        var wall = Stopwatch.StartNew();
        bool realTime = Environment.GetEnvironmentVariable("COZMO_LLM_EVENTS_REALTIME") != "0";
        foreach (double tick in ticks)
        {
            if (realTime)
                while (wall.Elapsed.TotalMilliseconds < tick) await Task.Delay(1);
            replayAt = tick;
            if (!connected && tick >= 1956.7431)
            {
                connected = true;
                rig.Engine.ConnectToRobot(M4ControlTests.Rig.RobotIp);
            }
            while (packetIndex < packets.Count && packets[packetIndex].At <= tick)
                decoder.ProcessIncoming(packets[packetIndex++].Raw);
            while (callIndex < calls.Count && calls[callIndex].At <= tick)
            {
                var call = calls[callIndex++];
                // Polls are reproduced below after every engine tick; catalogue/state
                // reads do not affect the engine. Native action calls are dispatched.
                if (call.Name is "get_state" or "read_events" or "get_camera_image" or "list_animations") continue;
                requests.Add(await client.Send("tools/call", new { name = call.Name, arguments = call.Arguments }));
            }
            rig.Tick(tick - previousTick);
            previousTick = tick;
            rig.Engine.DrainAppDelivery();
            var reply = await client.Tool("read_events", new { since = cursor, detail = "full", limit = 1024 });
            var page = reply.GetProperty("result").GetProperty("structuredContent");
            Assert.False(page.GetProperty("gap").GetBoolean());
            cursor = page.GetProperty("cursor").GetInt64();
            foreach (var e in page.GetProperty("events").EnumerateArray())
                if (ScopeKinds.Contains(e.GetProperty("type").GetString()!)) events.Add(e.Clone());
        }
        // Consume remaining packets at the captured boundary, then drain prepared
        // worker results without advancing the recorded clock.
        replayAt = end;
        while (packetIndex < packets.Count) decoder.ProcessIncoming(packets[packetIndex++].Raw);
        rig.Tick(end - previousTick);
        rig.Robot.Vision.WaitForProcessorIdle();
        rig.Tick(0);
        rig.Engine.DrainAppDelivery();
        var finalReply = await client.Tool("read_events", new { since = cursor, detail = "full", limit = 1024 });
        var finalPage = finalReply.GetProperty("result").GetProperty("structuredContent");
        Assert.False(finalPage.GetProperty("gap").GetBoolean());
        foreach (var e in finalPage.GetProperty("events").EnumerateArray())
            if (ScopeKinds.Contains(e.GetProperty("type").GetString()!)) events.Add(e.Clone());
        foreach (int request in requests) actionReplies.Add((await client.Receive(request)).Clone());
        string? report = Environment.GetEnvironmentVariable("COZMO_LLM_EVENTS_REPORT");
        var captured = reference.GetProperty("engineEvents").EnumerateArray()
            .Where(e => e.GetProperty("atMs").GetDouble() <= end).ToArray();
        var capturedCounts = captured.GroupBy(e => e.GetProperty("kind").GetString()!)
            .ToDictionary(g => g.Key, g => g.Count());
        // Different asynchronous worker admission yields different detector/world
        // histories. Report this honestly instead of treating live C# output as
        // an oracle or forcing observations into the replay to match its counts.
        const string occurrenceGap = "MISSING: independent native whole-session occurrence oracle; " +
            "Processor 0x00651F08 selects pending images according to worker availability. " +
            "Raw frames plus host ticks do not record that schedule.";
        output.WriteLine(occurrenceGap);
        var summary = new
        {
            end, inputPackets = packets.Count, packetIndex, seed,
            independentNativeOccurrenceOracle = occurrenceGap,
            recordedStackCounts = capturedCounts,
            recordedStackLivePolledCounts = reference.GetProperty("livePolledCounts"),
            recordedStackLiveDeliveryDifferences = reference.GetProperty("liveDelivery").EnumerateArray()
                .Where(e => !e.GetProperty("matchesPackedBody").GetBoolean()).Select(e => e.Clone()).ToArray(),
            recordedAbsentKinds = new[] { "face-recognised" },
            ready = rig.Engine.IsReadyDelivered, connection = rig.Engine.ConnectionState,
            calibration = rig.Robot.CameraSettings.Calibration is not null,
            receivedTags, cameraFrames, processedFrames, processedReports = processedReports.ToArray(), framesDropped = rig.Robot.Vision.FramesDropped,
            nvInFlight = rig.Engine.NvStorage?.InFlightTag,
            audioReady = rig.Engine.RuntimeWwiseAudio?.Ready.IsCompletedSuccessfully,
            audioOutput = rig.Engine.Robot?.AudioOutputSource.ToString(),
            nvSent = rig.SentSince(0).OfType<NVCommand>().Select(n => new { n.Tag, n.Op, n.Length }).ToArray(), nvLogs,
            actionReplies, wireCounts, feedCounts = events.GroupBy(e => e.GetProperty("type").GetString()).ToDictionary(g => g.Key!, g => g.Count()),
            faults = faults.ToArray(), missing = missing.ToArray(), emissions, events,
        };
        if (!string.IsNullOrEmpty(report)) File.WriteAllText(report, JsonSerializer.Serialize(summary));
        output.WriteLine(JsonSerializer.Serialize(new { end, inputPackets = packets.Count, wireCounts, faults = faults.Count, gaps = missing.Count }));
        Assert.Equal(packets.Count, packetIndex);
        Assert.Empty(faults);
        Assert.NotNull(rig.Robot.CameraSettings.Calibration);
        Assert.Equal(expectedEvents.Count, events.Count);
        for (int i = 0; i < events.Count; i++)
        {
            var expected = expectedEvents[i]; var actual = events[i];
            Assert.Equal(expected.Kind, actual.GetProperty("type").GetString());
            var timestamp = actual.GetProperty("robotTimestamp");
            Assert.Equal(expected.Timestamp, timestamp.ValueKind == JsonValueKind.Null ? null : timestamp.GetUInt32());
            Assert.Equal(expected.Body, PackFeedBody(actual));
        }
    }
    // Independent CLAD field writer: no production PackBody, report publisher,
    // serializer or bridge mapping is reused to predict the tool payload.
    private static byte[] PackFeedBody(JsonElement entry)
    {
        string kind = entry.GetProperty("type").GetString()!;
        var p = entry.GetProperty("payload");
        using var bytes = new MemoryStream();
        using var w = new BinaryWriter(bytes, Encoding.UTF8, true);
        float Float(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() switch
        { "NaN" => BitConverter.UInt32BitsToSingle(0x7FC00000), "Infinity" => float.PositiveInfinity, "-Infinity" => float.NegativeInfinity, _ => throw new InvalidDataException() } : value.GetSingle();
        void Floats(JsonElement value, params string[] fields)
        { foreach (string field in fields) w.Write(Float(value.GetProperty(field))); }
        void Pose(JsonElement value)
        { Floats(value, "x", "y", "z", "q0", "q1", "q2", "q3"); w.Write(value.GetProperty("originId").GetUInt32()); }
        switch (kind)
        {
            case "face-seen":
            case "face-recognised":
                w.Write(p.GetProperty("faceId").GetInt32()); w.Write(p.GetProperty("timestamp").GetUInt32());
                Pose(p.GetProperty("pose")); Floats(p.GetProperty("rect"), "x", "y", "width", "height");
                byte[] name = Encoding.UTF8.GetBytes(p.GetProperty("name").GetString()!);
                w.Write(checked((byte)name.Length)); w.Write(name); w.Write(p.GetProperty("expression").GetSByte());
                foreach (string attr in new[] { "smile", "gaze", "blink" })
                { var pair = p.GetProperty(attr); w.Write(pair.GetProperty("valid").GetBoolean()); Floats(pair, "first", "second"); }
                foreach (var score in p.GetProperty("expressionScores").EnumerateArray()) w.Write(score.GetByte());
                foreach (string part in new[] { "leftEye", "rightEye", "nose", "mouth" })
                { var points = p.GetProperty(part); w.Write(checked((byte)points.GetArrayLength())); foreach (var point in points.EnumerateArray()) Floats(point, "x", "y"); }
                break;
            case "face-id-changed": w.Write(p.GetProperty("oldId").GetInt32()); w.Write(p.GetProperty("newId").GetInt32()); break;
            case "face-lost": w.Write(p.GetProperty("faceId").GetInt32()); break;
            case "object-pose-lost": w.Write(p.GetProperty("objectId").GetUInt32()); break;
            case "cube-seen":
            case "charger-seen":
                foreach (string field in new[] { "timestamp", "family", "type", "objectId" }) w.Write(p.GetProperty(field).GetUInt32());
                Floats(p, "imageX", "imageY", "imageWidth", "imageHeight"); Pose(p.GetProperty("pose"));
                Floats(p, "topOrientationRad"); w.Write(p.GetProperty("isActive").GetBoolean()); break;
            case "cube-moved":
            case "cube-tapped":
                var cube = p.GetProperty("cube");
                w.Write(p.GetProperty("timestamp").GetUInt32()); w.Write(cube.GetProperty("worldObjectId").GetUInt32());
                if (kind == "cube-tapped")
                {
                    var tap = cube.GetProperty("lastTap");
                    w.Write(tap.GetProperty("numTaps").GetByte()); w.Write(tap.GetProperty("tapTime").GetByte());
                    w.Write(tap.GetProperty("tapNeg").GetSByte()); w.Write(tap.GetProperty("tapPos").GetSByte());
                }
                else if (cube.GetProperty("moving").GetBoolean())
                { Floats(cube.GetProperty("accel"), "x", "y", "z"); w.Write(p.GetProperty("axisOfAccel").GetByte()); }
                break;
            default: throw new InvalidDataException(kind);
        }
        return bytes.ToArray();
    }

    // Same PCM-device boundary as the private face replay; it never plays sound.
    private sealed class SilentSink : IPlatformPcmSink
    {
        private readonly object gate = new();
        private readonly AutoResetEvent ready = new(false);
        private readonly Queue<(ulong Token, double Due)> pending = new();
        private readonly Queue<ulong> done = new();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Timer timer;
        private double tail;
        private bool disposed;
        internal SilentSink() => timer = new Timer(_ => Complete(), null, 1, 1);
        public WaitHandle CompletionReady => ready;
        public void Submit(ulong token, byte[] pcm)
        {
            lock (gate)
            {
                if (disposed) return;
                tail = Math.Max(tail, clock.Elapsed.TotalMilliseconds) + pcm.Length / 192.0;
                pending.Enqueue((token, tail));
            }
        }
        private void Complete()
        {
            lock (gate)
            {
                if (disposed) return;
                bool completed = false;
                while (pending.TryPeek(out var item) && item.Due <= clock.Elapsed.TotalMilliseconds)
                { pending.Dequeue(); done.Enqueue(item.Token); completed = true; }
                if (completed) ready.Set();
            }
        }
        public IReadOnlyList<ulong> DrainCompleted()
        { lock (gate) { var result = done.ToArray(); done.Clear(); return result; } }
        public void Dispose()
        { lock (gate) { if (disposed) return; disposed = true; timer.Dispose(); ready.Dispose(); } }
    }

}
