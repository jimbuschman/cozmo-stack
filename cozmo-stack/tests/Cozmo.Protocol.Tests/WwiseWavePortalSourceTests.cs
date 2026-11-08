using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class WwiseWavePortalSourceTests
{
    private static WwisePlayingInstance Owner() => new(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, false);
    private static WwiseRuntimeGraph Graph(byte type, uint id, uint plugin)
    {
        var body = new byte[21];
        BitConverter.GetBytes(id).CopyTo(body, 0);
        BitConverter.GetBytes(plugin).CopyTo(body, 4);
        BitConverter.GetBytes(4u).CopyTo(body, 8);
        var graph = new WwiseRuntimeGraph(new WwiseGraphHostInputs(0, 48000, 1024, false, true, true), new WwiseGraphSeams(), null);
        Assert.Equal(1, graph.LoadBank("wave", Hb.Bank(1, (type, body))));
        return graph;
    }

    [Fact]
    public void RegistryLookupPrecedesSingleClaimAndFramesReachPitchInput()
    {
        const uint id = 77;
        var graph = Graph(19, id, WavePortal.PluginId);
        var portal = new WavePortal();
        portal.Give(new float[] { 1, 2, 3 }, 22050);
        var descriptor = new WwiseSourceDescriptor(WavePortal.PluginId, 0, id, 0, 0);
        Assert.Equal(2, WwiseSourceFactory.ModeForStream(descriptor.PluginId, descriptor.StreamType));
        var noOwner = WwiseSourceFactory.Create(WwiseSourceKind.PluginMode2, descriptor, _ => null, null, registry: graph.Registry, portal: portal);
        Assert.Throws<WwiseMissingBehaviourException>(() => noOwner.StartStream(0, 0));
        var source = Assert.IsType<WwiseWavePortalSource>(WwiseSourceFactory.Create(WwiseSourceKind.PluginMode2, descriptor, _ => null, null, registry: graph.Registry, owner: Owner(), portal: portal));
        var wrong = Assert.IsType<WwiseWavePortalSource>(WwiseSourceFactory.Create(WwiseSourceKind.PluginMode2, descriptor with { SourceId = id + 1 }, _ => null, null, registry: graph.Registry, owner: Owner(), portal: portal));
        Assert.Equal(2, wrong.StartStream(0, 0));
        Assert.Equal(1, source.StartStream(0, 0));
        Assert.Equal(22050, source.SampleRate);
        var buffer = new WwiseVoiceBuffer(1, 2) { ValidFrames = 2 };
        source.Io = buffer.State;
        buffer.State.Rate = 99;
        buffer.State.ValidFrames = 1; // +0xE is not the capacity at +0xC
        Assert.Equal(WavePortal.DataReady, source.Render(buffer));
        Assert.Equal(new float[] { 1, 2 }, (float[])buffer.State.Data!);
        Assert.Equal(2, buffer.State.ValidFrames);
        Assert.Equal(99u, buffer.State.Rate);
        Assert.Equal(WavePortal.NoMoreData, source.Render(buffer));
        Assert.Equal(new float[] { 3, 0 }, (float[])buffer.State.Data!);
        Assert.Equal(2, buffer.State.ValidFrames);
        Assert.Equal(2, wrong.StartStream(0, 0));
        var second = WwiseSourceFactory.Create(WwiseSourceKind.PluginMode2, descriptor, _ => null, null, registry: graph.Registry, owner: Owner(), portal: portal);
        Assert.Equal(2, second.StartStream(0, 0));
    }

    [Fact]
    public void WrongTableOrPluginDoesNotClaimAndExactEndRemainsReady()
    {
        var registry = new WwiseRuntimeRegistry();
        registry.Insert(new WwiseRuntimeFx { Id = 7, Custom = false, PluginId10 = WavePortal.PluginId });
        registry.Insert(new WwiseRuntimeFx { Id = 8, Custom = true, PluginId10 = 123 });
        var portal = new WavePortal();
        portal.Give(new float[] { 7, 8 }, 22050);
        WwiseWavePortalSource Make(uint id) => (WwiseWavePortalSource)WwiseSourceFactory.Create(
            WwiseSourceKind.PluginMode2, new WwiseSourceDescriptor(WavePortal.PluginId, 0, id, 0, 0),
            _ => null, null, registry: registry, owner: Owner(), portal: portal);
        Assert.Equal(2, Make(7).StartStream(0, 0));
        var failed = Make(8);
        Assert.Equal(2, failed.StartStream(0, 0));
        var failedBuffer = new WwiseVoiceBuffer(1, 2) { ValidFrames = 2 };
        failed.Io = failedBuffer.State;
        failedBuffer.State.ValidFrames = 1;
        failedBuffer.State.ChannelConfig = 99;
        Assert.Equal(2, failed.Render(failedBuffer));
        Assert.Null(failedBuffer.State.Data);
        Assert.Equal(0, failedBuffer.State.MaxFrames);
        Assert.Equal(0, failedBuffer.State.ValidFrames);
        Assert.Equal(99u, failedBuffer.State.ChannelConfig);
        Assert.Equal(0x2Bu, failedBuffer.State.Scratch08);
        Assert.Equal(uint.MaxValue, failedBuffer.State.Position);
        Assert.Equal(uint.MaxValue, failedBuffer.State.Total);
        Assert.Equal(1u, failedBuffer.State.Rate);
        Assert.Equal(0x3F800000u, failedBuffer.State.Word1C);
        registry.Insert(new WwiseRuntimeFx { Id = 9, Custom = true, PluginId10 = WavePortal.PluginId });
        var source = Make(9);
        Assert.Equal(1, source.StartStream(0, 0));
        var buffer = new WwiseVoiceBuffer(1, 2) { ValidFrames = 2 };
        source.Io = buffer.State;
        buffer.ValidFrames = 0;
        buffer.State.Scratch08 = 42;
        buffer.State.ValidFrames = 1;
        Assert.Equal(WavePortal.NoMoreData, source.Render(buffer));
        Assert.Equal(42u, buffer.State.Scratch08);
        Assert.Equal(1, buffer.State.ValidFrames);
        buffer.ValidFrames = 2;
        Assert.Equal(WavePortal.DataReady, source.Render(buffer));
        Assert.Equal(new float[] { 7, 8 }, (float[])buffer.State.Data!);
        buffer.ValidFrames = 0;
        Assert.Equal(WavePortal.NoMoreData, source.Render(buffer));
    }

    [Fact]
    public void DefaultBridgeCreatesSourceAndWritesProviderFormat()
    {
        var graph = Graph(19, 77, WavePortal.PluginId);
        var bridge = new WwisePlaybackBridge().WithTestSeams();
        bridge.SourceRegistry = graph.Registry;
        var pbi = bridge.CreatePbiWithMediaWords(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WavePortal.PluginId, 0, 77, 0, 0), false);
        pbi.Byte162 = 0xA0;
        var voice = new WwiseLiveVoice(1, 16);
        WavePortal.Shared.Give(new float[] { 3 }, 22050);
        try
        {
            Assert.Equal(1, bridge.AddSrc(voice, pbi, true));
            Assert.IsType<WwiseWavePortalSource>(voice.Source);
            Assert.Equal(22050u, pbi.SourceFormat158);
            Assert.Equal(0x4101u, pbi.Word15C);
            Assert.Equal(0x20, pbi.Byte160);
            Assert.Equal(1, pbi.Byte161);
            Assert.Equal(5, pbi.Byte162);
        }
        finally { WavePortal.Shared.Clear(); }
    }
}
