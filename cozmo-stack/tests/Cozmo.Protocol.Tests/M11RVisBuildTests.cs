using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The tests of these classes read and write the engine's process-wide ObjectID counter and unique-type map (<c>ObjectIdSpace</c>), so nothing else
/// runs while they do.
/// </summary>
[CollectionDefinition("ObjectID process statics", DisableParallelization = true)]
public sealed class ObjectIdProcessStaticsCollection { }

/// <summary>
/// R-VIS build batch A for M11-vision and its fix round: the ObjectID counter and the observed-object creation (M11-013), the located-object walk (M11-004), the confirmer
/// (M11-007), AddConnectedActiveObject (M11-041), UpdateObjectOrigins (M11-043), PotentialObjectsForLocalizingTo (M11-044), the frame sequence pieces (M11-037) and the enable
/// gate and mailbox (M11-049, M11-040). Each test names its record and the citation it checks, and its expected values are the citation's (an address's behaviour, a literal, an
/// order). Exceptions, which assert a relation the test itself sets up rather than a cited number: the <c>ImageRig</c> scenes place cubes at poses of the test's choosing and assert
/// that an object is located after two sightings, its state, and that IDs agree; the M11_044 scenes set the robot-state hooks the way each test says. The two-sighting rule (a
/// first sighting only makes a confirmer entry, 0x00506A04) is what <c>ImageRig.Observe</c> and <c>VisionTests.WorldRig.Observe</c> give explicitly; the tests of other classes that
/// depend on it do so through <c>Rig.Frame()</c> (ManipRig.cs), whose class comment lists the files.
/// </summary>
[Collection("ObjectID process statics")]
public class M11RVisBuildTests : IDisposable
{
    public M11RVisBuildTests() => ObjectIdSpace.ResetForTests();
    public void Dispose() => ObjectIdSpace.ResetForTests();

    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;
    private static readonly CameraCalibration Cal = CameraCalibration.Nominal();

    /// <summary>
    /// The tests that render markers must not pass silently without the marker library (AssetPresenceTests): they fail with the reason, or are skipped only when the run says so with
    /// COZMO_TESTS_WITHOUT_ASSETS=1. Returns true when the test may go on.
    /// </summary>
    private static bool NeedsLibrary()
    {
        if (Lib is not null) return true;
        if (Environment.GetEnvironmentVariable("COZMO_TESTS_WITHOUT_ASSETS") == "1") return false;
        throw new Xunit.Sdk.XunitException("the marker library is missing (Cozmo.Robot was built without Vision/Data/marker_nn_library.bin), so this test cannot run; " +
                                           "provide it or set COZMO_TESTS_WITHOUT_ASSETS=1 for a run that knowingly has none");
    }

    private static Pose3d At(double x, double y, double z = 22, double yaw = 0) => new(Mat3.AboutZ(yaw), new Vec3(x, y, z));

    private static ObservableObject Cube(BlockWorld w, uint id, Pose3d pose, ObjectType type = ObjectType.Block_LIGHTCUBE1, PoseState state = PoseState.Known) =>
        new(id, type, CubeGeometry.MarkersFor(type)) { Pose = pose, PoseState = state, OriginId = w.CurrentOriginId };

    /// <summary>A located object with a confirmer entry, as one that has been through a confirming sighting is.</summary>
    private static ObservableObject Locate(BlockWorld w, ObservableObject o)
    {
        w.AddLocatedObject(o);
        w.ApplyRelativeObservation(o, o.Pose, o.PoseState, 1);
        return o;
    }

    private static VisionPoseData Pd(uint ts, double head = -0.15, double lift = 0, bool moving = false, bool rotating = false, bool cameraMoving = false) =>
        new(ts, new Pose3d(Mat3.Identity, new Vec3(0, 0, 0)), head, lift, moving, rotating, cameraMoving);

    private static CameraModel CameraOf(VisionPoseData pd) => new(Cal, pd.CameraPose);

    // ------------------------------------------------------------------------------------------- M11-013

    /// <summary>
    /// M11-013, <c>ObjectID::Set</c> 0x008412D8..0x008412E6 (reads c, stores c + 1, hands out c: the first ID ever assigned is 0), <c>SetID</c>
    /// 0x004EF468 (a non-unique type takes the next value, 0x004EF4EA; a unique type looks its type up in a static map and copies the stored value on a
    /// hit, 0x004EF524, else takes the next value and stores it, 0x004EF50A..0x004EF522) and <c>ResetObjectIDCounter</c> 0x008412C8 (stores 0; no caller).
    /// </summary>
    [Fact]
    public void M11_013_TheCounterStartsAtZeroAndAUniqueTypeKeepsItsFirstID()
    {
        Assert.Equal(0u, ObjectIdSpace.Set());
        Assert.Equal(1u, ObjectIdSpace.Set());
        var cube2 = new ObservableObject(ObjectType.Block_LIGHTCUBE2, Array.Empty<KnownMarker>());
        var cube1 = new ObservableObject(ObjectType.Block_LIGHTCUBE1, Array.Empty<KnownMarker>());
        var prox = new ObservableObject(ObjectType.ProxObstacle, Array.Empty<KnownMarker>());
        Assert.Equal(ObservableObject.UnassignedId, cube2.ObjectId);          // ObjectID -1 until SetID (0x0087660E)
        cube2.SetID();
        Assert.Equal(2u, cube2.ObjectId);                                     // the next counter value, stored for LIGHTCUBE2
        cube2.SetID();
        Assert.Equal(2u, cube2.ObjectId);                                     // a hit copies the stored value
        cube1.SetID();
        Assert.Equal(3u, cube1.ObjectId);
        prox.SetID();
        Assert.Equal(4u, prox.ObjectId);                                      // not unique: the next value, every call
        prox.SetID();
        Assert.Equal(5u, prox.ObjectId);
        ObjectIdSpace.ResetObjectIDCounter();                                 // stores 0 and touches nothing else
        cube2.SetID();
        Assert.Equal(2u, cube2.ObjectId);
        prox.SetID();
        Assert.Equal(0u, prox.ObjectId);
        Assert.True(new ObservableObject(ObjectType.Block_LIGHTCUBE3, Array.Empty<KnownMarker>()).IsUnique);               // vtable +0x5C: ActiveCube 1 (0x004E3882)
        Assert.True(new ObservableObject(ObjectType.Charger_Basic, Array.Empty<KnownMarker>()).IsUnique);                   // Charger 1 (0x004E3883)
        Assert.False(prox.IsUnique);                                                                                         // the base returns 0 (0x004E02AE)
    }

    /// <summary>
    /// M11-013: the record's removal list. The engine has no AllowUnconnectedObjects switch (the string occurs 0 times in the .so, verified in
    /// 20260929-R-VIS-verify-pre-items1-3.md), and the type-derived LIGHTCUBE1..3 => 1..3 mapping has no source.
    /// </summary>
    [Fact]
    public void M11_013_ThereIsNoAllowUnconnectedObjectsSwitch()
    {
        Assert.Null(typeof(BlockWorld).GetProperty("AllowUnconnectedObjects"));
        Assert.Null(typeof(BlockWorld).GetField("AllowUnconnectedObjects"));
    }

    /// <summary>M11-013 (row 1.6): the creation clamp is <c>Radians(vtable[+0x1C]() * 0.0174533)</c> and that virtual returns 5.0 (0x004E024E..0x004E0250).</summary>
    [Fact]
    public void M11_013_TheCreationClampIsFiveDegrees()
    {
        Assert.Equal(5.0 * 0.0174533, BlockWorld.CreationFlatClampAngleRad, 9);
    }

    /// <summary>
    /// The same cubes seen unconnected. The IDs are the counter's, once per unique type, in the order the types are first given one (M11-013,
    /// 0x004EF468..0x004EF526): the first is 0, not the cube number; a second sighting of the type, a deletion and a new sighting, and a new BlockWorld
    /// (the counter and map are process statics) all give the same value.
    /// </summary>
    [Fact]
    public void M11_013_AnObservedUnconnectedCubeGetsTheCountersIdOncePerType()
    {
        if (!NeedsLibrary()) return;
        using var rig = new ImageRig();
        var r = rig.Observe((ObjectType.Block_LIGHTCUBE2, At(150, 0)));
        var two = Assert.Single(r.Objects).Object;
        Assert.Equal(0u, two.ObjectId);                                              // the first ID ever assigned, for LIGHTCUBE2, not 2 or 1
        var r3 = rig.Observe((ObjectType.Block_LIGHTCUBE2, At(150, 0)), (ObjectType.Block_LIGHTCUBE1, At(220, -50)));
        Assert.Equal(0u, r3.Objects.Single(o => o.Object.Type == ObjectType.Block_LIGHTCUBE2).Object.ObjectId);
        Assert.Equal(1u, r3.Objects.Single(o => o.Object.Type == ObjectType.Block_LIGHTCUBE1).Object.ObjectId);   // the next value, given the first time LIGHTCUBE1 is seen
        // deleted and seen again: the same value (the map is kept)
        rig.Vision.World.ResetToConstructed();
        var again = rig.Observe((ObjectType.Block_LIGHTCUBE2, At(150, 0)));
        Assert.Equal(0u, Assert.Single(again.Objects).Object.ObjectId);
    }

    /// <summary>
    /// M11-013 (rows 1.16, 1.20; H1.1..H1.6): an active instance whose connected counterpart is missing warns at most once per 10.0 s and the observation is
    /// KEPT: the located object is created by <c>AddLocatedObject</c> with ActiveID -1 and FactoryID 0 (0x00622ADE..0x00622E4E). The warning cooldown is
    /// the static <c>unordered_map&lt;int,float&gt;</c> at 0x0105B388: the entry is set to now + 10.0 (0x00620ED6..0x00620EDA, literal 0x00620B1E), where now is
    /// <c>BaseStationTimer::GetCurrentTimeInSeconds()</c> (0x00620E42..0x00620E5E), not the frame's timestamp: the test moves the wall clock and not the robot's.
    /// </summary>
    [Fact]
    public void M11_013_AnUnconnectedCubeIsKeptAndWarnsOncePerTenSeconds()
    {
        if (!NeedsLibrary()) return;
        using var rig = new ImageRig();
        double now = 100.0;
        rig.Vision.World.BaseStationSeconds = () => now;
        var first = rig.Observe((ObjectType.Block_LIGHTCUBE1, At(150, 0)));
        var o = Assert.Single(first.Objects).Object;
        Assert.Equal(0u, o.ObjectId);
        Assert.True(o.IsLocated);
        Assert.Equal(-1, o.ActiveId);                                                // no connected object: ActiveID stays -1
        Assert.Equal(0u, o.FactoryId);                                               // and FactoryID 0
        int warnings = rig.Log.Count(l => l.Contains("not connected"));
        Assert.Equal(1, warnings);                                                   // the confirming sighting warned
        now = 109.9;
        rig.Frame((ObjectType.Block_LIGHTCUBE1, At(150, 0)));                        // inside the 10 s cooldown
        Assert.Equal(warnings, rig.Log.Count(l => l.Contains("not connected")));
        rig.T += 11_000;                                                             // the robot's clock moving changes nothing
        rig.Frame((ObjectType.Block_LIGHTCUBE1, At(150, 0)));
        Assert.Equal(warnings, rig.Log.Count(l => l.Contains("not connected")));
        now = 110.1;                                                                 // now + 10.0 has passed on the wall clock
        rig.Frame((ObjectType.Block_LIGHTCUBE1, At(150, 0)));
        Assert.Equal(warnings + 1, rig.Log.Count(l => l.Contains("not connected")));
        Assert.Single(rig.Vision.World.LocatedObjects);
    }

    /// <summary>
    /// M11-013 with M11-041: a connected cube's observation has its counterpart, so there is no warning; the located object takes the connected object's
    /// ActiveID and FactoryID (<c>AddLocatedObject</c> 0x00622AE4..0x00622B9C) and the ObjectID the connected object was given at connection (the same per-type
    /// SetID value).
    /// </summary>
    [Fact]
    public void M11_013_AConnectedCubeIsObservedWithItsConnectedObjectsId()
    {
        if (!NeedsLibrary()) return;
        using var rig = new ImageRig();
        rig.Send(new ObjectConnectionState { ObjectID = 2, FactoryID = 0xBEEF, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true });
        uint connectedId = rig.Vision.World.ConnectedObjectIdForActiveId(2)!.Value;
        Assert.Equal(0u, connectedId);                                               // the first SetID of the process
        var r = rig.Observe((ObjectType.Block_LIGHTCUBE1, At(150, 0)));
        var o = Assert.Single(r.Objects).Object;
        Assert.Equal(connectedId, o.ObjectId);
        Assert.Equal(2, o.ActiveId);
        Assert.Equal(0xBEEFu, o.FactoryId);
        Assert.DoesNotContain(rig.Log, l => l.Contains("not connected"));
    }

    /// <summary>
    /// M11-044 (gap3-insert X1, P2/P3, P7): a confirmed object's pose is refreshed by <c>Insert</c> -> <c>UseDiscardedObservation</c> -> <c>AddVisualObservation</c> ->
    /// <c>UpdatePoseInInstance</c>. An unconnected cube is not usable for localization (activeID -1, <c>ActiveObject::CanBeUsedForLocalization</c> 0x004E4A06..0x004E4A28), so
    /// <c>CouldUseObjectForLocalization</c> is false, <c>Insert</c> discards the pair (0x0050CE88..0x0050CE8E) and, the matched object being in the world origin, calls
    /// <c>UseDiscardedObservation(pair, WasCameraMoving)</c> (0x0050CFE2); the flag is 0 because the confirmed shortcut skipped <c>AddVisualObservation</c> (0x00620C5C). One
    /// sighting 25 mm and 10 mm away, inside the 35.2 mm tolerance, therefore moves the located pose.
    /// </summary>
    [Fact]
    public void M11_044_AConfirmedUnconnectedObjectIsRefreshedByTheDiscardPath()
    {
        if (!NeedsLibrary()) return;
        using var rig = new ImageRig();
        var o = Assert.Single(rig.Observe((ObjectType.Block_LIGHTCUBE1, At(150, 0))).Objects).Object;
        Assert.False(rig.Vision.World.CanBeUsedForLocalization(o));                     // activeID -1
        var before = o.Pose.Translation;
        rig.Frame((ObjectType.Block_LIGHTCUBE1, At(175, 10)));
        Assert.True((o.Pose.Translation - new Vec3(175, 10, 22)).Length < 6, $"{before} -> {o.Pose.Translation}");
        Assert.Equal(PoseState.Known, o.PoseState);
    }

    /// <summary>
    /// M11-044 (P0, P6, P10, X1, X3; 0x00512458, 0x00512468): a connected cube (activeID >= 0, Known, fromDistance >= 0, resting flat, not moving) is usable. While the robot is not localized
    /// the gate is 1: the confirming sighting passes the discard gate, the motion gates and P5 (the observed and matched poses are the same), and P6 stores the pair; <c>LocalizeRobot</c>
    /// hands it to <c>LocalizeToObject</c>, whose only effect here is the two <c>SetLocalizedTo</c> writes (ID to Robot+0x2B8, 0 to +0x2BC). From the next frame the gate is 0, so every
    /// sighting is discarded into <c>UseDiscardedObservation</c> and refreshes the pose (X1), and <c>LocalizeToObject</c> is not run again.
    /// </summary>
    [Fact]
    public void M11_044_AStationaryRobotLocalizesToAUsableCubeOnceAndThenRefreshesItEveryFrame()
    {
        if (!NeedsLibrary()) return;
        using var rig = new ImageRig();
        rig.Send(new ObjectConnectionState { ObjectID = 2, FactoryID = 0xBEEF, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true });
        Assert.Null(rig.Vision.World.LocalizedToObjectId);
        var o = Assert.Single(rig.Observe((ObjectType.Block_LIGHTCUBE1, At(150, 0))).Objects).Object;
        Assert.True(rig.Vision.World.CanBeUsedForLocalization(o));
        Assert.Equal(o.ObjectId, rig.Vision.World.LocalizedToObjectId);
        Assert.False(rig.Vision.World.RobotMovedSinceLocalized);
        Assert.Equal(1, rig.Vision.World.UnbuiltLocalizeToObjectCalls);
        rig.Frame((ObjectType.Block_LIGHTCUBE1, At(175, 10)));                          // gate 0: discarded into UseDiscarded, refreshed
        Assert.True((o.Pose.Translation - new Vec3(175, 10, 22)).Length < 6, $"{o.Pose.Translation}");
        Assert.Equal(1, rig.Vision.World.UnbuiltLocalizeToObjectCalls);
    }

    /// <summary>
    /// M11-044 P0 / 0x00512B56..0x00512B8E, 0x00512458, 0x00512468, 0x005124F6: the localization fields. <c>SetLocalizedTo(obj)</c> stores the ID and 0; null stores -1 and leaves +0x2BC;
    /// each robot state ORs 1 in when <c>MovementComponent</c> +0xA or +0xC is set, and otherwise whether the robot is not on its treads; the gate follows them.
    /// </summary>
    [Fact]
    public void M11_044_TheLocalizationFieldsAreWrittenBySetLocalizedToAndEachRobotState()
    {
        var w = new BlockWorld();
        var o = Cube(w, 42, At(150, 0));
        Assert.Null(w.LocalizedToObjectId);
        Assert.False(w.RobotMovedSinceLocalized);                                     // 0 from construction (0x0050FF0C)
        w.NoteRobotState(false, false); Assert.False(w.RobotMovedSinceLocalized);     // nothing ORed in
        Assert.True(w.SetLocalizedTo(o));
        Assert.Equal(42u, w.LocalizedToObjectId);
        Assert.False(new PotentialObjectsForLocalizingTo(w).Gate);                    // localized, not moved since: 0
        w.NoteRobotState(true, false);                                                // a movement byte
        Assert.True(w.RobotMovedSinceLocalized);
        Assert.True(new PotentialObjectsForLocalizingTo(w).Gate);
        Assert.True(w.SetLocalizedTo(o));                                             // localizing again writes 0
        Assert.False(w.RobotMovedSinceLocalized);
        w.NoteRobotState(false, true);                                                // not on its treads
        Assert.True(w.RobotMovedSinceLocalized);
        w.SetLocalizedTo(o); w.NoteRobotState(false, false);
        Assert.False(w.RobotMovedSinceLocalized);                                     // an ordinary state leaves it
        w.NoteRobotState(true, false);                                                // set the flag first, so that "untouched" can be told from "cleared"
        Assert.True(w.RobotMovedSinceLocalized);
        Assert.True(w.SetLocalizedTo(null));
        Assert.Null(w.LocalizedToObjectId);
        Assert.True(w.RobotMovedSinceLocalized);                                      // null does not write +0x2BC (0x005124D0..0x005124FA)
        Assert.True(new PotentialObjectsForLocalizingTo(w).Gate);                     // not localized: 1
        var unassigned = new ObservableObject(ObjectType.Block_LIGHTCUBE1, Array.Empty<KnownMarker>());
        Assert.False(w.SetLocalizedTo(unassigned));                                   // IdNotSet
        Assert.Null(w.LocalizedToObjectId);
    }

