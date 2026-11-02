using Cozmo.Robot.Behavior;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class NativeNeedsMetadataTests
{
    private static void Set(NeedsManager n,string name,object value)=>typeof(NeedsManager).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(n,value);
    private static CozmoEngineOptions SourceOptions(BackupTestResources files,string directory,Func<long>? clock=null)
    {
        var options=files.Options(new CozmoEngineOptions{NeedsDeviceDirectory=directory,NeedsSystemClockMicroseconds=clock??(()=>3_000_000_000),DasDeviceId="device",LabLocaleLanguage="en",LabLocaleCountry="us"});
        string source=Path.GetDirectoryName(BackupTestResources.ShippedConfig())!;
        foreach(string name in new[]{"needs_config.json","needs_decay_config.json","needs_action_config.json","needs_level_config.json","needs_handlers_config.json"})
            File.Copy(Path.Combine(source,name),Path.Combine(options.ResourcesPath!,"config","engine",name),true);
        return options;
    }
    // Supplied source fields isolate the resolver table while retaining the real context,
    // Lab recipient, NV queue, local broadcasts and device-file writer.
    [Theory]
    [InlineData(false,false,false,0,0,0,false,true,true,false,false,0)]
    [InlineData(false,true,false,7,7,0,false,false,true,false,false,-1)]
    [InlineData(false,true,true,1,7,0,false,true,true,true,false,1)]
    [InlineData(false,true,false,1,7,0,false,false,true,true,false,-1)]
    [InlineData(true,false,false,0,7,2000,false,true,false,true,true,1)]
    [InlineData(true,true,false,1,7,2000,false,true,false,true,true,1)]
    [InlineData(true,true,false,7,7,2000,false,true,false,true,true,1)]
    [InlineData(true,true,false,7,7,500,false,false,true,false,false,-1)]
    [InlineData(true,true,false,7,7,1000,false,false,false,false,false,-1)]
    [InlineData(true,false,false,0,7,2000,true,true,true,true,true,1)]
    [InlineData(true,true,false,7,7,1000,true,false,true,false,false,-1)]
    public void NativeResolverFollowsNineSourceCases(bool robotCopy,bool deviceCopy,bool alternate,uint previous,uint serial,long robotSeconds,bool rewrite,bool writeDevice,bool writeRobot,bool changed,bool apply,int action)
    {
        using var files=new BackupTestResources();
        string directory=Path.Combine(files.Root,"nurture");Directory.CreateDirectory(directory);
        var options=SourceOptions(files,directory);
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,options);
        var engine=robot.Engine;var owner=engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false)!;
        var needs=engine.Needs;Assert.True(needs.HasNativePersistence);
        engine.Experiments.SetAssignmentVectorsForTest(Array.Empty<LabAssignmentDef>(),Array.Empty<LabAssignmentPair>());
        needs.InitAfterConnection();
        Set(needs,"_awaitingRobotData",false);Set(needs,"_robotReadSucceeded",robotCopy);Set(needs,"_deviceDataPresent",deviceCopy);
        Set(needs,"_robotRewriteNeeded",rewrite);Set(needs,"_previousSerial",previous);Set(needs,"_serial",serial);
        Set(needs,"_nativeDeviceTimestampMicros",1_000_000_000L);
        needs.State.NativeSerial=serial;needs.State.NativeStarsAwarded=11;needs.State.NativeLastDisconnectMicros=4;needs.State.NativeLastBackgroundMicros=8;needs.State.StoreLevel(NeedId.Play,.9f);
        var candidate=new NeedsState(needs.Config){NativeSerial=serial,NativeDateTimeMicros=robotSeconds*1_000_000L,NativeStarsAwarded=42};
        candidate.StoreLevel(NeedId.Play,.5f);Set(needs,"_nativeRobotCandidate",candidate);
        string fixedFile=needs.FixedDeviceFilePath!;if(File.Exists(fixedFile))File.Delete(fixedFile);
        string alternateFile=Path.Combine(directory,"alternate.json");
        if(alternate)
            File.WriteAllText(alternateFile,"{\"_StateFileVersion\":5,\"_DateTime\":1000,\"TimeCreated\":0,\"TimeLastDisconnect\":0,\"TimeLastAppBackgrounded\":0,\"OpenAppAfterDisconnect\":0,\"_SerialNumber\":7,\"CurNeedsUnlockLevel\":0,\"NumStarsAwarded\":0,\"NumStarsForNextUnlock\":1,\"CurNeedLevel\":{\"Repair\":90000,\"Energy\":80000,\"Play\":90000},\"PartIsDamaged\":{\"Head\":false,\"Lift\":false,\"Treads\":false},\"TimeLastStarAwarded\":0,\"ForceNextSong\":\"Count\"}");
        needs.AlternateDeviceFilePath=_=>alternateFile;
        engine.DrainAppDelivery();int changes=0;long seenDisconnect=-1,seenBackground=-1;var order=new List<string>();var wire=new List<(ushort Tag,byte[] Body)>();
        engine.EngineToGameMessage+=(tag,body)=>wire.Add((tag,body.ToArray()));
        using var reports=engine.AnimationMessages.SubscribeMessage(0x62,true,p=>order.Add("62:"+Assert.IsType<NativeNeedsStateReport>(p).Action));
        using var stage=engine.AnimationMessages.SubscribeMessage(0x69,true,p=>{Assert.Equal(23,Assert.IsType<int>(p));order.Add("69");});
        Set(needs,"_nativeOnboardingStage",23);
        using var subscription=engine.AnimationMessages.SubscribeMessage(0x6B,true,_=>
        {
            changes++;order.Add("6B");seenDisconnect=needs.State.NativeLastDisconnectMicros;seenBackground=needs.State.NativeLastBackgroundMicros;
            if(apply){needs.State.NativeLastDisconnectMicros=77;needs.State.NativeLastBackgroundMicros=88;}
        });
        needs.InitAfterReadFromRobotAttempt();
        engine.DrainAppDelivery();
        Assert.Equal(writeDevice,File.Exists(fixedFile));Assert.Equal(writeRobot,engine.NvStorage!.QueuedTags.Contains(NeedsManager.NeedsNvKey));
        if(writeRobot)Assert.False(needs.RobotRewriteNeeded);
        Assert.Equal(changed?1:0,changes);Assert.Equal(apply?42:alternate?0:11,needs.State.NativeStarsAwarded);
        var expected=new List<string>();if(alternate)expected.Add("62:"+action);if(changed)expected.Add("6B");if(action>=0&&!alternate)expected.Add("62:"+action);expected.Add("69");
        Assert.Equal(expected,order);Assert.Equal("17000000",Convert.ToHexString(Assert.Single(wire.Where(item=>item.Tag==0x69)).Body));
        if(apply)
        {
            Assert.Equal(deviceCopy&&previous==serial?4:0,seenDisconnect);
            Assert.Equal(deviceCopy&&previous!=serial?0:8,seenBackground);
            Assert.Equal(77,needs.State.NativeLastDisconnectMicros);Assert.Equal(88,needs.State.NativeLastBackgroundMicros);
        }
    }
    [Fact]
    public void NativeReadFailureAndFutureVersionKeepDefinedErrorSideEffects()
    {
        bool saved=EngineErrorState.ErrorFlagSet;
        try
        {
            var needs=new NeedsManager(()=>0);needs.EnableUncertifiedPersistenceForTests();
            var keys=new List<string>();needs.SourceLog=(level,channel,key,text)=>keys.Add(key);
            EngineErrorState.ErrorFlagSet=false;
            Assert.False(needs.FinishReadFromRobot(Array.Empty<byte>(),-1));
            Assert.False(EngineErrorState.ErrorFlagSet);
            Assert.Equal("NeedsManager.FinishReadFromRobot",keys[^1]);
            Assert.False(needs.FinishReadFromRobot(Array.Empty<byte>(),-4));
            Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.Equal("NeedsManager.FinishReadFromRobot.ReadFailedFinish",keys[^1]);
            EngineErrorState.ErrorFlagSet=false;
            Assert.False(needs.FinishReadFromRobot(new byte[]{6},0));
            Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.Equal("NeedsManager.FinishReadFromRobot.StateFileVersionIsFuture",keys[^1]);
        }
        finally{EngineErrorState.ErrorFlagSet=saved;}
    }
    [Fact]
    public void ActualNvReadStoresOnboardingBeforeDeviceWinsCaseEight()
    {
        using var files=new BackupTestResources();string directory=Path.Combine(files.Root,"nurture");Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"needsState.json"),"{\"_StateFileVersion\":5,\"_DateTime\":3000,\"TimeCreated\":1,\"TimeLastDisconnect\":0,\"TimeLastAppBackgrounded\":0,\"OpenAppAfterDisconnect\":0,\"_SerialNumber\":0,\"CurNeedsUnlockLevel\":0,\"NumStarsAwarded\":11,\"NumStarsForNextUnlock\":1,\"CurNeedLevel\":{\"Repair\":90000,\"Energy\":80000,\"Play\":90000},\"PartIsDamaged\":{\"Head\":false,\"Lift\":false,\"Treads\":false},\"TimeLastStarAwarded\":0,\"ForceNextSong\":\"Invalid\"}");
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,SourceOptions(files,directory));var engine=robot.Engine;
        var owner=engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false)!;var needs=engine.Needs;Assert.True(needs.HasNativePersistence);
        engine.Experiments.SetAssignmentVectorsForTest(Array.Empty<LabAssignmentDef>(),Array.Empty<LabAssignmentPair>());
        var commands=new List<NVCommand>();owner.SendFault=message=>{if(message is NVCommand command)commands.Add(command);return true;};
        var stages=new List<int>();using var subscription=engine.AnimationMessages.SubscribeMessage(0x69,true,p=>stages.Add(Assert.IsType<int>(p)));
        needs.InitAfterConnection();needs.InitAfterSerialNumberAcquired(0);engine.NvStorage!.Update();
        Assert.Contains(commands,c=>c.Tag==NeedsManager.NeedsNvKey&&c.Op==NvStorageComponent.OpRead);
        byte[] payload=Convert.FromHexString("05000000D007000000000000A861000050C30000F82401000000000000000000000000000000000000000000000000000000000002000000030000000000010000000000000000000000000000000000000000000000000000000000D204000000000000010000000E0000006400000000000000");
        Assert.Equal(116,payload.Length);
        byte[] data=new byte[16+payload.Length];BitConverter.GetBytes(NvStorageComponent.NonFactoryHeaderMagic).CopyTo(data,0);BitConverter.GetBytes((uint)payload.Length).CopyTo(data,8);payload.CopyTo(data,16);
        owner.DeliverMessage(new NVOpResult{Tag=NeedsManager.NeedsNvKey,Op=NvStorageComponent.OpRead,Result=0,Data=data});
        Assert.Equal(new[]{1},stages);Assert.Equal(11,needs.State.NativeStarsAwarded);
    }
    [Fact]
    public void FirstRobotNotRepresentedByNvFacadeDoesNotFabricateReadFailure()
    {
        using var files=new BackupTestResources();string directory=Path.Combine(files.Root,"nurture");Directory.CreateDirectory(directory);
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,SourceOptions(files,directory));var engine=robot.Engine;
        var first=engine.Robots.AddRobot(123,withRic:false)!;var primary=engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false)!;
        Assert.Same(first,engine.Robots.GetFirstRobot());Assert.Same(primary,engine.Robot);Assert.True(engine.Needs.HasNativePersistence);
        engine.Experiments.SetAssignmentVectorsForTest(Array.Empty<LabAssignmentDef>(),Array.Empty<LabAssignmentPair>());
        int stages=0;using var subscription=engine.AnimationMessages.SubscribeMessage(0x69,true,_=>stages++);
        engine.Needs.InitAfterConnection();engine.Needs.InitAfterSerialNumberAcquired(0);
        Assert.True(engine.Needs.AwaitingRobotData);Assert.DoesNotContain(NeedsManager.NeedsNvKey,engine.NvStorage!.QueuedTags);Assert.Equal(0,stages);
    }
    [Fact]
    public void UnsupportedDeviceInputDoesNotBecomeFileAbsentOrResumeResolver()
    {
        using var files=new BackupTestResources();string directory=Path.Combine(files.Root,"nurture");Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"needsState.json");const string input="{\"_StateFileVersion\":5,\"_DateTime\":1.5}";File.WriteAllText(path,input);
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,SourceOptions(files,directory));var engine=robot.Engine;
        Assert.True(engine.Needs.HasNativePersistence);Assert.Equal(input,File.ReadAllText(path));
        engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        engine.Experiments.SetAssignmentVectorsForTest(new[]{new LabAssignmentDef("k"u8.ToArray(),"u"u8.ToArray(),"v"u8.ToArray())},Array.Empty<LabAssignmentPair>());
        int stages=0;using var subscription=engine.AnimationMessages.SubscribeMessage(0x69,true,_=>stages++);
        engine.Needs.InitAfterReadFromRobotAttempt();
        Assert.Contains(0x196000u,engine.NvStorage!.QueuedTags);Assert.DoesNotContain(NeedsManager.NeedsNvKey,engine.NvStorage.QueuedTags);
        Assert.Equal(0,stages);Assert.Equal(input,File.ReadAllText(path));
    }
    [Fact]
    public void UnsupportedDeviceOutputDoesNotRunLaterRobotWrite()
    {
        using var files=new BackupTestResources();string directory=Path.Combine(files.Root,"nurture");Directory.CreateDirectory(directory);
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,SourceOptions(files,directory));var engine=robot.Engine;
        engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);var needs=engine.Needs;Assert.True(needs.HasNativePersistence);
        engine.Experiments.SetAssignmentVectorsForTest(Array.Empty<LabAssignmentDef>(),Array.Empty<LabAssignmentPair>());
        needs.InitAfterConnection();Set(needs,"_awaitingRobotData",false);Set(needs,"_robotReadSucceeded",false);Set(needs,"_deviceDataPresent",false);
        needs.State.ForceNextSong=99;string path=needs.FixedDeviceFilePath!;if(File.Exists(path))File.Delete(path);
        int stages=0;using var subscription=engine.AnimationMessages.SubscribeMessage(0x69,true,_=>stages++);
        needs.InitAfterReadFromRobotAttempt();
        Assert.Equal(1,stages);Assert.False(File.Exists(path));Assert.DoesNotContain(NeedsManager.NeedsNvKey,engine.NvStorage!.QueuedTags);
    }
    [Fact]
    public void WriteReloadsNeedsRobotAfterSourceLogReplacesOwner()
    {
        using var files=new BackupTestResources();string directory=Path.Combine(files.Root,"nurture");Directory.CreateDirectory(directory);
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,SourceOptions(files,directory));var engine=robot.Engine;
        var original=engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false)!;var needs=engine.Needs;Assert.True(needs.HasNativePersistence);
        needs.InitAfterConnection();Set(needs,"_awaitingRobotData",false);Set(needs,"_nativeCopiesResolved",true);/* supplied: this unit skips the read and resolver */
        EngineRobot? replacement=null;var sent=new List<NVCommand>();
        engine.LogEntry+=entry=>
        {
            if(entry.Name!="NeedsManager.StartWriteToRobot"||replacement is not null)return;
            engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);
            replacement=engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false)!;
            replacement.SendFault=message=>{if(message is NVCommand command)sent.Add(command);return true;};
            needs.InitAfterConnection();Set(needs,"_awaitingRobotData",false);
        };
        needs.PossiblyStartWriteToRobot(true);
        Assert.NotNull(replacement);Assert.NotSame(original,replacement);Assert.Same(replacement,engine.Robot);
        Assert.Contains(NeedsManager.NeedsNvKey,engine.NvStorage!.QueuedTags);
        engine.NvStorage.Update();Assert.Contains(sent,c=>c.Tag==NeedsManager.NeedsNvKey&&c.Op==NvStorageComponent.OpErase);
    }
    [Fact]
    public void ActualNativeDeviceThrottleUsesMicrosecondBoundary()
    {
        using var files=new BackupTestResources();string directory=Path.Combine(files.Root,"nurture");Directory.CreateDirectory(directory);long now=1_000_000;
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,SourceOptions(files,directory,()=>now));var needs=robot.Engine.Needs;Assert.True(needs.HasNativePersistence);
        needs.State.NativeDateTimeMicros=1_000_000;string path=needs.FixedDeviceFilePath!;File.WriteAllText(path,"sentinel");
        now=61_999_999;needs.PossiblyWriteToDevice();Assert.Equal("sentinel",File.ReadAllText(path));Assert.Equal(1_000_000,needs.State.NativeDateTimeMicros);
        now=62_000_000;needs.PossiblyWriteToDevice();Assert.NotEqual("sentinel",File.ReadAllText(path));Assert.Equal(62_000_000,needs.State.NativeDateTimeMicros);
    }
    [Fact]
    public void ActualNativeRobotThrottleAndForcedReadGuardUseMicroseconds()
    {
        using var files=new BackupTestResources();string directory=Path.Combine(files.Root,"nurture");Directory.CreateDirectory(directory);long now=1_000_000;int clockReads=0;
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,SourceOptions(files,directory,()=>{clockReads++;return now;}));var engine=robot.Engine;var needs=engine.Needs;Assert.True(needs.HasNativePersistence);
        clockReads=0;needs.PossiblyStartWriteToRobot(true);Assert.Equal(0,clockReads);
        engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);needs.InitAfterConnection();Set(needs,"_nativeCopiesResolved",true);/* supplied: this unit skips the read and resolver */Set(needs,"_nativeLastRobotWriteMicros",1_000_000L);
        now=602_000_000;needs.PossiblyStartWriteToRobot(true);Assert.Equal(1_000_000L,typeof(NeedsManager).GetField("_nativeLastRobotWriteMicros",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(needs));
        Assert.DoesNotContain(NeedsManager.NeedsNvKey,engine.NvStorage!.QueuedTags);
        Set(needs,"_awaitingRobotData",false);now=601_999_999;needs.PossiblyStartWriteToRobot(false);Assert.DoesNotContain(NeedsManager.NeedsNvKey,engine.NvStorage.QueuedTags);
        now=602_000_000;needs.PossiblyStartWriteToRobot(false);Assert.Contains(NeedsManager.NeedsNvKey,engine.NvStorage.QueuedTags);
    }
    [Fact]
    public void ActualNativeRestoreDividesMicrosecondsBeforeFloatConversion()
    {
        using var files=new BackupTestResources();string directory=Path.Combine(files.Root,"nurture");Directory.CreateDirectory(directory);long now=1_900_000;
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,SourceOptions(files,directory,()=>now));var needs=robot.Engine.Needs;Assert.True(needs.HasNativePersistence);
        needs.State.NativeDateTimeMicros=0;var logs=new List<string>();needs.State.SourceLog=(level,channel,key,text)=>{if(key=="NeedsState.ApplyDecay")logs.Add(text);};
        typeof(NeedsManager).GetMethod("ApplyNativeElapsed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(needs,new object[]{false});
        Assert.Equal(3,logs.Count);Assert.All(logs,text=>Assert.Contains("elapsed time of 1.000000 seconds",text));
    }
    [Fact]
    public void SourceStarThresholdsKeepActualLevelOrderAndLastFallback()
    {
        string? path=null;for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {var candidate=Path.Combine(d.FullName,"re-analysis","obb","assets","cozmo_resources","config","engine","needs_level_config.json");if(File.Exists(candidate)){path=candidate;break;}}
        Assert.NotNull(path);var levels=Assert.IsType<NativeStarThresholds>(NativeStarThresholds.Parse(File.ReadAllText(path!)));
        Assert.Equal(1,levels.GetMax(0));Assert.Equal(1,levels.GetMax(27));Assert.Equal(1,levels.GetMax(28));Assert.Equal(1,levels.GetMax(-1));
        Assert.Equal(0,NativeStarThresholds.Parse("{\"unlockLevels\":[]}")!.GetMax(0));
    }    [Fact]
    public void FullNeedsStateBodyMatchesNativePackLiteral()
    {
        var value=new NativeNeedsStateReport(new uint[]{0,0x3F800000,0xBF800000},new[]{0,1,-1},new[]{true,false,true},0x12345678,2,3,0x11223344,new byte[]{65},0x01020304);
        Assert.Equal("03000000000000803F000080BF030000000001000000FFFFFFFF0301000178563412020000000300000044332211014104030201",Convert.ToHexString(NativeNeedsStateReportCodec.Pack(value)));
    }
    [Fact]
    public void QuantizationAddsSourceHalfBeforeSignedConversion()
    {Assert.Equal(1,NeedsManager.QuantizeLevel(.000006f));Assert.Equal(0,NeedsManager.QuantizeLevel(-.000006f));Assert.Equal(50000,NeedsManager.QuantizeLevel(.5f));}
    [Fact]
    public void LiteralRobotBlobDecodesSourceOffsets()
    {
        var bytes=Convert.FromHexString("05000000D007000000000000A861000050C30000F82401000000000000000000000000000000000000000000000000000000000002000000030000000000010000000000000000000000000000000000000000000000000000000000D204000000000000010000000E0000006400000000000000");
        Assert.Equal(116,bytes.Length);var value=NeedsStateOnRobot.Unpack(bytes);
        Assert.Equal(2000ul,value.TimeLastWritten);Assert.Equal(new[]{25000,50000,75000},value.CurNeedLevel.Take(3));
        Assert.Equal(2,value.CurNeedsUnlockLevel);Assert.Equal(3,value.NumStarsAwarded);Assert.Equal(1,value.PartIsDamaged[2]);
        Assert.Equal(1234ul,value.TimeLastStarAwarded);Assert.Equal(1,value.OnboardingStageCompleted);Assert.Equal(14,value.ForceNextSong);Assert.Equal(100ul,value.TimeCreated);
    }
}
