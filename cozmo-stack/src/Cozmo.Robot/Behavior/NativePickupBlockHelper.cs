using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

//005B7A4C, PK1-PK10: helper owns the source state, parent owns every runner/tag.
internal sealed class NativePickupBlockHelper: IHelper
{
    private readonly ManipulationSystem _m;
    private readonly SteppedBehavior _parent;
    internal uint Target {get;}
    internal float MaxTurn {get;}
    internal bool Flag104 {get;}
    internal bool Flag105 {get;}
    internal uint Attempts {get;private set;}
    internal bool AlternateRetry {get;private set;}
    private Action<RobotCompletedAction>? _responseCallback;
    private Func<uint,byte>? _converter;
    internal NativePickupBlockHelper(ManipulationSystem m,SteppedBehavior parent,BehaviorHelperFactory factory,uint target,float maxTurn,bool flag104,bool flag105)
        :base(m.Robot,"PickupBlock",parent,factory)
    {_m=m;_parent=parent;Target=target;MaxTurn=maxTurn;Flag104=flag104;Flag105=flag105;}
    public override bool ShouldCancelDelegates(CozmoRobot robot)=>false;
    protected override BehaviorStatus Init(CozmoRobot robot)
    {Attempts=0;AlternateRetry=false;StartPickup(false);return Status;}
    protected override BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot)=>Status;
    private void Missing(string text)=>SteppedBehavior.ReportMissing("PickupBlockHelper "+text);
    internal void StartPickup(bool forceDrive)
    {
        if(forceDrive){Drive(true);return;}
        var owner=_m.Robot.Engine.Robot;
        if(owner?.StoredState is not {} state||_m.Vision.Calibration is not {} calibration)
        {Missing("visual pre-action005B7B48 actual Robot clock/calibration input unavailable; no direct pickup or replacement drive");return;}
        using var main=new Pose3d(owner.GetCheckedMainPose());
        var pre=PlaceRelObjectOffsetPoses.IsAtPreActionPoseWithVisualVerification(_m.World.GetLocatedObjectByIdHelper(Target),PreActionType.Docking,0,0,state.Timestamp,main,
            owner.CarryingOwner?.CarriedObjectId,calibration,ObjectPoseQueries.Engine,()=>_m.GetObstacles(main));
        if(pre!=ActionResult.Success){Drive(false);return;}
        Missing("native engine-ticked PickupObject/TurnTowardsLastFace/TurnTowardsObject runner recipients unavailable; execution preflight retains prior selection, no async substitute");

    }
    internal CompoundActionSequential BuildPickupCompound(ActionRunner pickup,ActionRunner? lastFace,ActionRunner? objectTurn)
    {
        var compound=new CompoundActionSequential(()=>_m.Robot.Engine.Timer.SecondsF,null);
        if(Attempts==0&&MathF.Abs(MaxTurn)>=BitConverter.UInt32BitsToSingle(0x3727C5AC))
        {
            if(lastFace is null||objectTurn is null)throw new NotSupportedException("Native first-attempt turn runner inputs unavailable");
            compound.AddAction(lastFace,true,false);compound.AddAction(objectTurn,true,false);
        }
        compound.AddAction(pickup,false,false);compound.SetNativeCompletionProxy(pickup.Tag);
        return compound;
    }
    private void Drive(bool force)
    {
        Missing("DriveToHelper005B7B5A tuple(type0,force="+force+",angle0,false,offset0/0), success re-entry/failed status0 known; concrete child recipient unavailable; no always-drive substitute");
    }
    private static byte ConvertResult(uint r)=>r is 0x04000003 or 0x04000004 or 0x04000005 or 0x03000015?(byte)1:r==0?(byte)3:(byte)0;
    internal void StartWithResponse(IActionRunner runner,Action<RobotCompletedAction> callback)
    {
        _responseCallback=callback;_converter=ConvertResult;
        _parent.StartActing(runner,completed=>
        {
            byte result=_converter!(completed.Result);
            var director=_m.Robot.Engine.Robot?.NativeBehaviorRuntime?.AI.PickupResponses;
            if(director is null){Missing("actual AI+C response director unavailable; original callback retained");return;}
            int trigger=result==3?0x23F:director.Trigger(result);
            void Complete()
            {var saved=_responseCallback;_responseCallback=null;_converter=null;saved?.Invoke(completed);}
            if(trigger==0x23F)Complete();
            else
            {
                _parent.StartActing(new TriggerAnimationAction(_m.Robot,new((AnimationTrigger)trigger,1,true,0,60f,false)),_=>Complete());
                _converter=null; //005B7482..7524: converter cleared after scheduling, before the response ends.
            }
        });
    }
    internal void Respond(RobotCompletedAction completed)
    {
        uint r=completed.Result;
        switch(r)
        {
            case 0:StoreStatus(BehaviorStatus.Complete);return;
            case 0x02000000:return;
            case 0x03000004:StoreStatus(BehaviorStatus.Failure);return;
            case 0x03000010:_m.Whiteboard.NoPreDockPosesObjectId=Target;return;
            case 0x04000001:Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();StoreStatus(BehaviorStatus.Failure);return;
            case 0x0300001D:
                Missing("generated objectInteractionCompleted payload byte15 absent from current completion carrier; SearchForBlock(minSeen0,maxStage0) recipient not substituted");return;
        }
        if(r is 0x04000003 or 0x04000004 or 0x04000005 or 0x03000015)
        {
            if(Attempts<=1){StartPickup(false);return;}
            if(Flag105&&!AlternateRetry){AlternateRetry=true;Attempts=0;StartPickup(true);return;}
            Mark();StoreStatus(BehaviorStatus.Failure);return;
        }
        if((r>>24)==4){StartPickup(false);return;}
        Mark();StoreStatus(BehaviorStatus.Failure);
    }
    private void Mark()
    {if(_m.World.GetLocatedObjectByIdHelper(Target) is {} target)_m.Whiteboard.SetFailedToUse(target,ObjectActionFailure.PickUpObject);}
}

internal sealed class NativePickupResponseDirector
{
    internal int Trigger(byte result)=>result switch {1=>0x1FA,0=>0x1F9,_=>0x23F}; //0056D86C/0056D904, finite actual AI+C owner.
}

