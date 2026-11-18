namespace Cozmo.Robot;

// Test instrumentation only. Capture the owner when an instance is created, so a fault on
// a native/background thread is still charged to its creating test. Unset in production.
internal static class IsolatedFaultProbe
{
    internal static Func<Action<Exception>>? Capture;
}