    /// <summary>
    /// M11-044 steady state (gap3-insert section 3 X1..X4, P6, P10): a cube seen on every frame by a stationary robot is located, refreshed, never marked unobserved or deleted, and no
    /// observation is dropped, using only the citations' rules. After the first localization the gate is 0 and every sighting goes through UseDiscarded, which writes the confirmer's +0x30
    /// (0x00506A9C), so the candidate test (<c>GetLastVisuallyMatchedTime &lt; ts</c>) excludes it. FINDING: on the frame where P6 stores the pair (gate still 1, before the first
    /// localization) the confirmer's +0x30 is written by the promoting <c>AddVisualObservation</c> in the same frame, so it is not stale then either.
    /// </summary>
    [Fact]
    public void M11_044_ACubeSeenEveryFrameByAStationaryRobotIsNeverMarkedUnobservedOrDropped()
    {
        if (!NeedsLibrary()) return;
        foreach (bool connected in new[] { true, false })
        {
            ObjectIdSpace.ResetForTests();
            using var rig = new ImageRig();
            if (connected) rig.Send(new ObjectConnectionState { ObjectID = 2, FactoryID = 0xBEEF, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true });
            int observedEvents = 0;
            rig.Vision.World.ObjectObserved += _ => observedEvents++;
            rig.Frame((ObjectType.Block_LIGHTCUBE1, At(150, 0)));                       // the first sighting: only an entry
            Assert.Empty(rig.Vision.World.LocatedObjects);
            for (int i = 0; i < 12; i++)
            {
                var r = rig.Frame((ObjectType.Block_LIGHTCUBE1, At(150, 0)));
                var o = Assert.Single(r.Objects).Object;                                 // not dropped
                Assert.Empty(r.Forgotten);
                Assert.Equal(PoseState.Known, o.PoseState);
                Assert.Equal(0, o.UnobservedCount);
                Assert.True(o.PoseConfirmationCount >= 2);
            }
            Assert.Equal(12, observedEvents);
            Assert.DoesNotContain(rig.Log, l => l.Contains("unobserved"));
            Assert.Single(rig.Vision.World.LocatedObjects);
        }
    }

    /// <summary>
    /// M11-044 (P6..P7 via the ImageRig): the state hooks the engine reads are wired by the vision system: <c>Robot::GetPose</c> is the CURRENT pose (0x00620BE2), so a
    /// robot 350 mm from the cube by its current pose gives dist 350 (> 250.00001), and the confirming sighting leaves the object Dirty (<c>closeAndSteady</c> false), while the same
    /// scene with the current pose beside the cube gives Known.
    /// </summary>
    [Fact]
    public void M11_044_TheDistanceIsFromTheRobotsCurrentPose()
    {
        if (!NeedsLibrary()) return;
        using var near = new ImageRig();
        Assert.Equal(PoseState.Known, Assert.Single(near.Observe((ObjectType.Block_LIGHTCUBE1, At(150, 0))).Objects).Object.PoseState);
        ObjectIdSpace.ResetForTests();
        using var far = new ImageRig();
        far.Vision.World.CurrentRobotPose = () => new Pose3d(Mat3.Identity, new Vec3(-200, 0, 0));
        Assert.Equal(PoseState.Dirty, Assert.Single(far.Observe((ObjectType.Block_LIGHTCUBE1, At(150, 0))).Objects).Object.PoseState);
    }

    /// <summary>
    /// M11-044 P0 (0x00512B56..0x00512B8E): the gate <c>this+0x10</c> is <c>([Robot+0x2B8] != -1) ? byte [Robot+0x2BC] : 1</c>. Not localized: 1. Localized and not moved since: 0 (the
    /// Robot constructor and <c>SetLocalizedTo</c> store 0 in +0x2BC). Localized and moved: 1.
    /// </summary>
    [Fact]
    public void M11_044_TheGateIsOneUnlessTheRobotIsLocalizedAndHasNotMoved()
    {
        var w = new BlockWorld();
        var o = Cube(w, 7, At(150, 0));
        Assert.True(new PotentialObjectsForLocalizingTo(w).Gate);
        w.SetLocalizedTo(o);
        Assert.False(new PotentialObjectsForLocalizingTo(w).Gate);
        w.NoteRobotState(true, false);
        Assert.True(new PotentialObjectsForLocalizingTo(w).Gate);
        w.SetLocalizedTo(null);
        Assert.True(new PotentialObjectsForLocalizingTo(w).Gate);
    }

    private static (BlockWorld W, ObservableObject Matched, PotentialObjectsForLocalizingTo P) UsableRig(uint id = 1, ObjectType type = ObjectType.Block_LIGHTCUBE1)
    {
        var w = new BlockWorld();
        var m = Locate(w, Cube(w, id, At(150, 0), type));
        m.ActiveId = 1; m.FromDistance = 100;
        return (w, m, new PotentialObjectsForLocalizingTo(w));
    }

    private static ObservableObject Seen(ObservableObject like, Pose3d pose, uint ts = 2000) =>
        new(like.ObjectId, like.Type, like.Markers) { Pose = pose, PoseState = PoseState.Dirty, LastObservedTimestamp = ts };

    /// <summary>
    /// M11-044 P2 (0x0050CE64..0x0050CE8E), P3 (0x0050CFC4..0x0050CFE6), P7: Insert discards when <c>(250.0f + 1e-5f) &lt; dist</c> (a NaN does not), when this+0x10 is 0, or when the matched
    /// object cannot be used; a discarded pair whose matched object is in the world origin goes to <c>UseDiscardedObservation(pair, wasCameraMoving)</c>, which calls
    /// <c>AddVisualObservation</c> unless the pair's flag is set; Insert returns 0 in every one of these paths. A matched object in another origin gets nothing.
    /// </summary>
    [Theory]
    [InlineData(250.0f, false, true, true, false)]          // at the limit: not discarded (250.0f + 1e-5f is 250.0000153f)
    [InlineData(250.0000153f, false, true, true, false)]
    [InlineData(250.1f, false, true, true, true)]           // over
    [InlineData(float.NaN, false, true, true, false)]       // a NaN does not discard
    [InlineData(100.0f, true, true, true, false)]           // flag set: the discard path calls nothing (and here there is no discard)
    public void M11_044_InsertDiscardsOverTheDistanceGate(float dist, bool flag, bool usable, bool gate, bool discarded)
    {
        var (w, m, p) = UsableRig();
        if (!usable) m.ActiveId = -1;
        var entry = w.EntryFor(m.ObjectId)!;
        int countBefore = entry.Count;
        int r = p.Insert(Seen(m, At(150, 0)), m, dist, flag, wasCameraMoving: false);
        if (discarded) { Assert.Equal(0, r); Assert.Equal(countBefore + 1, entry.Count); }     // UseDiscardedObservation reached AddVisualObservation
        else Assert.Equal(1, r);                                                                // stored
    }

    [Fact]
    public void M11_044_InsertDiscardsWhenTheGateIsClearOrTheObjectIsNotUsableAndFlagAndOriginDecideTheAction()
    {
        // this+0x10 == 0
        var (w, m, _) = UsableRig();
        w.SetLocalizedTo(m);                                                                     // localized, not moved since: the gate is 0
        var p = new PotentialObjectsForLocalizingTo(w);
        var entry = w.EntryFor(m.ObjectId)!;
        Assert.Equal(0, p.Insert(Seen(m, At(150, 0)), m, 100f, false, false));
        Assert.Equal(2, entry.Count);                                                             // discarded into AddVisualObservation
        // the flag is set: UseDiscardedObservation does nothing (0x0050CDE0)
        Assert.Equal(0, p.Insert(Seen(m, At(150, 0)), m, 100f, true, false));
        Assert.Equal(2, entry.Count);
        // CouldUse false (a Dirty cube is not usable) with the gate open
        var (w2, m2, p2) = UsableRig();
        m2.PoseState = PoseState.Dirty;
        var e2 = w2.EntryFor(m2.ObjectId)!;
        Assert.Equal(0, p2.Insert(Seen(m2, At(150, 0)), m2, 100f, false, false));
        Assert.Equal(2, e2.Count);
        // the matched object is in another origin: nothing at all
        var (w3, m3, p3) = UsableRig();
        m3.OriginId = 5;
        var e3 = w3.EntryFor(m3.ObjectId)!;
        Assert.Equal(0, p3.Insert(Seen(m3, At(150, 0)), m3, 300f, false, false));
        Assert.Equal(1, e3.Count);
        // the discard path runs while the robot is moving: the motion gates come after it (P3)
        var (w4, m4, p4) = UsableRig();
        w4.MovementBytes = () => (true, true);
        var e4 = w4.EntryFor(m4.ObjectId)!;
        Assert.Equal(0, p4.Insert(Seen(m4, At(150, 0)), m4, 300f, false, true));
        Assert.Equal(2, e4.Count);
    }

    /// <summary>
    /// M11-044 P8 (0x0050D170..0x0050D1C6): <c>CouldUseObjectForLocalization</c> is <c>(id != [DockingComponent+0xC]) &amp;&amp; CanBeUsedForLocalization &amp;&amp; (id != [MovementComponent+0x20])
    /// &amp;&amp; !WasObjectTappedRecently(id)</c>; <c>ActiveObject::CanBeUsedForLocalization</c> 0x004E49AC needs Known, activeID >= 0, fromDistance >= 0 and <c>IsRestingFlat(5 degrees)</c>.
    /// </summary>
    [Fact]
    public void M11_044_CouldUseObjectForLocalizationAndCanBeUsedForLocalization()
    {
        var (w, m, _) = UsableRig();
        Assert.True(w.CanBeUsedForLocalization(m));
        Assert.True(w.CouldUseObjectForLocalization(m));
        m.PoseState = PoseState.Dirty; Assert.False(w.CanBeUsedForLocalization(m)); m.PoseState = PoseState.Known;
        m.ActiveId = -1; Assert.False(w.CanBeUsedForLocalization(m)); m.ActiveId = 0; Assert.True(w.CanBeUsedForLocalization(m));   // 0 is >= 0
        m.FromDistance = -1; Assert.False(w.CanBeUsedForLocalization(m)); m.FromDistance = 0; Assert.True(w.CanBeUsedForLocalization(m));
        m.Pose = new Pose3d(Mat3.AxisAngle(new Vec3(1, 0, 0), 10 * Math.PI / 180), m.Pose.Translation);
        Assert.False(w.CanBeUsedForLocalization(m));                                                  // 10 degrees is not within 5
        m.Pose = new Pose3d(Mat3.AxisAngle(new Vec3(1, 0, 0), 3 * Math.PI / 180), m.Pose.Translation);
        Assert.True(w.CanBeUsedForLocalization(m));
        w.DockingObjectId = () => m.ObjectId; Assert.False(w.CouldUseObjectForLocalization(m)); w.DockingObjectId = null;
        w.MovementComponentObjectId = () => m.ObjectId; Assert.False(w.CouldUseObjectForLocalization(m)); w.MovementComponentObjectId = null;
        w.WasObjectTappedRecently = id => id == m.ObjectId; Assert.False(w.CouldUseObjectForLocalization(m)); w.WasObjectTappedRecently = null;
        Assert.True(w.CouldUseObjectForLocalization(m));
        // the Charger's slot (0x0101E1DC -> 0x004EA66E: movs r0,#0; bx lr) is false, and so is every other non-cube type (the ObservableObject default 0x004E024A)
        var charger = new ObservableObject(9, ObjectType.Charger_Basic, ChargerGeometry.Markers) { Pose = At(300, 0, 0), PoseState = PoseState.Known, OriginId = 1, ActiveId = 0, FromDistance = 10 };
        Assert.False(w.CanBeUsedForLocalization(charger));
        Assert.False(w.CanBeUsedForLocalization(new ObservableObject(ObjectType.ProxObstacle, Array.Empty<KnownMarker>()) { PoseState = PoseState.Known, ActiveId = 0, FromDistance = 10 }));
        // vptr+8 = IsMoving(uint*) (0x004E3844) with (this, 0) after the Known test: a moving cube warns and is not usable (0x004E49C2..0x004E49DE)
        var log = new List<string>(); w.Log += log.Add;
        m.IsMoving = true;
        Assert.False(w.CanBeUsedForLocalization(m));
        Assert.False(w.CouldUseObjectForLocalization(m));
        Assert.Contains(log, l => l.Contains("is moving"));
        m.IsMoving = false;
        Assert.True(w.CanBeUsedForLocalization(m));
    }

    /// <summary>
    /// M11-044 / M11-009 (0x004E49C2..0x004E49DE, 0x0050CE88..0x0050CFE2, 0x00505E36): a Known cube the radio reports as moving is not usable for localization, so <c>Insert</c> discards its
    /// pair even with the gate open and the observation is refreshed through <c>UseDiscardedObservation</c>; once the cube has stopped it is usable again and the same sighting is stored.
    /// </summary>
    [Fact]
    public void M11_044_AMovingKnownCubeIsDiscardedAndRefreshed()
    {
        var (w, m, p) = UsableRig();
        w.SetMoving(m.ObjectId, true);
        var entry = w.EntryFor(m.ObjectId)!;
        Assert.Equal(0, p.Insert(Seen(m, At(150, 0)), m, 100f, false, false));
        Assert.Equal(2, entry.Count);                                                 // refreshed through AddVisualObservation
        Assert.Equal(PoseState.Dirty, m.PoseState);                                   // UpdatePoseInInstance: not closeAndSteady while the cube reports movement
        m.PoseState = PoseState.Known;                                                // (the pose is Known again)
        w.SetMoving(m.ObjectId, false);
        Assert.Equal(1, p.Insert(Seen(m, At(150, 0)), m, 100f, false, false));       // usable: stored
        Assert.Equal(2, entry.Count);
    }

    private static RobotState Raw(uint ts, uint origin = 1, float x = 0, float y = 0) =>
        new() { Timestamp = ts, PoseOriginId = origin, Pose = new RobotPose { X = x, Y = y, Angle = 0 }, Accel = new AccelData { Z = 9800 }, Gyro = new GyroData() };

    private static RobotStateHistory HistoryOf(params uint[] rawTimestamps)
    {
        var h = new RobotStateHistory();
        foreach (var t in rawTimestamps) h.Add(Raw(t));
        return h;
    }

    /// <summary>
    /// M11-044 (0x00531CCC, 0x00531CDC..0x00531D06): <c>GetComputedStateAt</c> is a <c>lower_bound</c> over the computed-state map and fails unless a computed state exists with EXACTLY the
    /// requested timestamp; raw states and nearby timestamps do not count. <c>ComputeAndInsertStateAt</c> (0x00654D92, key out = t at 0x00531558) is what puts one in, from the raw state at t.
    /// </summary>
    [Fact]
    public void M11_044_GetComputedStateAtNeedsAStateAtExactlyThatTimestamp()
    {
        var h = new RobotStateHistory();
        h.Add(Raw(500, x: 10, y: 20)); h.Add(Raw(501));
        Assert.Null(h.GetComputedStateAt(500));                                       // a raw state is not a computed one
        Assert.True(h.ComputeAndInsertStateAt(500));
        Assert.Equal(10, h.GetComputedStateAt(500)!.Value.Translation.X, 3);
        Assert.Equal(20, h.GetComputedStateAt(500)!.Value.Translation.Y, 3);
        Assert.Null(h.GetComputedStateAt(499));
        Assert.Null(h.GetComputedStateAt(501));
        h.Clear();
        Assert.Null(h.GetComputedStateAt(500));
    }

    /// <summary>
    /// M11-044 (0x00531431, 0x0053146A..0x00531472, 0x005314D0..0x00531558): <c>GetRawStateAt(t, ..., true)</c> returns 1 when no raw state at or after t exists, so nothing is inserted;
    /// with a raw state on each side of t the state is interpolated and inserted at the key t (not at either raw key). The blend itself is MISSING; the stand-in is counted.
    /// </summary>
    [Fact]
    public void M11_044_ComputeAndInsertFailsWithoutARawStateAtOrAfterTAndInsertsAtKeyTBetweenTwo()
    {
        var h = HistoryOf(1000, 1100);
        Assert.False(h.ComputeAndInsertStateAt(1101));                                // nothing at or after
        Assert.Null(h.GetComputedStateAt(1101));
        Assert.True(h.ComputeAndInsertStateAt(1030));
        Assert.NotNull(h.GetComputedStateAt(1030));                                   // key out = t
        Assert.Null(h.GetComputedStateAt(1000));
        Assert.Null(h.GetComputedStateAt(1100));
        Assert.Equal(1, h.InterpolationStandIns);                                     // the blend is not built: visible
        Assert.True(h.ComputeAndInsertStateAt(1100));                                 // a raw key needs no blend
        Assert.Equal(1, h.InterpolationStandIns);
    }

    /// <summary>
    /// M11-044 (CullToWindowSize 0x005309D1, cutoff = newest RAW key - 3000 at 0x00530A02..0x00530A0E with the 0xBB8 default at 0x005308BE; lower_bound then erase, 0x00530A5E..0x00530BBE):
    /// a computed state 2 to 3 s older than the newest raw state survives, keys below the cutoff go and the cutoff key itself stays; inserting a later computed state prunes nothing (no
    /// prune in ComputeAndInsertStateAt); a change of origin does not clear the computed map (AddRawOdomState 0x00530CF8).
    /// </summary>
    [Fact]
    public void M11_044_TheComputedMapIsCulledToThreeSecondsOfTheNewestRawKeyAndNothingElseClearsIt()
    {
        var h = HistoryOf(999, 1000, 2600);
        Assert.True(h.ComputeAndInsertStateAt(999)); Assert.True(h.ComputeAndInsertStateAt(1000)); Assert.True(h.ComputeAndInsertStateAt(2600));
        Assert.NotNull(h.GetComputedStateAt(999));                                    // 1.6 s older than 2600 and 2600 - 999 < 3000: no 2000 ms prune
        h.Add(Raw(4000));                                                             // cutoff 1000
        Assert.Null(h.GetComputedStateAt(999));
        Assert.NotNull(h.GetComputedStateAt(1000));                                   // the cutoff key stays
        Assert.NotNull(h.GetComputedStateAt(2600));                                   // 2 to 3 s old entries survive
        h.Add(Raw(4033, origin: 2));                                                  // a new origin
        Assert.NotNull(h.GetComputedStateAt(2600));                                   // the computed map is not cleared
        // the raw window is the same 3000 ms (the raw map is culled by the same call)
        var r = HistoryOf(1000, 4000);
        Assert.True(r.ComputeAndInsertStateAt(1000));                                 // still there: 4000 - 1000 = 3000 is not over the window
        r.Add(Raw(4001));
        Assert.Null(r.GetComputedStateAt(1000));                                      // cutoff 1001: erased
    }

