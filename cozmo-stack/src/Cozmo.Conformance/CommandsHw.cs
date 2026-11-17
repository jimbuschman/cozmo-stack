using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;

namespace Cozmo.Conformance;

internal static class CommandsHw
{
    internal const string TestId = "AI-COMMANDS-HW-1";
    internal const int TrackSeconds = 10;
    internal const int FaceActionLimitMs = 20_000;
    internal const float MinTurnFaceBearingDegrees = 15;
    internal const AnimationTrigger DemoAnimation = AnimationTrigger.CodeLabHappy;
    internal const string TurnFacePosition = "Start about 20 degrees to one side of where he is pointing, keeping both eyes in view. Hold still until he turns.";
    internal const string CubePosition = "Place one upright light cube about 25 cm ahead and a little to one side, with a marker facing Cozmo.";
    // FindFacesLookUp supplies 20..40 degrees at 0x005C1AA6..0x005C1AC2.
    // Reuse the lower endpoint for a repeatable operator setup.
    internal const float FaceSetupHeadAngle = (float)(20 * Math.PI / 180);
    internal const string FacePosition = "Get down near Cozmo's level in good light, about 30–60 cm in front of him, with your face a little above his camera.";
    internal const string Setup = "Put Cozmo on a clear, well-lit surface, off his charger and well away from edges. "
        + "Leave room to drive and turn. Have one working cube and one operator in view during the applicable prompts.";

    internal sealed record Step(string Id, string Plan, string Question, string Record, string Address, uint? Expected = 0);
    internal static readonly Step FaceSetup = new("face-setup", "Raise the head to 20 degrees.",
        "", "M15-103", "0x005C1AA6–0x005C1AC2, 0x005E331A–0x005E3350");
    internal static readonly Step[] Steps =
    {
        new("head", "Raise the head to 0.18 rad.", "Did his head tilt up?", "M4-016", "0x005485CC"),
        new("lift", "Raise the lift to 45 mm.", "Did his lift move to about the requested height?", "M4-016", "0x005493F6"),
        new("drive-straight", "Drive straight 100 mm.", "Did he drive about 10 cm straight ahead?", "M13-013", "0x005478C0"),
        new("turn-in-place", "Turn left 90 degrees in place.", "Did he turn left about 90 degrees?", "M13-022", "0x0054642C"),
        new("turn-to-face", "Turn toward the operator's observed face.", "Did he turn toward your face?", "M14-002", "0x0054C500"),
        new("turn-to-cube", "Turn toward a located light cube.", "Did he turn toward the cube?", "M13-020", "0x0054ADD4"),
        new("track-face", "Request face tracking and cancel after 10 seconds.", "Did he follow your face as you moved?", "M8-015", "0x005409DC–0x00540A46", 0x02000000),
        new("animation", "Play the CodeLabHappy celebration once.", "Did you see the celebration animation on his face?", "M8-020", "0x005441C0"),
        new("sentence", "Say one English sentence.", "Did you hear Cozmo say the sentence?", "M14-006", "0x00561D4C"),
        new("go-to-pose", "Drive to a point 200 mm ahead and turn left 90 degrees.", "Did he reach the point and turn left?", "M12-023", "0x0055AB4C"),
        new("teach-face", "Teach the operator's face a temporary name.", "Did you observe him learning the name?", "M14-012", "0x005FE520–0x005FE528"),
        new("rename-face", "Rename the newly enrolled face.", "Did you observe him acknowledge the new name?", "M14-012", "0x00657B66–0x00657B82", null),
        new("erase-face", "Erase the same enrolled face ID.", "Did you observe the temporary face being erased?", "M14-012", "E2G 0x57", null),
    };

    internal static readonly string[] RecordIds = Steps.Select(s => s.Record).Concat(new[]
        { FaceSetup.Record, "M14-003", "M14-008", "M11-035", "M11-038", "M4-009", "M4-023", "M15-031", "M2-021" }).Distinct().ToArray();

