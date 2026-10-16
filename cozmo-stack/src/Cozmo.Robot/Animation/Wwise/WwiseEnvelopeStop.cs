// fidelity: M9-024
namespace Cozmo.Robot.Animation.Wwise;

public sealed partial class WwisePlayingInstance
{
    public uint Transition148 { get; set; }
    public Func<uint, uint>? ReadTransitionSelector { get; set; }
    public Action<uint, uint, float, int, int>? UpdateTransitionA366F4 { get; set; }
    public Action<WwisePlayingInstance, int, int>? StopVirtual0 { get; set; }
    public List<WwiseEnvelopeState>? EnvelopeAttachments34 { get; set; }

    /// <summary>A01280 zero-duration descriptor {0,4,0}, flag1; no invented positive-duration transition.</summary>
    public void RequestEnvelopeStop()
    {
        if ((Flags1BC & 0x80) != 0) { DispatchStop(0); return; }
        if (Transition148 != 0)
        {
            if (ReadTransitionSelector is not { } read)
            { WwiseMidiRuntime.Missing("M9-024 transition+148 selector 0x00A35980 at 0x00A012AC"); return; }
            uint selector = read(Transition148);
            if ((unchecked(selector - 0x02000000) & ~0x02000000u) == 0) { DispatchStop(0); return; }
        }
        Flags1BC |= 0x40;
        if ((Flags1BA & 0x78) == 0) { DispatchStop(1); return; }
        if (Field144 != 0)
        {
            if (UpdateTransitionA366F4 is { } update) update(Field144, 0x02000000, 0f, 0, 4);
            else WwiseMidiRuntime.Missing("M9-024 transition 0x00A366F4 at 0x00A0132C (selector02000000,target0,duration0,curve4,flags0)");
            return;
        }
        Fade168 = 0f; MuteFade40 = 0f;
        if ((Flags1BD & 2) == 0) Flags1BD = (byte)((Flags1BD | 2) & ~0x1C);
        DispatchStop(0);
    }

    private void DispatchStop(int secondArgument)
    {
        if (StopVirtual0 is { } stop) { stop(this, 0, secondArgument); return; }
        if ((Flags1BD & 0x80) != 0)
        {
            WwiseMidiRuntime.Missing("M9-024 continuous vt0 0x00A6B29C continuation release 0x00A69FC8");
            return;
        }
        if ((Flags1BC & 0x20) != 0) return;
        Flags1BC |= 0x20;
        // Base 9FF7B8: detach transitions, then positioning cleanup before the final flags.
        if (Field144 != 0 || Transition148 != 0)
            WwiseMidiRuntime.Missing("M9-024 base vt0 transition detach 0x00A36618 at 0x009FF7B8");
        WwiseMidiRuntime.Missing("M9-024 base vt0 positioning 0x009FEEB8 and source/render stop notification join 0x009FF7B8..0x009FF8F0");
        Flags1BC = (byte)((Flags1BC & ~2) | 8);
    }
}

public sealed class WwiseEnvelopeRecipient
{
    public required object Identity { get; init; }
    public Action? ReleaseVirtualC { get; set; }
}

public sealed class WwiseEnvelopeVoiceLink
{
    public required WwisePlayingInstance Pbi { get; init; }
    public WwiseEnvelopeVoiceLink? Next { get; set; }
}

public sealed class WwiseEnvelopeActionLink
{
    public required object Action { get; init; }
    public WwiseEnvelopeActionLink? Next { get; set; }
}

public sealed class WwiseEnvelopeNoteEvent
{
    public WwiseEnvelopeVoiceLink? Voices2C { get; set; }
    public WwiseEnvelopeActionLink? Actions48 { get; set; }
    public Action? ReleaseA3E27C { get; set; }
}

/// <summary>State marker and cleanup inputs; phase timing is supplied by M9-030, never inferred from zero amplitude.</summary>
public sealed class WwiseEnvelopeState
{
    public WwiseEnvelopeState? Next { get; set; }
    public WwiseParamBundle? ModulatorProperties { get; set; }
    public bool HasModulator8 { get; set; }
    public uint ModulatorType10 { get; set; } = 1;
    public WwisePlayingInstance? PbiC { get; set; }
    public List<WwiseEnvelopeRecipient> Recipients { get; } = new();
    public object? Object1C { get; set; }
    public WwiseEnvelopeNoteEvent? Event20 { get; set; }
    public byte Note24 { get; set; } = 0xFF;
    public byte Channel25 { get; set; } = 0xFF;
    public object? Identity28 { get; set; }
    public object? Node2C { get; set; }
    public object? Output30 { get; set; }
    public uint Value34 { get; set; }
    public uint Deadline38 { get; set; } = int.MaxValue;
    public uint Cursor3C { get; set; }
    public int References40 { get; set; }
    public uint Marker44 { get; set; }
    public int Scope48 { get; set; } = 1;
    public uint Attachments50 { get; set; }
    public float Current74 { get; set; }
    public uint Attack78 { get; set; }
    public float Sustain80 { get; set; }
    public uint Decay84 { get; set; }
    public uint BufferFrames { get; set; }
    public Action? DestroyVirtual0 { get; set; }
    public void SetDeadline(uint position) // 9E21D0: only sentinel changes, marker3 stays3
    {
        if (Deadline38 == int.MaxValue) Deadline38 = unchecked(position + Cursor3C);
        if (Marker44 != 3) Marker44 = 2;
    }
    public bool ShouldRetire => !HasModulator8 || Marker44 == 3 || Scope48 <= 1 && Attachments50 == 0;
    public bool StopPolicy => ModulatorType10 != 0 && (ModulatorProperties?.Find(1) ?? 1) != 0;
}

