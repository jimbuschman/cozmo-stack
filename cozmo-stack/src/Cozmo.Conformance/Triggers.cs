using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;

namespace Cozmo.Conformance;

/// <summary>
/// Offline diagnostics for the shipped animation-trigger system: what Cozmo would play when something
/// happens, and why. No robot involved.
/// </summary>
public static class TriggersTool
{
    /// <summary>
    /// <c>triggers &lt;obb-dir&gt; [--trigger &lt;name&gt;] [--mood &lt;mood&gt;] [--seed N] [--coverage] [--limit N]</c>
    /// </summary>
    public static int Run(string[] a)
    {
        var dir = a.Length > 1 && !a[1].StartsWith("--") ? a[1] : null;
        if (dir is null)
        {
            Console.WriteLine("need the OBB directory (or one holding AnimationTriggerMap.json and the animation assets)");
            return 1;
        }
        var which = Arg(a, "--trigger");
        var mood = Arg(a, "--mood");
        int limit = int.TryParse(Arg(a, "--limit"), out var l) ? l : 20;
        var random = int.TryParse(Arg(a, "--seed"), out var seed) ? new Random(seed) : new Random(1);

        AnimationTriggerMap map;
        try { map = AnimationTriggerMap.Load(dir); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { Console.WriteLine(ex.Message); return 1; }

        var assets = FindAssetsRoot(dir);
        if (assets is null)
        { Console.WriteLine($"no directory holding animations and animationGroups under '{dir}'"); return 1; }
        var lib = AnimationLibrary.Open(assets);

        Console.WriteLine($"trigger map: {map.Count} triggers from {map.Source}");
        Console.WriteLine($"animations:  {lib.ClipNames.Count} clips, {lib.GroupNames.Count} groups from {assets}");
        if (map.UnknownTriggers.Count > 0)
            Console.WriteLine($"  !! {map.UnknownTriggers.Count} mapped names are not triggers this build knows, " +
                              $"e.g. {string.Join(", ", map.UnknownTriggers.Take(3))}");

        if (which is not null)
        {
            if (!Enum.TryParse<AnimationTrigger>(which, ignoreCase: true, out var t))
            {
                Console.WriteLine($"'{which}' is not an animation trigger");
                var near = Enum.GetNames<AnimationTrigger>()
                    .Where(n => n.Contains(which, StringComparison.OrdinalIgnoreCase)).Take(8).ToList();
                if (near.Count > 0) Console.WriteLine($"  did you mean: {string.Join(", ", near)}");
                return 1;
            }
            return One(map, lib, t, random, mood);
        }
        return Coverage(map, lib, random, mood, limit);
    }

    /// <summary>Prints the whole chain for one trigger, as evidence rather than as a claim.</summary>
    private static int One(AnimationTriggerMap map, AnimationLibrary lib, AnimationTrigger t,
                           Random random, string? mood)
    {
        var r = map.Resolve(t, lib, random, mood);
        Console.WriteLine($"\ntrigger {t}");
        Console.WriteLine($"  -> group {r.GroupName ?? "(none in the shipped map)"}");
        if (r.Candidates.Count > 0)
        {
            Console.WriteLine($"  -> {r.Candidates.Count} candidate animation(s)");
            foreach (var c in r.Candidates.Take(12)) Console.WriteLine($"       {c}");
            if (r.Candidates.Count > 12) Console.WriteLine($"       ... and {r.Candidates.Count - 12} more");
        }
        if (mood is not null) Console.WriteLine($"  -> mood filter '{mood}'");
        if (r.Selected is not null)
        {
            Console.WriteLine($"  -> selected {r.Selected}");
            Console.WriteLine($"  -> clip is {(lib.HasClip(r.Selected) ? "present in the assets" : "NOT present in the assets")}");
        }
        else Console.WriteLine($"  !! {r.Problem}");
        return r.Resolved ? 0 : 1;
    }

    /// <summary>
    /// How much of the shipped trigger table actually resolves to a playable clip. This is the number
    /// that says whether the reactive layer has anything real underneath it.
    /// </summary>
    private static int Coverage(AnimationTriggerMap map, AnimationLibrary lib, Random random,
                                string? mood, int limit)
    {
        int total = 0, mapped = 0, resolved = 0, clipPresent = 0;
        var noGroup = new List<string>();
        var missingGroup = new List<string>();
        var missingClip = new List<string>();

        foreach (var t in Enum.GetValues<AnimationTrigger>())
        {
            total++;
            var r = map.Resolve(t, lib, random, mood);
            if (r.GroupName is null) { if (noGroup.Count < limit) noGroup.Add(t.ToString()); continue; }
            mapped++;
            if (!r.Resolved)
            {
                if (missingGroup.Count < limit) missingGroup.Add($"{t} -> {r.GroupName}: {r.Problem}");
                continue;
            }
            resolved++;
            if (lib.HasClip(r.Selected!)) clipPresent++;
            else if (missingClip.Count < limit) missingClip.Add($"{t} -> {r.GroupName} -> {r.Selected}");
        }

        Console.WriteLine($"\ntrigger coverage{(mood is null ? "" : $" under mood '{mood}'")}");
        Console.WriteLine($"  triggers defined            {total}");
        Console.WriteLine($"  named a group by the map    {mapped}  ({100.0 * mapped / total:F1}%)");
        Console.WriteLine($"  selected an animation       {resolved}  ({100.0 * resolved / Math.Max(1, mapped):F1}% of mapped)");
        Console.WriteLine($"  and that clip is present    {clipPresent}  ({100.0 * clipPresent / Math.Max(1, resolved):F1}% of selected)");

        if (noGroup.Count > 0)
            Console.WriteLine($"\n  triggers the shipped map names no group for: {string.Join(", ", noGroup)}");
        if (missingGroup.Count > 0)
        {
            Console.WriteLine($"\n  first {missingGroup.Count} that named a group but selected nothing:");
            foreach (var m in missingGroup) Console.WriteLine($"    {m}");
        }
        if (missingClip.Count > 0)
        {
            Console.WriteLine($"\n  first {missingClip.Count} whose selected clip is absent from the assets:");
            foreach (var m in missingClip) Console.WriteLine($"    {m}");
        }
        return 0;
    }

    /// <summary>
    /// Finds the assets root: the directory holding both <c>animations</c> and <c>animationGroups</c>.
    /// The groups are what the trigger map names, so pointing at <c>animations</c> alone silently
    /// resolves nothing — which it did on the first run here.
    /// </summary>
    internal static string? FindAssetsRoot(string root)
    {
        if (!Directory.Exists(root)) return null;
        if (Directory.Exists(Path.Combine(root, "animationGroups"))) return root;
        var direct = Path.Combine(root, "assets", "cozmo_resources", "assets");
        if (Directory.Exists(Path.Combine(direct, "animationGroups"))) return direct;
        return Directory.EnumerateDirectories(root, "animationGroups", SearchOption.AllDirectories)
                        .Select(Path.GetDirectoryName)
                        .FirstOrDefault(d => d is not null);
    }

    private static string? Arg(string[] a, string name)
    {
        for (int i = 1; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return null;
    }
}
