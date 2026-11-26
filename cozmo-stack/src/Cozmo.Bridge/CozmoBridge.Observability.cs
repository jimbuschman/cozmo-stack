using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    // Presentation only. RobotState (F0, M2-012) contains pitch and yaw,
    // but no roll field. UFRS stores pitch at 0x00512974..82, raw accel and
    // gyro at 0x005129A4..BE, and MOVING/head-moving at 0x0063E30A..328.
    private object? ReadPhysicalState(RobotState? raw)
    {
        if (raw is null || Robot.Engine.Robot is not { } owner) return null;
        var sensors = Robot.Sensors;
        var treads = sensors.OffTreads;
        return new
        {
            robotTimestamp = raw.Timestamp,
            pitchRad = Finite(raw.Pose.Pitch),
            rollRad = (double?)null, // No received roll; do not invent a measurement.
            gyroRadps = Axes(raw.Gyro.X, raw.Gyro.Y, raw.Gyro.Z),
            accelerometerMmps2 = Axes(raw.Accel.X, raw.Accel.Y, raw.Accel.Z),
            filteredAccelMmps2 = Axes(treads.FilteredAccel.X, treads.FilteredAccel.Y, treads.FilteredAccel.Z),
            filteredAccelMagnitudeMmps2 = Finite(treads.FilteredAccelMagnitude),
            status = raw.Status,
            moving = owner.NativeMovementIsMoving,
            still = !owner.NativeMovementIsMoving, // Complement of native IsMoving, not an extra sensor.
            headMoving = owner.NativeMovementHeadMoving,
            bodyMoving = Robot.Motion.BodyMoving,
            wheelsMoving = raw.Has(RobotStatusFlag.AreWheelsMoving),
            headInPosition = raw.Has(RobotStatusFlag.HeadInPos),
            liftInPosition = raw.Has(RobotStatusFlag.LiftInPos),
            pickedUp = raw.Has(RobotStatusFlag.IsPickedUp),
            falling = raw.Has(RobotStatusFlag.IsFalling),
            cliffDetected = raw.Has(RobotStatusFlag.CliffDetected),
            offTreadsState = treads.Current.ToString(),
            offTreadsCandidate = treads.Candidate.ToString(),
            offTreadsCandidateTimeMs = treads.CandidateTimeMs,
        };
    }

    private static double? Finite(double value) => double.IsFinite(value) ? value : null;
    private static object Axes(float x, float y, float z) => new { x = Finite(x), y = Finite(y), z = Finite(z) };

    // 0x005A3550..0x005A37AA skips locked or noninterrupting strategies before
    // the predicate. Report the last *actual* evaluation alongside those live
    // gates, without calling WantsToRun (which consumes native latches).
    private object[] ReadPhysicalReactionChecks()
    {
        if (Robot.Engine.Robot?.BehaviorManagerOwner is not { } manager) return [];
        var current = manager.CurrentReactionTrigger;
        var detector = Robot.Sensors.UnexpectedMovement;
        return manager.Reactions.Where(reg => reg.Strategy.Trigger is
            ReactionTrigger.CliffDetected or ReactionTrigger.RobotFalling or ReactionTrigger.RobotPickedUp or
            ReactionTrigger.RobotPlacedOnSlope or ReactionTrigger.ReturnedToTreads or ReactionTrigger.RobotOnBack or
            ReactionTrigger.RobotOnFace or ReactionTrigger.RobotOnSide or ReactionTrigger.UnexpectedMovement)
            .Select(reg =>
            {
                var strategy = reg.Strategy;
                bool mayInterrupt = current is null || (current == strategy.Trigger ? strategy.CanInterruptSelf : strategy.CanInterruptOtherTriggeredBehavior);
                object? evaluation = strategy switch
                {
                    PlacedOnSlopeStrategy slope => slope.LastEvaluation,
                    GenericReactionStrategy generic => generic.LastEvaluation,
                    _ => null,
                };
                object? impact = reg.Behavior is ReactToImpactBehavior i ? new
                {
                    lastIntensity = i.LastImpactIntensity,
                    intensityThreshold = ReactionTable.ImpactIntensityThreshold, // strict >, 0x0060643C..4E
                    impactRecorded = i.ImpactRecorded,
                    fallingStoppedSeen = i.FallingStoppedSeen,
                    calibratedRecorded = i.CalibratedRecorded,
                    waitedOut = i.WaitedOut,
                } : null;
                return (object)new
                {
                    trigger = strategy.Trigger.ToString(),
                    behavior = reg.Behavior.Id,
                    enabled = manager.IsReactionTriggerEnabled(strategy.Trigger),
                    currentReaction = current?.ToString(),
                    mayInterrupt,
                    source = strategy.Basis,
                    lastEvaluation = evaluation,
                    impact,
                    detector = strategy.Trigger == ReactionTrigger.UnexpectedMovement ? detector.LastEvaluation : null,
                };
            }).ToArray();
    }

    private void OnHiccupsStarted(HiccupStartSource source) => Add("hiccups-started", new
    {
        source = source == HiccupStartSource.Scheduled ? "scheduled" : "overfeeding-notification",
        gameToEngineTag = source == HiccupStartSource.OverfeedingNotification ? 164 : (int?)null,
    });

    private sealed record ChargerEstimate(uint LastObservedTimestamp, double? PitchDeg, double? RollDeg,
        double? ViewingAngleDeg, double? CurrentViewingAngleDeg);

    // Orientation and views are host geometry only. Use the actual stored
    // charger rotation (0x004E9FB0..0x004EA08C retains it); the native pre-dock
    // pose is on its -X axis, facing +X into the charger (0x004E9FE4..0x004EA01A).
    private static ChargerEstimate ReadChargerEstimate(ObservableObject charger, Pose3d worldPose, EngineRobot owner)
    {
        Radians roll = new(), pitch = new(), yaw = new();
        worldPose.Rotation.GetEulerAngles(ref roll, ref pitch, ref yaw);
        const double degrees = 180 / Math.PI;
        double? AngleFrom(Pose3d robotPose)
        {
            using var relative = new Pose3d();
            if (!Pose3d.GetWithRespectTo(robotPose, charger.NativePose, relative)) return null;
            var t = relative.Translation;
            if (t.X == 0 && t.Y == 0) return null;
            // Absolute 0..180 degrees from the front-facing (-X) charger axis.
            return Finite(Math.Abs(Math.Atan2(t.Y, -t.X)) * degrees);
        }
        double? observedView = null;
        if (charger.LastObservedTimestamp != 0)
        {
            using var historical = new HistRobotState();
            uint resolved = 0;
            if (owner.PoseHistory.ComputeStateAt(charger.LastObservedTimestamp, ref resolved, historical, true) == 0 &&
                resolved == charger.LastObservedTimestamp)
                observedView = AngleFrom(historical.Pose);
        }
        return new(charger.LastObservedTimestamp, Finite(pitch.Value * degrees), Finite(roll.Value * degrees),
            observedView, AngleFrom(owner.GetCheckedMainPose()));
    }
}
