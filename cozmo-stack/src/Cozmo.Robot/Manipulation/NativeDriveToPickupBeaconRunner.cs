using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// M15-026: 0055C464..548; checked zero-face IDrive tuple.
internal sealed class NativeDriveToPickupBeaconRunner : CompoundActionSequential
{
    internal NativeDriveToObjectRunner? Drive {get;}
    internal NativePickupObjectRunner Pickup {get;}=null!;
    internal CompoundActionSequential? DriveAndWait {get;}
    internal Action<NativeActionUnavailable>? Unavailable;
    protected override string ActionName=>"DriveToPickupObject";
    public override string? NativeName=>ActionName;
    internal NativeDriveToPickupBeaconRunner(ManipulationSystem m,uint target,Func<bool>? current=null):base(()=>m.Robot.Engine.Timer.SecondsF)
    {
        current??=()=>true;SourceLog=(l,c,k,t)=>m.Robot.Engine.EmitSourceLog(l,c,k,t);SeedNativeEmptyCompletion();
        if(!current())return;
        if(m.Robot.Engine.Robot!.CarryingOwner!.CarriedObjectId!=target)
        {
            Drive=new(m,target,PreActionType.Docking);Drive.Unavailable=e=>Unavailable?.Invoke(e);
            if(!current()){Drive.RetireNeverAdmittedTree();return;}
            DriveAndWait=new(()=>m.Robot.Engine.Timer.SecondsF){SourceLog=SourceLog};DriveAndWait.SeedNativeEmptyCompletion();
            if(!current()){Drive.RetireNeverAdmittedTree();DriveAndWait.RetireNeverAdmittedTree();return;}
            DriveAndWait.AddAction(Drive,false,false);
            if(!current()){DriveAndWait.RetireNeverAdmittedTree();return;}
            var wait=new Wait(m,()=>Pickup.CheckPreActionPose=false);
            if(!current()){wait.RetireNeverAdmittedTree();DriveAndWait.RetireNeverAdmittedTree();return;}
            DriveAndWait.AddAction(wait,false,false);
            if(!current()){DriveAndWait.RetireNeverAdmittedTree();return;}
            AddAction(DriveAndWait,true,false);
            if(!current())return;
        }
        Pickup=new(m,target,checkPreActionPose:true);
        if(!current()){Pickup.RetireNeverAdmittedTree();return;}
        var wait2=new Wait(m,()=>{});
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
    private sealed class Wait(ManipulationSystem m,Action callback):NativeVisionRunner(m,0x33,0)
    {
        protected override string ActionName=>"WaitForLambda";
        public override byte? GetCompletionVariant()=>6;
        public override int Init()=>0;
        public override uint CheckIfDone(){callback();return 0;}
    }
}
