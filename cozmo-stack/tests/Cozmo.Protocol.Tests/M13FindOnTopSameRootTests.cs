using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// FIND-ON-TOP-SAME-ROOT-1 (record M13-007; also M12-008, M12-012, M11-037). The engine's
/// <c>FindObjectOnTopOrUnderneathHelper</c> 0x0062601C hands <c>FindLocatedObjectHelper</c> 0x0061EB78 a copy of the caller's filter
/// (census H5..H8). With the default filter every inline caller builds (onlyLatest 0, originMode 0, e.g. SetObjectAsAttachedToLift
/// 0x00632E86..0x00632F02) only the CURRENT origin is walked (F2, 0x0061EBF2..0x0061EC2A); the lambda's first step P1
/// (0x0062BA0E..0x0062BA48) rejects a candidate whose pose has no transform to the reference's parent, which is obj's ROOT
/// (H2 0x00626048 GetWithRespectToRoot; GetWithRespectTo returns 0 for different roots 0x00845A62..0x00845A68); and the first passing
/// object in (origin, family, type, ID) ascending order is returned (F3/F6, 0x0061ED82..0x0061ED8E). ClearLocatedObjectHelper passes the
/// default filter only (C9 0x00625DAA..0x00625E06), with no family 6/7 exclusion. Every expected value below is one of these facts,
/// never what the code returned.
/// </summary>
public class M13FindOnTopSameRootTests
{
    private static readonly MarkerLibrary? Lib = MarkerLibrary.EmbeddedOrNull;

    /// <summary>One light cube (ID 7) seen at (260, 0, 22) through the rig's vision, as M12RVisBuildTests' fixtures place it.</summary>
    private static (Rig Rig, ObservableObject Bottom) SeenCube()
    {
        var rig = new Rig { Head = 0.05f, Cube = new Pose3d(Mat3.Identity, new Vec3(260, 0, 22)) };
        rig.Frame(); rig.Frame();
        var bottom = rig.Vision.World.GetLocatedObjectById(7);
        Assert.NotNull(bottom);
        return (rig, bottom!);
    }

    /// <summary>
    /// A cube resting exactly on <paramref name="bottom"/> (44 mm above it in the bottom's root frame), keyed in <paramref name="origin"/>, whose pose
    /// is parented to <paramref name="parent"/>. It is inserted with AddLocatedObject: the substitution stands for an object the world holds in that
    /// origin and frame (for example after a delocalization), which the rig's single vision origin cannot produce by itself.
    /// </summary>
    private static ObservableObject OnTop(BlockWorld world, ObservableObject bottom, uint id, ObjectType type, uint origin, Pose3d parent)
    {
        using var flat = bottom.NativePose.GetWithRespectToRoot();
        var t = flat.Translation;
        var pose = new Pose3d(Mat3.Identity, new Vec3(t.X, t.Y, t.Z + 44));
        pose.SetParent(parent);
        var top = new ObservableObject(id, type, CubeGeometry.MarkersFor(type)) { OriginId = origin, PoseState = PoseState.Known, Pose = pose };
        world.AddLocatedObject(top);
        return top;
    }

    // ---------------------------------------------------------------- C10 SetObjectAsAttachedToLift 0x00632F14

    /// <summary>
    /// C10, the default filter's originMode 0 (0x00632F02 strh 0 over +0x6C/+0x6D; F2 0x0061EBF2..0x0061EC2A): a cube resting on the picked-up cube in
    /// the SAME root but keyed in ANOTHER origin is not walked, so this+0x14 stays -1 (0x0063314C..0x00633150) and neither the info nor the warning
    /// "Robot.SetObjectAsAttachedToLift" is logged. The same cube keyed in the current origin is found and becomes this+0x14 (0x0063318C..0x00633190).
    /// </summary>
    // reach: unit (DockingSystem.SetObjectAsAttachedToLift called directly, the PickAndPlaceResult dispatch is not driven; the cube on top is inserted with
    // AddLocatedObject in a chosen origin key, substituting a delocalization the rig does not drive).
    [Fact]
    public void M13_007_C10_ACubeOnTopInAnotherOriginIsNotAttached()
    {
        if (Lib is null) return;
        using var signals = SignalTestContext.Install();
        var (rig, bottom) = SeenCube();
        using (rig)
        {
            var world = rig.Vision.World;
            var root = world.GetNativeOrigin(world.CurrentOriginId);
            var top = OnTop(world, bottom, 50, ObjectType.Block_LIGHTCUBE3, world.CurrentOriginId + 1, root);
            Assert.True(PoseTreeNode.HasSameRoot(top.NativePose.Node, bottom.NativePose.Node));            // only the origin key differs
            var logs = new List<EngineLogEntry>(); rig.Robot.Engine.LogEntry += e => { lock (logs) logs.Add(e); };
            Assert.Equal(0u, rig.M.Docking.SetObjectAsAttachedToLift(7, MarkerType.LightCubeI_Front));
            Assert.True(rig.M.Docking.Carrying.IsCarrying(7));
            Assert.Null(rig.M.Docking.Carrying.CarriedOnTopId);                                            // -1
            lock (logs) Assert.DoesNotContain(logs, e => e.Name == "Robot.SetObjectAsAttachedToLift");
        }

        var (rig2, bottom2) = SeenCube();
        using (rig2)
        {
            var world = rig2.Vision.World;
            OnTop(world, bottom2, 50, ObjectType.Block_LIGHTCUBE3, world.CurrentOriginId, world.GetNativeOrigin(world.CurrentOriginId));
            Assert.Equal(0u, rig2.M.Docking.SetObjectAsAttachedToLift(7, MarkerType.LightCubeI_Front));
            Assert.Equal(50u, rig2.M.Docking.Carrying.CarriedOnTopId);
        }
    }

