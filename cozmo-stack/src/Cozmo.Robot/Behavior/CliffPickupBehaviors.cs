using Cozmo.Robot.Animation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// <c>BehaviorReactToCliff</c> (constructor 0x00604CD0 .. StopInternal 0x006053FC), the class the shipped map runs for
/// <c>ReactionTrigger.CliffDetected</c>. The state machine, from the gap pass (R-BEH2 M7 gap1, section 5.1) and the pre-extraction
/// check, in the engine's order:
/// <list type="bullet">
/// <item>fields: +0x11C u32 state, +0x120 u8 react-now, +0x121 u8 copy of the detected byte, +0x122 u16 saved cliff-detect threshold,
/// +0x124 u8 quit flag, +0x125 u8 finish flag; <c>IBehavior+0xD9</c> is the config key <c>alwaysStreamline</c> (0x005BC200..0x005BC21E; absent from
/// <c>reactToCliff.json</c>, so false) and <c>IBehavior+0xD8</c> the hard-spark flag <c>Init</c> computes (0x005BCCAA..0x005BCCC2);</item>
/// <item><c>InitInternal</c> 0x00604D50: the mood event <c>"CliffReact"</c>, the lock table 0x00C73746, then a switch on +0x11C: 0 saves the threshold, builds
/// a <c>WaitForLambdaAction</c> (timeout FLT_MAX) and starts it with <c>TransitionToPlayingStopReaction</c> or, when
/// <c>[[[robot+0x264]+0x30]+0x14] &lt; 2</c>, <c>TransitionToPlayingCliffReaction</c>; 1 sets +0x120 and calls <c>TransitionToPlayingCliffReaction</c>;
/// any other value is the error <c>"BehaviorReactToCliff.Init.InvalidState"</c> and returns 1;</item>
/// <item><c>TransitionToPlayingStopReaction</c> 0x00604F64, <c>TransitionToPlayingCliffReaction</c> 0x006050F0, <c>TransitionToBackingUp</c> 0x00605318,
/// <c>SendFinishedReactToCliffMessage</c> 0x006052BC, <c>UpdateInternal</c> 0x00605408, <c>StopInternal</c> 0x006053FC,
/// <c>HandleWhileRunning</c> 0x00605580, <c>HandleWhileNotRunning</c> 0x0060541C.</item>
/// </list>
/// The pieces that are other layers' stay seams that say so: <see cref="BehaviorContext.AiExpressedNeedValue"/> (the value
/// <c>[[[robot+0x264]+0x30]+0x14]</c>: unset, it is the constructed 3 and reported once), <see cref="DriveStraight"/> (M13's
/// <c>DriveStraightAction</c>) and the game broadcast of <c>RobotCliffEventFinished</c>.
/// </summary>
// fidelity: M7-019, M7-021
public sealed class ReactToCliffBehavior : SteppedBehavior
{
    /// <summary>The cliff animation's timeout, 60.0f (<c>movt r0,#0x4270</c> 0x00604FD4; 0x42700000).</summary>
    public static readonly float AnimationTimeout = BitConverter.Int32BitsToSingle(0x42700000);
    /// <summary>The stop reaction's wait-for-lambda timeout, 0.55f (<c>movw r3,#0xcccd</c> 0x00605026, <c>movt r3,#0x3f0c</c> 0x0060502C: 0x3F0CCCCD).</summary>
    public static readonly float StopWaitTimeout = BitConverter.Int32BitsToSingle(0x3F0CCCCD);
    /// <summary>The back-up distance, -60.0f (<c>movt r2,#0xc270</c> 0x00605340: 0xC2700000).</summary>
    public static readonly float BackUpDistanceMm = BitConverter.Int32BitsToSingle(unchecked((int)0xC2700000));
    /// <summary>The back-up speed, 100.0f (<c>movt r3,#0x42c8</c> 0x00605346: 0x42C80000).</summary>
    public static readonly float BackUpSpeedMmps = BitConverter.Int32BitsToSingle(0x42C80000);
    /// <summary>The wait-for-lambda timeout of Init's state 0, FLT_MAX (<c>movt r3,#0x7f7f</c> 0x00604E08: 0x7F7FFFFF).</summary>
    public static readonly float InitWaitTimeout = BitConverter.Int32BitsToSingle(0x7F7FFFFF);
    /// <summary>The emotion event Init triggers (literal at 0x00604D5C).</summary>
    public const string EmotionEventName = "CliffReact";
    /// <summary><c>BehaviorObjective</c> 0x1C, ReactedToCliff (<c>b.w</c> to <c>BehaviorObjectiveAchieved</c>, r1 = 0x1C, 0x006053A0).</summary>
    public const int ObjectiveReactedToCliff = 0x1C;
    /// <summary>The tags the constructor subscribes (table 0x00C73740, u16): CliffEvent 0x22, RobotStopped 0x34, ChargerEvent 0x39.</summary>
    public static readonly int[] SubscribedTags = { 0x22, 0x34, 0x39 };

