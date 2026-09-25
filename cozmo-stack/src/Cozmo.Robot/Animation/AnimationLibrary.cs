using System.Text.Json;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation;

/// <summary>SimpleMoodType (Unity SimpleMoodType.cs): the mood an animation group entry is for.</summary>
public enum SimpleMood : byte { Happy, Sad, Default, Count }

// fidelity: M5-011, M5-014
/// <summary>
/// One choice inside an animation group (D4, 0x0058C4C0..0x0058C7BE): Name, Weight, Mood (SimpleMoodType),
/// CooldownTime_Sec (default 0), UseHeadAngle and HeadAngleMin/Max_Deg (converted to radians for the gate).
/// </summary>
public sealed record AnimationGroupEntry(string Name, float Weight, float CooldownSec, string Mood)
{
    /// <summary>Whether this entry is only eligible while the head sits inside its window.</summary>
    public bool UseHeadAngle { get; init; }
    /// <summary>The window, in degrees as the asset holds it.</summary>
    public float HeadAngleMinDeg { get; init; }
    public float HeadAngleMaxDeg { get; init; }

    /// <summary>The group's container, whose cooldown map this entry's name keys (D7).</summary>
    internal GroupContainer Container { get; set; } = new();

    /// <summary>The mood as SimpleMoodType; a name that is none of them matches no request.</summary>
    public SimpleMood MoodType => Enum.TryParse<SimpleMood>(Mood, ignoreCase: false, out var m) ? m : SimpleMood.Count;

    public double HeadAngleMinRad => HeadAngleMinDeg * Math.PI / 180.0;
    public double HeadAngleMaxRad => HeadAngleMaxDeg * Math.PI / 180.0;

    /// <summary>D7: on cooldown iff the name's cooldown end (in the container) is after now.</summary>
    public bool IsOnCooldown(double nowSec) => Container.IsOnCooldown(Name, nowSec);

    /// <summary>When this entry's name comes off cooldown (the container's end time); negative infinity when never set.</summary>
    public double CooldownEndsSec => Container.CooldownEnd(Name);

    /// <summary>The head-angle gate (D5): true without UseHeadAngle or without a known angle, else min ≤ angle ≤ max.</summary>
    public bool HeadAngleAllows(double? headAngleDeg) =>
        !UseHeadAngle || headAngleDeg is not { } a || HeadAngleAllowsRad(a * Math.PI / 180.0);

    internal bool HeadAngleAllowsRad(double rad) => !UseHeadAngle || (rad >= HeadAngleMinRad && rad <= HeadAngleMaxRad);
}

// fidelity: M5-014
/// <summary>
/// The container's cooldown map (D7, 0x0058BA28..0x0058BA98): keyed by animation name, shared across groups; on cooldown
/// iff end &gt; now.
/// </summary>
internal sealed class GroupContainer
{
    private readonly Dictionary<string, double> _ends = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public bool IsOnCooldown(string name, double now) { lock (_gate) return _ends.TryGetValue(name, out var e) && e > now; }
    public double CooldownEnd(string name) { lock (_gate) return _ends.TryGetValue(name, out var e) ? e : double.NegativeInfinity; }
    public void SetCooldown(string name, double end) { lock (_gate) _ends[name] = end; }
    public double TimeUntilCooldownOver(string name, double now) => CooldownEnd(name) - now;
}

// fidelity: M5-011, M5-014
/// <summary>A named set of interchangeable animations, as the robot's own <c>animationGroups</c> assets define them.</summary>
public sealed class AnimationGroup
{
    private IReadOnlyList<AnimationGroupEntry> _entries = Array.Empty<AnimationGroupEntry>();

    public string Name { get; init; } = "";

    public IReadOnlyList<AnimationGroupEntry> Entries
    {
        get => _entries;
        init
        {
            _entries = value;
            var shared = new GroupContainer();
            foreach (var e in value) e.Container = shared;
        }
    }

    internal void UseContainer(GroupContainer c)
    {
        foreach (var e in _entries) e.Container = c;
    }

