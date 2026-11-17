using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class BridgeEventTests
{
    [Fact]
    public void ReportsReachTheFeedWithEngineIdsAndRobotTimestamps()
    {
        using var f = new Fixture();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        var engine = f.Rig.Engine;
        var rect = new FaceRect(100, 50, 50, 50);
        var pose = new EngineGamePose(200, 15, 60, 1, 0, 0, 0, f.Robot.CurrentOriginId);
        engine.PublishRobotObservedFace(new(-3, 1000, pose, rect, "", [], [], [], []));
        engine.PublishRobotObservedFace(new(7, 1001, pose, rect, "Ada", [], [], [], []));
        engine.PublishFaceIdReport(0x47, new NativeChangedFace(-3, 7));
        engine.PublishFaceIdReport(0x4B, new NativeDeletedFace(7));
        engine.DrainAppDelivery();

        Assert.Equal(-3, Single(bridge, "face-seen", 1000).GetProperty("faceId").GetInt32());
        Assert.Equal("Ada", Single(bridge, "face-recognised", 1001).GetProperty("name").GetString());
        Assert.Equal(7, Single(bridge, "face-id-changed").GetProperty("newId").GetInt32());
        Assert.Equal(7, Single(bridge, "face-lost").GetProperty("faceId").GetInt32());

        const uint slot = 4;
        M4ControlTests.ConnectCube(f.Rig, slot);
        for (uint i = 1; i <= 4 && !Find(bridge, "cube-seen").Any(); i++)
        {
            f.Motors();
            f.Image(i, cube: true);
        }
        var seen = Assert.Single(Find(bridge, "cube-seen"));
        uint id = seen.GetProperty("payload").GetProperty("objectId").GetUInt32();
        Assert.NotEqual(slot, id);
        Assert.Equal(seen.GetProperty("robotTimestamp").GetUInt32(), seen.GetProperty("payload").GetProperty("timestamp").GetUInt32());
        f.Rig.Data(new ObjectMoved { ObjectID = slot, Timestamp = 9001 });
        f.Rig.Tick();
        var move = Single(bridge, "cube-moved", 9001).GetProperty("cube");
        Assert.Equal(id, move.GetProperty("worldObjectId").GetUInt32());
        Assert.Equal(slot, move.GetProperty("objectId").GetUInt32());
        f.Rig.Data(new ObjectTapped { ObjectID = slot, Timestamp = 9002, NumTaps = 1, TapTime = 7, TapNeg = -25, TapPos = 45 });
        f.Rig.Tick();
        var tap = Single(bridge, "cube-tapped", 9002).GetProperty("cube");
        Assert.Equal(id, tap.GetProperty("worldObjectId").GetUInt32());
        Assert.Equal(-25, tap.GetProperty("lastTap").GetProperty("tapNeg").GetInt32());

        var cube = Assert.IsType<ObservableObject>(f.M.World.GetLocatedObjectById(id));
        f.M.World.MarkObjectUnobserved(cube);
        f.M.World.MarkObjectUnobserved(cube);
        engine.DeliverObservedObjectToApp(new(1002, 4, 13, 17, float.NaN, 20, 30, 40, pose, 0, true));
        engine.BroadcastBehaviorTransition(new(0, 4, 1, 2, 3, 4, "demo"));
        engine.BroadcastReactionTransition(null, ReactionTrigger.ObjectPositionUpdated);
        engine.BroadcastActionCompletion(new(54, 18, 0x03000018, [0x02000000], 0, CompletionVariant: 6));
        engine.DrainAppDelivery();

        Assert.Equal(id, Single(bridge, "object-pose-lost").GetProperty("objectId").GetUInt32());
        var charger = Single(bridge, "charger-seen", 1002);
        Assert.Equal(17u, charger.GetProperty("objectId").GetUInt32());
        Assert.Equal("NaN", charger.GetProperty("imageX").GetString());
        Assert.Equal("demo", Find(bridge, "behavior-changed").Last().GetProperty("payload").GetProperty("displayKey").GetString());
        Assert.Equal((byte)ReactionTrigger.ObjectPositionUpdated, Find(bridge, "reaction-changed").Last().GetProperty("payload").GetProperty("newTrigger").GetByte());
        var completed = Find(bridge, "action-completed").Last().GetProperty("payload");
        Assert.Equal("TIMEOUT", completed.GetProperty("resultName").GetString());
        Assert.Equal(54u, completed.GetProperty("completion").GetProperty("tag").GetUInt32());
        Assert.Equal(0x02000000u, completed.GetProperty("completion").GetProperty("subActionResults")[0].GetUInt32());
    }

    [Fact]
    public void FeedDetectsEvictionAndDoesNotConsumeEventsOnRead()
    {
        using var f = new Fixture();
        var engine = f.Rig.Engine;
        using var bridge = new CozmoBridge(f.Rig.Robot, eventCapacity: 2);
        Assert.Empty(bridge.ReadEvents().GetProperty("events").EnumerateArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => bridge.ReadEvents(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => bridge.ReadEvents(1));

        foreach (int id in new[] { 2, 3, 4 }) engine.PublishFaceIdReport(0x4B, new NativeDeletedFace(id));
        engine.DrainAppDelivery();
        var page = bridge.ReadEvents();
        Assert.Equal(3L, page.GetProperty("cursor").GetInt64());
        Assert.Equal(2L, page.GetProperty("oldestCursor").GetInt64());
        Assert.True(page.GetProperty("gap").GetBoolean());
        var events = page.GetProperty("events").EnumerateArray().ToArray();
        Assert.Equal(new long[] { 2, 3 }, events.Select(e => e.GetProperty("cursor").GetInt64()));
        Assert.All(events, e => Assert.NotEqual(default, e.GetProperty("timestampUtc").GetDateTimeOffset()));
        Assert.False(bridge.ReadEvents(1).GetProperty("gap").GetBoolean());
        Assert.Equal(4, bridge.ReadEvents(2).GetProperty("events")[0].GetProperty("payload").GetProperty("faceId").GetInt32());
        Assert.Empty(bridge.ReadEvents(3).GetProperty("events").EnumerateArray());

        bridge.Dispose();
        engine.PublishFaceIdReport(0x4B, new NativeDeletedFace(5));
        engine.DrainAppDelivery();
        Assert.Throws<ObjectDisposedException>(() => bridge.ReadEvents());
        Assert.True(engine.IsReadyDelivered);
    }

    [Fact]
    public void StateIsReadFromTheExistingRobotWorldAndActivity()
    {
        using var f = new Fixture();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        const uint slot = 4;
        M4ControlTests.ConnectCube(f.Rig, slot);
        uint id = f.M.World.ConnectedObjectIdForActiveId(slot)!.Value;
        var observed = new TrackedFace(new DetectedFace(-7, new(100, 50, 50, 50)), f.Robot.StoredState!.Timestamp)
        {
            HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(100, 0, 60),
                parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),
        };
        Assert.NotNull(f.M.Vision.Faces.AddOrUpdateFace(observed, f.Robot.GetCheckedMainPose(), false));
        f.Rig.State(RobotStatusFlag.IsOnCharger | RobotStatusFlag.HeadInPos, battery: 3.91f);
        f.Rig.Robot.Carrying.SetCarrying(id);
        var state = bridge.GetState();
        Assert.True(state.GetProperty("connection").GetProperty("connected").GetBoolean());
        Assert.Equal(3.91f, state.GetProperty("batteryVolts").GetSingle());
        Assert.True(state.GetProperty("isOnCharger").GetBoolean());
        Assert.True(state.GetProperty("isCarrying").GetBoolean());
        Assert.Equal(id, state.GetProperty("carriedObjectId").GetUInt32());
        Assert.Equal("Selection", state.GetProperty("activity").GetString());
        Assert.Equal("Selection", state.GetProperty("highLevelActivity").GetString());
        Assert.Equal(f.Robot.BehaviorManagerOwner!.Current?.Id, state.GetProperty("behavior").GetString());
        var face = Assert.Single(state.GetProperty("knownFaces").EnumerateArray());
        Assert.Equal(-7, face.GetProperty("faceId").GetInt32());
        var cube = Assert.Single(state.GetProperty("knownCubes").EnumerateArray());
        Assert.Equal(id, cube.GetProperty("worldObjectId").GetUInt32());
        Assert.Equal(slot, cube.GetProperty("radioObjectId").GetUInt32());

        using var offline = CozmoRobot.CreateOffline();
        using var disconnected = new CozmoBridge(offline);
        var absent = disconnected.GetState();
        Assert.False(absent.GetProperty("connection").GetProperty("ready").GetBoolean());
        Assert.Equal(JsonValueKind.Null, absent.GetProperty("batteryVolts").ValueKind);
        Assert.Empty(absent.GetProperty("knownFaces").EnumerateArray());
    }

    private static IEnumerable<JsonElement> Find(CozmoBridge bridge, string kind) =>
        bridge.ReadEvents().GetProperty("events").EnumerateArray().Where(e => e.GetProperty("type").GetString() == kind);

    private static JsonElement Single(CozmoBridge bridge, string kind, uint? timestamp = null)
    {
        var item = Assert.Single(Find(bridge, kind).Where(e => !timestamp.HasValue || e.GetProperty("robotTimestamp").GetUInt32() == timestamp));
        Assert.Equal(timestamp.HasValue ? JsonValueKind.Number : JsonValueKind.Null, item.GetProperty("robotTimestamp").ValueKind);
        return item.GetProperty("payload");
    }
}
