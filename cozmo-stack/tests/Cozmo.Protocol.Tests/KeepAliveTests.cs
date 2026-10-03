using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The live idle as the engine runs it, driven through the live entry (R-BEH2 batch 2). The retired
/// <c>IdleBehavior</c> was a second copy of this; the one idle is <c>AnimationStreamer::Update</c> (0x0057CE5C) with
/// ProceduralLive (0x198) on top of the idle stack, which runs <c>UpdateLiveAnimation</c> (0x0057D5F8) and the face keep-alive
/// <c>FaceLayerManager::KeepFaceAlive</c> (0x0058D374), once per 60 ms engine tick (<c>CozmoInstanceRunner::Run</c> 0x0065B3A8,
/// the period 0x03938700 ns).
///
/// Every expected value below is read off the binary at the address named with it (the check-2 report
/// <c>re-analysis/research/20261002-R-BEH2-check2-M7-idle-rows.md</c> and the disassembly), never taken from what the code
/// returns. The engine has no pusher of ProceduralLive, so these tests push it through the streamer's
/// <c>PushIdleAnimation</c> seam (a RECOVERABLE_GAP, recorded in the job report).
/// </summary>
public class KeepAliveTests
{
    /// <summary>Records what the live animation actually streams, with the engine-tick index each item arrived on.</summary>
    private sealed class Recorder : IAnimationSink
    {
        public int Tick;
        public readonly List<(int Tick, sbyte Deg, uint Dur)> Heads = new();
        public readonly List<(int Tick, byte Mm, uint Dur)> Lifts = new();
        public readonly List<(int Tick, BodyKeyframe K)> Bodies = new();
        public int BodyStops;
        public readonly List<string> Log = new();

        public void Face(FaceBitmap bitmap) { }
        public void Audio(byte[]? mulawFrame) { }
        public void Head(sbyte angleDeg, uint durationMs) => Heads.Add((Tick, angleDeg, durationMs));
        public void Lift(byte heightMm, uint durationMs) => Lifts.Add((Tick, heightMm, durationMs));
        public void Body(BodyKeyframe k) => Bodies.Add((Tick, k));
        public void AnimationStarted(byte tag) => Log.Add("start:" + tag);
        public void AnimationEnded() => Log.Add("end");
        public void BodyStop() => BodyStops++;
        public void Lights(LightsKeyframe k) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }

