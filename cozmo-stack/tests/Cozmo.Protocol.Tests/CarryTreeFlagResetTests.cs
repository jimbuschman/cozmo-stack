using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// CARRY-TREE-FLAG-RESET-1 (records M12-008, M11-044; inventory .scratch/CARRY-TREE-FLAG-RESET-1-census.md). The stack's world-frame view of a carried
/// object (ObservableObject.NativeCarryingTree, no engine field) must end where the engine re-parents the object to the world origin:
/// AddRobotRelativeObservation 0x00506B82..0x00506BAA (place; caller 0x005338A0) and UpdatePoseInInstance's write helper 0x00505F38/0x00505F56 (sighting,
/// before UnSetCarryObject 0x00505FBC). UnSetCarryingObjects alone keeps the lift parent (census E6). Expected values are hand-derived from the census rig:
/// robot (100,0,0) identity in the current origin; lift angle 0, so the lift is (66,0,0) w.r.t. the pivot (-41,0,45) (0x0050FFE0), (125,0,45) in the world;
/// the cube (2,3,4) identity w.r.t. the lift.
/// </summary>
[Collection("SteppedBehavior missing-report statics")]
public sealed class CarryTreeFlagResetTests
{
    private const float Tol = 1e-3f;

    private sealed class Fixture : IDisposable
    {
        internal readonly M4ControlTests.Rig Rig = new();
        internal readonly VisionSystem Vision;
        internal readonly ManipulationSystem M;
        internal readonly List<string> Missing = new();
        private readonly Action<string> _missing;
        private readonly bool _error = EngineErrorState.ErrorFlagSet;
        internal EngineRobot Robot => Rig.Engine.Robot!;
        internal Pose3d Origin => Robot.GetNativeOrigin(Robot.CurrentOriginId);
        internal Fixture()
        {
            Rig.ToSynced(); Vision = Rig.Robot.Vision; Vision.Calibration = CameraCalibration.Nominal(); Vision.Enabled = false;
            M = new ManipulationSystem(Rig.Robot, Vision);
            Vision.World.CurrentOriginId = Robot.CurrentOriginId;
            MoveTo(100);
            SteppedBehavior.ResetMissingForTests();
            _missing = Missing.Add; SteppedBehavior.MissingReported += _missing;
        }
        // A received RobotState (lift angle 0) in the robot's current origin: Robot::Update writes the main pose and ComputeLiftPose(0).
        internal void MoveTo(float x)
        {
            Rig.State(origin: Robot.CurrentOriginId, x: x);
            Assert.InRange(Robot.MainPose.Translation.X, x - Tol, x + Tol);                     // rig precondition
            Assert.Equal(0f, Robot.MainPose.Translation.Y); Assert.Equal(0f, Robot.MainPose.Translation.Z);
        }
        // M12PutDownCheckedRowsTests.cs:28's carried fixture shape: the cube (2,3,4) on the shared lift node, as the attach leaves it.
        internal ObservableObject AddCarried(uint id = 7)
        {
            var obj = new ObservableObject(id, ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            { Pose = new Pose3d(Rotation3d.Identity, new Vec3(2, 3, 4), parent: Robot.LiftPose), PoseState = PoseState.Known, OriginId = Robot.CurrentOriginId, NativeCarryingTree = true, LastObservedTimestamp = 42, FromDistance = 8 };   // the flag as the attach sets it (Docking.SetObjectAsAttachedToLift)
            Vision.World.AddLocatedObject(obj);
            M.Docking.Carrying.AttachToLift(obj.ObjectId, obj.Markers[0], new Pose3d(""), null, null);
            return obj;
        }
        public void Dispose()
        {
            SteppedBehavior.MissingReported -= _missing;
            M.Dispose(); Vision.Dispose(); Rig.Dispose(); EngineErrorState.ErrorFlagSet = _error;
        }
    }

    private static void AssertVec(Vec3 expected, Vec3 actual)
    {
        Assert.InRange(actual.X, expected.X - Tol, expected.X + Tol);
        Assert.InRange(actual.Y, expected.Y - Tol, expected.Y + Tol);
        Assert.InRange(actual.Z, expected.Z - Tol, expected.Z + Tol);
    }

