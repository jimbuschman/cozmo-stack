using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>Anki's <c>UnexpectedMovementType</c> (decompiled <c>Anki.Cozmo/UnexpectedMovementType.cs</c>, <c>: byte</c>).</summary>
public enum UnexpectedMovementType : byte
{
    TurnedButStopped,
    TurnedInSameDirection,
    TurnedInOppositeDirection,
}

/// <summary>Anki's <c>UnexpectedMovementSide</c> (decompiled <c>Anki.Cozmo/UnexpectedMovementSide.cs</c>, <c>: byte</c>).</summary>
public enum UnexpectedMovementSide : byte
{
    Unknown,
    Front,
    Back,
    Left,
    Right,
}

/// <summary>
/// One detection: the engine's <c>UnexpectedMovement</c> message (timestamp, type, side), plus the
/// evidence it was computed from.
/// </summary>
public sealed record UnexpectedMovementReport(
    uint Timestamp, UnexpectedMovementType Type, UnexpectedMovementSide Side,
    float AverageLeftMmps, float AverageRightMmps, int Count)
{
    public override string ToString() =>
        $"unexpected movement {Type} from {Side} at {Timestamp} (avg wheels {AverageLeftMmps:F0}/{AverageRightMmps:F0} mm/s over {Count})";
}

/// <summary>
/// The engine's unexpected-movement detector, <c>MovementComponent::CheckForUnexpectedMovement</c> at
/// 0x0063E398 in libcozmoEngine.so 3.4.0-1204, with its parameters from the component's constructor
/// (0x0063DA5C). It compares what the wheels are doing with what the gyro says the body is doing, and
/// raises a report when they disagree for long enough.
///
/// <b>Gates (NATIVE):</b> the check runs only on a physical robot (<c>Robot+0x14</c>), never while the
/// robot is picking or placing (<c>IS_PICKING_OR_PLACING</c>, stored to the carrying component at
/// 0x00512AA0 and tested at 0x0063E3E2), and a state with <c>IS_PICKED_UP</c>, <c>IS_ON_CHARGER</c> or
/// <c>CLIFF_DETECTED</c> set (mask 0x5008 at 0x0063E40A) clears the detector. The engine also skips the
/// check while its own <c>DirectDrive*</c> track locks are held (0x0063E3BA..0x0063E3DE); that lock set
/// does not exist in this stack, so <see cref="Suspended"/> stands in for it and the caller that drives the
/// wheels directly sets it (INFERRED as to which locks; the skip itself is native).
///
/// <b>The test (NATIVE), per state:</b>
/// <list type="bullet">
/// <item>|left| + |right| &lt; 20 mm/s → the wheels are not really driving; the count decays by one.</item>
/// <item>|gyro z| &lt; 0.174533 rad/s (10°/s): the body is not turning. If the wheels are commanded in
/// opposite directions (a turn in place) the count rises by one, type <see cref="UnexpectedMovementType.TurnedButStopped"/>.
/// If they agree, the commanded rotation (right − left) / 46 mm halved is compared with |gyro z|; a
/// difference above 0.2 rad/s also counts one (same type); otherwise nothing changes.</item>
/// <item>|gyro z| ≥ 0.174533: the body is turning. If its sign disagrees with the commanded (right − left),
/// the count rises by <b>two</b>, type <see cref="UnexpectedMovementType.TurnedInOppositeDirection"/>;
/// if they agree the count decays by one.</item>
/// </list>
/// The wheel speeds are summed into left/right accumulators (doubled on the count-by-two path) from the
/// state timestamp at which the count left zero. When the count exceeds 10 the averages decide the side:
/// left forward and right not → <see cref="UnexpectedMovementSide.Right"/>, the reverse → Left, both
/// forward → Front, otherwise Back (0x0063E6C0..0x0063E83A), and everything resets.
/// <see cref="UnexpectedMovementType.TurnedInSameDirection"/> is never produced by this function.
///
/// <b>Not reproduced (DEFERRED):</b> the engine then rewinds the robot's pose to the start timestamp and
/// adds a collision obstacle to its world model on that side; this stack has no world model.
/// </summary>
public sealed class UnexpectedMovementDetector
{
    // ---------------------------------------------------------------- NATIVE constants
    /// <summary>Wheel separation used to turn wheel speeds into a rotation rate: 46 mm (0x42380000).</summary>
    public const float WheelDistanceMm = 46f;
    /// <summary>Below this |left| + |right| the wheels are not driving: 20 mm/s (ctor 0x41A00000 → +0xB0).</summary>
    public const float MinWheelSpeedSumMmps = 20f;
    /// <summary>|gyro z| at or above which the body counts as turning: 0.174533 rad/s (ctor 0x3E32B8C2 → +0xA4).</summary>
    public const float GyroTurnThresholdRadps = 0.174533f;
    /// <summary>Allowed gap between commanded and measured rotation while driving: 0.2 rad/s (ctor 0x3E4CCCCD → +0xB4).</summary>
    public const float RotationMismatchToleranceRadps = 0.2f;
    /// <summary>The count that has to be exceeded: 10 (ctor <c>movs r1, #0xa</c> → +0xAC).</summary>
    public const int CountThreshold = 10;
    /// <summary>Wheel speed averages below this are "not moving" when deciding the side: 1e-5 (0x3727C5AC).</summary>
    public const float SideEpsilonMmps = 1e-5f;
    /// <summary>Status flags that reset the detector: IS_PICKED_UP | IS_ON_CHARGER | CLIFF_DETECTED (0x5008).</summary>
    public const RobotStatusFlag ResetFlags =
        RobotStatusFlag.IsPickedUp | RobotStatusFlag.IsOnCharger | RobotStatusFlag.CliffDetected;

