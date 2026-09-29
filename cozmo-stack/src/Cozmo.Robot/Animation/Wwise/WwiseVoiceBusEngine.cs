// fidelity: M6-022
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The voice buffer <c>params</c> the per-voice DSP chain reads and writes (M6-022 V8/V9/V11, C12 source
/// classes Q4). The field offsets are the instruction reads the report lists:
/// <c>+0x00</c> data pointer, <c>+0x04</c> rate/format, <c>+0x08</c> status, <c>+0x0C</c> valid frames,
/// <c>+0x0E</c> max frames, <c>+0x18</c> start sample, <c>+0x20</c> total, <c>+0x24</c> pitch/step,
/// <c>+0x28</c> result code. <c>+0x2C</c> is set to 1 when <c>[PBI+0x1F8]</c> is present (V7).
///
/// <para>The native buffer is planar float (<c>0x00AB3520</c> writes channel <c>c</c> at
/// <c>out + samples*c*4</c>). This model carries the planar channels and the settled counts.</para>
/// </summary>
public sealed class WwiseVoiceBuffer
{
    // fidelity: M6-022

    /// <summary>Planar sample data, one array per channel (native <c>+0x00</c>).</summary>
    public float[][] Channels { get; }

    /// <summary><c>+0x04</c>: the rate/format word the emit writes (C13 Q2).</summary>
    public int Rate { get; set; }

    /// <summary><c>+0x0C</c>: valid frames; the dispatcher sets <c>0x400</c> before the source render (C12 X4).</summary>
    public int ValidFrames { get; set; }

    /// <summary><c>+0x0E</c>: max frames.</summary>
    public int MaxFrames { get; }

    /// <summary><c>+0x18</c>: start sample.</summary>
    public int Start { get; set; }

    /// <summary><c>+0x20</c>: total/end sample.</summary>
    public int Total { get; set; }

    /// <summary><c>+0x24</c>: pitch/step.</summary>
    public int Pitch { get; set; }

    /// <summary><c>+0x28</c>: the DSP result code (0x11/0x2B/0x2D/2).</summary>
    public int Result { get; set; }

    /// <summary><c>+0x2C</c>: set to 1 when <c>[PBI+0x1F8]</c> is present (V7).</summary>
    public bool HasBusParam { get; set; }

    /// <summary>Creates a buffer of <paramref name="channels"/> planar arrays of <paramref name="maxFrames"/>.</summary>
    public WwiseVoiceBuffer(int channels, int maxFrames)
    {
        if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));
        if (maxFrames < 1) throw new ArgumentOutOfRangeException(nameof(maxFrames));
        Channels = new float[channels][];
        for (int c = 0; c < channels; c++) Channels[c] = new float[maxFrames];
        MaxFrames = maxFrames;
    }

    /// <summary>The channel count (native <c>+0x04</c>'s low byte, the format's channel config).</summary>
    public int ChannelCount => Channels.Length;

    /// <summary>Zeroes every channel for <paramref name="count"/> frames (the mixer's zero-pad).</summary>
    public void Clear(int count)
    {
        for (int c = 0; c < Channels.Length; c++)
            Array.Clear(Channels[c], 0, Math.Min(count, MaxFrames));
    }
}

/// <summary>
/// The source vtable the per-voice chain calls (M6-022 V8/V9, C12 source-classes Q3). The shipped robot audio
/// is Vorbis-first (1826 stream 1 + 27 stream 0) plus 333 ADPCM (source-classes Q1c); those source classes
/// are M6-002/M6-003's work, so the render is a seam here and is not re-implemented.
///
/// <para><b>Slots.</b> <see cref="Render"/> is <c>vt+0x30</c> (0xA44630 step 6, the render bodies
/// <c>0xA73D34</c>/<c>0xA72554</c>/<c>0xAB0448</c>/<c>0xAB1550</c>); <see cref="StartStream"/> is
/// <c>vt+0x28</c> (0xA54A30); <see cref="StartStreamSucceeded"/> is <c>vt+0x4C</c> (the byte at
/// <c>[PBI+0x1BE]</c> bit 6, C12 source-classes Q2).</para>
/// </summary>
public interface IWwiseVoiceSource
{
    /// <summary>The source's channel count (the format's channel config; mono 1, stereo 2).</summary>
    int Channels { get; }

    /// <summary>The source's sample rate, for the voice-stage resampler's <c>Init</c> (M6-004).</summary>
    int SampleRate { get; }

    /// <summary>
    /// <c>vt+0x30</c> render (V8 step 6): fill <paramref name="buffer"/> with up to
    /// <see cref="WwiseVoiceBuffer.MaxFrames"/> frames and return the result code (0x2D DataReady,
    /// 0x2E NoMoreData, 0x11, 2). The DSP leaves are M6-002/M6-003.
    /// </summary>
    int Render(WwiseVoiceBuffer buffer);

    /// <summary><c>vt+0x28</c> StartStream (0xA54A30), returns true on success (result 1).</summary>
    bool StartStream();

    /// <summary><c>vt+0x4C</c>: the <c>[PBI+0x1BE]</c> bit 6 the per-voice machine tests as <c>SRC10</c>.</summary>
    bool StartStreamSucceeded { get; }

    /// <summary>
    /// V7 prologue <c>0xA54F70</c>: <c>[source+8]</c>, a pointer to a <c>{+0,+4}</c> gain pair, or null when
    /// <c>[source+8]==0</c>. <c>[+4]</c> scales the gain; <c>[+0]</c> scales it again when <c>[bus+0x58]</c>
    /// bit0 is set. The source class is UNKNOWN, so this is a caller seam.
    /// </summary>
    (float At0, float At4)? Gain8 => null;
}

/// <summary>
/// One voice-to-bus connection (M6-022 V7/V8/V14, M6-012). The native voice holds a connection list at
/// <c>voice+0x28</c>; each connection carries the mixer state (the <see cref="WwiseMixerConnection"/>), the
/// destination bus and the aux/dry routing flags the V8 walk tests (<c>conn+0x68</c>, <c>conn+0x18</c>,
/// <c>conn+0x6C</c> bits 1/2).
/// </summary>
public sealed class WwiseVoiceConnection
{
    // fidelity: M6-022, M6-012

    /// <summary>The per-connection gain/pan mixer state (M6-012).</summary>
    public WwiseMixerConnection Mixer { get; }

    /// <summary>The destination bus (<c>conn+0x30</c>, the V8 aux-send walk's bus argument).</summary>
    public WwiseMixBus Bus { get; }

    /// <summary>The native <c>conn+0x68</c> flag; the V8 walk tests it.</summary>
    public bool HasAux { get; set; }

    /// <summary>The native <c>conn+0x18</c> flag; the V8 walk tests it.</summary>
    public bool HasDry { get; set; }

    /// <summary>The native <c>conn+0x6C</c> bits 1/2 (fade/format state).</summary>
    public byte Flags6C { get; set; }

    /// <summary>V7/C1 <c>0xA4B4B0</c> ducking: the per-connection <c>+0x60</c>.</summary>
    public float C60 { get; set; }

    /// <summary>V7/C1 <c>0xA4BE6C</c>: the per-connection gain <c>[conn+0xC] = [voice+0x1C]*gain</c>.</summary>
    public float ConnectionGain { get; set; } = 1f;

    /// <summary>V7/C1 <c>0xA4BC58</c> per-connection fields: <c>+0x08/+0x0C/+0x10/+0x14</c> and the four
    /// min inputs <c>+0x50/+0x54/+0x58/+0x5C</c>; <c>+0x64</c> is the re-init id.</summary>
    public float C0C { get; set; }
    public float C08 { get; set; }
    public float C10 { get; set; }
    public float C14 { get; set; }
    public float C50 { get; set; }
    public float C54 { get; set; }
    public float C58 { get; set; }
    public float C5C { get; set; }
    public int C64 { get; set; }

    /// <summary>V7/C1 <c>0xA4BDDC</c> <c>0xA67C58</c>/<c>0xA67B9C</c> re-init (unread seam).</summary>
    public Action<int>? ReinitHook { get; set; }

    /// <summary>V7/C1 <c>0xA4BEC4</c> <c>0xA5975C</c> per-connection conversion (unread seam).</summary>
    public Action<WwiseVoiceConnection>? Conversion5975C { get; set; }

    /// <summary>V7/C1 <c>0xA4BF4C</c> <c>0xA5D70C</c> per-connection step (unread seam).</summary>
    public Action<WwiseVoiceConnection>? Conversion5D70C { get; set; }

    /// <summary>The pan matrix the next <see cref="Refresh"/> applies (M6-012 gapE 2.1; caller-supplied).</summary>
    public float[]? TargetMatrix { get; set; }

    /// <summary>The composed target gain the next <see cref="Refresh"/> applies (M6-012 gapE 2.1).</summary>
    public float TargetGain { get; set; } = 1f;

    /// <summary>The first-update fade-in flag (M6-012 gapE 2.5).</summary>
    public bool FadeIn { get; set; }

    /// <summary>Creates a connection to <paramref name="bus"/> with the given input/output channel counts.</summary>
    public WwiseVoiceConnection(WwiseMixBus bus, int inputChannels, int outputChannels)
    {
        Bus = bus ?? throw new ArgumentNullException(nameof(bus));
        Mixer = new WwiseMixerConnection(inputChannels, outputChannels);
    }

    /// <summary>
    /// V8 step 3 <c>0xA56E00</c> (M6-012 gapE 2.5): promote the last end gain/matrix to the start and take
    /// the new target. <see cref="TargetMatrix"/> must be set; the identity diagonal is used when null.
    /// </summary>
    public void Refresh()
    {
        var matrix = TargetMatrix ?? IdentityMatrix();
        Mixer.Refresh(matrix, TargetGain, FadeIn);
    }

    private float[] IdentityMatrix()
    {
        var m = new float[Mixer.InputChannels * Mixer.OutputChannels];
        int diag = Math.Min(Mixer.InputChannels, Mixer.OutputChannels);
        for (int i = 0; i < diag; i++) m[i * Mixer.OutputChannels + i] = 1f;
        return m;
    }

    /// <summary>
    /// V8/V14 <c>0xA4FBEC(bus, params, conn, gains)</c>: zero-pad the voice buffer to the bus frame and
    /// accumulate the ramped mix into the bus buffer (M6-012). The native tests
    /// <c>[bus+0x1BC]==4</c> and <c>[bus+0x68]=0x2D</c> before the mixer; those are the bus lifetime's
    /// <see cref="WwiseMixBus.MixInput"/> state, which the caller sets.
    /// </summary>
    public void Mix(WwiseVoiceBuffer buffer)
    {
        Bus.MixInput();
        Mixer.ConsumeBuffer(buffer.Channels, new[] { Bus.Buffer }, buffer.ValidFrames, Bus.MaxFrames);
    }
}

/// <summary>
/// V7-d/e: one 0x4C-byte entry of the voice's send/connection table <c>[voice+0x10]</c> (C15 V7-d/V7-e).
/// The table's semantic identity is UNKNOWN; the fields the settled rows read are the per-target byte
/// <c>+0x44</c>, the send gain <c>+0x34</c> and the gathered value used by the <c>value &gt; 0</c> test.
/// </summary>
public sealed class WwiseVoiceSendEntry
{
    // fidelity: M6-022

    /// <summary>The gathered value; <c>0x9D4228</c> keeps only entries with <c>value &gt; 0</c>.</summary>
    public float Value { get; set; }

    /// <summary><c>+0x44</c>: the per-target byte copied from <c>[[r5+8]+0x22]</c>.</summary>
    public byte PerTargetByte { get; set; }

    /// <summary><c>+0x34</c>: the bus send gain <c>dBToLin([r5+0x58]*0.05)</c>.</summary>
    public float SendGain { get; set; } = 1f;
}

/// <summary>
/// V7-d <c>0xA4B93C</c>: the voice's send/connection table (array <c>[voice+0x10]</c>, count
/// <c>[voice+0x14]</c>, capacity <c>[voice+0x18]</c>, 0x4C-byte entries). The native allocates it lazily on
/// the first call; the sub-callees that gather the entries are RECOVERABLE_GAP seams.
/// </summary>
public sealed class WwiseVoiceSendTable
{
    // fidelity: M6-022

    /// <summary>The 0x4C-byte entries.</summary>
    public List<WwiseVoiceSendEntry> Entries { get; } = new();

    /// <summary><c>[voice+0x14]</c>: the used count (a caller input; the native reads it and does not derive
    /// it from the connection list <c>[voice+0x28]</c>, missing-bodies item 1.7).</summary>
    public int Count { get; set; }

    /// <summary><c>[voice+0x18]</c>: the array capacity used when the lazy allocation runs.</summary>
    public int Capacity { get; set; }

