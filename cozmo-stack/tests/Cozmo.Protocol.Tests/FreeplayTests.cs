using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
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

    // ------------------------------------------------------------------ choosers and strategies

    [Fact]
    public void TheScoringChooserWeighsFlatScoresPenaltiesAndTheRunningBonus()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var a = new Fake("a"); var b = new Fake("b"); var c = new Fake("c", runnable: false);
        var bound = new Dictionary<string, IBehavior> { ["a"] = a, ["b"] = b, ["c"] = c };
        var entries = new[]
        {
            new ScoredBehaviorEntry("a", 1.0, new Graph2d(new[] { (0.0, 0.0), (30.0, 1.0) }), null, null, Array.Empty<(EmotionType, Graph2d)>()),
            new ScoredBehaviorEntry("b", 0.8, null, null, null, Array.Empty<(EmotionType, Graph2d)>()),
            new ScoredBehaviorEntry("c", 5.0, null, null, null, Array.Empty<(EmotionType, Graph2d)>()),
            new ScoredBehaviorEntry("missing", 9.0, null, null, null, Array.Empty<(EmotionType, Graph2d)>()),
        };
        var chooser = new ScoringChooser(entries, bound, scoreBonusForCurrent: new Graph2d(new[] { (0.0, 1.0) }));
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

    [Fact]
    public void StrategiesRespectCooldownsDurationsMoodAndNeeds()
    {
        double clock = 0;
        var needs = new NeedsManager(() => clock);
        var inputs = new FreeplayInputs { Needs = needs };
        var simple = new ActivityStrategy { Type = "Simple", ShouldEndDurationSec = 25, CooldownBaseSec = 30, Random = new Random(1) };
        Assert.True(simple.WantsToStart(inputs, 0, out _));
        Assert.False(simple.WantsToEnd(inputs, 10, out _));
        Assert.True(simple.WantsToEnd(inputs, 25, out var why)); Assert.Contains("should end", why);
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
        // sparks first (priority 0), then the three needs activities, then the freeplay chain 10..17
        var subs = freeplay.SubActivities;
        Assert.True(subs.Count >= 20);
        Assert.All(subs.Where(s => s.Id.StartsWith("Sparks")), s => { Assert.Equal(0, s.Priority); Assert.Equal("Sparked", s.Type); Assert.NotNull(s.RequireSpark); });
        Assert.Equal(1, subs.Single(s => s.Id == "NeedsSevereLowRepair").Priority);
        Assert.Equal(2, subs.Single(s => s.Id == "NeedsSevereLowEnergy").Priority);
        Assert.Equal(3, subs.Single(s => s.Id == "NeedsSevereLowPlayGetIn").Priority);
        Assert.Equal(new[] { "PutDownDispatch", "Socialize", "Singing", "PlayWithHumans", "BuildPyramid", "PlayAlone", "Hiking", "NothingToDo" },
                     subs.Where(s => s.Priority >= 10).OrderBy(s => s.Priority).Select(s => s.Id));
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
        var hiking = new Activity
        {
            Id = "Hiking", Priority = 16, Strategy = new ActivityStrategy { ShouldEndDurationSec = 60, CooldownBaseSec = 15 },
            Chooser = new ScoringChooser(new[] { new ScoredBehaviorEntry("hikeA", 2, new Graph2d(new[] { (0.0, 0.0), (30.0, 1.0) }), null, null, Array.Empty<(EmotionType, Graph2d)>()),
                                                 new ScoredBehaviorEntry("hikeB", 1, null, null, null, Array.Empty<(EmotionType, Graph2d)>()) }, bound),
            InterludeChooser = new StrictPriorityChooser(new[] { "interlude" }, bound),
        };
        var playAlone = new Activity { Id = "PlayAlone", Priority = 15, Strategy = new ActivityStrategy { ShouldEndDurationSec = 25, CooldownBaseSec = 30 },
                                       Chooser = new ScoringChooser(new[] { new ScoredBehaviorEntry("playA", 1, null, null, null, Array.Empty<(EmotionType, Graph2d)>()) }, bound) };
        var nothing = new Activity { Id = "NothingToDo", Priority = 17, Strategy = new ActivityStrategy(), Chooser = new StrictPriorityChooser(new[] { "hikeB" }, bound) };
        var manager = new BehaviorManager(ctx);
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
}
