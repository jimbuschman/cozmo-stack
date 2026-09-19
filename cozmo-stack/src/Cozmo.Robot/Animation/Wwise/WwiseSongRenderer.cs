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
/// * A sound with a Loop property of 0 loops while the note is held and then plays out its current
///   iteration ("break on note-off"); one with no Loop property plays once; a finite count plays that
///   many times. Volume (dB) and Pitch (cents) properties are summed down the path and applied as gain
///   and a resampling ratio. Bus volumes, RTPCs and modulators are not applied; the vibrato LFO and the
///   note-off envelope the banks carry are therefore not heard (see WWISE_MUSIC.md, deferred).
/// * MIDI note tracking is off: no node in the shipped target sets a root note, and the per-key
///   containers cover one key each, so the recordings play at their recorded pitch.
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

    /// <param name="decode">Media id to mono PCM at <see cref="CozmoAudio.SampleRate"/>, or null when it cannot be decoded.</param>
    public WwiseSongRenderer(WwiseSoundLibrary lib, Func<uint, short[]?> decode, Random? random = null)
    {
        _lib = lib;
        _decode = decode;
        _random = random ?? new Random();
    }

    private const int Rate = CozmoAudio.SampleRate;
    private static int Samples(double ms) => (int)Math.Round(ms * Rate / 1000.0);

    public WwiseRenderedMusic Render(WwiseMusicPlan plan)
    {
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
                        Trigger(target, n.Key, n.Velocity, onset, heldMs, noteOff: false, 0, 0, 1, mix, ref voices, problems, 0);
                        if (voices > 0) played++; else silent++;
                        int offVoices = 0;
                        Trigger(target, n.Key, n.Velocity, onset + heldMs, 0, noteOff: true, 0, 0, 1, mix, ref offVoices, problems, 0);
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
        };
    }

    /// <summary>Walks the MIDI target for one note event, in either its note-on or its note-off phase.</summary>
    private void Trigger(uint nodeId, byte key, byte velocity, double startMs, double heldMs, bool noteOff,
                         double gainDb, double cents, uint playOn, double[] mix, ref int voices, List<string> problems, int depth)
    {
        if (depth > 16) return;
        var node = _lib.Node(nodeId);
        if (node is null) { problems.Add($"MIDI target node {nodeId} is not readable"); return; }
        var p = node.Params;

        if (p.Raw(WwiseProp.MidiKeyRangeMin) is { } kmin && key < kmin) return;
        if (p.Raw(WwiseProp.MidiKeyRangeMax) is { } kmax && key > kmax) return;
        if (p.Raw(WwiseProp.MidiVelocityRangeMin) is { } vmin && velocity < vmin) return;
        if (p.Raw(WwiseProp.MidiVelocityRangeMax) is { } vmax && velocity > vmax) return;
        if (p.Raw(WwiseProp.MidiPlayOnNoteType) is { } po) playOn = po;
        gainDb += p.Float(WwiseProp.Volume) ?? 0;
        cents += p.Float(WwiseProp.Pitch) ?? 0;

        switch (node)
        {
            case WwiseBlendNode or WwiseActorMixerNode:
                foreach (var c in node.Children)
                    Trigger(c, key, velocity, startMs, heldMs, noteOff, gainDb, cents, playOn, mix, ref voices, problems, depth + 1);
                break;

            case WwiseRandomSequenceNode rs:
                if (rs.Playlist.Count == 0) return;
                uint pick = rs.IsSequence ? NextInSequence(rs) : WeightedPick(rs);
                Trigger(pick, key, velocity, startMs, heldMs, noteOff, gainDb, cents, playOn, mix, ref voices, problems, depth + 1);
                break;

            case WwiseSoundNode s:
                bool playsAtNoteOff = playOn == 2;
                if (playsAtNoteOff != noteOff) return;
                var pcm = _decode(s.MediaId);
                if (pcm is null || pcm.Length == 0) { problems.Add($"sound {s.Id} media {s.MediaId} could not be decoded"); return; }
                double gain = Math.Pow(10, gainDb / 20.0);
                double ratio = Math.Pow(2, cents / 1200.0);
                double sampleMs = pcm.Length * 1000.0 / Rate / ratio;
                double lengthMs = noteOff ? sampleMs : LoopedLength(p.Raw(WwiseProp.Loop), sampleMs, heldMs);
                Place(mix, pcm, startMs, 0, lengthMs, gain, ratio);
                voices++;
                break;

            default:
                problems.Add($"node {nodeId} is a {node.Type}, which the sampler does not dispatch into");
                break;
        }
    }

    /// <summary>
    /// How long a sound sounds for a note held <paramref name="heldMs"/>: once when it does not loop, the
    /// full count when it loops a finite number of times, and, when it loops indefinitely (Loop = 0),
    /// until the note is released and the iteration then playing has finished.
    /// </summary>
    public static double LoopedLength(uint? loopProp, double sampleMs, double heldMs)
    {
        if (sampleMs <= 0) return 0;
        if (loopProp is null || loopProp == 1) return sampleMs;
        if (loopProp > 1) return sampleMs * loopProp.Value;
        int iterations = Math.Max(1, (int)Math.Ceiling(heldMs / sampleMs));
        return iterations * sampleMs;
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
    private static void Place(double[] mix, short[] pcm, double startMs, double sourceOffsetMs, double lengthMs, double gain, double ratio)
    {
        if (pcm.Length == 0 || lengthMs <= 0) return;
        int start = Samples(startMs);
        int count = Samples(lengthMs);
        double srcPos = sourceOffsetMs * Rate / 1000.0;
        for (int i = 0; i < count; i++)
        {
            int dst = start + i;
            if (dst >= mix.Length) break;
            if (dst >= 0)
            {
                int s = (int)srcPos % pcm.Length;
                mix[dst] += pcm[s] * gain;
            }
            srcPos += ratio;
        }
    }
}