    /// <summary>
    /// C10, P1 (0x0062BA0E..0x0062BA48): a cube resting on the picked-up cube, keyed in the CURRENT origin, whose pose is under an unrelated root is
    /// rejected by the lambda's GetWithRespectTo(candidate, obj's root) (different roots, 0x00845A62..0x00845A68), silently: this+0x14 stays -1 and
    /// no "Robot.SetObjectAsAttachedToLift" line (neither the info 0x00632FC0 nor the warning 0x00633160) is logged, because nothing was found.
    /// </summary>
    // reach: unit (DockingSystem.SetObjectAsAttachedToLift called directly; the cube on top is inserted under a substituted unrelated root).
    [Fact]
    public void M13_007_C10_ACubeOnTopUnderAnotherRootIsNotFound()
    {
        if (Lib is null) return;
        using var signals = SignalTestContext.Install();
        var (rig, bottom) = SeenCube();
        using (rig)
        {
            var world = rig.Vision.World;
            var unrelated = new Pose3d("substituted unrelated root");
            OnTop(world, bottom, 50, ObjectType.Block_LIGHTCUBE3, world.CurrentOriginId, unrelated);
            var logs = new List<EngineLogEntry>(); rig.Robot.Engine.LogEntry += e => { lock (logs) logs.Add(e); };
            Assert.Equal(0u, rig.M.Docking.SetObjectAsAttachedToLift(7, MarkerType.LightCubeI_Front));
            Assert.Null(rig.M.Docking.Carrying.CarriedOnTopId);
            lock (logs) Assert.DoesNotContain(logs, e => e.Name == "Robot.SetObjectAsAttachedToLift");
        }
    }

    /// <summary>
    /// F3/F6 (0x0061EC50..0x0061ED4A, returnFirst 0x0061ED82..0x0061ED8E): with two cubes that both pass the lambda, the one returned is the FIRST in
    /// (origin, family, type, ID) ascending order: same origin, family and type, so the lower ID, 55, whatever order they were added in (60 first here).
    /// </summary>
    // reach: unit (DockingSystem.SetObjectAsAttachedToLift called directly; both cubes on top are inserted with AddLocatedObject).
    [Fact]
    public void M13_007_C10_TheFirstMatchFollowsTheWalkOrder()
    {
        if (Lib is null) return;
        using var signals = SignalTestContext.Install();
        var (rig, bottom) = SeenCube();
        using (rig)
        {
            var world = rig.Vision.World;
            var root = world.GetNativeOrigin(world.CurrentOriginId);
            OnTop(world, bottom, 60, ObjectType.Block_LIGHTCUBE3, world.CurrentOriginId, root);
            OnTop(world, bottom, 55, ObjectType.Block_LIGHTCUBE3, world.CurrentOriginId, root);
            Assert.Equal(0u, rig.M.Docking.SetObjectAsAttachedToLift(7, MarkerType.LightCubeI_Front));
            Assert.Equal(55u, rig.M.Docking.Carrying.CarriedOnTopId);
        }
    }

    // ---------------------------------------------------------------- C11 CanInteractWithObjectHelper 0x0063C730

