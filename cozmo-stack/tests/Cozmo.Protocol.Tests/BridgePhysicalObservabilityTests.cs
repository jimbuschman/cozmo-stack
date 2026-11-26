using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
using static Cozmo.Protocol.Tests.BridgeEventPushTests;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class BridgePhysicalObservabilityTests
{
    private sealed class Gate() : ActionRunner(8, 0)
    {
        protected override string ActionName => "native reaction gate";
        public override bool CanInterrupt() => false;
        public override uint CheckIfDone() => EngineActionResult.Running;
    }

    [Fact]
    public async Task StateUsesActualPacketAndConfiguredReactionEvaluationsWithoutConsumingThemAgain()
    {
        using var f = new Fixture(sourceNvEmpty: true, keepNeedsFull: true, bodyHwVersion: 4);
        f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.Rig.Tick(4000);
        f.Robot.HandleFirmwareVersion(new FirmwareVersion { Signature = System.Text.Encoding.UTF8.GetBytes("{\"version\":2457}") });
        Assert.True(f.Robot.IsPhysicalRobot);
        var manager = f.Robot.BehaviorManagerOwner!;
        manager.Activity = null;
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now, new Gate());
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        f.Robot.ActionList.Clear();
        var input = f.Rig.MakeState(RobotStatusFlag.IsMoving | RobotStatusFlag.AreWheelsMoving | RobotStatusFlag.LiftInPos,
            origin: f.Robot.CurrentOriginId, frame: f.Robot.PoseFrameId);
        input.Pose.Pitch = .2f;
        input.Gyro = new GyroData { X = .03f, Y = -.04f, Z = .05f };
        input.Accel = new AccelData { X = 100, Y = -200, Z = 9700 };
        input.LwheelSpeedMmps = 4; input.RwheelSpeedMmps = 5;
        f.Rig.Data(input);
        f.Rig.Tick();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var state = Data(await client.Tool("get_state", new { }));
        var physical = state.GetProperty("physical");
        Assert.Equal(input.Timestamp, physical.GetProperty("robotTimestamp").GetUInt32());
        Assert.Equal(.2f, physical.GetProperty("pitchRad").GetSingle());
        Assert.Equal(JsonValueKind.Null, physical.GetProperty("rollRad").ValueKind);
        Assert.Equal(-.04f, physical.GetProperty("gyroRadps").GetProperty("y").GetSingle());
        Assert.Equal(9700f, physical.GetProperty("accelerometerMmps2").GetProperty("z").GetSingle());
        Assert.True(physical.GetProperty("moving").GetBoolean());
        Assert.False(physical.GetProperty("still").GetBoolean());
        Assert.True(physical.GetProperty("wheelsMoving").GetBoolean());
        Assert.True(physical.GetProperty("headMoving").GetBoolean());
        Assert.True(physical.GetProperty("liftInPosition").GetBoolean());
        Assert.False(physical.GetProperty("pickedUp").GetBoolean());
        Assert.Equal(f.Rig.Robot.Sensors.OffTreadsState.ToString(), physical.GetProperty("offTreadsState").GetString());
        var checks = state.GetProperty("physicalReactionChecks").EnumerateArray().ToArray();
        Assert.Equal(9, checks.Length);
        JsonElement Check(string trigger) => Assert.Single(checks, e => e.GetProperty("trigger").GetString() == trigger);
        var slope = Check("RobotPlacedOnSlope").GetProperty("lastEvaluation");
        Assert.InRange(slope.GetProperty("pitchDeg").GetDouble(), 11.45, 11.47);
        Assert.Equal(.05f, slope.GetProperty("maxAbsGyroRadps").GetSingle());
        Assert.Equal(10f, slope.GetProperty("minPitchDeg").GetSingle());
        Assert.Equal(55f, slope.GetProperty("maxPitchDeg").GetSingle());
        Assert.Equal(.01f, slope.GetProperty("gyroQuietThresholdRadps").GetSingle());
        Assert.Equal(.4, slope.GetProperty("requiredQuietSec").GetDouble());
        Assert.Equal(1.5, slope.GetProperty("pickupWindowSec").GetDouble());
        Assert.False(slope.GetProperty("gyroQuietLongEnough").GetBoolean());
        Assert.False(slope.GetProperty("wantsToRun").GetBoolean());
        var movement = Check("UnexpectedMovement").GetProperty("detector");
        Assert.Equal("wheels-below-threshold", movement.GetProperty("branch").GetString());
        Assert.Equal(9f, movement.GetProperty("wheelAbsSumMmps").GetSingle());
        Assert.Equal(20f, movement.GetProperty("minWheelSpeedSumMmps").GetSingle());
        Assert.Equal(10, movement.GetProperty("countThreshold").GetInt32());
        Assert.Equal("InAir", Check("RobotPickedUp").GetProperty("lastEvaluation").GetProperty("requiredOffTreadsStates")[0].GetString());
        Assert.Equal("OnTreads", Check("ReturnedToTreads").GetProperty("lastEvaluation").GetProperty("requiredOffTreadsStates")[0].GetString());
        Assert.Equal("OnBack", Check("RobotOnBack").GetProperty("lastEvaluation").GetProperty("requiredOffTreadsStates")[0].GetString());
        Assert.Equal("OnFace", Check("RobotOnFace").GetProperty("lastEvaluation").GetProperty("requiredOffTreadsStates")[0].GetString());
        Assert.Equal(2, Check("RobotOnSide").GetProperty("lastEvaluation").GetProperty("requiredOffTreadsStates").GetArrayLength());
        Assert.Equal(3000, Check("RobotFalling").GetProperty("lastEvaluation").GetProperty("timeoutMs").GetInt32());
        Assert.Equal(1000f, Check("RobotFalling").GetProperty("impact").GetProperty("intensityThreshold").GetSingle());
        Assert.False(Check("CliffDetected").GetProperty("lastEvaluation").GetProperty("wantsToRun").GetBoolean());
        // No tick, no new native predicate evaluation or latch consumption on a read.
        var reread = Data(await client.Tool("get_state", new { })).GetProperty("physicalReactionChecks");
        Assert.Equal(state.GetProperty("physicalReactionChecks").GetRawText(), reread.GetRawText());
        // Inbound falling-stop reaches the configured ReactToImpact recipient;
        // the intensity is reported from the same comparison, not reconstructed.
        f.Rig.Data(new FallingStopped { Timestamp = input.Timestamp, DurationMs = 200, ImpactIntensity = 1500f });
        f.Rig.Tick();
        var impact = Assert.Single(Data(await client.Tool("get_state", new { })).GetProperty("physicalReactionChecks").EnumerateArray(),
            e => e.GetProperty("trigger").GetString() == "RobotFalling").GetProperty("impact");
        Assert.Equal(1500f, impact.GetProperty("lastIntensity").GetSingle());
        Assert.True(impact.GetProperty("impactRecorded").GetBoolean());
    }

    [Fact]
    public async Task MapReportsEstimatedChargerTiltAndTheViewAtItsStoredSighting()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        f.Motors();
        uint stamp = f.Robot.StoredState!.Timestamp;
        var rotation = Rotation3d.FromAngleAxis(MathF.PI / 18, new Vec3(0, 1, 0)) *
            Rotation3d.FromAngleAxis(MathF.PI / 36, new Vec3(1, 0, 0));
        // The robot's origin is exactly 45 degrees off the charger's front axis.
        var robotInCharger = rotation.Rotate(new Vec3(-100, 100, 0));
        var charger = new ObservableObject(13, ObjectType.Charger_Basic, ChargerGeometry.Markers)
        {
            OriginId = f.Robot.CurrentOriginId,
            PoseState = PoseState.Known,
            LastObservedTimestamp = stamp,
            Pose = new Pose3d(rotation, new Vec3(-robotInCharger.X, -robotInCharger.Y, -robotInCharger.Z), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),
        };
        f.M.World.AddLocatedObject(charger);
        f.Y = 100; f.Motors(); // Current view differs from the original sighting.
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var map = Data(await client.Tool("get_map", new { }));
        var estimate = Assert.Single(map.GetProperty("objects").EnumerateArray(), o => o.GetProperty("worldObjectId").GetUInt32() == 13).GetProperty("chargerEstimate");
        Assert.Equal(stamp, estimate.GetProperty("lastObservedTimestamp").GetUInt32());
        Assert.Equal(10, estimate.GetProperty("pitchDeg").GetDouble(), 3);
        Assert.Equal(5, estimate.GetProperty("rollDeg").GetDouble(), 3);
        Assert.Equal(45, estimate.GetProperty("viewingAngleDeg").GetDouble(), 3);
        Assert.NotEqual(45, estimate.GetProperty("currentViewingAngleDeg").GetDouble(), 1);
    }
}
