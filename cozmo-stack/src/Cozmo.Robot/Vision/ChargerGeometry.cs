using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

/// <summary>
/// The engine's <c>Anki::Cozmo::Charger</c> (constructor 0x004E9B6C..0x004E9C90), NATIVE values: the object is
/// 96 x 80 x 31 mm (0x42C00000, 0x42A00000, 0x41F80000 stored at +0xF0; the order length, width, height is
/// INFERRED from the robot's drive-off distance being the length), and it carries one marker, code
/// <see cref="MarkerType.Charger"/> (2), added through <c>AddMarker</c> with a pose rotated −90° about Z
/// (0xBFC90FDB) and translated (86, 0, 22) mm (0x42AC0000, 0x41B00000 - which is 22, not the 11 this
/// stack had) and a marker size of x = 27 mm (width) by y = 20 mm (height)
/// (0x41D80000 at sp+0x14 and 0x41A00000 at sp+0x18; the AddMarker pointer is sp+0x14, and
/// <c>Get3dCorners</c> scales x by size.x and z by size.y). In this frame the charger's origin is at its
/// front lip on the floor, +X runs into the charger towards the back wall, the marker sits on the back wall
/// facing out (its normal is −X), and the robot drives onto it backwards.
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
// fidelity: M13-009
public static class ChargerGeometry
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    // The engine's literals, as the bit patterns the instructions load (M13-009). The stack's poses are double, so each value is
    // widened from its binary32 pattern at the engine's own points: the angles are the engine's Radians(float) values (the f32
    // pi/2 and pi, not the double ones), and nothing else is rounded further.
    /// <summary>96.0f (0x42C00000 at 0x004E9B9A).</summary>
    public const uint LengthBits = 0x42C00000;
    /// <summary>80.0f (0x42A00000 at 0x004E9BAC).</summary>
    public const uint WidthBits = 0x42A00000;
    /// <summary>31.0f (0x41F80000 at 0x004E9BB0).</summary>
    public const uint HeightBits = 0x41F80000;
    /// <summary>-pi/2 as the engine's float: 0xBFC90FDB (movw 0xFDB / movt 0xBFC9 at 0x004E9BBC..0x004E9BC2).</summary>
    public const uint MarkerAngleBits = 0xBFC90FDB;
    /// <summary>86.0f (0x42AC0000 at 0x004E9BD6).</summary>
    public const uint MarkerXBits = 0x42AC0000;
    /// <summary>22.0f (0x41B00000 at 0x004E9BE2).</summary>
    public const uint MarkerZBits = 0x41B00000;
    /// <summary>The marker size's y = 20.0f (0x41A00000 at 0x004E9C22), stored at sp+0x18.</summary>
    public const uint MarkerHeightBits = 0x41A00000;
    /// <summary>The marker size's x = 27.0f (0x41D80000 at 0x004E9C2C), stored at sp+0x14, the pointer AddMarker receives.</summary>
    public const uint MarkerWidthBits = 0x41D80000;
    /// <summary>pi as the engine's float: 0x40490FDB (movw 0xFDB / movt 0x4049 at 0x004EA1AC..0x004EA1B2).</summary>
    public const uint DockedAngleBits = 0x40490FDB;
    /// <summary>The docked pose's x = 30.0f (0x41F00000 at 0x004EA1C6).</summary>
    public const uint DockedXBits = 0x41F00000;
    /// <summary>The file-static Pose2d's y = 250.0f (0x437A0000 at 0x004D6BD6).</summary>
    public const uint PreDockDistanceBits = 0x437A0000;
    /// <summary>pi/2 as the engine's float, added to the Pose2d's angle (vldr at 0x004E9FE4).</summary>
    public const uint PreDockAngleAddBits = 0x3FC90FDB;
    /// <summary>-15.5f (0xC1780000 at 0x004EA018/0x004EA01A).</summary>
    public const uint PreDockZBits = 0xC1780000;

    public const double LengthMm = 96.0;
    public const double WidthMm = 80.0;
    public const double HeightMm = 31.0;
    public const double MarkerXMm = 86.0;
    /// <summary>22 mm up the back wall: 0x41B00000 in the constructor, which is 22 and not 11.</summary>
    public const double MarkerZMm = 22.0;
    /// <summary>The marker's width (x): 27.0 at sp+0x14 (0x41D80000), the AddMarker pointer.</summary>
    public const double MarkerWidthMm = 27.0;
    /// <summary>The marker's height (y): 20.0 at sp+0x18 (0x41A00000).</summary>
    public const double MarkerHeightMm = 20.0;
    public const double DockedXMm = 30.0;
    /// <summary>250 mm out from the marker: the y of the file-static Pose2d at 0x004D6BC4.</summary>
    public const double PreDockDistanceFromMarkerMm = 250.0;
    /// <summary>-15.5 mm: the z GeneratePreActionPoses gives the pose (0xC1780000 at 0x004EA01A).</summary>
    public const double PreDockZOffsetMm = -15.5;

    /// <summary>The marker's pose angle, -pi/2 as the engine's float (0xBFC90FDB).</summary>
    public static readonly double MarkerAngleRad = F(MarkerAngleBits);
    /// <summary>The docked pose's angle, pi as the engine's float (0x40490FDB).</summary>
    public static readonly double DockedAngleRad = F(DockedAngleBits);
    /// <summary>
    /// <c>Radians(p.angle + pi/2)</c> of <c>GeneratePreActionPoses</c> (<c>vadd.f32 s0, s2, s0</c> at 0x004E9FF2 with p.angle = Radians(0) from the
    /// initialiser 0x004D6BC4): the float sum 0 + 0x3FC90FDB.
    /// </summary>
    public static readonly double PreDockAngleRad = 0f + F(PreDockAngleAddBits);
    /// <summary>
    /// The ObjectID the world gave the (single) charger. The engine assigns it in <c>ObservableObject::SetID</c> 0x004EF468, which hands a unique type
    /// (Charger is one, 0x004E3883) one stable value from the process-wide counter (M11-013), so this is that value, or
    /// <see cref="ObservableObject.UnassignedId"/> (-1) before the charger has been given one. It replaces the fixed 100 this stack had, which the
    /// engine never assigned.
    /// </summary>
    // fidelity: M11-013
    public static uint ObjectId => ObjectIdSpace.UniqueIdOrUnassigned(ObjectType.Charger_Basic);

    public static Vec3 Size => new(F(LengthBits), F(WidthBits), F(HeightBits));

    public static readonly IReadOnlyList<KnownMarker> Markers = new[]
    {
        new KnownMarker(MarkerType.Charger, BlockFace.Front,
                        new Pose3d(F(MarkerAngleBits), new Vec3(0, 0, 1), new Vec3(F(MarkerXBits), 0, F(MarkerZBits))), F(MarkerWidthBits)) { HeightMm = F(MarkerHeightBits) },
    };

    /// <summary>The pose of a robot sitting on the charger, in the world (<c>Charger::GetRobotDockedPose</c> 0x004EA1A0): <c>Pose3d(Radians(pi), Z_AXIS, (30, 0, 0), parent = the charger's pose)</c>.</summary>
    // fidelity: M13-009
    public static Pose3d DockedRobotPose(Pose3d chargerPose) =>
        chargerPose.Compose(new Pose3d(Mat3.AboutZ(DockedAngleRad), new Vec3(F(DockedXBits), 0, 0)));

    /// <summary>
    /// <c>Charger::GeneratePreActionPoses(actionType, ...)</c> 0x004E9FB0: it clears the output, then emits ONE pose for action types 0 and 1 only
    /// (<c>cmp r5,#1; bhi</c> at 0x004E9FD4/0x004E9FD6, an unsigned compare) and nothing for any other type.
    /// </summary>
    // fidelity: M13-009
    public static IReadOnlyList<Pose3d> GeneratePreActionPoses(uint actionType, Pose3d chargerPose) =>
        actionType > 1 ? Array.Empty<Pose3d>() : new[] { PreDockPose(chargerPose) };

    /// <summary>
    /// The one pose, in the world: <c>Pose3d(Radians(p.angle + pi/2), Z_AXIS, (p.x, -p.y, -15.5), parent = the marker's pose)</c> (0x004E9FE4..0x004EA058)
    /// with p the file-static Pose2d(Radians(0), 0.0, 250.0) (0x004D6BC4/0x004D6BD6). The marker's own pose is -pi/2 at (86, 0, 22) on the charger, so the
    /// parent chain is charger, marker, this pose.
    /// </summary>
    // fidelity: M13-009
    public static Pose3d PreDockPose(Pose3d chargerPose) => PreDockPose(chargerPose, F(PreDockDistanceBits));

    /// <summary>The same pose at a distance of the caller's choosing (the Pose2d's y), for an align that stops short.</summary>
    public static Pose3d PreDockPose(Pose3d chargerPose, double distanceFromMarkerMm)
    {
        var onMarker = new Pose3d(Mat3.AboutZ(PreDockAngleRad), new Vec3(0f, -(float)distanceFromMarkerMm, F(PreDockZBits)));
        return chargerPose.Compose(Markers[0].PoseOnObject).Compose(onMarker);
    }
}
