// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The shipped source classes the factory <c>0xA562B8(mode, plugin, pbi)</c> can select (M6-025 B13, B14;
/// source-classes Q1c/Q1d). The class names are UNKNOWN; these are the vtable entry points.
/// </summary>
public enum WwiseSourceKind
{
    /// <summary>
    /// Registered plug-in 0x00040001, mode 1: Vorbis streamed, the 0xFC-byte class of vtable <c>0x103E138</c>: <c>vt+0x28 = 0xAB22D4</c> StartStream, <c>vt+0x30 = 0xAB1550</c> decode, <c>vt+0x78 = 0xAB12B4</c>
    /// (C32.3 corrected the earlier labels, which had the two vtables and their render bodies the other way round).
    /// </summary>
    VorbisStreamed,
    /// <summary>Registered plug-in 0x00040001, mode 3: Vorbis in-memory, the 0xD0-byte class of vtable <c>0x103E0B8</c>: <c>vt+0x28 = 0xAB0B20</c>, <c>vt+0x30 = 0xAB0448</c> (C32.3).</summary>
    VorbisInMemory,
    /// <summary>Plug-in class 2, mode 1: IMA ADPCM, <c>0xA74244</c> (the 0x70-byte stream class; its StartStream is <c>0xA7538C</c>).</summary>
    AdpcmMode1,
    /// <summary>Plug-in class 2, mode 3: IMA ADPCM, <c>0xA72A2C</c> (the 0x48-byte in-memory class).</summary>
    AdpcmMode3,
    /// <summary>Plug-in class 1, mode 1: PCM, <c>0xA76140</c>. No shipped bank uses it.</summary>
    PcmMode1,
    /// <summary>Plug-in class 1, mode 3: PCM, <c>0xA72D04</c>. No shipped bank uses it.</summary>
    PcmMode3,
    /// <summary><c>mode == 2</c>: the <c>0xA78D10</c> class (a 0x70-byte object). Body unread.</summary>
    PluginMode2,
}

/// <summary>
/// The source factory <c>0xA562B8</c> (M6-025 B13) and the stream-byte mode mapping <c>0x9B9CD0..0x9B9DBC</c>
/// (B14). The shipped robot audio is Vorbis (1826 stream 1 + 27 stream 0) plus 333 ADPCM; no bank uses the
/// PCM classes.
///
/// <para><b>Gap, named.</b> The native mode is <c>(descriptor+0xc &amp; 0x7f) &gt;&gt; 2</c> (B12), but the
/// <c>0x9CD340</c> descriptor's layout is a RECOVERABLE_GAP. B14 settles the codec path's mapping from the
/// stream byte instead (stream 0 → mode 3, stream 1/2 → mode 1, codec plug-ins only); that is what
/// <see cref="ModeForStream"/> implements. A caller that has the descriptor's mode can pass it directly.</para>
/// </summary>
public static class WwiseSourceFactory
{
    /// <summary>Vorbis registered plug-in id (B13/B14).</summary>
    public const uint VorbisPlugin = 0x00040001;

    /// <summary>IMA ADPCM plug-in id (B13/B14).</summary>
    public const uint AdpcmPlugin = 0x00020001;

    /// <summary>
    /// B14 (<c>0x9B9CD0..0x9B9DBC</c>): the stream-byte mode mapping. Only codec plug-ins
    /// (<c>plugin &amp; 0xF == 1</c>) take it; a source plug-in leaves mode 0.
    /// </summary>
    public static int ModeForStream(uint plugin, byte stream)
    {
        if ((plugin & 0x0F) != 1) return 0;
        return stream switch
        {
            0 => 3,
            1 or 2 => 1,
            _ => 0,
        };
    }

