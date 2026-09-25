using System.Security.Cryptography;

namespace Cozmo.Robot.Animation;

// fidelity: M5-005, M5-011, M5-017, M5-028, M5-030, M5-031
/// <summary>
/// The engine's <c>Util::RandomGenerator</c> (M5 inventory gap4 R1..R4, gap1 "RNG semantics"): a libc++
/// <c>std::mt19937</c>; <c>SetSeed(0)</c> seeds it from <c>std::random_device("/dev/urandom")</c>, a non-zero seed is used
/// as given (R1). The draws:
/// <list type="bullet">
/// <item><c>GetNextDbl</c> = (d0 + d1·2^32)·2^−64 from two draws (R1), in [0, 1) (1.0 itself with probability ≈ 2^−54);</item>
/// <item><c>RandInt(n)</c> = trunc(u·n); <c>RandIntInRange(a, b)</c> = a + trunc(u·(b − a + 1));</item>
/// <item><c>RandDbl(x)</c> = u·x; <c>RandDblInRange(a, b)</c> = a + u·(b − a) (gap1).</item>
/// </list>
/// sRNG and the context RNG are entropy-seeded (R2, R3; C3: the OS's RandomNumberGenerator stands for /dev/urandom);
/// the ScanlineDistorter's is seeded 1 (R4). Calls are serialised, as one generator may be shared across threads here.
/// </summary>
public sealed class EngineRandom
{
    private readonly Mt19937 _mt;
    private readonly object _gate = new();

    /// <summary>A generator seeded as <c>SetSeed(seed)</c> does: 0 takes an entropy seed (R1).</summary>
    public EngineRandom(uint seed) => _mt = new Mt19937(seed == 0 ? EntropySeed() : seed);

    /// <summary>An entropy-seeded generator (SetSeed(0), R1..R3).</summary>
    public EngineRandom() : this(0) { }

    /// <summary>A test seam: an mt19937 seeded from a caller's <see cref="Random"/>, so a timeline can be reproduced.</summary>
    public EngineRandom(Random seedSource) : this(unchecked((uint)seedSource.Next(1, int.MaxValue))) { }

    /// <summary>std::random_device("/dev/urandom")(): one 32-bit entropy word.</summary>
    private static uint EntropySeed()
    {
        uint s;
        do s = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4));
        while (s == 0);
        return s;
    }

    /// <summary>R1: (d0 + d1·2^32)·2^−64.</summary>
    public double GetNextDbl()
    {
        lock (_gate)
        {
            double d0 = _mt.Next();
            double d1 = _mt.Next();
            return (d0 + d1 * 4294967296.0) * (1.0 / 18446744073709551616.0);
        }
    }

    /// <summary>RandInt(n) = trunc(u·n).</summary>
    public int RandInt(int n) => (int)(GetNextDbl() * n);

    /// <summary>RandIntInRange(a, b) = a + trunc(u·(b − a + 1)).</summary>
    public int RandIntInRange(int a, int b) => a + (int)(GetNextDbl() * ((double)b - a + 1));

    /// <summary>RandDbl(x) = u·x.</summary>
    public double RandDbl(double x) => GetNextDbl() * x;

    /// <summary>RandDblInRange(a, b) = a + u·(b − a).</summary>
    public double RandDblInRange(double a, double b) => a + GetNextDbl() * (b - a);
}

/// <summary>std::mt19937 (the standard 32-bit Mersenne Twister, default seed 5489).</summary>
public sealed class Mt19937
{
    private const int N = 624, M = 397;
    private readonly uint[] _mt = new uint[N];
    private int _i;

    public Mt19937(uint seed = 5489u)
    {
        _mt[0] = seed;
        for (int i = 1; i < N; i++) _mt[i] = unchecked(1812433253u * (_mt[i - 1] ^ (_mt[i - 1] >> 30)) + (uint)i);
        _i = N;
    }

    public uint Next()
    {
        if (_i >= N)
        {
            for (int k = 0; k < N; k++)
            {
                uint y = (_mt[k] & 0x80000000u) | (_mt[(k + 1) % N] & 0x7FFFFFFFu);
                _mt[k] = _mt[(k + M) % N] ^ (y >> 1) ^ ((y & 1u) != 0 ? 0x9908B0DFu : 0u);
            }
            _i = 0;
        }
        uint z = _mt[_i++];
        z ^= z >> 11;
        z ^= (z << 7) & 0x9D2C5680u;
        z ^= (z << 15) & 0xEFC60000u;
        z ^= z >> 18;
        return z;
    }
}
