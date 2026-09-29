namespace Cozmo.Robot.Animation.Wwise;

/// <summary>One thing a play does: a recording, some recordings one after another, or some at once.</summary>
public abstract record WwisePlayNode;

/// <summary>A recording, with the level and pitch summed down the path that reached it.</summary>
public sealed record WwisePlaySound(uint SoundId, uint MediaId, double GainDb, double Cents) : WwisePlayNode;

/// <summary>Its parts play one after another, each starting when the one before it ends.</summary>
public sealed record WwisePlaySequence(IReadOnlyList<WwisePlayNode> Parts) : WwisePlayNode;

/// <summary>Its parts all start at once.</summary>
public sealed record WwisePlayTogether(IReadOnlyList<WwisePlayNode> Parts) : WwisePlayNode;

/// <summary>What one event play resolves to, with whatever could not be resolved named rather than dropped.</summary>
public sealed record WwisePlaybackPlan(uint EventId)
{
    public string? Name { get; init; }
    public WwisePlayNode? Root { get; init; }
    public IReadOnlyList<string> Problems { get; init; } = Array.Empty<string>();
    /// <summary>Every recording the plan will play, in the order it will play them.</summary>
    public IReadOnlyList<WwisePlaySound> Sounds
    {
        get
        {
            var list = new List<WwisePlaySound>();
            void Walk(WwisePlayNode? n)
            {
                switch (n)
                {
                    case WwisePlaySound s: list.Add(s); break;
                    case WwisePlaySequence q: foreach (var c in q.Parts) Walk(c); break;
                    case WwisePlayTogether t: foreach (var c in t.Parts) Walk(c); break;
                }
            }
            Walk(Root);
            return list;
        }
    }
}

/// <summary>
/// Resolves an ordinary (non-music) audio event to what it actually plays, by walking the containers under
/// its Play target with each container's own semantics.
///
/// This is the part <see cref="WwiseSoundLibrary.Resolve"/> deliberately does not do. That walk answers
/// "what could this event ever play", which is the right question for coverage, and it answers it by
/// collecting every Sound beneath the target. It is the wrong question for playing one: it loses which
/// child a random container would have chosen, that a sequence container plays its items one after
/// another, and which branch of a switch container the current switch selects.
///
/// The difference is audible in the singing get-in. <c>Play__Robot_VO__Singing_Getin_1</c> targets a
/// random container of three phrases; each phrase is a sequence of two per-key containers, played
/// continuously; each of those holds three recordings of that note. Flattened, that is 54 recordings and
/// the first one that decodes gets played, every time: one syllable, always the same. Walked, it is one of
/// three phrases, each two notes long, each note drawn from three takes.
///
/// The container fields this reads are all in the bank and all already parsed:
///
/// * <b>Play type</b> (<c>Mode</c>): 0 random, 1 sequence.
/// * <b>Play mode</b> (bit 3 of <c>Flags</c>): step plays one item per event, continuous plays the whole
///   playlist. In the shipped banks 12 of the 13 sequence containers are continuous and 13 of the 455
///   random ones are.
/// * <b>Weights</b> and <b>avoid-repeat count</b> on the playlist, for a random draw.
/// * <b>Switch assignments</b> and the default switch, for a switch container.
/// * <b>Volume</b> and <b>Pitch</b> on every node, summed down the path.
///
/// What Wwise does between those fields is the recovered runtime selection (M6-007, M9-022): a random
/// container draws the k-th eligible item through the global 64-bit LCG with its blocked/avoid list, and
/// a sequence container steps its cursor. A blend container's crossfades would be runtime behaviour, but
/// every shipped blend container has no blend track (M9-019), so a blend container here plays all of its
/// children at the level their own properties give.
/// </summary>
public static class WwisePlayback
{
    /// <summary>Wwise's play mode bit: set means the container plays its whole playlist rather than one item.</summary>
    public const byte ContinuousFlag = 0x08;

    /// <summary>
    /// The game object whose container state an ordinary event's play draws from. Every shipped RanSeq
    /// container has bank bit4 set (M6-007 §3.4), so its state is shared across game objects and this key
    /// does not affect the draw.
    /// </summary>
    private const int GameObject = 0;

