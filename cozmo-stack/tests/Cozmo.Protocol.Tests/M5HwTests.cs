using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// reach: unit — offline checks of the hardware harness, with synthetic records and wire evidence.
/// These do not exercise a robot and do not establish source fidelity or hardware acceptance.
/// </summary>
public class M5HwTests
{
    private static string Manifest()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "re-analysis", "fidelity_manifest.json");
            if (File.Exists(path)) return path;
        }
        throw new InvalidOperationException("fidelity_manifest.json not found");
    }

    [Fact]
    public void EveryNamedRecordExistsAndItsCurrentEvidenceAndUncertaintyArePreserved()
    {
        var warnings = new List<string>();
        var records = M5Hw.LoadRecords(Manifest(), warnings);
        var current = JsonNode.Parse(File.ReadAllText(Manifest()))!["records"]!.AsArray()
            .ToDictionary(r => r!["id"]!.GetValue<string>(), r => r!);
        Assert.Equal(M5Hw.RecordIds.OrderBy(id => id),
            records.Select(r => r["id"]!.GetValue<string>()).OrderBy(id => id));
        foreach (var record in records)
        {
            string id = record["id"]!.GetValue<string>();
            Assert.True(JsonNode.DeepEquals(current[id], record), $"{id}: record evidence or uncertainty was changed");
        }
        Assert.Contains("M5-036", M5Hw.HardwareOnlyIds);
        Assert.All(records.Where(r => r["status"]!.GetValue<string>() == "HARDWARE_ONLY"),
            r => Assert.Contains(r["id"]!.GetValue<string>(), M5Hw.HardwareOnlyIds));
        Assert.Equal("HARDWARE_ONLY", current["M5-036"]["status"]!.GetValue<string>());
        foreach (var record in records.Where(r => r["status"]!.GetValue<string>() is
            "HARDWARE_ONLY" or "BLOCKED_EXTERNAL" or "RECOVERABLE_GAP" or "IMPLEMENTATION_GAP"))
            Assert.Contains(warnings, warning => warning.Contains(record["id"]!.GetValue<string>(), StringComparison.Ordinal));
    }

    [Fact]
    public void ElevenDistinctChecksNameOnlyRealRecordsAndCoverThePassBTopics()
    {
        // Acceptance requirement: settling pass B's numbered 11-item list, 2026-10-19.
        Assert.Equal(11, M5Hw.Checks.Length);
        Assert.Equal(11, M5Hw.Checks.Select(c => c.Id).Distinct().Count());
        Assert.Null(M5Hw.CheckDefinitionProblem());
        Assert.All(M5Hw.Checks, check =>
        {
            Assert.NotEmpty(check.Records);
            Assert.Equal(check.Records.Length, check.Records.Distinct().Count());
            Assert.All(check.Records, id => Assert.Contains(id, M5Hw.RecordIds));
        });
        foreach (string id in new[]
        {
            "M5-036", "M5-023", "M5-010", "M4-047", "M5-030", "M5-062", "M5-063",
            "M12-024", "M5-038", "M1-045", "M5-037", "M5-053", "M5-055", "M5-059"
        })
            Assert.Contains(M5Hw.Checks, check => check.Records.Contains(id));
    }

    [Fact]
    public void MissingManifestRecordsStopPreflightRatherThanDroppingChecks()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, new JsonObject
            {
                ["records"] = new JsonArray(new JsonObject { ["id"] = "M5-036", ["status"] = "HARDWARE_ONLY" })
            }.ToJsonString());
            var error = Assert.Throws<InvalidOperationException>(() => M5Hw.LoadRecords(path, new List<string>()));
            Assert.Contains("M5-023", error.Message);
            Assert.Contains("M5-062", error.Message);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("two bands", "two-bands")]
    [InlineData(" TWO-BANDS ", "two-bands")]
    [InlineData("2", "two-bands")]
    [InlineData("two", "two-bands")]
    [InlineData("t", "t")]
    [InlineData("", "")]
    [InlineData("unknown observation", "unknown observation")]
    public void OperatorAnswerNormalizationHandlesSpacesWithoutInventingAnAnswer(string typed, string expected)
    {
        Assert.Equal(expected, M5Hw.NormaliseAnswer(typed, new[] { "top", "two-bands", "other" }));
    }

    private static M5Hw.WireMessage Out(double at, params byte[] bytes) => new(at, true, bytes);
    private static M5Hw.WireMessage In(double at, params byte[] bytes) => new(at, false, bytes);
    private static string Verdict(JsonObject result) => result["verdict"]!.GetValue<string>();

    [Fact]
    public void DelayedLoopCompletionCannotBeMistakenForDrivingEndCompletion()
    {
        // The host loop report may follow the synchronous construction of the end action.
        // Require that end stream's matching robot echo before its successful host completion.
        var wire = new[] { Out(110, 0x9B, 8), In(150, 0xCB, 8) };
        JsonObject Completion(double at, uint result = 0) => new()
        {
            ["kind"] = "action-completed", ["ActionType"] = 26,
            ["Result"] = result, ["atMs"] = at
        };
        var delayedLoop = new[] { Completion(120) };
        Assert.False(M5HwAnalysis.DrivingEnd(wire, delayedLoop, 100, 200)["endCompletionBeforeDrive"]!.GetValue<bool>());
        var actualEnd = delayedLoop.Append(Completion(160));
        Assert.True(M5HwAnalysis.DrivingEnd(wire, actualEnd, 100, 200)["endCompletionBeforeDrive"]!.GetValue<bool>());
        Assert.False(M5HwAnalysis.DrivingEnd(wire, actualEnd, 100, 155)["endCompletionBeforeDrive"]!.GetValue<bool>());
        Assert.False(M5HwAnalysis.DrivingEnd(wire, delayedLoop.Append(Completion(160, 0x03000018)), 100, 200)["endCompletionBeforeDrive"]!.GetValue<bool>());
        Assert.False(M5HwAnalysis.DrivingEnd(new[] { wire[0], In(150, 0xCB, 7) }, actualEnd, 100, 200)["endCompletionBeforeDrive"]!.GetValue<bool>());
        Assert.False(M5HwAnalysis.DrivingEnd(Array.Empty<M5Hw.WireMessage>(), actualEnd, 100, 200)["endCompletionBeforeDrive"]!.GetValue<bool>());
    }

    [Fact]
    public void CancellationRequiresAnActivePlayAndACompleteObservationWindow()
    {
        // A22/A23/A25: one Abort, no End, buffered frame may follow. The harness must first
        // observe an active stream, or it has not exercised the claimed cancellation.
        var active = new[] { Out(10, 0x9B, 1), Out(100, 0x8D) };
        Assert.Equal("OBSERVED_CANCEL_COMMAND", Verdict(M5Hw.GradeAbort(active, 100, 500)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5Hw.GradeAbort(active, 100, 499)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5Hw.GradeAbort(Array.Empty<M5Hw.WireMessage>(), 100, 500)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5Hw.GradeAbort(new[] { Out(100, 0x8D) }, 100, 500)));
        var alreadyEnded = new[] { Out(10, 0x9B, 1), Out(50, (byte)RobotMessageId.AnimEndOfAnimation), Out(100, 0x8D) };
        Assert.Equal("INCONCLUSIVE", Verdict(M5Hw.GradeAbort(alreadyEnded, 100, 500)));
    }

    [Fact]
    public void CancellationCountsOnlyOutboundMessagesWithinTheQuietWindow()
    {
        var rows = new[]
        {
            Out(10, 0x9B, 1), Out(100, 0x8D), In(110, 0x8D),
            Out(501, (byte)RobotMessageId.AnimEndOfAnimation)
        };
        var result = M5Hw.GradeAbort(rows, 100, 500);
        Assert.Equal(1, result["abortCount"]!.GetValue<int>());
        Assert.Equal(0, result["endCount"]!.GetValue<int>());
        Assert.Equal("OBSERVED_CANCEL_COMMAND", Verdict(result));
        Assert.Equal("OBSERVED_INVALID_CANCEL_COMMAND", Verdict(M5Hw.GradeAbort(rows.Append(Out(120, 0x8D)), 100, 500)));
        Assert.Equal("OBSERVED_CANCEL_COMMAND", Verdict(M5Hw.GradeAbort(rows.Append(Out(500, (byte)RobotMessageId.AnimEndOfAnimation)), 100, 500)));
    }

    [Fact]
    public void MalformedAbortCannotStandInForTheEmptyBodyMessage()
    {
        // Generated serializer/CLAD AbortAnimation has zero body bytes; the payload includes its tag.
        var malformed = new[] { Out(10, 0x9B, 1), Out(100, 0x8D, 99) };
        Assert.Equal("OBSERVED_INVALID_CANCEL_COMMAND", Verdict(M5Hw.GradeAbort(malformed, 100, 500)));
    }

    [Fact]
    public void CancellationTailIsObservedWithoutAnUnmeasuredNeutralReplayDeadline()
    {
        // reach: unit; M5-023 0x57B3F2..410 sends Abort. M5-028 0x57CF90..BE gates neutral replay
        // by last-stream time, absent from these captures; tail counts cannot reject the command.
        var one = new[] { Out(10, 0x9B, 1), Out(100, 0x8D), Out(150, 0x8F) };
        Assert.Equal("OBSERVED_CANCEL_COMMAND", Verdict(M5Hw.GradeAbort(one, 100, 500)));
        Assert.Equal("OBSERVED_CANCEL_COMMAND", Verdict(M5Hw.GradeAbort(one.Append(Out(200, 0x8F)), 100, 500)));
        var replay = one.Append(Out(170, 0x9B, 2)).Append(Out(200, 0x8F));
        Assert.Equal("OBSERVED_CANCEL_COMMAND", Verdict(M5Hw.GradeAbort(replay, 100, 500)));
    }

    [Fact]
    public void OutgoingTagsAreIndependentOfFirmwareEchoObservations()
    {
        // M5 A9 at 0x0057B660..0x0057B672: fresh explicit plays allocate 1, 2, 3.
        var rows = new[]
        {
            Out(10, 0x9B, 1), In(11, 0xCA, 1), In(12, 0xCB, 1),
            Out(20, 0x9B, 255), In(21, 0xCA, 255),
            Out(30, 0x9B, 2), In(31, 0xCA, 2), In(32, 0xCB, 2),
            Out(40, 0x9B, 3), In(41, 0xCA, 3), In(42, 0xCB, 3)
        };
        Assert.Equal("OBSERVED_OUTBOUND_TAG_SEQUENCE", Verdict(M5Hw.GradeTags(rows)));
        Assert.Equal("OBSERVED_OUTBOUND_TAG_SEQUENCE", Verdict(M5Hw.GradeTags(rows.Where(x => x.Outbound))));
        Assert.False(M5Hw.GradeTags(rows.Where(x => x.Outbound))["orderedMatchingEchoesObserved"]!.GetValue<bool>());
        Assert.Equal("OBSERVED_OUTBOUND_TAG_SEQUENCE", Verdict(M5Hw.GradeTags(rows.Where(x => x.AtMs != 41))));
        Assert.Equal("INCONCLUSIVE", Verdict(M5Hw.GradeTags(Array.Empty<M5Hw.WireMessage>())));
        var premature = new[]
        {
            In(1, 0xCA, 1), In(2, 0xCA, 2), In(3, 0xCA, 3),
            Out(10, 0x9B, 1), In(12, 0xCB, 1),
            Out(20, 0x9B, 2), In(22, 0xCB, 2),
            Out(30, 0x9B, 3), In(32, 0xCB, 3)
        };
        Assert.Equal("OBSERVED_OUTBOUND_TAG_SEQUENCE", Verdict(M5Hw.GradeTags(premature)));
        Assert.False(M5Hw.GradeTags(premature)["orderedMatchingEchoesObserved"]!.GetValue<bool>());
    }

    [Fact]
    public void NonSequentialAndMalformedTagEvidenceIsInconclusive()
    {
        var reversed = new[]
        {
            Out(10, 0x9B, 1), In(11, 0xCA, 1), In(12, 0xCB, 1),
            Out(20, 0x9B, 3), In(21, 0xCA, 3), In(22, 0xCB, 3),
            Out(30, 0x9B, 2), In(31, 0xCA, 2), In(32, 0xCB, 2)
        };
        Assert.Equal("INCONCLUSIVE", Verdict(M5Hw.GradeTags(reversed)));
        var malformed = new[]
        {
            Out(10, 0x9B, 1), In(11, 0xCA, 1, 99), In(12, 0xCB, 1),
            Out(20, 0x9B, 2), In(21, 0xCA, 2), In(22, 0xCB, 2),
            Out(30, 0x9B, 3), In(31, 0xCA, 3), In(32, 0xCB, 3)
        };
        Assert.Equal("OBSERVED_OUTBOUND_TAG_SEQUENCE", Verdict(M5Hw.GradeTags(malformed)));
        Assert.False(M5Hw.GradeTags(malformed)["orderedMatchingEchoesObserved"]!.GetValue<bool>());
    }

    private static M5Hw.WireMessage[] AudioWire()
    {
        // Synthetic 30 Hz stream, 5.033 seconds. Every third frame is silence.
        return Enumerable.Range(0, 152).Select(i =>
        {
            byte[] payload = i % 3 == 0 ? new byte[] { 0x8F } : new byte[745];
            if (payload.Length > 1) payload[0] = 0x8E;
            return Out(i * (1000.0 / 30), payload);
        }).ToArray();
    }

    private static JsonObject AudioState(double at, int streamed, int played, int bytesStreamed, int bytesPlayed, int drops = 0) => new()
    {
        ["kind"] = "animation-state",
        ["atMs"] = at,
        ["streamFrames"] = streamed,
        ["audioPlayed"] = played,
        ["streamBytes"] = bytesStreamed,
        ["bytesPlayed"] = bytesPlayed,
        ["drops"] = drops
    };

    // reach: unit (synthetic capture/delivery snapshots; no actual admission epoch or hardware coverage).
    [Fact]
    public void AudioNeedsSamplesSilenceSufficientDurationAndCounterEvidence()
    {
        var wire = AudioWire();
        var states = new[] { AudioState(100, 1000, 990, 100000, 99000), AudioState(5000, 1150, 1136, 110000, 101808) };
        var measured = M5HwAnalysis.Audio(wire, states, 0, 6000);
        Assert.Equal("INCONCLUSIVE", Verdict(measured));
        Assert.Equal("OBSERVED_AUDIO_FRAMING_AND_COUNTERS", measured["framingVerdict"]!.GetValue<string>());
        Assert.Equal("NOT_JUDGED", measured["budgetCompliance"]!.GetValue<string>());
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.Audio(Array.Empty<M5Hw.WireMessage>(), states, 0, 6000)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.Audio(wire, Array.Empty<JsonObject>(), 0, 6000)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.Audio(wire.Where(m => m.Payload[0] == 0x8F), states, 0, 6000)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.Audio(wire.Where(m => m.Payload[0] == 0x8E), states, 0, 6000)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.Audio(wire, states, 0, 4999)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.Audio(wire, new[] { states[0] }, 0, 6000)));
    }

    [Fact]
    public void AudioMalformedSampleAndSilenceAreReported()
    {
        var wire = AudioWire();
        var states = new[] { AudioState(100, 10, 0, 1000, 0), AudioState(5000, 160, 150, 11000, 10000) };
        // M2 serializer: a sample carries 744 body bytes plus tag; silence carries only its tag.
        wire[1] = Out(wire[1].AtMs, 0x8E, 1);
        wire[3] = Out(wire[3].AtMs, 0x8F, 1);
        var result = M5HwAnalysis.Audio(wire, states, 0, 6000);
        Assert.Equal("OBSERVED_MALFORMED_AUDIO_FRAMING", Verdict(result));
        Assert.Equal(2, result["malformedAudioMessages"]!.GetValue<int>());
    }

    [Fact]
    public void AudioUsesLifetimeDifferencesAndExcludesEndBypassFromBudgetSnapshots()
    {
        var wire = AudioWire().Append(Out(5050, (byte)RobotMessageId.AnimEndOfAnimation));
        var states = new[]
        {
            AudioState(100, 1000, 990, 100000, 99000, 254),
            AudioState(5000, 1150, 1136, 110000, 101808, 255),
            AudioState(5100, 1166, 1151, 120000, 110000, 1)
        };
        var result = M5HwAnalysis.Audio(wire, states, 0, 6000);
        Assert.Equal(14, result["maximumFramesAhead"]!.GetValue<int>());
        Assert.Equal(8192, result["maximumBytesAhead"]!.GetValue<int>());
        Assert.Equal(0, result["snapshotsAbove14Frames"]!.GetValue<int>());
        Assert.Equal(0, result["snapshotsAbove8192Bytes"]!.GetValue<int>());
        Assert.Equal(161, result["audioPlayedDelta"]!.GetValue<int>());
        Assert.Equal(166, result["framesStreamedDelta"]!.GetValue<int>());
        Assert.True(result["dropCountChanged"]!.GetValue<bool>());
        Assert.False(result["counterRows"]![2]!["beforeFirstEnd"]!.GetValue<bool>());
        Assert.Equal(30.0, result["wireRateHz"]!.GetValue<double>(), 6);
    }

    private static JsonObject HeadState(double at) => new()
    {
        ["kind"] = "state",
        ["atMs"] = at,
        ["headRad"] = 0f,
        ["motionHeadRad"] = -25f * (MathF.PI / 180f),
        ["status"] = 0
    };

    [Fact]
    public void LiveIdleUsesMotionHeadAngleRatherThanRawReportAndPreservesOutliers()
    {
        // B-M5-23 join: raw head is 0 while Motion keeps its constructed -25-degree value.
        // ±6 is A34's default variability; pairing a capture is an observation, not a proof.
        var wire = new[]
        {
            Out(100, 0x93, 50, 0, unchecked((byte)-25)),
            Out(200, 0x93, 50, 0, unchecked((byte)-19)),
            Out(300, 0x93, 50, 0, unchecked((byte)-18)),
            Out(100, (byte)RobotMessageId.AnimBodyMotion, 0, 0, 0, 0),
            Out(100, (byte)RobotMessageId.AnimLiftHeight, 0, 0, 0)
        };
        var result = M5HwAnalysis.LiveIdle(wire, new[] { HeadState(90) }, 100, 400);
        Assert.Equal("OBSERVED_LIVE_IDLE_MEASUREMENTS", Verdict(result));
        Assert.Equal(-25, result["pairedHeadFrames"]![0]!["baseDegrees"]!.GetValue<int>());
        Assert.Equal(0, result["pairedHeadFrames"]![0]!["differenceDegrees"]!.GetValue<int>());
        Assert.Equal(1, result["outsideDefaultSixDegreeWindow"]!.GetValue<int>());
    }

    [Fact]
    public void LiveIdleMissingPrecedingStateOrEitherWiggleCannotBeComplete()
    {
        var wire = new[]
        {
            Out(100, 0x93, 50, 0, unchecked((byte)-25)),
            Out(110, (byte)RobotMessageId.AnimBodyMotion, 0, 0, 0, 0),
            Out(120, (byte)RobotMessageId.AnimLiftHeight, 0, 0, 0)
        };
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.LiveIdle(wire, new[] { HeadState(101) }, 0, 400)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.LiveIdle(wire, Array.Empty<JsonObject>(), 0, 400)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.LiveIdle(wire.Take(2), new[] { HeadState(90) }, 0, 400)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.LiveIdle(wire.Where(m => m.Payload[0] != (byte)RobotMessageId.AnimBodyMotion), new[] { HeadState(90) }, 0, 400)));
    }

    [Fact]
    public void LiveIdleMalformedHeadIsReportedEvenWhenAnotherFrameCanBePaired()
    {
        var wire = new[]
        {
            Out(100, 0x93, 50, 0, unchecked((byte)-25)),
            Out(110, 0x93, 50),
            Out(120, (byte)RobotMessageId.AnimBodyMotion, 0, 0, 0, 0),
            Out(130, (byte)RobotMessageId.AnimLiftHeight, 0, 0, 0)
        };
        var result = M5HwAnalysis.LiveIdle(wire, new[] { HeadState(90) }, 0, 400);
        Assert.Equal("OBSERVED_MALFORMED_HEAD_KEYFRAME", Verdict(result));
        Assert.Equal(1, result["malformedHeadFrames"]!.GetValue<int>());
    }

    [Fact]
    public void MalformedBodyAndLiftMessagesDoNotCountAsObservedWiggles()
    {
        var wire = new[]
        {
            Out(100, 0x93, 50, 0, unchecked((byte)-25)),
            Out(120, (byte)RobotMessageId.AnimBodyMotion),
            Out(130, (byte)RobotMessageId.AnimLiftHeight)
        };
        var result = M5HwAnalysis.LiveIdle(wire, new[] { HeadState(90) }, 0, 400);
        Assert.Equal("INCONCLUSIVE", Verdict(result));
        Assert.Equal(0, result["bodyFrames"]!.GetValue<int>());
        Assert.Equal(0, result["liftFrames"]!.GetValue<int>());
    }
}
