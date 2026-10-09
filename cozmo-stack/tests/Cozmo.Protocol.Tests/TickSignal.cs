using Cozmo.Robot;
using Cozmo.Robot.Animation;

namespace Cozmo.Protocol.Tests;

// Observe a completed update. Predicates are checked only after the producer signals progress.
internal sealed class TickSignal : IDisposable
{
    private readonly AutoResetEvent _changed = new(false);
    private readonly object _gate = new();
    private bool _disposed;
    private readonly Action<Action> _unsubscribe;
    public TickSignal(CozmoEngine engine)
    {
        engine.TickCompleted += Signal;
        _unsubscribe = a => engine.TickCompleted -= a;
    }
    public TickSignal(CozmoAnimations animations)
    {
        animations.TickObserved += Signal;
        _unsubscribe = a => animations.TickObserved -= a;
    }
    private void Signal() { lock (_gate) if (!_disposed) _changed.Set(); }
    public void Until(Func<bool> condition)
    {
        while (!condition()) _changed.WaitOne();
    }
    public void Next(int count = 1)
    {
        for (int i = 0; i < count; i++) _changed.WaitOne();
    }
    public void Dispose()
    {
        _unsubscribe(Signal);
        lock (_gate) { _disposed = true; _changed.Dispose(); }
    }
}
