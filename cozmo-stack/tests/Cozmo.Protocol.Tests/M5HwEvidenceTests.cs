using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Robot.Animation;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class M5HwEvidenceTests
{
    private static M5HwAnalysis.Diagnostic Gate(double at, long sequence, byte id, int size,
        int bytes, int frames, bool sent = true, bool direct = false) => new(at,
            new SendGateObservation(0, sequence, 1, id, size, id is 0x8E or 0x8F,
                bytes, frames, !direct, sent, direct));

    private static M5HwAnalysis.Diagnostic Refresh() => new(0,
        new BudgetRefreshObservation(0, 1, 0, 0, 0, 0, 8192, 14));

    // reach: unit (review F1's tag/size wire and explicit layer/gate fixtures replace the scheduler and transport;
    // exercises the same JoinSends -> LayerActivity -> FiniteLayers path as the harness).
    [Fact]
    public void AnUnjoinedObservedLayerFrameCannotBeHiddenByCompleteSurvivingAudioCoverage()
    {
        byte[][] payloads = { new byte[] { 0x8F }, new byte[] { 0x9B, 1 },
            new byte[] { 0x98 }, new byte[] { 0x97, 0 }, new byte[] { 0x9A } };
        var wire = payloads.Select((b, i) => new M5Hw.WireMessage(10 + i, true, b, i + 1, 0)).ToArray();
        var hooks = payloads.Select((b, i) => Gate(9.9 + i, i + 1, b[0], b.Length, 8192, 14)).ToList();
        hooks.Add(new(9, new LayerFrameObservation(0, 1, 0, true, true, 1, 4)));
        var join = M5HwAnalysis.JoinSends(wire, hooks, 0, 20);
        Assert.Null(join.Problem);
        var (activity, _, problem) = M5HwAnalysis.LayerActivity(join, hooks, 0, 20);
        Assert.Null(problem);
        Assert.Equal("OBSERVED_CONDITIONAL_LAYER_FRAMES",
            M5HwAnalysis.FiniteLayers(wire, 0, 20, true, activity, problem)["verdict"]!.GetValue<string>());

        hooks.Add(new(15, new LayerFrameObservation(0, 2, 33, false, false, 99, null)));
        join = M5HwAnalysis.JoinSends(wire, hooks, 0, 20);
        (activity, var rows, problem) = M5HwAnalysis.LayerActivity(join, hooks, 0, 20);
        Assert.Null(join.Problem);
        Assert.Single(activity!);
        Assert.Equal(2, rows.Count);
        Assert.Equal("1 StreamLayers frame(s) could not be joined to the wire.", problem);
        var result = M5HwAnalysis.FiniteLayers(wire, 0, 20, true, activity, problem);
        Assert.Equal("INCONCLUSIVE", result["verdict"]!.GetValue<string>());
        Assert.False(result["faceActivityEstablished"]!.GetValue<bool>());
        Assert.Equal(problem, result["missing"]!.GetValue<string>());
    }

    // reach: unit (checked F2's explicit 0/1/3 ms observations; native C9 initial8192/14 and C14 costs1/1).
    [Fact]
    public void MidEpochWindowUsesEarlierSpendingWithoutReportingItAsWindowCoverage()
    {
        var hooks = new[] { Refresh(), Gate(1, 1, 0x8F, 1, 8192, 14), Gate(3, 2, 0x8F, 1, 8191, 13) };
        var samples = M5HwAnalysis.Admissions(hooks, 2, 4);
        var prefix = M5HwAnalysis.Admissions(hooks, 0, 1);
        var result = M5HwAnalysis.BudgetSamples(samples, M5HwAnalysis.Refreshes(hooks), prefix);
        Assert.Equal("OBSERVED_COHERENT_SAMPLES_WITHIN_GATE", result["coherentSampleVerdict"]!.GetValue<string>());
        Assert.Equal(0, result["allowanceAccountingMismatches"]!.GetValue<int>());
        Assert.Equal(0, result["accountingUnverifiedSamples"]!.GetValue<int>());
        Assert.Equal(3d, Assert.Single(result["samples"]!.AsArray())!["atMs"]!.GetValue<double>());
        Assert.Equal(1, result["coherentSamples"]!.GetValue<int>());
    }

    // reach: unit (omits the required 1 ms prefix, rather than inventing zero earlier spending).
    [Fact]
    public void UnavailablePrefixIsUnverifiedRatherThanAnEngineAccountingMismatch()
    {
        var hooks = new[] { Refresh(), Gate(3, 2, 0x8F, 1, 8191, 13) };
        var samples = M5HwAnalysis.Admissions(hooks, 2, 4);
        foreach (var prefix in new IReadOnlyList<M5HwAnalysis.BudgetAdmission>?[] { null, Array.Empty<M5HwAnalysis.BudgetAdmission>() })
        {
            var result = M5HwAnalysis.BudgetSamples(samples, M5HwAnalysis.Refreshes(hooks), prefix);
            Assert.Equal("INCONCLUSIVE", result["coherentSampleVerdict"]!.GetValue<string>());
            Assert.Equal(1, result["accountingUnverifiedSamples"]!.GetValue<int>());
            Assert.Equal(0, result["allowanceAccountingMismatches"]!.GetValue<int>());
            Assert.Null(result["samples"]![0]!["allowanceAccountingMatches"]);
            Assert.Contains("prefix", result["samples"]![0]!["accountingUnverifiedReason"]!.GetValue<string>());
        }
    }

    // reach: unit (complete source-derived prefix, then an actual wrong remainder:8190 instead of8191).
    [Fact]
    public void CompletePrefixStillDetectsAnIncorrectWindowRemainder()
    {
        var hooks = new[] { Refresh(), Gate(1, 1, 0x8F, 1, 8192, 14), Gate(3, 2, 0x8F, 1, 8190, 13) };
        var result = M5HwAnalysis.BudgetSamples(M5HwAnalysis.Admissions(hooks, 2, 4),
            M5HwAnalysis.Refreshes(hooks), M5HwAnalysis.Admissions(hooks, 0, 1));
        Assert.Equal("OBSERVED_ADMISSION_ACCOUNTING_MISMATCH", result["coherentSampleVerdict"]!.GetValue<string>());
        Assert.Equal(1, result["allowanceAccountingMismatches"]!.GetValue<int>());
        Assert.Equal(0, result["accountingUnverifiedSamples"]!.GetValue<int>());
    }

    private static (List<M5HwAnalysis.Diagnostic> Hooks, List<M5Hw.WireMessage> Wire, JsonObject[] Events) AudioFixture()
    {
        // C14: prefix silence spends1/1; unsuccessful offer spends0; C12 direct End spends0 of current allowance.
        // Window sample745 spends745/1, so the later silence sees7446/12. No next refresh in this fixture.
        var hooks = new List<M5HwAnalysis.Diagnostic> { Refresh(), Gate(1, 1, 0x8F, 1, 8192, 14),
            Gate(1.1, 2, 0x8F, 1, 8191, 13, sent: false), Gate(1.2, 0, 0x9A, 1, 8191, 13, direct: true),
            Gate(3, 3, 0x8E, 745, 8191, 13), Gate(6003, 4, 0x8F, 1, 7446, 12) };
        var sample = new byte[745]; sample[0] = 0x8E;
        var wire = new List<M5Hw.WireMessage> { new(1.01, true, new byte[] { 0x8F }, 1),
            new(1.21, true, new byte[] { 0x9A }, 2), new(3.01, true, sample, 3), new(6003.01, true, new byte[] { 0x8F }, 4) };
        var events = new[] { 4d, 6003.5 }.Select(at => new JsonObject { ["kind"] = "animation-state", ["atMs"] = at,
            ["streamFrames"] = 3, ["audioPlayed"] = 2, ["streamBytes"] = 747, ["bytesPlayed"] = 2, ["drops"] = 0 }).ToArray();
        return (hooks, wire, events);
    }

    // reach: unit (full diagnostic snapshot, serialized audio-size fixtures and delivered counter fixtures;
    // uses AudioFromDiagnostics, the live M5Run AUDIO wiring, without hardware or physical playback claims).
    [Theory]
    [InlineData(0)]
    [InlineData(2000)] // Prefix is older than the 1000 ms join margin; it still reduces this epoch's allowance.
    public void AudioWiringReplaysPrefixButClaimsOnlyTheJoinedWindow(double offset)
    {
        var (hooks, wire, events) = AudioFixture();
        for (int i = 4; i < hooks.Count; i++) hooks[i] = hooks[i] with { AtMs = hooks[i].AtMs + offset };
        for (int i = 2; i < wire.Count; i++) wire[i] = wire[i] with { AtMs = wire[i].AtMs + offset };
        foreach (var e in events) e["atMs"] = e["atMs"]!.GetValue<double>() + offset;
        var result = M5HwAnalysis.AudioFromDiagnostics(wire, events, 2 + offset, 6004 + offset, hooks);
        Assert.Equal("OBSERVED_AUDIO_FRAMING_AND_ADMISSION", result["verdict"]!.GetValue<string>());
        Assert.Equal("OBSERVED_FOR_JOINED_WINDOW", result["budgetCompliance"]!.GetValue<string>());
        Assert.Equal(2, result["budgetEvidence"]!["coherentSamples"]!.GetValue<int>());
        Assert.Equal(0, result["budgetEvidence"]!["allowanceAccountingMismatches"]!.GetValue<int>());
        Assert.Equal(2, result["orderedWireWindow"]!.AsArray().Count);

        // Truncate the prefix while preserving its refresh. The reduced first remainder cannot establish zero spend.
        var missing = M5HwAnalysis.AudioFromDiagnostics(wire, events, 2 + offset, 6004 + offset,
            hooks.Where(d => d.AtMs == 0 || d.AtMs >= 2 + offset).ToArray());
        Assert.Equal("INCONCLUSIVE", missing["verdict"]!.GetValue<string>());
        Assert.Equal("NOT_JUDGED", missing["budgetCompliance"]!.GetValue<string>());
        Assert.Contains("prefix", missing["admissionEvidenceMissing"]!.GetValue<string>());
        Assert.Equal(0, missing["budgetEvidence"]!["allowanceAccountingMismatches"]!.GetValue<int>());

        var brokenWire = wire.Where(w => w.FrameLine != 3).ToArray();
        var broken = M5HwAnalysis.AudioFromDiagnostics(brokenWire, events, 2 + offset, 6004 + offset, hooks);
        Assert.Equal("INCONCLUSIVE", broken["verdict"]!.GetValue<string>());
        Assert.Contains("join", broken["admissionEvidenceMissing"]!.GetValue<string>());
    }

    // reach: unit (an actual incorrect remainder after the complete prefix; live caller/helper wiring).
    [Fact]
    public void AudioWiringRetainsMismatchDetectionWithCompletePrefix()
    {
        var (hooks, wire, events) = AudioFixture();
        hooks[4] = Gate(3, 3, 0x8E, 745, 8190, 13);
        var result = M5HwAnalysis.AudioFromDiagnostics(wire, events, 2, 6004, hooks);
        Assert.Equal("OBSERVED_ADMISSION_ACCOUNTING_MISMATCH", result["verdict"]!.GetValue<string>());
        Assert.Equal(1, result["budgetEvidence"]!["allowanceAccountingMismatches"]!.GetValue<int>());
    }
}
