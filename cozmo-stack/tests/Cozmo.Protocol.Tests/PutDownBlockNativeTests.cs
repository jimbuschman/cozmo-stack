using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PutDownBlockNativeTests
{
    private static string Resources()
    {
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null;dir=dir.Parent)
        {var path=Path.Combine(dir.FullName,"re-analysis","obb","assets","cozmo_resources");if(File.Exists(Path.Combine(path,"config","engine","behaviorSystem","activities_config.json")))return path;}
        throw new InvalidOperationException("Shipped resources absent");
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new(Resources());
        internal EngineRobot Robot=>Rig.Engine.Robot!;
        internal NativeBehaviorRuntime Runtime=>Robot.NativeBehaviorRuntime!;
        internal ManipulationSystem M=>Runtime.Manipulation!;
        internal NativePutDownBlockBehavior Behavior=>Assert.IsType<NativePutDownBlockBehavior>(Runtime.Bound["PutDownBlock"]);
        internal Fixture(){Rig.ToSynced();Rig.Calibrate();Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos);M.Vision.UpdateCameraCalibration(CameraCalibration.Nominal());}
        internal void InstallNvCalibration()
        {
            for(int i=0;i<35&&Rig.Robot.CameraSettings.Calibration is null;i++)
            {
                Rig.Tick();var tag=Rig.Engine.NvStorage!.InFlightTag;if(tag is null)continue;
                var writer=new CladWriter();if(tag==CameraCalibration.NvEntryTag)
                {foreach(float value in new[]{290f,291f,160f,120f,0f})writer.F32(value);writer.U16(240);writer.U16(320);for(int k=0;k<8;k++)writer.F32(0);}
                Rig.Data(new NVOpResult{Tag=tag.Value,Op=0,Result=tag==CameraCalibration.NvEntryTag?(sbyte)0:(sbyte)1,Data=writer.ToArray()});Rig.Tick();
            }
            Assert.NotNull(Rig.Robot.CameraSettings.Calibration);Assert.Same(Rig.Robot.CameraSettings.Calibration,M.Vision.Calibration);
        }
        internal void Image(uint id,uint mask)
        {
            M.Vision.Enabled=true;M.Vision.ModeEnableMask=(int)mask;
            Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos);
            Rig.Data(new ImageImuData{ImageId=Robot.StoredState!.Timestamp,RateX=0,RateY=0,RateZ=0,Line2Number=0});Rig.Tick();
            M.Vision.HandOverFrame(new CameraFrame{ImageId=id,Timestamp=Robot.StoredState!.Timestamp,Width=320,Height=240,Encoding=1,RawPayload=new byte[320*240]});
            M.Vision.WaitForProcessorIdle();Rig.Engine.VisionUpdateAllResultsFailed!();Rig.Tick();
        }
        internal ObservableObject Carry()
        {
            var obj=new ObservableObject(7,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {Pose=new Pose3d(Rotation3d.Identity,new Vec3(0,0,22),parent:Robot.LiftPose),OriginId=Robot.CurrentOriginId,PoseState=PoseState.Known,NativeCarryingTree=true};
            M.World.AddLocatedObject(obj);using var relative=new Pose3d("");M.Docking.Carrying.AttachToLift(7,obj.Markers[0],relative,null,null);return obj;
        }
        internal FaceEntry Face(int id,uint time,Vec3 position,bool named=false)
        {
            var tracked=new TrackedFace(new DetectedFace(id,new FaceRect(0,0,10,10),Name:named?"Ada":null),time)
            {HeadPose=new Pose3d(Rotation3d.Identity,position,parent:Robot.GetNativeOrigin(Robot.CurrentOriginId))};
            return M.Vision.Faces.AddOrUpdateFace(tracked,Robot.GetCheckedMainPose(),false)!.Face;
        }
        internal void Motors()
        {
            float head=Rig.Robot.Motion.HeadAngleRad,lift=0,angle=0;
            foreach(var message in Rig.SentSince(0))switch(message)
            {
                case SetHeadAngle h:head=h.AngleRad;Rig.Data(new MotorActionAck{ActionId=h.ActionId});break;
                case SetBodyAngle b:angle=b.AngleRad;Rig.Data(new MotorActionAck{ActionId=b.ActionId});break;
                case SetLiftHeight l:lift=(float)Math.Asin((l.HeightMm-45f)/66f);Rig.Data(new MotorActionAck{ActionId=l.ActionId});break;
            }
            var state=Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,head:head,liftAngle:lift);state.Pose.Angle=angle;Rig.Data(state);Rig.Tick();
        }
        public void Dispose()=>Rig.Dispose();
    }
    [Fact]
    public void ConfiguredFactoryUsesOneSignedContextDrawAndRealOwnedSequence()
    {
        using var f=new Fixture();f.Carry();
        var ids=new[]{"PutDownBlock","PutDownBlockNothingToDo","SparksPutDownBlock","PyramidPutDownBlock"};
        var instances=ids.Select(id=>Assert.IsType<NativePutDownBlockBehavior>(f.Runtime.Bound[id])).ToArray();
        Assert.Equal(ids,instances.Select(b=>b.Id));Assert.Equal(4,instances.Distinct().Count());
        Assert.True(f.Behavior.IsRunnable(f.Runtime.Context));
        f.Rig.Engine.ContextRandom.SetSeed(123,()=>123);var reference=new Cozmo.Robot.Animation.EngineRandom();reference.SetSeed(123,()=>123);
        float expected=(float)((-75.0-(-45.0))*reference.GetNextDbl()+(-45.0));
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(f.Behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var sequence=Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        var drive=Assert.IsType<DriveStraightAction>(sequence.Children[0]);
        Assert.Equal(expected,f.Behavior.BackupDistance);Assert.Equal(expected,drive.DistanceMm);Assert.Equal(100f,drive.SpeedMmps);
        Assert.Equal(reference.GetNextDbl(),f.Rig.Engine.ContextRandom.GetNextDbl());
        Assert.Equal((byte)26,sequence.Children[1].Type);
        f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
    }
    // Supplied LOCAL5A callback unit, distinct from complete real ActionList execution below.
    [Fact]
    public void AnyInitialCallbackFailureStillBuildsLookAndFinalFailureReleasesCurrentCarry()
    {
        using var f=new Fixture();f.Carry();
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(f.Behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        uint initial=f.Behavior.CurrentRunnerTag;
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(initial,0,0x03000018,Array.Empty<uint>(),0));
        f.Behavior.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        var look=Assert.IsAssignableFrom<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());
        var parallel=Assert.IsType<CompoundActionParallel>(look.Children[0]);
        var backup=Assert.IsType<DriveStraightAction>(parallel.Children[1]);Assert.Equal(-30,backup.DistanceMm);Assert.Equal(-80f,backup.SpeedMmps);
        Assert.IsType<NativeWaitForImagesRunner>(look.Children[1]);
        var wrapper=Assert.IsType<NativeFaceBeforeAnimationRunner>(look.Children[2]);Assert.IsType<NativeLastFaceRunner>(wrapper.Children[0]);Assert.Equal(26,wrapper.Type);
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(f.Behavior.CurrentRunnerTag,0,0x03000018,Array.Empty<uint>(),0));f.Behavior.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        Assert.False(f.M.Docking.Carrying.IsCarryingObject);Assert.Equal(PoseState.Dirty,f.M.World.GetLocatedObjectByIdHelper(7)!.PoseState);
    }
    // Supplied LOCAL5A callback unit.
    [Fact]
    public void NoCarryLookSkipsBothPositioningAndWaitAndStopSuppressesStaleRelease()
    {
        using var f=new Fixture();f.Carry();Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(f.Behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        f.M.Docking.ReleaseCarriedObject();
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(f.Behavior.CurrentRunnerTag,0,0,Array.Empty<uint>(),0));f.Behavior.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);
        var look=Assert.IsAssignableFrom<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());Assert.Single(look.Children);Assert.IsType<NativeFaceBeforeAnimationRunner>(look.Children[0]);
        uint stale=f.Behavior.CurrentRunnerTag;f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        f.Carry();f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(stale,0,0,Array.Empty<uint>(),0));f.Behavior.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF);Assert.True(f.M.Docking.Carrying.IsCarryingObject);
    }
    [Fact]
    public void NativeCacheHasFourthObservationGateIndependentLifetimeAndOriginFallback()
    {
        using var f=new Fixture();using var output=new Pose3d("");var origin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        for(uint time=1;time<=3;time++)f.Face(9,time,new Vec3(100,0,50));
        Assert.False(f.M.Vision.Faces.CopyNativeLastFace(origin,false,output));
        f.Face(9,4,new Vec3(100,0,50));f.M.Vision.Faces.RemoveFaceByID(9);
        Assert.True(f.M.Vision.Faces.CopyNativeLastFace(origin,false,output));Assert.Equal(100,output.Translation.X);
        using var other=new Pose3d("other-origin");Assert.False(f.M.Vision.Faces.CopyNativeLastFace(other,true,output));
        Assert.True(f.M.Vision.Faces.CopyNativeLastFace(other,false,output));Assert.Same(other.Node,output.Node!.Parent);
        f.Face(10,2,new Vec3(200,0,60),true);Assert.True(f.M.Vision.Faces.CopyNativeLastFace(origin,false,output));Assert.Equal(200,output.Translation.X); //older-but-different replaces.
    }
    [Fact]
    public void SameTimestampUsesDistanceFromActualNonzeroMainNotLocalOrigin()
    {
        using var f=new Fixture();f.Rig.State(x:100);using var output=new Pose3d("");var origin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        f.Face(1,10,new Vec3(110,0,50),true);f.Face(2,10,new Vec3(20,0,50),true);
        Assert.True(f.M.Vision.Faces.CopyNativeLastFace(origin,false,output));Assert.Equal(110,output.Translation.X);
        f.Face(3,10,new Vec3(double.NaN,0,50),true);Assert.True(f.M.Vision.Faces.CopyNativeLastFace(origin,false,output));Assert.True(double.IsNaN(output.Translation.X));
    }
    [Fact]
    public void QueuedLastFaceHasOneCoreTagAndRealWaitPrioritySignedFaceAndFineTune()
    {
        using var f=new Fixture();f.Face(1,1,new Vec3(100,0,60),true);
        var action=new NativeLastFaceRunner(f.M);uint tag=action.Tag;Assert.Equal(0x29,action.Type);Assert.Equal(0u,action.RequiredTrackMask);
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,action,0);
        for(int i=0;i<15&&action.FaceState==0;i++)f.Motors();
        Assert.Equal(1,action.FaceState);f.Rig.Tick();
        f.Face(-1,2,new Vec3(80,0,60),true);f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(-1,2));
        f.Rig.Tick();Assert.Equal(-1,action.VerifiedId);Assert.Equal(2,action.FaceState);
        for(int i=0;i<15&&!action.DestructionObserved;i++)f.Motors();
        Assert.True(action.DestructionObserved);Assert.Equal(0u,action.State);Assert.True(f.M.Vision.Faces.HasTurnedTowardsFace(-1));Assert.False(ActionRunnerTagCounter.Global.IsInUse(tag));
    }
    [Fact]
    public void ConfiguredPutDownRunsThroughRealPathAnimationAndImageCompletionBeforeCarryRelease()
    {
        // Real configured instance→manager Start→ActionList→wire/C6/CA/CB→43→release.
        // Firmware full states/acks/images are admitted inputs; no hardware-motion claim.
        using var f=new Fixture();f.InstallNvCalibration();f.Rig.Robot.Animations.LoadFrom(Resources());f.Carry();
        var completions=new List<RobotCompletedAction>();using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction done)completions.Add(done);});
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(f.Behavior.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var initial=Assert.IsType<CompoundActionSequential>(f.Robot.ActionList.GetCurrentAction());uint firstTag=initial.Tag;
        var paths=new HashSet<ushort>();var animations=new HashSet<byte>();int image=0;
        try
        {
            for(int tick=0;tick<100&&(f.Behavior.HasCurrentAction||f.M.Docking.Carrying.IsCarryingObject);tick++)
            {
                f.Motors();
                foreach(var path in f.Rig.SentSince(0).OfType<ExecutePath>())if(paths.Add(path.EventId))
                {f.Rig.Data(new PathFollowingEvent{EventId=path.EventId,EventType=0});f.Rig.Tick();f.Rig.Data(new PathFollowingEvent{EventId=path.EventId,EventType=2});f.Rig.Tick();}
                byte tag=f.Rig.Robot.Animations.CurrentTag;
                if(tag!=0&&animations.Add(tag)){f.Rig.Data(new AnimationStarted{AnimId=tag});f.Rig.Data(new AnimationEnded{AnimId=tag});f.Rig.Tick();}
                if(f.Robot.ActionList.GetCurrentAction() is CompoundActionSequential look&&look.Tag!=firstTag)
                {
                    var wait=look.Children.OfType<NativeWaitForImagesRunner>().FirstOrDefault();
                    if(wait?.Subscribed==true)f.Image((uint)++image,1<<1);
                }
            }
            Assert.False(f.Behavior.HasCurrentAction);Assert.False(f.M.Docking.Carrying.IsCarryingObject);
            Assert.Equal(2,paths.Count);Assert.True(animations.Count>=2);Assert.Equal(2,image);
            Assert.Contains(completions,c=>c.Tag==firstTag&&c.Result==0);
            Assert.Equal(0u,completions.Last(c=>c.Tag!=firstTag).Result);
            Assert.Equal(PoseState.Dirty,f.M.World.GetLocatedObjectByIdHelper(7)!.PoseState);
        }
        finally{f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);f.Robot.ActionList.Clear();}
    }
    private sealed class Detector:IFaceDetector
    {
        public bool IsAvailable=>true;public string Description=>"explicit detector input at proprietary boundary";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image,uint timestamp)=>new[]{new DetectedFace(11,new FaceRect(120,40,60,60),new Vec2(135,60),new Vec2(165,60),RollRad:0)};
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void ActualVisionDrainUpdatesCacheBeforeCountGatedLocal46(bool external)
    {
        using var f=new Fixture();f.InstallNvCalibration();f.Robot.HasExternalInterface=external;
        var faceLogs=new List<string>();f.M.Vision.Faces.Log+=faceLogs.Add;f.M.Vision.FaceDetector=new Detector();var reports=new List<NativeObservedFace>();var hostObservations=new List<FaceObservation>();bool cacheReadyAtReport=false;
        f.M.Vision.Faces.FaceObserved+=hostObservations.Add;using var output=new Pose3d("");
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x46,true,p=>
        {
            if(p is not NativeObservedFace report)return;
            cacheReadyAtReport=f.M.Vision.Faces.CopyNativeLastFace(f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId),false,output);
            reports.Add(report);
        });
        for(uint i=1;i<=4;i++){f.Image(i,1<<2);Assert.True(hostObservations.Count==(int)i,"observations="+hostObservations.Count+" logs="+string.Join(" | ",faceLogs));Assert.Equal(external&&i==4?1:0,reports.Count);}
        Assert.Equal(4,hostObservations.Last().Face.TimesObserved);
        Assert.True(f.M.Vision.Faces.CopyNativeLastFace(f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId),false,output));
        if(external){Assert.True(cacheReadyAtReport);Assert.Equal(11,Assert.Single(reports).Id);Assert.Equal(hostObservations.Last().Timestamp,reports[0].Timestamp);}
    }

    [Fact]
    public void MissingPoseReinitRetainsTheRealWaitAndUsesStateThreeNeeds29OnlyAfterSuccess()
    {
        using var f=new Fixture();f.Face(1,1,new Vec3(100,0,60),true);var needs=new List<string>();
        f.Rig.Engine.Needs.Log+=needs.Add;
        var action=new NativeLastFaceRunner(f.M);f.Robot.ActionList.QueueAction(QueueActionPosition.Now,action,0);
        try
        {
            for(int i=0;i<15&&action.FaceState==0;i++)f.Motors();Assert.Equal(1,action.FaceState);f.Rig.Tick();
            f.M.Vision.Faces.ResetToConstructed();Assert.Equal(0,action.Init());Assert.Equal(3,action.FaceState);
            Assert.Equal(EngineActionResult.Running,action.CheckIfDone());Assert.DoesNotContain(needs,m=>m.Contains("SayName"));
            for(int i=0;i<10;i++)f.Rig.Engine.AnimationMessages.Deliver(0x43,true,new ProcessedImageReport(9,new uint[]{2},0));
            Assert.Equal(0u,action.CheckIfDone());Assert.Single(needs.Where(m=>m.StartsWith("needs.action_completed SayName:")));
        }
        finally{f.Robot.ActionList.Clear();f.Rig.Engine.Needs.Log-=needs.Add;}
        Assert.True(action.DestructionObserved);
    }

    [Fact]
    public void SameStampRoundedSqrtTieRetainsCachedPoseEvenWhenSquaredDistanceIsLarger()
    {
        using var f=new Fixture();using var output=new Pose3d("");var origin=f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        // f32 squared norms differ by one ULP; both sqrt instructions round to111.80340576171875 (including Z50).
        const float cachedY=.044194173f,candidateY=.03125f;
        f.Face(1,7,new Vec3(100,cachedY,50),true);f.Face(2,7,new Vec3(100,candidateY,50),true);
        Assert.True(f.M.Vision.Faces.CopyNativeLastFace(origin,false,output));Assert.Equal(cachedY,(float)output.Translation.Y);
    }

    [Fact]
    public void ReinitAppends46HandlesSoEarliestRecipientStillPrecedesInterveningObserver()
    {
        // Direct native Init/ReInit lifecycle unit; real retained LOCAL46 hub and real face entries.
        using var f=new Fixture();f.Face(1,1,new Vec3(100,0,60),true);f.Face(-1,2,new Vec3(80,0,60),true);
        var action=new NativeLastFaceRunner(f.M);int seen=0;
        try
        {
            Assert.Equal(0,action.Init());
            using(var intervening=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x46,true,_=>seen=action.VerifiedId))
            {
                Assert.Equal(0,action.Init());
                f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(-1,2));
                Assert.Equal(-1,seen);Assert.Equal(-1,action.VerifiedId);
            }
        }
        finally{action.Prep();action.WatcherEnding();}
        f.Face(3,3,new Vec3(1,0,1),true);f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(3,3));
        Assert.Equal(0,action.VerifiedId);Assert.True(action.DestructionObserved); //all appended subscriptions and native Impl retired.
    }

    [Fact]
    public void NativeDeletionPublishesBeforeEraseAndResetsVerifiedSoTheRealWaitStillRuns()
    {
        using var f=new Fixture();f.Face(1,1,new Vec3(100,0,60),true);
        var action=new NativeLastFaceRunner(f.M);var needs=new List<string>();f.Rig.Engine.Needs.Log+=needs.Add;
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,action,0);
        try
        {
            f.Rig.Tick();Assert.Equal(0,action.FaceState);
            f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(1,1));Assert.Equal(1,action.VerifiedId);
            bool present=false;int observedId=99;
            using(var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4B,true,_=>{present=f.M.Vision.Faces.GetFace(1) is not null;observedId=action.VerifiedId;}))f.M.Vision.Faces.RemoveFaceByID(1);
            Assert.True(present);Assert.Equal(0,observedId);Assert.Equal(0,action.VerifiedId);Assert.Null(f.M.Vision.Faces.GetFace(1));
            for(int tick=0;tick<15&&action.FaceState==0;tick++)f.Motors();
            Assert.Equal(1,action.FaceState);Assert.Equal(EngineActionResult.Running,action.State);Assert.False(action.DestructionObserved);
            Assert.DoesNotContain(needs,m=>m.StartsWith("needs.action_completed SeeFace:"));
        }
        finally{f.Robot.ActionList.Clear();f.Rig.Engine.Needs.Log-=needs.Add;}
    }
    [Fact]
    public void NativeChangedAbsentZeroRetargetsRequestedAndUniqueRenameResetsOnlyNewEntries()
    {
        using var f=new Fixture();var requested=f.Face(7,1,new Vec3(100,0,60),true);f.Face(9,2,new Vec3(500,0,60),true);
        var action=new NativeLastFaceRunner(f.M);int publicChanges=0;f.M.Vision.Faces.FaceIdChanged+=(_,_)=>publicChanges++;
        try
        {
            Assert.False(f.M.Vision.Faces.ChangeFaceID(0,7));Assert.Equal(0,publicChanges);Assert.Equal(7,action.RequestedId);
            Assert.Equal(0,action.Init());Assert.Equal(100,action.GoalPose.Translation.X); //requested lookup, not latest cached500.
            f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(9,2));Assert.Equal(0,action.VerifiedId);
            f.Rig.Engine.AnimationMessages.Deliver(0x46,true,new NativeObservedFace(7,1));Assert.Equal(7,action.VerifiedId);
            Assert.True(f.M.Vision.Faces.ChangeFaceID(7,10,"Renamed"));var renamed=f.M.Vision.Faces.GetFace(10)!;
            Assert.Equal(10,action.RequestedId);Assert.Equal(10,action.VerifiedId);Assert.Equal(0,renamed.TimesObserved);Assert.False(renamed.TurnedTowards);
            Assert.Equal(10,requested.Id);Assert.Equal("Renamed",requested.Name);Assert.Equal("Renamed",renamed.Name);Assert.Equal(requested.HeadPose.Translation,renamed.HeadPose.Translation);Assert.NotSame(requested.HeadPose.Node,renamed.HeadPose.Node);
            var target=f.Face(20,3,new Vec3(200,0,60),true);f.M.Vision.Faces.SetTurnedTowardsFace(20,true);
            Assert.True(f.M.Vision.Faces.ChangeFaceID(10,20,"collision"));Assert.Same(target,f.M.Vision.Faces.GetFace(20));Assert.Equal(1,target.TimesObserved);Assert.True(target.TurnedTowards);Assert.Equal(200,target.HeadPose.Translation.X);
            Assert.True(f.M.Vision.Faces.ChangeFaceID(20,20));Assert.Null(f.M.Vision.Faces.GetFace(20)); //unique-insert failure then remove(false), no selfguard.
        }
        finally{action.Prep();action.WatcherEnding();}
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void NativeLifecycleGuardAndCopySubscriptionLifetimeStaySeparateFromPublicSentinel(bool external)
    {
        using var f=new Fixture();f.Robot.HasExternalInterface=external;f.Face(7,1,new Vec3(100,0,60),true);
        using var requested=new NativeSmartFaceReference(f.Rig.Robot,7);using var verified=new NativeSmartFaceReference(f.Rig.Robot);
        Assert.Equal(external,requested.HasImpl);Assert.False(verified.HasImpl);Assert.Equal(0,verified.Id);
        int changed=0,deleted=0;using var c=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x47,true,_=>changed++);using var d=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x4B,true,_=>deleted++);
        using var publicId=f.M.Vision.Faces.GetSmartFaceID(-1);Assert.False(publicId.IsValid);Assert.Equal(-1,publicId.Id);
        f.M.Vision.Faces.ChangeFaceID(7,8);Assert.Equal(external?8:0,requested.Id);Assert.Equal(external?1:0,changed);
        f.M.Vision.Faces.RemoveFaceByID(8);Assert.Equal(0,requested.Id);Assert.Equal(external?1:0,deleted);
        f.M.Vision.Faces.RemoveFaceByID(8);Assert.Equal(external?1:0,deleted); //missing delete emits nothing.
    }
    [Fact]
    public void NativeCopyAlwaysCreatesNewSubscriptionsBeforeRetiringPreviousImpl()
    {
        using var f=new Fixture();f.Face(7,1,new Vec3(100,0,60),true);
        using var requested=new NativeSmartFaceReference(f.Rig.Robot,7);using var verified=new NativeSmartFaceReference(f.Rig.Robot);
        verified.CopyFrom(requested);int seen=0;
        using var middle=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x47,true,_=>seen=verified.Id);
        verified.CopyFrom(requested);f.M.Vision.Faces.ChangeFaceID(7,8);
        Assert.Equal(7,seen);Assert.Equal(8,verified.Id);Assert.Equal(8,requested.Id);
        verified.Reset();Assert.True(verified.HasImpl);Assert.Equal(0,verified.Id);
        f.M.Vision.Faces.ChangeFaceID(0,-1);Assert.Equal(-1,verified.Id);Assert.True(verified.IsValid);
    }

    [Fact]
    public void NativeLazyUpdateUsesCurrentGateButExistingAndCopyKeepTheirCapturedBus()
    {
        // Native reference unit with supplied local messages; tests actual receiver lifetime, not app publication.
        using var f=new Fixture();using var value=new NativeSmartFaceReference(f.Rig.Robot);using var copy=new NativeSmartFaceReference(f.Rig.Robot);
        f.Robot.HasExternalInterface=false;value.UpdateToId(7);Assert.False(value.HasImpl);Assert.Equal(0,value.Id);
        f.Robot.HasExternalInterface=true;value.UpdateToId(-1);Assert.True(value.HasImpl);Assert.True(value.IsValid);
        f.Robot.HasExternalInterface=false;value.UpdateToId(7);copy.CopyFrom(value);Assert.Equal(7,copy.Id);
        f.Rig.Engine.AnimationMessages.Deliver(0x47,true,new NativeChangedFace(7,8));Assert.Equal(8,value.Id);Assert.Equal(8,copy.Id);
        value.Reset();Assert.True(value.HasImpl);f.Rig.Engine.AnimationMessages.Deliver(0x47,true,new NativeChangedFace(0,9));Assert.Equal(9,value.Id);
        value.Dispose();f.Rig.Engine.AnimationMessages.Deliver(0x47,true,new NativeChangedFace(9,10));Assert.False(value.HasImpl);Assert.Equal(0,value.Id);
    }

}
