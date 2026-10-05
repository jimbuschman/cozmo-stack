// fidelity: M6-001, M6-009, M6-025
using System.Buffers.Binary;

namespace Cozmo.Robot.Animation.Wwise;

// The runtime node/bus graph, built the way the engine's HIRC loaders build it (B-M6b-4 batch 6c). Source: re-analysis/inventory/M6-wwise-bank.md Correction C44.1 and
// re-analysis/research/20261005-B-M6b-4-hirc-graph-15.md with its Verification section (V1..V9, the construction list). Row ids in the comments (A1.., B1.., C1.., D1.., E1.., F1..) are that report's.
//
// Production entry. Engine: the HIRC walker 0x009B3260 (called from the chunk loop 0x9B74D8 at 0x9B7AC0). C# counterpart: WwiseRuntimeGraph.LoadBank -> LoadHirc, which a host calls once per bank in the
// engine's bank order (Init.bnk first, F1). The host wires the graph into the play path with RuntimeNodeOf (WwisePlaybackLimiter.RuntimeNodeOf, WwisePlayPath.RuntimeNodeOf) and shares the reference count
// with BindReferenceCounts. No production code calls LoadBank yet (the bank-load wiring, C30.W, is outside this batch); the tests drive it.
//
// What this file does not decide. Every callee the inventory does not read is a REQUIRED SEAM (WwiseGraphSeams) that throws WwiseMissingBehaviourException when unset: the creators and inits of RanSeq, Switch, Layer,
// State, Attenuation, FxCustom and the music types (A10, A11), the init 0x9CE3B8 (D14), the State chunk (C11), the RTPC after-add step (L5-08), the release tail of 0x9F500C, the non-Play action classes, and
// the bus callbacks 0xA40E6C, 0xA4131C, 0xA40FF8, 0xA4454C, 0x9C62AC, vt+0xE4 (0x9C0A70). Where the verifier showed a callee returns at once while a global list is empty (0xA40E6C, 0xA4131C), the host states the
// list is empty as an explicit input (WwiseGraphHostInputs.ListEmptyA40E6C / ListEmptyA4131C); nothing is skipped silently.

/// <summary>
/// The host values the engine reads from memory this stack does not own. None has a default: a caller states each (C44.1 section 5 "UNKNOWN").
/// </summary>
/// <param name="PoolFillByte">The byte the engine's pool allocator (<c>0xA7A7F4</c>) leaves in memory it hands out: <c>[+0x47]</c> bit 7 (<c>0x9F402C</c>), <c>[+0x58]</c> bits 6..7 (<c>0x9EDC84</c>) and the unwritten bytes of a Sound's <c>[+0x68]</c> word come from it (C44.1 B2, V1). UNKNOWN on the phone; tests state 0.</param>
/// <param name="RateHz">The global at <c>0x105243C</c>: the recovery time and the action delay are converted with it (<c>0x9C4094..0x9C40EC</c>, <c>0xA6156C..0xA61670</c>). UNKNOWN (writers <c>0xA1C75C/0xA1C7D4</c>).</param>
/// <param name="RecoveryFloor">The u16 at <c>0x1052440</c>: a bus recovery at or below it is stored as 0.</param>
/// <param name="Gate9B09FC">The cell the walker tests before a bus load, <c>[GOT 0xFFFFFE0C]+0x34</c> (<c>0x9B2D7C..0x9B2DA0</c>); true calls the unread <c>0x9B09FC</c>, which is a visible stop.</param>
/// <param name="ListEmptyA40E6C">The global {ptr,count} list <c>0xA40E6C</c> reads is empty, so it returns at once (<c>0xA40E78</c>). Not established at load (V3); the host states it.</param>
/// <param name="ListEmptyA4131C">The same for <c>0xA4131C</c> (<c>0xA41328</c>).</param>
public sealed record WwiseGraphHostInputs(byte PoolFillByte, uint RateHz, ushort RecoveryFloor, bool Gate9B09FC, bool ListEmptyA40E6C, bool ListEmptyA4131C);

/// <summary>
/// The unread callees of the HIRC load. Each null member makes its use a <see cref="WwiseMissingBehaviourException"/>; a test double supplied here is a stand-in for a body the inventory does not read.
/// </summary>
public sealed class WwiseGraphSeams
{
    /// <summary>Types with no handler read: State (1, <c>0x9B3E98</c> -> <c>0xA26CC4</c>), Attenuation (14, <c>0xA67558</c>), 15..17, 20..23, and the music hook <c>[0x108D968]</c> = <c>0x984930</c> (types 10..13, &gt; 23; A11). Returns the walker result.</summary>
    public Func<WwiseObject, int>? UnreadType { get; set; }
    /// <summary>The RanSeq creator <c>0xA07114(id, 1)</c> (size 0x94, vptr <c>0x103B860</c>; A10). Returns an UNREGISTERED node shell (<see cref="WwiseRuntimeGraph.CreateNodeShell"/>); the loader registers it.</summary>
    public Func<uint, WwiseRoutingNode>? CreateRanSeq { get; set; }
    /// <summary>The RanSeq init <c>0xA0828C(node, body, size)</c>.</summary>
    public Func<WwiseRoutingNode, WwiseObject, int>? InitRanSeq { get; set; }
    /// <summary>The Switch creator <c>0xA2D9A8</c> (size 0xD0, vptr <c>0x103BDC8</c>).</summary>
    public Func<uint, WwiseRoutingNode>? CreateSwitch { get; set; }
    /// <summary>The Switch init <c>0xA2F1D0</c>.</summary>
    public Func<WwiseRoutingNode, WwiseObject, int>? InitSwitch { get; set; }
    /// <summary>The Layer creator <c>0x9D273C</c> (size 0x88, vptr <c>0x103B050</c>).</summary>
    public Func<uint, WwiseRoutingNode>? CreateLayer { get; set; }
    /// <summary>The Layer init <c>0x9D24D4</c> (not read).</summary>
    public Func<WwiseRoutingNode, WwiseObject, int>? InitLayer { get; set; }
    /// <summary>The FxCustom creator <c>0x9CF038</c> (not read). Returns an unregistered object.</summary>
    public Func<uint, WwiseRuntimeFx>? CreateFxCustom { get; set; }
    /// <summary>The FX init <c>0x9CE3B8</c> for share sets and customs (D14: <c>0x9CE174</c>, <c>0x9CDEC4</c>, <c>0x9CC358</c> not read).</summary>
    public Func<WwiseRuntimeFx, WwiseObject, int>? InitFx { get; set; }
    /// <summary>The factory <c>0xA60C1C(type, id)</c> for an action kind other than Play (Stop, Seek, ...): not read. Returns an unregistered object.</summary>
    public Func<ushort, uint, WwiseRuntimeAction>? CreateAction { get; set; }
    /// <summary>The class init (<c>vt+0x28</c>) of an action kind other than Play (it reads the type-specific tail of the body).</summary>
    public Func<WwiseRuntimeAction, WwiseObject, int>? ActionClassInit { get; set; }
    /// <summary>The State chunk of a node or bus with at least one group (<c>0x9F6078</c> beyond its lookup, <c>0xA28198</c>, <c>0xA27B20</c>, <c>0x9F24C8</c>; C11).</summary>
    public Func<WwiseRoutingNode, IReadOnlyList<(uint GroupId, byte SyncType, IReadOnlyList<(uint StateId, uint InstanceId)> States)>, int>? StateChunk { get; set; }
    /// <summary>The steps after a curve is added to a subscription (L5-08: <c>0xA0F07C</c>, <c>0xA0F990</c>, <c>0xA11624</c> for a game parameter, <c>0xA11D58</c> for a modulator source); not read.</summary>
    public Func<WwiseRoutingNode, WwiseRtpc, int>? RtpcAfterAdd { get; set; }
    /// <summary>A bus's <c>vt+0xE4 = 0x9C0A70</c> (called by <c>0x9F5C30</c>); only its address is read.</summary>
    public Action<WwiseRoutingNode>? BusVtE4 { get; set; }
    /// <summary><c>0xA40E6C([bus+8], value)</c> (the tail of the bus <c>vt+0xC4 = 0x9C3D04</c>); used when the host does not state its list empty.</summary>
    public Action<uint, float>? A40E6C { get; set; }
    /// <summary><c>0xA4131C([bus+8], ...)</c> (the tail of the bus <c>vt+0x8C = 0x9C0D00</c>, slot argument); used when the host does not state its list empty.</summary>
    public Action<uint, int>? A4131C { get; set; }
    /// <summary><c>0xA40FF8([bus+8])</c> (the tail of the bus <c>vt+0x7C = 0x9C0C2C</c>).</summary>
    public Action<uint>? A40FF8 { get; set; }
    /// <summary><c>0xA4454C([bus+8])</c> (<c>0x9C650C</c>, channel config changed).</summary>
    public Action<uint>? A4454C { get; set; }
    /// <summary><c>0x9C62AC(bus)</c> (<c>0x9C6560</c>, the B byte's bit 3).</summary>
    public Action<WwiseRoutingNode>? Bit3A9C62AC { get; set; }
    /// <summary>The bus mixer record when it is not the id-0 no-op <see cref="WwiseBusWalk.MixerRecord9C0FC0"/> reads.</summary>
    public Func<WwiseRoutingNode, uint, bool, int>? MixerVtE0 { get; set; }
    /// <summary>The tail of the release <c>vt+0xC = 0x9F500C</c> once the count reaches zero and the object is out of its table (<c>vt+0x18</c>, <c>0x9F4F28</c>, the unlink from the parent; not read). Arguments: the object, the error code that made the load fail.</summary>
    public Action<WwiseRegistryObject, int>? DestroyTail { get; set; }
}

