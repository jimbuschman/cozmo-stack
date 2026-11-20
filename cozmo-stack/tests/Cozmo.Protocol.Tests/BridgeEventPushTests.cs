using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class BridgeEventPushTests
{
    [Fact]
    public async Task SignificantLongPollSkipsRepeatsAndUsesFilterBeforeLimit()
    {
        using var f = new Fixture();
        var clock = new EventClock();
        using var bridge = new CozmoBridge(f.Rig.Robot, timeProvider: clock);
        await using var client = new McpServerTests.Client(bridge);
        var init = Data(await client.Request("initialize", new { protocolVersion = "2025-11-25" }), tool: false);
        Assert.False(init.GetProperty("capabilities").TryGetProperty("experimental", out _));
        await client.Notify("notifications/initialized", new { });
        Face(f.Rig.Engine, -1, 1000);
        Assert.Equal(new[] { "face-seen", "face-recognised" }, Events(bridge.ReadEvents()).Select(Type));
        f.Rig.Engine.PublishFaceIdReport(0x47, new NativeChangedFace(-1, 1));
        f.Rig.Engine.DrainAppDelivery();

        long cursor = bridge.CurrentEventCursor();
        var call = await client.Send("tools/call", new
        {
            name = "read_events",
            arguments = new { since = cursor, waitMs = 5000, significantOnly = true, types = new[] { "face-recognised" }, limit = 1 },
        });
        for (uint stamp = 1001; stamp < 1005; stamp++)
        {
            Face(f.Rig.Engine, 1, stamp);
            await Task.Yield();
        }
        clock.Advance(TimeSpan.FromSeconds(60));
        Face(f.Rig.Engine, 1, 61000);
        var response = Data(await client.Receive(call));
        Assert.Equal(61000u, Assert.Single(Events(response)).GetProperty("robotTimestamp").GetUInt32());
        // A standard client can keep receiving the unfiltered observations.
        Assert.Contains(Events(Data(await client.Tool("read_events", new { detail = "full" }))), e =>
            Type(e) == "face-recognised" && !e.GetProperty("significant").GetBoolean());
        Assert.True((await client.Tool("read_events", new { significantOnly = "true" })).GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task CallerOwnsOnlyItsNativeCompletion()
    {
        using var f = new Fixture();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        using var other = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var call = await client.Send("tools/call", new { name = "head", arguments = new { radians = 0.23 } });
        var runner = f.Robot.ActionList.GetCurrentAction()!;
        uint tag = runner.Tag;
        int mark = f.Rig.Mark();
        f.Rig.Tick();
        var motor = Assert.Single(f.Rig.SentSince(mark).OfType<SetHeadAngle>());
        f.Rig.Data(new MotorActionAck { ActionId = motor.ActionId });
        f.Rig.Tick();
        f.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos, head: 0.23f);
        Assert.Equal(0u, Data(await client.Receive(call)).GetProperty("engineResult").GetUInt32());
        f.Rig.Engine.DrainAppDelivery();

        var match = Assert.Single(Events(bridge.ReadEvents()), IsOurReport);
        Assert.True(match.GetProperty("significant").GetBoolean());
        var unrelated = Assert.Single(Events(other.ReadEvents()), IsOurReport);
        Assert.False(unrelated.GetProperty("significant").GetBoolean());
        Assert.False(match.GetProperty("payload").GetProperty("completion").TryGetProperty("hostCaller", out _));

        bool IsOurReport(JsonElement e) => Type(e) == "action-completed" &&
            e.GetProperty("payload").GetProperty("completion").GetProperty("tag").GetUInt32() == tag;
    }

    [Fact]
    public void PowerEventsFollowTheNativeThresholdAndChargerContacts()
    {
        using var f = new Fixture();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        void Supply(float volts, bool onCharger = false) => f.Rig.Robot.State.Handle(new RobotState
        {
            BatteryVoltage = volts, Status = onCharger ? (uint)RobotStatusFlag.IsOnCharger : 0,
        });
        Supply(3.5f);
        Supply(3.49f);
        Supply(3.48f);
        Supply(3.48f, onCharger: true);
        Supply(3.48f, onCharger: true);
        Supply(3.48f);
        var events = Events(bridge.ReadEvents()).ToArray();
        Assert.Equal(new[] { "low-battery", "on-charger", "off-charger", "low-battery" }, events.Select(Type));
        Assert.All(events, e => Assert.True(e.GetProperty("significant").GetBoolean()));
    }

    [Fact]
    public async Task TcpSubscribesWithFiltersAndReportsAnEvictedHistory()
    {
        using var f = new Fixture();
        using var bridge = new CozmoBridge(f.Rig.Robot, 4, ["face-seen"]);
        Face(f.Rig.Engine, 1, 1000);
        Face(f.Rig.Engine, 1, 1001);
        Face(f.Rig.Engine, 2, 1002);
        await using var stream = new BridgeEventStream(bridge, 0);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, stream.Port);
        using var socket = client.GetStream();
        using var input = new StreamReader(socket);
        await socket.WriteAsync("""{"since":0,"types":["face-seen"],"significantOnly":true}"""u8.ToArray());
        await socket.WriteAsync(new byte[] { 10 });
        var page = await ReadPage(input);
        Assert.True(page.GetProperty("gap").GetBoolean());
        Assert.Equal(6, page.GetProperty("cursor").GetInt64());
        Assert.Equal(3, page.GetProperty("oldestCursor").GetInt64());
        Assert.Equal(1002u, Assert.Single(Events(page)).GetProperty("robotTimestamp").GetUInt32());

        Face(f.Rig.Engine, 2, 1003);
        Face(f.Rig.Engine, 3, 1004);
        Assert.Equal(1004u, Assert.Single(Events(await ReadPage(input))).GetProperty("robotTimestamp").GetUInt32());

        using var invalid = new TcpClient();
        await invalid.ConnectAsync(IPAddress.Loopback, stream.Port);
        using var invalidSocket = invalid.GetStream();
        using var invalidInput = new StreamReader(invalidSocket);
        await invalidSocket.WriteAsync("{\"since\":1000}\n"u8.ToArray());
        Assert.True((await ReadPage(invalidInput)).TryGetProperty("error", out _));
    }

    [Fact]
    public async Task ChannelHandshakeAndNotificationUseOnlySignificantEvents()
    {
        using var f = new Fixture();
        using var bridge = new CozmoBridge(f.Rig.Robot, significantEventTypes: ["face-recognised"]);
        await using var client = new McpServerTests.Client(bridge, channel: true);
        var init = Data(await client.Request("initialize", new { protocolVersion = "2025-11-25" }), tool: false);
        Assert.Empty(init.GetProperty("capabilities").GetProperty("experimental").GetProperty("claude/channel").EnumerateObject());
        Face(f.Rig.Engine, 4, 1000);
        Assert.True((await client.NextAfterRequest("ping", new { })).TryGetProperty("id", out _));
        await client.Notify("notifications/initialized", new { });
        Face(f.Rig.Engine, 4, 1001);
        Face(f.Rig.Engine, 5, 1002);
        var response = await client.Next();
        Assert.False(response.TryGetProperty("id", out _));
        Assert.Equal("2.0", response.GetProperty("jsonrpc").GetString());
        Assert.Equal("notifications/claude/channel", response.GetProperty("method").GetString());
        var parameters = response.GetProperty("params");
        var meta = parameters.GetProperty("meta");
        Assert.Equal(new[] { "event_type", "face_id" }, meta.EnumerateObject().Select(p => p.Name));
        Assert.Equal("5", meta.GetProperty("face_id").GetString());
        Assert.Equal("face-recognised", meta.GetProperty("event_type").GetString());
        var item = Assert.Single(Events(JsonSerializer.Deserialize<JsonElement>(parameters.GetProperty("content").GetString()!)));
        Assert.Equal(1002u, item.GetProperty("robotTimestamp").GetUInt32());
    }

    internal static JsonElement Data(JsonElement reply, bool tool = true) =>
        tool ? reply.GetProperty("result").GetProperty("structuredContent") : reply.GetProperty("result");

    internal static JsonElement.ArrayEnumerator Events(JsonElement page) => page.GetProperty("events").EnumerateArray();
    internal static string? Type(JsonElement e) => e.GetProperty("type").GetString();

    internal static async Task<JsonElement> ReadPage(StreamReader input)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return JsonSerializer.Deserialize<JsonElement>(await input.ReadLineAsync(timeout.Token) ?? throw new EndOfStreamException());
    }

    private static void Face(CozmoEngine engine, int id, uint stamp)
    {
        engine.PublishRobotObservedFace(new(id, stamp, new(200, 0, 50, 1, 0, 0, 0, 0), new(10, 10, 50, 50),
            "Ada", [], [], [], []));
        engine.DrainAppDelivery();
    }

    internal sealed class EventClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => now;
        internal void Advance(TimeSpan span) => now += span;
    }
}
