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

/// <summary>
/// The engine's off-treads classifier, <c>Robot::CheckAndUpdateTreadsState</c> at 0x00511E00 in
/// libcozmoEngine.so 3.4.0-1204, together with the IMU filters <c>Robot::UpdateFullRobotState</c>
/// (0x0051291C) feeds it. Every threshold, filter constant and debounce below is read from that code;
/// nothing is tuned here.
///
/// <b>Inputs (NATIVE):</b> the pose pitch from <c>RobotState</c> (assigned to <c>Robot+0x304</c> at
/// 0x00512974), the accelerometer filtered with a 0.9/0.1 first-order filter per axis
/// (<c>Robot+0x380..0x388</c>, 0x00512A22..0x00512A6E) and the accelerometer magnitude filtered 0.95/0.05
/// (<c>Robot+0x37c</c>, 0x005129FA..0x00512A2E), plus the <c>IS_FALLING</c> (0x20) and <c>IS_PICKED_UP</c>
/// (0x08) status flags. The classifier only runs once the head is calibrated: the gate byte at
/// <c>Robot+0x314</c> is written by <c>Robot::SetHeadCalibrated</c> (0x0051519A).
///
/// <b>Classification (NATIVE), in the engine's order:</b>
/// <list type="number">
/// <item>falling flag set → candidate <see cref="OffTreadsState.Falling"/>, immediate (time now − 250 ms);</item>
/// <item>else | |accelY| − 9800 | &lt; 3000 → on a side: <see cref="OffTreadsState.OnRightSide"/> when
/// accelY &gt; 0, otherwise left, debounced 250 ms; a side candidate already set is kept;</item>
/// <item>else pitch &gt; 1.91986 rad (110°) or &lt; −1.39626 rad (−80°) → <see cref="OffTreadsState.OnFace"/>, 250 ms;</item>
/// <item>else |pitch − centre| ≤ 0.261799 rad (15°) → <see cref="OffTreadsState.OnBack"/>, where the
/// candidate time is set to now + 750 ms, so the change lands about a second later. The centre is a
/// two-entry table at 0x005122A0 indexed by <c>Robot+0x14</c>, the physical-robot flag set by
/// <c>Robot::SetPhysicalRobot</c>: 1.30027 rad (74.5°) on a physical robot, 1.6825 rad (96.4°) in simulation;</item>
/// <item>else if |pitch| ≤ 0.785398 rad (45°) and the candidate was on back, a side, face or falling →
/// <see cref="OffTreadsState.InAir"/>; a picked-up robot whose candidate was on treads → InAir immediately;
/// otherwise the candidate is kept.</item>
/// </list>
/// In the falling and level branches a final test (0x00511F48) drops straight to
/// <see cref="OffTreadsState.OnTreads"/>, immediately, when none of picked-up, on-side accel, on-back
/// pitch or on-face pitch holds and the candidate is not already OnTreads. The state changes when the
/// candidate has stood for 250 ms (0x00511FD0: <c>candidateTime + 250 ≤ now</c>).
///
/// <b>On a change (NATIVE):</b> entering Falling records the state timestamp and cancels the action list
/// (DAS <c>FallingStarted</c>); leaving it reports the duration (DAS <c>FallingStopped</c>); the engine
/// broadcasts <c>RobotOffTreadsStateChanged</c>, clears the on-charger-platform flag off treads and sets the
/// freeplay pause flag. The events here are that broadcast.
///
/// <b>Clock (LOCAL_POLICY):</b> the engine debounces against its own base-station clock in milliseconds;
/// this takes the time the caller passes, and <see cref="CozmoSensors"/> passes the robot's state timestamp,
/// which is the same millisecond scale and arrives with every state. The difference is at most one 30 Hz tick.
/// </summary>
public sealed class OffTreadsClassifier
{
    // ---------------------------------------------------------------- NATIVE constants
    /// <summary>Gravity in the accelerometer's units: the classifier's 9800 (vldr of 0xC6192000, negated).</summary>
    public const float GravityAccel = 9800f;
    /// <summary>How far |accelY| may sit from gravity to count as lying on a side: 3000 (0x453B8000).</summary>
    public const float OnSideAccelBand = 3000f;
    /// <summary>Pitch above which the robot is on its face: 1.91986 rad, 110° (0x3FF5BE0B).</summary>
    public const float OnFacePitchHighRad = 1.91986f;
    /// <summary>Pitch below which the robot is on its face: −1.39626 rad, −80° (0xBFB2B8C2).</summary>
    public const float OnFacePitchLowRad = -1.39626f;
    /// <summary>Centre of the on-back pitch band on a physical robot: 1.30027 rad, 74.5° (table 0x005122A8).</summary>
    public const double OnBackCentrePhysicalRad = 1.30027;
    /// <summary>Centre of the on-back pitch band in simulation: 1.6825 rad, 96.4° (table 0x005122A0).</summary>
    public const double OnBackCentreSimulatedRad = 1.6825;
    /// <summary>Half-width of the on-back band: 0.261799 rad, 15° (f64 at 0x005122B0).</summary>
    public const double OnBackHalfWidthRad = 0.261799;
    /// <summary>|pitch| at or below which the robot counts as level for the in-air test: 0.785398 rad, 45°.</summary>
    public const float LevelPitchRad = 0.785398f;
    /// <summary>The debounce: a candidate must stand this long before the state changes (0xFA).</summary>
    public const uint DebounceMs = 250;
    /// <summary>The extra delay on an on-back candidate: its time is set to now + 750 ms (0x2EE).</summary>
    public const uint OnBackExtraDelayMs = 750;
    /// <summary>Per-axis accelerometer filter: new = 0.9·old + 0.1·sample (0x3F666666, 0x3DCCCCD0).</summary>
    public const float AccelFilterAlpha = 0.1f;
    /// <summary>Accelerometer magnitude filter: new = 0.95·old + 0.05·|sample| (0x3F733333, 0x3D4CCCD0).</summary>
    public const float MagnitudeFilterAlpha = 0.05f;

