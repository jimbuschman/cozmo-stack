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
}
