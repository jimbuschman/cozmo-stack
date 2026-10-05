using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// B-ACTIONS batch 3a: the compound actions from re-analysis/research/20261004-actionlist-extraction.md, rows
/// P1-P8 (the shared ICompoundAction mechanisms), S1-S9 (CompoundActionSequential) and R1-R6
/// (CompoundActionParallel). Those rows are the evidence for manifest records M13-028 (the Sequential) and
/// M10-008 (the Parallel). Expected values are the rows' full results and the binary's order, never a constant
/// read back from the implementation.
/// </summary>
public class CompoundActionTests
{
    // ------------------------------------------------------------------ test children

    /// <summary>A concrete ActionRunner whose Update returns a scripted full result and records its calls.</summary>
    private sealed class Scripted : ActionRunner
    {
        public readonly string Name;
        public uint Result = EngineActionResult.Success;
        public Func<uint>? ResultFn;
        public int Updates;
        public int Resets;
        public bool ResetArg;
        public bool TrackLocked;
        public bool Retook;
        public readonly List<string> Order = new();

        public Scripted(string name) : base(type: 0x33, requiredTrackMask: 0) => Name = name;

        public override uint Update()
        {
            Updates++;
            uint r = ResultFn?.Invoke() ?? Result;
            State = r;
            return r;
        }

        public override uint CheckIfDone() => State;
        public override bool CanInterrupt() => true;

        public override void Reset(bool unlockTracks)
        {
            Resets++;
            ResetArg = unlockTracks;
            base.Reset(unlockTracks);
        }

        public override uint GetCompletionUnion()
        {
            Order.Add($"{Name}:union");
            return 0x0BADF00Du;
        }

        public override void Prep()
        {
            Order.Add($"{Name}:prep");
            // deliberately not base.Prep: the union read is what P6 orders, not a second one
        }

        protected override bool AreAllTracksLockedBy(uint mask, string owner) => TrackLocked;
        protected override void LockTracks(uint mask, string owner) { TrackLocked = true; Retook = true; }
        protected override void UnlockTracksInternal(uint mask, string owner) => TrackLocked = false;
    }

    /// <summary>Exposes the protected P4/P8 seams and records the parent callback/predicate order for S5/R3.</summary>
    private sealed class ProbeSequential : CompoundActionSequential
    {
        public readonly List<string> Order = new();
        public ProbeSequential(Func<float> clock, params ActionRunner?[] children) : base(clock, children) { }

        public bool ProbeIgnore(ActionRunner child, uint result) => ShouldIgnoreFailure(child, result);
        public void EnableProxy(uint tag) => SetCompletionUnionProxy(tag);
        public void Delay(float seconds) => SetDelay(seconds);

        protected override bool ShouldIgnoreFailure(ActionRunner child, uint result)
        {
            Order.Add("predicate");
            return base.ShouldIgnoreFailure(child, result);
        }

        public override void RunCompletionCallbacks(uint result)
        {
            Order.Add("callback");
            base.RunCompletionCallbacks(result);
        }
    }

    /// <summary>As <see cref="ProbeSequential"/> for the parallel dispatch.</summary>
    private sealed class ProbeParallel : CompoundActionParallel
    {
        public readonly List<string> Order = new();
        public ProbeParallel(Func<float> clock, params ActionRunner?[] children) : base(clock, children) { }

        protected override bool ShouldIgnoreFailure(ActionRunner child, uint result)
        {
            Order.Add("predicate");
            return base.ShouldIgnoreFailure(child, result);
        }

        public override void RunCompletionCallbacks(uint result)
        {
            Order.Add("callback");
            base.RunCompletionCallbacks(result);
        }
    }

    // ------------------------------------------------------------------ P

    /// <summary>P1: the compound's runner type is 0xFFFFFFFE with mask 0; a null child is skipped.</summary>
    [Fact]
    public void P1_CompoundRunnerTypeMaskAndNullChild()
    {
        var a = new Scripted("a");
        var seq = new CompoundActionSequential(() => 0f, a, null, new Scripted("b"));
        Assert.Equal(unchecked((int)0xFFFFFFFE), seq.Type);
        Assert.Equal(0u, seq.RequiredTrackMask);
        Assert.Equal(2, seq.Children.Count);                       // the null child was skipped
    }

