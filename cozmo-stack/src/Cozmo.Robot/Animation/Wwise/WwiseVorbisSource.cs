// fidelity: M6-002
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// Which of the two shipped Vorbis source classes a source is (C12 / source-classes report Q1d; labels corrected by C32.3 and checked in C36: the streamed class is vtable <c>0x103E138</c>, the in-memory class <c>0x103E0B8</c>).
/// The live source objects are <see cref="WwiseVorbisStreamSource"/> and <see cref="WwiseVorbisInMemorySource"/>; the offline whole-buffer decode of M6-002 is <see cref="WwiseVorbisNative.Decode"/>.
/// </summary>
public enum WwiseVorbisSourceKind
{
    /// <summary>
    /// Vorbis with <c>stream == 1 or 2</c>: vtable <c>0x103E138</c> (the 0xFC-byte class), <c>vt+0x28 = 0xAB22D4</c>, <c>vt+0x30 = 0xAB1550</c>, <c>vt+0x78 = 0xAB12B4</c> (M6-002 C12 as corrected by C32.3).
    /// </summary>
    Streamed,

    /// <summary>
    /// Vorbis with <c>stream == 0</c>: vtable <c>0x103E0B8</c> (the 0xD0-byte class), <c>vt+0x28 = 0xAB0B20</c>, <c>vt+0x30 = 0xAB0448</c> (M6-002 C12 as corrected by C32.3).
    /// </summary>
    InMemory,
}
