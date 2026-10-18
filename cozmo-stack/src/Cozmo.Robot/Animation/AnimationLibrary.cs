using System.Text.Json;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation;

/// <summary>SimpleMoodType (Unity SimpleMoodType.cs): the mood an animation group entry is for.</summary>
public enum SimpleMood : byte { Happy, Sad, Default, Count }

// fidelity: M5-011, M5-014
/// <summary>
/// One choice inside an animation group (D4, 0x0058C4C0..0x0058C7BE; C5 item 4): Name, Weight, Mood (SimpleMoodType),
/// CooldownTime_Sec (default 0), UseHeadAngle and HeadAngleMin/Max_Deg (converted to radians for the gate). There are no
/// defaults for Weight, Mood or HeadAngle: the loader rejects an entry that omits or mis-types any required one. An entry
/// without UseHeadAngle inherits the preceding window in its file; before the first window, M5-047 excludes it from backup.
/// </summary>
public sealed record AnimationGroupEntry(string Name, float Weight, double CooldownSec, string Mood)
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

    public float HeadAngleMinRad => HeadAngleMinDeg * BitConverter.Int32BitsToSingle(0x3C8EFA35);
    public float HeadAngleMaxRad => HeadAngleMaxDeg * BitConverter.Int32BitsToSingle(0x3C8EFA35);
    internal bool BackupWindowKnown { get; init; }
    internal float BackupMinRad { get; init; }
    internal float BackupMaxRad { get; init; }

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
    public float TimeUntilCooldownOver(string name, double now) { lock (_gate) return _ends.TryGetValue(name, out var end) ? (float)(end - now) : 0f; }
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
        set
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

    /// <summary>Checked G1–G9 group draw and B1–B6 backup, with f32 time, head and weights.</summary>
    public AnimationGroupEntry? Choose(Random random, string? mood = null, double? nowSec = null, double? headAngleDeg = null,
                                       bool strict = false)
    {
        var requested = mood is null ? SimpleMood.Default
            : Enum.TryParse<SimpleMood>(mood, ignoreCase: true, out var m) ? m : SimpleMood.Count;
        double? headRad = headAngleDeg is { } d ? d * Math.PI / 180.0 : null;
        return GetAnimationName(new EngineRandom(random), requested, nowSec, headRad, strict);
    }

    internal Action<string>? Log { get; set; }
    public SourceLogSink? SourceLog { get; set; }
    internal AnimationGroupEntry? GetAnimationName(EngineRandom rng, SimpleMood mood, double? nowSec, double? headRad, bool strict)
    {
        float time = (float)(nowSec ?? 0), head = (float)(headRad ?? 0);
        while (true)
        {
            SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Debug, "Unnamed", "AnimationGroup.GetAnimation", $"getting animation from group '{Name}', simple mood = '{mood}'");
            bool anyMood = false;
            float sum = 0;
            var candidates = new List<AnimationGroupEntry>();
            foreach (var e in _entries)
            {
                if (e.MoodType != mood) continue;
                anyMood = true;
                if (e.UseHeadAngle && (!(e.HeadAngleMinRad <= head) || e.HeadAngleMaxRad < head)) continue;
                if (e.IsOnCooldown((double)time)) continue;
                candidates.Add(e);
                sum += e.Weight;
            }
            // G4: even an empty pass consumes two MT outputs (0x0058AA72..0x0058AA86).
            float r = (float)rng.RandDbl((double)sum);
            if (candidates.Count != 0)
            {
                var pick = candidates[^1];
                foreach (var e in candidates) { r -= e.Weight; if (r < 0) { pick = e; break; } }
                pick.Container.SetCooldown(pick.Name, pick.CooldownSec + (double)time);
                SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Info, "Animations", "AnimationGroup.GetAnimation.Found", $"Group '{Name}' returning animation name '{pick.Name}'");
                return pick;
            }
            if (mood != SimpleMood.Default)
            {
                SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Info, "Animations", "AnimationGroup.GetAnimation.NoMoodMatch", $"No animations from group '{Name}' selected matching mood '{mood}', trying with default mood");
                mood = SimpleMood.Default;
                continue;
            }
            if (anyMood && !strict)
            {
                SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Info, "Animations", "AnimationGroup.GetAnimation.AllOnCooldown", "All animations are on cooldown. Selecting the one closest to being finished");
                float best = float.MaxValue;
                AnimationGroupEntry? backup = null;
                foreach (var e in _entries)
                {
                    if (e.MoodType != SimpleMood.Default) continue;
                    float left = e.Container.TimeUntilCooldownOver(e.Name, (double)time);
                    if (!(left < best)) continue;
                    // fidelity: M5-047
                    // Approved 64-bit-phone policy: pre-first-window entries fail B2; B4 still returns entries[0].
                    if (!e.UseHeadAngle && !e.BackupWindowKnown) continue;
                    float min = e.UseHeadAngle ? e.HeadAngleMinRad : e.BackupMinRad;
                    float max = e.UseHeadAngle ? e.HeadAngleMaxRad : e.BackupMaxRad;
                    if (!(min + BitConverter.Int32BitsToSingle(unchecked((int)0xBD4CCCCD)) <= head) || max + BitConverter.Int32BitsToSingle(0x3D4CCCCD) < head) continue;
                    SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Info, "Animations", "AnimationGroup.GetAnimation.BackupAnimationChosen", FormattableString.Invariant($"Animation is '{e.Name}' and new min cooldown is '{left:F6}'"));
                    backup = e; best = left;
                }
                if (backup != null)
                {
                    SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Info, "Animations", "AnimationGroup.GetAnimation.BackupAnimationFound", $"All animations in group '{Name}' were on cooldown / invalid, so selected '{backup.Name}'");
                    return backup;
                }
                SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Info, "Animations", "AnimationGroup.GetAnimation.NoBackup", $"All animations in group '{Name}' were on cooldown / invalid nothing could be returned so choosing first in list");
                return _entries[0];
            }
            SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Error, null, "AnimationGroup.GetAnimation.NoAnimation", $"Could not find a single animation from group '{Name}' to run. Returning empty");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
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
    public SourceLogSink? SourceLog { get; set; }
    public int Count => _pairs.Count;

    // The engine and its driving facade share the same parsed string-key container.
    internal static AnimationTriggerResponses FromPairs(IEnumerable<KeyValuePair<string,string>> pairs)
    {
        var responses = new AnimationTriggerResponses();
        foreach (var pair in pairs) responses._pairs.TryAdd(pair.Key, pair.Value);
        return responses;
    }

    public static AnimationTriggerResponses Load(string? dir, Action<string>? log = null, SourceLogSink? sourceLog = null) =>
        dir is null || !Directory.Exists(dir) ? new AnimationTriggerResponses() :
            Cozmo.Robot.Behavior.AnimationTriggerMap.LoadDirectory(dir, log, sourceLog).ToResponses();

    /// <summary>GetResponse (0x00670810..0x00670920).</summary>
    public string GetResponse(AnimationTrigger trigger)
    {
        string key = trigger.ToString();
        if (_pairs.TryGetValue(key, out var name))
        {
            SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Info, "Animations", "GetResponseForAnimationTrigger.Found", $"{key} -> {name}");
            return name;
        }
        SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Warning, null, "AnimationTriggerResponsesContainer::GetResponse", $"Animation requested for unknown response '{key}'");
        return "";
    }

    /// <summary>HasResponse (0x00670AD0, not re-read in the inventory): whether the key is present, with no log.</summary>
    public bool HasResponse(AnimationTrigger trigger) => _pairs.ContainsKey(trigger.ToString());
}

