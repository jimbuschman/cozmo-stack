using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// ACKNOWLEDGE-FACE-1: BehaviorAcknowledgeFace built from the approved read re-analysis/research/20261029-acknowledge-face-1-read.md (rows C1..S1).
/// Every expected value below comes from that read's addresses, never from running the code.
/// </summary>
public class AcknowledgeFaceNativeTests
{
    private sealed class Recorder : IReactToFaceListener
    {
        public readonly List<int> Finished = new();
        public int Cleared;
        public void FinishedReactingToFace(int faceId) => Finished.Add(faceId);
        public void ClearDesiredTargets() => Cleared++;
    }

    private static BehaviorContext Ctx(Rig rig, double nowSec = 100.0) =>
        new() { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), ClockSec = () => nowSec };

    private static Rig FaceRig(params (int Id, Vec3 Head, string? Name)[] faces)
    {
        var rig = new Rig();
        rig.Vision.FaceDetector = rig.FaceDetector;
        rig.FaceDetector.Faces.AddRange(faces);
        rig.Head = 0.3f;
        return rig;
    }

    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "assets", "animationGroups"))) return r;
            d = d.Parent;
        }
        return null;
    }

    private static bool RunnableInternal(SteppedBehavior b, BehaviorContext ctx) =>
        (bool)typeof(SteppedBehavior).GetMethod("IsRunnableInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(b, new object[] { ctx })!;

    private static void Drive(Rig rig, SteppedBehavior b, BehaviorContext ctx, bool start = true, double stepMs = 33)
    {
        double t = 0;
        rig.Robot.Animations.ManualTicking = true;
        rig.Robot.Animations.ClockMs = () => rig.Clock.NowMs;
        b.ActionTaskRunner = SignalTestContext.Schedule;
        if (start) b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        int steps = 0;
        while (b.Update(ctx, t))
        {
            rig.Pump();
            rig.Frame();
            t += stepMs;
            if (++steps > 10000) throw new TimeoutException("behaviour did not finish: " + string.Join(" | ", b.Trace));
            SignalTestContext.AdvanceBehavior(b);
        }
        b.Stop(BehaviorStopReason.Completed);
    }

    private static List<EngineLogEntry> Objectives(Rig rig)
    {
        var list = new List<EngineLogEntry>();
        rig.Robot.Engine.LogEntry += e => { if (e.Name == "IBehavior.BehaviorObjectiveAchieved") lock (list) list.Add(e); };
        return list;
    }

    /// <summary>
    /// B1/B2/F1/F4: GetBestFaceToTrack(set, false) 0x0056C110..0x0056C27E ranks by |head - robot+0x2FC| + 3.0 * |body|, so the face with the smaller
    /// cost goes first; FinishIteration 0x00602C34..0x00602C96 erases it, notifies listeners (slot 0), reports BehaviorObjectiveAchieved(0x1A) and
    /// begins again; the second face is then the set's only element (returned unchecked); the loop ends at 0 (empty set).
    /// </summary>
    // reach: unit (supplied: the target set through SetTargets, the strategy's T4 copy; the faces come from the fake detector through a real frame).
    [Fact]
    public void B2_F1_TwoFacesAreAcknowledgedInCostOrderWithOneObjectiveEach()
    {
        using var signals = SignalTestContext.Install();
        // Real face poses: face 9 is about 7 degrees off the heading (body term ~3*0.124), face 7 about 21 degrees (~3*0.359), at the same range
        // and height, so B2's cost puts face 9 first.
        using var rig = FaceRig((7, new Vec3(400, 150, 250), null), (9, new Vec3(400, -50, 250), null));
        rig.Frame();
        var ctx = Ctx(rig);
        var b = new AcknowledgeFaceBehavior(rig.Vision);
        var rec = new Recorder(); b.AddListener(rec);
        var objectives = Objectives(rig);
        b.SetTargets(new[] { 7, 9 });
        Drive(rig, b, ctx);
        Assert.Equal(new[] { 9, 7 }, rec.Finished);
        Assert.Empty(b.Targets);
        Assert.Equal(2, objectives.Count);
        Assert.All(objectives, e => Assert.Contains("Objective:ReactedAcknowledgedFace", e.Text));
        var trace = b.Trace.ToList();
        Assert.True(trace.FindIndex(l => l.StartsWith("start TurnTowardsFace(9)")) < trace.FindIndex(l => l.StartsWith("start TurnTowardsFace(7)")));
        Assert.Equal(2, trace.Count(l => l.StartsWith("start TurnTowardsFace(")));
        Assert.Equal(0x1Au, AcknowledgeFaceBehavior.ObjectiveReactedAcknowledgedFace);
    }

    /// <summary>
    /// B2 (0x0056C110..0x0056C27E): empty -> 0; a single element unchecked; ascending walk from FLT_MAX with a strict less-than; a face GetFace
    /// lacks, or one with no robot pose to take it relative to, is skipped, and with every face skipped the result stays 0.
    /// </summary>
    // reach: unit (real face poses from the fake detector through a real frame; the robot pose and robot+0x2FC are the rig's).
    [Fact]
    public void B2_GetBestFaceToTrackWalksTheSetWithRealPoses()
    {
        using var rig = FaceRig((7, new Vec3(400, 150, 250), null), (9, new Vec3(400, -50, 250), null));
        rig.Frame();
        var robot = rig.Vision.History.Latest?.RobotPose;
        Assert.NotNull(robot);
        float head = rig.Robot.Motion.HeadAngleRad;
        Assert.Equal(9, BestFaceToTrack.Get(new[] { 7, 9 }, rig.Vision.Faces, robot, head, false));
        Assert.Equal(0, BestFaceToTrack.Get(Array.Empty<int>(), rig.Vision.Faces, robot, head, false));
        Assert.Equal(42, BestFaceToTrack.Get(new[] { 42 }, rig.Vision.Faces, robot, head, false));     // unchecked
        Assert.Equal(7, BestFaceToTrack.Get(new[] { 3, 7 }, rig.Vision.Faces, robot, head, false));    // 3: GetFace null -> skipped
        Assert.Equal(0, BestFaceToTrack.Get(new[] { 3, 4 }, rig.Vision.Faces, robot, head, false));    // all skipped -> 0
        Assert.Equal(0, BestFaceToTrack.Get(new[] { 7, 9 }, rig.Vision.Faces, null, head, false));     // no robot pose -> all skipped -> 0
        Assert.Equal(0x7F7FFFFFu, BitConverter.SingleToUInt32Bits(BestFaceToTrack.InitialBest));         // literal 0x0056C158
    }

    /// <summary>
    /// B2's cost (0x0056C1D0..0x0056C22E, f32) on literal relative translations. Derivations:
    /// <list type="bullet">
    /// <item>For p = (x, y, 49) with sqrt(x*x+y*y) + 13 &gt; 300, GetAbsoluteHeadAngleToLookAtPose (0x0054B428..0x0054B564) has h = 49 + (-49) = 0,
    /// t = (300 - D)/150 &lt; 0 and u = -0/-10, so every conditional pick is 0.0f and atan2f(0, D) = 0: head = 0.0698132f (0x3D8EFA35).</item>
    /// <item>(400, 0, 49), robot+0x2FC 0, named: headTerm = |0x3D8EFA35 - 0| , body = atan2f(0, 400) = 0, cost = 0x3D8EFA35.</item>
    /// <item>(400, 400, 49), robot+0x2FC 0, named: body = atan2f(400, 400) = 0x3F490FDB (pi/4); bodyTerm = 0x3F490FDB * 3.0f;
    /// cost = (0x3D8EFA35 + bodyTerm) + 0 = 2.4260077f (0x401B43B6), f32 throughout.</item>
    /// <item>The same face unnamed with the bool true: s18 = 1000.0f, cost = 2.4260077f + 1000.0f = 1002.426f (0x447A9B44); with the bool false
    /// (AcknowledgeFace's call) s18 = 0.0f and the cost is 0x401B43B6 again.</item>
    /// <item>(400, 0, 49) with robot+0x2FC 0.5f: headTerm = |Radians(0x3D8EFA35 - 0.5f)| = 0.43018684f (0x3EDC4173).</item>
    /// </list>
    /// </summary>
    // reach: unit (direct call of the cost with literal translations).
    [Fact]
    public void B2_TheCostIsTheEnginesF32Formula()
    {
        static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);
        Assert.Equal(0x3D8EFA35u, Bits(BestFaceToTrack.Cost(new Vec3(400, 0, 49), 0f, true, false)));
        Assert.Equal(0x401B43B6u, Bits(BestFaceToTrack.Cost(new Vec3(400, 400, 49), 0f, true, false)));
        Assert.Equal(0x447A9B44u, Bits(BestFaceToTrack.Cost(new Vec3(400, 400, 49), 0f, false, true)));
        Assert.Equal(0x401B43B6u, Bits(BestFaceToTrack.Cost(new Vec3(400, 400, 49), 0f, false, false)));
        Assert.Equal(0x401B43B6u, Bits(BestFaceToTrack.Cost(new Vec3(400, 400, 49), 0f, true, true)));   // named: no surcharge
        Assert.Equal(0x3EDC4173u, Bits(BestFaceToTrack.Cost(new Vec3(400, 0, 49), 0.5f, true, false)));
    }

    /// <summary>B10/F1: FinishIteration runs on any result (0x005BF8F4); a failed turn still erases, notifies, reports and advances.</summary>
    // reach: unit (supplied: the target set; the rig's turn override returns false so TurnTowardsPoseAction ends with a failure).
    [Fact]
    public void B10_AFailedTurnStillAdvances()
    {
        using var signals = SignalTestContext.Install();
        using var rig = FaceRig((7, new Vec3(400, 150, 250), null), (9, new Vec3(400, -50, 250), null));
        rig.Frame();
        rig.Vision.TurnOverride = (_, _, _) => Task.FromResult(false);
        var ctx = Ctx(rig);
        var b = new AcknowledgeFaceBehavior(rig.Vision);
        var rec = new Recorder(); b.AddListener(rec);
        var objectives = Objectives(rig);
        b.SetTargets(new[] { 7, 9 });
        Drive(rig, b, ctx);
        Assert.Contains(b.Trace, l => l.StartsWith("TurnTowardsFace(9) -> ") && !l.EndsWith("-> Success"));
        Assert.Equal(new[] { 9, 7 }, rec.Finished);
        Assert.Equal(2, objectives.Count);
        Assert.Empty(b.Targets);
    }

    /// <summary>
    /// B4: greet = (now - [mgr+0x54] &lt;= 60.0f) &amp;&amp; [mgr+0x54] &gt;= 0 &amp;&amp; !HasTurnedTowardsFace &amp;&amp; !+0x12D. The manager's +0x54 is -1.0f from its ctor
    /// (0x005A08E4..0x005A08F0) and has no known writer (B5), so the default never greets; the say-name trigger is AcknowledgeFaceNamed (B7) and the
    /// no-name trigger AcknowledgeFaceUnnamed (B8).
    /// </summary>
    // reach: unit (the target set supplied through SetTargets; the manager is constructed with its ctor value, nothing supplied for +0x54).
    [Fact]
    public void B4_B5_TheDefaultManagerTimeNeverGreets()
    {
        using var signals = SignalTestContext.Install();
        using var rig = FaceRig((7, new Vec3(350, 150, 250), "Jim"));
        rig.Frame();
        var ctx = Ctx(rig);
        using var manager = new BehaviorManager(ctx);
        Assert.Equal(0xBF800000u, BitConverter.SingleToUInt32Bits(manager.GreetingTimeField54));
        var b = new AcknowledgeFaceBehavior(rig.Vision) { Manager = manager };
        b.SetTargets(new[] { 7 });
        Drive(rig, b, ctx);
        Assert.Contains(b.Trace, l => l.Contains("shouldPlayGreeting:0"));
        Assert.False(b.GreetingPlayed);
        Assert.Equal(AnimationTrigger.AcknowledgeFaceNamed, b.LastTurn!.SayNameTrigger);
        Assert.Equal(AnimationTrigger.AcknowledgeFaceUnnamed, b.LastTurn.NoNameTrigger);
        Assert.DoesNotContain(b.Trace, l => l.Contains("NamedFaceInitialGreeting"));
        Assert.Equal(1, (int)AnimationTrigger.AcknowledgeFaceNamed);
        Assert.Equal(2, (int)AnimationTrigger.AcknowledgeFaceUnnamed);
    }

    /// <summary>
    /// B4/B6/S1: with +0x54 inside the window (now - t = 60.0f exactly, the compare is &lt;=) the greeting callback is installed instead of the say-name
    /// trigger; the lambda 0x00602D8C..0x00602DDE sets +0x12D, fires GreetingSayName and returns 301 NamedFaceInitialGreeting. +0x12D is never reset,
    /// so a later face is not greeted (once only).
    /// </summary>
    // reach: unit (supplied: BehaviorManager+0x54, whose writer is UNKNOWN (B5); the target set through SetTargets).
    [Fact]
    public void B4_B6_TheGreetingPlaysOnceInsideTheWindow()
    {
        using var signals = SignalTestContext.Install();
        using var rig = FaceRig((7, new Vec3(350, 150, 250), "Jim"), (9, new Vec3(350, -150, 250), "Ann"));
        rig.Frame();
        var ctx = Ctx(rig, nowSec: 100.0);
        using var manager = new BehaviorManager(ctx) { GreetingTimeField54 = 40f };   // supplied: now - t = 60.0f
        var b = new AcknowledgeFaceBehavior(rig.Vision) { Manager = manager };
        b.SetTargets(new[] { 7 });
        Drive(rig, b, ctx);
        Assert.Contains(b.Trace, l => l.Contains("alreadyTurned:0, shouldPlayGreeting:1"));
        Assert.True(b.GreetingPlayed);
        Assert.Null(b.LastTurn!.SayNameTrigger);                                         // B6: SetSayNameAnimationTrigger is not called
        Assert.Equal(AnimationTrigger.AcknowledgeFaceUnnamed, b.LastTurn.NoNameTrigger);  // B8: both branches
        Assert.Contains(b.Trace, l => l.Contains("emotion event GreetingSayName"));
        Assert.Contains(b.Trace, l => l.Contains("NamedFaceInitialGreeting"));
        Assert.Equal(301, (int)AnimationTrigger.NamedFaceInitialGreeting);

        // S1: +0x12D kept across Stop and the next run: face 9 (never turned to) is not greeted.
        b.SetTargets(new[] { 9 });
        Drive(rig, b, ctx);
        Assert.Contains(b.Trace, l => l.Contains("alreadyTurned:0, shouldPlayGreeting:0"));
        Assert.Equal(AnimationTrigger.AcknowledgeFaceNamed, b.LastTurn!.SayNameTrigger);
    }

    /// <summary>B4: outside the window, a negative time, or a face already turned to: no greeting.</summary>
    // reach: unit (supplied: BehaviorManager+0x54 (B5 UNKNOWN writer), HasTurnedTowardsFace through FaceWorld.SetTurnedTowardsFace, the target set).
    [Theory]
    [InlineData(39.99f, false)]   // now - t = 60.01 > 60
    [InlineData(-0.5f, false)]    // t < 0
    [InlineData(40f, true)]       // in the window, but the face has been turned to
    public void B4_NoGreetingOutsideTheGate(float t, bool turned)
    {
        using var signals = SignalTestContext.Install();
        using var rig = FaceRig((7, new Vec3(350, 150, 250), "Jim"));
        rig.Frame();
        if (turned) rig.Vision.Faces.SetTurnedTowardsFace(7, true);
        var ctx = Ctx(rig, nowSec: 100.0);
        using var manager = new BehaviorManager(ctx) { GreetingTimeField54 = t };
        var b = new AcknowledgeFaceBehavior(rig.Vision) { Manager = manager };
        b.SetTargets(new[] { 7 });
        Drive(rig, b, ctx);
        Assert.Contains(b.Trace, l => l.Contains($"alreadyTurned:{(turned ? 1 : 0)}, shouldPlayGreeting:0"));
        Assert.False(b.GreetingPlayed);
    }

    /// <summary>B3: TurnTowardsFaceAction(robot, id, Radians(0x40490FDB), sayName 1) always; B9: +0x188 = 3, the ctor's 10 otherwise (0x0054B798).</summary>
    // reach: unit (the target set supplied through SetTargets).
    [Fact]
    public void B3_B9_SayNameIsAlwaysSetAndThreeImagesAreWaited()
    {
        using var signals = SignalTestContext.Install();
        using var rig = FaceRig((7, new Vec3(350, 150, 250), null));
        rig.Frame();
        var ctx = Ctx(rig);
        var b = new AcknowledgeFaceBehavior(rig.Vision);
        b.SetTargets(new[] { 7 });
        Drive(rig, b, ctx);
        Assert.True(b.LastTurn!.SayName);
        Assert.Equal(3, b.LastTurn.ImagesToWait);
        Assert.Equal(0x40490FDBu, BitConverter.SingleToUInt32Bits((float)b.LastTurn.MaxTurnAngleRad));
        using var other = new TurnTowardsFaceAction(rig.Vision, 7);
        Assert.Equal(10, other.ImagesToWait);
    }

    /// <summary>
    /// R1: the base's vtable+0x20/+0x24 slots return 0 (0x005BF04C, 0x0059EC12) and the class's +0x28 slot returns 0 (0x00602D20): not runnable
    /// while carrying (or off treads, or on the charger platform). R2 (0x006028BC..0x006028C6): runnable iff the target set is non-empty.
    /// I1 (0x006028C8..0x006028D0): Init starts no action.
    /// </summary>
    // reach: unit (supplied: the IsRunnableBase robot-byte inputs robot+0x355, robot+0x34A, carrying, +0x74 state and +0x70 unlock).
    [Fact]
    public void R1_R2_NotRunnableWhileCarryingOrWithAnEmptySet()
    {
        using var rig = FaceRig((7, new Vec3(350, 150, 250), null));
        rig.Frame();
        var ctx = Ctx(rig);
        bool carrying = false, offTreads = false, onPlatform = false;
        var b = new AcknowledgeFaceBehavior(rig.Vision)
        {
            RobotComponent284 = () => carrying, RobotState355 = () => offTreads, RobotState34a = () => onPlatform,
            RobotStateAllowsRun = () => true, UnlockAllowsRun = () => true,
        };
        Assert.False(RunnableInternal(b, ctx));                    // R2: empty set
        b.SetTargets(new[] { 7 });
        Assert.True(RunnableInternal(b, ctx));
        Assert.True(b.IsRunnableBase(ctx));
        carrying = true; Assert.False(b.IsRunnableBase(ctx));     // +0x28 override false
        carrying = false; offTreads = true; Assert.False(b.IsRunnableBase(ctx));
        offTreads = false; onPlatform = true; Assert.False(b.IsRunnableBase(ctx));

        // I1: Init only sets the begin-pending flag; no action starts until the first Update.
        using var signals = SignalTestContext.Install();
        b.ActionTaskRunner = SignalTestContext.Schedule;
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.DoesNotContain(b.Trace, l => l.StartsWith("start TurnTowardsFace("));
        Assert.Null(b.TargetFaceId);
        b.Stop(BehaviorStopReason.Interrupted);
    }

    /// <summary>
    /// T1/T2/T3/T4/S1 through the manager: the shipped map binds FacePositionUpdated to AcknowledgeFace; a named face seen for the first time fills
    /// desiredFaces (0x0060CB84..0x0060CE20); CheckReactions runs STBI (0x0060CAB0..0x0060CB5E), which copies the set into AcknowledgeFace+0x120; Stop
    /// (StopInternal 0x006028D2..0x00602926) calls the strategy's ClearDesiredTargets (slot 1, 0x0060D0CA) and clears the set.
    /// </summary>
    // reach: production up to the manager's CheckReactions (shipped map and strategy, real frame); the Stop is called directly (unit).
    [Fact]
    public void T4_S1_TheManagerFiresAcknowledgeFaceAndStopClearsTheStrategysDesiredFaces()
    {
        var obb = ObbRoot();
        if (obb is null || MarkerLibrary.EmbeddedOrNull is null) return;
        using var signals = SignalTestContext.Install();
        using var rig = FaceRig((7, new Vec3(350, 150, 250), "Jim"));
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = Ctx(rig);
        using var manager = new BehaviorManager(ctx);
        var reg = ShippedBehaviors.Reactions(rig.Robot, clockSec: () => 100.0, vision: rig.Vision, obbRoot: obb)
                                  .Single(r => r.Strategy.Trigger == ReactionTrigger.FacePositionUpdated);
        manager.AddReaction(reg.Strategy, reg.Behavior);
        var strategy = Assert.IsType<FacePositionUpdatedStrategy>(reg.Strategy);
        var b = Assert.IsType<AcknowledgeFaceBehavior>(reg.Behavior);
        b.ActionTaskRunner = SignalTestContext.Schedule;
        rig.Frame();
        Assert.Equal(new[] { 7 }, strategy.DesiredFaces);
        var fired = manager.CheckReactions(100.0);
        Assert.NotNull(fired);
        Assert.Equal("AcknowledgeFace", fired!.Behavior);
        Assert.Equal(new[] { 7 }, b.Targets);
        b.Stop(BehaviorStopReason.Interrupted);
        Assert.Empty(strategy.DesiredFaces);
        Assert.Empty(b.Targets);
    }
}
