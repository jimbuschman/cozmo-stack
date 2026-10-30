using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// THINK-ABOUT-BEACONS-1 (M15-011, M15-084): BehaviorThinkAboutBeacons built from the manager-checked read
/// re-analysis/research/20261029-think-about-beacons-1-read.md (rows L2..L4, R1..R5, I1..I6, S1..S3, U1, T1, P1).
/// Every expected value comes from that read's addresses or from the shipped Hiking_thinkAboutBeacons.json /
/// sparksThinkAboutBeacons.json (L4), never from running the code.
/// </summary>
[Collection("ObjectID process statics")]
public class ThinkAboutBeaconsNativeTests
{
    // L4: Hiking_thinkAboutBeacons.json:13-16 and sparksThinkAboutBeacons.json:12-15 give 175 / 75.
    private const uint Radius175Bits = 0x432F0000; // 175.0f
    private const uint Radius75Bits = 0x42960000;  // 75.0f
    // I3: HikingReactToNewArea is trigger 0xF0.
    private const int HikingReactToNewArea = 0xF0;

    private static BehaviorContext Ctx(Rig rig) => new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };

    private static bool RunnableInternal(SteppedBehavior b, BehaviorContext ctx) =>
        (bool)typeof(SteppedBehavior).GetMethod("IsRunnableInternal", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(b, new object[] { ctx })!;

    // The production list FreeplayStack.Create adds (FreeplayStack.cs:124, ShippedBehaviors.Navigation).
    private static ThinkAboutBeaconsBehavior Shipped(Rig rig, string id) =>
        Assert.IsType<ThinkAboutBeaconsBehavior>(ShippedBehaviors.Navigation(rig.M).Single(b => b.Id == id));

    private static void Start(ThinkAboutBeaconsBehavior b, BehaviorContext ctx) =>
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();

    /// <summary>Puts the native MAIN pose at (100, 50, 25) mm, 0.5 rad about z, under its own origin.</summary>
    private static void PlaceRobot(Rig rig)
    {
        var native = rig.Robot.Engine.Robot!;
        using var rotation = new Pose3d(0.5, new Vec3(0, 0, 1), new Vec3(0, 0, 0));
        using var pose = new Pose3d(native.MainPose);
        pose.Node!.Transform = new Transform3d(rotation.Quaternion, new Vec3(100, 50, 25));
        native.SetPose(pose);
    }

    /// <summary>L4 through the production list: Hiking radius 175.0f, Sparks 75.0f, both trigger name HikingReactToNewArea.</summary>
    // reach: production (ShippedBehaviors.Navigation, the list FreeplayStack.Create adds).
    [Fact]
    public void L4_TheShippedConfigsKeepTheF32RadiusAndTheTriggerName()
    {
        using var rig = new Rig();
        var hiking = Shipped(rig, "Hiking_ThinkAboutBeacons");
        var sparks = Shipped(rig, "SparksThinkAboutBeacons");
        Assert.Equal(Radius175Bits, BitConverter.SingleToUInt32Bits(hiking.BeaconRadiusMm));
        Assert.Equal(Radius75Bits, BitConverter.SingleToUInt32Bits(sparks.BeaconRadiusMm));
        Assert.Equal("HikingReactToNewArea", hiking.NewAreaAnimTrigger);
        Assert.Equal("HikingReactToNewArea", sparks.NewAreaAnimTrigger);
    }

    /// <summary>
    /// R1 base +0x20 0x005BF04C, R2 base +0x24 0x0059EC12 and R3 TAB +0x28 0x005E5F4C all return 0: not runnable off treads,
    /// while robot+0x34A, or while carrying.
    /// </summary>
    // reach: unit (supplied: the IsRunnableBase robot-byte inputs robot+0x355, robot+0x34A, carrying, the +0x74 state and +0x70 unlock).
    [Fact]
    public void R1_R3_NotRunnableOffTreadsOnThePlatformOrWhileCarrying()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        bool carrying = false, offTreads = false, platform = false;
        var b = Shipped(rig, "Hiking_ThinkAboutBeacons");
        b.RobotComponent284 = () => carrying; b.RobotState355 = () => offTreads; b.RobotState34a = () => platform;
        b.RobotStateAllowsRun = () => true; b.UnlockAllowsRun = () => true;
        Assert.True(b.IsRunnableBase(ctx));
        offTreads = true; Assert.False(b.IsRunnableBase(ctx));
        offTreads = false; platform = true; Assert.False(b.IsRunnableBase(ctx));
        platform = false; carrying = true; Assert.False(b.IsRunnableBase(ctx));
    }

    /// <summary>R5 0x005E5CC2..0x005E5CD8: runnable iff GetActiveBeacon is null; nothing else is read.</summary>
    // reach: unit (direct IsRunnableInternal; a beacon is added to the whiteboard directly).
    [Fact]
    public void R5_RunnableIffThereIsNoBeacon()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Shipped(rig, "Hiking_ThinkAboutBeacons");
        Assert.True(RunnableInternal(b, ctx));
        using var pose = new Pose3d(rig.Robot.Engine.Robot!.MainPose);
        rig.M.Whiteboard.AddBeacon(pose, 10);
        Assert.False(RunnableInternal(b, ctx));
    }

    /// <summary>
    /// I2 + S1..S3 (0x005E5F12..0x005E5F36, AddBeacon body 0x0056C39C..0x0056C3DE): exactly one beacon, at the robot pose w.r.t. its
    /// root (GetPose 0x004EA398 then GetWithRespectToRoot 0x004DF628), radius [+0x128] (175.0f Hiking, 75.0f Sparks), failure time 0.
    /// </summary>
    // reach: unit (the run is started directly; the native MAIN pose is set by the fixture).
    [Theory]
    [InlineData("Hiking_ThinkAboutBeacons", Radius175Bits)]
    [InlineData("SparksThinkAboutBeacons", Radius75Bits)]
    public void S1_S3_InitAddsOneBeaconAtTheRootRelativeRobotPose(string id, uint radiusBits)
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        PlaceRobot(rig);
        var b = Shipped(rig, id);
        Start(b, ctx);
        try
        {
            var beacon = Assert.Single(rig.M.Whiteboard.Beacons);
            Assert.Same(beacon, rig.M.Whiteboard.GetActiveBeacon());
            Assert.Equal(radiusBits, BitConverter.SingleToUInt32Bits((float)beacon.RadiusMm));
            Assert.Equal(0u, BitConverter.SingleToUInt32Bits(beacon.FailedToFindLocationTimeSec));
            using var root = rig.Robot.Engine.Robot!.MainPose.FindRoot();
            Assert.Same(root.Node, beacon.Pose.Node!.Parent);                        // re-expressed w.r.t. the root (0x004DF628): parent is the root
            Assert.Equal(100, beacon.Pose.Translation.X, 3);
            Assert.Equal(50, beacon.Pose.Translation.Y, 3);
            Assert.Equal(25, beacon.Pose.Translation.Z, 3);
            Assert.Equal(0.5f, (float)beacon.Pose.AngleAroundZ, 5);
            Assert.Contains(b.Trace, l => l.Contains("Selecting new beacon"));       // I1 (best effort)
            Assert.False(RunnableInternal(b, ctx));                                  // R5: the beacon now exists
        }
        finally { b.Stop(BehaviorStopReason.Interrupted); }
    }

    /// <summary>
    /// I3..I5 (0x005E5D9C..0x005E5E3C): HikingReactToNewArea (0xF0) is played as one plain TriggerAnimationAction
    /// (trigger, 1, interruptRunning true, tracks 0, 60.0f 0x42700000, false), not lift-safe, queued on the robot ActionList.
    /// U1 (0x005BDA56..0x005BDA62): the behaviour ends with its action.
    /// </summary>
    // reach: unit (the run is started directly; the action's terminal result is supplied; the ActionList and watcher delivery are real).
    [Fact]
    public void I4_I5_OnePlainTriggerAnimationActionAndTheBehaviourEndsWithIt()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Shipped(rig, "Hiking_ThinkAboutBeacons");
        Start(b, ctx);
        var current = rig.Robot.Engine.Robot!.ActionList.GetCurrentAction();
        Assert.Equal(typeof(TriggerAnimationAction), current!.GetType());             // not TriggerLiftSafeAnimationAction
        var request = ((TriggerAnimationAction)current).Request;
        Assert.Equal(HikingReactToNewArea, (int)request.Trigger);
        Assert.Equal(1, request.Argument2);
        Assert.True(request.Argument3);
        Assert.Equal(0, request.Argument4);
        Assert.Equal(0x42700000u, BitConverter.SingleToUInt32Bits(request.Argument5));
        Assert.False(request.Argument6);
        Assert.True(b.HasCurrentAction);
        Assert.True(b.Update(ctx, 0));                                                // U1: Running while there is an action
        ((ActionRunner)current).State = EngineActionResult.Success;
        rig.Robot.Engine.Robot!.ActionList.Update();
        Assert.False(b.HasCurrentAction);
        Assert.False(b.Update(ctx, 0));                                               // U1: Complete once the action is gone
        b.Stop(BehaviorStopReason.Completed);
        Assert.Single(rig.M.Whiteboard.Beacons);
    }

    /// <summary>
    /// I3 (0x005E5D9C..0x005E5DF4): an empty trigger string, or the name resolving to Count (0x23F), starts no action; the beacon
    /// is still added (I2 is unconditional), Init returns 0 (I6) and the base Update completes on the next tick (U1). R5: not runnable after.
    /// </summary>
    // reach: unit (the constructor takes a substituted trigger name; no shipped file has one).
    [Theory]
    [InlineData("")]
    [InlineData("Count")]
    public void I3_AnEmptyOrCountTriggerAddsTheBeaconAndCompletesWithNoAction(string trigger)
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = new ThinkAboutBeaconsBehavior(rig.M, "Hiking_ThinkAboutBeacons", 175f, trigger);
        Start(b, ctx);
        Assert.False(b.InitFailed);
        Assert.Single(rig.M.Whiteboard.Beacons);
        Assert.Null(rig.Robot.Engine.Robot!.ActionList.GetCurrentAction());
        Assert.False(b.HasCurrentAction);
        Assert.False(b.Update(ctx, 0));
        b.Stop(BehaviorStopReason.Completed);
        Assert.False(RunnableInternal(b, ctx));
    }

    /// <summary>
    /// An unknown name: the read does not settle AnimationTriggerFromString's result for it (MISSING), so the stack reports it visibly
    /// and starts no action. This asserts the visible MISSING, not an engine value.
    /// </summary>
    // reach: unit (the constructor takes a substituted, unshipped trigger name).
    [Fact]
    public void I3_AnUnknownTriggerNameIsReportedMissing()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var missing = new List<string>();
        void Capture(string line) { lock (missing) missing.Add(line); }
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += Capture;
        try
        {
            var b = new ThinkAboutBeaconsBehavior(rig.M, "Hiking_ThinkAboutBeacons", 175f, "NotATrigger");
            Start(b, ctx);
            Assert.Single(rig.M.Whiteboard.Beacons);
            Assert.False(b.HasCurrentAction);
            b.Stop(BehaviorStopReason.Completed);
        }
        finally { SteppedBehavior.MissingReported -= Capture; }
        lock (missing) Assert.Contains(missing, l => l.Contains("M15-084 I3") && l.Contains("NotATrigger"));
    }

    /// <summary>T1 (base Stop 0x005BF050): Stop removes nothing; the beacon stays and TAB stays not runnable.</summary>
    // reach: unit (the run is started directly; the Stop is the real IBehavior::Stop).
    [Theory]
    [InlineData(BehaviorStopReason.Interrupted)]
    [InlineData(BehaviorStopReason.Completed)]
    public void T1_StopLeavesTheBeacon(BehaviorStopReason reason)
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Shipped(rig, "Hiking_ThinkAboutBeacons");
        Start(b, ctx);
        var beacon = rig.M.Whiteboard.GetActiveBeacon();
        Assert.NotNull(beacon);
        b.Stop(reason);
        Assert.Same(beacon, Assert.Single(rig.M.Whiteboard.Beacons));
        Assert.False(RunnableInternal(b, ctx));
    }

    /// <summary>P1 (base ResumeInternal 0x005BDA94..0x005BDAC8): with the beacon in place IsRunnableInternal is false, so resume fails and no second beacon is added.</summary>
    // reach: unit (Resume called directly with no trigger).
    [Fact]
    public void P1_ResumeIsRefusedOnceTheBeaconExists()
    {
        using var rig = new Rig();
        var ctx = Ctx(rig);
        var b = Shipped(rig, "Hiking_ThinkAboutBeacons");
        b.RobotStateAllowsRun = () => true; b.UnlockAllowsRun = () => true;
        Start(b, ctx);
        b.Stop(BehaviorStopReason.Interrupted);
        rig.Robot.Engine.Robot!.ActionList.Update();
        Assert.True(b.Resume(null, 0));
        Assert.False(b.EngineRunning);
        Assert.Single(rig.M.Whiteboard.Beacons);
    }
}
