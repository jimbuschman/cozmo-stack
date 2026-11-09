using Cozmo.Protocol;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// fidelity: M15-022, M12-015 — P8 actual IAction/ActionList ground recipient, not the async public adapter.
internal class NativePlaceGroundRunner:NativeVisionRunner
{
    private bool _latched;
    private bool _ending;
    private uint _saved=uint.MaxValue;
    private NativeTurnTowardsObjectRunner? _verify;
    private readonly EngineRobot _owner;
    private readonly CarryingComponent _carrying;
    private readonly DockingComponentData _docking;
    private readonly Behavior.BehaviorManager _manager;
    private readonly object _stopGate = new();
    private Behavior.ScopedAcquisition? _reactionLease;
    internal TaskCompletionSource<(uint Result, bool OwnerRetired)>? AiNativeEnded;
    internal bool HostFailureCleanup;
    internal string? HostUnavailable;
    private bool Current => !_ending && !_owner.Lifetime.Destroying && ReferenceEquals(_owner.Engine.Robot, _owner) && !DestructionObserved && (ScopedParentCurrent?.Invoke()??true);
    protected override string ActionName=>"PlaceObjectOnGround";
    internal uint SavedObject=>_saved;
    internal bool StatusLatched => _latched;
    internal NativeTurnTowardsObjectRunner? VerifyChild=>_verify;
    internal NativePlaceGroundRunner(ManipulationSystem m):this(m,m.Robot.Engine.Robot ?? throw new NativeActionUnavailable("Ground Robot owner unavailable")){}
    internal NativePlaceGroundRunner(ManipulationSystem m,EngineRobot owner):base(m,0x18,7)
    {
        _owner=owner;
        ScopedOwner=owner;
        _carrying=owner.CarryingOwner ?? throw new NativeActionUnavailable("Ground carrying owner unavailable");
        _docking=owner.DockingOwner ?? throw new NativeActionUnavailable("Ground docking owner unavailable");
        _manager=owner.BehaviorManagerOwner ?? throw new NativeActionUnavailable("Ground behavior manager owner unavailable");
    }
    // fidelity: M12-052 - saved carried ID completion projection only; lifecycle/wire owners retained.
    public override byte? GetCompletionVariant()=>0;
    public override ObjectInteractionCompletion GetObjectInteractionCompletion()
    {
        // 00554AE0..4AFA: the other four native stack words were never initialized.
        // M1-034 withholds only the full undefined publication; saved ID/count1/false and scalar callbacks survive.
        Cozmo.Robot.Behavior.SteppedBehavior.ReportMissing("M1-034 M12-052 Ground completion 0x00554AE0..00554AFA: four uninitialized objectInteractionCompleted ID slots; full raw/API payload unavailable, defined saved-carry/count1/false and scalar result retained; PlaceRel normalization policy does not apply");
        return new(_saved,0,0,0,0,1,false,false);
    }
    internal override ActionWatcher? Watcher{get=>base.Watcher;set{base.Watcher=value;if(_verify is not null)_verify.Watcher=value;}}
    public override uint Update()
    {
        int depth=Watcher?.HostUpdateDepth??0;
        try { return base.Update(); }
        catch(NativeActionUnavailable error) when(AiNativeEnded is not null || HostFailureCleanup)
        {
            Watcher?.UnwindUnavailableHostUpdate(depth);
            HostUnavailable = error.Message;
            Behavior.SteppedBehavior.ReportMissing("M15-022 placement verification unavailable: "+error.Message);
            _owner.ActionList.DeferHostCleanup(()=>{if(_owner.ActionList.ContainsRunner(this))_owner.ActionList.Cancel(Tag);});
            return EngineActionResult.Running;
        }
    }
    public override int Init()
    {
        _latched=false;
        if (!Current) return unchecked((int)EngineActionResult.Cancelled);
        uint id=_carrying.CarriedObjectId??uint.MaxValue;int result;
        if(id==uint.MaxValue)
        {
            M.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"PlaceObjectOnGroundAction.CheckPreconditions.NotCarryingObject","");
            result=0x03000011;
        }
        else
        {
            _saved=id;bool failed=M.Docking.PlaceObjectOnGroundOwned(_owner,_carrying,()=>Current);
            if (!Current) return unchecked((int)EngineActionResult.Cancelled);
            _verify=new(M,id,unchecked((short)_carrying.NativeMarkerCode),0f,true,false){SuppressTrackLocking=true,Watcher=Watcher,ScopedOwner=_owner,ScopedParentCurrent=()=>Current};
            result=failed?0x03000016:0;
        }
        LogCallbackDeferral.RunGated(_stopGate,()=>{if(Current)M.Robot.Motion.StopAllMotorsOwned(_owner,()=>Current);});
        if (!Current) return unchecked((int)EngineActionResult.Cancelled);
        _latched=false;
        if(result==0)
        {
            var lease = _reactionLease = new Behavior.ScopedAcquisition();
            bool acquired = _manager.DisableScopedReactions(GroundPlacement.ReactionLockName,GroundPlacement.ReactionLockTable,lease);
            if (!Current || !acquired) return unchecked((int)EngineActionResult.Cancelled);
            lease.Completed = true;
        }
        return result;
    }
    public override uint CheckIfDone()
    {
        if (!Current) return EngineActionResult.Cancelled;
        if(_docking.Active!=0){_latched=true;return EngineActionResult.Running;}
        if(!_latched||_owner.NativeMovementIsMoving)return EngineActionResult.Running;
        uint result=EngineActionResult.Running;
        LogCallbackDeferral.RunGated(_stopGate,()=>{if(Current)result=_verify!.Update();});
        if (!Current) return EngineActionResult.Cancelled;
        if(result!=0&&result!=EngineActionResult.Running)
        {
            M.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"PlaceObjectOnGroundAction.CheckIfDone.FaceAndVerifyFailed",$"FaceAndVerify action reported failure, just clearing object {unchecked((int)_saved)}.");
            if(!Current)return EngineActionResult.Cancelled;
            M.World.ClearLocatedObjectByIdInCurrentOrigin(_saved);
        }
        return result;
    }
    public override void WatcherEnding()
    {
        if (_ending || DestructionObserved) return;
        _ending = true;
        var lease = _reactionLease; _reactionLease = null;
        if (lease is not null)
        {
            lease.Cancel();
            _manager.RemoveScopedReactions(GroundPlacement.ReactionLockName,lease);
        }
        else if(State!=EngineActionResult.NotStarted)_manager.RemoveDisableReactionsLock(GroundPlacement.ReactionLockName);
        var verify = _verify; _verify = null;
        verify?.Prep();verify?.WatcherEnding();base.WatcherEnding();
        AiNativeEnded?.TrySetResult((State,_owner.Lifetime.Destroying || !ReferenceEquals(_owner.Engine.Robot,_owner)));
    }
}
