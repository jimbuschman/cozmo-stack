// fidelity: M9-034, M9-037, M9-040
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>The six-word key passed by A717A4, distinct from a public object-only post.</summary>
public readonly record struct WwiseMidiControllerKey(object? Object, uint Playing, uint Target, byte Channel, byte Note, uint Extra);

/// <summary>Explicit parser calls used by the checked A3C760 chase; bodies remain M9-039.</summary>
public sealed class WwiseMidiChaseParser
{
    public Func<object>? SaveA3F1B0 { get; set; }
    public Action? ResetA3F0A0 { get; set; }
    public Func<WwiseParsedMidiEvent?>? NextA3FC4C { get; set; }
    public Action<object>? RestoreA3F1CC { get; set; }
}

public sealed partial class WwiseMidiRuntime
{
    public Action<uint, float, WwiseMidiControllerKey>? PostControllerA149C8 { get; set; }
    public Action<uint, WwiseMidiControllerKey, int, int, byte>? ResetControllerA14098 { get; set; }
    public Func<WwiseMidiClipContext, bool>? HardStopQuery { get; set; }
    public Action<WwiseMidiRecordedVoice, WwiseMidiHolder>? ReleaseVoiceVirtual1C { get; set; }
    public Action<object, WwiseRoutingNode, int>? CancelNoteAction9AB1F4 { get; set; }
    public Action<WwisePlayingInstance, int>? StopA01280 { get; set; }
    public WwiseMidiChaseParser ChaseParser { get; } = new();

    public void DispatchControllerA717A4(WwiseMidiHolder holder)
    {
        var midi = holder.Event14;
        var record = Targets.Find(x => ReferenceEquals(x.Target, holder.Target10));
        var c = holder.Context;
        var key = new WwiseMidiControllerKey(c.Table38 ? null : c.GameObject8, 0, c.TargetId30,
            midi.Channel, midi.Status == 0xA0 || (midi.Status & 0xEF) == 0x80 ? midi.Key : (byte)0xFF, 0);
        if (midi.Status == 0xB0)
        {
            if (midi.Key == 120 || midi.Key == 123) return; // handled separately; absent in shipped MIDI
            if (midi.Key == 121)
            {
                if (record is not null) record.ChannelMask = 0;
                for (uint id = 1; id <= 0x85; id++)
                    if (ResetControllerA14098 is { } reset) reset(id, key, 0, 4, 0);
                    else Missing($"M9-034 RTPC reset 0x00A14098 called at 0x00A71920, id {id}");
                return;
            }
            Post((uint)midi.Key + 1, midi.Velocity, key);
            if (midi.Key == 64 && record is not null)
            {
                uint bit = midi.Channel < 32 ? 1u << midi.Channel : 0;
                record.ChannelMask = unchecked((ushort)((record.ChannelMask & ~bit) | (midi.Velocity > 0x3F ? bit : 0)));
            }
        }
        else if (midi.Status == 0xE0) Post(0x83, (float)(midi.Key | midi.Velocity << 7) / 128f, key);
    }

    private void Post(uint id, float value, WwiseMidiControllerKey key)
    {
        if (PostControllerA149C8 is { } post) post(id, value, key);
        else Missing($"M9-034 scoped MIDI RTPC 0x00A149C8 at 0x00A71868/0x00A718D8, id {id}");
    }

    public void RetireA3EB4C(WwiseMidiHolder holder)
    {
        if (holder.NoteState8 is not { } state) return;
        // Includes every voice kind, unlike the propagation path's kind-1 exclusion.
        for (int i = 0; i < state.RecordedVoices2C.Count; i++)
        {
            var pbi = state.RecordedVoices2C[i].Pbi;
            pbi.Field1F8 = uint.MaxValue;
            pbi.RequestEnvelopeStop(); // A01280({0,4,0},1), after the sentinel store
        }
    }

    public void ReleaseA3E920(WwiseMidiHolder holder)
    {
        if (holder.NoteState8 is not { } state) return;
        foreach (var voice in state.RecordedVoices2C)
        {
            voice.Pbi.ApplyNoteOffDeadline(unchecked((uint)holder.Offset18));
            // The context's hard-stop query and kind-dependent leaf recipients are distinct from propagation.
            if (HardStopQuery is not { } query)
            { Missing("M9-042 context hard-stop query in 0x00A3E9A0..0x00A3E9C8"); continue; }
            bool hard = query(holder.Context);
            if (hard) { voice.Kind = 3; MidiStop(voice.Pbi, 1); }
            else if (voice.Kind == 2)
            {
                if (ReleaseVoiceVirtual1C is { } release) release(voice, holder);
                else Missing("M9-042 voice vt+0x1C at 0x00A3E9E8");
            }
            else if (voice.Kind == 3) MidiStop(voice.Pbi, holder.Offset18 == -1 ? 1 : 0);
        }
        foreach (var action in state.Actions48)
            if (CancelNoteAction9AB1F4 is { } cancel) cancel(action.Action, holder.Target10, action.Kind);
            else Missing("M9-042 pending cancellation 0x009AB1F4 at 0x00A3EA20");
    }

