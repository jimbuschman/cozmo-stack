// fidelity: M6-014
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// RobotAudioBuffer::UpdateBuffer 0x005985FC (M6-014, gapD D2.9, M6 A17): the consumer the Hijack's process
/// callback delivers each flushed chunk to. The native has <b>no zero-length check</b>: an <c>n = 0</c> chunk
/// pushes an empty frame exactly like a full one, so this delegate is called for it too.
///
/// A chunk is the samples the effect flushed, not a copy the bus makes; on the shipped chain those are the
/// Hijack's 744-sample robot chunks, and the partial one at the tail may be shorter or empty (M6-015).
/// </summary>
public delegate void WwiseUpdateBuffer(ReadOnlyMemory<float> chunk);

/// <summary>
/// One insert-FX slot of a mix bus, as the bus lifetime needs it (M6-014, gapD D2.2/D2.4/D2.5/D2.6). The bus
/// creates the effect lazily at its first <see cref="WwiseMixBus.GetResultingBuffer"/>, Initialises and
/// Resets it, executes it in slot order while the bus state is 1, and Terminates it at teardown. What the
/// effect does to the samples is the effect's own business (M6-013, M6-015); the bus only needs these facts.
///
/// The native slot is <c>{ u8 slot, u32 fxID, u8 isShareSet, u8 }</c> (gapB P6) and its effect is created by
/// <c>0x9CC2AC</c>. An unregistered plug-in returns nothing, so InsertFx drops the slot (D2.3); a
/// <see cref="WwiseBusFxSlot"/> whose factory returns null models exactly that.
/// </summary>
public interface IWwiseBusInsertFx
{
    /// <summary>
    /// D2.2/D2.4: an in-place effect writes the bus buffer itself (<c>vt+0x20</c> on bus buffer <c>+0x60</c>)
    /// and creates no out buffer; an out-of-place effect writes its own <c>slot+0x138</c> buffer. Only an
    /// out-of-place slot overrides the bus state in <see cref="WwiseMixBus.ReleaseBuffer"/> (D2.5). The
    /// shipped Hijack is in-place and synchronous (M6 A13).
    /// </summary>
    bool IsInPlace { get; }

    /// <summary>
    /// D2.5: the slot's own state after <see cref="Execute"/>. Each non-bypassed out-of-place slot overrides
    /// the bus state with it, the last such slot winning. The Hijack writes only uValidFrames and never
    /// eState (D2.8), so on the shipped chain this is never read.
    /// </summary>
    int State { get; }

    /// <summary>D2.2: <c>Init vt+0x1C</c>, once, at the bus's first GetResultingBuffer (Hijack Init → PrepareAudioBuffer).</summary>
    void Init();

    /// <summary>D2.2 (<c>Reset vt+0xC</c> after Init) and D2.4 (a newly bypassed slot is Reset once).</summary>
    void Reset();

    /// <summary>
    /// D2.4: <c>Execute vt+0x20</c> for one bus frame while the bus state is 1. <paramref name="validFrames"/>
    /// is the bus buffer's frame count (<c>+0x6E</c>): the full frame after mixing, 0 on the empty-input tail
    /// frame (D2.8). Every chunk it flushes, including an empty one, goes to <paramref name="updateBuffer"/>
    /// (D2.9).
    /// </summary>
    void Execute(int validFrames, WwiseUpdateBuffer updateBuffer);

    /// <summary>D2.6: <c>Term vt+8</c> at bus destruction (the Hijack's destroy callback → CloseAudioBuffer).</summary>
    void Term();
}

/// <summary>
/// One slot of a bus's FX chain (gapB P6): a factory that creates the effect lazily (D2.2, <c>0x9CC2AC</c>),
/// and the slot's own bypass bit (D2.4, <c>slot b0</c>). A factory returning null means the plug-in is not
/// registered, so the slot is dropped (D2.3) and never executed.
/// </summary>
public sealed record WwiseBusFxSlot(Func<IWwiseBusInsertFx?> Create, bool Bypassed = false);

