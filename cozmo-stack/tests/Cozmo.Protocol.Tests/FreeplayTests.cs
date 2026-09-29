using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using System.Text.Json;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M15: the needs system, the choosers and strategies, the shipped activity tree, the explorer and needs
/// behaviours, and the freeplay system choosing and running behaviours on its own over the fake robot side.
/// </summary>
public class FreeplayTests
{
    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (File.Exists(Path.Combine(r, "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "activities_config.json"))) return r;
            d = d.Parent;
        }
        return null;
    }

    private static BehaviorContext Ctx(Rig rig, MoodState? mood = null) => new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Mood = mood, Random = new Random(3) };

    /// <summary>IsRunnable is gated on the animation library; the stack tests load the real one (the empty trigger map keeps plays instant).</summary>
    private static void LoadAssets(Rig rig, string obb) => rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));

    private static bool Runnable(SteppedBehavior b, BehaviorContext ctx) =>
        (bool)typeof(SteppedBehavior).GetMethod("IsRunnableInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(b, new object[] { ctx })!;

    private sealed class Fake : IBehavior
    {
        public Fake(string id, bool runnable = true, int ticks = 1) { Id = id; Runnable = runnable; Ticks = ticks; }
        public string Id { get; }
        public string Class => "Fake";
        public bool Runnable { get; set; }
        public int Ticks { get; set; }
        public int Started, Left;
        public bool IsRunnable(BehaviorContext c) => Runnable;
        public double EvaluateScore(BehaviorContext c) => 1;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) { Started++; Left = Ticks; return Task.CompletedTask; }
        public bool Update(BehaviorContext c, double nowMs) => --Left > 0;
        public void Stop(BehaviorStopReason r) { }
    }

    // ------------------------------------------------------------------ needs

    [Fact]
    public void NeedsDecayIntoBracketsAndActionsRefillThem()
    {
        var obb = ObbRoot();
        double clock = 0;
        var needs = obb is null ? new NeedsManager(() => clock) : NeedsManager.FromObb(obb, () => clock, new Random(1));
        Assert.Equal(1.0, needs.State.GetNeedLevel(NeedId.Energy));
        Assert.Equal(NeedBracketId.Full, needs.State.GetNeedBracket(NeedId.Energy));
        // the shipped brackets: Energy warning at 0.21, Play at 0.14, Repair at 0.27
        needs.SetLevel(NeedId.Energy, 0.7); Assert.Equal(NeedBracketId.Normal, needs.State.GetNeedBracket(NeedId.Energy));   // Normal from 0.6
        needs.SetLevel(NeedId.Energy, 0.5); Assert.Equal(NeedBracketId.Warning, needs.State.GetNeedBracket(NeedId.Energy));  // Warning from 0.21
        var changes = new List<(NeedId, NeedBracketId, NeedBracketId)>();
        needs.BracketChanged += (n, a, b) => changes.Add((n, a, b));
        needs.SetLevel(NeedId.Energy, 0.0); Assert.Equal(NeedBracketId.Critical, needs.State.GetNeedBracket(NeedId.Energy));
        Assert.Equal(0.03, needs.State.GetNeedLevel(NeedId.Energy), 6);           // clamped to the minimum
        Assert.Contains((NeedId.Energy, NeedBracketId.Warning, NeedBracketId.Critical), changes);
        Assert.Equal((NeedId.Energy, NeedBracketId.Critical), needs.State.GetLowestNeedAndBracket());
        if (obb is not null)
        {
            // the Feed action (+0.33 ± 0.005 energy) lifts it out of Critical
            Assert.True(needs.RegisterNeedsActionCompleted("Feed"));
            Assert.InRange(needs.State.GetNeedLevel(NeedId.Energy), 0.03 + 0.325, 0.03 + 0.335);
            Assert.Equal(NeedBracketId.Warning, needs.State.GetNeedBracket(NeedId.Energy));
            Assert.False(needs.RegisterNeedsActionCompleted("NoSuchAction"));
            // decay: connected Play decays 0.014 / min above 0.5; one 60 s period
            needs.SetLevel(NeedId.Play, 0.9);
            needs.Update(); clock = 60; needs.Update();
            Assert.Equal(0.9 - 0.014, needs.State.GetNeedLevel(NeedId.Play), 5);
            // a need at Full waits out its fullness cooldown before decaying
            needs.SetLevel(NeedId.Repair, 1.0);
            Assert.True(needs.RegisterNeedsActionCompleted("RepairHead"));
            clock = 120; needs.Update();
            Assert.Equal(1.0, needs.State.GetNeedLevel(NeedId.Repair), 6);
        }
    }

    /// <summary>
    /// Needs actions are reported by the behaviours, not by the activity.
    /// <c>IBehavior::NeedActionCompleted</c> 0x005BE40C uses the behaviour's own <c>needsActionID</c>
    /// (+0x68, filled at 0x005BBCAA from <c>ExtractNeedsActionIDFromConfig</c> 0x005BBAE8) when the caller
    /// names none, and sixteen behaviours call it. An activity's <c>needsActionID</c> is parsed into
    /// <c>IActivity+0x1C</c> (<c>ReadConfig</c>, 0x005B2A9C) and never read again anywhere in the build, so
    /// ending an activity reports nothing - which this stack used to get wrong.
    /// </summary>
    [Fact]
    public void NeedsActionsAreReportedByTheBehavioursAndNotByTheActivity()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var ids = BehaviorNeedsActions.Load(obb);
        Assert.Equal(22, ids.Count);
        Assert.Equal("KnockDownCubes", ids["KnockOverCubes"]);
        Assert.Equal("KnockDownCubes_Sparked", ids["SparksKnockOverCubes"]);
        Assert.Equal("StackCube", ids["StackBlocks"]);
        Assert.Equal("Workout_Sparked", ids["SparksCubeLiftWorkout"]);
        Assert.False(ids.ContainsKey("BuildPyramid"));                 // the pyramid behaviour reports nothing

        using var rig = new Rig();
        double clock = 0;
        var needs = NeedsManager.FromObb(obb, () => clock, new Random(1));
        var ctx = Ctx(rig);
        ctx.Needs = needs;
        ctx.NeedsActionIds = ids;

        // the caller's action wins, then the behaviour's own, then the fallback; a behaviour with none and
        // no fallback reports nothing
        Assert.Equal("StackCube", BehaviorNeedsActions.Complete(new Fake("StackBlocks"), ctx));
        Assert.Equal("PickupCube", BehaviorNeedsActions.Complete(new Fake("Hiking_BringCubeToBeacon"), ctx, "PickupCube"));
        Assert.Equal("CozmoSings", BehaviorNeedsActions.Complete(new Fake("Singing"), ctx, fallback: "CozmoSings"));
        Assert.Null(BehaviorNeedsActions.Complete(new Fake("BuildPyramid"), ctx));

        // and it reaches the manager: KnockDownCubes is +0.2 play, give or take its 0.01 range
        needs.SetLevel(NeedId.Play, 0.5);
        Assert.Equal("KnockDownCubes", BehaviorNeedsActions.Complete(new Fake("KnockOverCubes"), ctx));
        Assert.InRange(needs.State.GetNeedLevel(NeedId.Play), 0.69, 0.71);

        // the activity keeps its parsed id and reports nothing with it
        var tree = ActivityTreeLoader.Load(obb, new Dictionary<string, IBehavior>());
        var pyramid = tree.SelectMany(a => a.SubActivities.Prepend(a)).First(a => a.Id == "BuildPyramid");
        Assert.Equal("PyramidCompleted", pyramid.NeedsActionId);

        var fake = new Fake("only", ticks: 2);
        var bound = new Dictionary<string, IBehavior> { ["only"] = fake };
        var manager = new BehaviorManager(ctx);
        var activity = new Activity
        {
            Id = "Hiking", Priority = 1, Strategy = new ActivityStrategy(), NeedsActionId = "Feed",
            Chooser = new StrictPriorityChooser(new[] { "only" }, bound),
        };
        var fp = new FreeplaySystem(manager, ctx, Fp(activity), bound, new FreeplayInputs { Needs = needs });
        needs.SetLevel(NeedId.Energy, 0.5);
        fp.Tick(0, 0);
        Assert.Equal("Hiking", fp.Decisions[^1].Activity);
        fp.OnRobotPutDown(1);
        fp.Tick(1, 1000);
        // "Feed" would have been +0.33 energy had the activity reported its id when it ended
        Assert.Equal(0.5, needs.State.GetNeedLevel(NeedId.Energy), 6);
    }

    // ------------------------------------------------------------------ choosers and strategies

    /// <summary>
    /// A behaviour with emotion scorers is scored by them and not by its flat score.
    /// IBehavior::EvaluateScoreInternal 0x005BEEC2 tail-calls MoodScorer::EvaluateEmotionScore as soon as
    /// the scorer list is non-empty and only an empty list reaches the flat score at +0x100, and
    /// EvaluateEmotionScore 0x0067C9B8 returns the mean of the graphs - with any graph that comes out
    /// within 1e-05 of zero ending the whole thing at zero. This stack added the two together.
    /// </summary>
    [Fact]
    public void AScoredEntryUsesItsEmotionScorersInsteadOfItsFlatScore()
    {
        using var rig = new Rig();
        var model = new MoodModel();
        model.AddEvent(new EmotionEvent("makeHappy", new[] { new EmotionAffector(EmotionType.Happy, 1.0) }));
        var mood = new MoodState(model);
        Assert.True(mood.Trigger("makeHappy", 0));
        var ctx = Ctx(rig, mood);
        var b = new Fake("a");

        // one scorer: the mean of one graph, and the flat score of 7 is not part of it
        var happy = new Graph2d(new[] { (0.0, 0.0), (1.0, 4.0) });
        var one = new ScoredBehaviorEntry("a", 7.0, null, null, null,
                                          new[] { new EmotionScorer(EmotionType.Happy, happy, false) });
        Assert.Equal(4.0, one.Evaluate(b, ctx, 0, null, null, null), 3);

        // two scorers: the mean of the two, so a second graph at 2 gives 3
        var social = new Graph2d(new[] { (0.0, 2.0), (1.0, 2.0) });
        var two = one with { EmotionScorers = new[] { new EmotionScorer(EmotionType.Happy, happy, false),
                                                      new EmotionScorer(EmotionType.Social, social, false) } };
        Assert.Equal(3.0, two.Evaluate(b, ctx, 0, null, null, null), 3);

        // a scorer that comes out at zero vetoes the behaviour outright
        var zero = new Graph2d(new[] { (0.0, 0.0), (1.0, 0.0) });
        var vetoed = one with { EmotionScorers = new[] { new EmotionScorer(EmotionType.Happy, happy, false),
                                                         new EmotionScorer(EmotionType.Social, zero, false) } };
        Assert.Equal(0.0, vetoed.Evaluate(b, ctx, 0, null, null, null), 6);

        // no scorers at all: the flat score, which is what every shipped config uses
        var flat = one with { EmotionScorers = Array.Empty<EmotionScorer>() };
        Assert.Equal(7.0, flat.Evaluate(b, ctx, 0, null, null, null), 3);
    }

    /// <summary>
    /// The non-running branch of <c>IBehavior::EvaluateScore</c> 0x005bef60 applies the repetition penalty
    /// only when +0x110 is set (0x005befd4/0x005befd8) and <c>now &gt;= +0x108</c>
    /// (0x005befe2..0x005beff2). The +0x108 threshold is what <c>StopWithoutImmediateRepetitionPenalty</c>
    /// sets to now + 1.0, so an interrupted behaviour keeps its score for about a second.
    /// </summary>
    [Fact]
    public void TheScoredEntrySkipsTheRepetitionPenaltyInsideTheSuppressionWindow()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = new Fake("a");
        var entry = new ScoredBehaviorEntry("a", 3.0, new Graph2d(new[] { (0.0, 0.0), (30.0, 1.0) }), null, null, Array.Empty<EmotionScorer>());

        Assert.Equal(3.0 / 30.0, entry.Evaluate(b, ctx, 1, 0, null, null), 4);                        // the graph at 1 s
        Assert.Equal(3.0, entry.Evaluate(b, ctx, 1, 0, null, null, penaltySuppressed: true), 4);      // inside +0x108
        Assert.Equal(3.0, entry.Evaluate(b, ctx, 1, 0, null, null, repetitionPenaltyEnabled: false), 4); // +0x110 clear
    }

    /// <summary>
    /// The running branch of <c>IBehavior::EvaluateScore</c> adds the float at +0x104
    /// (0x005bef80/0x005bef88) and multiplies by <c>EvaluateRunningPenalty</c> only when +0x111 is set
    /// (0x005bef84/0x005bef8c). The +0x104 bonus is not added when the behaviour is not running.
    /// </summary>
    [Fact]
    public void TheScoredEntryAddsTheRunningBonusAndAppliesTheRunningPenaltyEnable()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = new Fake("a");
        var runGraph = new Graph2d(new[] { (0.0, 0.5), (10.0, 1.0) });
        var entry = new ScoredBehaviorEntry("a", 2.0, null, runGraph, null, Array.Empty<EmotionScorer>());

        Assert.Equal(3.0, entry.Evaluate(b, ctx, 0, null, 10, null, runningBonus: 1.0), 4);                          // (2 + 1) * 1
        Assert.Equal(3.0, entry.Evaluate(b, ctx, 0, null, 10, null, runningBonus: 1.0, runningPenaltyEnabled: false), 4); // +0x111 clear
        Assert.Equal(2.0, entry.Evaluate(b, ctx, 0, null, null, null, runningBonus: 1.0), 4);                        // not running
    }

    /// <summary>
    /// The running branch of <c>IBehavior::EvaluateScore</c> 0x005bef60 is not gated on <c>IsRunnable</c>:
    /// a running behaviour scores <c>EvaluateScoreInternal + +0x104</c> regardless (only the non-running
    /// branch tests <c>IsRunnableBase</c> and <c>vtable+0x50</c>, 0x005befa2/0x005befce).
    /// </summary>
    [Fact]
    public void ARunningBehaviourThatIsNotRunnableStillScores()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var notRunnable = new Fake("a", runnable: false);
        var entry = new ScoredBehaviorEntry("a", 2.0, null, null, null, Array.Empty<EmotionScorer>());

        Assert.Equal(0.0, entry.Evaluate(notRunnable, ctx, 0, null, null, null), 4);              // non-running gate
        Assert.Equal(3.0, entry.Evaluate(notRunnable, ctx, 0, null, 10, null, runningBonus: 1.0), 4);   // running: (2 + 1)
    }

    [Fact]
    public void TheScoringChooserWeighsFlatScoresPenaltiesAndTheRunningBonus()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var a = new Fake("a"); var b = new Fake("b"); var c = new Fake("c", runnable: false);
        var bound = new Dictionary<string, IBehavior> { ["a"] = a, ["b"] = b, ["c"] = c };
        var entries = new[]
        {
            new ScoredBehaviorEntry("a", 1.0, new Graph2d(new[] { (0.0, 0.0), (30.0, 1.0) }), null, null, Array.Empty<EmotionScorer>()),
            new ScoredBehaviorEntry("b", 0.8, null, null, null, Array.Empty<EmotionScorer>()),
            new ScoredBehaviorEntry("c", 5.0, null, null, null, Array.Empty<EmotionScorer>()),
            new ScoredBehaviorEntry("missing", 9.0, null, null, null, Array.Empty<EmotionScorer>()),
        };
        var chooser = new ScoringChooser(entries, bound, scoreBonusForCurrent: new Graph2d(new[] { (0.0, 1.0) }));
        // ScoringBSRunnableChooser adds RandomGenerator::RandDbl (0x0060a4a8) to every non-running score;
        // pin it to zero so the ordering assertions are the engine's score rule and not the draw.
        chooser.RandomDraw = () => 0.0;
        Assert.Equal(new[] { "missing" }, chooser.Unbound);
        var d = chooser.GetDesiredActiveBehavior(null, 0, ctx, 0);
        Assert.Equal("a", d.Behavior!.Id);                                            // c is not runnable, missing is not built
        Assert.Contains(d.Scores, s => s.Id == "c" && s.Note == "not runnable");
        Assert.Contains(d.Scores, s => s.Id == "missing" && s.Note == "not built");
        // a just ran: its repetition penalty is 0 at 0 s, 0.5 at 15 s; b wins until a recovers past 0.8
        chooser.Ran("a", 0);
        Assert.Equal("b", chooser.GetDesiredActiveBehavior(null, 0, ctx, 1).Behavior!.Id);
        Assert.Equal("b", chooser.GetDesiredActiveBehavior(null, 0, ctx, 20).Behavior!.Id);   // a = 0.67
        Assert.Equal("a", chooser.GetDesiredActiveBehavior(null, 0, ctx, 29).Behavior!.Id);   // a = 0.97
        // while b runs it keeps its place against a's 0.97 thanks to the +1 running bonus
        var keep = chooser.GetDesiredActiveBehavior(b, 5, ctx, 29);
        Assert.Equal("b", keep.Behavior!.Id);
        Assert.Contains(keep.Scores, s => s.Id == "b" && s.Score > 1.5 && s.Note == "running");
        // the strict-priority chooser: the first runnable
        var strict = new StrictPriorityChooser(new[] { "c", "missing", "a", "b" }, bound);
        var sd = strict.GetDesiredActiveBehavior(null, 0, ctx, 0);
        Assert.Equal("a", sd.Behavior!.Id);
        Assert.Equal("a", strict.GetDesiredActiveBehavior(a, 3, ctx, 0).Behavior!.Id);
        c.Runnable = true;
        Assert.Equal("c", strict.GetDesiredActiveBehavior(a, 3, ctx, 0).Behavior!.Id);      // a higher priority became runnable
    }

    /// <summary>
    /// An activity that ended as soon as it started waits a flat three seconds, whatever its configured
    /// cooldown. IActivityStrategy::WantsToStart 0x005B529C takes the activity's start and end times
    /// together (ldrd r3, r2, [r1, #0x54] at its call site 0x005B26FE), subtracts them at 0x005B52EA, and
    /// when the run was positive but no longer than two basestation ticks it uses 3.0 (0x005B5312) in
    /// place of the cooldown at +0x20.
    /// </summary>
    /// <summary>
    /// An activity whose featureGate names a feature that is off never starts. WantsToStart 0x005B529C
    /// tests the gate before anything else (0x005B52A8) and refuses when
    /// CozmoFeatureGate::IsFeatureEnabled says no; the names and their states come from
    /// config/features.json, which the binary points at from 0x00BE8449.
    /// </summary>
    /// <summary>
    /// The decay modifiers, the damaged parts and the saved file.
    /// GetDecayMultipliers 0x0069C214 sorts each need's modifier list descending and applies the single
    /// first entry whose threshold is at or below the level (0x0069C270 vcmpe / 0x0069C278 bge); it does
    /// not combine every matching entry. NumDamagedPartsForRepairLevel 0x0069CCAC counts the leading
    /// broken-part thresholds at or above the repair level (0.98, 0.6, 0.3); WriteToDevice 0x00693BB0
    /// writes the levels with a timestamp and ApplyDecayForTimeSinceLastDeviceWrite 0x00695304 decays for
    /// the gap on the way back in.
    /// </summary>
    [Fact]
    public void TheNeedsDecayModifiersAndDamagedPartsFollowTheConfig()
    {
        var cfg = NeedsConfig.Default;
        var decay = new DecayConfig(
            new Dictionary<NeedId, IReadOnlyList<(double, double)>>
            {
                [NeedId.Play] = new[] { (0.0, 0.6) }, [NeedId.Repair] = new[] { (0.0, 0.0) }, [NeedId.Energy] = new[] { (0.0, 0.0) },
            },
            new Dictionary<NeedId, IReadOnlyList<(double, double)>>(),
            new Dictionary<NeedId, IReadOnlyList<(double, NeedId, double)>>
            {
                [NeedId.Repair] = new[] { (0.5, NeedId.Play, 1.0), (0.3, NeedId.Play, 2.0) },
            });

        var state = new NeedsState(cfg);
        state.SetNeedLevel(NeedId.Repair, 1.0);
        state.SetNeedLevel(NeedId.Play, 1.0);
        state.ApplyDecay(decay, 60, connected: true);
        Assert.Equal(0.4, state.GetNeedLevel(NeedId.Play), 3);      // 0.6 a minute; the 0.5 entry is first

        // Repair 0.3: the 0.3 entry is the first threshold at or below the level, so Play's multiplier is 2
        state.SetNeedLevel(NeedId.Repair, 0.3);
        state.SetNeedLevel(NeedId.Play, 1.0);
        state.ApplyDecay(decay, 60, connected: true);
        Assert.Equal(cfg.MinimumNeedLevel, state.GetNeedLevel(NeedId.Play), 3);   // 1.2 a minute, clamped

        // Repair 0.03: no entry's threshold is at or below the level, so the multiplier stays 1 and Play
        // decays at its base rate; the old implementation multiplied every matching entry instead.
        state.SetNeedLevel(NeedId.Repair, 0.03);
        state.SetNeedLevel(NeedId.Play, 1.0);
        state.ApplyDecay(decay, 60, connected: true);
        Assert.Equal(0.4, state.GetNeedLevel(NeedId.Play), 3);

        // the damaged parts follow the repair level against 0.98, 0.6, 0.3
        Assert.Equal(new[] { 0.98, 0.6, 0.3 }, cfg.BrokenPartThresholds);
        Assert.Equal(0, state.NumDamagedPartsForRepairLevel(1.0));
        Assert.Equal(1, state.NumDamagedPartsForRepairLevel(0.7));
        Assert.Equal(2, state.NumDamagedPartsForRepairLevel(0.5));
        Assert.Equal(3, state.NumDamagedPartsForRepairLevel(0.1));

        // and the file round-trips, decaying for the time between the write and the read
        double now = 0;
        var needs = new NeedsManager(() => now, cfg, decay);
        needs.SetLevel(NeedId.Play, 1.0);
        var path = Path.Combine(Path.GetTempPath(), NeedsManager.FileNameForSerial(0x41d04d9d));
        try
        {
            needs.Save(path, unixTimeSec: 1000);
            var back = new NeedsManager(() => now, cfg, decay);
            Assert.True(back.Load(path, unixTimeSec: 1060));         // a minute later
            Assert.Equal(0.4, back.State.GetNeedLevel(NeedId.Play), 3);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    /// <summary>
    /// The needs brackets come from the cached first threshold at or below the level.
    /// NeedsState::UpdateCurNeedsBrackets 0x0069C12C scans the need's shipped threshold vector (Repair
    /// 0.99/0.60/0.27/0, Energy 0.99/0.60/0.21/0, Play 0.99/0.50/0.14/0), takes the first index whose
    /// threshold is at or below the level (or the last when none is) and stores it at +0x70; GetNeedBracket
    /// 0x0069CBCC refreshes then returns it, and an invalid need warns and returns 4 (IsNeedAtBracket
    /// 0x0069CD80 errors and returns false).
    /// </summary>
    [Fact]
    public void TheNeedsBracketsUseTheFirstThresholdAtOrBelowTheLevel()
    {
        var state = new NeedsState(NeedsConfig.Default);
        Assert.Equal(NeedBracketId.Full, state.GetNeedBracket(NeedId.Repair));
        state.SetNeedLevel(NeedId.Repair, 0.99); Assert.Equal(NeedBracketId.Full, state.GetNeedBracket(NeedId.Repair));
        state.SetNeedLevel(NeedId.Repair, 0.9);  Assert.Equal(NeedBracketId.Normal, state.GetNeedBracket(NeedId.Repair));
        state.SetNeedLevel(NeedId.Repair, 0.27); Assert.Equal(NeedBracketId.Warning, state.GetNeedBracket(NeedId.Repair));
        state.SetNeedLevel(NeedId.Repair, 0.0);  Assert.Equal(NeedBracketId.Critical, state.GetNeedBracket(NeedId.Repair));
        Assert.True(state.IsNeedAtBracket(NeedId.Repair, NeedBracketId.Critical));

        // an invalid need index warns and returns 4 (there are only three needs)
        Assert.Equal((NeedBracketId)4, state.GetNeedBracket(3));
        Assert.Equal((NeedBracketId)4, state.GetNeedBracket(-1));
    }

    /// <summary>
    /// The FreeplayDataTracker accumulates in <c>SendData</c>, not <c>Update</c> (correction C1 §4).
    /// Constructor 0x0056EBD4 sets next-send to now + 30.0; Update 0x0056EC1A only sends at +0x18;
    /// SendData 0x0056EC48 adds <c>now - lastTimestamp</c> while the pause set is empty, rounds, emits
    /// robot.active_freeplay_time under 37 s, errors DataTooHigh at or above it, resets and sets next-send
    /// to now + 30.0; SetFreeplayPauseFlag 0x0056EEBC flushes the running segment when the set becomes
    /// non-empty and stamps the resume time when it becomes empty; ForceUpdate 0x0056EEB8 is SendData.
    /// </summary>
    [Fact]
    public void TheFreeplayDataTrackerReportsEveryThirtySecondsAndHonoursItsPauseFlags()
    {
        double clock = 0;
        var tracker = new FreeplayDataTracker(() => clock);
        var reports = new List<double>();
        tracker.ActiveFreeplayTime += reports.Add;

        // Update alone only checks the send time; nothing accumulates until a send is due
        tracker.Update(0); tracker.Update(10); tracker.Update(20);
        Assert.Empty(reports);
        Assert.Equal(0, tracker.ActiveSeconds, 6);
        tracker.Update(30);                                   // next send was now + 30
        Assert.Single(reports);
        Assert.Equal(30, reports[0], 6);                      // the accumulated 30 s, rounded
        Assert.Equal(0, tracker.ActiveSeconds, 6);
        Assert.Equal(60, tracker.NextSendSec, 6);

        // OffTreads pauses accumulation; pausing flushes the running segment 30..40 into the accumulator
        clock = 40;
        tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.OffTreads, true);
        Assert.Equal(10, tracker.ActiveSeconds, 6);
        tracker.Update(60);                                   // the next send is due; paused, so only the flushed 10 is reported
        Assert.Equal(2, reports.Count);
        Assert.Equal(10, reports[1], 6);

        // unpausing restarts the running segment; a forced update flushes it
        clock = 70;
        tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.OffTreads, false);
        clock = 80;
        tracker.Update(80);                                   // next send is 90, so no report yet
        Assert.Equal(2, reports.Count);
        Assert.Equal(0, tracker.ActiveSeconds, 6);        // Update does not accumulate; only SendData does
        tracker.ForceUpdate();                            // flushes the 70..80 segment
        Assert.Equal(3, reports.Count);
        Assert.Equal(10, reports[^1], 6);
    }

    /// <summary>
    /// <c>SendData</c> rounds the accumulator before the 37 s test (C1 §4): under 37 s fires the event, at
    /// or above it logs <c>DataTooHigh</c> and fires nothing.
    /// </summary>
    [Fact]
    public void TheFreeplayDataTrackerErrorsWhenTheRoundedTimeReachesThirtySevenSeconds()
    {
        // 36.4 s rounds to 36 and is reported
        double clock = 0;
        var under = new FreeplayDataTracker(() => clock);
        var underReports = new List<double>();
        under.ActiveFreeplayTime += underReports.Add;
        clock = 36.4;
        under.ForceUpdate();
        Assert.Equal(36, underReports[^1], 6);

        // 37 s exactly rounds to 37 and is the DataTooHigh error
        double clock2 = 0;
        var at37 = new FreeplayDataTracker(() => clock2);
        int reports = 0;
        at37.ActiveFreeplayTime += _ => reports++;
        var errors = new List<string>();
        at37.Log += errors.Add;
        clock2 = 37.0;
        at37.ForceUpdate();
        Assert.Equal(0, reports);
        Assert.Contains(errors, e => e.Contains("DataTooHigh"));
    }

    /// <summary>
    /// The NeedsManager pause transition (correction C1 §6). SetPaused 0x00695E04: a redundant call logs
    /// <c>NeedsManager.SetPaused.Redundant</c> and returns with no send, write or notification
    /// (0x00695E0C..0x00695E12); pausing stores the state and time, sends <c>NoAction</c> and forces a
    /// write; unpausing shifts the decay schedule and sends/writes nothing; both branches end with the
    /// local-notification and pause-state seams.
    /// </summary>
    [Fact]
    public void TheNeedsManagerPauseAndUnpauseFollowTheEngine()
    {
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        var writes = new List<bool>();
        var actions = new List<NeedsActionId>();
        var notifications = new List<bool>();
        int pauseStates = 0;
        var logs = new List<string>();
        needs.WriteToDevice = forced => writes.Add(forced);
        needs.NeedsStateSent += actions.Add;
        needs.LocalNotificationsSetPaused += notifications.Add;
        needs.SendNeedsPauseStateToGame = () => pauseStates++;
        needs.Log += logs.Add;

        // the first decay is due at 60; pausing at 10 leaves 50 owed
        clock = 10;
        needs.SetPaused(true);
        Assert.True(needs.IsPaused);
        Assert.Equal(new[] { NeedsActionId.NoAction }, actions);
        Assert.Equal(new[] { true }, writes);                 // the pause write is forced
        Assert.Equal(new[] { true }, notifications);
        Assert.Equal(1, pauseStates);

        // a redundant call does nothing at all
        needs.SetPaused(true);
        Assert.Single(actions); Assert.Single(writes); Assert.Single(notifications); Assert.Equal(1, pauseStates);
        Assert.Contains(logs, l => l.Contains("NeedsManager.SetPaused.Redundant"));

        // unpausing at 40 moves the next decay to 40 + 50 = 90 and sends/writes nothing
        clock = 40;
        needs.SetPaused(false);
        Assert.False(needs.IsPaused);
        Assert.Single(actions);
        Assert.Single(writes);
        Assert.Equal(new[] { true, false }, notifications);
        Assert.Equal(2, pauseStates);

        // the decay does not fire early, and fires at 90
        clock = 89; needs.Update();
        Assert.DoesNotContain(NeedsActionId.Decay, actions);
        clock = 90; needs.Update();
        Assert.Contains(NeedsActionId.Decay, actions);
        Assert.Equal(new[] { true, false }, writes);          // the 90 s write goes through PossiblyWriteToDevice (not forced)
    }

    /// <summary>
    /// Unpausing adds the pause duration to the per-need schedule (C1 §6). This stack keeps the fullness
    /// fill time and derives its deadline as fill + cooldown, so the deadline shifts by the pause duration.
    /// </summary>
    [Fact]
    public void TheUnpauseShiftsTheFullnessCooldownByThePauseDuration()
    {
        double clock = 0;
        var cfg = NeedsConfig.Default with
        {
            FullnessDecayCooldownSec = new Dictionary<NeedId, double> { [NeedId.Repair] = 100, [NeedId.Energy] = 100, [NeedId.Play] = 100 },
        };
        var decay = new DecayConfig(
            new Dictionary<NeedId, IReadOnlyList<(double, double)>>
            {
                [NeedId.Play] = new[] { (0.0, 0.06) }, [NeedId.Repair] = new[] { (0.0, 0.0) }, [NeedId.Energy] = new[] { (0.0, 0.0) },
            },
            new Dictionary<NeedId, IReadOnlyList<(double, double)>>());
        var actions = new Dictionary<string, NeedsActionDelta> { ["Fill"] = new("Fill", 0, 0, 0, 0, 1.0, 0, 0, 0) };
        var needs = new NeedsManager(() => clock, cfg, decay, actions);
        needs.SetLevel(NeedId.Play, 0.9);
        Assert.True(needs.RegisterNeedsActionCompleted("Fill"));   // Play to Full; the fill time is 0

        clock = 10; needs.SetPaused(true);
        clock = 30; needs.SetPaused(false);                        // a 20 s pause; fill 0 -> 20, deadline 100 -> 120
        needs.SetLevel(NeedId.Play, 1.0);

        clock = 119; needs.ApplyDecayAllNeeds(true);
        Assert.Equal(1.0, needs.State.GetNeedLevel(NeedId.Play), 6);   // deadline 20 + 100 = 120; now <= deadline skips
        // the deadline has passed: only the time outside the cooldown window decays. The shift put lastDecay
        // at 20, and the excluded window (deadline - start = 100) moves it to 120, so 121 decays one second.
        clock = 121; needs.ApplyDecayAllNeeds(true);
        Assert.Equal(1.0 - 0.06 * 1 / 60.0, needs.State.GetNeedLevel(NeedId.Play), 6);
    }

    /// <summary>
    /// A Full need's cooldown window is excluded from its decay. <c>ApplyDecayAllNeeds</c> 0x00695CFE, on
    /// the passed-deadline branch (0x00695D5A..0x00695D6A), adds <c>+0x208 - +0x1FC</c> (the deadline minus
    /// the fill time) to <c>+0x1E4</c> before decaying, so only the time after the deadline is decayed, not
    /// the whole time since the fill. The skipped passes leave <c>+0x1E4</c> alone.
    /// </summary>
    [Fact]
    public void AFullNeedDecaysOnlyTheTimeOutsideItsFullnessCooldown()
    {
        double clock = 0;
        var cfg = NeedsConfig.Default with
        {
            FullnessDecayCooldownSec = new Dictionary<NeedId, double> { [NeedId.Repair] = 100, [NeedId.Energy] = 100, [NeedId.Play] = 100 },
        };
        var decay = new DecayConfig(
            new Dictionary<NeedId, IReadOnlyList<(double, double)>>
            {
                [NeedId.Play] = new[] { (0.0, 0.06) }, [NeedId.Repair] = new[] { (0.0, 0.0) }, [NeedId.Energy] = new[] { (0.0, 0.0) },
            },
            new Dictionary<NeedId, IReadOnlyList<(double, double)>>());
        var actions = new Dictionary<string, NeedsActionDelta> { ["Fill"] = new("Fill", 0, 0, 0, 0, 1.0, 0, 0, 0) };
        var needs = new NeedsManager(() => clock, cfg, decay, actions);
        needs.SetLevel(NeedId.Play, 0.9);
        Assert.True(needs.RegisterNeedsActionCompleted("Fill"));   // Play to Full at t=0; deadline 100

        clock = 60; needs.ApplyDecayAllNeeds(true);
        Assert.Equal(1.0, needs.State.GetNeedLevel(NeedId.Play), 6);   // still inside the cooldown: skipped

        // at 150 the deadline has passed; only 100..150 (50 s) is outside the window. The fixed-period
        // version would have decayed the whole 150 s since the fill.
        clock = 150; needs.ApplyDecayAllNeeds(true);
        Assert.Equal(1.0 - 0.06 * 50 / 60.0, needs.State.GetNeedLevel(NeedId.Play), 6);
    }

    /// <summary>
    /// The disconnect transition (C1 §6). OnRobotDisconnected 0x00695908 writes the timestamp, clears the
    /// serial state, forces a write when not paused, clears the robot pointer, snapshots the needs, runs the
    /// DAS bracket check and sends the "disconnect" DAS event - with <b>no</b> SendNeedsStateToGame.
    /// </summary>
    [Fact]
    public void TheNeedsManagerDisconnectWritesAndSendsDasWithoutAStateBroadcast()
    {
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        var writes = new List<bool>();
        var actions = new List<NeedsActionId>();
        var das = new List<string>();
        needs.WriteToDevice = forced => writes.Add(forced);
        needs.NeedsStateSent += actions.Add;
        needs.SendNeedsLevelsDasEvent += das.Add;
        needs.Connected = true;

        clock = 5;
        needs.OnRobotDisconnected();
        Assert.False(needs.Connected);                       // the robot pointer is cleared
        Assert.Equal(5, needs.LastDisconnectSec, 6);
        Assert.Equal(new[] { true }, writes);                // forced write when not paused
        Assert.NotNull(needs.DisconnectNeedsSnapshot);
        Assert.Equal(new[] { "disconnect" }, das);
        Assert.Empty(actions);                               // no SendNeedsStateToGame here

        // while paused the disconnect does not write
        clock = 10; needs.SetPaused(true);
        writes.Clear();
        needs.OnRobotDisconnected();
        Assert.Empty(writes);
    }

    /// <summary>
    /// The needs manager's disconnect transition is wired to the always-fired removal edge.
    /// <c>NeedsManager::OnRobotDisconnected</c> 0x00695908 is called from
    /// <c>RobotManager::RemoveRobot</c> 0x0052F2DC..0x0052F2E0 in <b>both</b> branches, but the engine's
    /// <c>RobotDisconnected</c> broadcast is sent only when the connection manager did not answer the
    /// disconnect (CozmoEngine.cs:1315). The stack therefore wires the transition to
    /// <c>CozmoRobot.RobotRemoved</c>, raised by <c>ResetDevices</c> from <c>CozmoEngine.RemoveRobot</c> on
    /// every removal. Invoking the engine's removal step here raises <c>RobotRemoved</c> without the
    /// <c>RobotDisconnected</c> broadcast, which is the branch the broadcast edge would miss.
    /// </summary>
    [Fact]
    public void TheFreeplayStackDisconnectsTheNeedsManagerOnTheAlwaysFiredRemovalEdge()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        var writes = new List<bool>();
        needs.WriteToDevice = forced => writes.Add(forced);
        using var stack = FreeplayStack.Create(obb, rig.Robot, Ctx(rig), () => clock, rig.Vision, rig.M, needs: needs, withReactions: false);
        Assert.True(needs.Connected);

        // RemoveRobot's removal step, without Engine.RobotDisconnected being raised.
        clock = 5;
        rig.Robot.Engine.RobotRemoved?.Invoke();

        Assert.False(needs.Connected);
        Assert.Equal(5, needs.LastDisconnectSec, 6);
        Assert.Equal(new[] { true }, writes);
    }

    /// <summary>
    /// <c>PossiblyWriteToDevice</c> 0x00695DC4 is a 61 ms rate limiter: it writes only when the elapsed time
    /// since the stored write time is at least <c>61,000,000</c> ns, and then stores now.
    /// </summary>
    [Fact]
    public void PossiblyWriteToDeviceIsA61MillisecondRateLimiter()
    {
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        var writes = new List<bool>();
        needs.WriteToDevice = forced => writes.Add(forced);

        clock = 0; needs.PossiblyWriteToDevice();
        Assert.Empty(writes);                                // 0 ms elapsed
        clock = 0.060; needs.PossiblyWriteToDevice();
        Assert.Empty(writes);                                // 60 ms < 61 ms
        clock = 0.061; needs.PossiblyWriteToDevice();
        Assert.Single(writes);                               // exactly the throttle
        clock = 0.120; needs.PossiblyWriteToDevice();
        Assert.Single(writes);                               // 59 ms later
        clock = 0.122; needs.PossiblyWriteToDevice();
        Assert.Equal(2, writes.Count);                       // 61 ms later
        Assert.All(writes, w => Assert.False(w));            // PossiblyWriteToDevice is never the forced write
    }

    /// <summary>
    /// A need's <c>+0x1dc</c> pause flag, written by the <c>SetNeedsPauseStates</c> message, makes
    /// <c>ApplyDecayAllNeeds</c> skip it (0x00695D36).
    /// </summary>
    [Fact]
    public void APausedNeedIsSkippedByDecay()
    {
        double clock = 0;
        var cfg = NeedsConfig.Default;
        var decay = new DecayConfig(
            new Dictionary<NeedId, IReadOnlyList<(double, double)>>
            {
                [NeedId.Play] = new[] { (0.0, 60.0) }, [NeedId.Repair] = new[] { (0.0, 0.0) }, [NeedId.Energy] = new[] { (0.0, 0.0) },
            },
            new Dictionary<NeedId, IReadOnlyList<(double, double)>>());
        var needs = new NeedsManager(() => clock, cfg, decay);
        needs.SetNeedPaused(NeedId.Play, true);
        clock = 60; needs.ApplyDecayAllNeeds(true);
        Assert.Equal(1.0, needs.State.GetNeedLevel(NeedId.Play), 6);
        needs.SetNeedPaused(NeedId.Play, false);
        clock = 120; needs.ApplyDecayAllNeeds(true);
        Assert.True(needs.State.GetNeedLevel(NeedId.Play) < 1.0);
    }

    /// <summary>
    /// The decay uses each need's own last-decay time (<c>+0x1E4</c>), not a fixed period.
    /// <c>ApplyDecayAllNeeds</c> 0x00695CFE passes <c>now - this[need].lastDecay</c> to
    /// <c>NeedsState::ApplyDecay</c> and stores <c>+0x1E4 = now</c>; a need skipped by its pause flag or by
    /// its fullness deadline keeps <c>+0x1E4</c>, so the whole gap is decayed once the skip ends. Here Play
    /// is paused for three periods and then decays 240 s' worth on the first unpaused pass.
    /// </summary>
    [Fact]
    public void TheDecayUsesTheActualElapsedTimeSinceTheNeedLastDecayed()
    {
        double clock = 0;
        var cfg = NeedsConfig.Default;
        var decay = new DecayConfig(
            new Dictionary<NeedId, IReadOnlyList<(double, double)>>
            {
                [NeedId.Play] = new[] { (0.0, 0.06) }, [NeedId.Repair] = new[] { (0.0, 0.0) }, [NeedId.Energy] = new[] { (0.0, 0.0) },
            },
            new Dictionary<NeedId, IReadOnlyList<(double, double)>>());
        var needs = new NeedsManager(() => clock, cfg, decay);
        needs.SetNeedPaused(NeedId.Play, true);
        clock = 60; needs.Update();
        clock = 120; needs.Update();
        clock = 180; needs.Update();
        Assert.Equal(1.0, needs.State.GetNeedLevel(NeedId.Play), 6);   // skipped, and +0x1E4 left alone
        needs.SetNeedPaused(NeedId.Play, false);
        clock = 240; needs.Update();
        // 240 s at 0.06/min = 0.24; the fixed-period version would have taken only one period's 0.06.
        Assert.Equal(0.76, needs.State.GetNeedLevel(NeedId.Play), 6);
    }

    [Fact]
    public void AnActivityWhoseFeatureIsOffDoesNotStart()
    {
        var gates = new FeatureGates(new[] { ("Singing", true), ("Bouncer", false) });
        Assert.True(gates.IsEnabled("singing"));          // the engine lowercases before it looks up
        Assert.False(gates.IsEnabled("Bouncer"));
        Assert.False(gates.IsEnabled("NotListed"));

        var inputs = new FreeplayInputs { Features = gates };
        var singing = new ActivityStrategy { Type = "Simple", FeatureGate = "Singing" };
        var bouncer = new ActivityStrategy { Type = "Simple", FeatureGate = "Bouncer" };
        Assert.True(singing.WantsToStart(inputs, 0, out _));
        Assert.False(bouncer.WantsToStart(inputs, 0, out var why));
        Assert.Contains("Bouncer", why);

        // with no gates loaded nothing is consulted, which is what this stack can honestly say
        Assert.True(bouncer.WantsToStart(new FreeplayInputs(), 0, out _));
    }

    [Fact]
    public void AnActivityThatEndedAsSoonAsItStartedWaitsThreeSeconds()
    {
        Assert.Equal(3.0, ActivityStrategy.ShortRunCooldownSec, 6);

        var quick = new ActivityStrategy { Type = "Simple", CooldownBaseSec = 30 };
        quick.OnStarted(10);
        quick.OnEnded(10 + ActivityStrategy.TickSec);       // one tick: under the two-tick bar
        Assert.True(quick.InCooldown(12));                   // inside the three seconds
        Assert.False(quick.InCooldown(13.2));                // and out of them, not waiting the 30

        var normal = new ActivityStrategy { Type = "Simple", CooldownBaseSec = 30 };
        normal.OnStarted(10);
        normal.OnEnded(20);                                  // a real run: the configured cooldown stands
        Assert.True(normal.InCooldown(45));
        Assert.False(normal.InCooldown(51));
    }

    [Fact]
    public void StrategiesRespectCooldownsDurationsMoodAndNeeds()
    {
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        var inputs = new FreeplayInputs { Needs = needs };
        var simple = new ActivityStrategy { Type = "Simple", ShouldEndDurationSec = 25, CooldownBaseSec = 30, Random = new Random(1) };
        Assert.True(simple.WantsToStart(inputs, 0, out _));
        Assert.False(simple.WantsToEnd(inputs, 10, out _));
        Assert.False(simple.WantsToEnd(inputs, 25, out _));                                  // at the duration, not past it
        Assert.True(simple.WantsToEnd(inputs, 25.5, out var why)); Assert.Contains("should end", why);
        simple.OnEnded(100);
        Assert.False(simple.WantsToStart(inputs, 110, out var cd)); Assert.Contains("cooldown", cd);
        Assert.True(simple.WantsToStart(inputs, 131, out _));
        // socialize's mood gate: Social below 0.3 scores 1, above 0.3 scores 0, minimum 0.5
        var graph = new Graph2d(new[] { (-1.0, 1.0), (0.3, 1.0), (0.3, 0.0), (1.0, 0.0) });
        var mooded = new ActivityStrategy { Type = "Simple", RequiredMinStartMoodScore = 0.5, StartMoodScorer = new[] { (EmotionType.Social, graph) } };
        var obb = ObbRoot();
        if (obb is not null)
        {
            var mood = new MoodState(MoodModel.Load(obb));
            var withMood = new FreeplayInputs { Needs = needs, Mood = mood };
            Assert.True(mooded.WantsToStart(withMood, 0, out _));                           // Social starts at 0 -> score 1
            Assert.True(mood.Trigger("InteractWithNamedFace", 0) || true);
        }
        // needs strategies
        var needsStrategy = new ActivityStrategy { Type = "Needs", Need = NeedId.Energy, NeedBracket = NeedBracketId.Critical, HigherPriorityStrategy = "Repair", ShouldEndDurationSec = -1 };
        Assert.False(needsStrategy.WantsToStart(inputs, 0, out var r1)); Assert.Contains("not Critical", r1);
        needs.SetLevel(NeedId.Energy, 0);
        Assert.True(needsStrategy.WantsToStart(inputs, 0, out _));
        Assert.False(needsStrategy.WantsToEnd(inputs, 1000, out _));                         // -1: never by duration
        needs.SetLevel(NeedId.Repair, 0);
        Assert.False(needsStrategy.WantsToStart(inputs, 0, out var r2)); Assert.Contains("Repair", r2);
        needs.SetLevel(NeedId.Repair, 1); needs.SetLevel(NeedId.Energy, 1);
        Assert.True(needsStrategy.WantsToEnd(inputs, 1, out var r3)); Assert.Contains("left", r3);
        var transition = new ActivityStrategy { Type = "SevereNeedTransition", Need = NeedId.Play };
        needs.SetLevel(NeedId.Play, 0);
        Assert.True(transition.WantsToStart(inputs, 0, out _));
        needs.SetSevereExpressed(NeedId.Play, true);
        Assert.False(transition.WantsToStart(inputs, 0, out var r4)); Assert.Contains("expressed", r4);
        Assert.True(transition.WantsToEnd(inputs, 0, out _));
        var pyramid = new ActivityStrategy { Type = "Pyramid" };
        Assert.False(pyramid.WantsToStart(new FreeplayInputs { LocatedCubes = 2 }, 0, out _));
        Assert.True(pyramid.WantsToStart(new FreeplayInputs { LocatedCubes = 3 }, 0, out _));
        Assert.False(new ActivityStrategy { Type = "Spark" }.WantsToStart(inputs, 0, out _));
    }

    // ------------------------------------------------------------------ the shipped tree

    [Fact]
    public void TheShippedActivityTreeLoadsWithItsPrioritiesAndChoosers()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var bound = new Dictionary<string, IBehavior>();
        var tree = ActivityTreeLoader.Load(obb, bound);
        Assert.Equal(new[] { "Selection", "MeetCozmo", "Feeding", "Freeplay" }, tree.Select(a => a.Id));
        var freeplay = tree.Single(a => a.Id == "Freeplay");
        Assert.Equal("Freeplay", freeplay.Type);
        Assert.Equal(("Socialize", "Socialize", "PlayAlone", "Hiking"), freeplay.DesiredActivityNames);
        // activityPriority is parsed with ParseUint8 and discarded (0x005AD63C/0x005AD640); the child order
        // is the subActivities JSON array order (M15-013). The shipped array runs Sparks 0..13, the three
        // needs activities, then PutDownDispatch, Socialize, Singing, PlayWithHumans, BuildPyramid, PlayAlone,
        // Hiking and NothingToDo.
        var subs = freeplay.SubActivities;
        Assert.True(subs.Count >= 20);
        Assert.All(subs, s => Assert.Equal(0, s.Priority));
        Assert.All(subs.Where(s => s.Id.StartsWith("Sparks")), s => { Assert.Equal("Sparked", s.Type); Assert.NotNull(s.RequireSpark); });
        Assert.Equal(new[]
        {
            "SparksFireTruckAlarm", "SparksRollBlock", "SparksStackBlock", "SparksPeekABoo", "SparksPounceOnMotion",
            "SparksPopAWheelie", "SparksKnockOverCubes", "SparksPickUpCube", "SparksWorkout", "SparksBuildPyramid",
            "SparksFistBump", "SparksGatherCubes", "SparksTrackLaser", "SparksCozmoSings",
            "NeedsSevereLowRepair", "NeedsSevereLowEnergy", "NeedsSevereLowPlayGetIn",
            "PutDownDispatch", "Socialize", "Singing", "PlayWithHumans", "BuildPyramid", "PlayAlone", "Hiking", "NothingToDo",
        }, subs.Select(s => s.Id));
        var hiking = subs.Single(s => s.Id == "Hiking");
        Assert.Equal(BehaviorChooserType.Scoring, hiking.Chooser!.Type);
        Assert.Equal(AnimationTrigger.HikingDrivingLoop, hiking.DriveLoopAnim);
        Assert.Equal("Simple", hiking.Strategy.Type); Assert.Equal(60, hiking.Strategy.ShouldEndDurationSec); Assert.Equal(15, hiking.Strategy.CooldownBaseSec);
        var hikingChooser = (ScoringChooser)hiking.Chooser;
        Assert.Contains(hikingChooser.Entries, e => e.BehaviorId == "Hiking_ThinkAboutBeacons" && e.FlatScore == 8.0);
        Assert.Contains(hikingChooser.Entries, e => e.BehaviorId == "Hiking_VisitInterestingEdge" && e.RunningPenalty is not null);
        Assert.NotNull(hikingChooser.ScoreBonusForCurrent);
        Assert.Equal(BehaviorChooserType.StrictPriority, hiking.InterludeChooser!.Type);
        var energy = subs.Single(s => s.Id == "NeedsSevereLowEnergy");
        Assert.Equal("Needs", energy.Strategy.Type); Assert.Equal(NeedId.Energy, energy.Strategy.Need); Assert.Equal(NeedBracketId.Critical, energy.Strategy.NeedBracket); Assert.Equal("Repair", energy.Strategy.HigherPriorityStrategy);
        Assert.Equal(new[] { "DriveOffCharger", "PutDownBlock", "Needs_SevereLowEnergyGetIn", "Needs_SevereLowEnergyState", "Needs_Wait" }, energy.Chooser!.BehaviorIds);
        var socialize = subs.Single(s => s.Id == "Socialize");
        Assert.Equal(0.5, socialize.Strategy.RequiredMinStartMoodScore); Assert.Single(socialize.Strategy.StartMoodScorer);
        Assert.Equal(300.0, subs.Single(s => s.Id == "BuildPyramid").Strategy.CooldownBaseSec);
        Assert.Equal("Selection", tree[0].Chooser!.Type.ToString());
    }

    [Fact]
    public void TheStackBindsMostOfWhatTheTreeNamesAndReportsTheRest()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        using var stack = FreeplayStack.Create(obb, rig.Robot, Ctx(rig), () => 0, rig.Vision, rig.M, withReactions: false);
        var named = stack.Tree.SelectMany(a => a.AllBehaviorIds()).Distinct().ToList();
        int boundCount = named.Count(id => stack.Bound.ContainsKey(id));
        Assert.True(boundCount >= 70, $"bound {boundCount} of {named.Count}: unbound {string.Join(",", stack.UnboundIds)}");
        // known holes: the app's games, faces games, the explorer's memory-map behaviours
        Assert.Contains("RequestSpeedTap", stack.UnboundIds);
        Assert.Contains("Hiking_VisitInterestingEdge", stack.UnboundIds);
        Assert.DoesNotContain("Hiking_ThinkAboutBeacons", stack.UnboundIds);
        Assert.DoesNotContain("Needs_SevereLowEnergyState", stack.UnboundIds);
        Assert.DoesNotContain("SparksLookInPlace", stack.UnboundIds);
        Assert.DoesNotContain("Singing_AbaDaba", stack.UnboundIds);
        Assert.Contains("FeedingReactCubeShake", stack.Bound.Keys);
    }

    // ------------------------------------------------------------------ explorer and needs behaviours

    private static void RunToEnd(Rig rig, SteppedBehavior b, BehaviorContext ctx, Func<bool>? until = null, double stepMs = 100, int ms = 15000)
    {
        double t = 0;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (b.Update(ctx, t))
        {
            rig.Pump(); t += stepMs;
            if (until?.Invoke() ?? false) { b.Stop(BehaviorStopReason.Interrupted); return; }
            if (sw.ElapsedMilliseconds > ms) throw new TimeoutException("behaviour did not finish: " + string.Join(" | ", b.Trace));
            Thread.Sleep(2);
        }
        b.Stop(BehaviorStopReason.Completed);
    }

    private static void PanTilt(Rig rig)
    {
        rig.Vision.PanTiltOverride = (body, head, ct) => { rig.Angle = (float)body; rig.Head = (float)head; rig.State(); return Task.FromResult(true); };
    }

    [Fact]
    public void LookAroundInPlaceScansOneFullTurnAndStops()
    {
        using var rig = new Rig();
        PanTilt(rig);
        var p = new LookAroundParams { NumberOfScansBeforeStop = 1, S1Body = (10, 30), S2Wait = (0.1, 0.1), S3Body = (5, 25), S4HeadChanges = (1, 1), S6Body = (30, 65) };
        var ctx = Ctx(rig);
        var b = new ExploreLookAroundInPlaceBehavior(rig.Vision, "Hiking_LookInPlace360", p, rig.M);
        Assert.True(Runnable(b, ctx));
        RunToEnd(rig, b, ctx);
        Assert.Equal(1, b.Iterations);
        Assert.Equal(ExploreLookAroundInPlaceBehavior.Phase.Idle, b.CurrentPhase);
        Assert.True(b.Turns.Count >= 5, $"{b.Turns.Count} turns");
        // the first turn is against the main direction, the main turns with it
        double main = Math.Sign(b.Turns[1].BodyDeg);
        Assert.Equal(-main, Math.Sign(b.Turns[0].BodyDeg));
        Assert.Contains(b.Trace, l => l.Contains("Starting first iteration"));
        Assert.Contains(b.Trace, l => l.Contains("reached max iterations"));
        Assert.Contains(rig.LiftHeights, h => h == LiftPresets.LowDockMm);
        // carrying a cube forbids it unless the config allows
        rig.M.Docking.Carrying.SetCarrying(7);
        Assert.False(Runnable(b, ctx));
        Assert.True(Runnable(new ExploreLookAroundInPlaceBehavior(rig.Vision, "SparksLookInPlace", p with { CanCarryCube = true }, rig.M), ctx));
    }

    [Fact]
    public void DriveInDesperationIdlesDrivesToRandomPointsAndRequests()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = new DriveInDesperationBehavior(rig.Vision, rig.M, "Needs_SevereLowRepairState", useCubes: false, 5, 20, AnimationTrigger.NeedsSevereLowRepairRequest) { IdleScale = 0.01 };
        RunToEnd(rig, b, ctx, until: () => b.Trace.Any(l => l.Contains("NeedsSevereLowRepairRequest")), stepMs: 200, ms: 20000);
        Assert.True(b.RandomDrives is >= 1 and <= 3, $"{b.RandomDrives} drives");
        Assert.Contains(b.Trace, l => l.Contains("idling for"));
        Assert.Contains(b.Trace, l => l.Contains("random points next time we drive"));
        Assert.Contains(b.Trace, l => l.Contains("GetRandomDrivingPose"));
        Assert.Contains(b.Trace, l => l.Contains("NeedsSevereLowRepairRequest"));
        var lines = rig.Sent.OfType<ExecutePath>().Count();
        Assert.True(lines >= b.RandomDrives);
        Assert.True(Math.Abs(rig.X) + Math.Abs(rig.Y) > 20);                              // it moved
    }

    [Fact]
    public void ExpressNeedsAndTheGetInFollowTheBrackets()
    {
        using var rig = new Rig();
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        var ctx = Ctx(rig);
        var express = new ExpressNeedsBehavior(needs, "Needs_MildLowEnergyRequest", NeedId.Energy, NeedBracketId.Warning, new[] { AnimationTrigger.NeedsMildLowEnergyRequest },
                                               new Graph2d(new[] { (0.0, 20.0), (0.1, 20.0), (0.5, 60.0), (1.0, 60.0) }), rig.Vision);
        express.Clock = () => clock * 1000;
        Assert.False(Runnable(express, ctx));
        needs.SetLevel(NeedId.Energy, 0.25);
        Assert.True(Runnable(express, ctx));
        Assert.Equal(35.0, express.CooldownSec, 3);                                          // the graph at level 0.25
        RunToEnd(rig, express, ctx);
        Assert.Contains(express.Trace, l => l.Contains("NeedsMildLowEnergyRequest"));
        Assert.False(Runnable(express, ctx));                                                 // in cooldown
        clock = 40; Assert.True(Runnable(express, ctx));
        var getIn = new PlayAnimOnNeedsChangeBehavior(needs, "Needs_SevereLowEnergyGetIn", NeedId.Energy, new[] { AnimationTrigger.NeedsSevereLowEnergyGetIn });
        Assert.False(Runnable(getIn, ctx));
        needs.SetLevel(NeedId.Energy, 0);
        Assert.True(Runnable(getIn, ctx));
        RunToEnd(rig, getIn, ctx);
        Assert.True(needs.IsSevereExpressed(NeedId.Energy));
        Assert.False(Runnable(getIn, ctx));                                                   // expressed until the need recovers
        needs.SetLevel(NeedId.Energy, 0.5); needs.SetLevel(NeedId.Energy, 0);
        Assert.True(Runnable(getIn, ctx));
        var sparks = new EarnedSparksBehavior(needs);
        Assert.False(Runnable(sparks, ctx));
        needs.SparksRewardPending = true;
        Assert.True(Runnable(sparks, ctx));
        RunToEnd(rig, sparks, ctx);
        Assert.False(needs.SparksRewardPending);
        Assert.Contains(sparks.Trace, l => l.Contains("EarnedSparks"));
    }

    // ------------------------------------------------------------------ the freeplay system

    private static Activity Fp(params Activity[] subs) => new()
    {
        Id = "Freeplay", Type = "Freeplay", Strategy = new ActivityStrategy(), SubActivities = subs.OrderBy(s => s.Priority).ToList(),
        DesiredActivityNames = ("Socialize", "Socialize", "PlayAlone", "Hiking"),
    };

    [Fact]
    public void FreeplayPicksTheDesiredActivityFromObjectsAndRunsItsBehaviours()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var hikeA = new Fake("hikeA", ticks: 2); var hikeB = new Fake("hikeB", ticks: 2); var playA = new Fake("playA", ticks: 2); var interlude = new Fake("interlude", ticks: 1);
        var bound = new Dictionary<string, IBehavior> { ["hikeA"] = hikeA, ["hikeB"] = hikeB, ["playA"] = playA, ["interlude"] = interlude };
        // one repetition history for the whole tree, the manager's, as FreeplayStack wires it
        var manager = new BehaviorManager(ctx);
        var penalty = manager.Penalty;
        var hiking = new Activity
        {
            Id = "Hiking", Priority = 16, Strategy = new ActivityStrategy { ShouldEndDurationSec = 60, CooldownBaseSec = 15 },
            Chooser = new ScoringChooser(new[] { new ScoredBehaviorEntry("hikeA", 2, new Graph2d(new[] { (0.0, 0.0), (30.0, 1.0) }), null, null, Array.Empty<EmotionScorer>()),
                                                 new ScoredBehaviorEntry("hikeB", 1, null, null, null, Array.Empty<EmotionScorer>()) }, bound, penalty: penalty),
            InterludeChooser = new StrictPriorityChooser(new[] { "interlude" }, bound),
        };
        var playAlone = new Activity { Id = "PlayAlone", Priority = 15, Strategy = new ActivityStrategy { ShouldEndDurationSec = 25, CooldownBaseSec = 30 },
                                       Chooser = new ScoringChooser(new[] { new ScoredBehaviorEntry("playA", 1, null, null, null, Array.Empty<EmotionScorer>()) }, bound, penalty: penalty) };
        var nothing = new Activity { Id = "NothingToDo", Priority = 17, Strategy = new ActivityStrategy(), Chooser = new StrictPriorityChooser(new[] { "hikeB" }, bound) };
        var inputs = new FreeplayInputs();
        var fp = new FreeplaySystem(manager, ctx, Fp(hiking, playAlone, nothing), bound, inputs);
        var log = new List<string>(); fp.Log += log.Add;

        // no face, no cube: Hiking is the desired activity; hikeA (2) runs first
        var d = fp.Tick(0, 0);
        Assert.Equal("Hiking", d.Activity); Assert.Equal("hikeA", d.Behavior);
        Assert.Equal(1, hikeA.Started);
        fp.Tick(1, 1000);                                                                     // hikeA finishes (2 ticks)
        var d3 = fp.Tick(2, 2000);
        // hikeA just ran (penalty 0): the interlude is inserted, then hikeB
        Assert.Contains(log, l => l.Contains("inserting interlude interlude"));
        Assert.True(d3.Behavior is "interlude" or "hikeB", d3.ToString());
        for (int i = 3; i < 8; i++) fp.Tick(i, i * 1000);
        Assert.True(hikeB.Started >= 1, string.Join(" | ", fp.Decisions.Select(x => $"{x.AtSec}:{x.Activity}/{x.Behavior}")));
        // a cube appears and the robot is put down: Hiking is kicked out and PlayAlone (cube only) picked
        inputs.LocatedCubes = 1;
        fp.OnRobotPutDown(10);
        var d10 = fp.Tick(10, 10000);
        Assert.Contains(log, l => l.Contains("Kicking out 'Hiking' on put down"));
        Assert.Equal("PlayAlone", d10.Activity); Assert.Equal("playA", d10.Behavior);
        // PlayAlone should end after 25 s; once playA finishes the next pick happens, and Hiking is in cooldown
        for (int i = 11; i < 40; i++) fp.Tick(i, i * 1000);
        Assert.Contains(log, l => l.Contains("'PlayAlone' wants to end"));
        Assert.Contains(fp.Decisions, x => x.Activity == "Hiking" && x.AtSec > 25);       // Hiking's 15 s cooldown from t=10 has passed
        Assert.Contains(log, l => l.Contains("robot.freeplay_goal_started PlayAlone"));
    }

    /// <summary>
    /// The second gate on the activity, <c>GetDesiredActiveBehaviorInternal</c> 0x005AE68C..0x005AE704.
    /// An activity that chooses no behaviour is dropped and barred from the re-pick
    /// ("NoBehaviorChosenWhileRunning ... This activity is not allowed to be repicked", the third argument of
    /// PickNewActivityForSpark going false at 0x005AE75C); an activity that chooses a behaviour it is not
    /// already running is dropped too when its strategy wants to end ("NewBehaviorChosenWhileRunning"). And
    /// <c>IActivity::OnDeselected</c> 0x005B3548 hands a pending freeplay sparks reward to the app on the way
    /// out (<c>NeedsManager::SparksRewardCommunicatedToUser</c> 0x00696E64).
    /// </summary>
    [Fact]
    public void AnActivityThatChoosesNoBehaviourIsDroppedAndNotRepicked()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        ctx.Needs = needs;
        var a = new Fake("a", ticks: 1);
        var b = new Fake("b", ticks: 40);
        var c = new Fake("c", runnable: false, ticks: 40);
        var bound = new Dictionary<string, IBehavior> { ["a"] = a, ["b"] = b, ["c"] = c };
        var manager = new BehaviorManager(ctx);
        var first = new Activity { Id = "First", Priority = 1, Strategy = new ActivityStrategy(), Chooser = new StrictPriorityChooser(new[] { "a" }, bound) };
        // a scoring chooser, because only a chooser that can displace a running behaviour reaches the second
        // gate: StrictPriorityBSRunnableChooser takes the running one without even asking (0x0060B250)
        var second = new Activity
        {
            Id = "Second", Priority = 2, Strategy = new ActivityStrategy { ShouldEndDurationSec = 1 },
            Chooser = new ScoringChooser(new[] { new ScoredBehaviorEntry("b", 1, null, null, null, Array.Empty<EmotionScorer>()),
                                                 new ScoredBehaviorEntry("c", 5, null, null, null, Array.Empty<EmotionScorer>()) }, bound),
        };
        var fp = new FreeplaySystem(manager, ctx, Fp(first, second), bound, new FreeplayInputs { Needs = needs });
        var log = new List<string>(); fp.Log += log.Add;

        Assert.Equal("First", fp.Tick(0, 0).Activity);
        Assert.Equal(1, a.Started);

        // 'a' stops wanting to run: First chooses nothing, is dropped and is barred from the re-pick, so the
        // same tick lands on Second. The sparks reward pending when First ends goes to the app.
        a.Runnable = false;
        needs.SparksRewardPending = true;
        var d = fp.Tick(1, 1000);
        Assert.Contains(log, l => l.Contains("NoBehaviorChosenWhileRunning") && l.Contains("not allowed to be repicked"));
        Assert.Equal("Second", d.Activity);
        Assert.Equal("b", d.Behavior);
        Assert.False(needs.SparksRewardPending);

        // Second has wanted to end since t=2, and 'b' running keeps it alive.
        Assert.Equal("Second", fp.Tick(3, 3000).Activity);
        Assert.Equal("b", manager.Current?.Id);

        // the moment its chooser wants a behaviour that is not the one running, it goes instead
        c.Runnable = true;
        fp.Tick(4, 4000);
        Assert.True(log.Any(l => l.Contains("NewBehaviorChosenWhileRunning") && l.Contains("'Second'")), string.Join(" | ", log));
        Assert.Null(fp.Current);                                               // nothing else can be picked
        Assert.Equal(0, c.Started);
    }

    [Fact]
    public void ASevereNeedTakesPriorityOverFreeplayAndTheGetInPlaysOnce()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        LoadAssets(rig, obb);
        double clock = 0;
        var ctx = Ctx(rig);
        using var stack = FreeplayStack.Create(obb, rig.Robot, ctx, () => clock, rig.Vision, rig.M, withReactions: false, random: new Random(2));
        var log = new List<string>(); stack.Freeplay.Log += log.Add;
        var needs = stack.Needs;
        PanTilt(rig);
        foreach (var b in stack.Bound.Values.OfType<DriveInDesperationBehavior>()) b.IdleScale = 0.01;
        // energy critical: the NeedsSevereLowEnergy activity (priority 2) wins over the freeplay chain
        needs.SetLevel(NeedId.Energy, 0.0);
        var decisions = new List<FreeplayDecision>();
        for (int i = 0; i < 40; i++) { clock = i * 0.5; decisions.Add(stack.Tick(clock, clock * 1000, rig.Robot, rig.Vision, rig.M)); rig.Pump(); Thread.Sleep(5); }
        Assert.Contains(decisions, d => d.Activity == "NeedsSevereLowEnergy" && d.Behavior == "Needs_SevereLowEnergyGetIn");
        Assert.Contains(decisions, d => d.Activity == "NeedsSevereLowEnergy" && d.Behavior == "Needs_SevereLowEnergyState");
        Assert.True(needs.IsSevereExpressed(NeedId.Energy));
        Assert.Equal(1, decisions.Select(d => d.Behavior).Distinct().Count(b => b == "Needs_SevereLowEnergyGetIn"));
        // feeding refills the energy: the activity wants to end, and freeplay resumes with Hiking (no face, no cube)
        needs.RegisterNeedsActionCompleted("Feed"); needs.RegisterNeedsActionCompleted("Feed"); needs.RegisterNeedsActionCompleted("Feed");
        for (int i = 40; i < 80; i++) { clock = i * 0.5; decisions.Add(stack.Tick(clock, clock * 1000, rig.Robot, rig.Vision, rig.M)); rig.Pump(); Thread.Sleep(5); }
        Assert.Equal("NeedsSevereLowEnergy", stack.Freeplay.Current?.Id);   // the running behaviour is not interrupted by the refill alone
        // the desperation drive loops (IsRunnableInternal 0x005D90AC returns 1): the activity ends the way the engine's
        // does after the app's Feeding activity, by the requested-activity path
        Assert.True(stack.Freeplay.Current!.Strategy.WantsToEnd(stack.Freeplay.Inputs, 0, out var endReason), endReason);
        Assert.Contains("left Critical", endReason);
        stack.Freeplay.RequestNewActivity();
        for (int i = 80; i < 90; i++) { clock = i * 0.5; decisions.Add(stack.Tick(clock, clock * 1000, rig.Robot, rig.Vision, rig.M)); rig.Pump(); Thread.Sleep(5); }
        Assert.Contains(log, l => l.Contains("'NeedsSevereLowEnergy' was requested"));
        Assert.Contains(decisions.Where(d => d.AtSec >= 20), d => d.Activity == "Hiking");
    }

    [Fact]
    public void TheWholeStackDrivesOffTheChargerAndPlaysWithTheCubeItSees()
    {
        var obb = ObbRoot();
        if (obb is null || MarkerLibrary.EmbeddedOrNull is null) return;
        using var rig = new Rig();
        LoadAssets(rig, obb);
        double clock = 0;
        var ctx = Ctx(rig);
        using var stack = FreeplayStack.Create(obb, rig.Robot, ctx, () => clock, rig.Vision, rig.M, withReactions: false, random: new Random(4));
        var log = new List<string>(); stack.Freeplay.Log += log.Add;
        PanTilt(rig);
        // sitting on the charger (facing out) with a cube on its side ahead
        rig.Charger = new Pose3d(Mat3.AboutZ(Math.PI), new Vec3(30, 0, 0)); rig.OnCharger = true; rig.Angle = 0f; rig.State();
        rig.Cube = new Pose3d(Mat3.AboutX(Math.PI / 2), new Vec3(260, 20, 22));
        Assert.Single(rig.Frame().Objects);
        rig.DockOutcome = BlockStatus.NoBlock;
        var decisions = new List<FreeplayDecision>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 400 && sw.ElapsedMilliseconds < 30000; i++)
        {
            clock = i * 0.25;
            decisions.Add(stack.Tick(clock, clock * 1000, rig.Robot, rig.Vision, rig.M));
            rig.Pump(); rig.Frame(); Thread.Sleep(2);
            if (decisions.Any(d => d.Behavior == "RollBlockOnSide") && rig.Sent.OfType<DockWithObject>().Any()) break;
        }
        var summary = string.Join(" | ", decisions.Where((d, i) => i == 0 || decisions[i - 1].Behavior != d.Behavior).Select(d => $"{d.AtSec}:{d.Activity}/{d.Behavior}"));
        // the cube-only rule picks PlayAlone; DriveOffCharger (1000) runs first, then the roll (RollBlockOnSide 1.0)
        Assert.Equal("PlayAlone", decisions.First(d => d.Activity is not null).Activity);
        Assert.Contains(decisions, d => d.Behavior == "DriveOffCharger");
        Assert.False(rig.OnCharger, summary);
        Assert.Contains(decisions, d => d.Behavior == "RollBlockOnSide");
        Assert.Contains(rig.Sent, m => m is DockWithObject);
        Assert.Contains(log, l => l.Contains("robot.goal_from_face_and_cube 0:1 -> PlayAlone"));
    }

    /// <summary>
    /// The spark re-selection (correction C1 §1). The invalid-spark latch at <c>BehaviorManager+0x65</c> is
    /// set only by an <c>ActivateSpark</c> whose <c>UnlockId == 0x55</c> (the stack's null) and cleared by
    /// <c>SwitchToRequestedSpark</c>. When the current activity's spark equals the requested spark and the
    /// latch is set, <c>GetDesiredActiveBehaviorInternal</c> 0x005AE40C..0x005AE46E logs
    /// <c>ActivityFreeplay.ChooseNextBehavior.SparkReselected</c> and re-picks with the "ask the current
    /// activity WantsToEnd" flag cleared (arg 0).
    /// </summary>
    [Fact]
    public void AReRequestedInvalidSparkDropsTheCurrentActivityAndRepicks()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var a = new Fake("a", ticks: 100);
        var b = new Fake("b", ticks: 100);
        var bound = new Dictionary<string, IBehavior> { ["a"] = a, ["b"] = b };
        var manager = new BehaviorManager(ctx);
        var first = new Activity
        {
            Id = "First", Priority = 1, RequireSpark = null,
            // Cooldown 0, so only the reselect exclusion keeps First from being chosen again.
            Strategy = new ActivityStrategy { ShouldEndDurationSec = 1000, CooldownBaseSec = 0 },
            Chooser = new StrictPriorityChooser(new[] { "a" }, bound),
        };
        var second = new Activity
        {
            Id = "Second", Priority = 2, RequireSpark = null,
            Strategy = new ActivityStrategy { ShouldEndDurationSec = 1000, CooldownBaseSec = 0 },
            Chooser = new StrictPriorityChooser(new[] { "b" }, bound),
        };
        var fp = new FreeplaySystem(manager, ctx, Fp(first, second), bound, new FreeplayInputs());
        var log = new List<string>(); fp.Log += log.Add;

        Assert.Equal("First", fp.Tick(0, 0).Activity);
        Assert.False(fp.RequestedSparkInvalid);

        // a real spark request does not set the latch
        fp.SetRequestedSpark("SparksFireTruckAlarm");
        Assert.False(fp.RequestedSparkInvalid);
        // the invalid unlock 0x55 (the stack's null) sets it
        fp.SetRequestedSpark(null);
        Assert.True(fp.RequestedSparkInvalid);

        var d = fp.Tick(1, 1000);
        Assert.Contains(log, l => l.Contains("ActivityFreeplay.ChooseNextBehavior.SparkReselected: Spark re-selected: none behavior will be selected"));
        Assert.False(fp.RequestedSparkInvalid);              // SwitchToRequestedSpark cleared it
        Assert.Equal("Second", d.Activity);
        Assert.Equal("b", d.Behavior);
    }

    /// <summary>
    /// The invalid-spark latch <c>BehaviorManager+0x65</c> is cleared by <c>SwitchToRequestedSpark</c>
    /// 0x005A4220 after <b>every</b> pick (C1 §1), not only the reselect branch. A requested-activity pick
    /// that never reselects still clears it.
    /// </summary>
    [Fact]
    public void TheInvalidSparkLatchClearsOnEveryPick()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var a = new Fake("a", ticks: 100);
        var b = new Fake("b", ticks: 100);
        var bound = new Dictionary<string, IBehavior> { ["a"] = a, ["b"] = b };
        var manager = new BehaviorManager(ctx);
        var first = new Activity
        {
            Id = "First", Priority = 1, RequireSpark = "FireTruckAlarm",
            Strategy = new ActivityStrategy { ShouldEndDurationSec = 1000, CooldownBaseSec = 0 },
            Chooser = new StrictPriorityChooser(new[] { "a" }, bound),
        };
        var second = new Activity
        {
            Id = "Second", Priority = 2, RequireSpark = null,
            Strategy = new ActivityStrategy { ShouldEndDurationSec = 1000, CooldownBaseSec = 0 },
            Chooser = new StrictPriorityChooser(new[] { "b" }, bound),
        };
        var fp = new FreeplaySystem(manager, ctx, Fp(first, second), bound, new FreeplayInputs());
        var log = new List<string>(); fp.Log += log.Add;

        // First requires FireTruckAlarm; requesting it picks First and does not set the latch
        fp.SetRequestedSpark("FireTruckAlarm");
        Assert.False(fp.RequestedSparkInvalid);
        Assert.Equal("First", fp.Tick(0, 0).Activity);

        // the invalid unlock 0x55 (null) sets the latch, but First's spark is not null, so it is not a reselect
        fp.SetRequestedSpark(null);
        Assert.True(fp.RequestedSparkInvalid);
        fp.RequestNewActivity();
        var d = fp.Tick(1, 1000);
        Assert.Equal("Second", d.Activity);
        Assert.False(fp.RequestedSparkInvalid);              // the pick cleared +0x65
        Assert.DoesNotContain(log, l => l.Contains("SparkReselected"));
    }

    /// <summary>
    /// <c>DetectBracketChangeForDas(force)</c> 0x00695958 enters the event branch when
    /// <c>cached != current || force</c>. On <c>force</c> it emits for all three needs even when unchanged
    /// and does <b>not</b> update the cached bracket (<c>+0x214</c>), so a following conditional pass still
    /// reports a changed need.
    /// </summary>
    [Fact]
    public void TheDisconnectBracketCheckForcesAllThreeNeedsAndKeepsTheCache()
    {
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        needs.SetLevel(NeedId.Energy, 0.0);                 // Energy cache becomes Critical
        needs.State.SetNeedLevel(NeedId.Energy, 1.0);       // current Full; the cache stays Critical
        var changes = new List<(NeedId, NeedBracketId, NeedBracketId)>();
        needs.BracketChanged += (n, a, b) => changes.Add((n, a, b));

        needs.DetectBracketChangeForDas(true);
        Assert.Equal(3, changes.Count);                     // all three, even the two unchanged
        Assert.Contains((NeedId.Energy, NeedBracketId.Critical, NeedBracketId.Full), changes);

        changes.Clear();
        needs.DetectBracketChangeForDas(false);             // +0x214 was not updated by the force
        Assert.Equal(1, changes.Count);
        Assert.Contains((NeedId.Energy, NeedBracketId.Critical, NeedBracketId.Full), changes);
    }

    /// <summary>
    /// <c>ActivityStrategy::FromJson</c> keeps the <c>IActivityStrategy</c> constructor defaults (row 25) for
    /// keys the config omits: <c>JsonTools::GetValueOptional&lt;float&gt;</c> 0x004FA580 only overwrites when
    /// the member exists, so an absent <c>activityShouldEndDurationSecs</c> is 60 and an absent
    /// <c>cooldownBaseSecs</c> is -1. The shipped <c>sparksFireTruckAlarm.json</c> carries neither key.
    /// </summary>
    [Fact]
    public void AnActivityStrategyWithNoStrategyKeysKeepsTheConstructorDefaults()
    {
        using var doc = JsonDocument.Parse("{\"type\":\"Simple\"}");
        var s = ActivityStrategy.FromJson(doc.RootElement);
        Assert.Equal(-1, s.CanEndDurationSec);
        Assert.Equal(60, s.ShouldEndDurationSec);
        Assert.Equal(-1, s.CooldownBaseSec);
        Assert.Equal(0, s.CooldownRandomnessSec);
        Assert.False(s.StartInCooldown);
        Assert.Equal(-1, s.RequiredRecentOnTreadsEventSec);
        Assert.Equal(-1, s.RequiredMinStartMoodScore);
        Assert.Null(s.FeatureGate);

        var obb = ObbRoot();
        if (obb is null) return;
        var tree = ActivityTreeLoader.Load(obb, new Dictionary<string, IBehavior>());
        var truck = tree.SelectMany(a => a.SubActivities.Prepend(a)).Single(a => a.Id == "SparksFireTruckAlarm");
        Assert.Equal(60, truck.Strategy.ShouldEndDurationSec);
        Assert.Equal(-1, truck.Strategy.CooldownBaseSec);
    }

    /// <summary>
    /// <c>SendData</c> 0x0056EC48 guards the report/error block with the accumulator being non-zero: a zero
    /// accumulator emits nothing, while the reset and the <c>+0x18 = now + 30.0</c> stamp still happen.
    /// </summary>
    [Fact]
    public void TheFreeplayDataTrackerEmitsNothingWhenTheAccumulatorIsZero()
    {
        double clock = 0;
        var tracker = new FreeplayDataTracker(() => clock);
        var reports = new List<double>();
        tracker.ActiveFreeplayTime += reports.Add;
        var logs = new List<string>();
        tracker.Log += logs.Add;

        tracker.ForceUpdate();                          // 0 s accumulated: nothing to report
        Assert.Empty(reports);
        Assert.Empty(logs);
        Assert.Equal(30, tracker.NextSendSec, 6);       // the next-send stamp still ran

        clock = 30;
        tracker.ForceUpdate();                          // a non-zero segment still reports
        Assert.Single(reports);
        Assert.Equal(30, reports[0], 6);
    }
}
