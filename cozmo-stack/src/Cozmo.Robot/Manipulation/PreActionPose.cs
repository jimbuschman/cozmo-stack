using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// <c>Anki::Cozmo::PreActionPose::ActionType</c>: the jump table in <c>Block::GeneratePreActionPoses</c>
/// (0x004E5958, <c>tbh</c> on 0..5) and <c>DriveToObjectAction::InitHelper</c>'s <c>cmp r0, #6</c>
/// ("ActionType==NONE but no distance set either") give the values; the names are Anki's from the pre-action
/// pose visualisation colours and the actions that ask for each type (INFERRED names, NATIVE values).
/// </summary>
public enum PreActionType : byte { Docking = 0, PlaceRelative = 1, PlaceOnGround = 2, Entry = 3, Rolling = 4, Flipping = 5, None = 6 }

/// <summary>A pose the robot drives to before acting on an object: where, for which marker, and for what.</summary>
// fidelity: M12-001
public sealed record PreActionPose(PreActionType Type, KnownMarker Marker, Pose3d WorldPose, double DistanceMm)
{
    /// <summary>
    /// The built <c>Pose3d(angle, Z_AXIS, translation)</c>'s translation, in the marker's frame (C-E7):
    /// Docking/Rolling <c>(0, -65, -size.z/2)</c>; PlaceRelative <c>(0, -100, -size.z/2)</c>; PlaceOnGround
    /// <c>(0, -49, -size.z/2)</c>; Flipping <c>(size.y/2 + 56.5771, -56.5771, -size.z/2)</c>. The <c>sb</c>
    /// Y rotation is applied on top of it (see <see cref="CubePreActionPoses.RotateBy"/>); it is a different
    /// thing from <see cref="DistanceMm"/>.
    /// </summary>
    public Vec3 LocalOffsetMm { get; init; }
    /// <summary>The built <c>Pose3d</c> angle about Z: pi/2 for types 0/1/2/4, 3pi/4 for Flipping (C-E7).</summary>
    public double LocalAngleRad { get; init; }
    /// <summary>The inner <c>sb</c> index 0..3: the Y rotation 0, pi/2, pi, -pi/2 that generated this pose (C-E7).</summary>
    public int RotationIndex { get; init; }

    public override string ToString() => $"{Type} via {Marker.Code} at {WorldPose}";
}

/// <summary>
/// One 16-byte <c>Block::BlockFaceDef_t</c> record, verbatim from the rodata tables the engine copies into
/// <c>Block::LookupBlockInfo</c>'s per-object face-def vector (M12-021, H1): FaceName at +0, the
/// <c>Vision::MarkerType</c> code at +4, size 25.0 at +8, and the two per-face, per-rotation gate masks at
/// +0xC (action types 0 and 5) and +0xD (type 4).
/// </summary>
public readonly record struct BlockFaceDef(BlockFace Face, int MarkerCode, double SizeMm, byte MaskForTypes0And5, byte MaskForType4);

/// <summary>
/// <c>Block::GeneratePreActionPoses</c> (0x004E5808..0x004E5D40), NATIVE geometry.
///
/// The engine clears the output, reads the block size, then <c>tbh</c>-dispatches on <c>actionType</c> 0..5
/// (0x004E595E, table 0x004E5962); anything above 5 produces nothing, and type 3 (Entry) jumps straight to
/// the loop tail, so it produces nothing either. For the types that build poses it walks the six face-def
/// records of the block info in engine vector order (<see cref="LightCubeFaceDefs"/>: Front, Back, Left,
/// Right, Top, Bottom), gets each face's marker, and, for each <c>sb</c> in 0..3 (Y rotations 0, pi/2, pi,
/// -pi/2; table 0x010590AC, C-E7), tests <c>(1&lt;&lt;sb) &amp; mask</c>: the +0xC mask for types 0 and 5, the +0xD mask for type 4.
/// Types 1 and 2 are ungated.
///
/// Each branch builds <c>Pose3d(angle, Z_AXIS, translation)</c> with the marker as parent and then rotates it
/// about Y by <c>sb·pi/2</c> with <c>Transform3d::RotateBy</c> 0x0084BB3A (C-E7; see
/// <see cref="BuiltPoseFor"/>). The fourth ctor argument, stored at <c>PreActionPose+0x18</c> (0x0050DD46),
/// is <see cref="PreActionPose.DistanceMm"/>: 75.0 for Docking, 40.0 for PlaceRelative, 0 for PlaceOnGround,
/// 75.0 for Rolling, 0 for Flipping.
///
/// The world-space pose is composed by the copy-with-pose ctor
/// <c>PreActionPose(PreActionPose const&amp;, Pose3d const&amp;, float, float)</c> 0x0050DF00, called from
/// <c>ActionableObject::GetCurrentPreActionPoses</c> 0x004DF850 (E1): see
/// <see cref="ComposeWorld"/>. The returned pose's translation is the b=0 stored translation pushed out
/// along its own direction by <c>b = min(dist2, element+0x18)</c>, where <c>dist2</c> is the perpendicular
/// projection of the robot's world pose onto the offset line.
/// </summary>
public static class CubePreActionPoses
{
    /// <summary>The 4th ctor argument (<c>PreActionPose+0x18</c>) for Docking: 75.0 (0x42960000 at 0x004E5A36).</summary>
    public const double DockingDistanceMm = 75.0;
    /// <summary>PlaceRelative: 40.0 (0x42200000 at 0x004E5ADA).</summary>
    public const double PlaceRelativeDistanceMm = 40.0;
    /// <summary>PlaceOnGround: 0 (0x004E5B5A/0x004E5B80).</summary>
    public const double PlaceOnGroundDistanceMm = 0.0;
    /// <summary>Rolling: 75.0 (0x42960000 at 0x004E5C50).</summary>
    public const double RollingDistanceMm = 75.0;
    /// <summary>Flipping: 0 (0x004E5D04/0x004E5D28).</summary>
    public const double FlippingDistanceMm = 0.0;

    /// <summary>The built PlaceRelative pose translation's Y: -100.0 (0x004E5A70).</summary>
    public const double PlaceRelativeOffsetMm = -100.0;
    /// <summary>The built PlaceOnGround pose translation's Y: -49.0 (0xC2440000 at 0x004E5B0C).</summary>
    public const double PlaceOnGroundOffsetMm = -49.0;
    /// <summary>
    /// The Flipping corner's 56.5771 as the engine's binary32: 0x42624EEF in the rodata float at 0x004E5C60 (added to size.x/2 with
    /// <c>vadd.f32</c> at 0x004E5934) and 0xC2624EEF built by <c>movw/movt</c> at 0x004E5CB2..0x004E5CBC (the Y). NOT the double 56.5771
    /// (binary32 0x42624EF3).
    /// </summary>
    // fidelity: M12-001
    public static readonly double FlippingCornerMm = BitConverter.UInt32BitsToSingle(0x42624EEF);
    /// <summary>The built pose's Z rotation for Docking/PlaceRelative/PlaceOnGround/Rolling: pi/2 (C-E7).</summary>
    public const double DockingAngleRad = Math.PI / 2;
    /// <summary>The built pose's Z rotation for Flipping: 3pi/4 (C-E7).</summary>
    public const double FlippingAngleRad = 3 * Math.PI / 4;

    /// <summary>
    /// <c>operator&gt;(angleTolerance, Radians(0))</c> (0x0084CC90) uses the ~1e-5 rad epsilon 0x3727C5AC,
    /// so the positivity guard is <c>angleTolerance &gt; 1e-5</c>.
    /// </summary>
    public static readonly double DistanceThresholdEpsilonRad = BitConverter.UInt32BitsToSingle(0x3727C5AC);
    /// <summary>The -1.0f the threshold writes when the guard fails (0x005501AE).</summary>
    public const double DistanceThresholdSentinel = -1.0;
    /// <summary>The 1e-5 (0x3727C5AC) the <c>GetCurrentPreActionPoses</c> offset geometry compares dx/dy against (E1).</summary>
    public static readonly double OffsetEpsilon = BitConverter.UInt32BitsToSingle(0x3727C5AC);

    // ------------------------------------------------------------------ M12-021 face-def records
    // The engine copies these verbatim from rodata into LookupBlockInfo's per-object vector. The vector order
    // is Front, Back, Left, Right, Top, Bottom - different from CubeGeometry.FaceTable's order.