    /// <summary>The engine's physical-robot gate. Defaults on: this stack drives physical robots.</summary>
    public bool IsPhysical { get; set; } = true;
    /// <summary>Stands in for the engine's direct-drive track locks: set while a caller drives the wheels itself.</summary>
    public bool Suspended { get; set; }

    /// <summary>The running disagreement count (<c>+0xA0</c>).</summary>
    public int Count { get; private set; }
    /// <summary>State timestamp at which the count left zero (<c>+0x94</c>).</summary>
    public uint StartTimestamp { get; private set; }
    /// <summary>Accumulated left and right wheel speed since then (<c>+0x98</c>, <c>+0x9C</c>).</summary>
    public float SumLeft { get; private set; }
    public float SumRight { get; private set; }
    /// <summary>The last report, for behaviours that need the side after the event.</summary>
    public UnexpectedMovementReport? Last { get; private set; }

    /// <summary>Raised when the count exceeds the threshold. The engine broadcasts <c>UnexpectedMovement</c> here.</summary>
    public event Action<UnexpectedMovementReport>? Detected;

    public void Reset()
    {
        Count = 0; StartTimestamp = 0; SumLeft = 0; SumRight = 0;
    }

    /// <summary>Feeds one robot state. Returns the report when one is raised on this state.</summary>
    public UnexpectedMovementReport? Update(RobotState s)
    {
        if (!IsPhysical || Suspended) return null;
        if (s.Has(RobotStatusFlag.IsPickingOrPlacing)) return null;
        if ((s.Status & (uint)ResetFlags) != 0) { Reset(); return null; }

        float left = s.LwheelSpeedMmps, right = s.RwheelSpeedMmps;
        float commandedRotation = (right - left) / WheelDistanceMm;
        float sum = MathF.Abs(left) + MathF.Abs(right);
        if (sum < MinWheelSpeedSumMmps)
        {
            if (Count > 0) Count--;
            return null;
        }

        float gz = s.Gyro.Z;
        float absGz = MathF.Abs(gz);
        UnexpectedMovementType type;
        if (absGz < GyroTurnThresholdRadps)
        {
            // the body is not turning
            if (float.IsNegative(left) != float.IsNegative(right))
            {
                type = UnexpectedMovementType.TurnedButStopped;                 // 0x0063E4E8
                Accumulate(s, left, right, 1);
            }
            else
            {
                float expected = MathF.Abs(0.5f * commandedRotation);          // 0x0063E540
                float diff = MathF.Abs(expected - absGz);
                type = UnexpectedMovementType.TurnedButStopped;
                if (diff > RotationMismatchToleranceRadps) Accumulate(s, left, right, 1);
            }
        }
        else
        {
            // the body is turning: does it agree with the command?
            if (float.IsNegative(right - left) != float.IsNegative(gz))
            {
                type = UnexpectedMovementType.TurnedInOppositeDirection;        // 0x0063E51C
                Accumulate(s, left, right, 2);
            }
            else
            {
                if (Count > 0) Count--;                                          // 0x0063E674
                return null;
            }
        }

        if (Count <= CountThreshold) return null;                                // 0x0063E5B2: bls

        float avgLeft = SumLeft / Count, avgRight = SumRight / Count;
        bool leftForward = avgLeft > SideEpsilonMmps, rightForward = avgRight > SideEpsilonMmps;
        UnexpectedMovementSide side;
        if (leftForward != rightForward) side = leftForward ? UnexpectedMovementSide.Right : UnexpectedMovementSide.Left;
        else if (leftForward) side = UnexpectedMovementSide.Front;
        else side = UnexpectedMovementSide.Back;

        var report = new UnexpectedMovementReport(StartTimestamp, type, side, avgLeft, avgRight, Count);
        Reset();
        Last = report;
        Detected?.Invoke(report);
        return report;
    }

    private void Accumulate(RobotState s, float left, float right, int by)
    {
        if (Count == 0) StartTimestamp = s.Timestamp;
        Count += by;
        SumLeft += left * by;
        SumRight += right * by;
    }
}
