namespace Cozmo.Transport;

// fidelity: M1-048, M1-047
/// <summary>The shipped process-wide _errG byte, also used by the higher-layer error paths.</summary>
public static class EngineErrorState
{
    private static int _errorFlag;
    public static bool ErrorFlagSet
    {
        get => Volatile.Read(ref _errorFlag) != 0;
        set => Volatile.Write(ref _errorFlag, value ? 1 : 0);
    }

    // _errBreakOnError (0x01051E78) ships initialized to 1.
    internal static bool BreakOnError = true;
    internal static void StoreAndMaybeBreak()
    {
        ErrorFlagSet = true; // _errG at 0x0105DD34, before the conditional call.
        if (BreakOnError) DebugBreakOnError();
    }

    // PLT 0x004A4114 -> 0x0080DAB4: bx lr. The shipped target has no effect.
    private static void DebugBreakOnError() { }
}
