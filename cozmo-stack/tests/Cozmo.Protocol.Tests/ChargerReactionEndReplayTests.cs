using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class ChargerReactionEndReplayTests(ITestOutputHelper output)
{
    private static readonly string[] ChargerLockTriggers =
    [
        "CubeMoved", "FacePositionUpdated", "FistBump", "Frustration", "Hiccup", "NoPreDockPoses",
        "ObjectPositionUpdated", "PetInitialDetection", "Sparked", "UnexpectedMovement",
    ];

    [Fact]
    public async Task ContactLossKeepsChargerReactionUntilRecordedPickupAndTreads()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_CHARGER_END_BUNDLE");
        if (string.IsNullOrWhiteSpace(bundle)) return;
        const double start = 100000, end = 271000, firstRefusal = 169829.43, secondRefusal = 236389.287;
        const string lockName = "ReactToOnCharger_behaviorLock";
        var inputs = RecordedRobotInput.ReadInbound(bundle, time => time >= start, end, [0xF0])
            .Select(item => (item.At, State: Assert.IsType<RobotState>(item.Message))).ToArray();
        Assert.NotEmpty(inputs);

        using var fixture = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true,
            keepNeedsFull: true, bodyHwVersion: 4);
        var rig = fixture.Rig;
        var robot = fixture.Robot;
        var manager = robot.BehaviorManagerOwner!;
        rig.Robot.SetEnableFreeplayActivity(false);
        using var bridge = new CozmoBridge(rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();

        // The original session already had admitted actions before this window.
        int prime = await client.Send("tools/call", new { name = "head", arguments = new { radians = 0.25 } });
        for (int i = 0; i < 6; i++) fixture.Motors();
        Assert.Equal("SUCCESS", Data(await client.Receive(prime)).GetProperty("engineResultName").GetString());

        var contacts = new List<(double At, bool Contacts)>();
        var contactReports = new List<byte>();
        var typedContacts = new List<bool>();
        var contactLogs = new List<string>();
        rig.Engine.EngineToGameMessage += (tag, body) => { if (tag == 0x39) contactReports.Add(Assert.Single(body.ToArray())); };
        rig.Robot.Sensors.ChargerEvent += typedContacts.Add;
        rig.Engine.LogEntry += entry =>
        {
            if (entry.Name is "robot.on_charger" or "robot.off_charger") contactLogs.Add(entry.Name);
        };
        var reactions = new List<ReactionTransitionReport>();
        rig.Engine.ReactionTransition += reactions.Add;
        int cancels = 0;
        robot.CancelIdleTimeoutDelivered += () => cancels++;
        int refused = 0;
        double lastTime = 0;
        float deadline = -1;
        long baseNs = rig.NowNs;
        rig.DefaultTickMs = 0;
        foreach (var (at, state) in inputs)
        {
            bool before = rig.Robot.Sensors.OnChargerContacts;
            state.PoseOriginId = robot.CurrentOriginId;
            state.PoseFrameId = robot.PoseFrameId;
            rig.NowNs = baseNs + (long)((at - start) * 1_000_000);
            rig.Data(state);
            rig.Tick();
            lastTime = at;
            if (before != rig.Robot.Sensors.OnChargerContacts)
                contacts.Add((at, rig.Robot.Sensors.OnChargerContacts));

            if (refused == 0 && at >= firstRefusal)
            {
                Assert.True(rig.Robot.Sensors.OnChargerContacts);
                deadline = robot.Idle.DisconnectDeadline;
                Assert.True(deadline > 0);
                await Refuse("drive-straight", new { distanceMm = 150, speedMmps = 50 });
                refused++;
            }
            if (at is > 230950 and < 241590)
            {
                Assert.False(rig.Robot.Sensors.OnChargerContacts);
                Assert.Equal(OffTreadsState.OnTreads, rig.Robot.Sensors.OffTreadsState);
                Assert.Equal(ReactionTrigger.PlacedOnCharger, manager.CurrentReactionTrigger);
                Assert.True(robot.ActionList.ExternalActionsDisabled);
                Assert.Equal(0, cancels);
                Assert.Equal(deadline, robot.Idle.DisconnectDeadline);
            }
            if (refused == 1 && at >= secondRefusal)
            {
                await Refuse("head", new { radians = -0.15 });
                refused++;
            }
        }
        rig.Engine.DrainAppDelivery();
        Assert.True(lastTime > 270900);
        Assert.Equal(2, refused);
        Assert.Equal(new[] { true, false, true, false }, contacts.Select(edge => edge.Contacts));
        Assert.Equal(125798.932, contacts[0].At, 2);
        Assert.Equal(200255.030, contacts[1].At, 2);
        Assert.Equal(202478.372, contacts[2].At, 2);
        Assert.Equal(230910.180, contacts[3].At, 2);
        Assert.Equal(new byte[] { 1, 0, 1, 0 }, contactReports);
        Assert.Equal(contacts.Select(edge => edge.Contacts), typedContacts);
        Assert.Equal(new[] { "robot.on_charger", "robot.off_charger", "robot.on_charger", "robot.off_charger" }, contactLogs);
        var afterPlacement = reactions.SkipWhile(r => r.NewTrigger != (byte)ReactionTrigger.PlacedOnCharger).Take(4).ToArray();
        Assert.Equal(new byte[] { 9, 12, 14, 22 }, afterPlacement.Select(r => r.NewTrigger));
        Assert.Equal(1, cancels);
        Assert.Equal(-1f, robot.Idle.DisconnectDeadline);
        Assert.Equal(-1f, robot.Idle.FaceOffDeadline);
        Assert.False(robot.ActionList.ExternalActionsDisabled);
        Assert.DoesNotContain(manager.GetReactionLocks(), row => row.Id == lockName);
        Assert.Equal(2, rig.Engine.ConnectionState);

        // Run the next external command after the two physical reactions release ownership.
        rig.DefaultTickMs = 60;
        int next = await client.Send("tools/call", new { name = "head", arguments = new { radians = -0.15 } });
        for (int i = 0; i < 8; i++) fixture.Motors();
        Assert.True(Data(await client.Receive(next)).GetProperty("admitted").GetBoolean());
        output.WriteLine($"states={inputs.Length}, contactEdges={string.Join(", ",contacts)}, transitions={string.Join(", ",afterPlacement.Select(r => r.NewTrigger))}, idleCancels={cancels}");

        async Task Refuse(string name, object arguments)
        {
            Assert.Equal("ReactToOnCharger", manager.Current?.Id);
            Assert.Equal(ReactionTrigger.PlacedOnCharger, manager.CurrentReactionTrigger);
            Assert.True(robot.ActionList.ExternalActionsDisabled);
            var held = Assert.Single(manager.GetReactionLocks(), row => row.Id == lockName);
            Assert.Equal(ChargerLockTriggers, held.Triggers);
            Assert.False(manager.IsReactionTriggerEnabled(ReactionTrigger.FacePositionUpdated));
            Assert.True(manager.IsReactionTriggerEnabled(ReactionTrigger.RobotPickedUp));
            var reply = Data(await client.Tool(name, arguments));
            Assert.True(reply.TryGetProperty("status", out var status), reply.ToString());
            Assert.Equal("not-admitted", status.GetString());
            Assert.False(reply.GetProperty("admitted").GetBoolean());
            Assert.Equal(JsonValueKind.Null, reply.GetProperty("engineResult").ValueKind);
            var why = reply.GetProperty("why");
            var reaction = why.GetProperty("snapshot").GetProperty("reaction");
            Assert.Equal("PlacedOnCharger", reaction.GetProperty("trigger").GetString());
            Assert.Equal("ReactToOnCharger", reaction.GetProperty("behavior").GetString());
            Assert.Contains("Pick Cozmo up", why.GetProperty("hint").GetString());
        }
    }

    [Fact]
    public async Task AppCancelIdleTimeoutReleasesAChargerReactionWithContactsStillOn()
    {
        // Unity's sleep-dialog Cancel action sends G2E 0x54. The reaction consumes
        // that latch in its next update even when the robot still has contact.
        using var fixture = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        var rig = fixture.Rig;
        var robot = fixture.Robot;
        var manager = robot.BehaviorManagerOwner!;
        rig.Robot.SetEnableFreeplayActivity(false);
        var prime = rig.Robot.Motion.QueueAiHeadAngleAsync(0.25f);
        for (int i = 0; i < 360; i++) fixture.Motors(); // also crosses the placement strategy's first-call deadline
        Assert.True((await prime).Admitted);

        const RobotStatusFlag contacts = RobotStatusFlag.IsOnCharger | RobotStatusFlag.IsCharging |
            RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos | RobotStatusFlag.IsBodyAccMode;
        for (int i = 0; i < 3; i++) rig.State(contacts, robot.CurrentOriginId, head: fixture.Head);
        Assert.Equal(ReactionTrigger.PlacedOnCharger, manager.CurrentReactionTrigger);
        Assert.True(robot.ActionList.ExternalActionsDisabled);
        Assert.True(robot.Idle.DisconnectDeadline > 0);

        rig.Engine.CancelIdleTimeout();
        for (int i = 0; i < 3; i++) rig.State(contacts, robot.CurrentOriginId, head: fixture.Head);
        Assert.True(rig.Robot.Sensors.OnChargerContacts);
        Assert.Null(manager.CurrentReactionTrigger);
        Assert.False(robot.ActionList.ExternalActionsDisabled);
        Assert.Equal(-1f, robot.Idle.DisconnectDeadline);
        Assert.DoesNotContain(manager.GetReactionLocks(), row => row.Id == "ReactToOnCharger_behaviorLock");
    }

    private static JsonElement Data(JsonElement reply) =>
        reply.GetProperty("result").GetProperty("structuredContent");
}
