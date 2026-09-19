using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// A behaviour that plays one animation, chosen from the shipped trigger map.
///
/// This is the reconstruction of the engine's config-driven <c>PlayAnim</c> and <c>PlayAnimWithFace</c>
/// classes, which name their animation in an <c>animTriggers</c> field rather than in code — 33 shipped
/// configs do exactly that. Where a config lists more than one trigger the first that resolves is used.
///
/// It claims the tracks its clip touches for as long as it runs, through the scope, so the idle layer
/// yields those tracks and only those, which is what the engine's <c>SmartLockTracks</c> does.
/// </summary>
public sealed class PlayAnimBehavior : IBehavior
{
    private readonly IReadOnlyList<AnimationTrigger> _triggers;
    private Task<AnimationEndReason>? _playing;
    private volatile bool _finished;

    public PlayAnimBehavior(string id, string behaviorClass, IEnumerable<AnimationTrigger> triggers,
                            double score = 1.0)
    {
        Id = id;
        Class = behaviorClass;
        _triggers = triggers.ToList();
        Score = score;
    }

    public string Id { get; }
    public string Class { get; }

    /// <summary>How much this wants to run. Configs carry no score, so a caller sets it.</summary>
    public double Score { get; set; }

    /// <summary>The animation actually selected on the last start, for tracing.</summary>
    public string? LastSelected { get; private set; }

    public bool IsRunnable(BehaviorContext context) =>
        context.Robot.Animations.Library is not null && _triggers.Count > 0;

    public double EvaluateScore(BehaviorContext context) => Score;

    public Task StartAsync(BehaviorContext context, BehaviorScope scope, CancellationToken cancel)
    {
        _finished = false;
        LastSelected = null;
        var lib = context.Robot.Animations.Library;
        if (lib is null) { _finished = true; return Task.CompletedTask; }

        foreach (var trigger in _triggers)
        {
            var resolved = context.Triggers.Resolve(trigger, lib, context.Random);
            if (!resolved.Resolved) continue;

            var clip = lib.GetClip(resolved.Selected!);
            scope.LockTracks(clip.Tracks);
            LastSelected = resolved.Selected;
            _playing = context.Robot.Animations.Play(resolved.Selected!);
            if (_playing is null) { _finished = true; return Task.CompletedTask; }
            _playing.ContinueWith(_ => _finished = true, TaskScheduler.Default);
            return Task.CompletedTask;
        }

        // Nothing resolved. Finishing immediately is the honest outcome; it is not an error and it is
        // not a reason to play something else.
        _finished = true;
        return Task.CompletedTask;
    }

    public bool Update(BehaviorContext context, double nowMs) => !_finished;

    public void Stop(BehaviorStopReason reason)
    {
        _finished = true;
        _playing = null;
    }
}

/// <summary>
/// A behaviour that plays whatever animation it is handed.
///
/// The shipped <c>PlayArbitraryAnim</c> config carries no animation at all, because the engine's version
/// is told which one to play by whoever starts it. This keeps that shape: the clip is a property rather
/// than configuration.
/// </summary>
public sealed class PlayArbitraryAnimBehavior : IBehavior
{
    private Task<AnimationEndReason>? _playing;
    private volatile bool _finished = true;

    public string Id => "PlayArbitraryAnim";
    public string Class => "PlayArbitraryAnim";

    /// <summary>The clip to play. Nothing runs until this is set.</summary>
    public string? ClipName { get; set; }

    /// <summary>How much this wants to run when it has a clip.</summary>
    public double Score { get; set; } = 1.0;

    public bool IsRunnable(BehaviorContext context) =>
        ClipName is not null && context.Robot.Animations.Library?.HasClip(ClipName) == true;

    public double EvaluateScore(BehaviorContext context) => IsRunnable(context) ? Score : 0;

    public Task StartAsync(BehaviorContext context, BehaviorScope scope, CancellationToken cancel)
    {
        _finished = false;
        var lib = context.Robot.Animations.Library;
        if (ClipName is null || lib is null || !lib.HasClip(ClipName))
        {
            _finished = true;
            return Task.CompletedTask;
        }
        scope.LockTracks(lib.GetClip(ClipName).Tracks);
        _playing = context.Robot.Animations.Play(ClipName);
        if (_playing is null) { _finished = true; return Task.CompletedTask; }
        _playing.ContinueWith(_ => _finished = true, TaskScheduler.Default);
        return Task.CompletedTask;
    }

