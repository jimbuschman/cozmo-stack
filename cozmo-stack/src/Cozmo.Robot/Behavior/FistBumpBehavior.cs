using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// <c>BehaviorFistBump</c> (<c>CreateBehavior</c> case 0x19, constructor 0x005F1D8C, 0x158 bytes; <c>InitInternal</c> 0x005F1ED8, <c>UpdateInternal</c>
/// 0x005F1F24, <c>StopInternal</c> 0x005F26F8, <c>IsRunnableInternal</c> 0x005F1ED4: always true). The state is the 32-bit value at +0x11C; the shipped
/// configs are <c>freeplay/userInteractive/fistBump.json</c> (3.0 s face search, abort when no face, update the last completion) and
/// <c>freeplay/sparkable/sparksFistBump.json</c> (5.0 s, no abort, the completion time is not updated).
///
/// <b>Constructor</b> (0x005F1D98..0x005F1E3A): counters, timestamps and snapshots are zero, <c>abortIfNoFaceFound</c> is true and
/// <c>updateLastCompletionTime</c> false; then the optional keys <c>maxTimeToLookForFace_s</c> (+0x12C), <c>abortIfNoFaceFound</c> (+0x130) and
/// <c>updateLastCompletionTime</c> (+0x148) are read.
///
/// <b>Init</b> (0x005F1ED4..0x005F1F1E): the eight-trigger lock table (<see cref="ReactionLockTables.FistBump"/>, 0x00C6FDC8) is installed through
/// <c>SmartDisableReactionsWithLock</c>, the idle trigger 0x23F (<c>AnimationTrigger::Count</c>) is pushed, the search and retry fields and the off-treads
/// stamp are cleared, and the state is 1 when the carried-object ID <c>[[robot+0x284]+8]</c> is -1 and 0 otherwise.
///
/// <b>Update</b>: first the persistent off-treads exit (0x005F1F4A..0x005F1F7A): while the off-treads state is not OnTreads the first tick stamps +0x144 and
/// after strictly more than 1.0 s the behaviour returns the terminal 2; OnTreads clears the stamp. Then the gate (0x005F1F7E..0x005F1F8E): the state
/// switch (<c>tbh</c> 0x005F1F98) runs only when the state is 5 or 6, or when no action is running (+0x84 == 0); otherwise the update returns
/// (0x005F2506).
/// <list type="bullet">
/// <item>0: <c>PlaceObjectOnGroundAction</c>, then state 1 (0x005F1FB0..0x005F1FCA, 0x005F2364).</item>
/// <item>1: <c>TurnTowardsFaceAction(face 0, maxTurn pi)</c> with the last-face-pose vtable and its +0x193 byte set, completing into the callback below
/// (0x005F2070..0x005F20CA, 0x005F27EE..0x005F2814).</item>
/// <item>2: the bounded search (see <see cref="SearchState"/>).</item>
/// <item>3: <c>TriggerAnimationAction(0xC7 FistBumpRequestOnce, 1, 1, 0, 60.0, 0)</c>, state 4 (0x005F20E2..0x005F2112, 0x005F235A..0x005F2374).</item>
/// <item>4: +0x134 = now, lift and head power disabled, <c>TriggerAnimationAction(0xC6 FistBumpIdle ...)</c>, state 5 (0x005F200C..0x005F2054).</item>
/// <item>5: waits for MovementComponent +0x0B and +0x0A to be zero, then snapshots the lift angle (+0x300 into +0x140) and the raw accel X (+0x360 into
/// +0x13C) and enters 6; settling that took more than 0.5 s warns (0x005F212A..0x005F2168).</item>
/// <item>6: a bump (|lift - snapshot| &gt; 0x3C0EFA35, |gyro Y +0x370| &gt; 0x3E32B8C2 or |accel X - snapshot| &gt; 0x457A0000) stops the idle action, powers the
/// motors back, starts <c>0xC9 FistBumpSuccess</c> and enters 7; otherwise, when the idle action has ended, power is restored and +0x138 incremented: the first
/// time <c>0xC8 FistBumpRequestRetry</c> and back to 4, the second <c>0xCA FistBumpLeftHanging</c> and 8 (0x005F21B2..0x005F24A2).</item>
/// <item>7: NeedActionCompleted(0), objective 8, <c>ResetTrigger(updateLastCompletionTime)</c>, objective 7; 8: objective 9, ResetTrigger, objective 7; 9 (entered at
/// 0x005F228C): ResetTrigger, objective 7; each returns the terminal 2 (0x005F2274..0x005F229A, 0x005F26B8..0x005F26F6).</item>
/// </list>
/// <b>Stop</b>: lift and head power are enabled and <c>ResetTrigger(false)</c> is called (0x005F26F8..0x005F2714).
///
/// <b>Configured owners</b> provide the idle stack, manipulation, face world, motors and objective broadcast.
/// The standalone constructor also supports explicit inputs for simulation and host tools.
/// </summary>
// fidelity: M7-018
public sealed class FistBumpBehavior : SteppedBehavior
{
    // ---- the engine's literals, as bit patterns
    /// <summary>The off-treads window: 1.0f (strictly more than this returns 2).</summary>
    public static readonly float OffTreadsLimitSec = BitConverter.Int32BitsToSingle(0x3F800000);
    /// <summary>The motor-settle warning limit: 0.5f.</summary>
    public static readonly float SettleWarnSec = BitConverter.Int32BitsToSingle(0x3F000000);
    /// <summary>Lift angle bump threshold 0.008726646 (0x3C0EFA35, half a degree).</summary>
    public static readonly float LiftBumpRad = BitConverter.Int32BitsToSingle(0x3C0EFA35);
    /// <summary>Gyro-Y bump threshold 0.17453292 (0x3E32B8C2, ten degrees per second).</summary>
    public static readonly float GyroBumpRadPerSec = BitConverter.Int32BitsToSingle(0x3E32B8C2);
    /// <summary>Accel-X bump threshold 4000.0 (0x457A0000).</summary>
    public static readonly float AccelBumpMmps2 = BitConverter.Int32BitsToSingle(0x457A0000);
    /// <summary>The search pan angles (data 0x00C6FDC0..0x00C6FDC7): -0.2617994 (0xBE860A92) then +0.5235988 (0x3F060A92).</summary>
    public static readonly float[] ScanPanRad =
    {
        BitConverter.Int32BitsToSingle(unchecked((int)0xBE860A92)),
        BitConverter.Int32BitsToSingle(0x3F060A92),
    };
    /// <summary>The search tilt: 0.6108652 as the engine's float, 0x3F1C61AA (<c>movw r1,#0x61aa</c> 0x005F23B0; <c>movt r1,#0x3f1c</c> 0x005F23B6).</summary>
    public static readonly float ScanTiltRad = BitConverter.Int32BitsToSingle(0x3F1C61AA);
    /// <summary>The animation timeout every <c>TriggerAnimationAction</c> here passes: 60.0f.</summary>
    public static readonly float AnimationTimeoutSec = BitConverter.Int32BitsToSingle(0x42700000);
    /// <summary><c>Radians(pi)</c> (0x005F2084): the largest body turn of the face turn. The float pi, 0x40490FDB.</summary>
    public static readonly float MaxTurnRad = BitConverter.Int32BitsToSingle(0x40490FDB);
    /// <summary>The scan reschedule range <c>RandDblInRange(1.0, 2.0)</c>.</summary>
    public const double ScanDelayMinSec = 1.0, ScanDelayMaxSec = 2.0;
    /// <summary>The face is recent when <c>(robot+0x2C - timestamp) &gt;&gt; 3 &lt;= 124</c>, unsigned (0x005F22B4..0x005F2340).</summary>
    public const uint RecentFaceShifted = 124;
    /// <summary><c>NO_FACE</c> 0x0300000E (the <c>TurnTowardsFaceAction</c> result that enters the search).</summary>
    public const uint NoFaceResult = 0x0300000E;
    /// <summary>The idle trigger Init pushes: 0x23F, <c>AnimationTrigger::Count</c> (0x005F1EEE).</summary>
    public const AnimationTrigger IdleTriggerCount = (AnimationTrigger)0x23F;

