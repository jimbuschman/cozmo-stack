namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The hash Wwise uses to turn a name into an id.
///
/// Everything in a Wwise bank is identified by a 32-bit number rather than a name: banks, events, switch
/// groups, switch states, game parameters. The mapping is FNV-1 (not FNV-1a) over the **lower-cased** name,
/// and it is exact — there is no table to look it up in.
///
/// Verified against this build's own six banks, where the hash of each bank's name reproduces the id in
/// its BKHD chunk:
///
/// | name | hash | bank id |
/// | --- | --- | --- |
/// | Music | 3991942870 | 3991942870 |
/// | SFX | 393239870 | 393239870 |
/// | Cozmo | 2386142475 | 2386142475 |
/// | UI | 1551306167 | 1551306167 |
/// | Init | 1355168291 | 1355168291 |
/// | Dev_Debug | 1453038702 | 1453038702 |
///
/// Six of six, with no near misses. That matters because it means names appearing in shipped configuration
/// — the <c>audioSwitchGroup</c> and <c>audioSwitch</c> the 39 Singing behaviours carry, for instance — can
/// be resolved to the ids the banks use, without any name table being shipped.
/// </summary>
public static class WwiseHash
{
    private const uint Offset = 2166136261;
    private const uint Prime = 16777619;

    /// <summary>The id Wwise would give this name.</summary>
    public static uint Of(string name)
    {
        uint h = Offset;
        foreach (char c in name)
        {
            // Lower-cased ASCII: Wwise names are ASCII, and the hash is over the lower-cased form.
            byte b = (byte)(c is >= 'A' and <= 'Z' ? c + 32 : c);
            h = unchecked(h * Prime);
            h ^= b;
        }
        return h;
    }
}
