using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The cube shake the singing vibrato is driven by (fidelity manifest M9-017), recovered from
/// <c>CubeAccelListeners</c> and <c>BehaviorSinging::InitInternal</c>.
///
/// These are pure functions over the recovered arithmetic, so they run without a robot or the OBB. What
/// they pin is the filter, the squared magnitude, the hysteresis and the constants the singing behaviour
/// passes — the four things that decide whether shaking a cube makes Cozmo warble, and by how much.
/// </summary>
public class CubeShakeTests
{
    /// <summary>
    /// HighPassFilterListener::UpdateInternal (0x00636598): y = a * (y + x - xPrevious), then the previous
    /// input is replaced. A constant input therefore decays to nothing, which is what makes gravity
    /// invisible to the shake detector, and a step shows up once and then decays.
    /// </summary>
    [Fact]
    public void TheHighPassFilterPassesChangeAndForgetsAConstant()
    {
        var f = new CubeHighPassFilter(0.5f);

        // a step from 0 to 10: half of it comes through, then it decays by half each sample
        Assert.Equal(5.0f, f.Update(10, 0, 0).X, 5);
        Assert.Equal(2.5f, f.Update(10, 0, 0).X, 5);
        Assert.Equal(1.25f, f.Update(10, 0, 0).X, 5);
        for (int i = 0; i < 40; i++) f.Update(10, 0, 0);
        Assert.True(Math.Abs(f.Value.X) < 1e-6, $"a constant should decay away, left {f.Value.X}");

        // each axis is filtered on its own
        var g = new CubeHighPassFilter(0.5f);
        var v = g.Update(4, 8, 12);
        Assert.Equal((2.0f, 4.0f, 6.0f), v);
    }

    /// <summary>
    /// ShakeListener (0x00636620 and 0x0063679E): the thresholds are compared against the squared
    /// magnitude, the higher one starts the shake and the lower one keeps it, and the callback receives
    /// the squared magnitude itself. A cube that never crosses the higher threshold never calls back.
    /// </summary>
    [Fact]
    public void TheShakeListenerUsesSquaredMagnitudesAndHysteresis()
    {
        var reported = new List<float>();
        // a filter coefficient of 1 passes the signal through unchanged (y = y + x - xPrevious telescopes
        // to x), so the thresholds can be read against the input directly
        var listener = new CubeShakeListener(1.0f, lowThreshold: 2.5f, highThreshold: 3.9f, reported.Add);

        // 3 in x: 9 is under 3.9 squared, which is 15.21, so nothing starts
        listener.Update(3, 0, 0);
        Assert.False(listener.IsShaking);
        Assert.Empty(reported);

        // 8 in x: 64 is over 15.21, so it starts, and the callback gets the square, not the magnitude
        listener.Update(8, 0, 0);
        Assert.True(listener.IsShaking);
        Assert.Equal(64.0f, Assert.Single(reported), 4);

        // back to 3 while shaking: 9 is over 2.5 squared, which is 6.25, so the lower threshold holds it
        listener.Update(3, 0, 0);
        Assert.True(listener.IsShaking);
        Assert.Equal(9.0f, reported[^1], 4);

        // 2 in x: 4 is under 6.25, so it stops and stops reporting
        listener.Update(2, 0, 0);
        Assert.False(listener.IsShaking);
        Assert.Equal(2, reported.Count);

        // and it will not start again until the higher threshold is crossed: 3 squared is 9, over the
        // lower one but under the higher, and it stays quiet
        listener.Update(3, 0, 0);
        Assert.False(listener.IsShaking);
        Assert.Equal(2, reported.Count);
    }

    /// <summary>
    /// The constants BehaviorSinging passes, and what they mean end to end: the squared magnitude goes
    /// through UpdateInternal's divide by 3000 and the half-and-half smoothing, so the vibrato at rest is
    /// 0 and a hard shake walks it up towards 1. A shake that only just crosses the start threshold moves
    /// the vibrato by a quarter of a per cent, which is why the effect is for shaking, not for handling.
    /// </summary>
    [Fact]
    public void TheSingingConstantsTurnAShakeIntoAVibratoValue()
    {
        Assert.Equal(0.5f, CubeShakeListener.SingingFilterCoefficient);
        Assert.Equal(2.5f, CubeShakeListener.SingingLowThreshold);
        Assert.Equal(3.9f, CubeShakeListener.SingingHighThreshold);

        // just over the start threshold: 3.9 squared is 15.21, and 15.21 / 3000 is about 0.005
        float justOver = CubeShakeListener.SingingHighThreshold * CubeShakeListener.SingingHighThreshold;
        Assert.InRange(Cozmo.Robot.Behavior.SingingBehavior.NextVibrato(0, justOver), 0.002f, 0.003f);

        // the value that saturates it: a squared magnitude of 3000, a filtered magnitude of about 55
        Assert.Equal(0.5f, Cozmo.Robot.Behavior.SingingBehavior.NextVibrato(0, 3000f), 5);
        Assert.InRange(Math.Sqrt(3000), 54.7, 54.8);

        // and held there it converges on 1 rather than jumping
        float v = 0;
        for (int i = 0; i < 20; i++) v = Cozmo.Robot.Behavior.SingingBehavior.NextVibrato(v, 9000f);
        Assert.InRange(v, 0.999f, 1.0f);
    }
}
