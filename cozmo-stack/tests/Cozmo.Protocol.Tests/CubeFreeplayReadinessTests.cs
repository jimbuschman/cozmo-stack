using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;
using Xunit.Abstractions;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class CubeFreeplayReadinessTests(ITestOutputHelper output)
{
    [Fact]
    public void RecordedChargerBootWaitsInSelectionAndDriveOffWinsWhenFreeplayIsEnabled()
    {
        var resources = PickupPyramidNativeExecutionTests.Resources();
        using var rig = new M4ControlTests.Rig(resources, keepNeedsFull: true);
        rig.Robot.Animations.LoadFrom(resources);
        rig.ToSynced();
        rig.Calibrate();
        var robot = rig.Engine.Robot!;
        var runtime = robot.NativeBehaviorRuntime!;
        var manager = robot.BehaviorManagerOwner!;
        var driveOff = Assert.IsType<DriveOffChargerBehavior>(runtime.Bound["DriveOffCharger"]);

        var bundle = Environment.GetEnvironmentVariable("COZMO_CUBE_FREEPLAY_BUNDLE");
        if (bundle is not null)
        {
            VerifyRecordedSelection(bundle);
            var input = ReadChargerStartupStates(bundle).ToArray();
            Assert.NotEmpty(input);
            double previous = input[0].At - 60;
            foreach (var (at,state) in input)
            {
                state.PoseOriginId = robot.CurrentOriginId;
                state.PoseFrameId = robot.PoseFrameId;
                rig.Data(state);
                rig.Tick(at - previous);
                previous = at;
            }
            rig.ContinueRobotTimestampAfterReplay(input[^1].State.Timestamp);
        }
        else
        {
            rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger);
        }

        Assert.True(rig.Robot.Sensors.OnChargerPlatform); // 00511D4C..00511D6A.
        Assert.True(driveOff.IsRunnable(runtime.Context)); // 005C0B10.
        Assert.Same(runtime.Bound["Wait"], manager.Current);
        Assert.Equal("Selection", runtime.Observe().Activity);
        int first = rig.Mark();
        rig.Tick(60);
        Assert.DoesNotContain(rig.SentSince(first), packet => packet is ExecutePath);

        // Counterfactual from the same starting state. The archive used an explicit
        // drive-straight while Selection was still parked.
        var objectives = new List<uint>();
        using var outcomes = rig.Engine.AnimationMessages.SubscribeMessage(0x8B, true,
            payload => { if (payload is uint objective) objectives.Add(objective); });
        rig.Robot.SetEnableFreeplayActivity(true);
        rig.Tick(60);
        var owned = Assert.IsType<DriveOffChargerBehavior>(manager.Current);
        Assert.Contains(owned.Id, new[] { "DriveOffCharger", "Hiking_DriveOffCharger" });
        var activity = runtime.FreeplayObserver.Current!;
        Assert.Contains(activity.Chooser!.BehaviorIds, id => id == owned.Id);
        var choice = runtime.FreeplayObserver.Decisions.LastOrDefault(decision => decision.Behavior == owned.Id);
        Assert.NotNull(choice);
        output.WriteLine($"Freeplay chose {choice.Activity}/{choice.Behavior}: {choice.Reason}");
        var line = Assert.Single(rig.SentSince(first).OfType<AppendPathSegmentLine>());
        Assert.InRange(MathF.Abs(MathF.Sqrt(MathF.Pow(line.XEndMm-line.XStartMm,2) +
            MathF.Pow(line.YEndMm-line.YStartMm,2)) - owned.DriveDistanceMm),0,.001f);

        var answers = new HashSet<byte>();
        void Pump()
        {
            byte tag = rig.Robot.Animations.CurrentTag;
            if (tag != 0 && answers.Add(tag))
            {
                rig.Data(new AnimationStarted { AnimId = tag });
                rig.Data(new AnimationEnded { AnimId = tag });
            }
            rig.Tick(60);
        }
        ushort path = rig.Robot.Motion.Path.Field42;
        rig.Data(new PathFollowingEvent { EventId = path, EventType = (byte)PathEventType.Started });
        for (int i = 0; i < 8; i++) Pump();

        rig.Data(rig.MakeState(RobotStatusFlag.IsBodyAccMode,origin:robot.CurrentOriginId,frame:robot.PoseFrameId,
            x:line.XEndMm,y:line.YEndMm));
        rig.Tick();
        rig.Data(new PathFollowingEvent { EventId = path, EventType = (byte)PathEventType.Completed });
        for (int i = 0; i < 20 && !owned.LeftChargerOnTreads; i++) Pump();

        Assert.Contains((uint)DriveOffChargerBehavior.ObjectiveDriveOffCharger, objectives);
        Assert.Equal(ActionResult.Success, owned.DriveResult);
        Assert.True(owned.LeftChargerOnTreads);
        Assert.NotNull(owned.DroveOffAtSec);
        Assert.Equal((float)owned.DroveOffAtSec.Value, runtime.Context.LastDriveOffChargerSec);
    }

    private static IEnumerable<(double At,RobotState State)> ReadChargerStartupStates(string bundle)
    {
        foreach (string line in File.ReadLines(Path.Combine(bundle,"messages.jsonl")))
        {
            using var json = JsonDocument.Parse(line);
            var row = json.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (at > 14997) yield break; // The recorded on-charger Selection snapshot.
            if (row.GetProperty("outbound").GetBoolean() || row.GetProperty("tag").GetString() != "0xF0") continue;
            yield return (at,Assert.IsType<RobotState>(RobotMessage.Parse(Convert.FromHexString(row.GetProperty("payloadHex").GetString()!))));
        }
    }

    private static void VerifyRecordedSelection(string bundle)
    {
        var snapshot = File.ReadLines(Path.Combine(bundle,"events.jsonl")).Select(line => JsonSerializer.Deserialize<JsonElement>(line))
            .Single(row => row.GetProperty("atMs").GetDouble() is >= 14996 and <= 14997 &&
                row.GetProperty("kind").GetString() == "mcp-output");
        var state = snapshot.GetProperty("result").GetProperty("structuredContent");
        Assert.True(state.GetProperty("isOnCharger").GetBoolean());
        Assert.Equal("Selection",state.GetProperty("highLevelActivity").GetString());
        Assert.Equal("Wait",state.GetProperty("behavior").GetString());
    }
}
