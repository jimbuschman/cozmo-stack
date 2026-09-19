namespace Cozmo.Robot.Behavior;

/// <summary>Where a behaviour is in its lifecycle.</summary>
public enum BehaviorLifecycle
{
    /// <summary>Constructed and configured, not running.</summary>
    Ready,
    /// <summary>Running.</summary>
    Running,
    /// <summary>Running, and waiting for the action it started to finish.</summary>
    Acting,
    /// <summary>Asked to stop once its current action completes.</summary>
    Stopping,
    /// <summary>Not running. May be started again.</summary>
    Stopped,
}

/// <summary>Why a behaviour stopped, which decides whether it is penalised for repeating.</summary>
public enum BehaviorStopReason
{
    /// <summary>It finished what it set out to do.</summary>
    Completed,
    /// <summary>Something of higher priority took over.</summary>
    Interrupted,
    /// <summary>A caller stopped it.</summary>
    Cancelled,
    /// <summary>It could not continue.</summary>
    Failed,
}

/// <summary>
/// What a behaviour can reach. Passed in rather than reached for, so a behaviour can be exercised
/// offline against a robot that is not connected.
/// </summary>
public sealed class BehaviorContext
{
    public required CozmoRobot Robot { get; init; }
    public required AnimationTriggerMap Triggers { get; init; }
    public BehaviorArbiter? Arbiter { get; init; }
    public MoodState? Mood { get; init; }
    public Random Random { get; init; } = new();
}

/// <summary>
/// One behaviour: something Cozmo can decide to do.
///
/// This is a reconstruction of the shipped <c>IBehavior</c>, whose interface **is** exported by the engine
/// even though the concrete <c>BehaviorReactToX</c> subclasses are not. The lifecycle below is taken from
/// those exported names rather than designed here:
///
/// * <c>Init</c>, <c>IsRunnable</c>, <c>Update</c>/<c>UpdateInternal</c>, <c>Stop</c>, <c>Resume</c>
/// * <c>StartActing</c>, <c>StopActing</c>, <c>HandleActionComplete</c>,
///   <c>StopOnNextActionComplete</c> — a behaviour runs *actions* and is told when they finish
/// * <c>EvaluateScore</c>, <c>EvaluateRepetitionPenalty</c>, <c>IncreaseScoreWhileActing</c> — which
///   behaviour runs is decided by score, not by a fixed list
/// * the <c>Smart*</c> family — <c>SmartLockTracks</c>, <c>SmartPushIdleAnimation</c>,
///   <c>SmartSetCustomLightPattern</c>, <c>SmartDisableReactionsWithLock</c> and their removals — are
///   scoped acquisitions the engine releases for the behaviour when it stops. That pattern is reproduced
///   by <see cref="BehaviorScope"/>.
/// </summary>
public interface IBehavior
{
    /// <summary>The shipped <c>behaviorID</c> this implements.</summary>
    string Id { get; }

    /// <summary>The shipped <c>behaviorClass</c> this implements.</summary>
    string Class { get; }

    /// <summary>Whether this could run right now, given the robot's state.</summary>
    bool IsRunnable(BehaviorContext context);

    /// <summary>
    /// How much this wants to run, before penalties. The engine's <c>EvaluateScoreInternal</c>. Zero or
    /// less means it does not want to run at all.
    /// </summary>
    double EvaluateScore(BehaviorContext context);

    /// <summary>Starts. Called only when <see cref="IsRunnable"/> was true.</summary>
    Task StartAsync(BehaviorContext context, BehaviorScope scope, CancellationToken cancel);

    /// <summary>
    /// Advances the behaviour. Returns false when it has finished of its own accord, which the engine
    /// expresses by the behaviour completing its actions.
    /// </summary>
    bool Update(BehaviorContext context, double nowMs);

    /// <summary>Stops. Anything the behaviour acquired through its scope is released after this returns.</summary>
    void Stop(BehaviorStopReason reason);
}

