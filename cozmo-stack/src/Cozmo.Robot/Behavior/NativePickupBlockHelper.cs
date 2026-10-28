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
    internal NativeSearchForBlockHelper? SearchHelper {get;private set;}
    internal NativeDriveToHelper? DriveHelper {get;private set;}
    private Action<RobotCompletedAction>? _responseCallback;
    private Func<uint,byte>? _converter;
    private bool _hostUnavailable;
    private int _availabilityEpoch;
    internal NativePickupBlockHelper(ManipulationSystem m,SteppedBehavior parent,BehaviorHelperFactory factory,uint target,float maxTurn,bool flag104,bool flag105)
        :base(m.Robot,"PickupBlock",parent,factory)
    {_m=m;_parent=parent;Target=target;MaxTurn=new Radians(maxTurn).Value;Flag104=flag104;Flag105=flag105;}
    public override bool ShouldCancelDelegates(CozmoRobot robot)=>false;
    protected override void OnInitializeOnStackSlot20(){Interlocked.Increment(ref _availabilityEpoch);_hostUnavailable=false;}
    protected override void OnStopSlot24(bool first)=>Interlocked.Increment(ref _availabilityEpoch);
    protected override BehaviorStatus Init(CozmoRobot robot)
    {Attempts=0;AlternateRetry=false;StartPickup(false);return Status;}
    protected override BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot)=>Status;
    private void Missing(string text)=>SteppedBehavior.ReportMissing("PickupBlockHelper "+text);
    internal void StartPickup(bool forceDrive)
    {
        if(_hostUnavailable)return;
        try{StartPickupAvailable(forceDrive);}
        catch(NativeActionUnavailable error)
        {
            _hostUnavailable=true;_responseCallback=null;_converter=null;
            Missing("M12-031 visual pre-action005B7B48 unavailable: "+error.Message);
            int epoch=Volatile.Read(ref _availabilityEpoch);uint tag=_parent.CurrentRunnerTag;
            _m.Robot.Engine.Robot?.ActionList.DeferHostCleanup(()=>
            {
                if(epoch!=Volatile.Read(ref _availabilityEpoch)||!ReferenceEquals(_parent.HelperWeakReference?.Pointer,this)||!_parent.HasLiveHelper||_parent.CurrentRunnerTag!=tag)return;
                var manager=_m.Robot.Engine.Robot?.BehaviorManagerOwner;
                if(ReferenceEquals(manager?.Current,_parent))manager.Stop(BehaviorStopReason.Interrupted,_m.Robot.Engine.Timer.SecondsF);
                else _parent.Stop(BehaviorStopReason.Interrupted);
            });
        }
    }
    private void StartPickupAvailable(bool forceDrive)
    {
        if(forceDrive){Drive(true);return;}
        var owner=_m.Robot.Engine.Robot;
        if(owner?.StoredState is not {} state||_m.Vision.Calibration is not {} calibration)
        {Missing("visual pre-action005B7B48 actual Robot clock/calibration input unavailable; no direct pickup or replacement drive");return;}
        using var main=new Pose3d(owner.GetCheckedMainPose());
        var pre=PlaceRelObjectOffsetPoses.IsAtPreActionPoseWithVisualVerification(_m.World.GetLocatedObjectByIdHelper(Target),PreActionType.Docking,0,0,state.Timestamp,main,
            owner.CarryingOwner?.CarriedObjectId,calibration,ObjectPoseQueries.Engine,()=>_m.NativeObstacles(main));
        if(pre!=ActionResult.Success){Drive(false);return;}
        var pickup=new NativePickupObjectRunner(_m,Target);
        NativeLastFaceRunner? face=null;NativeTurnTowardsObjectRunner? turn=null;
        if(Attempts==0&&MathF.Abs(MaxTurn)>=BitConverter.UInt32BitsToSingle(0x3727C5AC))
        {
            face=new(_m,MaxTurn,Flag104,0x187,0x188);
            turn=new(_m,Target,NativeTurnTowardsObjectRunner.AnyCode,Radians.Pi,false,false);
        }
        var compound=BuildPickupCompound(pickup,face,turn);
        StartWithResponse(compound,Respond);Attempts=unchecked(Attempts+1);

    }
    internal CompoundActionSequential BuildPickupCompound(ActionRunner pickup,ActionRunner? lastFace,ActionRunner? objectTurn)
    {
        var compound=new PickupSequence(this);
        if(Attempts==0&&MathF.Abs(MaxTurn)>=BitConverter.UInt32BitsToSingle(0x3727C5AC))
        {
            if(lastFace is null||objectTurn is null)throw new NotSupportedException("Native first-attempt turn runner inputs unavailable");
            compound.AddAction(lastFace,true,false);compound.AddAction(objectTurn,true,false);
        }
        compound.AddAction(pickup,false,false);compound.SetNativeCompletionProxy(pickup.Tag);
        return compound;
    }
    private sealed class PickupSequence:CompoundActionSequential
    {
        private readonly NativePickupBlockHelper _helper;
        private bool _hostFailurePending;
        internal PickupSequence(NativePickupBlockHelper helper):base(()=>helper._m.Robot.Engine.Timer.SecondsF){_helper=helper;}
        public override uint Update()
        {
            if(_hostFailurePending&&State==EngineActionResult.Running)return EngineActionResult.Running;
            int depth=Watcher?.HostUpdateDepth??0;
            try{return base.Update();}
            catch(NativeActionUnavailable error)
            {
                Watcher?.UnwindUnavailableHostUpdate(depth);
                _hostFailurePending=true;
                var h=_helper;h._responseCallback=null;h._converter=null;
                SteppedBehavior.ReportMissing(error.ToString());
                h._m.Robot.Engine.Robot!.ActionList.DeferHostCleanup(()=>
                {
                    if(h._parent.CurrentRunnerTag!=Tag)return;
                    var manager=h._m.Robot.Engine.Robot?.BehaviorManagerOwner;
                    if(ReferenceEquals(manager?.Current,h._parent))manager.Stop(BehaviorStopReason.Interrupted,h._m.Robot.Engine.Timer.SecondsF);
                    else h._parent.Stop(BehaviorStopReason.Interrupted);
                    h._m.Robot.Engine.Robot?.ActionList.Cancel(Tag); // Only this unsupported owned subtree; callbacks were cleared before cancellation.
                });
                return EngineActionResult.Running; //host teardown pending, never interpreted as native completion/failure.
            }
        }
    }
    private void Drive(bool force)
    {
        var drive=Factory.CreateNativeDriveTo(_m,_parent,Target,force);DriveHelper=(NativeDriveToHelper)drive.Pointer;
        Delegation.Delegate=drive;
        Delegation.OnSuccessHandler=_=>{StartPickup(false);return Status;};
        Delegation.OnFailureHandler=_=>BehaviorStatus.Failure;
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
                if(completed.ObjectInteraction is not {} interaction)
                {Missing("PK9 defined internal completion flag unavailable; no invented zero/Search delegation");return;}
                if(interaction.Flag){Mark();StoreStatus(BehaviorStatus.Failure);return;}
                var search=Factory.CreateNativeSearchForBlock(_m,_parent,Target);
                SearchHelper=(NativeSearchForBlockHelper)search.Pointer;
                Delegation.Delegate=search;
                Delegation.OnSuccessHandler=_=>{StartPickup(false);return Status;};
                Delegation.OnFailureHandler=_=>{Mark();return BehaviorStatus.Failure;};
                return;
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
