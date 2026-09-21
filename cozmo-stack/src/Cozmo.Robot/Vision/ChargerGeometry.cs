using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

/// <summary>
/// The engine's <c>Anki::Cozmo::Charger</c> (constructor 0x004E9B6C..0x004E9C90), NATIVE values: the object is
/// 96 x 80 x 31 mm (0x42C00000, 0x42A00000, 0x41F80000 stored at +0xF0; the order length, width, height is
/// INFERRED from the robot's drive-off distance being the length), and it carries one marker, code
/// <see cref="MarkerType.Charger"/> (2), added through <c>AddMarker</c> with a pose rotated −90° about Z
/// (0xBFC90FDB) and translated (86, 0, 22) mm (0x42AC0000, 0x41B00000 - which is 22, not the 11 this
/// stack had) and a marker size of 20 x 27 mm
/// (0x41A00000, 0x41D80000). In this frame the charger's origin is at its front lip on the floor, +X runs into
/// the charger towards the back wall, the marker sits on the back wall facing out (its normal is −X), and the
/// robot drives onto it backwards.
///
/// <c>GetRobotDockedPose</c> (0x004EA1A0) is read exactly: <c>Pose3d(Radians(3.14159), Z_AXIS,
/// (30, 0, 0), parent = the charger's pose)</c> - rotated π about Z, facing out of the charger, 30 mm
/// along +X. There is no conflict with the mount action's π/2 test after all: that test is on the
/// <em>failure</em> path of <c>MountChargerAction::CheckIfDone</c> and decides whether a failed mount is
/// worth a retry drive, not whether the robot is docked (see <c>MountChargerAction</c>).
///
/// <c>GeneratePreActionPoses</c> (0x004E9FB0) is read too, and it produces exactly one pose, for action
/// types 0 and 1 only. It is <c>Pose3d(Radians(p.angle + π/2), Z_AXIS, (p.x, −p.y, −15.5),
/// parent = the charger's marker pose)</c>, where <c>p</c> is a file-static <c>Pose2d(0, 0, 250)</c>
/// built by the initialiser at 0x004D6BC4. Composed through the marker - which is itself −π/2 about Z at
/// (86, 0, 22) - that is the identity rotation at (86 − 250, 0, 22 − 15.5) = (−164, 0, 6.5) in the
/// charger's frame: on the charger's axis, 250 mm out from the marker, facing along the charger's +X,
/// which is into the charger.
/// </summary>
public static class ChargerGeometry
{
    public const double LengthMm = 96.0;
    public const double WidthMm = 80.0;
    public const double HeightMm = 31.0;
    public const double MarkerXMm = 86.0;
    /// <summary>22 mm up the back wall: 0x41B00000 in the constructor, which is 22 and not 11.</summary>
    public const double MarkerZMm = 22.0;
    public const double MarkerWidthMm = 20.0;
    public const double MarkerHeightMm = 27.0;
    public const double DockedXMm = 30.0;
    /// <summary>250 mm out from the marker: the y of the file-static Pose2d at 0x004D6BC4.</summary>
    public const double PreDockDistanceFromMarkerMm = 250.0;
    /// <summary>−15.5 mm: the z GeneratePreActionPoses gives the pose (0xC1780000 at 0x004EA01A).</summary>
    public const double PreDockZOffsetMm = -15.5;
    /// <summary>The world-model id of the (single, passive) charger (LOCAL: the engine assigns ids in observation order).</summary>
    public const uint ObjectId = 100;

    public static Vec3 Size => new(LengthMm, WidthMm, HeightMm);

    public static readonly IReadOnlyList<KnownMarker> Markers = new[]
    {
        new KnownMarker(MarkerType.Charger, BlockFace.Front, new Pose3d(-Math.PI / 2, new Vec3(0, 0, 1), new Vec3(MarkerXMm, 0, MarkerZMm)), MarkerWidthMm) { HeightMm = MarkerHeightMm },
    };

    /// <summary>The pose of a robot sitting on the charger, in the world (<c>Charger::GetRobotDockedPose</c>).</summary>
    public static Pose3d DockedRobotPose(Pose3d chargerPose) =>
        chargerPose.Compose(new Pose3d(Mat3.AboutZ(Math.PI), new Vec3(DockedXMm, 0, 0)));

    /// <summary>
    /// The one pre-action pose <c>Charger::GeneratePreActionPoses</c> makes, in the world: on the
    /// charger's axis, <see cref="PreDockDistanceFromMarkerMm"/> out from the marker, facing into the
    /// charger, <see cref="PreDockZOffsetMm"/> below the marker.
    /// </summary>
    public static Pose3d PreDockPose(Pose3d chargerPose) =>
        chargerPose.Compose(new Pose3d(Mat3.Identity,
                                       new Vec3(MarkerXMm - PreDockDistanceFromMarkerMm, 0,
                                                MarkerZMm + PreDockZOffsetMm)));

    /// <summary>The same pose at a distance of the caller's choosing, for an align that stops short.</summary>
    public static Pose3d PreDockPose(Pose3d chargerPose, double distanceFromMarkerMm) =>
        chargerPose.Compose(new Pose3d(Mat3.Identity,
                                       new Vec3(MarkerXMm - distanceFromMarkerMm, 0,
                                                MarkerZMm + PreDockZOffsetMm)));
}