    // Diagnostic relative bearing from the two public poses, using the signed
    // yaw and a shared origin. The action still computes its own native turn.
    internal static float? FaceBearingDegrees(RobotObservedFaceReport face, EngineRobotState? state)
    {
        if (state is null || state.Pose.OriginId != face.Pose.OriginId) return null;
        float x = face.Pose.X - state.Pose.X;
        float y = face.Pose.Y - state.Pose.Y;
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(state.PoseAngleRad) || (x == 0 && y == 0))
            return null;
        return Radians.Rescale(MathF.Atan2(y, x) - state.PoseAngleRad) * (180 / MathF.PI);
    }

    internal static T? WaitForReport<T>(Action<Action<T>> subscribe, Action<Action<T>> unsubscribe,
        Func<T, bool> matches, Action postRequest, TimeSpan timeout, CancellationToken cancellationToken) where T : class
    {
        bool hasDeadline = timeout != Timeout.InfiniteTimeSpan;
        var elapsed = Stopwatch.StartNew();
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Receive(T report)
        {
            if (completion.Task.IsCompleted) return;
            if ((!hasDeadline || elapsed.Elapsed <= timeout) && matches(report)) completion.TrySetResult(report);
        }
        subscribe(Receive);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            postRequest(); // The production game APIs enqueue and return.
            TimeSpan remaining = hasDeadline ? timeout - elapsed.Elapsed : Timeout.InfiniteTimeSpan;
            if (hasDeadline && remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            return completion.Task.WaitAsync(remaining, cancellationToken)
                .GetAwaiter().GetResult();
        }
        catch (TimeoutException) { return null; }
        finally { unsubscribe(Receive); }
    }

    internal static void PrintPlan()
    {
        Console.WriteLine(Setup + "\nFreeplay is off.");
        foreach (var step in Steps)
        {
            if (step.Id is "turn-to-face" or "track-face")
            {
                Console.WriteLine($"[{step.Id}-setup] {FaceSetup.Plan} {FacePosition} ({FaceSetup.Record}, {FaceSetup.Address})");
                Console.WriteLine($"Wait for a fresh public face observation before starting the {FaceActionLimitMs / 1000} s action window.");
                if (step.Id == "turn-to-face")
                    Console.WriteLine($"{TurnFacePosition} Wait for a fresh face at least {MinTurnFaceBearingDegrees} degrees off center and record its bearing.");
            }
            if (step.Id == "turn-to-cube")
                Console.WriteLine($"[turn-to-cube-setup] {CubePosition} Wait up to 20 s for a fresh light-cube observation.");
            string check = step.Expected is { } expected ? $"expect 0x{expected:X8}" : "report only";
            Console.WriteLine($"[{step.Id}] {step.Plan} {check} ({step.Record}, {step.Address})");
        }
        Console.WriteLine("The run also records public face, cube and object-pose reports and yes/no observations.");
    }
}

