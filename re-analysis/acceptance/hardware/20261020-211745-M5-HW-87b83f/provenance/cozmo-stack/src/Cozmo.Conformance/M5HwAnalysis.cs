using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;

namespace Cozmo.Conformance;

/// <summary>
/// Offline measurement of hardware captures, not a source-fidelity oracle. Counter snapshots are taken on
/// delivery, not atomically with the streamer's budget calculation. Keep those limitations in each result.
/// </summary>
public static class M5HwAnalysis
{
    public static JsonObject DrivingPhases(IEnumerable<M5Hw.WireMessage> messages, IEnumerable<JsonObject> events,
        uint driveTag, double fromMs, double toMs)
    {
        var rows = events.Where(e => e["atMs"]!.GetValue<double>() >= fromMs && e["atMs"]!.GetValue<double>() <= toMs).OrderBy(e => e["atMs"]!.GetValue<double>()).ToArray();
        var phaseLogs = rows.Where(e => e["name"]?.GetValue<string>() == "GetResponseForAnimationTrigger.Found").ToArray();
        string[] names = { ((AnimationTrigger)0x95).ToString(), ((AnimationTrigger)0x91).ToString(), ((AnimationTrigger)0x8D).ToString() };
        var phases = names.Select(name => phaseLogs.FirstOrDefault(e => e["text"]?.GetValue<string>().StartsWith(name + " -> ", StringComparison.Ordinal) == true)).ToArray();
        uint? ActionId(JsonObject? phase)
        {
            if (phase is null) return null;
            string text = phase["text"]!.GetValue<string>(); string group = text[(text.IndexOf(" -> ", StringComparison.Ordinal) + 4)..];
            string prefix = "Preconditions for PlayAnimation" + group + " [";
            var ids = new HashSet<uint>();
            foreach (var e in rows.Where(e => e["name"]?.GetValue<string>() == "IAction.Update.PreconditionsMet"))
            {
                string value = e["text"]?.GetValue<string>() ?? "";
                int end = value.IndexOf(']', prefix.Length <= value.Length ? prefix.Length : 0);
                if (value.StartsWith(prefix, StringComparison.Ordinal) && end > prefix.Length
                    && uint.TryParse(value.AsSpan(prefix.Length, end - prefix.Length), out uint id)) ids.Add(id);
            }
            return ids.Count == 1 ? ids.Single() : null;
        }
        var ids = phases.Select(ActionId).ToArray();
        var completions = rows.Where(e => e["kind"]?.GetValue<string>() == "action-completed").ToArray();
        JsonObject? Completion(uint? tag, int type)
        {
            if (tag is null) return null;
            var matches = completions.Where(e => e["Tag"]?.GetValue<uint>() == tag && e["ActionType"]?.GetValue<int>() == type).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }
        var start = Completion(ids[0], 26); var endAction = Completion(ids[2], 26); var drive = Completion(driveTag, 12);
        uint? startResult = start?["Result"]?.GetValue<uint>(), driveResult = drive?["Result"]?.GetValue<uint>();
        bool? loopRequired = startResult is { } sr ? sr == 0 : null;
        bool loopObserved = phases[1] is not null;
        double? startAt = phases[0]?["atMs"]?.GetValue<double>(), loopAt = phases[1]?["atMs"]?.GetValue<double>(), endAt = phases[2]?["atMs"]?.GetValue<double>();
        bool ordered = startAt is not null && endAt is not null && startAt <= endAt
            && (!loopObserved || (startAt <= loopAt && loopAt <= endAt));
        var endEvidence = endAt is { } endTime && ids[2] is { } endId && drive is not null
            ? DrivingEnd(messages, completions, endTime, drive["atMs"]!.GetValue<double>(), endId)
            : new JsonObject { ["endCompletionBeforeDrive"] = false, ["missing"] = "End action identity or drive completion unavailable/ambiguous." };
        bool endBefore = endEvidence["endCompletionBeforeDrive"]!.GetValue<bool>();
        bool endActionBefore = endAction is not null && drive is not null
            && endAction["atMs"]!.GetValue<double>() <= drive["atMs"]!.GetValue<double>();
        bool identity = startResult is not null && ids[2] is not null && driveResult is not null;
        string phaseVerdict = !identity ? "INCONCLUSIVE_PHASE_IDENTITY"
            : loopRequired == true && !loopObserved ? "INCONCLUSIVE_REQUIRED_LOOP_REQUEST"
            : loopRequired == false && loopObserved ? "OBSERVED_LOOP_AFTER_NONZERO_START"
            : !ordered ? "INCONCLUSIVE_PHASE_ORDER" : endAction is null ? "INCONCLUSIVE_END_COMPLETION" : !endActionBefore ? "OBSERVED_END_COMPLETION_ORDER_MISMATCH"
            : loopRequired == true ? "OBSERVED_SUCCESSFUL_START_LOOP_END_REQUESTS" : "OBSERVED_FAILED_START_END_CLEANUP";
        bool successRequests = driveResult == 0 && phaseVerdict == "OBSERVED_SUCCESSFUL_START_LOOP_END_REQUESTS";
        // Operator 2026-10-20 (DRIVING split): M5-063 is judged on whichever phase path the action results select,
        // whatever the drive result. Successful traversal belongs to the M12/M13 acceptance.
        bool m5PathObserved = phaseVerdict is "OBSERVED_SUCCESSFUL_START_LOOP_END_REQUESTS" or "OBSERVED_FAILED_START_END_CLEANUP";
        bool loopCovered = phaseVerdict == "OBSERVED_SUCCESSFUL_START_LOOP_END_REQUESTS";
        var missing = new JsonArray();
        if (phaseVerdict == "INCONCLUSIVE_PHASE_IDENTITY")
        {
            var absent = new List<string>();
            if (startResult is null) absent.Add("the Start animation action's result");
            if (ids[2] is null) absent.Add("an End animation action (no End driving animation was requested)");
            if (driveResult is null) absent.Add("the drive action's result");
            missing.Add("Could not identify " + string.Join(", ", absent) + ", so the driving-animation path cannot be judged.");
        }
        else if (phaseVerdict == "INCONCLUSIVE_REQUIRED_LOOP_REQUEST") missing.Add("The Start animation succeeded but no Loop animation request was seen.");
        else if (phaseVerdict == "INCONCLUSIVE_PHASE_ORDER") missing.Add("The Start, Loop and End animation requests were not seen in that order.");
        else if (phaseVerdict == "INCONCLUSIVE_END_COMPLETION") missing.Add("No completion was seen for the End animation action.");
        var stop = DrivingStop(rows, driveResult);
        return new JsonObject
        {
            ["verdict"] = m5PathObserved ? phaseVerdict : "INCONCLUSIVE",
            ["missing"] = missing.Count == 0 ? null : missing,
            ["explanation"] = stop["explanation"]?.DeepClone(), ["driveStop"] = stop,
            ["m5_063PathObserved"] = m5PathObserved,
            ["loopRequestCovered"] = loopCovered,
            ["loopCoverageOpen"] = !loopCovered,
            ["phaseVerdict"] = phaseVerdict, ["startActionId"] = ids[0], ["loopActionId"] = ids[1], ["endActionId"] = ids[2],
            ["startCompletion"] = start?.DeepClone(), ["startResult"] = startResult, ["loopRequired"] = loopRequired,
            ["loopRequestObserved"] = loopObserved, ["loopCoverageMissing"] = loopRequired is null ? null : loopRequired == true && !loopObserved,
            ["conditionalPhaseOrder"] = ordered, ["endEvidence"] = endEvidence, ["endCompletionBeforeDrive"] = endBefore,
            ["endActionCompletion"] = endAction?.DeepClone(), ["endActionCompletionBeforeDrive"] = endActionBefore,
            ["endResultObservation"] = endAction?["Result"]?.DeepClone(),
            ["endWireAssessment"] = "NOT_JUDGED: successful End result and matching firmware echo are separate observations; M5-063 0x4F0DC8..DD4 requires the matching End action completion irrespective of its result.",
            ["driveCompletion"] = drive?.DeepClone(), ["driveResult"] = driveResult,
            ["successfulStartLoopEndRequestCoverage"] = successRequests, ["successfulTraversalCoverage"] = false,
            ["traversalJudgement"] = "NOT_JUDGED: successful traversal belongs to the M12/M13 acceptance, which must observe a successful DriveToPose. Phase requests/API results alone do not certify physical traversal or M12/M13 source completeness. Failed traversal cannot satisfy successful traversal coverage.",
            ["driveOutcomeAssessment"] = driveResult == 0 ? "API_SUCCESS_REQUIRES_TRAVERSAL_REVIEW" : "UNASSESSED_M12_M13_OUTCOME",
            ["phaseTriggerLogs"] = new JsonArray(phaseLogs.Select(e => e.DeepClone()).ToArray()),
            ["boundaryDiagnostics"] = new JsonArray(rows.Where(e => e["kind"]?.GetValue<string>() == "stack-log"
                && e["line"]?.GetValue<string>().StartsWith("MISSING:", StringComparison.Ordinal) == true).Select(e => e.DeepClone()).ToArray()),
            ["identityNote"] = "Action IDs require an unambiguous existing PreconditionsMet log for the selected group. Missing/ambiguous logs do not imply a source defect. Host completion delivery may follow the synchronous Loop request; matching ID/result, not delivery timestamp, determines its requirement.",
            ["verdictRule"] = "M5-063 (0x4F0D80..D8C, 0x4F0E14..2A) requires matching Start ID and result0 before requesting the default Loop. A failed Start permits Start->End cleanup. A matching End completion sets state4 regardless of result (0x4F0DC8..DD4); successful End/robot echo coverage is separate from the completion-order gate. "
                + "Operator 2026-10-20: the check's verdict covers M5-063 only and is OBSERVED on either selected path (successful Start->Loop->End requests or failed Start->End cleanup), whatever the drive result; contradictions stay non-OBSERVED. "
                + "loopRequestCovered records the Loop request; a failed-path run leaves Loop coverage open for a later run. Successful traversal belongs to the M12/M13 acceptance: successfulTraversalCoverage stays false and the traversal is NOT_JUDGED here. No planner/action correction or acceptance follows."
        };
    }