    /// <summary>P2/P4: the raw bool overload's first bool true installs an always-true predicate; false leaves it empty.</summary>
    [Fact]
    public void P2_TheBoolOverloadInstallsTheIgnorePredicate()
    {
        var ignored = new Scripted("i");
        var kept = new Scripted("k");
        var seq = new ProbeSequential(() => 0f);
        seq.AddAction(ignored, true, false);
        seq.AddAction(kept, false, false);

        Assert.True(seq.ProbeIgnore(ignored, 0x03000000u));        // always-true predicate
        Assert.False(seq.ProbeIgnore(kept, 0x03000000u));          // empty predicate
    }

    /// <summary>P6: the virtual child union is read BEFORE the child's Prep, and the union/type are cached by tag.</summary>
    [Fact]
    public void P6_UnionIsReadBeforePrepAndCachedByTag()
    {
        float now = 0f;
        var a = new Scripted("a") { Result = EngineActionResult.Success };
        var seq = new ProbeSequential(() => now, a);
        seq.EnableProxy(a.Tag);

        seq.Update();                                              // a completes: store/delete

        Assert.True(a.Order.IndexOf("a:union") < a.Order.IndexOf("a:prep"));
        Assert.Equal(0x0BADF00Du, seq.GetCompletionUnion());       // the proxy falls back to the tag cache
        Assert.Equal(0x33, seq.Type);                              // P6: the proxy match copied the child type to +0x44
    }

    /// <summary>P7: a retained child (DeleteOnCompletion false) re-takes its tracks before destruction.</summary>
    [Fact]
    public void P7_A_RetainedChildReTakesItsTracksOnTeardown()
    {
        float now = 0f;
        var a = new Scripted("a") { Result = EngineActionResult.Success, TrackLocked = true };
        var seq = new CompoundActionSequential(() => now, a);
        seq.SetDeleteOnCompletion(false);

        seq.Update();                                              // a completes and is retained; its tracks unlock
        Assert.Equal(1, a.Updates);
        Assert.False(a.TrackLocked);                               // P6 false: unlocked, still retained
        Assert.Single(seq.Children);

        seq.WatcherEnding();                                       // P7: teardown re-takes the unsigned owner
        Assert.True(a.Retook);
    }

    /// <summary>P8: without the proxy the base union is returned; with it a live child's union is preferred.</summary>
    [Fact]
    public void P8_TheProxyPrefersTheLiveChildUnion()
    {
        float now = 0f;
        var a = new Scripted("a");
        var seq = new ProbeSequential(() => now, a);
        Assert.Equal(0u, seq.GetCompletionUnion());                // no proxy: the base +0x1C cache

        seq.EnableProxy(a.Tag);
        Assert.Equal(0x33, seq.Type);                              // P8 SetProxyTag wrote +0x44 from the live child
        Assert.Equal(0x0BADF00Du, seq.GetCompletionUnion());       // the live child's union
    }

    // ------------------------------------------------------------------ S

    /// <summary>S8/S9: [A,B,C] zero delay; A and B tick on the same outer tick, C on the next.</summary>
    [Fact]
    public void S8_S9_ZeroDelayTicksTwoChildrenOnTheSameTickAndTheThirdNext()
    {
        float now = 0f;
        var a = new Scripted("a");
        var b = new Scripted("b");
        var c = new Scripted("c");
        var seq = new CompoundActionSequential(() => now, a, b, c);

        Assert.Equal(EngineActionResult.Running, seq.Update());    // A success -> B same tick -> C remains
        Assert.Equal(1, a.Updates);
        Assert.Equal(1, b.Updates);
        Assert.Equal(0, c.Updates);

        Assert.Equal(EngineActionResult.Success, seq.Update());    // C completes
        Assert.Equal(1, c.Updates);
    }

    /// <summary>S5: the parent RunCallbacks(full failure) runs BEFORE the ignore predicate; ignored -> move on.</summary>
    [Fact]
    public void S5_CallbacksRunBeforeTheIgnorePredicate()
    {
        float now = 0f;
        var a = new Scripted("a") { Result = 0x03000000u };        // category 3 failure
        var seq = new ProbeSequential(() => now);
        seq.AddAction(a, true, false);                             // install the always-true ignore predicate

        uint result = seq.Update();

        int callback = seq.Order.IndexOf("callback");
        int predicate = seq.Order.IndexOf("predicate");
        Assert.True(callback >= 0 && predicate >= 0 && callback < predicate);
        Assert.Equal(EngineActionResult.Success, result);          // ignored failure -> MoveToNext -> end SUCCESS
    }

