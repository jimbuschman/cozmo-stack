using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class AiTurnToFaceControlTests
{
    private sealed class Camera : IFaceDetector
    {
        public bool IsAvailable=>true;
        public string Description=>"supplied face";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image,uint timestamp)=>
            new[]{new DetectedFace(-7,new FaceRect(100,70,90,90),new Vec2(75,140),new Vec2(115,140),RollRad:0)
                {NativeNewEntryRotatingTooFast=false}};
    }

    private static void Observe(PickupPyramidNativeExecutionTests.Fixture f)
    {
        f.M.Vision.FaceDetector=new Camera();f.Head=.1f;f.Motors();
        for(uint id=500;id<504;id++)f.Image(id,modes:1<<2);
        Assert.NotNull(f.M.Vision.Faces.GetNativeFace(f.Robot,-7)?.NativeTrackingTranslation);
    }

    [Fact]
    public async Task MissingNonzeroIdDoesNotUseCachedFaceAndReportsVariantSix()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);Observe(f);
        var local=new List<RobotCompletedAction>();f.Robot.ActionList.RegisterActionEndedCallback(local.Add);
        var raw=new List<byte[]>();f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x5A)raw.Add(body.ToArray());};
        var typed=new List<ActionCompletionReport>();f.Rig.Engine.ActionCompleted+=typed.Add;
        int mark=f.Rig.Mark();var task=f.Rig.Robot.Motion.QueueAiTurnToFaceAsync(999,Radians.Pi);
        var runner=Assert.IsType<NativeLastFaceRunner>(f.Robot.ActionList.GetCurrentAction());f.Rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);Assert.Equal(0u,result.EngineResult);
        Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is SetHeadAngle or SetBodyAngle);
        Assert.False(f.Robot.ActionList.ContainsRunner(runner));
        var report=Assert.Single(local.Where(x=>x.Tag==runner.Tag));Assert.Equal((byte)6,report.CompletionVariant);
        Assert.Equal((byte)6,report.PackBody()[^1]);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Contains(raw,body=>BitConverter.ToUInt32(body,0)==runner.Tag && BitConverter.ToInt32(body,4)==0x29 &&
            BitConverter.ToUInt32(body,8)==0 && body[^1]==6);
        Assert.Equal((byte)6,Assert.Single(typed.Where(value=>value.Tag==runner.Tag)).CompletionVariant);
    }

    [Fact]
    public async Task ZeroIdUsesCachedFaceAndMaximumGate()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);Observe(f);
        using var cancel=new CancellationTokenSource();int mark=f.Rig.Mark();
        var task=f.Rig.Robot.Motion.QueueAiTurnToFaceAsync(0,-.01f,cancel.Token);
        var runner=Assert.IsType<NativeLastFaceRunner>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(.01f,runner.MaximumTurn,5);f.Rig.Tick();
        Assert.True(runner.TurnSkipped);
        Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is SetHeadAngle or SetBodyAngle);
        Assert.False(task.IsCompleted);
        cancel.Cancel();f.Rig.Tick();
        Assert.Equal(0x02000000u,(await task.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
    }

    [Fact]
    public async Task NoInterfaceMakesNonzeroSmartReferenceUseCachedFace()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);Observe(f);
        f.Robot.HasExternalInterface=false;
        using var cancel=new CancellationTokenSource();
        var task=f.Rig.Robot.Motion.QueueAiTurnToFaceAsync(999,.01f,cancel.Token);
        var runner=Assert.IsType<NativeLastFaceRunner>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(0,runner.RequestedId);
        f.Rig.Tick();Assert.True(runner.TurnSkipped);Assert.False(task.IsCompleted);
        cancel.Cancel();f.Rig.Tick();
        Assert.Equal(0x02000000u,(await task.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
    }

    [Fact]
    public async Task DeletedPendingIdUsesNativeCachedFaceBranch()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);Observe(f);
        var head=f.Rig.Robot.Motion.SetHeadAngleAsync(.5f);f.Rig.Tick();
        var predecessor=f.Robot.ActionList.GetCurrentAction()!;
        using var cancel=new CancellationTokenSource();
        var task=f.Rig.Robot.Motion.QueueAiTurnToFaceAsync(-7,.01f,cancel.Token);
        f.Rig.Engine.AnimationMessages.Deliver(0x4B,true,new NativeDeletedFace(-7));
        f.Robot.ActionList.Cancel(predecessor.Tag);f.Rig.Tick();
        var runner=Assert.IsType<NativeLastFaceRunner>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(0,runner.RequestedId);f.Rig.Tick();Assert.True(runner.TurnSkipped);
        cancel.Cancel();f.Rig.Tick();
        Assert.Equal(0x02000000u,(await task.WaitAsync(TimeSpan.FromSeconds(2))).EngineResult);
        await head;
    }

    [Fact]
    public async Task QueuedRemapAndWrappedMaximumArePreservedAtInit()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);Observe(f);
        var head=f.Rig.Robot.Motion.SetHeadAngleAsync(.5f);f.Rig.Tick();
        var predecessor=f.Robot.ActionList.GetCurrentAction()!;
        using var cancel=new CancellationTokenSource();
        var task=f.Rig.Robot.Motion.QueueAiTurnToFaceAsync(-7,-4f,cancel.Token);
        Assert.True(f.M.Vision.Faces.ChangeFaceID(-7,-8));
        f.Robot.ActionList.Cancel(predecessor.Tag);f.Rig.Tick();
        var runner=Assert.IsType<NativeLastFaceRunner>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(-8,runner.RequestedId);Assert.Equal(2.2831855f,runner.MaximumTurn,5);
        cancel.Cancel();f.Rig.Tick();
        Assert.True((await task.WaitAsync(TimeSpan.FromSeconds(2))).Admitted);
        await head;
    }

    [Fact]
    public async Task PublicExplicitFaceTurnsAndCompletesAfterRealObservations()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);Observe(f);
        int mark=f.Rig.Mark();var task=f.Rig.Robot.Motion.QueueAiTurnToFaceAsync(-7,Radians.Pi);
        var runner=Assert.IsType<NativeLastFaceRunner>(f.Robot.ActionList.GetCurrentAction());
        bool verification=false;
        for(uint i=0;i<100 && !task.IsCompleted;i++)
        {
            f.Motors();f.Image(510+i,modes:1<<2);
            verification|=runner.VerifiedId==-7;
        }
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);Assert.Equal(0u,result.EngineResult);
        Assert.Contains(f.Rig.SentSince(mark),m=>m is SetHeadAngle or SetBodyAngle);
        foreach(var head in f.Rig.SentSince(mark).OfType<SetHeadAngle>())
        {Assert.Equal(15f,head.MaxSpeedRadPerSec);Assert.Equal(20f,head.AccelRadPerSec2);}
        foreach(var body in f.Rig.SentSince(mark).OfType<SetBodyAngle>())
        {Assert.Equal(5.2359877f,MathF.Abs(body.MaxSpeedRadPerSec),5);Assert.Equal(10f,body.AccelRadPerSec2);}
        Assert.True(verification);
        Assert.True(f.M.Vision.Faces.GetFace(-7)!.TurnedTowards);
    }

    [Fact]
    public async Task PendingCancellationReleasesReferencesAndLeavesPredecessor()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var hub=f.Rig.Engine.AnimationMessages;int changed=hub.Count(0x47,true),deleted=hub.Count(0x4B,true);
        var head=f.Rig.Robot.Motion.SetHeadAngleAsync(.5f);f.Rig.Tick();
        using var cancel=new CancellationTokenSource();var task=f.Rig.Robot.Motion.QueueAiTurnToFaceAsync(-7,1,cancel.Token);
        Assert.Equal(changed+1,hub.Count(0x47,true));Assert.Equal(deleted+1,hub.Count(0x4B,true));
        cancel.Cancel();f.Rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));Assert.True(result.Admitted);Assert.Equal(0x02000001u,result.EngineResult);
        Assert.Equal(changed,hub.Count(0x47,true));Assert.Equal(deleted,hub.Count(0x4B,true));Assert.False(head.IsCompleted);
        f.Robot.ActionList.Cancel(-1);f.Rig.Tick();
    }

    [Fact]
    public async Task AdmissionRejectsInvalidAngleAndDisabledActions()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        Assert.False((await f.Rig.Robot.Motion.QueueAiTurnToFaceAsync(0,float.NaN)).Admitted);
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.False((await f.Rig.Robot.Motion.QueueAiTurnToFaceAsync(0,1,cancel.Token)).Admitted);
        int changed=f.Rig.Engine.AnimationMessages.Count(0x47,true),deleted=f.Rig.Engine.AnimationMessages.Count(0x4B,true);
        f.Robot.ActionList.ExternalActionsDisabled=true;
        Assert.False((await f.Rig.Robot.Motion.QueueAiTurnToFaceAsync(0,1)).Admitted);
        Assert.Equal(changed,f.Rig.Engine.AnimationMessages.Count(0x47,true));
        Assert.Equal(deleted,f.Rig.Engine.AnimationMessages.Count(0x4B,true));
    }

    [Fact]
    public async Task OwnerReplacementDuringMotorSendDoesNotTouchSuccessor()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);Observe(f);
        var owner=f.Robot;bool replaced=false;int mark=-1;
        owner.SendFault=message=>
        {
            if(!replaced && message is SetHeadAngle or SetBodyAngle)
            {
                replaced=true;f.Rig.Engine.Robots.RemoveRobot(1,false);f.Rig.Engine.Robots.AddRobot(1,withRic:false);
                mark=f.Rig.Mark();
            }
            return null;
        };
        try
        {
            var task=f.Rig.Robot.Motion.QueueAiTurnToFaceAsync(-7,Radians.Pi);f.Rig.Tick();
            Assert.True(replaced);
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);Assert.True(result.OwnerRetired);
            Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is SetHeadAngle or SetBodyAngle);
        }
        finally {owner.SendFault=null;}
    }

    [Fact]
    public async Task SuccessorStartedDuringReferenceReleaseKeepsItsLocksAndMotion()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);Observe(f);
        var owner=f.Robot;using var cancel=new CancellationTokenSource();
        var task=f.Rig.Robot.Motion.QueueAiTurnToFaceAsync(-7,Radians.Pi,cancel.Token);
        f.Rig.Tick();
        NativeTrackFaceRunner? successor=null;int mark=-1;
        var hub=f.Rig.Engine.AnimationMessages;
        hub.ReleaseObserved=line=>
        {
            if(successor is not null || line!="unlink E2G 47")return;
            successor=new NativeTrackFaceRunner(f.M,owner,-7);
            owner.ActionList.QueueAction(QueueActionPosition.InParallel,successor);
            owner.ActionList.Update();mark=f.Rig.Mark();
        };
        try
        {
            cancel.Cancel();f.Rig.Tick();
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(0x02000000u,result.EngineResult);
            Assert.NotNull(successor);Assert.True(owner.ActionList.ContainsRunner(successor));
            Assert.Equal(-7,owner.NativeTrackingFaceId);
            Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is MoveHead or DriveWheels);
        }
        finally {hub.ReleaseObserved=null;if(successor is not null)owner.ActionList.Cancel(successor.Tag);}
    }
}