/// <summary>
/// What GetOrCreateMixBus matches a mix bus on (D2.1): the node context's <c>+0x4C</c> and <c>+0x50</c> and
/// the device key at <c>+0x28/+0x2C</c>. The meanings of those four native fields are not in the rows, so the
/// key is an opaque caller input; a bus only compares it for equality.
/// </summary>
public readonly record struct WwiseMixBusKey(int Ctx4C, int Ctx50, int DeviceKey28, int DeviceKey2C);

/// <summary>
/// One mix bus's activity lifetime (M6-014, gapD D2.1..D2.9). The original creates a bus on demand when a
/// voice (or a child bus) mixes into it, instantiates its FX lazily in the same bus pass that first executes
/// them, and destroys it after a frame in which nothing mixed, nothing is connected and the bus was not
/// reused. That makes the lifetime <b>per contiguous voice activity</b> on the bus — one shared instance
/// across overlapping events, and a new instance (a new Hijack stream) after an idle frame.
///
/// <para><b>What the caller owns.</b> The row does not settle what a bus's identity fields mean, what the FX
/// chain contains, how many frames a frame has, or what a flushed chunk holds, so none of those is invented
/// here: the key is an opaque input (D2.1), the FX are caller-supplied slots (D2.2/D2.4, M6-013/M6-015), the
/// frame size is a constructor input (M6-018), and the chunk sink is passed to
/// <see cref="GetResultingBuffer"/> (D2.9). The voice activity is the caller calling <see cref="MixInput"/>
/// on the destination bus during the voice pass (D2.7), and the caller drives the per-frame order
/// (voice pass, then <see cref="GetResultingBuffer"/> + <see cref="ReleaseBuffer"/> per bus, then
/// <see cref="WwiseMixBusHierarchy.RemoveIdle"/>).</para>
///
/// <para><b>Not wired.</b> This is a standalone model; it is not yet the production bus/effect lifetime
/// (M6-013/M6-015/M6-017).</para>
/// </summary>
public sealed class WwiseMixBus
{
    /// <summary>D2.4/D2.6: the only state in which the FX run and in which the bus survives a frame.</summary>
    public const int StateActive = 1;

    /// <summary>D2.1: a node whose state is 2 is not reused by GetOrCreateMixBus.</summary>
    public const int StateNotReusable = 2;

    /// <summary>D2.1 (<c>Init 0xA4F0EC</c>) and D2.5 (ReleaseBuffer with no mixing this frame).</summary>
    public const int StateIdle = 4;

    /// <summary>D2.5: mixing input sets eState (<c>+0x68</c>) to 0x2D.</summary>
    public const int EStateMixed = 0x2D;

    /// <summary>D2.5: ReleaseBuffer sets eState to 0x11, which makes the next ReleaseBuffer with no mixing go idle.</summary>
    public const int EStateReleased = 0x11;

    private readonly IReadOnlyList<WwiseBusFxSlot> _slots;
    private readonly IWwiseBusInsertFx?[] _fx;
    private readonly bool[] _bypassLatch;
    private bool _fxInstantiated;
    private int _state = StateIdle;
    private int _eState = EStateReleased;
    private int _connections;
    private bool _touched;

    /// <param name="key">The reuse key (D2.1); opaque to this class.</param>
    /// <param name="slots">The FX chain in slot order (gapB P6, M6-013). May be empty.</param>
    /// <param name="maxFrames">The bus frame size (M6-018, <c>0x400</c>); also the bus buffer's length.</param>
    public WwiseMixBus(WwiseMixBusKey key, IReadOnlyList<WwiseBusFxSlot> slots,
                       int maxFrames = WwiseRuntimeSettings.SamplesPerFrame)
    {
        if (maxFrames <= 0) throw new ArgumentOutOfRangeException(nameof(maxFrames));
        ArgumentNullException.ThrowIfNull(slots);
        Key = key;
        _slots = slots;
        _fx = new IWwiseBusInsertFx?[slots.Count];
        _bypassLatch = new bool[slots.Count];
        MaxFrames = maxFrames;
        Buffer = new float[maxFrames];
    }

