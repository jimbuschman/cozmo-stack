using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-025 / M6-009 (C34.2 R6, R7, 0xA01918, verification corrections 4, 5, 15): the per-PBI modulator list consumption. Every expected value is the engine's own output from re-analysis/tools/emu/emu_mod.py (the real
/// 0x9E8224, 0x9E62AC, 0x9E61B4 and 0xA01918 under Unicorn on a manager, an out list, a context record and a PBI built in emulated memory; the one stand-in is 0x9DCE44, which C34 does not adopt: it records its five
/// arguments and returns a scripted value per id). The generated WwiseModulatorOracle.cs holds the inputs and the outputs.
/// </summary>
public class WwiseModulatorListTests
{
    private static WwiseModulatorOutRecord Rec(int index, uint w4, uint wc, uint[] ids) => new() { Word0 = 0, Word4 = w4, Word8 = (uint)(0x80 + index), WordC = wc, Ids10 = ids };

    private static (WwiseModulatorManager Mgr, List<(uint Id, uint W0, uint W4, uint W8, uint WC, uint[] Ctx, uint W10, uint List)> Log) Manager(
        (uint Key, uint[] Ids)[] table, Dictionary<uint, int> results, uint listMarker = 0x77770000)
    {
        var log = new List<(uint, uint, uint, uint, uint, uint[], uint, uint)>();
        var mgr = new WwiseModulatorManager { Word10 = 0x10101010 };
        foreach (var (key, ids) in table) mgr.AddDependents(key, ids);
        mgr.A9DCE44 = (id, rec, ctx, w10, list) =>
        {
            log.Add((id, rec.Word0, rec.Word4, rec.Word8, rec.WordC,
                new[] { ctx.Key14, ctx.Word1E4, ctx.Word1E8, ctx.Word1C, ctx.Word140, ctx.Word1D8, ctx.Bit1BF2, ReferenceEquals(ctx.Pbi, null) ? 0u : ctx.Pbi.PlayingId },
                w10, listMarker));
            return results.TryGetValue(id, out int r) ? r : 1;
        };
        return (mgr, log);
    }

    private static WwiseModulatorCtxRec Ctx(WwisePlayingInstance pbi) => new(0xC0DE0000, 0xC0DE0001, 0xC0DE0002, 0xC0DE0003, 0xC0DE0004, 0xC0DE0005, 0xC0DE0006, pbi);

    private static void AssertCalls((uint Id, uint W0, uint W4, uint W8, uint WC, uint[] Ctx, uint W10, uint List)[] engine,
        List<(uint Id, uint W0, uint W4, uint W8, uint WC, uint[] Ctx, uint W10, uint List)> actual, string name, uint pbiWord)
    {
        Assert.True(engine.Length == actual.Count, $"{name}: {engine.Length} engine calls, {actual.Count} C# calls");
        for (int i = 0; i < engine.Length; i++)
        {
            var e = engine[i];
            var a = actual[i];
            // The engine's eighth ctx word is 0xC0DE0007 for 0x9E62AC / 0x9E61B4 (the scenario's word); the PBI itself for 0xA01918 (printed 0xFFFFFFFF); the C# record carries the PBI object, logged by its playing id.
            var expectedCtx = e.Ctx.ToArray();
            if (expectedCtx[7] == 0xFFFFFFFFu) expectedCtx[7] = pbiWord;
            else expectedCtx[7] = a.Ctx[7];
            Assert.True((e.Id, e.W0, e.W4, e.W8, e.WC, e.W10, e.List) == (a.Id, a.W0, a.W4, a.W8, a.WC, a.W10, a.List), $"{name}: call {i} arguments differ");
            Assert.True(expectedCtx.SequenceEqual(a.Ctx), $"{name}: call {i} context record differs");
        }
    }

