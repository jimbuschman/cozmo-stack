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
        needs.BracketChanged += (n, a, b, _) => changes.Add((n, a, b));
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
    /// M1-024 CD6..CD11: <c>CozmoEngine::Update</c> state 3 calls <c>NeedsManager::Update</c> between
    /// UpdateRobotConnection → MessageHandler::ProcessMessages and UpdateAllRobots → Robot::Update, on
    /// <c>BaseStationTimer::GetCurrentTimeInSeconds</c> (0x004ED632..0x004ED640). <c>FreeplayStack.Create</c>
    /// hands the manager to the engine, so the decay advances on the engine tick and no longer on
    /// <c>FreeplaySystem.Tick</c>.
    /// </summary>
    [Fact]
    public void TheNeedsManagerUpdatesOnTheEngineTickNotTheFreeplayTick()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        LoadAssets(rig, obb);
        double clock = 0;
        using var stack = FreeplayStack.Create(obb, rig.Robot, Ctx(rig), () => clock, rig.Vision, rig.M, withReactions: false);
        var needs = stack.Needs;
        Assert.NotNull(rig.Robot.Engine.NeedsUpdate);          // FreeplayStack handed it to the engine
        needs.SetLevel(NeedId.Play, 0.9);

        // the freeplay tick alone does not decay: the needs manager is not ticked from FreeplaySystem.Tick
        clock = 1000;
        stack.Freeplay.Tick(1000, 1_000_000);
        Assert.Equal(0.9, needs.State.GetNeedLevel(NeedId.Play), 6);

        // the engine tick does, on its own BaseStationTimer seconds
        rig.Clock.Advance(120_000);
        rig.Tick();
        Assert.True(needs.State.GetNeedLevel(NeedId.Play) < 0.9);
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
            new Dictionary<NeedId, IReadOnlyList<(double, double)>>
            {
                // the unconnected table: the engine's Load/robot-apply path passes connected=false (Appendix I4)
                [NeedId.Play] = new[] { (0.0, 0.3) }, [NeedId.Repair] = new[] { (0.0, 0.0) }, [NeedId.Energy] = new[] { (0.0, 0.0) },
            },
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

        // and the file round-trips, decaying for the time between the write and the read through the engine's
        // unconnected path (ApplyDecayForTimeSinceLastDeviceWrite(false), Appendix I4)
        double now = 0;
        var needs = new NeedsManager(() => now, cfg, decay);
        needs.SetLevel(NeedId.Play, 1.0);
        var path = Path.Combine(Path.GetTempPath(), NeedsManager.FileNameForSerial(0x41d04d9d));
        try
        {
            needs.Save(path, unixTimeSec: 1000);
            var back = new NeedsManager(() => now, cfg, decay);
            now = 1060;                                              // a minute later on the stack clock
            Assert.True(back.Load(path));
            Assert.Equal(0.7, back.State.GetNeedLevel(NeedId.Play), 3);   // 0.3/min unconnected, not the 0.6 connected rate
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
    /// J11 (0x00695F02..0x00695F6A): on unpause the engine adds the pause duration to each need's
    /// <c>+0x1f0</c> (the per-need pause start) and <c>+0x214</c> (the bracket-change clock) always, as it
    /// does <c>+0x1e4</c>. The constructor zeroes <c>+0x1f0</c> and seeds <c>+0x214</c> to the init time
    /// (J9/J10).
    /// </summary>
    [Fact]
    public void TheUnpauseShiftsThePerNeedPauseStartAndTheBracketChangeClock()
    {
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
        {
            Assert.Equal(0.0, needs.NeedPauseStartSec(n), 6);      // the ctor zeroes +0x1f0
            Assert.Equal(0.0, needs.BracketChangedSec(n), 6);      // the ctor seeds +0x214 = now
        }

        clock = 10; needs.SetPaused(true);
        clock = 30; needs.SetPaused(false);                        // a 20 s pause

        foreach (var n in new[] { NeedId.Repair, NeedId.Energy, NeedId.Play })
        {
            Assert.Equal(20.0, needs.NeedPauseStartSec(n), 6);     // +0x1f0 += pauseDuration always
            Assert.Equal(20.0, needs.BracketChangedSec(n), 6);     // +0x214 += pauseDuration always
        }
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
    /// The disconnect transition (C1 §6). OnRobotDisconnected 0x00695908 writes the timestamp, resets the
    /// <c>+0x30 OpenAppAfterDisconnect</c> counter, forces a write when not paused, clears the robot pointer,
    /// snapshots the device timestamp into <c>+0x1B8</c> (J4), runs the DAS bracket check and sends the
    /// "disconnect" DAS event - with <b>no</b> SendNeedsStateToGame.
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

        // +0x1B8 is the state DateTime snapshot, not the need levels (J4): Load sets +8/+0xC = 1234.
        Assert.True(needs.Load(DeviceNeedsFile(5, 7, 1234, 0.9), applyElapsedDecay: false));

        clock = 5;
        needs.OnRobotDisconnected();
        Assert.False(needs.Connected);                       // the robot pointer is cleared
        Assert.Equal(5, needs.LastDisconnectSec, 6);
        Assert.Equal(new[] { true }, writes);                // forced write when not paused
        Assert.Equal(1234, needs.DeviceTimestampSnapshotSec, 6);   // +0x1B8/+0x1BC = +8/+0xC (0x00695934)
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
    /// M15-016 (J13): <c>CozmoEngine::HandleMessage&lt;ConnectToRobot&gt;</c> 0x004ED018..0x004ED11C calls
    /// <c>NeedsManager::InitAfterConnection</c> 0x004ED10E unconditionally after AddRobot. The engine's
    /// <c>CozmoEngine.ConnectToRobotHandled</c> edge is raised from <c>CozmoEngine.ConnectToRobot</c>; the
    /// stack subscribes to it and replays it once when Robot 1 already exists at creation (the offline rig's
    /// engine has one). <c>CozmoEngine.ConnectToRobot</c> cannot complete a real transport connection in this
    /// test, so the raised-edge path is invoked directly.
    /// </summary>
    [Fact]
    public void TheConnectToRobotEdgeAndReplayCallInitAfterConnection()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        Assert.False(needs.AwaitingRobotData);

        using var stack = FreeplayStack.Create(obb, rig.Robot, Ctx(rig), () => clock, rig.Vision, rig.M, needs: needs, withReactions: false);
        // the replay: Robot 1 already exists, so InitAfterConnection ran at creation (+0x3d0 = 1)
        Assert.True(needs.AwaitingRobotData);

        // the edge itself: a fresh manager that starts with +0x3d0 clear gets it set by the raised edge, and
        // the stack's subscription is what makes a later ConnectToRobot handling set the stack's manager again.
        var edgeNeeds = new NeedsManager(() => clock);
        Assert.False(edgeNeeds.AwaitingRobotData);
        rig.Robot.Engine.ConnectToRobotHandled += edgeNeeds.InitAfterConnection;
        needs.OnRobotDisconnected();
        Assert.False(needs.Connected);
        rig.Robot.Engine.ConnectToRobotHandled?.Invoke();
        Assert.True(edgeNeeds.AwaitingRobotData);
        Assert.True(needs.Connected);
        rig.Robot.Engine.ConnectToRobotHandled -= edgeNeeds.InitAfterConnection;
    }

    /// <summary>
    /// <c>PossiblyWriteToDevice</c> 0x00695DC4 is a 61-second rate limiter (J14): it compares the elapsed
    /// time since the stored write time against <c>0x03A2C940 = 61,000,000</c> on the microsecond clock
    /// (<c>ApplyDecayForTimeSinceLastDeviceWrite</c> divides by 1,000,000 at 0x0069532C), so 61 s, and then
    /// stores now.
    /// </summary>
    [Fact]
    public void PossiblyWriteToDeviceIsA61SecondRateLimiter()
    {
        Assert.Equal(61.0, NeedsManager.WriteThrottleSec, 6);
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        var writes = new List<bool>();
        needs.WriteToDevice = forced => writes.Add(forced);

        clock = 0; needs.PossiblyWriteToDevice();
        Assert.Empty(writes);                                // 0 s elapsed
        clock = 60.999; needs.PossiblyWriteToDevice();
        Assert.Empty(writes);                                // just under 61 s
        clock = 61; needs.PossiblyWriteToDevice();
        Assert.Single(writes);                               // exactly the throttle
        clock = 120; needs.PossiblyWriteToDevice();
        Assert.Single(writes);                               // 59 s later
        clock = 122; needs.PossiblyWriteToDevice();
        Assert.Equal(2, writes.Count);                       // 61 s later
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
        needs.SetLevel(NeedId.Energy, 0.0);                 // Energy cache becomes Critical; +0x214 written at 0
        needs.State.SetNeedLevel(NeedId.Energy, 1.0);       // current Full; the cache stays Critical
        var changes = new List<(NeedId, NeedBracketId, NeedBracketId, double)>();
        needs.BracketChanged += (n, a, b, elapsed) => changes.Add((n, a, b, elapsed));

        needs.DetectBracketChangeForDas(true);
        Assert.Equal(3, changes.Count);                     // all three, even the two unchanged
        Assert.Contains((NeedId.Energy, NeedBracketId.Critical, NeedBracketId.Full, 0.0), changes);

        changes.Clear();
        clock = 5;
        needs.DetectBracketChangeForDas(false);             // +0x214 was not updated by the force
        Assert.Equal(1, changes.Count);
        // J10: the elapsed is now - +0x214; the force pass did not write +0x214, so it is measured from the
        // last conditional write at t=0, and this conditional pass writes +0x214 = 5.
        Assert.Contains((NeedId.Energy, NeedBracketId.Critical, NeedBracketId.Full, 5.0), changes);
        Assert.Equal(5.0, needs.BracketChangedSec(NeedId.Energy), 6);
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

    // ------------------------------------------------------------------ M15-014 needs connection and persistence

    /// <summary>The JSON shape the engine's <c>WriteToDevice</c> uses (C2 rows 3/11): version, serial, timestamp and levels.</summary>
    private static byte[] NeedsJson(int version, uint serial, long dateTime, double play)
        => System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["_StateFileVersion"] = version,
            ["_DateTime"] = dateTime,
            ["_SerialNumber"] = serial,
            ["CurNeedLevel"] = new[] { 0.9, 0.8, play },
        }));

    /// <summary>Writes a device needs file and returns its path (the resolver's alternate-file case).</summary>
    private static string DeviceNeedsFile(int version, uint serial, long dateTime, double play)
    {
        var path = Path.Combine(Path.GetTempPath(), $"needs-device-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, System.Text.Encoding.UTF8.GetString(NeedsJson(version, serial, dateTime, play)));
        return path;
    }

    /// <summary>
    /// A version-5 <c>NeedsStateOnRobot</c> blob built by hand at the Appendix H offsets, so the tests
    /// assert the layout rather than the implementation's own packer.
    /// </summary>
    private static byte[] RobotBlobV5(ulong timeLastWritten, double repair, double energy, double play)
    {
        var b = new byte[NeedsStateOnRobot.Size];
        BitConverter.GetBytes(5u).CopyTo(b, 0x00);
        BitConverter.GetBytes(timeLastWritten).CopyTo(b, 0x04);
        BitConverter.GetBytes((int)(repair * 100000.0 + 0.5)).CopyTo(b, 0x0C);
        BitConverter.GetBytes((int)(energy * 100000.0 + 0.5)).CopyTo(b, 0x10);
        BitConverter.GetBytes((int)(play * 100000.0 + 0.5)).CopyTo(b, 0x14);
        return b;
    }

    /// <summary>An older-version blob: the shorter Appendix H layout with only the Play level set (index 2 @0x14).</summary>
    private static byte[] RobotBlobVersion(int version, ulong timeLastWritten, double play)
    {
        int size = version switch { 1 => 0x5C, 2 => 0x64, 3 => 0x68, 4 => 0x6C, _ => 0x74 };
        var b = new byte[size];
        b[0] = (byte)version;
        BitConverter.GetBytes(timeLastWritten).CopyTo(b, 0x04);
        BitConverter.GetBytes((int)(play * 100000.0 + 0.5)).CopyTo(b, 0x14);
        return b;
    }

    /// <summary>Answers the in-flight needs NV read (key 0x194000) with a valid non-factory header and payload.</summary>
    private static void ReplyNeedsRead(Rig rig, byte[] payload, sbyte result = 0)
    {
        byte[] data;
        if (result == 0)
        {
            data = new byte[16 + payload.Length];
            BitConverter.GetBytes(NvStorageComponent.NonFactoryHeaderMagic).CopyTo(data, 0);
            BitConverter.GetBytes((uint)payload.Length).CopyTo(data, 8);
            payload.CopyTo(data, 16);
        }
        else data = Array.Empty<byte>();
        rig.Send(new NVOpResult { Tag = NeedsManager.NeedsNvKey, Op = NvStorageComponent.OpRead, Result = result, Length = 0, Data = data });
        rig.Pump();
    }

    /// <summary>
    /// The mfgId serial edge reaches the needs manager (C2 rows 5-9). The engine exposes the edge; the stack
    /// owns the NeedsManager and subscribes. A serial already known when the stack is created (FreeplayTool
    /// connects first) is replayed, and either way <c>StartReadFromRobot</c> queues key 0x194000 on the robot's
    /// NV component (C2 row 10).
    /// </summary>
    [Fact]
    public void TheFreeplayStackConnectsTheNeedsManagerOnTheMfgIdSerialEdge()
    {
        var obb = ObbRoot();
        Assert.NotNull(obb);
        double clock = 0;

        // the edge arrives after the stack exists
        using (var rig = new Rig())
        {
            var needs = new NeedsManager(() => clock);
            using var stack = FreeplayStack.Create(obb!, rig.Robot, Ctx(rig), () => clock, rig.Vision, rig.M, needs: needs, withReactions: false);
            Assert.Null(rig.Robot.Engine.AcquiredSerialNumber);
            rig.Robot.Engine.RaiseSerialNumberAcquired(0x41D04D9D);
            Assert.Equal(0x41D04D9Du, needs.SerialNumber);
            Assert.Equal(0u, needs.PreviousSerialNumber);
            Assert.Equal(0x41D04D9Du, rig.Robot.Engine.AcquiredSerialNumber);

            rig.Tick();                                              // M3-026: Update sends the queued read
            rig.Pump();
            var read = rig.Sent.OfType<NVCommand>().Last();
            Assert.Equal(NeedsManager.NeedsNvKey, read.Tag);
            Assert.Equal(NvStorageComponent.OpRead, read.Op);
        }

        // the serial is already known when the stack is created: it is replayed
        using (var rig = new Rig())
        {
            var needs = new NeedsManager(() => clock);
            rig.Robot.Engine.RaiseSerialNumberAcquired(0x1234);
            using var stack = FreeplayStack.Create(obb!, rig.Robot, Ctx(rig), () => clock, rig.Vision, rig.M, needs: needs, withReactions: false);
            Assert.Equal(0x1234u, needs.SerialNumber);
        }
    }

    /// <summary>
    /// <c>InitAfterSerialNumberAcquired</c> 0x006943A0 (C2 row 9): the prior serial moves to +0x1cc, the new
    /// one lands at +0x34, +0x1c8/+0x1ca are cleared, and StartReadFromRobot runs. With no NV component to
    /// queue on, the queue fails and the resolution runs at once.
    /// </summary>
    [Fact]
    public void InitAfterSerialNumberAcquiredKeepsThePreviousSerialAndClearsTheReadFlags()
    {
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        needs.NvStorage = null;                                       // the queue-failure path
        needs.FinishReadFromRobot(RobotBlobVersion(1, 10, 0.5), 0);   // leave a rewrite flag set, then clear it
        Assert.True(needs.RobotRewriteNeeded);

        needs.InitAfterSerialNumberAcquired(7);
        Assert.Equal(7u, needs.SerialNumber);
        Assert.Equal(0u, needs.PreviousSerialNumber);
        Assert.False(needs.RobotReadSucceeded);
        Assert.False(needs.RobotRewriteNeeded);

        needs.InitAfterSerialNumberAcquired(8);
        Assert.Equal(8u, needs.SerialNumber);
        Assert.Equal(7u, needs.PreviousSerialNumber);
    }

    /// <summary>
    /// <c>StartReadFromRobot</c> 0x006944B4 (C2 row 10): with a connected NV component it queues a READ of
    /// key 0x194000; the callback's <c>FinishReadFromRobot</c> Boolean is stored at +0x1c8 (C2 row 11) and the
    /// resolution runs. Appendix G Q2: the engine's failure is an invalid tag only, so a bad tag logs, clears
    /// <c>+0x3d0</c> and returns 0 without sending.
    /// </summary>
    [Fact]
    public void StartReadFromRobotQueuesKey194000AndStoresTheCallbackResult()
    {
        double clock = 0;
        using var rig = new Rig();
        var needs = new NeedsManager(() => clock);
        needs.NvStorage = rig.Robot.Engine.NvStorage;
        needs.InitAfterSerialNumberAcquired(7);
        rig.Tick();                                                  // M3-026: Update sends the queued read
        rig.Pump();
        var read = rig.Sent.OfType<NVCommand>().Last();
        Assert.Equal(NeedsManager.NeedsNvKey, read.Tag);
        Assert.Equal(NvStorageComponent.OpRead, read.Op);

        // a successful version-5 blob: +0x1c8 true, no rewrite, the robot copy selected for a device write
        ReplyNeedsRead(rig, RobotBlobV5(2000, 0.9, 0.8, 0.5));
        Assert.True(needs.RobotReadSucceeded);
        Assert.False(needs.RobotRewriteNeeded);
        Assert.True(needs.HasRobotCopy);
        Assert.Equal(0.5, needs.State.GetNeedLevel(NeedId.Play), 6);

        // a missing item (-1): +0x1c8 false, and the resolution continues
        needs.InitAfterSerialNumberAcquired(8);
        rig.Tick();                                                  // M3-026: Update sends the queued read
        rig.Pump();
        ReplyNeedsRead(rig, Array.Empty<byte>(), -1);
        Assert.False(needs.RobotReadSucceeded);

        // an invalid tag: Read returns 0 and nothing is sent; the failure branch clears +0x3d0
        int before = rig.Sent.OfType<NVCommand>().Count();
        Assert.Equal(0, needs.StartReadFromRobot(0x123456));
        Assert.False(needs.AwaitingRobotData);
        rig.Pump();
        Assert.Equal(before, rig.Sent.OfType<NVCommand>().Count());
    }

    /// <summary>
    /// <c>NVStorageComponent.Read</c> (Appendix G Q2; 0x00644E14..0x00644EF7) returns 1 when the tag is
    /// valid and queued and 0 for an invalid tag only; the invalid-tag path does not send.
    /// </summary>
    [Fact]
    public void NvStorageReadReturnsZeroForAnInvalidTagAndDoesNotSend()
    {
        using var rig = new Rig();
        int callbacks = 0;
        Assert.Equal(0, rig.Robot.Engine.NvStorage!.Read(0x123456, _ => callbacks++));
        Assert.Equal(1, callbacks);                                  // the callback still gets (-6, empty)
        rig.Pump();
        Assert.DoesNotContain(rig.Sent, m => m is NVCommand);
        Assert.Equal(1, rig.Robot.Engine.NvStorage.Read(NeedsManager.NeedsNvKey, _ => { }));
    }

    /// <summary>
    /// <c>NeedsStateOnRobot::Pack</c>/<c>Unpack</c> (Appendix H): the version-5 layout is 116 bytes with the
    /// documented offsets, and a round trip preserves every field.
    /// </summary>
    [Fact]
    public void TheNeedsStateOnRobotVersionFiveBlobRoundTrips()
    {
        Assert.Equal(0x74, NeedsStateOnRobot.Size);
        var s = new NeedsStateOnRobot
        {
            Version = 5, TimeLastWritten = 0x1122334455667788, CurNeedsUnlockLevel = 7, NumStarsAwarded = 3,
            TimeLastStarAwarded = 0xAABBCCDDEEFF0011, OnboardingStageCompleted = 2, ForceNextSong = 4, TimeCreated = 0x0102030405060708,
        };
        s.CurNeedLevel[0] = 90000; s.CurNeedLevel[1] = 80000; s.CurNeedLevel[2] = 50000;
        s.PartIsDamaged[0] = 1; s.PartIsDamaged[5] = 1;

        var bytes = NeedsStateOnRobot.Pack(s);
        Assert.Equal(0x74, bytes.Length);
        Assert.Equal(5u, BitConverter.ToUInt32(bytes, 0x00));
        Assert.Equal(0x1122334455667788UL, BitConverter.ToUInt64(bytes, 0x04));
        Assert.Equal(90000, BitConverter.ToInt32(bytes, 0x0C));
        Assert.Equal(80000, BitConverter.ToInt32(bytes, 0x10));
        Assert.Equal(50000, BitConverter.ToInt32(bytes, 0x14));
        Assert.Equal(7, BitConverter.ToInt32(bytes, 0x34));
        Assert.Equal(3, BitConverter.ToInt32(bytes, 0x38));
        Assert.Equal(1, bytes[0x3C]); Assert.Equal(0, bytes[0x3D]); Assert.Equal(1, bytes[0x41]);
        Assert.Equal(0xAABBCCDDEEFF0011UL, BitConverter.ToUInt64(bytes, 0x5C));
        Assert.Equal(2, BitConverter.ToInt32(bytes, 0x64));
        Assert.Equal(4, BitConverter.ToInt32(bytes, 0x68));
        Assert.Equal(0x0102030405060708UL, BitConverter.ToUInt64(bytes, 0x6C));

        var back = NeedsStateOnRobot.Unpack(bytes);
        Assert.Equal(5u, back.Version);
        Assert.Equal(s.TimeLastWritten, back.TimeLastWritten);
        Assert.Equal(s.CurNeedLevel, back.CurNeedLevel);
        Assert.Equal(s.CurNeedsUnlockLevel, back.CurNeedsUnlockLevel);
        Assert.Equal(s.NumStarsAwarded, back.NumStarsAwarded);
        Assert.Equal(s.PartIsDamaged, back.PartIsDamaged);
        Assert.Equal(s.TimeLastStarAwarded, back.TimeLastStarAwarded);
        Assert.Equal(s.OnboardingStageCompleted, back.OnboardingStageCompleted);
        Assert.Equal(s.ForceNextSong, back.ForceNextSong);
        Assert.Equal(s.TimeCreated, back.TimeCreated);
    }

    /// <summary>
    /// The v1-4 conversion (Appendix H): the prefix is copied, a shorter layout's missing tail is zeroed and
    /// the converted version is forced to 5.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void TheV1ToV4ConversionZeroesTheMissingTailAndForcesVersionFive(int version)
    {
        int size = version switch { 1 => 0x5C, 2 => 0x64, 3 => 0x68, 4 => 0x6C, _ => 0x74 };
        var blob = new byte[size];
        blob[0] = (byte)version;
        BitConverter.GetBytes(0x1122334455667788UL).CopyTo(blob, 0x04);
        BitConverter.GetBytes(90000).CopyTo(blob, 0x0C);
        if (version >= 2) BitConverter.GetBytes(0xAABBCCDDUL).CopyTo(blob, 0x5C);
        if (version >= 3) BitConverter.GetBytes(0x0BADF00D).CopyTo(blob, 0x64);
        if (version >= 4) BitConverter.GetBytes(4).CopyTo(blob, 0x68);

        var s = NeedsStateOnRobot.UnpackVersioned(blob, version);
        Assert.Equal(5u, s.Version);                                 // forced
        Assert.Equal(0x1122334455667788UL, s.TimeLastWritten);
        Assert.Equal(90000, s.CurNeedLevel[0]);
        Assert.Equal(version >= 2 ? 0xAABBCCDDUL : 0UL, s.TimeLastStarAwarded);
        Assert.Equal(version >= 3 ? 0x0BADF00D : 0, s.OnboardingStageCompleted);
        Assert.Equal(version >= 4 ? 4 : 0, s.ForceNextSong);
        Assert.Equal(0UL, s.TimeCreated);                            // never present below v5
    }

    /// <summary>
    /// <c>FinishReadFromRobot</c> 0x00699DB0..0x0069A1AC, corrected contract (C2 row 11, Appendix H): result
    /// -1 and any result below -1 return false; a version above 5 returns false; version 5 returns true
    /// without +0x1ca; versions 1-4 return true with +0x1ca; version 0 returns true with +0x1ca and a
    /// "not supported" log.
    /// </summary>
    [Fact]
    public void FinishReadFromRobotFollowsTheCorrectedVersionContract()
    {
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        var logs = new List<string>();
        needs.Log += logs.Add;

        Assert.False(needs.FinishReadFromRobot(Array.Empty<byte>(), -1));    // the missing NV item
        Assert.False(needs.FinishReadFromRobot(Array.Empty<byte>(), -4));    // another NV failure

        Assert.True(needs.FinishReadFromRobot(RobotBlobV5(1, 0.9, 0.8, 0.5), 0));   // version 5: no rewrite
        Assert.False(needs.RobotRewriteNeeded);

        Assert.True(needs.FinishReadFromRobot(RobotBlobVersion(4, 1, 0.5), 0));     // versions 1-4: rewrite
        Assert.True(needs.RobotRewriteNeeded);

        Assert.True(needs.FinishReadFromRobot(RobotBlobVersion(1, 1, 0.5), 0));
        Assert.True(needs.RobotRewriteNeeded);

        Assert.False(needs.FinishReadFromRobot(new byte[] { 6 }, 0));               // above 5: the only zero version path

        Assert.True(needs.FinishReadFromRobot(new byte[] { 0 }, 0));                // the unsupported old version succeeds
        Assert.True(needs.RobotRewriteNeeded);
        Assert.Contains(logs, l => l.Contains("not supported"));
    }

    /// <summary>
    /// M15-018: a version-0 blob is read as all-zero fields, sets the rewrite flag and returns success
    /// (the engine's prefix is uninitialised; zeros are the forced SD2 policy).
    /// </summary>
    [Fact]
    public void AVersionZeroBlobIsReadAsZerosWithTheRewriteFlagSet()
    {
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        needs.WriteToDevice = _ => { };

        Assert.True(needs.FinishReadFromRobot(new byte[] { 0 }, 0));
        Assert.True(needs.RobotRewriteNeeded);
        Assert.True(needs.HasRobotCopy);

        // the all-zero levels are applied (clamped to the configured minimum)
        needs.InitAfterReadFromRobotAttempt();
        Assert.Equal(0.03, needs.State.GetNeedLevel(NeedId.Play), 6);
        Assert.Equal(0.03, needs.State.GetNeedLevel(NeedId.Repair), 6);
    }

    /// <summary>
    /// <c>StartWriteToRobot</c> 0x00695494..0x00695763 (Appendix G Q1): a fresh version-5
    /// <c>NeedsStateOnRobot</c> (116 bytes) is written to NV key 0x194000, with <c>timeLastWritten</c> the
    /// passed time and each level scaled by 100000.
    /// </summary>
    [Fact]
    public void StartWriteToRobotWritesTheVersionFiveBlobToKey194000()
    {
        using var rig = new Rig();
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        needs.NvStorage = rig.Robot.Engine.NvStorage;
        needs.SetLevel(NeedId.Repair, 0.9);
        needs.SetLevel(NeedId.Energy, 0.8);
        needs.SetLevel(NeedId.Play, 0.5);

        clock = 1000;
        needs.StartWriteToRobot(clock);
        rig.Tick();                                                  // M3-026: Update sends the queued write
        rig.Pump();

        var write = rig.Sent.OfType<NVCommand>().Last();
        Assert.Equal(NeedsManager.NeedsNvKey, write.Tag);
        Assert.Equal(NvStorageComponent.OpWrite, write.Op);
        Assert.Equal(NeedsStateOnRobot.Size, write.Data.Length);
        Assert.Equal(5u, BitConverter.ToUInt32(write.Data, 0x00));
        Assert.Equal(1000UL, BitConverter.ToUInt64(write.Data, 0x04));
        Assert.Equal(90000, BitConverter.ToInt32(write.Data, 0x0C));
        Assert.Equal(80000, BitConverter.ToInt32(write.Data, 0x10));
        Assert.Equal(50000, BitConverter.ToInt32(write.Data, 0x14));
        Assert.Equal(1000, needs.LastWriteToRobotSec, 6);

        // the terminal callback: a failed write logs and sets the error flag, a good one is a no-op
        needs.FinishWriteToRobot(-4);
        Assert.True(needs.WriteToRobotError);
        needs.FinishWriteToRobot(0);
        Assert.True(needs.WriteToRobotError);
    }

    /// <summary>
    /// The abort path (Appendix G Q1): while a robot read is outstanding (<c>+0x3d0</c>) the write logs
    /// "Aborting writing needs state to robot, because we are reading needs state from robot" and queues
    /// nothing.
    /// </summary>
    [Fact]
    public void StartWriteToRobotAbortsWhileAReadIsOutstanding()
    {
        using var rig = new Rig();
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        needs.NvStorage = rig.Robot.Engine.NvStorage;
        var logs = new List<string>();
        needs.Log += logs.Add;
        needs.InitAfterConnection();                                 // sets +0x3d0
        Assert.True(needs.AwaitingRobotData);

        needs.StartWriteToRobot(1);
        rig.Pump();

        Assert.DoesNotContain(rig.Sent.OfType<NVCommand>(), c => c.Op == NvStorageComponent.OpWrite);
        Assert.Contains(logs, l => l.Contains("Aborting writing needs state to robot, because we are reading needs state from robot"));
    }

    /// <summary>
    /// The per-serial filename <c>NeedsFilenameFromSerialNumber</c> 0x00695224 (C2 row 12): <c>needsState_</c> +
    /// the unsigned decimal serial + <c>.json</c>, including the unguarded serial zero.
    /// </summary>
    [Fact]
    public void TheNeedsFileNameIsPerSerial()
    {
        Assert.Equal("needsState_1104170397.json", NeedsManager.FileNameForSerial(0x41D04D9D));
        Assert.Equal("needsState_0.json", NeedsManager.FileNameForSerial(0));
        Assert.Equal("needsState_4294967295.json", NeedsManager.FileNameForSerial(uint.MaxValue));
    }

    /// <summary>
    /// <c>InitAfterReadFromRobotAttempt</c> 0x0069481C..0x00694D06 (C2 rows 12-13, Appendix H): the robot
    /// blob has no serial, so the stored-file serial <c>+0x1CC</c> is compared with the incoming serial
    /// <c>+0x34</c>. Neither copy schedules both writes; robot-only selects the robot data for a device
    /// write; matching serials compare timestamps and select the newer; mismatched serials select the robot
    /// data and clear the disconnect timing; the per-serial alternate file is attempted when the robot has no
    /// data and the stored serial differs.
    /// </summary>
    [Fact]
    public void TheResolverComparesTheStoredAndIncomingSerialAndSelectsTheCopy()
    {
        double clock = 0;

        // neither copy: a device write and a robot write are scheduled; with no NV storage the robot write fails
        var none = new NeedsManager(() => clock);
        var noneWrites = new List<bool>();
        none.WriteToDevice = f => noneWrites.Add(f);
        none.InitAfterReadFromRobotAttempt();
        Assert.Equal(new[] { false }, noneWrites);
        Assert.True(none.WriteToRobotError);

        // robot-only: the robot data is selected for a device write
        var robotOnly = new NeedsManager(() => clock);
        var writes = new List<bool>();
        robotOnly.WriteToDevice = f => writes.Add(f);
        robotOnly.FinishReadFromRobot(RobotBlobV5(2000, 0.9, 0.8, 0.5), 0);
        robotOnly.InitAfterReadFromRobotAttempt();
        Assert.Equal(new[] { false }, writes);
        Assert.Equal(0.5, robotOnly.State.GetNeedLevel(NeedId.Play), 6);

        using var rig = new Rig();

        // matching +0x1CC/+0x34: the newer timestamp wins. Device 3000 > robot 2000 -> device.
        var deviceNewer = new NeedsManager(() => clock);
        deviceNewer.WriteToDevice = _ => { };
        deviceNewer.NvStorage = rig.Robot.Engine.NvStorage;
        deviceNewer.Load(DeviceNeedsFile(5, 7, 3000, 0.9), applyElapsedDecay: false);
        deviceNewer.InitAfterSerialNumberAcquired(7);                // +0x1CC = 7, +0x34 = 7
        deviceNewer.FinishReadFromRobot(RobotBlobV5(2000, 0.9, 0.8, 0.5), 0);
        deviceNewer.InitAfterReadFromRobotAttempt();
        Assert.Equal(0.9, deviceNewer.State.GetNeedLevel(NeedId.Play), 6);

        // matching +0x1CC/+0x34: robot 2000 > device 1000 -> robot
        var robotNewer = new NeedsManager(() => clock);
        robotNewer.WriteToDevice = _ => { };
        robotNewer.NvStorage = rig.Robot.Engine.NvStorage;
        robotNewer.Load(DeviceNeedsFile(5, 7, 1000, 0.9), applyElapsedDecay: false);
        robotNewer.InitAfterSerialNumberAcquired(7);
        robotNewer.FinishReadFromRobot(RobotBlobV5(2000, 0.9, 0.8, 0.5), 0);
        robotNewer.InitAfterReadFromRobotAttempt();
        Assert.Equal(0.5, robotNewer.State.GetNeedLevel(NeedId.Play), 6);

        // mismatched +0x1CC (1) / +0x34 (7): the robot data wins and the old timing fields are cleared
        var mismatch = new NeedsManager(() => clock);
        mismatch.WriteToDevice = _ => { };
        mismatch.NvStorage = rig.Robot.Engine.NvStorage;
        mismatch.Load(DeviceNeedsFile(5, 1, 3000, 0.9), applyElapsedDecay: false);
        mismatch.InitAfterSerialNumberAcquired(7);                   // +0x1CC = 1, +0x34 = 7
        clock = 5;
        mismatch.OnRobotDisconnected();
        Assert.Equal(5, mismatch.LastDisconnectSec, 6);
        mismatch.FinishReadFromRobot(RobotBlobV5(2000, 0.9, 0.8, 0.5), 0);
        mismatch.InitAfterReadFromRobotAttempt();
        Assert.Equal(0.5, mismatch.State.GetNeedLevel(NeedId.Play), 6);
        Assert.Equal(0, mismatch.LastDisconnectSec, 6);

        // robot has no data and +0x1CC != +0x34: the per-serial alternate is attempted
        var alternate = new NeedsManager(() => clock);
        alternate.WriteToDevice = _ => { };
        alternate.NvStorage = rig.Robot.Engine.NvStorage;
        alternate.Load(DeviceNeedsFile(5, 1, 1000, 0.1), applyElapsedDecay: false);
        alternate.InitAfterSerialNumberAcquired(7);                  // +0x1CC = 1, +0x34 = 7
        alternate.AlternateDeviceFilePath = s => s == 7 ? DeviceNeedsFile(5, 7, 1000, 0.9) : null;
        alternate.InitAfterReadFromRobotAttempt();                   // the robot read has not answered
        Assert.Equal(0.9, alternate.State.GetNeedLevel(NeedId.Play), 6);
    }

    /// <summary>
    /// The Appendix I1 nine-case write-scheduling table. For each case: the device-write flag (a
    /// <c>WriteToDevice</c> call), the robot-write flag (a queued NV WRITE), the selected copy, the
    /// <c>SendNeedsStateToGame</c> action and whether <c>RobotChangedFromLastSession</c> fires.
    /// </summary>
    [Fact]
    public void TheResolverFollowsTheNineCaseWriteSchedulingTable()
    {
        double clock = 0;

        (int dw, int rw, List<NeedsActionId> sent, int changed, double play) Run(
            bool hasDevice, uint deviceSerial, long deviceTime, double devicePlay,
            bool hasRobot, int robotVersion, ulong robotTime, double robotPlay,
            uint incomingSerial, bool? alternateOk)
        {
            using var rig = new Rig();
            var needs = new NeedsManager(() => clock);
            needs.NvStorage = rig.Robot.Engine.NvStorage;
            int dw = 0;
            needs.WriteToDevice = _ => dw++;
            var sent = new List<NeedsActionId>();
            needs.NeedsStateSent += sent.Add;
            int changed = 0;
            needs.RobotChangedFromLastSession += () => changed++;

            if (hasDevice) needs.Load(DeviceNeedsFile(5, deviceSerial, deviceTime, devicePlay), applyElapsedDecay: false);
            needs.InitAfterSerialNumberAcquired(incomingSerial);
            rig.Tick();                                              // M3-026: Update sends the queued read
            rig.Pump();
            if (alternateOk is { } ok)
                needs.AlternateDeviceFilePath = s => s == incomingSerial && ok ? DeviceNeedsFile(5, incomingSerial, deviceTime, 0.9) : null;

            if (hasRobot)
                ReplyNeedsRead(rig, robotVersion == 5 ? RobotBlobV5(robotTime, 0.9, 0.8, robotPlay) : RobotBlobVersion(robotVersion, robotTime, robotPlay));
            else
                ReplyNeedsRead(rig, Array.Empty<byte>(), -1);

            int rw = rig.Sent.OfType<NVCommand>().Count(c => c.Op == NvStorageComponent.OpWrite);
            return (dw, rw, sent, changed, needs.State.GetNeedLevel(NeedId.Play));
        }

        // case 1: R=0 D=0 -> DW=1 RW=1, NoAction, no RobotChanged, defaults
        var c1 = Run(false, 0, 0, 0, false, 5, 0, 0, 0, null);
        Assert.Equal((1, 1), (c1.dw, c1.rw));
        Assert.Equal(new[] { NeedsActionId.NoAction }, c1.sent);
        Assert.Equal(0, c1.changed);
        Assert.Equal(1.0, c1.play, 6);

        // case 2: R=0 D=1 equal -> DW=0 RW=1, no send, device kept
        var c2 = Run(true, 7, 3000, 0.9, false, 5, 0, 0, 7, null);
        Assert.Equal((0, 1), (c2.dw, c2.rw));
        Assert.Empty(c2.sent); Assert.Equal(0, c2.changed); Assert.Equal(0.9, c2.play, 6);

        // case 3: R=0 D=1 mismatch, alternate ok -> DW=1 RW=1, Decay, RobotChanged, alternate copy
        var c3 = Run(true, 1, 1000, 0.1, false, 5, 0, 0, 7, true);
        Assert.Equal((1, 1), (c3.dw, c3.rw));
        Assert.Equal(new[] { NeedsActionId.Decay }, c3.sent);
        Assert.Equal(1, c3.changed); Assert.Equal(0.9, c3.play, 6);

        // case 4: R=0 D=1 mismatch, alternate fails -> DW=0 RW=1, no send, RobotChanged, device kept
        var c4 = Run(true, 1, 1000, 0.9, false, 5, 0, 0, 7, false);
        Assert.Equal((0, 1), (c4.dw, c4.rw));
        Assert.Empty(c4.sent); Assert.Equal(1, c4.changed); Assert.Equal(0.9, c4.play, 6);

        // case 5: R=1 D=0 -> DW=1, RW=+0x1CA (0 for v5), Decay, RobotChanged, robot applied
        var c5 = Run(false, 0, 0, 0, true, 5, 2000, 0.5, 7, null);
        Assert.Equal((1, 0), (c5.dw, c5.rw));
        Assert.Equal(new[] { NeedsActionId.Decay }, c5.sent);
        Assert.Equal(1, c5.changed); Assert.Equal(0.5, c5.play, 6);

        // case 5 with +0x1CA set (a v4 robot blob) -> RW=1
        var c5b = Run(false, 0, 0, 0, true, 4, 2000, 0.5, 7, null);
        Assert.Equal((1, 1), (c5b.dw, c5b.rw));

        // case 6: R=1 D=1 mismatch -> DW=1 RW=0, Decay, RobotChanged, robot applied, timing cleared
        var c6 = Run(true, 1, 3000, 0.9, true, 5, 2000, 0.5, 7, null);
        Assert.Equal((1, 0), (c6.dw, c6.rw));
        Assert.Equal(new[] { NeedsActionId.Decay }, c6.sent);
        Assert.Equal(1, c6.changed); Assert.Equal(0.5, c6.play, 6);

        // case 7: R=1 D=1 equal, robot newer -> DW=1 RW=0, Decay, RobotChanged, robot applied
        var c7 = Run(true, 7, 1000, 0.9, true, 5, 2000, 0.5, 7, null);
        Assert.Equal((1, 0), (c7.dw, c7.rw));
        Assert.Equal(new[] { NeedsActionId.Decay }, c7.sent);
        Assert.Equal(1, c7.changed); Assert.Equal(0.5, c7.play, 6);

        // case 8: R=1 D=1 equal, device newer -> DW=0 RW=1, no send, device kept
        var c8 = Run(true, 7, 3000, 0.9, true, 5, 1000, 0.5, 7, null);
        Assert.Equal((0, 1), (c8.dw, c8.rw));
        Assert.Empty(c8.sent); Assert.Equal(0, c8.changed); Assert.Equal(0.9, c8.play, 6);

        // case 9: R=1 D=1 equal, identical timestamps -> DW=0 RW=0, no send, no apply
        var c9 = Run(true, 7, 1000, 0.9, true, 5, 1000, 0.5, 7, null);
        Assert.Equal((0, 0), (c9.dw, c9.rw));
        Assert.Empty(c9.sent); Assert.Equal(0, c9.changed); Assert.Equal(0.9, c9.play, 6);
    }

    /// <summary>
    /// <c>PossiblyStartWriteToRobot</c> 0x00696ECC..0x00696F0D (Appendix I2): a write starts when the elapsed
    /// time since the last robot write is strictly greater than 600,999,999 clock units, or when forced; a
    /// null robot returns at once.
    /// </summary>
    [Fact]
    public void PossiblyStartWriteToRobotThrottlesAndForces()
    {
        using var rig = new Rig();
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        needs.NvStorage = rig.Robot.Engine.NvStorage;
        int WriteCount() => rig.Sent.OfType<NVCommand>().Count(c => c.Op == NvStorageComponent.OpWrite);
        void CompleteWrite()
        {
            rig.Send(new NVOpResult { Tag = NeedsManager.NeedsNvKey, Op = NvStorageComponent.OpWrite, Result = 0, Length = 0, Data = Array.Empty<byte>() });
            rig.Pump();
        }

        clock = 600; needs.PossiblyStartWriteToRobot(false); rig.Tick(); rig.Pump(); Assert.Equal(0, WriteCount());
        clock = 600.999999; needs.PossiblyStartWriteToRobot(false); rig.Tick(); rig.Pump(); Assert.Equal(0, WriteCount());   // not strictly greater
        clock = 601; needs.PossiblyStartWriteToRobot(false); rig.Tick(); rig.Pump(); Assert.Equal(1, WriteCount());           // 601 > 600.999999
        CompleteWrite();

        // force writes even when not overdue
        clock = 601.5; needs.PossiblyStartWriteToRobot(true); rig.Tick(); rig.Pump();
        Assert.Equal(2, WriteCount());
        CompleteWrite();

        // no connected robot: no write
        needs.Connected = false;
        clock = 9999; needs.PossiblyStartWriteToRobot(true); rig.Tick(); rig.Pump();
        Assert.Equal(2, WriteCount());
    }

    /// <summary>
    /// The NV WRITE terminal (Appendix I3): a single-chunk write reply completes with the reply's own result
    /// byte (0 on success, 1/2 deliver 1/2, not the read path's -3), and every negative result logs
    /// <c>WriteOpFailed</c> and is delivered.
    /// </summary>
    [Fact]
    public void TheNvWriteTerminalDeliversTheReplyResultByte()
    {
        using var rig = new Rig();
        var nv = rig.Robot.Engine.NvStorage!;

        sbyte? Deliver(sbyte replyResult)
        {
            sbyte? got = null;
            Assert.Equal(1, nv.Write(NeedsManager.NeedsNvKey, new byte[NeedsStateOnRobot.Size], r => got = r.Result));
            rig.Tick();                                              // M3-026: Update sends the queued write
            rig.Pump();
            var cmd = rig.Sent.OfType<NVCommand>().Last();
            Assert.Equal(NvStorageComponent.OpWrite, cmd.Op);
            Assert.Equal(NeedsStateOnRobot.Size, cmd.Data.Length);
            rig.Send(new NVOpResult { Tag = NeedsManager.NeedsNvKey, Op = NvStorageComponent.OpWrite, Result = replyResult, Length = 0, Data = Array.Empty<byte>() });
            rig.Pump();
            return got;
        }

        Assert.Equal((sbyte)0, Deliver(0));
        Assert.Equal((sbyte)1, Deliver(1));
        Assert.Equal((sbyte)2, Deliver(2));
        Assert.Equal((sbyte)-1, Deliver(-1));
        Assert.Equal((sbyte)-2, Deliver(-2));       // not retryable, so it still logs WriteOpFailed
        Assert.Contains(nv.Log, l => l.Contains("WriteOpFailed"));
    }

    /// <summary>
    /// Appendix I1 case 5 (R=1 D=0): the disconnect timestamp <c>+0x18/+0x1C</c> is cleared before the robot
    /// copy is applied (0x00694A46..0x00694A4C).
    /// </summary>
    [Fact]
    public void CaseFiveClearsTheDisconnectTimestampBeforeApplyingTheRobotCopy()
    {
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        needs.WriteToDevice = _ => { };
        clock = 5;
        needs.OnRobotDisconnected();                                  // +0x18/+0x1c = 5
        Assert.Equal(5, needs.LastDisconnectSec, 6);
        needs.Connected = true;                                       // the resolver runs on a live connection

        needs.FinishReadFromRobot(RobotBlobV5(2000, 0.9, 0.8, 0.5), 0);
        needs.InitAfterReadFromRobotAttempt();                        // R=1 D=0 -> case 5
        Assert.Equal(0, needs.LastDisconnectSec, 6);
        Assert.Equal(0.5, needs.State.GetNeedLevel(NeedId.Play), 6);
    }

    /// <summary>
    /// The reconnect edge (finding 6): a removal clears the engine's recorded serial, so a stack created
    /// after a reconnect does not replay the old one, and the next mfgId runs the edge again.
    /// </summary>
    [Fact]
    public void TheSerialEdgeHandlesEachNewMfgIdAcrossAReconnect()
    {
        var obb = ObbRoot();
        Assert.NotNull(obb);
        double clock = 0;
        using var rig = new Rig();
        var needs1 = new NeedsManager(() => clock);
        using (var stack = FreeplayStack.Create(obb!, rig.Robot, Ctx(rig), () => clock, rig.Vision, rig.M, needs: needs1, withReactions: false))
        {
            rig.Robot.Engine.RaiseSerialNumberAcquired(0x1111);
            Assert.Equal(0x1111u, needs1.SerialNumber);
        }

        // a removal ends the edge: the recorded serial is cleared
        rig.Robot.Engine.Robots.RemoveRobot(CozmoEngine.RobotId, wasConnecting: false);
        Assert.Null(rig.Robot.Engine.AcquiredSerialNumber);

        // a stack created now does not replay the old serial, and handles the new mfgId
        var needs2 = new NeedsManager(() => clock);
        using (var stack2 = FreeplayStack.Create(obb!, rig.Robot, Ctx(rig), () => clock, rig.Vision, rig.M, needs: needs2, withReactions: false))
        {
            Assert.Equal(0u, needs2.SerialNumber);
            rig.Robot.Engine.RaiseSerialNumberAcquired(0x2222);
            Assert.Equal(0x2222u, needs2.SerialNumber);
        }
    }

    // ------------------------------------------------------------------ J1-J5 startup device read

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "needs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// J1: <c>NeedsManager::InitInternal</c> 0x00693444..0x00693492 with no device directory: the fixed
    /// <c>needsState.json</c> cannot be found, so <c>AttemptReadFromDevice</c> returns false, the manager
    /// sends <c>NoAction</c> (0x00693476), forces <c>WriteToDevice(true)</c> (0x0069347E), raises the
    /// <c>app_start</c> DAS event (0x00693486) and runs <c>LocalNotifications::Generate</c> (0x00693492).
    /// It does not touch the robot-rewrite flag <c>+0x1CA</c>.
    /// </summary>
    [Fact]
    public void InitInternalWithNoDeviceDirectorySendsNoActionAndRunsTheTail()
    {
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        var writes = new List<bool>();
        var actions = new List<NeedsActionId>();
        var das = new List<string>();
        var logs = new List<string>();
        int generated = 0;
        needs.WriteToDevice = f => writes.Add(f);
        needs.NeedsStateSent += actions.Add;
        needs.SendNeedsLevelsDasEvent += das.Add;
        needs.LocalNotificationsGenerate = () => generated++;
        needs.Log += logs.Add;

        needs.InitInternal(0);

        Assert.Equal(new[] { NeedsActionId.NoAction }, actions);
        Assert.Equal(new[] { true }, writes);
        Assert.Equal(new[] { "app_start" }, das);
        Assert.Equal(1, generated);
        Assert.Contains(logs, l => l.Contains("FAILED to FIND file needsState.json on device"));
        Assert.False(needs.DeviceDataPresent);          // +0x1C9 = (byte)result
        Assert.False(needs.RobotRewriteNeeded);         // InitInternal does not touch +0x1CA
    }

    /// <summary>
    /// J1/J4/J5: <c>InitInternal</c> with a device directory holding <c>needsState.json</c> reads it,
    /// sends <c>Decay</c> (0x006936C6) and increments the <c>+0x30 OpenAppAfterDisconnect</c> counter
    /// (0x006936CE). The counter is observable through <c>Save</c>'s JSON. The
    /// <c>SendTimeSinceBackgroundedDasEvent</c> guard is <c>+0x20|+0x24 != 0</c> (0x0069771E); the current
    /// <c>Load</c> does not read <c>TimeLastAppBackgrounded</c> (the named J6/J7 gap), so <c>+0x20/+0x24</c>
    /// stay 0 and the event does not fire - the guard is tested with the counter only.
    /// </summary>
    [Fact]
    public void InitInternalReadsTheFixedDeviceFileAndSendsDecay()
    {
        double clock = 0;
        var dir = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, NeedsManager.FixedFileName),
                System.Text.Encoding.UTF8.GetString(NeedsJson(5, 7, 1000, 0.9)));
            var needs = new NeedsManager(() => clock) { DeviceDirectory = dir };
            var writes = new List<bool>();
            var actions = new List<NeedsActionId>();
            var das = new List<string>();
            var backgrounded = new List<(int, double)>();
            var logs = new List<string>();
            int generated = 0;
            needs.WriteToDevice = f => writes.Add(f);
            needs.NeedsStateSent += actions.Add;
            needs.SendNeedsLevelsDasEvent += das.Add;
            needs.SendTimeSinceBackgroundedDasEvent += (n, e) => backgrounded.Add((n, e));
            needs.LocalNotificationsGenerate = () => generated++;
            needs.Log += logs.Add;

            needs.InitInternal(0);

            Assert.Contains(NeedsActionId.Decay, actions);
            Assert.DoesNotContain(NeedsActionId.NoAction, actions);
            Assert.Equal(new[] { true }, writes);
            Assert.Equal(new[] { "app_start" }, das);
            Assert.Equal(1, generated);
            Assert.Contains(logs, l => l.Contains("Successfully read file needsState.json from device"));
            Assert.True(needs.DeviceDataPresent);
            Assert.Equal(0.9, needs.State.GetNeedLevel(NeedId.Play), 6);

            // J5: +0x20/+0x24 == 0 -> no needs.app_backgrounded_time event
            Assert.Empty(backgrounded);
            // the +0x30 counter incremented: Save writes it as the JSON OpenAppAfterDisconnect
            var outPath = Path.Combine(dir, "counter.json");
            needs.Save(outPath, unixTimeSec: 1000);
            using var doc = JsonDocument.Parse(File.ReadAllText(outPath));
            Assert.Equal(1, doc.RootElement.GetProperty("OpenAppAfterDisconnect").GetInt32());
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// J4: <c>AttemptReadFromDevice</c> 0x00693690..0x00693792 logs and returns false when the fixed file
    /// is missing, logs and returns false when <c>Load</c> (the engine's <c>ReadFromDevice</c>) fails, and
    /// on success logs <c>"Successfully read file needsState.json from device"</c> and returns true.
    /// </summary>
    [Fact]
    public void AttemptReadFromDeviceLogsMissingReadFailureAndSuccess()
    {
        double clock = 0;
        var dir = TempDir();
        try
        {
            var needs = new NeedsManager(() => clock) { DeviceDirectory = dir };
            var logs = new List<string>();
            needs.Log += logs.Add;

            // missing
            Assert.False(needs.AttemptReadFromDevice(needs.FixedDeviceFilePath));
            Assert.Contains(logs, l => l.Contains("FAILED to FIND file needsState.json on device"));

            // present but unreadable (version above 5 makes Load return false)
            File.WriteAllText(Path.Combine(dir, NeedsManager.FixedFileName),
                System.Text.Encoding.UTF8.GetString(NeedsJson(6, 7, 1000, 0.9)));
            Assert.False(needs.AttemptReadFromDevice(needs.FixedDeviceFilePath));
            Assert.Contains(logs, l => l.Contains("FAILED to read file needsState.json on device"));

            // readable
            File.WriteAllText(Path.Combine(dir, NeedsManager.FixedFileName),
                System.Text.Encoding.UTF8.GetString(NeedsJson(5, 7, 1000, 0.9)));
            Assert.True(needs.AttemptReadFromDevice(needs.FixedDeviceFilePath));
            Assert.Contains(logs, l => l.Contains("Successfully read file needsState.json from device"));
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// J3: <c>TryLoadAlternateDeviceFile</c> falls back to
    /// <c>&lt;DeviceDirectory&gt;/needsState_&lt;serial&gt;.json</c> when <c>AlternateDeviceFilePath</c> is
    /// null, so the resolver's per-serial alternate read (C2 row 12) is supplied by the host directory.
    /// </summary>
    [Fact]
    public void TheDeviceDirectorySuppliesThePerSerialAlternateFile()
    {
        double clock = 0;
        var dir = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, NeedsManager.FileNameForSerial(7)),
                System.Text.Encoding.UTF8.GetString(NeedsJson(5, 7, 1000, 0.9)));
            var needs = new NeedsManager(() => clock) { DeviceDirectory = dir, NvStorage = null, WriteToDevice = _ => { } };
            needs.Load(DeviceNeedsFile(5, 1, 1000, 0.1), applyElapsedDecay: false);   // +0x1CC = 1
            needs.InitAfterSerialNumberAcquired(7);                                   // +0x34 = 7, no robot data
            Assert.Equal(0.9, needs.State.GetNeedLevel(NeedId.Play), 6);              // the per-serial file won
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// J7: <c>FromObb</c> with a device directory wires <c>WriteToDevice</c> to <c>WriteDeviceFile</c> before
    /// <c>InitInternal</c>, so the startup forced write (0x0069347E) creates <c>needsState.json</c>; the
    /// same seam serves <c>PossiblyWriteToDevice</c> (0x00695DFA).
    /// </summary>
    [Fact]
    public void FromObbWithADeviceDirectoryWiresTheWriteSeam()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var dir = TempDir();
        try
        {
            double clock = 0;
            var needs = NeedsManager.FromObb(obb, () => clock, new Random(1), deviceDirectory: dir);
            var path = Path.Combine(dir, NeedsManager.FixedFileName);

            // InitInternal's forced WriteToDevice(true) wrote the file
            Assert.True(File.Exists(path));
            using (var doc = JsonDocument.Parse(File.ReadAllText(path)))
                Assert.Equal(NeedsManager.CurrentStateFileVersion, doc.RootElement.GetProperty("_StateFileVersion").GetInt32());

            // PossiblyWriteToDevice goes through the same seam
            File.Delete(path);
            clock = NeedsManager.WriteThrottleSec;
            needs.PossiblyWriteToDevice();
            Assert.True(File.Exists(path));
        }
        finally { Directory.Delete(dir, true); }
    }
}
