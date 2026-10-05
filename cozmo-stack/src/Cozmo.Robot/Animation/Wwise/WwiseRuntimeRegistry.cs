// fidelity: M6-001, M6-025
namespace Cozmo.Robot.Animation.Wwise;

// The runtime object registry the engine's HIRC loaders build (B-M6b-4 batch 6c, C44.1 B1..B6). Sources: re-analysis/inventory/M6-wwise-bank.md Correction C44.1 and
// re-analysis/research/20261005-B-M6b-4-hirc-graph-15.md with its Verification (V1..V9).
//
// What is built: the eleven tables (REG + 0x00 A, +0x14 B, +0x28 State, +0x3C Event, +0x50 Action, +0x78 Attenuation, +0x8C LFO, +0xB4 Dialogue, +0xC8 FxShareSet, +0xDC FxCustom,
// +0xF0 AudioDevice, B4), a per-object reference count [+0xC] that every AddRef and lookup shares (B1, B5, B6), the lookup that increments it on a hit for BOTH flags (0x9A7EB0, B5),
// and the head insertion into the id's chain (0x9F40F4, B4).
//
// What is NOT built, and why that is a decision and not an omission: the bucket arrays and the 0.9 rehash (literal 0x3F666666, primes from the table at 0xFFA650). Nothing in the
// stack iterates a table (every consumer looks an object up by id, and a chain only matters to a lookup of an id that appears twice, which the head insertion below keeps
// faithful), so bucket order is not observable and the rehash is not built. The bucket count, the initial prime and the order in which a rehash relinks a chain are not in the inventory either.
// Locks (0x4D3064) are not modelled: the loader is single-threaded.

/// <summary>The eleven registry tables, at their offsets from the registry base (<c>REG</c>, C44.1 B4).</summary>
public enum WwiseRegistryTable
{
    /// <summary>REG + 0x00: the node table (Sound, ActorMixer, RanSeq, Switch, Layer, music nodes).</summary>
    A = 0x00,
    /// <summary>REG + 0x14: the bus table.</summary>
    B = 0x14,
    /// <summary>REG + 0x28.</summary>
    State = 0x28,
    /// <summary>REG + 0x3C.</summary>
    Event = 0x3C,
    /// <summary>REG + 0x50.</summary>
    Action = 0x50,
    /// <summary>REG + 0x78.</summary>
    Attenuation = 0x78,
    /// <summary>REG + 0x8C (LFO / envelope).</summary>
    Lfo = 0x8C,
    /// <summary>REG + 0xB4.</summary>
    Dialogue = 0xB4,
    /// <summary>REG + 0xC8.</summary>
    FxShareSet = 0xC8,
    /// <summary>REG + 0xDC.</summary>
    FxCustom = 0xDC,
    /// <summary>REG + 0xF0.</summary>
    AudioDevice = 0xF0,
}

/// <summary>
/// An object of the registry: <c>0x9D0418(obj, id)</c> sets <c>[+8] = id</c> and <c>[+0xC] = 1</c> (C44.1 B1). The reference count is ONE field: the node AddRef <c>0x9F1CBC</c> (<c>vt+8</c>), the lookup <c>0x9A7EB0</c> and the
/// HIRC walker's found-existing paths all increment it, and the PBI table (<see cref="WwiseNodeRefTable"/>) increments the same field.
/// </summary>
public abstract class WwiseRegistryObject
{
    /// <summary><c>[obj+8]</c>.</summary>
    public uint Id { get; init; }

    /// <summary><c>[obj+0xC]</c>: the reference count, 1 after the object base constructor (<c>0x9D0418</c>).</summary>
    public int RefCount0C { get; set; } = 1;

    /// <summary>The table the object registers itself in (the table it is removed from when the count reaches zero, <c>0x9F5060..0x9F5168</c>).</summary>
    public abstract WwiseRegistryTable Table { get; }

    /// <summary><c>vt+8</c> (<c>0x9F1CBC</c>, C44.1 B6): <c>[obj+0xC]++</c> (the lock choice by <c>[node+0x46]</c> bit 2 is not observable).</summary>
    public void AddRef9F1CBC() => RefCount0C++;
}

