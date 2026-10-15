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
/// The shipped trigger-to-group container: every JSON file under the OBB's <c>assets/animationGroupMaps</c>.
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

    /// <summary>The directory or explicit host file this container was read from.</summary>
    public string Source { get; private init; } = "";

    /// <summary>Loads the complete resource map directory, or one explicitly named file for host tools.</summary>
    public static AnimationTriggerMap Load(string path) => Load(path, null, null);

    // fidelity: M5-034 - checked Load loop 0x00670438..0x0067075C and JsonCpp callees.
    private sealed class TerminatingInput : Exception
    {
        internal TerminatingInput(string reason) : base(reason) { }
    }

    internal static AnimationTriggerMap Load(string path, Action<string>? log, SourceLogSink? sourceLog)
    {
        if (File.Exists(path)) return LoadFiles(new[] { path }, path, log, sourceLog);
        var dir = ResourceDirectory(path);
        if (dir is not null) return LoadDirectory(dir, log, sourceLog);
        // Explicit host-tool convenience only: no recursive search outside animationGroupMaps.
        var direct = Path.Combine(path, "AnimationTriggerMap.json");
        if (File.Exists(direct)) return LoadFiles(new[] { direct }, direct, log, sourceLog);
        throw new FileNotFoundException($"no animationGroupMaps directory under '{path}'");
    }

    internal static string? ResourceDirectory(string root)
    {
        if (string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(root)), "animationGroupMaps", StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(root)) return root;
        foreach (var relative in new[] { "animationGroupMaps", "assets/animationGroupMaps", "assets/cozmo_resources/assets/animationGroupMaps" })
        {
            var directory = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(directory)) return directory;
        }
        return null;
    }

    internal static AnimationTriggerMap LoadDirectory(string directory, Action<string>? log, SourceLogSink? sourceLog,
        AnimationTriggerMap? previous = null) =>
        LoadFiles(AnimationLibrary.WalkFilesInReaddirOrder(directory).Where(file => file.EndsWith("json", StringComparison.Ordinal)), directory, log, sourceLog, previous);

    internal static AnimationTriggerMap LoadFiles(IEnumerable<string> files, string source, Action<string>? log,
        SourceLogSink? sourceLog, AnimationTriggerMap? previous = null)
    {
        var map = new AnimationTriggerMap { Source = source };
        bool completedFile = false, skippedFile = false;
        foreach (var file in files)
        {
            Dictionary<string, string> entries;
            try
            {
                // Stage a file so M1-034 loses only that file, including inserts preceding its fatal input.
                entries = ReadFile(file, log, sourceLog);
                completedFile = true;
            }
            catch (Exception e) when (e is TerminatingInput or JsonClipLoader.JsonCppException)
            {
                skippedFile = true;
                string reason = e is JsonClipLoader.JsonCppException ? "asString throw 0x008E48BE..0x008E4964: " + e.Message : e.Message;
                string message = $"M1-034 / M7-002: skipping malformed {Path.GetFileName(file)}; M5-034 file adds nothing; {file}; {reason}";
                if (log is not null) log(message); else SteppedBehavior.ReportMissing(message);
                continue;
            }
            foreach (var entry in entries)
            {
                if (!map._groups.TryAdd(entry.Key, entry.Value)) continue;
                if (!Enum.TryParse<AnimationTrigger>(entry.Key, false, out var trigger) || Enum.GetName(trigger) != entry.Key)
                    map._unknownTriggers.Add(entry.Key);
            }
        }
        // Existing host reload policy: a wholly terminating reload keeps the prior published map.
        // It never seeds the new container or changes first-wins between this load's completed files.
        return skippedFile && !completedFile && previous is not null ? previous : map;
    }

    private static Dictionary<string, string> ReadFile(string file, Action<string>? log, SourceLogSink? sourceLog)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        using var doc = AnimationLibrary.ReadAsJson(file, log, sourceLog);
        if (doc is null || doc.RootElement.ValueKind == JsonValueKind.Null) return entries;
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new TerminatingInput("non-object root at isMember 0x006704A0 / find throw 0x008EAF8A");
        if (!doc.RootElement.TryGetProperty("Pairs", out var pairs)) return entries;
        if (pairs.ValueKind != JsonValueKind.Array)
        {
            // size() is zero for null/scalars/empty objects: no indexed call, hence no native throw.
            if (pairs.ValueKind == JsonValueKind.Object && pairs.EnumerateObject().Any())
                throw new TerminatingInput("Pairs integer indexing throws at 0x008EA444..0x008EA446 / 0x008EA52C");
            return entries;
        }
        foreach (var pair in pairs.EnumerateArray())
        {
            if (pair.ValueKind is not (JsonValueKind.Null or JsonValueKind.Object))
                throw new TerminatingInput("non-object pair at find throw 0x008EAF8A");
            string Member(string key) => pair.ValueKind == JsonValueKind.Null || !pair.TryGetProperty(key, out var value)
                ? "" : JsonClipLoader.AsString(value);
            entries.TryAdd(Member("CladEvent"), Member("AnimName"));
        }
        return entries;
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