    /// <summary>The reuse key (D2.1).</summary>
    public WwiseMixBusKey Key { get; }

    /// <summary>The bus frame size (<c>+0x58</c>; the <c>CreateMixBus</c> frames argument, M6-018).</summary>
    public int MaxFrames { get; }

    /// <summary>The bus accumulation buffer (<c>bus buffer +0x60</c>), zeroed by every ReleaseBuffer (D2.5).</summary>
    public float[] Buffer { get; }

    /// <summary>The bus this one connects to as a child, or null (D2.1).</summary>
    public WwiseMixBus? Parent { get; private set; }

    /// <summary>
    /// V17 <c>[bus+0x1C8]</c> (C8): the output bus this bus's result mixes into. When non-null the bus pass
    /// calls <c>0xA4F9E0(outputBus, out, bus)</c>; when null it takes the device path <c>0x9E9E78</c>.
    /// </summary>
    public WwiseMixBus? OutputBus { get; set; }

    /// <summary>C7 <c>0xA4F9E0</c>: the source bus's start gain <c>[voice+0x3C]</c> (caller input).</summary>
    public float Gain3C { get; set; } = 1f;

    /// <summary>C7 <c>0xA4F9E0</c>: the source bus's end gain <c>[voice+0x40]</c> (caller input).</summary>
    public float Gain40 { get; set; } = 1f;

    /// <summary>C7 <c>0xA4F9E0</c>: <c>[bus+0x1A8]</c>'s object; when its <c>vt+0x28</c> exists it receives the
    /// mix instead of the <c>0xA45E9C</c> kernel (M6-012). Caller seam.</summary>
    public Action? OutputMixObject1A8 { get; set; }

    /// <summary>
    /// C7 <c>0xA4F9E0</c> / <c>0xA45E9C</c>: mix <paramref name="source"/> into this bus. It runs
    /// <see cref="MixInput"/>, zero-pads the source past <paramref name="validFrames"/>, then accumulates a
    /// linear ramp from <paramref name="startGain"/> to <paramref name="endGain"/> over the bus frame. The
    /// <c>0xA45E9C</c> kernel is M6-012's; the shipped robot bus is mono (M6-013 gapF 3.1).
    /// </summary>
    public void MixBuffer(float[] source, int validFrames, float startGain, float endGain)
    {
        ArgumentNullException.ThrowIfNull(source);
        MixInput();
        for (int k = 0; k < MaxFrames; k++)
        {
            float s = k < validFrames && k < source.Length ? source[k] : 0f;
            float g = startGain + (endGain - startGain) * ((float)k / MaxFrames);
            Buffer[k] += s * g;
        }
    }

    /// <summary>The bus state (<c>+0x1BC</c>): 4 at creation, 1 while input is mixed (D2.1/D2.5).</summary>
    public int State
    {
        get => _state;
        internal set => _state = value;                       // the oracle tests place a line in a given +0x1BC state (1, 2, 4)
    }

    /// <summary>
    /// V18c <c>0xA4D994</c>: the bus output gain <c>+0x84</c>, written from <c>dBToLin(bus+0x90)</c>; the
    /// output buffer copies it to <c>out+0x14</c> for the metering tail. A caller input in this model.
    /// </summary>
    public float OutputGain { get; set; } = 1f;

    /// <summary>V18c <c>0xA4D994</c>: the previous bus output gain <c>+0x80</c> (copied to <c>out+0x10</c>).</summary>
    public float PreviousOutputGain { get; set; } = 1f;

    /// <summary>
    /// V18 tail <c>[out+0x18]</c> (C12 X1): the level-analysis/meter object. Null means the native's
    /// <c>[out+0x18]==0</c> case and the tail is skipped.
    /// </summary>
    public WwiseBusMeter? Meter { get; set; }

