namespace Cozmo.Robot.Animation.Wwise;

/// <summary>One effect in a bus's chain, in the order the bus applies them.</summary>
public readonly record struct WwiseBusEffect(byte Index, uint EffectId, bool IsShareSet, bool IsRendered);

/// <summary>
/// An audio bus (HIRC type 8): where it sends, what it ducks, and the effects it applies.
///
/// All fifteen buses in the shipped <c>Init.bnk</c> consume their payload to the last byte under this
/// layout, which is how its two variable parts were settled: a positioning byte that carries one more
/// byte when it is non-zero (four buses set it), and a list of ducked buses (one bus ducks one).
/// </summary>
public sealed record WwiseBusNode(uint Id, string Bank, WwiseNodeParams Params,
                                  IReadOnlyList<WwiseBusEffect> Effects,
                                  IReadOnlyList<(uint BusId, float DuckVolumeDb, uint FadeOutMs, uint FadeInMs)> Ducks)
    : WwiseNode(Id, WwiseObjectType.AudioBus, Bank, Params, Array.Empty<uint>())
{
    /// <summary>The bus this one sends to, or 0 for a master bus.</summary>
    public uint ParentBusId => Params.ParentId;

    /// <summary>The bus's <c>+0x44</c>: the u16 max instances masked with <c>0x3FF</c> (D6.1, M6-026 W7).</summary>
    public ushort MaxInstances { get; init; }

    /// <summary>The bus's byte B (D6.1: b0 to <c>+0x45</c> bit2, b1 to <c>+0x45</c> bit3, b2 to <c>+0x47</c> bit6).</summary>
    public byte ByteB { get; init; }

    /// <summary>
    /// The bus's byte C (the byte after the channel config, <c>0x9C6528</c>): bit 0 stores <c>[bus+0x40]</c> <c>0xE0000</c> (<c>0x9C6540/44</c>, the only writer of those bits, C40.3 T-N1), bit 1 stores <c>[bus+0xCC]</c> bit 3 (<c>0x9C654C</c>). All 15 shipped buses have 2. The reader used to discard it.
    /// </summary>
    // fidelity: M6-025
    public byte ByteC { get; init; }

    // fidelity: M6-001, M6-025
    /// <summary>The bus reader's A byte (<c>0x9C6458..0x9C6464</c>: bit 0 to <c>[+0x46]</c> bit 7, bit 1 to <c>[+0x47]</c> bit 0).</summary>
    public byte ByteA { get; init; }
    /// <summary>The channel-config word (<c>0x9C64C4..0x9C665C</c>).</summary>
    public uint ChannelConfig { get; init; }
    /// <summary>The bank's max-duck float, stored to <c>[bus+0x6C]</c> by the bus init (<c>0x9C40D8</c>).</summary>
    public float MaxDuck { get; init; }
    /// <summary>The duck entries with the curve and target-property bytes (<c>0x9C4138</c> -> <c>0x9C3E94</c>); <see cref="WwiseBusNode.Ducks"/> keeps the first four fields.</summary>
    public IReadOnlyList<WwiseDuckEntry> DuckEntries { get; init; } = Array.Empty<WwiseDuckEntry>();
    /// <summary>The FX bypass byte (present only when the FX entry count is non-zero), the argument of <c>0x9F5C30(bus, bypass, -1)</c>.</summary>
    public byte FxBypass { get; init; }
    /// <summary>The mixer id read after the FX list (<c>vt+0xE0</c> = <c>0x9C0FC0</c> argument).</summary>
    public uint MixerId { get; init; }
    /// <summary>The mixer flag byte after the mixer id.</summary>
    public byte MixerFlag { get; init; }
    /// <summary>The byte stored to <c>[bus+0x45]</c> bit 5 (<c>0x9C41FC..0x9C4204</c>).</summary>
    public byte AttachByte { get; init; }

    /// <summary>The bank's recovery time in ms (D6.1: converted to samples at <c>bus+0x64</c>); <c>0x9C5240</c> tests <c>[bus+0x64] != 0</c> (M6-026 1.3).</summary>
    public uint RecoveryMs { get; init; }
}