    // ---------------------------------------------------------------- inputs the engine keeps on Robot
    /// <summary>
    /// The engine's physical-robot flag (<c>Robot+0x14</c>, set by <c>Robot::SetPhysicalRobot</c>). It
    /// selects the on-back centre. This stack only ever talks to a physical robot, so it defaults to true.
    /// </summary>
    public bool IsPhysical { get; set; } = true;

    /// <summary>
    /// The gate at <c>Robot+0x314</c>: the classifier does nothing until the head has been calibrated.
    /// <see cref="CozmoSensors"/> mirrors the robot's own calibration report into it.
    /// </summary>
    public bool HeadCalibrated { get; set; }

    // ---------------------------------------------------------------- derived state
    /// <summary>Accelerometer after the engine's 0.9/0.1 per-axis filter (<c>Robot+0x380</c>).</summary>
    public Vector3 FilteredAccel { get; private set; }
    /// <summary>|accelerometer| after the engine's 0.95/0.05 filter (<c>Robot+0x37c</c>). The shaken strategies read this.</summary>
    public float FilteredAccelMagnitude { get; private set; }
    /// <summary>|accelerometer| of the last state, unfiltered (<c>Robot+0x378</c>).</summary>
    public float RawAccelMagnitude { get; private set; }
    /// <summary>The pose pitch of the last state, radians (<c>Robot+0x304</c>).</summary>
    public float PitchRad { get; private set; }
    /// <summary>The classified state (<c>Robot+0x355</c>).</summary>
    public OffTreadsState Current { get; private set; } = OffTreadsState.OnTreads;
    /// <summary>The state being debounced towards (<c>Robot+0x356</c>).</summary>
    public OffTreadsState Candidate { get; private set; } = OffTreadsState.OnTreads;
    /// <summary>When the candidate was set, on the caller's clock (<c>Robot+0x358</c>).</summary>
    public long CandidateTimeMs { get; private set; }
    /// <summary>The state timestamp at which the current fall began (<c>Robot+0x35c</c>), 0 when not falling.</summary>
    public uint FallingStartedTimestamp { get; private set; }
    /// <summary>How many states have been classified.</summary>
    public int Updates { get; private set; }

    /// <summary>Raised when the classified state changes: old, then new.</summary>
    public event Action<OffTreadsState, OffTreadsState>? StateChanged;
    /// <summary>Raised on entering <see cref="OffTreadsState.Falling"/> (the engine's DAS FallingStarted).</summary>
    public event Action? FallingStarted;
    /// <summary>Raised on leaving <see cref="OffTreadsState.Falling"/>, with the fall's duration from the state timestamps.</summary>
    public event Action<uint>? FallingStopped;

    /// <summary>Puts the classifier back where a freshly constructed engine Robot starts.</summary>
    public void Reset()
    {
        FilteredAccel = default; FilteredAccelMagnitude = 0; RawAccelMagnitude = 0; PitchRad = 0;
        Current = Candidate = OffTreadsState.OnTreads; CandidateTimeMs = 0; FallingStartedTimestamp = 0; Updates = 0;
    }

