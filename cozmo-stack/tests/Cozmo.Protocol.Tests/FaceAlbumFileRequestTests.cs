using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class FaceAlbumFileRequestTests
{
    private static void DrainStartup(M4ControlTests.Rig rig)
    {
        rig.ToSynced();rig.State();var nv=rig.Engine.NvStorage!;
        for(int i=0;i<150 && !nv.IsIdle;i++)
        {
            rig.Tick();if(nv.InFlightTag is not {} tag)continue;
            rig.Data(new NVOpResult{Tag=tag,Op=0,Result=-1});rig.Tick();
        }
        Assert.True(nv.IsIdle);
    }

    private static string MakeAlbum(string directory,bool named)
    {
        Directory.CreateDirectory(directory);
        if(named)FaceAlbumFileStartupTests.SmallGuestAlbum(directory);else FaceAlbumFileStartupTests.EmptyGuestAlbum(directory);
        File.WriteAllText(Path.Combine(directory,"enrollData.json"),named
            ?"{\"17\":{\"enrollmentTime\":6,\"lastDataUpdateTime\":7,\"name\":\"Small\",\"albumEntries\":[{\"albumEntry\":1,\"lastSeenTime\":8}]}}":"{}");
        return directory;
    }

    [Theory]
    [InlineData("", "config/basestation/faceAlbums")]
    [InlineData("/", "config/basestation/faceAlbums/")]
    [InlineData("//", "config/basestation/faceAlbums/")]
    [InlineData("///", "config/basestation/faceAlbums//")]
    [InlineData("/Foo/", "config/basestation/faceAlbums/Foo")]
    [InlineData("../Foo", "config/basestation/faceAlbums/../Foo")]
    [InlineData("Foo//Bar", "config/basestation/faceAlbums/Foo//Bar")]
    public void ResourcePathAssemblyKeepsNativeText(string name,string expected)
    {
        Assert.Equal("root/"+expected,VisionSystem.ResolveFileAlbumRequest(name,true,"root"));
        Assert.Equal(name,VisionSystem.ResolveFileAlbumRequest(name,false,null));
        Assert.Null(VisionSystem.ResolveFileAlbumRequest(name,true,null));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void QueuedRequestLoadsSavesThenReplaysWithoutWaitingForAck(bool relative)
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();
        string root=relative?Path.Combine(options.ResourcesPath!,"config","basestation","faceAlbums","Foo"):Path.Combine(backup.Root,"album");
        MakeAlbum(root,true);
        using var rig=new M4ControlTests.Rig(engineOptions:options);DrainStartup(rig);rig.Robot.Vision.FaceRecognitionClockForTest=()=>100_000_000;
        var actions=new List<string>();var local=new List<LoadedKnownFaceReport>();string? initiated=null;
        rig.Engine.LogEntry+=entry=>{if(entry.Name=="NVStorageComponent.Write.DataQueued")actions.Add(entry.Text);if(entry.Name=="VisionComponent.SaveFaceAlbumToRobot.Initiated")initiated=entry.Text;};
        using var a=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>actions.Add("58"));
        using var b=rig.Engine.AnimationMessages.SubscribeMessage(0x1C,true,p=>{actions.Add("1c");local.Add(Assert.IsType<LoadedKnownFaceReport>(p));});
        if(relative)rig.Robot.LoadFaceAlbumFromFile("/Foo");else rig.Robot.LoadFaceAlbumFromFile(root,false);
        Assert.Empty(actions);rig.Tick();
        Assert.Equal(4,actions.Count);Assert.Contains("FaceAlbum",actions[0]);Assert.Contains("FaceEnroll",actions[1]);
        Assert.Equal(new[]{"58","1c"},actions.Skip(2));
        var report=Assert.Single(local);Assert.Equal((17,"Small",94L,93L,92L),(report.FaceId,report.Name,report.SecondsSinceEnrolled,report.SecondsSinceUpdated,report.SecondsSinceSeen));
        Assert.Equal("Small",Assert.Single(rig.Robot.Vision.Recognizer.EnrolledFaces).Name);
        var saved=rig.Robot.Vision.Recognizer.ExportStockData();
        int paddedAlbum=(saved.Album.Length+3)&~3,paddedEnrollment=(saved.Enrollment.Length+3)&~3;
        Assert.EndsWith($"Initiated save of {paddedAlbum}-byte album data and {paddedEnrollment}-byte enroll data to NVStorage",initiated);
        Assert.False(rig.Engine.NvStorage!.IsIdle); // neither write was acknowledged before replay
    }

    [Theory]
    [InlineData(false,0x184000u,false)] [InlineData(false,0x183000u,false)]
    [InlineData(true,0x184000u,false)] [InlineData(true,0x183000u,true)]
    public void AdmissionFailureUsesNativeWriteAndEraseResult(bool empty,uint rejectedTag,bool expectReplay)
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();string directory=MakeAlbum(Path.Combine(backup.Root,"album"),!empty);
        using var rig=new M4ControlTests.Rig(engineOptions:options);DrainStartup(rig);var vision=rig.Robot.Vision;int marker=0;var logs=new List<string>();
        rig.Engine.LogEntry+=entry=>logs.Add(entry.Name??"");
        byte expectedOp=empty?NvStorageComponent.OpErase:NvStorageComponent.OpWrite;
        vision.FileSaveRejectForTest=(op,tag)=>op==expectedOp && tag==rejectedTag;
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>marker++);
        rig.Robot.LoadFaceAlbumFromFile(directory,false);rig.Tick();
        Assert.Equal(expectReplay?1:0,marker);
        string failed=empty?(rejectedTag==0x184000?"SendEraseAlbumDataFail":"SendEraseEnrollDataFail"):(rejectedTag==0x184000?"SendWriteAlbumDataFail":"SendWriteEnrollDataFail");
        Assert.Contains("VisionComponent.SaveFaceAlbumToRobot."+failed,logs);
        if(!empty)Assert.True(logs.IndexOf("VisionComponent.SaveFaceAlbumToRobot.Initiated")>logs.IndexOf("VisionComponent.SaveFaceAlbumToRobot."+failed));
        if(!empty)Assert.Equal("Small",Assert.Single(vision.Recognizer.EnrolledFaces).Name); // failed save does not roll back load
    }

    [Fact]
    public void MissingFileDoesNotSaveOrReplay()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();using var rig=new M4ControlTests.Rig(engineOptions:backup.Options());DrainStartup(rig);
        var keys=new List<string>();int marker=0;rig.Engine.LogEntry+=entry=>keys.Add(entry.Name??"");
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>marker++);
        rig.Robot.LoadFaceAlbumFromFile(Path.Combine(backup.Root,"missing"),false);rig.Tick();
        Assert.Equal(0,marker);Assert.Contains("VisionComponent.LoadFaceAlbum.LoadFromFileFailed",keys);
        Assert.DoesNotContain("NVStorageComponent.Write.DataQueued",keys);Assert.DoesNotContain("NVStorageComponent.Erase.Queued",keys);
    }

    [Fact]
    public void MismatchedGuestDoesNotSaveOrReplay()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();string directory=MakeAlbum(Path.Combine(backup.Root,"album"),false);
        File.WriteAllText(Path.Combine(directory,"enrollData.json"),"{\"17\":{\"name\":\"Wrong\",\"albumEntries\":[{\"albumEntry\":1,\"lastSeenTime\":1}]}}");
        using var rig=new M4ControlTests.Rig(engineOptions:options);DrainStartup(rig);var keys=new List<string>();int marker=0;
        rig.Engine.LogEntry+=entry=>keys.Add(entry.Name??"");using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>marker++);
        rig.Robot.LoadFaceAlbumFromFile(directory,false);rig.Tick();
        Assert.Equal(0,marker);Assert.Empty(rig.Robot.Vision.Recognizer.EnrolledFaces);
        Assert.Contains("VisionComponent.LoadFaceAlbum.LoadFromFileFailed",keys);
        Assert.DoesNotContain("NVStorageComponent.Write.DataQueued",keys);Assert.DoesNotContain("NVStorageComponent.Erase.Queued",keys);
    }

    [Fact]
    public void QueuedRequestCannotMoveToReplacementOwner()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();string directory=MakeAlbum(Path.Combine(backup.Root,"album"),true);
        using var rig=new M4ControlTests.Rig(engineOptions:options);DrainStartup(rig);var original=rig.Engine.Robot!;int markers=0;
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>markers++);
        rig.Robot.LoadFaceAlbumFromFile(directory,false);
        rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
        var replacement=rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        rig.Tick();Assert.NotSame(original,replacement);Assert.Same(replacement,rig.Engine.Robot);
        Assert.Equal(0,markers);Assert.Empty(rig.Robot.Vision.Recognizer.EnrolledFaces);
    }

    [Fact]
    public void QueuedRequestCannotMoveToLaterSameOwnerSession()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        bool oldError=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            using var backup=new BackupTestResources();var options=backup.Options();string directory=MakeAlbum(Path.Combine(backup.Root,"album"),true);
            using var rig=new M4ControlTests.Rig(engineOptions:options);DrainStartup(rig);var owner=rig.Engine.Robot!;int markers=0;
            using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>markers++);
            rig.Robot.LoadFaceAlbumFromFile(directory,false);
            rig.Robot.Vision.InitializeNativeVision(owner,new FirmwareJsonValue{Kind=FirmwareJsonKind.Object});
            rig.Tick();Assert.Same(owner,rig.Engine.Robot);Assert.Equal(0,markers);
            Assert.Empty(rig.Robot.Vision.Recognizer.EnrolledFaces);
        }
        finally {Cozmo.Transport.EngineErrorState.ErrorFlagSet=oldError;}
    }

    [Fact]
    public void NoExternalInterfaceWithholdsTheQueuedRequest()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();string directory=MakeAlbum(Path.Combine(backup.Root,"album"),true);
        using var rig=new M4ControlTests.Rig(engineOptions:options);DrainStartup(rig);rig.Engine.Robot!.HasExternalInterface=false;int markers=0;
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>markers++);
        rig.Robot.LoadFaceAlbumFromFile(directory,false);rig.Tick();
        Assert.Equal(0,markers);Assert.Empty(rig.Robot.Vision.Recognizer.EnrolledFaces);Assert.True(rig.Engine.NvStorage!.IsIdle);
    }

    [Fact]
    public void EmptyAbsoluteRequestUsesNativeRootFileName()
    {
        if(ShippedLibraries.FindLibDir() is null || File.Exists("/data.bin"))return;
        using var backup=new BackupTestResources();using var rig=new M4ControlTests.Rig(engineOptions:backup.Options());DrainStartup(rig);
        var logs=new List<EngineLogEntry>();rig.Engine.LogEntry+=logs.Add;
        rig.Robot.LoadFaceAlbumFromFile("",false);rig.Tick();
        Assert.Contains(logs,entry=>entry.Name=="FaceRecognizer.LoadAlbum.FileOpenFail" && entry.Text.EndsWith("Filename: /data.bin",StringComparison.Ordinal));
    }

    [Fact]
    public void ReplacementDuringSaveAdmissionCannotReplayOntoSuccessor()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();string directory=MakeAlbum(Path.Combine(backup.Root,"album"),true);
        using var rig=new M4ControlTests.Rig(engineOptions:options);DrainStartup(rig);var old=rig.Engine.Robot!;EngineRobot? replacement=null;int marker=0;
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>marker++);
        rig.Engine.LogEntry+=entry=>
        {
            if(replacement is not null || entry.Name!="NVStorageComponent.Write.DataQueued")return;
            rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
            replacement=rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        };
        rig.Robot.LoadFaceAlbumFromFile(directory,false);rig.Tick();
        Assert.NotNull(replacement);Assert.NotSame(old,replacement);Assert.Same(replacement,rig.Engine.Robot);
        Assert.Equal(0,marker);Assert.Empty(rig.Robot.Vision.Recognizer.EnrolledFaces);
    }
}