    /// <summary>The states (BehaviorFistBump +0x11C).</summary>
    public const int PutDown = 0, TurnToFace = 1, SearchState = 2, Request = 3, Idle = 4, Settle = 5, DetectBump = 6, Success = 7, LeftHanging = 8, Abort = 9;

    private readonly VisionSystem? _vision;
    private readonly ManipulationSystem? _manipulation;
    private EngineRandom? _rng;
    private EngineRobot? _nativeOwner;
    private long _generation;

    private int _state;                    // +0x11C
    private float _searchStart;            // +0x120
    private float _nextScan;               // +0x124
    private int _scanIndex;                // +0x128
    private float _idleStart;              // +0x134
    private int _retries;                  // +0x138
    private float _accelXSnapshot;         // +0x13C
    private float _liftSnapshot;           // +0x140
    private float _offTreadsStamp;         // +0x144
    private readonly List<Action<bool>> _listeners = new();
    private CancellationTokenSource? _cancel;

    /// <summary><c>maxTimeToLookForFace_s</c> (+0x12C). Not stated when the key is absent: zero here (every shipped config has it).</summary>
    public float MaxTimeToLookForFaceSec { get; }
    /// <summary><c>abortIfNoFaceFound</c> (+0x130), true until the config says otherwise.</summary>
    public bool AbortIfNoFaceFound { get; }
    /// <summary><c>updateLastCompletionTime</c> (+0x148), false until the config says otherwise.</summary>
    public bool UpdateLastCompletionTime { get; }