    /// <summary>The per-target byte copied into entry 0 (<c>[[r5+8]+0x22]</c>).</summary>
    public byte PerTargetByte { get; set; }

    /// <summary>The bus send gain copied into entry 0.</summary>
    public float SendGain { get; set; } = 1f;

    /// <summary>
    /// The native's lazy 0x4C-byte array allocation at <c>[voice+0x10]</c> (0xA4B9C8): it runs only when
    /// <c>[voice+0x14]==0</c>, sizing the array by the capacity <c>[voice+0x18]</c>. The writers of those two
    /// fields are not settled by the report, so both are caller inputs.
    /// </summary>
    public void AllocateLazily()
    {
        if (Count != 0) return;
        while (Entries.Count < Capacity) Entries.Add(new WwiseVoiceSendEntry());
    }
}

/// <summary>
/// V12 <c>0xA54A30</c> (C15 V12-vt): the 0x9C-byte voice insert-FX slot object. Its vtable is
/// <c>0x103DC38</c> when a plug-in is present and <c>0x103DB98</c> otherwise, from GOT
/// <c>0x1040174</c>/<c>0x1040170</c>. Slots: <c>+0x24 = 0xA52678</c>, <c>+0x28 = 0xA79858</c> (init),
/// <c>+0x2C = 0xA79DAC</c>, <c>+0x38 = 0xA79A2C</c>, <c>+0x3C = 0xA79A78</c>. The class name is UNKNOWN;
/// the <c>vt+0x38</c>/<c>vt+0x3C</c> bodies are the plug-in's, so they are caller seams.
/// </summary>
public sealed class WwiseVoiceInsertFxSlot
{
    // fidelity: M6-022

    /// <summary>The plugin-present vtable <c>0x103DC38</c> (C15 V12-vt).</summary>
    public const uint PluginVtable = 0x103DC38;

    /// <summary>The no-plugin vtable <c>0x103DB98</c> (C15 V12-vt).</summary>
    public const uint NoPluginVtable = 0x103DB98;

    /// <summary>True when the slot holds a plug-in (selects <see cref="PluginVtable"/>).</summary>
    public bool HasPlugin { get; set; }

    /// <summary>The stored vtable (<c>0x103DC38</c> or <c>0x103DB98</c>).</summary>
    public uint Vtable => HasPlugin ? PluginVtable : NoPluginVtable;

    /// <summary>True once <c>vt+0x28 = 0xA79858</c> ran.</summary>
    public bool Initialised { get; private set; }

    /// <summary><c>vt+0x28 = 0xA79858</c>: the slot init.</summary>
    public void Initialise() => Initialised = true;

    /// <summary><c>vt+0x2C = 0xA79DAC</c>: the slot teardown.</summary>
    public Action? TeardownHook { get; set; }

    /// <summary><c>vt+0x38 = 0xA79A2C</c> (V8 step 1, per-params execute); the body is the plug-in's.</summary>
    public Action<WwiseVoiceBuffer>? Execute38Hook { get; set; }

    /// <summary><c>vt+0x3C = 0xA79A78</c> (V8 step 1, the state 0x2D/0x11 path).</summary>
    public Action<WwiseVoiceBuffer>? Execute3CHook { get; set; }

    /// <summary>Runs <c>vt+0x38</c>.</summary>
    public void Execute38(WwiseVoiceBuffer buffer) => Execute38Hook?.Invoke(buffer);

    /// <summary>Runs <c>vt+0x3C</c>.</summary>
    public void Execute3C(WwiseVoiceBuffer buffer) => Execute3CHook?.Invoke(buffer);

    /// <summary>Runs <c>vt+0x2C</c>.</summary>
    public void Teardown() => TeardownHook?.Invoke();
}

/// <summary>
/// V18b <c>0xA4E974</c> (C15 V18b-vt): the bus insert-FX slot state at <c>bus + i*0x1C</c> (<c>i=0..3</c>).
/// Fields: <c>+0xC8</c> format/reset target, <c>+0xCC</c> Init arg, <c>+0xD4</c> descriptor, <c>+0xD8</c>
/// effect object (<c>vt+0x1C</c> Init, <c>vt+0xC</c> Reset, <c>vt+0x20</c> Execute), <c>+0xDC</c> the 0x24-byte
/// helper (vtable <c>0x103D538</c>), <c>+0xE0</c> flags, <c>+0x138</c> out buffer, <c>+0x13C</c> format,
/// <c>+0x140</c> state, <c>+0x144/+0x146</c> u16 counts, <c>+0x150</c> object. The effect object's class is
/// registry-assigned and UNKNOWN (<c>0x9CC2AC</c>, registry <c>0x108D9DC</c>), so it is a caller seam.
/// </summary>
public sealed class WwiseBusInsertFxSlotState
{
    // fidelity: M6-022

    /// <summary>The helper object's static vtable <c>0x103D538</c> (C15 V18b-vt).</summary>
    public const uint HelperVtable = 0x103D538;

    /// <summary><c>+0xC8</c>: the format/reset target.</summary>
    public object? FormatC8 { get; set; }

    /// <summary><c>+0xCC</c>: the Init arg (<c>effect-&gt;vt+0x1C</c>'s 4th argument).</summary>
    public int InitArgCC { get; set; }

    /// <summary><c>+0xD4</c>: the plug-in descriptor pointer (<c>-1</c> after drop).</summary>
    public long DescriptorD4 { get; set; } = -1;

    /// <summary><c>+0xD8</c>: the effect object; its class is registry-assigned and UNKNOWN.</summary>
    public object? EffectD8 { get; set; }

    /// <summary><c>+0xDC</c>: the 0x24-byte helper object (vtable <see cref="HelperVtable"/>).</summary>
    public object? HelperDC { get; set; }

    /// <summary><c>+0xE0</c>: flags; bit0 in-place, bit1 set from the per-slot object.</summary>
    public byte FlagsE0 { get; set; }

    /// <summary><c>+0x138</c>: the out-buffer pointer (null when in-place).</summary>
    public float[]? OutBuffer138 { get; set; }

    /// <summary><c>+0x13C</c>: the format word.</summary>
    public int Format13C { get; set; }

    /// <summary><c>+0x140</c>: the state (<c>0x11</c> not-in-place, <c>0x2B</c> after drop).</summary>
    public int State140 { get; set; }

    /// <summary><c>+0x144</c>/<c>+0x146</c>: the u16 count and the zero.</summary>
    public ushort Count144 { get; set; }
    public ushort Zero146 { get; set; }

    /// <summary><c>+0x150</c>: another object destroyed by the slot drop.</summary>
    public object? Object150 { get; set; }

    /// <summary>V18b <c>0x9CC2AC</c>/<c>0x9CC4D8</c>: the plug-in create/validate seam (registry UNKNOWN).</summary>
    public Func<object?, int, bool>? CreatePlugin { get; set; }

    /// <summary>V18b <c>effect-&gt;vt+0x1C</c> Init.</summary>
    public Func<bool>? InitHook { get; set; }

    /// <summary>V18b <c>effect-&gt;vt+0xC</c> Reset.</summary>
    public Func<bool>? ResetHook { get; set; }

    /// <summary>V18b <c>effect-&gt;vt+0x20</c> Execute/GetBuffer.</summary>
    public Action<int>? ExecuteHook { get; set; }
}

/// <summary>
/// V7-m <c>0xA550CC..0xA551F0</c> (C17 V7-m): one 16-byte parameter ramp record
/// <c>{float current @+0, float target @+4, u16 rate @+8, u8 flag @+0xb}</c>. The four live records sit at
/// <c>voice+0x340</c>, <c>voice+0x510</c>, <c>voice+0x350</c> and <c>voice+0x520</c>. The field names are
/// UNKNOWN (C17 residual); the offsets and the ramp arithmetic are the row's.
/// </summary>
public sealed class WwiseVoiceRamp
{
    // fidelity: M6-022

    /// <summary><c>+0</c>: the ramped/current value.</summary>
    public float Current { get; set; }

    /// <summary><c>+4</c>: the stored target the gate compares and the body overwrites.</summary>
    public float Target { get; set; }

    /// <summary><c>+8</c>: the u16 rate.</summary>
    public ushort Rate { get; set; }

    /// <summary><c>+0xb</c>: set to 1 by the ramp body.</summary>
    public byte Flag { get; set; }
}

/// <summary>
/// The voice node (M6-022 V6/V7/V8). The native container is based at <c>0x108DF50</c>, head
/// <c>[+0x14]</c>, next <c>+0xD0</c>, state <c>+0xDC</c>, active 1, bus chain <c>+0xD4</c>, pending
/// <c>+0xD8</c>. This model carries the fields the settled rows read; the per-voice state machine's
/// unread callees are seams (see <see cref="WwiseVoiceBusPass"/>).
/// </summary>
public sealed class WwiseLiveVoice
{
    // fidelity: M6-022

    /// <summary>The source (<c>+0xD4</c>).</summary>
    public IWwiseVoiceSource? Source { get; set; }

    /// <summary>The pending source (<c>+0xD8</c>), attached by the state-0x11 tail (V15).</summary>
    public IWwiseVoiceSource? Pending { get; set; }

    /// <summary>The per-voice buffer (the native <c>params</c>).</summary>
    public WwiseVoiceBuffer Buffer { get; }

    /// <summary>Filter A (<c>+0x1C0</c>, M6-011): runs before the aux sends.</summary>
    public WwiseVoiceFilter FilterA { get; }

    /// <summary>Filter B (<c>+0x390</c>, M6-011): dry path only.</summary>
    public WwiseVoiceFilter FilterB { get; }

    /// <summary>The voice-stage resampler (<c>+0x100</c>, M6-004).</summary>
    public WwiseResampler Resampler { get; } = new();

    /// <summary>The connection list (<c>+0x28</c>).</summary>
    public List<WwiseVoiceConnection> Connections { get; } = new();

    /// <summary>The voice output gain (<c>+0x1C</c>), applied to the mixed signal.</summary>
    public float OutputGain { get; set; } = 1f;

    /// <summary>The voice output dB (<c>+0x20</c>), computed by C11's <c>0xA4AF50</c>.</summary>
    public float OutputDb { get; set; }

    /// <summary>The native state byte (<c>+0xDC</c>): 1 active, 2 stopped (V6/V16).</summary>
    public int State { get; set; } = 1;

    /// <summary>The native <c>+0xE0</c> (the next-source index; C12 voice-callees Q4).</summary>
    public int E0 { get; set; }

    /// <summary>The native <c>+0xE4</c> (the next-source request/result code; C12 voice-callees Q4).</summary>
    public int E4 { get; set; }

    /// <summary>The native <c>+0xCD</c> bit flags (A/format/fade state; C12 voice-callees Q4).</summary>
    public byte FlagsCD { get; set; }

    /// <summary>The native <c>+0xE8</c> bit 0 (C12 voice-callees Q4).</summary>
    public bool FlagE8 { get; set; }

    /// <summary>
    /// The native <c>+0xCC</c>: the send-count byte <c>0x9D4228</c> reads on entry and writes back with the
    /// gathered count (missing-bodies item 1.5 step 4/6; <c>0xA4BA7C add r3,r4,#0xcc</c>,
    /// <c>0xA4BA90 bl 0x9D4228</c>). It is not <c>[voice+0x14]</c>.
    /// </summary>
    public byte CountCC { get; set; }

    /// <summary>The voice id (<c>+0xF0</c>).</summary>
    public uint Id { get; set; }

    /// <summary>V7-m ramp 1: the 16-byte record at <c>voice+0x340</c> (current/target/rate/flag).</summary>
    public WwiseVoiceRamp Ramp340 { get; } = new();

    /// <summary>V7-m ramp 2: the 16-byte record at <c>voice+0x510</c>.</summary>
    public WwiseVoiceRamp Ramp510 { get; } = new();

    /// <summary>V7-m ramp 3: the 16-byte record at <c>voice+0x350</c>.</summary>
    public WwiseVoiceRamp Ramp350 { get; } = new();

    /// <summary>V7-m ramp 4: the 16-byte record at <c>voice+0x520</c>.</summary>
    public WwiseVoiceRamp Ramp520 { get; } = new();

    /// <summary>
    /// V7-p <c>[sp+0x2e]</c> (C18 V7-p, <c>0xA4BCB4..0xA4BCDC</c>, <c>0xA4BD08..0xA4BD20</c>,
    /// <c>0xA4BFEC..0xA4BFF0</c>): the <c>0xA4BC58</c> <c>param_6</c> output that gates the ramp-target copy.
    /// It is 0 iff the connection list is non-empty, some connection has <c>[conn+0x6C]&amp;2 == 0</c>, and
    /// <c>voice-&gt;vt+0x3C</c> returns 0; otherwise 1. When it is non-zero the four ramp targets are the
    /// voice target fields (<c>+0x344/+0x514/+0x354/+0x524</c>); when it is zero they are the four
    /// <see cref="OutputMin50"/> minima. It is written by every <see cref="WwiseVoiceBusPass.UpdateConnectionGains"/>
    /// call, including the second call at <c>0xA5572C</c> (C18 V7-q).
    /// </summary>
    public bool Run2E { get; set; }

