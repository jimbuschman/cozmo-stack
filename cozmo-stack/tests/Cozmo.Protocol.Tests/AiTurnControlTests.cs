using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class AiTurnControlTests
{
    [Fact]
    public async Task RelativeTurnUsesNativeWireAndMatchingAck()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var rig=f.Rig;
        var owner=f.Robot;
        var reports=new List<RobotCompletedAction>();
        owner.ActionList.RegisterActionEndedCallback(reports.Add);
        var raw=new List<byte[]>();
        void Report(ushort tag,ReadOnlyMemory<byte> body){if(tag==0x5A)raw.Add(body.ToArray());}
        rig.Engine.EngineToGameMessage+=Report;
        int mark=rig.Mark();
        var task=rig.Robot.Motion.QueueAiTurnInPlaceAsync(-4f,8f,-3f,2*MathF.PI);
        var runner=owner.ActionList.GetCurrentAction()!;
        rig.Tick();
        var wire=Assert.Single(rig.SentSince(mark).OfType<SetBodyAngle>());
        Assert.Equal(2.2831855f,wire.AngleRad);
        Assert.Equal(-Cozmo.Robot.Vision.TurnInPlaceAction.DefaultMaxSpeed,wire.MaxSpeedRadPerSec);
        Assert.Equal(-3f,wire.AccelRadPerSec2);
        Assert.Equal(Cozmo.Robot.Vision.TurnInPlaceAction.MinToleranceRad,wire.ToleranceRad);
        Assert.Equal((ushort)1,wire.NumHalfRevolutions);
        Assert.False(wire.IsAbsolute);
        rig.Data(new MotorActionAck{ActionId=unchecked((byte)(wire.ActionId+1))});rig.Tick();
        Assert.False(task.IsCompleted);
        rig.Data(new MotorActionAck{ActionId=wire.ActionId});rig.Tick();
        owner.ActionList.Cancel(runner.Tag);rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(0x02000000u,result.EngineResult);
        Assert.Equal(0u,result.RelocalizationCount);
        var report=Assert.Single(reports.Where(x=>x.Tag==runner.Tag));
        Assert.Equal((byte)5,report.CompletionVariant);
        rig.Engine.DrainAppDelivery();
        Assert.Contains(raw,body=>body.Length>=5 && BitConverter.ToUInt32(body,0)==runner.Tag && body[^5]==5 && BitConverter.ToUInt32(body,body.Length-4)==0);
        rig.Engine.EngineToGameMessage-=Report;
    }

    [Fact]
    public async Task AbsoluteTurnAndCustomProfileUseOriginalQueue()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var rig=f.Rig;
        var path=rig.Robot.Motion.Path;
        path.SetNativeCustomMotionProfile(PathMotionProfile.Default with { PointTurnSpeedRadPerSec=1.25f,PointTurnAccelRadPerSec2=3.5f });
        using var cancel=new CancellationTokenSource();
        int mark=rig.Mark();
        var task=rig.Robot.Motion.QueueAiTurnInPlaceAsync(1f,0,0,absolute:true,cancellationToken:cancel.Token);
        rig.Tick();
        var wire=Assert.Single(rig.SentSince(mark).OfType<SetBodyAngle>());
        Assert.Equal(1f,wire.AngleRad);
        Assert.True(wire.IsAbsolute);
        Assert.Equal((ushort)0,wire.NumHalfRevolutions);
        Assert.Equal(1.25f,wire.MaxSpeedRadPerSec);
        Assert.Equal(3.5f,wire.AccelRadPerSec2);
        cancel.Cancel();rig.Tick();
        Assert.Equal(0x02000000u,(await task.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        using var manualCancel=new CancellationTokenSource();
        mark=rig.Mark();
        var manual=rig.Robot.Motion.QueueAiTurnInPlaceAsync(1f,2.25f,4.5f,absolute:true,cancellationToken:manualCancel.Token);
        rig.Tick();
        var manualWire=Assert.Single(rig.SentSince(mark).OfType<SetBodyAngle>());
        Assert.Equal(1f,manualWire.AngleRad);
        Assert.Equal(2.25f,manualWire.MaxSpeedRadPerSec);
        Assert.Equal(4.5f,manualWire.AccelRadPerSec2);
        manualCancel.Cancel();rig.Tick();
        Assert.Equal(0x02000000u,(await manual.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        path.ClearNativeCustomMotionProfile();
    }

    [Fact]
    public async Task PendingCancellationAndReadinessDoNotSendTurn()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var rig=f.Rig;
        var owner=f.Robot;
        Assert.False((await rig.Robot.Motion.QueueAiTurnInPlaceAsync(float.NaN,1,1)).Admitted);
        owner.ActionList.ExternalActionsDisabled=true;
        Assert.False((await rig.Robot.Motion.QueueAiTurnInPlaceAsync(1,1,1)).Admitted);
        owner.ActionList.ExternalActionsDisabled=false;
        var head=rig.Robot.Motion.SetHeadAngleAsync(.5f);rig.Tick();
        using var cancel=new CancellationTokenSource();
        int mark=rig.Mark();
        var task=rig.Robot.Motion.QueueAiTurnInPlaceAsync(1,1,1,cancellationToken:cancel.Token);
        cancel.Cancel();rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.Equal(0x02000001u,result.EngineResult);
        Assert.Empty(rig.SentSince(mark).OfType<SetBodyAngle>());
        Assert.False(head.IsCompleted);
        owner.ActionList.Cancel(-1);rig.Tick();
    }

    [Fact]
    public async Task MatchingAckAndPositionCompleteAndReportRelocalization()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var rig=f.Rig;
        var owner=f.Robot;
        var reports=new List<RobotCompletedAction>();
        owner.ActionList.RegisterActionEndedCallback(reports.Add);
        var raw=new List<byte[]>();
        void Report(ushort tag,ReadOnlyMemory<byte> body){if(tag==0x5A)raw.Add(body.ToArray());}
        rig.Engine.EngineToGameMessage+=Report;
        int mark=rig.Mark();
        var task=rig.Robot.Motion.QueueAiTurnInPlaceAsync(1f,1f,2f);
        var runner=owner.ActionList.GetCurrentAction()!;
        rig.Tick();
        var wire=Assert.Single(rig.SentSince(mark).OfType<SetBodyAngle>());
        using (var pose=new Cozmo.Robot.Vision.Pose3d(owner.MainPose)) owner.SetNewPose(pose);
        var state=rig.MakeState(origin:owner.CurrentOriginId,frame:owner.PoseFrameId);
        state.Pose.Angle=wire.AngleRad;
        rig.Data(state);rig.Tick();
        rig.Data(new MotorActionAck{ActionId=unchecked((byte)(wire.ActionId+1))});rig.Tick();
        Assert.False(task.IsCompleted);
        rig.Data(new MotorActionAck{ActionId=wire.ActionId});rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0u,result.EngineResult);
        Assert.Equal(1u,result.RelocalizationCount);
        var report=Assert.Single(reports.Where(x=>x.Tag==runner.Tag));
        Assert.Equal((byte)5,report.CompletionVariant);
        Assert.Equal(1u,report.CompletionUnion);
        rig.Engine.DrainAppDelivery();
        Assert.Contains(raw,body=>body.Length>=5 && BitConverter.ToUInt32(body,0)==runner.Tag && body[^5]==5 && BitConverter.ToUInt32(body,body.Length-4)==1);
        rig.Engine.EngineToGameMessage-=Report;
    }

    [Fact]
    public async Task OwnerReplacementRetiresAdmittedTurn()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var rig=f.Rig;
        var original=f.Robot;
        var task=rig.Robot.Motion.QueueAiTurnInPlaceAsync(1f,1f,2f);
        rig.Tick();
        rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);
        rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        int mark=rig.Mark();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);
        Assert.True(result.OwnerRetired);
        Assert.NotSame(original,rig.Engine.Robot);
        rig.Tick();
        Assert.Empty(rig.SentSince(mark).OfType<SetBodyAngle>());
    }

    [Fact]
    public async Task InPositionSucceedsWithoutMotorAndMissingAckTimesOut()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var rig=f.Rig;
        int mark=rig.Mark();
        var stationary=rig.Robot.Motion.QueueAiTurnInPlaceAsync(0,1,1);
        rig.Tick();
        Assert.Equal(0u,(await stationary.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        Assert.Empty(rig.SentSince(mark).OfType<SetBodyAngle>());
        var timed=rig.Robot.Motion.QueueAiTurnInPlaceAsync(1,1,1);
        rig.Tick();
        Assert.Single(rig.SentSince(mark).OfType<SetBodyAngle>());
        rig.Tick(30_001);
        Assert.Equal(0x03000018u,(await timed.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
    }

    [Fact]
    public async Task RelativeTurnAccumulatesYawAcrossHalfRevolutions()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var rig=f.Rig;
        var owner=f.Robot;
        int mark=rig.Mark();
        var task=rig.Robot.Motion.QueueAiTurnInPlaceAsync(-4f,1,2);
        rig.Tick();
        var wire=Assert.Single(rig.SentSince(mark).OfType<SetBodyAngle>());
        rig.Data(new MotorActionAck{ActionId=wire.ActionId});rig.Tick();
        foreach (float angle in new[]{-1f,-2f,-3f,-4f})
        {
            var state=rig.MakeState(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.AreWheelsMoving,origin:owner.CurrentOriginId,frame:owner.PoseFrameId);
            state.Pose.Angle=angle;
            rig.Data(state);rig.Tick();
            Assert.False(task.IsCompleted);
        }
        var stopped=rig.MakeState(origin:owner.CurrentOriginId,frame:owner.PoseFrameId);
        stopped.Pose.Angle=-4f;
        rig.Data(stopped);rig.Tick();
        Assert.Equal(0u,(await task.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
    }
}