// fidelity: M5-001, M5-006, M5-009, M5-011, M5-014, M5-016, M5-033, M5-034
/// <summary>
/// Cozmo's own animation assets: the clips (<c>CannedAnimationContainer</c>), the groups (<c>AnimationGroupContainer</c>)
/// and the trigger map (<see cref="AnimationTriggerResponses"/>). Clips come from assets/animations/ and
/// config/engine/animations/ (gap1 C5): a ".bin" is FlatBuffer, anything else JSON (C4). Every clip is loaded at startup by four contiguous workers.
/// </summary>
public sealed class AnimationLibrary : IAnimationCatalog
{
    private const int ClipsField = 0;
    private const int ClipName = 0, ClipKeyframes = 1;
    private const int KfLift = 0, KfFace = 1, KfHead = 2, KfAudio = 3, KfLights = 4,
                      KfFaceAnim = 5, KfEvent = 6, KfBody = 7, KfRecordHeading = 8, KfTurnToHeading = 9;

    // CannedAnimationContainer::GetAnimation is a case-sensitive map lookup, so the clip name maps are Ordinal.
    private readonly Dictionary<string, string> _clipFiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _jsonClipFiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AnimationClip> _clipCache = new(StringComparer.Ordinal);
    private readonly List<string> _definitionOrder=new(); // M1-055 operator2026-10-17: stack load order.
    private readonly Dictionary<string, AnimationGroup> _groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly GroupContainer _cooldowns = new();
    private readonly object _gate = new();

    /// <summary>
    /// R45-1..R45-3: the shared context generator supplied to AnimationGroupContainer and each group.
    /// Standalone libraries own a generator; production supplies the engine context instance.
    /// </summary>
    // fidelity: M5-045
    public EngineRandom ContextRandom { get; set; }

    public AnimationLibrary() : this(null) { }

    internal AnimationLibrary(EngineRandom? contextRandom) => ContextRandom = contextRandom ?? new EngineRandom();

    /// <summary>The clip names available (FlatBuffer and JSON), whether or not they have been parsed yet.</summary>
    public IReadOnlyCollection<string> ClipNames { get { lock (_gate) return _definitionOrder.ToArray(); } }

    /// <summary>The JSON clips found (gap2 J1..J5: the four in config/engine/animations), also in <see cref="ClipNames"/>.</summary>
    public IReadOnlyCollection<string> JsonClipNames { get { lock (_gate) return _jsonClipFiles.Keys.ToArray(); } }
    /// <summary>Group names available.</summary>
    public IReadOnlyCollection<string> GroupNames { get { lock (_gate) return _groups.Keys.ToArray(); } }

    /// <summary>The trigger map (assets/animationGroupMaps).</summary>
    public AnimationTriggerResponses Triggers { get; private set; } = new();

    /// <summary>The engine log for the load errors (C2) and the group errors, including empty containers during loading.</summary>
    private Action<string>? _log;
    private SourceLogSink? _sourceLog;
    public Action<string>? Log { get => _log; set { _log = value; Triggers.Log = value; } }
    public SourceLogSink? SourceLog { get => _sourceLog; set { _sourceLog = value; Triggers.SourceLog = value; } }

    /// <summary>The robot's simple mood the groups choose by (MoodManager, M7). Unset: Default.</summary>
    public Func<SimpleMood>? CurrentMood { get; set; }
    private int _missingSimpleMoodReported;

    /// <summary>
    /// The time the group cooldowns use: MoodManager+0x130, its last-update seconds (G1; M7). Production supplies BaseStationTimer seconds; unset is zero.
    /// </summary>
    public Func<double>? CooldownTimeSec { get; set; }

    /// <summary>The robot's head angle in radians (robot+0x2FC) for the head-angle gate. Unset: the constructor head angle zero.</summary>
    public Func<double?>? HeadAngleRad { get; set; }

    /// <summary>
    /// Opens a library over an unpacked <c>cozmo_resources</c> tree. Point it at the directory that holds
    /// <c>assets/animations</c>, or directly at either directory.
    /// </summary>
    public static AnimationLibrary Open(string assetsRoot, Action<string>? log = null, SourceLogSink? sourceLog = null, EngineRandom? contextRandom = null)
        => OpenForLoad(assetsRoot, log, sourceLog, contextRandom, () => true, null, null);

