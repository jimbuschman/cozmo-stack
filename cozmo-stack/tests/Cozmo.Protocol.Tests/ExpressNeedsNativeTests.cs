using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// EXPRESS-NEEDS-1: BehaviorExpressNeeds built from the approved read re-analysis/research/20261029-express-needs-1-read.md
/// (rows C2, C8, C9, R1, R2, R4, R5, R6, I1..I5, P1, S1). Every expected value comes from that read's addresses or from the
/// shipped behaviors/freeplay/needs/*.json and needs_config.json, never from running the code.
/// </summary>
[Collection("ObjectID process statics")]
public class ExpressNeedsNativeTests
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

    // The production loader FreeplayStack.Create calls (FreeplayStack.cs: ExplorerBehaviors.LoadShipped).
    private static ExpressNeedsBehavior Shipped(Rig rig, string id) =>
        Assert.IsType<ExpressNeedsBehavior>(ExplorerBehaviors.LoadShipped(Obb(), rig.Vision, rig.M, rig.Robot.Engine.Needs).Single(b => b.Id == id));

    private static BehaviorContext Ctx(Rig rig) =>
        new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Needs = rig.Robot.Engine.Needs };

    private static bool RunnableInternal(SteppedBehavior b, BehaviorContext ctx) =>
        (bool)typeof(SteppedBehavior).GetMethod("IsRunnableInternal", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(b, new object[] { ctx })!;

    private static void SetEngineSeconds(Rig rig, long ns) => rig.Robot.Engine.Timer.UpdateTime(ns);

    private static bool Ignored(CompoundAction compound, ActionRunner child)
    {
        var map = (System.Collections.IDictionary)typeof(CompoundAction).GetField("_ignorePredicates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(compound)!;
        return map.Contains(child);
    }

    private static CompoundActionSequential Start(Rig rig, ExpressNeedsBehavior b, BehaviorContext ctx)
    {
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        return Assert.IsType<CompoundActionSequential>(rig.Robot.Engine.Robot!.ActionList.GetCurrentAction());
    }

    /// <summary>
    /// R1 (0x005BD864..0x005BD89C): base +0x20 0x005BF04C returns 0 (off treads), base +0x24 0x0059EC12 returns 0 (robot+0x34A),
    /// EN's +0x28 0x005EE860 returns 0: not runnable while carrying.
    /// </summary>
    // reach: unit (supplied: the IsRunnableBase robot-byte inputs robot+0x355, robot+0x34A, carrying, the +0x74 state and +0x70 unlock).
    [Fact]
    public void R1_NotRunnableWhileCarryingOffTreadsOrOnThePlatform()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        bool carrying = false, offTreads = false, platform = false;
        var b = Shipped(rig, "Needs_MildLowEnergyRequest");
        b.RobotComponent284 = () => carrying; b.RobotState355 = () => offTreads; b.RobotState34a = () => platform;
        b.RobotStateAllowsRun = () => true; b.UnlockAllowsRun = () => true;
        Assert.True(b.IsRunnableBase(ctx));
        carrying = true; Assert.False(b.IsRunnableBase(ctx));
        carrying = false; offTreads = true; Assert.False(b.IsRunnableBase(ctx));
        offTreads = false; platform = true; Assert.False(b.IsRunnableBase(ctx));
    }

    /// <summary>
    /// R5 (0x005EE5B0..0x005EE5DC): runnable iff (GetCooldownSec + [+0x138]) &lt; BaseStationTimer seconds, f32, strict. C2: +0x138 starts at 0.0f.
    /// R6: the shipped MildLowEnergyRequest graph (C9: (0,20)(0.1,20)(0.5,60)(1,60)) at level 0.55 lies between the 60 nodes, so 60.0f.
    /// Energy 0.55 is Warning (needs_config.json: Warning 0.21, Normal 0.6). So 60.0f + 0.0f &lt; 60.0f is false and the next float is true.
    /// </summary>
    // reach: unit (direct IsRunnableInternal; the engine timer value is a supplied input).
    [Fact]
    public void R5_TheCooldownIsAStrictF32CompareFromZero()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Shipped(rig, "Needs_MildLowEnergyRequest");
        rig.Robot.Engine.Needs.SetLevel(NeedId.Energy, 0.55);
        Assert.Equal(0u, BitConverter.SingleToUInt32Bits(b.LastSuccessSec));            // C2 +0x138 = 0.0f
        Assert.Equal(0x42700000u, BitConverter.SingleToUInt32Bits((float)b.CooldownSec)); // 60.0f
        SetEngineSeconds(rig, 60_000_000_000);
        Assert.Equal(0x42700000u, BitConverter.SingleToUInt32Bits(rig.Robot.Engine.Timer.SecondsF));
        Assert.False(RunnableInternal(b, ctx));                                           // 60 < 60 is false
        SetEngineSeconds(rig, 60_000_004_000);
        Assert.Equal(0x42700001u, BitConverter.SingleToUInt32Bits(rig.Robot.Engine.Timer.SecondsF));
        Assert.True(RunnableInternal(b, ctx));
    }

    /// <summary>
    /// I1..I4 (0x005EE690..0x005EE788) with C8/C9: one CompoundActionSequential; first TurnTowardsLastFacePoseAction (face 0, Radians 0x40490FDB,
    /// sayName false) added with ignoreFailure 1 (movs r3,#1 at 0x005EE6DA) even though no face is known; then one TriggerAnimationAction per trigger
    /// in array order (Bored: NothingToDoBoredIntro 329, NothingToDoBoredEvent 327, NothingToDoBoredOutro 330), each (1, true, 0, 60.0f 0x42700000,
    /// false), not lift-safe, ignoreFailure 0 (movs r3,#0 at 0x005EE736).
    /// </summary>
    // reach: production loader (ExplorerBehaviors.LoadShipped) and the real ActionList admission; the run is started directly (unit).
    [Fact]
    public void I1_I4_TheBoredConfigQueuesOneCompoundTurnFirstThenItsThreeTriggers()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Shipped(rig, "Needs_SevereLowPlayBored");
        Assert.Equal(new[] { 329, 327, 330 }, b.Triggers.Select(t => (int)t));
        Assert.False(rig.Vision.Faces.HasAnyFaces());                                      // no face known: the turn is still added
        var compound = Start(rig, b, ctx);
        try
        {
            Assert.Equal(4, compound.Children.Count);
            var turn = Assert.IsType<NativeLastFaceRunner>(compound.Children[0]);
            Assert.Equal(0x40490FDBu, BitConverter.SingleToUInt32Bits(turn.MaximumTurn));
            Assert.False(turn.SayName);
            Assert.Equal(0, turn.RequestedId);
            Assert.True(Ignored(compound, turn));
            var expected = new[] { 329, 327, 330 };
            for (int i = 0; i < 3; i++)
            {
                var anim = compound.Children[i + 1];
                Assert.Equal(typeof(TriggerAnimationAction), anim.GetType());               // not TriggerLiftSafeAnimationAction
                var request = ((TriggerAnimationAction)anim).Request;
                Assert.Equal(expected[i], (int)request.Trigger);
                Assert.Equal(1, request.Argument2);
                Assert.True(request.Argument3);
                Assert.Equal(0, request.Argument4);
                Assert.Equal(0x42700000u, BitConverter.SingleToUInt32Bits(request.Argument5));
                Assert.False(request.Argument6);
                Assert.False(Ignored(compound, anim));
            }
            Assert.True(b.HasCurrentAction);
        }
        finally { b.Stop(BehaviorStopReason.Interrupted); }
    }

    /// <summary>
    /// I5 (0x005EE89E..0x005EE8B6): only a SUCCESS category (byte 3 == 0) stores +0x138 = BaseStationTimer seconds; then R5 with c = 60.0f:
    /// 60.0f + 100.0f = 160.0f, so 160.0f is not runnable and the next float is. U1: the behaviour ends with its action.
    /// </summary>
    // reach: unit (supplied: the compound's terminal result; the watcher delivery and HandleActionComplete are real).
    [Fact]
    public void I5_ASuccessStampsTheEngineSecondsAndRestartsTheCooldown()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Shipped(rig, "Needs_MildLowEnergyRequest");
        rig.Robot.Engine.Needs.SetLevel(NeedId.Energy, 0.55);
        var compound = Start(rig, b, ctx);
        SetEngineSeconds(rig, 100_000_000_000);
        compound.State = EngineActionResult.Success; rig.Robot.Engine.Robot!.ActionList.Update();
        Assert.Equal(0x42C80000u, BitConverter.SingleToUInt32Bits(b.LastSuccessSec));     // 100.0f
        Assert.False(b.HasCurrentAction);
        Assert.False(b.Update(ctx, 0));                                                   // U1: ended with the action
        b.Stop(BehaviorStopReason.Completed);
        SetEngineSeconds(rig, 160_000_000_000);
        Assert.False(RunnableInternal(b, ctx));
        SetEngineSeconds(rig, 160_000_016_000);
        Assert.Equal(0x43200001u, BitConverter.SingleToUInt32Bits(rig.Robot.Engine.Timer.SecondsF));
        Assert.True(RunnableInternal(b, ctx));
    }

    /// <summary>I5: a failure category (here 0x03000004, category 3) leaves +0x138 unchanged (0.0f from C2).</summary>
    // reach: unit (supplied: the compound's terminal result; the watcher delivery and HandleActionComplete are real).
    [Fact]
    public void I5_AFailureLeavesTheStampUnchanged()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Shipped(rig, "Needs_MildLowEnergyRequest");
        var compound = Start(rig, b, ctx);
        SetEngineSeconds(rig, 100_000_000_000);
        compound.State = 0x03000004; rig.Robot.Engine.Robot!.ActionList.Update();
        Assert.False(b.HasCurrentAction);
        Assert.Equal(0u, BitConverter.SingleToUInt32Bits(b.LastSuccessSec));
        b.Stop(BehaviorStopReason.Completed);
    }

    /// <summary>I5 with S1: a Stop while the compound runs cancels it; no SUCCESS reaches the callback, so +0x138 stays 0.0f.</summary>
    // reach: unit (the run is started directly; the Stop and the ActionList cancel are real).
    [Fact]
    public void I5_ACancelledRunLeavesTheStampUnchanged()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Shipped(rig, "Needs_MildLowEnergyRequest");
        Start(rig, b, ctx);
        SetEngineSeconds(rig, 100_000_000_000);
        b.Stop(BehaviorStopReason.Interrupted);
        rig.Robot.Engine.Robot!.ActionList.Update();
        Assert.False(b.HasCurrentAction);
        Assert.Equal(0u, BitConverter.SingleToUInt32Bits(b.LastSuccessSec));
    }

    /// <summary>P1 (0x005EE7D4..0x005EE7D6, movs r0,#1; bx lr): ResumeInternal returns 1, so IBehavior::Resume fails and +0xa1 is cleared.</summary>
    // reach: unit (Resume called directly with no trigger).
    [Fact]
    public void P1_ResumeIsAlwaysRefused()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Shipped(rig, "Needs_MildLowEnergyRequest");
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        b.Stop(BehaviorStopReason.Interrupted);
        Assert.True(b.Resume(null, 0));
        Assert.False(b.EngineRunning);
        Assert.False(b.HasCurrentAction);                                                 // no InitInternal re-run, no new action
    }

    /// <summary>
    /// S1 (0x005EE7D8..0x005EE7E8) with C7: the shipped ForcedGetOut sets shouldClearExpressedState, so any Stop calls ClearSevereNeedExpression;
    /// MildLowEnergyRequest (absent key, 0) leaves the selection.
    /// </summary>
    // reach: production loader and the real IBehavior::Stop; the run is started directly (unit).
    [Theory]
    [InlineData(BehaviorStopReason.Interrupted)]
    [InlineData(BehaviorStopReason.Completed)]
    public void S1_StopClearsTheSelectionOnlyWhenShouldClear(BehaviorStopReason reason)
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var needs = rig.Robot.Engine.Needs;
        var keep = Shipped(rig, "Needs_MildLowEnergyRequest");
        needs.SetSevereExpressed(NeedId.Repair, true);
        keep.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        keep.Stop(reason);
        Assert.True(needs.IsSevereExpressed(NeedId.Repair));
        var clear = Shipped(rig, "Needs_SevereLowEnergyForcedGetOut");
        Assert.True(clear.ShouldClearExpressedState);
        clear.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        clear.Stop(reason);
        Assert.False(needs.IsSevereExpressed(NeedId.Repair));
    }
}
