using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Conformance;

internal static class PersonalityHw
{
    internal const string TestId = "PERSONALITY-HW-1";
    internal const int IdleMinutes = 15;
    private const string Setup = "Start Cozmo off his charger on a clear, well-lit surface.";

    internal static readonly string[] RecordIds =
    {
        "M7-001", "M7-019", "M7-020", "M8-012", "M10-001", "M10-002", "M10-003", "M10-004", "M10-007",
        "M10-008", "M10-011", "M4-012", "M12-029", "M13-040",
        "M4-009", "M4-023", "M11-085", "M13-038", "M14-004", "M14-008", "M15-014", "M15-031", "M15-048",
        "M15-058", "M15-080", "M15-082", "M15-091", "M15-104", "M15-108"
    };

    internal const string BehaviorReportSource = "M8-012, E2G 0x87 at 0x005A22B4..0x005A24AC, payload 0x0071B806..0x0071B884";
    internal const string ReactionReportSource = "M8-012, E2G 0x88 at 0x005A20AA..0x005A20D6 and 0x005A2EE8";
    internal static bool TryRequestSearchForFace(CozmoRobot robot)
    {
        // 0x005C9170 gates on HasAnyFaces(0,false); Selection admits G2E 0x94 at 0x0060AA58.
        if (robot.Vision.Faces.HasAnyFaces(0, false)) return false;
        robot.SetEnableFreeplayActivity(false);
        robot.ExecuteBehaviorById((byte)BehaviorID.VC_SearchForFace, 1);
        return true;
    }
    internal static bool TryRequestDriveToFace(CozmoRobot robot)
    {
        using var owner=robot.Engine.EnterSynchronousOwner();
        var runtime=robot.Engine.Robot?.NativeBehaviorRuntime;
        if(runtime is null || !runtime.Bound.TryGetValue("VC_ComeHere",out var behavior) ||
            !behavior.IsRunnable(runtime.Context)) return false;
        robot.SetEnableFreeplayActivity(false);
        robot.ExecuteBehaviorById((byte)BehaviorID.VC_ComeHere,1);
        return true;
    }
    internal readonly record struct NeedsLevels(float Repair, float Energy, float Play)
    {
        internal string Describe() => FormattableString.Invariant(
            $"Force Repair={Repair}, Energy={Energy}, Play={Play} via G2E 0xC6 (M15-048, 0x00697E78..0x00697EC6), then restore the saved levels.");

        internal void WithOverride(CozmoEngine engine, Action observe, Action<bool, float[]>? record = null)
        {
            float[]? saved = null;
            bool? savedFullMode = null;
            try
            {
                using (engine.EnterSynchronousOwner())
                {
                    var needs = engine.Needs;
                    if (needs.AwaitingRobotData)
                        throw new InvalidOperationException("Cozmo's stored needs are still loading. No test levels were applied.");
                    var snapshot = new[] { (float)needs.State.GetNeedLevel(NeedId.Repair),
                        (float)needs.State.GetNeedLevel(NeedId.Energy), (float)needs.State.GetNeedLevel(NeedId.Play) };
                    if (snapshot.Any(level => !float.IsFinite(level) || level < (float)needs.Config.MinimumNeedLevel
                        || level > (float)needs.Config.MaximumNeedLevel))
                        throw new InvalidOperationException("Cozmo's current needs are outside the range ForceSetNeedsLevels can restore.");
                    saved = snapshot;
                    savedFullMode = engine.GetNeedsStatus().KeepFull;
                    engine.SetNeedsMode(false);
                    var target = new[] { Repair, Energy, Play };
                    engine.AnimationMessages.Deliver(0xC6, false, target);
                    record?.Invoke(false, target);
                }
                observe();
            }
            finally
            {
                if (saved is not null)
                {
                    using var owner = engine.EnterSynchronousOwner();
                    engine.AnimationMessages.Deliver(0xC6, false, saved);
                    if (savedFullMode == true) engine.SetNeedsMode(true);
                    record?.Invoke(true, saved);
                }
            }
        }
    }

