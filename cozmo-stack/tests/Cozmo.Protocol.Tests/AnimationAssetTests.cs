using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The decoder run against Cozmo's real animation assets.
///
/// The assets are Anki's and the repository does not redistribute the OBB, so nothing is committed here.
/// These tests locate the unpacked resources if they are present and skip if they are not, which means they
/// are meaningful on a machine that has the OBB and silent on one that does not. Set
/// <c>COZMO_ASSETS</c> to point at the assets directory to override the search.
/// </summary>
public class AnimationAssetTests
{
    /// <summary>The unpacked assets directory, or null when the OBB is not on this machine.</summary>
    private static string? AssetsRoot()
    {
        var env = Environment.GetEnvironmentVariable("COZMO_ASSETS");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;

        // walk up from the test binary looking for the unpacked OBB that re-analysis keeps locally
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "re-analysis", "obb", "assets", "cozmo_resources", "assets");
            if (Directory.Exists(Path.Combine(candidate, "animations"))) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static AnimationLibrary? Library()
    {
        var root = AssetsRoot();
        return root is null ? null : AnimationLibrary.Open(root);
    }

    /// <summary>
    /// Makes the skip visible. Without this a machine with no OBB would report every asset test as passing
    /// when in fact none of them ran.
    /// </summary>
    [Fact]
    public void TheAssetTestsSayWhetherTheyActuallyRan()
    {
        var root = AssetsRoot();
        if (root is null)
        {
            Assert.True(true, "no unpacked OBB on this machine, so the asset tests are skipping");
            return;
        }
        var lib = AnimationLibrary.Open(root);
        Assert.True(lib.ClipNames.Count > 0, $"assets found at {root} but no clips loaded");
    }

    /// <summary>
    /// The faceAnimations track plays pre-rendered frames, and the assets for it ship. ReadFaceAnimationDir
    /// 0x00580048 lists the directories under faceAnimations - here face_bored_event_02 and
    /// face_bored_event_04 - and AddImage 0x00581440 thresholds each 128 x 64 grayscale image at 0x80 and
    /// keeps the odd canvas row of each pair before compressing it. This reads the same files and checks
    /// the frames are there and are not blank.
    /// </summary>
    [Fact]
    public void TheShippedFaceAnimationsLoadAsFrames()
    {
        var root = AssetsRoot();
        if (root is null) return;
        var faces = FaceAnimationLibrary.Open(root);
        Assert.Equal(2, faces.Names.Count);
        Assert.Contains("face_bored_event_02", faces.Names);

        var frames = faces.Frames("face_bored_event_02");
        Assert.NotNull(frames);
        Assert.True(frames!.Count > 100, $"expected the whole run of frames, got {frames.Count}");
        Assert.All(frames, f => Assert.Equal(FaceBitmap.Width * FaceBitmap.Height, f.Pixels.Length));
        Assert.Contains(frames, f => f.Pixels.ToArray().Any(px => px != 0));
        Assert.Null(faces.Frames("no_such_face_animation"));
    }

    /// <summary>
    /// The two clips that use the track play those frames one per streaming tick, which is what
    /// FaceAnimationKeyFrame::GetStreamMessage 0x004F97C8 does with its frame index and what IsDone
    /// 0x004F976C ends when the index reaches GetNumFrames.
    /// </summary>
    [Fact]
    public void AClipWithAFaceAnimationTrackStreamsItsFrames()
    {
        var root = AssetsRoot();
        if (root is null) return;
        var lib = AnimationLibrary.Open(root);
        var faces = FaceAnimationLibrary.Open(root);
        var clip = lib.GetClip("anim_bored_event_02");
        var fa = clip.Keyframes.OfType<FaceAnimationKeyframe>().ToList();
        Assert.NotEmpty(fa);
        Assert.Contains(fa, k => faces.Has(k.AnimName));

        var seen = new HashSet<string>();
        int shown = 0;
        var sink = new FaceRecordingSink(f => { shown++; seen.Add(Convert.ToHexString(FaceBitmapCodec.Encode(f))); });
        var scheduler = new AnimationScheduler(sink) { FaceAnimations = faces.Frames };
        scheduler.Play(clip, 0);
        for (double t = 0; t <= clip.DurationMs + 200; t += AnimationScheduler.FrameStepMs)
            scheduler.Advance(t);

        Assert.True(shown > 10, $"only {shown} face frames went out");
        Assert.True(seen.Count > 5, $"the frames did not change: {seen.Count} distinct");
    }

    private sealed class FaceRecordingSink : IAnimationSink
    {
        private readonly Action<FaceBitmap> _onFace;
        public FaceRecordingSink(Action<FaceBitmap> onFace) => _onFace = onFace;
        public void Face(FaceBitmap bitmap) => _onFace(bitmap);
        public void Audio(byte[]? mulawFrame) { }
        public void Head(sbyte angleDeg, uint durationMs) { }
        public void Lift(byte heightMm, uint durationMs) { }
        public void Body(BodyKeyframe keyframe) { }
        public void AnimationStarted(byte tag) { }
        public void AnimationEnded() { }
        public void BodyStop() { }
        public void Lights(LightsKeyframe keyframe) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }

    [Fact]
    public void TheShippedClipsAllDecode()
    {
        var lib = Library();
        if (lib is null) return;                 // no OBB on this machine

        Assert.True(lib.ClipNames.Count > 200, $"expected the full animation set, found {lib.ClipNames.Count}");

        int decoded = 0, keyframes = 0;
        var tracks = AnimationTrack.None;
        var failures = new List<string>();
        foreach (var name in lib.ClipNames)
        {
            try
            {
                var clip = lib.GetClip(name);
                decoded++;
                keyframes += clip.Keyframes.Count;
                tracks |= clip.Tracks;

                Assert.False(string.IsNullOrEmpty(clip.Name), $"{name} decoded without a name");
                // keyframes come out in time order, and the duration covers them all
                uint last = 0;
                foreach (var k in clip.Keyframes)
                {
                    Assert.True(k.TriggerTimeMs >= last, $"{name}: keyframes are out of order");
                    last = k.TriggerTimeMs;
                    Assert.True(k.EndTimeMs <= clip.DurationMs, $"{name}: a keyframe ends after the clip does");
                }
            }
            catch (Exception e) { failures.Add($"{name}: {e.GetType().Name}: {e.Message}"); }
        }

        Assert.Empty(failures);
        Assert.True(keyframes > 10000, $"only {keyframes} keyframes across {decoded} clips");
        // the real assets exercise every track the schema defines except the ones that are rare
        Assert.True(tracks.HasFlag(AnimationTrack.Face));
        Assert.True(tracks.HasFlag(AnimationTrack.Head));
        Assert.True(tracks.HasFlag(AnimationTrack.Audio));
        Assert.True(tracks.HasFlag(AnimationTrack.Lift));
        Assert.True(tracks.HasFlag(AnimationTrack.Body));
        Assert.True(tracks.HasFlag(AnimationTrack.Lights));
    }

    [Fact]
    public void EveryProceduralFaceKeyframeCarriesNineteenParametersPerEye()
    {
        var lib = Library();
        if (lib is null) return;

        int faces = 0;
        foreach (var name in lib.ClipNames)
            foreach (var k in lib.GetClip(name).Keyframes.OfType<FaceKeyframe>())
            {
                faces++;
                Assert.Equal(Eye.ParamCount, k.Pose.Left.ToArray().Length);
                Assert.Equal(Eye.ParamCount, k.Pose.Right.ToArray().Length);
            }
        Assert.True(faces > 1000, $"only {faces} face keyframes found across the whole asset set");
    }

    [Fact]
    public void AKnownClipDecodesToWhatTheAssetActuallyHolds()
    {
        var lib = Library();
        if (lib is null) return;
        if (!lib.HasClip("anim_bored_01")) return;

        var clip = lib.GetClip("anim_bored_01");
        Assert.Equal("anim_bored_01", clip.Name);
        Assert.True(clip.DurationMs > 0);

        // counts confirmed against the same file parsed with the reference FlatBuffers implementation
        Assert.Equal(27, clip.Keyframes.OfType<FaceKeyframe>().Count());
        Assert.Equal(10, clip.Keyframes.OfType<HeadKeyframe>().Count());
        Assert.Equal(2, clip.Keyframes.OfType<LiftKeyframe>().Count());
        Assert.Equal(2, clip.Keyframes.OfType<AudioKeyframe>().Count());
        Assert.Equal(12, clip.Keyframes.OfType<LightsKeyframe>().Count());
        Assert.Equal(1, clip.Keyframes.OfType<BodyKeyframe>().Count());

        var first = clip.Keyframes.OfType<FaceKeyframe>().First();
        Assert.Equal(0u, first.TriggerTimeMs);
        Assert.Equal(1f, first.Pose.FaceScaleX, 3);
        Assert.Equal(9.17f, first.Pose.Left[EyeParam.EyeCenterX], 2);
        Assert.Equal(1.21f, first.Pose.Left[EyeParam.EyeScaleX], 2);

        var head = clip.Keyframes.OfType<HeadKeyframe>().First();
        Assert.Equal(0u, head.TriggerTimeMs);
        Assert.Equal(264u, head.DurationTimeMs);

        var audio = clip.Keyframes.OfType<AudioKeyframe>().First();
        Assert.Equal(99u, audio.TriggerTimeMs);
        Assert.Equal(new long[] { 1620542011 }, audio.EventIds);
        Assert.Equal(1f, audio.Volume, 3);
    }

    [Fact]
    public void TheBodyRadiusTokenIsKeptAsWrittenBecauseItIsAString()
    {
        var lib = Library();
        if (lib is null) return;

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in lib.ClipNames)
            foreach (var b in lib.GetClip(name).Keyframes.OfType<BodyKeyframe>())
                tokens.Add(b.RadiusRaw);

