using Cozmo.Conformance;
using System.Text.Json.Nodes;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class M5HwLifecycleTests
{
    static M5Hw.WireMessage Out(double at, params byte[] payload) => new(at, true, payload);
    static string Verdict(JsonObject result) => result["verdict"]!.GetValue<string>();

    // reach: unit (synthetic ordered capture replaces robot/transport; hardware M5-036 remains unobserved).
    [Fact]
    public void MultipleLiveBurstsRetainPairsGapsAndTheOpenFinalBurst()
    {
        // Checked Update D414..44C permits reinit only after End, exhausted tracks and empty FIFO.
        // Same-datagram order matters; timestamp sorting must preserve insertion order.
        var wire = new[] { Out(10, 0x9B, 255), Out(100, 0x9A), Out(100, 0x9B, 255),
            Out(500, 0x9A), Out(560, 0x9B, 255) };
        var result = M5HwAnalysis.LiveBursts(wire, 0, 10000);
        Assert.Equal("OBSERVED_FF_BURST_ORDER", Verdict(result));
        Assert.Equal(2, result["completedBursts"]!.GetValue<int>());
        Assert.True(result["openFinalBurst"]!.GetValue<bool>());
        Assert.Equal(0, result["orderedLifecycle"]![2]!["restartGapMs"]!.GetValue<double>());
        Assert.Equal(60, result["orderedLifecycle"]![4]!["restartGapMs"]!.GetValue<double>());
        Assert.Contains("exact restart timing requires review", result["verdictRule"]!.GetValue<string>());
    }

    // reach: unit (synthetic lifecycle messages; no production scheduler or robot execution).
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void InvalidLiveOrderIsIdentified(int kind)
    {
        var wire = kind switch
        {
            0 => new[] { Out(1, 0x9A) },
            1 => new[] { Out(1, 0x9B, 255), Out(2, 0x9B, 255) },
            2 => new[] { Out(1, 0x9B, 1), Out(2, 0x9A) },
            3 => new[] { Out(1, 0x9B, 255), Out(2, 0x8D) },
            _ => new[] { Out(1, 0x9B, 255), Out(2, 0x9A, 255) }
        };
        Assert.Equal("OBSERVED_INVALID_BURST_ORDER", Verdict(M5HwAnalysis.LiveBursts(wire, 0, 10000)));
    }

    // reach: unit (synthetic captures substitute hardware; missing bursts/window stay inconclusive).
    [Fact]
    public void MissingOrShortLiveObservationCannotEstablishRepeatedBursts()
    {
        var one = new[] { Out(1, 0x9B, 255), Out(2, 0x9A) };
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.LiveBursts(one, 0, 10000)));
        var two = one.Concat(new[] { Out(3, 0x9B, 255), Out(4, 0x9A) });
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.LiveBursts(two, 0, 9999)));
    }

    // reach: unit (synthetic wire and precondition observations replace real persistent face execution).
    [Fact]
    public void PersistentFaceNeedsItsOwnPreconditionsAndOutputAndRejectsEnd()
    {
        // Face bytes97,01 are tag-only fixtures, not valid face serialization or decoding coverage.
        // M5-028 C530..556: HaveLayersToSend blocks End. Face output is conditional (M5-029).
        var face = new[] { Out(20, 0x97, 1) };
        Assert.Equal("OBSERVED_PERSISTENT_FACE_WINDOW", Verdict(M5HwAnalysis.PersistentFace(face, 0, 10000, true)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.PersistentFace(face, 0, 10000, false)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.PersistentFace(Array.Empty<M5Hw.WireMessage>(), 0, 10000, true)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5HwAnalysis.PersistentFace(face, 0, 9999, true)));
        Assert.Equal("OBSERVED_UNEXPECTED_LAYER_LIFECYCLE", Verdict(M5HwAnalysis.PersistentFace(face.Append(Out(30, 0x9A)), 0, 10000, true)));
        Assert.Equal("OBSERVED_UNEXPECTED_LAYER_LIFECYCLE", Verdict(M5HwAnalysis.PersistentFace(face.Append(Out(30, 0x9B, 255)), 0, 10000, true)));
    }
}