    /// <summary>
    /// B13: the factory's selection. <c>mode == 2</c> is the <c>0xA78D10</c> class; the plugin's high half
    /// selects PCM (1), ADPCM (2) or the registered list (&gt; 2); <c>mode == 0</c> or an unknown plugin
    /// returns no class (the native returns 0).
    /// </summary>
    public static WwiseSourceKind? Select(int mode, uint plugin)
    {
        if (mode == 0) return null;                                          // 0xA562D4 cmp r3,#0; 0xA562D8 beq 0xA563B4
        if (mode == 2) return WwiseSourceKind.PluginMode2;

        int cls = (int)(plugin >> 16);
        if (cls == 1)
            return mode == 3 ? WwiseSourceKind.PcmMode3 : WwiseSourceKind.PcmMode1;
        if (cls == 2)
            return mode == 3 ? WwiseSourceKind.AdpcmMode3 : WwiseSourceKind.AdpcmMode1;
        if (cls > 2)
        {
            // Registered list 0x108D9DC: 3-word entries {id, fnMode1, fnOtherwise}. The only shipped
            // registered plug-in is Vorbis; mode 1 is the streamed class, otherwise the in-memory one.
            if (plugin == VorbisPlugin)
                return mode == 1 ? WwiseSourceKind.VorbisStreamed : WwiseSourceKind.VorbisInMemory;
            return null;
        }
        return null;
    }

