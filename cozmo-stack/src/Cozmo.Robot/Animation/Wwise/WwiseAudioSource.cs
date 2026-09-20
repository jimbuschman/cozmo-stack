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
public sealed class WwiseAudioSource : IAnimationAudioSource, IAudioSwitchStates, IDisposable
{
    private readonly WwiseSoundLibrary _library;
    private readonly bool _ownsLibrary;
    private readonly WwiseCodebookLibrary? _codebooks;
    private readonly Dictionary<uint, short[]?> _cache = new();
    private readonly Dictionary<uint, short[]?> _mediaCache = new();
    private readonly Dictionary<(uint Event, uint Node), short[]?> _musicCache = new();
    private readonly Dictionary<uint, uint> _switches = new();
    private readonly List<WwiseMiss> _misses = new();
    private readonly object _gate = new();
    private readonly WwiseSongRenderer _renderer;

    /// <summary>Wraps an already-loaded library. Without codebooks, Vorbis events cannot be produced.</summary>
    /// <param name="random">Decides which of a note's recordings plays; pass a seeded instance for a reproducible render.</param>
    public WwiseAudioSource(WwiseSoundLibrary library, bool ownsLibrary = false,
                            WwiseCodebookLibrary? codebooks = null, Random? random = null)
    {
        _library = library;
        _ownsLibrary = ownsLibrary;
        _codebooks = codebooks ?? TryLoadCodebooks();
        _renderer = new WwiseSongRenderer(library, DecodeMedia, random);
    }

    /// <summary>
    /// Sets a switch group's value, as the engine does before a singing animation starts. A song rendered
    /// under one switch value is cached by the node it selected, so changing the switch and playing the
    /// event again renders the newly selected song.
    /// </summary>
    public void SetSwitch(uint groupId, uint switchId)
    {
        lock (_gate) _switches[groupId] = switchId;
    }

    public IReadOnlyDictionary<uint, uint> Switches { get { lock (_gate) return new Dictionary<uint, uint>(_switches); } }

    /// <summary>The last music render's report, for tools and acceptance records. Null until a music event was produced.</summary>
    public WwiseRenderedMusic? LastMusicRender { get; private set; }

    /// <summary>
    /// Renders a music event under the current switches (or the ones given) and reports what was done,
    /// without caching: the diagnostic form of what <see cref="GetPcm"/> does for a music event.
    /// </summary>
    public WwiseRenderedMusic RenderMusic(uint eventId, IReadOnlyDictionary<uint, uint>? switches = null)
    {
        var plan = _library.ResolveMusic(eventId, switches ?? Switches);
        return _renderer.Render(plan);
    }

    /// <summary>
    /// A Wwise Stop action (<c>WwiseBank.IsStopAction</c>): the event starts nothing and ends its target's
    /// voices. <c>Stop__Robot_VO__Cozmo_Singing_Stop</c> is one; the tempo animations raise it at their end.
    /// </summary>
    public bool IsStopEvent(long eventId)
    {
        if (eventId is < 0 or > uint.MaxValue) return false;
        var r = _library.Resolve((uint)eventId);
        return r.Actions.Count > 0 && r.Actions.All(a => !WwiseBank.IsPlayAction(a.ActionType)) && r.Actions.Any(a => WwiseBank.IsStopAction(a.ActionType));
    }

    /// <summary>
    /// Whether a Stop event's target is the playing event's Play target or one of its ancestors in the
    /// hierarchy (Wwise stops the target node's voices, which includes everything below it). True when either
    /// side cannot be resolved, so an unmatched Stop still ends the one voice this stack streams.
    /// </summary>
    public bool StopAffects(long stopEventId, long playingEventId)
    {
        if (stopEventId is < 0 or > uint.MaxValue || playingEventId is < 0 or > uint.MaxValue) return true;
        var stopTargets = _library.Resolve((uint)stopEventId).Actions.Where(a => WwiseBank.IsStopAction(a.ActionType)).Select(a => a.Target).ToHashSet();
        var playTargets = _library.Resolve((uint)playingEventId).Actions.Where(a => WwiseBank.IsPlayAction(a.ActionType)).Select(a => a.Target).ToList();
        if (stopTargets.Count == 0 || playTargets.Count == 0) return true;
        foreach (var start in playTargets)
        {
            uint id = start;
            for (int depth = 0; depth < 64 && id != 0; depth++)
            {
                if (stopTargets.Contains(id)) return true;
                if (_library.Node(id) is not { } n) break;
                id = n.Params.ParentId;
            }
        }
        return false;
    }

    /// <summary>
    /// Renders a music event under the given switches on a worker and caches the result, so the first
    /// <see cref="GetPcm"/> from the scheduler's thread finds it ready. A song is rendered whole (a 462 s
    /// sequence takes seconds), which on the scheduler thread would stall the animation timeline; the
    /// singing behaviour calls this when it posts the switch, before its get-in animation. <see cref="GetPcm"/>
    /// waits for an in-flight prewarm of the same song rather than rendering it a second time.
    /// </summary>
    public Task Prewarm(uint eventId, IReadOnlyDictionary<uint, uint>? switches = null)
    {
        var sw = switches is null ? Switches : new Dictionary<uint, uint>(switches);
        var plan = _library.ResolveMusic(eventId, sw);
        uint node = plan.SelectedNodeId ?? plan.TargetId;
        var key = (eventId, node);
        lock (_gate)
        {
            if (_musicCache.ContainsKey(key)) return Task.CompletedTask;
            if (_prewarms.TryGetValue(key, out var running)) return running;
            var task = Task.Run(() =>
            {
                var rendered = RenderTimed(plan);
                lock (_gate)
                {
                    if (!_musicCache.ContainsKey(key)) StoreMusic(eventId, plan, rendered);
                    _prewarms.Remove(key);
                }
            });
            _prewarms[key] = task;
            return task;
        }
    }

