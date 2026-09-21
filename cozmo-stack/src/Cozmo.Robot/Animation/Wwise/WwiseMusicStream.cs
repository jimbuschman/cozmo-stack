namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// One modulator bound to a property of a node on a voice's path, with the binding that says how far it
/// can move that property and — for an LFO — the game-parameter binding that sets its depth.
///
/// The depth is deliberately <b>not</b> resolved when the voice is built. <c>cozmo_singing_vibrato</c> is
/// posted every tick while Cozmo sings (<c>BehaviorSinging::UpdateInternal</c> 0x005EF0C8), so a depth
/// baked in at build time could never follow it. <see cref="DepthAt"/> is called again for every block of
/// every voice, which is what lets shaking a cube change a song that is already playing.
/// </summary>
public sealed record WwiseBoundModulator(WwiseModulatorNode Modulator, WwiseRtpc Binding, WwiseRtpc? DepthFrom)
{
    /// <summary>The modulator's depth in per cent under these parameter values.</summary>
    public double DepthAt(IReadOnlyDictionary<uint, float> parameters)
    {
        if (DepthFrom is null) return Modulator.Value(WwiseModulatorProp.LfoDepth, 0);
        float value = parameters.TryGetValue(DepthFrom.SourceId, out var v) ? v : 0f;
        return DepthFrom.Evaluate(value, out _);
    }
}

/// <summary>What the modulators did while a stretch of audio was rendered, so a level can be reported.</summary>
public sealed class WwiseModulationStats
{
    public int Applied { get; internal set; }
    public double PeakDb { get; internal set; }
    public double PeakCents { get; internal set; }
}

/// <summary>
/// One sound placed on the timeline: which recording, when it starts, how long it sounds, the level and
/// pitch summed down the path that reached it, and the modulators bound along that path.
///
/// A voice is rendered in place, and can be rendered a block at a time: it carries its own read position
/// through the recording, because a pitch modulation makes that position advance at a rate that changes
/// while the voice sounds. Blocks have to be rendered in order.
/// </summary>
public sealed class WwiseVoice
{
    private readonly short[] _pcm;
    private readonly IReadOnlyList<WwiseBoundModulator> _modulators;
    private readonly double _heldSeconds;
    private readonly int _startSample, _totalSamples;
    private double _sourcePosition;
    private int _emitted;
    private bool _counted;

    public WwiseVoice(uint soundId, short[] pcm, double startMs, double sourceOffsetMs, double lengthMs,
                      double gain, double ratio, double heldMs, uint branch,
                      IReadOnlyList<WwiseBoundModulator> modulators)
    {
        SoundId = soundId; StartMs = startMs; LengthMs = lengthMs; Gain = gain; Ratio = ratio; Branch = branch;
        _pcm = pcm; _modulators = modulators; _heldSeconds = heldMs / 1000.0;
        _startSample = Samples(startMs);
        _totalSamples = Math.Max(0, Samples(lengthMs));
        _sourcePosition = sourceOffsetMs * CozmoAudio.SampleRate / 1000.0;
    }

    public uint SoundId { get; }
    public double StartMs { get; }
    public double LengthMs { get; }
    public double Gain { get; }
    public double Ratio { get; }
    /// <summary>Which immediate child of the MIDI target this voice came through; 0 for an audio clip.</summary>
    public uint Branch { get; }
    /// <summary>The sample index the voice starts at, and the one after its last.</summary>
    public int StartSample => _startSample;
    public int EndSample => _startSample + _totalSamples;
    /// <summary>True once every sample of the voice has been rendered.</summary>
    public bool Finished => _emitted >= _totalSamples;

    private static int Samples(double ms) => (int)Math.Round(ms * CozmoAudio.SampleRate / 1000.0);

