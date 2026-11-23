using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using System.Text.Json;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M13X1BuilderTests
{
    private static Pose3d At(float x,float y,float angle=0)=>new(Mat3.AboutZ(angle),new Vec3(x,y,0));
    private static MotionPrimitiveSet Prims()
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir is not null){string p=Path.Combine(dir.FullName,"re-analysis/obb");if(Directory.Exists(p)){var prims=MotionPrimitiveSet.FromObb(p);if(prims is not null)return prims;}dir=dir.Parent;}
        throw new InvalidOperationException("Shipped motion primitives are required for this regression.");
    }
    private sealed record Unknown:PathSegment;
    [Fact]
    public void ShortPlannerFalseRetainsPathAndBaseMissingPlanDoesNotCopy()
    {
        var source=new List<string>();var planner=new NativeShortPlanner(false,(_,_,k,t)=>source.Add(k+" "+t),_=>{});
        Assert.False(planner.GetCompletePath(Pose3d.Identity,PathMotionProfile.Default,out _,out _));
        Assert.Equal(1,planner.ComputePath(Pose3d.Identity,new[]{At(12,0)}));
        Assert.True(planner.GetCompletePath(Pose3d.Identity,PathMotionProfile.Default,out var first,out _));Assert.Single(first);
        int before=source.Count;Assert.Equal(2,planner.ComputeNewPathIfNeeded(At(500,500),false));
        Assert.Equal(before,source.Count);Assert.Equal(2,planner.CheckPlanningStatus);
        Assert.True(planner.GetCompletePath(Pose3d.Identity,PathMotionProfile.Default,out var kept,out _));Assert.Equal(first,kept);
        // Force true, no line and no final turn: Face retains the previous path.
        planner.ComputePath(Pose3d.Identity,new[]{Pose3d.Identity});planner.GetCompletePath(Pose3d.Identity,PathMotionProfile.Default,out var retained,out _);Assert.Equal(first,retained);
    }
    [Fact]
    public void FaceAndApproachBackupPrintsTheSignedAngleAndKeepsTheReverseSegment()
    {
        using var rig = new Rig();
        var lines = new List<string>();
        rig.Robot.Engine.StackLogLine += lines.Add;
        var planner = rig.Robot.Motion.Path.NativeFace;
        using var start = At(0, 0);
        using var target = At(-20, 15);
        Assert.Equal(1, planner.ComputePath(start, [target]));
        Assert.Contains("FaceAndApproachPlanner: deltaTheta of 2.498091 above threshold, doing backup!", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("MISSING:") && line.Contains("doing backup"));
        Assert.True(planner.GetCompletePath(start, PathMotionProfile.Default, out var path, out _));
        var reverse = path.OfType<PathSegment.Line>().ToArray();
        Assert.NotEmpty(reverse);
        Assert.All(reverse, segment => Assert.True(segment.SpeedMmps < 0));
        Assert.Equal((-20f, 15f), (reverse[^1].ToX, reverse[^1].ToY));
    }

    [Fact]
    public void MinimalBackupRecipeAndLogsUseUpdatedCursor()
    {
        var logs=new List<(string Key,string Text)>();var p=new NativeShortPlanner(true,(_,_,k,t)=>logs.Add((k,t)),_=>{});
        p.ComputePath(At(10,0),new[]{At(0,0)});
        Assert.Contains(logs,x=>x.Key=="MinimalAnglePlanner.Plan.Backup"&&x.Text=="-10.000000");
        Assert.Contains(logs,x=>x.Key=="MinimalAnglePlanner.FinalPosition"&&x.Text=="(0.000000, 0.000000, -0.000000deg)");
    }
    [Fact]
    [Trait("Fidelity","M13-035 HARDWARE_ONLY scheduling")]
    public void WorkerPendingStopDoesNothingAndRunningStopClearsOnlyF2()
    {
        using var rig=new Rig();var backend=new LatticePlanner(new LatticeEnvironment(Prims()));
        var logs=new List<string>();using var runtime=new NativeLatticePlannerRuntime(backend,rig.Vision.World,()=>null,(_,_,k,_)=>logs.Add(k),_=>{});
        Assert.False(runtime.F0);Assert.False(runtime.F1);Assert.False(runtime.F3);Assert.True(runtime.F2);Assert.True(runtime.F4);Assert.False(runtime.F5);Assert.Equal(0,runtime.CheckPlanningStatus);
        lock(runtime.ContextMutex)
        {
            Assert.Equal(1,runtime.ComputePath(Pose3d.Identity,new[]{Pose3d.Identity}));Assert.True(runtime.F0);Assert.Equal(1,runtime.CheckPlanningStatus);
            runtime.StopPlanning();Assert.True(runtime.F2);Assert.True(runtime.F0);
        }
        Assert.True(SpinWait.SpinUntil(()=>runtime.CheckPlanningStatus==3,TimeSpan.FromSeconds(10)));
        backend.ArtificialPlannerDelayMs=3000;using var started=new ManualResetEventSlim();backend.PlanningStarted+=started.Set;
        Assert.Equal(1,runtime.ComputePath(Pose3d.Identity,new[]{At(100,0)}));Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
        Assert.True(runtime.F3);runtime.StopPlanning();Assert.False(runtime.F2);
        Assert.True(SpinWait.SpinUntil(()=>runtime.CheckPlanningStatus==0&&!runtime.F3,TimeSpan.FromSeconds(10)));
        runtime.Dispose();Assert.Contains("LatticePlanner.DestroyThread.Join",logs);Assert.Contains("LatticePlanner.DestroyThread.Joined",logs);
    }
    [Fact]
    public void FailedComputeNewTryLockReturnsTwoWithoutDispatch()
    {
        using var rig=new Rig();using var runtime=new NativeLatticePlannerRuntime(new(new LatticeEnvironment(Prims())),rig.Vision.World,()=>null,(_,_,_,_)=>{},_=>{});
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();var hold=Task.Run(()=>{lock(runtime.ContextMutex){entered.Set();release.Wait();}});
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        try{Assert.Equal(2,runtime.ComputeNewPathIfNeeded(Pose3d.Identity,false));Assert.False(runtime.F0);Assert.Equal(0,runtime.CheckPlanningStatus);}finally{release.Set();Assert.True(hold.Wait(TimeSpan.FromSeconds(10)));}
    }
    [Fact]
    public void ProfileSplitUsesForwardMagnitudeAndKeepsSignIncludingNegativeZero()
    {
        var line=new PathSegment.Line(0,0,100,0,30,200,200);
        var path=NativePathBuffer.ApplyMotionProfile(new[]{line},PathMotionProfile.Default,(_,_,_,_)=>{},_=>{});
        Assert.Equal(2,path.Count);var first=Assert.IsType<PathSegment.Line>(path[0]);var second=Assert.IsType<PathSegment.Line>(path[1]);
        Assert.Equal(90,first.ToX);Assert.Equal(100,first.SpeedMmps);Assert.Equal(90,second.FromX);Assert.Equal(20,second.SpeedMmps);
        var reverse = NativePathBuffer.ApplyMotionProfile(new[]
        {
            line with { SpeedMmps = BitConverter.UInt32BitsToSingle(0x80000000) }
        }, PathMotionProfile.Default, (_, _, _, _) => { }, _ => { });
        Assert.Equal(-100, Assert.IsType<PathSegment.Line>(reverse[0]).SpeedMmps);
        Assert.Equal(-20, Assert.IsType<PathSegment.Line>(reverse[1]).SpeedMmps);
        Assert.Empty(NativePathBuffer.ApplyMotionProfile(new PathSegment[]{new Unknown()},PathMotionProfile.Default,(_,_,_,_)=>{},_=>{}));
    }
    [Fact]
    public void PathSafetyAccumulatesSubthresholdRemaindersAndKeepsEmptyOutput()
    {
        var line=new PathSegment.Line(0,0,1,0,1,1,1);var output=new List<PathSegment>{line};var logs=new List<string>();
        NativePlanDependencies.Geometry G(PathSegment _)=>new(1,new(0,0,0),new(1,0,0),1,1,0);
        Assert.False(NativePlanDependencies.PathIsSafe(Array.Empty<PathSegment>(),output,0,G,(_,g,_) => (g.End,0),(_,_,k,_)=>logs.Add(k),_=>{}));Assert.Single(output);
        Assert.False(NativePlanDependencies.PathIsSafe(new[]{line,line},output,0,G,(_,g,_) => (g.End,0.000008f),(_,_,k,_)=>logs.Add(k),_=>{}));Assert.Single(output);Assert.Contains("xythetaEnvironment.PathIsSafe",logs);
        StateC state=new(0,0,0);float eps=BitConverter.Int32BitsToSingle(0x3727C5AC);
        float penalty=NativePlanDependencies.ApplyPathSegment(line,G(line),ref state,true,eps,16,1,(_,_,_)=>new[]{eps/10f},(_,_,_,_)=>{},_=>{});
        Assert.True(penalty>=eps);Assert.Equal(new StateC(0,0,0),state);
        var turn = new PathSegment.PointTurn(0, 0, 1, 0.1f, 1, 1, 1, true);
        var geometry = NativePlanDependencies.Describe(turn);
        Assert.Equal(0, NativePlanDependencies.ApplyPathSegment(turn, geometry, ref state, true, eps, 16, 1,
            (_, _, _) => Array.Empty<float>(), (_, _, key, _) => logs.Add(key), _ => { }));
        Assert.Equal(1, state.Theta);
        Assert.DoesNotContain("M1-034/M13-034", logs);

        state = default;
        Assert.Equal(0, NativePlanDependencies.ApplyPathSegment(turn, geometry, ref state, true, eps, 16, 1,
            (_, _, _) => new[] { 1000f }, (_, _, key, _) => logs.Add(key), _ => { }));
        Assert.Equal(1, state.Theta);
        Assert.Contains("M1-034/M13-034", logs);
    }
    [Fact]
    public void ShortPathSafetyUsesTheLatticeContextObstacleBuckets()
    {
        using var rig = new Rig();
        var environment = new LatticeEnvironment(Prims());
        var keys = new List<string>();
        using var runtime = new NativeLatticePlannerRuntime(new(environment), rig.Vision.World, () => null,
            (_, _, key, _) => keys.Add(key), _ => { });
        PathSegment[] path = [NativePlanDependencies.DefineLine(0, 0, 20, 0, 40, 200, 500)];
        Assert.True(runtime.CheckIsPathSafe(path, 0));
        Assert.True(environment.AddPolygonObstacle([new(10, -40), new(30, -40), new(30, 40), new(10, 40)],
            LatticeEnvironment.HardPenalty, uint.MaxValue));
        Assert.False(runtime.CheckIsPathSafe(path, 0));
        Assert.Contains("xythetaEnvironment.PathIsSafe", keys);
    }

    [Fact]
    public void LatticePreparationPreservesNearCollisionsAndFarSuccessors()
    {
        var environment = new LatticeEnvironment(Prims());
        Assert.True(environment.AddPolygonObstacle([new(-40, -40), new(30, -40), new(30, 40), new(-40, 40)],
            LatticeEnvironment.HardPenalty, uint.MaxValue));
        LatticeState near = new(0, 0, 0), far = new(-30, -30, 0);
        var nearBefore = environment.Successors(near, 0, false).ToArray();
        var farBefore = environment.Successors(far, 0, true).ToArray();
        Assert.NotEmpty(farBefore);
        Assert.True(nearBefore.Length < farBefore.Length);

        environment.PrepareForPlanning(error => throw new InvalidOperationException(error));

        Assert.Equal(nearBefore, environment.Successors(near, 0, false).ToArray());
        Assert.Equal(farBefore, environment.Successors(far, 0, true).ToArray());
    }

    [Fact]
    public void RecordedChargerApproachKeepsItsPlanAfterObstaclePreparation()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_CHARGER_CLIFF_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;
        // Anchor the search to the first ExecutePath 9 window and the preceding reported objects.
        var window = RecordedRobotInput.ReadOutbound(bundle, at => at >= 629070, 629090, [0x3D, 0x3F, 0x41])
            .Select(row => row.Message).ToArray();
        Assert.Equal(9, Assert.Single(window.OfType<ExecutePath>()).EventId);
        var line = window.OfType<AppendPathSegmentLine>().First();
        var turn = Assert.Single(window.OfType<AppendPathSegmentPointTurn>());
        using var cubePose = ObservedPose(609955, 0);
        using var chargerPose = ObservedPose(618445, 4);
        using var preDock = ChargerGeometry.PreDockPose(chargerPose);
        Assert.Equal(turn.TargetAngleRad, (float)preDock.AngleAroundZ, 5);
        // The capture records observations, but no map snapshot. Rebuild the type-3
        // polygons from the reported poses and the engine's bounding-quad routines.
        var cube = new ObservableObject(0, ObjectType.Block_LIGHTCUBE2, CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE2));
        var obstacles = new[]
        {
            Footprint.GetBoundingQuadXY(cube, cubePose, 0),
            ObjectFootprint.ChargerBoundingQuadXY(chargerPose, 0)
        };
        var environment = new LatticeEnvironment(Prims());
        Assert.True(environment.ImportObstaclePolygons(obstacles.Select(quad =>
            (IReadOnlyList<P2f>)Enumerable.Range(0, 4).Select(i => new P2f(quad[i].X, quad[i].Y)).ToArray()),
            tightPadding: false, uint.MaxValue));
        Assert.Equal(2, environment.ObstacleCount);
        var planner = new LatticePlanner(environment);
        planner.SetRunFlag(true);
        // Search 2 currentRobotState in the stack log and the matching pre-dock pose
        // converted to drive-centre coordinates (0x005477A0).
        StateC start = new(-117.798f, 11.8916f, -0.370406f);
        float heading = (float)preDock.AngleAroundZ;
        StateC goal = new((float)preDock.Translation.X - 20 * MathF.Cos(heading),
            (float)preDock.Translation.Y - 20 * MathF.Sin(heading), heading);
        var before = Assert.IsType<LatticePlan>(planner.ComputePath(start, [goal], maxExpansions: 100_000));
        environment.PrepareForPlanning(error => throw new InvalidOperationException(error));
        var after = Assert.IsType<LatticePlan>(planner.ComputePath(start, [goal], maxExpansions: 100_000));
        Assert.Equal(before.Start, after.Start);
        Assert.Equal(before.Cost, after.Cost);
        Assert.Equal(before.Expansions, after.Expansions);
        Assert.Equal(before.Actions.ToArray(), after.Actions.ToArray());
        Assert.Equal(before.Penalties.ToArray(), after.Penalties.ToArray());
        Assert.Equal(line.XStartMm, after.Start.X * environment.Primitives.ResolutionMm);
        Assert.Equal(line.YStartMm, after.Start.Y * environment.Primitives.ResolutionMm);
        var segments = new List<PathSegment>();
        NativePlanDependencies.AppendToPath(environment.Primitives, after, segments, 0,
            primitive => primitive.CachedPath, NativePlanDependencies.OffsetStart,
            gap => throw new InvalidOperationException(gap));
        var end = NativePlanDependencies.GetEndPose(segments.Last(segment => segment is not PathSegment.PointTurn));
        Assert.Equal(turn.XMm, end.X, 3);
        Assert.Equal(turn.YMm, end.Y, 3);

        Pose3d ObservedPose(uint timestamp, uint objectId)
        {
            foreach (string json in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
            {
                using var document = JsonDocument.Parse(json);
                var row = document.RootElement;
                if (row.GetProperty("atMs").GetDouble() >= 629000) break;
                if (row.GetProperty("kind").GetString() != "engine-to-game" ||
                    row.GetProperty("tag").GetString() != "0x44") continue;
                var payload = new CladReader(Convert.FromHexString(row.GetProperty("bodyHex").GetString()!));
                uint stamp = payload.U32();
                payload.U32(); // family
                payload.U32(); // type
                if (payload.U32() != objectId || stamp != timestamp) continue;
                for (int i = 0; i < 4; i++) payload.F32(); // image rectangle
                var position = new Vec3(payload.F32(), payload.F32(), payload.F32());
                return new Pose3d(new Rotation3d(payload.F32(), payload.F32(), payload.F32(), payload.F32()), position);
            }
            throw new InvalidDataException($"Object {objectId} at firmware timestamp {timestamp} is absent from the capture.");
        }
    }

    [Theory]
    [InlineData("094833", 544561.1182, 12, false, -0.168029f, 162.40463256835938f, 90.17548370361328f, 89.82451629638672f)]
    [InlineData("094833", 555525.2311, 13, true, -0.692048f, 164.60806274414062f, 91.4075698852539f, 88.5924301147461f)]
    [InlineData("182433", 717134.0557, 3, true, 0.350391f, 163.57691955566406f, 89.28025817871094f, 90.71974182128906f)]
    public void RecordedStackAndRollShortPathsMatchTheRobotWire(
        string session, double at, int id, bool minimal, float firstTurnDelta, float speed, float accel, float decel)
    {
        string? root = Environment.GetEnvironmentVariable("COZMO_SHORT_PLANNER_REPLAY_ROOT");
        if (string.IsNullOrEmpty(root)) return;
        string bundle = Path.Combine(root, $"20261119-{session}-LLM-SESSION");
        var window = RecordedRobotInput.ReadOutbound(bundle, ms => ms >= at - 2000, at,
            [0x3C, 0x3D, 0x3E, 0x3F, 0x41]).Select(row => row.Message).ToList();
        Assert.Equal(id, Assert.IsType<ExecutePath>(window[^1]).EventId);
        var recorded = window.Skip(window.FindLastIndex(message => message is ClearPath) + 1).SkipLast(1).ToArray();
        var startLine = recorded.OfType<AppendPathSegmentLine>().First();
        var firstTurn = recorded.OfType<AppendPathSegmentPointTurn>().First();
        var finalTurn = Assert.IsType<AppendPathSegmentPointTurn>(recorded[^1]);

        // Replay the short planner at its input boundary. The capture gives drive-center/goal
        // coordinates via the path endpoints and logs the signed first-turn delta to six decimals.
        // The speed chooser had already run. Supply its three recorded f32 values independently
        // of the planner output and compare the native path doler's fields below.
        float startYaw = EngineF.Rescale(firstTurn.TargetAngleRad - firstTurnDelta);
        using var start = At(startLine.XStartMm, startLine.YStartMm, startYaw);
        using var goal = At(finalTurn.XMm, finalTurn.YMm, finalTurn.TargetAngleRad);
        var profile = PathMotionProfile.Default with
        {
            SpeedMmps = speed,
            AccelMmps2 = accel,
            DecelMmps2 = decel
        };
        var missing = new List<string>();
        var planner = new NativeShortPlanner(minimal, (_, _, _, _) => { }, missing.Add);
        Assert.Equal(1, planner.ComputePath(start, [goal]));
        Assert.True(planner.GetCompletePath(start, profile, out var path, out _));
        Assert.DoesNotContain(missing, message => message.Contains("M13-034"));

        using var rig = new Rig();
        var sent = new List<RobotMessage>();
        Assert.Equal(0, rig.Robot.Motion.Path.ExecutePath(path, false, send: message =>
        {
            sent.Add(message);
            return true;
        }));
        var segments = sent.Where(message => message is AppendPathSegmentLine or AppendPathSegmentPointTurn).ToArray();
        Assert.Equal(recorded.Length, segments.Length);
        for (int index = 0; index < segments.Length; index++)
        {
            switch (recorded[index])
            {
                case AppendPathSegmentLine line:
                    var actualLine = Assert.IsType<AppendPathSegmentLine>(segments[index]);
                    Near(line.XStartMm, actualLine.XStartMm, 0.001f);
                    Near(line.YStartMm, actualLine.YStartMm, 0.001f);
                    Near(line.XEndMm, actualLine.XEndMm, 0.001f);
                    Near(line.YEndMm, actualLine.YEndMm, 0.001f);
                    Assert.Equal(line.Speed, actualLine.Speed);
                    break;
                case AppendPathSegmentPointTurn turn:
                    var actualTurn = Assert.IsType<AppendPathSegmentPointTurn>(segments[index]);
                    Near(turn.XMm, actualTurn.XMm, 0.001f);
                    Near(turn.YMm, actualTurn.YMm, 0.001f);
                    Near(turn.TargetAngleRad, actualTurn.TargetAngleRad, 0.00001f);
                    Assert.Equal(turn.AngleToleranceRad, actualTurn.AngleToleranceRad);
                    Assert.Equal(turn.UseShortestDirection, actualTurn.UseShortestDirection);
                    Assert.Equal(turn.Speed, actualTurn.Speed);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected recorded segment {recorded[index].Id}.");
            }
        }

        static void Near(float expected, float actual, float tolerance)
            => Assert.InRange(actual, expected - tolerance, expected + tolerance);
    }

    [Fact]
    public void SafePrefixRetainsCrossingActionOldPenaltyAndChecksLaterCollision()
    {
        // H5/H6: 40mm retention, old penalty + double 0.5, and startIndex >= count.
        var prims=Prims();var action=new MotionPrimitive(0,0,10,0,0,Array.Empty<IntermediatePose>(),1);
        var old=new LatticePlan(new(0,0,0),new[]{action,action,action},1,3){Penalties=new[]{0.25f,0.25f,0.25f}};
        var prefix=new LatticePlan(new(99,99,0),Array.Empty<MotionPrimitive>(),0,0);StateC end=default;int checkedCount=0;
        (LatticeState State,float Penalty) Apply(LatticeState state,MotionPrimitive p,bool check)
        {if(check)checkedCount++;return (NativePlanDependencies.Advance(state,p),checkedCount==3?0.875f:0.75f);}
        Assert.False(NativePlanDependencies.PlanIsSafe(old,0,40,prims,Apply,ref prefix,ref end));
        Assert.Equal(3,checkedCount);Assert.Single(prefix.Actions);Assert.Equal(new[]{0.25f},prefix.Penalties);
        Assert.Equal(10*prims.ResolutionMm,end.X);
        checkedCount=0;Assert.True(NativePlanDependencies.PlanIsSafe(old,3,40,prims,Apply,ref prefix,ref end));
        Assert.Equal(0,checkedCount);Assert.Empty(prefix.Actions);Assert.Equal(new LatticeState(30,0,0),prefix.Start);
    }
    [Fact]
    public void AppendKeepsDestinationStartAndCapacityMergesDoNotRequireCollinearity()
    {
        var path=new List<PathSegment>{new PathSegment.Line(0,0,1,0,1,2,3)};
        var second=new PathSegment.Line(500,500,999,888,2,4,5);
        NativePlanDependencies.AddSegmentsToPath(new[]{second},path,0,0,0,(s,_,_,_)=>s,_=>{});
        var merged=Assert.IsType<PathSegment.Line>(Assert.Single(path));Assert.Equal(999,merged.ToX);Assert.Equal(1,merged.SpeedMmps);Assert.Equal(2,merged.AccelMmps2);
        path=Enumerable.Repeat<PathSegment>(second,128).ToList();Assert.False(NativePathBuffer.Append(path,second,_=>{}));Assert.Equal(128,path.Count);
        var prim=new MotionPrimitive(0,0,1,0,0,Array.Empty<IntermediatePose>(),1);var a=new LatticePlan(new(10,0,0),new[]{prim},1,1){Penalties=new[]{0.1f}};var b=new LatticePlan(new(0,0,0),new[]{prim},1,1){Penalties=new[]{0.2f}};
        var appended=NativePlanDependencies.Append(a,b);Assert.Equal(a.Start,appended.Start);Assert.Equal(new[]{0.1f,0.2f},appended.Penalties);
    }
}
