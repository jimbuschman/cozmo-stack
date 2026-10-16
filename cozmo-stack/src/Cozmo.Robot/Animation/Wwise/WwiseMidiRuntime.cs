// fidelity: M9-013
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>The four bytes at event+0, distinct from an SMF running-status byte.</summary>
public readonly record struct WwiseMidiEvent(byte Status, byte Channel, byte Key, byte Velocity)
{
    public bool NoteOn => Status == 0x90 && Velocity != 0;
    public bool NoteOff => Status == 0x80 || Status == 0x90 && Velocity == 0;
}

/// <summary>ARM VCVT with saturation and truncation, not host unchecked-cast overflow behavior.</summary>
public static class WwiseMidiTiming
{
    public static int Convert(double value) => double.IsNaN(value) ? 0 : value >= int.MaxValue ? int.MaxValue
        : value <= int.MinValue ? int.MinValue : (int)value;
    // 0x983680..98370C and A3D370..A3D3B4: bias and addition are f32.
    public static int Round(float value) => Convert(value + (value > 0 ? 0.5f : -0.5f));
    public static int Step(uint frames, float scale) => Round((float)frames * scale);
    public static int Rescale(int offset, float scale) => Round((float)offset / scale);
    // A3C62C..A3C72C: input time is f32, intermediate arithmetic f64.
    public static int Position(float milliseconds, uint rate)
    {
        double value = (double)milliseconds * rate / 1000.0;
        return Convert(value + (value > 0 ? 0.5 : -0.5));
    }
    internal static int Shift(int value, byte count) => count >= 32 ? value >> 31 : value >> count;
}

/// <summary>Track and music-parent target-query inputs (99239C, 987CB8).</summary>
public sealed class WwiseMidiMusicNode
{
    public bool Track { get; init; }
    public WwiseMidiClipContext? FirstContext98 { get; set; }
    public byte Flags { get; set; } // track+94 / parent+7C; these are NOT NodeBase+47
    public WwiseParamBundle? Properties3C { get; set; }
    public WwiseMidiMusicNode? Parent34 { get; set; }
    public void SetAuthoredFlags(byte flags) => Flags = Track
        ? (byte)(((flags >> 2) & 1) | ((flags >> 3) & 1) << 2)
        : (byte)(((flags >> 2) & 1) << 1 | ((flags >> 3) & 1) << 3);
}

public readonly record struct WwiseParsedMidiEvent(WwiseMidiEvent Event, float TimeMs, uint Argument);

/// <summary>A3BF0C/A3CBE0 music clip context. Parser callbacks accept native events, never prepaired NotesAt.</summary>
public sealed class WwiseMidiClipContext
{
    public object Owner74 { get; }
    public WwiseMidiMusicNode Track78 { get; }
    public object Source7C { get; }
    public WwiseRoutingNode? Target34 { get; private set; }
    public uint TargetId30 { get; private set; }
    public bool Table38 { get; private set; }
    public int References3C { get; set; } = 1;
    public void RetainReference() => References3C = unchecked(References3C + 1);
    public void ReleaseReference()
    {
        References3C = unchecked(References3C - 1);
        if (References3C == 0) WwiseMidiRuntime.Missing(
            "M9-013 context vt0/pool destruction at 0x00A708EC..0x00A70914 after the checked decrement");
    }
    public byte Disabled40 { get; set; }
    public uint Alternate20 { get; set; }
    public object? GameObject8 { get; set; }
    public uint OffsetF4 { get; set; }
    public uint EndF8 { get; set; } = uint.MaxValue;
    public byte FlagsFC { get; set; }
    public Func<float, IReadOnlyList<WwiseParsedMidiEvent>>? ParserA3F6E0 { get; set; }
    public Action? ReleaseTarget { get; set; }
    public Action? InitializeReader99D5C4 { get; set; }
    public Func<int, bool>? TrackKind98 { get; set; }
    public Action<uint, bool>? SubscribeA19ECC { get; set; }
    public Func<bool>? AcquireSourceA1EC54 { get; set; }
    public Action? AttachA70B3C { get; set; }
    public Func<bool>? InitializeTailA3CD78 { get; set; }
    public Func<IReadOnlyList<float>>? CollectFactorsVirtualAC { get; set; }
    public WwiseMidiClipContext? Next100 { get; set; }

