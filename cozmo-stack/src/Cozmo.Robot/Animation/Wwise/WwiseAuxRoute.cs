// fidelity: M6-010, M6-022
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The process-wide send/audibility globals: <c>[0x1052454]</c> (a float: the game-defined send's linear threshold of <c>0x9BD368</c>, the below-audibility threshold of <c>0x9BEB30</c>/<c>0x9BCA68</c> and the ducking threshold of <c>0xA4B4B0</c>), <c>[0x1052450]</c> (the raw dB the setter
/// stores: the user-defined send's threshold) and <c>[0x105241C]</c> (the setter's type gate, initially 3). The image holds <c>0x37800000</c>, <c>0xC2C0999A</c> and 3; they change only through the setter <c>0x9A080C(value, type)</c> (<see cref="ApplySetterA9A080C"/>), which the Init.bnk STMG
/// reader <c>0x9B0B14</c> calls (the reader is UNREAD; <see cref="Shared"/> is the state its call produces for Init.bnk). Every reader in the stack shares <see cref="Shared"/> so they cannot disagree.
/// </summary>
// fidelity: M6-010, M6-022
public sealed class WwiseSendGlobals
{
    /// <summary><c>[0x1052454]</c> as the image holds it.</summary>
    public const uint ImageGameLinearBits = 0x37800000;

    /// <summary><c>[0x1052450]</c> as the image holds it.</summary>
    public const uint ImageUserDbBits = 0xC2C0999A;

    /// <summary>
    /// The process-wide state every reader shares (aux builder, below-audibility test, ducking): the state after the Init.bnk STMG load, <see cref="AfterInitBnkStmg"/>. The setter's only caller in the image is 0x9B0B48 inside the STMG reader 0x9B0B14 (<c>ldr r0,[sp,#0x28]; mov r1,#2; bl 0x9A080C</c>),
    /// dispatched at every bank load (0x9B7AC8/0x9B7AD8), and Init.bnk loads before any Play, so the engine never plays in the image state. Use <c>new WwiseSendGlobals()</c> for the pre-load (image) state.
    /// </summary>
    public static WwiseSendGlobals Shared { get; } = AfterInitBnkStmg();

    /// <summary>
    /// The state the STMG reader's call produces for Init.bnk: <c>setter(-80.0f, 2)</c> applied to a fresh image-state object (0xC2A00000 / 0x38D2306A, type gate 2; engine-checked by the oracle). This is NOT a replay of the reader: the reader body 0x9B0B14 stays an unread seam
    /// (<c>WwiseBankLoader.StmgA9B0B14</c> still throws). -80.0f is the shipped Init.bnk STMG threshold (C10: [0x1052450] = -80.0f; <c>WwiseAuxRouteTests.C40_4_TheSendThresholdSetterMatchesTheEngine_0x9A080C</c>).
    /// </summary>
    public static WwiseSendGlobals AfterInitBnkStmg()
    {
        var g = new WwiseSendGlobals();
        g.ApplySetterA9A080C(-80f, 2);
        return g;
    }

    /// <summary><c>[0x1052454]</c>.</summary>
    public float GameLinear { get; set; } = BitConverter.UInt32BitsToSingle(ImageGameLinearBits);

    /// <summary><c>[0x1052450]</c>.</summary>
    public float UserDb { get; set; } = BitConverter.UInt32BitsToSingle(ImageUserDbBits);

    /// <summary><c>[0x105241C]</c>: the setter returns 1 without storing when its type is above it, and stores its type here otherwise.</summary>
    public int TypeGate { get; set; } = 3;

    /// <summary>
    /// <c>0x9A080C(value, type)</c> (0x9A080C..0x9A0900): <c>value &lt; -96.3 (0xC2C0999A)</c> or <c>value &gt; 0</c> returns 0x1F; <c>type &gt; [0x105241C]</c> returns 1; otherwise <c>[0x105241C] = type</c>, <c>t = value * 0.05f</c>, <c>p = powf(10, t)</c> (the phone's libm: <see cref="WwiseHostMath.Powf"/>) and
    /// <c>f</c> = the fast power (0 for <c>t &lt; -37.0f</c>, else the polynomial of <see cref="WwisePlaybackLimiter.Lin9BEB30"/>), <c>[0x1052454] = p &gt; f ? p : f</c> (<c>vcmp s13,s15; vmovgt</c>: a NaN <c>p</c> loses) and <c>[0x1052450] = value</c> (raw bits); returns 1.
    /// </summary>
    public int ApplySetterA9A080C(float value, int type)
    {
        float min = BitConverter.UInt32BitsToSingle(0xC2C0999A);
        if (!(value >= min) && !float.IsNaN(value)) return 0x1F;                    // 0x9A0814 vcmpe s15,s14; bpl (unordered continues)
        if (value > 0f) return 0x1F;                                                // 0x9A0828 vcmpe s15,#0; bgt
        if (type > TypeGate) return 1;                                              // 0x9A083C..0x9A0848 ble 0x9A0850; mov r0,#1
        TypeGate = type;                                                            // 0x9A0860 str r1,[r3,#4]
        float t = value * BitConverter.UInt32BitsToSingle(0x3D4CCCCD);              // 0x9A086C
        float p = WwiseHostMath.Powf(10f, t);                                       // 0x9A0864..0x9A0874 powf(10.0f, t)
        float f = WwisePlaybackLimiter.Lin9BEB30(value);                            // 0x9A087C..0x9A08CC (t < -37.0f gives 0.0f at 0x9A0900)
        GameLinear = p > f ? p : f;                                                 // 0x9A08EC vcmp s13,s15; 0x9A08F4 vmovgt; 0x9A08F8 vstr
        UserDb = value;                                                             // 0x9A08E8 str r4,[r2]
        return 1;
    }
}

/// <summary>One 12-byte entry <c>0x9BD368</c> writes: <c>{bus id, linear gain, kind}</c> (kind 1 game-defined, 2 user-defined).</summary>
public readonly record struct WwiseAuxSendItem(uint BusId, float Gain, uint Kind);

/// <summary>
/// What <c>0x9BD368(ctx, out)</c> leaves in its out buffer: up to 8 entries and the zero id word after them, which is written only when the count is 7 or less (<c>0x9BD8A0 cmp r3,#7; addls..strls</c>; the early <c>pop</c>s at <c>0x9BD69C</c>, <c>0x9BD764</c>, <c>0x9BD804</c> run only
/// at a count of 8). With 8 entries nothing ends the list: <c>0x9D4228</c> stops at the end of its 8 entries too.
/// </summary>
public sealed class WwiseAuxSendBlock
{
    private readonly List<WwiseAuxSendItem> _items = new();

    /// <summary>The entries in order.</summary>
    public IReadOnlyList<WwiseAuxSendItem> Items => _items;

    /// <summary>The entry count (0..8).</summary>
    public int Count => _items.Count;

    /// <summary>True when the id word after the last entry is the written 0 (the count is 7 or less).</summary>
    public bool HasTerminator => _items.Count <= 7;

    internal void Add(WwiseAuxSendItem item) => _items.Add(item);

    /// <summary>The id word at index <paramref name="k"/> as <c>0x9D4228</c> reads it: the entry's id, or 0 for the terminator. The word after the entries of a block with no terminator does not exist.</summary>
    public uint IdAt(int k)
    {
        if (k < _items.Count) return _items[k].BusId;
        if (k == _items.Count && HasTerminator) return 0;
        throw new InvalidOperationException("the block has no id word at this index (8 entries, no terminator)");
    }
}

/// <summary>One 0x14-byte entry of <c>voice+0x2C</c> (C40.4 T-A6): <c>{target [0], current [4], handle [8] = -1, id [0xC], kind [0x10]}</c>.</summary>
public sealed class WwiseAuxEntry
{
    /// <summary><c>[+0]</c>: the target gain (the walk sums it into g[1]).</summary>
    public float Target { get; set; }

    /// <summary><c>[+4]</c>: the current gain (the walk sums it into g[0]); nothing moves it toward <see cref="Target"/> except the merge of <c>0x9D4228</c>.</summary>
    public float Current { get; set; }

    /// <summary><c>[+8]</c>: -1 for every entry <c>0x9D4228</c> writes; it is the key2 of the line the entry connects to (<c>0xA43434</c>).</summary>
    public int Handle { get; set; }

    /// <summary><c>[+0xC]</c>: the bus id.</summary>
    public uint Id { get; set; }

    /// <summary><c>[+0x10]</c>: the kind (1 game-defined, 2 user-defined) that becomes the connection's <c>arg5</c> (<c>0xA43434</c>).</summary>
    public uint Kind { get; set; }

    /// <summary>A copy.</summary>
    public WwiseAuxEntry Clone() => new() { Target = Target, Current = Current, Handle = Handle, Id = Id, Kind = Kind };
}

/// <summary>
/// The aux send route of a voice (C40.4, T-A1..T-A8): <c>0x9BDA88</c> (the gate), <c>0x9BD368</c> (the builder) and <c>0x9D4228</c> (the merge into <c>voice+0x2C</c>). It replaces <c>WwiseAuxSendBuilder</c> of <c>WwiseGain.cs</c>, a model of <c>0x9BD368</c> that used a decimal threshold, a
/// dB comparison, stopped nowhere at a zero id and wrote no terminator. The dispatch <c>0x9D4108 -&gt; 0xA43434</c> is <see cref="WwiseVoiceLinker.DispatchAuxEntry9D4108"/>; the aux walk is <see cref="WwiseVoiceBusPass"/>'s <c>MixConnections</c>.
/// </summary>
// fidelity: M6-010, M6-022
public static class WwiseAuxRoute
{
    /// <summary>
    /// <c>0x9BDA88(ctx)</c> (T-A5a): 1 when the byte <c>[ctx+0x88]</c> (<c>[pbi+0x94]</c>, the use-game-aux flag) is non-zero or any of the words <c>[ctx+0x74]</c>, <c>[ctx+0x78]</c>, <c>[ctx+0x7C]</c>, <c>[ctx+0x80]</c> (the four user aux ids, <c>pbi+0x80..0x8C</c>) is non-zero, else 0.
    /// </summary>
    public static bool Continue9BDA88(WwisePlayingInstance pbi)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        if (pbi.Byte94 != 0) return true;                                           // 0x9BDA88 ldrb r3,[r0,#0x88]; bne 0x9BDAC4
        for (int i = 0; i < 4; i++)                                                 // 0x9BDA94..0x9BDABC
            if (BitConverter.ToUInt32(pbi.Block80, 4 * i) != 0) return true;
        return false;
    }

    /// <summary>The <c>vcmpe</c> + fast power the builder uses: <c>x * 0.05f</c>, 0 below -37.0f, else the polynomial (<see cref="WwisePlaybackLimiter.Lin9BEB30"/> is the same code).</summary>
    public static float Lin(float x) => WwisePlaybackLimiter.Lin9BEB30(x);

    /// <summary>
    /// <c>0x9BD368(ctx, out)</c> (T-A5b, C40.4 correction 4). The game-defined sends (kind 1) exist only when the byte <c>[ctx+0x88]</c> is non-zero and <c>[GO+0x24]</c> is non-zero (the first pair's id; a zero first id leaves no game send even if later pairs have ids): <c>L = lin([ctx+0x84])</c> (the
    /// float at <c>pbi+0x90</c>) and, per GO pair in order and stopping at the first zero id, <c>g = L * gain</c> is stored when <c>threshold &lt; g</c> (<c>vcmpe</c>: a NaN is not stored). The user-defined sends (kind 2) take the four ids <c>pbi+0x80..0x8C</c> with the dB values
    /// <c>pbi+0x70..0x7C</c>: a zero id skips that entry and moves on, and an entry is stored as <c>{id, lin(dB), 2}</c> when the dB threshold is below its dB value (the fourth entry compares the other way round, <c>vcmpe s14,s15; ble</c>, with the same outcome for ordered values). Once 8
    /// entries exist processing ends. The game pairs stop at the first zero id; the user entries are independent.
    /// </summary>
    /// <param name="pbi">The ctx (<c>pbi+0xC</c>) the voice's owner holds.</param>
    /// <param name="go">The game object <c>[ctx+8]</c>; a null is the native null dereference (stop) when the use flag is set.</param>
    /// <param name="thresholds">The two globals.</param>
    public static WwiseAuxSendBlock Build9BD368(WwisePlayingInstance pbi, WwiseGameObjectRef? go, WwiseSendGlobals thresholds)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        ArgumentNullException.ThrowIfNull(thresholds);
        var block = new WwiseAuxSendBlock();
        if (pbi.Byte94 != 0)                                                        // 0x9BD368 ldrb r3,[r0,#0x88]; cmp r3,#0; beq 0x9BD5F0
        {
            var obj = go ?? throw new InvalidOperationException("M6-010 T-A5b: 0x9BD374 loads [ctx+8] and 0x9BD380 reads [[ctx+8]+0x24] with the use-game-aux byte set; a null game object is a native null dereference");
            if (obj.Aux24[0].BusId != 0)                                            // 0x9BD380 ldr r3,[r2,#0x24]; 0x9BD388 cmp r3,#0; beq 0x9BD5F0
            {
                float l = Lin(BitConverter.UInt32BitsToSingle(pbi.Word90));         // 0x9BD394..0x9BD3F0 ([ctx+0x84] = pbi+0x90, dB)
                for (int i = 0; i < 4; i++)                                         // the four pairs, 0x9BD3F4, 0x9BD484, 0x9BD520, 0x9BD5BC
                {
                    var (id, gain) = obj.Aux24[i];
                    if (id == 0) break;                                             // 0x9BD420 / 0x9BD4BC / 0x9BD554 cmp lr,#0; beq 0x9BD5F0
                    float g = WwiseArmFloat.Mul(l, gain);                           // 0x9BD3F0 vmul s14,s14,s13 (lin), then * [GO+0x28+8i]
                    if (thresholds.GameLinear < g) block.Add(new WwiseAuxSendItem(id, g, 1));   // 0x9BD3FC vcmpe s12,s14; 0x9BD404 strmi
                }
            }
        }

        // the user-defined sends: ids pbi+0x80..0x8C ([ctx+0x74..0x80]), dB pbi+0x70..0x7C ([ctx+0x64..0x70]).
        for (int i = 0; i < 4; i++)
        {
            uint id = BitConverter.ToUInt32(pbi.Block80, 4 * i);                    // 0x9BD5F4 / 0x9BD6C8 / 0x9BD768 / 0x9BD808
            if (id == 0) continue;                                                  // beq to the next entry
            float db = BitConverter.ToSingle(pbi.Block70, 4 * i);
            bool store = i < 3 ? thresholds.UserDb < db                              // 0x9BD60C vcmpe s15,s14; bpl skip
                               : db > thresholds.UserDb;                            // 0x9BD818 vcmpe s14,s15; ble skip
            if (!store) continue;
            block.Add(new WwiseAuxSendItem(id, Lin(db), 2));                        // 0x9BD630..0x9BD694 (the dB goes through the same fast power)
            if (block.Count > 7) return block;                                      // 0x9BD68C cmp r3,#7; bls; 0x9BD69C pop: no terminator at 8 (the fourth entry has no such test: the count cannot pass 8)
        }
        return block;                                                               // 0x9BD8A0: the terminator is the HasTerminator rule
    }

    /// <summary>
    /// <c>0x9D4228(block, arr, flag, &amp;count, voice)</c> (T-A6): merges the freshly built sends into the voice's entry array <c>voice+0x2C</c>. (1) The old entries with a target above 0 (<c>vcmpe s15,#0; ble</c>) are copied to a stack list <c>{id, target, kind}</c>. (2) The array is rewritten from the block
    /// entries up to the first zero id word (8 at most): target = gain, current = 0 when <paramref name="flag"/> (bit 1 of <c>[voice+0xCD]</c>, set once a merge has run) else the gain, handle -1. (3) For each old stack entry in order, the first new entry with the same id whose flag byte is clear gets
    /// <c>current = old target</c> and its flag set; none, with fewer than 8 entries, appends <c>{target 0, current old target, handle -1, old id, old kind}</c> (<c>0x9D4640..0x9D4668</c>). (4) the count is stored and <c>0x9D4108(voice, &amp;entry, mask)</c>
    /// is called for each entry by the caller (the engine stores the count first, then dispatches). A block whose first id is 0 with nothing kept gives 0 and dispatches nothing.
    /// </summary>
    /// <returns>The new count <c>n</c> (the byte the engine stores at <c>[voice+0xCC]</c>); <c>arr[0..n)</c> are the entries to dispatch in order.</returns>
    /// <param name="count">The current <c>[voice+0xCC]</c>.</param>
    /// <param name="stackFlags">
    /// The 8 flag bytes at <c>sp+0..7</c> of <c>0x9D4228</c>. The ones of the entries written in step 2 are cleared (<c>strb r2,[sp+k]</c>); the ones of APPENDED entries are never written, so an old entry sharing the id of an appended entry reads uninitialised stack
    /// (BLOCKED_EXTERNAL, T-A6): the value is the host's input, and without it the read throws <see cref="WwiseMissingBehaviourException"/> at that point.
    /// </param>
    public static int Merge9D4228(WwiseAuxSendBlock block, WwiseAuxEntry[] arr, byte count, bool flag, byte[]? stackFlags = null)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(arr);
        if (arr.Length < 8) throw new ArgumentException("the entry array holds 8 entries (voice+0x2C..0xCC)", nameof(arr));
        if (count > 8) throw new WwiseMissingBehaviourException("M6-010 T-A6: [voice+0xCC] above 8 overruns the 8-entry stack list of 0x9D4228 (0x9D4240..0x9D4288); nothing writes it above 8");

        // (1) 0x9D4240..0x9D4288
        var old = new List<(uint Id, float Target, uint Kind)>();
        for (int i = 0; i < count; i++)
            if (arr[i].Target > 0f) old.Add((arr[i].Id, arr[i].Target, arr[i].Kind));

        // (2) 0x9D4290..0x9D4568
        var flags = new byte[8];
        var known = new bool[8];                                                    // true once the flag byte of that index was written in this call
        int n = 0;
        uint id0 = block.IdAt(0);
        if (id0 == 0)                                                               // 0x9D429C / 0x9D440C beq 0x9D466C
        {
            if (old.Count == 0) return 0;                                           // 0x9D4678 strb lr,[r3]: no dispatch (voice+8 is not read)
        }
        else
        {
            for (int k = 0; k < 8; k++)
            {
                if (k > 0 && block.IdAt(k) == 0) break;                             // 0x9D42D8 / 0x9D4304 / ... beq 0x9D4688 / 0x9D46C8 / ..: the count is the index
                var it = block.Items[k];
                var e = arr[k];
                e.Target = it.Gain;                                                 // [r1] = [r0+4+12k]
                e.Current = flag ? 0f : it.Gain;                                    // flag != 0: 0 (str ip,[r1,#4]); else the gain
                e.Handle = -1;                                                      // str r4,[r1,#8]
                e.Id = it.BusId;                                                    // str r7,[r1,#0xC]
                e.Kind = it.Kind;                                                   // str sb,[r1,#0x10]
                flags[k] = 0; known[k] = true;                                      // strb r2,[sp+k] (r2 = 0)
                n = k + 1;
            }
        }

        // (3) 0x9D4570..0x9D4668
        foreach (var o in old)
        {
            bool matched = false;
            for (int idx = 0; idx < n; idx++)                                       // 0x9D45B0..0x9D45AC
            {
                if (arr[idx].Id != o.Id) continue;                                  // 0x9D45B4 cmp ip,fp; bne
                byte flagByte;
                if (known[idx]) flagByte = flags[idx];
                else
                    flagByte = (stackFlags ?? throw new WwiseMissingBehaviourException(
                        $"M6-010 T-A6 (BLOCKED_EXTERNAL): the old entry for bus {o.Id} meets the appended entry {idx} with the same id; its flag byte (0x9D45C0 ldrb ip,[r2,sp]) is uninitialised stack, so the merge cannot go on; supply the stack bytes as stackFlags"))[idx];
                if (flagByte != 0) continue;                                        // 0x9D45C4 cmp ip,#0; bne 0x9D45A0
                arr[idx].Current = o.Target;                                        // 0x9D45CC..0x9D45D8 str ip,[r0,#4]
                flags[idx] = 1; known[idx] = true;                                  // 0x9D45DC strb sl,[r2,#-0x68]
                matched = true;
                break;
            }
            if (matched || n > 7) continue;                                         // 0x9D4638 cmp r4,#7; bhi 0x9D45E0
            var app = arr[n];                                                       // 0x9D4640..0x9D4668: the flag byte of index n is NOT written
            app.Target = 0f; app.Current = o.Target; app.Handle = -1; app.Id = o.Id; app.Kind = o.Kind;
            n++;
        }

        return n;                                                                   // (4) 0x9D45F0 strb r4,[r3]; the dispatch loop 0x9D4608..0x9D462C is the caller's
    }
}

