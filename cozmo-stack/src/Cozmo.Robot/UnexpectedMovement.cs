using Cozmo.Protocol;
using Cozmo.Robot.Vision;

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
/// <c>CLIFF_DETECTED</c> set (mask 0x5008 at 0x0063E40A) clears the detector.
///
/// The track gate is read now, and it is the other way round from the way this stack had it. The loop at
/// 0x0063E3C0 is <c>AreAnyTracksLocked(4)</c> inlined - the same array at <c>MovementComponent+0x30</c>,
/// eight entries of twelve bytes, a track locked when its first word is non-zero, walked with a mask that
/// starts at 4 and shifts right (compare <c>AreAnyTracksLocked</c> 0x0063EF88) - and when that track's
/// word is zero it RETURNS, at 0x0063E3D0. Mask 4 is the body. So the engine only looks for unexpected
/// movement while something owns the wheels, which is exactly when the motion is supposed to match the
/// command; <c>MovementComponent::DirectDriveCheckSpeedAndLockTracks</c>, the symbol next to
/// <c>AreAnyTracksLocked</c>, is one of the things that takes that lock. This stack had a
/// <c>Suspended</c> flag that switched the check off while a caller drove, which is the opposite.
///
/// The gate applies only while the robot's flag at <c>Robot+0x248</c> is set (0x0063E3BA); with it clear
/// the engine skips the track loop and checks whatever owns the body. That flag was not traced to its
/// writer, so <see cref="TrackGateApplies"/> defaults to false and the check runs.
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
/// What the engine does next - rewind the pose and leave an obstacle behind - is
/// <see cref="UnexpectedMovementResponse"/>.
/// </summary>
public static class UnexpectedMovementResponse
{
    /// <summary>Clearance added to the obstacle's own depth: 5 mm (<c>vmov.f32 s4, #5.0</c> at 0x0063E6C6).</summary>
    public const float ClearanceMm = 5f;
    /// <summary>The obstacle sits this far in front of the robot's origin, past its own depth: 22.1 mm (0x41B0CCCC).</summary>
    public const float FrontOffsetMm = 22.1f;
    /// <summary>...or this far behind: −55.9 mm (0xC25F999A).</summary>
    public const float BackOffsetMm = -55.9f;
    /// <summary>...or this far to one side: ±27.1 mm (0x41D8CCCD / 0xC1D8CCCD), with the pose turned ±90°.</summary>
    public const float SideOffsetMm = 27.1f;