/// <summary>An Event (<c>0x9CC96C</c>, C44.1 D12): <c>[+0x10]</c> the first action, <c>[+0x14]</c> zero; registered in the Event table only after a successful init.</summary>
public sealed class WwiseRuntimeEvent : WwiseRegistryObject
{
    /// <inheritdoc/>
    public override WwiseRegistryTable Table => WwiseRegistryTable.Event;

    /// <summary><c>[+0x10]</c>: the head of the action chain (<c>0x9CD0D8..</c>: each action's <c>[+0x10]</c> is the next).</summary>
    public WwiseRuntimeAction? FirstAction10 { get; set; }

    /// <summary><c>[+0x14]</c>: zero after the constructor.</summary>
    public uint Field14 { get; set; }
}

/// <summary>An Action (<c>0xA60ADC</c> base, <c>0xA62E54</c> Play creator, C44.1 D13).</summary>
public sealed class WwiseRuntimeAction : WwiseRegistryObject
{
    /// <inheritdoc/>
    public override WwiseRegistryTable Table => WwiseRegistryTable.Action;

    /// <summary><c>[+0x10]</c>: the next action of an event's chain (zeroed by the constructor and by the event init, <c>0x9CD0D8</c>).</summary>
    public WwiseRuntimeAction? Next10 { get; set; }

    /// <summary><c>[+0x14]</c>: the base property bundle (4-byte entries), rebuilt by the init (<c>0xA613F4..0xA61564</c>); null when the count byte is 0.</summary>
    public WwiseParamBundle? BaseBundle14 { get; set; }

    /// <summary><c>[+0x18]</c>: the ranged property bundle (8-byte entries); null when the count byte is 0.</summary>
    public WwiseParamBundle? RangedBundle18 { get; set; }

    /// <summary><c>[+0x1C]</c>: the target id (<c>0xA60268</c>).</summary>
    public uint TargetId1C { get; set; }

    /// <summary><c>[+0x20]</c> (u16): the action type (<c>0xA60AEC</c>).</summary>
    public ushort Type20 { get; init; }

    /// <summary><c>[+0x22]</c>: bits 0..4 the fade curve (the constructor stores 4), bit 5 the Play class's init-done mark, bit 6 the isBus flag.</summary>
    public byte Byte22 { get; set; } = 4;

    /// <summary><c>[+0x24]</c> (Play): the bank id; -1 after the Play creator (<c>0xA62E54</c>).</summary>
    public uint BankId24 { get; set; } = 0xFFFFFFFFu;
}

/// <summary>
/// An FX share set (<c>0x9CEA28</c>, REG + 0xC8) or custom instance (<c>0x9CF038</c>, REG + 0xDC): <c>[+0x10] = -1</c> (the plug-in id), <c>[+0x14..0x38] = 0</c> (C44.1 D14). The init <c>0x9CE3B8</c> (plug-in id, parameter block) is not read:
/// it is a required seam of the loader.
/// </summary>
public sealed class WwiseRuntimeFx : WwiseRegistryObject
{
    /// <summary>True for a custom instance (REG + 0xDC), false for a share set (REG + 0xC8).</summary>
    public bool Custom { get; init; }

    /// <inheritdoc/>
    public override WwiseRegistryTable Table => Custom ? WwiseRegistryTable.FxCustom : WwiseRegistryTable.FxShareSet;

    /// <summary><c>[+0x10]</c>: the plug-in id, -1 after the constructor.</summary>
    public uint PluginId10 { get; set; } = 0xFFFFFFFFu;
}

/// <summary>The registry <c>REG</c> (C44.1 B4, B5).</summary>
public sealed class WwiseRuntimeRegistry
{
    private readonly Dictionary<WwiseRegistryTable, Dictionary<uint, List<WwiseRegistryObject>>> _tables = new();

    /// <summary>The element count of a table (<c>[table+0x10]</c>, <c>0x9F41B8..0x9F41C8</c>).</summary>
    public int Count(WwiseRegistryTable table) => _tables.TryGetValue(table, out var t) ? t.Values.Sum(c => c.Count) : 0;

    /// <summary>
    /// The registration insert of <c>0x9F40F4</c> (C44.1 B4): the object goes at the HEAD of its id's chain (<c>[node+4] = old head; head = node</c>) and the element count is incremented. The rehash is not built (see the file note).
    /// </summary>
    public void Insert(WwiseRegistryObject obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        if (!_tables.TryGetValue(obj.Table, out var t)) _tables[obj.Table] = t = new();
        if (!t.TryGetValue(obj.Id, out var chain)) t[obj.Id] = chain = new();
        chain.Insert(0, obj);
    }

