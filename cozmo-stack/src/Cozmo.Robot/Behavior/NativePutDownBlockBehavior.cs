using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M15-012
//005C7F98..84D4. Public PutDownBlockBehavior remains the separate manual adapter.
internal sealed class NativePutDownBlockBehavior:SteppedBehavior
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    internal float BackupDistance {get;private set;}
    internal NativePutDownBlockBehavior(BehaviorFactoryContext source,JsonElement config)
        :base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(),"PutDownBlock")
    {_m=source.Manipulation!;_robot=source.NativeRobot!;}
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>true;
    public override bool IsRunnable(BehaviorContext context)=>IsRunnableBase(context)&&IsRunnableInternal(context);
    private uint RawCarry=>_robot.CarryingOwner?.CarriedObjectId??uint.MaxValue;
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        if(_robot.CarryingOwner is null){ReportMissing("PutDownBlock actual Carrying owner unavailable; no no-carry substitute");return false;}
        return RawCarry!=uint.MaxValue||HasCurrentAction;
    }
    private CompoundActionSequential Sequence()=>new(()=>_m.Robot.Engine.Timer.SecondsF);
    private TriggerAnimationAction Animation(AnimationTrigger trigger)=>new(_m.Robot,new(trigger,1,true,0,60f,false));
    protected override void OnStart()
    {
        Scope.SmartDisableReactionsWithLock(Id,ReactionLockTables.PutDownBlock);
        double u=_m.Robot.Engine.ContextRandom.GetNextDbl();
        BackupDistance=(float)((-75.0-(-45.0))*u+(-45.0));
        var action=Sequence();action.AddAction(new DriveStraightAction(_m,BackupDistance,100f,true),false,false);
        action.AddAction(Animation(AnimationTrigger.PutDownBlockPutDown),false,false);
        IncreaseScoreWhileActing(5f);StartActing(action,_=>LookDown());
    }
    internal void LookDown()
    {
        var action=Sequence();
        if(RawCarry!=uint.MaxValue)
        {
            var parallel=new CompoundActionParallel(()=>_m.Robot.Engine.Timer.SecondsF);
            parallel.AddAction(_m.Robot.Motion.CreateNativeHeadChild(BitConverter.UInt32BitsToSingle(0xBEB2B8C2),BitConverter.UInt32BitsToSingle(0x3D0EFA35),15f,20f),false,false);
            parallel.AddAction(new DriveStraightAction(_m,-30),false,false);
            action.AddAction(parallel,false,false);
            action.AddAction(new NativeWaitForImagesRunner(_m,2,(VisionMode)1,0),false,false);
        }
        var animation=Animation(AnimationTrigger.PutDownBlockKeepAlive);
        action.AddAction(new NativeFaceBeforeAnimationRunner(_m,animation),false,false);
        IncreaseScoreWhileActing(5f);StartActing(action,_=>
        {
            if(RawCarry==uint.MaxValue)return;
            _m.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorPutDownBlock.LookDownAtBlock.DidNotSeeBlock","Forcibly setting carried objects as unattached (See COZMO-2192)");
            _m.Docking.ReleaseCarriedObject(forget:false);
        });
    }
}
