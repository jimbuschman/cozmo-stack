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

    /// <summary>
    /// The native copy bound (<c>GetIDFromString</c> 0x0099DB84, M6 0.10): at most 0x103 bytes including the
    /// NUL, so at most 0x102 string bytes are hashed. This is a byte-oriented C-string copy; do not add
    /// Unicode or culture semantics.
    /// </summary>
    public const int MaxBytes = 0x103;

    /// <summary>The id Wwise would give this name.</summary>
    public static uint Of(string name)
    {
        uint h = Offset;
        int n = 0;
        foreach (char c in name)
        {
            if (c == '\0') break;                               // the native copy is a C string
            if (n >= MaxBytes - 1) break;                       // at most 0x103 bytes including the NUL
            // Lower-cased ASCII: Wwise names are ASCII, and the hash is over the lower-cased form.
            byte b = (byte)(c is >= 'A' and <= 'Z' ? c + 32 : c);
            h = unchecked(h * Prime);
            h ^= b;
            n++;
        }
        return h;
    }
}
