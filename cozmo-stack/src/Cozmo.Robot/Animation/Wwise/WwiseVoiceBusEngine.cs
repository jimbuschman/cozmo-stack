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

    /// <summary><c>+0x04</c>: the AkChannelConfig word the output hand-off <c>0xA73490</c> writes (its 5th argument; C36: not a rate). The live sources fill <see cref="WwiseDecodeState.ChannelConfig"/>; this field is not read.</summary>
    public int Rate { get; set; }

    /// <summary>
    /// The voice pass block <c>state</c> (the engine's <c>S</c>, 0x28 bytes + the result): the sources fill it, the pitch node copies its output into it, the mix reads it. <see cref="Result"/> and <see cref="ValidFrames"/> are its
    /// <c>[+0x28]</c> and <c>u16 [+0xC]</c>.
    /// </summary>
    // fidelity: M6-022
    public WwiseDecodeState State { get; } = new() { Code28 = 0 };

    /// <summary>
    /// The engine's <c>[state+0xC]</c> (the frames asked of the source: the voice's pull loop writes <c>u16[0x1052440]</c> there before every source call, <c>0xA4478C..0xA4479C</c>; P01). The name is the earlier
    /// model's; the engine's valid-frames count <c>[state+0xE]</c> is <see cref="WwiseDecodeState.ValidFrames"/> of <see cref="State"/>.
    /// </summary>
    public int ValidFrames
    {
        get => State.MaxFrames;
        set => State.MaxFrames = unchecked((ushort)value);
    }

    /// <summary>The C# model's buffer capacity in frames (the planar arrays' length); the engine's <c>[state+0xE]</c> is the valid-frames count (<see cref="WwiseDecodeState.ValidFrames"/>), not a capacity.</summary>
    public int MaxFrames { get; }

    /// <summary><c>+0x18</c>: start sample.</summary>
    public int Start { get; set; }

    /// <summary><c>+0x20</c>: total/end sample.</summary>
    public int Total { get; set; }

    /// <summary><c>+0x24</c>: the media sample rate the output hand-off writes (its 4th argument; C36: not a pitch). The live sources fill <see cref="WwiseDecodeState.Rate"/>; this field is not read.</summary>
    public int Pitch { get; set; }

    /// <summary><c>+0x28</c>: the DSP result code (0x11/0x2B/0x2D/2): <see cref="WwiseDecodeState.Code28"/> of <see cref="State"/>.</summary>
    public int Result
    {
        get => State.Code28;
        set => State.Code28 = value;
    }

    /// <summary><c>+0x2C</c>: set to 1 when <c>[PBI+0x1F8]</c> is present (V7).</summary>
    public bool HasBusParam { get; set; }

    /// <summary>
    /// The voice pass block's per-voice initialisation (<c>0xA44A00..0xA44A48</c>, C38.3): <c>[state+0x00] = 0</c>, the channel word 0, <c>[state+0x08] = 0x2B</c>, <c>u16[state+0xC] = u16[0x1052440]</c> (1024), <c>u16[state+0xE] = 0</c>, no markers,
    /// <c>[state+0x28] = 0x2B</c> and the byte <c>[state+0x2C] = 0</c>. The words <c>+0x18..+0x24</c> are uninitialised stack in the engine and are left as they are.
    /// </summary>
    // fidelity: M6-022
    public void InitPassBlockA44A00()
    {
        State.Data = null;
        State.ChannelConfig = 0;
        State.Scratch08 = 0x2B;
        State.MaxFrames = unchecked((ushort)WwiseLiveVoice.PullFrames1052440);
        State.ValidFrames = 0;
        State.MarkerCount = 0;
        State.Markers = null;
        State.Code28 = 0x2B;
        HasBusParam = false;
    }

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
/// <c>vt+0x28</c> (0xA54A30); <see cref="StartStreamSucceeded"/> is the <c>[source+0x10]</c> bit 0 latch
/// that <c>0xA56650</c> sets when <c>vt+0x28</c> returns 1 (M6-025 C23 item 5 note 4, row 4.25). It is not
/// source <c>vt+0x4C</c>: native <c>vt+0x4C</c> returns <c>[[source+0xC]+0x1BE]</c> bit 6 (<c>0xA72B14</c>,
/// <c>0xA566C8</c>), a different fact.</para>
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

    /// <summary>
    /// <c>vt+0x28</c> StartStream: the raw <c>int</c> result, returned unchanged by <c>0xA56650</c>
    /// (<c>0xA56664..0xA56688</c>: <c>ldr r3,[r0]; ldr r3,[r3,#0x28]; blx r3</c>, the result stays in r0) and by AddSrc
    /// (<c>mov r6,r0</c>, <c>0xA55920</c>). It takes the owner PBI's <c>[owner+0x1DC]</c> and <c>[owner+0x1E0]</c>
    /// (<c>0xA5590C</c>, <c>0xA55910</c>, <c>0xA544D8</c>, <c>0xA544DC</c>) as <c>r1</c> and <c>r2</c>. 1 and 0x3F are the
    /// results AddSrc keeps; every other value goes to AddSrc's step 8 unchanged (M6-025 C27 step 7, C30). Call it through
    /// <see cref="WwiseVoiceSourceStart.StartA56650"/>, which owns the latch.
    /// </summary>
    // fidelity: M6-025
    int StartStream(uint arg1DC, uint arg1E0);

    /// <summary>
    /// The <c>[source+0x10]</c> bit 0 latch the per-voice machine tests as <c>SRC10</c>. <c>0xA56650</c> reads it
    /// (<c>0xA56650..0xA56660</c>) and sets it when <c>vt+0x28</c> returned exactly 1 (<c>0xA56678..0xA56684</c>); it is not
    /// source <c>vt+0x4C</c>, which is <c>[[source+0xC]+0x1BE]</c> bit 6 (M6-025 C23 row 4.25 and its check).
    /// </summary>
    bool StartStreamSucceeded { get; set; }

    /// <summary>
    /// <c>vt+0x6C</c>, the live-voice list ordering key <c>0xA42DEC</c> reads (C23 item 1 row 20 and item 5
    /// row 5.10): <c>0xA56708</c> (<c>mov r0,#0</c>) in every one of the ten shipped source vtables, so the
    /// default is 0. A source class outside those ten is not checked.
    /// </summary>
    // fidelity: M6-025
    int OrderKey6C => 0;

    /// <summary>
    /// V7 prologue <c>0xA54F70</c>: <c>[source+8]</c>, a pointer to a <c>{+0,+4}</c> gain pair, or null when
    /// <c>[source+8]==0</c>. <c>[+4]</c> scales the gain; <c>[+0]</c> scales it again when <c>[bus+0x58]</c>
    /// bit0 is set. The source class is UNKNOWN, so this is a caller seam.
    /// </summary>
    (float At0, float At4)? Gain8 => null;

    /// <summary>
    /// <c>vt+0x2C</c>, the source's close (<c>0xA5644C</c> in <c>0xA56414</c>): the six classes' bodies are <c>0xA73128</c>, <c>0xA72AF4</c>, <c>0xA7427C</c>, <c>0xA76178</c>, <c>0xAB0FC0</c>, <c>0xAB2958</c> (C34.3 S2..S7). A source class
    /// that has not built its body throws (it replaces the bridge's <c>SourceClose2C</c> seam).
    /// </summary>
    // fidelity: M6-025
    void Close2C() => throw new WwiseMissingBehaviourException(
        $"M6-025 S2..S7: {GetType().Name} has no source close (vt+0x2C) body");

    /// <summary>
    /// <c>vt+0x34</c>, the source's duration (<c>0xA56598..0xA565A4</c>): <c>0xA72F5C</c> for the six codec classes (C34.3 S1), read by <c>0xA56478</c> when <c>[source+0x10]</c> bit 0 is set. A class that has not built it throws
    /// (it replaces the bridge's <c>SourceDuration34</c> seam).
    /// </summary>
    // fidelity: M6-025
    float Duration34() => throw new WwiseMissingBehaviourException(
        $"M6-025 S1: {GetType().Name} has no source duration (vt+0x34) body");
}

/// <summary><c>0xA56650(source, a, b)</c>, the one caller of a source's <c>vt+0x28</c> that owns the <c>[source+0x10]</c> bit 0 latch.</summary>
// fidelity: M6-025
public static class WwiseVoiceSourceStart
{
    /// <summary>
    /// <c>0xA56650(source, a, b)</c>: with the latch set it returns 1 and does not call <c>vt+0x28</c>
    /// (<c>0xA56650..0xA56660</c>); otherwise it calls <c>vt+0x28(source, a, b)</c> and returns its raw result,
    /// setting the latch only when that result is 1 (<c>0xA56664..0xA56688</c>).
    /// </summary>
    /// <param name="ran">True when <c>vt+0x28</c> was called, false when the latch short-circuited it.</param>
    public static int StartA56650(IWwiseVoiceSource source, uint a, uint b, out bool ran)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.StartStreamSucceeded)                                  // 0xA56650 ldrb ip,[r0,#0x10]; tst ip,#1; beq
        {
            ran = false;
            return 1;                                                    // 0xA5665C
        }
        ran = true;
        int result = source.StartStream(a, b);                           // 0xA56664..0xA56674 vt+0x28
        if (result == 1) source.StartStreamSucceeded = true;             // 0xA56678..0xA56684 (cmp r0,#1; orreq)
        return result;
    }
}

/// <summary>
/// The connection descriptor <c>conn+0x18..+0x24 = {data, size, ptrA, ptrB}</c> (C24.4), zeroed by the ctor
/// (<c>0xA6F998..0xA6F9A4</c>). <see cref="Reserve"/> is <c>0xA67B9C(desc, inCh, outCh)</c>, <see cref="Free"/> is
/// the destructor part <c>0xA67C58</c>, and <see cref="SwapPointers"/> is the per-frame <c>+0x20/+0x24</c> swap
/// (<c>0xA4BE34..0xA4BE58</c>).
/// </summary>
public sealed class WwiseConnectionDescriptor
{
    // fidelity: M6-025

    /// <summary><c>+0x18</c> (and <c>+0x20</c> before any swap): the data block; null when unallocated.</summary>
    public byte[]? Data { get; private set; }

    /// <summary><c>+0x1C</c>: the byte size.</summary>
    public int Size { get; private set; }

    /// <summary><c>+0x20</c>: offset of the first half (starts at 0 = <c>data</c>).</summary>
    public int PtrA { get; private set; }

    /// <summary><c>+0x24</c>: offset of the second half (starts at <c>size/2</c> = <c>data + size/2</c>).</summary>
    public int PtrB { get; private set; }

    /// <summary><c>[conn+0x18] != 0</c>.</summary>
    public bool IsAllocated => Data is not null;

    /// <summary>
    /// The allocation-failure branch of <c>0xA67B9C</c> (<c>0xA67BE4 bl 0xA7A894</c>, <c>0xA67BE8 cmp r0,#0</c>,
    /// <c>0xA67BF0 beq 0xA67C40</c>, <c>0xA67C40 mov r0,#2</c>): the same hook as <see cref="WwisePlaybackLimiter.AllocationFails"/>
    /// and <see cref="WwiseStartList.AllocationFails"/>. True fails the allocation; null means it never fails. It is called once per
    /// allocation, after the old block is freed, so a failure leaves the four words zero.
    /// </summary>
    // fidelity: M6-025
    public Func<bool>? AllocationFails { get; set; }

    /// <summary>
    /// <c>0xA67B9C</c>: <c>size = ((outCh+3)&gt;&gt;2) * (inCh&lt;&lt;5)</c>; returns 1 at once when equal to
    /// <c>[desc+4]</c>, else frees and reallocates and stores <c>{data, size, data, data+size/2}</c>, returning 1.
    /// An allocation failure returns 2 with the four words zero (the old block was freed and the words cleared first,
    /// <c>0xA67C0C..0xA67C3C</c>, then <c>0xA67BEC str r0,[r5]</c> stores the null).
    /// </summary>
    public int Reserve(int inCh, int outCh)
    {
        int size = ((outCh + 3) >> 2) * (inCh << 5);
        if (size == Size) return 1;
        Free();
        if (AllocationFails?.Invoke() == true) return 2;                 // 0xA67BE8..0xA67C44
        Data = new byte[size];
        Size = size;
        PtrA = 0;
        PtrB = size / 2;
        return 1;
    }

    /// <summary><c>0xA67C58</c>: releases the allocation and zeroes the descriptor.</summary>
    public void Free()
    {
        Data = null;
        Size = 0;
        PtrA = 0;
        PtrB = 0;
    }

    /// <summary>The <c>+0x20</c>/<c>+0x24</c> swap.</summary>
    public void SwapPointers() => (PtrA, PtrB) = (PtrB, PtrA);
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

    /// <summary>
    /// The native <c>[conn+0x18] != 0</c> test the V8 walks make (C24.4): true once the connection descriptor
    /// (<see cref="Descriptor"/>, <c>conn+0x18..+0x24</c>) holds an allocation.
    /// </summary>
    // fidelity: M6-025
    public bool HasDry => Descriptor.IsAllocated;

