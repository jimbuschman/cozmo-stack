using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-025 (C31.1, C32.2): the media table, the bank loader's DIDX and DATA handlers for mode 0, the writer 0x9B49A4, the lookup 0xA1EC54 / 0x9BB320 / 0x9BB1F8 and the release 0xA1ECBC / 0x9B65A8. Every
/// expected value is the engine's own output, produced by re-analysis/tools/emu/emu_media.py (the writer, lookup and release under Unicorn on a BM object and bank objects) and emu_loader.py (the loader 0x9B74D8
/// itself, type 0 / mode 0, on a bank file); none comes from running this implementation. Each test names the oracle scenario (S1.., L1..) and the addresses it checks.
/// </summary>
public class WwiseMediaTableTests
{
    private const uint Base = 0x08100000;

    private static WwiseMediaBank Bank(params (uint Id, uint Offset, uint Size)[] entries)
    {
        var didx = new byte[12 * entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            BitConverter.GetBytes(entries[i].Id).CopyTo(didx, 12 * i);
            BitConverter.GetBytes(entries[i].Offset).CopyTo(didx, 12 * i + 4);
            BitConverter.GetBytes(entries[i].Size).CopyTo(didx, 12 * i + 8);
        }
        return new WwiseMediaBank { Id = 1, Didx18 = didx, Count30 = (uint)entries.Length };
    }

    private static (WwiseMediaTable Table, WwiseBankMemory Memory) NewTable()
    {
        var memory = new WwiseBankMemory();
        return (new WwiseMediaTable(memory), memory);
    }

    [Fact]
    public void S1_OneBankThreeEntriesTheWriterReturns1AndTheLookupGivesDataPlusOffsetAndSize()
    {
        // emu_media.py S1 (0x9B49A4 then 0xA1EC54): ret 1, [bank+0x2C] = 3, buckets 29 (the first prime of the table, 0x9B4A8C), 3 nodes; lookups (DATA+offset, size) with *r3 = the bank; an absent id gives (0, 0) and
        // leaves *r3; each lookup takes a reference (node refcount 2 after the first, [e+0x14]++ at 0x9BB210) and a bank with [+4] bit 1 gets [bank+0x48]++ (4 after three picks).
        var (table, _) = NewTable();
        var a = Bank((1001, 0, 10), (1002, 10, 20), (1003, 30, 5));
        Assert.Equal(1, table.WriteBankA9B49A4(a, Base));
        Assert.Equal(3u, a.Counter2C);
        Assert.Equal(29u, table.BucketCount);
        Assert.Equal(3u, table.NodeCount);
        var r1 = table.LookupA1EC54(1001, 777, 0);
        Assert.Equal((Base, 10u), (r1.Data, r1.Size));
        Assert.Same(a, r1.Bank);
        Assert.Equal(2, table.Find(1001)!.RefCount1C);
        var r2 = table.LookupA1EC54(1002, 777, 0);
        Assert.Equal((Base + 10, 20u), (r2.Data, r2.Size));
        var r3 = table.LookupA1EC54(1003, 777, 0);
        Assert.Equal((Base + 30, 5u), (r3.Data, r3.Size));
        var miss = table.LookupA1EC54(9999, 777, 0);
        Assert.Equal((0u, 0u), (miss.Data, miss.Size));
        Assert.Null(miss.Bank);
        Assert.Equal(4, a.RefCount48);
    }