    /// <summary>
    /// Feeds one robot state. Runs the IMU filters unconditionally, as the engine does, then the classifier
    /// when the head is calibrated. Returns true when the classified state changed.
    /// </summary>
    public bool Update(RobotState state, long nowMs)
    {
        Updates++;
        PitchRad = state.Pose.Pitch;

        // Robot::UpdateFullRobotState 0x005129C0..0x00512A6E: magnitude of the raw sample, then the filters.
        var a = state.Accel;
        RawAccelMagnitude = MathF.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
        FilteredAccelMagnitude = FilteredAccelMagnitude * (1 - MagnitudeFilterAlpha) + RawAccelMagnitude * MagnitudeFilterAlpha;
        FilteredAccel = new Vector3(
            FilteredAccel.X * (1 - AccelFilterAlpha) + a.X * AccelFilterAlpha,
            FilteredAccel.Y * (1 - AccelFilterAlpha) + a.Y * AccelFilterAlpha,
            FilteredAccel.Z * (1 - AccelFilterAlpha) + a.Z * AccelFilterAlpha);

        if (!HeadCalibrated) return false;                                      // Robot+0x314 gate

        bool falling = state.Has(RobotStatusFlag.IsFalling);
        bool pickedUp = state.Has(RobotStatusFlag.IsPickedUp);
        float pitch = PitchRad;
        float accelY = FilteredAccel.Y;

        float sideDeviation = MathF.Abs(MathF.Abs(accelY) - GravityAccel);
        bool onSideAccel = sideDeviation < OnSideAccelBand;
        bool rightSide = accelY > 0;
        double centre = IsPhysical ? OnBackCentrePhysicalRad : OnBackCentreSimulatedRad;
        double onBackDeviation = Math.Abs((double)pitch - centre);
        bool onBackPitch = onBackDeviation <= OnBackHalfWidthRad;
        bool onFacePitch = pitch > OnFacePitchHighRad || pitch < OnFacePitchLowRad;

        var candidate = Candidate;
        long candidateTime = CandidateTimeMs;
        OffTreadsState next;
        bool finalTest;                                                       // the 0x00511F48 block

        if (falling)
        {
            next = OffTreadsState.Falling;
            if (candidate != OffTreadsState.Falling) { candidateTime = nowMs - DebounceMs; candidate = next; }
            finalTest = true;
        }
        else if (onSideAccel)
        {
            if (candidate is OffTreadsState.OnLeftSide or OffTreadsState.OnRightSide) next = candidate;
            else
            {
                next = rightSide ? OffTreadsState.OnRightSide : OffTreadsState.OnLeftSide;
                candidateTime = nowMs; candidate = next;
            }
            finalTest = false;
        }
        else if (onFacePitch)
        {
            next = OffTreadsState.OnFace;
            if (candidate != next) { candidateTime = nowMs; candidate = next; }
            finalTest = false;
        }
        else if (onBackPitch)
        {
            next = OffTreadsState.OnBack;
            if (candidate != next) { candidateTime = nowMs + OnBackExtraDelayMs; candidate = next; }
            finalTest = false;
        }
        else
        {
            // 0x00512228: level, or tilted less than the on-back band.
            if (MathF.Abs(pitch) <= LevelPitchRad && candidate >= OffTreadsState.OnBack)
            {
                candidateTime = nowMs; next = OffTreadsState.InAir; candidate = next;
            }
            else if (!pickedUp) next = candidate;
            else if (candidate != OffTreadsState.OnTreads) next = candidate;
            else { candidateTime = nowMs - DebounceMs; next = OffTreadsState.InAir; candidate = next; }
            finalTest = true;
        }

        if (finalTest)
        {
            bool any = pickedUp || onSideAccel || onBackPitch || onFacePitch || next == OffTreadsState.OnTreads;
            if (!any) { candidateTime = nowMs - DebounceMs; candidate = OffTreadsState.OnTreads; next = OffTreadsState.OnTreads; }
        }

        Candidate = candidate;
        CandidateTimeMs = candidateTime;

        if (candidateTime + DebounceMs > nowMs) return false;                   // not stood long enough
        if (Current == next) return false;

        if (next == OffTreadsState.Falling)
        {
            FallingStartedTimestamp = state.Timestamp;
            FallingStarted?.Invoke();
        }
        else if (Current == OffTreadsState.Falling)
        {
            FallingStopped?.Invoke(unchecked(state.Timestamp - FallingStartedTimestamp));
            FallingStartedTimestamp = 0;
        }

        var old = Current;
        Current = candidate;
        StateChanged?.Invoke(old, Current);
        return true;
    }

    /// <summary>The on-back centre in use, for display.</summary>
    public double OnBackCentreRad => IsPhysical ? OnBackCentrePhysicalRad : OnBackCentreSimulatedRad;
}