    internal sealed record BehaviorCheck(BehaviorID Id, string Source, bool Optional = false, string? Missing = null,
        int WindowSeconds = 45, ReactionTrigger? Trigger = null)
    {
        internal string ReportSource => Trigger is null ? BehaviorReportSource : ReactionReportSource;
        internal string Name => Trigger?.ToString() ?? Id.ToString();
        internal bool Starts(JsonObject report) => Trigger is { } trigger
            ? IsReactionTransition(report, trigger, entering: true) : IsBehaviorStart(report, Id);
        internal bool Stops(JsonObject report) => Trigger is { } trigger
            ? IsReactionTransition(report, trigger, entering: false) : IsBehaviorStop(report, Id);
    }
    internal sealed record Step(string Id, string Instruction, string Question, BehaviorCheck? Check = null, NeedsLevels? Needs = null,
        string? Signal = null);
    internal const string PhysicalReactionSource = "config/engine/behaviorSystem/reactionTrigger_behavior_map.json; BehaviorTransition E2G 0x87 (0x005A22B4..0x005A24AC)";
    internal static string SignalSource(string signal)=>signal=="cube-tap"?
        "M4-023 BlockTapFilterComponent ObjectTapped broadcasts 0x00630C4C..0x00630C72 / 0x00631178..0x00631192":
        "M4-009 ObjectMoved E2G 0x0F 0x00534190..0x005341B0";

