using Cozmo.Conformance;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class M5HwFiniteLayerTests
{
    static M5Hw.WireMessage Out(double at, int line, params byte[] bytes) => new(at, true, bytes, line);
    static M5Hw.WireMessage[] Window() => new[] {
        Out(0, 1, 0x9B, 5), Out(0, 2, 0x8F), Out(0, 3, (byte)RobotMessageId.AnimBackpackLights),
        Out(33, 4, 0x8F), Out(33, 5, 0x97, 1),
        Out(380, 6, 0x8F), Out(380, 7, (byte)RobotMessageId.AnimBackpackLights), Out(500, 8, 0x9A) };
    static M5HwAnalysis.FaceActivity[] Flags() => new[] {
        new M5HwAnalysis.FaceActivity(2, 0, false), new M5HwAnalysis.FaceActivity(4, 0, true),
        new M5HwAnalysis.FaceActivity(6, 0, false) };

    // reach: unit (substitutes independently supplied per-frame face flags and tag-only wire fixtures;
    // real M5-029 flag/draw diagnostic join is unavailable in the allowed hardware harness scope).
    [Fact]
    public void NotDueFirstFaceAndBackpackOnlyTailDoNotRequireFaceAudioEquality()
    {
        // Checked first face trigger is cumulative duration >=33; backpack independently starts at0
        // and includes a segment at380/60. The tail flag here is an explicit fixture input, not a
        // claim about every random distortion.97,01 is tag-only, not a serialization oracle.
        var result = M5HwAnalysis.FiniteLayers(Window(), 0, 500, true, Flags());
        Assert.Equal("OBSERVED_CONDITIONAL_LAYER_FRAMES", result["verdict"]!.GetValue<string>());
        Assert.Equal(1, result["faces"]!.GetValue<int>());
        Assert.Equal(3, result["audioFrames"]!.GetValue<int>());
        Assert.False(result["frameEvidence"]![0]!["faceActive"]!.GetValue<bool>());
        Assert.True(result["frameEvidence"]![1]!["faceActive"]!.GetValue<bool>());
        Assert.Equal(0, result["frameEvidence"]![2]!["faces"]!.GetValue<int>());
        Assert.Equal(8, result["wireWindow"]!.AsArray().Count);
    }

    // reach: unit (synthetic flags stand in for the missing generation evidence; no scheduler or robot).
    [Fact]
    public void MissingRequiredFaceIsDetectedDespitePlausibleAggregateCounts()
    {
        var missing = Window().Where(m => m.FrameLine != 5);
        var result = M5HwAnalysis.FiniteLayers(missing, 0, 500, true, Flags());
        Assert.Equal("OBSERVED_MISSING_REQUIRED_FACE", result["verdict"]!.GetValue<string>());
        Assert.Equal(1, result["missingRequiredFaceFrames"]!.GetValue<int>());
        // Move the same tag-only face to the not-due first frame: totals alone still look identical.
        var shifted = missing.Append(Out(1, 9, 0x97, 1));
        Assert.Equal("OBSERVED_MISSING_REQUIRED_FACE", M5HwAnalysis.FiniteLayers(shifted, 0, 500, true, Flags())["verdict"]!.GetValue<string>());
    }

    // reach: unit (synthetic wire replaces capture; absent/partial flag evidence must remain unknown).
    [Fact]
    public void CountsAndDrainedStateCannotCertifyUnknownRandomFaceFlags()
    {
        var result = M5HwAnalysis.FiniteLayers(Window(), 0, 500, true);
        Assert.Equal("INCONCLUSIVE", result["verdict"]!.GetValue<string>());
        Assert.False(result["faceActivityEstablished"]!.GetValue<bool>());
        Assert.NotNull(result["missing"]);
        Assert.Equal("INCONCLUSIVE", M5HwAnalysis.FiniteLayers(Window(), 0, 500, true, Flags().Take(1).ToArray())["verdict"]!.GetValue<string>());
        Assert.Equal("INCONCLUSIVE", M5HwAnalysis.FiniteLayers(Window(), 0, 500, false, Flags())["verdict"]!.GetValue<string>());
    }
}
