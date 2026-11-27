using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("ObjectID process statics")]
public sealed class BehaviorTeardownBoundaryTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public void ActualSignalUnlinkRetiresWithoutInvokingPayloadOrReportingAGap(bool gameToEngine)
    {
        using var hub=new UiSubscriptionHub();int payloads=0;var missing=new List<string>();
        SteppedBehavior.MissingReported+=missing.Add;
        try
        {
            var handle=hub.SubscribeMessage(17,!gameToEngine,_=>payloads++);handle.Dispose();Assert.Equal(0,payloads);Assert.Equal(0,hub.Count(17,!gameToEngine));
            Assert.DoesNotContain(missing,s=>s.Contains("M8-027"));hub.Deliver(17,!gameToEngine);Assert.Equal(0,payloads);
        }
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact] public void SignalOwnerDestructionIsSilentThenLateHandleIsExpired()
    {
        var hub=new UiSubscriptionHub();int payloads=0;var missing=new List<string>();var a=hub.Subscribe(1,false,()=>payloads++);var b=hub.Subscribe(1,true,()=>payloads++);
        SteppedBehavior.MissingReported+=missing.Add;
        try{hub.Dispose();a.Dispose();b.Dispose();Assert.Equal(0,payloads);Assert.DoesNotContain(missing,s=>s.Contains("M8-027"));}
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact] public void ConnectedRobotConstructsStateRecipients()
    {
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0);
        var owner=Assert.IsType<EngineRobot>(robot.Engine.Robot);
        Assert.NotNull(owner.GyroDetector);
        Assert.NotNull(owner.DockingOwner);
        Assert.NotNull(owner.CarryingOwner);
        Assert.NotNull(robot.Engine.PickAndPlaceRoute);
        Assert.NotNull(robot.Engine.NeedsRobotDisconnected);
        Assert.NotNull(owner.NativeBehaviorRuntime?.Manipulation);
    }
    [Fact] public void RealEngineShutdownRetiresSubscriptionsSilently()
    {
        var engine=new CozmoEngine(new M4ControlTests.FakePort(),new CozmoEngineOptions(),()=>0);var lines=new List<string>();var entries=new List<EngineLogEntry>();
        engine.StackLogLine+=lines.Add;engine.LogEntry+=entries.Add;using var handle=engine.AnimationMessages.Subscribe(23,false,()=>{});
        engine.Dispose();Assert.DoesNotContain(lines,s=>s.Contains("M8-023/M8-027")&&s.StartsWith("MISSING:",StringComparison.Ordinal));Assert.DoesNotContain(entries,e=>e.Text.Contains("M8-027"));
    }
    [Fact] public void NativeRobotOwnerSlotsRemainExplicitlyMissingAtRealTeardown()
    {
        using var robot=CozmoRobot.CreateOffline();var lines=new List<string>();robot.Engine.StackLogLine+=lines.Add;
        robot.Engine.Robots.RemoveRobot(1,false);
        Assert.Contains(lines,s=>s.Contains("owner +0x44")&&s.Contains("M8-016/M8-023"));Assert.Contains(lines,s=>s.Contains("owner +0x48")&&s.Contains("M8-016/M8-023"));
    }
}
