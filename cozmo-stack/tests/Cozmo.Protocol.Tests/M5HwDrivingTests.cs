using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class M5HwDrivingTests
{
    static JsonObject Log(double at,string name,string text)=>new(){["kind"]="engine-log",["atMs"]=at,["name"]=name,["text"]=text};
    static JsonObject Complete(double at,uint tag,int type,uint result)=>new(){["kind"]="action-completed",["atMs"]=at,["Tag"]=tag,["ActionType"]=type,["Result"]=result};
    static List<JsonObject> Events(uint startResult=0x02000000,uint driveResult=0x04000002,double driveAt=170)=>new(){
        Log(10,"GetResponseForAnimationTrigger.Found","DriveStartDefault -> ag_start"),
        Log(11,"IAction.Update.PreconditionsMet","Preconditions for PlayAnimationag_start [100] successfully met."),
        Log(100,"GetResponseForAnimationTrigger.Found","DriveEndDefault -> ag_end"),
        Log(101,"IAction.Update.PreconditionsMet","Preconditions for PlayAnimationag_end [102] successfully met."),
        Complete(115,100,26,startResult), Complete(160,102,26,0),Complete(driveAt,99,12,driveResult)};
    static M5Hw.WireMessage[] Wire()=>new[]{new M5Hw.WireMessage(110,true,new byte[]{0x9B,18}),new M5Hw.WireMessage(150,false,new byte[]{0xCB,18})};
    static JsonObject Grade(IEnumerable<JsonObject> events)=>M5HwAnalysis.DrivingPhases(Wire(),events,99,0,200);

    // reach: unit (synthetic matched action/log/wire evidence, not a robot or planner execution).
    [Fact]
    public void FailedStartAndTraversalPermitNoLoopButDoNotSatisfySuccessfulTraversal()
    {
        var result=Grade(Events());
        // Operator 2026-10-20 DRIVING split: M5-063 is OBSERVED on the failed-Start path whatever the drive result.
        Assert.Equal("OBSERVED_FAILED_START_END_CLEANUP",result["verdict"]!.GetValue<string>());
        Assert.Equal("OBSERVED_FAILED_START_END_CLEANUP",result["phaseVerdict"]!.GetValue<string>());
        Assert.False(result["loopRequestCovered"]!.GetValue<bool>());
        Assert.True(result["loopCoverageOpen"]!.GetValue<bool>());
        Assert.False(result["loopRequired"]!.GetValue<bool>());
        Assert.False(result["loopCoverageMissing"]!.GetValue<bool>());
        Assert.True(result["endCompletionBeforeDrive"]!.GetValue<bool>());
        Assert.False(result["successfulTraversalCoverage"]!.GetValue<bool>());
        Assert.Equal(0x04000002u,result["driveResult"]!.GetValue<uint>());
        Assert.Equal("UNASSESSED_M12_M13_OUTCOME",result["driveOutcomeAssessment"]!.GetValue<string>());
    }

    // reach: unit (synthetic source-result branches; no planner fix or general acceptance of failure).
    [Fact]
    public void SuccessfulStartNeedsLoopAndDriveMustWaitForMatchingEndCompletion()
    {
        var missingLoop=Grade(Events(0));
        Assert.Equal("INCONCLUSIVE_REQUIRED_LOOP_REQUEST",missingLoop["phaseVerdict"]!.GetValue<string>());
        Assert.True(missingLoop["loopCoverageMissing"]!.GetValue<bool>());
        Assert.Equal("OBSERVED_END_COMPLETION_ORDER_MISMATCH",Grade(Events(driveAt:140))["phaseVerdict"]!.GetValue<string>());
        var wrongEnd=Events().Where(e=>e["Tag"]?.GetValue<uint>()!=102).Append(Complete(160,999,26,0));
        Assert.False(Grade(wrongEnd)["endCompletionBeforeDrive"]!.GetValue<bool>());
        Assert.Equal("INCONCLUSIVE_END_COMPLETION",Grade(wrongEnd)["phaseVerdict"]!.GetValue<string>());
        var ambiguousWire=Wire().Append(new M5Hw.WireMessage(105,true,new byte[]{0x9B,17}));
        var ambiguous=M5HwAnalysis.DrivingPhases(ambiguousWire,Events(),99,0,200);
        Assert.Equal("OBSERVED_FAILED_START_END_CLEANUP",ambiguous["phaseVerdict"]!.GetValue<string>());
        Assert.False(ambiguous["endCompletionBeforeDrive"]!.GetValue<bool>());
    }

    // reach: unit (synthetic action identities; an unrelated successful animation cannot require this Loop).
    [Fact]
    public void UnmatchedStartOrUnexpectedLoopIsNotTreatedAsValidFailureCleanup()
    {
        var unknown=Events().Where(e=>e["Tag"]?.GetValue<uint>()!=100).Append(Complete(115,999,26,0));
        Assert.Null(Grade(unknown)["loopRequired"]);
        var unexpected=Events(); unexpected.Add(Log(40,"GetResponseForAnimationTrigger.Found","DriveLoopDefault -> ag_loop"));
        Assert.Equal("OBSERVED_LOOP_AFTER_NONZERO_START",Grade(unexpected)["phaseVerdict"]!.GetValue<string>());
        Assert.Equal("INCONCLUSIVE",Grade(unexpected)["verdict"]!.GetValue<string>());     // contradiction stays non-OBSERVED
    }

    // reach: unit (synthetic request/completion ordering; public completion may follow synchronous Loop construction).
    [Fact]
    public void LoopBeforePublicStartReportCanStillBeTheCorrectSourceRequestSequence()
    {
        var events=Events(0,0); events.RemoveAll(e=>e["Tag"]?.GetValue<uint>()==100);
        events.Add(Complete(45,100,26,0));
        events.Add(Log(40,"GetResponseForAnimationTrigger.Found","DriveLoopDefault -> ag_loop"));
        events.Add(Log(41,"IAction.Update.PreconditionsMet","Preconditions for PlayAnimationag_loop [101] successfully met."));
        var result=Grade(events);
        Assert.Equal("OBSERVED_SUCCESSFUL_START_LOOP_END_REQUESTS",result["verdict"]!.GetValue<string>());
        Assert.True(result["loopRequestCovered"]!.GetValue<bool>());
        Assert.True(result["successfulStartLoopEndRequestCoverage"]!.GetValue<bool>());
        Assert.False(result["successfulTraversalCoverage"]!.GetValue<bool>());
    }

    // reach: unit (synthetic successful Start/Loop/End with a failed drive; native 0x4F0D80..D8C selects Loop on
    // matching Start result0, independent of the later drive result).
    [Fact]
    public void SuccessfulPhasePathIsObservedWhateverTheDriveResultAndTraversalStaysUnjudged()
    {
        var events=Events(0,0x04000002); events.RemoveAll(e=>e["Tag"]?.GetValue<uint>()==100);
        events.Add(Complete(45,100,26,0));
        events.Add(Log(40,"GetResponseForAnimationTrigger.Found","DriveLoopDefault -> ag_loop"));
        events.Add(Log(41,"IAction.Update.PreconditionsMet","Preconditions for PlayAnimationag_loop [101] successfully met."));
        var result=Grade(events);
        Assert.Equal("OBSERVED_SUCCESSFUL_START_LOOP_END_REQUESTS",result["verdict"]!.GetValue<string>());
        Assert.True(result["loopRequestCovered"]!.GetValue<bool>());
        Assert.False(result["successfulStartLoopEndRequestCoverage"]!.GetValue<bool>());
        Assert.False(result["successfulTraversalCoverage"]!.GetValue<bool>());
        Assert.StartsWith("NOT_JUDGED",result["traversalJudgement"]!.GetValue<string>());
        Assert.Contains("M12/M13",result["traversalJudgement"]!.GetValue<string>());
    }

    // reach: unit (immutable captured run replay; no new robot run, source settlement or traversal proof).
    [Fact]
    public void FirstRunFailedTraversingPathRetainsItsBoundaryDiagnosticsAndValidCleanup()
    {
        string? bundle=null;
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent){var p=Path.Combine(d.FullName,"re-analysis","acceptance","hardware","20261020-074223-M5-HW-2cc1cf");if(Directory.Exists(p)){bundle=p;break;}}
        Assert.NotNull(bundle);
        var frames=File.ReadLines(Path.Combine(bundle,"frames.jsonl")).Select((line,index)=>{
            var j=JsonNode.Parse(line)!;return new M5HwAnalysis.CapturedFrame(index+1,j["atMs"]!.GetValue<double>(),j["outbound"]!.GetValue<bool>(),FrameCodec.Decode(Convert.FromBase64String(j["rawBase64"]!.GetValue<string>())));});
        var events=File.ReadLines(Path.Combine(bundle,"events.jsonl")).Select(line=>JsonNode.Parse(line)!.AsObject());
        var result=M5HwAnalysis.DrivingPhases(M5HwAnalysis.ReadWire(frames),events,3000001,167900,170300);
        Assert.Equal(3000002u,result["startActionId"]!.GetValue<uint>());
        Assert.Equal(0x02000000u,result["startResult"]!.GetValue<uint>());
        Assert.False(result["loopRequired"]!.GetValue<bool>());
        Assert.Equal("OBSERVED_FAILED_START_END_CLEANUP",result["phaseVerdict"]!.GetValue<string>());
        Assert.Equal("OBSERVED_FAILED_START_END_CLEANUP",result["verdict"]!.GetValue<string>());
        Assert.False(result["loopRequestCovered"]!.GetValue<bool>());
        Assert.True(result["endCompletionBeforeDrive"]!.GetValue<bool>());
        Assert.False(result["successfulTraversalCoverage"]!.GetValue<bool>());
        Assert.Contains(result["boundaryDiagnostics"]!.AsArray(),r=>r!["line"]!.GetValue<string>().Contains("M13-033"));
    }
    // reach: unit (synthetic failed End report; native4F0DCE..DD4 checks End identity, not result0).
    [Fact]
    public void FailedEndCompletionIsNotConfusedWithEarlyDriveOrSuccessfulEndCoverage()
    {
        var events=Events().Where(e=>e["Tag"]?.GetValue<uint>()!=102).Append(Complete(160,102,26,0x0300000C));
        var result=M5HwAnalysis.DrivingPhases(Array.Empty<M5Hw.WireMessage>(),events,99,0,200);
        Assert.True(result["endActionCompletionBeforeDrive"]!.GetValue<bool>());
        Assert.False(result["endCompletionBeforeDrive"]!.GetValue<bool>());
        Assert.Equal("OBSERVED_FAILED_START_END_CLEANUP",result["phaseVerdict"]!.GetValue<string>());
        Assert.Equal("OBSERVED_FAILED_START_END_CLEANUP",result["verdict"]!.GetValue<string>());
        Assert.Equal(0x0300000Cu,result["endResultObservation"]!.GetValue<uint>());
    }

}
