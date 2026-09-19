namespace Cozmo.Robot.Animation.Wwise;

/// <summary>One clip of a segment as it will be played: the source, its timing, and whether it is MIDI.</summary>
public sealed record WwiseMusicClipPlan(uint TrackId, uint SourceId, uint PluginId, WwiseTrackClip Clip)
{
    public bool IsMidi => PluginId == WwiseMusicTrackNode.MidiPluginId;
}

/// <summary>One segment in play order, with the clips of every track under it, the tempo it plays at, and where its MIDI notes go.</summary>
public sealed record WwiseMusicSegmentPlan(uint SegmentId, double DurationMs, WwiseMeter Meter, float TempoBpm, IReadOnlyList<WwiseMusicClipPlan> Clips)
{
    /// <summary>The MIDI target for this segment's MIDI clips: the nearest ancestor that sets one (property 56), or null.</summary>
    public uint? MidiTargetNodeId { get; init; }
}

/// <summary>
/// How an event that targets the music hierarchy would play, given the current switch values: the chain
/// from event to switch container to playlist to segments and clips, kept whole so it can be printed as
/// evidence. Nothing is decoded here.
/// </summary>
public sealed record WwiseMusicPlan(uint EventId)
{
    public string? EventName { get; init; }
    /// <summary>The Play action's target: a music switch, playlist or segment.</summary>
    public uint TargetId { get; init; }
    public WwiseObjectType? TargetType { get; init; }
    /// <summary>The music switch container, when the target is one.</summary>
    public WwiseMusicSwitchNode? Switch { get; init; }
    /// <summary>The node the switch's decision tree selected for the given switch values.</summary>
    public uint? SelectedNodeId { get; init; }
    /// <summary>The playlist container the segments came from, when there is one.</summary>
    public WwiseMusicPlaylistNode? Playlist { get; init; }
    /// <summary>Segments in play order. Random groups contribute their first alternative and are flagged.</summary>
    public IReadOnlyList<WwiseMusicSegmentPlan> Segments { get; init; } = Array.Empty<WwiseMusicSegmentPlan>();
    /// <summary>True when a playlist group chooses at random, so the order above is one possibility.</summary>
    public bool HasRandomChoice { get; init; }
    /// <summary>The tempo the first segment plays at; see <see cref="WwiseMusic.EffectiveTempo"/>.</summary>
    public float TempoBpm { get; init; }
    /// <summary>The node the first segment's MIDI notes are dispatched to; see <see cref="WwiseMusic.EffectiveMidiTarget"/>.</summary>
    public uint? MidiTargetNodeId { get; init; }
    public string? Problem { get; init; }
}

/// <summary>
/// Resolves music events: the part of the hierarchy M6 stopped short of.
///
/// <code>
/// event -> Play action -> MusicSwitchContainer (type 12)
///            decision tree, one level per switch group, keyed by the current switch value
///        -> MusicPlaylistContainer (type 13): a tree of groups (sequence or random, with loop counts)
///           whose leaves are MusicSegments
///        -> MusicSegment (type 10): a duration and its MusicTracks
///        -> MusicTrack (type 11): sources (audio media, or a MIDI sequence) placed as clips
/// </code>
///
/// The structure above is read from the banks (<see cref="WwiseHierarchy"/>). How Wwise walks it is
/// runtime behaviour this package does not contain; the two rules used here are the smallest reading of
/// the data and are named as such: a decision level takes the child keyed by the group's current value and
/// otherwise the child keyed 0, and a continuous-sequence group plays its children in order. Anything
/// this resolver cannot settle from the data (a random group, a switch value with no key) is reported in
/// the plan rather than guessed.
/// </summary>
public static class WwiseMusic
{
    /// <summary>Sequence types of a playlist group, as the bank numbers them.</summary>
    public const int ContinuousSequence = 0, StepSequence = 1, ContinuousRandom = 2, StepRandom = 3;

    /// <summary>Builds the play plan for an event under the given switch values (group id to switch id).</summary>
    public static WwiseMusicPlan Resolve(WwiseSoundLibrary lib, uint eventId, IReadOnlyDictionary<uint, uint> switches)
    {
        var res = lib.Resolve(eventId);
        var plan = new WwiseMusicPlan(eventId) { EventName = res.Name };
        var play = res.Actions.Where(a => a.ActionType == WwiseBank.PlayAction).Select(a => a.Target).ToList();
        if (play.Count == 0) return plan with { Problem = res.Problem ?? "the event fires no Play action" };
        uint target = play[0];
        var node = lib.Node(target);
        plan = plan with { TargetId = target, TargetType = node?.Type };
        if (node is null) return plan with { Problem = $"the Play target {target} is in no loaded bank or is not a hierarchy node" };

        uint? midiTarget = null;
        uint current = target;
        if (node is WwiseMusicSwitchNode sw)
        {
            midiTarget = sw.MidiTargetNode;
            var picked = sw.Select(switches);
            plan = plan with { Switch = sw, SelectedNodeId = picked, MidiTargetNodeId = midiTarget, TempoBpm = sw.Meter.TempoBpm };
            if (picked is null)
            {
                var groups = string.Join(", ", sw.Arguments.Select(a => lib.Names.NameOf(a.GroupId) ?? a.GroupId.ToString()));
                return plan with { Problem = $"no decision-tree path for the current values of {groups}" };
            }
            current = picked.Value;
            node = lib.Node(current);
            if (node is null) return plan with { Problem = $"the decision tree chose node {current}, which is in no loaded bank" };
        }

        var segments = new List<WwiseMusicSegmentPlan>();
        bool random = false;
        if (node is WwiseMusicPlaylistNode pl)
        {
            plan = plan with { Playlist = pl };
            var order = new List<uint>();
            Flatten(pl.Playlist, 0, order, ref random);
            foreach (var segId in order)
                if (lib.Node(segId) is WwiseMusicSegmentNode seg) segments.Add(SegmentPlan(lib, seg));
                else return plan with { Problem = $"playlist entry {segId} is not a segment in any loaded bank" };
        }
        else if (node is WwiseMusicSegmentNode seg)
        {
            segments.Add(SegmentPlan(lib, seg));
        }
        else
        {
            return plan with { Problem = $"node {current} is a {node.Type}, which is not music" };
        }

        return plan with
        {
            Segments = segments, HasRandomChoice = random,
            MidiTargetNodeId = segments.Count > 0 ? segments[0].MidiTargetNodeId : midiTarget,
            TempoBpm = segments.Count > 0 ? segments[0].TempoBpm : plan.TempoBpm,
            Problem = segments.Count == 0 ? "the playlist reaches no segment" : null,
        };
    }

