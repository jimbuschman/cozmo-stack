using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Manipulation;

// Proposed Roll R9:0055C9E6..0055CB26. Real IDrive nine-argument tuple, native dock proxy.
internal sealed class NativeDriveToRollRunner:CompoundActionSequential
{
    private readonly ManipulationSystem _m;
    internal NativeDriveToObjectRunner? Drive {get;}
    internal NativeRollObjectRunner Roll {get;}
    internal CompoundActionSequential? DriveAndWait {get;}
    internal Action? BeforeDock {get;set;}
    internal Action<NativeActionUnavailable>? Unavailable;
    internal float RequestedFaceTurn {get;private set;}
    protected override string ActionName=>"DriveToRollObject";
    public override string? NativeName=>ActionName;
    internal NativeDriveToRollRunner(ManipulationSystem m,uint id):base(()=>m.Robot.Engine.Timer.SecondsF)
    {
        _m=m;Type=5;SourceLog=(l,c,k,t)=>m.Robot.Engine.EmitSourceLog(l,c,k,t);SeedNativeEmptyCompletion();
        if(m.Robot.Engine.Robot!.CarryingOwner!.CarriedObjectId!=id)
        {
            Drive=new(m,id,PreActionType.Rolling);Drive.Unavailable=error=>Unavailable?.Invoke(error);
            DriveAndWait=new(()=>m.Robot.Engine.Timer.SecondsF){SourceLog=SourceLog};DriveAndWait.SeedNativeEmptyCompletion();
            DriveAndWait.AddAction(Drive,false,false);DriveAndWait.AddAction(new NativeLambda(m,()=>{Roll.CheckPreActionPose=false;}),false,false);
            AddAction(DriveAndWait,true,false);
        }
        // Caller maximum is Radians0: the native ctor builds no face/object-turn pair.
        Roll=new(m,id);Roll.Unavailable=error=>Unavailable?.Invoke(error);
        AddAction(new NativeLambda(m,()=>BeforeDock?.Invoke()),false,false);
        AddAction(Roll,false,false);SetNativeCompletionProxy(Roll.Tag);
    }
    internal void RollToUpright()
    {
        if(State==EngineActionResult.Running)return;
        if(Drive is not null&&NativeRollGeometry.TryApproach(_m,Roll.Action.ObjectId,out float angle))
        {Drive.Action.UseApproachAngle=true;Drive.Action.ApproachAngleRad=angle;}
    }
    internal void SetFaceParameters(float maximum,int named,int unnamed)
    {
        RequestedFaceTurn=maximum;
        _m.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,"Actions","IDriveToInteractWithObject.SetMaxTurnTowardsFaceAngle.NullActions","Cannot set angle of null actions: constructor maximum was zero");
        //0055BAF4: setters cannot create absent face/object-turn children. Named triggers have no face receiver.
    }
    private sealed class NativeLambda(ManipulationSystem m,Action callback):NativeVisionRunner(m,0x33,0)
    {
        protected override string ActionName=>"WaitForLambda";
        public override byte? GetCompletionVariant()=>6;
        public override int Init()=>0;
        public override uint CheckIfDone(){callback();return 0;}
    }
}
