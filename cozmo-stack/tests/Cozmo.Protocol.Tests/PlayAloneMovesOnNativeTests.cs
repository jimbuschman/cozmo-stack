using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PlayAloneMovesOnNativeTests
{
    [Fact]
    public void CanonicalEmptyWorldFreeplayMovesPastPlayAloneOverFiveMinutes()
    {
        // Offline firmware acknowledgments follow actual wire commands. No activity, child, needs or reaction is forced.
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var parent=f.Runtime.FreeplayObserver;var manager=f.Robot.BehaviorManagerOwner!;
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        double start=f.Rig.Engine.Timer.Seconds;
        var transitions=new List<string>();var missing=new List<string>();void Observe(string value)=>missing.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} child={parent.Current?.Id} behavior={manager.Current?.Id} {value}");
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Observe;
        bool sawSocialize=false,sawPlayAlone=false,sawLater=false,sawFind=false,findStopped=false;
        byte priorAnimation=0;string previous="";
        try
        {
            f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
            while(f.Rig.Engine.Timer.Seconds-start<300)
            {
                f.Motors();
                byte tag=f.Rig.Robot.Animations.CurrentTag;
                if(tag!=0 && tag!=priorAnimation){f.Rig.Data(new AnimationStarted{AnimId=tag});f.Rig.Data(new AnimationEnded{AnimId=tag});}
                priorAnimation=tag;
                string child=parent.Current?.Id??"<null>";string current=manager.Current?.Id??"<null>";
                string state=child+"/"+current;
                if(state!=previous){transitions.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} {state}");previous=state;}
                sawSocialize|=child=="Socialize";sawPlayAlone|=child=="PlayAlone";
                sawFind|=ReferenceEquals(manager.Current,find);if(sawFind&&!find.EngineRunning)findStopped=true;
                // Require a real running configured candidate owned by the later selected child's chooser.
                if(sawPlayAlone && findStopped && child!="PlayAlone" && parent.Current is {} selected && manager.Current is SteppedBehavior {EngineRunning:true} running &&
                   (selected.Chooser?.BehaviorIds.Contains(running.Id)==true || selected.InterludeChooser?.BehaviorIds.Contains(running.Id)==true) &&
                   f.Runtime.Bound.TryGetValue(running.Id,out var actual) && ReferenceEquals(actual,running))sawLater=true;
            }
            string evidence="transitions="+string.Join(" | ",transitions)+"; decisions="+string.Join(" | ",parent.Decisions.TakeLast(30))+"; missing="+string.Join(" | ",missing);
            Assert.True(sawSocialize && sawFind,evidence);Assert.True(sawPlayAlone,evidence);Assert.True(findStopped,evidence);Assert.True(sawLater,evidence);
        }
        finally {SteppedBehavior.MissingReported-=Observe;}
    }
}
