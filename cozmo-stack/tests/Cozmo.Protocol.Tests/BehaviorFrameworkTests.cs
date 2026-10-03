using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
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

    /// <summary>
    /// Two track locks on one scope: the undos restore a saved mask, so they must run most-recent-first or
    /// the earlier mask is left behind (IBehavior::Stop unlocks every map entry, so Dispose must clear all).
    /// </summary>
    [Fact]
    public void TwoTrackLocksOnOneScopeAreBothReleased()
    {
        var scope = new BehaviorScope();
        scope.LockTracks(AnimationTrack.Head);
        scope.LockTracks(AnimationTrack.Lift);
        Assert.Equal(AnimationTrack.Head | AnimationTrack.Lift, scope.LockedTracks);

        scope.Dispose();
        Assert.Equal(AnimationTrack.None, scope.LockedTracks);
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

    /// <summary>
    /// IBehavior::Stop 0x005bd08c releases the scope in a fixed category order regardless of acquisition
    /// order: (a) disable-reaction locks 0x005bd12c, (b) the idle animation 0x005bd142, (c) the motion
    /// profile 0x005bd150, (d) the track locks 0x005bd15c..0x005bd174. Acquire them in that same order,
    /// so a LIFO release (the old behaviour) would produce the exact reverse.
    /// </summary>
    // fidelity: M8-009
    [Fact]
    public void AScopeReleasesInTheEnginesFixedCategoryOrder()
    {
        var order = new List<string>();
        bool? reactionsStillDisabledAtIdleRemove = null;
        bool? idleStillSetAtMotionClear = null;
        AnimationTrack? tracksAtMotionClear = null;

        var scope = new BehaviorScope();
        scope.DisableReactions();                                    // (a)
        scope.SmartPushIdleAnimation(() => { }, () =>               // (b)
        {
            reactionsStillDisabledAtIdleRemove = scope.ReactionsDisabled;
            order.Add("idle");
        });
        scope.SmartSetMotionProfile(() => { }, () =>                // (c)
        {
            idleStillSetAtMotionClear = scope.IdleAnimationSet;
            tracksAtMotionClear = scope.LockedTracks;
            order.Add("motion");
        });
        scope.LockTracks(AnimationTrack.Head);                       // (d)

        scope.Dispose();

        Assert.Equal(new[] { "idle", "motion" }, order);             // (b) before (c); LIFO would be motion, idle
        Assert.False(reactionsStillDisabledAtIdleRemove);            // (a) before (b)
        Assert.False(idleStillSetAtMotionClear);                     // (b) before (c)
        Assert.Equal(AnimationTrack.Head, tracksAtMotionClear);      // (c) before (d): still held
        Assert.Equal(AnimationTrack.None, scope.LockedTracks);       // (d) released last
    }

    /// <summary>
    /// CalibrateMotorAction inherits a 30.0 s IAction timeout: IAction::IAction 0x00540c44 writes -1.0f
    /// to +0x74 as the "not started" sentinel; IAction::UpdateInternal 0x00540d1c computes
    /// start + vtable[+0x2c], and CalibrateMotorAction's slot +0x2c (vtable vptr 0x0102194c, slot
    /// 0x01021978 -> thunk 0x0052b0c2) returns 0x41f00000 = 30.0f. A calibration that never reports is
    /// still waited on at five seconds and completes at thirty.
    /// </summary>
    // fidelity: M8-008
    [Fact]
    public void TheHeadCalibrationWaitRunsToThirtySeconds()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Random = new Random(7) };
        var probe = new CalibrationProbe();
        using var scope = new BehaviorScope();

        probe.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.False(probe.Done);
        probe.Update(ctx, 0);           // arms the deadline
        probe.Update(ctx, 5_000);       // the old backstop would have completed here
        Assert.False(probe.Done);
        probe.Update(ctx, 30_000);
        Assert.True(probe.Done);
    }

    private sealed class CalibrationProbe : SteppedBehavior
    {
        public CalibrationProbe() : base("calibrationProbe", "CalibrateMotorAction") { }
        public bool Done;
        protected override void OnStart() => CalibrateHead(() => Done = true);
        protected override bool KeepsRunningWithoutAction => true;
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

    /// <summary>The graph a test gives the history: the (0 s, 0.0) to (30 s, 1.0) shape of mood_config.json's defaultRepetitionPenalty, as an explicit input.</summary>
    private static DecayGraph RecoveryGraph() => new("test", new (double, double)[] { (0, 0), (30, 1) });

    /// <summary>
    /// M8-002, 0x005bc55c..0x005bc566: a behaviour whose config has no <c>repetitionPenalty</c> key gets a flat graph,
    /// <c>AddNode(0.0f, 1.0f, true)</c> (<c>movs r1,#0</c>, <c>mov.w r2,#0x3f800000</c>, <c>movs r3,#1</c>). It is not
    /// mood_config.json's <c>defaultRepetitionPenalty</c>, which only the emotion-event fallback reads
    /// (0x0067befe..0x0067bf0a). So a default-constructed history never penalises, however recently the behaviour ran.
    /// </summary>
    [Fact]
    public void AMissingRepetitionPenaltyIsAFlatOne()
    {
        Assert.Equal(0x3f800000, BitConverter.SingleToInt32Bits(RepetitionPenalty.One));
        var p = new RepetitionPenalty();
        Assert.Equal(1.0, p.For("X", 0));
        p.Ran("X", 100);
        Assert.Equal(1.0, p.For("X", 100));
        Assert.Equal(1.0, p.For("X", 100.5));
        Assert.Equal(1.0, p.For("X", 500));
        // the flat graph is the single node (0, 1.0)
        var flat = RepetitionPenalty.FlatGraph();
        Assert.Equal(new[] { (0.0, 1.0) }, flat.Nodes);
    }

    /// <summary>
    /// M8-002, <c>EvaluateRepetitionPenalty</c> 0x005beee6..0x005bef1a: a last-run stamp of zero or less returns 1.0
    /// (<c>vcmpe.f32 s0,#0</c>; <c>itt le</c>; <c>movle.w r0,#0x3f800000</c>, 0x005beeea..0x005beef8), anything else is the
    /// graph at <c>now - stamp</c>. Given the graph (0, 0.0)..(30, 1.0): a stamp of 100 is penalised, a stamp of 0 is not.
    /// </summary>
    [Fact]
    public void AStampOfZeroOrLessIsNotPenalised()
    {
        var p = new RepetitionPenalty(RecoveryGraph());
        Assert.Equal(1.0, p.For("X", 0));                       // never ran: no stamp
        p.Ran("X", 100);
        Assert.Equal(0.0, p.For("X", 100), 3);
        Assert.Equal(0.5, p.For("X", 115), 2);
        Assert.Equal(1.0, p.For("X", 130), 3);
        Assert.Equal(1.0, p.For("X", 500), 3);
        p.Ran("Z", 0);                                          // stamped at 0: <= 0, so 1.0 (0x005beef8)
        Assert.Equal(1.0, p.For("Z", 1));
        p.Ran("N", -4);
        Assert.Equal(1.0, p.For("N", 1));
        p.Ran("Q", double.NaN);                                 // NaN compares "le" in vcmpe/vmrs: 1.0 as well
        Assert.Equal(1.0, p.For("Q", 1));
    }

    [Fact]
    public void ForgettingClearsThePenalty()
    {
        var p = new RepetitionPenalty(RecoveryGraph());
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

    /// <summary>
    /// A manager for the stack's own ranking (<see cref="BehaviorManager.ChooseAndSwitch"/>, M8-004: no engine counterpart). It
    /// applies the graph the history is given, so these tests give it one explicitly; the default is the flat 1.0.
    /// </summary>
    private static BehaviorManager Manager(CozmoRobot robot, string obb, bool withPenaltyGraph = true)
    {
        var ctx = new BehaviorContext
        {
            Robot = robot,
            Triggers = AnimationTriggerMap.Load(obb),
            Random = new Random(5),
        };
        return new BehaviorManager(ctx, withPenaltyGraph ? new RepetitionPenalty(RecoveryGraph()) : null);
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

        Assert.Equal("a", m.ChooseAndSwitch(10).Chosen);
        m.Stop(BehaviorStopReason.Completed, 10);               // IBehavior::Stop stamps +0x30 = 10 (> 0, so penalised)
        Assert.Equal("b", m.ChooseAndSwitch(11).Chosen);        // a: 5 * graph(1 s) = 0.17 < b: 4
        m.Stop(BehaviorStopReason.Completed, 11);
        Assert.Equal("a", m.ChooseAndSwitch(100).Chosen);       // both recovered; a scores 5 against b's 4
    }

    /// <summary>
    /// M8-002, 0x005beeee..0x005beef8: a last-run stamp of zero is not penalised, so a behaviour the manager stopped at time 0 is not
    /// scored down at 1 s. (This replaces a test that called the same sequence "not penalised" while its sibling asserted the
    /// opposite for a stamp above zero.)
    /// </summary>
    [Fact]
    public void ABehaviourStoppedAtTimeZeroIsNotPenalised()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        using var m = Manager(robot, obb);
        m.Add(new Fake("a", 5));
        m.Add(new Fake("b", 4));
        Assert.Equal("a", m.ChooseAndSwitch(0).Chosen);
        m.Stop(BehaviorStopReason.Interrupted, 0);              // stamp 0
        Assert.Equal("a", m.ChooseAndSwitch(1).Chosen);         // 5 * 1.0 beats 4
    }

    /// <summary>
    /// A manager built with no graph applies the engine's flat default (M8-002): a behaviour that just ran loses nothing.
    /// </summary>
    [Fact]
    public void WithoutAGraphTheManagerAppliesNoRepetitionPenalty()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        using var m = Manager(robot, obb, withPenaltyGraph: false);
        m.Add(new Fake("a", 5));
        m.Add(new Fake("b", 4));
        Assert.Equal("a", m.ChooseAndSwitch(10).Chosen);
        m.Stop(BehaviorStopReason.Completed, 10);
        Assert.Equal("a", m.ChooseAndSwitch(11).Chosen);
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
    /// M8-005: the shipped config that lists more than one trigger (NothingToDo_BoredAnim, three) takes
    /// <c>StartSequenceLoop</c> 0x005C0294 - <c>StartPlayingAnimations</c> 0x005C0158 special-cases only a vector of exactly one
    /// trigger (<c>cmp r1,#4</c> 0x005C0168) - and its first child starts when the loop does: the clip of the first trigger is
    /// playing, and nothing locks the clip's tracks (the action's mask is 0, 0x005C02D8).
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
        Assert.Equal(1, bored.NumLoops);                        // the key defaults to Json::Value(1) (0x005C001C)

        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext
        {
            Robot = robot,
            Triggers = AnimationTriggerMap.Load(obb),
            Random = new Random(4),
        };

        using var scope = new BehaviorScope();
        bored.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.Contains(bored.Trace, l => l.StartsWith("play NothingToDoBoredIntro -> "));
        Assert.True(bored.HasCurrentAction);                     // the compound action is +0x84
        Assert.Equal(AnimationTrack.None, scope.LockedTracks);
        Assert.Equal(0, robot.Motion.LockedTracks);              // mask 0: LockTracks inserts nothing (0x005C02D8)
        bored.Stop(BehaviorStopReason.Interrupted);
        Assert.False(bored.HasCurrentAction);
    }

    private static PlayAnimBehavior PlayAnimOf(int loops, params AnimationTrigger[] triggers) =>
        new("pa", "PlayAnim", triggers) { NumLoops = loops };

    /// <summary>
    /// M8-005, the loop path (zero or two-or-more triggers). <c>StartSequenceLoop</c> builds a <c>CompoundActionSequential</c> once per loop
    /// while the loop index is below <c>num_loops</c> (signed <c>bge</c>, 0x005C02A8..0x005C02AE), adding each child with
    /// <c>ignoreFailure = 0</c> (<c>movs r3,#0</c> 0x005C02F6; <c>AddAction</c> 0x0054EC7C installs the ignore function only for 1), so
    /// a failed child ends that sequence (<c>CompoundActionSequential::UpdateInternal</c> 0x0054F70C, 0x0054F81A..0x0054F820), and the
    /// completion then runs the next loop (closure 0x005C0552). With an empty trigger map every trigger resolves to nothing, so
    /// each loop's first child fails: three loops attempt the first trigger three times and never the second or third.
    /// </summary>
    [Fact]
    public void AFailedChildEndsItsSequenceAndTheNextLoopStarts()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Random = new Random(1) };
        var b = PlayAnimOf(3, AnimationTrigger.Hiccup, AnimationTrigger.NothingToDoBoredEvent);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        for (int tick = 0; tick < 12 && b.Update(ctx, tick * 33); tick++) { }
        int first = b.Trace.Count(l => l.StartsWith("Hiccup:"));
        int second = b.Trace.Count(l => l.StartsWith("NothingToDoBoredEvent:"));
        Assert.Equal(3, first);                                  // one failed first child per loop, num_loops = 3
        Assert.Equal(0, second);                                 // ignoreFailure = 0: the second child never starts
        // but every loop built both actions when it started (0x005C02CA..0x005C030E), and each constructor warned about its empty group
        Assert.Equal(3, b.Trace.Count(l => l.EndsWith("the animation group for Hiccup is empty")));
        Assert.Equal(3, b.Trace.Count(l => l.EndsWith("the animation group for NothingToDoBoredEvent is empty")));
        Assert.False(b.Update(ctx, 1000));                       // the behaviour completed
    }

    /// <summary>
    /// M8-005: on the loop path a <c>num_loops</c> of zero or less plays nothing: the signed compare at 0x005C02A8..0x005C02AE is
    /// <c>loop index &gt;= num_loops</c> for index 0. No action is started, so +0x84 stays zero and
    /// <c>IBehavior::UpdateInternal</c> 0x005BDA56 (<c>1</c> while +0x84 is set, else <c>2</c>) completes the behaviour at once.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveLoopCountOnTheLoopPathPlaysNothing(int loops)
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Random = new Random(1) };
        var b = PlayAnimOf(loops, AnimationTrigger.Hiccup, AnimationTrigger.NothingToDoBoredEvent);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Empty(b.Trace);
        Assert.False(b.HasCurrentAction);
        Assert.False(b.Update(ctx, 0));
    }

    /// <summary>
    /// M8-005: exactly one trigger is one <c>TriggerLiftSafeAnimationAction(trigger, numLoops = num_loops, 1, 0, 60.0f, 0)</c>
    /// (0x005C0174..0x005C0190); its timeout is 60.0f = 0x42700000 (<c>movt r1,#0x4270</c> 0x005C017E) from the first update
    /// (<c>IAction::UpdateInternal</c> 0x00540D1C, deadline = start + timeout), so at 60 000 ms of the manager's clock the action ends
    /// with TIMEOUT 0x03000018 and the behaviour completes. With <c>num_loops</c> 0 the action loops until cancelled and the
    /// <c>PlayAnimationAction</c> constructor turns a 60.0f timeout into FLT_MAX (0x00543C96..0x00543CB2), so the same clock never times it out.
    /// </summary>
    [Fact]
    public void ASingleTriggerTimesOutAtSixtySecondsUnlessItLoopsForever()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        Assert.Equal(0x42700000, BitConverter.SingleToInt32Bits(SteppedBehavior.TriggerAnimationTimeoutSec));
        foreach (int loops in new[] { 1, 0 })
        {
            using var robot = CozmoRobot.CreateOffline();
            robot.Transport.OfflineAcceptConnection();
            robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
            var ctx = new BehaviorContext { Robot = robot, Triggers = AnimationTriggerMap.Load(obb), Random = new Random(4) };
            var b = PlayAnimOf(loops, AnimationTrigger.Hiccup);
            b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
            Assert.Contains(b.Trace, l => l.StartsWith("play Hiccup -> "));
            Assert.True(b.Update(ctx, 0));                                  // arms the deadline at 0 + 60 000
            Assert.True(b.Update(ctx, 59_999));
            Assert.DoesNotContain(b.Trace, l => l == "action timed out");
            b.Update(ctx, 60_000);
            if (loops == 1)
            {
                Assert.Contains(b.Trace, l => l == "action timed out");      // 0x03000018 at start + 60.0
                Assert.False(b.Update(ctx, 60_001));                          // the failure ran the completion; nothing is left
            }
            else
            {
                Assert.DoesNotContain(b.Trace, l => l == "action timed out");   // FLT_MAX: never
                Assert.True(b.Update(ctx, 3_600_000));
                b.Stop(BehaviorStopReason.Cancelled);
            }
        }
    }

    /// <summary>
    /// M8-005 / M8-007: the engine never resumes a PlayAnim. <c>BehaviorPlayAnimSequence::ResumeInternal</c> (<c>vtable+0x4c</c>) 0x005BFF1E is
    /// <c>movs r0,#1; bx lr</c>, and <c>TryToResumeBehavior</c> treats a non-zero <c>IBehavior::Resume</c> as failure (0x005A2C76..0x005A2C7A).
    /// </summary>
    [Fact]
    public void APlayAnimIsNeverResumed()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Random = new Random(1) };
        var b = PlayAnimOf(1, AnimationTrigger.Hiccup);
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        b.Stop(BehaviorStopReason.Interrupted);
        b.AttachRun(ctx, new BehaviorScope());
        Assert.True(b.Resume(ReactionTrigger.RobotOnBack, 10));             // a non-zero result is a failed resume
        Assert.False(b.EngineRunning);                                       // +0xa1 = 0 (0x005BCFA2)
    }

    /// <summary>
    /// M8-005: <c>BehaviorPlayAnimSequence::IsRunnableInternal</c> 0x005C013A is false for an empty trigger vector, and the
    /// <c>vtable+0x20/+0x24/+0x28</c> gates are 0, 0 and 1 (relocations 0x01026940, 0x01026944, 0x01026948).
    /// </summary>
    [Fact]
    public void APlayAnimWithNoTriggersIsNotRunnable()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext { Robot = robot, Triggers = AnimationTriggerMap.Load(obb) };
        var empty = PlayAnimOf(1);
        Assert.False(empty.IsRunnable(ctx));
        Assert.True(PlayAnimOf(1, AnimationTrigger.Hiccup).IsRunnable(ctx));
        // robot+0x355 set: IsRunnableBase calls vtable+0x20, which is 0 for PlayAnim, so it is not runnable
        var one = PlayAnimOf(1, AnimationTrigger.Hiccup);
        one.RobotState355 = () => true;
        Assert.False(one.IsRunnableBase(ctx));
        one.RobotState355 = null;
        one.RobotComponent284 = () => true;                                   // carrying: vtable+0x28 is 1 for PlayAnim
        Assert.True(one.IsRunnableBase(ctx));
    }

    /// <summary>
    /// M8-007 / M8-005, <c>TriggerLiftSafeAnimationAction</c> 0x00544710: the action's mask is <c>tracksToLock</c> = 0, so a PlayAnim sends no
    /// DisableAnimTracks for the tracks its clip uses (<c>LockTracks</c> with a zero mask inserts nothing and sends nothing, 0x00640098);
    /// but the constructor ORs LIFT (2) into the mask while the robot is carrying and <c>robot+0x355</c> is 0 (0x00544714..0x00544732), so
    /// then <c>LockTracks</c> sends <c>DisableAnimTracks{2}</c> (0x0064017C..0x00640188) and the end of the action sends
    /// <c>EnableAnimTracks{2}</c> (0x0063FFA8..0x0063FFB4).
    /// </summary>
    [Fact]
    public void APlayAnimLocksNothingExceptTheLiftWhileCarrying()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = AnimationTriggerMap.Load(obb), Random = new Random(4) };
        rig.Pump(); rig.Sent.Clear();

        var plain = PlayAnimOf(1, AnimationTrigger.Hiccup);
        plain.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Contains(plain.Trace, l => l.StartsWith("play Hiccup -> "));
        var clip = rig.Robot.Animations.Library!.GetClip(plain.LastSelected!);
        Assert.NotEqual(0, CozmoMotion.MaskFor(clip.Tracks));                // the clip does use motion tracks...
        rig.Pump();
        Assert.Equal(0, rig.Robot.Motion.LockedTracks);                       // ...and none is locked
        Assert.Empty(rig.Sent.OfType<DisableAnimTracks>());
        plain.Stop(BehaviorStopReason.Cancelled);
        rig.Pump();
        Assert.Empty(rig.Sent.OfType<EnableAnimTracks>());

        rig.Robot.Motion.IsCarryingObject = () => true;                       // carrying; the off-treads state is OnTreads (0)
        var carrying = PlayAnimOf(1, AnimationTrigger.Hiccup);
        carrying.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        rig.Pump();
        Assert.Equal(new byte[] { 2 }, rig.Sent.OfType<DisableAnimTracks>().Select(m => m.Field0));
        Assert.Equal(CozmoMotion.LiftTrack, rig.Robot.Motion.LockedTracks);
        carrying.Stop(BehaviorStopReason.Cancelled);
        rig.Pump();
        Assert.Equal(new byte[] { 2 }, rig.Sent.OfType<EnableAnimTracks>().Select(m => m.Field0));
        Assert.Equal(0, rig.Robot.Motion.LockedTracks);
    }

    /// <summary>
    /// M8-006: the two wants-to-run strategies that read the needs, as the engine evaluates them.
    /// StrategyInNeedsBracket::WantsToRunInternal 0x006141A0 is a single call to NeedsState::IsNeedAtBracket(need, bracket);
    /// StrategyExpressNeedsTransition::WantsToRunInternal 0x006136D8 asks IsNeedAtBracket(need, Critical) - the literal 3 at
    /// 0x006136EC - and then is true only if the one value [[robot+0x264]+0x30]+0x14 differs from its need (0x006136FC..0x00613704).
    /// That value has no source in this stack (the name of the object and of its +0x14 is UNKNOWN in the inventory), so it is a seam,
    /// <see cref="BehaviorContext.AiExpressedNeedValue"/>, and without it the strategy refuses rather than answers.
    /// </summary>
    [Fact]
    public void TheNeedsWantsToRunStrategiesFollowTheNeedsState()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        double now = 0;
        var needs = new NeedsManager(() => now);
        NeedId? expressed = null;
        var ctx = new BehaviorContext
        {
            Robot = robot,
            Triggers = new AnimationTriggerMap(),
            Needs = needs,
            Random = new Random(1),
            AiExpressedNeedValue = () => expressed,
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
        Assert.True(transition.WantsToRunNow(ctx));               // the expressed value (null here) is not Energy

        expressed = NeedId.Energy;                                // the one value equals the strategy's need: cmp r2,r1; movne
        Assert.True(inBracket.WantsToRunNow(ctx));
        Assert.False(transition.WantsToRunNow(ctx));
        expressed = NeedId.Play;                                  // another need being expressed does not stop it
        Assert.True(transition.WantsToRunNow(ctx));

        // the per-need set of the old stack is not the engine's value and no longer decides anything
        needs.SetSevereExpressed(NeedId.Energy, true);
        Assert.True(transition.WantsToRunNow(ctx));

        // with nothing to supply that value it is the SevereNeedsComponent's constructed 3 (none, 0x00572A0E), reported once: not the strategy's need, so true
        // once the Critical test has passed (it reads the value only then); the old throw was the stack refusing, not an engine path
        var unwired = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Needs = needs };
        Assert.True(transition.WantsToRunNow(unwired));
        needs.SetLevel(NeedId.Energy, 1.0);
        Assert.False(transition.WantsToRunNow(unwired));          // not Critical: false before the value is read

        // the other strategy types have no body in the inventory: no silent answer
        foreach (var type in new[] { "RobotShaken", "PlacedOnCharger", "RobotPlacedOnSlope", "Generic", "AlwaysRun" })
        {
            var other = new PlayAnimBehavior("s", "PlayAnim", new[] { AnimationTrigger.Hiccup }) { WantsToRunStrategy = type };
            Assert.Throws<NotSupportedException>(() => other.WantsToRunNow(ctx));
        }
        // no strategy config at all: no strategy
        Assert.True(new PlayAnimBehavior("n", "PlayAnim", new[] { AnimationTrigger.Hiccup }).WantsToRunNow(ctx));
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
        // the action's mask is tracksToLock = 0 (M8-005/M8-007): the clip's tracks are not locked by anyone
        byte mask = CozmoMotion.MaskFor(robot.Animations.Library!.GetClip(b.LastSelected!).Tracks);
        Assert.True(mask != 0 && !robot.Motion.AreAnyTracksLocked(mask));
        b.Stop(BehaviorStopReason.Cancelled);
    }