    /// <summary>S5: an unignored failure stores/deletes and returns the full failure.</summary>
    [Fact]
    public void S5_AnUnignoredFailureReturnsTheFullFailure()
    {
        float now = 0f;
        var a = new Scripted("a") { Result = 0x03000000u };
        var seq = new ProbeSequential(() => now, a);

        Assert.Equal(0x03000000u, seq.Update());
        Assert.True(seq.Order.IndexOf("predicate") >= 0);          // the predicate is still consulted
    }

    /// <summary>S6: a category-4 retry decrements the parent, resets the children(true) and returns RUNNING.</summary>
    [Fact]
    public void S6_RetryDecrementsResetsChildrenAndReturnsRunning()
    {
        float now = 0f;
        var a = new Scripted("a") { Result = 0x04000006u };
        var seq = new CompoundActionSequential(() => now, a) { RetriesRemain = 1 };
        int resetsAtConstruction = a.Resets;                       // S1: the ctor reset the children(true)

        Assert.Equal(EngineActionResult.Running, seq.Update());
        Assert.Equal(0, seq.RetriesRemain);
        Assert.Equal(resetsAtConstruction + 1, a.Resets);          // S6: one more Reset(true)
        Assert.True(a.ResetArg);
        Assert.Equal(EngineActionResult.Running, seq.State);       // the outer Update stored RUNNING

        a.Result = EngineActionResult.Success;
        Assert.Equal(EngineActionResult.Success, seq.Update());
    }

    /// <summary>S6: with no retry left, the category-4 result goes to the S5 failure path.</summary>
    [Fact]
    public void S6_NoRetryFallsToTheFailurePath()
    {
        float now = 0f;
        var a = new Scripted("a") { Result = 0x04000006u };
        var seq = new CompoundActionSequential(() => now, a) { RetriesRemain = 0 };
        int resetsAtConstruction = a.Resets;

        Assert.Equal(0x04000006u, seq.Update());
        Assert.Equal(resetsAtConstruction, a.Resets);              // no retry reset
    }

    /// <summary>S7: a positive delay stamps now+delay and holds the next child until the deadline passes.</summary>
    [Fact]
    public void S7_MoveToNextDelayHoldsTheNextChildUntilTheDeadline()
    {
        float now = 0f;
        var a = new Scripted("a");
        var b = new Scripted("b");
        var seq = new ProbeSequential(() => now, a, b);
        seq.Delay(2f);

        Assert.Equal(EngineActionResult.Running, seq.Update());    // A success stamps deadline 0+2; B not ticked
        Assert.Equal(1, a.Updates);
        Assert.Equal(0, b.Updates);

        Assert.Equal(EngineActionResult.Running, seq.Update());    // now 0 < deadline 2: B still held
        Assert.Equal(0, b.Updates);

        now = 2f;                                                  // deadline 2 > now 2 is false: B ticks
        Assert.Equal(EngineActionResult.Success, seq.Update());
        Assert.Equal(1, b.Updates);
    }

    /// <summary>S4: a category above 4 is SUCCESS without moving on.</summary>
    [Fact]
    public void S4_CategoryAboveFourIsSuccess()
    {
        float now = 0f;
        var a = new Scripted("a") { Result = 0x05000000u };
        var b = new Scripted("b");
        var seq = new CompoundActionSequential(() => now, a, b);

        Assert.Equal(EngineActionResult.Success, seq.Update());
        Assert.Equal(0, b.Updates);                                // no MoveToNext
    }

    /// <summary>S3: an empty sequence returns SUCCESS without parent RunCallbacks.</summary>
    [Fact]
    public void S3_AnEmptySequenceReturnsSuccessWithoutCallbacks()
    {
        float now = 0f;
        var seq = new ProbeSequential(() => now);
        int callbacks = 0;
        seq.AddCompletionCallback(_ => callbacks++);

        Assert.Equal(EngineActionResult.Success, seq.Update());
        Assert.Equal(0, callbacks);                                // S3: no parent RunCallbacks in this branch
    }