    /// <summary>ActionResult CANCELLED_WHILE_RUNNING: ActionList::Cancel sets 0x02000000 (0x00540A3C..0x00540A40).</summary>
    public const uint CancelledWhileRunning = 0x02000000;

    /// <summary>
    /// Explains a cancelled drive whose window holds a robot stop: the engine's RobotStopped handler cancels the
    /// action list (0x0053542E..0x00535480; research/20261020-m5-drive-cliff-stop.md). An explanation, not a verdict.
    /// </summary>
    public static JsonObject DrivingStop(IEnumerable<JsonObject> windowEvents, uint? driveResult)
    {
        var rows = windowEvents.ToArray();
        bool Log(string name, string? text = null) => rows.Any(e => e["kind"]?.GetValue<string>() == "engine-log"
            && e["name"]?.GetValue<string>() == name && (text is null || e["text"]?.GetValue<string>().Contains(text, StringComparison.Ordinal) == true));
        bool robotStopped = Log("RobotImplMessaging.HandleRobotStopped");
        bool pathInterrupted = Log("PathComponent.ReceivedPathEvent", "PATH_INTERRUPTED");
        bool cliffTrace = rows.Any(e => e["kind"]?.GetValue<string>() == "stack-log"
            && e["line"]?.GetValue<string>().Contains("StoppingDueToCliff", StringComparison.Ordinal) == true);
        bool cancelled = driveResult == CancelledWhileRunning;
        return new JsonObject
        {
            ["driveCancelled"] = cancelled, ["robotStoppedLog"] = robotStopped, ["pathInterruptedLog"] = pathInterrupted,
            ["stoppingDueToCliffTrace"] = cliffTrace,
            // Only RobotStopped has a cited cancel path; RobotStopped's field meaning is unknown, so the cause is named only with the cliff trace.
            ["explanation"] = !(cancelled && robotStopped) ? null : cliffTrace
                ? "The robot reported a cliff stop (ProxSensors.UpdateCliff.StoppingDueToCliff) and RobotStopped; the engine's RobotStopped handler cancels the action list (0x0053542E..0x00535480), so the drive ended CANCELLED_WHILE_RUNNING. The cancel is engine behaviour; whether the cliff was real or false (e.g. the on-charger threshold, M13-017) needs review (research/20261020-m5-drive-cliff-stop.md)."
                : "The robot sent RobotStopped (cause not logged); the engine's RobotStopped handler cancels the action list (0x0053542E..0x00535480), so the drive ended CANCELLED_WHILE_RUNNING. Why the robot stopped needs review."
        };
    }

    // Optional independently observed enqueue/generation origin. Raw first-send time is insufficient.
    public sealed record TeardownOrigin(int FrameLine, int SubmessageOrdinal, string Origin, double OriginAtMs);

    public static JsonObject Teardown(IEnumerable<M5Hw.WireMessage> messages, IEnumerable<JsonObject> events,
        double beginMs, double endMs, bool completed, IReadOnlyList<TeardownOrigin>? origins = null)
    {
        var raw = messages.Where(m => m.Payload.Length > 0).OrderBy(m => m.AtMs).ThenBy(m => m.FrameLine).ThenBy(m => m.SubmessageOrdinal).ToArray();
        var unique = OrderedUnique(raw).ToArray();
        var first = new HashSet<M5Hw.WireMessage>(unique, ReferenceEqualityComparer.Instance);
        var during = unique.Where(m => !m.IsRetransmission && m.Outbound && m.AtMs >= beginMs && m.AtMs <= endMs).ToArray();
        var resends = raw.Where(m => m.Outbound && m.AtMs >= beginMs && m.AtMs <= endMs
            && (m.IsRetransmission || !first.Contains(m))).ToArray();
        var markers = events.Where(e => e["kind"]?.GetValue<string>() == "engine-log"
            && e["name"]?.GetValue<string>() == "AnimationStreamer.ClearSendBuffer"
            && e["text"]?.GetValue<string>()?.StartsWith("Clearing unsent ", StringComparison.Ordinal) == true
            && e["atMs"]!.GetValue<double>() >= beginMs && e["atMs"]!.GetValue<double>() <= endMs).ToArray();
        var candidates = new JsonArray(); int unknown = 0, flush = 0, queued = 0;
        foreach (var m in during.Where(m => m.Payload[0] is >= 0x8E and <= 0x9B))
        {
            var evidence = origins?.Where(o => o.FrameLine == m.FrameLine && o.SubmessageOrdinal == m.SubmessageOrdinal).ToArray();
            var origin = evidence?.Length == 1 ? evidence[0] : null;
            string classification;
            if (origin is { Origin: "queued-before-dispose" } && origin.OriginAtMs <= beginMs)
            { classification = "PREQUEUED_FIRST_SEND"; queued++; }
            else if (origin is { Origin: "streamer-final-flush" } && origin.OriginAtMs >= beginMs && origin.OriginAtMs <= endMs)
            { classification = "OBSERVED_STREAMER_FINAL_FLUSH"; flush++; }
            else { classification = "UNKNOWN_ENQUEUE_OR_GENERATION_ORIGIN"; unknown++; }
            candidates.Add(new JsonObject { ["wire"] = WireEvidence(new[] { m }), ["classification"] = classification,
                ["origin"] = origin?.Origin, ["originAtMs"] = origin?.OriginAtMs });
        }
        // Every INCONCLUSIVE condition is named, so the summary always prints why.
        var missing = new JsonArray();
        if (!completed) missing.Add("Dispose did not complete inside the harness limit.");
        if (endMs <= beginMs) missing.Add("The Dispose interval is empty (end not after begin).");
        if (unknown > 0) missing.Add($"{unknown} first send(s) during Dispose have no enqueue origin.");
        if (markers.Length == 0) missing.Add("No AnimationStreamer.ClearSendBuffer discard log inside the Dispose interval.");
        return new JsonObject
        {
            ["verdict"] = flush > 0 ? "OBSERVED_STREAMER_FINAL_FLUSH" : !completed || endMs <= beginMs
                || unknown > 0 || markers.Length == 0 ? "INCONCLUSIVE" : "OBSERVED_DISPOSE_DISCARD_INTERVAL",
            ["missing"] = flush > 0 || missing.Count == 0 ? null : missing,
            ["disposeBeginMs"] = beginMs, ["disposeEndMs"] = endMs, ["disposeCompleted"] = completed,
            ["retransmissionsDuringDispose"] = WireEvidence(resends), ["newOutboundDuringDispose"] = WireEvidence(during),
            ["abortAllPrefixCandidates"] = WireEvidence(during.Where(m => m.Payload[0] == 0x8D)),
            ["animationDataCandidates"] = candidates, ["unknownOrigins"] = unknown,
            ["knownPrequeuedFirstSends"] = queued, ["observedStreamerFlushes"] = flush,
            ["streamerDiscardMarkers"] = new JsonArray(markers.Select(e => e.DeepClone()).ToArray()),
            ["beforeWindow"] = WireEvidence(raw.Where(m => m.AtMs >= beginMs - 1000 && m.AtMs < beginMs)),
            ["afterWindow"] = WireEvidence(raw.Where(m => m.AtMs > endMs && m.AtMs <= endMs + 1000)),
            ["queueOriginNote"] = "Retransmissions are identified from full raw history. A first wire send during Dispose may have been queued before it; its origin comes only from independent evidence (the harness joins the B-M5-32 send-gate observations to the wire, TeardownOrigins). Unknown origin is inconclusive, never inferred as a destructor flush.",
            ["verdictRule"] = "M4-026 AbortAll precedes M5-022/037 streamer teardown. Abort is retained as prefix traffic, not classified as a final flush by tag alone. ClearSendBuffer logs/discards pending messages without sending. Require a completed Dispose interval and discard marker; retain before/after windows. Post-close silence alone proves neither discard nor destructor fidelity. Cache assertions and all other teardown scope remain separate."
        };
    }

    /// <summary>
    /// Enqueue origins for <see cref="Teardown"/> from the run's own send-gate observations (B-M5-32). The gate
    /// observations made from one join margin before Dispose begins through its end are joined to the wire by
    /// <see cref="JoinSends"/>; a joined message whose gate observation is stamped at or before dispose-begin was handed
    /// to the reliable transport before Dispose began, so it is "queued-before-dispose" however late its first wire
    /// send (M1-006 pacing; research/20261020-m5-teardown-sends.md). A gate observation inside the interval gets no
    /// origin and stays unknown. When the join cannot be made, no origin is given and the reason is in the evidence.
    /// </summary>
    public static IReadOnlyList<TeardownOrigin> TeardownOrigins(IEnumerable<M5Hw.WireMessage> messages,
        IReadOnlyList<Diagnostic> diagnostics, double beginMs, double endMs, out JsonObject evidence)
    {
        var join = JoinSends(messages, diagnostics, beginMs, endMs);
        var origins = new List<TeardownOrigin>();
        if (join.Problem is null)
            foreach (var (key, gateAt) in join.GateAtMsByWire)
                if (key.Outbound && gateAt <= beginMs)
                    origins.Add(new TeardownOrigin(key.Line, key.Ordinal, "queued-before-dispose", gateAt));
        evidence = new JsonObject
        {
            ["sendJoin"] = join.Evidence.DeepClone(), ["queuedBeforeDisposeOrigins"] = origins.Count,
            ["originRule"] = "A wire message joined (by stream order, B-M5-32) to a successful send-gate observation stamped at or before "
                + "dispose-begin was queued before Dispose; later first sends are reliable-layer pacing (M1-006). Gate observations "
                + "inside the interval give no origin. No join, no origins."
        };
        return origins;
    }

    // ---------------------------------------------------------------- B-M5-32 diagnostic hooks

    /// <summary>One read-only engine observation from <see cref="AnimationScheduler.Diagnostic"/>, stamped on the
    /// harness clock at its observed point (the hook's own Stopwatch timestamp, taken before a send).</summary>
    public sealed record Diagnostic(double AtMs, AnimationDiagnostic Observation);