    /// <summary>
    /// <c>conn+0x18..+0x24</c>: the <c>{data, size, ptrA, ptrB}</c> descriptor, zeroed by the ctor
    /// (<c>0xA6F998..0xA6F9A4</c>) and sized by <c>0xA67B9C</c> in the per-frame pass (C24.4).
    /// </summary>
    // fidelity: M6-025
    public WwiseConnectionDescriptor Descriptor { get; } = new();

    /// <summary>The native <c>conn+0x6C</c> bits 1/2 (fade/format state).</summary>
    public byte Flags6C { get; set; }

    /// <summary><c>conn+0x48/+0x4C</c>: the 64-bit output-device id (ctor <c>0xA6F98C strd</c>, C23 row 17).</summary>
    // fidelity: M6-025
    public WwiseDeviceId Device { get; set; }

    /// <summary>
    /// <c>conn+0x68</c>: the ctor's <c>arg5</c> (<c>0xA6F990</c>), 0 for a dry connection and
    /// <c>[send+0x10]</c> (ORed with 4 when the two bus bit6 flags differ) for an aux one (C23.3).
    /// </summary>
    // fidelity: M6-025
    public uint Arg68 { get; set; }

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

    /// <summary>V7/C1 <c>0xA4BEC4</c> <c>0xA5975C</c> per-connection conversion (unread seam).</summary>
    public Action<WwiseVoiceConnection>? Conversion5975C { get; set; }

    /// <summary>V7/C1 <c>0xA4BF4C</c> <c>0xA5D70C</c> per-connection step (unread seam).</summary>
    public Action<WwiseVoiceConnection>? Conversion5D70C { get; set; }

    /// <summary>The pan matrix the next <see cref="Refresh"/> applies (M6-012 gapE 2.1; caller-supplied).</summary>
    public float[]? TargetMatrix { get; set; }

    /// <summary>The composed target gain the next <see cref="Refresh"/> applies (M6-012 gapE 2.1).</summary>
    public float TargetGain { get; set; } = 1f;

    /// <summary>
    /// The first-update fade-in flag (M6-012 gapE 2.5): the live <c>conn+0x6C</c> bit2, read at <c>0xA4C0C0
    /// tst r3,#4</c>. Every frame rewrites that bit (<c>0xA4C584..0xA4C598</c>, <see cref="WwiseVoiceBusPass.SetConnectionBit2"/>),
    /// so this is derived from <see cref="Flags6C"/>, not a copy.
    /// </summary>
    // fidelity: M6-025
    public bool FadeIn => (Flags6C & 0x04) != 0;

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
    /// <c>0xA4FBEC(bus, state, conn, {g0, g1})</c> (V2-05, C38.3): with <c>u16 [state+0xE] == 0</c> it returns and nothing is touched (<c>0xA4FBF4..0xA4FBF8</c>). Otherwise <c>[bus+0x68] = 0x2D</c> and a bus state of 4 becomes 1
    /// (<see cref="WwiseMixBus.MixInput"/>), the channels of the voice's block are zero-padded from <c>u16 [state+0xE]</c> to <c>u16 [state+0xC]</c> (planar stride <c>u16 [state+0xC]</c>; the padding is skipped with no pad or no channels),
    /// <c>u16 [state+0xE] = u16 [state+0xC]</c>, and unless the bus has a mixer plug-in (<c>[bus+0x1A8] != 0 &amp;&amp; [[bus+0x1A8]+0xC] != 0</c>: none on shipped data, the plug-in's <c>vt+0x28</c> is not adopted, so it throws) the matrix mixer
    /// <see cref="WwiseMixerConnection.MatrixMixA45E9C"/> runs with <c>start = (conn[0x10] * conn[8]) * g0</c> and <c>end = (conn[0x14] * conn[0xC]) * g1</c> (float32, in that order) over the BUS frame count (not the voice's valid count: that
    /// only gates and pads); then <c>u16 [bus+0x6E] = u16 [bus+0x58]</c>. An LFE configuration (bit 15 of the channel word, <c>0xA45FD0</c> unread) throws.
    /// </summary>
    // fidelity: M6-022, M6-012
    public void MixA4FBEC(WwiseDecodeState state, float g0, float g1)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ValidFrames == 0) return;                                          // 0xA4FBF4..0xA4FBF8 ldrh lr,[r1,#0xe]; beq 0xA4FD08
        Bus.MixInput();                                                              // 0xA4FC00..0xA4FC24 [bus+0x68] = 0x2D; [bus+0x1BC] == 4 -> 1
        int max = state.MaxFrames;                                                   // 0xA4FC14 ldrh r3,[r1,#0xc]
        int valid = state.ValidFrames;
        if (valid > max) throw new WwiseMissingBehaviourException("M6-022 V2-05: u16 [state+0xE] above u16 [state+0xC] makes the pad length wrap (0xA4FC28); not modelled");
        int channels = (byte)state.ChannelConfig;                                    // 0xA4FC38 ldrb r8,[r1,#4]
        var data = state.Data as float[] ?? throw new WwiseMissingBehaviourException("M6-022 V2-05: the voice's block has valid frames but no data pointer (0xA4FC4C ldr r3,[r1] is 0: the engine writes through it)");
        int pad = max - valid;                                                       // 0xA4FC28 subs r6,r3,lr
        if (pad != 0 && channels != 0)                                               // 0xA4FC34..0xA4FC40
            for (int c = 0; c < channels; c++)
                Array.Clear(data, valid + c * max, pad);                             // 0xA4FC58..0xA4FC6C memset(data + (valid + c * max) * 4, 0, pad * 4)
        state.ValidFrames = unchecked((ushort)max);                                  // 0xA4FC88 strh r2,[r4,#0xe]
        if (Bus.OutputMixObject1A8 is not null && Bus.MixObject1A8C is not null)     // 0xA4FC7C..0xA4FC98 ldr r3,[r5,#0x1a8]; ldr r2,[r3,#0xc]
            throw new WwiseMissingBehaviourException("M6-022 V2-05: the bus has a mixer plug-in ([bus+0x1A8] and [[bus+0x1A8]+0xC] set): its vt+0x28 (0xA4FCD0) is not adopted (none on shipped data, C37.4)");
        if ((state.ChannelConfig & 0x8000u) != 0)
            throw new WwiseMissingBehaviourException("M6-012 V2-06: the LFE branch of the matrix mixer (0xA45FD0..0xA46078) is not adopted (no shipped configuration has it)");
        if (channels != Mixer.InputChannels)
            throw new InvalidOperationException($"the voice's block has {channels} channels, the connection mixes {Mixer.InputChannels}");
        float start = (C10 * C08) * g0;                                              // 0xA4FD58..0xA4FD60 vmul.f32 s15,s15,s11; vmul.f32 s15,s15,s13
        float end = (C14 * C0C) * g1;                                                // 0xA4FD44, 0xA4FD5C
        var sources = new ReadOnlyMemory<float>[channels];
        for (int c = 0; c < channels; c++) sources[c] = data.AsMemory(c * max, max);
        Mixer.MatrixMixA45E9C(sources, new[] { Bus.Buffer }, start, end, Bus.MaxFrames);   // 0xA4FD6C bl 0xA45E9C(S, bus+0x60, &{start, end}, [conn+0x24], [conn+0x20], [bus+0x5C], u16 [bus+0x58])
        Bus.SetFramesA4FD74();                                                       // 0xA4FD70..0xA4FD74 u16 [bus+0x6E] = u16 [bus+0x58]
    }

    /// <summary>
    /// The earlier model's mix (the legacy render order, whose block is the planar <see cref="WwiseVoiceBuffer.Channels"/>): zero-pad to the bus frame and accumulate through <see cref="WwiseMixerConnection.ConsumeBuffer"/> with this
    /// connection's <see cref="WwiseMixerConnection.StartGain"/> / <see cref="WwiseMixerConnection.EndGain"/>. The engine's mix is <see cref="MixA4FBEC"/>.
    /// </summary>
    public void Mix(WwiseVoiceBuffer buffer)
    {
        Bus.MixInput();
        Mixer.ConsumeBuffer(buffer.Channels, new[] { Bus.Buffer }, buffer.ValidFrames, Bus.MaxFrames);
    }
}

/// <summary>
/// One 0x4C-byte entry of the voice's table <c>[voice+0x10]</c> (C40.4 T-A4: <c>0xA4B93C</c> initialises entry 0 at <c>0xA4BB00..0xA4BB50</c> and refreshes its byte <c>+0x44</c> and float <c>+0x34</c> on every call). The table is NOT the array <c>0x9D4228</c> merges into
/// (that is <c>voice+0x2C</c>, <see cref="WwiseAuxEntry"/>). The fields no adopted row names are not modelled.
/// </summary>
public sealed class WwiseVoiceSendEntry
{
    // fidelity: M6-022

    /// <summary><c>+0x0</c>, <c>+0x4</c>, <c>+0x8</c>, <c>+0x30</c>: zero from the entry's initialisation (<c>0xA4BB28..0xA4BB4C</c>).</summary>
    public uint Word0 { get; set; }
    public uint Word4 { get; set; }
    public uint Word8 { get; set; }
    public uint Word30 { get; set; }

    /// <summary><c>+0x38</c>, <c>+0x3C</c>, <c>+0x48</c>: 1.0f from the initialisation (<c>0xA4BB38..0xA4BB44</c>).</summary>
    public float Gain38 { get; set; } = 1f;
    public float Gain3C { get; set; } = 1f;
    public float Gain48 { get; set; } = 1f;

    /// <summary><c>+0x40</c>: -1 from the initialisation (<c>0xA4BB20</c>, <c>0xA4BB28</c>).</summary>
    public int Word40 { get; set; } = -1;

    /// <summary><c>+0x44</c>: the game object's listener mask byte <c>[[ctx+8]+0x22]</c>, stored at every <c>0xA4B93C</c> (<c>0xA4B9D4..0xA4B9E8</c>).</summary>
    public byte PerTargetByte { get; set; }

    /// <summary><c>+0x34</c>: the dry send gain <c>lin([ctx+0x58]) * [[ctx+8]+0x60]</c> (the PBI's <c>+0x64</c> through the fast power, times the game object's output bus volume), stored at every <c>0xA4B93C</c> (<c>0xA4BA4C..0xA4BA54</c>).</summary>
    public float SendGain { get; set; } = 1f;
}

