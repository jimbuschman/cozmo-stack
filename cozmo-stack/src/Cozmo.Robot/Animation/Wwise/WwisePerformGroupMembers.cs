// fidelity: M6-022
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// One item of the V26 group member's array (M6-022 V26, 0x9FDD90, C12 bus-group Q5). The native item
/// carries <c>+0x40</c> (value base), <c>+0x3c</c> (value slope per tick), the three coefficient bases
/// <c>+0x48/+0x4c/+0x50</c> and slopes <c>+0x54/+0x58/+0x5c</c>, the target array <c>+0x20</c> (count
/// <c>+0x24</c>) and the completion tick <c>+0x34</c>. The class name is UNKNOWN; the behaviour is read.
/// </summary>
public interface IWwiseV26Item
{
    /// <summary><c>obj+0x40</c>: the value base.</summary>
    float ValueBase { get; }

    /// <summary><c>obj+0x3c</c>: the value slope per tick.</summary>
    float ValueSlope { get; }

    /// <summary><c>obj+0x48</c>.</summary>
    float Out0Base { get; }

    /// <summary><c>obj+0x4c</c>.</summary>
    float Out1Base { get; }

    /// <summary><c>obj+0x50</c>.</summary>
    float Out2Base { get; }

    /// <summary><c>obj+0x54</c>.</summary>
    float Out0Slope { get; }

    /// <summary><c>obj+0x58</c>.</summary>
    float Out1Slope { get; }

    /// <summary><c>obj+0x5c</c>.</summary>
    float Out2Slope { get; }

    /// <summary><c>obj+0x34</c>: the completion tick.</summary>
    long CompletionTick { get; }

    /// <summary>The target array <c>obj+0x20</c>, count <c>obj+0x24</c>.</summary>
    IReadOnlyList<IWwiseV26Target> Targets { get; }
}

/// <summary>One target of a V26 item (M6-022 V26): the object at <c>[[p]+0xd0]</c> with the gate byte at
/// <c>+0x3c</c> and the three write slots at <c>+0x18/+0x1c/+0x20</c>.</summary>
public interface IWwiseV26Target
{
    /// <summary><c>[target+0x3c] &amp; 4</c>: when set, the write is skipped (C12 bus-group Q5).</summary>
    bool GateFlag4 { get; }

    /// <summary>Writes the three interpolated values to <c>+0x18/+0x1c/+0x20</c>.</summary>
    void Write(float v0, float v1, float v2);
}

/// <summary>
/// The row table a V26 completion item reads (M6-022 V28-tail, 0x9FD910, missing-bodies item 5): the
/// descriptor at <c>[obj+0xC]</c> is <c>{+0 base, +4 count, +8 slope0, +0xC slope1, +0x10 slope2}</c>. Each
/// element is 0x10 bytes <c>{e0,e1,e2,e3}</c>. The Wwise feature name is UNKNOWN.
/// </summary>
public interface IWwiseV26Table
{
    /// <summary><c>[obj+0xC]+4</c>: the row count.</summary>
    int Count { get; }

    /// <summary>The row at <paramref name="index"/>: <c>{e0,e1,e2,e3}</c>.</summary>
    (float E0, float E1, float E2, float E3) Row(int index);

    /// <summary>The table descriptor's three slopes (<c>+8/+0xC/+0x10</c>).</summary>
    (float S0, float S1, float S2) Slopes { get; }
}

/// <summary>
/// A V26 item that carries the completion state <c>0x9FD910</c> reads (M6-022 V28-tail, missing-bodies
/// item 5): the table <c>+0xC</c>, the shuffle bag <c>+8</c>, the two indices <c>+0x10/+0x12</c>, the row
/// index <c>+0x14</c>, the flags <c>+0x18</c>, the byte <c>+0x1D</c>, the thresholds <c>+0x30/+0x34</c>,
/// the segment length/rate/t-start <c>+0x38/+0x3C/+0x40</c>, and the mutable base/slopes
/// <c>+0x48/+0x4C/+0x50/+0x54/+0x58/+0x5C</c>.
/// </summary>
public interface IWwiseV26CompletionItem : IWwiseV26Item
{
    /// <summary><c>[obj+0xC]</c>: the row table descriptor.</summary>
    IWwiseV26Table Table { get; }

