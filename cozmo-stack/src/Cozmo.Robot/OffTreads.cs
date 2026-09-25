using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>
/// How the robot is sitting, as the engine classifies it. Anki's own enum, from the decompiled
/// <c>unity/scripts/csharp/Anki.Cozmo/OffTreadsState.cs</c> (declared <c>: sbyte</c>); the engine stores
/// the same value at <c>Robot+0x355</c> and its reaction strategies compare against these ordinals.
/// </summary>
public enum OffTreadsState : sbyte
{
    OnTreads,
    InAir,
    OnBack,
    OnLeftSide,
    OnRightSide,
    OnFace,
    Falling,
}

// fidelity: M10-001, M10-005, M10-012, M10-013
/// <summary>
/// The engine's off-treads classifier, <c>Robot::CheckAndUpdateTreadsState</c> (0x00511E00, M10 inventory rows A1..A16,
/// gap pass 1 3a..3e and 5a), together with the IMU part of <c>Robot::UpdateFullRobotState</c> that feeds it (M2 RS8).
///
/// <b>Clock (M10-005, A2, gap1 6a..6d):</b> <c>now</c> is BaseStationTimer::GetCurrentTimeStamp, u32(seconds·1000) of
/// the engine tick's start, the same value for every state handled in one tick; <see cref="CozmoSensors"/> passes
/// <see cref="BaseStationTimer.TimeStampMs"/>. The robot timestamp (robot+0x2C, the state's own) is used only for
/// the Falling events and +0x35C (A8, A9). Candidate times and the commit test are u32 (A7).
///
/// <b>Gate (A1):</b> nothing is classified until the head is calibrated (robot+0x314).
///
/// <b>Inputs (A3, A4):</b> the pitch (robot+0x304, RS4), the filtered accel Y (+0x384, RS8), IS_FALLING 0x20,
/// IS_PICKED_UP 0x08, and the physical flag robot+0x14 (M10-010), which is 0 until FirmwareVersion arrives and
/// picks the on-back centre: 1.30027 physical, 1.68250 otherwise.
///
/// <b>Branches (A5, A6)</b> and the <b>commit (A7)</b> are below. <b>Consequences of a commit (A8..A14)</b>, in the
/// engine's address order: the Falling DAS events with ActionList::Cancel(−1) on entering Falling; the +0x355
/// store; the RobotOffTreadsStateChanged broadcast (<see cref="StateChanged"/>); on states 2..6 the carried object
/// is unattached (M12 seam); on OnTreads the BlockWorld localizable check (M11 seam) and the stores
/// +0x2C4 = 1, +0x2B8 = −1; on anything but OnTreads SetOnChargerPlatform(false) (M4 C8 P6); the freeplay pause
/// flag 2 "OffTreads" (M15 seam).
///
/// MISSING (A5): pitchExtreme uses Anki's Radians <c>operator&gt;</c>/<c>operator&lt;</c> (0x84CC90: "diff &gt; 0 &amp;&amp;
/// !IsNear(1e-5)"). Whether the Radians difference is wrapped to (−π, π] before the sign test, and IsNear's exact
/// bound, are not in the rows; a wrapped difference would make every pitch below −1.2217 rad count as extreme. The
/// plain float comparisons below are kept until those two facts are extracted.
/// </summary>
public sealed class OffTreadsClassifier
{
    // ---------------------------------------------------------------- A5, A6 constants
    /// <summary>Gravity in the accelerometer's units (A5: literal 0x5121FC = −9800).</summary>
    public const float GravityAccel = 9800f;
    /// <summary>A5: onSide = sideDev &lt; 3000 (0x512200).</summary>
    public const float OnSideAccelBand = 3000f;
    /// <summary>A5: pitch &gt; Rad(1.91986) is extreme.</summary>
    public const float OnFacePitchHighRad = 1.91986f;
    /// <summary>A5: pitch &lt; Rad(−1.39626) is extreme.</summary>
    public const float OnFacePitchLowRad = -1.39626f;
    /// <summary>A5: the on-back centre on a physical robot, f64 at 0x5122A8.</summary>
    public const double OnBackCentrePhysicalRad = 1.30027;
    /// <summary>A5: the on-back centre when not physical, f64 at 0x5122A0.</summary>
    public const double OnBackCentreSimulatedRad = 1.68250;
    /// <summary>A5: onBack = |d| ≤ 0.261799 (0x5122B0).</summary>
    public const double OnBackHalfWidthRad = 0.261799;
    /// <summary>A5: level = |pitch| ≤ 0.785398 (0x5122BC).</summary>
    public const float LevelPitchRad = 0.785398f;
    /// <summary>A6/A7: the 250 ms debounce (t = now − 250; commit when t + 250 ≤ now).</summary>
    public const uint DebounceMs = 250;
    /// <summary>A6: an on-back candidate's time is now + 750 (`addw r0,fp,#0x2ee`).</summary>
    public const uint OnBackExtraDelayMs = 750;

