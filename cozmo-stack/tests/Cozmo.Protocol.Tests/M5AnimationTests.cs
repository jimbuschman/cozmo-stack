using System.Net;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M5 animation, against the frozen inventory re-analysis/inventory/M5-animation.md (Appendix A rows A1..A37, B, C, D,
/// E; the gap passes' K, L, G, D, S, C, R, Q1, J, O, and the OpenCV A/B/C rows). Every expected value below is read off
/// a row or computed by hand from a row's rule; none is taken from what the code returns.
/// </summary>
public class M5AnimationTests
{
    // ================================================================== harness

    private sealed class Log : IAnimationSink
    {
        public readonly List<string> L = new();
        public readonly List<byte[]> Faces = new();
        public readonly List<ushort[]> Lights = new();
        public int? Played;
        public int? AudioFramesPlayed => Played;
        public void Face(FaceBitmap bitmap) => L.Add("face");
        public void FaceImage(byte[] payload) { Faces.Add(payload); L.Add("face"); }
        public void Audio(byte[]? mulawFrame) => L.Add(mulawFrame is null ? "silence" : "sample");
        public void Head(sbyte angleDeg, uint durationMs) => L.Add($"head:{angleDeg}:{durationMs}");
        public void Lift(byte heightMm, uint durationMs) => L.Add($"lift:{heightMm}:{durationMs}");
        public void AnimationStarted(byte tag) => L.Add($"start:{tag}");
        public void AnimationEnded() => L.Add("end");
        public void Body(BodyKeyframe k) => L.Add($"body:{k.Speed}:{k.EncodedRadius}");
        public void BodyStop() => L.Add("bodystop");
        void IAnimationSink.Lights(LightsKeyframe keyframe) => L.Add("lights-keyframe");
        public void BackpackLights(ushort[] leds) { Lights.Add(leds); L.Add("lights"); }
        public void AnimEvent(byte animEvent) => L.Add($"event:{animEvent}");
        public void Event(string eventId) { }
        public void RecordHeading() => L.Add("record");
        public void Finished(string clipName, bool completed) => L.Add(completed ? "finished" : "cancelled");
        public int Count(string what) => L.Count(e => e == what);
        public List<List<string>> Frames()
        {
            var f = new List<List<string>>();
            foreach (var e in L)
            {
                if (e is "silence" or "sample") f.Add(new List<string>());
                if (f.Count > 0) f[^1].Add(e);
            }
            return f;
        }
    }

    private static string? AssetsRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var c = Path.Combine(dir.FullName, "re-analysis", "obb", "assets", "cozmo_resources", "assets");
            if (Directory.Exists(c)) return c;
        }
        return null;
    }

    private static AnimationClip Clip(string name, params Keyframe[] frames)
    {
        var list = frames.OrderBy(f => f.TriggerTimeMs).ToList();
        AnimationTrack tracks = 0;
        uint end = 0;
        foreach (var f in list) { tracks |= f.Track; end = Math.Max(end, f.EndTimeMs); }
        return new AnimationClip { Name = name, Keyframes = list, Tracks = tracks, DurationMs = end };
    }

    /// <summary>One Update per 33 ms of the caller's clock (the seam builds one frame per call).</summary>
    private static void Run(AnimationScheduler s, double from, double to)
    {
        for (double t = from; t <= to; t += 33) s.Advance(t);
    }

    private static FaceKeyframe FaceAt(uint t, float eyeCenterX = 0f)
    {
        var pose = ProceduralFaceRenderer.Nominal();
        pose.Left[EyeParam.EyeCenterX] = eyeCenterX;
        return new FaceKeyframe(t, pose);
    }

    /// <summary>A catalog with named clips, a trigger → group map and groups of one entry each.</summary>
    private sealed class Catalog : IAnimationCatalog
    {
        public readonly Dictionary<string, AnimationClip> Clips = new();
        public readonly Dictionary<AnimationTrigger, string> Triggers = new();
        public readonly Dictionary<string, string[]> Groups = new();
        public bool HasAnimationForTrigger(AnimationTrigger trigger) => Triggers.ContainsKey(trigger);
        public string GetAnimationForTrigger(AnimationTrigger trigger) => Triggers.GetValueOrDefault(trigger, "");
        public string GetAnimationNameFromGroup(string group, bool strict) => Groups.TryGetValue(group, out var g) && g.Length > 0 ? g[0] : "";
        public (string Name, int Count) GetFirstAnimationName(string group) =>
            Groups.TryGetValue(group, out var g) && g.Length > 0 ? (g[0], g.Length) : ("", 0);
        public AnimationClip? GetAnimation(string name) => Clips.GetValueOrDefault(name);
    }

    // ------------------------------------------------------------------ a minimal FlatBuffer writer for the CozmoAnim schema

    /// <summary>A table: its fields by id.</summary>
    private sealed class T
    {
        public readonly SortedDictionary<int, object> F = new();
        public T Set(int id, object v) { F[id] = v; return this; }
    }

    private static byte[] Fb(T root)
    {
        var buf = new List<byte>(new byte[4]);
        int rootAt = WriteTable(buf, root);
        Patch(buf, 0, rootAt - 0);
        return buf.ToArray();
    }

    private static void Patch(List<byte> b, int at, int value)
    {
        var bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < 4; i++) b[at + i] = bytes[i];
    }

    private static int WriteTable(List<byte> b, T t)
    {
        int maxId = t.F.Count == 0 ? -1 : t.F.Keys.Max();
        // inline layout: soffset (4) then each field
        var fieldOffsets = new Dictionary<int, int>();
        var inline = new List<byte>();
        foreach (var (id, v) in t.F)
        {
            fieldOffsets[id] = 4 + inline.Count;
            switch (v)
            {
                case byte x: inline.Add(x); break;
                case sbyte x: inline.Add((byte)x); break;
                case bool x: inline.Add((byte)(x ? 1 : 0)); break;
                case short x: inline.AddRange(BitConverter.GetBytes(x)); break;
                case ushort x: inline.AddRange(BitConverter.GetBytes(x)); break;
                case int x: inline.AddRange(BitConverter.GetBytes(x)); break;
                case uint x: inline.AddRange(BitConverter.GetBytes(x)); break;
                case float x: inline.AddRange(BitConverter.GetBytes(x)); break;
                default: inline.AddRange(new byte[4]); break;      // an offset, patched below
            }
        }
        int vtSize = 4 + 2 * (maxId + 1);
        int vt = b.Count;
        b.AddRange(BitConverter.GetBytes((ushort)vtSize));
        b.AddRange(BitConverter.GetBytes((ushort)(4 + inline.Count)));
        for (int id = 0; id <= maxId; id++)
            b.AddRange(BitConverter.GetBytes((ushort)(fieldOffsets.TryGetValue(id, out var o) ? o : 0)));
        int table = b.Count;
        b.AddRange(BitConverter.GetBytes(table - vt));
        b.AddRange(inline);
        foreach (var (id, v) in t.F)
        {
            int fieldAt = table + fieldOffsets[id];
            int? target = v switch
            {
                string s => WriteString(b, s),
                float[] fs => WriteVector(b, fs.Length, i => BitConverter.GetBytes(fs[i])),
                long[] ls => WriteVector(b, ls.Length, i => BitConverter.GetBytes(ls[i])),
                T child => WriteTable(b, child),
                T[] children => WriteTableVector(b, children),
                _ => null,
            };
            if (target is { } at) Patch(b, fieldAt, at - fieldAt);
        }
        return table;
    }

    private static int WriteString(List<byte> b, string s)
    {
        int at = b.Count;
        var bytes = Encoding.UTF8.GetBytes(s);
        b.AddRange(BitConverter.GetBytes(bytes.Length));
        b.AddRange(bytes);
        b.Add(0);
        return at;
    }

    private static int WriteVector(List<byte> b, int n, Func<int, byte[]> element)
    {
        int at = b.Count;
        b.AddRange(BitConverter.GetBytes(n));
        for (int i = 0; i < n; i++) b.AddRange(element(i));
        return at;
    }

    private static int WriteTableVector(List<byte> b, T[] children)
    {
        int at = b.Count;
        b.AddRange(BitConverter.GetBytes(children.Length));
        int first = b.Count;
        b.AddRange(new byte[4 * children.Length]);
        for (int i = 0; i < children.Length; i++)
        {
            int tableAt = WriteTable(b, children[i]);
            Patch(b, first + 4 * i, tableAt - (first + 4 * i));
        }
        return at;
    }

    /// <summary>One AnimClip in an AnimClips file; <paramref name="keyframes"/> by Keyframes field id (D1).</summary>
    private static AnimationClip Load(string name, params (int Field, T[] Frames)[] keyframes)
    {
        var kf = new T();
        foreach (var (field, frames) in keyframes) kf.Set(field, frames);
        var clip = new T().Set(0, name).Set(1, kf);
        return Assert.Single(AnimationLibrary.Parse(Fb(new T().Set(0, new[] { clip }))));
    }

    private const int FLift = 0, FFace = 1, FHead = 2, FAudio = 3, FLights = 4, FFaceAnim = 5, FEvent = 6, FBody = 7,
                      FRecord = 8, FTurnTo = 9;

    // ================================================================== M5-001: clip loading (D1, gap1 C1..C3)

    /// <summary>
    /// D1, gap1 C1, C2: the tracks are read in the order Lift, ProcFace, Head, RobotAudio, Backpack, FaceAnim, Event, Body,
    /// RecordHeading, TurnTo; a keyframe the track refuses (here a head keyframe whose trigger is not after the previous
    /// one, BadTriggerTime, L4) ends the load: the lift, the face and the first head keyframe stay, and the body keyframe,
    /// on a later track, is never loaded.
    /// </summary>
    [Fact]
    public void M5_001_D1_C1_C2_ARejectedKeyframeEndsTheLoadAndKeepsWhatCameBefore()
    {
        var clip = Load("c",
            (FBody, new[] { new T().Set(0, 0u).Set(1, 100u).Set(2, "STRAIGHT").Set(3, (short)50) }),
            (FLift, new[] { new T().Set(0, 0u).Set(1, 100u).Set(2, (byte)50) }),
            (FFace, new[] { new T().Set(0, 0u) }),
            (FHead, new[] { new T().Set(0, 10u).Set(1, 100u).Set(2, (sbyte)5), new T().Set(0, 10u).Set(1, 100u).Set(2, (sbyte)6) }));
        Assert.True(clip.LoadTruncated);
        Assert.Single(clip.Keyframes.OfType<LiftKeyframe>());
        Assert.Single(clip.Keyframes.OfType<FaceKeyframe>());
        Assert.Equal((sbyte)5, Assert.Single(clip.Keyframes.OfType<HeadKeyframe>()).AngleDeg);
        Assert.Empty(clip.Keyframes.OfType<BodyKeyframe>());
    }

    /// <summary>
    /// gap2 J1..J5, gap4 J1.1..J1.10, MD3: the four shipped JSON clips load. Worked from the assets and the rows:
    /// soundTestAnim is one audio keyframe {4068444155, volume 1.0, probability 1.0 (the scalar as one element), hasAlts
    /// false}; anim_qa_firmwaremessaging_01's head angles ±24.999999999999996 truncate to ±24 (J1.5); ANIMATION_TEST's body
    /// keyframes are radius 50 (numeric, CheckTurnSpeed: 150 stays), TURN_IN_PLACE at 450 → 300 (CheckRotationSpeed) and
    /// STRAIGHT at 10; anim_triple_backup's first body keyframe is speed −25 for 500 ms. None is truncated.
    /// </summary>
    [Fact]
    public void M5_001_J1_TheFourShippedJsonClipsLoad()
    {
        var root = AssetsRoot();
        if (root is null) return;
        var lib = AnimationLibrary.Open(root);
        Assert.Equal(new[] { "ANIMATION_TEST", "anim_qa_firmwaremessaging_01", "anim_triple_backup", "soundTestAnim" },
                     lib.JsonClipNames.OrderBy(n => n, StringComparer.Ordinal));
        foreach (var n in lib.JsonClipNames) Assert.False(lib.GetClip(n).LoadTruncated, n);

        var sound = Assert.Single(lib.GetClip("soundTestAnim").Keyframes.OfType<AudioKeyframe>());
        Assert.Equal(new long[] { 4068444155 }, sound.EventIds);
        Assert.Equal((1f, false), (sound.Volume, sound.HasAlts));
        Assert.Equal(new[] { 1f }, sound.Probabilities);

        var qa = lib.GetClip("anim_qa_firmwaremessaging_01");
        Assert.Equal(new sbyte[] { -24, 24 }, qa.Keyframes.OfType<HeadKeyframe>().Select(h => h.AngleDeg));
        Assert.Equal((byte)92, Assert.Single(qa.Keyframes.OfType<LiftKeyframe>()).HeightMm);
        Assert.Equal(6, qa.Keyframes.OfType<LightsKeyframe>().Count());

        var test = lib.GetClip("ANIMATION_TEST").Keyframes.OfType<BodyKeyframe>().ToList();
        Assert.Equal(new short?[] { 50, 0, 0x7FFF }, test.Select(b => b.EncodedRadius));
        Assert.Equal(new short[] { 150, 300, 10 }, test.Select(b => b.Speed));

        var backup = lib.GetClip("anim_triple_backup").Keyframes.OfType<BodyKeyframe>().First();
        Assert.Equal(((short)-25, 500u), (backup.Speed, backup.DurationTimeMs));
    }

    /// <summary>gap4 P1: a clip named FaceAnimationManager::ProceduralAnimName, "_PROCEDURAL_", is skipped.</summary>
    [Fact]
    public void M5_001_P1_TheProceduralNameIsSkipped()
    {
        var dir = Directory.CreateTempSubdirectory("m5json");
        try
        {
            var anims = Directory.CreateDirectory(Path.Combine(dir.FullName, "animations"));
            File.WriteAllText(Path.Combine(anims.FullName, "p.json"),
                "{\"_PROCEDURAL_\":[{\"Name\":\"HeadAngleKeyFrame\",\"triggerTime_ms\":0,\"durationTime_ms\":100,\"angle_deg\":5,\"angleVariability_deg\":0}]}");
            File.WriteAllText(Path.Combine(anims.FullName, "q.json"),
                "{\"q\":[{\"Name\":\"HeadAngleKeyFrame\",\"triggerTime_ms\":0,\"durationTime_ms\":100,\"angle_deg\":5.9,\"angleVariability_deg\":0},{\"Name\":\"HeadAngleKeyFrame\",\"triggerTime_ms\":0,\"durationTime_ms\":1,\"angle_deg\":1,\"angleVariability_deg\":0}]}");
            var lib = AnimationLibrary.Open(dir.FullName);
            Assert.False(lib.HasClip("_PROCEDURAL_"));
            var q = lib.GetClip("q");
            Assert.True(q.LoadTruncated);                                   // the second head has no later trigger (J1.2)
            Assert.Equal((sbyte)5, Assert.Single(q.Keyframes.OfType<HeadKeyframe>()).AngleDeg);   // 5.9 truncated
        }
        finally { dir.Delete(recursive: true); }
    }

    // ================================================================== M5-002: ProceduralFace default and SetFromFlatBuf (C6, gap3 K4)

    /// <summary>gap3 K4: ProceduralFace() is all 0 except EyeScaleX/Y = 1 on both eyes, face scale 1, centre 0, angle 0, no distorter.</summary>
    [Fact]
    public void M5_002_K4_TheDefaultFace()
    {
        var f = new ProceduralFacePose();
        foreach (var eye in new[] { f.Left, f.Right })
            for (int i = 0; i < Eye.ParamCount; i++)
                Assert.Equal(i is 2 or 3 ? 1f : 0f, eye[i]);
        Assert.Equal((0f, 1f, 1f, 0f, 0f), (f.FaceAngle, f.FaceScaleX, f.FaceScaleY, f.FaceCenterX, f.FaceCenterY));
        Assert.Null(f.Distorter);
    }

    /// <summary>
    /// C6: a wrong-size eye is left unchanged (the default eye); each value is Clipped, a NaN keeping the previous (C9);
    /// a negative scale becomes 0; an absent scale is 1; the centre goes through SetFacePosition: for a default face's
    /// eye box (xmax = 96 + 15 = 111, ymin = 32 − 20 = 12, K2) x is clamped to 128 − 111 = 17 and y to −12.
    /// </summary>
    [Fact]
    public void M5_002_C6_SetFromFlatBufRules()
    {
        var right = new float[19];
        right[2] = 1f; right[3] = 1f; right[5] = float.NaN; right[6] = 2f;
        var clip = Load("f", (FFace, new[]
        {
            new T().Set(0, 0u).Set(4, -1f).Set(6, new float[18]).Set(7, right),
            new T().Set(0, 33u).Set(2, 100f).Set(3, -100f),
        }));
        var faces = clip.Keyframes.OfType<FaceKeyframe>().ToList();
        var a = faces[0].Pose;
        Assert.Equal(new Eye().ToArray(), a.Left.ToArray());          // 18 floats: unchanged
        Assert.Equal(0f, a.Right[5]);                                  // NaN keeps the default's 0
        Assert.Equal(1f, a.Right[6]);                                  // radius 2 clipped to 1
        Assert.Equal(0f, a.FaceScaleX);                                // negative → 0
        Assert.Equal(1f, a.FaceScaleY);                                // absent → 1
        var b = faces[1].Pose;
        Assert.Equal(17f, b.FaceCenterX);
        Assert.Equal(-12f, b.FaceCenterY);
    }

    /// <summary>
    /// C2: SetFromFlatBuf and Interpolate call SetFacePosition before the face scale is set, so the clamp uses scale 1:
    /// xmax = 96 + 15 = 111 gives 17, where a scale of 2 would have given 128 − (96 + 30) = 2.
    /// </summary>
    [Fact]
    public void M5_002_C2_TheCentreIsClampedBeforeTheScaleIsSet()
    {
        var clip = Load("f", (FFace, new[] { new T().Set(0, 0u).Set(2, 100f).Set(4, 2f).Set(5, 2f) }));
        var pose = Assert.Single(clip.Keyframes.OfType<FaceKeyframe>()).Pose;
        Assert.Equal((17f, 2f), (pose.FaceCenterX, pose.FaceScaleX));

        var a = new ProceduralFacePose { FaceCenterX = 100f, FaceScaleX = 2f };
        var b = new ProceduralFacePose { FaceCenterX = 100f, FaceScaleX = 2f };
        var mid = ProceduralFacePose.Interpolate(a, b, 0.5f);
        Assert.Equal((17f, 2f), (mid.FaceCenterX, mid.FaceScaleX));
    }

    // ================================================================== M5-003: GetFaceHelper, Interpolate, Clip, Combine

    /// <summary>
    /// C7: due when start + trigger ≤ t; with no next keyframe the face is used and the track advances; with the next due
    /// too, no face and the track advances; otherwise interpolated with frac = (t − trig)/(next − trig), advancing when
    /// the next is due by t + 33.
    /// </summary>
    [Fact]
    public void M5_003_C7_GetFaceHelper()
    {
        var a = new ProceduralFacePose();
        var b = new ProceduralFacePose();
        b.Left[EyeParam.EyeCenterX] = 10f;

        var track = new StreamTrack<FaceFrame>();
        track.AddKeyFrameToBack(new FaceFrame(0, a));
        track.AddKeyFrameToBack(new FaceFrame(100, b));
        var face = new ProceduralFacePose();
        Assert.True(TrackLayerComponent.GetFaceHelper(track, 0, 50, ref face, replace: true));
        Assert.Equal(5f, face.Left[EyeParam.EyeCenterX], 4);            // frac 0.5
        Assert.Equal(0u, track.Current!.Trigger);                        // 50 + 33 < 100: no advance
        Assert.True(TrackLayerComponent.GetFaceHelper(track, 0, 70, ref face, replace: true));
        Assert.Equal(100u, track.Current!.Trigger);                      // 70 + 33 ≥ 100: advanced
        Assert.False(TrackLayerComponent.GetFaceHelper(track, 0, 90, ref face, replace: true));   // not due
        Assert.True(TrackLayerComponent.GetFaceHelper(track, 0, 100, ref face, replace: true));   // last: used
        Assert.True(track.AtEnd);

        var both = new StreamTrack<FaceFrame>();
        both.AddKeyFrameToBack(new FaceFrame(0, a));
        both.AddKeyFrameToBack(new FaceFrame(10, b));
        Assert.False(TrackLayerComponent.GetFaceHelper(both, 0, 20, ref face, replace: true));    // the next is due too
        Assert.Equal(10u, both.Current!.Trigger);
    }

    /// <summary>
    /// C8, G10: t = 0 or 1 copies the face with its distorter; 0 &lt; t &lt; 1 starts from a default face (no distorter);
    /// angles blend as unit vectors, so 350° and 10° meet at 0°. C9: a NaN keeps the target's previous value.
    /// </summary>
    [Fact]
    public void M5_003_C8_C9_G10_InterpolateAndClip()
    {
        var a = new ProceduralFacePose { Distorter = new ScanlineDistorter(2, 0f) };
        var b = new ProceduralFacePose();
        Assert.NotNull(ProceduralFacePose.Interpolate(a, b, 0f).Distorter);
        Assert.Null(ProceduralFacePose.Interpolate(a, b, 0.5f).Distorter);

        a.FaceAngle = 350f; b.FaceAngle = 10f;
        float mid = ProceduralFacePose.Interpolate(a, b, 0.5f).FaceAngle;
        Assert.InRange(mid, -0.01f, 0.01f);

        Assert.Equal(0.25f, Eye.Clip(EyeParam.UpperLidY, float.NaN, 0.25f));
        Assert.Equal(45f, Eye.Clip(EyeParam.UpperLidAngle, 90f, 0f));
        Assert.Equal(1f, Eye.Clip(EyeParam.LowerLidBend, 3f, 0f));
        Assert.Equal(500f, Eye.Clip(EyeParam.EyeCenterX, 500f, 0f));   // unclipped
    }

    /// <summary>
    /// C10: Combine adds EyeCenterX/Y, EyeAngle and both lid angles, the face angle and centre; multiplies EyeScaleX/Y and
    /// the face scale; keeps the base's other parameters; no clip.
    /// </summary>
    [Fact]
    public void M5_003_C10_Combine()
    {
        var baseFace = new ProceduralFacePose { FaceScaleX = 2f, FaceCenterX = 1f };
        baseFace.Left[EyeParam.EyeScaleX] = 2f;
        baseFace.Left[EyeParam.UpperLidY] = 0.3f;
        baseFace.Left[EyeParam.UpperLidAngle] = 40f;
        var layer = new ProceduralFacePose { FaceScaleX = 3f, FaceCenterX = 2f };
        layer.Left[EyeParam.EyeScaleX] = 1.5f;
        layer.Left[EyeParam.UpperLidY] = 0.9f;
        layer.Left[EyeParam.UpperLidAngle] = 40f;
        baseFace.Combine(layer);
        Assert.Equal(3f, baseFace.Left[EyeParam.EyeScaleX]);          // 2 × 1.5
        Assert.Equal(0.3f, baseFace.Left[EyeParam.UpperLidY]);         // kept
        Assert.Equal(80f, baseFace.Left[EyeParam.UpperLidAngle]);      // 40 + 40, not clipped to 45
        Assert.Equal(6f, baseFace.FaceScaleX);
        Assert.Equal(3f, baseFace.FaceCenterX);
    }

    // ================================================================== M5-004, M5-005: head and lift

    /// <summary>C2, C3: 0x93 {u16 duration, s8 angle} and 0x94 {u16 duration, u8 height}, each once at its trigger.</summary>
    [Fact]
    public void M5_004_C2_C3_HeadAndLiftGoOnceAtTheirTrigger()
    {
        var log = new Log();
        var s = new AnimationScheduler(log);
        s.Play(Clip("hl", new HeadKeyframe(66, 120, -7, 0), new LiftKeyframe(66, 250, 60, 0), new EventKeyframe(200, "TAPPED_BLOCK")), 0);
        Run(s, 0, 300);
        Assert.Equal(new[] { "head:-7:120" }, log.L.Where(e => e.StartsWith("head")));
        Assert.Equal(new[] { "lift:60:250" }, log.L.Where(e => e.StartsWith("lift")));
        var frames = log.Frames();
        Assert.Contains("head:-7:120", frames[2]);                       // the frame at 66

        // on the wire the duration is a u16 (strh): 70000 goes as 4464
        using var rig = new Rig();
        rig.ToSuccess();
        int before = rig.Port.Messages().Count;
        rig.Robot.Animations.Scheduler.Play(Clip("long", new HeadKeyframe(0, 70_000, -7, 0)), 0);
        rig.Robot.Animations.Scheduler.Advance(0);
        var head = Assert.Single(rig.Port.Messages().Skip(before).OfType<Protocol.HeadAngle>());
        Assert.Equal(((ushort)4464, (sbyte)-7), (head.DurationTimeMs, head.AngleDeg));
    }

    /// <summary>C3, M5-005: the variability draw is not clamped: a height of 254 with variability 5 can wrap past 255.</summary>
    [Fact]
    public void M5_005_C3_VariabilityIsNotClamped()
    {
        var seen = new HashSet<int>();
        for (int seed = 0; seed < 200; seed++)
        {
            var log = new Log();
            var s = new AnimationScheduler(log, new Random(seed));
            s.Play(Clip("l", new LiftKeyframe(0, 100, 254, 5)), 0);
            s.Advance(0);
            var lift = log.L.Single(e => e.StartsWith("lift"));
            seen.Add(int.Parse(lift.Split(':')[1]));
        }
        Assert.Contains(seen, v => v < 5);                               // 256..259 wrapped to 0..3
        Assert.All(seen, v => Assert.True(v >= 249 || v <= 3));
    }

    // ================================================================== M5-006: body (C4, C5, S1)

    /// <summary>
    /// C4, S1: TURN_IN_PLACE/POINT_TURN → 0 with |v| &gt; 300 clamped to ±300; STRAIGHT → 0x7FFF and a number (atoi, "23.0"
    /// → 23) with |v| ≥ 221 clamped to ±220; a negative duration becomes INT_MAX; an unknown token rejects the keyframe.
    /// </summary>
    [Fact]
    public void M5_006_C4_S1_TheRadiusStringAndTheSpeedClamps()
    {
        var clip = Load("b", (FBody, new[]
        {
            new T().Set(0, 0u).Set(1, 100u).Set(2, "TURN_IN_PLACE").Set(3, (short)400),
            new T().Set(0, 10u).Set(1, 100u).Set(2, "POINT_TURN").Set(3, (short)-350),
            new T().Set(0, 20u).Set(1, 100u).Set(2, "TURN_IN_PLACE").Set(3, (short)300),
            new T().Set(0, 30u).Set(1, 100u).Set(2, "STRAIGHT").Set(3, (short)250),
            new T().Set(0, 40u).Set(1, 100u).Set(2, "STRAIGHT").Set(3, (short)220),
            new T().Set(0, 50u).Set(1, unchecked((uint)-1)).Set(2, "23.0").Set(3, (short)221),
        }));
        var b = clip.Keyframes.OfType<BodyKeyframe>().ToList();
        Assert.Equal(new short[] { 300, -300, 300, 220, 220, 220 }, b.Select(k => k.Speed));
        Assert.Equal(new short?[] { 0, 0, 0, 0x7FFF, 0x7FFF, 23 }, b.Select(k => k.EncodedRadius));
        Assert.Equal((uint)int.MaxValue, b[5].DurationTimeMs);

        var bad = Load("x", (FBody, new[] { new T().Set(0, 0u).Set(1, 100u).Set(2, "SPIRAL").Set(3, (short)10) }));
        Assert.True(bad.LoadTruncated);
        Assert.Empty(bad.Keyframes);
    }

    /// <summary>
    /// C5: counter 0 sends {speed, radius}; null frames while counter &lt; duration; the stop {0, 0x7FFF} on the first frame
    /// with counter ≥ duration (counter +33 per frame), whatever the speed. Duration 100: frames 0 (start), 33, 66, 99
    /// (nothing), 132 (stop). Duration 0: the start only.
    /// </summary>
    [Fact]
    public void M5_006_C5_TheStopGoesOnTheFirstFrameWithTheCounterAtTheDuration()
    {
        var log = new Log();
        var s = new AnimationScheduler(log);
        s.Play(Clip("b", new BodyKeyframe(0, 100, "STRAIGHT", 0), new EventKeyframe(300, "TAPPED_BLOCK")), 0);
        Run(s, 0, 400);
        var frames = log.Frames();
        Assert.Contains("body:0:32767", frames[0]);
        for (int i = 1; i <= 3; i++) Assert.DoesNotContain(frames[i], e => e.StartsWith("body"));
        Assert.Contains("bodystop", frames[4]);
        Assert.Equal(1, log.Count("bodystop"));

        var log0 = new Log();
        var s0 = new AnimationScheduler(log0);
        s0.Play(Clip("b0", new BodyKeyframe(0, 0, "STRAIGHT", 40), new EventKeyframe(200, "TAPPED_BLOCK")), 0);
        Run(s0, 0, 300);
        Assert.Equal(1, log0.L.Count(e => e.StartsWith("body:")));
        Assert.Equal(0, log0.Count("bodystop"));
    }

    // ================================================================== M5-007, M5-018: the timeline

    /// <summary>
    /// A18: the stream time is frames built × 33, the budget-stopped frame included; no frame is built while the robot's
    /// budget is exhausted (C9: 14 audio frames with nothing played).
    /// </summary>
    [Fact]
    public void M5_007_A18_TheStreamTimeIsFramesBuiltTimes33()
    {
        var log = new Log { Played = 0 };
        var s = new AnimationScheduler(log);
        s.Play(Clip("t", new EventKeyframe(5_000, "TAPPED_BLOCK")), 0);
        s.Advance(0);
        Assert.Equal(14, log.Count("silence"));
        Assert.Equal(15 * 33, s.StreamTimeMs);
        s.Advance(60);
        Assert.Equal(15 * 33, s.StreamTimeMs);
    }

    /// <summary>
    /// A15: with an audio animation the frames go on after every keyframe track is at its end, while the sound plays
    /// (the audio animation's readiness decides, not HasFramesLeft).
    /// </summary>
    [Fact]
    public void M5_018_A15_FramesContinueWhileTheSoundPlays()
    {
        var log = new Log();
        var s = new AnimationScheduler(log) { AudioSource = new Tone(744 * 10) };
        s.Play(Clip("a", new AudioKeyframe(0, new long[] { 1 }, 1f, new[] { 1f }, false)), 0);
        Run(s, 0, 600);
        Assert.Equal(10, log.Count("sample"));
        Assert.Equal(1, log.Count("end"));
        Assert.True(log.L.IndexOf("end") > log.L.LastIndexOf("sample"));
    }

    private sealed class Tone : IAnimationAudioSource
    {
        private readonly int _n;
        public Tone(int samples) => _n = samples;
        public short[]? GetPcm(long eventId, float volume) => Enumerable.Repeat((short)8000, _n).ToArray();
        public string? NameOf(long eventId) => "tone";
    }

    // ================================================================== M5-008: one streaming animation (A4..A9, A13, A27)

    /// <summary>
    /// A4: a streaming animation and a non-null newcomer without interrupt → refused (tag 0), nothing changes; A5/A9: with
    /// interrupt the old one is aborted and the newcomer gets the next tag; A9: tags cycle 1..0xFE.
    /// </summary>
    [Fact]
    public void M5_008_A4_A5_A9_RefuseInterruptAndTheTags()
    {
        var s = new AnimationScheduler(new Log());
        var first = s.Play(Clip("a", new EventKeyframe(5_000, "TAPPED_BLOCK")), 0)!;
        Assert.Equal(1, first.Tag);
        Assert.Null(s.Play(Clip("b", new EventKeyframe(0, "TAPPED_BLOCK")), 1, replaceRunning: false));
        Assert.Equal("a", s.Playing);
        var second = s.Play(Clip("b", new EventKeyframe(5_000, "TAPPED_BLOCK")), 2)!;
        Assert.Equal(2, second.Tag);
        Assert.Equal(AnimationEndReason.Replaced, first.Completion.Result);

        var tags = new List<byte>();
        for (int i = 0; i < 300; i++) tags.Add(s.Play(Clip("c", new EventKeyframe(5_000, "TAPPED_BLOCK")), 3)!.Tag);
        Assert.All(tags, t => Assert.InRange(t, (byte)1, (byte)0xFE));
        int wrap = tags.IndexOf(0xFE);
        Assert.Equal(1, tags[wrap + 1]);                                 // after 0xFE comes 1
    }

    /// <summary>
    /// A13: each loop re-inits with the same tag and sends its own Start and End; the handle completes on the Update
    /// after the last End; numLoops 0 loops forever.
    /// </summary>
    [Fact]
    public void M5_008_A13_EachLoopReInitsWithTheSameTag()
    {
        var log = new Log();
        var s = new AnimationScheduler(log);
        var h = s.Play(Clip("l", new HeadKeyframe(0, 100, 5, 0)), 0, numLoops: 3)!;
        Run(s, 0, 600);
        Assert.Equal(new[] { "start:1", "start:1", "start:1" }, log.L.Where(e => e.StartsWith("start")));
        Assert.Equal(3, log.Count("end"));
        Assert.Equal(AnimationEndReason.Completed, h.Completion.Result);

        var forever = new AnimationScheduler(new Log());
        forever.Play(Clip("f", new HeadKeyframe(0, 100, 5, 0)), 0, numLoops: 0);
        Run(forever, 0, 1000);
        Assert.True(forever.IsPlaying);
    }

    /// <summary>A27: ReplayLastAnimation plays the last streamed name again (interrupt = 1), with a new tag.</summary>
    [Fact]
    public void M5_008_A27_ReplayLastAnimation()
    {
        var cat = new Catalog();
        var clip = Clip("r", new EventKeyframe(5_000, "TAPPED_BLOCK"));
        cat.Clips["r"] = clip;
        var s = new AnimationScheduler(new Log()) { Catalog = cat };
        s.Play(clip, 0);
        var again = s.ReplayLastAnimation(10)!;
        Assert.Equal("r", again.ClipName);
        Assert.Equal(2, again.Tag);
    }

    // ================================================================== M5-009: audio keyframe load rules (C13)

    /// <summary>
    /// C13: the id is the low 32 bits; volume defaults to 1.0; no probability vector gives 1/N each; a count mismatch or a
    /// running sum above 1 rejects the keyframe.
    /// </summary>
    [Fact]
    public void M5_009_C13_TheAudioKeyframeLoadRules()
    {
        var ok = Load("a", (FAudio, new[] { new T().Set(0, 0u).Set(1, new[] { 0x1_0000_0005L, 7L }) }));
        var k = Assert.Single(ok.Keyframes.OfType<AudioKeyframe>());
        Assert.Equal(new long[] { 5, 7 }, k.EventIds);
        Assert.Equal(1f, k.Volume);
        Assert.Equal(new[] { 0.5f, 0.5f }, k.Probabilities);
        Assert.True(k.HasAlts);

        var mismatch = Load("m", (FAudio, new[] { new T().Set(0, 0u).Set(1, new[] { 1L, 2L }).Set(3, new[] { 1f }) }));
        Assert.True(mismatch.LoadTruncated);
        Assert.Empty(mismatch.Keyframes);
        var over = Load("o", (FAudio, new[] { new T().Set(0, 0u).Set(1, new[] { 1L, 2L }).Set(3, new[] { 0.6f, 0.6f }) }));
        Assert.True(over.LoadTruncated);
    }

    // ================================================================== M5-010: the neutral face (A2, A6, A30, A31)

    private static (AnimationScheduler S, Log Log, AnimationClip Neutral) WithNeutral()
    {
        var cat = new Catalog();
        var neutralPose = ProceduralFaceRenderer.Nominal();
        neutralPose.Left[EyeParam.EyeCenterX] = 3f;
        var neutral = Clip("anim_neutral_eyes_01", new FaceKeyframe(0, neutralPose));
        cat.Clips[neutral.Name] = neutral;
        cat.Triggers[AnimationTrigger.NeutralFace] = "ag_neutral_face";
        cat.Groups["ag_neutral_face"] = new[] { neutral.Name };
        var log = new Log();
        var s = new AnimationScheduler(log, new Random(1)) { Catalog = cat };
        s.LoadNeutralFace();
        return (s, log, neutral);
    }

    /// <summary>A2: the neutral clip is the first of ag_neutral_face; its first ProceduralFace keyframe becomes the layer base face.</summary>
    [Fact]
    public void M5_010_A2_TheNeutralFaceIsTheLayerBase()
    {
        var (s, _, neutral) = WithNeutral();
        Assert.Same(neutral, s.NeutralFaceAnimation);
        Assert.Equal(3f, s.LayerBaseFace.Left[EyeParam.EyeCenterX]);
    }

    /// <summary>
    /// A6, A31: a cancel (SetStreamingAnimation(null)) sets +0x73; the keep-alive block, once nothing streams, there is no
    /// idle and more than 0.5 s has passed since the last stream, replays the neutral clip with SetStreamingAnimation
    /// (a new tag, 2) and it streams in that same Update. Before the 0.5 s nothing is replayed.
    /// </summary>
    [Fact]
    public void M5_010_A6_A31_TheNeutralFaceIsReplayedAfterACancel()
    {
        var (s, log, _) = WithNeutral();
        s.Play(Clip("x", new EventKeyframe(5_000, "TAPPED_BLOCK")), 1_000);
        s.Advance(1_000);                                                 // +0x88 = 1.0 s
        s.Stop();
        log.L.Clear();
        s.Advance(1_400);
        Assert.DoesNotContain(log.L, e => e.StartsWith("start"));
        s.Advance(1_600);
        Assert.Equal("start:2", log.L.First(e => e.StartsWith("start")));
        Assert.Equal("anim_neutral_eyes_01", s.Playing);
        Assert.Contains("face", log.L);
    }

    // ================================================================== M5-011, M5-014: groups (D5..D7)

    private static AnimationGroup Group(string name, params AnimationGroupEntry[] e) => new() { Name = name, Entries = e };

    /// <summary>D5: RandDbl(Σw) minus each weight, the pick where r goes below 0: a weight-0 first entry is never picked.</summary>
    [Fact]
    public void M5_011_D5_TheWeightedDraw()
    {
        var g = Group("g", new AnimationGroupEntry("zero", 0f, 0f, "Default"), new AnimationGroupEntry("one", 1f, 0f, "Default"));
        for (int seed = 0; seed < 50; seed++) Assert.Equal("one", g.Choose(new Random(seed))!.Name);
    }

    /// <summary>
    /// D6: with nothing inside the head window, in Default and not strict, the backup is the Default entry whose
    /// [min − 0.05, max + 0.05] rad window holds the head angle, with no cooldown set; with no backup the first entry of
    /// the list; strict gives nothing.
    /// </summary>
    [Fact]
    public void M5_014_D6_TheDefaultBackup()
    {
        var a = new AnimationGroupEntry("a", 1f, 100f, "Default") { UseHeadAngle = true, HeadAngleMinDeg = 10, HeadAngleMaxDeg = 20 };
        var b = new AnimationGroupEntry("b", 1f, 0f, "Default") { UseHeadAngle = true, HeadAngleMinDeg = 30, HeadAngleMaxDeg = 40 };
        var g = Group("g", a, b);
        // 21° = 0.3665 rad: outside a's window, inside a's window + 0.05 (0.3991)
        Assert.Equal("a", g.Choose(new Random(1), nowSec: 0, headAngleDeg: 21)!.Name);
        Assert.False(a.IsOnCooldown(0.1));                                // a backup sets no cooldown
        // 25° = 0.4363 rad: within neither widened window → the first entry
        Assert.Equal("a", g.Choose(new Random(1), nowSec: 0, headAngleDeg: 25)!.Name);
        Assert.Null(g.Choose(new Random(1), nowSec: 0, headAngleDeg: 25, strict: true));
    }

    /// <summary>D5, D7: a pick sets now + cooldown; the cooldown map is keyed by name and shared across the container's groups.</summary>
    [Fact]
    public void M5_014_D7_TheCooldownIsSharedByName()
    {
        var root = AssetsRoot();
        if (root is null) return;
        var lib = AnimationLibrary.Open(root);
        var groups = lib.GroupNames.Select(n => lib.GetGroup(n)!).ToList();
        var shared = groups.SelectMany(g => g.Entries.Select(e => (g, e))).GroupBy(x => x.e.Name)
                           .FirstOrDefault(x => x.Select(y => y.g.Name).Distinct().Count() > 1);
        if (shared is null) return;
        var (g1, e1) = shared.First();
        var (g2, e2) = shared.First(x => x.g.Name != g1.Name);
        e1.Container.SetCooldown(e1.Name, 10);
        Assert.True(e2.IsOnCooldown(5));
    }

    // ================================================================== M5-013: sprite faces (C12)

    // ================================================================== M5-015, M5-021, M5-032: the drawer

    /// <summary>
    /// gap1 D1, M5-015: a corner radius below 1 pushes the corner point: the left eye's upper-inner corner (15, −20) lands
    /// on canvas (47, 12), so the straight inner edge runs down column 47 from there (row 12 itself is under the upper lid,
    /// whose polygon at LidY 0 spans y −21..−20, D3); at the nominal 0.5 the radii are round(7.5) = 8 and 10 and the arc
    /// about (7, −10) is at x ≈ 10.5 on row 13, so (47, 13) stays dark.
    /// </summary>
    [Fact]
    public void M5_015_D1_ARadiusBelowOneIsTheCornerPoint()
    {
        var sharp = ProceduralFaceRenderer.Nominal();
        sharp.Left[EyeParam.UpperInnerRadiusX] = 0f;
        var img = new byte[64 * 128];
        ProceduralFaceRenderer.DrawEye(sharp, 0, img, 0);
        Assert.Equal(255, img[13 * 128 + 47]);                            // row 12 is under the upper lid (D3: y -21..-20)
        var round = new byte[64 * 128];
        ProceduralFaceRenderer.DrawEye(ProceduralFaceRenderer.Nominal(), 0, round, 0);
        Assert.Equal(0, round[13 * 128 + 47]);
    }

    /// <summary>
    /// gap3 A4..A9 (stock drawing.cpp): fillConvexPoly LINE_4 draws each edge as a 4-connected line, then fills
    /// [(x1+0x8000)&gt;&gt;16, (x2+0x8000)&gt;&gt;16]. For the triangle (0,0), (4,0), (0,4), worked by hand: the span fill
    /// gives rows 0..3 of 5, 4, 3, 2 pixels and stops at y = 4; the diagonal's staircase (0,4) (1,4) (1,3) (2,3) (2,2)
    /// (3,2) (3,1) (4,1) (4,0) adds the rest: rows of 5, 5, 4, 3, 2.
    /// </summary>
    [Fact]
    public void M5_021_A4_A9_FillConvexPolyLine4()
    {
        var img = new byte[8 * 8];
        OpenCv310.FillConvexPoly(img, 8, 8, new[] { (0, 0), (4, 0), (0, 4) }, 1);
        int[] rows = new int[8];
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) rows[y] += img[y * 8 + x];
        Assert.Equal(new[] { 5, 5, 4, 3, 2, 0, 0, 0 }, rows);
        Assert.Equal(1, img[1 * 8 + 4]);
        Assert.Equal(1, img[2 * 8 + 3]);
        Assert.Equal(0, img[3 * 8 + 3]);
    }

    /// <summary>
    /// gap3 B3: ellipse2Poly rounds with cvRound, ties to even: axes 5 at 30° and 150° give y = 5·0.5 = 2.5 → 2 (roundf
    /// would give 3) and x = ±5·0.8660254 → ±4.
    /// </summary>
    [Fact]
    public void M5_021_B3_Ellipse2PolyRoundsTiesToEven()
    {
        Assert.Equal(new[] { (4, 2), (-4, 2) }, OpenCv310.Ellipse2Poly(0, 0, 5, 5, 0, 30, 150, 120));
        Assert.Equal(2, OpenCv310.CvRound(2.5));
        Assert.Equal(4, OpenCv310.CvRound(3.5));
    }

    /// <summary>gap3 A9, A10: LineIterator conn 4 counts dx + dy + 1; clipLine clips (−2, 0)–(2, 0) to (0, 0)–(2, 0) in a 3 × 3 image.</summary>
    [Fact]
    public void M5_021_A9_A10_LineIteratorAndClipLine()
    {
        var it = new OpenCv310.LineIterator(new byte[25], 5, 5, 0, 0, 3, 2, 4, leftToRight: true);
        Assert.Equal(6, it.Count);
        int x1 = -2, y1 = 0, x2 = 2, y2 = 0;
        Assert.True(OpenCv310.ClipLine(3, 3, ref x1, ref y1, ref x2, ref y2));
        Assert.Equal((0, 0, 2, 0), (x1, y1, x2, y2));
    }

    /// <summary>
    /// E1, E2, gap1 D2..D5: the nominal eye's outline covers rows 12..52 (32 ± 20) and columns 17..47; the lids at LidY 0
    /// cover rows 11..12 (y −21..−20) and 52..53 (20..21); the eye box spans rows 11..53. With _firstScanLine 0 the even
    /// rows in [11, 53) are cleared: rows 13..51 odd stay lit, rows 12, 32 and 52 are dark. With 1 every point moves down a
    /// row (outline 13..53, lids 12..13 and 53..54, box 12..54) and the odd rows in [12, 54) are cleared: rows 14..52 even
    /// stay lit.
    /// </summary>
    [Fact]
    public void M5_032_E1_E2_D4_TheScanLineParity()
    {
        var face = ProceduralFaceRenderer.Nominal();
        var c0 = ProceduralFaceRenderer.DrawFace(face, 0);
        Assert.Equal(0, c0[12 * 128 + 32]);
        Assert.NotEqual(0, c0[13 * 128 + 32]);
        Assert.NotEqual(0, c0[51 * 128 + 32]);
        Assert.Equal(0, c0[52 * 128 + 32]);
        Assert.Equal(0, c0[32 * 128 + 32]);
        Assert.NotEqual(0, c0[33 * 128 + 17]);
        Assert.NotEqual(0, c0[33 * 128 + 47]);
        Assert.Equal(0, c0[33 * 128 + 48]);
        Assert.Equal(0, c0[33 * 128 + 16]);

        var c1 = ProceduralFaceRenderer.DrawFace(face, 1);
        Assert.Equal(0, c1[13 * 128 + 32]);
        Assert.NotEqual(0, c1[14 * 128 + 32]);
        Assert.NotEqual(0, c1[52 * 128 + 32]);
        Assert.Equal(0, c1[53 * 128 + 32]);
        Assert.Equal(0, c1[33 * 128 + 32]);
    }

    /// <summary>
    /// E1, gap3 C1..C6: a face centre of (3, 0) is not the identity, so the canvas goes through warpAffine INTER_NEAREST: the
    /// inverse map is x − 3 (X0 = cvRound(−3·1024) + 512, (1024x − 2560) &gt;&gt; 10 = x − 3), so the eye's columns 17..47
    /// move to 20..50.
    /// </summary>
    [Fact]
    public void M5_032_C1_C6_AFaceTransformGoesThroughWarpAffine()
    {
        var face = ProceduralFaceRenderer.Nominal();
        face.FaceCenterX = 3f;
        var c = ProceduralFaceRenderer.DrawFace(face, 0);
        Assert.Equal(0, c[33 * 128 + 19]);
        Assert.NotEqual(0, c[33 * 128 + 20]);
        Assert.NotEqual(0, c[33 * 128 + 50]);
        Assert.Equal(0, c[33 * 128 + 51]);
    }

    /// <summary>
    /// C2 (0x00585CB6..0x00585D98): the rotated row extent comes from the 4 corners of each eye box. Worked by hand for a
    /// face at 45°, scale 0.5, the left eye raised 10 (its box x 16..48, y 1..43; the right 80..112, 11..53): row 1 of the
    /// matrix is y' = 0.35355338·(y − x) + 43.313708; the lowest corner is the right box's (112, 11) → 7.6 → floor 7, the
    /// highest the left box's (16, 43) → 52.86 → ceil 53. The box around both eyes would have given (4, 57).
    /// </summary>
    [Fact]
    public void M5_032_C2_TheRotatedRowExtentUsesEachEyesCorners()
    {
        var face = ProceduralFaceRenderer.Nominal();
        face.FaceAngle = 45f;
        face.FaceScaleX = face.FaceScaleY = 0.5f;
        face.Left[EyeParam.EyeCenterY] = -10f;
        var img = new byte[64 * 128];
        var l = ProceduralFaceRenderer.DrawEye(face, 0, img, 0);
        var r = ProceduralFaceRenderer.DrawEye(face, 1, img, 0);
        Assert.Equal(new ProceduralFaceRenderer.EyeBox(16, 1, 32, 42), l);
        Assert.Equal(new ProceduralFaceRenderer.EyeBox(80, 11, 32, 42), r);
        var m = ProceduralFaceRenderer.Matrix(45f, 0.5f, 0.5f, 0f, 0f, 64f, 32f);
        Assert.Equal((7, 53), ProceduralFaceRenderer.TransformedRowExtent(m, l, r));
    }

    // ================================================================== M5-016: the backpack track (C16..C18)

    /// <summary>
    /// C17: raw when any of r, g, b &gt; 1, else ×255, truncated; alpha only from a 4th element ≥ 0; default 0xFF00CCFF;
    /// the word ((r&lt;&lt;7)&amp;0x7C00) | ((g&lt;&lt;2)&amp;0x3E0) | (b&gt;&gt;3) | (a ≠ 0 ? 0x8000 : 0). Worked: [1, 0, 0, 1] →
    /// 0xFC00; [255, 128, 8, 0] → 0x7E01; [0.5, 0.5, 0.5] → 127s with the default alpha → 0xBDEF; [] → 0xFC19.
    /// C18: the order Left, Front, Middle, Back, Right.
    /// </summary>
    [Fact]
    public void M5_016_C17_C18_TheColourWordsAndTheOrder()
    {
        Assert.Equal(0xFC00, BackpackColor.Encode(BackpackColor.FromArray(new[] { 1f, 0f, 0f, 1f })));
        Assert.Equal(0x7E01, BackpackColor.Encode(BackpackColor.FromArray(new[] { 255f, 128f, 8f, 0f })));
        Assert.Equal(0xBDEF, BackpackColor.Encode(BackpackColor.FromArray(new[] { 0.5f, 0.5f, 0.5f })));
        Assert.Equal(0xFC19, BackpackColor.Encode(BackpackColor.FromArray(Array.Empty<float>())));

        var red = new[] { 1f, 0f, 0f, 1f };
        var off = new[] { 0f, 0f, 0f, 0f };
        var k = new LightsKeyframe(0, 0, Left: red, Right: off, Front: off, Middle: red, Back: off);
        Assert.Equal(new ushort[] { 0xFC00, 0, 0xFC00, 0, 0 }, k.EncodedLeds);
    }

    /// <summary>
    /// C18: while current the keyframe is sent every frame until counter ≥ duration, that frame too: duration 100 gives 5
    /// frames (counters 0, 33, 66, 99, 132); a second keyframe due at 33 waits behind it (A19) and first goes on frame 5.
    /// </summary>
    [Fact]
    public void M5_016_C18_TheBackpackIsSentEveryFrameWhileCurrent()
    {
        var log = new Log();
        var s = new AnimationScheduler(log);
        var off = new float[4];
        s.Play(Clip("bp", new LightsKeyframe(0, 100, off, off, off, off, off),
                          new LightsKeyframe(33, 0, new[] { 1f, 0f, 0f, 1f }, off, off, off, off),
                          new EventKeyframe(400, "TAPPED_BLOCK")), 0);
        Run(s, 0, 500);
        var frames = log.Frames();
        for (int i = 0; i < 5; i++) Assert.Contains("lights", frames[i]);
        Assert.Equal(new ushort[] { 0, 0, 0, 0, 0 }, log.Lights[4]);
        Assert.Equal(0xFC00, log.Lights[5][0]);
        Assert.Equal(6, log.Lights.Count);
    }

    // ================================================================== M5-019: the stored face (C11)

    /// <summary>C11: only a streaming animation (storeFace = 1) writes its face back as the layer base; an idle animation does not.</summary>
    [Fact]
    public void M5_019_C11_OnlyAStreamingAnimationWritesTheBaseFace()
    {
        var cat = new Catalog();
        cat.Clips["idle"] = Clip("idle", FaceAt(0, 9f), FaceAt(33, 9f), new EventKeyframe(500, "TAPPED_BLOCK"));
        cat.Triggers[AnimationTrigger.AcknowledgeObject] = "g";
        cat.Groups["g"] = new[] { "idle" };
        var s = new AnimationScheduler(new Log()) { Catalog = cat };
        s.Play(Clip("streamed", FaceAt(0, 4f)), 0);
        s.Advance(0);
        Assert.Equal(4f, s.LayerBaseFace.Left[EyeParam.EyeCenterX]);
        s.Advance(33);                                                    // the clip ends (A13)
        s.PushIdleAnimation(AnimationTrigger.AcknowledgeObject, "test");
        Run(s, 66, 300);
        Assert.Equal(4f, s.LayerBaseFace.Left[EyeParam.EyeCenterX]);
    }

    // ================================================================== M5-023: abort (A22..A25)

    /// <summary>
    /// A22, A24, A25: Abort broadcasts AnimationAborted{tag} (tag ≠ 0); startSent and endSent are cleared; the send buffer
    /// and +0xA0 are kept; the next Update flushes the leftovers within the refreshed budget and no EndOfAnimation follows.
    /// </summary>
    [Fact]
    public void M5_023_A22_A24_A25_AbortBroadcastsKeepsTheBufferAndSendsNoEnd()
    {
        var log = new Log { Played = 0 };
        var s = new AnimationScheduler(log);
        var aborted = new List<byte>();
        s.AnimationAborted += aborted.Add;
        s.Play(Clip("x", new EventKeyframe(5_000, "TAPPED_BLOCK")), 0);
        s.Advance(0);
        Assert.True(s.Stream.Count > 0);
        Assert.True(s.Stop());
        Assert.Equal(new byte[] { 1 }, aborted);
        Assert.Equal((false, false), s.StartEndFlags);
        Assert.Equal(1, s.StreamTag);
        Assert.True(s.Stream.Count > 0);
        log.Played = 14;
        s.Advance(60);
        Assert.Equal(0, s.Stream.Count);
        Assert.Equal(15, log.Count("silence"));
        Assert.Equal(0, log.Count("end"));
    }

    /// <summary>A23: the broadcast makes the robot layer send AbortAnimation 0x8D directly (RobotEventHandler → SendAbortAnimation).</summary>
    [Fact]
    public void M5_023_A23_TheRobotLayerSendsAbortAnimation()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Robot.Animations.Scheduler.Play(Clip("x", new EventKeyframe(5_000, "TAPPED_BLOCK")), 0);
        rig.Robot.Animations.Scheduler.Advance(0);
        int before = rig.Port.Messages().Count;
        rig.Robot.Animations.Stop();
        Assert.Single(rig.Port.Messages().Skip(before).OfType<AbortAnimation>());
    }

    /// <summary>A37: ~Robot's AbortAll sends AbortAnimation 0x8D and then StopAllMotors before the streamer goes.</summary>
    [Fact]
    public void M5_022_A37_TeardownSendsAbortAnimationThenStopAllMotors()
    {
        var rig = new Rig();
        rig.ToSuccess();
        int before = rig.Port.Messages().Count;
        rig.Dispose();
        var after = rig.Port.Messages().Skip(before).ToList();
        int abort = after.FindIndex(m => m is AbortAnimation);
        int stop = after.FindIndex(m => m is StopAllMotors);
        Assert.True(abort >= 0 && stop > abort, $"abort {abort}, stop {stop}");
    }

    // ================================================================== M5-024: InitStream (A10..A12)

    /// <summary>A12: endSent = IsEmpty and startSent = 0; nothing is sent by InitStream.</summary>
    [Fact]
    public void M5_024_A12_TheStartAndEndFlags()
    {
        var log = new Log();
        var s = new AnimationScheduler(log);
        s.Play(new AnimationClip { Name = "empty" }, 0);
        Assert.Equal((false, true), s.StartEndFlags);
        s.Play(Clip("x", new EventKeyframe(100, "TAPPED_BLOCK")), 1);
        Assert.Equal((false, false), s.StartEndFlags);
        Assert.Empty(log.L.Where(e => e != "cancelled"));
    }

    // ================================================================== M5-025: the frame (A16, A17, A19)

    /// <summary>
    /// A16: audio, StartOfAnimation, Head, Lift, Event, FaceAnimation, the procedural face, BackpackLights, Body,
    /// RecordHeading (TurnToRecordedHeading's message is MISSING and not sent).
    /// </summary>
    [Fact]
    public void M5_025_A16_TheFrameOrder()
    {
        var log = new Log();
        var s = new AnimationScheduler(log);
        var off = new float[4];
        s.Play(Clip("all", new TurnToRecordedHeadingKeyframe(0, 0, 0, 0, 1000, 1000, 2, 0, false),
                           new RecordHeadingKeyframe(0), new BodyKeyframe(0, 0, "STRAIGHT", 10),
                           new LightsKeyframe(0, 0, off, off, off, off, off), FaceAt(0),
                           new EventKeyframe(0, "TAPPED_BLOCK"), new LiftKeyframe(0, 100, 50, 0), new HeadKeyframe(0, 100, 5, 0)), 0);
        s.Advance(0);
        Assert.Equal(new[] { "silence", "start:1", "head:5:100", "lift:50:100", "event:2", "face", "lights", "body:10:32767", "record" },
                     log.L.Take(9));
    }

    /// <summary>A19: only the current keyframe of a track is considered, so heads due at 0, 5 and 10 go on frames 0, 1 and 2.</summary>
    [Fact]
    public void M5_025_A19_OneKeyframePerTrackPerFrame()
    {
        var log = new Log();
        var s = new AnimationScheduler(log);
        s.Play(Clip("h", new HeadKeyframe(0, 100, 1, 0), new HeadKeyframe(5, 100, 2, 0), new HeadKeyframe(10, 100, 3, 0)), 0);
        Run(s, 0, 200);
        var frames = log.Frames();
        Assert.Contains("head:1:100", frames[0]);
        Assert.Contains("head:2:100", frames[1]);
        Assert.Contains("head:3:100", frames[2]);
    }

    /// <summary>A16 (9): no face goes out without the layered face flag, so the last face is not resent (the robot keeps it).</summary>
    [Fact]
    public void M5_025_A16_NoFaceWithoutTheLayeredFaceFlag()
    {
        var log = new Log();
        var s = new AnimationScheduler(log);
        s.Play(Clip("f", FaceAt(0), new EventKeyframe(300, "TAPPED_BLOCK")), 0);
        Run(s, 0, 400);
        Assert.Single(log.Faces);
    }

    // ================================================================== M5-026: the end (A20, A21)

    /// <summary>
    /// A20: with no frames left and the buffer empty, EndOfAnimation goes directly after the frame; the handle completes
    /// on the next Update (A13) and nothing is sent after the End.
    /// </summary>
    [Fact]
    public void M5_026_A20_TheEndGoesDirectlyAndNothingFollows()
    {
        var log = new Log();
        var s = new AnimationScheduler(log);
        var h = s.Play(Clip("h", new HeadKeyframe(0, 100, 5, 0)), 0)!;
        s.Advance(0);
        Assert.Equal(new[] { "silence", "start:1", "head:5:100", "end" }, log.L);
        Assert.False(h.Completion.IsCompleted);
        s.Advance(33);
        Assert.Equal(AnimationEndReason.Completed, h.Completion.Result);
        Run(s, 66, 400);
        Assert.Equal(new[] { "silence", "start:1", "head:5:100", "end", "finished" }, log.L);
    }

    // ================================================================== M5-027: idle animations (A1, A28..A30)

    /// <summary>
    /// A1, A30: the idle stack starts {Count, "default_anim_lock"}; RemoveIdleAnimation refuses the last entry and an
    /// unknown lock (1); Push then Remove by lock works (0).
    /// </summary>
    [Fact]
    public void M5_027_A1_A30_TheIdleStack()
    {
        var s = new AnimationScheduler(new Log());
        Assert.Equal(new[] { (AnimationScheduler.IdleCount, "default_anim_lock") }, s.IdleStack);
        Assert.Equal(1, s.RemoveIdleAnimation("default_anim_lock", 0));
        s.PushIdleAnimation(AnimationTrigger.AcknowledgeObject, "x");
        Assert.Equal(1, s.RemoveIdleAnimation("nope", 0));
        Assert.Equal(0, s.RemoveIdleAnimation("x", 0));
        Assert.Single(s.IdleStack);
    }

    /// <summary>
    /// A29: a new idle runs InitStream(anim, 0xFF) and builds no frame that Update; the next Update streams it with
    /// StartOfAnimation 0xFF.
    /// </summary>
    [Fact]
    public void M5_027_A29_AnIdleAnimationInitsWithTag0xFfThenStreams()
    {
        var cat = new Catalog();
        cat.Clips["idle"] = Clip("idle", new HeadKeyframe(0, 100, 5, 0), new EventKeyframe(500, "TAPPED_BLOCK"));
        cat.Triggers[AnimationTrigger.AcknowledgeObject] = "g";
        cat.Groups["g"] = new[] { "idle" };
        var log = new Log();
        var s = new AnimationScheduler(log) { Catalog = cat };
        s.PushIdleAnimation(AnimationTrigger.AcknowledgeObject, "test");
        s.Advance(0);
        Assert.Empty(log.L);
        s.Advance(33);
        Assert.Equal(new[] { "silence", "start:255", "head:5:100" }, log.L);
    }

    // ================================================================== M5-028, M5-029: keep-alive and layers

    /// <summary>
    /// A1, A31: no keep-alive before the first stream (+0x88 = −FLT_MAX); after a stream, only once more than 0.5 s has
    /// passed: then KeepFaceAlive adds the "KeepAliveEyeDart" persistent layer and a "Blink" layer (K5, K6), and StreamLayers
    /// opens with StartOfAnimation under a new tag (2, the counter after the clip's 1, A36) and sends no End while layers
    /// exist (Q1.9).
    /// </summary>
    [Fact]
    public void M5_028_A31_Q1_KeepAliveAfterTheFirstStreamAndHalfASecond()
    {
        var log = new Log();
        var s = new AnimationScheduler(log, new Random(3));
        Run(s, 0, 2_000);
        Assert.Empty(log.L);
        Assert.False(s.HaveLayersToSend);

        s.Play(Clip("c", new HeadKeyframe(0, 100, 5, 0)), 2_000);
        s.Advance(2_000);
        s.Advance(2_033);                                                 // completes
        log.L.Clear();
        s.Advance(2_400);
        Assert.False(s.HaveLayersToSend);                                 // 0.4 s: not yet
        s.Advance(2_600);
        Assert.Contains("KeepAliveEyeDart", s.FaceLayerNames);
        Assert.Contains("Blink", s.FaceLayerNames);
        Assert.Equal("start:2", log.L.First(e => e.StartsWith("start")));
        Run(s, 2_633, 4_000);
        Assert.Equal(0, log.Count("end"));
        Assert.True(log.Faces.Count > 20);                                // a face every frame once the dart is due
    }

    /// <summary>
    /// A12, L7, Q1.11: a non-live InitStream runs RemoveKeepFaceAlive(99): the dart layer becomes a "RemoveKeepAliveEyeDart"
    /// layer (a copy of its current keyframe at 0 and a default face at 99).
    /// </summary>
    [Fact]
    public void M5_024_A12_L7_ANonLiveInitStreamRemovesTheDart()
    {
        var s = new AnimationScheduler(new Log(), new Random(3));
        s.Play(Clip("c", new HeadKeyframe(0, 100, 5, 0)), 1_000);
        s.Advance(1_000);
        s.Advance(1_033);
        s.Advance(1_600);
        Assert.Contains("KeepAliveEyeDart", s.FaceLayerNames);
        s.Play(Clip("d", new EventKeyframe(1_000, "TAPPED_BLOCK")), 1_700);
        Assert.DoesNotContain("KeepAliveEyeDart", s.FaceLayerNames);
        Assert.Contains("RemoveKeepAliveEyeDart", s.FaceLayerNames);
        var remove = s.Layers.Face.AllLayers.Single(l => l.Name == "RemoveKeepAliveEyeDart");
        Assert.Equal(new uint[] { 0, 99 }, remove.Track.Frames.Select(f => f.Trigger));
        Assert.Equal(new ProceduralFacePose().Left.ToArray(), remove.Track.Frames[1].Face.Left.ToArray());
    }

    /// <summary>
    /// K7..K9: the blink is 8 keyframes at 33, 66, 99, 132, 165, 198, 298, 331; frame i scales EyeScaleX by widthMul and
    /// EyeScaleY by heightMul of the table; the last is the original face; the closed frame flips the drawer's
    /// _firstScanLine at generation time.
    /// </summary>
    [Fact]
    public void M5_028_K7_K9_TheBlink()
    {
        var scan = new ScanLineState();
        var m = new FaceLayerManager(new EngineRandom(new Random(1)), scan);
        var track = m.GenerateBlink();
        Assert.Equal(new uint[] { 33, 66, 99, 132, 165, 198, 298, 331 }, track.Frames.Select(f => f.Trigger));
        float[] width = { 1.05f, 1.20f, 2.50f, 5.00f, 2.00f, 1.20f, 1.00f, 1f };
        float[] height = { 0.85f, 0.60f, 0.10f, 0.05f, 0.15f, 0.70f, 0.90f, 1f };
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(width[i], track.Frames[i].Face.Left[EyeParam.EyeScaleX], 5);
            Assert.Equal(height[i], track.Frames[i].Face.Right[EyeParam.EyeScaleY], 5);
        }
        Assert.Equal(1, scan.Drawer);
    }

    /// <summary>
    /// K1, K3: the dart keyframe: x and y in [−6, 6], the trigger in [50, 200], a default face through LookAt(x, y, 5, 5,
    /// 1.1, 0.85, 0.1): the centre is (x, y); sY = 0.85 + 0.25·min((5 − y)/10, 1); the eye on the side looked towards
    /// gets (1 + 0.1·min(|x|/5, 1))·sY.
    /// </summary>
    [Fact]
    public void M5_028_K1_K3_TheDartKeyframe()
    {
        var p = new LiveIdleParams();
        p.SetDefaultParams();
        for (int seed = 0; seed < 20; seed++)
        {
            var m = new FaceLayerManager(new EngineRandom(new Random(seed)), new ScanLineState());
            var kf = m.GenerateEyeShift(p);
            float x = kf.Face.FaceCenterX, y = kf.Face.FaceCenterY;
            Assert.InRange(x, -6, 6);
            Assert.InRange(y, -6, 6);
            Assert.InRange(kf.Trigger, 50u, 200u);
            float sY = 0.85f + 0.25f * MathF.Min((5f - y) / 10f, 1f);
            float towards = (1f + 0.1f * MathF.Min(MathF.Abs(x) / 5f, 1f)) * sY;
            float away = (1f - 0.1f * MathF.Min(MathF.Abs(x) / 5f, 1f)) * sY;
            Assert.Equal(x < 0 ? towards : away, kf.Face.Left[EyeParam.EyeScaleY], 4);
            Assert.Equal(x < 0 ? away : towards, kf.Face.Right[EyeParam.EyeScaleY], 4);
            Assert.Equal(y > 0 ? 2f * MathF.Min(y / 5f, 1f) : 0f, kf.Face.Left[EyeParam.EyeCenterX], 4);
        }
    }

    /// <summary>A34: SetDefaultParams, and SetParam(BlinkMax) clamped to 30000.</summary>
    [Fact]
    public void M5_028_A34_TheDefaultParameters()
    {
        var p = new LiveIdleParams();
        p.SetDefaultParams();
        var expected = new float[] { 3000, 4000, 1000, 100, 1000, 250, 1500, 10, 0.5f, 50, 500, 250, 2000, 35, 8, 50, 500, 250, 1000, 6,
                                     250, 1000, 6, 0.92f, 1.08f, 50, 200, 0.1f, 1.1f, 0.85f };
        for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], p[(LiveIdleParam)i]);
        p.SetParam(LiveIdleParam.BlinkSpacingMaxTime_ms, 50_000);
        Assert.Equal(30_000, p[LiveIdleParam.BlinkSpacingMaxTime_ms]);
    }

    /// <summary>
    /// L1..L7: AddLayer returns 0 and takes tags 1, 2, …; a persistent layer that runs out holds its last keyframe with its
    /// time frozen and yields a face every frame (Q1.5); AddToPersistentLayer puts the new keyframe at its trigger + the last
    /// one's + 33; RemovePersistentLayer builds the "Remove" layer.
    /// </summary>
    [Fact]
    public void M5_029_L1_L7_TheLayerManager()
    {
        var m = new FaceLayerManager(new EngineRandom(new Random(1)), new ScanLineState());
        var one = new StreamTrack<FaceFrame>();
        one.AddKeyFrameToBack(FaceFrame.Default(0));
        Assert.Equal(0, m.AddLayer("a", one, 0));
        Assert.Equal(new byte[] { 1 }, m.AllLayers.Select(l => l.Tag));
        byte p = m.AddPersistentLayer("p", one);
        Assert.Equal(2, p);

        var face = new ProceduralFacePose();
        for (int i = 0; i < 5; i++)
        {
            var f = face;
            Assert.True(m.ApplyLayersToFrame((t, st, tm) => TrackLayerComponent.GetFaceHelper(t, st, tm, ref f, replace: false)));
        }
        Assert.Single(m.AllLayers);                                      // "a" ran out and went; "p" holds
        m.AddToPersistentLayer(p, new FaceFrame(50, new ProceduralFacePose()));
        Assert.Equal(new uint[] { 0, 83 }, m.AllLayers.Single().Track.Frames.Select(f => f.Trigger));
        m.RemovePersistentLayer(p, 99);
        Assert.Equal("Removep", Assert.Single(m.AllLayers).Name);
    }

    // ================================================================== M5-031: glitch (G3..G8, Q2)

    /// <summary>
    /// G5: GetNextDistortionFrame steps through 11 entries (the first gives the face a distorter), then removes it and
    /// returns false; the durations are 33 or 66. G6: 2 to 4 control points from 0 to 1, amounts in 1..maxAmt, and 120
    /// noise points for 0.1. G7: Update(0) moves each amount by exactly 1.
    /// </summary>
    [Fact]
    public void M5_031_G5_G7_TheDistorter()
    {
        var track = FaceLayerManager.GenerateFaceDistortion(1f);
        Assert.Equal(12, track.Count);
        Assert.NotNull(track.Frames[0].Face.Distorter);
        Assert.Null(track.Frames[^1].Face.Distorter);
        uint prev = 0;
        foreach (var f in track.Frames) { Assert.Contains(f.Trigger - prev, new uint[] { 33, 66 }); prev = f.Trigger; }

        var d = new ScanlineDistorter(3, 0.1f);
        var pts = d.Points;
        Assert.InRange(pts.Count, 2, 4);
        Assert.Equal(0f, pts[0].Pos);
        Assert.Equal(1f, pts[^1].Pos);
        Assert.All(pts, q => Assert.InRange(q.Amount, 1, 3));
        Assert.Equal(120, d.Noise.Count);
        var before = pts.Select(q => q.Amount).ToArray();
        d.Update(0);
        Assert.All(d.Points.Select((q, i) => Math.Abs(q.Amount - before[i])), v => Assert.Equal(1, v));
    }

    /// <summary>
    /// Q2: GenerateGlitchLights: triggers 0, 200, 260, 320, 380 with durations 200, 60, 60, 60, 60; KF0 all off; KF1 only
    /// Middle red (0xFC00); KF2 Middle and one other red; KF3 Middle off and two others red, the first one kept; KF4 all off.
    /// </summary>
    [Fact]
    public void M5_031_Q2_GenerateGlitchLights()
    {
        var m = new BackpackLayerManager(new EngineRandom(new Random(5)));
        var t = m.GenerateGlitchLights().Frames;
        Assert.Equal(new uint[] { 0, 200, 260, 320, 380 }, t.Select(f => f.Trigger));
        Assert.Equal(new uint[] { 200, 60, 60, 60, 60 }, t.Select(f => f.Duration));
        Assert.All(t[0].Leds, v => Assert.Equal(0, v));
        Assert.Equal(new ushort[] { 0, 0, 0xFC00, 0, 0 }, t[1].Leds);
        Assert.Equal(0xFC00, t[2].Leds[2]);
        Assert.Equal(2, t[2].Leds.Count(v => v == 0xFC00));
        Assert.Equal(0, t[3].Leds[2]);
        Assert.Equal(2, t[3].Leds.Count(v => v == 0xFC00));
        Assert.All(t[4].Leds, v => Assert.Equal(0, v));
        for (int i = 0; i < 5; i++) if (i != 2 && t[2].Leds[i] == 0xFC00) Assert.Equal(0xFC00, t[3].Leds[i]);
    }

    /// <summary>
    /// G3, gap4 B1..B3: AddGlitch adds a face "Glitch" layer and a backpack "Glitch" layer; the backpack layer's current
    /// keyframe overwrites all five LEDs of the frame (B2): KF0 all off through its final frame, then KF1 Middle red.
    /// </summary>
    [Fact]
    public void M5_031_G3_B1_B3_AGlitchStreamsItsFaceAndBackpackLayers()
    {
        var log = new Log();
        var s = new AnimationScheduler(log, new Random(4));
        s.Layers.AddGlitch(1f);
        Assert.Equal(new[] { "Glitch" }, s.FaceLayerNames);
        Assert.Single(s.Layers.Backpack.AllLayers);
        Run(s, 0, 600);                                                   // StreamLayers (A36): the layers stream
        Assert.True(log.Lights.Count >= 9);
        // B3: KF0 (0, 200 ms) is current until its counter reaches 200: layer times 0..231, its final frame included
        Assert.All(log.Lights.Take(8), l => Assert.Equal(new ushort[5], l));
        Assert.Equal(new ushort[] { 0, 0, 0xFC00, 0, 0 }, log.Lights[8]);                 // KF1 at 264
        Assert.True(log.Faces.Count > 5);
    }

    // ================================================================== M5-033: event, record heading, turn to (C15, C19, S2)

    /// <summary>C15, C19, S2: an unknown event rejects the keyframe; TurnTo clamps speed ±300 and accel/decel ±13636 at load.</summary>
    [Fact]
    public void M5_033_C15_C19_S2_EventAndTurnToAtLoad()
    {
        var bad = Load("e", (FEvent, new[] { new T().Set(0, 0u).Set(1, "NOT_AN_EVENT") }));
        Assert.True(bad.LoadTruncated);
        var good = Load("e", (FEvent, new[] { new T().Set(0, 0u).Set(1, "TAPPED_BLOCK") }));
        Assert.Equal(Cozmo.Robot.Animation.AnimEvent.TAPPED_BLOCK, Assert.Single(good.Keyframes.OfType<EventKeyframe>()).Parsed);

        var tt = Assert.Single(Load("t", (FTurnTo, new[]
        {
            new T().Set(0, 0u).Set(1, 100u).Set(3, (short)400).Set(4, (short)20000).Set(5, (short)-20000),
        })).Keyframes.OfType<TurnToRecordedHeadingKeyframe>());
        Assert.Equal((short)300, tt.SpeedDegPerSec);
        Assert.Equal((short)13636, tt.AccelDegPerSec2);
        Assert.Equal((short)-13636, tt.DecelDegPerSec2);
        Assert.Equal((ushort)2, tt.ToleranceDeg);
    }

    // ================================================================== M5-034: the trigger map (gap1 C7)

    /// <summary>gap1 C7: every *.json with "Pairs" under the directory, the first CladEvent entry wins; unknown gives "" with a warning.</summary>
    [Fact]
    public void M5_034_C7_TheTriggerMap()
    {
        var dir = Directory.CreateTempSubdirectory("m5maps");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "a.json"),
                "{\"Pairs\":[{\"CladEvent\":\"AcknowledgeObject\",\"AnimName\":\"first\"},{\"CladEvent\":\"AcknowledgeObject\",\"AnimName\":\"second\"}]}");
            File.WriteAllText(Path.Combine(dir.FullName, "b.json"), "{\"NotPairs\":[]}");
            var map = AnimationTriggerResponses.Load(dir.FullName);
            var logs = new List<string>();
            map.Log = logs.Add;
            Assert.Equal("first", map.GetResponse(AnimationTrigger.AcknowledgeObject));
            Assert.Equal("", map.GetResponse(AnimationTrigger.BlockReact));
            Assert.Contains(logs, l => l.Contains("Animation requested for unknown response 'BlockReact'"));
        }
        finally { dir.Delete(recursive: true); }
    }

    // ================================================================== queued M5 items (PROJECT_STATE "For the M5 batch")

    /// <summary>
    /// A29, 0x0057D080: StreamLive appends to the live animation only; the Updates stream it: the first re-inits the live
    /// idle (InitStream(live, 0xFF), no frame), the next sends silence, StartOfAnimation 0xFF and the keyframe, and, the
    /// keyframe consumed, the End.
    /// </summary>
    [Fact]
    public void M5_027_A29_StreamLiveKeyframesGoOutInsideTheUpdate()
    {
        var log = new Log();
        var s = new AnimationScheduler(log);
        Assert.True(s.StreamLive(new HeadKeyframe(0, 100, 5, 0), 0));
        Assert.Empty(log.L);
        s.Advance(0);
        Assert.Empty(log.L);
        s.Advance(60);
        Assert.Equal(new[] { "silence", "start:255", "head:5:100", "end" }, log.L);
    }

    /// <summary>
    /// A5, A9, A12, A13: a Play and a Stop inside one tick, with the live idle running: Play aborts the idle (AnimationAborted
    /// 0xFF) and InitStreams the clip (tag 1), Stop aborts it (AnimationAborted 1); +0x64 is still set, so the next Update
    /// continues the live animation rather than re-initialising it, and its StartOfAnimation carries +0xA0 = 1.
    /// </summary>
    [Fact]
    public void M5_027_A13_APlayThenStopInOneTickContinuesTheLiveStream()
    {
        var log = new Log();
        var s = new AnimationScheduler(log);
        var aborted = new List<byte>();
        s.AnimationAborted += aborted.Add;
        s.StreamLive(new BodyKeyframe(0, 2_000, "STRAIGHT", 10), 0);
        s.Advance(0);
        s.Advance(60);
        Assert.Contains("start:255", log.L);
        log.L.Clear();
        s.Play(Clip("x", new EventKeyframe(5_000, "TAPPED_BLOCK")), 100);
        s.Stop();
        Assert.Equal(new byte[] { 0xFF, 1 }, aborted);
        s.Advance(120);
        Assert.Equal("start:1", log.L.First(e => e.StartsWith("start")));
        Assert.DoesNotContain("start:255", log.L);
    }

    /// <summary>A20, A24: no BodyStop is added on a cancel or at the end: only the keyframe's own stop (C5) is sent.</summary>
    [Fact]
    public void M5_026_A20_A24_NoBodyStopBeyondTheKeyframesOwn()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        var sch = rig.Robot.Animations.Scheduler;
        int before = rig.Port.Messages().Count;
        sch.Play(Clip("drive", new BodyKeyframe(0, 2_000, "STRAIGHT", 40), new EventKeyframe(3_000, "TAPPED_BLOCK")), 0);
        sch.Advance(0);
        sch.Stop();
        Assert.Single(rig.Port.Messages().Skip(before).OfType<BodyMotion>());

        before = rig.Port.Messages().Count;
        sch.Play(Clip("short", new BodyKeyframe(0, 33, "STRAIGHT", 40)), 10);
        for (int i = 0; i < 5; i++) sch.Advance(10 + 33 * i);
        var body = rig.Port.Messages().Skip(before).OfType<BodyMotion>().ToList();
        Assert.Equal(2, body.Count);                                      // the start and the keyframe's stop
        Assert.Equal((0, (short)0x7FFF), (body[1].Speed, body[1].RadiusMm));
    }

    /// <summary>
    /// C14: a failed send stays at the front, is not counted, and is retried at the next Update; AudioFramesSent and
    /// KeyframeFired count it once, when it goes. A send that throws is a failed send, not a fault.
    /// </summary>
    [Fact]
    public void M5_007_C14_AFailedOrThrowingSendIsRetriedAndCountedOnce()
    {
        var sink = new FlakySink();
        var s = new AnimationScheduler(sink);
        int fired = 0;
        s.KeyframeFired += _ => fired++;
        s.Play(Clip("h", new HeadKeyframe(0, 100, 5, 0), new EventKeyframe(500, "TAPPED_BLOCK")), 0);
        sink.FailNext = 1;
        s.Advance(0);
        Assert.Equal(0, s.AudioFramesSent);
        sink.ThrowOnHead = true;
        s.Advance(33);
        Assert.Equal(1, s.AudioFramesSent);
        Assert.Equal(0, fired);
        sink.ThrowOnHead = false;
        s.Advance(66);
        Assert.Equal(1, fired);
        Assert.Equal(1, sink.Heads);
    }

    private sealed class FlakySink : IAnimationSink
    {
        public int FailNext, Heads;
        public bool ThrowOnHead;
        public bool LastSendSucceeded { get; private set; } = true;
        public void Face(FaceBitmap bitmap) { }
        public void Audio(byte[]? mulawFrame) { LastSendSucceeded = FailNext-- <= 0; }
        public void Head(sbyte angleDeg, uint durationMs)
        {
            if (ThrowOnHead) throw new InvalidOperationException("the robot went away");
            LastSendSucceeded = true;
            Heads++;
        }
        public void Lift(byte heightMm, uint durationMs) { }
        public void AnimationStarted(byte tag) => LastSendSucceeded = true;
        public void AnimationEnded() => LastSendSucceeded = true;
        public void Body(BodyKeyframe keyframe) { }
        public void BodyStop() { }
        public void Lights(LightsKeyframe keyframe) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }

    /// <summary>
    /// C9, C14, A18 on the production wiring (the engine's own tick): with the robot reporting nothing played, the stream
    /// stops at 14 audio frames; when it reports 14 played, the next ticks refill to 28.
    /// </summary>
    [Fact]
    public void M3_013_C9_A18_TheProductionStreamStopsAtTheBudgetAndRefills()
    {
        var port = new FakePort();
        using var robot = CozmoRobot.CreateForTest(port, EngineTickRunner.HostNowNs, new CozmoEngineOptions { BlockPoolPath = "" });
        robot.Engine.StartProduction();
        void Data(RobotMessage m) => port.Raise(ReceiverMarker.Data, Rig.RobotEp, m.ToBytes());
        robot.Engine.ConnectToRobot(Rig.RobotIp);
        Assert.True(SpinWait.SpinUntil(() => robot.Engine.ConnectionState == 1, 3000));
        port.Raise(ReceiverMarker.OnConnected, Rig.RobotEp);
        Assert.True(SpinWait.SpinUntil(() => robot.Engine.ConnectionState == 2, 3000));
        Data(new RobotAvailable { SerialNumberHead = 0x1234, HwVersion = 5 });
        Data(new FirmwareVersion { RobotId = 1, Signature = Encoding.UTF8.GetBytes(Rig.ShippedFw) });
        Assert.True(SpinWait.SpinUntil(() => port.Messages().Any(m => m is GetManufacturingInfo), 3000));
        Data(new ManufacturingID { SerialNumber = 0xABCD, BodyHwVersion = 7, BodyColor = 2 });
        Assert.True(SpinWait.SpinUntil(() => port.Messages().Any(m => m is SyncTime), 3000));
        // CD20: ready to stream waits for the NV queue to drain; answer the calibration read so streaming opens.
        Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = -1, Length = 0, Data = Array.Empty<byte>() });
        Data(new SyncTimeAck());
        Data(new RobotState { Timestamp = 10, PoseOriginId = 1 });
        Assert.True(SpinWait.SpinUntil(() => robot.AnimationStreamingOpen, 3000));

        int before = port.Messages().Count;
        int Audio() => port.Messages().Skip(before).Count(m => m is AudioSample or AudioSilence);
        robot.Animations.Play(Clip("long", new EventKeyframe(10_000, "TAPPED_BLOCK")));
        Assert.True(SpinWait.SpinUntil(() => Audio() >= 14, 3000));
        Thread.Sleep(300);                                                // several engine ticks with nothing played
        Assert.Equal(14, Audio());
        Data(new AnimationState { Timestamp = 11, NumAudioFramesPlayed = 14, NumAnimBytesPlayed = 0, Tag = 1 });
        Assert.True(SpinWait.SpinUntil(() => Audio() >= 28, 3000));
        Thread.Sleep(300);
        Assert.Equal(28, Audio());
        robot.Animations.Stop();
    }

    /// <summary>
    /// B3 (0x0057D428..0x0057D43E): the live idle's UpdateStream sets +0x88 = now, so with nothing streamed before, the keep-alive
    /// block (A31, idle == live) runs from the Update after the first live frame: its layers appear.
    /// </summary>
    [Fact]
    public void M5_027_B3_AnIdleStreamSetsTheLastStreamTime()
    {
        var s = new AnimationScheduler(new Log(), new Random(2));
        s.StreamLive(new BodyKeyframe(0, 2_000, "STRAIGHT", 10), 0);
        s.Advance(1_000);                                                 // InitStream(live): no frame, +0x88 not set
        s.Advance(1_033);                                                 // UpdateStream(live): +0x88 = 1.033 s
        Assert.False(s.HaveLayersToSend);
        s.Advance(1_066);
        Assert.Contains("KeepAliveEyeDart", s.FaceLayerNames);
        Assert.Contains("Blink", s.FaceLayerNames);
    }

    /// <summary>
    /// C4 (0x0057D40C..0x0057D412): a streaming Update clears +0x64 (A13), so once a clip played over the live idle has
    /// ended, the live idle is re-initialised (InitStream(live, 0xFF)) although it is still the same idle with frames left,
    /// and its next frame carries StartOfAnimation 0xFF again.
    /// </summary>
    [Fact]
    public void M5_027_C4_AfterAClipTheLiveIdleIsReinitialisedWithTag255()
    {
        var log = new Log();
        var s = new AnimationScheduler(log, new Random(3));
        s.StreamLive(new BodyKeyframe(0, 20_000, "STRAIGHT", 10), 0);
        long t = 1_000;
        for (int i = 0; i < 4; i++, t += 33) s.Advance(t);
        Assert.Equal(1, log.Count("start:255"));
        s.Play(Clip("x", new HeadKeyframe(0, 100, 5, 0)), 0);
        for (int i = 0; i < 30 && log.Count("end") == 0; i++, t += 33) s.Advance(t);
        int end = log.L.IndexOf("end");
        Assert.True(end >= 0);
        for (int i = 0; i < 4; i++, t += 33) s.Advance(t);
        Assert.Contains("start:255", log.L.Skip(end + 1));
    }

    /// <summary>
    /// B1 (0x0057D03A..0x0057D04C): with a top other than Count the no-animation path goes straight to the idle and neither
    /// flushes nor sends an End; the idle's InitStream drops the leftovers (A12).
    /// </summary>
    [Fact]
    public void M5_027_B1_WithAnIdleOnTopTheLeftoversAreNotFlushed()
    {
        var cat = new Catalog();
        cat.Clips["idle"] = Clip("idle", new HeadKeyframe(0, 100, 5, 0), new EventKeyframe(500, "TAPPED_BLOCK"));
        cat.Triggers[AnimationTrigger.AcknowledgeObject] = "g";
        cat.Groups["g"] = new[] { "idle" };
        var log = new Log { Played = 0 };
        var s = new AnimationScheduler(log) { Catalog = cat };
        s.Play(Clip("x", new EventKeyframe(5_000, "TAPPED_BLOCK")), 0);
        s.Advance(0);
        Assert.True(s.Stream.Count > 0);
        s.Stop();
        s.PushIdleAnimation(AnimationTrigger.AcknowledgeObject, "test");
        log.Played = 100;
        log.L.Clear();
        s.Advance(60);
        Assert.Empty(log.L);
        Assert.Equal(0, s.Stream.Count);
    }

    /// <summary>
    /// gap4 L2..L8: the ProceduralLive idle generates its own keyframes. With the default parameters, no keyframe until
    /// +0x44 ≥ TimeBeforeWiggle 1000 (the 18th idle Update, +0x44 = 17·60 = 1020); then body, lift and head keyframes in one
    /// Update, streamed in that Update under StartOfAnimation 0xFF: the head angle truncated, (s8)trunc(0.5 rad·57.2958) = 28
    /// (a round would give 29), the lift at the mean 35 (both variabilities set to 0 here), the body at ±10 mm/s for
    /// 250..1500 ms.
    /// </summary>
    [Fact]
    public void M5_030_L1_L8_TheLiveIdleGeneratesItsKeyframes()
    {
        var log = new Log();
        var s = new AnimationScheduler(log, new Random(8));
        s.LiveIdleParameters.SetDefaultParams();
        s.LiveIdleParameters[LiveIdleParam.HeadAngleVariability_deg] = 0;
        s.LiveIdleParameters[LiveIdleParam.LiftHeightVariability_mm] = 0;
        s.LiveIdleInputs.HeadAngleRad = () => 0.5f;
        s.PushIdleAnimation(AnimationTrigger.ProceduralLive, "test");
        for (int i = 0; i < 17; i++) s.Advance(60 * i);
        Assert.Empty(log.L);
        s.Advance(60 * 17);
        Assert.Contains("start:255", log.L);
        Assert.Contains("head:28:", log.L.Single(e => e.StartsWith("head:")));
        Assert.StartsWith("lift:35:", log.L.Single(e => e.StartsWith("lift:")));
        var body = log.L.Single(e => e.StartsWith("body:"));
        Assert.InRange(int.Parse(body.Split(':')[1]), -10, 10);
    }

    /// <summary>
    /// gap4 T1, T2: TurnToRecordedHeading goes as 0x92 at counter 0, 13 bytes: s16 offset, s16 speed, s16 accel, s16 decel,
    /// u16 tolerance, u16 numHalfRevs, u8 useShortestDir; the track is then held for the duration.
    /// </summary>
    [Fact]
    public void M5_033_T1_T2_TurnToRecordedHeadingBytes()
    {
        var k = new TurnToRecordedHeadingKeyframe(0, 100, -90, 200, 1000, -1000, 2, 1, true);
        var bytes = RobotAnimationSink.ToMessage(k).ToBytes();
        Assert.Equal(new byte[] { 0x92, 0xA6, 0xFF, 0xC8, 0x00, 0xE8, 0x03, 0x18, 0xFC, 0x02, 0x00, 0x01, 0x00, 0x01 }, bytes);

        using var rig = new Rig();
        rig.ToSuccess();
        int before = rig.Port.Messages().Count;
        var sch = rig.Robot.Animations.Scheduler;
        sch.Play(Clip("t", k, new EventKeyframe(400, "TAPPED_BLOCK")), 0);
        for (int i = 0; i < 6; i++) sch.Advance(33 * i);
        Assert.Single(rig.Port.Messages().Skip(before).OfType<Protocol.TurnToRecordedHeading>());
    }

    /// <summary>
    /// gap4 R1, C3: mt19937 seeded 5489 gives 3499211612 then 581869302 (the standard sequence); GetNextDbl is
    /// (d0 + d1·2^32)·2^−64.
    /// </summary>
    [Fact]
    public void M5_005_R1_Mt19937AndGetNextDbl()
    {
        var mt = new Mt19937(5489);
        Assert.Equal(3499211612u, mt.Next());
        Assert.Equal(581869302u, mt.Next());
        var r = new EngineRandom(5489u);
        Assert.Equal((3499211612.0 + 581869302.0 * 4294967296.0) / 18446744073709551616.0, r.GetNextDbl());
    }

    // ------------------------------------------------------------------ a robot on a fake port

    private sealed class FakePort : IEngineTransport
    {
        public readonly List<byte[]> Sent = new();
        public bool TimedOut { get; set; }
        public event Action<ReceiverEvent>? Received;
        public void Start() { }
        public void Connect(IPAddress ip, bool isSimulated) { }
        public void Disconnect(IPEndPoint address) { }
        public void SendData(byte[] clad) { lock (Sent) Sent.Add(clad); }
        public void Raise(ReceiverMarker m, IPEndPoint? a, byte[]? d = null) => Received?.Invoke(new ReceiverEvent(m, a, d));
        public List<RobotMessage> Messages() { lock (Sent) return Sent.Select(b => RobotMessage.Parse(b)).ToList(); }
    }

    private sealed class Rig : IDisposable
    {
        public long NowNs = 1_000_000_000;
        public readonly FakePort Port = new();
        public readonly CozmoRobot Robot;
        public CozmoEngine Engine => Robot.Engine;
        public static readonly IPAddress RobotIp = IPAddress.Parse("172.31.1.1");
        public static readonly IPEndPoint RobotEp = new(RobotIp, 5551);
        public const string ShippedFw = "{\"version\": 2381, \"time\": 1546972025, \"build\": \"DEVELOPMENT\"}";

        public Rig() => Robot = CozmoRobot.CreateForTest(Port, () => NowNs, new CozmoEngineOptions { BlockPoolPath = "" });
        public void Tick() { NowNs += 60_000_000; Engine.Tick(); }
        public void Data(RobotMessage m) => Port.Raise(ReceiverMarker.Data, RobotEp, m.ToBytes());

        public void ToSuccess()
        {
            Engine.ConnectToRobot(RobotIp);
            Tick();
            Port.Raise(ReceiverMarker.OnConnected, RobotEp);
            Tick();
            Data(new RobotAvailable { SerialNumberHead = 0x1234, HwVersion = 5 });
            Data(new FirmwareVersion { RobotId = 1, Signature = Encoding.UTF8.GetBytes(ShippedFw) });
            Tick();
            Data(new ManufacturingID { SerialNumber = 0xABCD, BodyHwVersion = 7, BodyColor = 2 });
            Tick();
        }

        public void Dispose() => Robot.Dispose();
    }
}

