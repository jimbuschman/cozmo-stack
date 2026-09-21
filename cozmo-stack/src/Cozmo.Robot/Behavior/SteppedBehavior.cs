using System.Collections.Concurrent;
using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// A behaviour written the way the engine's <c>BehaviorReactToX</c> classes are written: a chain of
/// transitions, each starting one action (<c>TriggerAnimationAction</c>, <c>WaitAction</c>,
/// <c>CalibrateMotorAction</c>) whose completion calls the next transition, and a per-tick
/// <c>UpdateInternal</c> for the classes that poll the robot in between.
///
/// The engine's <c>IBehavior::StartActing</c> runs an action and calls a member function when it ends;
/// <c>StopActing</c> cancels it; a behaviour with no action running completes. Those three things are
/// reproduced here, so each shipped class can be transcribed transition by transition rather than
/// re-designed. Completion callbacks are queued and run inside <see cref="Update"/>, on the manager's
/// thread and clock, which is where the engine's run too.
/// </summary>
public abstract class SteppedBehavior : IBehavior
{
    private readonly object _gate = new();
    private readonly ConcurrentQueue<Action> _pending = new();
    private readonly List<string> _trace = new();

    private CozmoAnimations? _animations;
    private long _generation;
    private bool _owns;
    private volatile bool _acting;

    private Action? _onWaitDone;
    private double _waitRemainingMs = double.NaN, _waitDeadlineMs = double.NaN;

    private Func<bool>? _waitCondition;
    private Action<bool>? _onConditionDone;
    private double _conditionTimeoutMs, _conditionDeadlineMs = double.NaN;

    private volatile bool _finished = true;

    // The engine's StopActing(false, false) cancels the action without its callback. Each started action
    // remembers the epoch it began in; a stop advances the epoch and the stale completion is dropped.
    private int _actionEpoch;

    protected SteppedBehavior(string id, string behaviorClass)
    {
        Id = id;
        Class = behaviorClass;
    }

    public string Id { get; }
    public string Class { get; }

    /// <summary>Reactions outscore ordinary behaviours by default, as the engine's preempt them.</summary>
    public double Score { get; set; } = 5.0;

    /// <summary>
    /// The clock used for timings that outlive one run (a behaviour that remembers when it last ran).
    /// Milliseconds. Settable so a test can wind it.
    /// </summary>
    public Func<double> Clock { get; set; } = () => Environment.TickCount64;

    /// <summary>What the behaviour has done this run, one line per step. For tests and the conformance tools.</summary>
    public IReadOnlyList<string> Trace { get { lock (_gate) return _trace.ToArray(); } }

    /// <summary>Raised for every trace line as it is written.</summary>
    public event Action<string>? Step;

    /// <summary>The context and scope of the current run. Valid from <see cref="OnStart"/> until stopped.</summary>
    protected BehaviorContext Context { get; private set; } = null!;

    /// <summary>
    /// <c>IBehavior::NeedActionCompleted</c> 0x005BE40C: report a needs action to the needs manager - the one
    /// named here, or this behaviour's own <c>needsActionID</c> from its shipped config when none is named.
    /// Returns what was reported, or null when there was nothing to report.
    /// </summary>
    protected string? NeedActionCompleted(string? actionId = null, string? fallback = null) =>
        BehaviorNeedsActions.Complete(this, Context, actionId, fallback);
    protected BehaviorScope Scope { get; private set; } = null!;

    /// <summary>The manager's clock as of the current <see cref="Update"/>, milliseconds.</summary>
    protected double NowMs { get; private set; }

    /// <summary>The manager's clock at the first <see cref="Update"/> of this run, or null before it.</summary>
    protected double? StartedMs { get; private set; }

    /// <summary>Whether an animation started by this behaviour is still playing.</summary>
    protected bool Acting => _acting;

    /// <summary>Whether anything is in flight: an animation, a wait, a condition, or a queued callback.</summary>
    protected bool Busy => _acting || _onWaitDone is not null || _waitCondition is not null || !_pending.IsEmpty;

    /// <summary>
    /// The engine completes a behaviour that is not acting. A class whose <c>UpdateInternal</c> polls the
    /// robot between actions overrides this to stay alive; it then ends itself with <see cref="Finish"/>.
    /// </summary>
    protected virtual bool KeepsRunningWithoutAction => false;

    public virtual bool IsRunnable(BehaviorContext context) =>
        context.Robot.Animations.Library is not null && IsRunnableInternal(context);

    /// <summary>The engine's <c>IsRunnableInternal</c>. Most reaction classes just return true.</summary>
    protected virtual bool IsRunnableInternal(BehaviorContext context) => true;

    public virtual double EvaluateScore(BehaviorContext context) => IsRunnable(context) ? Score : 0;

    public Task StartAsync(BehaviorContext context, BehaviorScope scope, CancellationToken cancel)
    {
        Context = context;
        Scope = scope;
        _finished = false;
        StartedMs = null;
        ClearWaits();
        while (_pending.TryDequeue(out _)) { }
        lock (_gate) _trace.Clear();
        OnStart();
        if (!_finished && !Busy && !KeepsRunningWithoutAction) _finished = true;
        return Task.CompletedTask;
    }