    // ---------------------------------------------------------------- inputs the engine keeps on Robot
    /// <summary>
    /// Robot+0x14, the physical flag (M10-010, A4): 0 from the Robot constructor, set by SetPhysicalRobot from
    /// HandleFirmwareVersion. <see cref="CozmoSensors"/> copies <see cref="EngineRobot.IsPhysicalRobot"/> in before each state.
    /// </summary>
    public bool IsPhysical { get; set; }

    /// <summary>Robot+0x314, the head-calibrated gate (A1, M4 MA21).</summary>
    public bool HeadCalibrated { get; set; }

    // ---------------------------------------------------------------- IMU state (M2 RS8, M10-012, M10-013)
    // fidelity: M10-012, M10-013
    /// <summary>
    /// Robot+0x360..+0x368, the raw accel of the last state (RS8), written only by UpdateFullRobotState (gap2 7a).
    /// Before the first state the engine holds heap contents (gap2 7b); policy MD1 (M10-013) uses 0.
    /// </summary>
    public Vector3 RawAccel { get; private set; }
    /// <summary>Robot+0x36C..+0x374, the raw gyro of the last state (RS9, gap2 7a); 0 before the first state (MD1, M10-013).</summary>
    public Vector3 RawGyro { get; private set; }
    /// <summary>Robot+0x378, |accel| of the last state (RS8); 0 from the constructor's memclr (gap1 5a).</summary>
    public float RawAccelMagnitude { get; private set; }
    /// <summary>Robot+0x37C, 0.05·|a| + 0.95·old (RS8); 0 from the constructor (gap1 5a). RobotShaken reads it (C15).</summary>
    public float FilteredAccelMagnitude { get; private set; }
    /// <summary>Robot+0x380..+0x388, 0.1·a + 0.9·old per axis (RS8); 0 from the constructor (gap1 5a).</summary>
    public Vector3 FilteredAccel { get; private set; }
    /// <summary>Robot+0x304, the pose pitch of the last state (RS4).</summary>
    public float PitchRad { get; private set; }

    // ---------------------------------------------------------------- classifier state (A16: only CUTS writes it)
    /// <summary>+0x355, the committed state; 0 from the constructor (0x5100E2..0x5100EA).</summary>
    public OffTreadsState Current { get; private set; } = OffTreadsState.OnTreads;
    /// <summary>+0x356, the candidate.</summary>
    public OffTreadsState Candidate { get; private set; } = OffTreadsState.OnTreads;
    /// <summary>+0x358, the candidate's time on the BaseStationTimer ms clock (u32).</summary>
    public uint CandidateTimeMs { get; private set; }
    /// <summary>+0x35C: the robot timestamp at which the current fall was entered, 0 when not falling (A8, A9).</summary>
    public uint FallingStartedTimestamp { get; private set; }
    /// <summary>+0x2C4 = 1 on a commit to OnTreads (A11). What it means is not in the rows (an M11 interface).</summary>
    public byte Robot2C4 { get; private set; }
    /// <summary>+0x2B8 = −1 on a commit to OnTreads (A11). What it means is not in the rows (an M11 interface).</summary>
    public int Robot2B8 { get; private set; }
    /// <summary>How many states have been fed (not an engine field).</summary>
    public int Updates { get; private set; }