/// <summary>
/// The voice's table <c>[voice+0x10]</c> (count <c>[voice+0x14]</c>, capacity <c>[voice+0x18]</c>, 0x4C-byte entries): <c>AddSrc</c> allocates it with capacity 1; <c>0xA4B93C</c> (<see cref="WwiseVoiceBusPass.RefreshTailA4B9BC"/>) initialises entry 0 when the count is 0
/// (allocating a table first when the capacity is 0, <c>0xA4BB54..0xA4BC30</c>) and keeps entry 0's byte <c>+0x44</c> and float <c>+0x34</c> current.
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
    /// <c>0xA4BAF0..0xA4BB50</c> (the capacity is non-zero, so the table AddSrc allocated is used): <c>[voice+0x14] = 1</c> and entry 0 is initialised (<c>[+0x40] = -1</c>, byte <c>[+0x44] = 0</c>, <c>[+0x30] = 0</c>, <c>[+0]</c>, <c>[+4]</c>, <c>[+8] = 0</c>, <c>[+0x34]</c>, <c>[+0x38]</c>, <c>[+0x3C]</c>, <c>[+0x48] = 1.0f</c>). A table with
    /// no entry 0 (a null entry pointer: <c>0xA4BB14 adds r6,r8,r6,lsl#2; beq 0xA4BAA0</c>) returns false and the caller stops.
    /// </summary>
    public bool InitEntry0A4BB00()
    {
        Count = 1;                                                                  // 0xA4BB08 str r2,[r4,#0x14]
        if (Capacity == 0) return false;
        if (Entries.Count == 0) Entries.Add(new WwiseVoiceSendEntry());             // the 0x4C-byte entry the table holds (a fresh entry has the initialisation's values)
        Entries[0] = new WwiseVoiceSendEntry();                                     // 0xA4BB18..0xA4BB4C
        return true;
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

    /// <summary>
    /// <c>vt+0xC</c> (the in-place wrapper's <c>0xA7915C</c>, C38.1 P1-15): the release of the chain after the pull: the wrapper frees its own buffer <c>[W+0x2C]</c> and returns when it has one (false: the chain stops here), else it forwards to its
    /// upstream (true). The wrapper object is not built (the slot is a hook stand-in), so the hook is required when the release reaches a filled slot.
    /// </summary>
    public Func<bool>? ReleaseVtCHook { get; set; }

    /// <summary><c>vt+0xC</c>: see <see cref="ReleaseVtCHook"/>; true when the chain continues upstream.</summary>
    public bool ReleaseVtC() => (ReleaseVtCHook ?? throw new WwiseMissingBehaviourException(
        "M6-022 P1-15: the insert-FX wrapper's vt+0xC (0xA7915C) is not built; supply WwiseVoiceInsertFxSlot.ReleaseVtCHook"))();

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

    private IWwiseVoiceSource? _source;

    /// <summary>The source (<c>+0xD4</c>). The pitch node's upstream <c>[N+4]</c> follows it (the engine stores it at the connect step of <c>0xA54A30</c> and again in the pending-source arm <c>0xA52CB0</c>).</summary>
    public IWwiseVoiceSource? Source
    {
        get => _source;
        set
        {
            _source = value;
            PitchNode.Upstream = value as IWwisePitchNodeSource;
        }
    }

    /// <summary>The pending source (<c>+0xD8</c>), attached by the state-0x11 tail (V15).</summary>
    public IWwiseVoiceSource? Pending { get; set; }

    /// <summary>
    /// <c>voice+8</c> (M6-025 B11, C24 header, C25.4): AddSrc sets it to <c>[source+0xC]+0xC</c> = <c>pbi+0xC</c> on the
    /// new-voice path only (<c>0xA55934..0xA55948</c>), so it holds the owner PBI; the chain match in
    /// <c>0xA4304C</c> reads <c>[voice+8]+0x1BC</c> = the owner's <c>pbi+0x1C8</c> (<c>0xA430E8</c>).
    /// </summary>
    // fidelity: M6-025
    public object? BusOwner8 { get; set; }

    /// <summary>
    /// <c>voice+0xEC</c> (M6-025 B10): the engine pointer <c>0xA548B8</c> stores
    /// (<c>0xA548B8 str r1,[r0,#0xec]</c>), set from <c>AttachVoice</c>'s <c>0xA430A8</c> call.
    /// </summary>
    // fidelity: M6-025
    public object? EngineEC { get; set; }

    /// <summary>
    /// <c>voice+0xC</c>: the dry line <c>0xA4C280</c> stores for a connection to the main device (2,0) with
    /// <c>arg5 == 0</c> (C23 item 1 row 16 (c)); the first line in the <c>+0x1C8</c> chain with
    /// <c>+0x1CC</c> bit1 set, or null (row 16 (a)). Cleared again when the row-19 format check destroys
    /// the just-made dry connection (row 19).
    /// </summary>
    // fidelity: M6-025
    public WwiseMixBus? DryLineC { get; set; }

    /// <summary>The per-voice buffer (the native <c>params</c>).</summary>
    public WwiseVoiceBuffer Buffer { get; }

    /// <summary>Filter A (<c>+0x1C0</c>, M6-011): runs before the aux sends.</summary>
    public WwiseVoiceFilter FilterA { get; }

    /// <summary>Filter B (<c>+0x390</c>, M6-011): dry path only.</summary>
    public WwiseVoiceFilter FilterB { get; }

    /// <summary>The voice's pitch node <c>N = voice+0x100</c> (M6-022, M6-004): it owns the resampler, the held block and the output block, and runs the consumption of the sources' blocks (<c>0xA52D4C</c>).</summary>
    // fidelity: M6-022
    public WwisePitchNodeIntake PitchNode { get; }

    /// <summary>The voice-stage resampler <c>R = voice+0x108</c> (M6-004).</summary>
    public WwiseResampler Resampler => PitchNode.Resampler;

    /// <summary>
    /// <c>u32 [voice+0xEC]</c>: the mix rate the pitch node's resampler converts to (<c>0xA54A54</c> passes it to <c>0xA5321C</c>, <c>0xA52CF8</c> to the format change). <c>0xA548B8</c> stores <c>u32 [0x105243C]</c> there
    /// (<c>0xA430A4</c>: <c>0x1040068 -&gt; 0x105243C</c>, 48000 in the shipped data); <see cref="EngineEC"/> holds the engine pointer the earlier model named for it.
    /// </summary>
    // fidelity: M6-022
    public uint MixRateEC { get; set; } = WwiseRuntimeSettings.MixRateHz;

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

    /// <summary>
    /// <c>voice+0x2C..voice+0xCC</c> (C40.4 T-A6): the eight 0x14-byte entries <c>0x9D4228</c> merges the aux sends into; <see cref="CountCC"/> of them are live. The aux walk of the voice pass sums them (<c>0xA447D8..0xA44898</c>).
    /// </summary>
    // fidelity: M6-010, M6-022
    public WwiseAuxEntry[] AuxEntries2C { get; } = Enumerable.Range(0, 8).Select(_ => new WwiseAuxEntry()).ToArray();

    /// <summary>
    /// <c>voice+0xF0</c> (C25.5): a word, not an id. The voice ctor zeroes it (<c>0xA5470C..0xA54764</c>) and the voice
    /// init sets it to <c>[pbi+0x15C]</c> (<c>0xA54A64</c>, <c>0xA54B60</c>, <c>0xA54B70</c>; <see cref="WwisePlayingInstance.Word15C"/>).
    /// Its low byte is the input channel count <c>inCh</c> of the connection descriptor (<c>0xA54F28</c>, <c>0xA55020</c>,
    /// <c>0xA4BC74</c>).
    /// </summary>
    // fidelity: M6-025
    public uint Word0xF0 { get; set; }

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
        PitchNode = new WwisePitchNodeIntake { Voice = this };
        Buffer = new WwiseVoiceBuffer(channels, maxFrames);
        FilterA = new WwiseVoiceFilter(WwiseVoiceFilterRole.A);
        FilterB = new WwiseVoiceFilter(WwiseVoiceFilterRole.B);
    }

    /// <summary>
    /// <c>u16[0x1052440]</c> (.data, 1024 in this build; read by the engine oracle <c>emu_decode_cases.py pull_loop_cases</c>): the frames the voice asks the source for, written to <c>[state+0xC]</c> before every source
    /// call (<c>0xA4475C..0xA4479C</c>), whatever the voice's own buffer size.
    /// </summary>
    // fidelity: M6-022
    public const int PullFrames1052440 = 1024;

    /// <summary>
    /// V8 <c>0xA44630</c>. For a source of the engine classes (<see cref="IWwisePitchNodeSource"/> with a pitch node) the order is the engine's (research live-bodies-6 P01/P02, the verifier's read):
    /// <list type="number">
    /// <item><c>vt+0x38</c> of the insert-FX slots 3..0 (<c>0xA44650..0xA4466C</c>, an empty slot skipped, the result <c>[state+0x28]</c> then tested: 0x2B goes to the next lower slot, 0x2D / 0x11 on, anything else
    /// returns from the whole function: no notify, no mix); the slot 3 reaching 0x2D / 0x11 goes straight to filter A, a lower one to the <c>vt+0x3C</c> pass from the next slot up;</item>
    /// <item>the pitch pass <c>0xA53134(voice+0x100)</c> (<c>0xA44730..0xA44738</c>, <see cref="RunPitchPassA53134"/>): the pre-steps, then with frames held (<c>u16[node+0x6E] != 0</c>) the intake <c>0xA52D4C</c> and the source
    /// is not called, else the result 0x11 when the node's last-buffer byte is set;</item>
    /// <item>the result 0x2B runs the source loop <c>0xA4475C..0xA44788</c> (<see cref="PullFrames1052440"/> into <c>[state+0xC]</c>, the source's <c>vt+0x30</c>, a 0x2E calling <c>0xA55C14</c>, a result other than 0x11 / 0x2D
    /// returning, else the intake <c>0xA52D4C</c> and the loop again on 0x2B); a result other than 0x2D / 0x11 after the pass returns;</item>
    /// <item><c>vt+0x3C</c> of the slots 0..3 (<c>0xA44694..0xA446D8</c>; 0x2B sends the walk back down from the same index, <c>0xA4471C</c>);</item>
    /// <item>filter A <c>0xA4C60C(voice+0x1C0)</c> (<c>0xA446E0</c>), the ramps <c>0xA56E00(voice+0x380)</c> (<c>0xA446EC</c>);</item>
    /// <item><c>0xA548C0(voice, state)</c> (<c>0xA44700</c>, <see cref="RunA548C0"/>: not a decode: the play-position update, the clamp of <c>u16[state+0xE]</c> by the PBI's stop offset <c>vt+0x58</c> and the start of the
    /// pending source, <c>0xA548C0..0xA54954</c>), then a result other than 0x11 / 0x2D returns (<c>0xA44704..0xA44710</c>);</item>
    /// <item>the notify <c>0xA03E8C</c> (<c>0xA447D4</c>), then the aux and dry mixes.</item>
    /// </list>
    /// A source without a pitch node (a test double, the in-memory ADPCM class whose body is unread) and a voice without a source keep the earlier approximation, NOT the engine's order (MISSING: those classes' pitch nodes):
    /// filter A and the connection refresh first, the source called once (looped on 0x2B), the resampler step, then the notify and the mixes.
    /// </summary>
    /// <param name="notify">The V8 step 7 <c>0xA03E8C(manager, params)</c> listener notification; caller seam.</param>
    public void Render(Action<WwiseLiveVoice>? notify = null)
    {
        InsertFx?.Invoke(this);                                  // an extra caller hook, not an engine step
        if (Source is IWwisePitchNodeSource { HasPitchNode: true } pitchSource)
        {
            if (!RenderEngineOrder(pitchSource)) return;         // 0xA44714: the function returns: no notify, no mix
        }
        else
            RenderLegacyOrder();

        notify?.Invoke(this);                                    // 0xA447D4 bl 0xA03E8C

        MixConnections(Source is IWwisePitchNodeSource { HasPitchNode: true });
    }

    /// <summary>The engine's order of <c>0xA44630</c> up to the notify (see <see cref="Render"/>); false where the engine returns early.</summary>
    // fidelity: M6-022, M6-025
    private bool RenderEngineOrder(IWwisePitchNodeSource ps)
    {
        // The result [state+0x28] is the buffer's Result and the state's Code28 (one object, Buffer.State, as in the engine); the sources fill it, the pitch node copies its output into it, and the mixes (0xA4FBEC, MixA4FBEC) read it:
        // u16 [state+0xE] gates and pads, the bus frame count is mixed.
        int r4 = 4;                                              // 0xA44634
        int phase = 0;                                           // 0: the vt+0x38 walk down (0xA4463C), 1: the vt+0x3C walk up (0xA44694), 2: filter A (0xA446E0), 3: the pitch pass (0xA44730)
        while (true)
        {
            switch (phase)
            {
                case 0:
                {
                    if (r4 == 0) { phase = 3; break; }                              // 0xA4463C..0xA4464C
                    int r6 = r4 - 1;                                                // 0xA44654
                    var slot = InsertFxSlots[r4 - 1];                               // 0xA44650..0xA44658 [voice + 4 * r4 + 0x36c]
                    if (slot is null) { r4 = r6; break; }                           // 0xA4465C..0xA44660 -> 0xA44724
                    if (slot.Execute38Hook is null) throw new WwiseMissingBehaviourException("M6-022 V8: the insert-FX slot's vt+0x38 (0xA79A2C / the plug-in's) is not read; supply WwiseVoiceInsertFxSlot.Execute38Hook");
                    slot.Execute38(Buffer);                                         // 0xA44664..0xA44670 vt+0x38
                    int res = Buffer.Result;                                        // 0xA44674
                    if (res == 0x2B) { r4 = r6; break; }                            // 0xA44678..0xA4467C -> 0xA44724
                    if (res != 0x2D && res != 0x11) return false;                   // 0xA44680..0xA44688 -> 0xA44714
                    phase = r4 == 4 ? 2 : 1;                                        // 0xA4468C..0xA44690
                    break;
                }
                case 1:
                {
                    var slot = InsertFxSlots[r4];                                   // 0xA44694..0xA4469C [voice + 4 * r4 + 0x370]
                    if (slot is not null)
                    {
                        if (slot.Execute3CHook is null) throw new WwiseMissingBehaviourException("M6-022 V8: the insert-FX slot's vt+0x3C (0xA79A78 / the plug-in's) is not read; supply WwiseVoiceInsertFxSlot.Execute3CHook");
                        slot.Execute3C(Buffer);                                     // 0xA446B0..0xA446B8 vt+0x3C
                        int res = Buffer.Result;                                    // 0xA446BC
                        if (res == 0x2B) { phase = r4 == 0 ? 3 : 0; break; }        // 0xA446C0..0xA446C4 -> 0xA4471C: r4 == 0 to the pitch pass, else the walk down from r4
                        if (res != 0x11 && res != 0x2D) return false;               // 0xA446C8..0xA446D0 -> 0xA44714
                    }
                    r4++;                                                           // 0xA446D4
                    if (r4 == 4) phase = 2;                                         // 0xA446D8..0xA446DC
                    break;
                }
                case 2:
                {
                    if (Buffer.State.Data is { } filterData)                        // 0xA4C60C: returns when [state] == 0
                    {
                        var channel0 = (filterData as float[] ?? throw new InvalidOperationException("filter A reads planar float[] data")).AsSpan(0, Math.Min(Buffer.State.MaxFrames, ((float[])filterData).Length));
                        FilterA.Process(channel0);                                  // 0xA446E0 0xA4C60C(voice+0x1C0, state) (the frame count of 0xA766B8 is not adopted: the whole channel 0 of u16[state+0xC] frames, as before)
                    }
                    GainStageA56E00();                                              // 0xA446EC 0xA56E00(voice+0x380, state)
                    RunA548C0(Source as IWwisePitchNodeSource ?? throw new WwiseMissingBehaviourException("M6-022 R5.3: 0xA548C0 reads [[voice+0xD4]+0xC]; the current source is not a pitch-node source"));   // 0xA44700 bl 0xA548C0(voice, state)
                    int res = Buffer.Result;                                        // 0xA44704
                    return res == 0x11 || res == 0x2D;                              // 0xA44708..0xA44710 beq 0xA447C4 (notify); else 0xA44714
                }
                default:
                {
                    RunPitchPassA53134();                                           // 0xA44730..0xA44738 0xA53134(voice+0x100, state)
                    int res = Buffer.Result;                                        // 0xA4473C
                    if (res == 0x2B)                                                // 0xA44740..0xA44744 -> 0xA4475C
                    {
                        if (!PullSourceLoop()) return false;
                        res = Buffer.Result;
                    }
                    if (res != 0x2D && res != 0x11) return false;                   // 0xA44748..0xA44750 -> 0xA44714
                    r4 = 0;                                                         // 0xA44754
                    phase = 1;                                                      // 0xA44758 b 0xA44694
                    break;
                }
            }
        }
    }

    /// <summary>
    /// <c>0xA56E00(voice+0x380, state)</c> (V2-09, C38.3): returns when <c>[state] == 0</c> or <c>[[voice+0x388]+0x34] == 0</c> (<c>pbi+0x34</c> is null for a non-positioned sound: every shipped Sound carries a non-3D positioning byte, C32.1); otherwise it runs the
    /// per-connection gain / ramp stage <c>0xA56A7C</c>, which is not read (a visible stop). <c>[voice+0x388]</c> is the voice's PBI (<c>0xA549A0</c> stores it): the pitch node's owner PBI <c>[N+0xB4]</c> stands for it. The earlier model refreshed every connection's gain
    /// pair and matrices here (<see cref="WwiseVoiceConnection.Refresh"/>); the engine does not: the connections' gains and matrices are the host's inputs to the mix.
    /// </summary>
    // fidelity: M6-022
    private void GainStageA56E00()
    {
        if (Buffer.State.Data is null) return;                                       // 0xA56E00..0xA56E08
        var pbi = PitchNode.Pbi ?? throw new WwiseMissingBehaviourException("M6-022 V2-09: 0xA56E00 reads [[voice+0x388]+0x34]; the voice's PBI is not set (the pitch node's owner stands for it)");
        if (pbi.Field34 == 0) return;                                                // 0xA56E10..0xA56E1C
        throw new WwiseMissingBehaviourException("M6-022 V2-09: 0xA56A7C (the per-connection gain / ramp stage of a positioned sound, [pbi+0x34] != 0) is not read");
    }

    /// <summary>The source loop <c>0xA4475C..0xA44788</c>; false where the engine returns (<c>0xA44714</c>), true when it reaches <c>0xA44748</c> with the result left in the buffer. The source called is always the current <c>[voice+0xD4]</c> (a pending-source switch inside the intake replaces it).</summary>
    private bool PullSourceLoop()
    {
        while (true)
        {
            var src = Source as IWwisePitchNodeSource ?? throw new WwiseMissingBehaviourException("M6-022 V8: the voice's current source [voice+0xD4] is not a pitch-node source");
            Buffer.ValidFrames = PullFrames1052440;                                 // 0xA4478C..0xA4479C: [state+0xC] = u16[0x1052440]
            src.Io = Buffer.State;                                                  // the sources fill the voice pass block (the engine hands the one block to every vt+0x30)
            src.Render(Buffer);                                                     // 0xA447A4 vt+0x30
            if (Buffer.Result == 0x2E)                                              // 0xA447A8..0xA447AC
            {
                (SourceNotReadyA55C14 ?? throw new WwiseMissingBehaviourException(
                    "M6-025 P02: a source result of 0x2E calls 0xA55C14(voice) (0xA447B8), which posts 0xA0428C; supply WwiseLiveVoice.SourceNotReadyA55C14"))(this, Source!);
            }
            int res = Buffer.Result;                                                // 0xA447BC / 0xA447A8
            if (res != 0x11 && res != 0x2D) return false;                           // 0xA44768..0xA44778 -> 0xA44714
            RunIntake();                                                            // 0xA4477C bl 0xA52D4C
            if (Buffer.Result != 0x2B) return true;                                 // 0xA44780..0xA44788 bne 0xA44748
        }
    }

    private void RunIntake() => PitchNode.IntakeA52D4C(Buffer.State);

    /// <summary>
    /// The pitch pass <c>0xA53134(node, state)</c> (V10, EXACT_SOURCE, <c>0xA53134..0xA531B0</c>), in the engine's order: <c>byte [node+0xB9] = 0</c> (<c>0xA5314C</c>); <c>[node+0x48] = u16[state+0xC]</c> (<c>0xA53144</c>,
    /// <c>0xA53154</c>); the source's <c>vt+0x20</c> (<c>0xA53158..0xA5315C</c>, <c>0xA5668C</c>: <c>[[src+0xC]+0x44]</c>, the owner PBI's pitch in cents); <c>0xA47384(node+8, that value, (u16[[node+0xB4]+0x1BE] &amp; 0x380) == 0)</c>
    /// (<c>0xA53160..0xA53180</c>, <see cref="WwiseResampler.SetPitch"/>); then, with <c>u16[node+0x6E] != 0</c>, the tail call to <c>0xA52D4C</c> (<c>0xA531A4..0xA531B0</c>), else, with <c>byte [node+0xB8] != 0</c>,
    /// <c>[state+0x28] = 0x11</c> (<c>0xA53190..0xA5319C</c>).
    /// </summary>
    // fidelity: M6-022
    private void RunPitchPassA53134()
    {
        var node = PitchNode;
        node.ByteB9 = 0;                                                            // 0xA5314C strb 0,[node+0xB9]
        node.Word48 = unchecked((ushort)Buffer.ValidFrames);                        // 0xA53144 ldrh [state+0xC]; 0xA53154 str [node+0x48]
        var upstream = node.Upstream ?? throw new WwiseMissingBehaviourException(
            "M6-022 V10: the pitch pass calls [node+4]->vt+0x20 (0xA5668C: [[src+0xC]+0x44]); the node has no upstream source");
        float pitch = (upstream.Owner ?? throw new WwiseMissingBehaviourException(
            "M6-022 V10: the source's owner PBI [src+0xC] is not set (vt+0x20 = 0xA5668C reads its +0x44)")).Pitch44;   // 0xA53158..0xA5315C
        var pbi = node.Pbi ?? throw new WwiseMissingBehaviourException("M6-022 V10: the pitch pass reads u16[[node+0xB4]+0x1BE]; the node's owner PBI is not set");
        bool interp = (((pbi.Flags1BE) | (pbi.Flags1BF << 8)) & 0x380) == 0;       // 0xA53160..0xA53174 u16[pbi+0x1BE] & 0x380 == 0 (bits 7..9)
        node.Resampler.SetPitch(pitch, interp);                                     // 0xA53180 bl 0xA47384
        if (node.HeldFrames6E != 0)                                                 // 0xA53184..0xA5318C ldrh [node+0x6E]; bne 0xA531A4
            RunIntake();                                                            // 0xA531A4..0xA531B0 tail call 0xA52D4C
        else if (node.ByteB8 != 0)                                                  // 0xA53190..0xA53194
            Buffer.Result = 0x11;                                                   // 0xA53198..0xA5319C str 0x11,[state+0x28]
    }

    /// <summary>
    /// The release chain after the pull (<c>0xA5495C -> [voice+0x1C0]->vt+0xC</c>, P1-15): filter A's <c>vt+0xC</c> (<c>0xA56718</c>) forwards to its upstream, the in-place FX wrappers' <c>vt+0xC</c> (<c>0xA7915C</c>: a wrapper holding its own buffer
    /// frees it and ends the chain, else forwards), and the chain ends at the pitch node's <see cref="WwisePitchNodeIntake.ReleaseBufferA52800"/>. The order of the slots (3 down to 0, the pull order of V2-01) is read from that order; the
    /// <c>[W+4]</c> links the voice init stores (<c>vt+0x24</c>, <c>0xA54A30</c>) are not traced.
    /// </summary>
    // fidelity: M6-022
    public void ReleaseChainVtC()
    {
        for (int i = InsertFxSlots.Length - 1; i >= 0; i--)
        {
            var slot = InsertFxSlots[i];
            if (slot is null) continue;
            if (!slot.ReleaseVtC()) return;
        }
        PitchNode.ReleaseBufferA52800();
    }

    /// <summary>
    /// The play-position repository <c>G = *0x108D8F8</c> (GOT <c>0x1040150</c>) that <c>0xA548C0</c> hands to <c>0xA05574</c> (<c>0xA548EC..0xA54900</c>). Required when a PBI with the callback flag <c>0x100000</c> is mixed.
    /// </summary>
    // fidelity: M6-022
    public WwisePlayPositionRepository? PositionRepository { get; set; }

    /// <summary>
    /// The writer of the format bytes <c>pbi+0x158..0x162</c> that the non-streamed source classes' <c>vt+0x28</c> (StartStream) perform (<c>0xA72760..0xAB138C</c>, C26.5; the bridge's <c>SourceFormatWriter15C</c>); required by
    /// <see cref="RunA548C0"/> when it starts a pending source of such a class. A streamed Vorbis source writes the bytes itself.
    /// </summary>
    // fidelity: M6-025
    public Action<WwisePlayingInstance, IWwiseVoiceSource>? StartStreamFormatWriter { get; set; }

    /// <summary>
    /// <c>0xA548C0(voice, mix)</c> (C31 R5.3 with the verifier's R4.8 correction; <c>0xA548C0..0xA54954</c>, called at <c>0xA44700</c>): <c>pbi = [[voice+0xD4]+0xC]</c> (the current source's owner); with <c>[pbi+4] &amp; 0x100000</c> and
    /// <c>[mix+0x18] != -1</c> the play-position update <c>0xA05574(G, [pbi+0x140], mix+0x18, [voice+0xD4])</c>; then <c>r0 = pbi vt+0x58()</c> (<c>0x9CBACC</c>: <c>[pbi+0x1F8]</c>, stored back as -1), and when <c>r0 != -1</c>: an UNSIGNED 32-bit compare
    /// of the whole <c>r0</c> against the zero-extended <c>u16[mix+0xE]</c> followed by a halfword store of <c>r0</c> when lower, and byte <c>[mix+0x2C] = 1</c> whenever <c>r0 != -1</c>; then with <c>[voice+0xD8] != 0</c>
    /// <c>0xA56650([voice+0xD8], [pbi'+0x1DC], [pbi'+0x1E0])</c> (<c>0xA54948</c>, pbi' the pending source's owner), a result of 2 storing <c>[mix+0x28] = 2</c>. The mix block is the source's io state (<see cref="IWwisePitchNodeSource.Io"/>, the
    /// voice's <c>state</c>) whose result word is also <see cref="WwiseVoiceBuffer.Result"/>; <c>[mix+0x2C]</c> is <see cref="WwiseVoiceBuffer.HasBusParam"/>.
    /// <para>MISSING (visible stops): <c>[state+0x1C]</c> (<c>0xA05574</c>'s second info word) is uninitialised stack in the engine unless a source writes it (<see cref="WwiseDecodeState.Word1CWritten"/>); the owner of a pending source that is not a
    /// pitch-node source; the format writer of a non-streamed pending source (<see cref="StartStreamFormatWriter"/>).</para>
    /// </summary>
    // fidelity: M6-022, M6-025
    private void RunA548C0(IWwisePitchNodeSource ps)
    {
        var pbi = ps.Owner ?? throw new WwiseMissingBehaviourException(
            "M6-022 R5.3: 0xA548C0 reads pbi = [[voice+0xD4]+0xC] (0xA548C0..0xA548CC); the current source has no owner PBI");
        var io = Buffer.State;                                                      // r5 = the mix block (state)
        if ((pbi.Flags4 & 0x100000) != 0 && io.Position != 0xFFFFFFFF)              // 0xA548D4..0xA548E8 tst [pbi+4],#0x100000; cmn [mix+0x18],#1
        {
            if (!io.Word1CWritten)
                throw new WwiseMissingBehaviourException(
                    "M6-022 R5.3: 0xA05574 copies the 16 bytes at mix+0x18; [mix+0x1C] is uninitialised stack in the voice pass block (0xA44A00..0xA44A48 never store it) and no adopted source writes it");
            (PositionRepository ?? throw new WwiseMissingBehaviourException(
                "M6-022 R5.3: 0xA05574(G = *0x108D8F8, ...) (0xA548EC..0xA54900) needs the play-position repository; supply WwiseLiveVoice.PositionRepository"))
                .UpdateA05574(pbi.PlayingId, io.Position, io.Word1C, io.Total, io.Rate, ps);   // 0xA548F4 ldr r1,[r4,#0x140]; 0xA548F0 add r2,r5,#0x18; r3 = [voice+0xD4]
        }
        uint r0 = pbi.TakeStopOffset9CBACC();                                       // 0xA54904..0xA54910 vt+0x58 = 0x9CBACC
        if (r0 != 0xFFFFFFFF)                                                       // 0xA54914 cmn r0,#1; beq 0xA54930
        {
            ushort r3 = io.ValidFrames;                                             // 0xA5491C ldrh r3,[r5,#0xe]
            if (r0 < r3) io.ValidFrames = unchecked((ushort)r0);                    // 0xA54920 cmp r0,r3 (unsigned, 32 bit); 0xA54928 strhlo r0,[r5,#0xe]
            Buffer.HasBusParam = true;                                              // 0xA54924 mov r3,#1; 0xA5492C strb r3,[r5,#0x2c] (unconditional here)
        }
        if (Pending is not { } pending) return;                                     // 0xA54930..0xA54938 ldr r0,[r6,#0xd8]; cmp r0,#0
        var owner = (pending as IWwisePitchNodeSource)?.Owner ?? throw new WwiseMissingBehaviourException(
            "M6-022 R5.3: the pending source's owner [[voice+0xD8]+0xC] (0xA5493C) is only available for a pitch-node source");
        int result = WwiseVoiceSourceStart.StartA56650(pending, owner.Read1DC(), owner.Read1E0(), out bool ran);   // 0xA54940..0xA54948
        if (ran && pending is not IWwiseStreamingVoiceSource { WritesSourceFormatInStartStream: true })
            (StartStreamFormatWriter ?? throw new WwiseMissingBehaviourException(
                "M6-025 C26.5: the StartStream writers of pbi+0x158..0x162 (0xA72760..0xAB138C) are not built for the pending source's class; supply WwiseLiveVoice.StartStreamFormatWriter")).Invoke(owner, pending);
        if (result == 2)                                                            // 0xA5494C cmp r0,#2; 0xA54950 streq r0,[r5,#0x28]
        {
            Buffer.Result = 2;
            io.Code28 = 2;
        }
    }

    /// <summary>
    /// Opt-in to the earlier approximation of the render order (see <see cref="Render"/>) for a voice whose source has no pitch node (a test double, the in-memory ADPCM class whose body is unread) or that has no source.
    /// It is NOT the engine's order and is MISSING; without this flag such a voice throws <see cref="WwiseMissingBehaviourException"/> at the render instead of running silently. Only tests set it.
    /// </summary>
    public bool AllowRenderOrderApproximation { get; set; }

    /// <summary>The earlier approximation for a voice whose source has no pitch node (see <see cref="Render"/>): NOT the engine's order.</summary>
    private void RenderLegacyOrder()
    {
        if (!AllowRenderOrderApproximation)
            throw new WwiseMissingBehaviourException(
                "M6-022 V8: this voice's source has no pitch node (or there is no source), so the engine's render order 0xA44630 is not reproduced for it (the in-memory ADPCM class 0xA72A2C is unread); set WwiseLiveVoice.AllowRenderOrderApproximation only for a test that accepts the approximation");
        // insert-FX slots vt+0x38 from 3..0, then vt+0x3c from 0..3, unconditionally (NOT the engine's gated walk).
        for (int i = 3; i >= 0; i--) InsertFxSlots[i]?.Execute38(Buffer);
        for (int i = 0; i < 4; i++) InsertFxSlots[i]?.Execute3C(Buffer);

        FilterA.Process(Buffer.Channels[0]);                     // filter A (M6-011)

        // gain/ramp 0xA56E00(voice+0x380) refreshes each connection's ramp (M6-012).
        foreach (var connection in Connections)
            connection.Refresh();

        if (Source is not null)
        {
            do
            {
                Buffer.ValidFrames = Buffer.MaxFrames;           // the earlier approximation (C12 X4: 0x400), not u16[0x1052440]: the mixes below read it as the frame count
                Buffer.Result = Source.Render(Buffer);
                if (Buffer.Result == 0x2E)
                    (SourceNotReadyA55C14 ?? throw new WwiseMissingBehaviourException(
                        "M6-025 P02: a source result of 0x2E calls 0xA55C14(voice) (0xA447B8), which posts 0xA0428C; supply WwiseLiveVoice.SourceNotReadyA55C14"))(this, Source);
            }
            while (Buffer.Result == 0x2B);
        }
        else
        {
            Buffer.ValidFrames = Buffer.MaxFrames;               // V8: params+0xC = 0x400 (C12 X4)
        }

        if (Buffer.Result is 0x11 or 0x2D && Source is not null && Source.Channels == 1)
            ApplyResampler();
    }

    /// <summary>
    /// The aux-send and dry mix walks after the notify (<c>0xA447D8..0xA44938</c>). The engine order mixes the voice's pass block through <see cref="WwiseVoiceConnection.MixA4FBEC"/>: the aux walk (<c>[conn+0x68] != 0</c>,
    /// <c>[conn+0x18] != 0</c>, <c>([conn+0x6C] &amp; 6) != 6</c>) passes the gains <c>{g0, g1}</c>, which start at 0.0f and sum, over the <c>[voice+0xCC]</c> entries of <c>voice+0x2C</c> whose id equals <c>0xA68A2C(line+0x4C)</c>, the entry's current (g0) and target (g1) in float32 (C40.4 T-A8,
    /// <c>0xA44850..0xA44894</c>; the matching key is the line context's <see cref="WwiseBusContext.Key"/>),
    /// <c>[voice+0xCC] != 0</c>, throws); the dry walk (<c>[conn+0x68] == 0</c>) passes <c>{1.0f, 1.0f}</c> and runs filter B once before the first dry mix (<c>0xA4492C</c>).
    /// </summary>
    internal void MixConnections(bool engineOrder)
    {
        if (engineOrder && CountCC > 8)
            throw new WwiseMissingBehaviourException("M6-010 T-A8: [voice+0xCC] above 8 makes the aux walk read past the 8 entries of voice+0x2C (0xA44850..0xA44894); nothing writes it above 8");
        // the aux-send walk 0xA4FBEC for the connections whose conn+0x68 is set, skipping a connection with ([conn+0x6C]&6)==6.
        for (int ci = 0; ci < Connections.Count; ci++)
        {
            var connection = Connections[ci];
            // C24.4 0xA447EC..0xA4480C: [conn+0x68] != 0, [conn+0x18] != 0, ([conn+0x6C] & 6) != 6.
            if (!connection.HasAux || !connection.HasDry) continue;
            if ((connection.Flags6C & 6) == 6) continue;
            if (!engineOrder) { connection.Mix(Buffer); continue; }
            // C40.4 T-A8, 0xA44814..0xA44898: g = {0.0f, 0.0f}; for each of [voice+0xCC] entries of voice+0x2C whose id [e+0xC] equals 0xA68A2C(line+0x4C) (the line's context key: the bus id, or -(byte) without a bus):
            // g[1] += [e+0] (the target), g[0] += [e+4] (the current), float32 adds in entry order; then 0xA4FBEC(line, S, conn, &g).
            uint key = connection.Bus.Context.Key;                                   // 0xA68A2C(line+0x4C) = [bus+8], or -(u8)byte
            float g0 = 0f, g1 = 0f;                                                  // 0xA44814 vldr s15,[pc,#0x120] (0.0f); vstr [sp+8], [sp+0xC]
            for (int i = 0; i < CountCC; i++)                                        // 0xA44828..0xA44894 (the count byte is re-read each pass, 0xA4488C)
            {
                var e = AuxEntries2C[i];
                if (e.Id != key) continue;                                           // 0xA44860 cmp sl,r0; bne 0xA44840
                g1 = WwiseArmFloat.Add(g1, e.Target);                                // 0xA44878 vadd.f32 s14,s14,s13 -> [sp+0xC] (VFP NaN rules)
                g0 = WwiseArmFloat.Add(g0, e.Current);                               // 0xA44884 vadd.f32 s15,s15,s14 -> [sp+8]
            }
            WalkTrace?.Invoke($"M {ci} {BitConverter.SingleToUInt32Bits(g0):x} {BitConverter.SingleToUInt32Bits(g1):x}");
            connection.MixA4FBEC(Buffer.State, g0, g1);                              // 0xA44898..0xA448A8 bl 0xA4FBEC(sb, r5, r6, &g)
        }

        // filter B 0xA4C60C(voice+0x390) before the first dry mix, then the dry-mix walk with gain 1.0.
        bool firstDry = true;
        for (int ci = 0; ci < Connections.Count; ci++)
        {
            var connection = Connections[ci];
            // C24.4 0xA448FC..0xA4491C: [conn+0x68] == 0 and the same two tests.
            if (connection.HasAux || !connection.HasDry) continue;
            if ((connection.Flags6C & 6) == 6) continue;
            if (firstDry)
            {
                if (engineOrder) WalkTrace?.Invoke("F");                            // 0xA4492C add r0,r7,#0x390; 0xA44934 bl 0xA4C60C (the trace is called before the filter runs)
                if (!engineOrder) FilterB.Process(Buffer.Channels[0]);
                else if (Buffer.State.Data is float[] filterBData)                  // 0xA4C60C returns when [state] == 0
                    FilterB.Process(filterBData.AsSpan(0, Math.Min(Buffer.State.MaxFrames, filterBData.Length)));
                firstDry = false;
            }
            if (!engineOrder) connection.Mix(Buffer);
            else
            {
                WalkTrace?.Invoke($"M {ci} {BitConverter.SingleToUInt32Bits(1f):x} {BitConverter.SingleToUInt32Bits(1f):x}");
                connection.MixA4FBEC(Buffer.State, 1f, 1f);                         // 0xA448C4, 0xA448E0..0xA448EC: g = {1.0f, 1.0f}
            }
        }
    }

    /// <summary>A test hook: <see cref="MixConnections"/> reports each mix it makes (<c>M &lt;connection index&gt; &lt;g0 bits&gt; &lt;g1 bits&gt;</c>) and filter B's one call (<c>F</c>), in the engine's call order.</summary>
    internal Action<string>? WalkTrace { get; set; }

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
    /// <c>0xA55C14(voice)</c> (P02, the same body as <c>0xA54580</c>): with bit 1 of <c>[source+0x10]</c> set it returns; else <c>byte [voice+0xE8] |= 1</c> and <c>0xA0428C(mgr, [pbi+0x134], 0x9BD138(pbi))</c> with
    /// <c>pbi = [voice+8]</c>. Called when the source's result is 0x2E; required then (what <c>0xA0428C</c> posts is unread: <see cref="WwiseVoiceLinker"/> reaches it through the same seams).
    /// </summary>
    // fidelity: M6-025
    public Action<WwiseLiveVoice, IWwiseVoiceSource>? SourceNotReadyA55C14 { get; set; }

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

    /// <summary>
    /// <c>voice vt+0x48</c> = <c>0xA533FC</c> (M6-026 7.6): with a current source, <c>0xA565D0(src)</c> = <c>0xA01840([src+0xC])</c> clears <c>pbi.1BA</c> bits 3..6; then the state is 2 (stopped).
    /// The owner is the current source's (<c>[src+0xC]</c>), read through <paramref name="ownerOfSource"/>, not <c>voice+8</c>.
    /// </summary>
    // fidelity: M6-026
    public void StopA533FC(Func<IWwiseVoiceSource, WwisePlayingInstance> ownerOfSource)
    {
        ArgumentNullException.ThrowIfNull(ownerOfSource);
        if (Source is { } src)                                       // 0xA565D0([voice+0xD4]): pbi = [src+0xC], then 0xA01840(pbi)
        {
            var pbi = ownerOfSource(src);
            pbi.Flags1BA = (byte)(pbi.Flags1BA & ~0x78);
        }
        State = 2;
    }

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

    /// <summary>V7 <c>[voice+0x1B4]</c>: when non-zero the insert-FX/start build is skipped.</summary>
    public bool Has1B4 { get; set; }

    /// <summary>V7-f: the four insert-FX slots <c>voice+0x370..0x37C</c> (V12).</summary>
    public WwiseVoiceInsertFxSlot[] InsertFxSlots { get; } = new WwiseVoiceInsertFxSlot[4];

    /// <summary>V7-f <c>0xA5321C(voice+0x100,...)</c>: a host's own resampler/pitch start (returns true on 1); unset, <see cref="StartResamplerA5321C"/> runs the engine's.</summary>
    public Func<bool>? StartResampler5321C { get; set; }

    /// <summary>
    /// <c>0xA54A3C..0xA54A78</c> -> <c>0xA5321C(voice+0x100, F2, pbi, [voice+0xEC])</c>: <c>pbi = [[voice+0xD4]+0xC]</c> and <c>F2</c> its 12 bytes at <c>+0x158</c> (the rate, the channel word <c>+0x15C</c> and the format word <c>u16 +0x160</c>); returns
    /// true when the node's resampler init returned 1.
    /// </summary>
    // fidelity: M6-022
    public bool StartResamplerA5321C()
    {
        var ps = Source as IWwisePitchNodeSource ?? throw new WwiseMissingBehaviourException("M6-022 V7-f: 0xA54A30 starts the pitch node with the current source's owner PBI ([[voice+0xD4]+0xC]); the current source is not a pitch-node source");
        var pbi = ps.Owner ?? throw new WwiseMissingBehaviourException("M6-022 V7-f: the current source has no owner PBI [src+0xC]");
        var f2 = new WwiseResamplerFormat(pbi.Byte160 | (pbi.Byte161 << 8), (byte)pbi.Word15C, unchecked((int)pbi.SourceFormat158), pbi.Word15C);   // 0xA54A50..0xA54A68 ldm [pbi+0x158]
        return PitchNode.InitA5321C(f2, pbi, MixRateEC) == 1;                       // 0xA54A54 ldr r3,[r0,#0xec]; 0xA54A78 bl 0xA5321C
    }

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

    /// <summary>V7 <c>0xA4B4B0(voice)</c> ducking apply; see <see cref="WwiseVoiceBusPass.ApplyDucking"/>.</summary>
    public Action? ApplyDuckingHook { get; set; }

    /// <summary>The table <c>[voice+0x10]</c> (<see cref="WwiseVoiceSendTable"/>).</summary>
    public WwiseVoiceSendTable? SendTable { get; set; }

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
    public List<WwiseLiveVoice> Voices { get; set; } = new();

    /// <summary>
    /// The deferred PBI-notification queue <c>Q</c> (<c>0x108DE78</c>; V21, C34.3 S8) the flush <c>0xA38420</c> drains. Its init is unread, so the host supplies it; <see cref="FlushPbiNotifications"/> needs it.
    /// Replaces the old <c>Queue&lt;WwisePbiNotification&gt;</c>.
    /// </summary>
    public WwiseNotificationQueue? Notifications { get; set; }

    /// <summary>
    /// <c>0xA0188C(pbi, code, r2, r3)</c>, the per-item handler the flush calls first (<c>0xA38480</c>, V21). Its body is the bridge's <see cref="WwisePlaybackBridge.HandleNotificationA0188C"/>; required by the flush.
    /// </summary>
    public Action<object?, int, int, int>? NotificationHandlerA0188C { get; set; }

    /// <summary>The code-4 teardown of the flush (<c>0xA384C8..0xA38508</c>: unlink, <c>0x9D3470</c>, <c>vt+0x10</c>, <c>vt+4</c>, free): the bridge's <see cref="WwisePlaybackBridge.TerminatePbi"/>; required for a code-4 item.</summary>
    public Action<object?>? TerminateNotifiedPbiA384C8 { get; set; }

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
    /// <item><b>0xA43D24</b> the ducking/volume pre-pass (V5a): <c>0xA55750</c> per active voice (built since batch 5f, C37.1: <see cref="PrePassVoicesA43D24"/>, <see cref="PrePassVoiceA55750"/>,
    /// <see cref="RefreshVoiceGainA4B93C"/>), then the per-bus <c>+0x88/+0x8C</c> dB/linear, <c>0xA4AF50</c> per voice, <c>0xA437E0</c> flagged buses descending,
    /// <c>0xA4B4B0</c> per voice, which are not adopted: the required <see cref="DuckPrePassTailA43D6C"/> seam.</item>
    /// <item><b>0xA39564</b> the node cleanup (V5b): clear bit 2 of <c>[node+0x1BE]</c> on the list
    /// <c>0x108DEC8</c>, optionally <c>0xA00494</c> per node, then <c>0x9F3BA4</c> per array element. The
    /// node/array objects are a caller seam.</item>
    /// </list>
    /// </summary>
    public void VoicePass()
    {
        // fidelity: M6-022, M6-025, M6-026
        // 0xA44978 bl 0x9D3CC0, 0xA4497C bl 0xA43D24, 0xA44980 bl 0xA39564, unconditionally and in this order (C24.1, C30). Each is a
        // REQUIRED collaborator: a missing one throws and is never skipped, because the engine runs all three every pass.
        (AdvanceTickCounters ?? throw new WwiseMissingBehaviourException(
            "M6-022 V5 / M6-025 C24.1: 0x9D3CC0 (the pending-voice walk, WwisePlaybackBridge.WalkPendingVoices) runs at 0xA44978 on every voice pass; supply AdvanceTickCounters"))();
        PrePassA43D24();                                             // 0xA4497C bl 0xA43D24
        (NodeCleanup ?? throw new WwiseMissingBehaviourException(
            "M6-026 E1: 0xA39564 (WwisePlaybackLimiter.PerFrameA39564, called at 0xA44980) runs on every voice pass; supply NodeCleanup"))();

        VoicesRendered = 0;
        int i = 0;
        while (i < Voices.Count)                                     // V6: head [0x108DF64], next +0xD0
        {
            var voice = Voices[i];
            // fidelity: M6-026 (7.2): per voice the block starts with result 0x2B (AK_DataNeeded) and the mix-result byte 0.
            voice.Buffer.InitPassBlockA44A00();                      // 0xA44A00..0xA44A48 (result 0x2B, the mix-result byte 0, max frames 1024)
            if (voice.State == 1 && RunVoiceStateMachine(voice))     // V7: returns 1 when the voice has a live source
            {
                voice.Render(_notify);                               // V8: 0xA44630
                VoicesRendered++;
                PostMix(voice);                                      // 7.2: 0xA55CC4 on 0x2E, else 0xA5495C (0xA44B80..0xA44BA8)
            }
            StopDecisionA44A58(voice);                                   // 7.3..7.6
            if (voice.State == 2)                                    // 7.7: unlink, count--, 0x9D40C4
            {
                Voices.RemoveAt(i);
                (DestroyVoiceA9D40C4 ?? throw new WwiseMissingBehaviourException(
                    "M6-026 7.7: 0x9D40C4 (the voice teardown, WwiseVoiceLinker.TeardownVoice) is needed for a stopped voice; supply DestroyVoiceA9D40C4"))(voice);
            }
            else i++;
        }
    }

    /// <summary>The owner PBI of a source (<c>[source+0xC]</c>), for <c>[[voice+0xD4]+0xC]</c> in the stop decision (7.3) and the stop (7.6). Required; wire it to <see cref="WwisePlaybackBridge.TryOwnerOf"/>.</summary>
    // fidelity: M6-026
    public Func<IWwiseVoiceSource, object?>? SourceOwner { get; set; }

    private WwisePlayingInstance OwnerOfSourceRaw(IWwiseVoiceSource source)
        => (SourceOwner ?? throw new WwiseMissingBehaviourException(
            "M6-026 7.3: [[voice+0xD4]+0xC] (the current source's owner) needs the source-owner lookup; supply SourceOwner"))(source) as WwisePlayingInstance
           ?? throw new WwiseMissingBehaviourException("M6-026 7.3: the current source has no owner PBI ([src+0xC]); the engine would dereference it");

    private WwisePlayingInstance OwnerOfSource(WwiseLiveVoice voice)
        => OwnerOfSourceRaw(voice.Source ?? throw new WwiseMissingBehaviourException(
            "M6-026 7.3: the voice has no current source ([voice+0xD4] == 0); the engine dereferences it (0xA44A50)"));

    /// <summary><c>0x9D40C4(voice, 0)</c> (7.7): the destroy of a voice the pass stopped. Required when a voice reaches state 2.</summary>
    // fidelity: M6-026
    public Action<WwiseLiveVoice>? DestroyVoiceA9D40C4 { get; set; }

    /// <summary><c>voice vt+0x4C</c> = <c>0xA53558</c> (7.5, 7.6: the pause of a paused-and-running PBI). Its state-1 body (<c>0xA565D8</c>, <c>0xA052F4</c>) is unread; required when reached.</summary>
    // fidelity: M6-026
    public Action<WwiseLiveVoice>? PauseVoice4C { get; set; }

    /// <summary>
    /// The <c>NoMoreData</c> continuation with a pending source (7.5): <c>[voice+0xD8] = 0</c>, <c>0xA55D04(voice, 0)</c>, <c>0xA55A84(voice, fp, 1, 0) == 1</c> and <c>0xA54A30(voice) == 1</c> -> <c>0xA56478(fp)</c> and
    /// continue (true), else stop (false). The bodies are RECOVERABLE_GAP, so it is a required seam.
    /// </summary>
    // fidelity: M6-026
    public Func<WwiseLiveVoice, IWwiseVoiceSource, bool>? ContinueWithPendingSource { get; set; }

    /// <summary><c>0xA55CC4(voice, blk)</c>, run after a mix whose result is <c>0x2E</c> (AK_NoDataReady) (7.2, <c>0xA44B80..0xA44BA8</c>). RECOVERABLE_GAP; required then.</summary>
    // fidelity: M6-026
    public Action<WwiseLiveVoice, WwiseVoiceBuffer>? PostMixNoDataReadyA55CC4 { get; set; }

    /// <summary><c>0xA5495C(voice)</c>, run after a mix with any other result (7.2, <c>0xA44B80..0xA44BA8</c>). RECOVERABLE_GAP; required then.</summary>
    // fidelity: M6-026
    public Action<WwiseLiveVoice>? PostMixA5495C { get; set; }

    private void PostMix(WwiseLiveVoice voice)
    {
        if (voice.Buffer.Result == 0x2E)
            (PostMixNoDataReadyA55CC4 ?? throw new WwiseMissingBehaviourException(
                "M6-026 7.2: 0xA55CC4 (after a 0x2E mix) is RECOVERABLE_GAP; supply PostMixNoDataReadyA55CC4"))(voice, voice.Buffer);
        else
            (PostMixA5495C ?? (v => WwisePlaybackBridge.PostMixA5495C(v)))(voice);   // 0xA5495C: the release chain to the pitch node (C38.1 P1-15) unless a host supplies its own
    }

    /// <summary>
    /// The stop decision of <c>0xA44948</c> for one voice (7.3, 7.5, 7.6). <c>sl</c> is <c>(1BC &amp; 0x20) ? ([pbi+0x1F8] == -1) : 0</c> on <c>pbi = [[voice+0xD4]+0xC]</c> (the owner PBI in
    /// <c>voice+8</c>), forced to 1 by a non-zero mix-result byte. A result of <c>0x11</c> stops on <c>sl</c> or when there is no pending source; any other result stops on <c>2</c> (AK_Fail) or <c>sl</c>,
    /// and otherwise pauses a paused-and-running PBI. The stop is <c>voice vt+0x48</c> = <c>0xA533FC</c> (<see cref="WwiseLiveVoice.StopA533FC"/>).
    /// </summary>
    public void StopDecisionA44A58(WwiseLiveVoice voice)
    {
        // 7.3 (0xA44A50..0xA44A54, 0xA44BB4..0xA44BBC): pbi = [[voice+0xD4]+0xC] with no alternative: a voice with no source is a null dereference in the engine.
        var pbi = OwnerOfSource(voice);
        bool sl = pbi is not null && (pbi.Flags1BC & 0x20) != 0 && pbi.Field1F8 == 0xFFFFFFFF;
        if (voice.Buffer.HasBusParam) sl = true;                     // 0xA44BB4..0xA44BC8: [sp+0x38] != 0 forces the stop
        bool pausedAndRunning = pbi is not null && (pbi.Flags1BC & 0x80) != 0 && voice.State == 1;
        int result = voice.Buffer.Result;
        if (result == 0x11)
        {
            if (sl) { voice.StopA533FC(OwnerOfSourceRaw); return; }
            var next = voice.Pending;
            if (next is null) { voice.StopA533FC(OwnerOfSourceRaw); return; }
            voice.Pending = null;
            bool go = (ContinueWithPendingSource ?? throw new WwiseMissingBehaviourException(
                "M6-026 7.5: 0xA55D04, 0xA55A84, 0xA54A30 and 0xA56478 (the switch to the pending source) are RECOVERABLE_GAP; supply ContinueWithPendingSource"))(voice, next);
            if (!go) voice.StopA533FC(OwnerOfSourceRaw);
            return;
        }
        if (result == 2 || sl) { voice.StopA533FC(OwnerOfSourceRaw); return; }
        if (pausedAndRunning)
            (PauseVoice4C ?? throw new WwiseMissingBehaviourException(
                "M6-026 7.5: voice vt+0x4C (0xA53558, the pause) is unread; supply PauseVoice4C"))(voice);
    }

    /// <summary>
    /// V5a: an override of the whole of <c>0xA43D24</c> (<c>0xA4497C</c>) for a host or test that runs the pre-pass itself; when set, <see cref="PrePassA43D24"/> calls it and nothing else. When unset the pass runs
    /// <see cref="PrePassVoicesA43D24"/> (C37.1) and then the required <see cref="DuckPrePassTailA43D6C"/>.
    /// </summary>
    // fidelity: M6-022
    public Action? DuckPrePass { get; set; }

    /// <summary>
    /// The part of <c>0xA43D24</c> after the voice walk (<c>0xA43D6C..0xA43EFC</c>: the per-bus <c>+0x88 / +0x8C</c> ducking sums, <c>0xA4AF50</c> per voice, <c>0xA437E0</c> on the buses with <c>[bus+0x1CC] &amp; 2</c> and <c>0xA4B4B0</c> per voice;
    /// V5a). C37.1 adopts only the voice walk, so this is a REQUIRED collaborator (a missing one throws and is never skipped).
    /// </summary>
    // fidelity: M6-022
    public Action? DuckPrePassTailA43D6C { get; set; }

    /// <summary>
    /// CalcEffectiveParams as <c>0xA55750</c> reaches it (<c>0xA55888..0xA55894</c>: the PBI context's <c>vt+0x24(ctx, 0)</c>, <see cref="WwisePlayPath.CalcEffectiveParams"/> with no Play params). Required when a voice's PBI has bit 5 of
    /// <c>[pbi+0xE8]</c> clear at the pre-pass.
    /// </summary>
    // fidelity: M6-022
    public Action<WwisePlayingInstance>? CalcEffectiveParamsVt24 { get; set; }

    /// <summary>
    /// The part of <c>0xA4B93C</c> after the voice gain store (<c>0xA4B9BC..0xA4BC30</c>: <c>0x9BE28C</c>, <c>0x9BF8E4</c>, <c>0xA5E694</c>, the send table lazy allocation, <c>0x9BDA88</c>, <c>0x9BD368</c>, <c>0x9D4228</c> and the latch of bit 1 of
    /// <c>[voice+0xCD]</c>); C37.1 adopts only the gain, so this is a REQUIRED collaborator of <see cref="RefreshVoiceGainA4B93C"/>.
    /// </summary>
    // fidelity: M6-022
    // TEST-ONLY override: production leaves it null and runs the engine's tail (RefreshTailA4B9BC); a test sets it to isolate the voice pre-pass from the aux route.
    public Action<WwiseLiveVoice>? VoiceRefreshTailA4B9BC { get; set; }

    /// <summary>The two globals <c>0x9BD368</c> compares against (<see cref="WwiseSendGlobals"/>: the shared process state after the Init.bnk STMG setter 0x9A080C).</summary>
    // fidelity: M6-010
    public WwiseSendGlobals AuxThresholds { get; set; } = WwiseSendGlobals.Shared;

    /// <summary>
    /// <c>0x9D4108(voice, entry, mask)</c> (C40.4 T-A7a): the per-entry dispatch <c>0x9D4228</c> makes, <see cref="WwiseVoiceLinker.DispatchAuxEntry9D4108"/>. REQUIRED: the pass has no linker of its own, so a merge that has entries to dispatch throws
    /// <see cref="WwiseMissingBehaviourException"/> while it is unset (no entry is silently dropped).
    /// </summary>
    // fidelity: M6-010, M6-025
    public Action<WwiseLiveVoice, WwiseAuxEntry, byte>? AuxDispatch9D4108 { get; set; }

    /// <summary>
    /// The 8 uninitialised stack bytes <c>sp+0..7</c> of <c>0x9D4228</c> whose value an old entry reads when it meets an APPENDED entry of the same id (BLOCKED_EXTERNAL, T-A6). Null: that read throws. A host that must run past it gives the bytes.
    /// </summary>
    // fidelity: M6-010
    public byte[]? UninitialisedStackFlags9D4228 { get; set; }

    /// <summary>
    /// <c>0xA4BB54..0xA4BC30</c> (the table is absent or has capacity 0): the 0x4C-byte table the pool allocation <c>0xA7A7F4</c> gives, stored at <c>[voice+0x10]</c> with capacity 1 (the old table, when there is one and its count is 0, is freed). REQUIRED when the tail reaches it:
    /// <c>AddSrc</c> allocates the table first (<see cref="WwisePlaybackBridge.NewVoiceAllocSendTable4C"/>), so this only happens after a failed allocation there. Return null for the allocation failure (<c>0xA4BB74 beq 0xA4BAA0</c>: the tail returns).
    /// </summary>
    // fidelity: M6-022
    public Func<WwiseLiveVoice, WwiseVoiceSendTable?>? AllocateSendTableA4BB54 { get; set; }

    /// <summary>
    /// <c>0xA43D24</c> (C37.1 L7-10, V5a): with <see cref="DuckPrePass"/> set the override; otherwise the voice walk <see cref="PrePassVoicesA43D24"/> and the required tail <see cref="DuckPrePassTailA43D6C"/>.
    /// </summary>
    // fidelity: M6-022
    private void PrePassA43D24()
    {
        if (DuckPrePass is { } whole) { whole(); return; }
        PrePassVoicesA43D24();                                       // 0xA43D24..0xA43D6C
        (DuckPrePassTailA43D6C ?? throw new WwiseMissingBehaviourException(
            "M6-022 V5a: 0xA43D6C..0xA43EFC (the per-bus ducking sums, 0xA4AF50, 0xA437E0, 0xA4B4B0) is not adopted by C37.1; supply DuckPrePassTailA43D6C (or DuckPrePass for the whole pre-pass)"))();
    }

    /// <summary>
    /// The voice walk of <c>0xA43D24</c> (<c>0xA43D24..0xA43D6C</c>, C37.1 L7-10): the voice list (<c>[0x108DF54+0x14]</c>, link <c>[+0xD0]</c>) in order, and for every voice with <c>[voice+0xDC] == 1</c> <c>0xA55750(voice)</c>.
    /// </summary>
    // fidelity: M6-022
    public void PrePassVoicesA43D24()
    {
        for (int i = 0; i < Voices.Count; i++)                       // 0xA43D30..0xA43D68
        {
            var voice = Voices[i];
            if (voice.State == 1) PrePassVoiceA55750(voice);         // 0xA43D4C ldr r3,[r4,#0xdc]; cmp r3,#1; 0xA43D5C bl 0xA55750
        }
    }

    /// <summary>
    /// <c>0xA55750(voice)</c> (C37.1 L7-10, <c>0xA55750..0xA55890</c>): <c>pbi = [[voice+0xD4]+0xC]</c>. <c>[pbi+0xE8] &amp; 0x20</c> clear: the context's <c>vt+0x24</c> (CalcEffectiveParams, <see cref="CalcEffectiveParamsVt24"/>); set with
    /// <c>[pbi+0xE9]</c> bit 0 set: <c>vt+0x28 = 0x9FF414 -> 0x9FF368</c> (<see cref="WwisePlayPath.Recompute9FF368"/>: <c>[pbi+0x3C] = [pbi+0x98] + [pbi+0x118]</c>, <c>[pbi+0x40]</c> from the factors, the dirty bit cleared); set with the bit clear: nothing.
    /// Then <c>[pbi+0x1BE] &amp; 0x14 == 0</c>: <c>0xA4B93C(voice)</c> (<see cref="RefreshVoiceGainA4B93C"/>); otherwise <c>0xA55790</c> (<c>0xA0275C</c> and the stop path <c>0xA557A0..0xA55850</c>), which is not adopted (RECOVERABLE_GAP, a visible stop).
    /// </summary>
    // fidelity: M6-022
    public void PrePassVoiceA55750(WwiseLiveVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var pbi = OwnerOfSource(voice);                              // 0xA55750..0xA5575C ldr r3,[r0,#0xd4]; ldr r5,[r3,#0xc]
        if ((pbi.Flags0E8 & 0x20) == 0)                              // 0xA55768..0xA55774 ands r1,r1,#0xff; beq 0xA55888
            (CalcEffectiveParamsVt24 ?? throw new WwiseMissingBehaviourException(
                "M6-022 L7-10: 0xA55888..0xA55894 calls the context's vt+0x24 (CalcEffectiveParams, r1 = 0); supply WwiseVoiceBusPass.CalcEffectiveParamsVt24"))(pbi);
        else if ((pbi.Flags0E9 & 1) != 0)                            // 0xA55778..0xA55780 tst r3,#1; bne 0xA5585C
            WwisePlayPath.Recompute9FF368(pbi);                      // 0xA55860..0xA55868 ctx vt+0x28 = 0x9FF414 -> 0x9FF368
        if ((pbi.Flags1BE & 0x14) != 0)                              // 0xA55784..0xA5578C (and 0xA5586C..0xA55874 after the vt+0x28 call)
            throw new WwiseMissingBehaviourException(
                "M6-022 L7-10: [pbi+0x1BE] & 0x14 != 0 takes 0xA55790 (0xA0275C, then the stop path 0xA557A0..0xA55850); not adopted (RECOVERABLE_GAP)");
        RefreshVoiceGainA4B93C(voice);                               // 0xA55878..0xA5587C bl 0xA4B93C(voice)
    }

    /// <summary>
    /// <c>0xA4B93C(voice)</c>'s gain store (C37.1 L7-10, <c>0xA4B93C..0xA4B9B8</c>): <c>ctx = [voice+8]</c> (the owner PBI); <c>y = [ctx+0x30] * 0.05f</c> (<c>[pbi+0x3C]</c>); <c>y &lt; -37.0f</c> gives 0.0f, else the engine's fast pow of <c>0xA4B94C..0xA4B9A8</c>
    /// (binary32, non-fused, the constants as bits: <see cref="WwisePlaybackLimiter.Lin9BEB30"/> is the same code); <c>[voice+0x1C] = [ctx+0x34] * lin</c> (<c>[pbi+0x40]</c> times it). The rest of the function (<c>0xA4B9BC..</c>) is
    /// <see cref="VoiceRefreshTailA4B9BC"/>, required.
    /// </summary>
    // fidelity: M6-022, M6-010
    public void RefreshVoiceGainA4B93C(WwiseLiveVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var pbi = voice.BusOwner8 as WwisePlayingInstance ?? throw new WwiseMissingBehaviourException(
            "M6-022 L7-10: 0xA4B93C reads ctx = [voice+8] (0xA4B944); the voice has no owner PBI in BusOwner8");
        float lin = WwisePlaybackLimiter.Lin9BEB30(pbi.Volume3C);   // 0xA4B94C..0xA4B9A8 (0xA4BAA8 for y < -37.0f)
        voice.OutputGain = pbi.MuteFade40 * lin;                     // 0xA4B9AC vldr s15,[r5,#0x34]; 0xA4B9B4 vmul.f32; 0xA4B9B8 vstr s14,[r4,#0x1c]
        if (VoiceRefreshTailA4B9BC is { } over) over(voice);                    // a host's or test's whole-tail override
        else RefreshTailA4B9BC(voice, pbi);                                     // 0xA4B9BC..0xA4BC30
    }

    /// <summary>
    /// <c>0xA4B9BC..0xA4BAA0</c> (C40.4 T-A4), the part of <c>0xA4B93C</c> after the voice gain store, for the ctx <paramref name="pbi"/> = <c>[voice+8]</c>:
    /// <list type="number">
    /// <item><c>0x9BE28C(ctx)</c>: with <c>[ctx+0xDD]</c> bit 1 clear it returns <c>[ctx+0xDC] &amp; 3</c> (0 for a non-3D sound after <c>0x9BEB30</c>); a non-zero result takes <c>0xA4BAB8</c> (<c>0x9BF8E4</c>, <c>0xA5E694</c>) and bit 1 set takes <c>0x9BDB18</c> and the 3D body: those bodies are unread, so both throw.</item>
    /// <item>Entry 0 of <c>[voice+0x10]</c>: a count of 0 initialises it (<see cref="WwiseVoiceSendTable.InitEntry0A4BB00"/>; with capacity 0 a table is allocated first, <see cref="AllocateSendTableA4BB54"/>), then its byte <c>+0x44</c> = <c>[[ctx+8]+0x22]</c> and its float <c>+0x34</c> = <c>lin([ctx+0x58]) * [[ctx+8]+0x60]</c>.</item>
    /// <item><c>0x9BDA88(ctx)</c> zero ends the call. Otherwise <c>0x9BD368</c> builds the sends (<see cref="WwiseAuxRoute.Build9BD368"/>), <c>0x9D4228</c> merges them into <c>voice+0x2C</c> with <c>flag</c> = bit 1 of <c>[voice+0xCD]</c> (<see cref="WwiseAuxRoute.Merge9D4228"/>), and
    /// <c>[voice+0xCD] |= 2</c>.</item>
    /// </list>
    /// </summary>
    // fidelity: M6-010, M6-022
    public void RefreshTailA4B9BC(WwiseLiveVoice voice, WwisePlayingInstance pbi)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(pbi);
        if ((pbi.Flags0E9 & 2) != 0)                                            // 0x9BE28C ldrb r3,[r0,#0xdd]; tst r3,#2; bne 0x9BE2B4
            throw new WwiseMissingBehaviourException("M6-022 T-A4: 0x9BE28C with [ctx+0xDD] bit 1 set calls 0x9BDB18 and runs the 3D body 0x9BE2B8..; unread");
        if ((pbi.Flags0E8 & 3) != 0)                                            // 0x9BE2A4..0x9BE2A8 ldrb r0,[r4,#0xdc]; and r0,r0,#3; 0xA4B9C0 cmp r0,#0; bne 0xA4BAB8
            throw new WwiseMissingBehaviourException("M6-022 T-A4: a non-zero 0x9BE28C result takes 0xA4BAB8 (0x9BF8E4, 0xA5E694), which are unread; a non-3D sound has bits 0..1 of [ctx+0xDC] cleared by 0x9BEB30");
        var go = pbi.GameObjectRef14 ?? throw new WwiseMissingBehaviourException(
            "M6-010 T-A4: 0xA4B9D4 reads [[ctx+8]+0x22] and [[ctx+8]+0x60]; the PBI has no game object reference (WwisePlayingInstance.GameObjectRef14, set by the context init)");

        var table = voice.SendTable;
        if (table is null || table.Count == 0)                                  // 0xA4B9C8 ldr r6,[r4,#0x14]; cmp r6,#0; beq 0xA4BAF0
        {
            if (table is null || table.Capacity == 0)                           // 0xA4BAF0 ldr sb,[r4,#0x18]; cmp sb,#0; beq 0xA4BB54
            {
                table = (AllocateSendTableA4BB54 ?? throw new WwiseMissingBehaviourException(
                    "M6-022 T-A4: 0xA4BB54 allocates the 0x4C-byte table (0xA7A7F4); supply WwiseVoiceBusPass.AllocateSendTableA4BB54"))(voice);
                if (table is null) return;                                      // 0xA4BB74 beq 0xA4BAA0
                table.Capacity = 1;                                             // 0xA4BC28..0xA4BC2C str r3,[r4,#0x18] (r3 = 1)
                voice.SendTable = table;                                        // 0xA4BC20 str r3,[r4,#0x10]
            }
            if (!table.InitEntry0A4BB00()) return;                              // 0xA4BB00..0xA4BB50 (a null entry returns, 0xA4BB14)
        }
        var entry0 = table.Entries[0];
        entry0.PerTargetByte = go.Mask22;                                       // 0xA4B9D4..0xA4B9E8 ldr r2,[r5,#8]; ldrb r2,[r2,#0x22]; strb r2,[r3,#0x44]
        entry0.SendGain = WwiseArmFloat.Mul(go.Volume60, WwiseAuxRoute.Lin(pbi.ReadWord64()));    // 0xA4B9F0..0xA4BA54: s13 = [GO+0x60]; s14 = lin(pbi+0x64); vmul.f32 s14,s13,s14; vstr [table+0x34]

        if (!WwiseAuxRoute.Continue9BDA88(pbi)) return;                         // 0xA4BA58..0xA4BA64 bl 0x9BDA88; beq 0xA4BAA0
        var block = WwiseAuxRoute.Build9BD368(pbi, go, AuxThresholds);          // 0xA4BA74 bl 0x9BD368(ctx, sp+0x10)
        var dispatch = AuxDispatch9D4108;
        byte mask = go.Mask22;                                                  // 0x9D45F8..0x9D4600 ldrb r8,[[[voice+8]+8]+0x22]
        int n = WwiseAuxRoute.Merge9D4228(block, voice.AuxEntries2C, voice.CountCC, (voice.FlagsCD & 2) != 0,   // 0xA4BA78..0xA4BA90: flag = ubfx [voice+0xCD],#1,#1; count = [voice+0xCC]
            UninitialisedStackFlags9D4228);
        voice.CountCC = (byte)n;                                                // 0x9D45F0 strb r4,[r3] (before the dispatches)
        for (int i = 0; i < n; i++)                                             // 0x9D4608..0x9D462C
            (dispatch ?? throw new WwiseMissingBehaviourException(
                "M6-010 T-A7a: 0x9D4228 dispatches every entry through 0x9D4108 (the bus lookup, the device list and 0xA43434); supply WwiseVoiceBusPass.AuxDispatch9D4108 (WwiseVoiceLinker.DispatchAuxEntry9D4108)"))(voice, voice.AuxEntries2C[i], mask);
        voice.FlagsCD = (byte)(voice.FlagsCD | 2);                              // 0xA4BA94..0xA4BA9C
    }

    /// <summary>V5b: <c>0xA39564</c> (<c>0xA44980</c>), <see cref="WwisePlaybackLimiter.PerFrameA39564"/>; a REQUIRED collaborator.</summary>
    // fidelity: M6-026
    public Action? NodeCleanup { get; set; }

    /// <summary>V5: <c>0x9D3CC0</c> (<c>0xA44978</c>), <see cref="WwisePlaybackBridge.WalkPendingVoices"/>; a REQUIRED collaborator.</summary>
    // fidelity: M6-025
    public Action? AdvanceTickCounters { get; set; }

    /// <summary>
    /// V7 <c>0xA54F1C</c>: the per-voice parameter/state machine, transliterated from C12 voice-callees Q4
    /// (settled branch table) and C17 V7-j..V7-m. The settled gates are <c>E4=[voice+0xE4]</c>,
    /// <c>E0=[voice+0xE0]</c>, <c>A=[voice+0xCD]&amp;1</c>, <c>E8=[voice+0xE8]&amp;1</c>,
    /// <c>SRC10=[source+0x10]&amp;1</c>; the named callees are <see cref="SetConnectionBit2"/> (<c>0xA4C584</c>),
    /// <see cref="ReleaseBusRef"/> / <see cref="AcquireBusRef"/> (<c>0xA022E8</c>/<c>0xA0228C</c>),
    /// <see cref="NextSource"/> (<c>0xA01768</c>), <see cref="RefreshTailA4B9BC"/> (<c>0xA4B93C</c>, <c>0x9D4228</c>) and
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
                    voice.Buffer.Result = (int)voice.Word0xF0;             // [params+4] = [voice+0xF0]
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
        int r = (voice.StartSource56650 ?? throw new WwiseMissingBehaviourException(
            "M6-025 C30: 0xA554F8 calls 0xA56650(source, [bus+0x1DC], [bus+0x1E0]) on the live state machine; supply StartSource56650 rather than defaulting to 0"))();   // 0xA554F8
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
    /// V21 <c>0xA38420</c>: the deferred PBI-notification flush (C34.3 S8). While the queue's count (<c>[Q+0x18]</c>) is not 0 the head item <c>{pbi, code, r2, r3}</c> goes to <c>0xA0188C</c> (<c>0xA38480</c>); a code 4 (Term,
    /// <c>[item+8] == 4</c>) then runs the teardown (<c>0xA384C8..0xA38508</c>); the item is removed (<c>0xA38518..0xA3856C</c>, <see cref="WwiseNotificationQueue.PopHeadA38518"/>) when the queue still has a head.
    /// </summary>
    public void FlushPbiNotifications()
    {
        var queue = Notifications ?? throw new WwiseMissingBehaviourException(
            "M6-022 V21: the notification queue Q (0x108DE78) is initialised by an unread init; supply WwiseVoiceBusPass.Notifications");
        while (queue.Count != 0)                                     // 0xA3846C cmp r3,#0; beq 0xA385A0
        {
            var item = queue.Head ?? throw new InvalidOperationException("the notification queue has a count but no head: the flush dereferences it (0xA38474..0xA38478)");   // 0xA38474 ldr r4,[r6,#4]
            (NotificationHandlerA0188C ?? throw new WwiseMissingBehaviourException(
                "M6-022 V21: 0xA0188C is not wired; supply WwiseVoiceBusPass.NotificationHandlerA0188C"))(item.Pbi, item.Code, item.R2, item.R3);   // 0xA38480 bl 0xA0188C
            if (item.Code == WwisePbiNotification.TermCode)          // 0xA38484..0xA38488 cmp r3,#4
                (TerminateNotifiedPbiA384C8 ?? throw new WwiseMissingBehaviourException(
                    "M6-022 V21: the code-4 teardown is not wired; supply WwiseVoiceBusPass.TerminateNotifiedPbiA384C8"))(item.Pbi);   // 0xA384C8..0xA38508
            if (queue.Head is not null)                              // 0xA3845C / 0xA3850C ldr r1,[r5,#4]; cmp r1,#0; bne 0xA38518
                queue.PopHeadA38518();
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

        // UNRESOLVED (MISSING for the Extractor): this bus model keeps its own cache field (WwiseMixBus.NextSource1BB),
        // while the native cache is the single pbi+0x1BB shared by 0xA01768's callers (AddSrc and 0xA37258, 0xA373B4,
        // 0xA37578, 0xA37650, 0xA37944, 0xA55B54); the bus-to-pbi identity is not settled here, so it is not unified.
        // fidelity: M6-025
        var result = (bus.NextSourceEda ?? throw new WwiseMissingBehaviourException(
            "M6-025 C27: 0x9EEDA4 (inside 0xA01768) is unread; supply WwiseMixBus.NextSourceEda")).Invoke(bus.NextSourceE0);
        int code;
        if (result.Code == 3)                                                   // 0xA017A4 cmp r0,#3
        {
            int r = (bus.E0Vt120 ?? throw new WwiseMissingBehaviourException(
                "M6-025 C27: vt+0x120 (0xA017C8..0xA017E4) is unread; supply WwiseMixBus.E0Vt120")).Invoke(bus.E0Arg14C);
            code = r == 0 ? 1 : 2;                                              // only the mapped value is stored
        }
        else
        {
            code = result.Code & 0xF;
        }
        index = result.Index & 7;
        bus.NextSource1BB = (byte)(0x80 | (index & 7) | ((code & 0xF) << 3));
        return code;
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
        if (!(voice.StartResampler5321C ?? voice.StartResamplerA5321C)()) return 2;  // 0xA54A78 0xA5321C != 1 -> 2

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
        if ((voice.Word0xF0 & 0xFF) == 0) return;                          // 0xA4BD74 cmp r6,#0; beq 0xA4BFD4
        for (int i = 0; i < 4; i++) minima[i] = 100f;                // 0x42CA0000

        foreach (var c in voice.Connections)
        {
            // C24.4 (0xA4BE08..0xA4BE58). inCh = the low byte of [voice+0xF0] (0xA4BC74); outCh = the low byte
            // of [[conn+0x30]+0x64] (0xA4BE24..0xA4BE2C). A changed [conn+0x64] frees the descriptor (0xA67C58)
            // and clears +0xC, +0x64, +0x14; 0xA67B9C then sizes it; with [conn+0x18] != 0, [conn+0x64] = inCh
            // and +0x20/+0x24 swap every frame. The order of these steps inside 0xA4BC58 is not stated by
            // C24.4 (reported as MISSING).
            int inCh = (int)(voice.Word0xF0 & 0xFF);
            int outCh = (int)(c.Bus.Format64 & 0xFF);
            if (c.C64 != inCh)
            {
                c.Descriptor.Free();
                c.C0C = 0f;
                c.C14 = 0f;
                c.C64 = 0;
                // C26.6: 0xA67B9C only on the reinit path (0xA4BDDC..0xA4BE08). A result other than 1 leaves +0x18 == 0
                // and moves to the next connection (0xA4BE0C..0xA4BE18, batch4a-missing row 3.3).
                if (c.Descriptor.Reserve(inCh, outCh) != 1) continue;
            }
            if (c.HasDry)
            {
                c.C64 = inCh;
                c.Descriptor.SwapPointers();
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
    /// The threshold: <c>0xA4B4B0</c> reads <c>[[0x10400AC]] = [0x1052454]</c>, which after the Init.bnk STMG setter (<c>0x9A080C(-80, 2)</c>) is binary32 <c>0x38D2306A</c>
    /// (the earlier 0x38D1B717 = 0.0001f was contradicted). <see cref="DuckingThreshold"/> is <see cref="WwiseSendGlobals.Shared"/>'s value and stays overridable.
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

        float g = DuckingThreshold;                                  // [0x1052454]
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
    /// V7/C11 <c>0xA4B4B0</c>: the ducking threshold global <c>[[0x10400AC]] = [0x1052454]</c>: <see cref="WwiseSendGlobals.Shared"/>'s game threshold (0x38D2306A, the state after the Init.bnk STMG setter 0x9A080C(-80, 2); the earlier
    /// 0x38D1B717 = 0.0001f was contradicted).
    /// </summary>
    public static float DuckingThreshold
    {
        get => WwiseSendGlobals.Shared.GameLinear;
        set => WwiseSendGlobals.Shared.GameLinear = value;
    }
}

/// <summary>The notification codes of the queue <c>0xA38600</c> fills (V21, C12 X6).</summary>
public static class WwisePbiNotification
{
    /// <summary>V21: the Term message code <c>[item+8]==4</c> (C12 X6).</summary>
    public const int TermCode = 4;
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