/// <summary>One bank as the walker keeps it: <c>[bank+0x3C]</c> the object list, <c>[bank+0x40]</c> the count, <c>[bank+0x44]</c> the capacity (A1, A2).</summary>
public sealed class WwiseLoadedBank
{
    internal WwiseLoadedBank(string name) { Name = name; }
    /// <summary>The bank file name (diagnostics).</summary>
    public string Name { get; }
    /// <summary>BKHD dword 0.</summary>
    public uint Version { get; internal set; }
    /// <summary>BKHD dword 1.</summary>
    public uint BankId { get; internal set; }
    /// <summary>The BKHD flag: <c>[loader+0x64] = (u16 at BKHD+0xC != 0)</c> (<c>0x9B2230</c>); it adds four bytes to a node's and a bus's tail. 0 in all six shipped banks.</summary>
    public bool FeedbackFlag { get; internal set; }
    /// <summary>The objects appended by the handlers, in HIRC order (a found object is appended again for each bank that holds it).</summary>
    public List<WwiseRegistryObject> Objects { get; } = new();
    /// <summary>The list capacity: count of the HIRC chunk plus the previous capacity (<c>0x9B32C0..0x9B3348</c>).</summary>
    public int Capacity { get; internal set; }
}

/// <summary>
/// The runtime node graph and the HIRC loader that builds it (see the file header). One instance is the engine's registry <c>REG</c>, its bank list and the globals the loaders write.
/// </summary>
public sealed class WwiseRuntimeGraph
{
    private readonly WwiseGraphHostInputs _host;
    private readonly WwiseGraphSeams _seams;
    private readonly WwiseRtpcStore? _rtpc;
    private readonly WwiseBusReaderSeams _readerSeams;
    private uint _nextKey = 0x01000000;
    private readonly Dictionary<(uint Key, uint Param), List<WwiseRtpc>> _subscriptionCurves = new();