    /// <summary>
    /// <c>AnimationGroup::GetAnimationName</c> (D5, D6; 0x0058A946..0x0058AE2A):
    /// <list type="bullet">
    /// <item>candidates: mood == the requested mood (null asks for Default), inside the head window when UseHeadAngle
    /// (the robot's head angle in radians), and not on cooldown;</item>
    /// <item>a weighted draw r = RandDbl(Σw), subtracting each weight and taking the entry where r goes below 0, or the
    /// last candidate; the pick's cooldown end set to now + CooldownTime_Sec;</item>
    /// <item>none and the mood not Default: again with Default;</item>
    /// <item>in Default, when some Default entry exists and not strict: the Default entry with the smallest
    /// TimeUntilCooldownOver among those whose [min − 0.05, max + 0.05] rad window holds the head angle, whatever their
    /// UseHeadAngle, and no cooldown set; without one, the first entry of the list; otherwise (or strict) an error and
    /// nothing.</item>
    /// </list>
    /// A null <paramref name="nowSec"/> leaves the cooldown out and a null <paramref name="headAngleDeg"/> the head gate
    /// (this stack's callers without a clock or a robot).
    /// </summary>
    public AnimationGroupEntry? Choose(Random random, string? mood = null, double? nowSec = null, double? headAngleDeg = null,
                                       bool strict = false)
    {
        var requested = mood is null ? SimpleMood.Default
            : Enum.TryParse<SimpleMood>(mood, ignoreCase: true, out var m) ? m : SimpleMood.Count;
        double? headRad = headAngleDeg is { } d ? d * Math.PI / 180.0 : null;
        return GetAnimationName(new EngineRandom(random), requested, nowSec, headRad, strict);
    }

    internal AnimationGroupEntry? GetAnimationName(EngineRandom rng, SimpleMood mood, double? nowSec, double? headRad, bool strict)
    {
        while (true)
        {
            var candidates = _entries.Where(e => e.MoodType == mood
                                                 && (headRad is not { } h || e.HeadAngleAllowsRad(h))
                                                 && (nowSec is not { } n || !e.IsOnCooldown(n))).ToList();
            if (candidates.Count > 0)
            {
                double total = 0;
                foreach (var c in candidates) total += c.Weight;
                double r = rng.RandDbl(total);
                AnimationGroupEntry pick = candidates[^1];
                foreach (var c in candidates)
                {
                    r -= c.Weight;
                    if (r < 0) { pick = c; break; }
                }
                if (nowSec is { } now) pick.Container.SetCooldown(pick.Name, now + pick.CooldownSec);
                return pick;
            }
            if (mood != SimpleMood.Default)
            {
                mood = SimpleMood.Default;
                continue;
            }
            var defaults = _entries.Where(e => e.MoodType == SimpleMood.Default).ToList();
            if (defaults.Count > 0 && !strict)
            {
                AnimationGroupEntry? backup = null;
                double best = double.PositiveInfinity;
                foreach (var e in defaults)
                {
                    if (headRad is { } h && !(h >= e.HeadAngleMinRad - 0.05 && h <= e.HeadAngleMaxRad + 0.05)) continue;
                    double left = nowSec is { } n ? e.Container.TimeUntilCooldownOver(e.Name, n) : 0;
                    if (backup is null || left < best) { backup = e; best = left; }
                }
                return backup ?? _entries[0];
            }
            return null;
        }
    }

    public override string ToString() => $"{Name}: {Entries.Count} animation(s)";
}

// fidelity: M5-034
/// <summary>
/// <c>AnimationTriggerResponsesContainer</c> (gap1 C7, 0x00670438..0x0067075C; D8): every "*.json" under
/// assets/animationGroupMaps, recursively; a file is used only if it parses and has "Pairs"; each entry CladEvent →
/// AnimName, inserted only when the key is absent (the first wins), with no enum check. GetResponse(trigger) looks up
/// EnumToString(trigger): found, an info log and the name; not found, a warning and "".
/// </summary>
public sealed class AnimationTriggerResponses
{
    private readonly Dictionary<string, string> _pairs = new(StringComparer.Ordinal);

    public Action<string>? Log { get; set; }
    public int Count => _pairs.Count;

