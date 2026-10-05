// fidelity: M9-011, M9-026, M9-027
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>What a bus chain did to a buffer, so a level can be reported rather than asserted.</summary>
public sealed record WwiseBusChainReport(double InputPeak, double OutputPeak, double LimiterReductionDb)
{
    /// <summary>The effects applied, in order, named.</summary>
    public IReadOnlyList<string> Stages { get; init; } = Array.Empty<string>();
    /// <summary>Anything in the chain this build could not apply, named rather than skipped silently.</summary>
    public IReadOnlyList<string> Problems { get; init; } = Array.Empty<string>();
    /// <summary>
    /// Things the chain established and acted on that a reader should know, but which are not faults. At the
    /// stack's current render rate the exact EQ coefficient routine caps the shipped 14298 Hz low-pass to
    /// <c>0.45·fs</c>; that is a consequence of the stack's rate, not of the engine's, which runs the chain
    /// at the 48000 Hz Wwise mix rate and resamples through the Hijack (M6-017/M6-018). M9-027.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

/// <summary>
/// The effect chain a robot's audio passes through on its way out of Wwise, built from the shipped banks
/// and applied to a rendered buffer.
///
/// <b>Which bus, and why.</b> <c>RobotAudioClient</c>'s constructor (0x005994A0) registers five robot
/// audio buffers, and the last three arguments of each call are the game object, the Anki Hijack plug-in
/// index and the bus:
///
/// <code>
///   game object 7  plug-in 1  bus 2678428988  Robot_Bus_1
///   game object 8  plug-in 2  bus 2678428991  Robot_Bus_2
///   game object 9  plug-in 3  bus 2678428990  Robot_Bus_3
///   game object 10 plug-in 4  bus 2678428985  Robot_Bus_4
///   game object 6  plug-in 0  bus 0           Cozmo_OnDevice, which plays on the phone
/// </code>
///
/// (0x0059962A..0x0059966A: the bus ids are built as 0x9FA59539 plus 3, 6, 5 and 0.) The four buses each
/// carry the same three effects followed by their own Anki Hijack, whose single parameter is that same
/// plug-in index — 1, 2, 3, 4 — so the bank and the binary agree on the routing from two directions.
/// A singing behaviour posts its switch on game object 7, so what the robot hears is Robot_Bus_1's output
/// after <c>Robot_Bus_Eq_MasterCurve</c>, <c>Robot_Bus_Eq_HiLowPass</c> and <c>Robot_Bus_Peak_Limiter</c>.
///
/// <b>What is exact.</b> Every parameter below is read from <c>Init.bnk</c>: three EQ bands with their
/// type, gain, frequency and Q, and a limiter's threshold, ratio, look-ahead and release. The filters and
/// the limiter are the recovered Wwise plug-in DSP (M6-013; M9-026): the parametric-EQ coefficient routine
/// and direct-form-I biquad of <see cref="WwiseEqPlugin"/> (<see cref="WwiseEqCoefficients"/>), and the
/// peak-hold look-ahead limiter of <see cref="WwiseLimiterPlugin"/>, driven through the mono adapters
/// <see cref="WwiseParametricEq"/> and <see cref="WwisePeakLimiter"/>. The former stand-in biquad and limiter
/// are gone from the live path. The chain runs at the rate its caller passes; the stack currently renders
/// and runs it at the robot's 22320 Hz (<see cref="CozmoAudio.SampleRate"/>).
///
/// <b>The rate caveat (M9-027).</b> This is the exact recovered chain, but the stack runs it at its current
/// 22320 Hz render rate. The engine runs the bus FX at the 48000 Hz Wwise mix rate (M6-018 / gapC 4.6),
/// where the shipped <c>14298 Hz</c> low-pass is in band (Nyquist 24000), and only then resamples the mix
/// to 22320 for the robot through the Hijack (M6-015 / M6-017). At the stack's 22320 Hz the exact
/// coefficient routine's <c>0.45·fs</c> cap (gapC 4.3) puts that low-pass at <b>10044 Hz and applies
/// it</b>, not skipped. That cap is therefore a consequence of the stack's render rate, <b>not</b> the
/// engine's behaviour; moving the Wwise mix to 48000 and resampling through the Hijack is M6-017/M6-018,
/// and M9-027 stays an implementation gap until then.
/// </summary>
public sealed class WwiseBusChain
{
    /// <summary>The bus a singing voice reaches, from the engine's own registration table.</summary>
    public const uint RobotBus1 = 2678428988;

    private readonly List<(string Name, Action<float[]> Apply)> _stages = new();
    private readonly List<Action> _resets = new();
    private readonly List<string> _problems = new();
    private readonly List<string> _notes = new();
    private readonly int _rate;
    private double _reductionDb;

    private WwiseBusChain(int rate) => _rate = rate;

    /// <summary>
    /// Builds the chain for one bus at one sample rate. Effects the build cannot apply are named in the
    /// report; the Anki Hijack is the tap that sends the bus on to the robot and does nothing to the
    /// samples, so it is listed and passed over.
    /// </summary>
    public static WwiseBusChain For(WwiseSoundLibrary lib, uint busId, int sampleRate)
    {
        var chain = new WwiseBusChain(sampleRate);
        if (lib.Node(busId) is not WwiseBusNode bus)
        {
            chain._problems.Add($"bus {busId} is not in the loaded banks");
            return chain;
        }
        foreach (var slot in bus.Effects.OrderBy(e => e.Index))
        {
            if (lib.Node(slot.EffectId) is not WwiseEffectNode fx)
            {
                chain._problems.Add($"effect {slot.EffectId} on bus {busId} is not readable");
                continue;
            }
            string name = lib.Names.Effects.TryGetValue(fx.Id, out var n) ? n : fx.Id.ToString();
            chain.Add(name, fx);
        }
        return chain;
    }

