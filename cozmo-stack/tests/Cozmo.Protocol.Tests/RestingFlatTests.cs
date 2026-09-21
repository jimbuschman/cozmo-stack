using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

// What makes a cube a valid bottom to stack on (fidelity manifest M12-012).
public class RestingFlatTests
{
    private static ObservableObject CubeWith(Mat3 rotation) =>
        new(7, ObjectType.Block_LIGHTCUBE1, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
        { Pose = new Pose3d(rotation, new Vec3(100, 0, 22)), PoseState = PoseState.Known };

    /// <summary>
    /// IsRestingFlat 0x0087751C takes acos of the absolute dot between the object's own Z and the
    /// nearest parent axis, so a cube on any of its six faces is flat - the sign is thrown away - and
    /// only one tilted past the tolerance is not.
    /// </summary>
    [Theory]
    [InlineData(0.0, true)]              // sitting squarely
    [InlineData(Math.PI, true)]          // upside down is still flat
    [InlineData(Math.PI / 2, true)]      // on its side: its Z now lies along a parent axis
    [InlineData(0.15, true)]             // 8.6 degrees, inside ten
    [InlineData(0.2, false)]             // 11.5 degrees, outside
    [InlineData(Math.PI / 4, false)]     // balanced on an edge
    public void ACubeIsFlatWhenItsZLiesNearAParentAxis(double tiltRad, bool flat)
    {
        var cube = CubeWith(Mat3.AboutY(tiltRad));
        Assert.Equal(flat, cube.IsRestingFlat(StackBlocksBehavior.RestingFlatToleranceRad));
    }

    /// <summary>The tolerance is the engine's ten degrees, from CanInteractWithObjectHelper 0x0063C670.</summary>
    [Fact]
    public void TheToleranceIsTenDegrees()
    {
        Assert.Equal(10.0, StackBlocksBehavior.RestingFlatToleranceRad * 180 / Math.PI, 2);
    }

    /// <summary>
    /// A rotation about Z does not tilt the cube at all, so spinning it on the table never makes it an
    /// invalid bottom.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.9)]
    [InlineData(2.5)]
    public void SpinningACubeOnTheTableLeavesItFlat(double yaw) =>
        Assert.True(CubeWith(Mat3.AboutZ(yaw)).IsRestingFlat(StackBlocksBehavior.RestingFlatToleranceRad));
}
