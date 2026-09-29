using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-002: the C# window combine against the engine's own 0x00AB5A94, the float <c>mdct_unroll_lap</c>.
///
/// The oracle is libcozmoEngine.so's combine, executed under an ARM emulator for every long/short combination
/// of both shipped block-size pairs (256/2048, 512/1024) and four start/end windows each
/// (<c>re-analysis/tools/emu/emu_combine.py --fixtures</c>). The inputs come from a small deterministic
/// generator that both sides share, so the fixture holds only the engine's outputs. Every output must match
/// bit for bit.
/// </summary>
public class WwiseVorbisCombineNativeTests
{
    private const int Len = 4096;

    private static float[] Lcg(int seed, int count)
    {
        long x = seed;
        var out_ = new float[count];
        for (int i = 0; i < count; i++)
        {
            x = (x * 1103515245 + 12345) % 2147483648;
            out_[i] = (float)(x / 2147483648.0 * 2.0 - 1.0);
        }
        return out_;
    }

    [Fact]
    public void TheCombineMatchesTheEnginesOwnCodeBitForBit()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "combine_native.bin"));
        int pos = 0;
        int Int() { int v = BitConverter.ToInt32(bytes, pos); pos += 4; return v; }
        int count = Int();
        Assert.Equal(32, count);
        for (int c = 0; c < count; c++)
        {
            int bs0 = Int(), bs1 = Int(), prev = Int(), cur = Int(), start = Int(), end = Int(), seed = Int();
            int n = end - start;
            var engine = new float[n];
            Buffer.BlockCopy(bytes, pos, engine, 0, 4 * n);
            pos += 4 * n;

            var output = new float[Len];
            WwiseVorbisNative.Combine(bs0, bs1, prev != 0, cur != 0, Lcg(seed, Len), Lcg(seed + 1000, Len),
                WwiseVorbisNative.WindowTable(bs0), WwiseVorbisNative.WindowTable(bs1), output, start, end);

            for (int i = 0; i < n; i++)
                Assert.True(BitConverter.SingleToInt32Bits(engine[i]) == BitConverter.SingleToInt32Bits(output[i]),
                    $"bs {bs0}/{bs1} prev {prev} cur {cur} start {start} end {end}: output[{i}] engine {engine[i]:R}, C# {output[i]:R}");
        }
        Assert.Equal(bytes.Length, pos);
    }
}
