// fidelity: M9-013
namespace Cozmo.Robot.Animation.Wwise;

public sealed class WwiseMidiPlayRequest
{
    public WwiseMidiEvent Event84 { get; set; }
    public WwiseMidiNoteState? NoteState88 { get; init; }
    public required WwiseMidiHolder Holder { get; init; }
    public uint Offset74 { get; set; }
    public uint Context7C { get; set; }
    public byte Flags124 { get; set; }
    public byte Flags128 { get; set; } = 6;
    public int Depth28 { get; set; }
    public ushort[] History2C { get; init; } = new ushort[32];
    public uint HistoryBits { get; set; }
    public float Gain90 { get; set; } = 1f;
    public WwiseMidiPlayRequest Clone() => new()
    {
        Event84 = Event84, NoteState88 = NoteState88, Holder = Holder, Offset74 = Offset74, Context7C = Context7C,
        Flags124 = Flags124, Flags128 = Flags128, Depth28 = Depth28, History2C = (ushort[])History2C.Clone(),
        HistoryBits = HistoryBits, Gain90 = Gain90
    };
}

/// <summary>Runtime-only virtual slots and modulation inputs, keyed by the existing registry nodes.</summary>
public sealed class WwiseMidiNodeSlots
{
    public byte RanSeq91 { get; set; }
    public int? Kind10 { get; set; }
    public Func<WwiseMidiPlayRequest, (WwiseRoutingNode Node, ushort Index)?>? SelectA0A3B4 { get; set; }
    public Func<WwiseMidiPlayRequest, int>? Play128 { get; set; }
    public Action<WwiseRoutingNode>? ReleaseSelected { get; set; }
    public Func<int, WwiseMidiPlayRequest, float>? ModulationA11590 { get; set; }
    public ulong ModulationMask { get; set; }
    public bool DelayModulationEnabled { get; set; }
    public Func<WwiseMidiPlayRequest, float, int>? Defer9EDEB8 { get; set; }
}

/// <summary>9EE454 filter, 9F12E0 delay, 9D0758 Layer and A0A6FC single-step dispatch.</summary>
public sealed class WwiseMidiRouting
{
    private readonly Dictionary<WwiseRoutingNode, WwiseMidiNodeSlots> _slots = new();
    private readonly WwiseRng _rng;
    public uint SampleRate { get; set; }
    public WwiseMidiRouting(WwiseRng rng) => _rng = rng;
    public WwiseMidiNodeSlots Slots(WwiseRoutingNode node)
    {
        if (!_slots.TryGetValue(node, out var result)) _slots.Add(node, result = new());
        return result;
    }
    private static int Property(WwiseRoutingNode node, byte id, int fallback)
        => node.BaseBundle3C?.Find(id) is { } word ? unchecked((int)word) : fallback;
    private float FloatProperty(WwiseRoutingNode node, byte id)
        => node.BaseBundle3C?.Find(id) is { } word ? BitConverter.UInt32BitsToSingle(word) : 0f;
    private float? Modulated(WwiseRoutingNode node, byte prop, int selector, WwiseMidiPlayRequest request)
    {
        float value = FloatProperty(node, prop); var slots = Slots(node);
        if ((slots.ModulationMask & (1UL << selector)) == 0) return value;
        if (slots.ModulationA11590 is { } modulate) return value + modulate(selector, request);
        WwiseMidiRuntime.Missing($"M9-035 modulation 0x00A11590, node {node.Id}, selector {selector}");
        return null;
    }

    public int Filter(WwiseRoutingNode node, WwiseMidiPlayRequest request, ref bool inherit)
    {
        int mask = Property(node, 0x35, 0xFFFF);
        if ((WwiseMidiTiming.Shift(mask, request.Event84.Channel) & 1) == 0) return 0x52;
        if (inherit)
        {
            if (node.Parent is { } parent)
            {
                int result = Filter(parent, request, ref inherit);
                if (result != 1) return result;
            }
            inherit = false;
        }
        var midi = request.Event84;
        if (Modulated(node, 0x2F, 0x0B, request) is not { } noteOffset) return 2;
        int key = Math.Clamp(unchecked(midi.Key + WwiseMidiTiming.Round(noteOffset)), 0, 127);
        if (key < Property(node, 0x31, 0) || key > Property(node, 0x32, 127)) return 0x52;
        if (Modulated(node, 0x30, 0x0C, request) is not { } velocityOffset) return 2;
        int velocity = Math.Clamp(unchecked(midi.Velocity + WwiseMidiTiming.Round(velocityOffset)), 1, 127);
        if (velocity < Property(node, 0x33, 0) || velocity > Property(node, 0x34, 127)) return 0x52;
        request.Event84 = midi with { Key = (byte)key, Velocity = (byte)velocity };
        return 1;
    }