    /// <summary>
    /// V7-p/V7-q <c>[sp+0x2f]</c> (C18 V7-p/V7-q): the <c>0xA4BC58</c> <c>param_7</c> output, also returned by
    /// <see cref="WwiseVoiceBusPass.UpdateConnectionGains"/>. The second call at <c>0xA5572C</c> rewrites it
    /// after the ramps have run (C18 V7-q), so it is a voice field rather than a local.
    /// </summary>
    public bool P2F { get; set; }

    /// <summary>Creates a voice with a <paramref name="channels"/>-channel, <paramref name="maxFrames"/>-frame buffer.</summary>
    public WwiseLiveVoice(int channels, int maxFrames)
    {
        Buffer = new WwiseVoiceBuffer(channels, maxFrames);
        FilterA = new WwiseVoiceFilter(WwiseVoiceFilterRole.A);
        FilterB = new WwiseVoiceFilter(WwiseVoiceFilterRole.B);
    }

    /// <summary>
    /// V8 <c>0xA44630</c>: the render + mix dispatcher order, which is settled:
    /// <list type="number">
    /// <item>insert-FX slots <c>vt+0x38</c> 4..1 then <c>vt+0x3c</c> on state 0x2D/0x11 (V12) â€” the slot
    /// class identity is RECOVERABLE_GAP, so the slots are a seam (<see cref="InsertFx"/>, caller-supplied);</item>
    /// <item>filter A <c>0xA4C60C(voice+0x1C0)</c>;</item>
    /// <item>gain/ramp <c>0xA56E00(voice+0x380)</c> (per connection, M6-012);</item>
    /// <item>source execute <c>0xA548C0</c> (<see cref="IWwiseVoiceSource.Render"/>);</item>
    /// <item>on 0x11/0x2D pitch <c>0xA53134</c> and resampler execute <c>0xA52D4C</c> (M6-004);</item>
    /// <item>notify <c>0xA03E8C</c> (a caller callback);</item>
    /// <item>aux-send walk <c>0xA4FBEC</c> per aux connection;</item>
    /// <item>filter B <c>0xA4C60C(voice+0x390)</c> before the first dry mix;</item>
    /// <item>dry-mix walk <c>0xA4FBEC</c> with gain 1.0.</item>
    /// </list>
    /// The native's <c>0xA54A30</c> start/insert-FX build is not read in full; the source is started by the
    /// caller through <see cref="IWwiseVoiceSource.StartStream"/>.
    /// </summary>
    /// <param name="notify">The V8 step 7 <c>0xA03E8C(manager, params)</c> listener notification; caller seam.</param>
    public void Render(Action<WwiseLiveVoice>? notify = null)
    {
        InsertFx?.Invoke(this);                                  // V8 step 1 (extra caller hook)
        // V8 step 1: insert-FX slots vt+0x38 from 4..1, then vt+0x3c on state 0x2D/0x11 (C12 voice-callees Q5).
        for (int i = 3; i >= 0; i--) InsertFxSlots[i]?.Execute38(Buffer);
        for (int i = 0; i < 4; i++) InsertFxSlots[i]?.Execute3C(Buffer);

        FilterA.Process(Buffer.Channels[0]);                     // V8 step 2: filter A (M6-011)

        // V8 step 3: gain/ramp 0xA56E00(voice+0x380) refreshes each connection's ramp (M6-012). The exact
        // composed gain is gapC; the caller supplies each connection's target matrix/gain.
        foreach (var connection in Connections)
            connection.Refresh();

        // V8 step 4: source execute 0xA548C0 -> source vt+0x30. The decoder is M6-002/M6-003.
        Buffer.ValidFrames = Buffer.MaxFrames;                   // V8: params+0xC = 0x400 (C12 X4)
        if (Source is not null)
            Buffer.Result = Source.Render(Buffer);

        // V8 step 6: on 0x11/0x2D pitch 0xA53134 and resampler 0xA52D4C (M6-004). The resampler's output
        // feeds the same buffer; only the mono int16/float kernels are read, so a stereo source is refused.
        if (Buffer.Result is 0x11 or 0x2D && Source is not null && Source.Channels == 1)
            ApplyResampler();

        notify?.Invoke(this);                                    // V8 step 7: 0xA03E8C

        // V8 step 7: the aux-send walk 0xA4FBEC for the connections whose conn+0x68 is set, skipping a
        // connection with ([conn+0x6C]&6)==6.
        foreach (var connection in Connections)
        {
            if (!connection.HasAux) continue;
            if ((connection.Flags6C & 6) == 6) continue;
            connection.Mix(Buffer);
        }

        // V8 step 8: filter B 0xA4C60C(voice+0x390) before the first dry mix, then the dry-mix walk with
        // gain 1.0.
        bool firstDry = true;
        foreach (var connection in Connections)
        {
            if (!connection.HasDry) continue;
            if (firstDry)
            {
                FilterB.Process(Buffer.Channels[0]);             // V8 step 8: filter B before the first dry mix
                firstDry = false;
            }
            connection.Mix(Buffer);                              // V8 step 8: dry mix, gain 1.0
        }
    }

    /// <summary>
    /// V8 step 6 <c>0xA53134</c>/<c>0xA52D4C</c> (M6-004): the voice-stage resampler converts the source
    /// rate to the mix rate. Only the mono int16 kernel is read, so this runs only for a mono source and
    /// otherwise leaves the buffer untouched (a stereo int16 kernel is not read, M6-004).
    /// </summary>
    private void ApplyResampler()
    {
        if (Source!.SampleRate == WwiseRuntimeSettings.MixRateHz)
            return;                                              // ratio 1: no conversion needed
        throw new NotSupportedException(
            "M6-022 V8 step 6 (0xA53134/0xA52D4C): the voice-stage resampler from a non-mix-rate source is " +
            "M6-004's work; only the mono int16 kernel is read, so a rate change is not invented here.");
    }

    /// <summary>
    /// V8 step 1: the insert-FX slot callback. The slot class identity is RECOVERABLE_GAP (V12/V18b), so the
    /// slots are caller-supplied rather than modelled here.
    /// </summary>
    public Action<WwiseLiveVoice>? InsertFx { get; set; }

    /// <summary>V7: the first connection's bus (the native <c>[source+0xC]</c> chain); null when unconnected.</summary>
    public WwiseMixBus? PrimaryBus => Connections.Count > 0 ? Connections[0].Bus : null;

    // ---------------------------------------------------------------- V7 vtable seams
    //
    // 0xA54F1C calls these voice/bus/filter vtable slots. Their bodies are per-class and not read
    // (RECOVERABLE_GAP / UNKNOWN class identity), so they are caller seams. The defaults keep the ordinary
    // render path running; they are not recovered values.

    /// <summary>V7 <c>voice-&gt;vt+0x3C</c>: the per-voice source request; default 1 (live).</summary>
    public Func<int>? VoiceRequest3C { get; set; }

    /// <summary>V7 <c>voice-&gt;vt+0x48</c>: the stop/fail path.</summary>
    public Action? VoiceStop48 { get; set; }

    /// <summary>V7 <c>voice-&gt;vt+0x58</c>: called at <c>0xA554D8</c> on the E8 return-1 path; its return is
    /// discarded (the bit2 value is the saved <c>bus-&gt;vt+0x3C</c> return).</summary>
    public Func<bool>? VoiceBit58 { get; set; }

    /// <summary>V7 <c>[voice+0x1C4]</c>: the downstream node the <c>0xA4C620</c> wrapper forwards to. The wrapper
    /// vtable is <c>0x103C120</c> (<c>[0x104017C]+8</c>), slot <c>+0x18</c> = <c>0xA4C620</c> (C16 V7-h).</summary>
    public object? FilterInner1C4 { get; set; }

    /// <summary>V7 <c>[[voice+0x1C4]]-&gt;vt+0x18(inner, E0, r2)</c>: the downstream node's slot (C16 V7-h).</summary>
    public Func<int, int, int>? FilterInner18 { get; set; }

    /// <summary>V7 <c>voice+0x1C0-&gt;vt+0x14(E0)</c>.</summary>
    public Func<int, int>? FilterRequest14 { get; set; }

    /// <summary>V7 <c>voice+0x1C0-&gt;vt+0x10(&amp;s)</c>; its return is stored in <c>params+0x28</c>.</summary>
    public Func<int, int>? FilterRequest10 { get; set; }

    /// <summary>V7 <c>voice+0x1C0-&gt;vt+0xC</c>.</summary>
    public Action? FilterRequest0C { get; set; }

    /// <summary>V7 <c>0xA56650(source, [bus+0x1DC], [bus+0x1E0])</c>: 1 already set, else start; default 0.</summary>
    public Func<int>? StartSource56650 { get; set; }

    /// <summary>V7 <c>0xA4B93C(voice)</c>; see <see cref="WwiseVoiceBusPass.InitSendTable"/>.</summary>
    public Action? InitSendTable { get; set; }

    /// <summary>V7 <c>[voice+0x1B4]</c>: when non-zero the insert-FX/start build is skipped.</summary>
    public bool Has1B4 { get; set; }

    /// <summary>V7-f: the four insert-FX slots <c>voice+0x370..0x37C</c> (V12).</summary>
    public WwiseVoiceInsertFxSlot[] InsertFxSlots { get; } = new WwiseVoiceInsertFxSlot[4];

    /// <summary>V7-f <c>0xA5321C(voice+0x100,...)</c>: the resampler/pitch start; returns true on 1.</summary>
    public Func<bool>? StartResampler5321C { get; set; }

    /// <summary>V7-f <c>0xA019B8(bus,i,...)</c>: resolves the bus's insert-FX slot candidate; UNKNOWN registry.</summary>
    public Func<int, object?>? ResolveBusSlot { get; set; }

    /// <summary>V7-f <c>0x9CC2AC</c>/<c>0x9CC4D8</c>: the plug-in create/validate (registry-assigned, UNKNOWN).</summary>
    public Func<object, int, bool>? CreatePlugin { get; set; }

    /// <summary>V7-f <c>0xA5676C(voice+0x380, bus)</c>: the gain/ramp init; M6-012.</summary>
    public Action? InitGain { get; set; }

    /// <summary>V7-f <c>voice-&gt;vt+0x6C</c>: the voice start hook.</summary>
    public Action? VoiceStart6C { get; set; }

    /// <summary>V7 <c>source-&gt;vt+0x4C</c>: the per-source request; class identity UNKNOWN.</summary>
    public Action<WwiseVoiceBuffer>? SourceRequest4C { get; set; }

    /// <summary>V7 <c>0xA4B93C</c> sub-callee <c>0x9BDA88(r5)</c>; RECOVERABLE_GAP, returns true to continue.</summary>
    public Func<bool>? SendTableContinue9BDA88 { get; set; }

    /// <summary>V7 <c>0xA4B93C</c> sub-callee <c>0x9BD368(r5, sp+0x10)</c>; RECOVERABLE_GAP.</summary>
    public Action? SendTableGather9BD368 { get; set; }

    /// <summary>V7 <c>0xA4B4B0(voice)</c> ducking apply; see <see cref="WwiseVoiceBusPass.ApplyDucking"/>.</summary>
    public Action? ApplyDuckingHook { get; set; }

    /// <summary>V7-d/e: the send/connection table (<c>[voice+0x10]</c>).</summary>
    public WwiseVoiceSendTable? SendTable { get; set; }

    /// <summary>V7-e <c>0x9D4108</c>: the per-entry dispatch; the registry/object identity is UNKNOWN.</summary>
    public Action<WwiseVoiceSendEntry>? DispatchEntryHook { get; set; }

    /// <summary>V7/C1 <c>0xA4BC58</c> the four output-float minima <c>[sp+0x5c..0x68]</c>.</summary>
    public float[] OutputMin50 { get; } = new float[4];

    /// <summary>V7/C1 <c>0xA4BC58</c> <c>r8</c>: the low byte of the <c>source-&gt;vt+0x4C</c> return. The built
    /// <see cref="SourceRequest4C"/> seam is void, so this is a caller input.</summary>
    public int SourceGain8Low { get; set; }
}

