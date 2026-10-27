using System.Text;
using System.Collections.Concurrent;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class DriveOffPilotDrivingMoodTests
{
    [Theory]
    [InlineData("Sad",false,1,0x94,0x90,0x8C,0)]
    [InlineData("Sad",true,1,0x94,0x90,0x8C,0)]
    [InlineData("Happy",false,0,0x95,0x91,0x8D,1)]
    [InlineData("Happy",true,0,0x96,0x92,0x8E,1)]
    [InlineData("Default",false,2,0x95,0x91,0x8D,1)]
    [InlineData("Default",true,2,0x96,0x92,0x8E,1)]
    public void ActualStraightDispatchReadsCanonicalMoodBeforeExplicitNeedsInput(string moodName,bool needsMet,int rawMood,int start,int loop,int end,int needsCalls)
    {
        // Actual engine ActionList→Straight.Init→bound driving handler→canonical
        // Mood owner. Event JSON/needs Boolean/full states are admitted fixture
        // inputs; no competing needs producer or animation completion is invented.
        string resources=Directory.CreateTempSubdirectory("pilot-driving-mood-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(resources,"emotionevents"));
            File.WriteAllText(Path.Combine(resources,"emotionevents","fixture.json"),
                "{\"emotionEvents\":[{\"name\":\"Sad\",\"emotionAffectors\":[{\"emotionType\":\"Happy\",\"value\":-0.5}]},{\"name\":\"Happy\",\"emotionAffectors\":[{\"emotionType\":\"Happy\",\"value\":0.5}]}]}",Encoding.UTF8);
            using var rig=new M4ControlTests.Rig(resources);rig.ToSynced();rig.Calibrate();rig.State();
            var owner=rig.Engine.Robot!;var mood=owner.MoodOwner!;
            if(moodName!="Default")mood.Trigger(moodName,rig.Engine.Timer.SecondsF);
            Assert.Equal(rawMood,(int)mood.GetSimpleMood());
            var handler=rig.Robot.Animations.DrivingAnimations;Assert.Null(handler.GetSimpleMoodForTest);Assert.Empty(handler.Entries);
            int reads=0;handler.AreNeedsMetForTest=()=>{reads++;return needsMet;};
            using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);
            var action=new DriveStraightAction(m,96);int mark=rig.Mark();
            try
            {
                Assert.Equal(0,owner.ActionList.QueueAction(QueueActionPosition.Now,action,0));rig.Tick(10);
                Assert.Equal(new DrivingAnimations((AnimationTrigger)start,(AnimationTrigger)loop,(AnimationTrigger)end),handler.Current);
                Assert.Equal(needsCalls,reads);Assert.Single(rig.SentSince(mark).OfType<Cozmo.Protocol.AppendPathSegmentLine>());
            }
            finally{owner.ActionList.Cancel(action.Tag);handler.AreNeedsMetForTest=null;}
        }
        finally{Directory.Delete(resources,true);}
    }

    [Theory]
    [InlineData(-1,false,false,0x96)]
    [InlineData(0,false,false,0x95)] [InlineData(1,false,false,0x95)] [InlineData(2,false,false,0x95)]
    [InlineData(0,true,false,0x95)] [InlineData(1,true,false,0x95)] [InlineData(2,true,false,0x95)]
    [InlineData(0,true,true,0x94)]
    public void ActualCanonicalNeedLevelsFeedTheSameDrivingSelection(int lowNeed,bool critical,bool sad,int start)
    {
        string resources=Directory.CreateTempSubdirectory("pilot-driving-needs-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(resources,"emotionevents"));
            File.WriteAllText(Path.Combine(resources,"emotionevents","fixture.json"),
                "{\"emotionEvents\":[{\"name\":\"Sad\",\"emotionAffectors\":[{\"emotionType\":\"Happy\",\"value\":-0.5}]}]}",Encoding.UTF8);
            using var rig=new M4ControlTests.Rig(resources);rig.ToSynced();rig.Calibrate();rig.State();
            var needs=rig.Engine.Needs;
            foreach(var need in new[]{NeedId.Repair,NeedId.Energy,NeedId.Play})
            {
                var thresholds=needs.Config.Brackets[need];
                needs.State.SetNeedLevel(need,(thresholds.Full+thresholds.Normal)/2);
            }
            if(lowNeed>=0)
            {
                var need=(NeedId)lowNeed;var thresholds=needs.Config.Brackets[need];
                needs.State.SetNeedLevel(need,critical?(thresholds.Warning+thresholds.Critical)/2:(thresholds.Normal+thresholds.Warning)/2);
                Assert.Equal(critical?NeedBracketId.Critical:NeedBracketId.Warning,needs.State.GetNeedBracket(need));
            }
            var owner=rig.Engine.Robot!;
            if(sad)owner.MoodOwner!.Trigger("Sad",rig.Engine.Timer.SecondsF);
            var handler=rig.Robot.Animations.DrivingAnimations;
            Assert.Null(handler.AreNeedsMetForTest);Assert.Null(handler.GetSimpleMoodForTest);Assert.Empty(handler.Entries);
            using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);
            var action=new DriveStraightAction(m,96);
            try
            {
                Assert.Equal(0,owner.ActionList.QueueAction(QueueActionPosition.Now,action,0));rig.Tick(10);
                Assert.Equal(new DrivingAnimations((AnimationTrigger)start,(AnimationTrigger)(start-4),(AnimationTrigger)(start-8)),handler.Current);
                Assert.Equal(lowNeed<0,needs.State.AreNeedsMet());
            }
            finally{owner.ActionList.Cancel(action.Tag);}
        }
        finally{Directory.Delete(resources,true);}
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void OverrideBypassesMissingMoodAndFailedEmptyStackSelectionPreservesPrior(bool missingOwner)
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        var owner=rig.Engine.Robot!;var mood=owner.MoodOwner;
        var handler=missingOwner?new DrivingAnimationHandler(rig.Robot):rig.Robot.Animations.DrivingAnimations;
        var missing=new ConcurrentQueue<string>();rig.Engine.StackLogLine+=missing.Enqueue;
        int reads=0;handler.AreNeedsMetForTest=()=>{reads++;return true;};
        var prior=new DrivingAnimations((AnimationTrigger)10,(AnimationTrigger)11,(AnimationTrigger)12);
        try
        {
            owner.MoodOwner=null;
            handler.Push(prior,"explicit-override");Assert.True(handler.Init(0,1,false,false));
            Assert.Equal(prior,handler.Current);Assert.Equal(0,reads); // no Mood or needs read.
            handler.Remove("explicit-override");
            Assert.False(handler.Init(0,2,false,false));Assert.Equal(prior,handler.Current);Assert.Equal(0,reads);
            Assert.Contains(missing,line=>line.Contains("M1-034/M7-026")&&line.Contains("prior triplet retained"));
        }
        finally{owner.MoodOwner=mood;handler.AreNeedsMetForTest=null;handler.ActionIsBeingDestroyed();rig.Engine.StackLogLine-=missing.Enqueue;}
    }
}
