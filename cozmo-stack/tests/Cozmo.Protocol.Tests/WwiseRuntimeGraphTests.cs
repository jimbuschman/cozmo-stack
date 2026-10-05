// fidelity: M6-001, M6-009, M6-025
using System.Text;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The rig of the runtime-graph tests (B-M6b-4 batch 6c, C44.1): the loader with the stand-ins for the callees the inventory does not read, and the row text the oracle
/// (re-analysis/tools/emu/emu_graph.py) prints for the engine's objects. The stand-ins are TEST DOUBLES of unread bodies, not behaviour: RanSeq / Switch / Layer
/// are node shells that run the NodeBase the rows name (C), State, Attenuation, LFO, music and FX inits do nothing, and the bus list callbacks return (the host states
/// the global lists empty).
/// </summary>
internal static class GraphRig
{
    /// <summary>The shipped banks in the order emu_graph.py loads them.</summary>
    public static readonly string[] BankFiles = { "Init.bnk", "English(US)/Cozmo.bnk", "SFX.bnk", "UI.bnk", "Dev_Debug.bnk", "Music.bnk" };

    public static string? FindMeta()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var candidate = Path.Combine(d.FullName, "re-analysis", "obb", "sound_meta");
            if (Directory.Exists(candidate)) return candidate;
            d = d.Parent;
        }
        return null;
    }

    public static byte[] ReadBank(string meta, string name) => File.ReadAllBytes(Path.Combine(meta, name.Replace('/', Path.DirectorySeparatorChar)));

    public sealed class Rig
    {
        public required WwiseRuntimeGraph Graph { get; init; }
        public required WwiseGraphSeams Seams { get; init; }
        public required WwiseRtpcStore? Rtpc { get; init; }
        /// <summary>The objects the release tail was called for, with the error code that made the load fail.</summary>
        public required List<(WwiseRegistryObject Object, int Code)> Destroyed { get; init; }
    }

    public static Rig NewRig(byte fill, uint rate, ushort floor, bool manager = true, Action<WwiseGraphSeams>? tweak = null, bool listsEmpty = true)
    {
        WwiseRuntimeGraph? g = null;
        var rtpc = manager ? new WwiseRtpcStore() : null;
        var destroyed = new List<(WwiseRegistryObject Object, int Code)>();
        var seams = new WwiseGraphSeams
        {
            UnreadType = _ => 1,
            CreateRanSeq = id => g!.CreateNodeShell(id, WwiseNodeCategory44.Sound),
            CreateSwitch = id => g!.CreateNodeShell(id, WwiseNodeCategory44.Sound),
            CreateLayer = id => g!.CreateNodeShell(id, WwiseNodeCategory44.Sound),
            InitRanSeq = (n, o) => ContainerInit(g!, n, o),
            InitSwitch = (n, o) => ContainerInit(g!, n, o),
            InitLayer = (n, o) => ContainerInit(g!, n, o),
            CreateFxCustom = id => new WwiseRuntimeFx { Id = id, Custom = true },
            InitFx = (_, _) => 1,
            CreateAction = (type, id) => new WwiseRuntimeAction { Id = id, Type20 = type },
            ActionClassInit = (_, _) => 1,
            StateChunk = (_, _) => 1,
            RtpcAfterAdd = (_, _) => 1,
            BusVtE4 = _ => { },
            A40FF8 = _ => { },
            A4454C = _ => { },
            Bit3A9C62AC = _ => { },
            DestroyTail = (o, code) => { destroyed.Add((o, code)); DestroyTailDouble(o); },
        };
        tweak?.Invoke(seams);
        g = new WwiseRuntimeGraph(new WwiseGraphHostInputs(fill, rate, floor, false, listsEmpty, listsEmpty), seams, rtpc);
        return new Rig { Graph = g, Seams = seams, Rtpc = rtpc, Destroyed = destroyed };
    }

    /// <summary>
    /// A TEST DOUBLE of the tail of the release <c>0x9F500C</c> (<c>vt+0x18</c>, <c>0x9F4F28</c>, the unlink from the parent), which the inventory does not read: it does what the engine's registry shows after a failed load in
    /// emu_graph.py (a failed node leaves its output bus's child array and its parent's child array and each owner gives its reference back; a failed Event gives back the references on its actions).
    /// </summary>
    private static void DestroyTailDouble(WwiseRegistryObject o)
    {
        if (o is WwiseRoutingNode n)
        {
            if (n.OutputBus is { } ob) { (n.IsBus ? ob.BusChildren58 : ob.NonBusChildren48).Remove(n); ob.RefCount0C--; }
            if (n.Parent is { } p) { p.Children5C.Remove(n); p.RefCount0C--; }
        }
        else if (o is WwiseRuntimeEvent e)
        {
            for (var a = e.FirstAction10; a is not null; a = a.Next10) a.RefCount0C--;
        }
    }

    /// <summary>
    /// A TEST DOUBLE of the RanSeq / Switch / Layer init (<c>0xA0828C</c>, <c>0xA2F1D0</c>, <c>0x9D24D4</c>, unread): the NodeBase of the rows (C), then each child id of the container's list is linked to it: the child's
    /// <c>[+0x34]</c> is the container and the container's count goes up once per child (what the engine's registry shows after its own containers loaded, emu_graph.py; the arrays themselves are not compared).
    /// </summary>
    private static int ContainerInit(WwiseRuntimeGraph g, WwiseRoutingNode n, WwiseObject o)
    {
        var parsed = WwiseHierarchy.TryRead(o, out var problem, requireWholeBody: false) ?? throw new InvalidOperationException(problem);
        int r = g.NodeBase9F6EF8(n, parsed.Params, partial: false);
        if (r != 1) return r;
        foreach (uint cid in parsed.Children)
        {
            var child = g.Registry.Find(WwiseRegistryTable.A, cid) as WwiseRoutingNode;
            if (child is null) return 0xF;
            child.Parent = n;
            n.AddRef9F1CBC();
        }
        return 1;
    }

    // ------------------------------------------------------------------ the row text (the same as emu_graph.py prints)

    private static string Bundle(WwiseParamBundle? b)
    {
        if (b is null) return "-";
        var sb = new StringBuilder("[");
        for (int i = 0; i < b.Ids.Count; i++)
        {
            if (i != 0) sb.Append(',');
            sb.Append($"{b.Ids[i]:X2}={b.FirstWords[i]:X8}");
            if (b.SecondWords is { } s) sb.Append($"/{s[i]:X8}");
        }
        return sb.Append(']').ToString();
    }

    private static string Reg(WwiseRtpcRegistry? r) => r is null ? "-" : $"{r.MaskA:X16}/{r.Cache:X16}/{r.Byte1C}";

    private static string Fx(WwiseFxChunk? c)
        => c is null ? "-" : $"v{(uint)c.Version0} i{string.Join(",", c.Ids.Select(x => x.ToString("X8")))} r{string.Join(",", c.Rendered.Select(x => x.ToString("X2")))} " +
                              $"s{string.Join(",", c.Share.Select(x => x.ToString("X2")))} y{c.Bypass:X2}";

    private static string Id(WwiseRoutingNode? n) => n is null ? "-" : n.Id.ToString();

    public static string NodeRow(int typ, WwiseRoutingNode n)
    {
        bool bus = typ == 8;
        var sb = new StringBuilder();
        sb.Append($"{typ} {n.Id} r{n.RefCount0C} p{Id(n.Parent)} o{Id(n.OutputBus)} x{n.Word40:X8} d{n.Dword44:X8} s{(bus ? "-" : n.Word58.ToString("X4"))} b{Bundle(n.BaseBundle3C)} ");
        sb.Append($"g{(bus ? "-" : Bundle(n.RangedBundle4C))} a{(bus || n.AuxIds54 is null ? "-" : string.Join(",", n.AuxIds54.Select(x => x.ToString("X8"))))} ");
        sb.Append($"f{Fx(n.Fx28)} h{Reg(n.Registry14)} j{(bus ? "-" : Reg(n.Registry20))} k{(bus ? Reg(n.RegistryC8) : "-")}");
        if (typ == 2) sb.Append($" S{n.SourceId5C:X8},{n.SourceId60:X8},{n.InMemorySize64:X8},{n.Word68:X8},{n.Field6C:X8},{n.Plugin70:X8}");
        else if (typ == 7) sb.Append($" C{n.Children5C.Count}/{n.ChildCapacity64}:{string.Join(",", n.Children5C.Select(c => c.Id.ToString()))}");
        else if (bus)
        {
            string ducks = string.Join(";", n.DuckList70.Select(d => $"{d.TargetBusId}:{BitConverter.SingleToUInt32Bits(d.Volume):X8}:{d.FadeOutMs:X8}:{d.FadeInMs:X8}:{d.Curve:X8}:{d.TargetProperty:X8}"));
            string a = string.Join(",", n.NonBusChildren48.Select(c => c.Id.ToString()));
            string b = string.Join(",", n.BusChildren58.Select(c => c.Id.ToString()));
            sb.Append($" U{n.Word68:X8},{BitConverter.SingleToUInt32Bits(n.MaxDuck6C):X8},{n.RecoverySamples64},{n.ByteCC:X2} B{(a.Length == 0 ? "-" : a)}/{(b.Length == 0 ? "-" : b)} D{(ducks.Length == 0 ? "-" : ducks)}");
        }
        return sb.ToString();
    }

    public static string EventRow(WwiseRuntimeEvent e)
    {
        var chain = new List<string>();
        for (var a = e.FirstAction10; a is not null; a = a.Next10) chain.Add(a.Id.ToString());
        return $"4 {e.Id} r{e.RefCount0C} A{(chain.Count == 0 ? "-" : string.Join(",", chain))}";
    }

    public static string ActionRow(WwiseRuntimeAction a)
    {
        bool play = (a.Type20 >> 8) == 4;
        return $"3 {a.Id} r{a.RefCount0C} t{a.Type20:X4} f{(play ? a.Byte22.ToString("X2") : "-")} k{(play ? a.BankId24.ToString("X8") : "-")} c{a.TargetId1C:X8} b{Bundle(a.BaseBundle14)} g{Bundle(a.RangedBundle18)}";
    }

    /// <summary>(type, id, text) for every object of tables A, B, Event and Action. <paramref name="types"/> labels table A's nodes with their HIRC type (the first object of that id in the banks).</summary>
    public static List<(int Type, uint Id, string Text)> Rows(WwiseRuntimeGraph g, IReadOnlyDictionary<uint, byte> types)
    {
        var rows = new List<(int, uint, string)>();
        foreach (var o in g.Registry.Objects(WwiseRegistryTable.A).Cast<WwiseRoutingNode>())
        {
            int t = types.TryGetValue(o.Id, out var tv) ? tv : 2;
            rows.Add((t, o.Id, NodeRow(t, o)));
        }
        foreach (var o in g.Registry.Objects(WwiseRegistryTable.B).Cast<WwiseRoutingNode>()) rows.Add((8, o.Id, NodeRow(8, o)));
        foreach (var o in g.Registry.Objects(WwiseRegistryTable.Event).Cast<WwiseRuntimeEvent>()) rows.Add((4, o.Id, EventRow(o)));
        foreach (var o in g.Registry.Objects(WwiseRegistryTable.Action).Cast<WwiseRuntimeAction>()) rows.Add((3, o.Id, ActionRow(o)));
        return rows;
    }

    /// <summary>The master / second slots at 0x108D9B0, in the oracle's text.</summary>
    public static string MasterRow(WwiseRuntimeGraph g)
        => $"master={Id(g.MasterBuses.Master)} f8={g.MasterBuses.MasterField8:X8} second={Id(g.MasterBuses.Secondary)} f14={g.MasterBuses.SecondaryField14:X8}";

    public static uint Fnv(string text)
    {
        uint h = 0x811C9DC5;
        foreach (char c in text) h = unchecked((h ^ c) * 0x01000193);
        return h;
    }

    /// <summary>The (type, id) of every HIRC object of a bank file, first occurrence wins per id (the oracle's label map).</summary>
    public static void AddTypes(byte[] bank, Dictionary<uint, byte> types)
    {
        int off = 0;
        while (off + 8 <= bank.Length)
        {
            uint sz = BitConverter.ToUInt32(bank, off + 4);
            if (bank[off] == 'H' && bank[off + 1] == 'I' && bank[off + 2] == 'R' && bank[off + 3] == 'C')
            {
                uint n = BitConverter.ToUInt32(bank, off + 8);
                int p = off + 12;
                for (uint i = 0; i < n; i++)
                {
                    uint osz = BitConverter.ToUInt32(bank, p + 1);
                    types.TryAdd(BitConverter.ToUInt32(bank, p + 5), bank[p]);
                    p += 5 + (int)osz;
                }
            }
            off += 8 + (int)sz;
        }
    }
}