    /// <summary>
    /// Creates the source object for a selection. <paramref name="media"/> resolves the descriptor's media
    /// id; <paramref name="codebooks"/> is required for Vorbis. The PCM and mode-2 classes have no read
    /// body, so they throw rather than being approximated.
    /// </summary>
    public static IWwiseVoiceSource Create(
        WwiseSourceKind kind, WwiseSourceDescriptor descriptor,
        Func<uint, WwiseMedia?> media, WwiseCodebookLibrary? codebooks,
        WwiseStreamingContext? streaming = null)
    {
        ArgumentNullException.ThrowIfNull(media);

        // The unread classes are refused before the media is resolved, so the refusal is the visible reason.
        switch (kind)
        {
            case WwiseSourceKind.PcmMode1:
            case WwiseSourceKind.PcmMode3:
                throw new NotSupportedException(
                    "M6-025 B13: the PCM source classes (0xA76140/0xA72D04) have no read render body; " +
                    "no shipped bank uses them");
            case WwiseSourceKind.PluginMode2:
                throw new NotSupportedException(
                    "M6-025 B13: the mode==2 class (0xA78D10) has no read render body");
        }

        var wem = media(descriptor.SourceId)
            ?? throw new InvalidDataException($"media {descriptor.SourceId} is not present (M6-025 B14)");

        switch (kind)
        {
            case WwiseSourceKind.VorbisStreamed:
            case WwiseSourceKind.VorbisInMemory:
                if (codebooks is null)
                    throw new NotSupportedException(
                        "M6-025 B13: Vorbis needs the packed codebook library (M6-002)");
                return new WwiseVorbisVoiceSource(
                    kind == WwiseSourceKind.VorbisStreamed ? WwiseVorbisSourceKind.Streamed : WwiseVorbisSourceKind.InMemory,
                    wem, codebooks, streaming: kind == WwiseSourceKind.VorbisStreamed ? streaming : null);
            case WwiseSourceKind.AdpcmMode1:
            case WwiseSourceKind.AdpcmMode3:
                return new WwiseAdpcmVoiceSource(wem, streaming: kind == WwiseSourceKind.AdpcmMode1 ? streaming : null,
                    streamed: kind == WwiseSourceKind.AdpcmMode1);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
}

/// <summary>
/// A source that exposes the <c>pbi+0x158</c> source-format word. The word is the <c>0x9CD340</c>
/// descriptor's <c>+4</c> (M6-025 B15, residual Q2); the descriptor's layout is a RECOVERABLE_GAP, so the
/// value is a caller input, not derived here.
/// </summary>
public interface IWwiseVoiceSourceFormat
{
    /// <summary>The <c>pbi+0x158</c> value the source's StartStream writes.</summary>
    uint SourceFormatWord { get; }
}

/// <summary>
/// The inputs a streamed source needs beyond its media (B-M6b-4 batch 5b): the stream manager, the owner PBI (<c>[S+0xC]</c>), the source block (<c>[[S+0xC]+0x150]</c>) and the seams of the bodies that
/// C33 does not adopt. Without it a streamed source's StartStream throws (<see cref="WwiseMissingBehaviourException"/>); there is no fallback to the offline decode.
/// </summary>
public sealed class WwiseStreamingContext
{
    /// <summary>The stream manager (<c>[0x108D798+0x10]</c>).</summary>
    public required WwiseStreamManager Manager { get; init; }

    /// <summary>The owner PBI.</summary>
    public required WwisePlayingInstance Pbi { get; init; }

    /// <summary>The source block fields the stream functions read.</summary>
    public required WwiseSourceBlock150 Block { get; init; }

    /// <summary>The seams.</summary>
    public required WwiseStreamSourceSeams Seams { get; init; }
}

/// <summary>
/// A source whose <c>vt+0x28</c> writes the PBI's format bytes (<c>pbi+0x158..0x162</c>) itself (the streamed classes, 0xAB12B4): the bridge's <c>SourceFormatWriter15C</c> seam is not needed for it.
/// </summary>
public interface IWwiseStreamingVoiceSource
{
    /// <summary>True when StartStream's own header parse writes the PBI format bytes.</summary>
    bool WritesSourceFormatInStartStream { get; }
}

/// <summary>
/// The Vorbis <see cref="IWwiseVoiceSource"/> adapter (M6-025 B13, source-classes Q1d). The streamed kind (mode 1, the 0xFC-byte class of vtable <c>0x103E138</c>) runs the engine's own StartStream
/// <c>0xAB22D4</c> (<see cref="WwiseVorbisStreamSource.StartStreamAB22D4"/>) and returns its raw result (1, 0x3F, 2, 7, 8, 0x34, ...); its decode (<c>vt+0x30 = 0xAB1550</c>) runs the buffering gate of that
/// body and then the offline decoder <see cref="WwiseVorbisSource"/> (M6-002), which is the named SEAM for the packet decode <c>0xAB7E40</c> and the output hand-off <c>0xA73490</c> (no adopted row reads them).
/// The in-memory kind (mode 3, class <c>0x103E0B8</c>, <c>vt+0x28 = 0xAB0B20</c>) keeps the earlier behaviour (the body of 0xAB0B20 is not adopted): the media is decoded at StartStream and the result is 1.
/// </summary>
public sealed class WwiseVorbisVoiceSource : IWwiseVoiceSource, IWwiseVoiceSourceFormat, IWwiseStreamingVoiceSource
{
    // fidelity: M6-025
    private readonly WwiseVorbisSource _source;
    private readonly WwiseVorbisSourceKind _kind;
    private readonly WwiseVorbisStreamSource? _stream;
    private float[]? _samples;
    private int _position;
    private bool _latch;

    /// <summary>The <c>pbi+0x158</c> format word (the descriptor's +4; caller input, see the interface).</summary>
    public uint SourceFormatWord { get; }

    /// <summary>Creates the adapter; <paramref name="streaming"/> is required for the streamed kind to start.</summary>
    public WwiseVorbisVoiceSource(
        WwiseVorbisSourceKind kind, WwiseMedia media, WwiseCodebookLibrary codebooks, uint sourceFormatWord = 0,
        WwiseStreamingContext? streaming = null)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(codebooks);
        if (media.Codec != WwiseCodec.Vorbis)
            throw new ArgumentException($"not a Vorbis media: format tag 0x{media.FormatTag:X4}", nameof(media));
        _kind = kind;
        _source = new WwiseVorbisSource(kind, media, codebooks, media.Channels);
        Channels = media.Channels;
        SampleRate = media.SampleRate;
        SourceFormatWord = sourceFormatWord;
        if (kind == WwiseVorbisSourceKind.Streamed && streaming is not null)
            _stream = new WwiseVorbisStreamSource(streaming.Manager, streaming.Pbi, streaming.Block, streaming.Seams);
    }

    /// <summary>The engine's stream functions for this source (the streamed kind with a context), or null.</summary>
    public WwiseVorbisStreamSource? StreamSource => _stream;

    /// <inheritdoc />
    public bool WritesSourceFormatInStartStream => _stream is not null;

    /// <summary>Vorbis <c>src+0x38</c> config: mono 1, stereo 2 (M6-002).</summary>
    public int Channels { get; }

    /// <summary>The media's sample rate, for the voice-stage resampler (M6-004).</summary>
    public int SampleRate { get; }

    /// <summary>The <c>[source+0x10]</c> bit 0 latch, written only by <c>0xA56650</c> (<see cref="WwiseVoiceSourceStart.StartA56650"/>).</summary>
    public bool StartStreamSucceeded
    {
        get => _stream?.StartLatch ?? _latch;
        set { if (_stream is not null) _stream.StartLatch = value; else _latch = value; }
    }

    /// <summary>
    /// <c>vt+0x28</c> StartStream. Streamed: <c>0xAB22D4</c> with the PBI's own <c>[pbi+0x1DC]</c> / <c>[pbi+0x1E0]</c> (the two arguments are ignored, as the engine ignores them: C32.3), the raw result.
    /// In-memory: the offline decode and 1 (unchanged, 0xAB0B20 not adopted).
    /// </summary>
    public int StartStream(uint arg1DC, uint arg1E0)
    {
        if (_kind == WwiseVorbisSourceKind.Streamed)
        {
            var stream = _stream ?? throw new WwiseMissingBehaviourException(
                "M6-025 C33.3: a streamed Vorbis source needs a WwiseStreamingContext; there is no fallback to the offline decode");
            return stream.StartStreamAB22D4();
        }
        var rendered = _source.Render(WwiseRuntimeSettings.SamplesPerFrame);
        _samples = rendered.Data;
        _position = 0;
        if (_samples is null)
            throw new WwiseMissingBehaviourException(
                "M6-025 C27 step 7: the in-memory Vorbis source's vt+0x28 (0xAB0B20) is not adopted; no result code is invented");
        return 1;
    }

    /// <summary>
    /// <c>vt+0x30</c> render. Streamed: the buffering gate of <c>0xAB1550</c> (<see cref="WwiseVorbisStreamSource.DecodeGateAB1550"/>), whose status is returned with no frames when it blocks the decode; then the
    /// offline decode (the named seam), run once. Returns 0x2D while data remains, else 0x2E.
    /// </summary>
    public int Render(WwiseVoiceBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (_stream is not null)
        {
            var gate = _stream.DecodeGateAB1550();
            if (!gate.Decode)
            {
                buffer.ValidFrames = 0;
                buffer.Result = gate.Result;
                return gate.Result;
            }
            if (_samples is null)
            {
                _samples = _source.Render(WwiseRuntimeSettings.SamplesPerFrame).Data
                    ?? throw new WwiseMissingBehaviourException("M6-025 G7: the offline decode seam produced no samples");
                _position = 0;
            }
        }
        if (_samples is null) return 0x2E;
        int channels = Channels;
        int frames = Math.Min(buffer.MaxFrames, _samples.Length / channels - _position);
        if (frames < 0) frames = 0;
        for (int f = 0; f < frames; f++)
            for (int c = 0; c < channels; c++)
                buffer.Channels[c][f] = _samples[(_position + f) * channels + c];
        _position += frames;
        buffer.ValidFrames = frames;
        buffer.Result = frames == 0 ? 0x2E : 0x2D;
        return buffer.Result;
    }
}

/// <summary>
/// The IMA ADPCM <see cref="IWwiseVoiceSource"/> adapter (M6-025 B13): mode 1 <c>0xA74244</c> (the stream class, <c>vt+0x28 = 0xA7538C</c>) / mode 3 <c>0xA72A2C</c> over <see cref="WwiseAdpcm"/> (M6-003, bit-exact).
/// The streamed kind runs the engine's StartStream (<see cref="WwisePcmAdpcmStreamSource.StartStreamA7538C"/>) and returns its raw result; the header parse it calls (<c>0xA73ABC</c>) is a required seam. The decode
/// (<c>vt+0x30 = 0xA73D34</c>) is not adopted: the media is decoded offline at the first render. The in-memory kind keeps the earlier behaviour (decode at StartStream, result 1).
/// </summary>
public sealed class WwiseAdpcmVoiceSource : IWwiseVoiceSource, IWwiseVoiceSourceFormat, IWwiseStreamingVoiceSource
{
    // fidelity: M6-025
    private readonly WwiseMedia _media;
    private readonly bool _streamed;
    private readonly WwisePcmAdpcmStreamSource? _stream;
    private short[]? _samples;
    private int _position;
    private bool _latch;