    /// <summary>
    /// M12-008 / census E1+E4: the place (SetCarriedObjectAsUnattached 0x005338A0 -> AddRobotRelativeObservation SetParent(Robot::GetPose) 0x00506B9A,
    /// GetWithRespectToRoot 0x00506BA2, assignment 0x00506BAA, SetPose 0x004EF590..0x004EF5A2) leaves the cube root-parented in the world origin at its
    /// world pose. Expected: robot (100,0,0) + pivot (-41,0,45) + lift (66,0,0) + cube (2,3,4) = (127,3,49); w.r.t. the robot (27,3,49). The public pose is
    /// parented to the origin like the native pose, without the legacy frame flag.
    /// </summary>
    // reach: unit (SetCarriedObjectAsUnattached is entered directly on a ManipulationSystem built on the rig's live robot and vision; the carried state is
    // substituted by placing the cube on the shared lift node and CarryingComponent.AttachToLift, as M12PutDownCheckedRowsTests does).
    [Fact]
    public void M12_008_APlacedCubeIsParentedToTheOriginAndNoLongerAWorldFrameView()
    {
        using var f = new Fixture();
        var obj = f.AddCarried();
        Assert.True(obj.NativeCarryingTree);                                                    // attached: lift parent (not a root)
        AssertVec(new Vec3(127, 3, 49), obj.Pose.Translation);
        Assert.Equal(0u, f.M.Docking.SetCarriedObjectAsUnattached(false));
        Assert.Same(f.Origin.Node, obj.NativePose.Node!.Parent);                                // E4: parent = the world origin
        var pub = obj.Pose;
        AssertVec(new Vec3(127, 3, 49), pub.Translation);
        Assert.True(pub.HasParent);
        Assert.Same(obj.NativePose.Node.Parent, pub.Node!.Parent);
        Assert.False(pub.HasLegacyFrameInput);
        using var wrtRobot = pub.WithRespectTo(f.Robot.MainPose);                                // no MISSING throw: one origin
        AssertVec(new Vec3(27, 3, 49), wrtRobot.Translation);
        Assert.False(obj.NativeCarryingTree);
    }

    /// <summary>
    /// M11-044 consequence of the place (census §1, BlockWorld SetLocalizedTo native route): the once-carried placed cube is no longer refused with
    /// "marker loop withheld"; Robot::SetLocalizedTo 0x0051238C stores the object's ID (0x00512458).
    /// </summary>
    // reach: unit (the carried fixture above; SetLocalizedTo is called directly on the robot's LocalizationWorld, whose native route uses the
    // CozmoRobot-bound NativeSnapshotOwner/NativeCurrentCamera, not through LocalizeToObject). Substitution: the hand-built cube's marker times are
    // initialised with ObservableObject.InitializeNativeMarkerTimes, which production runs when it creates a located instance (BlockWorld.cs:2000).
    // The rig's engine is started through the offline seam, so IsOfflineEngine is cleared by reflection (as LocalizeToObjectSameOriginTests.cs:507 does);
    // otherwise SetLocalizedTo takes the offline flat route and never reaches the native-route refusal under test.
    [Fact]
    public void M11_044_APlacedOnceCarriedCubeCanBeLocalizedTo()
    {
        using var f = new Fixture();
        var obj = f.AddCarried();
        obj.InitializeNativeMarkerTimes();                                                      // substituted (see reach)
        typeof(CozmoEngine).GetProperty("IsOfflineEngine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(f.Rig.Engine, false);                                                     // substituted (see reach)
        Assert.Equal(0u, f.M.Docking.SetCarriedObjectAsUnattached(false));
        lock (f.Missing) f.Missing.Clear(); SteppedBehavior.ResetMissingForTests();
        bool localized = f.Vision.World.SetLocalizedTo(obj);
        lock (f.Missing) Assert.DoesNotContain(f.Missing, m => m.Contains("M11-044"));
        Assert.True(localized);
        Assert.Equal(7u, f.Vision.World.LocalizedToObjectId);
        Assert.False(obj.NativeCarryingTree);
    }

    /// <summary>
    /// M12-008 / census E11: a second pickup of the placed cube re-enters the lift (AddLiftRelativeObservation SetPose 0x00506E80..0x00506E94) and follows
    /// it. objWrtLift = (L + 4.0, 0, -12.5) (0x00632E7C 4.0; 0x00632E82 0xC1480000), L = |markers[0] translation| read from CubeGeometry.MarkersFor; at
    /// robot (200,0,0) the lift is (225,0,45), so the cube is (229 + L, 0, 32.5).
    /// </summary>
    // reach: unit (the attach is DockingSystem.SetObjectAsAttachedToLift, the public entry the dock result's handler uses, not a dock result).
    [Fact]
    public void M12_008_ASecondPickupOfThePlacedCubeFollowsTheLift()
    {
        using var f = new Fixture();
        var obj = f.AddCarried();
        Assert.Equal(0u, f.M.Docking.SetCarriedObjectAsUnattached(false));
        Assert.False(obj.NativeCarryingTree);
        var marker = CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1)[0];
        double L = marker.PoseOnObject.Translation.Length;
        Assert.Equal(0u, f.M.Docking.SetObjectAsAttachedToLift(obj.ObjectId, marker.Code));
        Assert.Same(f.Robot.LiftPose.Node, obj.NativePose.Node!.Parent);
        Assert.True(obj.NativeCarryingTree);
        f.MoveTo(200);
        var pub = obj.Pose;
        AssertVec(new Vec3((float)(225 + (L + 4)), 0, 32.5f), pub.Translation);   // lift x 225 + objWrtLift x (L + 4); 45 - 12.5
        Assert.False(pub.HasParent);                                                            // the carried world-frame view
    }

