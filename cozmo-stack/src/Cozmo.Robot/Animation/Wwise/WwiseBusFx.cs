// fidelity: M6-013
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The Parametric EQ filter types the bank can carry (gapC 4.2, 4.3). The values are the u32 type field of a
/// band; the labels are the row's:
/// <c>0 LP, 1 HP, 2 band-pass, 3 notch, 4 low shelf, 5 high shelf, 6 peaking</c>.
/// </summary>
public enum WwiseEqFilterType : uint
{
    /// <summary>Butterworth low pass (gapC 4.3). Q ignored.</summary>
    LowPass = 0,

    /// <summary>Butterworth high pass (gapC 4.3). Q ignored.</summary>
    HighPass = 1,

    /// <summary>RBJ band pass (gapC 4.3).</summary>
    BandPass = 2,

    /// <summary>RBJ notch (gapC 4.3).</summary>
    Notch = 3,

    /// <summary>RBJ low shelf, S = 1 fixed (gapC 4.3). Q unused.</summary>
    LowShelf = 4,

    /// <summary>RBJ high shelf, S = 1 fixed (gapC 4.3). Q unused.</summary>
    HighShelf = 5,

    /// <summary>RBJ peaking (gapC 4.3).</summary>
    Peaking = 6,
}

/// <summary>One EQ band's settings (gapC 4.2): <c>{u32 type, f32 gain dB, f32 freq, f32 Q, u8 on}</c>.</summary>
/// <param name="Type">The u32 type field; see <see cref="WwiseEqFilterType"/>.</param>
/// <param name="GainDb">The band gain in dB. Unused by the Butterworth LP/HP (gapC 4.3).</param>
/// <param name="Frequency">The band frequency in Hz, capped at <c>0.45·fs</c> by the coefficient routine.</param>
/// <param name="Q">The band Q. Ignored by LP/HP and by the shelves (gapC 4.3).</param>
/// <param name="On">Whether the band is applied (gapC 4.4). An off band is skipped.</param>
public readonly record struct WwiseEqBand(uint Type, float GainDb, float Frequency, float Q, bool On)
{
    /// <summary>The band's filter type.</summary>
    public WwiseEqFilterType FilterType => (WwiseEqFilterType)Type;
}

/// <summary>
/// The settings block a Parametric EQ ShareSet carries (M6-013, gapC 4.2, <c>SetParamsBlock 0xAA2E8C</c>,
/// 56 bytes): <c>3 × {u32 type, f32 gain dB, f32 freq, f32 Q, u8 on}</c>, then <c>f32 outputLevel dB</c> and
/// <c>u8 processLFE</c>.
///
/// <para>The two ShareSets on <c>Robot_Bus_1</c> are the shipped values, from Init.bnk HIRC type 18:</para>
/// <list type="bullet">
/// <item><c>0x6767FC1F</c>: band 1 low shelf +2.0 dB @835 Hz (Q 2.1 unused), band 2 peaking −2.5 dB @1359 Hz
/// Q 4.2, band 3 peaking −4.0 dB @5091 Hz Q 1.5, all on; output +1.5 dB.</item>
/// <item><c>0x174901C6</c>: band 1 high pass @333 Hz on, band 2 peaking −4.5 dB @1000 Hz Q 0.5 <b>off</b>,
/// band 3 low pass @14298 Hz on; output 0 dB.</item>
/// </list>
///
/// <para>The bank carries gain 0.0 and Q 1.0 for the LP/HP bands of <c>0x174901C6</c>; the coefficient routine
/// never reads them for types 0 and 1 (research bus-fx-17 section 1, Verification HOLDS). <c>processLFE</c> is 0
/// in both ShareSets (block byte <c>0x37</c>).</para>
/// </summary>
public sealed class WwiseEqSettings
{
    /// <summary>The first Robot_Bus_1 EQ ShareSet (gapC 4.2), full name <c>0x6767FC1F</c>.</summary>
    public const uint ShareSet1 = 0x6767FC1F;

    /// <summary>The second Robot_Bus_1 EQ ShareSet (gapC 4.2), full name <c>0x174901C6</c>.</summary>
    public const uint ShareSet2 = 0x174901C6;

    /// <summary>The three bands, in order.</summary>
    public IReadOnlyList<WwiseEqBand> Bands { get; }

    /// <summary>The post-EQ output level in dB (gapC 4.2).</summary>
    public float OutputLevelDb { get; }

    /// <summary>
    /// The <c>processLFE</c> byte (gapC 4.2). The row gives the field but not its shipped value; it is inert
    /// on the mono robot bus, so it is carried without inventing one.
    /// </summary>
    public bool ProcessLfe { get; }

