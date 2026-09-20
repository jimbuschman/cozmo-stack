using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

/// <summary>
/// The engine's <c>Anki::Cozmo::Charger</c> (constructor 0x004E9B6C..0x004E9C90), NATIVE values: the object is
/// 96 x 80 x 31 mm (0x42C00000, 0x42A00000, 0x41F80000 stored at +0xF0; the order length, width, height is
/// INFERRED from the robot's drive-off distance being the length), and it carries one marker, code
/// <see cref="MarkerType.Charger"/> (2), added through <c>AddMarker</c> with a pose rotated −90° about Z
/// (0xBFC90FDB) and translated (86, 0, 11) mm (0x42AC0000, 0x41B00000) and a marker size of 20 x 27 mm
/// (0x41A00000, 0x41D80000). In this frame the charger's origin is at its front lip on the floor, +X runs into
/// the charger towards the back wall, the marker sits on the back wall facing out (its normal is −X), and the
/// robot drives onto it backwards.
///
/// <c>GetRobotDockedPose</c> (0x004EA1A0): the docked robot is rotated π about Z (facing out of the charger)
/// and translated 30 mm (0x41F00000) along +X. <c>GeneratePreActionPoses</c> (0x004EA000) makes one Docking
/// pose; its exact offsets were not fully read (a static pose plus −15.5 mm), so the pre-dock pose here is
/// INFERRED: on the charger's axis, facing the marker, at the distance the mount action aligns to.
/// </summary>
public static class ChargerGeometry
{
    public const double LengthMm = 96.0;
    public const double WidthMm = 80.0;
    public const double HeightMm = 31.0;
    public const double MarkerXMm = 86.0;
    public const double MarkerZMm = 11.0;
    public const double MarkerWidthMm = 20.0;
    public const double MarkerHeightMm = 27.0;
    public const double DockedXMm = 30.0;
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
    /// The pre-dock pose: on the charger's axis in front of the lip, facing the marker, <paramref name="distanceFromMarkerMm"/>
    /// from the marker plane (INFERRED; the mount action then aligns to exactly this distance).
    /// </summary>
    public static Pose3d PreDockPose(Pose3d chargerPose, double distanceFromMarkerMm) =>
        chargerPose.Compose(new Pose3d(Mat3.Identity, new Vec3(MarkerXMm - distanceFromMarkerMm, 0, 0)));
}
