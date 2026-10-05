using System.Globalization;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M7-017, the needs-driven face distortion (B-FACE batch 1) and its wiring (batch 2). Every expected value
/// comes from the binary or the shipped config, never from the implementation: the component's addresses
/// (ctor 0x0063B3BE, Init 0x0063B3D4, Params 0x0063B434, GetCurrentDesiredDistortion 0x0063B760) and the
/// rows D01..D18 in <c>re-analysis/research/20261004-procedural-live-extraction.md</c>; the graphs from
/// <c>re-analysis/obb/assets/cozmo_resources/config/engine/needs_handlers_config.json</c>.
/// </summary>
public class DesiredFaceDistortionTests
{
    private static float F(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));
    private static uint Bits(float f) => unchecked((uint)BitConverter.SingleToInt32Bits(f));

    /// <summary>-1.0f, 0xBF800000 (D02/D10).</summary>
    private static float NegativeOne => F(0xBF800000);
    /// <summary>1e-5f, 0x3727C5AC (D18).</summary>
    private static float Epsilon => F(0x3727C5AC);
    /// <summary>0.1f, 0x3DCCCCCD (D13).</summary>
    private static float MinDegree => F(0x3DCCCCCD);

    /// <summary>One-node graphs so the repair level is irrelevant, with the given multipliers (D14/D15).</summary>
    private static string Block(double degree, double cooldown, double degreeMultiplier, double cooldownMultiplier)
    {
        string D(double d) => d.ToString("R", CultureInfo.InvariantCulture);
        return "{\"cooldown\":{\"nodes\":[{\"x\":0,\"y\":" + D(cooldown) + "}]},"
             + "\"cooldown_range_multiplier\":" + D(cooldownMultiplier) + ","
             + "\"degree\":{\"nodes\":[{\"x\":0,\"y\":" + D(degree) + "}]},"
             + "\"degree_range_multiplier\":" + D(degreeMultiplier) + "}";
    }

    private static void Init(DesiredFaceDistortionComponent c, string json, EngineRandom? rng)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        c.Init(doc.RootElement, rng);
    }

    private static NeedsManager Manager() => new(() => 0);

    /// <summary>
    /// Asserts the RNG has consumed exactly <paramref name="drawsConsumed"/> GetNextDbl pairs, then consumes
    /// one more pair from both it and a fresh same-seed generator and compares them.
    /// </summary>
    private static void AssertNextDrawEquals(EngineRandom rng, uint seed, int drawsConsumed)
    {
        var probe = new EngineRandom(seed);
        for (int i = 0; i < drawsConsumed; i++) probe.GetNextDbl();
        Assert.Equal(probe.GetNextDbl(), rng.GetNextDbl());
    }

    // ---------------------------------------------------------------- D09: the per-tick cache

    /// <summary>
    /// D09: the getter reads BaseStationTimer::GetTickCount and, when it equals the cached +0x14, returns the
    /// cached degree +0x10 at once (0x0063B76E..0x0063B782), drawing nothing. The cached tick starts at 0, so
    /// the first engine tick is a real (nonzero) tick, as the engine's monotonic timer is.
    /// </summary>
    [Fact]
    public void TheTickCacheReturnsTheCachedDegreeWithoutDrawingAgain()
    {
        const uint seed = 0xC0FFEE;
        var needs = Manager();
        long tick = 1;
        var c = new DesiredFaceDistortionComponent(needs) { TickCount = () => tick, ClockSeconds = () => 0f };
        var rng = new EngineRandom(seed);
        Init(c, Block(1.0, 2.0, 0.2, 0.3), rng);

        float first = c.GetCurrentDesiredDistortion();
        Assert.NotEqual(Bits(NegativeOne), Bits(first));
        float second = c.GetCurrentDesiredDistortion();
        Assert.Equal(Bits(first), Bits(second));                         // same tick: the cached value
        AssertNextDrawEquals(rng, seed, 2);                              // one sample = two pairs (degree, cooldown); the second call drew nothing
    }

    /// <summary>D09: a new tick resamples (the RNG advances by another degree and cooldown pair).</summary>
    [Fact]
    public void ANewTickResamples()
    {
        const uint seed = 0xC0FFEE;
        var needs = Manager();
        long tick = 1;
        float now = 0f;
        var c = new DesiredFaceDistortionComponent(needs) { TickCount = () => tick, ClockSeconds = () => now };
        var rng = new EngineRandom(seed);
        Init(c, Block(1.0, 2.0, 0.2, 0.3), rng);

        float first = c.GetCurrentDesiredDistortion();
        tick = 2;
        now = 3f;                                                        // past the first sample's deadline
        float second = c.GetCurrentDesiredDistortion();
        Assert.NotEqual(Bits(first), Bits(second));
        AssertNextDrawEquals(rng, seed, 4);                              // two samples = four pairs
    }

    // ---------------------------------------------------------------- D10, D12, D13: the -1 sentinels

    /// <summary>D10: params +0 null returns the -1.0f sentinel.</summary>
    [Fact]
    public void ParamsNullReturnsTheSentinel()
    {
        var needs = Manager();
        var c = new DesiredFaceDistortionComponent(needs) { TickCount = () => 1, ClockSeconds = () => 0f };
        Assert.Equal(Bits(NegativeOne), Bits(c.GetCurrentDesiredDistortion()));
    }

    /// <summary>D10: RNG +0xC null returns the sentinel.</summary>
    [Fact]
    public void RngNullReturnsTheSentinel()
    {
        var needs = Manager();
        var c = new DesiredFaceDistortionComponent(needs) { TickCount = () => 1, ClockSeconds = () => 0f };
        Init(c, Block(1.0, 2.0, 0.2, 0.3), null);
        Assert.Equal(Bits(NegativeOne), Bits(c.GetCurrentDesiredDistortion()));
    }

    /// <summary>D10: the manager's pause byte +0x1D5 returns the sentinel and draws nothing.</summary>
    [Fact]
    public void PausedManagerReturnsTheSentinelAndConsumesNoRng()
    {
        const uint seed = 7;
        var needs = Manager();
        needs.SetPaused(true);
        var c = new DesiredFaceDistortionComponent(needs) { TickCount = () => 1, ClockSeconds = () => 0f };
        var rng = new EngineRandom(seed);
        Init(c, Block(1.0, 2.0, 0.2, 0.3), rng);
        Assert.Equal(Bits(NegativeOne), Bits(c.GetCurrentDesiredDistortion()));
        AssertNextDrawEquals(rng, seed, 0);
    }

    /// <summary>D12: a deadline in the future returns the sentinel on the next tick and draws nothing.</summary>
    [Fact]
    public void ADeadlineInTheFutureReturnsTheSentinelAndConsumesNoRng()
    {
        const uint seed = 11;
        var needs = Manager();
        float now = 0f;
        long tick = 1;
        var c = new DesiredFaceDistortionComponent(needs) { TickCount = () => tick, ClockSeconds = () => now };
        var rng = new EngineRandom(seed);
        Init(c, Block(1.0, 2.0, 0.2, 0.3), rng);

        float first = c.GetCurrentDesiredDistortion();
        Assert.NotEqual(Bits(NegativeOne), Bits(first));                 // sampled: deadline = now + cooldown > 0
        Assert.True(c.Deadline > now);

        tick = 2; now = 0.01f;                                           // still within the deadline
        Assert.Equal(Bits(NegativeOne), Bits(c.GetCurrentDesiredDistortion()));
        AssertNextDrawEquals(rng, seed, 2);                              // one sample drew one pair; this call drew none
    }

    /// <summary>D13: a degree graph below 0.1f returns the sentinel and draws nothing.</summary>
    [Fact]
    public void ADegreeBelowTheThresholdReturnsTheSentinelAndConsumesNoRng()
    {
        const uint seed = 13;
        var needs = Manager();
        var c = new DesiredFaceDistortionComponent(needs) { TickCount = () => 1, ClockSeconds = () => 0f };
        var rng = new EngineRandom(seed);
        Init(c, Block(0.05, 2.0, 0.2, 0.3), rng);
        Assert.Equal(Bits(NegativeOne), Bits(c.GetCurrentDesiredDistortion()));
        AssertNextDrawEquals(rng, seed, 0);
    }

    // ---------------------------------------------------------------- D13: the 0.1 boundary

    /// <summary>
    /// D13: the comparison is <c>blt</c> against 0x3DCCCCCD, so exactly 0.1f is admitted and one ULP below
    /// (0x3DCCCCCC) is rejected.
    /// </summary>
    [Fact]
    public void TheDegreeThresholdAdmitsExactlyZeroPointOne()
    {
        var needs = Manager();
        var admitted = new DesiredFaceDistortionComponent(needs) { TickCount = () => 1, ClockSeconds = () => 0f };
        Init(admitted, Block((double)MinDegree, 2.0, 0.2, 0.3), new EngineRandom(1));
        float sampled = admitted.GetCurrentDesiredDistortion();
        Assert.NotEqual(Bits(NegativeOne), Bits(sampled));
        Assert.InRange(sampled, MinDegree * F(0x3F666666), MinDegree * F(0x3F8CCCCD));   // 0.1 * [0.9, 1.1]

        var rejected = new DesiredFaceDistortionComponent(needs) { TickCount = () => 1, ClockSeconds = () => 0f };
        Init(rejected, Block((double)F(0x3DCCCCCC), 2.0, 0.2, 0.3), new EngineRandom(1));
        Assert.Equal(Bits(NegativeOne), Bits(rejected.GetCurrentDesiredDistortion()));
    }

    // ---------------------------------------------------------------- D14, D15, D16: the draws

    /// <summary>
    /// D14/D15/D16: the degree draw is first and the cooldown draw second, each from float32 bounds
    /// <c>graph * (1 - h) .. graph * (h + 1)</c> with <c>h = multiplier * 0.5f</c>, widened to float64 and
    /// sampled by <c>RandDblInRange</c> (0x0082FA48 = <c>low + GetNextDbl()*(high-low)</c>). The test
    /// replicates the engine's own formula from the same seed; it does not call the component twice.
    /// </summary>
    [Fact]
    public void TheDegreeDrawHappensBeforeTheCooldownDrawWithFloatBounds()
    {
        const uint seed = 0x5EED;
        const float degree = 1.0f, cooldown = 2.0f;
        const float degreeMultiplier = 0.2f, cooldownMultiplier = 0.3f;
        float one = F(0x3F800000), half = F(0x3F000000);

        // the engine's float32 bounds, instruction for instruction (D14/D15)
        float degreeH = degreeMultiplier * half;
        float degreeLow = degree * (one - degreeH);
        float degreeHigh = degree * (degreeH + one);
        float cooldownH = cooldownMultiplier * half;
        float cooldownLow = cooldown * (one - cooldownH);
        float cooldownHigh = cooldown * (cooldownH + one);

        var expected = new EngineRandom(seed);
        double u0 = expected.GetNextDbl();                                // the degree draw consumes the first pair
        double expectedDegree = degreeLow + u0 * (degreeHigh - degreeLow);
        double u1 = expected.GetNextDbl();                                // the cooldown draw the second
        double expectedCooldown = cooldownLow + u1 * (cooldownHigh - cooldownLow);

        var needs = Manager();
        var c = new DesiredFaceDistortionComponent(needs) { TickCount = () => 1, ClockSeconds = () => 0f };
        Init(c, Block(degree, cooldown, degreeMultiplier, cooldownMultiplier), new EngineRandom(seed));

        Assert.Equal(Bits((float)expectedDegree), Bits(c.GetCurrentDesiredDistortion()));
        Assert.Equal(Bits((float)expectedCooldown), Bits(c.Deadline));   // D17: deadline = float32(now + sampledCooldown), now = 0
    }

    // ---------------------------------------------------------------- D17: the deadline

    /// <summary>
    /// D17: after a successful sample the deadline is <c>float32(now + sampledCooldown)</c> (0x0063B954); the
    /// next tick before it returns -1, and a tick at or past it resamples (D12's <c>deadline &lt;= now</c>).
    /// </summary>
    [Fact]
    public void TheDeadlineIsNowPlusTheSampledCooldown()
    {
        const uint seed = 99;
        const float degree = 1.0f, cooldown = 2.0f;
        const float degreeMultiplier = 0.2f, cooldownMultiplier = 0.3f;
        float one = F(0x3F800000), half = F(0x3F000000);
        float cooldownH = cooldownMultiplier * half;
        float cooldownLow = cooldown * (one - cooldownH);
        float cooldownHigh = cooldown * (cooldownH + one);
        var expected = new EngineRandom(seed);
        expected.GetNextDbl();                                            // the degree draw
        double expectedCooldown = cooldownLow + expected.GetNextDbl() * (cooldownHigh - cooldownLow);

        var needs = Manager();
        float now = 10f;
        long tick = 1;
        var c = new DesiredFaceDistortionComponent(needs) { TickCount = () => tick, ClockSeconds = () => now };
        var rng = new EngineRandom(seed);
        Init(c, Block(degree, cooldown, degreeMultiplier, cooldownMultiplier), rng);

        Assert.NotEqual(Bits(NegativeOne), Bits(c.GetCurrentDesiredDistortion()));
        Assert.Equal(Bits(now + (float)expectedCooldown), Bits(c.Deadline));

        tick = 2; now = c.Deadline - 0.01f;                              // inside the deadline
        Assert.Equal(Bits(NegativeOne), Bits(c.GetCurrentDesiredDistortion()));
        AssertNextDrawEquals(rng, seed, 2);                              // the blocked tick drew nothing

        tick = 3; now = c.Deadline;                                      // deadline <= now: eligible
        Assert.NotEqual(Bits(NegativeOne), Bits(c.GetCurrentDesiredDistortion()));
    }

    // ---------------------------------------------------------------- D05, D06: the shipped config

    /// <summary>
    /// D05/D06 through the shipped config: <c>NeedsManager.FromObb</c> builds and initialises the component
    /// from <c>needs_handlers_config.json</c>. At repair level 0.1 the degree graph is exactly 2.0 and the
    /// cooldown graph exactly 4.0; with the shipped multipliers 0.2 and 0.3 the logged ranges are
    /// 1.8..2.2 and 3.4..4.6.
    /// </summary>
    [Fact]
    public void TheShippedConfigParsesToTheShippedGraphsAndMultipliers()
    {
        var obb = ObbRoot();
        Assert.NotNull(obb);                                             // AssetPresenceTests guards the OBB
        var needs = NeedsManager.FromObb(obb!, () => 0, new Random(1));
        needs.TickCount = () => 1;
        needs.State.SetNeedLevel(NeedId.Repair, 0.1);
        var component = needs.DesiredFaceDistortion;
        Assert.NotNull(component);
        var log = new List<string>();
        component!.Log = log.Add;

        float sampled = component.GetCurrentDesiredDistortion();
        Assert.NotEqual(Bits(NegativeOne), Bits(sampled));
        Assert.Contains(log, l => l.Contains("DistortingFace.Degree") && l.Contains("desired 2.000000") && l.Contains("range(1.800000-2.200000)"));
        Assert.Contains(log, l => l.Contains("DistortingFace.Cooldown") && l.Contains("desired 4.000000") && l.Contains("range(3.400000-4.600000)"));
    }

    /// <summary>
    /// D06 shipped quirk: the degree parser's emptiness check examines the <b>cooldown</b> graph. A non-empty
    /// degree graph with an empty cooldown graph logs <c>ConfigError.DegreeParsingFailed</c>; a non-empty
    /// cooldown graph with an empty degree graph does not.
    /// </summary>
    [Fact]
    public void TheDegreeParserChecksTheCooldownGraphForEmptiness()
    {
        var needs = Manager();
        var c = new DesiredFaceDistortionComponent(needs);
        var log = new List<string>();
        c.Log = log.Add;

        DesiredFaceDistortionComponent.ErrorFlagSet = false;
        Init(c, "{\"cooldown\":{\"nodes\":[]},\"cooldown_range_multiplier\":0.3,"
              + "\"degree\":{\"nodes\":[{\"x\":0,\"y\":1.0}]},\"degree_range_multiplier\":0.2}", new EngineRandom(1));
        Assert.True(DesiredFaceDistortionComponent.ErrorFlagSet);
        Assert.Contains(log, l => l.Contains("ConfigError.CooldownParsingFailed"));
        Assert.Contains(log, l => l.Contains("ConfigError.DegreeParsingFailed"));   // the quirk: cooldown empty, degree not

        log.Clear();
        DesiredFaceDistortionComponent.ErrorFlagSet = false;
        Init(c, "{\"cooldown\":{\"nodes\":[{\"x\":0,\"y\":1.0}]},\"cooldown_range_multiplier\":0.3,"
              + "\"degree\":{\"nodes\":[]},\"degree_range_multiplier\":0.2}", new EngineRandom(1));
        Assert.DoesNotContain(log, l => l.Contains("ConfigError.CooldownParsingFailed"));
        Assert.DoesNotContain(log, l => l.Contains("ConfigError.DegreeParsingFailed"));
    }

    // ---------------------------------------------------------------- D07/D08/D18: the live entry

    private sealed class Sink : IAnimationSink
    {
        public void Face(FaceBitmap bitmap) { }
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

    private static AnimationScheduler Streaming()
    {
        var s = new AnimationScheduler(new Sink(), new Random(1));
        var clip = new AnimationClip
        {
            Name = "h",
            Keyframes = new List<Keyframe> { new HeadKeyframe(0, 100, 5, 0) },
            Tracks = AnimationTrack.Head,
            DurationMs = 100,
        };
        s.Play(clip, 1000);
        s.Advance(1000);          // streaming branch: +0x88 = 1.0
        return s;
    }

    /// <summary>
    /// D07/D08/D18 through the live entry: with the last-stream seconds +0x88 &gt; 0, TrackLayerComponent::Update
    /// reads the seam and tail-calls AddGlitch only when the degree is above 0x3727C5AC. AddGlitch adds the face
    /// layer named "Glitch".
    /// </summary>
    [Fact]
    public void TheStreamerGlitchesOnlyAboveTheEpsilon()
    {
        var above = Streaming();
        above.DesiredFaceDistortion = () => 0.5f;
        above.Advance(1033);                                             // the keep-alive block: _tlc.Update
        Assert.Contains("Glitch", above.FaceLayerNames);

        var at = Streaming();
        at.DesiredFaceDistortion = () => Epsilon;                        // the compare is '>', so equality does not glitch
        at.Advance(1033);
        Assert.DoesNotContain("Glitch", at.FaceLayerNames);

        var unset = Streaming();                                         // the unset seam is the -1.0f sentinel
        unset.Advance(1033);
        Assert.DoesNotContain("Glitch", unset.FaceLayerNames);
    }

    // ---------------------------------------------------------------- batch 3: P05, the live-update failure

    /// <summary>
    /// P05 (0x0057D084..0x0057D0E6): when UpdateLiveAnimation returns nonzero, the streamer logs
    /// "AnimationStreamer.Update.LiveUpdateFailed" and sets the process-global error flag _errG (0x0105DD34),
    /// then returns without the shared idle tail. The generator returns 1 when an append to the live animation
    /// fails; a live Head track already at its 1000-keyframe limit makes the head append fail.
    /// </summary>
    [Fact]
    public void ALiveUpdateFailureLogsAndSetsTheGlobalErrorFlag()
    {
        DesiredFaceDistortionComponent.ErrorFlagSet = false;
        var s = new AnimationScheduler(new Sink(), new Random(1));
        // fill the live Head track to its limit BEFORE the first ProceduralLive tick, and make the wiggle gate
        // open so UpdateLiveAnimation runs on that first tick (its head append is then refused).
        s.LiveIdleParameters[LiveIdleParam.TimeBeforeWiggleMotions_ms] = 0;
        for (int i = 0; i < 1001; i++) Assert.True(s.StreamLive(new HeadKeyframe(0, 1, 0, 0), 0));
        Assert.False(s.StreamLive(new HeadKeyframe(0, 1, 0, 0), 0));      // the track refuses above 1000 frames
        s.LiveIdleInputs.PickingOrPlacing = () => false;
        s.LiveIdleInputs.Moving = () => true;                            // body decrements
        s.LiveIdleInputs.LiftNotInPosition = () => true;                 // lift decrements
        s.LiveIdleInputs.HeadNotInPosition = () => false;                // head generates and the append fails
        s.LiveIdleInputs.LockedTracks = () => 0;
        s.LiveIdleInputs.Carrying = () => false;
        s.PushIdleAnimation(AnimationTrigger.ProceduralLive, "t");
        var log = new List<string>();
        s.Log = log.Add;

        s.Advance(0);

        Assert.True(DesiredFaceDistortionComponent.ErrorFlagSet);
        Assert.Contains(log, l => l.Contains("AnimationStreamer.Update.LiveUpdateFailed"));
    }

    // ---------------------------------------------------------------- batch 2: the FreeplayStack wiring

    /// <summary>
    /// B-FACE batch 2: <c>FreeplayStack.Create</c> wires the scheduler's DesiredFaceDistortion seam to the needs
    /// manager's getter, sets the manager's tick seam to the engine timer and the component's RNG to the context
    /// RNG (D07/D08/D18, P07).
    /// </summary>
    [Fact]
    public void FreeplayStackWiresTheSchedulerDistortionSeamToTheNeedsManager()
    {
        var obb = ObbRoot();
        Assert.NotNull(obb);
        using var rig = new Rig();
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Random = new Random(3) };
        using var stack = FreeplayStack.Create(obb!, rig.Robot, ctx, () => 0, withReactions: false);
        var scheduler = rig.Robot.Animations.Scheduler;
        Assert.NotNull(stack.Needs.DesiredFaceDistortion);
        Assert.Same(stack.Needs, scheduler.DesiredFaceDistortion!.Target);
        Assert.Same(scheduler.ContextRandom, stack.Needs.DistortionRng);
        Assert.Equal(rig.Robot.Engine.Timer.TickCount, stack.Needs.TickCount());
    }

    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (File.Exists(Path.Combine(r, "assets", "cozmo_resources", "config", "engine", "needs_handlers_config.json"))) return r;
            d = d.Parent;
        }
        return null;
    }
}