using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;

namespace Cozmo.Conformance;

internal static class PersonalityHw
{
    internal const string TestId = "PERSONALITY-HW-1";
    internal const int IdleMinutes = 15;
    private const string Setup = "Start Cozmo off his charger on a clear, well-lit surface.";

    internal static readonly string[] RecordIds =
    {
        "M7-001", "M7-019", "M8-012", "M10-001", "M10-003", "M10-004", "M10-008",
        "M4-009", "M4-023", "M11-085", "M13-038", "M14-004", "M14-008", "M15-014", "M15-031", "M15-048",
        "M15-058", "M15-080", "M15-082", "M15-091", "M15-104", "M15-108"
    };

    internal const string BehaviorReportSource = "M8-012, E2G 0x87 at 0x005A22B4..0x005A24AC, payload 0x0071B806..0x0071B884";
    internal readonly record struct NeedsLevels(float Repair, float Energy, float Play)
    {
        internal string Describe() => FormattableString.Invariant(
            $"Force Repair={Repair}, Energy={Energy}, Play={Play} via G2E 0xC6 (M15-048, 0x00697E78..0x00697EC6), then restore the saved levels.");

        internal void WithOverride(CozmoEngine engine, Action observe, Action<bool, float[]>? record = null)
        {
            float[]? saved = null;
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
                    record?.Invoke(true, saved);
                }
            }
        }
    }

    internal sealed record BehaviorCheck(BehaviorID Id, string Source, bool Optional = false, string? Missing = null,
        int WindowSeconds = 45);
    internal sealed record Step(string Id, string Instruction, string Question, BehaviorCheck? Check = null, NeedsLevels? Needs = null);

    internal static readonly Step[] Steps =
    {
        new("pickup", "Gently pick Cozmo up, keep him upright and hold him still.", "Did you see or hear a pickup reaction?"),
        new("putdown", "If Cozmo is already on his treads, gently pick him up and hold him upright briefly. Set him back down on his treads.", "Did you see or hear a reaction to being put down?"),
        new("on-back", "Gently place Cozmo on his back. Watch his response, then return him to his treads.", "Did you see or hear a response to being on his back?"),
        new("on-side", "Gently place Cozmo on one side. Watch his response, then return him to his treads.", "Did you see or hear a response to being on his side?"),
        new("on-face", "Support Cozmo facing down. Watch his response, then return him to his treads.", "Did you see or hear a response to being face down?"),
        new("shaken", "Pick up Cozmo, give him a brief gentle shake, then set him back on his treads.", "Did you see or hear a distinct response to the shake?"),
        new("cliff", "Let Cozmo approach a table edge under close hand supervision. Catch him if needed and then move him back to safety.", "Did you see or hear a reaction to the edge?"),
        new("acknowledge-face",
            "Keep your face out of view until the step begins. Then show an enrolled face he has yet to react to this session. For a face already seen at a distance, approach from beyond 40 cm to within 30 cm when he is free to react.",
            "Did you see or hear Cozmo acknowledge the face?",
            new(BehaviorID.AcknowledgeFace, "FacePositionUpdated names a new enrolled face or admits a newly close face after the 4 s cooldown (0x0060CB84..0x0060CE20). The strategy passes the desired set off the charger (0x0060CAB0..0x0060CB5E), and IsRunnableInternal requires that set (0x006028BC..0x006028C6). reactionTrigger_behavior_map.json selects AcknowledgeFace.")),
        new("interact-with-faces",
            "Show one face after the step begins. Keep it in view in good light while Cozmo is on his treads, off his charger and Freeplay enters Socialize.",
            "Did Cozmo turn to or track the face?",
            new(BehaviorID.InteractWithFaces, "IsRunnableInternal selects a recent face and requires a valid target (0x005C23AC..0x005C23EE). Socialize checks this behavior in phase 0 and selects it when runnable (0x005B02F6..0x005B0302, branch to 0x005B0354).")),
        new("search-for-face",
            "For the engine's search request, leave the camera view and allow the FaceWorld to empty. An enrolled face may remain in the FaceWorld.",
            "Did Cozmo search for a face?",
            new(BehaviorID.VC_SearchForFace, "voiceCommands/VC_SearchForFace.json maps the requested ID to SearchForFace. IsRunnableInternal requires !FaceWorld::HasAnyFaces(0,false) (0x005C9170..0x005C9180). Selection handles ExecuteBehaviorByID (G2E 0x94, 0x0060AA58..0x0060AB7A).",
                Missing: "SearchForFace is unbuilt in the configured native factory (case 0x0059CFA6).")),
        new("drive-to-face",
            "Keep one face in view farther than 20 cm from Cozmo on a clear level surface.",
            "Did Cozmo approach the face?",
            new(BehaviorID.VC_ComeHere, "voiceCommands/VC_ComeHere.json maps the requested ID to DriveToFace. Readiness needs a valid most recently observed face in the current origin (0x005DA588..0x005DA626). After verification, the drive branch uses the 200 mm standoff (0x005DACDC, 0x005DA950). Selection handles G2E 0x94 (0x0060AA58..0x0060AB7A).",
                Missing: "DriveToFace is unbuilt in the configured native factory (case 0x0059CB00).")),
        new("react-to-pet",
            "If a pet is available, keep its face in Cozmo's view for several camera frames. Start off the charger after at least 60 seconds without a pet reaction.",
            "Did you see or hear Cozmo react to the pet?",
            new(BehaviorID.ReactToPet, "PetInitialDetection requires three observations of an unreacted pet, off the charger, and a 60 s cooldown (0x00611AE4..0x00611CB6, 0x00611DD0). ReactToPet requires a target (0x00606ED8..0x00606EEC). reactionTrigger_behavior_map.json selects ReactToPet.", Optional: true)),
        new("face", "Show your face in good light. Use a previously enrolled face if available and listen for recognition by name.", "Did Cozmo acknowledge your face, by name if enrolled?"),
        new("cube-tap", "Put the light cube upright with a marker facing Cozmo. Give him time to see it, then tap the cube.", "Did you see or hear a response to the tap?"),
        new("cube-move", "Move the cube to a different spot within Cozmo's view, keeping it upright.", "Did you see or hear a response to the cube moving?"),
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
                Console.WriteLine($"  Start and stop: {check.Id}, {BehaviorReportSource}.");
                if (check.Optional) Console.WriteLine("  Optional. Skip if no pet is available.");
                if (check.Missing is { } missing)
                {
                    Console.WriteLine($"  MISSING: {missing}");
                    continue;
                }
            }
            if (step.Needs is { } needs) Console.WriteLine($"  {needs.Describe()}");
            Console.WriteLine($"  {step.Question} [yes/no]");
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
}

