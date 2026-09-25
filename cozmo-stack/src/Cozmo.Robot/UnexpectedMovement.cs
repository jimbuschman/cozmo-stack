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

// fidelity: M10-007
/// <summary>
/// The engine's UnexpectedMovement broadcast (B17): {timestamp = the current state's timestamp, movementType,
/// movementSide}, plus what it was computed from: <see cref="StartTimestamp"/> (+0x94, the history lookup time), the
/// wheel means and the count at the fire.
/// </summary>
public sealed record UnexpectedMovementReport(
    uint Timestamp, UnexpectedMovementType Type, UnexpectedMovementSide Side,
    float AverageLeftMmps, float AverageRightMmps, int Count)
{
    /// <summary>+0x94: the robot timestamp of the first increment (B10), which ComputeStateAt is asked for (B13).</summary>
    public uint StartTimestamp { get; init; }

    public override string ToString() =>
        $"unexpected movement {Type} from {Side} at {Timestamp} (avg wheels {AverageLeftMmps:F0}/{AverageRightMmps:F0} mm/s over {Count})";
}

/// <summary>What <c>RobotStateHistory::ComputeStateAt(t, &amp;t, &amp;hist, true)</c> returns (B13).</summary>
public enum HistoryLookupResult
{
    /// <summary>0: the historical state was computed.</summary>
    Ok,
    /// <summary>0x6000000: "PoseHistoryOriginMismatch" (info).</summary>
    OriginMismatch,
    /// <summary>Any other non-zero result: "PoseHistoryFailure" (warning).</summary>
    Failure,
}

// fidelity: M10-007
/// <summary>
/// The geometry of the unexpected-movement response (B14..B16, gap1 7a..7b).
/// </summary>
public static class UnexpectedMovementResponse
{
    /// <summary>B14: d = GetSizeByType(CollisionObstacle)[0] + 5.</summary>
    public const float ClearanceMm = 5f;
    /// <summary>B14: FRONT (d + 22.1, 0, 0).</summary>
    public const float FrontOffsetMm = 22.1f;
    /// <summary>B14: BACK (−55.9 − d, 0, 0).</summary>
    public const float BackOffsetMm = -55.9f;
    /// <summary>B14: LEFT (0, d + 27.1, 0) at +π/2; RIGHT (0, −27.1 − d, 0) at −π/2.</summary>
    public const float SideOffsetMm = 27.1f;

    /// <summary>
    /// B14 with gap1 7a/7b: d = 20 + 5 = 25, so FRONT x = 47.1, BACK x = −80.9, LEFT y = +52.1 (rotation +π/2),
    /// RIGHT y = −52.1 (rotation −π/2), in the frame of the robot pose after SetNewPose (B16).
    /// </summary>
    public static Pose3d ObstacleInRobotFrame(UnexpectedMovementSide side)
    {
        double depth = MarkerlessObject.SizeByType(ObjectType.CollisionObstacle)!.Value.X + ClearanceMm;
        return side switch
        {
            UnexpectedMovementSide.Front => new Pose3d(Mat3.Identity, new Vec3(depth + FrontOffsetMm, 0, 0)),
            UnexpectedMovementSide.Back => new Pose3d(Mat3.Identity, new Vec3(BackOffsetMm - depth, 0, 0)),
            UnexpectedMovementSide.Left => new Pose3d(Math.PI / 2, new Vec3(0, 0, 1), new Vec3(0, depth + SideOffsetMm, 0)),
            UnexpectedMovementSide.Right => new Pose3d(-Math.PI / 2, new Vec3(0, 0, 1), new Vec3(0, -(depth + SideOffsetMm), 0)),
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "no obstacle is placed for this side"),
        };
    }

    /// <summary>B16 steps 1..2: the obstacle parented to the robot pose after SetNewPose, taken with respect to the root.</summary>
    public static Pose3d ObstacleInWorld(Pose3d robotPoseAfterSetNewPose, UnexpectedMovementSide side) =>
        robotPoseAfterSetNewPose.Compose(ObstacleInRobotFrame(side));

    /// <summary>
    /// B15 steps 1..2: the historical pose with its 32-byte Rotation3d overwritten by the robot's current rotation,
    /// which is the pose SetNewPose receives.
    /// </summary>
    public static Pose3d RewoundPose(Pose3d historical, Pose3d now) => new(now.Rotation, historical.Translation);

    /// <summary>The word the engine puts in "Adding obstacle %s robot" (log strings 0x63EAC8..0x63EAF0).</summary>
    public static string Name(UnexpectedMovementSide side) => side switch
    {
        UnexpectedMovementSide.Front => " front of",
        UnexpectedMovementSide.Back => "behind",
        UnexpectedMovementSide.Left => " left of",
        UnexpectedMovementSide.Right => " right of",
        _ => "",
    };
}