    /// <summary>
    /// C11 (default filter 0x0063C6C0..0x0063C71E; the helper returns found == null, 0x0063C78A..0x0063C792): a cube resting on the cube in another
    /// origin key (same root) or under another root (current origin) does not block CanStackOnTopOfObject (F2; P1); the same cube in the current
    /// origin and root does (M12-012: a cube on top within 15.0 makes it false).
    /// </summary>
    // reach: unit (DockingSystem.CanStackOnTopOfObject called directly; the cube on top is inserted with AddLocatedObject in a substituted origin key or root).
    [Fact]
    public void M13_007_C11_OnlyACubeOnTopInTheCurrentOriginAndRootBlocksStacking()
    {
        if (Lib is null) return;
        using var signals = SignalTestContext.Install();
        (uint Origin, bool Unrelated, bool Expected)[] cases = { (1, false, true), (0, true, true), (0, false, false) };   // origin offset, root, CanStack
        foreach (var (offset, unrelatedRoot, expected) in cases)
        {
            var (rig, bottom) = SeenCube();
            using (rig)
            {
                var world = rig.Vision.World;
                var parent = unrelatedRoot ? new Pose3d("substituted unrelated root") : world.GetNativeOrigin(world.CurrentOriginId);
                OnTop(world, bottom, 50, ObjectType.Block_LIGHTCUBE3, world.CurrentOriginId + offset, parent);
                Assert.True(rig.M.Docking.CanStackOnTopOfObject(bottom) == expected, $"origin +{offset}, unrelated root {unrelatedRoot}");
            }
        }
    }

    // ---------------------------------------------------------------- C9 ClearLocatedObjectHelper 0x00625E1A