    /// <summary>The engine's <c>InitInternal</c>: start the first action.</summary>
    protected abstract void OnStart();

    /// <summary>The engine's <c>UpdateInternal</c>, after queued completions and waits have been serviced.</summary>
    protected virtual void OnUpdate() { }

    /// <summary>The engine's <c>StopInternal</c>.</summary>
    protected virtual void OnStop(BehaviorStopReason reason) { }

    public bool Update(BehaviorContext context, double nowMs)
    {
        if (_finished) return false;
        NowMs = nowMs;
        StartedMs ??= nowMs;

        // Only the completions already queued when this tick began run now. A callback that starts an
        // action which fails at once re-queues its own completion, and draining that inline would spin.
        int queued = _pending.Count;
        while (queued-- > 0 && !_finished && _pending.TryDequeue(out var callback)) callback();

        if (!_finished && _onWaitDone is { } waitDone)
        {
            if (double.IsNaN(_waitDeadlineMs)) _waitDeadlineMs = nowMs + _waitRemainingMs;
            if (nowMs >= _waitDeadlineMs)
            {
                _onWaitDone = null; _waitDeadlineMs = double.NaN;
                waitDone();
            }
        }

        if (!_finished && _waitCondition is { } condition)
        {
            if (double.IsNaN(_conditionDeadlineMs)) _conditionDeadlineMs = nowMs + _conditionTimeoutMs;
            bool met = condition();
            if (met || nowMs >= _conditionDeadlineMs)
            {
                var done = _onConditionDone;
                _waitCondition = null; _onConditionDone = null; _conditionDeadlineMs = double.NaN;
                done?.Invoke(met);
            }
        }

        if (!_finished) OnUpdate();
        if (!_finished && !Busy && !KeepsRunningWithoutAction) _finished = true;
        return !_finished;
    }

    public void Stop(BehaviorStopReason reason)
    {
        _finished = true;
        ClearWaits();
        lock (_gate) _actionEpoch++;
        StopOwnAnimation();
        OnStop(reason);
    }

    /// <summary>Ends the behaviour of its own accord: the engine's behaviour running out of actions.</summary>
    protected void Finish()
    {
        if (_finished) return;
        _finished = true;
        Log("done");
    }

    /// <summary>The engine's <c>StopActing</c>: cancel whatever action is running, without ending the behaviour.</summary>
    protected void StopActing()
    {
        ClearWaits();
        lock (_gate) _actionEpoch++;
        while (_pending.TryDequeue(out _)) { }
        StopOwnAnimation();
    }

