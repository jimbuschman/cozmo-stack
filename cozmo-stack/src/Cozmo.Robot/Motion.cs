using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>Why a motion call finished.</summary>
public enum MotionResult
{
    /// <summary>
    /// Done. For a head or lift move this is the engine's action success: the motor was already in position
    /// (nothing sent), or the robot acknowledged the action and then reported the motor in position and stopped
    /// (M4-016). For a wheel command and StopAll it is this stack's confirmation against the reported state.
    /// </summary>
    Acknowledged,
    /// <summary>Nothing came back in time. The command was sent; whether it took effect is unknown.</summary>
    TimedOut,
    /// <summary>Refused before anything was sent, because the robot was not in a state to accept it.</summary>
    Refused,
    /// <summary>The engine's action failure: see <see cref="MotionOutcome.EngineResult"/> (0x04000004, 0x03000016, 0x03000018).</summary>
    Failed,
}

/// <summary>Outcome of a motion call, with whatever the robot said about it.</summary>
public sealed record MotionOutcome(MotionResult Result, string Detail)
{
    public bool Ok => Result == MotionResult.Acknowledged;
    /// <summary>The engine's ActionResult code for a <see cref="MotionResult.Failed"/> move, else null.</summary>
    public uint? EngineResult { get; init; }
    public override string ToString() => $"{Result}: {Detail}";
}

/// <summary>
/// Cozmo's motors: wheels, head and lift - the engine's MovementComponent and its head and lift actions (M4).
///
/// Head and lift moves follow the game-message path (M4-003, MD1): the caller's speed, acceleration and duration
/// go into the action, with the values the original app passes as the defaults (head 10/20, lift 10/20, duration
/// 0). Nothing is sent when the motor is already in position; otherwise the move completes when the robot has
/// acknowledged its action id and then reports the motor in position and stopped (M4-016).
///
/// Direct drive (DriveWheels, MoveHead, MoveLift) locks the matching animation track while its speed is non-zero,
/// which sends DisableAnimTracks, and unlocks it at zero, which sends EnableAnimTracks (M4-014).
///
/// There is no calibration gate on direct motion (M4-004): the engine has none. One policy of this stack sits on top:
/// a wheel command is confirmed against the reported wheel speeds (M4-006).
/// </summary>
public sealed class CozmoMotion
{
    private readonly CozmoRobot _robot;
    private readonly object _gate = new();

    internal CozmoMotion(CozmoRobot robot)
    {
        _robot = robot;
        // fidelity: M4-003, M4-016
        // Batch 2: the head/lift actions are IActionRunner subclasses queued on Robot::Update's ActionList
        // (0x005140BC), so the list tick drives their IAction::UpdateInternal timeout/CheckIfDone
        // (0x00540D1A..0x00540E80). The old per-tick ActionRunnerUpdate hook is gone.
    }

    // ------------------------------------------------------------ MovementComponent state

    // fidelity: M4-005
    /// <summary>MC+8: 0 at construction (0x0063DA7C).</summary>
    private byte _actionIdCounter;

    /// <summary>Track bits (M4-014): HEAD 1, LIFT 2, BODY 4.</summary>
    public const byte HeadTrack = 1, LiftTrack = 2, BodyTrack = 4;

    /// <summary>The lock sets per track bit (MC+0x38.., one per bit); each holds the ids of whoever locked it.</summary>
    private readonly HashSet<string>[] _trackLocks = { new(), new(), new() };
    /// <summary>The direct drive's lock holder, MC+0xC4.</summary>
    private const string DirectDriveWho = "MovementComponent.DirectDrive";
    /// <summary>MC+0xB8 (body), +0xB9 (head), +0xBA (lift): this direct drive holds the track.</summary>
    private bool _ddBody, _ddHead, _ddLift;
    /// <summary>
    /// MC+0xD4, "direct drive is disabled": 0 from the constructor (0x0063DB0C). Nothing in this stack sets it (its
    /// writers are not in the M4 inventory), so it stays 0.
    /// </summary>
    private bool _directDriveDisabled;

    // fidelity: M4-001
    /// <summary>Robot+0x2FC: −0.436332 from the constructor (MA22, 0x0051007C..0x00510086); written by RS6.</summary>
    private float _headAngle = MinHeadAngleRad;
    /// <summary>MC+0xA, +0xB, +0xC: head, lift and body moving (M2 status bits: !HEAD_IN_POS, !LIFT_IN_POS, ARE_WHEELS_MOVING).</summary>
    private bool _headMoving, _liftMoving, _bodyMoving;
    /// <summary>Robot+0x300: the lift angle from the last handled state (RS7), radians; null before any.</summary>
    private float? _liftAngle;

