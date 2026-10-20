using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>Operator-run observation harness. Expectations: approved M5 inventory and settling pass B's eleven checks.
/// OBSERVED is evidence, never layer acceptance. No production behavior or manifest is changed.</summary>
public static class M5Hw
{
    public const string TestId = "M5-HW";
    public sealed record CheckDef(string Id, string Title, string[] Records);
    public static readonly string[] HardwareOnlyIds = { "M5-036", "M3-008" };
    public static readonly CheckDef[] Checks = {
        new("TAGS", "Three plays: wire start tags and robot echoes", new[]{"M5-008","M5-036"}),
        new("ABORT", "Mid-stream cancellation and robot counters", new[]{"M5-023","M5-036"}),
        new("KEEP_ALIVE", "Neutral replay, live bursts and persistent face stream", new[]{"M5-010","M5-023","M5-027","M5-028","M5-029","M5-030","M5-036","M5-057"}),
        new("LOCKED_HEAD", "Head keyframes still stream while head track is locked", new[]{"M4-014","M4-047","M5-035","M5-036"}),
        new("AUDIO", "744-byte samples, silence, counters and pacing", new[]{"M2-008","M2-009","M5-008","M5-036","M3-012","M6-042"}),
        new("LAYERS", "Face and backpack layer traffic", new[]{"M5-028","M5-029","M5-031","M5-032","M5-036"}),
        new("LIVE_IDLE", "Calibrated head input and default wiggles", new[]{"M5-030","M5-057","M5-036"}),
        new("PARAMETERS", "Uncapped 45000 ms blink maximum over 60 seconds", new[]{"M5-062","M5-028","M5-036","M3-008"}),
        new("DRIVING", "Short DriveToPose and driving animation sequence", new[]{"M5-063","M12-024","M7-026","M15-001","M8-020","M8-021","M5-036","M12-032","M12-002","M13-021","M13-033","M8-005","M8-015","M8-023","M8-027"}),
        new("SLEEP", "Sleep deadline fallback and known action gap", new[]{"M5-038","M1-045","M8-017","M8-020","M8-021"}),
        new("TEARDOWN", "Dispose interval, discarded buffer and transport provenance", new[]{"M5-037","M5-053","M5-055","M5-059","M5-060","M5-022","M4-026","M5-036","M6-042"}),
    };
    public static readonly string[] RecordIds = Checks.SelectMany(x => x.Records).Distinct().ToArray();
    public sealed record SourceRule(string Check, string Record, string Citation, string Comparison);
    public static readonly SourceRule[] SourceRules = {
        new("TAGS", "M5-008", "0x00579FD0..0x00579FD4; 0x0057B2D4..0x0057B2F6; 0x0057C410..0x0057C424", "Fresh outgoing tag allocation starts at 1 and increments; three captured non-idle starts are compared with 1,2,3. Layer starts share the allocator, so this does not establish clip attribution."),
        new("ABORT", "M5-023", "0x0057B3F2..0x0057B410; 0x00517DE4..0x00517E0A; 0x007AA392; 0x007A47DC", "One cancellation produces one reliable empty-body 0x8D; resends count once. Tail counts and physical reaction are observations."),
        new("KEEP_ALIVE", "M5-023", "0x0057B3F2..0x0057B410; 0x00517DE4..0x00517E0A", "The same cancellation-command comparison as ABORT."),
        new("KEEP_ALIVE", "M5-027", "0x0057D3EE..0x0057D44C", "Captured live-idle bursts use Start FF followed by End before another Start; no fixed restart time."),
        new("KEEP_ALIVE", "M5-028", "0x0057C52A..0x0057C556; 0x0057C606..0x0057C646", "With no animation/idle and persistent-layer prerequisites held, layer traffic continues without ending; snapshots limit continuous-state certainty."),
        new("LOCKED_HEAD", "M4-014", "0x0064014E..0x00640188; 0x0063FF94..0x0063FFB4", "Disable head mask1 before the clip and Enable mask1 after harness unlock; no early Enable."),
        new("LOCKED_HEAD", "M5-035", "0x0057C9D4..0x0057C9E2", "Head keyframes continue to stream while the track is locked. Firmware suppression is not inferred."),
        new("AUDIO", "M2-008", "0x007ABE46..0x007ABE54; 0x007ABF0A", "Outbound union size is member size plus its one-byte tag."),
        new("AUDIO", "M2-009", "0x007BCDAA; 0x007BCE8A", "AudioSample has 744 body bytes; AudioSilence has no body."),
        new("AUDIO", "M3-012", "0x0057C6F6..0x0057C7AC; 0x0057BF84..0x0057BFCC; 0x0057C464..0x0057C496", "Refresh byte/frame allowances from played/streamed counters, require available budget for buffered sends, subtract on success; direct End bypasses the gate."),
        new("LAYERS", "M5-029", "0x0064F1D4..0x0064F204; 0x0064F222..0x0064F22C; 0x0057C63C..0x0057C646", "A joined frame emits one face iff its recorded face-output flag is set."),
        new("LAYERS", "M5-028", "0x0057C52A..0x0057C556; 0x0057C606..0x0057C624", "Layer stream starts once and ends after layers and queued messages drain."),
        new("LIVE_IDLE", "M5-030", "0x0057D83E..0x0057D85C; 0x004F8C12..0x004F8C4A", "Head packet has a u16 duration and signed angle byte. Nearest delivered head input and random track occurrence remain observations."),
        new("PARAMETERS", "M5-062", "0x0057E89A..0x0057E8B2; 0x0057C1EC", "Base parameter setter stores the requested 44000/45000 values without the derived cap."),
        new("PARAMETERS", "M5-028", "0x0058D382..0x0058D38C; 0x0058D48C..0x0058D4BE; 0x0058D506..0x0058D550; 0x0082FA96..0x0082FAC0", "Each KeepFaceAlive call subtracts60; a due blink generates and draws an inclusive deadline from the requested range. No wall-clock blink interval is implied."),
        new("DRIVING", "M5-063", "0x004F0D80..0x004F0D8C; 0x004F0E14..0x004F0E2A; 0x004F0DC8..0x004F0DD4", "Matching successful Start requests Loop; matching End completion sets state4 irrespective of End result."),
        new("DRIVING", "M12-024", "0x0055B0BA..0x0055B0C8", "Drive remains running while its End animation is pending. This boundary comparison does not settle the remaining M12 record gap."),
        new("SLEEP", "M1-045", "0x0052CE54..0x0052CE6A", "KNOWN GAP: native calls the factory then queues its action. Capturing the host fallback/diagnostic is observation coverage only."),
        new("SLEEP", "M5-038", "0x0052CEA2..0x0052CFC0", "KNOWN GAP: the native factory constructs compound children; the test does not claim that the missing production writer ran."),
        new("TEARDOWN", "M5-037", "0x0057AF58; 0x0057B044..0x0057B0C0", "Streamer destruction clears queued messages without sending a final flush. A missing per-entry discard log may mean the queue was empty."),
        new("TEARDOWN", "M5-059", "0x0057F376..0x0057F37A; 0x0057FC20..0x0057FC22; 0x0057FA14..0x0057FA2E", "Construction and destruction remove the existing TestAnim.json in the configured cache.")
    };
    public sealed record CheckGuide(string Purpose, string Automatic, string Observation, string[] Phases);
    public static CheckGuide Guide(string id) => id switch
    {
        "TAGS" => new("Check the identifiers attached to three animation requests.",
            "Record outgoing start identifiers and the robot's replies.",
            "NO ANSWER NEEDED. This does not judge how the selected animation looks.", Array.Empty<string>()),
        "ABORT" => new("Check the messages sent when an animation is cancelled partway through.",
            "Record the stop command and the messages before and after it.",
            "Watch the eyes, head and lifting arms after the stop request. Your description of the physical result is an observation.", new[] { "CANCEL" }),
        "KEEP_ALIVE" => new("Check animation traffic after cancellation, during live idle, and with persistent face layers.",
            "Record the cancellation tail, live-idle start/end order, and a separate face-only window.",
            "Watch the cancellation and then 10 seconds of wheel/body activity. No particular eye shape or random wiggle is a passing answer.", new[] { "CANCEL", "LIVE" }),
        "LOCKED_HEAD" => new("Check that a head-track lock is sent while animation head commands continue to stream.",
            "Record the disable/enable commands, animation head commands, and the robot's reported head state.",
            "Watch the angle of the whole head between START and END. Whether the robot physically holds it is retained for review.", new[] { "LOCKED" }),
        "AUDIO" => new("Check audio-message sizes and the engine's send-budget decisions.",
            "Record sample/silence messages and join them to the engine's send-gate observations.",
            "NO ANSWER NEEDED. This does not judge sound quality or certify the unfinished sound layers.", Array.Empty<string>()),
        "LAYERS" => new("Check messages generated by a brief face/backpack distortion request.",
            "Compare each captured layer frame with the engine's recorded face-output flag.",
            "Watch the face screen and backpack lights separately. Report any change; no exact appearance is prescribed.", new[] { "LAYERS" }),
        "LIVE_IDLE" => new("Record default idle head, lift and body commands and the head-angle input used.",
            "Capture 12 seconds of idle traffic and retain head-angle comparisons for review.",
            "NO ANSWER NEEDED. A random movement may not occur during this sample.", Array.Empty<string>()),
        "PARAMETERS" => new("Check that blink parameters are stored without a cap and used by the blink timer.",
            "Read back 44000/45000 and check recorded timer transitions at KeepFaceAlive calls.",
            "Watch the eyes for 60 seconds and report whether you saw at least two blinks. These parameter values do not promise a 44–45 second wall-clock gap.", new[] { "BLINKS" }),
        "DRIVING" => new("Check the animation phase requests around a short drive attempt.",
            "Record Start, conditional Loop and End requests together with their action results.",
            "NO ANSWER NEEDED. The requested distance is 6 cm. This check does not establish successful navigation.", Array.Empty<string>()),
        "SLEEP" => new("Record the sleep-deadline fallback and the known missing action path.",
            "Record the fallback request and its diagnostic.",
            "NO ANSWER NEEDED. Do not look for a sleeping expression; the missing action path remains open.", Array.Empty<string>()),
        "TEARDOWN" => new("Check pending-animation cleanup when the connection is disposed.",
            "Record the actual disposal interval, buffer-discard evidence and temporary animation-cache cleanup.",
            "NO ANSWER NEEDED. Post-disconnect appearance is not a shutdown verdict.", Array.Empty<string>()),
        _ => throw new ArgumentException("Unknown M5 check " + id)
    };
    public static string DescribeCheck(string id, bool includeSources = false)
    {
        var guide = Guide(id);
        return $"WHAT THIS TESTS: {guide.Purpose}\nRECORDED AUTOMATICALLY: {guide.Automatic}\nYOUR PART: {guide.Observation}\n"
            + (includeSources ? "SOURCE: " + string.Join("; ", SourceRules.Where(r => r.Check == id).Select(r => $"{r.Record} {r.Citation}")) : "");
    }
    public static void Preview(IEnumerable<CheckDef> checks)
    {
        Console.WriteLine("M5 TEST PREVIEW — no robot, resources or network required.\n"
            + "The live test checks source-backed messages/state and saves physical observations separately.\n"
            + "You are never asked whether Cozmo simply 'looks right'. Read each preview, press Enter when ready, and watch only from START to END.\n");
        foreach (var check in checks)
        {
            Console.WriteLine($"--- {check.Id} ---\n{DescribeCheck(check.Id, true)}");
            foreach (var phase in Guide(check.Id).Phases)
            {
                var plan = ObservationPlans.Single(p => p.Id == phase);
                Console.WriteLine($"\nGET READY TO WATCH: {phase}\nLook at {plan.Watch}.\n{plan.Expected}\n"
                    + "[Press Enter when ready]\n[START — watch Cozmo]\n[END — answer the previewed questions]\n");
            }
        }
    }
    // Harness reporting only: explicit cases prevent an OBSERVED_MALFORMED_* result being printed as a pass.
    public static string ResultKind(JsonObject result)
    {
        string verdict = result["verdict"]?.GetValue<string>() ?? "INCONCLUSIVE";
        string kind = verdict switch
        {
            "OBSERVED_INVALID_CANCEL_COMMAND" or "OBSERVED_MALFORMED_AUDIO_FRAMING"
                or "OBSERVED_COHERENT_ADMISSION_VIOLATION" or "OBSERVED_ADMISSION_ACCOUNTING_MISMATCH"
                or "OBSERVED_BLINK_CALL_MISMATCH" or "OBSERVED_MISSING_REQUIRED_FACE"
                or "OBSERVED_UNEXPECTED_FACE" or "OBSERVED_INVALID_BURST_ORDER"
                or "OBSERVED_UNEXPECTED_LAYER_LIFECYCLE" or "OBSERVED_STREAMER_FINAL_FLUSH"
                or "OBSERVED_MALFORMED_HEAD_KEYFRAME" or "OBSERVED_LOOP_AFTER_NONZERO_START"
                or "OBSERVED_END_COMPLETION_ORDER_MISMATCH" or "OBSERVED_EARLY_HEAD_ENABLE" => "MISMATCH",
            "OBSERVED_OUTBOUND_TAG_SEQUENCE" or "OBSERVED_CANCEL_COMMAND" or "OBSERVED_FF_BURST_ORDER"
                or "OBSERVED_PERSISTENT_FACE_WINDOW" or "OBSERVED_LOCKED_HEAD_EVIDENCE"
                or "OBSERVED_AUDIO_FRAMING_AND_ADMISSION" or "OBSERVED_CONDITIONAL_LAYER_FRAMES"
                or "OBSERVED_BLINK_CALL_TRANSITIONS" or "OBSERVED_SUCCESSFUL_START_LOOP_END_REQUESTS"
                or "OBSERVED_FAILED_START_END_CLEANUP" or "OBSERVED_DISPOSE_DISCARD_INTERVAL" => "MATCH",
            "KNOWN_GAP" => "KNOWN_GAP",
            "OBSERVED_LIVE_IDLE_MEASUREMENTS" or "OBSERVED_CAPTURE" => "OBSERVATION",
            _ => "INCOMPLETE"
        };
        // Parent checks retain their subchecks; an explicit contradiction must not disappear under INCONCLUSIVE.
        foreach (var child in result.Select(p => p.Value).OfType<JsonObject>())
            if (child["verdict"] is not null && ResultKind(child) == "MISMATCH") return "MISMATCH";
        if (result["phaseVerdict"]?.GetValue<string>() is "OBSERVED_LOOP_AFTER_NONZERO_START" or "OBSERVED_END_COMPLETION_ORDER_MISMATCH")
            return "MISMATCH";
        return kind;
    }
    public static string DescribeResult(JsonObject result)
    {
        string kind = ResultKind(result);
        string meaning = kind switch
        {
            "MATCH" => "Captured evidence matched the stated source-backed comparison.",
            "MISMATCH" => "Captured evidence contradicts a stated comparison; review the saved detail.",
            "KNOWN_GAP" => "Known missing implementation path recorded; this is not an engine-behavior pass.",
            "OBSERVATION" => "Measurements saved for review; no source-backed pass/fail comparison is claimed.",
            _ => "The required setup or evidence was incomplete; this does not establish an engine failure."
        };
        var reasons = new List<string>();
        void Collect(JsonObject row)
        {
            foreach (string name in new[] { "missing", "problems", "captureProblems", "admissionEvidenceMissing" })
            {
                if (row[name] is JsonValue value && value.TryGetValue<string>(out var note)) reasons.Add(note);
                else if (row[name] is JsonArray notes) reasons.AddRange(notes.OfType<JsonValue>().Select(n => n.ToString()));
            }
            foreach (var child in row.Select(p => p.Value).OfType<JsonObject>())
                if (child["verdict"] is not null) Collect(child);
        }
        Collect(result);
        string detail = string.Join(" ", reasons.Distinct().Take(6));
        var observations = new List<string>();
        foreach (var (field, label) in new[] {
            ("clipStopped", "Original animation stopped"), ("restingEyeAppearance", "Resting-eye comparison"),
            ("blinkObservation", "Blink seen"), ("eyeDartObservation", "Eye dart seen"), ("bodyObservation", "Body/wheel movement"),
            ("headStayedPut", "Head stayed at its starting angle"), ("faceObservation", "Face-picture change"),
            ("backpackObservation", "Backpack-light change"), ("observation", "At least two blinks seen") })
            if (result[field]?["answer"] is JsonValue answer) observations.Add(label + ": " + answer.ToString());
        foreach (string field in new[] { "observationWindow", "liveObservationWindow" })
            if (result[field]?["freeText"] is JsonValue note && !string.IsNullOrWhiteSpace(note.ToString()))
                observations.Add("Your notes: " + note.ToString());
        return $"{result["id"]}: {kind} — {meaning}" + (detail.Length == 0 ? "" : "\n  " + detail)
            + (observations.Count == 0 ? "" : "\n  Physical observations (not graded): " + string.Join("; ", observations));
    }
    public static string? CheckDefinitionProblem()
    {
        if (Checks.SelectMany(x => x.Records).Except(RecordIds).Any() || RecordIds.Except(Checks.SelectMany(x => x.Records)).Any()) return "check/record mismatch";
        if (Checks.Any(c => !SourceRules.Any(r => r.Check == c.Id))) return "check has no source/known-gap basis";
        if (SourceRules.Any(r => !Checks.Any(c => c.Id == r.Check && c.Records.Contains(r.Record))
            || string.IsNullOrWhiteSpace(r.Citation) || string.IsNullOrWhiteSpace(r.Comparison))) return "invalid or uncited comparison";
        return null;
    }
    public static List<JsonObject> LoadRecords(string path, List<string> warnings)
    {
        if (CheckDefinitionProblem() is { } problem) throw new InvalidOperationException(problem);
        var all = JsonNode.Parse(File.ReadAllText(path))!["records"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => x["id"]!.GetValue<string>());
        var missing = RecordIds.Where(id => !all.ContainsKey(id)).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException("Missing fidelity records: " + string.Join(", ", missing));
        foreach (var id in RecordIds) if (all[id]["status"]!.GetValue<string>() is "HARDWARE_ONLY" or "IMPLEMENTATION_GAP" or "RECOVERABLE_GAP" or "BLOCKED_EXTERNAL") warnings.Add(id + ": " + all[id]["status"] + " — " + all[id]["unresolved"]);
        return RecordIds.Select(id => (JsonObject)all[id].DeepClone()).ToList();
    }
    public static string NormaliseAnswer(string answer, string[] choices)
    {
        string normal = answer.Trim().Replace(' ', '-').Replace('_', '-');
        string found = M3M4Hw.NormaliseAnswer(normal, choices);
        return choices.Contains(found) ? found : answer.Trim();
    }
    public sealed record ObservationPlan(string Id, string Watch, string Expected);
    public static readonly ObservationPlan[] ObservationPlans = {
        new("CANCEL", "the eyes on the face screen, the whole head, and the front lifting arms",
            "This takes a few seconds. An animation starts, then the program asks it to stop after about three quarters of a second.\n"
            + "Keep looking at Cozmo until END. The animation's exact expression varies; you do not need to identify it.\n"
            + "Afterward you will answer FOUR separate questions:\n"
            + "  1. Did the animation that was playing stop, or did it keep going?\n"
            + "  2. Did the eyes return to shapes like the picture in neutral-reference.svg? Compare their outlines, not color or thin display lines.\n"
            + "  3. Did the eyes briefly close and open again? That is a BLINK.\n"
            + "  4. Did the eye shapes slide left/right or up/down inside the screen? That is an EYE DART, not the whole head moving.\n"
            + "A later blink or eye dart does not by itself mean the original animation continued. If you cannot distinguish them, choose unclear."),
        new("LIVE", "the wheels and the whole body",
            "Watch for 10 seconds. We will ask ONE question: did the wheels/body make any small wiggles?\n"
            + "Look for the body shifting or turning slightly, rather than a long drive. Head or lifting-arm movement alone does not count.\n"
            + "Ignore changes to the eye picture for this question. We will stop this part before asking for your answer."),
        new("LOCKED", "the angle of the whole head, not the eye picture",
            "Watch during a short animation, until END (usually a few seconds).\n"
            + "We will ask ONE question: did the whole head stay at the same angle it had at START?\n"
            + "The eye picture or lifting arms may move; those do not count as head movement.\n"
            + "Ignore any head motion before START and after the announced UNLOCK."),
        new("LAYERS", "the face screen and the five lights on top of the backpack",
            "This effect is brief. Position yourself so you can see both before starting.\n"
            + "We will ask TWO questions: did the face picture briefly shift or become distorted, and did the backpack lights change?\n"
            + "A face change means the picture on the screen changed, not that the whole head moved.\n"
            + "The face and lights need not change at the same instant. Watch until END; answer for each separately.\n"
            + "If you only managed to watch one, choose not-observed for the other."),
        new("BLINKS", "the eyes on the face screen",
            "Watch for 60 seconds. A BLINK is the eyes briefly closing and reopening.\n"
            + "Sideways/up-down eye shifts are darts, not blinks. Moving the whole head is not a blink either.\n"
            + "We will ask ONE question: did you see at least two blinks during this part?\n"
            + "If you use a clock, you can record the measured gaps in the notes afterward; timing is optional.\n"
            + "The engine timer advances per KeepFaceAlive call, so 44000/45000 does not promise a 44-45 second wall-clock gap.\n"
            + "The program checks the timer automatically. Your answer describes only what you saw; unsure or missed observations are useful too.")
    };
    public sealed record WireMessage(double AtMs, bool Outbound, byte[] Payload,
        int FrameLine = 0, int SubmessageOrdinal = 0, ushort? ReliableSequence = null, int ConnectionEpoch = 0, bool IsRetransmission = false);
    public static JsonObject GradeAbort(IEnumerable<WireMessage> messages, double cancelMs, double quietEndMs)
    {
        var all = M5HwAnalysis.OrderedUnique(messages).ToArray();
        var rows = all.Where(x => x.Outbound && x.AtMs >= cancelMs && x.AtMs <= quietEndMs && x.Payload.Length > 0).ToArray();
        int abortIndex = Array.FindIndex(rows, x => x.Payload[0] == 0x8D);
        var command = abortIndex < 0 ? null : rows[abortIndex];
        var prior = command is null ? null : all.TakeWhile(x => !ReferenceEquals(x, command))
            .LastOrDefault(x => x.Outbound && x.Payload.Length > 0 && x.Payload[0] is 0x9B or 0x9A or 0x8D);
        bool active = prior is not null && prior.Payload.Length == 2 && prior.Payload[0] == 0x9B;
        var backlog = abortIndex < 0 ? rows : rows.Take(abortIndex).ToArray();
        var after = abortIndex < 0 ? Array.Empty<WireMessage>() : rows.Skip(abortIndex + 1).ToArray();
        int audio = after.Count(x => x.Payload[0] is 0x8E or 0x8F), abort = rows.Count(x => x.Payload[0] == 0x8D),
            end = after.Count(x => x.Payload[0] == 0x9A), faces = after.Count(x => x.Payload[0] == (byte)RobotMessageId.AnimFaceImage);
        bool malformedAbort = rows.Any(x => x.Payload[0] == 0x8D && x.Payload.Length != 1);
        bool malformedTail = after.Any(x => (x.Payload[0] is 0x9A or 0x8F) && x.Payload.Length != 1 || x.Payload[0] == 0x8E && x.Payload.Length != 745);
        return new JsonObject
        {
            ["verdict"] = malformedAbort || abort > 1 ? "OBSERVED_INVALID_CANCEL_COMMAND"
                : active && command is not null && quietEndMs - command.AtMs >= 400
                && abort == 1 ? "OBSERVED_CANCEL_COMMAND" : "INCONCLUSIVE",
            ["abortCount"] = abort, ["endCount"] = end, ["faceCount"] = faces, ["audioFrameCount"] = audio,
            ["activeStreamObserved"] = active, ["malformedAbort"] = malformedAbort, ["malformedTail"] = malformedTail,
            ["cancelCallAtMs"] = cancelMs, ["wireAbort"] = command is null ? null : M5HwAnalysis.WireEvidence(new[] { command }),
            ["quietEndMs"] = quietEndMs, ["preCommandBacklog"] = M5HwAnalysis.WireEvidence(backlog),
            ["postAbortMessages"] = M5HwAnalysis.WireEvidence(after),
            ["tailAssessment"] = "NOT_JUDGED: End/Start/audio/face counts are retained observations. The neutral-replay gate uses unobserved last-stream time (0x0057CF6A..0x0057CFF2), so a 400 ms interval after captured Abort does not establish a quiet-tail expectation.",
            ["coverageRequirement"] = "Observe an active stream, one cancellation call and its outgoing command, then retain at least 400 ms after that command. This interval is a harness choice.",
            ["verdictRule"] = "M5-023 0x0057B3F2..0x0057B410 -> 0x00517DE4..0x00517E0A sends one empty-body AbortAnimation (0x8D) for cancellation. Reliable retransmissions count once. This verdict covers the cancellation command only; tail traffic and physical/firmware effects remain separate observations."
        };
    }
    public static JsonObject GradeTags(IEnumerable<WireMessage> messages)
    {
        var rows = messages.OrderBy(x => x.AtMs).ToArray();
        var startRows = rows.Where(x => x.Outbound && x.Payload.Length == 2 && x.Payload[0] == 0x9B && x.Payload[1] != 255).Take(3).ToArray();
        var starts = startRows.Select(x => (int)x.Payload[1]).ToArray();
        var echoes = rows.Where(x => !x.Outbound && x.Payload.Length == 2 && x.Payload[0] == (byte)RobotMessageId.AnimStarted).ToArray();
        var ends = rows.Where(x => !x.Outbound && x.Payload.Length == 2 && x.Payload[0] == (byte)RobotMessageId.AnimEnded).ToArray();
        bool ordered = startRows.All(s => echoes.Any(e => e.AtMs >= s.AtMs && e.Payload[1] == s.Payload[1] && ends.Any(end => end.AtMs >= e.AtMs && end.Payload[1] == s.Payload[1])));
        return new JsonObject { ["verdict"] = starts.SequenceEqual(new[] { 1, 2, 3 }) ? "OBSERVED_OUTBOUND_TAG_SEQUENCE" : "INCONCLUSIVE",
            ["starts"] = JsonSerializer.SerializeToNode(starts), ["startedEchoes"] = JsonSerializer.SerializeToNode(echoes.Select(x => (int)x.Payload[1])),
            ["endedEchoes"] = JsonSerializer.SerializeToNode(ends.Select(x => (int)x.Payload[1])), ["orderedMatchingEchoesObserved"] = ordered && startRows.Length == 3,
            ["echoAssessment"] = "NOT_JUDGED: robot start/end replies are retained observations; M5-036 HARDWARE_ONLY.",
            ["verdictRule"] = "Fresh connection, three explicit plays; M5-008 0x0057B660..0x0057B672 increments outgoing tags 1,2,3. Robot echoes do not gate this source-backed outbound comparison." };
    }
    public static int Run(string[] args)
    {
        try
        {
            string? root = LinkCheck.FindRepoRoot(); if (root is null) throw new ArgumentException("Run inside the repository.");
            bool validate = false, preview = false, yes = false, drive = false; string? resources = null, output = null; IPAddress ip = RobotAddress.DefaultFor(false); HashSet<string>? only = null;
            for (int i = 1; i < args.Length; i++) switch (args[i])
                {
                    case "--validate": validate = true; break;
                    case "--preview": preview = true; break;
                    case "--yes": yes = true; break;
                    case "--allow-drive": drive = true; break;
                    case "--resources": resources = args[++i]; break;
                    case "--out": output = args[++i]; break;
                    case "--only": only = args[++i].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(); break;
                    default: if (!IPAddress.TryParse(args[i], out var parsed)) throw new ArgumentException("Unknown argument " + args[i]); ip = parsed; break;
                }
            if (only is not null && (only.Count == 0 || only.Except(Checks.Select(x => x.Id)).Any())) throw new ArgumentException("Unknown or empty --only check list.");
            var warnings = new List<string>(); var records = LoadRecords(Path.Combine(root, "re-analysis", "fidelity_manifest.json"), warnings);
            if (validate) { Console.WriteLine($"M5-HW: {Checks.Length} checks, {records.Count} valid records; no network opened.\n" + string.Join("\n", warnings)); return 0; }
            if (preview) { Preview(Checks.Where(c => only is null || only.Contains(c.Id))); return 0; }
            var options = EngineStartupResources.OptionsForRoot(resources);
            string bundle = Path.Combine(output ?? Path.Combine(root, "re-analysis", "acceptance", "hardware"), DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-M5-HW-" + Guid.NewGuid().ToString("N")[..6]);
            return new M5Run(root, bundle, ip, options.ResourcesPath!, records, args, only, yes, drive).Execute();
        }
        catch (Exception e) { Console.Error.WriteLine("m5-hw: " + e.Message + "\nusage: m5-hw [robot-ip] --resources <dir> [--only CHECK,...] [--allow-drive] [--yes] [--out <parent>] | --validate | --preview"); return 2; }
    }
}

internal sealed class M5Run
{
    readonly string root, bundle, resources; readonly IPAddress ip; readonly List<JsonObject> records; readonly string[] argv; readonly HashSet<string>? only; readonly bool yes, drive;
    readonly Stopwatch clock = Stopwatch.StartNew(); readonly ConcurrentQueue<JsonObject> events = new(); readonly ConcurrentQueue<(double T, bool Out, byte[] Raw, Frame? Frame, string? Error)> frames = new();
    readonly List<JsonObject> results = new(); readonly ConcurrentQueue<string> errors = new(); readonly CancellationTokenSource cancel = new();
    // B-M5-32: read-only engine observations (AnimationScheduler.Diagnostic), enqueued only; stamped on this clock.
    readonly ConcurrentQueue<AnimationDiagnostic> diagnostics = new(); readonly long clockOrigin;
    CozmoRobot? robot; RobotConnectionResponse? response; bool disposed, disposeCompleted, connected; string cache = ""; bool cacheCtor;
    JsonObject neutralReference = new();
    JsonObject? activeObservationWindow;
    double Now => clock.Elapsed.TotalMilliseconds;
    public M5Run(string root, string bundle, IPAddress ip, string resources, List<JsonObject> records, string[] argv, HashSet<string>? only, bool yes, bool drive)
    { this.root = root; this.bundle = bundle; this.ip = ip; this.resources = resources; this.records = records; this.argv = argv; this.only = only; this.yes = yes; this.drive = drive; clockOrigin = Stopwatch.GetTimestamp() - clock.ElapsedTicks; }
    double StampMs(long timestamp) => (timestamp - clockOrigin) * 1000.0 / Stopwatch.Frequency;
    List<M5HwAnalysis.Diagnostic> Diagnostics() => diagnostics.ToArray().Select(d => new M5HwAnalysis.Diagnostic(StampMs(d.Timestamp), d)).ToList();
    void Event(string kind, JsonObject data) { data["atMs"] = Now; data["kind"] = kind; events.Enqueue(data); }
    void Wait(int ms) { if (cancel.Token.WaitHandle.WaitOne(ms)) cancel.Token.ThrowIfCancellationRequested(); }
    bool Poll(Func<bool> predicate, int ms) { double end = Now + ms; while (Now < end) { if (predicate()) return true; Wait(10); } return predicate(); }
    JsonObject Observe(string note) => new() { ["verdict"] = "OBSERVED_CAPTURE", ["verdictRule"] = note };
    JsonObject Missing(string note) => new() { ["verdict"] = "INCONCLUSIVE", ["missing"] = note };
    string? ReadLineBounded() { var read = Task.Run(Console.ReadLine); while (!read.IsCompleted) { if (cancel.IsCancellationRequested) return null; Thread.Sleep(50); } return read.GetAwaiter().GetResult(); }
    JsonObject BeginWindow(string check, string phase)
    {
        var plan = M5Hw.ObservationPlans.Single(p => p.Id == phase);
        Console.WriteLine($"\nGET READY TO WATCH: {phase}\nLook at {plan.Watch}.\n{plan.Expected}");
        if (phase == "CANCEL") Console.WriteLine("Eye-shape picture to compare with: " + Path.Combine(bundle, "neutral-reference.svg"));
        if (!yes)
        {
            Console.WriteLine("Read the instructions first. Get a clear view of Cozmo, then press Enter to start watching.");
            ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested();
        }
        double from = Now;
        var window = new JsonObject { ["check"] = check, ["phase"] = phase, ["watch"] = plan.Watch,
            ["expected"] = plan.Expected, ["fromMs"] = from };
        activeObservationWindow = window;
        Console.WriteLine($"START {phase} — WATCH COZMO NOW. Keep watching until END.");
        Event("observation-window-start", (JsonObject)window.DeepClone()); return window;
    }
    void EndWindow(JsonObject window)
    {
        if (ReferenceEquals(activeObservationWindow, window)) activeObservationWindow = null;
        if (window["toMs"] is not null) return;
        window["outcome"] ??= "completed";
        window["toMs"] = Now; Console.WriteLine($"END {window["phase"]} — you can look back at the screen. Answer only about what you just watched.");
        Event("observation-window-end", (JsonObject)window.DeepClone());
    }
    void ClosePendingWindow(string reason)
    {
        if (activeObservationWindow is not { } window) return;
        window["outcome"] = "incomplete"; window["reason"] = reason; EndWindow(window);
    }
    JsonObject AskWindow(JsonObject window, string question, params string[] choices)
    {
        if (window["toMs"] is null) throw new InvalidOperationException("Close the observation window before asking.");
        choices = choices.Concat(new[] { "unclear", "not-observed" }).Distinct().ToArray();
        string bounded = $"Think only about the part between START {window["phase"]} and END {window["phase"]}. {question}";
        Console.WriteLine("\n" + bounded);
        for (int i = 0; i < choices.Length; i++)
        {
            string meaning = choices[i] switch
            {
                "seen" => "I watched and saw it happen.",
                "not-seen" => "I watched, but did not see it happen.",
                "unclear" => "I watched, but could not tell or am not sure.",
                "not-observed" => "I missed this part or was looking somewhere else.",
                "stopped" => "The animation that was playing stopped.",
                "continued" => "The same animation kept going after the stop request.",
                "resembled-reference" => "The eye outlines looked like the reference picture.",
                "different" => "I could see the eye outlines, and they looked different from the reference picture.",
                "yes" => "Yes, I could tell that this happened.",
                "no" => "No, I could tell that this did not happen.",
                _ => choices[i]
            };
            Console.WriteLine($"  {i + 1}. {choices[i]} — {meaning}");
        }
        Console.WriteLine("Type a number or the choice name, then press Enter. It is OK to be unsure; please do not guess.");
        double asked = Now; string? raw = yes ? null : ReadLineBounded();
        var answer = M5HwAnalysis.ObservationAnswer(window, bounded, raw, choices, asked, Now);
        Event("operator-observation", (JsonObject)answer.DeepClone()); return answer;
    }
    void WindowNotes(JsonObject window)
    {
        Console.WriteLine("Anything else you noticed during that part? Describe it in your own words, or press Enter to skip.");
        window["freeText"] = yes ? null : ReadLineBounded();
        Event("operator-window-notes", (JsonObject)window.DeepClone());
    }
    void SaveNeutralReference()
    {
        var clip = robot!.Animations.Scheduler.NeutralFaceAnimation;
        var frame = clip?.Keyframes.OfType<FaceKeyframe>().FirstOrDefault();
        if (frame is null) { neutralReference = Missing("Loaded neutral clip/first face is unavailable; no invented appearance reference"); Console.WriteLine("Resting-eye reference unavailable; its visual comparison will be incomplete."); return; }
        byte[] canvas = ProceduralFaceRenderer.DrawFace(frame.Pose, 0);
        var svg = new System.Text.StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 128 64' width='768' height='384'><title>Engine-derived loaded neutral face, scanline parity0; reference only, not firmware display</title><rect width='128' height='64' fill='black'/><g fill='white'>");
        for (int y = 0; y < 64; y++) for (int x = 0; x < 128; x++)
            if (canvas[y * 128 + x] != 0) svg.Append($"<rect x='{x}' y='{y}' width='1' height='1'/>");
        svg.Append("</g></svg>"); string path = Path.Combine(bundle, "neutral-reference.svg"); File.WriteAllText(path, svg.ToString());
        neutralReference = new() { ["clip"] = clip!.Name, ["firstFaceTriggerMs"] = frame.TriggerTimeMs,
            ["file"] = "neutral-reference.svg", ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
            ["basis"] = "M5-010 loaded neutral clip's first ProceduralFace keyframe; existing M5-032 renderer, explicit parity0. This engine-derived grayscale shape reference does not prove firmware color, scanout or display." };
        Event("neutral-reference", (JsonObject)neutralReference.DeepClone()); Console.WriteLine("Resting-eye reference: " + path);
    }
    public int Execute()
    {
        Directory.CreateDirectory(bundle); cache = Path.Combine(bundle, "cache"); Directory.CreateDirectory(cache); File.WriteAllText(Path.Combine(cache, "TestAnim.json"), "{}");
        Console.WriteLine("M5 animation and face test\n"
            + "Put Cozmo on the FLOOR, off the charger, with at least 30 cm of clear space around him.\n"
            + "His eyes, head, lifting arms and backpack lights may move or change; sound may play.\n"
            + "With --allow-drive, his wheels may wiggle and one check attempts a short 6 cm drive.\n"
            + "Some checks are automatic: they say NO ANSWER NEEDED. You do not need to judge those.\n"
            + "For the parts you watch, read the instructions, then press Enter when ready. Look at Cozmo from START to END.\n"
            + "After END, answer the numbered questions. 'not-seen' means you watched but saw nothing; 'not-observed' means you missed it.\n"
            + "Choose 'unclear' if you watched but are unsure. These answers are useful observations, not a request to declare pass or fail.\n"
            + "Allow a few minutes plus time to read and answer. Ctrl+C stops the run and saves the evidence.\nBundle: " + bundle);

        ConsoleCancelEventHandler interrupt = (_, e) => { e.Cancel = true; cancel.Cancel(); }; Console.CancelKeyPress += interrupt;
        using var timer = new Timer(_ => cancel.Cancel(), null, TimeSpan.FromMinutes(10), Timeout.InfiniteTimeSpan);
        try
        {
            if (!yes) { Console.WriteLine("Press Enter when ready."); ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested(); }
            using var resolution = new HighResolutionTimer();
            robot = CozmoRobot.Create(engineOptions: new CozmoEngineOptions { ResourcesPath = resources, AnimationCachePath = cache, BeforeStart = e => { EngineEvidence.Subscribe(e, line => { }, Event); e.GoToSleepRequested += () => Event("go-to-sleep-requested", new()); e.ActionCompleted += a => Event("action-completed", JsonSerializer.SerializeToNode(a)!.AsObject()); } });
            cacheCtor = !File.Exists(Path.Combine(cache, "TestAnim.json"));
            robot.Transport.FrameTrace += f => frames.Enqueue((Now, f.Outbound, (byte[])f.Raw.Clone(), f.Frame, f.Error));
            robot.Animations.Scheduler.Diagnostic += diagnostics.Enqueue; // raised under the scheduler lock: enqueue only, never call back
            robot.Engine.ConnectionResponse += r => { response = r; Event("connection-response", new() { ["result"] = r.Result.ToString(), ["firmware"] = r.FwVersion }); };
            robot.HandlerFaulted += e => Event("handler-fault", new() { ["exception"] = e.ToString() });
            robot.Animations.Event += s => Event("animation-event", new() { ["text"] = s });
            robot.State.StateUpdated += s => Event("state", new() { ["headRad"] = s.HeadAngle, ["motionHeadRad"] = robot.Motion.HeadAngleRad, ["status"] = s.Status, ["streamBytes"] = robot.Animations.Scheduler.Stream.BytesStreamed, ["streamFrames"] = robot.Animations.Scheduler.Stream.FramesStreamed });
            robot.Message += m => { if (m is AnimationState a) Event("animation-state", new() { ["robotTimestamp"] = a.Timestamp, ["bytesPlayed"] = a.NumAnimBytesPlayed, ["audioPlayed"] = a.NumAudioFramesPlayed, ["tag"] = a.Tag, ["drops"] = a.ClientDropCount, ["enabledAnimTracks"] = a.EnabledAnimTracks, ["streamBytes"] = robot.Animations.Scheduler.Stream.BytesStreamed, ["streamFrames"] = robot.Animations.Scheduler.Stream.FramesStreamed }); };
            robot.ConnectToRobot(ip);
            connected = Poll(() => response?.Result == RobotConnectionResult.Success && robot.Engine.Robot is { TimeSynced: true, FirstFullStateHandled: true, ReadyToStream: true }, 15000);
            if (connected) { bool assets = Poll(() => robot.Animations.ClipNames.Count > 0, 15000); bool calibrated = Poll(() => robot.State.CalibrationComplete, 20000); Event("check-readiness", new() { ["assets"] = assets, ["calibrated"] = calibrated }); connected = assets && calibrated; }
            if (connected)
            {
                SaveNeutralReference();
                if (!yes && neutralReference["file"] is not null)
                {
                    Console.WriteLine("Before starting: open the neutral-reference.svg file shown above in your browser (you can paste its full path into the address bar).\n"
                        + "Keep that picture available. Later we will ask whether Cozmo's eyes return to similar outlines after an animation stops.\n"
                        + "Compare the shapes of the two eyes, not their color or thin display lines. The picture comes from the loaded animation; it is a reference, not a claim that the robot displayed it.\n"
                        + "Press Enter after you have looked at the picture.");
                    ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested();
                }
            }
            foreach (var def in M5Hw.Checks)
            {
                double start = Now; JsonObject result;
                if (only is not null && !only.Contains(def.Id)) result = Missing("not selected by --only");
                else if (!connected || robot.Transport.State != LinkState.Connected) result = Missing("connection/readiness unavailable");
                else if (cancel.IsCancellationRequested) result = Missing("run interrupted");
                else
                {
                    Console.WriteLine($"\n--- {def.Id} ---\n{M5Hw.DescribeCheck(def.Id)}");
                    Event("check-start", new() { ["check"] = def.Id });
                    try { result = Check(def.Id); ClosePendingWindow("Check returned without completing the marked phase."); }
                    catch (Exception e) { ClosePendingWindow(e.Message); errors.Enqueue(def.Id + ": " + e); result = Missing(e.Message); }
                }
                result["id"] = def.Id; result["records"] = JsonSerializer.SerializeToNode(def.Records); result["fromMs"] = start; result["toMs"] = Now;
                result["sourceComparisons"] = JsonSerializer.SerializeToNode(M5Hw.SourceRules.Where(r => r.Check == def.Id));
                result["coverageNote"] = "Local time windows, requested sample counts, track occurrences, required hooks and bounded waits are capture requirements, not guaranteed engine timing or behavior.";
                result["uncertainty"] = new JsonArray(records.Where(x => def.Records.Contains(x["id"]!.GetValue<string>()) && x["status"]!.GetValue<string>() is "HARDWARE_ONLY" or "BLOCKED_EXTERNAL" or "IMPLEMENTATION_GAP" or "RECOVERABLE_GAP").Select(x => x.DeepClone()).ToArray()); results.Add(result);
                result["resultKind"] = M5Hw.ResultKind(result); result["summary"] = M5Hw.DescribeResult(result);
                Console.WriteLine(result["summary"]!.GetValue<string>());
            }
        }
        catch (Exception e) { errors.Enqueue(e.ToString()); }
        finally { ClosePendingWindow("Run interrupted before phase completion."); DisposeBounded(); Console.CancelKeyPress -= interrupt; WriteBundle(); }
        Console.WriteLine("M5 observations saved: " + bundle + "\nNo record settled; return this whole folder for review.");
        return errors.Count == 0 && results.Count == 11 && results.All(x => M5Hw.ResultKind(x) is "MATCH" or "OBSERVATION" or "KNOWN_GAP") ? 0 : 1;
    }
    List<M5Hw.WireMessage> Wire(double from = 0, double to = double.MaxValue, bool includeRetransmissions = false)
    {
        return M5HwAnalysis.ReadWire(frames.ToArray().Select((f, index) =>
            new M5HwAnalysis.CapturedFrame(index + 1, f.T, f.Out, f.Frame)), includeRetransmissions)
            .Where(m => m.AtMs >= from && m.AtMs <= to).ToList();
    }
    AnimationClip? Clip(uint min = 1000, uint max = 10000, AnimationTrack required = AnimationTrack.Face)
    {
        var lib = robot!.Animations.Library; if (lib is null) return null;
        return lib.ClipNames.Select(lib.GetClip).Where(c => !c.LoadTruncated && c.DurationMs >= min && c.DurationMs <= max && (c.Tracks & AnimationTrack.Body) == 0 && (c.Tracks & required) == required).OrderBy(c => c.DurationMs).ThenBy(c => c.Name, StringComparer.Ordinal).FirstOrDefault();
    }
    AnimationTicket Play(AnimationClip clip) { Event("play-call", new() { ["clip"] = clip.Name, ["durationMs"] = clip.DurationMs, ["tracks"] = clip.Tracks.ToString() }); return robot!.Animations.PlayTracked(clip) ?? throw new InvalidOperationException("PlayTracked returned null"); }
    void Complete(AnimationTicket ticket, int timeout) { if (!Poll(() => ticket.Completion.IsCompleted, timeout)) { robot!.Animations.StopIfCurrent(ticket.Generation); throw new TimeoutException("Animation did not complete"); } Event("play-completed", new() { ["tag"] = ticket.Tag, ["result"] = ticket.Completion.GetAwaiter().GetResult().ToString() }); }
    void IdleOn() { robot!.Animations.Scheduler.PushIdleAnimation(AnimationTrigger.ProceduralLive, "m5-hw"); Event("idle-push", new()); }
    void IdleOff() { robot!.Animations.Scheduler.RemoveIdleAnimation("m5-hw", robot.Engine.Timer.SecondsF * 1000.0); robot.Animations.Stop(); Event("idle-remove", new()); }
    JsonObject Check(string id)
    {
        var r = robot!; double start = Now;
        switch (id)
        {
            case "TAGS":
                {
                    var c = Clip(300, 2000); if (c is null) return Missing("No short face clip without body track");
                    for (int i = 0; i < 3; i++) Complete(Play(c), (int)c.DurationMs + 5000);
                    Wait(1500); return M5Hw.GradeTags(Wire(start));
                }
            case "ABORT":
            case "KEEP_ALIVE":
                {
                    var c = Clip(4000, 12000); if (c is null) return Missing("No long clip without body track");
                    var cancelWindow = BeginWindow(id, "CANCEL");
                    var t = Play(c); Wait(750); double at = Now; Console.WriteLine("STOP REQUESTED — keep watching Cozmo: does the animation stop, and what do the eyes do next?"); Event("cancel-call", new() { ["tag"] = t.Tag }); r.Animations.Stop();
                    Poll(() => Wire(at).Any(x => x.Outbound && x.Payload[0] == 0x8D), 2000);
                    var wireAbort = Wire(at).FirstOrDefault(x => x.Outbound && x.Payload[0] == 0x8D);
                    double abortAt = wireAbort?.AtMs ?? Now, quietEnd = abortAt + 400;
                    if (Now < quietEnd) Wait((int)Math.Ceiling(quietEnd - Now));
                    var result = M5Hw.GradeAbort(Wire(start, quietEnd), at, quietEnd);
                    result["cancellationCommand"] = result.DeepClone(); Wait(1600);
                    result["robotCounterTail"] = new JsonArray(events.Where(e => e["kind"]?.GetValue<string>() == "animation-state" && e["atMs"]!.GetValue<double>() >= at && e["atMs"]!.GetValue<double>() <= abortAt + 2000).Select(e => e.DeepClone()).ToArray());
                    result["robotEchoTail"] = JsonSerializer.SerializeToNode(Wire(at, abortAt + 2000).Where(x => !x.Outbound && x.Payload[0] is (byte)RobotMessageId.AnimStarted or (byte)RobotMessageId.AnimEnded).Select(x => new { atMs = x.AtMs, payloadHex = Convert.ToHexString(x.Payload) }));
                    var keep = Wire(quietEnd, abortAt + 2000).Where(x => x.Outbound && x.Payload[0] is 0x9B or (byte)RobotMessageId.AnimFaceImage).ToArray();
                    result["keepAliveTail"] = JsonSerializer.SerializeToNode(keep.Select(x => new { atMs = x.AtMs, payloadHex = Convert.ToHexString(x.Payload) }));
                    if (id == "KEEP_ALIVE" && !keep.Any(x => x.Payload[0] == (byte)RobotMessageId.AnimFaceImage)) result["verdict"] = "INCONCLUSIVE";
                    EndWindow(cancelWindow); result["wireObservationVerdict"] = result["verdict"]!.DeepClone();
                    result["cancelAtMs"] = at; result["keepAliveWindowEndMs"] = Now;
                    result["neutralReference"] = neutralReference.DeepClone();
                    result["clipStopped"] = AskWindow(cancelWindow, "After STOP REQUESTED, did the animation that was playing stop? A separate blink or eye dart afterward does not by itself mean it continued.", "stopped", "continued");
                    result["restingEyeAppearance"] = AskWindow(cancelWindow, "After the stop request, did the two eye outlines look like the reference picture you opened? Compare the shapes, not color or thin display lines.", "resembled-reference", "different");
                    result["blinkObservation"] = AskWindow(cancelWindow, "Did you see a BLINK: the eyes briefly closing and opening again?", "seen", "not-seen");
                    result["eyeDartObservation"] = AskWindow(cancelWindow, "Did you see an EYE DART: the eye shapes sliding within the screen, without counting movement of the whole head?", "seen", "not-seen");
                    WindowNotes(cancelWindow); result["observationWindow"] = cancelWindow;
                    result["physicalObservationAssessment"] = "NOT_JUDGED: answers and reference availability describe the physical result; M5-036 does not establish a firmware appearance expectation.";
                    result["timingNote"] = "Quiet interval is 400 ms after actual wire Abort; full two-second tail retained. The 0.5 s neutral gate uses native last-stream engine time, unavailable through existing public observations; no cancel-time equality or exact neutral deadline is asserted.";
                    result["nativeLastStreamTime"] = null;
                    result["neutralCandidateStarts"] = M5HwAnalysis.WireEvidence(Wire(quietEnd, abortAt + 2000).Where(x => x.Outbound && x.Payload.Length == 2 && x.Payload[0] == 0x9B && x.Payload[1] != 255));
                    if (id == "KEEP_ALIVE")
                    {
                        if (!drive) { result["liveIdle"] = Missing("--allow-drive required for default body wiggles"); result["verdict"] = "INCONCLUSIVE"; }
                        else
                        {
                            var liveWindow = BeginWindow(id, "LIVE"); double live = Now; IdleOn();
                            try
                            {
                                Wait(10000); double liveEnd = Now;
                                var bursts = M5HwAnalysis.LiveBursts(Wire(live, liveEnd), live, liveEnd);
                                result["liveIdle"] = bursts;
                                if (bursts["verdict"]!.GetValue<string>() != "OBSERVED_FF_BURST_ORDER") result["verdict"] = "INCONCLUSIVE";
                                result["liveFromMs"] = live; result["liveToMs"] = liveEnd;
                            }
                            finally { EndWindow(liveWindow); IdleOff(); }
                            result["bodyObservation"] = AskWindow(liveWindow, "Did you see the wheels or whole body make small wiggles? Head or lifting-arm movement alone does not count.", "seen", "not-seen");
                            WindowNotes(liveWindow); result["liveObservationWindow"] = liveWindow;
                        }
                        Console.WriteLine("NO ANSWER NEEDED for this next part: a short setup animation plays, then the program records the face for about 10 seconds.");
                        // A short nonlive play clears the previous idle and its persistent keepalive layers.
                        // Normal KeepFaceAlive then supplies the persistent eye dart; no fabricated layer or hook.
                        var prime = Clip(300, 2000);
                        if (prime is null) { result["persistentFace"] = Missing("No short clip to prime the no-idle layer path"); result["verdict"] = "INCONCLUSIVE"; }
                        else
                        {
                            Complete(Play(prime), (int)prime.DurationMs + 5000);
                            var scheduler = r.Animations.Scheduler;
                            bool Preconditions() => !scheduler.IsPlaying
                                && scheduler.IdleStack.Last().Trigger == AnimationScheduler.IdleCount
                                && scheduler.FaceLayerNames.Contains("KeepAliveEyeDart");
                            if (!Poll(Preconditions, 5000)) { result["persistentFace"] = Missing("No-animation/no-idle persistent KeepAliveEyeDart prerequisites not observed"); result["verdict"] = "INCONCLUSIVE"; }
                            else
                            {
                                double layerFrom = Now; var samples = new JsonArray(); bool held = true;
                                Event("persistent-face-window-start", new());
                                do
                                {
                                    bool valid = Preconditions(); held &= valid;
                                    samples.Add(new JsonObject { ["atMs"] = Now, ["preconditionsHeld"] = valid,
                                        ["faceLayers"] = JsonSerializer.SerializeToNode(scheduler.FaceLayerNames) });
                                    Wait(60);
                                } while (Now - layerFrom < 10000);
                                double layerTo = Now; held &= Preconditions();
                                Event("persistent-face-window-end", new());
                                var persistent = M5HwAnalysis.PersistentFace(Wire(layerFrom, layerTo), layerFrom, layerTo, held);
                                persistent["fromMs"] = layerFrom; persistent["toMs"] = layerTo;
                                persistent["preconditionSamples"] = samples;
                                persistent["preconditionNote"] = "Existing public scheduler snapshots sampled about every 60 ms; not atomic instrumentation. No idle or explicit play is issued during this window. Review raw traffic for intervening activity.";
                                result["persistentFace"] = persistent;
                                if (persistent["verdict"]!.GetValue<string>() != "OBSERVED_PERSISTENT_FACE_WINDOW") result["verdict"] = "INCONCLUSIVE";
                            }
                        }
                    }
                    return result;
                }
            case "LOCKED_HEAD":
                {
                    var c = Clip(1500, 7000, AnimationTrack.Head); if (c is null) return Missing("No head clip without body track");
                    double lockAt = Now; Event("head-lock-request", new());
                    r.Motion.LockTracks(CozmoMotion.HeadTrack, "m5-hw", "hardware test"); Wait(300);
                    JsonObject window;
                    try { window = BeginWindow(id, "LOCKED"); }
                    catch { r.Motion.UnlockTracks(CozmoMotion.HeadTrack, "m5-hw"); throw; }
                    double clipFrom = window["fromMs"]!.GetValue<double>(), clipTo = clipFrom, unlockAt = clipFrom;
                    try { Complete(Play(c), (int)c.DurationMs + 5000); clipTo = Now; }
                    finally
                    {
                        EndWindow(window); clipTo = window["toMs"]!.GetValue<double>(); unlockAt = Now; Console.WriteLine("UNLOCK head now. Movement after this announcement is outside LOCKED observation.");
                        Event("head-unlock-request", new()); r.Motion.UnlockTracks(CozmoMotion.HeadTrack, "m5-hw");
                    }
                    Wait(300);
                    var result = M5HwAnalysis.LockedHead(Wire(start), lockAt, clipFrom, clipTo, unlockAt, Now);
                    result["wireObservationVerdict"] = result["verdict"]!.DeepClone();
                    result["headStayedPut"] = AskWindow(window, "Did the WHOLE HEAD stay at its starting angle until END? Ignore the eye picture, lifting arms, and any head movement after UNLOCK.", "yes", "no");
                    WindowNotes(window); result["observationWindow"] = window;
                    result["physicalObservationAssessment"] = "NOT_JUDGED: observed head motion is retained separately from outgoing lock/stream commands; M5-036 HARDWARE_ONLY.";
                    return result;
                }
            case "AUDIO":
                {
                    var c = Clip(5500, 15000, AnimationTrack.Audio); if (c is null) return Missing("No >=5.5 s audio clip without body track");
                    var sound = new[] { Path.Combine(resources, "sound"), Path.Combine(resources, "assets", "sound") }.FirstOrDefault(Directory.Exists);
                    string source = "none (silence only)"; if (sound is not null) { r.Animations.AudioSource = Cozmo.Robot.Animation.Wwise.WwiseAudioSource.Load(new[] { sound }); source = sound; }
                    double playStart = Now; Complete(Play(c), (int)c.DurationMs + 8000); double playEnd = Now; Wait(500);
                    var hooks = Diagnostics();
                    var result = M5HwAnalysis.AudioFromDiagnostics(Wire(), events, playStart, playEnd, hooks); result["playFromMs"] = playStart; result["playToMs"] = playEnd;
                    result["clip"] = c.Name; result["audioSource"] = source;
                    return result;
                }
            case "LAYERS":
                {
                    var c = Clip(300, 2000); if (c is null) return Missing("No short clip to clear persistent keepalive layers");
                    var sched = r.Animations.Scheduler; var previous = sched.DesiredFaceDistortion; float timeout = sched.KeepAliveTimeoutSec;
                    try
                    {
                        sched.KeepAliveTimeoutSec = 600; Complete(Play(c), (int)c.DurationMs + 5000);
                        if (!Poll(() => !sched.HaveLayersToSend, 3000)) return Missing("Existing layers did not drain after nonlive InitStream RemoveKeepFaceAlive(99)");
                        var layerWindow = BeginWindow(id, "LAYERS"); double layerStart = Now; int pulse = 1; sched.DesiredFaceDistortion = () => Interlocked.Exchange(ref pulse, 0) == 1 ? 0.8f : 0f;
                        if (!Poll(() => Volatile.Read(ref pulse) == 0, 2000)) return Missing("TLC did not consume distortion pulse");
                        Wait(200); bool drained = Poll(() => !sched.HaveLayersToSend, 5000); Wait(300);
                        double layerEnd = Now; EndWindow(layerWindow);
                        var hooks = Diagnostics(); var join = M5HwAnalysis.JoinSends(Wire(), hooks, layerStart, layerEnd);
                        var (activity, layerRows, activityProblem) = M5HwAnalysis.LayerActivity(join, hooks, layerStart, layerEnd);
                        var result = M5HwAnalysis.FiniteLayers(Wire(layerStart, layerEnd), layerStart, layerEnd, drained, activity, activityProblem);
                        result["sendJoin"] = join.Evidence; result["layerFrameHook"] = layerRows;
                        result["layerFromMs"] = layerStart; result["layerToMs"] = layerEnd;
                        result["stimulus"] = "Finite public distortion-provider pulse through TLC.Update; 600s harness timeout suppresses keepalive and short nonlive InitStream removes its persistent layers. Per-frame face flags come from the read-only StreamLayers hook (B-M5-32), joined to the wire by stream order; counts alone cannot certify the random finite-glitch trace.";
                        var persistent = results.FirstOrDefault(x => x["id"]?.GetValue<string>() == "KEEP_ALIVE")?["persistentFace"];
                        result["persistentFaceCoverage"] = persistent?.DeepClone() ?? Missing("Separate KEEP_ALIVE persistent-face observation not available in this subset");
                        sched.DesiredFaceDistortion = previous; sched.KeepAliveTimeoutSec = timeout;
                        result["faceObservation"] = AskWindow(layerWindow, "Did the picture on the face screen briefly shift or become distorted? Movement of the whole head does not count.", "seen", "not-seen");
                        result["backpackObservation"] = AskWindow(layerWindow, "Did any of the five lights on top of the backpack change? Answer separately from the face picture.", "seen", "not-seen");
                        WindowNotes(layerWindow); result["observationWindow"] = layerWindow;
                        return result;
                    }
                    finally { sched.DesiredFaceDistortion = previous; sched.KeepAliveTimeoutSec = timeout; }
                }
            case "LIVE_IDLE":
                {
                    if (!drive) return Missing("--allow-drive required: default live idle includes body wiggles");
                    double liveEnd = start; IdleOn(); try { Wait(12000); liveEnd = Now; } finally { IdleOff(); }
                    var result = M5HwAnalysis.LiveIdle(Wire(start, liveEnd), events, start, liveEnd);
                    result["uncalibratedCase"] = "Not forced on real hardware; covered by B-M5-23 regression. Any uncalibrated states retained in events."; return result;
                }
            case "PARAMETERS":
                {
                    var prime = Clip(300, 2000); if (prime is null) return Missing("No clip to prime keepalive blink path"); Complete(Play(prime), (int)prime.DurationMs + 5000);
                    double parameterAt = Now; Event("blink-parameters-request", new() { ["minMs"] = 44000, ["maxMs"] = 45000, ["resetExistingDeadline"] = false });
                    r.Animations.SetLiveIdleAnimationParameters(new[] { LiveIdleParam.BlinkSpacingMinTime_ms, LiveIdleParam.BlinkSpacingMaxTime_ms }, new[] { 44000f, 45000f }); Wait(300);
                    var result = Observe("M5-062 B4 base setter accepts 45000 uncapped. Min 44000 and max 45000 avoid the max<=min fallback and make long-spacing observation discriminating; first pre-existing blink deadline may remain. No movement idle is pushed.");
                    result["maxReadback"] = r.Animations.Scheduler.LiveIdleParameters[LiveIdleParam.BlinkSpacingMaxTime_ms]; result["minReadback"] = r.Animations.Scheduler.LiveIdleParameters[LiveIdleParam.BlinkSpacingMinTime_ms];
                    // The B4 write and KeepFaceAlive both hold the scheduler lock: a call stamped after this readback ran after the write.
                    double readbackAt = Now; result["readbackAtMs"] = readbackAt;
                    JsonObject blinkWindow;
                    try { blinkWindow = BeginWindow(id, "BLINKS"); }
                    catch { r.Animations.SetLiveIdleAnimationParameters(Array.Empty<LiveIdleParam>(), Array.Empty<float>(), true); throw; }
                    var layerSamples = new JsonArray();
                    try
                    {
                        for (int second = 0; second < 60; second++)
                        {
                            layerSamples.Add(new JsonObject { ["atMs"] = Now, ["engineSecondsSnapshot"] = r.Engine.Timer.SecondsF,
                                ["faceLayers"] = JsonSerializer.SerializeToNode(r.Animations.Scheduler.FaceLayerNames) });
                            Wait(1000);
                        }
                    }
                    finally { EndWindow(blinkWindow); r.Animations.SetLiveIdleAnimationParameters(Array.Empty<LiveIdleParam>(), Array.Empty<float>(), true); }
                    result["observation"] = AskWindow(blinkWindow, "Did you see at least TWO BLINKS during this part? If you timed any gaps, add the measured times in the notes. No particular wall-clock gap is required.", "yes", "no");
                    WindowNotes(blinkWindow); result["observationWindow"] = blinkWindow;
                    double blinkEnd = blinkWindow["toMs"]!.GetValue<double>();
                    var calls = M5HwAnalysis.BlinkCalls(Diagnostics(), readbackAt, blinkEnd);
                    result["parameterRequestAtMs"] = parameterAt;
                    result["existingDeadline"] = calls.Count > 0 ? calls[0].BeforeMs : null;
                    result["existingDeadlineNote"] = "Blink timer before the first KeepFaceAlive call after the readback (hook, 0x58D382..38C); B4 retains it.";
                    var evidence = M5HwAnalysis.Blinks(Wire(parameterAt, blinkEnd), parameterAt, blinkEnd,
                        result["minReadback"]!.GetValue<float>(), result["maxReadback"]!.GetValue<float>(), result["observation"]!.AsObject(), calls);
                    evidence["layerNameSnapshots"] = layerSamples;
                    evidence["snapshotNote"] = "Wall-clock/public engine-time/layer-name samples are not KeepFaceAlive call numbers or generation/deadline events. Calls come only from the read-only KeepFaceAlive hook, from the readback onward. Do not infer a call count from elapsed time or fabricate blink events from pixel darkness.";
                    result["blinkEvidence"] = evidence; result["verdict"] = evidence["verdict"]!.DeepClone();
                    return result;
                }
            case "DRIVING":
                {
                    if (!drive) return Missing("--allow-drive not given; driving skipped");
                    var m = new ManipulationSystem(r, r.Vision); var pose = m.RobotPose(); if (pose is null) { m.Dispose(); return Missing("No world pose for DriveToPose"); }
                    var goal = new Pose3d(Rotation3d.Identity, new Vec3(60, 0, 0), "m5-hw-goal", pose);
                    var action = new DriveToPoseAction(m) { Goal = goal }; using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token); limit.CancelAfter(15000);
                    var task = action.RunAsync(limit.Token); var states = new JsonArray(); string? last = null;
                    try
                    {
                        while (!task.IsCompleted && !limit.IsCancellationRequested) { string? current = r.Animations.Playing; if (current != last) { states.Add(new JsonObject { ["atMs"] = Now, ["playing"] = current, ["tag"] = r.Animations.CurrentTag }); last = current; } Wait(10); }
                        if (!task.IsCompleted) { limit.Cancel(); if (!task.Wait(2000)) { DisposeBounded(); connected = false; throw new TimeoutException("Drive cancellation did not complete; robot torn down before action cleanup"); } }
                        Wait(100);
                        var result = Observe("Actual DriveToPose.RunAsync on real ActionList, 60 mm forward from current pose; animation names/tags and completion time retained. The verdict covers M5-063 on whichever phase path the action results select, whatever the drive result (operator 2026-10-20); successful traversal belongs to the M12/M13 acceptance. M12/M8 gaps remain explicit; no successful move is called source proof.");
                        result["actionCompletions"] = new JsonArray(events.Where(e => e["kind"]?.GetValue<string>() == "action-completed" && e["atMs"]!.GetValue<double>() >= start).Select(e => e.DeepClone()).ToArray()); result["animationTransitions"] = states; result["driveCompletedAtMs"] = Now; result["result"] = task.IsCompletedSuccessfully ? task.Result.ToString() : "timed out/cancelled"; result["trace"] = JsonSerializer.SerializeToNode(action.Trace);
                        result["triplet"] = "default trigger ids 0x95 / 0x91 / 0x8D; no pushed overrides";
                        var phaseAnalysis = M5HwAnalysis.DrivingPhases(Wire(start), events, action.Tag, start, Now);
                        result["phaseAnalysis"] = phaseAnalysis;
                        result["phaseTriggerLogs"] = phaseAnalysis["phaseTriggerLogs"]!.DeepClone();
                        result["endEvidence"] = phaseAnalysis["endEvidence"]!.DeepClone();
                        result["endCompletionBeforeDrive"] = phaseAnalysis["endCompletionBeforeDrive"]!.DeepClone();
                        result["conditionalPhaseOrder"] = phaseAnalysis["conditionalPhaseOrder"]!.DeepClone();
                        result["orderedDefaultPhases"] = phaseAnalysis["loopRequired"]?.GetValue<bool>() == true
                            && phaseAnalysis["loopRequestObserved"]!.GetValue<bool>() && phaseAnalysis["conditionalPhaseOrder"]!.GetValue<bool>();
                        result["verdict"] = phaseAnalysis["verdict"]!.DeepClone();
                        result["loopRequestCovered"] = phaseAnalysis["loopRequestCovered"]!.DeepClone();
                        result["successfulTraversalCoverage"] = false; result["traversalJudgement"] = phaseAnalysis["traversalJudgement"]!.DeepClone();
                        return result;
                    }
                    finally { limit.Cancel(); if (!task.IsCompleted && !task.Wait(2000)) { DisposeBounded(); connected = false; } r.Motion.StopAllMotors(); if (task.IsCompleted || disposeCompleted) { action.Dispose(); goal.Dispose(); m.Dispose(); } else { errors.Enqueue("Drive action dependencies retained until process exit because teardown did not complete"); } }
                }
            case "SLEEP":
                {
                    r.Engine.StartIdleTimeout(0.2f, -1); Wait(1500); r.Engine.CancelIdleTimeout();
                    bool fallback = events.Any(e => e["kind"]?.GetValue<string>() == "go-to-sleep-requested" && e["atMs"]!.GetValue<double>() >= start);
                    bool missing = events.Any(e => e["atMs"]!.GetValue<double>() >= start
                        && e["kind"]?.GetValue<string>() == "stack-log"
                        && e["line"]?.GetValue<string>().StartsWith("MISSING: M5-038 -> M8 ", StringComparison.Ordinal) == true);
                    var sends = Wire(start).Where(x => x.Outbound && x.Payload[0] == 0x9B).ToArray();
                    var result = fallback && missing ? new JsonObject { ["verdict"] = "KNOWN_GAP",
                        ["verdictRule"] = "Host diagnostic coverage only: M5-038/M1-045 fallback and visible missing-path diagnostic. Native 0x0052CE54..0x0052CE6A uses the SleepTriggerFactory and queues the action; that production writer and M8 action path remain incomplete." }
                        : Missing("Host fallback or missing-path diagnostic was not captured; the known sleep-action gap remains.");
                    result["goToSleepRequested"] = fallback; result["missingDiagnostic"] = missing; result["animationStarts"] = sends.Length; return result;
                }
            case "TEARDOWN":
                {
                    var c = Clip(4000, 12000); if (c is null) return Missing("No long clip for Dispose-during-play"); Play(c); Wait(750); File.WriteAllText(Path.Combine(cache, "TestAnim.json"), "{}");
                    double requestAt = Now; Event("dispose-request", new()); DisposeBounded(); Thread.Sleep(1000);
                    var begin = events.LastOrDefault(e => e["kind"]?.GetValue<string>() == "dispose-begin");
                    var end = events.LastOrDefault(e => e["kind"]?.GetValue<string>() == "dispose-end");
                    if (begin is null || end is null) return Missing("No complete actual Dispose call interval; request/timeout is not destruction evidence");
                    double beginMs = begin["atMs"]!.GetValue<double>(), endMs = end["atMs"]!.GetValue<double>();
                    var result = M5HwAnalysis.Teardown(Wire(includeRetransmissions: true), events, beginMs, endMs, disposeCompleted);
                    result["disposeInterval"] = result.DeepClone();
                    result["intervalVerdict"] = result["verdict"]!.DeepClone(); result["disposeRequestAtMs"] = requestAt;
                    result["cacheRemovedOnConstruction"] = cacheCtor; result["cacheRemovedOnDispose"] = !File.Exists(Path.Combine(cache, "TestAnim.json"));
                    result["postDisposeOutboundDiagnostic"] = M5HwAnalysis.WireEvidence(Wire(endMs, endMs + 1000).Where(m => m.Outbound && m.AtMs > endMs));
                    if (!cacheCtor || File.Exists(Path.Combine(cache, "TestAnim.json"))) result["verdict"] = "INCONCLUSIVE";
                    return result;
                }
            default: return Missing("No implementation");
        }
    }
    void DisposeBounded()
    {
        if (disposed || robot is null) return; disposed = true; var r = robot;
        var task = Task.Run(() =>
        {
            bool success = false; Event("dispose-begin", new());
            try { r.Dispose(); success = true; return true; }
            catch (Exception e) { errors.Enqueue("Dispose: " + e); return false; }
            finally { Event("dispose-end", new() { ["success"] = success }); }
        });
        bool finished = task.Wait(5000); disposeCompleted = finished && task.GetAwaiter().GetResult();
        if (!finished) errors.Enqueue("Dispose did not finish in 5 seconds; destruction interval incomplete");
        Event("dispose-returned", new() { ["completed"] = finished, ["success"] = disposeCompleted });
    }
    void WriteBundle()
    {
        foreach (var def in M5Hw.Checks.Where(d => results.All(x => x["id"]?.GetValue<string>() != d.Id))) results.Add(new() { ["id"] = def.Id, ["records"] = JsonSerializer.SerializeToNode(def.Records), ["verdict"] = "INCONCLUSIVE", ["missing"] = "run terminated before check" });
        foreach (var result in results) { result["resultKind"] = M5Hw.ResultKind(result); result["summary"] = M5Hw.DescribeResult(result); }
        File.WriteAllText(Path.Combine(bundle, "SUMMARY.txt"), "M5 animation and face test\n"
            + "MATCH compares captured messages/state with a cited source rule. MISMATCH needs review. INCOMPLETE means setup/evidence was missing.\n"
            + "OBSERVATION and KNOWN_GAP are not engine-behavior passes. Physical answers never settle M5 or certify firmware behavior.\n\n"
            + string.Join("\n\n", results.Select(M5Hw.DescribeResult))
            + "\n\nRun errors:\n" + (errors.IsEmpty ? "None." : string.Join("\n", errors)));
        File.WriteAllLines(Path.Combine(bundle, "frames.jsonl"), frames.Select(f => new JsonObject { ["atMs"] = f.T, ["outbound"] = f.Out, ["rawBase64"] = Convert.ToBase64String(f.Raw), ["error"] = f.Error }.ToJsonString()));
        File.WriteAllLines(Path.Combine(bundle, "messages.jsonl"), Wire().Select(m => new JsonObject { ["atMs"] = m.AtMs, ["outbound"] = m.Outbound, ["tag"] = $"0x{m.Payload[0]:X2}", ["payloadHex"] = Convert.ToHexString(m.Payload), ["frameLine"] = m.FrameLine, ["submessageOrdinal"] = m.SubmessageOrdinal, ["reliableSequence"] = m.ReliableSequence, ["connectionEpoch"] = m.ConnectionEpoch }.ToJsonString()));
        File.WriteAllLines(Path.Combine(bundle, "events.jsonl"), events.Select(e => e.ToJsonString()));
        File.WriteAllLines(Path.Combine(bundle, "diagnostics.jsonl"), Diagnostics().Select(d => { var row = JsonSerializer.SerializeToNode(d.Observation, d.Observation.GetType())!.AsObject(); row["type"] = d.Observation.GetType().Name; row["atMs"] = d.AtMs; return row.ToJsonString(); }));
        var files = new JsonArray(); foreach (var relative in new[] { "cozmo-stack/src/Cozmo.Conformance/M5Hw.cs", "cozmo-stack/src/Cozmo.Conformance/M5HwAnalysis.cs", "cozmo-stack/src/Cozmo.Conformance/Program.cs", "re-analysis/acceptance/hardware/scripts/m5-hw-1.md", "re-analysis/fidelity_manifest.json", "re-analysis/research/20261019-m5-settling-b.md" })
        {
            string source = Path.Combine(root, relative); if (!File.Exists(source)) { errors.Enqueue("Missing provenance file " + relative); continue; }
            try { string dest = Path.Combine(bundle, "provenance", relative); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(source, dest, true); files.Add(new JsonObject { ["path"] = relative, ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant() }); } catch (Exception e) { errors.Enqueue("Provenance: " + relative + ": " + e.Message); }
        }
        var binaries = new JsonArray(); foreach (var assembly in new[] { typeof(M5Hw).Assembly, typeof(CozmoRobot).Assembly, typeof(RobotMessage).Assembly, typeof(ReliableTransport).Assembly })
        {
            try { string source = assembly.Location; string dest = Path.Combine(bundle, "binaries", Path.GetFileName(source)); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(source, dest, true); binaries.Add(new JsonObject { ["file"] = Path.GetFileName(source), ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant() }); } catch (Exception e) { errors.Enqueue("Binary provenance: " + e.Message); }
        }
        var resourceHashes = new JsonArray(); foreach (var directory in new[] { "animations", "animGroups", "sound" }.SelectMany(d => new[] { Path.Combine(resources, d), Path.Combine(resources, "assets", d) }).Where(Directory.Exists).Distinct())
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                try { using var stream = File.OpenRead(file); resourceHashes.Add(new JsonObject { ["path"] = Path.GetRelativePath(resources, file), ["sha256"] = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant() }); } catch (Exception e) { errors.Enqueue("Resource hash: " + file + ": " + e.Message); }
            }
        File.WriteAllText(Path.Combine(bundle, "resource-hashes.json"), resourceHashes.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        string? commit = null; try { var p = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true }); commit = p!.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(); } catch (Exception e) { errors.Enqueue("git: " + e.Message); }
        var report = new JsonObject { ["testId"] = M5Hw.TestId, ["createdUtc"] = DateTime.UtcNow.ToString("O"), ["commit"] = commit, ["argv"] = JsonSerializer.SerializeToNode(argv), ["resources"] = resources, ["allowDrive"] = drive, ["connected"] = connected, ["checks"] = JsonSerializer.SerializeToNode(results), ["records"] = JsonSerializer.SerializeToNode(records), ["provenance"] = files, ["binaries"] = binaries, ["errors"] = JsonSerializer.SerializeToNode(errors), ["acceptance"] = "NOT_JUDGED: operator observations and manager review required; hardware success does not raise provenance" };
        File.WriteAllText(Path.Combine(bundle, "result.json"), report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(bundle, "README.txt"), "M5-HW evidence bundle. result.json contains every check and source records; messages.jsonl deduplicates reliable resends, frames.jsonl retains every datagram; events.jsonl retains startup logs, API events and counters; diagnostics.jsonl retains the read-only engine hook observations (KeepFaceAlive, StreamLayers frames, send gate, budget refresh). Times are host capture times, not device execution times. No acceptance or manifest changes performed. See provenance script for limits.");
    }
}

