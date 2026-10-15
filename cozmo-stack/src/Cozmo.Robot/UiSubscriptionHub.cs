namespace Cozmo.Robot;

// fidelity: M5-055, M5-056
// UI +0x38 G2E and +0x20 E2G use the same checked list/token algorithm.
// Managed storage replaces allocations and refcount frees. The host async action adapter
// shares this topology gate with every writer; callbacks execute outside it.
internal sealed class UiSubscriptionHub : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<ushort, Signal> _g2e = new(), _e2g = new();
    private bool _disposed;
    internal Action<string>? ReleaseObserved;

    private sealed class Token
    {
        internal int Owners = 1; // signal's owner; zero means expired.
        internal bool Lock() { if (Owners == 0) return false; ++Owners; return true; }
        internal void Release() => --Owners;
    }
    private sealed class Signal
    {
        internal readonly Token Lifetime = new();
        internal readonly LinkedList<Action<object?>?> Nodes = new();
        internal void Destroy()
        {
            while (Nodes.First is { } node) { node.Value = null; Nodes.Remove(node); }
            Lifetime.Release(); // 0x006634B8..0x006634E4: nodes before token.
        }
    }

    internal RetainedSubscription Subscribe(ushort tag, bool engineToGame, Action? callback = null)
        => SubscribeMessage(tag, engineToGame, callback is null ? null : _ => callback());

    internal RetainedSubscription SubscribeMessage(ushort tag, bool engineToGame, Action<object?>? callback)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var map = engineToGame ? _e2g : _g2e;
            if (!map.TryGetValue(tag, out var signal)) map.Add(tag, signal = new Signal());
            var node = signal.Nodes.AddLast(callback); // factory inserts before sentinel.
            var weak = new WeakReference<Token>(signal.Lifetime);
            // The signal address is borrowed. Keeping managed storage reachable does not keep
            // its native lifetime token alive: Destroy explicitly retires the signal's owner.
            return new RetainedSubscription(() =>
            {
                lock (_gate)
                {
                    if (!weak.TryGetTarget(out var token) || !token.Lock()) return;
                    token.Release(); // 0x00664710..0x00664718: drop temporary lock BEFORE signal traversal.
                    ReleaseObserved?.Invoke("token released");
                    for (var cursor = signal.Nodes.First; cursor is not null; cursor = cursor.Next)
                    {
                        if (!ReferenceEquals(cursor, node)) continue;
                        cursor.Value = null; // callback destruction precedes neighbor patching.
                        signal.Nodes.Remove(cursor); // 0x006634F4..0x0066352A, then node refcount/free.
                        ReleaseObserved?.Invoke($"unlink {(engineToGame ? "E2G" : "G2E")} {tag:X2}");
                        break;
                    }
                }
            });
        }
    }

    internal int Count(ushort tag, bool engineToGame)
    {
        lock (_gate) return (engineToGame ? _e2g : _g2e).GetValueOrDefault(tag)?.Nodes.Count ?? 0;
    }

    internal void Deliver(ushort tag, bool engineToGame, object? payload = null)
    {
        var snapshot = new List<LinkedListNode<Action<object?>?>>();
        lock (_gate)
        {
            if (!(engineToGame ? _e2g : _g2e).TryGetValue(tag, out var signal)) return;
            for (var node = signal.Nodes.First; node is not null; node = node.Next) snapshot.Add(node);
        }
        foreach (var node in snapshot)
        {
            Action<object?>? callback;
            lock (_gate) callback = node.Value;
            callback?.Invoke(payload);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var signal in _g2e.Values) signal.Destroy();
            _g2e.Clear();
            foreach (var signal in _e2g.Values) signal.Destroy();
            _e2g.Clear();
        }
    }
}