// fidelity: M10-002, M10-006, M10-007
/// <summary>
/// <c>MovementComponent::CheckForUnexpectedMovement</c> (0x0063E398), tail-called from MovementComponent::Update on
/// every synced state (B1), rows B1..B18.
///
/// <b>Gates (M10-006, B2..B5):</b> nothing while robot+0x14 (physical, M10-010) is 0; while robot+0x248
/// (AnimationState.tag) ≠ 0 the BODY track must be locked, otherwise return; IS_PICKING_OR_PLACING returns with no
/// reset; status &amp; 0x5008 resets +0x94..+0xA0 and returns.
///
/// <b>Rules (M10-002, B6..B11):</b> |l|+|r| &lt; 20 decays; quiet gyro (&lt; 0.174533): opposite wheel signs, or
/// |0.5·|(r−l)/46| − |gz|| &gt; 0.2, adds 1 with type 0; active gyro with the opposite sign adds 2 with type 2
/// (sums += 2l, 2r); the same sign decays and returns. The first increment stamps +0x94 with the state timestamp; it
/// fires at count &gt; 10.
///
/// <b>Response (M10-007, B12..B18):</b> gate IsReactionTriggerEnabled(20); ComputeStateAt(+0x94); side from the wheel
/// means; the rewind and the obstacle; then on every fire the broadcast and the reset.
///
/// MISSING (B8): whether the +1 paths add l and r to the sums (+0x98/+0x9C). B9 says the +2 path adds 2l and 2r;
/// B8 is silent. The +1 paths here add l and r once, as the existing code did, until the row is extracted.
/// MISSING (B9): whether the same-sign decrement is guarded by count &gt; 0 as B7's is ("count−− and return"). The
/// guard is kept until the row is extracted.
/// MISSING (B15): the AbsoluteLocalizationUpdate that SetNewPose leads to (AddVisionOnlyStateToHistory → +0x2C6 →
/// Robot::Update's SendAbsLocalizationUpdate(), M4 C9 F1..F3): its field values (timestamp, frame id after the ++,
/// origin, pose) are not in any inventory row (the F rows are not in the M4 inventory file). So the rewind is not
/// performed, and neither is the obstacle (B16), whose parent is the rewound pose; both are logged as MISSING.
/// </summary>
public sealed class UnexpectedMovementDetector
{
    // ---------------------------------------------------------------- B6 constants
    /// <summary>B6: wheelbase literal 46.0 at 0x63E7D0.</summary>
    public const float WheelDistanceMm = 46f;
    /// <summary>B6: +0xB0 = 20.0.</summary>
    public const float MinWheelSpeedSumMmps = 20f;
    /// <summary>B6: +0xA4 = 0.174533 rad/s.</summary>
    public const float GyroTurnThresholdRadps = 0.174533f;
    /// <summary>B6: +0xB4 = 0.2.</summary>
    public const float RotationMismatchToleranceRadps = 0.2f;
    /// <summary>B6: +0xAC = 10 (u8); B11 fires at count &gt; 10.</summary>
    public const int CountThreshold = 10;
    /// <summary>B14: ε = 1e-5 on the wheel means.</summary>
    public const float SideEpsilonMmps = 1e-5f;
    /// <summary>B5: IS_PICKED_UP | IS_ON_CHARGER | CLIFF_DETECTED (0x5008).</summary>
    public const RobotStatusFlag ResetFlags =
        RobotStatusFlag.IsPickedUp | RobotStatusFlag.IsOnCharger | RobotStatusFlag.CliffDetected;
    /// <summary>B12: the trigger whose enable state gates the rewind and the obstacle (20 = UnexpectedMovement).</summary>
    public const byte UnexpectedMovementTrigger = 20;

