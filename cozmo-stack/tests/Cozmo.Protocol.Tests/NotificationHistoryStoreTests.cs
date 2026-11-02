using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

// M15-111. Expected values come from unity/scripts/csharp/Cozmo.Util.Segmentation/SegmentationService.cs:
// UpdateAppInstallTime 334-344, InitUnlockedTrickDateTimeMap 547-552, UpdateUnlockedTrickDateTimeMap 380-390,
// SendDataToEngine 479-499. reach: unit for the store; the two engine tests enter through the production owner.
[Collection("M5 process statics")]
public sealed class NotificationHistoryStoreTests:IDisposable
{
    private readonly string _directory=Directory.CreateTempSubdirectory("cozmo-notification-history-").FullName;
    private readonly List<string> _logs=new();
    private string FilePath=>Path.Combine(_directory,NotificationHistoryStore.FileName);
    public void Dispose()=>Directory.Delete(_directory,true);
    private static long Micros(DateTime local)=>new DateTimeOffset(local,TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds()*1_000_000;
    private static readonly DateTime Noon=new(2026,3,10,15,30,0,DateTimeKind.Unspecified);
    private static int Seconds(DateTime local)=>(int)(Micros(local)/1_000_000);

    [Fact]
    public void FirstRunUsesLocalMidnightAndOnlyTheThreeDefaultIds()
    {
        var history=NotificationHistoryStore.LoadOrCreate(_directory,Micros(Noon),_logs.Add).Snapshot();
        int midnight=Seconds(Noon.Date);
        Assert.Equal(midnight,history.InstallSeconds);Assert.Equal(85,history.UnlockTimestamps.Count);Assert.Equal(85,history.HasPlayed.Count);
        for(int i=0;i<85;i++)Assert.Equal(i is 14 or 36 or 84?midnight:0,history.UnlockTimestamps[i]);
        Assert.All(history.HasPlayed,Assert.False);Assert.True(File.Exists(FilePath));Assert.Empty(_logs);
        Assert.Equal(433,NotificationDeterminantCodec.Pack(history).Length); // the existing tag 0xD6 body accepts it
    }
    [Fact]
    public void SecondRunReloadsAndKeepsTheOriginalInstall()
    {
        var first=NotificationHistoryStore.LoadOrCreate(_directory,Micros(Noon),_logs.Add).Snapshot();
        var second=NotificationHistoryStore.LoadOrCreate(_directory,Micros(Noon.AddDays(20)),_logs.Add).Snapshot();
        Assert.Equal(first.InstallSeconds,second.InstallSeconds);Assert.Equal(first.UnlockTimestamps,second.UnlockTimestamps);Assert.Equal(first.HasPlayed,second.HasPlayed);
        Assert.Empty(_logs);
    }
    [Theory]
    [InlineData("not json")]
    [InlineData("{\"InstallSeconds\":5,\"UnlockTimestamps\":[1],\"HasPlayed\":[false]}")]
    public void CorruptFileIsLoggedAndRecreated(string content)
    {
        File.WriteAllText(FilePath,content);
        var history=NotificationHistoryStore.LoadOrCreate(_directory,Micros(Noon),_logs.Add).Snapshot();
        Assert.Contains(_logs,line=>line.Contains("M15-111")&&line.Contains("recreating"));
        Assert.Equal(Seconds(Noon.Date),history.InstallSeconds);Assert.Equal(85,history.UnlockTimestamps.Count);
        _logs.Clear();
        Assert.Equal(history.UnlockTimestamps,NotificationHistoryStore.LoadOrCreate(_directory,Micros(Noon.AddDays(1)),_logs.Add).Snapshot().UnlockTimestamps);Assert.Empty(_logs);
    }
    [Fact]
    public void UnlockIsRecordedOncePersistsAndLeavesDefaultIdsAlone()
    {
        var store=NotificationHistoryStore.LoadOrCreate(_directory,Micros(Noon),_logs.Add);
        Assert.True(store.RecordUnlock(35,Micros(Noon.AddHours(1))));
        Assert.False(store.RecordUnlock(35,Micros(Noon.AddHours(2))));
        Assert.False(store.RecordUnlock(14,Micros(Noon.AddHours(2))));
        Assert.False(store.RecordUnlock(85,Micros(Noon.AddHours(2))));
        var reloaded=NotificationHistoryStore.LoadOrCreate(_directory,Micros(Noon.AddDays(1)),_logs.Add).Snapshot();
        Assert.Equal(Seconds(Noon.AddHours(1)),reloaded.UnlockTimestamps[35]);Assert.Equal(Seconds(Noon.Date),reloaded.UnlockTimestamps[14]);
    }

    private CozmoRobot Robot(BackupTestResources files,NotificationDeterminants? explicitHistory=null)
    {
        BackupFiles.ClearDefaultsForTest();
        string source=Path.GetDirectoryName(BackupTestResources.ShippedConfig())!;
        var options=files.Options(new CozmoEngineOptions{NeedsDeviceDirectory=_directory,NeedsSystemClockMicroseconds=()=>Micros(Noon),NotificationHistory=explicitHistory,DasDeviceId="device",LabLocaleLanguage="en",LabLocaleCountry="us"});
        foreach(string name in new[]{"needs_config.json","needs_decay_config.json","needs_action_config.json","needs_level_config.json","needs_handlers_config.json","local_notification_config.json"})
            File.Copy(Path.Combine(source,name),Path.Combine(options.ResourcesPath!,"config","engine",name),true);
        File.Copy(Path.Combine(source,"..","features.json"),Path.Combine(options.ResourcesPath!,"config","features.json"),true);
        return CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>1_000_000_000,options);
    }
    // reach: production. The engine with no explicit history creates the store before Needs Init, so startup Generate has its input.
    [Fact]
    public void EngineSuppliesTheStoreBeforeNeedsInitWhenHostGaveNoHistory()
    {
        using var files=new BackupTestResources();
        using(var robot=Robot(files)){Assert.True(robot.Engine.NativeLocalNotifications!.RandomHistoryKnown);Assert.Equal(14,robot.Engine.NativeLocalNotifications.LatestId); /* 14 is the first of the three tied install stamps, and LocalNotifications keeps the first maximum */ Assert.True(File.Exists(FilePath));}
        BackupFiles.ClearDefaultsForTest();
    }
    // An explicit host history stays the host's: no store file appears.
    [Fact]
    public void ExplicitHistoryIsNotReplaced()
    {
        using var files=new BackupTestResources();
        using(var robot=Robot(files,new NotificationDeterminants(7,new int[85],new bool[85]))){Assert.False(File.Exists(FilePath));}
        BackupFiles.ClearDefaultsForTest();
    }
    // reach: production. UnlockablesManager.HandleOnUnlockRequestSuccess records on the unlock's result report, which is
    // WriteComplete after the NV write succeeds; a failed write sends no report and records nothing.
    [Theory]
    [InlineData((sbyte)0,true)] [InlineData((sbyte)-2,false)]
    public void UnlockThroughProgressionRecordsOnSuccessfulWriteAndUpdatesTheEngine(sbyte writeResult,bool recorded)
    {
        using var files=new BackupTestResources();
        using(var robot=Robot(files))
        {
            var engine=robot.Engine;engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);var owner=engine.Robot!;var commands=new List<NVCommand>();
            owner.SendFault=message=>{if(message is NVCommand command)commands.Add(command);return true;};
            owner.Progression!.Init();engine.NvStorage!.Update();owner.DeliverMessage(new NVOpResult{Tag=0x182000,Op=NvStorageComponent.OpRead,Result=-1,Data=Array.Empty<byte>()});
            owner.Progression.SetUnlock(35,true);
            var nv=engine.NvStorage;int seen=0;
            for(int i=0;i<100&&!nv.IsIdle;i++)
            {
                nv.Update();
                while(seen<commands.Count){var c=commands[seen++];owner.DeliverMessage(new NVOpResult{Tag=c.Tag,Op=c.Op,Result=c.Op==NvStorageComponent.OpWrite?writeResult:(sbyte)0,Data=Array.Empty<byte>()});}
            }
            engine.Tick();
            int stored=JsonDocument.Parse(File.ReadAllText(FilePath)).RootElement.GetProperty("UnlockTimestamps")[35].GetInt32();
            Assert.Equal(recorded?Seconds(Noon):0,stored);
            Assert.Equal(recorded?35:14,engine.NativeLocalNotifications!.LatestId); // the new stamp is later than the install stamps, and reached the engine on 0xD6
        }
        BackupFiles.ClearDefaultsForTest();
    }
}
