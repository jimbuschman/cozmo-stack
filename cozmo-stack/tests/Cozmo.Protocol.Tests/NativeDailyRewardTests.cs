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
        internal void Set(string field,object? value)=>typeof(NeedsManager).GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(Needs,value);
        internal void LastStarYearsAgo()
        {DateTime old=new(2025,1,1,12,0,0,DateTimeKind.Unspecified);Needs.State.NativeLastStarMicros=new DateTimeOffset(old,TimeZoneInfo.Local.GetUtcOffset(old)).ToUnixTimeSeconds()*1_000_000;}
        internal void PlayEntersFull(){Needs.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Normal;Needs.State.StoreLevel(NeedId.Play,1f);}
        internal bool BracketsDirty=>(bool)typeof(NeedsState).GetField("_bracketsDirty",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(Needs.State)!;
        public void Dispose(){Robot.Dispose();Files.Dispose();BackupFiles.ClearDefaultsForTest();}
    }
    // A complete level as shipped in needs_level_config.json (all 13 scalars); only the fields a test varies differ.
    private static string Level(string rewards,int target=100,int minimum=100,int maximum=120,int prior=1)=>
        $"{{\"numStarsToUnlock\":1,\"targetSparksTotal\":{target},\"maxPriorLevelUnlocks\":{prior},\"minSparksPct\":0.9,\"maxSparksPct\":1.1,\"minSparks\":{minimum},\"minMaxSparks\":{maximum},\"freeplayTargetSparksTotal\":100,\"freeplayMinSparksRewardPct\":0.3,\"freeplayMinSparksPct\":0.02,\"freeplayMaxSparksPct\":0.1,\"freeplayMinSparks\":2,\"freeplayMinMaxSparks\":3,\"rewards\":[{rewards}]}}";
    private static NativeStarThresholds Config(params string[] levels)=>NativeStarThresholds.Parse("{\"unlockLevels\":["+string.Join(",",levels)+"]}")!;
    private const string Sparks="{\"rewardType\":\"Sparks\"}",FistBump="{\"rewardType\":\"Unlock\",\"data\":\"FistBump\"}",Song="{\"rewardType\":\"Song\",\"data\":\"Singing_PopGoesTheWeasel\"}",Badge="{\"rewardType\":\"MemoryBadge\",\"data\":\"A\"}";

    // M15-093 pack 0x0071732E (tag 0x67), 0x0071715C (tag 0x66), 0x00784414 (nested reward). reach: unit, codec only.
    [Fact]
    public void NativeStarPackersMatchIndependentArmLiterals()
    {
        Assert.Equal("000000000100000001000000",Convert.ToHexString(NeedsManager.PackStarUnlocked(0,1,1)));
        Assert.Equal("010000000100000000",Convert.ToHexString(NeedsManager.PackStarCompleted(1,1,Array.Empty<NativeNeedsReward>())));
        var rewards=new[]{new NativeNeedsReward(0,"100"u8.ToArray(),true),new NativeNeedsReward(1,"FistBump"u8.ToArray(),false),new NativeNeedsReward(2,"Singing_PopGoesTheWeasel"u8.ToArray(),false),new NativeNeedsReward(3,"A"u8.ToArray(),true)};
        Assert.Equal("78563412010000000400000000033130300101000000084669737442756D7000020000001853696E67696E675F506F70476F657354686557656173656C0003000000014101",Convert.ToHexString(NeedsManager.PackStarCompleted(0x12345678,1,rewards)));
    }
    // M15-093 0x00696AD6..0x00696AEC gates before the clock 0x00696B06; same day returns 0 at 0x00696B5A..0x00696B6E. reach: unit.
    [Fact]
    public void PriorAndCurrentBracketGatesSkipCalendarAndSameDayForceDoesNotAward()
    {
        using var f=new Fixture();var n=f.Needs;f.Reads=0;n.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Full;
        Assert.Equal(0,n.UpdateNativeStars(false));Assert.Equal(0,f.Reads);
        n.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Normal;n.State.StoreLevel(NeedId.Play,.7f);f.Reads=0;
        Assert.Equal(0,n.UpdateNativeStars(false));Assert.Equal(0,f.Reads);
        n.State.NativeLastStarMicros=f.Now;Assert.Equal(0,n.UpdateNativeStars(true));Assert.Equal(0,n.State.NativeStarsAwarded);
    }
    // M15-093 0x00696BBA..0x00696BE2, 0x0069CBBC..0x0069CBC8 and shipped level 0: 66 carries the updated level and max. reach: unit, direct call.
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
    // M15-093 0x00699460..64 prior Sparks skip, 0x00699604..12 decrement with no positivity test, 0x00699552..68 type 3 append. reach: unit.
    [Fact]
    public void PriorLevelSkipsSparksButProcessesEveryNewUnlockAndCanExceedCounter()
    {
        using var f=new Fixture();f.Set("_nativeStarThresholds",Config(Level(string.Join(",",Sparks,FistBump,Song,Badge))));int counter=1,words=0;f.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};var output=new List<NativeNeedsReward>();
        Assert.True(f.Needs.ProcessNativeLevel(f.Engine,0,true,ref counter,output));
        Assert.Equal(-1,counter);Assert.Equal(new[]{1,2,3},output.Select(r=>r.Type));Assert.Equal(0,words);
        Assert.Contains(35u,f.Owner.Progression!.Unlocked);Assert.Equal((int)NativeUnlockNames.FromBytes("Singing_PopGoesTheWeasel"u8.ToArray()),f.Needs.State.ForceNextSong);
    }
    // M15-093 0x00699468..70 space before the award, 0x0069961C..74 raw decimal and (space<raw)&&(space!=-1). reach: unit.
    [Fact]
    public void AwardReportsRawAmountAndSourceSpaceBeforeInventoryCap()
    {
        using var f=new Fixture();f.Owner.Inventory!.Words[1]=9;f.Owner.Inventory.Caps[1]=10;f.Engine.ContextRandom.WordSourceForTest=()=>0;
        int unused=0;var output=new List<NativeNeedsReward>();Assert.True(f.Needs.ProcessNativeLevel(f.Engine,0,false,ref unused,output));
        var sparks=Assert.Single(output.Where(x=>x.Type==0));Assert.Equal("100",Encoding.ASCII.GetString(sparks.Data));Assert.True(sparks.InventoryIsFull);Assert.Equal(10,f.Owner.Inventory.GetAmount(1));
    }
    // M15-071 award 0x006963BA, old pending 0x006963BC..0x00696402, then the new stores 0x0069640A..0x00696482. reach: unit, supplied pending.
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
    // M15-071 0x00696DA6..0x00696DF8: amount 90 of 100 gives 0.1, floored to f32 0.3; 0x4CCCCD00/2^32 equals f32 0.3 exactly, so strict < is false. reach: unit.
    [Theory]
    [InlineData(1073741824u,true)] [InlineData(2147483648u,false)] [InlineData(1288490240u,false)]
    public void FreeplayChanceUsesF32FloorAndStrictDoubleComparison(uint highWord,bool expected)
    {
        using var f=new Fixture();f.Owner.Inventory!.Words[1]=90;uint[] words={0,highWord,0,0};int index=0;f.Engine.ContextRandom.WordSourceForTest=()=>words[index++];
        Assert.Equal(expected,f.Needs.TryNativeFreeplayReward(f.Engine,NeedsActionId.RollACube,true));
        Assert.Equal(expected?4:2,index);Assert.Equal(expected,f.Needs.NativeRewardSnapshotForTest().Pending);
    }
    // M15-071 0x0069638A..0x006963A4, literal 0x3727C5AC at 0x0069645C: only ordered magnitude < epsilon skips. reach: production entry, supplied weight.
    [Theory]
    [InlineData(0x3727C5AB,false)] [InlineData(0x3727C5AC,true)] [InlineData(unchecked((int)0xB727C5AC),true)] [InlineData(0x7FC00000,true)]
    public void RegisterWeightSkipsOnlyOrderedMagnitudeBelowNativeEpsilon(int bits,bool expectPending)
    {
        using var f=new Fixture();var actions=Assert.IsType<Dictionary<string,NeedsActionDelta>>(f.Needs.Actions);var prior=actions["RollACube"];
        actions["RollACube"]=prior with{FreeplaySparksRewardWeight=BitConverter.Int32BitsToSingle(bits)};
        f.Needs.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Full;f.Engine.ContextRandom.WordSourceForTest=()=>0;
        Assert.True(f.Needs.RegisterNeedsActionCompleted("RollACube"));Assert.True(f.Needs.TryGetNativePending(out bool pending));Assert.Equal(expectPending,pending);
    }
    // M1-034, M15-093: 0x00699406..1A leaves the output empty; completion and the gate are the invalid read at 0x0068E7A4. reach: unit, supplied config.
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
    // M15-093 0x00696BCA sends 67, then 0x00696BCE/D2 reload stars and max before the signed compare. reach: unit.
    [Fact]
    public void StarUnlockedCallbackMutationIsRereadBeforeCompletion()
    {
        using var f=new Fixture();DateTime old=new(2025,1,1,12,0,0,DateTimeKind.Unspecified);f.Needs.State.NativeLastStarMicros=new DateTimeOffset(old,TimeZoneInfo.Local.GetUtcOffset(old)).ToUnixTimeSeconds()*1_000_000;
        f.Needs.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Normal;f.Needs.State.StoreLevel(NeedId.Play,1f);int completed=0;
        using var star=f.Engine.AnimationMessages.SubscribeMessage(0x67,true,_=>f.Needs.State.NativeStarsAwarded=0);
        using var level=f.Engine.AnimationMessages.SubscribeMessage(0x66,true,_=>completed++);
        Assert.Equal(1,f.Needs.UpdateNativeStars(false));Assert.Equal(0,completed);Assert.Equal(0,f.Needs.State.NativeStarsAwarded);Assert.Equal(0,f.Needs.State.NativeUnlockLevel);
    }

    // M15-093 0x00696AD6 cbz prior, 0x00696AEA cmp force: prior 0 with force skips GetNeedBracket 0x00696ADE, whose helper
    // 0x0069CB20 refreshes the bracket cache (0x0069C12C..0x0069C1FE). A nonzero prior refreshes even when forced.
    // reach: unit, a direct UpdateStarsState call; the production caller passes force=false.
    [Fact]
    public void ForcedCheckWithPriorZeroLeavesBracketCacheDirtyButNonzeroPriorRefreshes()
    {
        using var f=new Fixture();var n=f.Needs;n.State.NativeLastStarMicros=f.Now;
        n.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Full;n.State.StoreLevel(NeedId.Play,.7f);Assert.True(f.BracketsDirty);
        Assert.Equal(0,n.UpdateNativeStars(true));Assert.True(f.BracketsDirty);
        n.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Normal;
        Assert.Equal(0,n.UpdateNativeStars(true));Assert.False(f.BracketsDirty);
    }
    // M15-093 0x00699416/0x00699480: the passed index selects all five star scalars. Level 0 gives max(vcvt(100*0.9+0.5),100)=100,
    // level 1 would give max(vcvt(1000*0.9+0.5),500)=900; word 0 draws the lower bound. reach: unit, supplied config.
    [Fact]
    public void StarSparksScalarsComeFromThePassedLevelNotTheLiveLevel()
    {
        using var f=new Fixture();f.Set("_nativeStarThresholds",Config(Level(Sparks),Level(Sparks,target:1000,minimum:500,maximum:600)));
        f.Needs.State.NativeUnlockLevel=1;f.Engine.ContextRandom.WordSourceForTest=()=>0;int counter=0;var output=new List<NativeNeedsReward>();
        Assert.True(f.Needs.ProcessNativeLevel(f.Engine,0,false,ref counter,output));
        Assert.Equal("100",Encoding.ASCII.GetString(Assert.Single(output).Data));Assert.Equal(100,f.Owner.Inventory!.GetAmount(1));
    }
    // M15-093 0x006994F2..0x006994FA parses and skips id zero before 0x006994FC reads Progression; later rewards still run.
    // reach: unit, Progression removed to observe the order. The name "Invalid" is UnlockId 0.
    [Fact]
    public void IdZeroUnlockSkipsBeforeProgressionIsRead()
    {
        using var f=new Fixture();f.Set("_nativeStarThresholds",Config(Level("{\"rewardType\":\"Unlock\",\"data\":\"Invalid\"},"+Badge+","+FistBump)));
        Assert.Equal(0u,NativeUnlockNames.FromBytes("Invalid"u8.ToArray()));var progression=f.Owner.Progression;f.Owner.Progression=null;
        try
        {
            int counter=0;var output=new List<NativeNeedsReward>();
            Assert.False(f.Needs.ProcessNativeLevel(f.Engine,0,false,ref counter,output)); // stops only at the real FistBump
            Assert.Equal(3,Assert.Single(output).Type);
        }
        finally{f.Owner.Progression=progression;}
    }
    // M15-093, the unknown RNG rule of the handoff: the Unlock before the first draw (0x00697174) stays applied; nothing after it runs.
    // reach: production entry RegisterNeedsActionCompleted; the unknown history is supplied through MarkHistoryUnavailable.
    [Fact]
    public void UnknownRngStopsDailyLevelAtFirstDrawAndKeepsTheKnownPrefix()
    {
        using var f=new Fixture();f.Set("_nativeStarThresholds",Config(Level(string.Join(",",FistBump,Sparks,Song))));
        f.LastStarYearsAgo();f.PlayEntersFull();f.Engine.NativeLocalNotifications!.MarkHistoryUnavailable();
        // The action's own ApplyDelta draws (0x006968DE) come first; the log line marks the end of them.
        int words=0,afterDeltas=-1;f.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};f.Needs.Log+=line=>{if(line.StartsWith("needs.action_completed"))afterDeltas=words;};int star=0,completed=0;
        using var s=f.Engine.AnimationMessages.SubscribeMessage(0x67,true,_=>star++);using var c=f.Engine.AnimationMessages.SubscribeMessage(0x66,true,_=>completed++);
        Assert.False(f.Needs.RegisterNeedsActionCompleted("RollACube"));
        Assert.Equal(1,star);Assert.Equal(0,completed);Assert.Equal(afterDeltas,words);Assert.Equal(1,f.Needs.State.NativeStarsAwarded);Assert.Equal(0,f.Needs.State.NativeUnlockLevel);
        Assert.Contains(35u,f.Owner.Progression!.Unlocked);Assert.Equal(0,f.Owner.Inventory!.GetAmount(1));Assert.NotEqual((int)NativeUnlockNames.FromBytes("Singing_PopGoesTheWeasel"u8.ToArray()),f.Needs.State.ForceNextSong);
        Assert.False(f.Needs.TryGetNativePending(out _)); // Section 2: the unknown daily result withdraws the pending proof
        Assert.Null(f.Needs.UpdateNativeStars(true));Assert.Equal(1,star);
    }
    // M15-071 0x00696DE4: the chance draw is the first draw of the freeplay gate. Action deltas stay applied, no word is consumed and the
    // proof is withdrawn; a zero-weight action never reaches the draw (0x0069638A..A4) and keeps it. reach: production entry, supplied unknown history.
    [Fact]
    public void UnknownRngStopsFreeplayGateAtItsDrawAndZeroWeightKeepsTheProof()
    {
        using var f=new Fixture();f.Needs.State.NativePreviousBrackets[NeedId.Play]=NeedBracketId.Full;f.Engine.NativeLocalNotifications!.MarkHistoryUnavailable();
        int words=0,afterDeltas=-1;f.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};f.Needs.Log+=line=>{if(line.StartsWith("needs.action_completed"))afterDeltas=words;};
        Assert.Equal(0f,(float)Assert.IsType<Dictionary<string,NeedsActionDelta>>(f.Needs.Actions)["SeeFace"].FreeplaySparksRewardWeight);
        Assert.True(f.Needs.RegisterNeedsActionCompleted("SeeFace"));Assert.True(f.Needs.TryGetNativePending(out bool pending));Assert.False(pending);Assert.Equal(afterDeltas,words);
        Assert.True(f.Needs.RegisterNeedsActionCompleted("RollACube"));
        Assert.Equal(afterDeltas,words);Assert.Equal(0,f.Owner.Inventory!.GetAmount(1));Assert.False(f.Needs.TryGetNativePending(out _));Assert.False(f.Needs.NativeRewardPayloadKnownForTest);
    }
    // M1-034 0x0068E7A4: an empty level vector is the original's invalid read, skipped with the prior state kept (false, proof kept).
    // A config that never loaded is an unknown input, not that fault (null, proof withdrawn). reach: production entry, supplied config.
    [Fact]
    public void EmptyConfigIsAKnownSkipButUnavailableConfigIsUnknown()
    {
        using var f=new Fixture();f.Set("_nativeStarThresholds",Config());
        Assert.False(f.Needs.TryNativeFreeplayReward(f.Engine,NeedsActionId.RollACube,true));
        Assert.True(f.Needs.RegisterNeedsActionCompleted("RollACube"));Assert.True(f.Needs.NativePendingKnownFalse);
        f.Set("_nativeStarThresholds",null);
        Assert.Null(f.Needs.TryNativeFreeplayReward(f.Engine,NeedsActionId.RollACube,true));
        int counter=0;Assert.False(f.Needs.ProcessNativeLevel(f.Engine,0,false,ref counter,new List<NativeNeedsReward>()));
        Assert.True(f.Needs.RegisterNeedsActionCompleted("RollACube"));Assert.False(f.Needs.TryGetNativePending(out _));
    }
    // M15-093 0x00696BCA 67, 0x00696BD8 completion (66), 0x00696BDE..E2 forced write, 0x00696BEA Generate, with no result test
    // between the last two. reach: production entry; the NV owner is removed to make the writer return false.
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void NewDayOrderIsReportsThenForcedWriteThenGenerateWhateverTheWriterReturns(bool nvPresent)
    {
        using var f=new Fixture();f.LastStarYearsAgo();f.PlayEntersFull();f.Engine.ContextRandom.WordSourceForTest=()=>0;var order=new List<string>();
        using var s=f.Engine.AnimationMessages.SubscribeMessage(0x67,true,_=>order.Add("67"));using var c=f.Engine.AnimationMessages.SubscribeMessage(0x66,true,_=>order.Add("66"));
        var nv=f.Engine.NvStorage!;var inner=f.Needs.TryLocalNotificationsGenerate!;
        f.Needs.TryLocalNotificationsGenerate=()=>{order.Add(nv.QueuedTags.Contains(NeedsManager.NeedsNvKey)||nv.InFlightTag==NeedsManager.NeedsNvKey?"generate-after-write":"generate");return inner();};
        if(!nvPresent)f.Needs.NvStorage=null;
        Assert.Equal(1,f.Needs.UpdateNativeStars(false));
        Assert.Equal(new[]{"67","66",nvPresent?"generate-after-write":"generate"},order);
    }
    // M15-093 0x00698762..0x0069879C: the counter is tested between prior levels; the current level always runs.
    // Prior level 0 grants FistBump (counter 1->0), so prior level 1 is not visited and the current level 2 still is. reach: production-shaped day change, supplied config.
    [Fact]
    public void CompletionCounterStopsLaterPriorLevelsButNotTheCurrentLevel()
    {
        using var f=new Fixture();f.Set("_nativeStarThresholds",Config(Level(FistBump),Level(Song),Level(Badge),Level(Sparks)));
        f.Needs.State.NativeUnlockLevel=2;f.Needs.State.NativeStarsAwarded=0;f.Needs.State.NativeStarsForNextUnlock=1;f.LastStarYearsAgo();f.PlayEntersFull();
        StarLevelCompletedReport? report=null;using var c=f.Engine.AnimationMessages.SubscribeMessage(0x66,true,p=>report=Assert.IsType<StarLevelCompletedReport>(p));
        Assert.Equal(1,f.Needs.UpdateNativeStars(false));
        Assert.Equal(new[]{1,3},report!.Rewards.Select(r=>r.Type));Assert.Equal(3,report.Level);
        Assert.Contains(35u,f.Owner.Progression!.Unlocked);Assert.NotEqual((int)NativeUnlockNames.FromBytes("Singing_PopGoesTheWeasel"u8.ToArray()),f.Needs.State.ForceNextSong);
    }
    // Manager decision 2026-11-02: only shipped complete object levels are built; anything else stops visibly rather than filling a value.
    [Theory]
    [InlineData("{\"unlockLevels\":[{\"numStarsToUnlock\":1}]}")]
    [InlineData("{\"unlockLevels\":[null]}")]
    public void LevelOutsideTheShippedShapeLeavesConfigUnavailable(string text)=>Assert.Null(NativeStarThresholds.Parse(text));
}
