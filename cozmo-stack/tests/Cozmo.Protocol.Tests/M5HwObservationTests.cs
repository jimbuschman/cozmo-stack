using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class M5HwObservationTests
{
    static M5Hw.WireMessage Out(double at, params byte[] bytes) => new(at, true, bytes);
    static M5Hw.WireMessage State(double at, float head) => new(at, false,
        new RobotState { Timestamp=(uint)at, HeadAngle=head, CliffDataRaw=new ushort[4] }.ToBytes());
    static M5Hw.WireMessage Tracks(double at, byte mask) => new(at, false,
        new AnimationState { Timestamp=(uint)at, EnabledAnimTracks=mask }.ToBytes());
    static M5Hw.WireMessage[] Locked() => new[] { Out(10,0x9D,1), Out(20,0x9B,5),
        Out(30,0x93,50,0,10), Tracks(35,254), State(40,-0.125f),
        Out(60,0x93,50,0,20), Tracks(65,254), State(70,-0.125f), Out(80,0x9A),
        Out(100,0x9E,1), State(110,0.5f) };
    static JsonObject Grade(IEnumerable<M5Hw.WireMessage> wire) => M5HwAnalysis.LockedHead(wire,0,20,80,90,120);

    // reach: unit (synthetic Disable/Enable/F1/state capture; no robot or physical lock claim).
    [Fact]
    public void OwnUnlockAndLaterHeadMovementAreOutsideTheLockedClip()
    {
        var result=Grade(Locked());
        Assert.Equal("OBSERVED_LOCKED_HEAD_EVIDENCE",result["verdict"]!.GetValue<string>());
        Assert.False(result["enableBeforeUnlock"]!.GetValue<bool>());
        Assert.True(result["harnessUnlockEnableObserved"]!.GetValue<bool>());
        Assert.False(result["headTelemetryChanged"]!.GetValue<bool>());
        Assert.Equal(2,result["headKeyframes"]!.GetValue<int>());
        Assert.Equal(2,result["headTelemetry"]!.AsArray().Count);
        Assert.Equal(254,result["trackReports"]![0]!["enabledAnimTracks"]!.GetValue<byte>());
    }

    // reach: unit (synthetic wire substitutes physical phases; wrong/missing producers remain distinct).
    [Fact]
    public void MissingDisableWrongMaskEarlyEnableAndLockedMovementAreSeparateEvidence()
    {
        Assert.False(Grade(Locked().Where(m=>m.Payload[0]!=0x9D))["validDisableBeforeClip"]!.GetValue<bool>());
        var wrong=Locked().Where(m=>m.Payload[0]!=0x9D).Prepend(Out(10,0x9D,4));
        Assert.Equal("INCONCLUSIVE",Grade(wrong)["verdict"]!.GetValue<string>());
        Assert.True(Grade(Locked().Append(Out(50,0x9E,1)))["enableBeforeUnlock"]!.GetValue<bool>());
        var moved=Grade(Locked().Append(State(75,0.2f)));
        Assert.True(moved["headTelemetryChanged"]!.GetValue<bool>());
        Assert.Equal("OBSERVED_LOCKED_HEAD_EVIDENCE",moved["verdict"]!.GetValue<string>());
        Assert.True(Grade(Locked().Append(Tracks(75,255)))["headTrackReportedEnabled"]!.GetValue<bool>());
        Assert.Equal("OBSERVED_LOCKED_HEAD_EVIDENCE",Grade(Locked().Where(m=>m.Outbound))["verdict"]!.GetValue<string>());
    }

    // reach: unit (observation metadata/raw answers replace interactive input; no source/physical verdict).
    [Theory]
    [InlineData("unclear")] [InlineData("not observed")] [InlineData("no")] [InlineData("something unexpected")]
    public void UnexpectedAndUnclearAnswersKeepTheirWindowAndRawText(string raw)
    {
        var window=new JsonObject{["check"]="LOCKED_HEAD",["phase"]="LOCKED",["fromMs"]=20d,["toMs"]=80d,
            ["freeText"]="head moved only after unlock"};
        var answer=M5HwAnalysis.ObservationAnswer(window,"Exclude post-UNLOCK motion",raw,
            new[]{"yes","no","unclear","not-observed"},150,200);
        Assert.Equal(raw,answer["rawAnswer"]!.GetValue<string>());
        Assert.Equal(80,answer["window"]!["toMs"]!.GetValue<double>());
        Assert.Equal("head moved only after unlock",answer["window"]!["freeText"]!.GetValue<string>());
        Assert.Null(answer["verdict"]);
        Assert.NotEqual("yes",answer["answer"]!.GetValue<string>());
    }

    // reach: unit (static prompt plans, not a human or robot; validates every current observation family).
    [Fact]
    public void EveryObservationFamilyExplainsWhatToWatchAndSeparatesBlinkDartAndPostUnlockMotion()
    {
        Assert.Equal(new[]{"BLINKS","CANCEL","LAYERS","LIVE","LOCKED"},M5Hw.ObservationPlans.Select(p=>p.Id).OrderBy(x=>x));
        Assert.All(M5Hw.ObservationPlans,p=>{Assert.NotEmpty(p.Watch);Assert.NotEmpty(p.Expected);});
        Assert.Contains("after the announced UNLOCK",M5Hw.ObservationPlans.Single(p=>p.Id=="LOCKED").Expected);
        Assert.Contains("neutral-reference.svg",M5Hw.ObservationPlans.Single(p=>p.Id=="CANCEL").Expected);
        Assert.Contains("darts, not blinks",M5Hw.ObservationPlans.Single(p=>p.Id=="BLINKS").Expected);
        var noHuman=M5HwAnalysis.ObservationAnswer(new JsonObject{["fromMs"]=0,["toMs"]=1},"q",null,new[]{"yes","unclear"},2,3);
        Assert.Equal("not-observed",noHuman["answer"]!.GetValue<string>());
        Assert.Null(noHuman["verdict"]);
        Assert.Throws<InvalidOperationException>(()=>M5HwAnalysis.ObservationAnswer(
            new JsonObject{["fromMs"]=0},"q","yes",new[]{"yes"},2,3));
    }
}
