using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    private async Task<CommandResult> TurnToPixel(uint id, int x, int? y, CancellationToken token)
    {
        var engine = Robot.Engine;
        EngineRobot owner;
        uint origin;
        float body;
        float? head;
        Task<CommandResult> turn;
        using (engine.EnterSynchronousOwner())
        {
            (EngineRobot Owner, CameraFrame Frame, uint Origin)? snapshot;
            lock (cameraGate) snapshot = returnedImage;
            if (snapshot is not { } image || image.Frame.ImageId != id ||
                !ReferenceEquals(image.Owner, engine.Robot) || image.Owner.Lifetime.Destroying ||
                image.Owner.CurrentOriginId != image.Origin)
                return Refused("Unknown or stale imageId. Read get_camera_image again.");
            owner = image.Owner;
            origin = image.Origin;
            uint timestamp = image.Frame.Timestamp;
            if (timestamp < owner.PoseHistory.OldestTimeStamp || timestamp > owner.PoseHistory.NewestTimeStamp ||
                timeProvider.GetUtcNow() - image.Frame.ReceivedUtc > TimeSpan.FromMilliseconds(owner.PoseHistory.WindowMs))
                return Refused("Image is outside the pose history window. Read get_camera_image again.");
            // Calibration from the robot NV callback 0x0065AB68, installed at 0x0065ADE0.
            var cal = Robot.Vision.NativeOuterCalibrationFor(owner);
            if (cal is null || cal.Columns == 0 || cal.Rows == 0 || !(cal.FocalLengthX > 0 && cal.FocalLengthY > 0))
                return Refused("Camera calibration unavailable.");
            using var history = new HistRobotState();
            uint resolved = 0;
            if (owner.PoseHistory.ComputeStateAt(timestamp, ref resolved, history, false) != 0 ||
                history.State.PoseOriginId != origin)
                return Refused("No exposure pose in the current origin. Read get_camera_image again.");
            var scaled = cal.Scaled(EncodedImageDecoder.Columns, EncodedImageDecoder.Rows);
            // ComputeTurnTowardsImagePointAngles 0x0051879C uses the exposure pose.
            var angles = TurnTowardsImagePoint.Angles(scaled, x, y ?? scaled.CenterY, history.Pose.AngleAroundZ, history.State.HeadAngle);
            body = (float)angles.BodyRad;
            head = y is null ? null : (float)angles.HeadRad;
            turn = FromAction(Robot.Motion.QueueAiTurnInPlaceAsync(body, TurnInPlaceAction.DefaultMaxSpeed,
                TurnInPlaceAction.DefaultAccel, absolute: true, cancellationToken: token));
        }
        var result = await turn.ConfigureAwait(false);
        if (result.EngineResult == 0 && head is { } tilt)
        {
            Task<CommandResult> move;
            using (engine.EnterSynchronousOwner())
            {
                if (!ReferenceEquals(owner, engine.Robot) || owner.Lifetime.Destroying || owner.CurrentOriginId != origin)
                    return Refused("Owner or origin changed during turn.") with { Report = new { imageId = id, bodyRadians = body } };
                move = FromAction(Robot.Motion.QueueAiHeadAngleAsync(tilt, token));
            }
            result = await move.ConfigureAwait(false);
        }
        return result with { Report = new { imageId = id, bodyRadians = body, headRadians = head } };
    }

    private static CommandResult Refused(string reason) => new("not-admitted", false, null, null, reason);

    private object FailureHelp(string command, BridgeArguments args)
    {
        using var read = Robot.Engine.EnterSynchronousOwner();
        using var poses = Robot.Vision.World.EnterPoseMutation();
        var owner = Robot.Engine.Robot;
        RelativePose? relative = null;
        uint? observed = null;
        bool? anchored = null;
        bool charger = command is "go-home" or "mount-charger";
        bool face = command is "come-here" or "search-for-face";
        if (owner is not null && !owner.Lifetime.Destroying && Robot.Engine.ConnectionState == 2)
        {
            if (charger)
            {
                uint? id = command == "mount-charger" ? args.UInt("chargerId") : null;
                var target = Robot.Vision.World.LocatedObjects.Where(o => o.Type == ObjectType.Charger_Basic &&
                    (id == null || o.ObjectId == id)).OrderByDescending(o => o.LastObservedTimestamp).FirstOrDefault();
                if (target is not null)
                {
                    relative = Relative(target.NativePose);
                    observed = target.LastObservedTimestamp;
                    anchored = relative is not null;
                }
                if (owner.Engine.C2ChargerMemory.Get(owner) is { } memory && (id == null || id == memory.WorldObjectId)
                    && (observed == null || memory.LastObservedTimestamp >= observed))
                {
                    using var pose = owner.Engine.C2ChargerMemory.CopyAnchoredPose(owner);
                    relative = pose is null ? null : Relative(pose);
                    observed = memory.LastObservedTimestamp;
                    anchored = memory.Anchored;
                }
            }
            else if (face && Robot.Vision.Faces.Faces.OrderByDescending(f => f.LastObservedTimestamp).FirstOrDefault() is { } seen)
            {
                relative = Relative(seen.HeadPose);
                observed = seen.LastObservedTimestamp;
                anchored = relative is not null;
            }
        }
        uint? now = Robot.State.Latest?.Timestamp;
        uint? ageMs = now >= observed ? now - observed : null;
        string next = relative is { } located
            ? FormattableString.Invariant($"Look with get_camera_image toward bearing {located.BearingDeg:F0} degrees and take a short clear step if you see the target.")
            : charger ? "Look for the charger with get_camera_image and centre it before driving closer."
            : face ? "Look for a person with get_camera_image and centre them before asking Cozmo to find their face."
            : "Look with get_camera_image and get_map before choosing a short clear drive.";
        return new { anchored, distanceMm = relative?.DistanceMm, bearingDeg = relative?.BearingDeg, ageMs, next };
    }
}