[CollectionDefinition("M5 process statics", DisableParallelization = true)]
public class M5ProcessStaticsCollection { }

/// <summary>
/// The two _firstScanLine values are process statics (M3 B3; M5 fix B8), toggled by every streamer's InitStream, so the
/// tests that read them run alone and assert relative to the values they found.
/// </summary>
[Collection("M5 process statics")]
public class M5ScanLineTests
{
    private sealed class Log : IAnimationSink
    {
        public readonly List<byte[]> Faces = new();
        public void Face(FaceBitmap bitmap) { }
        public void FaceImage(byte[] payload) => Faces.Add(payload);
        public void Audio(byte[]? mulawFrame) { }
        public void Head(sbyte angleDeg, uint durationMs) { }
        public void Lift(byte heightMm, uint durationMs) { }
        public void AnimationStarted(byte tag) { }
        public void AnimationEnded() { }
        public void Body(BodyKeyframe keyframe) { }
        public void BodyStop() { }
        public void Lights(LightsKeyframe keyframe) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }

    private static AnimationClip Clip(string name, params Keyframe[] frames)
    {
        var list = frames.OrderBy(f => f.TriggerTimeMs).ToList();
        AnimationTrack tracks = 0;
        uint end = 0;
        foreach (var f in list) { tracks |= f.Track; end = Math.Max(end, f.EndTimeMs); }
        return new AnimationClip { Name = name, Keyframes = list, Tracks = tracks, DurationMs = end };
    }

