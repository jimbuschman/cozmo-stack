using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Robot.Manipulation;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class AnalyzerCheckedRowsTests:IDisposable
{
    private readonly bool _error=EngineErrorState.ErrorFlagSet;
    public void Dispose()=>EngineErrorState.ErrorFlagSet=_error;
    private sealed record Log(EngineLogLevel Level,string? Channel,string Key,string Text);
    private static AIComponent Owner(List<Log> logs)=>new(()=>1,sourceLog:(l,c,k,t)=>logs.Add(new(l,c,k,t)));
    [Fact] public void RequestsAreSetsAndDisableWinsUntilSingleRemoval()
    {
        var logs=new List<Log>();var a=Owner(logs).Analyzer;Assert.False(a.IsProcessRunning(1));
        a.AddEnableRequest(1,"Hiking");a.AddEnableRequest(1,"Hiking");Assert.True(a.IsProcessRunning(1));
        a.AddDisableRequest(1,"VisitInterestingEdge");a.AddDisableRequest(1,"VisitInterestingEdge");Assert.False(a.IsProcessRunning(1));
        a.RemoveDisableRequest(1,"VisitInterestingEdge");Assert.True(a.IsProcessRunning(1));a.RemoveEnableRequest(1,"Hiking");Assert.False(a.IsProcessRunning(1));
        Assert.Equal(new Log(EngineLogLevel.Info,"AIInfoAnalysis","AIInformationAnalyzer.AddEnableRequest","'Hiking' requested to enable process 'CalculateInterestingRegions'"),logs[0]);
        Assert.Equal(new Log(EngineLogLevel.Warning,null,"AIInformationAnalyzer.AddEnableRequest.DuplicatedID","Hiking had already requested enabling 1"),logs[1]);
        Assert.Equal("VisitInterestingEdge had already requested disabling 1",logs[3].Text);
        Assert.Equal("'VisitInterestingEdge' removed the request to disable process 'CalculateInterestingRegions'",logs[4].Text);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void MissingRemovalEmplacesAndPreservesMisnamedNativeWarning(bool enable)
    {
        var logs=new List<Log>();var a=Owner(logs).Analyzer;
        if(enable)a.RemoveEnableRequest(72,"x");else a.RemoveDisableRequest(72,"x");
        Assert.False(a.IsProcessRunning(72));var l=Assert.Single(logs);
        Assert.Equal("AIInformationAnalyzer.Add"+(enable?"Enable":"Disable")+"Request.IDNotFound",l.Key);
        Assert.Equal("x had not requested "+(enable?"enabling":"disabling")+" 72 before or had already removed the request.",l.Text);
    }
    [Fact] public void IsRunningDoesNotRegisterUnknownProcess()
    {
        var logs=new List<Log>();var a=Owner(logs).Analyzer;Assert.False(a.IsProcessRunning(99));Assert.False(a.IsProcessRunning(99));Assert.Equal(2,logs.Count);
        Assert.All(logs,l=>Assert.Equal(new Log(EngineLogLevel.Warning,null,"AIInformationAnalyzer.IsProcessRunning.ProcessNotRegistered","Process 'ERROR' is not even registered"),l));
        Assert.Equal("Invalid",AIInformationAnalyzer.ProcessName(0));
    }
    [Fact] public void ActualAIUpdateRepeatsMissingCallableErrorWithoutConsumingRequests()
    {
        using var robot=CozmoRobot.CreateOffline();var logs=new List<Log>();var owner=Owner(logs);owner.Analyzer.AddEnableRequest(27,"first");logs.Clear();EngineErrorState.ErrorFlagSet=false;
        owner.Update(robot);owner.Update(robot);Assert.True(EngineErrorState.ErrorFlagSet);Assert.True(owner.Analyzer.IsProcessRunning(27));Assert.Equal(2,logs.Count);
        Assert.All(logs,l=>Assert.Equal(new Log(EngineLogLevel.Error,null,"AIInformationAnalyzer.Update","Process wants to run, but no code provided"),l));
        owner.Analyzer.AddDisableRequest(27,"stop");logs.Clear();owner.Update(robot);Assert.Empty(logs);
    }
    [Fact] public void ActivityLifecycleRequestsReachSameAIAnalyzer()
    {
        var logs=new List<Log>();var owner=Owner(logs);var activity=new Activity{Id="Hiking",Strategy=new(),InfoAnalyzerProcess=1,Analyzer=owner.Analyzer};
        activity.OnSelected(1);Assert.True(owner.Analyzer.IsProcessRunning(1));activity.OnDeselected(2);Assert.False(owner.Analyzer.IsProcessRunning(1));
        Assert.Equal("AIInformationAnalyzer.RemoveEnableRequest",logs[^1].Key);
    }
    private sealed class Probe():SteppedBehavior("Probe","Probe"){protected override void OnStart(){} }
    [Fact] public void RequiredProcessWordGatesBeforeOtherChecksAndReportsNativeError()
    {
        using var robot=CozmoRobot.CreateOffline();var owner=new AIComponent(()=>1);var ctx=new BehaviorContext{Robot=robot,Triggers=new AnimationTriggerMap(),AI=owner};var p=new Probe{RequiredProcessId=1};bool later=false;p.RobotStateAllowsRun=()=>{later=true;return false;};
        var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        EngineErrorState.ErrorFlagSet=false;Assert.False(p.IsRunnableBase(ctx));Assert.False(later);Assert.True(EngineErrorState.ErrorFlagSet);
        var error=Assert.Single(logs.Where(l=>l.Name=="IBehavior.IsRunnable.RequiredProcessNotFound"));Assert.EndsWith("Required process 'CalculateInterestingRegions' is not enabled for 'Probe'",error.Text);Assert.Equal(EngineLogLevel.Error,error.Level);
        owner.Analyzer.AddEnableRequest(1,"x");Assert.False(p.IsRunnableBase(ctx));Assert.True(later);
        later=false;p.RequiredProcessId=0;p.RequiredProcessRunning=()=>throw new Exception("zero word must bypass");Assert.False(p.IsRunnableBase(ctx));Assert.True(later);
    }
    [Fact] public void BaseContentEqualityIgnoresTimestampsAndCliffKeepsStrictNaNBound()
    {
        var a=new SourceMapData(MemoryMapContentType.ClearOfObstacle,1,2);Assert.True(a.Same(new SourceMapData(MemoryMapContentType.ClearOfObstacle,900,901)));Assert.False(a.Same(null));Assert.False(a.Same(new SourceMapData(MemoryMapContentType.Cliff,1,2)));
        float tol=BitConverter.UInt32BitsToSingle(0x35A00000);var cliff=new CliffMapData(new Vec2(0,0),1);
        Assert.True(cliff.Same(new CliffMapData(new Vec2(MathF.BitDecrement(tol),0),7)));Assert.False(cliff.Same(new CliffMapData(new Vec2(tol,0),1)));Assert.False(cliff.Same(new CliffMapData(new Vec2(float.NaN,0),1)));
    }
}