/// <summary>
/// The per-voice and per-bus pass bodies (M6-022 V5..V20). The engine skeleton's
/// <see cref="IWwiseVoiceBusPass"/> is kept; this is the concrete implementation.
///
/// <para><b>Settled and modelled.</b> The V5 pre-pass call order (0x9D3CC0, 0xA43D24, 0xA39564), the V6
/// voice-list walk, the V8 render dispatcher order, the V14 voice-&gt;bus mix, the V17 bus pass last-to-first
/// walk with the V18 output branch, and the V20 idle removal. The DSP bodies are composed from the existing
/// modules (<see cref="WwiseVoiceFilter"/>, <see cref="WwiseResampler"/>, <see cref="WwiseMixerConnection"/>,
/// <see cref="WwiseMixBus"/>, the Hijack), not re-implemented.</para>
///
/// <para><b>Explicit gaps, not silent defaults.</b>
/// <list type="bullet">
/// <item><b>The bus metering stage <c>0xA50044..0xA50FD0</c> (C12 X1).</b> The bus-output tail is level
/// analysis, not mixing; its DSP identity is RECOVERABLE_GAP. <see cref="WwiseBusMetering"/> throws rather
/// than inventing a kernel.</item>
/// <item><b>The per-voice state machine <c>0xA54F1C</c> (V7).</b> Its branch table is settled but its callees
/// (<c>0xA4C584</c>, <c>0xA022E8</c>, <c>0xA0228C</c>, <c>0xA01768</c>, <c>0xA4B93C</c>, <c>0x9D4228</c>,
/// <c>0xA54A30</c>'s insert-FX build) are RECOVERABLE_GAP. <see cref="RunVoiceStateMachine"/> exposes the
/// settled gates and throws on an unread callee path.</item>
/// <item><b>The insert-FX slot class identity (V12/V18b)</b> is RECOVERABLE_GAP; slots are caller-supplied.</item>
/// <item><b>The bus mix kernel <c>0xA4F9E0</c>/<c>0xA45E9C</c></b> is M6-012's <see cref="WwiseMixerConnection"/>.</item>
/// </list></para>
/// </summary>
public sealed class WwiseVoiceBusPass : IWwiseVoiceBusPass
{
    // fidelity: M6-022

    private readonly WwiseMixBusHierarchy _buses;
    private readonly WwiseOutputDeviceState _deviceState;
    private readonly WwiseUpdateBuffer? _updateBuffer;
    private readonly Action<WwiseLiveVoice>? _notify;

    /// <summary>
    /// The <c>sp+0x40</c> sink of the second <c>0xA4BC58</c> call at <c>0xA5572C</c> (C18 V7-q): the four
    /// float outputs are written there and never read, so a scratch keeps them off
    /// <see cref="WwiseLiveVoice.OutputMin50"/> while the call still updates
    /// <see cref="WwiseLiveVoice.Run2E"/>/<see cref="WwiseLiveVoice.P2F"/>.
    /// </summary>
    private readonly float[] _sp40 = new float[4];

    /// <summary>The voice list (the native container head <c>0x108DF64</c>).</summary>
    public List<WwiseLiveVoice> Voices { get; } = new();

    /// <summary>The deferred PBI-notification queue (<c>0x108DE7C</c>; V21).</summary>
    public Queue<WwisePbiNotification> PbiNotifications { get; } = new();

    /// <param name="buses">The on-demand mix-bus table (M6-014).</param>
    /// <param name="deviceState">The output-device state/gates (M6-022 G1..G10).</param>
    /// <param name="updateBuffer">The Hijack chunk sink (M6-015/M6-014 D2.9), or null.</param>
    /// <param name="notify">The V8 step 7 listener notification seam.</param>
    public WwiseVoiceBusPass(
        WwiseMixBusHierarchy buses,
        WwiseOutputDeviceState deviceState,
        WwiseUpdateBuffer? updateBuffer = null,
        Action<WwiseLiveVoice>? notify = null)
    {
        _buses = buses ?? throw new ArgumentNullException(nameof(buses));
        _deviceState = deviceState ?? throw new ArgumentNullException(nameof(deviceState));
        _updateBuffer = updateBuffer;
        _notify = notify;
    }

    /// <summary>How many voices the voice pass rendered.</summary>
    public int VoicesRendered { get; private set; }

    /// <summary>
    /// V5 pre-pass then the V6..V16 voice walk.
    /// <list type="number">
    /// <item><b>0x9D3CC0</b> advances bus/source tick counters (V5, C12); the tick list is a caller input.</item>
    /// <item><b>0xA43D24</b> the ducking/volume pre-pass (V5a): <c>0xA55750</c> per active voice, the per-bus
    /// <c>+0x88/+0x8C</c> dB/linear, <c>0xA4AF50</c> per voice, <c>0xA437E0</c> flagged buses descending,
    /// <c>0xA4B4B0</c> per voice. Those callees are unread; this models the call order with seams.</item>
    /// <item><b>0xA39564</b> the node cleanup (V5b): clear bit 2 of <c>[node+0x1BE]</c> on the list
    /// <c>0x108DEC8</c>, optionally <c>0xA00494</c> per node, then <c>0x9F3BA4</c> per array element. The
    /// node/array objects are a caller seam.</item>
    /// </list>
    /// </summary>
    public void VoicePass()
    {
        // V5 0x9D3CC0: the bus/source tick advance. The tick list object is a caller input (C12).
        AdvanceTickCounters?.Invoke();

        // V5a 0xA43D24: the ducking/volume pre-pass order. Its unread callees are seams.
        DuckPrePass?.Invoke();

        // V5b 0xA39564: the node cleanup order. The node objects are a caller seam.
        NodeCleanup?.Invoke();

        VoicesRendered = 0;
        foreach (var voice in Voices)                                // V6: head [0x108DF64], next +0xD0
        {
            if (voice.State != 1) continue;                          // V6: active 1
            if (!RunVoiceStateMachine(voice)) continue;              // V7: returns 1 when the voice has a live source
            voice.Render(_notify);                                   // V8: 0xA44630
            VoicesRendered++;
        }
    }

    /// <summary>V5a: <c>0xA43D24</c>'s unread per-bus/voice callees; caller seam.</summary>
    public Action? DuckPrePass { get; set; }

    /// <summary>V5b: <c>0xA39564</c>'s node/array objects; caller seam.</summary>
    public Action? NodeCleanup { get; set; }

    /// <summary>V5: <c>0x9D3CC0</c>'s tick list; caller seam.</summary>
    public Action? AdvanceTickCounters { get; set; }

    /// <summary>
    /// V7 <c>0xA54F1C</c>: the per-voice parameter/state machine, transliterated from C12 voice-callees Q4
    /// (settled branch table) and C17 V7-j..V7-m. The settled gates are <c>E4=[voice+0xE4]</c>,
    /// <c>E0=[voice+0xE0]</c>, <c>A=[voice+0xCD]&amp;1</c>, <c>E8=[voice+0xE8]&amp;1</c>,
    /// <c>SRC10=[source+0x10]&amp;1</c>; the named callees are <see cref="SetConnectionBit2"/> (<c>0xA4C584</c>),
    /// <see cref="ReleaseBusRef"/> / <see cref="AcquireBusRef"/> (<c>0xA022E8</c>/<c>0xA0228C</c>),
    /// <see cref="NextSource"/> (<c>0xA01768</c>), <see cref="InitSendTable"/> (<c>0xA4B93C</c>),
    /// <see cref="GatherAndDispatch"/> (<c>0x9D4228</c>) and
    /// <see cref="StartStreamAndBuildInsertFx"/> (<c>0xA54A30</c>).
    ///
    /// <para><b>Order.</b> The <c>P2F!=0</c> path runs the four parameter ramps (<see cref="RunParameterRamps"/>,
    /// <c>0xA550CC..0xA551F0</c>) then the <c>0xA551F0</c> branch table; the <c>P2F==0</c> path enters
    /// <c>0xA5532C</c>. Every sub-branch of either path joins the common continuation at <c>0xA5521C</c>
    /// (E8 gate <c>0xA553A4</c> -&gt; budget step <c>0xA55228</c> -&gt; dispatch <c>0xA55248</c> -&gt; bit0
    /// write and <c>0xA555B8</c> detour <c>0xA5526C</c> -&gt; tail <c>0xA5528C</c>). The <c>0xA5530C</c>
    /// epilogue (<c>voice-&gt;vt+0x48</c>, return 0, no tail) is the only early exit.</para>
    ///
    /// <para><b>Seams.</b> The voice/bus/filter vtable slots (<c>voice-&gt;vt+0x3C/0x48/0x58</c>,
    /// <c>bus-&gt;vt+0x3C/0x24/0x28</c>, <c>voice+0x1C0-&gt;vt+0x18/0x14/0x10/0xC</c>, <c>0xA56650</c>) are
    /// per-class and not read (class identity UNKNOWN), so they are caller inputs. The RECOVERABLE_GAP
    /// sub-callees inside <c>0xA4B93C</c> (<c>0x9BE28C</c>, <c>0x9BDA88</c>, <c>0x9BD368</c>,
    /// <c>0x9BF8E4</c>, <c>0xA5E694</c>) and <c>0x9EEDA4</c> inside <c>0xA01768</c> are seams and do not
    /// throw on the ordinary render path.</para>
    /// </summary>
    /// <returns>True when the voice has a live source and the V8 dispatcher should run.</returns>
    public bool RunVoiceStateMachine(WwiseLiveVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var source = voice.Source;
        if (source is null) return false;
        var bus = voice.PrimaryBus;
        if (bus is null) return false;

        // Prologue 0xA54F20..0xA54F4C: [bus+0x1F8].
        if (bus.NextSourceParam != -1)
        {
            voice.Buffer.HasBusParam = true;                         // [params+0x2C] = 1
            if (bus.NextSourceParam == 0) return false;              // 0xA5531C
        }

        // 0xA54F50..0xA54FD0: the V7 prologue dB gain ([bus+0x54]+[bus+0x11C])*0.05, clamped at -37, then
        // the [source+8] factors.
        float gain = VoiceGain(voice, bus, source);

        voice.SourceRequest4C?.Invoke(voice.Buffer);                 // 0xA55008 source->vt+0x4C
        bool p2f = UpdateConnectionGains(voice, bus, gain);          // 0xA55058 0xA4BC58 -> [sp+0x2f]

        // 0xA55090..0xA550C4: the sample count s16 = round([params+0xC] * [bus+0x164]). It is read by the
        // P2F==0 path's 0xA5556C branch and again by the budget step 0xA55228 (C16 V7-i).
        int s = (int)MathF.Round(voice.Buffer.ValidFrames * bus.SampleScale164);

        // 0xA550CC..0xA551F0: the four parameter ramps run only on the P2F!=0 path and only when the
        // connection list [voice+0x28] is non-empty (C17 V7-m). They run before the 0xA551F0 branch table.
        if (p2f && voice.Connections.Count != 0)
            RunParameterRamps(voice, bus);

        bool r5;
        bool stopped = false;
        if (!p2f)
        {
            // 0xA550C8 -> 0xA5532C: the P2F==0 path (C17 V7-k). Its sub-branches set r5 and join the common
            // continuation at 0xA5521C; only 0xA5530C is an early return.
            r5 = Path5532C(voice, bus, s);
        }
        else
        {
            bool a = (voice.FlagsCD & 1) != 0;
            int e4 = voice.E4;
            bool src10 = source.StartStreamSucceeded;

            // 0xA551F0..0xA552EC: the branch table (C12 voice-callees Q4 / C16 V7-g).
            if (a)
            {
                // 0xA55200
                if (src10) r5 = true;                                    // -> 0xA55218
                else if (e4 == 2) r5 = Path554F0(voice, bus, ref p2f);   // -> 0xA554F0
                else r5 = true;                                          // -> 0xA55218
            }
            else
            {
                // 0xA552B0
                if (e4 != 2) r5 = true;                                  // -> 0xA55218
                else if (!src10) r5 = Path554F0(voice, bus, ref p2f);    // -> 0xA554F0
                else r5 = Path552C8(voice, bus, ref p2f, out stopped);   // -> 0xA552C8
            }

            if (stopped) return false;                               // 0xA5530C epilogue (no shared tail)
        }

        // 0xA5521C: the E8 gate; the only gate before the budget step 0xA55228.
        if (voice.FlagE8)
        {
            int r = bus.SourceRequest3C?.Invoke(voice.E0) ?? 1;      // 0xA553A4/0xA553B4
            if (r == 1)
            {
                // 0xA554CC/0xA554D0: the bus->vt+0x3C return is saved to [sp+0x24] before the call.
                // 0xA554D8 calls voice->vt+0x58 and discards its return; 0xA554E0/0xA554E4 pass the saved
                // bus return as r1, so bit2 comes from r, not from voice->vt+0x58.
                voice.VoiceBit58?.Invoke();                          // 0xA554D8, return discarded
                SetConnectionBit2(voice, (r & 1) != 0);              // 0xA554E8 0xA4C584(voice, r1)
                voice.FlagE8 = false;                                // 0xA553C8 clear bit0
            }
            else if (r == 2)
            {
                voice.VoiceStop48?.Invoke();                         // 0xA555A0
                r5 = false;
                voice.FlagE8 = false;                                // 0xA555B4 -> 0xA553C8 clear bit0
            }
            else
            {
                voice.FlagE8 = false;                                // 0xA553C8
            }
        }

        // 0xA55228: the budget step. There is no gate on [bus+0x164]; s may be 0 and the step still runs
        // (C16 V7-i).
        int budget = bus.FrameBudget;
        if (budget >= s) r5 = false;                                 // 0xA55234 sets r5=0
        if (budget >= 0) bus.FrameBudget = budget - s;               // 0xA5523C..0xA55244

        // 0xA55248..0xA55268: the acquire/release pair, gated on E4/A/P2F.
        if (voice.E4 == 0)
        {
            // 0xA5526C.
        }
        else if ((voice.FlagsCD & 1) == 0)
        {
            if (p2f) ReleaseBusRef(bus);                             // 0xA55384 -> 0xA55398 0xA022E8(bus,1)
        }
        else if (!p2f)
        {
            AcquireBusRef(bus);                                      // 0xA5547C -> 0xA55484 0xA0228C(bus)
        }

        // 0xA5526C: bit0 = P2F; then the insert-FX/start path when r5 and [voice+0x1B4]==0.
        voice.FlagsCD = (byte)((voice.FlagsCD & ~1) | (p2f ? 1 : 0)); // bfi r3,r2,#0,#1
        if (r5 && !voice.Has1B4)
        {
            int r = StartStreamAndBuildInsertFx(voice);              // 0xA555B8 -> 0xA54A30
            if (r == 1)
            {
                // 0xA555F0: the state-0x11 tail.
                if ((bus.FlagsE8 & 0x20) != 0 && (bus.FlagsE9 & 1) != 0)
                {
                    bus.BusStart28?.Invoke();                        // [[bus+0xC]]->vt+0x28 on bus+0xC
                    bus.C4 = 101f;                                   // 0x42CA0000
                    voice.Buffer.Result = (int)voice.Id;             // [params+4] = [voice+0xF0]
                    voice.FlagsCD = (byte)(voice.FlagsCD & ~8);      // clear bit3
                    ApplyDucking(voice, bus);                        // 0xA4B4B0
                    // 0xA5572C (C18 V7-q): the second 0xA4BC58 call passes the same &sp+0x2e/&sp+0x2f but
                    // its four float outputs are sp+0x40, so they are discarded and only the flags are
                    // rewritten after the ramps have run.
                    UpdateConnectionGains(voice, bus, VoiceGain(voice, bus, source), _sp40);  // 0xA5572C 0xA4BC58
                }
                else if ((bus.FlagsE8 & 0x20) == 0)
                {
                    bus.BusStop24?.Invoke();                         // 0xA5573C [[bus+0xC]]->vt+0x24
                }
            }
            else
            {
                voice.VoiceStop48?.Invoke();                         // 0xA5528C? the r!=1 path
            }
        }

        voice.FlagsCD = (byte)(voice.FlagsCD | 8);                   // 0xA5528C: [voice+0xCD] |= 8
        return r5;
    }