    /// <summary>LIGHTCUBE1 @0x00C45C40 (copy loop 0x004E4D12): +0xC = 0x05 on FaceNames 0..3, 0x00 on 4, 0x0F on 5; +0xD = 0x0F everywhere.</summary>
    // fidelity: M12-021
    public static readonly BlockFaceDef[] LightCube1FaceDefs =
    {
        new(BlockFace.Front,  6, 25.0, 0x05, 0x0F),
        new(BlockFace.Back,   4, 25.0, 0x05, 0x0F),
        new(BlockFace.Left,   7, 25.0, 0x05, 0x0F),
        new(BlockFace.Right,  8, 25.0, 0x05, 0x0F),
        new(BlockFace.Top,    9, 25.0, 0x00, 0x0F),
        new(BlockFace.Bottom, 5, 25.0, 0x0F, 0x0F),
    };

    /// <summary>LIGHTCUBE2 @0x00C45CA0 (copy loop 0x004E4DD2): same masks, marker codes +6.</summary>
    // fidelity: M12-021
    public static readonly BlockFaceDef[] LightCube2FaceDefs =
    {
        new(BlockFace.Front,  12, 25.0, 0x05, 0x0F),
        new(BlockFace.Back,   10, 25.0, 0x05, 0x0F),
        new(BlockFace.Left,   13, 25.0, 0x05, 0x0F),
        new(BlockFace.Right,  14, 25.0, 0x05, 0x0F),
        new(BlockFace.Top,    15, 25.0, 0x00, 0x0F),
        new(BlockFace.Bottom, 11, 25.0, 0x0F, 0x0F),
    };

    /// <summary>LIGHTCUBE3 @0x00C45D00 (copy loop 0x004E4E88): same masks, marker codes +12.</summary>
    // fidelity: M12-021
    public static readonly BlockFaceDef[] LightCube3FaceDefs =
    {
        new(BlockFace.Front,  18, 25.0, 0x05, 0x0F),
        new(BlockFace.Back,   16, 25.0, 0x05, 0x0F),
        new(BlockFace.Left,   19, 25.0, 0x05, 0x0F),
        new(BlockFace.Right,  20, 25.0, 0x05, 0x0F),
        new(BlockFace.Top,    21, 25.0, 0x00, 0x0F),
        new(BlockFace.Bottom, 17, 25.0, 0x0F, 0x0F),
    };

    /// <summary>LIGHTCUBE_GHOST @0x00C45D60 (copy loop 0x004E4F3C): 0x0F/0x0F on every face, marker 39.</summary>
    // fidelity: M12-021
    public static readonly BlockFaceDef[] GhostFaceDefs =
    {
        new(BlockFace.Front,  39, 25.0, 0x0F, 0x0F),
        new(BlockFace.Back,   39, 25.0, 0x0F, 0x0F),
        new(BlockFace.Left,   39, 25.0, 0x0F, 0x0F),
        new(BlockFace.Right,  39, 25.0, 0x0F, 0x0F),
        new(BlockFace.Top,    39, 25.0, 0x0F, 0x0F),
        new(BlockFace.Bottom, 39, 25.0, 0x0F, 0x0F),
    };

    /// <summary>The face-def vector for an object type, in the engine's vector order.</summary>
    public static IReadOnlyList<BlockFaceDef> FaceDefsFor(ObjectType type) => type switch
    {
        ObjectType.Block_LIGHTCUBE1 => LightCube1FaceDefs,
        ObjectType.Block_LIGHTCUBE2 => LightCube2FaceDefs,
        ObjectType.Block_LIGHTCUBE3 => LightCube3FaceDefs,
        ObjectType.Block_LIGHTCUBE_GHOST => GhostFaceDefs,
        _ => Array.Empty<BlockFaceDef>(),
    };

    /// <summary>The 4th ctor argument for an action type (0 for the types that produce nothing).</summary>
    public static double DistanceFor(PreActionType type) => type switch
    {
        PreActionType.Docking => DockingDistanceMm,
        PreActionType.PlaceRelative => PlaceRelativeDistanceMm,
        PreActionType.PlaceOnGround => PlaceOnGroundDistanceMm,
        PreActionType.Rolling => RollingDistanceMm,
        PreActionType.Flipping => FlippingDistanceMm,
        _ => 0.0,
    };

    /// <summary>
    /// The per-type <c>Pose3d(angle, Z_AXIS, translation)</c> that <c>Block::GeneratePreActionPoses</c>
    /// builds, before the <c>sb</c> Y rotation (C-E7). The translation's third component is
    /// <c>-size.z/2</c> (-22 for a cube; 0x004E5938), and the marker is the built pose's parent.
    /// </summary>
    public static (Vec3 Translation, double Angle) BuiltPoseFor(PreActionType type, Vec3 size)
    {
        double z = -size.Z / 2;
        return type switch
        {
            PreActionType.Docking => (new Vec3(0, -65, z), DockingAngleRad),
            PreActionType.PlaceRelative => (new Vec3(0, PlaceRelativeOffsetMm, z), DockingAngleRad),
            PreActionType.PlaceOnGround => (new Vec3(0, PlaceOnGroundOffsetMm, z), DockingAngleRad),
            PreActionType.Rolling => (new Vec3(0, -65, z), DockingAngleRad),
            // 0x004E583A (vldr s16,[r0,#4] = the SECOND size component) / 0x004E592C/0x004E5934: s18 = size.y * 0.5f (vmul.f32) then + 56.577f (vadd.f32): float arithmetic, then the stored float
            PreActionType.Flipping => (new Vec3((double)((float)size.Y * 0.5f + (float)FlippingCornerMm), -FlippingCornerMm, z), FlippingAngleRad),
            _ => (new Vec3(0, 0, 0), 0),
        };
    }

    /// <summary>One element of <c>Block::GeneratePreActionPoses</c>'s output: the pose stored in the object's cache vector (marker-local, before the push-out).</summary>
    public sealed record StoredElement(KnownMarker Marker, Pose3d Stored, Vec3 BuiltTranslation, double BuiltAngle, int Sb);

    /// <summary>
    /// <c>Block::GeneratePreActionPoses</c> 0x004E5808 for one action type (M12-001, M12-021): the cache vector's elements in the engine's order
    /// (face-def vector order x <c>sb</c> 0..3), gated by the masks. Types 3 (Entry) and above 5 give none.
    /// </summary>
    // fidelity: M12-001, M12-021
    public static List<StoredElement> GenerateStored(ObservableObject cube, PreActionType type)
    {
        var list = new List<StoredElement>();
        if (type is PreActionType.Entry or PreActionType.None) return list;
        var size = CubeGeometry.SizeOf(cube.Type);
        foreach (var def in FaceDefsFor(cube.Type))
        {
            var marker = cube.Markers.FirstOrDefault(m => m.Face == def.Face);
            if (marker is null) continue;
            for (int sb = 0; sb < 4; sb++)
            {
                if (!Enabled(type, def, sb)) continue;
                var (builtTranslation, builtAngle) = BuiltPoseFor(type, size);
                // Block::GeneratePreActionPoses builds Pose3d(builtAngle, Z_AXIS, builtTranslation) with the marker
                // as parent, then rotates it about Y by sb*pi/2 with Transform3d::RotateBy 0x0084BB3A (C-E7). The
                // PreActionPose ctor re-roots to the marker's parent, so stored = marker.PoseOnObject ∘ built.
                var built = new Pose3d(builtAngle, new Vec3(0, 0, 1), builtTranslation);
                var rotated = RotateBy(built, new Vec3(0, 1, 0), sb * (Math.PI / 2));
                list.Add(new StoredElement(marker, marker.PoseOnObject.Compose(rotated), builtTranslation, builtAngle, sb));
            }
        }
        return list;
    }

    /// <summary>
    /// The pre-action poses of a located cube for an action type, in the world frame, BEFORE the validity filter of
    /// <c>ActionableObject::GetCurrentPreActionPoses</c> (<see cref="GetCurrentPreActionPoses"/> applies it).
    ///
    /// <paramref name="robotPose"/> is the engine's <c>param_2</c> (the robot's world pose, E8): the returned
    /// pose is pushed out along the stored direction by <c>b</c>, which when
    /// <paramref name="preDockPoseOffsetMm"/> is 0 is <c>min(dist2, DistanceMm)</c>, where <c>dist2</c> is the
    /// perpendicular projection of the robot pose onto the offset line (E1). Every path this stack builds
    /// passes 0 (E7).
    /// </summary>
    // fidelity: M12-001, M12-021
    public static IReadOnlyList<PreActionPose> For(ObservableObject cube, PreActionType type, Pose3d robotPose,
                                                   double preDockPoseOffsetMm = 0)
    {
        var poses = new List<PreActionPose>();
        foreach (var e in GenerateStored(cube, type)) poses.Add(Compose(cube, e, type, robotPose, preDockPoseOffsetMm));
        return poses;
    }

