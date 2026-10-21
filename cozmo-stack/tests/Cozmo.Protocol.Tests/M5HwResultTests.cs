using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class M5HwResultTests
{
    // reach: unit (synthetic capture results; verifies harness reporting, not robot behavior).
    [Theory]
    [InlineData("OBSERVED_INVALID_CANCEL_COMMAND")]
    [InlineData("OBSERVED_MALFORMED_AUDIO_FRAMING")]
    [InlineData("OBSERVED_COHERENT_ADMISSION_VIOLATION")]
    [InlineData("OBSERVED_BLINK_CALL_MISMATCH")]
    [InlineData("OBSERVED_EARLY_HEAD_ENABLE")]
    [InlineData("OBSERVED_STREAMER_FINAL_FLUSH")]
    public void ObservedContradictionsCannotBeReportedAsMatches(string verdict)
    {
        var result = new JsonObject { ["id"] = "test", ["verdict"] = verdict };
        Assert.Equal("MISMATCH", M5Hw.ResultKind(result));
        var parent = new JsonObject { ["id"] = "parent", ["verdict"] = "INCONCLUSIVE", ["child"] = result };
        Assert.Equal("MISMATCH", M5Hw.ResultKind(parent));
    }

    // reach: unit (synthetic outgoing M5-023 cancellation 0x57B3F2..410; physical answers have no source oracle).
    [Theory]
    [InlineData("continued", "different", "not-seen")]
    [InlineData("unclear", "not-observed", "not-observed")]
    public void PhysicalAnswersRemainObservationsAlongsideTheCommandComparison(string stopped, string eyes, string body)
    {
        var result = M5Hw.GradeAbort(new[] {
            new M5Hw.WireMessage(10, true, new byte[]{0x9B,1}),
            new M5Hw.WireMessage(100, true, new byte[]{0x8D}) }, 100, 500);
        result["clipStopped"] = new JsonObject { ["answer"] = stopped };
        result["restingEyeAppearance"] = new JsonObject { ["answer"] = eyes };
        result["bodyObservation"] = new JsonObject { ["answer"] = body };
        Assert.Equal("MATCH", M5Hw.ResultKind(result));
        Assert.Equal(stopped, result["clipStopped"]!["answer"]!.GetValue<string>());
    }

    // reach: unit (synthetic partial capture; native M2-008/009 sizes0x7BCDAA/0x7BCE8A are independent of coverage).
    [Fact]
    public void MalformedAudioIsStillAContradictionWhenOtherCoverageIsMissing()
    {
        var result = M5HwAnalysis.Audio(new[] { new M5Hw.WireMessage(1, true, new byte[]{0x8E,1}) },
            Array.Empty<JsonObject>(), 0, 2);
        Assert.Equal("OBSERVED_MALFORMED_AUDIO_FRAMING", result["verdict"]!.GetValue<string>());
        Assert.NotEmpty(result["missing"]!.AsArray());
        Assert.Equal("MISMATCH", M5Hw.ResultKind(result));
    }

    // reach: unit (partial capture; M5-0300x4F8C12..4A head packet shape independent of random track coverage).
    [Fact]
    public void MalformedIdleHeadIsStillAContradictionWithoutOtherTracks()
    {
        var result = M5HwAnalysis.LiveIdle(new[] { new M5Hw.WireMessage(1, true, new byte[]{0x93,1}) },
            Array.Empty<JsonObject>(), 0, 2);
        Assert.Equal("OBSERVED_MALFORMED_HEAD_KEYFRAME", result["verdict"]!.GetValue<string>());
        Assert.NotEmpty(result["missing"]!.AsArray());
        Assert.Equal("MISMATCH", M5Hw.ResultKind(result));
    }

    // reach: unit (synthetic rows shaped like run 20261020-200624-M5-HW-97161e, whose TEARDOWN and DRIVING summaries
    // printed no reason; research/20261020-m5-teardown-sends.md, research/20261020-m5-drive-cliff-stop.md).
    [Fact]
    public void InconclusiveTeardownAndDrivingRowsAlwaysPrintAReason()
    {
        var wire = new[] { new M5Hw.WireMessage(110, true, new byte[] { 0x8E }, 5, 0, 10) };
        var marker = new JsonObject { ["kind"] = "engine-log", ["name"] = "AnimationStreamer.ClearSendBuffer", ["text"] = "Clearing unsent animFaceImage", ["atMs"] = 115d };
        var teardown = M5HwAnalysis.Teardown(wire, new[] { marker }, 100, 120, true);
        teardown["id"] = "TEARDOWN"; teardown["disposeInterval"] = teardown.DeepClone();
        string text = M5Hw.DescribeResult(teardown);
        Assert.Contains("COULD NOT CHECK", text);
        Assert.Contains("1 first send(s) during Dispose have no enqueue origin.", text);

        // DRIVING as the run wrote it: no top-level reason; the only one sits in phaseAnalysis.endEvidence (no verdict).
        var endEvidence = new JsonObject { ["endCompletionBeforeDrive"] = false, ["missing"] = "End action identity or drive completion unavailable/ambiguous." };
        var driving = new JsonObject { ["id"] = "DRIVING", ["verdict"] = "INCONCLUSIVE",
            ["phaseAnalysis"] = new JsonObject { ["verdict"] = "INCONCLUSIVE", ["phaseVerdict"] = "INCONCLUSIVE_PHASE_IDENTITY", ["endEvidence"] = endEvidence },
            ["endEvidence"] = endEvidence.DeepClone() };
        string drivingText = M5Hw.DescribeResult(driving);
        Assert.Contains("End action identity or drive completion unavailable/ambiguous.", drivingText);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(drivingText, "unavailable/ambiguous"));
    }

    // reach: unit (synthetic events with the run's log names; ActionList::Cancel's 0x02000000 at 0x00540A3C..0x00540A40,
    // RobotStopped -> ActionList::Cancel(-1) at 0x0053542E..0x00535480; research/20261020-m5-drive-cliff-stop.md).
    [Fact]
    public void ACancelledDriveAfterARobotStopIsExplainedNotPassed()
    {
        JsonObject Log(double at, string name, string text) => new() { ["kind"] = "engine-log", ["atMs"] = at, ["name"] = name, ["text"] = text };
        JsonObject Done(double at, uint tag, int type, uint result) => new() { ["kind"] = "action-completed", ["atMs"] = at, ["Tag"] = tag, ["ActionType"] = type, ["Result"] = result };
        var events = new List<JsonObject> {
            Log(10, "GetResponseForAnimationTrigger.Found", "DriveStartDefault -> ag_start"),
            Log(11, "IAction.Update.PreconditionsMet", "Preconditions for PlayAnimationag_start [100] successfully met."),
            Log(50, "PathComponent.ReceivedPathEvent", "ID:1 Event:PATH_INTERRUPTED"),
            Log(51, "RobotImplMessaging.HandleRobotStopped", "1"),
            Done(60, 100, 26, 0x02000000), Done(61, 99, 12, 0x02000000) };
        var phase = M5HwAnalysis.DrivingPhases(Array.Empty<M5Hw.WireMessage>(), events, 99, 0, 100);
        Assert.Equal("INCONCLUSIVE", phase["verdict"]!.GetValue<string>());
        // RobotStopped without a cliff trace: the cancel path is named, the cause is not.
        string stopOnly = phase["explanation"]!.GetValue<string>();
        Assert.StartsWith("The robot sent RobotStopped (cause not logged)", stopOnly);
        var row = new JsonObject { ["id"] = "DRIVING", ["verdict"] = phase["verdict"]!.DeepClone(), ["phaseAnalysis"] = phase };
        Assert.Equal("INCOMPLETE", M5Hw.ResultKind(row));
        Assert.Contains(stopOnly, M5Hw.DescribeResult(row));
        // With the firmware cliff trace the cliff is named, and whether it was real stays open.
        var withCliff = events.Append(new JsonObject { ["kind"] = "stack-log", ["atMs"] = 49d,
            ["line"] = "Robot.Trace [ProxSensors.UpdateCliff.StoppingDueToCliff] [1]" }).ToList();
        Assert.StartsWith("The robot reported a cliff stop", M5HwAnalysis.DrivingPhases(Array.Empty<M5Hw.WireMessage>(), withCliff, 99, 0, 100)["explanation"]!.GetValue<string>());
        // PATH_INTERRUPTED alone names no cancel path: no explanation.
        var pathOnly = events.Where(e => e["name"]?.GetValue<string>() != "RobotImplMessaging.HandleRobotStopped").ToList();
        Assert.Null(M5HwAnalysis.DrivingPhases(Array.Empty<M5Hw.WireMessage>(), pathOnly, 99, 0, 100)["explanation"]);
        // A drive that was not cancelled gets no explanation, even with the stop logs present.
        events[^1] = Done(61, 99, 12, 0);
        Assert.Null(M5HwAnalysis.DrivingPhases(Array.Empty<M5Hw.WireMessage>(), events, 99, 0, 100)["explanation"]);
    }

    // reach: unit (harness order; research/20261020-m5-liveidleturn-linger.md: the engine keeps the LiveIdleTurn eye
    // shift after live idle, 0x0057D606..0x0057D60C, 0x0057D8C6..0x0057D8D6, so layer-free face checks come first).
    [Fact]
    public void LayerFreeFaceChecksRunBeforeAnyLiveIdle()
    {
        Assert.Equal(new[] { "TAGS", "ABORT", "LAYERS", "KEEP_ALIVE", "LOCKED_HEAD", "AUDIO", "LIVE_IDLE", "PARAMETERS", "DRIVING", "SLEEP", "TEARDOWN" },
            M5Hw.Checks.Select(c => c.Id));
        Assert.Equal(new[] { "CANCEL", "LIVE" }, M5Hw.Guide("KEEP_ALIVE").Phases);
        Assert.Contains("then 10 s of face-only idle, then 10 s of idle wiggles", M5Hw.Guide("KEEP_ALIVE").CozmoDoes);
        Assert.Contains("the face still carries the LiveIdleTurn eye shift from an earlier live idle; the engine keeps it (research/20261020-m5-liveidleturn-linger.md)",
            M5Hw.LiveIdleTurnNote(new[] { "LiveIdleTurn" }));
        Assert.Equal("", M5Hw.LiveIdleTurnNote(new[] { "KeepAliveEyeDart" }));
        var preview = new StringWriter(); M5Hw.Preview(M5Hw.Checks, preview);
        string text = preview.ToString();
        int cancel = text.IndexOf("GET READY TO WATCH: the eyes", StringComparison.Ordinal), faceOnly = text.IndexOf(M5Hw.KeepAliveFaceOnlyLine, StringComparison.Ordinal),
            wheels = text.IndexOf("GET READY TO WATCH: the wheels", StringComparison.Ordinal);
        Assert.True(cancel >= 0 && cancel < faceOnly && faceOnly < wheels);
    }

    // reach: unit (reporting helper only).
    [Fact]
    public void AddMissingKeepsEveryReason()
    {
        var row = new JsonObject();
        M5Hw.AddMissing(row, "a"); Assert.Equal("a", row["missing"]!.GetValue<string>());
        M5Hw.AddMissing(row, "b"); Assert.Equal(new[] { "a", "b" }, row["missing"]!.AsArray().Select(n => n!.GetValue<string>()));
        M5Hw.AddMissing(row, "c"); Assert.Equal(3, row["missing"]!.AsArray().Count);
    }

    // reach: unit (harness metadata only; prevents uncited source comparisons or stale fidelity references).
    [Fact]
    public void EveryCheckHasARecordedSourceOrKnownGapBasis()
    {
        Assert.Null(M5Hw.CheckDefinitionProblem());
        foreach (var check in M5Hw.Checks)
            Assert.All(M5Hw.SourceRules.Where(r => r.Check == check.Id), rule => {
                Assert.Contains(rule.Record, check.Records);
                Assert.Contains("0x", rule.Citation);
                Assert.NotEmpty(rule.Comparison);
            });
    }
}
