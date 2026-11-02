using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeMotionDetectorTests
{
    [Fact]
    public void ConstantSuppliedTypedFramesReachRealComparisonWithoutReport()
    {
        string path=Enumerable.Range(0,1).SelectMany(_=>Ancestors(AppContext.BaseDirectory)).Select(d=>Path.Combine(d,"re-analysis","obb","assets","cozmo_resources","config","engine","vision_config.json")).First(File.Exists);
        var config=new FirmwareJsonValue();Assert.True(Json.TryParseInto(File.ReadAllBytes(path),config));
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var owner=rig.Engine.Robot!;
        using var robotPose=new Pose3d(Rotation3d.Identity,Vec3.Zero);
        var raw=new RobotState{Timestamp=1000,HeadAngle=0,Status=0x200,Pose=new RobotPose(),Accel=new AccelData(),Gyro=new GyroData(),CliffDataRaw=new ushort[4]};
        using var history=HistRobotState.From(robotPose,raw);
        using var first=new NativeGroundFrame(owner,1000,history,false,new float[9]);
        using var second=new NativeGroundFrame(owner,1601,history,false,new float[9]);
        using var poses=new NativeMotionPoseState();var reports=new List<NativeObservedMotion>();
        var detector=new NativeMotionDetector(config,_=>{},()=>true);
        poses.Admit(first);Assert.True(detector.Detect(new NativeMotionImage(120,160,1,160,Enumerable.Repeat((byte)100,19200).ToArray(),1000),poses,320,240,reports));
        poses.Admit(second);Assert.True(detector.Detect(new NativeMotionImage(120,160,1,160,Enumerable.Repeat((byte)100,19200).ToArray(),1601),poses,320,240,reports));
        Assert.Equal(1u,detector.ComparisonCount);Assert.Empty(reports);
    }
    private static IEnumerable<string> Ancestors(string path){for(var dir=new DirectoryInfo(path);dir is not null;dir=dir.Parent)yield return dir.FullName;}
}