    /// <summary>V7 <c>bus+0x90</c>: the bus volume in dB (V18c <c>0xA4D994</c>).</summary>
    public float VolumeDb90 { get; set; }

    /// <summary>V7 <c>0xA4B4B0</c> ducking: <c>bus+0x88</c> (the bus ducking sum).</summary>
    public float DuckingSum88 { get; set; }

    /// <summary>V7 <c>0xA4B4B0</c> ducking: <c>bus+0x1E0</c>.</summary>
    public float Ducking1E0 { get; set; }

    /// <summary>V7 <c>0xA4B4B0</c> ducking: <c>bus+0x1E4</c>.</summary>
    public float Ducking1E4 { get; set; }

    /// <summary>V7 <c>0xA4B4B0</c> ducking: <c>bus+0x1D8</c>.</summary>
    public float Ducking1D8 { get; set; }

    /// <summary>V7 <c>bus+0x1C0</c>: the connection count (see <see cref="Connections"/>).</summary>
    public int ConnectionCount => _connections;

    /// <summary>The per-frame eState (<c>+0x68</c>): 0x2D after mixing, 0x11 after ReleaseBuffer (D2.5).</summary>
    public int EState => _eState;

    /// <summary>The connection count (<c>+0x1C0</c>), incremented by Connect and decremented by Disconnect (D2.1).</summary>
    public int Connections => _connections;

    /// <summary>The reuse flag (<c>+0x1CC b0</c>): set when GetOrCreateMixBus reuses this bus (D2.1).</summary>
    public bool Touched => _touched;

    /// <summary>The bus buffer's frame count (<c>+0x6E</c>): max frames after mixing, 0 after ReleaseBuffer (D2.5).</summary>
    public int Frames { get; private set; }

    /// <summary>Whether the FX chain has been instantiated (<c>+0x1B8 b2</c>), i.e. the lazy SetInsertFx has run (D2.2).</summary>
    public bool IsFxInstantiated => _fxInstantiated;

    /// <summary>The bus-level FX bypass (<c>+0x1B8 b0</c>, the bank's FX bypass bits, gapB P6); a caller input.</summary>
    public bool BusBypassed { get; set; }

    /// <summary>Connect <c>0xA4F664</c>: <c>+0x1C0++</c> (D2.1).</summary>
    public void Connect() => _connections++;

    /// <summary>Disconnect <c>0xA4F6F0</c>: <c>+0x1C0--</c> (D2.1). Refuses to go below zero.</summary>
    public void Disconnect()
    {
        if (_connections <= 0)
            throw new InvalidOperationException("a bus connection count cannot go below zero (D2.1 +0x1C0)");
        _connections--;
    }

    /// <summary>D2.1: a child bus also connects to its parent, so the parent's count goes up.</summary>
    public void ConnectToParent(WwiseMixBus parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (Parent is not null)
            throw new InvalidOperationException("this bus already has a parent (D2.1)");
        Parent = parent;
        parent.Connect();
    }

    /// <summary>MarkTouched <c>+0x1CC b0</c>: GetOrCreateMixBus sets it when it reuses this node (D2.1).</summary>
    public void MarkTouched() => _touched = true;

    // ---------------------------------------------------------------- voice-to-bus line fields (M6-025, C23)
    //
    // The fields the connection-creation path 0xA42C60/0xA429F0/0xA42210/0xA42754/0xA4C280 reads and writes on
    // a bus line (a "VPL"). They are plain stores: the behaviour lives in WwiseVoiceLinker.

    /// <summary>
    /// <c>vpl+0x4C</c> (bus pointer), <c>+0x50</c> (key2) and the default-context byte: what
    /// <c>0xA42CA8..0xA42D40</c> matches a line on (M6-025 C23 item 1 row 12, corrected: both null bus
    /// pointers skip the key tests).
    /// </summary>
    // fidelity: M6-025
    public WwiseBusContext Context { get; set; } = WwiseBusContext.None;