public sealed record WwiseEnvelopeEvent(object? Object0, WwiseMidiEvent Midi4, WwiseEnvelopeNoteEvent? Event8,
    object? IdentityC, object? Node10, uint Value14, uint Control18, WwisePlayingInstance? Pbi1C);

/// <summary>9E21FC and 9E2AE4; receives actual envelope states from the as-yet absent native modulator owner.</summary>
public sealed class WwiseEnvelopeRuntime
{
    public Func<int, bool> Allocate { get; set; } = _ => true;
    public Action<WwiseEnvelopeNoteEvent>? RetainEventA3E268 { get; set; }
    public Action<WwiseEnvelopeState, WwiseEnvelopeRecipient?>? AddRecipient9E2554 { get; set; }
    public Func<WwiseEnvelopeState, uint>? InitializeParameters9D5934 { get; set; }
    public Action<WwiseEnvelopeState>? GeometryVirtual10 { get; set; }
    public WwiseEnvelopeState? ActiveHead { get; set; }
    public WwiseEnvelopeState? ActiveTail { get; set; }
    public uint ActiveCount { get; set; }
    public Func<object, WwiseEnvelopeState, bool>? ActionMembershipA63A6C { get; set; }
    public Action<object, WwiseEnvelopeRecipient, int>? Cancel9AB1F4 { get; set; }
    public object? PendingOwner { get; set; }
    public Action<object, WwiseEnvelopeRecipient, object?, object?>? Cancel9AB8AC { get; set; }
    public Action<WwiseEnvelopeRecipient, object?, object?, int, int>? NodeAction9F4A64 { get; set; }

    public static bool Trigger(uint selector, WwiseEnvelopeEvent input)
    {
        byte status = input.Midi4.Status;
        if (status == 0) return selector == 1;
        if (selector == 1) return input.Control18 != 3;
        return selector == 2 && input.Midi4.NoteOff;
    }

    public WwiseEnvelopeState? Acquire(WwiseParamBundle? properties, WwiseEnvelopeEvent input,
        WwiseEnvelopeState? prior, WwiseEnvelopeRecipient? recipient, out bool created)
    {
        created = false;
        if (!Trigger(properties?.Find(15) ?? 1, input)) return null;
        if (prior is not null)
        {
            if ((input.Control18 & ~2u) == 1) Initialize(prior, properties, input, recipient);
            return prior; // envelope vt+20 returns1
        }
        if ((properties?.Find(0) ?? 0) != 0 && input.Control18 == 0 || !Allocate(0x8C)) return null;
        var state = new WwiseEnvelopeState();
        Initialize(state, properties, input, recipient);
        created = true;
        return state;
    }

    public void Initialize(WwiseEnvelopeState state, WwiseParamBundle? properties, WwiseEnvelopeEvent input,
        WwiseEnvelopeRecipient? recipient)
    {
        if (!ReferenceEquals(state.Event20, input.Event8) || !ReferenceEquals(state.Node2C, input.Node10)) Cleanup(state);
        state.HasModulator8 = true; state.ModulatorProperties = properties;
        state.Value34 = input.Value14; state.Identity28 = input.IdentityC;
        state.Marker44 = 1; state.Cursor3C = 0; state.Object1C = null;
        state.Note24 = 0xFF; state.Channel25 = input.Midi4.Channel;
        state.Scope48 = unchecked((int)(properties?.Find(0) ?? 0));
        if (state.Scope48 != 3)
        {
            state.Object1C = input.Object0;
            if (state.Scope48 != 2)
            {
                if (state.Scope48 != 1) state.PbiC = input.Pbi1C;
                var midi = input.Midi4;
                state.Note24 = midi.Status == 0xA0 || (midi.Status & 0xEF) == 0x80 ? midi.Key : (byte)0xFF;
                if (state.Event20 is null && input.Event8 is { } midiEvent)
                {
                    state.Event20 = midiEvent;
                    if (RetainEventA3E268 is { } retain) retain(midiEvent);
                    else WwiseMidiRuntime.Missing("M9-024 event retain 0x00A3E268 at 0x009E27FC");
                }
                if (AddRecipient9E2554 is { } add) add(state, recipient);
                else WwiseMidiRuntime.Missing("M9-024 recipient add 0x009E2554 at 0x009E2738");
                state.Node2C = input.Node10;
                if (state.Note24 == 0xFF && state.Channel25 == 0xFF && state.Node2C is null && state.PbiC is null) state.Scope48 = 2;
            }
            if (state.Object1C is null) state.Scope48 = 3;
        }
        if (InitializeParameters9D5934 is not { } initialize)
        { WwiseMidiRuntime.Missing("M9-030 envelope initializer 0x009D5934 at 0x009E279C"); return; }
        uint parameter8 = initialize(state);
        if (GeometryVirtual10 is not { } geometry)
        { WwiseMidiRuntime.Missing("M9-030 geometry 0x009E1FB8 at 0x009E27AC"); return; }
        geometry(state); state.Deadline38 = parameter8;
        if (state.Current74 != 0f) state.Value34 = 0;
        if (state.Attack78 == 0) state.Current74 = state.Decay84 == 0 ? state.Sustain80 : 1f;
    }

