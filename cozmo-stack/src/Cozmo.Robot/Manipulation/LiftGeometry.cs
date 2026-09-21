using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// Where the lift is, and where a carried object sits on it.
///
/// The engine keeps this as a pose tree, built in <c>Robot::Robot</c> 0x0050FBF0. Two links matter:
///
/// <list type="bullet">
/// <item>the <b>lift pivot</b> at robot+0x2E4, a child of the robot origin at translation
///   <c>(-41, 0, 45)</c> with no rotation — <c>0xC2240000</c>, 0, <c>0x42340000</c> at 0x0050FFE0;</item>
/// <item>the <b>lift pose</b> at robot+0x2F0, a child of the pivot at <c>(66, 0, 0)</c>
///   (<c>0x42840000</c> at 0x00510038), rotated about Y by the lift angle. <c>Robot::ComputeLiftPose</c>
///   0x005151AC does that rotation and then rotates the orientation back by the same angle, so the
///   plate stays level however high the arm is.</item>
/// </list>
///
/// The 45 and the 66 are the numbers the height conversion already used; the <c>-41</c> is the one that
/// was missing, and it is why a carried cube cannot be placed from the lift height alone.
///
/// <c>CarryingComponent::SetObjectAsAttachedToLift</c> 0x00632CC4 then puts the object <em>with respect
/// to the lift pose</em> at <c>(|dockMarkerOffset| + 4, 0, -12.5)</c>: it takes the norm of the dock
/// marker's translation on the object (0x00632E42 onwards), adds 4, and pairs it with -12.5
/// (<c>0xC1480000</c> at 0x00632E82). So the object hangs below the plate with its dock marker four
/// millimetres in front of it.
/// </summary>
public static class LiftGeometry
{
    /// <summary>The lift pivot in the robot frame: robot+0x2E4's translation.</summary>
    public static readonly Vec3 PivotInRobotFrame = new(-41, 0, 45);
    /// <summary>The arm from the pivot to the plate: robot+0x2F0's translation.</summary>
    public const double ArmLengthMm = 66;
    /// <summary>How far in front of the plate the dock marker is held.</summary>
    public const double MarkerClearanceMm = 4;
    /// <summary>How far below the plate the object's centre hangs.</summary>
    public const double ObjectDropMm = -12.5;

    /// <summary>
    /// The lift pose in the robot's frame for a given lift angle: the arm swung about Y, with the plate
    /// kept level. <c>ComputeLiftPose</c> rotates by the angle and un-rotates the orientation, which
    /// leaves a pure translation.
    /// </summary>
    public static Pose3d LiftPoseInRobotFrame(double liftAngleRad) => new(
        Mat3.Identity,
        new Vec3(PivotInRobotFrame.X + ArmLengthMm * Math.Cos(liftAngleRad),
                 PivotInRobotFrame.Y,
                 PivotInRobotFrame.Z + ArmLengthMm * Math.Sin(liftAngleRad)));

    /// <summary>
    /// Where a carried object sits relative to the lift pose, given the dock marker it was picked up by.
    /// The engine uses the norm of the marker's translation on the object, so which face it is does not
    /// matter, only how far out from the centre it sits.
    /// </summary>
    public static Pose3d ObjectOnLift(KnownMarker dockMarker) => new(
        Mat3.Identity,
        new Vec3(dockMarker.PoseOnObject.Translation.Length + MarkerClearanceMm, 0, ObjectDropMm));

    /// <summary>The carried object's pose in the world: robot, pivot, arm, object, composed in order.</summary>
    public static Pose3d CarriedObjectWorldPose(Pose3d robotPose, double liftAngleRad, KnownMarker dockMarker) =>
        robotPose.Compose(LiftPoseInRobotFrame(liftAngleRad)).Compose(ObjectOnLift(dockMarker));
}