/// <summary>
/// An effect (HIRC type 18, a share set, or 19, a custom instance): which plug-in, and its parameters as
/// the plug-in packed them. All 89 in the shipped banks consume exactly.
///
/// The plug-in ids are the ones <c>PluginInfo.xml</c> lists for this build, which is how each parameter
/// block is known to be the one it is: 0x006E0003 is <c>AkPeakLimiter</c>, 0x00690003
/// <c>ParametricEQ</c>, 0x006C0003 <c>AkCompressor</c>, 0x000112C3 Anki's own <c>HijackAudio</c>.
/// </summary>
public sealed record WwiseEffectNode(uint Id, WwiseObjectType Type, string Bank, uint PluginId, ReadOnlyMemory<byte> Parameters)
    : WwiseNode(Id, Type, Bank,
                new WwiseNodeParams(0, 0, 0, new Dictionary<byte, uint>(), new Dictionary<byte, (float, float)>(),
                                    Array.Empty<WwiseRtpc>(), Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>()),
                Array.Empty<uint>())
{
    public const uint ParametricEqPlugin = 0x00690003;
    public const uint PeakLimiterPlugin = 0x006E0003;
    public const uint CompressorPlugin = 0x006C0003;
    /// <summary>Anki's own effect, which taps a bus and sends what it hears to a robot. Its one parameter is the robot's index.</summary>
    public const uint AnkiHijackPlugin = 0x000112C3;

    private float Float(int offset) => BitConverter.ToSingle(Parameters.Span.Slice(offset, 4));
    private uint Word(int offset) => BitConverter.ToUInt32(Parameters.Span.Slice(offset, 4));

    /// <summary>
    /// The three bands and the output level of a Wwise Parametric EQ. Each band is a filter type, a gain
    /// in dB, a frequency in Hz, a Q and an on/off byte, packed in that order; the four types the shipped
    /// banks use are 0 low pass, 1 high pass, 4 low shelf and 6 peaking. The reading is settled by the
    /// bank's own naming: <c>Robot_Bus_Eq_HiLowPass</c> is the one that carries types 1 and 0, at 333 Hz
    /// and 14298 Hz.
    /// </summary>
    public (IReadOnlyList<(uint Type, float GainDb, float Frequency, float Q, bool On)> Bands, float OutputDb)? ParametricEq()
    {
        if (PluginId != ParametricEqPlugin || Parameters.Length < 56) return null;
        var bands = new List<(uint, float, float, float, bool)>(3);
        for (int i = 0; i < 3; i++)
        {
            int o = i * 17;
            bands.Add((Word(o), Float(o + 4), Float(o + 8), Float(o + 12), Parameters.Span[o + 16] != 0));
        }
        return (bands, Float(51));
    }

    /// <summary>
    /// A Wwise Peak Limiter's five floats and two flags: threshold in dB, ratio, look-ahead and release in
    /// seconds, output level in dB, then process-LFE and channel-link. The same five-floats-two-flags block
    /// is what a Wwise Compressor carries, with attack in place of look-ahead.
    /// </summary>
    public (float ThresholdDb, float Ratio, float LookAheadSeconds, float ReleaseSeconds, float OutputDb)? PeakLimiter()
    {
        if (PluginId is not (PeakLimiterPlugin or CompressorPlugin) || Parameters.Length < 22) return null;
        return (Float(0), Float(4), Float(8), Float(12), Float(16));
    }

    /// <summary>
    /// A Peak Limiter's two flag bytes (M6-013 / gapC 4.5, the 22-byte params block): <c>processLFE</c> then
    /// <c>channelLink</c>, after the five floats. The row gives their runtime-object offsets as <c>+0x1C</c>
    /// and <c>+0x1D</c>; the shipped <c>0xDF2230FF</c> carries 0 for both (gapC 4.5).
    /// </summary>
    public (bool ProcessLfe, byte ChannelLink) LimiterFlags()
    {
        if (PluginId is not (PeakLimiterPlugin or CompressorPlugin) || Parameters.Length < 22)
            return (false, 0);
        return (Parameters.Span[20] != 0, Parameters.Span[21]);
    }

    /// <summary>The robot index an Anki Hijack taps a bus for: 1 to 4, matching the four Robot_Bus_N.</summary>
    public uint? HijackIndex() =>
        PluginId == AnkiHijackPlugin && Parameters.Length >= 4 ? Word(0) : null;
}
