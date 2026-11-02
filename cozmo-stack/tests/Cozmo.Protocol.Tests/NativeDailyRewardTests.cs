using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class NativeDailyRewardTests
{
    private sealed class Fixture:IDisposable
    {
        internal readonly BackupTestResources Files=new();
        internal readonly CozmoRobot Robot;
        internal CozmoEngine Engine=>Robot.Engine;
        internal NeedsManager Needs=>Engine.Needs;
        internal EngineRobot Owner=>Engine.Robot!;
        internal long Now;internal int Reads;
        internal Fixture()
        {
            DateTime local=new(2026,1,1,12,0,0,DateTimeKind.Unspecified);Now=new DateTimeOffset(local,TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds()*1_000_000;
            string nurture=Path.Combine(Files.Root,"nurture");Directory.CreateDirectory(nurture);
            int seconds=(int)(Now/1_000_000);int[] timestamps=new int[85];timestamps[14]=timestamps[36]=timestamps[84]=seconds;
            var options=Files.Options(new CozmoEngineOptions{NeedsDeviceDirectory=nurture,NeedsSystemClockMicroseconds=()=>{Reads++;return Now;},NotificationHistory=new(seconds,timestamps,new bool[85]),DasDeviceId="device",LabLocaleLanguage="en",LabLocaleCountry="us"});
            string source=Path.GetDirectoryName(BackupTestResources.ShippedConfig())!;
            foreach(string name in new[]{"needs_config.json","needs_decay_config.json","needs_action_config.json","needs_level_config.json","needs_handlers_config.json","local_notification_config.json","unlock_config_nurture.json","inventory_config.json"})
                File.Copy(Path.Combine(source,name),Path.Combine(options.ResourcesPath!,"config","engine",name),true);
            File.Copy(Path.Combine(source,"..","features.json"),Path.Combine(options.ResourcesPath!,"config","features.json"),true);
            Robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,options);Assert.True(Needs.HasNativePersistence);
            Engine.Experiments.SetAssignmentVectorsForTest(Array.Empty<LabAssignmentDef>(),Array.Empty<LabAssignmentPair>());
            Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);Needs.InitAfterConnection();Set("_awaitingRobotData",false);
            Owner.Progression!.Init();Owner.Progression.ReadSink(new NvResult(-1,Array.Empty<byte>()));Owner.Inventory!.Init();Owner.Inventory.ReadSink(new NvResult(-1,Array.Empty<byte>()));
            Assert.True(Engine.NativeLocalNotifications!.RandomHistoryKnown);Engine.DrainAppDelivery();
        }
        internal void Set(string field,object value)=>typeof(NeedsManager).GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(Needs,value);
        public void Dispose(){Robot.Dispose();Files.Dispose();BackupFiles.ClearDefaultsForTest();}
    }

    [Fact]
    public void NativeStarPackersMatchIndependentArmLiterals()
    {
        Assert.Equal("000000000100000001000000",Convert.ToHexString(NeedsManager.PackStarUnlocked(0,1,1)));
        Assert.Equal("010000000100000000",Convert.ToHexString(NeedsManager.PackStarCompleted(1,1,Array.Empty<NativeNeedsReward>())));
        var rewards=new[]{new NativeNeedsReward(0,"100"u8.ToArray(),true),new NativeNeedsReward(1,"FistBump"u8.ToArray(),false),new NativeNeedsReward(2,"Singing_PopGoesTheWeasel"u8.ToArray(),false),new NativeNeedsReward(3,"A"u8.ToArray(),true)};
        Assert.Equal("78563412010000000400000000033130300101000000084669737442756D7000020000001853696E67696E675F506F70476F657354686557656173656C0003000000014101",Convert.ToHexString(NeedsManager.PackStarCompleted(0x12345678,1,rewards)));
    }
    [Fact]
    public void PriorAndCurrentBracketGatesSkipCalendarAndSameDayForceDoesNotAward()
    {
        using var f=new Fixture();var n=f.Needs;f.Reads=0;n.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Full;
        Assert.Equal(0,n.UpdateNativeStars(false));Assert.Equal(0,f.Reads);
        n.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Normal;n.State.StoreLevel(NeedId.Play,.7f);f.Reads=0;
        Assert.Equal(0,n.UpdateNativeStars(false));Assert.Equal(0,f.Reads);
        n.State.NativeLastStarMicros=f.Now;Assert.Equal(0,n.UpdateNativeStars(true));Assert.Equal(0,n.State.NativeStarsAwarded);
    }
    [Fact]
    public void ChangedYearAwardsSourceLevelRewardsAndPublishesUpdatedLevel()
    {
        using var f=new Fixture();var n=f.Needs;DateTime previous=new(2025,1,1,12,0,0,DateTimeKind.Unspecified);
        n.State.NativeLastStarMicros=new DateTimeOffset(previous,TimeZoneInfo.Local.GetUtcOffset(previous)).ToUnixTimeSeconds()*1_000_000;
        n.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Normal;n.State.StoreLevel(NeedId.Play,1f);
        f.Engine.ContextRandom.WordSourceForTest=()=>0;var wire=new List<(ushort Tag,byte[] Body)>();f.Engine.EngineToGameMessage+=(tag,body)=>wire.Add((tag,body.ToArray()));
        Assert.Equal(1,n.UpdateNativeStars(false));f.Engine.DrainAppDelivery();
        Assert.Equal("000000000100000001000000",Convert.ToHexString(Assert.Single(wire.Where(x=>x.Tag==0x67)).Body));
        Assert.Equal("01000000010000000300000000033130300001000000084669737442756D7000020000001853696E67696E675F506F70476F657354686557656173656C00",Convert.ToHexString(Assert.Single(wire.Where(x=>x.Tag==0x66)).Body));
        Assert.Equal(1,n.State.NativeUnlockLevel);Assert.Equal(0,n.State.NativeStarsAwarded);Assert.Equal(1,n.State.NativeStarsForNextUnlock);
        Assert.Equal(f.Now,n.State.NativeLastStarMicros);Assert.Equal(100,f.Owner.Inventory!.GetAmount(1));Assert.Contains(NeedsManager.NeedsNvKey,f.Engine.NvStorage!.QueuedTags);
    }
    [Fact]
    public void PriorLevelSkipsSparksButProcessesEveryNewUnlockAndCanExceedCounter()
    {
        using var f=new Fixture();var levels=NativeStarThresholds.Parse("{\"unlockLevels\":[{\"numStarsToUnlock\":1,\"rewards\":[{\"rewardType\":\"Sparks\"},{\"rewardType\":\"Unlock\",\"data\":\"FistBump\"},{\"rewardType\":\"Song\",\"data\":\"Singing_PopGoesTheWeasel\"},{\"rewardType\":\"MemoryBadge\",\"data\":\"A\"}]}]}");
        Assert.NotNull(levels);f.Set("_nativeStarThresholds",levels!);int counter=1,words=0;f.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};var output=new List<NativeNeedsReward>();
        Assert.True(f.Needs.ProcessNativeLevel(f.Engine,0,true,ref counter,output));
        Assert.Equal(-1,counter);Assert.Equal(new[]{1,2,3},output.Select(r=>r.Type));Assert.Equal(0,words);
        Assert.Contains(35u,f.Owner.Progression!.Unlocked);Assert.Equal((int)NativeUnlockNames.FromBytes("Singing_PopGoesTheWeasel"u8.ToArray()),f.Needs.State.ForceNextSong);
    }
    [Fact]
    public void AwardReportsRawAmountAndSourceSpaceBeforeInventoryCap()
    {
        using var f=new Fixture();f.Owner.Inventory!.Words[1]=9;f.Owner.Inventory.Caps[1]=10;f.Engine.ContextRandom.WordSourceForTest=()=>0;
        int unused=0;var output=new List<NativeNeedsReward>();Assert.True(f.Needs.ProcessNativeLevel(f.Engine,0,false,ref unused,output));
        var sparks=Assert.Single(output.Where(x=>x.Type==0));Assert.Equal("100",Encoding.ASCII.GetString(sparks.Data));Assert.True(sparks.InventoryIsFull);Assert.Equal(10,f.Owner.Inventory.GetAmount(1));
    }
    [Fact]
    public void FreeplayUsesSameRngAndCommunicatesOldPendingAfterInventoryAward()
    {
        using var f=new Fixture();f.Needs.SupplyNativeRewardForTest(true,4,"old");f.Engine.ContextRandom.SetSeed(5489);
        var order=new List<string>();f.Engine.LogEntry+=entry=>{if(entry.Name=="meta.inventory.change")order.Add("inventory");};
        using var sub=f.Engine.AnimationMessages.SubscribeMessage(0x65,true,p=>{Assert.Equal(new FreeplaySparksAwardedReport(4,"old"),Assert.IsType<FreeplaySparksAwardedReport>(p));order.Add("old");});
        Assert.True(f.Needs.TryNativeFreeplayReward(f.Engine,NeedsActionId.RollACube,true));
        Assert.Equal(new[]{"inventory","old"},order);Assert.Equal(9,f.Owner.Inventory!.GetAmount(1));
        Assert.Equal((true,9,"needs.FreeplaySparksReward.RollACube",true),f.Needs.NativeRewardSnapshotForTest());
    }
    [Theory]
    [InlineData(1073741824u,true)] [InlineData(2147483648u,false)] [InlineData(1288490240u,false)]
    public void FreeplayChanceUsesF32FloorAndStrictDoubleComparison(uint highWord,bool expected)
    {
        using var f=new Fixture();f.Owner.Inventory!.Words[1]=90;uint[] words={0,highWord,0,0};int index=0;f.Engine.ContextRandom.WordSourceForTest=()=>words[index++];
        Assert.Equal(expected,f.Needs.TryNativeFreeplayReward(f.Engine,NeedsActionId.RollACube,true));
        Assert.Equal(expected?4:2,index);Assert.Equal(expected,f.Needs.NativeRewardSnapshotForTest().Pending);
    }
    [Theory]
    [InlineData(0x3727C5AB,false)] [InlineData(0x3727C5AC,true)] [InlineData(unchecked((int)0xB727C5AC),true)] [InlineData(0x7FC00000,true)]
    public void RegisterWeightSkipsOnlyOrderedMagnitudeBelowNativeEpsilon(int bits,bool expectPending)
    {
        using var f=new Fixture();var actions=Assert.IsType<Dictionary<string,NeedsActionDelta>>(f.Needs.Actions);var prior=actions["RollACube"];
        actions["RollACube"]=prior with{FreeplaySparksRewardWeight=BitConverter.Int32BitsToSingle(bits)};
        f.Needs.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Full;f.Engine.ContextRandom.WordSourceForTest=()=>0;
        Assert.True(f.Needs.RegisterNeedsActionCompleted("RollACube"));Assert.True(f.Needs.TryGetNativePending(out bool pending));Assert.Equal(expectPending,pending);
    }
    [Fact]
    public void EmptyLevelVectorKeepsKnownPendingAndSkipsOnlyInvalidLevelUnits()
    {
        using var f=new Fixture();f.Set("_nativeStarThresholds",NativeStarThresholds.Parse("{\"unlockLevels\":[]}")!);int words=0;f.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        var output=new List<NativeNeedsReward>();int counter=1;Assert.True(f.Needs.ProcessNativeLevel(f.Engine,0,false,ref counter,output));Assert.Empty(output);
        Assert.False(f.Needs.TryNativeFreeplayReward(f.Engine,NeedsActionId.RollACube,true));Assert.Equal(0,words);Assert.True(f.Needs.NativePendingKnownFalse);Assert.True(f.Engine.NativeLocalNotifications!.RandomHistoryKnown);
        DateTime old=new(2025,1,1,12,0,0,DateTimeKind.Unspecified);f.Needs.State.NativeLastStarMicros=new DateTimeOffset(old,TimeZoneInfo.Local.GetUtcOffset(old)).ToUnixTimeSeconds()*1_000_000;
        f.Needs.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Normal;f.Needs.State.StoreLevel(NeedId.Play,1f);
        int star=0,completed=0;using var s=f.Engine.AnimationMessages.SubscribeMessage(0x67,true,_=>star++);using var c=f.Engine.AnimationMessages.SubscribeMessage(0x66,true,_=>completed++);
        Assert.Equal(1,f.Needs.UpdateNativeStars(false));Assert.Equal(1,star);Assert.Equal(0,completed);Assert.True(f.Needs.NativePendingKnownFalse);Assert.True(f.Engine.NativeLocalNotifications.RandomHistoryKnown);
    }
    [Fact]
    public void StarUnlockedCallbackMutationIsRereadBeforeCompletion()
    {
        using var f=new Fixture();DateTime old=new(2025,1,1,12,0,0,DateTimeKind.Unspecified);f.Needs.State.NativeLastStarMicros=new DateTimeOffset(old,TimeZoneInfo.Local.GetUtcOffset(old)).ToUnixTimeSeconds()*1_000_000;
        f.Needs.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Normal;f.Needs.State.StoreLevel(NeedId.Play,1f);int completed=0;
        using var star=f.Engine.AnimationMessages.SubscribeMessage(0x67,true,_=>f.Needs.State.NativeStarsAwarded=0);
        using var level=f.Engine.AnimationMessages.SubscribeMessage(0x66,true,_=>completed++);
        Assert.Equal(1,f.Needs.UpdateNativeStars(false));Assert.Equal(0,completed);Assert.Equal(0,f.Needs.State.NativeStarsAwarded);Assert.Equal(0,f.Needs.State.NativeUnlockLevel);
    }
}