    /// <summary>
    /// <c>vpl+0x30</c>: the r1 argument of the line Init <c>0xA4F0EC</c> (<c>0xA4F0FC str r1,[r0,#0x30]</c>). Both callers (<c>0xA423AC/B0</c>,
    /// <c>0xA42470/74</c>) pass r0 = r1 = the new line, so this is the line itself, never the bus (the bus is <c>ctx.Bus</c>,
    /// <see cref="Context"/>). <see cref="WwiseVoiceLinker"/> stores it in Init.
    /// </summary>
    // fidelity: M6-025
    public WwiseMixBus? SelfBus30 { get; set; }

    /// <summary>
    /// <c>vpl+0x6C</c>: the u16 frame count the line Init stores (<c>0xA4F28C strh r7,[r4,#0x6c]</c>, C24.3) after the buffer
    /// allocation succeeded. It is the frames argument as given (<see cref="MaxFrames"/> is the buffer's length).
    /// </summary>
    // fidelity: M6-025
    public ushort InitFrames6C { get; set; }

    /// <summary><c>vpl+0x28/+0x2C</c>: the 64-bit output-device id the line belongs to (row 12).</summary>
    // fidelity: M6-025
    public WwiseDeviceId Device { get; set; }

    /// <summary>
    /// <c>0xA42210</c> allocation class (row 14): true for the 0x1E8-byte line (ctor <c>0xA41E48</c>, chosen
    /// when <c>ctx.bus != 0 &amp;&amp; [bus+0x40]&amp;0xE0000</c>), false for the 0x1D0-byte line (<c>0xA4DFB8</c>).
    /// </summary>
    // fidelity: M6-025
    public bool IsExtendedLine { get; set; }

    /// <summary>
    /// <c>vpl+0x1CC</c> bit1, the bit <c>0xA4C280</c> looks for in the <c>+0x1C8</c> chain (row 16). It is
    /// cleared at creation (<c>0xA42210</c>); no writer is in the C23 rows, so it stays a caller field.
    /// </summary>
    // fidelity: M6-025
    public bool Bit1OfFlags1CC { get; set; }

    /// <summary>
    /// <c>vpl+0x64</c>: the line's format word. Its type nibble (bits 8..11) equal to 1 triggers the
    /// device-table check at <c>0xA4C388..0xA4C3AC</c> (row 19). Written by the line Init <c>0xA4F0EC</c>.
    /// </summary>
    // fidelity: M6-025
    public uint Format64 { get; set; }

    /// <summary>
    /// <c>vpl+0x44</c>: <c>cfgB</c>, stored by the line Init <c>0xA4F0EC</c> next to <c>+0x64 = cfgA</c> (C24.3:
    /// <c>cfgB = parent ? W : cfgA</c>).
    /// </summary>
    // fidelity: M6-025
    public uint Config44 { get; set; }

    /// <summary>
    /// <c>vpl+0x1CC</c> bit3: the <c>flag</c> argument of <c>0xA42210</c>, stored by <c>0xA422DC</c> (C24.3). It is 0
    /// at every call site and no reader was found.
    /// </summary>
    // fidelity: M6-025
    public bool Bit3OfFlags1CC { get; set; }

    /// <summary>
    /// <c>[[vpl+0x1A8]+0xC]</c>: the mix object's <c>+0xC</c> member the disconnect <c>0xA4F6F0</c> tests before
    /// calling <c>mixobj-&gt;vt+0x24(conn)</c> (C24.6). Null with no <see cref="OutputMixObject1A8"/>; in shipped
    /// data <c>[line+0x1A8]</c> stays 0 (C24.6).
    /// </summary>
    // fidelity: M6-025
    public object? MixObject1A8C { get; set; }

    /// <summary><c>vpl+0xC0</c>; <c>0xA4F664</c> clears bit3 (row 18). The field's meaning is UNKNOWN.</summary>
    // fidelity: M6-025
    public byte FlagsC0 { get; set; }

