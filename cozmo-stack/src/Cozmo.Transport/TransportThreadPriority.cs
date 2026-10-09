using System.Diagnostics;

namespace Cozmo.Transport;

// fidelity: M1-014
/// <summary>The host scheduler boundary; the policy argument records the engine's requested policy.</summary>
internal interface ITransportThreadScheduler
{
    int GetPriorityMin(int enginePolicy);
    int GetPriorityMax(int enginePolicy);
    int SetPriority(Thread thread, int enginePolicy, int priority);
    // The host supplies its error text in place of the phone's errno/strerror primitive.
    string GetErrorText(int result) => $"host result {result}";
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

internal readonly record struct TransportPriorityDiagnostic(string Level, string Channel, string Tag, string Body);

// fidelity: M1-047
internal static class TransportThreadPriority
{
    /// <summary>P3/P4: default2 skips the request; transport3 requests policy2 at f32 75% of the host range.</summary>
    internal static TransportPriorityRequest Request(Thread thread, int priority, ITransportThreadScheduler host,
        Action<TransportPriorityDiagnostic>? diagnostic = null)
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
        // 0x00833596..B2: result0 logs channel/tag/format; the old policy and priority are literal -1.
        if (result == 0)
            Emit(new("info", "Unnamed", "SetThreadPriority.Success",
                FormattableString.Invariant($"Changed thread policy:priority from -1:-1 to {policy}:{requested})")), diagnostic);
        // 0x00833530..32: EPERM1 skips both success and failure. Other errors log before errG/break gate.
        else if (request.Failed)
        {
            Emit(new("error", "", "SetThreadPriority.Failed",
                FormattableString.Invariant($"Error: {host.GetErrorText(result)} (res={result}) setting thread policy:priority {policy}:{requested}")), diagnostic);
            EngineErrorState.StoreAndMaybeBreak();
        }
        return request;
    }

    private static void Emit(TransportPriorityDiagnostic entry, Action<TransportPriorityDiagnostic>? diagnostic)
    {
        if (diagnostic is not null) { diagnostic(entry); return; }
        if (entry.Level == "info") Trace.TraceInformation("{0}/{1}: {2}", entry.Channel, entry.Tag, entry.Body);
        else Trace.TraceError("{0}: {1}", entry.Tag, entry.Body);
    }
}
