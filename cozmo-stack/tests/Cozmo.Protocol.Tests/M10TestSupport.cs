using Cozmo.Robot.Behavior;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Helpers for driving an M10 reaction strategy's public decision entry,
/// <c>IReactionTriggerStrategy.ShouldTriggerBehavior(robot, behaviour)</c> (M10 inventory C6). The engine passes the
/// behaviour as an argument; the strategy's own predicate is <c>ShouldTriggerBehaviorInternal</c> over it.
///
/// A test that wants the strategy's predicate uses a runnable stand-in, exactly as the engine's
/// <c>beh+0xA1 || IsRunnable</c> treats an already-running behaviour as runnable (gap1 2b/2c). A strategy that
/// produces a target for its bound behaviour is instead given that behaviour, because it writes the target onto it
/// (CubeMoved, ObjectPositionUpdated, NoPreDockPoses).
/// </summary>
internal static class M10Support
{
    /// <summary>The reaction context the manager builds for one tick: context, BaseStationTimer seconds, the
    /// current reaction trigger (C5), and "is running" (IBehavior+0xA1; none of these stand-ins is running).</summary>
    public static ReactionContext Context(BehaviorContext ctx, double nowSec, ReactionTrigger? current = null) =>
        new(ctx, nowSec, current, _ => false);

    /// <summary>C6 with a runnable stand-in behaviour, for a predicate-only test.</summary>
    public static bool Triggers(IReactionTriggerStrategy strategy, BehaviorContext ctx, double nowSec,
                                ReactionTrigger? current = null) =>
        strategy.ShouldTriggerBehavior(Context(ctx, nowSec, current), Runnable.Instance);

    /// <summary>
    /// The pre-M10 test call shape, mapped onto C6 with a runnable stand-in. Lets a predicate-only test read
    /// as it did before the strategy API took the behaviour as an argument (inventory C6).
    /// </summary>
    public static bool ShouldTrigger(this IReactionTriggerStrategy strategy, BehaviorContext ctx,
                                     ReactionTrigger? current, double nowSec) =>
        strategy.ShouldTriggerBehavior(Context(ctx, nowSec, current), Runnable.Instance);

    /// <summary>The same, for a target-producing strategy whose bound behaviour must receive the target.</summary>
    public static bool ShouldTrigger(this IReactionTriggerStrategy strategy, BehaviorContext ctx,
                                     ReactionTrigger? current, double nowSec, IBehavior behaviour) =>
        strategy.ShouldTriggerBehavior(Context(ctx, nowSec, current), behaviour);

    /// <summary>C6 with the strategy's own bound behaviour, for a target-producing strategy.</summary>
    public static bool Triggers(IReactionTriggerStrategy strategy, BehaviorContext ctx, double nowSec,
                                ReactionTrigger? current, IBehavior behaviour) =>
        strategy.ShouldTriggerBehavior(Context(ctx, nowSec, current), behaviour);

    /// <summary>A trivial runnable behaviour, or one that is not, usable as a manager's current or as a stand-in.</summary>
    public static IBehavior RunnableBehaviour(string id = "m10-test-behaviour", bool runnable = true) =>
        new StandIn(id, runnable);

    private sealed class StandIn : IBehavior
    {
        public StandIn(string id, bool runnable) { Id = id; Runnable = runnable; }
        public string Id { get; }
        public string Class => "test";
        public bool Runnable { get; set; }
        public bool IsRunnable(BehaviorContext c) => Runnable;
        public double EvaluateScore(BehaviorContext c) => 1;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) => Task.CompletedTask;
        public bool Update(BehaviorContext c, double nowMs) => true;
        public void Stop(BehaviorStopReason r) { }
    }

    private sealed class Runnable : IBehavior
    {
        public static readonly Runnable Instance = new();
        public string Id => "m10-test-behaviour";
        public string Class => "test";
        public bool IsRunnable(BehaviorContext c) => true;
        public double EvaluateScore(BehaviorContext c) => 1;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) => Task.CompletedTask;
        public bool Update(BehaviorContext c, double nowMs) => true;
        public void Stop(BehaviorStopReason r) { }
    }
}