    private static PreActionPose Compose(ObservableObject cube, StoredElement e, PreActionType type, Pose3d robotPose, double preDockPoseOffsetMm)
    {
        var world = ComposeWorld(cube.Pose, e.Stored, robotPose, DistanceFor(type), preDockPoseOffsetMm);
        return new PreActionPose(type, e.Marker, world, DistanceFor(type))
        {
            LocalOffsetMm = e.BuiltTranslation,
            LocalAngleRad = e.BuiltAngle,
            RotationIndex = e.Sb,
        };
    }

    /// <summary>The per-object cache vectors at <c>this+0x28+12*type</c> (<c>ActionableObject::GetCurrentPreActionPoses</c>): generated by virtual slot 2 when empty.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ObservableObject, Dictionary<PreActionType, List<StoredElement>>> Caches = new();

    /// <summary>
    /// <c>ActionableObject::GetCurrentPreActionPoses</c> 0x004DF850 (M12-001, M12-036): iterates <paramref name="withAction"/> in ASCENDING ActionType order (an empty set
    /// collects nothing and returns 0); per type the cache vector is generated by <c>Block::GeneratePreActionPoses</c> when it is empty; an element is skipped when
    /// <paramref name="withCode"/> is non-empty and does not hold its marker code; the world pose is <c>PreActionPose(elem, objectPose, a = elem+0x18, b)</c>
    /// (<see cref="ComposeWorld"/>); it is appended only when the object's virtual <c>IsPreActionPoseValid</c> (<see cref="PreActionValidity"/>) returns exactly 1. Nothing
    /// else drops a pose and nothing is sorted: the order is (ascending ActionType) x (cache order) x (survivors). The return is <c>sb &amp; 1</c> (0x004DFDD4): true when
    /// some type's cache vector was empty and had to be generated during this call, NOT whether a pose survived. The visualisation block (<c>param_8</c>) is not modelled.
    /// </summary>
    // fidelity: M12-001, M12-036
    public static bool GetCurrentPreActionPoses(ObservableObject obj, IReadOnlyCollection<PreActionType> withAction, IReadOnlyCollection<MarkerType> withCode,
                                                Pose3d robotPose, double preDockPoseOffsetMm, IReadOnlyList<PreActionObstacle> obstacles,
                                                List<PreActionPose> output, List<string>? trace = null)
    {
        bool regenerated = false;
        var caches = Caches.GetOrCreateValue(obj);
        foreach (var type in withAction.Distinct().OrderBy(t => (int)t))
        {
            List<StoredElement> cache;
            lock (caches)
            {
                if (!caches.TryGetValue(type, out cache!)) caches[type] = cache = new List<StoredElement>();
                if (cache.Count == 0) { regenerated = true; cache.AddRange(GenerateStored(obj, type)); }
                cache = cache.ToList();
            }
            foreach (var e in cache)
            {
                if (withCode.Count > 0 && !withCode.Contains(e.Marker.Code)) continue;
                var pose = Compose(obj, e, type, robotPose, preDockPoseOffsetMm);
                if (PreActionValidity.IsPreActionPoseValid(obj, pose, obstacles, trace)) output.Add(pose);
            }
        }
        return regenerated;
    }

    /// <summary>The gate at 0x004E5976/0x004E5B94/0x004E5C80: <c>(1&lt;&lt;sb) &amp; mask</c>, only for types 0, 4 and 5.</summary>
    public static bool Enabled(PreActionType type, BlockFaceDef def, int sb) => type switch
    {
        PreActionType.Docking or PreActionType.Flipping => (def.MaskForTypes0And5 & (1 << sb)) != 0,
        PreActionType.Rolling => (def.MaskForType4 & (1 << sb)) != 0,
        _ => true,
    };

    /// <summary>
    /// <c>Transform3d::RotateBy</c> 0x0084BB3A: the translation is rotated by the matrix and the rotation is
    /// pre-multiplied by it.
    /// </summary>
    public static Pose3d RotateBy(Pose3d pose, Vec3 axis, double angleRad)
    {
        var r = Mat3.AxisAngle(axis, angleRad);
        return new Pose3d(r * pose.Rotation, r * pose.Translation);
    }

    /// <summary>
    /// <c>ActionableObject::GetCurrentPreActionPoses</c> 0x004DF850 / the copy-with-pose ctor
    /// <c>PreActionPose(PreActionPose const&amp;, Pose3d const&amp;, float, float)</c> 0x0050DF00 (E1):
    /// <c>world = objectPose ∘ Pose3d(stored.rotation, unit(stored.translation)·(|stored.translation| + b))</c>.
    /// <paramref name="preDockPoseOffsetMm"/> is the ctor's <c>b</c> when non-zero; otherwise <c>b</c> is
    /// <c>min(dist2, distanceMm)</c>, with <c>dist2</c> the perpendicular projection of
    /// <paramref name="robotPose"/> onto the offset line through the b=0 pose in the direction
    /// <c>(cos(angle), sin(angle))·distanceMm</c> (0x004DF9B8..0x004DFC16).
    /// </summary>
    // fidelity: M12-001
    public static Pose3d ComposeWorld(Pose3d objectPose, Pose3d stored, Pose3d robotPose, double distanceMm,
                                      double preDockPoseOffsetMm)
    {
        double b;
        if (preDockPoseOffsetMm != 0)
        {
            b = preDockPoseOffsetMm;
        }
        else
        {
            double d = distanceMm;
            var w0 = objectPose.Compose(stored);
            double ang = w0.AngleAroundZ;
            double dx = Math.Cos(ang) * d;
            double dy = Math.Sin(ang) * d;
            double px, py;
            if (Math.Abs(dx) < OffsetEpsilon)
            {
                px = 0;
                py = robotPose.Translation.Y - w0.Translation.Y;
            }
            else if (Math.Abs(dy) < OffsetEpsilon)
            {
                px = robotPose.Translation.X - w0.Translation.X;
                py = 0;
            }
            else
            {
                double m = dy / dx;
                double a = robotPose.Translation.Y + robotPose.Translation.X / m - w0.Translation.Y + m * w0.Translation.X;
                double q = a / (m + 1 / m);
                px = q - w0.Translation.X;
                py = (robotPose.Translation.Y + robotPose.Translation.X / m - q / m) - w0.Translation.Y;
            }
            double dist2 = Math.Sqrt(px * px + py * py);
            b = dist2 >= d ? d : (dist2 > 0 ? dist2 : 0);
        }
        double len = stored.Translation.Length;
        var pushed = len <= 0 ? stored.Translation : stored.Translation.Normalized() * (len + b);
        return objectPose.Compose(new Pose3d(stored.Rotation, pushed));
    }

    /// <summary>
    /// <c>ComputePreActionPoseDistThreshold(out, Pose3d r1, Pose3d r2, Radians r3)</c> (0x00550098) with r1 the goal / pre-action
    /// pose, r2 the object pose and r3 the angle (M12-020: all three callers 0x0055ACF0, 0x005561A0, 0x00550FF8 pass them in that order).
    /// It computes <c>r2.GetWithRespectTo(r1)</c>, the SECOND pose in the FIRST pose's frame (0x005500EE..0x005500F4).
    ///
    /// The distance is the <b>3-D</b> norm of the relative transform's translation (0x00550102..0x00550122,
    /// <c>vsqrt</c>), not the planar X/Y distance. It writes two outputs: <c>out[0] = 2·dist·sin(tol)</c>
    /// (0x00550164/0x005501A4) and <c>out[1] = dist·sin(tol)</c> (0x005501A8). The only tolerance branch is
    /// the positivity guard <c>angleTolerance &gt; Radians(0)</c> (0x005500BC, with <c>operator&gt;</c>'s
    /// ~1e-5 rad epsilon); when it is false the function writes the -1.0f sentinel pair and returns
    /// (0x005501AE). There is <b>no floor</b> substituted for the tolerance. A <c>GetWithRespectTo</c>
    /// failure also writes the -1.0f pair (0x005501C0); the C# transform never fails, so that path is
    /// unreachable here.
    ///
    /// Returns false when the guard rejected the tolerance; both outputs are then
    /// <see cref="DistanceThresholdSentinel"/>.
    /// </summary>
    // fidelity: M12-001, M12-020
    public static bool DistanceThresholdMm(Pose3d preActionPose, Pose3d objectPose, double angleToleranceRad,
                                           out double thresholdTwice, out double thresholdOnce)
    {
        // 0x005500B8..0x005500C2: operator>(Radians tol, Radians(0)) 0x0084CC90 = (tol - 0 > 0) && !IsNear(tol, 0, 0x3727C5AC); binary32 throughout
        float tol = (float)angleToleranceRad;
        if (!(tol > 0f && !EngineRadians.IsNear(tol, 0.0, DistanceThresholdEpsilonRad)))
        {
            thresholdTwice = DistanceThresholdSentinel;
            thresholdOnce = DistanceThresholdSentinel;
            return false;
        }
        var d = objectPose.WithRespectTo(preActionPose).Translation;
        // 0x00550102..0x00550122: vmul.f32 / vadd.f32 in the order ((x*x + y*y) + z*z), then vsqrt.f32 (NaN: sqrtf)
        float fx = (float)d.X, fy = (float)d.Y, fz = (float)d.Z;
        float sum = fx * fx;
        sum = sum + fy * fy;
        sum = sum + fz * fz;
        float dist = MathF.Sqrt(sum);
        // 0x00550140..0x0055014E: sinf(tol) then vmul.f32; 0x00550164: out0 = s16 + s16 (vadd.f32); 0x005501A8: out1 = s16
        float once = dist * MathF.Sin(tol);
        float twice = once + once;
        thresholdOnce = once;
        thresholdTwice = twice;
        return true;
    }