    /// <summary><c>0x9F40F4(node)</c> (C44.1 B4): the table is chosen by <c>[node+0x46]</c> bit 2 (<c>0x9A80B8</c>): clear is table A, set is table B.</summary>
    public void Register9F40F4(WwiseRoutingNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Insert(node);
    }

    /// <summary>The object with <paramref name="id"/> in <paramref name="table"/> (the first in its chain), or null. Does not touch the reference count.</summary>
    public WwiseRegistryObject? Find(WwiseRegistryTable table, uint id)
        => _tables.TryGetValue(table, out var t) && t.TryGetValue(id, out var chain) ? chain[0] : null;

    /// <summary>
    /// <c>0x9A7EB0(REG, id, flag)</c> (C44.1 B5): flag 0 searches table A, any other flag table B; a hit increments <c>[obj+0xC]</c> for BOTH flags (<c>0x9A7F14..0x9A7F1C</c> and <c>0x9A7F7C..0x9A7F88</c>) and returns it.
    /// </summary>
    public WwiseRegistryObject? Lookup9A7EB0(uint id, int flag)
        => LookupAndAddRef(flag == 0 ? WwiseRegistryTable.A : WwiseRegistryTable.B, id);

    /// <summary>
    /// The lookup the walker's inline searches make (Event 0x9B3134..0x9B3190, Action 0x9B3074, FxShareSet 0x9B3FBC, ActorMixer 0x9B3FD4): a hit increments <c>[obj+0xC]</c>.
    /// </summary>
    public WwiseRegistryObject? LookupAndAddRef(WwiseRegistryTable table, uint id)
    {
        var o = Find(table, id);
        o?.AddRef9F1CBC();
        return o;
    }

    /// <summary>Removes the object from its table (the table removal of the release at zero, <c>0x9F5060..0x9F5168</c>); false when it was not registered.</summary>
    public bool Remove(WwiseRegistryObject obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        if (!_tables.TryGetValue(obj.Table, out var t) || !t.TryGetValue(obj.Id, out var chain)) return false;
        bool removed = chain.Remove(obj);
        if (chain.Count == 0) t.Remove(obj.Id);
        return removed;
    }

    /// <summary>Every routing node of table B (the buses), in no particular order (the engine's bit-6 propagation <c>0x9C5598</c> follows <c>[bus+0x58]</c>; the model's helper filters this list by <c>OutputBus</c>).</summary>
    public IEnumerable<WwiseRoutingNode> Buses()
        => _tables.TryGetValue(WwiseRegistryTable.B, out var t) ? t.Values.SelectMany(c => c).OfType<WwiseRoutingNode>() : Enumerable.Empty<WwiseRoutingNode>();

    /// <summary>Every object of a table, in no particular order (a convenience for tests; the engine's code never iterates a table here).</summary>
    public IEnumerable<WwiseRegistryObject> Objects(WwiseRegistryTable table)
        => _tables.TryGetValue(table, out var t) ? t.Values.SelectMany(c => c) : Enumerable.Empty<WwiseRegistryObject>();

    /// <summary>The runtime node (or bus) with the id of a parsed hierarchy node: table B for a bus, table A otherwise. The <c>RuntimeNodeOf</c> hook of the limiter and the play path.</summary>
    public WwiseRoutingNode? RuntimeNodeOf(WwiseNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Find(node.Type == WwiseObjectType.AudioBus ? WwiseRegistryTable.B : WwiseRegistryTable.A, node.Id) as WwiseRoutingNode;
    }

    /// <summary>
    /// The bus AddRef <c>bus-&gt;vt+8</c> (<c>0xA4F0EC</c>, <c>0xA4F1B4..0xA4F1C0</c>) as a <see cref="WwiseVoiceLinkSeams.BusAddRefVt8"/> value: <c>[bus+0xC]++</c>, the same field the graph loader and the PBI table write.
    /// </summary>
    public static readonly Action<WwiseRoutingNode> BusAddRef = bus => bus.AddRef9F1CBC();
}
