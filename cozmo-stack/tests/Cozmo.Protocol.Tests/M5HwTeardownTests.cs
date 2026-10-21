using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class M5HwTeardownTests
{
    static JsonObject Marker(double at) => new(){["kind"]="engine-log",["name"]="AnimationStreamer.ClearSendBuffer",
        ["text"]="Clearing unsent animFaceImage",["atMs"]=at};
    static string Bundle()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        { var p=Path.Combine(d.FullName,"re-analysis","acceptance","hardware","20261020-074223-M5-HW-2cc1cf"); if(Directory.Exists(p))return p; }
        throw new DirectoryNotFoundException("Immutable M5 first-run bundle required for replay.");
    }

    // reach: unit (immutable captured raw traffic/log replay, using historical host request/return bounds;
    // not a new exact Dispose-call timing measurement or hardware acceptance run).
    [Fact]
    public void FirstRunOldHeadResendAndAbortPrefixDoNotBecomeAStreamerFlush()
    {
        var frames=File.ReadLines(Path.Combine(Bundle(),"frames.jsonl")).Select((line,index)=>{
            var j=JsonNode.Parse(line)!;
            return new M5HwAnalysis.CapturedFrame(index+1,j["atMs"]!.GetValue<double>(),j["outbound"]!.GetValue<bool>(),
                FrameCodec.Decode(Convert.FromBase64String(j["rawBase64"]!.GetValue<string>())));});
        var events=File.ReadLines(Path.Combine(Bundle(),"events.jsonl")).Select(line=>JsonNode.Parse(line)!.AsObject()).ToArray();
        var result=M5HwAnalysis.Teardown(M5HwAnalysis.ReadWire(frames,true),events,172516.4529,172549.2551,true);
        Assert.Equal("OBSERVED_DISPOSE_DISCARD_INTERVAL",result["verdict"]!.GetValue<string>());
        Assert.Contains(result["retransmissionsDuringDispose"]!.AsArray(),r=>r!["frameLine"]!.GetValue<int>()==13228
            && r["payloadHex"]!.GetValue<string>().StartsWith("93"));
        Assert.Contains(result["abortAllPrefixCandidates"]!.AsArray(),r=>r!["frameLine"]!.GetValue<int>()==13230);
        Assert.Equal(0,result["observedStreamerFlushes"]!.GetValue<int>());
        Assert.Equal(0,result["unknownOrigins"]!.GetValue<int>());
        Assert.Single(result["streamerDiscardMarkers"]!.AsArray());
    }

    // reach: unit (synthetic independent origins and tag-only face identity stubs, not serialization;
    // live enqueue/destructor provenance is unavailable within the allowed harness scope).
    [Theory]
    [InlineData(0x97)] [InlineData(0x8F)]
    public void KnownNewStreamerFlushIsDifferentFromUnknownOrPreviouslyQueuedData(int tag)
    {
        var wire=new[]{new M5Hw.WireMessage(110,true,new byte[]{(byte)tag},5,2,10),
            new M5Hw.WireMessage(111,true,new byte[]{0x8D},6,0,11)};
        var unknown=M5HwAnalysis.Teardown(wire,new[]{Marker(115)},100,120,true);
        Assert.Equal("INCONCLUSIVE",unknown["verdict"]!.GetValue<string>());
        Assert.Equal(1,unknown["unknownOrigins"]!.GetValue<int>());
        var bad=new[]{new M5HwAnalysis.TeardownOrigin(5,2,"streamer-final-flush",110)};
        Assert.Equal("OBSERVED_STREAMER_FINAL_FLUSH",M5HwAnalysis.Teardown(wire,new[]{Marker(115)},100,120,true,bad)["verdict"]!.GetValue<string>());
        var queued=new[]{new M5HwAnalysis.TeardownOrigin(5,2,"queued-before-dispose",90)};
        var result=M5HwAnalysis.Teardown(wire,new[]{Marker(115)},100,120,true,queued);
        Assert.Equal("OBSERVED_DISPOSE_DISCARD_INTERVAL",result["verdict"]!.GetValue<string>());
        Assert.Equal(1,result["knownPrequeuedFirstSends"]!.GetValue<int>());
        Assert.Single(result["abortAllPrefixCandidates"]!.AsArray());
    }

    // reach: unit (synthetic interval/marker metadata; quiet transport alone cannot establish destructor coverage).
    [Fact]
    public void ClosedTransportSilenceWithoutDiscardOrCompletedIntervalIsInconclusive()
    {
        Assert.Equal("INCONCLUSIVE",M5HwAnalysis.Teardown(Array.Empty<M5Hw.WireMessage>(),Array.Empty<JsonObject>(),100,120,true)["verdict"]!.GetValue<string>());
        Assert.Equal("INCONCLUSIVE",M5HwAnalysis.Teardown(Array.Empty<M5Hw.WireMessage>(),new[]{Marker(115)},100,120,false)["verdict"]!.GetValue<string>());
        var spoof=new JsonObject{["kind"]="operator-window-notes",["name"]="AnimationStreamer.ClearSendBuffer",["text"]="Clearing unsent animFaceImage",["atMs"]=115};
        Assert.Equal("INCONCLUSIVE",M5HwAnalysis.Teardown(Array.Empty<M5Hw.WireMessage>(),new[]{spoof},100,120,true)["verdict"]!.GetValue<string>());
    }

    // The shape of run 20261020-200624-M5-HW-97161e (research/20261020-m5-teardown-sends.md): the last tick queued
    // reliable seq 12751..12757 (8E, 93, 97, 8E, 97, 8E, 93) before dispose-begin; 12754..12757 first went out during
    // Dispose, paced by the reliable layer (M1-006); then AbortAll (0x8D) and one ClearSendBuffer discard. Native order:
    // ~Robot AbortAll 0x00511120, ~AnimationStreamer 0x0051154A whose ClearSendBuffer 0x0057B044..0x0057B0B4 discards
    // without sending; the transport is torn down later (0x0062EE98..0x0062EEB8), so queued traffic keeps draining.
    static readonly (byte Tag,int Size)[] LastTick={(0x8E,745),(0x93,4),(0x97,5),(0x8E,745),(0x97,5),(0x8E,745),(0x93,4)};
    const double DisposeBegin=226702.65, DisposeEnd=226772.30;
    static byte[] Body((byte Tag,int Size) m){var b=new byte[m.Size];b[0]=m.Tag;return b;}
    static M5Hw.WireMessage[] TeardownWire()
    {
        double[] at={226699.96,226699.96,226699.96,226703.56,226703.56,226737.70,226737.70};
        int[] line={16507,16507,16507,16508,16508,16512,16512}, ordinal={1,2,3,2,3,0,1};
        return LastTick.Select((m,k)=>new M5Hw.WireMessage(at[k],true,Body(m),line[k],ordinal[k],(ushort)(12751+k),1))
            .Append(new M5Hw.WireMessage(226770.14,true,new byte[]{0x8D},16514,3,12760,1)).ToArray();
    }
    static List<M5HwAnalysis.Diagnostic> Gates(double lastTwoAt=226699.50)=>LastTick.Select((m,k)=>new M5HwAnalysis.Diagnostic(
        k>=5?lastTwoAt:226699.15+k*0.05,new Cozmo.Robot.Animation.SendGateObservation(0,13050+k,3341,m.Tag,m.Size,m.Tag==0x8E,
            8192,14,true,true,false))).ToList();

    // reach: unit (synthetic gate observations and wire with the bundle's shape; the live join runs in M5Hw TEARDOWN).
    [Fact]
    public void SendGateOriginsMakeTheRunsFourDisposeFirstSendsPrequeued()
    {
        var wire=TeardownWire(); var events=new[]{Marker(226767.71)};
        var origins=M5HwAnalysis.TeardownOrigins(wire,Gates(),DisposeBegin,DisposeEnd,out var evidence);
        Assert.True(evidence["sendJoin"]!["joined"]!.GetValue<bool>());
        var result=M5HwAnalysis.Teardown(wire,events,DisposeBegin,DisposeEnd,true,origins);
        Assert.Equal("OBSERVED_DISPOSE_DISCARD_INTERVAL",result["verdict"]!.GetValue<string>());
        var candidates=result["animationDataCandidates"]!.AsArray();
        Assert.Equal(new ushort[]{12754,12755,12756,12757},candidates.Select(c=>c!["wire"]![0]!["reliableSequence"]!.GetValue<ushort>()));
        Assert.All(candidates,c=>Assert.Equal("PREQUEUED_FIRST_SEND",c!["classification"]!.GetValue<string>()));
        Assert.Equal(4,result["knownPrequeuedFirstSends"]!.GetValue<int>());
        Assert.Equal(0,result["unknownOrigins"]!.GetValue<int>());
        Assert.Null(result["missing"]);
        // Without origins the same capture is INCONCLUSIVE, and says why.
        var bare=M5HwAnalysis.Teardown(wire,events,DisposeBegin,DisposeEnd,true);
        Assert.Equal("INCONCLUSIVE",bare["verdict"]!.GetValue<string>());
        Assert.Contains("4 first send(s) during Dispose have no enqueue origin.",bare["missing"]!.AsArray().Select(n=>n!.GetValue<string>()));
    }

    // reach: unit (synthetic; the grader is not loosened: a gate observation inside Dispose gives no origin).
    [Fact]
    public void AGateObservationInsideDisposeLeavesItsSendUnknown()
    {
        var wire=TeardownWire();
        var origins=M5HwAnalysis.TeardownOrigins(wire,Gates(lastTwoAt:226710.0),DisposeBegin,DisposeEnd,out _);
        var result=M5HwAnalysis.Teardown(wire,new[]{Marker(226767.71)},DisposeBegin,DisposeEnd,true,origins);
        Assert.Equal("INCONCLUSIVE",result["verdict"]!.GetValue<string>());
        Assert.Equal(2,result["unknownOrigins"]!.GetValue<int>());
        Assert.Contains("2 first send(s) during Dispose have no enqueue origin.",result["missing"]!.AsArray().Select(n=>n!.GetValue<string>()));
        // No gate observations at all: no join, so no origins.
        Assert.Empty(M5HwAnalysis.TeardownOrigins(wire,new List<M5HwAnalysis.Diagnostic>(),DisposeBegin,DisposeEnd,out var none));
        Assert.False(none["sendJoin"]!["joined"]!.GetValue<bool>());
    }
}
