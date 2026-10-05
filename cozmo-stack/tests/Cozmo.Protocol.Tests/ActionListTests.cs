using System.Net;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// B-ACTIONS batch 1: the engine's ActionList, ActionQueue, the tick and the six QueueActionPositions, from
/// re-analysis/research/20261004-actionlist-extraction.md (rows A1..A9, T5..T8, Q1..Q15, C1..C6). Every expected
/// value is a value from that report or the shipped Unity enum, never a constant read back from the implementation.
/// </summary>
public class ActionListTests
{
    // ------------------------------------------------------------------ fake runner

    /// <summary>A queue-facing IActionRunner whose Update result is scripted (T7/T8).</summary>
    private sealed class FakeAction : IActionRunner
    {
        public uint State { get; set; } = EngineActionResult.NotStarted;
        public byte RetriesRemain { get; set; }
        public uint OriginalTag { get; set; }
        public uint Tag { get; set; }
        public int Type { get; set; }
        public uint RequiredTrackMask { get; set; }
        public bool SuppressTrackLocking { get; set; }

        /// <summary>T8: the full result the scripted Update returns and stores.</summary>
        public uint UpdateResult { get; set; } = EngineActionResult.Running;
        public bool Interruptible { get; set; } = true;

        public int UpdateCalls;
        public bool Cancelled;
        public bool ResetCalled;
        public bool ResetArg;
        public bool PrepCalled;
        public bool EndingCalled;
        public bool Unlocked;
        public Action? OnUpdate;

        public uint Update()
        {
            UpdateCalls++;
            OnUpdate?.Invoke();
            State = UpdateResult;
            return UpdateResult;
        }

        public void Cancel()
        {
            Cancelled = true;
            if (State != EngineActionResult.NotStarted) State = EngineActionResult.Cancelled;
        }

        public void Reset(bool unlockTracks) { ResetCalled = true; ResetArg = unlockTracks; }
        public bool CanInterrupt() => Interruptible;
        public void Prep() => PrepCalled = true;
        public void WatcherEnding() => EndingCalled = true;
        public void UnlockTracks() => Unlocked = true;
        public bool SetTag(uint tag) { Tag = tag; return true; }
    }

    /// <summary>A concrete ActionRunner so the C1/C2/C3 base rules can be driven.</summary>
    private sealed class ConcreteRunner : ActionRunner
    {
        public ConcreteRunner() : base(type: 7, requiredTrackMask: 0) { }
        public override uint Update() => EngineActionResult.Running;
        public override bool CanInterrupt() => true;
        public override uint CheckIfDone() => EngineActionResult.Success;
    }

    private static ActionList NewList() => new();

    // ------------------------------------------------------------------ A / T

    /// <summary>A7: IsEmpty is the map count, not the current/pending of the queues.</summary>
    [Fact]
    public void A7_IsEmptyIsTheMapCountNotCurrentOrPending()
    {
        var list = NewList();
        var a = new FakeAction();
        Assert.True(list.IsEmpty);

        Assert.Equal(0, list.QueueAction(QueueActionPosition.AtEnd, a));
        Assert.False(list.IsEmpty);                        // A7: a queue node exists even with no current
        Assert.Null(list.QueueAt(0)!.Current);             // A8: inspection does not promote
        Assert.Equal(a, list.QueueAt(0)!.Front);
    }

    /// <summary>A8: GetCurrentAction prefers the current, then the pending front, and does not promote.</summary>
    [Fact]
    public void A8_GetCurrentActionPrefersCurrentThenFront()
    {
        var list = NewList();
        var a = new FakeAction();
        var b = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, a);
        list.QueueAction(QueueActionPosition.AtEnd, b);

        Assert.Equal(a, list.GetCurrentAction());          // pending front, no promote
        Assert.Equal(2, list.QueueAt(0)!.PendingCount);

