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

    // The matrix halves (pass 16 B3, C44.3): the allocation (0xA67B9C) holds two matrices of size/2 bytes, each numIn rows of ((outCh + 3) >> 2) * 4 floats. [conn+0x20] (PtrA) is the NEXT matrix
    // (0xA5975C writes it, 0xA25FF8's matrix argument), [conn+0x24] (PtrB) is the PREVIOUS one (0xA45E9C reads prev = [conn+0x24], next = [conn+0x20]). The allocation is NOT zeroed (the pool's
    // memory is uninitialised): half B holds whatever the pool held, and the engine writes it before it reads it (the zero fill of 0xA4C16C clears only half A); this model starts it at zero.

    /// <summary>The next matrix <c>[conn+0x20]</c>: the whole half, <c>size / 2</c> bytes as floats (empty when unallocated).</summary>
    public Span<float> NextMatrix => Data is null ? default : System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(Data.AsSpan(PtrA, Size / 2));

    /// <summary>The previous matrix <c>[conn+0x24]</c>: the whole half (empty when unallocated).</summary>
    public Span<float> PrevMatrix => Data is null ? default : System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(Data.AsSpan(PtrB, Size / 2));

    /// <summary><c>memcpy(dst = [conn+0x20] (next), src = [conn+0x24] (prev), floats * 4)</c> (0xA4C088..0xA4C0B8, 0xA59878..0xA598B0); a count of 0 copies nothing (<c>cmp r2,#0; beq</c>).</summary>
    public void CopyPrevToNext(int floats)
    {
        if (floats == 0) return;
        PrevMatrix[..floats].CopyTo(NextMatrix);
    }

    /// <summary><c>memcpy(dst = [conn+0x24] (prev), src = [conn+0x20] (next), floats * 4)</c> (0xA4BF6C..0xA4BF9C, 0xA4C0D8..0xA4C130); a count of 0 copies nothing.</summary>
    public void CopyNextToPrev(int floats)
    {
        if (floats == 0) return;
        NextMatrix[..floats].CopyTo(PrevMatrix);
    }
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

    /// <summary>V7/C1 <c>0xA4BC58</c> per-connection fields: <c>+0x08/+0x0C/+0x10/+0x14</c> (the engine's ctor sets all four to 1.0f, <c>0xA6F918</c>) and the four
    /// min inputs <c>+0x50/+0x54/+0x58/+0x5C</c>; <c>+0x64</c> is the re-init id. <c>+0xC</c> is <c>[voice+0x1C]*gain</c>, <c>+0x14</c> the send gain 0xA5975C writes.</summary>
    public float C0C { get; set; }
    public float C08 { get; set; }
    public float C10 { get; set; }
    public float C14 { get; set; }
    public float C50 { get; set; }
    public float C54 { get; set; }
    public float C58 { get; set; }
    public float C5C { get; set; }
    public int C64 { get; set; }

    /// <summary>
    /// The earlier model's pan matrix for <see cref="Refresh"/> (M6-012 gapE 2.1; caller-supplied): used ONLY by the legacy render order (<see cref="WwiseLiveVoice.AllowRenderOrderApproximation"/>), never on the engine path, where the
    /// matrices are the descriptor's halves that <c>0xA4BC58</c> -&gt; <c>0xA5975C</c> -&gt; <c>0xA25FF8</c> produce (<see cref="Descriptor"/>).
    /// </summary>
    public float[]? TargetMatrix { get; set; }

    /// <summary>The earlier model's composed target gain for <see cref="Refresh"/> (legacy render order only).</summary>
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
    /// LEGACY render order only (<see cref="WwiseLiveVoice.AllowRenderOrderApproximation"/>; NOT the engine path, which never calls it): the earlier model's promote-and-take of the last end gain/matrix
    /// (M6-012 gapE 2.5). <see cref="TargetMatrix"/> must be set; the identity diagonal is used when null.
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
    /// <see cref="WwiseMixKernels.MatrixMixA45E9C"/> runs (prev = <c>[conn+0x24]</c>, next = <c>[conn+0x20]</c>, the descriptor's halves) with <c>start = (conn[0x10] * conn[8]) * g0</c> and <c>end = (conn[0x14] * conn[0xC]) * g1</c> (float32, in that order) over the BUS frame count (not the voice's valid count: that
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
        // A45E9C reads the destination row count from the bus buffer's cfg word [bus+0x64] and the input rows from the block's cfg [S+4]; the model's bus buffer is one mono float[], so another count is a visible stop.
        int outChannels = (int)(Bus.Format64 & 0xFFu);
        if (outChannels != 1)
            throw new WwiseMissingBehaviourException($"M6-012 V2-06: the bus buffer's cfg 0x{Bus.Format64:X} has {outChannels} channels; the model's bus buffer is one mono float[] (the Master line's cfg is UNKNOWN, C44.3 9.3)");
        float start = (C10 * C08) * g0;                                              // 0xA4FD58..0xA4FD60 vmul.f32 s15,s15,s11; vmul.f32 s15,s15,s13
        float end = (C14 * C0C) * g1;                                                // 0xA4FD44, 0xA4FD5C
        var sources = new ReadOnlyMemory<float>[channels];
        for (int c = 0; c < channels; c++) sources[c] = data.AsMemory(c * max, max);
        // 0xA4FD6C bl 0xA45E9C(S, bus+0x60, &{start, end}, prev = [conn+0x24], [sp] = next = [conn+0x20], [sp+4] = [bus+0x5C], [sp+8] = u16 [bus+0x58]); the rows are the engine's padded ones (D3/D4).
        int stride = WwiseChannelMatrix.Rows(Bus.Format64);
        if (Descriptor.PrevMatrix.Length < channels * stride)
            throw new InvalidOperationException($"the connection's matrices hold {Descriptor.PrevMatrix.Length} floats; {channels} channels x {stride} are read (the engine reads past its allocation)");
        WwiseMixKernels.MatrixMixA45E9C(Descriptor.PrevMatrix, Descriptor.NextMatrix, stride, sources, new[] { Bus.Buffer }, channels, outChannels, start, end, Bus.InvFrames5C, Bus.MaxFrames);
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
/// The FX descriptor <c>fx</c> that <c>0xA019B8(pbi, i, &amp;fx)</c> (= node <c>vt+0xE8</c> = <c>0x9EEF2C</c>) hands to <c>0xA54A30</c> (C41.4, rows 4.1, 4.4, 4.5): <c>[fx+0x10]</c> the plug-in id, <c>[fx+0x14]</c> the parameter object (its <c>vt+0xC</c> clones it with the
/// plug-in allocator, the clone's <c>vt+0x14</c> destroys it), <c>[fx+0x24]</c> / <c>[fx+0x28]</c> the RTPC records (0x20 bytes each) and <c>vt+8</c> / <c>vt+0xC</c> the reference count (AddRef / Release). The resolver body <c>0x9EEF2C</c> is not extracted, so the descriptor is
/// supplied by the host (<see cref="WwiseLiveVoice.ResolveNodeFx9EEF2C"/>).
/// </summary>
// fidelity: M6-022
public sealed class WwiseVoiceFxDescriptor
{
    /// <summary><c>[fx+0x10]</c>: the plug-in id.</summary>
    public uint Id { get; set; }

    /// <summary><c>[fx+0x14]-&gt;vt+0xC(clone, allocator)</c>: clones the parameter object; null here is <c>[fx+0x14] == 0</c>.</summary>
    public Func<IWwisePluginMemAlloc, object?>? CloneParams { get; set; }

    /// <summary>The clone's <c>vt+0x14</c> (<c>0x9CF820</c>, the holder teardown): destroys the cloned parameter object.</summary>
    public Action<object, IWwisePluginMemAlloc>? DestroyParams { get; set; }

    /// <summary><c>[fx+0x28]</c>: the number of RTPC records at <c>[fx+0x24]</c>; the shipped Compressor ShareSet has none (row 4.5).</summary>
    public int RtpcRecordCount { get; set; }

    /// <summary>The reference count: 1 from the resolver, <see cref="AddRef"/> by the holder (<c>fx-&gt;vt+8</c>), <see cref="Release"/> by <c>vt+0xC</c>.</summary>
    public int RefCount { get; private set; } = 1;

    /// <summary><c>fx-&gt;vt+8</c>.</summary>
    public void AddRef() => RefCount++;

    /// <summary><c>fx-&gt;vt+0xC</c>.</summary>
    public void Release() => RefCount--;
}

/// <summary>
/// The 0x18-byte context object a wrapper hands the plug-in as Init's <c>ctx</c> (<c>0xA6C22C</c>, vptr <c>0x103D4F0</c>, row 4.2): <c>[+4] = i</c>, <c>[+8..0x10] = 0</c>, <c>[+0x14] = voice</c>.
/// </summary>
// fidelity: M6-022
public sealed class WwiseVoiceFxContext
{
    /// <summary><c>[+4]</c>: the slot index.</summary>
    public int Index { get; }

    /// <summary><c>[+0x14]</c>: the voice.</summary>
    public WwiseLiveVoice Voice { get; }

    internal WwiseVoiceFxContext(WwiseLiveVoice voice, int index) { Voice = voice; Index = index; }
}

/// <summary>
/// One node of the voice's chain as its downstream neighbour calls it (C41.6 row 2.10: the chain is <c>src</c>, the pitch node, the FX slots in slot order, the filter holder <c>voice+0x1C0</c>, <c>voice+0x380</c>; each node's <c>vt+0x24</c> stores its upstream neighbour at
/// <c>[node+4]</c>, <c>0xA54D8C..0xA54DAC</c>). The slots forward <c>vt+0x10/0x14/0x18/0x1C/0x20</c> to their upstream neighbour; the pitch node's bodies of those slots are unread (<see cref="WwiseLiveVoice"/>'s required seams).
/// </summary>
// fidelity: M6-022
public interface IWwiseFxChainNode
{
    /// <summary><c>vt+0x10(this, &amp;r1)</c>: the result goes to <c>[S+0x28]</c> in V7's <c>0xA55598</c>.</summary>
    int Vt10(int r1);

    /// <summary><c>vt+0x14(this, r1)</c>.</summary>
    void Vt14(int r1);

    /// <summary><c>vt+0x18(this, r1, r2)</c>.</summary>
    int Vt18(int r1, int r2);

    /// <summary><c>vt+0x1C(this)</c>.</summary>
    int Vt1C();

    /// <summary><c>vt+0x20(this)</c>.</summary>
    int Vt20();
}

/// <summary>
/// The voice's in-place FX wrapper (C41.4 rows 4.1..4.7): the 0x34-byte object of vptr <c>0x103DB98</c> that <c>0xA54A30</c> builds when <c>byte [info+8] != 0</c> (the plug-in works in place; the shipped Compressor does), <c>vt+0x28 = 0xA792B0</c>. The 0x9C-byte
/// out-of-place class (<c>0x103DC38</c>, <c>vt+0x28 = 0xA79858</c>) is not extracted: building it is a required stop (<see cref="WwiseMissingBehaviourException"/>). A slot whose creation fails is not made: <c>[voice+0x370+4i]</c> stays null; there is no "no plug-in" form.
/// Layout (row 4.2): <c>+4</c> the upstream node, <c>+8</c> the voice, <c>+0xC</c> the context, <c>+0x10</c> the holder (<c>+0x14</c> the cloned parameters, <c>+0x18</c> the descriptor), <c>+0x1C</c> the plug-in id, <c>+0x20</c> done, <c>+0x21</c> bypass,
/// <c>+0x22</c> reset done, <c>+0x24</c> the slot index, <c>+0x28</c> the plug-in, <c>+0x2C</c> the buffer, <c>+0x30</c> the channel word.
/// <para>The <c>Execute38Hook</c> / <c>Execute3CHook</c> / <c>ReleaseVtCHook</c> / <c>TeardownHook</c> properties are TEST-ONLY overrides (the engine oracles of the render order stand in for the slot bodies); production leaves them null and runs the wrapper's bodies.</para>
/// </summary>
// fidelity: M6-022
public sealed class WwiseVoiceInsertFxSlot : IWwiseFxChainNode
{
    /// <summary>The wrapper's vptr (<c>.got 0x1040170 -&gt; 0x103DB90 + 8</c>, row 4.1).</summary>
    public const uint Vtable = 0x103DB98;

    /// <summary>The out-of-place class's vptr (<c>.got 0x1040174 -&gt; 0x103DC30 + 8</c>; not built).</summary>
    public const uint OutOfPlaceVtable = 0x103DC38;

    /// <summary>The wrapper's allocation size (<c>ldr r1,#0x34</c>, row 4.1).</summary>
    public const int Size = 0x34;

    private IWwiseFxChainNode? _upstream;
    private WwiseLiveVoice? _voice;
    private IWwiseEffectPlugin? _plugin;
    private float[]? _buffer;
    private WwiseVoiceFxDescriptor? _fx;
    private object? _params;
    private WwiseVoiceFxContext? _ctx;

    // The hook-only slot (the engine oracles of the render order use it); the engine's slot is made by Create.
    internal WwiseVoiceInsertFxSlot() { }

    /// <summary><c>[W+4]</c>: the upstream node (<c>vt+0x24 = 0xA52678</c>).</summary>
    public IWwiseFxChainNode? Upstream => _upstream;

    /// <summary><c>[W+0x1C]</c>: the plug-in id (initial -1).</summary>
    public uint PluginId { get; private set; } = 0xFFFFFFFF;

    /// <summary><c>byte [W+0x20]</c>: done (set when the result was 0x11).</summary>
    public byte Done { get; private set; }

    /// <summary><c>byte [W+0x21]</c>: bypass (voice <c>vt+0x68</c> / <c>vt+0x70</c> write it).</summary>
    public byte Bypass { get; set; }

    /// <summary><c>byte [W+0x22]</c>: reset done.</summary>
    public byte ResetDone { get; private set; }

    /// <summary><c>[W+0x24]</c>: the slot index.</summary>
    public int Index { get; private set; }

    /// <summary><c>[W+0x28]</c>: the plug-in instance (<c>vt+0x40 = 0xA79364</c>).</summary>
    public IWwiseEffectPlugin? Plugin => _plugin;

    /// <summary><c>[W+0x2C]</c>: the buffer the process step allocates when the voice has no data.</summary>
    public float[]? Buffer => _buffer;

    /// <summary><c>[W+0x30]</c>: the channel word <c>[fmt+4]</c> (<c>vt+0x44 = 0xA7904C</c>).</summary>
    public uint ChannelWord { get; private set; }

    /// <summary><c>[W+0x14]</c>: the cloned parameter object the holder keeps.</summary>
    public object? Params => _params;

    /// <summary><c>[W+0x18]</c>: the descriptor the holder keeps.</summary>
    public WwiseVoiceFxDescriptor? Descriptor => _fx;

    /// <summary><c>[W+0xC]</c>: the context object.</summary>
    public WwiseVoiceFxContext? Context => _ctx;

    /// <summary>TEST-ONLY: replaces <c>vt+0x38</c> (<c>0xA790E8</c>).</summary>
    internal Action<WwiseVoiceBuffer>? Execute38Hook { get; set; }

    /// <summary>TEST-ONLY: replaces <c>vt+0x3C</c> (<c>0xA791A8</c>).</summary>
    internal Action<WwiseVoiceBuffer>? Execute3CHook { get; set; }

    /// <summary>TEST-ONLY: replaces <c>vt+0xC</c> (<c>0xA7915C</c>); true when the chain continues upstream.</summary>
    internal Func<bool>? ReleaseVtCHook { get; set; }

