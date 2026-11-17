using System.Collections.Frozen;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using static Cozmo.Bridge.Argument;

namespace Cozmo.Bridge;

internal sealed record CommandResult(string Status, bool? Admitted, uint? EngineResult, string? EngineResultName,
    string? Detail = null, bool? OwnerRetired = null, object? Report = null)
{
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
        new("head", "Set the head angle.", [Number("radians", "Head angle in radians.")],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiHeadAngleAsync(a.Number("radians"), ct))),
        new("lift", "Set the lift height.", [Number("heightMm", "Lift height in millimetres.")],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiLiftHeightAsync(a.Number("heightMm"), ct))),
        new("drive-straight", "Drive by a signed relative distance.",
            [Number("distanceMm", "Signed distance in millimetres."), Number("speedMmps", "Speed in millimetres per second."),
             Boolean("shouldPlayAnimation", "Use the native driving animation.", true)],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiDriveStraightAsync(a.Number("distanceMm"), a.Number("speedMmps"), a.Flag("shouldPlayAnimation"), ct))),
        new("turn-in-place", "Turn the body in place.",
            [Angle, Number("speedRadPerSec", "Speed in radians per second."),
             Number("accelRadPerSec2", "Acceleration in radians per second squared."),
             Number("toleranceRadians", "Angle tolerance in radians.", 0), Boolean("absolute", "Use an absolute heading.", false)],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiTurnInPlaceAsync(a.Number("angleRadians"), a.Number("speedRadPerSec"),
                a.Number("accelRadPerSec2"), a.Number("toleranceRadians"), a.Flag("absolute"), ct))),
        new("turn-to-face", "Turn toward a tracked face.", [FaceId, TurnLimit],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiTurnToFaceAsync(a.Int("faceId"), a.Number("maximumTurnRadians"), ct))),
        new("turn-to-cube", "Turn toward a located light cube.", [WorldObjectId, TurnLimit],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiTurnToObjectAsync(a.UInt("objectId"), a.Number("maximumTurnRadians"), ct))),
        new("track-face", "Track a face until the engine ends tracking or the caller cancels.",
            [FaceId, Boolean("headOnly", "Reserve only the head track.", false)],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiTrackFaceAsync(a.Int("faceId"), a.Flag("headOnly"), ct))),
        new("animation", "Play an authored animation by its shipped trigger name.",
            [Text("trigger", "Case-sensitive animation trigger name.", choices: Enum.GetNames<AnimationTrigger>()),
             Integer("loops", "Native animation loop count.", 0, uint.MaxValue, 1),
             Boolean("useSafeLiftMotion", "Use the native safe-lift animation action.", true),
             Boolean("ignoreBodyTrack", "Ignore the animation's body track.", false),
             Boolean("ignoreHeadTrack", "Ignore the animation's head track.", false),
             Boolean("ignoreLiftTrack", "Ignore the animation's lift track.", false)],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiAnimationAsync(Enum.Parse<AnimationTrigger>(a.Text("trigger")),
                a.UInt("loops"), a.Flag("useSafeLiftMotion"), a.Flag("ignoreBodyTrack"), a.Flag("ignoreHeadTrack"), a.Flag("ignoreLiftTrack"), ct))),
        new("say-text", "Say English text in Cozmo's voice.", [Text("text", "English text to say.")],
            (b, a, ct) => FromAction(b.Robot.SayEnglishSentenceAsync(a.Text("text"), ct))),
        new("pickup", "Pick up a cube.", [WorldObjectId, Boolean("driveToObject", "Drive to the cube before pickup.", true)],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiPickupCubeAsync(a.UInt("objectId"), a.Flag("driveToObject"), ct))),
        new("put-down", "Put the carried object down on the ground.", NoArguments,
            (b, _, ct) => FromAction(b.Robot.PlaceObjectOnGroundAsync(ct))),
        new("roll", "Drive to a cube and roll it.", [WorldObjectId],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiRollCubeAsync(a.UInt("objectId"), ct))),
        new("stack", "Stack the carried cube on the target cube.", [WorldObjectId],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiStackOnCubeAsync(a.UInt("objectId"), ct))),
        new("flip", "Drive to a cube and flip it.", [WorldObjectId],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiFlipCubeAsync(a.UInt("objectId"), ct))),
        new("wheelie", "Drive to a cube and pop a wheelie.", [WorldObjectId],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiPopAWheelieAsync(a.UInt("objectId"), ct))),
        new("go-to-pose", "Drive to a pose in the current world origin.",
            [Number("xMm", "World x in millimetres."), Number("yMm", "World y in millimetres."), Angle],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiGotoPoseAsync(a.Number("xMm"), a.Number("yMm"), a.Number("angleRadians"), ct))),
        new("mount-charger", "Drive to the located charger and mount it.",
            [Integer("chargerId", "Engine world ID of the located charger.", 0, int.MaxValue)],
            (b, a, ct) => FromAction(b.Robot.Motion.QueueAiMountChargerAsync(a.UInt("chargerId"), ct))),
        new("teach-face", "Select Meet and enroll a face under a name.",
            [Text("name", "Enrollment name, at most 255 UTF-8 bytes.", 255),
             Integer("saveId", "Album ID to update, or zero for enrollment.", 0, int.MaxValue, 0)],
            (b, a, ct) => b.TeachFace(a.Text("name"), a.Int("saveId"), ct)),
        new("rename-face", "Rename an enrolled face.",
            [EnrolledFaceId, Text("oldName", "Existing name, at most 255 UTF-8 bytes.", 255, allowEmpty: true),
             Text("newName", "New name, at most 255 UTF-8 bytes.", 255, allowEmpty: true)],
            (b, a, ct) => b.RenameFace(a.Int("faceId"), a.Text("oldName"), a.Text("newName"), ct)),
        new("erase-face", "Erase an enrolled face.", [EnrolledFaceId],
            (b, a, ct) => b.EraseFace(a.Int("faceId"), ct)),
        new("freeplay", "Enable or disable Freeplay.", [Boolean("enabled", "True enables Freeplay.")],
            (b, a, ct) => b.SetFreeplay(a.Flag("enabled"), ct)),
        new("set_needs_mode", "Choose full needs (refill and pause) or real native decay and action changes.",
            [Text("mode", "Needs mode.", choices: ["full", "real"])],
            (b, a, ct) => b.SetNeedsMode(a.Text("mode"), ct)),
        new("set_needs", "Set repair, energy and play through the engine's force-levels message. Select real mode first.",
            [new("repair", "number", "Repair level, natively clamped.", 0, 1),
             new("energy", "number", "Energy level, natively clamped.", 0, 1),
             new("play", "number", "Play level, natively clamped.", 0, 1)],
            (b, a, ct) => b.SetNeeds(a.Number("repair"), a.Number("energy"), a.Number("play"), ct)),
    ];

    private static readonly FrozenDictionary<string, BridgeCommand> ByName =
        Commands.ToFrozenDictionary(c => c.Name, StringComparer.Ordinal);
    private static readonly JsonElement Catalogue = JsonSerializer.SerializeToElement(Commands.Select(c => c.Describe()), Json);

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

        try
        {
            var result = await entry.Execute(this, parsed, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.SerializeToElement(result, Json);
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("MISSING:", StringComparison.Ordinal))
        {
            return Error("unavailable", error.Message);
        }
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
            AiTurnToFaceActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired),
            AiTurnToObjectActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired),
            AiTrackFaceActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired, new { r.FaceId }),
            AiAnimationActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired, new { r.AnimationName }),
            AiSpeechActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired),
            AiPickupActionResult r => CommandResult.Action(r.Admitted, r.EngineResult, r.Detail, r.OwnerRetired),
            Cozmo.Robot.Manipulation.ActionResult r => new("completed", null, (uint)r, Enum.GetName((NativeActionResultName)r)),
            _ => throw new InvalidOperationException("Unexpected result from a Cozmo command."),
        };
    }
}
