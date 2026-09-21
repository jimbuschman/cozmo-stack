namespace Cozmo.Robot.Animation.Wwise;

/// <summary>What a bus chain did to a buffer, so a level can be reported rather than asserted.</summary>
public sealed record WwiseBusChainReport(double InputPeak, double OutputPeak, double LimiterReductionDb)
{
    /// <summary>The effects applied, in order, named.</summary>
    public IReadOnlyList<string> Stages { get; init; } = Array.Empty<string>();
    /// <summary>Anything in the chain this build could not apply, named rather than skipped silently.</summary>
    public IReadOnlyList<string> Problems { get; init; } = Array.Empty<string>();
    /// <summary>
    /// Things the chain established and acted on that a reader should know, but which are not faults: the
    /// low-pass at 14298 Hz sitting above Nyquist for the robot's 22320 Hz, for instance, which means it
    /// cannot act here and could not have acted in the engine either.
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
/// <b>What is exact and what is not.</b> Every parameter below is read from <c>Init.bnk</c>: three EQ
/// bands with their type, gain, frequency and Q, and a limiter's threshold, ratio, look-ahead and release.
/// The filters and the limiter themselves are ordinary, standard designs — biquads by the usual bilinear
/// formulas, and a look-ahead peak limiter — because Audiokinetic's own implementations are in the Wwise
/// runtime, which does not ship in the APK. So the settings are the product's and the arithmetic between
/// them is this stack's, which the fidelity manifest records as M9-011.
///
/// <b>One band cannot act.</b> The robot's audio runs at 22320 Hz, so Nyquist is 11160 Hz and the
/// low-pass at 14298 Hz is above it. It is reported as out of band rather than applied at a frequency it
/// cannot have. The engine's own robot audio is at the same rate
/// (<c>AnimConstants::AUDIO_SAMPLE_RATE</c>), so the band cannot have acted there either.
/// </summary>
public sealed class WwiseBusChain
{
    /// <summary>The bus a singing voice reaches, from the engine's own registration table.</summary>
    public const uint RobotBus1 = 2678428988;

    private readonly List<(string Name, Action<double[]> Apply)> _stages = new();
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

    private void Add(string name, WwiseEffectNode fx)
    {
        if (fx.ParametricEq() is { } eq)
        {
            var filters = new List<Biquad>();
            foreach (var (type, gainDb, frequency, q, on) in eq.Bands)
            {
                if (!on) continue;
                if (frequency >= _rate * 0.49)
                {
                    _notes.Add($"{name}: the band at {frequency:F0} Hz is above Nyquist for {_rate} Hz and cannot act");
                    continue;
                }
                if (Biquad.Design(type, gainDb, frequency, q, _rate) is { } b) filters.Add(b);
                else _problems.Add($"{name}: filter type {type} is not one of the four the shipped banks use");
            }
            double output = Math.Pow(10, eq.OutputDb / 20.0);
            _stages.Add((name, buffer =>
            {
                foreach (var f in filters) f.Process(buffer);
                if (Math.Abs(output - 1) > 1e-9)
                    for (int i = 0; i < buffer.Length; i++) buffer[i] *= output;
            }));
            return;
        }

        if (fx.PeakLimiter() is { } limiter && fx.PluginId == WwiseEffectNode.PeakLimiterPlugin)
        {
            _stages.Add((name, buffer => _reductionDb = Math.Min(_reductionDb, ApplyLimiter(buffer, limiter, _rate))));
            return;
        }

        if (fx.HijackIndex() is { } index)
        {
            _stages.Add(($"{name} (tap for robot {index})", _ => { }));
            return;
        }

        _problems.Add($"{name}: plug-in 0x{fx.PluginId:X8} is not applied by this build");
    }

    /// <summary>Applies the chain in place and reports what it did.</summary>
    public WwiseBusChainReport Process(double[] buffer)
    {
        double inPeak = Peak(buffer);
        _reductionDb = 0;
        foreach (var (_, apply) in _stages) apply(buffer);
        return new WwiseBusChainReport(inPeak, Peak(buffer), _reductionDb)
        {
            Stages = _stages.Select(s => s.Name).ToList(),
            Problems = _problems.ToList(),
            Notes = _notes.ToList(),
        };
    }

    /// <summary>The chain with nothing in it: used when the banks are not loaded, so a render still works.</summary>
    public bool IsEmpty => _stages.Count == 0;

    private static double Peak(double[] b)
    {
        double p = 0;
        foreach (var v in b) p = Math.Max(p, Math.Abs(v));
        return p;
    }

