using System.Diagnostics;

namespace Cozmo.Robot.Emulation;

/// <summary>Opt-in, read-only host profiling. Never supplies samples or changes native scheduling.</summary>
internal static class SpeechTiming
{
    internal static volatile Action<string, long, short[]?>? Observer;
    internal static void Mark(string phase, short[]? samples = null)
    {
        var observer = Observer;
        if (observer is null) return;
        try { observer(phase, Stopwatch.GetTimestamp(), samples); }
        catch { /* A diagnostic subscriber cannot interrupt speech. */ }
    }
}
