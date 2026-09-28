// fidelity: M6-002
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>Which of the two shipped Vorbis source classes a source is (C12 / source-classes report Q1d).</summary>
public enum WwiseVorbisSourceKind
{
    /// <summary>Vorbis with <c>stream == 1 or 2</c>: vtable 0x103E0B8, render <c>0xAB0448</c> (M6-002 C12).</summary>
    Streamed,
    /// <summary>Vorbis with <c>stream == 0</c>: vtable 0x103E138, render <c>0xAB1550</c> (M6-002 C12).</summary>
    InMemory,
}

/// <summary>
/// The voice <c>params</c> struct the source render slots write (C12 / source-classes report Q4): the data
/// pointer, the rate/format, the status, valid/max frames, the start/total samples, the pitch and the result
/// code. These offsets are read from the emit call sites; the emit body itself is not in the reports.
/// </summary>
public sealed class WwiseVorbisRenderParams
{
    /// <summary>params+0x00, the data pointer.</summary>
    public float[]? Data { get; set; }
    /// <summary>params+0x04, the rate/format word.</summary>
    public int Format { get; set; }
    /// <summary>params+0x08, the status (0x2B etc.).</summary>
    public int Status { get; set; }
    /// <summary>params+0x0C, the valid frame count (set to the frame size before the call).</summary>
    public int ValidFrames { get; set; }
    /// <summary>params+0x0E, the maximum frame count.</summary>
    public int MaxFrames { get; set; }
    /// <summary>params+0x18, the start sample.</summary>
    public int StartSample { get; set; }
    /// <summary>params+0x20, the total/end sample.</summary>
    public int TotalSamples { get; set; }
    /// <summary>params+0x24, the pitch/step.</summary>
    public int Pitch { get; set; }
    /// <summary>params+0x28, the result code (0x2D DataReady or 0x2E NoMoreData; the emit sets only these).</summary>
    public int Result { get; set; }
    /// <summary>params+0x10, the marker count written by 0x9D4C24.</summary>
    public int MarkerCount { get; set; }
    /// <summary>params+0x14, the marker descriptor array written by 0x9D4C24.</summary>
    public object? MarkerArray { get; set; }
}

/// <summary>
/// The two shipped Vorbis source classes and their render slot (M6-002, correction C12; source-classes report
/// Q3/Q4). The streamed class 0xAB0448 sets up the decoder state (<c>src+0x80..+0xA8</c>, <c>+0xC8</c>), calls
/// the framing 0xAB7E40 and emits through <c>0xA73490</c>; the in-memory class 0xAB1550 keeps an internal
/// read buffer (<c>+0xEC..+0xF8</c>), calls 0xAB7E40 and emits through the same <c>0xA73490</c>. The
/// <c>params</c> offsets are settled; the emit body <c>0xA73490</c> is <b>not</b> in the reports, so
/// <see cref="Emit"/> refuses rather than guessing its framing.
///
/// This class is standalone and unwired: nothing calls it from WwisePlayback / WwiseAudioSource /
/// WwiseSongRenderer / AnimationScheduler.
/// </summary>
public sealed class WwiseVorbisSource
{
    private readonly WwiseVorbisSourceKind _kind;
    private readonly WwiseMedia _media;
    private readonly WwiseCodebookLibrary _codebooks;
    private readonly int _channels;

    // The settled source-class fields (Q4).
    /// <summary>src+0x38, the u16 channel config (1 = mono).</summary>
    public int Config { get; set; }
    /// <summary>src+0x14, the total/end sample (Q2).</summary>
    public int TotalSamples { get; set; }
    /// <summary>src+0x18, the start sample; the emit advances it by the emitted frames (Q2).</summary>
    public int StartSample { get; set; }
    /// <summary>src+0x3C, the frame count / output.</summary>
    public int FrameCount { get; set; }
    /// <summary>src+0x40, the result code.</summary>
    public int ResultCode { get; set; }
    /// <summary>src+0x48, the frames this block.</summary>
    public int FramesThisBlock { get; set; }
    /// <summary>src+0x80, the decode output pointer.</summary>
    public float[]? DecodeOutput { get; set; }
    /// <summary>src+0x84, the sample-rate/format word.</summary>
    public int Format { get; set; }
    /// <summary>src+0x88, the last frame count.</summary>
    public int LastFrameCount { get; set; }
    /// <summary>src+0x8C, the offset.</summary>
    public int Offset { get; set; }
    /// <summary>src+0x90, the ready flag.</summary>
    public int Ready { get; set; }
    /// <summary>src+0x98/+0xA0/+0xA4/+0xA8, the decode state/buffers.</summary>
    public int DecodeState { get; set; }
    public int DecodeA0 { get; set; }
    public int DecodeA4 { get; set; }
    public int DecodeA8 { get; set; }
    /// <summary>src+0xB0/+0xB4, the in-memory input read position and flag.</summary>
    public int ReadPosition { get; set; }
    public int ReadFlag { get; set; }
    /// <summary>src+0xBC, the rate.</summary>
    public int Rate { get; set; }
    /// <summary>src+0xC8, the stream offset accumulator.</summary>
    public int StreamOffset { get; set; }
    /// <summary>src+0xCC, the internal buffer size/offset.</summary>
    public int InternalBuffer { get; set; }
    /// <summary>src+0xEC..+0xF8, the in-memory read buffer / count / phase / flag.</summary>
    public byte[]? InMemoryBuffer { get; set; }
    public int InMemoryCount { get; set; }
    public int InMemoryPhase { get; set; }
    public int InMemoryFlag { get; set; }