    [Fact]
    public void S2_ALookupPicksTheLargestEligibleItemFirstOfEqualSizesAndFlaggedBanksAreSkipped()
    {
        // emu_media.py S2 (0x9BB1F8): banks A (5, off 0, size 100), B (5, off 8, size 300), C (5, off 4, size 300) written in that order: refcount 3 (a rewrite by another bank adds a reference), items newest first C, B, A
        // (0x9B4C04..0x9B4C6C); the lookup takes the first item of the largest size (signed, strictly greater: C), refcount 4, and C's [bank+0x48] becomes 2 (B's stays 1). With B and C flagged ([+4] bit 0, 0x9BB250)
        // the pick is A and the refcount 5; with A flagged too the result is (0, 0) and the reference taken is given back (0x9BB2E8).
        var (table, _) = NewTable();
        var a = Bank((5, 0, 100)); var b = Bank((5, 8, 300)); var c = Bank((5, 4, 300));
        Assert.Equal(1, table.WriteBankA9B49A4(a, 0x1000));
        Assert.Equal(1, table.WriteBankA9B49A4(b, 0x2000));
        Assert.Equal(1, table.WriteBankA9B49A4(c, 0x3000));
        var node = table.Find(5)!;
        Assert.Equal(3, node.RefCount1C);
        Assert.Equal(new[] { (0x3004u, 300u), (0x2008u, 300u), (0x1000u, 100u) }, node.Items.Select(i => (i.Data, i.Size)).ToArray());
        Assert.Equal(new[] { c, b, a }, node.Items.Select(i => i.Bank).ToArray());
        Assert.Equal(5u, node.LastId20);
        var r = table.LookupA1EC54(5, 0, 0);
        Assert.Equal((0x3004u, 300u), (r.Data, r.Size));
        Assert.Same(c, r.Bank);
        Assert.Equal(4, node.RefCount1C);
        Assert.Equal((1, 2), (b.RefCount48, c.RefCount48));
        b.Flags4 |= 1; c.Flags4 |= 1;
        r = table.LookupA1EC54(5, 0, 0);
        Assert.Equal((0x1000u, 100u), (r.Data, r.Size));
        Assert.Same(a, r.Bank);
        Assert.Equal(5, node.RefCount1C);
        a.Flags4 |= 1;
        r = table.LookupA1EC54(5, 0, 0);
        Assert.Equal((0u, 0u), (r.Data, r.Size));
        Assert.Null(r.Bank);
        Assert.Equal(5, node.RefCount1C);                                            // restored
        var (table2, _) = NewTable();
        var plain = Bank((5, 0, 100));
        plain.Flags4 = 0;                                                            // [+4] bit 1 clear: no reference on the bank
        table2.WriteBankA9B49A4(plain, 0x1000);
        var pr = table2.LookupA1EC54(5, 0, 0);
        Assert.Equal((0x1000u, 100u), (pr.Data, pr.Size));
        Assert.Equal(1, plain.RefCount48);
    }

    [Fact]
    public void S2_AFastSourceBlockWithAPointerReturnsItWithItsSizeAndLeavesTheBank()
    {
        // 0xA1EC58..0xA1EC70 (R1.6): [obj+0x10] != 0 stores [obj+8] to *r2 and [obj+0x10] to *r1; r3 is untouched.
        var (table, _) = NewTable();
        var r = table.LookupA1EC54(1, 777, 0x08001000);
        Assert.Equal((0x08001000u, 777u), (r.Data, r.Size));
        Assert.Null(r.Bank);
    }

    [Fact]
    public void S3_TheSameBankRewritingItsEntryOverwritesTheItemAndAddsAReference()
    {
        // emu_media.py S3 (0x9B4BCC..0x9B4CB8, 0x9B4CCC..0x9B4CDC): (7, 0, 10) written, then written again with DATA at 0x5000: one item with the new pointer, refcount 2.
        var (table, _) = NewTable();
        var a = Bank((7, 0, 10));
        table.WriteBankA9B49A4(a, 0x1000);
        var node = table.Find(7)!;
        Assert.Equal((1, 1, 1), (node.RefCount1C, node.Items.Count, node.ItemCapacity));
        Assert.Equal(0x1000u, node.Items[0].Data);
        a.Counter2C = 0;
        Assert.Equal(1, table.WriteBankA9B49A4(a, 0x5000));
        Assert.Equal((2, 1), (node.RefCount1C, node.Items.Count));
        Assert.Equal((0x5000u, 10u), (node.Items[0].Data, node.Items[0].Size));
    }

    [Fact]
    public void S4_AnEntryWithIdZeroIsSkippedButCounted()
    {
        // emu_media.py S4 (0x9B4A3C..0x9B4A44, 0x9B4A18..0x9B4A28): (0, 0, 4) is skipped, (11, 4, 4) is written; [bank+0x2C] = 2, one node.
        var (table, _) = NewTable();
        var a = Bank((0, 0, 4), (11, 4, 4));
        Assert.Equal(1, table.WriteBankA9B49A4(a, 0x1000));
        Assert.Equal(2u, a.Counter2C);
        Assert.Equal(1u, table.NodeCount);
        Assert.Equal((0x1004u, 4u), (table.Find(11)!.Items[0].Data, table.Find(11)!.Items[0].Size));
    }