    public void Cleanup(WwiseEnvelopeState state)
    {
        state.Marker44 = 3;
        if (state.HasModulator8 && state.StopPolicy)
        {
            if (state.Scope48 == 0) state.PbiC?.RequestEnvelopeStop();
            else if (state.Scope48 == 1 && state.Recipients.Count != 0)
            {
                if (state.Event20 is { } midiEvent)
                {
                    for (var link = midiEvent.Voices2C; link is not null; link = link.Next)
                        if (link.Pbi.EnvelopeAttachments34?.Any(x => ReferenceEquals(x, state)) == true)
                            link.Pbi.RequestEnvelopeStop();
                    for (var action = midiEvent.Actions48; action is not null; action = action.Next)
                        for (int i = 0; i < state.Recipients.Count; i++)
                        {
                            if (ActionMembershipA63A6C is not { } membership)
                            { WwiseMidiRuntime.Missing("M9-024 action membership 0x00A63A6C -> 0x009E79B0 at 0x009E23E8"); continue; }
                            if (!membership(action.Action, state)) continue;
                            if (Cancel9AB1F4 is { } cancel) cancel(action.Action, state.Recipients[i], 3);
                            else WwiseMidiRuntime.Missing("M9-024 pending cancellation 0x009AB1F4 at 0x009E240C");
                        }
                }
                else for (int i = 0; i < state.Recipients.Count; i++)
                {
                    var recipient = state.Recipients[i];
                    if (PendingOwner is { } owner)
                    {
                        if (Cancel9AB8AC is { } cancel) cancel(owner, recipient, state.Object1C, state.Node2C);
                        else WwiseMidiRuntime.Missing("M9-024 fallback cancellation 0x009AB8AC at 0x009E2470");
                    }
                    if (NodeAction9F4A64 is { } nodeAction) nodeAction(recipient, state.Object1C, state.Node2C, 0, 4);
                    else WwiseMidiRuntime.Missing("M9-024 recipient action 0x009F4A64 at 0x009E2488");
                }
            }
        }
        state.HasModulator8 = false; state.PbiC = null; state.Node2C = null; state.Identity28 = null;
        state.Note24 = 0xFF; state.Channel25 = 0xFF;
        while (state.Recipients.Count > 0)
        {
            int i = state.Recipients.Count - 1; var recipient = state.Recipients[i]; state.Recipients.RemoveAt(i);
            if (recipient.ReleaseVirtualC is { } release) release();
            else WwiseMidiRuntime.Missing("M9-024 recipient vt+C at 0x009E227C");
        }
        state.Object1C = null;
        if (state.Event20 is { } old)
        {
            state.Event20 = null;
            if (old.ReleaseA3E27C is { } release) release();
            else WwiseMidiRuntime.Missing("M9-024 note-state release 0x00A3E27C at 0x009E22D0");
        }
        state.Output30 = null;
    }

    public void Retire()
    {
        WwiseEnvelopeState? previous = null, retired = null;
        for (var state = ActiveHead; state is not null;)
        {
            var next = state.Next;
            if (state.ShouldRetire)
            {
                if (previous is null) ActiveHead = next; else previous.Next = next;
                if (ReferenceEquals(ActiveTail, state)) ActiveTail = previous;
                ActiveCount--; state.Next = retired; retired = state;
            }
            else previous = state;
            state = next;
        }
        while (retired is { } state)
        {
            retired = state.Next; Cleanup(state);
            if (--state.References40 == 0)
            {
                if (state.DestroyVirtual0 is { } destroy) destroy();
                else WwiseMidiRuntime.Missing("M9-024 state destructor 0x009E2828 after 0x009E2AE4");
            }
        }
    }

    public void ReportAbsentOwner() => WwiseMidiRuntime.Missing("M9-024 modulator manager 0x009E6D2C -> 0x009E2BD0; M9-030 timing and M9-031 note-off joins absent");

    /// <summary>The fatal growth branch, not a guessed .NET allocator-failure detector.</summary>
    public void SkipFatalPopulationGrowth(WwiseEnvelopeState state)
    {
        // 9E36C0 writes address18 then UDF at9E36C4. M1-034: this one work item is skipped.
        // No state/output store: retain the prior marker, cursor, output and deadline.
        WwiseMidiRuntime.Missing("M1-034 M9-024: skip envelope population work item at 0x009E36C0/0x009E36C4; preserve previous state/output");
    }
}