internal sealed partial class M15CubeRun
{
    private readonly ConcurrentQueue<JsonObject> behaviorTransitions = new();

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
                Console.WriteLine($"Waiting up to {check.WindowSeconds} seconds for {check.Id} to start and stop.");
                bool Started(JsonObject e) => e["atMs"]!.GetValue<double>() >= start && PersonalityHw.IsBehaviorStart(e, check.Id);
                bool Stopped(JsonObject e) => e["atMs"]!.GetValue<double>() >= start && PersonalityHw.IsBehaviorStop(e, check.Id);
                void WaitForReports()
                {
                    try { Poll(() => behaviorTransitions.Any(Started) && behaviorTransitions.Any(Stopped), check.WindowSeconds * 1000); }
                    finally { checkEnd = Now; }
                }
                if (step.Needs is { } levels)
                    levels.WithOverride(robot.Engine, WaitForReports, (restored, values) => Event(
                        restored ? "needs-levels-restored" : "needs-levels-forced", new()
                        {
                            ["phase"] = step.Id,
                            ["repair"] = values[0], ["energy"] = values[1], ["play"] = values[2],
                        }));
                else
                    WaitForReports();
                checkResult = BehaviorTransitions(check.Id, start, checkEnd ?? Now).Any(e => PersonalityHw.IsBehaviorStart(e, check.Id))
                    ? "passed" : "failed";
                Console.WriteLine(checkResult == "passed" ? "Engine start report observed." : "No matching engine start report in the window.");
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
            observed = ReadYesNo(step.Question);
            answers[step.Id] = observed;
            Event("operator-observation", new() { ["phase"] = step.Id, ["question"] = step.Question, ["observed"] = observed });
        }
        finally
        {
            var phase = new JsonObject { ["phase"] = step.Id, ["startMs"] = start, ["endMs"] = Now,
                ["question"] = step.Question, ["observed"] = observed };
            if (step.Check is { } check)
            {
                phase["behaviorCheck"] = new JsonObject
                {
                    ["expected"] = check.Id.ToString(), ["source"] = check.Source,
                    ["reportSource"] = PersonalityHw.BehaviorReportSource, ["result"] = checkResult ?? "interrupted",
                    ["windowEndMs"] = checkEnd,
                    ["transitions"] = new JsonArray(BehaviorTransitions(check.Id, start, checkEnd ?? Now)
                        .Select(e => e.DeepClone()).ToArray()),
                };
            }
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
        => behaviorTransitions.Where(e => e["atMs"]!.GetValue<double>() >= start && e["atMs"]!.GetValue<double>() <= end
                && (PersonalityHw.IsBehaviorStart(e, id) || PersonalityHw.IsBehaviorStop(e, id)))
            .OrderBy(e => e["atMs"]!.GetValue<double>()).ToArray();

    JsonArray PersonalityReports(double start, double end)
    {
        return new JsonArray(events.Where(e =>
                e["kind"]!.GetValue<string>() is "behavior-transition" or "reaction-transition" or "behavior-objective"
                    or "robot-public-state" or "face-observed" or "cube-connection" or "cube-movement"
                && e["atMs"]!.GetValue<double>() >= start && e["atMs"]!.GetValue<double>() <= end)
            .OrderBy(e => e["atMs"]!.GetValue<double>()).Select(e => e.DeepClone()).ToArray());
    }
}