public class WwiseRuntimeGraphTests
{
    // ------------------------------------------------------------------ the engine's own HIRC load as the oracle (emu_graph.py)

    private static readonly (string Name, byte Fill, uint Rate, ushort Floor, string[] Banks)[] Passes =
    {
        ("full", 0x00, 48000, 0, GraphRig.BankFiles),
        ("pool", 0xFF, 48000, 0, GraphRig.BankFiles),
        ("init44100", 0x00, 44100, 50000, new[] { "Init.bnk" }),
        ("init22050", 0xFF, 22050, 0, new[] { "Init.bnk" }),
    };

    [Fact]
    public void M6_001_TheShippedBanksBuildTheGraphTheEnginesWalkerBuilds_FourPassesAndEveryObject()
    {
        // C44.1 A1..A12, B1..B6, C1..C12, D1..D14 against the engine: emu_graph.py runs the real chunk loop 0x9B74D8 and the HIRC walker 0x9B3260 with every handler it dispatches to over the shipped
        // banks (Init, Cozmo, SFX, UI, Dev_Debug, Music) and dumps its registry: one row per Sound / ActorMixer / RanSeq / Switch / Layer node, bus, Event and Action. The shipped chain (Event 188399711, Action
        // 859129412, Sound 957475640, ActorMixer 13023553, root 62050212 and the 15 buses) is compared field by field; every other object by the hash of its row.
        var meta = GraphRig.FindMeta();
        if (meta is null) { Assert.Fail("re-analysis/obb/sound_meta (the unpacked shipped banks) was not found above the test binaries"); return; }
        var oracle = WwiseRuntimeGraphOracle.Rows.Select(r => r.Split(' ', 3)).ToList();
        foreach (var (name, fill, rate, floor, banks) in Passes)
        {
            var rig = GraphRig.NewRig(fill, rate, floor);
            var types = new Dictionary<uint, byte>();
            var results = new List<string>();
            foreach (var b in banks)
            {
                var bytes = GraphRig.ReadBank(meta, b);
                GraphRig.AddTypes(bytes, types);
                results.Add($"{b}={rig.Graph.LoadBank(b, bytes)}");
            }
            var mine = GraphRig.Rows(rig.Graph, types);
            var expectedFull = new Dictionary<(int, uint), string>();
            var expectedHash = new Dictionary<(int, uint), uint>();
            string? expectedResults = null, expectedMaster = null;
            foreach (var p in oracle.Where(p => p[0] == name))
            {
                if (p[1] == "R") expectedResults = p[2];
                else if (p[1] == "M") expectedMaster = p[2];
                else if (p[1] == "F") { var f = p[2].Split(' ', 3); expectedFull[(int.Parse(f[0]), uint.Parse(f[1]))] = p[2]; }
                else { var h = p[2].Split(' '); expectedHash[(int.Parse(h[0]), uint.Parse(h[1]))] = Convert.ToUInt32(h[2], 16); }
            }
            Assert.Equal(expectedResults, string.Join(" ", results));
            Assert.Equal(expectedMaster, GraphRig.MasterRow(rig.Graph));
            Assert.Equal(expectedFull.Count + expectedHash.Count, mine.Count);
            var bad = new List<string>();
            foreach (var (t, id, text) in mine)
            {
                if (expectedFull.TryGetValue((t, id), out var full)) { if (full != text) bad.Add($"{name} engine: {full}\n{name} C#:     {text}"); }
                else if (expectedHash.TryGetValue((t, id), out var hash)) { if (hash != GraphRig.Fnv(text)) bad.Add($"{name} {t} {id}: hash differs; C# row: {text}"); }
                else bad.Add($"{name} {t} {id}: not in the engine's registry");
            }
            Assert.True(bad.Count == 0, $"{bad.Count} of {mine.Count} objects differ in pass {name}; first: {string.Join("\n", bad.Take(4))}");
        }
    }

