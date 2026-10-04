using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-025 / M6-026 (C34.3 S8, V21): the PBI-notification queue <c>Q</c> (0x108DE78), its push 0xA38600 and the flush 0xA38420 (the pop 0xA38518..0xA3856C). The expected value of every step in
/// <see cref="NotificationQueueOracle"/> is the engine's own output: re-analysis/tools/emu/emu_notify.py runs the real push and flush under Unicorn on the same queue setups (the array, its free-list order and the limit
/// are written directly because the init is unread); the allocator and the per-item handler 0xA0188C are logging stand-ins on both sides. None comes from running this implementation.
/// </summary>
public class WwiseNotificationQueueTests
{
    public static IEnumerable<object[]> Names() => NotificationQueueOracle.Scenarios.Keys.Select(k => new object[] { k });

    private static string Id(WwiseNotificationItem? item) => item is null ? "-" : item.Id.ToString();

    private static string Dump(WwiseNotificationQueue q)
    {
        var items = new List<string>();
        for (var p = q.Head; p is not null; p = p.Next)
            items.Add($"{p.Id}:{unchecked((uint)(int)p.Pbi!)}/{unchecked((uint)p.Code)}/{unchecked((uint)p.R2)}/{unchecked((uint)p.R3)}");
        var free = new List<string>();
        for (var p = q.Free; p is not null; p = p.Next) free.Add(p.Id.ToString());
        return $"head={Id(q.Head)} tail={Id(q.Tail)} free={string.Join(",", free)} count={q.Count} queue={string.Join(",", items)}";
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void TheQueueGivesTheEnginesPushAndFlush(string name)
    {
        var (capacity, limit, freeOrder, allocFails, ops, steps) = NotificationQueueOracle.Scenarios[name];
        var log = new List<string>();
        int calls = 0, syncedFrees = 0;
        WwiseBankMemory memory = null!;
        void Sync()
        {
            for (; syncedFrees < memory.FreeCalls; syncedFrees++) log.Add("free");
        }
        memory = new WwiseBankMemory { AllocationFails = () => { Sync(); log.Add("alloc"); return allocFails.Contains(++calls); } };
        var queue = new WwiseNotificationQueue(memory, capacity, limit, freeOrder);
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState())
        {
            Notifications = queue,
            NotificationHandlerA0188C = (pbi, code, r2, r3) =>
            {
                Sync();
                log.Add($"h:{unchecked((uint)(int)pbi!)}/{unchecked((uint)code)}/{unchecked((uint)r2)}/{unchecked((uint)r3)}");
            },
        };
        queue.FlushA38420 = pass.FlushPbiNotifications;
        for (int i = 0; i < ops.Length; i++)
        {
            log.Clear();
            syncedFrees = memory.FreeCalls;
            string result;
            var parts = ops[i].Split(':');
            if (parts[0] == "push")
            {
                try
                {
                    queue.Push(int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]), int.Parse(parts[4]));
                    result = "ok";
                }
                catch (InvalidOperationException)
                {
                    result = "crash";
                }
            }
            else
            {
                pass.FlushPbiNotifications();
                result = "flush";
            }
            Sync();
            Assert.Equal(steps[i], $"{result} | {string.Join(' ', log)} | {Dump(queue)}");
        }
    }

    [Fact]
    public void TheFlushNeedsItsQueueAndItsHandlers()
    {
        // 0xA38420 reads Q (the init of which no row reads), calls 0xA0188C for every item and the teardown for code 4: each is a required collaborator, never a silent default.
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState());
        Assert.Throws<WwiseMissingBehaviourException>(() => pass.FlushPbiNotifications());
        var memory = new WwiseBankMemory();
        var queue = new WwiseNotificationQueue(memory, 2, 8, new[] { 0, 1 });
        pass.Notifications = queue;
        pass.FlushPbiNotifications();                                       // an empty queue: the loop does not run (0xA3846C cmp r3,#0; beq 0xA385A0)
        queue.Push(1, 3, 0, 0);
        Assert.Throws<WwiseMissingBehaviourException>(() => pass.FlushPbiNotifications());
        pass.NotificationHandlerA0188C = (_, _, _, _) => { };
        pass.FlushPbiNotifications();
        queue.Push(2, WwisePbiNotification.TermCode, 1, 0);
        Assert.Throws<WwiseMissingBehaviourException>(() => pass.FlushPbiNotifications());
    }

    [Fact]
    public void ACodeFourItemRunsTheTeardownAfterTheHandlerAndIsThenRemoved()
    {
        // 0xA38480 bl 0xA0188C, 0xA38484..0xA38488 cmp r3,#4, the teardown 0xA384C8..0xA38508, then the pop 0xA38518 (the pbi is the item's [item+4], 0xA38494).
        var calls = new List<string>();
        var memory = new WwiseBankMemory();
        var queue = new WwiseNotificationQueue(memory, 2, 8, new[] { 0, 1 });
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState())
        {
            Notifications = queue,
            NotificationHandlerA0188C = (p, c, r2, r3) => calls.Add($"handle {p}/{c}/{r2}/{r3}"),
            TerminateNotifiedPbiA384C8 = p => calls.Add($"term {p}"),
        };
        queue.Push(7, 4, 1, 0);
        queue.Push(8, 3, 0, 5);
        queue.Push(9, 4, 2, 0);
        pass.FlushPbiNotifications();
        Assert.Equal(new[] { "handle 7/4/1/0", "term 7", "handle 8/3/0/5", "handle 9/4/2/0", "term 9" }, calls);
        Assert.Equal(0u, queue.Count);
    }
}
