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

// every trigger's clip resolves. Each action's tracks are locked by the scheduler for that action
        // (IActionRunner::Update 0x00540370 / MovementComponent::LockTracks 0x00640098), not pre-claimed on
        // the scope, so the scope holds nothing.
        using var scope = new BehaviorScope();
        bored.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        var lib = robot.Animations.Library!;
        AnimationTrack union = AnimationTrack.None;
        foreach (var t in bored.Triggers)
        {
            var r = ctx.Triggers.Resolve(t, lib, new Random(4));
            if (r.Resolved) union |= lib.GetClip(r.Selected!).Tracks;
        }
        Assert.NotEqual(AnimationTrack.None, union);
        Assert.Equal(AnimationTrack.None, scope.LockedTracks);
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
        // the clip's tracks are locked by the scheduler for the play (IActionRunner::Update 0x00540370 /
        // MovementComponent::LockTracks 0x00640098), not by the scope
        byte mask = CozmoMotion.MaskFor(robot.Animations.Library!.GetClip(b.LastSelected!).Tracks);
        Assert.True(mask != 0 && robot.Motion.AreAnyTracksLocked(mask));
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
    /// The M8-001 lifecycle state on a <see cref="SteppedBehavior"/>: Init's running flag +0xa1 and the
    /// +0x80 counter (<c>cbz</c> on the <c>vtable+0x48</c> result at 0x005bcd06/0x005bcd0c), and Resume's
    /// +0x114 counter and +0x118 = now + 15.0 on the second CliffDetected/UnexpectedMovement
    /// (<c>cmp r5,#0x14</c> 0x005bcf16; <c>vstr s0,[r4,#0x118]</c> 0x005bcf4c). The first such trigger takes
    /// the normal path (<c>blt 0x5bcf7e</c> 0x005bcf2c). IsRunnableScored reads +0x118.
    /// </summary>
    [Fact]
    public void TheLifecycleInitAndResumeFollowTheEngineFields()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 100 };
        var probe = new LifecycleProbe { InternalRunnable = true };
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.False(probe.EngineRunning);          // IsRunnableInternal non-zero clears +0xa1
        Assert.Equal(0, probe.ScoreIncreaseCount);

        var probe2 = new LifecycleProbe { InternalRunnable = false };
        probe2.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.True(probe2.EngineRunning);          // otherwise +0xa1 stays 1 and +0x80++
        Assert.Equal(1, probe2.ScoreIncreaseCount);

        // first CliffDetected: normal path, no suppression
        Assert.False(probe2.Resume(ReactionTrigger.CliffDetected, 100));
        Assert.Equal(1, probe2.ResumeCount);
        Assert.True(probe2.IsRunnableScored(100));
        // second: TooManyResumesCliffOrMovement, +0x118 = now + 15
        bool fired = false;
        probe2.TooManyResumesCliffOrMovement += () => fired = true;
        Assert.True(probe2.Resume(ReactionTrigger.CliffDetected, 200));
        Assert.True(fired);
        Assert.Equal(2, probe2.ResumeCount);
        Assert.Equal(215.0, probe2.ResumeSuppressionUntilSec, 3);
        Assert.False(probe2.IsRunnableScored(214));
        Assert.True(probe2.IsRunnableScored(215));
        // a non-counting trigger never takes the special path
        Assert.False(probe2.Resume(ReactionTrigger.RobotShaken, 300));
        Assert.Equal(2, probe2.ResumeCount);
    }

    /// <summary>
    /// <c>IncreaseScoreWhileActing</c> 0x005bf02c adds to +0x104 only while the current-action handle +0x84
    /// is non-zero (<c>cbz r2,#0x5bf042</c> 0x005bf030); <c>ScoredActingStateChanged</c> 0x005bf044 clears
    /// it and has no engine caller.
    /// </summary>
    [Fact]
    public void IncreaseScoreWhileActingOnlyAddsWhileActing()
    {
        var probe = new LifecycleProbe();
        probe.IncreaseScoreWhileActing(5.0);
        Assert.Equal(0.0, probe.RunningScoreBonus, 3);   // not acting: +0x84 is zero
        probe.ScoredActingStateChanged(false);
        Assert.Equal(0.0, probe.RunningScoreBonus, 3);
    }

    /// <summary>
    /// <c>IBehavior::StopOnNextActionComplete</c> 0x005bd624 sets +0xa0 = 1
    /// (<c>strb.w r1,[r4,#0xa0]</c> 0x005bd698), so <c>Update</c> 0x005bd074 returns 2 once the current-action
    /// handle +0x84 is clear. Its callers are the M7/M15 concrete behaviours.
    /// </summary>
    [Fact]
    public void StopOnNextActionCompleteMakesUpdateReturnTwo()
    {
        var probe = new LifecycleProbe();
        Assert.Equal(0, probe.UpdateStatus());
        probe.StopOnNextActionComplete();
        Assert.Equal(2, probe.UpdateStatus());
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
    /// <c>IBehavior::StopActing(keepAction, viaCallback)</c> 0x005bd34c: <c>viaCallback</c> non-zero skips
    /// the <c>StopHelperWithoutCallback</c> path (<c>cbnz r6,#0x5bd3da</c> 0x005bd362); <c>keepAction</c>
    /// controls whether <c>+0x84</c> is cleared (0x005bd3e0/0x005bd3e4).
    /// </summary>
    [Fact]
    public void StopActingHonoursItsViaCallbackAndKeepActionParameters()
    {
        var probe = new LifecycleProbe();
        probe.DoStopActing(keepAction: false, viaCallback: true);
        Assert.Equal(0, probe.HelperStops);
        probe.DoStopActing(keepAction: false, viaCallback: false);
        Assert.Equal(1, probe.HelperStops);
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
        var penalty = new RepetitionPenalty();
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
    /// The manager's resume path calls <c>IBehavior::Resume</c> (0x005a2c76) and must not run
    /// <c>InitLifecycle</c>, which clears +0x114. So a second CliffDetected resume through
    /// <c>BehaviorManager.Update</c> takes the TooManyResumesCliffOrMovement path (0x005bcf4c).
    /// </summary>
    [Fact]
    public void TheManagerResumePathPreservesTheResumeCounter()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Random = new Random(1) };
        using var m = new BehaviorManager(ctx);
        var parked = new LifecycleProbe();
        m.Add(parked);
        m.AddReaction(new AlwaysTriggerStrategy(), new OneShotBehavior("react"));

        Assert.True(m.StartAsync("probe", 0).GetAwaiter().GetResult());
        Assert.Equal("probe", m.Current!.Id);

        // the reaction fires and parks the probe; the reaction then finishes and the probe resumes
        Assert.NotNull(m.CheckReactions(1));
        Assert.Equal("react", m.Current!.Id);
        m.Update(0, 2);
        Assert.Equal("probe", m.Current!.Id);
        Assert.Equal(1, parked.ResumeCount);

        // again: the counter survives the resume, so the second takes the special path
        bool fired = false;
        parked.TooManyResumesCliffOrMovement += () => fired = true;
        Assert.NotNull(m.CheckReactions(3));
        Assert.Equal("react", m.Current!.Id);
        m.Update(0, 4);
        Assert.True(fired);
        Assert.Null(m.Current);
        Assert.Equal(2, parked.ResumeCount);
        Assert.False(parked.IsRunnableScored(4));
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

        Assert.Equal("a", m.ChooseAndSwitch(0).Chosen);
        m.Stop(BehaviorStopReason.Interrupted, 0);
        Assert.Equal("b", m.ChooseAndSwitch(1).Chosen);   // a's repetition penalty applies at 1 s
    }

    /// <summary>
    /// The behaviour-side <c>StopWithoutImmediateRepetitionPenalty</c> (0x005beea0) writes +0x108 = now + 1.0
    /// on the shared history, which the scored chooser then honours.
    /// </summary>
    [Fact]
    public void TheBehaviourApiWritesTheSuppressionWindow()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var penalty = new RepetitionPenalty();
        var ctx = new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 50, Penalty = penalty };
        var probe = new LifecycleProbe();
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();

        penalty.Ran("probe", 50);
        Assert.False(penalty.IsSuppressed("probe", 50));
        probe.StopWithoutImmediateRepetitionPenalty();
        Assert.True(penalty.IsSuppressed("probe", 50.5));
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
        m.FinishCurrentBehavior(m.Find("b")!, immediate: true, 2);
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
    /// a play whose required tracks are held is refused (0x005404a8) and retried, not played.
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
        Assert.Null(robot.Animations.PlayTracked(clip.Name, lockTracks: clip.Tracks));   // refused, retried
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
        public LifecycleProbe() : base("probe", "Probe") { }
        protected override bool KeepsRunningWithoutAction => true;
        protected override void OnStart() { }
        protected override bool IsRunnableInternal(BehaviorContext context) => InternalRunnable;
        public override bool IsRunnable(BehaviorContext context) => InternalRunnable;
        public bool InternalRunnable { get; set; } = true;
        public int HelperStops;
        public bool Gate20 = true;
        protected override bool RunnableGate20(BehaviorContext context) => Gate20;
        protected override void StopHelperWithoutCallback() => HelperStops++;
        public void DoStopActing(bool keepAction, bool viaCallback) => StopActing(keepAction, viaCallback);
        public void SetHandle(object? handle) => SetSharedHandle(handle);
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

    /// <summary>A reaction strategy that always fires, for the manager's resume path.</summary>
    private sealed class AlwaysTriggerStrategy : ReactionTriggerStrategy
    {
        public override ReactionTrigger Trigger => ReactionTrigger.CliffDetected;
        public override string Basis => "test: always trigger";
        public override bool ShouldResumeLast => true;
        public override bool CanInterruptOtherTriggeredBehavior => true;
        public override bool CanInterruptSelf => true;
        protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior) => true;
        public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }
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
        var p = new RepetitionPenalty();
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