    /// <summary>
    /// <c>0xA4F664</c> parent link (row 15, <c>vpl+0x1C8</c>): records the parent without another
    /// <see cref="Connect"/>, because <c>0xA4F664</c> on the parent has already counted the input.
    /// </summary>
    // fidelity: M6-025
    public void SetParentLink(WwiseMixBus parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (Parent is not null)
            throw new InvalidOperationException("this bus already has a parent (D2.1)");
        Parent = parent;
        OutputBus = parent;                                 // vpl+0x1C8 (row 15; V17 C8 names +0x1C8 the output bus)
    }

    /// <summary>
    /// Mixing input (D2.5, <c>0xA4F9E0</c> child bus / <c>0xA4FBEC</c> voice): <c>+0x68 = 0x2D</c>, state
    /// <c>4 → 1</c>, and <c>+0x6E =</c> max frames. The caller calls it for each voice or child bus that
    /// mixes into this bus during the voice pass (D2.7).
    /// </summary>
    public void MixInput()
    {
        _eState = EStateMixed;
        _state = StateActive;
        Frames = MaxFrames;
    }

    /// <summary><c>u16 [bus+0x6E] = u16 [bus+0x58]</c> (<c>0xA4FD70..0xA4FD74</c>, the end of the voice mix <c>0xA4FBEC</c>): the bus buffer's frame count is the bus frame size.</summary>
    // fidelity: M6-022
    public void SetFramesA4FD74() => Frames = MaxFrames;

    /// <summary>
    /// GetResultingBuffer <c>0xA4FEF8</c> (D2.2/D2.4): instantiate the FX chain lazily if it has not been
    /// (<c>(+0x1B8 &amp; 0xC) ≠ 4</c> → SetInsertFx <c>0xA4F754</c>), then, while the state is 1, run the
    /// slots in order. An in-place slot executes on the bus buffer; a bypassed slot (<c>slot b0</c> or
    /// <c>+0x1B8 b0</c>) is Reset once on the transition and not executed (D2.4).
    /// </summary>
    /// <param name="updateBuffer">The chunk consumer (D2.9). Each chunk an effect flushes reaches it, empty or not.</param>
    public void GetResultingBuffer(WwiseUpdateBuffer updateBuffer)
    {
        ArgumentNullException.ThrowIfNull(updateBuffer);
        InstantiateFxIfNeeded();
        if (_state != StateActive) return;

        for (int i = 0; i < _fx.Length; i++)
        {
            var fx = _fx[i];
            if (fx is null) continue;                       // slot dropped: plug-in not registered (D2.3)

            bool bypassed = _slots[i].Bypassed || BusBypassed;
            if (bypassed)
            {
                // D2.4/gapC 3.1: a newly bypassed slot is Reset once; the latch is its slot bit1.
                if (!_bypassLatch[i]) { fx.Reset(); _bypassLatch[i] = true; }
                continue;
            }
            _bypassLatch[i] = false;
            fx.Execute(Frames, updateBuffer);
        }
    }

    /// <summary>
    /// ReleaseBuffer <c>0xA4F36C</c> (D2.5), run for every bus every frame: <c>state = (+0x68 == 0x11) ? 4 : 1</c>,
    /// then each non-bypassed out-of-place FX slot overrides it with its own state (the last wins), then
    /// <c>+0x68 = 0x11</c>, <c>+0x6E = 0</c> and the buffer is zeroed.
    /// </summary>
    public void ReleaseBuffer()
    {
        int state = _eState == EStateReleased ? StateIdle : StateActive;
        for (int i = 0; i < _fx.Length; i++)
        {
            var fx = _fx[i];
            if (fx is null) continue;
            if (_slots[i].Bypassed || BusBypassed) continue;
            if (!fx.IsInPlace) state = fx.State;            // D2.5: last non-bypassed out-of-place slot wins
        }

        _state = state;
        _eState = EStateReleased;
        Frames = 0;
        Array.Clear(Buffer, 0, Buffer.Length);
    }

