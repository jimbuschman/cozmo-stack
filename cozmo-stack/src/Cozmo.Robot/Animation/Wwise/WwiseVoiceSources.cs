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
                    wem, codebooks, streaming: streaming);
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
/// A source whose voice owns a pitch node (<c>voice+0x100</c>, linked to the source at <c>[node+4]</c>) that takes its blocks through <c>0xA52D4C</c>: the engine classes (the Vorbis kinds and the streamed ADPCM class).
/// <see cref="WwiseLiveVoice.Render"/> runs the engine's order for such a source (<c>0xA44630</c>): the source's <c>vt+0x30</c> (<see cref="IWwiseVoiceSource.Render"/>: the decode only) is called inside the
/// <c>0x2B</c> loop <c>0xA4475C..0xA44788</c>, and the voice (not the source) calls the intake.
/// </summary>
// fidelity: M6-022
public interface IWwisePitchNodeSource : IWwiseVoiceSource
{
    /// <summary>True when the source runs in the engine's order (it has a pitch node and <see cref="IWwiseVoiceSource.Render"/> is the decode only). False for the in-memory ADPCM class, whose body is unread.</summary>
    bool HasPitchNode { get; }

    /// <summary>The 0x28-byte io state the source fills: the voice's pass block <c>state</c> (P01). The voice assigns <see cref="WwiseVoiceBuffer.State"/> before every <c>vt+0x30</c> call: the engine hands the one block to whichever source is current.</summary>
    WwiseDecodeState Io { get; set; }

    /// <summary><c>vt+0xC</c> (<c>0xA52F18</c>, <c>0xA52FD0</c>): the delivered block is released (the streamed Vorbis class <c>0xAB1100</c>, the in-memory <c>0xAB032C</c>, the ADPCM stream <c>0xA73A14</c>).</summary>
    void ReleaseOutput();

    /// <summary>The owner PBI <c>[source+0xC]</c>: <c>vt+0x20 = 0xA5668C</c> returns its <c>+0x44</c> (the effective pitch) and the pitch pass reads <c>u16[+0x1BE] &amp; 0x380</c> from it. Null: the pitch pass is a visible stop.</summary>
    WwisePlayingInstance? Owner { get; }
}

/// <summary>
/// The Vorbis <see cref="IWwiseVoiceSource"/> adapter (M6-025 B13, source-classes Q1d). The streamed kind (mode 1, the 0xFC-byte class of vtable <c>0x103E138</c>) runs the engine's StartStream <c>0xAB22D4</c>
/// (<see cref="WwiseVorbisStreamSource.StartStreamAB22D4"/>) and its decode <c>vt+0x30 = 0xAB1550</c> (<see cref="WwiseVorbisStreamSource.DecodeAB1550"/>: the packet collection, the frame loop <c>0xAB7E40</c> and the output
/// hand-off <c>0xA73490</c>); the in-memory kind (mode 3, the 0xD0-byte class of vtable <c>0x103E0B8</c>) runs <c>0xAB0B20</c> (<see cref="WwiseVorbisInMemorySource.StartStreamAB0B20"/>) and <c>0xAB0448</c>. Both return the engine's raw
/// StartStream result (1, 0x3F, 2, 7, 8, 0x34, ...). There is no fallback to an offline decode: a source without a <see cref="WwiseStreamingContext"/> throws <see cref="WwiseMissingBehaviourException"/> at StartStream.
/// <see cref="Render"/> is <c>vt+0x30</c> only: the decode fills the io state (<see cref="Io"/>, P01) and returns its result. The voice (<see cref="WwiseLiveVoice.Render"/>) calls it inside the <c>0x2B</c> loop and then,
/// for a result of 0x11 / 0x2D, the voice's pitch node's intake (<see cref="WwiseLiveVoice.PitchNode"/>, <c>0xA52D4C</c>: a 0x2D with no valid frames becomes 0x2B, the voice then calls the source again; the node consumes the block).
/// </summary>
public sealed class WwiseVorbisVoiceSource : IWwisePitchNodeSource, IWwiseVoiceSourceFormat, IWwiseStreamingVoiceSource
{
    // fidelity: M6-025
    private readonly WwiseVorbisSourceKind _kind;
    private readonly WwiseVorbisStreamSource? _stream;
    private readonly WwiseVorbisInMemorySource? _mem;
    private readonly WwiseStreamingContext? _ctx;