    internal static readonly Step[] Steps =
    {
        new("pickup", "Gently pick Cozmo up, keep him upright and hold him still. Watch the reaction, then return him to his treads.", "Did you see or hear a pickup reaction?",
            new(BehaviorID.ReactToPickup,PhysicalReactionSource)),
        new("putdown", "If Cozmo is already on his treads, gently pick him up and hold him upright briefly. Set him back down on a level surface. The engine normally waits quietly; it calibrates the head only when the pitch reads above 10 degrees.",
            "Did you observe any movement or sound when he was set down?",
            new(BehaviorID.ReactToReturnedToTreads,"ReturnedToTreads admits RobotOffTreadsStateChanged=OnTreads when trigger 14 is enabled (0x0060DE36..0x0060DE58). Init queues WaitAction(robot,0.5f) (0x00608500..0x00608528), then only |pitch degrees| > 10 queues CalibrateMotorAction(robot,true,false) (0x006085C4..0x0060868E). No animation or sound is queued.")),
        new("on-back", "Gently place Cozmo on his back. Watch his response, then return him to his treads.", "Did you see or hear a response to being on his back?",
            new(BehaviorID.ReactToRobotOnBack,PhysicalReactionSource)),
        new("on-side", "Gently place Cozmo on one side. Watch his response, then return him to his treads.", "Did you see or hear a response to being on his side?",
            new(BehaviorID.ReactToRobotOnSide,PhysicalReactionSource)),
        new("on-face", "Support Cozmo facing down. Watch his response, then return him to his treads.", "Did you see or hear a response to being face down?",
            new(BehaviorID.ReactToRobotOnFace,PhysicalReactionSource)),
        new("shaken", "Pick up Cozmo, give him a brief gentle shake, then set him back on his treads.", "Did you see or hear a distinct response to the shake?",
            new(BehaviorID.ReactToRobotShaken,PhysicalReactionSource)),
        new("cliff", "Let Cozmo approach a table edge under close hand supervision. Catch him if needed and then move him back to safety.", "Did you see or hear a reaction to the edge?",
            new(BehaviorID.ReactToCliff,PhysicalReactionSource)),
        new("acknowledge-face",
            "Keep your face out of view until the step begins. Then show an enrolled face he has yet to react to this session. For a face already seen at a distance, approach from beyond 40 cm to within 30 cm when he is free to react.",
            "Did you see or hear Cozmo acknowledge the face?",
            new(BehaviorID.AcknowledgeFace, "FacePositionUpdated names a new enrolled face or admits a newly close face after the 4 s cooldown (0x0060CB84..0x0060CE20). The strategy passes the desired set off the charger (0x0060CAB0..0x0060CB5E), and IsRunnableInternal requires that set (0x006028BC..0x006028C6). reactionTrigger_behavior_map.json selects AcknowledgeFace.")),
        new("interact-with-faces",
            "Show one face after the step begins. Keep it in view in good light while Cozmo is on his treads, off his charger and Freeplay enters Socialize.",
            "Did Cozmo turn to or track the face?",
            new(BehaviorID.InteractWithFaces, "IsRunnableInternal selects a recent face and requires a valid target (0x005C23AC..0x005C23EE). Socialize checks this behavior in phase 0 and selects it when runnable (0x005B02F6..0x005B0302, branch to 0x005B0354).")),
        new("search-for-face",
            "Leave the camera view and allow the FaceWorld to empty. An enrolled face may remain in the FaceWorld. When Cozmo begins searching, show your face again so he can play the found-face response.",
            "Did Cozmo search for a face?",
            new(BehaviorID.VC_SearchForFace, "voiceCommands/VC_SearchForFace.json maps the requested ID to SearchForFace. IsRunnableInternal requires !FaceWorld::HasAnyFaces(0,false) (0x005C9170..0x005C9180). Selection handles ExecuteBehaviorByID (G2E 0x94, 0x0060AA58..0x0060AB7A).")),
        new("drive-to-face",
            "Keep one face in view farther than 20 cm from Cozmo on a clear level surface.",
            "Did Cozmo approach the face?",
            new(BehaviorID.VC_ComeHere, "voiceCommands/VC_ComeHere.json maps the requested ID to DriveToFace. Readiness needs a valid most recently observed face in the current origin (0x005DA588..0x005DA626). After verification, the drive branch uses the 200 mm standoff (0x005DACDC, 0x005DA950). Selection handles G2E 0x94 (0x0060AA58..0x0060AB7A).",
                WindowSeconds: 45)),
        new("react-to-pet",
            "If a pet is available, keep its face in Cozmo's view for several camera frames. Start off the charger after at least 60 seconds without a pet reaction.",
            "Did you see or hear Cozmo react to the pet?",
            new(BehaviorID.ReactToPet, "PetInitialDetection requires three observations of an unreacted pet, off the charger, and a 60 s cooldown (0x00611AE4..0x00611CB6, 0x00611DD0). ReactToPet requires a target (0x00606ED8..0x00606EEC). reactionTrigger_behavior_map.json selects ReactToPet.", Optional: true)),
        new("face", "Show your face in good light. Use a previously enrolled face if available and listen for recognition by name.", "Did Cozmo acknowledge your face, by name if enrolled?",
            new(BehaviorID.AcknowledgeFace,PhysicalReactionSource)),
        new("cube-tap", "Put the light cube upright with a marker facing Cozmo. Give him time to see it, then tap the cube.", "Did you see or hear a response to the tap?",Signal:"cube-tap"),
        new("cube-move", "Move the cube to a different spot within Cozmo's view, keeping it upright.", "Did you see or hear a response to the cube moving?",Signal:"cube-move"),
        new("express-needs",
            "Keep Cozmo on his treads, off the charger and in Freeplay. Give him room to react.",
            "Did you see or hear a low-energy request?",
            new(BehaviorID.Needs_MildLowEnergyRequest, "Energy 0.4 is in Warning (0.21 to below 0.60, needs_config.json). needs_MildLowEnergyRequest.json selects Warning with a level-dependent cooldown. IsRunnableInternal checks both (0x005EE570..0x005EE5DC). The Freeplay activity interlude lists the request in socialize.json and hiking.json.",
                WindowSeconds: 75),
            new(1f, .4f, 1f)),
        new("needs-change-animation",
            "Keep Cozmo on his treads and off the charger. Allow the severe energy introduction to play.",
            "Did you see the severe low-energy introduction?",
            new(BehaviorID.Needs_SevereLowEnergyGetIn, "Energy 0.1 is in Critical (below 0.21, needs_config.json). needsSevereLowEnergy.json admits Critical Energy through InNeedsBracket (0x006141A0) and selects the GetIn before the severe state. GetIn requires Critical and Energy not yet selected as the expressed need (0x005BFECA..0x005BFEF6)."),
            new(1f, .1f, 1f)),
        new("drive-in-desperation",
            "Give Cozmo a clear, open area where he can drive. Keep him off the charger and without a held cube. Allow the severe energy introduction to finish.",
            "Did you see Cozmo start his low-energy drive?",
            new(BehaviorID.Needs_SevereLowEnergyState, "Energy 0.1 is in Critical (below 0.21, needs_config.json). The chooser in needsSevereLowEnergy.json tries the GetIn before this DriveInDesperation instance. GetIn Stop marks Energy as expressed (0x005BFEF8), and DriveInDesperation's own gate returns true (0x005D90AC..0x005D90AE)."),
            new(1f, .1f, 1f)),
        new("earned-sparks",
            "Run an activity that ordinarily earns a Freeplay Sparks reward, then watch for the reward behavior.",
            "Did you see the earned-Sparks animation?",
            new(BehaviorID.EarnedSparks, "Levels are unchanged. EarnedSparks reads NeedsManager's pending reward byte at +0x3D8 (0x005DAEC2..0x005DAECA). ForceSetNeedsLevels only stores levels and sends NoAction (0x00697E78..0x00697EC6). A genuine reward can make EarnedSparks runnable in the Freeplay interlude (socialize.json, hiking.json).")),
        new("react-to-impact",
            "Keep Cozmo supported while gently lifting him upright and setting him back on his treads. Use this window if the firmware has been reporting falls during normal handling.",
            "Did you observe the impact reaction?",
            new(BehaviorID.ReactToImpact, "The firmware's FallingStarted report is forwarded at 0x00534F3C. The enabled strategy latches it for 3000 ms (0x0060D838, 0x0060DD6E); the reaction waits for treads and empty lift (0x005BD864..0x005BD874, 0x0102BEB0..0x0102BEB8, 0x00606482). FallingStopped intensity above 1000 enables the later impact animation (0x0060642C..0x0060644E, 0x00606348). A bump alone does not supply FallingStarted.",
                Optional: true, Trigger: ReactionTrigger.RobotFalling)),
        new("react-to-placed-on-slope",
            "Gently lift Cozmo, then promptly place him at 15 to 25 degrees nose-up on a stable, grippy incline. Keep him steady on his treads with room to react.",
            "Did you observe the slope reaction?",
            new(BehaviorID.ReactToPlacedOnSlope, "Pitch must be above 10 and below 55 degrees, with all gyro axes at or below 0.01 rad/s for more than 0.4 s. The strategy requires pickup or less than 1.5 s since pickup, and off-treads state below OnBack (0x006144FC..0x00614688, strategy at 0x00612EB0..0x00612ECC).",
                Trigger: ReactionTrigger.RobotPlacedOnSlope)),
        new("react-to-unexpected-movement",
            "On a clear level surface, gently keep Cozmo facing the same direction during the script's in-place turn. Leave both treads free to turn. Release him when the reaction starts or the turn ends.",
            "Did you observe the unexpected-movement reaction?",
            new(BehaviorID.ReactToUnexpectedMovement, "For a held turn the physical detector reads opposite wheel signs, sum of absolute wheel speeds at least 20 mm/s and absolute gyro Z below 0.174533 rad/s. It fires after the count exceeds 10 (0x0063E398..0x0063E5DA; event at 0x0063E932, strategy 0x0060D96A).",
                Optional: true, Trigger: ReactionTrigger.UnexpectedMovement)),
        new("react-to-motor-calibration",
            "The offline replay supplies the firmware's automatic calibration start.",
            "Did you observe the motor calibration reaction?",
            new(BehaviorID.ReactToMotorCalibration, "The R2E 0xD1 filter requires CalibStarted and AutoStarted (0x0060D618, 0x0060DCFA..0x0060DD16).",
                Missing: "StartMotorCalibration 0x58 requests head/lift calibration (M4-012). No supported request is known to produce the firmware's AutoStarted flag.",
                Trigger: ReactionTrigger.MotorCalibration)),
        new("react-to-on-charger",
            "Keep Cozmo off his charger for the first 22 seconds of this step, then place him on the charger.",
            "Did you observe the on-charger reaction?",
            new(BehaviorID.ReactToOnCharger, "Charger contacts rising broadcasts ChargerEvent 0x39 with OnCharger true (0x00511BF4..0x00511C0A). The first strategy evaluation sets a 20 s deadline, and each evaluation consumes the latch even before that deadline (0x00614474..0x006144D0, 0x006120D0..0x006120E8).",
                Trigger: ReactionTrigger.PlacedOnCharger)),
        new("react-to-frustration",
            "Keep Cozmo off his charger and give him room to react. The script queues up to 12 drive-to-object actions for an absent object, 30 seconds apart, until the reaction starts.",
            "Did you observe the frustration reaction?",
            new(BehaviorID.ReactToFrustrationMinor, "DriveToObject aborts before moving when the target is absent (0x00559670..0x00559676, 0x00559798..0x005597AC). Its ABORT maps to a Confident change of -0.2 (mood_config.json, emotionevents/action_events.json; 0x0067B318). The script spaces attempts at the 30 s full-weight point of the shipped default repetition graph. The strategy checks Confident below -0.6 with a 60 s cooldown for minor, or below -0.9 for major (0x0060EE6E..0x0060EED8; reactionTrigger_behavior_map.json). The engine uses no fixed failure count.",
                WindowSeconds: 375, Trigger: ReactionTrigger.Frustration)),
        new("alone", $"Place Cozmo in the clear enclosed area, move yourself and the cube out of view, and leave him alone for {IdleMinutes} minutes.", "Did you observe Cozmo do anything on his own while left alone?")
    };

