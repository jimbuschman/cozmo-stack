// fidelity: M6-011
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The host libm seam of the voice filter (the third after <see cref="Powf"/> and <see cref="Expf"/>): the engine's <c>tanf</c> is the phone's libm (PLT <c>0x4AB038</c>, called at <c>0xA767B0</c>, <c>0xA771E4</c>, <c>0xA77544</c>, <c>0xA77F6C</c>), which does not ship,
/// so it is EQUIVALENT_IMPLEMENTATION: <see cref="Tanf"/> is <c>tan</c> in double precision rounded once to binary32. Exactness on a given phone is BLOCKED_EXTERNAL (Bionic's tanf is not guaranteed correctly rounded; a result one ulp off changes the
/// stored coefficients). It is the ONE place the stack computes it.
/// <para>The double-rounded result is the correctly rounded binary32 value unless the exact <c>tan(x)</c> lies within about one double ulp of a binary32 rounding midpoint. <c>re-analysis/tools/emu/emu_filter.py</c> enumerates the arguments the filter produces
/// (<c>(fc / (float)rate) * pi</c> for the cutoff map of both filters on a grid of 0..100 at seven rates, the ramp values of the 0 to 15 ramp and others, the shipped Sound's <c>0x3F6231D6</c>, and every argument the emulated run passed), computes the correctly
/// rounded binary32 with 200-bit arithmetic, and the test <c>M6_011_Tanf_is_the_correctly_rounded_float32_for_every_argument_the_filter_produces</c> asserts this function equals it for all of them.</para>
/// </summary>
public static partial class WwiseHostMath
{
    /// <summary>The binary32 correctly rounded <c>tan(x)</c> (the host stand-in for the phone's <c>tanf</c>): double precision <c>tan</c>, rounded once to binary32.</summary>
    public static float Tanf(float x) => (float)Math.Tan(x);
}