    /// <summary>The <c>pbi+0x158</c> format word (the descriptor's +4; caller input, see the interface).</summary>
    public uint SourceFormatWord { get; }

    /// <summary>Creates the adapter; <paramref name="streaming"/> is required for either kind to start.</summary>
    public WwiseVorbisVoiceSource(
        WwiseVorbisSourceKind kind, WwiseMedia media, WwiseCodebookLibrary codebooks, uint sourceFormatWord = 0,
        WwiseStreamingContext? streaming = null)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(codebooks);
        if (media.Codec != WwiseCodec.Vorbis)
            throw new ArgumentException($"not a Vorbis media: format tag 0x{media.FormatTag:X4}", nameof(media));
        _kind = kind;
        _ctx = streaming;
        Channels = media.Channels;
        SampleRate = media.SampleRate;
        SourceFormatWord = sourceFormatWord;
        if (streaming is not null)
        {
            streaming.Seams.Vorbis.Codebooks ??= codebooks;                     // the packed library is the engine's static table: a host input of the shared context
            if (kind == WwiseVorbisSourceKind.Streamed)
                _stream = new WwiseVorbisStreamSource(streaming.Manager, streaming.Pbi, streaming.Block, streaming.Seams);
            else
                _mem = new WwiseVorbisInMemorySource(streaming.Pbi, streaming.Seams.Vorbis, streaming.Manager.TryAlloc, streaming.Seams.PoolFree, streaming.Seams.BaseDestructor);
        }
        Io = new WwiseDecodeState();
    }

    /// <summary>The engine's stream functions for this source (the streamed kind with a context), or null.</summary>
    public WwiseVorbisStreamSource? StreamSource => _stream;

    /// <summary>The in-memory class's functions (the in-memory kind with a context), or null.</summary>
    public WwiseVorbisInMemorySource? InMemorySource => _mem;

    /// <inheritdoc />
    public bool HasPitchNode => true;

    /// <inheritdoc />
    public WwisePlayingInstance? Owner => _ctx?.Pbi;

    /// <summary>The io state the decode fills (the voice's pass block, P01).</summary>
    public WwiseDecodeState Io { get; set; }

    /// <summary>
    /// <c>vt+0xC</c>: the streamed class's <c>0xAB1100</c> or the in-memory class's <c>0xAB032C</c> (V08): the delivered block is freed.
    /// </summary>
    public void ReleaseOutput()
    {
        if (_stream is not null) _stream.ReleaseOutputAB1100();
        else _mem?.ReleaseOutputAB032C();
    }

    /// <summary>
    /// <c>vt+0x2C</c>: the streamed kind is <c>0xAB2958</c> (<see cref="WwiseVorbisStreamSource.Close2CAB2958"/>), the in-memory kind <c>0xAB0FC0</c> (<see cref="WwiseVorbisInMemorySource.Close2CAB0FC0"/>) (C34.3 S6, S7).
    /// </summary>
    public void Close2C()
    {
        if (_stream is not null) { _stream.Close2CAB2958(); return; }
        (_mem ?? throw new WwiseMissingBehaviourException("M6-025 S6: a Vorbis source without a WwiseStreamingContext has no state to close")).Close2CAB0FC0();
    }

    /// <summary><c>vt+0x34</c> = <c>0xA72F5C</c> (S1) for both kinds.</summary>
    public float Duration34()
        => _stream?.Duration34A72F5C() ?? _mem?.Duration34() ?? throw new WwiseMissingBehaviourException(
            "M6-025 S1: a Vorbis source without a WwiseStreamingContext has no total or loop samples");

    /// <inheritdoc />
    public bool WritesSourceFormatInStartStream => _stream is not null || _mem is not null;

    /// <summary>Vorbis <c>src+0x38</c> config: mono 1, stereo 2 (M6-002).</summary>
    public int Channels { get; }

    /// <summary>The media's sample rate, for the voice-stage resampler (M6-004).</summary>
    public int SampleRate { get; }

    /// <summary>The <c>[source+0x10]</c> bit 0 latch, written only by <c>0xA56650</c> (<see cref="WwiseVoiceSourceStart.StartA56650"/>).</summary>
    public bool StartStreamSucceeded
    {
        get => _stream?.StartLatch ?? (_mem is not null && (_mem.Flags10 & 1) != 0);
        set
        {
            if (_stream is not null) _stream.StartLatch = value;
            else if (_mem is not null) _mem.Flags10 = (byte)((_mem.Flags10 & ~1) | (value ? 1 : 0));
        }
    }

    /// <summary>
    /// <c>vt+0x28</c> StartStream. Streamed: <c>0xAB22D4</c> with the PBI's own <c>[pbi+0x1DC]</c> / <c>[pbi+0x1E0]</c> (the two arguments are ignored, as the engine ignores them: C32.3), the raw result. In-memory:
    /// <c>0xAB0B20(S, data, size)</c> with <paramref name="arg1DC"/> resolved through the bank memory and <paramref name="arg1E0"/> as the size.
    /// </summary>
    public int StartStream(uint arg1DC, uint arg1E0)
    {
        if (_stream is not null) return _stream.StartStreamAB22D4();
        var mem = _mem ?? throw new WwiseMissingBehaviourException(
            $"M6-025 C33.3: a {_kind} Vorbis source needs a WwiseStreamingContext; there is no fallback to the offline decode");
        WwiseBytePtr data = default;
        if (arg1DC != 0)
        {
            var memory = _ctx!.Seams.Memory ?? throw new WwiseMissingBehaviourException("M6-025 V17: [pbi+0x1DC] is a bank-memory address; supply WwiseStreamSourceSeams.Memory");
            var (block, offset) = memory.Resolve(arg1DC);
            data = new WwiseBytePtr(block, offset);
        }
        return mem.StartStreamAB0B20(data, arg1E0);
    }

    /// <summary>
    /// <c>vt+0x30</c>: the decode (<c>0xAB1550</c> / <c>0xAB0448</c>) fills <see cref="Io"/> (<c>[state+0xC]</c>, <see cref="WwiseVoiceBuffer.ValidFrames"/>, is set by the voice to <c>u16[0x1052440]</c> before the call) and
    /// returns its result. The 0x2E handler <c>0xA55C14</c>, the intake <c>0xA52D4C</c> and the 0x2B loop are the voice's (<see cref="WwiseLiveVoice.Render"/>, <c>0xA44768..0xA44788</c>).
    /// </summary>
    public int Render(WwiseVoiceBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Io.MaxFrames = unchecked((ushort)buffer.ValidFrames);                   // 0xA4478C..0xA4479C strh r2,[r5,#0xc]
        if (_stream is not null) _stream.DecodeAB1550(Io);                      // 0xAB1550
        else (_mem ?? throw new WwiseMissingBehaviourException("M6-025 G7: a Vorbis source without a WwiseStreamingContext cannot decode")).DecodeAB0448(Io);   // 0xAB0448
        buffer.Result = Io.Code28;
        return buffer.Result;
    }
}

