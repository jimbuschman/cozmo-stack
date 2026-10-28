using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M15-012
//005C8174..82DA: shared native/public look builder; ordinary199 is behind the exact face wrapper.
internal static class NativePutDownActions
{
    //5C7FD0..80DE: exact same source initial sequence for both callers.
    internal static CompoundActionSequential CreateInitial(ManipulationSystem m,out float backup,Action? animationStarted=null)
    {
        double u=m.Robot.Engine.ContextRandom.GetNextDbl();backup=(float)((-75.0-(-45.0))*u+(-45.0));
        var sequence=new CompoundActionSequential(()=>m.Robot.Engine.Timer.SecondsF);sequence.SeedNativeEmptyCompletion();
        sequence.AddAction(new DriveStraightAction(m,backup,100f,true),false,false);
        sequence.AddAction(new ObservedAnimation(m,AnimationTrigger.PutDownBlockPutDown,animationStarted),false,false);
        return sequence;
    }
    internal static CompoundActionSequential CreateLook(ManipulationSystem m,SteppedBehavior parent,bool keepAlive,Action? keepAliveStarted=null)
    {
        var owner=m.Robot.Engine.Robot??throw new InvalidOperationException("MISSING M15-012 actual Robot owner");
        var carrying=owner.CarryingOwner??throw new InvalidOperationException("MISSING M15-012 actual Carrying owner");
        var sequence=new LookSequence(m,parent);sequence.SeedNativeEmptyCompletion();
        if((carrying.CarriedObjectId??uint.MaxValue)!=uint.MaxValue)
        {
            var parallel=new CompoundActionParallel(()=>m.Robot.Engine.Timer.SecondsF);parallel.SeedNativeEmptyCompletion();
            parallel.AddAction(m.Robot.Motion.CreateNativeHeadChild(BitConverter.UInt32BitsToSingle(0xBEB2B8C2),BitConverter.UInt32BitsToSingle(0x3D0EFA35),15f,20f),false,false);
            parallel.AddAction(new DriveStraightAction(m,-30),false,false);
            sequence.AddAction(parallel,false,false);
            sequence.AddAction(new NativeWaitForImagesRunner(m,2,(VisionMode)1,0),false,false);
        }
        if(keepAlive)
        {
            var animation=new ObservedAnimation(m,AnimationTrigger.PutDownBlockKeepAlive,keepAliveStarted);
            sequence.AddAction(new NativeFaceBeforeAnimationRunner(m,animation),false,false);
        }
        return sequence;
    }
    private sealed class ObservedAnimation(ManipulationSystem m,AnimationTrigger trigger,Action? started):TriggerAnimationAction(m.Robot,new(trigger,1,true,0,60f,false))
    {
        public override int Init(){started?.Invoke();return base.Init();}
    }
    private sealed class LookSequence(ManipulationSystem m,SteppedBehavior parent):CompoundActionSequential(()=>m.Robot.Engine.Timer.SecondsF)
    {
        private bool _unavailable;
        public override uint Update()
        {
            if(_unavailable&&(State==EngineActionResult.NotStarted||State==EngineActionResult.Running))return EngineActionResult.Running;
            int depth=Watcher?.HostUpdateDepth??0;
            try{return base.Update();}
            catch(NativeActionUnavailable error)
            {
                Watcher?.UnwindUnavailableHostUpdate(depth);_unavailable=true;SteppedBehavior.ReportMissing(error.ToString());
                var owner=m.Robot.Engine.Robot;
                owner?.ActionList.DeferHostCleanup(()=>
                {
                    if(parent.CurrentRunnerTag!=Tag)return;
                    if(ReferenceEquals(owner.BehaviorManagerOwner?.Current,parent))owner.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,m.Robot.Engine.Timer.SecondsF);
                    else parent.Stop(BehaviorStopReason.Interrupted);
                    owner.ActionList.Cancel(Tag);
                });
                return EngineActionResult.Running; // Host unavailable teardown, never a source completion.
            }
        }
    }
}
