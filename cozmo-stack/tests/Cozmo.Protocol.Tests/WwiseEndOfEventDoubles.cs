using Cozmo.Robot.Animation.Wwise;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Test doubles for the playing-id entry's unread bodies (M6-026 R4.1). The engine's EndOfEvent body 0xA03618 reads [item+0x20]/[item+0x24] (no adopted writer) and calls 0xA0C238, 0xA1C660 and 0xA1C65C
/// (unread). The doubles make no source claim: the writer records the PostEvent arguments, the lookup finds no game object, and the callees do nothing. Tests that assert the throw build the runtime directly.
/// </summary>
internal static class WwiseEndOfEventDoubles
{
    /// <summary>Explicit test value for a PostEvent without a game object (the engine's value for it is not in any adopted row, so it is a double, not a default).</summary>
    public const uint DoubleGameObjectForNull = 0xD0B1E001;

    public static WwiseEventRuntime Runtime(IEnumerable<WwiseBank> banks, WwiseRng rng)
    {
        var runtime = new WwiseEventRuntime(banks, rng);
        Install(runtime);
        return runtime;
    }

    public static void Install(WwiseEventRuntime runtime)
    {
        runtime.PlayingItemFieldsWriter = (item, eventId, gameObject) => { item.EventId20 = eventId; item.GameObject24 = gameObject ?? DoubleGameObjectForNull; };
        var s = runtime.PlayingIds.Seams;
        s.GameObjectLookupA0C238 = _ => null;
        s.A1C660 = _ => { };
        s.A1C65C = _ => { };
    }
}
