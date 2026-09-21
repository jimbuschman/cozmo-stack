namespace Cozmo.Robot.Animation.Wwise;

/// <summary>What rendering one music plan produced, with the counts that make it checkable.</summary>
public sealed record WwiseRenderedMusic(short[] Pcm, double DurationMs)
{
    /// <summary>Notes whose start fell inside a clip's window.</summary>
    public int NotesInWindow { get; init; }
    /// <summary>Notes that reached at least one sound in the MIDI target.</summary>
    public int NotesPlayed { get; init; }
    /// <summary>Notes that matched no sound (outside every key range) and so made no sound.</summary>
    public int NotesSilent { get; init; }
    /// <summary>Note-off sounds played.</summary>
    public int NoteOffsPlayed { get; init; }
    /// <summary>Audio clips (non-MIDI) placed.</summary>
    public int AudioClips { get; init; }
    /// <summary>Samples that still hit full scale after the output stage. Zero when the stage did its job.</summary>
    public int ClippedSamples { get; init; }
    /// <summary>Anything that could not be done, named.</summary>
    public IReadOnlyList<string> Problems { get; init; } = Array.Empty<string>();
    /// <summary>The loudest output sample.</summary>
    public short Peak { get; init; }
    /// <summary>The loudest sample of the raw sum, in full-scale units, before the output stage; above 32767 means the sum would have clipped.</summary>
    public double PreLimitPeak { get; init; }
    /// <summary>The gain the output stage applied, in dB (0 or negative). See <see cref="WwiseSongRenderer"/>.</summary>
    public double OutputGainDb { get; init; }
    /// <summary>Modulator bindings acted on while rendering: one per voice per binding on its path.</summary>
    public int ModulationsApplied { get; init; }
    /// <summary>The largest level change any modulator made, in dB. Negative; 0 when none had any effect.</summary>
    public double ModulationPeakDb { get; init; }
    /// <summary>The largest pitch change any modulator made, in cents. 0 when none had any effect.</summary>
    public double ModulationPeakCents { get; init; }
    /// <summary>
    /// How many voices each immediate child of the MIDI target contributed. For the singing sampler that
    /// is the note-on layer, the note-off layer and the get-in branch, and the get-in branch's share is
    /// the open question the fidelity manifest records as M9-013: it is reported rather than buried.
    /// </summary>
    public IReadOnlyDictionary<uint, int> VoicesByBranch { get; init; } = new Dictionary<uint, int>();
}