    // R8 A3CC88..A3CD74: the factors are multiplied in order; NaN is not equal to zero.
    public int CompleteInitTail(IReadOnlyList<float> factors)
    {
        float product = 1f;
        foreach (float f in factors) product *= f;
        Disabled40 = product == 0f ? (byte)1 : (byte)0;
        return 1;
    }

    public WwiseMidiClipContext(object owner, WwiseMidiMusicNode track, object source)
        => (Owner74, Track78, Source7C) = (owner, track, source);

    public int ResolveTarget(WwiseRuntimeRegistry registry)
    {
        if (Target34 is not null)
        {
            if (ReleaseTarget is { } release) release();
            else WwiseMidiRuntime.Missing("M9-013 R5 target vt+C release at 0x00A3BE04");
        }
        Target34 = null; TargetId30 = 0;
        for (var node = Track78; node is not null; node = node.Parent34)
        {
            int bit = node.Track ? 0 : 1;
            if ((node.Flags & (1 << bit)) == 0) continue;
            TargetId30 = node.Properties3C?.Find(0x38) ?? 0;
            Table38 = (node.Flags & (1 << (bit + 2))) != 0;
            break; // explicit zero overrides ancestors too
        }
        if (TargetId30 == 0) return 0;
        Target34 = registry.Lookup9A7EB0(TargetId30, Table38 ? 1 : 0) as WwiseRoutingNode;
        if (Target34 is not null) return 1;
        TargetId30 = 0;
        return 0;
    }

    public int Initialize(WwiseRuntimeRegistry registry)
    {
        int targetResult = ResolveTarget(registry);
        if (InitializeReader99D5C4 is not { } reader)
        { WwiseMidiRuntime.Missing("M9-005 reader init 0x0099D5C4 called at 0x00A3CBF8"); return 2; }
        reader();
        if (TrackKind98 is not { } kind)
        { WwiseMidiRuntime.Missing("M9-013 track vt+0x98(3) at 0x00A3CC0C"); return 2; }
        if (!kind(3)) return 2;
        if (SubscribeA19ECC is not { } subscribe)
        { WwiseMidiRuntime.Missing("M9-013 subscription 0x00A19ECC at 0x00A3CC44"); return 2; }
        subscribe(0x2000, true);
        if (targetResult != 1) return 2;
        if (AcquireSourceA1EC54 is not { } acquire)
        { WwiseMidiRuntime.Missing("M9-005 source acquisition 0x00A1EC54 at 0x00A3CC60"); return 2; }
        if (!acquire())
        {
            if (AttachA70B3C is { } attach) attach();
            else WwiseMidiRuntime.Missing("M9-005 list attachment 0x00A70B3C at 0x00A3CC84");
        }
        else if (InitializeTailA3CD78 is not { } tail)
        { WwiseMidiRuntime.Missing("M9-039 reader helpers 0x00A3CD78..0x00A3CE14"); return 2; }
        else if (!tail()) return 2;
        Next100 = Track78.FirstContext98; Track78.FirstContext98 = this;
        if (CollectFactorsVirtualAC is not { } collect)
        { WwiseMidiRuntime.Missing("M9-041 track collector vt+0xAC at 0x00A3CCF8"); return 2; }
        return CompleteInitTail(collect());
    }

