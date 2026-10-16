using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("ObjectID process statics")]
public sealed class BehaviorTeardownBoundaryTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public void ActualSignalUnlinkReportsCaptureBoundaryWithoutInvokingPayload(bool gameToEngine)
    {
        using var hub=new UiSubscriptionHub();int payloads=0;var missing=new List<string>();
        SteppedBehavior.MissingReported+=missing.Add;
        try
        {
            var handle=hub.SubscribeMessage(17,!gameToEngine,_=>payloads++);handle.Dispose();Assert.Equal(0,payloads);Assert.Equal(0,hub.Count(17,!gameToEngine));
            Assert.Single(missing.Where(s=>s.Contains("M8-023/M8-027")&&s.Contains("0066350E")));hub.Deliver(17,!gameToEngine);Assert.Equal(0,payloads);
        }
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact] public void SignalOwnerDestructionReportsOnceThenLateHandleIsExpired()
    {
        var hub=new UiSubscriptionHub();int payloads=0;var missing=new List<string>();var a=hub.Subscribe(1,false,()=>payloads++);var b=hub.Subscribe(1,true,()=>payloads++);
        SteppedBehavior.MissingReported+=missing.Add;
        try{hub.Dispose();a.Dispose();b.Dispose();Assert.Equal(0,payloads);Assert.Single(missing.Where(s=>s.Contains("M8-023/M8-027")));}
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact] public void CaptureDiagnosticDoesNotHoldSignalStorageGateAcrossExternalCallout()
    {
        using var hub=new UiSubscriptionHub();var handle=hub.Subscribe(19,false,()=>{});bool observed=false;
        void Missing(string text)
        {
            if(!text.Contains("M8-023/M8-027"))return;
            observed=true;Assert.True(Task.Run(()=>hub.Count(19,false)).Wait(TimeSpan.FromSeconds(2)));
        }
        SteppedBehavior.MissingReported+=Missing;
        try{handle.Dispose();Assert.True(observed);}
        finally{SteppedBehavior.MissingReported-=Missing;}
    }
    [Fact] public void RealEngineShutdownKeepsCaptureGapVisibleAfterMissingSinkDetaches()
    {
        var engine=new CozmoEngine(new M4ControlTests.FakePort(),new CozmoEngineOptions(),()=>0);var lines=new List<string>();var entries=new List<EngineLogEntry>();
        engine.StackLogLine+=lines.Add;engine.LogEntry+=entries.Add;using var handle=engine.AnimationMessages.Subscribe(23,false,()=>{});
        engine.Dispose();Assert.Contains(lines,s=>s.Contains("M8-023/M8-027")&&s.StartsWith("MISSING:",StringComparison.Ordinal));Assert.DoesNotContain(entries,e=>e.Text.Contains("M8-027"));
    }
    [Fact] public void NativeRobotOwnerSlotsRemainExplicitlyMissingAtRealTeardown()
    {
        using var robot=CozmoRobot.CreateOffline();var lines=new List<string>();robot.Engine.StackLogLine+=lines.Add;
        robot.Engine.Robots.RemoveRobot(1,false);
        Assert.Contains(lines,s=>s.Contains("owner +0x44")&&s.Contains("M8-016/M8-023"));Assert.Contains(lines,s=>s.Contains("owner +0x48")&&s.Contains("M8-016/M8-023"));
    }
}