/// <summary>
/// Renders a resolved music plan to PCM: audio clips placed and trimmed as the track says, and MIDI clips
/// played through their MIDI target as a sampler.
///
/// Everything the renderer reads is bank data (<see cref="WwiseHierarchy"/>, <see cref="WwiseMidi"/>).
/// How that data is acted on is Wwise runtime behaviour that this package does not contain, so the
/// rules below are taken from Audiokinetic's public documentation of MIDI playback and are labelled
/// CORROBORATED, not NATIVE; where the documentation leaves a choice open, the choice is named:
///
/// * A note-on plays the MIDI target with the note attached. A blend container or actor-mixer plays every
///   child; a random container plays one child by weight (not repeating the last, per its avoid-repeat
///   count); a sequence container plays its next playlist item; a sound plays.
/// * At every node the note is filtered by that node's MIDI key range and velocity range properties
///   (49..52), where set; a node without them passes everything. A node whose MIDI play-on property (46)
///   is 2 plays at note-off instead of note-on; the setting is inherited down the tree, default note-on.
/// * A sound with a Loop property of 0 sounds for exactly as long as the note is held, looping if the
///   note outlasts the recording; one with no Loop property plays once; a finite count plays that many
///   times. See <see cref="LoopedLength"/>, which sets out why. Volume (dB) and Pitch (cents) properties
///   are summed down the path and applied as gain and a resampling ratio.
/// * Every modulator bound to a node on the path (an RTPC whose source type is 2) is evaluated over the
///   life of the voice and mapped through that binding's curve onto the property it drives: the note-off
///   envelope onto Volume, the vibrato LFO onto Pitch. Neither target node sets the property its modulator
///   drives, so how a bound value would combine with an existing one does not arise on this path. A
///   modulator's own depth may itself be driven by a game parameter, which is how the cube shake reaches
///   the vibrato; with no shake the depth is 0 and the LFO contributes nothing.
/// * MIDI note tracking is off. Not because no root note is set — although none is, anywhere in any bank —
///   but because of the node bit vectors: see <see cref="WwiseNodeParams"/>, which sets out why no bit in
///   any shipped bank can be the one that enables it.
/// * Velocity is not applied: nothing in the shipped target binds an RTPC to it.
/// * A clip plays its source from BeginTrim for its length, starting at PlayAt + BeginTrim on the
///   segment's timeline; a MIDI note that is still held when the clip ends is released there.
///
/// Mixing is additive into a wide accumulator. The raw sum of the shipped recordings exceeds full scale
/// on every song (three layers of near-full-scale voice, notes overlapping their predecessors' tails), and
/// in the product the robot's bus carries a peak limiter and a master compressor (Init.txt effects
/// 3743559935 <c>Robot_Bus_Peak_Limiter</c> and 2313011259 <c>Cozmo_Voice_Master_Compressor</c>) whose
/// parameters this build does not read. The output stage here is a stand-in for them and is
/// <b>LOCAL_POLICY</b>, not a reproduction: when the raw peak exceeds full scale the whole render is scaled
/// down so its peak sits at full scale, and both the raw peak and the gain applied are reported, so a
/// listener knows the level was ours. Nothing is hard-clipped silently.
/// </summary>
public sealed class WwiseSongRenderer
{
    private readonly WwiseSoundLibrary _lib;
    private readonly Func<uint, short[]?> _decode;
    private readonly Random _random;
    private readonly Dictionary<uint, int> _sequenceCursor = new();
    private readonly Dictionary<uint, uint> _lastPick = new();
    /// <summary>
    /// One render at a time. <c>_random</c>, <c>_sequenceCursor</c> and <c>_lastPick</c> carry Wwise's
    /// play-to-play state across a render, and two prewarms can now be in flight at once (a song and a
    /// get-in, or two songs queued back to back), which would tear those dictionaries and draw from
    /// <see cref="Random"/> concurrently.
    /// </summary>
    private readonly object _renderGate = new();

    /// <param name="decode">Media id to mono PCM at <see cref="CozmoAudio.SampleRate"/>, or null when it cannot be decoded.</param>
    public WwiseSongRenderer(WwiseSoundLibrary lib, Func<uint, short[]?> decode, Random? random = null)
    {
        _lib = lib;
        _decode = decode;
        _random = random ?? new Random();
    }

    /// <summary>
    /// The game parameters in force for a render, by parameter id. A modulator's depth can be driven by
    /// one — <c>cozmo_singing_vibrato</c> drives the vibrato LFO's — and a parameter that is not set here
    /// reads 0, which is also what the engine posts when nothing is driving it
    /// (<c>BehaviorSinging::StopInternal</c> 0x005EF2B0).
    ///
    /// A song is rendered whole before it plays, so the value taken here is the value for the whole song;
    /// the engine's Wwise follows the parameter continuously. See the fidelity manifest, M9-016.
    /// </summary>
    public IReadOnlyDictionary<uint, float> Parameters { get; set; } = new Dictionary<uint, float>();

    /// <summary>
    /// Immediate children of the MIDI target to leave out of a render. Empty by default, and the renderer
    /// leaves nothing out on its own: every child of the target receives notes, which is what the container
    /// rules give and what <see cref="WwiseRenderedMusic.VoicesByBranch"/> reports.
    ///
    /// It exists because of one unresolved question. The singing sampler's third child is the get-in
    /// branch, the phrases the three <c>Play__Robot_VO__Singing_Getin_*</c> events play and that the
    /// behaviour's get-in animation raises before the song starts. It carries no MIDI filter of its own,
    /// and four of the eighteen containers under it carry no key range either, so under the rules above a
    /// note can reach it; measured on Aba Daba it adds 41 voices to a 42-note song. Whether Wwise's MIDI
    /// dispatch really routes notes into it is Wwise runtime behaviour, and no Wwise runtime ships in the
    /// package (fidelity manifest M9-013). Only a recording of the stock app singing can settle it, so this
    /// lets the two readings be rendered and listened to side by side rather than argued about.
    /// </summary>
    public IReadOnlySet<uint> ExcludeBranches { get; set; } = new HashSet<uint>();