    internal static IReadOnlyList<Step> SelectSteps(string? list)
    {
        if (list is null) return Steps;
        var requested = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = Steps.Select(s => s.Id).ToArray();
        var unknown = requested.Except(ids, StringComparer.OrdinalIgnoreCase).ToArray();
        if (unknown.Length > 0 || requested.Count == 0)
            throw new ArgumentException($"--steps takes a comma list of: {string.Join(",", ids)}"
                + (unknown.Length > 0 ? $" (unknown: {string.Join(",", unknown)})" : ""));
        return Steps.Where(s => requested.Contains(s.Id)).ToArray();
    }

    internal static string SetupFor(IReadOnlyList<Step> steps)
    {
        var setup = Setup;
        if (steps.Any(s => s.Id is "cube-tap" or "cube-move")) setup += "\nHave one working light cube ready.";
        if (steps.Any(s => s.Id == "cliff")) setup += "\nStay close enough to catch him at the edge.";
        if (steps.Any(s => s.Id == "alone")) setup += "\nUse a clear, flat floor or enclosed area for the 15-minute round.";
        return setup;
    }

    internal static void PrintPlan(IReadOnlyList<Step> steps)
    {
        Console.WriteLine(SetupFor(steps));
        foreach (var step in steps)
        {
            Console.WriteLine($"[{step.Id}] {step.Instruction}");
            if (step.Check is { } check)
            {
                Console.WriteLine($"  {check.Source}");
                Console.WriteLine($"  Start and stop within {check.WindowSeconds} s: {check.Name}, {check.ReportSource}.");
                if (check.Optional) Console.WriteLine("  Optional. Run only when the setup is available.");
                if (check.Missing is { } missing)
                {
                    Console.WriteLine($"  MISSING: {missing}");
                    continue;
                }
            }
            if (step.Needs is { } needs) Console.WriteLine($"  {needs.Describe()}");
            if(step.Signal is {} signal)Console.WriteLine($"  Wait up to 45 s for {signal}; watch at least 5 s after it and let reported clips finish within the limit. {SignalSource(signal)}.");
            Console.WriteLine($"  {step.Question} [yes/no/partly]");
        }
        Console.WriteLine("Operator answers are observations. Checks use the named engine report in the step window.");
    }

