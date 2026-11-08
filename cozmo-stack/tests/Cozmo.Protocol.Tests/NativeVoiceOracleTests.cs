using Cozmo.Robot.Emulation;
using System.Security.Cryptography;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class NativeVoiceOracleTests
{
    [Fact]
    public void EnglishSentenceRendersNativeHijackFrames()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        using var oracle = new NativeVoiceOracle(lib, GraphRig.ReadBank(meta, "Init.bnk"), GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"));
        var samples = AcapelaTtsTests.ProbeSamples().Select(s => s * BitConverter.UInt32BitsToSingle(0x38000100)).ToArray();
        uint playing = oracle.Post(samples, 22050);
        Assert.Equal(1u, playing);
        for (int i = 0; i < 185; i++) oracle.Render(21334);
        Assert.Equal(92, oracle.Frames.Count);
        Assert.All(oracle.Frames, frame => Assert.Equal(744, frame.Length));
        Assert.Equal((0xC0FFEEu, 7u, playing, 0xBF0BACE1u), oracle.EndOfEvent);
        byte[] bytes = new byte[oracle.Frames.Count * 744 * 4];
        int offset = 0;
        foreach (var frame in oracle.Frames)
            foreach (float sample in frame)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), BitConverter.SingleToUInt32Bits(sample));
                offset += 4;
            }
        Assert.Equal("a0a2b7665c88de12899af203a4c754d9e3b7c619483995ea871e0600c4c60533", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }
}