    private volatile uint _state;            // +0x11C
    private volatile byte _reactNow;         // +0x120
    private volatile byte _cliffDetected;    // +0x121
    private ushort _savedThreshold;          // +0x122
    private volatile byte _quit;             // +0x124
    private volatile byte _finish;           // +0x125
    private CozmoRobot? _robot;
    private CancellationTokenSource? _driveCancel;

    public ReactToCliffBehavior(CozmoRobot? robot = null, string id = "ReactToCliff") : base(id, "ReactToCliff")
    {
        if (robot is not null) Attach(robot);
    }

    /// <summary>IBehavior +0xD9, the config key <c>alwaysStreamline</c> (0x005BC200..0x005BC21E): false when the key is absent, as it is in the shipped config.</summary>
    public bool AlwaysStreamline { get; init; }

    /// <summary>+0x11C: the state HandleWhileNotRunning leaves for Init (0 or 1).</summary>
    public uint State => _state;
    /// <summary>+0x120.</summary>
    public bool ReactNow => _reactNow != 0;
    /// <summary>+0x121.</summary>
    public byte CliffDetectedCopy => _cliffDetected;
    /// <summary>+0x122: the cliff-detect threshold Init saved.</summary>
    public ushort SavedThreshold => _savedThreshold;
    /// <summary>+0x124.</summary>
    public bool QuitDueToSuspiciousCliff => _quit != 0;
    /// <summary>+0x125.</summary>
    public bool FinishRequested => _finish != 0;

    /// <summary>
    /// <c>DriveStraightAction(robot, distance, speed, shouldPlayAnimation = true)</c> (M13, <c>TransitionToBackingUp</c> 0x0060534E): runs the drive and returns when
    /// it ends, whatever its result (the completion callback runs for any result). The stack's <c>DriveStraightAction</c> has no
    /// <c>shouldPlayAnimation</c> parameter. Unset: the drive cannot be started (MISSING).
    /// </summary>
    public Func<float, float, CancellationToken, Task>? DriveStraight { get; set; }

    /// <summary><c>Robot::Broadcast(MessageEngineToGame(RobotCliffEventFinished))</c> (0x006052D2, 0x006052DA): the local seam; the app-facing message is not built.</summary>
    public event Action? RobotCliffEventFinished;

    /// <summary>Subscribes the constructor's tags: CliffEvent (0x22) and RobotStopped (0x34). ChargerEvent (0x39) has no broadcaster in this stack.</summary>
    public void Attach(CozmoRobot robot)
    {
        _robot = robot;
        robot.Sensors.CliffDetected += r => HandleCliffEvent((byte)r.Sensors);
        robot.Sensors.RobotStopped += _ => HandleRobotStopped();
        robot.Sensors.ChargerEvent += HandleChargerEvent;       // tag 0x39
    }

    /// <summary><c>IsRunnableInternal</c> 0x00604D4C: <c>movs r0,#1</c>.</summary>
    protected override bool IsRunnableInternal(BehaviorContext context) => true;

    /// <summary>
    /// UpdateInternal polls +0x125 with no action in flight (the base returns Complete only with no action; a class with a pending drive stays alive).
    /// </summary>
    protected override bool KeepsRunningWithoutAction => HasCurrentAction;

    private void EngineLog(string line) => (_robot ?? Context?.Robot)?.Engine.Log(line);

    // ------------------------------------------------------------------ events