    internal void Update(WwiseMidiRuntime runtime, int step, uint rate, List<WwiseMidiPending> queue)
    {
        if ((FlagsFC & 2) != 0) return;
        RetainReference();
        uint offset = OffsetF4; OffsetF4 = 0;
        float elapsed = (float)unchecked(step - (int)offset) / ((float)rate / 1000f);
        uint end = EndF8; byte savedFlags = FlagsFC;
        if (elapsed != 0)
        {
            if (ParserA3F6E0 is not { } parser)
            {
                WwiseMidiRuntime.Missing("M9-005 parser 0x00A3F6E0 at 0x00A3C59C");
                ReleaseReference(); return;
            }
            foreach (var parsed in parser(elapsed))
            {
                int position = WwiseMidiTiming.Position(parsed.TimeMs, rate);
                if (parsed.Event.NoteOn && (end == uint.MaxValue ? (savedFlags & 4) != 0 : (uint)position >= end)) continue;
                runtime.Enqueue(this, parsed.Event, unchecked((uint)position + offset), parsed.Argument,
                    (FlagsFC & 4) != 0, (FlagsFC & 8) != 0, queue);
                FlagsFC &= 0xF7;
            }
        }
        // A3C53C: finite clips end after the parser list (also when elapsed is exactly zero).
        if (end != uint.MaxValue)
        {
            EndF8 = 0;
            if ((FlagsFC & 4) == 0) { FlagsFC |= 4; ReleaseReference(); }
        }
        ReleaseReference();
    }
}

public sealed class WwiseMidiPending
{
    public required WwiseMidiClipContext Context { get; init; }
    public WwiseMidiEvent Event { get; init; }
    public uint Offset { get; set; }
    public uint Argument { get; init; }
    public byte Flags { get; init; }
}

public sealed class WwiseMidiNoteState
{
    public int References { get; set; } = 1;
    public uint Count40 { get; set; }
    public uint Count5C { get; set; }
    public object? HeldOwner0C { get; set; }
    public List<WwiseMidiRecordedNode> RecordedNodes10 { get; } = new();
    public List<WwiseMidiRecordedVoice> RecordedVoices2C { get; } = new();
    public List<(object Action, int Kind)> Actions48 { get; } = new();
}

public sealed record WwiseMidiRecordedNode(int Kind, WwiseRoutingNode Node, byte Key, byte Channel);
public sealed class WwiseMidiRecordedVoice(int kind, WwisePlayingInstance pbi)
{
    public int Kind { get; set; } = kind;
    public WwisePlayingInstance Pbi { get; } = pbi;
}

public sealed class WwiseMidiHolder
{
    public required WwiseMidiClipContext Context { get; init; }
    public required WwiseRoutingNode Target10 { get; init; }
    public WwiseMidiNoteState? NoteState8 { get; set; }
    public WwiseMidiEvent Event14 { get; set; }
    public int Offset18 { get; set; }
    public byte Flags1C { get; set; }
}

public sealed class WwiseMidiTargetRecord
{
    public required WwiseRoutingNode Target { get; init; }
    public ushort ChannelMask { get; set; }
    public List<WwiseMidiHolder> Holders { get; } = new();
}

/// <summary>Music callback/clip/queue/holders. Rows R1–R8/E1–E12; the renderer owner is a named missing join.</summary>
public sealed partial class WwiseMidiRuntime
{
    internal static void Missing(string message) => SteppedBehavior.ReportMissing(message);
    public WwiseMidiRouting Routing { get; }
    public List<WwiseMidiClipContext> Clips { get; } = new();
    public List<WwiseMidiTargetRecord> Targets { get; } = new();
    public List<WwiseMidiHolder> Held { get; } = new();
    public List<WwiseMidiPending> Pending { get; } = new();
    public Func<int, bool> Allocate { get; set; } = _ => true;
    public Action<WwiseMidiPending>? OwnerVirtual4 { get; set; }
    public Action<WwiseMidiHolder>? A717A4 { get; set; }
    public Action<WwiseMidiHolder>? A3EB4C { get; set; }
    public Action<WwiseMidiHolder>? NoteOffA3E920 { get; set; }
    public Action<WwiseMidiRecordedVoice, WwiseMidiHolder>? PropagateOwnerA8 { get; set; }
    public byte ContextFlags36 { get; set; }
    public ulong Clock { get; private set; }
    public int Step38 { get; private set; }
    public byte Reset40 { get; set; }
    public byte Reset41 { get; set; }
    public Action<int>? RendererVirtual44 { get; set; }

