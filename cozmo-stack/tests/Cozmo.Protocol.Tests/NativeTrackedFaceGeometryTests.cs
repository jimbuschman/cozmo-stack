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
            // A deadlock never completes; 15 s only allows for thread-pool start delay under the full suite's load.
            queryCompleted=Task.Run(()=>world.GetFaceIDsObservedSince(0)).Wait(TimeSpan.FromSeconds(15));
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
        Assert.Equal(input.NativeTrackingTranslation, input.HeadPose.Translation);
        Assert.Equal(input.NativeTrackingRotation, input.HeadPose.Quaternion);
        using var firstRoot=Pose3d.WrapExisting(f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node!);
        Assert.Same(firstRoot.Node, oldPose.Node!.Parent);
        Assert.Equal(oldNativeTranslation, oldPose.Translation);
        Assert.Equal(oldNativeRotation, oldPose.Quaternion);
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
        Assert.Equal(second.NativeTrackingTranslation, second.HeadPose.Translation);
        Assert.Equal(second.NativeTrackingRotation, second.HeadPose.Quaternion);
        using var newRoot=Pose3d.WrapExisting(f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId).Node!);
        Assert.NotSame(firstRoot.Node,newRoot.Node);
        Assert.Same(newRoot.Node, updated.HeadPose.Node!.Parent);
        Assert.Same(firstRoot.Node, oldPose.Node!.Parent);
        Assert.True(world.CopyNativeLastFace(newRoot,true,cached));
        Assert.Equal(oldNativeTranslation,cached.Translation);
        Assert.Equal(second.NativeTrackingRotation,cached.Quaternion);
        Assert.False(world.CopyNativeLastFace(firstRoot,true,cached));
        Assert.True(world.ChangeFaceID(7,8));
        world.ResetToConstructed();
        Assert.Equal(oldTranslation, oldPose.Translation); Assert.Equal(oldRotation, oldPose.Quaternion);
        Assert.Same(firstRoot.Node, oldPose.Node!.Parent);
        Assert.False(world.CopyNativeLastFace(newRoot,false,cached));
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
        Assert.False(world.CopyNativeLastFace(origin,false,cached));
        Assert.Same(previous,world.GetLastObservedFace());
        Assert.Null(world.AddOrUpdateNativeFace(incoming,Pose3d.Identity,false,origin.Node!,true,requireNativePose:true));
        Assert.Same(previous,world.GetFace(7));Assert.Equal(10u,previous.LastObservedTimestamp);
        Assert.Equal(new Vec3(20,0,50),previous.HeadPose.Translation);
        Assert.False(world.CopyNativeLastFace(origin,false,cached));
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

    [Fact]
    public void LegacyWritesNeverSeedOrDisplaceNativeCacheAndClearEntryNativeProvenance()
    {
        using var f = new Fixture(sourceNvEmpty:true);
        var world = f.M.Vision.Faces; var owner = f.Robot; var origin = owner.GetNativeOrigin(owner.CurrentOriginId);
        using var cached = new Pose3d();
        TrackedFace Legacy(int id, uint stamp, float x) => new(new DetectedFace(id, new FaceRect(1,2,10,10), Name:"Legacy"), stamp)
        { HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(x,0,60)) };
        world.AddOrUpdateFace(Legacy(7,10,2), Pose3d.Identity,false);
        Assert.False(world.CopyNativeLastFace(origin,true,cached));
        Assert.False(world.CopyNativeLastFace(origin,false,cached));
        var native = new TrackedFace(new DetectedFace(8,new FaceRect(1,2,10,10),new Vec2(2,2),new Vec2(3,2),Name:"Native"),10)
        { HeadPose = new Pose3d(Rotation3d.Identity,new Vec3(100,0,60)), NativeTrackingTranslation = new Vec3(100,0,60), NativeTrackingRotation = Rotation3d.Identity };
        Assert.NotNull(world.AddOrUpdateNativeFace(native,Pose3d.Identity,false,origin.Node!,true,requireNativePose:true,expectedOwner:owner));
        Assert.True(world.CopyNativeLastFace(origin,true,cached)); Assert.Equal(100,cached.Translation.X);
        world.AddOrUpdateFace(Legacy(7,10,1),Pose3d.Identity,false); // same stamp, closer
        world.AddOrUpdateFace(Legacy(8,20,3),Pose3d.Identity,false); // same ID, later stamp
        Assert.True(world.CopyNativeLastFace(origin,true,cached)); Assert.Equal(100,cached.Translation.X);
        Assert.Null(world.GetFace(8)!.NativeTrackingTranslation);
        var noParts = new TrackedFace(new DetectedFace(8,new FaceRect(1,2,10,10),Name:"Native"),21)
        { HeadPose = new Pose3d(Rotation3d.Identity,new Vec3(200,0,60)), NativeTrackingTranslation = new Vec3(200,0,60), NativeTrackingRotation = Rotation3d.Identity };
        Assert.Null(world.AddOrUpdateNativeFace(noParts,Pose3d.Identity,false,origin.Node!,true,requireNativePose:true,expectedOwner:owner));
        world.RemoveFaceByID(8); Assert.True(world.CopyNativeLastFace(origin,true,cached));
        world.ClearAllFaces(); Assert.False(world.CopyNativeLastFace(origin,true,cached));
    }

    [Fact]
    public void FirstNativeObservationDoesNotInheritSameIdLegacyCountOrName()
    {
        using var f = new Fixture(sourceNvEmpty:true);
        var world = f.M.Vision.Faces; var owner = f.Robot; var origin = owner.GetNativeOrigin(owner.CurrentOriginId);
        for (uint i = 1; i <= 4; i++)
        {
            var legacy = new TrackedFace(new DetectedFace(7,new FaceRect(1,2,10,10),Name:i==1?"Host":null),i)
            { HeadPose = new Pose3d(Rotation3d.Identity,new Vec3(20,0,60)) };
            world.AddOrUpdateFace(legacy,Pose3d.Identity,false);
        }
        var previous = world.GetFace(7)!.HeadPose;
        using var cached = new Pose3d(); Assert.False(world.CopyNativeLastFace(origin,false,cached));
        var rejected = new TrackedFace(new DetectedFace(7,new FaceRect(1,2,10,10),new Vec2(2,2),new Vec2(3,2)),100)
        { HeadPose = new Pose3d(Rotation3d.Identity,new Vec3(100,0,60)), NativeTrackingTranslation = new Vec3(100,0,60), NativeTrackingRotation = Rotation3d.Identity };
        Assert.Null(world.AddOrUpdateNativeFace(rejected,Pose3d.Identity,true,origin.Node!,true,requireNativePose:true,expectedOwner:owner));
        Assert.Equal(4,world.GetFace(7)!.TimesObserved); Assert.Same(previous,world.GetFace(7)!.HeadPose);
        for (uint i = 1; i <= 4; i++)
        {
            var native = new TrackedFace(new DetectedFace(7,new FaceRect(1,2,10,10),new Vec2(2,2),new Vec2(3,2)),100+i)
            { HeadPose = new Pose3d(Rotation3d.Identity,new Vec3(100,0,60)), NativeTrackingTranslation = new Vec3(100,0,60), NativeTrackingRotation = Rotation3d.Identity };
            var observation = world.AddOrUpdateNativeFace(native,Pose3d.Identity,false,origin.Node!,true,requireNativePose:true,expectedOwner:owner)!;
            Assert.Equal((int)i, observation.NativeSnapshot!.Count);
            Assert.False(observation.Face.HasName);
            if (i == 1) Assert.Same(previous,observation.PreviousPose);
            Assert.Equal(i == 4, world.CopyNativeLastFace(origin,true,cached));
        }
    }
}
