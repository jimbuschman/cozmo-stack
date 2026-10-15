using System.Globalization;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Vision;

public sealed partial class VisionSystem
{
    private readonly object _disposeGate = new();
    // fidelity: M11-055, M11-078
    internal LifetimeProfiler LifetimeProfiler { get; } = new();
}

// Checked Profiler destruction/PrintTimerData input boundary, not a substitute timing producer.
// fidelity: M11-055, M11-078
internal sealed class LifetimeProfiler : IDisposable
{
    internal sealed class Timer(string name)
    {
        internal readonly string Name = name;
        internal long Total, SavedTotal;
        internal int Calls, SavedCalls;
    }
    private readonly List<Timer> _timers = new();
    internal string Channel = "Profiler", Key = "Profiler";
    internal SourceLogSink? SourceLog;
    private bool _disposed;
    // Identity is per timer, not string-value equality: native keys are const char*.
    internal Timer CreateTimer(string name)
    {
        var timer = new Timer(name);
        _timers.Add(timer);
        return timer;
    }
    // 0x006B0EE6..0x006B0F2A: only Init reaching both successful config reads renames it.
    internal void ApplyVisionPerformanceLoggingNames()
    {
        Key = "VisionSystem.Profiler";
        Channel = "VisionSystem";
    }
    internal void Print(Timer timer)
    {
        long total = timer.Total;
        int calls = timer.Calls;
        long delta = unchecked(total - timer.SavedTotal);
        int deltaCalls = unchecked(calls - timer.SavedCalls);
        double average = calls >= 1 ? (double)total / calls : 0.0;
        double deltaAverage = deltaCalls >= 1 ? (double)delta / deltaCalls : 0.0;
        SourceLog?.Invoke(EngineLogLevel.Info, Channel, Key, string.Format(CultureInfo.InvariantCulture,
            "{0} averaged {1:F4}ms over {2} calls ({3:F4}ms over {4} calls since last print)",
            PoseDiagnostics.CString(timer.Name), average, calls, deltaAverage, deltaCalls));
        timer.SavedTotal = total;
        timer.SavedCalls = calls;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Operator M11-047 extension: creation/insertion order, rather than literal-pointer hash order.
        foreach (var timer in _timers) Print(timer);
        _timers.Clear();
    }
}
