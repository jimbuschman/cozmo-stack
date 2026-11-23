using System.Diagnostics;
using System.Globalization;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Vision;

public sealed partial class VisionSystem
{
    private readonly object _disposeGate = new();
    // fidelity: M11-055, M11-078
    internal LifetimeProfiler LifetimeProfiler { get; private set; }

    private LifetimeProfiler CreateLifetimeProfiler() => new()
    {
        SourceLog = (level, channel, key, text) => _robot.Engine.EmitSourceLog(level, channel, key, text),
        DasTime = (key, value, count) => _robot.Engine.EmitSourceLog(EngineLogLevel.Event, null, key, value,
            new Dictionary<string, string> { ["$data"] = count }),
    };

    // VisionComponent owns and destroys VisionSystem 0x00652580..0x0065259C.
    // Exchange ownership under _groundGate; teardown logs run after releasing that gate.
    private LifetimeProfiler ReplaceLifetimeProfiler()
    {
        var retired = LifetimeProfiler;
        LifetimeProfiler = CreateLifetimeProfiler();
        return retired;
    }
}

// Profiler diagnostics use monotonic time; their counters do not feed vision results.
// fidelity: M11-055, M11-078
internal sealed class LifetimeProfiler : IDisposable
{
    internal sealed class Timer(string name)
    {
        internal readonly string Name = name;
        internal long Total, SavedTotal, SavedDasTotal, LastInfo, LastDas;
        internal int Calls, SavedCalls, SavedDasCalls;
    }
    private readonly List<Timer> _timers = new();
    private readonly object _gate = new();
    private Timer? _decodeJpeg;
    private long _infoPeriodMs = -1, _dasPeriodMs = -1;
    internal string Channel = "Profiler", Key = "Profiler";
    internal SourceLogSink? SourceLog;
    internal Action<string, string, string>? DasTime;
    private bool _disposed;
    // Identity is per timer, not string-value equality: native keys are const char*.
    internal Timer CreateTimer(string name)
    {
        var timer = new Timer(name);
        lock (_gate) _timers.Add(timer);
        return timer;
    }
    // 0x006B0EE6..0x006B0F2A: only Init reaching both successful config reads renames it.
    internal void ApplyVisionPerformanceLoggingNames()
    {
        Key = "VisionSystem.Profiler";
        Channel = "VisionSystem";
    }
    internal void ConfigureVisionPeriods(long infoPeriodMs, long dasPeriodMs)
    {
        lock (_gate)
        {
            if (_disposed) return;
            ApplyVisionPerformanceLoggingNames();
            _infoPeriodMs = infoPeriodMs;
            _dasPeriodMs = dasPeriodMs;
        }
    }

    // 0x0087D052..0x0087D062 takes steady-clock time before finding/creating the timer.
    internal long TicDecodeJpeg()
    {
        long start = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            if (!_disposed) _decodeJpeg ??= CreateTimer("DecodeJPEG");
        }
        return start;
    }

    internal void TocDecodeJpeg(long start)
    {
        LogCallbackDeferral.RunGated(_gate, () =>
        {
            if (_disposed || _decodeJpeg is not { } timer) return;
            // 0x0087D268..0x0087D2A4 truncates elapsed nanoseconds to integral milliseconds.
            timer.Total = unchecked(timer.Total + ElapsedMilliseconds(start, Stopwatch.GetTimestamp()));
            timer.Calls = unchecked(timer.Calls + 1);
            long now = Stopwatch.GetTimestamp();
            // 0x0087D2C6..0x0087D2EC and 0x0087D30E..0x0087D32E: strict period comparisons.
            if (_infoPeriodMs >= 0 && ElapsedMilliseconds(timer.LastInfo, now) > _infoPeriodMs)
            {
                timer.LastInfo = now;
                Print(timer);
            }
            if (_dasPeriodMs >= 0 && ElapsedMilliseconds(timer.LastDas, now) > _dasPeriodMs)
            {
                timer.LastDas = now;
                LogDasTimes(timer);
            }
        });
    }

    private static long ElapsedMilliseconds(long start, long end)
        => (long)(((Int128)end - start) * 1000 / Stopwatch.Frequency);

    // 0x0087D488..0x0087D6EA: totals in event text, call counts in $data, separate DAS baseline.
    private void LogDasTimes(Timer timer)
    {
        // 0x004DD788..0x004DD7AE initializes the prefix used at 0x0087D4BC.
        string name = $"robot.vision.profiler.{Key}.{timer.Name}";
        DasTime?.Invoke(name + ".OverallTime_ms", timer.Total.ToString(CultureInfo.InvariantCulture),
            timer.Calls.ToString(CultureInfo.InvariantCulture));
        DasTime?.Invoke(name + ".RecentTime_ms", unchecked(timer.Total - timer.SavedDasTotal).ToString(CultureInfo.InvariantCulture),
            unchecked(timer.Calls - timer.SavedDasCalls).ToString(CultureInfo.InvariantCulture));
        timer.SavedDasTotal = timer.Total;
        timer.SavedDasCalls = timer.Calls;
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
        Timer[] timers;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            timers = _timers.ToArray();
            _timers.Clear();
        }
        // Operator M11-047 extension: print in timer insertion order.
        foreach (var timer in timers) Print(timer);
    }
}