    private const int Rate = CozmoAudio.SampleRate;
    private static int Samples(double ms) => (int)Math.Round(ms * Rate / 1000.0);

    public WwiseRenderedMusic Render(WwiseMusicPlan plan)
    {
        lock (_renderGate) return RenderLocked(plan);
    }

    private WwiseRenderedMusic RenderLocked(WwiseMusicPlan plan)
    {
        _modulations = 0; _modPeakDb = 0; _modPeakCents = 0; _branchVoices.Clear();
        var problems = new List<string>();
        if (plan.Problem is not null) return new WwiseRenderedMusic(Array.Empty<short>(), 0) { Problems = new[] { plan.Problem } };

        double totalMs = plan.Segments.Sum(s => s.DurationMs);
        var mix = new double[Samples(totalMs)];
        int inWindow = 0, played = 0, silent = 0, offs = 0, audioClips = 0;

        double segOffsetMs = 0;
        foreach (var seg in plan.Segments)
        {
            foreach (var clip in seg.Clips)
            {
                double windowBegin = clip.Clip.BeginTrimMs, windowEnd = windowBegin + clip.Clip.LengthMs;
                double clipStartOnTimeline = segOffsetMs + clip.Clip.PlayAtMs;     // where source time 0 falls

                if (clip.IsMidi)
                {
                    var bytes = _lib.ReadMedia(clip.SourceId, out _);
                    if (bytes is null) { problems.Add($"MIDI source {clip.SourceId} is missing"); continue; }
                    WwiseMidi midi;
                    try { midi = WwiseMidi.Parse(bytes); }
                    catch (InvalidDataException ex) { problems.Add($"MIDI source {clip.SourceId}: {ex.Message}"); continue; }
                    if (seg.MidiTargetNodeId is not { } target)
                    {
                        problems.Add($"track {clip.TrackId} is MIDI but no node above segment {seg.SegmentId} sets a MIDI target");
                        continue;
                    }
                    foreach (var n in midi.NotesAt(seg.TempoBpm))
                    {
                        if (n.StartMs < windowBegin || n.StartMs >= windowEnd) continue;
                        inWindow++;
                        double heldMs = Math.Min(n.StartMs + n.LengthMs, windowEnd) - n.StartMs;
                        double onset = clipStartOnTimeline + n.StartMs;
                        int voices = 0;
                        Trigger(target, n.Key, n.Velocity, onset, heldMs, noteOff: false, 0, 0, 1, NoModulators, mix, ref voices, problems, 0, 0);
                        if (voices > 0) played++; else silent++;
                        int offVoices = 0;
                        Trigger(target, n.Key, n.Velocity, onset + heldMs, 0, noteOff: true, 0, 0, 1, NoModulators, mix, ref offVoices, problems, 0, 0);
                        offs += offVoices;
                    }
                }
                else
                {
                    var pcm = _decode(clip.SourceId);
                    if (pcm is null) { problems.Add($"audio source {clip.SourceId} could not be decoded"); continue; }
                    audioClips++;
                    Place(mix, pcm, clipStartOnTimeline + windowBegin, windowBegin, clip.Clip.LengthMs, 1.0, 1.0);
                }
            }
            segOffsetMs += seg.DurationMs;
        }

        // The output stage: see the class summary. Static, so a whole song keeps its dynamics.
        double rawPeak = 0;
        for (int i = 0; i < mix.Length; i++) rawPeak = Math.Max(rawPeak, Math.Abs(mix[i]));
        double gain = rawPeak > short.MaxValue ? short.MaxValue / rawPeak : 1.0;

        var outPcm = new short[mix.Length];
        int clipped = 0; short peak = 0;
        for (int i = 0; i < mix.Length; i++)
        {
            double v = Math.Round(mix[i] * gain);
            if (v > short.MaxValue) { v = short.MaxValue; clipped++; }
            else if (v < short.MinValue) { v = short.MinValue; clipped++; }
            outPcm[i] = (short)v;
            int magnitude = Math.Min(Math.Abs((int)outPcm[i]), short.MaxValue);
            if (magnitude > peak) peak = (short)magnitude;
        }
        return new WwiseRenderedMusic(outPcm, totalMs)
        {
            NotesInWindow = inWindow, NotesPlayed = played, NotesSilent = silent, NoteOffsPlayed = offs,
            AudioClips = audioClips, ClippedSamples = clipped, Problems = problems, Peak = peak,
            PreLimitPeak = rawPeak, OutputGainDb = gain < 1.0 ? 20 * Math.Log10(gain) : 0,
            ModulationsApplied = _modulations, ModulationPeakDb = _modPeakDb, ModulationPeakCents = _modPeakCents,
            VoicesByBranch = new Dictionary<uint, int>(_branchVoices),
        };
    }

