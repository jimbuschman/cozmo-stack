using System.Collections.Concurrent;
using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M13PlannerOwnershipTests
{
    private static string Obb()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {
            var path=Path.Combine(d.FullName,"re-analysis/obb");
            if(MotionPrimitiveSet.FromObb(path) is not null)return path;
        }
        throw new InvalidOperationException("Shipped motion primitives required");
    }
    private static Pose3d At(float x,float y=0,float yaw=0,Pose3d? parent=null)
        =>new(Rotation3d.FromAngleAxis(yaw,new Vec3(0,0,1)),new Vec3(x,y,0),parent:parent);
    private static void Move(Pose3d pose,float x,float yaw=0)
    {using var value=At(x,0,yaw);pose.Assign(value);}
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static IReadOnlyList<Pose3d> Owned(NativeLatticePlannerRuntime r)=>Field<IReadOnlyList<Pose3d>>(r,"_goals");
    private static IReadOnlyList<(byte Id,StateC C)> Context(NativeLatticePlannerRuntime r)=>Field<IReadOnlyList<(byte,StateC)>>(r,"_goalContext");
    private static LatticePlanner Backend()=>new(new LatticeEnvironment(MotionPrimitiveSet.FromObb(Obb())!));
    private static NativeLatticePlannerRuntime Runtime(LatticePlanner backend,M4ControlTests.Rig rig,SourceLogSink? sink=null)
        =>new(backend,rig.Robot.Vision.World,()=>null,sink??((_,_,_,_)=>{}),_=>{});
    private static void Wait(NativeLatticePlannerRuntime runtime,int status=2)
    {Assert.True(SpinWait.SpinUntil(()=>runtime.CheckPlanningStatus==status,TimeSpan.FromSeconds(10)),"Planner did not publish expected status");lock(runtime.ContextMutex){} }
    private static void HardPoint(LatticeEnvironment env,float x,float half=1)
    {
        // Unit substitution: inject an already expanded hard C-space polygon into every heading.
        var polygon=new FastPolygonF(new P2f[]{new(x-half,-half),new(x+half,-half),new(x+half,half),new(x-half,half)});
        for(int i=0;i<env.Primitives.NumAngles;i++)((List<(FastPolygonF Poly,float Penalty)>)env.Bucket(i)).Add((polygon,1000));
    }

    [Fact]
    public void ActualDispatchCapturesGoalAndNumericStartBeforeCallerMutation()
    {
        // reach: production runtime dispatch with admitted robot states and host action Init.
        // Upstream action/Path conversion remains explicit MISSING; this asserts runtime capture only.
        // P2/P4: own full goals and scalar selected start are captured before notification under E0.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        using var start=At(0);using var goal=At(200,0,.3f);
        using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);Assert.True(m.LoadPlanner(Obb()));
        var runtime=m.GetNativePlanners().Lattice!;
        using var drive=new DriveToPoseAction(m){Goal=goal};
        lock(runtime.ContextMutex)
        {
            Assert.Equal(0,drive.Init());Assert.Same(runtime,m.NativePlanners!.Current);
            Assert.True(runtime.F0);Move(goal,500,1);
            drive.Dispose(); // source T4: action Abort/Stop does not release the runtime-owned admitted goal/context
            rig.State(origin:rig.Engine.Robot!.CurrentOriginId,x:50);
        }
        Wait(runtime);
        lock(runtime.ContextMutex)
        {
            var plan=Assert.IsType<LatticePlan>(runtime.TotalPlan);
            Assert.Equal((short)0,plan.Start.X);Assert.Equal((short)20,plan.States().Last().X);
            Assert.True(runtime.GetCompletePath(start,PathMotionProfile.Default,out var path,out var chosen));
            Assert.Equal(0,chosen);Assert.Equal(.3f,Assert.IsType<PathSegment.PointTurn>(path.Last()).TargetAngleRad);
        }
    }

    [Fact]
    public void InitialFilteringKeepsOriginalIdsAndRawValuesForWorkerRevalidation()
    {
        // reach: unit (substitutes missing-map policy and direct hard C-space cells to make rejection reachable;
        // real ComputePath, worker, repeated validation and GetCompletePath; no camera/map admission claim).
        // P2/P3/P5: full vector includes rejected0, raw context keeps ID1 and101.25, and worker re-rounds raw.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.Robot.Vision.World.Maps.CurrentOrigin=uint.MaxValue;
        using var start=At(0);using var rejected=At(500);using var target=At(101.25f,0,.2f);
        var backend=Backend();HardPoint(backend.Env,500);
        using var runtime=Runtime(backend,rig);
        lock(runtime.ContextMutex)
        {
            Assert.Equal(1,runtime.ComputePath(start,new[]{rejected,target}));
            Assert.Equal(2,Owned(runtime).Count);var raw=Assert.Single(Context(runtime));
            Assert.Equal((byte)1,raw.Id);Assert.Equal(101.25f,raw.C.X);
            Move(target,700,1);
            // Changed environment after initial GoalIsValid: continuous101.25 is free but rounded100 collides.
            // Revalidating rounded persistent100 would fail; raw101.25 must RoundSafe to110.
            backend.Env.ClearObstacles();HardPoint(backend.Env,100,.25f);
        }
        Wait(runtime);
        lock(runtime.ContextMutex)
        {
            Assert.Equal(1,backend.ChosenGoalId);Assert.Equal((short)11,runtime.TotalPlan!.States().Last().X);
            Assert.True(runtime.GetCompletePath(start,PathMotionProfile.Default,out var path,out var goal));
            Assert.Equal(1,goal);Assert.Equal(.2f,Assert.IsType<PathSegment.PointTurn>(path.Last()).TargetAngleRad);
            Assert.Equal(101.25f,Assert.Single(Context(runtime)).C.X);
        }
    }

    [Fact]
    public void ReplanSnapshotsNewStartAndReusesAdmittedRawGoalsWithoutRecapturingWrappers()
    {
        // reach: unit (direct runtime replan; substituted input transforms, actual worker and search).
        // P4/T2: no goal recopy; refreshed start is numeric before the replan worker is signalled.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        using var start=At(0);using var target=At(200);using var movedStart=At(100,100);
        var backend=Backend();using var runtime=Runtime(backend,rig);
        Assert.Equal(1,runtime.ComputePath(start,new[]{target}));Wait(runtime);
        lock(runtime.ContextMutex)
        {
            Move(target,500);
            Assert.Equal(1,runtime.ComputeNewPathIfNeeded(movedStart,false));
            Move(movedStart,400);
        }
        Wait(runtime);
        Assert.Equal(new LatticeState(10,10,0),runtime.TotalPlan!.Start);
        Assert.Equal((short)20,runtime.TotalPlan.States().Last().X);
    }

    [Fact]
    public void UnforcedReplanSelectsRetainedPrefixEndpointBeforeWorkerSignal()
    {
        // reach: unit (missing map and injected hard C-space cell; real retained-prefix dispatch and worker).
        // P4 0x004FEED6/DA selects the prefix endpoint as scalar context, not the supplied start wrapper.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.Robot.Vision.World.Maps.CurrentOrigin=uint.MaxValue;
        using var start=At(0);using var target=At(200);using var current=At(0);
        var backend=Backend();using var runtime=Runtime(backend,rig);
        Assert.Equal(1,runtime.ComputePath(start,new[]{target}));Wait(runtime);
        lock(runtime.ContextMutex)
        {
            HardPoint(backend.Env,150);
            Assert.Equal(1,runtime.ComputeNewPathIfNeeded(current,false));
            var retained=runtime.TotalPlan!;Assert.NotEmpty(retained.Actions);
            var endpoint=retained.States().Last();var captured=Field<StateC>(runtime,"_start");
            Assert.Equal(backend.Env.Primitives.ResolutionMm*endpoint.X,captured.X);
            Assert.Equal(backend.Env.Primitives.ResolutionMm*endpoint.Y,captured.Y);
            Assert.Equal(backend.Env.Primitives.Angles[endpoint.Theta],captured.Theta);
            Assert.NotEqual(0,captured.X);Move(current,1000);
        }
        Wait(runtime);Assert.Equal((short)20,runtime.TotalPlan!.States().Last().X);
    }

    [Fact]
    public void PendingAndRunningRequestsRejectBeforeReplacingOwnedInputs()
    {
        // reach: unit (actual mutex and a deterministic worker observation barrier; direct runtime requests).
        // P1: pending/F3 failed-lock gates reject before replacement. Barrier released before join on every exit.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        using var start=At(0);using var first=At(200);using var rejected=At(500);
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var backend=Backend();backend.PlanningStarted+=()=>{entered.Set();release.Wait();};
        using var runtime=Runtime(backend,rig);
        try
        {
            lock(runtime.ContextMutex)
            {
                Assert.Equal(1,runtime.ComputePath(start,new[]{first}));
                Assert.Equal(0,runtime.ComputePath(start,new[]{rejected}));
            }
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));Assert.True(runtime.F3);
            Assert.Equal(0,runtime.ComputePath(start,new[]{rejected}));
            release.Set();Wait(runtime);
            Assert.Equal((short)20,runtime.TotalPlan!.States().Last().X);
        }
        finally{release.Set();}
    }

    [Fact]
    public void FullCopiesShareParentsAndReplacementAssignsAppendsShrinksAndSkipsSelfVector()
    {
        // reach: unit (substitutes parented inputs and synchronous mode, real ComputePath/pose lifetimes).
        // P2/T3: separate nodes with shared parent; common prefix Assign, append copy, reverse shrink; self skip.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        using var parent=At(0);using var start=At(0);using var a=At(200,parent:parent);using var b=At(300,parent:parent);
        using var c=At(400,parent:parent);using var d=At(500,parent:parent);
        var backend=Backend();using var runtime=Runtime(backend,rig);runtime.F5=true;
        Assert.Equal(1,runtime.ComputePath(start,new[]{a,b,c}));var initial=Owned(runtime).ToArray();
        Assert.NotSame(a.Node,initial[0].Node);Assert.Same(parent.Node,initial[0].Node!.Parent);
        Assert.Equal(1,runtime.ComputePath(start,new[]{b}));
        Assert.Same(initial[0],Owned(runtime)[0]);Assert.Equal(300,Owned(runtime)[0].Translation.X);
        Assert.Null(initial[1].Node);Assert.Null(initial[2].Node);
        Assert.Equal(1,runtime.ComputePath(start,new[]{a,d}));var expanded=Owned(runtime).ToArray();
        Assert.Same(initial[0],expanded[0]);Assert.NotSame(d.Node,expanded[1].Node);
        Assert.Equal(1,runtime.ComputePath(start,Owned(runtime)));Assert.Same(expanded[0],Owned(runtime)[0]);Assert.Same(expanded[1],Owned(runtime)[1]);
        Assert.Equal((short)20,runtime.TotalPlan!.States().Last().X);
        Assert.Equal(1,runtime.ComputePath(start,new[]{a,b,c,d})); // exceeds retained capacity3: reverse-release then exact-count allocation
        Assert.All(expanded,p=>Assert.Null(p.Node));Assert.Equal(4,Owned(runtime).Count);
        Assert.NotSame(a.Node,Owned(runtime)[0].Node);Assert.Equal((short)20,runtime.TotalPlan!.States().Last().X);
    }

    [Fact]
    public void AllInvalidAcceptedRequestReplacesFullVectorAndClearsNumericContextAndOldPlan()
    {
        // reach: unit (missing-map/hard-environment substitution; actual admitted runtime replacement timing P2).
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.Robot.Vision.World.Maps.CurrentOrigin=uint.MaxValue;
        using var start=At(0);using var first=At(200);using var invalid=At(500);
        var backend=Backend();using var runtime=Runtime(backend,rig);runtime.F5=true;
        Assert.Equal(1,runtime.ComputePath(start,new[]{first}));Assert.NotEmpty(runtime.TotalPlan!.Actions);
        HardPoint(backend.Env,500);
        Assert.Equal(0,runtime.ComputePath(start,new[]{invalid}));
        Assert.Equal(500,Assert.Single(Owned(runtime)).Translation.X);Assert.Empty(Context(runtime));Assert.Empty(runtime.TotalPlan!.Actions);
    }

    [Fact]
    public void StopKeepsOwnedInputsAndTeardownJoinsBeforeReleasingThem()
    {
        // reach: unit (direct runtime, deterministic pending work under actual mutex; observes join log, not a mock join).
        // T1/T5: Stop does not release; joined0x00501954 precedes full-vector release0x005019D8/DC.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();using var start=At(0);using var target=At(200);
        var backend=Backend();IReadOnlyList<Pose3d>? owned=null;var observations=new ConcurrentQueue<bool>();
        using var runtime=Runtime(backend,rig,(_,_,key,_)=>{if(key=="LatticePlanner.DestroyThread.Joined")observations.Enqueue(owned is not null&&owned.All(p=>p.Node is not null));});
        lock(runtime.ContextMutex)
        {
            Assert.Equal(1,runtime.ComputePath(start,new[]{target}));owned=Owned(runtime).ToArray();runtime.StopPlanning();
            Assert.True(runtime.F0);Assert.All(owned,p=>Assert.NotNull(p.Node));
        }
        runtime.Dispose();Assert.True(Assert.Single(observations));Assert.All(owned,p=>Assert.Null(p.Node));
    }
}