    /// <summary><c>[obj+8]</c>: the used-byte descriptor's buffer (the shuffle bag); <c>[+4]</c> is its length.</summary>
    IList<byte> ShuffleBag { get; }

    /// <summary><c>[obj+0x10]</c>: the u16 index (random/end path).</summary>
    int Index10 { get; set; }

    /// <summary><c>[obj+0x12]</c>: the u16 index limit.</summary>
    int IndexLimit12 { get; }

    /// <summary><c>[obj+0x14]</c>: the u16 row index.</summary>
    int RowIndex14 { get; set; }

    /// <summary><c>[obj+0x18]</c>: bit0 randomize, bit1 continue.</summary>
    int Flags18 { get; }

    /// <summary><c>[obj+0x1D]</c>: the byte the shuffle-bag continue rule reads.</summary>
    byte Byte1D { get; }

    /// <summary><c>[obj+0x30]</c>: the previous threshold.</summary>
    float PrevThreshold30 { get; set; }

    /// <summary><c>[obj+0x34]</c>: the threshold (the completion tick).</summary>
    float Threshold34 { get; set; }

    /// <summary><c>[obj+0x38]</c>: the segment length.</summary>
    int SegmentLength38 { get; set; }

    /// <summary><c>[obj+0x3C]</c>: the rate <c>1/length</c>.</summary>
    float Rate3c { get; set; }

    /// <summary><c>[obj+0x40]</c>: the t-start <c>-old/length</c>.</summary>
    float TStart40 { get; set; }

    /// <summary>Writes the base <c>+0x48/+0x4C/+0x50</c>.</summary>
    void SetBase(float b0, float b1, float b2);

    /// <summary>Writes the slopes <c>+0x54/+0x58/+0x5C</c>.</summary>
    void SetSlopes(float s0, float s1, float s2);
}

/// <summary>
/// The engine's LCG at <c>[0x1040090] -> 0x108D868</c> (M6-022 V28-tail, missing-bodies item 5 step 2): state
/// = state*0x5851F42D4C957F2D + 1; the output is the high 32 bits shifted right one, giving
/// <c>u * 2^-30 - 1</c> in <c>[-1,1)</c>. The two multiplier halves are used directly by the native.
/// </summary>
public sealed class WwiseLcg
{
    // fidelity: M6-022
    private ulong _state;

    /// <summary>Seeds the generator (the native's global state).</summary>
    public WwiseLcg(ulong seed = 0) => _state = seed;

    /// <summary>The current state, for a caller that mirrors the native global.</summary>
    public ulong State { get => _state; set => _state = value; }

    /// <summary>One step; returns the high word shifted right one.</summary>
    public uint Next()
    {
        _state = _state * 0x5851F42D4C957F2DUL + 1UL;
        return (uint)(_state >> 33);
    }

    /// <summary>A value in <c>[-1,1)</c>: <c>u * 2^-30 - 1</c>.</summary>
    public float NextSigned() => (float)(Next() * (1.0 / 1073741824.0) - 1.0);
}

/// <summary>
/// 0x9FD910 (M6-022 V26-tail, missing-bodies item 5): the V26 on-completion table/random segment selector.
/// It picks the next 3-component target (the next table row, or a random row via the LCG and the
/// <c>[obj+8]</c> shuffle bag), sets the base <c>= row + rand*slope</c> with <c>rand</c> in <c>[-1,1)</c>,
/// sets the slopes <c>= target - base</c>, the segment length <c>max(1,(G+e3-1)/G)</c>, the rate
/// <c>1/len</c> and the t-start <c>-old/len</c>, and advances the threshold <c>[obj+0x34]</c>.
///
/// <para><b>Gap.</b> The divisor <c>G = [[0x10400EC]]</c>'s identity is UNKNOWN (missing-bodies UNKNOWN
/// list 4). It is a caller input here; when it is 0 the completion uses <c>G = 1</c> and records the gap
/// rather than inventing the constant.</para>
/// </summary>
public static class WwiseV26Completion
{
    // fidelity: M6-022

    /// <summary>0x9FD910: advances <paramref name="item"/> at <paramref name="tick"/>.</summary>
    /// <param name="item">The item whose completion state is updated.</param>
    /// <param name="tick">The engine tick.</param>
    /// <param name="divisor">The UNKNOWN <c>G</c> divisor; 0 selects the documented gap (G=1).</param>
    /// <returns>True when the UNKNOWN divisor was used as 1 (the gap).</returns>
    public static bool Complete(IWwiseV26CompletionItem item, long tick, int divisor, WwiseLcg lcg)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(lcg);
        bool divisorGap = divisor <= 0;
        int g = divisorGap ? 1 : divisor;