    // ---------------------------------------------------------------- inputs (copied in by CozmoSensors per state)
    /// <summary>B2: robot+0x14, 0 until FirmwareVersion (M10-010).</summary>
    public bool IsPhysical { get; set; }
    /// <summary>B3: robot+0x248, the tag of the last AnimationState (M3-012 C10).</summary>
    public byte AnimationStateTag { get; set; }
    /// <summary>B3: the BODY track's lock set is non-empty (MovementComponent+0x30 + 12·2; M4-014).</summary>
    public bool BodyTrackLocked { get; set; }

    // ---------------------------------------------------------------- response seams (B12, B13)
    /// <summary>
    /// B12: BehaviorManager::IsReactionTriggerEnabled(20). A <see cref="Behavior.BehaviorManager"/> installs it. Null (no
    /// manager attached) takes the disabled path: side UNKNOWN, no rewind, no obstacle.
    /// </summary>
    public Func<bool>? ReactionTriggerEnabled { get; set; }
    /// <summary>
    /// B13: RobotStateHistory::ComputeStateAt(+0x94, &amp;t, &amp;hist, true), the M11 interface; returns the result and the
    /// historical pose. Null (no ComputeStateAt in this stack) takes the failure path, logged as MISSING.
    /// </summary>
    public Func<uint, (HistoryLookupResult Result, Pose3d Pose)>? ComputeStateAt { get; set; }
    /// <summary>B15: Robot::GetPose, the robot's current pose, for the rotation copy (an M11 interface).</summary>
    public Func<Pose3d?>? CurrentRobotPose { get; set; }

    // ---------------------------------------------------------------- state (+0x94..+0xA0)
    /// <summary>+0xA0: the count.</summary>
    public int Count { get; private set; }
    /// <summary>+0x94: the robot timestamp of the first increment.</summary>
    public uint StartTimestamp { get; private set; }
    /// <summary>+0x98 / +0x9C: the wheel-speed sums.</summary>
    public float SumLeft { get; private set; }
    public float SumRight { get; private set; }
    /// <summary>The last broadcast (not an engine field; ReactToUnexpectedMovement reads it).</summary>
    public UnexpectedMovementReport? Last { get; private set; }

    /// <summary>B17: the UnexpectedMovement broadcast, raised on every fire.</summary>
    public event Action<UnexpectedMovementReport>? Detected;
    /// <summary>The engine's log lines (B11, B13, B14, B15, B16).</summary>
    public event Action<string>? Log;

    /// <summary>B5 / B17: +0x94..+0xA0 = 0.</summary>
    public void Reset()
    {
        Count = 0; StartTimestamp = 0; SumLeft = 0; SumRight = 0;
    }

    // fidelity: M1-025, M1-015
    /// <summary>
    /// <see cref="Reset"/> plus the last report and the Robot inputs, back to their as-constructed values, for a removed
    /// robot (CB33, CC26, CC27). Seams and subscribers are kept.
    /// </summary>
    internal void ResetToConstructed()
    {
        Reset();
        Last = null;
        IsPhysical = false;
        AnimationStateTag = 0;
        BodyTrackLocked = false;
    }

