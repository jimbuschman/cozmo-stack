using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class M13ContactChargerTests
{
    private const RobotStatusFlag Contact = RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.IsOnCharger;
    private static ObservableObject Charger(M4ControlTests.Rig rig) => Assert.Single(rig.Robot.Vision.World.LocatedObjects
        .Where(o => o.Type == ObjectType.Charger_Basic && o.OriginId == rig.Engine.Robot!.CurrentOriginId));

    private static void OwnedPose(M4ControlTests.Rig rig, float x, float y, float angle = 0)
    {
        var robot = rig.Engine.Robot!;
        using var value = new Pose3d(Rotation3d.FromAngleAxis(angle, new Vec3(0, 0, 1)),
            new Vec3(x, y, 0), "fixture", robot.GetNativeOrigin(robot.CurrentOriginId));
        robot.SetPose(value);
    }

    private static void ContactState(M4ControlTests.Rig rig, bool on)
    {
        // A1: contact observation precedes pose admission. A mismatching origin prevents this fixture packet
        // from replacing the already-owned pose after observing; no ingress pose writer is moved/substituted.
        rig.Data(rig.MakeState(on ? Contact : RobotStatusFlag.IsBodyAccMode, origin: 999, x: 900, y: 800));
        rig.Tick();
    }

    // reach: production (received RobotState -> synchronized device routing -> SetOnCharger -> real shared world).
    // Setup supplies the existing owned pose; coordinates in the contact packet deliberately disagree.
    [Fact]
    public void ContactCreatesKnownRootRelativeChargerAndRepeatedTrueObservesWithoutAnotherEdge()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var world = rig.Robot.Vision.World;
        OwnedPose(rig, 100, 200, BitConverter.UInt32BitsToSingle(0x3FC90FDB));
        var edges = new List<bool>(); rig.Robot.Sensors.ChargerEvent += edges.Add;
        ContactState(rig, true);
        var charger = Charger(rig);
        Assert.Same(rig.Robot.LocalizationWorld, world);
        Assert.Equal(PoseState.Known, charger.PoseState);
        Assert.Equal(ObjectFamily.Charger, charger.Family);
        Assert.Equal(ObjectIdSpace.UniqueIdOrUnassigned(ObjectType.Charger_Basic), charger.ObjectId);
        Assert.NotEqual(ObservableObject.UnassignedId, charger.ObjectId);
        // A3/A6: robot + Rz(pi/2)*(30,0,0), not the inverse docked-robot operation.
        Assert.InRange(charger.Pose.Translation.X, 99.9999, 100.0001);
        Assert.InRange(charger.Pose.Translation.Y, 229.9999, 230.0001);
        Assert.InRange(charger.Pose.Rotation[1,0], -1.0001, -0.9999);
        using (var root = charger.NativePose.FindRoot())
            Assert.Same(rig.Engine.Robot!.GetNativeOrigin(rig.Engine.Robot.CurrentOriginId).Node, root.Node);
        Assert.NotSame(rig.Engine.Robot!.MainPose.Node, charger.NativePose.Node!.Parent);
        Assert.False(charger.NativePose.HasLegacyFrameInput);
        Assert.Equal("Object_" + charger.ObjectId, charger.NativePose.Name);
        Assert.Equal(1, charger.PoseConfirmationCount);
        Assert.Equal(0u, charger.LastObservedTimestamp);
        Assert.Equal(0u, charger.Confirmation!.LastPoseUpdatedTime);
        uint stamp = charger.AddedToWorldTimestamp;
        Assert.Equal(rig.Robot.State.Latest!.Timestamp, stamp);
        Assert.Equal(new[] { true }, edges);

        OwnedPose(rig, 400, 500);
        Assert.InRange(charger.Pose.Translation.Y, 229.9999, 230.0001); // root-relative object does not follow robot
        ContactState(rig, true);
        Assert.Same(charger, Charger(rig));
        Assert.Equal(430d, charger.Pose.Translation.X);
        Assert.Equal(500d, charger.Pose.Translation.Y);
        Assert.Equal(stamp, charger.AddedToWorldTimestamp); // updates are not insertions
        Assert.Equal(charger.Pose.Translation, charger.ReferencePose.Translation);
        Assert.Equal(1, charger.PoseConfirmationCount);
        Assert.Equal(new[] { true }, edges);
    }

    // reach: production (real ingress and recipient wiring; observers do not replace world/map/light operations).
    [Fact]
    public void InsertionAndKnownStateRecipientsPrecedeContactOutputsAndConfirmerTail()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var world = rig.Robot.Vision.World; var order = new List<string>();
        world.Maps.ObjectRouteForTest = route => order.Add("map:" + route);
        rig.Robot.Lights.Cubes.PoseStatePlayObservedForTest = (id, trigger, layer, modifier, lights, offset) =>
        {
            Assert.Equal("Visible", trigger); Assert.Equal(2, layer); // corrected A9: NEW Known =>0x25/layer2
            var obj = world.GetLocatedObjectById(id)!;
            Assert.NotEqual(0u, obj.AddedToWorldTimestamp);
            Assert.Null(obj.Confirmation); // native notifications precede confirmer stores
            order.Add("light:Known");
        };
        rig.Robot.Sensors.OnChargerPlatformChangedInternal += on =>
        {
            Assert.True(on); Assert.False(rig.Robot.Sensors.OnChargerContacts);
            Assert.Equal(1, Charger(rig).PoseConfirmationCount);
            Assert.Contains(rig.RawSince(0), b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold
                && b[1] == 50 && b[2] == 0); // tracker hook follows threshold send
            order.Add("platform:tracker");
        };
        bool? callbackOutsideLocks = null;
        world.PoseStateChanged += (_, _, _) => callbackOutsideLocks =
            !System.Threading.Monitor.IsEntered(world.FrameGate)
            && !System.Threading.Monitor.IsEntered(rig.Engine.Robot!.PoseTreeGate);
        rig.Robot.Sensors.ChargerEventInternal += on =>
        { Assert.True(on); Assert.False(rig.Robot.Sensors.OnChargerContacts); order.Add("contact:edge"); };
        ContactState(rig, true);
        Assert.Equal(new[] { "map:add", "map:add", "light:Known", "platform:tracker", "contact:edge" }, order);
        Assert.True(rig.Robot.Sensors.OnChargerContacts);
        Assert.True(callbackOutsideLocks);
        order.Clear();
        ContactState(rig, true);
        Assert.Equal(new[] { "map:cache" }, order); // unchanged Known has no state/light/edge action
    }

    // reach: production (existing located object fixture; received contact-on uses real world update/notifier).
    [Fact]
    public void ExistingObjectPreservesDistanceVisualTimeAndLocalizationWithKnownState()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        ContactState(rig, true); var obj = Charger(rig); var world = rig.Robot.Vision.World;
        obj.FromDistance = 75; obj.LastObservedTimestamp = 1234;
        obj.PoseState = PoseState.Dirty; obj.PoseConfirmationCount = 4;
        world.SetLocalizedTo(obj);
        var requests = new List<string>();
        rig.Robot.Lights.Cubes.PoseStatePlayObservedForTest = (_, trigger, _, _, _, _) => requests.Add(trigger);
        ContactState(rig, true);
        Assert.Same(obj, Charger(rig)); Assert.Equal(PoseState.Known, obj.PoseState);
        Assert.Equal(75, obj.FromDistance); Assert.Equal(1234u, obj.LastObservedTimestamp);
        Assert.Equal(1234u, obj.Confirmation!.LastPoseUpdatedTime);
        Assert.Equal(1, obj.PoseConfirmationCount);
        Assert.Equal(obj.ObjectId, world.LocalizedToObjectId);
        Assert.Equal(new[] { "Visible" }, requests);
    }

    // reach: production (connected-only world fixture, then actual received contact-on and native factory/ID owners).
    [Fact]
    public void ConnectedOnlyIdentityIsCopiedIntoANewLocatedInstance()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var world = rig.Robot.Vision.World;
        int id = world.AddConnectedActiveObject(2, 0x12345678, ObjectType.Charger_Basic);
        var connected = world.GetConnectedActiveObjectByIdHelper(unchecked((uint)id));
        ContactState(rig, true); var located = Charger(rig);
        Assert.NotSame(connected, located); Assert.Equal(unchecked((uint)id), located.ObjectId);
        Assert.Equal(2, located.ActiveId); Assert.Equal(0x12345678u, located.FactoryId);
    }

    // reach: production (old-origin located fixture cannot satisfy the current-origin ingress query).
    [Fact]
    public void OldOriginChargerIsNotReusedAsTheCurrentLocatedInstance()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced(); var world = rig.Robot.Vision.World;
        var old = new ObservableObject(800, ObjectType.Charger_Basic, ChargerGeometry.Markers) { OriginId = 22 };
        using var origin = new Pose3d("old") { Id = 22 };
        var pose = new Pose3d(Rotation3d.Identity, new Vec3(1000, 0, 0), "old charger", origin);
        old.InitPose(pose, PoseState.Known); world.AddLocatedObject(old);
        ContactState(rig, true);
        Assert.NotSame(old, Charger(rig)); Assert.Equal(22u, old.OriginId);
        Assert.Equal(1000d, old.Pose.Translation.X);
    }

    // reach: production (real VisionSystem platform hook on engine Tick; no synthetic distance cutoff).
    [Fact]
    public void ContactOffRetainsChargerAndPlatformUntilFootprintsSeparateThenSends400Once()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var events = new List<string>();
        rig.Robot.Sensors.OnChargerPlatformChanged += on => events.Add("platform:" + on);
        rig.Robot.Sensors.ChargerEvent += on => events.Add("contact:" + on);
        ContactState(rig, true); var obj = Charger(rig);
        ContactState(rig, false);
        Assert.True(rig.Robot.Sensors.OnChargerPlatform); Assert.False(rig.Robot.Sensors.OnChargerContacts);
        Assert.Same(obj, Charger(rig));
        Assert.Equal(new[] { "platform:True", "contact:True", "contact:False" }, events);
        int mark = rig.Mark();
        OwnedPose(rig, 1000, 0); rig.Tick();
        Assert.False(rig.Robot.Sensors.OnChargerPlatform); Assert.Same(obj, Charger(rig));
        Assert.Equal("platform:False", events[^1]);
        var thresholds = rig.RawSince(mark).Where(b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold).ToArray();
        Assert.Equal(new byte[] { (byte)RobotMessageId.SetCliffDetectThreshold, 0x90, 0x01 }, Assert.Single(thresholds));
        mark = rig.Mark(); rig.Tick();
        Assert.DoesNotContain(rig.RawSince(mark), b => b[0] == (byte)RobotMessageId.SetCliffDetectThreshold);
    }

    // reach: production (RobotState arrives before SyncTimeAck; no contact observation passes the ingress gate).
    [Fact]
    public void UnsynchronizedIngressAndRepeatedFalseHaveNoContactObservation()
    {
        using var rig = new M4ControlTests.Rig();
        rig.Engine.ConnectToRobot(M4ControlTests.Rig.RobotIp); rig.Tick();
        rig.Data(rig.MakeState(Contact)); rig.Tick();
        Assert.Empty(rig.Robot.Vision.World.LocatedObjects);
        rig.ToSynced();
        ContactState(rig, false); ContactState(rig, false);
        Assert.Empty(rig.Robot.Vision.World.LocatedObjects);
        Assert.False(rig.Robot.Sensors.OnChargerContacts); Assert.False(rig.Robot.Sensors.OnChargerPlatform);
    }
    // reach: unit (received contact-on, with a cached-element fixture inserted into the existing actionable cache;
    // full native cached-pose destructor ownership remains M12-001/M12-045's explicit MISSING).
    [Fact]
    public void ContactPoseWriteClearsTheExistingActionableCache()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced(); ContactState(rig, true);
        var obj = Charger(rig);
        var field = typeof(Cozmo.Robot.Manipulation.CubePreActionPoses).GetField("Caches",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var caches = (System.Runtime.CompilerServices.ConditionalWeakTable<ObservableObject,
            Dictionary<Cozmo.Robot.Manipulation.PreActionType, List<Cozmo.Robot.Manipulation.CubePreActionPoses.StoredElement>>>)field.GetValue(null)!;
        var values = new List<Cozmo.Robot.Manipulation.CubePreActionPoses.StoredElement>
        {
            new(ChargerGeometry.Markers[0], Pose3d.Identity, new Vec3(1, 0, 0), 0, 0)
        };
        caches.GetOrCreateValue(obj)[Cozmo.Robot.Manipulation.PreActionType.Docking] = values;
        ContactState(rig, true);
        Assert.Empty(values);
    }

    // reach: production (a located ProxObstacle fixture, then received contacts and real insertion/deletion owners).
    [Fact]
    public void InsertionDeletesIntersectingProxObstacleBeforePublishingTheCharger()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced(); var world = rig.Robot.Vision.World;
        var prox = new ObservableObject(700, ObjectType.ProxObstacle, Array.Empty<KnownMarker>())
            { OriginId = rig.Engine.Robot!.CurrentOriginId };
        var pose = new Pose3d(Rotation3d.Identity, new Vec3(30, 0, 0), "prox",
            rig.Engine.Robot.GetNativeOrigin(rig.Engine.Robot.CurrentOriginId));
        prox.InitPose(pose, PoseState.Known); world.AddLocatedObject(prox);
        rig.Robot.Lights.Cubes.PoseStatePlayObservedForTest = (_, _, _, _, _, _) =>
            Assert.Null(world.GetLocatedObjectById(700));
        ContactState(rig, true);
        Assert.Null(world.GetLocatedObjectById(700)); Assert.NotNull(Charger(rig));
    }

}
