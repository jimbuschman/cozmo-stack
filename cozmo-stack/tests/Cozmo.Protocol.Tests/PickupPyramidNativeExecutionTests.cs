using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PickupPyramidNativeExecutionTests
{
    private static string Resources()
    {
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null;dir=dir.Parent)
        {var path=Path.Combine(dir.FullName,"re-analysis","obb","assets","cozmo_resources");if(File.Exists(Path.Combine(path,"config","engine","behaviorSystem","activities_config.json")))return path;}
        throw new InvalidOperationException("Shipped resources absent");
    }
    internal sealed class Fixture:IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new(Resources());
        internal EngineRobot Robot=>Rig.Engine.Robot!;
        internal NativeBehaviorRuntime Runtime=>Robot.NativeBehaviorRuntime!;
        internal ManipulationSystem M=>Runtime.Manipulation!;
        internal float Head=-.15f,Lift,Yaw,X,Y;
        private int _motorMark;
        internal Fixture()
        {
            Rig.ToSynced();Rig.Calibrate();Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,head:Head);
            for(int i=0;i<35&&Rig.Robot.CameraSettings.Calibration is null;i++)
            {
                Rig.Tick();var tag=Rig.Engine.NvStorage!.InFlightTag;if(tag is null)continue;
                var writer=new CladWriter();if(tag==CameraCalibration.NvEntryTag)
                {foreach(float value in new[]{290f,291f,160f,120f,0f})writer.F32(value);writer.U16(240);writer.U16(320);for(int k=0;k<8;k++)writer.F32(0);}
                Rig.Data(new NVOpResult{Tag=tag.Value,Op=0,Result=tag==CameraCalibration.NvEntryTag?(sbyte)0:(sbyte)1,Data=writer.ToArray()});Rig.Tick();
            }
            Assert.NotNull(Rig.Robot.CameraSettings.Calibration);Assert.Same(Rig.Robot.CameraSettings.Calibration,M.Vision.Calibration);
        }
        internal ObservableObject Add(uint id=7,float x=150,float z=22)
        {
            var obj=new ObservableObject(id,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {Pose=new Pose3d(Rotation3d.Identity,new Vec3(x,0,z),parent:Robot.GetNativeOrigin(Robot.CurrentOriginId)),OriginId=Robot.CurrentOriginId,PoseState=PoseState.Known,LastObservedTimestamp=Robot.StoredState!.Timestamp};
            obj.LastObservedMarkers=obj.Markers.Select(m=>m.Code).ToArray();
            foreach(var marker in obj.Markers)marker.LastObservedTimestamp=obj.LastObservedTimestamp;
            M.World.AddLocatedObject(obj);return obj;
        }
        internal void Motors(RobotStatusFlag extra=0)
        {
            var messages=Rig.SentSince(_motorMark);_motorMark=Rig.Mark();
            foreach(var message in messages)switch(message)
            {
                case SetHeadAngle h:Head=h.AngleRad;Rig.Data(new MotorActionAck{ActionId=h.ActionId});break;
                case SetBodyAngle b:Yaw=b.AngleRad;Rig.Data(new MotorActionAck{ActionId=b.ActionId});break;
                case SetLiftHeight l:Lift=(float)Math.Asin((l.HeightMm-45f)/66f);Rig.Data(new MotorActionAck{ActionId=l.ActionId});break;
            }
            var state=Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos|extra,origin:Robot.CurrentOriginId,frame:Robot.PoseFrameId,x:X,y:Y,head:Head,liftAngle:Lift);state.Pose.Angle=Yaw;Rig.Data(state);Rig.Tick();
        }
        internal void Image(uint id,bool cube=false,float cubeX=150,int modes=1<<1)
        {
            M.Vision.Enabled=true;M.Vision.ModeEnableMask=modes;
            uint stamp=Robot.StoredState!.Timestamp;
            Rig.Data(new ImageImuData{ImageId=stamp,RateX=0,RateY=0,RateZ=0,Line2Number=0});Rig.Tick();
            var cal=M.Vision.Calibration!;var image=new GrayImage(cal.Columns,cal.Rows);image.Fill(150);
            if(cube)
            {
                using var state=HistRobotState.From(Robot.GetCheckedMainPose(),Robot.StoredState!);using var camera=Robot.CopyHistoricalCameraPose(state);
                using var flatCamera=camera.GetWithRespectToRoot();
                using var pose=new Pose3d(Rotation3d.Identity,new Vec3(cubeX,0,22));
                MarkerRenderer.DrawObject(image,MarkerLibrary.EmbeddedOrNull!,new CameraModel(cal,flatCamera),ObjectType.Block_LIGHTCUBE1,pose);
            }
            M.Vision.HandOverFrame(new CameraFrame{ImageId=id,Timestamp=stamp,Width=cal.Columns,Height=cal.Rows,Encoding=1,RawPayload=image.Pixels});
            M.Vision.WaitForProcessorIdle();Rig.Engine.VisionUpdateAllResultsFailed!();Rig.Tick();
        }
        public void Dispose()=>Rig.Dispose();
    }
    private sealed class PayloadRunner(ObjectInteractionCompletion payload):ActionRunner(0x16,0)
    {
        internal ObjectInteractionCompletion Value=payload;
        protected override string ActionName=>"supplied completion producer";
        public override ObjectInteractionCompletion GetObjectInteractionCompletion()=>Value;
        public override uint CheckIfDone()=>0;
        public override bool CanInterrupt()=>false;
        public override void WatcherEnding(){Value=new(99,99,99,99,99,5,false);base.WatcherEnding();}
    }
    [Fact]
    public void FullCarrierSurvivesCurrentTagProxyPrepDestructionAndWatcherSnapshot()
    {
        var expected=new ObjectInteractionCompletion(7,9,uint.MaxValue,uint.MaxValue,uint.MaxValue,2,true);
        var child=new PayloadRunner(expected);Assert.True(child.SetTag(778899));
        var compound=new CompoundActionSequential(()=>1f);compound.AddAction(child,false,false);compound.SetNativeCompletionProxy(child.Tag);
        var watcher=new ActionWatcher{ClockSeconds=()=>1f};compound.Watcher=watcher;
        var records=new List<RobotCompletedAction>();watcher.RegisterCallback(records.Add);
        Assert.Equal(0u,compound.Update());Assert.True(child.DestructionObserved);
        Assert.Equal(expected,compound.GetObjectInteractionCompletion());compound.Prep();compound.WatcherEnding();watcher.Update();
        Assert.Equal(expected,records.Last(r=>r.Tag==compound.Tag).ObjectInteraction);
        Assert.Equal(new byte[]{7,0,0,0,9,0,0,0,255,255,255,255,255,255,255,255,255,255,255,255,2,1},expected.PackBody());
        Assert.Equal(36,new RobotCompletedAction(1,0x16,0,Array.Empty<uint>(),0,ObjectInteraction:expected).PackBody().Length);
    }
    [Fact]
    public void UndefinedPublicPayloadStillDeliversDefinedFlagAndScalarLocally()
    {
        using var f=new Fixture();var app=new List<ActionCompletionReport>();var local=new List<RobotCompletedAction>();
        f.Rig.Engine.ActionCompleted+=app.Add;
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)local.Add(r);});
        var original=new RobotCompletedAction(123,0x16,0x0300001D,Array.Empty<uint>(),0,ObjectInteraction:new(0,0,0,0,0,0,true,false));
        f.Rig.Engine.BroadcastActionCompletion(original);f.Rig.Engine.DrainAppDelivery();
        Assert.Empty(app);Assert.Equal(original,Assert.Single(local));Assert.True(local[0].ObjectInteraction!.Flag);
    }
    [Fact]
    public void OwnedVerifyUsesActualLocal44IdAndSpecificObservedMarkerBeforeTerminalParallelFailure()
    {
        using var f=new Fixture();var cube=f.Add();var marker=cube.Markers[0];
        var action=new NativeVerifyObjectRunner(f.M,7,unchecked((short)marker.Code));
        Assert.Equal(0x2E,action.Type);Assert.Equal(1u,action.RequiredTrackMask);
        try
        {
            Assert.Equal(0,action.Init());
            f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(1,2,1,8));Assert.False(action.HaveSeen());
            f.Rig.Engine.AnimationMessages.Deliver(0x44,true,new NativeObservedObject(1,2,1,7));Assert.True(action.HaveSeen());Assert.Equal(0u,action.CheckIfDone());
        }
        finally{action.Prep();action.WatcherEnding();}
    }
    private static string Describe(ActionRunner a)
    {
        string value=a.Type.ToString("X")+":"+a.State.ToString("X8");
        if(a is NativePanTiltRunner)
        {
            var c=(CompoundAction)typeof(NativePanTiltRunner).GetField("Compound",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(a)!;
            value+="["+string.Join(",",c.Children.Select(Describe))+"]";
        }
        if(a is NativeTurnTowardsObjectRunner)
        {
            var v=(ActionRunner?)typeof(NativeTurnTowardsObjectRunner).GetField("_verification",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(a);
            if(v is not null)value+="verify="+v.State.ToString("X8");
        }
        return value;
    }
    [Fact]
    public void QueuedPickupUsesRealMotorImagesDockFlagRawLoadAndCarryCompletion()
    {
        // Actual concrete owned pickup/ActionList path, supplied located scene and firmware inputs.
        using var f=new Fixture();f.Add();var action=new NativePickupObjectRunner(f.M,7);
        var reports=new List<RobotCompletedAction>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)reports.Add(r);});
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,action,0);bool dock=false;uint image=0;int observed=0;var trace=new List<string>();
        using var objects=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x44,true,p=>{if(p is NativeObservedObject o&&o.ObjectId==7)observed++;});
        try
        {
            for(int i=0;i<100&&!action.DestructionObserved;i++)
            {
                trace.Add("head="+f.Head+" markers="+f.M.Vision.LastResult?.Markers.Count+" "+action.State.ToString("X8")+"/"+string.Join(",",action.Prepared?.Children.Select(c=>Describe(c))??Array.Empty<string>()));
                f.Motors();
                if(!dock&&f.Rig.SentSince(0).OfType<DockWithObject>().Any())
                {
                    var request=Assert.Single(f.Rig.SentSince(0).OfType<DockWithObject>());Assert.True(request.Field8);Assert.Equal((byte)2,request.DockingMethod);
                    f.Motors(RobotStatusFlag.IsPickingOrPlacing);f.Rig.Data(new LiftLoad{Field0=true});f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=2});f.Motors();dock=true;
                }
                if(!dock)f.Image(++image,action.Prepared?.Children.OfType<NativeTurnTowardsObjectRunner>().Any(c=>c.Initialized)==true);
            }
            Assert.True(dock,"dock not sent; state="+action.State.ToString("X8")+" observed="+observed+" trace="+string.Join(" | ",trace));
            Assert.True(observed>0);Assert.Equal(0u,action.State);Assert.True(action.DestructionObserved);
            Assert.Equal(7u,f.Robot.CarryingOwner!.CarriedObjectId);Assert.Null(f.Robot.DockingOwner!.TargetId);Assert.Equal((ushort)3,f.Robot.DockingOwner.MarkerCode);
            var done=Assert.Single(reports.Where(r=>r.Tag==action.Tag));Assert.Equal(7u,done.ObjectInteraction!.Word0);Assert.Equal((byte)1,done.ObjectInteraction.Count);Assert.False(done.ObjectInteraction.Flag);
        }
        finally{f.Robot.ActionList.Clear();}
    }
    [Fact]
    public void NativeMarkerConstructionAndUnsignedTimestampQueryRetainEarlierNewerMarker()
    {
        using var f=new Fixture();var obj=new ObservableObject(71,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1));
        Assert.All(obj.Markers,m=>Assert.Null(m.LastObservedTimestamp));
        obj.InitializeNativeMarkerTimes(); //explicit NEW native-construction input; public unknown stays unknown.
        Assert.All(obj.Markers,m=>Assert.Equal(0u,m.LastObservedTimestamp));
        obj.LastObservedTimestamp=100;obj.Markers[0].LastObservedTimestamp=200;obj.Markers[1].LastObservedTimestamp=100;
        obj.LastObservedMarkers=new[]{obj.Markers[1].Code}; //legacy list deliberately does not contain retained newer marker.
        Assert.Equal(new[]{obj.Markers[0],obj.Markers[1]},NativeObservedMarkers.Get(obj,100));
        Assert.Empty(NativeObservedMarkers.Get(obj,0));obj.Markers[2].LastObservedTimestamp=null;
        Assert.Throws<NativeActionUnavailable>(()=>NativeObservedMarkers.Get(obj,100)); //explicit unknown is never silently zero.
    }
    [Fact]
    public void RealOwnedRetirementAbortsActiveRetainedRequestBeforeLatePickupCannotAttach()
    {
        using var f=new Fixture();var target=f.Add();var runner=new NativePickupObjectRunner(f.M,7);
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,runner,0);f.Rig.Tick();
        var marker=target.Markers.First(m=>m.Face==BlockFace.Front);
        Assert.True(f.M.Docking.StartNativeDock(7,unchecked((short)marker.Code),DockAction.PickupLow,PathMotionProfile.Default));
        f.Motors(RobotStatusFlag.IsPickingOrPlacing);Assert.Equal((byte)1,f.Robot.DockingOwner!.Active);
        int mark=f.Rig.Mark();f.Robot.ActionList.Clear();
        Assert.True(runner.DestructionObserved);Assert.Single(f.Rig.SentSince(mark).OfType<AbortDocking>());
        Assert.Null(f.Robot.DockingOwner.TargetId);Assert.Equal((ushort)3,f.Robot.DockingOwner.MarkerCode);
        f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=2});f.Rig.Tick();Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);
        f.Rig.Data(new LiftLoad{Field0=true});f.Rig.Tick();Assert.Equal((byte)0,runner.LoadState); //action-owned DA handle retired.
        Assert.Equal(0,f.Rig.Robot.Motion.LockedTracks);
    }
    private sealed class Parent:SteppedBehavior
    {
        internal Action<Parent>? Entry;
        internal Parent():base("native-pickup-response-unit","Probe"){}
        public override bool IsRunnable(BehaviorContext context)=>true;
        protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
        protected override void OnStart()=>Entry?.Invoke(this);
    }
    private sealed class FailurePayloadRunner(ObjectInteractionCompletion value):ActionRunner(0x16,0)
    {
        protected override string ActionName=>"supplied original failed pickup report";
        public override ObjectInteractionCompletion GetObjectInteractionCompletion()=>value;
        public override uint CheckIfDone()=>0x0300001D;
        public override bool CanInterrupt()=>false;
    }
    [Fact]
    public void OriginalImmutablePayloadSurvivesTheActualResponseAnimationCallback()
    {
        //Supplied original result unit; real parent84/response ActionList/CA/CB, not successful native pickup proof.
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(Resources());
        var parent=new Parent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);
        var helper=new NativePickupBlockHelper(f.M,parent,f.Runtime.AI.Helpers.Factory,7,0,false,true);
        var expected=new ObjectInteractionCompletion(7,8,uint.MaxValue,uint.MaxValue,uint.MaxValue,2,true);
        RobotCompletedAction? received=null;var original=new FailurePayloadRunner(expected);
        parent.Entry=_=>helper.StartWithResponse(original,r=>received=r);var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);
        try
        {
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            var animations=new HashSet<byte>();
            for(int i=0;i<30&&received is null;i++)
            {
                f.Rig.Tick();byte tag=f.Rig.Robot.Animations.CurrentTag;
                if(tag!=0&&animations.Add(tag)){f.Rig.Data(new AnimationStarted{AnimId=tag});f.Rig.Data(new AnimationEnded{AnimId=tag});f.Rig.Tick();}
            }
            Assert.NotEmpty(animations);Assert.NotNull(received);Assert.Equal(original.Tag,received!.Value.Tag);
            Assert.Equal(0x0300001Du,received.Value.Result);Assert.Equal(expected,received.Value.ObjectInteraction);
        }
        finally{manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();}
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LateNamedFaceFailsClearlyAndRetiresOnlyItsOwnedSubtreeWithoutPickupContinuation(bool uiEnabled)
    {
        //Injected LOCAL46 recipient unit, actual LastFace motor/Needs/Mood/error-boundary lifecycle.
        using var f=new Fixture();f.Add();var origin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        void Face(string? name)
        {
            var tracked=new TrackedFace(new DetectedFace(11,new FaceRect(0,0,20,20),new Vec2(5,5),new Vec2(15,5),Name:name),1)
            {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(150,0,60),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId))};
            f.M.Vision.Faces.AddOrUpdateFace(tracked,f.Robot.GetCheckedMainPose(),false);
        }
        for(int i=0;i<4;i++)Face(null);
        var parent=new Parent();parent.BindActionCompletionOwner(f.Rig.Robot,f.Robot);
        var helper=new NativePickupBlockHelper(f.M,parent,f.Runtime.AI.Helpers.Factory,7,Radians.Pi,true,true);
        var last=new NativeLastFaceRunner(f.M,Radians.Pi,true,0x187,0x188);var pickup=new NativePickupObjectRunner(f.M,7);
        var sequence=helper.BuildPickupCompound(pickup,last,new NativeTurnTowardsObjectRunner(f.M,7,NativeTurnTowardsObjectRunner.AnyCode,Radians.Pi,false,false));
        bool callback=false;parent.Entry=_=>helper.StartWithResponse(sequence,_=>callback=true);
        var manager=f.Robot.BehaviorManagerOwner!;manager.Add(parent);var missing=new List<string>();SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=missing.Add;
        try
        {
            Assert.True(manager.StartAsync(parent.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            var unrelated=new NativeWaitForImagesRunner(f.M,10,(VisionMode)1,0);
            Assert.Equal(0,f.Robot.ActionList.QueueAction(QueueActionPosition.InParallel,unrelated));
            f.Motors();Assert.Equal(0,last.FaceState);
            typeof(CozmoEngine).GetProperty("UiMessageProcessingEnabled",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(f.Rig.Engine,uiEnabled);
            Face("Ada");f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(11,1));
            for(int i=0;i<30&&(parent.HasCurrentAction||!sequence.DestructionObserved);i++)f.Motors();
            Assert.False(parent.HasCurrentAction,"face="+last.FaceState+" verified="+last.VerifiedId+" tree="+Describe(last)+" missing="+string.Join(" | ",missing));Assert.False(callback);Assert.True(sequence.DestructionObserved);Assert.True(last.DestructionObserved);Assert.True(pickup.DestructionObserved);
            Assert.DoesNotContain(f.Rig.SentSince(0),m=>m is DockWithObject);
            Assert.False(unrelated.DestructionObserved);Assert.Equal(EngineActionResult.Running,unrelated.State);
            Assert.Equal(0,f.Rig.Robot.Motion.LockedTracks);Assert.Equal(0,f.Robot.ActionList.Watcher.HostUpdateDepth);
            Assert.Contains(missing,line=>line.Contains("NativeActionUnavailable")&&line.Contains("SayText"));
        }
        finally{SteppedBehavior.MissingReported-=missing.Add;manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);parent.RetireActionCompletionOwner();f.Robot.ActionList.Clear();}
    }

    [Fact]
    public void ActualPayloadGetterOverwritesTargetWordZeroWithTheActualCarryingSet()
    {
        //Actual producer component with explicit actual-owner carry input; not firmware pickup proof.
        using var f=new Fixture();f.Add();var action=new NativePickupObjectRunner(f.M,7);
        try
        {
            Assert.Equal(0,action.Init());
            Assert.False(action.GetObjectInteractionCompletion().IsPayloadDefined);
            f.Robot.CarryingOwner!.SetCarrying(9);
            var payload=action.GetObjectInteractionCompletion();
            Assert.True(payload.IsPayloadDefined);Assert.Equal(9u,payload.Word0);Assert.Equal((byte)1,payload.Count);Assert.False(payload.Flag);
            Assert.Equal(new byte[]{9,0,0,0,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,1,0},payload.PackBody());
        }
        finally{action.State=EngineActionResult.Running;action.Prep();action.WatcherEnding();f.M.Docking.UnsetCarryingObjects(false);}
    }
    [Fact]
    public void RealNoObjectFailureKeepsInternalFlagAndSuppressesUndefinedPublicPayload()
    {
        using var f=new Fixture();f.Add();var action=new NativePickupObjectRunner(f.M,7);
        var reports=new List<RobotCompletedAction>();var app=new List<ActionCompletionReport>();f.Rig.Engine.ActionCompleted+=app.Add;
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)reports.Add(r);});
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,action,0);
        try
        {
            NativeVisuallyVerifyNoObjectRunner? noObject=null;
            for(int i=0;i<20;i++)
            {f.Motors();noObject=action.Prepared?.Children.OfType<NativeVisuallyVerifyNoObjectRunner>().Single();if(noObject is {Positioning:false})break;}
            Assert.NotNull(noObject);Assert.False(noObject!.Positioning);
            f.Add(8,150,66); //explicit newly admitted object in checked raised-pose sphere, timestamp current.
            f.Rig.Tick();f.Rig.Engine.DrainAppDelivery();
            Assert.Equal(0x0300001Du,action.State);Assert.True(action.DestructionObserved);
            var done=Assert.Single(reports.Where(r=>r.Tag==action.Tag));Assert.True(done.ObjectInteraction!.Flag);Assert.False(done.ObjectInteraction.IsPayloadDefined);
            Assert.DoesNotContain(app,r=>r.Tag==action.Tag);Assert.DoesNotContain(f.Rig.SentSince(0),m=>m is DockWithObject);
        }
        finally{f.Robot.ActionList.Clear();}
    }
    [Fact]
    public void RawDockingBitIsWrittenBeforeUnknownOriginRejectionAndChargerRecipient()
    {
        using var f=new Fixture();var docking=f.Robot.DockingOwner!;
        var high=f.Rig.MakeState(RobotStatusFlag.IsPickingOrPlacing,origin:999,frame:f.Robot.PoseFrameId);
        f.Rig.Data(high);f.Rig.Tick();Assert.Equal((byte)1,docking.Active);Assert.Equal(high.Timestamp,f.Robot.StoredState!.Timestamp);Assert.Null(f.Robot.AcceptedState);
        var low=f.Rig.MakeState(origin:999,frame:f.Robot.PoseFrameId);f.Rig.Data(low);f.Rig.Tick();Assert.Equal((byte)0,docking.Active);
        Assert.Null(docking.TargetId);Assert.False(docking.Success); //no request/success gate was introduced.
    }
    [Fact]
    public void HistoricalCameraUsesTheSameOwnedComputedPoseAndHeadInsteadOfCurrentMain()
    {
        using var f=new Fixture();f.Head=0;f.Motors();f.Motors();uint stamp=f.Robot.StoredState!.Timestamp;
        using var history=new HistRobotState();Assert.True(f.M.Vision.History.ComputeAndInsertStateAt(stamp,history,out uint origin));
        Assert.Equal(f.Robot.CurrentOriginId,origin);Assert.Equal(0f,history.State.HeadAngle);
        f.X=100;f.Head=.4f;f.Motors();
        using var camera=f.Robot.CopyHistoricalCameraPose(history);
        Assert.Same(history.Pose.Node,camera.Node!.Parent);
        Assert.Equal(4.520000457763672f,(float)camera.Translation.X);Assert.Equal(0f,(float)camera.Translation.Y);Assert.Equal(41f,(float)camera.Translation.Z);
        using var rootCamera=camera.GetWithRespectToRoot();Assert.Equal(4.520000457763672f,(float)rootCamera.Translation.X);
        Assert.Equal(100f,(float)f.Robot.GetCheckedMainPose().Translation.X);
    }
    private sealed class SuppliedFaceDetector:IFaceDetector
    {
        public bool IsAvailable=>true;
        public string Description=>"supplied face detector result at proprietary boundary";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image,uint timestamp)=>new[]{new DetectedFace(11,new FaceRect(120,40,60,60),new Vec2(135,60),new Vec2(165,60),RollRad:0)};
    }
    [Fact]
    public void MixedRealWorkerFrameUsesHistoricalCameraForMarkersWithoutReplacingExistingFaceCameraInput()
    {
        // Same scene/head/face input with marker mode off/on; preserve the existing labelled face adapter.
        using var f=new Fixture();var cube=f.Add();f.M.Vision.FaceDetector=new SuppliedFaceDetector();
        var diagnostic=new List<string>();f.M.Vision.Log+=diagnostic.Add;f.M.World.Log+=diagnostic.Add;
        f.Image(1,true,modes:1<<2);
        var before=Assert.Single(f.M.Vision.LastFaces).HeadPose;
        var faceTranslation=before.Translation;var faceRotation=before.Quaternion;
        var objects=new List<NativeObservedObject>();
        var eventOrder=new List<(string Kind,uint Stamp)>();var faceTimesAtWorld=new List<(uint World,uint? Face)>();
        f.M.World.ObjectObserved+=o=>{eventOrder.Add(("world",o.Timestamp));faceTimesAtWorld.Add((o.Timestamp,f.M.Vision.Faces.GetFace(11)?.LastObservedTimestamp));};
        f.M.Vision.Faces.FaceObserved+=o=>eventOrder.Add(("face",o.Timestamp));
        using var observed=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x44,true,p=>{if(p is NativeObservedObject value)objects.Add(value);});
        //Actual pose confirmer requires repeated observations; no fabricated44 or timestamp writes.
        for(uint frame=2;frame<=10&&objects.Count==0;frame++)
        {
            f.Motors();f.Image(frame,true,modes:(1<<1)|(1<<2));
            var after=Assert.Single(f.M.Vision.LastFaces).HeadPose;
            Assert.Equal(faceTranslation,after.Translation);Assert.Equal(faceRotation,after.Quaternion);
        }
        Assert.True(objects.Count>0,"markers="+f.M.Vision.LastResult?.Markers.Count+" head="+f.Head+" lift="+f.Lift+" world="+string.Join(" | ",diagnostic));
        Assert.Contains(objects,o=>o.ObjectId==7);
        Assert.NotEmpty(NativeObservedMarkers.Get(cube,cube.LastObservedTimestamp));
        Assert.NotEmpty(faceTimesAtWorld);
        Assert.All(faceTimesAtWorld,t=>Assert.True(t.Face<t.World,"world callback must precede this frame's face mutation"));
        foreach(var worldEvent in eventOrder.Where(e=>e.Kind=="world"))
            Assert.True(eventOrder.IndexOf(worldEvent)<eventOrder.FindIndex(e=>e.Kind=="face"&&e.Stamp==worldEvent.Stamp));
    }
    [Fact]
    public void RealWorkerReleasesFrameAndPoseGatesBeforePublicFrameAndProcessedImageRecipients()
    {
        using var f=new Fixture();bool? hostHeld=null,localHeld=null;
        void Frame(VisionFrameResult _) => hostHeld=Monitor.IsEntered(f.M.World.FrameGate)||Monitor.IsEntered(f.Robot.PoseTreeGate);
        f.M.Vision.FrameProcessed+=Frame;
        using var subscription=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x43,true,_=>localHeld=Monitor.IsEntered(f.M.World.FrameGate)||Monitor.IsEntered(f.Robot.PoseTreeGate));
        try
        {
            f.Image(1,true);
            Assert.Equal(false,hostHeld);Assert.Equal(false,localHeld);
        }
        finally{f.M.Vision.FrameProcessed-=Frame;}
    }
    [Fact]
    public void ShippedConfiguredPickupRunsManagerHelperFirstTurnsWorker44DockVerifyAndTypedCallback()
    {
        //Source-admitted pre-action pose and simulated firmware inputs. No injected5A or substituted executor.
        using var f=new Fixture();var cube=f.Add();f.Rig.Robot.Animations.LoadFrom(Resources());
        var input=DockPreActionPoses.Get(new PreActionPoseInput(cube,PreActionType.Docking,false,DockActionBase.PreActionAngleToleranceRad,0,false,0),
            f.Robot.GetCheckedMainPose(),null,()=>f.M.GetObstacles(f.Robot.GetCheckedMainPose()));
        Assert.Equal(ActionResult.Success,input.Result);using var goal=input.Poses[input.ClosestIndex].WorldPose.GetWithRespectToRoot();
        f.X=(float)goal.Translation.X;f.Y=(float)goal.Translation.Y;f.Yaw=(float)goal.AngleAroundZ;f.Motors();
        cube.LastObservedTimestamp=f.Robot.StoredState!.Timestamp;foreach(var marker in cube.Markers)marker.LastObservedTimestamp=cube.LastObservedTimestamp;
        f.Robot.Progression!.Unlocked.Add(6);
        var pickup=Assert.IsType<NativePickUpCubeBehavior>(f.Runtime.Bound["SparksPickupSingleCubeForPyramid"]);
        Assert.True(pickup.IsRunnable(f.Runtime.Context));Assert.True(pickup.CheckExecutionAvailability());
        var reports=new List<RobotCompletedAction>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)reports.Add(r);});
        var app=new List<ActionCompletionReport>();f.Rig.Engine.ActionCompleted+=app.Add;
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(pickup.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var animations=new HashSet<byte>();bool dock=false,firstTurns=false;uint image=0;
        try
        {
            for(int i=0;i<150&&ReferenceEquals(f.Robot.BehaviorManagerOwner.Current,pickup);i++)
            {
                f.Motors();byte tag=f.Rig.Robot.Animations.CurrentTag;
                if(tag!=0&&animations.Add(tag)){f.Rig.Data(new AnimationStarted{AnimId=tag});f.Rig.Data(new AnimationEnded{AnimId=tag});f.Rig.Tick();}
                if(pickup.Helper is {} helper)
                {
                    Assert.True(helper.Flag104);Assert.Equal(BitConverter.UInt32BitsToSingle(0x3FC90FDB),helper.MaxTurn);
                    if(f.Robot.ActionList.GetCurrentAction() is CompoundActionSequential seq&&seq.Children.Any(c=>c is NativeLastFaceRunner))
                        firstTurns=true;
                    if(!dock&&f.Rig.SentSince(0).OfType<DockWithObject>().Any())
                    {
                        Assert.True(Assert.Single(f.Rig.SentSince(0).OfType<DockWithObject>()).Field8);
                        f.Motors(RobotStatusFlag.IsPickingOrPlacing);f.Rig.Data(new LiftLoad{Field0=true});f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=2});f.Motors();dock=true;
                    }
                    if(!dock)
                    {
                        var currentSequence=f.Robot.ActionList.GetCurrentAction() as CompoundActionSequential;
                        var realPickup=currentSequence?.Children.OfType<NativePickupObjectRunner>().FirstOrDefault();
                        bool visible=realPickup?.Prepared?.Children.OfType<NativeTurnTowardsObjectRunner>().Any(c=>c.Initialized)==true;
                        f.Image(++image,visible);
                    }
                }
            }
            f.Rig.Engine.DrainAppDelivery();
            Assert.True(firstTurns);Assert.True(dock);Assert.NotNull(pickup.Helper);Assert.Equal(1u,pickup.Helper!.Attempts);Assert.Equal(BehaviorStatus.Complete,pickup.Helper.Status);
            Assert.False(pickup.HasCurrentAction);Assert.Equal(7u,f.Robot.CarryingOwner!.CarriedObjectId);
            var completed=Assert.Single(reports.Where(r=>r.ObjectInteraction is {IsPayloadDefined:true}&&r.Result==0));
            Assert.Equal(7u,completed.ObjectInteraction!.Word0);Assert.Contains(app,r=>r.Tag==completed.Tag&&r.ObjectInteraction==completed.ObjectInteraction);
            Assert.True(animations.Count>=2);
        }
        finally{f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Robot.ActionList.Clear();}
    }

}