/// <summary>
/// The IMA ADPCM <see cref="IWwiseVoiceSource"/> adapter (M6-025 B13): mode 1 <c>0xA74244</c> (the stream class, <c>vt+0x28 = 0xA7538C</c>) / mode 3 <c>0xA72A2C</c> over <see cref="WwiseAdpcm"/> (M6-003, bit-exact).
/// The streamed kind runs the engine's StartStream (<see cref="WwisePcmAdpcmStreamSource.StartStreamA7538C"/>, with the header parse <c>0xA73ABC</c>) and returns its raw result; its decode <c>vt+0x30 = 0xA73D34</c>
/// (<see cref="WwisePcmAdpcmStreamSource.DecodeA73D34"/>) fills the io state and returns its result (the voice runs the pitch node's intake, as for the Vorbis adapter). The in-memory kind (mode 3, <c>0xA72A2C</c>; no shipped bank reaches it, its body is
/// unread) keeps the earlier behaviour (decode at StartStream, result 1) unchanged.
/// </summary>
public sealed class WwiseAdpcmVoiceSource : IWwisePitchNodeSource, IWwiseVoiceSourceFormat, IWwiseStreamingVoiceSource
{
    // fidelity: M6-025
    private readonly WwiseMedia _media;
    private readonly bool _streamed;
    private readonly WwisePcmAdpcmStreamSource? _stream;
    private readonly WwiseStreamingContext? _ctx;
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
        _ctx = streaming;
        _streamed = streamed;
        Channels = media.Channels;
        SampleRate = media.SampleRate;
        SourceFormatWord = sourceFormatWord;
        if (streamed && streaming is not null)
            _stream = new WwisePcmAdpcmStreamSource(streaming.Manager, streaming.Pbi, streaming.Block, streaming.Seams) { Class = WwisePcmAdpcmClass.AdpcmStream };
        Io = new WwiseDecodeState();
    }

    /// <summary>The engine's stream functions for this source (the streamed kind with a context), or null.</summary>
    public WwisePcmAdpcmStreamSource? StreamSource => _stream;

    /// <inheritdoc />
    public bool HasPitchNode => _streamed;

    /// <inheritdoc />
    public WwisePlayingInstance? Owner => _ctx?.Pbi;

    /// <summary>The io state the decode fills (the voice's pass block, P01).</summary>
    public WwiseDecodeState Io { get; set; }

    /// <summary><c>vt+0xC = 0xA73A14</c> (the streamed kind): the delivered block is freed.</summary>
    public void ReleaseOutput() => _stream?.ReleaseOutputA73A14();

    /// <summary>The in-memory class's close state (<c>[src+0x2C]</c>, <c>[src+0x44]</c>): written by the unread in-memory StartStream <c>0xA72A2C</c>, so it is host input. Required to close the in-memory kind.</summary>
    public WwiseInMemorySourceFields? InMemoryFields { get; set; }

    /// <summary>The pool free sink the in-memory close reports to (the streamed kind uses its seams').</summary>
    public WwisePoolFree? PoolFree { get; set; }

    /// <summary><c>vt+0x2C</c>: the streamed kind is <c>0xA7427C</c> (<see cref="WwisePcmAdpcmStreamSource.Close2C"/>), the in-memory kind <c>0xA72AF4</c> over <see cref="InMemoryFields"/> (C34.3 S3, S4).</summary>
    public void Close2C()
    {
        if (_stream is not null) { _stream.Close2C(); return; }
        (InMemoryFields ?? throw new WwiseMissingBehaviourException(
            "M6-025 S3: the in-memory ADPCM class's state is written by the unread StartStream 0xA72A2C; supply WwiseAdpcmVoiceSource.InMemoryFields")).Close2C(PoolFree);
    }

    /// <summary><c>vt+0x34</c> = <c>0xA72F5C</c> (S1) for the streamed kind; the in-memory kind's fields come from the unread <c>0xA7279C..0xA7287C</c>, so it throws.</summary>
    public float Duration34()
        => _stream?.Duration34A72F5C() ?? throw new WwiseMissingBehaviourException(
            "M6-025 S1: the in-memory ADPCM class's total and loop samples ([src+0x14], [src+0x24], [src+0x28]) are written by the unread 0xA7279C..0xA7287C");

    /// <inheritdoc />
    public bool WritesSourceFormatInStartStream => _stream is not null;

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

    /// <summary>
    /// <c>vt+0x30</c> render. Streamed: <c>0xA73D34</c> fills the io state and returns its result (the voice then runs the intake, as for <see cref="WwiseVorbisVoiceSource.Render"/>). In-memory (unchanged, the class is unread:
    /// MISSING; it has no pitch node here and is not run in the engine's order): publish the next block, int16 scaled by 1/32768.
    /// </summary>
    public int Render(WwiseVoiceBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (_streamed)
        {
            var stream = _stream ?? throw new WwiseMissingBehaviourException(
                "M6-025 C33.3: a streamed ADPCM source needs a WwiseStreamingContext; there is no fallback to the offline decode");
            Io.MaxFrames = unchecked((ushort)buffer.ValidFrames);               // 0xA4478C..0xA4479C
            stream.DecodeA73D34(Io);                                            // 0xA73D34
            buffer.Result = Io.Code28;
            return buffer.Result;
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
