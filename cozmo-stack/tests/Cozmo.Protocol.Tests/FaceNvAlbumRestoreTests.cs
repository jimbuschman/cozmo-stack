using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class FaceNvAlbumRestoreTests
{
    private static byte[] Header(byte[] payload)
    {
        var bytes=new byte[16+payload.Length];
        BitConverter.GetBytes(NvStorageComponent.NonFactoryHeaderMagic).CopyTo(bytes,0);
        BitConverter.GetBytes(payload.Length).CopyTo(bytes,8);payload.CopyTo(bytes,16);return bytes;
    }
    private static byte[] Enrollment(int next,params StoredEnrolledFace[] faces)
    {
        var bytes=new List<byte>();bytes.AddRange(BitConverter.GetBytes(StoredEnrolledFace.VersionPrefix));
        bytes.AddRange(BitConverter.GetBytes(next));foreach(var face in faces)face.Pack(bytes);return bytes.ToArray();
    }
    private static void CompleteReads(M4ControlTests.Rig rig,byte[] album,byte[] enrollment,bool enrollmentFails=false)
    {
        rig.State();bool done=false;int steps=0;
        while(!done && steps++<80)
        {
            rig.Tick();if(rig.Engine.NvStorage!.InFlightTag is not {} tag)continue;
            byte[]? payload=tag==0x184000?album:tag==0x183000?enrollment:null;
            sbyte result=payload is null || tag==0x183000 && enrollmentFails?(sbyte)-1:(sbyte)0;
            rig.Data(new NVOpResult{Tag=tag,Op=0,Result=result,Length=0,Data=result==0?Header(payload!):Array.Empty<byte>()});
            rig.Tick();done=tag==0x183000;
        }
        Assert.True(done);
    }

    [Theory]
    [InlineData(220,-120)] [InlineData(-8,100)]
    public void SmallerRealGuestRestoresThroughNvAndPublishesCapturedReports(int slotSeconds,int expectedSeenAge)
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var resources=new BackupTestResources();string directory=Path.Combine(resources.Root,"guest");Directory.CreateDirectory(directory);
        FaceAlbumFileStartupTests.SmallGuestAlbum(directory);byte[] album=File.ReadAllBytes(Path.Combine(directory,"data.bin"));
        var unnamed=new StoredEnrolledFace{FaceID=2,Name="",Timestamp28Seconds=200,Timestamp30Seconds=150};
        var old=new StoredEnrolledFace{FaceID=17,Name="old"};
        var last=new StoredEnrolledFace{FaceID=17,Name="\u00e9",Timestamp28Seconds=6,Timestamp30Seconds=7};
        last.AlbumEntryIds.Add(1);last.AlbumEntrySeconds.Add(slotSeconds);
        byte[] metadata=Enrollment(321,old,unnamed,last);
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());rig.Robot.Vision.FaceRecognitionClockForTest=()=>100_000_000;
        var local=new List<string>();var reports=new List<LoadedKnownFaceReport>();var raw=new List<(ushort Tag,byte[] Body)>();
        using var a=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>local.Add("58"));
        using var b=rig.Engine.AnimationMessages.SubscribeMessage(0x1C,true,p=>{var report=Assert.IsType<LoadedKnownFaceReport>(p);reports.Add(report);local.Add("1c:"+report.FaceId);});
        rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag is 0x58 or 0x1C)raw.Add((tag,body.ToArray()));};
        rig.ToSynced();CompleteReads(rig,album,metadata);rig.Tick();
        Assert.Equal(new[]{"58","1c:2","1c:17"},local);
        Assert.Equal((0L,0L,100L,""),(reports[0].SecondsSinceEnrolled,reports[0].SecondsSinceUpdated,reports[0].SecondsSinceSeen,reports[0].Name));
        Assert.Equal((94L,93L,(long)expectedSeenAge,"\u00e9"),(reports[1].SecondsSinceEnrolled,reports[1].SecondsSinceUpdated,reports[1].SecondsSinceSeen,reports[1].Name));
        Assert.Equal(new ushort[]{0x58,0x1C,0x1C},raw.Select(item=>item.Tag));
        Assert.Equal((byte)1,raw[2].Body[28]);Assert.Equal((byte)0xE9,raw[2].Body[29]);
        var faces=rig.Robot.Vision.Recognizer.EnrolledFaces.OrderBy(face=>face.FaceID).ToArray();
        Assert.Equal(new[]{2,17},faces.Select(face=>face.FaceID));Assert.Equal("\u00e9",faces[1].Name);
        var exported=rig.Robot.Vision.Recognizer.ExportStockData();
        Assert.NotEmpty(exported.Album);Assert.Equal(321,BitConverter.ToInt32(exported.Enrollment,4));
    }

    [Fact]
    public void SmallerNvGuestRecognizesTheSourceFaceOnLaterCameraFrames()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var resources=new BackupTestResources();string directory=Path.Combine(resources.Root,"guest");Directory.CreateDirectory(directory);
        FaceAlbumFileStartupTests.SmallGuestAlbum(directory);byte[] album=File.ReadAllBytes(Path.Combine(directory,"data.bin"));
        var face=new StoredEnrolledFace{FaceID=17,Name="Small"};face.AlbumEntryIds.Add(1);face.AlbumEntrySeconds.Add(8);
        byte[] metadata=Enrollment(321,face);
        using var fixture=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true,sourceNvStored:tag=>tag switch
        {VisionSystem.FaceAlbumNvTag=>album,VisionSystem.FaceEnrollmentNvTag=>metadata,_=>null});
        var vision=fixture.M.Vision;Assert.Contains(vision.Recognizer.EnrolledFaces,value=>value.FaceID==17);
        vision.Enabled=true;vision.ModeEnableMask=1<<2;vision.Schedules=AllVisionModesSchedule.InitDefaultSchedules();
        fixture.Rig.Robot.SetVisionWhileMoving(true);fixture.Rig.Tick();
        using var doc=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","okao","okao_oracle.json")));
        var root=doc.RootElement;var images=root.GetProperty("images").EnumerateObject().ToDictionary(p=>p.Name,p=>Convert.FromBase64String(p.Value.GetString()!));
        uint id=400;
        foreach(var frame in root.GetProperty("frames").EnumerateArray())
        {
            string name=frame.GetProperty("name").GetString()!;
            fixture.Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos);
            fixture.NativeCameraFrame(id++,images[frame.GetProperty("image").GetString()!]);
            fixture.Rig.Engine.VisionUpdateAllResultsFailed!();fixture.Rig.Tick();
            if(name=="face_s10_2")
                Assert.True(SpinWait.SpinUntil(()=>((OkaoFaceDetector)vision.FaceDetector).RecognitionWorkCompleted,TimeSpan.FromSeconds(10)));
            if(name=="face_s10_3")break;
        }
        var recognized=Assert.Single(vision.LastFaces);Assert.Equal(17,recognized.Id);Assert.Equal("Small",recognized.Name);
    }

    [Theory]
    [InlineData(7,false)] [InlineData(8,true)] [InlineData(9,true)] [InlineData(10,true)] [InlineData(11,true)] [InlineData(12,false)] [InlineData(13,false)]
    public void EmptyGuestRequiresNativeEnrollmentHeader(int length,bool success)
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var resources=new BackupTestResources();string directory=Path.Combine(resources.Root,"guest");Directory.CreateDirectory(directory);
        FaceAlbumFileStartupTests.EmptyGuestAlbum(directory);byte[] album=File.ReadAllBytes(Path.Combine(directory,"data.bin"));
        var metadata=new byte[length];if(length>=4)BitConverter.GetBytes(StoredEnrolledFace.VersionPrefix).CopyTo(metadata,0);
        if(length>=8)BitConverter.GetBytes(500).CopyTo(metadata,4);
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());int markers=0;
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>markers++);
        rig.ToSynced();CompleteReads(rig,album,metadata);
        Assert.Equal(success?1:0,markers);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void MissingEnrollmentOrWrongGuestKeyNeverReplays(bool enrollmentFails)
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var resources=new BackupTestResources();string directory=Path.Combine(resources.Root,"guest");Directory.CreateDirectory(directory);
        FaceAlbumFileStartupTests.SmallGuestAlbum(directory);byte[] album=File.ReadAllBytes(Path.Combine(directory,"data.bin"));
        var face=new StoredEnrolledFace{FaceID=17,Name="wrong"};face.AlbumEntryIds.Add(2);face.AlbumEntrySeconds.Add(1);
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());int reports=0;
        using var a=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>reports++);
        using var b=rig.Engine.AnimationMessages.SubscribeMessage(0x1C,true,_=>reports++);
        rig.ToSynced();CompleteReads(rig,album,Enrollment(321,face),enrollmentFails);
        Assert.Equal(0,reports);Assert.Empty(rig.Robot.Vision.Recognizer.EnrolledFaces);
    }

    [Fact]
    public void EntryClippingSamplesEachClockBeforeOneReportClock()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var resources=new BackupTestResources();string directory=Path.Combine(resources.Root,"guest");Directory.CreateDirectory(directory);
        FaceAlbumFileStartupTests.EmptyGuestAlbum(directory);byte[] album=File.ReadAllBytes(Path.Combine(directory,"data.bin"));
        var first=new StoredEnrolledFace{FaceID=10,Timestamp28Seconds=200,Timestamp30Seconds=150};
        var second=new StoredEnrolledFace{FaceID=2,Timestamp28Seconds=200,Timestamp30Seconds=150};
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());var reports=new List<LoadedKnownFaceReport>();
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x1C,true,p=>reports.Add(Assert.IsType<LoadedKnownFaceReport>(p)));
        rig.ToSynced();var clock=new Queue<long>(new[]{80_000_000L,90_000_000L,100_000_000L});
        rig.Robot.Vision.FaceRecognitionClockForTest=()=>clock.Dequeue();
        CompleteReads(rig,album,Enrollment(123,first,second));
        Assert.Equal(new[]{2,10},reports.Select(report=>report.FaceId));
        Assert.Equal(10,reports[0].SecondsSinceEnrolled);Assert.Equal(20,reports[1].SecondsSinceEnrolled);Assert.Empty(clock);
    }

    [Fact]
    public void CompatibilityCallbackRunsAfterNativeReplayEvenWhenItReinitializes()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        bool oldError=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            using var resources=new BackupTestResources();string directory=Path.Combine(resources.Root,"guest");Directory.CreateDirectory(directory);
            FaceAlbumFileStartupTests.EmptyGuestAlbum(directory);byte[] album=File.ReadAllBytes(Path.Combine(directory,"data.bin"));
            using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());var order=new List<string>();
            using var a=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>order.Add("58"));
            using var b=rig.Engine.AnimationMessages.SubscribeMessage(0x1C,true,_=>order.Add("1c"));
            rig.ToSynced();var owner=rig.Engine.Robot!;
            rig.Robot.Vision.Recognizer.FacesLoaded+=_=>{order.Add("compat");rig.Robot.Vision.InitializeNativeVision(owner,new FirmwareJsonValue{Kind=FirmwareJsonKind.Object});};
            CompleteReads(rig,album,Enrollment(321,new StoredEnrolledFace{FaceID=7,Name=""}));
            Assert.Equal(new[]{"58","1c","compat"},order);
        }
        finally {Cozmo.Transport.EngineErrorState.ErrorFlagSet=oldError;}
    }

    [Fact]
    public void ReplacementDuringFirstReportDoesNotReceiveOldFaceReports()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var resources=new BackupTestResources();string directory=Path.Combine(resources.Root,"guest");Directory.CreateDirectory(directory);
        FaceAlbumFileStartupTests.EmptyGuestAlbum(directory);byte[] album=File.ReadAllBytes(Path.Combine(directory,"data.bin"));
        using var rig=new M4ControlTests.Rig(engineOptions:resources.Options());int markers=0,faces=0;EngineRobot? replacement=null;
        using var a=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>
        {
            markers++;rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
            replacement=rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        });
        using var b=rig.Engine.AnimationMessages.SubscribeMessage(0x1C,true,_=>faces++);
        rig.ToSynced();var original=rig.Engine.Robot!;
        CompleteReads(rig,album,Enrollment(321,new StoredEnrolledFace{FaceID=7,Name=""}));
        Assert.Equal(1,markers);Assert.Equal(0,faces);Assert.NotNull(replacement);Assert.NotSame(original,replacement);
        Assert.Same(replacement,rig.Engine.Robot);Assert.Empty(rig.Robot.Vision.Recognizer.EnrolledFaces);
    }
}