    /// <summary>TEST-ONLY: replaces <c>vt+0x2C</c> (<c>0xA7933C</c>).</summary>
    internal Action? TeardownHook { get; set; }

    private WwiseLiveVoice VoiceOrThrow() => _voice ?? throw new WwiseMissingBehaviourException("M6-022 4.6: a hook-only insert-FX slot has no voice");

    private IWwiseEffectPlugin PluginOrThrow(string what) => _plugin ?? throw new WwiseMissingBehaviourException(
        $"M6-022 4.6: {what} needs the slot's plug-in [W+0x28]; this slot was made without one (hook-only)");

    /// <summary>
    /// <c>vt+0x28 = 0xA792B0(W, plugin, &amp;fx, i, voice, &amp;fmt)</c> (row 4.3): <c>[W+0x28] = plugin</c>, <c>[W+0x30] = [fmt+4]</c>, <c>[W+0x2C] = 0</c>, then <c>0xA793D4</c> (a result other than 1 is returned), then the plug-in's <c>vt+0x1C</c> Init
    /// (<c>alloc, [W+0xC], [W+0x14], fmt</c>; a result other than 1 is returned) and the tail <c>plugin-&gt;vt+0xC</c> (Reset).
    /// </summary>
    public int InitA792B0(IWwiseEffectPlugin plugin, WwiseVoiceFxDescriptor fx, int i, WwiseLiveVoice voice, WwiseEffectFormat fmt)
    {
        _plugin = plugin;                                                          // 0xA792B0.. [W+0x28] = plugin
        ChannelWord = fmt.ChannelWord;                                             // [W+0x30] = [fmt+4]
        _buffer = null;                                                            // [W+0x2C] = 0
        int r = InitHolderA793D4(fx, i, voice);
        if (r != 1) return r;
        r = plugin.Init(voice.PluginAllocator, _ctx, _params, fmt);                // plugin vt+0x1C(alloc, [W+0xC], [W+0x14], fmt)
        if (r != 1) return r;
        return plugin.Reset();                                                     // tail plugin vt+0xC
    }

    /// <summary>
    /// <c>0xA793D4(W, plugin, &amp;fx, i, voice)</c> (row 4.4): <c>[W+0xC] = 0</c>, <c>[W+8] = voice</c>, <c>[W+0x20..0x22] = 0</c>, <c>[W+0x24] = i</c>; <c>key = [voice+8]</c> (0 when the voice has no PBI context); <c>0x9CF644(W+0x10, fx, key, 1)</c> (0 returns 2);
    /// <c>[W+0x1C] = [fx+0x10]</c>; the 0x18-byte context (an allocation failure leaves <c>[W+0xC] = 0</c> and returns 2) <c>0xA6C22C(ctx, voice, i)</c>, <c>[W+0xC] = ctx</c>; 1.
    /// </summary>
    private int InitHolderA793D4(WwiseVoiceFxDescriptor fx, int i, WwiseLiveVoice voice)
    {
        _ctx = null;                                                               // [W+0xC] = 0
        _voice = voice;                                                            // [W+8] = voice
        Done = 0; Bypass = 0; ResetDone = 0;                                       // [W+0x20..0x22] = 0
        Index = i;                                                                 // [W+0x24] = i
        object? holderParams = HolderInit9CF644(fx, flag: true);                   // key = [voice+8]: the holder's key loops are required stops (row 4.5)
        if (holderParams is null) return 2;                                        // 0 -> return 2
        PluginId = fx.Id;                                                          // [W+0x1C] = [fx+0x10]
        if (voice.FxAllocationFails?.Invoke() == true) { _ctx = null; return 2; }  // alloc 0x18 -> null: [W+0xC] = 0, return 2
        _ctx = new WwiseVoiceFxContext(voice, i);                                  // 0xA6C22C: [+4] = i, [+8..0x10] = 0, [+0x14] = voice
        return 1;
    }

    /// <summary>
    /// <c>0x9CF644(holder, fx, key, flag)</c> (row 4.5), the shipped ShareSet path: <c>[fx+0x14]</c> clones into <c>[holder+4]</c>, <c>[holder+8] = fx</c>, <c>fx-&gt;vt+8</c> (AddRef); the function returns <c>[holder+4]</c>. A descriptor with no parameter object (<c>[fx+0x14] == 0</c>:
    /// the engine returns the holder's initial <c>[holder+4]</c>, whose ctor is unread) and one with RTPC records (<c>0xA11F98</c> per record, <c>0x9CF3A4</c>, <c>0x9E61B4</c>: unread) are required stops.
    /// </summary>
    private object? HolderInit9CF644(WwiseVoiceFxDescriptor fx, bool flag)
    {
        if (fx.CloneParams is null)
            throw new WwiseMissingBehaviourException("M6-022 4.5: 0x9CF644 with [fx+0x14] == 0 returns the holder's initial [holder+4], whose constructor is unread");
        _params = fx.CloneParams(VoiceOrThrow().PluginAllocator);                  // [holder+4] = [fx+0x14]->vt+0xC(clone, allocator)
        _fx = fx;                                                                  // [holder+8] = fx
        fx.AddRef();                                                               // fx->vt+8
        if (!flag) return _params;
        if (fx.RtpcRecordCount != 0)
            throw new WwiseMissingBehaviourException("M6-022 4.5: 0x9CF644's RTPC record loops (0xA11F98 per record, 0x9CF3A4, 0x9E61B4) are unread; only a ShareSet with no records (the shipped Compressor's) is built");
        return _params;
    }

    /// <summary><c>vt+0x24 = 0xA52678</c>: stores the upstream neighbour at <c>[W+4]</c> (<c>0xA54D8C..0xA54DAC</c>).</summary>
    public void SetUpstreamA52678(IWwiseFxChainNode upstream) => _upstream = upstream;

    /// <summary>
    /// <c>vt+0xC = 0xA7915C</c> (row 4.6), the local part: a wrapper holding its own buffer frees it and returns (the chain stops: false); otherwise the release goes on to the upstream neighbour (true; the voice walks the chain,
    /// <see cref="WwiseLiveVoice.ReleaseChainVtC"/>).
    /// </summary>
    public bool ReleaseVtC()
    {
        if (ReleaseVtCHook is { } hook) return hook();
        if (_buffer is not null) { _buffer = null; return false; }                 // 0xA7A914(pool, buf); [W+0x2C] = 0
        return true;                                                               // n = [W+4]; n->vt+0xC(n) if non-null
    }

    /// <summary><c>vt+0x10 = 0xA79108</c> (row 4.6): done returns 0x11; else the plug-in's <c>vt+0x24</c> (TimeSkip, <c>*r1</c>; the Compressor returns 0x2D, ignored) and then <c>n-&gt;vt+0x10(n, r1)</c>.</summary>
    public int Vt10(int r1)
    {
        if (Done != 0) return 0x11;
        PluginOrThrow("vt+0x10 (0xA79108)").Slot24();                              // plugin->vt+0x24(plugin, *r1): the result is ignored
        return (_upstream ?? throw new WwiseMissingBehaviourException("M6-022 4.6: vt+0x10 forwards to [W+4], which is not linked")).Vt10(r1);
    }

    /// <summary>
    /// <c>vt+0x14 = 0xA79054</c> (+ <c>0xA79384</c>, row 4.6): <c>r1 != 2</c> resets the plug-in (<c>vt+0xC</c>); then <c>r1 == 0</c> clears done and calls <c>n-&gt;vt+0x14(n, 0)</c>; otherwise, when done, it returns, else <c>n-&gt;vt+0x14(n, r1)</c>.
    /// </summary>
    public void Vt14(int r1)
    {
        if (r1 != 2) PluginOrThrow("vt+0x14 (0xA79054)").Reset();
        if (r1 == 0) { Done = 0; }
        else if (Done != 0) return;
        (_upstream ?? throw new WwiseMissingBehaviourException("M6-022 4.6: vt+0x14 forwards to [W+4], which is not linked")).Vt14(r1);
    }

    /// <summary><c>vt+0x18 = 0xA793B0</c> (row 4.6): done returns 1; else <c>n-&gt;vt+0x18(n, r1, r2)</c>.</summary>
    public int Vt18(int r1, int r2)
    {
        if (Done != 0) return 1;
        return (_upstream ?? throw new WwiseMissingBehaviourException("M6-022 4.6: vt+0x18 forwards to [W+4], which is not linked")).Vt18(r1, r2);
    }

    /// <summary><c>vt+0x1C = 0xA79018</c> (row 4.6): the plug-in's <c>vt+0xC</c> (Reset), <c>[W+0x20] = 0</c>, then <c>n-&gt;vt+0x1C(n)</c>.</summary>
    public int Vt1C()
    {
        PluginOrThrow("vt+0x1C (0xA79018)").Reset();
        Done = 0;
        return (_upstream ?? throw new WwiseMissingBehaviourException("M6-022 4.6: vt+0x1C forwards to [W+4], which is not linked")).Vt1C();
    }

    /// <summary><c>vt+0x20 = 0xA4C660</c> (rows 2.10, 4.6): <c>[W+4]</c> null returns 0, else <c>n-&gt;vt+0x20(n)</c>.</summary>
    public int Vt20() => _upstream is null ? 0 : _upstream.Vt20();

    /// <summary><c>vt+0x34 = 0xA7935C</c> (row 4.6): returns 0.</summary>
    public int Vt34() => 0;

    /// <summary><c>vt+0x40 = 0xA79364</c> (row 4.6): returns the plug-in <c>[W+0x28]</c>.</summary>
    public IWwiseEffectPlugin? Vt40() => _plugin;

    /// <summary><c>vt+0x44 = 0xA7904C</c> (row 4.6): returns <c>[W+0x30]</c>.</summary>
    public uint Vt44() => ChannelWord;

    /// <summary>
    /// <c>vt+0x2C = 0xA7933C</c> (row 4.6): <c>W-&gt;vt+0x30</c> (<see cref="Vt30A79088"/>) then <c>0xA79488(W)</c> (<c>0x9CF820(W+0x10)</c>: the holder's teardown, the parameter clone's <c>vt+0x14</c> and the descriptor's <c>vt+0xC</c>; the context is deleted and
    /// <c>[W+0xC] = 0</c>). A holder over a descriptor with RTPC records (<c>0xA0EDB0</c> per record) is a required stop.
    /// </summary>
    public void Teardown()
    {
        if (TeardownHook is { } hook) { hook(); return; }
        Vt30A79088();
        if (_fx is { } fx)
        {
            if (fx.RtpcRecordCount != 0)
                throw new WwiseMissingBehaviourException("M6-022 4.5: 0x9CF820's per-record 0xA0EDB0 is unread");
            if (_params is not null) fx.DestroyParams?.Invoke(_params, VoiceOrThrow().PluginAllocator);   // [holder+4]->vt+0x14
            _params = null;
            fx.Release();                                                         // [holder+8]->vt+0xC
            _fx = null;
        }
        _ctx = null;                                                              // 0xA79488: the ctx is deleted, [W+0xC] = 0
    }

    /// <summary><c>vt+0x30 = 0xA79088</c> (row 4.6): <c>plugin-&gt;vt+8(plugin, allocator)</c> (Term), <c>[W+0x28] = 0</c>, then the buffer <c>[W+0x2C]</c> is freed and zeroed.</summary>
    public void Vt30A79088()
    {
        if (_plugin is { } plugin)                                                 // 0xA79090: a null [W+0x28] is skipped
        {
            plugin.Term(VoiceOrThrow().PluginAllocator);
            _plugin = null;
        }
        _buffer = null;
    }

    /// <summary>Runs <c>vt+0x38</c> (<c>0xA790E8</c>, row 4.6): with <c>[W+0x20] != 0</c> it sets <c>[S+0x28] = 0x11</c> and tail-calls <c>vt+0x3C</c>; otherwise it does nothing.</summary>
    public void Execute38(WwiseVoiceBuffer buffer)
    {
        if (Execute38Hook is { } hook) { hook(buffer); return; }
        if (Done == 0) return;
        buffer.State.Code28 = 0x11;
        Process(buffer.State);
    }

    /// <summary>Runs <c>vt+0x3C</c> (<c>0xA791A8</c>, row 4.7).</summary>
    public void Execute3C(WwiseVoiceBuffer buffer)
    {
        if (Execute3CHook is { } hook) { hook(buffer); return; }
        Process(buffer.State);
    }

    /// <summary>
    /// <c>vt+0x3C = 0xA791A8(W, S)</c> (row 4.7, the process step). <c>[W+0x21] != 0</c> or <c>byte [[[W+8]+8]+0x8B] != 0</c> (<c>[pbi+0x97]</c>; a voice with no PBI context is an undefined instruction in the engine): with <c>[W+0x22] == 0</c> the plug-in is reset, then
    /// <c>[W+0x22] = 1</c> and the audio is left alone. Otherwise <c>[W+0x22] = 0</c>; a result of 0x11 sets <c>[W+0x20]</c>; with <c>[S] == 0</c> a buffer of <c>u16[S+0xC] * byte[S+4] * 4</c> bytes is allocated into <c>[W+0x2C]</c> (a failure stores
    /// <c>[S+0x28] = 2</c> and returns) and stored in <c>[S]</c> with <c>u16[S+0xE] = 0</c>; then <c>[S+8] = [S+0x28]</c>, the plug-in's <c>vt+0x20(S)</c> and <c>[S+0x28] = [S+8]</c>.
    /// </summary>
    private void Process(WwiseDecodeState s)
    {
        var plugin = PluginOrThrow("vt+0x3C (0xA791A8)");
        var voice = VoiceOrThrow();
        if (Bypass != 0 || (voice.BusOwner8 as WwisePlayingInstance ?? throw new WwiseMissingBehaviourException(
            "M6-022 4.7: 0xA791A8 reads byte [[[W+8]+8]+0x8B] ([pbi+0x97]) when [W+0x21] == 0; the voice has no PBI context in BusOwner8 (the engine's null context is an undefined instruction)")).Byte97 != 0)   // 0xA791A8..
        {
            if (ResetDone == 0) plugin.Reset();                                    // plugin->vt+0xC
            ResetDone = 1;
            return;
        }
        ResetDone = 0;
        if (s.Code28 == 0x11) Done = 1;                                            // r3 == 0x11 -> [W+0x20] = 1
        if (s.Data is null)                                                        // [S] == 0
        {
            if (voice.FxAllocationFails?.Invoke() == true) { s.Code28 = 2; return; }   // 0xA7A894 -> null: [S+0x28] = 2
            _buffer = new float[s.MaxFrames * (int)(s.ChannelConfig & 0xFF)];     // u16[S+0xC] * byte[S+4] * 4 bytes
            s.Data = _buffer;
            s.ValidFrames = 0;                                                     // u16[S+0xE] = 0
        }
        s.Scratch08 = unchecked((uint)s.Code28);                                   // [S+8] = [S+0x28]
        plugin.Execute(s);                                                         // plugin->vt+0x20(plugin, S)
        s.Code28 = unchecked((int)s.Scratch08);                                    // [S+0x28] = [S+8]
    }

