using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// fidelity: M12-010
//54C8FC/54CA1C/54CA3C: the engine-clock timed wait (not WaitForImages32).
internal sealed class NativeTimedWaitRunner:NativeVisionRunner
{
    internal float Duration {get;}
    internal float Deadline {get;private set;}=-1f;
    protected override string ActionName=>"Wait";
    internal NativeTimedWaitRunner(ManipulationSystem m,float duration):base(m,0x31,0){Duration=duration;}
    public override byte? GetCompletionVariant()=>6;
    public override int Init(){Deadline=M.Robot.Engine.Timer.SecondsF+Duration;return 0;}
    public override uint CheckIfDone()=>M.Robot.Engine.Timer.SecondsF>Deadline?0:EngineActionResult.Running;
}

// fidelity: M12-010
//5469C0..5470EE: native owned Nearby; all motors are children of this action.
internal sealed class NativeNearbySearchRunner:NativeVisionRunner
{
    private readonly uint _target;
    private readonly float _distance,_speed,_head;
    private readonly CompoundActionSequential _compound;
    private RetainedSubscription? _observed;
    private int _seen;
    protected override string ActionName=>"SearchForNearbyObject";
    public override byte? GetCompletionVariant()=>6;
    internal CompoundActionSequential Sequence=>_compound;
    internal bool SeenLatch=>Volatile.Read(ref _seen)!=0;
    internal (float W1,float W2,float W3,float Turn1,float Turn2) Draw {get;private set;}
    internal NativeNearbySearchRunner(ManipulationSystem m,uint target,float distance,float speed,float head):base(m,0x21,0)
    {
        _target=target;_distance=distance;_speed=speed;_head=head;
        _compound=new(()=>m.Robot.Engine.Timer.SecondsF){SourceLog=SourceLog};
        _compound.SeedNativeEmptyCompletion();
        if(m.Robot.Engine.Robot?.HasExternalInterface==true)
            _observed=m.Robot.Engine.AnimationMessages.SubscribeMessage(0x44,true,p=>{if(p is NativeObservedObject value&&value.ObjectId==_target)Interlocked.Exchange(ref _seen,1);});
    }
    internal override ActionWatcher? Watcher {get=>base.Watcher;set{base.Watcher=value;_compound.Watcher=value;}}
    public override int Init()
    {
        _compound.ClearActions();_compound.TimeoutWarningEnabled=TimeoutWarningEnabled;
        var rng=M.Robot.Engine.ContextRandom;
        double w1=rng.RandDblInRange(SearchForNearbyObjectAction.WaitMinSec,SearchForNearbyObjectAction.WaitMaxSec);
        double coin=rng.RandDbl(1d);
        double a1=rng.RandDblInRange(SearchForNearbyObjectAction.SearchAngleMinRad,SearchForNearbyObjectAction.SearchAngleMaxRad);
        double w2=rng.RandDblInRange(SearchForNearbyObjectAction.WaitMinSec,SearchForNearbyObjectAction.WaitMaxSec);
        double a2=rng.RandDblInRange(SearchForNearbyObjectAction.SearchAngleMinRad,SearchForNearbyObjectAction.SearchAngleMaxRad);
        double w3=rng.RandDblInRange(SearchForNearbyObjectAction.WaitMinSec,SearchForNearbyObjectAction.WaitMaxSec);
        double sign=coin>.5d?1d:-1d;
        float turn1=(float)(sign*a1),turn2=(float)((double)(-turn1)-sign*a2);
        Draw=((float)w1,(float)w2,(float)w3,turn1,turn2);
        _compound.AddAction(new NativeTimedWaitRunner(M,(float)w1),false,false);
        var motion=new CompoundActionParallel(()=>M.Robot.Engine.Timer.SecondsF){SourceLog=SourceLog};
        motion.SeedNativeEmptyCompletion();
        var drive=MathF.Abs(100f-_speed)<BitConverter.UInt32BitsToSingle(0x3727C5AC)
            ?new DriveStraightAction(M,_distance,false):new DriveStraightAction(M,_distance,_speed,false);
        motion.AddAction(drive,false,false);
        motion.AddAction(M.Robot.Motion.CreateNativeHeadChild(_head,(float)SearchForNearbyObjectAction.HeadToleranceRad,
            CozmoMotion.ActionDefaultHeadSpeedRadPerSec,CozmoMotion.ActionDefaultHeadAccelRadPerSec2),false,false);
        _compound.AddAction(motion,false,false);
        _compound.AddAction(new NativeTimedWaitRunner(M,(float)w1),false,false);
        _compound.AddAction(Turn(turn1),false,false);
        _compound.AddAction(new NativeTimedWaitRunner(M,(float)w2),false,false);
        _compound.AddAction(Turn(turn2),false,false);
        _compound.AddAction(new NativeTimedWaitRunner(M,(float)w3),false,false);
        uint result=_compound.Update();
        return (result|EngineActionResult.Running)==EngineActionResult.Running?0:unchecked((int)result);
    }
    private NativeTurnInPlaceRunner Turn(float angle)=>new(M,new TurnInPlaceSpec(angle,false,
        SearchForNearbyObjectAction.TurnToleranceRad,true,false,0,0,false));
    public override uint CheckIfDone()
    {
        uint result=_compound.Update();
        if(Interlocked.Exchange(ref _seen,0)!=0)return 0;
        return result==0&&_target!=uint.MaxValue?0x0300001D:result;
    }
    public override void Prep(){_compound.Prep();base.Prep();}
    public override void WatcherEnding()
    {_compound.Prep();_observed?.Dispose();_observed=null;_compound.WatcherEnding();base.WatcherEnding();}
}
