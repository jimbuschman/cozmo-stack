using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class AiPickupControlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectPickupUsesRealObservationDockAndCarry(bool customProfile)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add();
        var motion = f.Rig.Robot.Motion;
        var profile = customProfile
            ? PathMotionProfile.Default with { IsCustom = true, DockSpeedMmps = 47f, DockAccelMmps2 = 125f, DockDecelMmps2 = 260f }
            : PathMotionProfile.Default;
        var task = customProfile
            ? motion.QueueAiPickupCubeAsync(7, false, profile, CancellationToken.None)
            : motion.QueueAiPickupCubeAsync(7, false);
        Assert.Equal(customProfile, motion.Path.Field48);
        var action=Assert.IsType<NativePickupObjectRunner>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal((0x15,7u,false),(action.Type,action.RequiredTrackMask,action.CheckPreActionPose));
        bool dock=false;uint image=0;
        try
        {
            for(int i=0;i<120&&!action.DestructionObserved;i++)
            {
                f.Motors();
                if(!dock && f.Rig.SentSince(0).OfType<DockWithObject>().Any())
                {
                    var request=Assert.Single(f.Rig.SentSince(0).OfType<DockWithObject>());
                    Assert.Equal((byte)2,request.DockingMethod);Assert.True(request.Field8);
                    Assert.Equal((profile.DockSpeedMmps, profile.DockAccelMmps2, profile.DockDecelMmps2),
                        (request.SpeedMmps, request.AccelMmps2, request.DecelMmps2));
                    f.Motors(RobotStatusFlag.IsPickingOrPlacing);
                    f.Rig.Data(new LiftLoad{Field0=true});
                    f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=2});
                    f.Motors();dock=true;
                }
                if(!dock)f.Image(++image,action.Prepared?.Children.OfType<NativeTurnTowardsObjectRunner>().Any(c=>c.Initialized)==true);
            }
            Assert.True(dock);
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);Assert.Equal(0u,result.EngineResult);
            Assert.Equal(7u,f.Robot.CarryingOwner!.CarriedObjectId);
            Assert.False(motion.Path.Field48);
        }
        finally { f.Robot.ActionList.Clear(); }
    }

    [Fact]
    public async Task DriveBranchRunsRealPathAndPickupWithProxyCompletion()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations:true);
        f.Add(x:300);
        var task=f.Rig.Robot.Motion.QueueAiPickupCubeAsync(7,true);
        var compound=Assert.IsType<NativeDriveToPickupBeaconRunner>(f.Robot.ActionList.GetCurrentAction());
        var drive=Assert.IsType<NativeDriveToObjectRunner>(compound.Drive);
        var paths=new HashSet<ushort>();ExecutePath? pending=null;int mark=0;uint frame=0;bool dock=false;
        try
        {
            for(int i=0;i<350&&!compound.DestructionObserved;i++)
            {
                if(f.M.GetNativePlanners().Lattice is {} planner && planner.CheckPlanningStatus==1)
                    Assert.True(SpinWait.SpinUntil(()=>planner.CheckPlanningStatus!=1,TimeSpan.FromSeconds(10)));
                f.Motors();
                if(pending is {} complete && drive.Drive?.Goals is {Count:>0} goals)
                {
                    using var goal=goals[drive.Drive.GoalIndex].GetWithRespectToRoot();
                    f.X=(float)goal.Translation.X;f.Y=(float)goal.Translation.Y;f.Yaw=(float)goal.AngleAroundZ;f.Motors();
                    f.Rig.Data(new PathFollowingEvent{EventId=complete.EventId,EventType=(byte)PathEventType.Completed});pending=null;f.Motors();
                }
                foreach(var message in f.Rig.SentSince(mark))
                    if(message is ExecutePath execute && paths.Add(execute.EventId))
                    {f.Rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=(byte)PathEventType.Started});pending=execute;}
                mark=f.Rig.Mark();
                if(!dock && f.Rig.SentSince(0).OfType<DockWithObject>().Any())
                {
                    f.Motors(RobotStatusFlag.IsPickingOrPlacing);f.Rig.Data(new LiftLoad{Field0=true});
                    f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=2});f.Motors();dock=true;
                }
                if(!dock)
                {
                    bool visible=drive.Sequence.Children.OfType<NativeTurnTowardsObjectRunner>().Any(c=>c.Initialized) ||
                        compound.Pickup.Prepared?.Children.OfType<NativeTurnTowardsObjectRunner>().Any(c=>c.Initialized)==true;
                    f.Image(++frame,visible,cubeX:300);
                }
            }
            Assert.NotEmpty(paths);Assert.True(dock);
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);Assert.Equal(0u,result.EngineResult);
            Assert.Equal(7u,f.Robot.CarryingOwner!.CarriedObjectId);
        }
        finally {f.Robot.ActionList.Clear();}
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingCancellationAddressesTheExactRequestedBranch(bool drive)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        f.Add();
        var head=f.Rig.Robot.Motion.SetHeadAngleAsync(.5f);f.Rig.Tick();
        using var cancel=new CancellationTokenSource();
        var request=f.Rig.Robot.Motion.QueueAiPickupCubeAsync(7,drive,cancel.Token);
        cancel.Cancel();f.Rig.Tick();
        var result=await request.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);Assert.Equal(0x02000001u,result.EngineResult);
        Assert.False(head.IsCompleted);
        f.Robot.ActionList.Clear();
    }

    [Fact]
    public async Task MissingTargetCompletesLocallyWithoutPackingUndefinedPickupIds()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty: true);
        var logs = new List<string>();
        var sourceLogs = new List<EngineLogEntry>();
        var app = new List<ActionCompletionReport>();
        var local = new List<RobotCompletedAction>();
        f.Rig.Engine.StackLogLine += logs.Add;
        f.Rig.Engine.LogEntry += sourceLogs.Add;
        f.Rig.Engine.ActionCompleted += app.Add;
        using var subscription = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A, true, value =>
        {
            if (value is RobotCompletedAction completed)
                local.Add(completed);
        });

        var task = f.Rig.Robot.Motion.QueueAiPickupCubeAsync(7, false);
        var runner = Assert.IsType<NativePickupObjectRunner>(f.Robot.ActionList.GetCurrentAction());
        f.Motors();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        f.Rig.Engine.DrainAppDelivery();

        Assert.True(result.Admitted);
        Assert.Equal(0x03000004u, result.EngineResult);
        var completion = Assert.Single(local, record => record.Tag == runner.Tag);
        Assert.Equal(0x03000004u, completion.Result);
        Assert.Equal(((byte)0, false, false), (completion.ObjectInteraction!.Count, completion.ObjectInteraction.Flag,
            completion.ObjectInteraction.IsPayloadDefined));
        Assert.DoesNotContain(app, record => record.Tag == runner.Tag);
        Assert.Contains(sourceLogs, entry => entry.Name == "PickupObjectAction.EmitCompletionSignal.DockActionNotSet" &&
            entry.Text.EndsWith("Dock action not set before filling completion signal", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("M1-034 PickupObject 0x005537D4..0x00553958") &&
            line.EndsWith("skipped the undefined public 5A payload."));
        Assert.DoesNotContain(logs, line => line.Contains("MISSING: Concrete object-interaction completion fields unavailable"));
    }

    [Fact]
    public async Task RejectsNegativeSignedWorldIdBeforeAdmission()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var result=await f.Rig.Robot.Motion.QueueAiPickupCubeAsync(uint.MaxValue,false);
        Assert.False(result.Admitted);Assert.Null(result.EngineResult);
    }

    [Fact]
    public async Task ReentrantCancellationDuringPreparedHeadSendDoesNotStartLaterChildren()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        f.Add();
        var task=f.Rig.Robot.Motion.QueueAiPickupCubeAsync(7,false);
        var runner=Assert.IsType<NativePickupObjectRunner>(f.Robot.ActionList.GetCurrentAction());
        bool cancelled=false;int mark=-1;
        f.Robot.SendFault=message=>
        {
            if(!cancelled && message is SetHeadAngle)
            {
                cancelled=true;f.Robot.ActionList.Cancel(runner.Tag);mark=f.Rig.Mark();
            }
            return null;
        };
        try
        {
            for(int i=0;i<40&&!cancelled;i++)f.Motors();
            Assert.True(cancelled);
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.Admitted);Assert.NotEqual(0u,result.EngineResult);
            Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is SetLiftHeight or DockWithObject);
        }
        finally {f.Robot.SendFault=null;f.Robot.ActionList.Clear();}
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CarryAttachmentReentryCannotMutateTheReplacementOwner(bool onChanged)
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var cube=f.Add();var owner=f.Robot;var carrying=owner.CarryingOwner!;var actions=owner.ActionList;
        var task=f.Rig.Robot.Motion.QueueAiPickupCubeAsync(7,false);
        var runner=Assert.IsType<NativePickupObjectRunner>(owner.ActionList.GetCurrentAction());
        bool replaced=false,dock=false;uint image=0;int mark=-1;
        void Replace()
        {
            if(replaced)return;
            replaced=true;f.Rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);
            f.Rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);mark=f.Rig.Mark();
        }
        void Changed(uint? _) { if(onChanged)Replace(); }
        carrying.Changed+=Changed;
        owner.SendFault=message=>{if(!onChanged && message is CarryStateUpdate)Replace();return null;};
        try
        {
            for(int i=0;i<120&&!replaced&&!runner.DestructionObserved;i++)
            {
                f.Motors();
                if(!dock && f.Rig.SentSince(0).OfType<DockWithObject>().Any())
                {
                    f.Motors(RobotStatusFlag.IsPickingOrPlacing);f.Rig.Data(new LiftLoad{Field0=true});
                    f.Rig.Data(new PickAndPlaceResult{Field1=true,Field3=2});dock=true;
                }
                if(!dock)f.Image(++image,runner.Prepared?.Children.OfType<NativeTurnTowardsObjectRunner>().Any(c=>c.Initialized)==true);
            }
            Assert.True(replaced);
            var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(result.OwnerRetired);
            Assert.Null(f.Robot.CarryingOwner!.CarriedObjectId);
            Assert.NotSame(f.Robot.LiftPose.Node,cube.NativePose.Node?.Parent);
            Assert.DoesNotContain(f.Rig.SentSince(mark),m=>m is DockWithObject or CarryStateUpdate);
        }
        finally {owner.SendFault=null;carrying.Changed-=Changed;actions.Clear();}
    }
}
