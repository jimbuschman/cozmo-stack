using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
using Xunit.Abstractions;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

// reach: production. The robot is built by the production factory, its NV reads come back through the wire as a
// fresh robot's would, ticks go through the engine, and the action enters through RegisterNeedsActionCompleted,
// the recipient of IBehavior::NeedActionCompleted (0x005BE40C -> 0x006960D0). Fixture inputs: motor/camera/animation
// echoes and the needs file. Nothing is reseeded after startup.
[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeRewardCanonicalTests(ITestOutputHelper output)
{
    private sealed record Run(List<(double At,string Id)> Chosen,List<string> Holds,List<FreeplaySparksAwardedReport> Sparks,List<StarUnlockedReport> Stars,List<StarLevelCompletedReport> Levels,double IdleAfterAction,double LongestIdleAfterAction,int Frames,int RobotWrites);

    internal static long FixedClockMicros=>InitialSeconds()*1_000_000;
    private static long InitialSeconds()
    {DateTime local=new(2026,1,15,12,0,0,DateTimeKind.Unspecified);return new DateTimeOffset(local,TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds();}
    // The host inputs production takes from its settings: the nurture directory with a needs file. Notification history is not
    // passed: the engine's own M15-111 store supplies it, as on a real robot.
    internal static CozmoEngineOptions SourceOptions(string directory,Func<long> clock,float playLevel,long lastStarAgeSeconds)
    {
        long initial=InitialSeconds();int play=(int)(playLevel*100000);
        File.WriteAllText(Path.Combine(directory,"needsState.json"),$"{{\"_StateFileVersion\":5,\"_DateTime\":{initial},\"TimeCreated\":{initial},\"TimeLastDisconnect\":0,\"TimeLastAppBackgrounded\":0,\"OpenAppAfterDisconnect\":0,\"_SerialNumber\":2,\"CurNeedsUnlockLevel\":0,\"NumStarsAwarded\":0,\"NumStarsForNextUnlock\":1,\"CurNeedLevel\":{{\"Repair\":100000,\"Energy\":100000,\"Play\":{play}}},\"PartIsDamaged\":{{\"Head\":false,\"Lift\":false,\"Treads\":false}},\"TimeLastStarAwarded\":{initial-lastStarAgeSeconds},\"ForceNextSong\":\"Invalid\"}}");
        return new CozmoEngineOptions{ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),BlockPoolPath="",NeedsDeviceDirectory=directory,NeedsSystemClockMicroseconds=clock,DasDeviceId="source-fixture",LabLocaleLanguage="en",LabLocaleCountry="us"};
    }

    private Run RunFreeplay(float playLevel,long lastStarAgeSeconds,Action<NeedsManager>? afterAction=null)
    {
        string directory=Directory.CreateTempSubdirectory("cozmo-native-reward-").FullName;
        try
        {
            long initial=InitialSeconds();long wall=initial*1_000_000;
            var options=SourceOptions(directory,()=>wall,playLevel,lastStarAgeSeconds);
            using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,engineOptions:options,sourceNvEmpty:true);
            var engine=f.Rig.Engine;var needs=engine.Needs;var manager=f.Robot.BehaviorManagerOwner!;
            Assert.True(File.Exists(Path.Combine(directory,NotificationHistoryStore.FileName))); // M15-111 production default
            Assert.True(needs.HasNativePersistence);Assert.True(engine.Experiments.NativeAssignmentHistoryAvailable);Assert.True(engine.NativeLocalNotifications!.RandomHistoryKnown);Assert.True(f.Robot.Inventory!.Ready);
            // M15-034: 0x0064B89E reads unlock_config_nurture.json and an empty NV read installs its defaultUnlocks.
            foreach(uint id in DefaultUnlockIds())Assert.Contains(id,f.Robot.Progression!.Unlocked);
            double start=engine.Timer.Seconds,idle=0,idleRun=0,longest=0,last=start;int mark=f.Rig.Mark();byte active=0;uint camera=1;double nextCamera=start+1;bool sent=false;
            var chosen=new List<(double At,string Id)>();var holds=new List<string>();var sparks=new List<FreeplaySparksAwardedReport>();var stars=new List<StarUnlockedReport>();var levels=new List<StarLevelCompletedReport>();
            using var s65=engine.AnimationMessages.SubscribeMessage(0x65,true,p=>sparks.Add(Assert.IsType<FreeplaySparksAwardedReport>(p)));
            using var s67=engine.AnimationMessages.SubscribeMessage(0x67,true,p=>stars.Add(Assert.IsType<StarUnlockedReport>(p)));
            using var s66=engine.AnimationMessages.SubscribeMessage(0x66,true,p=>levels.Add(Assert.IsType<StarLevelCompletedReport>(p)));
            var prior=manager.DasTransition;manager.DasTransition=next=>{prior?.Invoke(next);chosen.Add((engine.Timer.Seconds-start,next.Id));};
            // A hold reports through ReportMissing: M8-013 for the chooser, M15-071 for the pending proof, M15-093 for the star machine.
            void Record(string text){if(sent&&(text.Contains("M8-013")||text.Contains("M15-071")||text.Contains("M15-093")))lock(holds)holds.Add(text);}
            SteppedBehavior.MissingReported+=Record;
            try
            {
                f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
                while(engine.Timer.Seconds-start<300)
                {
                    f.Motors();double elapsed=engine.Timer.Seconds-start;wall=initial*1_000_000+(long)(elapsed*1_000_000);
                    // Fixture scene: the alternating brightness reads as ground motion, which keeps Pounce pouncing with no end
                    // (M15-107: its timeouts are only tested with no pending point). A still scene after the action lets it get bored.
                    if(engine.Timer.Seconds>=nextCamera){f.NativeCameraFrame(camera,sent?(byte)150:(camera&1)==0?(byte)200:(byte)100);camera++;nextCamera+=1;}
                    foreach(var message in f.Rig.SentSince(mark))
                    {
                        if(message is StartOfAnimation begin){active=begin.AnimId;f.Rig.Data(new AnimationStarted{AnimId=active});}
                        else if(message is EndOfAnimation)f.Rig.Data(new AnimationEnded{AnimId=active});
                    }
                    mark=f.Rig.Mark();
                    if(!sent&&elapsed>=30)
                    {
                        SteppedBehavior.ResetMissingForTests();sent=true;
                        Assert.True(needs.RegisterNeedsActionCompleted("RollACube")); // needs_action_config.json:404-412, weight 1
                        afterAction?.Invoke(needs);
                    }
                    double now=engine.Timer.Seconds;
                    if(sent)
                    {
                        if(manager.Current is null){idle+=now-last;idleRun+=now-last;longest=Math.Max(longest,idleRun);}
                        else idleRun=0;
                    }
                    last=now;
                }
                output.WriteLine($"chosen: {string.Join(", ",chosen.Select(c=>$"{c.At:F1}:{c.Id}"))}");
                output.WriteLine($"chosen total={chosen.Count} idleAfter={idle:F1}s longestIdle={longest:F1}s frames={camera-1} sparks={sparks.Count} stars={stars.Count} levels={levels.Count} pending={needs.NativeRewardSnapshotForTest()} current={manager.Current?.Id}");
                foreach(string line in holds)output.WriteLine("missing: "+line);
                int writes=f.Rig.SentSince(0).OfType<NVCommand>().Count(c=>c.Tag==NeedsManager.NeedsNvKey&&c.Op==NvStorageComponent.OpWrite);
                return new(chosen,holds,sparks,stars,levels,idle,longest,(int)camera-1,writes);
            }
            finally{SteppedBehavior.MissingReported-=Record;manager.DasTransition=prior;}
        }
        finally{Directory.Delete(directory,true);}
    }
    private static uint[] DefaultUnlockIds()
    {
        using var document=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(PickupPyramidNativeExecutionTests.Resources(),"config","engine","unlock_config_nurture.json")),new System.Text.Json.JsonDocumentOptions{CommentHandling=System.Text.Json.JsonCommentHandling.Skip,AllowTrailingCommas=true});
        return document.RootElement.GetProperty("defaultUnlocks").EnumerateArray().Select(e=>NativeUnlockNames.FromBytes(System.Text.Encoding.UTF8.GetBytes(e.GetString()!))).ToArray();
    }
    private static void AssertLoopKeptChoosing(Run run)
    {
        Assert.Empty(run.Holds);
        // Harness bounds, not engine values: the loop must still be changing behaviour late in the run and never sit empty.
        Assert.True(run.Chosen.Count(c=>c.At>=30)>=10,"behaviours chosen after the action: "+run.Chosen.Count(c=>c.At>=30));
        Assert.True(run.Chosen[^1].At>=240,"last behaviour chosen at "+run.Chosen[^1].At);
        Assert.True(run.LongestIdleAfterAction<5,"longest idle "+run.LongestIdleAfterAction);
        Assert.True(run.Frames>=299);
    }

    // M15-071, M15-093: at zero Sparks 0x00696D74 gives chance 1, so the draw passes and the amount/pending/key stores
    // 0x0069640A..0x00696482 run. needs_level_config.json level 0 gives the bounds max(vcvt(100*0.02+0.5),2)=2 and
    // max(vcvt(100*0.1+0.5),3)=10 (0x00697146..0x00697172). playAlone.json:160-163 puts EarnedSparks first in the interlude
    // chooser; its StopInternal 0x005DAF7C sends tag 0x65.
    [Fact]
    public void WeightedActionRewardsNaturallyAndFreeplayKeepsChoosingFor300Seconds()
    {
        var run=RunFreeplay(1f,0,needs=>
        {
            var value=needs.NativeRewardSnapshotForTest();
            Assert.True(value.Known);Assert.True(value.Pending);Assert.InRange(value.Amount,2,10);Assert.Equal("needs.FreeplaySparksReward.RollACube",value.Key);
        });
        AssertLoopKeptChoosing(run);
        double earned=run.Chosen.First(c=>c.At>=30&&c.Id=="EarnedSparks").At;
        var report=run.Sparks[0];Assert.Equal("needs.FreeplaySparksReward.RollACube",report.DisplayKey);Assert.InRange(report.Amount,2,10);
        Assert.Contains(run.Chosen,c=>c.At>earned&&c.Id!="EarnedSparks");
        Assert.Empty(run.Stars);Assert.Empty(run.Levels); // 0x00696B5A..0x00696B6E: same local day, no star
    }

    // M15-093: Play enters Full (needs_config.json BracketLevelPlayFull 0.99) on a new day, 0x00696BBA..0x00696BE2. Level 0 has
    // one star and rewards Sparks/FistBump/Singing_PopGoesTheWeasel (needs_level_config.json); its Sparks bounds are
    // max(vcvt(100*0.9+0.5),100)=100 and max(vcvt(100*1.1+0.5),120)=120. Return 1 skips the weighted branch at 0x00696374.
    [Fact]
    public void DailyStarSendsBothReportsWritesTheRobotAndFreeplayKeepsChoosing()
    {
        var run=RunFreeplay(.9f,2*86400,needs=>
        {
            Assert.True(needs.TryGetNativePending(out bool pending));Assert.False(pending);
            Assert.Equal(1,needs.State.NativeUnlockLevel);Assert.Equal(0,needs.State.NativeStarsAwarded);
        });
        AssertLoopKeptChoosing(run);
        Assert.Equal(new StarUnlockedReport(0,1,1),Assert.Single(run.Stars));
        var level=Assert.Single(run.Levels);Assert.Equal(1,level.Level);Assert.Equal(1,level.Maximum);
        Assert.Equal(new[]{0,1,2},level.Rewards.Select(r=>r.Type));
        Assert.InRange(int.Parse(level.Rewards[0].Data),100,120);Assert.Equal("FistBump",level.Rewards[1].Data);Assert.Equal("Singing_PopGoesTheWeasel",level.Rewards[2].Data);
        Assert.True(run.RobotWrites>=1);
    }
}
