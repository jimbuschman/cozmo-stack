using System.Security.Cryptography;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-002: the whole C# Vorbis decode of shipped Cozmo sounds against the engine's own decoder.
///
/// The oracle is libcozmoEngine.so's complete decode path (setup 0x00AB6380/0x00AB63E0, state 0x00AB3264,
/// the frame loop 0x00AB7E40 with the packet entry, inverse, IMDCT and window combine it calls) executed under
/// an ARM emulator (<c>re-analysis/tools/emu/emu_vorbis.py --fixtures</c>). The fixture holds the SHA-256 of
/// each engine output, mono and stereo, for both shipped block-size pairs (256/2048 and 512/1024); every
/// sample must match bit for bit. The media come from the OBB, so the test does nothing where it is not
/// unpacked.
/// </summary>
public class WwiseVorbisWholeDecodeNativeTests
{
    [Fact]
    public void ShippedSoundsDecodeBitForBitLikeTheEngine()
    {
        if (WwiseAssets.Library is not { } lib) return;                   // no OBB on this machine
        var codebooks = WwiseAudioSource.TryLoadCodebooks();
        Assert.NotNull(codebooks);

        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "vorbis_native.txt"))
            .Where(l => l.Length > 0 && l[0] != '#').ToArray();
        Assert.True(lines.Length >= 8);
        var seen = new HashSet<(int Channels, int Short, int Long)>();
        foreach (var line in lines)
        {
            var f = line.Split(' ');
            uint id = uint.Parse(f[0]);
            int channels = int.Parse(f[1]), floats = int.Parse(f[2]);
            var bytes = lib.ReadMedia(id, out _);
            Assert.NotNull(bytes);
            var media = WwiseMedia.Parse(bytes!);
            Assert.Equal(channels, media.Channels);
            seen.Add((channels, 1 << media.Vorbis!.BlockSize0Pow, 1 << media.Vorbis.BlockSize1Pow));

            var pcm = WwiseVorbisNative.Decode(media, codebooks!);
            Assert.Equal(floats, pcm.Length);
            var raw = new byte[4 * pcm.Length];
            Buffer.BlockCopy(pcm, 0, raw, 0, raw.Length);
            Assert.True(Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant() == f[3],
                $"{id}.wem: the C# decode differs from the engine's (localise it with emu_vorbis.py --spectrum/--memo)");
        }
        Assert.Contains((1, 256, 2048), seen);
        Assert.Contains((2, 256, 2048), seen);
        Assert.Contains((1, 512, 1024), seen);
        Assert.Contains((2, 512, 1024), seen);
    }
}