    // ---------------------------------------------------------------- consequences (A8..A14)
    /// <summary>A10: the RobotOffTreadsStateChanged broadcast, with the old and the new state.</summary>
    public event Action<OffTreadsState, OffTreadsState>? StateChanged;
    /// <summary>A8: the DAS event "Robot.CheckAndUpdateTreadsState.FallingStarted" "t=%dms" (DAS only; the game FallingStarted comes from the robot, C1).</summary>
    public event Action? FallingStarted;
    /// <summary>A9: the DAS event "…FallingStopped" with duration = ts − +0x35C (DAS only).</summary>
    public event Action<uint>? FallingStopped;
    /// <summary>A8: ActionList(robot+0x250)::Cancel(−1) on entering Falling: cancel the current action and delete the queued ones (gap1 3a..3e). The ActionList is an M8/M12 interface.</summary>
    public event Action<int>? ActionListCancel;
    /// <summary>A12: on a commit to 2..6, if CarryingComponent+8 ≠ −1, SetCarriedObjectAsUnattached(true). The carrying component is M12's; this seam is called with the test left to it.</summary>
    public Action? UnattachCarriedObjectIfCarrying { get; set; }
    /// <summary>A11: BlockWorld::AnyRemainingLocalizableObjects (M11 seam). Null: no BlockWorld attached and no log.</summary>
    public Func<bool>? AnyRemainingLocalizableObjects { get; set; }
    /// <summary>A13: SetOnChargerPlatform(false) on a commit to anything but OnTreads (M4 C8 P6).</summary>
    public Action? ClearOnChargerPlatform { get; set; }
    /// <summary>A14: FreeplayDataTracker::SetFreeplayPauseFlag(new ≠ 0, flag 2 "OffTreads") (M15 seam).</summary>
    public Action<bool>? SetFreeplayPauseFlagOffTreads { get; set; }
    /// <summary>The engine's log lines (A11, A10).</summary>
    public event Action<string>? Log;

    /// <summary>Puts the classifier back where a freshly constructed engine Robot starts (gap1 5a, A16, MD1).</summary>
    public void Reset()
    {
        RawAccel = default; RawGyro = default;
        FilteredAccel = default; FilteredAccelMagnitude = 0; RawAccelMagnitude = 0; PitchRad = 0;
        Current = Candidate = OffTreadsState.OnTreads; CandidateTimeMs = 0; FallingStartedTimestamp = 0; Updates = 0;
        Robot2C4 = 0; Robot2B8 = 0;
    }

    // fidelity: M1-025, M1-015
    /// <summary>
    /// <see cref="Reset"/> plus the two Robot inputs, back to their as-constructed values, for a removed robot (CB33,
    /// CC26, CC27): the head gate closed and the physical flag 0 (A4). Subscribers and seams are kept.
    /// </summary>
    internal void ResetToConstructed()
    {
        Reset();
        IsPhysical = false;
        HeadCalibrated = false;
    }