    /// <summary>
    /// V7 prologue <c>0xA54F50..0xA54FD0</c> (C12 voice-callees Q4): <c>s=([bus+0x54]+[bus+0x11C])*0.05</c>,
    /// clamped at -37 with the fast-pow linearisation, then scaled by <c>[[source+8]+4]</c> and, when
    /// <c>[bus+0x58]</c> bit0 is set, by <c>[source+8]</c>.
    /// </summary>
    private static float VoiceGain(WwiseLiveVoice voice, WwiseMixBus bus, IWwiseVoiceSource source)
    {
        float lin = WwiseGain.DbToLinear(bus.Gain54 + bus.Gain11C);   // 0x3D4CCCCD / 0xC2140000 / fast pow
        if (source.Gain8 is { } g)
        {
            lin *= g.At4;                                            // 0xA54FD8/0xA54FE4
            if ((bus.Gain58 & 1) != 0) lin *= g.At0;                 // 0xA54FE8/0xA54FEC
        }
        return lin;
    }

    /// <summary>
    /// V7 <c>0xA552C8</c>: <c>E4==2, SRC10 set, A==0</c>. A <c>bus-&gt;vt+0x3C</c> return of 2 short-circuits
    /// to the stop and never reaches the <c>0xA552F0</c> wrapper; otherwise the wrapper is called with
    /// <c>r2 = 1</c> (return 1) or <c>0</c> (fall-through), and only a wrapper return of 1 reaches
    /// <c>0xA55218</c> (C16 V7-g).
    /// </summary>
    private static bool Path552C8(WwiseLiveVoice voice, WwiseMixBus bus, ref bool p2f, out bool stopped)
    {
        stopped = false;
        int r = bus.SourceRequest3C?.Invoke(voice.E0) ?? 1;           // 0xA552D8
        if (r == 2) { voice.VoiceStop48?.Invoke(); stopped = true; return false; }   // 0xA5530C
        int r2 = r == 1 ? 1 : 0;                                     // 0xA555E8 mov r2,r0 / 0xA552EC mov r2,r5
        int f = Wrapper18(voice, voice.E0, r2);                      // 0xA552F0 [voice+0x1C0]->vt+0x18
        if (f == 1) return true;                                     // 1 -> 0xA55218
        voice.VoiceStop48?.Invoke();                                 // 0xA5530C
        stopped = true;
        return false;
    }

    /// <summary>
    /// V7 <c>0xA5532C</c> (C17 V7-k): the <c>P2F==0</c> branch reached from <c>0xA550C8</c>. The settled
    /// branches, each ending at the common continuation <c>0xA5521C</c> (never straight at the tail):
    /// <list type="bullet">
    /// <item><c>E4==2</c>, <c>A=[voice+0xCD]&amp;1</c> set: call <c>voice+0x1C0-&gt;vt+0x14(E0)</c>; when
    /// <c>[voice+0xE0]==2</c> -> <c>0xA554A4</c> (<c>r5=0</c>), else call <c>voice+0x1C0-&gt;vt+0xC</c> and
    /// fall into <c>0xA55498</c>;</item>
    /// <item><c>0xA55498</c> re-checks <c>[voice+0xE0]==1</c>: when set, if <c>[bus+0x1D8] &lt; s</c> call
    /// <c>voice+0x1C0-&gt;vt+0x10(&amp;s)</c> and store the return in <c>params+0x28</c>; then <c>r5=0</c>;</item>
    /// <item><c>E4==1</c>: <c>voice-&gt;vt+0x48</c>; <c>r5=0</c>;</item>
    /// <item>anything else continues at <c>0xA55218</c> with <c>r5=1</c>.</item>
    /// </list>
    /// </summary>
    /// <param name="voice">The voice.</param>
    /// <param name="bus">The voice's bus (<c>r6</c> in the native).</param>
    /// <param name="s">The <c>0xA55090</c> sample count.</param>
    /// <returns>The <c>r5</c> flag the caller carries into the common continuation (<c>0xA5521C</c>).</returns>
    private static bool Path5532C(WwiseLiveVoice voice, WwiseMixBus bus, int s)
    {
        if (voice.E4 == 2)
        {
            if ((voice.FlagsCD & 1) != 0)
            {
                voice.FilterRequest14?.Invoke(voice.E0);             // 0xA55544/48
                if (voice.E0 == 2) return false;                     // 0xA55554 -> 0xA554A4 (r5=0)
                voice.FilterRequest0C?.Invoke();                     // 0xA55560/64 -> 0xA55498
            }
            // 0xA55498: re-check E0 == 1 (C17 item 3).
            if (voice.E0 == 1)
            {
                if (bus.FrameBudget < s)                             // 0xA55574 bge -> 0xA554A4
                    voice.Buffer.Result = voice.FilterRequest10?.Invoke(s) ?? voice.Buffer.Result;  // 0xA55598
            }
            return false;                                            // 0xA554A4/0xA5556C: r5=0 -> 0xA5521C
        }
        if (voice.E4 == 1)
        {
            voice.VoiceStop48?.Invoke();                             // 0xA55344/0xA5534C
            return false;                                            // r5=0 -> 0xA5521C
        }
        return true;                                                 // else -> 0xA55218 (r5=1)
    }

    /// <summary>
    /// V7-m <c>0xA550CC..0xA551F0</c> (C17 V7-m, C18 V7-p): the four parameter ramps, run only when
    /// <c>P2F!=0</c> and <c>[voice+0x28]!=0</c>. The targets are the <c>sp+0x30/34/38/3C</c> values: when
    /// <see cref="WwiseLiveVoice.Run2E"/> (<c>[sp+0x2e]</c>) is non-zero the copy at
    /// <c>0xA55068..0xA5508C</c> puts the voice target fields (<c>[voice+0x344]</c>, <c>[voice+0x514]</c>,
    /// <c>[voice+0x354]</c>, <c>[voice+0x524]</c>) there; when it is zero the copy is skipped and they hold
    /// the four <see cref="WwiseLiveVoice.OutputMin50"/> minima (zero when <c>id == 0</c>). Ramps 2 and 4
    /// apply <c>max(target,[bus+0x68])</c>/<c>max(target,[bus+0x6C])</c> first; all clamp to 100.0 and floor
    /// at 0. A record whose clamped target differs from its stored target gets flag 1, the new target, and
    /// <c>cur += (target_old - cur) * 0.125 * rate</c>.
    /// </summary>
    private static void RunParameterRamps(WwiseLiveVoice voice, WwiseMixBus bus)
    {
        // 0xA5505C..0xA5508C: the copy into sp+0x30/34/38/3C is gated on [sp+0x2e]; when the gate is clear
        // the four slots hold the 0xA4BC58 minima (0xA55090 is only reached with sp+0x30..3c already set).
        float t1 = voice.Run2E ? voice.Ramp340.Target : voice.OutputMin50[0];
        float t2 = voice.Run2E ? voice.Ramp510.Target : voice.OutputMin50[1];
        float t3 = voice.Run2E ? voice.Ramp350.Target : voice.OutputMin50[2];
        float t4 = voice.Run2E ? voice.Ramp520.Target : voice.OutputMin50[3];

        RunRamp(voice.Ramp340, t1, hasFloor: false, floor: 0f);          // 0xA550D8..0xA55104
        RunRamp(voice.Ramp510, t2, hasFloor: true, floor: bus.RampFloor68);   // 0xA55108..0xA55148
        RunRamp(voice.Ramp350, t3, hasFloor: false, floor: 0f);          // 0xA5514C..0xA55178
        RunRamp(voice.Ramp520, t4, hasFloor: true, floor: bus.RampFloor6C);   // 0xA5517C..0xA551BC
    }

    /// <summary>One V7-m ramp record: clamp/floor the target, then ramp <c>current</c> toward it.</summary>
    private static void RunRamp(WwiseVoiceRamp ramp, float target, bool hasFloor, float floor)
    {
        float clamped = target;
        if (hasFloor && clamped <= floor) clamped = floor;               // 0xA55118/0xA5518C vmovle
        if (clamped < 0f) clamped = 0f;                                  // the 0xA554AC/0xA554B4/... stubs
        if (clamped > 100f) clamped = 100f;                              // 0x42C80000
        if (ramp.Target == clamped) return;                              // bne to the body only on a change
        float targetOld = ramp.Target;
        ramp.Flag = 1;                                                   // 0xA55450/0xA5541C/0xA553E4/0xA551CC
        ramp.Target = clamped;                                           // 0xA5545C/0xA55428/0xA553F0/0xA551D8
        ramp.Current += (targetOld - ramp.Current) * 0.125f * ramp.Rate; // 0xA55464..0xA55474
    }

    /// <summary>
    /// V7-h <c>0xA4C620</c>, the <c>0x103C120</c> slot <c>+0x18</c>: if <c>[this+4]</c> (= <c>[voice+0x1C4]</c>)
    /// is null return 1; else tail-call <c>[[voice+0x1C4]]-&gt;vt+0x18(inner, E0, r2)</c>.
    /// </summary>
    private static int Wrapper18(WwiseLiveVoice voice, int e0, int r2)
    {
        if (voice.FilterInner1C4 is null) return 1;                  // 0xA4C624/0xA4C628/0xA4C638
        return voice.FilterInner18?.Invoke(e0, r2) ?? 1;             // 0xA4C62C..0xA4C634
    }

    /// <summary>
    /// V7 <c>0xA554F0</c>: <c>E4==2, SRC10 clear</c>, calls <c>0xA56650</c>. A return of <c>0x3F</c> sets
    /// <c>r5=0</c> and <c>P2F=0</c> without calling <c>voice-&gt;vt+0x48</c>; return 1 reaches <c>0xA55218</c>;
    /// anything else calls <c>voice-&gt;vt+0x48</c> and sets <c>r5=0, P2F=0</c> (C12 voice-callees Q4).
    /// </summary>
    private static bool Path554F0(WwiseLiveVoice voice, WwiseMixBus bus, ref bool p2f)
    {
        int r = voice.StartSource56650?.Invoke() ?? 0;               // 0xA554F8
        if (r == 1) return true;                                     // 0xA55218
        if (r != 0x3F) voice.VoiceStop48?.Invoke();                  // the else path only
        p2f = false;                                                 // [sp+0x2f]=0
        return false;                                                // -> 0xA5521C with r5=0
    }