    /// <summary>
    /// A look-ahead peak limiter. The gain for each sample is worked out from the loudest sample in the
    /// look-ahead window that starts at it, so the reduction is already in place by the time a peak
    /// arrives; anything over the threshold is pushed back towards it by the ratio, and the reduction is
    /// let go again over the release time. Returns the deepest reduction it applied, in dB.
    ///
    /// The signal itself is not delayed. A hardware limiter delays it by the look-ahead and lives with the
    /// latency; an offline render does not have to, and not delaying keeps the song aligned with the
    /// animation that plays it.
    ///
    /// Full scale here is 32767, because that is what the render sums into and what the robot's frames
    /// carry; the threshold is in dB relative to it.
    /// </summary>
    private static double ApplyLimiter(double[] buffer, (float ThresholdDb, float Ratio, float LookAheadSeconds, float ReleaseSeconds, float OutputDb) p, int rate)
    {
        if (buffer.Length == 0) return 0;
        double threshold = short.MaxValue * Math.Pow(10, p.ThresholdDb / 20.0);
        double ratio = Math.Max(1.0, p.Ratio);
        int look = Math.Max(1, (int)Math.Round(p.LookAheadSeconds * rate));
        double releaseCoefficient = p.ReleaseSeconds > 0
            ? Math.Exp(-1.0 / (p.ReleaseSeconds * rate))
            : 0.0;
        double output = Math.Pow(10, p.OutputDb / 20.0);

        var input = (double[])buffer.Clone();

        // A running maximum over the look-ahead window, kept as a monotonic deque so the pass is linear
        // rather than quadratic: a nine-millisecond window at 22320 Hz is 201 samples, and a long song is
        // ten million of them.
        var window = new int[buffer.Length == 0 ? 1 : buffer.Length];
        int head = 0, tail = 0;
        double gain = 1.0, deepest = 0;
        int filled = -1;

        for (int i = 0; i < buffer.Length; i++)
        {
            int windowEnd = Math.Min(i + look - 1, buffer.Length - 1);
            while (filled < windowEnd)
            {
                filled++;
                double v = Math.Abs(input[filled]);
                while (tail > head && Math.Abs(input[window[tail - 1]]) <= v) tail--;
                window[tail++] = filled;
            }
            while (head < tail && window[head] < i) head++;
            double ahead = head < tail ? Math.Abs(input[window[head]]) : 0;

            double wanted = 1.0;
            if (ahead > threshold)
            {
                double over = 20 * Math.Log10(ahead / threshold);
                wanted = Math.Pow(10, -(over - over / ratio) / 20.0);
            }
            // attack is instantaneous, which the look-ahead is what makes musical; release is exponential
            gain = wanted < gain ? wanted : wanted + (gain - wanted) * releaseCoefficient;
            deepest = Math.Min(deepest, 20 * Math.Log10(Math.Max(gain, 1e-6)));
            buffer[i] = input[i] * gain * output;
        }
        return deepest;
    }

    /// <summary>
    /// A direct-form-1 biquad, with the four filter shapes the shipped banks use. The coefficient formulas
    /// are the standard bilinear-transform ones; Audiokinetic's are not in the package, so this is the
    /// arithmetic between the product's settings and not the product's own (M9-011).
    /// </summary>
    private sealed class Biquad
    {
        private double _b0, _b1, _b2, _a1, _a2;

        public static Biquad? Design(uint type, double gainDb, double frequency, double q, int rate)
        {
            double w = 2 * Math.PI * frequency / rate;
            double cos = Math.Cos(w), sin = Math.Sin(w);
            q = q <= 0 ? 0.7071 : q;
            double alpha = sin / (2 * q);
            double a0, b0, b1, b2, a1, a2;

            switch (type)
            {
                case 0:                                            // low pass
                    b0 = (1 - cos) / 2; b1 = 1 - cos; b2 = (1 - cos) / 2;
                    a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
                    break;
                case 1:                                            // high pass
                    b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = (1 + cos) / 2;
                    a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
                    break;
                case 4:                                            // low shelf
                {
                    double a = Math.Pow(10, gainDb / 40.0);
                    double beta = Math.Sqrt(a) / q;
                    b0 = a * ((a + 1) - (a - 1) * cos + beta * sin);
                    b1 = 2 * a * ((a - 1) - (a + 1) * cos);
                    b2 = a * ((a + 1) - (a - 1) * cos - beta * sin);
                    a0 = (a + 1) + (a - 1) * cos + beta * sin;
                    a1 = -2 * ((a - 1) + (a + 1) * cos);
                    a2 = (a + 1) + (a - 1) * cos - beta * sin;
                    break;
                }
                case 6:                                            // peaking
                {
                    double a = Math.Pow(10, gainDb / 40.0);
                    b0 = 1 + alpha * a; b1 = -2 * cos; b2 = 1 - alpha * a;
                    a0 = 1 + alpha / a; a1 = -2 * cos; a2 = 1 - alpha / a;
                    break;
                }
                default:
                    return null;
            }
            return new Biquad { _b0 = b0 / a0, _b1 = b1 / a0, _b2 = b2 / a0, _a1 = a1 / a0, _a2 = a2 / a0 };
        }

        public void Process(double[] buffer)
        {
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                double x = buffer[i];
                double y = _b0 * x + _b1 * x1 + _b2 * x2 - _a1 * y1 - _a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                buffer[i] = y;
            }
        }
    }
}
