// fidelity: M6-025, M6-009
namespace Cozmo.Robot.Animation.Wwise;

// The per-PBI modulator list consumption of CalcEffectiveParams (M6-wwise-bank.md C34.2, rows R6, R7 and the verification's corrections 4 and 5): the list reset 0x9E8224, the list walker 0x9E62AC, its
// recursion 0x9E61B4 and the consumer 0xA01918 the Play path calls at 0xA38044 with r1 = params+0x108.
//
// Production entry. Engine: CalcEffectiveParams 0x9FFAD4 reaches the walker through its first-time block (0x9FFF24..0x9FFFA8, only when [pbi+0xE8] bit 6 is clear) and the Play path 0xA379D8 through 0xA01918 (0xA38044).
// The C# counterparts are WwisePlayPath.CalcEffectiveParams and WwisePlaybackBridge.PlaySound (-> WwisePlayPath.ConsumeModulatorsA01918), both reached from WwisePlaybackBridge.OnPlay. Nothing in production constructs
// them yet (C30.W is parked).
//
// Replaces: the test-double seams WwisePlaySeams.A9E8224 / A9E62AC and WwisePlaybackBridge.TailA01918.
//
// Unread, named (throw WwiseMissingBehaviourException when reached unset): 0x9DCE44 (called per id), 0xA6E848 (the producer that fills the out list through node vt+0xAC -> 0x9EF258), the writers of [pbi+0x1C] and [pbi+0x1E8]
// (words of the record 0x9E62AC receives).

/// <summary>
/// A 20-byte record of the modulator out list (the elements <c>0x9E62AC</c> walks) and, by reuse of the shape, the 16-byte record <c>{id, [e+4], 3, [e+0xC]}</c> it builds for <c>0x9E61B4</c>. The fields the control flow reads are
/// <c>+4</c>, <c>+0xC</c> and the id holder <c>+0x10</c> (<c>{array, count}</c>).
/// </summary>
public sealed class WwiseModulatorOutRecord
{
    /// <summary><c>+0</c>: the record's id (the first word of the local record).</summary>
    public uint Word0 { get; init; }

    /// <summary><c>+4</c>.</summary>
    public uint Word4 { get; init; }

    /// <summary><c>+8</c> (3 in the local records).</summary>
    public uint Word8 { get; init; }

    /// <summary><c>+0xC</c>.</summary>
    public uint WordC { get; init; }

    /// <summary><c>[[+0x10]]</c> array and <c>[[+0x10]+4]</c> count: the ids <c>0x9E62AC</c> visits.</summary>
    public IReadOnlyList<uint> Ids10 { get; init; } = Array.Empty<uint>();
}

/// <summary>
/// The 8 words <c>0x9FFF38..0x9FFF98</c> / <c>0xA01918</c> store before the call: <c>[pbi+0x14]</c>, <c>[pbi+0x1E4]</c>, <c>[pbi+0x1E8]</c>, <c>[pbi+0x1C]</c>, <c>[pbi+0x140]</c>, <c>[pbi+0x1D8]</c>, <c>([pbi+0x1BF] &gt;&gt; 2) &amp; 1</c> and the PBI (verification
/// correction 5: the report omitted <c>[pbi+0x1C]</c>).
/// </summary>
public readonly record struct WwiseModulatorCtxRec(uint Key14, uint Word1E4, uint Word1E8, uint Word1C, uint Word140, uint Word1D8, uint Bit1BF2, WwisePlayingInstance Pbi);

/// <summary>The manager <c>*0x10400E8</c> (= <c>0x108D8DC</c>) that <c>0x9E62AC</c> / <c>0x9E61B4</c> take as <c>r0</c>.</summary>
public sealed class WwiseModulatorManager
{
    private readonly Dictionary<uint, uint[]> _dependents = new();

    /// <summary><c>[mgr+0x10]</c>: the word passed as <c>r3</c> of <c>0x9DCE44</c> (<c>0x9E630C</c>, <c>0x9E6244</c>).</summary>
    public uint Word10 { get; set; }

    /// <summary>
    /// Adds an entry of the hash <c>0x9E61B4</c> walks (<c>[mgr]</c> buckets, <c>[mgr+4]</c> count; an entry's id holder is <c>{[e], [e+4]}</c> = array and count, its key <c>[e+0xC]</c>, its chain link <c>[e+0x10]</c>). The chain order of
    /// two entries with one key is not modelled, so a duplicate key is refused.
    /// </summary>
    public void AddDependents(uint key, uint[] ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (!_dependents.TryAdd(key, ids)) throw new InvalidOperationException("a duplicate key: the engine's chain order is not modelled");
    }

    /// <summary>The number of entries (the engine's <c>[mgr+4]</c> is the bucket count, which a dictionary does not have; only emptiness matters here).</summary>
    public int EntryCount => _dependents.Count;

    /// <summary>
    /// <c>0x9DCE44(id, rec, ctxrec, [mgr+0x10], list)</c> (<c>0x9E6254</c>, <c>0x9E631C</c>): the per-id body that creates or refreshes the PBI's modulator instance. Not adopted (C34.2: unread); required. Returns the engine's result (1 counts as success).
    /// </summary>
    public Func<uint, WwiseModulatorOutRecord, WwiseModulatorCtxRec, uint, WwisePlayingInstance, int>? A9DCE44 { get; set; }

