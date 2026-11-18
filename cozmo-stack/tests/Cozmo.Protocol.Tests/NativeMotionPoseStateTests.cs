using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
public sealed class NativeMotionPoseStateTests
{
    // 0x006B00A2..B2 / 0x006503A8 construct a default previous carrier;
    // 0x006B3576..0x006B35F4 still compares it on the first detection.
    [Theory]
    [InlineData(0f,0f,false)]
    [InlineData(10f,0f,true)]
    [InlineData(0f,.2f,true)]
    public void FirstFaceFrameComparesAgainstTheDefaultCarrier(float x,float head,bool reset)
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        using var frame=Frame(rig.Engine.Robot!,x,0,head,1);
        using var worker=new NativeMotionPoseState();worker.Admit(frame);
        Assert.False(worker.HasPrevious);
        Assert.Equal(reset,worker.ResetFaceTracking());
        worker.Admit(frame);
        Assert.False(worker.ResetFaceTracking());
    }
    private static NativeGroundFrame Frame(EngineRobot owner,float x,float y,float head,uint timestamp)
    {
        using var pose=new Pose3d(Rotation3d.Identity,new Vec3(x,y,0));
        var state=new RobotState{Timestamp=timestamp,HeadAngle=head,Pose=new RobotPose(),Accel=new AccelData(),Gyro=new GyroData(),CliffDataRaw=new ushort[4]};
        using var history=HistRobotState.From(pose,state);
        return new NativeGroundFrame(owner,timestamp,history,false,new float[9]);
    }
    [Fact]
    public void WorkerCopiesEachAdmittedCarrierAndUsesStrictLocalThresholds()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var owner=rig.Engine.Robot!;
        using var a=Frame(owner,0,0,0,1);using var b=Frame(owner,.5f,0,0,2);
        using var worker=new NativeMotionPoseState();worker.Admit(a);
        Assert.True(worker.HasCurrent);Assert.False(worker.HasPrevious);Assert.Equal(1u,worker.FrameCount);
        worker.Admit(b);Assert.True(worker.HasPrevious);Assert.Equal(2u,worker.FrameCount);
        Assert.False(NativeMotionPoseState.BodySame(worker.Current!,worker.Previous!));
        Assert.True(NativeMotionPoseState.HeadSame(worker.Current!,worker.Previous!));
        Assert.NotSame(a.RobotPose,worker.Previous!.RobotPose);
        Assert.Same(worker.Current!.RobotPose.Node,worker.Current.CameraPose.Node!.Parent);
    }
    [Fact]
    public void HeadEqualityFailsAndIndependentSubHalfMillimeterAxesPass()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var owner=rig.Engine.Robot!;
        using var a=Frame(owner,0,0,0,1);using var b=Frame(owner,.49f,.49f,BitConverter.UInt32BitsToSingle(0x3AE4C388),2);
        Assert.True(NativeMotionPoseState.BodySame(a,b));Assert.False(NativeMotionPoseState.HeadSame(a,b));
    }
}
