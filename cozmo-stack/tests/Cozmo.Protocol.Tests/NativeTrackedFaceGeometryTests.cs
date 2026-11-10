using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

public sealed class NativeTrackedFaceGeometryTests
{
    private sealed class FaceDetector : IFaceDetector
    {
        public bool IsAvailable => true;
        public string Description => "supplied face geometry";
        public bool Parts = true;
        public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp) =>
            new[] { new DetectedFace(7, new FaceRect(110,65,58,64),
                Parts ? new Vec2(126,91) : null, Parts ? new Vec2(151,94) : null,
                Name: "Ada", RollRad: .25) { NativeNewEntryRotatingTooFast = false } };
    }

    [Fact]
    public void NativeCompositionMatchesIndependentArmWords()
    {
        float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
        var rotation = Rotation3d.FromAngleAxis(F(0x3FC90FDB), new Vec3(0,0,1));
        var camera = new Transform3d(rotation, new Vec3(10,20,30));
        var robot = new Transform3d(rotation, new Vec3(40,50,60));
        var pose = VisionSystem.ComposeNativeFacePose(new Vec3(F(0xC24BB833),F(0xC28164B7),F(0x442BBD85)),camera,robot);
        Assert.Equal(0x428DDC19u,BitConverter.SingleToUInt32Bits((float)pose.Translation.X));
        Assert.Equal(0x42F964B5u,BitConverter.SingleToUInt32Bits((float)pose.Translation.Y));
        Assert.Equal(0x44423D83u,BitConverter.SingleToUInt32Bits((float)pose.Translation.Z));
        Assert.Equal(0L,BitConverter.DoubleToInt64Bits(pose.Rotation.W));
        Assert.Equal(0L,BitConverter.DoubleToInt64Bits(pose.Rotation.X));
        Assert.Equal(0x3FE6A09E667F3BCD,BitConverter.DoubleToInt64Bits(pose.Rotation.Y));
        Assert.Equal(0x3FE6A09E667F3BCD,BitConverter.DoubleToInt64Bits(pose.Rotation.Z));
    }

    [Theory]
    [InlineData(true, 0xC24BB833u, 0xC28164B7u, 0x442BBD85u)]
    [InlineData(false, 0xC38CE8D0u, 0xC351EA9Bu, 0x43FF65F8u)]
    public void CameraProjectionMatchesOriginalArmWords(bool parts, uint x, uint y, uint z)
    {
        var face = new TrackedFace(new DetectedFace(7,new FaceRect(110,65,58,64),
            parts ? new Vec2(126,91) : null, parts ? new Vec2(151,94) : null, RollRad:.25),1);
        var cal = CameraCalibration.Nominal() with { FocalLengthX=290, FocalLengthY=292, CenterX=160, CenterY=120, Skew=0 };
        face.UpdateNativeCameraTranslation(cal);
        Assert.Equal(x,BitConverter.SingleToUInt32Bits((float)face.NativeCameraTranslation.X));
        Assert.Equal(y,BitConverter.SingleToUInt32Bits((float)face.NativeCameraTranslation.Y));
        Assert.Equal(z,BitConverter.SingleToUInt32Bits((float)face.NativeCameraTranslation.Z));
    }

    [Fact]
    public void QueuedFramesKeepDetachedInputAndOriginOwnedFaceAcrossRelocalization()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        var vision=f.M.Vision;var world=vision.Faces;var detector=new FaceDetector();
        vision.FaceDetector=detector;vision.Enabled=true;vision.ModeEnableMask=1<<2;
        vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        f.Rig.Robot.SetVisionWhileMoving(true);f.Rig.Tick();
        FaceObservation? observed=null;bool queryCompleted=false;
        world.FaceObserved+=o=>
        {
            observed=o;
            queryCompleted=Task.Run(()=>world.GetFaceIDsObservedSince(0)).Wait(TimeSpan.FromSeconds(2));
        };
        void Frame(uint id,float head)
        {
            f.Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,head:head);
            f.NativeCameraFrame(id,150);f.Rig.Tick(); // real F2 worker and EngineRobot.Update drain
        }
        Frame(300,.1f);
        Assert.True(queryCompleted);
        var first=Assert.IsType<FaceEntry>(world.GetFace(7));
        var input=Assert.Single(vision.LastFaces);var oldPose=first.HeadPose;
        var oldTranslation=oldPose.Translation;var oldRotation=oldPose.Quaternion;
        var oldNativeTranslation=first.NativeTrackingTranslation;
        var oldNativeRotation=first.NativeTrackingRotation;
        Assert.False(input.HeadPose.HasParent);
        using var firstRoot=Pose3d.WrapExisting(f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node!);
        using var cached=new Pose3d();
        Assert.True(world.CopyNativeLastFace(firstRoot,true,cached));
        Assert.Equal(oldNativeTranslation,cached.Translation);
        Assert.Equal(oldNativeRotation,cached.Quaternion);
        Assert.Same(firstRoot.Node,cached.Node!.Parent);

        f.Robot.Delocalize(false);detector.Parts=false;
        Frame(301,.3f);
        var updated=Assert.IsType<FaceEntry>(world.GetFace(7));
        var second=Assert.Single(vision.LastFaces);
        Assert.Same(oldPose,observed!.PreviousPose);
        Assert.Equal(oldTranslation,oldPose.Translation);Assert.Equal(oldRotation,oldPose.Quaternion);
        Assert.Equal(oldTranslation,updated.HeadPose.Translation);
        Assert.Equal(oldNativeTranslation,updated.NativeTrackingTranslation);
        Assert.Equal(second.NativeTrackingRotation,updated.NativeTrackingRotation);
        Assert.NotEqual(oldNativeRotation,updated.NativeTrackingRotation);
        Assert.NotEqual(oldNativeTranslation,second.NativeTrackingTranslation);
        Assert.False(second.HeadPose.HasParent);
        using var newRoot=Pose3d.WrapExisting(f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node!);
        Assert.NotSame(firstRoot.Node,newRoot.Node);
        Assert.True(world.CopyNativeLastFace(newRoot,true,cached));
        Assert.Equal(oldNativeTranslation,cached.Translation);
        Assert.Equal(second.NativeTrackingRotation,cached.Quaternion);
        Assert.False(world.CopyNativeLastFace(firstRoot,true,cached));
    }

    [Fact]
    public void NativeNoPartsCannotBorrowTranslationFromLegacyEntry()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        var world=f.M.Vision.Faces;
        var legacy=new TrackedFace(new DetectedFace(7,new FaceRect(1,2,10,10),Name:"Ada"),10)
            {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(20,0,50))};
        var previous=world.AddOrUpdateFace(legacy,Pose3d.Identity,false)!.Face;
        var incoming=new TrackedFace(new DetectedFace(7,new FaceRect(1,2,10,10)),11)
        {
            NativeTrackingTranslation=new Vec3(30,0,60),NativeTrackingRotation=Rotation3d.Identity,
            HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(30,0,60))
        };
        var origin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        using var cached=new Pose3d();
        Assert.True(world.CopyNativeLastFace(origin,false,cached));
        Assert.Equal(20,cached.Translation.X);
        Assert.Null(world.AddOrUpdateNativeFace(incoming,Pose3d.Identity,false,origin.Node!,true,requireNativePose:true));
        Assert.Same(previous,world.GetFace(7));Assert.Equal(10u,previous.LastObservedTimestamp);
        Assert.Equal(new Vec3(20,0,50),previous.HeadPose.Translation);
        Assert.True(world.CopyNativeLastFace(origin,false,cached));Assert.Equal(20,cached.Translation.X);
    }

    [Fact]
    public void RetiredCapturedOwnerCannotCommitFaceOrCache()
    {
        using var f=new Fixture(sourceNvEmpty:true);
        var world=f.M.Vision.Faces;var owner=f.Robot;int generation=world.NativeGeneration!();
        var origin=owner.GetNativeOrigin(owner.CurrentOriginId).Node!;
        var face=new TrackedFace(new DetectedFace(7,new FaceRect(1,2,10,10),Name:"Ada"),11)
        {
            NativeTrackingTranslation=new Vec3(30,0,60),NativeTrackingRotation=Rotation3d.Identity,
            HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(30,0,60))
        };
        f.Rig.Engine.Robots.RemoveRobot(1,false);f.Rig.Engine.Robots.AddRobot(1,withRic:false);
        Assert.Null(world.AddOrUpdateNativeFace(face,Pose3d.Identity,false,origin,true,requireNativePose:true,expectedOwner:owner,expectedGeneration:generation));
        Assert.Null(world.GetFace(7));
        using var cached=new Pose3d();
        Assert.False(world.CopyNativeLastFace(f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId),true,cached));
    }
}