    private static void Run(AnimationScheduler s, double from, double to)
    {
        for (double t = from; t <= to; t += 33) s.Advance(t);
    }

    /// <summary>
    /// A11, gap4 E1: both values toggle when the name differs from the last InitStream's, or when now + GetLastKeyFrameEndTime
    /// − the last toggle &gt; 30000; not for the same name within that. E1 for this clip is the event's trigger, 100.
    /// </summary>
    [Fact]
    public void M5_024_A11_E1_TheScanLineToggleRule()
    {
        var s = new AnimationScheduler(new Log());
        var a = Clip("m5-a11-" + Guid.NewGuid(), new EventKeyframe(100, "TAPPED_BLOCK"));
        var (d0, f0) = s.FirstScanLines;
        s.Play(a, 0);
        Assert.Equal((1 - d0, 1 - f0), s.FirstScanLines);                // a new name; +0x1D4 = 0
        s.Play(a, 29_900);
        Assert.Equal((1 - d0, 1 - f0), s.FirstScanLines);                // 29900 + 100 − 0 = 30000: not above
        s.Play(a, 29_901);
        Assert.Equal((d0, f0), s.FirstScanLines);                        // 30001 > 30000
        s.Play(Clip("m5-a11-other-" + Guid.NewGuid(), new EventKeyframe(100, "TAPPED_BLOCK")), 29_902);
        Assert.Equal((1 - d0, 1 - f0), s.FirstScanLines);
    }