    public FistBumpBehavior(string id, float maxTimeToLookForFaceSec, bool abortIfNoFaceFound, bool updateLastCompletionTime,
                            VisionSystem? vision = null, ManipulationSystem? manipulation = null, EngineRandom? rng = null) : base(id, "FistBump")
    {
        MaxTimeToLookForFaceSec = maxTimeToLookForFaceSec;
        AbortIfNoFaceFound = abortIfNoFaceFound;
        UpdateLastCompletionTime = updateLastCompletionTime;
        _vision = vision; _manipulation = manipulation; _rng = rng;
    }

    /// <summary>The constructor's config read (0x005F1D98..0x005F1E3A; keys at 0x005F1DD0, 0x005F1DFC, 0x005F1E28).</summary>
    public static FistBumpBehavior FromConfig(JsonElement config, VisionSystem? vision = null, ManipulationSystem? manipulation = null)
    {
        string id = BehaviorConfigLoader.ExtractIdFromConfig(config).ToString();
        float max = 0f;
        bool abort = true, update = false;
        if (config.TryGetProperty("maxTimeToLookForFace_s", out var m) && m.ValueKind == JsonValueKind.Number) max = (float)m.GetDouble();
        if (config.TryGetProperty("abortIfNoFaceFound", out var a) && a.ValueKind is JsonValueKind.True or JsonValueKind.False) abort = a.GetBoolean();
        if (config.TryGetProperty("updateLastCompletionTime", out var u) && u.ValueKind is JsonValueKind.True or JsonValueKind.False) update = u.GetBoolean();
        return new FistBumpBehavior(id, max, abort, update, vision, manipulation);
    }

    internal static FistBumpBehavior FromConfig(JsonElement config, BehaviorFactoryContext context)
    {
        var behavior = FromConfig(config, context.Vision, context.Manipulation);
        if (context.NativeRobot is { } owner && context.Manipulation is not null && context.Vision is not null)
            behavior._nativeOwner = owner;
        return behavior;
    }

    internal bool IsNativeOwnedBy(EngineRobot owner) => ReferenceEquals(_nativeOwner, owner);

    // Standalone inputs.

    /// <summary>Standalone provider for the Count idle trigger.</summary>
    public Action<AnimationTrigger>? SmartPushIdleAnimation { get; set; }
    /// <summary>Standalone provider for lift power.</summary>
    public Action<bool>? EnableLiftPower { get; set; }
    /// <summary>Standalone provider for head power.</summary>
    public Action<bool>? EnableHeadPower { get; set; }
    /// <summary>Standalone provider for the search pan and tilt.</summary>
    public Func<float, float, bool, bool, CancellationToken, Task>? PanAndTilt { get; set; }
    /// <summary><c>PlaceObjectOnGroundAction</c> (state 0). Defaults to the manipulation system's, when there is one.</summary>
    public Func<CancellationToken, Task>? PlaceObjectOnGround { get; set; }
    /// <summary>The turn of states 1 and 2 (<c>TurnTowardsFaceAction</c> with the last-face-pose vtable, +0x193 = 1). Defaults to the vision system's.</summary>
    public Func<CancellationToken, Task<uint>>? TurnTowardsLastFace { get; set; }

    /// <summary>A strategy (M10: <c>ReactionTriggerStrategyFistBump</c>) that wants <c>ResetTrigger(bool)</c> when the behaviour ends registers here.</summary>
    public void AddListener(Action<bool> listener) => _listeners.Add(listener);

    /// <summary>The state (+0x11C), for tests.</summary>
    public int State => _state;
    /// <summary>+0x138, for tests.</summary>
    public int Retries => _retries;
    /// <summary>+0x124, for tests.</summary>
    public float NextScanTime => _nextScan;
    /// <summary>+0x128, for tests.</summary>
    public int ScanIndex => _scanIndex;

