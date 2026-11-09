using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// M15-026: 0055C464..548; checked zero-face IDrive tuple.
internal sealed class NativeDriveToPickupBeaconRunner : CompoundActionSequential
{
    internal NativeDriveToObjectRunner? Drive {get;}
    internal NativePickupObjectRunner Pickup {get;}=null!;
    internal CompoundActionSequential? DriveAndWait {get;}
    internal Action<NativeActionUnavailable>? Unavailable;
    private EngineRobot? _owner;
    private bool _ending;
    private readonly object _cleanupGate=new();
    internal TaskCompletionSource<(uint Result, bool OwnerRetired)>? AiNativeEnded;
    internal string? HostUnavailable;
    protected override string ActionName=>"DriveToPickupObject";
    public override string? NativeName=>ActionName;
    internal NativeDriveToPickupBeaconRunner(ManipulationSystem m,uint target,Func<bool>? current=null,EngineRobot? owner=null):base(()=>m.Robot.Engine.Timer.SecondsF)
    {
        _owner=owner;
        current??=()=>true;SourceLog=(l,c,k,t)=>m.Robot.Engine.EmitSourceLog(l,c,k,t);SeedNativeEmptyCompletion();
        bool Live()=>current() && !_ending && !DestructionObserved;
        if(!current())return;
        if(m.Robot.Engine.Robot!.CarryingOwner!.CarriedObjectId!=target)
        {
            Drive=owner is null ? new(m,target,PreActionType.Docking) : new(m,owner,target,PreActionType.Docking,Live);Drive.Unavailable=e=>Unavailable?.Invoke(e);
            if(!current()){Drive.RetireNeverAdmittedTree();return;}
            DriveAndWait=new(()=>m.Robot.Engine.Timer.SecondsF){SourceLog=SourceLog};DriveAndWait.SeedNativeEmptyCompletion();
            if(!current()){Drive.RetireNeverAdmittedTree();DriveAndWait.RetireNeverAdmittedTree();return;}
            DriveAndWait.AddAction(Drive,false,false);
            if(!current()){DriveAndWait.RetireNeverAdmittedTree();return;}
            var wait=new Wait(m,()=>{if(Live())Pickup.CheckPreActionPose=false;}){ScopedOwner=owner,ScopedParentCurrent=owner is null?null:Live};
            if(!current()){wait.RetireNeverAdmittedTree();DriveAndWait.RetireNeverAdmittedTree();return;}
            DriveAndWait.AddAction(wait,false,false);
            if(!current()){DriveAndWait.RetireNeverAdmittedTree();return;}
            AddAction(DriveAndWait,true,false);
            if(!current())return;
        }
        Pickup=owner is null ? new(m,target,checkPreActionPose:true) : new(m,owner,target,checkPreActionPose:true,directMarkerTurn:false);
        if(owner is not null)Pickup.ScopedParentCurrent=Live;
        if(owner is not null)
        {
            void Failed(NativeActionUnavailable error)
            {
                HostUnavailable=error.Message;
                Behavior.SteppedBehavior.ReportMissing("M12-017 pickup compound input unavailable: "+error.Message);
                owner.ActionList.DeferHostCleanup(()=>{if(owner.ActionList.ContainsRunner(this))owner.ActionList.Cancel(Tag);});
            }
            Unavailable=Failed;
            Pickup.Unavailable=Failed;
        }
        if(!current()){Pickup.RetireNeverAdmittedTree();return;}
        var wait2=new Wait(m,()=>{}){ScopedOwner=owner,ScopedParentCurrent=owner is null?null:Live};
        if(!current()){wait2.RetireNeverAdmittedTree();Pickup.RetireNeverAdmittedTree();return;}
        AddAction(wait2,false,false);
        if(!current()){Pickup.RetireNeverAdmittedTree();return;}
        AddAction(Pickup,false,false);
        if(!current())return;
        SetNativeCompletionProxy(Pickup.Tag);
    }
    public override uint Update()
    {
        int depth=Watcher?.HostUpdateDepth??0;
        try{return base.Update();}
        catch(NativeActionUnavailable error){Watcher?.UnwindUnavailableHostUpdate(depth);Unavailable?.Invoke(error);return EngineActionResult.Running;}
    }
    public override void WatcherEnding()
    {
        if(_ending || DestructionObserved)return;
        _ending=true;
        if(_owner is null)base.WatcherEnding();
        else LogCallbackDeferral.RunGated(_cleanupGate,base.WatcherEnding);
        if(AiNativeEnded is {} ended && _owner is {} owner)
            ended.TrySetResult((State,owner.Lifetime.Destroying || !ReferenceEquals(owner.Engine.Robot,owner)));
    }
    private sealed class Wait(ManipulationSystem m,Action callback):NativeVisionRunner(m,0x33,0)
    {
        protected override string ActionName=>"WaitForLambda";
        public override byte? GetCompletionVariant()=>6;
        public override int Init()=>0;
        public override uint CheckIfDone(){if(!ScopedCurrent)return EngineActionResult.Cancelled;callback();return ScopedCurrent?0:EngineActionResult.Cancelled;}
    }
}
