using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    private sealed record Intent(Guid Id, Guid Session, string Command, BridgeArguments Args,
        long Cursor, DateTimeOffset Started, RobotState? Initial, EngineRobot? Owner, WorldPose? InitialPose)
    {
        internal TaskCompletionSource<string> Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed record Observation(string Status, string Reason, string? Evidence = null, double? AgeMs = null);
    private readonly AsyncLocal<Intent?> activeIntent = new();
    private readonly Dictionary<uint, Intent> trackingIntents = [];
    private readonly HashSet<Intent> pendingIntents = [];
    private Guid outcomeSession = Guid.NewGuid();
    private RobotState? outcomeState;
    private DateTimeOffset outcomeStateTime;

    private Intent BeginIntent(string command, BridgeArguments args)
    {
        var pose = OutcomePose(command);
        lock (eventGate)
        {
            ThrowIfDisposed();
            var intent = new Intent(Guid.NewGuid(), outcomeSession, command, args, lastCursor, timeProvider.GetUtcNow(),
                outcomeState ?? Robot.State.Latest, Robot.Engine.Robot, pose);
            pendingIntents.Add(intent);
            activeIntent.Value = intent;
            return intent;
        }
    }

    // Caller holds eventGate. A new link cannot inherit an old native tag or an old F0.
    private void EndOutcomeSession(string reason)
    {
        foreach (var ticket in pendingIntents) ticket.Closed.TrySetResult(reason);
        foreach (var ticket in trackingIntents.Values) ticket.Closed.TrySetResult(reason);
        outcomeSession = Guid.NewGuid();
        eventCaller = new object();
        var endedTracks = trackingIntents.ToArray();
        trackingIntents.Clear();
        foreach (var (tag, ticket) in endedTracks)
            Add("outcome-closed", new { actionTag = tag, intentId = ticket.Id, sessionId = ticket.Session, status = "unknown", reason });
        outcomeState = null;
    }

    private WorldPose? OutcomePose(string command)
    {
        if (command is not ("turn-in-place" or "go-to-pose" or "turn-toward-image-point")) return null;
        using var owner = Robot.Engine.EnterSynchronousOwner();
        using var poses = Robot.Vision.World.EnterPoseMutation();
        return ReadWorldPose();
    }

    private JsonNode DescribeOutcome(Intent intent, CommandResult result, JsonObject? response, int? terminalFace = null, WorldPose? poseAfter = null)
    {
        var now = timeProvider.GetUtcNow();
        var observed = Observe();
        return JsonSerializer.SerializeToNode(new
        {
            intentId = intent.Id, sessionId = intent.Session, submitted = true, submittedUtc = intent.Started,
            admitted = result.Admitted, terminalEngineResult = result.EngineResult, observedUtc = now, physical = observed,
        }, Json)!;

        Observation Observe()
        {
            if (intent.Closed.Task.IsCompletedSuccessfully) return new("unknown", intent.Closed.Task.Result);
            if (!ReferenceEquals(intent.Owner, Robot.Engine.Robot) || result.OwnerRetired == true) return new("unknown", "robot owner retired");
            if (events.TryPeek(out var first) && first.Full.Cursor > intent.Cursor + 1) return new("unknown", "event cursor gap");
            if (result.Admitted == false) return new("not verified", "command was refused");
            if (result.EngineResult is > 0) return new("not verified", result.EngineResultName ?? "native action did not succeed");
            if (result.Status == "submitted" || result.Status == "no-report") return new("unknown", result.Detail ?? result.Status);
            if (response?["truncatedByRobot"]?.GetValue<bool>() == true) return new("not verified", "robot cut off playback");
            if (intent.Command == "animation" && response?["report"]?["playbackTag"]?.GetValue<byte>() is byte tag)
            {
                string? name = response["report"]?["animationName"]?.GetValue<string>();
                var playback = events.Select(e => e.Full).Where(e => e.Cursor > intent.Cursor &&
                    e.Type is ("animation-started" or "animation-ended" or "animation-aborted") &&
                    e.Payload.GetProperty("tag").GetByte() == tag && e.Payload.GetProperty("name").GetString() == name).ToArray();
                if (playback.Length >= 2 && playback[^1].Type == "animation-ended" && playback[^2].Type == "animation-started")
                    return new("verified", "robot reported both playback boundaries for the requested clip", "animation-ended",
                        (now - playback[^1].TimestampUtc).TotalMilliseconds);
                return Unverified("no matching start and end reports from the robot");
            }
            if (intent.Command is "turn-to-face" or "track-face" or "search-for-face")
            {
                var report = response?["report"];
                if (report?["facePoseAvailable"]?.GetValue<bool>() == false)
                    return new("not verified", "engine reported success; not verified because no target face pose was available");
                int? id = terminalFace ?? report?["faceId"]?.GetValue<int>();
                if (id is not null)
                {
                    // E2G RobotObservedFace (0x46) reports identity, exposure timestamp and world pose.
                    var last = events.Reverse().Select(e => e.Full).FirstOrDefault(e => e.Cursor > intent.Cursor &&
                        e.Type is ("face-seen" or "face-lost") && e.Payload.TryGetProperty("faceId", out var face) && face.GetInt32() == id);
                    if (last is { Type: "face-seen", RobotTimestamp: { } stamp } && stamp > intent.Initial?.Timestamp &&
                        last.Payload.TryGetProperty("pose", out var pose) && pose.GetProperty("originId").GetUInt32() == intent.Owner?.CurrentOriginId)
                    {
                        double age = Math.Max((now - last.TimestampUtc).TotalMilliseconds,
                            outcomeState?.Timestamp > stamp ? outcomeState.Timestamp - stamp : 0);
                        if (age >= 0 && age <= intent.Owner!.PoseHistory.WindowMs)
                            return new("verified", "target face observed in current origin", "face-seen", age);
                    }
                }
                return Unverified("no fresh observation of the target face");
            }
            if (outcomeState is not { } state || state.Timestamp <= (intent.Initial?.Timestamp ?? 0) || outcomeStateTime < intent.Started)
                return Unverified("no fresh postcondition observation");
            double ageMs = (now - outcomeStateTime).TotalMilliseconds;
            if (ageMs < 0 || ageMs > intent.Owner?.PoseHistory.WindowMs) return Unverified("robot state is stale");
            // F0 (M2-012) supplies contacts, carrying, pose and joint angles. Native joint tolerances:
            // CozmoMotion.GameHeadToleranceRad / GameLiftToleranceMm; SetTolerance 0x00545D50 for body turns.
            bool? matches = intent.Command switch
            {
                "mount-charger" or "go-home" => state.Has(RobotStatusFlag.IsOnCharger),
                "drive-off-charger" => !state.Has(RobotStatusFlag.IsOnCharger),
                "pickup" => state.Has(RobotStatusFlag.IsCarryingBlock) && Robot.Carrying.CarriedObjectId == intent.Args.UInt("objectId"),
                "put-down" => !state.Has(RobotStatusFlag.IsCarryingBlock),
                "head" => state.Has(RobotStatusFlag.HeadInPos) && Math.Abs(state.HeadAngle - intent.Args.Number("radians")) <= CozmoMotion.GameHeadToleranceRad,
                "lift" => state.Has(RobotStatusFlag.LiftInPos) && Math.Abs(state.LiftHeightMm - intent.Args.Number("heightMm")) <= CozmoMotion.GameLiftToleranceMm,
                "turn-in-place" or "go-to-pose" or "turn-toward-image-point" => PoseMatches(),
                _ => null,
            };
            return matches switch
            {
                true => new("verified", "fresh robot state matches the requested postcondition", "RobotState F0", ageMs),
                false => new("not verified", "fresh robot state does not confirm the requested postcondition", "RobotState F0", ageMs),
                _ => Unverified("no independent physical postcondition observed"),
            };
        }

        bool? PoseMatches()
        {
            if (intent.InitialPose is not { } before || poseAfter is not { } after ||
                after.OriginId != before.OriginId || after.PoseFrameId != before.PoseFrameId ||
                after.RobotTimestamp is not { } t || t <= before.RobotTimestamp || after.AgeMs > intent.Owner?.PoseHistory.WindowMs)
                return null;
            bool Near(double goal, double tolerance) => Math.Abs(Math.IEEERemainder(after.HeadingRad - goal, 2 * Math.PI)) <= tolerance;
            if (intent.Command == "turn-in-place")
                return Near(intent.Args.Number("angleRadians") + (intent.Args.Flag("absolute") ? 0 : before.HeadingRad),
                    Math.Max(Math.Abs(intent.Args.Number("toleranceRadians")), TurnInPlaceAction.MinToleranceRad));
            if (intent.Command == "turn-toward-image-point")
            {
                if (response?["report"]?["bodyRadians"]?.GetValue<float>() is not float body) return null;
                return Near(body, TurnInPlaceAction.MinToleranceRad) &&
                    (response["report"]?["headRadians"]?.GetValue<float>() is not float head ||
                     Math.Abs(outcomeState!.HeadAngle - head) <= CozmoMotion.GameHeadToleranceRad);
            }
            // Planar position/yaw observation of the goal; the native arrival check
            // uses the full pose in DriveToPoseAction.ReadyState, 0x0055AEC2..0x0055B0A6.
            const double distance = DriveToPoseAction.DefaultGoalDistanceToleranceMm;
            return Math.Sqrt(Math.Pow(after.XMm - intent.Args.Number("xMm"), 2) + Math.Pow(after.YMm - intent.Args.Number("yMm"), 2)) <= distance &&
                Near(intent.Args.Number("angleRadians"), DriveToPoseAction.GoalAngleToleranceRad);
        }

        Observation Unverified(string reason) => result.EngineResult == 0
            ? new("not verified", "engine reported success; not verified: " + reason) : new("unknown", reason);
    }
}