    /// <summary>
    /// The creation of one slot by <c>0xA54A30</c> (rows 4.1, 4.3): the wrapper (<c>byte [info+8] != 0</c>) is allocated (a failure runs <c>plugin-&gt;vt+8(allocator)</c>, releases the descriptor and fails) and initialised through
    /// <see cref="InitA792B0"/>; the caller stores it, links it into the chain and releases the descriptor on success; on failure it runs <see cref="Teardown"/>, frees the wrapper and releases the descriptor. Returns the slot, or null (the slot stays null; <paramref name="allocationFailed"/> tells the pool failure, which ends the whole build with 2).
    /// </summary>
    internal static WwiseVoiceInsertFxSlot? Create(WwiseLiveVoice voice, int i, WwiseVoiceFxDescriptor fx, IWwiseEffectPlugin plugin, WwiseEffectFormat fmt, out bool allocationFailed)
    {
        allocationFailed = false;
        if (voice.FxAllocationFails?.Invoke() == true)                             // the 0x34-byte pool block: null
        {
            plugin.Term(voice.PluginAllocator);                                    // plugin->vt+8(0x108DA00)
            fx.Release();
            allocationFailed = true;                                               // 0xA54EC8..0xA54EFC, 0xA54A88: 0xA54A30 returns 2
            return null;
        }
        var slot = new WwiseVoiceInsertFxSlot();
        int r = slot.InitA792B0(plugin, fx, i, voice, fmt);
        if (r == 1)
        {
            fx.Release();                                                          // the caller's own reference (0xA54E88..0xA54EC0)
            return slot;
        }
        slot.Teardown();                                                           // W->vt+0x2C, W->vt+0, pool free
        fx.Release();
        return null;
    }
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

    /// <summary>
    /// The four 16-byte parameter-ramp records of V7-m (<c>voice+0x340</c>, <c>0x510</c>, <c>0x350</c>, <c>0x520</c>) are the filter bands themselves (C43.1; <c>0x340 = node(0x1D0) + 0x170</c>): <c>voice+0x340</c> = <c>FilterA.LowPass</c>,
    /// <c>0x510</c> = <c>FilterB.LowPass</c>, <c>0x350</c> = <c>FilterA.HighPass</c>, <c>0x520</c> = <c>FilterB.HighPass</c> (<see cref="WwiseVoiceFilterBand"/>: <c>+0</c> current, <c>+4</c> target, <c>u16 +8</c> steps, <c>+0xB</c> dirty).
    /// </summary>
    // fidelity: M6-011, M6-022
    public WwiseVoiceFilterBand Ramp340 => FilterA.LowPass;

    /// <summary>See <see cref="Ramp340"/>: <c>voice+0x510</c>.</summary>
    // fidelity: M6-011, M6-022
    public WwiseVoiceFilterBand Ramp510 => FilterB.LowPass;

    /// <summary>See <see cref="Ramp340"/>: <c>voice+0x350</c>.</summary>
    // fidelity: M6-011, M6-022
    public WwiseVoiceFilterBand Ramp350 => FilterA.HighPass;