    /// <summary>One modulator and the binding that says what it drives on the node that named it.</summary>
    private readonly record struct Bound(WwiseModulatorNode Modulator, WwiseRtpc Binding, double Depth);

    private static readonly IReadOnlyList<Bound> NoModulators = Array.Empty<Bound>();
    private int _modulations;
    private double _modPeakDb, _modPeakCents;
    private readonly Dictionary<uint, int> _branchVoices = new();

    /// <summary>
    /// The modulators bound on one node, with each one's depth already resolved: an LFO's depth can itself
    /// be driven by a game parameter, and a modulator that names one reads it from <see cref="Parameters"/>
    /// through its own curve. A modulator this reader cannot find is named rather than skipped silently.
    /// </summary>
    private IReadOnlyList<Bound> BindingsOn(WwiseNodeParams p, IReadOnlyList<Bound> inherited, List<string> problems)
    {
        List<Bound>? added = null;
        foreach (var r in p.Rtpcs)
        {
            if (r.SourceType != WwiseRtpc.ModulatorSource) continue;
            if (_lib.Node(r.SourceId) is not WwiseModulatorNode mod)
            {
                problems.Add($"modulator {r.SourceId} is bound but could not be read");
                continue;
            }
            double depth = mod.Value(WwiseModulatorProp.LfoDepth, 0);
            foreach (var own in mod.Params.Rtpcs)
            {
                if (own.SourceType != WwiseRtpc.GameParameterSource || own.ParamId != 0) continue;
                float value = Parameters.TryGetValue(own.SourceId, out var v) ? v : 0f;
                depth = own.Evaluate(value, out bool reduced);
                if (reduced) problems.Add($"modulator {mod.Id}: depth curve uses an interpolation this reader reads as linear");
            }
            (added ??= new List<Bound>(inherited)).Add(new Bound(mod, r, depth));
        }
        return added ?? inherited;
    }

