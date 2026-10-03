using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Tests for the mood model. The headline result is a negative one and is asserted deliberately:
/// mood does not change animation selection in this build.
/// </summary>
public class MoodTests
{
    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (File.Exists(Path.Combine(r, "assets", "cozmo_resources", "config", "engine", "mood_config.json")))
                return r;
            d = d.Parent;
        }
        return null;
    }

    [Fact]
    public void TheShippedConfigFilesLoadDespiteTheirComments()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var m = MoodModel.Load(obb);
        Assert.NotEmpty(m.Events);
        Assert.NotEmpty(m.DecayGraphs);
        Assert.Empty(m.UnknownEmotions);
    }

    /// <summary>The shipped files use // comments, which JSON does not allow.</summary>
    [Fact]
    public void CommentsAreStrippedRatherThanRejected()
    {
        var text = "{ \"a\": 1, // a note\n  \"b\": 2 }";
        var clean = MoodModel.Decomment(text);
        Assert.DoesNotContain("a note", clean);
        Assert.Contains("\"b\"", clean);
    }

    [Fact]
    public void DecayInterpolatesBetweenNodesAndFlattensOutside()
    {
        var g = new DecayGraph("default", new (double, double)[] { (0, 1), (10, 1), (150, 0) });
        Assert.Equal(1, g.At(-5));
        Assert.Equal(1, g.At(0));
        Assert.Equal(1, g.At(10));
        Assert.Equal(0.5, g.At(80), 2);
        Assert.Equal(0, g.At(150));
        Assert.Equal(0, g.At(1000));
    }

    [Fact]
    public void AnEventMovesTheAxesItNamesAndThenFades()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var model = MoodModel.Load(obb);
        var ev = model.Events.FirstOrDefault(e => e.Affectors.Count > 0);
        Assert.NotNull(ev);

        var mood = new MoodState(model);
        Assert.True(mood.Trigger(ev!.Name, 0));
        var moved = ev.Affectors[0].Emotion;
        Assert.NotEqual(0, mood[moved]);

        // wind well past the longest shipped decay curve: MoodManager::Update is the only decay (0x0067B5D4), its first step is the
        // 1e-4f floor (last time 0, 0x0067B5EE..0x0067B604), the second takes the rest
        mood.Advance(1);
        mood.Advance(1000);
        Assert.Equal(0, mood[moved], 3);
    }

    [Fact]
    public void AnUnknownEventChangesNothing()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var mood = new MoodState(MoodModel.Load(obb));
        Assert.False(mood.Trigger("NotAnEventThatExists", 0));
        Assert.All(mood.Snapshot().Values, v => Assert.Equal(0, v));
    }

    /// <summary>
    /// The finding that bounds the whole mood layer: every animation-group entry in this build is
    /// "Default", so no mood can change what is selected. If a future build adds mood-specific
    /// alternatives this test fails, which is exactly when the selector would need writing.
    /// </summary>
    [Fact, Trait("Category", "Exhaustive")]
    public void AnimationSelectionIsMoodInvariantInThisBuild()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var groups = Path.Combine(obb, "assets", "cozmo_resources", "assets", "animationGroups");
        if (!Directory.Exists(groups)) return;

        var moods = new HashSet<string>(StringComparer.Ordinal);
        int entries = 0;
        foreach (var f in Directory.EnumerateFiles(groups, "*.json", SearchOption.AllDirectories))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(f));
            if (!doc.RootElement.TryGetProperty("Animations", out var anims)) continue;
            foreach (var a in anims.EnumerateArray())
            {
                entries++;
                if (a.TryGetProperty("Mood", out var m) && m.GetString() is { } s) moods.Add(s);
            }
        }
        Assert.True(entries > 1000, $"only {entries} group entries were read");
        Assert.Equal(new[] { "Default" }, moods.OrderBy(x => x));
    }

    // ---------------------------------------------------------------- M7-013

    /// <summary>
    /// A value saturates at plus or minus one: <c>Emotion::Add</c> clamps the sum between the two
    /// literals it loads at 0x00679622 and 0x00679630 before storing it.
    /// </summary>
    [Fact]
    public void AnAxisSaturatesAtOne()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var model = MoodModel.Load(obb);
        var ev = model.Events.FirstOrDefault(e => e.Affectors.Count > 0 && Math.Abs(e.Affectors[0].Value) > 0.1);
        Assert.NotNull(ev);
        var axis = ev!.Affectors[0].Emotion;
        double sign = Math.Sign(ev.Affectors[0].Value);

        var mood = new MoodState(model);
        for (int i = 0; i < 100; i++) mood.Trigger(ev.Name, 0);      // no time passes, so nothing decays
        Assert.Equal(sign, mood[axis], 6);
    }

    /// <summary>
    /// The decay curve is flat outside its nodes and linear between them, which is the whole of
    /// <c>GraphEvaluator2d::EvaluateY</c> at 0x00804BD0.
    /// </summary>
    [Fact]
    public void TheDecayCurveIsFlatOutsideItsNodesAndLinearBetween()
    {
        var g = new DecayGraph("test", new[] { (10.0, 1.0), (20.0, 0.5), (30.0, 0.0) });
        Assert.Equal(1.0, g.At(-100));      // below the first node
        Assert.Equal(1.0, g.At(0));
        Assert.Equal(1.0, g.At(10));
        Assert.Equal(0.75, g.At(15), 6);    // linear between
        Assert.Equal(0.5, g.At(20), 6);
        Assert.Equal(0.0, g.At(30), 6);
        Assert.Equal(0.0, g.At(1_000));     // above the last node

        // a single node is that node's value at every x, as EvaluateY returns before the loop
        Assert.Equal(1.0, new DecayGraph("one", new[] { (0.0, 1.0) }).At(10_000));
    }

    /// <summary>
    /// The decay clock is only restarted by a change that is bigger than 0.05 and pushes the value
    /// further from zero without flipping its sign - the three tests at 0x006796A8, 0x0067968A and
    /// 0x006796AE. A change that fails any of them moves the value and leaves it decaying on the
    /// schedule it was already on.
    /// </summary>
    [Fact]
    public void OnlyAChangeThatDrivesTheValueOnwardsRestartsTheDecayClock()
    {
        Assert.Equal(0x3D4CCCCDu, unchecked((uint)BitConverter.SingleToInt32Bits(MoodState.DecayResetThreshold)));    // 0.05f, 0x0067967E

        // Confident decays to zero 70 s after its clock was last restarted. The engine decays only in MoodManager::Update, so each
        // scenario ticks it once a second (the first tick step is the 1e-4f floor, so the decay clock is 1e-4 short of the time).
        var model = new MoodModel();
        model.AddDecayGraph(new DecayGraph("Confident", new[] { (0.0, 1.0), (30.0, 1.0), (60.0, 0.5), (70.0, 0.0) }));
        model.AddEvent(new EmotionEvent("Big", new[] { new EmotionAffector(EmotionType.Confident, 0.8) }));
        model.AddEvent(new EmotionEvent("Nudge", new[] { new EmotionAffector(EmotionType.Confident, 0.02) }));
        model.AddEvent(new EmotionEvent("Back", new[] { new EmotionAffector(EmotionType.Confident, -0.2) }));

        static void Run(MoodState m, int from, int to) { for (int t = from; t <= to; t++) m.Advance(t); }

        // a nudge at 40 s does not restart the clock, so the value is gone by 71 s (clock 70.0001 s)
        var a = new MoodState(model);
        a.Trigger("Big", 0);
        Run(a, 1, 40);
        a.Trigger("Nudge", 40);
        Run(a, 41, 71);
        Assert.Equal(0, a[EmotionType.Confident], 6);

        // nor does a change back towards zero
        var b = new MoodState(model);
        b.Trigger("Big", 0);
        Run(b, 1, 40);
        b.Trigger("Back", 40);
        Run(b, 41, 71);
        Assert.Equal(0, b[EmotionType.Confident], 6);

        // but a second big push in the same direction does, so there is still something left at 71 s
        var c = new MoodState(model);
        c.Trigger("Big", 0);
        Run(c, 1, 40);
        c.Trigger("Big", 40);
        Run(c, 41, 71);
        Assert.True(c[EmotionType.Confident] > 0.4,
                    $"the clock was not restarted: {c[EmotionType.Confident]}");
        Run(c, 72, 111);
        Assert.Equal(0, c[EmotionType.Confident], 6);
    }

    /// <summary>
    /// M7-013: <c>Emotion::Add</c> 0x00679618 resets the decay clock <b>unconditionally on a sign flip</b>
    /// (<c>teq</c> at 0x006796A8, <c>bne #0x6796bc</c> at 0x006796AC), on top of the kept-sign three-test
    /// case. Reachable from the shipped events: <c>PickupSucceeded</c> gives Confident +0.2,
    /// <c>DrivingActionFailedWithAbort</c> gives Confident -0.2, so the second flips the sign at 40 s and
    /// the clock must restart there.
    /// </summary>
    [Fact]
    public void ASignFlipRestartsTheDecayClock()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var model = MoodModel.Load(obb);
        var positive = model.Events.First(e => e.Affectors.Any(a => a.Emotion == EmotionType.Confident && a.Value >= 0.2));
        var negative = model.Events.First(e => e.Affectors.Any(a => a.Emotion == EmotionType.Confident && a.Value <= -0.2));

        var mood = new MoodState(model);
        mood.Trigger(positive.Name, 0);
        Assert.True(mood[EmotionType.Confident] > 0);
        for (int t = 1; t <= 40; t++) mood.Advance(t);

        mood.Trigger(negative.Name, 40);
        double flipped = mood[EmotionType.Confident];
        Assert.True(flipped < 0, $"the shipped negative event did not flip the sign: {flipped}");

        // The clock was zeroed at the flip, so 30 s later the value is the post-flip value, not decayed
        // to zero on the schedule it was already on.
        for (int t = 41; t <= 70; t++) mood.Advance(t);
        Assert.Equal(flipped, mood[EmotionType.Confident], 6);
    }

    // ---------------------------------------------------------------- M7-013: the engine float arithmetic

    private static float F(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));
    private static uint Bits(float f) => unchecked((uint)BitConverter.SingleToInt32Bits(f));

    private static MoodState Linear(double happy, out MoodModel model, double x1 = 10.0)
    {
        model = new MoodModel();
        model.AddDecayGraph(new DecayGraph("Happy", new[] { (0.0, 1.0), (x1, 0.0) }));
        model.AddEvent(new EmotionEvent("E", new[] { new EmotionAffector(EmotionType.Happy, happy) }));
        return new MoodState(model);
    }

    /// <summary>
    /// M7-013: MoodManager::Update 0x0067B5D4 and Emotion::Update 0x006795A4 in float32. The first step is the 1e-4f
    /// floor (the stored last time +0x130 is 0; literal 0x38D1B717 at 0x0067B5EA), then t - last; per update the decay clock is
    /// clock + dt (vadd.f32 0x006795C6) and the value is multiplied by new / old (vdiv.f32 0x006795E6, vmul.f32
    /// 0x006795F8) with the graph read by EvaluateY (0x00804BD0: x gap 10, t = (x - 0) / 10, y = 1 + t * (0 - 1), all float). The
    /// expected value bits come from a float32 emulation of those instructions on a (0,1),(10,0) graph: after the 1e-4f step
    /// 0x3EFFFF58; after a 1.0 s step 0x3EE665BF; after a 3.0 s step 0x3E9998F2.
    /// </summary>
    [Fact]
    public void M7_013_AdvanceIsTheEnginesFloat32UpdateWithTheFirstStepAtTheFloor()
    {
        var mood = Linear(0.5, out _);
        Assert.True(mood.Trigger("E", 0));
        Assert.Equal(0x3F000000u, Bits((float)mood[EmotionType.Happy]));
        mood.Advance(1);
        Assert.Equal(0x3EFFFF58u, Bits((float)mood[EmotionType.Happy]));
        mood.Advance(2);
        Assert.Equal(0x3EE665BFu, Bits((float)mood[EmotionType.Happy]));
        mood.Advance(5);
        Assert.Equal(0x3E9998F2u, Bits((float)mood[EmotionType.Happy]));
    }

    /// <summary>
    /// M7-013: the engine affector value is a float: an event of 0.1 adds the float 0.1f (0x3DCCCCCD), widened, to the value;
    /// the public double surface carries the exact value of the float, not 0.1.
    /// </summary>
    [Fact]
    public void M7_013_AnAffectorIsAddedAsAFloat()
    {
        var mood = Linear(0.1, out _);
        mood.Trigger("E", 0);
        Assert.Equal((double)F(0x3DCCCCCD), mood[EmotionType.Happy]);
        Assert.NotEqual(0.1, mood[EmotionType.Happy]);
    }

    /// <summary>
    /// M7-013: StaticMoodData builds every decay graph as the built-in curve first (InitDecayGraphs 0x0067CDC3): (0,1), (15,1),
    /// (60,0.9f), (150,0.6f), (300,0); a model without any graph decays with it, and is gone past 300 s.
    /// </summary>
    [Fact]
    public void M7_013_AModelWithoutGraphsDecaysOnTheBuiltInCurve()
    {
        var model = new MoodModel();
        model.AddEvent(new EmotionEvent("E", new[] { new EmotionAffector(EmotionType.Happy, 0.5) }));
        Assert.Same(MoodModel.BuiltInDecay, model.DecayFor(EmotionType.Happy));
        Assert.Equal(new (double, double)[] { (0, 1), (15, 1), (60, (double)F(0x3F666666)), (150, (double)F(0x3F19999A)), (300, 0) },
                     MoodModel.BuiltInDecay.Nodes);
        var mood = new MoodState(model);
        mood.Trigger("E", 0);
        mood.Advance(1);
        mood.Advance(14);
        Assert.Equal(0x3F000000u, Bits((float)mood[EmotionType.Happy]));      // still on the flat first 15 s
        mood.Advance(400);
        Assert.Equal(0u, Bits((float)mood[EmotionType.Happy]));
    }

    /// <summary>
    /// M7-013: MoodManager::TriggerEmotionEvent 0x0067B85C calls Emotion::Add only (0x0067B8F6..0x0067B902): no
    /// Emotion::Update, no decay. Triggering at t = 100 with no update in between leaves the value where the last update put it,
    /// plus the affector (the old code decayed it to the time of the trigger first).
    /// </summary>
    [Fact]
    public void M7_013_TriggerDoesNotDecayAnything()
    {
        var mood = Linear(0.25, out _);
        mood.Trigger("E", 0);
        mood.Advance(1);
        mood.Advance(5);
        float afterUpdates = (float)mood[EmotionType.Happy];
        mood.Trigger("E", 100);
        Assert.Equal(Bits(afterUpdates + F(0x3E800000)), Bits((float)mood[EmotionType.Happy]));      // 0.25f = 0x3E800000
    }

    /// <summary>
    /// M7-013: a step below 1e-4f is replaced by 1e-4f with a warning (0x0067B608..0x0067B66C): an update at the same time as the
    /// last advances the clock by 1e-4f and logs once; the first step (last time 0) is the floor without a warning.
    /// </summary>
    [Fact]
    public void M7_013_AStepBelowTheFloorIsTheFloorWithAWarning()
    {
        var mood = Linear(0.5, out _);
        var log = new List<string>();
        mood.Log = log.Add;
        mood.Trigger("E", 0);
        mood.Advance(5);                       // last = 0: the floor, no warning
        Assert.Empty(log);
        float afterFirst = (float)mood[EmotionType.Happy];
        Assert.Equal(0x3EFFFF58u, Bits(afterFirst));
        mood.Advance(5);                       // dt = 0 < 1e-4f: the floor again, with a warning
        Assert.Single(log);
        Assert.True((float)mood[EmotionType.Happy] < afterFirst);
    }

    /// <summary>
    /// M7-013: the history ring of Emotion (ctor 0x00679406..0x00679428: capacity 0x80, one initial {0.0f, 0.0f} sample; append
    /// 0x0067945C..0x00679498 after each Emotion::Update; GetHistoryValueTicksAgo(n) 0x006794F8: n = 0 or an empty ring gives
    /// the current value, otherwise the sample at (head + (count > n ? count - n : 0)) mod 0x80). With the samples numbered
    /// from the initial one s0, after N updates the ring holds the last min(N + 1, 128) of them and n ticks ago is s(N + 1 - n)
    /// while count > n, the oldest otherwise. Checked across the wrap (N = 200: s73..s200 held).
    /// </summary>
    [Fact]
    public void M7_013_TheHistoryRingHoldsTheLastHundredAndTwentyEightSamples()
    {
        var mood = Linear(0.8, out _, x1: 1000.0);              // a slow decay, so every sample differs
        mood.Trigger("E", 0);
        var vals = new List<float> { 0f };                      // s0: the constructor sample {0, 0}
        Assert.Equal(0u, Bits(mood.GetHistoryValueTicksAgo(EmotionType.Happy, 60)));
        Assert.Equal(Bits((float)mood[EmotionType.Happy]), Bits(mood.GetHistoryValueTicksAgo(EmotionType.Happy, 0)));
        for (int t = 1; t <= 200; t++)
        {
            mood.Advance(t);
            vals.Add((float)mood[EmotionType.Happy]);
        }
        const int N = 200;
        Assert.Equal(Bits(vals[N]), Bits(mood.GetHistoryValueTicksAgo(EmotionType.Happy, 0)));        // the current value
        Assert.Equal(Bits(vals[N]), Bits(mood.GetHistoryValueTicksAgo(EmotionType.Happy, 1)));        // the newest sample
        foreach (int n in new[] { 2, 60, 61, 100, 127 })
            Assert.Equal(Bits(vals[N + 1 - n]), Bits(mood.GetHistoryValueTicksAgo(EmotionType.Happy, (uint)n)));
        // count == 128 is not above 128, so 128 and beyond answer the oldest held sample, s73
        foreach (int n in new[] { 128, 129, 1000 })
            Assert.Equal(Bits(vals[73]), Bits(mood.GetHistoryValueTicksAgo(EmotionType.Happy, (uint)n)));
        // the values really differ, so the checks above can tell the samples apart
        Assert.NotEqual(Bits(vals[N - 59]), Bits(vals[N]));
    }

    /// <summary>
    /// M7-013 / M8-003: before the ring wraps (N = 10: 11 samples held, s0..s10) 60 ticks ago is the oldest, s0 = 0.0f
    /// (count 11 is not above 60: the head, 0x00679508..0x0067950C).
    /// </summary>
    [Fact]
    public void M7_013_AShortHistoryAnswersItsOldestSample()
    {
        var mood = Linear(0.8, out _, x1: 1000.0);
        mood.Trigger("E", 0);
        for (int t = 1; t <= 10; t++) mood.Advance(t);
        Assert.Equal(0u, Bits(mood.GetHistoryValueTicksAgo(EmotionType.Happy, 60)));
        Assert.NotEqual(0u, Bits(mood.GetHistoryValueTicksAgo(EmotionType.Happy, 5)));
    }
}
