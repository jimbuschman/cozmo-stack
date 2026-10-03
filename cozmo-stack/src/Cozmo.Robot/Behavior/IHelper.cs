namespace Cozmo.Robot.Behavior;

// fidelity: M8-011
/// <summary>
/// The engine's <c>IBSRunnable</c>, as far as <c>IHelper</c> derives from it: <c>IHelper::IHelper</c> 0x005b6428 calls
/// <c>IBSRunnable::IBSRunnable(name)</c> (PLT 0x4b1d40, 0x005b6430), and the name is the <c>std::string</c> at +0x20 that the component logs. Its six
/// vtable hooks (+0x08..+0x1c, pure in <c>IBSRunnable</c>, vtable 0x010252a0) are read by its wrapper functions: <c>Update</c> +0x0c (0x005a70b0),
/// <c>WantsToBeActivated</c> +0x10 (0x005a70ce), <c>OnActivated</c> +0x14 (0x005a70ea), <c>OnDeactivated</c> +0x18 (0x005a70fa) and
/// <c>LeftActivatableScope</c> +0x1c (0x005a7104). <c>IHelper</c>'s vtable (0x01025b78) gives them the bodies in <see cref="IHelper"/>.
/// </summary>
public abstract class BSRunnable
{
    protected BSRunnable(string name) { Name = name; }
    /// <summary>The runnable's name (+0x20).</summary>
    public string Name { get; }
}

// fidelity: M8-011
/// <summary>
/// <c>DelegateProperties</c>, the block embedded at <c>IHelper+0x60</c>: the delegate <c>shared_ptr&lt;IHelper&gt;</c> (+0x60/+0x64) and two handlers,
/// properties +0x08 (IHelper +0x68) and +0x20 (IHelper +0x80). <c>UpdateWhileActive</c> moves them into +0x30 and +0x48 and then
/// <c>ClearDelegateProperties()</c> (PLT 0x4b2148, body 0x005b62ec) nulls the delegate and empties both (inventory Correction A5, extraction 4.4/4.6).
/// </summary>
public sealed class DelegateProperties
{
    /// <summary>+0x60: the helper this one wants run on top of it.</summary>
    public HelperRef? Delegate { get; set; }
    /// <summary>Properties +0x08 (IHelper +0x68): moved into the success-result handler (+0x30).</summary>
    public Func<CozmoRobot, BehaviorStatus>? OnSuccessHandler { get; set; }
    /// <summary>Properties +0x20 (IHelper +0x80): moved into the failure-result handler (+0x48).</summary>
    public Func<CozmoRobot, BehaviorStatus>? OnFailureHandler { get; set; }

    /// <summary><c>DelegateProperties::ClearDelegateProperties</c> 0x005b62ec: nulls the delegate, destroys and empties both handlers.</summary>
    public void Clear()
    {
        Delegate?.Release();
        Delegate = null;
        OnSuccessHandler = null;
        OnFailureHandler = null;
    }

    /// <summary>
    /// <c>DelegateProperties::SucceedImmediatelyOnDelegateFailure</c> 0x005b6338 writes properties +0x08 with a functor whose body is
    /// <c>movs r0,#2; bx lr</c> (0x005b7606): it returns Complete. Only the field mapping is claimed, not the name's sense (the symbol and the mapping disagree);
    /// no caller exists in the engine.
    /// </summary>
    public void SucceedImmediatelyOnDelegateFailure() => OnSuccessHandler = _ => BehaviorStatus.Complete;

    /// <summary>
    /// <c>DelegateProperties::FailImmediatelyOnDelegateFailure</c> 0x005b63b0 writes properties +0x20 with a functor whose body is <c>movs r0,#0; bx lr</c>
    /// (0x005b7662): it returns Failure. Its engine callers are <c>DriveToHelper::RespondToDriveResult</c> 0x005b5e04 and <c>PickupBlockHelper::StartPickupAction</c> 0x005b7c44.
    /// </summary>
    public void FailImmediatelyOnDelegateFailure() => OnFailureHandler = _ => BehaviorStatus.Failure;
}

