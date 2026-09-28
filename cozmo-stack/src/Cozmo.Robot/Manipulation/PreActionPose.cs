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
    /// <c>(0, -49, -size.z/2)</c>; Flipping <c>(size.x/2 + 56.5771, -56.5771, -size.z/2)</c>. The <c>sb</c>
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
    /// <summary>The Flipping corner's 56.5771 (0xC2624EEF at 0x004E5CB8).</summary>
    public const double FlippingCornerMm = 56.5771;
    /// <summary>The built pose's Z rotation for Docking/PlaceRelative/PlaceOnGround/Rolling: pi/2 (C-E7).</summary>
    public const double DockingAngleRad = Math.PI / 2;
    /// <summary>The built pose's Z rotation for Flipping: 3pi/4 (C-E7).</summary>
    public const double FlippingAngleRad = 3 * Math.PI / 4;

    /// <summary>
    /// <c>operator&gt;(angleTolerance, Radians(0))</c> (0x0084CC90) uses the ~1e-5 rad epsilon 0x3727C5AC,
    /// so the positivity guard is <c>angleTolerance &gt; 1e-5</c>.
    /// </summary>
    public const double DistanceThresholdEpsilonRad = 1e-5;
    /// <summary>The -1.0f the threshold writes when the guard fails (0x005501AE).</summary>
    public const double DistanceThresholdSentinel = -1.0;
    /// <summary>The 1e-5 (0x3727C5AC) the <c>GetCurrentPreActionPoses</c> offset geometry compares dx/dy against (E1).</summary>
    public const double OffsetEpsilon = 1e-5;

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
            PreActionType.Flipping => (new Vec3(size.X / 2 + FlippingCornerMm, -FlippingCornerMm, z), FlippingAngleRad),
            _ => (new Vec3(0, 0, 0), 0),
        };
    }

    /// <summary>
    /// The pre-action poses of a located cube for an action type, in the world frame.
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
        if (type is PreActionType.Entry or PreActionType.None) return poses;
        var size = CubeGeometry.SizeOf(cube.Type);
        foreach (var def in FaceDefsFor(cube.Type))
        {
            var marker = cube.Markers.FirstOrDefault(m => m.Face == def.Face);
            if (marker is null) continue;
            for (int sb = 0; sb < 4; sb++)
            {
                if (!Enabled(type, def, sb)) continue;
                poses.Add(Build(cube, marker, type, sb, size, robotPose, preDockPoseOffsetMm));
            }
        }
        return poses;
    }

    /// <summary>The gate at 0x004E5976/0x004E5B94/0x004E5C80: <c>(1&lt;&lt;sb) &amp; mask</c>, only for types 0, 4 and 5.</summary>
    public static bool Enabled(PreActionType type, BlockFaceDef def, int sb) => type switch
    {
        PreActionType.Docking or PreActionType.Flipping => (def.MaskForTypes0And5 & (1 << sb)) != 0,
        PreActionType.Rolling => (def.MaskForType4 & (1 << sb)) != 0,
        _ => true,
    };

    private static PreActionPose Build(ObservableObject cube, KnownMarker marker, PreActionType type, int sb, Vec3 size,
                                       Pose3d robotPose, double preDockPoseOffsetMm)
    {
        var (builtTranslation, builtAngle) = BuiltPoseFor(type, size);
        // Block::GeneratePreActionPoses builds Pose3d(builtAngle, Z_AXIS, builtTranslation) with the marker
        // as parent, then rotates it about Y by sb*pi/2 with Transform3d::RotateBy 0x0084BB3A (C-E7). The
        // PreActionPose ctor re-roots to the marker's parent, so stored = marker.PoseOnObject ∘ built.
        var built = new Pose3d(builtAngle, new Vec3(0, 0, 1), builtTranslation);
        var rotated = RotateBy(built, new Vec3(0, 1, 0), sb * (Math.PI / 2));
        var stored = marker.PoseOnObject.Compose(rotated);
        var world = ComposeWorld(cube.Pose, stored, robotPose, DistanceFor(type), preDockPoseOffsetMm);
        return new PreActionPose(type, marker, world, DistanceFor(type))
        {
            LocalOffsetMm = builtTranslation,
            LocalAngleRad = builtAngle,
            RotationIndex = sb,
        };
    }

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
    /// <c>ComputePreActionPoseDistThreshold(out, objectPose, preActionPose, angleTolerance)</c> (0x00550098).
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
    public static bool DistanceThresholdMm(Pose3d objectPose, Pose3d preActionPose, double angleToleranceRad,
                                           out double thresholdTwice, out double thresholdOnce)
    {
        if (!(angleToleranceRad > DistanceThresholdEpsilonRad))
        {
            thresholdTwice = DistanceThresholdSentinel;
            thresholdOnce = DistanceThresholdSentinel;
            return false;
        }
        var d = objectPose.WithRespectTo(preActionPose).Translation;
        double dist = Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        thresholdOnce = dist * Math.Sin(angleToleranceRad);
        thresholdTwice = 2 * thresholdOnce;
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