    /// <summary>The <c>pbi+0x158</c> format word (the descriptor's +4; caller input).</summary>
    public uint SourceFormatWord { get; }

    /// <summary>Creates the adapter.</summary>
    public WwiseAdpcmVoiceSource(WwiseMedia media, uint sourceFormatWord = 0, WwiseStreamingContext? streaming = null, bool streamed = false)
    {
        ArgumentNullException.ThrowIfNull(media);
        if (media.Codec != WwiseCodec.Adpcm)
            throw new ArgumentException($"not an ADPCM media: format tag 0x{media.FormatTag:X4}", nameof(media));
        _media = media;
        _streamed = streamed;
        Channels = media.Channels;
        SampleRate = media.SampleRate;
        SourceFormatWord = sourceFormatWord;
        if (streamed && streaming is not null)
            _stream = new WwisePcmAdpcmStreamSource(streaming.Manager, streaming.Pbi, streaming.Block, streaming.Seams);
    }

    /// <summary>The engine's stream functions for this source (the streamed kind with a context), or null.</summary>
    public WwisePcmAdpcmStreamSource? StreamSource => _stream;

    /// <inheritdoc />
    public bool WritesSourceFormatInStartStream => false;

    /// <summary>The channel count.</summary>
    public int Channels { get; }

    /// <summary>The sample rate.</summary>
    public int SampleRate { get; }