    /// <summary>
    /// Where the obstacle goes, in the robot's own frame
    /// (<c>MovementComponent::CheckForUnexpectedMovement</c> 0x0063E6BC..0x0063E842). The depth is the
    /// collision obstacle's own x, 20 mm, plus the 5 mm clearance; the four cases are the four the wheel
    /// averages pick out:
    /// <list type="bullet">
    /// <item>Front (both wheels forward): +(depth + 22.1) along x, no rotation (0x0063E7A4).</item>
    /// <item>Back (neither): −55.9 − depth along x, no rotation (0x0063E81A).</item>
    /// <item>Left (only the right wheel forward, so the robot turned left): the pose turned +π/2 about Z
    /// and moved +(depth + 27.1) along its y (0x0063E7D4).</item>
    /// <item>Right (only the left wheel forward): turned −π/2 and moved −(depth + 27.1) (0x0063E704).</item>
    /// </list>
    /// The translation is written straight into the rotated pose's transform, so it is in the rotated
    /// frame, which is why the two side cases carry a sign as well as a rotation.
    /// </summary>
    public static Pose3d ObstacleInRobotFrame(UnexpectedMovementSide side)
    {
        double depth = (MarkerlessObject.SizeByType(ObjectType.CollisionObstacle)!.Value.X) + ClearanceMm;
        return side switch
        {
            UnexpectedMovementSide.Front => new Pose3d(Mat3.Identity, new Vec3(depth + FrontOffsetMm, 0, 0)),
            UnexpectedMovementSide.Back => new Pose3d(Mat3.Identity, new Vec3(BackOffsetMm - depth, 0, 0)),
            UnexpectedMovementSide.Left => new Pose3d(Math.PI / 2, new Vec3(0, 0, 1), new Vec3(0, depth + SideOffsetMm, 0)),
            UnexpectedMovementSide.Right => new Pose3d(-Math.PI / 2, new Vec3(0, 0, 1), new Vec3(0, -(depth + SideOffsetMm), 0)),
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "no obstacle is placed for this side"),
        };
    }

    /// <summary>The same pose in the world, parented to the robot as <c>SetParent</c> does at 0x0063E8C4.</summary>
    public static Pose3d ObstacleInWorld(Pose3d robotPose, UnexpectedMovementSide side) =>
        robotPose.Compose(ObstacleInRobotFrame(side));

    /// <summary>
    /// The pose <c>Robot::SetNewPose</c> receives (0x0063E842..0x0063E87E): the robot's pose at the
    /// timestamp the disagreement began, with the rotation it has now copied over it - the 32-byte loop at
    /// 0x0063E868 overwrites the historical transform's rotation and leaves its translation, which lives
    /// at +0x20. So the robot is put back where it was and keeps the heading it ended up with.
    /// </summary>
    public static Pose3d RewoundPose(Pose3d atStartOfMovement, Pose3d now) =>
        new(now.Rotation, atStartOfMovement.Translation);

    /// <summary>
    /// The whole response, in the engine's order: look the start timestamp up in the state history, rewind
    /// the pose, then leave a collision obstacle on the side the wheels point to. Returns null - and logs
    /// the engine's "Could not get robot pose at t=%u" - when the history has nothing at that time, which
    /// is the one case where the engine does neither.
    ///
    /// Only the obstacle is applied here. The engine owns the robot's pose and hands the rewound one to
    /// <c>Robot::SetPose</c>, which its own <c>Robot::Update</c> then sends on to the robot as an
    /// <c>AbsoluteLocalizationUpdate</c> (<c>SendAbsLocalizationUpdate</c> 0x00514710 takes the latest
    /// vision-only state, which <c>SetNewPose</c> has just added). This stack reads the robot's pose from
    /// its state stream, so the rewound pose is returned for the caller to send.
    /// </summary>
    public static (Pose3d Rewound, ObservableObject Obstacle)? Apply(
        UnexpectedMovementReport report, RobotStateHistory history, BlockWorld world, Pose3d currentRobotPose,
        Action<string>? log = null)
    {
        if (report.Side == UnexpectedMovementSide.Unknown) return null;
        if (history.At(report.Timestamp) is not { } at)
        {
            log?.Invoke($"MovementComponent.CheckForUnexpectedMovement.PoseHistoryFailure: Could not get robot pose at t={report.Timestamp}");
            return null;
        }
        var rewound = RewoundPose(at.RobotPose, currentRobotPose);
        var obstacle = world.AddCollisionObstacle(ObstacleInWorld(rewound, report.Side));
        log?.Invoke($"MovementComponent.CheckForUnexpectedMovement.AddingCollisionObstacle: Adding obstacle {Name(report.Side)} robot");
        return (rewound, obstacle);
    }

    /// <summary>The word the engine puts in "Adding obstacle %s robot" (0x0063EAC8..0x0063EAF2).</summary>
    private static string Name(UnexpectedMovementSide side) => side switch
    {
        UnexpectedMovementSide.Front => " front of",
        UnexpectedMovementSide.Back => "behind",
        UnexpectedMovementSide.Left => " left of",
        UnexpectedMovementSide.Right => " right of",
        _ => "",
    };
}

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
    /// <summary>
    /// Whether the body track is locked - something owns the wheels. The engine's gate: with
    /// <see cref="TrackGateApplies"/> set and this clear, <c>CheckForUnexpectedMovement</c> returns at
    /// 0x0063E3D0 without looking.
    /// </summary>
    public bool BodyTrackLocked { get; set; }

    /// <summary>
    /// Whether the body-track gate applies at all: the engine's <c>Robot+0x248</c> (0x0063E3BA).
    /// </summary>
    public bool TrackGateApplies { get; set; }

    /// <summary>A local switch for a caller that wants the check off; the engine has no equivalent.</summary>
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

    // fidelity: M1-025, M1-015
    /// <summary>
    /// <see cref="Reset"/> plus the last report and the Robot gates, back to their as-constructed values, for a
    /// removed robot (CB33, CC26, CC27). <see cref="Suspended"/> is a caller's switch with no engine counterpart and
    /// is kept, as are subscribers.
    /// </summary>
    internal void ResetToConstructed()
    {
        Reset();
        Last = null;
        IsPhysical = true;
        BodyTrackLocked = false;
        TrackGateApplies = false;
    }

    /// <summary>Feeds one robot state. Returns the report when one is raised on this state.</summary>
    public UnexpectedMovementReport? Update(RobotState s)
    {
        if (!IsPhysical || Suspended) return null;
        if (TrackGateApplies && !BodyTrackLocked) return null;
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