    // ------------------------------------------------------------------ R

    /// <summary>R2: any running child leaves the parallel RUNNING; all success ends it with RunCallbacks(SUCCESS).</summary>
    [Fact]
    public void R2_AnyRunningIsRunningElseSuccess()
    {
        float now = 0f;
        var running = new Scripted("r") { Result = EngineActionResult.Running };
        var done = new Scripted("d");
        var p = new CompoundActionParallel(() => now, running, done);

        Assert.Equal(EngineActionResult.Running, p.Update());
        Assert.Equal(1, running.Updates);
        Assert.Equal(1, done.Updates);                             // a RUNNING child does not stop later children

        running.Result = EngineActionResult.Success;
        Assert.Equal(EngineActionResult.Success, p.Update());
    }

    /// <summary>R2: an empty parallel ends with RunCallbacks(SUCCESS)/SUCCESS, empty included.</summary>
    [Fact]
    public void R2_AnEmptyParallelReturnsSuccess()
    {
        var p = new CompoundActionParallel(() => 0f);
        Assert.Equal(EngineActionResult.Success, p.Update());
    }

    /// <summary>R3: the first failure stops the later children and runs the callback before the predicate.</summary>
    [Fact]
    public void R3_FirstFailureLeavesLaterChildrenUntouchedAndRunsCallbacksFirst()
    {
        float now = 0f;
        var a = new Scripted("a") { Result = 0x03000000u };
        var b = new Scripted("b");
        var p = new ProbeParallel(() => now, a, b);

        Assert.Equal(0x03000000u, p.Update());
        Assert.Equal(1, a.Updates);
        Assert.Equal(0, b.Updates);                                // R3: later child untouched
        Assert.True(p.Order.IndexOf("callback") < p.Order.IndexOf("predicate"));
    }

    /// <summary>R3: an ignored failure stores/deletes the child and ticks the later ones.</summary>
    [Fact]
    public void R3_AnIgnoredFailureContinuesToTheLaterChildren()
    {
        float now = 0f;
        var a = new Scripted("a") { Result = 0x03000000u };
        var b = new Scripted("b");
        var p = new ProbeParallel(() => now);
        p.AddAction(a, true, false);
        p.AddAction(b, false, false);

        Assert.Equal(EngineActionResult.Success, p.Update());      // ignored -> continue -> B success -> SUCCESS
        Assert.Equal(1, b.Updates);
        Assert.True(p.Order.IndexOf("callback") < p.Order.IndexOf("predicate"));
    }

    /// <summary>R4: a retried category-4 child calls Reset(true) on the compound and leaves the rest unticked.</summary>
    [Fact]
    public void R4_RetryResetsTheCompoundAndReturnsRunning()
    {
        float now = 0f;
        var a = new Scripted("a") { Result = 0x04000006u };
        var b = new Scripted("b");
        var p = new CompoundActionParallel(() => now, a, b) { RetriesRemain = 1 };

        Assert.Equal(EngineActionResult.Running, p.Update());
        Assert.Equal(0, p.RetriesRemain);
        Assert.Equal(1, a.Resets);
        Assert.True(a.ResetArg);                                   // R4: virtual Reset(true)
        Assert.Equal(0, b.Updates);                                // no further children this tick

        a.Result = EngineActionResult.Success;
        Assert.Equal(EngineActionResult.Success, p.Update());
    }

    // ------------------------------------------------------------------ the live entry

    /// <summary>
    /// The live entry: a compound queued on the engine robot's ActionList (Robot::Update's 0x005140BC step) is
    /// ticked through CozmoEngine.Update, and its children are ticked in insertion order.
    /// </summary>
    [Fact]
    public void TheCompoundTicksThroughCozmoEngineUpdate()
    {
        using var robot = CozmoRobot.CreateOffline();
        var engineRobot = robot.Engine.Robots.Get(CozmoEngine.RobotId);
        Assert.NotNull(engineRobot);

        var a = new Scripted("a");
        var b = new Scripted("b");
        var compound = new CompoundActionParallel(() => robot.Engine.Timer.SecondsF, a, b);
        engineRobot!.ActionList.QueueAction(QueueActionPosition.Now, compound, 0);

        robot.Engine.Tick();

        Assert.Equal(1, a.Updates);
        Assert.Equal(1, b.Updates);
    }
}