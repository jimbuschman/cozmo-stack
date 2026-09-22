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
    private readonly Dictionary<uint, short[]?> _mediaCache = new();
    private readonly Dictionary<uint, uint> _switches = new();
    private readonly Dictionary<uint, float> _parameters = new();
    /// <summary>The draw, the sequence positions and the last pick a container play carries between plays.</summary>
    private readonly Random _eventRandom;
    private readonly Dictionary<uint, int> _eventCursor = new();
    private readonly Dictionary<uint, uint> _eventLastPick = new();
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
        _renderer = new WwiseSongRenderer(library, DecodeMedia, random)
        {
            // What the robot hears is the output of Robot_Bus_1, which the engine's own registration
            // table binds to the robot game object a singing behaviour posts on. See WwiseBusChain.
            BusChain = WwiseBusChain.For(library, WwiseBusChain.RobotBus1, CozmoAudio.SampleRate),
        };
        _eventRandom = random ?? new Random();
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

    /// <summary>
    /// Sets a game parameter, as <c>RobotAudioClient::PostRobotParameter</c> does. The renderer reads
    /// these when a modulator's depth is bound to one: <c>cozmo_singing_vibrato</c> drives the singing
    /// vibrato's depth from nothing to full. A song already rendered is not re-rendered for a new value —
    /// see <see cref="WwiseSongRenderer.Parameters"/>.
    /// </summary>
    public void SetParameter(uint parameterId, float value)
    {
        lock (_gate)
        {
            _parameters[parameterId] = value;
            var snapshot = new Dictionary<uint, float>(_parameters);
            _renderer.Parameters = snapshot;
            // and to every song already playing, which is the point: the vibrato reaches a song that has
            // already started rather than only one that has not.
            foreach (var stream in _streams.Values) stream.SetParameters(snapshot);
        }
    }

    public IReadOnlyDictionary<uint, float> Parameters { get { lock (_gate) return new Dictionary<uint, float>(_parameters); } }

    /// <summary>
    /// Children of the singing sampler's MIDI target to leave out of a render, for listening to the two
    /// readings of the get-in branch side by side. Empty by default; see
    /// <see cref="WwiseSongRenderer.ExcludeBranches"/>.
    /// </summary>
    public IReadOnlySet<uint> ExcludeBranches
    {
        get => _renderer.ExcludeBranches;
        set
        {
            lock (_gate)
            {
                _renderer.ExcludeBranches = value;
                foreach (var stream in _streams.Values) stream.Dispose();
                _streams.Clear();                       // a song already prepared was prepared the other way
            }
        }
    }

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
    /// Prepares a music event under the given switches on a worker, so the first <see cref="GetPcm"/> from
    /// the scheduler's thread finds samples waiting. Resolving the plan, parsing the MIDI and decoding the
    /// recordings is the slow part and none of it may happen on the scheduler's thread; the singing
    /// behaviour calls this when it posts the switch, before its get-in animation.
    ///
    /// What it does <b>not</b> do is render the whole song. The song is rendered as it plays
    /// (<see cref="WwiseMusicStream"/>), a little ahead of the clock, because
    /// <c>Cozmo_Singing_Vibrato</c> is posted every tick while Cozmo sings and a song rendered whole and
    /// cached could never hear it.
    /// </summary>
    public Task Prewarm(uint eventId, IReadOnlyDictionary<uint, uint>? switches = null)
    {
        var sw = switches is null ? Switches : new Dictionary<uint, uint>(switches);
        // Keyed by the node the switch selected, not by the event: the same tempo event plays a different
        // song for every Cozmo_Sings switch value, and preparing one must not answer for another.
        var key = (eventId, _library.ResolveMusic(eventId, sw) is { } p ? p.SelectedNodeId ?? p.TargetId : 0u);
        lock (_gate)
        {
            // A stream that has already been played is not reused: Wwise draws a note's recording again
            // on every play, so preparing the same song a second time prepares it again.
            if (_streams.TryGetValue(key, out var existing))
            {
                if (!existing.HasBegun) return Task.CompletedTask;
                existing.Dispose();
                _streams.Remove(key);
            }
            if (_prewarms.TryGetValue(key, out var running)) return running;
            var task = Task.Run(() =>
            {
                var stream = BuildStream(eventId, sw, 1f);
                lock (_gate)
                {
                    _prewarms.Remove(key);
                    if (stream is null) return;
                    if (_streams.TryGetValue(key, out var old)) old.Dispose();
                    _streams[key] = stream;
                }
                stream.Start();
            });
            _prewarms[key] = task;
            return task;
        }
    }

    private readonly Dictionary<(uint Event, uint Node), Task> _prewarms = new();
    private readonly Dictionary<(uint Event, uint Node), WwiseMusicStream> _streams = new();

    /// <summary>Wall time of the last music preparation, for the tool and the scheduler-safety test.</summary>
    public TimeSpan LastMusicRenderTime { get; private set; }

    /// <summary>
    /// Resolves and prepares one song. Everything expensive happens here: the plan, the MIDI, the draws
    /// and the decoding. Nothing is mixed, so this is bounded by the recordings rather than the length of
    /// the song, which is what makes a 462-second sequence affordable.
    /// </summary>
    private WwiseMusicStream? BuildStream(uint eventId, IReadOnlyDictionary<uint, uint> switches, float volume)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var plan = _library.ResolveMusic(eventId, switches);
        var voices = _renderer.BuildVoices(plan);
        LastMusicRenderTime = timer.Elapsed;
        if (voices.TotalMs <= 0 || (voices.NotesPlayed == 0 && voices.AudioClips == 0))
        {
            lock (_gate)
                _misses.Add(new WwiseMiss(eventId, plan.EventName,
                    voices.Problems.Count > 0 ? string.Join("; ", voices.Problems) : "the music plan produced no sound",
                    WwiseCodec.Unknown));
            return null;
        }
        IReadOnlyDictionary<uint, float> parameters;
        lock (_gate) parameters = new Dictionary<uint, float>(_parameters);
        // Its own chain: the filters and the limiter carry state from block to block, so two songs must
        // not share one. Building it is reading a handful of bank objects.
        var chain = WwiseBusChain.For(_library, WwiseBusChain.RobotBus1, CozmoAudio.SampleRate);
        return new WwiseMusicStream(voices, chain, parameters, _renderer.ExcludeBranches, volume);
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
    /// What the event plays is worked out by walking the containers under its Play target with their own
    /// semantics (<see cref="WwisePlayback"/>): a random container draws, a sequence container plays its
    /// items one after another, a switch container follows the switch. So a play is drawn afresh each
    /// time, as Wwise draws it, and an event whose phrase is several recordings long plays all of them.
    /// Only the decoded media are cached, not the mix, so the draw is not frozen.
    /// </summary>
    public short[]? GetPcm(long eventId, float volume)
    {
        if (eventId is < 0 or > uint.MaxValue) return null;
        uint id = (uint)eventId;
        float gain = GainForKeyframeVolume(volume);

        // A music event streams: its buffer is filled in as the song plays, so the keyframe volume is
        // applied inside the stream rather than by copying and scaling what has been rendered so far.
        if (IsMusicEvent(id)) return ProduceMusic(id, gain);

        short[]? pcm;
        lock (_gate) pcm = Produce(id);
        if (pcm is null) return null;
        if (Math.Abs(gain - 1f) < 0.001f) return pcm;

        var scaled = new short[pcm.Length];
        for (int i = 0; i < pcm.Length; i++)
            scaled[i] = (short)Math.Clamp((int)MathF.Round(pcm[i] * gain), short.MinValue, short.MaxValue);
        return scaled;
    }

    /// <summary>
    /// The <c>Event_Volume</c> game parameter, FNV-1 hash 0xD2687048. An audio keyframe's volume is not a
    /// gain the engine multiplies by: <c>RobotAudioAnimationOnRobot::BeginBufferingAudioOnRobotMode</c>
    /// posts the event, then calls <c>RobotAudioClient::SetCozmoEventParameter(playingId, 0xD2687048,
    /// volume)</c> at 0x00598298 and processes the queue. What that parameter does is in the banks.
    /// </summary>
    public const uint EventVolumeParameter = 0xD2687048;

    private WwiseRtpc? _eventVolumeRtpc;
    private bool _eventVolumeSearched;

    /// <summary>
    /// The amplitude an audio keyframe's volume works out to, through the binding the banks give
    /// <see cref="EventVolumeParameter"/>.
    ///
    /// In the shipped banks that parameter drives the Volume property of three actor mixers -
    /// 62050212 and 682998829 in Cozmo.bnk and 121198006 in Dev_Debug.bnk - additively, over a curve from
    /// (0, -1) to (1, 0). Volume is in decibels, so the whole range of the keyframe volume is one decibel
    /// of trim: 1.0 leaves the event alone and 0.0 takes one decibel off it. It is not the linear gain this
    /// stack used to apply, under which a volume of 0 was silence.
    ///
    /// The curve is read from the loaded banks rather than hard-coded, so a bank set that binds it
    /// differently is followed. With no binding at all the volume does nothing, which is what Wwise would
    /// do with a parameter nothing listens to.
    /// </summary>
    public float GainForKeyframeVolume(float volume)
    {
        var rtpc = EventVolumeBinding();
        if (rtpc is null) return 1f;
        double db = rtpc.Evaluate(volume, out _);
        return (float)Math.Pow(10.0, db / 20.0);
    }

    /// <summary>The first Event_Volume binding on a Volume property in the loaded banks, if there is one.</summary>
    private WwiseRtpc? EventVolumeBinding()
    {
        lock (_gate)
        {
            if (_eventVolumeSearched) return _eventVolumeRtpc;
            _eventVolumeSearched = true;
            foreach (var bank in _library.Banks)
                foreach (var objectId in bank.Objects.Keys)
                {
                    if (_library.Node(objectId) is not { } node) continue;
                    foreach (var r in node.Params.Rtpcs)
                    {
                        if (r.SourceType != WwiseRtpc.GameParameterSource) continue;
                        if (r.SourceId != EventVolumeParameter) continue;
                        if (r.ParamId != (byte)WwiseProp.Volume) continue;
                        return _eventVolumeRtpc = r;
                    }
                }
            return _eventVolumeRtpc = null;
        }
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
    private short[]? ProduceMusic(uint eventId, float volume)
    {
        WwiseMusicStream? stream;
        bool prewarming;
        var plan = _library.ResolveMusic(eventId, Switches);
        var key = (eventId, plan.SelectedNodeId ?? plan.TargetId);
        lock (_gate)
        {
            _streams.TryGetValue(key, out stream);
            prewarming = _prewarms.ContainsKey(key);
        }
        if (stream is not null)
        {
            stream.Volume = volume;
            stream.BeginPlayback();
            LastMusicRender = stream.Snapshot();
            return stream.Pcm;
        }
        UnpreparedMusicEvents++;
        if (!prewarming) Prewarm(eventId, _switches);          // starts on a worker; this play stays silent
        return null;
    }

    /// <summary>
    /// How much of a streaming song is rendered. The buffer handed to the scheduler is the whole song's
    /// worth of samples and only the front of it is real, so this answers with what has been committed;
    /// the rest is still waiting on the parameters it will be rendered under. The call doubles as
    /// playback's report of how far it has got, which is what bounds how far ahead the render runs.
    /// </summary>
    public int ReadySamples(short[] pcm, int consumed)
    {
        WwiseMusicStream? stream = null;
        lock (_gate)
            foreach (var s in _streams.Values)
                if (ReferenceEquals(s.Pcm, pcm)) { stream = s; break; }
        if (stream is null) return pcm.Length;          // a decoded sound, ready in full
        stream.NoteConsumedTo(consumed);
        int ready = stream.Ready;
        if (consumed >= ready && consumed < pcm.Length) stream.NoteUnderrun();
        return ready;
    }

    /// <summary>The stream for an event under the current switches, if one has been prepared. Tests and tools.</summary>
    public WwiseMusicStream? StreamFor(uint eventId)
    {
        var plan = _library.ResolveMusic(eventId, Switches);
        lock (_gate) return _streams.GetValueOrDefault((eventId, plan.SelectedNodeId ?? plan.TargetId));
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

    /// <summary>
    /// Resolves the event to a plan and renders it: each recording decoded, its summed level and pitch
    /// applied, laid out where the plan puts it. A recording that cannot be decoded is named in
    /// <see cref="Misses"/> and left out; the rest of the phrase still plays.
    /// </summary>
    private short[]? Produce(uint eventId)
    {
        var plan = WwisePlayback.Resolve(_library, eventId, _switches, _eventRandom, _eventCursor, _eventLastPick);
        var reasons = new List<string>(plan.Problems);
        if (plan.Root is null)
        {
            _misses.Add(new WwiseMiss(eventId, plan.Name,
                reasons.Count > 0 ? string.Join("; ", reasons) : "the event resolves to no media", WwiseCodec.Unknown));
            return null;
        }

        // Decode first, so the layout can use the lengths the decoder actually produced.
        var decoded = new Dictionary<uint, short[]>();
        var lastCodec = WwiseCodec.Unknown;
        foreach (var s in plan.Sounds)
        {
            if (decoded.ContainsKey(s.MediaId)) continue;
            var pcm = DecodeMedia(s.MediaId);
            if (pcm is null || pcm.Length == 0)
            {
                var refr = _library.Describe(s.MediaId, "");
                if (refr.Media is { } m) lastCodec = m.Codec;
                reasons.Add($"{s.MediaId}: " + (refr.Problem
                    ?? refr.Media?.UndecodableReason
                    ?? (refr.Media?.Codec == WwiseCodec.Vorbis && _codebooks is null
                        ? $"Vorbis needs {CodebookFileName}, which was not found"
                        : "could not be decoded")));
                continue;
            }
            decoded[s.MediaId] = pcm;
        }
        if (decoded.Count == 0)
        {
            _misses.Add(new WwiseMiss(eventId, plan.Name, string.Join("; ", reasons), lastCodec));
            return null;
        }

        int rate = CozmoAudio.SampleRate;
        var voices = new List<(int Start, short[] Pcm, double Gain, double Ratio)>();
        int total = 0;

        int Lay(WwisePlayNode node, int startSample)
        {
            switch (node)
            {
                case WwisePlaySound s:
                {
                    if (!decoded.TryGetValue(s.MediaId, out var pcm)) return 0;
                    double ratio = Math.Pow(2, s.Cents / 1200.0);
                    double gain = Math.Pow(10, s.GainDb / 20.0);
                    int length = (int)Math.Round(pcm.Length / ratio);
                    voices.Add((startSample, pcm, gain, ratio));
                    total = Math.Max(total, startSample + length);
                    return length;
                }
                case WwisePlaySequence q:
                {
                    int at = startSample, length = 0;
                    foreach (var part in q.Parts) { int n = Lay(part, at); at += n; length += n; }
                    return length;
                }
                case WwisePlayTogether t:
                {
                    int longest = 0;
                    foreach (var part in t.Parts) longest = Math.Max(longest, Lay(part, startSample));
                    return longest;
                }
                default: return 0;
            }
        }

        Lay(plan.Root, 0);
        if (total <= 0)
        {
            _misses.Add(new WwiseMiss(eventId, plan.Name, string.Join("; ", reasons), lastCodec));
            return null;
        }
        if (reasons.Count > 0) _misses.Add(new WwiseMiss(eventId, plan.Name, string.Join("; ", reasons), lastCodec));

        // One voice at unity with nothing to mix into is the common case; hand back its samples unchanged.
        if (voices.Count == 1 && voices[0].Start == 0 && Math.Abs(voices[0].Gain - 1) < 1e-9
            && Math.Abs(voices[0].Ratio - 1) < 1e-9)
            return voices[0].Pcm;

        var mix = new double[total];
        foreach (var (start, pcm, gain, ratio) in voices)
        {
            double src = 0;
            for (int i = start; i < mix.Length; i++)
            {
                int index = (int)src;
                if (index >= pcm.Length) break;
                mix[i] += pcm[index] * gain;
                src += ratio;
            }
        }
        var outPcm = new short[total];
        for (int i = 0; i < total; i++)
            outPcm[i] = (short)Math.Clamp(Math.Round(mix[i]), short.MinValue, short.MaxValue);
        return outPcm;
    }

    /// <summary>
    /// Mixes to mono and resamples to the rate the robot's audio path expects.
    ///
    /// Cozmo's own recordings are 44100 or 48000 Hz and the robot takes 22320
    /// (<see cref="CozmoAudio.SampleRate"/>, the engine's <c>AnimConstants::AUDIO_SAMPLE_RATE</c>), so
    /// every shipped sound is decimated by about two to one on the way in. Taking the nearest sample, as
    /// this did, folds everything above 11160 Hz in the source back down into the band, which on a sung
    /// vowel is audible as a hard, gritty edge that is not in the recording. The sung notes are the worst
    /// case: 48000 Hz, forty-two of them in a song, and they are the content the whole M9 path exists for.
    ///
    /// So the kernel is a windowed sinc (Lanczos, three lobes) whose cutoff is the lower of the two
    /// Nyquists, which band-limits and interpolates in the one pass. That is a standard resampler, not
    /// Audiokinetic's: Wwise's own is in its runtime, which does not ship in this package, so what is
    /// fixed here is a defect of this stack rather than a reproduction of theirs (fidelity manifest
    /// M6-004). <see cref="WavAudioSource"/> is left alone; it carries the harness's own test signals and
    /// captured WAVs, not shipped content.
    /// </summary>
    public static short[] ToRobotRate(short[] interleaved, int channels, int sourceRate)
    {
        if (channels < 1) channels = 1;
        int frames = interleaved.Length / channels;
        if (frames == 0) return Array.Empty<short>();

        // to mono first, so the kernel runs once
        var mono = new double[frames];
        if (channels == 1)
            for (int i = 0; i < frames; i++) mono[i] = interleaved[i];
        else
            for (int i = 0; i < frames; i++)
            {
                int sum = 0;
                for (int c = 0; c < channels; c++) sum += interleaved[i * channels + c];
                mono[i] = sum / (double)channels;
            }

        if (sourceRate == CozmoAudio.SampleRate || sourceRate <= 0)
        {
            var same = new short[frames];
            for (int i = 0; i < frames; i++) same[i] = Clamp(mono[i]);
            return same;
        }

        int outFrames = (int)((long)frames * CozmoAudio.SampleRate / sourceRate);
        if (outFrames <= 0) return Array.Empty<short>();

        double ratio = CozmoAudio.SampleRate / (double)sourceRate;   // below 1 when decimating
        double cutoff = Math.Min(1.0, ratio);                        // of the source Nyquist
        const int Lobes = 3;
        double halfWidth = Lobes / cutoff;

        var outBuf = new short[outFrames];
        for (int i = 0; i < outFrames; i++)
        {
            double centre = i / ratio;
            int lo = (int)Math.Ceiling(centre - halfWidth), hi = (int)Math.Floor(centre + halfWidth);
            double sum = 0, norm = 0;
            for (int k = lo; k <= hi; k++)
            {
                double w = Lanczos((k - centre) * cutoff, Lobes);
                if (w == 0) continue;
                norm += w;
                if (k >= 0 && k < frames) sum += mono[k] * w;        // outside the clip is silence
            }
            outBuf[i] = Clamp(norm > 0 ? sum / norm : 0);
        }
        return outBuf;
    }

    private static short Clamp(double v) =>
        (short)Math.Clamp(Math.Round(v), short.MinValue, short.MaxValue);

    /// <summary>sinc(t) windowed by sinc(t / lobes), zero outside the window.</summary>
    private static double Lanczos(double t, int lobes)
    {
        t = Math.Abs(t);
        if (t < 1e-9) return 1.0;
        if (t >= lobes) return 0.0;
        double pt = Math.PI * t;
        return lobes * Math.Sin(pt) * Math.Sin(pt / lobes) / (pt * pt);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var stream in _streams.Values) stream.Dispose();
            _streams.Clear();
        }
        if (_ownsLibrary) _library.Dispose();
    }
}
