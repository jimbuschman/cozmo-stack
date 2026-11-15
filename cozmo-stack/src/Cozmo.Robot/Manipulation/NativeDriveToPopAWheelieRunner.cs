using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// fidelity: M13-041 - current ordinary DriveToPop composition; Sparks widening pending.
// POP-A-WHEELIE-1 D1/D2:0055D1E8..D26C; shared IDrive0055B2C8/AddDock0055B7AC.
internal sealed class NativeDriveToPopAWheelieRunner:AiCubeTrickCompound
{
    internal NativeDriveToObjectRunner? Drive {get;}
    internal NativeLastFaceRunner? Face {get;}
    internal NativePopAWheelieRunner Pop {get;}
    internal CompoundActionSequential? DriveAndWait {get;}
    internal float MaximumFaceTurn {get;}
    internal Action? BeforeDock {get;set;}
    protected override string ActionName=>"DriveToPopAWheelie";
    public override string? NativeName=>ActionName;
    internal NativeDriveToPopAWheelieRunner(ManipulationSystem m,uint id,float maximum,EngineRobot? owner=null,Func<bool>? current=null):base(m,owner,current,retainConfiguredUnavailable:true)
    {
        MaximumFaceTurn=maximum;Type=5;SourceLog=(l,c,k,t)=>m.Robot.Engine.EmitSourceLog(l,c,k,t);SeedNativeEmptyCompletion();
        Pop=new(m,id) { ScopedOwner=owner, ScopedParentCurrent=owner is null ? null : AiExecutionCurrent };
        Pop.Unavailable=ReportUnavailable;
        if((owner??m.Robot.Engine.Robot)!.CarryingOwner!.CarriedObjectId!=id)
        {
            // Native pre-action type4 (Rolling), not the legacy manual adapter's Docking0.
            Drive=owner is null ? new(m,id,PreActionType.Rolling) : new(m,owner,id,PreActionType.Rolling,AiExecutionCurrent);
            Drive.Unavailable=ReportUnavailable;
            DriveAndWait=new(()=>m.Robot.Engine.Timer.SecondsF){SourceLog=SourceLog};DriveAndWait.SeedNativeEmptyCompletion();
            DriveAndWait.AddAction(Drive,false,false);
            DriveAndWait.AddAction(new NativeLambda(m,()=>Pop.CheckPreActionPose=false) { ScopedOwner=owner, ScopedParentCurrent=owner is null ? null : AiExecutionCurrent },false,false);
            AddAction(DriveAndWait,true,false);
            if(new Radians(maximum)>new Radians(0))
            {
                Face=new(m,maximum,true);AddAction(Face,true,false);
                AddAction(new NativeTurnTowardsObjectRunner(m,id,NativeTurnTowardsObjectRunner.AnyCode,maximum,false,false),true,false);
            }
        }
        AddAction(new NativeLambda(m,()=>BeforeDock?.Invoke()) { ScopedOwner=owner, ScopedParentCurrent=owner is null ? null : AiExecutionCurrent },false,false);
        AddAction(Pop,false,false);SetNativeCompletionProxy(Pop.Tag);
    }
    internal void SetNameTriggers(int named,int unnamed)=>Face?.SetNativeNameTriggers(named,unnamed);
    private sealed class NativeLambda(ManipulationSystem m,Action callback):NativeVisionRunner(m,0x33,0)
    {
        protected override string ActionName=>"WaitForLambda";
        public override byte? GetCompletionVariant()=>6;
        public override int Init()=>0;
        public override uint CheckIfDone(){if(!ScopedCurrent)return EngineActionResult.Cancelled;callback();return 0;}
    }
}
