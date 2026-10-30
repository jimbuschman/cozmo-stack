using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// WAIT-1: BehaviorWait built from the checked read re-analysis/research/20261029-wait-1-read.md. Every expected value
/// comes from the vtable 0x010242A8 stubs (0x0059EC00..0x0059EC3C) or the shipped configs, never from running the code.
/// </summary>
[Collection("ObjectID process statics")]
public class WaitNativeTests
{
    private static string Obb()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var root = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(root, "assets", "cozmo_resources", "config", "engine", "behaviorSystem"))) return root;
            d = d.Parent;
        }
        throw new DirectoryNotFoundException("shipped behavior resources");
    }

    private static BehaviorContext Ctx(Rig rig) =>
        new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Needs = rig.Robot.Engine.Needs };

    private static bool RunnableInternal(SteppedBehavior b, BehaviorContext ctx) =>
        (bool)typeof(SteppedBehavior).GetMethod("IsRunnableInternal", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(b, new object[] { ctx })!;

    /// <summary>C3: both shipped configs (needs_Wait.json, wait.json) load as BehaviorWait through the production loader.</summary>
    // reach: production loader (ExplorerBehaviors.LoadShipped, called by FreeplayStack.Create).
    [Fact]
    public void C3_BothShippedWaitConfigsLoad()
    {
        using var rig = new Rig();
        var all = ExplorerBehaviors.LoadShipped(Obb(), rig.Vision, rig.M, rig.Robot.Engine.Needs);
        Assert.IsType<WaitBehavior>(all.Single(b => b.Id == "Needs_Wait"));
        Assert.IsType<WaitBehavior>(all.Single(b => b.Id == "Wait"));
    }

    /// <summary>
    /// R1..R4 (0x005BD864..0x005BD89C): +0x20 0x0059EC0E returns 1 (runnable off treads), +0x24 is the base 0x0059EC12
    /// returning 0 (not runnable with robot+0x34A), +0x28 0x0059EC16 returns 1 (runnable while carrying);
    /// IsRunnableInternal 0x0059EC38 returns 1.
    /// </summary>
    // reach: unit (supplied: the IsRunnableBase robot-byte inputs robot+0x355, robot+0x34A, carrying, +0x74 and +0x70).
    [Fact]
    public void R1_R4_RunnableOffTreadsAndCarryingButNotWithRobot34A()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        bool carrying = false, offTreads = false, platform = false;
        var b = new WaitBehavior("Needs_Wait");
        b.RobotComponent284 = () => carrying; b.RobotState355 = () => offTreads; b.RobotState34a = () => platform;
        b.RobotStateAllowsRun = () => true; b.UnlockAllowsRun = () => true;
        Assert.True(b.IsRunnableBase(ctx));
        Assert.True(RunnableInternal(b, ctx));
        carrying = true; Assert.True(b.IsRunnableBase(ctx));
        carrying = false; offTreads = true; Assert.True(b.IsRunnableBase(ctx));
        offTreads = false; platform = true; Assert.False(b.IsRunnableBase(ctx));
    }

    /// <summary>
    /// I1 (0x0059EC34: returns 0, starts nothing) and U1 (0x0059EC00: always Running): the run queues no action and
    /// never completes by itself; S1 (0x0059EC3C) does nothing.
    /// </summary>
    // reach: unit (the run is started directly).
    [Fact]
    public void I1_U1_StartsNoActionAndNeverEndsByItself()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = new WaitBehavior("Wait");
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.Null(rig.Robot.Engine.Robot!.ActionList.GetCurrentAction());
        for (int i = 0; i < 5; i++) Assert.True(b.Update(ctx, 1000.0 * i));
        b.Stop(BehaviorStopReason.Interrupted);
        Assert.Null(rig.Robot.Engine.Robot!.ActionList.GetCurrentAction());
    }
}