    [Fact]
    public void S5_TheTableRehashesToTheNextPrimeWhenTheLoadFactorAbove0_9IsMet()
    {
        // emu_media.py S5 (0x9B4A60..0x9B4AA8, 0x9B4D04..0x9B5038): 27 nodes in 29 buckets (27/29 = 0.931 is checked before the 28th is inserted) rehash to 53 on the 28th entry; 60 entries end at 97 buckets;
        // every key is still found after the rehash.
        foreach (var (count, buckets) in new[] { (27, 29u), (28, 53u), (60, 97u) })
        {
            var (table, _) = NewTable();
            var a = Bank(Enumerable.Range(0, count).Select(i => ((uint)(100 + i), (uint)i, 1u)).ToArray());
            Assert.Equal(1, table.WriteBankA9B49A4(a, 0x1000));
            Assert.Equal(buckets, table.BucketCount);
            Assert.Equal((uint)count, table.NodeCount);
            Assert.Equal((uint)count, a.Counter2C);
        }
        var (t28, _) = NewTable();
        t28.WriteBankA9B49A4(Bank(Enumerable.Range(0, 28).Select(i => ((uint)(100 + i), (uint)i, 1u)).ToArray()), 0x1000);
        Assert.Equal(new[] { 0x1000u, 0x100Du, 0x101Bu }, new[] { 100u, 113u, 127u }.Select(k => t28.LookupA1EC54(k, 0, 0).Data).ToArray());
    }

    [Fact]
    public void S5_ThePrimeTableIsTheEnginesTableAtFFA248()
    {
        // The 27 words at 0xFFA248 (read from the .so by the oracle's setup): 0x1D first, 0x60000005 last.
        Assert.Equal(27, WwiseMediaTable.PrimeTable.Length);
        Assert.Equal(0x1Du, WwiseMediaTable.PrimeTable[0]);
        Assert.Equal(0x60000005u, WwiseMediaTable.PrimeTable[^1]);
        Assert.Equal(0x3F666666, WwiseMediaTable.LoadLimitBits);
    }

    [Theory]
    [InlineData(1, 0u, 0u, 0u, true)]    // the rehash array allocation (0x9B4D40): 0x34, buckets 0, cleanup
    [InlineData(2, 0u, 29u, 0u, true)]   // the first node (0x9B4B38): 0x34, buckets 29 (the rehash held), no node, cleanup
    [InlineData(3, 0u, 29u, 0u, true)]   // the first items array (0x9B4EE4): the node is removed again (0x9B4FA4..0x9B5094), 0x34, cleanup
    public void S6_AnAllocationFailureOnTheFirstEntryReturns0x34AndReachesTheUnadoptedCleanup(int failing, uint counter, uint buckets, uint nodes, bool cleanup)
    {
        // emu_media.py S6: ret 0x34 with [bank+0x2C] 0, the table as listed and 0x9B45D8 called once. 0x9B45D8's body is not adopted, so the C# stops there (WwiseMissingBehaviourException) with the same table state.
        var (table, memory) = NewTable();
        int calls = 0;
        memory.AllocationFails = () => ++calls == failing;
        var a = Bank((1001, 0, 10), (1002, 10, 20));
        Assert.True(cleanup);
        Assert.Throws<WwiseMissingBehaviourException>(() => table.WriteBankA9B49A4(a, 0x1000));
        Assert.Equal(counter, a.Counter2C);
        Assert.Equal(buckets, table.BucketCount);
        Assert.Equal(nodes, table.NodeCount);
        Assert.Null(table.Find(1001));
    }

    [Fact]
    public void S6_AFailureAfterAnEntryWasProcessedReturns1WithoutTheCleanupAndFailingTheItemsOfTheSecondEntryRemovesItsNode()
    {
        // emu_media.py S6: allocation #4 (the second entry's node) fails after the first entry: ret 1 (0x9B4C84 returns [sp+8] = 1, which the first entry stored), [bank+0x2C] 1, one node, no cleanup, node 1001 intact.
        // Allocation #5 (the second entry's items array): the node is removed again, [sp+8] = 0x34, cleanup (here the unadopted body: an exception), one node left.
        var (t4, m4) = NewTable();
        int c4 = 0;
        m4.AllocationFails = () => ++c4 == 4;
        var a4 = Bank((1001, 0, 10), (1002, 10, 20));
        Assert.Equal(1, t4.WriteBankA9B49A4(a4, 0x1000));
        Assert.Equal(1u, a4.Counter2C);
        Assert.Equal(1u, t4.NodeCount);
        Assert.Equal(0x1000u, t4.Find(1001)!.Items[0].Data);
        Assert.Null(t4.Find(1002));

        var (t5, m5) = NewTable();
        int c5 = 0;
        m5.AllocationFails = () => ++c5 == 5;
        var a5 = Bank((1001, 0, 10), (1002, 10, 20));
        Assert.Throws<WwiseMissingBehaviourException>(() => t5.WriteBankA9B49A4(a5, 0x1000));
        Assert.Equal(1u, a5.Counter2C);
        Assert.Equal(1u, t5.NodeCount);
        Assert.Null(t5.Find(1002));
    }