        var table = item.Table;
        int idx = item.RowIndex14;
        int count = table.Count;
        if (idx < count)
        {
            AdvanceRow(item, table, idx, g, lcg);
            return divisorGap;
        }

        // Random/end path (0x9FD93C..0x9FDD48).
        if ((item.Flags18 & 1) == 0)
        {
            item.Index10++;
            if (item.IndexLimit12 <= item.Index10)
            {
                if ((item.Flags18 & 2) != 0)
                {
                    item.Index10 = 0;
                }
                else
                {
                    // 0x9FDCD0: "return with [obj]=0". The meaning of [obj+0] is UNKNOWN (missing-bodies
                    // UNKNOWN list 5), so the field is not written here rather than guessed.
                    return divisorGap;
                }
            }
        }
        else
        {
            uint r = lcg.Next();
            item.Index10 = item.IndexLimit12 == 0 ? 0 : (int)(r % (uint)item.IndexLimit12);
        }

        if ((item.Flags18 & 2) != 0)
        {
            // Shuffle bag: set buffer[idx]; if all set, clear and set buffer[idx] again.
            int bagIndex = Math.Clamp(idx, 0, Math.Max(0, item.ShuffleBag.Count - 1));
            bool anyZero = false;
            for (int i = 0; i < item.ShuffleBag.Count; i++)
                if (item.ShuffleBag[i] == 0) { anyZero = true; break; }
            if (!anyZero && item.ShuffleBag.Count > 0)
                for (int i = 0; i < item.ShuffleBag.Count; i++) item.ShuffleBag[i] = 0;
            if (bagIndex < item.ShuffleBag.Count) item.ShuffleBag[bagIndex] = 1;
        }

        // Interpolate from the current base to row 0 (0x9FDA00 -> 0x9FDB70).
        item.RowIndex14 = 0;
        InterpolateToRow(item, table, 0, g);
        return divisorGap;
    }

    private static void AdvanceRow(IWwiseV26CompletionItem item, IWwiseV26Table table, int idx, int g, WwiseLcg lcg)
    {
        var row = table.Row(idx);
        var slopes = table.Slopes;
        float r0 = lcg.NextSigned();
        float r1 = lcg.NextSigned();
        float r2 = lcg.NextSigned();
        float b0 = row.E0 + r0 * slopes.S0;
        float b1 = row.E1 + r1 * slopes.S1;
        float b2 = row.E2 + r2 * slopes.S2;
        item.SetBase(b0, b1, b2);
        item.RowIndex14 = idx + 1;

        int length = Math.Max(1, (g + (int)row.E3 - 1) / g);
        item.SegmentLength38 = length;

        int next = idx + 1;
        if (table.Count > next)
        {
            InterpolateToRow(item, table, next, g);
        }
        else
        {
            // Random/end path: the base is already set; target row 0.
            InterpolateToRow(item, table, 0, g);
        }
    }

    private static void InterpolateToRow(IWwiseV26CompletionItem item, IWwiseV26Table table, int rowIndex, int g)
    {
        var target = table.Row(rowIndex);
        float old = item.Threshold34;
        int length = item.SegmentLength38;
        item.Threshold34 = old + length;
        item.PrevThreshold30 = old;
        item.Rate3c = 1f / length;
        item.TStart40 = -old / length;
        item.SetSlopes(target.E0 - item.Out0Base, target.E1 - item.Out1Base, target.E2 - item.Out2Base);
    }
}

/// <summary>
/// V26 group member 2 <c>0x9FF308(manager,tick)</c> (M6-022 V26, C12 bus-group Q5). It walks its items and,
/// for each item whose <c>[item]==1</c>, runs <c>0x9FDD90(item,tick)</c>: the settled three-component
/// interpolation, then the completion tail <c>0x9FD910</c> when <c>tick &gt;= [obj+0x34]</c>. The manager
/// object identity is UNKNOWN; the item list is a caller input.
/// </summary>
public sealed class WwiseGroupMemberV26 : IWwisePerformGroupMember
{
    // fidelity: M6-022