    public WwiseMidiRuntime(WwiseRng rng)
    {
        Routing = new WwiseMidiRouting(rng);
        A717A4 = DispatchControllerA717A4;
        A3EB4C = RetireA3EB4C;
        NoteOffA3E920 = ReleaseA3E920;
    }

    public void ReportAbsentOwner() => Missing("M9-013 music renderer/context owner 0x0099B8E8 -> 0x00A3D234; native event parser M9-005 0x00A3F6E0 absent at song entry");

    public int CreateSource(ushort sourceKind16, object? owner, WwiseMidiMusicNode track, object source,
        Func<WwiseMidiClipContext, int> initialize, out WwiseMidiClipContext? context)
    {
        context = null;
        if (sourceKind16 != 0x10)
        { Missing("M9-013 non-MIDI music source factory 0x0099B8F4"); return 2; }
        if (owner is null) return 0x1F;
        if (!Allocate(0x104)) return 2;
        context = new WwiseMidiClipContext(owner, track, source);
        if (initialize(context) == 1) return 1;
        context.ReleaseReference(); context = null; return 2;
    }

    public void Frame(uint frames, float scale, uint rate, ushort frameThreshold)
    {
        if ((ContextFlags36 & 3) == 0 || (ContextFlags36 & 0x10) != 0) return;
        int step = WwiseMidiTiming.Step(frames, scale);
        Step38 = step;
        Routing.SampleRate = rate;
        // Native virtual+44 precedes MIDI clips; its owner is outside these rows.
        if (RendererVirtual44 is { } renderer) renderer(step);
        else Missing("M9-013 renderer vt+0x44 at 0x009836E4 (renderer owner scheduling)");
        var queue = new List<WwiseMidiPending>();
        for (int i = 0; i < Clips.Count;)
        {
            var clip = Clips[i]; var next = i + 1 < Clips.Count ? Clips[i + 1] : null;
            clip.Update(this, step, rate, queue);
            if (next is null) break;
            i = Clips.IndexOf(next); if (i < 0) break;
        }
        foreach (var entry in queue) entry.Offset = unchecked((uint)WwiseMidiTiming.Rescale((int)entry.Offset, scale));
        Drain(queue, step, frameThreshold);
        Clock = unchecked(Clock + (uint)step);
        Missing("M9-013 renderer scheduling tail 0x00985EE4 at 0x00983710 (outside the checked frame-step row)");
    }

    public void Enqueue(WwiseMidiClipContext context, WwiseMidiEvent midi, uint offset, uint argument = 0,
        bool bit6 = false, bool bit7 = false, List<WwiseMidiPending>? queue = null)
    {
        if ((midi.Status & 0x80) == 0 || (midi.Status & 0xF0) == 0xF0 || context.Disabled40 != 0 || !Allocate(0x14)) return;
        queue ??= Pending;
        var entry = new WwiseMidiPending { Context = context, Event = midi, Offset = offset,
            Argument = argument & 0x3FFFFFFF, Flags = (byte)((bit6 ? 0x40 : 0) | (bit7 ? 0x80 : 0)) };
        context.RetainReference();
        int index = 0;
        while (index < queue.Count)
        {
            var old = queue[index];
            if (old.Offset > offset || old.Offset == offset &&
                (((old.Flags & 0x40) != 0 && !bit6) || ((old.Flags & 0x80) == 0 && bit7))) break;
            index++;
        }
        queue.Insert(index, entry);
    }

    public void Drain(int step, ushort frames) => Drain(Pending, step, frames);
    private void Drain(List<WwiseMidiPending> queue, int step, ushort frames)
    {
        while (queue.Count != 0)
        {
            var entry = queue[0]; queue.RemoveAt(0);
            if ((entry.Flags & 0xC0) == 0x80)
            {
                if (OwnerVirtual4 is { } owner) owner(entry);
                else StartClipA3CEB4(entry, queue);
            }
            Admit(entry.Context, entry.Event, unchecked((int)entry.Offset));
            entry.Context.ReleaseReference();
        }
        AdvanceHolders(step, frames);
    }

