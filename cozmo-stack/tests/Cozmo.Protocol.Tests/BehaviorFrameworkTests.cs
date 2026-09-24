using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Tests for the reconstructed behaviour framework: scoped resources, the repetition penalty, and
/// score-based selection.
/// </summary>
public class BehaviorFrameworkTests
{
    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "assets", "animationGroups")))
                return r;
            d = d.Parent;
        }
        return null;
    }

    // ------------------------------------------------------------------ scoped resources

    /// <summary>
    /// The engine's Smart* methods exist so a behaviour cannot leak a lock by forgetting to undo it.
    /// Disposing the scope must release everything it took.
    /// </summary>
    [Fact]
    public void AScopeReleasesEverythingItTook()
    {
        var scope = new BehaviorScope();
        scope.LockTracks(AnimationTrack.Head | AnimationTrack.Lift);
        scope.DisableReactions();
        Assert.Equal(AnimationTrack.Head | AnimationTrack.Lift, scope.LockedTracks);
        Assert.True(scope.ReactionsDisabled);

        scope.Dispose();
        Assert.Equal(AnimationTrack.None, scope.LockedTracks);
        Assert.False(scope.ReactionsDisabled);
    }

    [Fact]
    public void AScopeRunsCustomUndoInReverseOrder()
    {
        var order = new List<int>();
        var scope = new BehaviorScope();
        scope.OnRelease(() => order.Add(1));
        scope.OnRelease(() => order.Add(2));
        scope.Dispose();
        Assert.Equal(new[] { 2, 1 }, order);
    }

    /// <summary>Registering after disposal runs the undo immediately rather than leaking it.</summary>
    [Fact]
    public void RegisteringAfterDisposalStillReleases()
    {
        var scope = new BehaviorScope();
        scope.Dispose();
        bool ran = false;
        scope.OnRelease(() => ran = true);
        Assert.True(ran);
    }

    // ------------------------------------------------------------------ repetition penalty

    /// <summary>
    /// mood_config.json's defaultRepetitionPenalty runs from (0 s, 0.0) to (30 s, 1.0): a behaviour that
    /// just ran scores nothing and recovers linearly over thirty seconds.
    /// </summary>
    [Fact]
    public void TheRepetitionPenaltyMatchesTheShippedCurve()
    {
        var p = new RepetitionPenalty();
        Assert.Equal(1.0, p.For("X", 0));
        p.Ran("X", 100);
        Assert.Equal(0.0, p.For("X", 100), 3);
        Assert.Equal(0.5, p.For("X", 115), 2);
        Assert.Equal(1.0, p.For("X", 130), 3);
        Assert.Equal(1.0, p.For("X", 500), 3);
    }

    [Fact]
    public void ForgettingClearsThePenalty()
    {
        var p = new RepetitionPenalty();
        p.Ran("X", 100);
        p.Forget("X");
        Assert.Equal(1.0, p.For("X", 100), 3);
    }

    // ------------------------------------------------------------------ selection

    private sealed class Fake : IBehavior
    {
        public Fake(string id, double score, bool runnable = true)
        { Id = id; Score = score; Runnable = runnable; }
        public string Id { get; }
        public string Class => "Fake";
        public double Score { get; set; }
        public bool Runnable { get; set; }
        public bool Started, Stopped;
        public BehaviorStopReason? Reason;
        public bool IsRunnable(BehaviorContext c) => Runnable;
        public double EvaluateScore(BehaviorContext c) => Score;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t)
        { Started = true; return Task.CompletedTask; }
        public bool Update(BehaviorContext c, double nowMs) => true;
        public void Stop(BehaviorStopReason r) { Stopped = true; Reason = r; }
    }

    private static BehaviorManager Manager(CozmoRobot robot, string obb)
    {
        var ctx = new BehaviorContext
        {
            Robot = robot,
            Triggers = AnimationTriggerMap.Load(obb),
            Random = new Random(5),
        };
        return new BehaviorManager(ctx);
    }

    [Fact]
    public void TheHighestScoringRunnableBehaviourWins()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        using var m = Manager(robot, obb);

        m.Add(new Fake("low", 1));
        m.Add(new Fake("high", 9));
        m.Add(new Fake("higher-but-not-runnable", 99, runnable: false));

        var s = m.ChooseAndSwitch(0);
        Assert.Equal("high", s.Chosen);
        Assert.Contains(s.Scores, x => x.Id == "higher-but-not-runnable" && x.Note == "not runnable");
    }

    [Fact]
    public void ChoosingNothingIsReportedRatherThanSilent()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        using var m = Manager(robot, obb);
        m.Add(new Fake("none", 0));

        var reported = new List<BehaviorSelection>();
        m.Selected += reported.Add;
        var s = m.ChooseAndSwitch(0);
        Assert.Null(s.Chosen);
        Assert.Single(reported);
        Assert.Contains("no behaviour", s.Reason);
    }

    /// <summary>
    /// The point of the penalty: having run, a behaviour stops winning for a while, so a robot with two
    /// similar options alternates rather than repeating one.
    /// </summary>
    [Fact]
    public void ABehaviourThatJustRanLosesToOneThatHasNot()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        using var m = Manager(robot, obb);
        m.Add(new Fake("a", 5));
        m.Add(new Fake("b", 4));

        Assert.Equal("a", m.ChooseAndSwitch(0).Chosen);
        m.Stop(BehaviorStopReason.Completed, 0);
        Assert.Equal("b", m.ChooseAndSwitch(1).Chosen);
        m.Stop(BehaviorStopReason.Completed, 1);
        Assert.Equal("a", m.ChooseAndSwitch(100).Chosen);
    }

    /// <summary>
    /// The engine has StopWithoutImmediateRepetitionPenalty for exactly this: being interrupted is not
    /// the behaviour's fault, so it is not penalised for it.
    /// </summary>
    [Fact]
    public void AnInterruptedBehaviourIsNotPenalised()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        using var m = Manager(robot, obb);
        m.Add(new Fake("a", 5));
        m.ChooseAndSwitch(0);
        m.Stop(BehaviorStopReason.Interrupted, 0);
        Assert.Equal("a", m.ChooseAndSwitch(1).Chosen);
    }

    // ------------------------------------------------------------------ the shipped behaviours

    /// <summary>
    /// The runnable set is honest: it grew from 5 to 20 in M10 because the off-treads classifier, the shake
    /// and unexpected-movement detectors and the calibration reports removed real blockers, and because six
    /// PlayAnim configs only ever needed their trigger. If it grows again it should be for the same kind of
    /// reason, not because something was stubbed with a fake input.
    /// </summary>
    [Fact]
    public void OnlyTheBehavioursWhoseInputsExistAreBuilt()
    {
        var built = ShippedBehaviors.Implementable();
        Assert.Equal(20, built.Count);
        Assert.Equal(new[]
        {
            "FeedingReactCubeShake", "FeedingReactCubeShake_Severe", "FeedingReactFullCube", "FeedingReactFullCube_Severe",
            "FeedingReactSeeCharged", "FeedingReactSeeCharged_Severe", "Hiccup", "PlayArbitraryAnim",
            "ReactToCliff", "ReactToFrustrationMinor", "ReactToMotorCalibration", "ReactToObstacle", "ReactToPickup",
            "ReactToPlacedOnSlope", "ReactToReturnedToTreads", "ReactToRobotOnBack", "ReactToRobotOnFace",
            "ReactToRobotOnSide", "ReactToRobotShaken", "ReactToUnexpectedMovement",
        }, built.Select(b => b.Id).OrderBy(x => x, StringComparer.Ordinal));
    }

    /// <summary>Every built behaviour's id and class must match a shipped config, not be made up.</summary>
    [Fact]
    public void EveryBuiltBehaviourMatchesAShippedConfig()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var dir = Path.Combine(obb, "assets", "cozmo_resources", "config", "engine",
                               "behaviorSystem", "behaviors");
        if (!Directory.Exists(dir)) return;

        var shipped = new Dictionary<string, string>();
        foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            var text = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(f), "//[^\n\r]*", "");
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("behaviorID", out var id) &&
                    doc.RootElement.TryGetProperty("behaviorClass", out var cls))
                    shipped[id.GetString()!] = cls.GetString()!;
            }
            catch (System.Text.Json.JsonException) { }
        }

        foreach (var b in ShippedBehaviors.Implementable())
        {
            Assert.True(shipped.ContainsKey(b.Id), $"{b.Id} is not a shipped behaviorID");
            Assert.Equal(shipped[b.Id], b.Class);
        }
    }

    /// <summary>
    /// A PlayAnim config that lists several triggers plays all of them, in order.
    /// BehaviorPlayAnimSequence::StartPlayingAnimations 0x005C0158 special-cases a list of exactly one
    /// trigger (cmp r1, #4 on the vector's byte length) and sends everything else to StartSequenceLoop
    /// 0x005C0294, which puts one TriggerLiftSafeAnimationAction per trigger into a
    /// CompoundActionSequential and repeats the whole list until the counter reaches num_loops (default 1,
    /// from Json::Value::Value(1) at 0x005C001C). NothingToDo_BoredAnim is the one shipped config that
    /// lists more than one - its own comment calls them a "sequence of anims" - and this stack used to
    /// play only the first.
    /// </summary>
    [Fact]
    public void APlayAnimBehaviourWithSeveralTriggersTakesThemAll()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var shipped = PlayAnimBehavior.LoadShipped(obb);
        var bored = shipped.FirstOrDefault(b => b.Id == "NothingToDo_BoredAnim");
        Assert.NotNull(bored);
        Assert.Equal(new[] { AnimationTrigger.NothingToDoBoredIntro, AnimationTrigger.NothingToDoBoredEvent,
                             AnimationTrigger.NothingToDoBoredOutro }, bored!.Triggers);
        Assert.Equal(1, bored.NumLoops);

        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext
        {
            Robot = robot,
            Triggers = AnimationTriggerMap.Load(obb),
            Random = new Random(4),
        };

        // every trigger's clip is resolved, which is why the scope ends up holding the union of their
        // tracks rather than only the first clip's
        using var scope = new BehaviorScope();
        bored.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        var lib = robot.Animations.Library!;
        AnimationTrack union = AnimationTrack.None;
        foreach (var t in bored.Triggers)
        {
            var r = ctx.Triggers.Resolve(t, lib, new Random(4));
            if (r.Resolved) union |= lib.GetClip(r.Selected!).Tracks;
        }
        Assert.Equal(union, scope.LockedTracks);
        bored.Stop(BehaviorStopReason.Interrupted);
    }

    /// <summary>
    /// The two wants-to-run strategies that read the needs, as the engine evaluates them.
    /// StrategyInNeedsBracket::WantsToRunInternal 0x006141A0 is a single call to
    /// NeedsState::IsNeedAtBracket(need, bracket); StrategyExpressNeedsTransition::WantsToRunInternal
    /// 0x006136D8 asks IsNeedAtBracket(need, Critical) - the literal 3 at 0x006136EC - and then refuses
    /// when that need is the one already being expressed (0x006136FC). Before this, a behaviour with
    /// either strategy reported not runnable whatever the needs said.
    /// </summary>
    [Fact]
    public void TheNeedsWantsToRunStrategiesFollowTheNeedsState()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        double now = 0;
        var needs = new NeedsManager(() => now);
        var ctx = new BehaviorContext
        {
            Robot = robot,
            Triggers = new AnimationTriggerMap(),
            Needs = needs,
            Random = new Random(1),
        };

        var inBracket = new PlayAnimBehavior("b", "PlayAnim", new[] { AnimationTrigger.Hiccup })
        {
            WantsToRunStrategy = "InNeedsBracket", StrategyNeed = NeedId.Energy, StrategyBracket = NeedBracketId.Critical,
        };
        var transition = new PlayAnimBehavior("t", "PlayAnim", new[] { AnimationTrigger.Hiccup })
        {
            WantsToRunStrategy = "ExpressNeedsTransition", StrategyNeed = NeedId.Energy,
        };

        needs.SetLevel(NeedId.Energy, 1.0);
        Assert.False(inBracket.WantsToRunNow(ctx));
        Assert.False(transition.WantsToRunNow(ctx));

        needs.SetLevel(NeedId.Energy, 0.0);                       // Critical
        Assert.Equal(NeedBracketId.Critical, needs.State.GetNeedBracket(NeedId.Energy));
        Assert.True(inBracket.WantsToRunNow(ctx));
        Assert.True(transition.WantsToRunNow(ctx));

        needs.SetSevereExpressed(NeedId.Energy, true);            // already being expressed
        Assert.True(inBracket.WantsToRunNow(ctx));
        Assert.False(transition.WantsToRunNow(ctx));

        // a strategy that only ever reaches a behaviour through the reaction map still says no here
        var shaken = new PlayAnimBehavior("s", "PlayAnim", new[] { AnimationTrigger.Hiccup })
        {
            WantsToRunStrategy = "RobotShaken",
        };
        Assert.False(shaken.WantsToRunNow(ctx));
    }

    [Fact]
    public void APlayAnimBehaviourResolvesItsTriggerAgainstTheShippedAssets()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext
        {
            Robot = robot,
            Triggers = AnimationTriggerMap.Load(obb),
            Random = new Random(2),
        };
        var b = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup });
        Assert.True(b.IsRunnable(ctx));

        using var scope = new BehaviorScope();
        b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.NotNull(b.LastSelected);
        Assert.True(robot.Animations.Library!.HasClip(b.LastSelected!));
        Assert.NotEqual(AnimationTrack.None, scope.LockedTracks);
    }
}
