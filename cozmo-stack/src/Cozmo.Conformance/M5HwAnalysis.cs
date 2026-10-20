using System.Text.Json.Nodes;
using Cozmo.Protocol;

namespace Cozmo.Conformance;

/// <summary>
/// Offline measurement of hardware captures, not a source-fidelity oracle. Counter snapshots are taken on
/// delivery, not atomically with the streamer's budget calculation. Keep those limitations in each result.
/// </summary>
public static class M5HwAnalysis
{
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