    /// <summary>The pre-action pose nearest the robot (<c>DriveToObjectAction::GetClosestPreDockPose</c>).</summary>
    public static PreActionPose? Closest(IReadOnlyList<PreActionPose> poses, Pose3d robot)
    {
        PreActionPose? best = null; double bestD = double.MaxValue;
        foreach (var p in poses)
        {
            var d = p.WorldPose.Translation - robot.Translation;
            double dist = d.X * d.X + d.Y * d.Y + d.Z * d.Z;
            if (dist < bestD) { bestD = dist; best = p; }
        }
        return best;
    }
}

/// <summary>
/// The engine's <c>Radians</c> comparisons the placement path uses (M12-033; re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q3,
/// verified in 20260929-R-VIS-verify-M12-gap2.md). <c>Radians::rescale</c> 0x0084C87C keeps the value in (-pi, pi]; a value already in
/// range is unchanged, a value with |v| &lt; 10 is stepped by 2pi, a larger one uses <c>v - ceil(v/2pi - 0.5)*2pi</c>, NaN passes through.
/// <c>Radians::IsNear</c> 0x0084CC0A rescales the difference and is a STRICT <c>|d| &lt; |tol|</c>: the tolerance's sign is ignored and NaN is
/// false. The float constants are the report's "-pi (0x0084C938), +pi (0x0084C93C), 2pi (0x0084C940), -2pi (0x0084C944)"; their bit patterns
/// are taken to be the standard floats of those values (the report gives the addresses, not the bits).
/// </summary>
// fidelity: M12-033
public static class EngineRadians
{
    public static readonly double Pi = BitConverter.UInt32BitsToSingle(0x40490FDB);
    public static readonly double NegPi = BitConverter.UInt32BitsToSingle(0xC0490FDB);
    public static readonly double TwoPi = BitConverter.UInt32BitsToSingle(0x40C90FDB);
    public static readonly double NegTwoPi = BitConverter.UInt32BitsToSingle(0xC0C90FDB);

    /// <summary><c>Radians::rescale</c> 0x0084C87C.</summary>
    public static double Rescale(double v)
    {
        if (double.IsNaN(v)) return v;
        if (v > NegPi && v <= Pi) return v;
        if (Math.Abs(v) < 10.0)
        {
            while (v <= NegPi) v += TwoPi;
            while (v > Pi) v += NegTwoPi;
            return v;
        }
        return v - Math.Ceiling(v / TwoPi - 0.5) * TwoPi;
    }

    /// <summary><c>Radians::IsNear(this, other, tol)</c> 0x0084CC0A: <c>|rescale(this - other)| &lt; |tol|</c>, strictly; NaN gives false.</summary>
    public static bool IsNear(double angle, double other, double tolerance) =>
        Math.Abs(Rescale(Rescale(angle) - Rescale(other))) < Math.Abs(tolerance);

    /// <summary>
    /// <c>Rotation3d::GetAngleAroundZaxis</c> 0x0084AA1C (matrix version 0x0084B424..0x0084B47C): <c>atan2f(R10, R00)</c> when
    /// <c>R10^2 + R00^2 &gt; R01^2 + R11^2</c>, otherwise <c>atan2f(-R01, R11)</c>; the result goes through <c>Radians(float)</c>.
    /// </summary>
    public static double GetAngleAroundZaxis(Mat3 r)
    {
        double r00 = r[0, 0], r10 = r[1, 0], r01 = r[0, 1], r11 = r[1, 1];
        double angle = r10 * r10 + r00 * r00 > r01 * r01 + r11 * r11 ? Math.Atan2(r10, r00) : Math.Atan2(-r01, r11);
        return Rescale(angle);
    }
}

/// <summary>
/// The two <c>ObservableObject</c> queries <c>ComputePlaceRelObjectOffsetPoses</c> 0x005558F4 and
/// <c>TransformPlacementOffsetsRelativeObject</c> 0x00554E40 make (M12-033, verified in 20260929-R-VIS-verify-M12-gap2.md Q1/Q2):
///
/// <para><c>GetZRotatedPointAboveObjectCenter(f)</c> 0x00877574: <c>L</c> is the object's pose re-expressed against the root of its pose tree (via
/// 0x004DF628), <c>dimZ = GetDimInParentFrame&lt;'Z'&gt;</c> (0x00557794), <c>yaw = GetAngleAroundZaxis(L)</c>, <c>T = (this.pose.x, this.pose.y,
/// L.z + dimZ * f)</c> and the result is <c>Pose3d(yaw, Z_AXIS, T, parent = root)</c>: <c>f</c> multiplies the extent (0 the centre, 0.5 the top face) and
/// only the yaw of the rotation is kept. This stack has no pose tree (every pose is a world-origin pose), so the root re-expression is the identity and the
/// engine's "un-converted x, y" and "root-relative z, yaw" coincide (the record's frame-mixing caveat cannot show here). The axis choice of
/// <c>GetDimInParentFrame&lt;'Z'&gt;</c> reuses <see cref="CubeGeometry.DimInParentFrameZ"/> (M12-012's reading); M12-033's own text selects the size
/// component by the signed axis <c>GetRotatedParentAxis</c> returns, and for a cube (44 mm on every axis) the two cannot differ.</para>
///
/// <para><c>GetClosestMarkerPose(pose, planar, out, marker)</c> 0x00877774: over the object's marker list (in order) the marker pose with respect to
/// <paramref name="pose"/>; the distance is <c>x^2 + y^2</c> when planar is 1, else 3-D; the strictly smallest wins from 0x7F7FFFFF (float max, NaN never
/// wins); its code is stored and the result is 0, else 0x06000000. The input marker is never read.</para>
///
/// The record fields are function values so that a test can supply a query as an INPUT, but the default is <see cref="Engine"/>.
/// </summary>
// fidelity: M12-033
public sealed record ObjectPoseQueries(Func<ObservableObject, double, Pose3d> ZRotatedPointAboveObjectCenter,
                                       Func<ObservableObject, Pose3d, MarkerType?> ClosestMarkerCode)
{
    /// <summary>The engine's queries (M12-033).</summary>
    public static readonly ObjectPoseQueries Engine = new(GetZRotatedPointAboveObjectCenter, (o, p) => GetClosestMarkerPose(o, p, planar: true, out _));

    /// <summary><c>ObservableObject::GetZRotatedPointAboveObjectCenter(float)</c> 0x00877574.</summary>
    public static Pose3d GetZRotatedPointAboveObjectCenter(ObservableObject obj, double f)
    {
        var l = obj.Pose;
        double dimZ = CubeGeometry.DimInParentFrameZ(obj);
        double yaw = EngineRadians.GetAngleAroundZaxis(l.Rotation);
        var t = new Vec3(obj.Pose.Translation.X, obj.Pose.Translation.Y, l.Translation.Z + dimZ * f);
        return new Pose3d(yaw, new Vec3(0, 0, 1), t);
    }

    /// <summary><c>ObservableObject::GetClosestMarkerPose</c> 0x00877774: the closest marker's code and pose (with respect to <paramref name="pose"/>), or null (result 0x06000000).</summary>
    public static MarkerType? GetClosestMarkerPose(ObservableObject obj, Pose3d pose, bool planar, out Pose3d markerWrtPose)
    {
        markerWrtPose = default;
        MarkerType? best = null;
        double bestD2 = float.MaxValue;                                        // 0x7F7FFFFF (0x00877914)
        foreach (var m in obj.Markers)
        {
            var wrt = obj.Pose.Compose(m.PoseOnObject).WithRespectTo(pose);    // GetWithRespectTo(markerPose, pose)
            double x = wrt.Translation.X, y = wrt.Translation.Y, z = wrt.Translation.Z;
            double d2 = planar ? x * x + y * y : x * x + y * y + z * z;
            if (d2 < bestD2) { bestD2 = d2; best = m.Code; markerWrtPose = wrt; }   // strictly smaller; NaN never is
        }
        return best;
    }
}