    internal static bool IsBehaviorStart(JsonObject e, BehaviorID id) =>
        e["kind"]?.GetValue<string>() == "behavior-transition"
        && e[nameof(BehaviorTransitionReport.NewId)]?.GetValue<byte>() == (byte)id
        && e[nameof(BehaviorTransitionReport.OldId)]?.GetValue<byte>() != (byte)id;

    internal static bool IsBehaviorStop(JsonObject e, BehaviorID id) =>
        e["kind"]?.GetValue<string>() == "behavior-transition"
        && e[nameof(BehaviorTransitionReport.OldId)]?.GetValue<byte>() == (byte)id
        && e[nameof(BehaviorTransitionReport.NewId)]?.GetValue<byte>() != (byte)id;

    internal static bool ObservationCompleted(BehaviorCheck check,IEnumerable<JsonObject> reports,double start,double end)
    {
        bool started=false;
        foreach(var report in reports.Where(report=>report["atMs"]?.GetValue<double>() is {} time&&time>=start&&time<=end)
            .OrderBy(report=>report["atMs"]!.GetValue<double>()))
        {
            if(check.Starts(report))started=true;
            else if(started&&check.Stops(report))return true;
        }
        return false;
    }
    internal static double? ObservationStartedAt(BehaviorCheck check,IEnumerable<JsonObject> reports,double start,double end)=>
        reports.Where(report=>report["atMs"]?.GetValue<double>() is {} at&&at>=start&&at<=end&&check.Starts(report))
            .Select(report=>(double?)report["atMs"]!.GetValue<double>()).OrderBy(at=>at).FirstOrDefault();
    internal static string? ParseObservation(string value)=>value.Trim().ToLowerInvariant() switch
    {"yes" or "y"=>"yes","no" or "n"=>"no","partly" or "p"=>"partly",_=>null};

    private static bool IsReactionTransition(JsonObject report, ReactionTrigger trigger, bool entering)
    {
        if (report["kind"]?.GetValue<string>() != "reaction-transition") return false;
        var old = report[nameof(ReactionTransitionReport.OldTrigger)]!.GetValue<byte>();
        var next = report[nameof(ReactionTransitionReport.NewTrigger)]!.GetValue<byte>();
        return entering ? next == (byte)trigger && old != next : old == (byte)trigger && old != next;
    }

