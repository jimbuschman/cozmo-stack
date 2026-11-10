using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

public sealed class NativeImagePointTests
{
    [Theory]
    [InlineData(160f,120f,-3.14159274f,-3.14159274f,0x40490FDBu,0x40490FDBu)]
    [InlineData(200.5f,33.25f,0.4f,0.1f,0x3E85C18Cu,0x3EC70EB4u)]
    [InlineData(450f,412f,-2.8f,-2.8f,0x402CA88Cu,0x402CA88Cu)]
    [InlineData(-130f,-172f,2.8f,2.8f,0xC02CA88Cu,0xC02CA88Cu)]
    [InlineData(450f,412f,10.9f,-10.9f,0xC01CE9CCu,0x3F618770u)]
    public void NativeHelperMatchesOriginalArm(float x,float y,float yaw,float head,uint expectedBody,uint expectedHead)
    {
        var cal=CameraCalibration.Nominal() with { FocalLengthX=290, FocalLengthY=292, CenterX=160, CenterY=120 };
        var (body,tilt)=TurnTowardsImagePoint.Angles(cal,x,y,yaw,head);
        Assert.Equal(expectedBody,BitConverter.SingleToUInt32Bits((float)body));
        Assert.Equal(expectedHead,BitConverter.SingleToUInt32Bits((float)tilt));
    }

    [Fact]
    public void NativeHelperPreservesNegativeZero()
    {
        var cal=CameraCalibration.Nominal() with { FocalLengthX=290, FocalLengthY=292, CenterX=160, CenterY=120 };
        float negativeZero=BitConverter.UInt32BitsToSingle(0x80000000);
        var (body,head)=TurnTowardsImagePoint.Angles(cal,160,120,negativeZero,negativeZero);
        Assert.Equal(0x80000000u,BitConverter.SingleToUInt32Bits((float)body));
        Assert.Equal(0x80000000u,BitConverter.SingleToUInt32Bits((float)head));
    }

    [Fact]
    public async Task AttachedActionUsesOuterCalibrationAndRealMotorChildren()
    {
        using var scene=new Fixture();
        var vision=scene.M.Vision;
        var outer=vision.Calibration! with { FocalLengthX=290, FocalLengthY=292, CenterX=160, CenterY=120 };
        vision.SetNativeOuterCalibration(outer,scene.Robot);
        vision.Calibration=outer with { FocalLengthX=900, FocalLengthY=900 };
        uint timestamp=scene.Robot.StoredState!.Timestamp;
        int mark=scene.Rig.Mark();
        var task=TurnTowardsImagePoint.RunAsync(vision,240,40,timestamp,CancellationToken.None);
        for(int i=0;i<80 && !task.IsCompleted;i++){scene.Rig.Tick();scene.Motors();}
        Assert.True(await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotEmpty(scene.Rig.SentSince(mark).OfType<SetBodyAngle>());
        Assert.NotEmpty(scene.Rig.SentSince(mark).OfType<SetHeadAngle>());
        var body=scene.Rig.SentSince(mark).OfType<SetBodyAngle>().First();
        Assert.Equal(MathF.Atan2(-80,290),body.AngleRad,5);
        var head=scene.Rig.SentSince(mark).OfType<SetHeadAngle>().First();
        Assert.Equal(MathF.Atan2(80,292)-0.15f,head.AngleRad,5);
    }

    [Fact]
    public async Task MissingHistoryDoesNotStartMotorChildren()
    {
        using var scene=new Fixture();
        int mark=scene.Rig.Mark();
        var task=TurnTowardsImagePoint.RunAsync(scene.M.Vision,240,40,uint.MaxValue,CancellationToken.None);
        for(int i=0;i<10 && !task.IsCompleted;i++)scene.Rig.Tick();
        Assert.False(await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(scene.Rig.SentSince(mark).OfType<SetBodyAngle>());
        Assert.Empty(scene.Rig.SentSince(mark).OfType<SetHeadAngle>());
    }

    [Fact]
    public async Task ReplacedOwnerCannotRunQueuedAction()
    {
        using var scene=new Fixture();
        var old=scene.Robot;
        int mark=scene.Rig.Mark();
        var task=TurnTowardsImagePoint.RunAsync(scene.M.Vision,240,40,old.StoredState!.Timestamp,CancellationToken.None);
        scene.Rig.Engine.Robots.RemoveRobot(1,false);
        scene.Rig.Engine.Robots.AddRobot(1,withRic:false);
        for(int i=0;i<10 && !task.IsCompleted;i++)scene.Rig.Tick();
        Assert.False(await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(scene.Rig.SentSince(mark).OfType<SetBodyAngle>());
        Assert.Empty(scene.Rig.SentSince(mark).OfType<SetHeadAngle>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationRetiresOnlyThisAction(bool start)
    {
        using var scene=new Fixture();
        using var cancel=new CancellationTokenSource();
        var task=TurnTowardsImagePoint.RunAsync(scene.M.Vision,240,40,scene.Robot.StoredState!.Timestamp,cancel.Token);
        if(start)scene.Rig.Tick();
        cancel.Cancel();
        for(int i=0;i<10 && !task.IsCompleted;i++)scene.Rig.Tick();
        Assert.False(await task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task CanonicalVisionWithoutRuntimeDoesNotUseLegacyMotorPath()
    {
        using var scene=new Fixture();
        var owner=scene.Robot;
        var runtime=owner.NativeBehaviorRuntime;
        int mark=scene.Rig.Mark();
        try
        {
            owner.NativeBehaviorRuntime=null;
            Assert.False(await TurnTowardsImagePoint.RunAsync(scene.Rig.Robot.Vision,240,40,owner.StoredState!.Timestamp,CancellationToken.None));
            Assert.Empty(scene.Rig.SentSince(mark).OfType<SetBodyAngle>());
            Assert.Empty(scene.Rig.SentSince(mark).OfType<SetHeadAngle>());
        }
        finally { owner.NativeBehaviorRuntime=runtime; }
    }

    [Fact]
    public async Task AttachedActionUsesNonInterpolatedHistory()
    {
        using var scene=new Fixture();
        var owner=scene.Robot;
        var first=scene.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:owner.CurrentOriginId,head:-0.3f,frame:owner.PoseFrameId);
        scene.Rig.Data(first);scene.Rig.Tick();
        var second=scene.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:owner.CurrentOriginId,head:0.3f,frame:owner.PoseFrameId);
        scene.Rig.Data(second);scene.Rig.Tick();
        uint stamp=first.Timestamp+8;
        using var nearest=new HistRobotState();using var interpolated=new HistRobotState();uint time=0;
        Assert.Equal(0u,owner.PoseHistory.ComputeStateAt(stamp,ref time,nearest,false));
        Assert.Equal(0u,owner.PoseHistory.ComputeStateAt(stamp,ref time,interpolated,true));
        Assert.NotEqual(nearest.State.HeadAngle,interpolated.State.HeadAngle);
        int mark=scene.Rig.Mark();
        using var cancel=new CancellationTokenSource();
        var task=TurnTowardsImagePoint.RunAsync(scene.M.Vision,160,120,stamp,cancel.Token);
        scene.Rig.Tick();
        var head=Assert.Single(scene.Rig.SentSince(mark).OfType<SetHeadAngle>());
        Assert.Equal(nearest.State.HeadAngle,head.AngleRad,5);
        cancel.Cancel();
        for(int i=0;i<10 && !task.IsCompleted;i++)scene.Rig.Tick();
        Assert.False(await task.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