    /// <summary>
    /// C9: ClearLocatedObjectHelper passes the DEFAULT filter only (0x00625DAA..0x00625E06), so a markerless object (family 6, ProxObstacle) resting
    /// on a deleted cube is a candidate of the on-top search, where C1/C2/C8 exclude families 6 and 7. Its footprint (vtable +0x50 for a markerless
    /// object) is not ported (M13-023), so the labelled planar stand-in decides it and reports the visible MISSING; that MISSING appearing during the
    /// delete is what this test checks. The recursion ClearLocatedObjectHelper(top) (0x00625E7A) then resets the selected ID; that outcome rests on
    /// the stand-in's decision, not on the engine's footprint.
    /// </summary>
    // reach: unit (BlockWorld.DeleteLocatedObjects called directly on a host-fixture world with flat poses; the M13-023 stand-in decides the footprint).
    [Fact]
    public void M13_007_C9_TheClearSearchDoesNotExcludeMarkerlessObjects()
    {
        var w = new BlockWorld();
        var logs = new List<string>(); w.Log += logs.Add;
        var cube = new ObservableObject(1, ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
        { OriginId = w.CurrentOriginId, PoseState = PoseState.Known, Pose = new Pose3d(Mat3.Identity, new Vec3(200, 0, 22)) };
        w.AddLocatedObject(cube);
        var markerless = new ObservableObject(2, ObjectType.ProxObstacle, Array.Empty<KnownMarker>())
        { OriginId = w.CurrentOriginId, PoseState = PoseState.Known, Pose = new Pose3d(Mat3.Identity, new Vec3(200, 0, 66)) };
        w.AddLocatedObject(markerless);
        Assert.Equal(ObjectFamily.MarkerlessObject, markerless.Family);
        Assert.True(w.SelectNativeObject(2));
        logs.Clear();
        var filter = new BlockWorldFilter(); filter.AllowedIds.Add(1);
        w.DeleteLocatedObjects(filter);
        Assert.Single(logs, l => l.StartsWith("MISSING: M13-007 / M13-023 FindObjectOnTopOrUnderneathHelper 0x0062601C at ClearLocatedObjectHelper 0x00625E1A"));   // once per site
        Assert.Null(w.SelectedObjectId);
    }

    // ---------------------------------------------------------------- C8 UpdatePoseOfStackedObjects 0x0062190C, native origin bound

    /// <summary>
    /// C8 with the live vision rig (native origin owner bound): cube A has been carried and placed (NativeCarryingTree set, its pose parented to the
    /// origin) and cube B rests on it. A is re-observed 30 mm further along x. The write helper's old pose is a copy of A's own pose with its parent
    /// (GetPose 0x005060CA, copy ctor 0x005060D2), so the clone at the old pose shares B's root and P1 (0x0062BA0E..0x0062BA48) passes; B, not observed
    /// in the last 100 ms (0x00629BAE), follows: B's pose w.r.t. A's old pose pre-composed with A's new pose (0x0062196E..0x006219A8), written Dirty
    /// (0x006219C0, AddObjectRelativeObservation SetPose Dirty 0x00506D3C..0x00506D3E). The expected pose is computed from A's poses by that formula.
    /// </summary>
    // reach: unit (the frames go through the rig's vision pipeline, but the past carry is substituted by setting NativeCarryingTree, and B is
    // inserted with AddLocatedObject; the pick-up and place dispatch is not driven).
    [Fact]
    public void M13_007_C8_ACarriedAndPlacedCubeCarriesTheCubeOnTopWhenReobserved()
    {
        if (Lib is null) return;
        using var signals = SignalTestContext.Install();
        var (rig, a) = SeenCube();
        using (rig)
        {
            var world = rig.Vision.World;
            var root = world.GetNativeOrigin(world.CurrentOriginId);
            Assert.Same(root.Node, a.NativePose.Node!.Parent);                                          // placed: in the origin
            a.NativeCarryingTree = true;                                                                // substituted: has been carried
            var b = OnTop(world, a, 50, ObjectType.Block_LIGHTCUBE3, world.CurrentOriginId, root);
            Transform3d aOld; using (var f = a.NativePose.GetWithRespectToRoot()) aOld = f.Transform;
            Transform3d bOld; using (var f = b.NativePose.GetWithRespectToRoot()) bOld = f.Transform;
            rig.Cube = new Pose3d(Mat3.Identity, new Vec3(290, 0, 22));
            for (int i = 0; i < 4 && b.PoseState != PoseState.Dirty; i++) rig.Frame();
            Assert.Equal(PoseState.Dirty, b.PoseState);
            Transform3d aNew; using (var f = a.NativePose.GetWithRespectToRoot()) aNew = f.Transform;
            Assert.True(aNew.Translation.X > aOld.Translation.X + 20, "A was not re-observed at its new place");
            var expected = aNew.Compose(aOld.Inverse().Compose(bOld));
            using (var f = b.NativePose.GetWithRespectToRoot())
            {
                Assert.Equal(expected.Translation.X, f.Translation.X, 2);
                Assert.Equal(expected.Translation.Y, f.Translation.Y, 2);
                Assert.Equal(expected.Translation.Z, f.Translation.Z, 2);
            }
        }
    }

    /// <summary>
    /// C8 with a CARRIED stack (A on the lift, B attached on top, both NativeCarryingTree as the attach sets them) when A is re-posed while still carried.
    /// The engine order of that sighting (CarriedOnTopBindingTests' path) is: the write helper copies A's own pose, parented to the lift, as the old pose
    /// (0x005060CA/0x005060D2) and writes the new one (B, a child of A's node, moves with it); UnSetCarryObject (0x00505FBC) clears both IDs; later in
    /// the frame UpdatePoseOfStackedObjects runs. The search measures in the ROOT frame: refPose = GetWithRespectToRoot(old pose) (H2 0x00626048),
    /// targetZ = refPose.z + 22 (H4), the candidate's root-frame z and dims (P5/P6 0x0062BA4A..0x0062BB0E). With A re-posed 5 mm from where the lift
    /// held it, B still rests on the old pose, is found, re-parented to A's parent and moved: newPose = (B w.r.t. the old pose) pre-composed with A's
    /// new pose (0x0062196E..0x006219A8), written Dirty (0x006219C0, 0x00506D3C..0x00506D3E). Expected root-frame result:
    /// aNew * inv(aOld) * aNew * bLocal, from the transforms read here.
    /// </summary>
    // reach: unit. The attach is real (rig vision frames, the public DockingSystem.SetObjectAsAttachedToLift, as CarriedOnTopBindingTests does). The
    // sighting itself is SUBSTITUTED: the rig cannot render a cube on the lift into view (tried head angles -0.4..0.2 rad, no sighting), so the write
    // helper's steps are driven directly: the old-pose copy of A's native pose, SetPose, the broadcast inside the frame window (flag and last image time
    // set, and the list cleared as the frame start does, by reflection), then DockingSystem.UnsetCarryingObjects(false) and BlockWorld.UpdatePoseOfStackedObjects.
    [Fact]
    public void M13_007_C8_ACarriedStackFollowsWhenTheCarriedCubeIsReposedOnTheLift()
    {
        if (Lib is null) return;
        using var signals = SignalTestContext.Install();
        using var rig = new Rig { Head = 0.05f, Cube = new Pose3d(Mat3.Identity, new Vec3(260, 0, 22)) };
        rig.MoreCubes.Add((ObjectType.Block_LIGHTCUBE2, new Pose3d(Mat3.Identity, new Vec3(260, 0, 66))));
        rig.Frame(); rig.Frame();
        var world = rig.Vision.World;
        var a = world.GetLocatedObjectById(7)!; var b = world.GetLocatedObjectById(8)!;
        var marker = a.Markers.First(k => k.Code == MarkerType.LightCubeI_Front).Code;
        Assert.Equal(0u, rig.M.Docking.SetObjectAsAttachedToLift(a.ObjectId, marker));
        Assert.Equal(8u, rig.M.Docking.Carrying.CarriedOnTopId);
        Assert.True(a.NativeCarryingTree && b.NativeCarryingTree);
        Assert.Same(a.NativePose.Node, b.NativePose.Node!.Parent);
        Transform3d aOld; using (var f = a.NativePose.GetWithRespectToRoot()) aOld = f.Transform;
        var bLocal = b.NativePose.Transform;                                                     // B w.r.t. A's node
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(BlockWorld).GetMethod("ClearPoseChanges", flags)!.Invoke(world, null);           // the frame start clears the list (0x00624F78)
        typeof(BlockWorld).GetField("_appendPoseChanges", flags)!.SetValue(world, true);
        typeof(BlockWorld).GetField("_lastImageTimestamp", flags)!.SetValue(world, unchecked(b.LastObservedTimestamp + 1000u));
        var origin = world.GetNativeOrigin(world.CurrentOriginId);
        using (var old = new Pose3d(a.NativePose))                                               // 0x005060CA/0x005060D2: parent (the lift) kept
        {
            Assert.True(old.HasParent);
            var oldState = a.PoseState;
            var next = new Pose3d(aOld.Rotation.Matrix(), new Vec3(aOld.Translation.X + 5, aOld.Translation.Y, aOld.Translation.Z));
            next.SetParent(origin);
            a.SetPose(next, -1.0, PoseState.Known);
            world.BroadcastObjectPoseChanged(a, old, oldState);
        }
        rig.M.Docking.UnsetCarryingObjects(false);                                               // 0x00505FBC -> 0x00633F74
        Assert.Null(rig.M.Docking.Carrying.CarriedOnTopId);
        world.UpdatePoseOfStackedObjects();
        Assert.Equal(PoseState.Dirty, b.PoseState);
        Assert.NotSame(a.NativePose.Node, b.NativePose.Node!.Parent);                            // re-parented by C8 (0x006219A8): it was found
        Transform3d aNew; using (var f = a.NativePose.GetWithRespectToRoot()) aNew = f.Transform;
        var expected = aNew.Compose(aOld.Inverse()).Compose(aNew.Compose(bLocal));
        using (var f = b.NativePose.GetWithRespectToRoot())
        {
            Assert.Equal(expected.Translation.X, f.Translation.X, 2);
            Assert.Equal(expected.Translation.Y, f.Translation.Y, 2);
            Assert.Equal(expected.Translation.Z, f.Translation.Z, 2);
        }
    }

    // ---------------------------------------------------------------- DeleteLocatedObjects 0x0061D1CE.., the delete notification's old pose

    /// <summary>
    /// DeleteLocatedObjects copies the deleted object's own pose with its parent into the notification (GetPose 0x0061D290, copy ctor 0x0061D2A6) and
    /// broadcasts it (0x0061D5C8); MapComponent's remove reads that old pose's root ID (GetRootID 0x0067EC3C) and removes the object from that root's
    /// map. A once-carried cube (NativeCarryingTree set) placed in the origin is therefore removed from the origin's memory map.
    /// </summary>
    // reach: unit (BlockWorld.DeleteLocatedObjects called directly; the origin, map, robot pose and image time are supplied; the past carry is
    // substituted by setting NativeCarryingTree).
    [Fact]
    public void M13_007_DeletingAOnceCarriedCubeRemovesItFromTheOriginsMap()
    {
        using var origin = new Pose3d("bound origin") { Id = 1 };
        using var robotPose = new Pose3d("robot", origin);
        var w = new BlockWorld { NativeOrigin = _ => origin };
        var memory = new MemoryMap();
        w.Maps.Maps[origin.Id] = memory; w.Maps.RobotPose = () => robotPose; w.Maps.ImageTimestamp = () => 123u;
        var cube = new ObservableObject(5, ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
        { OriginId = w.CurrentOriginId, PoseState = PoseState.Known, Pose = new Pose3d(Mat3.Identity, new Vec3(200, 0, 22)) };
        cube.NativePose.SetParent(origin);
        w.AddLocatedObject(cube);
        cube.NativeCarryingTree = true;                                                          // substituted: has been carried and placed
        Assert.False(cube.Pose.HasParent);                                                       // the public view is the flat copy
        var removed = new List<uint>(); memory.ObjectRemovalForTest = r => removed.Add(r.ObjectId);
        var filter = new BlockWorldFilter(); filter.AllowedIds.Add(5);
        w.DeleteLocatedObjects(filter);
        Assert.Equal(new[] { 5u }, removed);
    }
}
