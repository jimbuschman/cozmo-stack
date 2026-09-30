// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// A 64-bit output-device id (<c>entry+0x10/+0x14</c>, <c>conn+0x48/+0x4C</c>, <c>vpl+0x28/+0x2C</c>): the
/// main device is (lo 2, hi 0) (M6-025 C23 item 1 rows 5-7, rows C23.20/C23.21: <c>0x9EBCE4/0x9EBCE8</c>).
/// </summary>
public readonly record struct WwiseDeviceId(uint Lo, uint Hi)
{
    /// <summary>The main output device (2,0) (C23.20).</summary>
    public static readonly WwiseDeviceId Main = new(2, 0);

    /// <summary>True for the main device: the <c>cmp r5,#0; cmpeq r4,#2</c> at <c>0xA42FC0</c>.</summary>
    public bool IsMain => Lo == 2 && Hi == 0;
}

/// <summary>
/// One output-device list entry (C23 item 1 row 5: <c>+0x10/+0x14</c> id, <c>+0x18</c> listener mask,
/// <c>+0x1C</c> channel-config word).
/// </summary>
public sealed class WwiseOutputDeviceEntry
{
    // fidelity: M6-025

    /// <summary><c>+0x10/+0x14</c>.</summary>
    public WwiseDeviceId Id { get; init; }

    /// <summary><c>+0x18</c>: the listener mask.</summary>
    public uint ListenerMask { get; set; }

    /// <summary>
    /// <c>+0x1C</c>: the channel-config word. The main device holds the constant
    /// <see cref="WwiseOutputDeviceList.MainConfigWord"/> (C23.18/C23.23).
    /// </summary>
    public uint ConfigWord { get; set; }

    /// <summary>
    /// The device table (base <c>[E+0x58]</c>, count <c>[E+0x5C]</c>): 8-byte <c>{key, built object}</c> entries (C24.5).
    /// The row-19 caller scans it for a key before calling <c>0x9EA23C</c> (C25.6); the find-or-insert body appends to it.
    /// </summary>
    // fidelity: M6-025
    public List<WwiseDeviceTableEntry> Table { get; } = new();
}

/// <summary>One 8-byte <c>{key, built object}</c> entry of a device entry's table (C24.5, C25.6).</summary>
// fidelity: M6-025
public sealed class WwiseDeviceTableEntry
{
    /// <summary>The key (<c>+0</c>).</summary>
    public uint Key { get; init; }

    /// <summary>The built object (<c>+4</c>); null is the <c>{key, 0}</c> a failed build leaves before it is removed.</summary>
    public object? Built { get; set; }
}

/// <summary>
/// The output-device list (<c>0x108DAE8</c>, head <c>0x108DB04</c>, entries linked at <c>+4</c>; C23 item 1
/// row 5, C23.21). Devices are appended at the tail.
/// </summary>
public sealed class WwiseOutputDeviceList
{
    // fidelity: M6-025

    /// <summary>
    /// The main device's <c>entry+0x1C</c> word: <c>0x00003102</c> (2 channels, standard config, mask 3;
    /// C23.18 <c>0x8D8188..0x8D8192</c>, <c>0x99DCBC..0x99DCE4</c>, <c>0xA571CC..0xA57210</c>) and on the
    /// dummy sink also (C23.23 <c>0x9E9698</c>).
    /// </summary>
    public const uint MainConfigWord = 0x00003102;

    /// <summary>The main device's listener mask: <c>0xFF</c> (C23.21 <c>0x9EBDBC mov r1,#0xff</c>).</summary>
    public const uint MainListenerMask = 0xFF;

    private readonly List<WwiseOutputDeviceEntry> _entries = new();

    /// <summary>The entries in list order (head first).</summary>
    public IReadOnlyList<WwiseOutputDeviceEntry> Entries => _entries;

    /// <summary>
    /// <c>0x9EBC50</c> with an empty list and plug-in id 0 (C23.20): creates the main device (2,0) with mask
    /// 0xFF and config word 0x3102. Whether the OpenSL sink or the dummy sink backs it is HARDWARE_ONLY and
    /// never changes the value (C23.23).
    /// </summary>
    public WwiseOutputDeviceEntry CreateMainDevice()
        => Add(WwiseDeviceId.Main, MainListenerMask, MainConfigWord);

    /// <summary>
    /// <c>0x9EB10C</c> success path (C23.21): append at the tail, the mask stored at <c>entry+0x18</c>; for a
    /// non-zero mask the first (2,0) entry has the mask's bits cleared (<c>0x9EB450..0x9EB454</c>), and for
    /// (2,0) itself the equal-id path restores the mask (<c>0x9EB48C/0x9EB490</c>), a net 0xFF.
    /// </summary>
    public WwiseOutputDeviceEntry Add(WwiseDeviceId id, uint listenerMask, uint configWord)
    {
        var entry = new WwiseOutputDeviceEntry { Id = id, ListenerMask = listenerMask, ConfigWord = configWord };
        _entries.Add(entry);
        if (listenerMask != 0)
        {
            var main = _entries.FirstOrDefault(e => e.Id.IsMain);
            if (main is not null)
            {
                if (id.IsMain) main.ListenerMask = listenerMask;      // 0x9EB48C/0x9EB490: net 0xFF
                else main.ListenerMask &= ~listenerMask;              // 0x9EB450..0x9EB454
            }
        }
        return entry;
    }

    /// <summary>Finds the first entry with <paramref name="id"/> (the id compare of <c>0xA42E34..0xA42E44</c>).</summary>
    public WwiseOutputDeviceEntry? Find(WwiseDeviceId id) => _entries.FirstOrDefault(e => e.Id == id);
}

/// <summary>
/// The node-side routing data <c>vt+0x88</c> reads (C23 item 1 row 10): <c>[node+0x38]</c> the output bus,
/// <c>[node+0x34]</c> the parent, and for a bus the fields of the <c>0x9C2A30</c> predicate. The per-bus
/// values for the shipped buses are RECOVERABLE_GAP (Init.bnk bus fields <c>+0x28/+0x40/+0x46/+0x54/+0x68</c>),
/// so the caller fills them; this class owns only the predicate and the walk.
/// </summary>
public sealed class WwiseRoutingNode
{
    // fidelity: M6-025

    /// <summary><c>[node+8]</c>: the object id (a bus's id is what <c>0xA68A2C</c> keys on).</summary>
    public uint Id { get; init; }

    /// <summary>True for a bus (vtable slot <c>0x9C2A30</c>); false for Sound/RanSeq/Switch/ActorMixer/Layer (<c>0x9F1E3C</c>).</summary>
    public bool IsBus { get; init; }

    /// <summary><c>[node+0x38]</c>: the output bus (for a bus, its parent bus, C23 row 10).</summary>
    public WwiseRoutingNode? OutputBus { get; set; }

