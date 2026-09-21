using Cozmo.Protocol;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

// The body-turn message, read out of the engine rather than left for the robot to answer
// (fidelity manifest M11-014 and M11-015).
public class BodyAngleTests
{
    /// <summary>
    /// The absolute form, as TurnInPlaceAction::Init sends it when +0xC0 is set: the target angle, a
    /// positive speed, no half-revolutions, and the flag set. The speed is unsigned on this path because
    /// the engine only stuffs the direction bit on the relative one.
    /// </summary>
    [Fact]
    public void TheAbsoluteFormSendsTheAngleAndNoHalfRevolutions()
    {
        var b = TurnTowardsPose.Message(1.0, TurnTowardsPose.MaxSpeedRadPerSec, TurnTowardsPose.AccelRadPerSec2,
                                        TurnTowardsPose.ToleranceRad, 0, isAbsolute: true, actionId: 7).ToBytes();
        Assert.Equal(21, b.Length);                                   // tag + 20
        Assert.Equal(1.0f, BitConverter.ToSingle(b, 1));
        Assert.Equal(5.23599f, BitConverter.ToSingle(b, 5), 5);       // 300 deg/s, the engine's default
        Assert.Equal(10.0f, BitConverter.ToSingle(b, 9));
        Assert.Equal(0.0349066f, BitConverter.ToSingle(b, 13), 6);    // 2 degrees
        Assert.Equal(0, BitConverter.ToUInt16(b, 17));
        Assert.Equal(1, b[19]);
        Assert.Equal(7, b[20]);
        Assert.True(BitConverter.ToSingle(b, 5) > 0);
    }

    /// <summary>
    /// The relative form. The first word is still an absolute heading — current plus the turn, wrapped the
    /// way Anki::Radians wraps — the half-revolutions are floor(|relative| / pi), and the sign of the turn
    /// is bit 31 of the speed.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.5, 0.5, 0, false)]
    [InlineData(0.0, -0.5, -0.5, 0, true)]
    [InlineData(1.0, 2.0, 3.0, 0, false)]
    [InlineData(0.0, 4.0, 4.0 - 2 * Math.PI, 1, false)]      // past half a circle: one half-revolution
    [InlineData(0.0, -4.0, 2 * Math.PI - 4.0, 1, true)]
    [InlineData(0.0, 7.0, 7.0 - 2 * Math.PI, 2, false)]
    public void TheRelativeFormCarriesTheAbsoluteTargetTheHalfRevolutionsAndTheSign(
        double current, double relative, double expectedAngle, int expectedHalfRevs, bool negative)
    {
        var b = TurnTowardsPose.RelativeMessage(current, relative, TurnTowardsPose.MaxSpeedRadPerSec,
                                                TurnTowardsPose.AccelRadPerSec2, TurnTowardsPose.ToleranceRad, 3).ToBytes();
        Assert.Equal((float)expectedAngle, BitConverter.ToSingle(b, 1), 4);
        Assert.Equal(expectedHalfRevs, BitConverter.ToUInt16(b, 17));
        Assert.Equal(0, b[19]);                                       // relative
        uint speed = BitConverter.ToUInt32(b, 5);
        Assert.Equal(negative, (speed & 0x8000_0000u) != 0);
        Assert.Equal(5.23599f, Math.Abs(BitConverter.ToSingle(b, 5)), 5);
    }

    /// <summary>
    /// The constants are the engine's, not this stack's: 300 deg/s and 10 rad/s^2 from the
    /// TurnInPlaceAction constructor's +0x78 triple, with 2 degrees of tolerance and a 25 revolution bound.
    /// </summary>
    [Fact]
    public void TheTurnConstantsAreTheOnesTheConstructorHolds()
    {
        Assert.Equal(5.23599, TurnTowardsPose.MaxSpeedRadPerSec, 5);
        Assert.Equal(300.0, TurnTowardsPose.MaxSpeedRadPerSec * 180 / Math.PI, 2);
        Assert.Equal(10.0, TurnTowardsPose.AccelRadPerSec2);
        Assert.Equal(2.0, TurnTowardsPose.ToleranceRad * 180 / Math.PI, 3);
        Assert.Equal(25.0, TurnTowardsPose.MaxRevolutions);
    }
}
