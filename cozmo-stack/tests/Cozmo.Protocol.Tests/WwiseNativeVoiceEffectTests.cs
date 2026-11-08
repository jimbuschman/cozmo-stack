using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Emulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class WwiseNativeVoiceEffectTests
{
    private sealed class Source : IWwiseVoiceSource
    {
        public int Channels => 1;
        public int SampleRate => 48000;
        public bool StartStreamSucceeded { get; set; } = true;
        public int Render(WwiseVoiceBuffer buffer) => 0x2D;
        public int StartStream(uint a, uint b) => 1;
    }

    [Theory]
    [InlineData(WwiseNativeVoiceEffect.ExpanderId)]
    [InlineData(WwiseNativeVoiceEffect.HarmonizerId)]
    public void UninitializedEffectCanBeReleasedByTheVoiceWrapper(uint id)
    {
        string? libDir = ShippedLibraries.FindLibDir();
        if (libDir is null) return;
        var allocator = new WwisePluginAllocator();
        using var effect = new WwiseNativeVoiceEffect(libDir, id, allocator);
        Assert.Equal(1, effect.Term(allocator));
        Assert.Equal(allocator.Sizes.Count, allocator.Freed.Count);
    }

    [Fact]
    public void ExpanderCanBeInstalledExecutedAndTornDownThroughVoiceSlot()
    {
        string? libDir = ShippedLibraries.FindLibDir();
        if (libDir is null) return;
        byte[] block = Convert.FromHexString("00003cc26666d6409eef273de17a943e000000000000");
        var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 7, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, false);
        var fx = new WwiseVoiceFxDescriptor { Id = WwiseNativeVoiceEffect.ExpanderId, CloneParams = _ => block.ToArray() };
        var voice = new WwiseLiveVoice(1, 256)
        {
            Source = new Source(),
            BusOwner8 = pbi,
            StartResampler5321C = () => true,
            ResolveNodeFx9EEF2C = i => { if (i != 0) return (null, (byte)0); fx.AddRef(); return (fx, (byte)0); },
            GainNode380Vt24 = _ => { },
            PluginRegistry9CC2AC = (id, alloc) => id == fx.Id ? new WwiseNativeVoiceEffect(libDir, id, alloc) : null,
        };
        Assert.Equal(1, WwiseVoiceBusPass.StartStreamAndBuildInsertFx(voice));
        var slot = Assert.IsType<WwiseVoiceInsertFxSlot>(voice.InsertFxSlots[0]);
        var data = Samples(7);
        var buffer = new WwiseVoiceBuffer(1, 256);
        buffer.State.Data = data;
        buffer.State.ChannelConfig = 0x4101;
        buffer.State.MaxFrames = buffer.State.ValidFrames = 256;
        buffer.State.Code28 = 0x2D;
        slot.Execute3C(buffer);
        Assert.Equal("e444bc615cfbc3b6", Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(data.AsSpan()))).ToLowerInvariant()[..16]);
        Assert.Equal(0x2D, buffer.State.Code28);
        slot.Teardown();
        Assert.Null(slot.Plugin);
    }

    // Independent oracle: emu_fxlib FxEngine, the native effect lifetime at 48000 Hz,
    // mono 0x4101, 256 valid frames per call. Inputs use emu_comp's XS generator.
    [Theory]
    [InlineData(WwiseNativeVoiceEffect.ExpanderId, "00003cc26666d6409eef273de17a943e000000000000", "e444bc615cfbc3b6", "2d96b626ede4e823", "2ca508e58fcab4d6", "44ab214e321c7f4d", "cb976e9d52451cdf")]
    [InlineData(WwiseNativeVoiceEffect.HarmonizerId, "0100007a440000000004000000000000000000fa450000803f0100007a44000080c0050000000000000000c050440000803f000000000000c0c200000000000400000000", "a4c0c0c1932bb548", "2343181d1d392674", "a18822dc4dd68ab9", "8605a4d793cad810", "e77653f201103d79")]
    public void RealEngineMatchesIndependentNativeOracle(uint id, string block, params string[] expected)
    {
        string? libDir = ShippedLibraries.FindLibDir();
        if (libDir is null) return;
        var allocator = new WwisePluginAllocator();
        using var effect = new WwiseNativeVoiceEffect(libDir, id, allocator);
        Assert.Equal(1, effect.GetPluginInfo(out var info));
        Assert.Equal(3u, info.Word0);
        var context = new WwiseVoiceFxContext(new WwiseLiveVoice(1, 256), 0);
        Assert.Equal(1, effect.Init(allocator, context, Convert.FromHexString(block), new WwiseEffectFormat(48000, 0x4101)));
        Assert.Equal(1, effect.Reset());
        Assert.Throws<WwiseMissingBehaviourException>(() => effect.Execute(new WwiseDecodeState
        {
            Data = new float[512], ChannelConfig = 0x4102, MaxFrames = 256, ValidFrames = 256,
        }));
        for (int frame = 0; frame < expected.Length; frame++)
        {
            var samples = Samples((uint)(7 + frame));
            var state = new WwiseDecodeState { Data = samples, ChannelConfig = 0x4101, Scratch08 = 0x2D, MaxFrames = 256, ValidFrames = 256 };
            effect.Execute(state);
            Assert.Equal((ushort)256, state.MaxFrames);
            Assert.Equal((ushort)256, state.ValidFrames);
            Assert.Equal(0x2Du, state.Scratch08);
            string actual = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(samples.AsSpan()))).ToLowerInvariant()[..16];
            Assert.Equal(expected[frame], actual);
        }
        var tailSamples = Samples(12);
        var tail = new WwiseDecodeState { Data = tailSamples, ChannelConfig = 0x4101, Scratch08 = 0x11, MaxFrames = 256, ValidFrames = 199 };
        effect.Execute(tail);
        Assert.Equal((ushort)256, tail.MaxFrames);
        bool harmonizer = id == WwiseNativeVoiceEffect.HarmonizerId;
        Assert.Equal((ushort)(harmonizer ? 256 : 199), tail.ValidFrames);
        Assert.Equal((uint)(harmonizer ? 0x2D : 0x11), tail.Scratch08);
        string tailHash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(tailSamples.AsSpan()))).ToLowerInvariant()[..16];
        Assert.Equal(harmonizer ? "ae387b5117bad0bc" : "de3679e93363cb82", tailHash);
        Assert.Equal(1, effect.Term(allocator));
    }

    private static float[] Samples(uint seed)
    {
        seed = unchecked(seed * 2654435761u + 12345u);
        if (seed == 0) seed = 1;
        var data = new float[256];
        for (int i = 0; i < data.Length; i++)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            data[i] = unchecked((int)seed) / 2147483648.0f;
        }
        return data;
    }
}
