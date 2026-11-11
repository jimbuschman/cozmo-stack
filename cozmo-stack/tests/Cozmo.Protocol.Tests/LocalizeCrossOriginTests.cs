using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// LOCALIZE-CROSS-ORIGIN-1: Robot::LocalizeToObject's cross-origin branch (0x0051583C..0x005159B2) and its recipients, from the census
/// .scratch/LOCALIZE-CROSS-ORIGIN-1-census.md (rows X0..X20, sections 6a..6d). Expected values are worked by hand from the census rows (identity
/// rotations or a single yaw, so the pose-tree products are exact or checked to a tolerance); none is read back from the C#.
/// </summary>
[Collection("SteppedBehavior missing-report statics")]
public sealed class LocalizeCrossOriginTests
{
    private const ObjectType Cube1 = ObjectType.Block_LIGHTCUBE1;
    private const ObjectType Cube2 = ObjectType.Block_LIGHTCUBE2;

    private sealed class Setup : IDisposable
    {
        internal readonly PickupPyramidNativeExecutionTests.Fixture F;
        internal EngineRobot Owner => F.Robot;
        internal BlockWorld World => F.M.World;
        internal readonly List<EngineLogEntry> Logs = new();
        internal readonly List<string> Missing = new();
        private readonly bool _error;
        private readonly Action<string> _missing;
        internal Pose3d Origin(uint id) => Owner.GetNativeOrigin(id);

        internal Setup()
        {
            F = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
            F.Rig.Tick();
            F.Rig.Engine.LogEntry += Logs.Add;
            SteppedBehavior.ResetMissingForTests();
            _missing = Missing.Add; SteppedBehavior.MissingReported += _missing;
            _error = EngineErrorState.ErrorFlagSet; EngineErrorState.ErrorFlagSet = false;
        }

        internal bool Logged(string name) => Logs.Any(e => e.Name == name);
        internal EngineLogEntry Log(string name) => Logs.Last(e => e.Name == name);

        public void Dispose()
        {
            SteppedBehavior.MissingReported -= _missing;
            EngineErrorState.ErrorFlagSet = _error;
            F.Dispose();
        }
    }

    private static void AssertT(Vec3 t, double x, double y, double z, int digits = 3)
    {
        Assert.Equal(x, t.X, digits); Assert.Equal(y, t.Y, digits); Assert.Equal(z, t.Z, digits);
    }

    // ------------------------------------------------------------------------------------------ PoseOriginList::Rejigger (X6/X7)

    /// <summary>
    /// X6 0x00847D7C..0x00847DD4: a new origin whose ID is not in the list is the UnknownOrigin VERIFY (key 0x00847EA0); the current origin is the
    /// NewOriginIsCurrent VERIFY (key 0x00C29758); both return 1 (0x00847DD2) with no state change.
    /// </summary>
    // reach: unit — Rejigger called directly on the fixture robot's origin list; the unknown origin is a test root.
    [Fact]
    public void LCO_X6_TheRejiggerGuardsReturnOneAndChangeNothing()
    {
        using var s = new Setup();
        uint current = s.Owner.CurrentOriginId;
        var currentOrigin = s.Origin(current);
        string name = currentOrigin.Name;
        var transform = currentOrigin.Transform;
        using var foreign = new Pose3d("foreign root") { Id = 99 };
        lock (s.Owner.PoseTreeGate)
        {
            using var _ = Pose3d.UseSourceLog((l, c, k, t) => s.Owner.Engine.EmitSourceLog(l, c, k, t), () => { });
            Assert.Equal(1u, s.Owner.RejiggerOrigins(foreign, new Transform3d(Rotation3d.Identity, new Vec3(1, 2, 3))));
            Assert.True(s.Logged("PoseOriginList.Rejigger.UnknownOrigin"));
            Assert.Equal(1u, s.Owner.RejiggerOrigins(currentOrigin, new Transform3d(Rotation3d.Identity, new Vec3(1, 2, 3))));
            Assert.True(s.Logged("PoseOriginList.Rejigger.NewOriginIsCurrent"));
        }
        Assert.Equal(current, s.Owner.CurrentOriginId);
        Assert.Equal(name, currentOrigin.Name);
        Assert.Equal(transform, currentOrigin.Transform);
        Assert.False(currentOrigin.HasParent);
    }