    /// <summary>The EngineToRobot tags the animation stream sends (IAnimationSink C2..C19, A20, A21): 0x8E, 0x8F,
    /// 0x91..0x95, 0x97..0x9B.</summary>
    public static readonly byte[] StreamMessageIds = { 0x8E, 0x8F, 0x91, 0x92, 0x93, 0x94, 0x95, 0x97, 0x98, 0x99, 0x9A, 0x9B };

    /// <summary>Harness choice: a joined wire message must be captured within this long after its gate observation, and
    /// gate observations up to this long before a window are included so in-flight sends can be joined.</summary>
    public const double JoinMarginMs = 1000;

    /// <summary>The join of successful send-gate observations to the captured outbound wire.</summary>
    public sealed class SendJoin
    {
        public string? Problem { get; init; }
        public IReadOnlyDictionary<long, M5Hw.WireMessage> BySequence { get; init; } = new Dictionary<long, M5Hw.WireMessage>();
        public IReadOnlySet<(bool Outbound, int Line, int Ordinal)> JoinedWire { get; init; } = new HashSet<(bool, int, int)>();
        /// <summary>The harness-clock time of the gate observation each joined wire message was joined to.</summary>
        public IReadOnlyDictionary<(bool Outbound, int Line, int Ordinal), double> GateAtMsByWire { get; init; } = new Dictionary<(bool, int, int), double>();
        public JsonObject Evidence { get; init; } = new();
        public static (bool Outbound, int Line, int Ordinal) Key(M5Hw.WireMessage m) => (m.Outbound, m.FrameLine, m.SubmessageOrdinal);
        public bool IsJoined(M5Hw.WireMessage m) => JoinedWire.Contains(Key(m));
    }

    /// <summary>
    /// Joins the successful send-gate observations (C14 gate and the direct End) made in [fromMs - margin, toMs] to
    /// the outbound stream messages of the capture, by stream order: the gate's send order must equal one contiguous
    /// run of the deduplicated outbound stream-tag messages, matching tag and EngineToRobot size, each captured no
    /// earlier than its gate observation and within the margin after it. Exactly one such run must exist; none or
    /// several leave the whole join unmade, with the reason. Time only bounds candidates; order and content decide.
    /// </summary>
    public static SendJoin JoinSends(IEnumerable<M5Hw.WireMessage> messages, IReadOnlyList<Diagnostic> diagnostics,
        double fromMs, double toMs)
    {
        var sent = diagnostics.Where(d => d.Observation is SendGateObservation { Sent: true }
                && d.AtMs >= fromMs - JoinMarginMs && d.AtMs <= toMs)
            .Select(d => (d.AtMs, Gate: (SendGateObservation)d.Observation)).ToArray();
        var wire = OrderedUnique(messages).Where(m => m.Outbound && m.Payload.Length > 0
            && Array.IndexOf(StreamMessageIds, m.Payload[0]) >= 0).ToArray();
        string? problem = null; int offset = -1, candidates = 0;
        if (sent.Length == 0) problem = "No successful send-gate observation in the window; nothing to join.";
        else if (sent.Any(s => s.Gate.MessageId is null)) problem = "A successful send-gate observation names no catalogued message tag; its wire identity is unknown.";
        else
        {
            for (int o = 0; o + sent.Length <= wire.Length; o++)
            {
                if (wire[o].AtMs < sent[0].AtMs) continue;
                if (wire[o].AtMs > sent[0].AtMs + JoinMarginMs) break;
                bool match = true;
                for (int k = 0; k < sent.Length && match; k++)
                {
                    var w = wire[o + k]; var (at, gate) = sent[k];
                    match = w.Payload[0] == gate.MessageId && w.Payload.Length == gate.Size
                        && w.AtMs >= at && w.AtMs <= at + JoinMarginMs;
                }
                if (match) { candidates++; offset = o; }
            }
            if (candidates == 0) problem = "No contiguous run of captured outbound stream messages matches the gate's send order (tag, size, capture after gate); another sender or a lost/unsent message breaks the join.";
            else if (candidates > 1) problem = $"{candidates} runs of captured stream messages match the gate's send order; the join is ambiguous.";
        }
        var bySequence = new Dictionary<long, M5Hw.WireMessage>();
        var joined = new HashSet<(bool, int, int)>();
        var gateAt = new Dictionary<(bool, int, int), double>();
        if (problem is null)
            for (int k = 0; k < sent.Length; k++)
            {
                joined.Add(SendJoin.Key(wire[offset + k]));
                gateAt[SendJoin.Key(wire[offset + k])] = sent[k].AtMs;
                if (sent[k].Gate.BufferSequence != 0) bySequence[sent[k].Gate.BufferSequence] = wire[offset + k];
            }
        int unjoinedInWindow = wire.Count(m => m.AtMs >= fromMs && m.AtMs <= toMs && !joined.Contains(SendJoin.Key(m)));
        return new SendJoin
        {
            Problem = problem, BySequence = bySequence, JoinedWire = joined, GateAtMsByWire = gateAt,
            Evidence = new JsonObject
            {
                ["joined"] = problem is null, ["problem"] = problem, ["sentGateObservations"] = sent.Length,
                ["matchingRuns"] = candidates, ["unjoinedStreamWireInWindow"] = unjoinedInWindow,
                ["firstJoinedWire"] = problem is null ? WireEvidence(new[] { wire[offset] }) : null,
                ["lastJoinedWire"] = problem is null ? WireEvidence(new[] { wire[offset + sent.Length - 1] }) : null,
                ["marginMs"] = JoinMarginMs,
                ["joinRule"] = "B-M5-32: successful C14 gate sends and direct End (0x57BF96..BFCC, 0x57C464..496) in send order "
                    + "must equal exactly one contiguous run of deduplicated outbound stream-tag messages (tag, EngineToRobot size), "
                    + "each captured at or after its gate observation and within the margin. Zero or several runs: unjoined. "
                    + "The margin is a harness choice; time bounds candidates, order and content decide."
            }
        };
    }

    /// <summary>
    /// M5-029 per-frame face activity from the StreamLayers hook, keyed by the wire identity of each frame's audio
    /// message through the send join. A frame whose audio (or, with its face flag set, whose face) is not joined stays
    /// unjoined and is not guessed.
    /// </summary>
    public static (IReadOnlyList<FaceActivity>? Activity, JsonArray Rows, string? Problem) LayerActivity(SendJoin join,
        IReadOnlyList<Diagnostic> diagnostics, double fromMs, double toMs)
    {
        var frames = diagnostics.Where(d => d.Observation is LayerFrameObservation && d.AtMs >= fromMs && d.AtMs <= toMs).ToArray();
        var rows = new JsonArray();
        if (join.Problem is not null) return (null, rows, "Send join unavailable: " + join.Problem);
        if (frames.Length == 0) return (null, rows, "No StreamLayers frame observation in the window.");
        var activity = new List<FaceActivity>(); int unjoined = 0;
        foreach (var d in frames)
        {
            var f = (LayerFrameObservation)d.Observation;
            join.BySequence.TryGetValue(f.AudioBufferSequence, out var audio);
            M5Hw.WireMessage? face = null;
            if (f.FaceBufferSequence is { } s) join.BySequence.TryGetValue(s, out face);
            string? reason = audio is null ? "The frame's audio message is not joined to the wire."
                : f.FaceFlag && f.FaceBufferSequence is null ? "Face flag set but no face was buffered (BufferFaceToSend produced no message)."
                : f.FaceFlag && face is null ? "The frame's buffered face is not joined to the wire."
                : !f.FaceFlag && f.FaceBufferSequence is not null ? "A face was buffered with the face flag clear." : null;
            if (reason is null) activity.Add(new FaceActivity(audio!.FrameLine, audio.SubmessageOrdinal, f.FaceFlag));
            else unjoined++;
            rows.Add(new JsonObject { ["atMs"] = d.AtMs, ["frameNumber"] = f.FrameNumber, ["streamTimeMs"] = f.StreamTimeMs,
                ["faceFlag"] = f.FaceFlag, ["backpackFlag"] = f.BackpackFlag,
                ["audioWire"] = audio is null ? null : WireEvidence(new[] { audio }),
                ["faceWire"] = face is null ? null : WireEvidence(new[] { face }), ["unjoinedReason"] = reason });
        }
        return (activity, rows, unjoined > 0 ? $"{unjoined} StreamLayers frame(s) could not be joined to the wire." : null);
    }

    /// <summary>M5-028 KeepFaceAlive calls from the hook, in call order, within the window.</summary>
    public static IReadOnlyList<BlinkCall> BlinkCalls(IReadOnlyList<Diagnostic> diagnostics, double fromMs, double toMs)
        => diagnostics.Where(d => d.Observation is KeepFaceAliveObservation && d.AtMs >= fromMs && d.AtMs <= toMs)
            .Select(d => { var k = (KeepFaceAliveObservation)d.Observation;
                return new BlinkCall(k.CallNumber, d.AtMs, k.BlinkTimerBeforeMs, k.BlinkTimerAfterMs, k.BlinkGenerated); }).ToArray();

    /// <summary>M3-012 gate observations within the window, as coherent admission samples.</summary>
    public static IReadOnlyList<BudgetAdmission> Admissions(IReadOnlyList<Diagnostic> diagnostics, double fromMs, double toMs)
        => diagnostics.Where(d => d.Observation is SendGateObservation && d.AtMs >= fromMs && d.AtMs <= toMs)
            .Select(d => { var g = (SendGateObservation)d.Observation;
                return new BudgetAdmission(d.AtMs, g.BudgetEpoch, g.MessageId ?? 0, g.Size, g.RemainingBytes,
                    g.RemainingAudioFrames, g.Sent, Coherent: true, CountsAsAudioFrame: g.CountsAsAudioFrame,
                    DirectBypass: g.DirectBypass, BufferSequence: g.BufferSequence, Admitted: g.DirectBypass ? null : g.Admitted); }).ToArray();