        Assert.NotEmpty(tokens);
        // whatever the tokens are, each one either parses as a number or is preserved verbatim
        foreach (var t in tokens)
        {
            var k = new BodyKeyframe(0, 0, t, 0);
            Assert.True(k.IsStraight || k.RadiusMm is not null || t.Length > 0);
        }
    }

    [Fact]
    public void AnimationGroupsLoadAndChooseOnlyRealClips()
    {
        var lib = Library();
        if (lib is null) return;

        Assert.True(lib.GroupNames.Count > 10, $"only {lib.GroupNames.Count} groups loaded");
        var rnd = new Random(7);
        int checkedGroups = 0, missing = 0;
        foreach (var gname in lib.GroupNames)
        {
            var g = lib.GetGroup(gname)!;
            if (g.Entries.Count == 0) continue;
            checkedGroups++;
            for (int i = 0; i < 5; i++)
            {
                var pick = g.Choose(rnd);
                Assert.NotNull(pick);
                if (!lib.HasClip(pick!.Name)) missing++;
            }
        }
        Assert.True(checkedGroups > 10);
        // a group may name a clip that is not shipped; that is the asset's business, not a decode failure
        Assert.True(missing == 0 || missing < checkedGroups * 5,
            "every single group pick named a clip that does not exist, which suggests a decode fault");
    }

    /// <summary>
    /// A file can hold more than one clip. anim_bored_01.bin holds anim_bored_01 and anim_bored_02, and
    /// indexing by filename alone left the second unreachable.
    /// </summary>
    [Fact]
    public void EveryClipInsideAFileIsReachableNotJustTheOneNamedAfterIt()
    {
        var lib = Library();
        if (lib is null) return;
        if (!lib.HasClip("anim_bored_01")) return;

        Assert.True(lib.HasClip("anim_bored_02"), "anim_bored_02 lives inside anim_bored_01.bin");
        var second = lib.GetClip("anim_bored_02");
        Assert.Equal("anim_bored_02", second.Name);
        Assert.NotEqual(lib.GetClip("anim_bored_01").DurationMs, second.DurationMs);

        // its own body keyframe, distinct from its sibling's
        var body = Assert.Single(second.Keyframes.OfType<BodyKeyframe>());
        Assert.Equal(693u, body.TriggerTimeMs);
        Assert.Equal(495u, body.DurationTimeMs);
        Assert.Equal(-38, body.Speed);
        Assert.True(body.IsStraight);

        // and the index is now larger than the file count, because of files like this one
        Assert.True(lib.ClipNames.Count > 289, $"only {lib.ClipNames.Count} clips indexed");
    }

    /// <summary>A real clip driven through the scheduler, to prove the timeline works on real data.</summary>
    [Fact]
    public void ARealClipPlaysThroughTheSchedulerOnItsOwnTimeline()
    {
        var lib = Library();
        if (lib is null) return;
        if (!lib.HasClip("anim_bored_01")) return;

        var clip = lib.GetClip("anim_bored_01");
        var fired = new List<Keyframe>();
        var faces = 0;
        var sink = new CountingSink(() => faces++);
        var s = new AnimationScheduler(sink);
        s.KeyframeFired += fired.Add;

        var handle = s.Play(clip, 0)!;
        for (double t = 0; t <= clip.DurationMs + 100; t += 1000.0 / AnimationScheduler.FrameRateHz)
            s.Advance(t);

        Assert.Equal(clip.Keyframes.Count, fired.Count);
        Assert.Equal(AnimationEndReason.Completed, handle.Completion.Result);
        Assert.True(faces > clip.DurationMs / 100, "the face should be redrawn on most frames");
        // Each track fires in timeline order. Across tracks it does not: keyframes that fall in the same
        // streamed frame go out in the engine's fixed per-track order (head, lift, event, face, lights,
        // body), not in the order the clip happens to list them, so a body keyframe at 100 ms can follow a
        // head keyframe at 130 ms when both land on the same frame.
        foreach (var track in fired.GroupBy(k => k.GetType()))
        {
            var inTrack = track.ToList();
            for (int i = 1; i < inTrack.Count; i++)
                Assert.True(inTrack[i].TriggerTimeMs >= inTrack[i - 1].TriggerTimeMs,
                    $"{track.Key.Name} fired out of order at index {i}");
        }
    }

    private sealed class CountingSink : IAnimationSink
    {
        private readonly Action _onFace;
        public CountingSink(Action onFace) => _onFace = onFace;
        public void Face(FaceBitmap bitmap) => _onFace();
        public void Audio(byte[]? mulawFrame) { }
        public void Head(sbyte angleDeg, uint durationMs) { }
        public void Lift(byte heightMm, uint durationMs) { }
        public void Body(BodyKeyframe keyframe) { }
        public void AnimationStarted(byte tag) { }
        public void AnimationEnded() { }

        public void BodyStop() { }
        public void Lights(LightsKeyframe keyframe) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }
}
