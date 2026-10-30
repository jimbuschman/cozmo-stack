using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using System.Reflection;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class CheckForStackAtIntervalNativeTests
{
    private sealed class SuppliedChild : ActionRunner
    {
        internal SuppliedChild() : base(0x2C, 0) { }
        public override int Init() => 0;
        public override uint CheckIfDone() => EngineActionResult.Running;
        public override bool CanInterrupt() => false;
    }
    private static void LoadLibrary(Fixture f)
    {
        f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        Assert.NotNull(f.Rig.Robot.Animations.Library);
    }
    private static Fixture ReadyFixture()
    {
        var f = new Fixture();
        try { LoadLibrary(f); return f; }
        catch { f.Dispose(); throw; }
    }
    private static void Terminal(Fixture f, uint result)
    {
        Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction()).State = result;
        f.Robot.ActionList.Update();
    }
    private static NativeCheckForStackAtIntervalBehavior SelectScan(Fixture f)
    {
        var manager = f.Robot.BehaviorManagerOwner!;
        manager.SparkNameInput = _ => "KnockOverThreeCubeStack";
        manager.SetNativeRequestedSpark(5, 1);
        f.Runtime.AdmitActivity("SparksKnockOverCubes");
        manager.SwitchToNativeRequestedSpark();
        var activity = NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a => a.Id == "SparksKnockOverCubes");
        var owner = activity.SparkedOwner!;
        // Supplied state 2 bypasses the separately checked Spark get-in animation.
        typeof(NativeSparkedActivity).GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, 2);
        manager.Update(0, f.Rig.Engine.Timer.SecondsF);
        return Assert.IsType<NativeCheckForStackAtIntervalBehavior>(manager.Current);
    }
    // reach: unit, supplied f32 values.
    [Fact]
    public void StrictFloatComparisonRejectsEqualityAndUnordered()
    {
        Assert.False(NativeCheckForStackAtIntervalBehavior.IsDue(0f, 0f));
        Assert.True(NativeCheckForStackAtIntervalBehavior.IsDue(MathF.BitIncrement(0f), 0f));
        Assert.False(NativeCheckForStackAtIntervalBehavior.IsDue(float.NaN, 0f));
        Assert.False(NativeCheckForStackAtIntervalBehavior.IsDue(1f, float.NaN));
    }

    // reach: configured instance, direct readiness with supplied located objects.
    [Fact]
    public void ConfiguredOwnerAppendsCanonicalWorldOrderAndRetainsDuplicatesUntilStop()
    {
        using var f = ReadyFixture();
        var scan = Assert.IsType<NativeCheckForStackAtIntervalBehavior>(f.Runtime.Bound["SparksCheckForStackAtInterval"]);
        f.Add(20, 350); f.Add(3, 100);
        var filter = new BlockWorldFilter();
        filter.AllowedFamilies.Add(ObjectFamily.Block); filter.AllowedFamilies.Add(ObjectFamily.LightCube);
        var expected = f.M.World.FindLocatedMatchingObjects(filter).Select(o => o.ObjectId).ToArray();
        Assert.Equal(new uint[] { 3, 20 }, expected);
        Assert.True(scan.IsRunnable(f.Runtime.Context));
        Assert.Equal(expected, scan.Targets);
        Assert.True(scan.IsRunnable(f.Runtime.Context));
        Assert.Equal(expected.Concat(expected), scan.Targets);
        scan.Stop(BehaviorStopReason.Interrupted);
        Assert.Empty(scan.Targets);
        Assert.Equal(-1, scan.Index);
    }

    // reach: configured instance, supplied missing host library and located object.
    [Fact]
    public void MissingHostLibraryMarksReadinessUnavailableWithoutAppendingTargets()
    {
        using var f = new Fixture(); f.Add();
        var scan = Assert.IsType<NativeCheckForStackAtIntervalBehavior>(f.Runtime.Bound["SparksCheckForStackAtInterval"]);
        Assert.Null(f.Rig.Robot.Animations.Library);
        var readiness = scan.EvaluateHostReadiness(f.Runtime.Context);
        Assert.False(readiness.Available); Assert.False(readiness.Runnable);
        Assert.Empty(scan.Targets);
        LoadLibrary(f);
        Assert.True(scan.IsRunnable(f.Runtime.Context));
        Assert.Equal(new uint[] { 7 }, scan.Targets);
    }

    // reach: manual navigation catalog and actual configured factory, no activity admission.
    [Fact]
    public void LegacyNavigationCatalogDoesNotRegisterTheConfiguredScanId()
    {
        using var f = new Fixture();
        Assert.DoesNotContain(ShippedBehaviors.Navigation(f.M), behavior => behavior.Id == "SparksCheckForStackAtInterval");
        Assert.IsType<NativeCheckForStackAtIntervalBehavior>(f.Runtime.Bound["SparksCheckForStackAtInterval"]);
    }

    // reach: configured instance, direct Init with an empty cached target vector.
    [Fact]
    public void EmptyDirectInitConsumesSharedIdOnceAndRetainsItAcrossStop()
    {
        using var f = new Fixture();
        var scan = Assert.IsType<NativeCheckForStackAtIntervalBehavior>(f.Runtime.Bound["SparksCheckForStackAtInterval"]);
        uint before = ObjectIdSpace.Set();
        using var scope = new BehaviorScope();
        scan.StartAsync(f.Runtime.Context, scope, default).GetAwaiter().GetResult();
        Assert.True(scan.InitFailed);
        Assert.Equal(unchecked(before + 1), scan.GhostId);
        scan.Stop(BehaviorStopReason.Interrupted);
        scan.StartAsync(f.Runtime.Context, scope, default).GetAwaiter().GetResult();
        Assert.Equal(unchecked(before + 1), scan.GhostId);
        Assert.Equal(unchecked(before + 2), ObjectIdSpace.Set());
    }

    // reach: configured Spark chooser with supplied state 2 admission and supplied terminal motor results; no supplied ghost child.
    [Fact]
    public void ConfiguredChooserSkipsEachMissingGhostAndWaitsFromRealReturnCompletion()
    {
        using var f = ReadyFixture();
        f.Add(20, 350); f.Add(3, 100);
        var missing = new List<string>(); SteppedBehavior.MissingReported += missing.Add;
        try
        {
            var scan = SelectScan(f);
            var cached = scan.Targets.ToArray();
            Assert.True(cached.Length >= 2);
            Assert.Equal(new uint[] { 3, 20 }, cached.Take(2));
            for (int i = 0; i < cached.Length; i++)
            {
                var face = Assert.IsType<NativeTurnTowardsObjectRunner>(f.Robot.ActionList.GetCurrentAction());
                uint id = (uint)typeof(NativeTurnTowardsObjectRunner).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(face)!;
                Assert.Equal(cached[i], id);
                Assert.Equal(0f, scan.Deadline);
                Terminal(f, 0x03000000);
                Assert.Equal(i + 1, missing.Count(line => line.Contains("custom ghost marker input")));
            }
            Assert.IsType<NativePanTiltRunner>(f.Robot.ActionList.GetCurrentAction());
            Assert.Equal(0f, scan.Deadline);
            f.Rig.Engine.Timer.UpdateTime(100_000_000_000L);
            Terminal(f, 0x03000000);
            Assert.Equal(115f, scan.Deadline);
            var manager = f.Robot.BehaviorManagerOwner!;
            manager.Stop(BehaviorStopReason.Interrupted, 100);
            var activity = NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a => a.Id == "SparksKnockOverCubes");
            var chooser = Assert.IsType<StrictPriorityChooser>(activity.Chooser);
            f.Rig.Engine.Timer.UpdateTime(114_000_000_000L);
            Assert.NotSame(scan, chooser.GetDesiredActiveBehavior(null, 0, f.Runtime.Context, 114).Behavior);
            f.Rig.Engine.Timer.UpdateTime(115_000_000_000L);
            Assert.NotSame(scan, chooser.GetDesiredActiveBehavior(null, 0, f.Runtime.Context, 115).Behavior);
            Assert.False(scan.IsRunnable(f.Runtime.Context));
            f.Rig.Engine.Timer.UpdateTime(115_000_010_000L); // next f32 after 115
            Assert.True(scan.IsRunnable(f.Runtime.Context));
        }
        finally { SteppedBehavior.MissingReported -= missing.Add; f.Robot.BehaviorManagerOwner!.Stop(BehaviorStopReason.Interrupted, 0); f.Robot.ActionList.Clear(); }
    }

    // reach: configured instance, direct start and supplied ghost child and terminal results.
    [Theory]
    [InlineData(0u)] [InlineData(0x03000000u)] [InlineData(0x04000001u)]
    public void SuppliedGhostChildResultAdvancesToRealReturnAndReturnResultStampsDeadline(uint result)
    {
        // Supplied child reaches the callback only; it does not establish custom marker geometry or physical head completion.
        using var f = ReadyFixture(); f.Add();
        var scan = Assert.IsType<NativeCheckForStackAtIntervalBehavior>(f.Runtime.Bound["SparksCheckForStackAtInterval"]);
        scan.SuppliedGhostChildForTest = (_, _) => new SuppliedChild();
        Assert.True(scan.IsRunnable(f.Runtime.Context));
        try
        {
            using var scope = new BehaviorScope();
            scan.StartAsync(f.Runtime.Context, scope, default).GetAwaiter().GetResult();
            Terminal(f, 0x03000000);
            Assert.IsType<SuppliedChild>(f.Robot.ActionList.GetCurrentAction());
            // HeadInPos clear keeps the real head child running after its in-position latch.
            f.Rig.State(RobotStatusFlag.IsBodyAccMode | RobotStatusFlag.LiftInPos,
                origin: f.Robot.CurrentOriginId, head: -0.15f);
            float actualHead = f.Rig.Robot.Motion.HeadAngleRad;
            Assert.Equal(-0.15f, actualHead);
            Assert.True(f.Rig.Robot.Motion.HeadMoving);
            Terminal(f, result);
            var returned = Assert.IsType<NativePanTiltRunner>(f.Robot.ActionList.GetCurrentAction());
            Assert.True(returned.Parameters.PanIsAbsolute);
            Assert.False(returned.Parameters.HeadIsAbsolute);
            Assert.Equal(0d, returned.Parameters.HeadAngleRad);
            f.Robot.ActionList.Update(); // actual return Init constructs a relative-zero head child.
            Assert.True(returned.Initialized);
            var compound = (CompoundActionParallel)typeof(NativePanTiltRunner).GetField("Compound", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(returned)!;
            var head = Assert.Single(compound.Children.Where(child => child.Type == 0x12));
            float target = (float)head.GetType().GetField("Target", BindingFlags.Instance | BindingFlags.Public)!.GetValue(head)!;
            Assert.Equal(actualHead, target);
            float before = f.Rig.Engine.Timer.SecondsF;
            Terminal(f, result);
            Assert.Equal(before + 15f, scan.Deadline);
        }
        finally { scan.Stop(BehaviorStopReason.Interrupted); f.Robot.ActionList.Clear(); }
    }

    // reach: configured Spark chooser with supplied state 2 and diagnostic reentry.
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MissingDiagnosticReentryCannotAdvanceReplacementRun(bool deferred)
    {
        using var f = ReadyFixture(); f.Add();
        var scan = SelectScan(f);
        var manager = f.Robot.BehaviorManagerOwner!;
        bool replaced = false;
        void Replace()
        {
            manager.Stop(BehaviorStopReason.Interrupted, 0);
            Assert.True(scan.IsRunnable(f.Runtime.Context));
            Assert.True(manager.StartAsync(scan.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            replaced = true;
        }
        void Diagnostic(string line)
        {
            if (!line.Contains("custom ghost marker input") || replaced) return;
            if (deferred) f.Robot.ActionList.DeferHostCleanup(Replace);
            else Replace();
        }
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += Diagnostic;
        try
        {
            var old = Assert.IsType<NativeTurnTowardsObjectRunner>(f.Robot.ActionList.GetCurrentAction());
            Terminal(f, 0x03000000);
            Assert.True(replaced);
            Assert.Same(scan, manager.Current);
            Assert.True(scan.EngineRunning);
            Assert.NotSame(old, f.Robot.ActionList.GetCurrentAction());
            Assert.IsType<NativeTurnTowardsObjectRunner>(f.Robot.ActionList.GetCurrentAction());
            Assert.Equal(0f, scan.Deadline);
        }
        finally { SteppedBehavior.MissingReported -= Diagnostic; manager.Stop(BehaviorStopReason.Interrupted, 0); f.Robot.ActionList.Clear(); }
    }

    // reach: configured instance, direct Init after supplied target deletion.
    [Fact]
    public void LostCachedTargetTakesSourceNullBranchWithoutActionOrDeadline()
    {
        using var f = ReadyFixture(); f.Add();
        var scan = Assert.IsType<NativeCheckForStackAtIntervalBehavior>(f.Runtime.Bound["SparksCheckForStackAtInterval"]);
        Assert.True(scan.IsRunnable(f.Runtime.Context));
        var filter = new BlockWorldFilter(); filter.AllowedIds.Add(7);
        f.M.World.DeleteLocatedObjects(filter);
        using var scope = new BehaviorScope();
        try
        {
            scan.StartAsync(f.Runtime.Context, scope, default).GetAwaiter().GetResult();
            Assert.Null(f.Robot.ActionList.GetCurrentAction());
            Assert.Equal(0f, scan.Deadline);
            Assert.Equal(0, scan.Index);
        }
        finally { scan.Stop(BehaviorStopReason.Interrupted); f.Robot.ActionList.Clear(); }
    }
}
