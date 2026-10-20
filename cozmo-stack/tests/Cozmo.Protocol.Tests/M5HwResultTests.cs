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