    /// <summary>See <see cref="Ramp340"/>: <c>voice+0x520</c>.</summary>
    // fidelity: M6-011, M6-022
    public WwiseVoiceFilterBand Ramp520 => FilterB.HighPass;

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
                    slot.Execute38(Buffer);                                         // 0xA44664..0xA44670 vt+0x38 (the wrapper's 0xA790E8, C41.4)
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
                        slot.Execute3C(Buffer);                                     // 0xA446B0..0xA446B8 vt+0x3C (the wrapper's 0xA791A8, C41.4)
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
                    FilterA.ProcessA4C60C(Buffer.State);                            // 0xA446E0 0xA4C60C(voice+0x1C0, state): returns when [state] == 0, else 0xA766B8 with byte[S+4] channels, u16[S+0xE] frames, u16[S+0xC] stride (C43.2)
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
    /// per-connection gain / ramp stage <c>0xA56A7C</c>, which is not read (a visible stop). <c>[voice+0x388]</c> is the voice's PBI (<c>0xA54D5C</c> and <c>0xA549A0</c> store it): <see cref="Pbi388"/> (an unset one is a visible stop, the engine reads it raw). The earlier model refreshed every connection's gain
    /// pair and matrices here (<see cref="WwiseVoiceConnection.Refresh"/>); the engine does not: the connections' gains and matrices are the host's inputs to the mix.
    /// </summary>
    // fidelity: M6-022
    private void GainStageA56E00()
    {
        if (Buffer.State.Data is null) return;                                       // 0xA56E00..0xA56E08
        var pbi = Pbi388 ?? throw new WwiseMissingBehaviourException("M6-022 V2-09: 0xA56E00 reads [[voice+0x388]+0x34] raw; [voice+0x388] is not set (0xA54D5C / 0xA549A0 store it)");
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

    /// <summary>
    /// The legacy approximation's filter call: channel 0 of the voice's own planar arrays through the engine's process (one channel, all <c>Length</c> frames, stride <c>Length</c>). It is NOT an engine step: the engine runs filter A / B on the pass
    /// block <c>S</c> (<see cref="WwiseVoiceFilter.ProcessA4C60C"/>). A filter whose init <c>0xA764D4</c> never ran (the build <c>0xA54A30</c> is not part of this order) is initialised for the voice's channel count first, which the engine would have done.
    /// </summary>
    private void LegacyFilter(WwiseVoiceFilter filter)
    {
        if (!filter.IsInitialised) filter.InitA764D4((uint)Buffer.ChannelCount, 0, null);
        var data = Buffer.Channels[0];
        filter.Process(data, 1, (ushort)data.Length, (ushort)data.Length);
    }

    /// <summary>The earlier approximation for a voice whose source has no pitch node (see <see cref="Render"/>): NOT the engine's order.</summary>
    private void RenderLegacyOrder()
    {
        if (!AllowRenderOrderApproximation)
            throw new WwiseMissingBehaviourException(
                "M6-022 V8: this voice's source has no pitch node (or there is no source), so the engine's render order 0xA44630 is not reproduced for it (the in-memory ADPCM class 0xA72A2C is unread); set WwiseLiveVoice.AllowRenderOrderApproximation only for a test that accepts the approximation");
        // insert-FX slots vt+0x38 from 3..0, then vt+0x3c from 0..3, unconditionally (NOT the engine's gated walk).
        for (int i = 3; i >= 0; i--) InsertFxSlots[i]?.Execute38(Buffer);
        for (int i = 0; i < 4; i++) InsertFxSlots[i]?.Execute3C(Buffer);

        LegacyFilter(FilterA);                                   // filter A (M6-011)

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
                if (!engineOrder) LegacyFilter(FilterB);
                else FilterB.ProcessA4C60C(Buffer.State);                           // 0xA4C60C(voice+0x390, S) returns when [state] == 0, else 0xA766B8 (C43.2)
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

    // ---------------------------------------------------------------- V7 vtable seams
    //
    // 0xA54F1C calls these voice / pitch-node / filter slots. Where C41 gives a body it is built; the rest are REQUIRED seams (a missing one is a visible stop, never a default).

    /// <summary>
    /// <c>voice-&gt;vt+0x48</c> (<c>0xA533FC</c>, <see cref="StopA533FC"/>): TEST-ONLY override of the stop V7 makes at <c>0xA5530C</c>, <c>0xA55344</c>, <c>0xA55514</c>, <c>0xA555A0</c> and <c>0xA555D0</c>; unset, V7 runs <see cref="StopA533FC"/>.
    /// The bridge's required <c>0xA54480</c> call reads it too.
    /// </summary>
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

    /// <summary>
    /// <c>voice-&gt;vt+0x58</c> (<c>0xA53698</c>), called at <c>0xA554D8</c> on the E8 return-1 path for its side effect (its return is not used: <c>0xA4C584</c> gets the saved <c>pbi-&gt;vt+0x3C</c> return). Its body is report row 2.11, which C41.6 does not adopt: a REQUIRED seam.
    /// </summary>
    // fidelity: M6-022
    public Action? VoiceVt58A53698 { get; set; }

    /// <summary>
    /// The pitch node's <c>vt+0x10</c> / <c>vt+0x14</c> / <c>vt+0x18</c> / <c>vt+0x1C</c> / <c>vt+0x20</c> as the first FX slot (or the filter holder, with no slot) calls them as its upstream neighbour (row 2.10, 4.6): not extracted, so REQUIRED seams.
    /// </summary>
    // fidelity: M6-022
    public Func<int, int>? PitchNodeVt10 { get; set; }

    /// <summary>See <see cref="PitchNodeVt10"/>.</summary>
    public Action<int>? PitchNodeVt14 { get; set; }

    /// <summary>See <see cref="PitchNodeVt10"/>.</summary>
    public Func<int, int, int>? PitchNodeVt18 { get; set; }

    /// <summary>See <see cref="PitchNodeVt10"/>.</summary>
    public Func<int>? PitchNodeVt1C { get; set; }

    /// <summary>See <see cref="PitchNodeVt10"/>.</summary>
    public Func<int>? PitchNodeVt20 { get; set; }

    /// <summary>An optional observer called right after the filter A Reset <c>0xA7666C(this+0x10, r1)</c> that the holder's <c>vt+0x14</c> (<c>0xA4C5D8</c>) runs before forwarding (C43: the Reset itself is <see cref="WwiseVoiceFilter.ResetA7666C"/>; this hook is not an engine step).</summary>
    // fidelity: M6-022
    public Action<int>? FilterAVt14A7666C { get; set; }

    private sealed class PitchNodeChain : IWwiseFxChainNode
    {
        private readonly WwiseLiveVoice _voice;
        public PitchNodeChain(WwiseLiveVoice voice) => _voice = voice;
        public int Vt10(int r1) => (_voice.PitchNodeVt10 ?? throw Missing("vt+0x10"))(r1);
        public void Vt14(int r1) => (_voice.PitchNodeVt14 ?? throw Missing("vt+0x14"))(r1);
        public int Vt18(int r1, int r2) => (_voice.PitchNodeVt18 ?? throw Missing("vt+0x18"))(r1, r2);
        public int Vt1C() => (_voice.PitchNodeVt1C ?? throw Missing("vt+0x1C"))();
        public int Vt20() => (_voice.PitchNodeVt20 ?? throw Missing("vt+0x20"))();
        private static WwiseMissingBehaviourException Missing(string slot)
            => new($"M6-022 2.10: the pitch node's {slot} (the end of the filter holder's / the first FX slot's chain) is not extracted; supply WwiseLiveVoice.PitchNodeVt{slot[5..]}");
    }

    private IWwiseFxChainNode? _pitchChain;

    /// <summary>
    /// <c>[voice+0x1C4]</c>, the filter holder's upstream neighbour (row 2.10, <c>0xA54D8C..0xA54DAC</c>: the chain is src, pitch node, the created slots in slot order, the holder): the highest filled slot, else the pitch node.
    /// </summary>
    // fidelity: M6-022
    public IWwiseFxChainNode HolderUpstream1C4
    {
        get
        {
            for (int i = InsertFxSlots.Length - 1; i >= 0; i--)
                if (InsertFxSlots[i] is { } slot) return slot;
            return _pitchChain ??= new PitchNodeChain(this);
        }
    }

    /// <summary>The slot's upstream neighbour (the previous filled slot, else the pitch node): what <c>vt+0x24</c> stores in <c>[W+4]</c> (<c>0xA54D8C..0xA54DAC</c>).</summary>
    internal IWwiseFxChainNode UpstreamOfSlot(int index)
    {
        for (int i = index - 1; i >= 0; i--)
            if (InsertFxSlots[i] is { } slot) return slot;
        return _pitchChain ??= new PitchNodeChain(this);
    }

    /// <summary>
    /// <c>[voice+0x1C0]-&gt;vt+0xC</c> (<c>0xA4C5B0</c>, row 2.10): <c>n = [this+4]</c> and, when non-null, <c>n-&gt;vt+0xC(n)</c>: the release walk <see cref="ReleaseChainVtC"/> (the slots from the highest, each stopping the walk when it frees a buffer, then the pitch node).
    /// </summary>
    // fidelity: M6-022
    public void HolderVt0C() => ReleaseChainVtC();

    /// <summary><c>[voice+0x1C0]-&gt;vt+0x10(&amp;r1)</c> (<c>0xA4C5C8</c>, row 2.10): <c>n-&gt;vt+0x10(n, r1)</c> with no null check.</summary>
    // fidelity: M6-022
    public int HolderVt10(int r1) => HolderUpstream1C4.Vt10(r1);

    /// <summary><c>[voice+0x1C0]-&gt;vt+0x14(r1)</c> (<c>0xA4C5D8</c>, row 2.10, C43): <c>0xA7666C(this+0x10, r1)</c> (filter A's Reset, <see cref="WwiseVoiceFilter.ResetA7666C"/>; <see cref="FilterAVt14A7666C"/> only observes it), then <c>n-&gt;vt+0x14(n, r1)</c>.</summary>
    // fidelity: M6-011, M6-022
    public void HolderVt14(int r1)
    {
        FilterA.ResetA7666C();                                       // 0xA4C5E0..0xA4C5E8 bl 0xA7666C(voice+0x1C0+0x10)
        FilterAVt14A7666C?.Invoke(r1);
        HolderUpstream1C4.Vt14(r1);
    }

    /// <summary><c>[voice+0x1C0]-&gt;vt+0x18(r1, r2)</c> (<c>0xA4C620</c>, row 2.10): a null <c>[this+4]</c> returns 1, else <c>n-&gt;vt+0x18(n, r1, r2)</c>.</summary>
    // fidelity: M6-022
    public int HolderVt18(int r1, int r2) => HolderUpstream1C4.Vt18(r1, r2);

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

    /// <summary>
    /// The node's <c>vt+0xE8</c> = <c>0x9EEF2C(node = [pbi+0xE0], i, &amp;out, [pbi+0x14])</c> (rows 4.1, 2.11; <c>0xA533CC</c>, <c>0xA019B8</c>): <c>out = {fx*, byte}</c>: the FX descriptor of slot <c>i</c> (null: none) and the BYPASS BYTE the resolver itself writes at <c>out+4</c>
    /// (<c>0x9EEFEC</c>; 0 at <c>0x9EF0D4</c>). The build reaches it through <c>0xA019B8</c>, which first tests <c>[pbi+0xE9]</c> bit 2 (set: the descriptor is released, nothing resolved, no descriptor); voice <c>vt+0x70</c> (<c>0xA5338C</c>) calls it directly with no such gate.
    /// The resolver body is not extracted: a REQUIRED seam. Each call hands out a reference the caller releases.
    /// </summary>
    // fidelity: M6-022
    public Func<int, (WwiseVoiceFxDescriptor? Fx, byte Bypass)>? ResolveNodeFx9EEF2C { get; set; }

    /// <summary>
    /// <c>vt+0x24(this = voice+0x380 node, upstream)</c> (<c>0xA54D98..0xA54DAC</c>, the FIRST call of the link loop, with the filter holder <see cref="HolderNode1C0"/> as its upstream): its body is not extracted (the node's vtable was not identified), so it is a
    /// REQUIRED seam of the build; the argument is the upstream node object.
    /// </summary>
    // fidelity: M6-022
    public Action<object>? GainNode380Vt24 { get; set; }

    /// <summary>The filter holder object at <c>voice+0x1C0</c> as an element of the build's chain array (<c>0xA54D40</c>).</summary>
    // fidelity: M6-022
    public object HolderNode1C0 { get; } = new();

    /// <summary>The <c>voice+0x380</c> gain-stage node as the last element of the build's chain array (<c>0xA54D84</c>).</summary>
    // fidelity: M6-022
    public object GainNode380 { get; } = new();

    /// <summary>The <c>(node, upstream)</c> pairs of the last build's <c>vt+0x24</c> loop, in call order (<c>0xA54D8C..0xA54DB8</c>).</summary>
    // fidelity: M6-022
    public List<(object Node, object Upstream)> ChainLinks { get; } = new();

    internal IWwiseFxChainNode PitchChainNode => _pitchChain ??= new PitchNodeChain(this);

    /// <summary>
    /// <c>0x9CC2AC(id, &amp;plugin, &amp;info)</c> (row 4.1): the plug-in registry lookup and <c>create(allocator)</c>; null is "not registered" (the slot stays null). The registry body is not extracted: a REQUIRED seam. <c>plugin-&gt;vt+0x10(info)</c> (GetPluginInfo) is the plug-in's.
    /// </summary>
    // fidelity: M6-022
    public Func<uint, IWwisePluginMemAlloc, IWwiseEffectPlugin?>? PluginRegistry9CC2AC { get; set; }

    /// <summary>The plug-in allocator <c>0x108DA00</c> every FX call passes (a managed stand-in: it only answers whether an allocation succeeds and records the frees).</summary>
    // fidelity: M6-022
    public IWwisePluginMemAlloc PluginAllocator { get; set; } = new WwisePluginAllocator();

    /// <summary>The engine's pool allocations of the FX path (<c>0xA7A7F4</c>/<c>0xA7A894</c>: the 0x34-byte wrapper, the 0x18-byte context, the process buffer): true makes the call return null. Null never fails.</summary>
    // fidelity: M6-022
    public Func<bool>? FxAllocationFails { get; set; }

    /// <summary><c>[voice+0x388]</c> (<c>[[voice+0x380]+8]</c>): the PBI <c>0xA5676C(voice+0x380, pbi)</c> stores (<c>0xA54D5C</c>); <see cref="GainStageA56E00"/> reads it.</summary>
    // fidelity: M6-022
    public WwisePlayingInstance? Pbi388 { get; set; }

    /// <summary>The two pool allocations of <c>0xA764D4</c> (the filter's coefficient and history blocks): true makes a call return null (the init returns 2). Null never fails.</summary>
    // fidelity: M6-022
    public Func<bool>? FilterAllocationFails { get; set; }

    /// <summary>V7-f <c>voice-&gt;vt+0x6C</c>: the voice start hook.</summary>
    public Action? VoiceStart6C { get; set; }

    /// <summary>The table <c>[voice+0x10]</c> (<see cref="WwiseVoiceSendTable"/>).</summary>
    public WwiseVoiceSendTable? SendTable { get; set; }

    /// <summary>V7/C1 <c>0xA4BC58</c> the four output-float minima <c>[sp+0x5c..0x68]</c>.</summary>
    public float[] OutputMin50 { get; } = new float[4];
}

/// <summary>
/// The <c>param_12</c> of <c>0xA4BC58</c> (verification, rows 1.5 and 1.18, X2): a pointer to <c>{[pbi+0x140], word}</c> when <c>[pbi+4] &amp; 0x10</c> is set, else 0. <paramref name="PlayingId"/> is the playing id (<c>[pbi+0x140]</c>); <paramref name="Word"/> is the
/// <c>[voice+0xF0]</c> V7 read at <c>0xA54F28</c> (<c>r8</c>): the second call at <c>0xA5572C</c> passes that stale value, not the current word.
/// </summary>
// fidelity: M6-022
public readonly record struct WwiseGainArg12(uint PlayingId, uint Word);

/// <summary>
/// <c>0xA4BC58</c> as V7 calls it (rows 1.5, 1.18); the return is the byte <c>[sp+0x2F]</c> (S2F). The callee also writes <see cref="WwiseLiveVoice.Run2E"/> (<c>[sp+0x2E]</c>) and the four float outputs <paramref name="floatOutputs"/>.
/// </summary>
// fidelity: M6-022
public delegate bool WwiseConnectionGainsA4BC58(WwiseLiveVoice voice, WwisePlayingInstance pbi, float gain, byte arg5, WwiseGainArg12? arg12, float[] floatOutputs);

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
/// <item><b>The per-voice state machine <c>0xA54F1C</c> (V7)</b> is built on the owner PBI (C41, <see cref="RunVoiceStateMachine"/>, checked against the engine in <c>WwiseVoiceStateOracleTests</c>); what C41 does not give (<c>0xA370E4</c>, voice <c>vt+0x58</c>, the pitch node's chain slots, <c>0xA7666C</c>, <c>0x99CC40</c>,
/// the FX resolver, registry and <c>&amp;fmt</c>) is a required seam that throws <see cref="WwiseMissingBehaviourException"/>.</item>
/// <item><b>The insert-FX slot</b> is the in-place wrapper <see cref="WwiseVoiceInsertFxSlot"/> (C41.4); the out-of-place class <c>0x103DC38</c> is not extracted (a required stop).</item>
/// <item><b>The bus-to-bus mix <c>0xA4F9E0</c></b> is <see cref="MixOutputBus"/> (C44.3 G10) over M6-012's <see cref="WwiseMixKernels"/> (<c>0xA45E9C</c> / <c>0xA46668</c>) with the bus gain stage <see cref="WwiseMixBus.GainStageA4D994"/>; <see cref="WwiseMixerConnection"/> is the EARLIER model (legacy render order only).</item>
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
    public void VoicePass(int arg)
    {
        // fidelity: M6-022, M6-025, M6-026
        // arg = the pass's first argument (0xA44948 r0 -> [sp+4]): 0xA44DE0..0xA44DF4 byte [A] == 0 ? 1 : byte [B] (WwiseOutputDeviceState.BusPassArg), also the argument of 0xA44C18; the engine passes it.
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
            bool v7 = voice.State == 1 && RunVoiceStateMachine(voice);   // V7 (0xA44B50): returns 1 when the voice has a live source
            if (v7 && (arg & 1) != 0)                                // 0xA44B54..0xA44B5C ldr r3,[sp,#4]; tst r0,r3; beq 0xA44BAC (no render, no 0xA5495C / 0xA55CC4)
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
        RequireBaseCtx(pbi);                                         // the ctx vt+0x24 / vt+0x28 of the other PBI classes are not read (C41.3)
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
    /// <c>0xA4BC58</c> as V7 calls it (C41.1 row 1.5): <c>(voice, ctx = pbi+0xC, id = [voice+0xF0], gain, arg5, &amp;S2E, &amp;S2F, &amp;f30, &amp;f34, &amp;f38, &amp;f3C, arg12)</c>. The returned byte is <c>[sp+0x2F]</c>; the callee also writes <see cref="WwiseLiveVoice.Run2E"/>
    /// (<c>[sp+0x2E]</c>) and the four float outputs. TEST-ONLY override (the V7 oracle stands in for the callee).
    /// </summary>
    internal WwiseConnectionGainsA4BC58? ConnectionGainsOverrideA4BC58 { get; set; }

    /// <summary><c>0xA4B4B0(voice)</c> as V7 calls it (<c>0xA55644</c>): TEST-ONLY override (the V7 oracle stands in for the callee).</summary>
    internal Action<WwiseLiveVoice>? DuckingOverrideA4B4B0 { get; set; }

    /// <summary><c>0xA54A30(voice)</c> as V7 calls it (<c>0xA555C0</c>): TEST-ONLY override (the V7 oracle stands in for the callee); unset, <see cref="StartStreamAndBuildInsertFx"/>.</summary>
    internal Func<WwiseLiveVoice, int>? StartStreamOverrideA54A30 { get; set; }

    /// <summary>
    /// The limiter that owns the global the acquire <c>0xA0228C</c> increments (<c>0x1040144</c>'s target, <see cref="WwisePlaybackLimiter.GlobalVirtualCount"/>); REQUIRED when V7 acquires.
    /// </summary>
    // fidelity: M6-022
    public WwisePlaybackLimiter? Limiter { get; set; }

    /// <summary>
    /// V7 <c>0xA54F1C(voice, S)</c> on the OWNER PBI <c>[[voice+0xD4]+0xC]</c> (C41.1, rows 1.1..1.19, 1.20; the pre-pass <c>0xA55750</c> runs before it every pass, C41.2, <see cref="PrePassVoiceA55750"/>). Order:
    /// <list type="number">
    /// <item>the gate on <c>[pbi+0x1F8]</c> (-1 continues with S untouched; otherwise <c>[S+0x2C] = 1</c> and 0 returns 0, <c>0xA5531C</c>), <c>[S+4] = [voice+0xF0]</c> (the channel-config word, <c>0xA54F64</c>), the gain <c>lin(([pbi+0x54]+[pbi+0x11C])*0.05f)</c> times <c>[[src+8]+4]</c>
    /// and, with <c>byte [pbi+0x58]</c> bit 0, <c>[[src+8]]</c> (row 1.4);</item>
    /// <item><c>0xA4BC58</c> with <c>arg5</c> from <c>src-&gt;vt+0x4C</c> (a pure getter: bit 6 of <c>byte [pbi+0x1BE]</c>, verification D1) and <c>arg12 = ([pbi+4]&amp;0x10) ? {[pbi+0x140], [voice+0xF0] as read at entry} : 0</c> (rows 1.5, 1.6);</item>
    /// <item>the scaled frames <c>s = round-half-away(float(u16 [S+0xC]) * [pbi+0x164])</c> in single precision (row 1.7); with <c>S2F != 0</c> and a connection list the four ramps (row 1.9) and the branch table (rows 1.10, 1.12), with <c>S2F == 0</c> the path <c>0xA5532C</c> (row 1.11);</item>
    /// <item>the E8 gate <c>0xA5521C</c> (row 1.13), the budget step (row 1.15), the dispatch (row 1.16), <c>[voice+0xCD]</c> bit 0 := S2F, the build <c>0xA54A30</c> when r5 and <c>[voice+0x1B4]</c> is clear (a result other than 1 stops the voice and V7 returns 0, verification D2) and the <c>0xA5561C</c> tail in every case of
    /// <c>[pbi+0xE8]&amp;0x20</c> / <c>[pbi+0xE9]&amp;1</c> (row 1.18), then <c>[voice+0xCD] |= 8</c> and <c>r5</c> (row 1.19).</item>
    /// </list>
    /// <c>pbi-&gt;vt+0x3C</c> is class specific (C41.3, <see cref="WwisePlayingInstance.RequestVt3C"/>); the voice's own vt slots, the filter holder's methods and the pitch node's slots that C41 does not give are required seams.
    /// </summary>
    /// <returns>True when the voice has a live source and the V8 dispatcher should run.</returns>
    // fidelity: M6-022
    public bool RunVoiceStateMachine(WwiseLiveVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var source = voice.Source ?? throw new WwiseMissingBehaviourException("M6-022 V7: the voice has no current source ([voice+0xD4] == 0); 0xA54F1C dereferences it (0xA54F20)");
        var pbi = OwnerOfSource(voice);                              // 0xA54F20, 0xA54F2C r5 = [voice+0xD4], r6 = [r5+0xC]
        uint r8 = voice.Word0xF0;                                    // 0xA54F28 ldr r8,[r0,#0xf0]: the value both arg12 words use (0xA54F5C, 0xA55658)
        var state = voice.Buffer.State;

        // 0xA54F34..0xA54F4C (row 1.3): the gate on [pbi+0x1F8].
        uint gate = pbi.Field1F8;
        if (gate != 0xFFFFFFFFu)
        {
            voice.Buffer.HasBusParam = true;                         // 0xA54F48 strb 1,[S+0x2C]
            if (gate == 0) return false;                             // 0xA54F4C beq 0xA5531C
        }

        // 0xA54F50..0xA54FEC (row 1.4).
        state.ChannelConfig = r8;                                    // 0xA54F64 str r8,[r1,#4]
        float gain = SourceGainA54F50(source, pbi);

        // 0xA54FF0..0xA55058 (rows 1.5, 1.6): call 1.
        byte arg5 = SourceVt4C(pbi);                                 // 0xA55008 src->vt+0x4C
        var arg12 = Arg12(pbi, r8);                                  // 0xA5501C..0xA55028
        bool s2f = RunConnectionGains(voice, pbi, gain, arg5, arg12, voice.OutputMin50);

        // 0xA55090..0xA550C4 (row 1.7).
        int s = ScaledFramesA55090(state.MaxFrames, pbi.Ratio);

        bool r5;
        if (!s2f)
        {
            r5 = Path5532C(voice, pbi, s);                           // 0xA550C8 beq 0xA5532C
        }
        else
        {
            if (voice.Connections.Count != 0) RunParameterRamps(voice, pbi);   // 0xA550CC..0xA551EC (row 1.9)
            bool a = (voice.FlagsCD & 1) != 0;                       // 0xA551F0..0xA551F8
            int e4 = voice.E4;
            bool src10 = source.StartStreamSucceeded;                // byte [src+0x10] bit 0

            if (a)
            {
                if (src10) r5 = true;                                // 0xA55200..0xA55208 -> 0xA55218
                else if (e4 == 2) r5 = Path554F0(voice, pbi, source, ref s2f);   // 0xA5520C..0xA55214 -> 0xA554F0
                else r5 = true;
            }
            else
            {
                if (e4 != 2) r5 = true;                              // 0xA552B0..0xA552B8 -> 0xA55218
                else if (!src10) r5 = Path554F0(voice, pbi, source, ref s2f);    // 0xA552BC..0xA552C4 -> 0xA554F0
                else if (!Path552C8(voice, pbi, out r5)) return false;           // 0xA552C8: the stop 0xA5530C returns 0 with no tail
            }
        }

        // 0xA5521C (row 1.13): the E8 gate.
        if (voice.FlagE8)
        {
            int r = pbi.RequestVt3C(voice.E0);                       // 0xA553A4..0xA553B4 pbi->vt+0x3C(pbi, E0)
            if (r == 1)
            {
                (voice.VoiceVt58A53698 ?? throw new WwiseMissingBehaviourException(
                    "M6-022 2.11: voice vt+0x58 (0xA53698), called at 0xA554D8 on the E8 return-1 path, is report row 2.11, which C41.6 does not adopt; supply WwiseLiveVoice.VoiceVt58A53698"))();
                SetConnectionBit2(voice, true);                      // 0xA554E8 0xA4C584(voice, r & 1) with r == 1
            }
            else if (r == 2)
            {
                Stop48(voice);                                       // 0xA555A0
                r5 = false;
            }
            voice.FlagE8 = false;                                    // 0xA553C8 bfc bit 0 (all three cases)
        }

        // 0xA55228..0xA55244 (row 1.15): the budget step.
        int budget = unchecked((int)pbi.StartOffset);                // [pbi+0x1D8], signed
        if (budget >= s) r5 = false;                                 // 0xA55230..0xA55234 cmp r3,r2; movge r5,#0
        if (budget >= 0) pbi.StartOffset = unchecked((uint)(budget - s));   // 0xA5523C..0xA55244

        // 0xA55248..0xA55268, 0xA55384..0xA55398, 0xA5547C..0xA55488 (row 1.16): the dispatch.
        if (voice.E4 != 0)
        {
            if ((voice.FlagsCD & 1) == 0) { if (s2f) ReleaseVirtualA022E8(pbi); }   // 0xA55384 -> 0xA022E8(pbi, 1)
            else if (!s2f) AcquireVirtualA0228C(pbi);                // 0xA5547C -> 0xA0228C(pbi)
        }

        // 0xA5526C..0xA5528C (row 1.17).
        voice.FlagsCD = (byte)((voice.FlagsCD & ~1) | (s2f ? 1 : 0));       // 0xA55274 bfi r3,r2,#0,#1
        if (r5 && voice.PitchNode.Pbi is null)                       // 0xA55280..0xA55288 [voice+0x1B4] == 0
        {
            int r = StartStreamA54A30(voice);                        // 0xA555C0 bl 0xA54A30
            if (r == 1)
                TailA555F0(voice, pbi, r8);                          // 0xA555F0 (row 1.18)
            else
            {
                r5 = false;                                          // 0xA555D4 mov r5,r3 (r3 = [voice+0x1B4] = 0)
                Stop48(voice);                                       // 0xA555DC..0xA555E0
            }
        }

        voice.FlagsCD = (byte)(voice.FlagsCD | 8);                   // 0xA5528C..0xA55298 (row 1.19)
        return r5;
    }

    /// <summary>
    /// <c>0xA5561C..0xA55734</c> (row 1.18), after a successful <c>0xA54A30</c>: the context call by <c>[pbi+0xE8]&amp;0x20</c> (clear: <c>vt+0x24</c> = CalcEffectiveParams; set with <c>[pbi+0xE9]&amp;1</c>: <c>vt+0x28</c> = <c>0x9FF368</c>; set with the bit clear: nothing), then in every case
    /// <c>[pbi+0xC4] = 101.0f</c>, <c>[S+4] = [voice+0xF0]</c> (re-read), <c>[voice+0xCD]</c> bit 3 cleared, <c>0xA4B4B0(voice)</c>, the gain recomputed from the fresh <c>[pbi+0x54]+[pbi+0x11C]</c> and <c>src-&gt;vt+0x4C</c> read a second time, and the second <c>0xA4BC58</c> with its
    /// four float outputs at <c>sp+0x40</c> (discarded; <c>&amp;S2E</c> / <c>&amp;S2F</c> are still passed) and <c>arg12 = {[pbi+0x140], r8}</c> with the STALE <c>r8</c> (<c>0xA54F28</c>).
    /// </summary>
    // fidelity: M6-022
    private void TailA555F0(WwiseLiveVoice voice, WwisePlayingInstance pbi, uint stale)
    {
        var state = voice.Buffer.State;
        if ((pbi.Flags0E8 & 0x20) == 0)                              // 0xA555F0..0xA555FC
            CtxCalcEffectiveParams(pbi);                             // 0xA5573C..0xA55748 ctx->vt+0x24 (0xA000E0 -> 0x9FFAD4), r1 = 0
        else if ((pbi.Flags0E9 & 1) != 0)                            // 0xA55600..0xA55608
        {
            RequireBaseCtx(pbi);
            WwisePlayPath.Recompute9FF368(pbi);                      // 0xA5560C..0xA55618 ctx->vt+0x28 (0x9FF414 -> 0x9FF368)
        }
        pbi.FieldC4 = Float101;                                      // 0xA55620..0xA55628 [pbi+0xC4] = 0x42CA0000
        state.ChannelConfig = voice.Word0xF0;                        // 0xA5561C ldr r3,[r4,#0xf0]; 0xA55634 str r3,[r7,#4]
        voice.FlagsCD = (byte)(voice.FlagsCD & ~8);                  // 0xA55638..0xA55640 bfc bit 3
        DuckA4B4B0(voice);                                           // 0xA55644 bl 0xA4B4B0
        var source2 = voice.Source ?? throw new WwiseMissingBehaviourException("M6-022 V7: the voice lost its source across 0xA54A30 ([voice+0xD4] re-read at 0xA55648)");
        var pbi2 = OwnerOfSource(voice);                             // 0xA55648..0xA55654 r2 = [[voice+0xD4]+0xC]
        float gain2 = SourceGainA54F50(source2, pbi2);               // 0xA55660..0xA556E0: the fresh gain
        byte arg5 = SourceVt4C(pbi2);                                // 0xA556E4..0xA556EC src->vt+0x4C, a second time
        var arg12 = Arg12(pbi, stale);                               // 0xA556F0..0xA55704 ([pbi+4]&0x10) ? &{[pbi+0x140], r8}
        RunConnectionGains(voice, pbi, gain2, arg5, arg12, _sp40);   // 0xA55708..0xA5572C bl 0xA4BC58
    }

    private static readonly float Float101 = BitConverter.Int32BitsToSingle(0x42CA0000);

    /// <summary><c>0xA54F50..0xA54FEC</c> / <c>0xA55648..0xA556E0</c> (row 1.4): the V7 gain.</summary>
    private static float SourceGainA54F50(IWwiseVoiceSource source, WwisePlayingInstance pbi)
    {
        float lin = WwisePlaybackLimiter.Lin9BEB30(pbi.Field54 + pbi.Ranges118.MakeUpGain);   // 0xA54F74 vadd.f32; the *0.05f, the -37.0f floor and the fast power (the same code, 1.4 note)
        if (source.Gain8 is { } g)                                   // 0xA54FD0 cmp r2,#0 ([src+8])
        {
            lin = WwiseArmFloat.Mul(lin, g.At4);                      // 0xA54FD8, 0xA54FE4 vmul.f32 s16,s16,[r2+4] (the engine's invalid-operation NaN)
            if ((pbi.Byte58 & 1) != 0) lin = WwiseArmFloat.Mul(lin, g.At0);   // 0xA54FDC..0xA54FEC vmulne.f32 s16,s16,[r2]
        }
        return lin;
    }

    /// <summary>
    /// <c>src-&gt;vt+0x4C</c> (verification D1): a pure getter, bit 6 of <c>byte [[src+0xC]+0x1BE]</c> (base source vtable <c>0x103C848</c> slot <c>0xA566C8</c>; derived <c>0x103D6C0</c>, <c>0x103D740</c>, <c>0x103D7C0</c>, <c>0x103D840</c> slot <c>0xA72B14</c>); the argument 5 of <c>0xA4BC58</c>.
    /// </summary>
    // fidelity: M6-022
    public static byte SourceVt4C(WwisePlayingInstance owner) => (byte)((owner.Flags1BE >> 6) & 1);

    private static WwiseGainArg12? Arg12(WwisePlayingInstance pbi, uint word)
        => (pbi.Flags4 & 0x10) != 0 ? new WwiseGainArg12(pbi.PlayingId, word) : null;      // 0xA5501C..0xA55028: ([pbi+4]&0x10) ? &{[pbi+0x140], r8} : 0

    /// <summary>
    /// <c>0xA55090..0xA550C4</c> (row 1.7): <c>vcvt.f32.u32(u16)</c> times <c>[pbi+0x164]</c>, then <c>+0.5f</c> when the product is above 0 (else <c>-0.5f</c>: zero, negative and NaN) and <c>vcvt.s32.f32</c> (toward zero, saturating, NaN to 0): round-half-away-from-zero in single precision.
    /// </summary>
    // fidelity: M6-022
    public static int ScaledFramesA55090(ushort maxFrames, float ratio)
    {
        float s15 = (float)maxFrames * ratio;                        // 0xA550A4 vcvt.f32.u32; 0xA550A8 vmul.f32 s15,s15,s13
        float half = s15 > 0f ? 0.5f : -0.5f;                        // 0xA550AC..0xA550B8 vmov.f32 s16,#-0.5; vcmpe s15,#0; vmovgt s16,s14
        return FloatToS32(s15 + half);                               // 0xA550C0 vadd.f32; 0xA550C4 vcvt.s32.f32
    }

    /// <summary><c>vcvt.s32.f32</c>: truncation toward zero, saturating, NaN to 0.</summary>
    public static int FloatToS32(float v)
    {
        if (float.IsNaN(v)) return 0;
        if (v >= 2147483648f) return int.MaxValue;
        if (v <= -2147483648f) return int.MinValue;
        return (int)v;
    }

    /// <summary>The call of <c>0xA4BC58</c> (<see cref="ConnectionGainsOverrideA4BC58"/> or the real one).</summary>
    private bool RunConnectionGains(WwiseLiveVoice voice, WwisePlayingInstance pbi, float gain, byte arg5, WwiseGainArg12? arg12, float[] outputs)
        => ConnectionGainsOverrideA4BC58 is { } over
            ? over(voice, pbi, gain, arg5, arg12, outputs)
            : UpdateConnectionGains(voice, pbi, gain, arg5, arg12, outputs);

    /// <summary><c>0xA4B4B0(voice)</c> (row 2.4): <c>line = [voice+0xC]</c>, null returns; <see cref="ApplyDucking"/> otherwise.</summary>
    private void DuckA4B4B0(WwiseLiveVoice voice)
    {
        if (DuckingOverrideA4B4B0 is { } over) { over(voice); return; }
        if (voice.DryLineC is not { } line) return;
        ApplyDucking(voice, line);
    }

    private int StartStreamA54A30(WwiseLiveVoice voice)
        => StartStreamOverrideA54A30 is { } over ? over(voice) : StartStreamAndBuildInsertFx(voice);

    /// <summary><c>voice-&gt;vt+0x48</c> (<c>0xA533FC</c>): <see cref="WwiseLiveVoice.VoiceStop48"/> when a host or test supplies one, else <see cref="WwiseLiveVoice.StopA533FC"/>.</summary>
    private void Stop48(WwiseLiveVoice voice)
    {
        if (voice.VoiceStop48 is { } stop) stop();
        else voice.StopA533FC(OwnerOfSourceRaw);
    }

    private static void RequireBaseCtx(WwisePlayingInstance pbi)
    {
        if (pbi.PbiClass != WwisePbiClass.Base)
            throw new WwiseMissingBehaviourException("M6-022 C41.3: the PBI context's vt+0x24 / vt+0x28 of the 0x9883AC and 0xA6A8A0 classes are not read; only the base / Sound PBI's (ctx vptr 0x103B7DC: 0xA000E0, 0x9FF414) are");
    }

    /// <summary>The PBI context's <c>vt+0x24</c> (row 1.18; <c>0xA000E0</c> -&gt; PBI <c>vt+0x44</c> = <c>0x9FFAD4</c>, CalcEffectiveParams) with <c>r1 = 0</c>.</summary>
    private void CtxCalcEffectiveParams(WwisePlayingInstance pbi)
    {
        RequireBaseCtx(pbi);
        (CalcEffectiveParamsVt24 ?? throw new WwiseMissingBehaviourException(
            "M6-022 1.18: the PBI context's vt+0x24 (CalcEffectiveParams, 0x9FFAD4, r1 = 0) is a required seam; supply WwiseVoiceBusPass.CalcEffectiveParamsVt24"))(pbi);
    }

    /// <summary>
    /// V7 <c>0xA552C8</c> (<c>E4 == 2</c>, <c>SRC10</c> set, <c>A == 0</c>, rows 1.10, 1.14): <c>pbi-&gt;vt+0x3C(pbi, E0)</c>; a result of 2 stops (<c>0xA5530C</c>: V7 returns 0 with no tail); 1 gives <c>r2 = 1</c>, any other 0; then <c>[voice+0x1C0]-&gt;vt+0x18(E0, r2)</c>
    /// (<see cref="WwiseLiveVoice.HolderVt18"/>): 1 continues at <c>0xA55218</c> (<paramref name="r5"/> = 1), anything else stops. Returns false when V7 returns at once.
    /// </summary>
    private bool Path552C8(WwiseLiveVoice voice, WwisePlayingInstance pbi, out bool r5)
    {
        r5 = false;
        int r = pbi.RequestVt3C(voice.E0);                           // 0xA552C8..0xA552D8
        if (r == 2) { Stop48(voice); return false; }                 // 0xA552E8 beq 0xA5530C
        int r2 = r == 1 ? 1 : 0;                                     // 0xA555E8 mov r2,r0 / 0xA552EC mov r2,r5 (r5 = 0 here)
        int f = voice.HolderVt18(voice.E0, r2);                      // 0xA552F0..0xA55300 [voice+0x1C0]->vt+0x18
        if (f == 1) { r5 = true; return true; }                      // 0xA55308 beq 0xA55218
        Stop48(voice);                                               // 0xA5530C
        return false;
    }

    /// <summary>
    /// V7 <c>0xA5532C</c> (<c>S2F == 0</c>, row 1.11), entered with <c>r5 = 0</c>: <c>E4 == 2</c> with <c>A</c> set runs the holder's <c>vt+0x14(E0)</c> (<see cref="WwiseLiveVoice.HolderVt14"/>) and, unless <c>E0 == 2</c> (r5 = 0), its <c>vt+0xC</c>; then (also with
    /// <c>A</c> clear) <c>E0 == 1</c> with <c>[pbi+0x1D8] &lt; s</c> runs the holder's <c>vt+0x10(&amp;s)</c> and stores its result in <c>[S+0x28]</c> (r5 = 0 whichever); <c>E4 == 1</c> stops the voice (r5 = 0); any other <c>E4</c> continues with r5 = 1.
    /// </summary>
    private bool Path5532C(WwiseLiveVoice voice, WwisePlayingInstance pbi, int s)
    {
        if (voice.E4 == 2)                                           // 0xA55334..0xA55338
        {
            if ((voice.FlagsCD & 1) != 0)                            // 0xA55490 tst r2,#1
            {
                voice.HolderVt14(voice.E0);                          // 0xA55534..0xA55548 [voice+0x1C0]->vt+0x14(E0)
                if (voice.E0 == 2) return false;                     // 0xA5554C..0xA55554 -> 0xA554A4
                voice.HolderVt0C();                                  // 0xA55558..0xA55564 [voice+0x1C0]->vt+0xC
            }
            if (voice.E0 == 1)                                       // 0xA55498..0xA554A0
            {
                int budget = unchecked((int)pbi.StartOffset);        // 0xA5556C..0xA55570
                if (budget < s)                                      // 0xA55574..0xA55578 cmp r3,r2; bge 0xA554A4
                    voice.Buffer.State.Code28 = voice.HolderVt10(s); // 0xA5557C..0xA55598 vt+0x10(&s), str r0,[S+0x28]
            }
            return false;                                            // 0xA554A4 r5 = 0 / 0xA55588
        }
        if (voice.E4 == 1)                                           // 0xA5533C..0xA55340
        {
            Stop48(voice);                                           // 0xA55344..0xA55350
            return false;                                            // 0xA55354 b 0xA5521C with r5 = 0
        }
        return true;                                                 // 0xA55340 bne 0xA55218
    }

    /// <summary>
    /// V7-m <c>0xA550CC..0xA551F0</c> (row 1.9): the four parameter ramps, run only when <c>S2F != 0</c> and <c>[voice+0x28] != 0</c>. The targets are <c>f30/f34/f38/f3C</c>: with <c>S2E != 0</c> the copy at <c>0xA5505C..0xA5508C</c> replaces them by the stored targets
    /// (<c>[voice+0x344]</c>, <c>[voice+0x514]</c>, <c>[voice+0x354]</c>, <c>[voice+0x524]</c>); otherwise they are the four <see cref="WwiseLiveVoice.OutputMin50"/> minima. Ramps 2 and 4 first take <c>max</c> with <c>[pbi+0x68]</c> / <c>[pbi+0x6C]</c>; all clamp to 0 below and 100.0f above.
    /// </summary>
    private static void RunParameterRamps(WwiseLiveVoice voice, WwisePlayingInstance pbi)
    {
        float t1 = voice.Run2E ? voice.Ramp340.Target : voice.OutputMin50[0];     // f30
        float t2 = voice.Run2E ? voice.Ramp510.Target : voice.OutputMin50[1];     // f34
        float t3 = voice.Run2E ? voice.Ramp350.Target : voice.OutputMin50[2];     // f38
        float t4 = voice.Run2E ? voice.Ramp520.Target : voice.OutputMin50[3];     // f3C

        RunRamp(voice.Ramp340, t1, hasFloor: false, floor: 0f);                   // 0xA550D8..0xA55104
        RunRamp(voice.Ramp510, t2, hasFloor: true, floor: pbi.Field68);           // 0xA55108..0xA55148
        RunRamp(voice.Ramp350, t3, hasFloor: false, floor: 0f);                   // 0xA5514C..0xA55178
        RunRamp(voice.Ramp520, t4, hasFloor: true, floor: pbi.Field6C);           // 0xA5517C..0xA551BC
    }

    /// <summary>
    /// One ramp record (row 1.9, C43.1): the floor (<c>vmovle</c>), the clamp to 0 (<c>bmi</c>) and to 100.0f (<c>vmovgt</c>), then a changed target runs the band's setter (<see cref="WwiseVoiceFilterBand.SetTarget"/>: the flag, the target and
    /// <c>cur + ((oldTarget - cur) * 0.125f) * float(u16 steps)</c>) on the filter band record itself.
    /// </summary>
    // fidelity: M6-011, M6-022
    private static void RunRamp(WwiseVoiceFilterBand ramp, float target, bool hasFloor, float floor)
    {
        float clamped = target;
        if (hasFloor && (clamped <= floor || float.IsNaN(clamped) || float.IsNaN(floor))) clamped = floor;   // 0xA55118/0xA5518C vcmpe; vmovle (LE holds for an unordered compare: N != V)
        if (clamped < 0f) clamped = 0f;                                  // 0xA5511C..0xA55124 bmi -> 0xA554C4 (the literal 0)
        if (clamped > 100f) clamped = 100f;                              // 0xA55128..0xA55134 vmovgt (0x42C80000)
        ramp.SetTarget(clamped);                                         // vcmp.f32; bne to the setter only on a change (a NaN differs): 0xA55444 / 0xA55410 / 0xA553D8 / 0xA551C0
    }

    /// <summary>
    /// V7 <c>0xA554F0</c> (<c>E4 == 2</c> with <c>SRC10</c> clear, row 1.12): <c>0xA56650(src, [pbi+0x1DC], [pbi+0x1E0])</c>: 1 continues at <c>0xA55218</c> (r5 = 1); <c>0x3F</c> gives r5 = 0 and S2F = 0 with no stop; anything else stops the voice with r5 = 0 and S2F = 0.
    /// A source whose <c>vt+0x28</c> ran and does not write the PBI's format bytes needs the host's <see cref="WwiseLiveVoice.StartStreamFormatWriter"/> (as <c>0xA548C0</c>'s start does).
    /// </summary>
    private bool Path554F0(WwiseLiveVoice voice, WwisePlayingInstance pbi, IWwiseVoiceSource source, ref bool s2f)
    {
        int r = WwiseVoiceSourceStart.StartA56650(source, pbi.Read1DC(), pbi.Read1E0(), out bool ran);   // 0xA554F0..0xA554F8
        if (ran && source is not IWwiseStreamingVoiceSource { WritesSourceFormatInStartStream: true })
            (voice.StartStreamFormatWriter ?? throw new WwiseMissingBehaviourException(
                "M6-025 C26.5: the StartStream writers of pbi+0x158..0x162 (0xA72760..0xAB138C) are not built for this source's class; supply WwiseLiveVoice.StartStreamFormatWriter")).Invoke(pbi, source);
        if (r == 0x3F) { s2f = false; return false; }                // 0xA554FC..0xA55508 moveq r5,#0; strbeq r5,[sp,#0x2f]
        if (r == 1) return true;                                     // 0xA5550C..0xA55510 -> 0xA55218
        Stop48(voice);                                               // 0xA55514..0xA55520
        s2f = false;                                                 // 0xA55524..0xA5552C
        return false;
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
    /// V17 <c>0xA4F9E0(parent, S, child)</c> (pass 16 G10, C44.3): the bus-to-output-bus mix, called when the bus has an output bus at <c>+0x1C8</c>. It returns when <c>u16 [S+0xE] == 0</c>; sets the parent's mix state (<c>[parent+0x1BC] 4 -&gt; 1</c>,
    /// <c>[parent+0x68] = 0x2D</c>); zero-pads each channel of S from <c>u16 [S+0xE]</c> to <c>u16 [S+0xC]</c> and sets <c>u16 [S+0xE] = u16 [S+0xC]</c> (<c>0xA4FA68</c>); forwards to the parent's mixer object (<c>[[parent+0x1A8]+0xC]</c>, <c>vt+0x28</c>: none on shipped data) when it has one;
    /// returns without mixing when the child has no matrix descriptor (<c>[child+0x34] == 0</c>); else, when <c>[child+0xC0] &amp; 6</c> is set or the cfg words <c>[S+4]</c> and <c>[parent+0x64]</c> differ, runs <c>0xA45E9C(S, parent+0x60, {S[0x10], S[0x14]}, prev = [child+0x40], next = [child+0x3C], [parent+0x5C], u16 [parent+0x58])</c>;
    /// otherwise (identical cfgs) ramps each channel with <c>0xA46668(S row, parent row, start = S[0x10], inc = (S[0x14] - S[0x10]) * [parent+0x5C], u16 [parent+0x58])</c> (no matrix). Both mixing paths end with <c>u16 [parent+0x6E] = u16 [parent+0x58]</c>.
    /// <c>S[0x10]</c>/<c>S[0x14]</c> are the pair <c>0xA4FEF8</c> copied from <c>{[child+0x80], [child+0x84]}</c> after <c>0xA4D994</c>. The model's bus buffers are one mono <c>float[]</c> each: another channel count is a visible stop (the Master line's cfg is UNKNOWN).
    /// Which two cfg words the engine compares is read from the oracle's engine run, not stated by C44.3 (reported).
    /// </summary>
    // fidelity: M6-022, M6-012
    public void MixOutputBus(WwiseMixBus outputBus, float[] source, WwiseMixBus sourceBus)
    {
        ArgumentNullException.ThrowIfNull(outputBus);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceBus);

        // 0xA4F9E0: ldrh ip,[r1,#0xe]; cmp ip,#0; bxeq lr -- a source buffer with no valid frames returns before anything is touched.
        if (sourceBus.Frames == 0) return;

        if (OutputBusMix is { } hook) { hook(outputBus, source, sourceBus); return; }

        outputBus.MixStateA4F9EC();                                          // 0xA4F9EC..0xA4FA1C: [parent+0x1BC] == 4 -> 1; [parent+0x68] = 0x2D
        int channels = (int)(sourceBus.Format64 & 0xFFu);                    // 0xA4FA24 ldrb sb,[r1,#4]
        int max = sourceBus.MaxFrames, valid = sourceBus.Frames;             // u16 [S+0xC], u16 [S+0xE]
        if (valid < max && channels != 0)                                    // 0xA4FA20 beq 0xA4FA64; 0xA4FA28 cmp sb,#0
            for (int c = 0; c < channels; c++)
                Array.Clear(source, valid + c * max, max - valid);           // 0xA4FA3C..0xA4FA5C memset(S.data + (valid + c * max) * 4, 0, (max - valid) * 4)
        sourceBus.SetFramesA4FD74();                                         // 0xA4FA68 strh r7,[r6,#0xe]: u16 [S+0xE] = u16 [S+0xC]

        if (outputBus.OutputMixObject1A8 is { } obj && outputBus.MixObject1A8C is not null)   // 0xA4FA64..0xA4FAC0: [parent+0x1A8], [[+0x1A8]+0xC] -> vt+0x28
        {
            obj();
            return;
        }
        if (!sourceBus.MatrixDescriptor34.IsAllocated) return;               // 0xA4FAD0..0xA4FAD8 ldr r3,[r2,#0x34]; beq 0xA4FAC4

        if (channels != 1 || (int)(outputBus.Format64 & 0xFFu) != 1)
            throw new WwiseMissingBehaviourException($"M6-012 G10: the model's bus buffers are one mono float[] each; the mix of cfg 0x{sourceBus.Format64:X} into cfg 0x{outputBus.Format64:X} needs another layout (the Master line's cfg is UNKNOWN, C44.3 9.3)");
        if (source.Length < max || outputBus.Buffer.Length < outputBus.MaxFrames)
            throw new InvalidOperationException("the bus buffers are shorter than their frame counts");
        float g0 = sourceBus.OutGain10, g1 = sourceBus.OutGain14;            // S[0x10], S[0x14]
        bool matrixPath = (sourceBus.FlagsC0 & 6) != 0 || sourceBus.Format64 != outputBus.Format64;   // 0xA4FADC..0xA4FB18 (tst r3,#6; the two cfg words)
        int frames = outputBus.MaxFrames;                                    // u16 [parent+0x58]
        if (matrixPath)
        {
            int stride = WwiseChannelMatrix.Rows(outputBus.Format64);
            var d = sourceBus.MatrixDescriptor34;
            if ((sourceBus.Format64 & 0x8000u) != 0 || (outputBus.Format64 & 0x8000u) != 0)
                throw new WwiseMissingBehaviourException("M6-012 V2-06: the LFE branch of the matrix mixer (0xA45FD0..0xA46078) is not adopted");
            if (frames > source.Length) throw new InvalidOperationException("the child's buffer is shorter than the parent's frame count");
            var sources = new ReadOnlyMemory<float>[] { source.AsMemory(0, max) };
            WwiseMixKernels.MatrixMixA45E9C(d.PrevMatrix, d.NextMatrix, stride, sources, new[] { outputBus.Buffer }, channels, 1, g0, g1, outputBus.InvFrames5C, frames);   // 0xA4FB44 bl 0xA45E9C
        }
        else
        {
            float inc = (g1 - g0) * outputBus.InvFrames5C;                   // 0xA4FB9C vsub.f32 s15,s15,s13; 0xA4FBA0 vmul.f32 s15,s15,s14
            WwiseMixKernels.RampAccumulateA46668(source.AsSpan(0, frames), outputBus.Buffer, g0, inc, frames);   // 0xA4FBDC bl 0xA46668(S row, parent row, start, inc, frames)
        }
        outputBus.SetFramesA4FD74();                                         // 0xA4FB48 u16 [parent+0x6E] = u16 [parent+0x58]
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
        bus.GetResultingBuffer(_updateBuffer ?? (_ => { }));         // V18: 0xA4F754/0xA4FD84
        bus.GainStageA4D994();                                       // V18: 0xA4D994 after the FX loop (G1; the conditions of the four call sites are MISSING: once per call here)
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
    /// <c>0xA022E8(pbi, 1)</c> (row 1.16): with <c>[pbi+0x1BE]</c> bit 5 clear it returns; else it clears the bit, decrements <c>u16 [ptr+0x22]</c> for each of the <c>[pbi+0x1F0]</c> pointers of the array <c>[pbi+0x1EC]</c> and tail-calls <c>0xA370E4</c>, which is
    /// <c>[0x108DE78] -= 1</c> (<c>0xA370E4..0xA370F8</c>): the limiter's <see cref="WwisePlaybackLimiter.ReleaseVirtual0A022E8"/>, which owns that global (the REQUIRED <see cref="Limiter"/>).
    /// </summary>
    // fidelity: M6-022
    public void ReleaseVirtualA022E8(WwisePlayingInstance pbi)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        if ((pbi.Flags1BE & 0x20) == 0) return;                      // 0xA022E8: bit 5 clear
        (Limiter ?? throw new WwiseMissingBehaviourException(
            "M6-022 1.16: 0xA022E8 decrements the global at 0x108DE78 (0xA370E4), which the limiter owns; supply WwiseVoiceBusPass.Limiter")).ReleaseVirtual0A022E8(pbi);
    }

    /// <summary>
    /// <c>0xA0228C(pbi)</c> (row 1.16): with <c>[pbi+0x1BE]</c> bit 5 set it returns; else it sets the bit, increments <c>u16 [ptr+0x22]</c> for each pointer of the array <c>[pbi+0x1EC]</c> and the global at <c>0x1040144</c>'s target (the limiter's
    /// <see cref="WwisePlaybackLimiter.AcquireVirtual0A0228C"/>, which owns that global: the REQUIRED <see cref="Limiter"/>).
    /// </summary>
    // fidelity: M6-022
    public void AcquireVirtualA0228C(WwisePlayingInstance pbi)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        if ((pbi.Flags1BE & 0x20) != 0) return;                      // 0xA0228C: bit 5 already set
        (Limiter ?? throw new WwiseMissingBehaviourException(
            "M6-022 1.16: 0xA0228C increments the global at 0x1040144's target, which the limiter owns; supply WwiseVoiceBusPass.Limiter")).AcquireVirtual0A0228C(pbi);
    }

    /// <summary>
    /// V7-f <c>0xA54A30(voice)</c> (C41.4 rows 4.1..4.6, 2.10; the binary order <c>0xA54A30..0xA54F1C</c>): <c>pbi = [[voice+0xD4]+0xC]</c>; the resampler start (<c>0xA5321C</c>, a result other than 1 returns 2); then for each slot <c>i = 0..3</c> the FX descriptor
    /// (<c>0xA019B8</c> -> <c>0x9EEF2C</c>; none: next slot), the plug-in (<c>0x9CC2AC</c>; not 1: the descriptor is released, next slot) and its info (<c>0x9CC4D8</c>: <c>[info] == 3</c> and <c>[info+4]</c> 0x7E001 / 0x7E002; <c>byte [info+0xA] != 0</c>; for an in-place plug-in <c>byte [info+9] != 0</c>:
    /// each rejection runs the plug-in's <c>vt+8</c> (Term) and releases the descriptor, <c>0xA54D08</c>, then the next slot). By <c>byte [info+8]</c>: in place makes the 0x34-byte wrapper (<see cref="WwiseVoiceInsertFxSlot.Create"/>, Init <c>0xA792B0</c> then Reset; an Init failure leaves the
    /// slot null and goes on to the next slot, <c>0xA54B38</c>); a pool failure of the wrapper returns 2 from the WHOLE function (<c>0xA54BE8</c>/<c>0xA54DFC</c> -> <c>0xA54EC8</c> -> <c>0xA54A88</c>, C24.2); out of place is the <c>0x103DC38</c> class, not extracted (a required stop). The format
    /// <c>&amp;fmt</c> handed to the wrapper and the plug-in is <c>{[voice+0xEC], [pbi+0x15C], [pbi+0x160]}</c> (<c>0xA54AD0</c> overwrites the first word of the copy of <c>[pbi+0x158..]</c> with the mix rate). Then <c>[voice+0xF0] = [pbi+0x15C]</c> (<c>0xA54B60..0xA54B70</c>), filter A
    /// <c>0xA764D4(voice+0x1D0, [voice+0xF0], 0)</c> and filter B <c>0xA764D4(voice+0x3A0, [voice+0xF0], 0)</c> (each result other than 1 is returned, <c>0xA54B78..0xA54B80</c>, <c>0xA54D48..0xA54D4C</c>), <c>0xA5676C(voice+0x380, pbi)</c> (<c>[node+8] = pbi</c>, returns 1: the voice's
    /// <c>+0x388</c>), the chain links (<c>vt+0x24</c> from the voice+0x380 node down to the pitch node, <c>0xA54D3C..0xA54DB8</c>) and the voice's <c>vt+0x6C</c> (<c>0xA5335C</c>, <see cref="WwiseLiveVoice.VoiceStart6C"/> or the built <see cref="WwisePlayPath"/> body). Returns 1, or the first failing result.
    /// The node resolver <c>0x9EEF2C</c> (behind <c>0xA019B8</c>'s <c>[pbi+0xE9]</c> bit 2 gate), the registry <c>0x9CC2AC</c> and the 0x380 node's <c>vt+0x24</c> are not extracted: required seams.
    /// </summary>
    // fidelity: M6-022
    public static int StartStreamAndBuildInsertFx(WwiseLiveVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        var pbi = (voice.Source as IWwisePitchNodeSource)?.Owner ?? voice.BusOwner8 as WwisePlayingInstance ?? throw new WwiseMissingBehaviourException(
            "M6-022 V7-f: 0xA54A30 reads pbi = [[voice+0xD4]+0xC]; the current source has no owner PBI (BusOwner8 is the same PBI's context)");
        if (!(voice.StartResampler5321C ?? voice.StartResamplerA5321C)()) return 2;  // 0xA54A78 0xA5321C != 1 -> 0xA54A88 returns 2

        for (int i = 0; i < voice.InsertFxSlots.Length; i++)         // 0xA54ADC.. up to four slots
        {
            WwiseVoiceFxDescriptor? fx = null;
            if ((pbi.Flags0E9 & 4) == 0)                             // 0xA019B8: [pbi+0xE9] bit 2 set -> the out pointer is zeroed, the resolver is not called
                fx = (voice.ResolveNodeFx9EEF2C ?? throw new WwiseMissingBehaviourException(
                    "M6-022 4.1: node vt+0xE8 = 0x9EEF2C (reached through 0xA019B8) resolves slot i's FX descriptor and is not extracted; supply WwiseLiveVoice.ResolveNodeFx9EEF2C"))(i).Fx;   // 0xA54AF0 0xA019B8 -> 0x9EEF2C
            if (fx is null) continue;                                // 0xA54AF8 beq 0xA54B50
            var plugin = (voice.PluginRegistry9CC2AC ?? throw new WwiseMissingBehaviourException(
                "M6-022 4.1: 0x9CC2AC (the plug-in registry lookup and create) is not extracted; supply WwiseLiveVoice.PluginRegistry9CC2AC"))(fx.Id, voice.PluginAllocator);
            if (plugin is null) { fx.Release(); continue; }          // 0xA54B30..0xA54B38: not 1 -> release the descriptor, next slot
            plugin.GetPluginInfo(out var info);                      // 0x9CC2AC: plugin->vt+0x10(info)
            // 0x9CC4D8(id, 3, &info) != 0, byte [info+0xA] != 0: 0xA54D08 plug-in vt+8, release, next slot. byte [info+9] (0xA54DD0) cannot be reported through IWwiseEffectPlugin (WwisePluginInfo has no such field): it is the pre-initialised 0.
            if (info.Word0 != 3 || (info.Word4 != 0x7E001 && info.Word4 != 0x7E002) || info.ByteA != 0)
            {
                plugin.Term(voice.PluginAllocator);
                fx.Release();
                continue;
            }
            if (info.Byte8 == 0)                                     // 0xA54BB8: the 0x9C-byte 0x103DC38 class
                throw new WwiseMissingBehaviourException("M6-022 4.1: an out-of-place plug-in (byte [info+8] == 0) gets the 0x9C-byte class of vptr 0x103DC38 (vt+0x28 = 0xA79858), which is not extracted");
            var fmt = new WwiseEffectFormat(voice.MixRateEC, pbi.Word15C);   // 0xA54AD0 str r2,[sp,#0x34] ([voice+0xEC]); [sp+0x38] = [pbi+0x15C]
            var slot = WwiseVoiceInsertFxSlot.Create(voice, i, fx, plugin, fmt, out bool allocationFailed);
            if (allocationFailed) return 2;                          // 0xA54EC8..0xA54EFC, 0xA54A88: the whole function returns 2
            voice.InsertFxSlots[i] = slot!;                          // [voice+0x370+4i] = W; an Init failure leaves the slot null
        }

        voice.Word0xF0 = pbi.Word15C;                                // 0xA54B60 ldr r1,[sp,#0x38]; 0xA54B70 str r1,[r5,#0xf0]
        int rf = voice.FilterA.InitA764D4(voice.Word0xF0, 0, voice.FilterAllocationFails);   // 0xA54B74 0xA764D4(voice+0x1D0, [voice+0xF0], 0)
        if (rf != 1) return rf;                                      // 0xA54B78..0xA54B80
        rf = voice.FilterB.InitA764D4(voice.Word0xF0, 0, voice.FilterAllocationFails);       // 0xA54D44 0xA764D4(voice+0x3A0, [voice+0xF0], 0)
        if (rf != 1) return rf;                                      // 0xA54D48..0xA54D4C
        voice.Pbi388 = pbi;                                          // 0xA54D5C 0xA5676C(voice+0x380, pbi): str r1,[r0,#8]; returns 1
        // 0xA54D3C..0xA54DB8: arr = [src, pitch node, the filled slots compacted in slot order, the holder voice+0x1C0, the voice+0x380 node]; k = n+1 DOWN to 1: arr[k]->vt+0x24(arr[k-1]).
        var chain = new List<object> { voice.Source!, voice.PitchNode };
        foreach (var slot in voice.InsertFxSlots) if (slot is not null) chain.Add(slot);
        chain.Add(voice.HolderNode1C0);
        chain.Add(voice.GainNode380);
        voice.ChainLinks.Clear();
        for (int k = chain.Count - 1; k >= 1; k--)
        {
            object node = chain[k], upstream = chain[k - 1];
            voice.ChainLinks.Add((node, upstream));
            if (node is WwiseVoiceInsertFxSlot linked)
                linked.SetUpstreamA52678(upstream as IWwiseFxChainNode ?? voice.PitchChainNode);   // 0xA52678: [W+4] = upstream
            else if (ReferenceEquals(node, voice.GainNode380))
                (voice.GainNode380Vt24 ?? throw new WwiseMissingBehaviourException(
                    "M6-022 2.10: 0xA54D98..0xA54DAC calls vt+0x24 on the voice+0x380 node first, whose body is not extracted; supply WwiseLiveVoice.GainNode380Vt24"))(upstream);
            // the pitch node's ([node+4] = src, set when the source is assigned) and the holder's (derived: HolderUpstream1C4) vt+0x24 store nothing the model keeps separately
        }
        if (voice.VoiceStart6C is { } start) start();                // 0xA54DC4 voice->vt+0x6C
        else WwisePlayPath.RefreshVoiceVt6C(voice);                  // 0xA5335C (R2.12)
        return 1;
    }

    /// <summary>
    /// <c>0xA4BC58</c> (C12 voice-callees Q4/F1, C16 V7-g, C18 V7-n/V7-o/V7-p; C41.1 rows 1.5, 1.18 for the arguments): the per-connection gain/format update on the owner PBI. <c>param_2 = pbi+0xC</c> (so <c>[param_2+0x3C]/[param_2+0x40]</c> are <c>[pbi+0x48]/[pbi+0x4C]</c>, the
    /// propagated <c>[param_2+0xA8..0xB4]</c> / <c>[param_2+0xB8..0xC4]</c> are <c>[pbi+0xB4..0xC0]</c> / <c>[pbi+0xC4..0xD0]</c> and <c>[param_2+0xDC]</c> is <c>[pbi+0xE8]</c>). It computes the aggregate <c>sb</c>/<c>fp</c> flags over the connection list, runs the branch structure that
    /// produces the <c>[sp+0x2f]</c> byte (returned, and stored in <see cref="WwiseLiveVoice.P2F"/>) and the <c>[sp+0x2e]</c> byte (<see cref="WwiseLiveVoice.Run2E"/>), sets <c>[conn+0xC] = [voice+0x1C]*gain</c>, bit 2 of <c>[conn+0x6C]</c> (the first branch from <paramref name="arg5"/>,
    /// <c>src-&gt;vt+0x4C</c>), and keeps the four running minima of <c>[conn+0x50/+0x54/+0x58/+0x5C]</c> in <paramref name="floatOutputs"/>. <c>voice-&gt;vt+0x3C</c> is <c>0xA55E90</c>: <c>([pbi+0x1BE] &amp; 0x14) != 0</c> (gapE 3.3).
    /// </summary>
    /// <param name="voice">The voice (<c>param_1</c>).</param>
    /// <param name="pbi">The owner PBI <c>[source+0xC]</c>; <c>param_2</c> is <c>pbi+0xC</c>.</param>
    /// <param name="gain">The <c>param_4</c> gain.</param>
    /// <param name="arg5">The <c>param_5</c> byte: <c>src-&gt;vt+0x4C</c> (<see cref="SourceVt4C"/>).</param>
    /// <param name="arg12">The <c>param_12</c> pointer: <c>{[pbi+0x140], word}</c> or none; <c>0xA5D70C</c> is its reader (<c>0xA4BF38</c>, X2); a non-null value is a required stop (dead on Cozmo, C44.3).</param>
    /// <param name="floatOutputs">
    /// The <c>param_8..11</c> destination, in order. The first call passes <see cref="WwiseLiveVoice.OutputMin50"/> (the caller's <c>sp+0x30..0x3c</c>); the second call at <c>0xA5572C</c> passes a scratch because its outputs go to <c>sp+0x40</c> and are never read (C18 V7-q).
    /// </param>
    /// <returns>The <c>P2F</c> byte the state machine reads at <c>0xA55248</c>/<c>0xA5526C</c>.</returns>
    /// <remarks>
    /// The exact engine body (pass 16 + the pass-13 truth table, C44.3): the prelude (<c>sb</c>/<c>fp</c>/<c>r5</c>), the three S2E/S2F outcomes, the per-connection loop <see cref="ConnectionLoopA4BD74"/> (B1..B15) with <c>0xA5975C</c> (<see cref="ConnectionMatrixA5975C"/>) and
    /// <c>0xA25FF8</c> (<see cref="WwiseChannelMatrix"/>); a non-null <paramref name="arg12"/> (<c>0xA5D70C</c>, dead on Cozmo) and the 3D branch are required stops. The four output floats <c>[sp+0x5c..0x68]</c> carry the running minima of the connection fields <c>+0x50/+0x54/+0x58/+0x5C</c> (100.0f start, only when the loop is entered).
    /// </remarks>
    // fidelity: M6-022
    public static bool UpdateConnectionGains(WwiseLiveVoice voice, WwisePlayingInstance pbi, float gain, byte arg5, WwiseGainArg12? arg12 = null, float[]? floatOutputs = null)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(pbi);
        float[] minima = floatOutputs ?? voice.OutputMin50;

        // 0xA4BC90..0xA4BCAC: the four float outputs are zeroed at entry. They become 100.0f only at 0xA4BD84 (the loop's entry); a call that does not reach it leaves them 0 (pass-13 verification item 2).
        for (int i = 0; i < minima.Length; i++) minima[i] = 0f;

        // 0xA4BCB4..0xA4BCF0 (pass-16 verification item 1): sb = the AND of bit 2 over the connections (the value of the last iteration while it still holds); r5 is a latch set once and cleared at the first connection whose bit 1 is clear; fp is
        // reset to 1 at every iteration head and is 0 only when the latch is still set after a connection with bit 1 set. With no connection (0xA4C198): fp = 0, r5 = sb = 1.
        bool sb = true, r5 = true, fp = false;
        foreach (var c in voice.Connections)
        {
            fp = true;                                                   // 0xA4BCBC mov fp,#1
            if (sb) sb = (c.Flags6C & 0x04) != 0;                        // 0xA4BCB8 cmp sb,#0; 0xA4BCC0 ldrbne sb,[r0,#0x6c]; 0xA4BCC4 ubfxne sb,sb,#2,#1
            if (r5)                                                      // 0xA4BCC8 cmp r5,#0; beq 0xA4BCE8
            {
                if ((c.Flags6C & 0x02) == 0) { r5 = false; fp = true; }  // 0xA4BCDC moveq r5,#0; 0xA4BCE0 moveq fp,#1
                else fp = false;                                         // 0xA4BCE4 movne fp,#0
            }
        }

        int vt3c = (pbi.Flags1BE & 0x14) != 0 ? 1 : 0;                   // 0xA4BCF4..0xA4BD00 blx [voice vt+0x3C] = 0xA55E90: ([[[voice+0xD4]+0xC]+0x1BE] & 0x14) != 0 (pass 13: 0 for a fresh, unpaused, unstopped PBI)
        bool cd8 = (voice.FlagsCD & 8) != 0;                             // 0xA4BD04 ldrb r3,[sl,#0xcd] (bit 3: clear at the first update, set at 0xA55294)

        // The S2E/S2F truth table (pass-13 verification), 0xA4BD08..0xA4C080. s2f is the byte stored through the outC pointer ([sp+0x58]); the S2E byte stored at 0xA4BFEC is r5 (see the end).
        bool s2f;
        bool loop;                                                       // reaches 0xA4BD74 (the min-init, the per-connection loop and the B14 tail)
        if (vt3c == 0 && !cd8)
        {
            s2f = fp;                                                    // 0xA4C03C strb fp,[r4]
            if (!fp) { SetBit2(voice, 1); loop = false; }                // 0xA4C04C beq 0xA4BD40: bit 2 := 1 on every connection (0xA4BD4C movne r0,#1), then 0xA4BD6C fp == 0 -> 0xA4BFEC
            else { SetBit2(voice, arg5); loop = true; }                  // 0xA4C054 b 0xA4C024: bit 2 := argA byte (r8), then 0xA4BD6C fp != 0 -> 0xA4BD74
        }
        else if (vt3c == 0)                                              // bit 3 set
        {
            if (r5) { if (!sb) { s2f = true; loop = true; } else { s2f = false; loop = false; r5 = true; } }   // 0xA4C00C bne 0xA4BD1C: sb == 0 -> 0xA4C010 (S2F = 1, then 0xA4BD74); else 0xA4BD28 (S2F = 0) -> 0xA4C080 (r5 = 1, exit)
            else { s2f = true; loop = true; }                            // 0xA4C010: S2F = 1, bit 3 set -> 0xA4BD74
        }
        else if (!cd8)                                                   // vt3c != 0, bit 3 clear
        {
            s2f = false;                                                 // 0xA4C058 strb r3(0),[r4]
            r5 = true;                                                   // 0xA4C070 mov r5,#1
            SetBit2(voice, vt3c);                                        // 0xA4BD54 bfi r3,r0,#2,#1 with r0 = the vt+0x3C result; fp = bit 3 of cd = 0 (0xA4C05C..0xA4C064), so 0xA4BD6C exits
            loop = false;
        }
        else                                                             // vt3c != 0, bit 3 set
        {
            if (!sb) { r5 = true; s2f = true; loop = true; }             // 0xA4BD1C cmp sb,#0; 0xA4BD20 moveq r5,#1; 0xA4BD24 beq 0xA4C010
            else { s2f = false; loop = false; r5 = true; }               // 0xA4BD28 strb 0 -> 0xA4BD38 bne 0xA4C080: r5 = 1
        }

        // 0xA4BD74: the per-connection loop and the B14 tail (only on the branches that reach it).
        if (loop) ConnectionLoopA4BD74(voice, pbi, gain, minima, arg12);

        // 0xA4BFEC/0xA4BFF0: the S2E byte is r5 (0 iff the list is non-empty, some connection has bit 1 clear and vt+0x3C returned 0); every vt3c != 0 path leaves r5 = 1 (0xA4BD20, 0xA4C070, 0xA4C080).
        voice.Run2E = r5;
        voice.P2F = s2f;
        return s2f;
    }

