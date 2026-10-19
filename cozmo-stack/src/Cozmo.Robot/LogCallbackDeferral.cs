namespace Cozmo.Robot;

/// <summary>
/// B-M11-8: while a thread holds EngineRobot.PoseTreeGate, the log callbacks it would run (LogEntry, the stack log, the
/// display, the MISSING channel, the DAS call observer) are queued on that thread and run, in their original order, once
/// the gate is released. A subscriber that re-enters the engine then cannot deadlock on the gate. Each entry is still built
/// at the moment it is emitted (its "(tcNNNN)" prefix included); only the dispatch to subscribers waits. Other threads are
/// unaffected, and with no deferral open every call runs at once, as before. Stack plumbing, not an engine behaviour.
///
/// The drain isolates each dispatch (M1-034 handler isolation): a throw is handed to the dispatch's fault handler (the
/// engine's ReportFault) and the rest still run. A dispatch with no owning engine (the static MISSING channel, the DAS
/// observer) that throws is reported through <see cref="System.Diagnostics.Trace"/>; after a gated body that completed
/// normally the first such exception is rethrown once the whole queue has run (as the undeferred call would have thrown it to
/// its caller), and after a body that threw it is not rethrown, so it never masks the exception in flight.
/// </summary>
internal static class LogCallbackDeferral
{
    private readonly record struct Pending(Action Dispatch, Action<Exception>? Fault);
    [ThreadStatic] private static List<Pending>? t_pending;
    [ThreadStatic] private static int t_depth;

    // A scope is needed for native handler windows spanning several helpers. It shares the same ordered queue
    // as RunGated, so nested pose/light/history gates cannot flush public callbacks while a frame gate is held.
    internal static IDisposable EnterGated(object gate)
    {
        if(t_depth++==0)t_pending=new List<Pending>();
        Monitor.Enter(gate);
        return new HeldGate(gate);
    }
    private sealed class HeldGate(object gate):IDisposable
    {
        private object? _gate=gate;
        public void Dispose()
        {
            var held=Interlocked.Exchange(ref _gate,null);if(held is null)return;
            Monitor.Exit(held);
            if(--t_depth==0)
            {
                var pending=t_pending;t_pending=null;
                // A using scope can unwind an exception. Dispatch every queued callback, isolating faults,
                // without masking the exception that caused the native handler to unwind.
                if(pending is not null)Drain(pending,rethrow:false);
            }
        }
    }

    /// <summary>Runs <paramref name="body"/> under <paramref name="gate"/>; its log callbacks run after the gate is released.</summary>
    internal static void RunGated(object gate, Action body)
    {
        if (t_depth++ == 0) t_pending = new List<Pending>();
        bool completed = false;
        try
        {
            lock (gate) body();
            completed = true;
        }
        finally
        {
            if (--t_depth == 0)
            {
                var pending = t_pending;
                t_pending = null;
                if (pending is not null) Drain(pending, completed);
            }
        }
    }

    /// <summary>Runs <paramref name="dispatch"/> now, or queues it when this thread is inside <see cref="RunGated"/>.</summary>
    internal static void Run(Action dispatch, Action<Exception>? fault = null)
    {
        if (t_pending is { } pending) pending.Add(new Pending(dispatch, fault));
        else dispatch();
    }

    private static void Drain(List<Pending> pending, bool rethrow)
    {
        Exception? unowned = null;
        foreach (var p in pending)
        {
            try { p.Dispatch(); }
            catch (Exception e)
            {
                if (p.Fault is { } fault)
                {
                    try { fault(e); } catch (Exception f) { System.Diagnostics.Trace.TraceError("M1-034 deferred log fault handler threw: " + f); }
                }
                else
                {
                    System.Diagnostics.Trace.TraceError("M1-034 deferred log dispatch threw: " + e);
                    unowned ??= e;
                }
            }
        }
        if (rethrow && unowned is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(unowned).Throw();
    }
}