    public static AnimationTriggerResponses Load(string? dir)
    {
        var r = new AnimationTriggerResponses();
        if (dir is null || !Directory.Exists(dir)) return r;
        foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(f));
                if (!doc.RootElement.TryGetProperty("Pairs", out var pairs) || pairs.ValueKind != JsonValueKind.Array) continue;
                foreach (var p in pairs.EnumerateArray())
                {
                    string key = p.TryGetProperty("CladEvent", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString()! : "";
                    string val = p.TryGetProperty("AnimName", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
                    r._pairs.TryAdd(key, val);
                }
            }
            catch (JsonException) { }
        }
        return r;
    }

    /// <summary>GetResponse (0x00670810..0x00670920).</summary>
    public string GetResponse(AnimationTrigger trigger)
    {
        string key = trigger.ToString();
        if (_pairs.TryGetValue(key, out var name))
        {
            Log?.Invoke($"info: AnimationTriggerResponsesContainer.GetResponse: {key} -> {name}");
            return name;
        }
        Log?.Invoke($"warning: AnimationTriggerResponsesContainer.GetResponse: Animation requested for unknown response '{key}'");
        return "";
    }

    /// <summary>HasResponse (0x00670AD0, not re-read in the inventory): whether the key is present, with no log.</summary>
    public bool HasResponse(AnimationTrigger trigger) => _pairs.ContainsKey(trigger.ToString());
}

// fidelity: M5-001, M5-006, M5-009, M5-011, M5-014, M5-016, M5-033, M5-034
/// <summary>
/// Cozmo's own animation assets: the clips (<c>CannedAnimationContainer</c>), the groups (<c>AnimationGroupContainer</c>)
/// and the trigger map (<see cref="AnimationTriggerResponses"/>). Clips come from assets/animations/ and
/// config/engine/animations/ (gap1 C5): a ".bin" is FlatBuffer, anything else JSON (C4). Loading is lazy per file.
/// </summary>
public sealed class AnimationLibrary : IAnimationCatalog
{
    private const int ClipsField = 0;
    private const int ClipName = 0, ClipKeyframes = 1;
    private const int KfLift = 0, KfFace = 1, KfHead = 2, KfAudio = 3, KfLights = 4,
                      KfFaceAnim = 5, KfEvent = 6, KfBody = 7, KfRecordHeading = 8, KfTurnToHeading = 9;

    private readonly Dictionary<string, string> _clipFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _jsonClipFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AnimationClip> _clipCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AnimationGroup> _groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly GroupContainer _cooldowns = new();
    private readonly object _gate = new();

    /// <summary>
    /// The RNG the group draw uses: the context RNG (AnimationGroupContainer+0x28 → context+0x14, gap4 R3), shared with the
    /// streamer's live idle and layer managers. Unset: an entropy-seeded generator of its own.
    /// </summary>
    public EngineRandom ContextRandom { get; set; } = new();

    /// <summary>The clip names available (FlatBuffer and JSON), whether or not they have been parsed yet.</summary>
    public IReadOnlyCollection<string> ClipNames { get { lock (_gate) return _clipFiles.Keys.ToArray(); } }

    /// <summary>The JSON clips found (gap2 J1..J5: the four in config/engine/animations), also in <see cref="ClipNames"/>.</summary>
    public IReadOnlyCollection<string> JsonClipNames { get { lock (_gate) return _jsonClipFiles.Keys.ToArray(); } }
    /// <summary>Group names available.</summary>
    public IReadOnlyCollection<string> GroupNames { get { lock (_gate) return _groups.Keys.ToArray(); } }

    /// <summary>The trigger map (assets/animationGroupMaps).</summary>
    public AnimationTriggerResponses Triggers { get; private set; } = new();

    /// <summary>The engine log for the load errors (C2) and the group errors.</summary>
    public Action<string>? Log { get; set; }

    /// <summary>The robot's simple mood the groups choose by (MoodManager, M7). Unset: Default.</summary>
    public Func<SimpleMood>? CurrentMood { get; set; }

    /// <summary>
    /// The time the group cooldowns use: MoodManager+0x130, its last-update seconds (D5; M7). Unset: this machine's
    /// monotonic clock in seconds, a stand-in until MoodManager is wired.
    /// </summary>
    public Func<double>? CooldownTimeSec { get; set; }