    /// <summary>
    /// X7 0x00847DD6..0x00847E84: T is copied into the OLD current origin's transform (0x00847E1A..0x00847E40), SetParent(old, new) (0x00847E48), the old
    /// name gets "_REJ" (string 0x00847F20), the current origin and ID become the new one (0x00847E7A / 0x00847E7E), 0 is returned and no origin is erased.
    /// </summary>
    // reach: unit — Rejigger called directly after a production Delocalize made origin 2.
    [Fact]
    public void LCO_X7_ASuccessfulRejiggerReparentsTheOldOriginUnderTheNewOne()
    {
        using var s = new Setup();
        uint first = s.Owner.CurrentOriginId;
        s.Owner.Delocalize(false);
        uint second = s.Owner.CurrentOriginId;
        Assert.NotEqual(first, second);
        var ids = s.Owner.PoseOriginIds.ToArray();
        var t = new Transform3d(Rotation3d.FromAngleAxis(.5f, new Vec3(0, 0, 1)), new Vec3(10, 20, 0));
        lock (s.Owner.PoseTreeGate)
        {
            using var _ = Pose3d.UseSourceLog((l, c, k, tx) => s.Owner.Engine.EmitSourceLog(l, c, k, tx), () => { });
            using var target = Pose3d.WrapExisting(s.Origin(first).Node!);
            Assert.Equal(0u, s.Owner.RejiggerOrigins(target, t));
        }
        var old = s.Origin(second);
        Assert.Equal(t, old.Transform);
        Assert.Same(s.Origin(first).Node, old.Node!.Parent);
        Assert.Equal("Origin" + second + "_REJ", old.Name);
        Assert.Equal("Origin" + first, s.Origin(first).Name);
        Assert.Equal(first, s.Owner.CurrentOriginId);
        Assert.Equal(ids, s.Owner.PoseOriginIds.ToArray());
        Assert.Same(s.Owner.OriginNames.Get(first), s.Owner.OriginNames.Get(second)!.Parent);
        Assert.Equal("Origin" + second + "_REJ", s.Owner.OriginNames.Get(second)!.Name);
    }

    // ------------------------------------------------------------------------------------------ PoseOriginList::Flatten (X13)

    /// <summary>
    /// X13 0x00847F34..0x00847FBE: in ascending ID order, an origin with a parent that is not an immediate child of the target is converted in place with
    /// GetWithRespectTo(P, target, P) (0x00847F70) and renamed + "_FLT" (0x00847FD8); a direct child (IsChildOf, 0x00844FD4) and a root are skipped; a failed
    /// conversion (another tree) is not renamed and iteration goes on. Worked: O1 at (10,0,0) in O2, O2 at (0,5,0) in O3, so O1 w.r.t. O3 is (10,5,0).
    /// </summary>
    // reach: unit — Flatten called directly; the origin parents are set by the test (production builds them through Rejigger).
    [Fact]
    public void LCO_X13_FlattenConvertsIndirectOriginsInPlaceAndSkipsDirectChildren()
    {
        using var s = new Setup();
        uint o1 = s.Owner.CurrentOriginId;
        s.Owner.Delocalize(false); uint o2 = s.Owner.CurrentOriginId;
        s.Owner.Delocalize(false); uint o3 = s.Owner.CurrentOriginId;
        s.Owner.Delocalize(false); uint o4 = s.Owner.CurrentOriginId;
        s.Owner.Delocalize(false); uint o5 = s.Owner.CurrentOriginId;
        lock (s.Owner.PoseTreeGate)
        {
            s.Origin(o1).Node!.Transform = new Transform3d(Rotation3d.Identity, new Vec3(10, 0, 0)); s.Origin(o1).SetParent(s.Origin(o2));
            s.Origin(o2).Node!.Transform = new Transform3d(Rotation3d.Identity, new Vec3(0, 5, 0)); s.Origin(o2).SetParent(s.Origin(o3));
            s.Origin(o4).SetParent(s.Origin(o5));
            using var _ = Pose3d.UseSourceLog((l, c, k, t) => s.Owner.Engine.EmitSourceLog(l, c, k, t), () => { });
            s.Owner.FlattenOrigins(o3);
        }
        Assert.Same(s.Origin(o3).Node, s.Origin(o1).Node!.Parent);
        AssertT(s.Origin(o1).Translation, 10, 5, 0);
        Assert.Equal("Origin" + o1 + "_FLT", s.Origin(o1).Name);
        Assert.Equal("Origin" + o2, s.Origin(o2).Name);                       // direct child: skipped
        AssertT(s.Origin(o2).Translation, 0, 5, 0);
        Assert.Equal("Origin" + o3, s.Origin(o3).Name);                       // root: skipped
        Assert.Equal("Origin" + o4, s.Origin(o4).Name);                       // another tree: conversion fails, no rename
        Assert.Same(s.Origin(o5).Node, s.Origin(o4).Node!.Parent);
        Assert.Same(s.Owner.OriginNames.Get(o3), s.Owner.OriginNames.Get(o1)!.Parent);
    }