    /// <summary>
    /// Renders the part of this voice that falls in <c>[from, to)</c> into <paramref name="mix"/>, with
    /// the modulators evaluated under <paramref name="parameters"/> as they stand now. Blocks must be
    /// asked for in order; a block wholly before the voice starts, or after it ends, does nothing.
    /// </summary>
    public void RenderInto(double[] mix, int from, int to, IReadOnlyDictionary<uint, float> parameters,
                           WwiseModulationStats stats)
    {
        if (_pcm.Length == 0 || _totalSamples == 0) return;
        int first = Math.Max(from, _startSample + _emitted);
        int last = Math.Min(Math.Min(to, mix.Length), _startSample + _totalSamples);
        if (first >= last) return;

        // Which bindings can move anything at all under the parameters in force for this block. An LFO at
        // zero depth produces nothing at any instant, which is the ordinary case with no cube being
        // shaken, so the loop below keeps its fast path.
        List<(WwiseBoundModulator Bound, double Depth)>? live = null;
        foreach (var b in _modulators)
        {
            double depth = b.DepthAt(parameters);
            if (b.Modulator.IsLfo && depth <= 0) continue;
            (live ??= new()).Add((b, depth));
        }
        if (live is not null && !_counted) { stats.Applied += live.Count; _counted = true; }

        for (int i = first; i < last; i++)
        {
            double gain = Gain, step = Ratio;
            if (live is not null)
            {
                double seconds = (i - _startSample) / (double)CozmoAudio.SampleRate;
                double db = 0, cents = 0;
                foreach (var (b, depth) in live)
                {
                    double value = b.Modulator.ValueAt(seconds, _heldSeconds, depth);
                    double mapped = b.Binding.Evaluate(value, out _);
                    if (b.Binding.ParamId == (byte)WwiseProp.Volume) db += mapped; else cents += mapped;
                }
                if (db < stats.PeakDb) stats.PeakDb = db;
                if (Math.Abs(cents) > Math.Abs(stats.PeakCents)) stats.PeakCents = cents;
                if (db != 0) gain *= Math.Pow(10, db / 20.0);
                if (cents != 0) step *= Math.Pow(2, cents / 1200.0);
            }
            if (i >= 0) mix[i] += _pcm[(int)_sourcePosition % _pcm.Length] * gain;
            _sourcePosition += step;
            _emitted++;
        }
    }
}

/// <summary>Every voice a plan resolves to, with the counts that make a render checkable.</summary>
public sealed record WwiseVoicePlan(IReadOnlyList<WwiseVoice> Voices, double TotalMs)
{
    public int NotesInWindow { get; init; }
    public int NotesPlayed { get; init; }
    public int NotesSilent { get; init; }
    public int NoteOffsPlayed { get; init; }
    public int AudioClips { get; init; }
    public IReadOnlyList<string> Problems { get; init; } = Array.Empty<string>();
}

/// <summary>
/// A song rendered as it plays, so that a game parameter posted while it plays can reach it.
///
/// <b>Why this exists.</b> The engine's singing behaviour posts <c>Cozmo_Singing_Vibrato</c> on every
/// tick from the cube shake, and in the banks that parameter drives the depth of the LFO bound to the
/// sampler's pitch. Rendering the whole song up front and caching the samples made that impossible: the
/// parameter could change all it liked and the audio was already fixed. So the song is rendered a block
/// at a time, each block reading the parameters as they stand when it is rendered, a little ahead of
/// where playback has reached.
///
/// <b>Scheduler safety.</b> Nothing here runs on the animation scheduler's thread. A worker renders
/// ahead of the wall clock by <see cref="LeadMs"/>; the scheduler only ever reads samples out of
/// <see cref="Pcm"/>, which is allocated whole at the start and filled in from the front. If the worker
/// were ever to fall behind, the scheduler would read zeros rather than block, and the shortfall is
/// counted in <see cref="Underruns"/>.
///
/// <b>What this does not reproduce.</b> The lead means a parameter posted at time t reaches the audio at
/// about t + <see cref="LeadMs"/>. That latency is inherent to rendering ahead of playback at all, and it
/// is bounded and stated rather than left to be discovered. The fidelity manifest records it as part of
/// M9-016.
/// </summary>
public sealed class WwiseMusicStream : IDisposable
{
    /// <summary>
    /// How far ahead of the wall clock the worker renders. Two animation frames: long enough that the
    /// scheduler never waits, short enough that a shake reaches the audio in well under a tenth of a
    /// second.
    /// </summary>
    public const double LeadMs = 66;

