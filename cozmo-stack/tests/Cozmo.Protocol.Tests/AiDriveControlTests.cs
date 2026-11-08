using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class AiDriveControlTests
{
    [Fact]
    public async Task DefaultRequestPassesAnimationFlagToNativeRunner()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        using var cancel=new CancellationTokenSource();
        var task=f.Rig.Robot.Motion.QueueAiDriveStraightAsync(30,20,cancellationToken:cancel.Token);
        var action=Assert.IsType<DriveStraightAction>(f.Robot.ActionList.GetCurrentAction());
        Assert.True(action.ShouldPlayAnimation);
        cancel.Cancel();f.Rig.Tick();
        Assert.True((await task.WaitAsync(TimeSpan.FromSeconds(2))).Admitted);
    }

    [Fact]
    public async Task ConstructorTagCollisionRetiresUnqueuedDriveWithoutSharedCleanup()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var rig=f.Rig;var owner=f.Robot;
        bool previousError=Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        var active=new DriveStraightAction(f.M,owner,100,20,false);
        owner.ActionList.QueueAction(QueueActionPosition.Now,active);rig.Tick();
        Assert.True(rig.Robot.Motion.Path.IsActive);
        uint reserved=0;
        for(uint tag=1;tag<=1_000_000;tag++)if(!ActionRunnerTagCounter.Global.IsInUse(tag)){reserved=tag;break;}
        Assert.NotEqual(0u,reserved);
        bool fired=false;
        void Observe(EngineLogEntry entry)
        {
            if(fired || entry.Name!="DriveStraightAction.Constructor.NegativeSpeed")return;
            fired=true;Assert.True(ActionRunnerTagCounter.Global.TryReserve(reserved));
        }
        rig.Engine.LogEntry+=Observe;int mark=rig.Mark();
        try
        {
            var result=await rig.Robot.Motion.QueueAiDriveStraightAsync(100,-20,false);
            Assert.True(fired);Assert.False(result.Admitted);Assert.Null(result.EngineResult);
            Assert.DoesNotContain(rig.SentSince(mark),m=>m is ClearPath or AppendPathSegmentLine or ExecutePath);
            Assert.Same(active,owner.ActionList.GetCurrentAction());
            Assert.True(rig.Robot.Motion.Path.IsActive);
        }
        finally{rig.Engine.LogEntry-=Observe;if(fired)ActionRunnerTagCounter.Global.Release(reserved);owner.ActionList.Cancel(active.Tag);Cozmo.Transport.EngineErrorState.ErrorFlagSet=previousError;}
    }

    [Fact]
    public async Task UnreadyRobotRefusesBeforeAdmission()
    {
        using var rig=new M4ControlTests.Rig();
        int mark=rig.Mark();
        var result=await rig.Robot.Motion.QueueAiDriveStraightAsync(100,20,false);
        Assert.False(result.Admitted);
        Assert.Null(result.EngineResult);
        Assert.Empty(rig.SentSince(mark).OfType<ClearPath>());
    }

    [Fact]
    public async Task ConstructorWarningReadinessLossRefusesWithoutCallingOwnerRetired()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var rig=f.Rig;var owner=f.Robot;bool fired=false;int mark=rig.Mark();
        void Observe(EngineLogEntry entry)
        {
            if(fired || entry.Name!="DriveStraightAction.Constructor.NegativeSpeed")return;
            fired=true;owner.FirstFullStateHandled=false;
        }
        rig.Engine.LogEntry+=Observe;
        try
        {
            var result=await rig.Robot.Motion.QueueAiDriveStraightAsync(100,-20,false);
            Assert.True(fired);Assert.False(result.Admitted);Assert.False(result.OwnerRetired);
            Assert.DoesNotContain(rig.SentSince(mark),m=>m is ClearPath or AppendPathSegmentLine or ExecutePath);
        }
        finally{owner.FirstFullStateHandled=true;rig.Engine.LogEntry-=Observe;}
    }

    [Fact]
    public async Task ConstructorWarningReplacementRetiresWithoutNativeCleanup()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var rig=f.Rig;var original=f.Robot;bool replaced=false;int after=-1;
        void Observe(EngineLogEntry entry)
        {
            if(replaced || entry.Name!="DriveStraightAction.Constructor.NegativeSpeed")return;
            replaced=true;
            rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);
            rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);
            after=rig.Mark();
        }
        rig.Engine.LogEntry+=Observe;
        try
        {
            var result=await rig.Robot.Motion.QueueAiDriveStraightAsync(100,-20,false);
            Assert.True(replaced);Assert.NotSame(original,rig.Engine.Robot);
            Assert.False(result.Admitted);Assert.True(result.OwnerRetired);
            Assert.DoesNotContain(rig.SentSince(after),m=>m is ClearPath or AppendPathSegmentLine or ExecutePath);
        }
        finally{rig.Engine.LogEntry-=Observe;}
    }

    [Fact]
    public async Task PublicDriveUsesNativePathAndCompletion()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var rig=f.Rig;var owner=f.Robot;
        var local=new List<RobotCompletedAction>();var typed=new List<ActionCompletionReport>();var raw=new List<byte[]>();
        using var sub=rig.Engine.AnimationMessages.SubscribeMessage(0x5A,true,p=>{if(p is RobotCompletedAction r)local.Add(r);});
        rig.Engine.ActionCompleted+=typed.Add;
        void Raw(ushort tag,ReadOnlyMemory<byte> body){if(tag==0x5A)raw.Add(body.ToArray());}
        rig.Engine.EngineToGameMessage+=Raw;
        int mark=rig.Mark();
        var head=rig.Robot.Motion.SetHeadAngleAsync(.5f);
        rig.Tick();
        var first=owner.ActionList.GetCurrentAction();
        var task=rig.Robot.Motion.QueueAiDriveStraightAsync(96,25,false);
        Assert.Same(first,owner.ActionList.GetCurrentAction());
        Assert.False(head.IsCompleted);
        Assert.Empty(rig.SentSince(mark).OfType<AppendPathSegmentLine>());
        owner.ActionList.Cancel(first!.Tag);
        var action=Assert.IsType<DriveStraightAction>(owner.ActionList.GetCurrentAction());
        Assert.InRange(action.Tag,1u,1_000_000u);
        rig.Tick();
        var sent=rig.SentSince(mark);
        var line=Assert.Single(sent.OfType<AppendPathSegmentLine>());
        Assert.Equal(25f,line.Speed.SpeedMmps);
        var execute=Assert.Single(sent.OfType<ExecutePath>());
        Assert.False(execute.Unknown);
        Assert.True(sent.FindIndex(m=>m is ClearPath)<sent.IndexOf(line));
        Assert.True(sent.IndexOf(line)<sent.IndexOf(execute));
        rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=0});rig.Tick();
        Assert.False(task.IsCompleted);
        rig.Data(new PathFollowingEvent{EventId=execute.EventId,EventType=2});rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);Assert.Equal(0u,result.EngineResult);
        var report=Assert.Single(local.Where(x=>x.Tag==action.Tag));
        Assert.Equal((byte)6,report.CompletionVariant);
        rig.Engine.DrainAppDelivery();
        Assert.Equal((byte)6,Assert.Single(typed.Where(x=>x.Tag==action.Tag)).CompletionVariant);
        Assert.Contains(raw,body=>body.Length>4 && BitConverter.ToUInt32(body,0)==action.Tag && body[^1]==6);
        rig.Engine.EngineToGameMessage-=Raw;
    }

    [Fact]
    public async Task PendingDriveCancellationClearsTheOriginalSharedPath()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;var rig=f.Rig;
        var active=new DriveStraightAction(f.M,owner,100,20,false);
        owner.ActionList.QueueAction(QueueActionPosition.Now,active);rig.Tick();
        Assert.True(rig.Robot.Motion.Path.IsActive);
        using var cancel=new CancellationTokenSource();
        var task=rig.Robot.Motion.QueueAiDriveStraightAsync(50,20,false,cancel.Token);
        int mark=rig.Mark();cancel.Cancel();rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);Assert.Equal(0x02000001u,result.EngineResult);
        Assert.Contains(rig.SentSince(mark),m=>m is ClearPath);
        Assert.DoesNotContain(rig.SentSince(mark),m=>m is AppendPathSegmentLine);
        owner.ActionList.Cancel(active.Tag);rig.Tick();
    }

    [Fact]
    public async Task ReactionDiscardRunsNativeSharedCleanupButReportsNotAdmitted()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;var rig=f.Rig;
        var active=new DriveStraightAction(f.M,owner,100,20,false);
        owner.ActionList.QueueAction(QueueActionPosition.Now,active);rig.Tick();
        Assert.True(rig.Robot.Motion.Path.IsActive);
        owner.ActionList.ExternalActionsDisabled=true;
        int mark=rig.Mark();
        var result=await rig.Robot.Motion.QueueAiDriveStraightAsync(30,20,false);
        Assert.False(result.Admitted);
        Assert.Contains(rig.SentSince(mark),m=>m is ClearPath);
        Assert.Same(active,owner.ActionList.GetCurrentAction());
        owner.ActionList.ExternalActionsDisabled=false;
        owner.ActionList.Cancel(active.Tag);rig.Tick();
    }

    [Fact]
    public async Task ReactionDiscardStillCleansSharedPathWhenReadinessChangesInItsLog()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;var rig=f.Rig;bool changed=false;
        var active=new DriveStraightAction(f.M,owner,100,20,false);
        owner.ActionList.QueueAction(QueueActionPosition.Now,active);rig.Tick();
        Assert.True(rig.Robot.Motion.Path.IsActive);
        owner.ActionList.ExternalActionsDisabled=true;
        void Observe(EngineLogEntry entry)
        {
            if(changed || entry.Name!="ActionQueue.QueueAction.ExternalActionsDisabled")return;
            changed=true;owner.FirstFullStateHandled=false;
        }
        rig.Engine.LogEntry+=Observe;int mark=rig.Mark();
        try
        {
            var result=await rig.Robot.Motion.QueueAiDriveStraightAsync(30,20,false);
            Assert.True(changed);Assert.False(result.Admitted);Assert.False(result.OwnerRetired);
            Assert.Contains(rig.SentSince(mark),m=>m is ClearPath);
        }
        finally
        {
            rig.Engine.LogEntry-=Observe;owner.FirstFullStateHandled=true;
            owner.ActionList.ExternalActionsDisabled=false;owner.ActionList.Cancel(active.Tag);
        }
    }

    [Fact]
    public async Task ReactionDiscardReplacementReportsRetiredAndDoesNotTouchSuccessor()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var owner=f.Robot;var rig=f.Rig;bool replaced=false;int after=-1;
        var active=new DriveStraightAction(f.M,owner,100,20,false);
        owner.ActionList.QueueAction(QueueActionPosition.Now,active);rig.Tick();
        owner.ActionList.ExternalActionsDisabled=true;
        void Observe(EngineLogEntry entry)
        {
            if(replaced||entry.Name!="PathComponent.Abort")return;
            replaced=true;rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);
            rig.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false);after=rig.Mark();
        }
        rig.Engine.LogEntry+=Observe;
        try
        {
            var result=await rig.Robot.Motion.QueueAiDriveStraightAsync(30,20,false);
            Assert.True(replaced);Assert.False(result.Admitted);Assert.True(result.OwnerRetired);
            Assert.DoesNotContain(rig.SentSince(after),m=>m is ClearPath or AppendPathSegmentLine or ExecutePath);
        }
        finally{rig.Engine.LogEntry-=Observe;}
    }

    [Fact]
    public async Task ActiveDriveCancellationAndHostRefusalsAreDistinct()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture(sourceNvEmpty:true);
        var rig=f.Rig;var owner=f.Robot;
        Assert.False((await rig.Robot.Motion.QueueAiDriveStraightAsync(float.NaN,20)).Admitted);
        using var already=new CancellationTokenSource();already.Cancel();
        Assert.False((await rig.Robot.Motion.QueueAiDriveStraightAsync(10,20,false,already.Token)).Admitted);
        using var cancel=new CancellationTokenSource();
        var task=rig.Robot.Motion.QueueAiDriveStraightAsync(100,20,false,cancel.Token);rig.Tick();
        int mark=rig.Mark();cancel.Cancel();rig.Tick();
        var result=await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Admitted);Assert.Equal(0x02000000u,result.EngineResult);
        Assert.Contains(rig.SentSince(mark),m=>m is ClearPath);
        owner.ActionList.ExternalActionsDisabled=true;
        var refused=await rig.Robot.Motion.QueueAiDriveStraightAsync(10,20,false);
        Assert.False(refused.Admitted);
        owner.ActionList.ExternalActionsDisabled=false;
    }
}