    /// <summary>
    /// M11-044 P5 with the exact-timestamp rule: without computed states at the matched object's last pose update and at ts, <c>GetComputedStateAt</c> fails (0x00531D06) and the pair goes to P6
    /// even though the observed pose moved; with both present (and the same robot pose) the moved observation is refreshed and Insert returns 0. A last pose update 2.5 s before ts is still
    /// found, the computed map keeping 3000 ms (0x005308BE), so P5 (not P6) decides.
    /// </summary>
    [Fact]
    public void M11_044_TheStationaryTestNeedsComputedStatesAtBothTimes()
    {
        var (w, m, p) = UsableRig();
        w.EntryFor(m.ObjectId)!.LastPoseUpdatedTime = 500;
        var h = HistoryOf(500, 2000);
        w.ComputedRobotPoseAt = h.GetComputedStateAt;
        Assert.True(h.ComputeAndInsertStateAt(2000));                                 // ts only: the lastPoseUpdated lookup fails
        Assert.Equal(1, p.Insert(Seen(m, At(160, 0)), m, 100f, false, false));
        Assert.Equal(1, w.EntryFor(m.ObjectId)!.Count);
        var (w2, m2, p2) = UsableRig();
        w2.EntryFor(m2.ObjectId)!.LastPoseUpdatedTime = 500;
        var h2 = HistoryOf(500, 2000);
        w2.ComputedRobotPoseAt = h2.GetComputedStateAt;
        Assert.True(h2.ComputeAndInsertStateAt(500)); Assert.True(h2.ComputeAndInsertStateAt(2000));
        Assert.Equal(0, p2.Insert(Seen(m2, At(160, 0)), m2, 100f, false, false));
        Assert.Equal(new Vec3(160, 0, 22), m2.Pose.Translation);
        var (w3, m3, p3) = UsableRig();                                              // a nearby (not equal) timestamp does not do
        w3.EntryFor(m3.ObjectId)!.LastPoseUpdatedTime = 500;
        var h3 = HistoryOf(499, 500, 2000);
        w3.ComputedRobotPoseAt = h3.GetComputedStateAt;
        Assert.True(h3.ComputeAndInsertStateAt(499)); Assert.True(h3.ComputeAndInsertStateAt(2000));
        Assert.Equal(1, p3.Insert(Seen(m3, At(160, 0)), m3, 100f, false, false));
        // 2.5 s old lastPoseUpdated (0x005309D1: the cutoff is 3000 ms): both states are still there
        var (w4, m4, p4) = UsableRig();
        w4.EntryFor(m4.ObjectId)!.LastPoseUpdatedTime = 500;
        var h4 = HistoryOf(500, 3000);
        w4.ComputedRobotPoseAt = h4.GetComputedStateAt;
        Assert.True(h4.ComputeAndInsertStateAt(500));
        var seen4 = Seen(m4, At(160, 0), ts: 3000);
        Assert.True(h4.ComputeAndInsertStateAt(3000));
        Assert.Equal(0, p4.Insert(seen4, m4, 100f, false, false));                   // P5 refreshed it: not P6's store
    }

    /// <summary>
    /// M11-044 P4 (0x0050CE92..0x0050CEB0): with the pair past the discard gate, Insert returns 0 without storing or discarding when MovementComponent +0xA (head not in position) or +0xC
    /// (wheels moving) is set or the camera was moving.
    /// </summary>
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void M11_044_TheMotionGatesReturnZeroWithoutStoringOrRefreshing(bool headNotInPos, bool wheels, bool cameraMoving)
    {
        var (w, m, p) = UsableRig();
        w.MovementBytes = () => (headNotInPos, wheels);
        var entry = w.EntryFor(m.ObjectId)!;
        Assert.Equal(0, p.Insert(Seen(m, At(160, 0)), m, 100f, false, cameraMoving));
        Assert.Equal(1, entry.Count);
        Assert.Empty(p.Pairs);
    }

    /// <summary>
    /// M11-044 P5 (0x0050CEB4..0x0050CF86, 0x0050D122): with both robot poses obtainable and the same within (1,1,1) mm and 1 degree, an observed pose that differs from the matched object's
    /// (by more than the same tolerances) is refreshed through <c>UseDiscardedObservation(pair, false)</c> and Insert returns 0; identical observed and matched poses fall through to the
    /// per-root store; a robot pose that changed, or a failing <c>GetComputedStateAt</c>, does too. <c>GetLastPoseUpdatedTime</c> is the entry's +0x2C (0 without an entry).
    /// </summary>
    [Fact]
    public void M11_044_TheStationaryRobotTestRefreshesAMovedObservation()
    {
        var (w, m, p) = UsableRig();
        var entry = w.EntryFor(m.ObjectId)!;
        entry.LastPoseUpdatedTime = 500;
        var asked = new List<uint>();
        w.ComputedRobotPoseAt = ts => { asked.Add(ts); return Pose3d.Identity; };
        Assert.Equal(0, p.Insert(Seen(m, At(155, 0)), m, 100f, false, false));
        Assert.Equal(new uint[] { 500, 2000 }, asked);                                // the last pose update, then ts
        Assert.Equal(2, entry.Count);                                                 // refreshed
        Assert.Equal(new Vec3(155, 0, 22), m.Pose.Translation);
        // identical poses (within 1 mm): stored
        var (w2, m2, p2) = UsableRig();
        w2.ComputedRobotPoseAt = _ => Pose3d.Identity;
        Assert.Equal(1, p2.Insert(Seen(m2, At(150.5, 0)), m2, 100f, false, false));
        Assert.Single(p2.Pairs);
        // the robot moved between the two times: stored (a different pose per time)
        var (w3, m3, p3) = UsableRig();
        w3.EntryFor(m3.ObjectId)!.LastPoseUpdatedTime = 500;
        w3.ComputedRobotPoseAt = ts => ts == 500 ? Pose3d.Identity : new Pose3d(Mat3.Identity, new Vec3(20, 0, 0));
        Assert.Equal(1, p3.Insert(Seen(m3, At(160, 0)), m3, 100f, false, false));
        Assert.Equal(1, w3.EntryFor(m3.ObjectId)!.Count);
        // GetComputedStateAt fails: stored
        var (w4, m4, p4) = UsableRig();
        w4.ComputedRobotPoseAt = _ => null;
        Assert.Equal(1, p4.Insert(Seen(m4, At(160, 0)), m4, 100f, false, false));
    }

    /// <summary>
    /// M11-044 P6 (0x0050CF8A..0x0050D120): one pair per matched pose root; the first is stored (return 1); a later one at least as far keeps the stored one and, being in the world origin,
    /// goes to <c>UseDiscardedObservation(new, false)</c> (return 1); a nearer one replaces it and, the root being the current origin, the OLD pair goes to
    /// <c>UseDiscardedObservation(old, false)</c> (return 1). Each discard reaches <c>AddVisualObservation</c> only when the pair's flag is clear.
    /// </summary>
    [Fact]
    public void M11_044_OnePairPerRootNearestWinsAndTheLosersAreDiscarded()
    {
        var w = new BlockWorld();
        ObservableObject Usable(uint id, ObjectType t) { var o = Locate(w, Cube(w, id, At(150, 0), t)); o.ActiveId = 1; o.FromDistance = 100; return o; }
        var a = Usable(1, ObjectType.Block_LIGHTCUBE1); var b = Usable(2, ObjectType.Block_LIGHTCUBE2); var c = Usable(3, ObjectType.Block_LIGHTCUBE3);
        var p = new PotentialObjectsForLocalizingTo(w);
        Assert.Equal(1, p.Insert(Seen(a, At(150, 0)), a, 100f, false, false));
        Assert.Same(a, p.Pairs[1].Matched);
        Assert.Equal(1, p.Insert(Seen(b, At(150, 0)), b, 150f, false, false));                  // farther: the stored pair stays, the new one is discarded
        Assert.Same(a, p.Pairs[1].Matched);
        Assert.Equal(2, w.EntryFor(2)!.Count);                                                    // UseDiscarded(new) reached AddVisualObservation
        Assert.Equal(1, w.EntryFor(1)!.Count);
        Assert.Equal(1, p.Insert(Seen(c, At(150, 0)), c, 50f, false, false));                   // nearer: replaces, the old one is discarded
        Assert.Same(c, p.Pairs[1].Matched);
        Assert.Equal(2, w.EntryFor(1)!.Count);
        Assert.Equal(1, w.EntryFor(3)!.Count);
        // an equal distance counts as "not nearer" (the bpl at 0x0050D108)
        var (w2, m2, p2) = UsableRig();
        var d = Locate(w2, Cube(w2, 4, At(150, 0), ObjectType.Block_LIGHTCUBE2)); d.ActiveId = 1; d.FromDistance = 1;
        p2.Insert(Seen(m2, At(150, 0)), m2, 80f, false, false);
        Assert.Equal(1, p2.Insert(Seen(d, At(150, 0)), d, 80f, false, false));
        Assert.Same(m2, p2.Pairs[1].Matched);
        // a flag-set pair that loses is not run again
        var (w3, m3, p3) = UsableRig();
        var e3 = Locate(w3, Cube(w3, 5, At(150, 0), ObjectType.Block_LIGHTCUBE2)); e3.ActiveId = 1; e3.FromDistance = 1;
        p3.Insert(Seen(m3, At(150, 0)), m3, 80f, false, false);
        p3.Insert(Seen(e3, At(150, 0)), e3, 90f, true, false);
        Assert.Equal(1, w3.EntryFor(5)!.Count);
    }

    /// <summary>
    /// M11-044 P10 (0x0050D1CC..0x0050D4DA, 0x005154B0): with one stored pair in the current origin <c>LocalizeRobot</c> calls <c>LocalizeToObject</c>, which here performs only the
    /// <c>SetLocalizedTo</c> field writes (counted as NOT BUILT beyond them); an unusable matched object is the UnlocalizedObject failure and its result is returned; with several pairs the
    /// farthest goes first and every one is attempted.
    /// </summary>
    [Fact]
    public void M11_044_LocalizeRobotDoesOnlyTheSetLocalizedToFieldWrites()
    {
        var (w, m, p) = UsableRig();
        Assert.Equal(0u, new PotentialObjectsForLocalizingTo(w).LocalizeRobot());          // empty: 0
        Assert.Null(w.LocalizedToObjectId);
        p.Insert(Seen(m, At(150, 0)), m, 100f, false, false);
        Assert.Equal(0u, p.LocalizeRobot());
        Assert.Equal(m.ObjectId, w.LocalizedToObjectId);
        Assert.False(w.RobotMovedSinceLocalized);
        Assert.Equal(1, w.UnbuiltLocalizeToObjectCalls);
        // an unusable matched object: LocalizeToObject refuses (vtable+0x18 != 1) and the failure is the result
        var (w2, m2, p2) = UsableRig();
        p2.Insert(Seen(m2, At(150, 0)), m2, 100f, false, false);
        m2.PoseState = PoseState.Dirty;
        Assert.Equal(1u, p2.LocalizeRobot());
        Assert.Null(w2.LocalizedToObjectId);
        // the tap filter
        var (w3, m3, p3) = UsableRig();
        w3.ShouldIgnoreMovementDueToDoubleTap = _ => true;
        p3.Insert(Seen(m3, At(150, 0)), m3, 100f, false, false);
        Assert.Equal(1u, p3.LocalizeRobot());
        // two roots: the pair in another origin is farther, so it goes first and the current-origin one last (it is re-found by ID)
        var w4 = new BlockWorld();
        ObservableObject Usable(uint id, ObjectType t, uint origin) { var o = Locate(w4, Cube(w4, id, At(150, 0), t)); o.ActiveId = 1; o.FromDistance = 10; return o; }
        var near = Usable(1, ObjectType.Block_LIGHTCUBE1, 1);
        var farObj = Cube(w4, 2, At(150, 0), ObjectType.Block_LIGHTCUBE2); farObj.OriginId = 2; farObj.ActiveId = 1; farObj.FromDistance = 10; w4.AddLocatedObject(farObj);
        var p4 = new PotentialObjectsForLocalizingTo(w4);
        Assert.Equal(1, p4.Insert(Seen(near, At(150, 0)), near, 50f, false, false));
        Assert.Equal(1, p4.Insert(Seen(farObj, At(150, 0)), farObj, 100f, false, false));
        Assert.Equal(0u, p4.LocalizeRobot());
        Assert.Equal(2, w4.UnbuiltLocalizeToObjectCalls);
        Assert.Equal(near.ObjectId, w4.LocalizedToObjectId);
    }

    /// <summary>
    /// M11-044 A6..A7 (0x0062115E..0x00621214): <c>AddAndUpdateObjects</c> stores <c>BlockWorld+0x54 = 1</c> and <c>BlockWorld+0x58 = (u32)fmax((double)ts, MemoryMap.vt+0x38())</c>.
    /// </summary>
    [Fact]
    public void M11_044_TheTimeOfLastChangeIsTheLargerOfTheTimestampAndTheMapsTime()
    {
        if (!NeedsLibrary()) return;
        using var rig = new ImageRig();
        var o = Assert.Single(rig.Observe((ObjectType.Block_LIGHTCUBE1, At(150, 0))).Objects).Object;
        Assert.True(rig.Vision.World.DidObjectsChange);
        Assert.Equal(o.LastObservedTimestamp, rig.Vision.World.TimeOfLastChangeField);
        rig.Vision.World.MemoryMapTimeOfLastChange = () => 9_000_000.0;
        rig.Frame((ObjectType.Block_LIGHTCUBE1, At(150, 0)));
        Assert.Equal(9_000_000u, rig.Vision.World.TimeOfLastChangeField);
    }

    // ------------------------------------------------------------------------------------------- fix round: M11-007 (MarkObjectUnknown), M11-037, M11-004, M11-049

    /// <summary>
    /// M11-007, <c>MarkObjectUnknown</c> 0x00507128: the walk is UPWARD only. Two consecutive misses of the middle cube of a stack of three delete it and the cube above it (0x0050724C..0x0050731E)
    /// and the cube UNDER it survives: nothing underneath is ever looked at. Nothing is written to any PoseState, and the confirmer entry is not erased (0x005073F4..0x005073FC).
    /// </summary>
    [Fact]
    public void M11_007_MarkObjectUnknownWalksUpwardOnlyAndTheLowerObjectSurvives()
    {
        var w = new BlockWorld();
        var lower = Locate(w, Cube(w, 5, At(200, 0, 22), state: PoseState.Known));
        var mid = Locate(w, Cube(w, 1, At(200, 0, 66), state: PoseState.Dirty));
        var top = Locate(w, Cube(w, 2, At(200, 0, 110), state: PoseState.Known));
        var far = Locate(w, Cube(w, 3, At(200, 200, 22), state: PoseState.Known));
        var changes = new List<(uint, PoseState, PoseState)>();
        w.PoseStateChanged += (o, a, b) => changes.Add((o.ObjectId, a, b));
        var gone = new List<ObservableObject>();
        w.MarkObjectUnobserved(mid, gone); w.MarkObjectUnobserved(mid, gone);
        Assert.Equal(2, gone.Count); Assert.Contains(mid, gone); Assert.Contains(top, gone);
        Assert.Same(lower, w.GetLocatedObjectById(5));                             // the lower object survives
        Assert.Same(far, w.GetLocatedObjectById(3));
        Assert.Null(w.GetLocatedObjectById(1)); Assert.Null(w.GetLocatedObjectById(2));
        Assert.Equal(PoseState.Known, lower.PoseState);
        Assert.Equal(PoseState.Dirty, mid.PoseState);                              // no PoseState written
        Assert.NotNull(w.EntryFor(1));                                             // the entry is kept
        Assert.Equal(2, changes.Count);
        // marking the LOWER cube unknown takes the whole stack above it
        var w2 = new BlockWorld();
        var a = Locate(w2, Cube(w2, 1, At(200, 0, 22), state: PoseState.Dirty));
        Locate(w2, Cube(w2, 2, At(200, 0, 66))); Locate(w2, Cube(w2, 3, At(200, 0, 110)));
        w2.MarkObjectUnobserved(a); w2.MarkObjectUnobserved(a);
        Assert.Empty(w2.LocatedObjects);
    }

    /// <summary>
    /// M11-007 (0x0050728E <c>mov r7,fp</c>, 0x0050731C <c>cmp r7,#0</c>, 0x0050731E <c>bne 0x005071D2</c>): a carried cube found on top is warned about and NOT added to the set, but it
    /// becomes the current object, so the walk CONTINUES and the cube above it (the filter ignores only the current ID) is found and deleted.
    /// </summary>
    [Fact]
    public void M11_007_ACarriedCubeIsSkippedButTheWalkContinuesAboveIt()
    {
        var w = new BlockWorld { IsCarryingObject = id => id == 2 };
        var a = Locate(w, Cube(w, 1, At(200, 0, 22), state: PoseState.Dirty));
        var b = Locate(w, Cube(w, 2, At(200, 0, 66)));
        var c = Locate(w, Cube(w, 3, At(200, 0, 110)));
        w.MarkObjectUnobserved(a); w.MarkObjectUnobserved(a);
        Assert.Null(w.GetLocatedObjectById(1));
        Assert.Same(b, w.GetLocatedObjectById(2));                                  // the carried cube is not deleted
        Assert.Null(w.GetLocatedObjectById(3));                                     // the cube above it is
    }

    /// <summary>
    /// M11-007 (second skip clause, 0x00505EDE..0x00505EF6) with <c>ActiveObject::CanBeUsedForLocalization</c> 0x004E49AC: an existing object that is Known, has activeID and fromDistance
    /// >= 0 and rests flat, seen beyond 250 mm by a steady camera (so not closeAndSteady), is left alone; the same object with activeID -1, or tilted 10 degrees, is written (Dirty,
    /// dist > 250).
    /// </summary>
    [Fact]
    public void M11_007_TheSecondSkipClauseSkipsAUsableKnownObjectOutOfRange()
    {
        ObservableObject Existing(BlockWorld w, Action<ObservableObject>? tweak = null)
        {
            var e = Locate(w, Cube(w, 66, At(150, 0), state: PoseState.Known));
            e.ActiveId = 1; e.FromDistance = 100;
            tweak?.Invoke(e);
            return e;
        }
        var w1 = new BlockWorld(); var e1 = Existing(w1);
        w1.AddVisualObservation(Instance(66, At(160, 0)), e1, wasCameraMoving: false, dist: 300);
        Assert.Equal(new Vec3(150, 0, 22), e1.Pose.Translation);
        Assert.Equal(PoseState.Known, e1.PoseState);
        var w2 = new BlockWorld(); var e2 = Existing(w2, e => e.ActiveId = -1);
        w2.AddVisualObservation(Instance(66, At(160, 0)), e2, false, 300);
        Assert.Equal(new Vec3(160, 0, 22), e2.Pose.Translation);
        Assert.Equal(PoseState.Dirty, e2.PoseState);
        var w3 = new BlockWorld(); var e3 = Existing(w3, e => e.Pose = new Pose3d(Mat3.AxisAngle(new Vec3(1, 0, 0), 10 * Math.PI / 180), new Vec3(150, 0, 22)));
        w3.AddVisualObservation(Instance(66, At(160, 0)), e3, false, 300);
        Assert.Equal(new Vec3(160, 0, 22), e3.Pose.Translation);
    }