    private float NowSeconds => _nativeOwner?.Engine.Timer.SecondsF ?? (float)(NowMs / 1000.0);
    private EngineRandom Rng => _nativeOwner?.Engine.ContextRandom ?? (_rng ??= new EngineRandom(Context.Random));

    private bool NativeAvailable => _nativeOwner is { } owner && !owner.Lifetime.Destroying &&
        ReferenceEquals(owner.Engine.Robot, owner) && owner.NativeBehaviorRuntime is { IsRetired: false } runtime &&
        ReferenceEquals(runtime.Manipulation, _manipulation) && runtime.Bound.TryGetValue(Id, out var actual) &&
        ReferenceEquals(actual, this) && NativeScoringAvailable;

    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) =>
        _nativeOwner?.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => ++_generation;
    protected override long CaptureHostExecutionVersion() => _generation;
    protected override bool HostExecutionVersionCurrent(long version) =>
        _nativeOwner is null || version == _generation && NativeAvailable;
    protected override bool HostStopContinuationCurrent(long version) =>
        _nativeOwner is null || _generation == unchecked(version + 1);
    protected override bool HostResumeContinuationCurrent(long version) =>
        _nativeOwner is null || _generation == version || _generation == unchecked(version + 1);

    // Vtable +0x20, +0x24 and +0x28 at 0x0102A738..40. The last slot returns 1 at 0x005F278C.
    protected override bool RunnableGate20(BehaviorContext context) => _nativeOwner is null && base.RunnableGate20(context);
    protected override bool RunnableGate24(BehaviorContext context) => _nativeOwner is null && base.RunnableGate24(context);
    protected override bool RunnableGate28(BehaviorContext context) => true;

    /// <summary><c>IsRunnableInternal</c> 0x005F1ED4: always true after the common gates.</summary>
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        if (_nativeOwner is null || NativeAvailable) return true;
        MarkReadinessUnavailable();
        ReportMissing("M7-018 FistBump configured owner unavailable");
        return false;
    }

    /// <summary>UpdateInternal polls with no action in flight, so the behaviour outlives its actions and ends itself with the terminal 2.</summary>
    protected override bool KeepsRunningWithoutAction => true;

    /// <summary><c>InitInternal</c> 0x005F1ED8 (see the class summary).</summary>
    protected override void OnStart()
    {
        long generation = CaptureHostExecutionVersion();
        uint? carriedId;
        if (_nativeOwner is { } owner)
        {
            if (!NativeAvailable || owner.CarryingOwner is not { } carrying)
            {
                InitFailed = true;
                ReportMissing("M7-018 FistBump configured carrying owner unavailable at 0x005F1F08");
                return;
            }
            bool Current() => HostExecutionVersionCurrent(generation);
            if (!Scope.SmartDisableReactionsWithLockGuarded(Id, ReactionLockTables.FistBump, Current)) return;
            var scheduler = Context.Robot.Animations.Scheduler;
            var idle = new ScopedAcquisition();
            // SmartPushIdleAnimation concatenates the prefix initialized at 0x004D75D4..EE with +0x40.
            string idleName = "Behavior_" + Id;
            if (!Scope.SmartPushIdleAnimationGuarded(
                () => scheduler.PushScopedIdle(IdleTriggerCount, idleName, idle),
                () => { idle.Cancel(); scheduler.RemoveScopedIdle(idleName, idle, owner.Engine.Timer.TimeStampMs); },
                Current)) return;
            carriedId = carrying.CarriedObjectId;
        }
        else
        {
            Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.FistBump);
            if (SmartPushIdleAnimation is { } push) push(IdleTriggerCount);
            else ReportMissing("FistBump standalone SmartPushIdleAnimation(0x23F) provider unavailable");
            var carrying = Context.Robot.Motion.IsCarryingObject;
            if (carrying is null) ReportMissing("FistBump standalone carried-object provider unavailable");
            carriedId = carrying?.Invoke() == true ? 0u : null;
        }

        _searchStart = 0f; _nextScan = 0f; _scanIndex = 0; _retries = 0; _offTreadsStamp = 0f;
        _state = (carriedId ?? uint.MaxValue) == uint.MaxValue ? TurnToFace : PutDown;
    }

    private void StartNativeAction(ActionRunner action, long generation, Action<RobotCompletedAction>? complete = null)
    {
        bool Current() => HostExecutionVersionCurrent(generation);
        if (!Current()) { action.RetireNeverAdmittedTree(); return; }
        StartActing(action, complete is null ? null : result => { if (Current()) complete(result); }, Current);
    }

    /// <summary><c>UpdateInternal</c> 0x005F1F24 (see the class summary).</summary>
    protected override void OnUpdate()
    {
        float now = NowSeconds;
        long generation = CaptureHostExecutionVersion();
        bool Current() => HostExecutionVersionCurrent(generation);
        if (!Current()) return;

        // 0x005F1F4A..0x005F1F7A: the persistent off-treads exit
        if (Context.Robot.Sensors.OffTreadsState != OffTreadsState.OnTreads)
        {
            if (_offTreadsStamp == 0f) _offTreadsStamp = now;
            else if (now - _offTreadsStamp > OffTreadsLimitSec) { Finish(); return; }     // 0x005F1F74 -> 0x005F22A0: the terminal 2
        }
        else _offTreadsStamp = 0f;

        // 0x005F1F7E..0x005F1F8E: the state switch runs only in 5 or 6, or with no action running (+0x84 == 0)
        if (_state != Settle && _state != DetectBump && HasCurrentAction) return;

        switch (_state)
        {
            case PutDown:
                // 0x005F1FB0..0x005F1FCA: PlaceObjectOnGroundAction, then the state store 0x005F2364
                if (_nativeOwner is { } owner && _manipulation is { } m)
                {
                    try
                    {
                        StartNativeAction(new NativePlaceGroundRunner(m, owner) { HostFailureCleanup = true }, generation);
                    }
                    catch (NativeActionUnavailable error)
                    {
                        ReportMissing("M15-022 FistBump native ground-placement input unavailable: " + error.Message);
                        if (Current()) Finish();
                        return;
                    }
                }
                else StartAction("PlaceObjectOnGroundAction", PlaceObjectOnGroundRunner(), () => { });
                if (Current()) _state = TurnToFace;
                break;

            case TurnToFace:
                // 0x005F2070..0x005F20CA: the face turn; the callback (0x005F27EE..0x005F2814) writes the state
                StartTurn(generation);
                break;

            case SearchState:
                UpdateSearch(now, generation);
                break;

            case Request:
                // 0x005F20E2..0x005F2112, state store 0x005F235A..0x005F2374
                PlayAction(AnimationTrigger.FistBumpRequestOnce, generation);
                if (Current()) _state = Idle;
                break;

            case Idle:
                // 0x005F200C..0x005F2054
                _idleStart = now;
                SetPower(false, generation);
                if (!Current()) return;
                PlayAction(AnimationTrigger.FistBumpIdle, generation);
                if (Current()) _state = Settle;
                break;

            case Settle:
                // 0x005F212A..0x005F2168: both MovementComponent bytes (+0x0B lift, +0x0A head) must be zero
                if (Context.Robot.Motion.LiftMoving || Context.Robot.Motion.HeadMoving) break;
                var settled = (_nativeOwner ?? Context.Robot.Engine.Robot)?.StoredState;
                if (_nativeOwner is not null && settled is null)
                {
                    ReportMissing("M7-018 FistBump motor-settle telemetry unavailable at 0x005F2146");
                    return;
                }
                _liftSnapshot = settled?.LiftAngle ?? 0f;  // robot+0x300
                _accelXSnapshot = _nativeOwner is null ? Context.Robot.Sensors.OffTreads.RawAccel.X : settled!.Accel.X; // robot+0x360
                _state = DetectBump;
                if (now - _idleStart > SettleWarnSec)                                                // 0x005F2168 -> 0x005F21AC
                {
                    // 0x005F2178: event "BehaviorFistBump.UpdateInternal.MotorSettleTimeTooLong" (0x005F25EC), format "%f" (0x005F2624), the elapsed time as a double
                    string text = ((double)(now - _idleStart)).ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                    string warn = "warning: BehaviorFistBump.UpdateInternal.MotorSettleTimeTooLong: " + text;
                    Log(warn);
                    Context.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning, null, "BehaviorFistBump.UpdateInternal.MotorSettleTimeTooLong", text);
                }
                break;

            case DetectBump:
                UpdateDetectBump(generation);
                break;

            case Success:
                // 0x005F2274..0x005F229A: NeedActionCompleted(NoAction = 0), objective 8, ResetTrigger(+0x148), objective 7, the terminal 2
                NeedActionCompleted();
                if (!Current()) return;
                ObjectiveAchieved(BehaviorObjective.FistBumpSuccess);
                if (Current()) CompleteAttempt(generation);
                break;

            case LeftHanging:
                // objective 9, ResetTrigger(+0x148), objective 7
                ObjectiveAchieved(BehaviorObjective.FistBumpLeftHanging);
                if (Current()) CompleteAttempt(generation);
                break;

            case Abort:
                // 0x005F228C: ResetTrigger(+0x148), then objective 7
                CompleteAttempt(generation);
                break;
        }
    }

    /// <summary>
    /// State 2 (the bounded face search). The order is the order the inventory lists the branches in (timeout, face, scan), which is how the state's
    /// case is laid out from 0x005F1FE2; the inventory does not say it in so many words.
    /// </summary>
    private void UpdateSearch(float now, long generation)
    {
        // 0x005F1FE2..0x005F2006: now > +0x120 + maxTime ends the search
        if (now > _searchStart + MaxTimeToLookForFaceSec)
        {
            _state = AbortIfNoFaceFound ? Abort : Request;
            return;
        }
        // 0x005F22B4..0x005F2340: GetLastObservedFace(..., true) non-zero and (robot+0x2C - result) >> 3 <= 124 (unsigned)
        var faceOwner = _nativeOwner ?? Context.Robot.Engine.Robot;
        if (_vision is not null && faceOwner is not null &&
            faceOwner.TryGetNativeOriginOwner(faceOwner.CurrentOriginId, out var faceOrigin) && faceOrigin is not null &&
            faceOwner.StoredState is { } state)
        {
            uint faceStamp = LastFaceStamp(_vision, faceOrigin);
            if (faceStamp != 0 && unchecked(state.Timestamp - faceStamp) >> 3 <= RecentFaceShifted)
            {
                // 0x005F22F8..0x005F2340: no verification store and no completion callback.
                StartTurn(generation, stateOne: false);
                if (HostExecutionVersionCurrent(generation)) _state = Request;
                return;
            }
        }
        else if (_nativeOwner is not null)
        {
            ReportMissing("M7-018 FistBump current-origin last face or robot timestamp unavailable at 0x005F22B4");
            return;
        }
        else if (_vision is null)
            ReportMissing("BehaviorFistBump state 2 GetLastObservedFace: standalone vision input unavailable");
        // 0x005F2382..0x005F24EE: the scan
        if (now > _nextScan)
        {
            float pan = ScanPanRad[_scanIndex];
            // 0x005F23BE..0x005F23C6: PanAndTiltAction(robot, pan, tilt, isPanAbsolute = false, isTiltAbsolute = true) ([sp] = 0, [sp+4] = 1)
            if (_nativeOwner is not null && _manipulation is { } m)
            {
                var parameters = new PanAndTiltAction
                {
                    PanAngleRad = pan, HeadAngleRad = ScanTiltRad, PanIsAbsolute = false, HeadIsAbsolute = true
                };
                StartNativeAction(new NativePanTiltRunner(m, parameters), generation);
            }
            else if (PanAndTilt is { } pat)
                StartAction($"PanAndTiltAction(pan {pan}, tilt {ScanTiltRad})", ct => pat(pan, ScanTiltRad, false, true, ct), () => { });
            else ReportMissing("FistBump standalone PanAndTiltAction provider unavailable");
            if (!HostExecutionVersionCurrent(generation)) return;
            float delay = (float)Rng.RandDblInRange(ScanDelayMinSec, ScanDelayMaxSec);
            if (!HostExecutionVersionCurrent(generation)) return;
            _nextScan = now + delay;
            _scanIndex = _scanIndex + 1 < ScanPanRad.Length ? _scanIndex + 1 : 0;           // 0x005F24D8..0x005F24EE: idx+1 < (end-begin)>>2 else 0; the vector has two entries (0x004D7D10..0x004D7D4E)
        }
    }

    // FaceWorld::GetLastObservedFace(pose, true) (0x004F5AE2..0x004F5B38): the cache stamp; the bool requires the cached pose to be in the
    // current world origin (it is not a name filter). 0 when nothing is cached or the pose is in another origin.
    private static uint LastFaceStamp(VisionSystem vision, Pose3d origin)
    {
        using var pose = new Pose3d("");
        return vision.Faces.CopyNativeLastFaceTimestamp(origin, true, pose);
    }

    /// <summary>State 6: a bump, or the idle action ending without one.</summary>
    private void UpdateDetectBump(long generation)
    {
        var robot = Context.Robot;
        var sample = (_nativeOwner ?? robot.Engine.Robot)?.StoredState;
        if (_nativeOwner is not null && sample is null)
        {
            ReportMissing("M7-018 FistBump bump telemetry unavailable at 0x005F21B2");
            return;
        }
        float lift = sample?.LiftAngle ?? 0f;
        float gyroY = _nativeOwner is null ? robot.Sensors.OffTreads.RawGyro.Y : sample!.Gyro.Y;
        float accelX = _nativeOwner is null ? robot.Sensors.OffTreads.RawAccel.X : sample!.Accel.X;
        bool bump = MathF.Abs(lift - _liftSnapshot) > LiftBumpRad
                    || MathF.Abs(gyroY) > GyroBumpRadPerSec
                    || MathF.Abs(accelX - _accelXSnapshot) > AccelBumpMmps2;
        bool Current() => HostExecutionVersionCurrent(generation);
        if (bump)
        {
            // 0x005F2208..0x005F2258
            StopActing(keepAction: true, viaCallback: false);      // StopActing(this, 1, 0) 0x005F2208..0x005F2214
            if (!Current()) return;
            SetPower(true, generation);
            if (!Current()) return;
            PlayAction(AnimationTrigger.FistBumpSuccess, generation);
            if (Current()) _state = Success;
            return;
        }
        if (!HasCurrentAction)
        {
            // 0x005F23FC..0x005F24A2: the idle action ended without a bump
            SetPower(true, generation);
            if (!Current()) return;
            _retries++;
            if (_retries == 1)
            {
                PlayAction(AnimationTrigger.FistBumpRequestRetry, generation);
                if (Current()) _state = Idle;
            }
            else
            {
                PlayAction(AnimationTrigger.FistBumpLeftHanging, generation);
                if (Current()) _state = LeftHanging;
            }
        }
    }

    /// <summary>
    /// <c>TurnTowardsFaceAction(robot, 0, Radians(pi), ...)</c> with the last-face-pose vtable and +0x193 = 1; its completion callback tests the result
    /// (0x005F27EE..0x005F2814): <c>NO_FACE</c> stamps the search start (+0x120) and enters state 2, every other result enters state 3.
    /// Both call sites pass sayName=false (0x005F2090..9C and 0x005F2310..24).
    /// </summary>
    private void StartTurn(long generation, bool stateOne = true)
    {
        void TurnEnded(uint result)
        {
            if (result == NoFaceResult) { _searchStart = NowSeconds; _state = SearchState; }
            else _state = Request;
        }
        if (_nativeOwner is { } owner && _manipulation is { } m)
        {
            var action = new NativeLastFaceRunner(m, owner, 0, sayName: false) { RequireVerifiedFace = stateOne };
            StartNativeAction(action, generation, stateOne ? result => TurnEnded(result.Result) : null);
            return;
        }
        if (_turnRunner(stateOne) is not { } run)
        {
            ReportMissing("BehaviorFistBump TurnTowardsFaceAction (0x005F2070..0x005F20CA): no vision system is attached to this behaviour, so no turn can run; it ends as a result other than NO_FACE (the search is skipped)");
            _state = Request;
            return;
        }
        if (!stateOne)
        {
            StartAction("TurnTowardsFaceAction(last face, pi)", run, _ => { }, uint.MaxValue);       // state 2's: no fail-if-no-face byte, empty callback
            return;
        }
        StartAction("TurnTowardsFaceAction(last face, pi)", run, TurnEnded, uint.MaxValue);
    }

    private Func<CancellationToken, Task<uint>>? _turnRunner(bool stateOne)
    {
        if (TurnTowardsLastFace is { } t) return t;
        if (_vision is not { } v) return null;
        return async ct =>
        {
            // fidelity: M7-018 -- MISSING: the native turn is TurnTowardsFaceAction(robot, id 0, pi, sayName 0) (0x005F2300..0x005F2324),
            // which reads the native entry pose and last-face cache; this legacy behaviour has no owner-bound action seam, so the
            // turn still reads the host last face and its flat projection while state 2's gate reads the native cache.
            SteppedBehavior.ReportMissing("M7-018 FistBump last-face turn runs the legacy TurnTowardsLastFacePoseAction (host face pose), not the native TurnTowardsFaceAction (0x005F2300..0x005F2324): no owner-bound action seam");
            using var turn = new TurnTowardsLastFacePoseAction(v, MaxTurnRad, sayName: false) { RequireVerifiedFace = stateOne };      // +0x193 = 1 is state 1's store only (0x005F20B8)
            return (uint)await turn.RunAsync(ct).ConfigureAwait(false);
        };
    }

    private Func<CancellationToken, Task>? PlaceObjectOnGroundRunner()
    {
        if (PlaceObjectOnGround is { } p) return p;
        if (_manipulation is not { } m) return null;
        var owner = m.Robot.Engine.Robot;
        return ct => m.PlaceObjectOnGroundAsync(owner, ct);
    }

    /// <summary>A <c>TriggerAnimationAction(trigger, 1, 1, 0, 60.0, 0)</c> started with <c>StartActing</c> and an empty completion.</summary>
    private void PlayAction(AnimationTrigger trigger, long generation)
    {
        if (_nativeOwner is not null)
            StartNativeAction(new TriggerAnimationAction(Context.Robot, new(trigger, 1, true, 0, AnimationTimeoutSec, false)), generation);
        else PlayTrigger(trigger, () => { }, AnimationTrack.None, AnimationTimeoutSec);
    }

    /// <summary>One async action started with <c>StartActing</c>: +0x84 is set until it ends, and the callback runs on the behaviour's tick while it still runs.</summary>
    private void StartAction(string what, Func<CancellationToken, Task>? run, Action onDone)
    {
        if (run is null)
        {
            ReportMissing($"BehaviorFistBump {what}: no manipulation system is attached to this behaviour, so the action cannot run; it is not started");
            return;
        }
        StartAction(what, async ct => { await run(ct).ConfigureAwait(false); return 0u; }, _ => onDone(), 0u);
    }

    private void StartAction(string what, Func<CancellationToken, Task<uint>> run, Action<uint> onDone, uint failed)
    {
        int handle = StartActing();
        if (handle == 0) return;
        int epoch = CallbackEpoch;
        _cancel?.Cancel();
        var cts = _cancel = new CancellationTokenSource();
        Log($"start {what}");
        ObserveAsyncWork(RunAsyncAction(() => { var work = run(cts.Token); ObserveAsyncInvocation(work); return work; }).ContinueWith(t =>
        {
            uint result = t.Status == TaskStatus.RanToCompletion ? t.Result : failed;
            Post(() =>
            {
                ActingEnded(handle);
                if (ReferenceEquals(_cancel, cts)) _cancel = null;
                Log($"{what} -> 0x{result:X8}");
                if (CallbackMayRun(epoch)) onDone(result);
            });
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default));
    }

    /// <summary>Requests motor power in lift, head order (0x005F2704 and 0x005F270E).</summary>
    private void SetPower(bool enable, long generation)
    {
        if (_nativeOwner is { } owner)
        {
            if (!HostExecutionVersionCurrent(generation)) return;
            if (!Context.Robot.Motion.SetNativeMotorPower(owner, MotorID.MOTOR_LIFT, enable))
                ReportMissing("M7-018 FistBump lift power send unavailable at 0x00640634");
            if (!HostExecutionVersionCurrent(generation)) return;
            if (!Context.Robot.Motion.SetNativeMotorPower(owner, MotorID.MOTOR_HEAD, enable))
                ReportMissing("M7-018 FistBump head power send unavailable at 0x006406CC");
        }
        else
        {
            if (EnableLiftPower is { } lift) lift(enable);
            else ReportMissing("FistBump standalone EnableLiftPower provider unavailable");
            if (EnableHeadPower is { } head) head(enable);
            else ReportMissing("FistBump standalone EnableHeadPower provider unavailable");
        }
    }

    private void ObjectiveAchieved(BehaviorObjective objective)
    {
        long generation = CaptureHostExecutionVersion();
        Log($"BehaviorObjectiveAchieved({(uint)objective})");
        if (_nativeOwner is { } owner && HostExecutionVersionCurrent(generation))
            owner.Engine.ReportBehaviorObjective((uint)objective, objective.ToString(), Id);
    }

    private void CompleteAttempt(long generation)
    {
        ResetTrigger(UpdateLastCompletionTime);
        if (!HostExecutionVersionCurrent(generation)) return;
        ObjectiveAchieved(BehaviorObjective.FistBumpComplete);
        if (HostExecutionVersionCurrent(generation)) Finish();
    }

    /// <summary><c>ResetTrigger(bool)</c> 0x005F26B8..0x005F26F6: every registered fist-bump listener.</summary>
    private void ResetTrigger(bool updateLastCompletionTime)
    {
        foreach (var listener in _listeners.ToArray()) listener(updateLastCompletionTime);
    }

    /// <summary><c>StopInternal</c> 0x005F26F8..0x005F2714: lift and head power back on, then <c>ResetTrigger(false)</c>.</summary>
    protected override void OnStop(BehaviorStopReason reason)
    {
        _cancel?.Cancel();
        _cancel = null;
        long generation = CaptureHostExecutionVersion();
        SetPower(true, generation);
        if (HostExecutionVersionCurrent(generation)) ResetTrigger(false);
        if (_generation == generation) ++_generation;
    }

    internal void RetireNativeActionOwner()
    {
        if (_nativeOwner is not null) RetireActionCompletionOwner();
    }
}