    /// <summary>Every observed budget refresh (C9), whatever its time: an epoch's rows may follow its refresh.</summary>
    public static IReadOnlyList<BudgetRefresh> Refreshes(IReadOnlyList<Diagnostic> diagnostics)
        => diagnostics.Where(d => d.Observation is BudgetRefreshObservation)
            .Select(d => { var r = (BudgetRefreshObservation)d.Observation;
                return new BudgetRefresh(d.AtMs, r.BudgetEpoch, r.BytesPlayed, r.FramesPlayed, r.BytesStreamed,
                    r.FramesStreamed, r.BytesToSend, r.FramesToSend); }).ToArray();

    public sealed record BudgetRefresh(double AtMs, long Epoch, int BytesPlayed, int FramesPlayed, int BytesStreamed,
        int FramesStreamed, int BytesToSend, int FramesToSend);

    // Coherent means actual remaining allowances observed at the send gate (the B-M5-32 hook), not a
    // subtraction of an old delivered F1 payload from later stream totals.
    public sealed record BudgetAdmission(double AtMs, long Epoch, byte MessageId, int MessageBytes,
        int RemainingBytes, int RemainingAudioFrames, bool Sent, bool Coherent,
        bool? CountsAsAudioFrame = null, bool DirectBypass = false, long BufferSequence = 0, bool? Admitted = null);

    // prefix is the complete observed gate history before the judged window, not a join-margin slice.
    // Null means that history is unavailable; an empty list explicitly means no earlier offers were observed.
    public static JsonObject BudgetSamples(IReadOnlyList<BudgetAdmission>? samples, IReadOnlyList<BudgetRefresh>? refreshes = null,
        IReadOnlyList<BudgetAdmission>? prefix = null)
    {
        var rows = new JsonArray(); int violations = 0, coherent = 0, formulaMismatches = 0, accountingMismatches = 0, unverified = 0;
        // C9 (0x57C6F6..7AC): bytes = played - streamed + 8192, negative -> 0, then min(30000); frames = max(0, played - streamed + 14).
        var byEpoch = new Dictionary<long, BudgetRefresh>();
        var refreshRows = new JsonArray();
        var relevantEpochs = (samples ?? Array.Empty<BudgetAdmission>()).Where(s => s.Coherent).Select(s => s.Epoch).ToHashSet();
        foreach (var r in (refreshes ?? Array.Empty<BudgetRefresh>()).Where(r => relevantEpochs.Contains(r.Epoch)))
        {
            byEpoch[r.Epoch] = r;
            int bytes = unchecked(r.BytesPlayed - r.BytesStreamed + 8192); if (bytes < 0) bytes = 0;
            int expectedBytes = Math.Min(bytes, 30000), expectedFrames = Math.Max(unchecked(r.FramesPlayed - r.FramesStreamed + 14), 0);
            bool mismatch = r.BytesToSend != expectedBytes || r.FramesToSend != expectedFrames;
            if (mismatch) { formulaMismatches++;
                refreshRows.Add(new JsonObject { ["atMs"] = r.AtMs, ["epoch"] = r.Epoch, ["bytesToSend"] = r.BytesToSend,
                    ["expectedBytes"] = expectedBytes, ["framesToSend"] = r.FramesToSend, ["expectedFrames"] = expectedFrames }); }
        }
        // C14 (0x57BFB0..BFCC): only a successful buffered send subtracts its size and, for audio, one frame.
        var spent = new Dictionary<long, (int Bytes, int Frames)>();
        var prefixProblems = new Dictionary<long, string>();
        var anchoredEpochs = new HashSet<long>();
        foreach (var sample in prefix ?? Array.Empty<BudgetAdmission>())
        {
            if (!relevantEpochs.Contains(sample.Epoch) || !byEpoch.TryGetValue(sample.Epoch, out var refresh)) continue;
            var used = spent.GetValueOrDefault(sample.Epoch);
            if (!sample.Coherent || sample.AtMs < refresh.AtMs)
                prefixProblems[sample.Epoch] = "The accounting prefix contains an incoherent offer or an offer before its refresh.";
            else if (sample.RemainingBytes != unchecked(refresh.BytesToSend - used.Bytes)
                || sample.RemainingAudioFrames != refresh.FramesToSend - used.Frames)
                prefixProblems[sample.Epoch] = "The earlier gate history does not establish the accounting prefix from its refresh.";
            if (!prefixProblems.ContainsKey(sample.Epoch)) anchoredEpochs.Add(sample.Epoch);
            if (sample.Sent && !sample.DirectBypass && sample.MessageId != 0x9A)
                spent[sample.Epoch] = (unchecked(used.Bytes + sample.MessageBytes), used.Frames
                    + (sample.CountsAsAudioFrame ?? (sample.MessageId is 0x8E or 0x8F) ? 1 : 0));
        }
        foreach (var sample in samples ?? Array.Empty<BudgetAdmission>())
        {
            bool directEnd = sample.DirectBypass || sample.MessageId == 0x9A;
            int frameCost = sample.CountsAsAudioFrame is { } audio ? (audio ? 1 : 0) : sample.MessageId is 0x8E or 0x8F ? 1 : 0;
            bool violation = sample.Coherent && sample.Sent && !directEnd
                && (sample.MessageBytes > sample.RemainingBytes || frameCost > sample.RemainingAudioFrames);
            if (sample.Coherent) coherent++;
            if (violation) violations++;
            bool? accountingMatches = null; string? accountingProblem = null;
            if (!sample.Coherent) prefixProblems[sample.Epoch] = "An incoherent offer prevents establishing subsequent accounting.";
            if (sample.Coherent && refreshes is not null)
            {
                if (sample.Epoch == 0 || !byEpoch.TryGetValue(sample.Epoch, out var refresh))
                    accountingProblem = "The budget epoch refresh was not observed.";
                else if (prefix is null)
                    accountingProblem = "The accounting prefix from the budget refresh through the window is unavailable.";
                else if (prefixProblems.TryGetValue(sample.Epoch, out var problem)) accountingProblem = problem;
                else if (sample.AtMs < refresh.AtMs) accountingProblem = "The gate observation precedes its budget refresh.";
                else if (!anchoredEpochs.Contains(sample.Epoch) && (sample.RemainingBytes != refresh.BytesToSend
                    || sample.RemainingAudioFrames != refresh.FramesToSend))
                {
                    accountingProblem = "The accounting prefix is unavailable: the first observed offer already has reduced allowances.";
                    prefixProblems[sample.Epoch] = accountingProblem;
                }
                else
                {
                    anchoredEpochs.Add(sample.Epoch);
                    var used = spent.GetValueOrDefault(sample.Epoch);
                    accountingMatches = sample.RemainingBytes == unchecked(refresh.BytesToSend - used.Bytes)
                        && sample.RemainingAudioFrames == refresh.FramesToSend - used.Frames;
                    if (accountingMatches == false) accountingMismatches++;
                    if (sample.Sent && !directEnd) spent[sample.Epoch] = (unchecked(used.Bytes + sample.MessageBytes), used.Frames + frameCost);
                }
            }
            if (accountingProblem is not null) unverified++;
            rows.Add(new JsonObject { ["atMs"] = sample.AtMs, ["epoch"] = sample.Epoch,
                ["messageId"] = sample.MessageId, ["messageBytes"] = sample.MessageBytes,
                ["remainingBytesAtGate"] = sample.RemainingBytes, ["remainingFramesAtGate"] = sample.RemainingAudioFrames,
                ["admitted"] = sample.Admitted, ["sent"] = sample.Sent, ["coherentGateObservation"] = sample.Coherent,
                ["bufferSequence"] = sample.BufferSequence, ["directEndBypass"] = directEnd,
                ["observedViolation"] = violation, ["allowanceAccountingMatches"] = accountingMatches,
                ["accountingUnverifiedReason"] = accountingProblem });
        }
        return new JsonObject { ["coherentSampleVerdict"] = violations > 0 ? "OBSERVED_COHERENT_ADMISSION_VIOLATION"
                : formulaMismatches + accountingMismatches > 0 ? "OBSERVED_ADMISSION_ACCOUNTING_MISMATCH"
                : unverified > 0 ? "INCONCLUSIVE" : coherent > 0 ? "OBSERVED_COHERENT_SAMPLES_WITHIN_GATE" : "NOT_JUDGED",
            ["coherentSamples"] = coherent, ["observedViolations"] = violations,
            ["allowanceFormulaMismatches"] = formulaMismatches, ["allowanceAccountingMismatches"] = accountingMismatches,
            ["accountingUnverifiedSamples"] = refreshes is null ? null : unverified,
            ["refreshesChecked"] = refreshes is null ? null : byEpoch.Count, ["formulaMismatchRows"] = refreshRows, ["samples"] = rows,
            ["budgetCompliance"] = "NOT_JUDGED",
            ["coverageNote"] = "Individual coherent samples do not certify all sends. Buffered admission uses actual remaining byte/frame allowances; direct End bypasses it but still adds one frame and its bytes on success. Only epochs used in the judged window are checked. Complete earlier gate history from each refresh is required before checking the window remainder against C14; missing prefix evidence is unverified, never assumed zero." };
    }

    // Actual call/deadline observations only: the B-M5-32 KeepFaceAlive hook (BlinkCalls) or a named unit substitute.
    public sealed record BlinkCall(long CallNumber, double AtMs, int BeforeMs, int AfterMs, bool Generated);

