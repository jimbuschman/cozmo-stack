using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// Proposed Roll recovery join PB1-PB4:005B8C1C..005B8E68; common helper owns retirement/cancellation.
internal sealed class NativePlaceBlockHelper:IHelper
{
    private readonly ManipulationSystem _m;
    private readonly SteppedBehavior _parent;
    internal float Angle {get;private set;}
    internal NativeTurnInPlaceRunner? Turn {get;private set;}
    internal CompoundActionSequential? Placement {get;private set;}
    internal NativePlaceBlockHelper(ManipulationSystem m,SteppedBehavior parent,BehaviorHelperFactory factory):base(m.Robot,"PlaceBlock",parent,factory){_m=m;_parent=parent;}
    public override bool ShouldCancelDelegates(CozmoRobot robot)=>false;
    protected override BehaviorStatus Init(CozmoRobot robot)
    {
        var rng=_m.Robot.Engine.ContextRandom;
        double angle=rng.RandDblInRange(BitConverter.UInt64BitsToDouble(0x3FE921FB54442D18),BitConverter.UInt64BitsToDouble(0x3FF921FB54442D18));
        if(rng.RandDbl(1d)<.5d)angle=-angle;
        Angle=(float)angle;
        Turn=new(_m,new TurnInPlaceSpec(Angle,false,TurnInPlaceAction.MinToleranceRad,true,false,0,0,false));
        _parent.StartActing(Turn,_=>Place());return Status;
    }
    private void Place()
    {
        Placement=new(()=>_m.Robot.Engine.Timer.SecondsF);Placement.SeedNativeEmptyCompletion();
        Placement.AddAction(new NativePlaceGroundRunner(_m),false,true);
        _parent.StartActing(Placement,_=>StoreStatus(BehaviorStatus.Complete));
    }
    protected override BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot)=>Status;
}
