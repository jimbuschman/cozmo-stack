using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// Proposed derived lifecycle owner, BUILD-PYRAMID-1 E1..A4; no assigned record.
internal sealed class NativeBuildPyramidBehavior:NativeBuildPyramidBaseBehavior
{
    private static readonly ReactionLockTable PlacementLock=new("BuildPyramid","0x00C6C691",new[]{8});
    internal NativeBuildPyramidBehavior(BehaviorFactoryContext source,JsonElement json):base(source,json,"BuildPyramid")
    {
        // E1:005DBCA4..BCB2, bound recipient005DBCF9.
        ObservationFinished=_=>PickupTop();
    }
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        try{RefreshTargets();return StaticId!=uint.MaxValue&&BaseId!=uint.MaxValue&&TopId!=uint.MaxValue;}
        catch(NativeConfigurationUnavailable error){ReportMissing(error.Message);return false;}
    }
    private uint Carry=>(_robot.CarryingOwner??throw new NativeConfigurationUnavailable("BuildPyramid actual carry owner unavailable")).CarriedObjectId??uint.MaxValue;
    // I1..I3:005DBF04..BF84. Cache unavailable never becomes an empty cache.
    protected override void OnStart()
    {
        ResetBehavior();
        if(StopWhenFull&&Full().Count!=0){StopWhenFull=false;React();return;}
        StopWhenFull=false;
        if(Bases().Count!=0||Full().Count!=0){if(Carry!=uint.MaxValue)PlaceTop();else PickupTop();}
        else if(Carry!=uint.MaxValue)Place();else Pickup();
    }
    // D1/D2:005DBD20..BD9C, actual shared helper with one-use trigger0x1A.
    internal void PickupTop()
    {
        State=3;SetStateName("DrivingToTopBlock");
        PickupHelper=new(_m,this,AI.Helpers.Factory,TopId,0f,false,false,0x1A);
        IHelper? raw=PickupHelper;var handle=AI.Helpers.AddHelperToComponent(ref raw);
        try{SmartDelegateToHelper(handle,_=>PlaceTop(),null);}finally{handle.Release();}
    }
    // fidelity: M12-028, M12-047 -- P1..P6:005DC0B4..DC304.
    internal void PlaceTop()
    {
        State=5;SetStateName("PlacingTopBlock");Scope.SmartDisableReactionsWithLock(Id,PlacementLock);
        var stat=_m.World.GetLocatedObjectByIdHelper(StaticId);var basis=_m.World.GetLocatedObjectByIdHelper(BaseId);
        if(stat is null||basis is null)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorBuildPyramid.TransitionToPlacingTopBlock.NullObject",$"Object {unchecked((int)StaticId)} is NULL");
            StaticId=uint.MaxValue;return;
        }
        var entries=Bases().ToArray();
        using var logs=Pose3d.UseSourceLog((l,c,k,t)=>_robot.Engine.EmitSourceLog(l,c,k,t),_robot.Engine.Das.ForceFlushNow);
        foreach(var entry in entries)
        {
            // Keep actual strong refs/order/duplicates; reread ID fields after synchronous delegation.
            if(!entry.ContainsBlock(BaseId)||!entry.ContainsBlock(StaticId))continue;
            using var midpoint=new Pose3d();Pose3d? center=null,relative=null;
            try
            {
                Vec3 xy;
                using(var mutation=_m.World.EnterPoseMutation())
                {
                    if(!new NativeConfigurationGeometry(_m.World,_robot).InteriorMidpoint(stat,basis,midpoint))break;
                    center=PlacementProducerGeometry.Center(stat);relative=new Pose3d();
                    if(!Pose3d.GetWithRespectTo(midpoint,center,relative))break;
                    xy=relative.Translation;
                }
                PlacementHelper=new(_m,this,AI.Helpers.Factory,StaticId,false,(float)xy.X,(float)xy.Y,false);
                IHelper? raw=PlacementHelper;var handle=AI.Helpers.AddHelperToComponent(ref raw);
                try{SmartDelegateToHelper(handle,_=>PlacementSucceeded(),_=>PlacementFailed());}finally{handle.Release();}
            }
            finally{relative?.Dispose();center?.Dispose();}
            // DC2FE..304 continues after every normal delegate return, no winner/dedup rule.
        }
    }
    // fidelity: M12-049 -- actual callback recipients C1..C6; cache producers remain absent.
    internal void PlacementSucceeded()=>React();
    internal void PlacementFailed()
    {
        if(Carry!=uint.MaxValue)return;
        StopWhenFull=true;
        // C4/C5:005DC818..DC976. B1 checked variability0; actual native motion children
        // retain their Init no-send/clamp/ack gates. Lift0 is a height, not preset or speed5.
        var parallel=new CompoundActionParallel(()=>_robot.Engine.Timer.SecondsF);
        parallel.AddAction(new DriveStraightAction(_m,-20f,20f,false),false,false);
        parallel.AddAction(_m.Robot.Motion.CreateNativeHeadChild(BitConverter.UInt32BitsToSingle(0x3EDF66F3),BitConverter.UInt32BitsToSingle(0x3D0EFA35),15f,20f),false,false);
        parallel.AddAction(_m.Robot.Motion.CreateNativeLiftChild(0f,5f),false,false);
        var sequence=new CompoundActionSequential(()=>_robot.Engine.Timer.SecondsF);
        sequence.AddAction(parallel,false,false);
        sequence.AddAction(new NativeTimedWaitRunner(_m,1f),false,false);
        sequence.AddAction(_m.Robot.Motion.CreateNativeHeadChild(BitConverter.UInt32BitsToSingle(0x3E860A92),BitConverter.UInt32BitsToSingle(0x3D0EFA35),15f,20f),false,false);
        StartActing(sequence,null);
    }
    // A1..A4:005DBFB0..DC00A; empty completion callback retains state6.
    internal void React()
    {
        State=6;SetStateName("ReactingToPyramid");
        _robot.Engine.ReportBehaviorObjective(3,"BuiltPyramid",Id);
        NeedActionCompleted();
        StartActing(new TriggerLiftSafeAnimationAction(_m.Robot,new((AnimationTrigger)0x17,1,true,0,60f,false)),null);
    }
}