    /// <summary>
    /// V17 <c>0xA44C18(arg)</c>: if <paramref name="arg"/> != 0, walk the bus array last-to-first. For each
    /// bus: <c>0xA4FEF8(bus,&amp;out)</c> (the output + FX + gain), then <c>[bus+0x1C8]</c> ? <c>0xA4F9E0</c>
    /// (the bus-to-output-bus mix) : the device path <c>0x9E9E78</c>; then <c>0xA4F36C(bus)</c> ReleaseBuffer.
    /// Both paths converge on the device walk and <c>0xA43F64</c> idle removal.
    /// </summary>
    public void BusPass(int arg)
    {
        if (arg != 0)
        {
            for (int i = _buses.Buses.Count - 1; i >= 0; i--)         // V17: last-to-first
            {
                var bus = _buses.Buses[i];
                var outBuffer = GetBusOutput(bus);                   // V18 0xA4FEF8(bus,&out)
                // V17: [bus+0x1C8] ? 0xA4F9E0(outputBus, out, bus) : the device path 0x9E9E78. The
                // device path runs only when the bus output's valid frames are non-zero (B1-V17,
                // 0xA44CB8/0xA44D2C), so a silent bus is not routed to the sink.
                if (bus.OutputBus is not null)
                    MixOutputBus(bus.OutputBus, outBuffer, bus);     // 0xA4F9E0
                else if (bus.Frames != 0)
                    _deviceState.RouteBus(bus);                      // 0x9E9E78 (C8)
                bus.ReleaseBuffer();                                 // V17: 0xA4F36C
            }
        }

        // V17: both paths converge on the 0x9E9F08 device walk and 0xA43F64 idle removal.
        _deviceState.AdvanceFrame(arg);
        _deviceState.ReleaseDeviceFrames();                          // 0x9E9F08 (C9)
        _buses.RemoveIdle();                                         // V20 0xA43F64
    }

    /// <summary>
    /// V17 <c>0xA4F9E0(outputBus, out, bus)</c> (C7): the bus-to-output-bus mix, called when the bus has an
    /// output bus at <c>+0x1C8</c>. It sets the output bus's mix state (<c>[bus+0x1BC]==4 -&gt; 1</c>,
    /// <c>[bus+0x68]=0x2D</c>), zero-pads the source past its valid frames, then either calls
    /// <c>[outputBus+0x1A8]-&gt;vt+0x28</c> or the <c>0xA45E9C</c> mixer with the source's
    /// <c>[voice+0x3C]/[voice+0x40]</c> gains. The kernel itself is M6-012's; the built default performs the
    /// same mono accumulation.
    /// </summary>
    public void MixOutputBus(WwiseMixBus outputBus, float[] source, WwiseMixBus sourceBus)
    {
        ArgumentNullException.ThrowIfNull(outputBus);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceBus);

        // 0xA4F9E0: ldrh ip,[r1,#0xe]; cmp ip,#0; bxeq lr -- a source buffer with no valid frames
        // returns before the mix state, the [outputBus+0x1A8] object and the 0xA45E9C kernel.
        if (sourceBus.Frames == 0) return;

