using System.Text.Json;
using System.Reflection;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public class DriveInDesperationNativeTests
{
    private static string Config()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var path = Path.Combine(d.FullName, "re-analysis", "obb", "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "behaviors", "freeplay", "needs", "needs_SevereLowEnergyState.json");
            if (File.Exists(path)) return File.ReadAllText(path);
            d = d.Parent;
        }
        throw new FileNotFoundException("shipped Energy config");
    }
    private static string Resources()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var path = Path.Combine(d.FullName, "re-analysis", "obb", "assets", "cozmo_resources");
            if (File.Exists(Path.Combine(path, "config", "engine", "behaviorSystem", "activities_config.json"))) return path;
        }
        throw new FileNotFoundException("shipped behavior resources");
    }
    // reach: production binding; no parent picker or activity admission substituted.
    // Shipped activity and factory case 0x59ca9a bind Energy to this owner.
    [Fact]
    public void ShippedRuntimeBindsEnergyAndKeepsStrictPriorityOrder()
    {
        using var rig = new M4ControlTests.Rig(Resources()); rig.ToSynced();
        var runtime = rig.Engine.Robot!.NativeBehaviorRuntime!;
        Assert.IsType<NativeDriveInDesperationBehavior>(runtime.Bound["Needs_SevereLowEnergyState"]);
        var energy = Assert.Single(NativeBehaviorRuntime.AllActivities(runtime.Tree).Where(a => a.Id == "NeedsSevereLowEnergy"));
        Assert.Equal(new[] { "DriveOffCharger", "PutDownBlock", "Needs_SevereLowEnergyGetIn", "Needs_SevereLowEnergyState", "Needs_Wait" }, energy.Chooser!.BehaviorIds);
    }
    // reach: actual admission entry with supplied activity ID; parent NeedsExpression is not built (M8-013).
    [Fact]
    public void EnergyActivityAdmissionExposesMissingParentAndKeepsPriorActivity()
    {
        using var f = new Fixture();
        var messages = new List<string>(); void Observe(string message) => messages.Add(message);
        SteppedBehavior.MissingReported += Observe;
        try
        {
            var prior = f.Robot.BehaviorManagerOwner!.Activity;
            f.Runtime.AdmitActivity("NeedsSevereLowEnergy");
            Assert.Same(prior, f.Robot.BehaviorManagerOwner.Activity);
            Assert.Contains(messages, m => m.Contains("M8-013") && m.Contains("NeedsExpression"));
        }
        finally { SteppedBehavior.MissingReported -= Observe; }
    }
    // reach: offline CLI --simulate with shipped assets; verifies the actual printed boundary, no robot or hardware run.
    [Fact]
    public async Task StandaloneSimulationPrintsLegacyEnergyBoundary()
    {
        var obb = Directory.GetParent(Directory.GetParent(Resources())!.FullName)!.FullName;
        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            Assert.Equal(0, await FreeplayTool.Run(new[] { "freeplay", "--simulate", "--obb", obb, "--ticks", "0" }));
        }
        finally { Console.SetOut(previous); }
        Assert.Contains("MISSING: Needs_SevereLowEnergyState uses the legacy standalone custom driver", output.ToString());
        Assert.Contains("Native NeedsExpression admission remains unbuilt (M8-013)", output.ToString());
    }
    // reach: unit; supplied factory inputs and canonical carrying/platform state. Native vtable cells 0x10288f4/f8/fc and runnable 0x5d90ac.
    [Fact]
    public void EnergyFactoryUsesOwnGatesAndRejectsRepair()
    {
        using var rig = new Rig();
        using var doc = JsonDocument.Parse(Config());
        var ctx = new BehaviorFactoryContext { Manipulation = rig.M, NativeRobot = rig.Robot.Engine.Robot };
        var factory = new BehaviorFactory();
        var container = new BehaviorContainer(factory, ctx, Array.Empty<KeyValuePair<BehaviorID, JsonElement>>());
        var b = Assert.IsType<NativeDriveInDesperationBehavior>(factory.CreateBehavior(container, BehaviorClass.DriveInDesperation, ctx, doc.RootElement, out var status));
        Assert.Equal(BehaviorCreateStatus.Created, status);
        var inputs = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        b.RobotState355 = () => true;
        b.RobotStateAllowsRun = () => true; b.UnlockAllowsRun = () => true;
        Assert.True(b.IsRunnableBase(inputs));
        rig.Robot.Engine.Robot!.CarryingOwner!.SetCarrying(7); Assert.False(b.IsRunnableBase(inputs));
        rig.Robot.Engine.Robot.CarryingOwner.ClearNativeCarryingFields(false);
        rig.Robot.Sensors.SetOnChargerPlatform(true); Assert.False(b.IsRunnableBase(inputs));
        rig.Robot.Sensors.SetOnChargerPlatform(false);
        using var repair = JsonDocument.Parse(Config().Replace("Needs_SevereLowEnergyState", "Needs_SevereLowRepairState"));
        Assert.Null(factory.CreateBehavior(container, BehaviorClass.DriveInDesperation, ctx, repair.RootElement, out status));
        Assert.Equal(BehaviorCreateStatus.NotImplemented, status);
    }
    // reach: unit; direct actual path owner. Setter 0x64ab90..a0 copies then enables; clear 0x64ac4a only clears flag.
    [Fact]
    public void CustomProfileClearKeepsStoredProfile()
    {
        using var rig = new Rig(); var path = rig.Robot.Motion.Path;
        var profile = new PathMotionProfile { SpeedMmps = 40, IsCustom = true };
        path.SetNativeCustomMotionProfile(profile);
        Assert.True(path.Field48); Assert.Equal(40f, path.NativeMotionProfile.SpeedMmps);
        path.ClearNativeCustomMotionProfile();
        Assert.False(path.Field48); Assert.Equal(40f, path.NativeMotionProfile.SpeedMmps);
    }
    // reach: unit; actual native wait and supplied engine clock. 0x54ca1c..60 uses f32 now+duration and strict greater-than.
    [Fact]
    public void WaitAtDeadlineRemainsRunning()
    {
        using var rig = new Rig();
        rig.Robot.Engine.Timer.UpdateTime(0);
        var wait = new NativeTimedWaitRunner(rig.M, 1.5f);
        Assert.Equal(0, wait.Init());
        rig.Robot.Engine.Timer.UpdateTime(1_500_000_000);
        Assert.Equal(EngineActionResult.Running, wait.CheckIfDone());
        rig.Robot.Engine.Timer.UpdateTime(1_600_000_000);
        Assert.Equal(0u, wait.CheckIfDone());
    }
    // reach: supplied native manager activation/world and wait terminal; real ActionList dispatch.
    // 0x5d9dc2..9e3a retains the farthest valid distance, with the first equal-distance object winning.
    [Theory]
    [InlineData(100f, 300f, 8u)]
    [InlineData(300f, 300f, 7u)]
    public void IdleCompletionSelectsFarthestLocatedCube(float firstX, float secondX, uint expected)
    {
        using var f = new Fixture();
        f.Add(7, firstX); f.Add(8, secondX);
        var behavior = Assert.IsType<NativeDriveInDesperationBehavior>(f.Runtime.Bound["Needs_SevereLowEnergyState"]);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var wait = Assert.IsType<NativeTimedWaitRunner>(f.Robot.ActionList.GetCurrentAction());
        wait.State = 0; f.Robot.ActionList.Update();
        var target = (uint)typeof(NativeDriveInDesperationBehavior).GetField("_target", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(behavior)!;
        Assert.Equal(expected, target);
        f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
    }
    // reach: supplied manager activation, located cube/height-overlapping charger and terminal child results;
    // actual ActionList D6 broadcast, PAP input and owned turns. 005D995C/9A54, 005D9C22, 005DA51E and 005DA354..A3CE.
    [Theory]
    [InlineData(0u, true)]
    [InlineData(0x03000000u, false)]
    public void ChargerObstacleSkipsUnavailablePapAndPreservesCompletionDelivery(uint lookResult, bool requests)
    {
        using var f = new Fixture();
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        f.Robot.HasExternalInterface = true;
        f.Add(7, 250);
        var charger = new ObservableObject(9, ObjectType.Charger_Basic, ChargerGeometry.Markers)
        { Pose = new Pose3d(Rotation3d.Identity, new Vec3(100, 0, 22), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)), OriginId = f.Robot.CurrentOriginId, PoseState = PoseState.Known };
        f.M.World.AddLocatedObject(charger);
        var messages = new List<string>(); void Observe(string message) => messages.Add(message);
        SteppedBehavior.MissingReported += Observe;
        try
        {
            var behavior = Assert.IsType<NativeDriveInDesperationBehavior>(f.Runtime.Bound["Needs_SevereLowEnergyState"]);
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            var wait = Assert.IsType<NativeTimedWaitRunner>(f.Robot.ActionList.GetCurrentAction());
            uint waitTag = wait.Tag;
            var queue = f.Robot.ActionList.QueueAt(0)!;
            var completions = new List<RobotCompletedAction>();
            using var later = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x5A, true, p => { if (p is RobotCompletedAction r) completions.Add(r); });
            wait.State = 0; f.Robot.ActionList.Update();
            Assert.Contains(completions, r => r.Tag == waitTag);
            var deleting = (HashSet<uint>)typeof(ActionQueue).GetField("_deletionTags", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(queue)!;
            Assert.DoesNotContain(waitTag, deleting);
            Assert.Equal(7u, f.M.World.SelectedObjectId);
            Assert.Contains(messages, m => m.Contains("M12-036") && m.Contains("0x005D995C") && m.Contains("footprint") && m.Contains("0x005D9A54"));
            var look = Assert.IsType<NativeTurnTowardsObjectRunner>(f.Robot.ActionList.GetCurrentAction());
            Assert.Equal(7u, (uint)typeof(NativeTurnTowardsObjectRunner).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(look)!);
            Assert.True((bool)typeof(NativeTurnTowardsObjectRunner).GetField("_verify", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(look)!);
            Assert.False((bool)typeof(NativeTurnTowardsObjectRunner).GetField("_track", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(look)!);
            look.State = lookResult; f.Robot.ActionList.Update();
            if (!requests) Assert.Null(f.Robot.ActionList.GetCurrentAction());
            else
            {
                var request = Assert.IsType<NativeTurnTowardsFaceWrapperRunner>(f.Robot.ActionList.GetCurrentAction());
                request.State = 0; f.Robot.ActionList.Update();
                var final = Assert.IsType<NativeTurnTowardsObjectRunner>(f.Robot.ActionList.GetCurrentAction());
                Assert.Equal(7u, (uint)typeof(NativeTurnTowardsObjectRunner).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(final)!);
                Assert.False((bool)typeof(NativeTurnTowardsObjectRunner).GetField("_verify", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(final)!);
                Assert.Equal(uint.MaxValue, (uint)typeof(NativeDriveInDesperationBehavior).GetField("_target", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(behavior)!);
            }
            f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
        }
        finally { SteppedBehavior.MissingReported -= Observe; }
    }
    // reach: supplied initial round count, two manager activations and terminal Wait/Drive results, through actual ActionList callbacks.
    // 005D970E..9744 has no failure callback; the unavailable sentinel helper and its motions are not fabricated.
    [Fact]
    public void EachSentinelSearchOccurrenceReportsSkippedMotions()
    {
        using var f = new Fixture();
        var messages = new List<string>(); void Observe(string message) => messages.Add(message);
        SteppedBehavior.MissingReported += Observe;
        try
        {
            var behavior = Assert.IsType<NativeDriveInDesperationBehavior>(f.Runtime.Bound["Needs_SevereLowEnergyState"]);
            typeof(NativeDriveInDesperationBehavior).GetField("_rounds", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(behavior, 3);
            for (int i = 0; i < 2; i++)
            {
                Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
                Assert.IsType<NativeTimedWaitRunner>(f.Robot.ActionList.GetCurrentAction()).State = 0; f.Robot.ActionList.Update();
                Assert.IsType<DriveToPoseAction>(f.Robot.ActionList.GetCurrentAction()).State = 0; f.Robot.ActionList.Update();
                f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
            }
            Assert.Equal(2, messages.Count(m => m.Contains("M12-010/M8-011") && m.Contains("search motions skipped")));
        }
        finally { SteppedBehavior.MissingReported -= Observe; }
    }
    // reach: supplied manager activation, empty native world, and real Wait/Drive completion.
    // 0x5d9d8a..9db8 stamps cooldown before the empty query; 0x5da2de calls sentinel search only after drive completion.
    [Fact]
    public void EmptyWorldStampsCooldownThenReportsMissingSentinelSearch()
    {
        using var f = new Fixture();
        var missing = new List<string>(); void Observe(string message) => missing.Add(message);
        SteppedBehavior.MissingReported += Observe;
        try
        {
            var behavior = Assert.IsType<NativeDriveInDesperationBehavior>(f.Runtime.Bound["Needs_SevereLowEnergyState"]);
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            float now = f.Rig.Engine.Timer.SecondsF;
            int words = 0; f.Rig.Engine.ContextRandom.WordSourceForTest = () => { words++; return 0; };
            Assert.IsType<NativeTimedWaitRunner>(f.Robot.ActionList.GetCurrentAction()).State = 0; f.Robot.ActionList.Update();
            float cooldown = (float)typeof(NativeDriveInDesperationBehavior).GetField("_cooldown", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(behavior)!;
            Assert.InRange(cooldown, now + 15f, now + 30f);
            var drive = Assert.IsType<DriveToPoseAction>(f.Robot.ActionList.GetCurrentAction());
            Assert.Equal(8, words); // cooldown, angle, sign, distance: each consumes two MT words.
            Assert.InRange((float)drive.Goal!.AngleAroundZ, -0.69814f, -0.69812f);
            Assert.InRange((float)drive.Goal.Translation.X, 38.30f, 38.31f);
            Assert.InRange((float)drive.Goal.Translation.Y, -32.15f, -32.13f);
            drive.State = 0; f.Robot.ActionList.Update();
            Assert.Contains(missing, message => message.Contains("M12-010/M8-011") && message.Contains("sentinel"));
            f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
        }
        finally { f.Rig.Engine.ContextRandom.WordSourceForTest = null; SteppedBehavior.MissingReported -= Observe; }
    }
    // reach: supplied manager activation, non-planar main pose and RNG words; real wait completion and drive action.
    // 0x5d934c..93fe: current full rotation multiplied by Rz(yaw + signed 40 degrees).
    [Fact]
    public void RandomPoseComposesFullRotationAndCurrentYawAgain()
    {
        using var f = new Fixture();
        var main = f.Robot.GetCheckedMainPose();
        var yaw = Rotation3d.FromAngleAxis(1f, new Vec3(0, 0, 1));
        var roll = Rotation3d.FromAngleAxis(.5f, new Vec3(1, 0, 0));
        main.Node!.Transform = new Transform3d(yaw * roll, new Vec3(0, 0, 0));
        var behavior = Assert.IsType<NativeDriveInDesperationBehavior>(f.Runtime.Bound["Needs_SevereLowEnergyState"]);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        f.Rig.Engine.ContextRandom.WordSourceForTest = () => 0;
        try
        {
            Assert.IsType<NativeTimedWaitRunner>(f.Robot.ActionList.GetCurrentAction()).State = 0; f.Robot.ActionList.Update();
            var rotation = Assert.IsType<DriveToPoseAction>(f.Robot.ActionList.GetCurrentAction()).Goal!.Quaternion;
            // Independently evaluated quaternion for Rz(1)*Rx(.5)*Rz(1-40deg), with tolerance for libm.
            Assert.InRange(rotation.W, .77077, .77081); Assert.InRange(rotation.X, .23246, .23251);
            Assert.InRange(rotation.Y, .08459, .08465); Assert.InRange(rotation.Z, .58707, .58712);
        }
        finally { f.Rig.Engine.ContextRandom.WordSourceForTest = null; f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF); }
    }
    // reach: supplied manager activation and classifier state; real action cancellation and re-entry wait.
    // 0x5d9062..90a8 cancels once on the edge and resets ID/latch before starting Idle on return.
    [Fact]
    public void OffTreadsCancelsWaitOnceAndReturnStartsNewWait()
    {
        using var f = new Fixture();
        var behavior = Assert.IsType<NativeDriveInDesperationBehavior>(f.Runtime.Bound["Needs_SevereLowEnergyState"]);
        Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(behavior.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        var first = Assert.IsType<NativeTimedWaitRunner>(f.Robot.ActionList.GetCurrentAction());
        var current = typeof(OffTreadsClassifier).GetProperty("Current")!;
        current.SetValue(f.Rig.Robot.Sensors.OffTreads, OffTreadsState.InAir);
        behavior.Update(f.Runtime.Context, f.Rig.Engine.Timer.SecondsF * 1000.0);
        Assert.Null(f.Robot.ActionList.GetCurrentAction());
        behavior.Update(f.Runtime.Context, f.Rig.Engine.Timer.SecondsF * 1000.0);
        Assert.Null(f.Robot.ActionList.GetCurrentAction());
        current.SetValue(f.Rig.Robot.Sensors.OffTreads, OffTreadsState.OnTreads);
        behavior.Update(f.Runtime.Context, f.Rig.Engine.Timer.SecondsF * 1000.0);
        var second = Assert.IsType<NativeTimedWaitRunner>(f.Robot.ActionList.GetCurrentAction());
        Assert.NotSame(first, second);
        f.Robot.BehaviorManagerOwner.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.SecondsF);
    }
    // reach: unit; supplied activation and RNG words, real configured factory, ActionList and native wait.
    // 0x5d8eca..8fe4: install profile then start f32 wait.
    [Fact]
    public async Task EnergyInitInstallsProfileAndStartsWait()
    {
        using var rig = new Rig();
        using var doc = JsonDocument.Parse(Config());
        var ctx = new BehaviorFactoryContext { Manipulation = rig.M, NativeRobot = rig.Robot.Engine.Robot };
        var factory = new BehaviorFactory();
        var container = new BehaviorContainer(factory, ctx, Array.Empty<KeyValuePair<BehaviorID, JsonElement>>());
        var b = Assert.IsType<NativeDriveInDesperationBehavior>(factory.CreateBehavior(container, BehaviorClass.DriveInDesperation, ctx, doc.RootElement, out _));
        rig.Robot.Engine.ContextRandom.WordSourceForTest = () => 0;
        using var scope = new BehaviorScope();
        await b.StartAsync(new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() }, scope, default);
        Assert.True(rig.Robot.Motion.Path.Field48);
        var profile = rig.Robot.Motion.Path.NativeMotionProfile;
        Assert.Equal(40f, profile.SpeedMmps); Assert.Equal(50f, profile.AccelMmps2); Assert.Equal(500f, profile.DecelMmps2);
        Assert.Equal(1.5f, profile.PointTurnSpeedRadPerSec); Assert.Equal(2f, profile.PointTurnAccelRadPerSec2); Assert.Equal(10f, profile.PointTurnDecelRadPerSec2);
        Assert.Equal(60f, profile.DockSpeedMmps); Assert.Equal(200f, profile.DockAccelMmps2); Assert.Equal(500f, profile.DockDecelMmps2);
        Assert.Equal(40f, profile.ReverseSpeedMmps); Assert.True(profile.IsCustom);
        var wait = Assert.IsType<NativeTimedWaitRunner>(rig.Robot.Engine.Robot!.ActionList.GetCurrentAction());
        Assert.Equal(1.5f, wait.Duration);
        b.Stop(BehaviorStopReason.Interrupted);
        scope.Dispose(); // supplied direct activation has no manager to dispose its scope.
        Assert.False(rig.Robot.Motion.Path.Field48);
    }
}
