using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Bridge;
using Cozmo.Mcp;
using Cozmo.Protocol;
using Cozmo.Robot;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
using Client = Cozmo.Protocol.Tests.McpServerTests.Client;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class McpResultSizeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ReadEventsLimitsMatchingInputsBeforeCollapsingAndContinuesWithoutSkipping()
    {
        using var rig = new M4ControlTests.Rig();
        using var bridge = new CozmoBridge(rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        for (uint i = 1; i <= 105; i++)
        {
            Append(bridge, "face-seen", JsonSerializer.SerializeToElement(new { observation = i }), i);
            Append(bridge, "behavior-changed", JsonSerializer.SerializeToElement(new { transition = i }));
        }
        var first = Data(await client.Tool("read_events", new { types = new[] { "face-seen", "behavior-changed" },
            excludeTypes = new[] { "behavior-changed" } }));
        Assert.Empty(first.GetProperty("events").EnumerateArray());
        var summary = Assert.Single(first.GetProperty("sightings").EnumerateArray());
        Assert.Equal(100, summary.GetProperty("count").GetInt32());
        Assert.Equal(1u, summary.GetProperty("firstRobotTimestamp").GetUInt32());
        Assert.Equal(100u, summary.GetProperty("lastRobotTimestamp").GetUInt32());
        Assert.Equal(100u, summary.GetProperty("lastPayload").GetProperty("observation").GetUInt32());
        Assert.Equal(200, first.GetProperty("cursor").GetInt64());

        var tail = Data(await client.Tool("read_events", new { since = 200, types = new[] { "face-seen" }, detail = "full" }));
        Assert.Equal(5, tail.GetProperty("events").GetArrayLength());
        Assert.Equal(101u, tail.GetProperty("events")[0].GetProperty("robotTimestamp").GetUInt32());
        Assert.Equal(210, tail.GetProperty("cursor").GetInt64());
        Assert.False(tail.TryGetProperty("sightings", out _));

        var full = Data(await client.Tool("read_events", new { detail = "full", limit = 1024 }));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(bridge.ReadEvents().GetRawText()), JsonNode.Parse(full.GetRawText())));
    }

    [Fact]
    public async Task CompactReadGroupsOnlyRepeatedRecognitionsOfTheSameIdentity()
    {
        using var rig = new M4ControlTests.Rig();
        using var bridge = new CozmoBridge(rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        void See(int id, string name, uint stamp) =>
            Append(bridge, "face-recognised", JsonSerializer.SerializeToElement(new { faceId = id, name, stamp }), stamp);

        See(1, "Jim", 10);
        See(1, "Jim", 20);
        See(2, "Jim", 30);
        See(2, "Jim", 40);
        See(1, "James", 50);
        See(1, "James", 60);
        See(1, "Jim", 70);
        See(1, "Jim", 80);
        foreach (string type in new[] { "face-id-changed", "face-lost", "face-enrollment-completed", "face-renamed", "face-erased" })
            Append(bridge, type, JsonSerializer.SerializeToElement(new { faceId = 1 }));

        var reply = Data(await client.Tool("read_events", new { }));
        Assert.Equal(new long[] { 1, 3, 5, 7, 9, 10, 11, 12, 13 },
            reply.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("cursor").GetInt64()));
        var rows = reply.GetProperty("sightings").EnumerateArray().ToArray();
        Assert.Equal(3, rows.Length);
        Check(rows[0], 1, "Jim", 2, 20, 80);
        Check(rows[1], 2, "Jim", 1, 40, 40);
        Check(rows[2], 1, "James", 1, 60, 60);

        static void Check(JsonElement row, int id, string name, int count, uint first, uint last)
        {
            Assert.Equal("face-recognised", row.GetProperty("type").GetString());
            Assert.Equal(id, row.GetProperty("faceId").GetInt32());
            Assert.Equal(name, row.GetProperty("name").GetString());
            Assert.Equal(count, row.GetProperty("count").GetInt32());
            Assert.Equal(first, row.GetProperty("firstRobotTimestamp").GetUInt32());
            Assert.Equal(last, row.GetProperty("lastRobotTimestamp").GetUInt32());
            Assert.Equal(last, row.GetProperty("lastPayload").GetProperty("stamp").GetUInt32());
        }
    }

    [Fact]
    public async Task FilteredWaitOnlyWakesForMatchingEventsAndRespectsItsCursor()
    {
        using var rig = new M4ControlTests.Rig();
        using var bridge = new CozmoBridge(rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        int request = await client.Send("tools/call", new
        {
            name = "read_events", arguments = new { types = new[] { "action-completed" }, waitMs = 5000 },
        });
        Append(bridge, "face-seen", JsonSerializer.SerializeToElement(new { faceId = 7 }));
        await client.Request("ping", new { });
        Append(bridge, "action-completed", JsonSerializer.SerializeToElement(new { completion = new { tag = 1 } }));
        var page = Data(await client.Receive(request));
        Assert.Equal("action-completed", Assert.Single(page.GetProperty("events").EnumerateArray()).GetProperty("type").GetString());
        Assert.Equal(2, page.GetProperty("cursor").GetInt64());
    }

    [Fact]
    public async Task PutDownWaitsForItsOwnCompletionAndKeepsItInAnOversizedWindow()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        int call = await client.Send("tools/call", new { name = "put-down", arguments = new { } });
        uint tag = f.Robot.ActionList.GetCurrentAction()!.Tag;
        var rawCompletionTags = new List<uint>();
        var publicCompletionTags = new List<uint>();
        f.Rig.Engine.ActionCompleted += report => publicCompletionTags.Add(report.Tag);
        f.Rig.Engine.EngineToGameMessage += (kind, body) =>
        {
            if (kind == 0x5A) rawCompletionTags.Add(new CladReader(body.ToArray()).U32());
        };
        Append(bridge, "face-recognised", JsonSerializer.SerializeToElement(new { faceId = 0, name = "Jim" }));
        Append(bridge, "face-recognised", JsonSerializer.SerializeToElement(new { faceId = 0, name = "Jim" }));
        for (int i = 1; i <= 40; i++)
            Append(bridge, "face-recognised", JsonSerializer.SerializeToElement(new { faceId = i, name = $"Name {i}", points = new string('x', 1200) }));
        f.Rig.Tick();
        var result = await client.Receive(call);
        Assert.True(WireBytes(result) < 20000, WireBytes(result).ToString());
        var action = Data(result);
        Assert.True(action.GetProperty("admitted").GetBoolean());
        Assert.False(action.GetProperty("ownerRetired").GetBoolean());
        Assert.Equal("Native placement action ended", action.GetProperty("detail").GetString());
        Assert.Equal(0x03000011u, action.GetProperty("engineResult").GetUInt32());
        var during = action.GetProperty("during");
        Assert.Contains(during.GetProperty("events").EnumerateArray(),
            e => e.GetProperty("type").GetString() == "action-completed" &&
                e.GetProperty("payload").GetProperty("completion").GetProperty("tag").GetUInt32() == tag);
        var continuation = during.GetProperty("continuation");
        Assert.DoesNotContain(continuation.GetProperty("excludeTypes").EnumerateArray(),
            type => type.GetString() == "face-recognised");
        var sighting = Assert.Single(during.GetProperty("sightings").EnumerateArray());
        Assert.Equal(1, sighting.GetProperty("count").GetInt32());
        var later = Data(await client.Tool("read_events", new { since = continuation.GetProperty("since"),
            excludeTypes = continuation.GetProperty("excludeTypes"), detail = "full" }));
        Assert.Contains(later.GetProperty("events").EnumerateArray(), e => e.GetProperty("type").GetString() == "face-recognised" &&
            e.GetProperty("payload").GetProperty("faceId").GetInt32() == 40);
        Assert.DoesNotContain(tag, rawCompletionTags);
        Assert.DoesNotContain(tag, publicCompletionTags);
    }

    [Fact]
    public async Task SentAndAbortedNamesSurviveABridgeAttachingAfterClipSelection()
    {
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        var animations = f.Rig.Robot.Animations;
        var clip = animations.Library!.GetClip("anim_reacttocliff_pickup_03");
        var ticket = animations.PlayTracked(clip)!;
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();

        int mark = f.Rig.Mark();
        for (int i = 0; i < 20 && !f.Rig.SentSince(mark).OfType<StartOfAnimation>().Any(); i++) f.Rig.Tick();
        Assert.Contains(f.Rig.SentSince(mark).OfType<StartOfAnimation>(), start => start.AnimId == ticket.Tag);
        f.Rig.Data(new AnimationStarted { AnimId = ticket.Tag });
        f.Rig.Tick();
        animations.Stop();
        f.Rig.Engine.DrainAppDelivery();

        var events = Data(await client.Tool("read_events", new { types = new[] { "animation-started", "animation-aborted" } }))
            .GetProperty("events").EnumerateArray().ToArray();
        foreach (string type in new[] { "animation-started", "animation-aborted" })
        {
            var item = Assert.Single(events, e => e.GetProperty("type").GetString() == type);
            Assert.Equal(ticket.Tag, item.GetProperty("payload").GetProperty("tag").GetByte());
            Assert.Equal(clip.Name, item.GetProperty("payload").GetProperty("name").GetString());
        }
    }

    private static JsonElement Data(JsonElement reply) => reply.GetProperty("result").GetProperty("structuredContent");
    private static int WireBytes(JsonElement reply) => Encoding.UTF8.GetByteCount(reply.GetRawText());

    // Recorded event tapes start where the engine's typed reports have already
    // become MCP feed entries. Feed replay exercises presentation, not synthesis.
    private static readonly MethodInfo AppendFeed = typeof(CozmoBridge).GetMethod("Append", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo FeedGate = typeof(CozmoBridge).GetField("eventGate", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static void Append(CozmoBridge bridge, string type, JsonElement payload, uint? timestamp = null)
    {
        lock (FeedGate.GetValue(bridge)!)
            AppendFeed.Invoke(bridge, [type, timestamp, payload, false, null, null, null]);
    }

    [Fact]
    public void CompactDuringPairsAnimationEndsAndAbortsWithoutLosingUnmatchedEvents()
    {
        using var rig = new M4ControlTests.Rig();
        using var bridge = new CozmoBridge(rig.Robot);
        void Animation(string type, byte tag, string name) =>
            Append(bridge, type, JsonSerializer.SerializeToElement(new { tag, name }));

        Animation("animation-ended", 9, "older");
        Animation("animation-started", 5, "first");
        Animation("animation-started", 6, "second");
        Animation("animation-aborted", 6, "second");
        Animation("animation-ended", 5, "first");
        Animation("animation-started", 5, "still playing");
        var result = new JsonObject();
        AttachDuring.Invoke(bridge, [result, 0L, false, null]);
        var during = JsonSerializer.SerializeToElement(result).GetProperty("during");
        var summaries = during.GetProperty("animations").EnumerateArray().ToArray();
        Assert.Equal(new[] { "first", "second" }, summaries.Select(a => a.GetProperty("name").GetString()));
        Assert.Equal(new[] { "ended", "aborted" }, summaries.Select(a => a.GetProperty("result").GetString()));
        Assert.Equal(new long[] { 1, 6 }, during.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("cursor").GetInt64()));

        AttachDuring.Invoke(bridge, [result, 0L, true, null]);
        var full = JsonSerializer.SerializeToElement(result).GetProperty("during");
        Assert.Equal(6, full.GetProperty("events").GetArrayLength());
        Assert.False(full.TryGetProperty("animations", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturedEnrollmentCallKeepsRecognitionsAndFitsTheCompactReply(bool full)
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_REACTION_LOCK_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;
        var messages = File.ReadLines(Path.Combine(bundle, "events.jsonl"))
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line))
            .Where(row => row.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.GetInt32() == 11)
            .Take(2).ToArray();
        var request = messages.Single(row => row.GetProperty("kind").GetString() == "mcp-input").GetProperty("params");
        var response = messages.Single(row => row.GetProperty("kind").GetString() == "mcp-output")
            .GetProperty("result").GetProperty("structuredContent");
        var captured = response.GetProperty("during").GetProperty("events").EnumerateArray().ToArray();
        var terminal = response.GetProperty("report");
        var arguments = JsonNode.Parse(request.GetProperty("arguments").GetRawText())!.AsObject();
        if (full) arguments["duringDetail"] = "full";
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        int id = await client.Send("tools/call", new { name = request.GetProperty("name").GetString(), arguments });
        foreach (var e in captured)
            Append(bridge, e.GetProperty("type").GetString()!, e.GetProperty("payload"),
                e.GetProperty("robotTimestamp") is { ValueKind: JsonValueKind.Number } stamp ? stamp.GetUInt32() : null);
        f.Robot.PublishEnrollmentResult(terminal.GetProperty("result").GetByte(), terminal.GetProperty("id").GetInt32(),
            terminal.GetProperty("name").GetString()!);
        f.Rig.Engine.DrainAppDelivery();
        var reply = await client.Receive(id);
        var during = Data(reply).GetProperty("during");
        var all = during.GetProperty("events").EnumerateArray().ToList();
        if (full)
        {
            Assert.False(during.TryGetProperty("sightings", out _));
            Assert.False(during.TryGetProperty("continuation", out _));
            Assert.True(WireBytes(reply) > 20000);
            Assert.Equal(captured.Length, all.Count);
        }
        else
        {
            int size = WireBytes(reply);
            Assert.True(size < 20000, size.ToString());
            Assert.False(during.TryGetProperty("continuation", out _));
            var text = JsonSerializer.Deserialize<JsonElement>(reply.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!);
            var textWindow = text.GetProperty("during");
            Assert.True(textWindow.GetProperty("detailsInStructuredContent").GetBoolean());
            Assert.Equal(during.GetProperty("events").EnumerateArray().Where(e => e.GetProperty("type").GetString()!.StartsWith("face-", StringComparison.Ordinal))
                    .Select(e => e.GetRawText()),
                textWindow.GetProperty("events").EnumerateArray().Select(e => e.GetRawText()));
            Assert.Equal(Data(reply).GetProperty("report").GetRawText(), text.GetProperty("report").GetRawText());
            var groups = during.GetProperty("sightings").EnumerateArray().ToArray();
            Assert.Equal(185, Assert.Single(groups, e => e.GetProperty("type").GetString() == "face-seen").GetProperty("count").GetInt32());
            var repeats = Assert.Single(groups, e => e.GetProperty("type").GetString() == "face-recognised");
            Assert.Equal(1, repeats.GetProperty("faceId").GetInt32());
            Assert.Equal("Jim", repeats.GetProperty("name").GetString());
            Assert.Equal(70, repeats.GetProperty("count").GetInt32());
            Assert.Equal(100595u, repeats.GetProperty("firstRobotTimestamp").GetUInt32());
            Assert.Equal(113485u, repeats.GetProperty("lastRobotTimestamp").GetUInt32());
            Assert.Equal(113485u, repeats.GetProperty("lastPayload").GetProperty("timestamp").GetUInt32());
            var paired = during.GetProperty("animations").EnumerateArray().SelectMany(a =>
                new[] { a.GetProperty("startCursor").GetInt64(), a.GetProperty("endCursor").GetInt64() }).ToHashSet();
            Assert.Equal(captured.Where((e, i) => !paired.Contains(i + 1) && !MayCollapse(e.GetProperty("type").GetString()))
                    .Select(e => (e.GetProperty("type").GetString(), e.GetProperty("payload").GetRawText())),
                all.Where(e => !MayCollapse(e.GetProperty("type").GetString()))
                    .Select(e => (e.GetProperty("type").GetString(), e.GetProperty("payload").GetRawText())));
            output.WriteLine($"Captured enrollment compact response {size} bytes, without a continuation.");
        }

        var expected = captured.Where(e => e.GetProperty("type").GetString() == "face-recognised").ToArray();
        var recognitions = all.Where(e => e.GetProperty("type").GetString() == "face-recognised").ToArray();
        Assert.Equal(full ? 71 : 1, recognitions.Length);
        Assert.Equal((full ? expected : expected[..1]).Select(e => e.GetProperty("payload").GetRawText()),
            recognitions.Select(e => e.GetProperty("payload").GetRawText()));
    }

    [Theory]
    [InlineData("COZMO_CHARGER_SLEEP_BUNDLE")]
    [InlineData("COZMO_REACTION_LOCK_BUNDLE")]
    public async Task SessionEventTapesPassThroughTheMcpClientWithoutDroppingNonSightings(string variable)
    {
        string? bundle = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(bundle)) return;
        using var rig = new M4ControlTests.Rig();
        var calls = new Dictionary<int, (string Name, JsonElement Args)>();
        int pages = 0, maxWire = 0, maxActionWire = 0, continuationPages = 0;
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            using var source = JsonDocument.Parse(line);
            var row = source.RootElement;
            string? kind = row.GetProperty("kind").GetString();
            if (!row.TryGetProperty("id", out var requestId) || !requestId.TryGetInt32(out int id)) continue;
            if (kind == "mcp-input" && row.GetProperty("method").GetString() == "tools/call")
            {
                var call = row.GetProperty("params");
                calls[id] = (call.GetProperty("name").GetString()!, call.GetProperty("arguments").Clone());
                continue;
            }
            if (kind != "mcp-output" || !calls.TryGetValue(id, out var request) ||
                !row.TryGetProperty("result", out var result) || !result.TryGetProperty("structuredContent", out var response)) continue;
            var captured = request.Name == "read_events" ? response :
                response.TryGetProperty("during", out var during) ? during : default;
            if (captured.ValueKind != JsonValueKind.Object || !captured.TryGetProperty("events", out var original)) continue;
            var tape = original.EnumerateArray().ToArray();
            using var bridge = new CozmoBridge(rig.Robot);
            await using var client = new Client(bridge);
            await client.Initialize();
            foreach (var e in tape)
                Append(bridge, e.GetProperty("type").GetString()!, e.GetProperty("payload"),
                    e.GetProperty("robotTimestamp") is { ValueKind: JsonValueKind.Number } ts ? ts.GetUInt32() : null);
            if (request.Name != "read_events")
            {
                var action = JsonNode.Parse(response.GetRawText())!.AsObject();
                action.Remove("during");
                AttachDuring.Invoke(bridge, [action, 0L, false, null]);
                object wire = ToolResult.Invoke(null, [JsonSerializer.SerializeToElement(action), result.GetProperty("isError").GetBoolean()])!;
                int size = JsonSerializer.SerializeToUtf8Bytes(new { jsonrpc = "2.0", id, result = wire }).Length;
                maxActionWire = Math.Max(maxActionWire, size);
                Assert.True(size < 20000, $"{request.Name} #{id}: action reply was {size} bytes");
            }
            long since = 0;
            var delivered = new List<JsonElement>();
            while (true)
            {
                var reply = await client.Tool("read_events", new { since });
                maxWire = Math.Max(maxWire, WireBytes(reply));
                Assert.True(WireBytes(reply) < 20000, $"{request.Name} #{id}: {WireBytes(reply)} bytes");
                var page = Data(reply);
                delivered.AddRange(page.GetProperty("events").EnumerateArray().Select(e => e.Clone()));
                long next = page.GetProperty("cursor").GetInt64();
                if (next == tape.Length) break;
                Assert.True(since < next);
                since = next;
                continuationPages++;
            }
            var originals = tape.Where(e => !MayCollapse(e.GetProperty("type").GetString())).ToArray();
            var actual = delivered.Where(e => !MayCollapse(e.GetProperty("type").GetString())).ToArray();
            Assert.Equal(originals.Select(e => e.GetProperty("payload").GetRawText()), actual.Select(e => e.GetProperty("payload").GetRawText()));
            AssertRequiredRecognitions(tape, delivered);
            pages++;
        }
        Assert.True(pages > 0);
        Assert.True(continuationPages > 0);
        output.WriteLine($"{pages} recorded tool windows, {continuationPages} continuation pages, largest read reply {maxWire} bytes, largest action reply {maxActionWire} bytes.");
    }

    private static readonly MethodInfo AttachDuring = typeof(CozmoBridge).GetMethod("AttachDuring", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo ToolResult = typeof(CozmoMcpServer).GetMethod("ToolResult", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void AssertRequiredRecognitions(JsonElement[] tape, List<JsonElement> delivered)
    {
        var visible = delivered.ToDictionary(e => e.GetProperty("cursor").GetInt64());
        var previous = new Dictionary<int, string>();
        for (int i = 0; i < tape.Length; i++)
        {
            var item = tape[i];
            if (item.GetProperty("type").GetString() != "face-recognised") continue;
            var payload = item.GetProperty("payload");
            int id = payload.GetProperty("faceId").GetInt32();
            string name = payload.GetProperty("name").GetString()!;
            if (previous.GetValueOrDefault(id) != name)
                Assert.Equal(payload.GetRawText(), visible[i + 1].GetProperty("payload").GetRawText());
            previous[id] = name;
        }
    }

    private static bool MayCollapse(string? type) =>
        type is "face-seen" or "cube-seen" or "charger-seen" or "robot-state" or "face-recognised";
}