    internal static Task<uint> QueueAbsentObjectDrive(CozmoEngine engine, CancellationToken cancellation)
    {
        using var lease = engine.EnterSynchronousOwner();
        cancellation.ThrowIfCancellationRequested();
        if (engine.Robot is not { Lifetime.Destroying: false } owner
            || owner.NativeBehaviorRuntime?.Manipulation is not { } manipulation)
            throw new InvalidOperationException("MISSING: The native drive-to-object owner is unavailable.");

        const uint absentId = uint.MaxValue;
        if (manipulation.World.GetLocatedObjectByIdHelper(absentId) is not null)
            throw new InvalidOperationException("The reserved absent object ID unexpectedly has a pose.");
        // This diagnostic only takes the absent-target precondition. Let Freeplay retain its tracks.
        var drive = new NativeDriveToObjectRunner(manipulation, owner, absentId, PreActionType.Docking)
        {
            SuppressTrackLocking = true,
        };
        var completed = new TaskCompletionSource<uint>(TaskCreationOptions.RunContinuationsAsynchronously);
        drive.AddCompletionCallback(result => completed.TrySetResult(result));
        if (owner.ActionList.QueueAction(QueueActionPosition.InParallel, drive) != 0)
            throw new InvalidOperationException("The absent-object drive was rejected.");
        return WaitForResult();

        async Task<uint> WaitForResult()
        {
            using var registration = cancellation.Register(() => owner.ActionList.Cancel(drive.Tag));
            return await completed.Task.WaitAsync(cancellation).ConfigureAwait(false);
        }
    }
}

internal sealed partial class M15CubeRun
{
    private readonly ConcurrentQueue<JsonObject> personalityTransitions = new();

    void SubscribePersonalityReports(CozmoEngine engine)
    {
        engine.RobotPublicStateChanged += report => Event("robot-public-state", JsonSerializer.SerializeToNode(report, BundleJson)!.AsObject());
        // M14-008: RobotObservedFace carries the name the engine published in 0x46.
        engine.RobotObservedFace += face => Event("face-observed", new()
        {
            ["faceId"] = face.FaceId,
            ["robotTimestamp"] = face.Timestamp,
            ["name"] = face.Name
        });
        engine.EngineToGameMessage += (tag, body) =>
        {
            switch (tag)
            {
                case ObjectConnectionStateMessage.UnionTag when body.Length == 13:
                    // M11-085: the E2G connection body carries the world ObjectID.
                    var reader = new CladReader(body);
                    Event("cube-connection", new()
                    {
                        ["objectId"] = reader.U32(), ["factoryId"] = reader.U32(),
                        ["objectType"] = reader.I32(), ["connected"] = reader.U8() != 0
                    });
                    break;
                case 0x0F when body.Length == 21:
                    // M4-009: ObjectMoved broadcast 0x00534190..0x005341B0.
                    var move = ObjectMoved.Read(new CladReader(body));
                    Event("cube-movement", new()
                    {
                        ["objectId"] = move.ObjectID, ["robotTimestamp"] = move.Timestamp, ["moving"] = true
                    });
                    break;
                case 0x10 when body.Length == 8:
                    var stop = ObjectStoppedMoving.Read(new CladReader(body));
                    Event("cube-movement", new()
                    {
                        ["objectId"] = stop.ObjectID, ["robotTimestamp"] = stop.Timestamp, ["moving"] = false
                    });
                    break;
            }
        };
    }

    void RunPersonality(CozmoRobot r)
    {
        Event("request-freeplay", new() { ["enabled"] = true });
        r.SetEnableFreeplayActivity(true);
        try
        {
            foreach (var step in PersonalitySteps)
            {
                if (step.Check?.Missing is not null)
                {
                    RunPersonalityStep(r, step);
                    continue;
                }
                if (step.Check?.Optional == true && !ReadYesNo($"Run the optional [{step.Id}] step?"))
                {
                    RunPersonalityStep(r, step, skipped: true);
                    continue;
                }
                Console.WriteLine($"\nPress Enter to start [{step.Id}].");
                ReadOperatorInput();
                RunPersonalityStep(r, step);
            }
        }
        finally
        {
            Event("request-freeplay", new() { ["enabled"] = false });
            r.SetEnableFreeplayActivity(false);
        }
    }

