using System.Diagnostics;
using System.Text.Json.Nodes;
using Cozmo.Robot;
using Cozmo.Robot.Animation;

namespace Cozmo.Conformance;

/// <summary>Host startup prerequisite shared by hardware harnesses. No robot behavior is substituted.</summary>
// fidelity: M2-021, M5-010
public static class HarnessStartup
{
    public const string BadNeutralAnimGroup = "AnimationStreamer.Constructor.BadNeutralAnimGroup";
    public static readonly string[] RecordIds = { "M2-021", "M5-010" };
    public enum Outcome { Ready, Failed, TimedOut, Cancelled }
    public sealed record Gate(Outcome Outcome, double WaitedMs, string? FailureReason, int BudgetMs)
    {
        public bool Ready => Outcome == Outcome.Ready;
        public string Describe() => Outcome switch
        {
            Outcome.Ready => $"engine readiness delivered after {WaitedMs:0} ms; connect callback completed",
            Outcome.Failed => "engine startup failed before connect: " + (FailureReason ?? "(no reason given)"),
            Outcome.TimedOut => $"engine readiness was not delivered within the {BudgetMs} ms host budget; no connect requested",
            _ => "run interrupted while waiting for engine readiness; no connect requested"
        };
        public JsonObject ToJson() => new()
        {
            ["outcome"] = Outcome.ToString(), ["ready"] = Ready, ["waitedMs"] = WaitedMs, ["budgetMs"] = BudgetMs,
            ["failureReason"] = FailureReason, ["description"] = Describe(),
            ["rule"] = "M2-021: request connection only after IsReadyDelivered; cancellation or startup failure takes priority. The 60-second budget is host coverage, not recovered engine timing."
        };
    }

    public static Gate ConnectAfterReadiness(CozmoRobot robot, Action connect, Func<bool>? cancelled = null, int budgetMs = M1M2Hw.StartupBudgetMs) =>
        ConnectAfterReadiness(() => robot.Engine.IsReadyDelivered, () => robot.Engine.StartupFailed, () => robot.Engine.StartupFailureReason,
            connect, cancelled, budgetMs);

    public static Gate ConnectAfterReadiness(Func<bool> readyDelivered, Func<bool> startupFailed, Func<string?> failureReason,
        Action connect, Func<bool>? cancelled = null, int budgetMs = M1M2Hw.StartupBudgetMs)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            if (cancelled?.Invoke() == true) return new(Outcome.Cancelled, sw.Elapsed.TotalMilliseconds, null, budgetMs);
            if (startupFailed()) return new(Outcome.Failed, sw.Elapsed.TotalMilliseconds, failureReason(), budgetMs);
            if (readyDelivered())
            {
                // Ready may have been delivered while cancellation/failure was published. Check again before the callback.
                if (cancelled?.Invoke() == true) return new(Outcome.Cancelled, sw.Elapsed.TotalMilliseconds, null, budgetMs);
                if (startupFailed()) return new(Outcome.Failed, sw.Elapsed.TotalMilliseconds, failureReason(), budgetMs);
                connect();
                return new(Outcome.Ready, sw.Elapsed.TotalMilliseconds, null, budgetMs);
            }
            if (sw.ElapsedMilliseconds >= budgetMs) return new(Outcome.TimedOut, sw.Elapsed.TotalMilliseconds, null, budgetMs);
            Thread.Sleep(5);
        }
    }

    public static bool HasCachedNeutralFace(CozmoRobot robot) =>
        robot.Animations.Scheduler.NeutralFaceAnimation?.Keyframes.OfType<FaceKeyframe>().Any() == true;

    public sealed record Report(JsonObject[] Errors, bool BadNeutral, Gate? Gate, bool? NeutralAvailable, string? Interrupted)
    {
        public bool PositiveVerdictAllowed => Gate is { Ready: true } && !BadNeutral && NeutralAvailable != false && Interrupted is null;
        public string? Reason => Interrupted ?? (Gate is null ? "startup ended before the readiness/connect gate completed"
            : !Gate.Ready ? Gate.Describe()
            : BadNeutral ? $"startup logged {BadNeutralAnimGroup}: this streamer's cached neutral/reset source was unavailable at construction (M5-010); later explicit clips can still render"
            : NeutralAvailable == false ? "the streamer's cached neutral clip or its first face keyframe is unavailable (M5-010)"
            : null);
        public string Describe() => (Gate?.Describe() ?? "readiness/connect gate not completed") + "; "
            + Errors.Length + " error-level engine log(s)"
            + (Errors.Length == 0 ? "" : ": " + string.Join("; ", Errors.Select(e => $"{e["name"]}: {e["text"]}")))
            + (PositiveVerdictAllowed ? "" : ". STARTUP INCOMPLETE: " + Reason);
        public JsonObject ToJson() => new()
        {
            ["gate"] = Gate?.ToJson(), ["records"] = new JsonArray(RecordIds.Select(id => (JsonNode)id).ToArray()),
            ["sources"] = new JsonArray(
                "M2-021: unity/scripts/csharp/StartupManager.cs:322-341; unity/scripts/csharp/ConnectionFlowController.cs:670-673; native ConnectToRobot 0x004ED020..0x004ED07C has no loader gate",
                "M5-010: AnimationStreamer constructor 0x0057A0D4..0x0057A282 reads/caches neutral/reset source once"),
            ["errorCount"] = Errors.Length, ["errors"] = new JsonArray(Errors.Select(e => e.DeepClone()).ToArray()),
            ["badNeutralAnimGroup"] = BadNeutral, ["cachedNeutralFaceAvailable"] = NeutralAvailable,
            ["positiveVerdictAllowed"] = PositiveVerdictAllowed, ["reason"] = Reason, ["summary"] = Describe(),
            ["window"] = "engine-log subscription registered in BeforeStart through connection setup completion, or startup interruption before cleanup; construction before BeforeStart is not captured",
            ["interrupted"] = Interrupted
        };
    }

    public static Report Analyse(IEnumerable<JsonObject> engineLogs, Gate? gate, bool? neutralAvailable = null, string? interrupted = null)
    {
        var errors = engineLogs.Where(e => e["level"]?.GetValue<string>() == "error")
            .Select(e => new JsonObject { ["name"] = e["name"]?.DeepClone(), ["channel"] = e["channel"]?.DeepClone(),
                ["text"] = e["text"]?.DeepClone(), ["atMs"] = e["atMs"]?.DeepClone() }).ToArray();
        return new(errors, errors.Any(e => e["name"]?.GetValue<string>() == BadNeutralAnimGroup), gate, neutralAvailable, interrupted);
    }

    /// <summary>Stop before any observation/test actions. Does not rewrite or hide already captured evidence.</summary>
    public static void RequireValid(Report report)
    {
        if (!report.PositiveVerdictAllowed) throw new InvalidOperationException("Startup incomplete: " + report.Reason);
    }
}
