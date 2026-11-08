using System.Buffers.Binary;
using System.Security.Cryptography;
using Cozmo.Robot.Emulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class NativeWwiseVoiceHostTests
{
    [Fact]
    public void NameSwitchAndPitchMatchIndependentNativeReference()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        using var host = new NativeWwiseVoiceHost(lib, GraphRig.ReadBank(meta, "Init.bnk"),
            GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), 1024);
        float scale = BitConverter.UInt32BitsToSingle(0x38000100);
        Assert.Throws<InvalidOperationException>(() => host.ConfigureVoice(0x2F8B3BF4, 0.1f));
        host.Offer(AcapelaTtsTests.ProbeSamples().Select(s => s * scale).ToArray(), 22050);
        host.ConfigureVoice(0x2F8B3BF4, 0.1f);
        Assert.Throws<InvalidOperationException>(() => host.ConfigureVoice(0x2F8B3BF4, 0.1f));
        host.PostOffered(applyDefaultVolume: true);
        for (int i = 0; i < 195; i++) host.Step(21334, consumeOutput: true);
        Assert.Equal(98, host.Frames.Count);
        Assert.Equal("52155a7f13a96f2d372595f01825d6266ce1a35f8f47bf114569f80d90198344", Hash(host.Frames));
        Assert.NotNull(host.EndOfEvent);
        Assert.True(host.HijackEnded);
    }

    [Fact]
    public void CheckedOpenSlSinkMatchesIndependentNativeVoiceReferenceAndTerminates()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        using var host = new NativeWwiseVoiceHost(lib, GraphRig.ReadBank(meta, "Init.bnk"),
            GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk"), 1024);
        float scale = BitConverter.UInt32BitsToSingle(0x38000100);
        float[] samples = AcapelaTtsTests.ProbeSamples().Select(sample => sample * scale).ToArray();
        Assert.Equal(1u, host.Post(samples, 22050));
        int consumed = 0;
        int framesAtEnd = -1;
        for (int tick = 0; tick < 195; tick++)
        {
            byte[]? block = host.Step(21334, consumeOutput: true);
            if (block is not null) { Assert.Equal(4096, block.Length); consumed++; }
            if (framesAtEnd < 0 && host.EndOfEvent is not null) framesAtEnd = host.Frames.Count;
        }
        Assert.Equal(195, consumed);
        Assert.Equal(100, host.Frames.Count);
        Assert.All(host.Frames, frame => Assert.Equal(744, frame.Length));
        Assert.Equal((0xC0FFEEu, 7u, 1u, 0xBF0BACE1u), host.EndOfEvent);
        Assert.InRange(framesAtEnd, 0, 99); // a later Hijack frame follows EndOfEvent
        Assert.True(host.HijackEnded);
        Assert.Equal("6e1ca4d3a3b57cf96505d5d83025f754f51c392e111a1ec17534b34f8c01cfe9", Hash(host.Frames));
        host.Dispose();
        Assert.True(host.TerminatedCleanly);
        Assert.Null(host.ShutdownDiagnostic);
        Assert.Throws<ObjectDisposedException>(() => host.Step(1, false));
    }

    [Fact]
    public void StopCompletesAndAbruptReleaseIsVisible()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        string meta = GraphRig.FindMeta() ?? throw new InvalidOperationException("shipped bank metadata missing");
        byte[] init = GraphRig.ReadBank(meta, "Init.bnk"), english = GraphRig.ReadBank(meta, "English(US)/Cozmo.bnk");
        float scale = BitConverter.UInt32BitsToSingle(0x38000100);
        float[] samples = AcapelaTtsTests.ProbeSamples().Select(sample => sample * scale).ToArray();
        using (var host = new NativeWwiseVoiceHost(lib, init, english, 1024))
        {
            host.Post(samples, 22050);
            for (int tick = 0; tick < 60; tick++)
            {
                if (tick == 24) host.Stop();
                host.Step(21334, consumeOutput: true);
            }
            Assert.NotNull(host.EndOfEvent);
            Assert.True(host.HijackEnded);
            Assert.Equal(16, host.Frames.Count);
            Assert.Equal("748ec3acaae0a1c3d23543efa27e1fb567cfafd8873bf3ca1fcbfe21e510e652", Hash(host.Frames));
            host.Dispose();
            Assert.True(host.TerminatedCleanly);
        }
        var abrupt = new NativeWwiseVoiceHost(lib, init, english, 1024);
        abrupt.Post(samples, 22050);
        abrupt.Dispose();
        Assert.False(abrupt.TerminatedCleanly);
        Assert.Contains("MISSING", abrupt.ShutdownDiagnostic);
        Assert.Throws<ObjectDisposedException>(() => abrupt.Step(1, false));
    }

    private static string Hash(IReadOnlyList<float[]> frames)
    {
        byte[] bytes = new byte[frames.Sum(frame => frame.Length) * 4];
        int offset = 0;
        foreach (float[] frame in frames)
            foreach (float sample in frame)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), BitConverter.SingleToUInt32Bits(sample));
                offset += 4;
            }
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
