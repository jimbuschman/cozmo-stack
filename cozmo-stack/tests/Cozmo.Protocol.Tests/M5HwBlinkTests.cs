using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class M5HwBlinkTests
{
    static JsonObject Yes() => new(){["answer"]="yes",["window"]=new JsonObject{["fromMs"]=0,["toMs"]=60000}};

    // reach: unit (substitutes captured native call/deadline observations; real join at58D382/58D54C unavailable).
    [Fact]
    public void ExistingDeadlineThenNewIntervalDecrementsByCallsDespiteLongWallPause()
    {
        // Native subtracts60 first; source RNG reset is inclusive44000..45000. The old120 deadline
        // survives B4; a9940ms gap with no KeepFaceAlive call does not consume9940ms from its timer.
        var calls=new[]{new M5HwAnalysis.BlinkCall(1,0,120,60,false),
            new M5HwAnalysis.BlinkCall(2,60,60,44500,true),
            new M5HwAnalysis.BlinkCall(3,10000,44500,44440,false)};
        var result=M5HwAnalysis.Blinks(Array.Empty<M5Hw.WireMessage>(),0,60000,44000,45000,Yes(),calls);
        Assert.Equal("OBSERVED_BLINK_CALL_TRANSITIONS",result["verdict"]!.GetValue<string>());
        Assert.Equal(9940,result["generationCalls"]![2]!["wallGapMs"]!.GetValue<double>());
        Assert.Equal("NOT_JUDGED",result["firmwareAcceptance"]!.GetValue<string>());
        var wrong=calls.Take(2).Append(new M5HwAnalysis.BlinkCall(3,10000,44500,34560,false)).ToArray();
        Assert.Equal("OBSERVED_BLINK_CALL_MISMATCH",M5HwAnalysis.Blinks(Array.Empty<M5Hw.WireMessage>(),0,60000,44000,45000,Yes(),wrong)["verdict"]!.GetValue<string>());
    }

    // reach: unit (valid raw black face image and F1 markers, synthetic capture; no generation or display claim).
    [Fact]
    public void DarkImageAndYesAnswerAreNotBlinkGenerationOrFirmwareAcceptance()
    {
        // A raw1024-byte zero-mask image is blank in the engine model, regardless of why it was sent.
        var face=new FaceImage{Image=new byte[1024]};
        var wire=new[]{new M5Hw.WireMessage(10,false,new AnimationState{Timestamp=10,NumAudioFramesPlayed=20}.ToBytes()),
            new M5Hw.WireMessage(20,true,face.ToBytes(),5,2,10),
            new M5Hw.WireMessage(30,false,new AnimationState{Timestamp=30,NumAudioFramesPlayed=21}.ToBytes())};
        var result=M5HwAnalysis.Blinks(wire,0,60000,44000,45000,Yes());
        Assert.Equal("INCONCLUSIVE",result["verdict"]!.GetValue<string>());
        Assert.False(result["generationEvidenceAvailable"]!.GetValue<bool>());
        Assert.Equal(0,result["encodedFaceDiagnostics"]![0]!["engineModelLitPixels"]!.GetValue<int>());
        Assert.NotNull(result["encodedFaceDiagnostics"]![0]!["precedingWireAnimationState"]);
        Assert.NotNull(result["encodedFaceDiagnostics"]![0]!["followingWireAnimationState"]);
        Assert.Empty(result["generationCalls"]!.AsArray());
        Assert.Equal("yes",result["physicalObservation"]!["answer"]!.GetValue<string>());
        Assert.Equal("NOT_JUDGED",result["firmwareAcceptance"]!.GetValue<string>());
    }

    // reach: unit (synthetic recorded calls replace missing runtime diagnostic; never infer unseen calls).
    [Fact]
    public void DiscontinuousOrOutOfRangeGenerationIsNotCertified()
    {
        var gap=new[]{new M5HwAnalysis.BlinkCall(1,0,60,44500,true),new M5HwAnalysis.BlinkCall(3,60,44500,44440,false)};
        Assert.Equal("OBSERVED_BLINK_CALL_MISMATCH",M5HwAnalysis.Blinks(Array.Empty<M5Hw.WireMessage>(),0,60000,44000,45000,Yes(),gap)["verdict"]!.GetValue<string>());
        var wrong=new[]{new M5HwAnalysis.BlinkCall(1,0,60,30000,true)};
        Assert.Equal("OBSERVED_BLINK_CALL_MISMATCH",M5HwAnalysis.Blinks(Array.Empty<M5Hw.WireMessage>(),0,60000,44000,45000,Yes(),wrong)["verdict"]!.GetValue<string>());
    }
    // reach: unit (synthetic equal-time capture identities; bounds use actual raw order, not timestamp equality).
    [Fact]
    public void EqualTimeF1BoundsRespectFrameIdentity()
    {
        var payload=new AnimationState{EnabledAnimTracks=255}.ToBytes();
        var wire=new[]{new M5Hw.WireMessage(10,false,payload,1),
            new M5Hw.WireMessage(10,true,new FaceImage{Image=new byte[1024]}.ToBytes(),2),
            new M5Hw.WireMessage(10,false,payload,3)};
        var row=M5HwAnalysis.Blinks(wire,0,60000,44000,45000,Yes())["encodedFaceDiagnostics"]![0]!;
        Assert.Equal(1,row["precedingWireAnimationState"]![0]!["frameLine"]!.GetValue<int>());
        Assert.Equal(3,row["followingWireAnimationState"]![0]!["frameLine"]!.GetValue<int>());
    }

}