/// <summary>
/// One entry of the obstacle vector <c>BlockWorld::GetObstacles</c> fills (M12-036): <c>(Quad2f footprint, ObjectID)</c>, stride 0x28, the ObjectID at +0x24.
/// <see cref="Quad"/> is <c>o-&gt;GetBoundingQuadXY(o.pose, padding)</c> (M13-007, <see cref="Footprint"/>). When the exact footprint cannot be computed (a non-cube, or a cube
/// pose that is not yaw-only: <c>cv::minAreaRect</c> and the other <c>GetBoundingQuadXY</c> overrides are unread, M13-023) <see cref="Quad"/> is null and the entry is the
/// LABELLED STAND-IN of M13-023, a reduction that differs from the engine: it "contains" a point within half a cube (22 mm, the same constant as
/// <see cref="BlockConfigurationManager.PlanarStandIn"/>) of <see cref="Centre"/> in x, y, and its centroid is <see cref="Centre"/>.
/// </summary>
// fidelity: M12-036
public sealed record PreActionObstacle(Quadrilateral? Quad, Point2f Centre, uint ObjectId)
{
    /// <summary>True when this entry is the M13-023 stand-in and not an exact footprint.</summary>
    public bool IsStandIn => Quad is null;

    /// <summary>The engine's <c>Quadrilateral::Contains</c> for an exact quad; the planar 22 mm rule for the stand-in.</summary>
    public bool Contains(Point2f p) => Quad is { } q ? q.Contains(p) : StandInContains(Centre, p);

    /// <summary>The centroid of the four corners, <c>Quadrilateral::ComputeCentroid</c> 0x004DF7BE = (c0 + c2 + c1 + c3) * 0.25 (fix-round-2 verifier), or the stand-in's centre.</summary>
    public Point2f Centroid => Quad is { } q ? MeanOf(q) : Centre;

    internal static Point2f MeanOf(Quadrilateral q) =>
        new((q[0].X + q[2].X + q[1].X + q[3].X) * 0.25f, (q[0].Y + q[2].Y + q[1].Y + q[3].Y) * 0.25f);

    internal static bool StandInContains(Point2f centre, Point2f p)
    {
        float dx = p.X - centre.X, dy = p.Y - centre.Y;
        return MathF.Sqrt(dx * dx + dy * dy) <= (float)(CubeGeometry.CubeSizeMm / 2);
    }

    /// <summary>The footprint entry for <paramref name="o"/> at its own pose with <paramref name="padding"/>: exact for a cube whose pose is yaw-only, otherwise the stand-in.</summary>
    public static PreActionObstacle For(ObservableObject o, float padding)
    {
        Footprint.TryGetBoundingQuadXY(o, o.Pose, padding, out var quad);
        return new PreActionObstacle(quad, new Point2f((float)o.Pose.Translation.X, (float)o.Pose.Translation.Y), o.ObjectId);
    }
}

/// <summary>
/// <c>ActionableObject::IsPreActionPoseValid</c> 0x004DF2C0 and <c>BlockWorld::GetObstacles</c> 0x00626D44: the pre-action pose filter (M12-036; re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q2,
/// verified in 20260929-R-VIS-verify-M12-gap3.md and by the M12 fix-round verifier). Built for cube candidates; the Ramp 0x0050F9AC and Charger 0x004EA648 overrides (M12-034) are unread and have no
/// pose generator here.
/// </summary>
// fidelity: M12-036
public static class PreActionValidity
{
    /// <summary>0x3E0930A4 = 0.13397461 (0x004DF61C): <c>|R22 - 1|</c> must be below it, i.e. R22 &gt; cos 30 degrees.</summary>
    public static readonly float RotationTolerance = BitConverter.UInt32BitsToSingle(0x3E0930A4);
    /// <summary>0x425F999A = 55.9: how far behind the pose the swept footprint starts, and the length added to |m - p|.</summary>
    public static readonly float SweepBehindMm = BitConverter.UInt32BitsToSingle(0x425F999A);
    /// <summary>0x41D8CCCD = 27.1: the half width of the swept footprint (the perpendicular's scale).</summary>
    public static readonly float SweepHalfWidthMm = BitConverter.UInt32BitsToSingle(0x41D8CCCD);
    /// <summary>The step between sample centres: 10.</summary>
    public const float SweepStepMm = 10f;

    /// <summary>
    /// <c>BlockWorld::GetObstacles(vec, padding)</c> 0x00626D44 (called with 0.0 by <c>IDockAction::GetPreActionPoses</c>): the ignore-ID set is <paramref name="ignoreIds"/>
    /// (<c>CarryingComponent::GetCarryingObjects</c>), <c>minZ</c> is the z of the robot's pose in its root frame and <c>maxZ = minZ + Robot::GetHeight()</c>
    /// (0x00626DEC..0x00626E02); <c>GetLocatedObjectBoundingBoxesXY</c> 0x00626BB5 walks the located objects of the current origin (every family) and the predicate
    /// 0x0062C1D4 excludes an object whose Z extent <c>[z - 0.5*rz, z + 0.5*rz]</c>, <c>rz = (R*size)[z]</c> (0x0062C246..0x0062C27E), has (top &lt;= minZ AND bottom &lt;= minZ)
    /// OR (top &gt;= maxZ AND bottom &gt;= maxZ); every other object appends (<c>GetBoundingQuadXY(o.pose, padding)</c>, o.ID). Float32 arithmetic as the engine's.
    /// </summary>
    public static List<PreActionObstacle> GetObstacles(IEnumerable<ObservableObject> locatedObjects, IReadOnlyCollection<uint> ignoreIds, Pose3d robotPose,
                                                       double robotHeightMm, float padding)
    {
        var result = new List<PreActionObstacle>();
        float minZ = (float)robotPose.Translation.Z, maxZ = minZ + (float)robotHeightMm;
        foreach (var o in locatedObjects)
        {
            if (ignoreIds.Contains(o.ObjectId)) continue;
            var size = CubeGeometry.SizeOf(o.Type);
            var r = o.Pose.Rotation;
            float rz = (float)(r[2, 0] * size.X + r[2, 1] * size.Y + r[2, 2] * size.Z);
            float z = (float)o.Pose.Translation.Z;
            float bottom = z - 0.5f * rz, top = z + 0.5f * rz;
            if ((top <= minZ && bottom <= minZ) || (top >= maxZ && bottom >= maxZ)) continue;
            result.Add(PreActionObstacle.For(o, padding));
        }
        return result;
    }

