using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// fidelity: M13-041 - current ordinary DriveToPop composition; Sparks widening pending.
// POP-A-WHEELIE-1 D1/D2:0055D1E8..D26C; shared IDrive0055B2C8/AddDock0055B7AC.
internal sealed class NativeDriveToPopAWheelieRunner:CompoundActionSequential
{
    internal NativeDriveToObjectRunner? Drive {get;}
    internal NativeLastFaceRunner? Face {get;}
    internal NativePopAWheelieRunner Pop {get;}
    internal CompoundActionSequential? DriveAndWait {get;}
    internal float MaximumFaceTurn {get;}
    internal Action? BeforeDock {get;set;}
    internal Action<NativeActionUnavailable>? Unavailable;
    private bool _unavailable;
    protected override string ActionName=>"DriveToPopAWheelie";
    public override string? NativeName=>ActionName;
    internal NativeDriveToPopAWheelieRunner(ManipulationSystem m,uint id,float maximum):base(()=>m.Robot.Engine.Timer.SecondsF)
    {
        MaximumFaceTurn=maximum;Type=5;SourceLog=(l,c,k,t)=>m.Robot.Engine.EmitSourceLog(l,c,k,t);SeedNativeEmptyCompletion();
        Pop=new(m,id);Pop.Unavailable=error=>Unavailable?.Invoke(error);
        if(m.Robot.Engine.Robot!.CarryingOwner!.CarriedObjectId!=id)
        {
            // Native pre-action type4 (Rolling), not the legacy manual adapter's Docking0.
            Drive=new(m,id,PreActionType.Rolling);Drive.Unavailable=error=>Unavailable?.Invoke(error);
            DriveAndWait=new(()=>m.Robot.Engine.Timer.SecondsF){SourceLog=SourceLog};DriveAndWait.SeedNativeEmptyCompletion();
            DriveAndWait.AddAction(Drive,false,false);
            DriveAndWait.AddAction(new NativeLambda(m,()=>Pop.CheckPreActionPose=false),false,false);
            AddAction(DriveAndWait,true,false);
            if(new Radians(maximum)>new Radians(0))
            {
                Face=new(m,maximum,true);AddAction(Face,true,false);
                AddAction(new NativeTurnTowardsObjectRunner(m,id,NativeTurnTowardsObjectRunner.AnyCode,maximum,false,false),true,false);
            }
        }
        AddAction(new NativeLambda(m,()=>BeforeDock?.Invoke()),false,false);
        AddAction(Pop,false,false);SetNativeCompletionProxy(Pop.Tag);
    }
    internal void SetNameTriggers(int named,int unnamed)=>Face?.SetNativeNameTriggers(named,unnamed);
    public override uint Update()
    {
        if(_unavailable)return EngineActionResult.Running;
        int depth=Watcher?.HostUpdateDepth??0;
        try{return base.Update();}
        catch(NativeActionUnavailable error){Watcher?.UnwindUnavailableHostUpdate(depth);_unavailable=true;Unavailable?.Invoke(error);return EngineActionResult.Running;}
    }
    private sealed class NativeLambda(ManipulationSystem m,Action callback):NativeVisionRunner(m,0x33,0)
    {
        protected override string ActionName=>"WaitForLambda";
        public override byte? GetCompletionVariant()=>6;
        public override int Init()=>0;
        public override uint CheckIfDone(){callback();return 0;}
    }
}
