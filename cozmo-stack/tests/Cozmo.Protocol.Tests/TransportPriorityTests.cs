using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

// fidelity: M1-047
public sealed class TransportPriorityTests
{
    private sealed class Host : ITransportThreadScheduler
    {
        internal int Min = 1, Max = 99, Result;
        internal readonly List<string> Calls = new();
        internal readonly List<(Thread Thread, int Policy, int Priority)> Requests = new();
        public int GetPriorityMin(int policy) { Calls.Add($"min:{policy}"); return Min; }
        public int GetPriorityMax(int policy) { Calls.Add($"max:{policy}"); return Max; }
        public int SetPriority(Thread thread, int policy, int priority)
        {
            Calls.Add($"set:{policy}:{priority}");
            Requests.Add((thread, policy, priority));
            Assert.True(thread.IsAlive);
            return Result;
        }
    }

    /// <summary>PRIMARY-SOURCE ORACLE: 0x008367C0 selects3; 0x007FBE0C/14 request both workers in order.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiveConstructorRequestsImmediateThenDeferredEvenWithoutSyncCallback(bool manualPump)
    {
        var host = new Host();
        using var t = new ReliableTransport(null, new ManualClock(), manualPump, threadScheduler: host);
        Assert.Equal(new[] { "min:2", "max:2", "set:2:74", "min:2", "max:2", "set:2:74" }, host.Calls);
        Assert.Equal(t.Executor.ThreadId, host.Requests[0].Thread.ManagedThreadId);
        Assert.Equal("cozmo-transport", host.Requests[0].Thread.Name);
        Assert.Equal("cozmo-transport-timer", host.Requests[1].Thread.Name);
        Assert.NotEqual(host.Requests[0].Thread.ManagedThreadId, host.Requests[1].Thread.ManagedThreadId);
        Assert.All(t.PriorityRequests, r => { Assert.Equal(3, r.EnginePriority); Assert.Equal(2, r.EnginePolicy); });
        if (manualPump) Assert.Null(t.Scheduler); // R36 has no repeating callback, not a missing executor worker.
    }

    /// <summary>0x00833504..1A: s32 difference->f32, multiply bits3F400000, truncate, add minimum.</summary>
    [Theory]
    [InlineData(1, 99, 74)]
    [InlineData(-10, 1, -2)]
    [InlineData(0, 16777217, 12582912)] // s32->f32 rounds 16777217 to 16777216 before multiply.
    [InlineData(7, 7, 7)]
    public void PriorityUsesNativeFloatWidthAndTruncatesBeforeAddingMinimum(int min, int max, int expected)
    {
        var host = new Host { Min = min, Max = max };
        var r = TransportThreadPriority.Request(Thread.CurrentThread, 3, host);
        Assert.Equal(expected, r.HostPriority);
        Assert.Equal(expected, Assert.Single(host.Requests).Priority);
    }

    /// <summary>TaskExecutor compare/beq 0x007FBE02..06 skips the host entirely for default2.</summary>
    [Fact]
    public void DefaultPrioritySkipsBothRangeQueriesAndRequest()
    {
        var host = new Host();
        var logs = new List<TransportPriorityDiagnostic>();
        TransportThreadPriority.Request(Thread.CurrentThread, 2, host, logs.Add);
        Assert.Empty(host.Calls);
        Assert.Empty(logs);
    }

    /// <summary>0x0083352E..32: 0 succeeds; EPERM1 takes the non-error branch; other results fail.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(3, true)]
    public void PermissionDeniedDoesNotEnterFailureBranch(int result, bool failed)
    {
        var host = new Host { Result = result };
        Assert.Equal(failed, TransportThreadPriority.Request(Thread.CurrentThread, 3, host).Failed);
        Assert.Single(host.Requests);
    }

    /// <summary>0x00833596..B2: old policy/priority are -1; exact success channel/tag/body, including final ')'.</summary>
    [Fact]
    public void SuccessfulRequestLogsExactSourceInfoAfterTheHostRequest()
    {
        var host = new Host();
        var logs = new List<TransportPriorityDiagnostic>();
        TransportThreadPriority.Request(Thread.CurrentThread, 3, host, entry =>
        {
            Assert.Equal(new[] { "min:2", "max:2", "set:2:74" }, host.Calls);
            logs.Add(entry);
        });
        Assert.Equal(new TransportPriorityDiagnostic("info", "Unnamed", "SetThreadPriority.Success",
            "Changed thread policy:priority from -1:-1 to 2:74)"), Assert.Single(logs));
    }

    /// <summary>0x00833530..32: EPERM1 jumps past success and failure diagnostics.</summary>
    [Fact]
    public void PermissionDeniedProducesNoSuccessOrFailureLog()
    {
        var host = new Host { Result = 1 };
        var logs = new List<TransportPriorityDiagnostic>();
        TransportThreadPriority.Request(Thread.CurrentThread, 3, host, logs.Add);
        Assert.Empty(logs);
        Assert.Single(host.Requests);
    }

    /// <summary>0x0083354A..56: source failure tag/format, with host-owned strerror text.</summary>
    [Fact]
    public void FailedRequestKeepsSourceErrorFormatAndArguments()
    {
        var host = new Host { Result = 3 };
        var logs = new List<TransportPriorityDiagnostic>();
        TransportThreadPriority.Request(Thread.CurrentThread, 3, host, logs.Add);
        Assert.Equal(new TransportPriorityDiagnostic("error", "", "SetThreadPriority.Failed",
            "Error: host result 3 (res=3) setting thread policy:priority 2:74"), Assert.Single(logs));
    }
}