/// <summary>
/// The scoped resources a behaviour holds while it runs.
///
/// The engine's <c>Smart*</c> methods exist so a behaviour cannot leak a track lock or a light pattern by
/// forgetting to undo it: whatever it takes is released when it stops. This does the same, and
/// <see cref="Dispose"/> is what releases everything at once.
/// </summary>
public sealed class BehaviorScope : IDisposable
{
    private readonly List<Action> _undo = new();
    private readonly object _gate = new();
    private readonly BehaviorArbiter? _arbiter;
    private bool _disposed;

    /// <summary>
    /// A scope with no arbiter models the locks without enforcing them, which is only useful in tests.
    /// Pass the arbiter for the locks to actually take effect.
    /// </summary>
    public BehaviorScope(BehaviorArbiter? arbiter = null) => _arbiter = arbiter;

    /// <summary>Tracks this behaviour has claimed, released when it stops.</summary>
    public Animation.AnimationTrack LockedTracks { get; private set; }

    /// <summary>Whether this behaviour has asked for reactions to be held off.</summary>
    public bool ReactionsDisabled { get; private set; }

    /// <summary>The engine's SmartLockTracks: claim tracks for as long as this behaviour runs.</summary>
    public void LockTracks(Animation.AnimationTrack tracks)
    {
        lock (_gate)
        {
            if (_disposed) return;
            var before = LockedTracks;
            LockedTracks |= tracks;
            _undo.Add(() => LockedTracks = before);
        }
    }

    /// <summary>
    /// The engine's SmartDisableReactionsWithLock: stop reactions interrupting this behaviour.
    ///
    /// The lock is taken on the arbiter, so it actually suppresses reactions rather than only recording
    /// that it was asked for, and it is released with the scope.
    /// </summary>
    public void DisableReactions()
    {
        lock (_gate)
        {
            if (_disposed || ReactionsDisabled) return;
            ReactionsDisabled = true;
            _arbiter?.DisableReactions(this);
            _undo.Add(() =>
            {
                ReactionsDisabled = false;
                _arbiter?.EnableReactions(this);
            });
        }
    }

    /// <summary>Registers any other undo, for resources this type does not model directly.</summary>
    public void OnRelease(Action undo)
    {
        lock (_gate)
        {
            if (_disposed) undo();
            else _undo.Add(undo);
        }
    }

    /// <summary>Releases everything, most recent first.</summary>
    public void Dispose()
    {
        List<Action> undo;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            undo = new List<Action>(_undo);
            _undo.Clear();
        }
        for (int i = undo.Count - 1; i >= 0; i--)
        {
            try { undo[i](); }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
        }
    }
}

/// <summary>
/// The repetition penalty: how much a behaviour's score is reduced for having run recently.
///
/// From the shipped <c>mood_config.json</c>, whose <c>defaultRepetitionPenalty</c> is a two-node graph
/// running from (0 s, 0.0) to (30 s, 1.0). So a behaviour that has just run scores zero and recovers
/// linearly to its full score after thirty seconds. This is what stops Cozmo doing the same thing twice
/// in a row without anything having to forbid it.
/// </summary>
public sealed class RepetitionPenalty
{
    private readonly DecayGraph _graph;
    private readonly Dictionary<string, double> _lastRunSec = new();

    public RepetitionPenalty(DecayGraph? graph = null) =>
        _graph = graph ?? new DecayGraph("defaultRepetitionPenalty",
            new (double, double)[] { (0, 0), (30, 1) });

    /// <summary>The multiplier for a behaviour, 0 immediately after it ran and 1 once recovered.</summary>
    public double For(string behaviorId, double nowSec) =>
        _lastRunSec.TryGetValue(behaviorId, out var last) ? _graph.At(nowSec - last) : 1.0;

    /// <summary>Records that a behaviour ran.</summary>
    public void Ran(string behaviorId, double nowSec) => _lastRunSec[behaviorId] = nowSec;

    /// <summary>
    /// The engine's <c>StopWithoutImmediateRepetitionPenalty</c>: a behaviour that was interrupted rather
    /// than completed is not penalised for it.
    /// </summary>
    public void Forget(string behaviorId) => _lastRunSec.Remove(behaviorId);
}