    private void MidiStop(WwisePlayingInstance pbi, int flag)
    {
        pbi.Field1F8 = uint.MaxValue;
        if (StopA01280 is { } stop) stop(pbi, flag);
        else if (flag == 1) pbi.RequestEnvelopeStop();
        else Missing("M9-042 0x00A01280 flag0 branch at 0x00A3E9D4");
    }

    public void StartClipA3CEB4(WwiseMidiPending first) => StartClipA3CEB4(first, Pending);
    private void StartClipA3CEB4(WwiseMidiPending first, List<WwiseMidiPending> queue)
    {
        var c = first.Context;
        var record = Targets.Find(x => ReferenceEquals(x.Target, c.Target34));
        if (record is not null)
        {
            for (int i = 0; i < Clips.Count; i++)
            {
                var old = Clips[i];
                if (ReferenceEquals(old, c) || (old.FlagsFC & 4) == 0 || !ReferenceEquals(old.Target34, c.Target34)) continue;
                old.RetainReference(); // A708C0, balanced by A708D4 after synthesis
                for (int q = 0; q < queue.Count;)
                    if (ReferenceEquals(queue[q].Context, old))
                    { var e = queue[q]; queue.RemoveAt(q); e.Context.ReleaseReference(); }
                    else q++;
                for (byte channel = 0; channel < 16; channel++)
                    Admit(old, new WwiseMidiEvent(0xB0, channel, 64, 0), unchecked((int)first.Offset), contextOnly: true);
                record.ChannelMask = 0;
                // A71138 inserts after equal offsets; next is read after each insertion in native.
                for (int h = 0; h < record.Holders.Count; h++)
                {
                    var held = record.Holders[h];
                    if (!ReferenceEquals(held.Context, old) || !held.Event14.NoteOn || (held.Flags1C & 4) != 0) continue;
                    Admit(old, held.Event14 with { Status = 0x80 }, unchecked((int)first.Offset), contextOnly: true);
                }
                old.ReleaseReference();
            }
        }
        foreach (var chased in ChaseA3C760(c, first.Offset, first.Argument))
        {
            if (first.Event.Status == 0xB0 && chased.Event.Status == 0xB0 &&
                first.Event.Channel == chased.Event.Channel && first.Event.Key == chased.Event.Key)
            { c.ReleaseReference(); continue; }
            Admit(c, chased.Event, unchecked((int)first.Offset), contextOnly: true);
            c.ReleaseReference(); // chase queue retained c in A70738
        }
    }

    public IReadOnlyList<WwiseMidiPending> ChaseA3C760(WwiseMidiClipContext context, uint offset, uint bound)
    {
        var output = new List<WwiseMidiPending>();
        if (ChaseParser.SaveA3F1B0 is not { } save)
        { Missing("M9-039 parser snapshot 0x00A3F1B0 at 0x00A3C790"); return output; }
        object snapshot = save();
        if (ChaseParser.ResetA3F0A0 is not { } reset)
        { Missing("M9-039 parser reset 0x00A3F0A0 at 0x00A3C798"); return output; }
        reset();
        for (uint i = 0; i < bound; i++)
        {
            if (ChaseParser.NextA3FC4C is not { } next)
            { Missing("M9-039 parser next 0x00A3FC4C at 0x00A3C7E4"); return output; }
            var parsed = next();
            if (parsed is null || bound < parsed.Value.Argument) break;
            var midi = parsed.Value.Event;
            if (midi.Status != 0xB0) continue;
            // A3C844 matches controller number only, across channels; removes all older matches.
            for (int q = 0; q < output.Count;)
                if (output[q].Event.Status == 0xB0 && output[q].Event.Key == midi.Key)
                { output[q].Context.ReleaseReference(); output.RemoveAt(q); }
                else q++;
            Enqueue(context, midi, offset, parsed.Value.Argument, queue: output);
        }
        if (ChaseParser.RestoreA3F1CC is { } restore) restore(snapshot);
        else Missing("M9-039 parser restore 0x00A3F1CC at 0x00A3C8D8");
        return output;
    }
}
