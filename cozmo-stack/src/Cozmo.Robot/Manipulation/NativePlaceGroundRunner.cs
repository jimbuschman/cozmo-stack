using Cozmo.Protocol;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// fidelity: M15-022, M12-015 — P8 actual IAction/ActionList ground recipient, not the async public adapter.
internal sealed class NativePlaceGroundRunner:NativeVisionRunner
{
    private bool _latched;
    private uint _saved=uint.MaxValue;
    private NativeTurnTowardsObjectRunner? _verify;
    protected override string ActionName=>"PlaceObjectOnGround";
    internal uint SavedObject=>_saved;
    internal NativeTurnTowardsObjectRunner? VerifyChild=>_verify;
    internal NativePlaceGroundRunner(ManipulationSystem m):base(m,0x18,7){}
    public override byte? GetCompletionVariant()=>0;
    public override ObjectInteractionCompletion GetObjectInteractionCompletion()
    {
        // 00554AE0..4AFA: the other four native stack words were never initialized.
        // M1-034 withholds only the full undefined publication; saved ID/count1/false and scalar callbacks survive.
        Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("M1-034 M15-022 Ground completion 0x00554AE0..00554AFA: four uninitialized objectInteractionCompleted ID slots; full raw/API payload unavailable, defined saved-carry/count1/false and scalar result retained; PlaceRel normalization policy does not apply");
        return new(_saved,0,0,0,0,1,false,false);
    }
    internal override ActionWatcher? Watcher{get=>base.Watcher;set{base.Watcher=value;if(_verify is not null)_verify.Watcher=value;}}
    public override int Init()
    {
        _latched=false;var owner=M.Robot.Engine.Robot!;var carry=owner.CarryingOwner!;
        uint id=carry.CarriedObjectId??uint.MaxValue;int result;
        if(id==uint.MaxValue)result=0x03000011;
        else
        {
            _saved=id;bool failed=M.Docking.PlaceObjectOnGround(false);
            _verify=new(M,id,unchecked((short)carry.NativeMarkerCode),0f,true,false){SuppressTrackLocking=true,Watcher=Watcher};
            result=failed?0x03000016:0;
        }
        M.Robot.Motion.StopAllMotors();_latched=false;
        if(result==0)owner.BehaviorManagerOwner?.DisableReactionsWithLock(PlaceObjectOnGroundAction.ReactionLockName,PlaceObjectOnGroundAction.ReactionLockTable,true);
        return result;
    }
    public override uint CheckIfDone()
    {
        var state=M.Robot.Engine.Robot!.StoredState??throw new NativeActionUnavailable("Ground actual raw robot state unavailable");
        if(state.Has(RobotStatusFlag.IsPickingOrPlacing)){_latched=true;return EngineActionResult.Running;}
        if(!_latched||M.Robot.Motion.BodyMoving)return EngineActionResult.Running;
        uint result=_verify!.Update();
        if(result!=0&&result!=EngineActionResult.Running)M.World.ClearLocatedObjectByIdInCurrentOrigin(_saved);
        return result;
    }
    public override void WatcherEnding()
    {
        if(State!=EngineActionResult.NotStarted)M.Robot.Engine.Robot?.BehaviorManagerOwner?.RemoveDisableReactionsLock(PlaceObjectOnGroundAction.ReactionLockName);
        _verify?.Prep();_verify?.WatcherEnding();_verify=null;base.WatcherEnding();
    }
}