// fidelity: M8-011
/// <summary>
/// The engine's <c>IHelper</c> base (inventory Correction A5, extraction 1.4). Fields: +0x1c the status (constructed 2 = Complete, 0x005b6440), +0x20 the name, +0x2c
/// "init has run" (constructed 0), +0x30/+0x48 the success/failure result handlers (empty), +0x60 the <see cref="DelegateProperties"/>, +0x98 the start time
/// (a float, constructed 0), +0x9c the behaviour, +0xa0 the factory. The three subclass slots are +0x28 <see cref="ShouldCancelDelegates"/>, +0x2c
/// <see cref="Init"/> and +0x30 <see cref="UpdateWhileActiveInternal"/> (pure virtual in the base; the relocations 0x01025ba0, 0x01025ba4, 0x01025ba8). The six
/// concrete helper classes (DriveTo, PickupBlock, PlaceBlock, PlaceRelObject, RollBlock, SearchForBlock) are NOT built: their bodies are unread
/// (RECOVERABLE_GAP); each one's <c>ShouldCancelDelegates</c> returns 0 (0x005b57fc, 0x005b7b2a, 0x005b8ca6, 0x005b8fca, 0x005b9a12, 0x005bae9a).
/// </summary>
public abstract class IHelper : BSRunnable
{
    private readonly CozmoRobot _robot;
    private readonly SteppedBehavior _behavior;                       // +0x9c
    private BehaviorStatus _status = BehaviorStatus.Complete;         // +0x1c
    private bool _initHasRun;                                         // +0x2c
    private Func<CozmoRobot, BehaviorStatus>? _successHandler;        // +0x30 (manager +0x40)
    private Func<CozmoRobot, BehaviorStatus>? _failureHandler;        // +0x48 (manager +0x58)
    private float _startTime;                                         // +0x98

    /// <summary><c>IHelper::IHelper(string const&amp; name, Robot&amp;, IBehavior&amp;, BehaviorHelperFactory&amp;)</c> 0x005b6428..0x005b6490.</summary>
    protected IHelper(CozmoRobot robot, string name, SteppedBehavior behavior, BehaviorHelperFactory factory) : base(name)
    {
        _robot = robot;
        _behavior = behavior;
        Factory = factory;
    }

    /// <summary>+0xa0.</summary>
    public BehaviorHelperFactory Factory { get; }
    /// <summary>+0x60: the delegate and its two handlers.</summary>
    public DelegateProperties Delegation { get; } = new();
    /// <summary>+0x1c.</summary>
    public BehaviorStatus Status => _status;
    /// <summary>+0x2c.</summary>
    public bool InitHasRun => _initHasRun;
    /// <summary>+0x98 (float).</summary>
    public float StartTime => _startTime;
    /// <summary>Whether the +0x30 handler is stored (manager +0x40 non-null).</summary>
    public bool HasSuccessHandler => _successHandler is not null;
    /// <summary>Whether the +0x48 handler is stored (manager +0x58 non-null).</summary>
    public bool HasFailureHandler => _failureHandler is not null;

    // ---- the IBSRunnable hook slots, with IHelper's vtable contents (extraction 4.2)

    /// <summary>Slot +0x08: <c>bx lr</c> (0x005b5f0a).</summary>
    protected virtual void Slot08() { }
    /// <summary>Slot +0x0c (IBSRunnable::Update's hook): <c>movs r0,#2</c> (0x005b5f0c), so Complete.</summary>
    public virtual BehaviorStatus RunnableUpdate() => BehaviorStatus.Complete;
    /// <summary>Slot +0x10 (WantsToBeActivated's hook): <c>movs r0,#0</c> (0x005b5f10).</summary>
    public virtual bool RunnableWantsToBeActivated() => false;
    /// <summary>Slot +0x14 (OnActivated's hook): <c>bx lr</c> (0x005b5f14).</summary>
    protected virtual void RunnableOnActivated() { }
    /// <summary>Slot +0x18 (OnDeactivated's hook): <c>bx lr</c> (0x005b5f16).</summary>
    protected virtual void RunnableOnDeactivated() { }
    /// <summary>Slot +0x1c (LeftActivatableScope's hook): <c>bx lr</c> (0x005b5f18).</summary>
    protected virtual void RunnableLeftActivatableScope() { }