    [Fact]
    public void S6_AGrowthFailureOnAnExistingNodeDeletesTheWholeNode()
    {
        // emu_media.py S6 (0x9B4FA4..0x9B5094): bank A wrote id 5; bank B writes id 5 and its items array cannot grow: the node, with A's item, is unlinked and freed ([BM+0x40] 0), ret 0x34, cleanup.
        var (table, memory) = NewTable();
        table.WriteBankA9B49A4(Bank((5, 0, 10)), 0x1000);
        int calls = 0;
        memory.AllocationFails = () => ++calls == 1;
        Assert.Throws<WwiseMissingBehaviourException>(() => table.WriteBankA9B49A4(Bank((5, 4, 20)), 0x2000));
        Assert.Equal(0u, table.NodeCount);
        Assert.Null(table.Find(5));
    }

    [Fact]
    public void S7_TheReleaseDropsAReferenceAndFreesTheNodeAtZero()
    {
        // emu_media.py S7 (0xA1ECBC -> 0x9B65A8): after a write and a lookup the refcount is 2; a release gives 1 (the node stays), a second removes it ([BM+0x40] 0); a lookup then gives (0, 0); a release of an absent
        // key changes nothing; a source block with a pointer ([obj+0x10] != 0) releases nothing (0xA1ECC0).
        var (table, _) = NewTable();
        table.WriteBankA9B49A4(Bank((21, 0, 10)), 0x1000);
        table.LookupA1EC54(21, 0, 0);
        Assert.Equal(2, table.Find(21)!.RefCount1C);
        table.ReleaseA1ECBC(21, 0x08001000);
        Assert.Equal(2, table.Find(21)!.RefCount1C);
        table.ReleaseA1ECBC(21, 0);
        Assert.Equal(1, table.Find(21)!.RefCount1C);
        Assert.Equal(1u, table.NodeCount);
        table.ReleaseA1ECBC(21, 0);
        Assert.Null(table.Find(21));
        Assert.Equal(0u, table.NodeCount);
        var after = table.LookupA1EC54(21, 0, 0);
        Assert.Equal((0u, 0u), (after.Data, after.Size));
        table.ReleaseA1ECBC(21, 0);
        Assert.Equal(0u, table.NodeCount);
        var (empty, _) = NewTable();
        Assert.Equal((0u, 0u), (empty.LookupA1EC54(1, 0, 0).Data, empty.LookupA1EC54(1, 0, 0).Size));    // an empty table: [BM+0x38] == 0 (0x9BB35C..0x9BB364)
    }

    // ------------------------------------------------------------------ the loader (emu_loader.py)

    private static byte[] Chunk(string tag, byte[] body)
    {
        var b = new List<byte>();
        b.AddRange(System.Text.Encoding.ASCII.GetBytes(tag));
        b.AddRange(BitConverter.GetBytes((uint)body.Length));
        b.AddRange(body);
        return b.ToArray();
    }

    private static byte[] Cat(params byte[][] parts) => parts.SelectMany(x => x).ToArray();

    private static readonly byte[] Bkhd = Chunk("BKHD", new byte[16]);
    private static readonly byte[] Didx3 = Cat(
        BitConverter.GetBytes(1001u), BitConverter.GetBytes(0u), BitConverter.GetBytes(10u),
        BitConverter.GetBytes(1002u), BitConverter.GetBytes(10u), BitConverter.GetBytes(20u),
        BitConverter.GetBytes(1003u), BitConverter.GetBytes(30u), BitConverter.GetBytes(5u));
    private static readonly byte[] Data35 = Enumerable.Range(0, 35).Select(i => (byte)i).ToArray();

    private sealed class LoaderRig
    {
        public readonly WwiseBankMemory Memory = new() { PoolCreateResult = 119, PoolCheckResult = 1, PoolAttributes = 0, PoolBlockSize = 0 };
        public readonly WwiseMediaTable Table;
        public readonly WwiseBankLoader Loader;
        public readonly WwiseMediaBank Bank = new() { Id = 0x1234 };
        public readonly List<string> Handled = new();
        public int HandlerResult = 1, BkhdResult = 1;
        public LoaderRig()
        {
            Table = new WwiseMediaTable(Memory);
            Loader = new WwiseBankLoader(Table)
            {
                ReadBkhd = (_, _) => BkhdResult,
                ChunkHandler = chunk => { Handled.Add(chunk.FourCc); chunk.Stream.Skip(chunk.Size, out _); return HandlerResult; },   // the stand-in consumes the chunk body as the engine's handlers do
            };
        }

