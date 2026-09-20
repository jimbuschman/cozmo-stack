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
public sealed record PreActionPose(PreActionType Type, KnownMarker Marker, Pose3d WorldPose, double DistanceMm)
{
    public override string ToString() => $"{Type} via {Marker.Code} at {WorldPose}";
}

/// <summary>
/// <c>Block::GeneratePreActionPoses</c> (0x004E5900..0x004E5D40), NATIVE geometry: for each face marker whose
/// face is enabled for the action (<c>tst</c> against the block info's face mask), a pre-action pose is built
/// with respect to the marker and offset out along the marker's normal by a distance:
///
/// | type | distance | pose w.r.t. marker |
/// | --- | --- | --- |
/// | Docking | 75.0 mm (0x42960000) | the docking pose rotated +90° about Z (the robot faces the marker) |
/// | PlaceRelative | 40.0 mm (0x42200000) | +90° about Z, translated (0, −100, ·) |
/// | PlaceOnGround | as PlaceRelative with (0, −49, ·) | |
/// | Rolling | 75.0 mm | as Docking |
/// | Flipping | at the cube's corner: 135° (2.35619) and ±56.5771 mm (= 40·√2) | |
///
/// Only the four side faces have pre-action poses: the top and bottom markers face up or down. The pose is
/// on the ground plane (z = 0) with the robot's X axis pointing at the marker. The engine builds the full 3-D
/// chain through <c>Pose3d(angle, Z, translation)</c> and <c>Transform3d::RotateBy</c>; the ground-plane
/// reduction here is exact for a cube resting flat, which <c>ClampPoseToFlat</c> ensures upstream.
/// </summary>
public static class CubePreActionPoses
{
    public const double DockingDistanceMm = 75.0;
    public const double RollingDistanceMm = 75.0;
    public const double PlaceRelativeDistanceMm = 40.0;
    public const double PlaceOnGroundOffsetMm = 49.0;
    public const double FlippingCornerMm = 56.5771;
    public const double FlippingAngleRad = 2.35619;

    /// <summary>The pre-action poses of a located cube for an action type, in the world frame.</summary>
    public static IReadOnlyList<PreActionPose> For(ObservableObject cube, PreActionType type)
    {
        var poses = new List<PreActionPose>();
        if (type == PreActionType.None) return poses;
        foreach (var m in cube.Markers)
        {
            var normal = (cube.Pose.Rotation * m.NormalOnObject).Normalized();
            if (Math.Abs(normal.Z) > 0.5) continue;                       // top and bottom faces: no pre-action pose
            var centre = cube.Pose.Apply(m.PoseOnObject.Translation);
            var flat = new Vec3(normal.X, normal.Y, 0).Normalized();
            double distance = type switch
            {
                PreActionType.Docking => DockingDistanceMm,
                PreActionType.Rolling => RollingDistanceMm,
                PreActionType.PlaceRelative => PlaceRelativeDistanceMm,
                PreActionType.PlaceOnGround => PlaceOnGroundOffsetMm,
                PreActionType.Flipping => FlippingCornerMm,
                _ => DockingDistanceMm,
            };
            if (type == PreActionType.Flipping)
            {
                // the corner pose: 135° from the face, 40√2 out (the corner of a 44 mm cube plus the approach)
                var side = new Vec3(-flat.Y, flat.X, 0);
                foreach (double sign in new[] { 1.0, -1.0 })
                {
                    var p = centre + flat * (FlippingCornerMm * Math.Cos(Math.PI / 4)) + side * (sign * FlippingCornerMm * Math.Sin(Math.PI / 4));
                    var dir = (centre - p) with { Z = 0 };
                    poses.Add(new PreActionPose(type, m, new Pose3d(Mat3.AboutZ(Math.Atan2(dir.Y, dir.X)), new Vec3(p.X, p.Y, 0)), FlippingCornerMm));
                }
                continue;
            }
            var pos = centre + flat * distance;
            double heading = Math.Atan2(-flat.Y, -flat.X);                // facing the marker
            poses.Add(new PreActionPose(type, m, new Pose3d(Mat3.AboutZ(heading), new Vec3(pos.X, pos.Y, 0)), distance));
        }
        return poses;
    }

    /// <summary>
    /// <c>ComputePreActionPoseDistThreshold(objectPose, preActionPose, angleTolerance)</c> (0x00550098): the
    /// planar distance from the pre-action pose to the object times sin(angleTolerance), used as the "close
    /// enough" box around a pre-action pose (the further the pose is from the object, the more lateral slack
    /// the same angular error allows). The engine takes the angle tolerance itself when it is above a floor;
    /// the floor value was not read (LOCAL: none).
    /// </summary>
    public static double DistanceThresholdMm(Pose3d objectPose, Pose3d preActionPose, double angleToleranceRad)
    {
        var d = objectPose.Translation - preActionPose.Translation;
        double dist = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        return dist * Math.Sin(angleToleranceRad);
    }

    /// <summary>The pre-action pose nearest the robot (<c>DriveToObjectAction::GetClosestPreDockPose</c>).</summary>
    public static PreActionPose? Closest(IReadOnlyList<PreActionPose> poses, Pose3d robot)
    {
        PreActionPose? best = null; double bestD = double.MaxValue;
        foreach (var p in poses)
        {
            var d = p.WorldPose.Translation - robot.Translation;
            double dist = d.X * d.X + d.Y * d.Y;
            if (dist < bestD) { bestD = dist; best = p; }
        }
        return best;
    }
}