    /// <summary>
    /// The MIDI target a music node's notes go to: its own MidiTargetNode property (56) or the nearest
    /// ancestor's, the same way the tempo is inherited. The three Cozmo_Sings switch containers set it, so
    /// a song reached through its playlist directly (the 19 <c>Play__Robot_VO__Singing_*</c> events)
    /// inherits the container's; the two playlists with no container above them (Happy Birthday,
    /// Oh My Darlin') set it themselves, as does one segment. Every shipped song has a target.
    /// </summary>
    public static uint? EffectiveMidiTarget(WwiseSoundLibrary lib, uint nodeId)
    {
        for (int depth = 0; depth < 32; depth++)
        {
            var node = lib.Node(nodeId);
            if (node is not (WwiseMusicSegmentNode or WwiseMusicPlaylistNode or WwiseMusicSwitchNode)) return null;
            if (node.Params.Raw(WwiseProp.MidiTargetNode) is { } t) return t;
            if (node.Params.ParentId == 0) return null;
            nodeId = node.Params.ParentId;
        }
        return null;
    }

    /// <summary>
    /// The tempo a music node plays at: its own meter when that overrides its parent's, otherwise the
    /// nearest ancestor's that does; a root that overrides nothing plays its stored value.
    ///
    /// This is the one reading of the data that accounts for every song. The source duration Wwise wrote
    /// into each of the 46 MIDI clips equals the sequence's end-of-track ticks at 9600 per beat and this
    /// tempo: the playlist's for the 12 songs whose playlist overrides (160, 200, 240, 90 and 100 bpm), the
    /// switch container's 80, 100 or 120 for the other 34. Neither the segment's stored 120 nor the tempo in
    /// the MIDI file header fits them (nine headers disagree with the duration; all 46 fit this rule).
    /// </summary>
    public static float EffectiveTempo(WwiseSoundLibrary lib, uint nodeId)
    {
        float last = 0;
        for (int depth = 0; depth < 32; depth++)
        {
            var (meter, parent) = lib.Node(nodeId) switch
            {
                WwiseMusicSegmentNode s => (s.Meter, s.Params.ParentId),
                WwiseMusicPlaylistNode p => (p.Meter, p.Params.ParentId),
                WwiseMusicSwitchNode w => (w.Meter, w.Params.ParentId),
                _ => ((WwiseMeter?)null, 0u),
            };
            if (meter is null) break;
            last = meter.Value.TempoBpm;
            if (meter.Value.OverridesParent || parent == 0) return last;
            nodeId = parent;
        }
        return last;
    }

    /// <summary>
    /// Flattens a playlist tree. Items are stored in pre-order: a group carries its child count and its
    /// children follow. Sequence groups contribute every child in order, repeated by their loop count
    /// (a loop count of 0 means forever and is taken once here); random groups contribute their first
    /// child and set <paramref name="random"/>.
    /// </summary>
    private static int Flatten(IReadOnlyList<WwisePlaylistItem> items, int index, List<uint> order, ref bool random)
    {
        if (index >= items.Count) return index;
        var item = items[index];
        int next = index + 1;
        if (item.ChildCount == 0)
        {
            int reps = Math.Max(1, (int)item.Loop);
            for (int i = 0; i < reps; i++) order.Add(item.SegmentId);
            return next;
        }
        bool isRandom = item.SequenceType is ContinuousRandom or StepRandom;
        if (isRandom) random = true;
        int reps2 = Math.Max(1, (int)item.Loop);
        var children = new List<uint>();
        for (uint c = 0; c < item.ChildCount; c++)
        {
            bool r = false;
            var sub = new List<uint>();
            next = Flatten(items, next, sub, ref r);
            random |= r;
            if (!isRandom || c == 0) children.AddRange(sub);
        }
        for (int i = 0; i < reps2; i++) order.AddRange(children);
        return next;
    }

    private static WwiseMusicSegmentPlan SegmentPlan(WwiseSoundLibrary lib, WwiseMusicSegmentNode seg)
    {
        var clips = new List<WwiseMusicClipPlan>();
        foreach (var tid in seg.Children)
        {
            if (lib.Node(tid) is not WwiseMusicTrackNode track) continue;
            foreach (var clip in track.Clips)
            {
                uint plugin = track.Sources.FirstOrDefault(s => s.SourceId == clip.SourceId).PluginId;
                clips.Add(new WwiseMusicClipPlan(track.Id, clip.SourceId, plugin, clip));
            }
        }
        return new WwiseMusicSegmentPlan(seg.Id, seg.DurationMs, seg.Meter, EffectiveTempo(lib, seg.Id), clips)
        {
            MidiTargetNodeId = EffectiveMidiTarget(lib, seg.Id),
        };
    }
}
