using Cozmo.Transport;

namespace Cozmo.Protocol.Tests;

/// <summary>Waits for completed transport work or a queued timer copy, without polling the host clock.</summary>
internal sealed class TransportActivity : IDisposable
{
    private readonly ReliableTransport _transport;
    private readonly AutoResetEvent _changed = new(false);
    private readonly object _gate = new();
    private bool _disposed;
    internal TransportActivity(ReliableTransport transport)
    {
        _transport = transport;
        transport.ExecutorCompleted += Notify;
        if (transport.Scheduler is { } scheduler) scheduler.CopyPosted += Notify;
    }
    private void Notify()
    {
        lock (_gate) if (!_disposed) _changed.Set();
    }
    internal bool Until(Func<bool> condition)
    {
        while (!condition()) _changed.WaitOne();
        return true;
    }
    public void Dispose()
    {
        _transport.ExecutorCompleted -= Notify;
        if (_transport.Scheduler is { } scheduler) scheduler.CopyPosted -= Notify;
        lock (_gate) { _disposed = true; _changed.Dispose(); }
    }
    internal static bool Wait(ReliableTransport transport, Func<bool> condition)
    {
        using var activity = new TransportActivity(transport);
        return activity.Until(condition);
    }
}