    /// <summary>
    /// Walks the MIDI target for one note event, in either its note-on or its note-off phase, and returns
    /// how long what it placed occupies, so that a container whose play mode is continuous can lay its
    /// items out one after another. That is the same reading of the play-mode bit that
    /// <see cref="WwisePlayback"/> gives an ordinary event play, so one bank field is not read two ways.
    /// </summary>
    private double Trigger(uint nodeId, byte key, byte velocity, double startMs, double heldMs, bool noteOff,
                           double gainDb, double cents, uint playOn, IReadOnlyList<Bound> modulators,
                           double[] mix, ref int voices, List<string> problems, int depth, uint branch)
    {
        if (depth > 16) return 0;
        var node = _lib.Node(nodeId);
        if (node is null) { problems.Add($"MIDI target node {nodeId} is not readable"); return 0; }
        var p = node.Params;

        if (p.Raw(WwiseProp.MidiKeyRangeMin) is { } kmin && key < kmin) return 0;
        if (p.Raw(WwiseProp.MidiKeyRangeMax) is { } kmax && key > kmax) return 0;
        if (p.Raw(WwiseProp.MidiVelocityRangeMin) is { } vmin && velocity < vmin) return 0;
        if (p.Raw(WwiseProp.MidiVelocityRangeMax) is { } vmax && velocity > vmax) return 0;
        if (p.Raw(WwiseProp.MidiPlayOnNoteType) is { } po) playOn = po;
        gainDb += p.Float(WwiseProp.Volume) ?? 0;
        cents += p.Float(WwiseProp.Pitch) ?? 0;
        modulators = BindingsOn(p, modulators, problems);

        switch (node)
        {
            case WwiseBlendNode or WwiseActorMixerNode:
            {
                double longest = 0;
                foreach (var c in node.Children)
                {
                    if (depth == 0 && ExcludeBranches.Contains(c)) continue;
                    longest = Math.Max(longest, Trigger(c, key, velocity, startMs, heldMs, noteOff, gainDb, cents,
                        playOn, modulators, mix, ref voices, problems, depth + 1, depth == 0 ? c : branch));
                }
                return longest;
            }

            case WwiseRandomSequenceNode rs:
            {
                if (rs.Playlist.Count == 0) return 0;
                if ((rs.Flags & WwisePlayback.ContinuousFlag) != 0)
                {
                    double at = startMs, total = 0;
                    foreach (var (child, _) in rs.Playlist)
                    {
                        double n = Trigger(child, key, velocity, at, heldMs, noteOff, gainDb, cents, playOn,
                                           modulators, mix, ref voices, problems, depth + 1, branch);
                        at += n; total += n;
                    }
                    return total;
                }
                uint pick = rs.IsSequence ? NextInSequence(rs) : WeightedPick(rs);
                return Trigger(pick, key, velocity, startMs, heldMs, noteOff, gainDb, cents, playOn, modulators,
                               mix, ref voices, problems, depth + 1, branch);
            }

            case WwiseSoundNode s:
            {
                bool playsAtNoteOff = playOn == 2;
                if (playsAtNoteOff != noteOff) return 0;
                var pcm = _decode(s.MediaId);
                if (pcm is null || pcm.Length == 0) { problems.Add($"sound {s.Id} media {s.MediaId} could not be decoded"); return 0; }
                double gain = Math.Pow(10, gainDb / 20.0);
                double ratio = Math.Pow(2, cents / 1200.0);
                double sampleMs = pcm.Length * 1000.0 / Rate / ratio;
                double lengthMs = noteOff ? sampleMs : LoopedLength(p.Raw(WwiseProp.Loop), sampleMs, heldMs);
                var modulation = Modulation(modulators, heldMs, problems);
                Place(mix, pcm, startMs, 0, lengthMs, gain, ratio, modulation);
                voices++;
                _branchVoices[branch] = _branchVoices.GetValueOrDefault(branch) + 1;
                return lengthMs;
            }

            default:
                problems.Add($"node {nodeId} is a {node.Type}, which the sampler does not dispatch into");
                return 0;
        }
    }

    /// <summary>
    /// Turns the modulators bound on a voice's path into the two things a voice needs while it sounds: a
    /// level offset in dB and a pitch offset in cents, both as functions of the seconds since the voice
    /// started. Returns null when nothing bound to this voice can have any effect, which is the ordinary
    /// case for the vibrato LFO with no cube being shaken, so that the rendering loop keeps its fast path.
    /// </summary>
    private Func<double, (double Db, double Cents)>? Modulation(IReadOnlyList<Bound> modulators, double heldMs, List<string> problems)
    {
        if (modulators.Count == 0) return null;
        var live = new List<Bound>();
        foreach (var b in modulators)
        {
            if (b.Modulator.IsLfo && b.Depth <= 0) continue;          // depth 0: no output, at any instant
            if (b.Binding.ParamId is not ((byte)WwiseProp.Volume or (byte)WwiseProp.Pitch))
            {
                problems.Add($"modulator {b.Modulator.Id} drives property {b.Binding.ParamId}, which the sampler does not apply");
                continue;
            }
            live.Add(b);
        }
        if (live.Count == 0) return null;
        _modulations += live.Count;
        double held = heldMs / 1000.0;
        return t =>
        {
            double db = 0, cents = 0;
            foreach (var b in live)
            {
                double value = b.Modulator.ValueAt(t, held, b.Depth);
                double mapped = b.Binding.Evaluate(value, out _);
                if (b.Binding.ParamId == (byte)WwiseProp.Volume) db += mapped; else cents += mapped;
            }
            if (db < _modPeakDb) _modPeakDb = db;
            if (Math.Abs(cents) > Math.Abs(_modPeakCents)) _modPeakCents = cents;
            return (db, cents);
        };
    }