    /// <summary>The items the member walks (the native manager's list).</summary>
    public List<IWwiseV26Item> Items { get; } = new();

    /// <summary>
    /// The UNKNOWN completion divisor <c>G = [[0x10400EC]]</c> (missing-bodies UNKNOWN list 4). It is a
    /// caller input; 0 selects the documented gap (G=1) in <see cref="WwiseV26Completion.Complete"/>.
    /// </summary>
    public int SegmentDivisor { get; set; }

    /// <summary>The LCG state the completion draws from (the native global <c>0x108D868</c>).</summary>
    public WwiseLcg Lcg { get; } = new();

    /// <summary>True once a completion ran with the UNKNOWN divisor gap (G treated as 1).</summary>
    public bool CompletionDivisorGap { get; private set; }

    /// <summary>V26/0x9FDD90: evaluate every item at <paramref name="tick"/>.</summary>
    public void Tick(long tick)
    {
        foreach (var item in Items)
        {
            // 0x9FDD94..0x9FDDA4: val = [obj+0x40] + (float)tick * [obj+0x3c].
            float val = item.ValueBase + tick * item.ValueSlope;

            // 0x9FDDAC..0x9FDDB4, 0x9FDE34..0x9FDE40: clamp to [0,1].
            float t = val < 1.0f ? (val > 0.0f ? val : 0.0f) : 1.0f;

            // 0x9FDDC8..0x9FDDE8.
            float out0 = item.Out0Base + t * item.Out0Slope;
            float out1 = item.Out1Base + t * item.Out1Slope;
            float out2 = item.Out2Base + t * item.Out2Slope;

            // 0x9FDDF0..0x9FDE1C: write the targets whose ([target+0x3c] & 4) == 0.
            foreach (var target in item.Targets)
                if (!target.GateFlag4)
                    target.Write(out0, out1, out2);

            // 0x9FDE20..0x9FDE30: tick < [obj+0x34] returns; else tail-call 0x9FD910.
            if (tick >= item.CompletionTick && item is IWwiseV26CompletionItem completion)
                if (WwiseV26Completion.Complete(completion, tick, SegmentDivisor, Lcg))
                    CompletionDivisorGap = true;
        }
    }
}

/// <summary>One item of the V25 group member's array (M6-022 V25, 0xA3693C, C12 bus-group Q4): the state at
/// <c>+0x30</c>, the tick at <c>+0x1C</c>, and the array at <c>+0x20</c> (count <c>+0x24</c>).</summary>
public interface IWwiseV25Item
{
    /// <summary><c>item+0x30</c>: 1/4 process, 2 arm, 6 free (C12 bus-group Q4).</summary>
    int State { get; set; }

    /// <summary><c>item+0x1C</c>: the tick stored on the state-2 arm.</summary>
    long Tick { get; set; }

    /// <summary><c>item+0x20</c>: the buffer freed by <c>0xA3587C</c>; <c>+0x24</c>/<c>+0x28</c> are zeroed.</summary>
    Action? FreeBuffers { get; }
}

/// <summary>
/// V25 group member 1 <c>0xA36AC4(manager,tick)</c> (M6-022 V25, C12 bus-group Q4). It calls
/// <c>0xA3693C(manager,tick,manager)</c> and <c>0xA3693C(manager,tick,manager+0xC)</c>; <c>0xA3693C</c>
/// walks an array of items with state at <c>+0x30</c>: 4/1 -> <c>0xA35998(item,tick)</c>; 2 ->
/// <c>[+0x1C]=tick</c>, <c>[+0x30]=3</c>; 6 -> <c>0xA3587C</c>+free.
/// </summary>
public sealed class WwiseGroupMemberV25 : IWwisePerformGroupMember
{
    // fidelity: M6-022

    /// <summary>The first array (the native <c>manager</c>).</summary>
    public List<IWwiseV25Item> Items { get; } = new();

    /// <summary>The second array (the native <c>manager+0xC</c>).</summary>
    public List<IWwiseV25Item> Items2 { get; } = new();

    /// <summary>C12 bus-group Q4 <c>0xA35998(item,tick)</c>: the state-1/4 processor; caller seam.</summary>
    public Action<IWwiseV25Item, long>? Process { get; set; }