    /// <summary>Slot +0x20, called with no argument by <see cref="InitializeOnStack"/> (0x005b674e..0x005b6756); a no-op in the base (0x005b5f1a). The engine name is UNKNOWN.</summary>
    protected virtual void OnInitializeOnStackSlot20() { }
    /// <summary>Slot +0x24, called with the bool by <see cref="Stop"/> (0x005b67ee); a no-op in the base (0x005b5f1c). The engine name is UNKNOWN.</summary>
    protected virtual void OnStopSlot24(bool first) { }

    // ---- the three subclass slots

    /// <summary>Slot +0x28 <c>ShouldCancelDelegates(Robot const&amp;) const</c>.</summary>
    public abstract bool ShouldCancelDelegates(CozmoRobot robot);
    /// <summary>Slot +0x2c <c>Init(Robot&amp;)</c>.</summary>
    protected abstract BehaviorStatus Init(CozmoRobot robot);
    /// <summary>Slot +0x30 <c>UpdateWhileActiveInternal(Robot&amp;)</c>.</summary>
    protected abstract BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot);

    private void Log(string level, string channel, string id, string text) =>
        _robot.Engine.Log($"{level}: [{channel}] {id}: {text}");

    /// <summary>
    /// <c>IHelper::InitializeOnStack()</c> 0x005b6706: +0x2c = 0 (0x005b670c); +0x1c = 1 Running (0x005b6714); the +0x30 handler destroyed and emptied
    /// (0x005b6712); the +0x48 handler destroyed and emptied (0x005b672e); then slot +0x20 (0x005b674e). It does not clear the delegate or the +0x68/+0x80
    /// handlers. Its only caller is <c>PushHelperOntoStackAndUpdate</c> 0x0056dc34.
    /// </summary>
    public void InitializeOnStack()
    {
        _initHasRun = false;
        _status = BehaviorStatus.Running;
        _successHandler = null;
        _failureHandler = null;
        OnInitializeOnStackSlot20();
    }

    /// <summary>
    /// <c>IHelper::UpdateWhileActive(Robot&amp;, shared_ptr&lt;IHelper&gt;&amp; out)</c> 0x005b64b8, returning +0x1c. (a) With +0x2c set the status is
    /// <c>UpdateWhileActiveInternal</c> (0x005b64ce..0x005b64e2). (b) The first run logs the event "robot.behavior_helper.start" and "IHelper.Init" with the
    /// name, sets +0x2c = 1 and +0x98 = <c>GetCurrentTimeInSeconds()</c> (0x005b6570..0x005b657e), and the status is <c>Init</c> (0x005b6584..0x005b658c);
    /// (c) a Running status with a null delegate runs <c>UpdateWhileActiveInternal</c> in the same call, a non-null delegate skips it (0x005b658e..0x005b65aa).
    /// (d) A non-null delegate is copied to <paramref name="sub"/>, the +0x68 handler is moved into +0x30 and the +0x80 handler into +0x48, and the delegate
    /// properties are cleared (0x005b65d6..0x005b663c).
    /// </summary>
    public BehaviorStatus UpdateWhileActive(CozmoRobot robot, out HelperRef? sub)
    {
        sub = null;
        if (_initHasRun)
        {
            _status = UpdateWhileActiveInternal(robot);
        }
        else
        {
            Log("debug", "Events", "robot.behavior_helper.start", Name);       // sEventF (no DAS sink in this stack)
            Log("info", "BehaviorHelpers", "IHelper.Init", Name);
            _initHasRun = true;
            _startTime = robot.Engine.Timer.SecondsF;
            _status = Init(robot);
            if (_status == BehaviorStatus.Running && Delegation.Delegate is null)
                _status = UpdateWhileActiveInternal(robot);
        }
        if (Delegation.Delegate is { } delegateRef)
        {
            sub = delegateRef.Copy();
            _successHandler = Delegation.OnSuccessHandler;
            _failureHandler = Delegation.OnFailureHandler;
            Delegation.Clear();
        }
        return _status;
    }

    /// <summary>
    /// <c>IHelper::OnDelegateSuccess(Robot&amp;)</c> 0x005b6b94: logs "IHelper.OnDelegateSuccess"; a stored +0x30 handler is called and its result stored in
    /// +0x1c (0x005b6be6..0x005b6bfa); then the +0x30 handler is destroyed and emptied (0x005b6c12/0x005b6c16); returns +0x1c, unchanged when there was no handler.
    /// </summary>
    public BehaviorStatus OnDelegateSuccess(CozmoRobot robot)
    {
        Log("debug", "BehaviorHelpers", "IHelper.OnDelegateSuccess", Name);
        if (_successHandler is { } handler) _status = handler(robot);
        _successHandler = null;
        return _status;
    }

    /// <summary>
    /// <c>IHelper::OnDelegateFailure(Robot&amp;)</c> 0x005b6ca4: logs "IHelper.OnDelegateFailure"; a stored +0x48 handler is called and its result stored in
    /// +0x1c (0x005b6cf6..0x005b6d04); then, as read at 0x005b6d06..0x005b6d22 (which reads <c>[r4,#0x40]</c>), the handler at +0x30 - not +0x48 - is destroyed
    /// and emptied; returns +0x1c.
    /// </summary>
    public BehaviorStatus OnDelegateFailure(CozmoRobot robot)
    {
        Log("info", "BehaviorHelpers", "IHelper.OnDelegateFailure", Name);
        if (_failureHandler is { } handler) _status = handler(robot);
        _successHandler = null;
        return _status;
    }

    /// <summary><c>IHelper::IsActing()</c> 0x005b6758: <c>[[this+0x9c]+0x84] != 0</c>, the behaviour's current-action handle.</summary>
    public bool IsActing() => _behavior.HasCurrentAction;

    /// <summary><c>IHelper::StopActing(bool)</c> 0x005b6ec2: <c>IBehavior::StopActing(keepAction, 1)</c> on the behaviour (0x005b6ec6 <c>movs r2,#1</c>).</summary>
    public void StopActing(bool keepAction) => _behavior.StopActingFromHelper(keepAction);

    /// <summary>
    /// <c>IHelper::Stop(bool first)</c> 0x005b6768: logs "IHelper.Stop" with the name, <paramref name="first"/> and <see cref="IsActing"/> (0x005b6788..0x005b67a4);
    /// <c>LogStopEvent(first)</c> (0x005b67d0); when <paramref name="first"/> is true and the behaviour has a current action, <c>IBehavior::StopActing(false, true)</c>
    /// (0x005b67d4..0x005b67e6: viaCallback true, so the behaviour's own helper is not stopped); then slot +0x24 with <paramref name="first"/> (0x005b67ee).
    /// </summary>
    public void Stop(bool first)
    {
        Log("info", "BehaviorHelpers", "IHelper.Stop", $"{Name} isActive={(first ? 1 : 0)}, IsActing={(IsActing() ? 1 : 0)}");
        LogStopEvent(first);
        if (first && _behavior.HasCurrentAction) _behavior.StopActingFromHelper(keepAction: false);
        OnStopSlot24(first);
    }

    /// <summary>
    /// <c>IHelper::LogStopEvent(bool)</c> 0x005b6850: the event by +0x1c, 0 "robot.behavior_helper.failure", 2 "robot.behavior_helper.success", 1 "...cancel" when
    /// the bool is 1 else "...inactive_stop" (0x005b68a0..0x005b68c2), with a duration from +0x98; a negative duration is the error "IHelper.Stop.InvalidTime". The
    /// event argument layout and the duration's exact expression are not in the inventory: this stack logs <c>now - startTime</c> in float.
    /// </summary>
    private void LogStopEvent(bool first)
    {
        string? name = _status switch
        {
            BehaviorStatus.Failure => "robot.behavior_helper.failure",
            BehaviorStatus.Complete => "robot.behavior_helper.success",
            BehaviorStatus.Running => first ? "robot.behavior_helper.cancel" : "robot.behavior_helper.inactive_stop",
            _ => null,
        };
        if (name is null) return;
        float duration = _robot.Engine.Timer.SecondsF - _startTime;
        if (duration < 0f) { Log("error", "BehaviorHelpers", "IHelper.Stop.InvalidTime", Name); return; }
        Log("debug", "Events", name, $"{Name} {duration}");
    }
}