    /// <summary>gap4 E1: the unsigned max of each non-empty track's last trigger + duration (Head, Lift, Body, TurnTo, Backpack) or trigger.</summary>
    [Fact]
    public void M5_024_E1_GetLastKeyFrameEndTime()
    {
        var clip = Clip("e1", new HeadKeyframe(0, 500, 1, 0), new HeadKeyframe(100, 50, 1, 0), new EventKeyframe(400, "TAPPED_BLOCK"),
                        new BodyKeyframe(10, 300, "STRAIGHT", 1));
        Assert.Equal(400u, StreamAnimation.Of(clip).LastKeyFrameEndTime);   // head last: 150; body: 310; event: 400
    }

    /// <summary>
    /// C12: each stored image has two variants, even rows cleared and odd rows cleared; the track sends the variant for the
    /// FaceAnimationManager's _firstScanLine (toggled by the InitStream of a new name, A11), one per frame.
    /// </summary>
    [Fact]
    public void M5_013_C12_TwoVariantsChosenByTheScanLine()
    {
        var lit = Enumerable.Repeat((byte)255, 64 * 128).ToArray();
        var frame = FaceAnimationFrame.FromCanvas(lit);
        var even = FaceBitmapCodec.DecodeCanvas(frame.EvenRowsCleared);
        var odd = FaceBitmapCodec.DecodeCanvas(frame.OddRowsCleared);
        Assert.Equal(0, even[0]);
        Assert.NotEqual(0, even[128]);
        Assert.NotEqual(0, odd[0]);
        Assert.Equal(0, odd[128]);

        var log = new Log();
        var s = new AnimationScheduler(log) { FaceAnimationVariants = _ => new[] { frame, frame } };
        s.Play(Clip("sprite-" + Guid.NewGuid(), new FaceAnimationKeyframe(0, "anim")), 0);
        int fsl = s.FirstScanLines.FaceAnimation;
        Run(s, 0, 100);
        Assert.Equal(2, log.Faces.Count);
        Assert.All(log.Faces, p => Assert.Equal(frame.ForScanLine(fsl), p));
    }
}