        public int Run(byte[] file, out WwiseMemoryBankStream stream)
        {
            stream = new WwiseMemoryBankStream(file);
            return Loader.LoadMode0(stream, Bank);
        }

        public int Run(byte[] file) => Run(file, out _);
    }

    [Fact]
    public void L1_TheLoaderReadsDidxAndDataAndRunsTheWriter()
    {
        // emu_loader.py L1 (0x9B74D8 type 0 / mode 0): BKHD, DIDX (3 entries), DATA (35 bytes); the pool is created with (0, 35, 35, 9, 0x10) (0x9B7C88), checked and its attributes read (bit 3 clear: the chunk size is
        // allocated from it); ret 1, the stream closed once, 3 nodes in 29 buckets; bank: id 0x1234, [+4] = 2, DATA and DIDX buffers, [+0x1C] = 35, [+0x24] = the pool id, [+0x28] = 1, [+0x2C] = [+0x30] = 3, [+0x48] = 1,
        // [+0x50] = 3.
        var rig = new LoaderRig();
        Assert.Equal(1, rig.Run(Cat(Bkhd, Chunk("DIDX", Didx3), Chunk("DATA", Data35)), out var stream));
        Assert.True(stream.Closed);
        Assert.Equal(29u, rig.Table.BucketCount);
        Assert.Equal(3u, rig.Table.NodeCount);
        var b = rig.Bank;
        Assert.Equal((2, 35u, 119, 1, 3u, 3u, 1, 3), ((int)b.Flags4, b.DataSize1C, b.PoolId24, (int)b.PoolFlag28, b.Counter2C, b.Count30, b.RefCount48, (int)b.Flags50));
        Assert.NotEqual(0u, b.DataBuffer14);
        Assert.NotNull(b.Didx18);
        var hit = rig.Table.LookupA1EC54(1002, 0, 0);
        Assert.Equal(b.DataBuffer14 + 10, hit.Data);                                // DATA + offset
        Assert.Equal(20u, hit.Size);
        Assert.Equal(Data35.Skip(10).Take(20).ToArray(), rig.Memory.Block(b.DataBuffer14).AsSpan(10, 20).ToArray());   // the bytes read into the DATA buffer
    }

    [Fact]
    public void L2_WithPoolAttributesBit3SetTheChunkTakesOneBlockWhenItFits()
    {
        // emu_loader.py L2: attributes 8 and a block size of 64 (>= 35): 0xA7AA9C then 0xA7A9FC (one block), ret 1, the same bank and table. L3: block size 16 (< 35): [bank+0x14] stays 0 (0x9B7BF0 bhi 0x9B7C08),
        // ret 0x34, [bank+0x1C] 0, nothing written, the table empty.
        var rig = new LoaderRig();
        rig.Memory.PoolAttributes = 8; rig.Memory.PoolBlockSize = 64;
        Assert.Equal(1, rig.Run(Cat(Bkhd, Chunk("DIDX", Didx3), Chunk("DATA", Data35))));
        Assert.Equal(3u, rig.Table.NodeCount);
        Assert.Equal(35u, rig.Bank.DataSize1C);
        var small = new LoaderRig();
        small.Memory.PoolAttributes = 8; small.Memory.PoolBlockSize = 16;
        Assert.Equal(0x34, small.Run(Cat(Bkhd, Chunk("DIDX", Didx3), Chunk("DATA", Data35))));
        Assert.Equal(0u, small.Bank.DataBuffer14);
        Assert.Equal((0u, 0u, 0u), (small.Bank.DataSize1C, small.Bank.Counter2C, small.Table.NodeCount));
        Assert.Equal(3u, small.Bank.Count30);
    }

    [Fact]
    public void L4_ADataChunkBeforeTheDidxDoesNotRunTheWriterAndASecondDidxReplacesTheFirst()
    {
        // emu_loader.py L4: DATA then DIDX: ret 1, [bank+0x30] = 3 but nothing is written (the writer runs at DATA time, when the count was 0), table empty, [bank+0x2C] 0.
        // L5: DIDX (3 entries), DIDX (1 entry), DATA: [bank+0x2C] is still 0 when the second DIDX arrives, so it replaces the first (0x9B78EC): [bank+0x30] 1, one node.
        var late = new LoaderRig();
        Assert.Equal(1, late.Run(Cat(Bkhd, Chunk("DATA", Data35), Chunk("DIDX", Didx3))));
        Assert.Equal((0u, 3u, 0u), (late.Bank.Counter2C, late.Bank.Count30, late.Table.NodeCount));
        var twice = new LoaderRig();
        Assert.Equal(1, twice.Run(Cat(Bkhd, Chunk("DIDX", Didx3), Chunk("DIDX", Didx3.Take(12).ToArray()), Chunk("DATA", Data35))));
        Assert.Equal((1u, 1u, 1u), (twice.Bank.Counter2C, twice.Bank.Count30, twice.Table.NodeCount));
    }

