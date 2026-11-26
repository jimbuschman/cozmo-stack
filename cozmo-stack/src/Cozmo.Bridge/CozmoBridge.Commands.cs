using System.Collections.Frozen;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using static Cozmo.Bridge.Argument;

namespace Cozmo.Bridge;

internal sealed record CommandResult(string Status, bool? Admitted, uint? EngineResult, string? EngineResultName,
    string? Detail = null, bool? OwnerRetired = null, object? Report = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public object? FaceOperation { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    internal uint? OwnedCompletionTag { get; init; }

    internal static CommandResult Action(bool admitted, uint? code, string detail, bool retired, object? report = null) =>
        new(!admitted ? "not-admitted" : retired ? "owner-retired" : "completed", admitted, code,
            code is { } result ? Enum.GetName((NativeActionResultName)result) : null, detail, retired, report);

    internal static CommandResult Posted() => new("submitted", null, null, null);
    internal static CommandResult Confirmed(object report) => new("confirmed", null, null, null, Report: report);
    internal static CommandResult NotReady() => new("not-admitted", false, null, null, "Robot is not ready");
    internal static CommandResult NoReport() => new("no-report", null, null, null, "The engine did not send a matching report before the timeout");
}

/// <summary>JSON commands and observations for one caller-owned Cozmo robot.</summary>
public sealed partial class CozmoBridge : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private static readonly Argument WorldObjectId = Integer("objectId", "Engine world object ID.", 0, int.MaxValue);
    private static readonly Argument FaceId = Integer("faceId", "Signed engine face ID.", int.MinValue, int.MaxValue);
    private static readonly Argument EnrolledFaceId = Integer("faceId", "Positive enrolled face ID.", 1, int.MaxValue);
    private static readonly Argument TurnLimit = Number("maximumTurnRadians", "Maximum turn angle in radians.");
    private static readonly Argument Angle = Number("angleRadians", "Angle in radians.");
    private static readonly Argument[] NoArguments = [];

    private static readonly BridgeCommand[] Commands =
    [
        new("head", "Set the head angle in radians. Success: completed with engineResult=0. Failure: isError, a nonzero engineResult, not-admitted or owner-retired; read detail, why and during.", [Number("radians", "Head angle in radians.")],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiHeadAngleAsync(a.Number("radians"), ct))),
        new("lift", "Set lift height in millimetres. Success: completed with engineResult=0. Failure: isError, a nonzero engineResult, not-admitted or owner-retired; read detail, why and during.", [Number("heightMm", "Lift height in millimetres.")],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiLiftHeightAsync(a.Number("heightMm"), ct))),
        // DriveStraight.cs defaults the trailing bool to true. The factory at 0x00529EF6–0x00529F1A forwards it.
        new("drive-straight", "Drive a signed relative distance in millimetres. Success: native engineResult=0. Failure: nonzero engineResult, not-admitted or owner-retired; read why and during before choosing another move.",
            [Number("distanceMm", "Signed distance in millimetres."), Number("speedMmps", "Speed in millimetres per second."),
             Boolean("shouldPlayAnimation", "Play the native driving animation. Set false for a quick drive without it.", true)],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiDriveStraightAsync(a.Number("distanceMm"), a.Number("speedMmps"), a.Flag("shouldPlayAnimation"), ct))),
        // TurnInPlace.cs packs four floats and isAbsolute. The factory at 0x0052AE74–0x0052AEBA consumes those five fields.
        new("turn-in-place", "Turn the body using radians. Success: native engineResult=0; report.relocalizationCount records relocalizations. Failure: nonzero engineResult, not-admitted or owner-retired; inspect why and during.",
            [Angle, Number("speedRadPerSec", "Speed in radians per second."),
             Number("accelRadPerSec2", "Acceleration in radians per second squared."),
             Number("toleranceRadians", "Angle tolerance in radians.", 0), Boolean("absolute", "Use an absolute heading.", false)],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiTurnInPlaceAsync(a.Number("angleRadians"), a.Number("speedRadPerSec"),
                a.Number("accelRadPerSec2"), a.Number("toleranceRadians"), a.Flag("absolute"), ct))),
        new("turn-to-face", "Turn toward a current signed face ID. Success: engineResult=0; inspect report.facePoseAvailable, faceId and requestedFaceId. report.endReason explains completion; callerCancellationRequested is a separate host observation. An unknown ID can return SUCCESS with facePoseAvailable=false and turn nowhere. Failure: nonzero engineResult, not-admitted or owner-retired; inspect detail, why and during.", [FaceId, TurnLimit],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiTurnToFaceAsync(a.Int("faceId"), a.Number("maximumTurnRadians"), ct))),
        new("turn-to-cube", "Turn toward an engine world object ID. Success: native engineResult=0. Failure: nonzero engineResult, not-admitted or owner-retired; inspect why and during and refresh the located object before retrying.", [WorldObjectId, TurnLimit],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiTurnToObjectAsync(a.UInt("objectId"), a.Number("maximumTurnRadians"), ct))),
        new("track-face", "Track the signed faceId; headOnly=true moves only the head. Success: submitted and report.actionTag mean admission, not tracking success. Read action-completed events for the native terminal result and signed final tracking ID. Use cancel-action with actionTag to stop; missing or stale observations do not end native tracking. Failure: not-admitted or owner-retired, or a nonzero terminal engineResult in the completion event.",
            [FaceId, Boolean("headOnly", "Reserve only the head track.", false)],
            (b, a, ct) => b.TrackFace(a.Int("faceId"), a.Flag("headOnly"), ct)),
        new("animation", "Play a shipped case-sensitive trigger. Success: native engineResult=0; report identifies animationName/playbackTag. Failure: isError, native failure or truncatedByRobot=true. A robot cutoff can retain SUCCESS; read playbackIssues, why and during before claiming full playback.",
            [Text("trigger", "Case-sensitive animation trigger name.", choices: Enum.GetNames<AnimationTrigger>()),
             Integer("loops", "Native animation loop count.", 0, uint.MaxValue, 1),
             Boolean("useSafeLiftMotion", "Use the native safe-lift animation action.", true),
             Boolean("ignoreBodyTrack", "Ignore the animation's body track.", false),
             Boolean("ignoreHeadTrack", "Ignore the animation's head track.", false),
             Boolean("ignoreLiftTrack", "Ignore the animation's lift track.", false)],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiAnimationAsync(Enum.Parse<AnimationTrigger>(a.Text("trigger")),
                a.UInt("loops"), a.Flag("useSafeLiftMotion"), a.Flag("ignoreBodyTrack"), a.Flag("ignoreHeadTrack"), a.Flag("ignoreLiftTrack"), ct))),
        new("say-text", "Say English text in Cozmo's voice and wait for the native action. Success: engineResult=0. Failure: nonzero engineResult, not-admitted, owner-retired or an unavailable audio path; inspect detail, why and during. Completion alone does not establish what was heard.", [Text("text", "English text to say.")],
            (b, a, ct) => b.SayText(a.Text("text"), ct)),
        new("pickup", "Pick up a cube by world object ID, optionally driving to it. Success: native engineResult=0; check fresh isCarrying/carriedObjectId. Failure: nonzero engineResult, not-admitted or owner-retired; inspect why and during, including reaction interruptions.", [WorldObjectId, Boolean("driveToObject", "Drive to the cube before pickup.", true)],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiPickupCubeAsync(a.UInt("objectId"), a.Flag("driveToObject"), ct))),
        new("put-down", "Place the carried object on the ground. Success: native engineResult=0; check fresh carrying state. Failure: nonzero engineResult or an error result; inspect why and during for the actual outcome.", NoArguments,
            (b, _, ct) => b.PutDown(ct)),
        new("roll", "Drive to a cube world ID and roll it. Success: native engineResult=0. Failure: nonzero engineResult, not-admitted or owner-retired; inspect why and during. Earlier approach checks can be superseded by later native steps.", [WorldObjectId],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiRollCubeAsync(a.UInt("objectId"), ct))),
        new("stack", "Stack the carried cube on the target world cube ID. Success: native engineResult=0; check fresh object/carrying state. Failure: nonzero engineResult, not-admitted or owner-retired; inspect why and during.", [WorldObjectId],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiStackOnCubeAsync(a.UInt("objectId"), ct))),
        new("flip", "Drive to a cube world ID and flip it. Success: native engineResult=0. Failure: nonzero engineResult, not-admitted or owner-retired; inspect why and during and refresh the cube observation.", [WorldObjectId],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiFlipCubeAsync(a.UInt("objectId"), ct))),
        new("wheelie", "Drive to a cube world ID and pop a wheelie. Success: native engineResult=0. Failure: nonzero engineResult, not-admitted or owner-retired; inspect why and during and refresh the cube observation.", [WorldObjectId],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiPopAWheelieAsync(a.UInt("objectId"), ct))),
        new("go-to-pose", "Drive to x/y millimetres and a heading in radians in the current world origin. Success: native engineResult=0. Failure: nonzero engineResult, not-admitted or owner-retired; inspect why and during. Supply an established world-origin target.",
            [Number("xMm", "World x in millimetres."), Number("yMm", "World y in millimetres."), Angle],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiGotoPoseAsync(a.Number("xMm"), a.Number("yMm"), a.Number("angleRadians"), ct))),
        new("mount-charger", "Drive to a located charger world ID and mount it. Line up square: the recorded final check used about 7.5 degrees yaw-error tolerance and 21 mm Y-offset tolerance; geometry-dependent live bounds appear in why when captured. Success: native engineResult=0; check fresh isOnCharger/isCharging. Failure: native failure, not-admitted or owner-retired; read why and during. See cozmo-guide for sources and axes.",
            [Integer("chargerId", "Engine world ID of the located charger.", 0, int.MaxValue)],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiMountChargerAsync(a.UInt("chargerId"), ct))),
        new("teach-face", "Enroll a face under a name. Success: faceOperation.outcome=enrolled with native engineResult=0, faceId and name. Failure: failed with the native reason, not-admitted or no-report; inspect detail, why and during. A failure can leave a partial named record. Fresh sightings arrive separately.",
            [Text("name", "Enrollment name, at most 255 UTF-8 bytes.", 255),
             Integer("saveId", "Album ID to update, or zero for enrollment.", 0, int.MaxValue, 0)],
            (b, a, ct) => b.TeachFace(a.Text("name"), a.Int("saveId"), ct)),
        new("enroll-face", "Alias of teach-face. Success: faceOperation.outcome=enrolled with engineResult=0, faceId and name. Failure: failed with the native reason, not-admitted or no-report; inspect detail, why and during. A failure can leave a partial named record. Fresh sightings arrive separately.",
            [Text("name", "Enrollment name, at most 255 UTF-8 bytes.", 255),
             Integer("saveId", "Album ID to update, or zero for enrollment.", 0, int.MaxValue, 0)],
            (b, a, ct) => b.TeachFace(a.Text("name"), a.Int("saveId"), ct)),
        new("rename-face", "Rename an enrolled ID using its old and new name. Success: confirmed with faceOperation.outcome=renamed and ID/name. Failure: not-admitted, no-report or an error; inspect why and during. Confirmation does not wait for NV persistence or spoken-response completion.",
            [EnrolledFaceId, Text("oldName", "Existing name, at most 255 UTF-8 bytes.", 255, allowEmpty: true),
             Text("newName", "New name, at most 255 UTF-8 bytes.", 255, allowEmpty: true)],
            (b, a, ct) => b.RenameFace(a.Int("faceId"), a.Text("oldName"), a.Text("newName"), ct)),
        new("erase-face", "Erase face enrollment by ID. Success: confirmed with faceOperation.outcome=erased and lastKnownName; the native report name is empty. Failure: not-admitted, no-report or an error; inspect why and during. Confirmation does not wait for NV persistence; continued sightings can register an unnamed guest using the same ID.", [EnrolledFaceId],
            (b, a, ct) => b.EraseFace(a.Int("faceId"), ct)),
        new("set_camera_color", "Set the robot's camera colour flag on its existing stream. Success: confirmed reports the requested setting; use the encoding and isColor on a subsequent get_camera_image to see which format the robot sent. Failure: not-admitted when the robot is unavailable.",
            [Boolean("enabled", "True requests colour frames, false requests greyscale.")],
            (b, a, ct) => b.SetCameraColor(a.Flag("enabled"), ct)),
        new("set-headlight", "Turn the infrared headlight beside the camera on or off (invisible to people; lights the scene for the camera in the dark). Success: confirmed reports the requested setting; the robot does not report the light's state. Failure: not-admitted when the robot is unavailable.",
            [Boolean("enabled", "True turns the headlight on, false turns it off.")],
            (b, a, ct) => b.SetHeadlight(a.Flag("enabled"), ct)),
        new("set-backpack-lights", "Set selected backpack LEDs. Other LEDs retain this client's last requested values. Success: submitted reports the pattern sent to the engine's light layer, where normal animation and charging priorities apply. Failure: invalid arguments or robot unavailable.",
            [Integer("ledMask", "Sum the selected LED bits: 1 left, 2 front, 4 middle, 8 back, 16 right.", 1, 31, 31), ..LightPatternArguments],
            (b, a, ct) => b.SetGameLights(a, ct, null)),
        new("set-cube-lights", "Set selected LEDs on a connected cube. Other LEDs retain this client's last requested values. Success: submitted reports the pattern queued for the robot. Freeplay may replace the output on its next update. Failure: invalid arguments, robot unavailable, or cube disconnected.",
            [WorldObjectId, Integer("ledMask", "Sum the selected cube LED bits: 1, 2, 4, 8 in wire order.", 1, 15, 15), ..LightPatternArguments],
            (b, a, ct) => b.SetGameLights(a, ct, a.UInt("objectId"))),
        new("lock-reactions", "Disable selected engine reaction triggers under a named lock. Success: confirmed shows the active locks. Failure: invalid arguments or not-admitted when the robot is unavailable.",
            [Text("id", "Lock ID to pass to unlock-reactions.", 255), Argument.ReactionSelection(Enum.GetNames<ReactionTrigger>())],
            (b, a, ct) => b.ChangeReactionLock(a.Text("id"), a.Value("triggers"), ct)),
        new("unlock-reactions", "Remove one named engine reaction lock. Success: confirmed shows the active locks. Failure: invalid arguments or not-admitted when the robot is unavailable.",
            [Text("id", "Lock ID to remove.", 255)], (b, a, ct) => b.ChangeReactionLock(a.Text("id"), null, ct)),
        new("cancel-action", "Cancel one of your current or queued engine actions by actionTag from get_state.activeActions. Success: submitted means the engine received the cancel. Read action-completed events or the original action result for native completion. Failure: not-admitted if the robot is unavailable or the action has ended or belongs to another caller.", [Integer("actionTag", "Your engine action tag.", 1, uint.MaxValue)],
            (b, a, ct) => b.CancelAction(a.UInt("actionTag"), ct)),
        new("request-behavior", "Ask the native Selection activity to run one shipped behavior by its case-sensitive BehaviorID, including VC_SearchForFace, VC_ComeHere, VC_HowAreYouDoing_* and Hiking_LookInPlace360. Success: submitted means the native request was received, not that the behavior ran; watch behavior-changed events; behavior-request-not-runnable reports the native readiness reason when blocked. Freeplay resumes after the native run count is spent if it was enabled. Failure: not-admitted for unbuilt, excluded or unavailable owners; native readiness and reactions may delay or preempt the run.",
            [Text("behavior", "Shipped behavior ID.", choices: Enum.GetNames<BehaviorID>().Where(IsRequestableBehavior).ToArray())],
            (b, a, ct) => b.RequestBehavior(a.Text("behavior"), null, ct)),
        new("request-spark", "Ask for a shipped Spark activity by its exact UnlockId name, or Count to cancel. Success: submitted means the native hard request was stored and Freeplay chooses the Spark normally. Failure: not-admitted for an unknown name, unavailable owner or active command bracket; engine gates still determine execution.",
            [Text("spark", "Shipped Spark UnlockId, e.g. RollCube or Workout; Count cancels.", 80)],
            (b, a, ct) => b.RequestSpark(a.Text("spark"), ct)),
        new("play-arbitrary-anim", "Supply one shipped animation trigger to the configured PlayArbitraryAnim behavior and request one native run. Success: submitted means the Selection request was received; observe behavior and action events. Failure: not-admitted when the retained owner or resources are unavailable or the behavior already runs.",
            [Text("trigger", "Case-sensitive animation trigger.", choices: Enum.GetNames<AnimationTrigger>())],
            (b, a, ct) => b.RequestBehavior("PlayArbitraryAnim", Enum.Parse<AnimationTrigger>(a.Text("trigger")), ct)),
        new("freeplay", "Enable or disable Freeplay. Success: submitted means the app activity input was handled. The reply includes the requested setting and the observed Freeplay state. Failure: not-admitted when the robot is not ready, or an error; inspect why. There is no native action result for this setting.", [Boolean("enabled", "True enables Freeplay.")],
            (b, a, ct) => b.SetFreeplay(a.Flag("enabled"), ct)),
        new("wake", "Wake Cozmo from the going-to-sleep state on the charger, as the app's sleep-dialog Cancel button did: cancels the idle timeout, removes the pause lock and resumes the requested Freeplay setting. With Freeplay enabled, Cozmo can then drive off the charger. Success: submitted, confirmed later by the sleep-cancelled event. Failure: no-sleep-pending when no going-to-sleep notice is pending, or not-admitted when the robot is not ready.", [],
            (b, a, ct) => b.Wake(ct)),
        new("set_needs_mode", "Choose full needs (refill/pause) or real native needs. Success: confirmed returns keepFull/loaded/applied/paused and levels; applied shows whether the input reached loaded needs. Failure: an error result; inspect why. This setting has no native action result.",
            [Text("mode", "Needs mode.", choices: ["full", "real"])],
            (b, a, ct) => b.SetNeedsMode(a.Text("mode"), ct)),
        new("set_needs", "Set repair/energy/play through the engine force-levels message, after selecting real mode. Success: confirmed returns native clamped levels and needs status. Failure: not-admitted or an error when full mode is selected or stored needs are not loaded; inspect detail and why. There is no native action result.",
            [new("repair", "number", "Repair level, natively clamped.", 0, 1),
             new("energy", "number", "Energy level, natively clamped.", 0, 1),
             new("play", "number", "Play level, natively clamped.", 0, 1)],
            (b, a, ct) => b.SetNeeds(a.Number("repair"), a.Number("energy"), a.Number("play"), ct)),
    ];

    private static readonly BridgeCommand[] PublishedCommands = Commands.Select(AddAdmissionWaitParameter)
        .Select(command => command with
        {
            Parameters = [..command.Parameters, Text("duringDetail", "Compact sightings or every available event in full.",
                choices: ["compact", "full"]) with { Default = JsonSerializer.SerializeToElement("compact") }],
        }).ToArray();
    private static readonly FrozenDictionary<string, BridgeCommand> ByName =
        PublishedCommands.ToFrozenDictionary(c => c.Name, StringComparer.Ordinal);
    private static readonly JsonElement Catalogue = JsonSerializer.SerializeToElement(PublishedCommands.Select(c => c.Describe()), Json);

    /// <summary>The supported command names and the JSON Schema for each arguments object.</summary>
    public JsonElement GetCommandCatalogue() => Catalogue;

    /// <summary>Invokes an existing Cozmo command and returns its result as JSON.</summary>
    public async Task<JsonElement> ExecuteAsync(string command, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (command is null || !ByName.TryGetValue(command, out var entry))
            return Error("unknown-command", $"Unknown command '{command}'.");
        BridgeArguments parsed;
        try { parsed = entry.Parse(arguments); }
        catch (ArgumentException error) { return Error("invalid-arguments", error.Message); }
        catch (InvalidOperationException error) when (error is not ObjectDisposedException)
        {
            return Error("invalid-arguments", error.Message);
        }

        long callStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        long startCursor = CurrentEventCursor();
        object? previousCaller = ActionRunner.CallerContext.Value;
        ActionRunner.CallerContext.Value = eventCaller;
        try
        {
            using var capture = new ActionWhyCapture(this);
            var (result, admissionWait) = await ExecuteWithAdmissionWait(entry, parsed, capture, cancellationToken).ConfigureAwait(false);
            if (command is "roll" or "turn-to-cube" && result.OwnedCompletionTag is { } interactionTag)
                await WaitForOwnedCompletion(startCursor, interactionTag).ConfigureAwait(false);
            var response = System.Text.Json.JsonSerializer.SerializeToNode(result, Json)!.AsObject();
            if (admissionWait is not null) response["admissionWait"] = JsonSerializer.SerializeToNode(admissionWait, Json);
            if (command is "freeplay" or "turn-in-place" or "animation")
                response["freeplay"] = JsonSerializer.SerializeToNode(Robot.GetFreeplayObservation(), Json);
            if (command == "freeplay") response["requestedEnabled"] = parsed.Flag("enabled");
            if (command == "animation")
            {
                var details = JsonSerializer.SerializeToElement(result.Report, Json);
                byte? tag = details.ValueKind == JsonValueKind.Object && details.TryGetProperty("playbackTag", out var playbackTag) &&
                    playbackTag.ValueKind == JsonValueKind.Number ? playbackTag.GetByte() : null;
                string? name = details.ValueKind == JsonValueKind.Object && details.TryGetProperty("animationName", out var clip) ? clip.GetString() : null;
                var issues = tag is null ? [] : playbackMonitor?.IssuesSince(callStartedAt, tag, name) ?? [];
                response["truncatedByRobot"] = issues.Length != 0;
                response["playbackIssues"] = JsonSerializer.SerializeToNode(issues, Json);
            }
            if (result.Admitted == false || result.EngineResult is > 0 || result.Status is "owner-retired" or "no-report" ||
                response["truncatedByRobot"]?.GetValue<bool>() == true)
                response["why"] = capture.Describe(result, response["truncatedByRobot"]?.GetValue<bool>() == true);
            AttachDuring(response, startCursor, parsed.Text("duringDetail") == "full", result.OwnedCompletionTag);
            return JsonSerializer.SerializeToElement(response, Json);
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("MISSING:", StringComparison.Ordinal))
        {
            return Error("unavailable", error.Message);
        }
        finally { ActionRunner.CallerContext.Value = previousCaller; }
    }

    private static JsonElement Error(string code, string message) =>
        JsonSerializer.SerializeToElement(new { error = new { code, message } }, Json);

    private Task<CommandResult> SetNeedsMode(string mode, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(CommandResult.Confirmed(Robot.Engine.SetNeedsMode(mode == "full")));
    }

    private Task<CommandResult> SetNeeds(float repair, float energy, float play, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { return Task.FromResult(CommandResult.Confirmed(Robot.Engine.SetNeedsLevels(repair, energy, play))); }
        catch (InvalidOperationException error)
        { return Task.FromResult(new CommandResult("not-admitted", false, null, null, error.Message)); }
    }

    private static async Task<CommandResult> FromAction<T>(Task<T> task)
    {
        return (await task.ConfigureAwait(false)) switch
        {
            AiHeadActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired),
            AiLiftActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired),
            AiDriveActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired),
            AiTurnActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired,
                new { r.RelocalizationCount }),
            AiTurnToFaceActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired,
                new { r.FacePoseAvailable, r.FaceId, r.RequestedFaceId, r.EndReason, r.CallerCancellationRequested }),
            AiTurnToObjectActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired)
                with { OwnedCompletionTag = r.PublishedCompletionTag },
            AiTrackFaceActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired, new { r.FaceId, r.RequestedFaceId, r.EndReason, r.CallerCancellationRequested }),
            AiAnimationActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired, new { r.AnimationName, r.PlaybackTag }),
            AiPickupActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired)
                with { OwnedCompletionTag = r.PublishedCompletionTag },
            _ => throw new InvalidOperationException("Unexpected result from a Cozmo command."),
        };
    }
}