    /// <summary>
    /// How long a sound sounds for a note held <paramref name="heldMs"/>: once when it does not loop, the
    /// full count when it loops a finite number of times, and, when it loops indefinitely (Loop = 0),
    /// <b>for as long as the note is held</b>.
    ///
    /// That last rule is what the shipped sampler is built around, and getting it wrong is audible from
    /// the first note. Every one of the 42 recordings under the note-on layer is a sustained vowel of
    /// between 4.2 and 7.4 seconds and every one of them carries Loop = 0, while the notes in the shipped
    /// songs are between about 125 and 190 milliseconds long. A rule that let a note play out a whole
    /// iteration of its recording therefore played roughly five and three quarter seconds of vowel for a
    /// note lasting a fifth of a second, and with a note starting every fifth of a second some thirty of
    /// them sounded at once: the sum ran about 13 dB over full scale and the melody was not audible in it.
    ///
    /// What the bank says instead: the recording loops so that a note longer than the recording can be
    /// sustained, the note layer carries the bit that governs a looping sound at note-off, and a separate
    /// note-off layer of half-second recordings at -14 dB plays when the note is released — a release
    /// tail, which is only a release tail if the sustain it follows has stopped.
    ///
    /// A sound that does not loop is left alone: it plays once, or its finite count. None of the note-on
    /// recordings is such a sound, and the note-off and get-in recordings, which are, are played whole.
    ///
    /// Whether Wwise fades the last few milliseconds of a cut voice is a runtime detail that does not ship
    /// in the package; nothing is faded here (fidelity manifest M9-010).
    /// </summary>
    public static double LoopedLength(uint? loopProp, double sampleMs, double heldMs)
    {
        if (sampleMs <= 0) return 0;
        if (loopProp is null || loopProp == 1) return sampleMs;
        if (loopProp > 1) return sampleMs * loopProp.Value;
        return Math.Max(0, heldMs);
    }

    private uint NextInSequence(WwiseRandomSequenceNode rs)
    {
        int i = _sequenceCursor.GetValueOrDefault(rs.Id);
        _sequenceCursor[rs.Id] = (i + 1) % rs.Playlist.Count;
        return rs.Playlist[i % rs.Playlist.Count].ChildId;
    }

    private uint WeightedPick(WwiseRandomSequenceNode rs)
    {
        var candidates = rs.Playlist.ToList();
        if (rs.AvoidRepeatCount > 0 && candidates.Count > 1 && _lastPick.TryGetValue(rs.Id, out var last))
            candidates.RemoveAll(c => c.ChildId == last);
        long total = candidates.Sum(c => (long)Math.Max(0, c.Weight));
        uint chosen = candidates[0].ChildId;
        if (total > 0)
        {
            long r = (long)(_random.NextDouble() * total);
            foreach (var c in candidates)
            {
                r -= Math.Max(0, c.Weight);
                if (r < 0) { chosen = c.ChildId; break; }
            }
        }
        _lastPick[rs.Id] = chosen;
        return chosen;
    }

    /// <summary>
    /// Adds a sound into the mix from <paramref name="startMs"/> on the timeline, reading the source from
    /// <paramref name="sourceOffsetMs"/> for <paramref name="lengthMs"/>, looping the source when the
    /// length exceeds it, at a gain and a resampling ratio (2 for an octave up).
    /// </summary>
    private static void Place(double[] mix, short[] pcm, double startMs, double sourceOffsetMs, double lengthMs,
                              double gain, double ratio, Func<double, (double Db, double Cents)>? modulation = null)
    {
        if (pcm.Length == 0 || lengthMs <= 0) return;
        int start = Samples(startMs);
        int count = Samples(lengthMs);
        double srcPos = sourceOffsetMs * Rate / 1000.0;
        for (int i = 0; i < count; i++)
        {
            int dst = start + i;
            if (dst >= mix.Length) break;
            double step = ratio, g = gain;
            if (modulation is not null)
            {
                var (db, cents) = modulation(i / (double)Rate);
                if (db != 0) g *= Math.Pow(10, db / 20.0);
                if (cents != 0) step *= Math.Pow(2, cents / 1200.0);
            }
            if (dst >= 0)
            {
                int s = (int)srcPos % pcm.Length;
                mix[dst] += pcm[s] * g;
            }
            srcPos += step;
        }
    }
}