        if (OutputBusMix is { } hook) { hook(outputBus, source, sourceBus); return; }
        if (outputBus.OutputMixObject1A8 is { } obj) { outputBus.MixInput(); obj(); return; }
        outputBus.MixBuffer(source, sourceBus.Frames, sourceBus.Gain3C, sourceBus.Gain40);   // 0xA45E9C
    }

    /// <summary>
    /// V17 <c>0xA4F9E0(outputBus, out, bus)</c> (C7/C8): an optional caller override for the bus-to-output-bus
    /// mix. When null the built <see cref="MixOutputBus"/> default runs; it is no longer the only path.
    /// </summary>
    public Action<WwiseMixBus, float[], WwiseMixBus>? OutputBusMix { get; set; }

    /// <summary>
    /// V18 <c>0xA4FEF8(bus, out)</c>: run the FX chain (through the bus lifetime's
    /// <see cref="WwiseMixBus.GetResultingBuffer"/>, which owns SetInsertFx <c>0xA4F754</c>, the FX execute
    /// <c>0xA4FD84</c> and the bus gain <c>0xA4D994</c>), then the bus level-analysis/metering tail
    /// <c>0xA50044..0xA50FD0</c>. The native runs the tail only when the meter object <c>[out+0x18]</c> is
    /// non-null (bus-metering Q1), so with no meter attached the tail is skipped.
    /// </summary>
    public float[] GetBusOutput(WwiseMixBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        bus.GetResultingBuffer(_updateBuffer ?? (_ => { }));         // V18: 0xA4F754/0xA4FD84/0xA4D994
        WwiseBusMetering.Run(bus);                                   // V18 tail: 0xA50044..0xA50FD0 (C12 X1)
        BusMeter?.Invoke(bus);                                       // optional extra caller hook
        return bus.Buffer;                                           // 0xA4FEF8's *out (0xA4FD84 -> bus+0x60)
    }

    /// <summary>
    /// V18 tail: an optional extra caller hook after <see cref="WwiseBusMetering.Run"/>. The meter itself is
    /// attached to the bus (<see cref="WwiseMixBus.Meter"/>); its RECOVERABLE_GAP kernels are seams on
    /// <see cref="WwiseBusMeter"/>.
    /// </summary>
    public Action<WwiseMixBus>? BusMeter { get; set; }

    /// <summary>
    /// V21 <c>0xA38420</c>: the deferred PBI-notification flush. Each item's code 4 (Term) unlinks the PBI,
    /// calls <c>0x9D3470</c>, its <c>vt+0x10</c> Term, its <c>vt+4</c> destructor and frees it. The PBI
    /// object is a caller seam (the PBI classes are M6-006/M6-008).
    /// </summary>
    public void FlushPbiNotifications()
    {
        while (PbiNotifications.Count > 0)
        {
            var item = PbiNotifications.Dequeue();
            item.Handle?.Invoke(item);                               // V21: 0xA0188C generic handler
            if (item.Code == WwisePbiNotification.TermCode)          // V21: [item+8] == 4
                item.Terminate?.Invoke(item);                        // V21: unlink, 0x9D3470, vt+0x10, vt+4, free
        }
    }

    // ---------------------------------------------------------------- V7 named callees

    /// <summary>
    /// V7-a <c>0xA4C584(voice,bit)</c> (C15 V7-a): for every connection in <c>[voice+0x28]</c>, set bit2 of
    /// <c>[conn+0x6C]</c> to the low bit of <paramref name="bit"/>.
    /// </summary>
    public static void SetConnectionBit2(WwiseLiveVoice voice, bool bit)
    {
        ArgumentNullException.ThrowIfNull(voice);
        foreach (var connection in voice.Connections)
            connection.Flags6C = (byte)(bit ? connection.Flags6C | 0x04 : connection.Flags6C & ~0x04);
    }

    /// <summary>
    /// V7-b <c>0xA022E8(bus,unused)</c> (C15 V7-b): if <c>[bus+0x1BE]&amp;0x20</c> is set, clear it, decrement
    /// the per-target counter at <c>[entry+0x22]</c> for each of <c>[bus+0x1F0]</c> entries in
    /// <c>[bus+0x1EC]</c>, then decrement the global dword. The array identity is UNKNOWN.
    /// </summary>
    public static void ReleaseBusRef(WwiseMixBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        if ((bus.Flags1BE & 0x20) == 0) return;
        bus.Flags1BE = (byte)(bus.Flags1BE & ~0x20);
        var arr = bus.RefCountArray1EC;
        for (int i = 0; i < bus.RefCount1F0 && arr is not null && i < arr.Length; i++)
            arr[i] = unchecked(arr[i] - 1);
        BusRefGlobal--;
    }

    /// <summary>V7-b <c>0xA0228C(bus)</c> (C15 V7-b): the exact inverse of <see cref="ReleaseBusRef"/>.</summary>
    public static void AcquireBusRef(WwiseMixBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        if ((bus.Flags1BE & 0x20) != 0) return;
        bus.Flags1BE = (byte)(bus.Flags1BE | 0x20);
        var arr = bus.RefCountArray1EC;
        for (int i = 0; i < bus.RefCount1F0 && arr is not null && i < arr.Length; i++)
            arr[i] = unchecked(arr[i] + 1);
        BusRefGlobal++;
    }

    /// <summary>The global dword the acquire/release pair touches (identity UNKNOWN, C15 V7-b).</summary>
    public static int BusRefGlobal;

    /// <summary>
    /// V7-c <c>0xA01768(bus,&amp;out)</c> (C15 V7-c): the cached 4-bit next-source code and 3-bit index in
    /// <c>[bus+0x1BB]</c> (bit7 valid); calls <c>0x9EEDA4([bus+0xE0])</c> (seam, RECOVERABLE_GAP), then
    /// <c>[[bus+0xE0]]-&gt;vt+0x120([bus+0x14C])</c> when the result is 3. Called as
    /// <c>0xA01768(bus,&amp;[voice+0xE0])</c>.
    /// </summary>
    public static int NextSource(WwiseMixBus bus, out int index)
    {
        ArgumentNullException.ThrowIfNull(bus);
        byte cached = bus.NextSource1BB;
        if ((cached & 0x80) != 0)
        {
            index = cached & 7;
            return (cached >> 3) & 0xF;
        }

        bus.NextSource1BB = (byte)(cached | 0x80);
        var result = bus.NextSourceEda?.Invoke(bus.NextSourceE0) ?? (0, 0);
        int code;
        if (result.Code == 3)
        {
            bus.E0Vt120?.Invoke(bus.E0Arg14C);
            code = result.Code == 0 ? 1 : 2;
        }
        else
        {
            code = result.Code & 0xF;
        }
        index = result.Index & 7;
        bus.NextSource1BB = (byte)((bus.NextSource1BB & 0x80) | (index & 7) | ((code & 0xF) << 3));
        return code;
    }

    /// <summary>
    /// V7-d <c>0xA4B93C(voice)</c> (C15 V7-d): initialise the voice's send/connection table
    /// (<c>[voice+0x10]</c> array, <c>[voice+0x14]</c> count, <c>[voice+0x18]</c> capacity, 0x4C-byte
    /// entries), copy the per-target byte and bus send gain into entry 0, gather via <c>0x9BD368</c> then
    /// dispatch <c>0x9D4228</c>, and latch bit1 of <c>[voice+0xCD]</c>. The sub-callees <c>0x9BE28C</c>,
    /// <c>0x9BDA88</c>, <c>0x9BD368</c>, <c>0x9BF8E4</c>, <c>0xA5E694</c> are RECOVERABLE_GAP and are seams.
    /// </summary>
    public static void InitSendTable(WwiseLiveVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        voice.SendTable ??= new WwiseVoiceSendTable();
        // 0xA4B9C8: r6 = [voice+0x14]; the lazy allocation runs only when it is 0, sized by [voice+0x18].
        // The count source is [voice+0x14], NOT the connection-list count [voice+0x28] (missing-bodies 1.7).
        voice.SendTable.AllocateLazily();
        if (voice.SendTable.Entries.Count > 0)
        {
            // 0xA4B9D4: copy [[r5+8]+0x22] to [[voice+0x10]+0x44] and the bus send gain to +0x34.
            var entry0 = voice.SendTable.Entries[0];
            entry0.PerTargetByte = voice.SendTable.PerTargetByte;
            entry0.SendGain = voice.SendTable.SendGain;
        }

        // 0xA4BA5C: 0x9BDA88(r5); if 0 return.
        if (voice.SendTableContinue9BDA88?.Invoke() == false) return;

        // 0xA4BA74/0xA4BA90: 0x9BD368(r5, sp+0x10) then 0x9D4228(sp+0x10, voice+0x2C, ...).
        voice.SendTableGather9BD368?.Invoke();
        GatherAndDispatch(voice);

        // 0xA4BA98: latch bit1 of [voice+0xCD].
        voice.FlagsCD = (byte)(voice.FlagsCD | 0x02);
    }

    /// <summary>
    /// V7-e <c>0x9D4228(paramBlock,outArray,flag,&amp;countByte,obj)</c> (C15 V7-e): gather the active
    /// entries (<c>value &gt; 0</c>) into the output array and dispatch <c>0x9D4108(obj,entry,mask)</c> per
    /// entry. The count byte is <c>[voice+0xCC]</c>, not <c>[voice+0x14]</c> (missing-bodies item 1.5
    /// step 4/6: <c>0xA4BA7C add r3,r4,#0xcc</c>; <c>0xA4BA90 bl 0x9D4228</c>). The block/entry semantic
    /// identity is UNKNOWN; the gather/dispatch order is the row's.
    /// </summary>
    public static int GatherAndDispatch(WwiseLiveVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        voice.SendTable ??= new WwiseVoiceSendTable();
        int count = 0;
        foreach (var entry in voice.SendTable.Entries)
        {
            if (entry.Value <= 0f) continue;
            count++;
            DispatchEntry(voice, entry);
        }
        voice.CountCC = (byte)count;                                 // 0xA4BA90: 0x9D4228 writes [voice+0xCC]
        return count;
    }

    /// <summary>
    /// V7-e <c>0x9D4108(obj,entry,mask)</c>: look up <c>0x9A7EB0</c> and call <c>0xA43434</c> for nodes
    /// matching the mask, then <c>found-&gt;vt+0xC</c>. The registry/object identity is UNKNOWN, so the
    /// per-entry update is a caller seam.
    /// </summary>
    public static void DispatchEntry(WwiseLiveVoice voice, WwiseVoiceSendEntry entry)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(entry);
        voice.DispatchEntryHook?.Invoke(entry);
    }

    /// <summary>
    /// V7-f <c>0xA54A30(voice)</c> (C15 V7-f): start the source/resampler, resolve up to four bus insert-FX
    /// slots via <c>0xA019B8</c>/<c>0x9CC2AC</c>/<c>0x9CC4D8</c>, build the 0x9C-byte voice slot objects
    /// (<see cref="WwiseVoiceInsertFxSlot"/>, vtables <c>0x103DC38</c>/<c>0x103DB98</c>, init
    /// <c>vt+0x28</c>), then initialise filter A/B and the gain. The plug-in registry and the effect class
    /// are UNKNOWN; the create/validate are caller seams. Returns 1 on success, 2 when the resampler start
    /// fails.
    /// </summary>
    public static int StartStreamAndBuildInsertFx(WwiseLiveVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        if (voice.StartResampler5321C?.Invoke() == false) return 2;  // 0xA54A78 0xA5321C != 1 -> 2

        for (int i = 0; i < voice.InsertFxSlots.Length; i++)
        {
            var candidate = voice.ResolveBusSlot?.Invoke(i);         // 0xA54AF0 0xA019B8
            if (candidate is null) continue;
            if (voice.CreatePlugin?.Invoke(candidate, i) != true) continue;  // 0x9CC2AC/0x9CC4D8
            var slot = voice.InsertFxSlots[i] ??= new WwiseVoiceInsertFxSlot();
            slot.HasPlugin = true;
            slot.Initialise();                                       // vt+0x28 = 0xA79858
        }

        // 0xA54B60: [voice+0xF0] = [sp+0x38]; filter A init 0xA764D4(voice+0x1D0,0); if 1 -> filter B.
        voice.FilterA.Reset();                                       // 0xA764D4 filter A init
        voice.FilterB.Reset();                                       // 0xA764D4 filter B init
        voice.InitGain?.Invoke();                                    // 0xA5676C(voice+0x380, bus)

        foreach (var slot in voice.InsertFxSlots)
            slot?.Initialise();                                      // slot->vt+0x24
        voice.VoiceStart6C?.Invoke();                                // voice->vt+0x6C
        return 1;
    }

    /// <summary>
    /// V7/C1 <c>0xA4BC58(voice, busChain, id, gain, ...)</c> (C12 voice-callees Q4/F1, C16 V7-g, C18
    /// V7-n/V7-o/V7-p): the per-connection gain/format update. It computes the aggregate <c>sb</c>/<c>fp</c>
    /// flags over the connection list, runs the branch structure that produces the <c>[sp+0x2f]</c> byte
    /// <c>P2F</c> and the <c>[sp+0x2e]</c> byte <see cref="WwiseLiveVoice.Run2E"/>, sets
    /// <c>[conn+0xC] = [voice+0x1C]*gain</c>, sets bit2 of <c>[conn+0x6C]</c>, copies
    /// <c>[param_2+0x3C]/[param_2+0x40]</c> (<c>[bus+0x48]/[bus+0x4C]</c>) to the connection, propagates
    /// <c>+0xA8..0xB4</c> to <c>+0xB8..0xC4</c>, and keeps the four running minima of
    /// <c>[conn+0x50/+0x54/+0x58/+0x5C]</c> in <paramref name="floatOutputs"/>.
    /// </summary>
    /// <param name="voice">The voice (<c>param_1</c>).</param>
    /// <param name="bus">The bus (<c>[source+0xC]</c>); <c>param_2</c> is <c>bus+0xC</c>.</param>
    /// <param name="gain">The <c>param_4</c> gain.</param>
    /// <param name="floatOutputs">
    /// The <c>param_8..11</c> destination, in order. The first call passes
    /// <see cref="WwiseLiveVoice.OutputMin50"/> (the caller's <c>sp+0x30..0x3c</c>); the second call at
    /// <c>0xA5572C</c> passes a scratch array because its outputs go to <c>sp+0x40</c> and are never read
    /// (C18 V7-q). Null means <see cref="WwiseLiveVoice.OutputMin50"/>.
    /// </param>
    /// <returns>The <c>P2F</c> byte the state machine reads at <c>0xA55248</c>/<c>0xA5526C</c>.</returns>
    /// <remarks>
    /// The conversion sub-callees <c>0xA5975C</c>, <c>0xA67C58</c>, <c>0xA67B9C</c> and <c>0xA5D70C</c> are
    /// unread and stay explicit seams (finding 5). The four output floats <c>[sp+0x5c..0x68]</c> carry the
    /// min of the connection fields <c>+0x50/+0x54/+0x58/+0x5C</c>.
    /// </remarks>
    public static bool UpdateConnectionGains(WwiseLiveVoice voice, WwiseMixBus bus, float gain, float[]? floatOutputs = null)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(bus);
        float[] minima = floatOutputs ?? voice.OutputMin50;

        // 0xA4BC90..0xA4BCAC: the four output floats start at 0.
        for (int i = 0; i < minima.Length; i++) minima[i] = 0f;

        // 0xA4BCB4..0xA4BCF0: the aggregate flags sb (bit2 AND) and fp (bit1).
        bool sb = true, fp = true, run = true;
        if (voice.Connections.Count == 0)
        {
            fp = false;                                              // 0xA4C198 mov fp,r0 (r0=0)
            run = true;                                              // 0xA4C19C mov r5,sb (sb=1)
        }
        else
        {
            foreach (var c in voice.Connections)
            {
                if (sb) sb = (c.Flags6C & 0x04) != 0;                // 0xA4BCC0/CC4
                if (run)
                {
                    if ((c.Flags6C & 0x02) == 0) { run = false; fp = true; }  // 0xA4BCDC/E0
                    else fp = false;                                 // 0xA4BCE4
                }
            }
        }

        int vt3c = voice.VoiceRequest3C?.Invoke() ?? 1;              // 0xA4BCF4 voice->vt+0x3C
        bool cd8 = (voice.FlagsCD & 8) != 0;

        bool p2f;
        bool runTail;
        if (vt3c != 0)
        {
            if (!cd8)
            {
                // 0xA4C058: set bit2 (to vt3c&1) on every connection. The native overwrites fp with
                // cd8 & 8 (=0) at 0xA4C05C, so 0xA4BD6C skips the main loop and the tail: only SetBit2.
                if (voice.Connections.Count != 0)
                    SetBit2(voice, vt3c & 1);
                p2f = false;
                runTail = false;
            }
            else
            {
                // 0xA4BD1C: sb==0 -> 0xA4C010 -> main loop + tail; else 0xA4C080 end (skip tail).
                if (!sb) { p2f = true; MainConnectionLoop(voice, bus, gain, minima); runTail = true; }
                else { p2f = false; runTail = false; }
            }
        }
        else
        {
            if (!cd8)
            {
                // 0xA4C03C: p2f = fp; fp==0 -> 0xA4BD40 bit2=1 only, 0xA4BD6C skips the tail;
                // fp!=0 -> 0xA4C024 bit2=r8 + main loop + tail.
                p2f = fp;
                if (!fp) { SetBit2(voice, 1); runTail = false; }
                else { SetBit2(voice, voice.SourceGain8Low); MainConnectionLoop(voice, bus, gain, minima); runTail = true; }
            }
            else if (run)
            {
                // 0xA4BD1C: sb==0 -> 0xA4C010 -> main loop + tail; else 0xA4C080 end (skip tail).
                if (!sb) { p2f = true; MainConnectionLoop(voice, bus, gain, minima); runTail = true; }
                else { p2f = false; runTail = false; }
            }
            else
            {
                // 0xA4C010: p2f=1, then the 0xA4BD74 main loop + tail.
                p2f = true;
                MainConnectionLoop(voice, bus, gain, minima);
                runTail = true;
            }
        }

        // 0xA4BFC4/0xA4BFD4: the tail runs only on the branches that reach 0xA4BFD4; 0xA4C080
        // (vt3c!=0 && cd8 && sb, and vt3c==0 && cd8 && run && sb) and 0xA4BD6C (the !cd8 paths whose fp
        // is 0) skip it. Propagate [param_2+0xA8..0xB4] to [param_2+0xB8..0xC4], then clear [voice+0xCD]
        // bit2 and [param_2+0xDC] bit4 (C18 X3).
        if (runTail)
        {
            bus.PropagateParams();
            voice.FlagsCD = (byte)(voice.FlagsCD & ~0x04);
            bus.ClearDcBit4();
        }

        // 0xA4BFEC/0xA4BFF0: the epilogue writes param_6 = r5. r5 is 0 iff the list is non-empty, some
        // connection has bit1 clear, and voice->vt+0x3C returned 0; every vt3c!=0 sub-path forces r5=1
        // (0xA4BD20, 0xA4C070, 0xA4C080). param_7 = p2f is the byte this method returns.
        voice.Run2E = vt3c != 0 || run;
        voice.P2F = p2f;
        return p2f;
    }

    /// <summary>0xA4BD54: set bit2 of every connection's <c>[conn+0x6C]</c> to the low bit of <paramref name="bit"/>.</summary>
    private static void SetBit2(WwiseLiveVoice voice, int bit)
    {
        foreach (var c in voice.Connections)
            c.Flags6C = (byte)((c.Flags6C & ~0x04) | ((bit & 1) << 2));
    }

    /// <summary>
    /// 0xA4BD74..0xA4BFB8: the per-connection gain/format loop. It sets the four output floats to 100.0,
    /// then per connection copies <c>[param_2+0x3C]/[param_2+0x40]</c> (<c>[bus+0x48]/[bus+0x4C]</c>, C18
    /// X3) to <c>+0x50/+0x58</c>, zeros <c>+0x54/+0x5C</c>, runs the <c>0xA5975C</c> conversion (seam) and
    /// keeps the minimum in the four output floats. It runs only when the id low byte is non-zero.
    /// </summary>
    private static void MainConnectionLoop(WwiseLiveVoice voice, WwiseMixBus bus, float gain, float[] minima)
    {
        if ((voice.Id & 0xFF) == 0) return;                          // 0xA4BD74 cmp r6,#0; beq 0xA4BFD4
        for (int i = 0; i < 4; i++) minima[i] = 100f;                // 0x42CA0000

        foreach (var c in voice.Connections)
        {
            // 0xA4BE20: if [conn+0x64] != id re-init via 0xA67C58/0xA67B9C (unread seams).
            if (c.C64 != (int)(voice.Id & 0xFF))
            {
                c.ReinitHook?.Invoke((int)(voice.Id & 0xFF));
                c.C64 = (int)(voice.Id & 0xFF);
            }

            c.C0C = voice.OutputGain * gain;                         // 0xA4BE6C [conn+0xc] = [voice+0x1c]*gain
            c.ConnectionGain = c.C0C;
            c.C50 = bus.Param2_3C;                                   // 0xA4BE80 [r7+0x3c], r7 = param_2
            c.C58 = bus.Param2_40;                                   // 0xA4BE88 [r7+0x40]
            c.C54 = 0f;
            c.C5C = 0f;
            c.Conversion5975C?.Invoke(c);                            // 0xA4BEC4 (seam)

            minima[0] = MathF.Min(minima[0], c.C50);                 // 0xA4BEC8..0xA4BF34
            minima[1] = MathF.Min(minima[1], c.C54);
            minima[2] = MathF.Min(minima[2], c.C58);
            minima[3] = MathF.Min(minima[3], c.C5C);

            c.Conversion5D70C?.Invoke(c);                            // 0xA4BF4C (seam)
            c.C08 = c.C0C;                                           // 0xA4BFA8 [conn+8]=[conn+0xc]
            c.C10 = c.C14;                                           // 0xA4BFAC [conn+0x10]=[conn+0x14]
        }
    }

    /// <summary>
    /// V7/C11 <c>0xA4B4B0(voice)</c> (C12 voice-callees Q4): apply the bus ducking to the voice output gain
    /// and each connection's <c>+0x60</c>, setting <c>[conn+0x6C]</c> bit1 when <c>s15 &lt;= threshold</c>.
    /// The threshold is settled: <c>0xA4B4B0</c> reads <c>[[0x10400AC]] = [0x1052454]</c> = binary32
    /// <c>0x38D1B717 = 0.0001f</c>, the Init.bnk -80 dB linear threshold (inventory M6-wwise-bank.md row
    /// 3.3 line ~846, C10). <see cref="DuckingThreshold"/> carries that default and stays overridable.
    /// </summary>
    /// <param name="voice">The voice (the native <c>r0</c>).</param>
    /// <param name="bus">The bus (<c>[voice+0xC]</c> in the native; the caller's primary bus).</param>
    public static void ApplyDucking(WwiseLiveVoice voice, WwiseMixBus bus)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(bus);

        // 0xA4B4B0..0xA4B5A8 (C12 voice-callees Q4).
        float s15 = bus.DuckingSum88;                                // [r3+0x88]
        float s13 = bus.Ducking1E0;                                  // [r3+0x1E0]
        float s14 = voice.OutputDb;                                  // [voice+0x20]
        s13 += s15;
        s14 -= s13;
        if (s14 > 0f) s13 += s14 * bus.Ducking1E4;                   // [r3+0x1E4]
        float s12 = bus.Ducking1D8;                                  // [r3+0x1D8]
        if (s12 < s13) s12 = s13;
        s15 -= s12;
        s14 = WwiseGain.DbToLinear(s15);                             // 0.05 / -37 / fast-pow
        s12 = s14 * voice.OutputGain;                                // [voice+0x1C]

        float g = DuckingThreshold;                                  // [0x1052454] = 0x38D1B717 = 0.0001f
        foreach (var connection in voice.Connections)                // [voice+0x28]
        {
            float sv = s14 * connection.C60;
            connection.C60 = sv;
            bool bit = sv <= g;                                      // bfi #1
            connection.Flags6C = (byte)((connection.Flags6C & ~0x02) | (bit ? 0x02 : 0));
        }

        voice.OutputGain = s12;                                      // 0xA4B5A4 vstr s12,[r0,#0x1c]
        voice.ApplyDuckingHook?.Invoke();                            // optional extra caller observer
    }

    /// <summary>
    /// V7/C11 <c>0xA4B4B0</c>: the ducking threshold global <c>[[0x10400AC]] = [0x1052454]</c>, the Init.bnk
    /// -80 dB linear threshold, binary32 <c>0x38D1B717 = 0.0001f</c> (inventory M6-wwise-bank.md row 3.3,
    /// C10). It is a settable property so tests can override it.
    /// </summary>
    public static float DuckingThreshold { get; set; } = 0.0001f;
}

