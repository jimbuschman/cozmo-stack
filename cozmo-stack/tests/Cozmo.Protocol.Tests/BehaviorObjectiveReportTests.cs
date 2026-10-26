using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class BehaviorObjectiveReportTests
{
    [Theory]
    [InlineData((byte)1,true,true)] [InlineData((byte)0,true,false)]
    [InlineData((byte)2,true,false)] [InlineData((byte)255,true,false)] [InlineData((byte)1,false,false)]
    public void ExactBroadcastGateKeepsDiagnosticsAndAppEnqueuePrecedesLocal(byte flag,bool external,bool sends)
    {
        // reach: unit (direct shared producer; substituted flag and external-interface state).
        // RC3 0x005BE22E/23A gate only broadcast; 0x006625CE precedes local0x006625FA.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var engine=rig.Engine;
        rig.Engine.Robot!.HasExternalInterface=external;
        var logs=new List<EngineLogEntry>();engine.LogEntry+=logs.Add;
        var host=new List<byte[]>();engine.EngineToGameMessage+=(tag,p)=>{if(tag==0x8B)host.Add(p.ToArray());};
        uint before=engine.NativeUiDeliveryCount;int local=0;
        using var subscription=engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>
        {
            Assert.Equal(4u,Assert.IsType<uint>(p));Assert.Empty(host);
            Assert.Equal(before+1,engine.NativeUiDeliveryCount);local++;
        });
        engine.ReportBehaviorObjective(4,"DroveAsIntended","DriveOffCharger",flag);
        Assert.Equal(sends?1:0,local);Assert.Empty(host);
        Assert.Equal(before+(sends?1u:0u),engine.NativeUiDeliveryCount);
        var diagnostics=logs.Where(e=>e.Name is "IBehavior.BehaviorObjectiveAchieved" or "robot.freeplay_objective_achieved").ToArray();
        Assert.Equal(2,diagnostics.Length);Assert.Equal("IBehavior.BehaviorObjectiveAchieved",diagnostics[0].Name);
        Assert.Equal("DriveOffCharger",diagnostics[1].Text);Assert.Equal("DroveAsIntended",diagnostics[1].Data["$data"]);
        engine.DrainAppDelivery();
        if(sends)Assert.Equal(new byte[]{4,0,0,0},Assert.Single(host));else Assert.Empty(host);
    }

    [Fact]
    public void DeferredHostHandlersRemainIsolated()
    {
        // reach: unit (direct source producer; intentionally throwing first host handler tests M1-034 isolation).
        using var rig=new M4ControlTests.Rig();rig.ToSynced();int second=0;
        rig.Engine.EngineToGameMessage+=(tag,_)=>{if(tag==0x8B)throw new InvalidOperationException("objective host fixture");};
        rig.Engine.EngineToGameMessage+=(tag,p)=>{if(tag==0x8B){Assert.Equal(new byte[]{4,0,0,0},p.ToArray());second++;}};
        rig.Engine.ReportBehaviorObjective(4,"DroveAsIntended","DriveOffCharger");Assert.Equal(0,second);
        rig.Engine.DrainAppDelivery();Assert.Equal(1,second);
    }

    private static void Admit(M4ControlTests.Rig rig,float x,bool contacts)
    {
        var robot=rig.Engine.Robot!;
        var state=rig.MakeState(RobotStatusFlag.IsBodyAccMode|(contacts?RobotStatusFlag.IsOnCharger:0),
            origin:robot.CurrentOriginId,frame:robot.PoseFrameId,x:x);
        rig.Data(state);rig.Tick(10);
        Assert.Equal(0u,robot.LastFullStateResult);Assert.Equal(x,(float)robot.GetCheckedMainPose().Translation.X);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public void ReceivedPathCompletionRunsActualDriveOffActionCallback(bool success)
    {
        // reach: unit (host-started behavior and deterministic async scheduler substitute native behavior/action admission,
        // whose M8 ownership gap remains visible; actual admitted RobotStates, received PathFollowingEvent,
        // real DriveStraight ActionList completion, queued behavior callback and objective producer are exercised).
        // 0x005C0EA4 requires full result0; objective4 broadcast precedes existing mood attempt0x005C0EE2.
        using var signals=SignalTestContext.Install();using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.Calibrate();
        Admit(rig,0,true);rig.Robot.Animations.ManualTicking=true;
        using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);
        var behavior=new DriveOffChargerBehavior(m){ActionTaskRunner=SignalTestContext.Schedule};
        var context=new BehaviorContext{Robot=rig.Robot,Triggers=new AnimationTriggerMap()};
        var order=new List<string>();var host=new List<byte[]>();
        rig.Engine.EngineToGameMessage+=(tag,p)=>{if(tag==0x8B)host.Add(p.ToArray());};
        using var local=rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>
        {Assert.Equal(4u,Assert.IsType<uint>(p));order.Add("objective");});
        behavior.Step+=s=>{if(s.Contains("emotion event DriveOffCharger"))order.Add("mood");};
        behavior.StartAsync(context,new BehaviorScope(),default).GetAwaiter().GetResult();
        try
        {
            Assert.True(SpinWait.SpinUntil(()=>
            {
                SignalTestContext.Drain();rig.Tick(10);
                return rig.SentSince(0).OfType<ExecutePath>().Any();
            },TimeSpan.FromSeconds(10)),"DriveStraight did not enqueue its actual path");
            var path=rig.Robot.Motion.Path;ushort id=path.Field42;
            if(success)
            {
                rig.Data(new PathFollowingEvent{EventId=id,EventType=0});rig.Tick(10);
                Assert.Equal(3,path.DriveToPoseStatus);
                Admit(rig,200,false);
            }
            else
            {
                // Native response timeout -> state6; the received cancel completion resolves to failure state0.
                rig.Tick(1001);Assert.Equal(6,path.DriveToPoseStatus);
            }
            rig.Data(new PathFollowingEvent{EventId=id,EventType=2});rig.Tick(10);
            Assert.True(SpinWait.SpinUntil(()=>
            {
                SignalTestContext.Drain();rig.Tick(10);
                if(behavior.HasPostedWork)behavior.Update(context,rig.Engine.Timer.SecondsF);
                return behavior.DriveResult.HasValue;
            },TimeSpan.FromSeconds(10)),"Real action completion did not reach the behavior callback");
            Assert.Equal(success?ActionResult.Success:ActionResult.FailedTraversingPath,behavior.DriveResult);
            Assert.Equal(success?new[]{"objective","mood"}:Array.Empty<string>(),order);
            if(success)
            {
                Assert.Contains(behavior.Trace,s=>s.Contains("emotion event DriveOffCharger: no mood attached"));
                rig.Engine.DrainAppDelivery();Assert.Equal(new byte[]{4,0,0,0},Assert.Single(host));
            }
            else{Assert.Empty(host);Assert.DoesNotContain(behavior.Trace,s=>s.Contains("emotion event DriveOffCharger"));}
            rig.Tick(10);SignalTestContext.Drain();
            Assert.Equal(success?1:0,order.Count(s=>s=="objective"));
        }
        finally{behavior.Stop(BehaviorStopReason.Completed);}
    }
}