    /// <summary><c>[node+0x34]</c>: the parent node.</summary>
    public WwiseRoutingNode? Parent { get; set; }

    /// <summary>Bus: any FX entry at <c>[bus+0x28]+{4,0xC,0x14,0x1C}</c> is non-zero.</summary>
    public bool AnyFxEntryNonZero { get; set; }

    /// <summary>Bus: <c>bus-&gt;vt+0x44() == 0xC</c> (the meaning of the value is UNKNOWN).</summary>
    public bool Vt44Is0C { get; set; }

    /// <summary>
    /// Bus: <c>[bus+0x68]</c>, the bus channel-config word <c>0xA68A94</c> returns (C24.3). The value per shipped
    /// bus comes from Init.bnk (Cozmo_Robot and Robot_Bus_1..4 hold <c>0x4101</c>); the caller fills it.
    /// </summary>
    public uint Word68 { get; set; }

    /// <summary>Bus: <c>[bus+0x68]</c> (its low byte, the field the <c>0x9C2A30</c> predicate tests).</summary>
    public byte Byte68
    {
        get => (byte)Word68;
        set => Word68 = (Word68 & ~0xFFu) | value;
    }

    /// <summary>Bus: <c>[bus+0x46]</c>; bit7 is tested here and bit7/bits3..4 by <c>0xA689B8</c>.</summary>
    public byte Byte46 { get; set; }

    /// <summary>Bus: <c>[bus+0x40]</c>; <c>&amp; 0xE0000</c> is tested here, at <c>0xA42210</c> and <c>0x9BC9FC</c>.</summary>
    public uint Word40 { get; set; }

    /// <summary>Bus: <c>[bus+0x54]</c>.</summary>
    public uint Word54 { get; set; }

    /// <summary>Bus: <c>[bus+0xCC]</c> bit6, the main-vs-secondary flag (C23 row 11).</summary>
    public bool Bit6 { get; set; }

    /// <summary>
    /// <c>vt+0x88</c> (C23 item 1 row 10). A bus returns itself when any predicate holds
    /// (<c>0x9C2A30..0x9C2AD8</c>), else tail-jumps to <c>0x9F1E3C</c> on itself; every node class otherwise
    /// runs <c>0x9F1E3C</c>: the output bus's <c>vt+0x88</c> if non-null, else the parent's, else null.
    /// </summary>
    public WwiseRoutingNode? Vt88()
    {
        if (IsBus)
        {
            if (AnyFxEntryNonZero || Vt44Is0C || Byte68 != 0 || (Byte46 & 0x80) != 0
                || OutputBus is null || (Word40 & 0xE0000) != 0 || Word54 != 0)
                return this;
        }
        if (OutputBus is not null) return OutputBus.Vt88();       // 0x9F1E3C: [node+0x38]
        if (Parent is not null) return Parent.Vt88();             // [node+0x34]
        return null;
    }
}

/// <summary>
/// The Bus SetInitialValues <c>0x9C3FFC</c> writers of <c>bus+0xCC</c> bit6 (C23 item 1 row 11 and the check's
/// row 5.7): the first parentless bus is the master (bit6 = 1); a second parentless bus, only when the second
/// slot <c>[0x108D9B0+0x10]</c> is empty, gets bit6 = 0; a bus that gets a parent inherits its parent's bit6
/// down its subtree (<c>0x9C5598</c>).
/// </summary>
public sealed class WwiseMasterBusRegistry
{
    // fidelity: M6-025

    /// <summary><c>[0x108D9B0+4]</c>: the master bus.</summary>
    public WwiseRoutingNode? Master { get; private set; }

    /// <summary><c>[0x108D9B0+0x10]</c>: the second parentless bus.</summary>
    public WwiseRoutingNode? Secondary { get; private set; }

    /// <summary>Registers a parentless bus (<c>[r1+4] == 0</c>).</summary>
    public void RegisterParentless(WwiseRoutingNode bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        if (Master is null)                                      // 0x9C4314..0x9C4328
        {
            Master = bus;
            bus.Bit6 = true;
        }
        else if (!ReferenceEquals(bus, Master) && Secondary is null)   // 0x9C4038..0x9C4064
        {
            Secondary = bus;
            bus.Bit6 = false;
        }
    }

    /// <summary>
    /// <c>0x9C5820</c> tail <c>b 0x9C5598</c>: when <paramref name="bus"/> gets <paramref name="parent"/> and
    /// the parent's bit6 differs, the parent's bit6 is stored on the bus and recursively on its children.
    /// </summary>
    public static void InheritFromParent(WwiseRoutingNode bus, WwiseRoutingNode parent, IEnumerable<WwiseRoutingNode> children)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(parent);
        if (bus.Bit6 == parent.Bit6) return;
        Propagate(bus, parent.Bit6, children);
    }

    private static void Propagate(WwiseRoutingNode bus, bool bit6, IEnumerable<WwiseRoutingNode> children)
    {
        bus.Bit6 = bit6;
        foreach (var child in children.Where(c => ReferenceEquals(c.OutputBus, bus)))
            Propagate(child, bit6, children);
    }
}

/// <summary>
/// A bus context <c>{u32 busPtr, u32 key2, u8 byte}</c> (C23 item 1 rows 9 and 12). The default context
/// (<c>0xA68950</c>) is <c>{0, -1, byte 1}</c>.
/// </summary>
public readonly record struct WwiseBusContext(WwiseRoutingNode? Bus, int Key2, byte Byte)
{
    /// <summary>The initial <c>{0, -1, 0}</c> of <c>0xA42C6C..0xA42C8C</c> and <c>0xA689B8</c>.</summary>
    public static readonly WwiseBusContext None = new(null, -1, 0);

    /// <summary>The static default <c>0xA68950()</c> = <c>{0, -1, byte 1}</c> (<c>strb 1</c> at <c>0xA6897C</c>).</summary>
    public static readonly WwiseBusContext Default = new(null, -1, 1);

    /// <summary><c>0xA68A2C</c>: <c>busPtr ? [busPtr+8] : -byte</c>.</summary>
    public uint Key => Bus is not null ? Bus.Id : unchecked((uint)-(int)Byte);
}

/// <summary>
/// What <c>0xA42DEC</c> reads through the PBI (<c>pbi+0xE0</c>, <c>[pbi+0x14]</c>, <c>pbi+0xE9</c>). The
/// writers of these in the C# PBI are not in the C23 rows (<c>0x9BC90C</c>/<c>0xA19CDC</c> context init), so the
/// linker takes them from a caller function; the defaults are the game-object constructor's.
/// </summary>
public sealed class WwisePbiRouting
{
    // fidelity: M6-025

