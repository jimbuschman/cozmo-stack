using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-002: the C# transliteration of the engine's Vorbis inverse MDCT, <c>mdct_backward</c> (0x00AB4E34),
/// against the engine's own code.
///
/// The oracle is not this stack's code and not a reference decoder: it is libcozmoEngine.so's mdct_backward,
/// executed under an ARM emulator on a fixed input (<c>re-analysis/tools/emu/emu_imdct.py --fixtures</c>).
/// Every output must match bit for bit. The sizes are every block size the shipped media declare: all 3,910
/// shipped Vorbis sounds use 256/2048 or 512/1024.
/// </summary>
public class WwiseVorbisImdctNativeTests
{
    [Theory]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    public void TheImdctMatchesTheEnginesOwnCodeBitForBit(int n)
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"imdct_native_{n}.bin"));
        Assert.Equal(n, BitConverter.ToInt32(bytes, 0));
        var input = new float[n];
        var engine = new float[n];
        Buffer.BlockCopy(bytes, 4, input, 0, 4 * n);
        Buffer.BlockCopy(bytes, 4 + 4 * n, engine, 0, 4 * n);

        var ours = (float[])input.Clone();
        WwiseVorbisNative.ImdctBackward(ours, n);

        for (int i = 0; i < n; i++)
            Assert.True(BitConverter.SingleToInt32Bits(engine[i]) == BitConverter.SingleToInt32Bits(ours[i]),
                $"n={n}, output[{i}]: engine {engine[i]:R} (0x{BitConverter.SingleToInt32Bits(engine[i]):X8}), " +
                $"C# {ours[i]:R} (0x{BitConverter.SingleToInt32Bits(ours[i]):X8})");
    }
}