    // fidelity: M9-011, M9-026, M9-027
    private void Add(string name, WwiseEffectNode fx)
    {
        if (fx.ParametricEq() is { } eq)
        {
            // M9-026: the exact coefficient routine's bands, from the shipped ShareSet's own settings.
            // M9-027: this stack runs the chain at its own render rate, not the engine's 48000 Hz Wwise mix
            // rate (M6-017/M6-018). The routine caps every band at 0.45·fs (gapC 4.3), so at that stack rate
            // the 14298 Hz low-pass is capped and applied; the cap is a stack-rate consequence, not the
            // engine's behaviour, and is reported rather than the band skipped.
            var bands = new List<WwiseEqBand>(eq.Bands.Count);
            foreach (var (type, gainDb, frequency, q, on) in eq.Bands)
            {
                bands.Add(new WwiseEqBand(type, gainDb, frequency, q, on));
                if (on && frequency > WwiseEqCoefficients.NyquistFraction * _rate)
                    _notes.Add($"{name}: at this stack's {_rate} Hz render rate the {frequency:F0} Hz band is " +
                               $"capped to 0.45·fs = {WwiseEqCoefficients.NyquistFraction * _rate:F0} Hz by the " +
                               "exact EQ coefficient routine (gapC 4.3); the engine runs this chain at the " +
                               "48000 Hz Wwise mix rate (M6-017/M6-018)");
            }
            // C45.2: the block's ProcessLFE byte is at 0x37 (0xAA2F2C); the shipped ShareSets carry 0.
            var settings = WwiseEqSettings.For(fx.Id, bands, eq.OutputDb, fx.Parameters.Span[0x37] != 0);
            var filter = new WwiseParametricEq(settings, _rate);
            _resets.Add(filter.Reset);
            _stages.Add((name, scratch => filter.Process(scratch)));
            return;
        }

        if (fx.PeakLimiter() is { } limiter && fx.PluginId == WwiseEffectNode.PeakLimiterPlugin)
        {
            // M9-026: the exact Peak Limiter plug-in (WwiseLimiterPlugin, P2 0xAA0EB4). The two flag bytes are
            // the bank's own (gapC 4.5); the shipped ShareSet is unlinked (channelLink 0). The linked processes
            // the inventory reads only structurally throw WwiseMissingBehaviourException when Execute reaches them.
            var (processLfe, channelLink) = fx.LimiterFlags();
            var settings = new WwisePeakLimiterSettings(
                limiter.ThresholdDb, limiter.Ratio, limiter.LookAheadSeconds, limiter.ReleaseSeconds,
                limiter.OutputDb, processLfe, channelLink);
            var device = new WwisePeakLimiter(settings, _rate);
            _resets.Add(device.Reset);
            _stages.Add((name, scratch =>
            {
                device.Process(scratch);
                _reductionDb = Math.Min(_reductionDb, 20 * Math.Log10(Math.Max(device.MinGain, 1e-6)));
            }));
            return;
        }

        if (fx.HijackIndex() is { } index)
        {
            _stages.Add(($"{name} (tap for robot {index})", _ => { }));
            return;
        }

        _problems.Add($"{name}: plug-in 0x{fx.PluginId:X8} is not applied by this build");
    }

    /// <summary>Applies the chain to a whole buffer in place and reports what it did.</summary>
    public WwiseBusChainReport Process(double[] buffer) => ProcessBlock(buffer, 0, buffer.Length);

    /// <summary>
    /// Applies the chain to <c>[from, from + count)</c> in place, keeping every effect's state between
    /// calls, so a song can be run through it a block at a time as it plays. Blocks must be given in
    /// order: the filters carry their previous samples and the limiter carries its gain.
    ///
    /// The recovered plug-ins are float32 arithmetic on Wwise's normalized bus buffer (−1..1), so the
    /// render's full-scale slice (<see cref="short.MaxValue"/>) is normalized once here, run through every
    /// stage and scaled back.
    /// </summary>
    public WwiseBusChainReport ProcessBlock(double[] buffer, int from, int count)
    {
        // Starting at the top of a buffer means a new piece of audio, so nothing of the last one carries
        // into it: a filter that remembered the end of the previous song would colour the start of this.
        if (from == 0) Reset();
        count = Math.Max(0, Math.Min(count, buffer.Length - from));
        double inPeak = Peak(buffer, from, count);
        _reductionDb = 0;
        if (count > 0)
        {
            const float fullScale = short.MaxValue;
            var scratch = new float[count];
            for (int i = 0; i < count; i++) scratch[i] = (float)(buffer[from + i] / fullScale);
            foreach (var (_, apply) in _stages) apply(scratch);
            for (int i = 0; i < count; i++) buffer[from + i] = scratch[i] * fullScale;
        }
        return new WwiseBusChainReport(inPeak, Peak(buffer, from, count), _reductionDb)
        {
            Stages = _stages.Select(s => s.Name).ToList(),
            Problems = _problems.ToList(),
            Notes = _notes.ToList(),
        };
    }

    /// <summary>The chain with nothing in it: used when the banks are not loaded, so a render still works.</summary>
    public bool IsEmpty => _stages.Count == 0;

    /// <summary>Forgets every filter's history and the limiter's gain, ready for a fresh piece of audio.</summary>
    public void Reset() { foreach (var r in _resets) r(); }

    private static double Peak(double[] b, int from, int count)
    {
        double p = 0;
        for (int i = from; i < from + count; i++) p = Math.Max(p, Math.Abs(b[i]));
        return p;
    }
}