    void RunPersonalityStep(CozmoRobot robot, PersonalityHw.Step step, bool skipped = false)
    {
        double start = Now;
        Event("phase-start", new() { ["phase"] = step.Id });
        bool? observed = null;
        string? answer=null;
        string question=step.Question;
        double? idleStart = null, idleEnd = null;
        double? checkEnd = null;
        string? checkResult = null;
        try
        {
            if (step.Check?.Missing is { } missing)
            {
                checkResult = "missing";
                string line = $"MISSING [{step.Id}]: {missing}";
                Console.WriteLine(line);
                stackLines.Enqueue(line);
                Event("missing", new() { ["phase"] = step.Id, ["detail"] = missing });
                return;
            }
            if (skipped)
            {
                checkResult = "skipped";
                return;
            }
            Console.WriteLine(step.Instruction);
            if (step.Check is { } check)
            {
                Console.WriteLine($"Waiting up to {check.WindowSeconds} seconds for {check.Name} to start and stop.");
                bool Started(JsonObject e) => e["atMs"]!.GetValue<double>() >= start && check.Starts(e);
                void WaitForReports()
                {
                    using var stimulus = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
                    try
                    {
                        if (step.Id == "search-for-face")
                        {
                            Console.WriteLine("Waiting for the current-origin FaceWorld to empty, then requesting SearchForFace.");
                            Poll(() => PersonalityHw.TryRequestSearchForFace(robot),
                                Math.Max(0, (int)(start + check.WindowSeconds * 1000 - Now)));
                        }
                        if (step.Id == "drive-to-face")
                            Poll(() => PersonalityHw.TryRequestDriveToFace(robot),
                                Math.Max(0, (int)(start + check.WindowSeconds * 1000 - Now)));
                        Task<AiTurnActionResult>? turn = step.Id == "react-to-unexpected-movement"
                            ? robot.Motion.QueueAiTurnInPlaceAsync(MathF.PI, TurnInPlaceAction.DefaultMaxSpeed,
                                TurnInPlaceAction.DefaultAccel, cancellationToken: stimulus.Token) : null;
                        if (step.Id == "react-to-frustration")
                            RunFrustrationActions(robot, () => personalityTransitions.Any(Started), start, stimulus.Token);
                        Poll(() => PersonalityHw.ObservationCompleted(check,personalityTransitions,start,Now),
                            Math.Max(0, (int)(start + check.WindowSeconds * 1000 - Now)));
                        if (turn?.IsCompletedSuccessfully == true)
                            Event("reaction-stimulus-turn", JsonSerializer.SerializeToNode(turn.Result, BundleJson)!.AsObject());
                    }
                    finally
                    {
                        checkEnd = Now;
                        stimulus.Cancel();
                        if (step.Id is "search-for-face" or "drive-to-face") robot.SetEnableFreeplayActivity(true);
                    }
                }
                try
                {
                    if (step.Needs is { } levels)
                        levels.WithOverride(robot.Engine, WaitForReports, (restored, values) => Event(
                            restored ? "needs-levels-restored" : "needs-levels-forced", new()
                            {
                                ["phase"] = step.Id,
                                ["repair"] = values[0], ["energy"] = values[1], ["play"] = values[2],
                            }));
                    else
                        WaitForReports();
                    checkResult = StepTransitions(check, start, checkEnd ?? Now).Any(check.Starts)
                        ? "passed" : "failed";
                    Console.WriteLine(checkResult == "passed"
                        ? (PersonalityHw.ObservationCompleted(check,personalityTransitions,start,checkEnd??Now)?"Engine start and stop reports observed.":"Engine trigger arrived; the behavior is still running at the observation limit.")
                        : "The engine trigger never arrived in this window.");
                }
                catch (InvalidOperationException e) when (check.Trigger is not null)
                {
                    checkResult = "harness-error";
                    errors.Enqueue($"{step.Id}: {e.Message}");
                    Console.WriteLine(e.Message);
                    Event("reaction-stimulus-error", new() { ["phase"] = step.Id, ["detail"] = e.Message });
                }
            }
            if(step.Signal is {} signal)
            {
                Console.WriteLine($"Waiting up to 45 seconds for the engine's {signal} report.");
                bool received=Poll(()=>narration?.SawSignal(signal,start,Now)==true,45000);
                if(received)
                {
                    // Host observation policy: leave time for a reaction after
                    // the cube event, then let acknowledged clips finish. This
                    // is not a native reaction threshold or a new pass criterion.
                    double signalAt=Now;
                    Console.WriteLine("Event received. Watching the response before asking for your observation.");
                    Poll(()=>narration?.ObservationSettled(signalAt,Now)==true,
                        Math.Max(0,(int)(start+45000-Now)));
                }
                checkEnd=Now;checkResult=received?"passed":"failed";
                Console.WriteLine(received?"Engine event observed.":"The engine trigger never arrived in this window.");
            }
            if (step.Id == "alone")
            {
                Console.WriteLine("Press Enter once the area is clear to start the idle timer.");
                ReadOperatorInput();
                idleStart = Now;
                Event("idle-window-start", new() { ["minutes"] = PersonalityHw.IdleMinutes });
                for (int minute = 1; minute <= PersonalityHw.IdleMinutes && !cancel.IsCancellationRequested; minute++)
                {
                    cancel.Token.WaitHandle.WaitOne(TimeSpan.FromMinutes(1));
                    if (!cancel.IsCancellationRequested) Console.WriteLine($"  idle: {minute} of {PersonalityHw.IdleMinutes} minutes");
                }
                idleEnd = Now;
                cancel.Token.ThrowIfCancellationRequested();
                Event("idle-window-end", new());
            }
            // Incidental Freeplay clips before this step's actual trigger must
            // not be presented as the requested reaction.
            double observationStart=step.Check is {} expected?
                PersonalityHw.ObservationStartedAt(expected,personalityTransitions,start,checkEnd??Now)??Now:start;
            question=narration?.ObservationQuestion(observationStart,Now,step.Question)??step.Question;
            Console.WriteLine(question+" [yes/no/partly]");
            while(answer is null)
            {
                answer=PersonalityHw.ParseObservation(ReadOperatorInput());
                if(answer is null)Console.WriteLine("Enter yes, no or partly.");
            }
            observed=answer=="partly"?null:answer=="yes";
            answers[step.Id] = answer;
            Event("operator-observation", new() { ["phase"] = step.Id, ["question"] = question, ["observed"] = observed,["answer"]=answer });
        }
        finally
        {
            var phase = new JsonObject { ["phase"] = step.Id, ["startMs"] = start, ["endMs"] = Now,
                ["question"] = question, ["observed"] = observed,["answer"]=answer };
            if (step.Check is { } check)
            {
                phase[check.Trigger is null ? "behaviorCheck" : "reactionCheck"] = new JsonObject
                {
                    ["expected"] = check.Name, ["source"] = check.Source,
                    ["reportSource"] = check.ReportSource, ["result"] = checkResult ?? "interrupted",
                    ["windowEndMs"] = checkEnd,
                    ["transitions"] = new JsonArray(StepTransitions(check, start, checkEnd ?? Now)
                        .Select(e => e.DeepClone()).ToArray()),
                };
            }
            if(step.Signal is {} expectedSignal)
                phase["signalCheck"]=new JsonObject{["expected"]=expectedSignal,["result"]=checkResult??"interrupted",
                    ["windowEndMs"]=checkEnd,["source"]=PersonalityHw.SignalSource(expectedSignal)};
            if (idleStart is { } idle)
            {
                phase["idleStartMs"] = idle;
                phase["idleEndMs"] = idleEnd;
            }
            phases.Add(phase);
            Event("phase-end", (JsonObject)phase.DeepClone());
        }
    }