    /// <summary>
    /// A CliffEvent (tag 0x22) with the byte at <c>CliffEvent+4</c>. Not running: <c>HandleWhileNotRunning</c> 0x0060541C (detected 0 or +0x124 set: nothing;
    /// otherwise the warning <c>"BehaviorReactToCliff.CliffWithoutStop"</c>, +0x120 = 1, +0x121 = detected, +0x11C = 1). Running: <c>HandleWhileRunning</c>
    /// 0x00605580 (detected non-zero and +0x120 clear: the debug line <c>"BehaviorReactToCliff.GotCliff"</c> and +0x121 = detected, +0x120 = 1).
    /// </summary>
    public void HandleCliffEvent(byte detected)
    {
        if (!EngineRunning)
        {
            if (detected == 0 || _quit != 0) return;
            string warn = "warning: BehaviorReactToCliff.CliffWithoutStop: Got a cliff event but stop isn't running, skipping straight to cliff react (bad latency?)";
            Log(warn);
            EngineLog(warn);
            _reactNow = 1;
            _cliffDetected = detected;
            _state = 1;
            return;
        }
        if (detected != 0 && _reactNow == 0)
        {
            string dbg = "debug: BehaviorReactToCliff.GotCliff: Got cliff event while running";
            Log(dbg);
            EngineLog(dbg);
            _cliffDetected = detected;
            _reactNow = 1;
        }
    }

    /// <summary>RobotStopped (tag 0x34), not running: +0x11C = 0 and +0x124 = 0 (0x0060541C dispatch). While running the tag is ignored (0x00605580).</summary>
    public void HandleRobotStopped()
    {
        if (EngineRunning) return;
        _state = 0;
        _quit = 0;
    }

    /// <summary>ChargerEvent (tag 0x39): while running, a non-zero <c>onCharger</c> sets +0x125 (0x006055EA..0x006055F0). Not running: nothing.</summary>
    public void HandleChargerEvent(bool onCharger)
    {
        if (!EngineRunning) return;
        if (onCharger) _finish = 1;
    }

    // ------------------------------------------------------------------ InitInternal

    /// <summary>
    /// <c>[[[robot+0x264]+0x30]+0x14]</c> (0x00605176, 0x00604DDE): the SevereNeedsComponent's single current severe <c>NeedId</c> (Repair 0, Energy 1, Play 2,
    /// Count 3 = none). Its constructor stores 3 (0x00572A0E) and <c>ClearSevereNeedExpression</c> stores 3 (0x00572D6A); this stack has no component with the
    /// engine's writers, so with <see cref="BehaviorContext.AiExpressedNeedValue"/> unset the engine's value is exactly 3 here, and the gap is reported once.
    /// A null from the seam is 3 as well.
    /// </summary>
    private int SevereNeedValue()
    {
        var seam = Context.AiExpressedNeedValue;
        if (seam is null)
        {
            ReportMissing("[[[robot+0x264]+0x30]+0x14] (SevereNeedsComponent's current severe NeedId, 0x00604DDE, 0x00605176): this stack has no SevereNeedsComponent with the engine's writers (SetSevereNeedExpression / ClearSevereNeedExpression), so the value is its constructed 3 (none)");
            return 3;
        }
        return seam() is { } need ? (int)need : 3;
    }