    [Fact]
    public void L6_OtherChunksGoToTheirHandlersAndAnUnknownOneIsSkippedBySize()
    {
        // emu_loader.py L6: an unknown 'ABCD' chunk is skipped (0x9B78B8), HIRC then INIT reach their handlers (0x9B7AB0, 0x9B7AA0) and the loader goes on: ret 1, handled [HIRC, INIT], 3 nodes.
        // L7: a zero-size STMG is skipped without a handler (0x9B7AC8). L8: a handler result of 9 is returned (0x9B79E8).
        var rig = new LoaderRig();
        Assert.Equal(1, rig.Run(Cat(Bkhd, Chunk("ABCD", new byte[3]), Chunk("HIRC", new byte[4]), Chunk("INIT", new byte[2]), Chunk("DIDX", Didx3), Chunk("DATA", Data35))));
        Assert.Equal(new[] { "HIRC", "INIT" }, rig.Handled);
        Assert.Equal(3u, rig.Table.NodeCount);
        var stmg = new LoaderRig();
        Assert.Equal(1, stmg.Run(Cat(Bkhd, Chunk("STMG", Array.Empty<byte>()), Chunk("DIDX", Didx3), Chunk("DATA", Data35))));
        Assert.Empty(stmg.Handled);
        var failing = new LoaderRig { HandlerResult = 9 };
        Assert.Equal(9, failing.Run(Cat(Bkhd, Chunk("HIRC", new byte[4]), Chunk("DIDX", Didx3))));
        Assert.Equal(new[] { "HIRC" }, failing.Handled);
        Assert.Equal(0u, failing.Table.NodeCount);
    }

    [Fact]
    public void L9_PoolFailuresAndShortReadsReturnTheEnginesCodes()
    {
        // emu_loader.py: L9 the pool create returns -1: 0x34 (0x9B7C90..0x9B7C94), [bank+0x24] stays -1; L10 the pool check returns 5: ret 5, [bank+0x24] = 119 and [bank+0x28] = 1 are already stored;
        // L11 BKHD returns 9: ret 9; L12 a 5-byte header: 7 (0x9B7810); L13 a DATA chunk with 20 of its 35 bytes: 7 (0x9B7C48), the buffer and size stored; L17 the file ends after BKHD: 1; every run closes the stream.
        var create = new LoaderRig();
        create.Memory.PoolCreateResult = -1;
        Assert.Equal(0x34, create.Run(Cat(Bkhd, Chunk("DIDX", Didx3), Chunk("DATA", Data35)), out var s9));
        Assert.True(s9.Closed);
        Assert.Equal(-1, create.Bank.PoolId24);
        var check = new LoaderRig();
        check.Memory.PoolCheckResult = 5;
        Assert.Equal(5, check.Run(Cat(Bkhd, Chunk("DIDX", Didx3), Chunk("DATA", Data35))));
        Assert.Equal((119, 1), (check.Bank.PoolId24, (int)check.Bank.PoolFlag28));
        var bkhd = new LoaderRig { BkhdResult = 9 };
        Assert.Equal(9, bkhd.Run(Cat(Bkhd, Chunk("DIDX", Didx3)), out var s11));
        Assert.True(s11.Closed);
        Assert.Equal(7, new LoaderRig().Run(Cat(Bkhd, new byte[] { (byte)'D', (byte)'I', (byte)'D', (byte)'X', (byte)'1' })));
        var shortData = new LoaderRig();
        Assert.Equal(7, shortData.Run(Cat(Bkhd, Chunk("DIDX", Didx3), System.Text.Encoding.ASCII.GetBytes("DATA"), BitConverter.GetBytes(35u), new byte[20])));
        Assert.Equal((35u, 0u), (shortData.Bank.DataSize1C, shortData.Bank.Counter2C));
        Assert.Equal(1, new LoaderRig().Run(Bkhd));
    }

