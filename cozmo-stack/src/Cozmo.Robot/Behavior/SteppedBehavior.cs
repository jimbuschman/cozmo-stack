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

    // TriggerAnimationAction's timeout_s (IAction+0x74, written by the constructor). The engine passes
    // 60.0 for the reaction animations; null means the engine's "no timeout".
    private double _actionTimeoutMs = double.NaN;
    private double _actionDeadlineMs = double.NaN;
    private Action? _onActionTimeout;

    private volatile bool _finished = true;

    // The engine's StopActing(false, false) cancels the action without its callback. Each started action
    // remembers the epoch it began in; a stop advances the epoch and the stale completion is dropped.
    private int _actionEpoch;

    // The IBehavior engine fields the M8 rows name, kept beside the stack's own run state so the
    // lifecycle transitions are reproducible and testable. IBehavior +0xa1 running, +0xa0 acting-without-
    // action, +0x34 running-penalty clock, +0x114 resume counter, +0x118 resume suppression, +0x80
    // score-increase counter, +0xd8 spark gate, +0x104 running-score bonus.
    private bool _engineRunning;
    private bool _engineActingFlag;
    private double _runningPenaltyClockSec;
    private int _resumeCount;
    private double _resumeSuppressionUntilSec;
    private int _scoreIncreaseCount;
    private bool _sparkDisabled;
    private bool _resuming;
    // IBehavior +0x84: the current-action handle is set. IBehavior +0x98: a shared handle Init/StopOnNextActionComplete release.
    private bool _currentAction;
    private object? _sharedHandle;

    protected SteppedBehavior(string id, string behaviorClass)
    {
        Id = id;
        Class = behaviorClass;
    }

    public string Id { get; }
    public string Class { get; }

    /// <summary>M8-004 gap: the engine's default is zero; this in-code score is used only by
    /// <see cref="BehaviorManager.ChooseAndSwitch"/> until the chooser path is wired (M8-013).</summary>
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
        context.Robot.Animations.Library is not null && IsRunnableBase(context) && IsRunnableInternal(context);

    /// <summary>The engine's <c>IsRunnableInternal</c> (the <c>vtable+0x50</c> gate). Most reaction classes just return true.</summary>
    protected virtual bool IsRunnableInternal(BehaviorContext context) => true;

    // ============================================================== the IBehavior engine lifecycle (M8-001)

    /// <summary>
    /// <c>IBehavior::IsRunnableBase</c> 0x005bd778: the running flag <c>+0xa1</c> short-circuits to true
    /// (<c>ldrb.w r0,[r4,#0xa1]</c> 0x005bd780); otherwise the required AI process (<c>+0x1c</c> against
    /// <c>robot+0x264+8</c>, <c>AIInformationAnalyzer::IsProcessRunning</c> 0x005bd7de), the robot state
    /// byte <c>+0x74</c> against 3 or <c>robot+0x264+0x30+0x14</c> (0x005bd7e6..0x005bd7f6), the unlock id
    /// <c>+0x70</c> (0x55 bypasses) through <c>ProgressionUnlockComponent::IsUnlocked(robot+0x448, id,
    /// true)</c> (0x005bd810), the float timers <c>+0x78</c>/<c>+0x7c</c> (0x005bd81a..), the wants-to-run
    /// strategy <c>+0x38</c> (0x005bd8a4), and finally <c>now &gt;= +0x118</c> (0x005bd8b4..0x005bd8c8).
    /// The robot/AI/progression inputs are cross-layer seams; a null seam means "no such requirement".
    /// </summary>
    // fidelity: M8-001
    public bool IsRunnableBase(BehaviorContext context)
    {
        if (_engineRunning) return true;                                          // +0xa1
        if (RequiredProcessRunning is { } process && !process()) return false;    // +0x1c
        if (RobotStateAllowsRun is { } state && !state()) return false;           // +0x74
        if (UnlockAllowsRun is { } unlock && !unlock()) return false;             // +0x70
        if (RecentTimersAllowRun is { } timers && !timers()) return false;        // +0x78/+0x7c
        // The three robot-byte gates and their behaviour virtuals (decomp 0x005bd864..0x005bd89c):
        // robot+0x355 -> vtable+0x20, robot+0x34a -> vtable+0x24, robot+0x284->+8 != -1 -> vtable+0x28.
        if (RobotState355?.Invoke() == true && !RunnableGate20(context)) return false;
        if (RobotState34a?.Invoke() == true && !RunnableGate24(context)) return false;
        if (RobotComponent284?.Invoke() == true && !RunnableGate28(context)) return false;
        if (WantsToRunAllowsRun is { } wants && !wants()) return false;           // +0x38
        double now = context.ClockSec?.Invoke() ?? Clock() / 1000.0;
        return now >= _resumeSuppressionUntilSec;                                 // +0x118
    }

    /// <summary>The AI process seam (<c>+0x1c</c> against <c>robot+0x264+8</c>); null = no required process.</summary>
    public Func<bool>? RequiredProcessRunning { get; set; }
    /// <summary>The robot-state seam (<c>+0x74</c> against 3 or the AI state); null = allow.</summary>
    public Func<bool>? RobotStateAllowsRun { get; set; }
    /// <summary>The progression-unlock seam (<c>+0x70</c> through <c>IsUnlocked</c>); null = allow.</summary>
    public Func<bool>? UnlockAllowsRun { get; set; }
    /// <summary>The recent-event timer seam (<c>+0x78</c>/<c>+0x7c</c>); null = allow.</summary>
    public Func<bool>? RecentTimersAllowRun { get; set; }
    /// <summary>The wants-to-run strategy seam (<c>+0x38</c>); null = allow.</summary>
    public Func<bool>? WantsToRunAllowsRun { get; set; }
    /// <summary>Robot byte <c>+0x355</c> (0x005bd864); when true the <c>vtable+0x20</c> gate must pass. Null = not modelled.</summary>
    public Func<bool>? RobotState355 { get; set; }
    /// <summary>Robot byte <c>+0x34a</c> (0x005bd876); when true the <c>vtable+0x24</c> gate must pass. Null = not modelled.</summary>
    public Func<bool>? RobotState34a { get; set; }
    /// <summary>Robot component <c>+0x284</c> whose <c>+8</c> is checked (0x005bd888); when true the <c>vtable+0x28</c> gate must pass. Null = not modelled.</summary>
    public Func<bool>? RobotComponent284 { get; set; }
    /// <summary>The <c>vtable+0x20</c> runnable gate; the engine calls it when robot+0x355 is set.</summary>
    protected virtual bool RunnableGate20(BehaviorContext context) => true;
    /// <summary>The <c>vtable+0x24</c> runnable gate; the engine calls it when robot+0x34a is set.</summary>
    protected virtual bool RunnableGate24(BehaviorContext context) => true;
    /// <summary>The <c>vtable+0x28</c> runnable gate; the engine calls it when robot+0x284->+8 is not -1.</summary>
    protected virtual bool RunnableGate28(BehaviorContext context) => true;

    /// <summary>IBehavior +0xa1: the engine's running flag, as Init/Stop/Resume set it.</summary>
    public bool EngineRunning => _engineRunning;
    /// <summary>
    /// The <c>IBehavior::Init</c> failure signal (0x005a1eae "BehaviorManager.SetCurrentBehavior.InitFailed").
    /// The engine's <c>Init</c> returns a bool; the stack's <c>StartAsync</c> does not, so an M7/M15 class
    /// whose Init can fail sets this. Nothing in M8 sets it.
    /// </summary>
    public bool InitFailed { get; set; }
    /// <summary>IBehavior +0xd8: the spark gate Init computed (from the AI process).</summary>
    public bool SparkDisabled => _sparkDisabled;
    /// <summary>IBehavior +0x80: bumped by Init when <see cref="IsRunnableInternal"/> is zero.</summary>
    public int ScoreIncreaseCount => _scoreIncreaseCount;
    /// <summary>IBehavior +0x34: the running-penalty clock, stamped by Init and the Resume normal path.</summary>
    public double RunningPenaltyClockSec => _runningPenaltyClockSec;
    /// <summary>IBehavior +0x114: how many CliffDetected/UnexpectedMovement resumes have been counted.</summary>
    public int ResumeCount => _resumeCount;
    /// <summary>IBehavior +0x118: the TooManyResumesCliffOrMovement suppression stamp.</summary>
    public double ResumeSuppressionUntilSec => _resumeSuppressionUntilSec;
    /// <summary>IBehavior +0x104: the running-score bonus <see cref="IncreaseScoreWhileActing"/> adds to.</summary>
    public double RunningScoreBonus { get; private set; }
    /// <summary>IBehavior +0xa2: whether <see cref="Resume"/> is inside its normal path.</summary>
    public bool ResumeInProgress => _resuming;
    /// <summary>IBehavior +0x84: whether a current-action handle is set.</summary>
    public bool HasCurrentAction => _currentAction;

    /// <summary>
    /// <c>IBehavior::IncreaseScoreWhileActing</c> 0x005bf02c: adds <paramref name="amount"/> to +0x104 only
    /// while the current-action handle +0x84 is non-zero (<c>ldr.w r2,[r0,#0x84]</c> 0x005bf02c;
    /// <c>cbz r2,#0x5bf042</c> 0x005bf030; <c>vadd.f32</c>/<c>vstr</c> 0x005bf03a/0x005bf03e). Its only
    /// engine callers are M7/M15 concrete behaviours (KnockOverCubes 10.0f, PopAWheelie/PutDownBlock/
    /// RollBlock/StackBlocks 0.8f/5.0f, and the FUN_0059ec54 helper); nothing in M8 calls it.
    /// </summary>
    // fidelity: M8-003
    public void IncreaseScoreWhileActing(double amount)
    {
        if (_currentAction) RunningScoreBonus += amount;   // +0x84 non-zero
    }

    /// <summary>
    /// <c>IBehavior::ScoredActingStateChanged</c> 0x005bf044 clears +0x104 (<c>movs r1,#0</c>;
    /// <c>str.w r1,[r0,#0x104]</c>). It has <b>no</b> engine caller (exhaustive scan), so this is the
    /// exported function only and nothing wires it.
    /// </summary>
    // fidelity: M8-003
    public void ScoredActingStateChanged(bool acting) => RunningScoreBonus = 0;

    /// <summary>IBehavior +0x10c: this behaviour's objective (from its config), default 0x29 invalid.</summary>
    public BehaviorObjective BehaviorObjective { get; set; } = BehaviorObjective.Invalid;

    /// <summary>
    /// <c>IBehavior::HandleBehaviorObjective</c> 0x005bf00c: when this behaviour's <c>+0x10c</c> is not
    /// 0x29 and the achieved objective equals it (<c>cmp r1,r0</c> 0x005bf01a), stamp the <b>last-run</b>
    /// clock <c>+0x30 = now</c> (<c>str r0,[r4,#0x30]</c> 0x005bf028) - the stamp
    /// <c>EvaluateRepetitionPenalty</c> 0x005beee6 reads. Returns whether it stamped.
    /// </summary>
    // fidelity: M8-003
    public bool HandleBehaviorObjective(BehaviorObjective achieved, double nowSec)
    {
        if (BehaviorObjective == BehaviorObjective.Invalid || achieved != BehaviorObjective) return false;
        Context?.Penalty?.Ran(Id, nowSec);   // +0x30 = now (str r0,[r4,#0x30] 0x005bf028)
        return true;
    }

    /// <summary>
    /// <c>IBehavior::IsRunnableScored</c> 0x005bda28: 1 when <c>now &gt;= +0x118</c>, else 0
    /// (<c>vldr s0,[r4,#0x118]</c> 0x005bda38; <c>vcmpe.f32</c> 0x005bda3e; <c>movpl r0,#1</c> 0x005bda48).
    /// It has <b>no</b> engine caller and is not in the IBehavior vtable, so no chooser and not the manager
    /// consults it; this is the exported function only.
    /// </summary>
    // fidelity: M8-001
    public bool IsRunnableScored(double nowSec) => nowSec >= _resumeSuppressionUntilSec;

    /// <summary>
    /// <c>IBehavior::StopWithoutImmediateRepetitionPenalty</c> 0x005beea0: stamp +0x108 = now + 1.0 on the
    /// shared repetition history. Only the M7/M15 concrete behaviours call this; nothing in M8 does.
    /// </summary>
    // fidelity: M8-002
    public void StopWithoutImmediateRepetitionPenalty()
    {
        double now = Context.ClockSec?.Invoke() ?? Clock() / 1000.0;
        Context.Penalty?.StopWithoutImmediateRepetitionPenalty(Id, now);
    }

    /// <summary>
    /// The engine's <c>IBehavior::Init</c> 0x005bcb54, on the parts this stack models. The spark gate +0xd8
    /// comes from the AI process (<c>robot+0x44-&gt;+0x58</c>/<c>+0x5c</c>) and is an interface to that
    /// layer; <c>vtable+0x48</c> is the <see cref="IsRunnableInternal"/> seam; the SparkBehaviorDisables
    /// lock is <see cref="SparkBehaviorDisables"/>.
    /// </summary>
    // fidelity: M8-001
    protected void InitLifecycle(BehaviorContext context, double nowSec, bool sparkDisabled)
    {
        _sparkDisabled = sparkDisabled;          // +0xd8
        _engineActingFlag = false;               // halfword 0x0100 at +0xa0: +0xa0 = 0
        _engineRunning = true;                   //                             +0xa1 = 1
        _runningPenaltyClockSec = nowSec;        // +0x34 = now
        _sharedHandle = null;                    // +0x98 cleared (releases whatever it held)
        if (IsRunnableInternal(context)) _engineRunning = false; else _scoreIncreaseCount++;   // +0x80
        SparkBehaviorDisables();                 // the "SparkBehaviorDisables" lock
        _resumeCount = 0;                        // +0x114 = 0
    }

    /// <summary>
    /// The <c>SparkBehaviorDisables</c> lock seam: the engine takes it when the unlock id +0x70 is not 0x55
    /// and equals <c>robot+0x44-&gt;+0x58</c>. The progression/AI-process layer owns those values.
    /// </summary>
    protected virtual void SparkBehaviorDisables() { }

    /// <summary>
    /// <c>IBehavior::Update</c> 0x005bd074: returns 2 when byte +0xa0 is set and the current-action handle
    /// +0x84 is zero (<c>cbz r1</c> 0x005bd078; <c>cbz r1</c> 0x005bd07e; <c>movs r0,#2</c> 0x005bd088),
    /// else 0 for the update seam. The manager maps 2 to FinishCurrentBehavior.
    /// </summary>
    // fidelity: M8-001
    public int UpdateStatus() => _engineActingFlag && !_currentAction ? 2 : 0;   // +0xa0 && +0x84 == 0

    /// <summary>
    /// <c>IBehavior::Resume</c> 0x005bceac. Returns true when the TooManyResumesCliffOrMovement path fired
    /// (the caller must not resume); false for the normal path. Trigger 0 (CliffDetected) and 0x14
    /// (UnexpectedMovement) count against +0x114 (<c>cmp r5,#0x14</c> 0x005bcf16; <c>cmpne r5,#0</c>
    /// 0x005bcf1a); only when the pre-increment value is >= 1 does it set +0x118 = now + 15.0 and raise
    /// <see cref="TooManyResumesCliffOrMovement"/> (<c>vstr s0,[r4,#0x118]</c> 0x005bcf4c). The normal path
    /// sets +0xa2 = 1, stamps +0x34 = now, calls <c>vtable+0x4c</c> (<see cref="ResumeInternal"/>), clears
    /// +0xa2, and on a zero result sets +0xa1 = 1 and takes the SparkBehaviorDisables lock; on a non-zero
    /// result clears +0xa1 (0x005bcf80..0x005bcfde).
    /// </summary>
    // fidelity: M8-001
    public bool Resume(ReactionTrigger trigger, double nowSec)
    {
        if (trigger is ReactionTrigger.CliffDetected or ReactionTrigger.UnexpectedMovement)
        {
            int prior = _resumeCount;
            _resumeCount = prior + 1;
            if (prior >= 1)
            {
                _resumeSuppressionUntilSec = nowSec + 15.0;
                TooManyResumesCliffOrMovement?.Invoke();
                return true;
            }
        }
        _resuming = true;                        // +0xa2 = 1
        _runningPenaltyClockSec = nowSec;        // +0x34 = now
        int result = ResumeInternal();           // vtable+0x4c
        _resuming = false;                       // +0xa2 = 0
        if (result == 0)
        {
            _engineRunning = true;               // +0xa1 = 1
            SparkBehaviorDisables();             // Spark lock when +0x70 == robot+0x44->+0x58
        }
        else
        {
            _engineRunning = false;              // +0xa1 = 0
        }
        return false;
    }

    /// <summary>Raised on the TooManyResumesCliffOrMovement path (MoodManager::TriggerEmotionEvent).</summary>
    public event Action? TooManyResumesCliffOrMovement;

    /// <summary>
    /// The <c>vtable+0x4c ResumeInternal</c> seam; returns the engine's int (0 = resumed, non-zero =
    /// failed). The M7/M15 classes override it.
    /// </summary>
    protected virtual int ResumeInternal() => 0;

    /// <summary>
    /// <c>IBehavior::StopOnNextActionComplete</c> 0x005bd624..0x005bd6bb: sets the acting flag
    /// <c>+0xa0 = 1</c> (<c>strb.w r1,[r4,#0xa0]</c> 0x005bd698) and releases the <c>+0x98</c> handle
    /// (0x005bd694/0x005bd6b4). Its callers are the M7/M15 concrete behaviours; <c>UpdateStatus</c> then
    /// returns 2 once the current action handle <c>+0x84</c> is clear.
    /// </summary>
    // fidelity: M8-001
    public void StopOnNextActionComplete()
    {
        _engineActingFlag = true;   // +0xa0 = 1 (0x005bd698)
        _sharedHandle = null;       // +0x98 released (0x005bd694/0x005bd6b4)
    }

    /// <summary>IBehavior +0x98: the shared handle Init and StopOnNextActionComplete release.</summary>
    public object? SharedHandle => _sharedHandle;

    /// <summary>Sets the +0x98 handle (nothing in M8 sets it; the engine's action code does).</summary>
    protected void SetSharedHandle(object? handle) => _sharedHandle = handle;

    /// <summary>
    /// The resume entry: sets up the run without <see cref="InitLifecycle"/>, so the +0x114 counter and the
    /// other engine fields survive a resume. The engine's <c>BehaviorManager::TryToResumeBehavior</c>
    /// (0x005a2c76) calls <c>IBehavior::Resume</c> and does not call <c>IBehavior::Init</c>.
    /// </summary>
    // fidelity: M8-001
    public Task ResumeAsync(BehaviorContext context, BehaviorScope scope, CancellationToken cancel)
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
        InitLifecycle(context, context.ClockSec?.Invoke() ?? Clock() / 1000.0, SparkGate?.Invoke() ?? false);
        OnStart();
        if (!_finished && !Busy && !KeepsRunningWithoutAction) _finished = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// The spark-gate input (<c>robot+0x44-&gt;+0x58</c>/<c>+0x5c</c>), an interface to the AI/progression
    /// layer. Null means the gate reads false, which is what a robot with no AI process attached means.
    /// </summary>
    public Func<bool>? SparkGate { get; set; }

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

        // TriggerAnimationAction's timeout: IAction::IsDone returns true once now passes +0x74. The
        // deadline is armed on the first tick after the play starts.
        if (!_finished && _acting && _onActionTimeout is { } timeoutDone && !double.IsNaN(_actionTimeoutMs))
        {
            lock (_gate)
            {
                if (double.IsNaN(_actionDeadlineMs)) _actionDeadlineMs = nowMs + _actionTimeoutMs;
                if (nowMs >= _actionDeadlineMs)
                {
                    _onActionTimeout = null; _actionTimeoutMs = double.NaN; _actionDeadlineMs = double.NaN;
                    Log("action timed out");
                    StopActing();
                    _pending.Enqueue(timeoutDone);
                }
            }
        }

        if (!_finished && !Busy && !KeepsRunningWithoutAction) _finished = true;
        return !_finished;
    }

    public void Stop(BehaviorStopReason reason)
    {
        _finished = true;
        _engineRunning = false;                  // IBehavior::Stop clears +0xa1 (0x005bd10c)
        OnStop(reason);                          // vtable+0x54 StopInternal (0x005bd110/0x005bd114)
        StopActing(keepAction: false, viaCallback: false);   // IBehavior::Stop calls StopActing(0,0) 0x005bd126
    }

    /// <summary>Ends the behaviour of its own accord: the engine's behaviour running out of actions.</summary>
    protected void Finish()
    {
        if (_finished) return;
        _finished = true;
        Log("done");
    }

    /// <summary>
    /// The engine's <c>IBehavior::StopActing(keepAction, viaCallback)</c> 0x005bd34c: calls
    /// <c>vtable+0x80(0)</c> (<c>blx r2</c> 0x005bd360); when <c>viaCallback</c> is false and a helper is
    /// live it logs "Stopping behavior helper because action stopped without callback" and calls
    /// <c>StopHelperWithoutCallback</c> (0x005bd39c/0x005bd3d6); then, if the current-action handle
    /// <c>+0x84</c> is set, it cancels it in <c>ActionList</c> (0x005bd3f0) and clears <c>+0x84</c> unless
    /// <paramref name="keepAction"/> (0x005bd3e0/0x005bd3e4). This stack cancels by advancing the action
    /// epoch and dropping the queued completion; the helper path is the unowned BehaviorHelperComponent.
    /// </summary>
    // fidelity: M8-001
    protected void StopActing(bool keepAction, bool viaCallback)
    {
        ClearWaits();
        lock (_gate) _actionEpoch++;
        while (_pending.TryDequeue(out _)) { }
        StopOwnAnimation();                                  // vtable+0x80(0)
        if (_currentAction && !keepAction) _currentAction = false;   // +0x84 cleared unless keepAction
        if (!viaCallback) StopHelperWithoutCallback();
    }

    /// <summary>The parameterless convenience: <c>StopActing(0,0)</c>.</summary>
    protected void StopActing() => StopActing(keepAction: false, viaCallback: false);

    /// <summary>
    /// The helper-without-callback seam (0x005bd3d6). The helper component is unowned by any record, so
    /// the default does nothing; a concrete M7/M15 class that has one overrides it.
    /// </summary>
    protected virtual void StopHelperWithoutCallback() { }

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
    // fidelity: M8-007
    protected void PlayTrigger(AnimationTrigger trigger, Action onDone, AnimationTrack alsoLock = AnimationTrack.None,
                               double? timeoutSec = null)
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
        var lockTracks = clip.Tracks | alsoLock;
        byte mask = CozmoMotion.MaskFor(lockTracks);
        if (mask != 0 && Context.Robot.Motion.AreAnyTracksLocked(mask))
        {
            // IActionRunner::Update 0x00540370: AreAnyTracksLocked 0x00540440; the action stays queued and is
            // retried next tick, logging "Action %s [%d] not running because required tracks are locked"
            // (0x005404a8).
            Log($"{trigger} -> {resolved.Selected}: Action not running because required tracks are locked");
            _pending.Enqueue(() => PlayTrigger(trigger, onDone, alsoLock, timeoutSec));
            return;
        }

        var ticket = Context.Robot.Animations.PlayTracked(resolved.Selected!, lockTracks: lockTracks);
        if (ticket is null)
        {
            // A track it needs is owned. The engine waits: IActionRunner::Update leaves the action queued
            // and tries again next tick rather than failing it, so the play is deferred, not skipped.
            Log($"{trigger} -> {resolved.Selected}: a track it needs is owned; waiting for it");
            _pending.Enqueue(() => PlayTrigger(trigger, onDone, alsoLock, timeoutSec));
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
            _currentAction = true;               // +0x84 set
            epoch = _actionEpoch;
            // TriggerAnimationAction's timeout_s: the deadline is computed on the first Update after the
            // play starts, because the engine starts it on the manager's clock.
            _actionTimeoutMs = timeoutSec is { } t ? t * 1000 : double.NaN;
            _actionDeadlineMs = double.NaN;
            _onActionTimeout = timeoutSec is null ? null : onDone;
        }
        ticket.Completion.ContinueWith(_ =>
        {
            bool current;
            lock (_gate)
            {
                current = epoch == _actionEpoch;
                if (current)
                {
                    _owns = false; _acting = false; _currentAction = false;   // +0x84 cleared on completion
                    _onActionTimeout = null; _actionTimeoutMs = double.NaN; _actionDeadlineMs = double.NaN;
                }
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
    /// <c>CalibrateMotorAction::CheckIfDone</c> 0x00547D38 stays running until the motors it was asked for
    /// report calibrated, testing <c>Robot::IsHeadCalibrated</c> and <c>IsLiftCalibrated</c> against the
    /// two request flags at +0x78 and +0x79 and the two already-started flags at +0x7A and +0x7B.
    ///
    /// The action does time out, at 30.0 s. <c>IAction::IAction</c> 0x00540C44 writes -1.0f to +0x74
    /// (<c>movs r1, #0</c> 0x00540C9E / <c>movt r1, #0xbf80</c> 0x00540CA2 / <c>str r1,[r4,#0x74]</c>
    /// 0x00540CA6), but that is the "timer not started" sentinel: <c>IAction::UpdateInternal</c>
    /// 0x00540D1C arms it (0x00540D52/0x00540D5A/0x00540D64) and computes the deadline as
    /// <c>start + vtable[+0x2c]</c> (0x00540D94/0x00540D9E/0x00540DA2). <c>CalibrateMotorAction</c>
    /// inherits the default slot +0x2c (vtable vptr 0x0102194C, slot 0x01021978 -> thunk 0x0052B0C2)
    /// which returns <c>0x41F00000</c> = 30.0f, and on expiry the action fails with 0x03000018
    /// ("IAction.Update.TimedOut" 0x00540FE0 / "%s timed out after %.1f seconds." 0x00540FF8).
    /// </summary>
    // fidelity: M8-008
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
            Log(ok ? "head calibration reported complete"
                   : $"head calibration timed out after {CalibrationAllowanceSec:F1} seconds");
            onDone();
        }, "head calibration");
    }

    /// <summary>
    /// The engine's <c>CalibrateMotorAction</c> timeout: the inherited <c>IAction</c> vtable[+0x2c] value
    /// 30.0 s (thunk 0x0052B0C2 returns 0x41F00000). See <see cref="CalibrateHead"/>.
    /// </summary>
    public const double CalibrationAllowanceSec = 30.0;

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