    /// <summary>The ShareSet id this settings block came from, or 0 for a caller-constructed block.</summary>
    public uint ShareSetId { get; }

    private WwiseEqSettings(uint shareSetId, IReadOnlyList<WwiseEqBand> bands, float outputLevelDb, bool processLfe)
    {
        ShareSetId = shareSetId;
        Bands = bands;
        OutputLevelDb = outputLevelDb;
        ProcessLfe = processLfe;
    }

    /// <summary>Builds a settings block for a caller-supplied ShareSet (used for tests and future banks).</summary>
    public static WwiseEqSettings For(uint shareSetId, IReadOnlyList<WwiseEqBand> bands,
                                      float outputLevelDb, bool processLfe = false)
    {
        ArgumentNullException.ThrowIfNull(bands);
        if (bands.Count != 3) throw new ArgumentException("an EQ ShareSet has exactly three bands (gapC 4.2)", nameof(bands));
        return new(shareSetId, bands, outputLevelDb, processLfe);
    }

    /// <summary>Robot_Bus_1 slot 0: <c>0x6767FC1F</c> (gapC 4.2).</summary>
    public static WwiseEqSettings Eq1 { get; } = new(ShareSet1, new[]
    {
        new WwiseEqBand((uint)WwiseEqFilterType.LowShelf, 2.0f, 835f, 2.1f, true),
        new WwiseEqBand((uint)WwiseEqFilterType.Peaking, -2.5f, 1359f, 4.2f, true),
        new WwiseEqBand((uint)WwiseEqFilterType.Peaking, -4.0f, 5091f, 1.5f, true),
    }, 1.5f, false);

    /// <summary>Robot_Bus_1 slot 1: <c>0x174901C6</c> (gapC 4.2).</summary>
    public static WwiseEqSettings Eq2 { get; } = new(ShareSet2, new[]
    {
        new WwiseEqBand((uint)WwiseEqFilterType.HighPass, 0f, 333f, 1f, true),
        new WwiseEqBand((uint)WwiseEqFilterType.Peaking, -4.5f, 1000f, 0.5f, false),
        new WwiseEqBand((uint)WwiseEqFilterType.LowPass, 0f, 14298f, 1f, true),
    }, 0f, false);
}

/// <summary>One Peak Limiter settings block (gapC 4.5, <c>0xAA2084</c>, 22 bytes).</summary>
/// <param name="ThresholdDb">The threshold in dB (native <c>+4</c>).</param>
/// <param name="Ratio">The ratio (native <c>+8</c>).</param>
/// <param name="LookAheadSeconds">The look-ahead in seconds (native <c>+0x18</c>).</param>
/// <param name="ReleaseSeconds">The release in seconds (native <c>+0xC</c>).</param>
/// <param name="OutputDb">The output level in dB (native <c>+0x10</c>; stored as <c>powf(10, 0.05·v)</c>).</param>
/// <param name="ProcessLfe">The <c>processLFE</c> byte (native <c>+0x1C</c>).</param>
/// <param name="ChannelLink">The <c>channelLink</c> byte (native <c>+0x1D</c>).</param>
public readonly record struct WwisePeakLimiterSettings(
    float ThresholdDb,
    float Ratio,
    float LookAheadSeconds,
    float ReleaseSeconds,
    float OutputDb,
    bool ProcessLfe,
    byte ChannelLink)
{
    /// <summary>The plug-in's defaults (gapC 4.5, 0xAA20FC): <c>−12, 10, 0.01, 0.2, 1.0, 1, 1</c>.</summary>
    public static WwisePeakLimiterSettings Default { get; } = new(-12f, 10f, 0.01f, 0.2f, 1.0f, true, 1);

    /// <summary>The Robot_Bus_1 limiter ShareSet (gapC 4.5, <c>0xDF2230FF</c>).</summary>
    public const uint ShareSet = 0xDF2230FF;

    /// <summary>
    /// <c>0xDF2230FF</c>: threshold −1.0 dB, ratio 10.8, look-ahead 0.009 s, release 0.041 s, output 0 dB,
    /// processLFE 0, channelLink 0.
    /// </summary>
    public static WwisePeakLimiterSettings RobotBus1 { get; } =
        new(-1f, 10.8f, 0.009f, 0.041f, 0f, false, 0);
}