/// <summary>
/// VFP single-precision <c>vadd.f32</c> / <c>vmul.f32</c> as the phone runs them (FPSCR.DN clear): the result is the NaN operand when there is one (a signalling NaN first, then the first operand when both are quiet; the result is quieted) and the default NaN <c>0x7FC00000</c> for an invalid
/// operation (inf - inf, 0 * inf); the host's SSE gives <c>0xFFC00000</c> for the latter. Used where the engine's NaN bits are observable (the aux walk's sums, the send gain).
/// </summary>
// fidelity: M6-010, M6-022
public static class WwiseArmFloat
{
    private const uint Quiet = 0x00400000u;

    private static float Pick(float a, float b)
    {
        uint ab = BitConverter.SingleToUInt32Bits(a), bb = BitConverter.SingleToUInt32Bits(b);
        bool aNan = float.IsNaN(a), bNan = float.IsNaN(b);
        bool aSig = aNan && (ab & Quiet) == 0, bSig = bNan && (bb & Quiet) == 0;
        if (aSig) return BitConverter.UInt32BitsToSingle(ab | Quiet);
        if (bSig) return BitConverter.UInt32BitsToSingle(bb | Quiet);
        if (aNan) return a;
        if (bNan) return b;
        return BitConverter.UInt32BitsToSingle(0x7FC00000u);
    }

    /// <summary><c>vadd.f32 Sd, Sn = a, Sm = b</c>.</summary>
    public static float Add(float a, float b)
    {
        float r = a + b;
        return float.IsNaN(r) ? Pick(a, b) : r;
    }

    /// <summary><c>vmul.f32 Sd, Sn = a, Sm = b</c>.</summary>
    public static float Mul(float a, float b)
    {
        float r = a * b;
        return float.IsNaN(r) ? Pick(a, b) : r;
    }
}
