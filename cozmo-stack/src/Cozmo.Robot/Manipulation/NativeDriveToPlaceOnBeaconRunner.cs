using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// M15-008: 0055C71A..7A0, checked zero-face IDrive tuple.
internal sealed class NativeDriveToPlaceOnBeaconRunner:CompoundActionSequential
{
    internal NativeDriveToObjectRunner? Drive {get;}
    internal NativePlaceRelRunner Place {get;}=null!;
    internal CompoundActionSequential? DriveAndWait {get;}
    internal Action<NativeActionUnavailable>? Unavailable {get;set;}
    protected override string ActionName=>"DriveToPlaceOnObject";
    public override string? NativeName=>ActionName;
    internal NativeDriveToPlaceOnBeaconRunner(ManipulationSystem m,uint target,Func<bool>? current=null):base(()=>m.Robot.Engine.Timer.SecondsF)
    {
        current??=()=>true;SourceLog=(l,c,k,t)=>m.Robot.Engine.EmitSourceLog(l,c,k,t);SeedNativeEmptyCompletion();
        if(!current())return;
        if(m.Robot.Engine.Robot!.CarryingOwner!.CarriedObjectId!=target)
        {
            Drive=new(m,target,PreActionType.PlaceRelative);Drive.Unavailable=e=>Unavailable?.Invoke(e);
            if(!current()){Drive.RetireNeverAdmittedTree();return;}
            DriveAndWait=new(()=>m.Robot.Engine.Timer.SecondsF){SourceLog=SourceLog};DriveAndWait.SeedNativeEmptyCompletion();
            if(!current()){Drive.RetireNeverAdmittedTree();DriveAndWait.RetireNeverAdmittedTree();return;}
            DriveAndWait.AddAction(Drive,false,false);
            if(!current()){DriveAndWait.RetireNeverAdmittedTree();return;}
            var wait=new Wait(m,()=>Place.CheckPreActionPose=false);
            if(!current()){wait.RetireNeverAdmittedTree();DriveAndWait.RetireNeverAdmittedTree();return;}
            DriveAndWait.AddAction(wait,false,false);
            if(!current()){DriveAndWait.RetireNeverAdmittedTree();return;}
            AddAction(DriveAndWait,true,false);
            if(!current())return;
        }
        Place=new(m,target);Place.CheckPreActionPose=true;Place.Unavailable=e=>Unavailable?.Invoke(e);
        if(!current()){Place.RetireNeverAdmittedTree();return;}
        var wait2=new Wait(m,()=>{});
        if(!current()){wait2.RetireNeverAdmittedTree();Place.RetireNeverAdmittedTree();return;}
        AddAction(wait2,false,false);
        if(!current()){Place.RetireNeverAdmittedTree();return;}
        AddAction(Place,false,false);
        if(!current())return;
        SetNativeCompletionProxy(Place.Tag);
    }
    private sealed class Wait(ManipulationSystem m,Action callback):NativeVisionRunner(m,0x33,0)
    {
        protected override string ActionName=>"WaitForLambda";
        public override byte? GetCompletionVariant()=>6;
        public override int Init()=>0;
        public override uint CheckIfDone(){callback();return 0;}
    }
}