    /// <param name="host">The host inputs (no defaults).</param>
    /// <param name="seams">The unread callees; may be empty.</param>
    /// <param name="rtpcManager">The RTPC manager <c>*0x108D908</c> (<c>WwiseRtpcStore</c>); null is "the manager pointer is null", which makes the first RTPC entry fail with error 2 (V6).</param>
    public WwiseRuntimeGraph(WwiseGraphHostInputs host, WwiseGraphSeams seams, WwiseRtpcStore? rtpcManager)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _seams = seams ?? throw new ArgumentNullException(nameof(seams));
        _rtpc = rtpcManager;
        _readerSeams = new WwiseBusReaderSeams
        {
            FxSlotChangedVtC4 = NodeVtC4,
            FxSlotChangedVt8C = NodeVt8C,
            VtE4 = NodeVtE4,
            Vt7C = NodeVt7C,
            ChannelConfigChangedA4454C = id => (_seams.A4454C ?? throw Missing("0xA4454C([bus+8]) (0x9C650C)"))(id),
            FlagBit3A9C62AC = b => (_seams.Bit3A9C62AC ?? throw Missing("0x9C62AC (0x9C6560)"))(b),
            MixerVtE0 = _seams.MixerVtE0,
        };
    }

    /// <summary>The registry tables <c>REG</c>.</summary>
    public WwiseRuntimeRegistry Registry { get; } = new();

    /// <summary>The master / second parentless bus slots at <c>0x108D9B0</c>.</summary>
    public WwiseMasterBusRegistry MasterBuses { get; } = new();

    /// <summary>The banks in load order.</summary>
    public List<WwiseLoadedBank> Banks { get; } = new();

    /// <summary>The global byte <c>0x108DED9</c> that <c>0x9F1EE8</c> writes: <c>(bus == 0)</c>.</summary>
    public byte Global108DED9 { get; private set; }

    /// <summary>The <c>RuntimeNodeOf</c> hook of <see cref="WwisePlaybackLimiter"/> and <see cref="WwisePlayPath"/>: the runtime node of a parsed hierarchy node.</summary>
    public WwiseRoutingNode? RuntimeNodeOf(WwiseNode node) => Registry.RuntimeNodeOf(node);

    /// <summary>
    /// Makes the PBI reference table and the voice linker's bus AddRef write the registry's single <c>[node+0xC]</c> (C44.1 B1/B6): the two call sites that used to keep separate counts.
    /// </summary>
    public void BindReferenceCounts(WwiseNodeRefTable nodeRefs, WwiseVoiceLinkSeams linkerSeams)
    {
        ArgumentNullException.ThrowIfNull(nodeRefs);
        ArgumentNullException.ThrowIfNull(linkerSeams);
        nodeRefs.Registry = Registry;
        linkerSeams.BusAddRefVt8 = WwiseRuntimeRegistry.BusAddRef;
    }

    private static WwiseMissingBehaviourException Missing(string what)
        => new($"M6-001/M6-025 (C44.1): {what} is not read by the inventory; supply WwiseGraphSeams or the host input that states it");

    private uint NextKey()
    {
        uint k = _nextKey;
        _nextKey += 0x100;                                                                // a stand-in for the pool address node+0x10 (UNKNOWN host address; stride keeps the holders node+0x1C and bus+0xC4 apart)
        return k;
    }

    // ------------------------------------------------------------------ the bank and the HIRC walker (A1, A2, A3)

    /// <summary>
    /// Loads one bank file: the chunk loop (<c>0x9B74D8</c>). BKHD sets the flag of <see cref="WwiseLoadedBank"/>; HIRC runs the walker; every other chunk belongs to a loader outside this graph (INIT, STMG, ENVS, PLAT, STID, DIDX, DATA)
    /// and is stepped over by its size. Returns 1 or the first error code of the walker (A1: the loop continues only while the result is 1). A chunk past the end of the file throws <see cref="InvalidDataException"/> (not an engine path).
    /// </summary>
    public int LoadBank(string name, ReadOnlyMemory<byte> data)
    {
        var bank = new WwiseLoadedBank(name);
        Banks.Add(bank);
        var span = data.Span;
        int off = 0;
        while (off + 8 <= span.Length)
        {
            var tag = System.Text.Encoding.ASCII.GetString(span.Slice(off, 4));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(off + 4, 4));
            int body = off + 8;
            if (size > (uint)(span.Length - body)) throw new InvalidDataException($"{name}: chunk '{tag}' at 0x{off:X} claims {size} bytes, past the end of the file");
            if (tag == "BKHD")
            {
                if (size < 8) throw new InvalidDataException($"{name}: BKHD is only {size} bytes");
                bank.Version = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(body, 4));
                bank.BankId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(body + 4, 4));
                bank.FeedbackFlag = size >= 14 && BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(body + 12, 2)) != 0;
            }
            else if (tag == "HIRC")
            {
                int r = LoadHirc(bank, data.Slice(body, (int)size));
                if (r != 1) return r;
            }
            off = body + (int)size;
        }
        return 1;
    }

    /// <summary>
    /// <c>[loader+0x30]</c> (<c>0x9B2D64</c>): set once a bus was created; until then the first absent bus runs the reset <c>0x9C5DB4</c>. It persists across banks (observed under emu_graph.py: a second bank's new bus does not reset the
    /// master slots); who clears it, and when, is UNSUPPORTED in the inventory (C44.1 A6), so nothing here clears it.
    /// </summary>
    private bool _flag30;

    private int LoadHirc(WwiseLoadedBank bank, ReadOnlyMemory<byte> chunk)
    {
        var s = chunk.Span;
        if (s.Length < 4) throw new InvalidDataException($"{bank.Name}: HIRC is only {s.Length} bytes");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(s[..4]);                     // 0x9B3294
        bank.Capacity = checked((int)count + bank.Capacity);                              // 0x9B32C0..0x9B3348: count + the old capacity
        int p = 4;
        for (uint i = 0; i < count; i++)
        {
            if (p + 5 > s.Length) throw new InvalidDataException($"{bank.Name}: HIRC ends inside object {i} of {count}");
            byte type = s[p];
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(p + 1, 4));       // 0x9BBF64(hdr, 5): u8 type, u32 size
            int body = p + 5;
            if (size < 4 || size > (uint)(s.Length - body)) throw new InvalidDataException($"{bank.Name}: HIRC object {i} claims {size} bytes");
            var o = new WwiseObject
            {
                Id = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(body, 4)), Type = (WwiseObjectType)type,
                Payload = chunk.Slice(body, (int)size), Bank = bank.Name, FeedbackEnabled = bank.FeedbackFlag,
            };
            p = body + (int)size;
            int r = Handle(bank, o);                                                 // 0x9B338C: addls pc,pc,(type-1)<<2
            if (r != 1) return r;                                                         // 0x9B3684..0x9B369C: the loop continues only while the result is 1
        }
        return 1;
    }

    private int Handle(WwiseLoadedBank bank, WwiseObject o)
    {
        switch ((byte)o.Type)
        {
            case 2: return HandleSound(bank, o);
            case 3: return HandleAction(bank, o);
            case 4: return HandleEvent(bank, o);
            case 5: return HandleContainer(bank, o, _seams.CreateRanSeq, _seams.InitRanSeq, "RanSeq 0xA07114/0xA0828C");
            case 6: return HandleContainer(bank, o, _seams.CreateSwitch, _seams.InitSwitch, "Switch 0xA2D9A8/0xA2F1D0");
            case 7: return HandleActorMixer(bank, o);
            case 8: return HandleBus(bank, o);
            case 9: return HandleContainer(bank, o, _seams.CreateLayer, _seams.InitLayer, "Layer 0x9D273C/0x9D24D4");
            case 18: case 19: return HandleFx(bank, o);
            default:
                return (_seams.UnreadType ?? throw Missing($"the handler of HIRC type {(byte)o.Type} (State 0xA26CC4, Attenuation 0xA67558, the music hook 0x984930, types 15..17 and 20..23)"))(o);
        }
    }

    private static void Append(WwiseLoadedBank bank, WwiseRegistryObject obj)
    {
        if (bank.Objects.Count < bank.Capacity) bank.Objects.Add(obj);                    // A2: 0x9B3BD0..0x9B3C00 (index [bank+0x40]++ below [bank+0x44])
    }

    // ------------------------------------------------------------------ release (B6)

    /// <summary><c>vt+0xC = 0x9F500C</c> (B6): decrement; at zero the object leaves its table (<c>0x9F5060..0x9F5168</c>) and the unread tail runs (<see cref="WwiseGraphSeams.DestroyTail"/>).</summary>
    private void Release(WwiseRegistryObject o, int code = 0)
    {
        o.RefCount0C--;
        if (o.RefCount0C > 0) return;
        Registry.Remove(o);
        (_seams.DestroyTail ?? throw Missing($"the tail of the release 0x9F500C (0x9F50FC.., 0x9F4F28) for object {o.Id} reaching zero (the load failed with code {code})"))(o, code);
    }

    private int FailRelease(WwiseRegistryObject o, int code)
    {
        Release(o, code);
        return code;
    }

    private WwiseNode Parse(WwiseObject o)
    {
        // The engine does not check that a loader consumed the whole body (A1); the parse is asked not to either.
        var n = WwiseHierarchy.TryRead(o, out var problem, requireWholeBody: false);
        return n ?? throw Missing($"object {o.Id} of type {(byte)o.Type}: {problem}");
    }

    // ------------------------------------------------------------------ node construction (B1, B2, B3, D1, D5)

    /// <summary>
    /// The common node shell: <c>0x9D0418</c> (id, count 1), <c>0x9F402C</c> (<see cref="WwiseBusWalk.BaseCtor9F402C"/>), then <c>0x9EDC84</c> (B3: <c>[+0x59] = 0</c>, <c>[+0x58]</c> bits 3..5 cleared, the rest of the pool byte), and the
    /// category rule that gives <c>[+0x46]</c> bit 2 (<c>vt+0x44</c> in {0, 0xA, 0xC} is table B). NOT registered: the creator registers (<c>0x9F40F4</c>) after its own fields; the seams of RanSeq/Switch/Layer use this and the loader registers them.
    /// </summary>
    public WwiseRoutingNode CreateNodeShell(uint id, int category44)
    {
        var n = new WwiseRoutingNode { Id = id, Category44 = category44, SubscriptionKey10 = NextKey() };
        WwiseBusWalk.BaseCtor9F402C(n, _host.PoolFillByte);
        n.Word58 = (ushort)(_host.PoolFillByte & 0xC7);                                   // 0x9EDC84: [+0x58] bits 3..5 = 0; [+0x59] = 0 (bit 7 cleared from old & 0x80)
        bool bit2 = category44 is 0xA or 0xC || category44 == 0;
        n.Byte46 = (byte)((n.Byte46 & ~4) | (bit2 ? 4 : 0));
        return n;
    }

    /// <summary>The Sound creator <c>0xA1D814</c> (D1): the shell, the member constructor <c>0xA1E870</c> at <c>+0x5C</c> and the registration.</summary>
    public WwiseRoutingNode CreateSound(uint id)
    {
        var n = CreateNodeShell(id, WwiseNodeCategory44.Sound);
        uint fill = _host.PoolFillByte * 0x01010101u;
        n.SourceId5C = 0; n.InMemorySize64 = 0; n.Field6C = 0;                            // 0xA1E870
        n.SourceId60 = 0xFFFFFFFFu; n.Plugin70 = 0xFFFFFFFFu;
        n.Word68 = fill & ~0x80u;                                                         // [+0x68] bit 7 = 0; the other bits are the pool's
        Registry.Register9F40F4(n);                                                       // 0xA1D89C
        return n;
    }

    private WwiseRoutingNode CreateActorMixer(uint id)
    {
        var n = CreateNodeShell(id, WwiseNodeCategory44.ActorMixer);
        // 0xA69BCC (container base): [+0x5C]/[+0x60]/[+0x64] = 0 (the child array, its count and capacity)
        n.ChildCapacity64 = 0;
        Registry.Register9F40F4(n);
        return n;
    }

    // ------------------------------------------------------------------ Sound (A4, D1, D2)

    private int HandleSound(WwiseLoadedBank bank, WwiseObject o)
    {
        var sound = (WwiseSoundNode)Parse(o);
        var found = Registry.Lookup9A7EB0(o.Id, 0);                                       // 0x9B3E24 (the hit increments [+0xC])
        if (found is null)
        {
            var node = CreateSound(o.Id);                                                 // 0x9B4000 -> 0xA1D814
            int r = InitSound(node, sound, partial: false);                               // 0x9B4020 -> 0xA1DA08(node, body, size, bank, 0)
            if (r != 1) return FailRelease(node, r);
            Append(bank, node);
            return 1;
        }
        var existing = found as WwiseRoutingNode ?? throw Missing($"a table-A object {o.Id} that is not a node, found for a Sound");
        bool skip = (existing.Byte68 & 0x7C) != 0 && (existing.Byte68 & 0x80) != 0;      // 0x9B3E30..0x9B3E3C, 0x9B3FB0..0x9B3FB8: skip the init
        if (!skip)
        {
            int r = InitSound(existing, sound, partial: true);                            // 0x9B3E54..0x9B3E64: init with partial = 1
            if (r != 1) throw Missing($"the result handling of a failed partial Sound init (code {r}) of node {o.Id}");
        }
        Append(bank, existing);
        return 1;
    }

    /// <summary>
    /// <c>0xA1DA08</c> (D2): the source reader <c>0x9B9C90</c> fills a 0x20-byte struct from <c>u32 plugin, u8 stream, u32 sourceId, u32 mem, u8 bits</c>; a plug-in nibble of 1 (codec: 1 for stream 1 or 2, 3 for stream 0), 0 (returns 1, no codec bits)
    /// takes the form <c>0xA1EA68</c>; 2 or 5 (a source plug-in, whose params pointer is never null) takes <c>0xA1EB58</c>; any other nibble returns 2. Then <c>0x9F6EF8</c> (NodeBase) with <paramref name="partial"/>.
    /// </summary>
    private int InitSound(WwiseRoutingNode node, WwiseSoundNode s, bool partial)
    {
        uint plugin = s.PluginId;
        int kind = (int)(plugin & 0xF);
        byte b14 = (byte)((s.SourceBits & 1) | (s.StreamType == 1 ? 2 : 0));              // struct +0x14: bit 0 = bits &1, bit 1 = (stream == 1)
        byte b15 = (byte)(((s.SourceBits >> 1) & 1) << 1 | ((s.SourceBits >> 3) & 1) << 3);   // struct +0x15: bit 1 = bits &gt;&gt; 1, bit 3 = bits &gt;&gt; 3
        bool sourcePluginForm;
        switch (kind)
        {
            case 1:
                if (s.StreamType > 2) return 2;                                           // 0x9B9D90..0x9B9D94: a stream type above 2 returns 2
                int codec = s.StreamType is 1 or 2 ? 1 : 3;
                b14 |= (byte)(codec << 2);
                sourcePluginForm = false;
                break;
            case 0:
                sourcePluginForm = false;                                                 // 0x9B9D3C..0x9B9D44: returns 1 with no codec bits
                break;
            case 2:
            case 5:
                sourcePluginForm = true;                                                  // 0x9B9D4C: struct+0x18 non-null (V2)
                break;
            default:
                return 2;                                                                 // 0x9B9D3C..0x9B9D48
        }
        if (!sourcePluginForm)
        {
            node.SourceId5C = s.MediaId; node.SourceId60 = s.MediaId;                     // 0xA1EA68: [+0x5C] = [+0x60] = sourceId
            node.InMemorySize64 = s.InMemorySize;                                         // [+0x64] = mem
            node.Word68 = (uint)(b14 | (b15 << 8));                                       // [+0x68] word = struct bits (+0x14, +0x15)
            node.Field6C = 0;
            node.Plugin70 = plugin;                                                       // [+0x70] = plugin
            node.Byte68 = (byte)(node.Byte68 | 0x80);                                     // 0xA1EA98..0xA1EAAC: OR 0x80 into the byte
        }
        else
        {
            node.SourceId5C = s.MediaId; node.SourceId60 = 0xFFFFFFFFu;                   // 0xA1EB58(node+0x5C, sourceId): [+0x5C] = sourceId, [+0x60] = -1
            node.InMemorySize64 = 0; node.Field6C = 0; node.Plugin70 = 0xFFFFFFFFu;       // [+0x64] = 0, [+0x6C] = 0, [+0x70] = -1
            node.Word68 = (node.Word68 & 0xFFFFF000u) | 0x08u;                           // byte [+0x68] = 0x08 (codec bits 2..6 = 2); no bit 7. The nibble [+0x69] bits 0..3 is cleared too: observed under emu_graph.py (V2 names only the byte); bits 12..31 stay the pool's
        }
        return NodeBase9F6EF8(node, s.Params, partial);                                   // 0xA1DA74
    }

    // ------------------------------------------------------------------ ActorMixer (A5, D4, D5)

    private int HandleActorMixer(WwiseLoadedBank bank, WwiseObject o)
    {
        var am = (WwiseActorMixerNode)Parse(o);
        var found = Registry.Lookup9A7EB0(o.Id, 0);                                       // 0x9B3B00: table A lookup; a hit is [+0xC]++ (0x9B3FD4..0x9B3FE0)
        if (found is not null) { Append(bank, found); return 1; }                         // A3: found-existing, no init
        var node = CreateActorMixer(o.Id);                                                // 0xA66950(id)
        int r = InitActorMixer(node, am);                                                 // 0xA669EC(node, body, size)
        if (r != 1) return FailRelease(node, r);                                          // a node that holds children has a count above 1 after the release and stays registered (observed under emu_graph.py)
        Append(bank, node);
        return 1;
    }

    private int InitActorMixer(WwiseRoutingNode node, WwiseActorMixerNode am)
    {
        int r = NodeBase9F6EF8(node, am.Params, partial: false);                          // 0xA66A00..0xA66A14
        if (r != 1) return r;
        node.ChildCapacity64 = am.Children.Count;                                         // 0xA66A54..0xA66A74: alloc 4n, [+0x64] = n
        foreach (uint childId in am.Children)
        {
            r = AddChildById981658(node, childId);                                        // 0xA66A9C..0xA66AAC: vt+0x2C
            if (r != 1) return r;
        }
        return 1;
    }

    /// <summary><c>vt+0x2C = 0x981658</c> (D4): id 0 returns 0xE; the child is looked up in table A (the hit increments its count); absent returns 0xF; else <c>vt+0x28</c>.</summary>
    private int AddChildById981658(WwiseRoutingNode parent, uint childId)
    {
        if (childId == 0) return 0xE;
        var child = Registry.Lookup9A7EB0(childId, 0) as WwiseRoutingNode;                // 0x981694
        if (child is null) return 0xF;                                                    // 0x9816B8
        return ActorMixerAddChild981940(parent, child);                                   // its result: 1, or 0x15 for a child that has a parent
    }

    /// <summary>
    /// <c>vt+0x28 = 0x981940</c> (D4): <c>0xA66898</c> rejects a child whose <c>[+0x34]</c> is set (0x15) and a duplicate; the child goes into the id-sorted array <c>[+0x5C]</c> (the capacity grows by one when full); the child's <c>vt+0x1C</c>
    /// (<c>0x9F1C40</c>) stores <c>[child+0x34] = parent</c>; the parent's <c>vt+8</c> is called once; the child's reference (taken by the lookup or by the caller's <c>vt+8</c>) is released.
    /// </summary>
    private int ActorMixerAddChild981940(WwiseRoutingNode parent, WwiseRoutingNode child)
    {
        if (parent.Category44 != WwiseNodeCategory44.ActorMixer)
            throw Missing($"AddChild of a parent of category {parent.Category44} (only the ActorMixer's 0x981940 is read)");
        if (child.Parent is not null) { Release(child); return 0x15; }                    // 0xA6689C..0xA668A4; the child's lookup reference is released on this path too (observed: the child's count is back to its old value)
        if (parent.Children5C.Contains(child)) throw Missing("the result code of AddChild for a duplicate child (0xA66898)");
        int at = parent.Children5C.FindIndex(c => c.Id > child.Id);                       // the id-sorted insert (D4: "id-sorted array")
        parent.Children5C.Insert(at < 0 ? parent.Children5C.Count : at, child);
        if (parent.Children5C.Count > parent.ChildCapacity64) parent.ChildCapacity64 = parent.Children5C.Count;   // 0x981A54..0x981AC0: grown by one when full
        child.Parent = parent;                                                            // 0x981A30 -> 0x9F1C40: str r1,[r0,#0x34]
        parent.AddRef9F1CBC();                                                            // 0x981A40: the parent's vt+8, once per child
        Release(child);                                                                   // 0x981964..0x981970
        return 1;
    }

    // ------------------------------------------------------------------ Layer, RanSeq, Switch (A3, A10: creators and inits are seams)

    private int HandleContainer(WwiseLoadedBank bank, WwiseObject o, Func<uint, WwiseRoutingNode>? create, Func<WwiseRoutingNode, WwiseObject, int>? init, string what)
    {
        var found = Registry.Lookup9A7EB0(o.Id, 0);                                       // A3: found-existing is [+0xC]++ and append, no init
        if (found is not null) { Append(bank, found); return 1; }
        var node = (create ?? throw Missing($"the creator of {what}"))(o.Id);
        Registry.Register9F40F4(node);                                                    // the creator registers before the init
        int r = (init ?? throw Missing($"the init of {what}"))(node, o);
        if (r != 1) throw Missing($"what the walker does when the init of {what} fails (code {r}): A10 does not say whether the node is released");
        Append(bank, node);
        return 1;
    }

    // ------------------------------------------------------------------ Event and Action (A7, A8, D12, D13)

    private int HandleEvent(WwiseLoadedBank bank, WwiseObject o)
    {
        var found = Registry.LookupAndAddRef(WwiseRegistryTable.Event, o.Id);             // 0x9B3134..0x9B3190, found: [obj+0xC]++
        if (found is not null) { Append(bank, found); return 1; }
        var ev = new WwiseRuntimeEvent { Id = o.Id };                                     // 0x9CC96C (the base: id, count 1)
        int r = InitEvent(ev, o.Payload.Span);                                            // 0x9CD01C
        if (r != 1) return FailRelease(ev, r);
        Registry.Insert(ev);                                                              // 0x9CC9C4: registered only after a successful init
        Append(bank, ev);
        return 1;
    }

    private int InitEvent(WwiseRuntimeEvent ev, ReadOnlySpan<byte> body)
    {
        uint n = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(4, 4));               // n = [body+4]
        if (n == 0) return 1;
        WwiseRuntimeAction? prev = null;
        for (uint i = 0; i < n; i++)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(8 + 4 * (int)i, 4));
            if (id == 0) return 0xE;                                                      // the first id or a next id of 0
            var act = Registry.LookupAndAddRef(WwiseRegistryTable.Action, id) as WwiseRuntimeAction;   // 0x9CD0BC..0x9CD0C8: lookup, [act+0xC]++
            if (act is null) return 2;                                                    // 0x9CD10C
            act.Next10 = null;                                                            // 0x9CD0D8
            if (prev is not null) prev.Next10 = act; else ev.FirstAction10 = act;
            prev = act;
        }
        return 1;
    }

    /// <summary>The kind-independent head of an action body (gapA 1.7: id, type, target, isBus, the property bundle, the ranged bundle), and the Play class's tail (fade curve, bank id).</summary>
    private sealed class ActionBody
    {
        public ushort Type;
        public uint Target;
        public bool IsBus;
        public byte[] PropIds = Array.Empty<byte>();
        public uint[] PropValues = Array.Empty<uint>();
        public byte[] RangedIds = Array.Empty<byte>();
        public uint[] RangedMin = Array.Empty<uint>();
        public uint[] RangedMax = Array.Empty<uint>();
        public byte Fade;
        public uint BankId;
        public bool IsPlay => (Type >> 8) == 4;
    }

    private static ActionBody ParseAction(ReadOnlySpan<byte> b)
    {
        var a = new ActionBody { Type = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(4, 2)), Target = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(6, 4)), IsBus = b[10] != 0 };
        int p = 11;
        int n = b[p++];
        a.PropIds = b.Slice(p, n).ToArray(); p += n;
        a.PropValues = new uint[n];
        for (int i = 0; i < n; i++, p += 4) a.PropValues[i] = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(p, 4));
        n = b[p++];
        a.RangedIds = b.Slice(p, n).ToArray(); p += n;
        a.RangedMin = new uint[n]; a.RangedMax = new uint[n];
        for (int i = 0; i < n; i++, p += 8)
        {
            a.RangedMin[i] = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(p, 4));
            a.RangedMax[i] = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(p + 4, 4));
        }
        if (a.IsPlay) { a.Fade = b[p]; a.BankId = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(p + 1, 4)); }   // Play vt+0x28 (0xA62984): u8 fade curve, u32 bank id
        return a;
    }

    private int HandleAction(WwiseLoadedBank bank, WwiseObject o)
    {
        var parsed = ParseAction(o.Payload.Span);
        var found = Registry.LookupAndAddRef(WwiseRegistryTable.Action, o.Id) as WwiseRuntimeAction;   // 0x9B3074..0x9B307C
        if (found is not null)
        {
            if (parsed.Type == 0x403 && (found.Byte22 & 0x20) == 0)                       // 0x9B3088..0x9B3094: the BODY's type is compared with 0x403; a Play action not yet initialised is initialised again
            {
                int ri = InitAction(found, parsed, o);
                if (ri != 1) return FailRelease(found, ri);                               // 0x9B300C..0x9B302C: the found object is released and the code returned
            }
            Append(bank, found);
            return 1;
        }
        WwiseRuntimeAction act;
        if (parsed.IsPlay)
            act = new WwiseRuntimeAction { Id = o.Id, Type20 = parsed.Type };             // 0xA60ADC + the Play creator 0xA62E54 ([+0x24] = -1)
        else
            act = (_seams.CreateAction ?? throw Missing($"the creator of an action of type {parsed.Type:X4} (the factory 0xA60C1C beyond Play)"))(parsed.Type, o.Id);
        Registry.Insert(act);                                                             // 0xA60DEC: registered before the init
        int r = InitAction(act, parsed, o);                                               // 0xA613B0
        if (r != 1) return FailRelease(act, r);
        Append(bank, act);
        return 1;
    }

    /// <summary>
    /// <c>0xA613B0</c> (D13): <c>vt+0x18</c> (<c>0xA60250</c>) stores the target id and <c>[+0x22]</c> bit 6 (isBus); the two property bundles are rebuilt; the class init (<c>vt+0x28</c>; Play <c>0xA62984</c>: fade curve to <c>[+0x22]</c> bits 0..4,
    /// bank id to <c>[+0x24]</c>, <c>[+0x22] |= 0x20</c>); then property 0xF (delay) is rewritten to <c>(ms * rate) / 1000</c> as a signed 64-bit divide, even when the class init failed. A ranged delay is not settled (the ranged bundle is f32): a visible stop.
    /// </summary>
    private int InitAction(WwiseRuntimeAction act, ActionBody a, WwiseObject o)
    {
        act.TargetId1C = a.Target;                                                        // 0xA60268
        act.Byte22 = (byte)((act.Byte22 & ~0x40) | (a.IsBus ? 0x40 : 0));                 // 0xA60268..0xA6026C
        var propValues = (uint[])a.PropValues.Clone();
        act.BaseBundle14 = a.PropIds.Length == 0 ? null : new WwiseParamBundle(a.PropIds, propValues);   // 0xA613F4..0xA61564
        act.RangedBundle18 = a.RangedIds.Length == 0 ? null : new WwiseParamBundle(a.RangedIds, a.RangedMin, a.RangedMax);
        int r;
        if (a.IsPlay)
        {
            act.Byte22 = (byte)((act.Byte22 & ~0x1F) | (a.Fade & 0x1F));                  // 0xA62998..0xA6299C
            act.BankId24 = a.BankId;                                                      // 0xA629A8..0xA629B4
            act.Byte22 |= 0x20;                                                           // 0xA629B8..0xA629BC
            r = 1;
        }
        else
        {
            r = (_seams.ActionClassInit ?? throw Missing($"the class init (vt+0x28) of an action of type {a.Type:X4}"))(act, o);
        }
        int rat = Array.IndexOf(a.RangedIds, (byte)WwiseProp.DelayTime);                  // 0xA615DC..0xA61670: both words of the first ranged entry with id 0xF, held as u32, signed 64-bit (ms * rate) / 1000
        if (rat >= 0)
        {
            var mins = (uint[])a.RangedMin.Clone(); var maxs = (uint[])a.RangedMax.Clone();
            mins[rat] = unchecked((uint)(int)((long)(int)mins[rat] * _host.RateHz / 1000));
            maxs[rat] = unchecked((uint)(int)((long)(int)maxs[rat] * _host.RateHz / 1000));
            act.RangedBundle18 = new WwiseParamBundle(a.RangedIds, mins, maxs);
        }
        int at = Array.IndexOf(a.PropIds, (byte)WwiseProp.DelayTime);                     // 0xA6156C..0xA615D8: the first entry with id 0xF
        if (at >= 0)
        {
            propValues[at] = unchecked((uint)(int)((long)(int)propValues[at] * _host.RateHz / 1000));
            act.BaseBundle14 = new WwiseParamBundle(a.PropIds, propValues);
        }
        return r;
    }

    // ------------------------------------------------------------------ FxShareSet and FxCustom (A9, D14)

    private int HandleFx(WwiseLoadedBank bank, WwiseObject o)
    {
        bool custom = o.Type == WwiseObjectType.FxCustom;
        var table = custom ? WwiseRegistryTable.FxCustom : WwiseRegistryTable.FxShareSet;
        var found = Registry.LookupAndAddRef(table, o.Id);                                // 0x9B3FBC
        if (found is not null) { Append(bank, found); return 1; }
        var fx = custom
            ? (_seams.CreateFxCustom ?? throw Missing("the FxCustom creator 0x9CF038"))(o.Id)
            : new WwiseRuntimeFx { Id = o.Id };                                           // 0x9CEA28: [+0x10] = -1, [+0x14..0x38] = 0
        Registry.Insert(fx);
        int r = (_seams.InitFx ?? throw Missing("the FX init 0x9CE3B8 (0x9CE174, 0x9CDEC4, 0x9CC358)"))(fx, o);
        if (r != 1) throw Missing($"what the walker does when the FX init 0x9CE3B8 fails (code {r}): A9 does not say whether the object is released");
        Append(bank, fx);
        return 1;
    }

    // ------------------------------------------------------------------ NodeBase 0x9F6EF8 (C1..C12)

    /// <summary>
    /// <c>0x9F6EF8(node, &amp;cursor, &amp;size, partial)</c> (C44.1 C1..C12), in the engine's order, over the parsed block. After it (and on an abort) a non-bus node's <see cref="WwiseRoutingNode.Node40"/> is the engine's <c>[node+0x40]</c> word.
    /// </summary>
    public int NodeBase9F6EF8(WwiseRoutingNode node, WwiseNodeParams p, bool partial)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(p);
        try { return NodeBaseCore(node, p, partial); }
        finally { if (!node.IsBus) node.Node40 = node.Word40; }
    }

    private int NodeBaseCore(WwiseRoutingNode node, WwiseNodeParams p, bool partial)
    {
        int r = FxReader9ECAD8(node, p, partial);                                         // C1
        if (partial || r != 1) return r;                                                  // C2
        node.Byte45 = SetBit(node.Byte45, 5, p.OverrideAttach & 1);                       // C3: 0x9F6F3C..0x9F6F4C
        if (p.BusId != 0)                                                                 // C4: overrideBus
        {
            var bus = Registry.Lookup9A7EB0(p.BusId, 1) as WwiseRoutingNode;              // 0x9F70A0
            if (bus is null) return 2;                                                    // 0x9F7214: ABSENT is error 2
            node.AddRef9F1CBC();                                                          // node vt+8
            int lr = BusAddChild9C181C(bus, node);                                        // bus vt+0x28
            if (lr != 1) throw Missing($"the result handling of the output-bus link failing with {lr:X} (0x9C1528)");
            Release(bus);                                                                 // bus vt+0xC: the lookup's reference
        }
        if (p.ParentId != 0)                                                              // C5: directParent
        {
            if (Registry.Lookup9A7EB0(p.ParentId, 0) is { } parent)                       // ABSENT is skipped silently (0x9F7094)
            {
                var pn = parent as WwiseRoutingNode ?? throw Missing($"a parent {p.ParentId} that is not a node");
                node.AddRef9F1CBC();                                                      // node vt+8
                int lr = ActorMixerAddChild981940(pn, node);                              // parent vt+0x28
                if (lr != 1) throw Missing($"the result handling of the parent link failing with {lr:X} (0xA66898)");
                Release(pn);                                                              // parent vt+0xC: the lookup's reference
            }
        }
        // C6: the flags byte
        node.Word40 = (p.Bits & 1) != 0 ? node.Word40 | 1u : node.Word40 & ~1u;           // bit 0 -> [+0x40] bit 0 (0x9F6F74..0x9F6FA4)
        NodeVtC4(node);
        node.Byte45 = SetBit(node.Byte45, 7, (p.Bits >> 1) & 1);                          // bit 1 -> [+0x45] bit 7 (0x9F6FB8..0x9F6FD8)
        node.Byte47 = (byte)((node.Byte47 & ~0x1E) | (((p.Bits >> 2) & 0xF) << 1));       // bits 2..5 -> [+0x47] bits 1..4 (0x9F6FE0..0x9F7010)
        // C7: the property bundles (0x9ED51C)
        node.BaseBundle3C = PropBundle(p);
        node.RangedBundle4C = RangedBundle(p);
        node.Byte46 = (byte)(node.Byte46 | 1);                                            // 0x9ED554..0x9ED558
        // C8: positioning (0x9ECF44)
        byte pos = p.PositioningBits;
        node.Word40 = (pos & 1) != 0 ? node.Word40 | 0xFFEu : node.Word40 & ~0xFFEu;      // 0x9F6D94: bits 1..11
        if ((pos & 1) != 0 && (pos & 2) != 0)
        {
            node.Byte47 = (byte)(SetBit(node.Byte47, 0, (pos >> 2) & 1));                 // 0x9ECF7C..0x9ECF94
            node.Byte46 = (byte)(node.Byte46 & ~0x18);
        }
        if ((pos & 1) != 0 && (pos & 8) != 0) throw Missing("the 3D positioning body (0x9ECFB0..)");
        // C9: aux (0x9ED84C)
        byte aux = p.AuxBits;
        node.Word40 = (aux & 1) != 0 ? node.Word40 | (1u << 21) : node.Word40 & ~(1u << 21);   // 0x9ED878..0x9ED880
        NodeVtC4(node);
        node.Byte59 = SetBit(node.Byte59, 4, (aux >> 1) & 1);                             // 0x9ED890..0x9ED8A0
        node.Word40 = (aux & 4) != 0 ? node.Word40 | (0xFu << 22) : node.Word40 & ~(0xFu << 22);   // 0x9ED8B4..0x9ED8BC
        NodeVtC4(node);
        if ((aux & 8) != 0)                                                               // 0x9ED910..0x9ED998
        {
            if (node.AuxIds54 is null && p.AuxIds.Any(x => x != 0)) node.AuxIds54 = new uint[4];   // 0x9ED91C..0x9ED938, 0x9ED970: the block is allocated only when it is null AND an id is non-zero
            if (node.AuxIds54 is { } blk) for (int i = 0; i < 4; i++) blk[i] = p.AuxIds[i];
        }
        else if (node.AuxIds54 is { } block) Array.Clear(block);                          // 0x9ED8D4..0x9ED900
        // C10: advanced settings (0x9ED730)
        byte b0 = p.AdvancedByte0, b1 = p.AdvancedByte1, b4 = p.AdvancedByte3, b5 = p.AdvancedByte4;
        node.Byte45 = SetBit(node.Byte45, 6, (b0 >> 2) & 1);                              // 0x9ED768..0x9ED76C
        node.Word44 = (ushort)((node.Word44 & ~0x3FF) | (p.AdvancedMaxInstancesRaw & 0x3FF));   // 0x9ED77C..0x9ED784
        node.Byte58 = (byte)((node.Byte58 & 0xC0) | (b1 & 7) | ((b1 & 7) << 3));          // [+0x58] bits 0..2 and 3..5
        node.Byte59 = SetBit(SetBit(SetBit(node.Byte59, 5, (b5 >> 1) & 1), 6, (b5 >> 2) & 1), 7, (b5 >> 3) & 1);   // 0x9ED7A4..0x9ED7D0
        node.Byte45 = SetBit(node.Byte45, 2, b0 & 1);                                     // 0x9F627C(b0 &1): 0x9ED7D4
        node.Byte45 = SetBit(node.Byte45, 3, (b0 >> 1) & 1);                              // 0x9F68D8(b0 >> 1 &1): 0x9ED7E0
        node.Byte59 = (byte)((node.Byte59 & ~0xF) | (b4 & 0xF));                          // vt+0x130 = 0x980EF4(b4): [+0x59] bits 0..3 (0x9ED7F4)
        int v = (node.Byte59 & 0xF) == 3 ? 1 : node.Byte58 & 7;                           // V5: 0x980EF4 also rewrites [+0x58] bits 3..5
        node.Byte58 = (byte)((node.Byte58 & ~0x38) | (v << 3));
        node.Byte47 = SetBit(node.Byte47, 6, (b0 >> 3) & 1);                              // 0x9ED80C..0x9ED814
        node.Byte45 = SetBit(node.Byte45, 4, (b0 >> 4) & 1);                              // 0x9F6B44(b0 >> 4 &1): 0x9ED818
        node.Word40 = (b5 & 1) != 0 ? node.Word40 | (1u << 20) : node.Word40 & ~(1u << 20);   // 0x9ED824..0x9ED834
        NodeVtC4(node);
        // C11: the State chunk
        if (p.StateGroups.Count != 0)
        {
            r = (_seams.StateChunk ?? throw Missing("the State chunk (0x9F6078 beyond its lookup, 0xA28198, 0xA27B20, 0x9F24C8)"))(node, p.StateGroups);
            if (r != 1) return r;
        }
        // C12: the RTPC entries
        foreach (var rtpc in p.Rtpcs)
        {
            r = SubscribeRtpc(node, rtpc);
            if (r != 1) return r;
        }
        return 1;
    }

    /// <summary>The bundle <c>[+0x3C]</c> (<c>0x9ED51C</c>: count, id bytes, then the values, in bank order with duplicates); null when the count is 0.</summary>
    private static WwiseParamBundle? PropBundle(WwiseNodeParams p)
    {
        if (p.PropEntries.Count != 0) return new WwiseParamBundle(p.PropEntries.Select(e => e.Id).ToArray(), p.PropEntries.Select(e => e.Value).ToArray());
        return p.Props.Count == 0 ? null : WwiseParamBundle.FromProps(p.Props);
    }

    /// <summary>The ranged bundle <c>[+0x4C]</c> (8-byte entries); null when the count is 0.</summary>
    private static WwiseParamBundle? RangedBundle(WwiseNodeParams p)
    {
        if (p.RangedEntries.Count != 0)
            return new WwiseParamBundle(p.RangedEntries.Select(e => e.Id).ToArray(),
                p.RangedEntries.Select(e => BitConverter.SingleToUInt32Bits(e.Min)).ToArray(), p.RangedEntries.Select(e => BitConverter.SingleToUInt32Bits(e.Max)).ToArray());
        return p.RangedProps.Count == 0 ? null : new WwiseParamBundle(p.RangedProps.Keys.ToArray(),
            p.RangedProps.Values.Select(v => BitConverter.SingleToUInt32Bits(v.Min)).ToArray(), p.RangedProps.Values.Select(v => BitConverter.SingleToUInt32Bits(v.Max)).ToArray());
    }

    private static byte SetBit(byte value, int bit, int on) => (byte)(on != 0 ? value | (1 << bit) : value & ~(1 << bit));

    /// <summary>
    /// The FX reader <c>0x9ECAD8</c> (C1): partial 0: the overrideFX byte to <c>[+0x40]</c> bits 12..16; count 0 returns 1; each entry runs <c>0x9F5B24(node, slot, rendered != 0)</c> then, when not rendered and the id is set, <c>0x9F5760(node, slot, id, share != 0, 0)</c>;
    /// afterwards (also on an error) <c>0x9F5C30(node, bypass, -1)</c>. Partial 1 (an existing Sound) runs only the <c>0x9F5B24</c> calls.
    /// </summary>
    private int FxReader9ECAD8(WwiseRoutingNode node, WwiseNodeParams p, bool partial)
    {
        if (!partial) node.Word40 = p.OverrideFxByte != 0 ? node.Word40 | 0x1F000u : node.Word40 & ~0x1F000u;   // 0x9ECAF8..0x9ECB0C: any non-zero byte sets bits 12..16 (observed under emu_graph.py: the byte 2 sets them too)
        if (p.FxEntries.Count == 0) return 1;
        int result = 1;
        foreach (var e in p.FxEntries)
        {
            WwiseBusWalk.RenderedChange9F5B24(node, e.Slot, e.Rendered != 0, _readerSeams);                       // 0x9ECB8C: its return value is ignored (then cmp sb,#0)
            if (!partial && e.Rendered == 0 && e.Id != 0)
            {
                int r = WwiseBusWalk.RegisterFx9F5760(node, e.Slot, e.Id, (byte)(e.Share != 0 ? 1 : 0), 0, _readerSeams);   // 0x9ECBC4
                if (r != 1) { result = r; break; }
            }
        }
        if (!partial) WwiseBusWalk.FxBypass9F5C30(node, p.FxBypass, 0xFFFFFFFFu, _readerSeams);                      // 0x9ECBD4 / 0x9ECBE0
        return result;
    }

    // ------------------------------------------------------------------ the node/bus callbacks at load (C1, V3, V4)

    /// <summary>Node <c>vt+0xC4</c>: nothing for a Sound or ActorMixer while <c>[+0x30]</c> is null (<c>0xA1E050</c>, <c>0xA1DFE0</c>, <c>0x98154C</c>, C44.1 C1); a bus's is <c>0x9C3D04</c>.</summary>
    private void NodeVtC4(WwiseRoutingNode n)
    {
        if (n.IsBus) { BusVtC4_9C3D04(n); return; }
        NodeCallbackNoOp(n, "vt+0xC4");
    }

    private void NodeVt8C(WwiseRoutingNode n, int slot)
    {
        if (n.IsBus)
        {
            if (_host.ListEmptyA4131C) return;                                            // 0x9C0D00 tail-calls 0xA4131C([bus+8], ...): returns at once on an empty list (0xA41328)
            (_seams.A4131C ?? throw Missing("0xA4131C([bus+8], slot) (the tail of the bus vt+0x8C = 0x9C0D00)"))(n.Id, slot);
            return;
        }
        NodeCallbackNoOp(n, "vt+0x8C");
    }

    private void NodeVtE4(WwiseRoutingNode n)
    {
        if (n.IsBus) { (_seams.BusVtE4 ?? throw Missing("the bus vt+0xE4 = 0x9C0A70"))(n); return; }
        NodeCallbackNoOp(n, "vt+0xE4");
    }

    private void NodeVt7C(WwiseRoutingNode n)
    {
        if (n.IsBus) { (_seams.A40FF8 ?? throw Missing("0xA40FF8([bus+8]) (the tail of the bus vt+0x7C = 0x9C0C2C)"))(n.Id); return; }
        NodeCallbackNoOp(n, "vt+0x7C");
    }

    private static void NodeCallbackNoOp(WwiseRoutingNode n, string slot)
    {
        if (n.Category44 is not (WwiseNodeCategory44.Sound or WwiseNodeCategory44.ActorMixer))
            throw Missing($"the node {slot} of a category-{n.Category44} node");
        if (n.Node30 is not null) throw Missing($"the node {slot} of a node with a limiter ([+0x30] != 0)");
    }

    /// <summary>
    /// The bus <c>vt+0xC4 = 0x9C3D04</c> (V3): with an FX id set or <c>[+0x68]</c> byte non-zero it runs <c>0x9C39DC(bus, 0, 5)</c>, sets <c>[+0xCC] |= 0x10</c>, then <c>0xA40E6C([bus+8], value)</c> (an empty list returns at once). The other branch is not read.
    /// </summary>
    private void BusVtC4_9C3D04(WwiseRoutingNode bus)
    {
        if (!(bus.Fx28 is { AnyId: true } || bus.Byte68 != 0))
            throw Missing("the bus vt+0xC4 (0x9C3D04) with no FX id and a zero [+0x68] byte");
        float value = WwiseBusWalk.A9C39DC(bus, 0, 5, _rtpc);                             // 0x9C39DC(bus, 0, 5)
        bus.ByteCC = (byte)(bus.ByteCC | 0x10);                                           // [+0xCC] |= 0x10
        if (_host.ListEmptyA40E6C) return;                                                // 0xA40E78: the list is empty, return at once
        (_seams.A40E6C ?? throw Missing("0xA40E6C([bus+8], value) (the tail of the bus vt+0xC4 = 0x9C3D04)"))(bus.Id, value);
    }

    // ------------------------------------------------------------------ links (D3, D4)

    /// <summary>
    /// The bus <c>vt+0x28 = 0x9C181C</c> (D3): a child that already has an output bus first leaves it (<c>vt+0x30</c>, not read); <c>vt+0x12C</c> (<c>0x9C1528</c>) rejects a child with <c>[+0x38] != 0</c> (0x15) and a duplicate; a bus child goes into
    /// the sorted array <c>[bus+0x58]</c>, any other into <c>[bus+0x48]</c> (sorted by id here); the child's <c>vt+0x20</c> (<c>0x9F1EE8</c>, for a bus child <c>0x9C581C</c> with the bit-6 propagation) stores <c>[child+0x38] = bus</c> and <c>[child+0x40]</c> bits 26..28;
    /// the bus's <c>vt+8</c> is called; the child's reference is released.
    /// </summary>
    private int BusAddChild9C181C(WwiseRoutingNode bus, WwiseRoutingNode child)
    {
        if (!bus.IsBus) throw Missing("a bus link target that is not a bus");
        if (child.OutputBus is not null) throw Missing("the child's old output bus leaving (bus vt+0x30)");
        var array = child.IsBus ? bus.BusChildren58 : bus.NonBusChildren48;
        if (array.Contains(child)) throw Missing("the result code (5 or 0x17) of 0x9C1528 for a duplicate child");
        int at = array.FindIndex(c => c.Id > child.Id);
        array.Insert(at < 0 ? array.Count : at, child);                                   // 0x9C1968..0x9C1A00 / 0x9C18F4..0x9C1934
        if (child.Node30 is not null) throw Missing("0x9F1EE8 for a child with a limiter ([+0x30] != 0): 0xA443E0(1), vt+0xC4, 0x9F45C4");
        child.OutputBus = bus;                                                            // 0x9F1F4C
        child.Word40 |= 0x1C000000u;                                                      // 0x9F1F2C..0x9F1F38: bits 26..28
        Global108DED9 = 0;                                                                // (bus == 0)
        if (child.IsBus)                                                                  // 0x9C581C
            WwiseMasterBusRegistry.InheritFromParent(child, bus, Registry.Buses());
        bus.AddRef9F1CBC();                                                               // 0x9C195C
        Release(child);                                                                   // 0x9C1870..0x9C187C
        return 1;
    }

    // ------------------------------------------------------------------ Bus (A6, D6..D10)

    private int HandleBus(WwiseLoadedBank bank, WwiseObject o)
    {
        var bus = (WwiseBusNode)Parse(o);
        bool first = !_flag30;
        if (first && _host.Gate9B09FC) throw Missing("0x9B09FC (0x9B2D7C..0x9B2DA0)");     // the gate runs while [loader+0x30] == 0, before the lookup
        var found = Registry.Lookup9A7EB0(o.Id, 1);                                       // 0x9B2D44 (table B)
        if (found is not null) { _flag30 = true; Append(bank, found); return 1; }         // the found path falls to 0x9B2D64 and sets the flag too (0x9B2D60..0x9B2D64)
        if (first) MasterBuses.Reset9C5DB4();                                             // 0x9B2E44 -> 0x9C5DB4
        var node = ConstructBus(o.Id);                                                    // 0x9C3620
        int r = InitBus(node, bus, o.FeedbackEnabled);                                    // 0x9C3FFC
        if (r != 1) return FailRelease(node, r);
        _flag30 = true;                                                                   // 0x9B2D64
        Append(bank, node);
        return 1;
    }

    /// <summary>The bus constructor <c>0x9C3620</c> with the real callees (D6).</summary>
    public WwiseRoutingNode ConstructBus(uint id) => WwiseBusWalk.ConstructBus9C3620(id, Registry, _host, NextKey());

    private int InitBus(WwiseRoutingNode bus, WwiseBusNode b, bool feedback)
    {
        // D7: the parent
        uint parentId = b.ParentBusId;
        if (parentId == 0)
        {
            MasterBuses.RegisterParentless(bus);                                          // 0x9C4314..0x9C432C, 0x9C4034..0x9C4064
        }
        else
        {
            var parent = Registry.Lookup9A7EB0(parentId, 1) as WwiseRoutingNode;          // 0x9C4160
            if (parent is null) return 2;                                                 // 0x9C4330
            bus.AddRef9F1CBC();                                                           // this vt+8
            int lr = BusAddChild9C181C(parent, bus);                                      // parent vt+0x28
            if (lr != 1) throw Missing($"the result handling of the bus parent link failing with {lr:X}");
            Release(parent);                                                              // parent vt+0xC (0x9C4170..0x9C419C)
        }
        // D8: the bus reader 0x9C6420
        bus.BaseBundle3C = PropBundle(b.Params);                                          // 0x9C65CC
        byte a = b.ByteA, bb = b.ByteB;
        bus.Byte46 = SetBit(bus.Byte46, 7, a & 1);                                        // 0x9C6458..0x9C6464
        bus.Byte47 = SetBit(bus.Byte47, 0, (a >> 1) & 1);
        bus.Byte45 = SetBit(bus.Byte45, 2, bb & 1);                                       // 0x9F627C
        bus.Byte45 = SetBit(bus.Byte45, 3, (bb >> 1) & 1);                                // 0x9F68D8
        bus.Word44 = (ushort)((bus.Word44 & ~0x3FF) | (b.MaxInstances & 0x3FF));          // 0x9C6498..0x9C64A0
        bus.Byte47 = SetBit(bus.Byte47, 6, (bb >> 2) & 1);                                // 0x9C64B8
        WwiseBusWalk.ApplyChannelConfig(bus, b.ChannelConfig, _readerSeams);              // 0x9C64C4..0x9C651C
        WwiseBusWalk.ApplyFlagByteC(bus, b.ByteC, bb, _readerSeams);                      // 0x9C6528..0x9C6564
        // the rest of 0x9C3FFC
        bus.MaxDuck6C = b.MaxDuck;                                                        // 0x9C40D8
        uint rec = unchecked((uint)((long)(int)b.RecoveryMs * _host.RateHz / 1000));      // 0x9C40B0..0x9C40BC: umull + mla (recovery sign-extended) then the signed 64-bit divide by 1000 (0x4A685C)
        bus.RecoverySamples64 = rec <= _host.RecoveryFloor ? 0u : rec;                    // set to 0 if <= u16[0x1052440]
        foreach (var d in b.DuckEntries)                                                  // 0x9C4138 -> 0x9C3E94
        {
            int dup = bus.DuckList70.FindIndex(x => x.TargetBusId == d.TargetBusId);      // 0x9C3E94: 0x9C3EB8..0x9C3ECC walk the list comparing [entry+4] with the target id
            if (dup >= 0) bus.DuckList70[dup] = d;                                        // a hit (0x9C3ED0) overwrites volume, fades, curve and property; the count is not incremented
            else if (bus.DuckList70.Count >= 0x64) return 2;                              // 0x9C3FA8: the list is at its capacity 0x64
            else bus.DuckList70.Add(d);                                                   // the list [bus+0x70] (tail append)
            if (Registry.Lookup9A7EB0(d.TargetBusId, 1) is WwiseRoutingNode target)       // 0x9C3F..: ABSENT returns 1 with no subscription
            {
                if (d.TargetProperty >= WwiseBusWalk.ParamBitTable.Length)
                    throw Missing($"the duck target-property byte {d.TargetProperty}, beyond the word table 0xFFA6C0 (UNKNOWN: the engine reads past the table silently, ldr r1,[r3,r6,lsl #2])");
                uint bit = WwiseBusWalk.ParamBitTable[d.TargetProperty];
                if (bit > 63) throw Missing($"the mask bit {bit} of a duck subscription (0xA1A160)");
                target.RegistryC8 ??= new WwiseRtpcRegistry();                            // holder 3 (target+0xC4)
                target.RegistryC8.MaskA |= 1UL << (int)bit;                               // 0xA1A160(target+0xC4, T[targetProp])
                Release(target);                                                          // the engine's count of the target is unchanged afterwards (observed under emu_graph.py): the lookup's reference is given back
            }
        }
        int fr = WwiseBusWalk.ApplyFxList9C0D08(bus, b.Effects.Count, b.FxBypass,         // 0x9C41E0 -> 0x9C0D08
            i => new WwiseFxEntry(b.Effects[i].Index, b.Effects[i].EffectId, (byte)(b.Effects[i].IsShareSet ? 1 : 0), (byte)(b.Effects[i].IsRendered ? 1 : 0)),
            () => (b.MixerId, b.MixerFlag), _readerSeams);
        if (fr != 1) return fr;
        bus.Byte45 = SetBit(bus.Byte45, 5, b.AttachByte & 1);                             // 0x9C41FC..0x9C4204
        foreach (var rtpc in b.Params.Rtpcs)                                              // 0x9C4210..0x9C42D4
        {
            int r = SubscribeRtpc(bus, rtpc);
            if (r != 1) return r;
        }
        if (b.Params.StateGroups.Count != 0)                                              // 0x9C42E4: 0x9F6E3C
        {
            int r = (_seams.StateChunk ?? throw Missing("the bus State chunk (0x9F6E3C: 0x9F6078 beyond its lookup, 0xA28198, 0xA27B20, 0x9F24C8)"))(bus, b.Params.StateGroups);
            if (r != 1) return r;
        }
        return 1;                                                                         // 0x9F7364 skips 4 bytes with the BKHD flag: the parse does (feedback)
    }

    // ------------------------------------------------------------------ RTPC subscription at load (E1, E2, E3, L5-01..L5-08)

    /// <summary>
    /// The node/bus <c>vt+0xF0 = 0x9F1DE0</c> -> <c>0xA1A338</c> (E1, E2): a null manager returns 2 (V6); the registry <c>[node+0x14]</c> is created on first use (mask A 0, cache ~0, byte 0); a curve with no points returns 0x1F (L5-06); the curve joins the
    /// subscription (key <c>node+0x10</c>, parameter, type 2, accumulate), a repeated curve id is not read (L5-07); the after-add step is a seam (L5-08); the mask A bit of the parameter is set (<c>0xA1A160</c>), the byte <c>[reg+0x1C]</c> is 1 for a modulator source (type 2);
    /// then the node's <c>vt+0x80(param)</c> (a Sound's and ActorMixer's is a no-op; a bus's acts for parameters 29..33).
    /// </summary>
    private int SubscribeRtpc(WwiseRoutingNode node, WwiseRtpc r)
    {
        if (_rtpc is null) return 2;                                                      // 0xA1A35C..0xA1A36C, 0xA1A420: the manager pointer is null
        var reg = node.Registry14 ??= new WwiseRtpcRegistry();                            // 0xA1A42C..0xA1A474 (mask 0, cache ~0, byte 0)
        if (r.Points.Count == 0) return 0x1F;                                             // L5-06 (0xA11888..0xA118B0)
        var key = (node.SubscriptionKey10, r.ParamId);
        if (!_subscriptionCurves.TryGetValue(key, out var curves))
        {
            curves = new List<WwiseRtpc>();
            _subscriptionCurves[key] = curves;
            _rtpc.AddSubscription(new WwiseRtpcSubscription                               // 0xA117C8: the new entry (L5-04)
            {
                Key1 = node.SubscriptionKey10, Param = r.ParamId, Type = 2, Accumulate = r.Accumulate, Curves = curves,
            });
        }
        if (curves.Exists(c => c.CurveId == r.CurveId))
            throw Missing("a repeated curve id under one (node, parameter): the replaced curve is re-notified by 0xA0E284 / 0x9E63A0 (L5-07)");
        curves.Add(r);                                                                    // 0xA11C50..0xA11D14: appended
        int ar = (_seams.RtpcAfterAdd ?? throw Missing("the steps after a curve is added (0xA0F07C, 0xA0F990, 0xA11624 / 0xA11D58; L5-08)"))(node, r);
        if (ar != 1) return ar;
        if (r.ParamId > 63) throw Missing($"the mask A bit of RTPC parameter {r.ParamId} (0xA1A160)");
        reg.MaskA |= 1UL << (int)r.ParamId;                                               // 0xA1A18C..0xA1A1D4
        if (r.SourceType == 2) reg.Byte1C = 1;                                            // 0xA1A408..0xA1A418
        if (node.IsBus ? r.ParamId is >= 29 and <= 33 : false)
            throw Missing("the bus vt+0x80 = 0x9C2EAC for an RTPC parameter 29..33");
        if (!node.IsBus && node.Category44 is not (WwiseNodeCategory44.Sound or WwiseNodeCategory44.ActorMixer))
            throw Missing($"the vt+0x80 of a category-{node.Category44} node");
        return 1;                                                                         // vt+0x80: 0x980EE8 is bx lr
    }
}
