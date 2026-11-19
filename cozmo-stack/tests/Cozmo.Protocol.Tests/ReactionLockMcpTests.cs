using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
using Client = Cozmo.Protocol.Tests.McpServerTests.Client;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed partial class ReactionLockMcpTests(ITestOutputHelper output)
{
    [Fact]
    public async Task NamedLocksUseTheNativeTriggerSetsAndReleaseOnlyTheirOwnEntries()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        var manager = f.Robot.BehaviorManagerOwner!;

        Assert.Empty(Locks(Data(await client.Tool("get_state", new { }))));
        await Lock(client, "stacking", new[] { "ObjectPositionUpdated" });
        await Lock(client, "stacking", new[] { "CubeMoved", "ObjectPositionUpdated" });
        await Lock(client, "watching", new[] { "CubeMoved" });
        var held = Locks(Data(await client.Tool("get_state", new { })));
        Assert.Equal(new[] { "CubeMoved", "ObjectPositionUpdated" }, held["stacking"]);
        Assert.Equal(new[] { "CubeMoved" }, held["watching"]);
        Assert.False(manager.IsReactionTriggerEnabled(ReactionTrigger.CubeMoved));
        Assert.False(manager.IsReactionTriggerEnabled(ReactionTrigger.ObjectPositionUpdated));

        var unlocked = Data(await client.Tool("unlock-reactions", new { id = "stacking" }));
        Assert.Equal("confirmed", unlocked.GetProperty("status").GetString());
        Assert.DoesNotContain("stacking", Locks(unlocked.GetProperty("report")));
        Assert.False(manager.IsReactionTriggerEnabled(ReactionTrigger.CubeMoved));
        Assert.True(manager.IsReactionTriggerEnabled(ReactionTrigger.ObjectPositionUpdated));

        await Lock(client, "everything", "all");
        held = Locks(Data(await client.Tool("get_state", new { })));
        Assert.Equal(manager.MappedTriggers.Order().Select(t => t.ToString()), held["everything"]);
        Assert.All(manager.MappedTriggers, t => Assert.False(manager.IsReactionTriggerEnabled(t)));
        await client.Tool("unlock-reactions", new { id = "everything" });
        Assert.False(manager.IsReactionTriggerEnabled(ReactionTrigger.CubeMoved));
        await client.Tool("unlock-reactions", new { id = "watching" });
        Assert.Empty(Locks(Data(await client.Tool("get_state", new { }))));
    }

    [Fact]
    public async Task AdmissionWaitKeepsTheOriginalRefusalOnTimeoutAndRequeuesOnceOnExit()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        await SetUpBehavior(f);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        var manager = f.Robot.BehaviorManagerOwner!;
        var cube = f.Add(x: 125);
        Observe(f, cube, f.Robot.StoredState!.Timestamp);
        Assert.Equal(ReactionTrigger.ObjectPositionUpdated, manager.CheckReactions(f.Rig.Engine.Timer.Seconds)?.Trigger);
        Assert.True(f.Robot.ActionList.ExternalActionsDisabled);
        f.Rig.Engine.DrainAppDelivery();

        var timeout = Data(await client.Tool("head", new { radians = 0.25, waitAdmittedMs = 35 }));
        Assert.Equal("not-admitted", timeout.GetProperty("status").GetString());
        Assert.Equal("External actions are disabled by a reaction", timeout.GetProperty("detail").GetString());
        CheckWait(timeout, "ObjectPositionUpdated", timedOut: true);

        int call = await client.Send("tools/call",
            new { name = "head", arguments = new { radians = 0.25, waitAdmittedMs = 3000 } });
        Assert.True(f.Robot.ActionList.ExternalActionsDisabled);
        await Lock(client, "task", new[] { "ObjectPositionUpdated" });
        f.Rig.Engine.DrainAppDelivery();
        Assert.Null(manager.CurrentReactionTrigger);
        Assert.False(f.Robot.ActionList.ExternalActionsDisabled);
        int mark = f.Rig.Mark();
        for (int i = 0; i < 80 && !f.Rig.SentSince(mark).OfType<SetHeadAngle>().Any(h => h.AngleRad == 0.25f); i++)
        {
            f.Motors();
            await Task.Delay(5);
        }
        for (int i = 0; i < 5; i++) f.Motors();
        Assert.Single(f.Rig.SentSince(mark).OfType<SetHeadAngle>(), h => h.AngleRad == 0.25f);
        var reply = Data(await client.Receive(call));
        Assert.Equal("SUCCESS", reply.GetProperty("engineResultName").GetString());
        CheckWait(reply, "ObjectPositionUpdated", timedOut: false);
        Assert.Contains(reply.GetProperty("during").GetProperty("events").EnumerateArray(),
            e => e.GetProperty("type").GetString() == "reaction-changed");
    }

    [Fact]
    public async Task AdmissionWaitDoesNotRepeatAnAdmittedActionThatAReactionCancels()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        await SetUpBehavior(f);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();

        int mark = f.Rig.Mark();
        int call = await client.Send("tools/call",
            new { name = "head", arguments = new { radians = 0.25, waitAdmittedMs = 3000 } });
        f.Rig.Tick();
        Assert.Single(f.Rig.SentSince(mark).OfType<SetHeadAngle>(), h => h.AngleRad == 0.25f);
        Observe(f, f.Add(x: 125), f.Robot.StoredState!.Timestamp);
        var manager = f.Robot.BehaviorManagerOwner!;
        Assert.Equal(ReactionTrigger.ObjectPositionUpdated, manager.CheckReactions(f.Rig.Engine.Timer.Seconds)?.Trigger);
        f.Rig.Tick();

        var reply = Data(await client.Receive(call));
        Assert.True(reply.GetProperty("admitted").GetBoolean());
        Assert.Equal("CANCELLED_WHILE_RUNNING", reply.GetProperty("engineResultName").GetString());
        Assert.Empty(Names(reply.GetProperty("admissionWait").GetProperty("reactions")));
        await Lock(client, "task", new[] { "ObjectPositionUpdated" });
        for (int i = 0; i < 5; i++) f.Motors();
        Assert.Single(f.Rig.SentSince(mark).OfType<SetHeadAngle>(), h => h.AngleRad == 0.25f);
    }

    [Theory]
    [InlineData("head", """{"radians":0.25,"waitAdmittedMs":30001}""")]
    [InlineData("head", """{"radians":0.25,"waitAdmittedMs":-1}""")]
    [InlineData("head", """{"radians":0.25,"waitAdmittedMs":1.5}""")]
    [InlineData("lock-reactions", """{"id":"a","triggers":"CubeMoved"}""")]
    [InlineData("lock-reactions", """{"id":"a","triggers":["cubemoved"]}""")]
    [InlineData("lock-reactions", """{"id":"a","triggers":[]}""")]
    [InlineData("lock-reactions", """{"id":"a","triggers":["CubeMoved","CubeMoved"]}""")]
    public async Task InvalidAdmissionAndLockArgumentsAreRefused(string name, string json)
    {
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        using var args = JsonDocument.Parse(json);
        var reply = await bridge.ExecuteAsync(name, args.RootElement);
        Assert.Equal("invalid-arguments", reply.GetProperty("error").GetProperty("code").GetString());
    }

    private static async Task SetUpBehavior(Fixture f)
    {
        f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.Rig.Tick();
        var manager = f.Robot.BehaviorManagerOwner!;
        manager.Activity = null;
        Assert.True(await manager.StartAsync("Wait", f.Rig.Engine.Timer.Seconds));
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now, new GateAction());
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        f.Robot.ActionList.Clear();
    }

    private static void Observe(Fixture f, ObservableObject cube, uint timestamp)
    {
        var manager = f.Robot.BehaviorManagerOwner!;
        manager.Reactions.Select(r => r.Strategy).OfType<ObjectPositionUpdatedStrategy>().Single()
            .OnObserved(new ObjectObservation(cube, timestamp, [], cube.Pose, cube.PoseState, false, 0));
        manager.Reactions.Select(r => r.Strategy).OfType<CubeMovedReactionStrategy>().Single()
            .ObjectObserved(cube.ObjectId);
    }

    private static async Task Lock(Client client, string id, object triggers)
    {
        var reply = Data(await client.Tool("lock-reactions", new { id, triggers }));
        Assert.Equal("confirmed", reply.GetProperty("status").GetString());
    }

    private static Dictionary<string, string[]> Locks(JsonElement state) =>
        state.GetProperty("reactionLocks").EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetString()!, item => Names(item.GetProperty("triggers")));

    private static string[] Names(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToArray();

    private static void CheckWait(JsonElement reply, string trigger, bool timedOut)
    {
        var wait = reply.GetProperty("admissionWait");
        Assert.Equal(new[] { trigger }, Names(wait.GetProperty("reactions")));
        Assert.True(wait.GetProperty("waitedMs").GetDouble() > 0);
        Assert.Equal(timedOut, wait.GetProperty("timedOut").GetBoolean());
    }

    private static JsonElement Data(JsonElement reply) => reply.GetProperty("result").GetProperty("structuredContent");

    private sealed class GateAction() : ActionRunner(8, 0)
    {
        protected override string ActionName => "initial native reaction gate";
        public override bool CanInterrupt() => false;
        public override uint CheckIfDone() => EngineActionResult.Running;
    }
}
