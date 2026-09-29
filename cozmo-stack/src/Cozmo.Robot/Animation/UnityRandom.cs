namespace Cozmo.Robot.Animation;

// fidelity: M5-020
/// <summary>
/// UnityEngine.Random, as it ships in libunity.so (R-ANIM pre-extraction part 1 item 11, R3..R9).
///
/// One global xorshift128 state of four u32 words, shared by every draw and not locked (R6). The generator
/// (R4) is
/// <code>t = x ^ (x&lt;&lt;11); t ^= t&gt;&gt;8; t ^= w; new_w = t ^ (w&gt;&gt;19);</code>
/// then <c>(x,y,z,w) := (y,z,w,new_w)</c>, and the output is <c>new_w</c>. <c>RandomRangeInt(min, max)</c> (R5)
/// is <c>min + (out mod (max-min))</c> for <c>min &lt; max</c> and <c>min - (out mod (min-max))</c> for
/// <c>max &lt; min</c>; <c>min == max</c> returns min and does not advance the state. max is exclusive, so
/// <c>Range(1, 34)</c> is <c>1 + out % 33</c> and <c>Range(1, 14)</c> is <c>1 + out % 13</c>.
///
/// <b>Seed.</b> The native start-up seed is <c>time(NULL)</c> (R8), and <c>StartupManager.TryLoadMainScene</c>
/// re-seeds the one global stream with <c>Environment.TickCount</c> after the main scene loads, so Code Lab
/// runs under that seed (R9). Mono's <c>Environment.get_TickCount</c> in libmono.so was not read, so the value's
/// base (uptime or process time) is UNKNOWN; this stack uses the host's <see cref="Environment.TickCount"/> as
/// the forced stand-in (COMPATIBILITY_POLICY, SD2 time-seeded randomness). The generator and the range rule
/// are exact; only the seed value is a stand-in.
/// </summary>
public sealed class UnityRandom
{
    /// <summary>The LCG multiplier in InitState/set_seed (R7).</summary>
    private const uint Multiplier = 0x6C078965u;

    private uint _x, _y, _z, _w;

    /// <summary>The app's one global stream (R6).</summary>
    public static UnityRandom Shared { get; } = new UnityRandom();

    /// <summary>Seeds the stream from the host's tick count, standing in for the app's startup re-seed (R9).</summary>
    public UnityRandom() => InitState(unchecked((uint)Environment.TickCount));

    /// <summary>
    /// <c>InitState(seed)</c> / <c>set_seed</c> (R7): <c>s0 = seed</c>, then
    /// <c>s1 = s0·0x6C078965 + 1</c>, <c>s2 = s1·0x6C078965 + 1</c>, <c>s3 = s2·0x6C078965 + 1</c>.
    /// </summary>
    public void InitState(uint seed)
    {
        _x = seed;
        _y = unchecked(_x * Multiplier + 1u);
        _z = unchecked(_y * Multiplier + 1u);
        _w = unchecked(_z * Multiplier + 1u);
    }

    /// <summary><c>RandomRangeInt</c>'s generator (R4): xorshift128, the output is the new fourth word.</summary>
    public uint NextUInt()
    {
        uint t = _x ^ (_x << 11);
        t ^= t >> 8;
        t ^= _w;
        uint next = t ^ (_w >> 19);
        _x = _y;
        _y = _z;
        _z = _w;
        _w = next;
        return next;
    }

    /// <summary><c>RandomRangeInt(min, max)</c> (R5); <paramref name="maxExclusive"/> is exclusive.</summary>
    public int Range(int minInclusive, int maxExclusive)
    {
        if (minInclusive == maxExclusive) return minInclusive;
        uint draw = NextUInt();
        return minInclusive < maxExclusive
            ? minInclusive + (int)(draw % (uint)(maxExclusive - minInclusive))
            : minInclusive - (int)(draw % (uint)(minInclusive - maxExclusive));
    }
}