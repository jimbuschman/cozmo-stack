using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class SessionFiveApiReportsTests
{
    private const string PartOne = "COZMO_SESSION5_CHARGER_BUNDLE";
    private const string LaterParts = "COZMO_SESSION5_CUBE_REACTION_BUNDLE";
    private const uint FactoryId = 0x241A8EEC;

    [Theory]
    [InlineData(PartOne, 1407500, 1411200, 1375320.3836, 3u)]
    [InlineData(LaterParts, 165950, 208100, 187384.9767, 1u)]
    public void RadioDisconnectRetiresThePreviousCommandPoseUntilAnotherObservation(
        string variable, double firstRadio, double lastRadio, double observedAt, uint capturedId)
    {
        string? bundle = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(bundle)) return;
        var radios = RecordedRobotInput.ReadInbound(bundle, at => at >= firstRadio, lastRadio, [0xD0], discardRetries: true)
            .Select(row => (ObjectConnectionState)row.Message)
            .Where(row => row.FactoryID == FactoryId).ToArray();
        var connected = radios.Last(row => row.Connected);
        var disconnected = radios.Last();
        Assert.False(disconnected.Connected);
        Assert.Equal(connected.ObjectID, disconnected.ObjectID);
        var sighting = Assert.Single(ReadBodies(bundle, 0x44, observedAt, observedAt));
        var reader = new CladReader(sighting);
        uint stamp = reader.U32();
        Assert.Equal((uint)ObjectFamily.LightCube, reader.U32());
        Assert.Equal((uint)connected.ObjectType, reader.U32());
        Assert.Equal(capturedId, reader.U32());
        for (int i = 0; i < 4; i++) reader.F32();
        var position = new Vec3(reader.F32(), reader.F32(), reader.F32());
        var rotation = new Rotation3d(reader.F32(), reader.F32(), reader.F32(), reader.F32());

        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        var world = f.M.World;
        f.Rig.Data(connected);
        f.Rig.Tick();
        uint worldId = world.ConnectedObjectIdForActiveId(connected.ObjectID)!.Value;
        var root = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        using var pose = new Pose3d(rotation, position, parent: root);
        var cube = new ObservableObject(worldId, connected.ObjectType, CubeGeometry.MarkersFor(connected.ObjectType))
        {
            OriginId = f.Robot.CurrentOriginId,
            Pose = pose,
            PoseState = PoseState.Dirty,
            LastObservedTimestamp = stamp,
            ActiveId = (int)connected.ObjectID,
            FactoryId = FactoryId
        };
        world.AddLocatedObject(cube);
        var passive = f.Add(id: 89, x: 300);
        var linked = Cubes(bridge).Single(c => HasWorldId(c, worldId));
        Assert.True(linked.GetProperty("connected").GetBoolean());
        Assert.Equal(FactoryId, linked.GetProperty("factoryId").GetUInt32());
        Assert.Equal(stamp, linked.GetProperty("lastObservedTimestamp").GetUInt32());

        var connectionReports = new List<ObjectConnectionStateMessage>();
        f.Rig.Robot.Cubes.ConnectionStateBroadcast += connectionReports.Add;
        f.Rig.Data(disconnected);
        f.Rig.Tick();
        var drop = Assert.Single(connectionReports);
        Assert.Equal((worldId, FactoryId, (byte)0), (drop.ObjectID, drop.FactoryID, drop.ConnectedByte));
        Assert.Same(cube, world.GetLocatedObjectByIdHelper(worldId));
        Assert.Equal((-1, 0u, stamp), (cube.ActiveId, cube.FactoryId, cube.LastObservedTimestamp));
        var after = Cubes(bridge);
        Assert.DoesNotContain(after, c => HasWorldId(c, worldId));
        Assert.Contains(after, c => HasWorldId(c, passive.ObjectId));
        var radio = after.Single(c => c.GetProperty("factoryId").ValueKind == JsonValueKind.Number &&
            c.GetProperty("factoryId").GetUInt32() == FactoryId);
        Assert.False(radio.GetProperty("connected").GetBoolean());
        Assert.Equal(JsonValueKind.Null, radio.GetProperty("worldObjectId").ValueKind);
        Assert.Equal(JsonValueKind.Null, radio.GetProperty("relative").ValueKind);
        using var laterBridge = new CozmoBridge(f.Rig.Robot);
        Assert.DoesNotContain(Cubes(laterBridge), c => HasWorldId(c, worldId));

        // A later camera confirmation publishes 0x44 without reconnecting the radio.
        var sightings = new List<RobotObservedObjectReport>();
        f.Rig.Engine.RobotObservedObject += sightings.Add;
        for (uint frame = 1; frame <= 5 && !sightings.Any(report => report.ObjectId == worldId); frame++)
        {
            f.Motors();
            f.Image(frame, cube: true, cubeX: 200, markerObjectType: connected.ObjectType);
        }
        Assert.Contains(sightings, report => report.ObjectId == worldId);
        var observed = Cubes(bridge).Single(c => HasWorldId(c, worldId));
        Assert.False(observed.GetProperty("connected").GetBoolean());
        Assert.Equal(JsonValueKind.Null, observed.GetProperty("factoryId").ValueKind);

        f.Rig.Data(connected);
        f.Rig.Tick();
        Assert.True(Cubes(bridge).Single(c => HasWorldId(c, worldId)).GetProperty("connected").GetBoolean());
    }

    [Fact]
    public void RecordedPickupAndDriveCompletionsCarryFourDefinedMinusOneWords()
    {
        string? bundle = Environment.GetEnvironmentVariable(LaterParts);
        if (string.IsNullOrEmpty(bundle)) return;
        var expected = new ObjectInteractionCompletion(1, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, 1, false);
        var ids = new HashSet<uint> { 2, 0x2DC7D7, 0x2DC7EB };
        foreach (var body in ReadBodies(bundle, 0x5A, 60000, 173000))
        {
            var reader = new CladReader(body);
            uint tag = reader.U32();
            if (!ids.Remove(tag)) continue;
            Assert.Equal(tag == 0x2DC7D7 ? 0x0A : 0x16, reader.I32());
            Assert.Equal(0u, reader.U32());
            byte count = reader.U8();
            for (int i = 0; i < count; i++) reader.U32();
            Assert.Equal((byte)0, reader.U8());
            // DriveToObject 0x00559CB0..0x00559CC4 and normal PickupObject
            // 0x005537FE..0x00553826 explicitly initialize the unused IDs to -1.
            Assert.Equal(expected, new ObjectInteractionCompletion(
                reader.U32(), reader.U32(), reader.U32(), reader.U32(), reader.U32(), reader.U8(), reader.Bool()));
        }
        Assert.Empty(ids);
        using var f = new Fixture(sourceNvEmpty: true);
        var drive = new NativeDriveToObjectRunner(f.M, 1);
        try { Assert.Equal(expected, drive.GetObjectInteractionCompletion()); }
        finally { drive.Prep(); drive.WatcherEnding(); }
    }

    [Fact]
    public void RecordedWifiTimeoutReportsNativeZeroAndDASReasonOne()
    {
        string? bundle = Environment.GetEnvironmentVariable(LaterParts);
        if (string.IsNullOrEmpty(bundle)) return;
        var captured = Assert.Single(ReadRows(bundle, 1153478, 1153480)
            .Where(row => row.TryGetProperty("method", out var method) && method.GetString() == "notifications/claude/channel")
            .SelectMany(row =>
            {
                using var data = JsonDocument.Parse(row.GetProperty("params").GetProperty("content").GetString()!);
                return data.RootElement.GetProperty("events").EnumerateArray().Where(e =>
                    e.GetProperty("type").GetString() == "robot-disconnected").Select(e => e.Clone()).ToArray();
            })).GetProperty("payload");
        Assert.Equal(0, captured.GetProperty("timeSinceLastMsgSec").GetSingle());
        Assert.Equal("WifiTimeout", captured.GetProperty("reason").GetString());
        Assert.Equal(1, captured.GetProperty("reasonCode").GetByte());

        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        f.Rig.Port.TimedOut = true;
        f.Rig.Port.Raise(ReceiverMarker.OnDisconnected, M4ControlTests.Rig.RobotEp);
        f.Rig.Tick();
        var report = Assert.Single(bridge.ReadEvents().GetProperty("events").EnumerateArray().Where(e =>
            e.GetProperty("type").GetString() == "robot-disconnected")).GetProperty("payload");
        // RCM uses flag +0xA1 for the DAS reason at 0x0062FA34..0x0062FA3C.
        // RemoveRobot passes an independent literal +0f at 0x0052F2BE..0x0052F2C4.
        Assert.Equal(captured.GetRawText(), report.GetRawText());
        Assert.False(bridge.GetState().GetProperty("connection").GetProperty("connected").GetBoolean());
    }

    private static JsonElement[] Cubes(CozmoBridge bridge) =>
        bridge.GetState().GetProperty("knownCubes").EnumerateArray().ToArray();

    private static bool HasWorldId(JsonElement cube, uint id) =>
        cube.GetProperty("worldObjectId") is { ValueKind: JsonValueKind.Number } value && value.GetUInt32() == id;

    private static IEnumerable<byte[]> ReadBodies(string bundle, ushort tag, double start, double end) =>
        ReadRows(bundle, start, end)
            .Where(row => row.GetProperty("kind").GetString() == "engine-to-game" && row.GetProperty("tag").GetString() == $"0x{tag:X2}")
            .Select(row => Convert.FromHexString(row.GetProperty("bodyHex").GetString()!));

    private static IEnumerable<JsonElement> ReadRows(string bundle, double start, double end)
    {
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (at > end) yield break;
            if (at >= start) yield return row;
        }
    }
}