    // ------------------------------------------------------------------------------------------ MapComponent::UpdateOriginsOfObjects (X11a)

    /// <summary>
    /// X11a 0x0067E058..0x0067E1A8 (census 6c, 6d): entries in ascending (signed int) object-ID order (0x0067E064..0x0067E06C); a missing object is the
    /// NotAnObject info on channel MapComponent (0x0067E148..0x0067E160) and is left alone; an entry with no old-origin pose is left alone; otherwise
    /// RemoveObservableObject(obj, NEW origin) (0x0067E0E0), the new-origin entry emplaced (an existing one kept) with the old entry's +0x20 byte
    /// (0x0067E0FC..0x0067E102), its pose := GetWithRespectToRoot(old pose) only when that byte is set (0x0067E112..0x0067E134), and the old entry erased
    /// (0x0067E142). Worked: the old origin is at (1000, 0, 0) in the new one, so (100, 0, 0) becomes (1100, 0, 0).
    /// </summary>
    // reach: unit — UpdateOriginsOfObjects called on a standalone map component; the pose cache is filled through its private writer by reflection.
    [Fact]
    public void LCO_X11a_ObjectPosesMoveToTheNewOriginInObjectIdOrder()
    {
        using var maps = new SourceMapComponent();
        var logs = new List<(string? Channel, string Key, string Text)>();
        maps.SourceLog = (l, c, k, t) => logs.Add((c, k, t));
        var removed = new List<(uint Id, uint Origin)>();
        maps.RemoveObservableObject67F2C4 = (obj, origin) => removed.Add((obj.ObjectId, origin));
        using var o1 = new Pose3d("Origin1") { Id = 1 };
        using var o2 = new Pose3d("Origin2") { Id = 2 };
        using var o3 = new Pose3d("Origin3") { Id = 3 };
        o1.Node!.Transform = new Transform3d(Rotation3d.Identity, new Vec3(1000, 0, 0)); o1.SetParent(o2);
        var cache = typeof(SourceMapComponent).GetMethod("CacheObjectPose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        void Cache(uint id, Pose3d origin, double x, bool added)
        {
            using var pose = new Pose3d(Rotation3d.Identity, new Vec3(x, 0, 0), parent: origin);
            cache.Invoke(maps, new object[] { id, origin.Id, pose, added });
        }
        const uint negative = 0x80000000;
        Cache(5, o1, 100, true);
        Cache(6, o1, 7, false); Cache(6, o2, 1, true);
        Cache(negative, o1, 4, false);
        Cache(9, o1, 9, true);
        Cache(10, o3, 3, true);
        var located = new Dictionary<uint, ObservableObject>();
        foreach (uint id in new[] { 5u, 6u, negative, 10u })
            located[id] = new ObservableObject(id, Cube1, CubeGeometry.MarkersFor(Cube1));
        using (Pose3d.UseSourceLog((l, c, k, t) => logs.Add((c, k, t))))
            maps.UpdateOriginsOfObjects(1, 2, id => located.GetValueOrDefault(id));
        Assert.Equal(new[] { (negative, 2u), (5u, 2u), (6u, 2u) }, removed);
        Assert.Null(maps.CachedObjectPose(5, 1));
        var five = maps.CachedObjectPose(5, 2)!.Value;
        Assert.True(five.Added);
        AssertT(five.Pose.Translation, 1100, 0, 0);
        Assert.Same(o2.Node, five.Pose.Node!.Parent);
        var six = maps.CachedObjectPose(6, 2)!.Value;
        Assert.False(six.Added);                                                // the old entry's byte, copied onto the kept entry
        AssertT(six.Pose.Translation, 1, 0, 0);                                 // byte 0: the pose is not rewritten
        Assert.Null(maps.CachedObjectPose(6, 1));
        var neg = maps.CachedObjectPose(negative, 2)!.Value;
        Assert.False(neg.Added);
        Assert.Null(maps.CachedObjectPose(negative, 1));
        Assert.NotNull(maps.CachedObjectPose(9, 1));                            // not an object: untouched
        Assert.Contains(logs, l => l.Channel == "MapComponent" && l.Key == "MapComponent.UpdateOriginsOfObjects.NotAnObject" && l.Text.Contains("'9'"));
        Assert.NotNull(maps.CachedObjectPose(10, 3));                           // no old-origin pose: untouched
        Assert.Null(maps.CachedObjectPose(10, 2));
    }

    // ------------------------------------------------------------------------------------------ FaceWorld::UpdateFaceOrigins (X12)

    /// <summary>
    /// X12 0x004F5644..0x004F5866: faces in ascending face ID whose head pose's immediate parent is the old origin are converted with GetWithRespectTo(head,
    /// newOrigin) (0x004F56C6), logged "Flattened FaceID:%d w.r.t. %s" on channel FaceRecognizer (0x004F56F8), assigned (0x004F5722) and counted; a face under
    /// another parent is skipped; then the last-observed face pose (+0x14) is converted in place when its timestamp (+0x20) is not 0 and its parent is the old
    /// origin (0x004F57F6). Worked: Rejigger left the old origin at (1000, 0, 0) in the new one, so (10, 0, 100) becomes (1010, 0, 100). A conversion into an
    /// unrelated root fails: the HeadPoseUpdateFailed warning (0x004F5778), the pose is kept and not counted, and the last-observed warning (0x004F583C).
    /// </summary>
    // reach: unit — UpdateFaceOrigins called directly; the face entries and the last-observed pose are put into the FaceWorld's private stores by reflection.
    [Fact]
    public void LCO_X12_FacesUnderTheOldOriginMoveToTheNewOne()
    {
        using var s = new Setup();
        uint o1 = s.Owner.CurrentOriginId;
        s.Owner.Delocalize(false); uint o2 = s.Owner.CurrentOriginId;
        s.Owner.Delocalize(false); uint o3 = s.Owner.CurrentOriginId;      // an unrelated root
        lock (s.Owner.PoseTreeGate) { s.Origin(o2).Node!.Transform = new Transform3d(Rotation3d.Identity, new Vec3(1000, 0, 0)); s.Origin(o2).SetParent(s.Origin(o1)); }
        var faces = s.F.M.Vision.Faces;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var map = (Dictionary<int, FaceEntry>)typeof(FaceWorld).GetField("_faces", flags)!.GetValue(faces)!;
        FaceEntry Face(int id, uint origin, double x)
        {
            var entry = new FaceEntry { Id = id, HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(x, 0, 100), parent: s.Origin(origin)), LastObservedTimestamp = 5 };
            map[id] = entry; return entry;
        }
        var f4 = Face(4, o2, 10); f4.NativeTrackingTranslation = new Vec3(10, 0, 100); f4.NativeTrackingRotation = Rotation3d.Identity;
        var f1 = Face(1, o2, 20);
        var f2 = Face(2, o1, 30);                                                // not under the old origin
        typeof(FaceWorld).GetField("_nativeLastPose", flags)!.SetValue(faces, new Pose3d(Rotation3d.Identity, new Vec3(40, 0, 50), parent: s.Origin(o2)));
        typeof(FaceWorld).GetField("_nativeLastStamp", flags)!.SetValue(faces, 100u);
        int mark = s.Logs.Count;
        Assert.Equal(2, faces.UpdateFaceOrigins(o2, o1));
        var flattened = s.Logs.Skip(mark).Where(l => l.Name == "FaceWorld.UpdateFaceOrigins.FlatteningFace").ToList();
        Assert.Equal(new[] { $"Flattened FaceID:1 w.r.t. Origin{o1}", $"Flattened FaceID:4 w.r.t. Origin{o1}" }, flattened.Select(l => l.Text));
        Assert.All(flattened, l => { Assert.Equal("FaceRecognizer", l.Channel); Assert.Equal(EngineLogLevel.Debug, l.Level); });
        Assert.Same(s.Origin(o1).Node, f4.HeadPose.Node!.Parent); AssertT(f4.HeadPose.Translation, 1010, 0, 100);
        Assert.Same(s.Origin(o1).Node, f1.HeadPose.Node!.Parent); AssertT(f1.HeadPose.Translation, 1020, 0, 100);
        Assert.Same(s.Origin(o1).Node, f2.HeadPose.Node!.Parent); AssertT(f2.HeadPose.Translation, 30, 0, 100);
        var last = (Pose3d)typeof(FaceWorld).GetField("_nativeLastPose", flags)!.GetValue(faces)!;
        Assert.Same(s.Origin(o1).Node, last.Node!.Parent); AssertT(last.Translation, 1040, 0, 50);
        // A conversion into an unrelated root fails
        var f7 = Face(7, o1, 70);
        last.SetParent(s.Origin(o1));
        mark = s.Logs.Count;
        Assert.Equal(0, faces.UpdateFaceOrigins(o1, o3));
        Assert.Contains(s.Logs.Skip(mark), l => l.Name == "FaceWorld.UpdateFaceOrigins.HeadPoseUpdateFailed" && l.Text.Contains("FaceID:7"));
        Assert.Contains(s.Logs.Skip(mark), l => l.Name == "FaceWorld.UpdateFaceOrigins.UpdateLastObservedPoseFailed");
        Assert.Same(s.Origin(o1).Node, f7.HeadPose.Node!.Parent); AssertT(f7.HeadPose.Translation, 70, 0, 100);
        // timestamp 0: the last-observed pose is not touched (0x004F57xx test of +0x20)
        typeof(FaceWorld).GetField("_nativeLastStamp", flags)!.SetValue(faces, 0u);
        mark = s.Logs.Count;
        faces.UpdateFaceOrigins(o1, o3);
        Assert.DoesNotContain(s.Logs.Skip(mark), l => l.Name == "FaceWorld.UpdateFaceOrigins.UpdateLastObservedPoseFailed");
    }