    public bool Update(BehaviorContext context, double nowMs) => !_finished;

    public void Stop(BehaviorStopReason reason) { _finished = true; _playing = null; }
}

/// <summary>
/// A behaviour that reacts to something the robot reports about itself.
///
/// Only the reactions M4 actually detects are built this way — cliff, pick-up and charger — because a
/// reaction whose cause nothing reports could never run. It defers to <see cref="ReactionTable"/> for
/// which animation to play, so it inherits that table's recorded uncertainty rather than adding one.
/// </summary>
public sealed class ReactBehavior : IBehavior
{
    private readonly ReactionTable _table;
    private readonly Func<CozmoRobot, bool> _condition;
    private Task<AnimationEndReason>? _playing;
    private volatile bool _finished = true;

    public ReactBehavior(string id, string behaviorClass, ReactionTrigger trigger,
                         Func<CozmoRobot, bool> condition, ReactionTable? table = null, double score = 5.0)
    {
        Id = id;
        Class = behaviorClass;
        Trigger = trigger;
        _condition = condition;
        _table = table ?? ReactionTable.Default;
        Score = score;
    }

    public string Id { get; }
    public string Class { get; }
    public ReactionTrigger Trigger { get; }

    /// <summary>Reactions outscore ordinary behaviours by default, as the engine's preempt them.</summary>
    public double Score { get; set; }

    public string? LastSelected { get; private set; }

    public bool IsRunnable(BehaviorContext context) =>
        _table.For(Trigger) is not null
        && context.Robot.Animations.Library is not null
        && _condition(context.Robot);

    public double EvaluateScore(BehaviorContext context) => IsRunnable(context) ? Score : 0;

    public Task StartAsync(BehaviorContext context, BehaviorScope scope, CancellationToken cancel)
    {
        _finished = false;
        LastSelected = null;
        var entry = _table.For(Trigger);
        var lib = context.Robot.Animations.Library;
        if (entry is null || lib is null) { _finished = true; return Task.CompletedTask; }

        var resolved = context.Triggers.Resolve(entry.Animation, lib, context.Random);
        if (!resolved.Resolved) { _finished = true; return Task.CompletedTask; }

        scope.LockTracks(lib.GetClip(resolved.Selected!).Tracks);
        // A reaction should not be interrupted by another reaction part way through.
        scope.DisableReactions();
        LastSelected = resolved.Selected;
        _playing = context.Robot.Animations.Play(resolved.Selected!);
        if (_playing is null) { _finished = true; return Task.CompletedTask; }
        _playing.ContinueWith(_ => _finished = true, TaskScheduler.Default);
        return Task.CompletedTask;
    }

    public bool Update(BehaviorContext context, double nowMs) => !_finished;

    public void Stop(BehaviorStopReason reason) { _finished = true; _playing = null; }
}

/// <summary>
/// The behaviours this stack can actually run, built from the shipped configs.
///
/// Five of the 178 shipped behaviours, which is the honest count. The rest are blocked on cubes, vision,
/// Wwise switch-state audio, or robot state nothing yet derives; see `BEHAVIOR_INVENTORY.md` for each
/// one's blocker. Nothing is stubbed with a fake input to make this list longer.
/// </summary>
public static class ShippedBehaviors
{
    /// <summary>Creates the runnable set, matching the shipped configs' ids and classes.</summary>
    public static IReadOnlyList<IBehavior> Implementable() => new IBehavior[]
    {
        // PlayAnim behaviours: the config names the trigger, so these are faithful to the shipped data.
        new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup }),
        new PlayAnimBehavior("ReactToObstacle", "PlayAnim", new[] { AnimationTrigger.ReactToObstacle }),
        new PlayArbitraryAnimBehavior(),

        // Reactions whose cause M4 reports.
        new ReactBehavior("ReactToCliff", "ReactToCliff", ReactionTrigger.CliffDetected,
                          r => r.Sensors.CliffDetectedNow),
        new ReactBehavior("ReactToPickup", "ReactToPickup", ReactionTrigger.RobotPickedUp,
                          r => r.Sensors.PickedUp),
    };
}