    /// <summary>
    /// <c>[pbi+0xE0]</c>: the node whose <c>vt+0x88</c> gives the output bus. It is never null: the native
    /// dereferences it (<c>0xA42FA0 ldr r0,[sl,#0xe0]; 0xA42FA4 ldr r3,[r0]</c>, <c>0x9BD150..0x9BD154</c>; C24.9), and
    /// the "no bus" case is <c>vt+0x88</c> returning 0 (<see cref="WwiseRoutingNode.Vt88"/> returning null).
    /// </summary>
    public required WwiseRoutingNode Node { get; init; }

    /// <summary>
    /// <c>[[pbi+0x14]+0x22]</c>: the game object's listener mask; the default is 1 (C23 item 1 row 7:
    /// <c>0xA0B340 mov r6,#1</c>, <c>0xA0B3CC strb r6,[r4,#0x22]</c>).
    /// </summary>
    public byte ListenerMask { get; init; } = 1;

    /// <summary>
    /// <c>[[pbi+0x14]+0x78]</c>: the game object's key, <c>-1</c> from the constructor (<c>0xA0B3C8</c>, row 9).
    /// </summary>
    public int GameObjectKey78 { get; init; } = -1;

    /// <summary><c>pbi+0xE9</c> bit2: <c>0x9BD144</c> returns no bus when set (row 9).</summary>
    public bool NoBusOverride { get; init; }

    /// <summary><c>pbi+0xE9</c> bit3: <c>0xA4C280</c> walks the <c>+0x1C8</c> chain for <c>+0x1CC</c> bit1 (row 16).</summary>
    public bool ChainWalk { get; init; }
}

/// <summary>The arguments of the line Init <c>0xA4F0EC</c> (C24.3).</summary>
/// <param name="Key">The reuse key stored on the line (context key, key2, device id).</param>
/// <param name="Bus">The context's bus (<c>self</c>, <c>[line+0x30]</c>); null for the default context.</param>
/// <param name="CfgA">Stored at <c>[line+0x64]</c>; also the buffer's channel count byte.</param>
/// <param name="CfgB">Stored at <c>[line+0x44]</c>.</param>
/// <param name="Frames">The u16 frame count stored at <c>[line+0x6C]</c>.</param>
/// <param name="Context">The bus context passed by value (<c>[line+0x48] = 0xA68A2C(ctx)</c>).</param>
/// <param name="Device">The device id (u64).</param>
public sealed record WwiseLineInitArgs(
    WwiseMixBusKey Key, WwiseRoutingNode? Bus, uint CfgA, uint CfgB, int Frames, WwiseBusContext Context, WwiseDeviceId Device);

/// <summary>
/// The callee bodies the C23 rows leave unread or unsettled on the connection path, named so a missing one
/// throws <see cref="NotSupportedException"/> when the path reaches it instead of defaulting.
/// </summary>
public sealed class WwiseVoiceLinkSeams
{
    /// <summary><c>0xA54A30(voice)</c>, the voice init (<c>voice+0xCD</c> bit0 gate); body RECOVERABLE_GAP; must return 1.</summary>
    public Func<WwiseLiveVoice, int>? InitVoiceA54A30 { get; set; }

    /// <summary>
    /// The bus-line Init <c>0xA4F0EC(line, self, cfgA, cfgB, u16 frames, ctx, device)</c> (C24.3): allocates the
    /// line, its buffer <c>(cfgA &amp; 0xFF) * frames * 4</c> and the FX holder, and returns 2 when
    /// <c>bus-&gt;vt+0x98(3) == 0</c>, <c>0x34</c> on a buffer or holder allocation failure, else 1; any other
    /// result than 1 destroys the line and <c>0xA42210</c> returns 0. On 1 the caller stores
    /// <c>Format64 = cfgA</c> and <c>+0x44 = cfgB</c> on the returned line. The FX chain and the allocation
    /// are the seam's (the line class is built there).
    /// </summary>
    public Func<WwiseLineInitArgs, (int Result, WwiseMixBus? Line)>? InitLineA4F0EC { get; set; }

    /// <summary>
    /// <c>0x9C39DC(bus, 0, 5)</c>, the Bus Volume param 5 read that <c>0xA68A44</c> makes for a non-null bus
    /// (C24.6); body unread. A null bus never reaches it (<c>0xA68A44</c> returns 0.0f).
    /// </summary>
    public Func<WwiseRoutingNode, float>? BusVolumeParam5 { get; set; }

    /// <summary>
    /// <c>[[vpl+0x1A8]+0xC]</c> create (<c>0xA4E324</c>) and <c>mixbus-&gt;vt+0x20(conn)</c> (row 18); bodies
    /// unread. Called only when the line has a <see cref="WwiseMixBus.OutputMixObject1A8"/>.
    /// </summary>
    public Action<WwiseMixBus, object>? MixObjectAddInput { get; set; }

    /// <summary>
    /// <c>mixobj-&gt;vt+0x24(conn)</c>, the disconnect mirror of <see cref="MixObjectAddInput"/> (C24.6,
    /// <c>0xA4F6F0..0xA4F71C</c>); body unread. Called only when the line has a
    /// <see cref="WwiseMixBus.OutputMixObject1A8"/> whose <see cref="WwiseMixBus.MixObject1A8C"/> is non-null.
    /// </summary>
    public Action<WwiseMixBus, object>? MixObjectRemoveInput { get; set; }

    /// <summary>
    /// <c>0x9EA23C(deviceEntry, key)</c> (C24.5, C25.6). The row-19 caller scans the entry's <see cref="WwiseOutputDeviceEntry.Table"/>
    /// itself first (<c>0xA4C3D8..0xA4C410</c>, <c>0xA4C4D8..0xA4C504</c>) and calls this only when the key is absent, so
    /// a found key never reaches it. Its own contract (<c>0x9EA25C..0x9EA2C4</c>) is find-or-insert: it rescans and,
    /// on a hit, zeroes the value and rebuilds; on a miss it appends <c>{key, 0}</c> to the table first; it builds
    /// through <c>0xA22A3C</c> (<c>0xA22304</c> / <c>0xA22684</c>, unread), returns 1 when the built value is non-zero,
    /// else removes the entry and returns 2. Any other result than 1 destroys the new connection. The body is the
    /// seam's (the table mutation included).
    /// </summary>
    public Func<WwiseOutputDeviceEntry, uint, int>? DeviceTableFindOrInsert9EA23C { get; set; }

    /// <summary>The connection mixer's input/output channel counts (<c>0xA6F90C</c> rows give none).</summary>
    public Func<WwiseLiveVoice, WwiseMixBus, (int Input, int Output)>? ConnectionChannels { get; set; }

    /// <summary><c>[source+0x10]</c> bit1 (<c>0xA54584..0xA54588</c>); its writer is not in the rows.</summary>
    public Func<IWwiseVoiceSource, bool>? SourceFlag10Bit1 { get; set; }

