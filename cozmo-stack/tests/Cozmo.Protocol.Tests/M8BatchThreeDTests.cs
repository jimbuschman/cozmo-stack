using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// R-BEH2 batch 3d: M8-011's helper runtime (<c>BehaviorHelperComponent</c>, the <c>IHelper</c> base, <c>SmartDelegateToHelper</c> and the helper stop in
/// <c>StopActing</c>). Every expected value comes from a row of inventory M8-framework Correction A5 (research
/// <c>20261002-R-BEH2-M8-gap1-extraction.md</c> Part 1, the section and the addresses cited per test) and none is what the code returned. The helper bodies are a
/// test <see cref="IHelper"/> subclass: the six concrete helper classes' bodies are unread and not built.
/// </summary>
public class M8BatchThreeDTests
{
    // ------------------------------------------------------------------ fixtures

    private sealed class Probe : SteppedBehavior
    {
        public Probe(string id = "probe") : base(id, "Probe") { }
        protected override bool KeepsRunningWithoutAction => true;
        public Action<Probe>? OnStartHook;
        protected override void OnStart() => OnStartHook?.Invoke(this);
        protected override void OnUpdate() { }
        public Action? OnStopHook;
        protected override void OnStop(BehaviorStopReason reason) => OnStopHook?.Invoke();
        public bool Delegate(HelperRef h, Action<CozmoRobot>? success, Action<CozmoRobot>? failure) => SmartDelegateToHelper(h, success, failure);
        public bool StopHelper() => StopHelperWithoutCallback();
        public void DoStopActing(bool keepAction, bool viaCallback) => StopActing(keepAction, viaCallback);
    }

    private sealed class TestHelper : IHelper
    {
        private readonly List<string> _events;
        public int InitCalls, InternalCalls, CancelAsks;
        public BehaviorStatus InitResult = BehaviorStatus.Running;
        public BehaviorStatus InternalResult = BehaviorStatus.Running;
        public bool Cancel;
        public Action<TestHelper>? OnInit;
        public int OriginReadAtInit = int.MinValue;
        public Func<int>? StoredOrigin;
        public readonly List<bool> Stops = new();

        public TestHelper(CozmoRobot robot, string name, SteppedBehavior behavior, BehaviorHelperComponent component, List<string> events)
            : base(robot, name, behavior, component.Factory) => _events = events;