// ------------------------------------------------------------------ M8-001: the Init action-tag guard

    /// <summary>
    /// <c>IBehavior::Init</c> 0x005bcb54 compares <c>action+0x60</c> against the integer 3,000,000
    /// (<c>movw r6,#0xc6c0</c> 0x005bcbd0 / <c>movt r6,#0x2d</c> 0x005bcbda) and flags only tags above it,
    /// so externally-tagged game actions (1..2,000,000) are excluded. The warning's count is the main
    /// queue's current action plus its queued actions (0x005bcd64..0x005bcd6c).
    /// </summary>
    [Fact]
    public void TheInitTagGuardFlagsOnlyEngineTaggedActionsAndCountsTheMainQueue()
    {
        Assert.Equal(3_000_000, BehaviorInit.ActionTagSentinel);
        Assert.False(BehaviorInit.HasEngineTaggedAction(new[] { 1, 2_000_000, BehaviorInit.ActionTagSentinel }));
        Assert.True(BehaviorInit.HasEngineTaggedAction(new[] { 0x2dc6c1 }));      // the counter's first value
        Assert.True(BehaviorInit.HasEngineTaggedAction(new[] { 2_000_000, 3_000_001 }));
        Assert.Equal(0, BehaviorInit.MainQueueActionCount(false, 0));
        Assert.Equal(2, BehaviorInit.MainQueueActionCount(false, 2));
        Assert.Equal(3, BehaviorInit.MainQueueActionCount(true, 2));
    }

    /// <summary>
    /// M8-001, <c>IBehavior::Init</c> 0x005bcb54. The slots (the vptr points 8 bytes into the vtable; slot = (relocation - start - 8) / 4):
    /// vptr+0x48 is <c>InitInternal</c> (relocation 0x01026968 in <c>BehaviorPlayAnimSequence</c>'s vtable), vptr+0x50 is
    /// <c>IsRunnableInternal</c>. Init calls vptr+0x48 (<c>ldr r2,[r2,#0x48]; blx r2</c> 0x005bccfe/0x005bcd00): a non-zero result is Init's
    /// failure and clears the running flag +0xa1 (<c>cbz r5</c> 0x005bcd04; <c>strb.w r6,[r4,#0xa1]</c> 0x005bcd06); a zero result increments +0x80
    /// (0x005bcd0c) and leaves +0xa1 = 1 (the halfword 0x0100 at +0xa0, 0x005bccca). <c>IsRunnableInternal</c> has nothing to do with it.
    /// </summary>
    [Fact]
    public void InitCallsInitInternalAndItsResultIsInitsFailure()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 100 };

        // vptr+0x50 says no, vptr+0x48 succeeds: Init succeeds, and the behaviour is running
        var ok = new LifecycleProbe { InternalRunnable = false };
        ok.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.True(ok.EngineRunning);
        Assert.False(ok.InitFailed);
        Assert.Equal(1, ok.ScoreIncreaseCount);

        // vptr+0x50 says yes, vptr+0x48 fails: Init fails and +0xa1 is cleared
        var failing = new LifecycleProbe { InternalRunnable = true, FailInit = true };
        failing.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.False(failing.EngineRunning);
        Assert.True(failing.InitFailed);
        Assert.Equal(0, failing.ScoreIncreaseCount);
        Assert.Equal(100.0, failing.RunningPenaltyClockSec);                  // +0x34 = now was stamped before the call
    }

    /// <summary>
    /// M8-001, <c>IBehavior::Resume</c> 0x005bceac. A resume with trigger 0 (CliffDetected) or 0x14 (UnexpectedMovement) counts against +0x114,
    /// the store coming before the test (0x005bcf1e..0x005bcf2c). The first takes the normal path (<c>ResumeInternal</c>, vptr+0x4c). From
    /// the second on (old value &gt;= 1) the resume is refused: <c>+0x118 = now + 15.0f</c> (<c>vmov.f32 s0,#15.0</c> 0x005bcf36 = 0x41700000;
    /// <c>vstr</c> 0x005bcf4c), <c>MoodManager::TriggerEmotionEvent(TooManyResumesCliffOrMovement)</c> (0x005bcf68; Confident -1.0 in
    /// <c>emotionevents/reaction_events.json</c>, lines 22-26), and <c>IsRunnableBase</c>'s last test (<c>now &gt;= +0x118</c>, 0x005bd8be..0x005bd8c8)
    /// refuses the next resume through <c>ResumeInternal</c>'s own <c>IsRunnableBase</c> (0x005bda9c).
    /// </summary>
    [Fact]
    public void ResumeCountsCliffAndMovementAndRefusesFromTheSecondWithTheEmotionEvent()
    {
        var obb = ObbRoot();
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        double clock = 100;
        MoodState? mood = null;
        if (obb is not null)
        {
            var model = MoodModel.Load(obb);
            // the shipped asset: reaction_events.json line 22, emotionType Confident, value -1.0 (lines 25-26)
            var e = model.Event("TooManyResumesCliffOrMovement");
            Assert.NotNull(e);
            Assert.Equal(new[] { new EmotionAffector(EmotionType.Confident, -1.0) }, e!.Affectors);
            mood = new MoodState(model);
        }
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), ClockSec = () => clock, Mood = mood };
        Assert.Equal(0x41700000, BitConverter.SingleToInt32Bits(SteppedBehavior.TooManyResumesCooldownSec));

        var probe = new LifecycleProbe();
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        probe.Stop(BehaviorStopReason.Interrupted);                           // the manager stops it before it resumes it (0x005a2c5c)
        probe.AttachRun(ctx, new BehaviorScope());

        // first CliffDetected: the normal path through ResumeInternal (IsRunnableBase, the vptr+0x50 test, InitInternal)
        Assert.False(probe.Resume(ReactionTrigger.CliffDetected, 100));
        Assert.Equal(1, probe.ResumeCount);
        Assert.True(probe.EngineRunning);
        Assert.True(probe.IsRunnableScored(100));
        if (mood is not null) Assert.Equal(0.0, mood[EmotionType.Confident]);

        // second: refused, +0x118 = 200 + 15, the emotion event
        clock = 200;
        Assert.True(probe.Resume(ReactionTrigger.CliffDetected, 200));
        Assert.Equal(2, probe.ResumeCount);
        Assert.Equal(215.0, probe.ResumeSuppressionUntilSec);
        Assert.False(probe.IsRunnableScored(214));
        Assert.True(probe.IsRunnableScored(215));
        if (mood is not null) Assert.Equal(-1.0, mood[EmotionType.Confident]);

        // every later cliff/movement resume is refused too (the old value stays >= 1)
        Assert.True(probe.Resume(ReactionTrigger.UnexpectedMovement, 201));
        Assert.Equal(3, probe.ResumeCount);

        // a trigger that is not counted takes the normal path, and the +0x118 test inside ResumeInternal refuses it until 15 s have passed
        probe.Stop(BehaviorStopReason.Interrupted);
        clock = 214;
        Assert.True(probe.Resume(ReactionTrigger.RobotShaken, 214));
        Assert.Equal(3, probe.ResumeCount);
        Assert.False(probe.EngineRunning);
        // +0x118 was last set at 201 + 15 = 216
        clock = 216;
        Assert.False(probe.Resume(ReactionTrigger.RobotShaken, 216));
        Assert.True(probe.EngineRunning);
    }

    /// <summary>
    /// M8-001: <c>IncreaseScoreWhileActing</c> 0x005bf02c adds to +0x104 only while the current-action handle +0x84 is non-zero
    /// (<c>cbz r2,#0x5bf042</c> 0x005bf030). <c>ScoredActingStateChanged(bool)</c> 0x005bf044 - vptr+0x80, relocation 0x01026568 - ignores its
    /// argument and clears +0x104; <c>StartActing</c> calls it with true (0x005bdb54..0x005bdb5c) and <c>StopActing</c> with false
    /// (0x005bd358..0x005bd360), so every action start and every action stop clears the bonus.
    /// </summary>
    [Fact]
    public void ScoredActingStateChangedClearsTheBonusOnActionStartStopAndCompletion()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 1 };
        var probe = new LifecycleProbe();
        probe.IncreaseScoreWhileActing(5.0);
        Assert.Equal(0.0, probe.RunningScoreBonus);              // not acting: +0x84 is zero
        Assert.Equal(0, probe.StartActing());                     // not running: IBehavior.StartActing.Failure.NotRunning (0x005bdae2..0x005bdaee)
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();

        int h = probe.StartActing();                              // +0x84 set
        Assert.NotEqual(0, h);
        Assert.True(probe.HasCurrentAction);
        Assert.Equal(0, probe.StartActing());                     // IBehavior.StartActing.Failure.AlreadyActing (0x005bdaf0..0x005bdb3e)
        probe.IncreaseScoreWhileActing(5.0);
        Assert.Equal(5.0, probe.RunningScoreBonus);
        probe.IncreaseScoreWhileActing(10.0);
        Assert.Equal(15.0, probe.RunningScoreBonus);

        probe.ActingEnded(h);                                     // HandleActionComplete 0x005be1e6: +0x84 = 0, then vtable+0x80(0)
        Assert.False(probe.HasCurrentAction);
        Assert.Equal(0.0, probe.RunningScoreBonus);

        int h2 = probe.StartActing();                             // the next action clears it (the slot is called with true)
        Assert.NotEqual(0, h2);
        probe.IncreaseScoreWhileActing(5.0);
        probe.DoStopActing(keepAction: false, viaCallback: true); // StopActing clears it (the slot is called with false)
        Assert.Equal(0.0, probe.RunningScoreBonus);
        Assert.False(probe.HasCurrentAction);

        probe.StartActing();
        probe.IncreaseScoreWhileActing(5.0);
        probe.ScoredActingStateChanged(true);                     // the argument is ignored
        Assert.Equal(0.0, probe.RunningScoreBonus);

        // IBehavior::Stop calls StopActing(0,0) (0x005bd126), so a stop clears it as well
        probe.StartActing();
        probe.IncreaseScoreWhileActing(5.0);
        probe.Stop(BehaviorStopReason.Interrupted);
        Assert.Equal(0.0, probe.RunningScoreBonus);

        // while the behaviour is stopping (+0xa0) StartActing refuses (0x005bdad4..0x005bdadc)
        var stopping = new LifecycleProbe();
        stopping.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        stopping.StopOnNextActionComplete();
        Assert.Equal(0, stopping.StartActing());
    }

    /// <summary>
    /// M8-001: <c>StartActing</c> stores the completion callback at +0x98 and <c>StopOnNextActionComplete</c> (0x005bd694..0x005bd6b4) destroys it, so the
    /// action ends and clears +0x84 without calling back; <c>HandleActionComplete</c> also calls back only while +0xa1 is set (0x005be20a..0x005be21c).
    /// </summary>
    [Fact]
    public void StopOnNextActionCompleteDiscardsTheCompletionCallback()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 1 };
        int called = 0;
        var kept = new LifecycleProbe();
        kept.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        kept.Play(AnimationTrigger.Hiccup, () => called++);       // fails at once (no map): the completion is queued
        kept.Update(ctx, 0);
        Assert.Equal(1, called);

        var discarded = new LifecycleProbe();
        discarded.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        discarded.Play(AnimationTrigger.Hiccup, () => called++);
        discarded.StopOnNextActionComplete();
        discarded.Update(ctx, 0);
        Assert.Equal(1, called);                                   // destroyed: no call

        var stopped = new LifecycleProbe();
        stopped.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        stopped.Play(AnimationTrigger.Hiccup, () => called++);
        stopped.Stop(BehaviorStopReason.Interrupted);              // +0xa1 clear
        stopped.Update(ctx, 0);
        Assert.Equal(1, called);
    }

    /// <summary>
    /// M8-001: <c>IBehavior::StopOnNextActionComplete</c> 0x005bd624 sets +0xa0 = 1
    /// (<c>strb.w r1,[r4,#0xa0]</c> 0x005bd698), so <c>IBehavior::Update</c> 0x005bd074 returns 2 - before it calls <c>UpdateInternal</c>
    /// (vptr+0xc, <c>ldr r2,[r2,#0xc]; bx r2</c> 0x005bd080..0x005bd086) - once the current-action handle +0x84 is clear
    /// (<c>ldrb.w r1,[r0,#0xa0]; cbz</c>; <c>ldr.w r1,[r0,#0x84]; cbz</c>, 0x005bd074..0x005bd07e; <c>movs r0,#2</c> 0x005bd088). While an
    /// action is running (+0x84 set) <c>UpdateInternal</c> still runs. Its callers are the M7/M15 concrete behaviours.
    /// </summary>
    [Fact]
    public void StopOnNextActionCompleteMakesUpdateReturnTwoBeforeUpdateInternal()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 1 };
        var probe = new LifecycleProbe();
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Equal(0, probe.UpdateStatus());
        Assert.True(probe.Update(ctx, 0));                       // +0xa0 clear: UpdateInternal runs and keeps it
        Assert.Equal(1, probe.Updates);

        probe.StartActing();                                      // +0x84 set
        probe.StopOnNextActionComplete();
        Assert.Equal(0, probe.UpdateStatus());                    // +0xa0 set but +0x84 non-zero: not 2
        Assert.True(probe.Update(ctx, 33));
        Assert.Equal(2, probe.Updates);

        probe.DoStopActing(keepAction: false, viaCallback: true); // +0x84 cleared
        Assert.Equal(2, probe.UpdateStatus());
        Assert.False(probe.Update(ctx, 66));                      // returns 2 (finish) ...
        Assert.Equal(2, probe.Updates);                           // ... without calling UpdateInternal
    }

    /// <summary>
    /// <c>StopOnNextActionComplete</c> 0x005bd624 also releases the +0x98 handle
    /// (<c>ldr.w r0,[r4,#0x98]</c> 0x005bd694; <c>str.w r0,[r4,#0x98]</c> 0x005bd6b4).
    /// </summary>
    [Fact]
    public void StopOnNextActionCompleteReleasesTheSharedHandle()
    {
        var probe = new LifecycleProbe();
        probe.SetHandle(new object());
        Assert.NotNull(probe.SharedHandle);
        probe.StopOnNextActionComplete();
        Assert.Null(probe.SharedHandle);
    }

    /// <summary>
    /// M8-001: <c>IBehavior::StopActing(keepAction, viaCallback)</c> 0x005bd34c: <c>viaCallback</c> non-zero skips
    /// the <c>StopHelperWithoutCallback</c> path (<c>cbnz r6,#0x5bd3da</c> 0x005bd362). <c>keepAction</c> only decides whether +0x84 is
    /// cleared before <c>ActionList::Cancel</c> (<c>cbnz r5,#0x5bd3e8</c> 0x005bd3e0; <c>str.w r0,[r4,#0x84]</c> 0x005bd3e4); after the cancel it
    /// is cleared whenever it still holds the handle that was cancelled (<c>ldr.w r1,[r4,#0x84]; cmp r1,r6; itt eq; streq</c> 0x005bd3f4..0x005bd3fe),
    /// so +0x84 ends clear either way.
    /// </summary>
    [Fact]
    public void StopActingHonoursItsViaCallbackAndKeepActionParameters()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 1 };
        var probe = new LifecycleProbe();
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        probe.DoStopActing(keepAction: false, viaCallback: true);
        Assert.Equal(0, probe.HelperStops);
        probe.DoStopActing(keepAction: false, viaCallback: false);
        Assert.Equal(1, probe.HelperStops);

        Assert.NotEqual(0, probe.StartActing());
        probe.DoStopActing(keepAction: true, viaCallback: true);
        Assert.False(probe.HasCurrentAction);                    // cleared after the cancel although keepAction was set
        Assert.NotEqual(0, probe.StartActing());
        probe.DoStopActing(keepAction: false, viaCallback: true);
        Assert.False(probe.HasCurrentAction);
    }

    /// <summary>
    /// <c>IsRunnableBase</c> 0x005bd864: robot+0x355 set requires the <c>vtable+0x20</c> gate
    /// (<c>ldrb.w r0,[r5,#0x355]</c>; <c>blx r1</c>). The robot bytes are cross-layer seams.
    /// </summary>
    [Fact]
    public void IsRunnableBaseConsumesTheRobotStateAndBehaviourGates()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 0 };
        var probe = new LifecycleProbe { Gate20 = false };
        Assert.True(probe.IsRunnableBase(ctx));                 // the robot byte is not modelled
        probe.RobotState355 = () => true;
        Assert.False(probe.IsRunnableBase(ctx));                // +0x355 set and the vtable+0x20 gate fails
    }

    /// <summary>
    /// <c>IBehavior::HandleBehaviorObjective</c> 0x005bf00c: when +0x10c is not 0x29 and the achieved
    /// objective equals it, stamp the <b>last-run</b> clock <c>+0x30 = now</c> (<c>str r0,[r4,#0x30]</c>
    /// 0x005bf028) - the stamp <c>EvaluateRepetitionPenalty</c> 0x005beee6 reads. Asserted through the
    /// shared history, not by reading back the field the code wrote.
    /// </summary>
    [Fact]
    public void HandleBehaviorObjectiveStampsTheLastRunClockOnAMatch()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var penalty = new RepetitionPenalty(RecoveryGraph());
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 42, Penalty = penalty };
        var probe = new LifecycleProbe { BehaviorObjective = BehaviorObjective.PoppedWheelie };
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();

        Assert.False(probe.HandleBehaviorObjective(BehaviorObjective.PerformedWorkout, 10));
        Assert.Null(penalty.LastRunSec("probe"));
        Assert.True(probe.HandleBehaviorObjective(BehaviorObjective.PoppedWheelie, 42));
        Assert.Equal(42.0, penalty.LastRunSec("probe"));
        Assert.Equal(0.0, penalty.For("probe", 42), 3);          // the graph at 0 s after the stamp

        var invalid = new LifecycleProbe();
        Assert.False(invalid.HandleBehaviorObjective(BehaviorObjective.Invalid, 5));
    }

    /// <summary>
    /// The <c>BehaviorObjectiveFromString</c> 0x005bc5a0 mapping for the two shipped names, and the ordinals
    /// from the <c>EnumToString(BehaviorObjective)</c> 0x769540 table at 0x1033140: PerformedWorkout 18,
    /// PoppedWheelie 22, the 0x29 sentinel (table[41] "Count").
    /// </summary>
    [Fact]
    public void TheShippedBehaviorObjectivesParse()
    {
        Assert.Equal(BehaviorObjective.PerformedWorkout, BehaviorObjectives.FromString("PerformedWorkout"));
        Assert.Equal(BehaviorObjective.PoppedWheelie, BehaviorObjectives.FromString("PoppedWheelie"));
        Assert.Equal(BehaviorObjective.Invalid, BehaviorObjectives.FromString("NotAnObjective"));
        Assert.Equal(18, (int)BehaviorObjective.PerformedWorkout);
        Assert.Equal(22, (int)BehaviorObjective.PoppedWheelie);
        Assert.Equal(0x29, (int)BehaviorObjective.Invalid);
    }

    /// <summary>
    /// M8-001/M8-012: the manager's resume path calls <c>IBehavior::Resume</c> (0x005a2c76) and must not run <c>Init</c>, which clears +0x114. So a second
    /// CliffDetected resume through <c>BehaviorManager.Update</c> is refused (0x005bcf4c) and the manager switches to the empty info
    /// (0x005a2c7c..0x005a2cf4): nothing running, no trigger, nothing parked.
    /// </summary>
    [Fact]
    public void TheManagerResumePathPreservesTheResumeCounter()
    {
        var obb = ObbRoot();
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Random = new Random(1) };
        if (obb is not null) ctx.Mood = new MoodState(MoodModel.Load(obb));
        using var m = new BehaviorManager(ctx);
        var parked = new LifecycleProbe();
        m.Add(parked);
        var strategy = new AlwaysTriggerStrategy();
        m.AddReaction(strategy, new OneShotBehavior("react"));

        Assert.True(m.StartAsync("probe", 0).GetAwaiter().GetResult());
        Assert.Equal("probe", m.Current!.Id);

        // the reaction fires and parks the probe; the reaction then finishes and the probe resumes
        Assert.NotNull(m.CheckReactions(1));
        Assert.Equal("react", m.Current!.Id);
        Assert.Same(parked, m.ParkedBehavior);
        strategy.Fire = false;
        m.Update(0, 2);
        Assert.Equal("probe", m.Current!.Id);
        Assert.Equal(1, parked.ResumeCount);
        Assert.Null(m.ParkedBehavior);
        Assert.Null(m.CurrentReactionTrigger);                    // the resumed info is {probe, none, NoneTrigger} (0x005a2d10..0x005a2d94)

        // again: the counter survives the resume, so the second is refused
        strategy.Fire = true;
        Assert.NotNull(m.CheckReactions(3));
        Assert.Equal("react", m.Current!.Id);
        strategy.Fire = false;
        m.Update(0, 4);
        Assert.Null(m.Current);                                    // Resume failed: the empty switch
        Assert.Null(m.ParkedBehavior);
        Assert.Null(m.CurrentReactionTrigger);
        Assert.Equal(2, parked.ResumeCount);
        Assert.False(parked.IsRunnableScored(4));                  // +0x118 = 4 + 15
        Assert.True(parked.IsRunnableScored(19));
        if (ctx.Mood is { } mood) Assert.Equal(-1.0, mood[EmotionType.Confident]);    // TooManyResumesCliffOrMovement
    }

    /// <summary>
    /// The manager's stop must <b>not</b> write the +0x108 window: <c>StopWithoutImmediateRepetitionPenalty</c>
    /// is called only by the M7/M15 concrete behaviours, while <c>IBehavior::Stop</c> just stamps +0x30
    /// (0x005bd11a/0x005bd11e). So a manager-interrupted behaviour is penalised on the next score.
    /// </summary>
    [Fact]
    public void TheManagerDoesNotSuppressThePenaltyOnAnInterruptedStop()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        using var m = Manager(robot, obb);
        m.Add(new Fake("a", 5));
        m.Add(new Fake("b", 4));

        Assert.Equal("a", m.ChooseAndSwitch(10).Chosen);
        m.Stop(BehaviorStopReason.Interrupted, 10);
        Assert.Equal("b", m.ChooseAndSwitch(11).Chosen);   // a's repetition penalty applies at 1 s
    }

    /// <summary>
    /// M8-002: the behaviour-side <c>StopWithoutImmediateRepetitionPenalty</c> (0x005beea0) calls <c>IBehavior::Stop()</c> first (<c>blx</c> 0x005beea4):
    /// the running flag is cleared, <c>StopInternal</c> runs, the +0x30 last-run clock is stamped, <c>StopActing</c> runs and the scope is undone.
    /// Then +0x108 = now + 1.0f on the shared history (<c>vmov.f32 s0,#1.0</c> 0x005beeb0; <c>vadd.f32</c>; <c>vstr</c> 0x005beebc), which the scored
    /// chooser honours.
    /// </summary>
    [Fact]
    public void TheBehaviourApiStopsFirstAndThenWritesTheSuppressionWindow()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var penalty = new RepetitionPenalty(RecoveryGraph());
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 50, Penalty = penalty };
        var probe = new LifecycleProbe();
        bool scopeUndone = false;
        var scope = new BehaviorScope();
        scope.OnRelease(() => scopeUndone = true);
        probe.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.True(probe.EngineRunning);
        probe.StartActing();

        Assert.False(penalty.IsSuppressed("probe", 50));
        probe.StopWithoutImmediateRepetitionPenalty();
        Assert.False(probe.EngineRunning);                        // Stop ran
        Assert.Equal(1, probe.Stops);                             // StopInternal (vptr+0x54)
        Assert.False(probe.HasCurrentAction);                     // StopActing
        Assert.True(scopeUndone);                                 // the scope's undo
        Assert.Equal(50.0, penalty.LastRunSec("probe"));          // +0x30 = now
        Assert.True(penalty.IsSuppressed("probe", 50.5));         // +0x108 = 50 + 1.0f
        Assert.False(penalty.IsSuppressed("probe", 51));
    }

    /// <summary>
    /// <c>BehaviorManager::SwitchToBehaviorBase</c> 0x005a1e6a stops the current behaviour, gates on
    /// <c>IsRunnable</c> and starts the target; <c>FinishCurrentBehavior</c> 0x005a38ca finishes it (the
    /// immediate branch).
    /// </summary>
    [Fact]
    public void SwitchToBehaviorBaseAndFinishFollowTheEngine()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Random = new Random(1) };
        using var m = new BehaviorManager(ctx);
        m.Add(new OneShotBehavior("a"));
        m.Add(new OneShotBehavior("b"));

        Assert.True(m.SwitchToBehaviorBase(m.Find("a")!, 0).GetAwaiter().GetResult());
        Assert.Equal("a", m.Current!.Id);
        Assert.True(m.SwitchToBehaviorBase(m.Find("b")!, 1).GetAwaiter().GetResult());
        Assert.Equal("b", m.Current!.Id);
        m.FinishCurrentBehavior(m.Find("b")!, tryToResume: true, 2);   // nothing parked: the empty switch
        Assert.Null(m.Current);
    }

    /// <summary>
    /// <c>BehaviorManager::SwitchToBehaviorBase</c> 0x005a1e6a: a zero <c>IBehavior::IsRunnable</c> logs
    /// "BehaviorManager.SwitchToBehaviorBase.BehaviorNotRunnable" (0x005a1e88) and then falls through to
    /// <c>IBehavior::Init</c> (0x005a1e94) - it does not refuse.
    /// </summary>
    [Fact]
    public void SwitchToBehaviorBaseStillInitialisesWhenNotRunnable()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Random = new Random(1) };
        using var m = new BehaviorManager(ctx);
        var notRunnable = new OneShotBehavior("x") { Runnable = false };
        m.Add(notRunnable);
        var log = new List<string>();
        m.Log += log.Add;

        Assert.True(m.SwitchToBehaviorBase(notRunnable, 0).GetAwaiter().GetResult());
        Assert.Equal("x", m.Current!.Id);
        Assert.Contains(log, l => l.Contains("BehaviorNotRunnable"));
    }

    /// <summary>
    /// <c>IBehavior::ReadFromScoredJson</c> 0x005bc488 reads <c>considerThisHasRunForBehaviorObjective</c>
    /// from the behaviour's own config (<c>behaviors/freeplay/popAWheelie.json</c>,
    /// <c>cubeLiftWorkout.json</c>), not an activity chooser entry.
    /// </summary>
    [Fact]
    public void TheShippedBehaviorConfigsCarryTheirObjectives()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var objectives = BehaviorObjectives.Load(obb);
        Assert.Equal(BehaviorObjective.PerformedWorkout, objectives["CubeLiftWorkout"]);
        Assert.Equal(BehaviorObjective.PoppedWheelie, objectives["PopAWheelie"]);
    }

    /// <summary>
    /// <c>IActionRunner::Update</c> 0x00540370: <c>MovementComponent::AreAnyTracksLocked(mask)</c> 0x00540440;
    /// a play whose required tracks are held is refused (0x005404a8): the action ends with 0x03000019 and is not retried
    /// (see <see cref="ALockedTrackFailsTheActionAndIsNotRetried"/>). <c>CozmoAnimations.PlayTracked</c> is the play-and-lock step.
    /// <c>MovementComponent::LockTracks</c> 0x00640098 / <c>UnlockTracks</c> 0x0063fe5c.
    /// </summary>
    [Fact]
    public void APlayIsRefusedWhileItsTracksAreLocked()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var lib = robot.Animations.Library!;
        var clip = lib.ClipNames.Select(lib.GetClip).First(c => CozmoMotion.MaskFor(c.Tracks) != 0);
        byte mask = CozmoMotion.MaskFor(clip.Tracks);

        using var scope = new BehaviorScope(motion: robot.Motion);
        scope.LockTracks(clip.Tracks);
        Assert.True(robot.Motion.AreAnyTracksLocked(mask));
        Assert.Null(robot.Animations.PlayTracked(clip.Name, lockTracks: clip.Tracks));   // refused (0x03000019); the caller does not retry
        scope.Dispose();
        Assert.False(robot.Motion.AreAnyTracksLocked(mask));
        Assert.NotNull(robot.Animations.PlayTracked(clip.Name, lockTracks: clip.Tracks)); // now it runs
        robot.Animations.Stop();
    }

    /// <summary>
    /// The lock is released synchronously as the action ends, in <c>EndHandleLocked</c> (IActionRunner::Update
    /// 0x00540370 / MovementComponent::UnlockTracks 0x0063fe5c), so a stopped animation's lock is gone before
    /// the next play checks it.
    /// </summary>
    [Fact]
    public void APlayedAnimationReleasesItsTrackLockWhenStopped()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var lib = robot.Animations.Library!;
        var clip = lib.ClipNames.Select(lib.GetClip).First(c => CozmoMotion.MaskFor(c.Tracks) != 0);
        byte mask = CozmoMotion.MaskFor(clip.Tracks);

        var ticket = robot.Animations.PlayTracked(clip.Name, lockTracks: clip.Tracks);
        Assert.NotNull(ticket);
        Assert.True(robot.Motion.AreAnyTracksLocked(mask));
        robot.Animations.Stop();
        Assert.False(robot.Motion.AreAnyTracksLocked(mask));   // released in the stop path, not later
    }

    private sealed class LifecycleProbe : SteppedBehavior
    {
        public LifecycleProbe(string id = "probe") : base(id, "Probe") { }
        protected override bool KeepsRunningWithoutAction => true;
        /// <summary>InitInternal (vptr+0x48): the result is non-zero, Init's failure, when this is set.</summary>
        public bool FailInit;
        public int Updates, Stops;
        public Action<LifecycleProbe>? OnStartHook;
        public bool Gate28 = true;
        protected override bool RunnableGate28(BehaviorContext context) => Gate28;
        public BehaviorScope? ScopeSeen => Scope;
        protected override void OnStart() { OnStartHook?.Invoke(this); InitFailed = FailInit; }
        protected override void OnUpdate() => Updates++;
        protected override void OnStop(BehaviorStopReason reason) => Stops++;
        protected override bool IsRunnableInternal(BehaviorContext context) => InternalRunnable;
        public override bool IsRunnable(BehaviorContext context) => InternalRunnable;
        public bool InternalRunnable { get; set; } = true;
        public int HelperStops;
        public bool Gate20 = true;
        protected override bool RunnableGate20(BehaviorContext context) => Gate20;
        protected override void StopHelperWithoutCallback() => HelperStops++;
        public void DoStopActing(bool keepAction, bool viaCallback) => StopActing(keepAction, viaCallback);
        public void SetHandle(object? handle) => SetSharedHandle(handle);
        public void DoWait(double sec, Action done) => Wait(sec, done);
        public void DoParallel(Action done) { var a = StartParallel(2, done); if (a is null) return; WaitInParallel(a, 0.5); PlayTriggerInParallel(a, AnimationTrigger.Hiccup); }
        public void Play(AnimationTrigger trigger, Action done, AnimationTrack tracksToLock = AnimationTrack.None) => PlayTrigger(trigger, done, tracksToLock);
    }

    /// <summary>A behaviour that finishes on its first tick, for the manager's reaction/resume path.</summary>
    private sealed class OneShotBehavior : IBehavior
    {
        public OneShotBehavior(string id) => Id = id;
        public string Id { get; }
        public string Class => "OneShot";
        public bool Runnable { get; set; } = true;
        public bool IsRunnable(BehaviorContext c) => Runnable;
        public double EvaluateScore(BehaviorContext c) => 1;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) => Task.CompletedTask;
        public bool Update(BehaviorContext c, double nowMs) => false;
        public void Stop(BehaviorStopReason r) { }
    }

    /// <summary>A reaction strategy that fires while <see cref="Fire"/> is set, for the manager's resume path. It counts how often it is consulted.</summary>
    private sealed class AlwaysTriggerStrategy : ReactionTriggerStrategy
    {
        public AlwaysTriggerStrategy(ReactionTrigger trigger = ReactionTrigger.CliffDetected, List<string>? order = null)
        { _trigger = trigger; _order = order; }
        private readonly ReactionTrigger _trigger;
        private readonly List<string>? _order;
        public bool Fire = true;
        public int Checks;
        public bool Resume = true;
        public override ReactionTrigger Trigger => _trigger;
        public override string Basis => "test: always trigger";
        public override bool ShouldResumeLast => Resume;
        public override bool CanInterruptOtherTriggeredBehavior => true;
        public override bool CanInterruptSelf => true;
        protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior)
        {
            Checks++;
            _order?.Add("Reactions");
            return Fire;
        }
        public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }
    }

    // ------------------------------------------------------------------ M8-007: the action's track lock

    /// <summary>
    /// M8-007: <c>IActionRunner::Update</c> 0x00540370: when <c>MovementComponent::AreAnyTracksLocked(mask)</c> (0x00540440) is true the action ends with
    /// <c>0x03000019</c> TRACKS_LOCKED (<c>movs r6,#0x19; movt r6,#0x300; str r6,[r4,#0x18]</c> 0x00540572..0x0054057c). It does not retry:
    /// <c>RetriesRemain</c> 0x00540836 has only the two compound actions as callers, and <c>ActionQueue::Update</c> 0x0053f5f4 deletes any action whose
    /// result is not RUNNING. This stack still runs the behaviour's completion callback for the failed action (whether the engine's does is UNKNOWN in
    /// the inventory), once.
    /// </summary>
    [Fact]
    public void ALockedTrackFailsTheActionAndIsNotRetried()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = AnimationTriggerMap.Load(obb), Random = new Random(4), ClockSec = () => 1 };
        var probe = new LifecycleProbe();
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();

        rig.Robot.Motion.LockTracks(CozmoMotion.LiftTrack, "someone-else");   // another owner holds the lift
        int done = 0;
        probe.Play(AnimationTrigger.Hiccup, () => done++, AnimationTrack.Lift);
        Assert.Contains(probe.Trace, l => l.Contains("required tracks are locked") && l.Contains("0x03000019"));
        Assert.False(rig.Robot.Animations.IsPlaying);                          // not started
        Assert.Equal(0, done);                                                   // the completion runs from Update, not inline
        probe.Update(ctx, 0);
        Assert.Equal(1, done);                                                   // one completion for the failed action

        // the holder lets go; nothing retries the action
        rig.Robot.Motion.UnlockTracks(CozmoMotion.LiftTrack, "someone-else");
        for (int tick = 1; tick < 10; tick++) probe.Update(ctx, tick * 33);
        Assert.False(rig.Robot.Animations.IsPlaying);
        Assert.Equal(1, done);
        Assert.Equal(1, probe.Trace.Count(l => l.Contains("required tracks are locked")));
    }

    /// <summary>
    /// M8-007: <c>IActionRunner::Update</c> 0x00540584..0x0054058e: <c>LockTracks(mask +0x54, to_string(action id +0x60), name +0x48)</c>, the id coming from
    /// the same counter every action draws from (0x0053fe54..0x0053fe68), so the owner key is the action's id and no two actions share one. The
    /// lock goes out as <c>DisableAnimTracks</c> (0x0064017c..0x00640188) and its release as <c>EnableAnimTracks</c> (0x0063ffa8..0x0063ffb4).
    /// </summary>
    [Fact]
    public void AnAnimationActionLocksItsMaskUnderItsActionId()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = AnimationTriggerMap.Load(obb), Random = new Random(4), ClockSec = () => 1 };
        var probe = new LifecycleProbe();
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        rig.Pump(); rig.Sent.Clear();

        int before = int.Parse(rig.Robot.Motion.NextActionTag());               // takes one tag from the shared counter
        probe.Play(AnimationTrigger.Hiccup, () => { }, AnimationTrack.Lift);
        Assert.True(rig.Robot.Animations.IsPlaying);
        Assert.True(rig.Robot.Motion.AreAllTracksLockedBy(CozmoMotion.LiftTrack, (before + 1).ToString()));
        Assert.False(rig.Robot.Motion.AreAllTracksLockedBy(CozmoMotion.LiftTrack, "play-1"));
        rig.Pump();
        Assert.Equal(new byte[] { 2 }, rig.Sent.OfType<DisableAnimTracks>().Select(m => m.Field0));
        Assert.Empty(rig.Sent.OfType<EnableAnimTracks>());
        probe.Stop(BehaviorStopReason.Cancelled);
        rig.Pump();
        Assert.Equal(new byte[] { 2 }, rig.Sent.OfType<EnableAnimTracks>().Select(m => m.Field0));
        Assert.Equal(0, rig.Robot.Motion.LockedTracks);
    }

    // ------------------------------------------------------------------ M8-009: the release order inside a category

    /// <summary>
    /// M8-009: <c>IBehavior::Stop</c> 0x005bd08c walks the track-lock <c>std::map&lt;string, u8&gt;</c> at +0xb4 in key order, one <c>UnlockTracks(mask, name)</c> per entry,
    /// each able to send an <c>EnableAnimTracks</c> (0x005bd15c..0x005bd196; 0x0063ffb4). Locks taken as "a" (LIFT) then "b" (HEAD) go out as
    /// DisableAnimTracks 2 then 1 and come back as EnableAnimTracks 2 (key a) then 1 (key b); a most-recent-first release would send 1 then 2.
    /// </summary>
    [Fact]
    public void TheTrackLocksAreReleasedInKeyOrderOnTheWire()
    {
        using var rig = new Rig();
        rig.Pump(); rig.Sent.Clear();
        var scope = new BehaviorScope(motion: rig.Robot.Motion);
        Assert.True(scope.SmartLockTracks("a", AnimationTrack.Lift));
        Assert.True(scope.SmartLockTracks("b", AnimationTrack.Head));
        rig.Pump();
        Assert.Equal(new byte[] { 2, 1 }, rig.Sent.OfType<DisableAnimTracks>().Select(m => m.Field0));
        rig.Sent.Clear();
        scope.Dispose();
        rig.Pump();
        Assert.Equal(new byte[] { 2, 1 }, rig.Sent.OfType<EnableAnimTracks>().Select(m => m.Field0));
    }

    /// <summary>
    /// M8-009: the reaction locks are a <c>std::set&lt;string&gt;</c> at +0xa4; each pass of the loop at 0x005bd12c takes the first node (the lowest name) and calls
    /// <c>SmartRemoveDisableReactionsLock</c>, which removes <c>name + "_behaviorLock"</c> from the manager (0x005bd470..0x005bd4a8). Locks taken as "a"
    /// then "b" are removed a first.
    /// </summary>
    [Fact]
    public void TheReactionLocksAreReleasedInNameOrder()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap() };
        using var m = new BehaviorManager(ctx);
        var log = new List<string>();
        m.Log += log.Add;
        var table = new ReactionLockTable("test", "0x0", new[] { (int)ReactionTrigger.CliffDetected });
        var scope = new BehaviorScope(manager: m);
        Assert.True(scope.SmartDisableReactionsWithLock("a", table));
        Assert.True(scope.SmartDisableReactionsWithLock("b", table));
        Assert.True(m.HasDisableLock(ReactionTrigger.CliffDetected, "a_behaviorLock"));
        Assert.True(m.HasDisableLock(ReactionTrigger.CliffDetected, "b_behaviorLock"));
        log.Clear();
        scope.Dispose();
        var removed = log.Where(l => l.StartsWith("BehaviorManager.RemoveDisableReactionsLock: CliffDetected lock ")).ToList();
        Assert.Equal(new[]
        {
            "BehaviorManager.RemoveDisableReactionsLock: CliffDetected lock a_behaviorLock removed",
            "BehaviorManager.RemoveDisableReactionsLock: CliffDetected lock b_behaviorLock removed",
        }, removed);
    }

    // ------------------------------------------------------------------ M8-003: the score

    /// <summary>
    /// M8-003: <c>EvaluateRepetitionPenalty</c> 0x005beee6 and <c>EvaluateRunningPenalty</c> 0x005bef22 return 1.0 when their stamp (+0x30, +0x34) is &lt;= 0, else
    /// the graph at <c>now - stamp</c>; a behaviour whose config has no graph has the flat (0.0, 1.0) graph (0x005bc55c..0x005bc566, 0x005bc666..0x005bc678).
    /// </summary>
    [Fact]
    public void TheScoreAppliesNoPenaltyForAZeroStampOrAMissingGraph()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap() };
        var b = new Fake("a", 1);
        var recovery = new Graph2d(new[] { (0.0, 0.0), (30.0, 1.0) });
        var constantHalf = new Graph2d(new[] { (0.0, 0.5), (100.0, 0.5) });
        var none = Array.Empty<EmotionScorer>();

        // not running: the repetition penalty
        var withGraph = new ScoredBehaviorEntry("a", 2.0, recovery, null, null, none);
        Assert.Equal(2.0 * (double)(10f / 30f), withGraph.Evaluate(b, ctx, 50, 40, null, null), 9);   // graph at 10 s, in float (vdiv.f32 0x00804c..)
        Assert.Equal(2.0, withGraph.Evaluate(b, ctx, 50, 0, null, null));                          // stamp 0: 1.0
        Assert.Equal(2.0, withGraph.Evaluate(b, ctx, 50, null, null, null));                       // never stopped
        var noGraph = new ScoredBehaviorEntry("a", 2.0, null, null, null, none);
        Assert.Equal(2.0, noGraph.Evaluate(b, ctx, 50, 40, null, null));                           // flat 1.0

        // running: the running penalty, on its own stamp
        var running = new ScoredBehaviorEntry("a", 2.0, null, constantHalf, null, none);
        Assert.Equal(1.0, running.Evaluate(b, ctx, 50, null, 5, null, runningClockSec: 45));       // 2.0 * graph 0.5
        Assert.Equal(2.0, running.Evaluate(b, ctx, 50, null, 5, null, runningClockSec: 0));        // +0x34 <= 0: 1.0
        Assert.Equal(2.0, new ScoredBehaviorEntry("a", 2.0, null, null, null, none).Evaluate(b, ctx, 50, null, 5, null, runningClockSec: 45));   // flat
    }

    /// <summary>
    /// M8-003 / M7-013: MoodScorer::EvaluateEmotionScore 0x0067c9b8 loads the value of the emotion (vldr s22,[r0,#0x18] 0x0067c9f0), calls
    /// Emotion::GetHistoryValueTicksAgo(60) (movs r1,#0x3c 0x0067c9ee, call 0x0067c9f4), subtracts (vsub.f32 0x0067c9fc) and
    /// evaluates the graph at that float. With the samples numbered s0 (the constructor sample {0,0}) to sN, 60 ticks ago is s(N - 59); a fresh mood
    /// (N = 0, count 1 not above 60) answers s0 = 0.0f.
    /// </summary>
    [Fact]
    public void ATrackDeltaScorerSubtractsTheValueSixtyTicksAgoFromTheHistoryRing()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var model = new MoodModel();
        model.AddDecayGraph(new DecayGraph("Happy", new[] { (0.0, 1.0), (1000.0, 0.0) }));
        model.AddEvent(new EmotionEvent("E", new[] { new EmotionAffector(EmotionType.Happy, 0.8) }));
        var mood = new MoodState(model);
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Mood = mood };
        // y = x over [-1, 1] (a node pair, EvaluateY 0x00804BD0: t = (x - x0) / gap, y = y0 + t * (y1 - y0), float)
        var graph = new Graph2d(new[] { (-1.0, -1.0), (1.0, 1.0) });
        var entry = new ScoredBehaviorEntry("a", 2.0, null, null, null, new[] { new EmotionScorer(EmotionType.Happy, graph, TrackDelta: true) });

        // fresh: current 0 - s0 0 = 0 -> y = -1 + (0 - -1) / 2 * 2 = 0 -> the veto (|y| < 1e-5, 0x0067ca50) makes the score 0
        Assert.Equal(0.0, entry.Evaluate(new Fake("a", 1), ctx, 50, null, null, null));

        mood.Trigger("E", 0);
        var vals = new List<float> { 0f };
        for (int t = 1; t <= 100; t++) { mood.Advance(t); vals.Add((float)mood[EmotionType.Happy]); }
        float x = vals[100] - vals[100 - 59];
        Assert.True(x < 0 && x > -1);
        float tt = (x - -1f) / 2f;                                   // EvaluateY: the division first, then the multiply, then the add
        float y = -1f + tt * (1f - -1f);
        Assert.Equal((double)y, entry.Evaluate(new Fake("a", 1), ctx, 50, null, null, null));
    }

    // ------------------------------------------------------------------ M8-012: BehaviorManager::Update

    /// <summary>The activity as the manager sees it, recording the order of its calls.</summary>
    private sealed class RecordingActivity : IManagedActivity
    {
        public RecordingActivity(List<string> calls) => _calls = calls;
        private readonly List<string> _calls;
        public IBehavior? Desired;
        public void Update(double nowSec) => _calls.Add("Activity.Update");
        public IBehavior? GetDesiredActiveBehavior(IBehavior? current, double nowSec) { _calls.Add("Activity.GetDesired"); return Desired; }
        public void BehaviorSwitched(IBehavior? desired, bool started, double nowSec) => _calls.Add($"Activity.Switched:{desired?.Id ?? "null"}:{started}");
    }

    private static (CozmoRobot Robot, BehaviorContext Ctx) Offline(BehaviorArbiter? arbiter = null)
    {
        var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        return (robot, new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Random = new Random(1), Arbiter = arbiter });
    }

    /// <summary>
    /// M8-012: <c>BehaviorManager::Update</c> 0x005a2f68..0x005a31be: the activity's own <c>Update</c> (<c>vtable+0x20</c>, 0x005a2f84) first, then
    /// <c>CheckReactionTriggerStrategies</c> (0x005a3062), then <c>ChooseNextScoredBehaviorAndSwitch</c> (0x005a3078) - which asks the activity for the desired
    /// behaviour (0x005a2a4a) and, when it differs from the running one, switches to it (0x005a2ab4) - then <c>IBehavior::Update</c> (0x005a30bc).
    /// </summary>
    [Fact]
    public void TheTickRunsTheActivityThenReactionsThenTheScoredChoice()
    {
        var (robot, ctx) = Offline();
        using var holder = robot;
        using var m = new BehaviorManager(ctx);
        var calls = new List<string>();
        var strategy = new AlwaysTriggerStrategy(order: calls) { Fire = false };
        m.AddReaction(strategy, new OneShotBehavior("react"));
        var scored = new LifecycleProbe("scored");
        m.Activity = new RecordingActivity(calls) { Desired = scored };

        m.Update(0, 1);
        Assert.Equal(new[] { "Activity.Update", "Reactions", "Activity.GetDesired", "Activity.Switched:scored:True" }, calls);
        Assert.Equal("scored", m.Current!.Id);
        Assert.Equal(1, scored.Updates);                                         // IBehavior::Update ran on the behaviour just switched to

        calls.Clear();
        m.Update(33, 2);                                                         // the same answer: no switch
        Assert.Equal(new[] { "Activity.Update", "Reactions", "Activity.GetDesired" }, calls);
        Assert.Equal(2, scored.Updates);
    }

    /// <summary>
    /// M8-012: the scored switch has three gates (0x005a3068..0x005a3074): no reaction fired (<c>cbnz r5</c>), no UI game behaviour (<c>[this+0x30] == 0</c>) and the
    /// running trigger is NoneTrigger (<c>cmp r0,#0x16</c>). <c>CheckReactionTriggerStrategies</c> runs on every tick regardless, including while a reaction
    /// is the running trigger. The UI game fallback calls <c>SwitchToUIGameRequestBehavior</c> when a reaction did not fire and the UI game behaviour is not
    /// the running one (0x005a307c..0x005a309a).
    /// </summary>
    [Fact]
    public void TheScoredChoiceIsGatedByReactionsTheUiGameBehaviourAndTheRunningTrigger()
    {
        var (robot, ctx) = Offline();
        using var holder = robot;
        using var m = new BehaviorManager(ctx);
        var calls = new List<string>();
        var strategy = new AlwaysTriggerStrategy(order: calls) { Fire = false };
        var react = new LifecycleProbe("react");                                 // runs until it is stopped
        m.AddReaction(strategy, react);
        var scored = new LifecycleProbe("scored");
        m.Activity = new RecordingActivity(calls) { Desired = scored };
        m.Update(0, 1);
        calls.Clear();

        // gate 1: a reaction fired this tick
        strategy.Fire = true;
        m.Update(33, 2);
        Assert.Equal(new[] { "Activity.Update", "Reactions" }, calls);
        Assert.Same(react, m.Current);

        // gate 3: no reaction fired, but the running trigger is not NoneTrigger; the strategy is still consulted
        calls.Clear();
        strategy.Fire = false;
        m.Update(66, 3);
        Assert.Equal(new[] { "Activity.Update", "Reactions" }, calls);
        Assert.Equal(ReactionTrigger.CliffDetected, m.CurrentReactionTrigger);

        // the reaction ends itself (IBehavior::Update returns 2) and the interrupted behaviour is resumed; the tick's switch gate was still closed
        react.Stop(BehaviorStopReason.Completed);
        calls.Clear();
        m.Update(99, 4);
        Assert.Equal(new[] { "Activity.Update", "Reactions" }, calls);
        Assert.Same(scored, m.Current);
        Assert.Null(m.CurrentReactionTrigger);

        // trigger back to NoneTrigger: the scored choice is consulted again
        calls.Clear();
        m.Update(132, 5);
        Assert.Equal(new[] { "Activity.Update", "Reactions", "Activity.GetDesired" }, calls);

        // gate 2: a UI game behaviour exists. The scored choice is skipped and, as it is not the running behaviour, SwitchToUIGameRequestBehavior is called
        var ui = new OneShotBehavior("ui");
        int toUi = 0;
        m.UiGameBehavior = ui;
        m.SwitchToUIGameRequestBehavior = () => toUi++;
        calls.Clear();
        m.Update(165, 6);
        Assert.Equal(new[] { "Activity.Update", "Reactions" }, calls);
        Assert.Equal(1, toUi);
        m.Update(198, 7);
        Assert.Equal(2, toUi);

        // a UI game behaviour with no body for the switch refuses visibly
        m.SwitchToUIGameRequestBehavior = null;
        Assert.Throws<NotSupportedException>(() => m.Update(231, 8));
    }

    /// <summary>
    /// M8-012: <c>BehaviorManager::Update</c> 0x005a2f70: <c>ldrb r0,[r4]; cbz</c> - a manager that is not initialised logs
    /// <c>BehaviorManager.Update.NotInitialized</c> (0x005a2ff6) and returns without ticking the activity or a behaviour. A pending UI game request
    /// (+0x38) calls <c>SelectUIRequestGameBehavior</c> 0x005a2faa, then clears +0x38 (0x005a2fb4).
    /// </summary>
    [Fact]
    public void AnUninitialisedManagerTicksNothingAndAUiGameRequestIsSelectedOnce()
    {
        var (robot, ctx) = Offline();
        using var holder = robot;
        using var m = new BehaviorManager(ctx);
        var calls = new List<string>();
        var log = new List<string>();
        m.Log += log.Add;
        m.Activity = new RecordingActivity(calls);
        m.Initialized = false;
        m.Update(0, 1);
        Assert.Empty(calls);
        Assert.Contains("BehaviorManager.Update.NotInitialized", log);

        m.Initialized = true;
        int selected = 0;
        m.UiGameRequestPending = true;
        m.SelectUIRequestGameBehavior = () => selected++;
        m.Update(33, 2);
        Assert.Equal(1, selected);
        Assert.False(m.UiGameRequestPending);
        m.Update(66, 3);
        Assert.Equal(1, selected);

        // no body for the selection: refused, not skipped
        m.UiGameRequestPending = true;
        m.SelectUIRequestGameBehavior = null;
        Assert.Throws<NotSupportedException>(() => m.Update(99, 4));
    }

    /// <summary>
    /// M8-012: <c>FinishCurrentBehavior(behaviour, tryToResume)</c> 0x005a38c4: <c>cmp r2,#1</c>, and 1 tail-calls <c>TryToResumeBehavior</c> (0x005a38d6). The tick passes
    /// <c>trigger != NoneTrigger</c> (<c>movne r2,#1</c> 0x005a311e..0x005a3122), so a finished reaction resumes what it interrupted. With 0 the manager
    /// switches to the empty info and nothing is resumed.
    /// </summary>
    [Fact]
    public void FinishCurrentBehaviorsSecondArgumentIsTryToResume()
    {
        foreach (bool tryToResume in new[] { true, false })
        {
            var (robot, ctx) = Offline();
            using var holder = robot;
            using var m = new BehaviorManager(ctx);
            var parked = new LifecycleProbe();
            m.Add(parked);
            var strategy = new AlwaysTriggerStrategy();
            var react = new LifecycleProbe("react");
            m.AddReaction(strategy, react);
            Assert.True(m.StartAsync("probe", 0).GetAwaiter().GetResult());
            Assert.NotNull(m.CheckReactions(1));
            strategy.Fire = false;
            Assert.Equal("react", m.Current!.Id);

            m.FinishCurrentBehavior(react, tryToResume, 2);
            if (tryToResume)
            {
                Assert.Same(parked, m.Current);                                  // resumed
                Assert.Equal(1, parked.ResumeCount);
                Assert.Null(m.CurrentReactionTrigger);
            }
            else
            {
                Assert.Null(m.Current);                                          // the empty switch
                Assert.Equal(0, parked.ResumeCount);
                Assert.Null(m.ParkedBehavior);
                Assert.Null(m.CurrentReactionTrigger);
            }
        }
    }

    /// <summary>
    /// M8-012: <c>TryToResumeBehavior</c> 0x005a2b40 calls <c>IBehavior::Resume</c> with no <c>IsRunnable</c> pre-check (0x005a2c5c..0x005a2c76); a stepped behaviour's
    /// own <c>ResumeInternal</c> decides (<c>IsRunnableBase</c> then the vptr+0x50 test, 0x005bda9c..0x005bdab0), and a non-zero result switches to the
    /// empty info (0x005a2c7c..0x005a2cf4). A behaviour that is not a stepped one gets the base <c>ResumeInternal</c>: its own <c>IsRunnable</c>, then its start.
    /// </summary>
    [Fact]
    public void TheManagerResumesWithoutAnIsRunnablePreCheckAndAFailedResumeSwitchesToNothing()
    {
        // a stepped behaviour whose vptr+0x50 says no: ResumeInternal fails, the manager switches to the empty info
        {
            var (robot, ctx) = Offline();
            using var holder = robot;
            using var m = new BehaviorManager(ctx);
            var parked = new LifecycleProbe { InternalRunnable = false };
            m.Add(parked);
            var strategy = new AlwaysTriggerStrategy();
            m.AddReaction(strategy, new OneShotBehavior("react"));
            Assert.True(m.StartAsync("probe", 0).GetAwaiter().GetResult());      // SwitchToBehaviorBase carries on past a failed IsRunnable
            Assert.NotNull(m.CheckReactions(1));
            strategy.Fire = false;
            m.Update(0, 2);
            Assert.Null(m.Current);
            Assert.Null(m.ParkedBehavior);
            Assert.Null(m.CurrentReactionTrigger);
            Assert.Equal(1, parked.ResumeCount);                                 // Resume was called
            Assert.False(parked.EngineRunning);
        }
        // a behaviour that is not a SteppedBehavior has no ResumeInternal of its own, so it gets the base one: the behaviour's own IsRunnable test
        // (IBehavior::ResumeInternal 0x005bda94), not a manager pre-check. Not runnable: the resume fails and the manager switches to nothing;
        // runnable: it runs again.
        foreach (bool runnable in new[] { false, true })
        {
            var (robot, ctx) = Offline();
            using var holder = robot;
            using var m = new BehaviorManager(ctx);
            var parked = new OneShotBehavior("parked") { Runnable = true };
            m.Add(parked);
            var strategy = new AlwaysTriggerStrategy();
            m.AddReaction(strategy, new OneShotBehavior("react"));
            Assert.True(m.StartAsync("parked", 0).GetAwaiter().GetResult());
            parked.Runnable = runnable;
            Assert.NotNull(m.CheckReactions(1));
            strategy.Fire = false;
            m.Update(0, 2);
            if (runnable) Assert.Same(parked, m.Current);
            else { Assert.Null(m.Current); Assert.Null(m.ParkedBehavior); }
        }
    }

    /// <summary>
    /// M8-012: <c>TryToResumeBehavior</c>'s first step (0x005a2b48..0x005a2c3a) restores the stored head and lift when the default head angle (+8) is not FLT_MAX and the
    /// action list is empty - before and whether or not there is a behaviour to resume (the jump on a null resume pointer is at 0x005a2c58).
    /// </summary>
    [Fact]
    public void TheHeadAndLiftRestoreRunsEvenWhenNothingIsParked()
    {
        var (robot, ctx) = Offline();
        using var holder = robot;
        bool empty = false;
        using var m = new BehaviorManager(ctx) { ActionListIsEmpty = () => empty };
        var log = new List<string>();
        m.Log += log.Add;
        var strategy = new AlwaysTriggerStrategy { Resume = false };           // shouldResumeLast = 0: nothing is parked
        m.AddReaction(strategy, new OneShotBehavior("react"));
        m.SetDefaultHeadAndLiftState(true, 0.1f, 50f);
        Assert.NotNull(m.CheckReactions(1));
        strategy.Fire = false;
        Assert.Null(m.ParkedBehavior);
        empty = true;
        m.Update(0, 2);
        Assert.Contains(log, l => l.StartsWith("BehaviorManager.DefaultHeadAnfLiftState.ResumeBehavior: "));
        Assert.Null(m.Current);
    }

    /// <summary>
    /// M8-012: <c>SwitchToBehaviorBase</c> on an <c>Init</c> failure (0x005a1e98 <c>cbz r0</c>) logs <c>BehaviorManager.SetCurrentBehavior.InitFailed</c> (0x005a1eae),
    /// nulls the info's behaviour (0x005a1efc) and still calls <c>SetRunningAndResumeInfo(info)</c> (0x005a1f12) and the DAS transition (0x005a1f1c): the
    /// manager has no running behaviour, but the reaction's trigger is the running trigger and the behaviour it interrupted is still parked. With the trigger
    /// not NoneTrigger the scored switch's third gate stays closed.
    /// </summary>
    [Fact]
    public void AFailedInitStillStoresTheRunningInfo()
    {
        var (robot, ctx) = Offline();
        using var holder = robot;
        using var m = new BehaviorManager(ctx);
        var log = new List<string>();
        m.Log += log.Add;
        var parked = new LifecycleProbe();
        m.Add(parked);
        var failing = new LifecycleProbe("react") { FailInit = true };
        m.AddReaction(new AlwaysTriggerStrategy(), failing);
        var calls = new List<string>();
        m.Activity = new RecordingActivity(calls);
        Assert.True(m.StartAsync("probe", 0).GetAwaiter().GetResult());
        calls.Clear();

        Assert.Null(m.CheckReactions(1));                                       // the switch failed
        Assert.Contains(log, l => l.StartsWith("BehaviorManager.SetCurrentBehavior.InitFailed"));
        Assert.Null(m.Current);
        Assert.Equal(ReactionTrigger.CliffDetected, m.CurrentReactionTrigger);
        Assert.Same(parked, m.ParkedBehavior);
        Assert.False(failing.EngineRunning);

        m.Update(33, 2);                                                         // the running trigger is not NoneTrigger: no scored choice
        Assert.DoesNotContain("Activity.GetDesired", calls);
    }

    /// <summary>
    /// M8-012: <c>CheckReactionTriggerStrategies</c> is called on every tick (0x005a3062), so a strategy is consulted while a reaction runs. The only gate before
    /// <c>ShouldTriggerBehavior</c> is the per-trigger disable-lock count at node+0x28 (0x005a359a..0x005a359e); the engine has no global "reactions disabled"
    /// test, so a behaviour scope's arbiter-wide lock does not stop a reaction.
    /// </summary>
    [Fact]
    public void ReactionsAreConsultedEveryTickAndOnlyAPerTriggerLockSkipsOne()
    {
        var arbiter = new BehaviorArbiter();
        var (robot, ctx) = Offline(arbiter);
        using var holder = robot;
        using var m = new BehaviorManager(ctx);
        var cliff = new AlwaysTriggerStrategy(ReactionTrigger.CliffDetected) { Fire = false };
        var shaken = new AlwaysTriggerStrategy(ReactionTrigger.RobotShaken) { Fire = false };
        m.AddReaction(cliff, new OneShotBehavior("rc"));
        m.AddReaction(shaken, new OneShotBehavior("rs"));
        int fired = 0;
        m.ReactionTriggered += _ => fired++;

        m.Update(0, 1);
        m.Update(33, 2);
        Assert.Equal(2, cliff.Checks);
        Assert.Equal(2, shaken.Checks);

        // an arbiter-wide lock (a scope's DisableReactions) is not an engine gate
        arbiter.DisableReactions(new object());
        shaken.Fire = true;
        m.Update(66, 3);
        Assert.Equal(1, fired);

        // a per-trigger lock skips that trigger and no other (0x005a359a..0x005a359e)
        var mask = new bool[BehaviorManager.TriggerCount];
        mask[(int)ReactionTrigger.RobotShaken] = true;
        m.DisableReactionsWithLock("t", mask, stopCurrent: false);
        int shakenChecks = shaken.Checks, cliffChecks = cliff.Checks;
        m.Update(99, 4);
        Assert.Equal(shakenChecks, shaken.Checks);
        Assert.Equal(cliffChecks + 1, cliff.Checks);
        Assert.Equal(1, fired);
    }

    /// <summary>
    /// M8-012: <c>StopAndNullifyCurrentBehavior</c> 0x005a2028 calls <c>IBehavior::Stop()</c> only if the behaviour's running flag +0xa1 is set, so a behaviour
    /// that stopped itself is not stopped twice.
    /// </summary>
    [Fact]
    public void TheManagerStopsABehaviourOnlyWhileItsRunningFlagIsSet()
    {
        var (robot, ctx) = Offline();
        using var holder = robot;
        using var m = new BehaviorManager(ctx);
        var stoppedItself = new LifecycleProbe("a");
        var running = new LifecycleProbe("b");
        m.Add(stoppedItself);
        m.Add(running);

        Assert.True(m.StartAsync("a", 0).GetAwaiter().GetResult());
        stoppedItself.Stop(BehaviorStopReason.Interrupted);                      // +0xa1 = 0, one StopInternal
        m.Stop(BehaviorStopReason.Cancelled, 5);
        Assert.Equal(1, stoppedItself.Stops);

        Assert.True(m.StartAsync("b", 6).GetAwaiter().GetResult());
        m.Stop(BehaviorStopReason.Cancelled, 7);
        Assert.Equal(1, running.Stops);
    }

    /// <summary>M8-012 B1: Init runs with the manager's current behaviour still null (0x005a1e6a nulled it; SetRunningAndResumeInfo is 0x005a1f12).</summary>
    [Fact]
    public void InitRunsBeforeTheManagerStoresTheRunningInfo()
    {
        var (robot, ctx) = Offline();
        using var holder = robot;
        using var m = new BehaviorManager(ctx);
        IBehavior? seenCurrent = new OneShotBehavior("sentinel");
        var p = new LifecycleProbe { OnStartHook = _ => seenCurrent = m.Current };
        m.Add(p);
        Assert.True(m.StartAsync("probe", 0).GetAwaiter().GetResult());
        Assert.Null(seenCurrent);
        Assert.Same(p, m.Current);
    }

    /// <summary>M8-012 B9: nothing releases a scope but Stop; a failed Init (0x005a1e98..0x005a1efc) leaves what it took held until the behaviour's next Stop (0x005a2028, 0x005bd08c).</summary>
    [Fact]
    public void AFailedInitLeavesItsScopeHeldUntilTheNextStop()
    {
        var (robot, ctx) = Offline();
        using var holder = robot;
        using var m = new BehaviorManager(ctx);
        bool released = false;
        var p = new LifecycleProbe { FailInit = true, OnStartHook = x => x.ScopeSeen!.OnRelease(() => released = true) };
        m.Add(p);
        Assert.False(m.StartAsync("probe", 0).GetAwaiter().GetResult());
        Assert.False(released);
        p.FailInit = false;
        Assert.True(m.StartAsync("probe", 1).GetAwaiter().GetResult());     // the same scope is used again
        Assert.False(released);
        m.Stop(BehaviorStopReason.Cancelled, 2);
        Assert.True(released);
    }

    /// <summary>M8-001 B10: the Resume counter and refusal (0x005bcf16..0x005bcf7c) apply to every behaviour, including one that is not a SteppedBehavior.</summary>
    [Fact]
    public void ANonSteppedBehaviourGetsTheResumeCounterAndRefusal()
    {
        var (robot, ctx) = Offline();
        using var holder = robot;
        using var m = new BehaviorManager(ctx);
        var parked = new OneShotBehavior("parked");
        m.Add(parked);
        var strategy = new AlwaysTriggerStrategy();
        m.AddReaction(strategy, new OneShotBehavior("react"));
        Assert.True(m.StartAsync("parked", 0).GetAwaiter().GetResult());
        Assert.NotNull(m.CheckReactions(1)); strategy.Fire = false;
        m.Update(0, 2);
        Assert.Same(parked, m.Current);                                      // first cliff resume: normal path
        strategy.Fire = true;
        Assert.NotNull(m.CheckReactions(3)); strategy.Fire = false;
        m.Update(0, 4);
        Assert.Null(m.Current);                                              // second: refused
    }

    /// <summary>M8-003 B12: GraphEvaluator2d::EvaluateY (0x00804bd0..0x00804c40) in float; a span at or below 1e-5f returns the LEFT node's y (0x00804c14..0x00804c1c).</summary>
    [Fact]
    public void TheGraphEvaluatorIsFloatAndReturnsTheLeftNodeForATinySpan()
    {
        Assert.Equal(0x3727c5ac, BitConverter.SingleToInt32Bits(GraphEvaluator.Epsilon));
        var g = new Graph2d(new[] { (0.0, 3.0), (0.000001, 9.0), (10.0, 9.0) });
        Assert.Equal(3.0, g.EvaluateY(0.0000005));
        var d = new DecayGraph("t", new (double, double)[] { (0, 3), (0.000001, 9) });
        Assert.Equal(3.0, d.At(0.0000005));
        var h = new Graph2d(new[] { (0.0, 0.0), (30.0, 1.0) });
        Assert.Equal((double)(10f / 30f), h.EvaluateY(10.0));
    }

    /// <summary>M8-012 B2: SetRunningAndResumeInfo's ReactionTriggerTransition message, robot properties and light stop are reported MISSING while nothing is attached.</summary>
    [Fact]
    public void SetRunningAndResumeInfoReportsWhatItDoesNotDo()
    {
        SteppedBehavior.ResetMissingForTests();
        var seen = new List<string>();
        void On(string x) => seen.Add(x);
        SteppedBehavior.MissingReported += On;
        try
        {
            var (robot, ctx) = Offline();
            using var holder = robot;
            using var m = new BehaviorManager(ctx);
            var strategy = new AlwaysTriggerStrategy();
            m.AddReaction(strategy, new OneShotBehavior("react"));
            Assert.NotNull(m.CheckReactions(1));
            Assert.Contains(seen, l => l.Contains("ReactionTriggerTransition"));
            Assert.Contains(seen, l => l.Contains("UpdateRobotPropertiesForReaction"));
            Assert.Contains(seen, l => l.Contains("cube light"));
        }
        finally { SteppedBehavior.MissingReported -= On; }
    }

    /// <summary>M8-007 B8: IActionRunner::Update takes the lock before the action's Init (0x00540584..0x0054058e, then 0x00540592), so an action that cannot resolve its trigger still sends DisableAnimTracks and then EnableAnimTracks.</summary>
    [Fact]
    public void ALockedActionWhoseInitFailsStillSendsTheLockAndTheRelease()
    {
        using var rig = new Rig();
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Random = new Random(1), ClockSec = () => 1 };
        var probe = new LifecycleProbe();
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        rig.Pump(); rig.Sent.Clear();
        probe.Play(AnimationTrigger.Hiccup, () => { }, AnimationTrack.Lift);
        rig.Pump();
        Assert.Equal(new byte[] { 2 }, rig.Sent.OfType<DisableAnimTracks>().Select(m => m.Field0));
        Assert.Equal(new byte[] { 2 }, rig.Sent.OfType<EnableAnimTracks>().Select(m => m.Field0));
        Assert.Equal(0, rig.Robot.Motion.LockedTracks);
    }

    /// <summary>M8-001 B3: [robot+0x284]+8 is read from the robot when no seam is set (0x005bd888..0x005bd89a); a class's own vtable+0x28 decides.</summary>
    [Fact]
    public void TheCarryingGateIsReadFromTheRobot()
    {
        var (robot, ctx) = Offline();
        using var holder = robot;
        var p = new LifecycleProbe { Gate28 = false };
        Assert.True(p.IsRunnableBase(ctx));
        robot.Motion.IsCarryingObject = () => true;
        Assert.False(p.IsRunnableBase(ctx));
    }

    /// <summary>
    /// M8-003: the repetition penalty is float - float then the graph (vsub.f32 0x005bef12; the stamp is a float, 0x005bd11a), the score a float multiply
    /// (vmul.f32 0x005beffe). now = 12345.678, stamp = 12000.1f gives 345.57812 in the engine's route (345.5784 if subtracted in double); the suppression
    /// window compares floats (vcmpe.f32 0x005befea).
    /// </summary>
    [Fact]
    public void TheScorePathIsFloatWithTheEnginesOperationOrder()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap() };
        float dt = (float)12345.678 - 12000.1f;
        Assert.Equal(345.57812f, dt);
        var entry = new ScoredBehaviorEntry("a", 3.0, new Graph2d(new[] { (0.0, 0.0), (1000.0, 1.0) }), null, null, Array.Empty<EmotionScorer>());
        float y = 0f + (dt - 0f) / (1000f - 0f) * (1f - 0f);
        float expected = 3.0f * y;
        var score = entry.Evaluate(new Fake("a", 1), ctx, 12345.678, (double)12000.1f, null, null);
        Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits((float)score));
        // running branch: float(now) - float(+0x34) into the +0xf4 graph, then the float multiply by (score + bonus) (0x005bef4e, 0x005bef88, 0x005bef98)
        var running = new ScoredBehaviorEntry("a", 3.0, null, new Graph2d(new[] { (0.0, 0.0), (1000.0, 1.0) }), null, Array.Empty<EmotionScorer>());
        float rdt = (float)12345.678 - 12000.1f;
        float ry = 0f + (rdt - 0f) / (1000f - 0f) * (1f - 0f);
        float rexpected = (3.0f + 0.7f) * ry;
        var rscore = running.Evaluate(new Fake("a", 1), ctx, 12345.678, null, 345.5784, null, runningBonus: 0.7, runningClockSec: (double)12000.1f);
        Assert.Equal(BitConverter.SingleToInt32Bits(rexpected), BitConverter.SingleToInt32Bits((float)rscore));
        // suppression: float compare against the float sum
        var p = new RepetitionPenalty();
        p.StopWithoutImmediateRepetitionPenalty("X", 12345.678);
        float until = (float)12345.678 + 1.0f;
        Assert.True(p.IsSuppressed("X", 12345.678 + 0.5));
        Assert.False(p.IsSuppressed("X", (double)until));
    }

    /// <summary>
    /// M8-001: a standalone WaitAction is started with StartActing, so +0x84 is set while it waits (0x00608500 ReactToReturnedToTreads, 0x006065f0, 0x00607750), and
    /// StopOnNextActionComplete destroys its callback (+0x98). A wait and an animation in one CompoundActionParallel are one StartActing and one callback (0x006022fe..).
    /// </summary>
    [Fact]
    public void AStandaloneWaitIsTheCurrentActionAndAParallelPairIsOne()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 1 };
        var w = new LifecycleProbe();
        w.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        int called = 0;
        w.DoWait(1.0, () => called++);
        Assert.True(w.HasCurrentAction);
        Assert.Equal(0, w.StartActing());                         // already acting
        w.Update(ctx, 0);
        w.Update(ctx, 1000);
        Assert.Equal(1, called);
        Assert.False(w.HasCurrentAction);

        var d = new LifecycleProbe();
        d.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        d.DoWait(1.0, () => called += 10);
        d.StopOnNextActionComplete();
        d.Update(ctx, 0);
        d.Update(ctx, 1000);                                       // the wait runs out here
        d.Update(ctx, 2000);
        Assert.Equal(1, called);                                   // the +0x98 callback was destroyed: it never ran
        Assert.False(d.HasCurrentAction);                          // but the action did end (+0x84 cleared)

        // CompoundActionParallel::UpdateInternal 0x0054fab0..0x0054fbd0: the animation child fails at once (no map: 0x0300000C), ShouldIgnoreFailure's
        // map is empty (0x0054e991), so the compound ends on that tick with the failure (0x0054fb36..0x0054fbcc) and the wait is never awaited.
        var pair = new LifecycleProbe();
        pair.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        int both = 0;
        pair.DoParallel(() => both++);
        Assert.True(pair.HasCurrentAction);
        pair.Update(ctx, 0);
        Assert.Equal(1, both);
        Assert.False(pair.HasCurrentAction);
        pair.Update(ctx, 600);                                     // the cancelled wait does not call back again
        Assert.Equal(1, both);
    }

    /// <summary>
    /// M8-001: HandleActionComplete 0x005be1e6 stores +0x84 = 0 (0x005be1fc) before the callback is reachable (0x005be216), so a callback that starts the
    /// next action is not refused with AlreadyActing. The completion path clears +0x84 first and the behaviour stays busy until the callback is queued.
    /// </summary>
    [Fact]
    public void ACompletionCallbackMayStartTheNextAction()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = AnimationTriggerMap.Load(obb), Random = new Random(4), ClockSec = () => 1 };
        for (int round = 0; round < 20; round++)
        {
            var probe = new LifecycleProbe();
            probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
            int second = 0;
            probe.Play(AnimationTrigger.Hiccup, () => { probe.Play(AnimationTrigger.Hiccup, () => second++); });
            rig.Robot.Animations.Stop();
            double t = 0;
            for (int i = 0; i < 400 && !probe.Trace.Count(l => l.StartsWith("play ")).Equals(2); i++) { probe.Update(ctx, t += 33); Thread.Sleep(1); }
            Assert.Equal(2, probe.Trace.Count(l => l.StartsWith("play ")));
            Assert.DoesNotContain(probe.Trace, l => l.Contains("AlreadyActing"));
            probe.Stop(BehaviorStopReason.Cancelled);
        }
    }

    /// <summary>
    /// M8-003: MoodScorer::EvaluateEmotionScore 0x0067c9b8 is float: the graph value is a float, the sum is vadd.f32 (0x0067ca5a), the mean is the sum
    /// divided by vcvt.f32.u32(count) (vdiv.f32 0x0067ca6e..0x0067ca72) and the veto compares against the float 0x3727c5ac (0x0067c9da, 0x0067ca50).
    /// Graphs of 0.3, 0.6 and 0.9: the float route is 0.59999996f, the double route 0.6.
    /// </summary>
    [Fact]
    public void TheEmotionScoreIsAFloatMeanWithAFloatVeto()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var model = new MoodModel();
        model.AddEvent(new EmotionEvent("makeHappy", new[] { new EmotionAffector(EmotionType.Happy, 1.0) }));
        var mood = new MoodState(model);
        mood.Trigger("makeHappy", 0);
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Mood = mood };
        static EmotionScorer Const(double y) => new(EmotionType.Happy, new Graph2d(new[] { (0.0, y), (2.0, y) }), false);
        var entry = new ScoredBehaviorEntry("a", 99.0, null, null, null, new[] { Const(0.3), Const(0.6), Const(0.9) });
        float score = (float)entry.Evaluate(new Fake("a", 1), ctx, 0, null, null, null);
        Assert.Equal(BitConverter.SingleToInt32Bits(0.59999996f), BitConverter.SingleToInt32Bits(score));
        Assert.Equal(0x3727c5ac, BitConverter.SingleToInt32Bits(GraphEvaluator.Epsilon));
        // a graph value under the float epsilon vetoes the whole score
        var vetoed = new ScoredBehaviorEntry("a", 99.0, null, null, null, new[] { Const(0.3), Const(0.000005) });
        Assert.Equal(0.0, vetoed.Evaluate(new Fake("a", 1), ctx, 0, null, null, null));
    }

    /// <summary>A chooser that counts its consultations.</summary>
    private sealed class CountingChooser : IBehaviorChooser
    {
        public CountingChooser(IBehavior behavior) => _behavior = behavior;
        private readonly IBehavior _behavior;
        public int Calls;
        public BehaviorChooserType Type => BehaviorChooserType.Scoring;
        public IReadOnlyList<string> BehaviorIds => new[] { _behavior.Id };
        public ChooserDecision GetDesiredActiveBehavior(IBehavior? current, double currentRunningSec, BehaviorContext ctx, double nowSec)
        {
            Calls++;
            return new ChooserDecision(_behavior, "counting", Array.Empty<(string, double, string)>());
        }
    }

    /// <summary>
    /// M8-012: through the live freeplay entry (<c>FreeplaySystem.Tick</c> over a <c>BehaviorManager</c>): the activity pick and its chooser are what
    /// <c>ChooseNextScoredBehaviorAndSwitch</c> asks for, so they run only after <c>CheckReactionTriggerStrategies</c> found nothing (0x005a3062..0x005a3078).
    /// A tick in which a reaction fires does not consult the chooser, the strategy is consulted on every tick, and the next quiet tick consults it again.
    /// </summary>
    [Fact]
    public void TheFreeplayTickChecksReactionsBeforeTheActivityPick()
    {
        using var rig = new Rig();
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Random = new Random(1) };
        var manager = new BehaviorManager(ctx);
        var scored = new LifecycleProbe("scored");
        var chooser = new CountingChooser(scored);
        var sub = new Activity { Id = "A", Strategy = new ActivityStrategy(), Chooser = chooser };
        var freeplay = new Activity { Id = "Freeplay", Type = "Freeplay", Strategy = new ActivityStrategy(), SubActivities = new[] { sub } };
        var bound = new Dictionary<string, IBehavior> { ["scored"] = scored };
        var fp = new FreeplaySystem(manager, ctx, freeplay, bound, new FreeplayInputs());
        var strategy = new AlwaysTriggerStrategy { Fire = false };
        manager.AddReaction(strategy, new OneShotBehavior("react"));

        fp.Tick(0, 0);
        Assert.Equal("scored", manager.Current!.Id);
        Assert.Equal("A", fp.Current!.Id);
        Assert.Equal(1, strategy.Checks);
        int consulted = chooser.Calls;
        Assert.True(consulted >= 1);

        strategy.Fire = true;
        var d = fp.Tick(1, 1000);
        Assert.Equal(2, strategy.Checks);
        Assert.Equal(consulted, chooser.Calls);                                  // a reaction fired: no scored choice this tick
        Assert.Equal("reaction CliffDetected", d.Reason);
        Assert.Same(scored, manager.Current);                                    // the reaction finished and the interrupted behaviour was resumed
        Assert.Equal(1, scored.ResumeCount);

        strategy.Fire = false;
        fp.Tick(2, 2000);
        Assert.Equal(3, strategy.Checks);
        Assert.True(chooser.Calls > consulted);                                  // trigger back to NoneTrigger: the activity is asked again
    }

    // ------------------------------------------------------------------ M8-013: the selection chooser

    /// <summary>
    /// <c>SelectionBSRunnableChooser::GetDesiredActiveBehavior</c> 0x0060ad64: +0x2c is the requested
    /// behaviour, +0x34 is Wait (BehaviorID 0xb2), +0x3c is numRuns (-1 unlimited, one decrement per
    /// running-&gt;stopped edge at 0x0060adbe/0x0060adc0) and +0x40 is the previous running-state latch
    /// (0x0060adc8). The +0x34 countdown block runs only when +0x34 == +0x2c (0x0060ae10/0x0060ae14).
    /// </summary>
    [Fact]
    public void TheSelectionChooserReturnsTheRequestedBehaviourUntilItsNumRunsAreSpent()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Random = new Random(1) };
        var requested = new Fake("wanted", 1);
        var wait = new Fake("Wait", 1);
        var chooser = new SelectionChooser(new Dictionary<string, IBehavior> { ["wanted"] = requested, ["Wait"] = wait });

        Assert.Null(chooser.Requested);
        Assert.Equal("Wait", chooser.Wait!.Id);
        Assert.Equal(-1, chooser.NumRuns);
        Assert.Equal("Wait", chooser.GetDesiredActiveBehavior(null, 0, ctx, 0).Behavior!.Id);   // no request -> Wait

        chooser.RequestBehavior(requested, numRuns: 2);
        Assert.Equal("wanted", chooser.GetDesiredActiveBehavior(null, 0, ctx, 0).Behavior!.Id);       // latch false, no decrement
        Assert.Equal("wanted", chooser.GetDesiredActiveBehavior(requested, 0, ctx, 0).Behavior!.Id);  // running, latch true
        Assert.Equal("wanted", chooser.GetDesiredActiveBehavior(null, 0, ctx, 0).Behavior!.Id);       // edge: 2 -> 1
        Assert.Equal(1, chooser.NumRuns);
        Assert.Equal("wanted", chooser.GetDesiredActiveBehavior(requested, 0, ctx, 0).Behavior!.Id);
        Assert.Equal("Wait", chooser.GetDesiredActiveBehavior(null, 0, ctx, 0).Behavior!.Id);         // edge: 1 -> 0
        Assert.Equal(0, chooser.NumRuns);

        chooser.RequestBehavior(requested, numRuns: 0);                                              // 0 means never returned
        Assert.Equal("Wait", chooser.GetDesiredActiveBehavior(null, 0, ctx, 0).Behavior!.Id);
    }

    // ------------------------------------------------------------------ M8-002: the suppression window