    public WwiseMidiHolder? Admit(WwiseMidiClipContext context, WwiseMidiEvent midi, int offset, bool contextOnly = false)
    {
        // A04384 is a lock/unlock no-op with the app's flags (no 0x10000).
        if (context.Target34 is not { } target) return null;
        var record = Targets.Find(x => ReferenceEquals(x.Target, target));
        if (record is null)
        {
            if (!Allocate((Targets.Count + 1) * 16)) return null;
            record = new WwiseMidiTargetRecord { Target = target }; Targets.Add(record);
        }
        if ((target.Category44 == 0 || target.Category44 == 12) && midi.Status != 0xB0) return null;
        if (!Allocate(0x20)) return null;
        var holder = new WwiseMidiHolder { Context = context, Target10 = target, Event14 = midi, Offset18 = offset };
        if (midi.NoteOn)
        {
            if (Allocate(0x64))
            {
                holder.NoteState8 = new WwiseMidiNoteState();
                // A3DE10: two pools of four entries, result 0x34 on either failed allocation.
                if (!Allocate(0x40) || !Allocate(0x30)) { ReleaseHolder(holder); return null; }
            }
        }
        else holder.Flags1C = midi.NoteOff ? (byte)4 : (byte)7;
        if (midi.NoteOff)
        {
            var match = record.Holders.Find(old => (!contextOnly || ReferenceEquals(context, old.Context)) && Match(holder, old));
            if (match is null) { ReleaseHolder(holder); return null; }
        }
        if (midi.Status == 0xB0 && (midi.Key == 0x78 || midi.Key == 0x7B))
            Missing("M9-034 CC120/123 0x00A70C84 at 0x00A71044");
        int index = record.Holders.FindIndex(old => old.Offset18 > holder.Offset18);
        record.Holders.Insert(index < 0 ? record.Holders.Count : index, holder);
        return holder;
    }

    private static bool Match(WwiseMidiHolder incoming, WwiseMidiHolder old)
    {
        var a = incoming.Event14; var b = old.Event14;
        bool NoteStatus(byte s) => s == 0xA0 || (s & 0xEF) == 0x80;
        if (a.Channel != b.Channel || !NoteStatus(a.Status) || !NoteStatus(b.Status) || a.Key != b.Key || (old.Flags1C & 4) != 0) return false;
        if (a.NoteOff && b.Status == 0x90 && b.Velocity != 0)
        {
            incoming.Event14 = a with { Status = 0x80, Velocity = b.Velocity };
            incoming.NoteState8 = old.NoteState8;
            if (incoming.NoteState8 is { } state) state.References++;
        }
        if (a.Status == 0x80 || a.Status == 0x90 && a.Velocity == 0) old.Flags1C |= 4;
        return true;
    }

    private static void ReleaseHolder(WwiseMidiHolder holder)
    {
        if (holder.NoteState8 is { } state)
        {
            state.References--;
            if (state.References == 0) Missing("M9-013 note-state release 0x00A3E27C via 0x00A3E3C0");
        }
    }

    public bool StepHolder(WwiseMidiHolder holder, int step, ushort frames, bool allow)
    {
        var midi = holder.Event14;
        if (midi.Status != 0x90 && midi.Status != 0x80) return true;
        if (holder.Context.GameObject8 is not null && allow)
        {
            if ((holder.Flags1C & 2) == 0 && holder.Offset18 < frames)
            {
                if (midi.NoteOff)
                {
                    if (NoteOffA3E920 is { } noteOff) noteOff(holder);
                    else Missing("M9-031 note-off dispatch 0x00A3E920 at 0x00A3EB40");
                }
                holder.Flags1C |= 2;
            }
            if ((holder.Flags1C & 1) == 0) { Replay(holder); holder.Flags1C |= 1; }
        }
        holder.Offset18 = Math.Max(0, unchecked(holder.Offset18 - step));
        return (holder.Flags1C & 6) == 6;
    }

