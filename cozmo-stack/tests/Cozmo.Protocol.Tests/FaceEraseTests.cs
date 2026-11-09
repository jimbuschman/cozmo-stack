using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class FaceEraseTests
{
    private static byte[] Enrollment()
    {
        var bytes=new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(StoredEnrolledFace.VersionPrefix));
        bytes.AddRange(BitConverter.GetBytes(9));
        var erased=new StoredEnrolledFace {FaceID=7,Name="Drawn",Timestamp28Seconds=1,Timestamp30Seconds=1};
        erased.AlbumEntryIds.Add(3);erased.AlbumEntrySeconds.Add(1);erased.Pack(bytes);
        new StoredEnrolledFace {FaceID=8,Name="Survivor",Timestamp28Seconds=1,Timestamp30Seconds=1}.Pack(bytes);
        return bytes.ToArray();
    }

    private static PickupPyramidNativeExecutionTests.Fixture Fixture() => new(sourceNvEmpty:true,
        sourceNvStored: tag=>tag switch {VisionSystem.FaceAlbumNvTag=>FaceIdentityTests.Album(),
            VisionSystem.FaceEnrollmentNvTag=>Enrollment(),_=>null});

    [Fact]
    public void PublicEraseReportsBeforeNvRepliesAndRestoresRemainingIdentity()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var f=Fixture();
        var vision=f.M.Vision;
        var origin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        var observed=new TrackedFace(new DetectedFace(7,new FaceRect(0,0,20,20),Name:"Drawn"),10)
        { HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(100,0,50)) };
        Assert.NotNull(vision.Faces.AddOrUpdateNativeFace(observed,Pose3d.Identity,false,origin.Node!));
        var local=new List<NativeErasedEnrolledFace>();
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x57,true,value=>local.Add(Assert.IsType<NativeErasedEnrolledFace>(value)));
        int unrelated=0;
        vision.EnrolledFacesErasedInternal+=()=>unrelated++;
        using var enrollmentReport=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x56,true,_=>unrelated++);
        var app=new List<string>();var bodies=new List<byte[]>();
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x57){app.Add("raw");bodies.Add(body.ToArray());}};
        f.Rig.Engine.FaceErased+=report=>{app.Add("typed");Assert.Equal(new FaceErasedReport(7,""),report);};
        int mark=f.Rig.Mark(),seen=0;
        f.Rig.Robot.EraseEnrolledFaceById(7);f.Rig.Tick();
        Assert.Equal(new NativeErasedEnrolledFace(7,""),Assert.Single(local));
        Assert.False(f.Rig.Engine.NvStorage!.IsIdle);
        Assert.Equal("Survivor",Assert.Single(vision.Recognizer.EnrolledFaces).Name);
        Assert.Equal("Drawn",vision.Faces.GetFace(7)!.Name);
        Assert.Equal(0,unrelated);
        f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(new[]{"raw","typed"},app);
        Assert.Equal(new byte[]{7,0,0,0,0},Assert.Single(bodies));
        var exported=vision.GetSerializedFaceData();
        var commands=new List<NVCommand>();
        for(int i=0;i<100 && !commands.Any(c=>c.Tag==VisionSystem.FaceEnrollmentNvTag && c.Op==NvStorageComponent.OpWrite);i++)
        {
            var sent=f.Rig.SentSince(mark);
            for(;seen<sent.Count;seen++)if(sent[seen] is NVCommand command)
            {commands.Add(command);f.Rig.Data(new NVOpResult{Tag=command.Tag,Op=command.Op,Result=0});}
            f.Rig.Tick();
        }
        var relevant=commands.Where(c=>c.Tag==VisionSystem.FaceAlbumNvTag||c.Tag==VisionSystem.FaceEnrollmentNvTag).ToArray();
        Assert.Equal(new[]{(VisionSystem.FaceAlbumNvTag,NvStorageComponent.OpErase),(VisionSystem.FaceAlbumNvTag,NvStorageComponent.OpWrite),
            (VisionSystem.FaceEnrollmentNvTag,NvStorageComponent.OpErase),(VisionSystem.FaceEnrollmentNvTag,NvStorageComponent.OpWrite)},relevant.Select(c=>(c.Tag,c.Op)));
        var album=relevant[1].Data.Skip(16).Take(exported.Album.Length).ToArray();
        var enrollment=relevant[3].Data.Skip(16).Take(exported.Enrollment.Length).ToArray();
        Assert.Equal(exported.Album,album);Assert.Equal(exported.Enrollment,enrollment);
        using var restored=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true,sourceNvStored:tag=>tag switch
        {VisionSystem.FaceAlbumNvTag=>album,VisionSystem.FaceEnrollmentNvTag=>enrollment,_=>null});
        var remaining=Assert.Single(restored.M.Vision.Recognizer.EnrolledFaces);
        Assert.Equal(8,remaining.FaceID);Assert.Equal("Survivor",remaining.Name);
    }

    [Fact]
    public void MissingSignedIdWarnsWithoutSavingOrReporting()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var f=Fixture();
        var logs=new List<EngineLogEntry>();f.Rig.Engine.LogEntry+=logs.Add;
        int local=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x57,true,_=>local++);
        int mark=f.Rig.Mark();f.Rig.Robot.EraseEnrolledFaceById(-1);f.Rig.Tick();
        Assert.Contains(logs,e=>e.Name=="FaceRecognizer.EraseFace.NotFound" && e.Text.Contains("-1"));
        Assert.Equal(0,local);Assert.Equal(2,f.M.Vision.Recognizer.EnrolledFaces.Count);
        Assert.Empty(f.Rig.SentSince(mark).OfType<NVCommand>());
    }

    [Fact]
    public void ExistingUnnamedEnrollmentCanBeErased()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var f=Fixture();
        f.M.Vision.Recognizer.EnrolledFaces.Single(face=>face.FaceID==7).Name="";
        int local=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x57,true,_=>local++);
        f.Rig.Robot.EraseEnrolledFaceById(7);f.Rig.Tick();
        Assert.Equal(1,local);
        Assert.Equal(8,Assert.Single(f.M.Vision.Recognizer.EnrolledFaces).FaceID);
    }

    [Fact]
    public void NoExternalInterfaceStillErasesAndSavesButDoesNotPublish()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var f=Fixture();f.Robot.HasExternalInterface=false;
        int local=0,host=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x57,true,_=>local++);
        f.Rig.Engine.EngineToGameMessage+=(tag,_)=>{if(tag==0x57)host++;};
        f.Rig.Engine.FaceErased+=_=>host++;
        f.Rig.Robot.EraseEnrolledFaceById(7);f.Rig.Tick();f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(8,Assert.Single(f.M.Vision.Recognizer.EnrolledFaces).FaceID);
        Assert.False(f.Rig.Engine.NvStorage!.IsIdle);Assert.Equal(0,local);Assert.Equal(0,host);
    }

    [Fact]
    public void NvFailureDoesNotWithdrawOrDuplicateEraseReport()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var f=Fixture();
        int reports=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x57,true,_=>reports++);
        int mark=f.Rig.Mark();
        f.Rig.Robot.EraseEnrolledFaceById(7);f.Rig.Tick();
        Assert.Equal(1,reports);
        var command=Assert.Single(f.Rig.SentSince(mark).OfType<NVCommand>());
        f.Rig.Data(new NVOpResult {Tag=command.Tag,Op=command.Op,Result=-1});
        for(int i=0;i<5;i++)f.Rig.Tick();
        Assert.Equal(1,reports);
        Assert.Equal(8,Assert.Single(f.M.Vision.Recognizer.EnrolledFaces).FaceID);
    }

    [Fact]
    public void QueuedEraseCannotReachReplacementOwner()
    {
        using var f=Fixture();
        int reports=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x57,true,_=>reports++);
        using(f.Rig.Engine.EnterSynchronousOwner())
        {
            f.Rig.Robot.EraseEnrolledFaceById(7);
            f.Rig.Engine.Robots.RemoveRobot(1,false);f.Rig.Engine.Robots.AddRobot(1,withRic:false);
        }
        f.Rig.Tick();Assert.Equal(0,reports);
    }
}