    /// <summary>
    /// M11-037 (row 3C.2, 0x00621D64..0x00621D68, GetLastVisuallyMatchedTime 0x00507824..0x0050783A): the candidate test is only <c>GetLastVisuallyMatchedTime(ID) &lt; ts</c>, the getter
    /// reading the confirmer entry's +0x30 (0 when there is no entry). The object's own timestamp plays no part.
    /// </summary>
    [Fact]
    public void M11_037_TheCandidateTestIsTheConfirmersLastVisuallyMatchedTimeOnly()
    {
        var pd = Pd(1000);
        var cam = CameraOf(pd);
        var w = new BlockWorld();
        var o = Locate(w, Cube(w, 1, At(150, 0), state: PoseState.Dirty));
        o.LastObservedTimestamp = 1000;                                               // "seen this frame" by its own timestamp
        w.EntryFor(1)!.LastVisuallyMatchedTime = 500;                                 // but not visually matched
        w.CheckForUnobservedObjects(cam, 1000, false, false);
        Assert.Equal(1, o.UnobservedCount);                                           // a candidate
        w.EntryFor(1)!.LastVisuallyMatchedTime = 1000;
        w.CheckForUnobservedObjects(cam, 1000, false, false);
        Assert.Equal(1, o.UnobservedCount);                                           // matched at ts: not a candidate
        // no entry: 0 < ts, a candidate, and MarkObjectUnobserved answers 1 (its ObjectNotFound)
        var w2 = new BlockWorld();
        var log = new List<string>(); w2.Log += log.Add;
        var o2 = Cube(w2, 1, At(150, 0), state: PoseState.Dirty); w2.AddLocatedObject(o2);
        w2.CheckForUnobservedObjects(cam, 1000, false, false);
        Assert.Contains(log, l => l.Contains("MarkObjectUnobservedFailed"));
        Assert.False(BlockWorld.PaddingIsBuilt);                                      // xPad/yPad stay 0 and say so
    }

