using Cozmo.Robot;
using Cozmo.Bridge;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Protocol;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class SparksLookInPlaceNativeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExploreHonorsBaselineAndTheCommandTimeLimit(bool enabled)
    {
        using var f = new Fixture(sourceNvEmpty:true, engineOptions:new CozmoEngineOptions {
            ResourcesPath=PickupPyramidNativeExecutionTests.Resources(), BlockPoolPath="",
            C2=CozmoC2Settings.McpDefaults with { MapExplore=enabled } });
        using var bridge = new CozmoBridge(f.Rig.Robot);
        var result = await BridgeCommandTests.Invoke(bridge, "explore", """{"maxSeconds":1}""")
            .WaitAsync(TimeSpan.FromSeconds(4));
        if (enabled)
            Assert.Equal("time limit reached", result.GetProperty("report").GetProperty("reason").GetString());
        else
            Assert.Contains("disabled", result.GetProperty("detail").GetString());
    }

    [Fact]
    public void ExploreLegRejectsARememberedCliffAcrossTheForwardCorridor()
    {
        var map = new MemoryMap();
        var first = CozmoBridge.ChooseExploreLeg(map, new(0, 0), 0, []);
        Assert.NotNull(first);
        Assert.Equal(90, first.Value.XMm, 4);
        Vec2[] footprint = [new(60, -5), new(75, -5), new(75, 5), new(60, 5)];
        map.ReadC2Hazards = () => [new(new RoomOrigin(1, 1), MemoryMapContentType.Cliff, null, footprint, footprint, 0)];
        var avoided = CozmoBridge.ChooseExploreLeg(map, new(0, 0), 0, []);
        Assert.NotNull(avoided);
        Assert.NotEqual(0, avoided.Value.HeadingRad);
        Assert.False(map.HasCollisionRayWithTypes(new(0, 0), new(avoided.Value.XMm, avoided.Value.YMm)));
    }

    [Theory]
    [InlineData("cliff")]
    [InlineData("pickup")]
    [InlineData("cancel")]
    public async Task ExploreUsesTheNativeScanAndPlannerAndRetiresWhenInterrupted(string interrupt)
    {
        using var f = new Fixture(preloadAnimations:true, pumpAnimations:true, sourceNvEmpty:true,
            engineOptions:new CozmoEngineOptions { ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),
                BlockPoolPath="", C2=CozmoC2Settings.McpDefaults });
        using var bridge = new CozmoBridge(f.Rig.Robot);
        using var cancel = new CancellationTokenSource();
        var task = BridgeCommandTests.Invoke(bridge, "explore", """{"maxSeconds":30}""", cancel.Token);
        var scan = Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["Hiking_LookInPlace360"]);
        for (int i=0; i<500 && !task.IsCompleted; i++)
        {
            f.Motors();
            if (scan.SearchIterations != 0) break;
            // Supply terminal child completions to exercise the real scan state transitions.
            if (scan.OwnedRunner is { } child)
            {
                child.State = 0x03000018;
                f.Robot.ActionList.Update();
            }
        }
        Assert.True(scan.SearchIterations > 0);
        bool Driving()
        {
            lock (f.Rig.Log) return f.Rig.Log.Any(line => line.Contains("C2:map-explore: driving leg 1"));
        }
        for (int i=0; i<100 && !Driving() && !task.IsCompleted; i++) await Task.Delay(10);
        Assert.True(Driving());
        uint stamp = f.Robot.StoredState!.Timestamp;
        var pose = new EngineGamePose(100, 0, 0, 1, 0, 0, 0, f.Robot.CurrentOriginId);
        f.Rig.Engine.DeliverObservedObjectToApp(new(stamp, (uint)ObjectFamily.LightCube, (uint)ObjectType.Block_LIGHTCUBE1,
            21, 0, 0, 20, 20, pose, 0, true));
        f.Rig.Engine.DeliverObservedObjectToApp(new(stamp, (uint)ObjectFamily.Charger, (uint)ObjectType.Charger_Basic,
            32, 0, 0, 20, 20, pose, 0, false));
        f.Rig.Engine.PublishRobotObservedFace(new(-7, stamp, pose, new FaceRect(0, 0, 10, 10), "", [], [], [], []));
        f.Rig.Engine.DrainAppDelivery();
        Vec2[] footprint = [new(300,0), new(320,0), new(320,20), new(300,20)];
        f.M.World.Maps.Current!.InsertCliff(footprint, new CliffMapData(new(1,0), stamp));
        if (interrupt == "cliff")
        {
            f.Rig.Data(new CliffEvent { Timestamp=f.Robot.StoredState!.Timestamp, DetectedFlags=3, DidStopForCliff=true });
            f.Rig.Tick();
        }
        else if (interrupt == "pickup")
            f.Rig.Robot.Sensors.DispatchOffTreadsStateChanged(OffTreadsState.OnTreads, OffTreadsState.InAir);
        else cancel.Cancel();
        for (int i=0; i<10 && !task.IsCompleted; i++) { f.Rig.Tick(); await Task.Delay(10); }
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        var report = result.GetProperty("report");
        Assert.Equal(0, report.GetProperty("completedLegs").GetInt32());
        Assert.Contains(interrupt == "pickup" ? "picked up" : interrupt, report.GetProperty("reason").GetString());
        Assert.Equal(21u, Assert.Single(report.GetProperty("cubeIds").EnumerateArray()).GetUInt32());
        Assert.Equal(32u, Assert.Single(report.GetProperty("chargerIds").EnumerateArray()).GetUInt32());
        Assert.Equal(-7, Assert.Single(report.GetProperty("faceIds").EnumerateArray()).GetInt32());
        Assert.Contains(report.GetProperty("newHazards").EnumerateArray(), h => h.GetProperty("type").GetString() == "Cliff" &&
            h.GetProperty("localPolygon")[0].GetProperty("x").GetDouble() == 300);
        Assert.True(f.Robot.BehaviorManagerOwner!.IsReactionTriggerEnabled(ReactionTrigger.CliffDetected));
    }

    [Fact]
    public async Task RequestedWorkoutNaturallyChoosesRetainedFallbackInEmptyWorld()
    {
        // Public Freeplay/Spark input and supplied animation acknowledgments, with the actual chooser.
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);f.Robot.Progression!.Unlocked.Add(29);var manager=f.Robot.BehaviorManagerOwner!;
        var scan=Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        using var bridge=new CozmoBridge(f.Rig.Robot);
        Assert.Equal("submitted", (await BridgeCommandTests.Invoke(bridge, "request-spark", """{"spark":"Workout"}""")).GetProperty("status").GetString());
        Assert.Equal(29, manager.RequestedSparkId);
        var seen=new HashSet<byte>();
        for(int i=0;i<200&&!ReferenceEquals(manager.Current,scan);i++)
        {
            byte tag=f.Rig.Robot.Animations.CurrentTag;
            if(tag!=0&&seen.Add(tag)){f.Rig.Data(new AnimationStarted{AnimId=tag});f.Rig.Data(new AnimationEnded{AnimId=tag});}
            f.Rig.Tick(50);
        }
        Assert.Equal("SparksWorkout",f.Runtime.FreeplayObserver.Current?.Id);
        Assert.Same(scan,manager.Current);Assert.NotNull(scan.OwnedRunner);
        Assert.Equal("submitted", (await BridgeCommandTests.Invoke(bridge, "request-spark", """{"spark":"Count"}""")).GetProperty("status").GetString());
        Assert.Equal(0x55, manager.RequestedSparkId); Assert.Equal((byte)1, manager.SparkCancelLatch);
    }

    // Supplied terminal failures exercise the source's result-independent transitions.
    [Fact]
    public void ConfiguredScanUsesEightDrawsAndSkipsUnreachableHeadStages()
    {
        using var f=new Fixture();
        var scan=Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        void Complete(){var action=Assert.IsAssignableFrom<ActionRunner>(scan.OwnedRunner);action.State=0x03000018;f.Robot.ActionList.Update();}
        try
        {
            Assert.True(scan.ParametersAvailable);
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Assert.Equal(2,words);Assert.Equal(32f,Assert.IsType<float>(scan.OwnedRunner!.GetType().GetField("Target")!.GetValue(scan.OwnedRunner)));
            Complete();Assert.Equal(1,scan.NativeState);var s1=Assert.IsType<NativePanTiltRunner>(scan.OwnedRunner);Assert.Equal(6,words);
            Assert.Equal((float)(40d*BitConverter.Int64BitsToDouble(0x3F91DF46A2529D39)),(float)s1.Parameters.PanAngleRad);
            Assert.Equal((float)(-5d*BitConverter.Int64BitsToDouble(0x3F91DF46A2529D39)),(float)s1.Parameters.HeadAngleRad);
            Assert.Equal(120f*BitConverter.UInt32BitsToSingle(0x3C8EFA35),(float)s1.Parameters.MaxSpeedRadPerSec);
            Complete();Assert.Equal(2,scan.NativeState);var wait=Assert.IsType<NativeTimedWaitRunner>(scan.OwnedRunner);Assert.Equal(8,words);
            Assert.Equal(.20f,wait.Duration);wait.Init();float deadline=wait.Deadline;
            f.Rig.Engine.Timer.UpdateTime((long)Math.Round((double)deadline*1_000_000_000));
            Assert.Equal(deadline,f.Rig.Engine.Timer.SecondsF);Assert.Equal(0x01000000u,wait.CheckIfDone());
            f.Rig.Engine.Timer.UpdateTime((long)Math.Ceiling((double)MathF.BitIncrement(deadline)*1_000_000_000));Assert.Equal(0u,wait.CheckIfDone());
            Complete();Assert.Equal(3,scan.NativeState);Assert.IsType<NativePanTiltRunner>(scan.OwnedRunner);Assert.Equal(14,words);
            Complete();Assert.Equal(6,scan.NativeState);Assert.IsType<NativePanTiltRunner>(scan.OwnedRunner);Assert.Equal(18,words);
            Complete();Assert.Equal(1u,scan.SearchIterations);Assert.Equal(1,scan.NativeState);Assert.Equal(22,words);
            int starts=scan.ScoreIncreaseCount;
            f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
            using var scope=new BehaviorScope();scan.AttachRun(f.Runtime.Context,scope);
            Assert.False(scan.Resume(null,f.Rig.Engine.Timer.SecondsF));
            Assert.Equal(1u,scan.SearchIterations);Assert.Equal(starts,scan.ScoreIncreaseCount);
            Assert.Equal(32f,Assert.IsType<float>(scan.OwnedRunner!.GetType().GetField("Target")!.GetValue(scan.OwnedRunner)));
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }

    [Fact]
    public void ConfiguredOwnerIsRetainedAndRejectsUnsupportedParameters()
    {
        using var f=new Fixture();var scan=Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        Assert.True(scan.ParametersAvailable);Assert.True(scan.IsRunnable(f.Runtime.Context));
        Assert.Same(scan,f.Robot.BehaviorManagerOwner!.Find("SparksLookInPlace"));
        var selection=Assert.IsType<SelectionChooser>(f.Runtime.Tree.Single(a=>a.Id=="Selection").Chooser);
        Assert.Same(scan,selection.FindBehaviorByExecutableType!(6)); // native executable table: LookAroundInPlace
        // Literal source census includes specialized Pyramid choosers and the Gather delegate.
        (string path,int count)[] assets={
            ("freeplay/buildPyramid.json",2),("sparks/sparksBuildPyramid.json",2),
            ("sparks/sparksFireTruckAlarm.json",1),("sparks/sparksGatherCubes.json",1),
            ("sparks/sparksKnockOverCubes.json",1),("sparks/sparksPickupCube.json",1),
            ("sparks/sparksPopAWheelie.json",1),("sparks/sparksRollBlock.json",1),
            ("sparks/sparksStackBlock.json",1),("sparks/sparksWorkout.json",1)};
        static int Count(JsonElement value)=>value.ValueKind switch
        {
            JsonValueKind.String=>value.GetString()=="SparksLookInPlace"?1:0,
            JsonValueKind.Array=>value.EnumerateArray().Sum(Count),
            JsonValueKind.Object=>value.EnumerateObject().Sum(p=>Count(p.Value)),
            _=>0
        };
        int total=0;
        foreach(var (path,count) in assets)
        {
            string file=Path.Combine(f.Rig.Engine.Options.ResourcesPath!,"config","engine","behaviorSystem","activities",path.Replace('/',Path.DirectorySeparatorChar));
            using var asset=JsonDocument.Parse(File.ReadAllText(file),new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true});
            int actual=Count(asset.RootElement);Assert.Equal(count,actual);total+=actual;
            for(int i=0;i<actual;i++)Assert.Same(scan,f.Robot.BehaviorManagerOwner.Find("SparksLookInPlace"));
        }
        Assert.Equal(12,total);
        var source=f.Rig.Engine.HostBehaviorMap![BehaviorID.SparksLookInPlace];
        var changed=JsonNode.Parse(source.GetRawText(),documentOptions:new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true})!;
        changed["params"]!["s4_HeadAngleChangesMax"]=1;
        using var config=JsonDocument.Parse(changed.ToJsonString());
        using var other=new NativeSparksLookInPlace(new BehaviorFactoryContext{Manipulation=f.M,NativeRobot=f.Robot},config.RootElement);
        Assert.False(other.ParametersAvailable);
    }

    [Fact]
    public void CarryingSkipsLiftAndSensorRecipientRetires()
    {
        using var f=new Fixture();f.Add();f.Robot.CarryingOwner!.SetCarrying(7);
        var scan=Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        Assert.True(scan.IsRunnable(f.Runtime.Context));
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Equal(1,scan.NativeState);Assert.IsType<NativePanTiltRunner>(scan.OwnedRunner);
        var center=typeof(NativeSparksLookInPlace).GetField("_center",BindingFlags.Instance|BindingFlags.NonPublic)!;
        center.SetValue(scan,1f);
        f.Rig.Robot.Sensors.DispatchOffTreadsStateChanged(OffTreadsState.InAir,OffTreadsState.OnTreads);
        Assert.Equal(0f,(float)center.GetValue(scan)!);
        scan.Dispose();center.SetValue(scan,1f);
        f.Rig.Robot.Sensors.DispatchOffTreadsStateChanged(OffTreadsState.InAir,OffTreadsState.OnTreads);
        Assert.Equal(1f,(float)center.GetValue(scan)!);
    }

    [Fact]
    public void NoExternalInterfaceCreatesNoAdditionalSensorLease()
    {
        using var f=new Fixture();var sensors=f.Rig.Robot.Sensors;
        var field=sensors.GetType().GetField("ConfiguredBehaviorOffTreadsStateChanged",BindingFlags.Instance|BindingFlags.NonPublic)!;
        int Count()=>((Delegate?)field.GetValue(sensors))?.GetInvocationList().Length??0;
        int before=Count();f.Robot.HasExternalInterface=false;
        try
        {
            using var scan=new NativeSparksLookInPlace(new BehaviorFactoryContext{Manipulation=f.M,NativeRobot=f.Robot},f.Rig.Engine.HostBehaviorMap![BehaviorID.SparksLookInPlace]);
            Assert.Equal(before,Count());
        }
        finally {f.Robot.HasExternalInterface=true;}
    }

    [Fact]
    public void LateOwnedCompletionAfterStopCannotAdvanceOrRestartScan()
    {
        using var f=new Fixture();var scan=Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var action=Assert.IsAssignableFrom<ActionRunner>(scan.OwnedRunner);uint tag=scan.CurrentRunnerTag;
        f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(tag,action.Type,0,Array.Empty<uint>(),0));
        Assert.False(scan.EngineRunning);Assert.Null(scan.OwnedRunner);Assert.Equal(0u,scan.SearchIterations);
    }

    [Fact]
    public void ActualRobotReplacementRejectsLateMatchingCompletion()
    {
        using var f=new Fixture();var scan=Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var first=Assert.IsAssignableFrom<ActionRunner>(scan.OwnedRunner);uint firstTag=scan.CurrentRunnerTag;
        f.Rig.Engine.Robots.RemoveRobot(1,false);
        var replacement=f.Rig.Engine.Robots.AddRobot(1,withRic:false)!;
        var next=Assert.IsType<NativeSparksLookInPlace>(replacement.NativeBehaviorRuntime!.Bound["SparksLookInPlace"]);
        // AbortAll may have delivered a callback before retirement; compare only the post-retirement baseline.
        var retired=scan.OwnedRunner;uint retiredTag=scan.CurrentRunnerTag;uint iterations=scan.SearchIterations;int sent=f.Rig.SentSince(0).Count();
        f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(firstTag,first.Type,0,Array.Empty<uint>(),0));
        if(retired is not null)f.Rig.Engine.AnimationMessages.Deliver(0x5A,true,new RobotCompletedAction(retiredTag,retired.Type,0,Array.Empty<uint>(),0));
        Assert.Same(retired,scan.OwnedRunner);Assert.Equal(retiredTag,scan.CurrentRunnerTag);Assert.Equal(iterations,scan.SearchIterations);
        Assert.Null(next.OwnedRunner);Assert.False(next.EngineRunning);Assert.Equal(sent,f.Rig.SentSince(0).Count());
    }

    [Fact]
    public void DirectionEqualityAtOneHalfUsesZeroBranch()
    {
        using var f=new Fixture();var scan=Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        int word=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>++word==2?0x80000000u:0u;
        try
        {
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            var lift=Assert.IsAssignableFrom<ActionRunner>(scan.OwnedRunner);lift.State=0x03000018;f.Robot.ActionList.Update();
            var turn=Assert.IsType<NativePanTiltRunner>(scan.OwnedRunner);
            Assert.Equal((float)(40d*BitConverter.Int64BitsToDouble(0x3F91DF46A2529D39)),(float)turn.Parameters.PanAngleRad);
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }

    [Fact]
    public void CommonPlatformAndOffTreadsGatesDenyTheActualOwner()
    {
        using var f=new Fixture();var scan=Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        f.Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsOnCharger,origin:f.Robot.CurrentOriginId);
        Assert.True(f.Rig.Robot.Sensors.OnChargerPlatform);Assert.False(scan.IsRunnable(f.Runtime.Context));
        using var g=new Fixture();var other=Assert.IsType<NativeSparksLookInPlace>(g.Runtime.Bound["SparksLookInPlace"]);
        g.Rig.State(RobotStatusFlag.IsBodyAccMode|RobotStatusFlag.IsPickedUp,origin:g.Robot.CurrentOriginId);
        Assert.False(other.IsRunnable(g.Runtime.Context));
    }

    [Fact]
    public void ActualMainPoseYawControlsSweepTermination()
    {
        // Supplied action terminal results and observed main-pose yaw, not simulated motor success.
        using var f=new Fixture();var scan=Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        void Complete(){var action=Assert.IsAssignableFrom<ActionRunner>(scan.OwnedRunner);action.State=0x03000018;f.Robot.ActionList.Update();}
        void Cycle(float yaw)
        {
            Complete();Complete();Complete();
            var state=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,frame:f.Robot.PoseFrameId);
            state.Pose.Angle=yaw;f.Rig.Data(state);f.Rig.Tick(10);
            Complete();
        }
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        QueuedSelection.Keep(f.Rig.Robot,f.Robot.BehaviorManagerOwner,scan.Id);
        Complete();Cycle(2f);Cycle(-2.2831853f);Cycle(-.0831853f);
        Assert.Equal(3u,scan.SearchIterations);Assert.Equal(1,scan.NativeState);Assert.NotNull(scan.OwnedRunner);
        Cycle(.1168147f);Assert.Equal(4u,scan.SearchIterations);Assert.Equal(7,scan.NativeState);Assert.Null(scan.OwnedRunner);
    }

    [Fact]
    public void TurnsUseOneNonzeroIterationBaseline()
    {
        using var f=new Fixture();var scan=Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        var state=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,origin:f.Robot.CurrentOriginId,frame:f.Robot.PoseFrameId);
        state.Pose.Angle=1f;f.Rig.Data(state);f.Rig.Tick(10);
        f.Rig.Engine.ContextRandom.WordSourceForTest=()=>0;
        void Complete(){var action=Assert.IsAssignableFrom<ActionRunner>(scan.OwnedRunner);action.State=0x03000018;f.Robot.ActionList.Update();}
        try
        {
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            Complete();var s1=Assert.IsType<NativePanTiltRunner>(scan.OwnedRunner);
            Assert.Equal(new Radians(1f+(float)(40d*BitConverter.Int64BitsToDouble(0x3F91DF46A2529D39))).Value,(float)s1.Parameters.PanAngleRad,5);
            Complete();Complete();var s3=Assert.IsType<NativePanTiltRunner>(scan.OwnedRunner);
            Assert.Equal(1f,(float)s3.Parameters.PanAngleRad,5);
            Complete();var s6=Assert.IsType<NativePanTiltRunner>(scan.OwnedRunner);
            Assert.Equal(new Radians(1f-(float)(30d*BitConverter.Int64BitsToDouble(0x3F91DF46A2529D39))).Value,(float)s6.Parameters.PanAngleRad,5);
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }

    [Fact]
    public void SameRootIntermediateParentUsesLocalMainPoseYaw()
    {
        // Supplied API pose via native SetPose; this is not a firmware-produced pose claim.
        using var f=new Fixture();var scan=Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        using var parent=new Pose3d(Rotation3d.FromAngleAxis(.7f,new Vec3(0,0,1)),Vec3.Zero,parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        using var pose=new Pose3d(Rotation3d.FromAngleAxis(1f,new Vec3(0,0,1)),Vec3.Zero,parent:parent);
        f.Robot.SetPose(pose);Assert.Equal(1f,(float)f.Robot.GetCheckedMainPose().AngleAroundZ,5);
        using var flattened=f.Robot.GetCheckedMainPose().GetWithRespectToRoot();Assert.Equal(1.7f,(float)flattened.AngleAroundZ,5);
        f.Rig.Engine.ContextRandom.WordSourceForTest=()=>0;
        try
        {
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            var lift=Assert.IsAssignableFrom<ActionRunner>(scan.OwnedRunner);lift.State=0x03000018;f.Robot.ActionList.Update();
            var turn=Assert.IsType<NativePanTiltRunner>(scan.OwnedRunner);
            Assert.Equal(new Radians(1f+(float)(40d*BitConverter.Int64BitsToDouble(0x3F91DF46A2529D39))).Value,(float)turn.Parameters.PanAngleRad,5);
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }

    [Fact]
    public void FindFacesLookUpAlsoUsesLocalMainPoseYaw()
    {
        // Supplied same-root SetPose API input; the inherited helper reads local MAIN yaw.
        using var f=new Fixture();var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        using var parent=new Pose3d(Rotation3d.FromAngleAxis(.7f,new Vec3(0,0,1)),Vec3.Zero,parent:f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        using var pose=new Pose3d(Rotation3d.FromAngleAxis(1f,new Vec3(0,0,1)),Vec3.Zero,parent:parent);
        f.Robot.SetPose(pose);using var flattened=f.Robot.GetCheckedMainPose().GetWithRespectToRoot();Assert.Equal(1.7f,(float)flattened.AngleAroundZ,5);
        f.Rig.Engine.ContextRandom.WordSourceForTest=()=>0;
        try
        {
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(find.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            var lift=Assert.IsAssignableFrom<ActionRunner>(find.OwnedRunner);lift.State=0x03000018;f.Robot.ActionList.Update();
            var turn=Assert.IsType<NativePanTiltRunner>(find.OwnedRunner);
            float localDegrees=1f*BitConverter.UInt32BitsToSingle(0x42652EE1);
            float expected=(float)(((double)localDegrees-1d)*BitConverter.Int64BitsToDouble(0x3F91DF46A2529D39));
            Assert.Equal(new Radians(expected).Value,(float)turn.Parameters.PanAngleRad,5);
        }
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
    }

    [Theory]
    [InlineData(-1,true)] [InlineData(0,false)] [InlineData(1,false)]
    public void SweepThresholdUsesStrictFloatComparison(int offset,bool loops)
    {
        // Isolated boundary: supplied prior accumulation, actual zero observed yaw and actual S7 callback.
        using var f=new Fixture();var scan=Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        void Complete(){var action=Assert.IsAssignableFrom<ActionRunner>(scan.OwnedRunner);action.State=0x03000018;f.Robot.ActionList.Update();}
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(scan.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Complete();Complete();Complete();Complete();
        float threshold=BitConverter.UInt32BitsToSingle(0x40C90FDB);
        float supplied=offset<0?MathF.BitDecrement(threshold):offset>0?MathF.BitIncrement(threshold):threshold;
        typeof(NativeSparksLookInPlace).GetField("_accumulated",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(scan,supplied);
        Complete();Assert.Equal(1u,scan.SearchIterations);
        Assert.Equal(loops?1:7,scan.NativeState);Assert.Equal(loops,scan.OwnedRunner is not null);
    }
}
