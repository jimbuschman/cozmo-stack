using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Behavior;

// fidelity: M10-003, M10-016
// The canonical reaction map supplies the robot-owned inputs, configured target,
// shared RNG and whiteboard and retains the strategy until manager teardown.
internal sealed class HiccupReactionStrategy : ReactionTriggerStrategy, IDisposable
{
    internal static HiccupReactionStrategy? NativeGlobal { get; private set; }
    private readonly AIWhiteboard _whiteboard;
    private readonly EngineRandom _rng;
    private readonly Func<uint> _now;
    private readonly Func<bool> _feature;
    private readonly Func<bool?> _unlocked;
    private readonly Func<int> _severe;
    private readonly Func<int, int> _bracket;
    private readonly Action<int> _needs;
    private readonly Action<bool> _report;
    private readonly Action<float> _keepAlive;
    private readonly Action<bool, uint> _das;
    private readonly Action<string> _missing;
    private readonly Action _disabledInput;
    private readonly List<IDisposable> _subscriptions = new();
    private readonly uint _minOccurrence, _maxOccurrence, _minCount, _maxCount, _minSpacing, _maxSpacing, _cureDelay;
    private uint _occurrence, _spacing, _remaining, _total, _first;
    private bool _disabled, _reported, _disposed;
    private int _state;

