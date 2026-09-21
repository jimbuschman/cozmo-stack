using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

// The pose chain that holds a carried cube (fidelity manifest M12-008).
public class LiftGeometryTests
{
    private static KnownMarker Front => CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1)
        .First(m => m.Code == MarkerType.LightCubeI_Front);

    /// <summary>
    /// The two links Robot::Robot 0x0050FBF0 builds: a pivot at (-41, 0, 45) and a 66 mm arm off it.
    /// At a lift angle of zero the plate is 66 mm forward of the pivot and level with it.
    /// </summary>
    [Fact]
    public void TheLiftPoseIsThePivotPlusTheArm()
    {
        var flat = LiftGeometry.LiftPoseInRobotFrame(0);
        Assert.Equal(-41 + 66, flat.Translation.X, 6);
        Assert.Equal(0, flat.Translation.Y, 6);
        Assert.Equal(45, flat.Translation.Z, 6);

        // straight up: the arm contributes all of its length to the height and none to the reach
        var up = LiftGeometry.LiftPoseInRobotFrame(Math.PI / 2);
        Assert.Equal(-41, up.Translation.X, 4);
        Assert.Equal(45 + 66, up.Translation.Z, 4);
    }

    /// <summary>
    /// The height this produces is the height the message conversion already reported, which is the
    /// cross-check that the pivot and the arm are the right pair: 45 + 66 sin(angle).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.7)]
    public void TheHeightAgreesWithTheEnginesOwnConversion(double angle)
    {
        Assert.Equal(Cozmo.Protocol.RobotState.LiftHeightMmFromAngle((float)angle),
                     LiftGeometry.LiftPoseInRobotFrame(angle).Translation.Z, 3);
    }

    /// <summary>
    /// And where the object hangs: the dock marker four millimetres in front of the plate, the centre
    /// twelve and a half below it. SetObjectAsAttachedToLift 0x00632CC4 takes the norm of the marker's
    /// offset on the object, so a 44 mm cube gripped by a face marker sits 22 + 4 = 26 mm out.
    /// </summary>
    [Fact]
    public void TheObjectHangsBelowThePlateWithItsMarkerInFront()
    {
        var onLift = LiftGeometry.ObjectOnLift(Front);
        Assert.Equal(Front.PoseOnObject.Translation.Length + 4, onLift.Translation.X, 6);
        Assert.Equal(CubeGeometry.CubeSizeMm / 2 + 4, onLift.Translation.X, 3);
        Assert.Equal(-12.5, onLift.Translation.Z, 6);
    }

    /// <summary>The whole chain composes, and a cube carried by a robot at the origin lands in front of it.</summary>
    [Fact]
    public void TheCarriedObjectFollowsTheRobot()
    {
        var atOrigin = LiftGeometry.CarriedObjectWorldPose(Pose3d.Identity, 0, Front);
        Assert.Equal(-41 + 66 + CubeGeometry.CubeSizeMm / 2 + 4, atOrigin.Translation.X, 3);
        Assert.Equal(45 - 12.5, atOrigin.Translation.Z, 3);

        // turn the robot a quarter turn and the cube goes with it
        var turned = LiftGeometry.CarriedObjectWorldPose(
            new Pose3d(Mat3.AboutZ(Math.PI / 2), new Vec3(100, 0, 0)), 0, Front);
        Assert.Equal(100, turned.Translation.X, 3);
        Assert.Equal(atOrigin.Translation.X, turned.Translation.Y, 3);
    }
}
