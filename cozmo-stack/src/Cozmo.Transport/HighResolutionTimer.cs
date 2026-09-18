using System.Runtime.InteropServices;

namespace Cozmo.Transport;

/// <summary>
/// Raises the system timer resolution to 1 ms for as long as it is held.
///
/// Windows schedules sleeps on a 15.6 ms tick by default. The engine's reliable transport updates every 2 ms
/// and audio frames are due every 33 ms, so without this a sleep of either length overshoots by up to half a
/// frame and the engine's timing semantics are lost: resends fire late, the packet separation interval is
/// meaningless, and streamed audio stutters. Does nothing off Windows, where sleeps are already fine-grained.
///
/// Nesting is safe: Windows reference-counts the requests, and each instance ends exactly the one it began.
/// </summary>
public readonly struct HighResolutionTimer : IDisposable
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint BeginPeriod(uint ms);
    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint EndPeriod(uint ms);

    private readonly bool _raised;

    public HighResolutionTimer()
    {
        _raised = false;
        if (!OperatingSystem.IsWindows()) return;
        try { _raised = BeginPeriod(1) == 0; }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    /// <summary>True when the resolution was actually raised, i.e. on Windows and the call succeeded.</summary>
    public bool Raised => _raised;

    public void Dispose()
    {
        if (!_raised) return;
        try { EndPeriod(1); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }
}