    public int Delay(WwiseRoutingNode node, WwiseMidiPlayRequest request)
    {
        if ((request.Flags128 & 8) != 0) return 1;
        if ((request.Flags124 & 1) != 0) { request.Flags124 &= 0xFE; return 1; }
        float value = FloatProperty(node, 0x3B);
        var slots = Slots(node);
        if (slots.DelayModulationEnabled)
        {
            if (slots.ModulationA11590 is not { } modulate)
            { WwiseMidiRuntime.Missing("M9-035 delay modulation 0x00A11590 at 0x009F1410"); return 2; }
            value += modulate(0x3B, request);
        }
        var bundle = node.RangedBundle4C;
        int rangeIndex = bundle is null ? -1 : Array.IndexOf(bundle.Ids.ToArray(), (byte)0x3B);
        if (rangeIndex >= 0 && bundle!.SecondWords is { } maximums)
        {
            float min = BitConverter.UInt32BitsToSingle(bundle.FirstWords[rangeIndex]), max = BitConverter.UInt32BitsToSingle(maximums[rangeIndex]);
            float delta = max - min;
            float ranged = delta == 0f ? min : (float)((double)_rng.Next() / 2147483647.0 * delta) + min;
            value += ranged;
        }
        if (!(value > 0f)) return 1;
        if (request.Context7C != 0)
        {
            double samples = (double)value * SampleRate;
            request.Offset74 = unchecked(request.Offset74 + (uint)WwiseMidiTiming.Convert(samples + (samples > 0 ? 0.5 : -0.5)));
            return 1;
        }
        if (Slots(node).Defer9EDEB8 is not { } defer)
        { WwiseMidiRuntime.Missing("M9-013 delay scheduler 0x009EDEB8 from 0x009F1558"); return 2; }
        int result = defer(request, value);
        return result == 1 ? 3 : result;
    }

    public int Submit(WwiseRoutingNode target, WwiseMidiHolder holder, WwiseMidiEvent midi)
    {
        int? kind = Slots(target).Kind10;
        if (kind is null && target.Category44 == 5) kind = 0; // 9D0410, actual Layer slot
        if (kind is null)
        { WwiseMidiRuntime.Missing($"M9-013 target vt+0x10 at 0x00A3E800, node {target.Id}"); return 2; }
        if (kind == 0xB) return 1;
        var request = new WwiseMidiPlayRequest { Holder = holder, Event84 = midi, NoteState88 = holder.NoteState8,
            Offset74 = unchecked((uint)holder.Offset18) };
        return SubmitFiltered(target, request, false);
    }

    private int SubmitFiltered(WwiseRoutingNode node, WwiseMidiPlayRequest request, bool delayAccepted)
    {
        if (request.NoteState88 is { } state) state.References++;
        try
        {
            if (request.Event84.NoteOn)
            {
                bool inherit = (request.Flags128 & 2) != 0;
                int filter = Filter(node, request, ref inherit);
                if (filter != 1) return filter;
                request.Flags128 = (byte)((request.Flags128 & ~2) | (inherit ? 2 : 0));
            }
            int delay = delayAccepted ? 1 : Delay(node, request);
            if (delay != 1) return delay;
            return Play(node, request);
        }
        finally { if (request.NoteState88 is { } retained) retained.References--; }
    }

    public int Play(WwiseRoutingNode node, WwiseMidiPlayRequest request)
    {
        var slots = Slots(node);
        if (node.Category44 == 5)
        {
            if (node.MidiLayer84 != 0)
            { WwiseMidiRuntime.Missing("M9-013 continuous Layer 0x009D0F3C"); return 2; }
            // The request's context pointer is zero for A3DB48. Nonzero completion helpers remain unread.
            for (int i = 0; i < node.Children5C.Count; i++)
            {
                var copy = request.Clone();
                SubmitFiltered(node.Children5C[i], copy, false);
            }
            return 1;
        }
        if (node.Category44 == 2)
        {
            if ((slots.RanSeq91 & 0x40) != 0)
            { WwiseMidiRuntime.Missing("M9-013 continuous RanSeq 0x00A0B020"); return 2; }
            if (slots.SelectA0A3B4 is not { } select)
            { WwiseMidiRuntime.Missing("M9-022 candidate selection 0x00A0A3B4 (eligibility before MIDI filter)"); return 2; }
            var selected = select(request);
            if (selected is null) return 2;
            try
            {
                int oldDepth = request.Depth28++;
                if ((uint)oldDepth <= 31)
                {
                    request.History2C[oldDepth] = selected.Value.Index;
                    request.HistoryBits &= ~(1u << oldDepth);
                }
                int result = SubmitFiltered(selected.Value.Node, request, false);
                return result == 3 ? 1 : result; // no retry after the selected child rejects
            }
            finally
            {
                if (slots.ReleaseSelected is { } release) release(selected.Value.Node);
                else WwiseMidiRuntime.Missing("M9-013 selected child vt+C release at 0x00A0A830");
            }
        }
        if (slots.Play128 is { } play) return play(request);
        WwiseMidiRuntime.Missing($"M6-025 MIDI play vt+0x128 at 0x00A3DDE8, node {node.Id}; Sound/PBI request join");
        return 2;
    }

    /// <summary>L7's count split and registry join. Multi-entry selection is the separate M9-022 state machine.</summary>
    public (WwiseRoutingNode Node, ushort Index)? ChooseCandidate(WwiseRuntimeRegistry registry,
        IReadOnlyList<uint> playlist, bool sequence, Func<int?> selectSequence, Func<int?> selectRandom,
        Func<WwiseRoutingNode, bool> eligible48)
    {
        if (playlist.Count == 0) return null;
        if (playlist.Count == 1)
            return registry.Lookup9A7EB0(playlist[0], 0) is WwiseRoutingNode only ? (only, 0) : null;
        // The supplied M9-022 selector owns retry/history/avoid/shuffle, including registry eligibility.
        int? index = sequence ? selectSequence() : selectRandom();
        if (index is not { } selected) return null;
        if (registry.Lookup9A7EB0(playlist[selected], 0) is not WwiseRoutingNode node) return null;
        if (eligible48(node)) return (node, unchecked((ushort)selected));
        WwiseMidiRuntime.Missing("M9-022 rejected candidate release/retry 0x00A0A5B0 -> 0x00A0A49C; selector state owner required");
        return null;
    }
}