    public static JsonObject Blinks(IEnumerable<M5Hw.WireMessage> messages, double fromMs, double toMs,
        float minReadback, float maxReadback, JsonObject observation, IReadOnlyList<BlinkCall>? calls = null)
    {
        var wire = OrderedUnique(messages).Where(m => m.AtMs >= fromMs && m.AtMs <= toMs && m.Payload.Length > 0).ToArray();
        bool IsState(M5Hw.WireMessage m) => !m.Outbound && m.Payload[0] == (byte)RobotMessageId.AnimState && m.Payload.Length == 16;
        var faces = new JsonArray();
        for (int index = 0; index < wire.Length; index++)
        {
            var face = wire[index];
            if (!face.Outbound || face.Payload[0] != (byte)RobotMessageId.AnimFaceImage) continue;
            var row = new JsonObject { ["wire"] = WireEvidence(new[] { face }) };
            bool valid = face.Payload.Length >= 3 && (face.Payload[1] | face.Payload[2] << 8) == face.Payload.Length - 3;
            row["countPrefixValid"] = valid;
            if (valid) row["engineModelLitPixels"] = Cozmo.Robot.FaceBitmapCodec.DecodeCanvas(face.Payload.AsSpan(3)).Count(x => x != 0);
            var before = wire.Take(index).LastOrDefault(IsState);
            var after = wire.Skip(index + 1).FirstOrDefault(IsState);
            row["precedingWireAnimationState"] = before is null ? null : WireEvidence(new[] { before });
            row["followingWireAnimationState"] = after is null ? null : WireEvidence(new[] { after });
            faces.Add(row);
        }
        var problems = new JsonArray(); var captureProblems = new JsonArray(); var ticks = new JsonArray(); int generated = 0;
        BlinkCall? prior = null;
        foreach (var call in calls ?? Array.Empty<BlinkCall>())
        {
            bool inWindow = call.AtMs >= fromMs && call.AtMs <= toMs;
            if (!inWindow || (prior is not null && call.AtMs < prior.AtMs))
                captureProblems.Add("Call timestamps do not belong to this ordered observation window.");
            int remaining = unchecked(call.BeforeMs - 60);
            bool due = remaining <= 0;
            if (inWindow && (call.Generated != due || (!due && call.AfterMs != remaining)
                || (due && (call.AfterMs < 44000 || call.AfterMs > 45000)))) problems.Add("Call/deadline transition contradicts the checked 60-per-call branch.");
            if (prior is not null && (call.CallNumber != prior.CallNumber + 1 || call.BeforeMs != prior.AfterMs)) captureProblems.Add("Incomplete or discontinuous KeepFaceAlive call sequence.");
            if (call.Generated) generated++;
            ticks.Add(new JsonObject { ["callNumber"] = call.CallNumber, ["atMs"] = call.AtMs,
                ["beforeMs"] = call.BeforeMs, ["afterMs"] = call.AfterMs, ["generationObserved"] = call.Generated,
                ["wallGapMs"] = prior is null ? null : call.AtMs - prior.AtMs });
            prior = call;
        }
        bool captured = calls is { Count: > 0 } && generated > 0;
        return new JsonObject
        {
            ["verdict"] = problems.Count > 0 ? "OBSERVED_BLINK_CALL_MISMATCH"
                : captureProblems.Count > 0 ? "INCONCLUSIVE"
                : captured && minReadback == 44000 && maxReadback == 45000 ? "OBSERVED_BLINK_CALL_TRANSITIONS" : "INCONCLUSIVE",
            ["minReadback"] = minReadback, ["maxReadback"] = maxReadback,
            ["generationEvidenceAvailable"] = captured, ["generationCalls"] = ticks, ["problems"] = problems,
            ["captureProblems"] = captureProblems,
            ["encodedFaceDiagnostics"] = faces, ["physicalObservation"] = observation.DeepClone(),
            ["firmwareAcceptance"] = "NOT_JUDGED",
            ["missing"] = captured ? (minReadback == 44000 && maxReadback == 45000 ? null
                    : $"The blink settings read back as {minReadback}/{maxReadback} ms, not the requested 44000/45000.")
                : calls is { Count: > 0 }
                ? "KeepFaceAlive calls were observed but none generated a blink inside the window."
                : "No KeepFaceAlive call observation (0x58D382..38C/0x58D54C..550) in the window: the diagnostic hook was not supplied or the keep-alive gate stayed closed.",
            ["verdictRule"] = "M5-062 B4 base setter preserves the existing blink deadline. M5-028 subtracts60 "
                + "per KeepFaceAlive call, not wall milliseconds; a due blink generates and draws a new inclusive "
                + "44000..45000 deadline from these parameter values. Paused calls do not consume wall time. "
                + "Calls come only from the read-only KeepFaceAlive hook (B-M5-32), one row per actual call. "
                + "Readback, layer-name samples, dark encoded images and human answers cannot fabricate generation "
                + "events. Lit pixels use the engine codec model only; nearest raw F1 messages bound host capture "
                + "order, not firmware playback/display (M3-008/M5-036). No darkness threshold is an acceptance rule."
        };
    }

    public static JsonObject LockedHead(IEnumerable<M5Hw.WireMessage> messages, double lockAt,
        double clipFrom, double clipTo, double unlockAt, double toMs)
    {
        var all = OrderedUnique(messages).Where(m => m.Payload.Length > 0).ToArray();
        var wire = all.Where(m => m.AtMs >= lockAt && m.AtMs <= toMs).ToArray();
        var disables = wire.Where(m => m.Outbound && m.Payload[0] == 0x9D).ToArray();
        var enables = wire.Where(m => m.Outbound && m.Payload[0] == 0x9E).ToArray();
        bool validDisable = disables.Any(m => m.AtMs <= clipFrom && m.Payload.Length == 2 && m.Payload[1] == 1);
        bool earlyEnable = enables.Any(m => m.AtMs < unlockAt && m.Payload.Length == 2 && (m.Payload[1] & 1) != 0);
        bool ownUnlock = enables.Any(m => m.AtMs >= unlockAt && m.Payload.Length == 2 && m.Payload[1] == 1);
        var states = wire.Where(m => !m.Outbound && m.AtMs >= clipFrom && m.AtMs <= clipTo
            && m.Payload[0] == (byte)RobotMessageId.AnimState && m.Payload.Length == 16)
            .Select(m => (Wire: m, State: (AnimationState)RobotMessage.Parse(m.Payload))).ToArray();
        var heads = wire.Where(m => !m.Outbound && m.AtMs >= clipFrom && m.AtMs <= clipTo
            && m.Payload[0] == (byte)RobotMessageId.State && m.Payload.Length == 92)
            .Select(m => (Wire: m, State: (RobotState)RobotMessage.Parse(m.Payload))).ToArray();
        int commands = wire.Count(m => m.Outbound && m.AtMs >= clipFrom && m.AtMs <= clipTo && m.Payload[0] == 0x93 && m.Payload.Length == 4);
        bool reportedEnabled = states.Any(s => (s.State.EnabledAnimTracks & 1) != 0);
        bool changed = heads.Select(s => BitConverter.SingleToInt32Bits(s.State.HeadAngle)).Distinct().Count() > 1;
        var missing = new JsonArray();
        if (!earlyEnable)
        {
            if (!validDisable) missing.Add("No DisableAnimTracks for the head (mask 1) before the clip.");
            if (!ownUnlock) missing.Add("No EnableAnimTracks mask 1 after the harness unlock.");
            if (commands == 0) missing.Add("No head keyframe was sent during the clip.");
        }
        return new JsonObject
        {
            ["verdict"] = earlyEnable ? "OBSERVED_EARLY_HEAD_ENABLE" : !validDisable || !ownUnlock || commands == 0
                ? "INCONCLUSIVE" : "OBSERVED_LOCKED_HEAD_EVIDENCE",
            ["missing"] = missing.Count == 0 ? null : missing,
            ["firmwareObservationAssessment"] = "NOT_JUDGED: head telemetry changes, enabled-track reports and their sample counts are firmware observations, independent of the outgoing wire comparison.",
            ["lockRequestAtMs"] = lockAt, ["clipFromMs"] = clipFrom, ["clipToMs"] = clipTo,
            ["unlockRequestAtMs"] = unlockAt, ["observationEndMs"] = toMs,
            ["validDisableBeforeClip"] = validDisable, ["enableBeforeUnlock"] = earlyEnable,
            ["harnessUnlockEnableObserved"] = ownUnlock, ["headKeyframes"] = commands,
            ["headTrackReportedEnabled"] = reportedEnabled, ["headTelemetryChanged"] = changed,
            ["disableWire"] = WireEvidence(disables), ["enableWire"] = WireEvidence(enables),
            ["trackReports"] = new JsonArray(states.Select(s => (JsonNode)new JsonObject {
                ["atMs"] = s.Wire.AtMs, ["frameLine"] = s.Wire.FrameLine,
                ["robotTimestamp"] = s.State.Timestamp, ["enabledAnimTracks"] = s.State.EnabledAnimTracks }).ToArray()),
            ["headTelemetry"] = new JsonArray(heads.Select(s => (JsonNode)new JsonObject {
                ["atMs"] = s.Wire.AtMs, ["frameLine"] = s.Wire.FrameLine,
                ["robotTimestamp"] = s.State.Timestamp, ["headRad"] = s.State.HeadAngle }).ToArray()),
            ["verdictRule"] = "M4-014 Disable head mask1 before the clip; M5-035 still streams head keyframes; "
                + "the harness's own Enable mask1 follows its unlock request, not InitStream. Raw F1 masks and "
                + "head telemetry are bounded to the marked clip; later movement is excluded. Telemetry changes "
                + "are observations without an invented tolerance. No physical verdict follows from counters or "
                + "operator text alone; firmware execution/display remains M5-036 HARDWARE_ONLY."
        };
    }

    public static JsonObject ObservationAnswer(JsonObject window, string question, string? raw, string[] choices,
        double askedAtMs, double answeredAtMs)
    {
        if (window["fromMs"] is null || window["toMs"] is null)
            throw new InvalidOperationException("Observation answers require a closed, marked window.");
        return new()
        {
            ["window"] = window.DeepClone(), ["question"] = question, ["rawAnswer"] = raw,
            ["answer"] = raw is null ? "not-observed" : M5Hw.NormaliseAnswer(raw, choices),
            ["askedAtMs"] = askedAtMs, ["answeredAtMs"] = answeredAtMs,
            ["interpretation"] = "Operator observation only; unexpected, unclear and free text are retained for investigation, never converted into hardware acceptance."
        };
    }

    // Frame activity must come from independently established generation evidence, never total wire counts:
    // the B-M5-32 StreamLayers hook joined to the wire (LayerActivity), or a named unit substitute.
    public sealed record FaceActivity(int AudioFrameLine, int AudioOrdinal, bool Active);

