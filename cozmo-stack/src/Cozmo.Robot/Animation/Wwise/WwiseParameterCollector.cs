// fidelity: M9-035
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>Inputs to 9EF258; the native +AC dynamic recipients stay explicit.</summary>
public sealed class WwiseParameterCollectorNode
{
    public required WwiseRoutingNode Node { get; init; }
    public WwiseParameterCollectorNode? Parent34 { get; set; }
    public WwiseParameterCollectorNode? Bus38 { get; set; }
    public uint Flags40 { get; set; }
    public byte Flags59 { get; set; }
    public bool StateEnabled46 { get; set; }
    public List<WwiseParamBundle> StateTables18 { get; } = new();
    public ulong SelectorMask14 { get; set; }
    public uint Subscriber10 { get; set; }
    public byte? PositioningFlags24 { get; set; }
    public byte? GlobalSisFlags10 { get; set; }
    public List<(uint Object, byte Flags10)> ObjectSisFlags10 { get; } = new();
    public Func<WwiseCollectorCall, bool>? Filter9EF0E4 { get; set; }
    public Action<WwiseCollectorCall>? State9F9CDC { get; set; }
    public Action<WwiseCollectorCall>? Middle9EF2E0 { get; set; }
    public Action<WwiseCollectorCall>? VirtualAC { get; set; }
    public List<Action<WwiseCollectorCall>> Layers50 { get; } = new();
}

public sealed class WwiseCollectorCall
{
    public uint Mask { get; set; }
    public WwiseGainRtpcKey Key { get; init; }
    public bool IncludeBus { get; set; }
    public WwiseParameterCollectorNode? Excluded { get; init; }
    public float[] Output { get; init; } = new float[0x5C / 4];
    private sealed class Bytes { public byte F1C, F24, F5B; }
    private Bytes _bytes = new();
    public byte Flags1C { get => _bytes.F1C; set => _bytes.F1C = value; }
    public byte Flags24 { get => _bytes.F24; set => _bytes.F24 = value; }
    public byte Filter5B { get => _bytes.F5B; set => _bytes.F5B = value; }
    public WwiseCollectorCall Forward(uint mask, bool bus) => new()
    { Mask = mask, Key = Key, IncludeBus = bus, Excluded = Excluded, Output = Output,
        _bytes = _bytes };
}

public sealed class WwiseParameterCollector(WwiseRtpcStore store)
{
    public bool Entry9EF258(WwiseParameterCollectorNode n, WwiseCollectorCall call)
    {
        uint forwarded = call.Mask;
        if ((call.Mask & 0x40) != 0)
        {
            bool resolved = n.Filter9EF0E4 is { } filter ? filter(call) : Filter9EF0E4(n, call);
            if (resolved) forwarded &= ~0x40u;
        }
        if (n.State9F9CDC is { } state) state(call);
        else State9F9CDC(n, call); // original mask, before base properties
        foreach (var (bit, property, selector, offset) in new (uint, byte, int, int)[]
            { (1,0,0,0), (1,6,7,0x18), (2,2,2,8), (4,3,3,0xC), (8,4,4,0x10) })
        {
            if ((call.Mask & bit) == 0) continue;
            float value = BitConverter.UInt32BitsToSingle(n.Node.BaseBundle3C?.Find(property) ?? 0);
            call.Output[offset / 4] += value;
            if ((n.SelectorMask14 & (1UL << selector)) != 0)
                call.Output[offset / 4] += store.A11590(n.Subscriber10, (uint)selector, call.Key);
        }
        if ((call.Mask & 1) != 0 && (call.Flags1C & 2) == 0 && ((n.Flags59 & 0x20) != 0 || n.Parent34 is null))
            call.Flags1C = (byte)((call.Flags1C & ~1) | ((n.Flags59 >> 6) & 1) | 2);
        if ((call.Mask & 0x20) != 0 && (call.Flags24 & 1) == 0 && ((n.Flags40 & 0x20) != 0 || n.Parent34 is null))
        {
            // Mapping FFA6C0: property33 -> selector37. The native default's producer is explicit.
            if ((n.SelectorMask14 & (1UL << 37)) != 0) call.Output[0x20 / 4] = store.A11590(n.Subscriber10, 37, call.Key);
            else if (n.Node.BaseBundle3C?.Find(33) is { } raw) call.Output[0x20 / 4] = BitConverter.UInt32BitsToSingle(raw);
            else if (DefaultSelector37 is { } defaultValue) call.Output[0x20 / 4] = defaultValue;
            else { WwiseMidiRuntime.Missing("M9-041 default property33/selector37 table value at 0x009F0B44..0x009F0BA4"); return false; }
            call.Flags24 = (byte)((call.Flags24 & ~2) | 1 | ((n.Flags59 >> 6) & 2));
        }
        if (n.Middle9EF2E0 is not { } middle)
        { WwiseMidiRuntime.Missing("M9-041 collector middle 0x009EF2E0..0x009EFFD0"); return false; }
        middle(call);
        Tail9EFFD4(n, call, forwarded);
        return true;
    }
    public float? DefaultSelector37 { get; set; }

