using System.Text.Json.Nodes;
using Cozmo.Protocol;

namespace Cozmo.Conformance;

/// <summary>
/// Offline measurement of hardware captures, not a source-fidelity oracle. Counter snapshots are taken on
/// delivery, not atomically with the streamer's budget calculation. Keep those limitations in each result.
/// </summary>
public static class M5HwAnalysis
{
    // Actual call/deadline observations only. Public harness snapshots cannot currently populate this join.
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
        var problems = new JsonArray(); var ticks = new JsonArray(); int generated = 0;
        BlinkCall? prior = null;
        foreach (var call in calls ?? Array.Empty<BlinkCall>())
        {
            if (call.AtMs < fromMs || call.AtMs > toMs || (prior is not null && call.AtMs < prior.AtMs))
                problems.Add("Call timestamps do not belong to this ordered observation window.");
            int remaining = unchecked(call.BeforeMs - 60);
            bool due = remaining <= 0;
            if (call.Generated != due || (!due && call.AfterMs != remaining)
                || (due && (call.AfterMs < 44000 || call.AfterMs > 45000))) problems.Add("Call/deadline transition contradicts the checked 60-per-call branch.");
            if (prior is not null && (call.CallNumber != prior.CallNumber + 1 || call.BeforeMs != prior.AfterMs)) problems.Add("Incomplete or discontinuous KeepFaceAlive call sequence.");
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
                : captured && minReadback == 44000 && maxReadback == 45000 ? "OBSERVED_BLINK_CALL_TRANSITIONS" : "INCONCLUSIVE",
            ["minReadback"] = minReadback, ["maxReadback"] = maxReadback,
            ["generationEvidenceAvailable"] = captured, ["generationCalls"] = ticks, ["problems"] = problems,
            ["encodedFaceDiagnostics"] = faces, ["physicalObservation"] = observation.DeepClone(),
            ["firmwareAcceptance"] = "NOT_JUDGED",
            ["missing"] = captured ? null : "Actual KeepFaceAlive call numbers, pre-existing blink deadline and generation/reset values at 0x58D382..38C/0x58D54C..550 are not exposed by the allowed public harness observations.",
            ["verdictRule"] = "M5-062 B4 base setter preserves the existing blink deadline. M5-028 subtracts60 "
                + "per KeepFaceAlive call, not wall milliseconds; a due blink generates and draws a new inclusive "
                + "44000..45000 deadline from these parameter values. Paused calls do not consume wall time. "
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
        return new JsonObject
        {
            ["verdict"] = !validDisable || earlyEnable || !ownUnlock || commands == 0 || states.Length == 0 || heads.Length < 2
                ? "INCONCLUSIVE" : reportedEnabled ? "OBSERVED_HEAD_TRACK_REPORTED_ENABLED"
                : changed ? "OBSERVED_HEAD_TELEMETRY_CHANGED" : "OBSERVED_LOCKED_HEAD_EVIDENCE",
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

    // Frame activity must come from independently established generation evidence, never total wire counts.
    // The allowed public harness currently cannot supply this; synthetic unit fixtures name that substitution.
    public sealed record FaceActivity(int AudioFrameLine, int AudioOrdinal, bool Active);

    public static JsonObject FiniteLayers(IEnumerable<M5Hw.WireMessage> messages, double fromMs, double toMs,
        bool drained, IReadOnlyList<FaceActivity>? activity = null)
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
        bool established = audioIndices.Length > 0 && activity?.Count == audioIndices.Length && matched == audioIndices.Length;
        int assignedFaces = frames.Sum(f => f!["faces"]!.GetValue<int>());
        bool lifecycle = starts == 1 && ends == 1 && !rows.Any(m => m.Payload[0] == 0x8D)
            && rows.First(m => m.Payload[0] == 0x9B).Payload.Length == 2
            && rows.Last().Payload[0] == 0x9A && rows.Last().Payload.Length == 1;
        return new JsonObject
        {
            ["verdict"] = missingFace > 0 ? "OBSERVED_MISSING_REQUIRED_FACE" : unexpectedFace > 0
                ? "OBSERVED_UNEXPECTED_FACE" : established && drained && lifecycle && backpack > 0 && faceCount > 0 && assignedFaces == faceCount
                    ? "OBSERVED_CONDITIONAL_LAYER_FRAMES" : "INCONCLUSIVE",
            ["faces"] = faceCount, ["audioFrames"] = audioIndices.Length, ["backpackChanges"] = backpack,
            ["starts"] = starts, ["ends"] = ends, ["drained"] = drained,
            ["faceActivityEstablished"] = established, ["frameEvidence"] = frames,
            ["missingRequiredFaceFrames"] = missingFace, ["unexpectedFaceFrames"] = unexpectedFace,
            ["wireWindow"] = WireEvidence(rows),
            ["missing"] = established ? null : "Per-frame layered face flags and random-draw/generation timing are unavailable through the allowed public observations; aggregate counts cannot establish required face output.",
            ["verdictRule"] = "M5-029: ApplyLayersToAnim(null) sets face only from a due face-layer result; "
                + "StreamLayers buffers face iff that flag is set. First distortion face is not due before cumulative "
                + "duration >=33 ms; backpack has independent 0/200,200/60,260/60,320/60,380/60 timing. "
                + "Face/audio totals may differ. A stored base face does not set the flag. No exact 13/20 oracle. "
                + "Unknown flags/draws remain INCONCLUSIVE; distinct persistent coverage is KEEP_ALIVE."
        };
    }

    public sealed record CapturedFrame(int Line, double AtMs, bool Outbound, Frame? Frame);

    public static IEnumerable<M5Hw.WireMessage> ReadWire(IEnumerable<CapturedFrame> frames)
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
                if (m.IsReliable)
                {
                    if (!seen.Add(m.Seq)) continue;
                    if (seen.Count > 8192) seen.RemoveWhere(old => unchecked((ushort)(m.Seq - old)) > 4096);
                }
                yield return new(capture.AtMs, capture.Outbound, m.Payload, capture.Line, ordinal,
                    m.IsReliable ? m.Seq : null, capture.Outbound ? outEpoch : inEpoch);
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
            ["reliableSequence"] = m.ReliableSequence, ["connectionEpoch"] = m.ConnectionEpoch }).ToArray());

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
        return new JsonObject
        {
            ["verdict"] = problems.Count > 0 ? "OBSERVED_INVALID_BURST_ORDER"
                : pairs >= 2 && toMs - fromMs >= 10000 ? "OBSERVED_FF_BURST_ORDER" : "INCONCLUSIVE",
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
        return new JsonObject
        {
            ["verdict"] = !preconditionsHeld ? "INCONCLUSIVE" : invalid ? "OBSERVED_UNEXPECTED_LAYER_LIFECYCLE"
                : faces == 0 || toMs - fromMs < 10000 ? "INCONCLUSIVE" : "OBSERVED_PERSISTENT_FACE_WINDOW",
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
        IEnumerable<JsonObject> completions, double endTriggerAtMs, double driveCompletedAtMs)
    {
        var wire = messages.Where(m => m.AtMs >= endTriggerAtMs && m.AtMs <= driveCompletedAtMs)
            .OrderBy(m => m.AtMs).ToArray();
        var start = wire.FirstOrDefault(m => m.Outbound && m.Payload.Length == 2
            && m.Payload[0] == (byte)RobotMessageId.AnimStartOfAnimation && m.Payload[1] != 255);
        var ended = start is null ? null : wire.FirstOrDefault(m => !m.Outbound && m.AtMs >= start.AtMs
            && m.Payload.Length == 2 && m.Payload[0] == (byte)RobotMessageId.AnimEnded
            && m.Payload[1] == start.Payload[1]);
        // A loop's completion is enqueued before the synchronous handler creates the end action.
        // Its host delivery can therefore occur AFTER endTriggerAtMs. Require the end stream's
        // matching firmware echo before the successful animation-action report, not just that log.
        bool before = ended is not null && completions.Any(e =>
            e["kind"]?.GetValue<string>() == "action-completed"
            && e["ActionType"]!.GetValue<int>() == 26 && e["Result"]!.GetValue<uint>() == 0
            && e["atMs"]!.GetValue<double>() >= ended.AtMs
            && e["atMs"]!.GetValue<double>() <= driveCompletedAtMs);
        return new JsonObject
        {
            ["endPhaseWireTag"] = start?.Payload[1],
            ["endPhaseStartAtMs"] = start?.AtMs,
            ["endPhaseRobotEndedAtMs"] = ended?.AtMs,
            ["endCompletionBeforeDrive"] = before
        };
    }

    public static JsonObject Audio(IEnumerable<M5Hw.WireMessage> messages, IEnumerable<JsonObject> events,
        double fromMs, double toMs)
    {
        var wire = messages.Where(m => m.Outbound && m.AtMs >= fromMs && m.AtMs <= toMs
            && m.Payload.Length != 0).OrderBy(m => m.AtMs).ToArray();
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
            bool active = audio.Length > 0 && at >= audio[0].AtMs && at < firstEnd;
            if (active) { activeFramesAhead.Add(framesAhead); activeBytesAhead.Add(bytesAhead); }
            rows.Add(new JsonObject
            {
                ["atMs"] = at,
                ["framesAhead"] = framesAhead,
                ["bytesAhead"] = bytesAhead,
                ["beforeFirstEnd"] = active,
                ["snapshot"] = e.DeepClone()
            });
        }
        if (activeFramesAhead.Count == 0) missing.Add("No counter snapshot during the active audio stream before End.");
        var result = new JsonObject
        {
            ["verdict"] = missing.Count != 0 ? "INCONCLUSIVE" : bad != 0
                ? "OBSERVED_MALFORMED_AUDIO_FRAMING" : "OBSERVED_AUDIO_FRAMING_AND_COUNTERS",
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
            ["verdictRule"] = "M5 pass-B check 5: 744 sample bytes plus tag, empty silence, at least five seconds, "
                + "approximately 30 audio frames/s. M3-012 C9 allows 14 unplayed audio frames and 8192 bytes; "
                + "C12 counts direct End outside that budget. Snapshots and host wire pacing are observations, "
                + "not atomic budget samples or robot execution timestamps; manager judges any discrepancy."
        };
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
            ["verdict"] = missing.Count > 0 ? "INCONCLUSIVE" : malformed > 0
                ? "OBSERVED_MALFORMED_HEAD_KEYFRAME" : "OBSERVED_LIVE_IDLE_MEASUREMENTS",
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
