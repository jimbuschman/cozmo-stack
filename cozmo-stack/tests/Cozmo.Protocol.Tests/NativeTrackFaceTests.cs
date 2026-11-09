using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Animation;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeTrackFaceTests
{
    private sealed class ImmediateAction : ActionRunner
    {
        internal ImmediateAction():base(8,0) { }
        protected override string ActionName=>"front fixture";
        public override bool CanInterrupt()=>false;
        public override uint CheckIfDone()=>EngineActionResult.Success;
    }

    [Fact]
    public void RetiredCompanionBeforeInitFailsAndRestoresIdleWithoutSubscription()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;var p=f.Rig.Robot.Animations.Scheduler.LiveIdleParameters;
        p.SetParam(LiveIdleParam.EyeDartMaxDistance_pix,7f);
        uint tag=ActionRunnerTagCounter.Global.NextIdTag();
        Assert.True(ActionRunnerTagCounter.Global.TryReserve(tag));ActionRunnerTagCounter.Global.Release(tag);
        int subscriptions=f.Rig.Engine.AnimationMessages.Count(0x47,true);
        var ended=new List<RobotCompletedAction>();owner.ActionList.RegisterActionEndedCallback(ended.Add);
        var runner=new NativeTrackFaceRunner(f.M,owner,-7,true,BitConverter.UInt32BitsToSingle(0x3D8EFA35),false,-1,-1,tag);
        int mark=f.Rig.Mark();owner.ActionList.QueueAction(QueueActionPosition.Now,runner);f.Rig.Tick();
        Assert.Equal(0x03000000u,Assert.Single(ended.Where(x=>x.Tag==runner.Tag)).Result);
        Assert.Equal(subscriptions,f.Rig.Engine.AnimationMessages.Count(0x47,true));
        Assert.Equal(7f,p[LiveIdleParam.EyeDartMaxDistance_pix]);
        Assert.Equal(0,owner.NativeTrackingFaceId);
        Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is SetHeadAngle or SetBodyAngle);
    }

    [Fact]
    public void MissingInterfaceInitSetsErrorAndNeverInstallsSubscription()
    {
        bool previous=EngineErrorState.ErrorFlagSet;
        try
        {
            using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
            var owner=f.Robot;owner.HasExternalInterface=false;EngineErrorState.ErrorFlagSet=false;
            int count=f.Rig.Engine.AnimationMessages.Count(0x47,true);
            var ended=new List<RobotCompletedAction>();owner.ActionList.RegisterActionEndedCallback(ended.Add);
            var runner=new NativeTrackFaceRunner(f.M,owner,-7);
            owner.ActionList.QueueAction(QueueActionPosition.Now,runner);f.Rig.Tick();
            Assert.Equal(0x03000000u,Assert.Single(ended.Where(x=>x.Tag==runner.Tag)).Result);
            Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.Equal(count,f.Rig.Engine.AnimationMessages.Count(0x47,true));
            Assert.Equal(0,owner.NativeTrackingFaceId);
        }
        finally {EngineErrorState.ErrorFlagSet=previous;}
    }

    [Fact]
    public void FrontInterruptReinitializesAndReplacesFaceSubscription()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;int baseline=f.Rig.Engine.AnimationMessages.Count(0x47,true);
        var track=new NativeTrackFaceRunner(f.M,owner,-7);
        owner.ActionList.QueueAction(QueueActionPosition.Now,track);f.Rig.Tick();
        Assert.Equal(baseline+1,f.Rig.Engine.AnimationMessages.Count(0x47,true));
        owner.ActionList.QueueAction(QueueActionPosition.NowAndResume,new ImmediateAction());
        for(int i=0;i<10 && !ReferenceEquals(owner.ActionList.GetCurrentAction(),track);i++)f.Rig.Tick();
        Assert.Same(track,owner.ActionList.GetCurrentAction());
        f.Rig.Tick();
        Assert.Equal(baseline+1,f.Rig.Engine.AnimationMessages.Count(0x47,true));
        f.Rig.Engine.AnimationMessages.Deliver(0x47,true,new NativeChangedFace(-7,-8));
        Assert.Equal(unchecked((uint)-8),track.GetCompletionUnion());
        Assert.Equal(-7,owner.NativeTrackingFaceId);
        owner.ActionList.Cancel(track.Tag);
        Assert.Equal(baseline,f.Rig.Engine.AnimationMessages.Count(0x47,true));
    }

    [Fact]
    public void CancellationDuringReinitRetiresBothFaceSubscriptions()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;var hub=f.Rig.Engine.AnimationMessages;
        int baseline=hub.Count(0x47,true);
        var runner=new NativeTrackFaceRunner(f.M,owner,-7);
        owner.ActionList.QueueAction(QueueActionPosition.Now,runner);f.Rig.Tick();
        bool cancelled=false;
        hub.ReleaseObserved=message=>
        {
            if(!cancelled && message=="unlink E2G 47")
            {cancelled=true;owner.ActionList.Cancel(runner.Tag);}
        };
        try
        {
            owner.ActionList.QueueAction(QueueActionPosition.NowAndResume,new ImmediateAction());
            for(int i=0;i<10 && !cancelled;i++)f.Rig.Tick();
            Assert.True(cancelled);
            Assert.False(owner.ActionList.ContainsRunner(runner));
            Assert.Equal(baseline,hub.Count(0x47,true));
            Assert.Equal(0,owner.NativeTrackingFaceId);
        }
        finally {hub.ReleaseObserved=null;}
    }

    [Fact]
    public void SuccessorStartedDuringSubscriptionReleaseKeepsItsTrackingState()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;var hub=f.Rig.Engine.AnimationMessages;
        var old=new NativeTrackFaceRunner(f.M,owner,-7);
        owner.ActionList.QueueAction(QueueActionPosition.Now,old);f.Rig.Tick();
        NativeTrackFaceRunner? successor=null;int mark=-1;
        hub.ReleaseObserved=message=>
        {
            if(successor is not null || message!="unlink E2G 47")return;
            successor=new NativeTrackFaceRunner(f.M,owner,-8){SuppressTrackLocking=true};
            owner.ActionList.QueueAction(QueueActionPosition.InParallel,successor);
            owner.ActionList.Update();
            mark=f.Rig.Mark();
        };
        try
        {
            owner.ActionList.Cancel(old.Tag);
            Assert.NotNull(successor);
            Assert.True(owner.ActionList.ContainsRunner(successor));
            Assert.Equal(-8,owner.NativeTrackingFaceId);
            Assert.False(f.Rig.Robot.Motion.AreAnyTracksLocked(5));
            Assert.Equal(1f,f.Rig.Robot.Animations.Scheduler.LiveIdleParameters[LiveIdleParam.EyeDartMaxDistance_pix]);
            Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is MoveHead or DriveWheels);
        }
        finally {hub.ReleaseObserved=null;if(successor is not null)owner.ActionList.Cancel(successor.Tag);}
    }

    [Fact]
    public void SuccessorStartedDuringStopSendDoesNotReceiveOldMotorStop()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;
        var old=new NativeTrackFaceRunner(f.M,owner,-7);
        owner.ActionList.QueueAction(QueueActionPosition.Now,old);f.Rig.Tick();
        NativeTrackFaceRunner? successor=null;int mark=-1;
        owner.SendFault=message=>
        {
            if(successor is null && message is DriveWheels)
            {
                successor=new NativeTrackFaceRunner(f.M,owner,-8){SuppressTrackLocking=true};
                owner.ActionList.QueueAction(QueueActionPosition.InParallel,successor);
                owner.ActionList.Update();mark=f.Rig.Mark();
            }
            return null;
        };
        try
        {
            owner.ActionList.Cancel(old.Tag);
            Assert.NotNull(successor);
            Assert.Equal(-8,owner.NativeTrackingFaceId);
            Assert.False(f.Rig.Robot.Motion.AreAnyTracksLocked(5));
            Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is MoveHead or DriveWheels);
        }
        finally {owner.SendFault=null;if(successor is not null)owner.ActionList.Cancel(successor.Tag);}
    }

    [Fact]
    public void LostCompanionAfterInitCompletesSuccessfully()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        uint tag=ActionRunnerTagCounter.Global.NextIdTag();Assert.True(ActionRunnerTagCounter.Global.TryReserve(tag));
        try
        {
            var ended=new List<RobotCompletedAction>();f.Robot.ActionList.RegisterActionEndedCallback(ended.Add);
            var runner=new NativeTrackFaceRunner(f.M,f.Robot,-7,true,BitConverter.UInt32BitsToSingle(0x3D8EFA35),false,-1,-1,tag);
            f.Robot.ActionList.QueueAction(QueueActionPosition.Now,runner);f.Rig.Tick();
            Assert.True(f.Robot.ActionList.ContainsRunner(runner));
            ActionRunnerTagCounter.Global.Release(tag);f.Rig.Tick();
            Assert.Equal(0u,Assert.Single(ended.Where(x=>x.Tag==runner.Tag)).Result);
        }
        finally {ActionRunnerTagCounter.Global.Release(tag);}
    }

    [Fact]
    public void UnrelatedActualMainRootSetsErrorAfterConsumingFreshFace()
    {
        bool previous=EngineErrorState.ErrorFlagSet;
        try
        {
            using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
            var owner=f.Robot;
            var face=new TrackedFace(new DetectedFace(-9,new FaceRect(0,0,10,10)),10)
            {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(200,100,100)),NativeTrackingTranslation=new Vec3(200,100,100)};
            f.M.Vision.Faces.AddOrUpdateNativeFace(face,Pose3d.Identity,false,owner.GetNativeOrigin(owner.CurrentOriginId).Node!);
            var oldParent=owner.MainPose.Node!.Parent;
            owner.MainPose.ClearParent();
            try
            {
                var logs=new List<EngineLogEntry>();f.Rig.Engine.LogEntry+=logs.Add;
                var runner=new NativeTrackFaceRunner(f.M,owner,-9);
                owner.ActionList.QueueAction(QueueActionPosition.Now,runner);
                EngineErrorState.ErrorFlagSet=false;int mark=f.Rig.Mark();f.Rig.Tick();
                Assert.True(EngineErrorState.ErrorFlagSet);
                Assert.Contains(logs,e=>e.Name=="TrackFaceAction.UpdateTracking.PoseOriginError");
                Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is SetHeadAngle or SetBodyAngle);
                Assert.True(owner.ActionList.ContainsRunner(runner));
                owner.ActionList.Cancel(runner.Tag);
            }
            finally {owner.MainPose.Node!.SetParent(oldParent);}
        }
        finally {EngineErrorState.ErrorFlagSet=previous;}
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NativeHeadOrBodySendFailureEndsWithMotorFailure(bool failHead)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;
        var face=new TrackedFace(new DetectedFace(-9,new FaceRect(0,0,10,10)),10)
        {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(200,100,100)),NativeTrackingTranslation=new Vec3(200,100,100)};
        f.M.Vision.Faces.AddOrUpdateNativeFace(face,Pose3d.Identity,false,owner.GetNativeOrigin(owner.CurrentOriginId).Node!);
        int head=0,body=0;
        owner.SendFault=message=>
        {
            if(message is SetHeadAngle){head++;if(failHead)return false;}
            if(message is SetBodyAngle){body++;if(!failHead)return false;}
            return null;
        };
        try
        {
            var ended=new List<RobotCompletedAction>();owner.ActionList.RegisterActionEndedCallback(ended.Add);
            var runner=new NativeTrackFaceRunner(f.M,owner,-9);
            owner.ActionList.QueueAction(QueueActionPosition.Now,runner);f.Rig.Tick();
            Assert.Equal(0x03000016u,Assert.Single(ended.Where(x=>x.Tag==runner.Tag)).Result);
            Assert.Equal(1,head);Assert.Equal(failHead?0:1,body);
        }
        finally {owner.SendFault=null;}
    }

    [Fact]
    public void OffTreadsBodyIsRefusedAfterEligibleHeadCommand()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;
        var face=new TrackedFace(new DetectedFace(-9,new FaceRect(0,0,10,10)),10)
        {HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(200,100,100)),NativeTrackingTranslation=new Vec3(200,100,100)};
        f.Rig.State(RobotStatusFlag.IsPickedUp | RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos);
        Assert.NotEqual(OffTreadsState.OnTreads,f.Rig.Robot.Sensors.OffTreadsState);
        f.M.Vision.Faces.AddOrUpdateNativeFace(face,Pose3d.Identity,false,owner.GetNativeOrigin(owner.CurrentOriginId).Node!);
        var ended=new List<RobotCompletedAction>();owner.ActionList.RegisterActionEndedCallback(ended.Add);
        var runner=new NativeTrackFaceRunner(f.M,owner,-9);
        int mark=f.Rig.Mark();owner.ActionList.QueueAction(QueueActionPosition.Now,runner);
        owner.ActionList.Update();
        Assert.Equal(0x0300000Au,Assert.Single(ended.Where(x=>x.Tag==runner.Tag)).Result);
        Assert.Single(f.Rig.SentSince(mark).OfType<SetHeadAngle>());
        Assert.Empty(f.Rig.SentSince(mark).OfType<SetBodyAngle>());
    }
    [Fact]
    public void CancelPendingManualTrackerKeepsTheActiveTrackerIdentity()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot;
        var active = new NativeTrackFaceRunner(f.M, owner, -5);
        owner.ActionList.QueueAction(QueueActionPosition.Now, active);
        f.Rig.Tick();
        Assert.Equal(-5, owner.NativeTrackingFaceId);
        var pending = new NativeTrackFaceRunner(f.M, owner, -6, true,
            BitConverter.UInt32BitsToSingle(0x3D8EFA35), false, -1, -1, 0, manualScope: true);
        owner.ActionList.QueueAction(QueueActionPosition.AtEnd, pending);
        owner.ActionList.Cancel(pending.Tag);
        Assert.True(owner.ActionList.ContainsRunner(active));
        Assert.Equal(-5, owner.NativeTrackingFaceId);
        owner.ActionList.Cancel(active.Tag);
    }

    [Fact]
    public void RejectedParallelManualTrackerDoesNotStopTheActiveTracker()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot;
        var active = new NativeTrackFaceRunner(f.M, owner, -5);
        owner.ActionList.QueueAction(QueueActionPosition.Now, active);
        f.Rig.Tick();
        Assert.Equal(-5, owner.NativeTrackingFaceId);
        var rejected = new NativeTrackFaceRunner(f.M, owner, -6, true,
            BitConverter.UInt32BitsToSingle(0x3D8EFA35), false, -1, -1, 0, manualScope: true);
        owner.ActionList.QueueAction(QueueActionPosition.InParallel, rejected);
        int mark = f.Rig.Mark();
        f.Rig.Tick();
        Assert.True(owner.ActionList.ContainsRunner(active));
        Assert.Equal(-5, owner.NativeTrackingFaceId);
        Assert.DoesNotContain(f.Rig.SentSince(mark), message => message is MoveHead or DriveWheels);
        owner.ActionList.Cancel(active.Tag);
    }

    [Fact]
    public void VerifiedTurnRejectsMissingInitialFaceWithoutMoving()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var ended=new List<RobotCompletedAction>();
        f.Robot.ActionList.RegisterActionEndedCallback(ended.Add);
        var turn=new NativeLastFaceRunner(f.M,f.Robot,99,true) { RequireVerifiedFace=true };
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,turn);
        int mark=f.Rig.Mark();
        f.Rig.Tick();
        Assert.Equal(0x0300000Eu,Assert.Single(ended.Where(x=>x.Tag==turn.Tag)).Result);
        Assert.DoesNotContain(f.Rig.SentSince(mark),message=>message is SetHeadAngle or SetBodyAngle);
    }

    [Fact]
    public void MeetHeadOnlyProfileClampsOnScheduleAndStopsWithItsDriveTag()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner=f.Robot;
        f.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos,head:0);
        var origin=owner.GetNativeOrigin(owner.CurrentOriginId);
        var face=new TrackedFace(new DetectedFace(-12,new FaceRect(0,0,10,10)),10)
        {
            HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(200,0,49)),
            NativeTrackingTranslation=new Vec3(200,0,49)
        };
        var entry=f.M.Vision.Faces.AddOrUpdateNativeFace(face,Pose3d.Identity,false,origin.Node!)!.Face;
        uint driveTag=ActionRunnerTagCounter.Global.NextIdTag();
        Assert.True(ActionRunnerTagCounter.Global.TryReserve(driveTag));
        try
        {
            float tolerance=BitConverter.UInt32BitsToSingle(0x3D8EFA35);
            var runner=new NativeTrackFaceRunner(f.M,owner,-12,true,tolerance,true,.5f,.5f,driveTag);
            Assert.Equal(1u,runner.RequiredTrackMask);
            owner.ActionList.QueueAction(QueueActionPosition.Now,runner);
            int mark=f.Rig.Mark();
            f.Rig.Tick();
            Assert.Equal(tolerance,Assert.Single(f.Rig.SentSince(mark).OfType<SetHeadAngle>()).AngleRad,5);
            Assert.Empty(f.Rig.SentSince(mark).OfType<SetBodyAngle>());
            mark=f.Rig.Mark();
            entry.LastObservedTimestamp++;
            f.Rig.Tick(100);
            Assert.Empty(f.Rig.SentSince(mark).OfType<SetHeadAngle>());
            entry.LastObservedTimestamp++;
            f.Rig.Tick(500);
            Assert.Single(f.Rig.SentSince(mark).OfType<SetHeadAngle>());
            ActionRunnerTagCounter.Global.Release(driveTag);
            mark=f.Rig.Mark();
            entry.LastObservedTimestamp++;
            f.Rig.Tick();
            Assert.Empty(f.Rig.SentSince(mark).OfType<SetHeadAngle>());
            Assert.Null(owner.ActionList.GetCurrentAction());
        }
        finally { ActionRunnerTagCounter.Global.Release(driveTag); }
    }

    [Fact]
    public void MeetProfileDoesNotSendToReplacementDuringHeadSend()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;
        var face=new TrackedFace(new DetectedFace(-13,new FaceRect(0,0,10,10)),10)
        {
            HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(200,100,100)),
            NativeTrackingTranslation=new Vec3(200,100,100)
        };
        f.M.Vision.Faces.AddOrUpdateNativeFace(face,Pose3d.Identity,false,owner.GetNativeOrigin(owner.CurrentOriginId).Node!);
        bool replaced=false;
        int mark=-1;
        owner.SendFault=message=>
        {
            if (!replaced && message is SetHeadAngle)
            {
                replaced=true;
                f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);
                f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
                mark=f.Rig.Mark();
            }
            return null;
        };
        try
        {
            var runner=new NativeTrackFaceRunner(f.M,owner,-13,false,BitConverter.UInt32BitsToSingle(0x3D8EFA35),true,.2f,.7f,0);
            owner.ActionList.QueueAction(QueueActionPosition.Now,runner);
            f.Rig.Tick();
            Assert.True(replaced);
            Assert.DoesNotContain(f.Rig.SentSince(mark),message=>message is SetHeadAngle or SetBodyAngle);
        }
        finally { owner.SendFault=null; }
    }

    [Fact]
    public void MeetCleanupDoesNotSendStopToReplacementDuringDirectDriveUnlock()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;
        var runner=new NativeTrackFaceRunner(f.M,owner,-13,true,BitConverter.UInt32BitsToSingle(0x3D8EFA35),true,.2f,.7f,0)
        { SuppressTrackLocking=true };
        owner.ActionList.QueueAction(QueueActionPosition.Now,runner);
        f.Rig.Tick();
        f.Rig.Robot.Motion.MoveHead(.2f,requireCalibration:false);
        bool replaced=false;int mark=-1;
        owner.SendFault=message=>
        {
            if(!replaced && message is EnableAnimTracks)
            {
                replaced=true;
                f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);
                f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
                mark=f.Rig.Mark();
            }
            return null;
        };
        try
        {
            owner.ActionList.Cancel(runner.Tag);
            Assert.True(replaced);
            Assert.DoesNotContain(f.Rig.SentSince(mark),message=>message is MoveHead or DriveWheels);
        }
        finally { owner.SendFault=null; }
    }

    [Fact]
    public void BodyDeltaUsesPoseUpdatedDuringHeadSend()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var owner = f.Robot;
        var face = new TrackedFace(new DetectedFace(-8, new FaceRect(0, 0, 10, 10)), 10)
        {
            HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(200, 100, 100)),
            NativeTrackingTranslation = new Vec3(200, 100, 100)
        };
        f.M.Vision.Faces.AddOrUpdateNativeFace(face, Pose3d.Identity, false, owner.GetNativeOrigin(owner.CurrentOriginId).Node!);
        bool changed = false;
        owner.SendFault = message =>
        {
            if (message is SetHeadAngle && !changed)
            {
                changed = true;
                lock (owner.PoseTreeGate)
                    owner.MainPose.Node!.Transform = new Transform3d(Rotation3d.FromAngleAxis(.2f, new Vec3(0, 0, 1)), owner.MainPose.Translation);
            }
            return null;
        };
        try
        {
            var runner = new NativeTrackFaceRunner(f.M, owner, -8);
            owner.ActionList.QueueAction(QueueActionPosition.Now, runner);
            int mark = f.Rig.Mark();
            f.Rig.Tick();
            Assert.True(changed);
            var body = Assert.Single(f.Rig.SentSince(mark).OfType<SetBodyAngle>());
            float originalTarget = MathF.Atan2(100f, 200f);
            Assert.Equal(originalTarget, body.AngleRad, 5);
            Assert.Equal(MathF.Abs(originalTarget - .2f) / .4f, body.MaxSpeedRadPerSec, 5);
            owner.ActionList.Cancel(runner.Tag);
            f.Rig.Tick();
        }
        finally { owner.SendFault = null; }
    }

    private sealed class OneFace : IFaceDetector
    {
        internal int Calls;
        internal double EyeX = 120, EyeY = 110;
        public bool IsAvailable => true;
        public string Description => "supplied face";
        public IReadOnlyList<DetectedFace> Detect(GrayImage image, uint timestamp)
        {
            Calls++;
            return new[] { new DetectedFace(-7, new FaceRect(100, 70, 90, 90), new Vec2(EyeX, EyeY), new Vec2(EyeX + 40, EyeY), RollRad: 0) { NativeNewEntryRotatingTooFast = false } };
        }
    }

    private sealed class UnrelatedAction : ActionRunner
    {
        internal UnrelatedAction() : base(8, 0) { }
        protected override string ActionName => "unrelated fixture";
        public override bool CanInterrupt() => false;
        public override uint CheckIfDone() => EngineActionResult.Running;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManualInteractUsesTheCapturedFaceOnTheOriginalActionList(bool replaceOwner)
    {
        using var signals = SignalTestContext.Install();
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        var vision = f.M.Vision;
        var detector = new OneFace();
        vision.FaceDetector = detector;
        f.Head = .1f;
        f.Motors();
        f.Image(500, modes: 1 << 2);
        Assert.NotNull(vision.Faces.GetNativeFace(f.Robot, -7)?.NativeTrackingTranslation);
        var context = new BehaviorContext { Robot = f.Rig.Robot, Triggers = new AnimationTriggerMap() };
        var behavior = new InteractWithFacesBehavior(vision, m: f.M) { TrackTimeScale = .1 };
        behavior.ActionTaskRunner = SignalTestContext.Schedule;
        behavior.StartAsync(context, new BehaviorScope(), default).GetAwaiter().GetResult();
        var owner = f.Robot;
        var unrelated = new UnrelatedAction();
        owner.ActionList.QueueAction(QueueActionPosition.InParallel, unrelated);
        var tags = new HashSet<byte>();
        int sentMark = f.Rig.Mark();
        ushort? pathId = null;
        bool started = false, completed = false, sawHeadOnly = false, sawBoth = false;
        bool driveHead = false, driveBody = false, timedHead = false, timedBody = false;
        var previousPhase = behavior.CurrentPhase;
        try
        {
            for (int i = 0; i < 800 && !(sawBoth && timedHead && timedBody); i++)
            {
                behavior.Update(context, f.Rig.Engine.Timer.TimeStampMs);
                SignalTestContext.Drain();
                f.Motors();
                foreach (var message in f.Rig.SentSince(sentMark))
                {
                    if (message is ExecutePath path) pathId = path.EventId;
                    if (previousPhase == InteractWithFacesBehavior.Phase.DrivingForward && behavior.CurrentPhase == previousPhase && sawHeadOnly)
                    { driveHead |= message is SetHeadAngle; driveBody |= message is SetBodyAngle; }
                    if (previousPhase == InteractWithFacesBehavior.Phase.TrackingFace && sawBoth)
                    { timedHead |= message is SetHeadAngle; timedBody |= message is SetBodyAngle; }
                }
                sentMark = f.Rig.Mark();
                byte tag = f.Rig.Robot.Animations.CurrentTag;
                if (tag != 0 && tags.Add(tag))
                {
                    f.Rig.Data(new AnimationStarted { AnimId = tag });
                    f.Rig.Data(new AnimationEnded { AnimId = tag });
                }
                if (behavior.NativeTracker is { } tracker)
                {
                    if (behavior.CurrentPhase == InteractWithFacesBehavior.Phase.DrivingForward)
                    {
                        sawHeadOnly |= tracker.RequiredTrackMask == 1 && owner.ActionList.ContainsRunner(tracker);
                        detector.EyeY = 70;
                        if (pathId is { } id && !started)
                        { f.Rig.Data(new PathFollowingEvent { EventId = id, EventType = 0 }); started = true; }
                        else if (pathId is { } finished && started && !completed && driveHead)
                        { f.Rig.Data(new PathFollowingEvent { EventId = finished, EventType = 2 }); completed = true; }
                    }
                    else if (behavior.CurrentPhase == InteractWithFacesBehavior.Phase.TrackingFace)
                    {
                        sawBoth |= tracker.RequiredTrackMask == 5 && owner.ActionList.ContainsRunner(tracker);
                        detector.EyeX = 75; detector.EyeY = 140;
                    }
                }
                previousPhase = behavior.CurrentPhase;
                f.Image((uint)(501 + i), modes: 1 << 2);
                Thread.Sleep(2);
            }
            Assert.True(sawHeadOnly && driveHead && !driveBody && sawBoth && timedHead && timedBody,
                $"headOnly={sawHeadOnly} driveHead={driveHead} driveBody={driveBody} both={sawBoth} timedHead={timedHead} timedBody={timedBody}; {string.Join(" | ", behavior.Trace)}");
            var tracked = Assert.IsType<NativeTrackFaceRunner>(behavior.NativeTracker);
            if (replaceOwner)
            {
                f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId, false);
                f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId, withRic: false);
                int mark = f.Rig.Mark();
                behavior.Stop(BehaviorStopReason.Interrupted);
                f.Rig.Tick();
                Assert.DoesNotContain(f.Rig.SentSince(mark), message => message is SetHeadAngle or SetBodyAngle or MoveHead or DriveWheels);
                return;
            }
            behavior.Stop(BehaviorStopReason.Interrupted);
            Assert.False(owner.ActionList.ContainsRunner(tracked));
            Assert.True(owner.ActionList.ContainsRunner(unrelated));
        }
        finally { behavior.Stop(BehaviorStopReason.Interrupted); if (!replaceOwner) owner.ActionList.Cancel(unrelated.Tag); }
    }

    [Fact]
    public void CapturedFramesRetainTheirOwnNativeCameraPositionUntilDrain()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var vision = f.M.Vision;
        Assert.True(SpinWait.SpinUntil(() => vision.Enabled, TimeSpan.FromSeconds(5)));
        var detector = new OneFace(); vision.FaceDetector = detector;
        vision.ModeEnableMask = 1 << 2;
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        var positions = new List<Vec3>();
        vision.Faces.FaceObserved += o => { if (o.Face.NativeTrackingTranslation is { } p) positions.Add(p); };
        var priorGate = f.Rig.Engine.HasCameraCalibration;
        f.Rig.Engine.HasCameraCalibration = () => false;
        try
        {
            void Frame(uint id, float head)
            {
                f.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos, origin: f.Robot.CurrentOriginId, head: head);
                uint timestamp = f.Robot.StoredState!.Timestamp;
                f.Rig.Data(new ImageImuData { ImageId = id, Line2Number = 0 });
                f.Rig.Tick();
                f.Rig.CameraGrayFrame(id, timestamp, Enumerable.Repeat((byte)150, 320 * 240).ToArray());
                vision.WaitForProcessorIdle();
            }
            Frame(101, .1f);
            Frame(102, .2f);
            Assert.Empty(positions);
            f.Rig.Engine.HasCameraCalibration = priorGate;
            f.Rig.Engine.VisionUpdateAllResultsFailed!();
            Assert.True(positions.Count == 2, $"detector calls={detector.Calls}, positions={positions.Count}, frames={vision.FramesProcessed}");
            Assert.NotEqual(positions[0], positions[1]);
            Assert.NotNull(vision.Faces.GetNativeFace(f.Robot, -7)?.NativeTrackingRotation);
            var runner = new NativeTrackFaceRunner(f.M, f.Robot, -7);
            f.Robot.ActionList.QueueAction(QueueActionPosition.Now, runner);
            int mark = f.Rig.Mark();
            f.Rig.Tick();
            Assert.NotEmpty(f.Rig.SentSince(mark).OfType<SetHeadAngle>());
            f.Robot.ActionList.Cancel(runner.Tag);
            f.Rig.Tick();
        }
        finally { f.Rig.Engine.HasCameraCalibration = priorGate; }
    }

    [Fact]
    public void CapturedCameraAndRobotTransformsMatchNativeFaceParentWalk()
    {
        using var root = new Pose3d("VisionSystemOrigin");
        using var robot = new Pose3d(Rotation3d.FromAngleAxis(.37f, new Vec3(0, 0, 1)), new Vec3(130, -24, 7), parent: root);
        using var camera = new Pose3d(Rotation3d.FromAngleAxis(-.41f, new Vec3(0, 1, 0)), new Vec3(19, 4, 53), parent: robot);
        var point = new Vec3(42.125f, -17.75f, 263.5f);
        var initial = Rotation3d.FromAngleAxis(BitConverter.UInt32BitsToSingle(0x3FC90FDB), new Vec3(1, 0, 0));
        using var face = new Pose3d(initial, point, parent: camera);
        using var native = face.GetWithRespectToRoot();
        var captured = new Transform3d(initial, point);
        var cameraTransform = camera.Transform;
        var robotTransform = robot.Transform;
        captured.PreComposeWith(ref cameraTransform);
        captured.PreComposeWith(ref robotTransform);
        Assert.Equal(native.Translation, captured.Translation);
        Assert.Equal(native.Quaternion, captured.Rotation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnrollmentProfileClampsSmallAnglesAndMissingNativePositionDoesNotMove(bool cliff)
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        f.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos, head: 0);
        if (cliff)
        {
            f.Rig.Data(new CliffEvent { Timestamp = f.Robot.StoredState!.Timestamp, DetectedFlags = 1 });
            f.Rig.Tick();
        }
        var origin = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        var face = new TrackedFace(new DetectedFace(-2, new FaceRect(0, 0, 10, 10)), 10)
        { HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(200, 0, 49)) };
        var entry = f.M.Vision.Faces.AddOrUpdateNativeFace(face, Pose3d.Identity, false, origin.Node!)!.Face;
        var runner = new NativeTrackFaceRunner(f.M, f.Robot, -2, enrollment: true);
        Assert.Equal(cliff ? 1u : 5u, runner.RequiredTrackMask);
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now, runner);
        int mark = f.Rig.Mark();
        f.Rig.Tick();
        Assert.Empty(f.Rig.SentSince(mark).OfType<SetHeadAngle>());
        Assert.Empty(f.Rig.SentSince(mark).OfType<SetBodyAngle>());
        entry.NativeTrackingTranslation = new Vec3(200, 0, 49);
        entry.LastObservedTimestamp = 11;
        mark = f.Rig.Mark();
        f.Rig.Tick();
        var head = Assert.Single(f.Rig.SentSince(mark).OfType<SetHeadAngle>());
        var bodies = f.Rig.SentSince(mark).OfType<SetBodyAngle>().ToArray();
        float tolerance = BitConverter.UInt32BitsToSingle(0x3D8EFA35);
        Assert.Equal(tolerance, head.AngleRad, 5);
        if (cliff) Assert.Empty(bodies);
        else
        {
            var body = Assert.Single(bodies);
            Assert.Equal(tolerance, body.AngleRad, 5);
            Assert.Equal(tolerance, body.ToleranceRad);
        }
        f.Robot.ActionList.Cancel(runner.Tag);
        f.Rig.Tick();
    }

    [Fact]
    public void QueueUsesFreshFaceAndKeepsIdentityChangesInCompletion()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var origin = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        var face = new TrackedFace(new DetectedFace(-5, new FaceRect(0, 0, 10, 10)), 10)
        {
            HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(200, 100, 100)),
            NativeTrackingTranslation = new Vec3(200, 100, 100)
        };
        Assert.NotNull(f.M.Vision.Faces.AddOrUpdateNativeFace(face, Pose3d.Identity, false, origin.Node!));
        var runner = new NativeTrackFaceRunner(f.M, f.Robot, -5);
        var reports = new List<RobotCompletedAction>();
        var app = new List<ActionCompletionReport>();
        f.Rig.Engine.ActionCompleted += app.Add;
        using var sub = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A, true, p => { if (p is RobotCompletedAction r) reports.Add(r); });
        Assert.Equal(0, f.Robot.ActionList.QueueAction(QueueActionPosition.Now, runner));
        int mark = f.Rig.Mark();
        f.Rig.Tick();
        Assert.Equal(-5, f.Robot.NativeTrackingFaceId);
        var sent = f.Rig.SentSince(mark);
        var head = Assert.Single(sent.OfType<SetHeadAngle>());
        var body = Assert.Single(sent.OfType<SetBodyAngle>());
        float tilt = MathF.Atan(51f / MathF.Sqrt(200f * 200f + 100f * 100f));
        float pan = MathF.Atan2(100f, 200f);
        Assert.Equal(tilt, head.AngleRad, 5);
        Assert.Equal(MathF.Abs(tilt - f.Head) / .15f, head.MaxSpeedRadPerSec, 5);
        Assert.Equal(10000f, head.AccelRadPerSec2);
        Assert.Equal(0f, head.DurationSec);
        Assert.Equal(pan, body.AngleRad, 5);
        Assert.Equal(MathF.Abs(pan) / .4f, body.MaxSpeedRadPerSec, 5);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x436DFFDB), body.AccelRadPerSec2);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x3D0EFA35), body.ToleranceRad);
        Assert.Equal((ushort)0, body.NumHalfRevolutions);
        Assert.True(body.IsAbsolute);
        Assert.True(sent.IndexOf(head) < sent.IndexOf(body));
        mark = f.Rig.Mark();
        f.Rig.Tick();
        Assert.Empty(f.Rig.SentSince(mark).OfType<SetHeadAngle>());
        Assert.Empty(f.Rig.SentSince(mark).OfType<SetBodyAngle>());
        f.M.Vision.Faces.ChangeFaceID(-5, 9);
        Assert.Equal(-5, f.Robot.NativeTrackingFaceId);
        f.Robot.ActionList.Cancel(runner.Tag);
        f.Rig.Tick();
        Assert.Equal(0, f.Robot.NativeTrackingFaceId);
        var report = Assert.Single(reports.Where(r => r.Tag == runner.Tag));
        Assert.Equal((byte)3, report.CompletionVariant);
        Assert.Equal(9u, report.CompletionUnion);
        Assert.Equal(new byte[] { 3, 9, 0, 0, 0 }, report.PackBody()[^5..]);
        var publicReport = Assert.Single(app.Where(r => r.Tag == runner.Tag));
        Assert.Equal((byte)3, publicReport.CompletionVariant);
        Assert.Equal(9u, publicReport.NativeCompletionCache);
    }
}
