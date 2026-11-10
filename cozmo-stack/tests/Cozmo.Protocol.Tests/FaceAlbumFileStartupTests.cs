using System.Text;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class FaceAlbumFileStartupTests
{
    private static void SelectFile(CozmoEngineOptions options,string directory)
    {
        string config=Path.Combine(options.ResourcesPath!,"config","engine","vision_config.json");
        File.WriteAllText(config,File.ReadAllText(config).Replace("\"FaceAlbum\" : \"robot\"","\"FaceAlbum\" : "+JsonSerializer.Serialize(directory)));
    }
    private static void EmptyGuestAlbum(string directory)
    {
        string libraries=ShippedLibraries.FindLibDir() ?? throw new InvalidOperationException("shipped guest unavailable");
        using var guest=new OkaoFaceDetection(libraries,0,true);
        guest.StockVersion();guest.InitializeStockHandles();uint album=guest.FrCreateAlbum();
        try { var data=guest.FrSerializeAlbum(album);Assert.Equal(0,data.Status);File.WriteAllBytes(Path.Combine(directory,"data.bin"),data.Data); }
        finally { Assert.Equal(0,guest.FrCall("OKAO_FR_DeleteAlbumHandle",album)); }
    }

    private static void SmallGuestAlbum(string directory)
    {
        string libraries=ShippedLibraries.FindLibDir()!;
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","okao","okao_oracle.json")));
        var root=doc.RootElement;int width=root.GetProperty("width").GetInt32(),height=root.GetProperty("height").GetInt32();
        var images=root.GetProperty("images").EnumerateObject().ToDictionary(p=>p.Name,p=>Convert.FromBase64String(p.Value.GetString()!));
        using var guest=new OkaoFaceDetection(libraries,0,true);guest.StockVersion();guest.InitializeStockHandles();
        uint primary=guest.FrCreateAlbum();uint small=unchecked((uint)guest.FrCall("OKAO_FR_CreateAlbumHandle",guest.FrCommon,3,2));
        Assert.NotEqual(0u,small);bool registered=false;
        try
        {
            foreach(var frame in root.GetProperty("frames").EnumerateArray())
            {
                var image=images[frame.GetProperty("image").GetString()!];
                guest.DetectTracked(image,width,height,(raw,face,enrollable,excluded)=>
                {
                    if(!registered && face.LeftEye is not null && face.RightEye is not null)
                    {
                        guest.FrQueueCurrentImage();Assert.Equal(0,guest.FrExtractQueued());
                        Assert.Equal(0,guest.FrCall("OKAO_FR_RegisterData",small,guest.FrPendingFeature.Handle,1,0));registered=true;
                    }
                    return face;
                });
                if(registered)break;
            }
            Assert.True(registered);
            var bytes=guest.FrSerializeAlbum(small);Assert.Equal(0,bytes.Status);File.WriteAllBytes(Path.Combine(directory,"data.bin"),bytes.Data);
        }
        finally {Assert.Equal(0,guest.FrCall("OKAO_FR_DeleteAlbumHandle",small));Assert.Equal(0,guest.FrCall("OKAO_FR_DeleteAlbumHandle",primary));}
    }

    [Fact]
    public void EmptyGuestWithMissingMetadataInstallsAndContinues()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();
        string directory=Path.Combine(backup.Root,"empty");Directory.CreateDirectory(directory);EmptyGuestAlbum(directory);SelectFile(options,directory);
        using var rig=new M4ControlTests.Rig(engineOptions:options);var logs=new List<string>();int markers=0;
        rig.Engine.LogEntry+=entry=>logs.Add(entry.Name??"");
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>markers++);
        rig.ToSynced();
        Assert.Equal(2,markers);Assert.Contains("FaceRecognizer.LoadAlbum.EnrollDataFileReadFail",logs);
        Assert.DoesNotContain("VisionComponent.Init.LoadFaceAlbumFromFileFailed",logs);
        Assert.True(rig.Robot.Vision.NativeInitializationForTest!.ComponentInitialized);
        Assert.Empty(rig.Robot.Vision.Recognizer.EnrolledFaces);
    }

    [Fact]
    public void SameOwnerReinitializationFromFirstEraseStopsOldFileTail()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        bool oldError=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
        using var backup=new BackupTestResources();var options=backup.Options();SelectFile(options,Path.Combine(backup.Root,"missing"));
        using var rig=new M4ControlTests.Rig(engineOptions:options);int markers=0;
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>
        {
            markers++;var vision=rig.Robot.Vision;
            vision.InitializeNativeVision(vision.NativeInitializationForTest!.Owner,new FirmwareJsonValue{Kind=FirmwareJsonKind.Object});
        });
        rig.ToSynced();Assert.Equal(1,markers);Assert.True(rig.Engine.Robot!.VisionNvTailUnavailable);
        Assert.False(rig.Robot.Vision.NativeInitializationForTest!.ComponentInitialized);
        }
        finally { Cozmo.Transport.EngineErrorState.ErrorFlagSet=oldError; }
    }

    [Fact]
    public void ThrowingLocalReportHandlerDoesNotStopLaterHandlersOrCompletion()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();SelectFile(options,Path.Combine(backup.Root,"missing"));
        using var rig=new M4ControlTests.Rig(engineOptions:options);int markers=0;
        using var first=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>throw new InvalidOperationException("test"));
        using var second=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>markers++);
        rig.ToSynced();Assert.Equal(2,markers);Assert.True(rig.Robot.Vision.NativeInitializationForTest!.ComponentInitialized);
    }

    [Fact]
    public void InconsistentAlbumLogRunsOutsideRecognizerAndVisionGates()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();
        string directory=Path.Combine(backup.Root,"mismatch");Directory.CreateDirectory(directory);EmptyGuestAlbum(directory);
        File.WriteAllText(Path.Combine(directory,"enrollData.json"),"{\"2\":{\"name\":\"Two\",\"albumEntries\":[{\"albumEntry\":1,\"lastSeenTime\":1}]}}");SelectFile(options,directory);
        using var rig=new M4ControlTests.Rig(engineOptions:options);bool installed=false,completed=false;int markers=0;
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>
        {
            markers++;if(installed)return;installed=true;
            var recognizer=rig.Robot.Vision.Recognizer;
            recognizer.Log+=message=>
            {
                if(!message.Contains("SanityCheckBookkeeping"))return;
                completed=Task.Run(()=>recognizer.EnrolledFaces.Count).Wait(TimeSpan.FromSeconds(3));
                throw new InvalidOperationException("test log handler");
            };
        });
        rig.ToSynced();Assert.True(completed);Assert.Equal(2,markers);
        Assert.True(rig.Robot.Vision.NativeInitializationForTest!.ComponentInitialized);
    }

    [Fact]
    public void TerminatingFileIdInputSkipsCandidateAndStillCompletes()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();
        string directory=Path.Combine(backup.Root,"invalid");Directory.CreateDirectory(directory);EmptyGuestAlbum(directory);
        File.WriteAllText(Path.Combine(directory,"enrollData.json"),"{\"bad-id\":{}}");SelectFile(options,directory);
        using var rig=new M4ControlTests.Rig(engineOptions:options);var diagnostics=new List<string>();int markers=0;
        rig.Engine.StackLogLine+=diagnostics.Add;
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>markers++);
        rig.ToSynced();Assert.Equal(2,markers);Assert.True(rig.Robot.Vision.NativeInitializationForTest!.ComponentInitialized);
        Assert.Contains(diagnostics,line=>line.Contains("M1-034 M14-011 terminating file enrollment"));
    }

    [Fact]
    public void LongNameUsesNativeString8LowByteButKeepsFullLocalName()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();
        string directory=Path.Combine(backup.Root,"long");Directory.CreateDirectory(directory);EmptyGuestAlbum(directory);
        string name=new('x',257);
        File.WriteAllText(Path.Combine(directory,"enrollData.json"),"{\"2\":{\"name\":\""+name+"\",\"albumEntries\":[]}}");SelectFile(options,directory);
        using var rig=new M4ControlTests.Rig(engineOptions:options);LoadedKnownFaceReport? local=null;byte[]? raw=null;
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x1C,true,p=>local=Assert.IsType<LoadedKnownFaceReport>(p));
        rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x1C)raw=body.ToArray();};
        rig.ToSynced();rig.Tick();Assert.Equal(name,local!.Name);Assert.Equal(30,raw!.Length);Assert.Equal(1,raw[28]);Assert.Equal((byte)'x',raw[29]);
    }

    [Fact]
    public void MissingExternalInterfaceSuppressesBothReportRecipients()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();
        string directory=Path.Combine(backup.Root,"no-interface");Directory.CreateDirectory(directory);EmptyGuestAlbum(directory);
        File.WriteAllText(Path.Combine(directory,"enrollData.json"),"{\"2\":{\"name\":\"Two\",\"albumEntries\":[]}}");SelectFile(options,directory);
        using var rig=new M4ControlTests.Rig(engineOptions:options);int local=0,app=0;
        rig.Engine.LogEntry+=entry=>{if(entry.Name=="FaceTrackerImpl.Init.Success")rig.Robot.Vision.NativeInitializationForTest!.Owner.HasExternalInterface=false;};
        using var first=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>local++);
        using var second=rig.Engine.AnimationMessages.SubscribeMessage(0x1C,true,_=>local++);
        rig.Engine.EngineToGameMessage+=(tag,_)=>{if(tag is 0x58 or 0x1C)app++;};
        rig.ToSynced();rig.Tick();Assert.Equal(0,local);Assert.Equal(0,app);
        Assert.True(rig.Robot.Vision.NativeInitializationForTest!.ComponentInitialized);
    }

    [Fact]
    public void DefinedUnreadyFaceGuestStillReportsTwiceAndContinuesWithoutNvErase()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        bool oldError=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            using var backup=new BackupTestResources();var options=backup.Options();
            string directory=Path.Combine(backup.Root,"missing");SelectFile(options,directory);
            using var rig=new M4ControlTests.Rig(engineOptions:options);var names=new List<string>();int markers=0;
            rig.Robot.Vision.StockFaceCreateOverrideForTest=name=>name=="OKAO_FR_CreateAlbumHandle"?0u:null;
            rig.Engine.LogEntry+=entry=>names.Add(entry.Name??"");
            using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>markers++);
            rig.ToSynced();
            Assert.Equal(2,markers);Assert.True(rig.Robot.Vision.NativeInitializationForTest!.ComponentInitialized);
            Assert.False(rig.Engine.Robot!.VisionNvTailUnavailable);
            Assert.Contains("FaceRecognizer.EraseAllFaces.FaceLibClearAlbumFailed",names);
            Assert.Contains("VisionComponent.SaveFaceAlbumToRobot.GetSerializedFaceDataFailed",names);
            Assert.Contains("FaceTrackerImpl.LoadAlbum.NotInitialized",names);
            Assert.DoesNotContain("NVStorageComponent.Erase.Queued",names);
        }
        finally { Cozmo.Transport.EngineErrorState.ErrorFlagSet=oldError; }
    }

    [Fact]
    public void FailedEraseResponsePublishesNativeErrorCode()
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        bool oldError=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try
        {
            using var backup=new BackupTestResources();var options=backup.Options();SelectFile(options,Path.Combine(backup.Root,"missing"));
            using var rig=new M4ControlTests.Rig(engineOptions:options);var errors=new List<object?>();
            using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x7C,true,errors.Add);
            rig.ToSynced();rig.State();bool failed=false;
            for(int i=0;i<40 && !failed;i++)
            {
                rig.Tick();if(rig.Engine.NvStorage!.InFlightTag is not {} tag)continue;
                byte op=tag==0x184000?(byte)2:(byte)0;
                rig.Data(new NVOpResult{Tag=tag,Op=op,Result=tag==0x184000?(sbyte)-1:(sbyte)1,Data=Array.Empty<byte>()});
                rig.Tick();failed=tag==0x184000;
            }
            Assert.True(failed);Assert.Single(errors);Assert.Equal((byte)3,Assert.IsType<EngineErrorCodeReport>(errors[0]).Code);
        }
        finally { Cozmo.Transport.EngineErrorState.ErrorFlagSet=oldError; }
    }

    [Theory]
    [InlineData(8,92)]
    [InlineData(-8,100)]
    public void SmallerGuestAlbumCopiesRealFeatureIntoStockRecipient(int lastSeen,int expectedAge)
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();
        string directory=Path.Combine(backup.Root,"small");Directory.CreateDirectory(directory);SmallGuestAlbum(directory);
        File.WriteAllText(Path.Combine(directory,"enrollData.json"),"{\"17\":{\"enrollmentTime\":6,\"lastDataUpdateTime\":7,\"name\":\"Small\",\"albumEntries\":[{\"albumEntry\":-1,\"lastSeenTime\":\"ignored\"},{\"albumEntry\":1,\"lastSeenTime\":7},{\"albumEntry\":1,\"lastSeenTime\":"+lastSeen+"}]}}");
        string config=Path.Combine(options.ResourcesPath!,"config","engine","vision_config.json");
        File.WriteAllText(config,File.ReadAllText(config).Replace("\"FaceAlbum\" : \"robot\"","\"FaceAlbum\" : "+JsonSerializer.Serialize(directory)));
        using var rig=new M4ControlTests.Rig(engineOptions:options);rig.Robot.Vision.FaceRecognitionClockForTest=()=>100_000_000;
        LoadedKnownFaceReport? report=null;
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x1C,true,p=>report=Assert.IsType<LoadedKnownFaceReport>(p));
        rig.ToSynced();var recognizer=rig.Robot.Vision.Recognizer;
        var face=Assert.Single(recognizer.EnrolledFaces);Assert.Equal(17,face.FaceID);Assert.Equal("Small",face.Name);
        Assert.Equal(-1,face.CurrentAlbumEntryId);Assert.Equal(lastSeen*1_000_000L,face.AlbumEntries[1].LastSeenMicros);
        Assert.Equal(expectedAge,report!.SecondsSinceSeen);
        Assert.True(rig.Robot.Vision.NativeInitializationForTest!.ComponentInitialized);
        var exported=recognizer.ExportStockData();Assert.NotEmpty(exported.Album);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConstructorQueuesEraseThenReplaysFileReportsEvenWhenInstallFails(bool binaryPresent)
    {
        if(ShippedLibraries.FindLibDir() is null)return;
        using var backup=new BackupTestResources();var options=backup.Options();
        string directory=Path.Combine(backup.Root,"explicit-album");Directory.CreateDirectory(directory);
        if(binaryPresent)EmptyGuestAlbum(directory);
        File.WriteAllText(Path.Combine(directory,"enrollData.json"),"{\"2\":{\"enrollmentTime\":6,\"lastDataUpdateTime\":7,\"name\":\"Two\",\"albumEntries\":[{\"albumEntry\":0,\"lastSeenTime\":8}]},\"10\":{\"enrollmentTime\":2,\"lastDataUpdateTime\":3,\"name\":\"\",\"albumEntries\":[]}}");
        string config=Path.Combine(options.ResourcesPath!,"config","engine","vision_config.json");
        File.WriteAllText(config,File.ReadAllText(config).Replace("\"FaceAlbum\" : \"robot\"","\"FaceAlbum\" : "+JsonSerializer.Serialize(directory)));
        using var rig=new M4ControlTests.Rig(engineOptions:options);
        rig.Robot.Vision.FaceRecognitionClockForTest=()=>100_000_000;
        var local=new List<string>();var raw=new List<(ushort Tag,byte[] Body)>();var typed=new List<LoadedKnownFaceReport>();
        uint lastDelivery=rig.Engine.NativeUiDeliveryCount;bool queuedBeforeLocal=true;
        void CheckQueued(){uint next=rig.Engine.NativeUiDeliveryCount;if(next<=lastDelivery)queuedBeforeLocal=false;lastDelivery=next;}
        rig.Engine.LogEntry+=entry=>{if(entry.Name=="NVStorageComponent.Erase.Queued")local.Add("erase:"+entry.Text);};
        using var a=rig.Engine.AnimationMessages.SubscribeMessage(0x58,true,_=>{CheckQueued();local.Add("58");});
        using var b=rig.Engine.AnimationMessages.SubscribeMessage(0x1C,true,p=>{CheckQueued();local.Add("1c:"+Assert.IsType<LoadedKnownFaceReport>(p).FaceId);});
        rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag is 0x58 or 0x1C)raw.Add((tag,body.ToArray()));};
        rig.Robot.Vision.LoadedKnownFace+=typed.Add;
        rig.ToSynced();rig.Tick();
        Assert.True(rig.Robot.Vision.NativeInitializationForTest!.ComponentInitialized);
        Assert.True(queuedBeforeLocal);
        Assert.False(rig.Engine.Robot!.VisionNvTailUnavailable);
        Assert.Equal("erase:NVEntry_FaceAlbumData",local[0]);Assert.Equal("erase:NVEntry_FaceEnrollData",local[1]);
        Assert.Equal("58",local[2]);Assert.Equal("58",local[3]);
        if(binaryPresent)
        {
            Assert.Equal(new[]{"1c:10","1c:2"},local.Skip(4));
            Assert.Equal(new[]{10,2},typed.Select(x=>x.FaceId));
            Assert.Equal((98L,97L,100L,""),(typed[0].SecondsSinceEnrolled,typed[0].SecondsSinceUpdated,typed[0].SecondsSinceSeen,typed[0].Name));
            Assert.Equal((94L,93L,92L,"Two"),(typed[1].SecondsSinceEnrolled,typed[1].SecondsSinceUpdated,typed[1].SecondsSinceSeen,typed[1].Name));
            Assert.Empty(rig.Robot.Vision.Recognizer.EnrolledFaces); // metadata mismatches the empty guest
            Assert.Equal(new ushort[]{0x58,0x58,0x1C,0x1C},raw.Select(x=>x.Tag));
            using var r=new BinaryReader(new MemoryStream(raw[3].Body));
            Assert.Equal(94L,r.ReadInt64());Assert.Equal(93L,r.ReadInt64());Assert.Equal(92L,r.ReadInt64());Assert.Equal(2,r.ReadInt32());
            Assert.Equal(3,r.ReadByte());Assert.Equal("Two",Encoding.UTF8.GetString(r.ReadBytes(3)));
        }
        else {Assert.Equal(4,local.Count);Assert.Empty(typed);Assert.Equal(new ushort[]{0x58,0x58},raw.Select(x=>x.Tag));}
    }
}