    /// <summary>
    /// The engine's <c>TriggerAnimationAction</c>: resolve the trigger through the shipped map, claim its
    /// tracks, play it, and call <paramref name="onDone"/> when it ends. A trigger that resolves to nothing
    /// is reported and its callback still runs (the engine's action fails and its completion still fires),
    /// on the next tick rather than inline so a retry loop cannot recurse.
    ///
    /// The tracks are a lock, not a mute. Every action carries a track mask at +0x54 and
    /// <c>IActionRunner::Update</c> 0x00540370 does two things with it: while
    /// <c>MovementComponent::AreAnyTracksLocked(mask)</c> is true it refuses to run the action at all,
    /// warning "Action %s [%d] not running because required tracks are locked" (0x005404A8) and trying
    /// again on the next tick; otherwise it calls <c>MovementComponent::LockTracks(mask, tag, name)</c>
    /// through the helper at 0x004F0F4C and then runs it. LockTracks 0x00640098 walks the bits of the mask
    /// and records one owner per track in a multiset, and UnlockTracks 0x0063FE5C takes them out again, so
    /// a track can be held by several owners at once and is free when the last of them lets go.
    ///
    /// So an animation's tracks are claimed for the duration of the play and nothing else may drive them;
    /// they are not silenced in the animation itself. That is what the scope does here, and a play whose
    /// tracks are owned waits rather than being skipped.
    /// </summary>
    /// <param name="alsoLock">
    /// Tracks the engine's action locks on top of the ones the clip uses - its <c>tracksToLock</c>
    /// argument, which <c>BehaviorReactToUnexpectedMovement</c> sets to 4 (the body) when the movement
    /// came from behind. They are added to the scope's claim, which is what keeps the keep-alive off them
    /// for the length of the play.
    /// </param>
    protected void PlayTrigger(AnimationTrigger trigger, Action onDone, AnimationTrack alsoLock = AnimationTrack.None)
    {
        var lib = Context.Robot.Animations.Library;
        if (lib is null) { Log($"{trigger}: no animation assets are loaded"); _pending.Enqueue(onDone); return; }

        var resolved = Context.Triggers.Resolve(trigger, lib, Context.Random);
        if (!resolved.Resolved)
        {
            Log($"{trigger}: {resolved.Problem ?? "resolved to no animation"}");
            _pending.Enqueue(onDone);
            return;
        }

        var clip = lib.GetClip(resolved.Selected!);
        Scope.LockTracks(clip.Tracks | alsoLock);

        var ticket = Context.Robot.Animations.PlayTracked(resolved.Selected!);
        if (ticket is null)
        {
            // A track it needs is owned. The engine waits: IActionRunner::Update leaves the action queued
            // and tries again next tick rather than failing it, so the play is deferred, not skipped.
            Log($"{trigger} -> {resolved.Selected}: a track it needs is owned; waiting for it");
            _pending.Enqueue(() => PlayTrigger(trigger, onDone, alsoLock));
            return;
        }

        Log($"play {trigger} -> {resolved.Selected}");
        int epoch;
        lock (_gate)
        {
            _animations = Context.Robot.Animations;
            _generation = ticket.Generation;
            _owns = true;
            _acting = true;
            epoch = _actionEpoch;
        }
        ticket.Completion.ContinueWith(_ =>
        {
            bool current;
            lock (_gate)
            {
                current = epoch == _actionEpoch;
                if (current) { _owns = false; _acting = false; }
            }
            if (current) _pending.Enqueue(onDone);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>The engine's <c>WaitAction</c>: call back after this many seconds of the manager's clock.</summary>
    protected void Wait(double seconds, Action onDone)
    {
        _waitRemainingMs = seconds * 1000;
        // Started from a transition inside Update the wait begins now; started from OnStart, before the
        // manager has ticked, it begins at the first tick.
        _waitDeadlineMs = StartedMs is null ? double.NaN : NowMs + _waitRemainingMs;
        _onWaitDone = onDone;
        Log($"wait {seconds:F1} s");
    }

    /// <summary>
    /// The engine's <c>WaitForLambdaAction</c>: call back when the condition holds, or with false when the
    /// timeout passes first.
    /// </summary>
    protected void WaitUntil(Func<bool> condition, double timeoutSec, Action<bool> onDone, string what)
    {
        _waitCondition = condition;
        _onConditionDone = onDone;
        _conditionTimeoutMs = timeoutSec * 1000;
        _conditionDeadlineMs = StartedMs is null ? double.NaN : NowMs + _conditionTimeoutMs;
        Log($"wait for {what} (up to {timeoutSec:F1} s)");
    }

    /// <summary>
    /// The engine's <c>CalibrateMotorAction(head: true, lift: false)</c>: ask the robot to recalibrate the
    /// head and wait for its report that the calibration started and then finished.
    ///
    /// <c>CalibrateMotorAction::CheckIfDone</c> 0x00547D38 is the whole rule - it stays running until the
    /// motors it was asked for report calibrated, testing <c>Robot::IsHeadCalibrated</c> and
    /// <c>IsLiftCalibrated</c> against the two request flags at +0x78 and +0x79 and the two
    /// already-started flags at +0x7A and +0x7B - and it has no timeout to cut it short: the action's
    /// timeout, at <c>IAction</c>+0x74, is the -1 the constructor writes there
    /// (<c>movt r1, #0xbf80</c> at 0x00540CA2), which is the engine's "no timeout".
    ///
    /// LOCAL_POLICY: the five seconds here is this stack's backstop, not the engine's. Waiting for ever on
    /// a robot that never answers is not something to reproduce; the engine's own behaviours that sit out
    /// a recalibration - ReactToImpact and ReactToMotorCalibration - both wait five seconds, so that is
    /// the number used. On hardware the report arrives in about two.
    /// </summary>
    protected void CalibrateHead(Action onDone)
    {
        var state = Context.Robot.State;
        bool started = false;
        Context.Robot.Motion.RequestMotorCalibration(head: true, lift: false);
        Log("calibrate head (StartMotorCalibration head=1 lift=0)");
        WaitUntil(() =>
        {
            if (state.HeadCalibrating) started = true;
            return started && !state.HeadCalibrating;
        }, CalibrationAllowanceSec, ok =>
        {
            Log(ok ? "head calibration reported complete" : "no head calibration report within the allowance");
            onDone();
        }, "head calibration");
    }

    /// <summary>The allowance for a requested calibration to report back (see <see cref="CalibrateHead"/>).</summary>
    public const double CalibrationAllowanceSec = 5.0;

    /// <summary>
    /// Queues a completion to run on the next <see cref="Update"/>, on the manager's thread. Animation
    /// completions arrive this way; a subclass with another asynchronous action posts its completion here.
    /// </summary>
    protected void Post(Action callback) => _pending.Enqueue(callback);

    /// <summary>Writes one trace line.</summary>
    protected void Log(string line)
    {
        lock (_gate) _trace.Add(line);
        Step?.Invoke(line);
    }

    private void ClearWaits()
    {
        _onWaitDone = null; _waitDeadlineMs = double.NaN;
        _waitCondition = null; _onConditionDone = null; _conditionDeadlineMs = double.NaN;
    }

    private void StopOwnAnimation()
    {
        _acting = false;
        PlayAnimBehavior.StopOwnAnimation(ref _animations, ref _generation, ref _owns, _gate);
    }
}