    /// <summary>
    /// The base <c>ActionableObject::IsPreActionPoseValid(elem, obstacles)</c> 0x004DF2C0 for <paramref name="obj"/> and one composed element (the world pose):
    /// (1) the pose's rotation entry R22 must satisfy <c>|R22 - 1| &lt; 0.13397461</c> (NaN is invalid); (2) an empty obstacle vector is valid (0x004DF31A..0x004DF320);
    /// (3) otherwise the swept footprint: <c>p</c> = the pose's x, y; <c>m</c> = the element's marker's world x, y; <c>u = normalize(m - p)</c> (not normalised when
    /// <c>|m - p|^2 &lt;= 0</c>, so the perpendicular is (0, 0) and the loop count is 5); <c>L = |m - p|</c>; <c>n = (int)floorf((L + 55.9) / 10)</c> (floorf, 0x004DF468 through PLT 0x4A4084), and n &lt; 1 is valid; <c>start = p - 55.9 u</c>,
    /// <c>perp = (u.y, -u.x) * 27.1</c>; for i in 0..n-1 <c>centre = start + 10 i u</c>, <c>left = centre + perp</c>, <c>right = centre - perp</c>; for every obstacle O: skip
    /// when O's id equals this object's id; skip when the object's own quad <c>Q = this-&gt;vtbl[0x50](this, obj+4, 0)</c> contains O's centroid; otherwise a hit is
    /// <c>O.Contains(centre) || O.Contains(right) || O.Contains(left)</c> and any hit is invalid. <c>reachableFrom</c> is never used.
    /// </summary>
    public static bool IsPreActionPoseValid(ObservableObject obj, PreActionPose elem, IReadOnlyList<PreActionObstacle> obstacles, List<string>? trace = null)
    {
        float r22 = (float)elem.WorldPose.Rotation[2, 2];
        if (!(MathF.Abs(r22 - 1f) < RotationTolerance)) return false;                 // 0x004DF2F2..0x004DF318; NaN fails
        if (obstacles.Count == 0) return true;                                        // 0x004DF31A..0x004DF320
        var m = obj.Pose.Compose(elem.Marker.PoseOnObject).Translation;
        float px = (float)elem.WorldPose.Translation.X, py = (float)elem.WorldPose.Translation.Y;
        float dx = (float)m.X - px, dy = (float)m.Y - py;
        float len2 = dx * dx + dy * dy;
        float ux = dx, uy = dy, len;
        if (len2 <= 0f) len = 0f;                                                     // the direction is left as it is
        else { len = MathF.Sqrt(len2); float inv = 1f / len; ux = dx * inv; uy = dy * inv; }   // vdiv 1/len (0x004DF3C2) then vmul (0x004DF3D2)
        // n = (int)floorf((L + 55.9) / 10) (0x004DF45C..0x004DF48C: vdiv #10, blx floorf via PLT 0x4A4084 whose GOT slot 0x010402B8 is floorf, vcvt.s32.f32, cmp r5,#1, blt); L = 0 gives 5
        float nf = MathF.Floor((len + SweepBehindMm) / SweepStepMm);
        int n = float.IsNaN(nf) ? 0 : (int)nf;
        if (n < 1) return true;
        float sx = px - SweepBehindMm * ux, sy = py - SweepBehindMm * uy;
        float perpX = uy * SweepHalfWidthMm, perpY = -ux * SweepHalfWidthMm;
        float stepX = SweepStepMm * ux, stepY = SweepStepMm * uy;
        var own = PreActionObstacle.For(obj, 0f);                                     // Q: the object's own quad at its pose, padding 0
        if (own.IsStandIn) trace?.Add("IsPreActionPoseValid: own footprint is the M13-023 planar stand-in");
        // the engine accumulates centre, left and right by +10u per sample (0x004DF582..0x004DF5DA); left/right start at start +- perp
        float cx = sx, cy = sy, lx = sx + perpX, ly = sy + perpY, rx = sx - perpX, ry = sy - perpY;
        for (int i = 0; i < n; i++)
        {
            var centre = new Point2f(cx, cy);
            var left = new Point2f(lx, ly);
            var right = new Point2f(rx, ry);
            foreach (var o in obstacles)
            {
                if (o.ObjectId == obj.ObjectId) continue;                             // [base+0x18] vs [O+0x24]
                if (own.Contains(o.Centroid)) continue;                               // Q.Contains(centroid(O))
                if (o.Contains(centre) || o.Contains(right) || o.Contains(left))
                {
                    trace?.Add($"IsPreActionPoseValid: obstacle {o.ObjectId}{(o.IsStandIn ? " (M13-023 stand-in)" : "")} is in the swept footprint of the {elem.Type} pose via {elem.Marker.Code}");
                    return false;
                }
            }
            cx += stepX; cy += stepY; lx += stepX; ly += stepY; rx += stepX; ry += stepY;
        }
        return true;
    }
}

/// <summary>The <c>PreActionPoseInput</c> of <c>IDockAction::GetPreActionPoses</c> (M12-031): +0 object, +4 ActionType, +8 flag A, +0xC angle tolerance, +0x10 distanceFromMarker, +0x14 useApproachAngle, +0x18 approach angle.</summary>
public sealed record PreActionPoseInput(ObservableObject? Object, PreActionType ActionType, bool FlagA, double AngleToleranceRad,
                                        double DistanceFromMarkerMm, bool UseApproachAngle, double ApproachAngleRad);

/// <summary>The <c>PreActionPoseOutput</c> (M12-031): +0 result, the pose vector, +0x10 closest index, +0x14/+0x18 the robot's |dx|, |dy|, +0x1C the in-position byte, +0x20/+0x24 the threshold pair (initially -1.0).</summary>
public sealed class PreActionPoseOutput
{
    public ActionResult Result { get; set; }
    public List<PreActionPose> Poses { get; } = new();
    public int ClosestIndex { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public bool InPosition { get; set; }
    public double ThresholdX { get; set; } = -1.0;
    public double ThresholdY { get; set; } = -1.0;
}

/// <summary>
/// <c>IDockAction::GetPreActionPoses</c> 0x005508C8..0x00551200 (M12-031; re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q10, verified in
/// 20260929-R-VIS-verify-M12-gap2.md) and <c>IDockAction::RemoveMatchingPredockPose</c> 0x00551418 (M12-029).
///
/// <list type="number">
/// <item>A null object, or one the robot is carrying (<c>[robot+0x284]+8 == id</c>), is 0x03000004 (0x005508F0..0x0055096A).</item>
/// <item><c>BlockWorld::SelectObject(id)</c> (its return ignored; it stores the id at BlockWorld+0x60, which this stack has no field for) and
///   <c>BlockWorld::GetObstacles(0.0)</c> (<c>getObstacles</c>, M12-036) precede the generation (0x00550980..0x005509A0).</item>
/// <item>The poses come from <c>GetCurrentPreActionPoses</c> at 0x00550A3E (M12-001, <see cref="CubePreActionPoses.GetCurrentPreActionPoses"/>, with the validity filter of M12-036).</item>
/// <item>Only when <c>useApproachAngle</c>: a pose is erased when <c>|Radians(yaw) - approachAngle| &gt;= 0x3F490F33</c> (0.785388; the difference is
///   wrap-aware, 0x00550DC4).</item>
/// <item>No pose left is 0x03000010; the closest pose is the strictly smallest PLANAR squared distance from FLT_MAX (0x00550DC8), none is 0x03000005; its
///   |dx|, |dy| are kept.</item>
/// <item>The threshold pair is <c>ComputePreActionPoseDistThreshold(out, closestPose, objectPose, Radians(input+0xC))</c> (0x00550FD2..0x00551010, M12-020),
///   and the in-position byte is 0.</item>
/// <item>With both thresholds &gt; 0: |dx| &lt;= t0 and |dy| &lt;= t1 inside; then in position needs also |yaw of the closest pose with respect to the robot|
///   &lt; tolerance + (-9.99999975e-06) (0x005513B0 = 0xB727C5AC); a failed angle test leaves the byte 0 and the result 0. Outside the box the result is
///   0x04000001 when flag A is set, else 0. With either threshold &lt;= 0 the result is 0 and the byte 0.</item>
/// </list>
/// The yaw of "the closest pose with respect to the robot" is taken as the pose's yaw in the robot's frame (<c>GetAngleAroundZaxis</c>); the report states the
/// comparison but not the yaw extraction, so that is a disclosed choice.
/// </summary>
// fidelity: M12-031, M12-029
public static class DockPreActionPoses
{
    /// <summary>0xB727C5AC = -9.99999975e-06 (0x005513B0), added to the angle tolerance for the in-position yaw test.</summary>
    public static readonly double YawSlack = BitConverter.UInt32BitsToSingle(0xB727C5AC);
    /// <summary>0x3F490F33 = 0.785388 (0x00550DC4): the approach-angle filter's limit.</summary>
    public static readonly double ApproachAngleLimitRad = BitConverter.UInt32BitsToSingle(0x3F490F33);
    /// <summary>The <c>RemoveMatchingPredockPose</c> angle: Radians 0x3F060A92 (30 degrees).</summary>
    public static readonly double MatchAngleRad = BitConverter.UInt32BitsToSingle(0x3F060A92);
    /// <summary>The <c>RemoveMatchingPredockPose</c> tolerance: Point3(100, 100, 100).</summary>
    public static readonly Vec3 MatchToleranceMm = new(100, 100, 100);