/// <summary>
    /// <c>StopWithoutImmediateRepetitionPenalty</c> 0x005beea0 sets the +0x108 threshold to now + 1.0
    /// (0x005beeb0..0x005beebc). <c>EvaluateRepetitionPenalty</c> 0x005beee6 is the <b>pure graph</b>
    /// evaluation (<c>movle.w r0,#0x3f800000</c> 0x005beef8 when the stamp is &lt;= 0, else the graph at
    /// <c>now - +0x30</c>); the <c>+0x108</c> check lives in <c>EvaluateScore</c> (0x005befe2), so it is
    /// applied at the scoring site, not here.
    /// </summary>
    [Fact]
    public void TheRepetitionPenaltySuppressionIsAtTheScoringSiteNotInTheGraph()
    {
        var p = new RepetitionPenalty(RecoveryGraph());
        p.Ran("X", 100);
        Assert.Equal(0.0, p.For("X", 100), 3);                 // +0x30 just stamped
        p.StopWithoutImmediateRepetitionPenalty("X", 100);
        Assert.True(p.IsSuppressed("X", 100.5));
        Assert.Equal(1.0 / 60.0, p.For("X", 100.5), 4);        // For is the pure graph, no suppression
        Assert.False(p.IsSuppressed("X", 101.0));
        Assert.Equal(1.0 / 30.0, p.For("X", 101.0), 3);        // the graph at 1 s
    }

    // ------------------------------------------------------------------ M8-011: the Smart* helpers

    /// <summary>
    /// <c>IBehavior::SmartRemoveIdleAnimation</c> 0x005bd4c8 raises
    /// <c>VERIFY(%s): Behavior %s is trying to remove an idle, but none is currently set</c> through
    /// <c>sVerifyFailedReturnFalse</c> when +0xb0 is clear; it does not silently return.
    /// </summary>
    [Fact]
    public void SmartRemoveIdleAnimationWithoutAnIdleRaisesAVerifyFailure()
    {
        using var scope = new BehaviorScope();
        var failures = new List<string>();
        scope.VerifyFailed += failures.Add;
        Assert.False(scope.SmartRemoveIdleAnimation());
        Assert.Single(failures);
        Assert.Contains("SmartRemoveIdleAnimation", failures[0]);
    }

    /// <summary>
    /// <c>IBehavior::SmartPushIdleAnimation</c> 0x005be41c fails when +0xb0 is already set, and
    /// <c>SmartLockTracks</c> 0x005be5bc warns and returns 0 for an existing key rather than locking twice.
    /// </summary>
    [Fact]
    public void TheSmartHelpersRefuseADoubleAcquire()
    {
        using var scope = new BehaviorScope();
        var failures = new List<string>();
        scope.VerifyFailed += failures.Add;

        Assert.True(scope.SmartPushIdleAnimation(() => { }, () => { }));
        Assert.True(scope.IdleAnimationSet);
        Assert.False(scope.SmartPushIdleAnimation(() => { }, () => { }));

        Assert.True(scope.SmartLockTracks("a", AnimationTrack.Head));
        Assert.Equal(AnimationTrack.Head, scope.LockedTracks);
        Assert.False(scope.SmartLockTracks("a", AnimationTrack.Lift));
        Assert.Equal(AnimationTrack.Head, scope.LockedTracks);
        Assert.Equal(2, failures.Count);
    }

    /// <summary>The scope releases the Smart* acquisitions when it is disposed (the M8-009 release path).</summary>
    [Fact]
    public void TheSmartHelpersReleaseThroughTheScope()
    {
bool idleSet = false, idleRemoved = false, motionSet = false, motionCleared = false, lightPlayed = false;
        var scope = new BehaviorScope();
        scope.SmartPushIdleAnimation(() => idleSet = true, () => idleRemoved = true);
        scope.SmartSetMotionProfile(() => motionSet = true, () => motionCleared = true);
        scope.SmartSetCustomLightPattern(7, () => lightPlayed = true);
        scope.SmartLockTracks("a", AnimationTrack.Head);

        Assert.True(idleSet && motionSet && lightPlayed);
        Assert.True(scope.IdleAnimationSet && scope.MotionProfileSet);
        Assert.Contains(7u, scope.CustomLightPatterns);

        scope.Dispose();
        Assert.True(idleRemoved && motionCleared);
        Assert.False(scope.IdleAnimationSet);
        Assert.False(scope.MotionProfileSet);
        Assert.Empty(scope.CustomLightPatterns);
        Assert.Equal(AnimationTrack.None, scope.LockedTracks);
    }

    /// <summary>
    /// <c>SmartRemoveCustomLightPattern</c> 0x005be9b0 logs "No custom light pattern is set for object %d"
    /// and returns false for an object that was never set.
    /// </summary>
    [Fact]
    public void SmartRemoveCustomLightPatternWithoutAPatternFails()
    {
        using var scope = new BehaviorScope();
        var failures = new List<string>();
        scope.VerifyFailed += failures.Add;
        Assert.False(scope.SmartRemoveCustomLightPattern(3, () => { }));
        Assert.Single(failures);
    }

    // ------------------------------------------------------------------ M8-014: the whiteboard

