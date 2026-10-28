using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class HikingRollCubeNativeTests
{
    private static readonly Rotation3d Side=Rotation3d.FromAngleAxis(Radians.Pi*.5f,new(0,1,0));
    private static NativeRollBlockBehavior Roll(Fixture f)=>Assert.IsType<NativeRollBlockBehavior>(f.Runtime.Bound["Hiking_RollCube"]);
    private static ObservableObject Add(Fixture f,uint id,float x,Rotation3d? rotation=null,float z=22)
    {
        var obj=f.Add(id,x,z);obj.Pose=new Pose3d(rotation??Side,new(x,0,z),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));return obj;
    }
    private static NativeRollBlockBehavior Admit(Fixture f,bool hard=false)
    {
        f.Robot.Progression!.Unlocked.Add(9);var manager=f.Robot.BehaviorManagerOwner!;
        if(hard){manager.SparkNameInput=id=>id==9?"RollCube":"Count";manager.SetNativeRequestedSpark(9,0);manager.SwitchToNativeRequestedSpark();}
        // Explicit native manager choice/admission input; this does not assert Hiking/Gather competition or interlude readiness.
        Assert.True(manager.StartAsync("Hiking_RollCube",f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());return Roll(f);
    }
    [Fact]
    public void ActualShippedFactoryDefaultsTrueAndCarriesUnlockAndNeeds()
    {
        using var f=new Fixture();var roll=Roll(f);Assert.True(roll.RotationImportant);Assert.Equal(new[]{7},roll.OwnSlot2C());
        Assert.Equal(9u,roll.NativeBaseInputs!.RequiredUnlock);Assert.Equal((byte?)12,roll.NativeBaseInputs.CheckedExecutableType);
        Add(f,7,200,Rotation3d.Identity);f.Robot.Progression!.Unlocked.Add(9);Assert.False(roll.IsRunnable(f.Runtime.Context));
        Add(f,8,350);f.Rig.Tick();Assert.True(roll.IsRunnable(f.Runtime.Context));Assert.Equal(8u,roll.Target);
        f.Robot.Progression.Unlocked.Remove(9);Assert.False(roll.IsRunnable(f.Runtime.Context));
    }
    [Fact]
    public void SixAndSevenHaveIndependentLazyValidRetainedBestAndFailureMemory()
    {
        using var f=new Fixture();Add(f,7,300,Rotation3d.Identity);var side=Add(f,8,500);var cache=f.Runtime.PickupTargets!;
        Assert.Equal(new uint[]{7,8},cache.GetValid(6));Assert.Equal(new uint[]{8},cache.GetValid(7));Assert.Equal(8u,cache.GetBest(6));
        Add(f,6,100);Assert.DoesNotContain(6u,cache.GetValid(7));f.Rig.Tick();Assert.Contains(6u,cache.GetValid(7));Assert.Equal(8u,cache.GetBest(7));
        f.M.Whiteboard.SetFailedToUse(side,(ObjectActionFailure)3);Assert.Contains(8u,cache.GetValid(7));f.Rig.Tick();Assert.DoesNotContain(8u,cache.GetValid(7));Assert.Equal(6u,cache.GetBest(7));
        Assert.Equal(3,cache.Inspect(7).Rebuilds);Assert.Equal(1,cache.Inspect(6).Rebuilds);
    }
    [Theory]
    [InlineData(PoseState.Known,true)] [InlineData(PoseState.Dirty,true)] [InlineData(PoseState.Unknown,false)]
    public void RollPredicateUsesPoseStateAndActualRootAxis(PoseState state,bool expected)
    {
        using var f=new Fixture();var obj=Add(f,7,200);obj.PoseState=state;
        Assert.Equal(expected,f.Runtime.PickupTargets!.RollEligible(obj,true));
        if(state!=PoseState.Unknown){obj.Pose=new Pose3d(Rotation3d.Identity,new(200,0,22),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));Assert.True(f.Runtime.PickupTargets.RollEligible(obj,false));Assert.False(f.Runtime.PickupTargets.RollEligible(obj,true));}
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ClassOrdinaryAndHardParametersUseNativeHelperAndZeroCtorFaceChildren(bool hard)
    {
        using var f=new Fixture();Add(f,7,200);var roll=Admit(f,hard);
        Assert.Equal(hard?0:BitConverter.UInt32BitsToSingle(0x3FC90FDB),roll.RollHelper!.MaxFaceTurn);
        Assert.True(roll.RollHelper.Upright);Assert.False(roll.PreDockReached);
        var runner=new NativeDriveToRollRunner(f.M,7);runner.SetFaceParameters(roll.RollHelper.MaxFaceTurn,0x1F6,0x1F7);
        try{Assert.DoesNotContain(runner.Children,c=>c is NativeLastFaceRunner or NativeTurnTowardsObjectRunner);Assert.Equal(3,runner.Children.Count);Assert.Equal(2,runner.DriveAndWait!.Children.Count);Assert.False(runner.Drive!.Action.UseApproachAngle);}
        finally{runner.Prep();runner.WatcherEnding();}
    }
    [Fact]
    public void ApproachIsFirstBottomMarkerPoseAndFloatAtan2WithSourceVectorOrder()
    {
        using var f=new Fixture();var obj=Add(f,7,200);var poses=new List<PreActionPose>();
        CubePreActionPoses.GetCurrentPreActionPoses(obj,new[]{PreActionType.Rolling},Array.Empty<MarkerType>(),f.Robot.GetCheckedMainPose(),0,f.M.NativeObstacles(f.Robot.GetCheckedMainPose()),poses);
        try
        {
            var first=poses.First(p=>p.Marker.Face==BlockFace.Bottom);var vector=PlacementProducerGeometry.Vector(obj.NativePose,first.WorldPose);
            Assert.True(NativeRollGeometry.TryApproach(f.M,7,out float angle));Assert.Equal(BitConverter.SingleToUInt32Bits(MathF.Atan2((float)vector.Y,(float)vector.X)),BitConverter.SingleToUInt32Bits(angle));
            var runner=new NativeDriveToRollRunner(f.M,7);try{runner.RollToUpright();Assert.True(runner.Drive!.Action.UseApproachAngle);Assert.Equal(angle,runner.Drive.Action.ApproachAngleRad);}finally{runner.Prep();runner.WatcherEnding();}
        }
        finally{foreach(var p in poses)p.WorldPose.Dispose();}
    }
    [Fact]
    public void RollSelectionDefinedErrorsAndGeometricTopMarkerAreConcrete()
    {
        using var f=new Fixture();var obj=Add(f,7,150);var runner=new NativeRollObjectRunner(f.M,7);
        try
        {
            Assert.Equal(0u,runner.Select(obj));Assert.NotEqual(unchecked((short)obj.Markers.First(m=>m.Face==BlockFace.Top).Code),runner.SavedTopMarker);
            f.Robot.CarryingOwner!.SetCarrying(99);Assert.Equal(0x03000017u,runner.Select(obj));f.Robot.CarryingOwner.UnsetCarrying();
            obj.Pose=new Pose3d(Side,new(150,0,60),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));Assert.Equal(0x03000004u,runner.Select(obj));
            Assert.Equal(0x04000003u,runner.Verify());
        }
        finally{runner.Prep();runner.WatcherEnding();}
    }
    [Fact]
    public void UndefinedRollFullPublicationPreservesLocalDefinedFieldsThroughActualProxy()
    {
        using var f=new Fixture();Add(f,7,150);var runner=new NativeDriveToRollRunner(f.M,7);var local=new List<RobotCompletedAction>();var app=new List<ActionCompletionReport>();
        f.Rig.Engine.ActionCompleted+=app.Add;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)local.Add(r);});
        try
        {
            var payload=runner.Roll.GetObjectInteractionCompletion();Assert.Equal(7u,payload.Word0);Assert.Equal((byte)1,payload.Count);Assert.False(payload.Flag);Assert.False(payload.IsPayloadDefined);Assert.Throws<InvalidOperationException>(()=>payload.PackBody());
            f.Rig.Engine.BroadcastActionCompletion(new(998,0x1F,0x03000004,Array.Empty<uint>(),0,CompletionVariant:0,ObjectInteraction:payload));f.Rig.Engine.DrainAppDelivery();
            Assert.Empty(app);Assert.Equal(payload,Assert.Single(local).ObjectInteraction);
        }
        finally{runner.Prep();runner.WatcherEnding();}
    }
    [Fact]
    public void ActualConfiguredManagerCacheHelperDriveDockVerifyCallbackAndNeedsPipeline()
    {
        using var scene=new RollFirmwareScene();var f=scene.F;bool successAnimationObserved=false;var order=new List<string>();var local=new List<RobotCompletedAction>();var app=new List<ActionCompletionReport>();
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint id&&id==2)order.Add("objective2");});
        f.Rig.Engine.Needs.Log+=line=>{if(line.StartsWith("needs.action_completed RollACube:")){Assert.Equal(new[]{"objective2"},order);var animation=Assert.IsType<TriggerAnimationAction>(Roll(f).OwnedRunner);Assert.Equal((AnimationTrigger)0x1FB,animation.Request.Trigger);Assert.Equal(.8f,(float)Roll(f).RunningScoreBonus);successAnimationObserved=true;order.Add("needs39");}};
        using var completion=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r&&r.ActionType==0x1F)local.Add(r);});f.Rig.Engine.ActionCompleted+=r=>{if(r.ActionType==0x1F)app.Add(r);};
        var roll=Admit(f);for(int i=0;i<200&&order.Count<2;i++)scene.Step();
        Assert.True(order.Count==2,string.Join(" | ",scene.Trace.TakeLast(30)));Assert.Equal(new[]{"objective2","needs39"},order);Assert.True(roll.PreDockReached);
        Assert.Equal(1,scene.Rolls);var dock=Assert.Single(f.Rig.SentSince(0).OfType<DockWithObject>());Assert.Equal((byte)5,dock.DockAction);Assert.Equal((byte)0,dock.DockingMethod);Assert.Equal((byte)0,dock.Field6);Assert.False(dock.Field8);
        Assert.Equal(1u,roll.RollHelper!.Attempts);Assert.Contains(local,r=>r.Result==0&&r.ObjectInteraction is {Word0:var target,Count:1,Flag:false,IsPayloadDefined:false}&&target==scene.Target);
        f.Rig.Engine.DrainAppDelivery();Assert.Empty(app);Assert.Equal(1,roll.State);Assert.True(successAnimationObserved);
    }

    private sealed class Parent:SteppedBehavior
    {
        internal Action<Parent>? Entry;
        internal Parent():base("roll-join-probe","Probe"){}
        public override bool IsRunnable(BehaviorContext context)=>true;
        protected override bool KeepsRunningWithoutAction=>true;
        protected override void OnStart()=>Entry?.Invoke(this);
        internal bool Delegate(HelperRef helper)=>SmartDelegateToHelper(helper,null,null);
    }
    private static void Start(Fixture f,Parent parent)
    {parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());}
    private static void Stop(Fixture f,Parent parent)
    {f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();}
    private sealed class ResultInput(uint result):ActionRunner(0x1F,0)
    {
        protected override string ActionName=>"explicit raw Roll result input";
        public override uint CheckIfDone()=>result;
        public override bool CanInterrupt()=>false;
        public override ObjectInteractionCompletion GetObjectInteractionCompletion()=>new(7,0,0,0,0,1,false,false);
    }
    [Theory]
    [InlineData(0x04000003u,0x1FA)] [InlineData(0x0300001Du,0x1F9)] [InlineData(0x02000000u,0x1F9)] [InlineData(0u,0x23F)]
    public void ActualDirectorDefersOriginalScalarUntilResponseRegardlessOfResponseResult(uint result,int trigger)
    {
        // Explicit scalar callback boundary input, not a successful Roll execution claim.
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);var parent=new Parent();
        var helper=new NativeRollBlockHelper(f.M,parent,f.Runtime.AI.Helpers.Factory,7,true,0,null);var original=new ResultInput(result);RobotCompletedAction? completed=null;
        parent.Entry=_=>helper.StartWithResponse(original,r=>completed=r);Start(f,parent);
        try
        {
            for(int i=0;i<8&&parent.OwnedRunner==original;i++)f.Rig.Tick();
            if(trigger==0x23F){Assert.NotNull(completed);Assert.Null(parent.OwnedRunner);}
            else
            {
                Assert.Null(completed);var response=Assert.IsType<TriggerAnimationAction>(parent.OwnedRunner);Assert.Equal((AnimationTrigger)trigger,response.Request.Trigger);
                // Cancellation of the RESPONSE is a real ActionList input; original scalar must survive it.
                f.Robot.ActionList.Cancel(response.Tag);f.Robot.ActionList.Watcher.Update();
                for(int i=0;i<8&&completed is null;i++)f.Rig.Tick();
            }
            Assert.NotNull(completed);Assert.Equal(result,completed.Value.Result);Assert.Equal(original.Tag,completed.Value.Tag);Assert.False(completed.Value.ObjectInteraction!.IsPayloadDefined);
        }
        finally{Stop(f,parent);}
    }
    [Fact]
    public void DisabledPreactionUsesCurrentObservedMarkersAndRealVisualPrefix()
    {
        using var f=new Fixture();var obj=Add(f,7,150);var runner=new NativeRollObjectRunner(f.M,7){CheckPreActionPose=false};
        try{Assert.Equal(0,runner.Init());Assert.Collection(runner.Prepared!.Children,c=>Assert.IsType<NativeVisuallyVerifyNoObjectRunner>(c),c=>Assert.IsType<NativeTurnTowardsObjectRunner>(c));Assert.Empty(f.Rig.SentSince(0).OfType<DockWithObject>());}
        finally{runner.Prep();runner.WatcherEnding();}
        foreach(var marker in obj.Markers)marker.LastObservedTimestamp=0;
        var absent=new NativeRollObjectRunner(f.M,7){CheckPreActionPose=false};try{Assert.Equal(0x0300001D,absent.Init());Assert.Empty(f.Rig.SentSince(0).OfType<DockWithObject>());}finally{absent.Prep();absent.WatcherEnding();}
    }
    [Fact]
    public void SelectionHeightUsesSavedPoseWithRespectToActualMainParent()
    {
        using var f=new Fixture();var origin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        using var parent=new Pose3d(Rotation3d.Identity,new(0,0,70),parent:origin);var obj=Add(f,7,150);
        obj.Pose=new Pose3d(Side,new(150,0,-48),parent:parent);var runner=new NativeRollObjectRunner(f.M,7);
        try{Assert.Equal(0u,runner.Select(obj));obj.Pose=new Pose3d(Side,new(150,0,22),parent:parent);Assert.Equal(0x03000004u,runner.Select(obj));}
        finally{runner.Prep();runner.WatcherEnding();}
    }
    [Fact]
    public void PlaceBlockDrawsTwoCanonicalDoublesThenRealTurnAndGroundIgnoringTurnFailure()
    {
        using var f=new Fixture();var parent=new Parent();NativePlaceBlockHelper? helper=null;int words=0;
        f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        parent.Entry=p=>{helper=new(f.M,p,f.Runtime.AI.Helpers.Factory);IHelper? raw=helper;var h=f.Runtime.AI.Helpers.AddHelperToComponent(ref raw);try{p.Delegate(h);}finally{h.Release();}};
        Start(f,parent);
        try
        {
            Assert.Equal(4,words);Assert.Equal(-(float)BitConverter.UInt64BitsToDouble(0x3FE921FB54442D18),helper!.Angle);
            var turn=Assert.IsType<NativeTurnInPlaceRunner>(parent.OwnedRunner);Assert.Equal(1,helper.Status==BehaviorStatus.Running?1:0);
            // Real cancellation invokes source result-insensitive Turn callback, then actual Ground's defined not-carrying error.
            f.Robot.ActionList.Cancel(turn.Tag);f.Robot.ActionList.Watcher.Update();
            for(int i=0;i<12&&helper.Status==BehaviorStatus.Running;i++)f.Rig.Tick();
            Assert.NotNull(helper.Placement);Assert.Equal(BehaviorStatus.Complete,helper.Status);Assert.Empty(f.Rig.SentSince(0).OfType<PlaceObjectOnGround>());
            Assert.Equal(0x03000011u,helper.Placement!.State);Assert.Equal(4,words);
        }
        finally{f.Rig.Engine.ContextRandom.WordSourceForTest=null;Stop(f,parent);}
    }
    [Fact]
    public void CantInteractPreservesRunningAndHasNoFailureMarkOrOwnedAction()
    {
        using var f=new Fixture();Add(f,7,200,z:150);var parent=new Parent();NativeRollBlockHelper? helper=null;
        parent.Entry=p=>{helper=new(f.M,p,f.Runtime.AI.Helpers.Factory,7,true,0,null);IHelper? raw=helper;var h=f.Runtime.AI.Helpers.AddHelperToComponent(ref raw);try{p.Delegate(h);}finally{h.Release();}};
        Start(f,parent);
        try
        {
            for(int i=0;i<12;i++)f.Rig.Tick();Assert.Equal(BehaviorStatus.Running,helper!.Status);Assert.Equal(0u,helper.Attempts);Assert.Null(parent.OwnedRunner);
            Assert.Empty(f.M.Whiteboard.GetObjectFailureTable((ObjectActionFailure)3));Assert.Empty(f.Rig.SentSince(0).OfType<DockWithObject>());
        }
        finally{Stop(f,parent);}
    }
    [Fact]
    public void AxisChangeOnlyRetargetsWhenActualRobotDistanceIsStrictlyOver14400()
    {
        using var f=new Fixture();var obj=Add(f,7,120);Add(f,8,400);var roll=Admit(f);var first=roll.RollHelper;
        // Explicit new world observations, not production-output geometry. Equality at(120,0,0) does not retarget.
        obj.Pose=new Pose3d(Rotation3d.Identity,new(120,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));f.Rig.Tick();Assert.Same(first,roll.RollHelper);
        obj.Pose=new Pose3d(Rotation3d.Identity,new(121,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));f.Rig.Tick();Assert.Equal(8u,roll.Target);Assert.NotSame(first,roll.RollHelper);Assert.False(roll.PreDockReached);Assert.Equal(0,roll.State);
    }
}