    /// <summary>0xA4BD54: set bit2 of every connection's <c>[conn+0x6C]</c> to the low bit of <paramref name="bit"/> (<c>bfi r3,r0,#2,#1</c>).</summary>
    private static void SetBit2(WwiseLiveVoice voice, int bit)
    {
        foreach (var c in voice.Connections)
            c.Flags6C = (byte)((c.Flags6C & ~0x04) | ((bit & 1) << 2));
    }

    private static readonly float Float100 = BitConverter.Int32BitsToSingle(0x42C80000);   // 0xA4BD8C movt r3,#0x42c8: the minima start at 100.0f (NOT 101.0f)

    /// <summary>The running minimum of the four outputs (0xA4BEC8..0xA4BF34): <c>vcmpe out,conn; vmovpl out,conn</c> replaces the output unless <c>out &lt; conn</c> (an unordered compare replaces it, a NaN included).</summary>
    private static float MinStep(float output, float conn) => output < conn ? output : conn;

    /// <summary>
    /// <c>0xA4BD74..0xA4BFE8</c> (pass 16 B1..B15): the per-connection state machine and the tail. The gate: a zero low byte of <c>[voice+0xF0]</c> (inCh) skips the loop AND the P copy (<c>0xA4BD74 beq 0xA4BFD4</c>); the outputs stay 0.
    /// Per connection: the descriptor re-init on an inCh change (B2..B4), the UNPREDICATED <c>[conn+8] = [conn+0xC]</c> and <c>[conn+0x10] = [conn+0x14]</c> at the START (B5, <c>0xA4BE4C</c>, <c>0xA4BE64</c>) after the guarded
    /// <c>[conn+0x64] = inCh</c> and the next/prev swap, the bit-1 branch (B6), the gain store, the 3D/2D split (B8/B9), the running minima (B10), the dead <c>0xA5D70C</c> (B11) and the bit-2/ramp/flat endings (B11/B12).
    /// </summary>
    // fidelity: M6-022, M6-012
    private static void ConnectionLoopA4BD74(WwiseLiveVoice voice, WwisePlayingInstance pbi, float gain, float[] minima, WwiseGainArg12? arg12)
    {
        uint id = voice.Word0xF0;                                        // 0xA4BC88 str r2,[sp,#0x10]: the whole word is the A25FF8 / A5975C id argument
        int inCh = (int)(id & 0xFFu);                                    // 0xA4BC74 uxtb r6,r2
        if (inCh == 0)                                                   // 0xA4BD74 cmp r6,#0; 0xA4BD7C beq 0xA4BFD4
        {
            TailA4BFD4(voice, pbi);                                      // no P[0xA8..] copy on this path
            return;
        }
        float voiceGain = voice.OutputGain;                              // 0xA4BD78 vldr s17,[sl,#0x1c]
        for (int i = 0; i < 4; i++) minima[i] = Float100;                // 0xA4BD84..0xA4BDAC
        bool cd8 = (voice.FlagsCD & 8) != 0;                             // 0xA4BF5C ldr r3,[sp,#0xc]; ldrb r3,[r3,#0xcd]: re-read per connection (nothing in the loop changes it)

        foreach (var c in voice.Connections)                             // 0xA4BE20 .. 0xA4BE14 ldr fp,[fp,#0x28]
        {
            int outCh = (int)(c.Bus.Format64 & 0xFFu);                   // 0xA4BE24..0xA4BE2C ldr r3,[fp,#0x30]; ldrb r5,[r3,#0x64]
            if (c.C64 != inCh)                                           // 0xA4BE20..0xA4BE30 ldr r2,[fp,#0x64]; cmp r2,r6; bne 0xA4BDDC
            {
                c.Descriptor.Free();                                     // 0xA4BDEC bl 0xA67C58 (conn+0x18)
                c.C0C = 0f;                                              // 0xA4BDF4 str sb,[fp,#0xc]
                c.C64 = 0;                                               // 0xA4BDFC str r8,[fp,#0x64]
                c.C14 = 0f;                                              // 0xA4BE04 str sb,[fp,#0x14]
                if (c.Descriptor.Reserve(inCh, outCh) != 1) continue;    // 0xA4BE08 bl 0xA67B9C; 0xA4BE0C cmp r0,#1; 0xA4BE10 beq 0xA4C16C; else the next connection (0xA4BE14)
                // 0xA4C16C..0xA4C190: zero-fill [conn+0x20] (half A) with ((outCh + 3) >> 2) * inCh * 4 floats; half B stays as the pool left it.
                int zero = ((outCh + 3) >> 2) * inCh * 4;
                if (zero != 0) c.Descriptor.NextMatrix[..zero].Clear();
            }

            // 0xA4BE34..0xA4BE68 (B5).
            bool alloc = c.HasDry;                                       // 0xA4BE34 ldr r3,[fp,#0x18]; cmp r3,#0
            float oldNextGain = c.C0C;                                   // 0xA4BE38 ldr r1,[fp,#0xc]
            if (alloc)
            {
                c.C64 = inCh;                                            // 0xA4BE40 strne r6,[fp,#0x64]
                c.Descriptor.SwapPointers();                             // 0xA4BE44..0xA4BE58: [conn+0x20] <-> [conn+0x24]
            }
            c.C08 = oldNextGain;                                         // 0xA4BE4C str r1,[fp,#8]: unpredicated
            c.C10 = c.C14;                                               // 0xA4BE5C ldr r2,[fp,#0x14]; 0xA4BE64 str r2,[fp,#0x10]: unpredicated
            int copy = ((outCh + 3) >> 2) * c.C64 * 4;                   // the memcpy length in floats: ((u8[line+0x64] + 3) >> 2) * [conn+0x64] * 4 (0xA4BF6C..0xA4BF9C, 0xA4C08C..0xA4C0B8, 0xA4C0D8..0xA4C130)

            bool bit1 = (c.Flags6C & 0x02) != 0;                         // 0xA4BE54 ldrb r3,[fp,#0x6c]; 0xA4BE60 tst r3,#2; 0xA4BE68 bne 0xA4C088
            bool runMinima = true;
            if (bit1)
            {
                c.Descriptor.CopyPrevToNext(copy);                       // 0xA4C088..0xA4C0B8: memcpy([conn+0x20] <- [conn+0x24]); 0xA5975C is NOT called
                c.C0C = 0f;                                              // 0xA4C0C4 str sb,[fp,#0xc]
                runMinima = (c.Flags6C & 0x04) == 0 && cd8;              // 0xA4C0C8 beq 0xA4C10C / 0xA4C10C..0xA4C118 bne 0xA4BEC8 (the minima step with the stale conn+0x50..0x5C); a set bit 2 goes to 0xA4C0CC / 0xA4C0D8 with no minima step
            }
            else
            {
                c.C0C = voiceGain * gain;                                // 0xA4BE6C vmul.f32 s15,s17,s16; 0xA4BE70 vstr s15,[fp,#0xc]
                if ((pbi.Flags0E8 & 0x03) != 0)                          // 0xA4BE74 ldrb r3,[r7,#0xdc]; 0xA4BE78 tst r3,#3; 0xA4BE7C bne 0xA4C138
                    throw new WwiseMissingBehaviourException("M6-012 B8: the 3D positioning branch of 0xA4BC58 ([P+0xDC] & 3 != 0) calls 0xA5B9D0 and 0xA5993C (0xA4C138..0xA4C164), which C44.3 does not adopt (RECOVERABLE_GAP); the shipped chain is 2D (pass-16 F6)");
                c.C50 = pbi.Lpf48;                                       // 0xA4BE80 ldr r2,[r7,#0x3c]; 0xA4BE94 str r2,[fp,#0x50]
                c.C58 = pbi.Hpf4C;                                       // 0xA4BE88 ldr r3,[r7,#0x40]; 0xA4BE9C str r3,[fp,#0x58]
                c.C54 = 0f;                                              // 0xA4BE90 str sb,[fp,#0x54]
                c.C5C = 0f;                                              // 0xA4BEA0 str sb,[fp,#0x5c]
                int flag = (pbi.Flags0E8 & 0x10) != 0 ? 1 : ((voice.FlagsCD >> 2) & 1);   // 0xA4BEA4..0xA4BEC0: ([P+0xDC] & 0x10) ? 1 : ((voice[0xCD] >> 2) & 1)
                ConnectionMatrixA5975C(pbi, voice, id, flag, c);         // 0xA4BEC4 bl 0xA5975C(P, &voice+0x10, id, flag, conn)
            }

            if (runMinima)
            {
                minima[0] = MinStep(minima[0], c.C50);                   // 0xA4BEC8..0xA4BF34
                minima[1] = MinStep(minima[1], c.C54);
                minima[2] = MinStep(minima[2], c.C58);
                minima[3] = MinStep(minima[3], c.C5C);
            }

            if (arg12 is not null)                                       // 0xA4BF38..0xA4BF4C: 0xA5D70C(param_12, conn) only for a non-null param_12
                throw new WwiseMissingBehaviourException("M6-012 B11: param_12 is non-null, so 0xA4BF4C calls 0xA5D70C(param_12, conn) (-> 0xA03DA0, the callback of type 0x10, 'speaker volume matrix'); C44.3: the only PostEvent site passes the flags {0,1,5,9,13}, so [pbi+4] & 0x10 is never set (other writers of [pbi+4] not enumerated)");

            if ((c.Flags6C & 0x04) != 0)                                 // 0xA4BF50 ldrb r3,[fp,#0x6c]; tst r3,#4; bne 0xA4C0D8
            {
                c.Descriptor.CopyNextToPrev(copy);                       // 0xA4C0D8..0xA4C130: memcpy([conn+0x24] <- [conn+0x20])
                c.C08 = 0f;                                              // 0xA4C104 str sb,[fp,#8]: [conn+0x10] is left as set at the start
            }
            else if (!cd8)                                               // 0xA4BF5C..0xA4BF68: voice[0xCD] bit 3 set is the ramping case (no copy, no gain change)
            {
                c.Descriptor.CopyNextToPrev(copy);                       // 0xA4BF6C..0xA4BF9C: the flat case, memcpy([conn+0x24] <- [conn+0x20])
                c.C08 = c.C0C;                                           // 0xA4BFA0 ldr r2,[fp,#0xc]; 0xA4BFA8 str r2,[fp,#8]
                c.C10 = c.C14;                                           // 0xA4BFA4 ldr r3,[fp,#0x14]; 0xA4BFAC str r3,[fp,#0x10]
            }
        }

        pbi.PropagateParamsA4BFC4();                                     // 0xA4BFC4..0xA4BFD0 ldm [P+0xA8..0xB4]; stm [P+0xB8..0xC4]
        TailA4BFD4(voice, pbi);
    }

