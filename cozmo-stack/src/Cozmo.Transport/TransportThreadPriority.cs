using System.Diagnostics;

namespace Cozmo.Transport;

// fidelity: M1-014
/// <summary>The host scheduler boundary; the policy argument records the engine's requested policy.</summary>
internal interface ITransportThreadScheduler
{
    int GetPriorityMin(int enginePolicy);
    int GetPriorityMax(int enginePolicy);
    int SetPriority(Thread thread, int enginePolicy, int priority);
}

// fidelity: M1-014
/// <summary>
/// Windows host policy: use the managed thread priority range and primitive. Policy 2 is the engine's
/// SCHED_RR request, not a claim that Windows implements that scheduling policy. Permission/effect is host-owned.
/// </summary>
internal sealed class ManagedTransportThreadScheduler : ITransportThreadScheduler
{
    internal static readonly ManagedTransportThreadScheduler Instance = new();
    public int GetPriorityMin(int enginePolicy) => (int)ThreadPriority.Lowest;
    public int GetPriorityMax(int enginePolicy) => (int)ThreadPriority.Highest;
    public int SetPriority(Thread thread, int enginePolicy, int priority)
    {
        try { thread.Priority = (ThreadPriority)priority; return 0; }
        catch (UnauthorizedAccessException) { return 1; }
        catch (System.Security.SecurityException) { return 1; }
        catch (ThreadStateException) { return 3; }
        catch (PlatformNotSupportedException) { return 95; }
    }
}

internal readonly record struct TransportPriorityRequest(int ThreadId, int EnginePriority, int EnginePolicy,
    int HostPriority, int Result)
{
    internal bool Failed => Result != 0 && Result != 1;
}

// fidelity: M1-047
internal static class TransportThreadPriority
{
    /// <summary>P3/P4: default2 skips the request; transport3 requests policy2 at f32 75% of the host range.</summary>
    internal static TransportPriorityRequest Request(Thread thread, int priority, ITransportThreadScheduler host)
    {
        // TaskExecutor's constructor skips both setters at 0x007FBE02..06 for priority2.
        if (priority == 2) return new(thread.ManagedThreadId, priority, 0, 0, 0);
        int policy = 2;
        int min = host.GetPriorityMin(policy);
        int max = host.GetPriorityMax(policy);
        // The transport's only selected priority is 3 (0x008367C0). Keep signed subtraction,
        // signed->f32, multiplication, truncation, then signed addition (0x00833500..1A).
        float fraction = BitConverter.Int32BitsToSingle(0x3F400000);
        float range = unchecked(max - min);
        float scaled = range * fraction;
        int requested = unchecked(min + (int)scaled);
        int result = host.SetPriority(thread, policy, requested);
        var request = new TransportPriorityRequest(thread.ManagedThreadId, priority, policy, requested, result);
        // 0x0083352E..32: success0 and EPERM1 do not enter the error path. The host owns error text.
        if (request.Failed) Trace.TraceError("SetThreadPriority.Failed: host result {0}, policy:priority {1}:{2}",
            result, policy, requested);
        return request;
    }
}
