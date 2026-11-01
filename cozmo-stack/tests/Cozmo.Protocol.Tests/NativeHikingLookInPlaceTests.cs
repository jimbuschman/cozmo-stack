using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeHikingLookInPlaceTests
{
    [Fact]
    public void ChangedParametersAndMissingActualMapRemainUnavailable()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var scan=Assert.IsType<NativeHikingLookInPlace>(f.Runtime.Bound["Hiking_LookInPlaceForUnknown"]);Assert.True(scan.ParametersAvailable);
        var config=JsonNode.Parse(f.Rig.Engine.HostBehaviorMap![BehaviorID.Hiking_LookInPlaceForUnknown].GetRawText(),documentOptions:new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true})!;
        config["params"]!["prioritySectorCount"]=4;using var changed=JsonDocument.Parse(config.ToJsonString());
        using var other=new NativeHikingLookInPlace(new BehaviorFactoryContext{Manipulation=f.M,NativeRobot=f.Robot},changed.RootElement);Assert.False(other.ParametersAvailable);
        uint origin=f.M.World.Maps.CurrentOrigin;var map=f.M.World.Maps.Current!;f.M.World.Maps.Maps.Remove(origin);
        try {Assert.False(scan.EvaluateHostReadiness(f.Runtime.Context).Available);Assert.Null(scan.OwnedRunner);}
        finally {f.M.World.Maps.Maps[origin]=map;}
    }
    [Fact]
    public void RealUnknownMapConstructsNativeCompoundWithFourUpfrontDraws()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var scan=Assert.IsType<NativeHikingLookInPlace>(f.Runtime.Bound["Hiking_LookInPlaceForUnknown"]);
        var map=Assert.IsType<MemoryMap>(f.M.World.Maps.Current);Assert.True(map.HasCollisionRayWithTypes(new Vec2(36.95518,-15.30734),new Vec2(175.5371,-72.7098),t=>t==MemoryMapContentType.Unknown));
        int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>++words==6?0x80000000u:0u;
        try
        {
            Assert.True(scan.IsRunnable(f.Runtime.Context));Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Assert.Equal(8,words);Assert.Equal((byte)2,scan.Sectors[0]);
            var ray=scan.LastRay!.Value;Assert.Equal(36.95518,ray.From.X,4);Assert.Equal(-15.30734,ray.From.Y,4);Assert.Equal(175.5371,ray.To.X,3);Assert.Equal(-72.7098,ray.To.Y,3);
            var sequence=Assert.IsType<CompoundActionSequential>(scan.OwnedRunner);Assert.Equal(4,sequence.Children.Count);
            var first=Assert.IsType<NativePanTiltRunner>(sequence.Children[0]);var lift=sequence.Children[1];
            Assert.Equal(32f,Assert.IsType<float>(lift.GetType().GetField("Target")!.GetValue(lift)));
            Assert.IsType<TriggerLiftSafeAnimationAction>(sequence.Children[2]);var second=Assert.IsType<NativePanTiltRunner>(sequence.Children[3]);
            double deg=BitConverter.Int64BitsToDouble(0x3F91DF46A2529D39);
            Assert.Equal((float)(-22.5d*deg),(float)first.Parameters.PanAngleRad,5);
            Assert.Equal((float)(-15d*deg),(float)first.Parameters.HeadAngleRad,5);
            Assert.Equal((float)(-25d*deg),(float)second.Parameters.PanAngleRad,5); // reversed 0..-5, draw .5
            Assert.Equal((float)(-5d*deg),(float)second.Parameters.HeadAngleRad,5);
            Assert.Equal(180f*BitConverter.UInt32BitsToSingle(0x3C8EFA35),(float)first.Parameters.MaxSpeedRadPerSec);
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }

    [Fact]
    public void FailureCallbacksVisitAllSectorsAndRecordRealPoseOnce()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var scan=Assert.IsType<NativeHikingLookInPlace>(f.Runtime.Bound["Hiking_LookInPlaceForUnknown"]);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        for(int sector=0;sector<8;sector++)
        {
            Assert.Equal((byte)2,scan.Sectors[sector]);
            var action=Assert.IsAssignableFrom<ActionRunner>(scan.OwnedRunner);action.State=0x03000018;f.Robot.ActionList.Update();
            Assert.Equal((byte)(sector+1),scan.CompletedSectorCount);Assert.Equal((byte)3,scan.Sectors[sector]);
            if(sector is 1 or 2)
            {
                float score=(float)scan.NativeScoring!.Evaluate(scan,f.Runtime.Context,0,null,0,null,runningClockSec:0);
                Assert.Equal(sector==1?1.1f:1.1f-1f,score);
            }
        }
        Assert.Null(scan.OwnedRunner);Assert.Single(scan.PreviousLocations);Assert.False(scan.IsRunnable(f.Runtime.Context));
        using var pose30=new Pose3d(Rotation3d.Identity,new Vec3(30,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));f.Robot.SetPose(pose30);Assert.False(scan.IsRunnable(f.Runtime.Context));
        using var pose31=new Pose3d(Rotation3d.Identity,new Vec3(31,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));f.Robot.SetPose(pose31);Assert.True(scan.IsRunnable(f.Runtime.Context));
        int starts=scan.ScoreIncreaseCount;f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        using var scope=new BehaviorScope();scan.AttachRun(f.Runtime.Context,scope);Assert.False(scan.Resume(null,f.Rig.Engine.Timer.SecondsF));
        Assert.Equal((byte)0,scan.CompletedSectorCount);Assert.Single(scan.PreviousLocations);Assert.Equal(starts,scan.ScoreIncreaseCount);
    }

    [Fact]
    public void AcceptedSameRootPoseUsesFlattenedInitYawForRealRay()
    {
        // Supplied supported SetPose API input with a rotated intermediate parent, not firmware output.
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        using var parent=new Pose3d(Rotation3d.FromAngleAxis(.7f,new Vec3(0,0,1)),Vec3.Zero,parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        using var pose=new Pose3d(Rotation3d.FromAngleAxis(1f,new Vec3(0,0,1)),Vec3.Zero,parent:parent);f.Robot.SetPose(pose);
        var scan=Assert.IsType<NativeHikingLookInPlace>(f.Runtime.Bound["Hiking_LookInPlaceForUnknown"]);
        f.Rig.Engine.ContextRandom.WordSourceForTest=()=>0;
        try
        {
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            var ray=scan.LastRay!.Value;Assert.Equal(10.4183,ray.From.X,3);Assert.Equal(38.6194,ray.From.Y,3);
            var sequence=Assert.IsType<CompoundActionSequential>(scan.OwnedRunner);
            Assert.Equal(1.307301,(float)Assert.IsType<NativePanTiltRunner>(sequence.Children[0]).Parameters.PanAngleRad,4);
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }

    [Fact]
    public void RealMapWithSuppliedClearRootFinishesWithoutActions()
    {
        // Supplied native tree content; all eight queries still use the actual quad-tree ray primitive.
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var map=f.M.World.Maps.Current!;
        map.Tree.Root.ClearDescendants(map.Tree.Processor);
        map.Tree.Root.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.ClearOfObstacle,0,0)),map.Tree.Processor);
        Assert.False(map.HasCollisionRayWithTypes(new Vec2(36.95518,-15.30734),new Vec2(175.5371,-72.7098),t=>t==MemoryMapContentType.Unknown));
        var scan=Assert.IsType<NativeHikingLookInPlace>(f.Runtime.Bound["Hiking_LookInPlaceForUnknown"]);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Null(scan.OwnedRunner);Assert.Equal((byte)0,scan.CompletedSectorCount);Assert.All(scan.Sectors,value=>Assert.Equal((byte)1,value));
        Assert.Single(scan.PreviousLocations);Assert.False(scan.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF*1000));
    }

    [Fact]
    public void MixedActualTreeSkipsClearSectorAndChoosesRightOnTie()
    {
        // Supplied native tree cells; queries run the real SourceQuadTree ray walker.
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var map=f.M.World.Maps.Current!;var tree=map.Tree;var root=tree.Root;
        root.ClearDescendants(tree.Processor);root.X=0;root.Y=0;root.Side=400;root.Depth=1;root.Bounds=new SourceQuadBounds(0,0,400);
        root.Subdivide(tree.Processor);root.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.ClearOfObstacle,0,0)),tree.Processor);
        foreach(var child in root.Children)child!.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.ClearOfObstacle,0,0)),tree.Processor);
        root.Children[0]!.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.Unknown,0,0)),tree.Processor);
        Assert.False(map.HasCollisionRayWithTypes(new Vec2(36.95518,-15.30734),new Vec2(175.5371,-72.7098),t=>t==MemoryMapContentType.Unknown));
        Assert.True(map.HasCollisionRayWithTypes(new Vec2(36.95518,15.30734),new Vec2(175.5371,72.7098),t=>t==MemoryMapContentType.Unknown));
        var scan=Assert.IsType<NativeHikingLookInPlace>(f.Runtime.Bound["Hiking_LookInPlaceForUnknown"]);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Equal((byte)1,scan.Sectors[0]);Assert.Equal((byte)2,scan.Sectors[1]);Assert.Equal((byte)0,scan.Sectors[7]);Assert.NotNull(scan.OwnedRunner);
    }

    [Fact]
    public void CompletedLocationsEvictOldestAndInterruptedScanDoesNotAppend()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var tree=f.M.World.Maps.Current!.Tree;var root=tree.Root;
        root.Side=1000;root.Bounds=new SourceQuadBounds(0,0,1000); // supplied native map geometry for four spaced locations
        root.ClearDescendants(tree.Processor);
        root.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.ClearOfObstacle,0,0)),tree.Processor);
        var scan=Assert.IsType<NativeHikingLookInPlace>(f.Runtime.Bound["Hiking_LookInPlaceForUnknown"]);var manager=f.Robot.BehaviorManagerOwner!;
        void Position(float x){using var pose=new Pose3d(Rotation3d.Identity,new Vec3(x,0,0),parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));f.Robot.SetPose(pose);}
        foreach(float x in new[]{0f,31f,62f,93f})
        {
            Position(x);Assert.True(manager.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Assert.Null(scan.OwnedRunner);manager.Stop(BehaviorStopReason.Completed,f.Rig.Engine.Timer.SecondsF);
        }
        Assert.Equal(new[]{31d,62d,93d},scan.PreviousLocations.Select(p=>p.Translation.X));
        root.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.Unknown,0,0)),tree.Processor);
        Position(124f);Assert.True(manager.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());Assert.NotNull(scan.OwnedRunner);
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        Assert.Equal(new[]{31d,62d,93d},scan.PreviousLocations.Select(p=>p.Translation.X));
    }

    [Fact]
    public void ActualReplacementRejectsMatchingOldCompletion()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var scan=Assert.IsType<NativeHikingLookInPlace>(f.Runtime.Bound["Hiking_LookInPlaceForUnknown"]);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var first=Assert.IsAssignableFrom<ActionRunner>(scan.OwnedRunner);uint tag=scan.CurrentRunnerTag;
        f.Rig.Engine.Robots.RemoveRobot(1,false);var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
        var next=Assert.IsType<NativeHikingLookInPlace>(replacement.NativeBehaviorRuntime!.Bound["Hiking_LookInPlaceForUnknown"]);
        // AbortAll may deliver callbacks before retirement; compare only the post-retirement baseline.
        var retired=scan.OwnedRunner;uint retiredTag=scan.CurrentRunnerTag;byte count=scan.CompletedSectorCount;int sent=f.Rig.SentSince(0).Count();
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(tag,first.Type,0,Array.Empty<uint>(),0));
        if(retired is not null)f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(retiredTag,retired.Type,0,Array.Empty<uint>(),0));
        Assert.Same(retired,scan.OwnedRunner);Assert.Equal(retiredTag,scan.CurrentRunnerTag);Assert.Equal(count,scan.CompletedSectorCount);
        Assert.Null(next.OwnedRunner);Assert.False(next.EngineRunning);Assert.Equal(sent,f.Rig.SentSince(0).Count());
    }

    [Fact]
    public void MissingMapAfterCompletionHoldsAndLateStoppedCallbackDoesNotAdvance()
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var scan=Assert.IsType<NativeHikingLookInPlace>(f.Runtime.Bound["Hiking_LookInPlaceForUnknown"]);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var action=Assert.IsAssignableFrom<ActionRunner>(scan.OwnedRunner);uint tag=scan.CurrentRunnerTag;
        uint origin=f.M.World.Maps.CurrentOrigin;var map=f.M.World.Maps.Current!;f.M.World.Maps.Maps.Remove(origin);
        try
        {
            action.State=0x03000018;f.Robot.ActionList.Update();Assert.Equal((byte)0,scan.CompletedSectorCount);
            Assert.True(scan.Update(f.Runtime.Context,f.Rig.Engine.Timer.SecondsF*1000));Assert.Empty(scan.PreviousLocations);
            f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
            f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(tag,action.Type,0,Array.Empty<uint>(),0));
            Assert.False(scan.EngineRunning);Assert.Equal((byte)0,scan.CompletedSectorCount);Assert.Empty(scan.PreviousLocations);
        }
        finally {f.M.World.Maps.Maps[origin]=map;}
    }
}