    internal JsonObject[] BehaviorTransitions(BehaviorID id, double start, double end)
        => StepTransitions(new(id, ""), start, end);

    internal JsonObject[] StepTransitions(PersonalityHw.BehaviorCheck check, double start, double end)
        => personalityTransitions.Where(e => e["atMs"]!.GetValue<double>() >= start && e["atMs"]!.GetValue<double>() <= end
                && (check.Starts(e) || check.Stops(e)))
            .OrderBy(e => e["atMs"]!.GetValue<double>()).ToArray();

    private void RunFrustrationActions(CozmoRobot robot, Func<bool> started, double start, CancellationToken cancellation)
    {
        for (int attempt = 1; attempt <= 12 && !started() && Now < start + 365_000; attempt++)
        {
            using var action = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            action.CancelAfter(TimeSpan.FromSeconds(2));
            uint result;
            try { result = PersonalityHw.QueueAbsentObjectDrive(robot.Engine, action.Token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                throw new InvalidOperationException("The absent-object drive did not report completion in two seconds.");
            }
            Event("reaction-stimulus-action", new() { ["attempt"] = attempt, ["engineResult"] = result });
            if (result != (uint)ActionResult.BadObject)
                throw new InvalidOperationException($"Absent-object drive returned 0x{result:X8}.");
            if (attempt < 12) Poll(started, 30_000);
        }
    }

    JsonArray PersonalityReports(double start, double end)
    {
        return new JsonArray(events.Where(e =>
                e["kind"]!.GetValue<string>() is "behavior-transition" or "reaction-transition" or "behavior-objective"
                    or "robot-public-state" or "face-observed" or "cube-connection" or "cube-movement"
                && e["atMs"]!.GetValue<double>() >= start && e["atMs"]!.GetValue<double>() <= end)
            .OrderBy(e => e["atMs"]!.GetValue<double>()).Select(e => e.DeepClone()).ToArray());
    }
}
