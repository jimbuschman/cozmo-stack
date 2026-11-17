using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;

namespace Cozmo.Conformance;

internal static class PersonalityHw
{
    internal const string TestId = "PERSONALITY-HW-1";
    internal const int IdleMinutes = 15;
    private const string Setup = "Start Cozmo off his charger on a clear, well-lit surface.";

    internal static readonly string[] RecordIds =
    {
        "M7-001", "M7-019", "M8-012", "M10-001", "M10-003", "M10-004", "M10-008",
        "M4-009", "M4-023", "M11-085", "M13-038", "M14-008", "M15-031", "M15-058"
    };

    internal sealed record Step(string Id, string Instruction, string Question);

    internal static readonly Step[] Steps =
    {
        new("pickup", "Gently pick Cozmo up, keep him upright and hold him still.", "Did you see or hear a pickup reaction?"),
        new("putdown", "If Cozmo is already on his treads, gently pick him up and hold him upright briefly. Set him back down on his treads.", "Did you see or hear a reaction to being put down?"),
        new("on-back", "Gently place Cozmo on his back. Watch his response, then return him to his treads.", "Did you see or hear a response to being on his back?"),
        new("on-side", "Gently place Cozmo on one side. Watch his response, then return him to his treads.", "Did you see or hear a response to being on his side?"),
        new("on-face", "Support Cozmo facing down. Watch his response, then return him to his treads.", "Did you see or hear a response to being face down?"),
        new("shaken", "Pick up Cozmo, give him a brief gentle shake, then set him back on his treads.", "Did you see or hear a distinct response to the shake?"),
        new("cliff", "Let Cozmo approach a table edge under close hand supervision. Catch him if needed and then move him back to safety.", "Did you see or hear a reaction to the edge?"),
        new("face", "Show your face in good light. Use a previously enrolled face if available and listen for recognition by name.", "Did Cozmo acknowledge your face, by name if enrolled?"),
        new("cube-tap", "Put the light cube upright with a marker facing Cozmo. Give him time to see it, then tap the cube.", "Did you see or hear a response to the tap?"),
        new("cube-move", "Move the cube to a different spot within Cozmo's view, keeping it upright.", "Did you see or hear a response to the cube moving?"),
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
            Console.WriteLine($"  {step.Question} [yes/no]");
        }
        Console.WriteLine("Observations only. Each step records behavior, reactions, public state, and cube connection and movement reports.");
    }
}

internal sealed partial class M15CubeRun
{
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
                Console.WriteLine($"\nPress Enter to start [{step.Id}].");
                ReadOperatorInput();
                RunPersonalityStep(step);
            }
        }
        finally
        {
            Event("request-freeplay", new() { ["enabled"] = false });
            r.SetEnableFreeplayActivity(false);
        }
    }

    void RunPersonalityStep(PersonalityHw.Step step)
    {
        double start = Now;
        Event("phase-start", new() { ["phase"] = step.Id });
        bool? observed = null;
        double? idleStart = null, idleEnd = null;
        try
        {
            Console.WriteLine(step.Instruction);
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
            if (idleStart is { } idle)
            {
                phase["idleStartMs"] = idle;
                phase["idleEndMs"] = idleEnd;
            }
            phases.Add(phase);
            Event("phase-end", (JsonObject)phase.DeepClone());
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