    private readonly Dictionary<(uint Event, uint Node), Task> _prewarms = new();

    /// <summary>Wall time of the last music render, for the tool and the scheduler-safety test.</summary>
    public TimeSpan LastMusicRenderTime { get; private set; }

    private WwiseRenderedMusic RenderTimed(WwiseMusicPlan plan)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = _renderer.Render(plan);
        LastMusicRenderTime = sw.Elapsed;
        return r;
    }

    private void StoreMusic(uint eventId, WwiseMusicPlan plan, WwiseRenderedMusic rendered)
    {
        uint node = plan.SelectedNodeId ?? plan.TargetId;
        LastMusicRender = rendered;
        short[]? pcm = rendered.Pcm.Length > 0 && (rendered.NotesPlayed > 0 || rendered.AudioClips > 0) ? rendered.Pcm : null;
        if (pcm is null)
            _misses.Add(new WwiseMiss(eventId, plan.EventName,
                rendered.Problems.Count > 0 ? string.Join("; ", rendered.Problems) : "the music plan produced no sound", WwiseCodec.Unknown));
        _musicCache[(eventId, node)] = pcm;
    }

    /// <summary>Whether an event's Play target is in the music hierarchy, so it is produced by the renderer.</summary>
    public bool IsMusicEvent(uint eventId)
    {
        var r = _library.Resolve(eventId);
        foreach (var a in r.Actions)
            if (a.ActionType == WwiseBank.PlayAction && _library.Node(a.Target) is WwiseMusicSwitchNode or WwiseMusicPlaylistNode or WwiseMusicSegmentNode)
                return true;
        return false;
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
        if (IsMusicEvent(id)) pcm = ProduceMusic(id);
        else lock (_gate)
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

    /// <summary>
    /// A music event: resolved under the current switches, rendered through its MIDI target or its audio
    /// clips, and cached by the node the switch selected. Recorded as a miss, with the renderer's own
    /// problems, when nothing comes out. An in-flight <see cref="Prewarm"/> of the same song is awaited
    /// instead of rendering twice.
    ///
    /// LOCAL_POLICY: the cache holds the final PCM, so the random choices the renderer made (which of a
    /// note's three recordings plays) are frozen for the life of this source: the same song sounds the same
    /// every time it is sung in a session. Wwise would draw again on every play. Stated in WWISE_MUSIC.md §3
    /// and pinned by <c>TheMusicCacheFreezesTheRenderersRandomChoices</c>; a fresh source (or a new seed)
    /// draws afresh.
    ///
    /// This runs on the animation scheduler's tick, when an audio keyframe fires, so it must never block and
    /// never render: a whole song is seconds of work and the timeline would stall. A song that is not ready
    /// starts its render on a worker and this play is silent; the singing behaviour avoids that by awaiting
    /// <see cref="Prewarm"/> before it enters the tempo animation.
    /// </summary>
    private short[]? ProduceMusic(uint eventId)
    {
        WwiseMusicPlan plan;
        bool prewarming;
        uint node;
        lock (_gate)
        {
            plan = _library.ResolveMusic(eventId, _switches);
            node = plan.SelectedNodeId ?? plan.TargetId;
            if (_musicCache.TryGetValue((eventId, node), out var cached)) return cached;
            prewarming = _prewarms.ContainsKey((eventId, node));
        }
        UnpreparedMusicEvents++;
        if (!prewarming) Prewarm(eventId, _switches);          // starts on a worker; this play stays silent
        return null;
    }

    /// <summary>
    /// How many times a music event was asked for on the scheduler thread before its render was ready. Every
    /// one of those plays is silent; the singing behaviour is expected to keep this at zero.
    /// </summary>
    public int UnpreparedMusicEvents { get; private set; }

    /// <summary>One media file decoded to mono PCM at the robot's rate, cached; null when it cannot be decoded.</summary>
    private short[]? DecodeMedia(uint mediaId)
    {
        lock (_gate)
        {
            if (_mediaCache.TryGetValue(mediaId, out var cached)) return cached;
            var refr = _library.Describe(mediaId, "");
            short[]? pcm = null;
            if (refr.Media is { } m && m.IsDecodable && !(m.Codec == WwiseCodec.Vorbis && _codebooks is null))
            {
                var bytes = _library.ReadMedia(mediaId, out _);
                if (bytes is not null)
                {
                    try
                    {
                        var parsed = WwiseMedia.Parse(bytes);
                        if (parsed.Codec == WwiseCodec.Adpcm)
                            pcm = ToRobotRate(WwiseAdpcm.Decode(parsed), parsed.Channels, parsed.SampleRate);
                        else
                        {
                            var v = WwiseVorbis.Decode(parsed, _codebooks!);
                            pcm = ToRobotRate(v.Samples, v.Channels, v.SampleRate);
                        }
                    }
                    catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { pcm = null; }
                }
            }
            _mediaCache[mediaId] = pcm;
            return pcm;
        }
    }

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
    /// reason other than the clip itself. Cozmo's own sounds are 44100 Hz here and the robot takes 22320
    /// (<see cref="CozmoAudio.SampleRate"/>, the engine's <c>AnimConstants::AUDIO_SAMPLE_RATE</c>), so this
    /// is very nearly a halving in the common case.
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