    /// <summary>
    /// M12-008 / census E6: UnSetCarryingObjects alone does not re-parent (research/20261021-m12-detach-pose-join.md U5); the node stays on the lift and the
    /// public pose stays the world value (127,3,49), never the lift-local (2,3,4).
    /// </summary>
    // reach: unit (DockingSystem.UnsetCarryingObjects is called directly on the carried fixture above).
    [Fact]
    public void M12_008_UnsetAloneKeepsTheWorldViewOfALiftParentedCube()
    {
        using var f = new Fixture();
        var obj = f.AddCarried();
        f.M.Docking.UnsetCarryingObjects(false);
        Assert.Null(f.M.Docking.Carrying.CarriedObjectId);
        Assert.Same(f.Robot.LiftPose.Node, obj.NativePose.Node!.Parent);
        Assert.True(obj.NativeCarryingTree);
        AssertVec(new Vec3(127, 3, 49), obj.Pose.Translation);
        Assert.False(obj.Pose.HasParent);
    }

    /// <summary>
    /// M12-008 / M11-007 census E7: a sighting of the carried cube writes the observed pose through UpdatePoseInInstance's write helper
    /// (0x00505F38/0x00505F56) and then UnSetCarryObject (0x00505FBC): the cube's node takes the observation's parent and the world-frame view ends.
    /// </summary>
    // reach: unit (substituted: the direct attach and the uncited origin parent, below); the sighting runs through production camera frames (VisionSystem, as CarriedOnTopBindingTests.ASightingOfTheCarriedCubeUnsetsBothAndSendsCarryNone); the attach is
    // entered through DockingSystem.SetObjectAsAttachedToLift. Substitution: the asserted origin parent is the stack's observed-pose parent; the engine's
    // root-parenting of the observed pose (0x00625544 bl 0x4DF628, census Q1) is RECOVERABLE_GAP, not re-traced.
    [Fact]
    public void M12_008_ASightingOfTheCarriedCubeEndsTheWorldFrameView()
    {
        using var signals = SignalTestContext.Install();
        using var rig = new Rig();
        if (rig.NoLibrary) return;
        rig.Head = 0.05f;
        rig.Cube = new Pose3d(Mat3.Identity, new Vec3(260, 0, 22));
        rig.Frame(); rig.Frame();
        var cube = rig.Vision.World.GetLocatedObjectById(7)!;
        var marker = cube.Markers.First(k => k.Code == MarkerType.LightCubeI_Front).Code;
        Assert.Equal(0u, rig.M.Docking.SetObjectAsAttachedToLift(cube.ObjectId, marker));
        Assert.True(cube.NativeCarryingTree);
        rig.Frame();                                                                            // the carried cube is seen where it stood
        Assert.Null(rig.M.Docking.Carrying.CarriedObjectId);
        var robot = rig.Robot.Engine.Robot!;
        Assert.Same(robot.GetNativeOrigin(robot.CurrentOriginId).Node, cube.NativePose.Node!.Parent);
        Assert.Same(cube.NativePose.Node.Parent, cube.Pose.Node!.Parent);                        // the public pose shares the origin parent
        Assert.False(cube.NativeCarryingTree);
    }
}
