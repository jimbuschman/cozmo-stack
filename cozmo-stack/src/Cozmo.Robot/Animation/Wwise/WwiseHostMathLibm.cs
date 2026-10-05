// fidelity: M6-013
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The host libm seams of the Parametric EQ's coefficient routine <c>0xAA25E0</c> (the fourth, fifth and sixth after <see cref="Powf"/>, <see cref="Expf"/> and <see cref="Tanf"/>): the engine's <c>sinf</c> (PLT <c>0x4A4168</c>), <c>cosf</c> (PLT <c>0x4A415C</c>) and <c>sqrtf</c> (PLT <c>0x4A4078</c>)
/// are the phone's libm, which does not ship, so they are EQUIVALENT_IMPLEMENTATION: <see cref="Sinf"/> and <see cref="Cosf"/> are <c>sin</c> / <c>cos</c> in double precision rounded once to binary32, <see cref="Sqrtf"/> is the IEEE square root. Exactness on a given phone is BLOCKED_EXTERNAL (Bionic's
/// functions are not guaranteed correctly rounded; a result one ulp off changes the stored coefficients). They are the ONE place the stack computes these.
/// <para>The double-rounded result of sin / cos is the correctly rounded binary32 value unless the exact result lies within about one double ulp of a binary32 rounding midpoint. <c>re-analysis/tools/emu/emu_eq.py</c> collects every argument the emulated coefficient routine and the shipped Robot_Bus blocks (rates 48000 and 22320) pass, computes the correctly rounded
/// binary32 with 200-bit arithmetic, and the tests <c>M6_013_Sinf_..</c>, <c>M6_013_Cosf_..</c> assert these functions equal it for all of them. <c>sqrtf</c> is only called by the engine when <c>vsqrt.f32</c> produced a NaN (0xAA26AC, 0xAA26D4, 0xAA29C0, 0xAA29E8), so it never changes a result: the NaN stays a NaN.</para>
/// </summary>
public static partial class WwiseHostMath
{
    /// <summary>The binary32 correctly rounded <c>sin(x)</c> (the host stand-in for the phone's <c>sinf</c>): double precision <c>sin</c>, rounded once to binary32.</summary>
    public static float Sinf(float x) => (float)Math.Sin(x);

    /// <summary>The binary32 correctly rounded <c>cos(x)</c> (the host stand-in for the phone's <c>cosf</c>): double precision <c>cos</c>, rounded once to binary32.</summary>
    public static float Cosf(float x) => (float)Math.Cos(x);

    /// <summary>The binary32 square root (the host stand-in for the phone's <c>sqrtf</c>; IEEE, exact).</summary>
    public static float Sqrtf(float x) => MathF.Sqrt(x);
}
