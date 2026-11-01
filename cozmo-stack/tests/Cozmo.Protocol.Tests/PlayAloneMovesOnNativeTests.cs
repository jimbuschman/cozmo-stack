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
    [Fact]
    public void CanonicalEmptyWorldFreeplayMovesPastPlayAloneOverFiveMinutes()
    {
        // Offline firmware acknowledgments follow actual wire commands. No activity, child, needs or reaction is forced.
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true);
        var parent=f.Runtime.FreeplayObserver;var manager=f.Robot.BehaviorManagerOwner!;
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        double start=f.Rig.Engine.Timer.Seconds;
        var transitions=new List<string>();var missing=new List<string>();void Observe(string value)=>missing.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} child={parent.Current?.Id} behavior={manager.Current?.Id} {value}");
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Observe;
        bool sawSocialize=false,sawPlayAlone=false,sawLater=false,sawFind=false,findStopped=false;
        byte activeAnimation=0;int sentMark=f.Rig.Mark();string previous="";string? firstLater=null;
        var introClips=new HashSet<string>();var idleClips=new HashSet<string>();
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
                        f.Rig.Data(new AnimationStarted{AnimId=activeAnimation});
                    }
                    else if(message is EndOfAnimation)f.Rig.Data(new AnimationEnded{AnimId=activeAnimation});
                }
                string child=parent.Current?.Id??"<null>";string current=manager.Current?.Id??"<null>";
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
            string evidence="transitions="+string.Join(" | ",transitions)+"; decisions="+string.Join(" | ",parent.Decisions.TakeLast(30))+"; missing="+string.Join(" | ",missing);
            Assert.True(sawSocialize && sawFind,evidence);Assert.True(sawPlayAlone,evidence);Assert.True(findStopped,evidence);Assert.True(sawLater,evidence);
            var library=f.Rig.Robot.Animations.Library!;
            var introGroup=library.GetGroup("ag_hiking_getin")!;var idleGroup=library.GetGroup("ag_hiking_lookaround")!;
            Assert.NotEmpty(introClips);Assert.All(introClips,clip=>Assert.Contains(introGroup.Entries,e=>e.Name==clip));
            Assert.NotEmpty(idleClips);Assert.All(idleClips,clip=>Assert.Contains(idleGroup.Entries,e=>e.Name==clip));
            _output.WriteLine("First later owned action: "+firstLater);
        }
        finally {SteppedBehavior.MissingReported-=Observe;}
    }
}
