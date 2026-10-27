using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Transport;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M12CheckedRowsBuilderTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new();
        internal readonly VisionSystem Vision;
        internal readonly ManipulationSystem M;
        internal EngineRobot Native=>Rig.Engine.Robot!;
        internal Fixture()
        {
            Rig.ToSynced();
            Vision=Rig.Robot.Vision; Vision.Calibration=CameraCalibration.Nominal(); Vision.Enabled=false;
            M=new ManipulationSystem(Rig.Robot,Vision);
            Vision.World.CurrentOriginId=Native.CurrentOriginId;
        }
        internal ObservableObject Add(uint id,Vec3 position,ObjectType type=ObjectType.Block_LIGHTCUBE1,Pose3d? parent=null)
        {
            var obj=new ObservableObject(id,type,CubeGeometry.MarkersFor(type))
            {Pose=new Pose3d(Rotation3d.Identity,position,parent:parent??Native.GetNativeOrigin(Native.CurrentOriginId)),
             PoseState=PoseState.Known,OriginId=Vision.World.CurrentOriginId};
            Vision.World.AddLocatedObject(obj);return obj;
        }
        public void Dispose(){M.Dispose();Vision.Dispose();Rig.Dispose();}
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public async Task NativeDockCallbacksAndRetirementShareTheActionGate()
    {
        using var f = new Fixture();
        var action = new PickupObjectAction(f.M, 7);
        using var registration = f.M.Docking.RegisterActionHandlers(action, DockAction.PickupLow);
        using var attempted = new ManualResetEventSlim();
        f.Rig.Engine.MessageDispatchObservedForTest = (tag, name) =>
        { if (tag == RobotMessageId.LiftLoad && name == "IDockAction") attempted.Set(); };
        Task delivery;
        lock (action.NativeDockGate)
        {
            delivery = Task.Run(() => f.Rig.Engine.Broadcast(new LiftLoad { Field0 = true }));
            Assert.True(attempted.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(delivery.Wait(TimeSpan.FromMilliseconds(50)));
            Assert.Equal(0, action.LiftLoadState);
        }
        await delivery.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, action.LiftLoadState);
        using var retiring = new ManualResetEventSlim();
        Task retirement;
        lock (action.NativeDockGate)
        {
            retirement = Task.Run(() => { retiring.Set(); registration.Dispose(); });
            Assert.True(retiring.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(retirement.Wait(TimeSpan.FromMilliseconds(50)));
            Assert.Equal(1, f.Rig.Engine.AnimationMessages.Count(0x4C, true));
        }
        await retirement.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, f.Rig.Engine.AnimationMessages.Count(0x4C, true));
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public async Task UiDeliveryReleasesItsTopologyGateBeforeCallingAnAction()
    {
        using var hub = new UiSubscriptionHub();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var handle = hub.Subscribe(0x4C, true, () =>
        { entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(10))); });
        var delivery = Task.Run(() => hub.Deliver(0x4C, true));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            await Task.Run(() =>
            {
                using var another = hub.Subscribe(0x4C, true);
                Assert.Equal(2, hub.Count(0x4C, true));
            }).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, hub.Count(0x4C, true));
        }
        finally { release.Set(); }
        await delivery.WaitAsync(TimeSpan.FromSeconds(5));
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void DeletedTargetLogCallbacksDrainAfterReleasingTheActionGate()
    {
        using var f = new Fixture();
        var action = new PickupObjectAction(f.M, 7);
        using var registration = f.M.Docking.RegisterActionHandlers(action, DockAction.PickupLow);
        bool completed = false;
        uint observed = 0;
        Task? reader = null;
        f.Rig.Engine.LogEntry += entry =>
        {
            if (entry.Name != "IDockAction.RobotDeletedLocatedObject") return;
            reader = Task.Run(() => observed = action.ObjectId);
            completed = reader.Wait(TimeSpan.FromSeconds(5));
        };
        f.Rig.Engine.AnimationMessages.Deliver(0x4C, true, 7u);
        Assert.True(completed);
        reader!.GetAwaiter().GetResult();
        Assert.Equal(uint.MaxValue, observed);
    }

    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void AcceptedStateMutatesTheSameLiftNodeAndItsCarriedChildWithoutAPoseBroadcast()
    {
        using var f=new Fixture();var node=f.Native.LiftPose.Node;
        var obj=f.Add(7,Vec3.Zero);obj.Pose=new Pose3d(Rotation3d.Identity,Vec3.Zero,parent:f.Native.LiftPose);obj.NativeCarryingTree=true;
        int broadcasts=0;f.Vision.World.PoseStateChanged+=(_,_,_)=>broadcasts++;
        var original=f.Native.LiftPose.Transform;
        f.Native.SyncTime();f.Rig.State(liftAngle:BitConverter.Int32BitsToSingle(0x3FC90FDB));
        Assert.Equal(original,f.Native.LiftPose.Transform);
        f.Rig.Data(new SyncTimeAck());f.Rig.Tick();
        f.Rig.State(liftAngle:BitConverter.Int32BitsToSingle(0x3FC90FDB));
        Assert.Same(node,f.Native.LiftPose.Node);Assert.NotEqual(original,f.Native.LiftPose.Transform);
        Assert.InRange(obj.Pose.Translation.Z,110.999,111.001);Assert.Equal(0,broadcasts);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void LiftObservationBroadcastPrecedesConfirmerAndKeepsTheArgumentPose()
    {
        using var f=new Fixture();var obj=f.Add(7,new Vec3(10,0,0));
        f.Vision.World.ApplyRelativeObservation(obj,obj.NativePose,PoseState.Known,4);
        var confirmerNode=obj.Confirmation!.Pose.Node;
        obj.PoseState=PoseState.Dirty;obj.LastObservedTimestamp=123;
        using var argument=new Pose3d(Rotation3d.Identity,new Vec3(2,3,4),parent:f.Native.LiftPose);
        bool called=false;
        typeof(BlockWorld).GetField("_appendPoseChanges",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(f.Vision.World,true);
        f.Vision.World.SetObjectPoseChanged=(id,state)=>
        {
            // B: old wrapper alias was Same; ObservableObject::SetPose copies into its embedded pose
            // (0x004EF598..0x004EF5A2). The argument must remain independent when the callback writes again.
            called=true;Assert.Equal(4,obj.Confirmation!.Count);Assert.NotSame(argument,obj.NativePose);
            Assert.Equal(new Vec3(2,3,4),obj.NativePose.Translation);Assert.Same(f.Native.LiftPose.Node,obj.NativePose.Node!.Parent);
            using var changed=new Pose3d(Rotation3d.Identity,new Vec3(99,0,0));obj.SetPose(changed,0,PoseState.Known);
        };
        Assert.Equal(0u,ObjectPoseConfirmerRelative.AddLiftRelativeObservation(f.Vision.World,obj,argument));
        var queued = (System.Collections.IList)typeof(BlockWorld).GetField("_poseChanges",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Vision.World)!;
        var saved = (PoseChange)queued[0]!;
        Assert.NotNull(saved.Pose.Node);Assert.Equal(new Vec3(10,0,0),saved.Pose.Translation);
        Assert.True(called);Assert.Equal(1,obj.Confirmation!.Count);Assert.Equal(123u,obj.Confirmation.LastPoseUpdatedTime);
        Assert.Same(confirmerNode,obj.Confirmation.Pose.Node);
        Assert.Equal(new Vec3(2,3,4),obj.Confirmation.Pose.Translation);Assert.Same(f.Native.LiftPose.Node,obj.Confirmation.Pose.Node!.Parent);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void LiveAttachmentAlreadyHasItsLiftParentAtTheBroadcast()
    {
        using var signals=SignalTestContext.Install();
        using var rig=new Rig();if(rig.NoLibrary)return;
        rig.Cube=ManipulationTests.CubeAt(150,0);
        var obj=Assert.Single(rig.Frame().Objects).Object;
        bool seen=false;var previous=rig.Vision.World.SetObjectPoseChanged;
        rig.Vision.World.SetObjectPoseChanged=(id,state)=>
        {
            if(id==obj.ObjectId && state==PoseState.Known)
            {seen=true;Assert.True(obj.NativeCarryingTree);Assert.Same(rig.Robot.Engine.Robot!.LiftPose.Node,obj.NativePose.Node!.Parent);}
            previous?.Invoke(id,state);
        };
        Assert.Equal(0u,rig.M.Docking.SetObjectAsAttachedToLift(obj.ObjectId,obj.Markers[0].Code));
        Assert.True(seen);Assert.Same(rig.Robot.Engine.Robot!.LiftPose.Node,obj.Confirmation!.Pose.Node!.Parent);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void InitRechecksBeforeStartingAndAnUnreadDriveDoesNotCreateAStandInAction()
    {
        using var f=new Fixture();var helper=Helper(f);int checks=0,actions=0;
        helper.PrecheckForTest=()=>{++checks;return ActionResult.Success;};helper.StartActionForTest=_=>actions++;
        Assert.Equal(BehaviorStatus.Running,helper.UpdateWhileActive(f.Rig.Robot,out var sub));
        Assert.Null(sub);Assert.Equal(2,checks);Assert.Equal(1u,helper.Attempts);Assert.Equal(1,actions);
        var needsDrive=Helper(f);needsDrive.PrecheckForTest=()=>ActionResult.DidNotReachPreActionPose;needsDrive.StartActionForTest=_=>actions++;
        needsDrive.UpdateWhileActive(f.Rig.Robot,out sub);
        Assert.Null(sub);Assert.Equal(0u,needsDrive.Attempts);Assert.Equal(BehaviorStatus.Running,needsDrive.Status);Assert.Equal(1,actions);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ReinitReplacesRobotHandlesAppendsExternalHandlesAndRetiresInSourceOrder()
    {
        using var f=new Fixture();var action=new PickupObjectAction(f.M,7);
        using var subscriptions=f.M.Docking.RegisterActionHandlers(action,DockAction.PickupLow);
        f.Rig.Engine.Broadcast(new LiftLoad{Field0=true});Assert.Equal(1,action.LiftLoadState);
        f.M.Docking.RegisterActionHandlers(action,DockAction.PickupLow);Assert.Equal(0,action.LiftLoadState);
        Assert.Equal(2,f.Rig.Engine.AnimationMessages.Count(0x4C,true));
        int delivered=0;f.Rig.Engine.MessageDispatchObservedForTest=(tag,name)=>{if(tag==RobotMessageId.LiftLoad&&name=="IDockAction")delivered++;};
        f.Rig.Engine.Broadcast(new LiftLoad());Assert.Equal(1,delivered);Assert.Equal(2,action.LiftLoadState);
        f.Rig.Engine.AnimationMessages.Deliver(0x4C,true,8u);Assert.Equal(7u,action.ObjectId);
        f.Rig.Engine.AnimationMessages.Deliver(0x4C,true,7u);Assert.Equal(uint.MaxValue,action.ObjectId);
        Assert.Equal((ActionResult)0x03000004,action.CheckDeletedTarget());Assert.DoesNotContain(f.M.Docking.Sent,m=>m is AbortDocking);
        var order=new List<string>();f.Rig.Engine.AnimationMessages.ReleaseObserved=s=>{if(s.StartsWith("unlink"))order.Add(s);};
        f.Rig.Engine.MessageSubscriptionReleasedForTest=(tag,name)=>{if(name=="IDockAction")order.Add(tag.ToString());};
        subscriptions.Dispose();Assert.Equal(new[]{"unlink E2G 4C","unlink E2G 4C","LiftLoad","MovingLiftPostDock"},order);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void AlignC5SentinelReturnsBeforePayloadAndWithoutAnAnimationLookup()
    {
        using var f=new Fixture();var logs=new List<EngineLogEntry>();f.Rig.Engine.LogEntry+=logs.Add;
        var action=new AlignWithObjectAction(f.M,7,0,AlignmentType.Custom);
        using var registration=f.M.Docking.RegisterActionHandlers(action,DockAction.Align);
        f.Rig.Engine.Broadcast(new MovingLiftPostDock{Field0=255});
        Assert.DoesNotContain(logs,l=>l.Name.StartsWith("IDockAction.MovingLiftPostDockHandler"));
        Assert.Equal(0x23F,action.LiftAnimationTrigger);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void ApproachCalculationPreservesItsOutputAtEachFailureAndComposesTargetOnTheLeft()
    {
        using var f=new Fixture();using var target=new Pose3d("");float output=123;
        Assert.Equal(0x03000011u,(uint)PlacementApproachAngle.Compute(f.M,target,ref output));Assert.Equal(123,output);
        f.M.Docking.Carrying.SetCarrying(7);
        Assert.Equal(0x03000004u,(uint)PlacementApproachAngle.Compute(f.M,target,ref output));Assert.Equal(123,output);
        var obj=f.Add(7,Vec3.Zero);
        using var mismatched=new Pose3d(Rotation3d.FromAngleAxis(BitConverter.Int32BitsToSingle(0x3FC90FDB),new Vec3(1,0,0)),Vec3.Zero);
        Assert.Equal(0x0300000Bu,(uint)PlacementApproachAngle.Compute(f.M,mismatched,ref output));Assert.Equal(123,output);
        using var foreign=new Pose3d("foreign");obj.NativePose.SetParent(foreign);
        Assert.Equal(0x03000005u,(uint)PlacementApproachAngle.Compute(f.M,target,ref output));Assert.Equal(123,output);
        obj.Pose=new Pose3d(Rotation3d.FromAngleAxis(0.2f,new Vec3(1,0,0)),Vec3.Zero,parent:f.Native.MainPose);
        using var rotatedTarget=new Pose3d(Rotation3d.FromAngleAxis(0.4f,new Vec3(0,1,0)),Vec3.Zero);
        Assert.Equal(ActionResult.Success,PlacementApproachAngle.Compute(f.M,rotatedTarget,ref output));
        // OV05 target * inverse(carried): +atan2(sin(.4)*sin(.2), cos(.2)); reversed multiplication yields zero.
        Assert.InRange(output,(float)Math.Atan2(Math.Sin(0.4)*Math.Sin(0.2),Math.Cos(0.2))-0.000001f,
                                    (float)Math.Atan2(Math.Sin(0.4)*Math.Sin(0.2),Math.Cos(0.2))+0.000001f);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public async Task PlacementInitPortsBothPreconditionDiagnosticsAndTheErrorFlag()
    {
        bool prior=EngineErrorState.ErrorFlagSet;
        try
        {
            using var f=new Fixture();var logs=new List<EngineLogEntry>();f.Rig.Engine.LogEntry+=logs.Add;
            using var pose=new Pose3d("");
            var action=new DriveToPlaceCarriedObjectAction(f.M,pose,true,false,false,false,0);
            Assert.Equal((ActionResult)0x03000011,await action.RunAsync(default));
            Assert.Contains(logs,e=>e.Level==EngineLogLevel.Warning&&e.Name=="DriveToPlaceCarriedObjectAction.CheckPreconditions.NotCarryingObject"&&e.Text.EndsWith("Robot 1 cannot place an object because it is not carrying anything.",StringComparison.Ordinal));
            f.M.Docking.Carrying.SetCarrying(7);EngineErrorState.ErrorFlagSet=false;
            Assert.Equal((ActionResult)0x03000004,await action.RunAsync(default)); // lookup still uses ctor's +78=-1; diagnostic reads current +7C=7.
            Assert.Contains(logs,e=>e.Level==EngineLogLevel.Error&&e.Name=="DriveToPlaceCarriedObjectAction.CheckPreconditions.NoObjectWithID"&&e.Text.EndsWith("Robot 1's block world does not have an ActionableObject with ID=7.",StringComparison.Ordinal));
            Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally{EngineErrorState.ErrorFlagSet=prior;}
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void UpAxisTiesUnorderedAndNegativePiUseTheCheckedBranches()
    {
        Assert.Equal(1,RotatedParentAxis.Get(new Mat3(1,0,0,0,1,0,1,-1,1),2));
        Assert.Equal(-1,RotatedParentAxis.Get(new Mat3(1,0,0,0,1,0,double.NaN,10,20),2));
        Assert.Equal(-1,RotatedParentAxis.Get(new Mat3(1,0,0,0,1,0,0,0,0),2));
        var down=new Mat3(1,0,0,0,-1,0,0,0,-1);
        Assert.Equal(0x40490FDBu,BitConverter.SingleToUInt32Bits((float)PlacementApproachAngle.Helper(down)));
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void HeightToleranceUsesMaxTimesTheNativeWordAndCopyKeepsIt()
    {
        Assert.Equal(0x40DDB3D7u,BitConverter.SingleToUInt32Bits(PreActionPose.ComputeHeightTolerance(new Vec3(3,4,12))));
        Assert.Equal(0x3F13CD3Au,BitConverter.SingleToUInt32Bits(PreActionPose.ComputeHeightTolerance(new Vec3(1,double.NaN,12))));
        using var pose=new Pose3d(Rotation3d.Identity,new Vec3(3,4,12));
        var marker=CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1)[0];
        var pre=new PreActionPose(PreActionType.Docking,marker,pose,75);
        var copy=pre with{LocalAngleRad=1};Assert.Equal(0x40DDB3D7u,BitConverter.SingleToUInt32Bits(copy.HeightToleranceMm));
        var fractional=new PreActionPose(PreActionType.Docking,marker,pose,1.00000009);
        Assert.Equal(0x3F800001u,BitConverter.SingleToUInt32Bits((float)(fractional with{RotationIndex=2}).DistanceMm));
        var zero=Vec3.Zero;Assert.Equal(0,PreActionPose.MakeUnitLength(ref zero));Assert.Equal(Vec3.Zero,zero);
        var nan=new Vec3(double.NaN,2,3);Assert.Equal(0,PreActionPose.MakeUnitLength(ref nan));Assert.True(double.IsNaN(nan.X));Assert.Equal(2,nan.Y);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void CubePoseCallbackPassesTheNativeIdStateTriggerLayerAndZeroModifier()
    {
        using var f=new Fixture();var calls=new List<(uint Id,string Trigger,int Layer,bool Modifier,CubeLightModifier Lights,int Offset)>();
        f.Rig.Robot.Lights.Cubes.PoseStatePlayObservedForTest=(id,t,l,m,lights,o)=>calls.Add((id,t,l,m,lights,o));
        f.Rig.Robot.Lights.Cubes.OnActiveObjectPoseStateChanged(123,PoseState.Dirty,PoseState.Known);
        f.Rig.Robot.Lights.Cubes.OnActiveObjectPoseStateChanged(123,PoseState.Known,PoseState.Dirty);
        f.Rig.Robot.Lights.Cubes.OnActiveObjectPoseStateChanged(123,PoseState.Known,PoseState.Known);
        Assert.Equal(2,calls.Count);Assert.Equal("Visible",calls[0].Trigger);Assert.Equal("Connected",calls[1].Trigger);
        Assert.All(calls,c=>{Assert.Equal(123u,c.Id);Assert.Equal(2,c.Layer);Assert.False(c.Modifier);Assert.Equal(0,c.Offset);Assert.All(c.Lights.Leds,led=>Assert.Equal(default(LedPattern),led));Assert.Equal(0u,c.Lights.RotationPeriodMs);Assert.Equal(0,c.Lights.RelativeMode);Assert.Equal(System.Numerics.Vector2.Zero,c.Lights.RelativePoint);});
    }
    private static PlaceRelObjectHelper Helper(Fixture f,bool low=false)
    {
        var component=new BehaviorHelperComponent(()=>1);
        var helper=new PlaceRelObjectHelper(f.M,new StackBlocksBehavior(f.M),component.Factory,7,low,2,3,true)
            {PrecheckForTest=()=>ActionResult.Success,StartActionForTest=_=>{}};
        helper.InitializeOnStack();return helper;
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Theory]
    [InlineData(0u,2)]
    [InlineData(0x02000000u,1)]
    [InlineData(0x03000000u,0)]
    [InlineData(0x03000004u,0)]
    [InlineData(0x03000010u,1)]
    public void PlaceHelperResponseWritesOnlyTheCheckedStatusOrWhiteboard(uint result,int status)
    {
        using var f=new Fixture();var helper=Helper(f);
        helper.RespondToPlaceRelResult((ActionResult)result);Assert.Equal(status,(int)helper.Status);Assert.Equal(0u,helper.Attempts);
        Assert.Equal(result==0x03000010u?7u:(uint?)null,f.M.Whiteboard.NoPreDockPosesObjectId);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void PlaceHelperAllowsThreeAttemptsThenMarksTheTargetAndFails()
    {
        using var f=new Fixture();f.Add(7,Vec3.Zero);f.Add(8,new Vec3(200,0,0),ObjectType.Block_LIGHTCUBE2);f.M.Docking.Carrying.SetCarrying(8);
        var helper=Helper(f);var actions=new List<PlaceRelObjectAction>();helper.StartActionForTest=actions.Add;
        helper.StartPlaceRelObject();helper.RespondToPlaceRelResult((ActionResult)0x03000005);helper.RespondToPlaceRelResult((ActionResult)0x03000005);
        Assert.Equal(3u,helper.Attempts);Assert.Equal(3,actions.Count);
        Assert.All(actions,a=>{Assert.False(a.FlagAt0xA8);Assert.Equal(2,a.OffsetA);Assert.Equal(3,a.OffsetB);Assert.True(a.SkipOffsetTransform);Assert.False(a.DockActionFlagAt0x95);});
        helper.RespondToPlaceRelResult((ActionResult)0x03000005);Assert.Equal(3u,helper.Attempts);Assert.Equal(BehaviorStatus.Failure,helper.Status);
        Assert.True(f.M.Whiteboard.DidFailToUse(7,ObjectActionFailure.StackOnObject,100));
        var low=Helper(f,true);low.MarkFailedToStackOrPlace();Assert.True(f.M.Whiteboard.DidFailToUse(8,ObjectActionFailure.PlaceObjectAt,100));
        var tables=(SortedDictionary<uint,List<FailureInfo>>[])typeof(AIWhiteboard).GetField("_failureTables",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.M.Whiteboard)!;
        var lowPose=Assert.Single(tables[2][8]).Pose;
        Assert.Equal(new Vec3(2,3,0),lowPose.Translation);Assert.InRange(lowPose.AngleAroundZ,0.999999,1.000001);
        Assert.Same(f.M.World.GetLocatedObjectById(7)!.NativePose.Node,lowPose.Node!.Parent);
        Assert.Equal(Vec3.Zero,Assert.Single(tables[1][7]).Pose.Translation);
        f.M.Docking.Carrying.SetCarrying(999);low.MarkFailedToStackOrPlace();Assert.Single(tables[2]);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Theory]
    [InlineData(7u,true)]
    [InlineData(9u,false)]
    public void StackProducerUsesTheStoredTargetTupleAndRequiresTheCarriedTopId(uint carried,bool creates)
    {
        using var f=new Fixture();var behavior=new StackBlocksBehavior(f.M);
        var context=new BehaviorContext{Robot=f.Rig.Robot,Triggers=new AnimationTriggerMap(),AI=new AIComponent(()=>1)};
        typeof(SteppedBehavior).GetProperty("Context",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(behavior,context);
        typeof(StackBlocksBehavior).GetProperty(nameof(StackBlocksBehavior.TopObjectId))!.SetValue(behavior,7u);
        typeof(StackBlocksBehavior).GetProperty(nameof(StackBlocksBehavior.BottomObjectId))!.SetValue(behavior,8u);
        f.M.Docking.Carrying.SetCarrying(carried);
        PlaceRelObjectHelper? produced=null;behavior.PlacementHelperCreatedForTest=h=>produced=h;
        var logs=new List<EngineLogEntry>();f.Rig.Engine.LogEntry+=logs.Add;
        typeof(StackBlocksBehavior).GetMethod("TransitionToStackingBlock",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(behavior,null);
        if(creates)
        {
            Assert.NotNull(produced);Assert.Equal(8u,produced!.Target);Assert.False(produced.Low);Assert.True(produced.SkipTransform);Assert.Equal(0,produced.A);Assert.Equal(0,produced.B);
        }
        else
        {
            Assert.Null(produced);Assert.Equal(StackBlocksBehavior.Phase.PickingUpBlock,behavior.CurrentPhase);
            Assert.Contains(logs,e=>e.Level==EngineLogLevel.Debug&&e.Name=="BehaviorStackBlocks.FailBackToPickup"&&e.Text.EndsWith("wanted to stack, but we aren't carrying a block",StringComparison.Ordinal));
        }
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void TopProducerDoesNotSubstituteTheCachedMidpointForTheUnreadCornerOrder()
    {
        using var f=new Fixture();f.Add(7,new Vec3(100,0,22));f.Add(8,new Vec3(100,50,22),ObjectType.Block_LIGHTCUBE2);
        var map=new Dictionary<BlockConfigurationType,IReadOnlyList<BlockConfiguration>>
            {[BlockConfigurationType.PyramidBase]=new BlockConfiguration[]{new PyramidBase(7,8,new Vec3(12345,6789,42))}};
        var snapshotType=typeof(BlockConfigurationManager).GetNestedType("Snapshot",BindingFlags.NonPublic)!;
        var snapshot=Activator.CreateInstance(snapshotType,new object[]{map,new Dictionary<string,double>()});
        typeof(BlockConfigurationManager).GetField("_snapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(f.M.Configurations,snapshot);
        var behavior=new BuildPyramidBaseBehavior(f.M,"BuildPyramid",true);
        typeof(SteppedBehavior).GetProperty("Context",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(behavior,new BehaviorContext{Robot=f.Rig.Robot,Triggers=new AnimationTriggerMap(),AI=new AIComponent(()=>1)});
        typeof(BuildPyramidBaseBehavior).GetProperty(nameof(BuildPyramidBaseBehavior.StaticBlockId))!.SetValue(behavior,7u);
        typeof(BuildPyramidBaseBehavior).GetProperty(nameof(BuildPyramidBaseBehavior.BaseBlockId))!.SetValue(behavior,8u);
        typeof(BuildPyramidBaseBehavior).GetProperty(nameof(BuildPyramidBaseBehavior.TopBlockId))!.SetValue(behavior,8u);
        // OV74: reach the midpoint recipient via the explicit native strong-cache input, not the host snapshot above.
        f.M.Configurations.NativePyramidBaseStrongCache=()=>new[]{new NativePyramidBase(7,8)};
        int made=0;behavior.PlacementHelperCreatedForTest=h=>{made++;h.PrecheckForTest=()=>ActionResult.Running;};
        typeof(BuildPyramidBaseBehavior).GetMethod("TransitionToPlacingTopBlock",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(behavior,null);
        Assert.Equal(0,made);Assert.Equal(7u,behavior.StaticBlockId);Assert.Equal(BuildPyramidBaseBehavior.Phase.PlacingTopBlock,behavior.CurrentPhase);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void BaseOffsetsUseAllFourCandidatesAndAllOccupiedKeepsTheFirst()
    {
        using var f=new Fixture();f.Add(7,new Vec3(100,0,22));f.Add(8,new Vec3(300,0,22),ObjectType.Block_LIGHTCUBE2);
        float a=99,b=99;PlacementProducerGeometry.UpdateOffsets(f.M,7,8,ref a,ref b);Assert.Equal(-56,a);Assert.Equal(0,b);
        f.Add(9,new Vec3(144,44,22),ObjectType.Block_LIGHTCUBE3);
        PlacementProducerGeometry.UpdateOffsets(f.M,7,8,ref a,ref b);Assert.Equal(56,a);Assert.Equal(0,b);
        a=3;b=4;PlacementProducerGeometry.UpdateOffsets(f.M,99,8,ref a,ref b);Assert.Equal(3,a);Assert.Equal(4,b);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void VectorFailureReturnsZeroButTheOutDistanceKeepsTheAliasedCandidateWord()
    {
        bool previous=EngineErrorState.ErrorFlagSet;
        try
        {
            using var one=new Pose3d("one");using var two=new Pose3d("two");
            using var first=new Pose3d(Rotation3d.Identity,new Vec3(1,2,3),parent:one);
            using var second=new Pose3d("",two);var logs=new List<string>();
            using var scope=Pose3d.UseSourceLog((_,_,key,_)=>logs.Add(key));
            float distance=56;Assert.False(PlacementProducerGeometry.TryDistanceSquared(first,second,ref distance));Assert.Equal(56,distance);
            // Two unrelated roots do not have a common parent merely because both parent pointers are null.
            Assert.False(PlacementProducerGeometry.TryDistanceSquared(one,two,ref distance));Assert.Equal(56,distance);
            Assert.Equal(Vec3.Zero,PlacementProducerGeometry.Vector(first,second));Assert.Contains("ComputeVectorBetween.NoCommonParent",logs);Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally{EngineErrorState.ErrorFlagSet=previous;}
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public void OwnersAreConstructedDockingThenCarryingAndRawDropSendsNothing()
    {
        using var rig=new M4ControlTests.Rig();var order=new List<string>();rig.Engine.DockingCarryingOwnerObservedForTest=order.Add;rig.Robot.Carrying.SetCarrying(99);rig.ToSynced();
        var native=rig.Engine.Robot!;Assert.Equal(new[]{"new Docking","new Carrying"},order);
        Assert.Null(native.DockingOwner!.TargetId);Assert.Equal(0,native.DockingOwner.Active);Assert.False(native.DockingOwner.Success);
        Assert.Equal(3,native.DockingOwner.MarkerCode);Assert.Equal(0u,native.DockingOwner.Slot14|native.DockingOwner.Slot18|native.DockingOwner.Slot1C);
        Assert.Same(native.CarryingOwner,rig.Robot.Carrying);
        Assert.Null(native.CarryingOwner!.CarriedObjectId);Assert.Null(native.CarryingOwner.CarriedOnTopId);Assert.Equal(3,native.CarryingOwner.NativeMarkerCode);
        var oldCarry=native.CarryingOwner;int mark=0;
        native.Lifetime.Bind(0x288,_=>mark=rig.Mark());
        native.Lifetime.Bind(0x27C,_=>{Assert.Null(native.CarryingOwner);Assert.Null(native.DockingOwner);Assert.Empty(rig.SentSince(mark));});
        rig.Engine.Robots.RemoveRobot(1,false);Assert.Equal(new[]{"new Docking","new Carrying","drop Carrying","drop Docking"},order);
        Assert.Null(oldCarry.CarriedObjectId);
    }
    // reach: unit (direct component/helper entry or substituted fixture state; complete production caller is not asserted).
    [Fact]
    public async Task AbortReturnsSendResultAndKeepsHostPendingAndActiveState()
    {
        using var f=new Fixture();var target=f.Add(7,new Vec3(150,0,22));using var cancel=new CancellationTokenSource();
        f.Native.SendFault=m=>m is not AbortDocking;
        var run=f.M.Docking.DockAsync(target,target.Markers[0],DockAction.PickupLow,PathMotionProfile.Default,cancel:cancel.Token);
        var pendingField=typeof(DockingSystem).GetField("_pending",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var activeField=typeof(DockingSystem).GetField("_active",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var pending=pendingField.GetValue(f.M.Docking);var active=activeField.GetValue(f.M.Docking);
        Assert.NotNull(pending);Assert.NotNull(active);Assert.False(run.IsCompleted);
        Assert.False(f.M.Docking.Abort());Assert.Same(pending,pendingField.GetValue(f.M.Docking));Assert.Equal(active,activeField.GetValue(f.M.Docking));Assert.False(run.IsCompleted);
        f.Native.SendFault=_=>true;
        Assert.True(f.M.Docking.Abort());Assert.Same(pending,pendingField.GetValue(f.M.Docking));Assert.Equal(active,activeField.GetValue(f.M.Docking));Assert.False(run.IsCompleted);
        Assert.Equal(2,f.M.Docking.Sent.OfType<AbortDocking>().Count());
        Assert.All(f.M.Docking.Sent.OfType<AbortDocking>(),m=>Assert.Equal(new byte[]{0x43},m.ToBytes()));
        cancel.Cancel();await run; // the existing host run's own completion owns its cleanup, never AbortDocking.
    }
}