    private static float F(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));
    private static uint Bits(float f) => unchecked((uint)BitConverter.SingleToInt32Bits(f));

    /// <summary>
    /// A streamer with ProceduralLive on top of the idle stack and every tunable pinned so the cadence is arithmetic:
    /// the first SetParam runs the lazy default-set (0x0057C18E) and the values set here follow it. Body duration D and spacing S fixed,
    /// head and lift spacings so long that each generates once.
    /// </summary>
    private static (AnimationScheduler S, Recorder R) Live(int bodyDur = 300, int bodySpacing = 600, float straight = 1.0f, int seed = 5)
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r, new Random(seed));
        var p = s.LiveIdleParameters;
        p[LiveIdleParam.BodyMovementDurationMin_ms] = bodyDur; p[LiveIdleParam.BodyMovementDurationMax_ms] = bodyDur;
        p[LiveIdleParam.BodyMovementSpacingMin_ms] = bodySpacing; p[LiveIdleParam.BodyMovementSpacingMax_ms] = bodySpacing;
        p[LiveIdleParam.BodyMovementStraightFraction] = straight;
        p[LiveIdleParam.LiftMovementSpacingMin_ms] = 1_000_000; p[LiveIdleParam.LiftMovementSpacingMax_ms] = 1_000_000;
        p[LiveIdleParam.HeadMovementSpacingMin_ms] = 1_000_000; p[LiveIdleParam.HeadMovementSpacingMax_ms] = 1_000_000;
        p[LiveIdleParam.HeadAngleVariability_deg] = 0;
        p[LiveIdleParam.LiftHeightVariability_mm] = 0;
        s.PushIdleAnimation(AnimationTrigger.ProceduralLive, "test");
        return (s, r);
    }

    private static void Tick(AnimationScheduler s, Recorder r, int tick) { r.Tick = tick; s.Advance(60.0 * tick); }

    // ---------------------------------------------------------------- M7-017: the entry and its gates

    /// <summary>
    /// M7-017: with the default stack top (Count 0x23F) the engine never runs UpdateLiveAnimation: Update takes the S3b
    /// branch at 0x0057D03A..0x0057D04C (empty stack or top == 0x23F) and only the layers and the buffer are served.
    /// Nothing in the engine pushes ProceduralLive (check 2 section 1.8), so a freshly built streamer generates no
    /// head, lift or body keyframe however long it runs.
    /// </summary>
    [Fact]
    public void M7_017_WithTheDefaultIdleStackTheEngineNeverGeneratesLiveKeyframes()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r, new Random(5));
        for (int i = 0; i < 200; i++) Tick(s, r, i);
        Assert.Empty(r.Heads);
        Assert.Empty(r.Lifts);
        Assert.Empty(r.Bodies);
        Assert.False(s.LiveStreamActive);
        Assert.Equal(new[] { (AnimationScheduler.IdleCount, AnimationScheduler.DefaultAnimLock) }, s.IdleStack);
    }

    /// <summary>
    /// M7-017: the old StreamLive pushed ProceduralLive itself and short-circuited the streamer's generator by lock name
    /// ("StreamLive"); neither is in the engine. A keyframe appended to the live animation without ProceduralLive on the
    /// stack is never streamed (the S3b branch above), and the stack is untouched.
    /// </summary>
    [Fact]
    public void M7_017_StreamLiveAppendsToTheLiveAnimationAndDoesNotPushProceduralLive()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r, new Random(5));
        Assert.True(s.StreamLive(new HeadKeyframe(0, 250, 12, 0), 0));
        for (int i = 0; i < 30; i++) Tick(s, r, i);
        Assert.Empty(r.Heads);
        Assert.Single(s.IdleStack);
    }

    /// <summary>
    /// M7-017 / M7-008: ProceduralLive on top routes the idle through UpdateLiveAnimation. G1 (+0x194, set in the same branch,
    /// 0x0057D070..0x0057D074) passes; G2 reads GetParam&lt;int&gt;(2), which first runs the lazy default-set
    /// (0x0057DCE6..0x0057DCF4) - so with no parameter ever set, TimeBeforeWiggleMotions is the default 0x447A0000 =
    /// 1000.0, not 0 - and +0x44 (+= 0x3C per tail, 0x0057D446) reaches 1000 on the 18th Update (17 x 60 = 1020). Nothing
    /// streams before that; the head, lift and body keyframes of that Update stream in it.
    /// </summary>
    [Fact]
    public void M7_017_TheLazyDefaultSetHoldsTheFirstKeyframesBackUntilTheSeventeenthTick()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r, new Random(5));
        Assert.False(s.LiveIdleParameters.DefaultsSet);
        s.PushIdleAnimation(AnimationTrigger.ProceduralLive, "test");
        for (int i = 0; i < 17; i++) Tick(s, r, i);
        Assert.True(s.LiveIdleParameters.DefaultsSet);
        Assert.Equal(Bits(F(0x447A0000)), Bits(s.LiveIdleParameters[LiveIdleParam.TimeBeforeWiggleMotions_ms]));
        Assert.Empty(r.Heads);
        Assert.Empty(r.Lifts);
        Assert.Empty(r.Bodies);
        Tick(s, r, 17);
        Assert.Equal(17, Assert.Single(r.Heads).Tick);
        Assert.Equal(17, Assert.Single(r.Lifts).Tick);
        Assert.Equal(17, Assert.Single(r.Bodies).Tick);
    }

    /// <summary>
    /// M7-017: UpdateLiveAnimation's nonzero result (0x0057D086) is an sErrorF, the error flag, and a return of that
    /// result straight to the caller (0x0057D08C..0x0057D0E6 to 0x0057D032): the tail (0x0057D3EE..0x0057D448: InitStream /
    /// UpdateStream, +0x88 = now, +0x44 += 60) is skipped. A keyframe append fails above 1000 keyframes (AddKeyFrameToBackHelper,
    /// M5 L4), so a body track filled past that makes the generation at +0x44 = 1020 (the 18th Update) fail. The body duration
    /// is stored before the append (+0x198 = RandIntInRange, 0x0057D6CC) and the spacing after it (0x0057D978), so the next
    /// Updates count the 300 ms duration down (5 x 60) and the failure comes again on the 24th. The float +0x88 (the last
    /// UpdateStream time) is not stored on a failing Update: it stays at the 17th Update's 0.96f (0x3F75C28F) and moves on
    /// at the 19th.
    /// </summary>
    [Fact]
    public void M7_017_AFailedLiveAppendReturnsWithoutTheTail()
    {
        var (s, r) = Live();
        int now = 0;
        var errorTicks = new List<int>();
        s.Log = e => { if (e.Contains("LiveUpdateFailed")) errorTicks.Add(now); };
        for (int i = 0; i < 1100; i++) Assert.True(s.StreamLive(new BodyKeyframe(0, 100_000, "STRAIGHT", 0), 0) || i > 1000);
        for (int i = 0; i < 17; i++) { now = i; Tick(s, r, i); }
        Assert.Empty(errorTicks);
        Assert.Equal(0x3F75C28Fu, Bits(s.LastStreamSec));                          // 16 x 60 ms = 0.96f: the last UpdateStream
        now = 17; Tick(s, r, 17);
        Assert.Equal(new[] { 17 }, errorTicks);
        Assert.Equal(0x3F75C28Fu, Bits(s.LastStreamSec));                          // not stored: the tail did not run
        now = 18; Tick(s, r, 18);
        Assert.Equal(0x3F8A3D71u, Bits(s.LastStreamSec));                          // 18 x 60 ms = 1.08f: the tail ran again
        for (int i = 19; i <= 24; i++) { now = i; Tick(s, r, i); }
        Assert.Equal(new[] { 17, 23 }, errorTicks);
        // the body failed first on the 18th Update (no lift or head then); the next Update generates them
        Assert.Equal(18, Assert.Single(r.Heads).Tick);
        Assert.Equal(18, Assert.Single(r.Lifts).Tick);
    }

    /// <summary>
    /// M7-004 / M7-017: the base SetParam (0x0057C178) runs the lazy default-set first (0x0057C18E..0x0057C19C), then clamps into
    /// GetParamRange and stores, so a value set before the first Update survives the defaults the Update would otherwise apply.
    /// GetParamRange (0x0057F13C) falls back to fullRange (0x00C5A380) = {0xFF7FFFFF, 0x7F7FFFFF}: -FLT_MAX..FLT_MAX. An infinity
    /// is clamped to those bits and a NaN gives the maximum (<c>vcmpe; it le</c> is true unordered, 0x0057C1B4..0x0057C1C2).
    /// AnimationStreamer::SetParam replaces a blink maximum above 30000 by 30000 (0x0057C0C6) before that.
    /// </summary>
    [Fact]
    public void M7_004_SetParamRunsTheDefaultsFirstAndClampsToTheParamRange()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r, new Random(5));
        var p = s.LiveIdleParameters;
        Assert.False(p.DefaultsSet);
        p[LiveIdleParam.BodyMovementSpeedMinMax_mmps] = 77;
        Assert.True(p.DefaultsSet);
        s.PushIdleAnimation(AnimationTrigger.ProceduralLive, "test");
        for (int i = 0; i < 18; i++) Tick(s, r, i);        // the first Update's lazy default-set must not undo it
        Assert.Equal(0x429A0000u, Bits(p[LiveIdleParam.BodyMovementSpeedMinMax_mmps]));      // 77.0f
        Assert.Equal(0x447A0000u, Bits(p[LiveIdleParam.TimeBeforeWiggleMotions_ms]));         // a default still applied (1000.0f)

        p[LiveIdleParam.LiftHeightMean_mm] = float.PositiveInfinity;
        Assert.Equal(0x7F7FFFFFu, Bits(p[LiveIdleParam.LiftHeightMean_mm]));
        p[LiveIdleParam.LiftHeightMean_mm] = float.NegativeInfinity;
        Assert.Equal(0xFF7FFFFFu, Bits(p[LiveIdleParam.LiftHeightMean_mm]));
        p[LiveIdleParam.LiftHeightMean_mm] = float.NaN;
        Assert.Equal(0x7F7FFFFFu, Bits(p[LiveIdleParam.LiftHeightMean_mm]));
        p[LiveIdleParam.BlinkSpacingMaxTime_ms] = 40000;
        Assert.Equal(0x46EA6000u, Bits(p[LiveIdleParam.BlinkSpacingMaxTime_ms]));            // 30000.0f
    }

    // ---------------------------------------------------------------- M7-008: the tick and the gates

    /// <summary>
    /// M7-008: the first generation at +0x44 = 1020 has a body keyframe of the drawn duration (RandIntInRange(p5, p6), here
    /// 300 = 300), the engine's radius for straight 0x7FFF (0x0057D8DA) with the straight test <c>r &lt;= p8</c> on a double
    /// draw in [0,1) (0x0057D720..0x0057D748: p8 = 1.0f makes every draw straight), a speed in [-p7, p7] (p7 = 10), and the
    /// next one comes when duration + spacing (300 + 600) has been counted down by 60 per Update: 900 / 60 = 15
    /// decrements, generating on the 16th Update after the last (0x0057D650 decrements, 0x0057D6CC..0x0057D978 generates).
    /// </summary>
    [Fact]
    public void M7_008_TheNextBodyShuffleComesADurationAndAGapLaterOnTheSixtyMillisecondTick()
    {
        var (s, r) = Live();
        for (int i = 0; i < 17 + 16 * 3 + 1; i++) Tick(s, r, i);
        Assert.Equal(new[] { 17, 33, 49, 65 }, r.Bodies.Select(b => b.Tick).ToArray());
        Assert.All(r.Bodies, b =>
        {
            Assert.Equal(300u, b.K.DurationTimeMs);
            Assert.Equal(BodyKeyframe.StraightRadius, b.K.EncodedRadius);
            Assert.InRange((int)b.K.Speed, -10, 10);
        });
    }

    /// <summary>
    /// M7-008 (the corrected rows, check 2 section 1.3): picking or placing in progress (<c>[[robot+0x280]+4] != 0</c>,
    /// 0x0057D61C..0x0057D624 -> 0x0057D6BE) returns 0 with NO decrement, so the countdown stands still while it is set:
    /// held for 5 Updates, the next shuffle is 5 Updates later than the 16 of the free-running cadence.
    /// </summary>
    [Fact]
    public void M7_008_PickingOrPlacingFreezesTheCountdownAndDoesNotDecrementIt()
    {
        var (s, r) = Live();
        bool held = false;
        s.LiveIdleInputs.PickingOrPlacing = () => held;
        for (int i = 0; i <= 17; i++) Tick(s, r, i);
        held = true;
        for (int i = 18; i <= 22; i++) Tick(s, r, i);
        held = false;
        for (int i = 23; i <= 40; i++) Tick(s, r, i);
        Assert.Equal(new[] { 17, 33 + 5 }, r.Bodies.Select(b => b.Tick).Take(2).ToArray());
    }

    /// <summary>
    /// M7-008: a MovementComponent flag (IS_MOVING, +9), a locked track (AreAnyTracksLocked(4)) or a not-in-position flag
    /// DECREMENTS (0x0057D650, 0x0057D68A, 0x0057D6BA: <c>subs r0,#0x3c</c>) instead of generating, so a hold longer than the
    /// countdown lets the next shuffle come on the first free Update. Held from the 19th to the 38th Update (20 Updates, more than
    /// the 16 of the cadence), the next body keyframe is on the 39th.
    /// </summary>
    [Theory]
    [InlineData("moving")]
    [InlineData("locked")]
    public void M7_008_AMovementFlagOrALockedTrackKeepsDecrementingTheCountdown(string gate)
    {
        var (s, r) = Live();
        bool held = false;
        if (gate == "moving") s.LiveIdleInputs.Moving = () => held;
        else s.LiveIdleInputs.LockedTracks = () => (byte)(held ? 4 : 0);
        for (int i = 0; i <= 17; i++) Tick(s, r, i);
        held = true;
        for (int i = 18; i <= 37; i++) Tick(s, r, i);
        Assert.Single(r.Bodies);
        held = false;
        Tick(s, r, 38);
        Assert.Equal(new[] { 17, 38 }, r.Bodies.Select(b => b.Tick).ToArray());
    }

    /// <summary>
    /// M7-008: the lift alone also holds off, and decrements, while carrying (<c>[[robot+0x284]+8] + 1 != 0</c>,
    /// 0x0057D97E..0x0057D9CA). Carrying from the start keeps the lift out of the first generation at the 18th Update (head
    /// and body are generated); once carrying clears, the lift's duration and spacing are both 0, so it generates on that
    /// very Update.
    /// </summary>
    [Fact]
    public void M7_008_CarryingHoldsOnlyTheLiftBack()
    {
        var (s, r) = Live();
        bool carrying = true;
        s.LiveIdleInputs.Carrying = () => carrying;
        for (int i = 0; i <= 25; i++) Tick(s, r, i);
        Assert.Equal(17, Assert.Single(r.Heads).Tick);
        Assert.Empty(r.Lifts);
        carrying = false;
        Tick(s, r, 26);
        Assert.Equal(26, Assert.Single(r.Lifts).Tick);
    }

    /// <summary>
    /// M7-008: the carrying input is wired at the production call site (<c>CozmoAnimations</c>) from the manipulation
    /// system's CarryingComponent seam (<c>Motion.IsCarryingObject</c>); unwired it reads not carrying.
    /// </summary>
    [Fact]
    public void M7_008_TheCarryingInputIsWiredToTheCarryingComponent()
    {
        using var robot = CozmoRobot.CreateOffline();
        var inputs = robot.Animations.Scheduler.LiveIdleInputs;
        robot.Motion.IsCarryingObject = null;
        Assert.False(inputs.Carrying());
        robot.Motion.IsCarryingObject = () => true;
        Assert.True(inputs.Carrying());
        robot.Motion.IsCarryingObject = () => false;
        Assert.False(inputs.Carrying());
    }

    /// <summary>
    /// M7-008: a streaming Update stores 0 into +0x44 (<c>str r5,[r4,#0x44]</c> at 0x0057D000), so the idle counter restarts after
    /// a clip. Eleven idle Updates leave +0x44 at 600; a clip then streams, and the live keyframes come 17 Updates after the
    /// Update that ended it (that Update zeroes +0x44 in its streaming branch and the tail adds 60; 17 x 60 = 1020 >= 1000), not
    /// at the 18th Update overall, which an un-reset counter would give.
    /// </summary>
    [Fact]
    public void M7_008_AClipStreamingZeroesTheIdleCounter()
    {
        var (s, r) = Live();
        for (int i = 0; i < 10; i++) Tick(s, r, i);
        var clip = new AnimationClip { Name = "h", Keyframes = new List<Keyframe> { new HeadKeyframe(0, 100, 5, 0) }, Tracks = AnimationTrack.Head, DurationMs = 100 };
        s.Play(clip, 60.0 * 10);
        int t = 10;
        for (; t < 40 && (t == 10 || s.IsPlaying); t++) Tick(s, r, t);
        Assert.False(s.IsPlaying);
        int ended = t - 1;                       // the Update on which the clip completed
        Assert.Empty(r.Bodies);
        for (; t < ended + 25; t++) Tick(s, r, t);
        Assert.Equal(ended + 17, r.Bodies.First().Tick);
    }

    /// <summary>
    /// M7-010: the turn eye-shift sign takes bit 15 of the speed (0x0057D790..0x0057D7B6: <c>vmov.f32 s0,#1.0</c>, flipped only when
    /// the sign bit is set), so a drawn speed of 0 is positive: with the speed range pinned to 0 every turn's shift is +x, never
    /// negative (and never -0.0f).
    /// </summary>
    [Fact]
    public void M7_010_AZeroTurnSpeedTakesThePositiveEyeShiftSign()
    {
        var (s, r) = Live(bodyDur: 250, bodySpacing: 100, straight: 0.0f);
        s.LiveIdleParameters[LiveIdleParam.BodyMovementSpeedMinMax_mmps] = 0;
        int seen = 0;
        for (int i = 0; i <= 120; i++)
        {
            Tick(s, r, i);
            foreach (var l in s.Layers.Face.AllLayers.Where(l => l.Name == "LiveIdleTurn"))
                foreach (var f in l.Track.Frames) { seen++; Assert.Equal(0u, Bits(f.Face.FaceCenterX) & 0x80000000u); }
        }
        Assert.True(seen > 0);
        Assert.All(r.Bodies, b => Assert.Equal((short)0, b.K.Speed));
    }

    // ---------------------------------------------------------------- M7-009: head and lift

    /// <summary>
    /// M7-009: the head keyframe's angle is <c>(s8)</c> of <c>vcvt.s32.f32(robot[+0x2FC] * 57.29578f)</c> (0x0057D838..0x0057D84E;
    /// 0x42652EE1 at 0x0057DB2C): truncation toward zero, then the low byte. 0.5 rad is 28.64789 -> 28; -0.5 rad -> -28;
    /// 5.0 rad is 286.4789 -> 286, which wraps to 30 in an s8 (it is not clamped to 127).
    /// </summary>
    [Theory]
    [InlineData(0.5f, 28)]
    [InlineData(-0.5f, -28)]
    [InlineData(5.0f, 30)]
    public void M7_009_TheHeadAngleIsTheTruncatedDegreesWrappedToAnS8(float rad, int expected)
    {
        Assert.Equal(0x42652EE1u, Bits(AnimationScheduler.RadToDeg));
        var (s, r) = Live();
        s.LiveIdleInputs.HeadAngleRad = () => rad;
        for (int i = 0; i <= 17; i++) Tick(s, r, i);
        var head = Assert.Single(r.Heads);
        Assert.Equal(expected, (int)head.Deg);
    }

    /// <summary>
    /// M7-009: the lift keyframe is LiftHeightKeyFrame(mean 35, variability 8) with the drawn duration
    /// RandIntInRange(p9, p10) (the defaults 0x42480000 = 50.0 and 0x43FA0000 = 500.0 at 0x0057DB40..0x0057DCD6). With the
    /// variability set to 0 the height streamed is the mean exactly, 35, and the duration is in [50, 500]; the head's is
    /// RandIntInRange(p15, p16), also 50..500.
    /// </summary>
    [Fact]
    public void M7_009_TheLiftKeyframeCarriesTheMeanHeightAndADrawnDuration()
    {
        var (s, r) = Live();
        for (int i = 0; i <= 17; i++) Tick(s, r, i);
        var lift = Assert.Single(r.Lifts);
        Assert.Equal(35, (int)lift.Mm);
        Assert.InRange((int)lift.Dur, 50, 500);
        var head = Assert.Single(r.Heads);
        Assert.InRange((int)head.Dur, 50, 500);
    }

    // ---------------------------------------------------------------- M7-010: the body shuffle

    /// <summary>
    /// M7-010: the straight test is <c>r &lt;= p8</c> on <c>RandDblInRange(0.0, 1.0)</c> (0x0057D720..0x0057D748, <c>ble 0x0057D8C6</c>
    /// = straight), so a fraction of 0 makes every shuffle a turn on the spot (radius 0, 0x0057D80E) and 1.0 makes every one
    /// straight (0x7FFF). Speed is a whole mm/s in [-10, 10].
    /// </summary>
    [Theory]
    [InlineData(0.0f, (short)0)]
    [InlineData(1.0f, (short)0x7FFF)]
    public void M7_010_TheStraightFractionDecidesTheRadius(float fraction, short radius)
    {
        var (s, r) = Live(bodyDur: 250, bodySpacing: 100, straight: fraction);
        for (int i = 0; i <= 17 + 6 * 8; i++) Tick(s, r, i);
        Assert.True(r.Bodies.Count >= 6);
        Assert.All(r.Bodies, b => Assert.Equal(radius, b.K.EncodedRadius));
        Assert.All(r.Bodies, b => Assert.InRange((int)b.K.Speed, -10, 10));
    }

    /// <summary>
    /// M7-010 / M7-007: the turn's eye shift is the persistent layer "LiveIdleTurn" (AddOrUpdateEyeShift 0x0064F3C8, the call at
    /// 0x0057D7FC; the layer is added with AddPersistentLayer at 0x0064F472) and it is NOT gone a couple of ticks later - the
    /// persistent layer stays until RemoveEyeShift (0x0057D8D2, the straight branch). So after turns it is still there 40 Updates
    /// on, and the first straight shuffle removes it.
    /// </summary>
    [Fact]
    public void M7_010_ATurnsEyeShiftIsAPersistentLayerThatAStraightRemoves()
    {
        var (s, r) = Live(bodyDur: 250, bodySpacing: 100, straight: 0.0f);
        for (int i = 0; i <= 17; i++) Tick(s, r, i);
        Assert.Contains("LiveIdleTurn", s.FaceLayerNames);
        for (int i = 18; i <= 60; i++) Tick(s, r, i);
        Assert.Contains("LiveIdleTurn", s.FaceLayerNames);          // 43 Updates later: not removed by itself (0x0064F472)
        Assert.Single(s.FaceLayerNames, n => n == "LiveIdleTurn");   // updated in place (0x0064F41A), never a second layer

        s.LiveIdleParameters[LiveIdleParam.BodyMovementStraightFraction] = 1.0f;
        int i2 = 61;
        while (r.Bodies.Last().K.EncodedRadius != BodyKeyframe.StraightRadius && i2 < 120) Tick(s, r, i2++);
        Assert.Equal(BodyKeyframe.StraightRadius, r.Bodies.Last().K.EncodedRadius);
        Assert.DoesNotContain("LiveIdleTurn", s.FaceLayerNames);
    }

    /// <summary>
    /// M7-010 / M7-006: GenerateEyeShift (0x0058CFC4) discards the caller's 64.0 / 32.0 limits and uses
    /// max(xmin, 128 - xmax) = 17 and max(ymin, 64 - ymax) = 12 of a default face (0x0058CFE8..0x0058D030), and LookAt's
    /// SetFacePosition clamps the face centre into [-17, 17] x [-12, 12] (0x00583B20). The turn's x is drawn 0..21: over many
    /// turns the largest face centre is exactly 17.0f (0x41880000), never more, and the y never leaves +-10.
    /// </summary>
    [Fact]
    public void M7_010_TheTurnShiftIsClampedToSeventeenBySetFacePosition()
    {
        float maxAbsX = 0, maxAbsY = 0;
        for (int x = -21; x <= 21; x++)
            for (int y = -10; y <= 10; y++)
            {
                var kf = FaceLayerManager.GenerateEyeShift(x, y, 64f, 32f, F(0x3F8CCCCD), F(0x3F59999A), F(0x3DCCCCCD), 33);
                maxAbsX = Math.Max(maxAbsX, Math.Abs(kf.Face.FaceCenterX));
                maxAbsY = Math.Max(maxAbsY, Math.Abs(kf.Face.FaceCenterY));
                Assert.Equal(33u, kf.Trigger);
            }
        Assert.Equal(0x41880000u, Bits(maxAbsX));
        Assert.Equal(0x41200000u, Bits(maxAbsY));        // 10.0f: inside the 12 clamp
    }

    // ---------------------------------------------------------------- M7-017: the streamed wire and the clock

    /// <summary>
    /// M7-017 (M5 C5): a live body keyframe is stopped by its own stop message on the first live frame with its counter at the
    /// duration, and only once.
    /// </summary>
    [Fact]
    public void M7_017_ALiveBodyKeyframeStopsWhenItsDurationRunsOut()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r, new Random(2));
        s.PushLiveQuietly();
        s.StreamLive(new BodyKeyframe(0, 500, "TURN_IN_PLACE", 10), 1_000);
        s.Advance(1_000);                 // InitStream(live, 0xFF)
        s.Advance(1_033);                 // the first live frame: the body starts
        Assert.Single(r.Bodies);

        for (int i = 2; i <= 15; i++) s.Advance(1_000 + 33 * i);   // frames at 33 .. 462 of stream time
        Assert.Equal(0, r.BodyStops);
        s.Advance(1_000 + 33 * 16);       // the frame at 495: counter 495 < 500, still running
        s.Advance(1_000 + 33 * 17);       // the frame at 528: counter 528 >= 500, the stop
        Assert.Equal(1, r.BodyStops);
        for (int i = 18; i <= 30; i++) s.Advance(1_000 + 33 * i);
        Assert.Equal(1, r.BodyStops);     // and only once
    }

    /// <summary>
    /// M7-017: +0x88 and +0x1C0 are float32 (ctor 0xFF7FFFFF at 0x00579FDE and 0x3F000000 at 0x0057A040), compared with
    /// <c>vsub.f32</c> / <c>vcmpe.f32</c> (0x0057CF9A, 0x0057CFA4): the keep-alive of an idle-less streamer starts when
    /// <c>now - last</c> in FLOAT is greater than 0.5, not in double. At 100 000 s a float step is 2^-7 = 0.0078125 s, so
    /// 0.503 s after the last stream the float clock reads exactly 0.5 s later and the engine has not started the keep-alive yet;
    /// the next 125 ms tick, it has.
    /// </summary>
    [Fact]
    public void M7_017_TheKeepAliveTimeoutIsComparedInFloat32()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r, new Random(4));
        Assert.Equal(0xFF7FFFFFu, Bits(s.LastStreamSec));
        Assert.Equal(0x3F000000u, Bits(s.KeepAliveTimeoutSec));

        const double T0 = 100_000_000;                  // 100 000 s
        var clip = new AnimationClip
        {
            Name = "h", Keyframes = new List<Keyframe> { new HeadKeyframe(0, 100, 5, 0) }, Tracks = AnimationTrack.Head, DurationMs = 100,
        };
        s.Play(clip, T0);
        double tl = T0, t = T0;
        float seen = s.LastStreamSec;
        for (int i = 0; i < 60 && (s.IsPlaying || i < 2); i++, t += 125)
        {
            s.Advance(t);
            if (Bits(s.LastStreamSec) != Bits(seen)) { seen = s.LastStreamSec; tl = t; }
        }
        Assert.False(s.IsPlaying);
        Assert.True(seen > 0);
        Assert.Equal(tl / 1000.0, (double)seen);                      // a multiple of 0.125 s is exact in float
        Assert.Empty(s.FaceLayerNames);

        // the discriminating tick: double diff 0.503 > 0.5, float diff exactly 0.5f
        double tick = tl + 503;
        float nowF = (float)(tick / 1000.0);
        Assert.Equal(0x3F000000u, Bits(nowF - seen));
        Assert.True(tick / 1000.0 - seen > 0.5);
        s.Advance(tick);
        Assert.Empty(s.FaceLayerNames);                               // 0x0057CFA4: not (diff > 0.5f): no KeepFaceAlive yet

        s.Advance(tick + 125);                                        // float diff 0.625 > 0.5: KeepFaceAlive runs
        Assert.Contains("Blink", s.FaceLayerNames);
        Assert.Contains("KeepAliveEyeDart", s.FaceLayerNames);
    }

    /// <summary>
    /// M7-016: gate A of the keep-alive block is <c>[+0x88] &gt; 0.0f</c> (0x0057CF6A..0x0057CF76); +0x88 is -FLT_MAX until
    /// something has streamed. With the default stack and nothing streamed, no face layer is ever created, however long it
    /// runs. With ProceduralLive on top (the idle is the live animation, 0x0057CFB6) an EMPTY live animation counts as ended
    /// (the InitStream of an empty animation sets endSent, A10) and is only re-initialised each Update, never streamed, so
    /// +0x88 stays -FLT_MAX; the first UpdateStream is the 18th Update's (the keyframes generated in it), and the keep-alive
    /// starts on the 19th.
    /// </summary>
    [Fact]
    public void M7_016_TheKeepAliveWaitsForSomethingToHaveStreamed()
    {
        var r = new Recorder();
        var s = new AnimationScheduler(r, new Random(4));
        for (int i = 0; i < 100; i++) Tick(s, r, i);
        Assert.Empty(s.FaceLayerNames);

        var (l, lr) = Live();
        for (int i = 0; i <= 17; i++) { Tick(l, lr, i); Assert.Empty(l.FaceLayerNames); }
        Tick(l, lr, 18);
        Assert.Contains("Blink", l.FaceLayerNames);
        Assert.Contains("KeepAliveEyeDart", l.FaceLayerNames);
    }
}
