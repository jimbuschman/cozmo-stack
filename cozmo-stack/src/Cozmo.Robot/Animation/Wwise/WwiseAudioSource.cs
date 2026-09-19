namespace Cozmo.Robot.Animation.Wwise;

/// <summary>Why a particular event could not be turned into sound.</summary>
public sealed record WwiseMiss(long EventId, string? Name, string Reason, WwiseCodec Codec);

/// <summary>
/// Plays Cozmo's own shipped sounds, by resolving an animation's audio event id through the Wwise banks
/// to a media file and decoding it.
///
/// This is a drop-in <see cref="IAnimationAudioSource"/>: it sits behind the same seam
/// <see cref="WavAudioSource"/> uses, so the frozen M3 audio path and the M5 scheduler are untouched.
/// The scheduler asks for PCM at <see cref="CozmoAudio.SampleRate"/>; this class resamples and mixes
/// down to mono to meet that, exactly as the WAV source already does.
///
/// **Codec coverage.** Both codecs the shipped library uses decode here: all 2019 Wwise Vorbis files and
/// the 220 mono ADPCM files. What does not decode is reported through <see cref="Misses"/> and returns
/// null rather than being substituted — seven stereo ADPCM files whose block layout is not established,
/// media ids with no file behind them, and bank-embedded blobs that are not audio.
/// <see cref="WwiseMedia.IsDecodable"/> is the single place that decides. See WWISE_AUDIO.md.
/// </summary>
public sealed class WwiseAudioSource : IAnimationAudioSource, IDisposable
{
    private readonly WwiseSoundLibrary _library;
    private readonly bool _ownsLibrary;
    private readonly WwiseCodebookLibrary? _codebooks;
    private readonly Dictionary<uint, short[]?> _cache = new();
    private readonly List<WwiseMiss> _misses = new();
    private readonly object _gate = new();

    /// <summary>Wraps an already-loaded library. Without codebooks, Vorbis events cannot be produced.</summary>
    public WwiseAudioSource(WwiseSoundLibrary library, bool ownsLibrary = false,
                            WwiseCodebookLibrary? codebooks = null)
    {
        _library = library;
        _ownsLibrary = ownsLibrary;
        _codebooks = codebooks ?? TryLoadCodebooks();
    }

    /// <summary>Loads the banks and media under the given directories and plays from them.</summary>
    public static WwiseAudioSource Load(params string[] directories) =>
        new(WwiseSoundLibrary.Load(directories), ownsLibrary: true);

    /// <summary>
    /// Finds the vendored packed codebook library, which Wwise Vorbis streams index into. It ships with
    /// the build output; if it is missing, Vorbis events report that rather than failing obscurely.
    /// </summary>
    public static WwiseCodebookLibrary? TryLoadCodebooks()
    {
        foreach (var dir in CodebookSearchPath())
        {
            var path = Path.Combine(dir, CodebookFileName);
            if (!File.Exists(path)) continue;
            try { return WwiseCodebookLibrary.Load(path); }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { }
        }
        return null;
    }

    /// <summary>The name of the vendored codebook file; see third-party/ww2ogg/README.md.</summary>
    public const string CodebookFileName = "packed_codebooks_aoTuV_603.bin";

