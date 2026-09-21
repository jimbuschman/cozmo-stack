using System.Text.Json;

namespace Cozmo.Robot.Animation;

/// <summary>One choice inside an animation group.</summary>
public sealed record AnimationGroupEntry(string Name, float Weight, float CooldownSec, string Mood)
{
    /// <summary>Whether this entry is only eligible while the head sits inside its window.</summary>
    public bool UseHeadAngle { get; init; }
    /// <summary>The window, in degrees, when <see cref="UseHeadAngle"/> is set.</summary>
    public float HeadAngleMinDeg { get; init; }
    public float HeadAngleMaxDeg { get; init; }

    /// <summary>When this entry was last chosen, in the caller's seconds; negative infinity until it is.</summary>
    internal double LastSelectedSec { get; set; } = double.NegativeInfinity;

    /// <summary>
    /// <c>AnimationGroupContainer::IsAnimationOnCooldown</c>: an entry is unavailable until
    /// <c>now &gt;= selectedAt + CooldownTime_Sec</c>.
    /// </summary>
    public bool IsOnCooldown(double nowSec) => nowSec < LastSelectedSec + CooldownSec;

    /// <summary>When this entry next comes off cooldown, for the all-on-cooldown case.</summary>
    public double CooldownEndsSec => LastSelectedSec + CooldownSec;

    /// <summary>Whether the head angle allows this entry; always true when it does not use the gate.</summary>
    public bool HeadAngleAllows(double? headAngleDeg) =>
        !UseHeadAngle || headAngleDeg is not { } a || (a >= HeadAngleMinDeg && a <= HeadAngleMaxDeg);
}

/// <summary>
/// A named set of interchangeable animations, as the robot's own <c>animationGroups</c> assets define them.
/// The engine picks one by weight and mood; this picks by weight, and filters by mood when asked.
/// </summary>
public sealed class AnimationGroup
{
    public string Name { get; init; } = "";
    public IReadOnlyList<AnimationGroupEntry> Entries { get; init; } = Array.Empty<AnimationGroupEntry>();

    /// <summary>
    /// Chooses an entry by weight, as <c>AnimationGroup::GetAnimationName</c> at 0x0058A970 in
    /// libcozmoEngine.so does: the candidates' weights are summed, <c>RandDbl(total)</c> is drawn, and the
    /// entries are walked subtracting each weight until the draw goes negative. Entries whose mood does
    /// not match are excluded, and when nothing matches the engine retries with the Default mood, which
    /// the fallback to the whole pool below reproduces for this build's all-Default assets.
    ///
    /// Two more filters run before the draw, both carried in the shipped data.
    /// <c>AnimationGroupContainer::IsAnimationOnCooldown</c> excludes an entry until
    /// <c>now &gt;= selectedAt + CooldownTime_Sec</c>, and when every candidate is on cooldown the one
    /// soonest to come off it is taken rather than none. The head-angle gate (<c>UseHeadAngle</c> with
    /// <c>HeadAngleMin_Deg</c> and <c>HeadAngleMax_Deg</c>, used by the CozmoSays groups) drops an entry
    /// whose window the head is outside of.
    ///
    /// <paramref name="nowSec"/> and <paramref name="headAngleDeg"/> are what makes those two work; pass
    /// neither and the selection is the weighted draw alone, which is what this did before.
    /// </summary>
    public AnimationGroupEntry? Choose(Random random, string? mood = null,
                                       double? nowSec = null, double? headAngleDeg = null)
    {
        var pool = mood is null
            ? Entries
            : Entries.Where(e => string.Equals(e.Mood, mood, StringComparison.OrdinalIgnoreCase)).ToList();
        if (pool.Count == 0) pool = Entries;
        if (pool.Count == 0) return null;

        // the head-angle gate: an entry outside its window is simply not a candidate
        if (headAngleDeg is not null)
        {
            var gated = pool.Where(e => e.HeadAngleAllows(headAngleDeg)).ToList();
            if (gated.Count > 0) pool = gated;
        }

        // the cooldown: skip entries still on it, unless that leaves nothing, in which case the engine
        // takes the one that comes off soonest
        if (nowSec is { } now)
        {
            var ready = pool.Where(e => !e.IsOnCooldown(now)).ToList();
            if (ready.Count == 0)
            {
                var soonest = pool.OrderBy(e => e.CooldownEndsSec).First();
                soonest.LastSelectedSec = now;
                return soonest;
            }
            pool = ready;
        }

        var chosen = Draw(random, pool);
        if (nowSec is { } t && chosen is not null) chosen.LastSelectedSec = t;
        return chosen;
    }

    private static AnimationGroupEntry? Draw(Random random, IReadOnlyList<AnimationGroupEntry> pool)
    {
        float total = pool.Sum(e => MathF.Max(0f, e.Weight));
        if (total <= 0f) return pool[random.Next(pool.Count)];
        float pick = (float)random.NextDouble() * total;
        foreach (var e in pool)
        {
            pick -= MathF.Max(0f, e.Weight);
            if (pick <= 0f) return e;
        }
        return pool[^1];
    }

    public override string ToString() => $"{Name}: {Entries.Count} animation(s)";
}

/// <summary>
/// Loads Cozmo's own animation assets: the FlatBuffers clips under <c>assets/animations</c> and the JSON
/// groups under <c>assets/animationGroups</c>.
///
/// The assets are treated as data. Field names and the shape of each keyframe come from the schema that the
/// engine's own symbols confirm; nothing here guesses at what a value means. Loading is lazy per file, so
/// opening a library of three hundred clips costs one directory listing.
/// </summary>
public sealed class AnimationLibrary
{
    // field indices from the CozmoAnim schema, in declaration order
    private const int ClipsField = 0;
    private const int ClipName = 0, ClipKeyframes = 1;
    private const int KfLift = 0, KfFace = 1, KfHead = 2, KfAudio = 3, KfLights = 4,
                      KfFaceAnim = 5, KfEvent = 6, KfBody = 7, KfRecordHeading = 8, KfTurnToHeading = 9;