internal sealed partial class M15CubeRun
{
    static readonly JsonSerializerOptions CommandPayloadOptions = new()
    { IncludeFields = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

    void SubscribeCommandReports(CozmoEngine engine)
    {
        engine.RobotObservedFace += report =>
        {
            var data = JsonSerializer.SerializeToNode(report, CommandPayloadOptions)!.AsObject();
            data["recognizedByName"] = report.Name.Length != 0;
            Event("face-seen", data);
        };
        engine.RobotChangedObservedFaceId += report => CommandReport("face-id-changed", report);
        engine.RobotDeletedFace += report => CommandReport("face-lost", report);
        engine.RobotObservedObject += report =>
        {
            if (report.Family == (uint)ObjectFamily.LightCube) CommandReport("cube-seen", report);
        };
        engine.ObjectPoseUnknown += report => CommandReport("object-pose-lost", report);
        engine.FaceEnrollmentCompleted += report => CommandReport("face-enrollment-completed", report);
        engine.FaceRenamed += report => CommandReport("face-renamed", report);
        engine.FaceErased += report => CommandReport("face-erased", report);
    }

    void SubscribeCommandRobotReports(CozmoRobot r)
    {
        // RobotProcessedImage (E2G 0x43, M11-035, 0x00654A14..0x00654A1C)
        // identifies which captured timestamps actually reached each vision mode.
        r.Vision.ProcessedImage += report => CommandReport("image-processed", report);
        r.Cubes.CubeTapped += report => CommandReport("cube-tapped", report);
        r.Cubes.CubeMoved += report => CommandReport("cube-moved", report);
        r.Cubes.TapBroadcast += report => CommandReport("cube-tap-report", report);
        r.Cubes.MovementBroadcast += report => CommandReport("cube-movement-report", report);
    }

    void CommandReport<T>(string kind, T report) =>
        Event(kind, JsonSerializer.SerializeToNode(report, CommandPayloadOptions)!.AsObject());

    void RunCommands(CozmoRobot r)
    {
        r.SetEnableFreeplayActivity(false);
        PromptCommand("Clear space ahead and to Cozmo's left, then press Enter for head, lift, and drive commands.");
        RunCommand("head", t => r.Motion.QueueAiHeadAngleAsync(0.18f, t), a => (a.Admitted, a.EngineResult, a.Detail, a.OwnerRetired));
        RunCommand("lift", t => r.Motion.QueueAiLiftHeightAsync(45, t), a => (a.Admitted, a.EngineResult, a.Detail, a.OwnerRetired));
        RunCommand("drive-straight", t => r.Motion.QueueAiDriveStraightAsync(100, 60, cancellationToken: t), a => (a.Admitted, a.EngineResult, a.Detail, a.OwnerRetired));
        RunCommand("turn-in-place", t => r.Motion.QueueAiTurnInPlaceAsync(MathF.PI / 2,
            TurnInPlaceAction.DefaultMaxSpeed, TurnInPlaceAction.DefaultAccel, cancellationToken: t),
            a => (a.Admitted, a.EngineResult, a.Detail, a.OwnerRetired));

        var face = FindCommandFace(r, "turn-to-face", CommandsHw.TurnFacePosition);
        if (face is not null)
            RunCommand("turn-to-face", t => r.Motion.QueueAiTurnToFaceAsync(face.FaceId, MathF.PI, t),
                a => (a.Admitted, a.EngineResult, a.Detail, a.OwnerRetired), CommandsHw.FaceActionLimitMs);
        else SkipCommand("turn-to-face", "The head setup action did not succeed.");

        PromptCommand($"{CommandsHw.CubePosition} Press Enter when ready.");
        var cube = CommandsHw.WaitForReport<RobotObservedObjectReport>(
            receive => r.Engine.RobotObservedObject += receive,
            receive => r.Engine.RobotObservedObject -= receive,
            report => report.Family == (uint)ObjectFamily.LightCube,
            () => Console.WriteLine("Waiting for Cozmo to see the cube. If needed, turn a marker toward him."),
            TimeSpan.FromSeconds(20), cancel.Token);
        if (cube is not null)
            RunCommand("turn-to-cube", t => r.Motion.QueueAiTurnToObjectAsync(cube.ObjectId, MathF.PI, t),
                a => (a.Admitted, a.EngineResult, a.Detail, a.OwnerRetired));
        else SkipCommand("turn-to-cube", "No fresh public light-cube observation.");
        PromptCommand("Tap the cube, then move it. Keep it in view briefly, then take it out of view. Press Enter when done.");
        RecordOperatorAnswer("cube-events", "Did you observe Cozmo respond to the cube being tapped or moved?");

        var trackFace = FindCommandFace(r, "track-face", "Start centered. Once tracking starts, move slowly in a small arc for 10 seconds, keeping your face fully in view.");
        if (trackFace is not null)
            RunCommand("track-face", t => r.Motion.QueueAiTrackFaceAsync(trackFace.FaceId, headOnly: false, cancellationToken: t),
                a => (a.Admitted, a.EngineResult, a.Detail, a.OwnerRetired), CommandsHw.FaceActionLimitMs, TimeSpan.FromSeconds(CommandsHw.TrackSeconds));
        else SkipCommand("track-face", "The head setup action did not succeed.");
        PromptCommand("Move out of the camera's view. Press Enter when ready.");
        RecordOperatorAnswer("face-events", "Did you observe Cozmo notice or recognize you?");
        RunCommand("animation", t => r.Motion.QueueAiAnimationAsync(CommandsHw.DemoAnimation, cancellationToken: t),
            a => (a.Admitted, a.EngineResult, a.Detail, a.OwnerRetired), 65000);
        RunCommand("sentence", t => r.SayEnglishSentenceAsync("Hello. It is nice to see you.", t),
            a => (a.Admitted, a.EngineResult, a.Detail, a.OwnerRetired), 65000);

        PromptCommand(M15CubeHw.GotoSetup + " Press Enter when the area is clear.");
        RunCommand("go-to-pose", t =>
        {
            if (r.Motion.GetAiBodyObservation().LastState is not { } pose)
                return Task.FromResult(new AiDriveActionResult(false, null, "No published engine pose."));
            float x = pose.Pose.X + 200 * MathF.Cos(pose.PoseAngleRad);
            float y = pose.Pose.Y + 200 * MathF.Sin(pose.PoseAngleRad);
            float angle = Radians.Rescale(pose.PoseAngleRad + MathF.PI / 2);
            return r.Motion.QueueAiGotoPoseAsync(x, y, angle, t);
        }, a => (a.Admitted, a.EngineResult, a.Detail, a.OwnerRetired));

        RunCommandEnrollment(r);
    }

    RobotObservedFaceReport? FindCommandFace(CozmoRobot r, string id, string instructions)
    {
        var setup = CommandsHw.FaceSetup with { Id = $"{id}-setup" };
        if (!RunCommand(setup, t => r.Motion.QueueAiHeadAngleAsync(CommandsHw.FaceSetupHeadAngle, t),
            a => (a.Admitted, a.EngineResult, a.Detail, a.OwnerRetired), askOperator: false))
            return null;

        bool turning = id == "turn-to-face";
        float bearing = 0;
        var face = CommandsHw.WaitForReport<RobotObservedFaceReport>(
            receive => r.Engine.RobotObservedFace += receive,
            receive => r.Engine.RobotObservedFace -= receive,
            report =>
            {
                if (!turning) return true;
                if (CommandsHw.FaceBearingDegrees(report, r.Motion.GetAiBodyObservation().LastState) is not { } angle ||
                    MathF.Abs(angle) < CommandsHw.MinTurnFaceBearingDegrees) return false;
                bearing = angle;
                return true;
            },
            () =>
            {
                Console.WriteLine($"{CommandsHw.FacePosition} {instructions} Ctrl+C stops the run.");
                Console.WriteLine(turning
                    ? $"Waiting for a face at least {CommandsHw.MinTurnFaceBearingDegrees} degrees to either side."
                    : "Waiting for Cozmo to see you.");
            },
            Timeout.InfiniteTimeSpan, cancel.Token);
        if (turning && face is not null)
        {
            Console.WriteLine($"Face bearing before turn: {bearing:+0.0;-0.0;0.0} degrees (positive is left).");
            CommandReport("turn-to-face-bearing", new { face.FaceId, face.Timestamp, Degrees = bearing });
        }
        return face;
    }

    void RunCommand<T>(string id, Func<CancellationToken, Task<T>> request,
        Func<T, (bool Admitted, uint? Result, string? Detail, bool Retired)> read,
        int limitMs = 45000, TimeSpan? stopAfter = null)
        => RunCommand(CommandsHw.Steps.Single(s => s.Id == id), request, read, limitMs, stopAfter);

    bool RunCommand<T>(CommandsHw.Step step, Func<CancellationToken, Task<T>> request,
        Func<T, (bool Admitted, uint? Result, string? Detail, bool Retired)> read,
        int limitMs = 45000, TimeSpan? stopAfter = null, bool askOperator = true)
    {
        string id = step.Id;
        Console.WriteLine($"\n[{id}] {step.Plan}");
        using var actionCancel = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
        Task<T>? task = null;
        uint? result = null;
        double begin = Now;
        JsonObject phase;
        try
        {
            phase = Phase(id, () =>
            {
                var queued = task = request(actionCancel.Token);
                if (stopAfter is { } interval) actionCancel.CancelAfter(interval);
                return (queued, () =>
                {
                    var value = read(queued.Result);
                    result = value.Result;
                    return value;
                });
            }, limitMs, waitForExternalActions: true);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            phase = CommandPhase(id, begin, e.Message);
            RecordHarnessError(id, phase, e.Message);
        }
        if (phase["harnessError"] is not null)
        {
            SaveCommandResult(step, phase, null, judge: false);
            Event("command-result", (JsonObject)phase.DeepClone());
            return false;
        }
        // A tracking result is judged only when the script reached its deliberate cancel interval.
        bool scheduledStopReached = stopAfter is null || (actionCancel.IsCancellationRequested && !cancel.IsCancellationRequested);
        if (stopAfter is { } duration)
        {
            phase["stopAfterMs"] = duration.TotalMilliseconds;
            phase["plannedStopReached"] = scheduledStopReached;
        }
        if (task is { IsCompleted: false })
        {
            actionCancel.Cancel();
            if (!Poll(() => task.IsCompleted, 3000))
            {
                phase["detail"] = "The action did not retire after cancellation.";
                SaveCommandResult(step, phase, null, judge: false);
                throw new InvalidOperationException($"{id}: the action did not retire after cancellation");
            }
            if (task.IsCompletedSuccessfully)
                phase["cleanupEngineResult"] = FormatCode(read(task.Result).Result);
        }
        bool judge = scheduledStopReached || result != step.Expected;
        if (askOperator) FinishCommand(step, phase, result, judge);
        else
        {
            SaveCommandResult(step, phase, result, judge);
            Event("command-result", (JsonObject)phase.DeepClone());
        }
        return phase["admitted"]?.GetValue<bool>() == true && result == EngineActionResult.Success &&
            phase["ownerRetired"]?.GetValue<bool>() != true;
    }

    void RunCommandEnrollment(CozmoRobot r)
    {
        string name = "Test Robin " + Guid.NewGuid().ToString("N")[..6];
        string newName = name.Replace("Robin", "Wren", StringComparison.Ordinal);
        PromptCommand($"Show your face for enrollment, preferably a face Cozmo does not already know. The temporary name is {name}. Press Enter when ready.");
        // Unity's enrollment request stages its inputs. The configured enrollment owner is selected in Meet.
        try
        {
            var enrolled = WaitCommandReport<FaceEnrollmentCompletedReport>("teach-face",
                h => r.Engine.FaceEnrollmentCompleted += h, h => r.Engine.FaceEnrollmentCompleted -= h, _ => true,
                () => { r.ActivateHighLevelActivity("MeetCozmoFindFaces"); r.SetFaceToEnroll(name); },
                120000, report => report.Result, onNoReport: r.CancelFaceEnrollment);
            if (enrolled is not { Result: 0, Id: > 0 } || enrolled.Name != name)
            {
                SkipCommand("rename-face", "No successful enrollment of this run's temporary face.");
                SkipCommand("erase-face", "No successful enrollment of this run's temporary face.");
                return;
            }

            WaitCommandReport<FaceRenamedReport>("rename-face",
                h => r.Engine.FaceRenamed += h, h => r.Engine.FaceRenamed -= h,
                e => e.Id == enrolled.Id && e.Name == newName,
                () => r.UpdateEnrolledFaceById(enrolled.Id, name, newName), 10000);
            WaitCommandReport<FaceErasedReport>("erase-face",
                h => r.Engine.FaceErased += h, h => r.Engine.FaceErased -= h, e => e.Id == enrolled.Id,
                () => r.EraseEnrolledFaceById(enrolled.Id), 10000);
        }
        finally { r.SetEnableFreeplayActivity(false); }
    }

    T? WaitCommandReport<T>(string id, Action<Action<T>> subscribe, Action<Action<T>> unsubscribe,
        Func<T, bool> matches, Action request, int limitMs, Func<T, uint>? read = null, Action? onNoReport = null) where T : class
    {
        var step = CommandsHw.Steps.Single(s => s.Id == id);
        Console.WriteLine($"\n[{id}] {step.Plan}");
        double begin = Now;
        if (!WaitForExternalActions(id))
        {
            var skipped = CommandPhase(id, begin, ExternalActionsWaitDetail);
            RecordHarnessError(id, skipped, ExternalActionsWaitDetail);
            SaveCommandResult(step, skipped, null, judge: false);
            Event("command-result", (JsonObject)skipped.DeepClone());
            return null;
        }

        T? report = null;
        string detail = $"No matching engine report arrived within {limitMs / 1000} seconds.";
        try
        {
            report = CommandsHw.WaitForReport(subscribe, unsubscribe, matches, () =>
            {
                request();
                Console.WriteLine($"    Request queued. Waiting up to {limitMs / 1000} seconds for the engine report.");
            }, TimeSpan.FromMilliseconds(limitMs), cancel.Token);
            if (report is not null) detail = "Engine report received.";
        }
        catch (Exception e) when (e is not OperationCanceledException) { detail = e.Message; }
        // End the enrollment window before the operator observation prompt can wait for an answer.
        if (report is null) onNoReport?.Invoke();
        var phase = CommandPhase(id, begin, detail);
        if (report is not null) phase["report"] = JsonSerializer.SerializeToNode(report, BundleJson);
        FinishCommand(step, phase, report is null || read is null ? null : read(report));
        return report;
    }

    void SkipCommand(string id, string reason)
    {
        Console.WriteLine($"\n[{id}] Skipped. {reason}");
        var step = CommandsHw.Steps.Single(s => s.Id == id);
        FinishCommand(step, CommandPhase(id, Now, reason), null);
    }

    JsonObject CommandPhase(string id, double begin, string detail) => new()
    { ["phase"] = id, ["startMs"] = begin, ["endMs"] = Now, ["detail"] = detail };

    void FinishCommand(CommandsHw.Step step, JsonObject phase, uint? actual, bool judge = true)
    {
        SaveCommandResult(step, phase, actual, judge);
        phase["observed"] = RecordOperatorAnswer(step.Id, step.Question);
        Event("command-result", (JsonObject)phase.DeepClone());
    }

    void SaveCommandResult(CommandsHw.Step step, JsonObject phase, uint? actual, bool judge)
    {
        phase["engineResult"] = FormatCode(actual);
        phase["expected"] = FormatCode(step.Expected);
        phase["source"] = new JsonObject { ["record"] = step.Record, ["address"] = step.Address };
        string verdict = phase["harnessError"] is not null ? "HARNESS ERROR"
            : !judge || actual is null || step.Expected is null ? "OBSERVATION"
            : actual == step.Expected ? "PASS" : "FAIL";
        phase["verdict"] = verdict;
        phases.Add(phase);
        Console.WriteLine($"    {verdict} {FormatCode(actual)} {phase["detail"]}");
    }

    void PromptCommand(string prompt)
    {
        Console.WriteLine("\n" + prompt);
        ReadOperatorInput();
    }

    bool RecordOperatorAnswer(string phase, string question)
    {
        bool answer = ReadYesNo(question);
        answers[phase] = answer;
        Event("operator-observation", new() { ["phase"] = phase, ["question"] = question, ["observed"] = answer });
        return answer;
    }

    static string? FormatCode(uint? code) => code is { } value ? $"0x{value:X8}" : null;
}
