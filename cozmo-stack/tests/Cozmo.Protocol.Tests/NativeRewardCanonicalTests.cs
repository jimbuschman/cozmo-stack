using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
using Xunit.Abstractions;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeRewardCanonicalTests(ITestOutputHelper output)
{
    [Fact]
    public void ActualRestoredRewardCanBeEarnedAndFreeplayContinuesAcrossFiveMinutes()
    {
        string directory=Directory.CreateTempSubdirectory("cozmo-native-reward-").FullName;
        try
        {
            DateTime local=new(2026,1,15,12,0,0,DateTimeKind.Unspecified);long initial=new DateTimeOffset(local,TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds();long wall=initial*1_000_000;
            int[] timestamps=new int[85];timestamps[14]=timestamps[36]=timestamps[84]=(int)initial;
            var options=new CozmoEngineOptions{ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),BlockPoolPath="",NeedsDeviceDirectory=directory,NeedsSystemClockMicroseconds=()=>wall,NotificationHistory=new((int)initial,timestamps,new bool[85]),DasDeviceId="source-fixture",LabLocaleLanguage="en",LabLocaleCountry="us"};
            File.WriteAllText(Path.Combine(directory,"needsState.json"),$"{{\"_StateFileVersion\":5,\"_DateTime\":{initial},\"TimeCreated\":{initial},\"TimeLastDisconnect\":0,\"TimeLastAppBackgrounded\":0,\"OpenAppAfterDisconnect\":0,\"_SerialNumber\":2,\"CurNeedsUnlockLevel\":0,\"NumStarsAwarded\":0,\"NumStarsForNextUnlock\":1,\"CurNeedLevel\":{{\"Repair\":100000,\"Energy\":100000,\"Play\":100000}},\"PartIsDamaged\":{{\"Head\":false,\"Lift\":false,\"Treads\":false}},\"TimeLastStarAwarded\":{initial},\"ForceNextSong\":\"Invalid\"}}");
            using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,engineOptions:options,sourceNvEmpty:true);
            var engine=f.Rig.Engine;var needs=engine.Needs;var manager=f.Robot.BehaviorManagerOwner!;
            Assert.True(needs.HasNativePersistence);Assert.True(engine.Experiments.NativeAssignmentHistoryAvailable);Assert.True(engine.NativeLocalNotifications!.RandomHistoryKnown);Assert.True(f.Robot.Inventory!.Ready);
            engine.ContextRandom.SetSeed(5489);double start=engine.Timer.Seconds;int mark=f.Rig.Mark();byte active=0;uint camera=1;double nextCamera=start+1;
            bool sent=false,granted=false,earned=false,continued=false;var reports=new List<FreeplaySparksAwardedReport>();
            using var reward=engine.AnimationMessages.SubscribeMessage(0x65,true,p=>reports.Add(Assert.IsType<FreeplaySparksAwardedReport>(p)));
            var prior=manager.DasTransition;manager.DasTransition=next=>{prior?.Invoke(next);if(next.Id=="EarnedSparks")earned=true;};
            try
            {
                f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
                while(engine.Timer.Seconds-start<300)
                {
                    f.Motors();double elapsed=engine.Timer.Seconds-start;wall=initial*1_000_000+(long)(elapsed*1_000_000);
                    if(engine.Timer.Seconds>=nextCamera){f.NativeCameraFrame(camera,(camera&1)==0?(byte)200:(byte)100);camera++;nextCamera+=1;}
                    foreach(var message in f.Rig.SentSince(mark))
                    {
                        if(message is StartOfAnimation begin){active=begin.AnimId;f.Rig.Data(new AnimationStarted{AnimId=active});}
                        else if(message is EndOfAnimation)f.Rig.Data(new AnimationEnded{AnimId=active});
                    }
                    mark=f.Rig.Mark();
                    if(!sent&&elapsed>=30)
                    {
                        sent=true;Assert.True(needs.RegisterNeedsActionCompleted("RollACube"));
                        var value=needs.NativeRewardSnapshotForTest();granted=value.Known&&value.Pending&&value.Key=="needs.FreeplaySparksReward.RollACube";
                    }
                    if(sent&&elapsed>45&&manager.Current is not null)continued=true;
                }
                output.WriteLine($"action={sent} granted={granted} earned={earned} reports={reports.Count} continued={continued} frames={camera-1} pending={needs.NativeRewardSnapshotForTest()}");
                Assert.True(sent);Assert.True(granted,"The actual source gate did not grant in the supplied profile/seed scenario");Assert.True(earned);Assert.Contains(reports,r=>r.DisplayKey=="needs.FreeplaySparksReward.RollACube");
                Assert.True(continued);Assert.True(camera>100);Assert.True(f.M.Vision.NativeInitializationForTest?.Motion?.Detector.ComparisonCount>0);
            }
            finally{manager.DasTransition=prior;}
        }
        finally{Directory.Delete(directory,true);}
    }
}