    /// <summary>
    /// Destruction <c>0xA43F64</c> (D2.6), run after the bus pass from last to first: the bus is destroyed
    /// when <c>state ≠ 1</c>, <c>+0x1C0 == 0</c> and <c>+0x1CC b0 == 0</c>; otherwise it is kept and b0 is
    /// cleared. Destruction disconnects it from its parent and Terminates every instantiated slot
    /// (<c>Term vt+8</c>, the Hijack's CloseAudioBuffer).
    /// </summary>
    /// <returns>True when this call destroyed the bus.</returns>
    public bool RemoveIdle()
    {
        if (_state != StateActive && _connections == 0 && !_touched)
        {
            Parent?.Disconnect();
            Parent = null;
            foreach (var fx in _fx) fx?.Term();
            return true;
        }
        _touched = false;
        return false;
    }

    private void InstantiateFxIfNeeded()
    {
        if (_fxInstantiated) return;                        // (+0x1B8 & 0xC) == 4
        for (int i = 0; i < _slots.Count; i++)
        {
            var fx = _slots[i].Create();                    // create 0x9CC2AC; null = unregistered, dropped (D2.3)
            _fx[i] = fx;
            if (fx is not null)
            {
                fx.Init();                                  // Init vt+0x1C (D2.2)
                fx.Reset();                                 // Reset vt+0xC (D2.2)
            }
        }
        _fxInstantiated = true;                             // +0x1B8 b2
    }
}

/// <summary>
/// The on-demand mix-bus table (M6-014, gapD D2.1/D2.6). A bus is created when a voice or child bus first
/// needs it and appended to the global bus array; GetOrCreateMixBus reuses an existing node that matches the
/// key and is not in state 2, marking it touched. After the bus pass, the table is swept from last to first
/// and every idle, unconnected, untouched bus is destroyed.
/// </summary>
public sealed class WwiseMixBusHierarchy
{
    private readonly List<WwiseMixBus> _buses = new();

    /// <summary>The global bus array (<c>0x0108DF54</c>), in creation order.</summary>
    public IReadOnlyList<WwiseMixBus> Buses => _buses;

    /// <summary>
    /// GetOrCreateMixBus <c>0xA43438</c> (D2.1): an existing node matching <paramref name="key"/> with
    /// <c>state ≠ 2</c> is reused and marked touched (<c>+0x1CC b0</c>); otherwise <paramref name="create"/>
    /// makes one and it is appended to the array.
    /// </summary>
    public WwiseMixBus GetOrCreate(WwiseMixBusKey key, Func<WwiseMixBus> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        foreach (var bus in _buses)
        {
            if (bus.Key == key && bus.State != WwiseMixBus.StateNotReusable)
            {
                bus.MarkTouched();
                return bus;
            }
        }
        var made = create();
        _buses.Add(made);
        return made;
    }

    /// <summary>
    /// Appends a line the way <c>0xA42210</c> does after a successful Init (row 14): the array
    /// <c>0x108DF54</c> gets the new line at its end.
    /// </summary>
    // fidelity: M6-025
    public void Append(WwiseMixBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _buses.Add(bus);
    }

    /// <summary>
    /// <c>0xA42864..0xA428BC</c> (row 15, corrected): the default line was appended by <c>0xA42210</c>; the
    /// shift (count--, elements up, <c>str r4,[r2]</c> at index 0) leaves it at the front of the array.
    /// </summary>
    // fidelity: M6-025
    public void MoveToFront(WwiseMixBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        if (!_buses.Remove(bus)) throw new InvalidOperationException("the line is not in the array");
        _buses.Insert(0, bus);
    }

    /// <summary>
    /// Destruction pass <c>0xA43F64</c> (D2.6): sweep from last to first and destroy every bus whose
    /// <see cref="WwiseMixBus.RemoveIdle"/> says so. Returns how many were destroyed.
    /// </summary>
    public int RemoveIdle()
    {
        int destroyed = 0;
        for (int i = _buses.Count - 1; i >= 0; i--)
        {
            if (_buses[i].RemoveIdle())
            {
                _buses.RemoveAt(i);
                destroyed++;
            }
        }
        return destroyed;
    }
}
