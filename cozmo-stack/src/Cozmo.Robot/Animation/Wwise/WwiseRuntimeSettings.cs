namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The internal Wwise runtime format (M6-018, forced policy MD1): 48,000 Hz and 1,024 samples per engine
/// frame.
///
/// In the original both values depend on the phone: the mix rate is <c>min(native output rate, 48000)</c>
/// (gapB P3, gapD D1.3) and the frame size is rounded to the hardware buffer (gapB P4, default 0x400). This
/// stack has no phone, so it fixes them at the cap and the typical native rate. That is a recorded
/// compatibility policy, not a recovered value.
///
/// This is a <b>different domain</b> from the robot audio endpoint (<see cref="CozmoAudio"/> at 22,320 Hz and
/// 744 samples, M3). The two must never be substituted for one another. Derived values come from the
/// runtime's own setters (SetRate 0xA1C75C / SetFrame 0xA1C7D4, gapD D1.2/D1.3, gapE §4):
/// <list type="bullet">
/// <item><c>+8 msPerFrame = u32(float(frame) / (float(rate)/1000))</c> = 21;</item>
/// <item><c>+0xC = u32(0.25·frame·1000/rate)</c> = 5;</item>
/// <item><c>+0x10</c> (the LPF chunk N) <c>= floor(rate·128/48000)</c> = 128.</item>
/// </list>
/// </summary>
public static class WwiseRuntimeSettings
{
    /// <summary>Internal Wwise mix rate, SetRate 0xA1C75C. Not the robot endpoint's 22,320 Hz.</summary>
    public const int MixRateHz = 48000;

    /// <summary>Internal Wwise samples per engine frame, SetFrame 0xA1C7D4. Not the robot endpoint's 744.</summary>
    public const int SamplesPerFrame = 1024;

    /// <summary>Struct +8 (0x1052444): u32(float(frame) / (float(rate)/1000)) = 21.</summary>
    public static int MsPerFrame => (int)(SamplesPerFrame / (MixRateHz / 1000f));

    /// <summary>Struct +0xC: u32(0.25·frame·1000/rate) = 5.</summary>
    public static int QuarterFrameMs => (int)(0.25f * SamplesPerFrame * 1000f / MixRateHz);

    /// <summary>Struct +0x10: floor(rate·128/48000), the voice filter's chunk N = 128.</summary>
    public static int LpfChunkSamples => (int)MathF.Floor(MixRateHz * 128f / 48000f);
}
