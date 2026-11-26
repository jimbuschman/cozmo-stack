using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
using static Cozmo.Protocol.Tests.BridgeEventPushTests;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class MotionCommandOwnerGuardsTests
{
    [Fact]
    public async Task ReadyBridgeUsesCurrentNativeCommandOwnersAndShippedPlanner()
    {
        using var f = new Fixture(preloadAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        var engine = f.Rig.Engine;
        var owner = f.Robot;
        var facade = f.Rig.Robot;
        facade.SetEnableFreeplayActivity(false); f.Rig.Tick();
        using var bridge = new CozmoBridge(facade);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        Assert.True(Data(await client.Tool("get_state", new { })).GetProperty("connection").GetProperty("ready").GetBoolean());
        Assert.True(owner.FirstFullStateHandled);
        Assert.Same(owner, engine.Robot);
        Assert.True(owner.HasExternalInterface);
        // Face/object turn, pickup, roll/stack/flip/wheelie, mount, place
        // and drive all guard these same construction-owned fields.
        var runtime = owner.NativeBehaviorRuntime!;
        Assert.False(runtime.IsRetired);
        var manipulation = Assert.IsType<ManipulationSystem>(runtime.Manipulation);
        Assert.Same(f.M, manipulation);
        Assert.Same(facade.Vision, manipulation.Vision);
        Assert.NotNull(owner.CarryingOwner); Assert.NotNull(owner.DockingOwner);
        Assert.NotNull(owner.BehaviorManagerOwner); Assert.Same(owner.ActionList, owner.ActionListOwner);
        // Handler 0x0052A146 -> GetWorldOrigin 0x005128FC, constructor Delocalize 0x00510324.
        Assert.True(owner.TryGetNativeOriginOwner(owner.CurrentOriginId, out var origin));
        Assert.NotNull(origin!.Node);
        var path = facade.Motion.Path;
        Assert.Same(owner, path.OriginOwnerInput!.Invoke()); Assert.NotNull(path.PathDolerClearPath);
        // Shipped config/engine/cozmo_mprim.json; native import 0x00852014.
        var planner = Assert.IsType<LatticePlanner>(manipulation.Planner);
        Assert.NotEmpty(planner.Env.Primitives.Actions);
        Assert.NotNull(manipulation.GetNativePlanners().Lattice);
        int mark = f.Rig.Mark(); var reports = new System.Collections.Concurrent.ConcurrentQueue<EngineLogEntry>(); engine.LogEntry += reports.Enqueue;
        int request = await client.Send("tools/call", new { name = "go-to-pose", arguments = new { xMm = 150, yMm = 0, angleRadians = 0 } });
        var drive = Assert.IsType<DriveToPoseAction>(owner.ActionList.GetCurrentAction());
        Assert.Same(facade.Animations.DrivingAnimations, drive.DrivingAnimations);
        for (int i = 0; i < 100 && !f.Rig.SentSince(mark).OfType<ExecutePath>().Any(); i++)
        {
            if (manipulation.GetNativePlanners().Lattice is { CheckPlanningStatus: 1 } lattice)
                Assert.True(SpinWait.SpinUntil(() => lattice.CheckPlanningStatus != 1, TimeSpan.FromSeconds(10)));
            f.Motors();
        }
        Assert.Contains(f.Rig.SentSince(mark), m => m is ExecutePath);
        var completedPlan = Assert.Single(reports.Where(r => r.Name == "robot.plan_complete"));
        Assert.Equal(manipulation.GetNativePlanners().Current!.Name, completedPlan.Text); // 0x0064A00C..0x0064A024
        Assert.DoesNotContain(f.Rig.Log, line => line.Contains("MISSING: M12-023 GotoPose") ||
            line.Contains("MISSING: M12-032 robot.plan_complete") || line.Contains("MISSING: M13-033 native planner") ||
            line.Contains("MISSING: PathDolerOuter::ClearPath") || line.Contains("MISSING: M12-032 HandlePossibleOriginChanges"));
        Assert.True(owner.ActionList.Cancel(drive.Tag)); f.Rig.Tick();
        Assert.True(Data(await client.Receive(request)).GetProperty("admitted").GetBoolean());
    }

    [Fact]
    public async Task DirectLiftHasZeroVariabilityAndDoesNotDrawTheNativeRng()
    {
        using var f = new Fixture(sourceNvEmpty: true, keepNeedsFull: true);
        f.Rig.Robot.SetEnableFreeplayActivity(false); f.Rig.Tick();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        int liftDraws = 0;
        f.Rig.Engine.ContextRandom.WordSourceForTest = () =>
        {
            if (Environment.StackTrace.Contains("LiftInitHeightLocked", StringComparison.Ordinal)) liftDraws++;
            return 0;
        };
        int mark = f.Rig.Mark();
        int request = await client.Send("tools/call", new { name = "lift", arguments = new { heightMm = 60 } });
        var action = f.Robot.ActionList.GetCurrentAction()!;
        f.Rig.Tick();
        var wire = Assert.Single(f.Rig.SentSince(mark).OfType<SetLiftHeight>());
        // Handler 0x0052AC84..92 passes variability=0; 0x00549142..52 skips RNG.
        Assert.Equal(60f, wire.HeightMm);
        Assert.Equal(0, liftDraws);
        Assert.True(f.Robot.ActionList.Cancel(action.Tag)); f.Rig.Tick();
        Assert.True(Data(await client.Receive(request)).GetProperty("admitted").GetBoolean());
    }
}