    internal HiccupReactionStrategy(HiccupParams? p, AIWhiteboard whiteboard, EngineRandom rng,
        Func<uint> now, Func<bool> feature, Func<bool?> unlocked, Func<int> severe,
        Func<int, int> bracket, Action<int> needs, Action<bool> report, Action<float> keepAlive,
        Action<bool, uint> das, Action<string> missing, Func<HiccupReactionStrategy, IDisposable>? subscribeBase = null,
        Action? disabledInput = null)
    {
        _whiteboard = whiteboard; _rng = rng; _now = now; _feature = feature; _unlocked = unlocked;
        _severe = severe; _bracket = bracket; _needs = needs; _report = report;
        _keepAlive = keepAlive; _das = das; _missing = missing;
        _disabledInput = disabledInput ?? (() => missing("ReactionTriggerStrategyHiccup.OverfedButHiccupDisabled 0x00610A7C"));
        unchecked {
            _minOccurrence = (uint)(p?.MinHiccupOccurrenceFrequencySec ?? 0) * 1000;
            _maxOccurrence = (uint)(p?.MaxHiccupOccurrenceFrequencySec ?? 0) * 1000;
            _minCount = (uint)(p?.MinNumberOfHiccupsToDo ?? 0); _maxCount = (uint)(p?.MaxNumberOfHiccupsToDo ?? 0);
            _minSpacing = (uint)(p?.MinHiccupSpacingMs ?? 0); _maxSpacing = (uint)(p?.MaxHiccupSpacingMs ?? 0);
            _cureDelay = (uint)(p?.HiccupsWontOccurAfterBeingCuredTimeSec ?? 0) * 1000;
        }
        if (subscribeBase is not null) AddSubscription(subscribeBase(this));
        Reset();
    }
    public override ReactionTrigger Trigger => ReactionTrigger.Hiccup;
    public override string Basis => "Hiccup 0x0060FD50..0x00610B42";
    public override bool ShouldResumeLast => true;
    public override bool CanInterruptOtherTriggeredBehavior => false;
    public override bool CanInterruptSelf => false;
    public override bool ShouldTriggerBehavior(ReactionContext rc, IBehavior behavior)
    {
        if (!Forced) return ShouldTriggerBehaviorInternal(rc, behavior);
        bool result = ShouldTriggerBehaviorInternal(rc, behavior);
        Forced = false;
        if (_disposed || result) return !_disposed && result;
        _missing("M1-034/M10-003 Hiccup forced fallback 0x00610412: class 0x29 downcast is null; skipped vector write and admission");
        return false;
    }
    internal void ReceiveExecuteReactionTrigger(byte trigger, byte behavior, byte boundBehavior)
    {
        if (_disposed) return;
        if (trigger == (byte)Trigger && behavior == boundBehavior) Forced = true;
        else if (trigger == 0x16) Forced = false;
    }
    private uint Draw(uint min, uint max) => unchecked((uint)_rng.RandIntInRange((int)min, (int)max));
    private void Reset()
    {
        uint now = _now();
        if (_disposed) return;
        _remaining = _total = Draw(_minCount, _maxCount);
        _occurrence = _spacing = unchecked(now + Draw(_minOccurrence, _maxOccurrence));
        _first = 0;
        if (_reported) { _reported = false; _report(false); }
        if (_disposed) return;
        _whiteboard.HiccupsActive = false;
    }
    private void Cure(bool player)
    {
        uint now = _now();
        if (_disposed) return;
        _das(player, _first == 0 ? 0 : unchecked(now - _first));
        if (_disposed) return;
        Reset();
        if (_disposed) return;
        _state = player ? 2 : 3;
        if (player) _occurrence = unchecked(_occurrence + _cureDelay);
        _needs(player ? 0x19 : 0x18);
    }
    protected override bool ShouldTriggerBehaviorInternal(ReactionContext rc, IBehavior behavior)
    {
        if (_disposed) return false;
        var target = behavior as PlayAnimBehavior;
        bool feature = _feature();
        if (_disposed || !feature) return false;
        bool? unlocked = _unlocked();
        if (_disposed) return false;
        if (!unlocked.HasValue) { _missing("M10-003 Hiccup unlock state unavailable"); return false; }
        if (!unlocked.Value) { Reset(); return false; }
        bool disabled = _disabled; _disabled = false;
        if (_state >= 2)
        {
            if (target is null) return MissingTarget();
            target.ReplaceTriggers(_state == 2 ? new[] { AnimationTrigger.HiccupPlayerCure } :
                _state == 3 ? new[] { AnimationTrigger.HiccupSelfCure } : Array.Empty<AnimationTrigger>());
            bool runnable = target.IsRunnable(rc.Context);
            if (_disposed || !runnable) return false;
            _state = 0; _keepAlive(0.5f); return !_disposed;
        }
        uint now = _now();
        if (_disposed) return false;
        uint gateNow = _now();
        if (_disposed || gateNow <= _occurrence) return false;
        int severe = _severe();
        if (_disposed) return false;
        if (severe is 0 or 1) { Cure(false); return false; }
        _whiteboard.HiccupsActive = true;
        if (now <= _spacing) return false;
        _spacing = unchecked(now + Draw(_minSpacing, _maxSpacing));
        if (disabled) return false;
        uint old = _remaining; _remaining = unchecked(_remaining - 1);
        if (old == 0) { Cure(false); return false; }
        if (!_reported) { _reported = true; _report(true); }
        if (_disposed) return false;
        if (target is null) return MissingTarget();
        target.ReplaceTriggers(new[] { _remaining == unchecked(_total - 1) ? AnimationTrigger.HiccupGetIn : AnimationTrigger.Hiccup });
        bool canRun = target.IsRunnable(rc.Context);
        if (_disposed || !canRun) return false;
        if (_first == 0) _first = now;
        _needs(0x1A); return !_disposed;
    }
    private bool MissingTarget()
    {
        _missing("M1-034/M10-003 Hiccup target downcast 0x0061048C: skip null vector assignment");
        return false;
    }
    public override void EnabledStateChanged(BehaviorContext context, bool enabled)
    {
        if (_disposed || enabled) return;
        _disabled = true;
        int energy = _bracket(1); if (_disposed) return;
        int repair = _bracket(0); if (_disposed) return;
        uint now = _now(); if (_disposed) return;
        if (now > _occurrence && (energy == 3 || repair == 3)) Cure(false);
    }
    // E2G 53 and G2E 164; ingress is wired only by a real supported owner.
    internal void OffTreadsChanged(byte state)
    {
        if (_disposed || !IsReactionTriggerEnabled(Trigger)) return;
        uint now = _now(); if (_disposed || now <= _occurrence) return;
        if ((state == 5 || state == 2) && _state == 0) { _keepAlive(5f); if (!_disposed) _state = 1; }
        else if (state == 0 && _state == 1) Cure(true);
    }
    internal void Receive164()
    {
        if (_disposed) return;
        if (!IsReactionTriggerEnabled(Trigger))
        {
            _disabledInput();
            return;
        }
        uint now = _now(); if (_disposed) return;
        _occurrence = _spacing = now;
    }
    internal void AddSubscription(IDisposable subscription)
    {
        if (_disposed) subscription.Dispose(); else _subscriptions.Add(subscription);
    }
    internal void FinishConstruction()
    {
        if (!_disposed) NativeGlobal = this;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        uint now = _now();
        if (now > _occurrence) _das(false, _first == 0 ? 0 : unchecked(now - _first));
        NativeGlobal = null;
        foreach (var subscription in _subscriptions) subscription.Dispose();
    }
}