    /// <summary>The robot's head angle in radians (robot+0x2FC) for the head-angle gate. Unset: no gate.</summary>
    public Func<double?>? HeadAngleRad { get; set; }

    /// <summary>
    /// Opens a library over an unpacked <c>cozmo_resources</c> tree. Point it at the directory that holds
    /// <c>assets/animations</c>, or directly at either directory.
    /// </summary>
    public static AnimationLibrary Open(string assetsRoot)
    {
        var lib = new AnimationLibrary();
        var animations = FindDir(assetsRoot, "animations");
        var groups = FindDir(assetsRoot, "animationGroups");
        if (animations is null && groups is null)
            throw new DirectoryNotFoundException(
                $"no animations or animationGroups directory under {assetsRoot}. Point this at the unpacked " +
                "cozmo_resources assets directory.");

        // gap1 C5: assets/animations/ then config/engine/animations/, json and bin
        foreach (var dir in new[] { animations, FindConfigAnimations(assetsRoot) })
        {
            if (dir is null) continue;
            foreach (var f in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f);
                if (!ext.Equals(".bin", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    bool json = !ext.Equals(".bin", StringComparison.OrdinalIgnoreCase);
                    foreach (var name in ClipNamesIn(f))
                    {
                        // gap1 C3, gap4 P1: FaceAnimationManager::ProceduralAnimName is skipped
                        if (name.Length == 0 || name == JsonClipLoader.ProceduralAnimName) continue;
                        lib._clipFiles[name] = f;
                        if (json) lib._jsonClipFiles[name] = f;
                    }
                }
                catch (Exception) { /* fall back to the filename below */ }
                if (ext.Equals(".bin", StringComparison.OrdinalIgnoreCase))
                    lib._clipFiles.TryAdd(Path.GetFileNameWithoutExtension(f), f);
            }
        }

        if (groups is not null)
            foreach (var f in Directory.EnumerateFiles(groups, "*.json", SearchOption.AllDirectories))
            {
                var g = LoadGroup(f);
                if (g is null) continue;
                g.UseContainer(lib._cooldowns);
                lib._groups[g.Name] = g;
            }

        lib.Triggers = AnimationTriggerResponses.Load(FindDir(assetsRoot, "animationGroupMaps"));
        return lib;
    }