    /// <summary>
    /// Feeds one synced robot state. Runs the IMU part of UpdateFullRobotState unconditionally (RS8, RS9), then
    /// CheckAndUpdateTreadsState with <paramref name="nowMs"/> = BaseStationTimer ms (A2). Returns 1 only on a commit (A15).
    /// </summary>
    public bool Update(RobotState state, uint nowMs)
    {
        Updates++;
        PitchRad = state.Pose.Pitch;

        // RS8/RS9 (0x005129A4..0x00512A6E): raw accel and gyro, |a|, then the two filters.
        var a = state.Accel;
        RawAccel = new Vector3(a.X, a.Y, a.Z);
        RawGyro = new Vector3(state.Gyro.X, state.Gyro.Y, state.Gyro.Z);
        RawAccelMagnitude = MathF.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
        FilteredAccelMagnitude = 0.05f * RawAccelMagnitude + 0.95f * FilteredAccelMagnitude;
        FilteredAccel = new Vector3(
            0.1f * a.X + 0.9f * FilteredAccel.X,
            0.1f * a.Y + 0.9f * FilteredAccel.Y,
            0.1f * a.Z + 0.9f * FilteredAccel.Z);

        if (!HeadCalibrated) return false;                                      // A1: robot+0x314

        // A3
        bool falling = state.Has(RobotStatusFlag.IsFalling);
        bool pickedUp = state.Has(RobotStatusFlag.IsPickedUp);
        float pitch = PitchRad;
        float s24 = FilteredAccel.Y;

        // A5
        float sideDev = MathF.Abs(MathF.Abs(s24) - GravityAccel);
        bool onSide = sideDev < OnSideAccelBand;
        bool ySign = s24 > 0;
        double d = (double)pitch - (IsPhysical ? OnBackCentrePhysicalRad : OnBackCentreSimulatedRad);
        bool onBack = Math.Abs(d) <= OnBackHalfWidthRad;
        bool pitchExtreme = pitch > OnFacePitchHighRad || pitch < OnFacePitchLowRad;   // MISSING: Radians operators (see summary)
        bool level = MathF.Abs(pitch) <= LevelPitchRad;

        // A6
        var cand = Candidate;
        uint t = CandidateTimeMs;
        bool tail;
        if (falling)
        {
            if (cand != OffTreadsState.Falling) { t = unchecked(nowMs - DebounceMs); cand = OffTreadsState.Falling; }
            tail = true;
        }
        else if (onSide)
        {
            if (cand is not (OffTreadsState.OnLeftSide or OffTreadsState.OnRightSide))
            {
                cand = ySign ? OffTreadsState.OnRightSide : OffTreadsState.OnLeftSide;
                t = nowMs;
            }
            tail = false;
        }
        else if (pitchExtreme)
        {
            if (cand != OffTreadsState.OnFace) { cand = OffTreadsState.OnFace; t = nowMs; }
            tail = false;
        }
        else if (onBack)
        {
            if (cand != OffTreadsState.OnBack) { cand = OffTreadsState.OnBack; t = unchecked(nowMs + OnBackExtraDelayMs); }
            tail = false;
        }
        else
        {
            if (level && cand >= OffTreadsState.OnBack) { t = nowMs; cand = OffTreadsState.InAir; }
            else if (pickedUp && cand == OffTreadsState.OnTreads) { t = unchecked(nowMs - DebounceMs); cand = OffTreadsState.InAir; }
            tail = true;
        }
        // Tail T (0x511F48..0x511F98)
        if (tail && !(pickedUp || onSide || onBack || pitchExtreme || cand == OffTreadsState.OnTreads))
        {
            cand = OffTreadsState.OnTreads;
            t = unchecked(nowMs - DebounceMs);
        }
        Candidate = cand;
        CandidateTimeMs = t;

        // A7
        if (unchecked(t + DebounceMs) > nowMs) return false;
        if (Current == cand) return false;

        var old = Current;
        if (cand == OffTreadsState.Falling)
        {
            // A8
            FallingStartedTimestamp = state.Timestamp;
            FallingStarted?.Invoke();
            ActionListCancel?.Invoke(-1);
        }
        else if (old == OffTreadsState.Falling)
        {
            // A9
            FallingStopped?.Invoke(unchecked(state.Timestamp - FallingStartedTimestamp));
            FallingStartedTimestamp = 0;
        }
        Current = cand;                                                        // A7: +0x355 := +0x356

        // A10
        StateChanged?.Invoke(old, cand);
        Log?.Invoke($"Robot.OfftreadsState.TreadStateChanged: {old} -> {cand}");
        // A12
        if (cand is >= OffTreadsState.OnBack and <= OffTreadsState.Falling) UnattachCarriedObjectIfCarrying?.Invoke();
        // A11
        if (cand == OffTreadsState.OnTreads)
        {
            if (AnyRemainingLocalizableObjects is { } any && !any())
                Log?.Invoke("Robot.CheckAndUpdateTreadsState.NoMoreRemainingLocalizableObjects");
            Robot2C4 = 1;
            Robot2B8 = -1;
        }
        // A13
        else ClearOnChargerPlatform?.Invoke();
        // A14
        SetFreeplayPauseFlagOffTreads?.Invoke(cand != OffTreadsState.OnTreads);
        return true;                                                           // A15
    }

    /// <summary>The on-back centre in use (A5).</summary>
    public double OnBackCentreRad => IsPhysical ? OnBackCentrePhysicalRad : OnBackCentreSimulatedRad;
}