/// <summary>Builds the engine's parameter blocks from the settings records: the 56-byte Parametric EQ block (<c>0xAA2E8C</c>'s layout: the three bands at <c>0</c>, <c>0x11</c>, <c>0x22</c>, the output level at <c>0x33</c>, the ProcessLFE byte at <c>0x37</c>) and the 22-byte Peak Limiter block (<c>0xAA2084</c>'s layout).</summary>
internal static class WwiseBusFxBlocks
{
    internal static byte[] Eq(WwiseEqSettings settings)
    {
        var block = new byte[WwiseEqParams.BlockSize];
        for (int i = 0; i < 3; i++)
        {
            var band = settings.Bands[i];
            int o = 0x11 * i;
            BitConverter.TryWriteBytes(block.AsSpan(o, 4), band.Type);
            BitConverter.TryWriteBytes(block.AsSpan(o + 4, 4), band.GainDb);
            BitConverter.TryWriteBytes(block.AsSpan(o + 8, 4), band.Frequency);
            BitConverter.TryWriteBytes(block.AsSpan(o + 0xC, 4), band.Q);
            block[o + 0x10] = (byte)(band.On ? 1 : 0);
        }
        BitConverter.TryWriteBytes(block.AsSpan(0x33, 4), settings.OutputLevelDb);
        block[0x37] = (byte)(settings.ProcessLfe ? 1 : 0);
        return block;
    }

    internal static byte[] Limiter(WwisePeakLimiterSettings settings)
    {
        var block = new byte[WwiseLimiterParams.BlockSize];
        BitConverter.TryWriteBytes(block.AsSpan(0, 4), settings.ThresholdDb);
        BitConverter.TryWriteBytes(block.AsSpan(4, 4), settings.Ratio);
        BitConverter.TryWriteBytes(block.AsSpan(8, 4), settings.LookAheadSeconds);
        BitConverter.TryWriteBytes(block.AsSpan(0xC, 4), settings.ReleaseSeconds);
        BitConverter.TryWriteBytes(block.AsSpan(0x10, 4), settings.OutputDb);
        block[0x14] = (byte)(settings.ProcessLfe ? 1 : 0);
        block[0x15] = settings.ChannelLink;
        return block;
    }

    /// <summary>The mono Robot_Bus format the adapters run at: <c>u32 fmt[0]</c> the rate, <c>u32 fmt[4] = 0x4101</c> (the Cozmo_Robot bus's configuration word: one channel, no LFE flag).</summary>
    internal static WwiseEffectFormat MonoFormat(float sampleRate) => new((uint)MathF.Round(sampleRate), 0x4101);

    /// <summary>The engine's bus buffers carry at most <c>u16</c> frames; a longer span is fed in pieces of that size (the engine's own per-call frame count is the host configuration <c>u16 [0x1052440]</c>, not read).</summary>
    internal const int MaxFramesPerCall = 0xFFFF;

    /// <summary>Runs <paramref name="execute"/> over <paramref name="buffer"/> as mono <see cref="WwiseDecodeState"/>s of at most <see cref="MaxFramesPerCall"/> frames, copying the result back in place.</summary>
    internal static void RunMono(Span<float> buffer, Action<WwiseDecodeState> execute)
    {
        int pos = 0;
        while (pos < buffer.Length)
        {
            int n = Math.Min(buffer.Length - pos, MaxFramesPerCall);
            var data = buffer.Slice(pos, n).ToArray();
            execute(new WwiseDecodeState { Data = data, ChannelConfig = 0x4101, MaxFrames = (ushort)n, ValidFrames = (ushort)n });
            data.CopyTo(buffer.Slice(pos, n));
            pos += n;
        }
    }
}

/// <summary>
/// A thin compile-compatible adapter over the exact <see cref="WwiseEqPlugin"/> (<c>0xAA2A84</c>): a mono Robot_Bus slot built from <see cref="WwiseEqSettings"/> (the 56-byte block goes through <c>SetParamsBlock</c> <c>0xAA2E8C</c> and Clone <c>0xAA2DF4</c>, the instance through Create <c>0xAA257C</c>, Init <c>0xAA24B8</c>
/// and the owner's Reset <c>0xAA2488</c>). The later Robot_Bus slot batch replaces it with the bus's own slot objects; it adds no behaviour of its own except cutting a long span into buffers of at most <c>u16</c> frames.
/// <para>This is what <see cref="WwiseBusChain"/> builds per shipped EQ slot on Robot_Bus_1..4 (M9-026).</para>
/// </summary>
public sealed class WwiseParametricEq
{
    private readonly WwiseEqSettings _settings;
    private readonly float _sampleRate;
    private readonly WwiseEqPlugin _plugin;