    /// <summary><c>0xA0428C</c> (called at <c>0xA545CC</c>); body and arguments unread.</summary>
    public Action<WwiseLiveVoice, WwisePlayingInstance>? A0428C { get; set; }

    /// <summary>
    /// <c>0xA56414(source, 0)</c> then <c>source vt[0]</c> and the pool free (Term, row 5.17): the PBI
    /// <c>0xA054D8</c> lookup and <c>0xA01800</c> (<c>pbi+0x154 = 0</c>, notification code 4) live behind it.
    /// </summary>
    public Action<IWwiseVoiceSource>? CloseSource56414 { get; set; }

    /// <summary>
    /// <c>0xA53244(voice+0x100)</c> and <c>0xA76608(voice+0x1D0/+0x3A0)</c> (Term, row 5.17/5.20): buffer
    /// releases with unread bodies (<c>0xA69A38</c>, <c>0xA69AC8</c>, <c>0xA47360</c>). Optional: the C# voice
    /// owns managed buffers, so no observable state is missed; the hook exists for the wiring.
    /// </summary>
    public Action<WwiseLiveVoice>? TermBuffers { get; set; }
}

/// <summary>
/// The voice-to-bus connection creation (M6-025, C23 item 1 and 5): <c>0xA42DEC</c>, <c>0xA42C60</c>,
/// <c>0xA429F0</c>/<c>0xA689B8</c>, <c>0xA42210</c>, <c>0xA42754</c>, <c>0xA4C280</c>, the connection ctor
/// <c>0xA6F90C</c>, <c>0xA4F664</c>, the live-voice insertion, the deferred pending-voice list
/// (<c>0xA431A8</c>, <c>0xA544BC</c>) and the teardown <c>0x9D40C4</c>/<c>0xA53EA8</c>/<c>0xA55F2C</c>.
///
/// <para>Whatever the C23 rows do not settle is a named seam in <see cref="WwiseVoiceLinkSeams"/> that
/// throws <see cref="NotSupportedException"/> when reached and unset. See the class members for the rows.</para>
/// </summary>
public sealed class WwiseVoiceLinker
{
    // fidelity: M6-025

    private readonly Func<WwisePlayingInstance, WwisePbiRouting> _routingFor;

    /// <param name="buses">The line array <c>0x108DF54</c>.</param>
    /// <param name="devices">The output-device list <c>0x108DAE8</c>.</param>
    /// <param name="liveVoices">The live-voice list (head <c>0x108DF68</c>, index 0 is the head).</param>
    /// <param name="routingFor">The PBI's routing fields (<c>pbi+0xE0</c>, <c>[pbi+0x14]</c>, <c>pbi+0xE9</c>).</param>
    /// <param name="seams">The unread callee bodies.</param>
    public WwiseVoiceLinker(
        WwiseMixBusHierarchy buses, WwiseOutputDeviceList devices, List<WwiseLiveVoice> liveVoices,
        Func<WwisePlayingInstance, WwisePbiRouting> routingFor, WwiseVoiceLinkSeams seams)
    {
        Buses = buses ?? throw new ArgumentNullException(nameof(buses));
        Devices = devices ?? throw new ArgumentNullException(nameof(devices));
        LiveVoices = liveVoices ?? throw new ArgumentNullException(nameof(liveVoices));
        _routingFor = routingFor ?? throw new ArgumentNullException(nameof(routingFor));
        Seams = seams ?? throw new ArgumentNullException(nameof(seams));
    }

    /// <summary><c>[pbi+0xE0]</c>, the routing node (C27 step 4 passes it to <c>0x9EEDA4</c> and <c>vt+0x120</c>).</summary>
    public WwiseRoutingNode RoutingNodeE0(WwisePlayingInstance pbi) => _routingFor(pbi).Node;

    /// <summary>The line array <c>0x108DF54</c>.</summary>
    public WwiseMixBusHierarchy Buses { get; }

    /// <summary>The output-device list.</summary>
    public WwiseOutputDeviceList Devices { get; }

    /// <summary>The live-voice list the voice pass walks; a voice joins it only in <see cref="Link"/> (C23.4).</summary>
    public List<WwiseLiveVoice> LiveVoices { get; }

    /// <summary>
    /// The pending-voice list <c>0x108DA2C/0x108DA30</c> (C23.4): a voice whose <c>AddSrc</c> returned 0x3F
    /// waits here; the voice pass does not walk it.
    /// </summary>
    public List<WwiseLiveVoice> PendingVoices { get; } = new();

    /// <summary>The unread callee bodies.</summary>
    public WwiseVoiceLinkSeams Seams { get; }

    /// <summary>
    /// The u16 global <c>[0x1052440]</c> (<c>.data</c> initial value 0x400, C24 residuals): the <c>frames</c> argument of
    /// the line Init <c>0xA4F0EC</c> (C24.3) and the argument of the pending-voice walk <c>0x9D3CC0</c> (loaded at
    /// <c>0xA44970..0xA44974</c>, C25.7). Its runtime writer is not traced, so the line buffer size
    /// <c>(cfgA &amp; 0xFF) * frames * 4</c> is unconfirmed (RECOVERABLE_GAP).
    /// </summary>
    // fidelity: M6-025
    public ushort LineMaxFrames { get; set; } = 0x400;

    // ---------------------------------------------------------------- 0xA42DEC

    /// <summary>
    /// <c>0xA42DEC(voice, pbi)</c> (C23 item 1 rows 4-6, 20; item 5 rows 5.1-5.10). Returns 1 on success and
    /// 2 after the voice-init failure teardown (<c>0xA42FFC..0xA4300C</c>).
    /// </summary>
    public int Link(WwiseLiveVoice voice, WwisePlayingInstance pbi)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentNullException.ThrowIfNull(pbi);

        // 0xA42DF4..0xA42FFC: voice+0xCD bit0 set -> 0xA54A30(voice) must return 1, else 0x9D40C4, return 2.
        if ((voice.FlagsCD & 1) != 0)
        {
            var init = Seams.InitVoiceA54A30 ?? throw Missing("0xA54A30 (the voice init body)");
            if (init(voice) != 1)
            {
                TeardownVoice(voice);
                return 2;
            }
            voice.Word0xF0 = pbi.Word15C;                                  // C25.5: 0xA54A64 ldm, 0xA54B60, 0xA54B70
        }

        var routing = _routingFor(pbi);
        byte listenerMask = routing.ListenerMask;                          // 0xA42E10 ldrb r8,[r2,#0x22]