    private static string? FindDir(string root, string name)
    {
        if (string.Equals(Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)), name, StringComparison.OrdinalIgnoreCase))
            return Directory.Exists(root) ? root : null;
        foreach (var candidate in new[] { Path.Combine(root, name), Path.Combine(root, "assets", name) })
            if (Directory.Exists(candidate)) return candidate;
        return null;
    }

    /// <summary>config/engine/animations beside the assets directory (gap1 C5, string 0x0051F7EC).</summary>
    private static string? FindConfigAnimations(string root)
    {
        var full = Path.GetFullPath(root);
        var parent = Directory.GetParent(full.TrimEnd(Path.DirectorySeparatorChar))?.FullName;
        foreach (var baseDir in new[] { full, parent })
        {
            if (baseDir is null) continue;
            var c = Path.Combine(baseDir, "config", "engine", "animations");
            if (Directory.Exists(c)) return c;
        }
        return null;
    }

    private static IEnumerable<string> ClipNamesIn(string file)
    {
        if (file.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            var root = FlatTable.Root(File.ReadAllBytes(file));
            foreach (var c in root.Tables(ClipsField)) yield return c.String(ClipName) ?? "";
        }
        else
        {
            var name = JsonClipName(file, out _);
            if (name is not null) yield return name;
        }
    }

    /// <summary>
    /// gap2 J1, J2: the clip name is the first of the top-level member names in JsonCpp's sorted order; none is an
    /// EmptyFile error; more than one warns TooManyAnims and uses the first.
    /// </summary>
    private static string? JsonClipName(string file, out int count)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var names = doc.RootElement.ValueKind == JsonValueKind.Object
            ? doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList()
            : new List<string>();
        count = names.Count;
        return names.Count == 0 ? null : names[0];
    }

    /// <summary>True when a clip of this name is present.</summary>
    public bool HasClip(string name) { lock (_gate) return _clipFiles.ContainsKey(name); }

    /// <summary>Loads a clip by name, parsing it the first time and caching it after.</summary>
    public AnimationClip GetClip(string name)
    {
        lock (_gate)
        {
            if (_clipCache.TryGetValue(name, out var cached)) return cached;
            if (!_clipFiles.TryGetValue(name, out var path))
                throw new KeyNotFoundException($"no animation called '{name}'. {_clipFiles.Count} clips are loaded.");
            if (!path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
            {
                // gap2 J1..J4, gap4 J1.1..J1.10: a JSON clip, named by its first key
                var jc = JsonClipLoader.Load(path, Log) ?? throw new InvalidDataException($"'{path}' defines no animation");
                _clipCache[name] = jc;
                return jc;
            }
            var all = Parse(File.ReadAllBytes(path), Log);
            foreach (var c in all)
                if (c.Name.Length > 0) _clipCache.TryAdd(c.Name, c);
            var clip = all.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                       ?? all.FirstOrDefault()
                       ?? throw new InvalidDataException($"'{path}' holds no animation clips");
            _clipCache[name] = clip;
            return clip;
        }
    }

    /// <summary>Gets a group by name.</summary>
    public AnimationGroup? GetGroup(string name) { lock (_gate) return _groups.GetValueOrDefault(name); }

    // ------------------------------------------------------------------ IAnimationCatalog

    public bool HasAnimationForTrigger(AnimationTrigger trigger) => Triggers.HasResponse(trigger);

    public string GetAnimationForTrigger(AnimationTrigger trigger) => Triggers.GetResponse(trigger);

    /// <summary>GetAnimationNameFromGroup(group, strict) with the mood, the cooldown time and the head angle (D5, D6).</summary>
    public string GetAnimationNameFromGroup(string group, bool strict)
    {
        var g = GetGroup(group);
        if (g is null)
        {
            Log?.Invoke($"error: AnimationGroupContainer.GetAnimationNameFromGroup: no group '{group}'");
            return "";
        }
        double now = CooldownTimeSec?.Invoke() ?? Environment.TickCount64 / 1000.0;
        var pick = g.GetAnimationName(ContextRandom, CurrentMood?.Invoke() ?? SimpleMood.Default, now, HeadAngleRad?.Invoke(), strict);
        if (pick is null)
        {
            Log?.Invoke($"error: AnimationGroup.GetAnimationName: no animation in group '{group}'");
            return "";
        }
        return pick.Name;
    }

    public (string Name, int Count) GetFirstAnimationName(string group)
    {
        var g = GetGroup(group);
        return g is null || g.Entries.Count == 0 ? ("", 0) : (g.Entries[0].Name, g.Entries.Count);
    }

    public AnimationClip? GetAnimation(string name)
    {
        try { return GetClip(name); }
        catch (KeyNotFoundException) { return null; }
        catch (NotSupportedException e)
        {
            // a JSON clip holding a keyframe type the port cannot load (M5-001 MISSING): the engine's DefineFromJson would
            // have logged the failure at load and left the clip out, so it is logged in that form and treated as absent,
            // and nothing throws into the engine tick
            Log?.Invoke($"error: {name}: {e.Message}");
            return null;
        }
    }

    // ------------------------------------------------------------------ FlatBuffer clips (D1..D3, gap1 C1..C3)

    /// <summary>Every clip in one asset file. A file can hold more than one.</summary>
    public static IReadOnlyList<AnimationClip> ParseFile(string path) => Parse(File.ReadAllBytes(path));

    /// <summary>Every clip in an asset file already in memory.</summary>
    public static IReadOnlyList<AnimationClip> Parse(byte[] bytes) => Parse(bytes, null);

    private static IReadOnlyList<AnimationClip> Parse(byte[] bytes, Action<string>? log)
    {
        var root = FlatTable.Root(bytes);
        var clips = new List<AnimationClip>();
        foreach (var c in root.Tables(ClipsField)) clips.Add(ParseClip(c, log));
        return clips;
    }

    /// <summary>
    /// Animation::DefineFromFlatBuf (D1, gap1 C1, C2; 0x0057571C..0x00575EFC): Clear, then the tracks in the order Lift,
    /// ProcFace, Head, RobotAudio, Backpack (through JSON), FaceAnim, Event, Body, RecordHeading, TurnTo. A keyframe whose
    /// DefineFromFlatBuf fails, or that the track refuses (BadTriggerTime, TooManyFrames), logs "Adding X frame %d failed."
    /// and ends the load there: earlier tracks and keyframes stay, the rest are not loaded, and the clip is kept (C3).
    /// </summary>
    private static AnimationClip ParseClip(FlatTable clip, Action<string>? log)
    {
        var name = clip.String(ClipName) ?? "";
        var probe = new StreamAnimation(name, isLive: false);
        var frames = new List<Keyframe>();
        var kf = clip.Table(ClipKeyframes);
        bool failed = false;

        bool Add<TTrack>(StreamTrack<TTrack> track, TTrack item, Keyframe? k, string what, int index) where TTrack : class, IStreamKeyframe
        {
            if (k is null || !track.AddNewKeyFrameToBack(item))
            {
                log?.Invoke($"error: Animation.DefineFromFlatBuf: {name}: Adding {what} frame {index} failed.");
                failed = true;
                return false;
            }
            frames.Add(k);
            return true;
        }

        void Each(int field, string what, Func<FlatTable, Keyframe?> define, Func<Keyframe, bool> add)
        {
            if (failed || kf.IsEmpty) return;
            int i = 0;
            foreach (var t in kf.Tables(field))
            {
                var k = define(t);
                if (k is null)
                {
                    log?.Invoke($"error: Animation.DefineFromFlatBuf: {name}: Adding {what} frame {i} failed.");
                    failed = true;
                    return;
                }
                if (!add(k)) return;
                i++;
            }
        }

        int n = 0;
        Each(KfLift, "LiftHeight", DefineLift, k => Add(probe.Lift, new StreamKeyframe(k), k, "LiftHeight", n++));
        n = 0;
        Each(KfFace, "ProceduralFace", DefineFace, k => Add(probe.ProcFace, new FaceFrame(k.TriggerTimeMs, ((FaceKeyframe)k).Pose), k, "ProceduralFace", n++));
        n = 0;
        Each(KfHead, "HeadAngle", DefineHead, k => Add(probe.Head, new StreamKeyframe(k), k, "HeadAngle", n++));
        n = 0;
        Each(KfAudio, "RobotAudio", t => DefineAudio(t, log, name), k => Add(probe.RobotAudio, new StreamKeyframe(k), k, "RobotAudio", n++));
        n = 0;
        Each(KfLights, "BackpackLights", DefineLights, k => Add(probe.Backpack, new StreamKeyframe(k), k, "BackpackLights", n++));
        n = 0;
        Each(KfFaceAnim, "FaceAnimation", t => new FaceAnimationKeyframe(t.U32(0), t.String(1) ?? ""),
             k => Add(probe.FaceAnim, new StreamKeyframe(k), k, "FaceAnimation", n++));
        n = 0;
        Each(KfEvent, "Event", t => DefineEvent(t, log, name), k => Add(probe.Event, new StreamKeyframe(k), k, "Event", n++));
        n = 0;
        Each(KfBody, "BodyMotion", t => DefineBody(t, log, name), k => Add(probe.Body, new StreamKeyframe(k), k, "BodyMotion", n++));
        n = 0;
        Each(KfRecordHeading, "RecordHeading", t => new RecordHeadingKeyframe(t.U32(0)),
             k => Add(probe.RecHeading, new StreamKeyframe(k), k, "RecordHeading", n++));
        n = 0;
        Each(KfTurnToHeading, "TurnToRecordedHeading", DefineTurnTo, k => Add(probe.TurnTo, new StreamKeyframe(k), k, "TurnToRecordedHeading", n++));

        var sorted = frames.OrderBy(f => f.TriggerTimeMs).ToList();       // stable: each track keeps its order
        AnimationTrack tracks = 0;
        uint end = 0;
        foreach (var f in sorted) { tracks |= f.Track; end = Math.Max(end, f.EndTimeMs); }
        return new AnimationClip { Name = name, Keyframes = sorted, Tracks = tracks, DurationMs = end, LoadTruncated = failed };
    }

    /// <summary>A u32 duration field read as the engine's int: a negative value becomes INT_MAX (C4, C19).</summary>
    private static uint Duration(FlatTable t, int field)
    {
        int d = unchecked((int)t.U32(field));
        return d < 0 ? int.MaxValue : (uint)d;
    }

    /// <summary>C3: trigger f0, duration f1, height f2 (u8), variability f3 (default 0).</summary>
    private static Keyframe DefineLift(FlatTable t) => new LiftKeyframe(t.U32(0), t.U32(1), t.U8(2), t.U8(3));

    /// <summary>C2: trigger f0, duration f1 (u32), angle f2 (s8), variability f3 (default 0).</summary>
    private static Keyframe DefineHead(FlatTable t) => new HeadKeyframe(t.U32(0), t.U32(1), t.I8(2), t.U8(3));

    /// <summary>
    /// C6 ProceduralFace::SetFromFlatBuf onto a default face: leftEye f6 and rightEye f7 of 19 floats each through Clip
    /// (a wrong-size eye is left unchanged, with a warning); faceAngle f1 (0); scaleX/Y f4/f5 (1), a negative one 0;
    /// the centre f2/f3 through SetFacePosition, before the scale is set (C2).
    /// </summary>
    private static Keyframe DefineFace(FlatTable t)
    {
        var pose = new ProceduralFacePose();
        var left = t.Floats(6);
        var right = t.Floats(7);
        if (left.Length == Eye.ParamCount) pose.Left = Eye.FromAsset(left);
        if (right.Length == Eye.ParamCount) pose.Right = Eye.FromAsset(right);
        pose.FaceAngle = t.F32(1);
        pose.SetFacePosition(t.F32(2), t.F32(3));          // C2: before the scale (0x00583A08 before 0x00583A0C..)
        pose.FaceScaleX = MathF.Max(0f, t.F32(4, 1f));
        pose.FaceScaleY = MathF.Max(0f, t.F32(5, 1f));
        return new FaceKeyframe(t.U32(0), pose);
    }

    /// <summary>
    /// C13 RobotAudioKeyFrame::SetMembersFromFlatBuf: audioEventId f1 (each the low 32 bits), volume f2 (1.0),
    /// probability f3, hasAlts f4 (true). No probability vector gives 1/N each; a count mismatch or a running sum above 1
    /// is an error and the keyframe is rejected.
    /// </summary>
    private static Keyframe? DefineAudio(FlatTable t, Action<string>? log, string clip)
    {
        var ids = t.Longs(1).Select(v => (long)(uint)(v & 0xFFFFFFFF)).ToArray();
        var probs = t.Floats(3);
        if (probs.Length == 0 && ids.Length > 0)
        {
            probs = new float[ids.Length];
            Array.Fill(probs, 1f / ids.Length);
        }
        else if (probs.Length != ids.Length)
        {
            log?.Invoke($"error: RobotAudioKeyFrame.SetMembersFromFlatBuf: {clip}: {ids.Length} events, {probs.Length} probabilities");
            return null;
        }
        float sum = 0f;
        foreach (var p in probs)
        {
            sum += p;
            if (sum > 1.0f)
            {
                log?.Invoke($"error: RobotAudioKeyFrame.SetMembersFromFlatBuf: {clip}: the probabilities sum above 1");
                return null;
            }
        }
        return new AudioKeyframe(t.U32(0), ids, t.F32(2, 1f), probs, t.Bool(4, true));
    }

    /// <summary>
    /// C16: trigger f0, duration f1, Left f2, Right f3, Front f4, Middle f5, Back f6, converted to JSON and read by
    /// SetMembersFromJson (gap4 J1.9): a colour that is not 3 or 4 floats rejects the keyframe.
    /// </summary>
    private static Keyframe? DefineLights(FlatTable t)
    {
        var k = new LightsKeyframe(t.U32(0), t.U32(1), t.Floats(2), t.Floats(3), t.Floats(4), t.Floats(5), t.Floats(6));
        return k.EncodedLeds is null ? null : k;
    }

    /// <summary>C15: event_id f1 through AnimEventFromString; "Count" (unrecognised) warns and rejects the keyframe.</summary>
    private static Keyframe? DefineEvent(FlatTable t, Action<string>? log, string clip)
    {
        var e = new EventKeyframe(t.U32(0), t.String(1) ?? "");
        if (e.Parsed is null)
        {
            log?.Invoke($"warning: EventKeyFrame.SetMembersFromFlatBuf: {clip}: unrecognised event '{e.EventId}'");
            return null;
        }
        return e;
    }

    /// <summary>
    /// C4 / gap1 S1 BodyMotionKeyFrame::SetMembersFromFlatBuf: duration f1 (negative → INT_MAX, never stops); speed f3;
    /// the radius string f2: with any digit atoi clamped to int16 and CheckTurnSpeed; TURN_IN_PLACE or POINT_TURN → 0 and
    /// CheckRotationSpeed; STRAIGHT → 0x7FFF and CheckStraightSpeed; anything else an error that rejects the keyframe.
    /// CheckRotationSpeed: |v| &gt; 300 → ±300; CheckStraightSpeed and CheckTurnSpeed: |v| ≥ 221 → ±220.
    /// </summary>
    private static Keyframe? DefineBody(FlatTable t, Action<string>? log, string clip)
    {
        var raw = t.String(2) ?? "";
        short speed = t.I16(3);
        if (!BodyKeyframe.CheckSpeedForRadiusString(raw, ref speed))
        {
            log?.Invoke($"error: BodyMotionKeyFrame.ProcessRadiusString: {clip}: unrecognised radius '{raw}'");
            return null;
        }
        return new BodyKeyframe(t.U32(0), Duration(t, 1), raw, speed);
    }

    /// <summary>
    /// C19 / gap1 S2: duration f1 (negative → INT_MAX), offset f2 (0), speed f3, accel f4 (1000), decel f5 (1000),
    /// tolerance f6 (2), numHalfRevs f7 (0), useShortestDir f8 (false); CheckRotationSpeed: speed |v| &gt; 300 → ±300,
    /// accel and decel |v| ≥ 13637 → ±13636.
    /// </summary>
    private static Keyframe DefineTurnTo(FlatTable t)
    {
        short speed = t.I16(3), accel = t.I16(4, 1000), decel = t.I16(5, 1000);
        if (Math.Abs((int)speed) > 300) speed = (short)Math.Clamp((int)speed, -300, 300);
        if (Math.Abs((int)accel) >= 13637) accel = (short)Math.Clamp((int)accel, -13636, 13636);
        if (Math.Abs((int)decel) >= 13637) decel = (short)Math.Clamp((int)decel, -13636, 13636);
        return new TurnToRecordedHeadingKeyframe(t.U32(0), Duration(t, 1), t.I16(2), speed, accel, decel,
                                                 t.U16(6, 2), t.U16(7), t.Bool(8));
    }

    // ------------------------------------------------------------------ groups (D4)

    private static AnimationGroup? LoadGroup(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("Animations", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return null;
            var entries = new List<AnimationGroupEntry>();
            foreach (var e in arr.EnumerateArray())
            {
                string name = e.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                if (name.Length == 0) continue;
                entries.Add(new AnimationGroupEntry(
                    name,
                    e.TryGetProperty("Weight", out var w) ? (float)w.GetDouble() : 1f,
                    e.TryGetProperty("CooldownTime_Sec", out var c) ? (float)c.GetDouble() : 0f,
                    e.TryGetProperty("Mood", out var m) ? m.GetString() ?? "Default" : "Default")
                {
                    UseHeadAngle = e.TryGetProperty("UseHeadAngle", out var uh) && uh.GetBoolean(),
                    HeadAngleMinDeg = e.TryGetProperty("HeadAngleMin_Deg", out var hmin) ? (float)hmin.GetDouble() : float.NegativeInfinity,
                    HeadAngleMaxDeg = e.TryGetProperty("HeadAngleMax_Deg", out var hmax) ? (float)hmax.GetDouble() : float.PositiveInfinity,
                });
            }
            return new AnimationGroup { Name = Path.GetFileNameWithoutExtension(path), Entries = entries };
        }
        catch (JsonException)
        {
            return null;       // a file that is not a group definition is simply not one
        }
    }
}