        public override bool ShouldCancelDelegates(CozmoRobot robot) { CancelAsks++; _events.Add($"{Name}.Cancel?"); return Cancel; }
        protected override BehaviorStatus Init(CozmoRobot robot)
        {
            InitCalls++; _events.Add($"{Name}.Init");
            if (StoredOrigin is not null) OriginReadAtInit = StoredOrigin();
            OnInit?.Invoke(this);
            return InitResult;
        }
        protected override BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot) { InternalCalls++; _events.Add($"{Name}.Internal"); return InternalResult; }
        protected override void OnStopSlot24(bool first) { Stops.Add(first); _events.Add($"{Name}.Stop({first})"); }
    }

    private sealed class Fx : IDisposable
    {
        public readonly Rig Rig = new();
        public readonly List<string> Events = new();
        public readonly List<string> Log = new();
        public int Origin = 1;
        public int OriginReads;
        public readonly BehaviorHelperComponent Component;
        public readonly Probe Behavior = new();
        public CozmoRobot Robot => Rig.Robot;

        public Fx()
        {
            Rig.Robot.Engine.LogLine += l => { lock (Log) Log.Add(l); };
            Component = new BehaviorHelperComponent(() => { OriginReads++; return Origin; }, l => { lock (Log) Log.Add(l); });
            var ctx = new BehaviorContext { Robot = Rig.Robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 1 };
            Behavior.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        }

        public TestHelper H(string name) => new(Robot, name, Behavior, Component, Events) { StoredOrigin = () => Component.StoredWorldOriginId };
        public HelperRef Ref(TestHelper h) { IHelper? raw = h; return Component.AddHelperToComponent(ref raw); }
        public bool Logged(string part) { lock (Log) return Log.Any(l => l.Contains(part)); }
        public void Dispose() => Rig.Dispose();
    }

    // ------------------------------------------------------------------ the status values and the object

    /// <summary>Extraction header, <c>IHelper::LogStopEvent</c> 0x005b6850, <c>UpdateActiveHelper</c> 0x0056dee8/0x0056dedc/0x0056df20: 0 Failure, 1 Running, 2 Complete.</summary>
    [Fact]
    public void M8_011_TheStatusValuesAre0Failure1Running2Complete()
    {
        Assert.Equal(0, (int)BehaviorStatus.Failure);
        Assert.Equal(1, (int)BehaviorStatus.Running);
        Assert.Equal(2, (int)BehaviorStatus.Complete);
    }

    /// <summary>
    /// Row 2.1 (<c>AddHelperToComponent</c> 0x0056da8a): builds the shared_ptr from <c>*raw</c> (new control block, 0x0056da9c..0x0056dac0), writes null into <c>*raw</c>
    /// (0x0056da96) and does NOT append to the vector.
    /// </summary>
    [Fact]
    public void M8_011_AddHelperToComponentWrapsTheRawPointerNullsItAndDoesNotPush()
    {
        using var fx = new Fx();
        var h = fx.H("A");
        IHelper? raw = h;
        var r = fx.Component.AddHelperToComponent(ref raw);
        Assert.Null(raw);
        Assert.Same(h, r.Pointer);
        Assert.Equal(1, r.Control.Owners);
        Assert.Equal(0, fx.Component.StackCount);
    }

    /// <summary>Row 1.2 (constructor 0x0056da56..0x0056da78): the stack and both callbacks start empty; the factory (+0x00) is built with the component.</summary>
    [Fact]
    public void M8_011_AFreshComponentHasAnEmptyStackNoCallbacksAndAFactory()
    {
        using var fx = new Fx();
        Assert.Equal(0, fx.Component.StackCount);
        Assert.False(fx.Component.HasSuccessCallback);
        Assert.False(fx.Component.HasFailureCallback);
        Assert.Same(fx.Component, fx.Component.Factory.Component);   // 0x005b54ec stores the component pointer
    }

    // ------------------------------------------------------------------ DelegateToHelper (rows 2.2, 2.3)

    /// <summary>
    /// Row 2.2 (0x0056dad8..0x0056db32): an empty stack pushes the helper (the first update runs inside the push: Init, then UpdateWhileActiveInternal because the status is
    /// Running and the delegate is null, row 4.4 (c)), returns true, and +0x40 takes <c>GetWorldOriginID</c> only AFTER the push's own update (0x0056db1e then 0x0056db2c/0x0056db30).
    /// Row 2.3: "HelperComponent.StartNewHelper" is logged with the name, and the helper's <c>InitializeOnStack</c> runs first (Running, init not run).
    /// </summary>
    [Fact]
    public void M8_011_DelegateToHelperPushesRunsTheFirstUpdateAndStoresTheOriginAfterIt()
    {
        using var fx = new Fx();
        fx.Origin = 7;
        var a = fx.H("A");
        a.OnInit = _ => fx.Origin = 9;                   // the origin moves during the push's own update
        bool ok = fx.Component.DelegateToHelper(fx.Robot, fx.Ref(a), _ => { }, _ => { });
        Assert.True(ok);
        Assert.Equal(1, fx.Component.StackCount);
        Assert.Equal(new[] { "A.Init", "A.Internal" }, fx.Events);
        // UpdateActiveHelper (row 3.2) stored 7 first (+0x40 was never written, so 7 differs); DelegateToHelper then stores a fresh read AFTER the push: 9
        Assert.Equal(7, a.OriginReadAtInit);
        Assert.Equal(9, fx.Component.StoredWorldOriginId);
        Assert.True(fx.Logged("HelperComponent.StartNewHelper"));
        Assert.Equal(BehaviorStatus.Running, a.Status);
        Assert.True(a.InitHasRun);
    }

    /// <summary>
    /// Row 2.2 (4): a non-empty stack returns false without pushing, and by then both stored callbacks are already the new ones (0x0056dae8..0x0056daf8 before
    /// 0x0056dafc..0x0056db06). When the running helper completes it is therefore the NEW success callback that runs (row 3.14).
    /// </summary>
    [Fact]
    public void M8_011_DelegateToHelperOnANonEmptyStackReturnsFalseAfterOverwritingBothCallbacks()
    {
        using var fx = new Fx();
        var a = fx.H("A");
        var calls = new List<string>();
        Assert.True(fx.Component.DelegateToHelper(fx.Robot, fx.Ref(a), _ => calls.Add("oldSuccess"), _ => calls.Add("oldFailure")));
        var b = fx.H("B");
        Assert.False(fx.Component.DelegateToHelper(fx.Robot, fx.Ref(b), _ => calls.Add("newSuccess"), _ => calls.Add("newFailure")));
        Assert.Equal(0, b.InitCalls);
        Assert.Equal(1, fx.Component.StackCount);
        a.InternalResult = BehaviorStatus.Complete;
        fx.Component.Update(fx.Robot);
        Assert.Equal(new[] { "newSuccess" }, calls);
    }

    // ------------------------------------------------------------------ completion (row 3.14)

    /// <summary>
    /// Row 3.14 (0x0056e0fa..0x0056e15a): on an empty stack and status 2 the success callback is chosen, BOTH stored callbacks are cleared before it runs
    /// (<c>ClearStackMaintenanceVars</c> 0x0056e132), and then it is called with the robot. A callback that delegates again therefore starts a new stack on the cleared component.
    /// </summary>
    [Fact]
    public void M8_011_TheSuccessCallbackRunsAfterTheStoredCallbacksAreClearedAndMayStartANewStack()
    {
        using var fx = new Fx();
        var a = fx.H("A");
        a.InitResult = BehaviorStatus.Complete;   // Init returns 2: not Running, so UpdateWhileActiveInternal is not called in the same update
        bool? successSawCallbacks = null, failureCalled = false;
        var next = fx.H("N");
        bool? nested = null;
        Assert.True(fx.Component.DelegateToHelper(fx.Robot, fx.Ref(a),
            r =>
            {
                successSawCallbacks = fx.Component.HasSuccessCallback || fx.Component.HasFailureCallback;
                nested = fx.Component.DelegateToHelper(r, fx.Ref(next), null, null);
            },
            _ => failureCalled = true));
        Assert.False(successSawCallbacks);
        Assert.False(failureCalled);
        Assert.True(nested);
        Assert.Equal(new[] { "A.Init", "A.Stop(True)", "N.Init", "N.Internal" }, fx.Events);   // rows 3.9 (Stop(true) then pop), 4.4
        Assert.Equal(1, fx.Component.StackCount);
        Assert.Equal(0, a.InternalCalls);                                    // row 4.4 (c): Init returned 2, not 1
    }

    /// <summary>Row 3.14: status 0 chooses the failure callback.</summary>
    [Fact]
    public void M8_011_StatusFailureRunsTheFailureCallbackOnly()
    {
        using var fx = new Fx();
        var a = fx.H("A");
        a.InternalResult = BehaviorStatus.Failure;
        var calls = new List<string>();
        fx.Component.DelegateToHelper(fx.Robot, fx.Ref(a), _ => calls.Add("success"), _ => calls.Add("failure"));
        Assert.Equal(new[] { "failure" }, calls);
        Assert.False(fx.Component.HasSuccessCallback);
        Assert.False(fx.Component.HasFailureCallback);
    }

    /// <summary>
    /// Row 3.14: "any other status copies nothing" - a status that is neither 0 nor 2 (here 3, carried out of the helper's own status) runs no callback, yet the stored
    /// callbacks are still cleared (0x0056e132 is reached on the empty path).
    /// </summary>
    [Fact]
    public void M8_011_AStatusThatIsNeither0Nor2RunsNoCallbackButStillClearsThem()
    {
        using var fx = new Fx();
        var a = fx.H("A");
        a.InternalResult = (BehaviorStatus)3;
        var calls = new List<string>();
        fx.Component.DelegateToHelper(fx.Robot, fx.Ref(a), _ => calls.Add("success"), _ => calls.Add("failure"));
        Assert.Empty(calls);
        Assert.Equal(0, fx.Component.StackCount);
        Assert.False(fx.Component.HasSuccessCallback);
        Assert.False(fx.Component.HasFailureCallback);
    }

    /// <summary>Row 3.14: with the stack still non-empty (a Running helper) no callback is chosen and the callbacks stay stored (0x0056e0fa..0x0056e102).</summary>
    [Fact]
    public void M8_011_ARunningHelperKeepsItsCallbacks()
    {
        using var fx = new Fx();
        fx.Component.DelegateToHelper(fx.Robot, fx.Ref(fx.H("A")), _ => { }, _ => { });
        Assert.True(fx.Component.HasSuccessCallback);
        Assert.True(fx.Component.HasFailureCallback);
    }

    // ------------------------------------------------------------------ UpdateActiveHelper (rows 3.1..3.11)

    /// <summary>Row 3.1 (0x0056de88..0x0056de8e): an empty stack returns at once, with no origin read.</summary>
    [Fact]
    public void M8_011_AnEmptyStackReadsNoOrigin()
    {
        using var fx = new Fx();
        fx.Component.Update(fx.Robot);
        Assert.Equal(0, fx.OriginReads);
    }

    /// <summary>
    /// Row 3.2 (0x0056de94..0x0056deae): an unchanged origin is read once; a changed one is read a second time to store it.
    /// </summary>
    [Fact]
    public void M8_011_AChangedOriginIsReadTwiceAndStored()
    {
        using var fx = new Fx();
        fx.Component.DelegateToHelper(fx.Robot, fx.Ref(fx.H("A")), null, null);
        int afterDelegate = fx.OriginReads;
        fx.Component.Update(fx.Robot);                      // origin 1 == stored 1
        Assert.Equal(afterDelegate + 1, fx.OriginReads);
        fx.Origin = 5;
        int before = fx.OriginReads;
        fx.Component.Update(fx.Robot);                      // 5 != 1
        Assert.Equal(before + 2, fx.OriginReads);
        Assert.Equal(5, fx.Component.StoredWorldOriginId);
    }

    private (Fx fx, TestHelper a, TestHelper b, TestHelper c) ThreeDeep()
    {
        var fx = new Fx();
        var a = fx.H("A"); var b = fx.H("B"); var c = fx.H("C");
        a.OnInit = h => h.Delegation.Delegate = fx.Ref(b);
        b.OnInit = h => h.Delegation.Delegate = fx.Ref(c);
        Assert.True(fx.Component.DelegateToHelper(fx.Robot, fx.Ref(a), null, null));
        return (fx, a, b, c);
    }

    /// <summary>
    /// Row 3.8: a helper that delegates (non-null delegate after its Init, row 4.4 (c) skips the same-call UpdateWhileActiveInternal) has the sub-helper pushed
    /// through <c>PushHelperOntoStackAndUpdate</c> (0x0056dfac), and "the re-entry with sl == 1 calls UpdateWhileActive once more on whatever is then the top in the same
    /// tick" (the top is read after the recursion, 0x0056dfb8). Three levels: C's own push update (1), then B's frame (2), then A's frame (3).
    /// </summary>
    [Fact]
    public void M8_011_AfterPushingASubHelperTheNewTopUpdatesAgainInTheSameTickInEveryFrame()
    {
        var (fx, a, b, c) = ThreeDeep();
        using var guard = fx;
        Assert.Equal(3, fx.Component.StackCount);
        Assert.Equal(new[] { "A", "B", "C" }, fx.Component.StackHelpers.Select(h => h.Name));
        Assert.Equal((1, 0), (a.InitCalls, a.InternalCalls));
        Assert.Equal((1, 0), (b.InitCalls, b.InternalCalls));
        Assert.Equal((1, 3), (c.InitCalls, c.InternalCalls));
        Assert.True(fx.Logged("HelperComponent.UpdateActive.SubHelper"));
        Assert.True(fx.Logged("Helper A delegated to helper B"));      // row 3.8 format "Helper %s delegated to helper %s"
        // the delegate and its handlers were moved out of A and B (row 4.4 (d))
        Assert.Null(a.Delegation.Delegate);
        Assert.Null(b.Delegation.Delegate);
    }

    /// <summary>
    /// Rows 3.5, 3.6, 3.9, 3.10, 3.11 (0x0056deca..0x0056e0e4): a world-origin change stops every helper above the bottom one with status 0, top first - the top is not
    /// told anything first (the carried status is 1), the next is told <c>OnDelegateFailure</c> and then stopped with <c>Stop(true)</c> whatever that returned - and the
    /// bottom helper (stack size 1) is told <c>OnDelegateFailure</c> and then continues by what that returned (Running: its UpdateWhileActive runs). Neither stored
    /// callback runs.
    /// </summary>
    [Fact]
    public void M8_011_AWorldOriginChangeFailsEveryHelperAboveTheBottomOne()
    {
        var (fx, a, b, c) = ThreeDeep();
        using var guard = fx;
        var called = new List<string>();
        fx.Component.DelegateToHelper(fx.Robot, fx.Ref(fx.H("ignored")), _ => called.Add("s"), _ => called.Add("f"));   // refused (non-empty); stores the callbacks
        fx.Events.Clear();
        fx.Origin = 2;
        fx.Component.Update(fx.Robot);
        // CheckInactiveStackHelpers asks A and B (not the top); UpdateActiveHelper stops C, fails B, then A continues
        Assert.Equal(new[] { "A.Cancel?", "B.Cancel?", "C.Stop(True)", "B.Stop(True)", "A.Internal" }, fx.Events);
        Assert.Equal(new[] { "A" }, fx.Component.StackHelpers.Select(h => h.Name));
        Assert.Empty(called);
        Assert.True(fx.Logged("HelperComponent.UpdateActive.Complete"));
        Assert.True(fx.Logged("C no longer running"));
        Assert.True(fx.Logged("returning to helper: B"));
        Assert.True(fx.Logged("returning to helper: A"));
        Assert.Equal(2, fx.Component.StoredWorldOriginId);
    }

    /// <summary>
    /// Row 3.5/3.9: a helper that completes is stopped with <c>Stop(true)</c> and popped; the helper below it is then told <c>OnDelegateSuccess</c> (carried status 2), and
    /// "returning to helper" is logged (row 3.10).
    /// </summary>
    [Fact]
    public void M8_011_ACompletedSubHelperIsStoppedAndTheParentIsToldItSucceeded()
    {
        var (fx, a, b, c) = ThreeDeep();
        using var guard = fx;
        fx.Events.Clear();
        c.InternalResult = BehaviorStatus.Complete;
        fx.Component.Update(fx.Robot);
        // cancel checks on A, B; C completes -> Stop(true); B told OnDelegateSuccess (no handler, status unchanged Running) -> B continues: its UpdateWhileActive runs
        Assert.Equal(new[] { "A.Cancel?", "B.Cancel?", "C.Internal", "C.Stop(True)", "B.Internal" }, fx.Events);
        Assert.Equal(new[] { "A", "B" }, fx.Component.StackHelpers.Select(h => h.Name));
        Assert.True(fx.Logged("returning to helper: B"));
    }

    // ------------------------------------------------------------------ CheckInactiveStackHelpers (row 2.8) and Update (row 2.9)

    /// <summary>
    /// Row 2.8 (0x0056de36..0x0056de66): every element except the top is asked <c>ShouldCancelDelegates</c>; a result of 1 clears from the top down to the NEXT element
    /// inclusive (the iterator is r6 post-incremented), so the cancelling helper stays; only the real top gets <c>Stop(true)</c>. Then <c>Update</c> goes on to
    /// <c>UpdateActiveHelper</c> (row 2.9), which updates the new top.
    /// </summary>
    [Fact]
    public void M8_011_ACancellingHelperStaysAndEverythingAboveItIsStopped()
    {
        var (fx, a, b, c) = ThreeDeep();
        using var guard = fx;
        fx.Events.Clear();
        a.Cancel = true;
        fx.Component.Update(fx.Robot);
        Assert.Equal(new[] { "A.Cancel?", "C.Stop(True)", "B.Stop(False)", "A.Internal" }, fx.Events);
        Assert.Equal(new[] { "A" }, fx.Component.StackHelpers.Select(h => h.Name));
        Assert.Equal(0, c.CancelAsks);       // the top is never asked
    }

    /// <summary>Row 2.8: a cancelling middle helper clears only the top above it.</summary>
    [Fact]
    public void M8_011_AMiddleCancelClearsOnlyTheTop()
    {
        var (fx, a, b, c) = ThreeDeep();
        using var guard = fx;
        fx.Events.Clear();
        b.Cancel = true;
        fx.Component.Update(fx.Robot);
        Assert.Equal(new[] { "A.Cancel?", "B.Cancel?", "C.Stop(True)", "B.Internal" }, fx.Events);
        Assert.Equal(new[] { "A", "B" }, fx.Component.StackHelpers.Select(h => h.Name));
    }

    // ------------------------------------------------------------------ StopHelperWithoutCallback (row 2.6) and ClearStackFromTopToIter (row 2.7)

    /// <summary>
    /// Row 2.6 (0x0056dcc0..0x0056dcd6): the handle is compared with the BOTTOM element. A non-bottom helper returns false and does nothing; the bottom one clears the
    /// whole stack (row 2.7: the real top gets <c>Stop(true)</c>, the rest <c>Stop(false)</c>, the element at the iterator included) and returns true. The stored
    /// callbacks are neither called nor cleared.
    /// </summary>
    [Fact]
    public void M8_011_StopHelperWithoutCallbackActsOnlyForTheBottomHelperAndKeepsTheCallbacks()
    {
        var fx = new Fx();
        using var guard = fx;
        var a = fx.H("A"); var b = fx.H("B"); var c = fx.H("C");
        a.OnInit = h => h.Delegation.Delegate = fx.Ref(b);
        b.OnInit = h => h.Delegation.Delegate = fx.Ref(c);
        var called = new List<string>();
        var aRef = fx.Ref(a);
        fx.Component.DelegateToHelper(fx.Robot, aRef, _ => called.Add("s"), _ => called.Add("f"));
        fx.Events.Clear();

        Assert.False(fx.Component.StopHelperWithoutCallback(fx.Ref(b)));
        Assert.Equal(3, fx.Component.StackCount);
        Assert.Empty(fx.Events);

        Assert.True(fx.Component.StopHelperWithoutCallback(aRef));
        Assert.Equal(new[] { "C.Stop(True)", "B.Stop(False)", "A.Stop(False)" }, fx.Events);
        Assert.Equal(0, fx.Component.StackCount);
        Assert.Empty(called);
        Assert.True(fx.Component.HasSuccessCallback);
        Assert.True(fx.Component.HasFailureCallback);
    }

    /// <summary>
    /// Row 2.6 (0x0056dcc0..0x0056dcc8): no empty-stack guard. An emptied vector keeps its storage, so the compare is against the stale last-bottom pointer: that helper's
    /// handle returns true and ClearStackFromTopToIter returns at once (0x0056dcfa/0x0056dcfc), a different helper returns false, and nothing throws.
    /// </summary>
    [Fact]
    public void M8_011_StopHelperWithoutCallbackOnAnEmptyStackComparesAgainstTheStaleBottomAndDoesNothing()
    {
        using var fx = new Fx();
        var a = fx.H("A");
        var aRef = fx.Ref(a);
        Assert.False(fx.Component.StopHelperWithoutCallback(fx.Ref(fx.H("never"))));   // a vector never used: no bottom to match
        fx.Component.DelegateToHelper(fx.Robot, aRef, null, null);
        Assert.True(fx.Component.StopHelperWithoutCallback(aRef));
        Assert.Equal(0, fx.Component.StackCount);
        fx.Events.Clear();
        Assert.True(fx.Component.StopHelperWithoutCallback(aRef));          // stale bottom == A
        Assert.False(fx.Component.StopHelperWithoutCallback(fx.Ref(fx.H("B"))));
        Assert.Empty(fx.Events);
    }

    /// <summary>
    /// The engine is single threaded; this stack reaches the component from the engine tick and a behaviour thread. The component's monitor is not held while behaviour code
    /// runs (the completion callbacks and IHelper.Stop go through OutsideLock) and the behaviour's _gate is never held when entering it, so StopActing on one thread and
    /// AIComponent.Update on another cannot deadlock.
    /// </summary>
    [Fact]
    public void M8_011_StopActingOnABehaviourThreadAndTheEngineUpdateDoNotDeadlock()
    {
        var (fx, ai, manager) = WithManager();
        using var guard = fx;
        var ctx = new BehaviorContext { Robot = fx.Robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 1, AI = ai };
        var probe = new Probe("p");
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        var stop = false;
        var engine = Task.Run(() => { while (!Volatile.Read(ref stop)) ai.Update(fx.Robot); });
        var behaviour = Task.Run(() =>
        {
            for (int i = 0; i < 300; i++)
            {
                var h = new TestHelper(fx.Robot, "H" + i, fx.Behavior, ai.Helpers, new List<string>());
                var r = NewRef(ai, h);
                probe.Delegate(r, _ => { }, _ => { });
                r.Release();
                probe.StartActing();
                probe.DoStopActing(false, false);
            }
        });
        Assert.True(Task.WaitAll(new Task[] { behaviour }, TimeSpan.FromSeconds(30)), "StopActing and AIComponent.Update deadlocked");
        Volatile.Write(ref stop, true);
        Assert.True(engine.Wait(TimeSpan.FromSeconds(10)), "the engine update thread did not finish");
    }

    /// <summary>Row 2.5 (0x0056dcdc <c>bx lr</c>): <c>ClearStackLifetimeVars</c> is empty.</summary>
    [Fact]
    public void M8_011_ClearStackLifetimeVarsChangesNothing()
    {
        using var fx = new Fx();
        fx.Component.DelegateToHelper(fx.Robot, fx.Ref(fx.H("A")), _ => { }, _ => { });
        fx.Component.ClearStackLifetimeVars();
        Assert.Equal(1, fx.Component.StackCount);
        Assert.True(fx.Component.HasSuccessCallback);
    }

    // ------------------------------------------------------------------ IHelper (rows 4.1..4.8)

    /// <summary>Row 4.1 (constructor 0x005b6428..0x005b6490): +0x1c = 2 (Complete, 0x005b6440), +0x2c = 0, +0x98 = 0, no handlers.</summary>
    [Fact]
    public void M8_011_AFreshHelperIsCompleteWithNothingRun()
    {
        using var fx = new Fx();
        var h = fx.H("H");
        Assert.Equal(BehaviorStatus.Complete, h.Status);
        Assert.False(h.InitHasRun);
        Assert.Equal(0, BitConverter.SingleToInt32Bits(h.StartTime));
        Assert.False(h.HasSuccessHandler);
        Assert.False(h.HasFailureHandler);
        Assert.Equal("H", h.Name);
    }

    /// <summary>Row 4.2: IHelper's vtable gives the +0x0c hook the constant 2 (0x005b5f0c) and the +0x10 hook the constant 0 (0x005b5f10).</summary>
    [Fact]
    public void M8_011_TheRunnableHooksHaveTheConstantDefaults()
    {
        using var fx = new Fx();
        var h = fx.H("H");
        Assert.Equal(2, (int)h.RunnableUpdate());
        Assert.False(h.RunnableWantsToBeActivated());
    }

    /// <summary>
    /// Row 4.3 (<c>InitializeOnStack</c> 0x005b6706): +0x2c = 0, +0x1c = 1, the +0x30 and +0x48 handlers are emptied; the delegate and the +0x68/+0x80 handlers are NOT
    /// cleared.
    /// </summary>
    [Fact]
    public void M8_011_InitializeOnStackResetsTheStateButNotTheDelegateProperties()
    {
        using var fx = new Fx();
        var h = fx.H("H");
        var d = fx.Ref(fx.H("D"));
        h.OnInit = x => { x.Delegation.Delegate = d; x.Delegation.OnSuccessHandler = _ => BehaviorStatus.Complete; x.Delegation.OnFailureHandler = _ => BehaviorStatus.Failure; };
        h.UpdateWhileActive(fx.Robot, out var sub);          // moves the handlers into +0x30/+0x48
        Assert.NotNull(sub);
        Assert.True(h.HasSuccessHandler);
        Assert.True(h.HasFailureHandler);
        h.Delegation.OnSuccessHandler = _ => BehaviorStatus.Complete;
        h.Delegation.Delegate = fx.Ref(fx.H("E"));
        h.InitializeOnStack();
        Assert.Equal(BehaviorStatus.Running, h.Status);
        Assert.False(h.InitHasRun);
        Assert.False(h.HasSuccessHandler);
        Assert.False(h.HasFailureHandler);
        Assert.NotNull(h.Delegation.Delegate);
        Assert.NotNull(h.Delegation.OnSuccessHandler);
    }

    /// <summary>
    /// Row 4.4 (b)(c): the first <c>UpdateWhileActive</c> runs Init (and sets +0x2c and +0x98); a Running Init with a NON-null delegate skips
    /// <c>UpdateWhileActiveInternal</c> (0x005b658e..0x005b65aa), a null delegate runs it in the same call; the second call (+0x2c set) is only Internal (a).
    /// </summary>
    [Fact]
    public void M8_011_TheFirstUpdateRunsInitAndOnlyRunsInternalWhenThereIsNoDelegate()
    {
        using var fx = new Fx();
        var plain = fx.H("P");
        plain.InitializeOnStack();
        Assert.Equal(BehaviorStatus.Running, plain.UpdateWhileActive(fx.Robot, out var none));
        Assert.Null(none);
        Assert.Equal((1, 1), (plain.InitCalls, plain.InternalCalls));
        plain.UpdateWhileActive(fx.Robot, out _);
        Assert.Equal((1, 2), (plain.InitCalls, plain.InternalCalls));

        var delegating = fx.H("D");
        delegating.OnInit = x => x.Delegation.Delegate = fx.Ref(fx.H("S"));
        delegating.InitializeOnStack();
        delegating.UpdateWhileActive(fx.Robot, out var sub);
        Assert.NotNull(sub);
        Assert.Equal((1, 0), (delegating.InitCalls, delegating.InternalCalls));
    }

    /// <summary>
    /// Row 4.4 (d) and 4.6: the delegate is copied out, the +0x68 handler is moved to +0x30 and the +0x80 one to +0x48, and the properties are cleared. Row 4.5:
    /// <c>OnDelegateSuccess</c> calls the +0x30 handler and stores its result at +0x1c, then empties +0x30; <c>OnDelegateFailure</c> calls the +0x48 handler, stores its
    /// result, and then empties the handler at +0x30 - not +0x48 (0x005b6d06..0x005b6d22). With no handler the status is returned unchanged.
    /// </summary>
    [Fact]
    public void M8_011_TheDelegateHandlersAreMovedAndTheFailurePathEmptiesTheSuccessHandler()
    {
        using var fx = new Fx();
        var h = fx.H("H");
        h.OnInit = x =>
        {
            x.Delegation.Delegate = fx.Ref(fx.H("D"));
            x.Delegation.SucceedImmediatelyOnDelegateFailure();    // properties +0x08 -> returns 2
            x.Delegation.FailImmediatelyOnDelegateFailure();       // properties +0x20 -> returns 0
        };
        h.InitializeOnStack();
        h.UpdateWhileActive(fx.Robot, out var sub);
        Assert.NotNull(sub);
        Assert.Null(h.Delegation.Delegate);
        Assert.Null(h.Delegation.OnSuccessHandler);
        Assert.Null(h.Delegation.OnFailureHandler);
        Assert.True(h.HasSuccessHandler);
        Assert.True(h.HasFailureHandler);

        Assert.Equal(BehaviorStatus.Failure, h.OnDelegateFailure(fx.Robot));    // functor body 0x005b7662 returns 0
        Assert.False(h.HasSuccessHandler);    // emptied by the failure path
        Assert.True(h.HasFailureHandler);     // +0x48 stays

        var g = fx.H("G");
        g.OnInit = x => { x.Delegation.Delegate = fx.Ref(fx.H("D2")); x.Delegation.SucceedImmediatelyOnDelegateFailure(); };
        g.InitializeOnStack();
        g.UpdateWhileActive(fx.Robot, out _);
        Assert.Equal(BehaviorStatus.Complete, g.OnDelegateSuccess(fx.Robot));   // functor body 0x005b7606 returns 2
        Assert.False(g.HasSuccessHandler);
        // with no handler left the stored status comes back unchanged
        Assert.Equal(BehaviorStatus.Complete, g.OnDelegateSuccess(fx.Robot));
    }

    /// <summary>
    /// Row 4.7 (<c>IHelper::Stop(bool)</c> 0x005b6768): with <c>first</c> true and the behaviour's current action (+0x84) set it calls
    /// <c>IBehavior::StopActing(false, true)</c> (viaCallback true, so the behaviour's own helper is not stopped); with <c>first</c> false it does not; the +0x24 slot gets
    /// the bool either way. Row 4.8: <c>IsActing</c> is <c>[[this+0x9c]+0x84] != 0</c>.
    /// </summary>
    [Fact]
    public void M8_011_StopWithFirstTrueStopsTheBehavioursActionButNotItsHelper()
    {
        using var fx = new Fx();
        var h = fx.H("H");
        Assert.NotEqual(0, fx.Behavior.StartActing());
        Assert.True(h.IsActing());
        h.Stop(false);
        Assert.True(fx.Behavior.HasCurrentAction);          // first == false: the action stays
        h.Stop(true);
        Assert.False(fx.Behavior.HasCurrentAction);         // StopActing(false, true) cleared +0x84
        Assert.False(h.IsActing());
        Assert.Equal(new[] { false, true }, h.Stops);
        Assert.True(fx.Logged("IHelper.Stop"));
        Assert.True(fx.Logged("H isActive=0, IsActing=1"));  // format "%s isActive=%d, IsActing=%d" (name, first, +0x84 != 0)
    }

    // ------------------------------------------------------------------ the weak reference (row 5.1, 5.2)

    /// <summary>
    /// Row 5.1: the liveness test is <c>[ctl+4] + 1 != 0</c>, "at least one owner is left"; <c>weak_ptr::lock()</c> (0x005bd230) yields nothing once the last owner went.
    /// </summary>
    [Fact]
    public void M8_011_TheWeakReferenceIsLiveWhileAnOwnerIsLeft()
    {
        using var fx = new Fx();
        var r = fx.Ref(fx.H("A"));
        var weak = new HelperWeakRef(r);
        Assert.True(weak.IsLive);
        var locked = weak.Lock();
        Assert.NotNull(locked);
        Assert.Equal(2, r.Control.Owners);
        locked!.Release();
        r.Release();
        Assert.False(weak.IsLive);
        Assert.Null(weak.Lock());
    }

    // ------------------------------------------------------------------ SmartDelegateToHelper / the helper stop in StopActing, through the manager

    private static (Fx fx, AIComponent ai, BehaviorManager manager) WithManager()
    {
        var fx = new Fx();
        var ai = new AIComponent(() => fx.Origin, l => { lock (fx.Log) fx.Log.Add(l); });
        var ctx = new BehaviorContext { Robot = fx.Robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 1, AI = ai };
        var manager = new BehaviorManager(ctx);
        return (fx, ai, manager);
    }

    private static HelperRef NewRef(AIComponent ai, IHelper h) { IHelper? raw = h; return ai.Helpers.AddHelperToComponent(ref raw); }

    /// <summary>
    /// Rows 5.1 and 5.5, through the manager: a behaviour started by the manager delegates in its own Init through <c>SmartDelegateToHelper</c> (the log
    /// "Behavior requesting to delegate to helper %s", the component call, the weak reference stored afterwards); the manager stopping it calls <c>IBehavior::Stop</c> ->
    /// <c>StopActing(0,0)</c> (0x005bd126), which stops the live helper without callback (the helper is the bottom element): <c>Stop(true)</c>, stack empty, and the stored
    /// callbacks are not called and not cleared.
    /// </summary>
    [Fact]
    public void M8_011_AManagerStartedBehaviourDelegatesAndItsStopStopsTheHelperWithoutCallback()
    {
        var (fx, ai, manager) = WithManager();
        using var guard = fx;
        var helper = new TestHelper(fx.Robot, "H", fx.Behavior, ai.Helpers, fx.Events);
        var probe = new Probe("delegator");
        var called = new List<string>();
        bool? delegated = null;
        probe.OnStartHook = p =>
        {
            var r = NewRef(ai, helper);
            delegated = p.Delegate(r, _ => called.Add("s"), _ => called.Add("f"));
            r.Release();                                        // the creator's local goes out of scope
        };
        Assert.True(manager.SwitchToBehaviorBase(probe, 1.0).GetAwaiter().GetResult());
        Assert.True(delegated);
        Assert.Equal(1, ai.Helpers.StackCount);
        Assert.True(probe.HasLiveHelper);
        Assert.True(fx.Logged("delegator.SmartDelegateToHelper"));
        Assert.True(fx.Logged("Behavior requesting to delegate to helper H"));
        Assert.Equal(new[] { "H.Init", "H.Internal" }, fx.Events);

        probe.OnStopHook = () => fx.Events.Add("OnStop");
        manager.Stop(BehaviorStopReason.Cancelled, 2.0);        // IBehavior::Stop
        // 0x005bd0f4..0x005bd108: Stop's own direct helper stop comes first (no log), before +0xa1 is cleared, StopInternal (0x005bd114) and StopActing(0,0) (0x005bd126)
        Assert.Equal(new[] { "H.Init", "H.Internal", "H.Stop(True)", "OnStop" }, fx.Events);
        Assert.Equal(0, ai.Helpers.StackCount);
        Assert.Empty(called);
        Assert.True(ai.Helpers.HasSuccessCallback);
        Assert.True(ai.Helpers.HasFailureCallback);
        Assert.True(fx.Logged("Behavior stopping its helper"));
        // the creator had released its handle, so the pop left no owner: StopActing's gate (0x005bd364..0x005bd36e) finds the helper dead and logs nothing
        Assert.False(fx.Logged("Stopping behavior helper because action stopped without callback"));
    }

    /// <summary>
    /// Rows 5.5 and 5.4 together: when another handle still owns the helper after Stop's own helper stop (here the creator has not released), StopActing(0,0)'s gate
    /// still sees it live (0x005bd364..0x005bd36e) and logs "Stopping behavior helper because action stopped without callback"; its stop then finds an empty stack.
    /// </summary>
    [Fact]
    public void M8_011_AHelperStillOwnedAfterStopsOwnStopIsStoppedAgainByStopActingWithItsLog()
    {
        var (fx, ai, manager) = WithManager();
        using var guard = fx;
        var helper = new TestHelper(fx.Robot, "H", fx.Behavior, ai.Helpers, fx.Events);
        var probe = new Probe("p");
        HelperRef? held = null;
        probe.OnStartHook = p => { held = NewRef(ai, helper); p.Delegate(held, null, null); };   // not released
        Assert.True(manager.SwitchToBehaviorBase(probe, 1.0).GetAwaiter().GetResult());
        manager.Stop(BehaviorStopReason.Cancelled, 2.0);
        Assert.Equal(new[] { "H.Init", "H.Internal", "H.Stop(True)" }, fx.Events);   // stopped once: the second call compares against the stale bottom and clears nothing
        Assert.True(fx.Logged("Stopping behavior helper because action stopped without callback"));
        Assert.Equal(0, ai.Helpers.StackCount);
        held!.Release();
    }

    /// <summary>
    /// Row 5.4 (0x005bd362..0x005bd36e): <c>StopActing</c> stops a helper only when <c>viaCallback</c> is 0 AND the weak reference is set and live; with
    /// <c>viaCallback</c> 1 the helper keeps running.
    /// </summary>
    [Fact]
    public void M8_011_StopActingStopsAHelperOnlyWithoutCallbackAndOnlyWhenItIsLive()
    {
        var (fx, ai, manager) = WithManager();
        using var guard = fx;
        var probe = new Probe("p");
        var ctx = new BehaviorContext { Robot = fx.Robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 1, AI = ai };
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        probe.DoStopActing(false, false);                       // no weak reference yet: nothing to stop, nothing logged
        Assert.False(fx.Logged("StopActing.WithoutCallback.StopHelper"));

        var helper = new TestHelper(fx.Robot, "H", fx.Behavior, ai.Helpers, fx.Events);
        var r = NewRef(ai, helper);
        Assert.True(probe.Delegate(r, null, null));
        r.Release();
        probe.DoStopActing(false, true);                        // viaCallback: the helper is not touched
        Assert.Equal(1, ai.Helpers.StackCount);
        Assert.False(fx.Logged("StopActing.WithoutCallback.StopHelper"));
        probe.DoStopActing(false, false);
        Assert.Equal(0, ai.Helpers.StackCount);
        Assert.Equal(new[] { true }, helper.Stops);
        Assert.True(fx.Logged("StopActing.WithoutCallback.StopHelper"));
        // the weak reference's owner went with the pop: a further StopActing finds no live helper and stops nothing
        Assert.False(probe.HasLiveHelper);
        probe.DoStopActing(false, false);
        Assert.Single(helper.Stops);
    }

    /// <summary>
    /// Row 5.1 (3): a second delegation while the first helper is live warns "Attempted to start a handler while handle already running, stopping running helper" and
    /// stops the running helper without callback first, so the new one pushes onto the emptied stack and the call returns true.
    /// </summary>
    [Fact]
    public void M8_011_ASecondDelegationWhileTheFirstIsLiveStopsTheFirstAndThenDelegates()
    {
        var (fx, ai, manager) = WithManager();
        using var guard = fx;
        var probe = new Probe("p");
        var ctx = new BehaviorContext { Robot = fx.Robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 1, AI = ai };
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        var first = new TestHelper(fx.Robot, "H1", fx.Behavior, ai.Helpers, fx.Events);
        var second = new TestHelper(fx.Robot, "H2", fx.Behavior, ai.Helpers, fx.Events);
        var r1 = NewRef(ai, first); Assert.True(probe.Delegate(r1, null, null)); r1.Release();
        var r2 = NewRef(ai, second); Assert.True(probe.Delegate(r2, null, null)); r2.Release();
        Assert.True(fx.Logged("Attempted to start a handler while handle already running, stopping running helper"));
        Assert.Equal(new[] { true }, first.Stops);
        Assert.Equal(new[] { "H2" }, ai.Helpers.StackHelpers.Select(h => h.Name));
    }

    /// <summary>
    /// Row 5.2: the weak reference is stored AFTER <c>DelegateToHelper</c> returns, so a helper that ran to completion inside the push (the stack is empty again, row 3.14)
    /// has no owner once the creator lets go, and the next delegation skips the warn/stop. The success callback ran, with the callbacks already cleared.
    /// </summary>
    [Fact]
    public void M8_011_AHelperThatCompletedInsideThePushLeavesNoLiveReference()
    {
        var (fx, ai, manager) = WithManager();
        using var guard = fx;
        var probe = new Probe("p");
        var ctx = new BehaviorContext { Robot = fx.Robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 1, AI = ai };
        probe.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        var quick = new TestHelper(fx.Robot, "Q", fx.Behavior, ai.Helpers, fx.Events) { InitResult = BehaviorStatus.Complete };
        bool succeeded = false;
        var r = NewRef(ai, quick);
        Assert.True(probe.Delegate(r, _ => succeeded = true, null));
        Assert.True(succeeded);
        Assert.NotNull(probe.HelperWeakReference);          // stored after the call returned
        r.Release();
        Assert.False(probe.HasLiveHelper);
        var next = new TestHelper(fx.Robot, "N", fx.Behavior, ai.Helpers, fx.Events);
        var r2 = NewRef(ai, next); Assert.True(probe.Delegate(r2, null, null)); r2.Release();
        Assert.False(fx.Logged("Attempted to start a handler while handle already running"));
    }

    /// <summary>
    /// Row 5.1 (6): a refusal (a non-empty stack, because another behaviour's helper runs) returns false, stores no weak reference and logs the id name +
    /// "SmartDelegateToHelper.Failed" (no leading dot) with "Failed to delegate to helper".
    /// </summary>
    [Fact]
    public void M8_011_ARefusedDelegationReturnsFalseAndStoresNoReference()
    {
        var (fx, ai, manager) = WithManager();
        using var guard = fx;
        var ctx = new BehaviorContext { Robot = fx.Robot, Triggers = new AnimationTriggerMap(), ClockSec = () => 1, AI = ai };
        var other = new Probe("other"); other.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        var mine = new Probe("mine"); mine.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        var r1 = NewRef(ai, new TestHelper(fx.Robot, "H1", other, ai.Helpers, fx.Events)); Assert.True(other.Delegate(r1, null, null)); r1.Release();
        var r2 = NewRef(ai, new TestHelper(fx.Robot, "H2", mine, ai.Helpers, fx.Events));
        Assert.False(mine.Delegate(r2, null, null));
        r2.Release();
        Assert.Null(mine.HelperWeakReference);
        Assert.True(fx.Logged("mineSmartDelegateToHelper.Failed"));
        Assert.True(fx.Logged("Failed to delegate to helper"));
    }

    /// <summary>A behaviour whose context has no AIComponent cannot delegate: it throws instead of answering (the engine's [[[this+0x2c]+0x264]+0x10] always exists).</summary>
    [Fact]
    public void M8_011_WithoutAnAIComponentSmartDelegateToHelperThrows()
    {
        using var fx = new Fx();
        var h = fx.H("A");
        var r = fx.Ref(h);
        Assert.Throws<NotSupportedException>(() => fx.Behavior.Delegate(r, null, null));
    }

    // ------------------------------------------------------------------ the engine tick

    private sealed class Unsub : IDisposable
    {
        private readonly Action _a;
        public Unsub(Action a) => _a = a;
        public void Dispose() => _a();
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

    /// <summary>
    /// Row 1.5/1.6, through the live entry: <c>Robot::Update</c> runs <c>AIComponent::Update</c> (0x00513eac) after the first-full-state gate and before
    /// <c>ActionList::Update</c> (0x005140bc), the animation streamer (0x0051410c) and the later components. An engine tick runs the helper stack's update; the order relative
    /// to the later hooks is recorded by wrapping them.
    /// </summary>
    [Fact]
    public void M8_011_TheEngineTickRunsTheHelperStackBeforeTheActionListAndTheStreamer()
    {
        using var fx = new Fx();
        var ai = new AIComponent(() => fx.Origin);
        var h = new TestHelper(fx.Robot, "H", fx.Behavior, ai.Helpers, fx.Events);
        var order = new List<string>();
        var e = fx.Robot.Engine;
        e.AIComponentUpdate = () => { order.Add("ai"); ai.Update(fx.Robot); };
        var actions = e.ActionRunnerUpdate; e.ActionRunnerUpdate = () => { order.Add("actions"); actions?.Invoke(); };
        var streamer = e.AnimationStreamerUpdate; e.AnimationStreamerUpdate = () => { order.Add("streamer"); streamer?.Invoke(); };
        var components = e.RobotComponentsUpdate; e.RobotComponentsUpdate = () => { order.Add("components"); components?.Invoke(); };
        IHelper? raw = h;
        Assert.True(ai.Helpers.DelegateToHelper(fx.Robot, ai.Helpers.AddHelperToComponent(ref raw), null, null));
        Assert.Equal(1, h.InternalCalls);                   // the push's own update
        e.Tick();
        Assert.Equal(2, h.InternalCalls);                   // the engine tick's AIComponent::Update ran the helper once more
        Assert.Equal("ai", order[0]);
        Assert.True(order.IndexOf("ai") < order.IndexOf("actions"));
        if (order.Contains("streamer")) Assert.True(order.IndexOf("ai") < order.IndexOf("streamer"));
        Assert.True(order.IndexOf("ai") < order.IndexOf("components"));
    }

    /// <summary>
    /// Rows 1.7 (0x00513c5c..0x00513c9a): the block is skipped while the first full state has not been handled ([robot+0x34e] == 0), and the
    /// <c>VisionComponent::UpdateAllResults</c> gate returns when non-zero. With no source for the vision result the gate is reported MISSING once and the tick goes on.
    /// </summary>
    [Fact]
    public void M8_011_TheGatesAroundTheHelperTick()
    {
        using var robot = CozmoRobot.CreateOffline();
        var log = new List<string>();
        robot.Engine.LogLine += l => { lock (log) log.Add(l); };
        var missing = new List<string>();
        void onMissing(string m) { lock (missing) missing.Add(m); }
        SteppedBehavior.MissingReported += onMissing;
        using var unsub = new Unsub(() => SteppedBehavior.MissingReported -= onMissing);
        SteppedBehavior.ResetMissingForTests();
        var e = robot.Engine;
        int ticks = 0;
        e.AIComponentUpdate = () => ticks++;
        e.Tick();
        Assert.Equal(1, ticks);
        e.Tick();
        Assert.Equal(2, ticks);
        lock (missing) Assert.Equal(1, missing.Count(m => m.Contains("UpdateAllResults") && m.Contains("0x00513C6E")));   // reported once per process, not per tick

        e.VisionUpdateAllResultsFailed = () => true;
        e.Tick();
        Assert.Equal(2, ticks);                             // non-zero result: warns and returns, the helper tick is skipped
        lock (log) Assert.Contains(log, l => l.Contains("warning: Robot.Update:"));
        e.VisionUpdateAllResultsFailed = () => false;
        e.Tick();
        Assert.Equal(3, ticks);
    }

    /// <summary>Row 1.7: with [robot+0x34e] == 0 the block returns before the helper tick. A robot that has not handled the first full state does not run it.</summary>
    [Fact]
    public void M8_011_NothingRunsBeforeTheFirstFullState()
    {
        using var fx = new Fx();
        var e = fx.Robot.Engine;
        e.Robot!.FirstFullStateHandled = false;
        int ticks = 0;
        e.AIComponentUpdate = () => ticks++;
        e.Tick();
        Assert.Equal(0, ticks);
        e.Robot.FirstFullStateHandled = true;
        e.Tick();
        Assert.Equal(1, ticks);
    }

    /// <summary>
    /// The production wiring: <c>FreeplayStack.Create</c> builds the <c>AIComponent</c> (AIComponent+0x10, 0x00569aa4..0x00569ab2), hands it to the behaviours' context and to the
    /// engine's <c>Robot::Update</c> hook, and an engine tick then runs a helper that a behaviour delegated to through the manager. Dispose removes the hook.
    /// </summary>
    [Fact]
    public void M8_011_FreeplayStackWiresTheAIComponentIntoTheEngineTick()
    {
        var obb = ObbRoot();
        Assert.True(obb is not null, "re-analysis/obb assets not found: the FreeplayStack wiring test cannot run");
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb!, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap(), Random = new Random(3) };
        var events = new List<string>();
        using (var stack = FreeplayStack.Create(obb, rig.Robot, ctx, () => 1.0, rig.Vision, rig.M, withReactions: false, random: new Random(1)))
        {
            Assert.Same(stack.AI, ctx.AI);
            Assert.NotNull(rig.Robot.Engine.AIComponentUpdate);
            var probe = new Probe("live");
            TestHelper? helper = null;
            probe.OnStartHook = p =>
            {
                helper = new TestHelper(rig.Robot, "H", p, stack.AI.Helpers, events);
                var r = NewRef(stack.AI, helper);
                p.Delegate(r, null, null);
                r.Release();
            };
            Assert.True(stack.Manager.SwitchToBehaviorBase(probe, 1.0).GetAwaiter().GetResult());
            Assert.Equal(1, helper!.InternalCalls);
            rig.Robot.Engine.Tick();
            Assert.Equal(2, helper.InternalCalls);
        }
        Assert.Null(rig.Robot.Engine.AIComponentUpdate);
    }
}
