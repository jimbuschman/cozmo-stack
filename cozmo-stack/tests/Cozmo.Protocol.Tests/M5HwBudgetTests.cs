using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class M5HwBudgetTests
{
    // reach: unit (synthetic raw F1/delivered epochs reproduce the checked counter example; no send observation).
    [Fact]
    public void Old1549Through1551StatesPairedWith1565StreamFramesAreNotSendViolations()
    {
        var wire=new List<M5Hw.WireMessage>();
        for(int i=0;i<=150;i++)
        {
            var bytes=i==0?new byte[745]:new byte[1]; bytes[0]=(byte)(i==0?0x8E:0x8F);
            wire.Add(new(i*1000d/30,true,bytes,i+1));
        }
        var events=new List<JsonObject>(); int[] playedBytes={71560,72339,73116};
        for(int i=0;i<3;i++)
        {
            var state=new AnimationState{Timestamp=(uint)(i+1),NumAudioFramesPlayed=1549+i,NumAnimBytesPlayed=playedBytes[i]};
            wire.Add(new(1000+i*0.001,false,state.ToBytes(),501+i,0,null,7));
            events.Add(new(){["kind"]="animation-state",["atMs"]=1000.1+i*0.001,["robotTimestamp"]=(uint)(i+1),
                ["streamFrames"]=1565,["audioPlayed"]=1549+i,["streamBytes"]=80884,["bytesPlayed"]=playedBytes[i],["drops"]=0});
        }
        var result=M5HwAnalysis.Audio(wire,events,0,6000);
        Assert.Equal("INCONCLUSIVE",result["verdict"]!.GetValue<string>());
        Assert.Equal("OBSERVED_AUDIO_FRAMING_AND_COUNTERS",result["framingVerdict"]!.GetValue<string>());
        Assert.Equal("NOT_JUDGED",result["budgetCompliance"]!.GetValue<string>());
        Assert.Equal(16,result["maximumFramesAhead"]!.GetValue<int>());
        Assert.Equal(2,result["snapshotsAbove14Frames"]!.GetValue<int>());
        Assert.Equal(0,result["budgetEvidence"]!["observedViolations"]!.GetValue<int>());
        Assert.All(result["counterRows"]!.AsArray(),r=>{
            Assert.False(r!["isAtomicAdmissionEvidence"]!.GetValue<bool>());
            Assert.Equal(7,r["stateConnectionEpoch"]!.GetValue<int>());});
    }

    // reach: unit (substitutes independently captured actual remaining allowances at57BF96..BFCC;
    // the real public harness has no permitted atomic admission/success join).
    [Fact]
    public void CoherentOverBudgetSendIsDifferentFromAnUntrustedSnapshot()
    {
        var coherent=new M5HwAnalysis.BudgetAdmission(100,1,0x8F,1,8192,0,true,true);
        var result=M5HwAnalysis.BudgetSamples(new[]{coherent});
        Assert.Equal("OBSERVED_COHERENT_ADMISSION_VIOLATION",result["coherentSampleVerdict"]!.GetValue<string>());
        Assert.Equal(1,result["observedViolations"]!.GetValue<int>());
        var untrusted=M5HwAnalysis.BudgetSamples(new[]{coherent with {Coherent=false}});
        Assert.Equal("NOT_JUDGED",untrusted["coherentSampleVerdict"]!.GetValue<string>());
        Assert.Equal(0,untrusted["observedViolations"]!.GetValue<int>());
    }

    // reach: unit (synthetic coherent gate/End observations; no firmware or all-send compliance claim).
    [Fact]
    public void DirectEndBypassesAdmissionWhileBufferedByteLimitStillApplies()
    {
        var end=new M5HwAnalysis.BudgetAdmission(100,1,0x9A,1,0,0,true,true);
        var result=M5HwAnalysis.BudgetSamples(new[]{end});
        Assert.True(result["samples"]![0]!["directEndBypass"]!.GetValue<bool>());
        Assert.Equal(0,result["observedViolations"]!.GetValue<int>());
        Assert.Equal("NOT_JUDGED",result["budgetCompliance"]!.GetValue<string>());
        // Native C10 checks message size for non-audio buffered data too, frame cost0.
        var face=new M5HwAnalysis.BudgetAdmission(101,1,0x97,100,99,0,true,true);
        Assert.Equal(1,M5HwAnalysis.BudgetSamples(new[]{face})["observedViolations"]!.GetValue<int>());
    }
}
