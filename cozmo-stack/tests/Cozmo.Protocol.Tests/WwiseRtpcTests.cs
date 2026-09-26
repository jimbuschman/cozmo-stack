using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-009: the RTPC curve shapes and the scaling byte applied after the curve (gapA 5.3). The numbers are
/// the ones the inventory records, including the event_volume cross-check (0.5 → −3.01 dB, 0 → −764.6 dB).
/// </summary>
public class WwiseRtpcTests
{
    private static WwiseRtpc Curve(byte scaling, params (float From, float To, uint Interp)[] pts) =>
        new(0, WwiseRtpc.GameParameterSource, 1, 0, 0, scaling, pts);

    [Fact]
    public void LinearAndConstantInterpolationHold()
    {
        Assert.Equal(5.0, Curve(0, (0, 0, 4), (1, 10, 4)).Evaluate(0.5, out _), 6);
        Assert.Equal(3.0, Curve(0, (0, 3, 9), (1, 9, 9)).Evaluate(0.5, out _), 6);
    }

    [Theory]
    [InlineData(0u, 0.5, 0.875)]      // Log3: 1-(1-t)^3
    [InlineData(2u, 0.5, 0.625)]      // Log1: t(3-t)/2
    [InlineData(6u, 0.5, 0.375)]      // Exp1: t(t+1)/2
    [InlineData(8u, 0.5, 0.125)]      // Exp3: t^3
    public void TheAsymmetricShapesMatchTheRecoveredFormulas(uint interp, double t, double expected)
    {
        Assert.Equal(expected, Curve(0, (0, 0, interp), (1, 1, interp)).Evaluate(t, out _), 6);
    }

    [Fact]
    public void TheSymmetricShapesMatchTheRecoveredFormulas()
    {
        // InvSCurve at 0.25: sin(pi*0.25)/2
        Assert.Equal(Math.Sin(Math.PI * 0.25) / 2, Curve(0, (0, 0, 3), (1, 1, 3)).Evaluate(0.25, out _), 6);
        // Sine: sin(t*pi/2)
        Assert.Equal(Math.Sin(Math.PI / 4), Curve(0, (0, 0, 1), (1, 1, 1)).Evaluate(0.5, out _), 6);
        // SineRecip: 1 - cos(t*pi/2)
        Assert.Equal(1 - Math.Cos(Math.PI / 4), Curve(0, (0, 0, 7), (1, 1, 7)).Evaluate(0.5, out _), 6);
        // SCurve: the recovered polynomial is about 0.4997 at t = 0.5 and about 1 at the endpoint.
        Assert.Equal(0.4997, Curve(0, (0, 0, 5), (1, 1, 5)).Evaluate(0.5, out _), 3);
    }

    /// <summary>
    /// event_volume: points (0, −1.0, Sine) → (1, 0.0), scaling 2. The runtime result is
    /// 20·log10(sin(v·π/2)) dB: 1 → 0, 0.5 → −3.01, 0 → −764.6.
    /// </summary>
    [Fact]
    public void TheEventVolumeCurveScalesToTheRecoveredDecibels()
    {
        var c = Curve(2, (0, -1.0f, 1), (1, 0.0f, 1));
        Assert.Equal(0.0, c.EvaluateScaled(1.0, out _), 3);
        Assert.Equal(-3.01, c.EvaluateScaled(0.5, out _), 2);
        Assert.Equal(-764.616, c.EvaluateScaled(0.0, out _), 3);
    }

    [Fact]
    public void TheScalingByteIsAppliedAfterTheCurve()
    {
        Assert.Equal(100.0, WwiseRtpc.ApplyScaling(3, 2), 6);       // 10^2
        Assert.Equal(10.0, WwiseRtpc.ApplyScaling(4, 20), 6);       // 10^(0.05*20)
        Assert.Equal(0.0, WwiseRtpc.ApplyScaling(4, -800), 6);      // 0.05*y < -37
        Assert.Equal(7.0, WwiseRtpc.ApplyScaling(0, 7), 6);         // unchanged
    }

    [Fact]
    public void AnUnknownInterpolationFallsBackToLinearAndIsFlagged()
    {
        var c = Curve(0, (0, 0, 99), (1, 10, 4));
        Assert.Equal(5.0, c.Evaluate(0.5, out var reduced), 6);
        Assert.True(reduced);
    }
}