    private readonly WwiseVoicePlan _plan;
    private readonly WwiseBusChain? _chain;
    private readonly double[] _mix;
    private readonly short[] _out;
    private readonly object _gate = new();
    private readonly WwiseModulationStats _stats = new();
    private readonly List<string> _problems = new();
    private Dictionary<uint, float> _parameters;
    private double _volumeScale;
    private CancellationTokenSource? _worker;
    private int _nextVoice, _renderedTo, _committedTo;
    private readonly Dictionary<uint, int> _branchVoices = new();

    internal WwiseMusicStream(WwiseVoicePlan plan, WwiseBusChain? chain, IReadOnlyDictionary<uint, float> parameters,
                              IReadOnlySet<uint> excludeBranches, float volume)
    {
        _plan = plan;
        _chain = chain is { IsEmpty: false } ? chain : null;
        _parameters = new Dictionary<uint, float>(parameters);
        _volumeScale = volume;
        Excluded = excludeBranches;
        int samples = (int)Math.Round(plan.TotalMs * CozmoAudio.SampleRate / 1000.0);
        _mix = new double[Math.Max(0, samples)];
        _out = new short[_mix.Length];
        _problems.AddRange(plan.Problems);
    }

    /// <summary>
    /// The keyframe volume this song is playing at. Applied as the samples are committed, so it has to be
    /// set before playback rather than by scaling a copy of what has been rendered so far.
    /// </summary>
    public float Volume
    {
        get { lock (_gate) return (float)_volumeScale; }
        set { lock (_gate) _volumeScale = value; }
    }

    /// <summary>The buffer the scheduler reads. Filled in from the front as the song plays.</summary>
    public short[] Pcm => _out;
    public double DurationMs => _plan.TotalMs;
    /// <summary>Children of the MIDI target left out of this render; see <see cref="WwiseSongRenderer.ExcludeBranches"/>.</summary>
    public IReadOnlySet<uint> Excluded { get; }
    /// <summary>How many samples are ready to be read.</summary>
    public int Ready { get { lock (_gate) return _committedTo; } }
    /// <summary>Times the worker was asked for samples it had not rendered yet.</summary>
    public int Underruns { get; private set; }

    /// <summary>
    /// Whether this song has been handed to the scheduler. A stream is good for one play: Wwise draws
    /// which of a note's three recordings sounds afresh every time, so the next play prepares again
    /// rather than replaying the samples this one produced.
    /// </summary>
    public bool HasBegun { get; private set; }

    /// <summary>Sets the game parameters the next block will be rendered under.</summary>
    public void SetParameters(IReadOnlyDictionary<uint, float> parameters)
    {
        lock (_gate) _parameters = new Dictionary<uint, float>(parameters);
    }

