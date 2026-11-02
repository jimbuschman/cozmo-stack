using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using System.Runtime.InteropServices;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class NativeNotificationTests
{
    [DllImport("ucrtbase.dll",EntryPoint="_putenv_s",CallingConvention=CallingConvention.Cdecl)] private static extern int WindowsPutEnv(string name,string value);
    [DllImport("ucrtbase.dll",EntryPoint="_tzset",CallingConvention=CallingConvention.Cdecl)] private static extern void WindowsTzSet();
    [DllImport("libc",EntryPoint="setenv",CallingConvention=CallingConvention.Cdecl)] private static extern int UnixSetEnv(string name,string value,int overwrite);
    [DllImport("libc",EntryPoint="unsetenv",CallingConvention=CallingConvention.Cdecl)] private static extern int UnixUnsetEnv(string name);
    [DllImport("libc",EntryPoint="tzset",CallingConvention=CallingConvention.Cdecl)] private static extern void UnixTzSet();
    private static void CalendarZone(string? zone)
    {
        if(OperatingSystem.IsWindows()){Assert.Equal(0,WindowsPutEnv("TZ",zone??""));WindowsTzSet();}
        else if(OperatingSystem.IsLinux()){Assert.Equal(0,zone is null?UnixUnsetEnv("TZ"):UnixSetEnv("TZ",zone,1));UnixTzSet();}
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly BackupTestResources Files=new();
        internal readonly CozmoRobot Robot;
        internal CozmoEngine Engine=>Robot.Engine;
        internal LocalNotifications Owner=>Assert.IsType<LocalNotifications>(Engine.NativeLocalNotifications);
        internal Fixture(NotificationDeterminants? history=null,string? config=null,Func<long>? clock=null,bool supplyNeeds=true,bool supplyDecay=true)
        {
            var options=Files.Options(new CozmoEngineOptions{NotificationHistory=history,NeedsSystemClockMicroseconds=clock,DasDeviceId="device",LabLocaleLanguage="en",LabLocaleCountry="us"});
            string source=Path.GetDirectoryName(BackupTestResources.ShippedConfig())!;
            foreach(string name in new[]{"needs_config.json","needs_decay_config.json","needs_action_config.json","needs_level_config.json","needs_handlers_config.json","local_notification_config.json"})
                if((supplyNeeds||name!="needs_config.json")&&(supplyDecay||name!="needs_decay_config.json"))File.Copy(Path.Combine(source,name),Path.Combine(options.ResourcesPath!,"config","engine",name),true);
            File.Copy(Path.Combine(source,"..","features.json"),Path.Combine(options.ResourcesPath!,"config","features.json"),true);
            if(config is not null)File.WriteAllText(Path.Combine(options.ResourcesPath!,"config","engine","local_notification_config.json"),config);
            Robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,options);
        }
        public void Dispose(){Robot.Dispose();Files.Dispose();}
    }

    [Fact]
    public void D5UsesSourceOrderDuplicatesAndLiteralWireWithoutHistory()
    {
        const string input="{\"localNotificationConfigs\":[{\"textKeys\":[\"a\",\"b\"],\"titleKey\":\"T\"},{\"textKeys\":[\"a\"],\"titleKey\":\"\"}]}";
        using var f=new Fixture(config:input);var bodies=new List<byte[]>();f.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x99)bodies.Add(body.ToArray());};
        Assert.Equal(1,f.Engine.AnimationMessages.SubscriberCountForTest(0xD4,false));Assert.Equal(1,f.Engine.AnimationMessages.SubscriberCountForTest(0xD5,false));Assert.Equal(1,f.Engine.AnimationMessages.SubscriberCountForTest(0xD6,false));
        f.Engine.RequestNotificationTextKeys();f.Engine.Tick();f.Engine.DrainAppDelivery();
        Assert.Equal("030001610162016101000154",Convert.ToHexString(Assert.Single(bodies)));
    }
    [Fact]
    public void InitializationDeadlineUsesSuppliedEngineTime()
    {
        using var f=new Fixture();using var robot=CozmoRobot.CreateOffline(engineOptions:f.Engine.Options);
        var needs=NeedsManager.FromObb(f.Engine.Options.ResourcesPath,()=>20.25);robot.Engine.UseNeedsForOfflineTest(needs);
        robot.Engine.BindNativeNotifications(needs);
        Assert.Equal(20.25f,needs.NativeInitializationTime);Assert.Equal(80.25f,Assert.IsType<LocalNotifications>(robot.Engine.NativeLocalNotifications).Deadline);
    }
    [Fact]
    public void HostCalendarUsesNonUtcStandardTimeAndSourceHourAdjustment()
    {
        if(!OperatingSystem.IsWindows()&&!OperatingSystem.IsLinux())return;
        string? previous=Environment.GetEnvironmentVariable("TZ");
        try
        {
            CalendarZone("EST5EDT");
            Assert.True(NativeNotificationCalendar.TryMktime(new DateTime(2018,1,15,12,0,0),out int winter));Assert.Equal(1516032000,winter);
            Assert.True(NativeNotificationCalendar.TryMktime(new DateTime(2018,7,15,12,0,0),out int summer));Assert.Equal(1531670400,summer);
            Assert.True(NativeNotificationCalendar.TryLocalMinute(summer,out float minute));Assert.Equal(720f,minute);
        }
        finally{CalendarZone(previous);}
    }
    [Fact]
    public void CacheReportMatchesIndependentNativeWireLiteral()
    {
        const string config="{\"localNotificationConfigs\":[{\"notificationMainUnion\":{\"type\":\"notificationGeneral\"},\"connection\":\"Either\",\"whenType\":\"AfterAppClose\",\"whenParam\":1,\"rangeEarly\":0,\"rangeLate\":0,\"minimumDuration\":0,\"noEarlierThan\":0,\"noLaterThan\":1440,\"titleKey\":\"T\",\"textKeys\":[\"ab\"]}]}";
        using var f=new Fixture(config:config,clock:()=>1_700_000_000_000_000);var bodies=new List<byte[]>();f.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x98)bodies.Add(body.ToArray());};
        Assert.True(f.Owner.TryGenerate());f.Engine.DrainAppDelivery();Assert.Equal("3C0000000154026162000000",Convert.ToHexString(Assert.Single(bodies)));
    }
    [Fact]
    public void D6UsesLiteral433BytesEarliestTieAndDoesNotGenerate()
    {
        int[] times=new int[85];times[14]=times[36]=times[84]=0x01020304;bool[] played=new bool[85];
        var input=new NotificationDeterminants(0x01020304,times,played);
        const string literal="04030201550000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000403020100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000004030201000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000004030201550000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";
        Assert.Equal(433,NotificationDeterminantCodec.Pack(input).Length);Assert.Equal(literal,Convert.ToHexString(NotificationDeterminantCodec.Pack(input)));
        using var f=new Fixture();int clear=0;using var sub=f.Engine.AnimationMessages.SubscribeMessage(0x97,true,_=>clear++);
        f.Engine.AnimationMessages.Deliver(0xD6,false,input);Assert.Equal(14,f.Owner.LatestId);Assert.Equal(0x01020304,f.Owner.LatestTime);Assert.False(f.Owner.AnyMinigamePlayed);Assert.Equal(0,clear);
        played[17]=true;f.Engine.AnimationMessages.Deliver(0xD6,false,new NotificationDeterminants(0,times,played));Assert.True(f.Owner.AnyMinigamePlayed);Assert.Equal(0,clear);
    }
    [Fact]
    public void ShippedMixedScenarioUsesElevenCachesAndTwentyTwoSharedRngDraws()
    {
        DateTime local=new(2026,1,15,12,0,0,DateTimeKind.Unspecified);long now=new DateTimeOffset(local,TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds();
        Assert.True(NativeNotificationCalendar.TryLocalMinute(now,out float localMinute));Assert.Equal(720f,localMinute);
        int[] stamps=new int[85];stamps[35]=(int)now;var input=new NotificationDeterminants((int)now,stamps,new bool[85]);
        using var f=new Fixture(input,clock:()=>now*1_000_000L);Assert.Equal(44,f.Owner.ItemCount);
        NotificationTextKeysReport? keys=null;using var keySub=f.Engine.AnimationMessages.SubscribeMessage(0x99,true,p=>keys=Assert.IsType<NotificationTextKeysReport>(p));
        f.Engine.AnimationMessages.Deliver(0xD5,false,null);Assert.NotNull(keys);Assert.Equal(57,keys!.TextKeys.Count);Assert.Equal(25,keys.TitleKeys.Count);
        f.Engine.ContextRandom.SetSeed(5489);var tags=new List<int>();var reports=new List<NotificationScheduleReport>();
        using var clear=f.Engine.AnimationMessages.SubscribeMessage(0x97,true,_=>tags.Add(0x97));
        using var cache=f.Engine.AnimationMessages.SubscribeMessage(0x98,true,p=>{tags.Add(0x98);reports.Add(Assert.IsType<NotificationScheduleReport>(p));});
        Assert.True(f.Owner.TryGenerate());Assert.Equal(12,tags.Count);Assert.Equal(0x97,tags[0]);Assert.All(tags.Skip(1),x=>Assert.Equal(0x98,x));
        Assert.Equal(new[]{86400,259200,432000,604800,864000,1209600,1555200,1814400,2419200,604800,259200},reports.Select(r=>r.Seconds));
        Assert.Equal(new[]{"LocalNotif.CRMOnboarding.Unlocks.Body.01","LocalNotif.CRMOnboarding.PlayGame.Body.01","LocalNotif.CRMOnboarding.Sparks.Body.01","LocalNotif.CRMOnboarding.Explorer.Body.01","LocalNotif.CRMOnboarding.CozmoPerforms.Body.01","LocalNotif.CRMOnboarding.Freetime.Body.01","LocalNotif.CRMOnboarding.FistBump.Body.01","LocalNotif.CRMOnboarding.MeetCozmo.Body.01","LocalNotif.CRMOnboarding.CodeLab.Body.01","LocalNotif.LastUnlock.FistBump.Body.01","LocalNotif.TrickPlay.FistBump.Body.01"},reports.Select(r=>r.Text));
        Assert.All(reports,r=>{Assert.False(r.Persist);Assert.Equal("",r.Deeplink);Assert.Equal("",r.Url);});
        Assert.Equal((3646982597d+910208076d*4294967296d)/18446744073709551616d,f.Engine.ContextRandom.GetNextDbl());
    }
    [Fact]
    public void MissingDeterminantKeepsClearPrefixAndDoesNotRecoverLostRandomHistoryOnD6()
    {
        DateTime local=new(2026,1,15,12,0,0,DateTimeKind.Unspecified);long now=new DateTimeOffset(local,TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds();
        using var f=new Fixture(clock:()=>now*1_000_000L);int clear=0;using var sub=f.Engine.AnimationMessages.SubscribeMessage(0x97,true,_=>clear++);
        Assert.False(f.Owner.TryGenerate());Assert.Equal(1,clear);Assert.False(f.Owner.RandomHistoryKnown);
        f.Engine.AnimationMessages.Deliver(0xD6,false,new NotificationDeterminants((int)now,new int[85],new bool[85]));
        Assert.False(f.Owner.TryGenerate());Assert.Equal(2,clear);Assert.False(f.Owner.RandomHistoryKnown);
    }
    [Fact]
    public void MissingActualNeedsInputsStopAfterClearBeforeRandomDraw()
    {
        using var f=new Fixture(supplyNeeds:false,supplyDecay:false);int clear=0,words=0;f.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        using var sub=f.Engine.AnimationMessages.SubscribeMessage(0x97,true,_=>clear++);
        Assert.False(f.Owner.TryGenerate());Assert.Equal(1,clear);Assert.Equal(0,words);Assert.False(f.Owner.RandomHistoryKnown);
    }
    [Fact]
    public void UpdateUsesStrictDeadlineAndD4AndBothPauseInputsGenerate()
    {
        using var f=new Fixture(config:"{\"localNotificationConfigs\":[]}");int clear=0;
        using var sub=f.Engine.AnimationMessages.SubscribeMessage(0x97,true,_=>clear++);
        f.Owner.Update(60f);Assert.Equal(0,clear);f.Owner.Update(60.01f);Assert.Equal(1,clear);
        f.Owner.SetPaused(false);f.Owner.SetPaused(true);f.Engine.AnimationMessages.Deliver(0xD4,false,null);Assert.Equal(4,clear);
    }
    [Fact]
    public void ContextRetirementDuringClearStopsOriginalGeneration()
    {
        using var f=new Fixture();int words=0;f.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        using var sub=f.Engine.AnimationMessages.SubscribeMessage(0x97,true,_=>f.Engine.Dispose());
        Assert.False(f.Owner.TryGenerate());Assert.Equal(0,words);
    }
    [Fact]
    public void MissingDecayStillKeepsEarlierBracketRefresh()
    {
        using var f=new Fixture(supplyDecay:false);var state=f.Engine.Needs.State;state.StoreLevel(NeedId.Play,.1f);
        var dirty=typeof(NeedsState).GetField("_bracketsDirty",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        Assert.True((bool)dirty.GetValue(state)!);Assert.False(f.Owner.TryGenerate());Assert.False((bool)dirty.GetValue(state)!);
    }
    [Fact]
    public void AfterAppCloseConsumesTimingDrawBeforeMissingMinimum()
    {
        const string config="{\"localNotificationConfigs\":[{\"notificationMainUnion\":{\"type\":\"notificationGeneral\"},\"connection\":\"Either\",\"whenType\":\"AfterAppClose\",\"whenParam\":0,\"rangeEarly\":0,\"rangeLate\":0,\"textKeys\":[\"a\"]}]}";
        using var f=new Fixture(config:config,clock:()=>1_700_000_000_000_000);int words=0,clear=0,cache=0;f.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        using var c=f.Engine.AnimationMessages.SubscribeMessage(0x97,true,_=>clear++);using var n=f.Engine.AnimationMessages.SubscribeMessage(0x98,true,_=>cache++);
        Assert.False(f.Owner.TryGenerate());Assert.Equal(2,words);Assert.Equal(1,clear);Assert.Equal(0,cache);Assert.False(f.Owner.RandomHistoryKnown);
    }
    [Fact]
    public void EarlierWindowBranchDoesNotReadUnavailableUpperWindow()
    {
        const string config="{\"localNotificationConfigs\":[{\"notificationMainUnion\":{\"type\":\"notificationGeneral\"},\"connection\":\"Either\",\"whenType\":\"AfterAppClose\",\"whenParam\":0,\"rangeEarly\":0,\"rangeLate\":0,\"minimumDuration\":0,\"noEarlierThan\":1300,\"textKeys\":[\"a\"]}]}";
        DateTime local=new(2026,1,15,12,0,0,DateTimeKind.Unspecified);long now=new DateTimeOffset(local,TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds();
        using var f=new Fixture(config:config,clock:()=>now*1_000_000L);NotificationScheduleReport? report=null;using var sub=f.Engine.AnimationMessages.SubscribeMessage(0x98,true,p=>report=Assert.IsType<NotificationScheduleReport>(p));
        Assert.True(f.Owner.TryGenerate());Assert.NotNull(report);Assert.Equal(34800,report!.Seconds);
    }
    [Fact]
    public void SourcePredicateBoundariesUseStrictDeadlineAndPlayedState()
    {
        const string config="{\"localNotificationConfigs\":[{\"connection\":\"Either\",\"whenType\":\"AfterUnlock\",\"whenParam\":1,\"notificationMainUnion\":{\"type\":\"notificationTrickBased\",\"trickUnlockId\":\"FistBump\",\"comparisonType\":\"LastUnlock\"}},{\"connection\":\"Either\",\"whenType\":\"AfterUnlock\",\"whenParam\":1,\"notificationMainUnion\":{\"type\":\"notificationTrickBased\",\"trickUnlockId\":\"FistBump\",\"comparisonType\":\"NotPlayed\"}},{\"connection\":\"Either\",\"whenType\":\"AfterAppClose\",\"notificationMainUnion\":{\"type\":\"notificationPostOnboarding\",\"numDaysStartAfter\":30}}]}";
        long now=60;int[] times=new int[85];times[35]=1;bool[] played=new bool[85];
        using var f=new Fixture(new NotificationDeterminants(100,times,played),config,()=>now*1_000_000);
        Assert.True(f.Owner.TryEvaluateForTest(0,out bool eligible));Assert.True(eligible);
        now=61;Assert.True(f.Owner.TryEvaluateForTest(0,out eligible));Assert.False(eligible);
        played[35]=true;f.Engine.AnimationMessages.Deliver(0xD6,false,new NotificationDeterminants(100,times,played));Assert.True(f.Owner.TryEvaluateForTest(1,out eligible));Assert.False(eligible);
        now=2_592_100;Assert.True(f.Owner.TryEvaluateForTest(2,out eligible));Assert.False(eligible);
        now++;Assert.True(f.Owner.TryEvaluateForTest(2,out eligible));Assert.True(eligible);
    }
    [Fact]
    public void NotificationConnectionByteRemainsSetAfterDisconnect()
    {
        const string config="{\"localNotificationConfigs\":[{\"connection\":\"DidConnect\",\"whenType\":\"AfterAppClose\",\"notificationMainUnion\":{\"type\":\"notificationGeneral\"}},{\"connection\":\"DidNotConnect\",\"whenType\":\"AfterAppClose\",\"notificationMainUnion\":{\"type\":\"notificationGeneral\"}}]}";
        using var f=new Fixture(config:config);Assert.True(f.Owner.TryEvaluateForTest(0,out bool eligible));Assert.False(eligible);
        Assert.True(f.Owner.TryEvaluateForTest(1,out eligible));Assert.True(eligible);
        f.Engine.Needs.InitAfterConnection();Assert.True(f.Owner.TryEvaluateForTest(0,out eligible));Assert.True(eligible);
        f.Engine.Needs.OnRobotDisconnected();Assert.False(f.Engine.Needs.Connected);
        Assert.True(f.Owner.TryEvaluateForTest(0,out eligible));Assert.True(eligible);Assert.True(f.Owner.TryEvaluateForTest(1,out eligible));Assert.False(eligible);
    }
    [Fact]
    public void RoundedOneTextIndexSkipsOnlyThatItemAndKeepsLaterDraws()
    {
        const string config="{\"localNotificationConfigs\":[{\"notificationMainUnion\":{\"type\":\"notificationGeneral\"},\"connection\":\"Either\",\"whenType\":\"AfterAppClose\",\"whenParam\":0,\"rangeEarly\":0,\"rangeLate\":0,\"minimumDuration\":0,\"noEarlierThan\":0,\"noLaterThan\":1440,\"titleKey\":\"first\",\"textKeys\":[\"a\"]},{\"notificationMainUnion\":{\"type\":\"notificationGeneral\"},\"connection\":\"Either\",\"whenType\":\"AfterAppClose\",\"whenParam\":0,\"rangeEarly\":0,\"rangeLate\":0,\"minimumDuration\":0,\"noEarlierThan\":0,\"noLaterThan\":1440,\"titleKey\":\"second\",\"textKeys\":[\"b\"]}]}";
        using var f=new Fixture(config:config,clock:()=>1_700_000_000_000_000);uint[] words={0,0,uint.MaxValue,uint.MaxValue,0,0,0,0};int next=0;
        f.Engine.ContextRandom.WordSourceForTest=()=>words[next++];var reports=new List<NotificationScheduleReport>();var diagnostics=new List<string>();
        using var sub=f.Engine.AnimationMessages.SubscribeMessage(0x98,true,p=>reports.Add(Assert.IsType<NotificationScheduleReport>(p)));SteppedBehavior.MissingReported+=diagnostics.Add;
        try
        {
            Assert.True(f.Owner.TryGenerate());Assert.Equal(8,next);Assert.Equal("second",Assert.Single(reports).Title);Assert.True(f.Owner.RandomHistoryKnown);Assert.Equal(60f,f.Owner.Deadline);
            Assert.Contains(diagnostics,text=>text.Contains("M1-034")&&text.Contains("0068CB98"));
        }
        finally{SteppedBehavior.MissingReported-=diagnostics.Add;}
    }
    [Fact]
    public void NullInterfaceSkipsOnlyReportsAndKeepsKnownDrawsAndDeadline()
    {
        const string config="{\"localNotificationConfigs\":[{\"notificationMainUnion\":{\"type\":\"notificationGeneral\"},\"connection\":\"Either\",\"whenType\":\"AfterAppClose\",\"whenParam\":1,\"rangeEarly\":0,\"rangeLate\":0,\"minimumDuration\":0,\"noEarlierThan\":0,\"noLaterThan\":1440,\"textKeys\":[\"a\"]}]}";
        using var f=new Fixture(config:config,clock:()=>1_700_000_000_000_000);f.Engine.NeedsContextExternalInterfaceAvailable=false;int words=0,reports=0;var diagnostics=new List<string>();
        f.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};using var clear=f.Engine.AnimationMessages.SubscribeMessage(0x97,true,_=>reports++);using var cache=f.Engine.AnimationMessages.SubscribeMessage(0x98,true,_=>reports++);
        SteppedBehavior.MissingReported+=diagnostics.Add;
        try
        {
            Assert.True(f.Owner.TryGenerate());Assert.Equal(4,words);Assert.Equal(0,reports);Assert.True(f.Owner.RandomHistoryKnown);Assert.Equal(60f,f.Owner.Deadline);
            Assert.Contains(diagnostics,x=>x.Contains("M1-034")&&x.Contains("0068CAD0"));Assert.Contains(diagnostics,x=>x.Contains("M1-034")&&x.Contains("0068CC28"));
        }
        finally{SteppedBehavior.MissingReported-=diagnostics.Add;}
    }
}
