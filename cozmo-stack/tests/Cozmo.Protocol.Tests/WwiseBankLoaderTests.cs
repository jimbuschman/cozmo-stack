using System.Text;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-025 (C34.4 K1..K10, K12..K14, K16): the bank loader 0x9B74D8 with its chunk handlers (BKHD 0x9B21F4, DIDX / DATA, INIT 0x9B4048, ENVS 0x9B2988 / 0x9CC5A0, PLAT 0x9B2B08 / 0x9A6518, HIRC 0x9B3260 and its hook path),
/// the writer 0x9B49A4 with its cleanup 0x9B45D8, the registry 0xA68804, the pool functions and the reader. Every expected value in <see cref="BankLoaderOracle"/> is the engine's own output: re-analysis/tools/emu/emu_bankload.py runs the real
/// functions under Unicorn on the same inputs (the file bytes and parameters of <see cref="BankLoaderOracle.Specs"/>); none comes from running this implementation. The unread callees (STMG 0x9B0B14, STID 0x9B2410, the streamed DATA
/// 0x9B6C34, the file opens, 0xA57238, the music hook) are stand-ins on both sides, with the same effect (the chunk body consumed, the call logged).
/// </summary>
public class WwiseBankLoaderTests
{
    public static IEnumerable<object[]> Names() => BankLoaderOracle.Loads.Keys.Select(k => new object[] { k });

    private static string Hex(byte[]? b) => b is null ? "" : Convert.ToHexString(b).ToLowerInvariant();

    private sealed class Run
    {
        public WwiseBankMemory Memory = null!;
        public WwiseMediaTable Table = null!;
        public WwiseBankGlobals Globals = null!;
        public WwiseBankLoader Loader = null!;
        public List<string> Log = new();
        public int AllocCalls;
        public int Result;
        public WwiseMediaBank? Bank;
    }

    private static Run Execute(BankSpec spec)
    {
        var run = new Run();
        var file = Convert.FromHexString(spec.File);
        run.Memory = new WwiseBankMemory { MaxPools = spec.MaxPools };
        run.Memory.AllocationFails = () => { run.AllocCalls++; return spec.AllocFail != 0 && run.AllocCalls == spec.AllocFail; };
        run.Table = new WwiseMediaTable(run.Memory);
        run.Globals = new WwiseBankGlobals
        {
            Envs904Present = spec.Envs,
            EnvsTable = new WwiseEnvsTable(),
            PlatformString = spec.Platform,
            MediaPoolId14 = unchecked((int)spec.MediaPool),
            ExternalPluginLoadA57238 = name => run.Log.Add("plugin:" + (name ?? "None")),
        };
        for (int i = 0; i < 4; i++) run.Globals.XorKey[i] = spec.Xor[i];
        foreach (var id in spec.PluginsE8) run.Globals.PluginIdsE8.Add(id);
        foreach (var id in spec.PluginsDC) run.Globals.PluginIdsDC.Add(id);
        run.Loader = new WwiseBankLoader(run.Table, run.Globals);
        var loader = run.Loader;
        run.Globals.HircHook = null;                                              // the music-engine init has not run (explicit)
        if (spec.Hook is { } hook)
        {
            int next = 0;
            run.Globals.HircHook = (type, size, bank, id) =>
            {
                run.Log.Add($"hook:{type}/{size}");
                int result = next < hook.Length ? hook[next++] : 1;
                if (result == 1) loader.Reader.Skip9BBF9C(size, out _);       // a hook that continues has read the object itself: the stand-in consumes it (as the emulator's does)
                return result;
            };
        }
        loader.OpenReader = (reader, req, how) =>
        {
            run.Log.Add(how == WwiseReaderOpen.ByName9BC17C ? "open_name" : "open_id");
            reader.SetupMemory9BBB90(file, 0, (uint)file.Length);
            return spec.OpenRet;
        };
        loader.StmgA9B0B14 = reader => { reader.Skip9BBF9C((uint)spec.StmgSize, out _); run.Log.Add("stmg"); return spec.StmgRet; };
        loader.StidA9B2410 = (reader, size, bank) => { reader.Skip9BBF9C(size, out _); run.Log.Add("stid:" + size); return spec.StidRet; };
        loader.DataStreamedA9B6C34 = (bank, size, flag) => { loader.Reader.Skip9BBF9C(size, out _); run.Log.Add($"data9b6c34:{size}/{flag}"); return spec.DataStreamRet; };
        foreach (var (id, lang, flags50, word54) in spec.Registry)
            run.Table.Registry.Add(new WwiseMediaBank { Id = id, Language0C = lang, Flags4 = 0, PoolId24 = 0, Flags50 = flags50, Word54 = word54, RefCount48 = 1, Table = run.Table });
        var request = new WwiseBankLoadRequest
        {
            Id = spec.Id,
            HasName = spec.HasName,
            OpenFlags = 0x10,
            Type = spec.Type,
            PoolArg = unchecked((int)spec.PoolArg),
            LanguageOrMemory = spec.Lang,
            Memory = file,
            MemoryLength = (uint)file.Length,
            Mode = spec.Mode,
            AddRef = spec.AddRef,
            Flag12 = 0,
        };
        run.Result = loader.Load9B74D8(request, out run.Bank);
        return run;
    }

