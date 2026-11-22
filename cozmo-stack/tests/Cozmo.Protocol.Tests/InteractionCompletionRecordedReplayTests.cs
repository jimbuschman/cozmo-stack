using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class InteractionCompletionRecordedReplayTests
{
    [Theory]
    [InlineData("094833", 26, "roll", "DriveToRollObject", 8, 0x1F)]
    [InlineData("094833", 29, "turn-to-cube", "TurnTowardsObject", 9, 0x2C)]
    [InlineData("182433", 63, "roll", "DriveToRollObject", 23, 0x1F)]
    public void RecordedSuccessfulActionEndingPublishesTheOriginalTagAndDefinedFields(
        string session, int request, string tool, string actionName, int tag, int type)
    {
        string? root = Environment.GetEnvironmentVariable("COZMO_SHORT_PLANNER_REPLAY_ROOT");
        if (string.IsNullOrEmpty(root)) return;
        var events = Path.Combine(root, $"20261119-{session}-LLM-SESSION", "events.jsonl");
        uint? objectId = null;
        float maximumTurn = 0;
        bool loggedSuccess = false;
        uint? result = null;
        foreach (string line in File.ReadLines(events))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            string? kind = row.GetProperty("kind").GetString();
            bool requestedRow = row.TryGetProperty("id", out var id) &&
                id.ValueKind == JsonValueKind.Number && id.GetInt32() == request;
            if (kind == "mcp-input" && requestedRow)
            {
                var call = row.GetProperty("params");
                Assert.Equal(tool, call.GetProperty("name").GetString());
                var args = call.GetProperty("arguments");
                objectId = args.GetProperty("objectId").GetUInt32();
                if (args.TryGetProperty("maximumTurnRadians", out var maximum)) maximumTurn = maximum.GetSingle();
            }
            if (objectId is null) continue;
            if (kind == "engine-log" && row.GetProperty("name").GetString() == "IActionRunner.Update.ActionCompleted" &&
                row.GetProperty("text").GetString() == $"{actionName} [{tag}] succeeded with state SUCCESS.")
                loggedSuccess = true;
            if (kind == "engine-to-game" && row.GetProperty("tag").GetString() == "0x5A")
                Assert.NotEqual((uint)tag, new CladReader(Convert.FromHexString(row.GetProperty("bodyHex").GetString()!)).U32());
            if (kind != "mcp-output" || !requestedRow) continue;
            var reply = row.GetProperty("result").GetProperty("structuredContent");
            Assert.True(reply.GetProperty("admitted").GetBoolean());
            result = reply.GetProperty("engineResult").GetUInt32();
            break;
        }
        Assert.Equal(1u, objectId);
        Assert.True(loggedSuccess);
        Assert.Equal(0u, result);

        // Replay at the recorded terminal state. Use the real action and compound proxy
        // for the union, followed by the native watcher snapshot and app delivery.
        using var f = new Fixture(sourceNvEmpty: true);
        ActionRunner runner;
        if (tool == "roll")
            runner = new NativeDriveToRollRunner(f.M, objectId.Value);
        else
        {
            var turn = new NativeTurnTowardsObjectRunner(f.M, objectId.Value,
                NativeTurnTowardsObjectRunner.AnyCode, maximumTurn, false, false);
            turn.ConfigurePublicRequest(f.Robot, () => true);
            runner = turn;
        }
        Assert.True(runner.SetTag((uint)tag));
        Assert.Equal(type, runner.Type);
        runner.State = result.Value;
        try
        {
            var api = new List<ActionCompletionReport>();
            var raw = new List<byte[]>();
            var logs = new List<string>();
            f.Rig.Engine.StackLogLine += logs.Add;
            f.Rig.Engine.ActionCompleted += api.Add;
            f.Rig.Engine.EngineToGameMessage += (kind, body) => { if (kind == 0x5A) raw.Add(body.ToArray()); };
            var watcher = f.Robot.ActionList.Watcher;
            watcher.SendGameMessage(watcher.BuildCompletedAction(runner)!.Value);
            f.Rig.Engine.DrainAppDelivery();

            var report = Assert.Single(api);
            Assert.Equal(((uint)tag, type, 0u, (byte?)0),
                (report.Tag, report.ActionType, report.Result, report.CompletionVariant));
            Assert.Equal(objectId.Value, report.ObjectInteraction!.Word0);
            var reader = new CladReader(Assert.Single(raw));
            Assert.Equal(((uint)tag, type, 0u, (byte)0, (byte)0), (reader.U32(), reader.I32(), reader.U32(), reader.U8(), reader.U8()));
            Assert.Equal((objectId.Value, 0u, 0u, 0u, 0u, (byte)1, (byte)0),
                (reader.U32(), reader.U32(), reader.U32(), reader.U32(), reader.U32(), reader.U8(), reader.U8()));
            Assert.Single(logs, line => line.Contains("zeroed four undefined object-interaction ID words") &&
                line.Contains("operator decision 2026-11-22"));
        }
        finally
        {
            runner.Prep();
            runner.WatcherEnding();
        }
    }
}