    protected override void OnStart()
    {
        // 0x00604D6A..0x00604D84: MoodManager::TriggerEmotionEvent(robot+0x440, "CliffReact", MoodManager::GetCurrentTimeInSeconds())
        Context.Mood?.Trigger(EmotionEventName, Clock() / 1000.0);
        // 0x00604DA0: IBehavior::SmartDisableReactionsWithLock(own name, table 0x00C73746)
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.ReactToCliff);
        InitSwitch();
    }

    /// <summary>The switch on +0x11C of <c>InitInternal</c> (0x00604DAE..0x00604E82).</summary>
    private void InitSwitch()
    {
        switch (_state)
        {
            case 0:
            {
                _savedThreshold = Context.Robot.Sensors.CliffDetectThreshold;                  // this+0x122 = [[robot+0x288]+0xC]
                int sev = SevereNeedValue();
                // StartActing<ReactToCliff>(WaitForLambdaAction($_0, FLT_MAX), sev >= 2 ? StopReaction : CliffReaction)  (0x00604DEC, 0x00604E10, 0x00604E82)
                WaitUntil(InitWaitLambda, InitWaitTimeout,
                          _ => { if (sev >= 2) TransitionToPlayingStopReaction(); else TransitionToPlayingCliffReaction(); },
                          "the robot to stop moving (BehaviorReactToCliff lambda $_0)");
                break;
            }
            case 1:
                _reactNow = 1;                                                                  // 0x00604DAE..0x00604DBC
                TransitionToPlayingCliffReaction();
                break;
            default:
            {
                string err = "error: BehaviorReactToCliff.Init.InvalidState: Init called with invalid state";
                Log(err);
                EngineLog(err);
                InitFailed = true;                                                              // returns 1
                break;
            }
        }
    }

    /// <summary>
    /// Lambda <c>$_0</c> (invoke 0x006056B8): false while <c>[[robot+0x254]+0xC]</c> is non-zero (the movement component's byte MC+0xC, which M4 names
    /// "body moving"); else true when the saved threshold still equals <c>[[robot+0x288]+0xC]</c>; else the Info event
    /// <c>"BehaviorReactToCliff.QuittingDueToSuspiciousCliff"</c> (channel Behaviors, empty format), +0x124 = 1 and true.
    /// </summary>
    private bool InitWaitLambda()
    {
        var robot = Context.Robot;
        if (robot.Motion.BodyMoving) return false;                                              // 0x006056C0
        if (_savedThreshold == robot.Sensors.CliffDetectThreshold) return true;                 // 0x006056CE..0x006056D4
        string info = "info: [Behaviors] BehaviorReactToCliff.QuittingDueToSuspiciousCliff";
        Log(info);
        EngineLog(info);
        _quit = 1;                                                                              // 0x00605718
        return true;
    }

    // ------------------------------------------------------------------ the transitions

    /// <summary>
    /// <c>TransitionToPlayingStopReaction</c> 0x00604F64: the state-name helper with <c>"PlayingStopReaction"</c> (0x00604F90); +0x124 set: only
    /// <c>SendFinishedReactToCliffMessage</c> (0x00604FA2); else a <c>CompoundActionParallel</c> of
    /// <c>TriggerLiftSafeAnimationAction(robot, 0x19E ReactToCliffDetectorStop, 1, true, 0, 60.0f, false)</c> (<c>AddAction(.., false, false)</c>) and
    /// <c>WaitForLambdaAction(robot, $_1 = +0x120, 0.55f)</c> (<c>AddAction(.., true, false)</c>), started with <c>TransitionToPlayingCliffReaction</c>.
    /// </summary>
    private void TransitionToPlayingStopReaction()
    {
        SetStateName("PlayingStopReaction");
        if (_quit != 0)
        {
            SendFinishedReactToCliffMessage();
            return;
        }
        var compound = StartParallel(2, TransitionToPlayingCliffReaction);
        if (compound is null) return;
        RunTriggerAction(0, AnimationTrigger.ReactToCliffDetectorStop, o => compound.ChildDone(o), AnimationTrack.None, AnimationTimeout, numLoops: 1, liftSafe: true);
        WaitUntilInParallel(compound, () => _reactNow != 0, StopWaitTimeout, ignoreFailure: true);   // lambda $_1 (0x0060586A..0x00605870)
    }

    /// <summary>
    /// <c>TransitionToPlayingCliffReaction</c> 0x006050F0: the helper with <c>"PlayingCliffReaction"</c> (0x00605110); <c>+0xD9</c> (alwaysStreamline) or
    /// <c>+0xD8</c> (hard spark) non-zero: <c>TransitionToBackingUp</c> at once (0x00605132); else the DAS event <c>robot.cliff_detected</c> and a
    /// <c>TriggerLiftSafeAnimationAction(robot, trigger, 1, true, 0, 60.0f, false)</c>, <c>trigger</c> = 0x13D when the severe-need value is 0, 0x131 when 1, else
    /// 0x19D (0x0060518E, 0x0060519A, 0x006051A2), completed by <c>TransitionToBackingUp</c>.
    /// </summary>
    private void TransitionToPlayingCliffReaction()
    {
        SetStateName("PlayingCliffReaction");
        if (AlwaysStreamline || SparkDisabled)
        {
            TransitionToBackingUp();
            return;
        }
        Log("DAS robot.cliff_detected");
        EngineLog("info: DAS robot.cliff_detected");
        int sev = SevereNeedValue();
        var trigger = sev == 0 ? AnimationTrigger.NeedsSevereLowRepairCliffReact
                    : sev == 1 ? AnimationTrigger.NeedsSevereLowEnergyCliffReact
                    : AnimationTrigger.ReactToCliff;
        int handle = StartActing();
        if (handle == 0) return;
        RunTriggerAction(handle, trigger, _ => TransitionToBackingUp(), AnimationTrack.None, AnimationTimeout, numLoops: 1, liftSafe: true);
    }

    /// <summary>
    /// <c>TransitionToBackingUp</c> 0x00605318: when <c>[[robot+0x288]+5]</c> is non-zero (the cliff-detected-by-event byte), a
    /// <c>DriveStraightAction(robot, -60.0f, 100.0f, true)</c> with <c>SendFinishedReactToCliffMessage</c> as its completion (lambda $_2); else
    /// <c>SendFinishedReactToCliffMessage</c> and then <c>BehaviorObjectiveAchieved(0x1C, true)</c> (0x006053A0). The objective is only on the no-back-up branch.
    /// </summary>
    private void TransitionToBackingUp()
    {
        if (Context.Robot.Sensors.CliffDetectedByEvent)                                          // 0x0060532E ldrb r0,[r0,#5]
        {
            if (DriveStraight is not { } drive)
            {
                ReportMissing("ReactToCliff::TransitionToBackingUp DriveStraightAction(robot, -60.0f, 100.0f, true) (0x0060534E): M13's DriveStraightAction is not attached (DriveStraight seam); the drive and the RobotCliffEventFinished that follows it are not sent");
                Log("MISSING: DriveStraightAction is not attached; no back-up drive");
                return;
            }
            ReportMissing("DriveStraightAction(robot, -60.0f, 100.0f, shouldPlayAnimation = true) (0x0060534E): this stack's DriveStraightAction has no shouldPlayAnimation parameter (M13), so the drive plays no animation");
            int handle = StartActing();
            if (handle == 0) return;
            int epoch = CallbackEpoch;
            var cts = _driveCancel = new CancellationTokenSource();
            Log($"start DriveStraightAction({BackUpDistanceMm:F1} mm, {BackUpSpeedMmps:F1} mm/s)");
            Task.Run(() => drive(BackUpDistanceMm, BackUpSpeedMmps, cts.Token)).ContinueWith(t =>
            {
                Post(() =>
                {
                    ActingEnded(handle);
                    if (ReferenceEquals(_driveCancel, cts)) _driveCancel = null;
                    if (CallbackMayRun(epoch)) SendFinishedReactToCliffMessage();               // lambda $_2 (0x006058DA)
                });
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return;
        }
        SendFinishedReactToCliffMessage();
        Log($"BehaviorObjectiveAchieved(0x{ObjectiveReactedToCliff:X}, true)");
        ReportMissing("IBehavior::BehaviorObjectiveAchieved(BehaviorObjective, bool) body (veneer 0x008CC10C to PLT 0x4B2C64) is not in the inventory; ReactToCliff only logs the call");
    }

    /// <summary><c>SendFinishedReactToCliffMessage</c> 0x006052BC: an empty <c>RobotCliffEventFinished</c> wrapped in <c>MessageEngineToGame</c> and broadcast. It does not use <c>this</c>.</summary>
    private void SendFinishedReactToCliffMessage()
    {
        Log("broadcast RobotCliffEventFinished");
        RobotCliffEventFinished?.Invoke();
        ReportMissing("Robot::Broadcast(MessageEngineToGame(RobotCliffEventFinished)) 0x006052D2..0x006052DA: no engine-to-game message sink in this stack; only the local RobotCliffEventFinished event is raised");
    }

    /// <summary><c>UpdateInternal</c> 0x00605408: +0x125 set: cleared and the behaviour ends (returns 2); else the base <c>IBehavior::UpdateInternal</c> (tail call 0x00605418).</summary>
    protected override void OnUpdate()
    {
        if (_finish == 0) return;
        _finish = 0;
        Finish();
    }

    /// <summary><c>StopInternal</c> 0x006053FC: +0x11C = 0 and the halfword at +0x120 = 0 (+0x120 and +0x121). +0x122, +0x124 and +0x125 are left.</summary>
    protected override void OnStop(BehaviorStopReason reason)
    {
        _driveCancel?.Cancel();
        _driveCancel = null;
        _state = 0;
        _reactNow = 0;
        _cliffDetected = 0;
    }
}

/// <summary>
/// <c>BehaviorReactToPickup</c> (constructor 0x00607724, <c>InitInternal</c> 0x0060775E, <c>UpdateInternal</c> 0x00607BA8..0x00607CC5,
/// <c>StartAnim</c> 0x00607820..0x00607B98), the class the shipped map runs for <c>ReactionTrigger.RobotPickedUp</c>. <c>IsRunnableInternal</c>
/// 0x0060774C is <c>movs r0,#1</c> and <c>StopInternal</c> 0x00607D9C is a bare <c>bx lr</c>.
/// <list type="bullet">
/// <item><b>Fields:</b> +0x11C is a single-precision retry deadline (0.0f from the constructor), +0x120 a double retry scale (1.0 from the constructor
/// and <c>InitInternal</c>; <c>InitInternal</c> does not reset +0x11C).</item>
/// <item><b>InitInternal:</b> reset +0x120, a <c>WaitAction(0.5f)</c> started with <c>StartActing(action, StartAnim)</c> (0x00607764..0x00607780); returns 0.</item>
/// <item><b>UpdateInternal</b>, in order: +0x84 (an action in flight) returns 1; <c>robot+0x355 != 1</c> (<c>InAir</c>) returns 2; <c>robot+0x338</c> non-zero logs
/// <c>"BehaviorReactToPickup.OnCharger"</c> and returns 2; then <c>now &gt; +0x11C</c> (strict, float) or return 1; then cliff sensor 0
/// (<c>GetCliffDataRaw(0)</c>, <c>CliffSensorComponent+0xE</c>): <c>raw &gt;&gt; 4 &lt;= 0x18</c> calls <c>StartAnim</c>, otherwise the DAS event
/// <c>BehaviorReactToPickup.CalibratingHead</c> (format <c>"%d"</c>, the raw value) and a <c>CalibrateMotorAction(robot, true, false)</c> with an empty
/// callback; returns 1 either way.</item>
/// <item><b>StartAnim</b> (see <see cref="StartAnim"/>).</item>
/// </list>
/// </summary>
// fidelity: M7-015, M7-019, M7-021
public sealed class ReactToPickupBehavior : SteppedBehavior
{
    /// <summary>The initial wait, 0.5f (<c>mov.w r2,#0x3f000000</c> 0x0060776C).</summary>
    public static readonly float InitialWaitSec = BitConverter.Int32BitsToSingle(0x3F000000);
    /// <summary>The retry-scale growth, the promoted float 0.33f: the double 0.33000001311302185 (literal at 0x00607B98, bits 0x3FD51EB860000000).</summary>
    public static readonly double RetryScaleStep = BitConverter.Int64BitsToDouble(0x3FD51EB860000000);
    /// <summary>The retry interval's lower and upper multipliers, the doubles 3.0 (0x4008000000000000) and 6.0 (0x4018000000000000).</summary>
    public static readonly double RetryLowerFactor = BitConverter.Int64BitsToDouble(0x4008000000000000);
    public static readonly double RetryUpperFactor = BitConverter.Int64BitsToDouble(0x4018000000000000);
    /// <summary>The trigger time-out every StartAnim action passes: 60.0f (<c>movt r0,#0x4270</c> 0x00607A40).</summary>
    public static readonly float AnimationTimeout = BitConverter.Int32BitsToSingle(0x42700000);
    /// <summary>The cliff raw limit: <c>(raw &gt;&gt; 4) &lt;= 0x18</c> (0x00607C4A).</summary>
    public const int CliffRawLimit = 0x18;
    /// <summary>The recent-face window, 500 ms (<c>subs.w r2,r0,#0x1f4</c> 0x0060786E).</summary>
    public const uint RecentFaceWindowMs = 500;

    private float _deadline;                       // +0x11C
    private double _scale = 1.0;                   // +0x120
    private readonly VisionSystem? _vision;
    private EngineRandom? _rng;

    /// <param name="vision">The face and pet worlds <c>StartAnim</c> asks (<c>robot+0x38</c>, <c>robot+0x3C</c>, <c>GetLastImageTimeStamp</c>); null: no vision system, which is reported.</param>
    public ReactToPickupBehavior(VisionSystem? vision = null, string id = "ReactToPickup", EngineRandom? rng = null) : base(id, "ReactToPickup")
    {
        _vision = vision;
        _rng = rng;
    }

    /// <summary>+0x11C.</summary>
    public float RetryDeadline => _deadline;
    /// <summary>+0x120.</summary>
    public double RetryScale => _scale;

    /// <summary>
    /// <c>CarryingComponent::SetCarriedObjectAsUnattached(true)</c> (0x00607844), called by <c>StartAnim</c> first when <c>[[robot+0x284]+8] != -1</c>. M12's;
    /// unset while the robot is carrying, the call is reported MISSING and not made.
    /// </summary>
    public Action? SetCarriedObjectAsUnattached { get; set; }

    /// <summary>
    /// <c>[[[robot+0x264]+0x18]+0x74]</c> (0x0060793C..0x00607942), the AIWhiteboard byte that selects <c>HiccupRobotPickedUp</c> (0xE6) over <c>ReactToPickup</c>
    /// (0x1A9) in the fallback. Its writers are not read (RECOVERABLE_GAP); unset, the byte is taken as clear and that is reported.
    /// </summary>
    public Func<bool>? WhiteboardHiccupByte { get; set; }

    protected override bool IsRunnableInternal(BehaviorContext context) => true;               // 0x0060774C

    /// <summary>UpdateInternal polls with no action in flight (gate 1 returns 1 only while +0x84 is set), so the behaviour outlives its actions.</summary>
    protected override bool KeepsRunningWithoutAction => true;

    private EngineRandom Rng => _rng ??= new EngineRandom(Context.Random);

    private float NowSeconds => (float)(NowMs / 1000.0);

    /// <summary><c>InitInternal</c> 0x0060775E: +0x120 = 1.0, then <c>WaitAction(0.5f)</c> with <c>StartAnim</c> as its completion.</summary>
    protected override void OnStart()
    {
        _scale = 1.0;
        Wait(InitialWaitSec, StartAnim);
    }

    /// <summary><c>UpdateInternal</c>: the three gates and the retry (see the class summary).</summary>
    protected override void OnUpdate()
    {
        if (HasCurrentAction) return;                                                           // 0x00607BBA: return 1
        var sensors = Context.Robot.Sensors;
        if (sensors.OffTreadsState != OffTreadsState.InAir) { Finish(); return; }               // 0x00607BC4: robot+0x355 != 1 returns 2
        if (sensors.OnChargerContacts)                                                          // 0x00607BCC: robot+0x338
        {
            string line = "info: BehaviorReactToPickup.OnCharger: Stopping behavior because we are on the charger";
            Log(line);
            Context.Robot.Engine.Log(line);
            Finish();
            return;
        }
        float now = NowSeconds;                                                                 // BaseStationTimer::GetCurrentTimeInSeconds
        if (!(now > _deadline)) return;                                                         // 0x00607C38 ble: now <= deadline and NaN return 1
        ushort raw = sensors.CliffDataRawStored[0];                                             // GetCliffDataRaw(0) = [component+0xE]
        if ((raw >> 4) <= CliffRawLimit)                                                        // 0x00607C4A cmp r0,#0x18; bhi
        {
            StartAnim();
            return;
        }
        string das = $"info: DAS BehaviorReactToPickup.CalibratingHead: {raw}";
        Log(das);
        Context.Robot.Engine.Log(das);
        CalibrateHead(() => { });                                                               // CalibrateMotorAction(robot, true, false), empty callback
    }

    /// <summary>
    /// <c>StartAnim</c> 0x00607820..0x00607B98, in the engine's order: (1) a carried object is dropped
    /// (<c>SetCarriedObjectAsUnattached(true)</c>); (2) <c>hard</c> = an active hard spark (<c>[robot+0x44]+0x58 != 0x55 &amp;&amp; byte +0x5C == 0</c>, the value
    /// <see cref="SteppedBehavior.SparkGate"/> carries); (3) the faces seen in the last 500 ms of images; (4) when not hard and a face was seen: a named face is
    /// <c>SayTextAction(name, Name_Normal)</c> with <c>AcknowledgeFaceNamed</c>, else <c>TriggerAnimationAction(AcknowledgeFaceUnnamed, tracksToLock 4)</c>;
    /// (5) else, when not hard and a pet is known: the lowest-key pet's <c>PetDetectionShort_Cat</c> (type 1) or <c>_Dog</c>; (6) else the fallback
    /// <c>HiccupRobotPickedUp</c> or <c>ReactToPickup</c>; (7) the retry deadline and scale.
    /// </summary>
    private void StartAnim()
    {
        var motion = Context.Robot.Motion;
        // (1) 0x00607838..0x00607844
        if (motion.IsCarryingObject?.Invoke() == true)
        {
            if (SetCarriedObjectAsUnattached is { } drop) drop();
            else ReportMissing("ReactToPickup::StartAnim CarryingComponent::SetCarriedObjectAsUnattached(true) (0x00607844): M12's component is not attached to this behaviour (SetCarriedObjectAsUnattached seam); the carried object is not dropped");
        }

        // (2) 0x00607848..0x00607862
        bool hard;
        if (SparkGate is { } spark) hard = spark();
        else
        {
            ReportMissing("ReactToPickup::StartAnim the active-spark test ([robot+0x44]+0x58 != 0x55 && byte +0x5C == 0, 0x00607852..0x00607862): no spark layer (M15) supplies it; taken as no hard spark");
            hard = false;
        }

        // (3) 0x0060786A..0x00607880
        IReadOnlyList<int> faces = Array.Empty<int>();
        if (_vision is { } vision)
        {
            uint t = vision.LastRawFrameTimestamp ?? 0;                                         // Robot::GetLastImageTimeStamp
            uint since = t > RecentFaceWindowMs ? t - RecentFaceWindowMs : 0;                   // unsigned: subs r2,r0,#0x1f4; movls r2,#0
            faces = vision.Faces.GetFaceIDsObservedSince(since, false).OrderBy(i => i).ToList();   // a set<int>: ascending
        }
        else ReportMissing("ReactToPickup::StartAnim the face world and pet world (robot+0x38, robot+0x3C; M14) are not attached; taken as empty");

        if (!hard && faces.Count > 0)
        {
            // (4) 0x0060789C..0x00607A66
            string? name = null;
            foreach (int id in faces)
                if (_vision!.Faces.GetFace(id) is { HasName: true } face) { name = face.Name; break; }
            if (!string.IsNullOrEmpty(name))
            {
                // SayTextAction(robot, name, SayTextIntent 3 Name_Normal) + SetAnimationTrigger(AcknowledgeFaceNamed, tracksToLock 4), StartActing, empty callback
                ReportMissing("ReactToPickup::StartAnim SayTextAction(name, Name_Normal) with SetAnimationTrigger(AcknowledgeFaceNamed, 4) (0x00607A00..0x00607A2C): the SayTextAction primitive is not built in this stack; no action is started for a named face");
                Log($"MISSING: SayTextAction(\"{name}\", Name_Normal) is not built; no named-face acknowledgement");
            }
            else PlayTrigger(AnimationTrigger.AcknowledgeFaceUnnamed, () => { }, AnimationTrack.Body, AnimationTimeout);
        }
        else
        {
            PetEntry? pet = null;
            if (!hard && _vision is { } v) pet = v.Pets.Pets.OrderBy(p => p.Id).FirstOrDefault();   // the lowest key of the std::map<int, TrackedPet>
            if (pet is not null)
            {
                // (5) 0x00607988..0x006079C8
                var trigger = pet.Type == PetType.Cat ? AnimationTrigger.PetDetectionShort_Cat : AnimationTrigger.PetDetectionShort_Dog;
                PlayTrigger(trigger, () => { }, AnimationTrack.Body, AnimationTimeout);
            }
            else
            {
                // (6) 0x0060793C..0x0060797E
                bool hiccup;
                if (WhiteboardHiccupByte is { } flag) hiccup = flag();
                else
                {
                    ReportMissing("ReactToPickup::StartAnim [[[robot+0x264]+0x18]+0x74] (0x00607942): the AIWhiteboard byte's writers are unread (RECOVERABLE_GAP); taken as clear, so ReactToPickup (0x1A9) plays and not HiccupRobotPickedUp (0xE6)");
                    hiccup = false;
                }
                PlayTrigger(hiccup ? AnimationTrigger.HiccupRobotPickedUp : AnimationTrigger.ReactToPickup, () => { }, AnimationTrack.None, AnimationTimeout);
            }
        }

        // (7) 0x00607A8C..0x00607AE0
        double x = _scale;                                                                      // vldr d8,[r8,#0x120]
        double r = Rng.RandDblInRange(RetryLowerFactor * x, RetryUpperFactor * x);              // GetRNG()->RandDblInRange(3.0*x, 6.0*x)
        _deadline = NowSeconds + (float)r;                                                      // vcvt.f32.f64; vadd.f32 (0x00607ACE..0x00607ADC)
        _scale = x + RetryScaleStep;                                                            // vadd.f64 (0x00607AD4); vstr d2,[r8,#0x120]
    }
}