    /// <summary>Builds an EQ for the settings at a sample rate.</summary>
    public WwiseParametricEq(WwiseEqSettings settings, float sampleRate)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!(sampleRate > 0f)) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        _settings = settings;
        _sampleRate = sampleRate;
        var alloc = new WwisePluginAllocator();
        var block = WwiseEqParams.Create(alloc)!;
        block.SetParamsBlock(WwiseBusFxBlocks.Eq(settings));
        var param = block.Clone(alloc)!;
        _plugin = WwiseEqPlugin.Create(alloc)!;
        if (_plugin.Init(alloc, null, param, WwiseBusFxBlocks.MonoFormat(sampleRate)) != 1) throw new InvalidOperationException("the EQ's state allocation failed");
        _plugin.Reset();
    }

    /// <summary>The settings this EQ runs.</summary>
    public WwiseEqSettings Settings => _settings;

    /// <summary>The plug-in object the adapter drives.</summary>
    public WwiseEqPlugin Plugin => _plugin;

    /// <summary>The stored previous output gain (<c>fx+0x50</c>).</summary>
    public float PreviousOutputGain => _plugin.PreviousGain;

    /// <summary>The coefficients a band currently carries (<c>this+4+0x14*band</c>).</summary>
    public WwiseBiquadCoefficients BandCoefficients(int band) => _plugin.Coefficients(band);

    /// <summary>The sample rate in Hz.</summary>
    public float SampleRate => _sampleRate;

    /// <summary>Executes the plug-in over a mono bus buffer, in place.</summary>
    public void Process(Span<float> buffer) => WwiseBusFxBlocks.RunMono(buffer, _plugin.Execute);

    /// <summary>The owner's Reset (<c>vt+0xC</c>): the biquad state is zeroed; the coefficients and the previous gain are not touched.</summary>
    public void Reset() => _plugin.Reset();
}

/// <summary>
/// A thin compile-compatible adapter over the exact <see cref="WwiseLimiterPlugin"/> (<c>0xAA1BD8</c>, P2 <c>0xAA0EB4</c>): a mono Robot_Bus slot built from <see cref="WwisePeakLimiterSettings"/> (the 22-byte block goes through <c>SetParamsBlock</c> <c>0xAA2084</c> and Clone <c>0xAA1FC4</c>, the instance through Create <c>0xAA18F4</c>,
/// Init <c>0xAA1B98</c> and the owner's Reset <c>0xAA0940</c>). The later Robot_Bus slot batch replaces it with the bus's own slot objects; it adds no behaviour of its own except cutting a long span into buffers of at most <c>u16</c> frames. A linked limiter (ChannelLink set) on a mono bus runs P2 as the engine's Setup
/// selects; the processes the inventory does not cover (P1, P3, the LFE swap) throw <see cref="WwiseMissingBehaviourException"/> when Execute reaches them.
/// </summary>
public sealed class WwisePeakLimiter
{
    private readonly WwisePeakLimiterSettings _settings;
    private readonly float _sampleRate;
    private readonly WwiseLimiterPlugin _plugin;

    /// <summary>Builds a limiter for the settings at a sample rate.</summary>
    public WwisePeakLimiter(WwisePeakLimiterSettings settings, float sampleRate)
    {
        if (!(sampleRate > 0f)) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        _settings = settings;
        _sampleRate = sampleRate;
        var alloc = new WwisePluginAllocator();
        var block = WwiseLimiterParams.Create(alloc)!;
        block.SetParamsBlock(WwiseBusFxBlocks.Limiter(settings));
        var param = block.Clone(alloc)!;
        _plugin = WwiseLimiterPlugin.Create(alloc)!;
        if (_plugin.Init(alloc, null, param, WwiseBusFxBlocks.MonoFormat(sampleRate)) != 1) throw new InvalidOperationException("the limiter's allocation failed");
        _plugin.Reset();
    }

    /// <summary>The settings this limiter runs.</summary>
    public WwisePeakLimiterSettings Settings => _settings;

    /// <summary>The plug-in object the adapter drives.</summary>
    public WwiseLimiterPlugin Plugin => _plugin;

    /// <summary>The look-ahead length in samples, <c>L</c> (<c>[+0x2C]</c>).</summary>
    public int LookAheadSamples => (int)_plugin.LookAheadFrames;

    /// <summary>The attack coefficient (<c>[+0x48]</c>).</summary>
    public float Attack => _plugin.AttackCoefficient;

    /// <summary>The release coefficient (<c>[+0x44]</c>); the engine computes it at the first Execute, so it is read after one.</summary>
    public float Release => _plugin.ReleaseCoefficient;

