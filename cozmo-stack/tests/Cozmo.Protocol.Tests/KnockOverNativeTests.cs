using System.Buffers.Binary;
using System.Reflection;
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
public sealed class KnockOverNativeTests
{
    private static NativeKnockOverCubesBehavior Knock(Fixture f)=>Assert.IsType<NativeKnockOverCubesBehavior>(f.Runtime.Bound["KnockOverCubes"]);
    private static NativeKnockOverCubesBehavior Stack(Fixture f,float x=300)
    {
        f.Add(7,x,22);f.Add(8,x,66);f.Add(9,x,110);f.Robot.Progression!.Unlocked.Add(5);
        f.Runtime.Configurations.ForceUpdate();f.Runtime.Configurations.Update();Assert.Single(f.Runtime.Configurations.Snapshot.Stacks);
        return Knock(f);
    }
    private static void Spark(Fixture f,int id,byte soft)
    {
        // supplied entry at checked native switch leaf, not a recovered activity picker/admission.
        var manager=f.Robot.BehaviorManagerOwner!;manager.SparkNameInput=_=>"supplied-input";manager.SetNativeRequestedSpark(id,soft);manager.SwitchToNativeRequestedSpark();
    }
    private static void Start(Fixture f,NativeKnockOverCubesBehavior knock)
    {Assert.True(knock.IsRunnable(f.Runtime.Context));Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(knock.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());}
    [Theory]
    [InlineData(85,0,false)] [InlineData(5,0,true)] [InlineData(5,1,false)]
    public void ConfiguredFactoryUsesCanonicalStackProgressionAndHardSpark(int spark,byte soft,bool hard)
    {
        // reach: configured factory/manager/ActionList, supplied native world + progression + checked spark-switch inputs. K1-K8.
        using var f=new Fixture();var knock=Stack(f);Assert.Equal((byte)3,knock.MinimumHeight);
        f.Robot.Progression!.Unlocked.Remove(5);Assert.False(knock.IsRunnable(f.Runtime.Context));f.Robot.Progression.Unlocked.Add(5);
        Spark(f,spark,soft);Start(f,knock);Assert.Equal(hard,knock.HardSpark);
        Assert.Equal(hard?"KnockingOverStack":"ReachingForBlock",knock.Phase);Assert.Equal(7u,knock.Bottom);Assert.Equal(8u,knock.Middle);Assert.Equal(9u,knock.Top);
        Assert.NotNull(f.Robot.ActionList.GetCurrentAction());Assert.Null(f.Robot.BehaviorManagerOwner!.Activity);
        if(hard){Assert.Equal(0f,knock.Interaction!.MaximumFaceTurn);Assert.Null(knock.Interaction.Face);}
        f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,0);Assert.Equal(uint.MaxValue,knock.Bottom);
    }
    [Fact]
    public void ReachCapturesTopRangeInMainFrameBeforeBottomTurnExecutes()
    {
        using var f=new Fixture();var knock=Stack(f);var top=f.M.World.GetLocatedObjectByIdHelper(9)!;
        using var parent=new Pose3d(Rotation3d.Identity,new(100,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        top.Pose=new Pose3d(Rotation3d.Identity,new(210,0,110),parent:parent);
        Start(f,knock);var children=knock.LastSequence!.Children;
        Assert.Equal(new[]{0x2C,8,0x1A},children.Select(c=>c.Type));
        Assert.Equal(7u,(uint)typeof(NativeTurnTowardsObjectRunner).GetField("_id",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(children[0])!);
        var drive=Assert.IsType<DriveStraightAction>(children[1]);Assert.Equal(225f,drive.DistanceMm);Assert.Equal(60f,drive.SpeedMmps);Assert.True(drive.ShouldPlayAnimation);
        Assert.Equal(AnimationTrigger.KnockOverGrabAttempt,Assert.IsAssignableFrom<TriggerAnimationAction>(children[2]).Request.Trigger);
        Assert.Equal(0,knock.Attempts);
    }
    [Fact]
    public void CachedWeakExpiresOnSourceOwnerRemovalAndStopDoesNotAutoRefreshForResume()
    {
        // reach: unit scoped Init/Resume, deliberately bypasses manager IsRunnable refresh after native owner expiry.
        using var f=new Fixture();var knock=Stack(f);Assert.True(knock.IsRunnable(f.Runtime.Context));var original=Assert.Single(f.Runtime.Configurations.Snapshot.Stacks);
        f.M.World.GetLocatedObjectByIdHelper(9)!.Pose=new Pose3d(Rotation3d.Identity,new(800,0,110),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        f.Runtime.Configurations.ForceUpdate();f.Runtime.Configurations.Update();Assert.DoesNotContain(f.Runtime.Configurations.Snapshot.Stacks,s=>ReferenceEquals(s,original));Assert.Equal(2,Assert.Single(f.Runtime.Configurations.Snapshot.Stacks).BlockIds.Count);
        using var scope=new BehaviorScope(motion:f.Rig.Robot.Motion,manager:f.Robot.BehaviorManagerOwner);
        knock.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();Assert.True(knock.InitFailed);Assert.False(knock.EngineRunning);Assert.False(knock.HasCurrentAction);Assert.Null(f.Robot.ActionList.GetCurrentAction());
        // Own Stop clears weak + frozen IDs. No implicit IsRunnable/refresh inside Resume.
        using var g=new Fixture();var other=Stack(g);Start(g,other);g.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted,0);
        Assert.True(other.Resume(null,g.Rig.Engine.Timer.SecondsF));Assert.False(other.EngineRunning);Assert.Equal(uint.MaxValue,other.Top);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ActualUpAxisProducerPublishesOneRawAndTypedViewThenLocalBeforeApi(bool external)
    {
        // reach: unit actual native raw-message handler, real connected owner, no invented robot-wire11. K12/K24.
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);var connected=Assert.Single(f.M.World.ConnectedObjects);var order=new List<string>();byte[]? bytes=null;
        f.Rig.Engine.DrainAppDelivery();f.Robot.HasExternalInterface=external;uint count=f.Rig.Engine.NativeUiDeliveryCount;
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x11){bytes=body.ToArray();order.Add("raw");}};
        f.Rig.Robot.Cubes.UpAxisBroadcast+=notice=>{Assert.Equal(unchecked((int)connected.ObjectId),notice.ObjectID);order.Add("typed");};
        using var local=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x11,true,p=>{var notice=Assert.IsType<CubeUpAxisEvent>(p);Assert.Equal(0x12345678u,notice.Timestamp);Assert.Empty(order);order.Add("local");});
        f.Rig.Robot.Cubes.Handle(new ObjectUpAxisChanged{Timestamp=0x12345678,ObjectID=unchecked((uint)connected.ActiveId),UpAxis=UpAxis.ZPositive});
        Assert.Equal(external?new[]{"local"}:Array.Empty<string>(),order);Assert.Equal(count+(external?1u:0u),f.Rig.Engine.NativeUiDeliveryCount);
        f.Rig.Engine.DrainAppDelivery();
        if(external){Assert.Equal(new[]{"local","raw","typed"},order);Assert.NotNull(bytes);Assert.Equal(9,bytes!.Length);Assert.Equal(0x12345678u,BinaryPrimitives.ReadUInt32LittleEndian(bytes));Assert.Equal(connected.ObjectId,BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));Assert.Equal((byte)UpAxis.ZPositive,bytes[8]);}
        else{Assert.Empty(order);Assert.Null(bytes);}
    }
    [Fact]
    public void CachedMemberSignalAndRetryConstructionUseFrozenIdsAndOldAttemptCount()
    {
        // reach: unit supplied raw result branch; actual configured owner/owned constructors and native signal recipient. K10/K12.
        using var f=new Fixture();var knock=Stack(f);Start(f,knock);
        void Axis(int id)=>f.Rig.Engine.AnimationMessages.Deliver(0x11,true,new CubeUpAxisEvent(1,id,UpAxis.ZPositive));
        Axis(99);Assert.Empty(knock.Tipped);Axis(8);Axis(8);Assert.Equal(new uint[]{8},knock.Tipped);
        // Supplied terminal result at the owned ActionList boundary exercises real completion dispatch/callback ordering.
        void Complete(uint result){Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction()).State=result;f.Robot.ActionList.Update();}
        Complete(0);Assert.Equal("KnockingOverStack",knock.Phase);Assert.Equal(0,knock.Attempts);
        Complete(0x04000001);Assert.Equal(BitConverter.UInt32BitsToSingle(0x3FC90FDB),knock.Interaction!.MaximumFaceTurn);Assert.Equal(1,knock.Attempts);Assert.Empty(knock.Tipped);
        Complete(0x04000001);Assert.Equal(0f,knock.Interaction!.MaximumFaceTurn);Assert.Equal(2,knock.Attempts);
        Complete(0x04000001);Assert.Equal("BlindlyFlipping",knock.Phase);Assert.Equal(3,knock.Attempts);Assert.False(Assert.IsType<NativeFlipBlockRunner>(knock.LastSequence!.Children[0]).CheckPreActionPose);
    }
    [Fact]
    public void InteractionUsesTypeFiveFullNativeDriveFaceAndFlipProxyAndDefinedWaitDiagnostic()
    {
        using var f=new Fixture();f.Add();var action=new NativeDriveAndFlipRunner(f.M,7,BitConverter.UInt32BitsToSingle(0x3FC90FDB));action.SetNameTriggers(0xFD,0xFE);
        try
        {
            Assert.Equal(0x0F,action.Type); //54F23C proxy copies Flip type; request below remains type5.
            Assert.Equal(PreActionType.Flipping,action.Drive!.Action.ActionType);Assert.True(action.Drive.Action.NativeInputs);Assert.False(action.Face!.SayName);
            Assert.Equal(new[]{unchecked((int)0xFFFFFFFE),0x29,0x2C,0x0F},action.Children.Select(c=>c.Type));
            Assert.True(action.Flip.CheckPreActionPose);Assert.Equal((byte)6,action.Flip.GetCompletionVariant());Assert.Equal(6u,action.Flip.RequiredTrackMask);Assert.Equal(30f,action.Flip.TimeoutSeconds);
            var wait=action.DriveAndWait!.Children[1];Assert.Equal(0x33,wait.Type);Assert.Equal(float.MaxValue,wait.TimeoutSeconds);Assert.Equal(0,wait.Init());
            var logs=new List<string>();f.Rig.Engine.LogEntry+=e=>logs.Add(e.Name??"");bool previous=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
            try{Assert.Equal(0u,wait.CheckIfDone());Assert.Contains("IDriveToInteractWithObject.NoDockAction",logs);Assert.True(Cozmo.Transport.EngineErrorState.ErrorFlagSet);}
            finally{Cozmo.Transport.EngineErrorState.ErrorFlagSet=previous;}
        }
        finally{action.Prep();action.WatcherEnding();}
    }
    [Theory]
    [InlineData(0u)] [InlineData(0x03000016u)]
    public void FlipTerminalChildResultUsesCanonicalUpwardUnknownAndPreservesOriginalResult(uint result)
    {
        // reach: unit explicitly supplied embedded-child terminal result; actual native Check/world recipient, not full flip execution.
        using var f=new Fixture();f.Lift=1f;f.Motors();f.Add(7,30);f.Add(8,30,66);var flip=new NativeFlipBlockRunner(f.M,7){CheckPreActionPose=false};
        try
        {
            Assert.Equal(0,flip.Init());Assert.Equal(new[]{2,8},flip.Sequence.Children.Select(c=>c.RequiredTrackMask==2?2:c.Type));Assert.True(flip.Sequence.Children[1].Tag<flip.Sequence.Children[0].Tag); //drive constructed beforelift.
            foreach(var child in flip.Sequence.Children)child.State=result;
            Assert.Equal(result,flip.CheckIfDone());Assert.Null(f.M.World.GetLocatedObjectByIdHelper(7));Assert.Null(f.M.World.GetLocatedObjectByIdHelper(8));
        }
        finally{flip.Prep();flip.WatcherEnding();}
    }
    [Fact]
    public void FlipMissingAtInitAndWhileRunningAndPendingLiftCancellationAreDistinct()
    {
        using var f=new Fixture();var missing=new NativeFlipBlockRunner(f.M,7);Assert.Equal(0x03000004,missing.Init());missing.WatcherEnding();
        f.Add(7,30);var runningMissing=new NativeFlipBlockRunner(f.M,7){CheckPreActionPose=false};
        Assert.Equal(0,runningMissing.Init());var remove=new BlockWorldFilter();remove.AllowedIds.Add(7);f.M.World.DeleteLocatedObjects(remove);
        Assert.Equal(0x03000004u,runningMissing.CheckIfDone());Assert.False(runningMissing.DestructionObserved);Assert.Equal(uint.MaxValue,runningMissing.QueuedLiftTag);
        runningMissing.Prep();runningMissing.WatcherEnding();
        var cube=f.Add(7,30);var flip=new NativeFlipBlockRunner(f.M,7){CheckPreActionPose=false};var unrelated=new NativeTimedWaitRunner(f.M,100);
        f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,unrelated,0);f.Robot.ActionList.QueueAction(QueueActionPosition.Now,flip,0);
        for(int i=0;i<15&&flip.QueuedLiftTag==uint.MaxValue;i++){f.Motors();f.Rig.Tick();}
        Assert.NotEqual(uint.MaxValue,flip.QueuedLiftTag);uint liftTag=flip.QueuedLiftTag;var queues=(System.Collections.Generic.SortedDictionary<int,ActionQueue>)typeof(ActionList).GetField("_queues",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Robot.ActionList)!;
        var carryLift=Assert.Single(queues.Values.Select(q=>q.GetCurrentAction()).OfType<ActionRunner>().Where(a=>a.Tag==liftTag));Assert.True(carryLift.SuppressTrackLocking);
        f.Robot.ActionList.Cancel(flip.Tag);f.Rig.Tick();Assert.True(flip.DestructionObserved);Assert.True(carryLift.DestructionObserved);Assert.False(unrelated.DestructionObserved);
        Assert.Equal(liftTag,flip.QueuedLiftTag);Assert.NotNull(f.M.World.GetLocatedObjectByIdHelper(7));
    }
    [Theory]
    [InlineData(false,true,false)] [InlineData(true,true,false)] [InlineData(false,false,false)] [InlineData(false,true,true)]
    public void ReactionDirtyObjectiveNeedsAndAnimationOrderUsesActualRecipients(bool hard,bool tipped,bool carried)
    {
        // reach: unit supplied terminal action result, actual configured manager/callback/world/objective/needs/animation construction. K13.
        using var f=new Fixture();var knock=Stack(f);if(hard)Spark(f,5,0);Start(f,knock);
        void Complete(uint result){Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction()).State=result;f.Robot.ActionList.Update();}
        if(!hard)Complete(0); // source reach callback constructs Knock.
        if(tipped)f.Rig.Engine.AnimationMessages.Deliver(0x11,true,new CubeUpAxisEvent(1,8,UpAxis.ZNegative));
        if(carried)f.M.Docking.Carrying.SetCarrying(7);
        var order=new List<string>();var app=new List<byte[]>();f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x8B)app.Add(body.ToArray());};
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>
        {if(p is uint value&&value==13){Assert.True(f.Robot.BlockConfigurationForceUpdate);Assert.Empty(order);Assert.Empty(app);order.Add("objective13");}});
        f.Rig.Engine.Needs.Log+=line=>{if(line.StartsWith("needs.action_completed KnockDownCubes:")){Assert.Equal(new[]{"objective13"},order);order.Add("needs27");}};
        Complete(0);
        Assert.True(f.Robot.BlockConfigurationForceUpdate);Assert.Equal("PlayingReaction",knock.Phase);
        Assert.Equal(tipped?new[]{"objective13","needs27"}:Array.Empty<string>(),order);
        if(hard)Assert.False(knock.HasCurrentAction);
        else Assert.Equal(tipped?AnimationTrigger.KnockOverSuccess:AnimationTrigger.KnockOverFailure,Assert.IsAssignableFrom<TriggerAnimationAction>(f.Robot.ActionList.GetCurrentAction()).Request.Trigger);
        f.Rig.Engine.DrainAppDelivery();if(tipped)Assert.Equal(13u,BinaryPrimitives.ReadUInt32LittleEndian(Assert.Single(app)));else Assert.Empty(app);
        Assert.Equal(carried?(uint?)7:null,f.Robot.CarryingOwner!.CarriedObjectId);Assert.DoesNotContain(f.Rig.SentSince(0),m=>m is DockWithObject);
    }
    [Theory]
    [InlineData(5,0,true)] [InlineData(5,1,true)] [InlineData(6,0,false)] [InlineData(85,0,false)]
    public void CommonSparkLockUsesCanonicalConfiguredUnlockAndReleasesAndResumes(int spark,byte soft,bool matching)
    {
        // reach: configured manager Init/Stop plus explicit scoped Resume unit; checked common K1/K5 lifecycle.
        using var f=new Fixture();var knock=Stack(f);var manager=f.Robot.BehaviorManagerOwner!;Spark(f,spark,soft);Start(f,knock);
        const string key="SparkBehaviorDisables_behaviorLock";
        Assert.Equal(matching,manager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated,key));
        manager.Stop(BehaviorStopReason.Interrupted,0);Assert.False(manager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated,key));
        // Resume alone cannot refresh the cleared weak. An actual runnable query is required before reacquisition.
        Assert.True(knock.IsRunnable(f.Runtime.Context));using var scope=new BehaviorScope(motion:f.Rig.Robot.Motion,manager:manager);knock.AttachRun(f.Runtime.Context,scope);
        Assert.False(knock.Resume(null,f.Rig.Engine.Timer.SecondsF));Assert.Equal("KnockingOverStack",knock.Phase);
        Assert.Equal(matching,manager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated,key));
        knock.Stop(BehaviorStopReason.Interrupted);scope.Dispose();Assert.False(manager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated,key));
    }
    [Fact]
    public void FlipPoseSelectionUsesUnnamedObservedCacheAndRequiresItsActualCurrentOrigin()
    {
        // reach: unit supplied native face observations, actual cache + native type5 pose recipient. K17 literalY selection rules.
        using var f=new Fixture();var cube=f.Add(7,300);var interaction=new NativeDriveAndFlipRunner(f.M,7,0);
        try
        {
            bool inPosition=false;Assert.Equal(ActionResult.Success,interaction.GetPossiblePoses(cube,out var fallback,ref inPosition));Assert.True(Assert.Single(fallback).WorldPose.Translation.Y>0);Assert.False(inPosition);
            FaceEntry Face(int id,uint time,Pose3d parent,bool named)
            {
                var tracked=new TrackedFace(new DetectedFace(id,new FaceRect(0,0,10,10),Name:named?"Ada":null),time){HeadPose=new Pose3d(Rotation3d.Identity,new(100,500,60),parent:parent)};
                return f.M.Vision.Faces.AddOrUpdateFace(tracked,f.Robot.GetCheckedMainPose(),false)!.Face;
            }
            FaceEntry entry=null!;for(uint time=1;time<=4;time++)entry=Face(11,time,f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId),false);
            Assert.False(entry.HasName);Assert.Equal(4,entry.TimesObserved);
            Assert.Equal(ActionResult.Success,interaction.GetPossiblePoses(cube,out var withFace,ref inPosition));Assert.True(Assert.Single(withFace).WorldPose.Translation.Y<0);Assert.False(inPosition);
            using var foreign=new Pose3d("explicit-foreign-face-root");Face(12,5,foreign,true);
            Assert.Equal(ActionResult.Success,interaction.GetPossiblePoses(cube,out var ignored,ref inPosition));Assert.True(Assert.Single(ignored).WorldPose.Translation.Y>0);Assert.False(inPosition);
        }
        finally{interaction.Prep();interaction.WatcherEnding();}
    }
    [Fact]
    public void NoEyeFaceUpdatePreservesIncomingOriginAndRotationWithPriorTranslation()
    {
        // reach: unit explicit tracked face poses, actual FaceWorld consumer/cache; K27 full copy then translation-only overwrite.
        using var f=new Fixture();using var firstParent=new Pose3d("previous-origin");using var nextParent=new Pose3d("incoming-history-origin");
        var first=new TrackedFace(new DetectedFace(20,new FaceRect(0,0,10,10),Name:"Ada"),1){HeadPose=new Pose3d(Rotation3d.Identity,new(10,20,60),parent:firstParent)};
        f.M.Vision.Faces.AddOrUpdateFace(first,f.Robot.GetCheckedMainPose(),false);
        var rotation=Rotation3d.FromAngleAxis(.5f,new(0,0,1));
        var next=new TrackedFace(new DetectedFace(20,new FaceRect(0,0,10,10),Name:"Ada"),2){HeadPose=new Pose3d(rotation,new(90,80,70),parent:nextParent)};
        var entry=f.M.Vision.Faces.AddOrUpdateFace(next,f.Robot.GetCheckedMainPose(),false)!.Face;
        Assert.Equal(new Vec3(10,20,60),entry.HeadPose.Translation);Assert.Same(nextParent.Node,entry.HeadPose.Node!.Parent);Assert.Equal(rotation,entry.HeadPose.Quaternion);
        Assert.NotSame(next.HeadPose.Node,entry.HeadPose.Node);Assert.Equal(new Vec3(90,80,70),next.HeadPose.Translation);
        using var copied=new Pose3d("");Assert.False(f.M.Vision.Faces.CopyNativeLastFace(f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId),true,copied));Assert.True(f.M.Vision.Faces.CopyNativeLastFace(nextParent,true,copied));
    }
    private sealed class Replacement(ManipulationSystem m):SteppedBehavior("knock-replacement-input","Dev")
    {
        public override bool IsRunnable(BehaviorContext context)=>true;
        protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
        protected override void OnStart()=>StartActing(new NativeTimedWaitRunner(m,100),null);
    }
    [Fact]
    public void ManagerReplacementRetiresRunningBlindFlipAndItsLiftWithoutCancellingAnotherQueue()
    {
        // reach: unit supplied retry results and replacement behavior; actual configured manager/ActionList/Flip/motor/cancellation recipients.
        using var f=new Fixture();var knock=Stack(f,30);Start(f,knock);
        void Complete(uint result){Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction()).State=result;f.Robot.ActionList.Update();}
        Complete(0);Complete(0x04000001);Complete(0x04000001);Complete(0x04000001);Assert.Equal("BlindlyFlipping",knock.Phase);
        var flip=Assert.IsType<NativeFlipBlockRunner>(knock.LastSequence!.Children[0]);
        for(int i=0;i<15&&flip.QueuedLiftTag==uint.MaxValue;i++)f.Motors();Assert.NotEqual(uint.MaxValue,flip.QueuedLiftTag);
        var queues=(SortedDictionary<int,ActionQueue>)typeof(ActionList).GetField("_queues",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Robot.ActionList)!;
        var carry=Assert.Single(queues.Values.Select(q=>q.GetCurrentAction()).OfType<ActionRunner>().Where(a=>a.Tag==flip.QueuedLiftTag));
        var other=new NativeTimedWaitRunner(f.M,100);f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,other,0);f.Rig.Tick();
        var manager=f.Robot.BehaviorManagerOwner!;var replacement=new Replacement(f.M);replacement.BindActionCompletionOwner(f.Rig.Robot,f.Robot);manager.Add(replacement);
        int subscriptions=f.Rig.Engine.AnimationMessages.Count(0x11,true);
        try
        {
            Assert.True(manager.StartAsync(replacement.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Assert.Same(replacement,manager.Current);Assert.True(flip.DestructionObserved);Assert.True(carry.DestructionObserved);Assert.False(other.DestructionObserved);Assert.Equal(EngineActionResult.Running,other.State);
            Assert.Equal(uint.MaxValue,knock.Bottom);Assert.Equal(uint.MaxValue,knock.Middle);Assert.Equal(uint.MaxValue,knock.Top);Assert.False(knock.EngineRunning);
            Assert.Equal(subscriptions,f.Rig.Engine.AnimationMessages.Count(0x11,true));f.Rig.Engine.AnimationMessages.Deliver(0x11,true,new CubeUpAxisEvent(1,7,UpAxis.XPositive));Assert.Empty(knock.Tipped);
            Assert.False(manager.HasDisableLock(ReactionTrigger.UnexpectedMovement,"FlipBlock"));Assert.False(manager.HasDisableLock(ReactionTrigger.ObjectPositionUpdated,"KnockOverCubes_behaviorLock"));
        }
        finally{manager.Stop(BehaviorStopReason.Interrupted,0);replacement.RetireActionCompletionOwner();f.Robot.ActionList.Clear();}
    }
    [Theory]
    [InlineData(0x03000010u,true)] [InlineData(0x03000016u,false)]
    public void NoPreactionOrOtherNonRetryResultDoesNotConstructAnotherAttempt(uint result,bool whiteboard)
    {
        // reach: unit supplied terminal result at actual queued root; K10 literal branch effects.
        using var f=new Fixture();var knock=Stack(f);Start(f,knock);
        void Complete(uint state){Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction()).State=state;f.Robot.ActionList.Update();}
        Complete(0);var action=knock.Interaction;Complete(result);
        Assert.Same(action,knock.Interaction);Assert.Equal(0,knock.Attempts);Assert.False(knock.HasCurrentAction);Assert.Equal("KnockingOverStack",knock.Phase);
        Assert.Equal(whiteboard?(uint?)7:null,f.M.Whiteboard.NoPreDockPosesObjectId);Assert.DoesNotContain(f.Rig.SentSince(0),m=>m is DockWithObject);
    }
    [Fact]
    public void ConfiguredNativeRunExecutesReachApproachFlipWithInternalProxyAndRootCompletion()
    {
        // reach: configured factory/manager/ActionList/owned children. Supplied stack, unlock and firmware path/motor/animation/axis inputs.
        // Actual worker images supply the selected object's verification. Inherited lattice/PnP remain labelled frontiers, no activity is invented.
        using var f=new Fixture();M4ControlTests.ConnectCube(f.Rig);var connected=Assert.Single(f.M.World.ConnectedObjects);uint bottom=connected.ObjectId;
        f.Add(bottom,300,22);f.Add(100,300,66);f.Add(101,300,110);f.Robot.Progression!.Unlocked.Add(5);
        f.Runtime.Configurations.ForceUpdate();f.Runtime.Configurations.Update();var knock=Knock(f);Assert.True(knock.IsRunnable(f.Runtime.Context));
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        CompoundActionSequential? knockRoot=null;var delivery=new List<string>();var rawReports=new List<byte[]>();
        var reports=new List<RobotCompletedAction>();var phaseTrace=new List<string>();var internalRecords=new List<RobotCompletedAction>();
        int watcherHandle=f.Robot.ActionList.Watcher.RegisterCallback(internalRecords.Add);
        using var local=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction report){reports.Add(report);if(report.Tag==knockRoot?.Tag)delivery.Add("local");}});
        var app=new List<ActionCompletionReport>();f.Rig.Engine.ActionCompleted+=report=>{app.Add(report);if(report.Tag==knockRoot?.Tag)delivery.Add("typed");};
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x5A&&BinaryPrimitives.ReadUInt32LittleEndian(body.Span)==knockRoot?.Tag){rawReports.Add(body.ToArray());delivery.Add("raw");}};
        var logs=new List<string>();f.Rig.Engine.LogEntry+=e=>logs.Add(e.Name+":"+e.Text);
        var manager=f.Robot.BehaviorManagerOwner!;manager.DisableReactionsWithLock("knock-offline-inputs",Enumerable.Range(0,21).Select(i=>i==(int)ReactionTrigger.ObjectPositionUpdated).ToArray(),false);
        int mark=0,motorMark=0,nextSegment=0;sbyte currentSegment=-1;ExecutePath? pending=null;
        var wire=new List<RobotMessage>();var paths=new HashSet<ushort>();string finalKind="";int completedPaths=0;
        void Motors()
        {
            foreach(var message in f.Rig.SentSince(motorMark))switch(message)
            {
                case SetHeadAngle head:f.Head=head.AngleRad;f.Rig.Data(new MotorActionAck{ActionId=head.ActionId});break;
                case SetLiftHeight lift:f.Lift=(float)Math.Asin((lift.HeightMm-45f)/66f);f.Rig.Data(new MotorActionAck{ActionId=lift.ActionId});break;
                case SetBodyAngle body:f.Yaw=body.AngleRad;f.Rig.Data(new MotorActionAck{ActionId=body.ActionId});break;
            }
            motorMark=f.Rig.Mark();
            var state=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,frame:f.Robot.PoseFrameId,x:f.X,y:f.Y,head:f.Head,liftAngle:f.Lift);
            state.Pose.Angle=f.Yaw;state.CurrPathSegment=currentSegment;f.Rig.Data(state);f.Rig.Tick();
        }
        void Receive()
        {
            var sent=f.Rig.SentSince(mark);mark=f.Rig.Mark();
            foreach(var message in sent)
            {
                if(message is ClearPath){wire.Clear();pending=null;nextSegment=0;currentSegment=-1;}
                else if(message is AppendPathSegmentLine or AppendPathSegmentArc or AppendPathSegmentPointTurn)wire.Add(message);
                else if(message is ExecutePath execute&&paths.Add(execute.EventId))
                {pending=execute;nextSegment=0;f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=(byte)PathEventType.Started});phaseTrace.Add($"START {execute.EventId} buffered={wire.Count} total={f.Rig.Robot.Motion.Path.DoleSegmentCount}");}
            }
        }
        var animations=new HashSet<byte>();uint image=0;bool tipped=false,approached=false;NativeDriveAndFlipRunner? interaction=null;
        try
        {
            Assert.True(manager.StartAsync(knock.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            for(int tick=0;tick<260&&ReferenceEquals(manager.Current,knock);tick++)
            {
                if(f.M.GetNativePlanners().Lattice is {} runtime&&runtime.CheckPlanningStatus==1)
                    Assert.True(SpinWait.SpinUntil(()=>runtime.CheckPlanningStatus!=1,TimeSpan.FromSeconds(10)),"offline planner watchdog");
                Motors();Receive();
                if(pending is {} active)
                {
                    if(nextSegment<wire.Count)
                    {
                        var segment=wire[nextSegment];float centerX=f.X,centerY=f.Y;
                        switch(segment)
                        {
                            case AppendPathSegmentLine line:centerX=line.XEndMm;centerY=line.YEndMm;break; //no invented line heading.
                            case AppendPathSegmentArc arc:
                                float angle=arc.StartRad+arc.SweepRad;centerX=arc.XCenterMm+arc.RadiusMm*MathF.Cos(angle);centerY=arc.YCenterMm+arc.RadiusMm*MathF.Sin(angle);f.Yaw=angle+MathF.Sign(arc.SweepRad)*Radians.Pi*.5f;break;
                            case AppendPathSegmentPointTurn turn:centerX=turn.XMm;centerY=turn.YMm;f.Yaw=turn.TargetAngleRad;break;
                        }
                        float offset=f.Robot.CarryingOwner!.CarriedObjectId is null?20f:0f;
                        f.X=centerX+offset*MathF.Cos(f.Yaw);f.Y=centerY+offset*MathF.Sin(f.Yaw);
                        currentSegment=checked((sbyte)nextSegment);nextSegment++;finalKind=segment.GetType().Name;
                        phaseTrace.Add($"SEGMENT {active.EventId}/{currentSegment} {finalKind} {Convert.ToHexString(segment.ToBytes())}");Motors();Receive();
                    }
                    else if(nextSegment==f.Rig.Robot.Motion.Path.DoleSegmentCount)
                    {
                        phaseTrace.Add($"COMPLETE {active.EventId} consumed={nextSegment} final={finalKind}");completedPaths++;
                        f.Rig.Data(new PathFollowingEvent{EventId=active.EventId,EventType=(byte)PathEventType.Completed});pending=null;currentSegment=-1;Motors();Receive();
                    }
                }
                byte animation=f.Rig.Robot.Animations.CurrentTag;
                if(animation!=0&&animations.Add(animation)){f.Rig.Data(new AnimationStarted{AnimId=animation});f.Rig.Data(new AnimationEnded{AnimId=animation});f.Rig.Tick();}
                interaction??=knock.Interaction;if(interaction is not null)knockRoot??=knock.LastSequence;
                if(knock.Interaction is {} observed)
                {
                    var goal=observed.Drive?.Drive;
                    phaseTrace.Add($"{tick} phase={knock.Phase}/{knock.Attempts} MAIN={f.Robot.GetCheckedMainPose().Translation} yaw={f.Robot.GetCheckedMainPose().AngleAroundZ} drive={observed.Drive?.State:X8} flip={observed.Flip.State:X8} flag={observed.Flip.CheckPreActionPose} goal="+(goal?.Goals is {Count:>0} gs?string.Join(";",gs.Select(g=>g.Translation+"/"+g.AngleAroundZ)):"none"));
                }
                if(knock.Interaction?.Drive?.Drive is {} drive&&drive.Initialized)approached=true;
                if(knock.Interaction?.Drive?.Sequence.Children.OfType<NativeTurnTowardsObjectRunner>().Any(turn=>turn.Initialized)==true)f.Image(++image,true,300);
                var activeFlip=knock.Phase=="BlindlyFlipping"?knock.LastSequence?.Children.OfType<NativeFlipBlockRunner>().FirstOrDefault():knock.Interaction?.Flip;
                if(!tipped&&activeFlip is {Initialized:true,State:EngineActionResult.Running})
                {f.Rig.Data(new ObjectUpAxisChanged{Timestamp=f.Robot.StoredState!.Timestamp,ObjectID=unchecked((uint)connected.ActiveId),UpAxis=UpAxis.XPositive});f.Rig.Tick();tipped=true;}
            }
            f.Rig.Engine.DrainAppDelivery();string diagnostic=knock.Phase+" attempts="+knock.Attempts+" last="+knock.Interaction?.State.ToString("X8")+" "+string.Join(" | ",logs.TakeLast(20));
            var traceDirectory=Path.Combine(new DirectoryInfo(f.Rig.Engine.Options.ResourcesPath!).Parent!.Parent!.Parent!.Parent!.FullName,".scratch","knockover-cubes-1");Directory.CreateDirectory(traceDirectory);
            File.WriteAllLines(Path.Combine(traceDirectory,"configured-input-trace.txt"),phaseTrace.Concat(reports.Select(r=>$"REPORT {r.Tag}/{r.ActionType:X}/{r.Result:X8}")).Concat(logs));
            Assert.True(approached,diagnostic);Assert.True(tipped,diagnostic);Assert.NotNull(interaction);Assert.True(completedPaths>0);
            Assert.Equal("PlayingReaction",knock.Phase);Assert.False(ReferenceEquals(manager.Current,knock));
            //53FA5C/78/AA: queued outer root publishes5A;54F050 nested proxy stays in watcher/cache.
            var nested=Assert.Single(internalRecords.Where(r=>r.Tag==interaction!.Tag));Assert.Equal(0x0F,nested.ActionType);Assert.Equal(0u,nested.Result);Assert.Equal((byte)6,nested.CompletionVariant);Assert.Null(nested.ObjectInteraction);
            Assert.True(interaction!.DestructionObserved);Assert.Equal(0,knock.Attempts);
            var root=Assert.Single(reports.Where(r=>r.Tag==knockRoot!.Tag));Assert.Equal(-2,root.ActionType);Assert.Equal(0u,root.Result);Assert.Equal((byte)6,root.CompletionVariant);Assert.Null(root.ObjectInteraction);
            Assert.Contains(0x04000001u,root.SubActionResults);Assert.Contains(0u,root.SubActionResults);Assert.Equal(root.SubActionResults.Order().Distinct(),root.SubActionResults);
            var typed=Assert.Single(app.Where(r=>r.Tag==knockRoot!.Tag));Assert.Equal(root.SubActionResults,typed.SubActionResults);Assert.Equal((byte)6,typed.CompletionVariant);
            var raw=Assert.Single(rawReports);Assert.Equal(14+4*root.SubActionResults.Count,raw.Length);Assert.Equal(knockRoot!.Tag,BinaryPrimitives.ReadUInt32LittleEndian(raw));Assert.Equal(-2,BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(4)));Assert.Equal(0u,BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(8)));Assert.Equal((byte)root.SubActionResults.Count,raw[12]);Assert.Equal((byte)6,raw[^1]);
            for(int i=0;i<root.SubActionResults.Count;i++)Assert.Equal(root.SubActionResults[i],BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(13+4*i)));
            Assert.Equal(new[]{"local","raw","typed"},delivery);Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);
            Assert.DoesNotContain(f.Rig.SentSince(0),m=>m is DockWithObject);Assert.NotEmpty(f.Rig.SentSince(0).OfType<SetLiftHeight>());
        }
        finally{f.Robot.ActionList.Watcher.UnregisterCallback(watcherHandle);manager.RemoveDisableReactionsLock("knock-offline-inputs");manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Robot.ActionList.Clear();}
    }
}