    public static JsonObject FiniteLayers(IEnumerable<M5Hw.WireMessage> messages, double fromMs, double toMs,
        bool drained, IReadOnlyList<FaceActivity>? activity = null, string? activityProblem = null)
    {
        var rows = OrderedUnique(messages).Where(m => m.Outbound && m.AtMs >= fromMs && m.AtMs <= toMs
            && m.Payload.Length > 0).ToArray();
        var audioIndices = Enumerable.Range(0, rows.Length).Where(i => rows[i].Payload[0] is 0x8E or 0x8F).ToArray();
        var frames = new JsonArray(); int missingFace = 0, unexpectedFace = 0, matched = 0;
        foreach (int i in audioIndices)
        {
            int next = Array.FindIndex(rows, i + 1, m => m.Payload[0] is 0x8E or 0x8F or 0x9A);
            if (next < 0) next = rows.Length;
            int faces = rows.Skip(i + 1).Take(next - i - 1).Count(m => m.Payload[0] == 0x97);
            var flags = activity?.Where(a => a.AudioFrameLine == rows[i].FrameLine && a.AudioOrdinal == rows[i].SubmessageOrdinal).ToArray();
            bool? active = flags?.Length == 1 ? flags[0].Active : null;
            if (active is not null) matched++;
            if (active == true && faces != 1) missingFace++;
            if (active == false && faces != 0) unexpectedFace++;
            frames.Add(new JsonObject { ["audioFrameLine"] = rows[i].FrameLine, ["audioOrdinal"] = rows[i].SubmessageOrdinal,
                ["faceActive"] = active, ["faces"] = faces });
        }
        int starts = rows.Count(m => m.Payload[0] == 0x9B), ends = rows.Count(m => m.Payload[0] == 0x9A);
        int faceCount = rows.Count(m => m.Payload[0] == 0x97), backpack = rows.Count(m => m.Payload[0] == (byte)RobotMessageId.AnimBackpackLights);
        bool established = activityProblem is null && audioIndices.Length > 0
            && activity?.Count == audioIndices.Length && matched == audioIndices.Length;
        int assignedFaces = frames.Sum(f => f!["faces"]!.GetValue<int>());
        bool lifecycle = starts == 1 && ends == 1 && !rows.Any(m => m.Payload[0] == 0x8D)
            && rows.First(m => m.Payload[0] == 0x9B).Payload.Length == 2
            && rows.Last().Payload[0] == 0x9A && rows.Last().Payload.Length == 1;
        return new JsonObject
        {
            ["verdict"] = activityProblem is not null ? "INCONCLUSIVE" : missingFace > 0 ? "OBSERVED_MISSING_REQUIRED_FACE" : unexpectedFace > 0
                ? "OBSERVED_UNEXPECTED_FACE" : established && drained && lifecycle && backpack > 0 && faceCount > 0 && assignedFaces == faceCount
                    ? "OBSERVED_CONDITIONAL_LAYER_FRAMES" : "INCONCLUSIVE",
            ["faces"] = faceCount, ["audioFrames"] = audioIndices.Length, ["backpackChanges"] = backpack,
            ["starts"] = starts, ["ends"] = ends, ["drained"] = drained,
            ["faceActivityEstablished"] = established, ["frameEvidence"] = frames,
            ["missingRequiredFaceFrames"] = missingFace, ["unexpectedFaceFrames"] = unexpectedFace,
            ["wireWindow"] = WireEvidence(rows),
            ["missing"] = !established ? activityProblem
                ?? (activity is null ? "No per-frame layered face flag (StreamLayers hook) was supplied; aggregate counts cannot establish required face output."
                    : "The supplied per-frame face flags do not cover every audio frame in the window exactly once.")
                : missingFace > 0 || unexpectedFace > 0 ? null
                : !drained ? "The layers did not drain after the glitch."
                : !lifecycle ? "The layer stream did not start once and end once (one Start, one End last, no Abort)."
                : backpack == 0 ? "No backpack light message was sent in the window."
                : faceCount == 0 ? "No face image was sent in the window."
                : assignedFaces != faceCount ? "Some face images were not inside an audio frame." : null,
            ["verdictRule"] = "M5-029: ApplyLayersToAnim(null) sets face only from a due face-layer result; "
                + "StreamLayers buffers face iff that flag is set. First distortion face is not due before cumulative "
                + "duration >=33 ms; backpack has independent 0/200,200/60,260/60,320/60,380/60 timing. "
                + "Face/audio totals may differ. A stored base face does not set the flag. No exact 13/20 oracle. "
                + "Flags come from the read-only StreamLayers hook joined to the wire by stream order (B-M5-32); "
                + "unknown or unjoined flags remain INCONCLUSIVE; distinct persistent coverage is KEEP_ALIVE."
        };
    }

    public sealed record CapturedFrame(int Line, double AtMs, bool Outbound, Frame? Frame);

    public static IEnumerable<M5Hw.WireMessage> ReadWire(IEnumerable<CapturedFrame> frames, bool includeRetransmissions = false)
    {
        var seenOut = new HashSet<ushort>(); var seenIn = new HashSet<ushort>();
        int outEpoch = 0, inEpoch = 0;
        foreach (var capture in frames.OrderBy(f => f.AtMs).ThenBy(f => f.Line))
        {
            if (capture.Frame is not { } frame) continue;
            for (int ordinal = 0; ordinal < frame.Messages.Count; ordinal++)
            {
                var m = frame.Messages[ordinal]; var seen = capture.Outbound ? seenOut : seenIn;
                if (m.Type is ReliableMessageType.ConnectionRequest or ReliableMessageType.ConnectionResponse)
                { seen.Clear(); if (capture.Outbound) outEpoch++; else inEpoch++; continue; }
                if (m.Type is not (ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage)
                    || m.Payload.Length == 0) continue;
                bool retransmission = false;
                if (m.IsReliable)
                {
                    retransmission = !seen.Add(m.Seq);
                    if (retransmission && !includeRetransmissions) continue;
                    if (seen.Count > 8192) seen.RemoveWhere(old => unchecked((ushort)(m.Seq - old)) > 4096);
                }
                yield return new(capture.AtMs, capture.Outbound, m.Payload, capture.Line, ordinal,
                    m.IsReliable ? m.Seq : null, capture.Outbound ? outEpoch : inEpoch, retransmission);
            }
        }
    }

    public static IEnumerable<M5Hw.WireMessage> OrderedUnique(IEnumerable<M5Hw.WireMessage> messages)
    {
        var epochs = new Dictionary<(bool, int), HashSet<ushort>>();
        foreach (var m in messages.OrderBy(m => m.AtMs).ThenBy(m => m.FrameLine).ThenBy(m => m.SubmessageOrdinal))
        {
            if (m.ReliableSequence is { } seq)
            {
                var key = (m.Outbound, m.ConnectionEpoch);
                if (!epochs.TryGetValue(key, out var seen)) epochs[key] = seen = new();
                if (!seen.Add(seq)) continue;
                if (seen.Count > 8192) seen.RemoveWhere(old => unchecked((ushort)(seq - old)) > 4096);
            }
            yield return m;
        }
    }

    public static JsonArray WireEvidence(IEnumerable<M5Hw.WireMessage> messages) => new(messages.Select(m =>
        (JsonNode)new JsonObject { ["atMs"] = m.AtMs, ["payloadHex"] = Convert.ToHexString(m.Payload),
            ["frameLine"] = m.FrameLine, ["submessageOrdinal"] = m.SubmessageOrdinal,
            ["reliableSequence"] = m.ReliableSequence, ["connectionEpoch"] = m.ConnectionEpoch,
            ["isRetransmission"] = m.IsRetransmission }).ToArray());

    public static JsonObject LiveBursts(IEnumerable<M5Hw.WireMessage> messages, double fromMs, double toMs)
    {
        // Stable timestamp sort retains capture/submessage order for equal timestamps.
        var rows = messages.Where(m => m.Outbound && m.AtMs >= fromMs && m.AtMs <= toMs
            && m.Payload.Length > 0 && m.Payload[0] is 0x9B or 0x9A or 0x8D).OrderBy(m => m.AtMs);
        var evidence = new JsonArray(); var problems = new JsonArray();
        double? opened = null, lastEnd = null; int starts = 0, pairs = 0;
        foreach (var row in rows)
        {
            byte id = row.Payload[0];
            var item = new JsonObject { ["atMs"] = row.AtMs, ["payloadHex"] = Convert.ToHexString(row.Payload) };
            evidence.Add(item);
            if (id == 0x9B)
            {
                starts++;
                if (row.Payload.Length != 2 || row.Payload[1] != 255) problems.Add("Expected Start FF.");
                if (opened is not null) problems.Add("Start before the preceding burst End.");
                if (lastEnd is not null) item["restartGapMs"] = row.AtMs - lastEnd.Value;
                opened = row.AtMs;
            }
            else if (id == 0x9A)
            {
                if (row.Payload.Length != 1) problems.Add("Malformed End.");
                if (opened is null) problems.Add("End without preceding Start FF.");
                else { item["burstSpanMs"] = row.AtMs - opened.Value; pairs++; }
                opened = null; lastEnd = row.AtMs;
            }
            else problems.Add("Unexpected Abort inside the live observation window.");
        }
        var missing = new JsonArray();
        if (problems.Count == 0 && pairs < 2) missing.Add($"Only {pairs} complete live-idle burst(s) (Start FF then End) in the window; at least two are needed.");
        if (problems.Count == 0 && toMs - fromMs < 10000) missing.Add("The live-idle window was shorter than 10 s.");
        return new JsonObject
        {
            ["verdict"] = problems.Count > 0 ? "OBSERVED_INVALID_BURST_ORDER"
                : pairs >= 2 && toMs - fromMs >= 10000 ? "OBSERVED_FF_BURST_ORDER" : "INCONCLUSIVE",
            ["missing"] = missing.Count == 0 ? null : missing,
            ["startFF"] = starts, ["completedBursts"] = pairs, ["openFinalBurst"] = opened is not null,
            ["orderedLifecycle"] = evidence, ["problems"] = problems,
            ["verdictRule"] = "M5-027/030: UpdateLiveAnimation precedes the common idle tail; completed tracks, "
                + "audio and FIFO permit End, then endSent/no frames/empty FIFO permit InitStream(FF). "
                + "At least two paired bursts in a ten-second harness window; a final open burst is retained. "
                + "Wire spans/restart gaps are evidence only: exact restart timing requires review against generation, "
                + "engine ticks and budget/FIFO readiness; ordering alone does not certify it. M5-036 remains hardware-only."
        };
    }

