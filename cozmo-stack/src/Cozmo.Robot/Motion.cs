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
    /// <summary>The engine's action failure: see <see cref="MotionOutcome.EngineResult"/> (0x04000004, 0x03000016).</summary>
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

    internal CozmoMotion(CozmoRobot robot) => _robot = robot;

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
    /// Back to the state right after construction, for a removed robot (CB33, CC26, CC27): the action-id counter at 0,
    /// no track locks, direct drive holding nothing, the head at −25° and no motor moving. A move in flight ends as
    /// timed out without anything sent.
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
        foreach (var a in ending) a.Done.TrySetResult(new MotionOutcome(MotionResult.TimedOut, $"{a.What}: the robot was removed"));
    }

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
    /// <summary>M4-003: the IActionRunner's lock owner entry (the engine locks with the action's id/name; one
    /// action per track runs at a time, so a fixed owner is equivalent).</summary>
    private const string ActionRunnerWho = "IActionRunner";
    /// <summary>M4-016: the IAction timeout slot's default, 30.0 s (0x0052B0C2), not 5 s.</summary>
    internal static readonly TimeSpan DefaultActionTimeout = TimeSpan.FromSeconds(30);

    // fidelity: M4-001
    /// <summary>
    /// <c>Radians::rescale</c> (0x0084C87C), reached from the Radians ctor 0x0084C832: bring an angle into (−π, π].
    /// The engine uses the <c>ceil(v/2π − 0.5)</c> shortcut at |v| ≥ 10 and a 2π loop below it
    /// (0x0084C8A2..0x0084C937). The ctor rescales every Radians the engine builds, so MoveHeadToAngleAction clips
    /// the rescaled angle, not the raw command.
    /// </summary>
    internal static float RescaleRadians(float value)
    {
        if (value <= -MathF.PI || value > MathF.PI)
        {
            if (MathF.Abs(value) >= 10f)
            {
                float turns = MathF.Ceiling(value / (2f * MathF.PI) - 0.5f);
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
        float target = RescaleRadians(radians);
        if (target < MinHeadAngleRad)
        {
            Log($"warning: MoveHeadToAngleAction.Constructor.AngleTooLow: {radians:F4} rad, clipped to {MinHeadAngleRad}");
            target = MinHeadAngleRad;
        }
        else if (target > MaxHeadAngleRad)
        {
            Log($"warning: MoveHeadToAngleAction.Constructor.AngleTooHigh: {radians:F4} rad, clipped to {MaxHeadAngleRad}");
            target = MaxHeadAngleRad;
        }
        float tolerance = Math.Max(GameHeadToleranceRad, MinHeadToleranceRad);
        var a = new MoveAction(this, isHead: true, target, tolerance, $"head to {target:F3} rad");
        return RunAsync(a, id => new SetHeadAngle(target, maxSpeedRadPerSec, accelRadPerSec2, durationSec, id), timeout);
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
    /// end skips UnlockTracks (0x005408EC..0x005408F0), so the move runs while another action holds the lift track and sends no Disable/EnableAnimTracks of its own.
    /// </summary>
    public Task<MotionOutcome> SetLiftHeightAsync(float heightMm,
                                                  float maxSpeedRadPerSec = DefaultLiftSpeedRadPerSec,
                                                  float accelRadPerSec2 = DefaultLiftAccelRadPerSec2,
                                                  float durationSec = 0f,
                                                  TimeSpan? timeout = null, bool requireCalibration = true,
                                                  bool suppressTrackLocking = false)
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
        var a = new MoveAction(this, isHead: false, target, GameLiftToleranceMm, $"lift to {target:F1} mm") { SuppressTrackLocking = suppressTrackLocking };
        return RunAsync(a, id => new SetLiftHeight(target, maxSpeedRadPerSec, accelRadPerSec2, durationSec, id), timeout);
    }

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

    /// <summary>A MoveHeadToAngleAction or MoveLiftToHeightAction in flight.</summary>
    private sealed class MoveAction
    {
        public readonly CozmoMotion Owner;
        public readonly bool IsHead;
        public readonly float Target, Tolerance;
        public readonly string What;
        /// <summary>M4-003: the action's required track mask (+0x54): head 1 (0x00547EAC), lift 2 (0x005489EE).</summary>
        public readonly byte Mask;
        public byte Id;
        /// <summary>+0xAA / +0x95: the command was sent.</summary>
        public bool Sent;
        /// <summary>+0xAB / +0x96: the matching ack arrived.</summary>
        public bool Acked;
        /// <summary>+0xAD / +0x98: the motor was seen moving after the ack.</summary>
        public bool HasMoved;
        /// <summary>Head +0xAC / lift +0x97: in position, latched (C1, C6).</summary>
        public bool InPositionLatched;
        /// <summary>M4-003: this action holds its track lock (taken at 0x0054058E, released at 0x005408EC).</summary>
        public bool Locked;
        /// <summary>M13-028: the action's byte +0x56 (non-zero: Update neither tests nor takes the track lock, 0x00540428..0x00540434, and the end does not release it, 0x005408EC..0x005408F0).</summary>
        public bool SuppressTrackLocking { get; init; }
        public readonly TaskCompletionSource<MotionOutcome> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public MoveAction(CozmoMotion owner, bool isHead, float target, float tolerance, string what)
        {
            Owner = owner; IsHead = isHead; Target = target; Tolerance = tolerance; What = what;
            Mask = isHead ? HeadTrack : LiftTrack;
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

    // fidelity: M4-003, M4-005, M4-016
    private async Task<MotionOutcome> RunAsync(MoveAction a, Func<byte, RobotMessage> build, TimeSpan? timeout)
    {
        lock (_gate)
        {
            // fidelity: M4-003
            // IActionRunner::Update (0x00540370): AreAnyTracksLocked(mask) at 0x00540572..0x0054057C fails the
            // action with 0x03000019 and sends nothing; otherwise LockTracks(mask) at 0x0054058E sends
            // DisableAnimTracks. The action's end releases the lock (UnlockTracks 0x005408EC), which sends
            // EnableAnimTracks. The in-position branch still takes and releases the lock (M4-016 unresolved).
            if (!a.SuppressTrackLocking && IsTrackLockedLocked(a.Mask))
            {
                Log($"warning: IActionRunner.Update.TracksLocked: {a.What}: required tracks are locked");
                return new MotionOutcome(MotionResult.Failed, $"{a.What}: required tracks are locked") { EngineResult = ResultTracksLocked };
            }
            if (!a.SuppressTrackLocking)
            {
                LockTracksLocked(a.Mask, ActionRunnerWho);
                a.Locked = true;
            }

            // MA15, C6 L1: Init clears has-moved and sent/acked (a fresh action), latches in-position and sends nothing
            // when the motor is already in position. CheckIfDone then skips the ack wait (nothing was sent) and, the
            // latch being set, succeeds once the motor is not moving: at once when it is not moving now (always so for
            // the lift, whose in-position test includes MC+0xB == 0); a head in position but moving waits in the list.
            if (InPositionLocked(a))
            {
                if (!MovingLocked(a))
                {
                    if (a.Locked) { UnlockTracksLocked(a.Mask, ActionRunnerWho); a.Locked = false; }
                    return new MotionOutcome(MotionResult.Acknowledged, $"{a.What}: already in position, nothing sent");
                }
                a.InPositionLatched = true;
                _actions.Add(a);
            }
            else
            {
                _actions.Add(a);
                a.Id = unchecked(++_actionIdCounter);      // MA8: the id is taken in MoveHeadToAngle / MoveLiftToHeight
                if (!_robot.SendMessage(build(a.Id)))
                {
                    _actions.Remove(a);
                    if (a.Locked) { UnlockTracksLocked(a.Mask, ActionRunnerWho); a.Locked = false; }
                    return new MotionOutcome(MotionResult.Failed, $"{a.What}: the send failed") { EngineResult = ResultSendFailed };
                }
                a.Sent = true;
            }
        }
        var t = timeout ?? DefaultActionTimeout;
        var done = await Task.WhenAny(a.Done.Task, Task.Delay(t)).ConfigureAwait(false);
        if (done == a.Done.Task) return a.Done.Task.Result;
        bool stop;
        lock (_gate)
        {
            if (!_actions.Remove(a)) return a.Done.Task.Result;    // finished just now: Handle already unlocked
            // fidelity: M4-015
            // MA7: an action that ends while its track is moving stops that track (~IActionRunner 0x0054112E..0x00541192).
            stop = MovingLocked(a);
            // fidelity: M4-003
            if (a.Locked) { UnlockTracksLocked(a.Mask, ActionRunnerWho); a.Locked = false; }
        }
        if (stop) { if (a.IsHead) StopHead(); else StopLift(); }
        return new MotionOutcome(MotionResult.TimedOut,
            a.Acked ? $"{a.What}: action {a.Id} acknowledged but not in position within {t.TotalSeconds:F1}s"
                    : $"{a.What}: no acknowledgement of action {a.Id} within {t.TotalSeconds:F1}s");
    }

    // fidelity: M4-016
    /// <summary>
    /// CheckIfDone, run against the latest handled state, the same for the head (C1: MoveHeadToAngleAction 0x005485D8..
    /// 0x005488B6) and the lift (C6 rows L2..L6: MoveLiftToHeightAction 0x005493F6..0x00549508):
    /// 1. sent and not acked: Running (head 0x005485D8..0x005485E2, lift 0x005493F6..0x00549402);
    /// 2. the in-position latch (+0xAC / +0x97), which stays set once true (head 0x005485E4..0x005485F4, lift
    ///    0x00549406..0x00549418);
    /// 3. has moved (+0xAD / +0x98) := 1 while the motor is moving, MC+0xA / MC+0xB (head 0x0054872E..0x00548734, lift
    ///    0x0054941C..0x00549428);
    /// 4. in position: Success if not moving, else Running; not in position: moving Running, not moving and has moved
    ///    0x04000004 StoppedMakingProgress, otherwise Running (head 0x00548738..0x005488AC, lift 0x0054942C..0x00549508).
    /// The head's further code at 0x005485F8..0x00548728 is the eye-shift removal (H4..H6): it runs only while
    /// +0x9D == 0 and +0xA8 != 0, and +0xA8 is never set in this build (H7), so it never executes and changes neither
    /// the wire nor the result. The lift's CheckIfDone (0x005493F6..0x00549508) has no eye-shift block (C6). Neither
    /// needs anything here.
    /// </summary>
    private void CheckIfDoneLocked(List<(MoveAction, MotionOutcome)> finished)
    {
        foreach (var a in _actions.ToArray())
        {
            if (a.Sent && !a.Acked) continue;
            if (InPositionLocked(a)) a.InPositionLatched = true;
            bool moving = MovingLocked(a);
            if (moving) a.HasMoved = true;
            if (a.InPositionLatched)
            {
                if (!moving)
                    finished.Add((a, new MotionOutcome(MotionResult.Acknowledged,
                        $"{a.What}: robot acknowledged action {a.Id} and reports it in position")));
            }
            else if (!moving && a.HasMoved)
                finished.Add((a, new MotionOutcome(MotionResult.Failed,
                    $"{a.What}: action {a.Id} stopped out of position (StoppedMakingProgress)") { EngineResult = ResultStoppedMakingProgress }));
        }
        foreach (var (a, _) in finished)
        {
            _actions.Remove(a);
            // fidelity: M4-003
            // The action's end releases its track lock (UnlockTracks 0x005408EC), sending EnableAnimTracks.
            if (a.Locked) { UnlockTracksLocked(a.Mask, ActionRunnerWho); a.Locked = false; }
        }
    }

    // fidelity: M4-001, M4-016, M2-002
    /// <summary>
    /// Fed every robot message by <see cref="CozmoRobot"/> (after the state tracker). A MotorActionAck completes the
    /// ack step of the action whose id it carries, when that action's command was sent (MA16). A handled RobotState
    /// updates what the engine keeps: MC+0xA = !HEAD_IN_POS, MC+0xB = !LIFT_IN_POS, MC+0xC = ARE_WHEELS_MOVING (M2
    /// App. B status bits), Robot+0x300 = liftAngle (RS7), and Robot+0x2FC through RS6: ignored until the head is
    /// calibrated, a report below −28° stored as −25° and above 47.5° as 44.5°, with the HeadAngleOOB warning.
    /// </summary>
    internal void Handle(RobotMessage m)
    {
        var finished = new List<(MoveAction, MotionOutcome)>();
        lock (_gate)
        {
            switch (m)
            {
                case MotorActionAck ack:
                    foreach (var a in _actions)
                        if (a.Sent && !a.Acked && a.Id == ack.ActionId) a.Acked = true;
                    CheckIfDoneLocked(finished);
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
                    CheckIfDoneLocked(finished);
                    break;
            }
        }
        foreach (var (a, o) in finished) a.Done.TrySetResult(o);
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