    /// <summary>Feeds one synced robot state. Returns the broadcast when one is raised on this state.</summary>
    public UnexpectedMovementReport? Update(RobotState s)
    {
        if (!IsPhysical) return null;                                            // B2
        if (AnimationStateTag != 0 && !BodyTrackLocked) return null;             // B3
        if (s.Has(RobotStatusFlag.IsPickingOrPlacing)) return null;              // B4: no reset
        if ((s.Status & (uint)ResetFlags) != 0) { Reset(); return null; }        // B5

        float l = s.LwheelSpeedMmps, r = s.RwheelSpeedMmps;
        if (MathF.Abs(l) + MathF.Abs(r) < MinWheelSpeedSumMmps)                  // B7
        {
            if (Count > 0) Count--;
            return null;
        }

        float gz = s.Gyro.Z;
        UnexpectedMovementType type;
        if (MathF.Abs(gz) < GyroTurnThresholdRadps)                              // B8
        {
            type = UnexpectedMovementType.TurnedButStopped;
            if (float.IsNegative(l) != float.IsNegative(r)) Accumulate(s, l, r, 1);
            else if (MathF.Abs(0.5f * MathF.Abs((r - l) / WheelDistanceMm) - MathF.Abs(gz)) > RotationMismatchToleranceRadps)
                Accumulate(s, l, r, 1);
        }
        else                                                                      // B9
        {
            if (float.IsNegative(r - l) == float.IsNegative(gz))
            {
                if (Count > 0) Count--;                                          // MISSING (B9): the guard
                return null;
            }
            type = UnexpectedMovementType.TurnedInOppositeDirection;
            Accumulate(s, l, r, 2);
        }

        if (Count <= CountThreshold) return null;                                // B11
        Log?.Invoke($"MovementComponent.CheckForUnexpectedMovement: Unexpected movement detected {TypeName(type)}");
        var side = Respond(Count);                                               // B12..B16
        // B17: the broadcast on every fire, with the current state's timestamp, then the reset.
        var report = new UnexpectedMovementReport(s.Timestamp, type, side, SumLeft / Count, SumRight / Count, Count)
        { StartTimestamp = StartTimestamp };
        Last = report;
        Detected?.Invoke(report);
        Reset();
        return report;
    }

    /// <summary>B12..B16. Returns the side the broadcast carries.</summary>
    private UnexpectedMovementSide Respond(int n)
    {
        if (ReactionTriggerEnabled?.Invoke() != true) return UnexpectedMovementSide.Unknown;          // B12
        if (ComputeStateAt is not { } lookup)
        {
            Log?.Invoke("MISSING: MovementComponent.CheckForUnexpectedMovement: no RobotStateHistory::ComputeStateAt (M11 interface); the failure path is taken");
            return UnexpectedMovementSide.Unknown;
        }
        var (result, hist) = lookup(StartTimestamp);                                                     // B13
        if (result == HistoryLookupResult.OriginMismatch)
        {
            Log?.Invoke("info: MovementComponent.CheckForUnexpectedMovement.PoseHistoryOriginMismatch");
            return UnexpectedMovementSide.Unknown;
        }
        if (result != HistoryLookupResult.Ok)
        {
            Log?.Invoke($"warning: MovementComponent.CheckForUnexpectedMovement.PoseHistoryFailure: Could not get robot pose at t={StartTimestamp}");
            return UnexpectedMovementSide.Unknown;
        }
        // B14
        float meanL = SumLeft / n, meanR = SumRight / n;
        bool lf = meanL > SideEpsilonMmps, rf = meanR > SideEpsilonMmps;
        var side = lf && rf ? UnexpectedMovementSide.Front
                 : !lf && !rf ? UnexpectedMovementSide.Back
                 : rf ? UnexpectedMovementSide.Left
                 : UnexpectedMovementSide.Right;
        // B15, B16: MISSING (see the summary). The poses they would use are computable; the send is not.
        var now = CurrentRobotPose?.Invoke();
        if (now is { } p)
        {
            var rewound = UnexpectedMovementResponse.RewoundPose(hist, p);
            Log?.Invoke($"MISSING: MovementComponent.CheckForUnexpectedMovement: SetNewPose({rewound.Translation}) and its AbsoluteLocalizationUpdate are not sent (M4 C9 F1..F3 not in the rows); the collision obstacle{UnexpectedMovementResponse.Name(side)} robot is not added");
        }
        else Log?.Invoke("MISSING: MovementComponent.CheckForUnexpectedMovement: the rewind (SetNewPose) and the collision obstacle are not applied");
        return side;
    }

    private void Accumulate(RobotState s, float l, float r, int by)
    {
        if (Count == 0) StartTimestamp = s.Timestamp;                                                  // B10
        Count += by;
        SumLeft += l * by;
        SumRight += r * by;
    }

    /// <summary>B11: the type names in the warning (strings 0x63EA70/0x63EAA0).</summary>
    private static string TypeName(UnexpectedMovementType t) => t switch
    {
        UnexpectedMovementType.TurnedButStopped => "TURNED_BUT_STOPPED",
        UnexpectedMovementType.TurnedInOppositeDirection => "TURNED_IN_OPPOSITE_DIRECTION",
        _ => t.ToString(),
    };
}
