using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// REACT-UNEXPECTED-MOVEMENT-1 / M7-031: BehaviorReactToUnexpectedMovement built from the approved read
/// re-analysis/research/20261029-react-unexpected-movement-1-read.md (rows C1, H2, H3, R1..R3, R5, I1, I3..I6, K1, K2, U1, P1).
/// Every expected value comes from that read's addresses, never from running the code.
/// </summary>
[Collection("ObjectID process statics")]
public class ReactToUnexpectedMovementNativeTests
{
    private sealed class ImmediateAction : ActionRunner
    {
        internal ImmediateAction() : base(8, 0) { }
        protected override string ActionName => "test immediate action";
        public override bool CanInterrupt() => false;
        public override uint CheckIfDone() => 0;
    }

    private static string Resources()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var root = Path.Combine(d.FullName, "re-analysis", "obb", "assets", "cozmo_resources");
            if (Directory.Exists(Path.Combine(root, "config", "engine", "behaviorSystem"))) return root;
            d = d.Parent;
        }
        throw new DirectoryNotFoundException("shipped behavior resources");
    }

    private static BehaviorContext Ctx(Rig rig) =>
        new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Needs = rig.Robot.Engine.Needs };

    // The production construction (ShippedBehaviors.Reactions, called by BehaviorManager.InitializeRobotReactions).
    private static ReactToUnexpectedMovementBehavior Production(Rig rig) =>
        Assert.IsType<ReactToUnexpectedMovementBehavior>(
            Assert.Single(ShippedBehaviors.Reactions(rig.Robot, obbRoot: Resources()), r => r.Strategy.Trigger == ReactionTrigger.UnexpectedMovement).Behavior);

    /// <summary>
    /// Supplied boundary: the in-engine delivery Sensors.cs makes on every detector fire (D7/D9:
    /// <c>UnexpectedMovementDetectedInternal</c>), invoked directly. The detector itself (M10) is not exercised here.
    /// </summary>
    private static void Broadcast(Rig rig, UnexpectedMovementSide side)
    {
        var field = typeof(CozmoSensors).GetField("UnexpectedMovementDetectedInternal", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var handler = (Action<UnexpectedMovementReport>?)field.GetValue(rig.Robot.Sensors);
        Assert.NotNull(handler);
        handler!(new UnexpectedMovementReport(1234, UnexpectedMovementType.TurnedButStopped, side, 0f, 0f, 11));
    }

    private static TriggerAnimationAction Start(Rig rig, ReactToUnexpectedMovementBehavior b, BehaviorContext ctx)
    {
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        return Assert.IsType<TriggerLiftSafeAnimationAction>(rig.Robot.Engine.Robot!.ActionList.GetCurrentAction());
    }

    private static bool RunnableInternal(SteppedBehavior b, BehaviorContext ctx) =>
        (bool)typeof(SteppedBehavior).GetMethod("IsRunnableInternal", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(b, new object[] { ctx })!;

    /// <summary>
    /// R1 (base +0x20 0x005BF04C returns 0): not runnable off treads. R2 (base +0x24 0x0059EC12 returns 0): not runnable while robot+0x34A.
    /// R3 (+0x28 0x00609C1A returns 1): runnable while carrying. R5 (0x00609ABC): IsRunnableInternal is true.
    /// </summary>
    // reach: unit (supplied: the IsRunnableBase robot-byte inputs robot+0x355, robot+0x34A, carrying, +0x74 and +0x70).
    [Fact]
    public void R1_R5_NotOffTreadsNorWith34A_ButRunnableWhileCarrying()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Production(rig);
        bool carrying = false, offTreads = false, platform = false;
        b.RobotComponent284 = () => carrying; b.RobotState355 = () => offTreads; b.RobotState34a = () => platform;
        b.RobotStateAllowsRun = () => true; b.UnlockAllowsRun = () => true;
        Assert.True(b.IsRunnableBase(ctx));
        carrying = true; Assert.True(b.IsRunnableBase(ctx));
        carrying = false; offTreads = true; Assert.False(b.IsRunnableBase(ctx));
        offTreads = false; platform = true; Assert.False(b.IsRunnableBase(ctx));
        Assert.True(RunnableInternal(b, ctx));
    }

    /// <summary>
    /// I3 (0x00609B30..0x00609B44) with I2 ([[robot+0x264]+0x30]+0x14): Repair (0) → 0x1AE ReactToUnexpectedMovement_Severe_Repair,
    /// Energy (1) → 0x1AD _Severe_Energy, Play (2) or none (3, Count) → 0x1AC ReactToUnexpectedMovement.
    /// I5 (0x00609B12..0x00609B4E): one TriggerLiftSafeAnimationAction(trigger, 1, true, lock, 60.0f = 0x42700000, false).
    /// </summary>
    // reach: production construction (ShippedBehaviors.Reactions) and the real ActionList admission; the run is started directly (unit).
    [Theory]
    [InlineData(0, 0x1AE)]
    [InlineData(1, 0x1AD)]
    [InlineData(2, 0x1AC)]
    [InlineData(-1, 0x1AC)]
    public void I3_I5_TheSevereNeedPicksTheTriggerOfOneLiftSafeAction(int need, int expectedTrigger)
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Production(rig);
        if (need >= 0) rig.Robot.Engine.Needs.SevereNeeds.Set((NeedId)need);
        Assert.Equal(need >= 0 ? need : 3, rig.Robot.Engine.Needs.SevereNeeds.Selected);
        var action = Start(rig, b, ctx);
        try
        {
            var r = action.Request;
            Assert.Equal(expectedTrigger, (int)r.Trigger);
            Assert.Equal(1, r.Argument2);
            Assert.True(r.Argument3);
            Assert.Equal(0, r.Argument4);                                           // I4: side 0 (C1 ctor) is not BACK
            Assert.Equal(0x42700000u, BitConverter.SingleToUInt32Bits(r.Argument5));
            Assert.False(r.Argument6);
            Assert.True(b.HasCurrentAction);
        }
        finally { b.Stop(BehaviorStopReason.Interrupted); }
    }

    /// <summary>The trigger enum names match the read's I3 values (0x1AC, 0x1AD, 0x1AE).</summary>
    [Fact]
    public void I3_TheTriggerValuesAreTheEngineIds()
    {
        Assert.Equal(0x1AC, (int)AnimationTrigger.ReactToUnexpectedMovement);
        Assert.Equal(0x1AD, (int)AnimationTrigger.ReactToUnexpectedMovement_Severe_Energy);
        Assert.Equal(0x1AE, (int)AnimationTrigger.ReactToUnexpectedMovement_Severe_Repair);
    }

    /// <summary>I4 (0x00609B1E..0x00609B22, cmp r7,#2; moveq r2,#4): tracksToLock = 4 (BODY) iff the side is 2 (BACK), else 0.</summary>
    // reach: unit (supplied: the in-engine broadcast; production construction and real ActionList admission).
    [Theory]
    [InlineData(UnexpectedMovementSide.Unknown, 0)]
    [InlineData(UnexpectedMovementSide.Front, 0)]
    [InlineData(UnexpectedMovementSide.Back, 4)]
    [InlineData(UnexpectedMovementSide.Left, 0)]
    [InlineData(UnexpectedMovementSide.Right, 0)]
    public void I4_BodyIsLockedOnlyForBack(UnexpectedMovementSide side, int expectedLock)
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Production(rig);
        Broadcast(rig, side);
        var action = Start(rig, b, ctx);
        try
        {
            Assert.Equal(expectedLock, action.Request.Argument4);
            Assert.Equal((uint)expectedLock, action.RequiredTrackMask & 4u);
        }
        finally { b.Stop(BehaviorStopReason.Interrupted); }
    }

    /// <summary>
    /// C1 (+0x11C = 0 in the ctor 0x00609A58..0x00609A9A) and H2 (AlwaysHandle 0x00609BF8..0x00609C0A: +0x11C = movementSide of the last
    /// broadcast, whether or not the behaviour runs): the field starts Unknown, follows each broadcast while idle, and is kept across a run.
    /// </summary>
    // reach: unit (supplied: the in-engine broadcast).
    [Fact]
    public void H2_TheSideIsTheLastBroadcastAndIsKeptAcrossRuns()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Production(rig);
        Assert.Equal(UnexpectedMovementSide.Unknown, b.Side);
        Broadcast(rig, UnexpectedMovementSide.Left);
        Assert.Equal(UnexpectedMovementSide.Left, b.Side);
        Broadcast(rig, UnexpectedMovementSide.Back);
        Assert.Equal(UnexpectedMovementSide.Back, b.Side);
        var first = Start(rig, b, ctx);
        Assert.Equal(4, first.Request.Argument4);
        b.Stop(BehaviorStopReason.Interrupted);
        rig.Robot.Engine.Robot!.ActionList.Update();
        Assert.Equal(UnexpectedMovementSide.Back, b.Side);                          // not reset by Stop (T1) or Init
        var second = Start(rig, b, ctx);
        Assert.Equal(4, second.Request.Argument4);
        b.Stop(BehaviorStopReason.Interrupted);
    }

    /// <summary>
    /// K1 (wrapper 0x005BF790 ignores the ActionResult) and K2 (0x00609C5A..0x00609C60: BehaviorObjectiveAchieved(0x25 ReactedToUnexpectedMovement, true)):
    /// the objective is delivered once whatever the result; U1: the behaviour then ends with its action.
    /// </summary>
    // reach: unit (supplied: the action's terminal result; the watcher delivery and HandleActionComplete are real).
    [Theory]
    [InlineData(0u)]           // SUCCESS
    [InlineData(0x03000004u)]  // a failure category (3)
    public void K1_K2_TheObjectiveIsReportedOnAnyResult(uint result)
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Production(rig);
        var objectives = new List<object?>();
        using var subscription = rig.Robot.Engine.AnimationMessages.SubscribeMessage(0x8B, true, value => objectives.Add(value));
        var action = Start(rig, b, ctx);
        Assert.Empty(objectives);
        action.State = result; rig.Robot.Engine.Robot!.ActionList.Update();
        Assert.Equal(0x25u, Assert.IsType<uint>(Assert.Single(objectives)));
        Assert.False(b.HasCurrentAction);
        Assert.False(b.Update(ctx, 0));                                            // U1: ended with the action
        b.Stop(BehaviorStopReason.Completed);
    }

    /// <summary>
    /// P1 (base ResumeInternal 0x005BDA94..0x005BDAC8: IsRunnableBase && IsRunnableInternal, then Init again): a resumed RUM fires I1's
    /// emotion event again and starts a new lift-safe action.
    /// </summary>
    // reach: unit (Resume called directly with no trigger).
    [Fact]
    public void P1_ResumeRerunsInit()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Production(rig);
        b.RobotStateAllowsRun = () => true; b.UnlockAllowsRun = () => true;
        Start(rig, b, ctx);
        b.Stop(BehaviorStopReason.Interrupted);
        rig.Robot.Engine.Robot!.ActionList.Update();
        Assert.False(b.HasCurrentAction);
        Assert.False(b.Resume(null, 0));                                           // 0 = resumed
        Assert.True(b.EngineRunning);
        Assert.True(b.HasCurrentAction);
        Assert.IsType<TriggerLiftSafeAnimationAction>(rig.Robot.Engine.Robot!.ActionList.GetCurrentAction());
        b.Stop(BehaviorStopReason.Interrupted);
    }

    /// <summary>
    /// Stack diagnostic, not an engine row: the config-free instance (ShippedBehaviors.Implementable) has no robot to subscribe to, so C1/H2
    /// cannot hold; its Init is refused visibly (InitFailed) instead of reacting with a silent side 0. No action is queued.
    /// </summary>
    // reach: unit (the unbound constructor).
    [Fact]
    public void Unbound_InitFailsVisiblyWithoutQueueingAnAction()
    {
        using var rig = new Rig();
        var b = Assert.IsType<ReactToUnexpectedMovementBehavior>(ShippedBehaviors.Implementable().Single(x => x.Id == "ReactToUnexpectedMovement"));
        b.StartAsync(Ctx(rig), new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.True(b.InitFailed);
        Assert.False(b.EngineRunning);
        Assert.Null(rig.Robot.Engine.Robot!.ActionList.GetCurrentAction());
    }

    /// <summary>
    /// H3 (D9 + M10 C3): the behaviour's AlwaysHandle runs inside the broadcast before the manager switches, so the Init the manager
    /// starts sees the triggering broadcast's side.
    /// </summary>
    // reach: production manager and strategy (BehaviorManagerOwner from the shipped map); supplied: the in-engine broadcast.
    [Fact]
    public void H3_TheManagerStartedRunSeesTheTriggeringSide()
    {
        using var rig = new Rig(resources: Resources());
        var native = rig.Robot.Engine.Robot!;
        var manager = native.BehaviorManagerOwner!;
        var registration = Assert.Single(manager.Reactions.Where(r => r.Strategy.Trigger == ReactionTrigger.UnexpectedMovement));
        var b = Assert.IsType<ReactToUnexpectedMovementBehavior>(registration.Behavior);
        rig.Robot.Animations.LoadFrom(Path.Combine(Resources(), "assets")); // SteppedBehavior.IsRunnable needs a loaded library
        var previous = M10Support.RunnableBehaviour("rum-previous");
        manager.Add(previous);
        Assert.True(manager.StartAsync(previous.Id, 0).GetAwaiter().GetResult());
        // S4 / M10 C3: the manager walks the strategies only after the first queued action (sticky); open it with a test action that completes at once.
        Assert.Equal(0, native.ActionList.QueueAction(QueueActionPosition.Now, new ImmediateAction(), 0));
        Assert.Null(manager.CheckReactions(rig.Clock.NowMs / 1000.0));
        native.ActionList.Update();
        Assert.Null(native.ActionList.GetCurrentAction());
        Broadcast(rig, UnexpectedMovementSide.Back);
        var fired = manager.CheckReactions(rig.Clock.NowMs / 1000.0);
        Assert.NotNull(fired);
        Assert.Same(b, manager.Current);
        var action = Assert.IsType<TriggerLiftSafeAnimationAction>(native.ActionList.GetCurrentAction());
        Assert.Equal(4, action.Request.Argument4);
    }
}
