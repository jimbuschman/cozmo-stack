using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class AiTrackFaceControlTests
{
    private sealed class Camera : IFaceDetector
    {
        public bool IsAvailable=>true;
        public string Description=>"supplied tracking face";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image,uint timestamp)=>
            new[]{new DetectedFace(-7,new FaceRect(100,70,90,90),new Vec2(75,140),new Vec2(115,140),RollRad:0)
                {NativeNewEntryRotatingTooFast=false}};
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicTrackingUsesCurrentCameraAndMatchingMode(bool headOnly)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,sourceNvEmpty:true);
        f.M.Vision.FaceDetector=new Camera();f.Head=.1f;f.Motors();f.Image(500,modes:1<<2);
        Assert.NotNull(f.M.Vision.Faces.GetNativeFace(f.Robot,-7)?.NativeTrackingTranslation);
        using var cancel=new CancellationTokenSource();int mark=f.Rig.Mark();
        var task=f.Rig.Robot.Motion.QueueAiTrackFaceAsync(-7,headOnly,cancel.Token);
        var runner=Assert.IsType<NativeTrackFaceRunner>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(headOnly?1u:5u,runner.RequiredTrackMask);
        f.Rig.Tick();
        Assert.Single(f.Rig.SentSince(mark).OfType<SetHeadAngle>());
        Assert.Equal(headOnly?0:1,f.Rig.SentSince(mark).OfType<SetBodyAngle>().Count());
        mark=f.Rig.Mark();f.Rig.Tick();
        Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is SetHeadAngle or SetBodyAngle);
        Assert.True(f.M.Vision.Faces.ChangeFaceID(-7,-22));
        Assert.Equal(-7,f.Robot.NativeTrackingFaceId);
        mark=f.Rig.Mark();f.Rig.Tick();
        Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is SetHeadAngle or SetBodyAngle);
        cancel.Cancel();f.Rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);Assert.Equal(0x02000000u,result.EngineResult);Assert.Equal(-22,result.FaceId);
        Assert.False(f.Robot.ActionList.ContainsRunner(runner));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-19)]
    public async Task AbsentSignedTargetWaitsAndReturnsItsVariantThreeId(int id)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var reports=new List<RobotCompletedAction>();f.Robot.ActionList.RegisterActionEndedCallback(reports.Add);
        var raw=new List<byte[]>();f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x5A)raw.Add(body.ToArray());};
        var typed=new List<ActionCompletionReport>();f.Rig.Engine.ActionCompleted+=typed.Add;
        using var cancel=new CancellationTokenSource();int mark=f.Rig.Mark();
        var task=f.Rig.Robot.Motion.QueueAiTrackFaceAsync(id,false,cancel.Token);
        var runner=Assert.IsType<NativeTrackFaceRunner>(f.Robot.ActionList.GetCurrentAction());
        for(int i=0;i<3;i++)f.Rig.Tick();
        Assert.False(task.IsCompleted);
        Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is SetHeadAngle or SetBodyAngle);
        cancel.Cancel();f.Rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(id,result.FaceId);Assert.Equal(0x02000000u,result.EngineResult);
        var report=Assert.Single(reports.Where(r=>r.Tag==runner.Tag));
        Assert.Equal((byte)3,report.CompletionVariant);Assert.Equal(unchecked((uint)id),report.CompletionUnion);
        Assert.Equal(new[]{(byte)3}.Concat(BitConverter.GetBytes(id)),report.PackBody()[^5..]);
        f.Rig.Engine.DrainAppDelivery();Assert.Contains(raw,body=>body.SequenceEqual(report.PackBody()));
        var app=Assert.Single(typed.Where(value=>value.Tag==runner.Tag));
        Assert.Equal((byte)3,app.CompletionVariant);Assert.Equal(unchecked((uint)id),app.NativeCompletionCache);
    }

    [Fact]
    public async Task MatchingRemapChangesCompletionButNotPublishedMovementId()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        using var cancel=new CancellationTokenSource();
        var task=f.Rig.Robot.Motion.QueueAiTrackFaceAsync(-7,true,cancel.Token);
        var runner=Assert.IsType<NativeTrackFaceRunner>(f.Robot.ActionList.GetCurrentAction());
        f.Rig.Tick();Assert.Equal(-7,f.Robot.NativeTrackingFaceId);
        f.Rig.Engine.AnimationMessages.Deliver(0x47,true,new NativeChangedFace(-8,11));
        f.Rig.Engine.AnimationMessages.Deliver(0x47,true,new NativeChangedFace(-7,-22));
        Assert.Equal(-7,f.Robot.NativeTrackingFaceId);
        cancel.Cancel();f.Rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(-22,result.FaceId);Assert.Equal(0x02000000u,result.EngineResult);
        Assert.False(f.Robot.ActionList.ContainsRunner(runner));
    }

    [Fact]
    public async Task PendingExactCancellationLeavesPredecessorRunning()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var head=f.Rig.Robot.Motion.SetHeadAngleAsync(.5f);f.Rig.Tick();
        using var cancel=new CancellationTokenSource();
        var task=f.Rig.Robot.Motion.QueueAiTrackFaceAsync(0,false,cancel.Token);
        cancel.Cancel();f.Rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);Assert.Equal(0x02000001u,result.EngineResult);Assert.Equal(0,result.FaceId);
        Assert.False(head.IsCompleted);
        f.Robot.ActionList.Cancel(-1);f.Rig.Tick();
    }

    [Fact]
    public async Task AdmissionHonorsCancellationAndReactionGate()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.False((await f.Rig.Robot.Motion.QueueAiTrackFaceAsync(-1,false,cancel.Token)).Admitted);
        f.Robot.ActionList.ExternalActionsDisabled=true;
        Assert.False((await f.Rig.Robot.Motion.QueueAiTrackFaceAsync(-1,false)).Admitted);
    }

    [Fact]
    public async Task MissingInterfaceUsesNativeInitFailureAfterAdmission()
    {
        bool previous=EngineErrorState.ErrorFlagSet;
        try
        {
            using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
            f.Robot.HasExternalInterface=false;EngineErrorState.ErrorFlagSet=false;
            var task=f.Rig.Robot.Motion.QueueAiTrackFaceAsync(-7,false);f.Rig.Tick();
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);Assert.Equal(0x03000000u,result.EngineResult);
            Assert.Equal(-7,result.FaceId);Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally {EngineErrorState.ErrorFlagSet=previous;}
    }

    [Fact]
    public async Task OwnerReplacementDuringHeadSendDoesNotDriveSuccessor()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,sourceNvEmpty:true);
        f.M.Vision.FaceDetector=new Camera();f.Head=.1f;f.Motors();f.Image(500,modes:1<<2);
        var owner=f.Robot;bool replaced=false;int mark=-1;
        owner.SendFault=message=>
        {
            if(!replaced && message is SetHeadAngle)
            {
                replaced=true;f.Rig.Engine.Robots.RemoveRobot(1,false);f.Rig.Engine.Robots.AddRobot(1,withRic:false);
                mark=f.Rig.Mark();
            }
            return null;
        };
        try
        {
            var task=f.Rig.Robot.Motion.QueueAiTrackFaceAsync(-7,false);f.Rig.Tick();
            Assert.True(replaced);
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);Assert.True(result.OwnerRetired);
            Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is SetHeadAngle or SetBodyAngle);
        }
        finally {owner.SendFault=null;}
    }
}
