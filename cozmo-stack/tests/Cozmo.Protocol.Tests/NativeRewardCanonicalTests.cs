using Cozmo.Conformance;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
using Xunit.Abstractions;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

// reach: production. The robot is built by the production factory, its NV reads come back through the wire as a
// fresh robot's would, ticks go through the engine, and the action enters through RegisterNeedsActionCompleted,
// the recipient of IBehavior::NeedActionCompleted (0x005BE40C -> 0x006960D0). Options come from HardwareEngineOptions, the
// helper m13-idle-hw uses, with the test clock and paths as the only overrides. Remaining fixture inputs: the motor, camera,
// animation and NV echoes, the needs file on the PC and the robot's stored needs blob. Nothing is reseeded after startup.
[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeRewardCanonicalTests(ITestOutputHelper output)
{
    private sealed record Run(List<(double At,string Id)> Chosen,List<string> Holds,List<FreeplaySparksAwardedReport> Sparks,List<StarUnlockedReport> Stars,List<StarLevelCompletedReport> Levels,double IdleAfterAction,double LongestIdleAfterAction,int Frames,NeedsStateOnRobot? FirstRobotWrite);

    internal static long FixedClockMicros=>InitialSeconds()*1_000_000;
    private static long InitialSeconds()
    {DateTime local=new(2026,1,15,12,0,0,DateTimeKind.Unspecified);return new DateTimeOffset(local,TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds();}
    // The host inputs production takes from its settings: the nurture directory with a needs file. Notification history is not
    // passed: the engine's own M15-111 store supplies it, as on a real robot.
    internal static CozmoEngineOptions SourceOptions(string directory,Func<long> clock,float playLevel,long lastStarAgeSeconds)
    {
        long initial=InitialSeconds();int play=(int)(playLevel*100000);
        File.WriteAllText(Path.Combine(directory,"needsState.json"),$"{{\"_StateFileVersion\":5,\"_DateTime\":{initial},\"TimeCreated\":{initial},\"TimeLastDisconnect\":0,\"TimeLastAppBackgrounded\":0,\"OpenAppAfterDisconnect\":0,\"_SerialNumber\":2,\"CurNeedsUnlockLevel\":0,\"NumStarsAwarded\":0,\"NumStarsForNextUnlock\":1,\"CurNeedLevel\":{{\"Repair\":100000,\"Energy\":100000,\"Play\":{play}}},\"PartIsDamaged\":{{\"Head\":false,\"Lift\":false,\"Treads\":false}},\"TimeLastStarAwarded\":{initial-lastStarAgeSeconds},\"ForceNextSong\":\"Invalid\"}}");
        return HardwareEngineOptions.Create(PickupPyramidNativeExecutionTests.Resources(),localeName:"en-US",filesPath:directory,needsDirectory:directory,deviceIdPath:Path.Combine(directory,"device-id"),blockPoolPath:"",clock:clock);
    }
    // What the robot has stored; written after the PC's needs file, so the resolver takes it (0x0069481C..0x00694D06).
    private static byte[] StoredBlob(float repair,float energy,float play,int level,int stars,long lastStarSeconds)
    {
        var stored=new NeedsStateOnRobot{Version=5,TimeLastWritten=(ulong)(InitialSeconds()+10),CurNeedsUnlockLevel=level,NumStarsAwarded=stars,TimeLastStarAwarded=(ulong)lastStarSeconds,TimeCreated=(ulong)InitialSeconds()};
        stored.CurNeedLevel[0]=(int)(repair*100000);stored.CurNeedLevel[1]=(int)(energy*100000);stored.CurNeedLevel[2]=(int)(play*100000);
        return NeedsStateOnRobot.Pack(stored);
    }

    private Run RunFreeplay(byte[] stored,Action<NeedsManager>? afterAction=null)
    {
        string directory=Directory.CreateTempSubdirectory("cozmo-native-reward-").FullName;
        try
        {
            long initial=InitialSeconds()+20;long wall=initial*1_000_000;
            var options=SourceOptions(directory,()=>wall,1f,0);
            using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,engineOptions:options,sourceNvEmpty:true,sourceNvStored:tag=>tag==NeedsManager.NeedsNvKey?stored:null);
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
                    f.AnswerInFlightNv();f.Motors();double elapsed=engine.Timer.Seconds-start;wall=initial*1_000_000+(long)(elapsed*1_000_000);
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
                var write=f.Rig.SentSince(0).OfType<NVCommand>().FirstOrDefault(c=>c.Tag==NeedsManager.NeedsNvKey&&c.Op==NvStorageComponent.OpWrite);
                return new(chosen,holds,sparks,stars,levels,idle,longest,(int)camera-1,write is null?null:NeedsStateOnRobot.Unpack(write.Data[NvStorageComponent.NvHeaderSize..]));
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
    // 0x0069640A..0x00696482 run. The robot's stored level 2 in needs_level_config.json gives the bounds max(vcvt(25*0.02+0.5),2)=2
    // and max(vcvt(25*0.1+0.5),3)=3 (0x00697146..0x00697172). playAlone.json:160-163 puts EarnedSparks first in the interlude
    // chooser; its StopInternal 0x005DAF7C sends tag 0x65. The first robot write (0x00696536) carries the robot's level and stars.
    [Fact]
    public void WeightedActionRewardsNaturallyAndFreeplayKeepsChoosingFor300Seconds()
    {
        long lastStar=InitialSeconds();
        var run=RunFreeplay(StoredBlob(.8f,.8f,.9f,2,1,lastStar),needs=>
        {
            var value=needs.NativeRewardSnapshotForTest();
            Assert.True(value.Known);Assert.True(value.Pending);Assert.InRange(value.Amount,2,3);Assert.Equal("needs.FreeplaySparksReward.RollACube",value.Key);
            Assert.Equal(2,needs.State.NativeUnlockLevel);Assert.Equal(1,needs.State.NativeStarsAwarded);
        });
        AssertLoopKeptChoosing(run);
        double earned=run.Chosen.First(c=>c.At>=30&&c.Id=="EarnedSparks").At;
        var report=run.Sparks[0];Assert.Equal("needs.FreeplaySparksReward.RollACube",report.DisplayKey);Assert.InRange(report.Amount,2,3);
        Assert.Contains(run.Chosen,c=>c.At>earned&&c.Id!="EarnedSparks");
        Assert.Empty(run.Stars);Assert.Empty(run.Levels); // 0x00696B5A..0x00696B6E: same local day, no star
        var first=Assert.IsType<NeedsStateOnRobot>(run.FirstRobotWrite);
        Assert.Equal(2,first.CurNeedsUnlockLevel);Assert.Equal(1,first.NumStarsAwarded);Assert.Equal((ulong)lastStar,first.TimeLastStarAwarded);
        Assert.InRange(first.CurNeedLevel[0],70000,85000); // near the stored 0.8, not the default 1.0
    }

    // M15-093: Play enters Full (needs_config.json BracketLevelPlayFull 0.99) on a new day, 0x00696BBA..0x00696BE2. The stored
    // star takes level 2 to 2 of 1, so it completes. maxPriorLevelUnlocks 1 admits prior level 0, whose FistBump and song are not
    // default unlocks; Sparks is skipped there. Level 2 gives max(vcvt(100*0.9+0.5),100)=100, max(vcvt(100*1.1+0.5),120)=120.
    // Return 1 skips the weighted branch at 0x00696374. The forced write 0x00696BE2 carries the updated level.
    [Fact]
    public void DailyStarSendsBothReportsWritesTheRobotAndFreeplayKeepsChoosing()
    {
        var run=RunFreeplay(StoredBlob(.8f,.8f,.9f,2,1,InitialSeconds()-2*86400),needs=>
        {
            Assert.True(needs.TryGetNativePending(out bool pending));Assert.False(pending);
            Assert.Equal(3,needs.State.NativeUnlockLevel);Assert.Equal(0,needs.State.NativeStarsAwarded);
        });
        AssertLoopKeptChoosing(run);
        Assert.Equal(new StarUnlockedReport(2,1,2),Assert.Single(run.Stars));
        var level=Assert.Single(run.Levels);Assert.Equal(3,level.Level);Assert.Equal(1,level.Maximum);
        Assert.Equal(new[]{1,2,0,1,2},level.Rewards.Select(r=>r.Type));
        Assert.Equal("FistBump",level.Rewards[0].Data);Assert.InRange(int.Parse(level.Rewards[2].Data),100,120);Assert.Equal("PopAWheelieAction",level.Rewards[3].Data);Assert.Equal("Singing_TakeMeOutToTheBallgame",level.Rewards[4].Data);
        var first=Assert.IsType<NeedsStateOnRobot>(run.FirstRobotWrite);
        Assert.Equal(3,first.CurNeedsUnlockLevel);Assert.Equal(0,first.NumStarsAwarded);Assert.True(first.TimeLastStarAwarded>=(ulong)InitialSeconds());
        Assert.InRange(first.CurNeedLevel[0],70000,85000);
    }
}
