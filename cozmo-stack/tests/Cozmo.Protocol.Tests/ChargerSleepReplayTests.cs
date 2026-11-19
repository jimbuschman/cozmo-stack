using System.Security.Cryptography;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class ChargerSleepReplayTests(ITestOutputHelper output)
{
    [Fact]
    public async Task UncancelledDeadlinePublishesTheCapturedDisconnectReason()
    {
        using var fixture = new Fixture(sourceNvEmpty: true);
        var rig = fixture.Rig;
        using var bridge = new CozmoBridge(rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        rig.Engine.SetRobotDisconnectReason(RobotDisconnectReason.SleepPlacedOnCharger);
        rig.Engine.StartIdleTimeout(-1, 0.1f);
        rig.Tick();
        rig.Tick(120);
        rig.Tick();
        var events = (await client.Tool("read_events", new { since = 0 })).GetProperty("result")
            .GetProperty("structuredContent").GetProperty("events").EnumerateArray();
        var payload = Assert.Single(events.Where(e => e.GetProperty("type").GetString() == "robot-disconnected")).GetProperty("payload");
        Assert.Equal("SleepPlacedOnCharger", payload.GetProperty("reason").GetString());
        Assert.Equal((byte)4, payload.GetProperty("reasonCode").GetByte());
        Assert.Equal(RobotDisconnectReason.Unknown, rig.Engine.DisconnectReason);
    }

    [Fact]
    public async Task CapturedPickupCancelsTheArmedChargerDisconnect()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_CHARGER_SLEEP_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;
        const double start = 532000, end = 888000;
        string path = Path.Combine(bundle, "frames.jsonl");
        using (var input = File.OpenRead(path))
            Assert.Equal("B1DEF3D99A85432AEAE55E9D4AF1624DFDAAB160EA1D5D80AA95C947BF9C564E",
                Convert.ToHexString(SHA256.HashData(input)));
        var states = new List<(double At, RobotState State)>();
        using (var decoder = ReliableTransport.CreateOffline())
        {
            decoder.OfflineConnect();
            double at = 0;
            decoder.Received += e =>
            {
                if (at >= start && e.Data is { Length: > 0 } data && data[0] == (byte)RobotMessageId.State)
                    states.Add((at, Assert.IsType<RobotState>(RobotMessage.Parse(data))));
            };
            foreach (string line in File.ReadLines(path))
            {
                using var doc = JsonDocument.Parse(line);
                var row = doc.RootElement;
                at = row.GetProperty("atMs").GetDouble();
                if (at > end) break;
                if (!row.GetProperty("outbound").GetBoolean())
                    decoder.ProcessIncoming(Convert.FromBase64String(row.GetProperty("rawBase64").GetString()!));
            }
        }
        Assert.NotEmpty(states);
        using var fixture = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        var rig = fixture.Rig;
        var manager = fixture.Robot.BehaviorManagerOwner!;
        var configured = Assert.IsType<ReactToOnChargerBehavior>(fixture.Runtime.Bound["ReactToOnCharger"]);
        Assert.Same(configured, manager.Reactions.Single(r => r.Strategy.Trigger == ReactionTrigger.PlacedOnCharger).Behavior);
        using var bridge = new CozmoBridge(rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var notices = new List<byte[]>();
        rig.Engine.EngineToGameMessage += (tag, body) => { if (tag == 0x2D) notices.Add(body.ToArray()); };
        var transitions = new List<ReactionTransitionReport>();
        rig.Engine.ReactionTransition += transitions.Add;
        int cancels = 0;
        fixture.Robot.CancelIdleTimeoutDelivered += () => cancels++;
        var disconnected = new List<RobotDisconnectedMessage>();
        rig.Engine.RobotDisconnected += disconnected.Add;

        // Earlier commands in the session already opened the first-action reaction gate.
        int headCall = await client.Send("tools/call", new { name = "head", arguments = new { radians = 0.25 } });
        for (int i = 0; i < 6; i++) fixture.Motors();
        var primed = await client.Receive(headCall);
        Assert.False(primed.GetProperty("result").GetProperty("isError").GetBoolean(), primed.ToString());

        var contactEdges = new List<double>();
        bool contacts = false;
        long baseNs = rig.NowNs;
        rig.DefaultTickMs = 0;
        bool armed = false;
        double cancelAt = 0;
        foreach (var (at, state) in states)
        {
            state.PoseOriginId = fixture.Robot.CurrentOriginId;
            state.PoseFrameId = fixture.Robot.PoseFrameId;
            rig.NowNs = baseNs + (long)((at - start) * 1_000_000);
            rig.Data(state);
            rig.Tick();
            if (rig.Robot.Sensors.OnChargerContacts != contacts)
            {
                contacts = rig.Robot.Sensors.OnChargerContacts;
                contactEdges.Add(at);
            }
            armed |= fixture.Robot.Idle.DisconnectDeadline > 0;
            if (cancelAt == 0 && cancels > 0) cancelAt = at;
        }
        rig.NowNs = baseNs + (long)((end - start) * 1_000_000);
        rig.Tick();
        var events = (await client.Tool("read_events", new { since = 0 })).GetProperty("result")
            .GetProperty("structuredContent").GetProperty("events").EnumerateArray().ToArray();
        output.WriteLine($"states={states.Count}, notices={notices.Count}, armed={armed}, cancels={cancels}, cancelAt={cancelAt:F3}, contactEdges={string.Join(',', contactEdges)} transitions={string.Join(',', transitions.Select(t => t.NewTrigger))}");
        Assert.True(armed, "Recorded charger placement must arm the native deadlines.");
        Assert.Equal(new byte[] { 0 }, Assert.Single(notices));
        Assert.Equal(1, cancels);
        // Contact loss is read directly from the replay rather than rounded wall-clock notes.
        Assert.Equal(2, contactEdges.Count);
        Assert.True(cancelAt >= contactEdges[1] && cancelAt < 887193.3844);
        Assert.Contains(transitions, row => row.NewTrigger == (byte)ReactionTrigger.RobotPickedUp);
        Assert.Equal(-1f, fixture.Robot.Idle.FaceOffDeadline);
        Assert.Equal(-1f, fixture.Robot.Idle.DisconnectDeadline);
        Assert.Equal(2, rig.Engine.ConnectionState);
        Assert.Empty(disconnected);
        var sleep = events.Where(e => e.GetProperty("type").GetString() is "going-to-sleep" or "sleep-cancelled").ToArray();
        Assert.Equal(new[] { "going-to-sleep", "sleep-cancelled" }, sleep.Select(e => e.GetProperty("type").GetString()));
        Assert.False(sleep[0].GetProperty("payload").GetProperty("triggeredFromVoiceCommand").GetBoolean());
        Assert.Equal((byte)ReactionTrigger.RobotPickedUp, sleep[1].GetProperty("payload").GetProperty("reactionTrigger").GetByte());
    }
}