    /// <summary>The <c>[source+0x10]</c> bit 0 latch, written only by <c>0xA56650</c> (<see cref="WwiseVoiceSourceStart.StartA56650"/>).</summary>
    public bool StartStreamSucceeded
    {
        get => _stream?.StartLatch ?? _latch;
        set { if (_stream is not null) _stream.StartLatch = value; else _latch = value; }
    }

    /// <summary>
    /// <c>vt+0x28</c> StartStream. Streamed: <c>0xA7538C</c> and its raw result (the two arguments are the PBI's own pair, read inside). In-memory: the decode through M6-003 and the result 1 (unchanged).
    /// </summary>
    public int StartStream(uint arg1DC, uint arg1E0)
    {
        if (_streamed)
        {
            var stream = _stream ?? throw new WwiseMissingBehaviourException(
                "M6-025 C33.3: a streamed ADPCM source needs a WwiseStreamingContext; there is no fallback to the offline decode");
            return stream.StartStreamA7538C();
        }
        _samples = WwiseAdpcm.Decode(_media);
        _position = 0;
        return 1;
    }

    /// <summary><c>vt+0x30</c> render: publish the next block, int16 scaled by 1/32768 (the streamed class's gate <c>0xA73D34</c> is not adopted: the whole media is decoded offline on first use).</summary>
    public int Render(WwiseVoiceBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (_streamed && _samples is null)
        {
            _samples = WwiseAdpcm.Decode(_media);
            _position = 0;
        }
        if (_samples is null) return 0x2E;
        int channels = Channels;
        int frames = Math.Min(buffer.MaxFrames, _samples.Length / channels - _position);
        if (frames < 0) frames = 0;
        for (int f = 0; f < frames; f++)
            for (int c = 0; c < channels; c++)
                buffer.Channels[c][f] = _samples[(_position + f) * channels + c] / 32768f;
        _position += frames;
        buffer.ValidFrames = frames;
        buffer.Result = frames == 0 ? 0x2E : 0x2D;
        return buffer.Result;
    }
}
