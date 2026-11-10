using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class FaceIdReportsTests
{
    private sealed class EmptyDetector : IFaceDetector
    {
        public bool IsAvailable=>true;
        public string Description=>"Empty supplied face detection";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image,uint timestamp)=>Array.Empty<DetectedFace>();
    }
    private static FaceEntry Face(Fixture f,int id,uint timestamp,string? name=null)
    {
        var face=new TrackedFace(new DetectedFace(id,new FaceRect(0,0,10,10),Name:name),timestamp)
        { HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(100,0,60),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)) };
        return f.M.Vision.Faces.AddOrUpdateFace(face,f.Robot.GetCheckedMainPose(),false)!.Face;
    }

    [Fact]
    public void ChangedAndDeletedReportsHaveNativeBodiesAndPreEraseOrder()
    {
        using var f=new Fixture();var world=f.M.Vision.Faces;Face(f,-3,100);
        var raw=new List<(ushort Tag,byte[] Body)>();var typed=new List<string>();var local=new List<string>();
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag is 0x47 or 0x4B)raw.Add((tag,body.ToArray()));};
        f.Rig.Engine.RobotChangedObservedFaceId+=x=>typed.Add($"change:{x.OldId}:{x.NewId}");
        f.Rig.Engine.RobotDeletedFace+=x=>typed.Add($"delete:{x.FaceId}");
        using var c=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x47,true,p=>
        {var x=Assert.IsType<NativeChangedFace>(p);Assert.NotNull(world.GetFace(x.NewId));local.Add("change");});
        using var d=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4B,true,p=>
        {var x=Assert.IsType<NativeDeletedFace>(p);Assert.NotNull(world.GetFace(x.Id));local.Add("delete");});
        Assert.True(world.ChangeFaceID(-3,7));world.RemoveFaceByID(7);
        Assert.Equal(new[]{"change","delete"},local);Assert.Empty(raw);Assert.Empty(typed);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(new[]{"change:-3:7","delete:7"},typed);
        Assert.Equal((ushort)0x47,raw[0].Tag);Assert.Equal("FDFFFFFF07000000",Convert.ToHexString(raw[0].Body));
        Assert.Equal((ushort)0x4B,raw[1].Tag);Assert.Equal("07000000",Convert.ToHexString(raw[1].Body));
    }

    [Fact]
    public void ReentrantRemovalDoesNotEraseSameIdReplacement()
    {
        using var f=new Fixture();var world=f.M.Vision.Faces;var original=Face(f,7,100);FaceEntry? replacement=null;int reports=0;
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4B,true,_=>
        {
            reports++;world.RemoveFaceByID(7); // same entry is already being removed
            Assert.True(world.ChangeFaceID(7,8));replacement=Face(f,7,101);
        });
        world.RemoveFaceByID(7);
        Assert.Equal(1,reports);Assert.NotSame(original,replacement);Assert.Same(replacement,world.GetFace(7));
    }

    [Fact]
    public void ClearUsesSignedOrderAndReportsEachBeforeErase()
    {
        using var f=new Fixture();var world=f.M.Vision.Faces;Face(f,4,100);Face(f,-3,100);Face(f,1,100);
        var order=new List<string>();
        var raw=new List<byte[]>();f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x4B)raw.Add(body.ToArray());};
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4B,true,p=>
        {int id=Assert.IsType<NativeDeletedFace>(p).Id;Assert.NotNull(world.GetFace(id));order.Add($"native:{id}");});
        world.FaceDeleted+=id=>{Assert.Empty(world.Faces);order.Add($"public:{id}");};
        world.ClearAllFaces();
        Assert.Equal(new[]{"native:-3","native:1","native:4","public:-3","public:1","public:4"},order);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(new[]{"FDFFFFFF","01000000","04000000"},raw.Select(x=>Convert.ToHexString(x)));
    }

    [Fact]
    public void AbsentCollisionAndSelfChangeReportWithoutDeletion()
    {
        using var f=new Fixture();var world=f.M.Vision.Faces;Face(f,7,100);var target=Face(f,9,100);
        var changes=new List<(int,int)>();int deleted=0;
        using var c=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x47,true,p=>
        {var x=Assert.IsType<NativeChangedFace>(p);changes.Add((x.OldId,x.NewId));});
        using var d=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4B,true,_=>deleted++);
        Assert.False(world.ChangeFaceID(-10,4));
        Assert.True(world.ChangeFaceID(7,9));Assert.Same(target,world.GetFace(9));
        Assert.True(world.ChangeFaceID(9,9));Assert.Null(world.GetFace(9));
        Assert.Equal(new[]{(-10,4),(7,9),(9,9)},changes);Assert.Equal(0,deleted);
    }

    [Fact]
    public void ExternalInterfaceGateSuppressesBothNativeReportPaths()
    {
        using var f=new Fixture();var world=f.M.Vision.Faces;Face(f,-4,100);f.Robot.HasExternalInterface=false;
        int local=0,raw=0,typed=0;
        using var c=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x47,true,_=>local++);
        using var d=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4B,true,_=>local++);
        f.Rig.Engine.EngineToGameMessage+=(tag,_)=>{if(tag is 0x47 or 0x4B)raw++;};
        f.Rig.Engine.RobotDeletedFace+=_=>typed++;f.Rig.Engine.RobotChangedObservedFaceId+=_=>typed++;
        Assert.True(world.ChangeFaceID(-4,2));world.RemoveFaceByID(2);f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(0,local);Assert.Equal(0,raw);Assert.Equal(0,typed);Assert.Empty(world.Faces);
    }

    [Fact]
    public void QueuedCameraExpiryUsesPriorClockUnsignedWrapAndSignedOrder()
    {
        // Real F2/worker result and direct drain; detector output and prior clock are explicit fixture inputs.
        using var f=new Fixture();var world=f.M.Vision.Faces;
        Face(f,-3,1000);Face(f,2,1000);Face(f,-5,uint.MaxValue-15);Face(f,7,1000,"Ada");
        f.M.Vision.FaceDetector=new EmptyDetector();
        var deleted=new List<int>();
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4B,true,p=>deleted.Add(Assert.IsType<NativeDeletedFace>(p).Id));
        f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp=16000;
        uint actual=f.Robot.StoredState!.Timestamp;
        f.Image(600,modes:1<<2);
        Assert.Equal(new[]{-5},deleted);Assert.NotNull(world.GetFace(-3));Assert.NotNull(world.GetFace(2));
        Assert.Equal(actual,f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp);
        f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp=16001;
        f.Image(601,modes:1<<2);
        Assert.Equal(new[]{-5,-3,2},deleted);Assert.NotNull(world.GetFace(7));
    }

    [Fact]
    public void PublicClearCallbacksRunOutsideTheFaceAndFrameGates()
    {
        using var f=new Fixture();var world=f.M.Vision.Faces;Face(f,7,100);
        bool completed=false;
        world.FaceDeleted+=_=>completed=Task.Run(()=>world.GetFaceIDsObservedSince(0)).Wait(TimeSpan.FromSeconds(5));
        world.ClearAllFaces();
        Assert.True(completed);
    }

    [Fact]
    public void OwnerReplacementDuringDeletionStopsTheOldClear()
    {
        using var f=new Fixture();using var execution=f.Rig.Engine.EnterSynchronousOwner();
        var world=f.M.Vision.Faces;Face(f,1,100);Face(f,2,100);int deleted=0;
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4B,true,_=>
        {
            deleted++;
            f.Rig.Engine.Robots.RemoveRobot(1,false);
            f.Rig.Engine.Robots.AddRobot(1,withRic:false);
            Face(f,1,101);
        });
        world.ClearAllFaces();
        Assert.Equal(1,deleted);Assert.NotNull(world.GetFace(1));
    }
}