    [Fact]
    public void M6_001_SyntheticBanksTheEngineLoaded_ResultCodesAndEveryObjectMatch()
    {
        // The scenarios of emu_graph.py (the engine loaded each synthetic bank: the abort codes 2 / 0xE / 0xF / 0x15 / 0x1F, the found-existing paths, the re-init of a source-plug-in Sound, the sorted child arrays,
        // the master / second bus slots, the ducks, the FX / aux / positioning / advanced bit math over 240 random node blocks and 120 random bus blocks, the delay conversion over 80 random Play actions).
        var rows = WwiseRuntimeGraphOracle.Scenarios.Select(r => r.Split(' ', 3)).ToList();
        var names = rows.Select(r => r[0]).Distinct().ToList();
        Assert.True(names.Count >= 40);
        var failures = new List<string>();
        foreach (var name in names)
        {
            var mine = rows.Where(r => r[0] == name).ToList();
            var p = mine.Single(r => r[1] == "P")[2].Split(' ');
            byte fill = byte.Parse(p[0]); uint rate = uint.Parse(p[1]); ushort floor = ushort.Parse(p[2]); bool mgr = p[3] == "1";
            var rig = GraphRig.NewRig(fill, rate, floor, mgr);
            var types = new Dictionary<uint, byte>();
            var results = new List<string>();
            foreach (var b in mine.Where(r => r[1] == "B"))
            {
                var bytes = Convert.FromHexString(b[2].Split(' ')[1]);
                GraphRig.AddTypes(bytes, types);
                results.Add(rig.Graph.LoadBank("b" + b[2].Split(' ')[0], bytes).ToString());
            }
            string expectedResults = mine.Single(r => r[1] == "R")[2];
            if (expectedResults != string.Join(" ", results)) { failures.Add($"{name}: engine results {expectedResults}, C# {string.Join(" ", results)}"); continue; }
            string expectedMaster = mine.Single(r => r[1] == "M")[2];
            if (expectedMaster != GraphRig.MasterRow(rig.Graph)) { failures.Add($"{name}: engine master slots {expectedMaster}, C# {GraphRig.MasterRow(rig.Graph)}"); continue; }
            var expected = mine.Where(r => r[1] == "F").Select(r => r[2]).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var got = GraphRig.Rows(rig.Graph, types).Select(r => r.Text).OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (!expected.SequenceEqual(got))
            {
                var onlyEngine = expected.Except(got).Take(2).ToList();
                var onlyMine = got.Except(expected).Take(2).ToList();
                failures.Add($"{name}: {expected.Count} engine rows, {got.Count} C# rows\n  engine only: {string.Join("\n               ", onlyEngine)}\n  C# only:     {string.Join("\n               ", onlyMine)}");
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(6)));
    }
}
