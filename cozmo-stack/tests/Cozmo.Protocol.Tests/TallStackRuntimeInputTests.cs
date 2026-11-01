using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class TallStackRuntimeInputTests
{
    private static void Publish(PickupPyramidNativeExecutionTests.Fixture f)
    { f.Runtime.Configurations.ForceUpdate(); f.Runtime.Configurations.Update(); }
    private static void Move(PickupPyramidNativeExecutionTests.Fixture f, ObservableObject obj, float x, float z)
    {
        obj.Pose=new Pose3d(Rotation3d.Identity,new Vec3(x,0,z),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        f.Runtime.Configurations.SetObjectPoseChanged(obj.ObjectId,PoseState.Known); Publish(f);
    }
    private static TallStackLease? Lock(ITallStackWeakTarget weak)
    { Assert.True(weak.TryLock(out var lease)); return lease; }

    [Fact]
    public void CanonicalConfiguredOwnerUsesRealPublisherAndFirstRecipient()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Robot.Progression!.Unlocked.Remove(5);
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var body=Assert.IsType<NativeCantHandleTallStackBehavior>(f.Runtime.Bound["CantHandleTallStack"]);
        Assert.Same(f.Robot.TallStackContainers[0],body.State);
        var empty=body.EvaluateHostReadiness(f.Runtime.Context);
        Assert.True(empty.Available); Assert.False(empty.Runnable);
        f.Add(7,100);f.Add(8,100,66);f.Add(9,100,110);Publish(f);
        Assert.True(body.IsRunnable(f.Runtime.Context));
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(body.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.True(body.EngineRunning); Assert.NotNull(body.LookingSequence);
        Assert.True(body.State!.HasStoredPose); Assert.NotNull(body.State.Target);
        Assert.False(f.Robot.TallStackContainers[1].HasStoredPose);
        f.Robot.TallStackContainers[1].HasStoredPose=true;
        var observed=new List<string>();
        f.Rig.Engine.DelocalizationRecipientObservedForTest=name=>
        {
            observed.Add(name);
            if(name=="CantHandleTallStack[0]") { Assert.True(body.State.HasStoredPose); Assert.NotNull(body.State.Target); }
            if(name=="CantHandleTallStack[1]") { Assert.False(body.State.HasStoredPose); Assert.Null(body.State.Target); }
        };
        try { f.Robot.Delocalize(false); }
        finally { f.Rig.Engine.DelocalizationRecipientObservedForTest=null; }
        Assert.True(observed.IndexOf("BlockConfigurationManager") < observed.IndexOf("CantHandleTallStack[0]"));
        Assert.True(observed.IndexOf("CantHandleTallStack[0]") < observed.IndexOf("CantHandleTallStack[1]"));
        Assert.False(body.State.HasStoredPose); Assert.False(f.Robot.TallStackContainers[1].HasStoredPose);
        Assert.NotNull(body.OwnedRunner); Assert.True(body.EngineRunning);
    }

    [Fact]
    public void RebuildPreservesEqualIdentityButRemovalExpiresEvenWithHostReferences()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(7,100);var middle=f.Add(8,100,66);f.Add(9,100,110);Publish(f);
        var oldSnapshot=f.Runtime.Configurations.Snapshot;
        var stack=Assert.Single(oldSnapshot.Stacks);var view=stack.View;
        var weak=f.Runtime.TallStackWeakInput!()!;
        using(var before=Lock(weak)) Assert.NotNull(before);
        Publish(f);Assert.Same(stack,Assert.Single(f.Runtime.Configurations.Snapshot.Stacks));
        Move(f,middle,300,22);
        Assert.Null(Lock(weak));
        Assert.NotNull(oldSnapshot);Assert.NotNull(view);
        Move(f,middle,100,66);
        Assert.NotSame(stack,Assert.Single(f.Runtime.Configurations.Snapshot.Stacks));
        Assert.Null(Lock(weak));
        using var again=Lock(f.Runtime.TallStackWeakInput!()!);Assert.NotNull(again);
    }

    // Mechanism test: artificial temporary native strong lease, libc lock/release T8-T9.
    [Fact]
    public void ExplicitLeaseKeepsRemovedTargetLiveUntilReleased()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(7,100);var middle=f.Add(8,100,66);f.Add(9,100,110);Publish(f);
        var weak=f.Runtime.TallStackWeakInput!()!;var held=Lock(weak);Assert.NotNull(held);
        Move(f,middle,300,22);
        using(var concurrent=Lock(weak)) Assert.NotNull(concurrent);
        held!.Dispose();Assert.Null(Lock(weak));
    }

    [Fact]
    public void EqualTallestUsesFirstPublishedStack()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(7,100);f.Add(8,100,66);f.Add(9,100,110);
        f.Add(10,400);f.Add(11,400,66);f.Add(12,400,110);Publish(f);
        var stacks=f.Runtime.Configurations.Snapshot.Stacks;
        Assert.Equal(2,stacks.Count);
        using var selected=Lock(f.Runtime.TallStackWeakInput!()!);
        Assert.Equal(new uint[]{7,10},stacks.Select(s=>s.Bottom));
        Assert.NotNull(selected);Assert.Equal(7u,selected!.Value.Bottom);
    }

    [Fact]
    public void RetiringManagerExpiresTargetDespiteRetainedSnapshot()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Add(7,100);f.Add(8,100,66);f.Add(9,100,110);Publish(f);
        var snapshot=f.Runtime.Configurations.Snapshot;
        var raw=Assert.Single(snapshot.Stacks).WeakTarget;
        f.Runtime.Configurations.Dispose();
        Assert.Null(Lock(raw));Assert.Throws<NativeConfigurationUnavailable>(()=>f.Runtime.TallStackWeakInput!());
        Assert.NotEmpty(snapshot.Stacks);
    }

    [Fact]
    public void ReentrantUpdateDuringGeometryDiagnosticDefersRebuild()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        for(uint i=0;i<11;i++)f.Add(7+i,100,22+44*i);
        Publish(f);var manager=f.Runtime.Configurations;
        var weak=manager.Tallest()!.WeakTarget;
        bool entered=false;
        using(PoseDiagnostics.Use((_,_,key,_)=>
        {
            if(key!="LoopBoundOverflow" || entered)return;
            entered=true;manager.ForceUpdate();manager.Update();
        })) Publish(f);
        Assert.True(entered);Assert.True(f.Robot.BlockConfigurationForceUpdate);
        using(var held=Lock(weak))Assert.NotNull(held);
        manager.Update();Assert.False(f.Robot.BlockConfigurationForceUpdate);
    }

    [Fact]
    public void SavedTargetExpiresBeforeInitAndUnavailableOwnerIsNotEmpty()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        f.Robot.Progression!.Unlocked.Remove(5);
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        f.Add(7,100);var middle=f.Add(8,100,66);f.Add(9,100,110);Publish(f);
        var body=Assert.IsType<NativeCantHandleTallStackBehavior>(f.Runtime.Bound["CantHandleTallStack"]);
        Assert.True(body.IsRunnable(f.Runtime.Context));Move(f,middle,300,22);
        using var scope=new BehaviorScope();body.StartAsync(f.Runtime.Context,scope,default).GetAwaiter().GetResult();
        Assert.True(body.InitFailed);Assert.Null(body.OwnedRunner);
        var actual=f.Runtime.Configurations;
        try { f.M.World.NativeConfigurations=null;var result=body.EvaluateHostReadiness(f.Runtime.Context);Assert.False(result.Available); }
        finally { f.M.World.NativeConfigurations=actual; }
    }
}