    private readonly List<MoveAction> _actions = new();

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26, CC27): the action-id and lock-owner
    /// counters at 0, no track locks, direct drive holding nothing, the head at −25° and no motor moving. A move in
    /// flight ends as timed out without anything sent.
    /// </summary>
    internal void ResetToConstructed()
    {
        MoveAction[] ending;
        lock (_gate)
        {
            _actionIdCounter = 0;
            foreach (var s in _trackLocks) s.Clear();
            _ddBody = _ddHead = _ddLift = false;
            _directDriveDisabled = false;
            _headAngle = MinHeadAngleRad;
            _headMoving = _liftMoving = _bodyMoving = false;
            _liftAngle = null;
            ending = _actions.ToArray();
            _actions.Clear();
        }
        // M1-025: a move in flight ends as timed out without anything sent. The queued runners are cancelled and
        // deleted (which runs their destructor tail); the Done tasks already cleared above complete here.
        _robot.Engine.Robot?.ActionList.Cancel(-1);
        foreach (var a in ending) a.Done.TrySetResult(new MotionOutcome(MotionResult.TimedOut, $"{a.What}: the robot was removed"));
    }

    // fidelity: M4-003
    /// <summary>
    /// A new IActionRunner tag (+0x60) from the one global counter every action draws from, as text: the lock
    /// owner key <c>to_string(+0x60)</c> (LockTracks 0x00540584..0x0054058a via 0x004f0f4c). C1/U5: the counter
    /// is <c>IActionRunner::sTagCounter</c>, global, not per robot. An action that is not a head or lift move (an
    /// animation action, M8-007) takes its owner key here so no two actions share one.
    /// </summary>
    internal string NextActionTag() => ActionRunnerTagCounter.Global.NextIdTag().ToString();

    // fidelity: M4-005
    /// <summary>
    /// MA8: the u8 counter at MC+8 is pre-incremented (MoveHeadToAngle 0x006407D8..0x006407E6, MoveLiftToHeight
    /// 0x0064070C..0x0064071A, GetNextMotorActionID 0x006406EC..0x006406F4), so ids run 1..255, 0, 1, ... and head,
    /// lift and body share it.
    /// </summary>
    internal byte NextActionId()
    {
        lock (_gate) return unchecked(++_actionIdCounter);
    }

    // fidelity: M4-001
    /// <summary>
    /// The engine's head limits, written as the engine's float bits. MoveHeadToAngleAction clips a rescaled commanded
    /// angle against them with warnings (MA9, 0x00547F44 min / 0x00547FC2 max; the 2026-09-29 audit corrected the
    /// decimal-rounded 0xBEDF66E8/0x3F46D3FA to 0xBEDF66F3/0x3F46D3F2), and RS6 clamps a reported one (M2 App. B)
    /// against the OOB thresholds.
    /// </summary>
    public static readonly float MinHeadAngleRad = BitConverter.Int32BitsToSingle(unchecked((int)0xBEDF66F3));   // -25 degrees
    public static readonly float MaxHeadAngleRad = BitConverter.Int32BitsToSingle(unchecked((int)0x3F46D3F2));   // +44.5 degrees
    /// <summary>RS6: a reported head angle below this (−28°, 0xBEFA35DD) is stored as −25°.</summary>
    internal static readonly float ReportedHeadLowOob = BitConverter.Int32BitsToSingle(unchecked((int)0xBEFA35DD));
    /// <summary>RS6: a reported head angle above this (47.5°, 0x3F543B67) is stored as 44.5°.</summary>
    internal static readonly float ReportedHeadHighOob = BitConverter.Int32BitsToSingle(unchecked((int)0x3F543B67));

    // fidelity: M4-002
    /// <summary>
    /// The lift preset table at 0x00C54684 (MA14): key 0 LowDock 32, key 1 HighDock 76, key 2 HeightCarry 92, key 3
    /// OutOfFOV −1. MoveLiftToHeightAction::Init clamps a height in [0, ∞) outside [32, 92] with a warning, and sends
    /// a negative height to whichever of preset 0 and preset 2 is nearer the current height (MA13).
    /// </summary>
    public const float LowDockHeightMm = 32.0f, HighDockHeightMm = 76.0f, CarryHeightMm = 92.0f, OutOfFovHeightMm = -1.0f;
    public const float MinLiftHeightMm = LowDockHeightMm;
    public const float MaxLiftHeightMm = CarryHeightMm;
    /// <summary>
    /// Wheel speed the robot is documented to accept, from PyCozmo. Beyond this the robot clamps, it does
    /// not fault, so this is advisory. The engine's own ceiling was not located.
    /// </summary>
    public const float MaxWheelSpeedMmps = 200.0f;

    /// <summary>True while the robot says at least one wheel is turning.</summary>
    public bool WheelsMoving => _robot.State.Latest?.Has(RobotStatusFlag.AreWheelsMoving) ?? false;
    /// <summary>Left and right wheel speed in mm/s, as the robot reports them.</summary>
    public (float Left, float Right) WheelSpeeds =>
        _robot.State.Latest is { } s ? (s.LwheelSpeedMmps, s.RwheelSpeedMmps) : (0f, 0f);

    /// <summary>Robot+0x2FC as the engine keeps it: −25° until the head is calibrated, then the RS6-clamped report.</summary>
    public float HeadAngleRad { get { lock (_gate) return _headAngle; } }
    /// <summary>MC+0xA/+0xB/+0xC: whether the head, lift and body are moving, from the last handled state.</summary>
    public bool HeadMoving { get { lock (_gate) return _headMoving; } }
    public bool LiftMoving { get { lock (_gate) return _liftMoving; } }
    public bool BodyMoving { get { lock (_gate) return _bodyMoving; } }

    /// <summary>MC+0xB8 || +0xB9 || +0xBA: direct drive holds a track (M4 MA1; read by the M10 C7 unlock rule).</summary>
    internal bool DirectDriveHoldsAnyTrack { get { lock (_gate) return _ddBody || _ddHead || _ddLift; } }
    /// <summary>MC+0xD4, "direct drive is disabled" (M4 MA1; read by the M10 C7 unlock rule).</summary>
    internal bool DirectDriveDisabled { get { lock (_gate) return _directDriveDisabled; } }

    /// <summary>The tracks currently locked by anyone, as a mask (M4-014).</summary>
    public byte LockedTracks
    {
        get
        {
            lock (_gate)
            {
                byte m = 0;
                for (int b = 0; b < 3; b++) if (_trackLocks[b].Count > 0) m |= (byte)(1 << b);
                return m;
            }
        }
    }

    // fidelity: M4-004
    /// <summary>
    /// The engine has no calibration gate on direct motion (MA21): the only readers of Robot+0x314/+0x315 are
    /// 0x00511E1C, 0x00512378, 0x0051335E and 0x005151A6, and none of them gates DriveWheels, MoveHead or MoveLift;
    /// the engine reacts to calibration instead (MA20). The stack's old readiness gate - a state must already exist and
    /// the head and lift must have finished calibrating - is removed. The <c>requireCalibration</c> parameter is kept
    /// only so existing callers compile; it has no effect.
    /// </summary>
    private void Log(string line) => _robot.Engine.Log(line);

    // ------------------------------------------------------------------- track locks

    // fidelity: M4-014
    /// <summary>
    /// MovementComponent::LockTracks (MA3, 0x006400D0..0x00640188): a lock entry per bit; the bits whose lock set
    /// reaches size 1 are collected, and a non-zero mask sends DisableAnimTracks 0x9D {mask}, reliable.
    /// </summary>
    private void LockTracksLocked(byte mask, string who)
    {
        byte newly = 0;
        for (int b = 0; b < 3; b++)
        {
            if ((mask & (1 << b)) == 0) continue;
            _trackLocks[b].Add(who);
            if (_trackLocks[b].Count == 1) newly |= (byte)(1 << b);
        }
        if (newly != 0) _robot.SendMessage(new DisableAnimTracks { Field0 = newly });
    }

    // fidelity: M4-014
    /// <summary>
    /// MovementComponent::UnlockTracks (MA3, 0x0063FE92..0x0063FFB4): the entry is erased; the bits whose set becomes
    /// empty are collected, and a non-zero mask sends EnableAnimTracks 0x9E {mask}, reliable.
    /// </summary>
    private void UnlockTracksLocked(byte mask, string who)
    {
        byte freed = 0;
        for (int b = 0; b < 3; b++)
        {
            if ((mask & (1 << b)) == 0) continue;
            if (_trackLocks[b].Remove(who) && _trackLocks[b].Count == 0) freed |= (byte)(1 << b);
        }
        if (freed != 0) _robot.SendMessage(new EnableAnimTracks { Field0 = freed });
    }

    private bool AreAllTracksLockedLocked(byte mask)
    {
        for (int b = 0; b < 3; b++)
            if ((mask & (1 << b)) != 0 && _trackLocks[b].Count == 0) return false;
        return true;
    }

    private bool IsTrackLockedLocked(byte mask)
    {
        for (int b = 0; b < 3; b++)
            if ((mask & (1 << b)) != 0 && _trackLocks[b].Count > 0) return true;
        return false;
    }

    // fidelity: M8-007
    /// <summary>
    /// <c>MovementComponent::AreAnyTracksLocked(mask)</c> 0x00640098's reader at <c>0x00540440</c>: any bit
    /// of <paramref name="mask"/> whose lock set is non-empty. <c>IActionRunner::Update</c> 0x00540370
    /// fails an action whose required tracks are locked with 0x03000019 (0x00540572..0x0054057C); it does not
    /// retry (M8-007, M4-003).
    /// </summary>
    public bool AreAnyTracksLocked(byte mask) { lock (_gate) return IsTrackLockedLocked(mask); }

    // fidelity: M4-003
    /// <summary>
    /// <c>MovementComponent::AreAllTracksLockedBy(mask, owner)</c> (0x0064030C): every bit of
    /// <paramref name="mask"/> has <paramref name="owner"/> in its lock set. The ~IActionRunner stop gate
    /// (0x00541138/0x0054115E) only stops a track this action holds.
    /// </summary>
    public bool AreAllTracksLockedBy(byte mask, string owner)
    {
        lock (_gate)
        {
            for (int b = 0; b < 3; b++)
                if ((mask & (1 << b)) != 0 && !_trackLocks[b].Contains(owner)) return false;
            return true;
        }
    }

    /// <summary><c>MovementComponent::LockTracks</c> 0x00640098: one owner entry per bit in the multiset.</summary>
    public void LockTracks(byte mask, string who) { lock (_gate) LockTracksLocked(mask, who); }

    /// <summary><c>MovementComponent::UnlockTracks</c> 0x0063fe5c: remove the owner's entry per bit.</summary>
    public void UnlockTracks(byte mask, string who) { lock (_gate) UnlockTracksLocked(mask, who); }

    // fidelity: M10-004
    /// <summary>
    /// <c>MovementComponent::CompletelyUnlockAllTracks</c> (C1, 0x00640F84, export 0x640F85): walk the per-track lock
    /// sets in ascending index; a track whose set is empty is skipped entirely; every non-empty set is cleared,
    /// whoever holds it, with no filter by lock name and no test of the direct-drive flags. For each cleared track it
    /// logs "Unlocking track %s" with the mask <c>1&lt;&lt;k</c> and sends one <c>EnableAnimTracks</c> whose 1-byte
    /// payload is the track index <c>k</c>, not the mask, reliable and non-hot. This stack models only the three
    /// motion tracks (head, lift, body; M4-014), so indices 0..2 are the only non-empty ones here; the engine walks
    /// 0..7. What the firmware does with an index instead of a mask is HARDWARE_ONLY (M10-004 <c>unresolved</c>): the
    /// byte is kept exact. Unlike <see cref="UnlockTracks"/>, it neither erases one holder nor accumulates one mask.
    /// </summary>
    public void CompletelyUnlockAllTracks()
    {
        lock (_gate)
        {
            for (int k = 0; k < _trackLocks.Length; k++)
            {
                if (_trackLocks[k].Count == 0) continue;                    // empty set: no log, no clear, no message
                Log($"MovementComponent.UnlockAllTracks: Unlocking track {TrackName(k)}");
                _trackLocks[k].Clear();
                _robot.SendMessage(new EnableAnimTracks { Field0 = (byte)k });   // the index k, not 1<<k
            }
        }
    }

    /// <summary>The name <c>EnumToString(AnimTrackFlag)</c> gives the mask <c>1&lt;&lt;k</c> (C1).</summary>
    private static string TrackName(int k) => k switch
    {
        0 => "Head",
        1 => "Lift",
        2 => "Body",
        _ => k.ToString(),
    };

    /// <summary>
    /// The motion-track mask for an animation's tracks: the engine's <c>IActionRunner</c> +0x54 mask uses the
    /// same head/lift/body bits (M4-014 <see cref="HeadTrack"/>/<see cref="LiftTrack"/>/<see cref="BodyTrack"/>);
    /// face/audio/lights/events have no motion track.
    /// </summary>
    public static byte MaskFor(Animation.AnimationTrack tracks)
    {
        byte m = 0;
        if ((tracks & Animation.AnimationTrack.Head) != 0) m |= HeadTrack;
        if ((tracks & Animation.AnimationTrack.Lift) != 0) m |= LiftTrack;
        if ((tracks & Animation.AnimationTrack.Body) != 0) m |= BodyTrack;
        return m;
    }

    // fidelity: M4-014
    /// <summary>
    /// DirectDriveCheckSpeedAndLockTracks (MA2, 0x0063EFB0..0x0063F0B8): |speed| &lt; 1e-5 (0x0063F0F8) clears the flag
    /// and unlocks the mask if all its tracks are locked; otherwise it sets the flag and locks the mask unless all its
    /// tracks are already locked.
    /// </summary>
    private void DirectDriveCheckSpeedAndLockTracksLocked(float speed, ref bool flag, byte mask)
    {
        if (Math.Abs(speed) < 1e-5f)
        {
            flag = false;
            if (AreAllTracksLockedLocked(mask)) UnlockTracksLocked(mask, DirectDriveWho);
        }
        else
        {
            flag = true;
            if (!AreAllTracksLockedLocked(mask)) LockTracksLocked(mask, DirectDriveWho);
        }
    }

    // fidelity: M4-014
    /// <summary>
    /// The direct-drive gates of the game DriveWheels/MoveHead/MoveLift handlers (MA1, MA1-lift): ignored while direct
    /// drive is disabled (MC+0xD4); ignored when this direct drive does not hold the track and someone holds it.
    /// </summary>
    private MotionOutcome? DirectDriveRefused(bool holds, byte mask, string what, string lockedName)
    {
        if (_directDriveDisabled)
        {
            Log($"info: Ignoring {what} message while direct drive is disabled");
            return new MotionOutcome(MotionResult.Refused, $"{what} ignored: direct drive is disabled");
        }
        if (!holds && IsTrackLockedLocked(mask))
        {
            Log($"info: MovementComponent.{lockedName}");
            return new MotionOutcome(MotionResult.Refused, $"{what} ignored: {lockedName}");
        }
        return null;
    }

    // ------------------------------------------------------------------- wheels

    // fidelity: M4-014, M4-006
    /// <summary>
    /// The game DriveWheels (MA1, 0x0063ED82..0x0063EE9A): the direct-drive gates, the BODY track lock for |l|+|r|
    /// (0x0063EE32..0x0063EE56), then DriveWheels verbatim, reliable. Policy M4-006 then confirms it against the wheel
    /// speeds the robot reports (35 % / 5 mm/s); the engine has no counterpart.
    /// </summary>
    public async Task<MotionOutcome> DriveWheelsAsync(float leftMmps, float rightMmps,
                                                      float leftAccelMmps2 = 0f, float rightAccelMmps2 = 0f,
                                                      TimeSpan? confirmWithin = null,
bool requireCalibration = true)
    {
        lock (_gate)
        {
            if (DirectDriveRefused(_ddBody, BodyTrack, "DriveWheels", "WheelsLocked") is { } no) return no;
            DirectDriveCheckSpeedAndLockTracksLocked(Math.Abs(leftMmps) + Math.Abs(rightMmps), ref _ddBody, BodyTrack);
            _robot.SendMessage(new DriveWheels(leftMmps, rightMmps, leftAccelMmps2, rightAccelMmps2));
        }

        var outcome = await AwaitState(
            s => WheelMatches(s.LwheelSpeedMmps, leftMmps) && WheelMatches(s.RwheelSpeedMmps, rightMmps),
            confirmWithin ?? TimeSpan.FromSeconds(2));

        var (l, r) = WheelSpeeds;
        return outcome
            ? new MotionOutcome(MotionResult.Acknowledged,
                $"robot reports wheels at {l:F0}/{r:F0} mm/s, asked for {leftMmps:F0}/{rightMmps:F0}")
            : new MotionOutcome(MotionResult.TimedOut,
                $"robot reports wheels at {l:F0}/{r:F0} mm/s, asked for {leftMmps:F0}/{rightMmps:F0}");
    }

    // fidelity: M4-006
    /// <summary>Policy M4-006: whether one wheel's reported speed matches what it was asked for.</summary>
    internal static bool WheelMatches(float reported, float requested)
    {
        const float stopped = 5f;
        if (Math.Abs(requested) <= 0.01f) return Math.Abs(reported) <= stopped;
        if (Math.Sign(reported) != Math.Sign(requested)) return false;
        float tolerance = Math.Max(stopped, Math.Abs(requested) * 0.35f);
        return Math.Abs(Math.Abs(reported) - Math.Abs(requested)) <= tolerance;
    }

    /// <summary>Stops the wheels with a game DriveWheels of zero (which unlocks the BODY track). Does not touch head or lift.</summary>
    public Task<MotionOutcome> StopWheelsAsync(TimeSpan? confirmWithin = null)
        => DriveWheelsAsync(0f, 0f, confirmWithin: confirmWithin, requireCalibration: false);

    // --------------------------------------------------------------- head and lift

    // fidelity: M4-003
    /// <summary>MD1/MA11: the head speed the original app passes (Unity Robot.cs:1443-1450).</summary>
    public const float DefaultHeadSpeedRadPerSec = 10f;
    /// <summary>MD1/MA11: the head acceleration the original app passes.</summary>
    public const float DefaultHeadAccelRadPerSec2 = 20f;
    /// <summary>MD1/MA11: the lift speed the original app passes (Robot.cs:1638-1646).</summary>
    public const float DefaultLiftSpeedRadPerSec = 10f;
    /// <summary>MD1/MA11: the lift acceleration the original app passes.</summary>
    public const float DefaultLiftAccelRadPerSec2 = 20f;
    /// <summary>MA9: MoveHeadToAngleAction's constructor defaults, which only engine-internal callers get: 15 rad/s.</summary>
    public const float ActionDefaultHeadSpeedRadPerSec = 15f;
    /// <summary>MA9: 20 rad/s².</summary>
    public const float ActionDefaultHeadAccelRadPerSec2 = 20f;

    /// <summary>MA10: the game SetHeadAngle builds its action with tolerance 0.0349066 rad, whose engine float is
    /// 0x3D0EFA35 (M4-003 unresolved; the old decimal rounded to 0x3D0EFA39).</summary>
    internal static readonly float GameHeadToleranceRad = BitConverter.Int32BitsToSingle(unchecked((int)0x3D0EFA35));
    /// <summary>MA9: the head tolerance minimum, 2° (0x0054803E..0x005480AC), the same engine float 0x3D0EFA35.</summary>
    internal static readonly float MinHeadToleranceRad = BitConverter.Int32BitsToSingle(unchecked((int)0x3D0EFA35));
    /// <summary>MA12: the game SetLiftHeight builds its action with tolerance 5.0 mm.</summary>
    internal const float GameLiftToleranceMm = 5.0f;

    // fidelity: M4-003
    /// <summary>
    /// MA12: the M12 CarryingComponent interface. True while an object is on the lift
    /// (<c>CarryingComponent(+0x284)+8 != −1</c>). Set by the manipulation layer when it is built.
    /// </summary>
    internal Func<bool>? IsCarryingObject { get; set; }
    /// <summary>
    /// MA12: the M12 interface that runs <c>PlaceObjectOnGroundAction</c>. Set by the manipulation layer.
    /// </summary>
    internal Func<Task<MotionOutcome>>? PlaceObjectOnGroundAsync { get; set; }
    /// <summary>MA15: IsHeadInPosition adds 1e-5 to the tolerance.</summary>
    internal const float HeadInPositionSlack = 1e-5f;
    /// <summary>MA17: StoppedMakingProgress.</summary>
    public const uint ResultStoppedMakingProgress = 0x04000004;
    /// <summary>MA17: the send failed.</summary>
    public const uint ResultSendFailed = 0x03000016;
    /// <summary>M4-003: IActionRunner::Update's required-tracks-locked failure (0x00540572..0x0054057C).</summary>
    public const uint ResultTracksLocked = 0x03000019;
    /// <summary>M4-016: IAction::UpdateInternal's timeout failure (0x00540E80).</summary>
    public const uint ResultTimedOut = 0x03000018;
    /// <summary>M4-016: the IAction timeout slot's default, 30.0 s (0x0052B0C2), not 5 s.</summary>
    internal static readonly TimeSpan DefaultActionTimeout = TimeSpan.FromSeconds(30);

    // fidelity: M4-001
    /// <summary>
    /// <c>Radians::rescale</c> (0x0084C87C), reached from the Radians ctor 0x0084C832: bring an angle into (−π, π].
    /// The engine uses the <c>ceil(v/2π − 0.5)</c> shortcut at |v| ≥ 10 (0x0084C8AE..0x0084C8BA) and a 2π loop
    /// below it (0x0084C8C4..0x0084C900, 0x0084C88A..0x0084C8A8). The ctor rescales every Radians the engine
    /// builds, so MoveHeadToAngleAction clips the rescaled angle, not the raw command.
    ///
    /// The shortcut keeps the engine's <c>vcvt.s32.f32</c> / <c>vcvt.f32.s32</c> round trip at
    /// 0x0084C91E..0x0084C922: the ceil result is converted to s32 and back to f32 before the multiply. ARM's
    /// conversion saturates out-of-range operands to INT_MIN/INT_MAX and maps NaN to 0, so at ±inf the turn count
    /// is a finite float and the result stays ±inf — which the clip then catches — instead of becoming
    /// inf − inf = NaN; huge finite values keep their magnitude instead of being reduced by the (unrepresentable)
    /// turn count. For the small turn counts the game path uses the round trip changes nothing.
    /// </summary>
    internal static float RescaleRadians(float value)
    {
        if (value <= -MathF.PI || value > MathF.PI)
        {
            if (MathF.Abs(value) >= 10f)
            {
                float turns = MathF.Ceiling(value / (2f * MathF.PI) - 0.5f);
                turns = ArmFloatToIntToFloat(turns);
                value -= turns * (2f * MathF.PI);
            }
            else
            {
                while (value <= -MathF.PI) value += 2f * MathF.PI;
                while (value > MathF.PI) value -= 2f * MathF.PI;
            }
        }
        return value;
    }

    // fidelity: M4-001
    /// <summary>
    /// The engine's <c>vcvt.s32.f32</c> then <c>vcvt.f32.s32</c> (0x0084C91E..0x0084C922): round toward zero,
    /// saturating out of the s32 range to INT_MIN/INT_MAX; NaN becomes 0. Used by <see cref="RescaleRadians"/>.
    /// </summary>
    internal static float ArmFloatToIntToFloat(float x)
    {
        int i;
        if (float.IsNaN(x)) i = 0;
        else if (x >= 2147483648f) i = int.MaxValue;      // 2^31: vcvt saturates
        else if (x < -2147483648f) i = int.MinValue;
        else i = (int)x;                                  // vcvt rounds toward zero
        return i;
    }

    // fidelity: M4-001
    /// <summary>The 1e-5 near tolerance, the engine float 0x3727C5AC (0x0084CC64/0x0084CC68).</summary>
    internal static readonly float NearTolerance = BitConverter.Int32BitsToSingle(unchecked((int)0x3727C5AC));

    // fidelity: M4-001
    /// <summary>
    /// <c>Radians::IsNear</c> (0x0084CC0A): <c>|rescale(rescale(a) − b)| &lt; |tolerance|</c>, a strict &lt;
    /// (0x0084CC3C..0x0084CC58).
    /// </summary>
    internal static bool IsNear(float a, float b, float tolerance)
    {
        float d = RescaleRadians(RescaleRadians(a) - b);
        return MathF.Abs(d) < MathF.Abs(tolerance);
    }

    // fidelity: M4-001
    /// <summary>
    /// <c>Anki::operator&gt;</c> (0x0084CC90..0x0084CCD0): <c>a − b &gt; 0 &amp;&amp; !IsNear(a, b, 1e-5)</c>. The clip
    /// at 0x00547F44/0x00547FC2 calls <c>operator&lt;</c> for the min and <c>operator&gt;</c> for the max.
    /// </summary>
    internal static bool RadiansGreaterThan(float a, float b) => a - b > 0f && !IsNear(a, b, NearTolerance);

    // fidelity: M4-001
    /// <summary><c>Anki::operator&lt;</c> (0x0084CD12): <c>operator&gt;(b, a)</c>.</summary>
    internal static bool RadiansLessThan(float a, float b) => RadiansGreaterThan(b, a);

    // fidelity: M4-001, M4-003, M4-016
    /// <summary>
    /// The game SetHeadAngle (MA10): MoveHeadToAngleAction(angle, tolerance 0.0349066, variability 0), whose speed,
    /// acceleration and duration are the caller's. The Radians ctor rescales the angle into (−π, π] first
    /// (0x0084C832 → 0x0084C87C); the ctor then clips the rescaled angle to the head limits with a warning (MA9);
    /// the tolerance is at least 2°; variability 0 leaves the angle as it is. Init sends nothing when the head is
    /// already within tolerance + 1e-5 of the target (MA15), otherwise MoveHeadToAngle with the next action id. The
    /// move completes on the matching ack followed by the head in position and stopped; it fails with 0x04000004 if
    /// the head stops out of position after having moved, and with 0x03000016 if the send fails (MA16, MA17). The
    /// IAction timeout defaults to the engine's 30.0 s slot (M4-016; 0x0052B0C2).
    /// </summary>
    public Task<MotionOutcome> SetHeadAngleAsync(float radians,
                                                 float maxSpeedRadPerSec = DefaultHeadSpeedRadPerSec,
                                                 float accelRadPerSec2 = DefaultHeadAccelRadPerSec2,
                                                 float durationSec = 0f,
                                                 TimeSpan? timeout = null, bool requireCalibration = true)
    {
        // fidelity: M4-001
        // Radians ctor 0x0084C832 rescales first (0x0084C87C); so 99 rad → 99 − 16·2π = −1.5310 and clips to the
        // min with AngleTooLow, and −99 rad → −99 + 16·2π = +1.5310 and clips to the max with AngleTooHigh.
        // The clip is Anki::operator< (0x0084CD12) for the min and operator> (0x0084CC90..0x0084CCD0) for the max:
        // a − b > 0 and !IsNear(a, b, 1e-5), so a target within 1e-5 past a limit is sent unclipped.
        float target = RescaleRadians(radians);
        if (RadiansLessThan(target, MinHeadAngleRad))
        {
            Log($"warning: MoveHeadToAngleAction.Constructor.AngleTooLow: {radians:F4} rad, clipped to {MinHeadAngleRad}");
            target = MinHeadAngleRad;
        }
        else if (RadiansGreaterThan(target, MaxHeadAngleRad))
        {
            Log($"warning: MoveHeadToAngleAction.Constructor.AngleTooHigh: {radians:F4} rad, clipped to {MaxHeadAngleRad}");
            target = MaxHeadAngleRad;
        }
        float tolerance = Math.Max(GameHeadToleranceRad, MinHeadToleranceRad);
        // fidelity: M4-003, M4-016
        // MA10: the game handler constructs the action and queues it at QueueActionPosition::NOW
        // (ActionList::QueueActionNow 0x0053DCA0). No synchronous Init/Update (Q4): the first ActionList tick
        // promotes it and IAction::UpdateInternal stamps the start, sends in Init and checks the engine clock.
        var a = new MoveAction(this, isHead: true, target, tolerance, $"head to {target:F3} rad",
                               id => new SetHeadAngle(target, maxSpeedRadPerSec, accelRadPerSec2, durationSec, id));
        a.TimeoutSeconds = (float)(timeout ?? DefaultActionTimeout).TotalSeconds;
        Queue(a);
        return a.Done.Task;
    }

    // fidelity: M4-002, M4-003, M4-016
    /// <summary>
    /// The game SetLiftHeight (MA12): MoveLiftToHeightAction(h, tolerance 5.0 mm, variability 0) with the caller's
    /// speed, acceleration and duration. A height of exactly 32 mm while carrying runs PlaceObjectOnGroundAction
    /// instead (MA12, through the M12 <see cref="IsCarryingObject"/>/<see cref="PlaceObjectOnGroundAsync"/> seam).
    /// Init: a height in [0, ∞) outside [32, 92] is clamped with a warning; a
    /// negative height goes to the nearer of 32 and 92 to the current height (MA13). Nothing is sent when
    /// |target − height| &lt; tolerance and the lift is not moving (MA15). Completion as for the head, on the lift's
    /// in-position test and MC+0xB (MA17).
    /// The angular-tolerance clip (≥ 1.5°, MA13) cannot bind at 5 mm: the lift's steepest point, 66 mm/rad at 45 mm,
    /// makes 1.5° at most 1.73 mm. Its formula is not in the inventory and is not needed for this API.
    /// <paramref name="suppressTrackLocking"/> is the action's byte +0x56 (M13-028: FlipBlockAction::CheckIfDone sets it to 1 on its queued carry lift, 0x0055F152..0x0055F154):
    /// IActionRunner::Update 0x00540370 branches over both the AreAnyTracksLocked test and LockTracks when it is non-zero (0x00540428..0x00540434 -> 0x00540592), and the action's
    /// end skips the inline lock release in ~IActionRunner (0x0054120C..0x0054122A; 0x005408EC is IActionRunner::UnlockTracks, called only from the IAction constructor and IAction::Reset), so the move runs while another action holds the lift track and sends no Disable/EnableAnimTracks of its own.
    /// </summary>
    public Task<MotionOutcome> SetLiftHeightAsync(float heightMm,
                                                  float maxSpeedRadPerSec = DefaultLiftSpeedRadPerSec,
                                                  float accelRadPerSec2 = DefaultLiftAccelRadPerSec2,
                                                  float durationSec = 0f,
                                                  TimeSpan? timeout = null, bool requireCalibration = true,
                                                  bool suppressTrackLocking = false,
                                                  QueueActionPosition position = QueueActionPosition.Now)
    {
        // fidelity: M4-003
        // MA12: if the height is exactly 32.0 and something is carried, run PlaceObjectOnGroundAction instead.
        if (heightMm == LowDockHeightMm && IsCarryingObject?.Invoke() == true && PlaceObjectOnGroundAsync is not null)
            return PlaceObjectOnGroundAsync();
        float target = heightMm;
        if (target >= 0f && (target < LowDockHeightMm || target > CarryHeightMm))
        {
            float c = Math.Clamp(target, LowDockHeightMm, CarryHeightMm);
            Log($"warning: MoveLiftToHeightAction.Init.InvalidHeight: {heightMm:F1} mm, clamped to {c:F1}");
            target = c;
        }
        else if (target < 0f)
        {
            target = NegativeHeightTarget(CurrentLiftHeightMm());
        }
        // fidelity: M4-003, M4-016
        // MA12: the game handler queues the action at NOW; the ActionList tick drives its timeout/Init/CheckIfDone.
        var a = new MoveAction(this, isHead: false, target, GameLiftToleranceMm, $"lift to {target:F1} mm",
                               id => new SetLiftHeight(target, maxSpeedRadPerSec, accelRadPerSec2, durationSec, id))
                { SuppressTrackLocking = suppressTrackLocking };
        a.TimeoutSeconds = (float)(timeout ?? DefaultActionTimeout).TotalSeconds;
        Queue(a, position);
        return a.Done.Task;
    }

    /// <summary>
    /// Queue a head/lift action on the engine's ActionList (M4-003). The list is the engine's robot+0x250
    /// (EngineRobot.ActionList); the tick at Robot::Update 0x005140BC runs it. The game handlers use NOW; M13-028's
    /// carry lift uses IN_PARALLEL (position 5, 0x0055F164).
    /// </summary>
    private void Queue(MoveAction a, QueueActionPosition position = QueueActionPosition.Now)
    {
        lock (_gate) _actions.Add(a);
        _robot.Engine.Robot!.ActionList.QueueAction(position, a, 0);
    }

    /// <summary>The action's destructor tail completed: drop it from the ack dispatch list and map its stored
    /// engine result to the public <see cref="MotionOutcome"/>.</summary>
    private void ActionEnded(MoveAction a)
    {
        lock (_gate) _actions.Remove(a);
        a.Done.TrySetResult(OutcomeFor(a));
    }

    /// <summary>Map the engine's stored +0x18 result to <see cref="MotionOutcome"/> (the public API is unchanged).</summary>
    private static MotionOutcome OutcomeFor(MoveAction a) => a.State switch
    {
        EngineActionResult.Success => new MotionOutcome(MotionResult.Acknowledged,
            $"{a.What}: robot acknowledged action {a.Id} and reports it in position"),
        EngineActionResult.Timeout => new MotionOutcome(MotionResult.Failed,
            a.Acked ? $"{a.What}: action {a.Id} acknowledged but not in position within {a.TimeoutSeconds:F1}s"
                    : $"{a.What}: no acknowledgement of action {a.Id} within {a.TimeoutSeconds:F1}s")
            { EngineResult = ResultTimedOut },
        ResultStoppedMakingProgress => new MotionOutcome(MotionResult.Failed,
            $"{a.What}: action {a.Id} stopped out of position (StoppedMakingProgress)") { EngineResult = ResultStoppedMakingProgress },
        _ => new MotionOutcome(MotionResult.Failed, $"{a.What}: engine result 0x{a.State:X8}") { EngineResult = a.State },
    };

    // fidelity: M4-002
    /// <summary>
    /// MA13 for a negative height: the nearer of preset 0 (32) and preset 2 (92) to the current height; C5
    /// (0x00549104..0x0054913C: vcmpe s0,s4; it mi; vmovmi): 32 only when strictly nearer, so a tie goes to 92.
    /// </summary>
    internal static float NegativeHeightTarget(float currentHeightMm) =>
        Math.Abs(currentHeightMm - LowDockHeightMm) < Math.Abs(currentHeightMm - CarryHeightMm) ? LowDockHeightMm : CarryHeightMm;

    /// <summary>GetLiftHeight: 66·sin(Robot+0x300) + 45 (RS7); 45 mm (angle 0) before any state.</summary>
    private float CurrentLiftHeightMm()
    {
        lock (_gate) return RobotState.LiftHeightMmFromAngle(_liftAngle ?? 0f);
    }

    // fidelity: M4-014
    /// <summary>
    /// The game MoveHead (MA1, 0x0063F4F6..0x0063F5D2): the direct-drive gates ("HeadLocked"), the HEAD track lock
    /// for the speed, then MoveHead verbatim, reliable. No acknowledgement is defined.
    /// </summary>
    public MotionOutcome MoveHead(float radPerSec, bool requireCalibration = true)
    {
        lock (_gate)
        {
            if (DirectDriveRefused(_ddHead, HeadTrack, "MoveHead", "HeadLocked") is { } no) return no;
            DirectDriveCheckSpeedAndLockTracksLocked(radPerSec, ref _ddHead, HeadTrack);
            _robot.SendMessage(new MoveHead { SpeedRadPerSec = radPerSec });
        }
        return new MotionOutcome(MotionResult.Acknowledged, $"head moving at {radPerSec:F2} rad/s (no ack is defined)");
    }

    // fidelity: M4-014
    /// <summary>
    /// The game MoveLift (MA1-lift, 0x0063F73A..0x0063F92A): the direct-drive gates ("LiftLocked"), the LIFT track
    /// lock (mask 2) for the speed, then MoveLift verbatim, reliable.
    /// </summary>
    public MotionOutcome MoveLift(float radPerSec, bool requireCalibration = true)
    {
        lock (_gate)
        {
            if (DirectDriveRefused(_ddLift, LiftTrack, "MoveLift", "LiftLocked") is { } no) return no;
            DirectDriveCheckSpeedAndLockTracksLocked(radPerSec, ref _ddLift, LiftTrack);
            _robot.SendMessage(new MoveLift { SpeedRadPerSec = radPerSec });
        }
        return new MotionOutcome(MotionResult.Acknowledged, $"lift moving at {radPerSec:F2} rad/s (no ack is defined)");
    }

    // -------------------------------------------------------------- calibration

    // fidelity: M4-012
    /// <summary>
    /// CalibrateMotorAction's CalibrateMotors (MA18): StartMotorCalibration 0x58 {byte0 head, byte1 lift} (MA19). The
    /// engine never sends it automatically; this stack sends it only when a caller asks.
    /// </summary>
    public void RequestMotorCalibration(bool head, bool lift) =>
        _robot.SendMessage(new StartMotorCalibration { Head = head, Lift = lift });

    // ------------------------------------------------------------------ stopping

    // fidelity: M4-015
    /// <summary>The unlock preamble of the Stop functions (MA4, MA5): a track this direct drive holds is unlocked with speed 0.</summary>
    private void StopPreambleLocked(byte tracks)
    {
        if (_directDriveDisabled) return;
        if ((tracks & BodyTrack) != 0 && _ddBody) DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddBody, BodyTrack);
        if ((tracks & HeadTrack) != 0 && _ddHead) DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddHead, HeadTrack);
        if ((tracks & LiftTrack) != 0 && _ddLift) DirectDriveCheckSpeedAndLockTracksLocked(0f, ref _ddLift, LiftTrack);
    }

    // fidelity: M4-015
    /// <summary>MovementComponent::StopHead (MA4, 0x00640A08..0x00640AF6): the preamble, then MoveHead{0}, reliable.</summary>
    public void StopHead()
    {
        lock (_gate)
        {
            StopPreambleLocked(HeadTrack);
            _robot.SendMessage(new MoveHead { SpeedRadPerSec = 0f });
        }
    }

    // fidelity: M4-015
    /// <summary>MovementComponent::StopLift (MA4, 0x00640B3C..0x00640C2A): the preamble, then MoveLift{0}, reliable.</summary>
    public void StopLift()
    {
        lock (_gate)
        {
            StopPreambleLocked(LiftTrack);
            _robot.SendMessage(new MoveLift { SpeedRadPerSec = 0f });
        }
    }

    // fidelity: M4-015
    /// <summary>MovementComponent::StopBody (MA4, 0x00640C70..0x00640E4E): the preamble, then DriveWheels{0,0,0,0}, reliable.</summary>
    public void StopBody()
    {
        lock (_gate)
        {
            StopPreambleLocked(BodyTrack);
            _robot.SendMessage(new DriveWheels(0f, 0f, 0f, 0f));
        }
    }

    // fidelity: M4-007
    /// <summary>
    /// MovementComponent::StopAllMotors (MA5, 0x0063FBD8..0x0063FE10 → 0x0064099C..0x006409C2): the unlock preamble for
    /// all three tracks, then only StopAllMotors 0x3B. The engine sends no DriveWheels here.
    /// </summary>
    public void StopAllMotors()
    {
        lock (_gate)
        {
            StopPreambleLocked(BodyTrack | HeadTrack | LiftTrack);
            _robot.SendMessage(new StopAllMotors());
        }
    }

    // fidelity: M4-007
    /// <summary>
    /// <see cref="StopAllMotors"/>, then this stack's wait for the robot to report the wheels stopped. Never gated on
    /// calibration.
    /// </summary>
    public async Task<MotionOutcome> StopAllAsync(TimeSpan? confirmWithin = null)
    {
        StopAllMotors();

        bool stopped = await AwaitState(
            s => !s.Has(RobotStatusFlag.AreWheelsMoving) && Math.Abs(s.LwheelSpeedMmps) < 1f && Math.Abs(s.RwheelSpeedMmps) < 1f,
            confirmWithin ?? TimeSpan.FromSeconds(2));

        var (l, r) = WheelSpeeds;
        return stopped
            ? new MotionOutcome(MotionResult.Acknowledged, "robot reports all wheels stopped")
            : new MotionOutcome(MotionResult.TimedOut, $"robot still reports wheels at {l:F0}/{r:F0} mm/s");
    }

    // ------------------------------------------------------------------ the actions

    /// <summary>
    /// A MoveHeadToAngleAction or MoveLiftToHeightAction in flight, as the engine's IActionRunner subclass
    /// (L1..L18). The shared lifecycle is in <see cref="ActionRunner"/>; this class supplies the concrete
    /// Init/CheckIfDone and the MovementComponent track primitives.
    /// </summary>
    private sealed class MoveAction : ActionRunner
    {
        public readonly CozmoMotion Owner;
        public readonly bool IsHead;
        public readonly float Target, Tolerance;
        public readonly string What;
        /// <summary>The message builder that carries the action id (MA8): SetHeadAngle / SetLiftHeight.</summary>
        public readonly Func<byte, RobotMessage> Build;
        /// <summary>MA8: the motor action id on the wire (MC+8), taken in MoveHeadToAngle / MoveLiftToHeight.</summary>
        public byte Id;
        /// <summary>+0xAA / +0x95: the command was sent.</summary>
        public bool Sent;
        /// <summary>+0xAB / +0x96: the matching ack arrived.</summary>
        public bool Acked;
        /// <summary>+0xAD / +0x98: the motor was seen moving after the ack.</summary>
        public bool HasMoved;
        /// <summary>Head +0xAC / lift +0x97: in position, latched (C1, C6).</summary>
        public bool InPositionLatched;
        public readonly TaskCompletionSource<MotionOutcome> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public MoveAction(CozmoMotion owner, bool isHead, float target, float tolerance, string what,
                          Func<byte, RobotMessage> build)
            : base(isHead ? 0x12 : 0x13, isHead ? HeadTrack : LiftTrack)   // M13-022: head type 0x12; lift 0x13 (R-ANIM pre-extraction)
        {
            Owner = owner; IsHead = isHead; Target = target; Tolerance = tolerance; What = what; Build = build;
        }

        // L7: the engine clock (BaseStationTimer::GetCurrentTimeInSeconds).
        protected override float EngineClockSeconds => Owner._robot.Engine.Timer.SecondsF;

        protected override bool AreAnyTracksLocked(uint mask) => Owner.AreAnyTracksLocked((byte)mask);
        protected override void LockTracks(uint mask, string owner) => Owner.LockTracks((byte)mask, owner);
        protected override void UnlockTracksInternal(uint mask, string owner) => Owner.UnlockTracks((byte)mask, owner);
        protected override bool AreAllTracksLockedBy(uint mask, string owner) => Owner.AreAllTracksLockedBy((byte)mask, owner);
        protected override void StopHeadTrack() => Owner.StopHead();
        protected override void StopLiftTrack() => Owner.StopLift();
        protected override void StopBodyTrack() => Owner.StopBody();
        protected override bool HeadTrackMoving => Owner._headMoving;
        protected override bool LiftTrackMoving => Owner._liftMoving;
        protected override bool BodyTrackMoving => Owner._bodyMoving;

        public override bool CanInterrupt() => true;

        // fidelity: M4-016
        /// <summary>
        /// The concrete Init (MA15, C6 L1): in position sends nothing and latches; otherwise the next motor
        /// action id goes on the wire. A send failure is 0x03000016 (MA17). Init runs on the first gates-passing
        /// UpdateInternal tick, after the start time is stamped.
        /// </summary>
        public override int Init()
        {
            lock (Owner._gate)
            {
                if (Owner.InPositionLocked(this)) { InPositionLatched = true; return 0; }
                Id = Owner.NextActionId();                       // MA8: taken in MoveHeadToAngle / MoveLiftToHeight
                if (!Owner._robot.SendMessage(Build(Id))) return (int)ResultSendFailed;
                Sent = true;
                return 0;
            }
        }

        // fidelity: M4-016
        /// <summary>
        /// The concrete CheckIfDone, the same for the head (C1: MoveHeadToAngleAction 0x005485D8..0x005488B6) and
        /// the lift (C6 L2..L6: MoveLiftToHeightAction 0x005493F6..0x00549508):
        /// 1. sent and not acked: Running;
        /// 2. the in-position latch, which stays set once true;
        /// 3. has moved while the motor is moving;
        /// 4. in position: Success if not moving else Running; not in position: moving Running, stopped and has
        ///    moved 0x04000004 StoppedMakingProgress, otherwise Running.
        /// The head's eye-shift block (0x005485F8..0x00548728) never runs in this build (+0xA8 is never set), and
        /// the lift has none (C6), so neither changes the result.
        /// </summary>
        public override uint CheckIfDone()
        {
            lock (Owner._gate)
            {
                if (Sent && !Acked) return EngineActionResult.Running;
                if (Owner.InPositionLocked(this)) InPositionLatched = true;
                bool moving = Owner.MovingLocked(this);
                if (moving) HasMoved = true;
                if (InPositionLatched) return moving ? EngineActionResult.Running : EngineActionResult.Success;
                if (!moving && HasMoved) return ResultStoppedMakingProgress;
                return EngineActionResult.Running;
            }
        }

        // L16/D5: the base runs the stop-before-unlock tail; then the Done task maps the stored result.
        public override void WatcherEnding()
        {
            base.WatcherEnding();
            Owner.ActionEnded(this);
        }
    }

    // fidelity: M4-016
    /// <summary>
    /// MA15 IsHeadInPosition: IsNear(robot+0x2FC, target, tolerance + 1e-5) (0x005484F4..0x0054852C, PLT
    /// 0x0084CC0A). Radians::IsNear (0x0084CC0A) returns |rescale(this − target)| &lt; |tolerance|, a strict &lt;.
    /// Both values are inside (−π, π] here, so rescaling the difference cannot change it.
    /// </summary>
    private bool IsHeadInPositionLocked(MoveAction a) => Math.Abs(_headAngle - a.Target) < a.Tolerance + HeadInPositionSlack;

    // fidelity: M4-016
    /// <summary>MA15 IsLiftInPosition: |target − height| &lt; tolerance and MC+0xB == 0 (0x00548FEA..0x00549034).</summary>
    private bool IsLiftInPositionLocked(MoveAction a) =>
        Math.Abs(a.Target - RobotState.LiftHeightMmFromAngle(_liftAngle ?? 0f)) < a.Tolerance && !_liftMoving;

    private bool InPositionLocked(MoveAction a) => a.IsHead ? IsHeadInPositionLocked(a) : IsLiftInPositionLocked(a);
    private bool MovingLocked(MoveAction a) => a.IsHead ? _headMoving : _liftMoving;

    // fidelity: M4-001, M4-016, M2-002
    /// <summary>
    /// Fed every robot message by <see cref="CozmoRobot"/> (after the state tracker). A MotorActionAck completes the
    /// ack step of the action whose id it carries, when that action's command was sent (MA16). A handled RobotState
    /// updates what the engine keeps: MC+0xA = !HEAD_IN_POS, MC+0xB = !LIFT_IN_POS, MC+0xC = ARE_WHEELS_MOVING (M2
    /// App. B status bits), Robot+0x300 = liftAngle (RS7), and Robot+0x2FC through RS6: ignored until the head is
    /// calibrated, a report below −28° stored as −25° and above 47.5° as 44.5°, with the HeadAngleOOB warning.
    /// Batch 2: the state/ack flags are stored here; CheckIfDone runs on the ActionList tick (L9), not in Handle.
    /// </summary>
    internal void Handle(RobotMessage m)
    {
        lock (_gate)
        {
            switch (m)
            {
                case MotorActionAck ack:
                    foreach (var a in _actions)
                        if (a.Sent && !a.Acked && a.Id == ack.ActionId) a.Acked = true;
                    break;
                case RobotState s:
                    _headMoving = !s.Has(RobotStatusFlag.HeadInPos);
                    _liftMoving = !s.Has(RobotStatusFlag.LiftInPos);
                    _bodyMoving = s.Has(RobotStatusFlag.AreWheelsMoving);
                    _liftAngle = s.LiftAngle;
                    if (_robot.State.HeadCalibrated)
                    {
                        float h = s.HeadAngle;
                        if (h < ReportedHeadLowOob) { Log("warning: Robot.GetCameraHeadPose.HeadAngleOOB"); h = MinHeadAngleRad; }
                        else if (h > ReportedHeadHighOob) { Log("warning: Robot.GetCameraHeadPose.HeadAngleOOB"); h = MaxHeadAngleRad; }
                        _headAngle = h;
                    }
                    break;
            }
        }
    }

    /// <summary>Waits until the robot's reported state satisfies a condition.</summary>
    private async Task<bool> AwaitState(Func<RobotState, bool> condition, TimeSpan within)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void watch(RobotState s) { if (condition(s)) done.TrySetResult(true); }
        _robot.State.StateUpdated += watch;
        try
        {
            if (_robot.State.Latest is { } now && condition(now)) return true;
            return await Task.WhenAny(done.Task, Task.Delay(within)) == done.Task;
        }
        finally { _robot.State.StateUpdated -= watch; }
    }
}