    internal static string BankString(WwiseMediaBank b)
        => $"id={b.Id} lang={b.Language0C} flags4={b.Flags4} data14={(b.DataBuffer14 != 0 ? 1 : 0)} didx18={(b.Didx18 is not null ? 1 : 0)} size1c={b.DataSize1C} pool24={unchecked((uint)b.PoolId24)} flag28={b.PoolFlag28} " +
           $"ctr2c={b.Counter2C} cnt30={b.Count30} w34={b.Word34} ref48={unchecked((uint)b.RefCount48)} w4c={unchecked((uint)b.Word4C)} flags50={b.Flags50} w54={b.Word54} cap44={b.HircCapacity44} cnt40={b.HircCount40}";

    internal static string TableString(WwiseMediaTable table)
    {
        var nodes = new List<(uint Key, int Ref, int Items, int Capacity, uint Last)>();
        foreach (uint id in new uint[] { 1, 2, 1001, 1002, 1003 })
            if (table.Find(id) is { } n) nodes.Add((n.Key, n.RefCount1C, n.Items.Count, n.ItemCapacity, n.LastId20));
        return $"buckets={table.BucketCount} count={table.NodeCount} nodes={string.Join(";", nodes.Select(n => $"{n.Key}/{n.Ref}/{n.Items}/{n.Capacity}/{n.Last}"))}";
    }

