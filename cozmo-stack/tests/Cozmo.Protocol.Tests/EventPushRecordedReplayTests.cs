using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
using static Cozmo.Protocol.Tests.BridgeEventPushTests;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class EventPushRecordedReplayTests(ITestOutputHelper output)
{
    [Fact]
    public async Task EveningSessionFullReportsReachTcpAndMcpInTheSameOrder()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_EVENT_PUSH_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;

        using var fixture = new Fixture();
        var robot = fixture.Rig.Robot;
        var clock = new EventClock();
        using var bridge = new CozmoBridge(robot, 16000, timeProvider: clock);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        await using var stream = new BridgeEventStream(bridge, 0);
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, stream.Port);
        using var socket = tcp.GetStream();
        using var input = new StreamReader(socket);
        await socket.WriteAsync("{\"since\":0,\"significantOnly\":true}\n"u8.ToArray());

        Replay(bundle, fixture, bridge, clock);

        // Unfiltered in-process history establishes both the order and flag for each
        // event independently of the pagination, stream, and notification adapters.
        var expected = Events(bridge.ReadEvents()).Where(e => e.GetProperty("significant").GetBoolean())
            .ToArray();
        foreach (string type in new[] { "face-seen", "face-recognised", "cube-seen", "charger-seen", "cube-tapped",
            "cube-moved", "off-treads-changed", "reaction-changed", "action-completed", "going-to-sleep",
            "on-charger", "off-charger" })
            Assert.Contains(Events(bridge.ReadEvents()), e => Type(e) == type && e.GetProperty("significant").GetBoolean());

        Assert.Contains(expected, entry => Type(entry) == "on-charger" &&
            entry.GetProperty("robotTimestamp").GetUInt32() > 1180000);
        var polled = new List<JsonElement>();
        var pushed = new List<JsonElement>();
        long cursor = 0;
        do
        {
            var page = Data(await client.Tool("read_events", new { since = cursor, significantOnly = true, limit = 13, detail = "full" }));
            Assert.False(page.GetProperty("gap").GetBoolean());
            polled.AddRange(Events(page));
            cursor = page.GetProperty("cursor").GetInt64();
        } while (cursor < bridge.CurrentEventCursor());
        while (pushed.Count < expected.Length)
        {
            var page = await ReadPage(input);
            Assert.False(page.GetProperty("gap").GetBoolean());
            pushed.AddRange(Events(page));
        }
        foreach (var actual in new[] { polled, pushed })
        {
            Assert.Equal(expected.Length, actual.Count);
            for (int i = 0; i < expected.Length; i++)
                Assert.True(JsonElement.DeepEquals(expected[i], actual[i]), $"Feed record {i} differs.");
        }
        output.WriteLine($"Evening recording yielded {expected.Length} significant events from {bridge.CurrentEventCursor()} feed records.");
    }

    [Fact]
    public async Task SessionFourPushReplayKeepsReactionsRecognitionsAndFalls()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_EVENT_FILTER_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;
        using var fixture = new Fixture();
        var clock = new EventClock();
        using var bridge = new CozmoBridge(fixture.Rig.Robot, 16000, timeProvider: clock);
        Replay(bundle, fixture, bridge, clock);
        long endOfReplay = bridge.CurrentEventCursor();
        // A final significant sentinel lets the test finish even when the last
        // capture records are quiet; it is excluded from all measurements.
        var sleep = typeof(CozmoBridge).GetMethod("OnGoingToSleep", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Action<GoingToSleepReport>>(bridge);
        sleep(new(false));

        var full = Events(bridge.ReadEvents()).Where(e => e.GetProperty("cursor").GetInt64() <= endOfReplay).ToArray();
        var before = full.Where(e => e.GetProperty("significant").GetBoolean()).ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var after = new List<BridgeEvent>();
        await foreach (var page in bridge.StreamPushEventsAsync(0, timeout.Token))
        {
            after.AddRange(page.Events.Where(e => e.Cursor <= endOfReplay));
            if (page.Cursor > endOfReplay) break;
        }
        Assert.Equal(after.Select(e => e.Cursor).Order(), after.Select(e => e.Cursor));

        string[] preserve = ["reaction-changed", "face-recognised", "cube-moved", "cube-tapped", "cliff-detected",
            "unexpected-movement", "going-to-sleep", "object-pose-lost", "charger-seen"];
        foreach (var entry in before.Where(e => preserve.Contains(Type(e))))
            Assert.Contains(after, e => e.Cursor == entry.GetProperty("cursor").GetInt64());
        Assert.All(after.Where(e => e.Type == "reaction-changed"), e =>
        {
            Assert.Equal(JsonValueKind.String, e.Payload.GetProperty("oldTrigger").ValueKind);
            Assert.Equal(JsonValueKind.String, e.Payload.GetProperty("newTrigger").ValueKind);
            Assert.True(e.Payload.GetProperty("oldBehavior").ValueKind == JsonValueKind.String ||
                e.Payload.GetProperty("newBehavior").ValueKind == JsonValueKind.String);
        });
        Assert.Contains(after, e => e.Type == "off-treads-changed" &&
            e.Payload.GetProperty("from").GetString() == "InAir" && e.Payload.GetProperty("to").GetString() == "Falling");
        Assert.Contains(after, e => e.Type == "off-treads-changed" &&
            e.Payload.GetProperty("from").GetString() == "Falling" && e.Payload.GetProperty("to").GetString() == "OnTreads");
        // Full view marked the original caller's completions significant. The
        // new channel rule keeps its failures plus every foreign completion.
        foreach (var own in before.Where(e => Type(e) == "action-completed"))
        {
            bool success = own.GetProperty("payload").GetProperty("completion").GetProperty("result").GetUInt32() == 0;
            long cursor = own.GetProperty("cursor").GetInt64();
            if (success) Assert.DoesNotContain(after, e => e.Cursor == cursor);
            else Assert.Contains(after, e => e.Cursor == cursor);
        }
        foreach (var foreign in full.Where(e => Type(e) == "action-completed" && !e.GetProperty("significant").GetBoolean()))
            Assert.Contains(after, e => e.Cursor == foreign.GetProperty("cursor").GetInt64());
        Assert.Contains(after, e => e.Type == "action-completed");
        var faces = after.Where(e => e.Type is "face-seen" or "face-recognised").ToArray();
        Assert.NotEmpty(faces);
        Assert.True(faces.Count(e => e.Type == "face-seen") < before.Count(e => Type(e) == "face-seen"));
        Assert.All(faces, e => Assert.False(e.Payload.TryGetProperty("leftEye", out _)));

        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        int beforeBytes = JsonSerializer.SerializeToUtf8Bytes(before, json).Length;
        int afterBytes = JsonSerializer.SerializeToUtf8Bytes(after, json).Length;
        output.WriteLine($"Session 4 replay significant events: {before.Length} / {beforeBytes} UTF-8 bytes before, " +
            $"{after.Count} / {afterBytes} UTF-8 bytes after (event arrays, excluding transport wrappers).");
    }

    private static void Replay(string bundle, Fixture fixture, CozmoBridge bridge, EventClock clock)
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        Action<T> Handler<T>(string name) =>
            typeof(CozmoBridge).GetMethod(name, Private)!.CreateDelegate<Action<T>>(bridge);
        var offTreads = typeof(CozmoBridge).GetMethod("OffTreads", Private)!
            .CreateDelegate<Action<OffTreadsState, OffTreadsState>>(bridge);
        var moved = Handler<CubeTimestampEvent>("OnCubeMoved");
        var tapped = Handler<CubeTimestampEvent>("OnCubeTapped");
        var action = Handler<ActionCompletionReport>("OnActionCompleted");
        var cliff = Handler<CliffReport>("CliffDetected");
        var sleep = Handler<GoingToSleepReport>("OnGoingToSleep");
        var hostCaller = typeof(CozmoBridge).GetField("eventCaller", Private)!.GetValue(bridge);
        var engine = fixture.Rig.Engine;
        var robot = fixture.Rig.Robot;
        var events = new List<(double At, Action Play)>();
        var groundTags = new HashSet<uint>();
        var groundCalls = new HashSet<string>();
        OffTreadsState treads = OffTreadsState.OnTreads;
        var reactionNames = new Dictionary<double, string>();
        double? awaitingBehaviorAt = null;
        string? lastReactionBehavior = null;

        foreach (var line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            using var document = JsonDocument.Parse(line);
            var row = document.RootElement;
            var at = row.GetProperty("atMs").GetDouble();
            switch (row.GetProperty("kind").GetString())
            {
                case "mcp-input" when row.GetProperty("method").GetString() == "tools/call" &&
                    row.GetProperty("params").GetProperty("name").GetString() == "put-down":
                    groundCalls.Add(row.GetProperty("id").GetRawText());
                    break;
                case "mcp-output" when row.TryGetProperty("id", out var id) && groundCalls.Contains(id.GetRawText()) &&
                    row.TryGetProperty("result", out var wrapper) && wrapper.TryGetProperty("structuredContent", out var result) &&
                    result.TryGetProperty("during", out var during):
                    foreach (var entry in Events(during).Where(e => Type(e) == "action-completed"))
                    {
                        var completion = entry.GetProperty("payload").GetProperty("completion");
                        if (completion.GetProperty("actionType").GetInt32() == 24)
                            groundTags.Add(completion.GetProperty("tag").GetUInt32());
                    }
                    break;
                case "engine-log" when row.GetProperty("name").GetString() == "Robot.OfftreadsState.TreadStateChanged":
                    var next = Enum.Parse<OffTreadsState>(row.GetProperty("text").GetString()!.Split(':')[^1].Trim());
                    events.Add((at, () => { offTreads(treads, next); treads = next; }));
                    break;
                case "engine-to-game":
                    string tag = row.GetProperty("tag").GetString()!;
                    if (tag == "0x87" && awaitingBehaviorAt is { } reactionAt && at - reactionAt < 10)
                    {
                        byte id = Convert.FromHexString(row.GetProperty("bodyHex").GetString()!)[1];
                        reactionNames[reactionAt] = Enum.GetName((BehaviorID)id)!;
                        awaitingBehaviorAt = null;
                    }
                    if (tag is not ("0x0F" or "0x10" or "0x13" or "0x2D" or "0x44" or "0x46" or "0x47" or "0x4B" or "0x52" or "0x5A" or "0x88"))
                        break;
                    var body = new CladReader(Convert.FromHexString(row.GetProperty("bodyHex").GetString()!));
                    if (tag == "0x88") awaitingBehaviorAt = at;
                    events.Add((at, () => ReplayReport(at, tag, body)));
                    break;
            }
        }
        // The derived messages.jsonl stops at 300 s in this bundle. Read the
        // full datagram capture for contact, battery and cliff inputs.
        foreach (var (at, message) in RecordedRobotInput.ReadInbound(bundle, _ => true, double.PositiveInfinity, [0xF0, 0xC0]))
        {
            if (message is RobotState state) events.Add((at, () => robot.State.Handle(state)));
            else if (message is CliffEvent c) events.Add((at, () => cliff(new(c.Timestamp, (CliffSensors)c.DetectedFlags, c.DidStopForCliff))));
        }
        Assert.NotEmpty(events);
        double previous = 0;
        foreach (var (at, play) in events.OrderBy(e => e.At))
        {
            clock.Advance(TimeSpan.FromMilliseconds(at - previous));
            play();
            engine.DrainAppDelivery();
            previous = at;
        }

        void ReplayReport(double at, string tag, CladReader body)
        {
            switch (tag)
            {
                case "0x0F":
                case "0x10":
                case "0x13":
                    uint timestamp = body.U32(), worldId = body.U32();
                    var cube = new Cube { Moving = tag == "0x0F" };
                    if (cube.Moving) cube.UpAxis = (UpAxis)body.U8();
                    (tag == "0x13" ? tapped : moved)(new(cube.SnapshotForApp(worldId), timestamp));
                    break;
                case "0x2D":
                    sleep(new(body.Bool()));
                    break;
                case "0x44":
                    uint stamp = body.U32(), family = body.U32(), type = body.U32(), id = body.U32();
                    float x = body.F32(), y = body.F32(), w = body.F32(), h = body.F32();
                    engine.DeliverObservedObjectToApp(new(stamp, family, type, id, x, y, w, h, Pose(body), body.F32(), body.Bool()));
                    break;
                case "0x46":
                    int face = body.I32();
                    uint time = body.U32();
                    var pose = Pose(body);
                    var rect = new FaceRect(body.F32(), body.F32(), body.F32(), body.F32());
                    string name = body.String8();
                    var expression = (FacialExpression)body.I8();
                    FaceAttributePair ReadPair() => new(body.Bool(), body.F32(), body.F32());
                    var smile = ReadPair();
                    var gaze = ReadPair();
                    var blink = ReadPair();
                    var scores = body.VarArray(5, body.U8);
                    Vec2[] ReadPoints() => body.VarArray(body.U8(), () => new Vec2(body.F32(), body.F32()));
                    engine.PublishRobotObservedFace(new(face, time, pose, rect, name, ReadPoints(), ReadPoints(), ReadPoints(), ReadPoints())
                    {
                        Expression = expression, Smile = smile, Gaze = gaze, Blink = blink, ExpressionScores = scores,
                    });
                    break;
                case "0x47":
                    engine.PublishFaceIdReport(0x47, new NativeChangedFace(body.I32(), body.I32()));
                    break;
                case "0x4B":
                    engine.PublishFaceIdReport(0x4B, new NativeDeletedFace(body.I32()));
                    break;
                case "0x52":
                    engine.PublishObjectPoseUnknown(body.U32());
                    break;
                case "0x5A":
                    uint actionTag = body.U32();
                    int actionType = body.I32();
                    uint result = body.U32();
                    // This recorded process assigns 1..1000000 to direct host motion
                    // and speech. Ground placement uses a native tag, recovered from
                    // its own tool result above. A separate test runs actual admission.
                    var caller = actionTag is > 0 and <= 1000000 || groundTags.Contains(actionTag) ? hostCaller : null;
                    action(new(actionTag, actionType, result, body.VarArray(body.U8(), body.U32), 0) { HostCaller = caller });
                    break;
                case "0x88":
                    ReactionTrigger? Trigger(byte value) => value == 0x16 ? null : (ReactionTrigger)value;
                    var oldTrigger = Trigger(body.U8());
                    var newTrigger = Trigger(body.U8());
                    string? newBehavior = newTrigger.HasValue ? reactionNames.GetValueOrDefault(at) : null;
                    engine.BroadcastReactionTransition(oldTrigger, newTrigger,
                        oldTrigger.HasValue ? lastReactionBehavior : null, newBehavior);
                    lastReactionBehavior = newBehavior;
                    break;
            }
        }
    }

    private static EngineGamePose Pose(CladReader data) =>
        new(data.F32(), data.F32(), data.F32(), data.F32(), data.F32(), data.F32(), data.F32(), data.U32());
}