    /// <summary>
    /// M11-037 (0x00625020..0x00625052, 0x0062521A): the empty-list branch and UpdateMarkerlessObjects use <c>Robot::GetLastImageTimeStamp()</c>, not the frame's timestamp: with the last
    /// image timestamp 0 the branch skips the occluders and the unobserved check whatever the frame's timestamp is, and the markerless expiry is measured against it.
    /// </summary>
    [Fact]
    public void M11_037_TheEmptyListBranchAndMarkerlessObjectsUseTheLastImageTimestamp()
    {
        var w = new BlockWorld { LastImageTimestamp = () => 0 };
        var o = Locate(w, Cube(w, 1, At(150, 0), state: PoseState.Dirty));
        var pd = Pd(1000);
        w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(), CameraOf(pd), 1000, pd);
        Assert.Equal(0, o.UnobservedCount);
        w.LastImageTimestamp = () => 1000;
        w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(), CameraOf(pd), 5, pd);
        Assert.Equal(1, o.UnobservedCount);
        var cliff = w.AddMarkerlessObject(At(300, 0, 0), ObjectType.CliffDetection);
        cliff.LastObservedTimestamp = 100;
        w.LastImageTimestamp = () => 30101;                                            // 100 + 30000 < 30101 whatever the frame says
        w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(), CameraOf(pd), 1, pd);
        Assert.Null(w.GetObjectById(cliff.ObjectId));
    }

    /// <summary>
    /// M11-004 (0x00628248, 0x00628250, 0x0061FAA6): the non-unique closest-match predicate writes abs(delta) back to BOTH the translation tolerance and the angle tolerance, so a later
    /// object that is closer in translation but turned further than the first accepted one is refused; and the search ignores the observed object's own ID.
    /// </summary>
    [Fact]
    public void M11_004_TheNonUniquePredicateNarrowsTheAngleToo()
    {
        var w = new BlockWorld();
        var first = w.AddMarkerlessObject(new Pose3d(Mat3.AboutZ(0.3), new Vec3(100, 0, 0)), ObjectType.ProxObstacle);
        var second = w.AddMarkerlessObject(new Pose3d(Mat3.AboutZ(0.5), new Vec3(110, 0, 0)), ObjectType.ProxObstacle);
        var observed = new ObservableObject(ObjectType.ProxObstacle, Array.Empty<KnownMarker>()) { Pose = new Pose3d(Mat3.Identity, new Vec3(108, 0, 25)) };
        Assert.Same(first, w.FindObjectMatchForObservation(observed));               // second is 2 mm off but turned 0.5 > 0.3
        observed.ObjectId = first.ObjectId;                                           // its own ID is ignored
        Assert.Same(second, w.FindObjectMatchForObservation(observed));
    }

    /// <summary>
    /// M11-004 (0x00620FE0): for a non-unique type the located object is <c>GetLocatedObjectByIdHelper(id, -1)</c> with NO not-carried predicate, so a carried object is found. (No cube or
    /// charger is non-unique; the private method is called directly.)
    /// </summary>
    [Fact]
    public void M11_004_TheNonUniqueLocatedLookupHasNoCarriedPredicate()
    {
        var w = new BlockWorld { IsCarryingObject = _ => true };
        var prox = w.AddMarkerlessObject(new Pose3d(Mat3.Identity, new Vec3(100, 0, 0)), ObjectType.ProxObstacle);
        var instance = new ObservableObject(prox.ObjectId, ObjectType.ProxObstacle, Array.Empty<KnownMarker>());
        var method = typeof(BlockWorld).GetMethod("FindLocatedForObserved", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var args = new object?[] { instance, null, false };
        var found = method.Invoke(w, args);
        Assert.Same(prox, found);
        Assert.False((bool)args[2]!);
    }

    /// <summary>
    /// M11-037 (0x00536980, 0x0051397C, 0x0053698E): the engine calls <c>Robot::SetPhysicalRobot</c> (which reaches <c>VisionComponent::SetPhysicalRobot</c>) BEFORE
    /// <c>RobotAudioClient::SetOutputSource</c>: when the event is raised the physical flag is already set and the output source is not yet; afterwards the source is set.
    /// </summary>
    [Fact]
    public void M11_037_SetPhysicalRobotComesBeforeSetOutputSource()
    {
        using var robot = CozmoRobot.CreateOffline();
        var conn = new Frame { Type = ReliableMessageType.MultipleMixedMessages, SeqMin = 1, SeqMax = 1, Ack = 0, Messages = new List<SubMessage> { new(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1) } };
        robot.Transport.ProcessIncoming(FrameCodec.Encode(conn));
        RobotAudioOutputSource? sourceAtEvent = RobotAudioOutputSource.None; bool? physicalAtEvent = null;
        robot.Engine.PhysicalRobotSet += physical =>
        {
            sourceAtEvent = robot.Engine.Robot!.AudioOutputSource;
            physicalAtEvent = robot.Engine.Robot.IsPhysicalRobot == physical && physical;
        };
        robot.Engine.Robot!.HandleFirmwareVersion(new FirmwareVersion { Signature = System.Text.Encoding.UTF8.GetBytes("{\"version\":1}") });
        Assert.True(physicalAtEvent);                                                 // SetPhysicalRobot's write is done when VisionComponent hears it
        Assert.Null(sourceAtEvent);                                                   // SetOutputSource has not run yet (0x00536980 before 0x0053698E)
        Assert.Equal(RobotAudioOutputSource.PlayOnRobot, robot.Engine.Robot.AudioOutputSource);
    }

    /// <summary>
    /// M11-037 (0x0051397C): a vision system built after the firmware version was handled catches up from the recorded SetPhysicalRobot, not from the audio output source: with the
    /// output source still unset (the handler stopped between the two calls) the catch-up still finds the physical flag.
    /// </summary>
    [Fact]
    public void M11_037_TheCatchUpDoesNotDependOnTheAudioOutputSource()
    {
        using var robot = CozmoRobot.CreateOffline();
        var conn = new Frame { Type = ReliableMessageType.MultipleMixedMessages, SeqMin = 1, SeqMax = 1, Ack = 0, Messages = new List<SubMessage> { new(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1) } };
        robot.Transport.ProcessIncoming(FrameCodec.Encode(conn));
        Assert.False(robot.Engine.Robot!.PhysicalRobotRecorded);
        robot.Engine.Robot.HandleFirmwareVersion(new FirmwareVersion { Signature = System.Text.Encoding.UTF8.GetBytes("{\"version\":1}") });
        Assert.True(robot.Engine.Robot.PhysicalRobotRecorded);
        using var vision = new VisionSystem(robot, Cal);
        Assert.Equal(8, vision.World.LiftOccluderPoints.Count);
    }

    /// <summary>
    /// M11-037 (Q8, 0x00536980, 0x0051397C): the firmware version arrives once. A vision system built AFTER it was handled (the tools build it after connecting) catches up from what the
    /// engine robot recorded, so the lift occluder points are not left empty: no "sim" key is a physical robot (-36.5), a "sim" key the other (-28.5).
    /// </summary>
    [Theory]
    [InlineData("{\"version\":1}", -36.5)]
    [InlineData("{\"version\":1,\"sim\":1}", -28.5)]
    public void M11_037_ASystemBuiltAfterTheFirmwareVersionCatchesUp(string json, double expectedZ)
    {
        using var robot = CozmoRobot.CreateOffline();
        var conn = new Frame { Type = ReliableMessageType.MultipleMixedMessages, SeqMin = 1, SeqMax = 1, Ack = 0, Messages = new List<SubMessage> { new(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1) } };
        robot.Transport.ProcessIncoming(FrameCodec.Encode(conn));
        Assert.NotNull(robot.Engine.Robot);
        robot.Engine.Robot!.HandleFirmwareVersion(new FirmwareVersion { Signature = System.Text.Encoding.UTF8.GetBytes(json) });
        using var vision = new VisionSystem(robot, Cal);
        Assert.Equal(8, vision.World.LiftOccluderPoints.Count);
        Assert.Equal(expectedZ, vision.World.LiftOccluderPoints[7].Z);
    }


    // ------------------------------------------------------------------------------------------- M11-004

    /// <summary>
    /// M11-004, <c>FindLocatedObjectHelper</c> 0x0061EB78: the walk is ascending origin, family, type, ObjectID (0x0061EBD0..0x0061EBEA); every passing object becomes
    /// the running result and the result is the LAST passing object, or the FIRST with returnFirstOnly (0x0061ED82..0x0061ED8E, 0x0061EE12..0x0061EE36); the modify
    /// function runs for each passing object (0x0061ED72..0x0061ED7C); there is no distance or recency comparison.
    /// </summary>
    [Fact]
    public void M11_004_TheWalkIsAscendingAndTheResultIsTheLastPassingObjectOrTheFirst()
    {
        var w = new BlockWorld();
        var charger = new ObservableObject(9, ObjectType.Charger_Basic, ChargerGeometry.Markers) { Pose = At(300, 0, 0), PoseState = PoseState.Known, OriginId = 1 };
        w.AddLocatedObject(charger);
        w.AddLocatedObject(Cube(w, 5, At(100, 0), ObjectType.Block_LIGHTCUBE2));
        w.AddLocatedObject(Cube(w, 8, At(120, 0)));
        w.AddLocatedObject(Cube(w, 3, At(140, 0)));
        // family LightCube (2) before Charger (4), type LIGHTCUBE1 (1) before LIGHTCUBE2 (2), ID ascending
        Assert.Equal(new uint[] { 3, 8, 5, 9 }, w.FindLocatedMatchingObjects(new BlockWorldFilter()).Select(o => o.ObjectId));
        Assert.Equal(9u, w.FindLocatedObjectHelper(new BlockWorldFilter(), null, false)!.ObjectId);       // the last passing object
        Assert.Equal(3u, w.FindLocatedObjectHelper(new BlockWorldFilter(), null, true)!.ObjectId);        // the first with returnFirstOnly
        var seen = new List<uint>();
        w.FindLocatedObjectHelper(new BlockWorldFilter(), o => seen.Add(o.ObjectId), true);
        Assert.Equal(new uint[] { 3 }, seen);                                                             // the modify function ran for the first only
        seen.Clear();
        w.FindLocatedObjectHelper(new BlockWorldFilter(), o => seen.Add(o.ObjectId), false);
        Assert.Equal(new uint[] { 3, 8, 5, 9 }, seen);
        Assert.Null(w.FindLocatedObjectHelper(new BlockWorldFilter { AllowedIds = { 77 } }, null, false));
    }

    /// <summary>M11-004: the ignore and allowed sets of ID, family and type, and the predicate list, all must pass (0x0061EC60..0x0061ED66); an empty allowed set passes everything.</summary>
    [Fact]
    public void M11_004_TheFilterFieldsNarrowTheWalk()
    {
        var w = new BlockWorld();
        w.AddLocatedObject(new ObservableObject(9, ObjectType.Charger_Basic, ChargerGeometry.Markers) { Pose = At(300, 0, 0), PoseState = PoseState.Known, OriginId = 1 });
        w.AddLocatedObject(Cube(w, 5, At(100, 0), ObjectType.Block_LIGHTCUBE2));
        w.AddLocatedObject(Cube(w, 8, At(120, 0)));
        uint[] Ids(BlockWorldFilter f) => w.FindLocatedMatchingObjects(f).Select(o => o.ObjectId).ToArray();
        Assert.Equal(new uint[] { 8 }, Ids(new BlockWorldFilter { AllowedTypes = { ObjectType.Block_LIGHTCUBE1 } }));
        Assert.Equal(new uint[] { 8, 5 }, Ids(new BlockWorldFilter { AllowedFamilies = { ObjectFamily.LightCube } }));
        Assert.Equal(new uint[] { 8, 5 }, Ids(new BlockWorldFilter { IgnoreFamilies = { ObjectFamily.Charger } }));
        Assert.Equal(new uint[] { 8, 9 }, Ids(new BlockWorldFilter { IgnoreIds = { 5 } }));
        Assert.Equal(new uint[] { 9 }, Ids(new BlockWorldFilter { AllowedIds = { 9, 5 }, IgnoreTypes = { ObjectType.Block_LIGHTCUBE2 } }));
        var f = new BlockWorldFilter();
        f.Predicates.Add(o => o.Pose.Translation.X > 110);
        f.Predicates.Add(o => o.ObjectId != 9);
        Assert.Equal(new uint[] { 8 }, Ids(f));
    }

    /// <summary>
    /// M11-004, origin test 0x0061EBF2..0x0061EC4C: mode 0 keeps the robot's origin, mode 1 everything but it, mode 2 everything, any other value requires the
    /// origin to be absent from ignoreOrigins and, when allowedOrigins is not empty, present in it. The walk is ascending origin first.
    /// </summary>
    [Fact]
    public void M11_004_TheOriginModesSelectByTheRobotsOrigin()
    {
        var w = new BlockWorld { CurrentOriginId = 1 };
        var here = Cube(w, 4, At(100, 0)); var there = Cube(w, 4, At(200, 0)); there.OriginId = 2; var third = Cube(w, 6, At(300, 0)); third.OriginId = 3;
        w.AddLocatedObject(here); w.AddLocatedObject(there); w.AddLocatedObject(third);
        uint[] Origins(BlockWorldFilter f) => w.FindLocatedMatchingObjects(f).Select(o => o.OriginId).ToArray();
        Assert.Equal(new uint[] { 1 }, Origins(new BlockWorldFilter { OriginMode = OriginMode.InRobotFrame }));
        Assert.Equal(new uint[] { 2, 3 }, Origins(new BlockWorldFilter { OriginMode = OriginMode.NotInRobotFrame }));
        Assert.Equal(new uint[] { 1, 2, 3 }, Origins(new BlockWorldFilter { OriginMode = OriginMode.InAnyFrame }));
        Assert.Equal(new uint[] { 2 }, Origins(new BlockWorldFilter { OriginMode = OriginMode.Custom, AllowedOrigins = { 2 } }));
        Assert.Equal(new uint[] { 1, 3 }, Origins(new BlockWorldFilter { OriginMode = OriginMode.Custom, IgnoreOrigins = { 2 } }));
        Assert.Equal(new uint[] { 1, 2, 3 }, Origins(new BlockWorldFilter { OriginMode = OriginMode.Custom }));
        // the default filter is mode 0, so GetLocatedObjectByIdHelper finds only the robot's origin (ID 4 exists in two)
        Assert.Same(here, w.GetLocatedObjectById(4));
    }

    /// <summary>M11-004, onlyLatest (byte +0x6C): the predicate <c>obj[+0x1C] == BlockWorld[+0x90]</c> (0x0061EB98..0x0061EBCE, 0x00627EAA); [+0x90] is the first marker's timestamp, 0 for an empty list (0x00625024).</summary>
    [Fact]
    public void M11_004_OnlyLatestKeepsTheObjectsObservedInTheCurrentFrame()
    {
        var w = new BlockWorld();
        var a = Cube(w, 1, At(100, 0)); a.LastObservedTimestamp = 500;
        var b = Cube(w, 2, At(150, 0)); b.LastObservedTimestamp = 400;
        w.AddLocatedObject(a); w.AddLocatedObject(b);
        typeof(BlockWorld).GetField("_markerTimestamp", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(w, 500u);
        Assert.Equal(new uint[] { 1 }, w.FindLocatedMatchingObjects(new BlockWorldFilter { OnlyConsiderLatestUpdate = true }).Select(o => o.ObjectId));
        Assert.Equal(new uint[] { 1, 2 }, w.FindLocatedMatchingObjects(new BlockWorldFilter()).Select(o => o.ObjectId));
    }

    /// <summary>
    /// M11-004, <c>FindLocatedClosestMatchingObjectHelper</c> 0x0061FA68, predicate 0x006281DA: the ObjectType and <c>Pose3d::IsSameAs</c>, narrowing the captured
    /// tolerance to abs(delta), so the closest passing object is the last one accepted and wins whichever order the walk meets them in; the tolerance is 0.8 * extent
    /// (35.2 mm, thunk 0x004E025C) and the angle pi/4 (thunk 0x004E0290). This is the NON-unique branch of <c>FindObjectMatchForObservation</c> 0x005063CC.
    /// </summary>
    [Theory]
    [InlineData(100.0, 110.0)]
    [InlineData(110.0, 100.0)]
    public void M11_004_ForANonUniqueTypeTheClosestLocatedObjectWins(double firstX, double secondX)
    {
        var w = new BlockWorld();
        var first = w.AddMarkerlessObject(new Pose3d(Mat3.Identity, new Vec3(firstX, 0, 0)), ObjectType.ProxObstacle);
        var second = w.AddMarkerlessObject(new Pose3d(Mat3.Identity, new Vec3(secondX, 0, 0)), ObjectType.ProxObstacle);
        // the observation is 2 mm from the one at 110 and 8 from the one at 100; the markerless objects stand on the ground, half their height up
        var observed = new ObservableObject(ObjectType.ProxObstacle, Array.Empty<KnownMarker>()) { Pose = new Pose3d(Mat3.Identity, new Vec3(108, 0, 25)) };
        var match = w.FindObjectMatchForObservation(observed);
        Assert.Same(firstX == 110 ? first : second, match);
        // beyond the tolerance (35.2 mm) or the rotation (pi/4): no match
        var far = new ObservableObject(ObjectType.ProxObstacle, Array.Empty<KnownMarker>()) { Pose = new Pose3d(Mat3.Identity, new Vec3(160, 0, 25)) };
        Assert.Null(w.FindObjectMatchForObservation(far));
        var turned = new ObservableObject(ObjectType.ProxObstacle, Array.Empty<KnownMarker>()) { Pose = new Pose3d(Mat3.AboutZ(1.0), new Vec3(108, 0, 25)) };
        Assert.Null(w.FindObjectMatchForObservation(turned));
    }

    /// <summary>
    /// M11-004, <c>ObservableObject::IsSameAs</c> 0x008769A8 ignores its Radians tolerance (<c>mov r3,r4</c> at 0x008769D8) and passes pi to
    /// <c>Pose3d::IsSameAs_WithAmbiguity</c>: in the fallback scan of the confirmer's pending instances only the translation tolerance narrows, and the last accepted
    /// wins (0x005065C4..0x005066B4).
    /// </summary>
    [Fact]
    public void M11_004_TheFallbackMatchIgnoresTheAngleAndNarrowsOnlyTheTranslation()
    {
        var w = new BlockWorld();
        var near = new ObservableObject(ObjectType.ProxObstacle, Array.Empty<KnownMarker>()) { Pose = new Pose3d(Mat3.AboutZ(2.5), new Vec3(100, 0, 25)), ObjectId = 30 };
        var nearer = new ObservableObject(ObjectType.ProxObstacle, Array.Empty<KnownMarker>()) { Pose = new Pose3d(Mat3.AboutZ(-2.9), new Vec3(104, 0, 25)), ObjectId = 31 };
        w.AddVisualObservation(near, null, false, 0);                    // the first sighting of each: pending entries holding the instances
        w.AddVisualObservation(nearer, null, false, 0);
        var observed = new ObservableObject(ObjectType.ProxObstacle, Array.Empty<KnownMarker>()) { Pose = new Pose3d(Mat3.Identity, new Vec3(105, 0, 25)) };
        // both are within 35.2 mm however far round they are turned; the walk meets 30 first (5 mm off, the tolerance narrows to 5), then 31 (1 mm off, inside the narrowed 5): the last accepted wins
        Assert.Same(nearer, w.FindObjectMatchForObservation(observed));
        var observed2 = new ObservableObject(ObjectType.ProxObstacle, Array.Empty<KnownMarker>()) { Pose = new Pose3d(Mat3.Identity, new Vec3(99, 0, 25)) };
        // 1 mm from the first, 5 from the second: the second is outside the narrowed tolerance, so the first stays
        Assert.Same(near, w.FindObjectMatchForObservation(observed2));
    }

    /// <summary>
    /// M11-004, the unique branch of <c>FindObjectMatchForObservation</c> (rows 1.11 a..e, 0x00506498..0x0050675E): (a) exactly one located object of the family and type
    /// in the current origin is the match; (b) else a pending instance of the confirmer; (c) else the first located object in another origin; (d) else the LAST
    /// connected object of that family and type; (e) else none.
    /// </summary>
    [Fact]
    public void M11_004_TheUniqueMatchTriesLocatedThenPendingThenOtherOriginsThenConnected()
    {
        var w = new BlockWorld { CurrentOriginId = 1 };
        var observed = new ObservableObject(ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1)) { Pose = At(150, 0) };
        Assert.Null(w.FindObjectMatchForObservation(observed));                                                   // (e)
        Assert.NotEqual(-1, w.AddConnectedActiveObject(1, 0xA, ObjectType.Block_LIGHTCUBE1));
        var connected = w.ConnectedObjects.Single();
        Assert.Same(connected, w.FindObjectMatchForObservation(observed));                                        // (d)
        var elsewhere = Cube(w, 40, At(300, 0)); elsewhere.OriginId = 2; w.AddLocatedObject(elsewhere);
        Assert.Same(elsewhere, w.FindObjectMatchForObservation(observed));                                        // (c) beats (d)
        var pending = new ObservableObject(ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1)) { Pose = At(150, 0), ObjectId = 41 };
        w.AddVisualObservation(pending, null, false, 0);
        Assert.Same(pending, w.FindObjectMatchForObservation(observed));                                          // (b) beats (c)
        var here = Cube(w, 42, At(150, 0)); w.AddLocatedObject(here);
        Assert.Same(here, w.FindObjectMatchForObservation(observed));                                             // (a) beats (b)
        var second = Cube(w, 43, At(250, 0)); w.AddLocatedObject(second);
        Assert.Same(pending, w.FindObjectMatchForObservation(observed));                                          // two located: (a) does not apply, (b) does
    }

    // ------------------------------------------------------------------------------------------- M11-007

    private static (BlockWorld World, ObservableObject Located) Located(uint id, Pose3d pose, PoseState state, Func<uint, bool>? carrying = null)
    {
        var w = new BlockWorld { IsCarryingObject = carrying };
        var o = Locate(w, Cube(w, id, pose, state: state));
        return (w, o);
    }

    private static ObservableObject Instance(uint id, Pose3d pose, uint ts = 1000) =>
        new(id, ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1)) { Pose = pose, PoseState = PoseState.Dirty, LastObservedTimestamp = ts };

    /// <summary>
    /// M11-007, <c>AddVisualObservation</c> 0x0050684C: the first sighting adds <c>PoseConfirmation(observed, 1, 0)</c> and returns false (0x005068DC..0x00506982), so no located
    /// object exists; a matching second sighting makes the count 2, runs <c>UpdatePoseInInstance(stored, ...)</c> then <c>AddLocatedObject(stored)</c> and returns true
    /// (0x00506A04..0x00506A32, 0x00506AC2); the located object IS the first instance, in the state the second sighting decided.
    /// </summary>
    [Fact]
    public void M11_007_TheFirstSightingOnlyRecordsAndTheSecondLocatesTheFirstInstance()
    {
        var w = new BlockWorld();
        var first = Instance(50, At(150, 0));
        Assert.False(w.AddVisualObservation(first, null, false, 150));
        Assert.Empty(w.LocatedObjects);
        Assert.Equal(1, first.PoseConfirmationCount);
        Assert.Equal(PoseState.Dirty, first.PoseState);                            // InitPose gave Dirty and nothing has decided yet
        var second = Instance(50, At(152, 1), ts: 1033);
        Assert.True(w.AddVisualObservation(second, null, false, 150));
        Assert.Same(first, Assert.Single(w.LocatedObjects));
        Assert.Equal(2, first.PoseConfirmationCount);
        Assert.Equal(PoseState.Known, first.PoseState);                            // closeAndSteady: OnTreads, 150 <= 250, camera still, not moving
        Assert.Equal(new Vec3(152, 1, 22), first.Pose.Translation);                // SetPose(pose of the second sighting, dist, Known) (0x00505EC4..0x00505ECE)
        Assert.Equal(150.0, first.FromDistance);
    }

    /// <summary>
    /// M11-007, <c>UpdatePoseInInstance</c> 0x00505E5E..0x00505EBA: <c>closeAndSteady = OnTreads &amp;&amp; (250.0 + 1e-5 &gt;= dist) &amp;&amp; !wasCameraMoving &amp;&amp; notMoving</c>; Known when
    /// true, else Dirty. <c>GetMaxLocalizationDistance_mm</c> 0x004EF461 is 250.0; the comparison is float32 and, being a vcmpe followed by pl, true for NaN.
    /// </summary>
    [Theory]
    [InlineData(250.0, false, true, true, true)]          // exactly 250: close
    [InlineData(250.00001, false, true, true, true)]      // inside the 1e-5 slack (250.0 + 1e-5 in float32 is 250.000015)
    [InlineData(250.1, false, true, true, false)]         // over
    [InlineData(100.0, true, true, true, false)]          // the camera was moving
    [InlineData(100.0, false, false, true, false)]        // not on its treads
    [InlineData(100.0, false, true, false, false)]        // the connected object reports movement
    [InlineData(double.NaN, false, true, true, true)]     // vcmpe then pl: unordered passes
    public void M11_007_KnownOnlyWhenCloseAndSteady(double dist, bool cameraMoving, bool onTreads, bool notMoving, bool expectKnown)
    {
        var w = new BlockWorld { OnTreads = () => onTreads };
        Assert.NotEqual(-1, w.AddConnectedActiveObject(1, 0xA, ObjectType.Block_LIGHTCUBE1));
        var connected = w.ConnectedObjects.Single();
        connected.IsMoving = !notMoving;
        var first = Instance(connected.ObjectId, At(150, 0));
        w.AddVisualObservation(first, null, cameraMoving, dist);
        Assert.True(w.AddVisualObservation(Instance(connected.ObjectId, At(150, 0)), null, cameraMoving, dist));
        Assert.Equal(expectKnown ? PoseState.Known : PoseState.Dirty, first.PoseState);
    }

    /// <summary>
    /// M11-007, the skip gate 0x00505E98..0x00505EFA: the update is left alone when the object is not the docking object and not carried and there is an existing object while the
    /// camera was moving. The docking object (DockingComponent+0xC == ID) is updated through the write helper with the pose clamped flat by 20 degrees
    /// (0x00505F10..0x00505F56); a carried object is updated too and is then unset as carried, with the SeeingCarriedObject log (0x00505F5A..0x00505FBC).
    /// </summary>
    [Fact]
    public void M11_007_TheSkipGateAndTheDockingAndCarriedExceptions()
    {
        var tilt15 = new Pose3d(Mat3.AxisAngle(new Vec3(1, 0, 0), 15 * Math.PI / 180) * Mat3.Identity, new Vec3(160, 0, 22));

        // skipped: existing object, camera moving
        var (w1, e1) = Located(61, At(150, 0), PoseState.Known);
        w1.AddVisualObservation(Instance(61, At(160, 0)), e1, true, 100);
        Assert.Equal(new Vec3(150, 0, 22), e1.Pose.Translation);                                             // nothing was written
        Assert.Equal(PoseState.Known, e1.PoseState);

        // the docking object is not skipped, is clamped by 20 degrees, and (camera moving) becomes Dirty
        var (w2, e2) = Located(62, At(150, 0), PoseState.Known);
        w2.DockingObjectId = () => 62;
        w2.AddVisualObservation(Instance(62, tilt15), e2, true, 100);
        Assert.Equal(PoseState.Dirty, e2.PoseState);
        Assert.Equal(1.0, Math.Abs(e2.Pose.Rotation[2, 2]), 4);                                              // 15 degrees is inside 20: snapped upright

        // the same tilt on an ordinary object (no camera movement) is written as it is
        var (w3, e3) = Located(63, At(150, 0), PoseState.Dirty);
        w3.AddVisualObservation(Instance(63, tilt15), e3, false, 100);
        Assert.True(Math.Abs(e3.Pose.Rotation[2, 2]) < 0.99);

        // a carried object is not skipped and is unset as carried
        var unset = new List<uint>();
        var (w4, e4) = Located(64, At(150, 0), PoseState.Known, id => id == 64);
        w4.UnSetCarryObject = unset.Add;
        w4.AddVisualObservation(Instance(64, At(160, 0)), e4, true, 100);
        Assert.Equal(new Vec3(160, 0, 22), e4.Pose.Translation);
        Assert.Equal(new uint[] { 64 }, unset);
        // and without the hook the call is counted, not made
        var (w5, e5) = Located(65, At(150, 0), PoseState.Known, id => id == 65);
        w5.AddVisualObservation(Instance(65, At(160, 0)), e5, true, 100);
        Assert.Equal(1, w5.UnwiredCarryUnsetCalls);
    }


    /// <summary>
    /// M11-007, a mismatching sighting (0x00506A46..0x00506A4E) sets the count to 1 and the entry pose to the observed one; an old count of 0 (after a miss) does not run the
    /// located update (0x00506A52); both zero the miss count and set +0x30 (0x00506A9C..0x00506AA4).
    /// </summary>
    [Fact]
    public void M11_007_AMismatchResetsTheCountAndAMissThenASightingCountsFromOne()
    {
        var (w, e) = Located(70, At(150, 0), PoseState.Known);
        Assert.Equal(1, e.PoseConfirmationCount);
        Assert.False(w.AddVisualObservation(Instance(70, At(300, 0)), e, false, 100));                    // 150 mm away: a mismatch
        Assert.Equal(1, e.PoseConfirmationCount);
        Assert.Equal(new Vec3(300, 0, 22), e.ReferencePose.Translation);
        Assert.Equal(new Vec3(150, 0, 22), e.Pose.Translation);                                           // nothing was moved
        Assert.Equal(1u, w.MarkObjectUnobserved(new ObservableObject(99, ObjectType.Block_LIGHTCUBE1, Array.Empty<KnownMarker>())));   // no entry: the ObjectNotFound error (0x00507036)
        Assert.Equal(0u, w.MarkObjectUnobserved(e));
        Assert.Equal(0, e.PoseConfirmationCount);                                                         // strd zeroes the count (0x00506FE0)
        Assert.Equal(1, e.UnobservedCount);
        Assert.False(w.AddVisualObservation(Instance(70, At(300, 0), 1100), e, false, 100));             // old count 0 -> 1, returns false
        Assert.Equal(1, e.PoseConfirmationCount);
        Assert.Equal(0, e.UnobservedCount);                                                               // a sighting restarts the misses
    }

    /// <summary>
    /// M11-007 (C-R2), <c>MarkObjectUnobserved</c> 0x00506FBC: the miss count is at node+0x28; the forgetting branch runs when the value read was already at least 1 (0x00506FDE),
    /// so two consecutive misses forget and a sighting in between does not. <c>MarkObjectUnknown</c> 0x00507128 walks the stack (<c>FindObjectOnTopOrUnderneathHelper</c> 15.0, carried
    /// objects skipped 0x00507270..0x00507288), broadcasts per object and deletes the object and its stack (0x005073F4); it writes no PoseState.
    /// </summary>
    [Fact]
    public void M11_007_TwoConsecutiveMissesDeleteTheObjectAndItsStack()
    {
        var w = new BlockWorld();
        var bottom = Locate(w, Cube(w, 1, At(200, 0, 22), state: PoseState.Dirty));
        var top = Locate(w, Cube(w, 2, At(200, 0, 66), state: PoseState.Known));
        var far = Locate(w, Cube(w, 3, At(200, 200, 22), state: PoseState.Known));
        var changes = new List<(uint, PoseState, PoseState)>();
        w.PoseStateChanged += (o, a, b) => changes.Add((o.ObjectId, a, b));
        var gone = new List<ObservableObject>();
        Assert.Equal(0u, w.MarkObjectUnobserved(bottom, gone));
        Assert.Empty(gone);
        Assert.Equal(1, bottom.UnobservedCount);
        w.AddVisualObservation(Instance(1, At(200, 0, 22)), bottom, false, 100);                          // a sighting between: the misses restart
        Assert.Equal(0, bottom.UnobservedCount);
        Assert.Equal(0u, w.MarkObjectUnobserved(bottom, gone));
        Assert.Empty(gone);
        Assert.Equal(0u, w.MarkObjectUnobserved(bottom, gone));                                           // the second consecutive miss
        Assert.Contains(bottom, gone);
        Assert.Contains(top, gone);                                                                       // the object on top goes with it
        Assert.DoesNotContain(far, gone);
        Assert.Null(w.GetLocatedObjectById(1));
        Assert.Null(w.GetLocatedObjectById(2));
        Assert.Same(far, w.GetLocatedObjectById(3));
        Assert.Equal(PoseState.Dirty, bottom.PoseState);                                                  // no PoseState was written
        Assert.Equal(PoseState.Known, top.PoseState);
        Assert.Contains((1u, PoseState.Dirty, PoseState.Unknown), changes);                               // the broadcast per object, as this stack reports it
        Assert.Contains((2u, PoseState.Known, PoseState.Unknown), changes);
    }

    /// <summary>M11-007: a carried object in the stack is warned about and skipped (0x00507270..0x00507288), the rest still goes.</summary>
    [Fact]
    public void M11_007_ACarriedObjectInTheStackSurvivesMarkObjectUnknown()
    {
        var w = new BlockWorld { IsCarryingObject = id => id == 2 };
        var bottom = Locate(w, Cube(w, 1, At(200, 0, 22), state: PoseState.Dirty));
        var top = Locate(w, Cube(w, 2, At(200, 0, 66)));
        w.MarkObjectUnobserved(bottom); w.MarkObjectUnobserved(bottom);
        Assert.Null(w.GetLocatedObjectById(1));
        Assert.Same(top, w.GetLocatedObjectById(2));
    }

    /// <summary>
    /// M11-007, <c>SetPoseStateHelper</c> 0x0050612C refuses Invalid (0x0050617A: the CantSetInvalidPoseState error, no write); <c>MarkObjectDirty</c> 0x005075A4 sets Dirty
    /// (0x005075B2) and with propagate the object on top within 15 mm, unless carried (0x005076C2).
    /// </summary>
    [Fact]
    public void M11_007_SetPoseStateRefusesInvalidAndDirtyPropagatesUp()
    {
        var w = new BlockWorld { IsCarryingObject = id => id == 12 };
        var a = Locate(w, Cube(w, 10, At(200, 0, 22)));
        var b = Locate(w, Cube(w, 11, At(200, 0, 66)));
        var c = Locate(w, Cube(w, 12, At(200, 0, 110)));
        Assert.False(w.SetPoseStateHelper(a, PoseState.Unknown));
        Assert.Equal(PoseState.Known, a.PoseState);
        w.MarkObjectDirty(a, false);
        Assert.Equal(PoseState.Dirty, a.PoseState);
        Assert.Equal(PoseState.Known, b.PoseState);
        w.MarkObjectDirty(a, true);
        Assert.Equal(PoseState.Dirty, b.PoseState);                                                       // the object on top
        Assert.Equal(PoseState.Known, c.PoseState);                                                       // carried: not marked
    }

    // ------------------------------------------------------------------------------------------- M11-041

    /// <summary>M11-041, <c>AddConnectedActiveObject</c> 0x0062302C: a signed activeID of 5 or more warns and returns -1 (0x00623040); 4 passes; a negative one passes the signed test.</summary>
    [Fact]
    public void M11_041_TheActiveIdRangeIsATestForFiveOrMore()
    {
        var w = new BlockWorld();
        Assert.Equal(-1, w.AddConnectedActiveObject(5, 0xA, ObjectType.Block_LIGHTCUBE1));
        Assert.Equal(-1, w.AddConnectedActiveObject(1000, 0xA, ObjectType.Block_LIGHTCUBE1));
        Assert.Empty(w.ConnectedObjects);
        Assert.NotEqual(-1, w.AddConnectedActiveObject(4, 0xA, ObjectType.Block_LIGHTCUBE1));
        Assert.NotEqual(-1, w.AddConnectedActiveObject(-1, 0xB, ObjectType.Block_LIGHTCUBE2));            // signed compare: negatives pass
        Assert.Equal(2, w.ConnectedObjects.Count);
        // HandleActiveObjectConnectionState's range test is unsigned (cmp r7,#4; bhi 0x00533B58): above 4 never reaches it
        Assert.Equal(-1, w.HandleActiveObjectConnectionState(5, 0xC, ObjectType.Block_LIGHTCUBE3, true));
        Assert.Equal(2, w.ConnectedObjects.Count);
    }

    /// <summary>
    /// M11-041: the slot occupied (0x006230A6..0x0062319E): the same factoryID and type returns the existing object's ID and creates nothing; a different factoryID or type is a
    /// ConflictingActiveID error and <c>RemoveConnectedActiveObject(activeID)</c>, then the new object is registered; a connected object with the same factoryID only logs
    /// (0x006231A2..0x0062328C). The new object is registered with no pose and no PoseState (0x006239D0..0x00623A8C).
    /// </summary>
    [Fact]
    public void M11_041_AnOccupiedSlotIsReusedOrReplaced()
    {
        var w = new BlockWorld();
        var log = new List<string>(); w.Log += log.Add;
        int id = w.AddConnectedActiveObject(1, 0xA, ObjectType.Block_LIGHTCUBE1);
        Assert.Equal(0, id);                                                                              // the first SetID of the process
        var o = Assert.Single(w.ConnectedObjects);
        Assert.Equal(PoseState.Unknown, o.PoseState);                                                     // no PoseState
        Assert.Equal(Pose3d.Identity, o.Pose);                                                            // no pose
        Assert.Equal(1, o.ActiveId); Assert.Equal(0xAu, o.FactoryId);
        Assert.Equal(id, w.AddConnectedActiveObject(1, 0xA, ObjectType.Block_LIGHTCUBE1));                // FoundMatchingObjectAtSameSlot
        Assert.Same(o, Assert.Single(w.ConnectedObjects));
        Assert.Contains(log, l => l.Contains("FoundMatchingObjectAtSameSlot"));
        int replaced = w.AddConnectedActiveObject(1, 0xB, ObjectType.Block_LIGHTCUBE1);                   // another cube in the slot
        Assert.Contains(log, l => l.Contains("ConflictingActiveID"));
        var n = Assert.Single(w.ConnectedObjects);
        Assert.NotSame(o, n);
        Assert.Equal(0xBu, n.FactoryId);
        Assert.Equal(id, replaced);                                                                       // a unique type's ID is stable
        w.AddConnectedActiveObject(2, 0xB, ObjectType.Block_LIGHTCUBE2);                                  // the factoryID is already used
        Assert.Contains(log, l => l.Contains("FactoryIDAlreadyUsed"));
        Assert.Equal(2, w.ConnectedObjects.Count);
        Assert.Equal(-1, w.HandleActiveObjectConnectionState(1, 0xB, ObjectType.Block_LIGHTCUBE1, false));  // RemoveConnectedActiveObject on a disconnection
        Assert.Single(w.ConnectedObjects);
    }

    /// <summary>M11-041, CreateActiveObjectByType (0x00623296) is unread (M11-042): a type other than a light cube throws instead of guessing whether the engine returns null.</summary>
    [Fact]
    public void M11_041_CreateActiveObjectByTypeForOtherTypesIsAVisibleStub()
    {
        var w = new BlockWorld();
        Assert.Throws<NotSupportedException>(() => w.AddConnectedActiveObject(0, 0xA, ObjectType.Charger_Basic));
        Assert.Empty(w.ConnectedObjects);
    }

    /// <summary>
    /// M11-041, located objects with this activeID (0x0062339A..0x00623474, 0x006237E4..0x006238E6): the same factoryID gives the new object that object's ID; a factoryID of 0
    /// adopts it (the given factoryID written to every such object) and takes its ID; any other factoryID is the MismatchedFactoryID error, DeleteLocatedObjects of that ID in the
    /// current origin and a fresh SetID.
    /// </summary>
    [Fact]
    public void M11_041_ALocatedObjectWithTheSlotIsAdoptedOrReplaced()
    {
        // same factory
        var w = new BlockWorld();
        var same = Cube(w, 50, At(150, 0)); same.ActiveId = 2; same.FactoryId = 0xC; w.AddLocatedObject(same);
        Assert.Equal(50, w.AddConnectedActiveObject(2, 0xC, ObjectType.Block_LIGHTCUBE1));
        // never connected (factoryID 0): adopted
        var w2 = new BlockWorld();
        var never = Cube(w2, 51, At(150, 0)); never.ActiveId = 2; never.FactoryId = 0; w2.AddLocatedObject(never);
        Assert.Equal(51, w2.AddConnectedActiveObject(2, 0xD, ObjectType.Block_LIGHTCUBE1));
        Assert.Equal(0xDu, never.FactoryId);
        Assert.Equal(2, never.ActiveId);
        // another factory: deleted, fresh SetID (the unique map's value, not 52)
        var w3 = new BlockWorld();
        var other = Cube(w3, 52, At(150, 0)); other.ActiveId = 2; other.FactoryId = 0x111; w3.AddLocatedObject(other);
        int fresh = w3.AddConnectedActiveObject(2, 0x222, ObjectType.Block_LIGHTCUBE1);
        Assert.NotEqual(52, fresh);
        Assert.Equal(0, fresh);                                                                           // LIGHTCUBE1's first SetID of this test's counter
        Assert.Null(w3.GetLocatedObjectById(52));
    }

    /// <summary>
    /// M11-041, no located object of the type has this activeID (0x00623476..0x00623956): none of the type at all gives a fresh SetID; otherwise each is marked Dirty
    /// (<c>MarkObjectDirty(o,false)</c> 0x00623550) and updated (activeID -1: both set; the same factoryID: the activeID set; otherwise both set when the given factoryID is not 0), and the new
    /// object takes the first one's ID.
    /// </summary>
    [Fact]
    public void M11_041_LocatedObjectsOfTheTypeAreMarkedDirtyAndUpdated()
    {
        var w = new BlockWorld();
        var noSlot = Cube(w, 60, At(150, 0)); w.AddLocatedObject(noSlot);                               // activeID -1
        var sameFactory = Cube(w, 61, At(200, 0)); sameFactory.ActiveId = 3; sameFactory.FactoryId = 0xE; w.AddLocatedObject(sameFactory);
        var otherFactory = Cube(w, 62, At(250, 0)); otherFactory.ActiveId = 4; otherFactory.FactoryId = 0x999; w.AddLocatedObject(otherFactory);
        Assert.Equal(60, w.AddConnectedActiveObject(1, 0xE, ObjectType.Block_LIGHTCUBE1));               // the first of the type's ID
        Assert.All(new[] { noSlot, sameFactory, otherFactory }, o => Assert.Equal(PoseState.Dirty, o.PoseState));
        Assert.Equal(1, noSlot.ActiveId); Assert.Equal(0xEu, noSlot.FactoryId);
        Assert.Equal(1, sameFactory.ActiveId);                                                            // identical factory on a different slot: the activeID moves
        Assert.Equal(1, otherFactory.ActiveId); Assert.Equal(0xEu, otherFactory.FactoryId);              // another cube of the type: both set
        var w2 = new BlockWorld();
        var keep = Cube(w2, 70, At(150, 0)); keep.ActiveId = 3; keep.FactoryId = 0x999; w2.AddLocatedObject(keep);
        w2.AddConnectedActiveObject(1, 0, ObjectType.Block_LIGHTCUBE1);                                   // given factoryID 0: nothing is rewritten
        Assert.Equal(3, keep.ActiveId); Assert.Equal(0x999u, keep.FactoryId);
    }

    // ------------------------------------------------------------------------------------------- M11-043

    /// <summary>
    /// M11-043, <c>UpdateObjectOrigins</c> 0x00620534: the confirmer is cleared first (0x00620594); each object of the old origin is cloned into the new one when it has no counterpart
    /// (the clone takes the ID, the moved pose and the SOURCE's PoseState, 0x00506F02..0x00506F16, and is added with AddLocatedObject); a carried object keeps its own pose
    /// (0x00628E2E..0x00628FB0); the old origin's entries are erased when the last result was 0 (0x00620642..0x0062064C); the second pass gives every object of the current origin an
    /// entry with count 0 (<c>AddInExistingPose</c> 0x00506F48); then BroadcastLocatedObjectStates (0x006206E6) and the BlockConfigurationManager flag (0x006206EA).
    /// </summary>
    [Fact]
    public void M11_043_ObjectsAreClonedIntoTheNewOriginWithTheirStateAndTheOldOriginIsErased()
    {
        var w = new BlockWorld { CurrentOriginId = 1 };
        var dirty = Locate(w, Cube(w, 10, At(100, 20), state: PoseState.Dirty));
        var known = Locate(w, Cube(w, 11, At(150, 0), ObjectType.Block_LIGHTCUBE2, PoseState.Known));
        var carried = Locate(w, Cube(w, 12, At(50, 0, 40), ObjectType.Block_LIGHTCUBE3, PoseState.Known));
        w.IsCarryingObject = id => id == 12;
        int forced = 0; w.BlockConfigurationManagerForceUpdate = () => forced++;
        w.CurrentOriginId = 2;                                                                            // the robot is in the new origin after Rejigger
        // the old origin is at (1000, 0, 0) with respect to the new one
        Assert.Equal(0u, w.UpdateObjectOrigins(1, 2, new Pose3d(Mat3.Identity, new Vec3(1000, 0, 0))));
        Assert.Equal(new uint[] { 2 }, w.FindLocatedMatchingObjects(new BlockWorldFilter { OriginMode = OriginMode.InAnyFrame }).Select(o => o.OriginId).Distinct());   // the old origin is gone
        var d = w.GetLocatedObjectById(10)!;
        Assert.NotSame(dirty, d);                                                                         // a clone
        Assert.Equal(new Vec3(1100, 20, 22), d.Pose.Translation);
        Assert.Equal(PoseState.Dirty, d.PoseState);                                                       // the source's state, not Known and not set Dirty
        Assert.Equal(PoseState.Known, w.GetLocatedObjectById(11)!.PoseState);
        Assert.Equal(new Vec3(1150, 0, 22), w.GetLocatedObjectById(11)!.Pose.Translation);
        Assert.Equal(new Vec3(50, 0, 40), w.GetLocatedObjectById(12)!.Pose.Translation);                  // carried: its own pose
        Assert.Equal(0, d.PoseConfirmationCount);                                                         // the confirmer was cleared; AddInExistingPose makes entries with count 0
        Assert.Equal(d.Pose, d.ReferencePose);                                                            // holding the object's current pose
        Assert.Equal(1, forced);
        Assert.Equal(1, w.UnbuiltBroadcastLocatedObjectStatesCalls);                                      // no message type in this stack: counted
    }

    /// <summary>M11-043: a counterpart of the unique type in the new origin takes the moved object's pose and state (<c>CopyWithNewPose</c> 0x00506EF8) and no clone is made (0x00629066..0x00629256).</summary>
    [Fact]
    public void M11_043_ACounterpartInTheNewOriginTakesThePoseAndTheSourcesState()
    {
        var w = new BlockWorld { CurrentOriginId = 2 };
        var old = Cube(w, 20, At(100, 0), state: PoseState.Dirty); old.OriginId = 1; w.AddLocatedObject(old);
        var counterpart = Cube(w, 20, At(500, 500), state: PoseState.Known); counterpart.OriginId = 2; w.AddLocatedObject(counterpart);
        w.UpdateObjectOrigins(1, 2, new Pose3d(Mat3.Identity, new Vec3(0, 300, 0)));
        Assert.Same(counterpart, w.GetLocatedObjectById(20));
        Assert.Equal(new Vec3(100, 300, 22), counterpart.Pose.Translation);
        Assert.Equal(PoseState.Dirty, counterpart.PoseState);
        Assert.Single(w.FindLocatedMatchingObjects(new BlockWorldFilter { OriginMode = OriginMode.InAnyFrame }));
    }

    /// <summary>M11-043: the non-unique counterpart search (<c>FindLocatedObjectClosestToHelper</c>) is not built and says so.</summary>
    [Fact]
    public void M11_043_TheNonUniqueCounterpartSearchIsAVisibleStub()
    {
        var w = new BlockWorld { CurrentOriginId = 2 };
        var prox = new ObservableObject(30, ObjectType.ProxObstacle, Array.Empty<KnownMarker>()) { Pose = At(100, 0, 25), PoseState = PoseState.Known, OriginId = 1 };
        w.AddLocatedObject(prox);
        Assert.Throws<NotSupportedException>(() => w.UpdateObjectOrigins(1, 2, Pose3d.Identity));
    }

    // ------------------------------------------------------------------------------------------- M11-037

    /// <summary>
    /// M11-037 (rows 3.3, 3.6, 3.8): the EMPTY-list branch sets this+0x90 to 0 and, only when the last image timestamp is not 0, clears the occluders, adds the lift occluder and runs
    /// CheckForUnobservedObjects (0x00625020..0x00625052); either way it falls into the tail: BlockConfigurationManager::Update (0x0062520C) and UpdateMarkerlessObjects
    /// (0x0062521A). It never updates stacked poses.
    /// </summary>
    [Fact]
    public void M11_037_TheEmptyListBranchSkipsEverythingButTheTailWhenTheTimestampIsZero()
    {
        var w = new BlockWorld();
        int hook = 0; w.BlockConfigurationManagerUpdate = () => hook++;
        var o = Locate(w, Cube(w, 1, At(150, 0), state: PoseState.Dirty));
        var pd = Pd(1000);
        var cam = CameraOf(pd);
        cam.Occluders.Add(new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10) }, 100);
        w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(), cam, 0, pd);
        Assert.Equal(1, cam.Occluders.Count);                                                             // ts == 0: the occluders were not touched
        Assert.Equal(0, o.UnobservedCount);                                                               // and CheckForUnobservedObjects did not run
        Assert.Equal(1, hook);                                                                            // the tail still did
        w.UpdateObservedMarkers(Array.Empty<ObservedMarker>(), cam, 1000, pd);
        Assert.Equal(0, cam.Occluders.Count);                                                             // cleared (no lift points yet, so nothing added)
        Assert.Equal(1, o.UnobservedCount);                                                               // a Dirty object with nothing behind it: one miss (0x0062211E)
        Assert.Equal(2, hook);
    }

    /// <summary>
    /// M11-037 (3C.1): CheckForUnobservedObjects has no effect when the robot is not on its treads (Robot+0x355), was moving, or was rotating too fast (0x00621C7C..0x00621CFA);
    /// candidates exclude carried objects, the docking object and family 4 (0x00621D54..0x00621E80); an object with no markers is skipped silently.
    /// </summary>
    [Fact]
    public void M11_037_CheckForUnobservedObjectsHasItsEarlyReturnsAndCandidateExclusions()
    {
        var pd = Pd(1000);
        var cam = CameraOf(pd);
        BlockWorld Make(out ObservableObject o) { var w = new BlockWorld(); o = Locate(w, Cube(w, 1, At(150, 0), state: PoseState.Dirty)); return w; }
        var w1 = Make(out var o1);
        w1.OnTreads = () => false;
        w1.CheckForUnobservedObjects(cam, 1000, false, false);
        Assert.Equal(0, o1.UnobservedCount);
        var w2 = Make(out var o2);
        w2.CheckForUnobservedObjects(cam, 1000, robotMoving: true, rotatingTooFast: false);
        w2.CheckForUnobservedObjects(cam, 1000, robotMoving: false, rotatingTooFast: true);
        Assert.Equal(0, o2.UnobservedCount);
        var w3 = Make(out var o3);
        w3.IsCarryingObject = id => id == 1;
        w3.CheckForUnobservedObjects(cam, 1000, false, false);
        Assert.Equal(0, o3.UnobservedCount);
        var w4 = Make(out var o4);
        w4.DockingObjectId = () => 1;
        w4.CheckForUnobservedObjects(cam, 1000, false, false);
        Assert.Equal(0, o4.UnobservedCount);
        var w5 = new BlockWorld();
        var charger = Locate(w5, new ObservableObject(9, ObjectType.Charger_Basic, ChargerGeometry.Markers) { Pose = At(150, 0, 0), PoseState = PoseState.Dirty, OriginId = 1 });
        w5.CheckForUnobservedObjects(cam, 1000, false, false);
        Assert.Equal(0, charger.UnobservedCount);                                                         // family 4 is never a candidate
        var w6 = Make(out var o6);
        w6.CheckForUnobservedObjects(cam, 1000, false, false);
        Assert.Equal(1, o6.UnobservedCount);                                                              // the control: an ordinary Dirty cube is
        w6.CheckForUnobservedObjects(cam, 1000, false, false);
        Assert.Null(w6.GetLocatedObjectById(1));                                                          // and the second miss deletes it
    }

    /// <summary>
    /// M11-037 (5.1..5.7), <c>UpdatePoseOfStackedObjects</c> 0x00621794: for each PoseChange whose object is still located, a clone at the OLD pose finds the object on top within 15.0 that was not
    /// observed in the last 100 ms (0x00629BAE), is not carried and is not itself in the list; its new pose is its pose with respect to the old pose pre-composed with the object's new pose,
    /// written by <c>AddObjectRelativeObservation</c> as Dirty with fromDistance -1.0 (0x006219C0, 0x00506D3C..0x00506D3E). PoseChanges are appended only while this+0x74 is set (0x00624814).
    /// </summary>
    [Fact]
    public void M11_037_AnObjectOnTopFollowsTheObjectItRestsOn()
    {
        BlockWorld Make(out ObservableObject bottom, out ObservableObject top, uint topSeen = 0)
        {
            var w = new BlockWorld();
            bottom = Locate(w, Cube(w, 1, At(200, 0, 22)));
            top = Locate(w, Cube(w, 2, At(200, 0, 66)));
            top.LastObservedTimestamp = topSeen;
            return w;
        }
        void StartFrame(BlockWorld w) => Field<bool>(w, "_appendPoseChanges", true);
        void Moved(BlockWorld w, ObservableObject o, Pose3d newPose)
        {
            var old = o.Pose; var oldState = o.PoseState;
            o.SetPose(newPose, -1.0, PoseState.Known);
            w.BroadcastObjectPoseChanged(o, old, oldState);
        }
        var w1 = Make(out var b1, out var t1);
        Field<uint>(w1, "_lastImageTimestamp", 1000u);
        // outside a frame nothing is recorded (this+0x74 is clear)
        Moved(w1, b1, At(250, 50, 22, 0.5));
        w1.UpdatePoseOfStackedObjects();
        Assert.Equal(new Vec3(200, 0, 66), t1.Pose.Translation);
        // inside a frame the change is recorded and the top follows: its offset in the bottom's frame (0,0,44) is kept, turned by the bottom's new yaw
        var w2 = Make(out var b2, out var t2);
        Field<uint>(w2, "_lastImageTimestamp", 1000u);
        StartFrame(w2);
        Moved(w2, b2, At(250, 50, 22, 0.5));
        w2.UpdatePoseOfStackedObjects();
        Assert.Equal(new Vec3(250, 50, 66), t2.Pose.Translation);
        Assert.Equal(0.5, t2.Pose.AngleAroundZ, 6);
        Assert.Equal(PoseState.Dirty, t2.PoseState);
        Assert.Equal(-1.0, t2.FromDistance);
        // the top was observed within the last 100 ms: not a candidate
        var w3 = Make(out var b3, out var t3, topSeen: 950);
        Field<uint>(w3, "_lastImageTimestamp", 1000u); StartFrame(w3);
        Moved(w3, b3, At(250, 50, 22, 0.5));
        w3.UpdatePoseOfStackedObjects();
        Assert.Equal(new Vec3(200, 0, 66), t3.Pose.Translation);
        // carried: skipped
        var w4 = Make(out var b4, out var t4);
        w4.IsCarryingObject = id => id == 2;
        Field<uint>(w4, "_lastImageTimestamp", 1000u); StartFrame(w4);
        Moved(w4, b4, At(250, 50, 22, 0.5));
        w4.UpdatePoseOfStackedObjects();
        Assert.Equal(new Vec3(200, 0, 66), t4.Pose.Translation);
        // its own ID in the list: skipped, and it keeps the pose it was given
        var w5 = Make(out var b5, out var t5);
        Field<uint>(w5, "_lastImageTimestamp", 1000u); StartFrame(w5);
        Moved(w5, b5, At(250, 50, 22, 0.5));
        Moved(w5, t5, At(400, 0, 66));
        w5.UpdatePoseOfStackedObjects();
        Assert.Equal(new Vec3(400, 0, 66), t5.Pose.Translation);
        // a change to an object that has been deleted: ignored
        var w6 = Make(out var b6, out var t6);
        Field<uint>(w6, "_lastImageTimestamp", 1000u); StartFrame(w6);
        Moved(w6, b6, At(250, 50, 22, 0.5));
        var del = new BlockWorldFilter(); del.AllowedIds.Add(1);
        w6.DeleteLocatedObjects(del);
        w6.UpdatePoseOfStackedObjects();
        Assert.Equal(new Vec3(200, 0, 66), t6.Pose.Translation);
    }

    /// <summary>
    /// M11-037 (5.8), <c>UpdateMarkerlessObjects</c> 0x00625704 deletes objects of family 6 and type 15 (CliffDetection) whose predicate 0x0062B764 is true: timestamp + 30000 &lt; ts is
    /// "RemovingExpired"; else false when the timestamp is not before ts; else true when z is within [robotZ, robotZ + 67.7] and the footprint intersects the robot's quad
    /// (the footprint is unread, M13-023: the stack throws).
    /// </summary>
    [Fact]
    public void M11_037_ExpiredCliffObjectsAreDeletedAndTheFootprintTestIsAVisibleStub()
    {
        var w = new BlockWorld();
        var cliff = w.AddMarkerlessObject(At(300, 0, 0), ObjectType.CliffDetection);
        var collision = w.AddMarkerlessObject(At(300, 100, 0), ObjectType.CollisionObstacle);
        Assert.Equal(ObjectFamily.MarkerlessObject, cliff.Family);
        cliff.LastObservedTimestamp = 5000;
        collision.LastObservedTimestamp = 5000;
        var robot = Pose3d.Identity;
        // 5000 + 30000 is not before 35000, the object is older than ts and its z (25) is inside [robotZ, robotZ + 67.7]: the footprint test is reached and is not built
        Assert.Throws<NotSupportedException>(() => w.UpdateMarkerlessObjects(35000, robot));
        Assert.NotNull(w.GetObjectById(cliff.ObjectId));
        w.UpdateMarkerlessObjects(35001, robot);                                                          // 5000 + 30000 < 35001: expired, no geometry needed
        Assert.Null(w.GetObjectById(cliff.ObjectId));
        Assert.NotNull(w.GetObjectById(collision.ObjectId));                                              // other types are never touched
        // not older than ts: kept without any geometry
        var w2 = new BlockWorld();
        var fresh = w2.AddMarkerlessObject(At(300, 0, 0), ObjectType.CliffDetection);
        fresh.LastObservedTimestamp = 40000;
        w2.UpdateMarkerlessObjects(40000, robot);
        Assert.NotNull(w2.GetObjectById(fresh.ObjectId));
        // z outside [robotZ, robotZ + 67.7]: kept
        var w3 = new BlockWorld();
        var high = w3.AddMarkerlessObject(At(300, 0, 200), ObjectType.CliffDetection);
        high.LastObservedTimestamp = 39000;
        w3.UpdateMarkerlessObjects(40000, robot);
        Assert.NotNull(w3.GetObjectById(high.ObjectId));
    }

    /// <summary>
    /// M11-037 (Q8, C-R9): the occluder points are written only by <c>VisionComponent::SetPhysicalRobot</c> 0x00657F3C: (4,-15,-20), (4,15,-20), (-1,-15,-20), (-1,15,-20) and the same four at
    /// z = s, s = -36.5 for a non-zero argument (0xC2120000) and -28.5 for zero (0xC1E40000). They start empty (0x006500CA/0x006500D0).
    /// </summary>
    [Fact]
    public void M11_037_TheLiftOccluderPointsComeFromSetPhysicalRobot()
    {
        var w = new BlockWorld();
        Assert.Empty(w.LiftOccluderPoints);
        w.SetPhysicalRobot(true);
        Assert.Equal(new[]
        {
            new Vec3(4, -15, -20), new Vec3(4, 15, -20), new Vec3(-1, -15, -20), new Vec3(-1, 15, -20),
            new Vec3(4, -15, -36.5), new Vec3(4, 15, -36.5), new Vec3(-1, -15, -36.5), new Vec3(-1, 15, -36.5),
        }, w.LiftOccluderPoints);
        w.SetPhysicalRobot(false);
        Assert.All(w.LiftOccluderPoints.Skip(4), p => Assert.Equal(-28.5, p.Z));
    }

    /// <summary>
    /// M11-037 (Q8, 8.1): the float <c>AddLiftOccluder</c> passes to <c>Camera::AddOccluder</c> is the LENGTH of the translation of the lift transform wrt the camera:
    /// sqrt of the sum of squares of the three floats at Transform3d+0x20..0x28 (0x00656554..0x00656598), not a z scale. Here the lift frame is 200 mm ahead of the camera on its axis, so the
    /// depth is 200.
    /// </summary>
    [Fact]
    public void M11_037_TheLiftOccluderDepthIsTheLengthOfTheLiftTransformsTranslation()
    {
        var w = new BlockWorld();
        w.SetPhysicalRobot(true);
        // a robot at the origin facing +X with the lift raised to its lowest; the camera is put 200 mm behind the lift's origin looking along +X (camera X = world -Y, Y = -Z, Z = +X)
        var robot = Pose3d.Identity;
        var liftWorld = robot.Compose(LiftGeometry.LiftPoseInRobotFrame(0));
        var camPose = new Pose3d(new Mat3(0, 0, 1, -1, 0, 0, 0, -1, 0), liftWorld.Translation - new Vec3(200, 0, 0));
        var cam = new CameraModel(Cal, camPose);
        w.AddLiftOccluder(cam, new VisionPoseData(1000, robot, 0, 0, false, false));
        var occluder = Assert.Single(cam.Occluders.Entries);
        Assert.Equal(200.0, occluder.DepthMm, 6);
        Assert.Equal((camPose.Translation - liftWorld.Translation).Length, occluder.DepthMm, 6);
    }

    /// <summary>M11-037: <c>Robot::SetPhysicalRobot</c> passes its argument to <c>VisionComponent::SetPhysicalRobot</c> (0x0051397C); the vision system hears it from the engine.</summary>
    [Fact]
    public void M11_037_TheEngineRaisesSetPhysicalRobotToTheWorld()
    {
        using var robot = CozmoRobot.CreateOffline();
        using var vision = new VisionSystem(robot, Cal);
        Assert.Empty(vision.World.LiftOccluderPoints);
        robot.Engine.RaisePhysicalRobotSet(false);
        Assert.Equal(-28.5, vision.World.LiftOccluderPoints[7].Z);
        robot.Engine.RaisePhysicalRobotSet(true);
        Assert.Equal(-36.5, vision.World.LiftOccluderPoints[7].Z);
    }

    /// <summary>M11-004 (H2, 0x00642802): <c>WasCameraMoving</c> is <c>(status &amp; 0x8200) != 0x200</c> on the state nearest the timestamp.</summary>
    [Theory]
    [InlineData(0x0000u, true)]
    [InlineData(0x0200u, false)]
    [InlineData(0x8200u, true)]
    [InlineData(0x0201u, false)]     // IS_MOVING alone does not make the camera move: only bits 9 and 15 count
    [InlineData(0x0100u, true)]
    public void M11_004_TheCameraIsMovingUnlessTheHeadIsInPositionAndTheWheelsAreStill(uint status, bool expected)
    {
        var h = new RobotStateHistory();
        h.Add(new RobotState { Timestamp = 100, PoseOriginId = 1, Status = status, Accel = new AccelData { Z = 9800 }, Gyro = new GyroData() });
        Assert.Equal(expected, h.At(100)!.Value.CameraMoving);
    }

    // ------------------------------------------------------------------------------------------- M11-049, M11-040

    private static CameraFrame BadFrame(uint id) => new() { ImageId = id, Timestamp = id, Width = 320, Height = 240, Encoding = 8 };

    /// <summary>
    /// M11-049: <c>VisionComponent+0x48</c> (enabled) has one writer, the NV calibration-read callback, on every path of it (0x0065AE80): a failed read, a size mismatch and a success. Images are
    /// dropped until it has run (<c>SetNextImage</c> 0x00652B20..0x00652BEA). The pause byte +0x4B has no writer: there is no paused state.
    /// </summary>
    [Theory]
    [InlineData(1, 0)]                                          // NVResult != 0
    [InlineData(0, 3)]                                          // a size that is not the calibration's
    [InlineData(0, -1)]                                         // the right size
    public void M11_049_NoImageIsProcessedUntilTheCalibrationReadCallbackHasRunOnAnyOutcome(int nvResult, int size)
    {
        using var robot = CozmoRobot.CreateOffline();
        using var vision = new VisionSystem(robot);
        var log = new List<string>(); vision.Log += l => { lock (log) log.Add(l); };
        Assert.False(vision.Enabled);
        vision.HandOverFrame(BadFrame(1));
        vision.WaitForProcessorIdle();
        lock (log) Assert.Empty(log);                           // dropped: nothing processed, not even a decode attempt
        var callback = typeof(CameraSettings).GetMethod("OnCalibrationRead", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var data = new byte[size < 0 ? CameraSettings.CalibrationBytes : size];
        callback.Invoke(robot.CameraSettings, new object[] { new NvResult((sbyte)nvResult, data) });
        Assert.True(vision.Enabled);
        vision.HandOverFrame(BadFrame(2));
        // the frame is taken now. With a calibration installed (the success outcome) it is decoded, and the empty JPEG says so; without one (the two failure outcomes) the engine's
        // VisionSystem::Update refuses with the NotReady warning (M11-012, 0x006B4D66..0x006B4FFC)
        string expected = vision.Calibration is null ? "Must be initialized and have calibrated camera" : "frame 2";
        vision.WaitForProcessorIdle();
        lock (log) Assert.True(log.Any(l => l.Contains(expected)), string.Join(" | ", log));
        Assert.Null(typeof(VisionSystem).GetProperty("Paused"));
        Assert.Null(typeof(VisionSystem).GetProperty("IsPaused"));
    }

    /// <summary>
    /// M11-040: one pending slot, latest wins, discard on completion (SetNextImage 0x00653052..0x00653152, Processor 0x00651F08..0x00652262): while frame 1 is being processed, frames 2, 3 and 4
    /// arrive; 3 replaces 2 and 4 replaces 3 (each replacement is a counted dropped frame); when 1 finishes the pending slot is discarded without processing 4 and without counting it; a later
    /// frame is processed. The poll interval is 2 ms (0x001E8480).
    /// </summary>
    [Fact]
    public void M11_040_TheMailboxHasOnePendingSlotLatestWinsAndDiscardsOnCompletion()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(2), VisionSystem.ProcessorPollInterval);
        using var robot = CozmoRobot.CreateOffline();
        using var vision = new VisionSystem(robot, Cal) { Enabled = true };
        var seen = new List<string>();
        var inFrameOne = new ManualResetEventSlim(); var release = new ManualResetEventSlim();
        vision.Log += l =>
        {
            if (!l.StartsWith("frame ")) return;
            lock (seen) seen.Add(l);
            if (l.StartsWith("frame 1:")) { inFrameOne.Set(); release.Wait(); }
        };
        vision.HandOverFrame(BadFrame(1));
        Assert.True(inFrameOne.Wait(3000));
        vision.HandOverFrame(BadFrame(2));
        Assert.Equal(0, vision.FramesDropped);                  // the slot was empty (frame 1 was taken out of it)
        vision.HandOverFrame(BadFrame(3));
        vision.HandOverFrame(BadFrame(4));
        Assert.Equal(2, vision.FramesDropped);                  // 2 and 3 were replaced
        release.Set();
        vision.WaitForProcessorIdle();                                      // several polls
        lock (seen) Assert.DoesNotContain(seen, l => l.StartsWith("frame 4:") || l.StartsWith("frame 3:") || l.StartsWith("frame 2:"));
        Assert.Equal(2, vision.FramesDropped);                  // the discard is not counted
        vision.HandOverFrame(BadFrame(5));
        vision.WaitForProcessorIdle();
        lock (seen) Assert.True(seen.Any(l => l.StartsWith("frame 5:")));
    }

    /// <summary>
    /// M11-012: the live path never substitutes a nominal calibration. <c>VisionSystem::Update(PoseData, ImageCache)</c> tests the init flag (+0x58) and the camera's calibration
    /// pointer (+0x64) and returns 1 with the warning "Must be initialized and have calibrated camera to Update" (0x006B4D66, 0x006B4D70, 0x006B4FFC, 0x006B5022); nothing is processed and
    /// no result is pushed. So a system without a calibration returns null, warns (once) and stays without one.
    /// </summary>
    [Fact]
    public void M11_012_WithoutACalibrationTheVisionSystemRefusesAndDoesNotSubstituteOne()
    {
        using var robot = CozmoRobot.CreateOffline();
        using var vision = new VisionSystem(robot);
        var log = new List<string>(); vision.Log += log.Add;
        var image = new GrayImage(320, 240);
        image.Fill(120);
        Assert.Null(vision.ProcessCapture(image, 1, 1000));
        Assert.Null(vision.ProcessCapture(image, 2, 1033));
        Assert.Null(vision.Calibration);
        Assert.Equal(1, log.Count(l => l.Contains("Must be initialized and have calibrated camera to Update")));
        Assert.Equal(0, vision.FramesProcessed);
    }

    // ------------------------------------------------------------------------------------------- M11-044 round 3: the robot-side localization writes

    private static RobotState OffTreadsState(uint t, float ax = 0, float az = 9800, float pitch = 0, RobotStatusFlag flags = 0) =>
        new() { Timestamp = t, Status = (uint)flags, Accel = new AccelData { X = ax, Z = az }, Gyro = new GyroData(), Pose = new RobotPose { Pitch = pitch } };

    /// <summary>Drives a classifier through OnBack and back to OnTreads (the sequence of DerivedStateTests.RightingFromTheBackGoesThroughInAirOnlyWhileHeld) and returns after the commit to OnTreads.</summary>
    private static void CommitOnBackThenOnTreads(OffTreadsClassifier c)
    {
        c.HeadCalibrated = true; c.IsPhysical = true;
        uint t = 0;
        for (int i = 0; i < 120; i++, t += 33) c.Update(OffTreadsState(t), t);
        float back = (float)OffTreadsClassifier.OnBackCentrePhysicalRad;
        for (uint dt = 0; dt <= 1100; dt += 33) c.Update(OffTreadsState(t + dt, ax: 9800, az: 0, pitch: back, flags: RobotStatusFlag.IsPickedUp), t + dt);
        Assert.Equal(Cozmo.Robot.OffTreadsState.OnBack, c.Current);
        t += 1133;
        c.Update(OffTreadsState(t + 297), t + 297);
        Assert.Equal(Cozmo.Robot.OffTreadsState.OnTreads, c.Current);
    }

    /// <summary>
    /// M11-044 (b) Robot::Delocalize 0x00510A24: Robot+0x2B8 = -1 (0x00510A46), +0x2C4 = 0, +0x2C8 = -1.0f, +0x2C5 = 0; +0x2BC is not touched and the state history is not cleared.
    /// </summary>
    [Fact]
    public void M11_044_DelocalizeWritesTheRobotFields()
    {
        var w = new BlockWorld();
        var o = Cube(w, 42, At(150, 0));
        w.SetLocalizedTo(o);
        w.NoteRobotState(true, false);
        Assert.Equal((byte)1, w.Robot2C4);
        w.OnRobotDelocalized();
        Assert.Null(w.LocalizedToObjectId);
        Assert.Equal((byte)0, w.Robot2C4);
        Assert.Equal((byte)0, w.Robot2C5);
        Assert.Equal(-1.0f, w.Robot2C8);
        Assert.True(w.RobotMovedSinceLocalized);                                      // +0x2BC untouched
    }

    /// <summary>
    /// M11-044 (g) (AddRawOdomState 0x00530CF8 does not clear the computed map; Delocalize does not call the history's Clear): a state that reports a new origin id does not clear the computed
    /// states. (A new origin id is NOT the engine's Delocalize trigger; the tread-boundary test below is.)
    /// </summary>
    [Fact]
    public void M11_044_ANewOriginInTheStateStreamKeepsTheComputedStates()
    {
        using var rig = new ImageRig();
        rig.Send(Raw(2000, origin: 1));
        rig.Send(Raw(2033, origin: 1));
        Assert.True(rig.Vision.History.ComputeAndInsertStateAt(2033));
        rig.Send(Raw(2066, origin: 2));
        Assert.NotNull(rig.Vision.History.GetComputedStateAt(2033));
    }

    /// <summary>
    /// M11-044 (b) (0x00512A62..0x00512A94, 0x00512B88..0x00512BA6, 0x00512FB4): a treads commit that involves OnTreads (old state OnTreads gives r7 = 1; otherwise r7 = the new state is
    /// OnTreads; r7 = 0 without a commit) sets [+0x2C0] = 0, calls Delocalize (Robot+0x2B8 = -1, +0x2C4 = 0) and skips the history step. A state without a commit does none of it.
    /// </summary>
    [Fact]
    public void M11_044_ATreadsCommitInvolvingOnTreadsDelocalizesAndSkipsTheHistory()
    {
        using var rig = new ImageRig();
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false, AutoStarted = false });
        rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_LIFT, CalibStarted = false, AutoStarted = false });
        var w = rig.Vision.World;
        var o = Cube(w, 42, At(150, 0));
        rig.Send(Raw(2000, origin: 0));                                                // no commit: nothing happens
        w.SetLocalizedTo(o);
        rig.Send(Raw(2033, origin: 0));
        Assert.Equal(42u, w.LocalizedToObjectId);
        int count = rig.Vision.History.Count;
        var picked = Raw(2066, origin: 0); picked.Status = (uint)RobotStatusFlag.IsPickedUp;
        rig.Send(picked);                                                              // OnTreads -> InAir: the old state is OnTreads, r7 = 1
        Assert.Equal(Cozmo.Robot.OffTreadsState.InAir, rig.Robot.Sensors.OffTreadsState);
        Assert.Null(w.LocalizedToObjectId);
        Assert.Equal((byte)0, w.Robot2C4);
        Assert.Equal((byte)0, w.Robot2C0);
        Assert.Equal(count, rig.Vision.History.Count);                                 // the history step was skipped
        w.SetLocalizedTo(o);
        rig.Send(Raw(2099, origin: 0));                                                // InAir -> OnTreads: the new state is OnTreads, r7 = 1
        Assert.Equal(Cozmo.Robot.OffTreadsState.OnTreads, rig.Robot.Sensors.OffTreadsState);
        Assert.Null(w.LocalizedToObjectId);
        Assert.Equal(count, rig.Vision.History.Count);
        w.SetLocalizedTo(o);
        rig.Send(Raw(2132, origin: 0));                                                // no commit
        Assert.Equal(42u, w.LocalizedToObjectId);
        Assert.Equal(count + 1, rig.Vision.History.Count);
    }

    /// <summary>
    /// M11-044 (a) (0x00626FAC mode byte 3, 0x00626FB0 no origin added, 0x00626FD2, 0x0062704C): AnyRemainingLocalizableObjects looks in EVERY origin, so a usable cube located in another
    /// origin keeps the OnTreads commit from writing Robot+0x2C4 and Robot+0x2B8; with no usable cube in any origin the writes run.
    /// </summary>
    [Fact]
    public void M11_044_AUsableCubeInAnotherOriginStopsTheOnTreadsWrites()
    {
        var w = new BlockWorld();
        var other = Cube(w, 7, At(150, 0)); other.OriginId = 5; other.ActiveId = 1; other.FromDistance = 100;
        w.AddLocatedObject(other);
        Assert.NotEqual(other.OriginId, w.CurrentOriginId);
        Assert.True(w.AnyRemainingLocalizableObjects());
        var c = new OffTreadsClassifier { AnyRemainingLocalizableObjects = w.AnyRemainingLocalizableObjects };
        CommitOnBackThenOnTreads(c);
        Assert.Equal(0, c.Robot2B8);
        Assert.Equal((byte)0, c.Robot2C4);
        other.PoseState = PoseState.Dirty;                                             // no longer usable, still located
        Assert.False(w.AnyRemainingLocalizableObjects());
        var c2 = new OffTreadsClassifier { AnyRemainingLocalizableObjects = w.AnyRemainingLocalizableObjects };
        CommitOnBackThenOnTreads(c2);
        Assert.Equal(-1, c2.Robot2B8);
        Assert.Equal((byte)1, c2.Robot2C4);
    }

    /// <summary>
    /// M11-044 (a), CheckAndUpdateTreadsState 0x005120F6..0x00512188: on the commit to OnTreads BlockWorld::AnyRemainingLocalizableObjects (0x00626EF4) is called; non-zero SKIPS the log
    /// and both stores (0x00512112..0x0051211A cbnz); zero runs the log, Robot+0x2C4 = 1 (0x0051217C) and Robot+0x2B8 = -1 (0x00512184).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void M11_044_OnTreadsWritesTheRobotFieldsOnlyWhenNoLocalizableObjectRemains(bool anyRemain)
    {
        var c = new OffTreadsClassifier();
        var log = new List<string>(); c.Log += log.Add;
        int nothingRemains = 0;
        c.AnyRemainingLocalizableObjects = () => anyRemain;
        c.NothingLocalizableRemainsOnTreads = () => nothingRemains++;
        CommitOnBackThenOnTreads(c);
        Assert.Equal(anyRemain ? 0 : 1, (int)c.Robot2C4);
        Assert.Equal(anyRemain ? 0 : -1, c.Robot2B8);
        Assert.Equal(anyRemain ? 0 : 1, nothingRemains);
        Assert.Equal(anyRemain ? 0 : 1, log.Count(l => l.Contains("NoMoreRemainingLocalizableObjects")));
    }

    /// <summary>
    /// M11-044 (a) through the vision system: the world's own copy of Robot+0x2B8 survives the commit to OnTreads while a cube that can be used for localization is still located
    /// (0x00512188 skips the writes) and is cleared, with Robot+0x2C4 = 1, when none is left. The predicate is CanBeUsedForLocalization over every origin (0x00626FAC..0x0062704C); here the cube is usable, then Dirty.
    /// </summary>
    [Fact]
    public void M11_044_TheWorldsLocalizationSurvivesOnTreadsOnlyWhileAUsableCubeRemains()
    {
        foreach (bool usableRemains in new[] { true, false })
        {
            using var robot = CozmoRobot.CreateOffline();
            using var vision = new VisionSystem(robot, Cal);
            var m = Locate(vision.World, Cube(vision.World, 1, At(150, 0)));
            m.ActiveId = 1; m.FromDistance = 100;
            vision.World.SetLocalizedTo(m);
            if (!usableRemains) m.PoseState = PoseState.Dirty;
            CommitOnBackThenOnTreads(robot.Sensors.OffTreads);
            Assert.Equal(usableRemains ? 1u : (uint?)null, vision.World.LocalizedToObjectId);
        }
    }

    /// <summary>
    /// M11-044 (c), WritePoseHelper 0x0050609C with newState 2 (0x005060AC..0x005060C4): when [[Robot]+0x2B8] is the object's ID the write calls SetLocalizedTo(nullptr) first. A cube the
    /// robot is localized to that is refreshed as Dirty (here a moving cube's discarded sighting, 0x00505E36) drops the localization; another cube's write does not.
    /// </summary>
    [Fact]
    public void M11_044_AWriteThatMakesTheLocalizedCubeDirtyClearsTheLocalization()
    {
        var (w, m, _) = UsableRig();
        w.SetLocalizedTo(m);
        var other = Locate(w, Cube(w, 2, At(150, 50), ObjectType.Block_LIGHTCUBE2));
        w.SetMoving(other.ObjectId, true);
        var p = new PotentialObjectsForLocalizingTo(w);
        p.Insert(Seen(other, At(150, 50)), other, 100f, false, false);                 // refreshed as Dirty: another cube
        Assert.Equal(PoseState.Dirty, other.PoseState);
        Assert.Equal(m.ObjectId, w.LocalizedToObjectId);
        w.SetMoving(m.ObjectId, true);
        p.Insert(Seen(m, At(150, 0)), m, 100f, false, false);                          // refreshed as Dirty: the localized cube
        Assert.Equal(PoseState.Dirty, m.PoseState);
        Assert.Null(w.LocalizedToObjectId);
        Assert.Equal((byte)1, w.Robot2C4);                                                   // SetLocalizedTo(null) writes +0x2C4 = 1
    }

    /// <summary>
    /// M11-044 (c), SetPoseStateHelper 0x0050612C (0x0050613C..0x0050614A): the same clearing when a Dirty state is set directly (a Moved report gives MarkObjectDirty).
    /// </summary>
    [Fact]
    public void M11_044_MarkingTheLocalizedCubeDirtyClearsTheLocalizationAndMarkingAnotherDoesNot()
    {
        var (w, m, _) = UsableRig();
        var other = Locate(w, Cube(w, 2, At(150, 50), ObjectType.Block_LIGHTCUBE2));
        w.SetLocalizedTo(m);
        w.MarkObjectDirty(other, false);
        Assert.Equal(m.ObjectId, w.LocalizedToObjectId);
        w.MarkObjectDirty(m, false);
        Assert.Null(w.LocalizedToObjectId);
        Assert.Equal(PoseState.Dirty, m.PoseState);
    }

    /// <summary>
    /// M11-044 (d), SetLocalizedTo 0x0051238C: an object whose ID is -1 is the IdNotSet error (0x005123A0 -> 0x005124FC), return 1, no writes; a failed marker call (0x005123FE ->
    /// 0x0051254E) is an error, return 1, NO writes (+0x2B8, +0x2BC, +0x2C4 unchanged); LocalizeToObject and LocalizeRobot propagate the failure. Success writes +0x2B8 = ID, +0x2BC = 0 and
    /// +0x2C4 = 1; null writes +0x2B8 = -1 and +0x2C4 = 1 and leaves +0x2BC. An unwired marker loop counts (visible stand-in).
    /// </summary>
    [Fact]
    public void M11_044_SetLocalizedToCanFailWithoutWritingAndThePropagatesTheFailure()
    {
        var (w, m, p) = UsableRig();
        w.NoteRobotState(true, false);
        Assert.False(w.SetLocalizedTo(new ObservableObject(ObjectType.Block_LIGHTCUBE1, Array.Empty<KnownMarker>())));    // ID -1
        Assert.Null(w.LocalizedToObjectId); Assert.True(w.RobotMovedSinceLocalized); Assert.Equal((byte)0, w.Robot2C4);
        w.MarkerPosesRelativeToCamera = _ => false;
        Assert.False(w.SetLocalizedTo(m));
        Assert.Null(w.LocalizedToObjectId); Assert.True(w.RobotMovedSinceLocalized); Assert.Equal((byte)0, w.Robot2C4);
        Assert.Equal(1u, w.LocalizeToObject(Seen(m, At(150, 0)), m));                  // propagated
        p.Insert(Seen(m, At(150, 0)), m, 100f, false, false);
        Assert.Equal(1u, p.LocalizeRobot());                                           // LocalizeRobot returns LocalizeToObject's failure
        Assert.Null(w.LocalizedToObjectId);
        w.MarkerPosesRelativeToCamera = _ => true;
        Assert.True(w.SetLocalizedTo(m));
        Assert.Equal(m.ObjectId, w.LocalizedToObjectId); Assert.False(w.RobotMovedSinceLocalized); Assert.Equal((byte)1, w.Robot2C4);
        Assert.Equal(0, w.UnbuiltSetLocalizedToMarkerLoops);                           // wired: not counted
        w.MarkerPosesRelativeToCamera = null;
        w.SetLocalizedTo(m);
        Assert.Equal(1, w.UnbuiltSetLocalizedToMarkerLoops);                           // unwired: visible
    }

    /// <summary>
    /// M11-044 (e), UpdateFullRobotState 0x00512B8E (the OR into Robot+0x2BC) runs BEFORE the origin lookup at 0x00512C54: a state whose origin the robot rejects still ORs 1 in, while the
    /// history does not take it. A state dropped before time sync never gets that far.
    /// </summary>
    [Fact]
    public void M11_044_TheMovementOrRunsForAStateWhoseOriginIsRejected()
    {
        using var rig = new ImageRig();
        var er = rig.Robot.Engine.Robot!;
        er.OfflineSeamAcceptsAnyOrigin = false;
        var m = Locate(rig.Vision.World, Cube(rig.Vision.World, 1, At(150, 0)));
        rig.Vision.World.SetLocalizedTo(m);
        Assert.False(rig.Vision.World.RobotMovedSinceLocalized);
        int before = rig.Vision.History.Count;
        var moving = Raw(5000, origin: 99); moving.Status = (uint)RobotStatusFlag.AreWheelsMoving;
        rig.Send(moving);
        Assert.Equal(before, rig.Vision.History.Count);                                // the origin was rejected: not in the history
        Assert.True(rig.Vision.World.RobotMovedSinceLocalized);                        // but the OR ran
        rig.Vision.World.SetLocalizedTo(m);
        er.TimeSynced = false;
        var moving2 = Raw(5033, origin: 99); moving2.Status = (uint)RobotStatusFlag.AreWheelsMoving;
        rig.Send(moving2);
        Assert.False(rig.Vision.World.RobotMovedSinceLocalized);                       // dropped before the sync gate: no OR
    }

    /// <summary>
    /// M11-044 (f), UpdateVisionMarkers 0x00654D96..0x00654DE6: a non-zero result of ComputeAndInsertStateAt (no raw state at or after the frame's timestamp, 0x0053146A..0x00531472) jumps to
    /// 0x0065507E: UpdateObservedMarkers is never reached (no object is created or updated) and no computed state is inserted. With a raw state at the timestamp the same frame is processed.
    /// </summary>
    [Fact]
    public void M11_044_AFrameWithoutARawStateAtOrAfterItsTimestampNeverReachesUpdateObservedMarkers()
    {
        if (!NeedsLibrary()) return;
        using var rig = new ImageRig();
        rig.Send(new ObjectConnectionState { ObjectID = 2, FactoryID = 0xBEEF, ObjectType = ObjectType.Block_LIGHTCUBE1, Connected = true });
        rig.Send(Raw(3000));
        var pd = rig.Vision.History.At(3000)!.Value;
        var frame = new GrayImage(Cal.Columns, Cal.Rows);
        frame.Fill(150);
        MarkerRenderer.DrawObject(frame, Lib!, new CameraModel(Cal, pd.CameraPose), ObjectType.Block_LIGHTCUBE1, At(150, 0));
        var log = new List<string>(); rig.Vision.Log += log.Add;
        var late = rig.Vision.ProcessCapture(frame, 1, 3033);                          // nothing at or after 3033
        Assert.NotNull(late);
        Assert.Empty(late!.Objects);
        Assert.Empty(rig.Vision.World.LocatedObjects);
        Assert.Null(rig.Vision.History.GetComputedStateAt(3033));
        Assert.Contains(log, l => l == "VisionComponent.UpdateVisionMarkers.HistoricalPoseNotFound: Time: 3033, hist: 3000 to 3000");   // 0x00654DE2: the timestamp, GetOldestTimeStamp, GetNewestTimeStamp (R-FIX batch 3, M11-050)
        Assert.Equal(0, rig.Vision.World.UnbuiltLocalizeRobotCalls);                   // AddAndUpdateObjects never ran
        var onTime = rig.Vision.ProcessCapture(frame, 2, 3000);                        // a raw state exactly at the timestamp
        Assert.NotNull(onTime);
        Assert.NotNull(rig.Vision.History.GetComputedStateAt(3000));
        Assert.NotEmpty(onTime!.Markers);
    }

    // ------------------------------------------------------------------------------------------- helpers

    private static void Field<T>(object target, string name, T value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

    /// <summary>An offline robot and a vision system fed rendered frames, with no cube connected unless the test connects one.</summary>
    private sealed class ImageRig : IDisposable
    {
        public readonly CozmoRobot Robot = CozmoRobot.CreateOffline();
        public readonly VisionSystem Vision;
        public readonly List<string> Log = new();
        public uint T = 1000;
        private ushort _seq = 1;

        public ImageRig()
        {
            Deliver(new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), _seq++));
            Vision = new VisionSystem(Robot, Cal) { Enabled = false };
            Vision.World.Log += l => { lock (Log) Log.Add(l); };
        }

        public void Send(RobotMessage m) => Deliver(new SubMessage(ReliableMessageType.SingleReliableMessage, m.ToBytes(), _seq++));

        private void Deliver(SubMessage sm)
        {
            var f = new Frame { Type = ReliableMessageType.MultipleMixedMessages, SeqMin = sm.Seq, SeqMax = sm.Seq, Ack = 0, Messages = new List<SubMessage> { sm } };
            Robot.Transport.ProcessIncoming(FrameCodec.Encode(f));
        }

        public VisionFrameResult Frame(params (ObjectType Type, Pose3d Pose)[] cubes)
        {
            Send(new RobotState { Timestamp = T, Pose = new RobotPose { X = 0, Y = 0, Angle = 0 }, HeadAngle = -0.15f, Status = (uint)RobotStatusFlag.HeadInPos,
                                  Accel = new AccelData { Z = 9800 }, Gyro = new GyroData() });
            Send(new ImageImuData { ImageId = T, RateX = 0, RateY = 0, RateZ = 0, Line2Number = 0 });
            var pd = Vision.History.At(T)!.Value;
            var frame = new GrayImage(Cal.Columns, Cal.Rows);
            frame.Fill(150);
            foreach (var (type, pose) in cubes) MarkerRenderer.DrawObject(frame, Lib!, new CameraModel(Cal, pd.CameraPose), type, pose);
            var r = Vision.ProcessImage(frame, T, T, pd);
            T += 33;
            return r;
        }

        public VisionFrameResult Observe(params (ObjectType Type, Pose3d Pose)[] cubes)
        {
            Frame(cubes);
            return Frame(cubes);
        }

        public void Dispose() { Vision.Dispose(); Robot.Dispose(); }
    }
}