    /// <summary><c>IDockAction::GetPreActionPoses</c> 0x005508C8.</summary>
    public static PreActionPoseOutput Get(PreActionPoseInput input, Pose3d robotPose, uint? carriedObjectId,
                                          Func<IReadOnlyList<PreActionObstacle>> getObstacles, List<string>? trace = null)
    {
        var output = new PreActionPoseOutput();
        var obj = input.Object;
        if (obj is null) { trace?.Add("IsCloseEnoughToPreActionPose.NullObject"); output.Result = ActionResult.BadObject; return output; }
        if (carriedObjectId == obj.ObjectId) { trace?.Add("IsCloseEnoughToPreActionPose.CarryingSelectedObject"); output.Result = ActionResult.BadObject; return output; }
        // SelectObject(id) (no field here) and BlockWorld::GetObstacles(0.0) come next (0x00550980..0x005509A0), then GetCurrentPreActionPoses at 0x00550A3E with
        // withAction = {ActionType}; the request passes an EMPTY withCode set (0x00550A1C..0x00550A2C) and ignores the result byte (0x00550A42).
        var obstacles = getObstacles();
        CubePreActionPoses.GetCurrentPreActionPoses(obj, new[] { input.ActionType }, Array.Empty<MarkerType>(), robotPose, input.DistanceFromMarkerMm, obstacles, output.Poses, trace);
        if (input.UseApproachAngle)
        {
            for (int i = 0; i < output.Poses.Count;)
            {
                double yaw = EngineRotationYaw(output.Poses[i].WorldPose);
                if (Math.Abs(EngineRadians.Rescale(yaw - input.ApproachAngleRad)) >= ApproachAngleLimitRad) output.Poses.RemoveAt(i);
                else i++;
            }
        }
        return Finish(output, obj.Pose, robotPose, input.AngleToleranceRad, input.FlagA, trace);
    }

    /// <summary>
    /// Steps 5-7 over an already generated (and filtered) pose list: no pose left is 0x03000010, the closest pose is chosen, the threshold pair and the
    /// in-position byte follow. <c>FlipBlockAction::Init</c> reaches the same steps through <see cref="Get"/>.
    /// </summary>
    public static PreActionPoseOutput Finish(PreActionPoseOutput output, Pose3d objectPose, Pose3d robotPose, double angleToleranceRad, bool flagA, List<string>? trace = null)
    {
        if (output.Poses.Count == 0) { trace?.Add("IsCloseEnoughToPreActionPose.NoPreActionPoses"); output.Result = ActionResult.NoPreActionPoses; return output; }
        output.ClosestIndex = output.Poses.Count;
        float best = float.MaxValue;                                           // 0x00550DC8
        for (int i = 0; i < output.Poses.Count; i++)
        {
            var t = output.Poses[i].WorldPose.Translation;
            float dx = (float)robotPose.Translation.X - (float)t.X, dy = (float)robotPose.Translation.Y - (float)t.Y;
            float d2 = dx * dx + dy * dy;                                      // planar, world axes, single precision as the engine's vcmpe
            if (d2 < best) { best = d2; output.ClosestIndex = i; output.OffsetX = Math.Abs(dx); output.OffsetY = Math.Abs(dy); }
        }
        if (output.ClosestIndex == output.Poses.Count) { trace?.Add("IDockAction.GetPreActionPose.NoClosestPose"); output.Result = ActionResult.BadPose; return output; }
        output.Result = Evaluate(output, objectPose, robotPose, angleToleranceRad, flagA, trace);
        return output;
    }

    /// <summary>Steps 6 and 7 (the threshold, the in-position byte and the 0x04000001 result), on an already chosen closest pose.</summary>
    public static ActionResult Evaluate(PreActionPoseOutput output, Pose3d objectPose, Pose3d robotPose, double angleToleranceRad, bool flagA, List<string>? trace = null)
    {
        var closest = output.Poses[output.ClosestIndex].WorldPose;
        CubePreActionPoses.DistanceThresholdMm(closest, objectPose, angleToleranceRad, out double t0, out double t1);
        output.ThresholdX = t0; output.ThresholdY = t1; output.InPosition = false;
        if (!(t0 > 0) || !(t1 > 0)) return ActionResult.Success;
        if (output.OffsetX <= t0 && output.OffsetY <= t1)
        {
            double yaw = EngineRotationYaw(closest.WithRespectTo(robotPose));
            if (Math.Abs(yaw) < angleToleranceRad + YawSlack) output.InPosition = true;
            return ActionResult.Success;
        }
        if (!flagA) return ActionResult.Success;
        trace?.Add($"Robot is too far from pre-action pose ({output.OffsetX:F1}mm, {output.OffsetY:F1}mm).");
        return ActionResult.DidNotReachPreActionPose;
    }

    private static double EngineRotationYaw(Pose3d pose) => EngineRadians.GetAngleAroundZaxis(pose.Rotation);

    /// <summary>
    /// <c>IDockAction::RemoveMatchingPredockPose(match, poses)</c> 0x00551418: for each pose <c>p</c>, if <c>p.IsSameAs(match, Point3(100, 100, 100),
    /// Radians 0x3F060A92)</c> (the receiver is the vector element, the argument the match pose) it is erased and the same index is tested again. Returns
    /// whether any was erased.
    /// </summary>
    public static bool RemoveMatchingPredockPose(Pose3d match, List<PreActionPose> poses)
    {
        bool removed = false;
        for (int i = 0; i < poses.Count;)
        {
            if (poses[i].WorldPose.IsSameAs(match, MatchToleranceMm, MatchAngleRad, out _)) { poses.RemoveAt(i); removed = true; }
            else i++;
        }
        return removed;
    }
}

/// <summary>
/// <c>PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses</c> 0x005558F4 (M12-026), rows 12.1-12.18 of the pre-extraction, each
/// opened by the verifier (20260929-R-VIS-verify-pre-items11-13.md), the R-VIS gap 2 rows Q1-Q4 and <c>IsAtPreActionPoseWithVisualVerification</c> 0x005B6F5C.
/// NO PRODUCTION CALLER: the engine's three callers are the DriveToPlaceRelObjectAction lambda invoker (never installed on the wire path, M12-025), DriveToHelper
/// 0x005B5918 (M8) and IsAtPreActionPoseWithVisualVerification 0x005B7054 (M8); none is built in this stack, so this class is reached only from tests.
/// </summary>
public static class PlaceRelObjectOffsetPoses
{
    /// <summary>9.99999975e-06, the float 0x3727C5AC.</summary>
    public static readonly double Epsilon = BitConverter.UInt32BitsToSingle(0x3727C5AC);
    /// <summary>Radians(0x3E060A92) = 0.1308997: the GetPreActionPoses request angle (0x0055593E) and the IsSameAs angle (0x005561EA).</summary>
    public static readonly double PreActionAngleRad = BitConverter.UInt32BitsToSingle(0x3E060A92);
    /// <summary>2 degrees, 0x3D0EFA35 (0x00555D88/0x00555D8E).</summary>
    public static readonly double FacingToleranceRad = BitConverter.UInt32BitsToSingle(0x3D0EFA35);
    /// <summary>The Point3 z of the IsSameAs tolerance: 100.0 (<c>movt r2,#0x42c8</c>, 0x005561B6).</summary>
    public const double InPositionToleranceZ = 100.0;
    /// <summary>The 20.0 of row 12.14 (<c>vcmpe s23,s21</c>, 0x005560C8): a width above it is reduced by 20.</summary>
    public const double WidthReductionMm = 20.0;

    /// <summary>
    /// The whole function for a located object: <c>GetPreActionPoses</c> for ActionType 1 with flag A 0, tolerance Radians 0x3E060A92, distanceFromMarker 0
    /// and no approach angle (Q4.7, 0x00555928..0x00555974); a non-zero result leaves the vector empty, which exits with 0x03000010 (Q4.8), as does a
    /// zero result with an empty vector; then <see cref="Filter"/> over the returned poses.
    /// </summary>
    // fidelity: M12-026
    public static ActionResult Compute(ObservableObject obj, double a, double b, Pose3d robotPose, uint? carriedObjectId, CameraCalibration calibration,
                                       ObjectPoseQueries queries, Func<IReadOnlyList<PreActionObstacle>> getObstacles,
                                       out IReadOnlyList<Pose3d> poses, out bool alreadyInPosition, List<string>? trace = null)
    {
        var request = new PreActionPoseInput(obj, PreActionType.PlaceRelative, FlagA: false, PreActionAngleRad, 0.0, UseApproachAngle: false, 0.0);
        var output = DockPreActionPoses.Get(request, robotPose, carriedObjectId, getObstacles, trace);
        var elements = output.Result == ActionResult.Success ? output.Poses.Select(p => p.WorldPose).ToList() : new List<Pose3d>();
        return Filter(obj, a, b, robotPose, calibration, queries, elements, out poses, out alreadyInPosition, trace);
    }

