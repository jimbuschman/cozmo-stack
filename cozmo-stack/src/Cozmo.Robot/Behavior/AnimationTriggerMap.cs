using System.Text.Json;
using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// Every step taken to turn a trigger into a clip, kept whole so a selection can be explained rather than
/// asserted. Nothing here is inferred: each field is filled in by the step that produced it.
/// </summary>
public sealed record TriggerResolution(AnimationTrigger Trigger)
{
    /// <summary>The group the shipped map names for this trigger, if it names one.</summary>
    public string? GroupName { get; init; }
    /// <summary>The clips that group holds, before selection.</summary>
    public IReadOnlyList<string> Candidates { get; init; } = Array.Empty<string>();
    /// <summary>The clip chosen, when one could be.</summary>
    public string? Selected { get; init; }
    /// <summary>The mood the choice was made under, when the caller supplied one.</summary>
    public string? Mood { get; init; }
    /// <summary>Why no clip came out, when none did.</summary>
    public string? Problem { get; init; }

    public bool Resolved => Selected is not null;
}

/// <summary>
/// The shipped trigger-to-group table: Anki's own <c>AnimationTriggerMap.json</c> from the OBB.
///
/// This is the table the engine uses to answer "what should Cozmo play when X happens" without naming a
/// clip. It is read as data. **No mapping is invented here**: a trigger the shipped file does not name
/// resolves to nothing and says so, rather than falling back to something plausible.
///
/// In this build the file holds 573 pairs covering 505 distinct groups, and every <c>CladEvent</c> it
/// names exists in <see cref="AnimationTrigger"/>. The two triggers with no entry are
/// <c>ProceduralLive</c> and <c>ReactToMotorCalibration</c>, which the engine handles by other means.
/// </summary>
public sealed class AnimationTriggerMap
{
    private readonly Dictionary<AnimationTrigger, string> _groups = new();
    private readonly List<string> _unknownTriggers = new();

    /// <summary>How many triggers the loaded file mapped.</summary>
    public int Count => _groups.Count;

    /// <summary>
    /// Names in the file that are not triggers this build knows. Empty for the shipped file; a non-empty
    /// list means the map and the enum came from different builds.
    /// </summary>
    public IReadOnlyList<string> UnknownTriggers => _unknownTriggers;

    /// <summary>The file this was read from.</summary>
    public string Source { get; private init; } = "";

    /// <summary>
    /// Loads <c>AnimationTriggerMap.json</c>. Accepts either the file itself or a directory that contains
    /// it, at any depth, so the OBB root can be passed straight in.
    /// </summary>
    public static AnimationTriggerMap Load(string path)
    {
        var file = ResolveFile(path)
            ?? throw new FileNotFoundException($"no AnimationTriggerMap.json under '{path}'");
        var map = new AnimationTriggerMap { Source = file };

        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        if (!doc.RootElement.TryGetProperty("Pairs", out var pairs) || pairs.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"{file}: no Pairs array");

        foreach (var pair in pairs.EnumerateArray())
        {
            var ev = pair.TryGetProperty("CladEvent", out var e) ? e.GetString() : null;
            var anim = pair.TryGetProperty("AnimName", out var a) ? a.GetString() : null;
            if (ev is null || anim is null) continue;
            if (Enum.TryParse<AnimationTrigger>(ev, ignoreCase: false, out var trigger))
                map._groups[trigger] = anim;
            else
                map._unknownTriggers.Add(ev);
        }
        return map;
    }

    private static string? ResolveFile(string path)
    {
        if (File.Exists(path)) return path;
        if (!Directory.Exists(path)) return null;
        var direct = Path.Combine(path, "AnimationTriggerMap.json");
        if (File.Exists(direct)) return direct;
        return Directory.EnumerateFiles(path, "AnimationTriggerMap.json", SearchOption.AllDirectories)
                        .FirstOrDefault();
    }

    /// <summary>The group a trigger names, or null when the shipped map does not name one.</summary>
    public string? GroupFor(AnimationTrigger trigger) =>
        _groups.TryGetValue(trigger, out var g) ? g : null;

    /// <summary>Every trigger the map covers.</summary>
    public IReadOnlyCollection<AnimationTrigger> Triggers => _groups.Keys;

    /// <summary>
    /// Walks the whole chain for one trigger and records each step:
    /// trigger, group, candidate clips, selection.
    ///
    /// Selection itself is M5's <see cref="AnimationGroup.Choose"/>, which honours the group's own weights
    /// and mood, so this adds no policy of its own.
    /// </summary>
    public TriggerResolution Resolve(AnimationTrigger trigger, AnimationLibrary library,
                                     Random? random = null, string? mood = null)
    {
        var group = GroupFor(trigger);
        if (group is null)
            return new TriggerResolution(trigger)
            {
                Mood = mood,
                Problem = "the shipped trigger map names no group for this trigger",
            };

        var g = library.GetGroup(group);
        if (g is null)
            return new TriggerResolution(trigger)
            {
                GroupName = group, Mood = mood,
                Problem = $"the map names group '{group}', which the animation assets do not define",
            };

        var candidates = g.Entries.Select(c => c.Name).ToList();
        var pick = g.Choose(random ?? Random.Shared, mood);
        return new TriggerResolution(trigger)
        {
            GroupName = group,
            Candidates = candidates,
            Selected = pick?.Name,
            Mood = mood,
            Problem = pick is null
                ? (candidates.Count == 0
                    ? $"group '{group}' is empty"
                    : $"group '{group}' has {candidates.Count} clips but none was selectable" +
                      (mood is null ? "" : $" under mood '{mood}'"))
                : null,
        };
    }
}