    /// <summary>
    /// C12 bus-group Q4 <c>0xA3587C(obj)</c>: free <c>[obj+0x20]</c> and zero <c>+0x24/+0x28</c>. The state-6
    /// path then calls <c>0xA35878(item)</c> (UNKNOWN) and removes the item from the array.
    /// </summary>
    public Action<IWwiseV25Item>? Free { get; set; }

    /// <summary>V25: run both <c>0xA3693C</c> walks at <paramref name="tick"/>.</summary>
    public void Tick(long tick)
    {
        Walk(Items, tick);
        Walk(Items2, tick);
    }

    private void Walk(List<IWwiseV25Item> items, long tick)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            switch (item.State)
            {
                case 4:
                case 1:
                    Process?.Invoke(item, tick);                 // 0xA35998
                    break;
                case 2:
                    item.Tick = tick;                            // 0xA369D8
                    item.State = 3;
                    break;
                case 6:
                    item.FreeBuffers?.Invoke();                  // 0xA3587C
                    Free?.Invoke(item);                          // 0xA35878 + array removal
                    items.RemoveAt(i);
                    break;
            }
        }
    }
}

/// <summary>
/// V27 group member 3 <c>0x9D3C98()</c> (M6-022 V27, C12 bus-group Q6). Gate <c>byte[0x108DA34]</c> ->
/// <c>0x9D3644</c>; tail <c>0x9D3864</c>. Both walk the list at <c>0x108DA10</c> and tear down finished
/// objects. The object/voice classes are UNKNOWN and the nested destructors are RECOVERABLE_GAP, so the
/// walks are a caller seam. The row's key settled facts are modelled: the gate, the two calls in order, and
/// that <c>0xA437E0</c>/<c>0xA4B4B0</c> are <b>not</b> called here.
/// </summary>
public sealed class WwiseGroupMemberV27 : IWwisePerformGroupMember
{
    // fidelity: M6-022

    /// <summary>V27: the gate <c>byte[0x108DA34]</c>.</summary>
    public bool Gate { get; set; }

    /// <summary>V27 <c>0x9D3644</c>: the first list walk (gated); caller seam.</summary>
    public Action? FirstWalk { get; set; }

    /// <summary>V27 <c>0x9D3864</c>: the tail list walk; caller seam.</summary>
    public Action? TailWalk { get; set; }

    /// <summary>V27: <c>0x9D3C98()</c>.</summary>
    public void Tick(long tick)
    {
        if (Gate) FirstWalk?.Invoke();                           // 0x9D3CB0
        TailWalk?.Invoke();                                      // 0x9D3CB8
    }
}

/// <summary>
/// V28 group member 4 <c>0x9E6D2C(manager)</c> (M6-022 V28, C12 bus-group Q7 and the modulator-evaluator
/// report). It calls <c>0x9E2BD0(manager+0x10, tick)</c> before the lock, locks <c>[manager]+0x8C</c>,
/// walks the bucket hash (<c>+0x90</c> array, <c>+0x94</c> count) calling <c>0x9D8A24</c>, unlocks, then
/// tails <c>0x9E2AE4</c> (the refcount purge). The manager/object classes are UNKNOWN and the nested purge
/// bodies are RECOVERABLE_GAP; the call order and refcount rule are modelled with seams.
/// </summary>
public sealed class WwiseGroupMemberV28 : IWwisePerformGroupMember
{
    // fidelity: M6-022

    /// <summary>V28 <c>0x9E2BD0(manager+0x10, tick)</c>: the per-voice modulator/transition evaluator; caller seam.</summary>
    public Action<long>? EvaluateModulators { get; set; }

    /// <summary>V28: the bucket-hash nodes; each is passed to <c>0x9D8A24</c> (caller seam).</summary>
    public List<Action> Buckets { get; } = new();

    /// <summary>V28 tail <c>0x9E2AE4</c>: the refcount purge; caller seam.</summary>
    public Action? Purge { get; set; }

    /// <summary>V28: <c>0x9E6D2C(manager)</c>.</summary>
    public void Tick(long tick)
    {
        EvaluateModulators?.Invoke(tick);                        // 0x9E6D48, before the lock
        // lock [manager]+0x8C (0x4D3064); the caller's Buckets are already locked by the caller seam.
        foreach (var node in Buckets)
            node();                                              // 0x9D8A24 per bucket node
        // unlock (0x4D3070)
        Purge?.Invoke();                                         // 0x9E2AE4
    }
}