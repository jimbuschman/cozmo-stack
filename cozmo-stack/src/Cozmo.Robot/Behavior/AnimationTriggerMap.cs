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
// fidelity: M7-002
public sealed class AnimationTriggerMap
{
    private readonly Dictionary<string, string> _groups = new(StringComparer.Ordinal);
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
    public static AnimationTriggerMap Load(string path) => Load(path, null, null);
    // fidelity: M5-034 — checked Load 0x00670438..0x0067075C, failure rows 0x00670492..0x006704F4.
    internal sealed class NonObjectRootException : Exception { }
    internal static AnimationTriggerMap Load(string path, Action<string>? log, SourceLogSink? sourceLog)
    {
        var file = ResolveFile(path)
            ?? throw new FileNotFoundException($"no AnimationTriggerMap.json under '{path}'");
        var map = new AnimationTriggerMap { Source = file };

        using var doc = AnimationLibrary.ReadAsJson(file, log, sourceLog);
        if (doc is null) return map; // false read: its own Error/errG, not an M1-034 path.
        if (doc.RootElement.ValueKind == JsonValueKind.Null) return map;
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new NonObjectRootException();
        if (!doc.RootElement.TryGetProperty("Pairs", out var pairs) || pairs.ValueKind != JsonValueKind.Array)
            return map;

        foreach (var pair in pairs.EnumerateArray())
        {
            var ev = pair.TryGetProperty("CladEvent", out var e) ? JsonClipLoader.AsString(e) : null;
            var anim = pair.TryGetProperty("AnimName", out var a) ? JsonClipLoader.AsString(a) : null;
            if (ev is null || anim is null) continue;
            map._groups.TryAdd(ev, anim); // native string key and first insertion; no numeric enum parsing.
            if (!Enum.TryParse<AnimationTrigger>(ev, ignoreCase: false, out var trigger) || Enum.GetName(trigger) != ev)
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

    internal AnimationTriggerResponses ToResponses() => AnimationTriggerResponses.FromPairs(_groups);

    /// <summary>The group a trigger names, or null when the shipped map does not name one.</summary>
    public string? GroupFor(AnimationTrigger trigger) =>
        Enum.GetName(trigger) is {} key && _groups.TryGetValue(key, out var g) ? g : null;

    /// <summary>Every trigger the map covers.</summary>
    public IReadOnlyCollection<AnimationTrigger> Triggers => _groups.Keys
        .Where(k => Enum.TryParse<AnimationTrigger>(k, false, out var t) && Enum.GetName(t) == k)
        .Select(k => Enum.Parse<AnimationTrigger>(k)).ToArray();

    /// <summary>
    /// Walks the whole chain for one trigger and records each step:
    /// trigger, group, candidate clips, selection.
    ///
    /// Selection itself is M5's <see cref="AnimationLibrary.GetAnimationNameFromGroup"/>, with the library generator,
    /// mood, engine clock and head angle. Legacy random/mood arguments describe the request but do not override those inputs.
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

        // fidelity: M5-065 — the native C3 lookup must run even when the host diagnostic probe misses.
        var name = library.GetAnimationNameFromGroup(group, strict: false);
        var g = library.GetGroup(group); // silent, host-only candidate/presence probe
        var candidates = g?.Entries.Select(c => c.Name).ToList() ?? new List<string>();
        return new TriggerResolution(trigger)
        {
            GroupName = group,
            Candidates = candidates,
            Selected = name.Length == 0 ? null : name,
            Mood = mood,
            Problem = g is null ? $"the map names group '{group}', which the animation assets do not define" : name.Length == 0
                ? (candidates.Count == 0
                    ? $"group '{group}' is empty"
                    : $"group '{group}' has {candidates.Count} clips but none was selectable" +
                      (mood is null ? "" : $" under mood '{mood}'"))
                : null,
        };
    }
}