    /// <summary>
    /// Renders forward until <paramref name="toMs"/> of the song is ready, under the parameters in force
    /// now. Safe to call from any thread but one at a time; the worker is the only caller once started.
    /// </summary>
    public void AdvanceTo(double toMs)
    {
        lock (_gate)
        {
            int target = Math.Min(_mix.Length, (int)Math.Round(toMs * CozmoAudio.SampleRate / 1000.0));
            if (target <= _committedTo) return;

            // Every voice whose onset is before the target has to be started before the region is
            // committed, because a voice never writes before its own onset but may well write after it.
            while (_nextVoice < _plan.Voices.Count && _plan.Voices[_nextVoice].StartSample < target)
            {
                var v = _plan.Voices[_nextVoice++];
                if (Excluded.Contains(v.Branch)) continue;
                _branchVoices[v.Branch] = _branchVoices.GetValueOrDefault(v.Branch) + 1;
                _active.Add(v);
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].RenderInto(_mix, _renderedTo, target, _parameters, _stats);
                if (_active[i].EndSample <= target) _active.RemoveAt(i);
            }
            _renderedTo = target;

            Commit(target);
        }
    }

    private readonly List<WwiseVoice> _active = new();

    private void Commit(int target)
    {
        if (target <= _committedTo) return;
        int from = _committedTo, count = target - from;
        if (_chain is not null)
        {
            var report = _chain.ProcessBlock(_mix, from, count);
            foreach (var p in report.Problems) if (!_problems.Contains(p)) _problems.Add(p);
            LastChain = report;
        }
        for (int i = from; i < target; i++)
        {
            double v = Math.Round(_mix[i] * _volumeScale);
            if (v > short.MaxValue) { v = short.MaxValue; Clipped++; }
            else if (v < short.MinValue) { v = short.MinValue; Clipped++; }
            _out[i] = (short)v;
            int magnitude = Math.Min(Math.Abs((int)_out[i]), short.MaxValue);
            if (magnitude > Peak) Peak = (short)magnitude;
        }
        _committedTo = target;
    }

    /// <summary>Samples that hit full scale after the chain; zero when it did its job.</summary>
    public int Clipped { get; private set; }
    public short Peak { get; private set; }
    /// <summary>What the bus chain did to the last block committed.</summary>
    public WwiseBusChainReport? LastChain { get; private set; }

    /// <summary>What has been rendered so far, in the same shape a whole-song render reports.</summary>
    public WwiseRenderedMusic Snapshot() => new(_out, _plan.TotalMs)
    {
        NotesInWindow = _plan.NotesInWindow, NotesPlayed = _plan.NotesPlayed, NotesSilent = _plan.NotesSilent,
        NoteOffsPlayed = _plan.NoteOffsPlayed, AudioClips = _plan.AudioClips,
        ClippedSamples = Clipped, Peak = Peak, BusChain = LastChain,
        ModulationsApplied = _stats.Applied, ModulationPeakDb = _stats.PeakDb, ModulationPeakCents = _stats.PeakCents,
        VoicesByBranch = new Dictionary<uint, int>(_branchVoices),
        Problems = _problems.ToList(),
    };

    /// <summary>
    /// Renders the first <see cref="LeadMs"/>, on the caller's thread on purpose: the caller is the
    /// behaviour's prewarm, which is off the scheduler, and it means the scheduler finds samples waiting
    /// the moment the audio keyframe fires. No worker starts here — the clock that the render runs ahead
    /// of is playback's, not preparation's, and a get-in animation can sit between the two.
    /// </summary>
    public void Start() => AdvanceTo(LeadMs);

    /// <summary>
    /// Playback has begun: from here a worker keeps the render <see cref="LeadMs"/> ahead of the clock
    /// until the song is done. Calling it again does nothing, so the scheduler asking for the buffer on
    /// every frame is harmless.
    ///
    /// The clock is the wall clock rather than the scheduler's position. If the animation timeline
    /// freezes — which it does whenever the robot has no room for another audio frame — the render runs
    /// further ahead than the lead, and the parameter is baked in that much earlier. It can never run
    /// behind, which is the property that matters for not stalling the scheduler.
    /// </summary>
    public void BeginPlayback()
    {
        lock (_gate)
        {
            HasBegun = true;
            if (_worker is not null || _committedTo >= _mix.Length) return;
            var cts = new CancellationTokenSource();
            _worker = cts;
            var started = DateTime.UtcNow;
            Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested && Ready < _mix.Length)
                {
                    AdvanceTo((DateTime.UtcNow - started).TotalMilliseconds + LeadMs);
                    try { await Task.Delay(10, cts.Token); } catch (OperationCanceledException) { return; }
                }
            }, cts.Token);
        }
    }

    /// <summary>Renders everything remaining at once. For tools and tests that want the finished song.</summary>
    public WwiseRenderedMusic RenderAll()
    {
        AdvanceTo(_plan.TotalMs);
        return Snapshot();
    }

    /// <summary>Counts a read of samples that were not ready, so a shortfall is visible rather than silent.</summary>
    internal void NoteUnderrun() => Underruns++;

    public void Dispose()
    {
        _worker?.Cancel();
        _worker?.Dispose();
        _worker = null;
    }
}
