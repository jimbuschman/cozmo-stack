using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class LiveNarrationTests
{
    private static BehaviorTransitionReport Transition(BehaviorID old,BehaviorID next)=>new((byte)old,(byte)next,0,0,0,0,"");
    [Fact]
    public void ASelectedClipIsNotAnnouncedUntilItsOwnRobotAckAndRetainsItsOwnerAtEnd()
    {
        var lines=new List<string>();
        using var narrator=new LiveNarration(lines.Add,()=>0,automaticFlush:false);
        narrator.ObserveBehavior(47027,Transition(BehaviorID.Wait,BehaviorID.ReactToPickup));
        narrator.RegisterClip(47619,new(9,"first",3300,AnimationTrack.Audio|AnimationTrack.Head|AnimationTrack.Lift));
        narrator.RegisterClip(47700,new(11,"replacement",3000,AnimationTrack.Face));
        narrator.Flush();Assert.DoesNotContain(lines,line=>line.Contains("playing"));
        narrator.RobotStarted(48096,9);
        narrator.ObserveBehavior(49000,Transition(BehaviorID.ReactToPickup,BehaviorID.Wait));
        narrator.RobotEnded(51396,9);narrator.Flush();
        Assert.Contains("[00:48] ReactToPickup: playing first (3.3 s, sound+head+lift)",lines);
        Assert.Contains("[00:51] ReactToPickup: ended first after 3.3 s",lines);
        Assert.DoesNotContain(lines,line=>line.Contains("playing replacement"));
    }
    [Fact]
    public void TagReuseAndAbortDoNotRenameAnEarlierPlayback()
    {
        var lines=new List<string>();using var narrator=new LiveNarration(lines.Add,()=>0,automaticFlush:false);
        narrator.RegisterClip(0,new(9,"old",900,AnimationTrack.Head));
        narrator.RobotStarted(100,9);
        narrator.RegisterClip(200,new(9,"new",800,AnimationTrack.Lift));
        narrator.RobotEnded(1000,9);
        narrator.RobotStarted(1100,9);narrator.Aborted(1400,9);
        var plays=narrator.Playbacks(0,2000);
        Assert.Equal(new[]{"old","new"},plays.Select(p=>p.Clip.Name));
        Assert.Equal(new[]{"ended","aborted"},plays.Select(p=>p.Ending));
        Assert.Contains(lines,line=>line.Contains("ended old after 0.9 s"));
        Assert.Contains(lines,line=>line.Contains("aborted new after 0.3 s"));
        narrator.RobotEnded(1500,9);narrator.Flush();
        Assert.Contains(lines,line=>line.Contains("ended report for new after aborted"));
        Assert.DoesNotContain(lines,line=>line.Contains("start not observed"));
    }
    [Fact]
    public void UnknownRobotTagIsVisibleAndNeverBorrowsTheLatestClipName()
    {
        var lines=new List<string>();using var narrator=new LiveNarration(lines.Add,()=>0,automaticFlush:false);
        narrator.RegisterClip(0,new(3,"known",900,AnimationTrack.Head));
        narrator.RobotStarted(50,8);narrator.Flush();
        Assert.Contains(lines,line=>line.Contains("animation tag 8 (clip unavailable)"));
        Assert.Contains(lines,line=>line.Contains("duration and tracks unavailable"));
        Assert.DoesNotContain(lines,line=>line.Contains("playing known"));
    }
    [Fact]
    public void InitStreamSnapshotUsesPresentKeyframesAndDoesNotNeedAPlaybackNameLookupAtAck()
    {
        using var fixture=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true);
        var observed=new ConcurrentQueue<AnimationDiagnostic>();
        fixture.Rig.Robot.Animations.Scheduler.Diagnostic+=observed.Enqueue;
        var clip=fixture.Rig.Robot.Animations.Library!.GetClip("anim_reacttocliff_pickup_03");
        var ticket=fixture.Rig.Robot.Animations.PlayTracked(clip);
        Assert.NotNull(ticket);
        var snapshot=Assert.Single(observed.OfType<AnimationClipInitializedObservation>().Where(s=>s.Tag==ticket.Tag));
        Assert.Equal(clip.Name,snapshot.Name);
        Assert.True(snapshot.DurationMs>0);
        Assert.True(snapshot.Tracks.HasFlag(AnimationTrack.Audio));
        Assert.True(snapshot.Tracks.HasFlag(AnimationTrack.Head));
        Assert.True(snapshot.Tracks.HasFlag(AnimationTrack.Lift));
    }
    [Fact]
    public void ObservationWaitRequiresAStopAfterTheMatchingStartAndBlankInputCannotAnswerIt()
    {
        var check=PersonalityHw.Steps.Single(step=>step.Id=="pickup").Check!;
        JsonObject Report(double at,BehaviorID old,BehaviorID next)=>new(){["kind"]="behavior-transition",["atMs"]=at,
            [nameof(BehaviorTransitionReport.OldId)]=(byte)old,[nameof(BehaviorTransitionReport.NewId)]=(byte)next};
        var reports=new List<JsonObject>{Report(100,BehaviorID.ReactToPickup,BehaviorID.Wait)};
        Assert.False(PersonalityHw.ObservationCompleted(check,reports,0,5000));
        reports.Add(Report(200,BehaviorID.Wait,BehaviorID.ReactToPickup));
        Assert.False(PersonalityHw.ObservationCompleted(check,reports,0,5000));
        Assert.Null(PersonalityHw.ParseObservation(""));
        Assert.Equal("partly",PersonalityHw.ParseObservation("Partly"));
        reports.Add(Report(500,BehaviorID.ReactToPickup,BehaviorID.ReactToReturnedToTreads));
        Assert.True(PersonalityHw.ObservationCompleted(check,reports,0,5000));
    }
    [Fact]
    public void EveryNonIdlePersonalityStepWaitsForANamedReportBeforeTheQuestion()
    {
        Assert.All(PersonalityHw.Steps.Where(step=>step.Id!="alone"),
            step=>Assert.True(step.Check is not null||step.Signal is not null,step.Id));
    }
    [Fact]
    public void ACubeEventWindowDoesNotFinishBeforeTheSubsequentClipEnds()
    {
        using var narrator=new LiveNarration(_=>{},()=>0,automaticFlush:false);
        Assert.False(narrator.ObservationSettled(0,4999));
        narrator.RegisterClip(100,new(5,"response",7000,AnimationTrack.Head));
        narrator.RobotStarted(200,5);
        Assert.False(narrator.ObservationSettled(0,5000));
        narrator.RobotEnded(7200,5);
        Assert.True(narrator.ObservationSettled(0,7200));
    }
}
