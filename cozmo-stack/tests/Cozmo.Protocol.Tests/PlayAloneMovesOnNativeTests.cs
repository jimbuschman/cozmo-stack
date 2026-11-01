using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
using Xunit.Abstractions;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PlayAloneMovesOnNativeTests
{
    private readonly ITestOutputHelper _output;
    public PlayAloneMovesOnNativeTests(ITestOutputHelper output)=>_output=output;
    [Theory]
    [InlineData(5489u)] [InlineData(123u)]
    public void CanonicalEmptyWorldFreeplayMovesPastPlayAloneOverFiveMinutes(uint seed)
    {
        // Offline firmware acknowledges actual wire commands. SeeFace and FallingStopped are supplied inputs;
        // no activity, child, need level or reaction is forced. The nonzero source RNG seed is a replay input.
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true);
        f.Rig.Engine.ContextRandom.SetSeed(seed);
        var original=f.Robot;var parent=f.Runtime.FreeplayObserver;var manager=original.BehaviorManagerOwner!;
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        double start=f.Rig.Engine.Timer.Seconds;
        var transitions=new List<string>();var missing=new List<string>();void Observe(string value)=>missing.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} child={parent.Current?.Id} behavior={manager.Current?.Id} {value}");
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Observe;
        bool sawSocialize=false,sawPlayAlone=false,sawLater=false,sawFind=false,findStopped=false;
        byte activeAnimation=0;int sentMark=f.Rig.Mark();string previous="";string? firstLater=null;
        var introClips=new HashSet<string>();var idleClips=new HashSet<string>();
        bool seeSent=false,fallSent=false,fallApplied=false,changedAfterSee=false,changedAfterFall=false;
        string? seeBefore=null,fallBefore=null;int rewards=0;var stack=new List<string>();
        var eventTrace=new List<string>();
        void OnStack(string line){if(line.Contains("M15-071 Freeplay native pending"))stack.Add(line);}
        void OnNeeds(string line){if(line.StartsWith("needs.action_completed Fall:")){fallApplied=true;eventTrace.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} Fall applied current={manager.Current?.Id} paused={f.Rig.Engine.Needs.IsPaused} connected={f.Rig.Engine.Needs.Connected}");}}
        void OnFallAction(int action)=>eventTrace.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} sensor NeedsAction={action}");
        void OnFallReport(FallingStoppedReport report)=>eventTrace.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} sensor report intensity={report.ImpactIntensity}");
        f.Rig.Engine.StackLogLine+=OnStack;f.Rig.Engine.Needs.Log+=OnNeeds;
        f.Rig.Robot.Sensors.NeedsActionCompleted+=OnFallAction;f.Rig.Robot.Sensors.FallingStoppedInternal+=OnFallReport;
        using var reward=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x65,true,_=>rewards++);
        try
        {
            f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
            while(f.Rig.Engine.Timer.Seconds-start<300)
            {
                f.Motors();
                var sent=f.Rig.SentSince(sentMark);sentMark=f.Rig.Mark();
                foreach(var message in sent)
                {
                    if(message is StartOfAnimation begin)
                    {
                        activeAnimation=begin.AnimId;string? clip=f.Rig.Robot.Animations.Playing;
                        if(clip is not null && manager.Current?.Id=="Hiking_FirstLookIntro")introClips.Add(clip);
                        if(clip is not null && manager.Current?.Id=="NothingToDo_Idle")idleClips.Add(clip);
                        // Actual source A1/+84 and wire start, with the same retained configured owner.
                        if(clip is not null && manager.Current is PlayAnimBehavior {EngineRunning:true,HasCurrentAction:true} playing &&
                           playing.LastSelected==clip && f.Runtime.Bound.TryGetValue(playing.Id,out var playingOwner) && ReferenceEquals(playingOwner,playing))
                        {
                            if(seeSent && playing.Id!=seeBefore)changedAfterSee=true;
                            if(fallApplied && playing.Id!=fallBefore)changedAfterFall=true;
                        }
                        f.Rig.Data(new AnimationStarted{AnimId=activeAnimation});
                    }
                    else if(message is EndOfAnimation)f.Rig.Data(new AnimationEnded{AnimId=activeAnimation});
                }
                string child=parent.Current?.Id??"<null>";string current=manager.Current?.Id??"<null>";
                double elapsed=f.Rig.Engine.Timer.Seconds-start;
                if(!seeSent && elapsed>=80)
                {
                    seeSent=true;seeBefore=current;
                    eventTrace.Add($"t={elapsed:F2} SeeFace current={current} paused={f.Rig.Engine.Needs.IsPaused} connected={f.Rig.Engine.Needs.Connected} ownerSame={ReferenceEquals(f.Rig.Engine.Robot,original)}");
                    // Labeled source-action input; this does not synthesize a face or select a child.
                    Assert.True(f.Rig.Engine.Needs.RegisterNeedsActionCompleted("SeeFace"));
                    Assert.True(f.Rig.Engine.Needs.NativePendingKnownFalse);
                }
                if(!fallSent && elapsed>=160)
                {
                    fallSent=true;fallBefore=current;
                    eventTrace.Add($"t={elapsed:F2} FallingStopped sent current={current} paused={f.Rig.Engine.Needs.IsPaused} connected={f.Rig.Engine.Needs.Connected} ownerSame={ReferenceEquals(f.Rig.Engine.Robot,original)}");
                    // Actual robot ingress, with supplied impact above the checked 1000 threshold.
                    f.Rig.Data(new FallingStopped{Timestamp=123,DurationMs=100,ImpactIntensity=1500f});
                }
                string state=child+"/"+current;
                if(state!=previous){transitions.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} {state}");previous=state;}
                sawSocialize|=child=="Socialize";sawPlayAlone|=child=="PlayAlone";
                sawFind|=ReferenceEquals(manager.Current,find);if(sawFind&&!find.EngineRunning)findStopped=true;
                // Require a real running configured candidate owned by the later selected child's chooser.
                if(sawPlayAlone && findStopped && child!="PlayAlone" && parent.Current is {} selected && manager.Current is SteppedBehavior {EngineRunning:true,OwnedRunner: {} runner} running &&
                   (selected.Chooser?.BehaviorIds.Contains(running.Id)==true || selected.InterludeChooser?.BehaviorIds.Contains(running.Id)==true) &&
                   f.Runtime.Bound.TryGetValue(running.Id,out var actual) && ReferenceEquals(actual,running))
                {sawLater=true;firstLater ??= $"t={f.Rig.Engine.Timer.Seconds-start:F2} child={child} behavior={running.Id} runner={runner.GetType().Name}";}
            }
            string evidence=$"seeSent={seeSent} fallSent={fallSent} fallApplied={fallApplied} changedAfterSee={changedAfterSee} changedAfterFall={changedAfterFall} seeBefore={seeBefore} fallBefore={fallBefore}; events="+string.Join(" | ",eventTrace)+"; transitions="+string.Join(" | ",transitions)+"; decisions="+string.Join(" | ",parent.Decisions.TakeLast(30))+"; missing="+string.Join(" | ",missing);
            _output.WriteLine($"seed={seed} seeSent={seeSent} fallSent={fallSent} fallApplied={fallApplied} changedAfterSee={changedAfterSee} changedAfterFall={changedAfterFall} seeBefore={seeBefore} fallBefore={fallBefore}; events="+string.Join(" | ",eventTrace));
            Assert.True(sawSocialize && sawFind,evidence);Assert.True(sawPlayAlone,evidence);Assert.True(findStopped,evidence);Assert.True(sawLater,evidence);
            var library=f.Rig.Robot.Animations.Library!;
            var introGroup=library.GetGroup("ag_hiking_getin")!;var idleGroup=library.GetGroup("ag_hiking_lookaround")!;
            Assert.NotEmpty(introClips);Assert.All(introClips,clip=>Assert.Contains(introGroup.Entries,e=>e.Name==clip));
            Assert.NotEmpty(idleClips);Assert.All(idleClips,clip=>Assert.Contains(idleGroup.Entries,e=>e.Name==clip));
            Assert.True(seeSent,evidence);Assert.True(fallSent,evidence);Assert.True(fallApplied,evidence);
            Assert.True(changedAfterSee,evidence);Assert.True(changedAfterFall,evidence);
            Assert.True(f.Rig.Engine.Needs.NativePendingKnownFalse,evidence);Assert.Empty(stack);Assert.Equal(0,rewards);
            _output.WriteLine("First later owned action: "+firstLater);
        }
        finally {SteppedBehavior.MissingReported-=Observe;f.Rig.Engine.StackLogLine-=OnStack;f.Rig.Engine.Needs.Log-=OnNeeds;f.Rig.Robot.Sensors.NeedsActionCompleted-=OnFallAction;f.Rig.Robot.Sensors.FallingStoppedInternal-=OnFallReport;}
    }
}
