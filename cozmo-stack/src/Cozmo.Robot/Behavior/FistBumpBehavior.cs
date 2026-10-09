using System.Text.Json;
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
/// <b>Seams the inventory does not settle</b> (each reports MISSING when it is hit unset, and nothing is made up): what <c>SmartPushIdleAnimation(0x23F)</c>
/// does (<see cref="SmartPushIdleAnimation"/>), the bodies of <c>EnableLiftPower</c> and <c>EnableHeadPower</c> (<see cref="EnableLiftPower"/>,
/// <see cref="EnableHeadPower"/>), the <c>PanAndTiltAction</c> (<see cref="PanAndTilt"/>), and the body of <c>BehaviorObjectiveAchieved</c>. The strategy
/// that listens for the end (<c>ReactionTriggerStrategyFistBump</c>, M10) registers through <see cref="AddListener"/>.
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

    // ---- seams the inventory does not settle

    /// <summary>
    /// <c>SmartPushIdleAnimation(0x23F)</c>, called by Init (0x005F1EEE). What the engine does with the Count trigger is UNKNOWN in the inventory, so
    /// this is a seam: unset, the push is not made and the gap is reported.
    /// </summary>
    public Action<AnimationTrigger>? SmartPushIdleAnimation { get; set; }
    /// <summary><c>EnableLiftPower(bool)</c> (0x005F2704 for true). Its body is not in the inventory: unset, nothing is sent and the gap is reported.</summary>
    public Action<bool>? EnableLiftPower { get; set; }
    /// <summary><c>EnableHeadPower(bool)</c> (0x005F270E for true). Its body is not in the inventory: unset, nothing is sent and the gap is reported.</summary>
    public Action<bool>? EnableHeadPower { get; set; }
    /// <summary>
    /// <c>PanAndTiltAction(robot, pan, tilt, isPanAbsolute = false, isTiltAbsolute = true)</c> started by the search (0x005F23BE..0x005F23CE). This stack's
    /// <c>PanAndTiltAction</c> is not live (M13-020), so it is a seam taking those arguments. Unset, no action is started (the schedule still advances) and the gap is reported.
    /// </summary>
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

    private float NowSeconds => (float)(NowMs / 1000.0);
    private EngineRandom Rng => _rng ??= new EngineRandom(Context.Random);

    /// <summary><c>IsRunnableInternal</c> 0x005F1ED4: always true.</summary>
    protected override bool IsRunnableInternal(BehaviorContext context) => true;

    /// <summary>UpdateInternal polls with no action in flight, so the behaviour outlives its actions and ends itself with the terminal 2.</summary>
    protected override bool KeepsRunningWithoutAction => true;

    /// <summary><c>InitInternal</c> 0x005F1ED8 (see the class summary).</summary>
    protected override void OnStart()
    {
        // 0x005F1ED4..: SmartDisableReactionsWithLock(own name, table 0x00C6FDC8)
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.FistBump);
        // 0x005F1EEE: SmartPushIdleAnimation(0x23F)
        if (SmartPushIdleAnimation is { } push) push(IdleTriggerCount);
        else ReportMissing("BehaviorFistBump::InitInternal SmartPushIdleAnimation(0x23F = AnimationTrigger::Count) (0x005F1EEE): what the engine does with the Count trigger here is UNKNOWN in the inventory (SmartPushIdleAnimation seam); no idle is pushed");
        _searchStart = 0f; _nextScan = 0f; _scanIndex = 0; _retries = 0; _offTreadsStamp = 0f;
        // 0x005F1F08..0x005F1F18: state 1 when [[robot+0x284]+8] == -1 (nothing carried), else 0
        var carrying = Context.Robot.Motion.IsCarryingObject;
        if (carrying is null) ReportMissing("BehaviorFistBump::InitInternal [[robot+0x284]+8] (0x005F1F08): the carried-object ID has no source (the M12 carrying seam is not attached); taken as -1, nothing carried");
        _state = carrying?.Invoke() == true ? PutDown : TurnToFace;
    }

    /// <summary><c>UpdateInternal</c> 0x005F1F24 (see the class summary).</summary>
    protected override void OnUpdate()
    {
        float now = NowSeconds;

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
                StartAction("PlaceObjectOnGroundAction", PlaceObjectOnGroundRunner(), () => { });
                _state = TurnToFace;
                break;

            case TurnToFace:
                // 0x005F2070..0x005F20CA: the face turn; the callback (0x005F27EE..0x005F2814) writes the state
                StartTurn();
                break;

            case SearchState:
                UpdateSearch(now);
                break;

            case Request:
                // 0x005F20E2..0x005F2112, state store 0x005F235A..0x005F2374
                PlayAction(AnimationTrigger.FistBumpRequestOnce);
                _state = Idle;
                break;

            case Idle:
                // 0x005F200C..0x005F2054
                _idleStart = now;
                SetPower(false);
                PlayAction(AnimationTrigger.FistBumpIdle);
                _state = Settle;
                break;

            case Settle:
                // 0x005F212A..0x005F2168: both MovementComponent bytes (+0x0B lift, +0x0A head) must be zero
                if (Context.Robot.Motion.LiftMoving || Context.Robot.Motion.HeadMoving) break;
                _liftSnapshot = Context.Robot.Engine.Robot?.StoredState?.LiftAngle ?? 0f;           // robot+0x300
                _accelXSnapshot = Context.Robot.Sensors.OffTreads.RawAccel.X;                       // robot+0x360
                _state = DetectBump;
                if (now - _idleStart > SettleWarnSec)                                                // 0x005F2168 -> 0x005F21AC
                {
                    // 0x005F2178: event "BehaviorFistBump.UpdateInternal.MotorSettleTimeTooLong" (0x005F25EC), format "%f" (0x005F2624), the elapsed time as a double
                    string warn = $"warning: BehaviorFistBump.UpdateInternal.MotorSettleTimeTooLong: {((double)(now - _idleStart)).ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}";
                    Log(warn);
                    Context.Robot.Engine.Log(warn);
                }
                break;

            case DetectBump:
                UpdateDetectBump();
                break;

            case Success:
                // 0x005F2274..0x005F229A: NeedActionCompleted(NoAction = 0), objective 8, ResetTrigger(+0x148), objective 7, the terminal 2
                NeedActionCompleted();
                ObjectiveAchieved(8);
                ResetTrigger(UpdateLastCompletionTime);
                ObjectiveAchieved(7);
                Finish();
                break;

            case LeftHanging:
                // objective 9, ResetTrigger(+0x148), objective 7
                ObjectiveAchieved(9);
                ResetTrigger(UpdateLastCompletionTime);
                ObjectiveAchieved(7);
                Finish();
                break;

            case Abort:
                // 0x005F228C: ResetTrigger(+0x148), then objective 7
                ResetTrigger(UpdateLastCompletionTime);
                ObjectiveAchieved(7);
                Finish();
                break;
        }
    }

    /// <summary>
    /// State 2 (the bounded face search). The order is the order the inventory lists the branches in (timeout, face, scan), which is how the state's
    /// case is laid out from 0x005F1FE2; the inventory does not say it in so many words.
    /// </summary>
    private void UpdateSearch(float now)
    {
        // 0x005F1FE2..0x005F2006: now > +0x120 + maxTime ends the search
        if (now > _searchStart + MaxTimeToLookForFaceSec)
        {
            _state = AbortIfNoFaceFound ? Abort : Request;
            return;
        }
        // 0x005F22B4..0x005F2340: GetLastObservedFace(..., true) non-zero and (robot+0x2C - result) >> 3 <= 124 (unsigned)
        if (_vision is null)
            ReportMissing("BehaviorFistBump state 2 (0x005F22B4..0x005F2340): no vision system is attached to this behaviour, so GetLastObservedFace has no source; the face check is skipped");
        else if (_vision.Faces.GetLastObservedFace(namedOnly: true) is { } face && face.LastObservedTimestamp != 0)
        {
            uint robotClock = Context.Robot.Engine.Robot?.StoredState?.Timestamp ?? 0u;     // robot+0x2C
            if (unchecked(robotClock - face.LastObservedTimestamp) >> 3 <= RecentFaceShifted)
            {
                // 0x005F22F8..0x005F2340, 0x005F24F4..0x005F24FC: the turn is started WITHOUT the +0x193 byte (that store is state 1's, 0x005F20B8) and with an
                // EMPTY callback (r7 = 0), then state 3
                StartTurn(stateOne: false);
                _state = Request;
                return;
            }
        }
        // 0x005F2382..0x005F24EE: the scan
        if (now > _nextScan)
        {
            float pan = ScanPanRad[_scanIndex];
            // 0x005F23BE..0x005F23C6: PanAndTiltAction(robot, pan, tilt, isPanAbsolute = false, isTiltAbsolute = true) ([sp] = 0, [sp+4] = 1)
            if (PanAndTilt is { } pat)
                StartAction($"PanAndTiltAction(pan {pan}, tilt {ScanTiltRad})", ct => pat(pan, ScanTiltRad, false, true, ct), () => { });
            else ReportMissing("BehaviorFistBump search PanAndTiltAction(robot, pan, tilt, false, true) (0x005F23BE..0x005F23CE): this stack's PanAndTiltAction is not live (M13-020); no pan and tilt is started (PanAndTilt seam)");
            _nextScan = now + (float)Rng.RandDblInRange(ScanDelayMinSec, ScanDelayMaxSec);
            _scanIndex = _scanIndex + 1 < ScanPanRad.Length ? _scanIndex + 1 : 0;           // 0x005F24D8..0x005F24EE: idx+1 < (end-begin)>>2 else 0; the vector has two entries (0x004D7D10..0x004D7D4E)
        }
    }

    /// <summary>State 6: a bump, or the idle action ending without one.</summary>
    private void UpdateDetectBump()
    {
        var robot = Context.Robot;
        float lift = robot.Engine.Robot?.StoredState?.LiftAngle ?? 0f;
        bool bump = MathF.Abs(lift - _liftSnapshot) > LiftBumpRad
                    || MathF.Abs(robot.Sensors.OffTreads.RawGyro.Y) > GyroBumpRadPerSec
                    || MathF.Abs(robot.Sensors.OffTreads.RawAccel.X - _accelXSnapshot) > AccelBumpMmps2;
        if (bump)
        {
            // 0x005F2208..0x005F2258
            StopActing(keepAction: true, viaCallback: false);      // StopActing(this, 1, 0) 0x005F2208..0x005F2214
            SetPower(true);
            PlayAction(AnimationTrigger.FistBumpSuccess);
            _state = Success;
            return;
        }
        if (!HasCurrentAction)
        {
            // 0x005F23FC..0x005F24A2: the idle action ended without a bump
            SetPower(true);
            _retries++;
            if (_retries == 1)
            {
                PlayAction(AnimationTrigger.FistBumpRequestRetry);
                _state = Idle;
            }
            else
            {
                PlayAction(AnimationTrigger.FistBumpLeftHanging);
                _state = LeftHanging;
            }
        }
    }

    /// <summary>
    /// <c>TurnTowardsFaceAction(robot, 0, Radians(pi), ...)</c> with the last-face-pose vtable and +0x193 = 1; its completion callback tests the result
    /// (0x005F27EE..0x005F2814): <c>NO_FACE</c> stamps the search start (+0x120) and enters state 2, every other result enters state 3. The remaining
    /// constructor arguments (the say-name flag) are not in the inventory: false, as the M13-014 record has it for the other callers that set +0x193.
    /// </summary>
    private void StartTurn(bool stateOne = true)
    {
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
        StartAction("TurnTowardsFaceAction(last face, pi)", run, result =>
        {
            if (result == NoFaceResult) { _searchStart = NowSeconds; _state = SearchState; }
            else _state = Request;
        }, uint.MaxValue);
    }

    private Func<CancellationToken, Task<uint>>? _turnRunner(bool stateOne)
    {
        if (TurnTowardsLastFace is { } t) return t;
        if (_vision is not { } v) return null;
        return async ct =>
        {
            using var turn = new TurnTowardsLastFacePoseAction(v, MaxTurnRad, sayName: false) { RequireVerifiedFace = stateOne };      // +0x193 = 1 is state 1's store only (0x005F20B8)
            return (uint)await turn.RunAsync(ct).ConfigureAwait(false);
        };
    }

    private Func<CancellationToken, Task>? PlaceObjectOnGroundRunner()
    {
        if (PlaceObjectOnGround is { } p) return p;
        if (_manipulation is not { } m) return null;
        return ct => new PlaceObjectOnGroundAction(m).RunAsync(ct);
    }

    /// <summary>A <c>TriggerAnimationAction(trigger, 1, 1, 0, 60.0, 0)</c> started with <c>StartActing</c> and an empty completion.</summary>
    private void PlayAction(AnimationTrigger trigger) => PlayTrigger(trigger, () => { }, AnimationTrack.None, AnimationTimeoutSec);

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

    /// <summary>Disables (false) or re-enables (true) the lift and head power, lift first (0x005F2704, 0x005F270E).</summary>
    private void SetPower(bool enable)
    {
        if (EnableLiftPower is { } lift) lift(enable);
        else ReportMissing("BehaviorFistBump EnableLiftPower(bool) (0x005F2704): the MovementComponent function's body is not in the inventory; nothing is sent (EnableLiftPower seam)");
        if (EnableHeadPower is { } head) head(enable);
        else ReportMissing("BehaviorFistBump EnableHeadPower(bool) (0x005F270E): the MovementComponent function's body is not in the inventory; nothing is sent (EnableHeadPower seam)");
    }

    /// <summary><c>IBehavior::BehaviorObjectiveAchieved(BehaviorObjective)</c>: its body is not in the inventory, so only the call is traced (as ReactToCliff does).</summary>
    private void ObjectiveAchieved(int objective)
    {
        Log($"BehaviorObjectiveAchieved({objective})");
        ReportMissing("IBehavior::BehaviorObjectiveAchieved(BehaviorObjective) body (0x005F2288): not in the inventory; BehaviorFistBump only logs the call");
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
        SetPower(true);
        ResetTrigger(false);
    }
}