    [Fact]
    public void R6_0x9E8224_ClearsWord0COfEveryListItemAndANullHeadDoesNothing()
    {
        // C34.2 R6, emu_mod.py Reset: the loop 0x9E8230..0x9E8240 stores 0 at [item+0xC] for each item from [list] along [item+0]; a zero head returns at once (0x9E8228).
        foreach (var (name, (items, nullHead, after)) in ModulatorOracle.Reset)
        {
            var list = new List<WwiseListRecord>();
            for (int i = 0; i < items; i++) list.Add(new WwiseListRecord { Word0C = (uint)(0x70 + i) });
            list.Reverse();                                                                                   // the oracle builds head = the last item created
            WwiseModulatorManager.A9E8224(nullHead ? null : list);
            Assert.True(after.SequenceEqual(list.Select(x => x.Word0C)), $"{name}");
        }
    }

    [Fact]
    public void R7_0x9E62AC_EveryOracleScenarioVisitsTheSameIdsInTheSameOrderAndReturnsTheSameCode()
    {
        // C34.2 R7: per record, per id: 0x9DCE44(id, e, ctxrec, [mgr+0x10], list), then the local record {id, [e+4], 3, [e+0xC]} through 0x9E61B4; the return is 1 while every 0x9DCE44 gave 1 else 2 (sticky); 0x9E61B4's
        // result is ignored. emu_mod.py Walk (table of dependents, scripted results of the stand-in 0x9DCE44).
        Assert.NotEmpty(ModulatorOracle.Walk);
        foreach (var (name, (table, records, results, ret, calls)) in ModulatorOracle.Walk)
        {
            var (mgr, log) = Manager(table, results);
            var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 0x4242, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(0, 1, 5, 0, 0), new byte[0x44], null, continuous: false);
            var ctxRec = Ctx(pbi);
            int r = mgr.A9E62AC(records.Select((x, i) => Rec(i, x.W4, x.WC, x.Ids)).ToList(), ctxRec, pbi);
            Assert.True(ret == r, $"{name}: engine {ret}, C# {r}");
            AssertCalls(calls, log, name, 0xC0DE0007u);
        }
    }

    [Fact]
    public void R7_0x9E61B4_EveryOracleScenarioFollowsTheDependencyTable()
    {
        // C34.2 verification correction 15: 0x9E61B4(mgr, rec, ctxrec, list): no table or no key gives 1; each id of the entry goes to 0x9DCE44(id, rec, ...) then to the recursion on {id, 0, 3, [rec+0xC]}.
        Assert.NotEmpty(ModulatorOracle.Rec);
        foreach (var (name, (table, rec, results, ret, calls)) in ModulatorOracle.Rec)
        {
            var (mgr, log) = Manager(table, results);
            var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 0x4242, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(0, 1, 5, 0, 0), new byte[0x44], null, continuous: false);
            var record = new WwiseModulatorOutRecord { Word0 = rec.Id, Word4 = rec.W4, Word8 = rec.W8, WordC = rec.WC };
            int r = mgr.A9E61B4(record, Ctx(pbi), pbi);
            Assert.True(ret == r, $"{name}: engine {ret}, C# {r}");
            AssertCalls(calls, log, name, 0xC0DE0007u);
        }
    }

    [Fact]
    public void R8_0xA01918_EveryOracleScenarioConsumesTheOutVectorAndSetsE8Bit6()
    {
        // C34.2 verification corrections 4, 5: 0xA01918(pbi, vec, 1): [pbi+0x34] != 0 runs 0x9E8224; a non-zero [vec+4] runs 0x9E62AC with the 8-word record {[pbi+0x14], [pbi+0x1E4], [pbi+0x1E8], [pbi+0x1C],
        // [pbi+0x140], [pbi+0x1D8], ([pbi+0x1BF] >> 2) & 1, pbi}; [pbi+0xE8] |= 0x40. emu_mod.py Consume.
        Assert.NotEmpty(ModulatorOracle.Consume);
        foreach (var (name, (e8In, count, records, table, listItems, results, bf, e8Out, calls, itemsAfter)) in ModulatorOracle.Consume)
        {
            var (mgr, log) = Manager(table, results, listMarker: 0xFFFFFFFE);
            var items = new List<WwiseListRecord>();
            for (int i = 0; i < Math.Max(listItems, 0); i++) items.Add(new WwiseListRecord { Word0C = (uint)(0x70 + i) });
            items.Reverse();
            // [pbi+0x1BF] is written by the ctor from params+0x128 bit 2: the scenario's byte is either 4 or 0.
            var ctorParams = new WwisePlayInitParams { PlayingId = 0xAA140, TargetNodeId = 1, GameObjectId = 0xAA14, InitialDelaySamples = 0xAA1D8, Flags128 = (byte)(bf != 0 ? 4 : 0) };
            var pbi = new WwisePlayingInstance(ctorParams, 1, new WwiseSourceDescriptor(0, 1, 5, 0, 0), new byte[0x44], null, continuous: false) { Flags0E8 = e8In, Field34 = listItems < 0 ? 0u : 1u };
            // The words the C# PBI does not model are the seam's: [pbi+0x1E4] 0xAA1E4, [pbi+0x1E8] 0xAA1E8, [pbi+0x1C] 0xAA1C.
            var path = new WwisePlayPath(_ => null, new WwisePlaySeams
            {
                RecordsOf34 = _ => items,
                ModulatorCtxWords = _ => (0xAA1E4u, 0xAA1E8u, 0xAA1Cu),
            }) { Modulators = mgr };
            var vec = new WwiseAcLocalBlock { Word4 = count };
            for (int i = 0; i < records.Length; i++) vec.Records.Add(Rec(i, records[i].W4, records[i].WC, records[i].Ids));
            path.ConsumeModulatorsA01918(pbi, vec);
            Assert.True(e8Out == pbi.Flags0E8, $"{name}: E8 engine 0x{e8Out:X2}, C# 0x{pbi.Flags0E8:X2}");
            Assert.True(itemsAfter.SequenceEqual(items.Select(x => x.Word0C)), $"{name}: the list items");
            AssertCalls(calls, log, name, pbi.PlayingId);
        }
    }

    [Fact]
    public void R7_AnUnsetCalleeOrManagerOrWordsSeamIsAVisibleStop()
    {
        // 0x9DCE44 is not adopted; the manager, the record list and the three context words are required when reached.
        var mgr = new WwiseModulatorManager();
        var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new WwiseSourceDescriptor(0, 1, 5, 0, 0), new byte[0x44], null, continuous: false);
        Assert.Throws<WwiseMissingBehaviourException>(() => mgr.A9E62AC(new[] { Rec(0, 1, 2, new uint[] { 41 }) }, Ctx(pbi), pbi));
        var path = new WwisePlayPath(_ => null, new WwisePlaySeams());
        var vec = new WwiseAcLocalBlock { Word4 = 1 };
        vec.Records.Add(Rec(0, 1, 2, new uint[] { 41 }));
        Assert.Throws<WwiseMissingBehaviourException>(() => path.ConsumeModulatorsA01918(pbi, vec));                 // no ModulatorCtxWords
        path.Seams.ModulatorCtxWords = _ => (0u, 0u, 0u);
        Assert.Throws<WwiseMissingBehaviourException>(() => path.ConsumeModulatorsA01918(pbi, vec));                 // no manager
        pbi.Field34 = 1;
        var empty = new WwiseAcLocalBlock();
        Assert.Throws<WwiseMissingBehaviourException>(() => path.ConsumeModulatorsA01918(pbi, empty));               // no RecordsOf34
        // A count above the records the data pointer addresses cannot be walked.
        var bad = new WwiseAcLocalBlock { Word4 = 2 };
        bad.Records.Add(Rec(0, 1, 2, new uint[0]));
        Assert.Throws<InvalidOperationException>(() => { _ = bad.InUse; });
    }
}