    private static IEnumerable<string> CodebookSearchPath()
    {
        yield return AppContext.BaseDirectory;
        yield return Path.Combine(AppContext.BaseDirectory, "third-party", "ww2ogg");
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            yield return Path.Combine(d.FullName, "third-party", "ww2ogg");
            d = d.Parent;
        }
    }

    /// <summary>Whether the packed codebooks were found, and so whether Vorbis can be decoded at all.</summary>
    public bool CanDecodeVorbis => _codebooks is not null;

    /// <summary>The library behind this source, for diagnostics.</summary>
    public WwiseSoundLibrary Library => _library;

    /// <summary>
    /// Every event that could not be produced, and why. Empty until something has been asked for. This is
    /// the honest record of what is not yet decodable; nothing is silently substituted.
    /// </summary>
    public IReadOnlyList<WwiseMiss> Misses { get { lock (_gate) return _misses.ToList(); } }

    /// <summary>
    /// PCM for one audio event at <see cref="CozmoAudio.SampleRate"/>, mono, or null when this event
    /// cannot be produced. The scheduler treats null as "try the next alternative", which is the same
    /// contract <see cref="WavAudioSource"/> honours.
    ///
    /// Where an event resolves to several media files, they are that event's alternatives — a Wwise
    /// container picks between them at play time. The first that decodes is used, so an event with a mix
    /// of codecs still plays as long as one alternative is decodable.
    /// </summary>
    public short[]? GetPcm(long eventId, float volume)
    {
        if (eventId is < 0 or > uint.MaxValue) return null;
        uint id = (uint)eventId;

        short[]? pcm;
        lock (_gate)
        {
            if (!_cache.TryGetValue(id, out pcm))
            {
                pcm = Produce(id);
                _cache[id] = pcm;
            }
        }
        if (pcm is null) return null;
        if (Math.Abs(volume - 1f) < 0.001f) return pcm;

        var scaled = new short[pcm.Length];
        for (int i = 0; i < pcm.Length; i++)
            scaled[i] = (short)Math.Clamp((int)MathF.Round(pcm[i] * volume), short.MinValue, short.MaxValue);
        return scaled;
    }

    /// <summary>The event's authoring name, from SoundbanksInfo.xml.</summary>
    public string? NameOf(long eventId) =>
        eventId is >= 0 and <= uint.MaxValue ? _library.NameOf((uint)eventId) : null;

    /// <summary>Resolves and decodes, recording why each alternative failed when none works.</summary>
    private short[]? Produce(uint eventId)
    {
        var resolved = _library.Resolve(eventId);
        if (resolved.Media.Count == 0)
        {
            _misses.Add(new WwiseMiss(eventId, resolved.Name,
                resolved.Problem ?? "the event resolves to no media", WwiseCodec.Unknown));
            return null;
        }

        var reasons = new List<string>();
        var lastCodec = WwiseCodec.Unknown;
        foreach (var refr in resolved.Media)
        {
            if (refr.Media is not { } m)
            {
                reasons.Add($"{refr.MediaId}: {refr.Problem ?? "unreadable"}");
                continue;
            }
            lastCodec = m.Codec;
            if (!m.IsDecodable)
            {
                reasons.Add($"{refr.MediaId}: {m.UndecodableReason}");
                continue;
            }
            if (m.Codec == WwiseCodec.Vorbis && _codebooks is null)
            {
                reasons.Add($"{refr.MediaId}: Vorbis needs {CodebookFileName}, which was not found");
                continue;
            }
            var bytes = _library.ReadMedia(refr.MediaId, out _);
            if (bytes is null) { reasons.Add($"{refr.MediaId}: file went missing"); continue; }
            try
            {
                var parsed = WwiseMedia.Parse(bytes);
                if (parsed.Codec == WwiseCodec.Adpcm)
                    return ToRobotRate(WwiseAdpcm.Decode(parsed), parsed.Channels, parsed.SampleRate);
                var v = WwiseVorbis.Decode(parsed, _codebooks!);
                return ToRobotRate(v.Samples, v.Channels, v.SampleRate);
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
            {
                reasons.Add($"{refr.MediaId}: {ex.Message}");
            }
        }

        _misses.Add(new WwiseMiss(eventId, resolved.Name, string.Join("; ", reasons), lastCodec));
        return null;
    }

    /// <summary>
    /// Mixes to mono and resamples to the rate the robot's audio path expects.
    ///
    /// Deliberately the same treatment <see cref="WavAudioSource"/> gives its input — nearest sample, and
    /// an average across channels — so that swapping sources does not change how a clip sounds for any
    /// reason other than the clip itself. Cozmo's own sounds are 44100 Hz here and the robot takes 22050,
    /// so this is a halving in the common case.
    /// </summary>
    private static short[] ToRobotRate(short[] interleaved, int channels, int sourceRate)
    {
        if (channels < 1) channels = 1;
        int frames = interleaved.Length / channels;
        if (frames == 0) return Array.Empty<short>();

        int outFrames = sourceRate == CozmoAudio.SampleRate
            ? frames
            : (int)((long)frames * CozmoAudio.SampleRate / Math.Max(1, sourceRate));
        if (outFrames <= 0) return Array.Empty<short>();

        var outBuf = new short[outFrames];
        for (int i = 0; i < outFrames; i++)
        {
            int src = sourceRate == CozmoAudio.SampleRate
                ? i
                : (int)((long)i * sourceRate / CozmoAudio.SampleRate);
            if (src >= frames) src = frames - 1;
            if (channels == 1) { outBuf[i] = interleaved[src]; continue; }
            int sum = 0;
            for (int c = 0; c < channels; c++) sum += interleaved[src * channels + c];
            outBuf[i] = (short)Math.Clamp(sum / channels, short.MinValue, short.MaxValue);
        }
        return outBuf;
    }

    public void Dispose()
    {
        if (_ownsLibrary) _library.Dispose();
    }
}
