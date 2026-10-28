using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M15-012
// fidelity: M15-075
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
    protected override void OnStart()
    {
        Scope.SmartDisableReactionsWithLock(Id,ReactionLockTables.PutDownBlock);
        var action=NativePutDownActions.CreateInitial(_m,out float backup);BackupDistance=backup;
        IncreaseScoreWhileActing(5f);StartActing(action,_=>LookDown());
    }
    internal void LookDown()
    {
        var action=NativePutDownActions.CreateLook(_m,this,true);
        IncreaseScoreWhileActing(5f);StartActing(action,_=>
        {
            if(RawCarry==uint.MaxValue)return;
            _m.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorPutDownBlock.LookDownAtBlock.DidNotSeeBlock","Forcibly setting carried objects as unattached (See COZMO-2192)");
            _m.Docking.ReleaseCarriedObject(forget:false);
        });
    }
}