    public static JsonObject PersistentFace(IEnumerable<M5Hw.WireMessage> messages, double fromMs, double toMs,
        bool preconditionsHeld)
    {
        var rows = messages.Where(m => m.Outbound && m.AtMs >= fromMs && m.AtMs <= toMs
            && m.Payload.Length > 0).ToArray();
        int faces = rows.Count(m => m.Payload[0] == (byte)RobotMessageId.AnimFaceImage && m.Payload.Length > 1);
        int ends = rows.Count(m => m.Payload[0] == 0x9A);
        var lifecycle = rows.Where(m => m.Payload[0] is 0x9B or 0x9A or 0x8D).ToArray();
        bool invalid = ends > 0 || lifecycle.Any(m => m.Payload[0] == 0x8D)
            || lifecycle.Count(m => m.Payload[0] == 0x9B) > 1
            || lifecycle.Any(m => m.Payload[0] == 0x9B && (m.Payload.Length != 2 || m.Payload[1] == 255));
        var missing = new JsonArray();
        if (!preconditionsHeld) missing.Add("The no-animation, no-idle, KeepAliveEyeDart conditions did not hold for the whole window.");
        else if (!invalid)
        {
            if (faces == 0) missing.Add("No face image was sent in the window.");
            if (toMs - fromMs < 10000) missing.Add("The window was shorter than 10 s.");
        }
        return new JsonObject
        {
            ["verdict"] = !preconditionsHeld ? "INCONCLUSIVE" : invalid ? "OBSERVED_UNEXPECTED_LAYER_LIFECYCLE"
                : faces == 0 || toMs - fromMs < 10000 ? "INCONCLUSIVE" : "OBSERVED_PERSISTENT_FACE_WINDOW",
            ["missing"] = missing.Count == 0 ? null : missing,
            ["preconditionsHeld"] = preconditionsHeld, ["faces"] = faces, ["ends"] = ends,
            ["orderedLifecycle"] = JsonSerializerNode(lifecycle),
            ["verdictRule"] = "M5-028/029: no explicit animation or movement idle; a persistent KeepAliveEyeDart "
                + "face layer remains present. StreamLayers ends only once layers/FIFO drain. Require face output "
                + "and no End/Abort during this ten-second window, which may begin after Start. This observes a "
                + "bounded interval of the persistent stream, not indefinite execution or firmware display (M5-036)."
        };
    }

    private static JsonArray JsonSerializerNode(IEnumerable<M5Hw.WireMessage> rows) => new(rows.Select(m =>
        (JsonNode)new JsonObject { ["atMs"] = m.AtMs, ["payloadHex"] = Convert.ToHexString(m.Payload) }).ToArray());

    public static JsonObject DrivingEnd(IEnumerable<M5Hw.WireMessage> messages,
        IEnumerable<JsonObject> completions, double endTriggerAtMs, double driveCompletedAtMs, uint? expectedEndActionId = null)
    {
        var wire = OrderedUnique(messages).Where(m => m.AtMs >= endTriggerAtMs && m.AtMs <= driveCompletedAtMs).ToArray();
        var starts = wire.Where(m => m.Outbound && m.Payload.Length == 2
            && m.Payload[0] == (byte)RobotMessageId.AnimStartOfAnimation && m.Payload[1] != 255).ToArray();
        var start = starts.Length == 1 ? starts[0] : null;
        var ended = start is null ? null : wire.Skip(Array.IndexOf(wire, start) + 1).FirstOrDefault(m => !m.Outbound
            && m.Payload.Length == 2 && m.Payload[0] == (byte)RobotMessageId.AnimEnded
            && m.Payload[1] == start.Payload[1]);
        // A loop's completion is enqueued before the synchronous handler creates the end action.
        // Its host delivery can therefore occur AFTER endTriggerAtMs. Require the end stream's
        // matching firmware echo before the successful animation-action report, not just that log.
        bool before = ended is not null && completions.Any(e =>
            e["kind"]?.GetValue<string>() == "action-completed"
            && e["ActionType"]!.GetValue<int>() == 26 && e["Result"]!.GetValue<uint>() == 0
            && (expectedEndActionId is null || e["Tag"]?.GetValue<uint>() == expectedEndActionId)
            && e["atMs"]!.GetValue<double>() >= ended.AtMs
            && e["atMs"]!.GetValue<double>() <= driveCompletedAtMs);
        return new JsonObject
        {
            ["endPhaseWireTag"] = start?.Payload[1],
            ["expectedEndActionId"] = expectedEndActionId, ["candidateEndWireStarts"] = starts.Length,
            ["endPhaseStartAtMs"] = start?.AtMs,
            ["endPhaseRobotEndedAtMs"] = ended?.AtMs,
            ["endCompletionBeforeDrive"] = before
        };
    }

    // The live harness and regression fixtures use this same wiring. The full run snapshot supplies the
    // accounting prefix, independently of the bounded wire join and of the window's compliance claim.
    public static JsonObject AudioFromDiagnostics(IEnumerable<M5Hw.WireMessage> messages, IEnumerable<JsonObject> events,
        double fromMs, double toMs, IReadOnlyList<Diagnostic> diagnostics)
    {
        var wire = messages.ToArray();
        var history = Admissions(diagnostics, double.NegativeInfinity, toMs);
        return Audio(wire, events, fromMs, toMs, history.Where(a => a.AtMs >= fromMs).ToArray(),
            JoinSends(wire, diagnostics, fromMs, toMs), Refreshes(diagnostics), history.Where(a => a.AtMs < fromMs).ToArray());
    }

