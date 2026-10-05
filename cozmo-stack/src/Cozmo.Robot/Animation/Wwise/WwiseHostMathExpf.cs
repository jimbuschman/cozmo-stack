// fidelity: M6-013, M6-022
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The host libm seam of the Compressor (the second after <see cref="Powf"/>): the engine's <c>expf</c> is the phone's libm (PLT <c>0x4D0058</c>, GOT <c>0x104ED54</c>, symbol <c>expf</c>; called at <c>0xA9FB84</c>, <c>0xA9FBA4</c>, <c>0xA9FC20</c>, <c>0xAA04C4</c>, <c>0xAA04F0</c>), which does not ship,
/// so it is EQUIVALENT_IMPLEMENTATION: <see cref="Expf"/> is <c>exp</c> in double precision rounded once to binary32. It is the ONE place the stack computes it.
/// <para>The double-rounded result is the correctly rounded binary32 value unless the exact <c>exp(x)</c> lies within about one double ulp of a binary32 rounding midpoint. <c>re-analysis/tools/emu/emu_comp.py</c> enumerates the arguments the Compressor produces
/// (<c>-2.2f / (rate * P)</c> for the attack and release of the shipped blocks, of the defaults and of a grid, and <c>-1.0f / (rate * 0.02322f)</c>, for every realistic rate), computes the correctly rounded binary32 with 200-bit arithmetic, and the test
/// <c>M6_013_Expf_is_the_correctly_rounded_float32_for_every_argument_the_compressor_produces</c> asserts this function equals it for all of them (the script also reports the smallest distance to a midpoint). A phone libm whose
/// <c>expf</c> is one ulp off would change the stored coefficients <c>[this+0x20]</c>, <c>[this+0x2C]</c>, <c>[this+0x34]</c> and every output sample after.</para>
/// </summary>
public static partial class WwiseHostMath
{
    /// <summary>The binary32 correctly rounded <c>e^x</c> (the host stand-in for the phone's <c>expf</c>): double precision <c>exp</c>, rounded once to binary32.</summary>
    public static float Expf(float x) => (float)Math.Exp(x);
}
