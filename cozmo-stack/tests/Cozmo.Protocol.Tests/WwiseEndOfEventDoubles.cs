using Cozmo.Robot.Animation.Wwise;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Test doubles for the playing-id entry's unread bodies (M6-026 R4.1). The engine's EndOfEvent body 0xA03618 reads [item+0x20]/[item+0x24] (written by 0xA03108, C40.1: the real PostEvent writes them) and calls 0xA0C238, 0xA1C660 and 0xA1C65C
/// (unread). The doubles make no source claim: the lookup finds no game object, and the callees do nothing. A Post with no game object id leaves [item+0x24] unwritten (C40.1) and EndOfEvent then throws, so a test that completes an event passes a game object id. Tests that assert the throw build the runtime directly.
/// </summary>
internal static class WwiseEndOfEventDoubles
{
    /// <summary>Explicit test game object id for a PostEvent that should complete (the engine's value for "no game object" is not in any adopted row, so it is a test input, not a default).</summary>
    public const uint DoubleGameObjectForNull = 0xD0B1E001;

    public static WwiseEventRuntime Runtime(IEnumerable<WwiseBank> banks, WwiseRng rng)
    {
        var runtime = new WwiseEventRuntime(banks, rng);
        Install(runtime);
        return runtime;
    }

    public static void Install(WwiseEventRuntime runtime)
    {
        var s = runtime.PlayingIds.Seams;
        s.GameObjectLookupA0C238 = _ => null;
        s.A1C660 = _ => { };
        s.A1C65C = _ => { };
    }
}