    private readonly Dictionary<string, string> _clipFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AnimationClip> _clipCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AnimationGroup> _groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    /// <summary>Clip names available, whether or not they have been parsed yet.</summary>
    public IReadOnlyCollection<string> ClipNames { get { lock (_gate) return _clipFiles.Keys.ToArray(); } }
    /// <summary>Group names available.</summary>
    public IReadOnlyCollection<string> GroupNames { get { lock (_gate) return _groups.Keys.ToArray(); } }

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

        if (animations is not null)
            foreach (var f in Directory.EnumerateFiles(animations, "*.bin", SearchOption.AllDirectories))
            {
                // A file can hold more than one clip: anim_bored_01.bin holds anim_bored_01 and
                // anim_bored_02. Indexing by filename alone leaves the others unreachable, so every clip
                // name inside the file is registered. The filename is kept as a fallback for a file whose
                // contents cannot be read.
                try
                {
                    foreach (var clip in ParseFile(f))
                        if (clip.Name.Length > 0) lib._clipFiles[clip.Name] = f;
                }
                catch (Exception) { /* fall back to the filename below */ }
                lib._clipFiles.TryAdd(Path.GetFileNameWithoutExtension(f), f);
            }

        if (groups is not null)
            foreach (var f in Directory.EnumerateFiles(groups, "*.json", SearchOption.AllDirectories))
            {
                var g = LoadGroup(f);
                if (g is not null) lib._groups[g.Name] = g;
            }
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
            var all = ParseFile(path);
            // cache every clip in the file, since parsing it again for its sibling would be wasteful
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

    /// <summary>Every clip in one asset file. A file can hold more than one.</summary>
    public static IReadOnlyList<AnimationClip> ParseFile(string path) => Parse(File.ReadAllBytes(path));

    /// <summary>Every clip in an asset file already in memory.</summary>
    public static IReadOnlyList<AnimationClip> Parse(byte[] bytes)
    {
        var root = FlatTable.Root(bytes);
        var clips = new List<AnimationClip>();
        foreach (var c in root.Tables(ClipsField)) clips.Add(ParseClip(c));
        return clips;
    }

    private static AnimationClip ParseClip(FlatTable clip)
    {
        var frames = new List<Keyframe>();
        var kf = clip.Table(ClipKeyframes);
        if (!kf.IsEmpty)
        {
            foreach (var t in kf.Tables(KfLift))
                frames.Add(new LiftKeyframe(t.U32(0), t.U32(1), t.U8(2), t.U8(3)));

            foreach (var t in kf.Tables(KfFace))
            {
                var left = t.Floats(6);
                var right = t.Floats(7);
                frames.Add(new FaceKeyframe(t.U32(0), new ProceduralFacePose
                {
                    FaceAngle = t.F32(1),
                    FaceCenterX = t.F32(2),
                    FaceCenterY = t.F32(3),
                    FaceScaleX = t.F32(4, 1f),
                    FaceScaleY = t.F32(5, 1f),
                    Left = left.Length == Eye.ParamCount ? Eye.FromAsset(left) : new Eye(),
                    Right = right.Length == Eye.ParamCount ? Eye.FromAsset(right) : new Eye(),
                }));
            }

            foreach (var t in kf.Tables(KfHead))
                frames.Add(new HeadKeyframe(t.U32(0), t.U32(1), t.I8(2), t.U8(3)));

            foreach (var t in kf.Tables(KfAudio))
                frames.Add(new AudioKeyframe(t.U32(0), t.Longs(1), t.F32(2, 1f), t.Floats(3), t.Bool(4, true)));

            foreach (var t in kf.Tables(KfLights))
                frames.Add(new LightsKeyframe(t.U32(0), t.U32(1), t.Floats(2), t.Floats(3), t.Floats(4), t.Floats(5), t.Floats(6)));

            foreach (var t in kf.Tables(KfFaceAnim))
                frames.Add(new FaceAnimationKeyframe(t.U32(0), t.String(1) ?? ""));

            foreach (var t in kf.Tables(KfEvent))
                frames.Add(new EventKeyframe(t.U32(0), t.String(1) ?? ""));

            foreach (var t in kf.Tables(KfBody))
                frames.Add(new BodyKeyframe(t.U32(0), t.U32(1), t.String(2) ?? "", t.I16(3)));

            foreach (var t in kf.Tables(KfRecordHeading))
                frames.Add(new RecordHeadingKeyframe(t.U32(0)));

            foreach (var t in kf.Tables(KfTurnToHeading))
                frames.Add(new TurnToRecordedHeadingKeyframe(t.U32(0), t.U32(1), t.I16(2), t.I16(3),
                                                             t.I16(4, 1000), t.I16(5, 1000), t.U16(6, 2),
                                                             t.U16(7), t.Bool(8)));
        }

        frames.Sort((a, b) => a.TriggerTimeMs.CompareTo(b.TriggerTimeMs));
        AnimationTrack tracks = 0;
        uint end = 0;
        foreach (var f in frames) { tracks |= f.Track; end = Math.Max(end, f.EndTimeMs); }

        return new AnimationClip
        {
            Name = clip.String(ClipName) ?? "",
            Keyframes = frames,
            Tracks = tracks,
            DurationMs = end,
        };
    }

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