/// <summary>
/// V21 <c>0x9D3470</c>/<c>0xA38600</c>/<c>0xA01800</c>: one queued PBI notification. The native node is
/// <c>{next, obj, code, reason, extra}</c>; code 4 is Term (C12 X6). The object is a caller seam (the PBI
/// classes are M6-006/M6-008).
/// </summary>
public sealed class WwisePbiNotification
{
    /// <summary>V21: the Term message code <c>[item+8]==4</c> (C12 X6).</summary>
    public const int TermCode = 4;

    /// <summary>The message code (<c>[item+8]</c>).</summary>
    public int Code { get; init; }

    /// <summary>The reason (<c>[item+0xC]</c>; the field the old row mislabelled "reason" as the code).</summary>
    public int Reason { get; init; }

    /// <summary>The generic handler <c>0xA0188C</c>; caller seam.</summary>
    public Action<WwisePbiNotification>? Handle { get; init; }

    /// <summary>The Term path: unlink, <c>0x9D3470</c>, <c>vt+0x10</c>, <c>vt+4</c>, free; caller seam.</summary>
    public Action<WwisePbiNotification>? Terminate { get; init; }
}

/// <summary>
/// V18 tail <c>[out+0x18]</c>: the bus level-analysis/metering object (M6-022 V18, C12 X1, bus-metering
/// Q1/Q5, missing-bodies item 3). Field offsets: <c>+4</c> peak, <c>+8</c> RMS, <c>+0xc</c> filtered peak,
/// <c>+0x10</c> filter state (0x30 bytes/channel), <c>+0x14</c>/<c>+0x18</c> biquad coefficient objects,
/// <c>+0x1c</c> the <c>0xA52164</c> loudness output, <c>+0x20</c> the analysis flags.
/// </summary>
public sealed class WwiseBusMeter
{
    // fidelity: M6-022

    /// <summary>The meter's flag byte <c>[meter+0x20]</c>.</summary>
    public byte Flags { get; set; }

    /// <summary>The channel count the arrays were sized for.</summary>
    public int Channels { get; set; } = 1;

    /// <summary><c>+4</c>: per-channel min/max peak output.</summary>
    public float[] Peak { get; set; } = new float[1];

    /// <summary><c>+8</c>: per-channel RMS output.</summary>
    public float[] Rms { get; set; } = new float[1];

    /// <summary><c>+0xc</c>: per-channel filtered-peak output.</summary>
    public float[] FilteredPeak { get; set; } = new float[1];

    /// <summary><c>+0x10</c>: per-channel filter state (0x30 bytes = 12 floats each).</summary>
    public float[] FilterState { get; set; } = new float[12];

    /// <summary><c>+0x1c</c>: the <c>0xA52164</c> biquad/loudness output.</summary>
    public float BiquadLoudness { get; set; }

    /// <summary>
    /// The bit-2 fixed-coefficient filter kernel (0xA501B0..0xA50B54). Its DSP identity and per-lane mapping
    /// are RECOVERABLE_GAP (C15 residual; bus-metering Q1/Q5): no shipped artifact names it. The scalar flow
    /// runs without it and records <see cref="FilterKernelMissing"/> rather than inventing a kernel.
    /// </summary>
    public Func<ReadOnlySpan<float>, float>? FilterKernel { get; set; }

    /// <summary>True once the bit-2 stage ran with no filter kernel (the RECOVERABLE_GAP).</summary>
    public bool FilterKernelMissing { get; private set; }

    /// <summary>
    /// The bit-4 two-section biquad kernel (0xA52164). Its DSP identity is RECOVERABLE_GAP (bus-metering
    /// Q5); when null the stage records <see cref="BiquadKernelMissing"/> instead of inventing coefficients.
    /// </summary>
    public Func<ReadOnlySpan<float>, float>? BiquadKernel { get; set; }

    /// <summary>True once the bit-4 stage ran with no biquad kernel (the RECOVERABLE_GAP).</summary>
    public bool BiquadKernelMissing { get; private set; }

    /// <summary>
    /// <c>[bus+0xc1]&amp;0x1f</c>: the registered per-bus callback <c>0x9C806C(reg, bus+0x48, meter, out+4)</c>.
    /// The registry's semantic owner is RECOVERABLE_GAP (bus-metering Q4), so the callback is a caller seam.
    /// </summary>
    public Action<WwiseBusMeter, uint>? RegisteredCallback { get; set; }

    internal void MarkFilterKernelMissing() => FilterKernelMissing = true;
    internal void MarkBiquadKernelMissing() => BiquadKernelMissing = true;
}

/// <summary>
/// V18 tail <c>0xA50044..0xA50FD0</c>: the bus level-analysis/metering stage (C12 X1, missing-bodies item 3).
/// It writes no audio samples: the flag-selected analyses write only the meter arrays and the filter state.
/// The scalar control flow, field offsets, loop bounds, gains and store targets are EXACT_SOURCE; the bit-2
/// filter identity and the bit-4 biquad identity are RECOVERABLE_GAP (C15 residual). Those two kernels are
/// explicit, documented caller seams that do not throw on the normal path.
/// </summary>
public static class WwiseBusMetering
{
    // fidelity: M6-022

    /// <summary>The metering flag byte <c>[meter+0x20]</c>: bit0 peak, bit1 filtered peak, bit2 RMS, bit4 the biquad path.</summary>
    public const byte PeakFlag = 0x01;
    public const byte FilteredPeakFlag = 0x02;
    public const byte RmsFlag = 0x04;
    public const byte BiquadFlag = 0x10;

    /// <summary>0xA50FD4: the peak/RMS gain compensation <c>1.0009619</c>.</summary>
    public const float MeterGain = 1.0009619f;

    /// <summary>0xA52670: the <c>0xA52164</c> weighting <c>1.41253746</c> (10^0.15).</summary>
    public const float BiquadWeight = 1.41253746f;

    /// <summary>
    /// V18 tail: run the stage on <paramref name="bus"/>'s output. With no meter (<c>[out+0x18]==0</c>) the
    /// tail is skipped, as the native does.
    /// </summary>
    public static void Run(WwiseMixBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        if (bus.Meter is null) return;
        Run(bus.Meter, bus.Buffer, bus.Frames, bus.OutputGain);
    }

    /// <summary>
    /// The settled scalar flow. <paramref name="samples"/> is the planar output buffer, one channel plane at
    /// <c>ch*stride</c>; the shipped robot bus is mono (M6-013 gapF 3.1). <paramref name="gain"/> is the bus
    /// output gain (<c>out+0x14</c>).
    /// </summary>
    public static void Run(WwiseBusMeter meter, float[] samples, int validFrames, float gain)
    {
        ArgumentNullException.ThrowIfNull(meter);
        ArgumentNullException.ThrowIfNull(samples);
        int channels = Math.Max(1, meter.Channels);
        int stride = validFrames;

        if (validFrames == 0)
        {
            // 0xA5005C..: only clear the arrays selected by the flags.
            if ((meter.Flags & PeakFlag) != 0) Array.Clear(meter.Peak, 0, Math.Min(channels, meter.Peak.Length));
            if ((meter.Flags & RmsFlag) != 0) Array.Clear(meter.Rms, 0, Math.Min(channels, meter.Rms.Length));
            if ((meter.Flags & FilteredPeakFlag) != 0) Array.Clear(meter.FilteredPeak, 0, Math.Min(channels, meter.FilteredPeak.Length));
            return;
        }

        float compensated = gain * MeterGain;

        for (int ch = 0; ch < channels && ch < meter.Peak.Length; ch++)
        {
            var plane = samples.AsSpan(ch * stride, Math.Min(validFrames, samples.Length - ch * stride));

            // bit0 - min/max peak (0xA50D48..0xA50E80).
            if ((meter.Flags & PeakFlag) != 0)
            {
                if (gain <= 0f) meter.Peak[ch] = 0f;
                else
                {
                    float min = 0f, max = 0f;
                    foreach (float s in plane)
                    {
                        if (s < min) min = s;
                        if (s > max) max = s;
                    }
                    meter.Peak[ch] = MathF.Max(MathF.Abs(min), max) * compensated;
                }
            }

            // bit2 - RMS (0xA50C84..0xA50D44).
            if ((meter.Flags & RmsFlag) != 0 && ch < meter.Rms.Length)
            {
                float sum = 0f;
                foreach (float s in plane) sum += s * s;
                meter.Rms[ch] = MathF.Sqrt(sum / stride) * compensated;
            }

            // bit1 - fixed-coefficient filtered peak (0xA501B0..0xA50B54). Kernel identity RECOVERABLE_GAP.
            if ((meter.Flags & FilteredPeakFlag) != 0 && ch < meter.FilteredPeak.Length)
            {
                if (meter.FilterKernel is { } kernel) meter.FilteredPeak[ch] = kernel(plane) * compensated;
                else { meter.MarkFilterKernelMissing(); meter.FilteredPeak[ch] = 0f; }
            }
        }

        // bit4 - the 0xA52164 biquad/loudness stage (0xA500B0..0xA500BC). Kernel identity RECOVERABLE_GAP.
        if ((meter.Flags & BiquadFlag) != 0)
        {
            if (meter.BiquadKernel is { } kernel)
            {
                float sum = 0f;
                for (int ch = 0; ch < channels; ch++)
                    sum += kernel(samples.AsSpan(ch * stride, Math.Min(validFrames, samples.Length - ch * stride)));
                meter.BiquadLoudness = gain * gain * sum / channels;      // 0xA525A0..0xA525AC
            }
            else
            {
                meter.MarkBiquadKernelMissing();
                meter.BiquadLoudness = 0f;
            }
        }

        // [bus+0xc1]&0x1f: the registered per-bus callback (0xA500C0..0xA5019C). Owner RECOVERABLE_GAP.
        meter.RegisteredCallback?.Invoke(meter, 0);
    }
}