    public void Replay(WwiseMidiHolder holder)
    {
        if (holder.Event14.NoteOn) { Routing.Submit(holder.Target10, holder, holder.Event14); return; }
        if (holder.NoteState8 is not { } state) return;
        foreach (var recorded in state.RecordedNodes10)
            if (recorded.Kind == 1) Routing.Submit(recorded.Node, holder, holder.Event14 with { Key = recorded.Key, Channel = recorded.Channel });
        foreach (var voice in state.RecordedVoices2C)
        {
            if (voice.Kind == 1 || voice.Pbi.NodeE0 is null) continue;
            if (PropagateOwnerA8 is { } propagate) propagate(voice, holder);
            else Missing("M9-034 owner vt+0xA8 / 0x009F1BE8 at 0x00A3E7CC (descriptor kind3, original velocity)");
        }
    }

    public void AdvanceHolders(int step, ushort frames)
    {
        foreach (var record in Targets)
        {
            for (int i = 0; i < record.Holders.Count;)
            {
                var holder = record.Holders[i]; var midi = holder.Event14;
                bool masked = (WwiseMidiTiming.Shift(record.ChannelMask, midi.Channel) & 1) != 0;
                bool allow = !masked || midi.Status != 0x80 && (midi.Status != 0x90 || midi.Velocity != 0);
                if (!StepHolder(holder, step, frames, allow)) { i++; continue; }
                if (midi.Status != 0x80 && midi.Status != 0x90)
                {
                    if (A717A4 is { } dispatch) dispatch(holder);
                    else Missing("M9-034 non-note dispatch 0x00A717A4 at 0x00A71994");
                }
                record.Holders.RemoveAt(i);
                if (midi.NoteOff && holder.NoteState8 is { } state) { state.HeldOwner0C = this; Held.Add(holder); }
                else ReleaseHolder(holder);
            }
        }
        for (int i = 0; i < Held.Count;)
        {
            var holder = Held[i]; var state = holder.NoteState8!;
            if (state.Count40 != 0 || state.Count5C != 0) { i++; continue; }
            Held.RemoveAt(i); state.HeldOwner0C = null;
            if (A3EB4C is { } retire) retire(holder);
            else Missing("M9-013 retirement 0x00A3EB4C at 0x00A71A9C");
            ReleaseHolder(holder);
        }
    }
}

public sealed class WwiseMidiCallbackContext
{
    public required WwiseMidiRuntime Renderer38 { get; init; }
    public WwiseMidiCallbackContext? Next48 { get; set; }
    public uint Flags10 { get; set; }
    public float Scale3C { get; set; }
    public Func<int>? QueryVirtual70 { get; set; }
    public Action? ByteSet981DBC { get; set; }
}

/// <summary>97D14C callback and 97D744 registration. Audio frame count/rate stay caller inputs.</summary>
public sealed class WwiseMidiFrameCallback
{
    public bool Suppressed { get; set; }
    public byte ResetByte0C { get; set; }
    public WwiseMidiCallbackContext? NormalContexts { get; set; }
    public void Register(Action<uint, uint, Action> register9A5AFC, Func<(ushort Frames, uint Rate)> frameInput)
        => register9A5AFC(8, 0, () => { var input = frameInput(); Invoke(input.Frames, input.Rate); });
    public void Invoke(ushort frames, uint rate)
    {
        if (Suppressed)
        {
            for (var c = NormalContexts; c is not null; c = c.Next48)
            { c.Renderer38.Reset40 = 0; c.Renderer38.Reset41 = ResetByte0C; }
            Suppressed = false; ResetByte0C = 0;
            // 97D230 branches into the ordinary main loop, not an early return.
        }
        for (var context = NormalContexts; context is not null;)
        {
            var next = context.Next48;
            if ((context.Flags10 & 0x200000) != 0)
            {
                if (context.QueryVirtual70 is not { } query)
                    WwiseMidiRuntime.Missing("M9-034 context vt+0x70 at 0x0097D1C4");
                else if (query() == 1)
                {
                    if (context.ByteSet981DBC is { } set) set();
                    else WwiseMidiRuntime.Missing("M9-039 0x00981DBC called at 0x0097D1DC");
                }
            }
            context.Renderer38.Frame(frames, context.Scale3C, rate, frames);
            context = next;
        }
    }
}
