using Cozmo.Robot.Vision;
using Cozmo.Transport;
namespace Cozmo.Robot.Manipulation;

// fidelity: M13-014, M12-035
// K15-K19: actual9arg IDriveToInteract outer compound, not the public async adapter.
internal sealed class NativeDriveAndFlipRunner:CompoundActionSequential
{
    private readonly ManipulationSystem _m;
    internal NativeDriveToObjectRunner? Drive {get;}
    internal NativeLastFaceRunner? Face {get;}
    internal NativeFlipBlockRunner Flip {get;}
    internal CompoundActionSequential? DriveAndWait {get;}
    internal float MaximumFaceTurn {get;}
    internal Action<NativeActionUnavailable>? Unavailable;
    protected override string ActionName=>"DriveToAndFlipBlock";
    public override string? NativeName=>ActionName;
    internal NativeDriveAndFlipRunner(ManipulationSystem m,uint id,float maximum):base(()=>m.Robot.Engine.Timer.SecondsF)
    {
        _m=m;MaximumFaceTurn=maximum;Type=5;SourceLog=(l,c,k,t)=>m.Robot.Engine.EmitSourceLog(l,c,k,t);SeedNativeEmptyCompletion();
        if(m.Robot.Engine.Robot!.CarryingOwner!.CarriedObjectId!=id)
        {
            Drive=new(m,id,PreActionType.Flipping);Drive.Unavailable=error=>Unavailable?.Invoke(error);
            DriveAndWait=new(()=>m.Robot.Engine.Timer.SecondsF){SourceLog=SourceLog};DriveAndWait.SeedNativeEmptyCompletion();
            DriveAndWait.AddAction(Drive,false,false);DriveAndWait.AddAction(new InteractionWait(m),false,false);
            AddAction(DriveAndWait,true,false);
            if(new Radians(maximum)>new Radians(0))
            {
                Face=new(m,maximum,false);AddAction(Face,true,false);
                AddAction(new NativeTurnTowardsObjectRunner(m,id,NativeTurnTowardsObjectRunner.AnyCode,maximum,false,false),true,false);
            }
        }
        Flip=new(m,id);Flip.Unavailable=error=>Unavailable?.Invoke(error);
        if(Drive is not null)Drive.SetPosesFunction(GetPossiblePoses);
        AddAction(Flip,false,false);SetNativeCompletionProxy(Flip.Tag);
    }
    internal void SetNameTriggers(int named,int unnamed)=>Face?.SetNativeNameTriggers(named,unnamed);
    internal ActionResult GetPossiblePoses(ObservableObject obj,out IReadOnlyList<PreActionPose> poses,ref bool inPosition)
    {
        // L1's incoming vector is freshly empty on Init/Check. The indeterminate+100 branch
        // leaves inPosition=false/Flip.check=true either way; no threshold value is invented.
        poses=Array.Empty<PreActionPose>();
        var owner=_m.Robot.Engine.Robot!;var main=owner.GetCheckedMainPose();
        var output=DockPreActionPoses.Get(new(obj,PreActionType.Flipping,false,FlipBlockAction.InitAngleToleranceRad,0,false,0),main,owner.CarryingOwner!.CarriedObjectId,()=>_m.NativeObstacles(main));
        if(output.Result!=ActionResult.Success)return output.Result;
        using var face=new Pose3d("");bool known=_m.Vision.Faces.CopyNativeLastFace(owner.GetNativeOrigin(owner.CurrentOriginId),true,face);
        if(output.Poses.Count==0)return ActionResult.NoPreActionPoses;
        int a=-1,b=-1;float bestA=float.MaxValue,bestB=float.MaxValue;
        using var rel=new Pose3d("");
        for(int i=0;i<output.Poses.Count;i++)
        {
            output.Poses[i].WorldPose.TryWithRespectTo(main,rel);
            float d=FlipBlockAction.Norm3(rel.Translation);
            if(d<bestA){bestB=bestA;b=a;bestA=d;a=i;}else if(d<bestB){bestB=d;b=i;}
        }
        if(a<0||(output.Poses.Count>1&&b<0))throw new NativeActionUnavailable("M1-034/M12-035 native nearest pose read is undefined for nonordered distances; skipped");
        int chosen=a;
        if(output.Poses.Count>1)
        {
            using var ra=new Pose3d("");using var rb=new Pose3d("");
            if(known&&output.Poses[a].WorldPose.TryWithRespectTo(face,ra)&&output.Poses[b].WorldPose.TryWithRespectTo(face,rb))
                chosen=FlipBlockAction.Norm3(ra.Translation)>FlipBlockAction.Norm3(rb.Translation)?a:b;
            else
            {
                output.Poses[a].WorldPose.TryWithRespectTo(main,ra);output.Poses[b].WorldPose.TryWithRespectTo(main,rb);
                chosen=(float)ra.Translation.Y>=(float)rb.Translation.Y?a:b;
            }
        }
        poses=new[]{output.Poses[chosen]};return ActionResult.Success; // does NOT write inPosition.
    }
    private sealed class InteractionWait:NativeVisionRunner
    {
        internal InteractionWait(ManipulationSystem m):base(m,0x33,0){TimeoutSeconds=float.MaxValue;}
        protected override string ActionName=>"WaitForLambda";
        public override byte? GetCompletionVariant()=>6;
        public override int Init()=>0;
        public override uint CheckIfDone()
        {
            //9arg ctor leaves weak dock null/D4false; defined diagnostic then predicate1.
            M.Robot.Engine.EmitSourceLog(EngineLogLevel.Error,"Actions","IDriveToInteractWithObject.NoDockAction","No dock action");
            EngineErrorState.StoreAndMaybeBreak();return 0;
        }
    }
}