    /// <summary>0xA4BFD4..0xA4BFE8 (B14): <c>voice[0xCD]</c> bit 2 and <c>[P+0xDC]</c> bit 4 cleared.</summary>
    private static void TailA4BFD4(WwiseLiveVoice voice, WwisePlayingInstance pbi)
    {
        voice.FlagsCD = (byte)(voice.FlagsCD & ~0x04);                   // 0xA4BFD4..0xA4BFDC
        pbi.Flags0E8 = (byte)(pbi.Flags0E8 & ~0x10);                     // 0xA4BFE0..0xA4BFE8
    }

    /// <summary>
    /// <c>0xA5975C(P, &amp;voice+0x10, id, flag, conn)</c> (pass 16 F1..F6, C44.3): the per-connection send gain and the pan matrix. F1 (the device-record scan of <c>[0x108DAFC + 8]</c> for <c>[conn+0x48/+0x4C]</c>) only finds the record
    /// <c>0xA25FF8</c> passes to the ambisonic tail <c>0xA234FC</c> (a required stop, <see cref="WwiseChannelMatrix"/>), so it is not run. F2: <c>[conn+0x14] = ([conn+0x68] != 0) ? 1.0f : [[voice+0x10]+0x34]</c> (entry 0's dry gain).
    /// F3: with bit 0 of <c>[conn+0x6C]</c> clear (never, C4) only the copy; otherwise the pan fields <c>P[0xA8]/0xAC/0xB0</c> are compared (float compares: a NaN differs, -0 equals 0) with the previous frame's <c>P[0xB8]/0xBC/0xC0</c>,
    /// then the bytes <c>P[0xB4]</c>/<c>P[0xC4]</c>; any difference, or <paramref name="flag"/> != 0, or bit 2 of <c>[conn+0x6C]</c>, recomputes (F4) with the byte <c>P[0xB4]</c> as the A25FF8 flag; otherwise F5 copies the previous matrix to the next one.
    /// </summary>
    // fidelity: M6-012, M6-022
    private static void ConnectionMatrixA5975C(WwisePlayingInstance p, WwiseLiveVoice voice, uint id, int flag, WwiseVoiceConnection c)
    {
        // 0xA597A4..0xA597BC: ldr lr,[ip,#0x68]; cmp lr,#0; ldreq r4,[r8,#0x34]; movne r4,#0x3f800000; str r4,[ip,#0x14] (the table is dereferenced only for a dry connection)
        c.C14 = c.Arg68 != 0 ? 1.0f
            : (voice.SendTable?.Entries is { Count: > 0 } entries ? entries[0].SendGain
                : throw new WwiseMissingBehaviourException("M6-012 F2: [voice+0x10] has no entry 0; 0xA5975C reads [[voice+0x10]+0x34] for a dry connection (0xA59768, 0xA597B0)"));
        int outCh = (int)(c.Bus.Format64 & 0xFFu);
        int floats = ((outCh + 3) >> 2) * c.C64 * 4;                     // F5's memcpy length: ((u8[line+0x64] + 3) >> 2) * [conn+0x64] * 16 bytes

        byte flagByte;
        if ((c.Flags6C & 1) == 0) { c.Descriptor.CopyPrevToNext(floats); return; }   // 0xA597B8 tst lr,#1; beq 0xA59878 (never: bit 0 is never cleared, C4)
        if (p.PanB4 != p.FieldC4)                                      // 0xA597C4..0xA597D4: P[0xA8] vs P[0xB8]
            flagByte = p.PanC0;
        else if (p.PanB8 != p.FieldC8)                                 // 0xA598B4..0xA598C4: P[0xAC] vs P[0xBC]
            flagByte = p.PanC0;
        else if (p.PanBC != p.FieldCC)                                 // 0xA598C8..0xA598D8: P[0xB0] vs P[0xC0]
            flagByte = p.PanC0;
        else if (p.PanC0 != p.FieldD0)                                   // 0xA598DC..0xA598EC: ldrb P[0xB4], ldrb P[0xC4]; cmp r4,r5; movne r3,r5
            flagByte = p.PanC0;
        else if (flag != 0 || (c.Flags6C & 0x04) != 0)                   // 0xA598F0..0xA59904: cmp r3,#0 (the flag argument); tst lr,#4 -> r3 = P[0xC4]
            flagByte = p.FieldD0;
        else { c.Descriptor.CopyPrevToNext(floats); return; }            // 0xA598F8 beq 0xA59878: F5, new = prev

        float p1 = WwiseChannelMatrix.PanClamp((p.PanB4 + WwiseChannelMatrix.PanBias) * WwiseChannelMatrix.PanScale);             // 0xA597EC vadd.f32 s14,s14,s11; 0xA597F0 vmul.f32 s14,s14,s12 (+ the clamp)
        float p2 = WwiseChannelMatrix.PanClamp((p.PanB8 + WwiseChannelMatrix.PanBias) * WwiseChannelMatrix.PanScale);             // 0xA59818, 0xA5981C
        float p3 = p.PanBC / WwiseChannelMatrix.PanBias;                                    // 0xA59848 vdiv.f32 s13,s13,s12
        WwiseChannelMatrix.A25FF8(p1, p2, p3, flagByte, id, c.Bus.Format64, c.Descriptor.NextMatrix);   // 0xA5986C bl 0xA25FF8 ([sp] = id, [sp+4] = [[conn+0x30]+0x64], [sp+8] = [conn+0x20], [sp+0xC] = the device record)
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