    // OV72 full 9EF0E4..9EF250, reopened: null N+28 exits before the RTPC/SIS branch.
    public bool Filter9EF0E4(WwiseParameterCollectorNode n, WwiseCollectorCall call)
    {
        if ((n.Flags40 & 0x1F000) == 0 && n.Parent34 is not null) return false;
        call.Filter5B = 0;
        if (n.PositioningFlags24 is not { } flags) return true;
        call.Filter5B = (byte)((flags >> 4) & 1);
        if ((n.SelectorMask14 & (1UL << 33)) != 0)
        {
            // The helper builds an object-only key, not the caller's deeper scope suffix.
            float value = store.A11590(n.Subscriber10, 33, new(call.Key.GameObject, 0));
            call.Filter5B = value != 0f ? (byte)1 : (byte)0;
            return true;
        }
        foreach (var entry in n.ObjectSisFlags10)
            if (entry.Object == call.Key.GameObject) { call.Filter5B = (byte)((entry.Flags10 >> 4) & 1); return true; }
        if (n.GlobalSisFlags10 is { } global) call.Filter5B = (byte)((global >> 4) & 1);
        return true;
    }
    public static void State9F9CDC(WwiseParameterCollectorNode n, WwiseCollectorCall call)
    {
        if (!n.StateEnabled46) return;
        foreach (var table in n.StateTables18)
            foreach (var (mask, property, offset) in new (uint, byte, int)[]
                { (1,0,0), (1,6,0x18), (2,2,8), (4,3,0xC), (8,4,0x10), (0x10,5,0x14) })
                if ((call.Mask & mask) != 0 && table.Find(property) is { } raw)
                    call.Output[offset / 4] += BitConverter.UInt32BitsToSingle(raw);
    }

    public void Tail9EFFD4(WwiseParameterCollectorNode n, WwiseCollectorCall call, uint forwardedMask)
    {
        bool bus = call.IncludeBus && n.Bus38 is not null;
        if (n.Parent34 is { } parent && !ReferenceEquals(parent, call.Excluded)) Invoke(parent, call.Forward(forwardedMask, bus ? false : call.IncludeBus));
        if (bus && !ReferenceEquals(n.Bus38, call.Excluded)) Invoke(n.Bus38!, call.Forward(forwardedMask, false));
        foreach (var layer in n.Layers50) layer(call.Forward(forwardedMask, call.IncludeBus));
    }
    private static void Invoke(WwiseParameterCollectorNode n, WwiseCollectorCall call)
    {
        if (n.VirtualAC is { } recipient) recipient(call);
        else WwiseMidiRuntime.Missing("M9-041 collector dynamic +0xAC recipient at 0x009F0008/0x009F005C/0x009F00FC");
    }
}

/// <summary>Native current-value tree read by A17280. Population is an explicit owner input.</summary>
public sealed class WwiseRtpcCurrentScope
{
    public float? Value { get; set; }
    public SortedList<uint, WwiseRtpcCurrentScope> Children { get; } = new();
    public float? Lookup(WwiseRtpcScopeKey key)
    {
        uint[] words = [key.A, key.B, key.C, key.D, key.E, key.F];
        var node = this; float? result = Value;
        for (int depth = 0; depth < 6; depth++)
        {
            uint wild = depth is 3 or 4 ? 0xFFu : 0u;
            if (!node.Children.TryGetValue(words[depth], out var next) && !node.Children.TryGetValue(wild, out next)) break;
            node = next; if (node.Value is { } v) result = v;
        }
        return result;
    }
}


/// <summary>The alternate output passed as Z+18 by 9FBE74; only bit 2 of Z+3C belongs to the wrapper.</summary>
public sealed class WwisePositioningCollectorContext
{
    public float[] Values18 { get; } = new float[3];
    public byte Flags3C { get; set; }
}