    /// <summary>The stored previous output gain (<c>+0x14</c>).</summary>
    public float PreviousOutputGain => _plugin.PreviousGain;

    /// <summary>The smallest detector gain applied since the last <see cref="Reset"/> (a diagnostic for the bus chain's report, not an engine value).</summary>
    public float MinGain => _plugin.MinGainObserved;

    /// <summary>Executes the plug-in over a mono bus buffer, in place.</summary>
    public void Process(Span<float> buffer) => WwiseBusFxBlocks.RunMono(buffer, _plugin.Execute);

    /// <summary>The owner's Reset (<c>vt+0xC</c>): the ring and the detectors are zeroed and the just-reset flag set; the previous output gain, the write index, the tail counter and the release coefficient are not touched.</summary>
    public void Reset()
    {
        _plugin.Reset();
        _plugin.ResetDiagnostics();
    }
}

/// <summary>
/// The <c>Robot_Bus_1..4</c> FX chain (M6-013, C45.1): when the bus state is 1 the slots run in order: Parametric EQ <c>0x6767FC1F</c> → Parametric EQ <c>0x174901C6</c> → Peak Limiter <c>0xDF2230FF</c> → Hijack.
///
/// <para>The Hijack is the Anki tap that carries the bus to the robot (M6-015); it leaves the samples alone (its Execute drains the buffer's valid count through the resampler), so it is listed as the last slot. The bus-state gate and the lifetime are the M6-014 bus's job; <see cref="Process"/> applies the three DSP stages, and
/// <see cref="ProcessIfActive"/> adds the state-1 gate for a caller that does not have M6-014.</para>
///
/// <para>The live path is <see cref="WwiseBusChain"/> (M9-026): it builds the same three stages per shipped slot. This class is the convenience composition of those stages for tests and future buses.</para>
/// </summary>
public sealed class WwiseRobotBusFx
{
    /// <summary>The only bus state in which the FX run (gapC 3.1, D2.4).</summary>
    public const int StateActive = 1;

    /// <summary>The EQ ShareSet in slot 0 (gapC 4.2).</summary>
    public WwiseParametricEq Eq1 { get; }

    /// <summary>The EQ ShareSet in slot 1 (gapC 4.2).</summary>
    public WwiseParametricEq Eq2 { get; }

    /// <summary>The Peak Limiter ShareSet in slot 2 (gapC 4.5).</summary>
    public WwisePeakLimiter Limiter { get; }

    /// <summary>The chain's slots in order, named as the record names them.</summary>
    public IReadOnlyList<string> Slots { get; } = new[]
    {
        "Parametric EQ 0x6767FC1F",
        "Parametric EQ 0x174901C6",
        "Peak Limiter 0xDF2230FF",
        "Hijack (tap for the robot; does not change the samples)",
    };

    /// <summary>Builds the shipped Robot_Bus_1 chain at a sample rate.</summary>
    public WwiseRobotBusFx(float sampleRate = WwiseRuntimeSettings.MixRateHz)
        : this(WwiseEqSettings.Eq1, WwiseEqSettings.Eq2, WwisePeakLimiterSettings.RobotBus1, sampleRate)
    {
    }

    /// <summary>Builds a chain from explicit settings (used for tests and future buses).</summary>
    public WwiseRobotBusFx(WwiseEqSettings eq1, WwiseEqSettings eq2,
                           WwisePeakLimiterSettings limiter, float sampleRate)
    {
        Eq1 = new WwiseParametricEq(eq1, sampleRate);
        Eq2 = new WwiseParametricEq(eq2, sampleRate);
        Limiter = new WwisePeakLimiter(limiter, sampleRate);
    }

    /// <summary>Applies EQ 0x6767FC1F → EQ 0x174901C6 → limiter, in slot order, to a mono bus buffer.</summary>
    public void Process(Span<float> buffer)
    {
        Eq1.Process(buffer);
        Eq2.Process(buffer);
        Limiter.Process(buffer);
    }

    /// <summary>
    /// gapC 3.1: runs <see cref="Process"/> only when the bus state is
    /// <see cref="StateActive"/>, as the native FX loop does.
    /// </summary>
    public void ProcessIfActive(Span<float> buffer, int busState)
    {
        if (busState != StateActive) return;
        Process(buffer);
    }

    /// <summary>Resets both EQs and the limiter.</summary>
    public void Reset()
    {
        Eq1.Reset();
        Eq2.Reset();
        Limiter.Reset();
    }
}