        foreach (var entry in Devices.Entries)                            // 0xA42E24..0xA42E78, head first
        {
            if (voice.Connections.Any(c => c.Device == entry.Id)) continue;    // rows 5.4: same id -> skip
            if ((listenerMask & entry.ListenerMask) == 0) continue;            // 0xA42E64..0xA42E6C

            // C25.7: [pbi+0xE0] is dereferenced only after a device passes the listener mask (0xA42FA0), and
            // C24.9: a null node is a native null dereference, not "no bus".
            var node = NodeOf(routing);                                        // 0xA42FA0 ldr r0,[sl,#0xe0]
            // Row 6: bus = pbi->[+0xE0]->vt+0x88(); flag = bus ? bit6 : 1. This uses the raw vt+0x88 result,
            // not the 0x9BD144 override of row 9.
            var rawBus = node.Vt88();
            bool flag = rawBus is null || rawBus.Bit6;                         // 0xA42FB0..0xA42FBC
            if (entry.Id.IsMain != flag) continue;                             // 0xA42FC0..0xA42FD4
            CreateConnection(routing, voice, entry.Id);                        // 0xA42FE8 bl 0xA42C60, result ignored
        }

        InsertLive(voice);                                                 // 0xA42E7C..0xA42F48
        return 1;
    }

    /// <summary>
    /// The live-voice insertion at the end of <c>0xA42DEC</c> (row 20 / 5.9): the key is the max
    /// <see cref="IWwiseVoiceSource.OrderKey6C"/> over the voice's two source slots; insert before the first
    /// existing voice whose key is &gt;= the new key, else append. The head is index 0 (newest first when
    /// every key is 0, row 5.10).
    /// </summary>
    private void InsertLive(WwiseLiveVoice voice)
    {
        int key = SourceKey(voice);
        int index = 0;
        while (index < LiveVoices.Count && key > SourceKey(LiveVoices[index])) index++;   // 0xA42F10 cmp fp,r6; bgt
        LiveVoices.Insert(index, voice);
    }

    private static int SourceKey(WwiseLiveVoice v)
    {
        // C24.10: the key starts at 0 for both the new and the existing voice (0xA42EA4 mov r6,#0, 0xA42EDC mov
        // fp,#0), so a negative vt+0x6C clamps to 0. The two slots voice+0xD4/+0xD8 contribute their maximum.
        int key = 0;
        foreach (var s in new[] { v.Source, v.Pending })
        {
            if (s is null) continue;
            key = Math.Max(key, s.OrderKey6C);
        }
        return key;
    }

    /// <summary>
    /// <c>[pbi+0xE0]</c> (C24.9): a null node is a native null dereference, not "no bus"; the no-bus case is
    /// <see cref="WwiseRoutingNode.Vt88"/> returning null.
    /// </summary>
    private static WwiseRoutingNode NodeOf(WwisePbiRouting routing)
        => routing.Node ?? throw new InvalidOperationException(
            "M6-025 C24.9: [pbi+0xE0] is null; the native dereferences it (0xA42FA0/0xA42FA4, 0x9BD150), so a null " +
            "node is not the 'no bus' case (that is vt+0x88 returning 0)");

    // ---------------------------------------------------------------- 0xA42C60 / 0xA429F0

    /// <summary>
    /// <c>0xA42C60(ctx0=pbi+0xC, voice, dev)</c> (row 12, with row 9's context): find the line or create it,
    /// then <c>0xA4C280(..., 0)</c> (a dry connection). A creation that yields no line leaves the voice with
    /// no connection for that device.
    /// </summary>
    private void CreateConnection(WwisePbiRouting routing, WwiseLiveVoice voice, WwiseDeviceId dev)
    {
        // Row 9: busPtr = 0x9BD144(pbi+0xC): none when pbi+0xE9 bit2, else [pbi+0xE0]->vt+0x88(). Non-null gives
        // key2 = [[pbi+0x14]+0x78]; null gives the static default context.
        var busPtr = routing.NoBusOverride ? null : NodeOf(routing).Vt88();
        var ctx = busPtr is not null
            ? new WwiseBusContext(busPtr, routing.GameObjectKey78, 0)
            : WwiseBusContext.Default;

        var line = FindLine(ctx, dev);                                     // 0xA42CA8..0xA42D40
        line ??= CreateLine(ctx, dev, flag: false);                        // 0xA42D4C..0xA42D64 0xA429F0
        if (line is null) return;                                          // 0xA429F0 returned 0
        MakeConnection(routing, voice, line, dev, arg5: 0);                // 0xA42D94..0xA42DB8 0xA4C280(..., 0)
    }

    /// <summary>
    /// The array match of <c>0xA42CA8..0xA42D40</c> (row 12, corrected by the check): a line whose key equals the
    /// context's and whose <c>+0x50</c> equals <c>ctx.key2</c> (both tests skipped when both bus pointers are
    /// null), whose device id equals <paramref name="dev"/> and whose state is not 2. First match in array order.
    /// </summary>
    private WwiseMixBus? FindLine(WwiseBusContext ctx, WwiseDeviceId dev)
    {
        foreach (var line in Buses.Buses)
        {
            bool bothNull = ctx.Bus is null && line.Context.Bus is null;
            if (!bothNull)
            {
                if (line.Context.Key != ctx.Key) continue;
                if (line.Context.Key2 != ctx.Key2) continue;
            }
            if (line.Device != dev) continue;
            if (line.State == WwiseMixBus.StateNotReusable) continue;
            return line;
        }
        return null;
    }

    /// <summary>
    /// <c>0xA429F0(ctx, dev, flag)</c> and <c>0xA689B8</c> (row 13): parents are created before children up to the
    /// root. Returns null when a parent cannot be created or the line cannot be made.
    /// </summary>
    private WwiseMixBus? CreateLine(WwiseBusContext ctx, WwiseDeviceId dev, bool flag)
    {
        var parentCtx = ParentContext(ctx);                                // 0xA689B8
        WwiseMixBus? parentLine = null;
        if (parentCtx.Bus is not null)                                     // 0xA429F0..0xA42B28
        {
            parentLine = FindLine(parentCtx, dev);
            if (parentLine is null)
            {
                parentLine = CreateLine(parentCtx, dev, flag);             // recursion
                if (parentLine is null) return null;
            }
        }

        if (ctx.Byte != 0) return DefaultLine();                           // 0xA42AC8..0xA42B24 0xA42754()
        return MakeLine(ctx, dev, parentLine, flag);                       // 0xA42210
    }

    /// <summary>
    /// <c>0xA689B8(ctx)</c> (row 13): <c>{0,-1,0}</c> when the context's bus or its <c>[bus+0x38]</c> is null;
    /// otherwise <c>p = [ctx.bus+0x38]-&gt;vt+0x88()</c> and <c>{p, (p.+0x46 bit7 set and bits3..4 == 1 ? ctx.key2 : -1)}</c>,
    /// a null <c>p</c> giving <c>{0,-1,0}</c>. The byte of a non-null parent context is 0 (row 9: only the
    /// default context carries 1).
    /// </summary>
    private static WwiseBusContext ParentContext(WwiseBusContext ctx)
    {
        if (ctx.Bus is null || ctx.Bus.OutputBus is null) return WwiseBusContext.None;   // 0xA689E4
        var p = ctx.Bus.OutputBus.Vt88();                                   // 0xA689F4
        if (p is null) return WwiseBusContext.None;
        bool carry = (p.Byte46 & 0x80) != 0 && ((p.Byte46 >> 3) & 3) == 1;  // 0xA68A04, 0xA68A1C ubfx #3,#2
        return new WwiseBusContext(p, carry ? ctx.Key2 : -1, 0);
    }

    // ---------------------------------------------------------------- 0xA42210 / 0xA42754

    /// <summary>
    /// <c>0xA42210(ctx.bus, ctx.key2, ctx.byte, dev, parent, flag)</c> (row 14, C23.24, C24.3, C24.8). With no
    /// parent the device entry must exist and its <c>+0x1C</c> byte be non-zero, else it returns null; device lo 2
    /// then takes the default line as the parent if it exists (<c>0xA42640..0xA426FC</c>), lo 3 takes
    /// <c>0xA42754()</c> (<c>0xA425A0..0xA425AC</c>, null when its creation failed) and any other lo keeps parent 0
    /// (<c>0xA425A0 cmp r3,#3; bne 0xA4223C</c>, C24.8). The config words are C24.3's:
    /// <c>W = parent ? parent.Format64 : entry.ConfigWord</c>, <c>B = [bus+0x68]</c> (0 with no bus),
    /// <c>cfgA = (B &amp; 0xFF) != 0 ? B : W</c>, <c>cfgB = parent ? W : cfgA</c>. Init <c>0xA4F0EC</c> builds the
    /// line (a result other than 1 destroys it and returns null), which is appended to the array; a non-null
    /// parent is connected through <c>0xA4F664</c>, a null parent is not (C24.8).
    /// </summary>
    private WwiseMixBus? MakeLine(WwiseBusContext ctx, WwiseDeviceId dev, WwiseMixBus? parent, bool flag)
    {
        WwiseOutputDeviceEntry? entry = null;
        if (parent is null)
        {
            entry = Devices.Find(dev);
            if (entry is null) return null;                                // 0xA4248C..0xA42590
            if ((entry.ConfigWord & 0xFF) == 0) return null;               // entry+0x1C byte == 0
            if (dev.Lo == 2)
                parent = FindLine(WwiseBusContext.Default, WwiseDeviceId.Main);    // 0xA42640..0xA42718
            else if (dev.Lo == 3)
                parent = DefaultLine();                                    // 0xA425A0..0xA425AC 0xA42754
            // C24.8: any other lo keeps parent 0 (0xA425A0 cmp r3,#3; bne 0xA4223C).
        }

        // C24.3: W = parent ? [parent+0x64] : [deviceEntry+0x1C] (0xA42318; 0xA424D8..0xA4252C). The parentless
        // path reaches here only with the entry found above.
        uint w = parent is not null ? parent.Format64 : entry!.ConfigWord;
        uint b = ctx.Bus?.Word68 ?? 0;                                     // 0xA68A94: [bus+0x68], 0 with no bus
        uint cfgA = (b & 0xFF) != 0 ? b : w;                               // 0xA4234C
        uint cfgB = parent is not null ? w : cfgA;

        var key = new WwiseMixBusKey(unchecked((int)ctx.Key), ctx.Key2, unchecked((int)dev.Lo), unchecked((int)dev.Hi));
        var init = Seams.InitLineA4F0EC ?? throw Missing("0xA4F0EC (the bus-line Init beyond D2.1)");
        var (result, line) = init(new WwiseLineInitArgs(key, ctx.Bus, cfgA, cfgB, (int)LineMaxFrames, ctx, dev));
        if (result != 1) return null;                                      // 2 / 0x34 / any: destroy, return 0 (C24.3)
        if (line is null) throw new InvalidOperationException("M6-025 C24.3: the line Init seam returned 1 without a line");

        line.Format64 = cfgA;                                              // [line+0x64]
        line.Config44 = cfgB;                                              // [line+0x44]
        line.Context = ctx;                                                // vpl+0x4C/+0x50
        line.Device = dev;                                                 // vpl+0x28/+0x2C
        line.IsExtendedLine = ctx.Bus is not null && (ctx.Bus.Word40 & 0xE0000) != 0;   // 0xA42244..0xA4226C
        line.Bit3OfFlags1CC = flag;                                        // 0xA422DC: flag at +0x1CC bit3 (C24.3)
        // 0xA42210 clears +0x1CC bits 0 and 1 at creation (C23.2): the WwiseMixBus defaults.
        Buses.Append(line);                                                // 0xA42534..0xA42584
        if (parent is not null)
        {
            AddInput(parent, line);                                        // 0xA4F664(parent, line)
            line.SetParentLink(parent);
        }
        return line;
    }

    /// <summary>
    /// <c>0xA42754()</c> (row 15, corrected): the default-context line on device (2,0) with state != 2, found
    /// or created with <c>0xA42210(default ctx, (2,0), parent 0, 0)</c>. A created line is moved to index 0
    /// (<c>0xA42864..0xA428BC</c>) and the first line from index 1 with device (2,0), state != 2 and no
    /// <c>+0x1C8</c> becomes its child (<c>0xA428B4..0xA42928</c>: <c>+0x1C8</c> set, <c>0xA4F664</c>, return).
    /// </summary>
    private WwiseMixBus? DefaultLine()
    {
        var found = FindLine(WwiseBusContext.Default, WwiseDeviceId.Main);   // 0xA42754..0xA42830
        if (found is not null) return found;

        var created = MakeLine(WwiseBusContext.Default, WwiseDeviceId.Main, null, false);  // 0xA42830..0xA42854
        if (created is null) return null;                                  // the recursive 0xA42210 returned 0

        Buses.MoveToFront(created);                                        // 0xA42864..0xA428BC
        var all = Buses.Buses;
        for (int i = 1; i < all.Count; i++)                                // 0xA428B4..0xA42928
        {
            var line = all[i];
            if (line.Device == WwiseDeviceId.Main && line.State != WwiseMixBus.StateNotReusable && line.OutputBus is null)
            {
                AddInput(created, line);                                   // 0xA4F664(default, line)
                line.SetParentLink(created);                               // vpl+0x1C8 = default line
                break;                                                     // b 0xA42804
            }
        }
        return created;
    }

    // ---------------------------------------------------------------- 0xA4C280, 0xA6F90C, 0xA4F664

    /// <summary>
    /// <c>0xA4C280(voice, vpl, dev, arg5)</c> (row 16) with the ctor <c>0xA6F90C</c> (row 17) and the format
    /// check (row 19). <paramref name="routing"/> supplies <c>[[voice+8]+0xDD]&amp;8</c> (voice+8 is pbi+0xC, B11).
    /// </summary>
    private void MakeConnection(WwisePbiRouting routing, WwiseLiveVoice voice, WwiseMixBus vpl, WwiseDeviceId dev, uint arg5)
    {
        // (a) 0xA4C29C..0xA4C2D4: with pbi+0xE9 bit3, walk vpl then the +0x1C8 chain for +0x1CC bit1.
        bool ip = false;
        WwiseMixBus? r3 = null;
        if (routing.ChainWalk)
        {
            for (var l = vpl; l is not null; l = l.OutputBus)
            {
                if (l.Bit1OfFlags1CC) { ip = true; r3 = l; break; }
            }
        }

        voice.FlagsCD |= 4;                                                // (b) 0xA4C318 strb sl,[r5,#0xcd]
        if (dev.IsMain && arg5 == 0) voice.DryLineC = r3;                  // (c) voice+0xC = r3

        // (d,e) 0xA4C2E8..0xA4C350 allocate 0x70 and construct with 0xA6F90C.
        var channels = (Seams.ConnectionChannels ?? throw Missing("the connection mixer's input/output channel counts (0xA6F90C)"))(voice, vpl);
        var conn = new WwiseVoiceConnection(vpl, channels.Input, channels.Output);
        conn.C08 = conn.C0C = conn.C10 = conn.C14 = 1f;                    // 0xA6F918..: conn+8..+0x17 = 1.0f x4
        conn.Device = dev;                                                 // 0xA6F98C strd r8,sb,[r4,#0x48]
        conn.Arg68 = arg5;                                                 // 0xA6F990
        conn.HasAux = arg5 != 0;                                           // V8 walk tests conn+0x68
        bool bit2 = (voice.FlagsCD & 1) == 0;                              // !(voice+0xCD bit0) at connect time
        conn.Flags6C = (byte)(1 | (bit2 ? 4 : 0) | (ip ? 0x10 : 0));       // 0xA6F924/0xA6F930/0xA6F940/0xA6F948/0xA6F958
        // M6-012 row 2.5: conn.FadeIn is the live Flags6C bit2 (derived, C24.4 / 0xA4C0C0).
        AddInput(vpl, conn);                                               // 0xA6F9CC bl 0xA4F664(vpl, conn, 0, flags)

        // (f) 0xA4C354..0xA4C378: pushed at the head of voice+0x28, voice+0x24 (count) follows.
        voice.Connections.Insert(0, conn);

        // Row 19 / C24.5: 0xA4C37C..0xA4C574. Only when the line's type nibble (cfg >> 8) & 0xF is 1.
        uint cfg = vpl.Format64;                                           // [[conn+0x30]+0x64]
        if (((cfg >> 8) & 0xF) == 1)                                       // 0xA4C3AC..0xA4C3C0
        {
            uint mask = cfg >> 12;
            // 0xA4C37C..0xA4C3A8 walks the device list for the id; 0xA4C3CC ldr [r4,#0x5c] has no null check.
            var entry = Devices.Find(dev) ?? throw new InvalidOperationException(
                "M6-025 C24.5: the device entry is missing; the native dereferences it without a null check (0xA4C3CC)");
            // C25.6: the caller scans the table itself first and calls 0x9EA23C only when the key is absent; a
            // found key is success with no rebuild.
            int result = EnsureDeviceTableKey(entry, mask & ~8u);          // key 1 = mask & ~8 (0xA4C3D0, scan 0xA4C3D8..0xA4C410)
            if (result == 1 && (mask & 4) != 0)
                result = EnsureDeviceTableKey(entry, mask & ~0xCu);        // key 2 = mask & ~0xC (0xA4C4D4, scan 0xA4C4D8..0xA4C504)
            if (result != 1)
            {
                voice.Connections.Remove(conn);                            // unlink, destructor 0xA4F6F0, count--
                RemoveInput(vpl, conn);
                voice.FlagsCD |= 4;
                if (arg5 == 0) voice.DryLineC = null;                      // voice+0xC = 0 if conn+0x68 == 0
            }
        }
    }

    /// <summary>
    /// The row-19 table step (C25.6): scan the device table for <paramref name="key"/> (<c>0xA4C3D8..0xA4C410</c>,
    /// <c>0xA4C4D8..0xA4C504</c>); a found key is success (1) with no rebuild, an absent one calls <c>0x9EA23C</c>.
    /// </summary>
    private int EnsureDeviceTableKey(WwiseOutputDeviceEntry entry, uint key)
    {
        if (entry.Table.Any(t => t.Key == key)) return 1;
        var find = Seams.DeviceTableFindOrInsert9EA23C
            ?? throw Missing("0x9EA23C (the device table find-or-insert; its build bodies 0xA22304/0xA22684 are unread)");
        return find(entry, key);
    }

    /// <summary>
    /// <c>0xA4F664(vpl, conn)</c> (row 18): if the line's state is not 1, its volume is read from
    /// <c>0xA68A44(vpl+0x4C, 0)</c> and <c>+0xC0</c> bit3 cleared; when it has a <c>+0x1A8</c> object the
    /// mix object gets the input; then <c>+0x1C0++</c>.
    /// </summary>
    private void AddInput(WwiseMixBus vpl, object input)
    {
        if (vpl.State != WwiseMixBus.StateActive)                          // vpl+0x1BC != 1
        {
            // C24.6 0xA68A44(vpl+0x4C, 0): 0.0f for a null bus (0xA68A60), else 0x9C39DC(bus, 0, 5).
            vpl.VolumeDb90 = vpl.Context.Bus is null
                ? 0f
                : (Seams.BusVolumeParam5 ?? throw Missing("0x9C39DC (Bus Volume param 5)"))(vpl.Context.Bus);   // vpl+0x90
            vpl.FlagsC0 = (byte)(vpl.FlagsC0 & ~8);                        // clear vpl+0xC0 bit3
        }
        if (vpl.OutputMixObject1A8 is not null)                            // [vpl+0x1A8] non-null
        {
            var add = Seams.MixObjectAddInput ?? throw Missing("0xA4E324 and mixbus->vt+0x20(conn)");
            add(vpl, input);
        }
        vpl.Connect();                                                     // vpl+0x1C0++
    }

    /// <summary>
    /// <c>0xA4F6F0(vpl, conn)</c> (C24.6): <c>vpl+0x1C0--</c> and, when the line has a <c>+0x1A8</c> mix object
    /// with a non-null <c>[+0xC]</c>, <c>mixobj-&gt;vt+0x24(conn)</c>. The order of the two follows the C24.6 text
    /// (count first); the mix-object call is a required seam because its body is unread.
    /// </summary>
    private void RemoveInput(WwiseMixBus vpl, object conn)
    {
        vpl.Disconnect();                                                  // vpl+0x1C0--
        if (vpl.OutputMixObject1A8 is not null && vpl.MixObject1A8C is not null)
        {
            var remove = Seams.MixObjectRemoveInput ?? throw Missing("mixobj->vt+0x24 (0xA4F6F0..0xA4F71C)");
            remove(vpl, conn);
        }
    }

    // ---------------------------------------------------------------- 0xA431A8, 0xA544BC

    /// <summary>
    /// <c>0xA431A8(pbi, voice)</c> (row 3 and C23.1): <c>0xA544BC</c> 0x3F keeps the voice pending (returned
    /// as is); otherwise the voice leaves the pending list and 1 tail-calls <c>0xA42DEC</c>, anything else
    /// runs <c>0x9D40C4</c> and returns 2.
    /// </summary>
    public int ProcessPending(WwisePlayingInstance pbi, WwiseLiveVoice voice)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        ArgumentNullException.ThrowIfNull(voice);
        int r = NotReadyCheck(voice, pbi);                                 // 0xA431B8 bl 0xA544BC
        if (r == 0x3F) return 0x3F;                                        // 0xA431C0..: still pending
        PendingVoices.Remove(voice);                                       // 0xA43204..0xA43240 unlink
        if (r == 1) return Link(voice, pbi);                               // 0xA43248 b 0xA42DEC
        TeardownVoice(voice);                                              // 0xA43214 bl 0x9D40C4
        return 2;
    }

    /// <summary>
    /// <c>0xA544BC(voice, pbi)</c> (C23.1 and the check's row 3): re-runs <c>0xA56650</c>. Result 0x3F: a
    /// non-negative <c>pbi+0x1D8</c> returns 0x3F at once; a negative one runs <c>0xA54580</c> first and still
    /// returns 0x3F. Result 1: <c>+0x1D8 &gt;= round((L+1)*F*[pbi+0x164])</c> returns 0x3F, else (after
    /// <c>0xA54580</c> when <c>+0x1D8 &lt; 0</c>) 1. Anything else returns 2. L = 1 (inv 2.3), F is the frame size
    /// (inv 2.2; the settled policy value).
    /// </summary>
    public int NotReadyCheck(WwiseLiveVoice voice, WwisePlayingInstance pbi)
    {
        var source = voice.Source;
        int r;
        if (source is null) r = 0;
        else if (source.StartStreamSucceeded) r = 1;                       // 0xA56650: latch already set
        else if (!source.StartStream()) r = 0;
        else r = source.StartStreamCode;
        int offset = unchecked((int)pbi.StartOffset);                      // [pbi+0x1D8], signed

        if (r == 0x3F)                                                     // 0xA544EC beq 0xA54500
        {
            if (offset < 0) SideEffect54580(voice, pbi, source!);          // 0xA5457C bge fails -> 0xA54580
            return 0x3F;
        }
        if (r == 1)                                                        // 0xA54514
        {
            const int L = 1;
            int window = (int)MathF.Round((L + 1) * WwiseRuntimeSettings.SamplesPerFrame * pbi.Ratio, MidpointRounding.AwayFromZero);
            if (offset >= window) return 0x3F;                             // 0xA54570
            if (offset < 0) SideEffect54580(voice, pbi, source!);          // 0xA54580 first, then 1
            return 1;
        }
        return 2;
    }

    /// <summary><c>0xA54580..0xA545D0</c>: with source <c>+0x10</c> bit1 clear, <c>voice+0xE8 |= 1</c> and <c>0xA0428C</c>.</summary>
    private void SideEffect54580(WwiseLiveVoice voice, WwisePlayingInstance pbi, IWwiseVoiceSource source)
    {
        var bit1 = Seams.SourceFlag10Bit1 ?? throw Missing("[source+0x10] bit1 (0xA54584..0xA54588)");
        if (bit1(source)) return;                                          // bit1 set: returns without side effects
        voice.FlagE8 = true;                                               // voice+0xE8 |= 1
        var a = Seams.A0428C ?? throw Missing("0xA0428C (called at 0xA545CC)");
        a(voice, pbi);
    }

    // ---------------------------------------------------------------- 0x9D40C4

    /// <summary>
    /// <c>0x9D40C4(voice, unused)</c> (row 5.15): Term <c>vt+0x44 = 0xA53EA8</c>, then the destructor
    /// <c>vt[0] = 0xA55F2C</c>, then the pool free.
    /// </summary>
    public void TeardownVoice(WwiseLiveVoice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        TermVoice(voice);
        DestroyVoice(voice);
    }

    /// <summary>
    /// <c>0xA53EA8</c> (row 5.17): the current source is closed and cleared, the four insert-FX slots get
    /// <c>vt+0x2C</c> and are cleared, the pitch/filter buffers are released (<c>0xA53244</c>, <c>0xA76608</c>),
    /// <c>voice+0xCD |= 1</c>, then the pending source gets the same close.
    /// </summary>
    private void TermVoice(WwiseLiveVoice voice)
    {
        if (voice.Source is { } current)                                   // 0xA53EB0
        {
            (Seams.CloseSource56414 ?? throw Missing("0xA56414 (the source close: 0xA054D8, 0xA01800)"))(current);
            voice.Source = null;                                           // 0xA53F04
        }
        for (int i = 0; i < voice.InsertFxSlots.Length; i++)               // 0xA53F08..0xA53F60
        {
            var slot = voice.InsertFxSlots[i];
            if (slot is null) continue;
            slot.Teardown();                                               // vt+0x2C
            voice.InsertFxSlots[i] = null!;
        }
        Seams.TermBuffers?.Invoke(voice);                                  // 0xA53F74..0xA53F88
        voice.FlagsCD |= 1;                                                // 0xA53F98
        if (voice.Pending is { } pending)                                  // 0xA53FA8
        {
            (Seams.CloseSource56414 ?? throw Missing("0xA56414 (the source close: 0xA054D8, 0xA01800)"))(pending);
            voice.Pending = null;                                          // 0xA53FE4
        }
    }

    /// <summary>
    /// <c>0xA55F2C</c> (row 5.19): the <c>[voice+0x10]</c> array is freed and zeroed, then every connection is
    /// destroyed head first: unlink, count--, the destructor (<c>0xA4F6F0</c>: <c>vpl+0x1C0--</c>), free
    /// (<c>0xA55FF8..0xA5604C</c>).
    /// </summary>
    private void DestroyVoice(WwiseLiveVoice voice)
    {
        voice.SendTable = null;                                            // 0xA55FB8..0xA55FF4
        while (voice.Connections.Count > 0)                                // 0xA55FF8..0xA56044
        {
            var conn = voice.Connections[0];
            voice.Connections.RemoveAt(0);
            RemoveInput(conn.Bus, conn);                                   // 0xA4F6F0
        }
    }

    private static NotSupportedException Missing(string what)
        => new($"M6-025 C23: {what} is not settled by the approved inventory; supply the seam rather than defaulting");
}