    public static JsonObject Audio(IEnumerable<M5Hw.WireMessage> messages, IEnumerable<JsonObject> events,
        double fromMs, double toMs, IReadOnlyList<BudgetAdmission>? admissions = null,
        SendJoin? join = null, IReadOnlyList<BudgetRefresh>? refreshes = null,
        IReadOnlyList<BudgetAdmission>? accountingPrefix = null)
    {
        var ordered = OrderedUnique(messages).Where(m => m.AtMs >= fromMs && m.AtMs <= toMs && m.Payload.Length != 0).ToArray();
        var wire = ordered.Where(m => m.Outbound).ToArray();
        var rawStates = ordered.Where(m => !m.Outbound && m.Payload[0] == (byte)RobotMessageId.AnimState && m.Payload.Length == 16)
            .Select(m => (Wire: m, State: (AnimationState)RobotMessage.Parse(m.Payload))).ToArray();
        var audio = wire.Where(m => m.Payload[0] is 0x8E or 0x8F).ToArray();
        var states = events.Where(e => e["kind"]?.GetValue<string>() == "animation-state"
            && e["atMs"]!.GetValue<double>() >= fromMs && e["atMs"]!.GetValue<double>() <= toMs)
            .OrderBy(e => e["atMs"]!.GetValue<double>()).ToArray();
        var missing = new JsonArray();
        if (audio.Length < 2) missing.Add("Fewer than two audio messages.");
        if (!audio.Any(m => m.Payload[0] == 0x8E)) missing.Add("No AudioSample; silence alone does not exercise sample framing.");
        if (!audio.Any(m => m.Payload[0] == 0x8F)) missing.Add("No AudioSilence in this window; the silent-frame path is not observed.");
        if (states.Length < 2) missing.Add("Fewer than two AnimationState counter observations.");
        double span = audio.Length < 2 ? 0 : audio[^1].AtMs - audio[0].AtMs;
        if (span < 5000) missing.Add("Audio transmission spans less than five seconds.");
        int bad = audio.Count(m => m.Payload[0] == 0x8E ? m.Payload.Length != 745 : m.Payload.Length != 1);
        var gaps = audio.Zip(audio.Skip(1), (a, b) => b.AtMs - a.AtMs).OrderBy(x => x).ToArray();
        // End is counted as an audio frame by C12 but bypasses C9's budget. Do not compare snapshots
        // at/after End against 14 as though they came from the budgeted audio drain.
        double firstEnd = wire.FirstOrDefault(m => m.Payload[0] == (byte)RobotMessageId.AnimEndOfAnimation)?.AtMs
            ?? double.PositiveInfinity;
        var rows = new JsonArray();
        var activeFramesAhead = new List<int>();
        var activeBytesAhead = new List<int>();
        foreach (var e in states)
        {
            int framesAhead = unchecked(e["streamFrames"]!.GetValue<int>() - e["audioPlayed"]!.GetValue<int>());
            int bytesAhead = unchecked(e["streamBytes"]!.GetValue<int>() - e["bytesPlayed"]!.GetValue<int>());
            double at = e["atMs"]!.GetValue<double>();
            uint? robotTimestamp = e["robotTimestamp"]?.GetValue<uint>();
            var source = rawStates.LastOrDefault(s => s.Wire.AtMs <= at && s.State.Timestamp == robotTimestamp
                && s.State.NumAudioFramesPlayed == e["audioPlayed"]!.GetValue<int>()
                && s.State.NumAnimBytesPlayed == e["bytesPlayed"]!.GetValue<int>()).Wire;
            bool active = audio.Length > 0 && at >= audio[0].AtMs && at < firstEnd;
            if (active) { activeFramesAhead.Add(framesAhead); activeBytesAhead.Add(bytesAhead); }
            rows.Add(new JsonObject
            {
                ["atMs"] = at,
                ["framesAhead"] = framesAhead,
                ["bytesAhead"] = bytesAhead,
                ["beforeFirstEnd"] = active,
                ["rawStateWire"] = source is null ? null : WireEvidence(new[] { source }),
                ["stateConnectionEpoch"] = source?.ConnectionEpoch,
                ["isAtomicAdmissionEvidence"] = false,
                ["epochNote"] = "F1 payload epoch is earlier raw receipt; stream totals are sampled at API delivery. Neither difference is an atomic send-admission observation.",
                ["snapshot"] = e.DeepClone()
            });
        }
        if (activeFramesAhead.Count == 0) missing.Add("No counter snapshot during the active audio stream before End.");
        var result = new JsonObject
        {
            ["verdict"] = bad != 0 ? "OBSERVED_MALFORMED_AUDIO_FRAMING"
                : missing.Count != 0 ? "INCONCLUSIVE" : "OBSERVED_AUDIO_FRAMING_AND_COUNTERS",
            ["sampleCount"] = audio.Count(m => m.Payload[0] == 0x8E),
            ["silenceCount"] = audio.Count(m => m.Payload[0] == 0x8F),
            ["malformedAudioMessages"] = bad,
            ["audioSpanMs"] = span,
            ["wireRateHz"] = span > 0 ? (audio.Length - 1) * 1000.0 / span : null,
            ["largestWireGapMs"] = gaps.Length > 0 ? gaps[^1] : null,
            ["medianWireGapMs"] = gaps.Length > 0 ? gaps[gaps.Length / 2] : null,
            ["minimumFramesAhead"] = activeFramesAhead.Count > 0 ? activeFramesAhead.Min() : null,
            ["maximumFramesAhead"] = activeFramesAhead.Count > 0 ? activeFramesAhead.Max() : null,
            ["minimumBytesAhead"] = activeBytesAhead.Count > 0 ? activeBytesAhead.Min() : null,
            ["maximumBytesAhead"] = activeBytesAhead.Count > 0 ? activeBytesAhead.Max() : null,
            ["snapshotsAbove14Frames"] = activeFramesAhead.Count(n => n > 14),
            ["snapshotsAbove8192Bytes"] = activeBytesAhead.Count(n => n > 8192),
            ["counterRows"] = rows,
            ["missing"] = missing,
            ["coverageRequirement"] = "At least two audio packets, both sample and silence paths, five seconds of captured audio and counter samples are local coverage requirements, not engine guarantees.",
            ["verdictRule"] = "M2-008/M2-009 0x007BCDAA/0x007BCE8A and union +1 at 0x007ABF0A: 744 sample bytes plus tag, empty silence. "
                + "M3-012 C9 allows 14 unplayed audio frames and 8192 bytes; "
                + "C12 counts direct End outside that budget. Snapshots and host wire pacing are observations, "
                + "not atomic budget samples or robot execution timestamps. Counter differences above14/8192 "
                + "are mixed-epoch diagnostics, not alleged send violations. No2ms grouping threshold is used."
        };
        result["framingVerdict"] = result["verdict"]!.DeepClone();
        var budget = BudgetSamples(admissions, refreshes, accountingPrefix);
        result["budgetEvidence"] = budget; result["budgetCompliance"] = "NOT_JUDGED";
        result["orderedWireWindow"] = WireEvidence(ordered);
        // B-M5-32: every outbound audio message of the window must be joined, by stream order, to a successful
        // coherent gate observation whose epoch's allowances were themselves observed.
        int unjoinedAudio = join is null ? audio.Length : audio.Count(m => !join.IsJoined(m));
        int coherentInWindow = (admissions ?? Array.Empty<BudgetAdmission>()).Count(a => a.Coherent);
        string? admissionMissing = admissions is null || coherentInWindow == 0
                ? "No send-gate observation (0x57BF96..BFCC) was supplied for this window; delivery snapshots cannot replace admission epochs."
            : join is null ? "Gate observations were not joined to the wire."
            : join.Problem is not null ? "Send join unavailable: " + join.Problem
            : unjoinedAudio > 0 ? $"{unjoinedAudio} outbound audio message(s) in the window are not joined to a gate observation."
            : refreshes is null ? "Budget refresh observations (C9) were not supplied; gate allowances cannot be checked."
            : budget["accountingUnverifiedSamples"]!.GetValue<int>() > 0 ? "Accounting unverified: "
                + budget["samples"]!.AsArray().First(r => r!["accountingUnverifiedReason"] is not null)!["accountingUnverifiedReason"]!.GetValue<string>()
            : null;
        result["sendJoin"] = join?.Evidence.DeepClone();
        result["unjoinedAudioMessages"] = unjoinedAudio;
        result["admissionEvidenceMissing"] = admissionMissing;
        int mismatches = budget["allowanceFormulaMismatches"]!.GetValue<int>() + budget["allowanceAccountingMismatches"]!.GetValue<int>();
        if (budget["observedViolations"]!.GetValue<int>() > 0) result["verdict"] = "OBSERVED_COHERENT_ADMISSION_VIOLATION";
        else if (mismatches > 0) result["verdict"] = "OBSERVED_ADMISSION_ACCOUNTING_MISMATCH";
        else if (result["verdict"]!.GetValue<string>() == "OBSERVED_AUDIO_FRAMING_AND_COUNTERS")
        {
            result["verdict"] = admissionMissing is null ? "OBSERVED_AUDIO_FRAMING_AND_ADMISSION" : "INCONCLUSIVE";
            if (admissionMissing is null) result["budgetCompliance"] = "OBSERVED_FOR_JOINED_WINDOW";
        }
        result["admissionRule"] = "M3-012 C9 (0x57C6F6..7AC) refresh: bytes max(0,min(30000,played-streamed+8192)), frames max(0,played-streamed+14). "
            + "C14 (0x57BF96..BFCC): a buffered message passes when its size fits the bytes left and, for 0x8E/0x8F, a frame is left; "
            + "only a successful send subtracts. C12 direct End (0x57C464..496) bypasses the gate. Positive only when every outbound "
            + "audio message in the window is joined to an observed gate send and each relevant epoch has its refresh and complete earlier gate history; it covers this window, not every send of the run.";
        if (states.Length >= 2)
        {
            var first = states[0]; var last = states[^1];
            result["audioPlayedDelta"] = unchecked(last["audioPlayed"]!.GetValue<int>() - first["audioPlayed"]!.GetValue<int>());
            result["framesStreamedDelta"] = unchecked(last["streamFrames"]!.GetValue<int>() - first["streamFrames"]!.GetValue<int>());
            result["bytesPlayedDelta"] = unchecked(last["bytesPlayed"]!.GetValue<int>() - first["bytesPlayed"]!.GetValue<int>());
            result["bytesStreamedDelta"] = unchecked(last["streamBytes"]!.GetValue<int>() - first["streamBytes"]!.GetValue<int>());
            result["firstClientDropCount"] = first["drops"]!.DeepClone();
            result["lastClientDropCount"] = last["drops"]!.DeepClone();
            result["dropCountChanged"] = !JsonNode.DeepEquals(first["drops"], last["drops"]);
        }
        return result;
    }

    public static JsonObject LiveIdle(IEnumerable<M5Hw.WireMessage> messages, IEnumerable<JsonObject> events,
        double fromMs, double toMs)
    {
        var wire = messages.Where(m => m.Outbound && m.AtMs >= fromMs && m.AtMs <= toMs
            && m.Payload.Length > 0).OrderBy(m => m.AtMs).ToArray();
        var states = events.Where(e => e["kind"]?.GetValue<string>() == "state"
            && e["atMs"]!.GetValue<double>() <= toMs).OrderBy(e => e["atMs"]!.GetValue<double>()).ToArray();
        var heads = wire.Where(m => m.Payload[0] == 0x93).ToArray();
        var paired = new JsonArray();
        int outside = 0, unmatched = 0, malformed = 0;
        foreach (var head in heads)
        {
            // M5 inventory C2: tag, duration u16, angle s8. No variability byte on wire.
            if (head.Payload.Length != 4) { malformed++; continue; }
            var state = states.LastOrDefault(e => e["atMs"]!.GetValue<double>() <= head.AtMs);
            if (state is null) { unmatched++; continue; }
            float radians = state["motionHeadRad"]!.GetValue<float>();
            float degrees = radians * BitConverter.Int32BitsToSingle(0x42652EE1);
            int basis = unchecked((sbyte)(int)MathF.Truncate(degrees));
            int observed = unchecked((sbyte)head.Payload[3]);
            int delta = observed - basis;
            if (Math.Abs(delta) > 6) outside++;
            paired.Add(new JsonObject
            {
                ["atMs"] = head.AtMs,
                ["headDegrees"] = observed,
                ["baseDegrees"] = basis,
                ["differenceDegrees"] = delta,
                ["motionHeadRad"] = radians,
                ["stateAgeMs"] = head.AtMs - state["atMs"]!.GetValue<double>(),
                ["durationMs"] = head.Payload[1] | head.Payload[2] << 8,
                ["stateStatus"] = state["status"]?.DeepClone()
            });
        }
        var missing = new JsonArray();
        if (heads.Length == 0) missing.Add("No head keyframe in the live-idle window.");
        if (paired.Count == 0 || unmatched != 0) missing.Add("Missing preceding Motion.HeadAngleRad samples for one or more head keyframes.");
        int body = wire.Count(m => m.Payload[0] == (byte)RobotMessageId.AnimBodyMotion && m.Payload.Length == 5);
        int lift = wire.Count(m => m.Payload[0] == (byte)RobotMessageId.AnimLiftHeight && m.Payload.Length == 4);
        if (body == 0) missing.Add("No body wiggle observed.");
        if (lift == 0) missing.Add("No lift wiggle observed.");
        return new JsonObject
        {
            ["verdict"] = malformed > 0 ? "OBSERVED_MALFORMED_HEAD_KEYFRAME"
                : missing.Count > 0 ? "INCONCLUSIVE" : "OBSERVED_LIVE_IDLE_MEASUREMENTS",
            ["headFrames"] = heads.Length,
            ["bodyFrames"] = body,
            ["liftFrames"] = lift,
            ["malformedHeadFrames"] = malformed,
            ["unmatchedHeadFrames"] = unmatched,
            ["outsideDefaultSixDegreeWindow"] = outside,
            ["pairedHeadFrames"] = paired,
            ["missing"] = missing,
            ["verdictRule"] = "M5-030 A35: (s8)trunc(Motion.HeadAngleRad * float32 bits 0x42652EE1), "
                + "default variability +/-6 (A34). Pairings use the nearest preceding delivered state, "
                + "not the exact build-time state; queued frames can lag. Outliers require capture review, "
                + "and these observations do not assert uncalibrated-case coverage."
        };
    }
}