private sealed class FakeExternalInterface : IWhiteboardExternalInterface
    {
        public readonly List<int> SubscribedTags = new();
        public void SubscribeWhiteboardHandler(int tag, AIWhiteboard whiteboard) => SubscribedTags.Add(tag);
    }

    /// <summary>
    /// <c>AIWhiteboard::Init</c> 0x0056a394 subscribes three MessageEngineToGame handlers when the robot
    /// has an external interface — tags 0x44=68 RobotObservedObject, 0x45=69 RobotObservedPossibleObject
    /// and 0x35=53 RobotOffTreadsStateChanged — and otherwise warns "Initialized whiteboard with no
    /// external interface. Will miss events." (0x0056a3d2).
    /// </summary>
    [Fact]
    public void AWhiteboardWithNoExternalInterfaceWarnsOnInit()
    {
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, Cozmo.Protocol.ObjectType)>()), () => 0);
        var log = new List<string>();
        wb.Log += log.Add;
        wb.Init();
        Assert.Contains(log, l => l.Contains("no external interface"));
    }

    [Fact]
    public void AWhiteboardWithAnExternalInterfaceRegistersItsHandlers()
    {
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, Cozmo.Protocol.ObjectType)>()), () => 0);
        var external = new FakeExternalInterface();
        wb.ExternalInterface = external;
        wb.Init();
        Assert.Equal(new[] { AIWhiteboard.TagRobotObservedObject, AIWhiteboard.TagRobotObservedPossibleObject,
                             AIWhiteboard.TagRobotOffTreadsStateChanged }, external.SubscribedTags);
        Assert.Equal(new[] { 68, 69, 53 }, external.SubscribedTags);
    }

    /// <summary>
    /// <c>AIWhiteboard::AddBeacon</c> 0x0056c39c appends to the vector at +0x60 and calls
    /// <c>UpdateBeaconRender</c> (0x0056c3de); <c>Update</c> 0x0056a684 is a no-op.
    /// </summary>
    [Fact]
    public void AddBeaconAppendsAndRaisesTheRenderSeam()
    {
        var wb = new AIWhiteboard(new BlockWorld(() => Array.Empty<(uint, Cozmo.Protocol.ObjectType)>()), () => 0);
        int renders = 0;
        wb.BeaconRenderUpdated += () => renders++;
        var b = wb.AddBeacon(Pose3d.Identity, 100);
        Assert.Single(wb.Beacons);
        Assert.Same(b, wb.GetActiveBeacon());
        Assert.Equal(1, renders);
        wb.Update();                                   // no-op, no throw
        Assert.Single(wb.Beacons);
    }
}

