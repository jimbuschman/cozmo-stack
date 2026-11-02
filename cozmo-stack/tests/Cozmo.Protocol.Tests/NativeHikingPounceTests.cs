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
public sealed class NativeHikingPounceTests
{
    private static NativeHikingPounce Prepare(Fixture f)
    {
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        f.Rig.State(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,head:-.15f,liftAngle:-.2f);
        var p=Assert.IsType<NativeHikingPounce>(f.Runtime.Bound["Hiking_PounceOnMotion"]);
        f.Robot.Progression!.Unlocked.Add(p.NativeBaseInputs!.RequiredUnlock);
        return p;
    }

    [Theory]
    [InlineData(9999000000L,true)]
    [InlineData(10000000000L,false)]
    public void CliffTimerBoundaryControlsThePreconstructedPan(long nanoseconds,bool hasPan)
    {
        using var f=new Fixture();var p=Prepare(f);f.Rig.Engine.Timer.UpdateTime(nanoseconds);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(p.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var sequence=Assert.IsType<CompoundActionSequential>(p.OwnedRunner);
        Assert.Equal(hasPan?2:1,sequence.Children.Count);
        if(hasPan)
        {
            var pan=Assert.IsType<NativePanTiltRunner>(sequence.Children[0]);
            Assert.True(pan.Tag<sequence.Tag); // native constructs optional pan before the compound
        }
        Assert.IsType<TriggerLiftSafeAnimationAction>(sequence.Children[^1]);
    }

    [Fact]
    public void InitialCompoundAndPowerWaitFollowSuppliedTerminalResult()
    {
        using var f=new Fixture();var p=Prepare(f);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(p.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var initial=Assert.IsType<CompoundActionSequential>(p.OwnedRunner);
        Assert.Equal(2,initial.Children.Count);
        var pan=Assert.IsType<NativePanTiltRunner>(initial.Children[0]);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x3FC90FDB),(float)pan.Parameters.PanAngleRad);
        Assert.IsType<TriggerLiftSafeAnimationAction>(initial.Children[1]);
        int mark=f.Rig.Mark();
        initial.State=0x03000018;f.Robot.ActionList.Update(); // supplied terminal failure; source callback ignores result
        var disable=Assert.Single(f.Rig.SentSince(mark).OfType<EnableMotorPower>());
        Assert.Equal((byte)2,disable.Field0);Assert.Equal((byte)0,disable.Field1);
        Assert.True(p.LiftDisabled);Assert.Equal(10,p.NativeState);
        var wait=Assert.IsType<NativeTimedWaitRunner>(p.OwnedRunner);Assert.Equal(BitConverter.UInt32BitsToSingle(0x3E19999A),wait.Duration);
        wait.Init();float deadline=wait.Deadline;
        f.Rig.Engine.Timer.UpdateTime((long)Math.Round((double)deadline*1_000_000_000));Assert.NotEqual(0u,wait.CheckIfDone());
        f.Rig.Engine.Timer.UpdateTime((long)Math.Ceiling((double)MathF.BitIncrement(deadline)*1_000_000_000));Assert.Equal(0u,wait.CheckIfDone());
        int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>++words==4?0x80000000u:0u;
        try
        {
            wait.State=0x03000018;f.Robot.ActionList.Update(); // supplied failure, not a firmware completion
            Assert.False(p.LiftDisabled);Assert.Equal(3,p.NativeState);Assert.Equal(6,words);
            var search=Assert.IsType<CompoundActionSequential>(p.OwnedRunner);Assert.Equal(2,search.Children.Count);
            var first=Assert.IsType<NativePanTiltRunner>(search.Children[0]);var second=Assert.IsType<NativePanTiltRunner>(search.Children[1]);
            Assert.Equal((float)(20d*Math.PI/180d),(float)first.Parameters.PanAngleRad,6);
            Assert.Equal((float)(-20d*Math.PI/180d),(float)second.Parameters.PanAngleRad,6);
            search.State=0x03000018;f.Robot.ActionList.Update(); // 0x005F89BE..0x005F89CA: no head action here
            Assert.Equal(6,p.NativeState);var motionWait=Assert.IsType<NativeTimedWaitRunner>(p.OwnedRunner);Assert.Equal(2f,motionWait.Duration);
            motionWait.Init();float motionDeadline=motionWait.Deadline;
            f.Rig.Engine.Timer.UpdateTime((long)Math.Round((double)motionDeadline*1_000_000_000));Assert.NotEqual(0u,motionWait.CheckIfDone());
            f.Rig.Engine.Timer.UpdateTime((long)Math.Ceiling((double)MathF.BitIncrement(motionDeadline)*1_000_000_000));Assert.Equal(0u,motionWait.CheckIfDone());
            f.Rig.Engine.Timer.UpdateTime(20_000_000_000L);motionWait.State=0x03000018;f.Robot.ActionList.Update();
            Assert.Equal(13,p.NativeState);Assert.IsType<TriggerLiftSafeAnimationAction>(p.OwnedRunner);
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }

    [Fact]
    public void RealFrameWithBoundProducerKeepsUnselectedReadinessKnown()
    {
        using var f=new Fixture();var p=Prepare(f);
        var chooser=new ScoringChooser(new[]{f.Robot.NativeConfiguredScoreFields![p.Id].Value!},f.Runtime.Bound,native:true);
        chooser.EnableOrdinaryUnbuiltBoundary(f.Robot);
        Assert.False(chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,f.Rig.Engine.Timer.SecondsF).InputUnavailable);
        f.Image(1); // actual F4/F2 ingress with the configured source-owned producer
        Assert.False(chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,f.Rig.Engine.Timer.SecondsF).InputUnavailable);
        Assert.True(p.HostScoringInputsAvailable());
    }

    [Fact]
    public void ReplacementRejectsMatchingOldCompletionAfterRetirement()
    {
        using var f=new Fixture();var p=Prepare(f);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(p.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var old=Assert.IsAssignableFrom<ActionRunner>(p.OwnedRunner);uint tag=p.CurrentRunnerTag;
        f.Rig.Engine.Robots.RemoveRobot(1,false);var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
        var next=Assert.IsType<NativeHikingPounce>(replacement.NativeBehaviorRuntime!.Bound[p.Id]);
        var retired=p.OwnedRunner;uint retiredTag=p.CurrentRunnerTag;int state=p.NativeState;int sent=f.Rig.SentSince(0).Count;
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(tag,old.Type,0,Array.Empty<uint>(),0));
        if(retired is not null)f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(retiredTag,retired.Type,0,Array.Empty<uint>(),0));
        Assert.Same(retired,p.OwnedRunner);Assert.Equal(state,p.NativeState);Assert.Null(next.OwnedRunner);Assert.False(next.EngineRunning);
        Assert.Equal(sent,f.Rig.SentSince(0).Count);
    }

    [Fact]
    public void ResumeUsesHeadDownRatherThanRepeatingInitialCompound()
    {
        using var f=new Fixture();var p=Prepare(f);var manager=f.Robot.BehaviorManagerOwner!;
        Assert.True(manager.StartAsync(p.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        int starts=p.ScoreIncreaseCount;manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        using var scope=new BehaviorScope();p.AttachRun(f.Runtime.Context,scope);
        Assert.False(p.Resume(null,f.Rig.Engine.Timer.SecondsF));
        Assert.Equal(4,p.NativeState);Assert.NotNull(p.OwnedRunner);Assert.IsNotType<CompoundActionSequential>(p.OwnedRunner);
        Assert.Equal(starts,p.ScoreIncreaseCount);
    }

    [Theory]
    [InlineData(0.02f,false)]
    [InlineData(0.020001f,true)]
    public void ActualPitchUsesStrictCaughtThreshold(float pitch,bool caught)
    {
        using var f=new Fixture();var p=Prepare(f);
        var state=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,frame:f.Robot.PoseFrameId,liftAngle:-.2f);
        state.Pose.Pitch=pitch;f.Rig.Data(state);f.Rig.Tick();
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(p.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var initial=Assert.IsAssignableFrom<ActionRunner>(p.OwnedRunner);initial.State=0x03000018;f.Robot.ActionList.Update();
        var wait=Assert.IsAssignableFrom<ActionRunner>(p.OwnedRunner);wait.State=0x03000018;f.Robot.ActionList.Update();
        Assert.Equal(caught?2:3,p.NativeState);
        if(caught)Assert.IsType<TriggerLiftSafeAnimationAction>(p.OwnedRunner);
        else Assert.IsType<CompoundActionSequential>(p.OwnedRunner);
    }

    [Theory]
    [InlineData(35.5f,false)]
    [InlineData(35.6f,true)]
    public void ActualLiftHeightUsesStrictCaughtThreshold(float height,bool caught)
    {
        using var f=new Fixture();var p=Prepare(f);
        float angle=MathF.Asin((height-45f)/66f);
        var state=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,frame:f.Robot.PoseFrameId,liftAngle:angle);
        Assert.Equal(height,state.LiftHeightMm,3);f.Rig.Data(state);f.Rig.Tick();
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(p.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var initial=Assert.IsAssignableFrom<ActionRunner>(p.OwnedRunner);initial.State=0x03000018;f.Robot.ActionList.Update();
        var wait=Assert.IsAssignableFrom<ActionRunner>(p.OwnedRunner);wait.State=0x03000018;f.Robot.ActionList.Update();
        Assert.Equal(caught?2:3,p.NativeState);
    }

    [Fact]
    public void TransportCallbackStoppingDuringDisableRestoresPendingLiftOwnership()
    {
        // This rig connects Engine to the real ManualPump transport; its standalone M is not used.
        using var resources=new Fixture();using var rig=new Rig(resources:resources.Rig.Engine.Options.ResourcesPath);
        rig.Robot.Animations.LoadFrom(rig.Robot.Engine.Options.ResourcesPath!);rig.State(liftAngle:-.2f);rig.Tick();rig.Pump();
        var owner=rig.Robot.Engine.Robot!;var p=Assert.IsType<NativeHikingPounce>(owner.NativeBehaviorRuntime!.Bound["Hiking_PounceOnMotion"]);
        owner.Progression!.Unlocked.Add(p.NativeBaseInputs!.RequiredUnlock);var manager=owner.BehaviorManagerOwner!;
        Assert.True(manager.StartAsync(p.Id,rig.Robot.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var initial=Assert.IsType<CompoundActionSequential>(p.OwnedRunner);
        bool called=false;int mark=rig.Sent.Count;
        void Reenter()
        {
            if(called || !p.LiftDisabled)return;
            called=true;manager.Stop(BehaviorStopReason.Interrupted,rig.Robot.Engine.Timer.SecondsF);
        }
        rig.Robot.Transport.MessageQueued+=Reenter;
        try {initial.State=0x03000018;owner.ActionList.Update();}
        finally {rig.Robot.Transport.MessageQueued-=Reenter;}
        Assert.True(called);Assert.False(p.LiftDisabled);Assert.False(p.EngineRunning);Assert.Null(p.OwnedRunner);
        rig.Pump();
        var power=rig.Sent.Skip(mark).OfType<EnableMotorPower>().ToArray();
        Assert.Equal(new byte[]{0,1},power.Select(x=>x.Field1));Assert.All(power,x=>Assert.Equal((byte)2,x.Field0));
    }

    [Fact]
    public void TransportCallbackStoppingDuringRestoreDoesNotSendAnotherRestore()
    {
        using var resources=new Fixture();using var rig=new Rig(resources:resources.Rig.Engine.Options.ResourcesPath);
        rig.Robot.Animations.LoadFrom(rig.Robot.Engine.Options.ResourcesPath!);rig.State(liftAngle:-.2f);rig.Tick();rig.Pump();
        var owner=rig.Robot.Engine.Robot!;var p=Assert.IsType<NativeHikingPounce>(owner.NativeBehaviorRuntime!.Bound["Hiking_PounceOnMotion"]);
        owner.Progression!.Unlocked.Add(p.NativeBaseInputs!.RequiredUnlock);var manager=owner.BehaviorManagerOwner!;
        Assert.True(manager.StartAsync(p.Id,rig.Robot.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        int mark=rig.Sent.Count;var initial=Assert.IsAssignableFrom<ActionRunner>(p.OwnedRunner);
        initial.State=0x03000018;owner.ActionList.Update();
        var wait=Assert.IsType<NativeTimedWaitRunner>(p.OwnedRunner);Assert.True(p.LiftDisabled);
        bool called=false;
        void Reenter()
        {
            if(called || p.LiftDisabled)return;
            called=true;manager.Stop(BehaviorStopReason.Interrupted,rig.Robot.Engine.Timer.SecondsF);
        }
        rig.Robot.Transport.MessageQueued+=Reenter;
        try {wait.State=0x03000018;owner.ActionList.Update();}
        finally {rig.Robot.Transport.MessageQueued-=Reenter;}
        Assert.True(called);Assert.False(p.LiftDisabled);Assert.False(p.EngineRunning);Assert.Null(p.OwnedRunner);
        rig.Pump();var power=rig.Sent.Skip(mark).OfType<EnableMotorPower>().ToArray();
        Assert.Equal(new byte[]{0,1},power.Select(x=>x.Field1));Assert.All(power,x=>Assert.Equal((byte)2,x.Field0));
    }
    private static void Finish(NativeHikingPounce p,Fixture f)
    {var runner=Assert.IsAssignableFrom<ActionRunner>(p.OwnedRunner);runner.State=0x03000018;f.Robot.ActionList.Update();}
    [Fact]
    public void PounceResetsCreepCountAfterStartActing() // 0x005F8B36..0x005F8B38
    {
        using var f=new Fixture();var p=Prepare(f);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(p.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        for(int i=0;i<3;i++)Finish(p,f);
        for(int i=0;i<3;i++)
        {
            f.Rig.Engine.AnimationMessages.Deliver(0x49,true,new NativeObservedMotion(1,0,160,120,.1f,100,0,0,0,0,0,0,0,0,0,0));
            Finish(p,f);Assert.Equal(8,p.NativeState);Finish(p,f);Finish(p,f);Assert.Equal(6,p.NativeState);
        }
        f.Rig.Engine.ContextRandom.WordSourceForTest=()=>0u;
        try
        {
            f.Rig.Engine.AnimationMessages.Deliver(0x49,true,new NativeObservedMotion(2,0,160,120,.1f,60,0,0,0,0,0,0,0,0,0,0));
            Finish(p,f);Assert.Equal(9,p.NativeState);
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
        for(int i=0;i<12 && p.NativeState!=6;i++)Finish(p,f);
        Assert.Equal(6,p.NativeState);
        int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0u;};
        try
        {
            f.Rig.Engine.AnimationMessages.Deliver(0x49,true,new NativeObservedMotion(3,0,160,120,.1f,100,0,0,0,0,0,0,0,0,0,0));
            Assert.Equal(7,p.NativeState);Assert.Equal(0,words); // count back at 0: distance 100 creeps with no draw
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }
    [Fact]
    public void InitialReactionComparesPitchToTheRetainedBaseline() // 0x005F8BC4..0x005F8BCE, 0x005F8D20, 0x005F8DA4
    {
        using var f=new Fixture();var p=Prepare(f);
        void Pitch(float value)
        {
            var state=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,frame:f.Robot.PoseFrameId,liftAngle:-.2f);
            state.Pose.Pitch=value;f.Rig.Data(state);f.Rig.Tick();
        }
        var manager=f.Robot.BehaviorManagerOwner!;
        Pitch(.03f);
        Assert.True(manager.StartAsync(p.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Finish(p,f);Finish(p,f);Assert.Equal(2,p.NativeState); // baseline 0: caught
        Finish(p,f);Finish(p,f);Assert.Equal(6,p.NativeState);
        f.Rig.Engine.ContextRandom.WordSourceForTest=()=>0u;
        try
        {
            f.Rig.Engine.AnimationMessages.Deliver(0x49,true,new NativeObservedMotion(2,0,160,120,.1f,60,0,0,0,0,0,0,0,0,0,0));
            Assert.Equal(7,p.NativeState);Finish(p,f);Assert.Equal(9,p.NativeState); // 0x005F9740: saves .03 to +0x128
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        Pitch(.04f);
        Assert.True(manager.StartAsync(p.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Finish(p,f);Finish(p,f);Assert.Equal(3,p.NativeState); // delta .01 is not caught; absolute .04 would be
    }
    [Fact]
    public void ThreeRealWaitCancellationsThenShortDistanceStillConsumeProbabilityDraw()
    {
        using var f=new Fixture();var p=Prepare(f);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(p.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        void Complete(){var runner=Assert.IsAssignableFrom<ActionRunner>(p.OwnedRunner);runner.State=0x03000018;f.Robot.ActionList.Update();}
        Complete();Complete();Complete(); // supplied terminal results: initial, power wait, search (0x005F89BE: straight to wait)
        Assert.Equal(6,p.NativeState);
        int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0u;};
        try
        {
            for(int i=0;i<3;i++)
            {
                uint oldTag=p.CurrentRunnerTag;
                f.Rig.Engine.AnimationMessages.Deliver(0x49,true,new NativeObservedMotion(1,0,160,120,.1f,100,0,0,0,0,0,0,0,0,0,0));
                Assert.Equal(7,p.NativeState);Assert.NotEqual(oldTag,p.CurrentRunnerTag);
                Complete();Assert.Equal(8,p.NativeState);Assert.IsType<DriveStraightAction>(p.OwnedRunner);
                Complete();Assert.Equal(4,p.NativeState);Complete();Assert.Equal(6,p.NativeState);
            }
            Assert.Equal(0,words);
            f.Rig.Engine.AnimationMessages.Deliver(0x49,true,new NativeObservedMotion(2,0,160,120,.1f,60,0,0,0,0,0,0,0,0,0,0));
            Assert.Equal(2,words);Assert.Equal(7,p.NativeState);
            Complete();Assert.Equal(9,p.NativeState);Assert.IsType<CompoundActionSequential>(p.OwnedRunner);
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }    [Fact]
    public void RetiredPounceDriveCannotStopReplacementDrivingAnimations()
    {
        using var f=new Fixture();var old=f.Robot;
        var drive=new DriveStraightAction(f.M,old,20,60,true);
        f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
        var replacement=f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
        Assert.NotNull(replacement);
        var driving=f.Rig.Robot.Animations.DrivingAnimations;
        driving.Init(0,123,false,false);Assert.Equal(0,driving.State);
        drive.Dispose();Assert.Equal(0,driving.State);
    }    [Fact]
    public void OldCliffDispatchCannotEnterReplacementConfiguredPhase()
    {
        using var f=new Fixture();var old=f.Robot;EngineRobot? replacement=null;int configured=0;bool fired=false;
        void OnCliff(CliffReport _)=>configured++;
        f.Rig.Engine.MessageDispatchObservedForTest=(tag,name)=>
        {
            if(fired || tag!=RobotMessageId.CliffEvent || name!="Impl")return;
            fired=true;f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
            replacement=f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
            f.Rig.Robot.Sensors.ConfiguredBehaviorCliffDetected+=OnCliff;
        };
        try
        {
            f.Rig.Data(new CliffEvent{Timestamp=123,DetectedFlags=1});f.Rig.Tick();
            Assert.True(fired);Assert.NotNull(replacement);Assert.NotSame(old,replacement);Assert.Equal(0,configured);
        }
        finally {f.Rig.Engine.MessageDispatchObservedForTest=null;f.Rig.Robot.Sensors.ConfiguredBehaviorCliffDetected-=OnCliff;}
    }    [Fact]
    public void DriveMissingLogReplacementCannotStartForeignAnimation()
    {
        using var f=new Fixture();var old=f.Robot;var drive=new DriveStraightAction(f.M,old,20,60,true);
        bool fired=false;EngineRobot? replacement=null;
        void OnLog(string line)
        {
            if(fired || !line.Contains("M13-013 | CheckIfDone"))return;
            fired=true;f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);
            replacement=f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
            f.Rig.Robot.Animations.DrivingAnimations.Init(0,123,false,false);
        }
        f.Rig.Engine.StackLogLine+=OnLog;
        try
        {
            Assert.Equal(ActionResult.Abort,drive.CheckIfDone(new DriveStraightTick(false,true,true)));
            Assert.True(fired);Assert.NotNull(replacement);Assert.Equal(0,f.Rig.Robot.Animations.DrivingAnimations.State);
        }
        finally {f.Rig.Engine.StackLogLine-=OnLog;drive.Dispose();}
    }    [Fact]
    public void NonrunningMotionUsesStrictAreaAndInclusiveDistance()
    {
        using var f=new Fixture();var p=Prepare(f);float before=p.LastGroundMotionSeconds;
        f.Rig.Engine.Timer.UpdateTime(12000000000L);
        NativeObservedMotion Report(float area,short x)=>new(1,0,0,0,area,x,0,0,0,0,0,0,0,0,0,0);
        float threshold=BitConverter.UInt32BitsToSingle(0x3C23D70A);
        f.Rig.Engine.AnimationMessages.Deliver(0x49,true,Report(threshold,0));Assert.Equal(before,p.LastGroundMotionSeconds);
        f.Rig.Engine.AnimationMessages.Deliver(0x49,true,Report(MathF.BitIncrement(threshold),120));Assert.Equal(12f,p.LastGroundMotionSeconds);
        f.Rig.Engine.Timer.UpdateTime(13000000000L);
        f.Rig.Engine.AnimationMessages.Deliver(0x49,true,Report(MathF.BitIncrement(threshold),121));Assert.Equal(12f,p.LastGroundMotionSeconds);
    }}