        list.Update();                                     // promote a, tick it (RUNNING)
        Assert.Equal(a, list.GetCurrentAction());          // now the current
        Assert.Equal(1, list.QueueAt(0)!.PendingCount);    // b still pending
    }

    /// <summary>T6: the watcher ParentActionUpdating runs BEFORE the runner's Update.</summary>
    [Fact]
    public void T6_WatcherParentActionUpdatingRunsBeforeRunnerUpdate()
    {
        var list = NewList();
        var a = new FakeAction();
        bool parentSeen = false;
        a.OnUpdate = () => parentSeen = ReferenceEquals(list.Watcher.Parent, a);

        list.QueueAction(QueueActionPosition.AtEnd, a);
        list.Update();

        Assert.True(parentSeen);
        Assert.Equal(1, a.UpdateCalls);
    }

    /// <summary>T5: the first non-zero queue result is retained; the watcher runs after all queues.</summary>
    [Fact]
    public void T5_FirstNonZeroQueueResultIsRetained()
    {
        var list = NewList();
        var main = new FakeAction { UpdateResult = EngineActionResult.Running };          // queue 0 -> 0
        var parallel = new FakeAction { UpdateResult = EngineActionResult.Timeout };      // queue 1 -> 1
        list.QueueAction(QueueActionPosition.AtEnd, main);
        list.QueueAction(QueueActionPosition.InParallel, parallel);

        int result = list.Update();

        Assert.Equal(1, result);                           // T5: first non-zero kept
        Assert.Equal(1, main.UpdateCalls);
        Assert.Equal(1, parallel.UpdateCalls);             // all queues update despite the failure
        Assert.Equal(1, list.Watcher.UpdateCount);         // watcher after the queues
    }

    /// <summary>T8: a queue returns 0 only for the full results SUCCESS 0 and CANCELLED 0x02000000, else 1.</summary>
    [Theory]
    [InlineData(0x00000000u, 0)]   // SUCCESS
    [InlineData(0x02000000u, 0)]   // CANCELLED (T8's 0x02000000 rule)
    [InlineData(0x02000001u, 1)]   // NOT_STARTED
    [InlineData(0x03000018u, 1)]   // TIMEOUT
    public void T8_NonRunningQueueResultIsZeroOnlyForSuccessAndCancelled(uint fullResult, int expected)
    {
        var list = NewList();
        var a = new FakeAction { UpdateResult = fullResult };
        list.QueueAction(QueueActionPosition.AtEnd, a);

        Assert.Equal(expected, list.Update());
        Assert.True(a.EndingCalled);                       // the terminal current is Deleted
        Assert.Null(list.GetCurrentAction());
    }

    /// <summary>T7: an exact RUNNING keeps the current and returns 0.</summary>
    [Fact]
    public void T7_ExactRunningRetainsTheCurrent()
    {
        var list = NewList();
        var a = new FakeAction { UpdateResult = 0x01000000u };
        list.QueueAction(QueueActionPosition.AtEnd, a);

        Assert.Equal(0, list.Update());
        Assert.Equal(a, list.GetCurrentAction());
        Assert.False(a.EndingCalled);
        Assert.Equal(0, list.QueueAt(0)!.PendingCount);
    }

    /// <summary>T5: a queue is erased only when its pending count is 0 and its current is null.</summary>
    [Fact]
    public void T5_QueueIsErasedOnlyWhenEmpty()
    {
        var list = NewList();
        var a = new FakeAction();
        var b = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, a);
        list.QueueAction(QueueActionPosition.AtEnd, b);

        list.Update();                                     // a RUNNING: current a, pending b
        Assert.Equal(1, list.QueueCount);                  // not erased

        a.UpdateResult = EngineActionResult.Success;
        list.Update();                                     // a deleted; b promoted and RUNNING
        Assert.Equal(1, list.QueueCount);

        b.UpdateResult = EngineActionResult.Success;
        list.Update();                                     // b deleted; queue empty
        Assert.Equal(0, list.QueueCount);
        Assert.True(list.IsEmpty);
    }

    // ------------------------------------------------------------------ Q

    /// <summary>Q1: a null action and a position above 5 return API 1 with no delete.</summary>
    [Fact]
    public void Q1_NullActionAndBadPositionReturnApiOne()
    {
        var list = NewList();
        Assert.Equal(1, list.QueueAction(QueueActionPosition.Now, null));

        var a = new FakeAction();
        Assert.Equal(1, list.QueueAction((QueueActionPosition)6, a));
        Assert.Equal(0, list.QueueCount);
        Assert.False(a.EndingCalled);                      // no incoming delete
    }

    /// <summary>Q2: the disabled gate discards external TAGS; BAD_TAG is the STATE +0x18 and is checked outside the gate.</summary>
    [Fact]
    public void Q2_DisabledGateRejectsExternalTagRangesAndBadTag()
    {
        var list = NewList();
        list.ExternalActionsDisabled = true;

        var low = new FakeAction { Tag = 0x00000001 };                 // 1
        Assert.Equal(0, list.QueueAction(QueueActionPosition.AtEnd, low));   // discard, API 0
        Assert.True(low.EndingCalled);
        Assert.Equal(1, list.QueueCount);                              // Q2: the discard emplaces key 0
        Assert.False(list.IsEmpty);

        var high = new FakeAction { Tag = 0x002DC6C0 };                // 3,000,000
        Assert.Equal(0, list.QueueAction(QueueActionPosition.AtEnd, high));
        Assert.Equal(1, list.QueueCount);

        var engine = new FakeAction { Tag = 0x002DC6C1 };              // engine tag: allowed by the disabled gate
        Assert.Equal(0, list.QueueAction(QueueActionPosition.AtEnd, engine));
        Assert.Equal(1, list.QueueCount);

        // BAD_TAG is the action state +0x18, checked outside the robot+0x2C7 gate.
        list.ExternalActionsDisabled = false;
        var bad = new FakeAction { State = EngineActionResult.BadTag };
        Assert.Equal(0, list.QueueAction(QueueActionPosition.AtEnd, bad));
        Assert.True(bad.EndingCalled);
        Assert.False(list.IsEmpty);

        list.ExternalActionsDisabled = true;
        var badDisabled = new FakeAction { State = EngineActionResult.BadTag };
        Assert.Equal(0, list.QueueAction(QueueActionPosition.AtEnd, badDisabled));
        Assert.False(list.IsEmpty);

        list.ExternalActionsDisabled = false;
        var external = new FakeAction { Tag = 0x000F4240 };            // 1,000,000: allowed when the gate is off
        Assert.Equal(0, list.QueueAction(QueueActionPosition.AtEnd, external));
        Assert.Equal(1, list.QueueCount);
    }

    /// <summary>Q5/Q6: QueueNow with pending deletes the current WITHOUT Cancel and prepends the incoming.</summary>
    [Fact]
    public void Q6_QueueNowWithPendingDeletesCurrentWithoutCancel()
    {
        var list = NewList();
        var running = new FakeAction { UpdateResult = EngineActionResult.Running };
        var pending = new FakeAction();
        var incoming = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, running);
        list.Update();                                     // running is current
        list.QueueAction(QueueActionPosition.AtEnd, pending);

        Assert.Equal(0, list.QueueAction(QueueActionPosition.Now, incoming));

        Assert.False(running.Cancelled);                   // Q6: no Cancel
        Assert.True(running.EndingCalled);                 // but Deleted
        Assert.Equal(EngineActionResult.Running, running.State);   // the old state can remain RUNNING
        Assert.Null(list.QueueAt(0)!.Current);
        Assert.Equal(new[] { incoming, pending }, list.QueueAt(0)!.Pending);
    }

    /// <summary>Q6: the queue assigns the incoming +8 retries from the byte argument.</summary>
    [Fact]
    public void Q6_QueueNowAssignsIncomingRetries()
    {
        var list = NewList();
        var a = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, a, retries: 3);
        Assert.Equal(3, a.RetriesRemain);
    }

    /// <summary>Q7: QueueNow with empty pending Cancels the current before Deleting it, then appends.</summary>
    [Fact]
    public void Q7_QueueNowWithEmptyPendingCancelsCurrentThenAtEnd()
    {
        var list = NewList();
        var running = new FakeAction { UpdateResult = EngineActionResult.Running };
        var incoming = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, running);
        list.Update();                                     // running is current

        Assert.Equal(0, list.QueueAction(QueueActionPosition.Now, incoming));

        Assert.True(running.Cancelled);                    // Q7: Cancel before Delete
        Assert.True(running.EndingCalled);
        Assert.Null(list.QueueAt(0)!.Current);
        Assert.Equal(new[] { incoming }, list.QueueAt(0)!.Pending);
    }

    /// <summary>Q10: NEXT with current R and pending [A,B] inserts before the second node: R,[A,new,B].</summary>
    [Fact]
    public void Q10_NextWithCurrentAndTwoPendingInsertsBeforeSecond()
    {
        var list = NewList();
        var r = new FakeAction { UpdateResult = EngineActionResult.Running };
        var a = new FakeAction();
        var b = new FakeAction();
        var n = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, r);
        list.Update();                                     // r is current
        list.QueueAction(QueueActionPosition.AtEnd, a);
        list.QueueAction(QueueActionPosition.AtEnd, b);

        list.QueueAction(QueueActionPosition.Next, n);

        Assert.Equal(r, list.GetCurrentAction());
        Assert.Equal(new[] { a, n, b }, list.QueueAt(0)!.Pending);
        Assert.Equal(0, n.UpdateCalls);                    // no immediate Init/Update
    }

    /// <summary>Q11: AT_END appends at the tail.</summary>
    [Fact]
    public void Q11_AtEndAppendsAtTail()
    {
        var list = NewList();
        var a = new FakeAction();
        var b = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, a);
        list.QueueAction(QueueActionPosition.AtEnd, b);
        Assert.Equal(new[] { a, b }, list.QueueAt(0)!.Pending);
    }

    /// <summary>Q9: NOW_AND_CLEAR_REMAINING Cancels across ALL queues then QueueNext on key 0.</summary>
    [Fact]
    public void Q9_NowAndClearRemainingCancelsAllThenQueueNext()
    {
        var list = NewList();
        var mainCurrent = new FakeAction { UpdateResult = EngineActionResult.Running };
        var mainPending = new FakeAction();
        var parallel = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, mainCurrent);
        list.Update();                                     // mainCurrent is current
        list.QueueAction(QueueActionPosition.AtEnd, mainPending);
        list.QueueAction(QueueActionPosition.InParallel, parallel);
        list.Update();                                     // parallel is current of queue 1

        var incoming = new FakeAction();
        Assert.Equal(0, list.QueueAction(QueueActionPosition.NowAndClearRemaining, incoming));

        Assert.True(mainCurrent.Cancelled);
        Assert.True(mainPending.EndingCalled);
        Assert.False(mainPending.Cancelled);               // pending deleted without Cancel
        Assert.True(parallel.Cancelled);                   // Q9: across ALL queues
        Assert.Equal(new[] { incoming }, list.QueueAt(0)!.Pending);
        Assert.Null(list.QueueAt(0)!.Current);
    }

    /// <summary>Q12: IN_PARALLEL picks the lowest free positive signed key from 1.</summary>
    [Fact]
    public void Q12_InParallelChoosesLowestFreePositiveKey()
    {
        var list = NewList();
        var p1 = new FakeAction();
        var p2 = new FakeAction();
        list.QueueAction(QueueActionPosition.InParallel, p1);
        list.QueueAction(QueueActionPosition.InParallel, p2);
        list.QueueAction(QueueActionPosition.AtEnd, new FakeAction());   // key 0

        Assert.Equal(new[] { 0, 1, 2 }, list.QueueKeys);

        list.QueueAction(QueueActionPosition.InParallel, new FakeAction());
        Assert.Equal(new[] { 0, 1, 2, 3 }, list.QueueKeys);
    }

    /// <summary>Q13: NOW_AND_RESUME with no current and pending queues Now (prepend before the front).</summary>
    [Fact]
    public void Q13_NowAndResumeWithNoCurrentAndPendingQueuesNow()
    {
        var list = NewList();
        var a = new FakeAction();
        var incoming = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, a);

        list.QueueAction(QueueActionPosition.NowAndResume, incoming);

        Assert.Equal(new[] { incoming, a }, list.QueueAt(0)!.Pending);

        var empty = NewList();
        var only = new FakeAction();
        empty.QueueAction(QueueActionPosition.NowAndResume, only);
        Assert.Equal(new[] { only }, empty.QueueAt(0)!.Pending);           // neither -> AtEnd
    }

    /// <summary>Q14: NOW_AND_RESUME interrupts the current: Reset(false), state 0x03000009, pending [new, old, residual].</summary>
    [Fact]
    public void Q14_NowAndResumeInterruptsCurrentToInterruptedState()
    {
        var list = NewList();
        var running = new FakeAction { UpdateResult = EngineActionResult.Running };
        var residual = new FakeAction();
        var incoming = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, running);
        list.Update();                                     // running is current, RUNNING
        list.QueueAction(QueueActionPosition.AtEnd, residual);

        list.QueueAction(QueueActionPosition.NowAndResume, incoming);

        Assert.True(running.ResetCalled);
        Assert.False(running.ResetArg);                    // Reset(false)
        Assert.Equal(0x03000009u, running.State);          // Q14 interrupted
        Assert.True(running.Unlocked);                     // +56 false and RUNNING -> unlock
        Assert.False(running.EndingCalled);                // interruption does not destroy
        Assert.Null(list.QueueAt(0)!.Current);
        Assert.Equal(new[] { incoming, running, residual }, list.QueueAt(0)!.Pending);
    }

    /// <summary>Q15: a refused Interrupt logs and falls back to QueueNow (Q6/Q7).</summary>
    [Fact]
    public void Q15_RefusedInterruptFallsBackToQueueNow()
    {
        var list = NewList();
        var running = new FakeAction { UpdateResult = EngineActionResult.Running, Interruptible = false };
        var incoming = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, running);
        list.Update();                                     // running is current

        list.QueueAction(QueueActionPosition.NowAndResume, incoming);

        Assert.True(running.Cancelled);                    // Q7 fallback: pending empty -> Cancel+Delete
        Assert.True(running.EndingCalled);
        Assert.Equal(EngineActionResult.Cancelled, running.State);
        Assert.Equal(new[] { incoming }, list.QueueAt(0)!.Pending);
    }

    /// <summary>Q15 with a pending fallback: the current is Deleted WITHOUT Cancel and the incoming is prepended.</summary>
    [Fact]
    public void Q15_RefusedInterruptWithPendingUsesQueueNowNonEmptyBranch()
    {
        var list = NewList();
        var running = new FakeAction { UpdateResult = EngineActionResult.Running, Interruptible = false };
        var pending = new FakeAction();
        var incoming = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, running);
        list.Update();
        list.QueueAction(QueueActionPosition.AtEnd, pending);

        list.QueueAction(QueueActionPosition.NowAndResume, incoming);

        Assert.False(running.Cancelled);                   // Q6 branch: no Cancel
        Assert.True(running.EndingCalled);
        Assert.Equal(new[] { incoming, pending }, list.QueueAt(0)!.Pending);
    }

    // ------------------------------------------------------------------ C

    /// <summary>C4: Cancel(type) matches the current and pending on +0x44; -1 is the wildcard.</summary>
    [Fact]
    public void C4_CancelByType()
    {
        var list = NewList();
        var current = new FakeAction { Type = 1, UpdateResult = EngineActionResult.Running };
        var keep = new FakeAction { Type = 2 };
        var drop = new FakeAction { Type = 1 };
        list.QueueAction(QueueActionPosition.AtEnd, current);
        list.Update();
        list.QueueAction(QueueActionPosition.AtEnd, keep);
        list.QueueAction(QueueActionPosition.AtEnd, drop);

        Assert.True(list.Cancel(1));

        Assert.True(current.Cancelled);
        Assert.False(keep.EndingCalled);
        Assert.True(drop.EndingCalled);
        Assert.False(drop.Cancelled);                      // C4: pending deleted WITHOUT Cancel
        Assert.Equal(EngineActionResult.NotStarted, drop.State);   // pending unstarted results stay NOT_STARTED
        Assert.Equal(new[] { keep }, list.QueueAt(0)!.Pending);
    }

    /// <summary>C5: Cancel(idTag) compares the current's +0x60, not the original +0x5C.</summary>
    [Fact]
    public void C5_CancelByTagUsesCurrentTag()
    {
        var list = NewList();
        var current = new FakeAction { OriginalTag = 100, Tag = 100, UpdateResult = EngineActionResult.Running };
        var pending = new FakeAction { OriginalTag = 100, Tag = 200 };
        list.QueueAction(QueueActionPosition.AtEnd, current);
        list.Update();
        list.QueueAction(QueueActionPosition.AtEnd, pending);

        Assert.True(list.Cancel(100u));                    // matches the current tag
        Assert.True(current.Cancelled);
        Assert.False(pending.EndingCalled);                // 200 does not match
        Assert.Equal(new[] { pending }, list.QueueAt(0)!.Pending);
    }

    /// <summary>C6: Cancel leaves the empty queue nodes in the map; T5 erases them.</summary>
    [Fact]
    public void C6_CancelBeforeTickKeepsIsEmptyFalseUntilT5Erases()
    {
        var list = NewList();
        var a = new FakeAction();
        var b = new FakeAction();
        list.QueueAction(QueueActionPosition.AtEnd, a);
        list.QueueAction(QueueActionPosition.AtEnd, b);
        Assert.False(list.IsEmpty);

        Assert.True(list.Cancel(-1));

        Assert.False(list.IsEmpty);                        // C6/A7: no node erase here
        Assert.Equal(1, list.QueueCount);

        list.Update();

        Assert.True(list.IsEmpty);                         // T5 erased the empty queue
        Assert.Equal(0, list.QueueCount);
    }

    /// <summary>C1: the global counter seeds 0x002DC6C1, increments after return and wraps 0 to the seed.</summary>
    [Fact]
    public void C1_TagCounterSeedIncrementAndWrap()
    {
        var counter = new ActionRunnerTagCounter();
        Assert.Equal(0x002DC6C1u, counter.NextIdTag());    // C1: ELF data 0x01051020
        Assert.Equal(0x002DC6C2u, counter.NextIdTag());

        var wrap = new ActionRunnerTagCounter(0xFFFFFFFFu);
        Assert.Equal(0xFFFFFFFFu, wrap.NextIdTag());
        Assert.Equal(0x002DC6C1u, wrap.NextIdTag());       // C1: wrapped 0 replaced by the seed
    }

    /// <summary>C2: SetTag refuses while RUNNING; otherwise a non-zero unique tag stores, zero/collision fails.</summary>
    [Fact]
    public void C2_SetTagRules()
    {
        var a = new ConcreteRunner();
        uint original = a.OriginalTag;
        Assert.Equal(a.OriginalTag, a.Tag);                // C1: original +0x5C and current +0x60 start equal

        a.State = EngineActionResult.Running;
        Assert.False(a.SetTag(0x00123456u));               // RUNNING refuses
        Assert.Equal(EngineActionResult.BadTag, a.State);  // C2: refusal sets +0x18 BAD_TAG
        Assert.NotEqual(0x00123456u, a.Tag);

        a.State = EngineActionResult.NotStarted;
        Assert.True(a.SetTag(0x00123456u));                // unique non-zero stores
        Assert.Equal(0x00123456u, a.Tag);
        Assert.Equal(original, a.OriginalTag);             // original remains

        var b = new ConcreteRunner();
        Assert.False(b.SetTag(a.Tag));                     // collision fails while a holds the tag
        Assert.Equal(EngineActionResult.BadTag, b.State);

        Assert.False(a.SetTag(0u));                        // zero fails
        Assert.Equal(EngineActionResult.BadTag, a.State);
    }

    /// <summary>C3: runner Cancel leaves NOT_STARTED unchanged and sets CANCELLED otherwise.</summary>
    [Fact]
    public void C3_RunnerCancelRules()
    {
        var a = new ConcreteRunner();
        a.State = EngineActionResult.NotStarted;
        a.Cancel();
        Assert.Equal(EngineActionResult.NotStarted, a.State);   // unchanged

        a.State = EngineActionResult.Running;
        a.Cancel();
        Assert.Equal(EngineActionResult.Cancelled, a.State);
    }

    // ------------------------------------------------------------------ the live entry

    /// <summary>
    /// The live entry: Robot::Update's ActionList step (0x005140BC) ticks the queue through CozmoEngine.Update
    /// (EngineRobot.Update), not a helper. The first full state is set by the offline seam.
    /// </summary>
    [Fact]
    public void T5_TheTickRunsThroughCozmoEngineUpdate()
    {
        using var robot = CozmoRobot.CreateOffline();
        var engineRobot = robot.Engine.Robots.Get(CozmoEngine.RobotId);
        Assert.NotNull(engineRobot);
        Assert.True(engineRobot!.FirstFullStateHandled);

        var a = new FakeAction { UpdateResult = EngineActionResult.Running };
        engineRobot.ActionList.QueueAction(QueueActionPosition.AtEnd, a);

        robot.Engine.Tick();

        Assert.Equal(1, a.UpdateCalls);
        Assert.Equal(a, engineRobot.ActionList.GetCurrentAction());
        Assert.Equal(1, engineRobot.ActionList.Watcher.UpdateCount);
    }
}