    [Fact]
    public void L14_ADataChunkOfSizeZeroStillRunsTheWriterAndADidxOfSizeNotAMultipleOf12LeavesItsRemainderUnread()
    {
        // emu_loader.py L14: DATA size 0: nothing is read, the writer runs on [bank+0x14] = 0 (0x9B7A08..0x9B7A34): ret 1, 3 nodes, [bank+0x2C] 3, no DATA buffer. L18: one DIDX entry with id 0 then a 4-byte DATA: ret 1, no node,
        // [bank+0x2C] = [bank+0x30] = 1. L15: a DIDX chunk of 30 bytes reads only 2 entries (n = 30 / 12, 0x9B78FC) and leaves 6 bytes: the next header read straddles them and the loader returns 7.
        var zero = new LoaderRig();
        Assert.Equal(1, zero.Run(Cat(Bkhd, Chunk("DIDX", Didx3), Chunk("DATA", Array.Empty<byte>()))));
        Assert.Equal((3u, 3u, 0u), (zero.Table.NodeCount, zero.Bank.Counter2C, zero.Bank.DataBuffer14));
        var idZero = new LoaderRig();
        Assert.Equal(1, idZero.Run(Cat(Bkhd, Chunk("DIDX", Cat(BitConverter.GetBytes(0u), BitConverter.GetBytes(0u), BitConverter.GetBytes(4u))), Chunk("DATA", System.Text.Encoding.ASCII.GetBytes("abcd")))));
        Assert.Equal((0u, 1u, 1u), (idZero.Table.NodeCount, idZero.Bank.Counter2C, idZero.Bank.Count30));
        var odd = new LoaderRig();
        Assert.Equal(7, odd.Run(Cat(Bkhd, Chunk("DIDX", Cat(Didx3.Take(24).ToArray(), new byte[6])), Chunk("DATA", Data35))));
        Assert.Equal(2u, odd.Bank.Count30);
    }

    [Theory]
    [InlineData(1, 0x34, false)]    // the DIDX allocation (oracle allocation #2; #1 there is the bank object, which the caller makes): 0x34, [bank+0x18] = 0
    [InlineData(2, 0x34, true)]     // the DATA buffer from the pool (oracle #3): 0x34, [bank+0x14] = 0
    public void L16_AnAllocationFailureReturns0x34(int failing, int expected, bool didxLoaded)
    {
        var rig = new LoaderRig();
        int calls = 0;
        rig.Memory.AllocationFails = () => ++calls == failing;
        Assert.Equal(expected, rig.Run(Cat(Bkhd, Chunk("DIDX", Didx3), Chunk("DATA", Data35))));
        Assert.Equal(didxLoaded, rig.Bank.Didx18 is not null);
        Assert.Equal(0u, rig.Bank.DataBuffer14);
        Assert.Equal(0u, rig.Table.NodeCount);
    }