    public WwiseVorbisSource(WwiseVorbisSourceKind kind, WwiseMedia media, WwiseCodebookLibrary codebooks,
        int channels)
    {
        _kind = kind;
        _media = media;
        _codebooks = codebooks;
        _channels = channels;
        Config = channels == 1 ? 1 : 2;
    }

    /// <summary>Which shipped class this source is (C12 / Q1d).</summary>
    public WwiseVorbisSourceKind Kind => _kind;

    /// <summary>
    /// The render slot (streamed 0xAB0448 / in-memory 0xAB1550). It decodes through the packet driver and
    /// then calls <see cref="Emit"/>.
    /// </summary>
    public WwiseVorbisRenderParams Render(int frameSize)
    {
        var setup = ParseSetupForMedia();
        var dsp = WwiseVorbisNative.CreateState(setup, _channels,
            WwiseVorbisNative.StartSkip(seeking: false, seekPosition: 0),
            WwiseVorbisNative.EndTrim(loopCount: 1, _media.Vorbis!));
        var decoded = WwiseVorbisNative.DecodeFraming(dsp, _media.Data,
            (int)_media.Vorbis!.FirstAudioPacketOffset, _media.Vorbis.SampleCount);
        int frames = decoded.Length / _channels;
        return Emit(decoded, frames, pitch: 0, rate: Format, @params: null);
    }

    private WwiseVorbisSetup ParseSetupForMedia()
    {
        var v = _media.Vorbis ?? throw new InvalidDataException("the media has no vorb header (M6-002)");
        int at = (int)v.SetupPacketOffset;
        int size = BitConverter.ToUInt16(_media.Data.Span.Slice(at, 2));
        return WwiseVorbisNative.ParseSetup(new BitReader(_media.Data.Slice(at + 2, size)), _codebooks,
            _channels, v.BlockSize0Pow, v.BlockSize1Pow);
    }

    /// <summary>
    /// The shared emit <c>0xA73490(src, buf, frames, pitch, rate, params)</c> (C13 Q2): it publishes the
    /// pointer (no copy): <c>params+0x00 = buf</c>, <c>+0x04 = rate</c> (arg5), <c>+0x0C</c>/<c>+0x0E</c> =
    /// frames u16, <c>+0x18 = [src+0x18]</c>, <c>+0x20 = [src+0x14]</c>, <c>+0x24 = pitch</c>,
    /// <c>+0x28 = 0x2E when frames == 0 else 0x2D</c>, then advances <c>[src+0x18] += frames</c>. The
    /// <c>[src+0x2C]</c> object call <c>0x9D4C24</c> builds the marker descriptor at <c>params+0x10</c>/<c>+0x14</c>
    /// (its entry writer is UNKNOWN, so the count is 0 here). It does not set <c>0x11</c> or <c>2</c>.
    /// </summary>
    public WwiseVorbisRenderParams Emit(float[] buffer, int frames, int pitch, int rate,
        WwiseVorbisRenderParams? @params)
    {
        var p = @params ?? new WwiseVorbisRenderParams();
        p.Data = buffer;                                                  // +0x00
        p.Format = rate;                                                  // +0x04 = arg5
        p.ValidFrames = frames & 0xFFFF;                                  // +0x0C u16
        p.MaxFrames = frames & 0xFFFF;                                    // +0x0E u16
        p.StartSample = StartSample;                                      // +0x18 = [src+0x18] before increment
        p.TotalSamples = TotalSamples;                                    // +0x20 = [src+0x14]
        p.Pitch = pitch;                                                  // +0x24 = arg4
        p.Result = frames == 0 ? 0x2E : 0x2D;                             // +0x28
        p.MarkerCount = 0;                                                // +0x10 (0x9D4C24)
        p.MarkerArray = null;                                             // +0x14
        StartSample += frames;                                            // [src+0x18] += frames
        return p;
    }
}