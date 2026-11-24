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
public sealed class NativePhysicalReactionControlTests
{
    private static Fixture NewFixture()
    {
        var f=new Fixture(sourceNvEmpty:true);
        f.Rig.Robot.SetEnableFreeplayActivity(false);f.Rig.Tick(4000);
        var manager=f.Robot.BehaviorManagerOwner!;manager.Activity=null;
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        manager.DisableAllReactionsWithLock("physical-unit-input");
        return f;
    }
    private static void Start(Fixture f,SteppedBehavior behavior)=>behavior.StartAsync(f.Runtime.Context,new BehaviorScope(),default).GetAwaiter().GetResult();

    // 607C46..607CA8: raw>>4 >24 queues a head-only CalibrateMotorAction
    // with no result branch. Exercise the actual C0 latch and IAction timeout,
    // not a host WaitUntil completion or a weakened replay MISSING allowlist.
    [Theory] [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public void PickupRetryOwnsNativeHeadCalibrationAndItsResult(bool timeout, bool stopOnEvent)
    {
        using var f=NewFixture();
        var manager=f.Robot.BehaviorManagerOwner!;
        var b=Assert.IsType<ReactToPickupBehavior>(Assert.Single(manager.Reactions,
            r=>r.Strategy.Trigger==ReactionTrigger.RobotPickedUp).Behavior);
        Assert.True(b.NativeBound);
        for(int i=0;i<10;i++)f.Motors(RobotStatusFlag.IsPickedUp);
        Assert.Equal(OffTreadsState.InAir,f.Rig.Robot.Sensors.OffTreadsState);
        Start(f,b);
        // Explicit cancellation boundary: initial Wait callback ignores its result;
        // cancel the resulting uninitialized animation to reach the next retry.
        f.Robot.ActionList.Cancel(b.OwnedRunner!.Tag);
        var first=Assert.IsAssignableFrom<TriggerAnimationAction>(b.OwnedRunner);
        f.Robot.ActionList.Cancel(first.Tag);
        f.Rig.Tick(7000); // beyond the source 3..6-second first retry deadline
        var state=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos|RobotStatusFlag.IsPickedUp,
            origin:f.Robot.CurrentOriginId,frame:f.Robot.PoseFrameId);
        state.CliffDataRaw[0]=649; // actual high-raw sample in the recorded cliff window
        f.Rig.Data(state);f.Rig.Tick(); // CliffSensorComponent adopts the new raw sample on its update.
        var events=new List<EngineLogEntry>();var gaps=new List<string>();var reports=new List<RobotCompletedAction>();
        f.Robot.ActionList.RegisterActionEndedCallback(reports.Add);
        SteppedBehavior.ResetMissingForTests();
        f.Rig.Engine.StackLogLine+=line=>{if(line.Contains("MISSING",StringComparison.Ordinal))gaps.Add(line);};
        f.Rig.Engine.LogEntry+=entry=>
        {
            if(entry.Name!="BehaviorReactToPickup.CalibratingHead")return;
            events.Add(entry);
            if(stopOnEvent)b.Stop(BehaviorStopReason.Interrupted);
        };
        int mark=f.Rig.Mark();
        b.Update(f.Runtime.Context,f.Rig.Engine.Timer.TimeStampMs);
        Assert.Equal("649",Assert.Single(events).Text);
        Assert.Equal(EngineLogLevel.Event,events[0].Level);
        if(stopOnEvent)
        {
            Assert.Null(b.OwnedRunner);f.Rig.Tick();
            Assert.Empty(f.Rig.SentSince(mark).OfType<StartMotorCalibration>());
            return;
        }
        try
        {
            var runner=Assert.IsType<NativeHeadCalibrationRunner>(b.OwnedRunner);
            Assert.Same(runner,f.Robot.ActionList.GetCurrentAction());
            f.Rig.Tick();
            var request=Assert.Single(f.Rig.SentSince(mark).OfType<StartMotorCalibration>());
            Assert.True(request.Head);Assert.False(request.Lift);
            f.Rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=false});f.Rig.Tick();
            Assert.Same(runner,b.OwnedRunner); // done without a start report is insufficient
            if(timeout)f.Rig.Tick(30001);
            else
            {
                f.Rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=true});f.Rig.Tick();
                Assert.Same(runner,b.OwnedRunner);
                f.Rig.Data(new MotorCalibration{MotorID=MotorID.MOTOR_HEAD,CalibStarted=false});f.Rig.Tick();
            }
            f.Rig.Tick();f.Rig.Engine.DrainAppDelivery();
            Assert.Contains(reports,r=>r.Tag==runner.Tag && r.Result==(timeout?EngineActionResult.Timeout:0));
            Assert.DoesNotContain(gaps,g=>g.Contains("CalibratingHead engine event fields",StringComparison.Ordinal) ||
                g.StartsWith("MISSING: M8-015 host action producer",StringComparison.Ordinal));
        }
        finally{b.Stop(BehaviorStopReason.Interrupted);}
    }

    [Fact]
    public void ReactionRowsUseTheirConfiguredConstructorInstances()
    {
        using var f=NewFixture();var manager=f.Robot.BehaviorManagerOwner!;
        foreach(var (id,trigger) in new[]{("ReactToCliff",ReactionTrigger.CliffDetected),("ReactToImpact",ReactionTrigger.RobotFalling),
            ("ReactToPlacedOnSlope",ReactionTrigger.RobotPlacedOnSlope),("ReactToReturnedToTreads",ReactionTrigger.ReturnedToTreads),
            ("ReactToPickup",ReactionTrigger.RobotPickedUp),("ReactToRobotOnBack",ReactionTrigger.RobotOnBack),
            ("ReactToMotorCalibration",ReactionTrigger.MotorCalibration),
            ("ReactToUnexpectedMovement",ReactionTrigger.UnexpectedMovement)})
            Assert.Same(f.Runtime.Bound[id],Assert.Single(manager.Reactions,r=>r.Strategy.Trigger==trigger).Behavior);
    }

    [Fact]
    public void PhysicalReactionsReadTheOwnedPlatformCarryingAndOffTreadsGates()
    {
        using var f = NewFixture();
        var gates = new[]
        {
            ("ReactToCliff", false, true), ("ReactToPickup", true, true),
            ("ReactToRobotOnBack", true, true), ("ReactToImpact", false, false),
            ("ReactToMotorCalibration", false, true), ("ReactToPlacedOnSlope", true, true)
        };
        var sensors = f.Rig.Robot.Sensors;
        Assert.Equal(OffTreadsState.OnTreads, sensors.OffTreadsState);
        bool Runnable(string id) => Assert.IsAssignableFrom<SteppedBehavior>(f.Runtime.Bound[id]).IsRunnableBase(f.Runtime.Context);
        // Robot+0x34A is the platform writer at 0x00511D4C, independent of contact.
        sensors.SetOnChargerPlatform(true);
        Assert.True(sensors.OnChargerPlatform);
        Assert.False(sensors.OnChargerContacts);
        foreach (var (id, _, _) in gates) Assert.False(Runnable(id));
        sensors.SetOnChargerPlatform(false);
        foreach (var (id, _, _) in gates) Assert.True(Runnable(id));

        f.Add();
        f.Robot.CarryingOwner!.SetCarrying(7);
        foreach (var (id, _, carries) in gates) Assert.Equal(carries, Runnable(id));
        f.Robot.CarryingOwner.UnsetCarrying();

        for (int i = 0; i < 10; i++) f.Motors(RobotStatusFlag.IsPickedUp);
        Assert.Equal(OffTreadsState.InAir, sensors.OffTreadsState);
        foreach (var (id, offTreads, _) in gates) Assert.Equal(offTreads, Runnable(id));
    }

    [Fact]
    public void MotorCalibrationWaitWarnsWithTheCurrentNativeMotorFlags()
    {
        using var f = NewFixture();
        var behavior = Assert.IsType<ReactToMotorCalibrationBehavior>(f.Runtime.Bound["ReactToMotorCalibration"]);
        f.Rig.Data(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = true });
        f.Rig.Tick();
        var entries = new List<EngineLogEntry>();
        f.Rig.Engine.LogEntry += entries.Add;
        Start(f, behavior);
        try
        {
            Assert.Equal(5f, Assert.IsType<NativeTimedWaitRunner>(behavior.OwnedRunner).Duration);
            f.Rig.Tick();
            f.Rig.Tick(5001);
            var warning = Assert.Single(entries, entry => entry.Name == "BehaviorReactToMotorCalibration.Timeout");
            Assert.EndsWith("Calibration didn't complete (lift: 1, head: 0)", warning.Text, StringComparison.Ordinal);
            Assert.Equal(EngineLogLevel.Warning, warning.Level);
        }
        finally { behavior.Stop(BehaviorStopReason.Interrupted); }
    }

    [Theory]
    [InlineData(399)]
    [InlineData(400)]
    public void ConfiguredOnBackUsesTheCliffRawBoundaryAndNativeWait(ushort front)
    {
        using var f = NewFixture();
        var behavior = Assert.IsType<ReactToRobotOnBackBehavior>(f.Runtime.Bound["ReactToRobotOnBack"]);
        for (int i = 0; i < 40; i++)
        {
            var state = f.Rig.MakeState(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos | RobotStatusFlag.IsPickedUp,
                origin: f.Robot.CurrentOriginId, frame: f.Robot.PoseFrameId);
            state.Pose.Pitch = (float)OffTreadsClassifier.OnBackCentrePhysicalRad;
            state.Accel = new AccelData { X = 9800 };
            state.CliffDataRaw[0] = front;
            f.Rig.Data(state);
            f.Rig.Tick();
        }
        Assert.Equal(OffTreadsState.OnBack, f.Rig.Robot.Sensors.OffTreadsState);
        var logs = new List<EngineLogEntry>();
        f.Rig.Engine.LogEntry += logs.Add;
        Start(f, behavior);
        try
        {
            var action = behavior.OwnedRunner;
            if (front < 400)
            {
                var flip = Assert.IsType<TriggerAnimationAction>(action);
                Assert.Equal(AnimationTrigger.FlipDownFromBack, flip.Request.Trigger);
            }
            else
            {
                Assert.IsType<NativeHeadCalibrationRunner>(action);
                var entry = Assert.Single(logs, l => l.Name == "BehaviorReactToRobotOnBack.FlipDownIfNeeded.CalibratingHead");
                Assert.Equal("400", entry.Text);
            }
            // Both action arms use the same resultless DelayThenFlipDown callback.
            f.Robot.ActionList.Cancel(action!.Tag);
            Assert.Equal(.5f, Assert.IsType<NativeTimedWaitRunner>(behavior.OwnedRunner).Duration);
        }
        finally { behavior.Stop(BehaviorStopReason.Interrupted); }
    }

    // Ctor arguments from 60518E..6051BC,6080B2..6080F8,609B12..609B68, not a substitute clip selector.
    [Theory]
    [InlineData("ReactToCliff",0,0x13D)] [InlineData("ReactToCliff",1,0x131)] [InlineData("ReactToCliff",3,0x19D)]
    [InlineData("ReactToPlacedOnSlope",0,0x145)] [InlineData("ReactToPlacedOnSlope",1,0x139)] [InlineData("ReactToPlacedOnSlope",3,0x1A8)]
    [InlineData("ReactToUnexpectedMovement",0,0x1AE)] [InlineData("ReactToUnexpectedMovement",1,0x1AD)] [InlineData("ReactToUnexpectedMovement",3,0x1AC)]
    public void NativeAdmissionQueuesTheSelectedNeedActionWithEveryDefaultArgument(string id,int need,int trigger)
    {
        using var f=NewFixture();if(need<3)f.Rig.Engine.Needs.SevereNeeds.Set((NeedId)need);
        var behavior=Assert.IsAssignableFrom<SteppedBehavior>(f.Runtime.Bound[id]);
        if(behavior is ReactToCliffBehavior cliff)cliff.HandleCliffEvent(1); // unit boundary: subscribed CliffEvent+4 is nonzero
        Start(f,behavior);
        try
        {
            var action=Assert.IsAssignableFrom<TriggerAnimationAction>(behavior.OwnedRunner);
            if(id=="ReactToPlacedOnSlope")Assert.IsType<TriggerAnimationAction>(action);
            else Assert.IsType<TriggerLiftSafeAnimationAction>(action);
            Assert.Equal(trigger,(int)action.Request.Trigger);Assert.Equal(1,action.Request.Argument2);
            Assert.True(action.Request.Argument3);Assert.Equal(0,action.Request.Argument4);
            Assert.Equal(0x42700000u,BitConverter.SingleToUInt32Bits(action.Request.Argument5));Assert.False(action.Request.Argument6);
            Assert.Same(action,f.Robot.ActionList.GetCurrentAction());
        }
        finally{behavior.Stop(BehaviorStopReason.Interrupted);}
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ImpactQuietBranchDeliversTheActualWaitResult(bool timeout)
    {
        using var f=NewFixture();var b=Assert.IsType<ReactToImpactBehavior>(f.Runtime.Bound["ReactToImpact"]);
        // Unit inputs of AlwaysHandle606408; the wire replay separately exercises actual ingress.
        b.HandleFallingStarted();b.HandleFallingStopped(1000f);if(!timeout)b.HandleMotorCalibration(true,true);
        var reports=new List<ActionCompletionReport>();f.Rig.Engine.ActionCompleted+=reports.Add;
        Start(f,b);var wait=Assert.IsType<NativePredicateWaitRunner>(b.OwnedRunner);
        Assert.Equal(0x33,wait.Type);Assert.Equal(0u,wait.RequiredTrackMask);Assert.Equal(5f,wait.TimeoutSeconds);
        f.Rig.Tick();if(timeout)f.Rig.Tick(5001);f.Rig.Engine.DrainAppDelivery();
        Assert.Contains(reports,r=>r.Tag==wait.Tag&&r.Result==(timeout?0x03000018u:0u));
        Assert.Equal(timeout,b.WaitedOut);Assert.Null(b.OwnedRunner);
        Assert.DoesNotContain(reports,r=>r.AnimationName is not null);
        b.Stop(BehaviorStopReason.Completed);
    }

    [Theory] [InlineData(.3f,true)] [InlineData(-.3f,false)]
    public void SlopeStoresSignedPitchAndClearsRepeatFlagAtCalibrationAdmission(float pitch,bool high)
    {
        using var f=NewFixture();
        var state=f.Rig.MakeState(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,
            origin:f.Robot.CurrentOriginId,frame:f.Robot.PoseFrameId);state.Pose.Pitch=pitch;f.Rig.Data(state);f.Rig.Tick();
        var b=Assert.IsType<ReactToPlacedOnSlopeBehavior>(f.Runtime.Bound["ReactToPlacedOnSlope"]);
        Start(f,b);var first=Assert.IsType<TriggerAnimationAction>(b.OwnedRunner);
        // The source callback ignores result (608240): use real queue cancellation/completion, not a direct flag write.
        f.Robot.ActionList.Cancel(first.Tag);Assert.Equal(high,b.PitchWasHigh);b.Stop(BehaviorStopReason.Interrupted);
        Start(f,b);
        if(high){Assert.IsType<NativeHeadCalibrationRunner>(b.OwnedRunner);Assert.False(b.PitchWasHigh);}
        else Assert.IsType<TriggerAnimationAction>(b.OwnedRunner);
        b.Stop(BehaviorStopReason.Interrupted);
    }

    [Fact]
    public void CliffFinishedBroadcastQueuesRawReportBeforeSynchronousLocalAndHostCallback()
    {
        using var f=NewFixture();var order=new List<string>();
        f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x36){Assert.Empty(body.ToArray());order.Add("raw");}};
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x36,true,p=>{Assert.IsType<NativeCliffEventFinished>(p);order.Add("local");});
        f.Rig.Engine.ReportCliffEventFinished(()=>order.Add("host"));Assert.Equal(new[]{"local"},order);
        f.Rig.Engine.DrainAppDelivery();Assert.Equal(new[]{"local","raw","host"},order);
    }

    [Theory] [InlineData(9990,true)] [InlineData(10000,false)]
    public void SlopeRepeatUsesStrictTenSecondsFromTheFloatEngineClock(int elapsed,bool calibrate)
    {
        using var f=NewFixture();
        var state=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,
            origin:f.Robot.CurrentOriginId,frame:f.Robot.PoseFrameId);state.Pose.Pitch=.3f;f.Rig.Data(state);f.Rig.Tick();
        var b=Assert.IsType<ReactToPlacedOnSlopeBehavior>(f.Runtime.Bound["ReactToPlacedOnSlope"]);
        Start(f,b);var first=Assert.IsType<TriggerAnimationAction>(b.OwnedRunner);
        f.Robot.ActionList.Cancel(first.Tag);Assert.True(b.PitchWasHigh);b.Stop(BehaviorStopReason.Interrupted);
        f.Rig.Tick(elapsed);Start(f,b);
        if(calibrate)Assert.IsType<NativeHeadCalibrationRunner>(b.OwnedRunner);
        else Assert.IsType<TriggerAnimationAction>(b.OwnedRunner);
        b.Stop(BehaviorStopReason.Interrupted);
    }

    [Fact]
    public void RetiredPhysicalInstancesCannotRunAgainstAReplacementRobot()
    {
        using var f=NewFixture();var context=f.Runtime.Context;
        var bodies=new[]{"ReactToCliff","ReactToImpact","ReactToPlacedOnSlope","ReactToUnexpectedMovement"}
            .Select(id=>Assert.IsAssignableFrom<SteppedBehavior>(f.Runtime.Bound[id])).ToArray();
        f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        int mark=f.Rig.Mark();foreach(var body in bodies){Assert.False(body.IsRunnable(context));StartRetired(body,context);}
        Assert.Empty(f.Rig.SentSince(mark));
        static void StartRetired(SteppedBehavior body,BehaviorContext context)=>Assert.Throws<ObjectDisposedException>(
            ()=>body.StartAsync(context,new BehaviorScope(),default).GetAwaiter().GetResult());
    }

    [Fact]
    public void ReplacementRobotHasItsOwnSevereNeedsAndWakeSubscription()
    {
        using var f = NewFixture();
        var engine = f.Rig.Engine;
        var first = f.Runtime.AI.SevereNeeds;
        first.Set(NeedId.Play);
        Assert.Equal((int)NeedId.Play, first.Selected);

        engine.Robots.RemoveRobot(CozmoEngine.RobotId, false);
        Assert.Equal(0, engine.AnimationMessages.SubscriberCountForTest(166, false));
        engine.Robots.AddRobot(CozmoEngine.RobotId, withRic: false);
        var replacement = engine.Robot!.NativeBehaviorRuntime!.AI.SevereNeeds;
        Assert.Same(engine.Needs.SevereNeeds, replacement);
        Assert.NotSame(first, replacement);
        Assert.Equal(3, replacement.Selected);
        Assert.Equal(1, engine.AnimationMessages.SubscriberCountForTest(166, false));

        first.Set(NeedId.Energy);
        Assert.Equal((int)NeedId.Play, first.Selected);
        Assert.Equal(3, replacement.Selected);
    }

    [Fact]
    public void UnexpectedRewindPreservesHistoricalParentAndObstacleOffsetsUseNativeFloatBits()
    {
        using var origin=new Pose3d("history-origin");
        using var historical=new Pose3d(Rotation3d.Identity,new Vec3(100,200,3),parent:origin);
        using var current=new Pose3d(Rotation3d.FromAngleAxis(.2f,new Vec3(0,0,1)),new Vec3(9,8,7));
        using var rewind=UnexpectedMovementResponse.RewoundPose(historical,current);
        Assert.Same(historical.Node!.Parent,rewind.Node!.Parent);
        Assert.Equal(new Vec3(100,200,3),rewind.Translation);Assert.Equal(current.Quaternion,rewind.Quaternion);
        using var left=UnexpectedMovementResponse.ObstacleInRobotFrame(UnexpectedMovementSide.Left);
        using var front=UnexpectedMovementResponse.ObstacleInRobotFrame(UnexpectedMovementSide.Front);
        Assert.Equal(0x42506666u,BitConverter.SingleToUInt32Bits((float)left.Translation.Y));
        Assert.Equal(0x423C6666u,BitConverter.SingleToUInt32Bits((float)front.Translation.X));
    }
}