    private static string ExtraString(WwiseBankGlobals g)
    {
        var t = g.EnvsTable!;
        var sb = new StringBuilder("en=" + string.Join(",", t.Enabled.Select(x => (int)x)));
        for (int k = 0; k < 6; k++)
            sb.Append($" {k}:{t.Count[k]}/{t.Scaling[k]}/{(t.Count[k] != 0 ? Hex(t.Points[k]) : "")}");
        sb.Append(" plat=" + (g.PlatformString ?? "-"));
        return sb.ToString();
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void TheLoaderGivesTheEnginesResultBankTableAndCalls(string name)
    {
        // M6-025 K1..K10, K12..K14 (C34.4): emu_bankload.py scenario <name> on the real 0x9B74D8.
        var expected = BankLoaderOracle.Loads[name];
        var run = Execute(BankLoaderOracle.Specs[name]);
        Assert.Equal(expected.Ret, (uint)run.Result);
        Assert.Equal(expected.Bank is not null && expected.OutWritten && !expected.OutZero, run.Bank is not null);
        if (run.Bank is not null) Assert.Equal(expected.Bank, BankString(run.Bank));
        Assert.Equal(expected.Table, TableString(run.Table));
        Assert.Equal(expected.Flag64, run.Loader.BkhdAlignedFlag64);
        Assert.Equal(expected.ReaderLeft, run.Loader.Reader.Left8);
        Assert.Equal(expected.Log, run.Log);
        Assert.Equal(expected.Allocs, run.AllocCalls);
        Assert.Equal(expected.Extra, ExtraString(run.Globals));
    }

    // ------------------------------------------------------------------ K3: 0x9B2314

    public static IEnumerable<object[]> K3Names() => BankLoaderOracle.Chunk2314.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(K3Names))]
    public void TheMemoryChunkLoadGivesTheEnginesResults(string name)
    {
        // M6-025 K3 (C34.4): emu_bankload.py run_k3 on the real 0x9B2314(BM, size, _, bank) (no bl in the image reaches it; the oracle calls it directly).
        var (calls, dataLength, maxPools, allocFail, preset, poolOverride, steps) = BankLoaderOracle.Chunk2314[name];
        var memory = new WwiseBankMemory { MaxPools = maxPools };
        var table = new WwiseMediaTable(memory);
        var loader = new WwiseBankLoader(table);
        var data = new byte[dataLength];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)((i * 5 + 1) & 0xFF);
        loader.Reader.SetupMemory9BBB90(data, 0, (uint)data.Length);
        int shared = -1;
        if (preset is { } p) shared = memory.CreatePoolA7AC98(0, p.Size, p.Block, 9, 0x10);
        int allocCalls = 0;
        memory.AllocationFails = () => ++allocCalls == allocFail;
        WwiseMediaBank? bank = null;
        for (int i = 0; i < calls.Length; i++)
        {
            if (bank is null || preset is not null)
                bank = new WwiseMediaBank { Flags4 = 0, Flags50 = 0, RefCount48 = 0, PoolId24 = poolOverride >= 0 ? (int)poolOverride : shared, Table = table };
            int before = allocCalls;
            int r = loader.LoadMemoryChunkA9B2314((uint)calls[i], bank);
            int pool = bank.PoolId24;
            uint used = pool >= 0 && pool < maxPools ? memory.PoolUsed(pool) : 0;
            string buf = r == 1 && bank.DataBuffer14 != 0 && calls[i] != 0 ? Hex(memory.Block(bank.DataBuffer14).AsSpan(0, calls[i]).ToArray()) : "";
            Assert.Equal(steps[i], $"ret={r} {BankString(bank)} left={loader.Reader.Left8} allocs={allocCalls - before} used={used} buf={buf}");
        }
    }

    // ------------------------------------------------------------------ K16: the pool table

    public static IEnumerable<object[]> PoolNames() => BankLoaderOracle.Pools.Keys.Select(k => new object[] { k });

    private static string PoolDump(WwiseBankMemory memory, int pool)
    {
        var d = memory.DescriptorOrNull(pool);
        if (d is null || (d.BlockCount0 == 0 && d.BlockSize4 == 0)) return "-";
        var free = d.FreeList.Select(b => d.BlockSize4 != 0 ? ((b - d.Aligned0C) / d.BlockSize4).ToString() : "0");
        return $"[{d.BlockCount0}/{d.BlockSize4}/{d.Attr18}/{d.Flags1C}/{d.Align28}/{d.Used2C}/{d.Capacity30} free={string.Join(",", free)}]";
    }

    [Theory]
    [MemberData(nameof(PoolNames))]
    public void ThePoolTableGivesTheEnginesResults(string name)
    {
        // M6-025 K16 (C34.4): emu_bankload.py run_pools on the real 0xA7AC98, 0xA7AAE8, 0xA7A7C8, 0xA7AA9C, 0xA7A9FC and 0xA7AA48.
        var (maxPools, allocFail, ops, steps) = BankLoaderOracle.Pools[name];
        int calls = 0;
        var memory = new WwiseBankMemory { MaxPools = maxPools, AllocationFails = () => ++calls == allocFail };
        for (int i = 0; i < ops.Length; i++)
        {
            var a = ops[i].Split(':');
            int[] n = a.Skip(1).Select(int.Parse).ToArray();
            string res;
            switch (a[0])
            {
                case "create": res = $"id={memory.CreatePoolA7AC98(0, (uint)n[0], (uint)n[1], (uint)n[2], (uint)n[3])}"; break;
                case "check": res = $"r={memory.PoolCheckA7AAE8(n[0])}"; break;
                case "attrs": res = $"attr={memory.PoolAttributesA7A7C8(n[0])}"; break;
                case "bsize": res = $"bsize={memory.PoolBlockSizeA7AA9C(n[0])}"; break;
                case "pop":
                {
                    uint blk = memory.PopBlockA7A9FC(n[0]);
                    var d = memory.DescriptorOrNull(n[0])!;
                    res = "blk=" + (blk == 0 ? "null" : ((blk - d.Aligned0C) / Math.Max(d.BlockSize4, 1)).ToString());
                    break;
                }
                case "push":
                {
                    var d = memory.DescriptorOrNull(n[0])!;
                    memory.PushBlockA7AA48(n[0], d.Aligned0C + (uint)n[1] * d.BlockSize4);
                    res = "pushed";
                    break;
                }
                default: throw new InvalidOperationException(ops[i]);
            }
            Assert.Equal(steps[i], $"{res} {PoolDump(memory, 0)} {PoolDump(memory, 1)} {PoolDump(memory, 2)}");
        }
    }

    // ------------------------------------------------------------------ K15: the bank release 0x9B47D8

    public static IEnumerable<object[]> ReleaseNames() => BankLoaderOracle.Releases.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(ReleaseNames))]
    public void TheBankReleaseGivesTheEnginesResults(string name)
    {
        // M6-025 K15 (C34.4): emu_bankload.py run_release on the real 0x9B47D8(bank, force) of a bank the real loader built. DestroyPool's tail (0xA7AEC4 after its check) is unread: the oracle's stand-in only logs it, and the C# stops
        // there with WwiseMissingBehaviourException, so a release that reaches it is checked up to that call (the pool's used count after the block went back).
        var x = BankLoaderOracle.Releases[name];
        var run = Execute(BankLoaderOracle.Specs[x.Base]);
        var bank = run.Bank!;
        run.Log.Clear();
        if (x.Ref48 >= 0) bank.RefCount48 = unchecked((int)x.Ref48);
        if (x.W34 >= 0) bank.Word34 = (uint)x.W34;
        if (x.W4c >= 0) bank.Word4C = (int)x.W4c;
        if (x.Flags50 >= 0) bank.Flags50 = (byte)x.Flags50;
        run.Table.BankCallbackRecordA68150 = _ => run.Log.Add("a68150");
        int pool = bank.PoolId24;
        int freesBefore = run.Memory.FreeCalls;
        if (x.Log.Any(l => l.StartsWith("destroy_pool:")))
        {
            Assert.Throws<WwiseMissingBehaviourException>(() => run.Table.ReleaseBankA9B47D8(bank, x.Force != 0));
            Assert.Equal(x.PoolUsed, pool >= 0 && pool < run.Memory.MaxPools ? run.Memory.PoolUsed(pool) : 0u);
            Assert.Contains("destroy_pool:" + pool, x.Log);
            return;
        }
        run.Table.ReleaseBankA9B47D8(bank, x.Force != 0);
        Assert.Equal(x.FreedObject, bank.Freed);
        if (x.Bank is not null) Assert.Equal(x.Bank, BankString(bank));
        Assert.Equal(x.Table, TableString(run.Table));
        Assert.Equal(x.Log, run.Log);
        Assert.Equal(x.PoolUsed, pool >= 0 && pool < run.Memory.MaxPools ? run.Memory.PoolUsed(pool) : 0u);
        // the DIDX block of an owned DIDX goes back to the heap (the bank object itself is not an allocation the C# model keeps)
        Assert.Equal(x.FreedDidx, run.Memory.FreeCalls > freesBefore);
    }

    [Fact]
    public void UnsetHostStateThrowsInsteadOfYieldingPreInitEngineResults()
    {
        // The live engine's [0x108D904] (a manager pointer), the music hook [0x108D968], [0x108E358] and [[0x108D90C]+0x14] are written by inits no row reads: unset, they are named stops.
        var g = new WwiseBankGlobals();
        Assert.Throws<WwiseMissingBehaviourException>(() => g.Envs904Present);
        Assert.Throws<WwiseMissingBehaviourException>(() => g.HircHook);
        g.HircHook = null;
        Assert.Null(g.HircHook);
        Assert.Null(g.MediaPoolId14);
        Assert.Throws<WwiseMissingBehaviourException>(() => new WwiseBankMemory().MaxPools);
        Assert.Equal(1u, new WwisePoolDescriptor().Attr18);                      // 0xA7A694: the init sets [desc+0x18] = 1
    }

    [Fact]
    public void TheRtpcCurveBodyDefaultsToTheEnginesBinary32Function()
    {
        // C35.1: 0xA14E28 is adopted; the store's default IS the binary32 port (it no longer throws MISSING). event_volume 0xD2687048's curve (0,-1,interp 1)->(1,0,interp 4), scaling 2, at x = 0.5
        // is 0xC040B146 (-3.0108199) from the engine under Unicorn (re-analysis/tools/emu/emu_curve.py; research live-bodies-5 section 3).
        var curve = new WwiseRtpc(0xD2687048, 0, 1, 0, 0x0622369A, 2, new[] { (0f, -1f, 1u), (1f, 0f, 4u) });
        Assert.Equal(0xC040B146u, BitConverter.SingleToUInt32Bits(new WwiseRtpcStore().CurveA14E28(curve, 0.5f)));
    }
}
