using Cozmo.Robot.Behavior;
using Cozmo.Robot;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeFireTruckAlarmTests
{
    [Fact]
    public void ExplicitStartHandsOffBeforeEmptyReportAndStopDoesNotClearFlag()
    {
        using var f=new Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var truck=Assert.IsType<NativeFireTruckAlarmBehavior>(f.Runtime.Bound["SparksFireTruckAlarm"]);
        Assert.Same(truck,f.Robot.BehaviorManagerOwner!.Find(truck.Id));
        var selection=Assert.IsType<SelectionChooser>(f.Runtime.Tree.Single(a=>a.Id=="Selection").Chooser);
        Assert.Same(truck,selection.FindBehaviorByExecutableType!(2));
        Assert.True(f.Robot.Progression!.TryIsUnlocked(83,out bool unlocked));Assert.False(unlocked);
        Assert.False(truck.EvaluateHostReadiness(f.Runtime.Context).Runnable);
        int reports=0,objectives=0;long needsEpoch=f.Rig.Engine.Needs.NativePendingHistoryEpoch;
        using var objective=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8B,true,_=>objectives++);
        using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8F,true,_=>{Assert.True(f.Robot.NativeDoingCSharpTrick);reports++;});
        var raw=new List<byte[]>();f.Rig.Engine.EngineToGameMessage+=(tag,body)=>{if(tag==0x8F)raw.Add(body.ToArray());};
        Assert.True(f.Robot.BehaviorManagerOwner.StartAsync(truck.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.True(f.Robot.NativeDoingCSharpTrick);Assert.Equal(1,reports);Assert.Null(truck.OwnedRunner);
        f.Rig.Engine.DrainAppDelivery();Assert.Empty(Assert.Single(raw));
        Assert.False(truck.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF*1000));
        f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Completed,f.Rig.Engine.Timer.SecondsF);
        Assert.True(f.Robot.NativeDoingCSharpTrick);Assert.Equal(1,reports);
        f.Rig.Engine.Timer.UpdateTime(181_000_000_000);truck.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF*1000);
        Assert.True(f.Robot.NativeDoingCSharpTrick);
        Assert.Equal(0,objectives);Assert.Equal(needsEpoch,f.Rig.Engine.Needs.NativePendingHistoryEpoch);
        truck.StopTrickForTest();Assert.False(f.Robot.NativeDoingCSharpTrick);
    }
    [Fact]
    public void BusyInitAndResumeRefuseUntilSourceUnitStopTrick()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        // Supplied actual NV recipient input, not a natural unlock claim.
        var nv=new byte[2048];System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(nv,83);
        f.Robot.Progression!.ReadSink(new NvResult(0,nv));
        var truck=Assert.IsType<NativeFireTruckAlarmBehavior>(f.Runtime.Bound["SparksFireTruckAlarm"]);
        int reports=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8F,true,_=>reports++);
        using var scope=new BehaviorScope();truck.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        Assert.True(f.Robot.NativeDoingCSharpTrick);Assert.Equal(1,reports);
        truck.Stop(BehaviorStopReason.Interrupted);
        Assert.True(truck.Resume(null,f.Rig.Engine.Timer.SecondsF));Assert.Equal(1,reports);
        using var second=new BehaviorScope();truck.StartAsync(f.Runtime.Context,second,default).GetAwaiter().GetResult();
        Assert.True(truck.InitFailed);Assert.Equal(1,reports);
        truck.StopTrickForTest();Assert.False(f.Robot.NativeDoingCSharpTrick);
        truck.AttachRun(f.Runtime.Context,scope);
        Assert.False(truck.Resume(null,f.Rig.Engine.Timer.SecondsF));Assert.True(f.Robot.NativeDoingCSharpTrick);Assert.Equal(2,reports);
    }
    [Fact]
    public void ReplacementDoesNotInheritOldTrickFlagOrAcceptOldHelper()
    {
        using var f=new Fixture();var original=f.Robot;
        var truck=Assert.IsType<NativeFireTruckAlarmBehavior>(f.Runtime.Bound["SparksFireTruckAlarm"]);
        using var scope=new BehaviorScope();truck.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        Assert.True(original.NativeDoingCSharpTrick);
        f.Rig.Engine.Robots.RemoveRobot(1,false);var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
        Assert.False(replacement.NativeDoingCSharpTrick);truck.StopTrickForTest();Assert.False(replacement.NativeDoingCSharpTrick);
        Assert.NotSame(original,replacement);
    }
    [Fact]
    public void ConstructorCapturesSuppliedOriginalWithoutGlobalRobot()
    {
        // Constructor seam only; the removed owner is not admitted or treated as live.
        using var f=new Fixture();var original=f.Robot;
        var config=f.Rig.Engine.HostBehaviorMap![BehaviorID.SparksFireTruckAlarm];
        f.Rig.Engine.Robots.RemoveRobot(1,false);Assert.Null(f.Rig.Engine.Robot);
        using var constructed=new NativeFireTruckAlarmBehavior(new BehaviorFactoryContext{NativeRobot=original},config);
        Assert.Same(original,constructed.OriginalRobot);
    }
    [Fact]
    public void MissingContextInterfaceStillSetsFlagWithoutReport()
    {
        using var f=new Fixture();f.Rig.Engine.NeedsContextExternalInterfaceAvailable=false;
        var truck=Assert.IsType<NativeFireTruckAlarmBehavior>(f.Runtime.Bound["SparksFireTruckAlarm"]);
        int reports=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8F,true,_=>reports++);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(truck.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.True(f.Robot.NativeDoingCSharpTrick);Assert.Equal(0,reports);
    }
    [Fact]
    public void SuppliedUnlockedNvInputNeedsNoAnimationLibrary()
    {
        using var f=new Fixture();
        Assert.Null(f.Rig.Robot.Animations.Library);
        var nv=new byte[2048];System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(nv,83);
        f.Robot.Progression!.ReadSink(new NvResult(0,nv));
        var truck=Assert.IsType<NativeFireTruckAlarmBehavior>(f.Runtime.Bound["SparksFireTruckAlarm"]);
        var ready=truck.EvaluateHostReadiness(f.Runtime.Context);
        Assert.True(ready.Available);Assert.True(ready.Runnable);
    }
    [Fact]
    public void ReentrantStopDuringLogDoesNotStartTrickOrBroadcast()
    {
        using var f=new Fixture();
        var truck=Assert.IsType<NativeFireTruckAlarmBehavior>(f.Runtime.Bound["SparksFireTruckAlarm"]);
        int reports=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8F,true,_=>reports++);
        truck.Step+=_=>truck.Stop(BehaviorStopReason.Interrupted);
        using var scope=new BehaviorScope();truck.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        Assert.False(f.Robot.NativeDoingCSharpTrick);Assert.Equal(0,reports);Assert.Null(truck.OwnedRunner);
    }
    [Fact]
    public void ReentrantReplacementDuringLogDoesNotChangeNewRobotOrBroadcast()
    {
        using var f=new Fixture();var original=f.Robot;EngineRobot? replacement=null;
        var truck=Assert.IsType<NativeFireTruckAlarmBehavior>(f.Runtime.Bound["SparksFireTruckAlarm"]);
        int reports=0;using var sub=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x8F,true,_=>reports++);
        truck.Step+=_=>{f.Rig.Engine.Robots.RemoveRobot(1,false);replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false);};
        using var scope=new BehaviorScope();truck.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        Assert.NotNull(replacement);Assert.False(original.NativeDoingCSharpTrick);Assert.False(replacement!.NativeDoingCSharpTrick);
        Assert.Equal(0,reports);Assert.Null(truck.OwnedRunner);
    }
}
