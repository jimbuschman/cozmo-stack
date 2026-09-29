// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The shipped source classes the factory <c>0xA562B8(mode, plugin, pbi)</c> can select (M6-025 B13, B14;
/// source-classes Q1c/Q1d). The class names are UNKNOWN; these are the vtable entry points.
/// </summary>
public enum WwiseSourceKind
{
    /// <summary>Registered plug-in 0x00040001, mode 1: Vorbis streamed, vtable render <c>0xAB0448</c>.</summary>
    VorbisStreamed,
    /// <summary>Registered plug-in 0x00040001, mode 3: Vorbis in-memory, render <c>0xAB1550</c>.</summary>
    VorbisInMemory,
    /// <summary>Plug-in class 2, mode 1: IMA ADPCM, <c>0xA74244</c>.</summary>
    AdpcmMode1,
    /// <summary>Plug-in class 2, mode 3: IMA ADPCM, <c>0xA72A2C</c>.</summary>
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
        Func<uint, WwiseMedia?> media, WwiseCodebookLibrary? codebooks)
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
                    wem, codebooks);
            case WwiseSourceKind.AdpcmMode1:
            case WwiseSourceKind.AdpcmMode3:
                return new WwiseAdpcmVoiceSource(wem);
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
/// The Vorbis <see cref="IWwiseVoiceSource"/> adapter (M6-025 B13, source-classes Q1d): streamed
/// <c>0xAB0448</c> / in-memory <c>0xAB1550</c> over the existing bit-exact
/// <see cref="WwiseVorbisSource"/>. The decode is M6-002's; this class only publishes its samples through
/// the voice source slot.
///
/// <para><b>Adapter note.</b> <see cref="WwiseVorbisSource.Render"/> decodes the whole media in one call;
/// the native streamed class emits block by block. This adapter decodes once at StartStream and serves
/// <see cref="WwiseVoiceBuffer.MaxFrames"/> at a time, which is what the voice/bus pass consumes.</para>
/// </summary>
public sealed class WwiseVorbisVoiceSource : IWwiseVoiceSource, IWwiseVoiceSourceFormat
{
    // fidelity: M6-025
    private readonly WwiseVorbisSource _source;
    private float[]? _samples;
    private int _position;

    /// <summary>The <c>pbi+0x158</c> format word (the descriptor's +4; caller input, see the interface).</summary>
    public uint SourceFormatWord { get; }

    public WwiseVorbisVoiceSource(
        WwiseVorbisSourceKind kind, WwiseMedia media, WwiseCodebookLibrary codebooks, uint sourceFormatWord = 0)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(codebooks);
        if (media.Codec != WwiseCodec.Vorbis)
            throw new ArgumentException($"not a Vorbis media: format tag 0x{media.FormatTag:X4}", nameof(media));
        _source = new WwiseVorbisSource(kind, media, codebooks, media.Channels);
        Channels = media.Channels;
        SampleRate = media.SampleRate;
        SourceFormatWord = sourceFormatWord;
    }

    /// <summary>Vorbis <c>src+0x38</c> config: mono 1, stereo 2 (M6-002).</summary>
    public int Channels { get; }

    /// <summary>The media's sample rate, for the voice-stage resampler (M6-004).</summary>
    public int SampleRate { get; }

    /// <summary>Whether <see cref="StartStream"/> decoded the media.</summary>
    public bool StartStreamSucceeded { get; private set; }

    /// <summary>
    /// <c>vt+0x28</c> StartStream: run M6-002's decode and hold the samples. Returns true on success.
    /// </summary>
    public bool StartStream()
    {
        var rendered = _source.Render(WwiseRuntimeSettings.SamplesPerFrame);
        _samples = rendered.Data;
        _position = 0;
        StartStreamSucceeded = _samples is not null;
        return StartStreamSucceeded;
    }

    /// <summary><c>vt+0x30</c> render: publish the next block. Returns 0x2D while data remains, else 0x2E.</summary>
    public int Render(WwiseVoiceBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
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
/// The IMA ADPCM <see cref="IWwiseVoiceSource"/> adapter (M6-025 B13): mode 1 <c>0xA74244</c> / mode 3
/// <c>0xA72A2C</c> over <see cref="WwiseAdpcm"/> (M6-003, bit-exact).
/// </summary>
public sealed class WwiseAdpcmVoiceSource : IWwiseVoiceSource, IWwiseVoiceSourceFormat
{
    // fidelity: M6-025
    private readonly WwiseMedia _media;
    private short[]? _samples;
    private int _position;

    /// <summary>The <c>pbi+0x158</c> format word (the descriptor's +4; caller input).</summary>
    public uint SourceFormatWord { get; }

    public WwiseAdpcmVoiceSource(WwiseMedia media, uint sourceFormatWord = 0)
    {
        ArgumentNullException.ThrowIfNull(media);
        if (media.Codec != WwiseCodec.Adpcm)
            throw new ArgumentException($"not an ADPCM media: format tag 0x{media.FormatTag:X4}", nameof(media));
        _media = media;
        Channels = media.Channels;
        SampleRate = media.SampleRate;
        SourceFormatWord = sourceFormatWord;
    }

    public int Channels { get; }
    public int SampleRate { get; }
    public bool StartStreamSucceeded { get; private set; }

    /// <summary><c>vt+0x28</c> StartStream: decode through M6-003 and hold the interleaved samples.</summary>
    public bool StartStream()
    {
        _samples = WwiseAdpcm.Decode(_media);
        _position = 0;
        StartStreamSucceeded = true;
        return true;
    }

    /// <summary><c>vt+0x30</c> render: publish the next block, int16 scaled by 1/32768.</summary>
    public int Render(WwiseVoiceBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
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