    internal static AnimationLibrary OpenForLoad(string assetsRoot, Action<string>? log, SourceLogSink? sourceLog,
        EngineRandom? contextRandom, Func<bool> beginCollect, Action? beforeCollect, Action? afterGroups,
        Action<string>? beforeStat = null, Action<AnimationLibrary>? loaderCreated = null,
        Action<IReadOnlyList<string>>? loadCubeFiles = null, Func<string?>? cubeResourceRoot = null,
        AnimationTriggerResponses? triggerResponses = null, Action<float>? progressAdded = null)
    {
        var lib = new AnimationLibrary(contextRandom) { Log = log, SourceLog = sourceLog, _progressAdded = progressAdded };
        loaderCreated?.Invoke(lib);
        var animations = FindDir(assetsRoot, "animations");
        var groups = FindDir(assetsRoot, "animationGroups");
        if (animations is null && groups is null)
            throw new DirectoryNotFoundException(
                $"no animations or animationGroups directory under {assetsRoot}. Point this at the unpacked " +
                "cozmo_resources assets directory.");

        // R46-1..R46-4: collect all four vectors before Results; production retains the collect history.
        bool freshCollect = beginCollect();
        beforeCollect?.Invoke();
        string? configAnimations=FindConfigDirectory(assetsRoot,"animations");
        var files = CollectFiles(new[] { animations, configAnimations }, SourceLogDispatch.GetSink(sourceLog, log), beforeStat);
        string? cubeRoot=cubeResourceRoot is null?assetsRoot:cubeResourceRoot();
        string? cubeDirectory=cubeResourceRoot is null?FindConfigDirectory(assetsRoot,"lights/cubeLights")
            :cubeRoot is null?null:Path.Combine(cubeRoot,CubeLightAnimations.AnimationDirectory);
        if(cubeDirectory is not null&&!Directory.Exists(cubeDirectory))cubeDirectory=null; // Failed native opendir leaves this vector empty.
        var cubeFiles = CollectFiles(new[] { cubeDirectory }, SourceLogDispatch.GetSink(sourceLog, log), beforeStat);
        // B-M3M4-27 item 5: production counts the same ResourcesPath directory the constructor actually loads.
        string? backpackDirectory=cubeResourceRoot is null?FindConfigDirectory(assetsRoot,"lights/backpackLights")
            :cubeRoot is null?null:Path.Combine(cubeRoot,BackpackLightAnimations.ResourceDirectory);
        if(backpackDirectory is not null&&!Directory.Exists(backpackDirectory))backpackDirectory=null;
        var backpackFiles = CollectFiles(new[] { backpackDirectory }, SourceLogDispatch.GetSink(sourceLog, log), beforeStat);
        var groupFiles = CollectFiles(new[] { groups }, SourceLogDispatch.GetSink(sourceLog, log), beforeStat);
        EmitCollectResults(new[] { files.Count, groupFiles.Count, cubeFiles.Count, backpackFiles.Count },
            freshCollect, log, sourceLog);
        double started = MonotonicMilliseconds();
        Eye.ClippingWarningEnabled = false;
        float ratio = 0.7f / files.Count;
        // Operator M1-055: deterministic stack definition sequence, first definition wins.
        // One resource-relative sequence, independent of a resources-root versus nested-assets-root input.
        // Clips retain their file order; phone worker interleaving is not reproduced.
        string definitionRoot=configAnimations is null?Path.GetFullPath(assetsRoot)
            :Path.GetFullPath(Path.Combine(configAnimations,"..","..",".."));
        files=files.OrderBy(file=>Path.GetRelativePath(definitionRoot,file).Replace('\\','/'),StringComparer.Ordinal).ToList();
        foreach(string file in files)lib.LoadAnimationFile(file,ratio);
        Eye.ClippingWarningEnabled = true; // L10: only after Process returns normally.
        SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Info, "Animations", "RobotDataLoader.LoadAnimationsInternal.LoadTime", FormattableString.Invariant($"Time to load animations = {MonotonicMilliseconds() - started:F2} ms"));
        SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Info, "Animations", "RobotDataLoader.LoadAnimations.CannedAnimationsCount", $"Total number of canned animations available = {lib._clipCache.Count}");

        DispatchFiles(groupFiles, lib.LoadAnimationGroupFile);

        // L0: LoadNonConfigData 0x0051F3BC..0x0051F3CE: groups, cube lights, backpack lights, faces.
        loadCubeFiles?.Invoke(cubeFiles);
        afterGroups?.Invoke();
        progressAdded?.Invoke(BitConverter.Int32BitsToSingle(0x3E4CCCCD)); // M1-056: one atomic coarse addition after faces.
        lib.Triggers = triggerResponses ?? AnimationTriggerResponses.Load(Cozmo.Robot.Behavior.AnimationTriggerMap.ResourceDirectory(assetsRoot), log, sourceLog);
        lib.Triggers.Log = log; lib.Triggers.SourceLog = sourceLog;
        return lib;
    }

    // fidelity: M5-050
    private int _abortLoading;
    internal void AbortLoading() => Volatile.Write(ref _abortLoading, 1);
    internal static void DispatchFiles(IReadOnlyList<string> files, Action<string> load)
    {
        var workers = new List<Thread>();
        int offset = 0;
        for (int part = 0; part < 4; part++)
        {
            int first = offset, end = first + files.Count / 4 + (part < files.Count % 4 ? 1 : 0); offset = end;
            void Run() { for (int i = first; i < end; i++) load(files[i]); }
            if (part == 3) Run();
            else if (first != end) { var thread = new Thread(Run) { IsBackground = true }; workers.Add(thread); thread.Start(); }
        }
        foreach (var thread in workers) thread.Join();
    }
    // fidelity: M5-051
    private JsonDocument? ReadAsJson(string path) => ReadAsJson(path, Log, SourceLog);
    internal static JsonDocument? ReadAsJson(string path, Action<string>? log, SourceLogSink? sourceLog)
    {
        try
        {
            string text = File.ReadAllText(path);
            if (ExceedsJsonValueDepth(text))
            { log?.Invoke($"Loader file skipped (M1-034): JsonCpp stack-limit fatal path in {path}"); return null; }
            return JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 1000 });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            ReportReadAsJsonFailure(path, null, log, sourceLog);
            return null;
        }
    }
    private static bool ExceedsJsonValueDepth(string text)
    {
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(text), new JsonReaderOptions { MaxDepth = int.MaxValue });
        try
        {
            while (reader.Read())
                if (reader.TokenType is not (JsonTokenType.PropertyName or JsonTokenType.EndArray or JsonTokenType.EndObject)
                    && reader.CurrentDepth >= 1000) return true;
        }
        catch (JsonException) { } // A syntax failure still belongs to readAsJson's diagnostics.
        return false;
    }
    internal static void ReportReadAsJsonFailure(string path, string? readerErrors, Action<string>? log, SourceLogSink? sourceLog)
    {
        SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "DataPlatform.readAsJson", $"Failed to read [{path}]");
        Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
        if (readerErrors is null)
            log?.Invoke($"MISSING: M5-051 JsonCpp formatted reader errors for {path}, 0x008E09A4..0x008E2A70; conditional Unnamed debug text at 0x0084C124..0x0084C15C remains unread");
        else if (readerErrors.Length != 0)
            SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Debug, "Unnamed", "DataPlatform.readAsJson", $"Json reader errors [{readerErrors}]");
    }
    private void LoadAnimationGroupFile(string path)
    {
        if (Volatile.Read(ref _abortLoading) != 0) return;
        using var doc = ReadAsJson(path);
        if (doc is null || JsonClipLoader.IsEmpty(doc.RootElement)) return;
        string name = Path.GetFileNameWithoutExtension(path);
        SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Info, "Animations", "RobotDataLoader.LoadAnimationGroupFile.LoadingSpecificAnimGroupFromJson", $"Loading '{name}' from {path}");
        try
        {
            lock (_gate)
            {
                if (!_groups.TryGetValue(name, out var group)) { group = new AnimationGroup { Name = name }; _groups.Add(name, group); }
                if (!DefineGroup(group, doc.RootElement, HasClip, Log, SourceLog))
                {
                    SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Error, null, "AnimationGroupContainer.DefineAnimationGroupFromJson", $"Failed to define animation group '{name}' from Json.");
                    Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                }
                group.UseContainer(_cooldowns);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        { Log?.Invoke($"Loader file skipped (M1-034): fatal group JSON path in {path}; {e.GetType().Name}"); }
    }

    // L10b: CLOCK_MONOTONIC nanoseconds, converted to double milliseconds (0x00833D40..0x00833D6A).
    private static double MonotonicMilliseconds()
    {
        long ticks = System.Diagnostics.Stopwatch.GetTimestamp(), frequency = System.Diagnostics.Stopwatch.Frequency;
        long nanoseconds = ticks / frequency * 1_000_000_000 + ticks % frequency * 1_000_000_000 / frequency;
        return (double)nanoseconds * 1e-6;
    }

    private static string? FindDir(string root, string name)
    {
        if (string.Equals(Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)), name, StringComparison.OrdinalIgnoreCase))
            return Directory.Exists(root) ? root : null;
        foreach (var candidate in new[] { Path.Combine(root, name), Path.Combine(root, "assets", name) })
            if (Directory.Exists(candidate)) return candidate;
        return null;
    }

    /// <summary>R46-1..R46-3: a config/engine directory beside the assets directory.</summary>
    private static string? FindConfigDirectory(string root, string relative)
    {
        var full = Path.GetFullPath(root);
        var parent = Directory.GetParent(full.TrimEnd(Path.DirectorySeparatorChar))?.FullName;
        foreach (var baseDir in new[] { full, parent })
        {
            if (baseDir is null) continue;
            var c = Path.Combine(baseDir, "config", "engine", relative);
            if (Directory.Exists(c)) return c;
        }
        return null;
    }

    // fidelity: M5-046
    // R46-8 / L2: fresh mtime map; case-sensitive json/bin suffixes and successful stat only.
    private static List<string> CollectFiles(IEnumerable<string?> directories, SourceLogSink? sourceLog, Action<string>? beforeStat)
    {
        var files = new List<string>();
        foreach (var dir in directories)
            if (dir is not null)
                foreach (var f in WalkFilesInReaddirOrder(dir))
                    if (f.EndsWith("json", StringComparison.Ordinal) || f.EndsWith("bin", StringComparison.Ordinal))
                    {
                        beforeStat?.Invoke(f); // Test-only filesystem race injection; production uses the actual stat.
                        var stat = new FileInfo(f); stat.Refresh();
                        if (!stat.Exists) sourceLog?.Invoke(EngineLogLevel.Warning, null,
                            "RobotDataLoader.WalkAnimationDir", $"could not get mtime for {f}");
                        else files.Add(f);
                    }
        return files;
    }

    // X9 G2b: recurse at the directory entry, not after emitting all parent files. Host enumeration order is input.
    internal static IEnumerable<string> WalkFilesInReaddirOrder(string path)
    {
        foreach (var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue; // DT_LNK is neither DT_REG nor DT_DIR.
            if (entry is DirectoryInfo) { foreach (string file in WalkFilesInReaddirOrder(entry.FullName)) yield return file; }
            else if (entry is FileInfo) yield return entry.FullName;
        }
    }

    // fidelity: M5-046
    internal static void EmitCollectResults(IReadOnlyList<int> counts, bool fresh, Action<string>? log, SourceLogSink? sourceLog)
    {
        // X9 section 4e: the one production collect is fresh, and the map is cleared afterwards.
        // All 15 populations iterate reverse first insertion of walk order 0,2,3,1.
        foreach (int type in new[] { 1, 3, 2, 0 })
            if (counts[type] > 0)
                SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Info, "Animations", "RobotDataLoader.CollectAnimFiles.Results",
                    FormattableString.Invariant($"Found {counts[type]} animation files of type {type}"));
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

    /// <summary>Exact eager-container lookup (L9), without aliases or fallback.</summary>
    public AnimationClip GetClip(string name)
    {
        lock (_gate) return _clipCache.TryGetValue(name, out var clip) ? clip
            : throw new KeyNotFoundException($"no animation called '{name}'. {_clipCache.Count} clips are loaded.");
    }

    private Action<float>? _progressAdded;
    public float LoadingProgress { get; private set; }
    private void LoadAnimationFile(string path, float ratio)
    {
        if (Volatile.Read(ref _abortLoading) != 0) return;
        using var clipLog = Eye.UseWarningLog(Log, SourceLog);
        SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Info, "Animations", "RobotDataLoader.LoadAnimationFile.LoadingAnimationsFromBinaryOrJson", $"Loading animations from {path}");
        bool binary = path.EndsWith("bin", StringComparison.Ordinal);
        if (binary)
        {
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception) { bytes = Array.Empty<byte>(); } // ReadFileAsBinary open/read failure returns an empty vector.
            if (bytes.Length == 0) { BinaryError("BinaryDataEmpty", $"Found no data in {path}"); return; }
            try
            {
                var root = FlatTable.Root(bytes);
                var tables = root.Tables(ClipsField).ToList();
                if (!root.Has(ClipsField)) { BinaryError("AllClipsNull", $"Found no animations in {path}"); return; }
                if (tables.Count == 0) { BinaryError("AnimClipsEmpty", $"Found no animations in {path}"); return; }
                lock (_gate)
                {
                    int priorOrderCount=_definitionOrder.Count;
                    var prior = new Dictionary<string, (AnimationClip? Clip, string? File, string? JsonFile)>(StringComparer.Ordinal);
                    try
                    {
                        foreach (var table in tables)
                        {
                            string name = table.String(ClipName) ?? "";
                            SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Info, "Animations", "RobotDataLoader.LoadAnimationFile.LoadingSpecificAnimFromBinary", $"Loading '{name}' from {path}");
                            var clip = ParseClip(table, Log, SourceLog);
                            prior.TryAdd(clip.Name, (_clipCache.GetValueOrDefault(clip.Name), _clipFiles.GetValueOrDefault(clip.Name), _jsonClipFiles.GetValueOrDefault(clip.Name)));
                            Register(clip, path, false);
                        }
                    }
                    catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or OverflowException or InvalidDataException)
                    {
                        // M1-034 isolates the whole malformed file, including earlier tables. Holding the gate keeps
                        // another loader's registration from being overwritten by this rollback.
                        _definitionOrder.RemoveRange(priorOrderCount,_definitionOrder.Count-priorOrderCount);
                        foreach (var (name, old) in prior)
                        {
                            if (old.Clip is null) _clipCache.Remove(name); else _clipCache[name] = old.Clip;
                            if (old.File is null) _clipFiles.Remove(name); else _clipFiles[name] = old.File;
                            if (old.JsonFile is null) _jsonClipFiles.Remove(name); else _jsonClipFiles[name] = old.JsonFile;
                        }
                        throw;
                    }
                }
            }
            catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or OverflowException or InvalidDataException)
            {
                Log?.Invoke($"Loader file skipped (M1-034): malformed non-empty binary {path}; {e.GetType().Name}");
                return;
            }
        }
        else
        {
            using var doc = ReadAsJson(path);
            if (doc is not null && !JsonClipLoader.IsEmpty(doc.RootElement))
                try { lock (_gate) { var clip = JsonClipLoader.LoadDocument(doc, Log, SourceLog); if (clip != null) Register(clip, path, true); } }
                catch (Exception e) when (e is not OutOfMemoryException)
                { Log?.Invoke($"Loader file skipped (M1-034): JsonCpp fatal conversion in {path}; {e.GetType().Name}"); }
            // L6.5/L6.6 / 0x00521470..0x005214A8: failed reads and empty values still add ratio.
        }
        lock (_gate) LoadingProgress += ratio;
        _progressAdded?.Invoke(ratio); // Failed JSON still reaches this tail; failed binary returns above.
    }
    private void BinaryError(string key, string text)
    {
        SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Error, null, $"RobotDataLoader.LoadAnimationFile.{key}", $"{text}");
        Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
    }
    private void Register(AnimationClip clip, string path, bool json)
    {
        if (clip.Name == JsonClipLoader.ProceduralAnimName)
        {
            SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Error, null, "CannedAnimationContainer.DefineFromJson.ReservedName", $"Skipping animation with reserved name '{clip.Name}'.");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            return;
        }
        if(_clipCache.ContainsKey(clip.Name))return; // AddAnimation587E56 ->58802A: existing definition wins.
        _clipCache.Add(clip.Name,clip);_clipFiles.Add(clip.Name,path);_definitionOrder.Add(clip.Name);
        if (json) _jsonClipFiles[clip.Name] = path;
        if (clip.LoadTruncated)
        {
            SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Error, null, "CannedAnimationContainer.DefineFromJson", $"Failed to define animation '{clip.Name}' from Json.");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
        }
    }

    /// <summary>Gets a group by name.</summary>
    public AnimationGroup? GetGroup(string name) { lock (_gate) return _groups.GetValueOrDefault(name); }

    // fidelity: M5-065 — 0x0058B6F8..0x0058B768. Extra host presence probes retain silent GetGroup.
    internal AnimationGroup? GetGroupForSource(string name)
    {
        var group = GetGroup(name);
        if (group is not null) return group;
        SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Error, null,
            "AnimationGroupContainer.GetAnimationGroup_Const.InvalidName", $"AnimationGroup requested for unknown animation group '{name}'.");
        Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
        return null;
    }

    // ------------------------------------------------------------------ IAnimationCatalog

    public bool HasAnimationForTrigger(AnimationTrigger trigger) => Triggers.HasResponse(trigger);

    public string GetAnimationForTrigger(AnimationTrigger trigger) => Triggers.GetResponse(trigger);

    /// <summary>GetAnimationNameFromGroup(group, strict) with the mood, the cooldown time and the head angle (D5, D6).</summary>
    public string GetAnimationNameFromGroup(string group, bool strict)
    {
        var g = GetGroupForSource(group);
        if (g is null || g.Entries.Count == 0) return ""; // C3: only a miss logs; an existing empty group is silent.
        g.Log = Log; g.SourceLog = SourceLog;
        if (CurrentMood is null && Interlocked.Exchange(ref _missingSimpleMoodReported, 1) == 0)
            Log?.Invoke("MISSING: M5-011 SimpleMood (M7); Default retained because GetSimpleMood is not represented");
        return g.GetAnimationName(ContextRandom, CurrentMood?.Invoke() ?? SimpleMood.Default,
            (float)(CooldownTimeSec?.Invoke() ?? 0), HeadAngleRad?.Invoke(), strict)?.Name ?? "";
    }

    public (string Name, int Count) GetFirstAnimationName(string group)
    {
        var g = GetGroupForSource(group);
        return g is null || g.Entries.Count == 0 ? ("", 0) : (g.Entries[0].Name, g.Entries.Count);
    }

    // fidelity: M5-054
    public AnimationClip? GetAnimation(string name)
    {
        try { return GetClip(name); }
        catch (KeyNotFoundException)
        {
            SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Error, null,
                "CannedAnimationContainer.GetAnimation_Const.InvalidName", $"Animation requested for unknown animation '{name}'.");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            return null;
        }
    }

    // ------------------------------------------------------------------ FlatBuffer clips (D1..D3, gap1 C1..C3)

    /// <summary>Every clip in one asset file. A file can hold more than one.</summary>
    public static IReadOnlyList<AnimationClip> ParseFile(string path) => Parse(File.ReadAllBytes(path));

    /// <summary>Every clip in an asset file already in memory.</summary>
    public static IReadOnlyList<AnimationClip> Parse(byte[] bytes) => Parse(bytes, null);

    private static IReadOnlyList<AnimationClip> Parse(byte[] bytes, Action<string>? log, SourceLogSink? sourceLog = null)
    {
        var root = FlatTable.Root(bytes);
        var clips = new List<AnimationClip>();
        foreach (var c in root.Tables(ClipsField)) clips.Add(ParseClip(c, log, sourceLog));
        return clips;
    }

    /// <summary>
    /// Animation::DefineFromFlatBuf (D1, gap1 C1, C2; 0x0057571C..0x00575EFC): Clear, then the tracks in the order Lift,
    /// ProcFace, Head, RobotAudio, Backpack (through JSON), FaceAnim, Event, Body, RecordHeading, TurnTo. A keyframe whose
    /// DefineFromFlatBuf fails, or that the track refuses (BadTriggerTime, TooManyFrames), logs "Adding X frame %d failed."
    /// and ends the load there: earlier tracks and keyframes stay, the rest are not loaded, and the clip is kept (C3).
    /// </summary>
    private static AnimationClip ParseClip(FlatTable clip, Action<string>? log, SourceLogSink? sourceLog = null)
    {
        var name = clip.String(ClipName) ?? "";
        var probe = new StreamAnimation(name, isLive: false) { Log = log, SourceLog = sourceLog };
        var frames = new List<Keyframe>();
        var kf = clip.Table(ClipKeyframes);
        bool failed = false;

        bool Add<TTrack>(StreamTrack<TTrack> track, TTrack item, Keyframe? k, string what, int index) where TTrack : class, IStreamKeyframe
        {
            if (k is null || !track.AddNewKeyFrameToBack(item))
            {
                SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "Animation.DefineFromFlatBuf.AddKeyFrameFailure", $"Adding {what} frame {index} failed.");
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
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
                    SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "Animation.DefineFromFlatBuf.AddKeyFrameFailure", $"Adding {what} frame {i} failed.");
                    Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
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
        Each(KfFace, "ProceduralFace", t => DefineFace(t, log, sourceLog), k => Add(probe.ProcFace, new FaceFrame(k.TriggerTimeMs, ((FaceKeyframe)k).Pose), k, "ProceduralFace", n++));
        n = 0;
        Each(KfHead, "HeadAngle", DefineHead, k => Add(probe.Head, new StreamKeyframe(k), k, "HeadAngle", n++));
        n = 0;
        Each(KfAudio, "RobotAudio", t => DefineAudio(t, log, name, sourceLog), k => Add(probe.RobotAudio, new StreamKeyframe(k), k, "RobotAudio", n++));
        n = 0;
        Each(KfLights, "BackpackLights", t => DefineLights(t, log, sourceLog), k => Add(probe.Backpack, new StreamKeyframe(k), k, "BackpackLights", n++));
        n = 0;
        Each(KfFaceAnim, "FaceAnimation", t => new FaceAnimationKeyframe(t.U32(0), t.String(1) ?? ""),
             k => Add(probe.FaceAnim, new StreamKeyframe(k), k, "FaceAnimation", n++));
        n = 0;
        Each(KfEvent, "Event", t => DefineEvent(t, log, name, sourceLog), k => Add(probe.Event, new StreamKeyframe(k), k, "Event", n++));
        n = 0;
        Each(KfBody, "BodyMotion", t => DefineBody(t, log, name, sourceLog), k => Add(probe.Body, new StreamKeyframe(k), k, "BodyMotion", n++));
        n = 0;
        Each(KfRecordHeading, "RecordHeading", t => new RecordHeadingKeyframe(t.U32(0)),
             k => Add(probe.RecHeading, new StreamKeyframe(k), k, "RecordHeading", n++));
        n = 0;
        Each(KfTurnToHeading, "TurnToRecordedHeading", t => DefineTurnTo(t, log, name, sourceLog), k => Add(probe.TurnTo, new StreamKeyframe(k), k, "TurnToRecordedHeading", n++));

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
    private static Keyframe DefineFace(FlatTable t, Action<string>? log, SourceLogSink? sourceLog = null)
    {
        var pose = new ProceduralFacePose();
        var left = t.Floats(6);
        var right = t.Floats(7);
        if (left.Length == Eye.ParamCount) pose.Left = Eye.FromAsset(left);
        else SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Warning, null, "ProceduralFace.SetEyeArrayHelper.WrongNumParams", $"Unexpected number of parameters for leftEye array ({left.Length} vs. 19)");
        if (right.Length == Eye.ParamCount) pose.Right = Eye.FromAsset(right);
        else SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Warning, null, "ProceduralFace.SetEyeArrayHelper.WrongNumParams", $"Unexpected number of parameters for rightEye array ({right.Length} vs. 19)");
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
    private static Keyframe? DefineAudio(FlatTable t, Action<string>? log, string clip, SourceLogSink? sourceLog = null)
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
            SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "RobotAudioKeyFrame.SetMembersFromFlatBuf.UnknownProbabilities", $"{clip}: The number of audio event IDs ({ids.Length}) does not match number of probabilities ({probs.Length})");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            return null;
        }
        float sum = 0f;
        foreach (var p in probs)
        {
            sum += p;
            if (sum > 1.0f)
            {
                SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "RobotAudioKeyFrame.SetMembersFromFlatBuf.TotalProbabilitiesTooHigh", $"{clip}: The total probability of all audio events combined exceeds 1.0");
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                return null;
            }
        }
        return new AudioKeyframe(t.U32(0), ids, t.F32(2, 1f), probs, t.Bool(4, true));
    }

    /// <summary>
    /// C16: trigger f0, duration f1, Left f2, Right f3, Front f4, Middle f5, Back f6, converted to JSON and read by
    /// SetMembersFromJson (gap4 J1.9): a colour that is not 3 or 4 floats rejects the keyframe.
    /// </summary>
    private static Keyframe? DefineLights(FlatTable t, Action<string>? log, SourceLogSink? sourceLog = null)
    {
        var k = new LightsKeyframe(t.U32(0), t.U32(1), t.Floats(2), t.Floats(3), t.Floats(4), t.Floats(5), t.Floats(6));
        return BackpackColor.TryReadAll(k.Left, k.Right, k.Front, k.Middle, k.Back, out _, log, sourceLog) ? k : null;
    }

    /// <summary>C15: event_id f1 through AnimEventFromString; "Count" (unrecognised) warns and rejects the keyframe.</summary>
    private static Keyframe? DefineEvent(FlatTable t, Action<string>? log, string clip, SourceLogSink? sourceLog = null)
    {
        var e = new EventKeyframe(t.U32(0), t.String(1) ?? "");
        if (e.Parsed is null)
        {
            SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Warning, null, "EventKeyFrame.UnrecognizedEventName", $"{e.EventId}");
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
    private static Keyframe? DefineBody(FlatTable t, Action<string>? log, string clip, SourceLogSink? sourceLog = null)
    {
        var raw = t.String(2) ?? "";
        short speed = t.I16(3);
        short originalSpeed = speed;
        if (!BodyKeyframe.CheckSpeedForRadiusString(raw, ref speed))
        {
            SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "BodyMotionKeyFrame.BadRadiusString", $"{clip}: Unrecognized string for 'radius_mm' field: {raw}");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            return null;
        }
        LogBodyClamp(raw, originalSpeed, clip, log, sourceLog);
        return new BodyKeyframe(t.U32(0), Duration(t, 1), raw, speed);
    }

    /// <summary>
    /// C19 / gap1 S2: duration f1 (negative → INT_MAX), offset f2 (0), speed f3, accel f4 (1000), decel f5 (1000),
    /// tolerance f6 (2), numHalfRevs f7 (0), useShortestDir f8 (false); CheckRotationSpeed: speed |v| &gt; 300 → ±300,
    /// accel and decel |v| ≥ 13637 → ±13636.
    /// </summary>
    private static Keyframe DefineTurnTo(FlatTable t, Action<string>? log, string name, SourceLogSink? sourceLog = null)
    {
        short speed = t.I16(3), accel = t.I16(4, 1000), decel = t.I16(5, 1000);
        LogTurnClamps(speed, accel, decel, name, log, sourceLog);
        if (Math.Abs((int)speed) > 300) speed = (short)Math.Clamp((int)speed, -300, 300);
        if (Math.Abs((int)accel) >= 13637) accel = (short)Math.Clamp((int)accel, -13636, 13636);
        if (Math.Abs((int)decel) >= 13637) decel = (short)Math.Clamp((int)decel, -13636, 13636);
        return new TurnToRecordedHeadingKeyframe(t.U32(0), Duration(t, 1), t.I16(2), speed, accel, decel,
                                                 t.U16(6, 2), t.U16(7), t.Bool(8));
    }

    internal static void LogBodyClamp(string radius, short speed, string name, Action<string>? log, SourceLogSink? sourceLog = null)
    {
        bool point = !BodyKeyframe.HasDigits(radius) && (radius == "TURN_IN_PLACE" || radius == "POINT_TURN");
        int abs = Math.Abs((int)speed);
        if (point ? abs > 300 : abs >= 221)
        {
            string kind = point ? "CheckRotationSpeed.PointTurnSpeedExceedsLimit" : radius == "STRAIGHT" ? "CheckStraightSpeed.StraightSpeedExceedsLimit" : "CheckTurnSpeed.ArcSpeedExceedsLimit";
            string text = point ? $"PointTurn speed {abs} deg/s exceeds limit of 300.000000 deg/s. Clamping" : $"Speed {abs} mm/s exceeds limit of 220.000000 mm/s. Clamping";
            SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Info, "Animations", $"BodyMotionKeyFrame.{kind}", $"{name}: {text}");
        }
    }
    internal static void LogTurnClamps(short speed, short accel, short decel, string name, Action<string>? log, SourceLogSink? sourceLog = null)
    {
        if (Math.Abs((int)speed) > 300) SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Info, "Animations", "TurnToRecordedHeadingKeyFrame.CheckRotationSpeed.PointTurnSpeedExceedsLimit", $"{name}: PointTurn speed {Math.Abs((int)speed)} deg/s exceeds limit of 300.000000 deg/s. Clamping");
        if (Math.Abs((int)accel) >= 13637) SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Info, "Animations", "TurnToRecordedHeadingKeyFrame.CheckRotationAccel.PointTurnAccelExceedsLimit", $"{name}: PointTurn accel {Math.Abs((int)accel)} deg/s^2 exceeds limit of 13636.363281 deg/s^2. Clamping");
        if (Math.Abs((int)decel) >= 13637) SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Info, "Animations", "TurnToRecordedHeadingKeyFrame.CheckRotationAccel.PointTurnDecelExceedsLimit", $"{name}: PointTurn decel {Math.Abs((int)decel)} deg/s^2 exceeds limit of 13636.363281 deg/s^2. Clamping");
    }

    // ------------------------------------------------------------------ groups (D4)

    // fidelity: M5-011, M5-014
    /// <summary>
    /// <c>AnimationGroup::DefineFromJson</c> (C5 item 4.2..4.10): reads "Animations", then each entry. A missing or
    /// non-array "Animations", a JSON parse failure, or any rejected entry leaves the group in the container with the
    /// entries that did define (the caller added it by name first, C5 item 4.1).
    /// </summary>
    private static bool DefineGroup(AnimationGroup g, JsonElement root, Func<string, bool> hasClip, Action<string>? log, SourceLogSink? sourceLog = null)
    {
        if (!root.TryGetProperty("Animations", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            // 4.2: not an array logs NoAnimations and fails the define
            SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "AnimationGroup.DefineFromJson.NoAnimations", "Missing 'Animations' field for animation group.");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            return false;
        }
        var entries = new List<AnimationGroupEntry>();
        int entryIndex = -1;
        bool rejected = false;
        void Reject(bool sharedTail = true)
        {
            rejected = true;
            if (sharedTail) Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak(); // 0x0058C6E8..0x0058C6F4
            SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null,
                "AnimationGroup.DefineFromJson.AddEntryFailure", $"Adding animation {entryIndex} failed.");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak(); // 0x0058A6D2..0x0058A6D8
        }
        bool windowKnown = false;
        float inheritedMin = 0, inheritedMax = 0;
        foreach (var e in arr.EnumerateArray())
        {
            entryIndex++;
            // 4.4: Name must be a string, 4.5: and must name a loaded clip; otherwise the entry is rejected and the
            // group continues with the rest (4.3).
            if (!e.TryGetProperty("Name", out var n) || n.ValueKind != JsonValueKind.String)
            {
                SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "AnimationGroupEntry.DefineFromJson.NoName", "Missing 'Name' field for animation.");
                Reject();
                continue;
            }
            string name = n.GetString()!;
            if (!hasClip(name))
            {
                SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "AnimationGroupEntry.DefineFromJson.InvalidName", $"No canned animation exists named '{name}'");
                Reject();
                continue;
            }
            // 4.6: Weight must be numeric; there is no default of 1.
            if (!e.TryGetProperty("Weight", out var w) || w.ValueKind != JsonValueKind.Number)
            {
                SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "AnimationGroupEntry.DefineFromJson.NoWeight", "Missing 'Weight' field for animation.");
                Reject();
                continue;
            }
            // 0x00791F0C: an unknown string writes to cerr and returns Happy; only Count is BadType.
            if (!e.TryGetProperty("Mood", out var m) || m.ValueKind != JsonValueKind.String)
            {
                SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "AnimationGroupEntry.DefineFromJson.NoMood", "Missing 'Mood' field for animation.");
                Reject();
                continue;
            }
            // asCString 0x0058C59C, strlen 0x0058C5AC: the name is cut at the first NUL before the lookup (0x0058C5BE);
            // JsonCpp keeps embedded NULs in the decoded string (0x008E1530..0x008E154E).
            string mood = m.GetString()!;
            int nul = mood.IndexOf('\0');
            if (nul >= 0) mood = mood.Substring(0, nul);
            if (mood is not ("Happy" or "Sad" or "Default" or "Count"))
            {
                // cerr, then put(widen('\n')) and flush (0x00792036..0x0079204C): a bare LF on every host.
                Console.Error.Write($"error: string '{mood}' is not a valid SimpleMoodType value\n");
                Console.Error.Flush();
                mood = "Happy";
            }
            if (mood == "Count")
            {
                SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Warning, null, "SimpleMoodScorer.ReadFromJson.BadType", $"Bad 'Mood' = '{mood}'");
                Reject(false);
                continue;
            }
            // 4.8: CooldownTime_Sec defaults to 0.0.
            double cooldown = e.TryGetProperty("CooldownTime_Sec", out var c) && c.ValueKind == JsonValueKind.Number
                ? c.GetDouble() : 0.0;
            // 4.9: UseHeadAngle is optional; absent or false succeeds without a head window. 4.10: when true both
            // angles are required, else the entry is rejected. There are no infinity defaults.
            bool useHead = e.TryGetProperty("UseHeadAngle", out var uh) && uh.ValueKind == JsonValueKind.True;
            float minDeg = 0f, maxDeg = 0f;
            if (useHead)
            {
                if (!e.TryGetProperty("HeadAngleMin_Deg", out var mn) || mn.ValueKind != JsonValueKind.Number
                    || !e.TryGetProperty("HeadAngleMax_Deg", out var mx) || mx.ValueKind != JsonValueKind.Number)
                {
                    SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Error, null, "AnimationGroupEntry.DefineFromJson.NoHeadAngleWhenUsingHeadAngles", "Missing " +
                                "'HeadAngleMin_Deg' or 'HeadAngleMax_Deg' field for animation.");
                    Reject();
                    continue;
                }
                minDeg = (float)mn.GetDouble();
                maxDeg = (float)mx.GetDouble();
            }
            if (useHead) { inheritedMin = minDeg * BitConverter.Int32BitsToSingle(0x3C8EFA35); inheritedMax = maxDeg * BitConverter.Int32BitsToSingle(0x3C8EFA35); windowKnown = true; }
            entries.Add(new AnimationGroupEntry(name, (float)w.GetDouble(), cooldown, mood)
            {
                UseHeadAngle = useHead,
                BackupWindowKnown = windowKnown, BackupMinRad = inheritedMin, BackupMaxRad = inheritedMax,
                HeadAngleMinDeg = minDeg,
                HeadAngleMaxDeg = maxDeg,
            });
        }
        g.Entries = entries;
        return !rejected;
    }
}
