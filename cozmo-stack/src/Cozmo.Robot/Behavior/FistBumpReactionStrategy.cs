using Cozmo.Protocol;

namespace Cozmo.Robot.Behavior;

// fidelity: M10-003
// Independent implementation only: M7-018 does not yet provide a safe configured target.
internal sealed class FistBumpReactionStrategy : ReactionTriggerStrategy, IDisposable
{
    private static readonly string[] Names = {
        "Unknown", "AnimationPlayed", "BlockRolled", "BuiltPyramid", "DroveAsIntended", "EnrolledFaceWithName",
        "FireTruckAlarmPerformed", "FistBumpComplete", "FistBumpSuccess", "FistBumpLeftHanging", "FoundFace",
        "GatheredCubes", "InteractedWithFace", "KnockedOverBlocks", "LaserPounced", "LaserTracked", "PeekABooComplete",
        "PeekABooSuccess", "PerformedWorkout", "PerformedStrongWorkout", "PickedupBlock", "PickedUpAndPutDownBlock",
        "PoppedWheelie", "PouncedAndCaught", "PutDownBlock", "ReactedAcknowledgedCubeMoved", "ReactedAcknowledgedFace",
        "ReactedAcknowledgedObject", "ReactedToCliff", "ReactedToFrustration", "ReactedToOnCharger", "ReactedToPickup",
        "ReactedToPoke", "ReactedToRobotOnBack", "ReactedToRobotOnFace", "ReactedToRobotOnSide", "ReactedToRobotShaken",
        "ReactedToUnexpectedMovement", "RequestedGame", "Singing", "StackedBlock", "Count" };
    private readonly record struct Parameters(float Cooldown, float Probability, float Expiration);
    private readonly Dictionary<uint, Parameters> _parameters = new();
    private readonly object _gate = new();
    private readonly Func<float> _clock;
    private readonly Func<double> _draw;
    private readonly IDisposable? _subscription;
    private bool _disposed, _pending;
    private float _expiry, _lastCompletion;

    internal static string? ObjectiveName(uint value) => value < Names.Length ? Names[value] : null;
    internal static uint ObjectiveFromName(string name, Action<string>? diagnostic = null)
    {
        int index = Array.IndexOf(Names, name);
        if (index >= 0) return (uint)index;
        diagnostic?.Invoke($"BehaviorObjectiveFromString: unknown objective '{name}'");
        return 0;
    }

    // Future checked target binding calls this before appending the mapping (0x005A0526).
    // No production caller exists until M7-018 supplies that target.
    internal static FistBumpReactionStrategy CreateForTarget(EngineRobot robot,
        IEnumerable<FistBumpObjectiveParams> rows, Action<Action<bool>> addListener)
    {
        Func<Action<object?>, IDisposable>? subscribe = robot.HasExternalInterface
            ? callback => robot.Engine.AnimationMessages.SubscribeMessage(0x8B, true, callback) : null;
        var strategy = new FistBumpReactionStrategy(rows, () => robot.Engine.Timer.SecondsF,
            () => robot.Engine.ContextRandom.RandDbl(1), robot.Engine.EmitStackLog, subscribe);
        strategy.BindListener(addListener);
        return strategy;
    }

    // Subscribe only when the actual target exists and the native external interface is present.
    internal FistBumpReactionStrategy(IEnumerable<FistBumpObjectiveParams> rows, Func<float> clock,
        Func<double> draw, Action<string>? diagnostic = null, Func<Action<object?>, IDisposable>? subscribe = null)
    {
        _clock = clock; _draw = draw;
        foreach (var row in rows)
        {
            uint objective = ObjectiveFromName(row.BehaviorObjective, diagnostic);
            float cooldown = (float)row.TriggerCooldownTimeSec, probability = (float)row.TriggerProbability,
                expiration = (float)row.TriggerExpirationSec;
            if (objective == 41 || !(cooldown >= 0) || !(probability > 0 && probability <= 1) || !(expiration > 0))
            { diagnostic?.Invoke($"FistBump invalid objective parameters: {row.BehaviorObjective}"); continue; }
            _parameters[objective] = new(cooldown, probability, expiration);
        }
        _subscription = subscribe?.Invoke(payload => Receive(0x8B, payload));
    }

    public override ReactionTrigger Trigger => ReactionTrigger.FistBump;
    public override string Basis => "FistBump strategy 0x60DFB4..0x60EA2E";
    public override bool ShouldResumeLast => false;
    public override bool CanInterruptOtherTriggeredBehavior => false;
    public override bool CanInterruptSelf => false;
    internal bool Pending { get { lock (_gate) return _pending; } }
    internal float Expiry { get { lock (_gate) return _expiry; } }
    internal float LastCompletion { get { lock (_gate) return _lastCompletion; } }

    internal void Receive(int tag, object? payload)
    {
        lock (_gate)
        {
            if (_disposed || tag != 0x8B || payload is not uint objective) return;
            float now = _clock();
            if (!_parameters.TryGetValue(objective, out var p)) return;
            if (_lastCompletion != 0f && now - _lastCompletion <= p.Cooldown) return;
            if (_draw() >= (double)p.Probability) return;
            _pending = true; _expiry = now + p.Expiration;
        }
    }
    internal void ResetTrigger(bool updateLastCompletion)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _pending = false;
            if (updateLastCompletion) _lastCompletion = _clock();
        }
    }
    // Called by the eventual native target binding before the manager appends its map entry.
    // A weak callback avoids retaining or invoking a retired strategy through the legacy listener API.
    internal void BindListener(Action<Action<bool>> addListener)
    {
        var weak = new WeakReference<FistBumpReactionStrategy>(this);
        addListener(value => { if (weak.TryGetTarget(out var strategy)) strategy.ResetTrigger(value); });
    }
    protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            float now = _clock();
            if (!_pending) return false;
            // Native reads the target's +0xA1, which can diverge from manager Current.
            bool running = behavior is SteppedBehavior stepped ? stepped.EngineRunning : rc.IsRunning(behavior);
            if (running) { _pending = false; return false; }
            if (now > _expiry) { _pending = false; return false; }
            return rc.Context.Robot.Sensors.OffTreadsState == OffTreadsState.OnTreads && behavior.IsRunnable(rc.Context);
        }
    }
    public override void EnabledStateChanged(BehaviorContext context, bool enabled) { }
    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        _subscription?.Dispose();
    }
}