    /// <summary>
    /// Builds the plan for an event under the given switch values, drawing from the recovered Wwise
    /// selection engine wherever a container chooses. <paramref name="selection"/> carries the global LCG
    /// and every container's state across plays, as Wwise does; pass the same instance each time.
    /// </summary>
    public static WwisePlaybackPlan Resolve(WwiseSoundLibrary lib, uint eventId,
                                            IReadOnlyDictionary<uint, uint> switches, WwiseSelection selection)
    {
        var resolution = lib.Resolve(eventId);
        var problems = new List<string>();
        var targets = resolution.Actions
            .Where(a => a.ActionType == WwiseBank.PlayAction && a.Target != 0)
            .Select(a => a.Target).ToList();
        if (targets.Count == 0)
            return new WwisePlaybackPlan(eventId)
            {
                Name = resolution.Name,
                Problems = new[] { resolution.Problem ?? "the event fires no Play action" },
            };

        var roots = new List<WwisePlayNode>();
        foreach (var t in targets)
        {
            var node = Walk(lib, t, 0, 0, switches, selection, problems, 0);
            if (node is not null) roots.Add(node);
        }
        return new WwisePlaybackPlan(eventId)
        {
            Name = resolution.Name,
            Root = roots.Count switch { 0 => null, 1 => roots[0], _ => new WwisePlayTogether(roots) },
            Problems = problems,
        };
    }

    private static WwisePlayNode? Walk(WwiseSoundLibrary lib, uint id, double gainDb, double cents,
                                       IReadOnlyDictionary<uint, uint> switches, WwiseSelection selection,
                                       List<string> problems, int depth)
    {
        if (depth > 16) { problems.Add($"node {id}: the hierarchy is deeper than 16 levels"); return null; }
        if (lib.Node(id) is not { } node)
        {
            problems.Add($"node {id} is not readable");
            return null;
        }
        gainDb += node.Params.Float(WwiseProp.Volume) ?? 0;
        cents += node.Params.Float(WwiseProp.Pitch) ?? 0;

        switch (node)
        {
            case WwiseSoundNode s:
                if (s.IsSourcePlugin)
                {
                    problems.Add($"sound {s.Id} is a source plug-in ({s.PluginId:X8}), which makes its own audio");
                    return null;
                }
                return new WwisePlaySound(s.Id, s.MediaId, gainDb, cents);

            case WwiseRandomSequenceNode rs:
            {
                if (rs.Playlist.Count == 0) { problems.Add($"container {rs.Id} has an empty playlist"); return null; }
                bool continuous = (rs.Flags & ContinuousFlag) != 0;
                if (continuous)
                {
                    var order = rs.IsSequence ? rs.Playlist.Select(p => p.ChildId).ToList() : Shuffle(rs, selection.Rng);
                    var parts = new List<WwisePlayNode>();
                    foreach (var child in order)
                        if (Walk(lib, child, gainDb, cents, switches, selection, problems, depth + 1) is { } n)
                            parts.Add(n);
                    return parts.Count == 0 ? null : new WwisePlaySequence(parts);
                }
                // fidelity: M9-022
                int index = selection.NextIndex(rs.Id, GameObject, WwiseContainerSelectionSettings.FromNode(rs));
                if (index < 0) { problems.Add($"container {rs.Id} selected no item"); return null; }
                return Walk(lib, rs.Playlist[index].ChildId, gainDb, cents, switches, selection, problems, depth + 1);
            }

            case WwiseSwitchNode sw:
            {
                uint value = switches.TryGetValue(sw.GroupId, out var v) ? v : sw.DefaultSwitch;
                var chosen = sw.Assignments.FirstOrDefault(a => a.SwitchId == value).NodeIds
                             ?? sw.Assignments.FirstOrDefault(a => a.SwitchId == sw.DefaultSwitch).NodeIds;
                if (chosen is null || chosen.Count == 0)
                {
                    problems.Add($"switch container {sw.Id}: group {sw.GroupId} value {value} selects nothing");
                    return null;
                }
                var parts = new List<WwisePlayNode>();
                foreach (var child in chosen)
                    if (Walk(lib, child, gainDb, cents, switches, selection, problems, depth + 1) is { } n)
                        parts.Add(n);
                return parts.Count switch { 0 => null, 1 => parts[0], _ => new WwisePlayTogether(parts) };
            }

            case WwiseBlendNode or WwiseActorMixerNode:
            {
                var parts = new List<WwisePlayNode>();
                foreach (var child in node.Children)
                    if (Walk(lib, child, gainDb, cents, switches, selection, problems, depth + 1) is { } n)
                        parts.Add(n);
                return parts.Count switch { 0 => null, 1 => parts[0], _ => new WwisePlayTogether(parts) };
            }

            default:
                problems.Add($"node {id} is a {node.Type}, which is not an ordinary audio target");
                return null;
        }
    }

    /// <summary>
    /// A continuous random container plays its whole playlist, in an order drawn without replacement. The
    /// continuous path itself is M6-008 and its algorithm is not recovered; this keeps the stack's
    /// shuffle but sources the draw from the recovered global LCG rather than <see cref="Random"/>.
    /// </summary>
    private static List<uint> Shuffle(WwiseRandomSequenceNode rs, WwiseRng rng)
    {
        var items = rs.Playlist.Select(p => p.ChildId).ToList();
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = (int)(rng.Next() % (uint)(i + 1));
            (items[i], items[j]) = (items[j], items[i]);
        }
        return items;
    }
}