    /// <summary>
    /// Rows 12.3 and 12.6-12.18 over a given element list (the poses <c>GetPreActionPoses</c> returned).
    ///
    /// Disclosed, because this stack has no pose tree: the engine's two <c>GetWithRespectTo</c> failure exits (the element in the object-centre frame, 0x00555B64,
    /// and the shifted pose in the world origin, 0x0055616E -> 0x00556200 -> 0x00556252 -> 0x005562D8) both reach the loop tail WITHOUT advancing the index (Q4.9,
    /// verifier Q4); with every pose a world-origin pose <c>GetWithRespectTo</c> cannot fail here, so those two paths are unreachable and are not coded.
    /// </summary>
    // fidelity: M12-026, M12-020, M12-033
    public static ActionResult Filter(ObservableObject obj, double a, double b, Pose3d robotPose, CameraCalibration calibration,
                                      ObjectPoseQueries queries, IReadOnlyList<Pose3d> elements,
                                      out IReadOnlyList<Pose3d> poses, out bool alreadyInPosition, List<string>? trace = null)
    {
        alreadyInPosition = false;                                           // 12.3, 0x0055590A
        var list = new List<Pose3d>(elements);
        if (list.Count == 0) { poses = list; trace?.Add($"ComputePlaceRelObjectOffsetPoses: no pre-action poses (A={a}, B={b})"); return ActionResult.NoPreActionPoses; }
        bool xNear = Math.Abs(a) <= 1.0, yNear = Math.Abs(b) <= 1.0;         // 12.6 / Q4.10: [sp+0x3C], [sp+0x40] = (-1 <= A <= 1), (-1 <= B <= 1)
        int i = 0;
        while (i < list.Count)
        {
            var element = list[i];
            // 12.7: the element expressed in the object-centre frame (GetZRotatedPointAboveObjectCenter(0.0), M12-033)
            var centre = queries.ZRotatedPointAboveObjectCenter(obj, 0.0);
            var local = element.WithRespectTo(centre);
            double x = local.Translation.X, y = local.Translation.Y;
            // 12.8 / Q4.10: r8 = !(|A|<=1) && (|lx|>1), r7 = !(|B|<=1) && (|ly|>1)
            bool r8 = !xNear && (x < -1.0 || x > 1.0);
            bool r7 = !yNear && (y < -1.0 || y > 1.0);
            // 12.9: a sign mismatch erases with no size condition, then the same index is tested again
            if ((r8 && (a > Epsilon) != (x > Epsilon)) || (r7 && (b > Epsilon) != (y > Epsilon)))
            {
                list.RemoveAt(i); trace?.Add("erased: sign mismatch"); continue;
            }
            // 12.10: erase only while three or more poses remain
            if (r8 != r7 && list.Count >= 3)
            {
                bool erase = r8 ? Math.Abs(y) <= 1.0 : Math.Abs(x) <= 1.0;
                if (erase) { list.RemoveAt(i); trace?.Add("erased: other axis near the object centre"); continue; }
            }
            // 12.11 (Q3.5): yaw = GetAngleAroundZaxis(L); Radians::IsNear is a STRICT < on the wrapped difference (M12-033); Radians(4.712389) rescales to about -pi/2
            double yaw = EngineRadians.GetAngleAroundZaxis(local.Rotation);
            bool mode1 = EngineRadians.IsNear(yaw, BitConverter.UInt32BitsToSingle(0x3FC90FDB), FacingToleranceRad)
                         || EngineRadians.IsNear(yaw, BitConverter.UInt32BitsToSingle(0x4096CBE4), FacingToleranceRad);
            double d = mode1 ? y : x, o = mode1 ? b : a;
            // 12.12 (Q2.5, Q4.3, Q4.4): GetClosestMarkerPose(element, planar), then exactly one marker with that code
            bool failed = false; double limit = 0;
            var code = queries.ClosestMarkerCode(obj, element);
            if (code is null) { failed = true; trace?.Add("closest marker pose failed"); }
            else
            {
                var found = obj.Markers.Where(m => m.Code == code).ToList();
                if (found.Count != 1) { failed = true; trace?.Add($"markers with code {code}: {found.Count}"); }
                else
                {
                    // KnownMarker+0x10 is the marker's width (size.x, the Point<2,float> the ctor 0x0087E22C copies to +0x10/+0x14); KnownMarker.SizeMm is this
                    // stack's field for that width (its Get3dCorners scales X by SizeMm), which is a mapping of C# fields, not something the binary names.
                    double width = found[0].SizeMm;
                    // CameraCalibration fields by the engine's own names (CreateJson 0x0085F16C): nrows [c], ncols [c+2], focalLength_x [c+4].
                    double fov = EngineRadians.Rescale(2 * Math.Atan2(calibration.Columns * 0.5, calibration.FocalLengthX));   // Radians(2*atan2f(ncols*0.5, fx))
                    double t = Math.Tan(fov * 0.5);
                    double minDist = width / t, s4 = o + Math.Abs(d);
                    if (s4 < minDist) { failed = true; trace?.Add($"GetMaxOffsetObjectStillVisible.InvalidDistance: Total distance to object {s4} < min possible distance {minDist} to see the object"); }
                    else limit = s4 * t - width;
                }
            }
            // 12.13: a failed check erases, with no size condition
            if (failed) { list.RemoveAt(i); continue; }
            // 12.14 / Q4.5: W > 20 -> W - 20 (0x005560C8..0x005560D8)
            if (limit > WidthReductionMm) limit -= WidthReductionMm;
            double xs, ys;
            if (mode1) { xs = Math.Min(limit, Math.Max(-limit, a)); ys = b; }
            else { xs = a; ys = Math.Min(limit, Math.Max(-limit, b)); }
            // 12.15 / Q4.6: shift in the object-centre frame, then back to the world origin
            var shifted = new Pose3d(local.Rotation, new Vec3(x + xs, y + ys, local.Translation.Z));
            var world = centre.Compose(shifted);
            list[i] = world;
            // 12.16 / 12.17: the threshold pair (shifted element first, object pose second), z 100.0
            CubePreActionPoses.DistanceThresholdMm(world, obj.Pose, PreActionAngleRad, out double out0, out double out1);
            if (robotPose.IsSameAs(world, new Vec3(out0, out1, InPositionToleranceZ), PreActionAngleRad, out _)) alreadyInPosition = true;
            i++;
        }
        poses = list;
        if (list.Count == 0) { trace?.Add($"ComputePlaceRelObjectOffsetPoses: every pose erased (A={a}, B={b})"); return ActionResult.NoPreActionPoses; }   // 12.18
        return ActionResult.Success;
    }

    /// <summary>
    /// <c>IHelper::IsAtPreActionPoseWithVisualVerification</c> 0x005B6F5C (M12-026, M12-031): a null object (or a failed dynamic_cast) is 0x03000004; an object last observed more
    /// than 1000.0 before <paramref name="nowMs"/> (<c>(float)[obj_vbase+0x1C] + 1000.0 &lt; (float)now</c>, 0x005B6F9E..0x005B7024; 0x447A0000 at 0x005B7124) is 0x0300001D;
    /// ActionType 1 calls <c>ComputePlaceRelObjectOffsetPoses</c> (its return ignored, the flag byte pre-zeroed, 0x005B7030..0x005B7054); every other type calls
    /// <c>GetPreActionPoses</c> with {obj, type, flag A 0, tolerance 0x3E060A92, distance 0, useApproachAngle 0} (0x005B7060..0x005B70A8), a non-zero result is returned; in
    /// both branches the result is then (in-position byte == 0 ? 0x04000001 : 0) (0x005B70C6..0x005B70D2). NO PRODUCTION CALLER (M8's helper).
    /// </summary>
    // fidelity: M12-026, M12-031
    public static ActionResult IsAtPreActionPoseWithVisualVerification(ObservableObject? obj, PreActionType actionType, double a, double b,
                                                                       uint nowMs, Pose3d robotPose, uint? carriedObjectId,
                                                                       CameraCalibration calibration, ObjectPoseQueries queries,
                                                                       Func<IReadOnlyList<PreActionObstacle>> getObstacles)
    {
        if (obj is null) return ActionResult.BadObject;
        if ((double)obj.LastObservedTimestamp + 1000.0 < nowMs) return ActionResult.VisualObservationFailed;
        bool inPosition;
        if (actionType == PreActionType.PlaceRelative)
            Compute(obj, a, b, robotPose, carriedObjectId, calibration, queries, getObstacles, out _, out inPosition);
        else
        {
            var output = DockPreActionPoses.Get(new PreActionPoseInput(obj, actionType, FlagA: false, PreActionAngleRad, 0.0, UseApproachAngle: false, 0.0),
                                                robotPose, carriedObjectId, getObstacles);
            if (output.Result != ActionResult.Success) return output.Result;
            inPosition = output.InPosition;
        }
        return inPosition ? ActionResult.Success : ActionResult.DidNotReachPreActionPose;
    }
}