    // ------------------------------------------------------------------------------------------ LocalizeToObject cross-origin (X0..X16)

    /// <summary>
    /// The whole branch. Cube 7 (E) is known in origin 1 at (150, 0, 30). A production Delocalize makes origin 2 current (MAIN identity there, +0x2C4 = 0).
    /// O is seen in origin 2 at (140, 5, 22); the computed history entry at O's time is (100, 20, 0) yaw .25 in origin 2. So N = E·O⁻¹·H = (110, 15, 8)
    /// yaw .25 in origin 1 (S2..S8; S9 skipped, 0x0051570E). X1: E's root (1) is not the current origin (2). X2: the RejiggeringOrigins event with
    /// "Robot %d's current origin is %s, about to localize to origin %s." X4: T = T_N · inverse(identity) = T_N. X5..X7: origin 2 gets T, its parent becomes
    /// origin 1, its name Origin2_REJ, and the current origin is 1. X10: cube 8 seen in origin 2 at (50, 0, 22) is now in origin 1 at
    /// N·(50, 0, 22) = (110 + 50 cos .25, 15 + 50 sin .25, 30). X11: the map for origin 2 is merged and erased, the current map origin is 1. X12: a face
    /// whose head is at (300, 0, 100) in origin 2 is at N·(300, 0, 100) in origin 1. X13: origin 2 is a direct child of 1, so no "_FLT".
    /// S12..S15 then run: MAIN = N in origin 1, [Robot+0x2B8] = 7, and the next tick sends one 0x45 in origin 1 (X16) with N's x, y and yaw.
    /// </summary>
    // reach: unit — LocalizeToObject called directly on the production-wired world. Substituted inputs: O (built by the test), the computed history entry's
    // pose, and a face entry inserted into the FaceWorld map by reflection.
    [Fact]
    public void LCO_X0_X16_ACrossOriginLocalizationMovesTheWorldIntoTheRelocalizedOrigin()
    {
        using var s = new Setup();
        uint o1 = s.Owner.CurrentOriginId;
        var e = s.F.Add(7, 150, 30); e.ActiveId = 1; e.FromDistance = 100;
        s.Owner.Delocalize(false);
        uint o2 = s.Owner.CurrentOriginId;
        Assert.NotEqual(o1, o2);
        Assert.Same(e, s.World.FindLocatedObjectHelper(new BlockWorldFilter { OriginMode = OriginMode.InAnyFrame, AllowedIds = { 7 } }, null, true));
        uint t = s.Owner.StoredState!.Timestamp;
        using (var computed = new HistRobotState())
            Assert.Equal(0u, s.Owner.PoseHistory.ComputeAndInsertKeyedState(t, computed, out _, out _));
        HistRobotState? hist = null; uint key = 0;
        Assert.Equal(0u, s.Owner.PoseHistory.GetComputedNative(t, ref hist, true, ref key));
        using (var known = new Pose3d(Rotation3d.FromAngleAxis(.25f, new Vec3(0, 0, 1)), new Vec3(100, 20, 0), parent: s.Origin(o2)))
            lock (s.Owner.PoseTreeGate) hist!.Pose.Assign(known);
        // cube 8 known in origin 2
        var c8 = new ObservableObject(8, Cube2, CubeGeometry.MarkersFor(Cube2))
        { Pose = new Pose3d(Rotation3d.Identity, new Vec3(50, 0, 22), parent: s.Origin(o2)), OriginId = o2, PoseState = PoseState.Known, LastObservedTimestamp = t };
        s.World.AddLocatedObject(c8);
        // a face seen in origin 2 (substituted entry)
        var faces = s.F.M.Vision.Faces;
        var face = new FaceEntry { Id = 3, HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(300, 0, 100), parent: s.Origin(o2)), LastObservedTimestamp = t };
        var map = (Dictionary<int, FaceEntry>)typeof(FaceWorld).GetField("_faces", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(faces)!;
        map[3] = face;
        Assert.True(s.World.Maps.Maps.ContainsKey(o2));
        var o = new ObservableObject(7, Cube1, CubeGeometry.MarkersFor(Cube1))
        { Pose = new Pose3d(Rotation3d.Identity, new Vec3(140, 5, 22), parent: s.Origin(o2)), OriginId = o2, LastObservedTimestamp = t };
        uint frame = s.Owner.PoseFrameId; s.Owner.VisionPoseUpdated = 0;
        int mark = s.F.Rig.Mark();

        Assert.Equal(0u, s.World.LocalizeToObject(o, e));

        // S10 then X2
        Assert.Equal(unchecked(frame + 1), s.Owner.PoseFrameId);
        var das = s.Log("Robot.LocalizeToObject.RejiggeringOrigins");
        Assert.Equal(EngineLogLevel.Event, das.Level);
        Assert.Equal($"Robot {(int)s.Owner.NativeRobotId}'s current origin is Origin{o2}, about to localize to origin Origin{o1}.", das.Text);
        // X5..X7
        Assert.Equal(o1, s.Owner.CurrentOriginId);
        Assert.Equal(o1, s.World.CurrentOriginId);
        var rej = s.Origin(o2);
        Assert.Same(s.Origin(o1).Node, rej.Node!.Parent);
        Assert.Equal($"Origin{o2}_REJ", rej.Name);                              // X13: a direct child, no _FLT
        AssertT(rej.Translation, 110, 15, 8);
        Assert.Equal(.25, rej.AngleAroundZ, 4);
        Assert.False(s.Logged("Robot.LocalizeToObject.RejiggerFailed"));
        // X10
        var moved = s.World.GetLocatedObjectByIdHelper(8);
        Assert.NotNull(moved);
        Assert.Equal(o1, moved!.OriginId);
        Assert.Same(s.Origin(o1).Node, moved.NativePose.Node!.Parent);
        AssertT(moved.NativePose.Translation, 110 + 50 * Math.Cos(.25), 15 + 50 * Math.Sin(.25), 30, 2);
        Assert.Empty(s.World.FindLocatedMatchingObjects(new BlockWorldFilter { OriginMode = OriginMode.Custom, AllowedOrigins = { o2 } }));
        // X11
        Assert.Equal(o1, s.World.Maps.CurrentOrigin);
        Assert.False(s.World.Maps.Maps.ContainsKey(o2));
        // X12
        Assert.Same(s.Origin(o1).Node, face.HeadPose.Node!.Parent);
        AssertT(face.HeadPose.Translation, 110 + 300 * Math.Cos(.25), 15 + 300 * Math.Sin(.25), 108, 2);
        Assert.True(s.Logged("FaceWorld.UpdateFaceOrigins.FlatteningFace"));
        // S12..S15
        using (var mainRoot = s.Owner.MainPose.FindRoot()) Assert.Equal(o1, mainRoot.Id);
        AssertT(s.Owner.MainPose.GetWithRespectToRoot().Translation, 110, 15, 8);
        Assert.Equal(7u, s.World.LocalizedToObjectId);
        Assert.Equal((byte)1, s.World.Robot2C4);
        Assert.Equal(1, s.Owner.VisionPoseUpdated);
        Assert.DoesNotContain(s.Missing, m => m.Contains("LOCALIZE-CROSS-ORIGIN-1"));
        // X16
        Assert.DoesNotContain(s.F.Rig.SentSince(mark), m => m is AbsoluteLocalizationUpdate);
        s.F.Rig.Tick();
        var sent = Assert.Single(s.F.Rig.SentSince(mark).OfType<AbsoluteLocalizationUpdate>());
        Assert.Equal(o1, sent.PoseOriginId);
        Assert.Equal(t, sent.Timestamp);
        Assert.Equal(s.Owner.PoseFrameId, sent.PoseFrameId);
        Assert.Equal(110f, sent.PoseX, 3); Assert.Equal(15f, sent.PoseY, 3); Assert.Equal(.25f, sent.PoseAngleRad, 4);
    }
    /// <summary>
    /// Production reach: the cube localizes the robot in origin 1 through real rendered frames (as LocalizeToObjectSameOriginTests'
    /// M11_044_ALiveSightingOfAConnectedCubeLocalizesThroughTheNativePath), the robot delocalizes into origin 2, and the same cube is seen again. The engine
    /// matches the sighting to the cube known in origin 1, so LocalizeToObject takes the cross-origin branch: Rejigger makes origin 1 current again with origin 2
    /// under it, named Origin2_REJ (X5..X7), and the sighting's 0x45 carries origin 1 (X16: the vision-only state N is rooted at E's origin).
    /// </summary>
    // reach: production — real rendered frames through the queued worker, as the same-origin production test (the offline flag cleared by reflection, the
    // fixture's sim firmware report and outer calibration). Substitution: the delocalization is a direct call of the production Robot::Delocalize
    // (EngineRobot.Delocalize) instead of a robot-state trigger (pick-up / frame mismatch), which this rig does not drive. With pathInFlight the
    // PathComponent's in-flight test is substituted (stack machinery: the visible M12-032 RejiggerTargetsAndReplan gap, verifier F1); without it the
    // production binding (Motion.Path.IsActive, no path) is used and the gap is not reported.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LCO_Production_ADelocalizedRobotThatSeesAKnownCubeRelocalizesIntoItsOrigin(bool pathInFlight)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var missing = new System.Collections.Concurrent.ConcurrentQueue<string>();
        void Missing(string line) => missing.Enqueue(line);
        var logs = new System.Collections.Concurrent.ConcurrentQueue<EngineLogEntry>();
        f.Rig.Engine.LogEntry += logs.Enqueue;
        SteppedBehavior.MissingReported += Missing;
        try
        {
            M4ControlTests.ConnectCube(f.Rig, slot: 0);
            var world = f.M.World;
            f.Image(1, cube: true, cubeX: 150); f.Motors();
            typeof(CozmoEngine).GetProperty("IsOfflineEngine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(f.Robot.Engine, false);
            f.Image(2, cube: true, cubeX: 150); f.Motors();
            uint? cubeId = world.ConnectedObjectIdForActiveId(0);
            Assert.NotNull(cubeId);
            Assert.Equal(cubeId, world.LocalizedToObjectId);
            uint o1 = f.Robot.CurrentOriginId;
            f.Robot.Delocalize(false);
            f.Motors();
            uint o2 = f.Robot.CurrentOriginId;
            Assert.NotEqual(o1, o2);
            Assert.Null(world.LocalizedToObjectId);
            if (pathInFlight) world.PathInFlight = () => true;
            bool relocalized = false;
            List<AbsoluteLocalizationUpdate> sent = new();
            for (uint i = 3; i <= 6 && !relocalized; i++)
            {
                int mark = f.Rig.Mark();
                f.Image(i, cube: true, cubeX: 150); f.Motors();
                sent = f.Rig.SentSince(mark).OfType<AbsoluteLocalizationUpdate>().ToList();
                relocalized = f.Robot.CurrentOriginId == o1;
            }
            Assert.True(relocalized, "the robot did not relocalize into the cube's origin; logs: " +
                string.Join(" | ", logs.Select(l => l.Name).Where(n => n is not null && n.Contains("Localize")).TakeLast(10)) + " missing: " + string.Join(" | ", missing.TakeLast(5)));
            Assert.Contains(logs, l => l.Name == "Robot.LocalizeToObject.RejiggeringOrigins");
            Assert.Equal($"Origin{o2}_REJ", f.Robot.GetNativeOrigin(o2).Name);
            Assert.Same(f.Robot.GetNativeOrigin(o1).Node, f.Robot.GetNativeOrigin(o2).Node!.Parent);
            Assert.Equal(o1, world.CurrentOriginId);
            Assert.Equal(cubeId, world.LocalizedToObjectId);
            var update = Assert.Single(sent);
            Assert.Equal(o1, update.PoseOriginId);
            Assert.Equal(f.Robot.PoseFrameId, update.PoseFrameId);
            Assert.DoesNotContain(missing, m => m.Contains("LOCALIZE-CROSS-ORIGIN-1"));
            Assert.Equal(pathInFlight ? 1 : 0, missing.Count(m => m.Contains("M12-032 RejiggerTargetsAndReplan 0x006499D0 not built") && m.Contains($"{o2} -> {o1}")));
        }
        finally { SteppedBehavior.MissingReported -= Missing; f.Rig.Engine.LogEntry -= logs.Enqueue; }
    }
}