    private int Callee(uint id, WwiseModulatorOutRecord rec, WwiseModulatorCtxRec ctx, WwisePlayingInstance list)
        => (A9DCE44 ?? throw new WwiseMissingBehaviourException(
            "M6-025 R7: 0x9DCE44 (the per-id modulator body called at 0x9E631C / 0x9E6254) is not adopted by C34.2; supply WwiseModulatorManager.A9DCE44"))(id, rec, ctx, Word10, list);

    /// <summary>
    /// <c>0x9E62AC(mgr, outlist, ctxrec, list)</c> (R7; <paramref name="list"/> is <c>pbi+0x34</c>, the address of the PBI's list-head field, so the C# passes the PBI): <paramref name="records"/> is the out list <c>{data, count}</c> of 20-byte records; an empty list returns 1. Per record the ids of <c>[e+0x10]</c> are visited in order:
    /// <c>0x9DCE44(id, e, ctxrec, [mgr+0x10], list)</c>, then <c>0x9E61B4(mgr, {id, [e+4], 3, [e+0xC]}, ctxrec, list)</c> (result ignored). The return value is 1 while every <c>0x9DCE44</c> returned 1, else 2 (<c>cmp r0,#1; cmpeq sb,#1;
    /// moveq sb,#1; movne sb,#2</c>, <c>0x9E6350..0x9E6364</c>); once 2 it stays 2.
    /// </summary>
    public int A9E62AC(IReadOnlyList<WwiseModulatorOutRecord> records, WwiseModulatorCtxRec ctx, WwisePlayingInstance list)
    {
        ArgumentNullException.ThrowIfNull(records);
        int sb = 1;                                                                       // 0x9E62D8 mov sb,#1; 0x9E6390
        foreach (var e in records)                                                        // 0x9E62EC..0x9E637C
        {
            foreach (uint id in e.Ids10)                                                  // 0x9E6304..0x9E636C
            {
                int r0 = Callee(id, e, ctx, list);                                        // 0x9E631C bl 0x9DCE44
                var local = new WwiseModulatorOutRecord { Word0 = id, Word4 = e.Word4, Word8 = 3, WordC = e.WordC };   // 0x9E6320..0x9E634C
                sb = r0 == 1 && sb == 1 ? 1 : 2;                                          // 0x9E6350..0x9E6360
                A9E61B4(local, ctx, list);                                                // 0x9E6364 bl 0x9E61B4 (the result is not read)
            }
        }
        return sb;
    }

    /// <summary>
    /// <c>0x9E61B4(mgr, rec, ctxrec, list)</c> (R7's second call, verification correction 15): a table with no entries (<c>[mgr+4] == 0</c>) returns 1; the entry whose key <c>[e+0xC]</c> equals <c>[rec]</c> is looked up (a miss returns 1); for each of its
    /// ids: <c>0x9DCE44(id, rec, ctxrec, [mgr+0x10], list)</c>, then the recursion on <c>{id, 0, 3, [rec+0xC]}</c> (its result is ignored), the flag as in <see cref="A9E62AC"/>. The return value is that flag (1 when the entry has no ids).
    /// </summary>
    public int A9E61B4(WwiseModulatorOutRecord rec, WwiseModulatorCtxRec ctx, WwisePlayingInstance list)
    {
        ArgumentNullException.ThrowIfNull(rec);
        if (_dependents.Count == 0) return 1;                                             // 0x9E61BC..0x9E61C8 beq 0x9E61F8
        if (!_dependents.TryGetValue(rec.Word0, out var ids)) return 1;                   // 0x9E61E4..0x9E621C (hash by [rec] % count, chain on [e+0xC])
        int r7 = 1;                                                                       // 0x9E6234 movne r7,#1
        foreach (uint fp in ids)                                                          // 0x9E623C..0x9E629C
        {
            int r0 = Callee(fp, rec, ctx, list);                                          // 0x9E6254 bl 0x9DCE44
            var local = new WwiseModulatorOutRecord { Word0 = fp, Word4 = 0, Word8 = 3, WordC = rec.WordC };   // 0x9E626C..0x9E627C
            r7 = r0 == 1 && r7 == 1 ? 1 : 2;                                              // 0x9E6280..0x9E6290
            A9E61B4(local, ctx, list);                                                    // 0x9E6294 bl 0x9E61B4
        }
        return r7;                                                                        // 0x9E62A0
    }

    /// <summary>
    /// <c>0x9E8224(list)</c> (R6): for every item of the list (<c>[list]</c> is the head, <c>[item]</c> the next) <c>[item+0xC] = 0</c> (<c>0x9E8224..0x9E8240</c>). A null head returns at once.
    /// </summary>
    public static void A9E8224(IEnumerable<WwiseListRecord>? items)
    {
        if (items is null) return;
        foreach (var item in items) item.Word0C = 0;                                      // 0x9E8230..0x9E8240
    }
}