    [Fact]
    public void L16_AWriterAllocationFailureInsideTheLoaderReachesTheUnadoptedCleanup()
    {
        // emu_loader.py L16 #4: the first allocation of the writer (the bucket array) fails: ret 0x34 and the cleanup 0x9B45D8 runs once; the C# stops at that unadopted body.
        var rig = new LoaderRig();
        int calls = 0;
        rig.Memory.AllocationFails = () => ++calls == 3;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Run(Cat(Bkhd, Chunk("DIDX", Didx3), Chunk("DATA", Data35))));
        Assert.Equal(0u, rig.Table.BucketCount);
    }

    [Fact]
    public void TheLoaderNeedsItsUnreadBodiesAndOnlyReadsTypeZeroModeZero()
    {
        // BKHD (0x9B21F4), the chunk bodies and the pool functions are not adopted: reaching one unset throws, never defaults.
        var rig = new LoaderRig();
        rig.Loader.ReadBkhd = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => rig.Run(Bkhd));
        var chunk = new LoaderRig();
        chunk.Loader.ChunkHandler = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => chunk.Run(Cat(Bkhd, Chunk("HIRC", new byte[4]))));
        var pool = new LoaderRig();
        pool.Memory.PoolAttributes = null;
        Assert.Throws<WwiseMissingBehaviourException>(() => pool.Run(Cat(Bkhd, Chunk("DIDX", Didx3), Chunk("DATA", Data35))));
    }

    // ------------------------------------------------------------------ the PBI side (0xA02924, Term)

    [Fact]
    public void ThePbiStoresThePairAndTheBankThroughTheLimitersInsertAndReleasesThemInTerm()
    {
        // C31.1 R1.4, R1.13: 0xA0285C -> 0xA04D48 (returns 1) -> 0xA1EC54([pbi+0x150], pbi+0x1DC, pbi+0x1E0, pbi+0x108): the pair is (DATA + offset, size) and pbi+0x108 the bank; Term (0xA02AD8..0xA02B14) releases the
        // media (0xA1ECBC), zeroes pbi+0x1DC (not 0x1E0) and runs the bank's vt+0 (0x9B47D8: [bank+0x48]-- under the lock), zeroing pbi+0x108. The constructor stores 0 in all three (R1.1).
        var (table, _) = NewTable();
        var bank = Bank((12345, 16, 64));
        table.WriteBankA9B49A4(bank, Base);
        var p = new WwisePlayInitParams { PlayingId = 5, TargetNodeId = 1 };
        var pbi = new WwisePlayingInstance(p, 1, new WwiseSourceDescriptor(WwiseSourceFactory.VorbisPlugin, 1, 12345, 0, 0), new byte[0x44], null, continuous: false);
        Assert.Equal((0u, 0u, null), (pbi.Word1DC, pbi.Word1E0, (WwiseMediaBank?)pbi.Obj108));
        WwisePbiMedia.StoreSourceInfoA02924(pbi, table);
        Assert.Equal((Base + 16, 64u), (pbi.Word1DC, pbi.Word1E0));
        Assert.Same(bank, pbi.Obj108);
        Assert.Equal(2, bank.RefCount48);                                           // the pick took a reference (0x9BB2A8..0x9BB2D0)
        Assert.Equal(2, table.Find(12345)!.RefCount1C);
        bank.RefCount48 = 2;
        WwisePbiMedia.ReleaseInTerm(pbi, table);
        Assert.Equal((0u, 64u), (pbi.Word1DC, pbi.Word1E0));                         // +0x1E0 is kept
        Assert.Null(pbi.Obj108);
        Assert.Equal(1, bank.RefCount48);
        Assert.Equal(1, table.Find(12345)!.RefCount1C);
        // a miss stores (0, 0) and leaves pbi+0x108 (0xA1EC74..0xA1ECB4)
        var other = new WwisePlayingInstance(p, 1, new WwiseSourceDescriptor(WwiseSourceFactory.VorbisPlugin, 1, 777, 0, 0), new byte[0x44], null, continuous: false) { Word1DC = 9, Word1E0 = 9 };
        WwisePbiMedia.StoreSourceInfoA02924(other, table);
        Assert.Equal((0u, 0u, null), (other.Word1DC, other.Word1E0, (WwiseMediaBank?)other.Obj108));
    }

    [Fact]
    public void TheLimitersInsertIsTheEntryOfTheLookup()
    {
        // The entry (not wired to production yet, C30.W parked): WwisePlaybackLimiter.InsertPbiA0285C (0xA0285C) runs 0xA04D48 and then the lookup through MediaTable; without a table it stops (WwiseMissingBehaviourException).
        var (table, _) = NewTable();
        table.WriteBankA9B49A4(Bank((12345, 16, 64)), Base);
        var node = new WwiseSoundNode(1, "t.bnk", new WwiseNodeParams(0, 0, 0, new Dictionary<byte, uint>(), new Dictionary<byte, (float, float)>(), Array.Empty<WwiseRtpc>(),
            Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>()), WwiseSourceFactory.VorbisPlugin, 1, 12345, 0, 0);
        var limiter = new WwisePlaybackLimiter(_ => node)
        {
            RtpcSubscribeA19ECC = (_, _, _) => { },
            RegisterPlayingIdA04D48 = _ => { },
            MediaTable = table,
        };
        var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 5, TargetNodeId = 1 }, 1, WwiseSourceDescriptor.FromSound(node), new byte[0x44], null, continuous: false) { NodeE0 = node };
        Assert.Equal(1, limiter.InsertPbiA0285C(node, pbi));
        Assert.Equal((Base + 16, 64u), (pbi.Word1DC, pbi.Word1E0));
        limiter.MediaTable = null;
        var again = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 6, TargetNodeId = 1 }, 1, WwiseSourceDescriptor.FromSound(node), new byte[0x44], null, continuous: false) { NodeE0 = node };
        Assert.Throws<WwiseMissingBehaviourException>(() => limiter.InsertPbiA0285C(node, again));
    }

    [Fact]
    public void TheBankObjectsVt0DecrementsAndStopsAtTheUnreadUnloadPath()
    {
        // 0x9B47D8 (the word at 0x101C028): [bank+0x48]-- (0x9B4800..0x9B4814); a result above 0 returns (0x9B481C ble not taken); zero or below continues at 0x9B4830, which no row reads.
        var bank = new WwiseMediaBank { RefCount48 = 2 };
        bank.ReleaseVt0A9B47D8();
        Assert.Equal(1, bank.RefCount48);
        Assert.Throws<WwiseMissingBehaviourException>(() => bank.ReleaseVt0A9B47D8());
        Assert.Equal(0, bank.RefCount48);
    }
}
