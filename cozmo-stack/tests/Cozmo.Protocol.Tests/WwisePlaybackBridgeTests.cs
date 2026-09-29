using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-025, the Play -> PBI -> voice -> source creation bridge. Expected values come from correction C21's
/// rows B1..B18 and the residual report, not from the implementation.
/// </summary>
public class WwisePlaybackBridgeTests
{
    // ------------------------------------------------------------------ bank builders (M6-006 layout)

    private static void U32(List<byte> b, uint v) => b.AddRange(BitConverter.GetBytes(v));
    private static void U16(List<byte> b, ushort v) => b.AddRange(BitConverter.GetBytes(v));
    private static void F32(List<byte> b, float v) => b.AddRange(BitConverter.GetBytes(v));

    private static byte[] Chunk(string tag, byte[] body)
    {
        var b = new List<byte>();
        b.AddRange(System.Text.Encoding.ASCII.GetBytes(tag));
        U32(b, (uint)body.Length);
        b.AddRange(body);
        return b.ToArray();
    }

    private static byte[] File(uint bankId, params byte[][] chunks)
    {
        var bkhd = new List<byte>();
        U32(bkhd, 120);
        U32(bkhd, bankId);
        U32(bkhd, 0);
        U32(bkhd, 0);
        U32(bkhd, 0);
        var file = new List<byte>();
        file.AddRange(Chunk("BKHD", bkhd.ToArray()));
        foreach (var c in chunks) file.AddRange(c);
        return file.ToArray();
    }

    private static byte[] Hirc(params (byte Type, byte[] Payload)[] objects)
    {
        var h = new List<byte>();
        U32(h, (uint)objects.Length);
        foreach (var (type, payload) in objects)
        {
            h.Add(type);
            U32(h, (uint)payload.Length);
            h.AddRange(payload);
        }
        return Chunk("HIRC", h.ToArray());
    }

    private static void NodeBlock(List<byte> b, uint parent)
    {
        b.Add(0); b.Add(0); b.Add(0);
        U32(b, 0); U32(b, parent);
        b.Add(0); b.Add(0); b.Add(0); b.Add(0); b.Add(0);
        b.AddRange(new byte[6]);
        U32(b, 0);
        U16(b, 0);
    }

    private static byte[] SoundPayload(uint id, uint plugin, byte stream, uint media, uint parent = 0)
    {
        var b = new List<byte>();
        U32(b, id);
        U32(b, plugin);
        b.Add(stream);
        U32(b, media);
        U32(b, 0);
        b.Add(0);
        NodeBlock(b, parent);
        return b.ToArray();
    }

    private static byte[] EventPayload(uint id, params uint[] actions)
    {
        var b = new List<byte>();
        U32(b, id);
        U32(b, (uint)actions.Length);
        foreach (var a in actions) U32(b, a);
        return b.ToArray();
    }

    private static byte[] ActionPayload(uint id, ushort type, uint target, bool isBus,
                                        (byte Id, uint Value)[] props,
                                        (byte Id, float Min, float Max)[] ranged,
                                        byte[] typeParams)
    {
        var b = new List<byte>();
        U32(b, id);
        U16(b, type);
        U32(b, target);
        b.Add(isBus ? (byte)1 : (byte)0);
        b.Add((byte)props.Length);
        foreach (var (pid, _) in props) b.Add(pid);
        foreach (var (_, value) in props) U32(b, value);
        b.Add((byte)ranged.Length);
        foreach (var (rid, _, _) in ranged) b.Add(rid);
        foreach (var (_, min, max) in ranged) { F32(b, min); F32(b, max); }
        b.AddRange(typeParams);
        return b.ToArray();
    }

    private static byte[] PlayParams(byte curve, uint bankId)
    {
        var b = new List<byte> { curve };
        U32(b, bankId);
        return b.ToArray();
    }

    /// <summary>A source that fills every frame with a known value, so the render is observable.</summary>
    private sealed class SyntheticSource : IWwiseVoiceSource, IWwiseVoiceSourceFormat
    {
        public SyntheticSource(float value, int channels = 1, int rate = 48000, uint format = 0x1234)
        {
            Value = value;
            Channels = channels;
            SampleRate = rate;
            SourceFormatWord = format;
        }

        public float Value { get; }
        public int RenderCalls { get; private set; }
        public int Channels { get; }
        public int SampleRate { get; }
        public uint SourceFormatWord { get; }

        /// <summary>The vt+0x28 raw result (0xA56650): 1 by default, 0x3F for a streamed source.</summary>
        public int StartStreamCode { get; init; } = 1;

        public bool StartStreamSucceeded { get; private set; }

        /// <summary>0xA56650: set [source+0x10] bit0 only when vt+0x28 returns 1.</summary>
        public bool StartStream()
        {
            StartStreamSucceeded = StartStreamCode == 1;
            return true;
        }

        public int Render(WwiseVoiceBuffer buffer)
        {
            RenderCalls++;
            int frames = buffer.MaxFrames;
            for (int c = 0; c < Channels; c++)
                for (int f = 0; f < frames; f++) buffer.Channels[c][f] = Value;
            buffer.ValidFrames = frames;
            buffer.Result = 0x2D;
            return 0x2D;
        }
    }

    // ------------------------------------------------------------------ B1/B5/B6: Play creates the PBI

    [Fact]
    public void APostedPlayCreatesThePbiWithTheRowsFields()
    {
        // B1: the Play helper resolves the target and calls node->vt+0x128. B5/B6: 0xA02EC8 -> 0xA000E8
        // stores playing id +0x140, descriptor +0x150, target +0x14C, the three 1.0s, the start offset,
        // the chain id and the flags. The node is a Sound with the shipped Vorbis plugin 0x00040001.
        const uint target = 500;
        var bank = WwiseBank.Parse(File(1,
            Hirc(
                (2, SoundPayload(target, WwiseSourceFactory.VorbisPlugin, 1, 12345)),
                (3, ActionPayload(100, 0x0403, target, false,
                    Array.Empty<(byte, uint)>(),
                    Array.Empty<(byte, float, float)>(),
                    PlayParams(3, 0))),
                (4, EventPayload(900, 100)))),
            "t.bnk");

        var runtime = new WwiseEventRuntime(new[] { bank }, new WwiseRng(1));
        var bridge = new WwisePlaybackBridge
        {
            InitSource9BEB30 = _ => true,                       // B7 seam (body unread)
            NodeVt90 = _ => { },                                // B7 seam (body unread)
            BeforePlayA00618 = _ => { },                        // B7 seam (body unread)
            SetupFadeInTransition = (_, _) => 0,                // B16/F1 seam (body unread)
        };
        runtime.PlaybackBridge = bridge;
        runtime.RegisterGameObject(7);

        uint playingId = runtime.PostEvent(900, 7);
        runtime.AdvanceFrame();

        var pbi = Assert.Single(bridge.Instances);
        Assert.Equal(playingId, pbi.PlayingId);                 // +0x140 = params+0x24
        Assert.Equal(target, pbi.TargetNodeId);                 // +0x14C = params+4
        var desc = Assert.IsType<WwiseSourceDescriptor>(pbi.SourceDescriptor);
        Assert.Equal(0x00040001u, desc.PluginId);               // node+0x5c (F13: source literal)
        Assert.Equal((byte)1, desc.StreamType);
        Assert.Equal(12345u, desc.SourceId);
        Assert.Equal(1f, pbi.Ratio);                            // +0x164 = 1.0
        Assert.Equal(1f, pbi.Fade168);                          // +0x168 = 1.0
        Assert.Equal(1f, pbi.Fade16C);                          // +0x16C = 1.0
        Assert.Equal(0u, pbi.StartOffset);                      // +0x1D8 = params+0x74
        Assert.NotEqual(0u, pbi.ChainId);                       // +0x1C8 auto-allocated when +0x7C == 0
        Assert.Equal(0x44, pbi.Flags1BD);                       // +0x1BD = (continuous)<<7 | 0x44
        Assert.Equal(0, pbi.Field1E4);                          // +0x1E4 = params+0x84
        Assert.Equal(0xFFFFFFFFu, pbi.Field1F8);                // F5: 0xA0021C/0xA00318
        Assert.Equal(0x44, pbi.Block170.Length);                // +0x170 = 0x44 bytes from params+0x28

        // B16: params+0x70 == 0 and pbi+0x1BA&7 != 1 -> start-list type 0.
        var startNode = Assert.Single(bridge.StartList.Nodes);
        Assert.Equal(0, startNode.Type);
        Assert.Equal(playingId, startNode.Pbi.PlayingId);
    }

    [Fact]
    public void APbiChainIdComesFromParamsWhenNonZeroAndSetsBit3()
    {
        // B6 2c.8: params+0x7C != 0 -> pbi+0x1C8 = params+0x7C and pbi+0x1BE |= 8.
        var p = new WwisePlayInitParams { PlayingId = 9, ChainId = 0x1234, TargetNodeId = 1 };
        var pbi = new WwisePlayingInstance(p, 1, new object(), new byte[0x44], null, 0, 0, continuous: false);
        Assert.Equal(0x1234u, pbi.ChainId);
        Assert.Equal(8, pbi.Flags1BE & 8);
    }

    // ------------------------------------------------------------------ B12/B13/B14: the source factory

    [Theory]
    [InlineData(WwiseSourceFactory.VorbisPlugin, (byte)1, WwiseSourceKind.VorbisStreamed)]
    [InlineData(WwiseSourceFactory.VorbisPlugin, (byte)2, WwiseSourceKind.VorbisStreamed)]
    [InlineData(WwiseSourceFactory.VorbisPlugin, (byte)0, WwiseSourceKind.VorbisInMemory)]
    [InlineData(WwiseSourceFactory.AdpcmPlugin, (byte)1, WwiseSourceKind.AdpcmMode1)]
    [InlineData(WwiseSourceFactory.AdpcmPlugin, (byte)0, WwiseSourceKind.AdpcmMode3)]
    public void TheStreamByteSelectsTheShippedSourceClass(uint plugin, byte stream, WwiseSourceKind expected)
    {
        // B14: stream 0 -> mode 3, stream 1/2 -> mode 1 (codec plugins only). B13: plugin>>16 == 2 -> ADPCM;
        // the registered list -> Vorbis, mode 1 streamed (0xAB0448) else in-memory (0xAB1550).
        int mode = WwiseSourceFactory.ModeForStream(plugin, stream);
        Assert.Equal(expected, WwiseSourceFactory.Select(mode, plugin)!.Value);
    }

    [Fact]
    public void ThePcmAndModeTwoClassesAreRefusedRatherThanApproximated()
    {
        // B13: no shipped bank uses PCM; the mode==2 class body is unread.
        Assert.Equal(WwiseSourceKind.PcmMode1, WwiseSourceFactory.Select(1, 0x00010001));
        Assert.Throws<NotSupportedException>(() => WwiseSourceFactory.Create(
            WwiseSourceKind.PcmMode1, new WwiseSourceDescriptor(0x00010001, 1, 1, 0, 0), _ => null, null));
        Assert.Throws<NotSupportedException>(() => WwiseSourceFactory.Create(
            WwiseSourceKind.PluginMode2, new WwiseSourceDescriptor(0x00040001, 1, 1, 0, 0), _ => null, null));
    }

    // ------------------------------------------------------------------ B9: CalcEffectiveParams

    [Fact]
    public void CalcEffectiveParamsComposesTheRowsFields()
    {
        // B9 3.4/3.5/3.6: Volume +0x3C, Pitch +0x44, LPF +0x48, HPF +0x4C, mute/fade +0x40. The node chain
        // is M6-010's GetAudioParameters; with no randomizer ranges and an empty muted map, the compose is
        // the node's own props and the mute/fade product is pbi+0x168 * pbi+0x16C.
        var node = new WwiseGainNode
        {
            Id = 1,
            Props = new Dictionary<byte, float>
            {
                [WwiseGainProps.Volume] = 6f,
                [WwiseGainProps.Pitch] = 100f,
                [WwiseGainProps.LowPass] = 15f,
                [WwiseGainProps.HighPass] = 0f,
            },
        };

        var effective = WwiseCalcEffectiveParams.Calculate(
            node, WwiseParamSelect.NodeParams, new WwiseGainRanges(), null, default, null, 1f, 1f);

        Assert.Equal(6f, effective.VolumeDb);                    // pbi+0x3C
        Assert.Equal(100f, effective.PitchCents);                // pbi+0x44
        Assert.Equal(15f, effective.LowPass);                    // pbi+0x48
        Assert.Equal(0f, effective.HighPass);                    // pbi+0x4C
        Assert.Equal(1f, effective.MuteFade);                    // pbi+0x40 = 1*1*1
        Assert.Equal(WwiseGain.DbToLinear(6f), effective.VoiceGain);   // voice+0x1C
    }

    [Fact]
    public void CalcEffectiveParamsResetsWhenTheNodeIsNull()
    {
        // B9 3.1: param_2 == 0 resets to 0 and pbi+0x40 = 1.0.
        var effective = WwiseCalcEffectiveParams.Calculate(null, WwiseParamSelect.NodeParams, null, null, default, null, 1f, 1f);
        Assert.Equal(0f, effective.VolumeDb);
        Assert.Equal(1f, effective.MuteFade);
        Assert.Equal(1f, effective.VoiceGain);
    }

    // ------------------------------------------------------------------ B10/B11: AddSrc and attach

    [Fact]
    public void AddSrcSetsVoiceSourceAndBusOwnerAndReturnsOne()
    {
        // B11: on StartStream success store voice+0xD4 (bActive=1) and voice+8 = [source+0xC]+0xC.
        // 0xA56650: a vt+0x28 result of 1 returns 1 and sets [source+0x10] bit0.
        var bridge = new WwisePlaybackBridge();
        var synthetic = new SyntheticSource(0.5f, format: 0xABCD);
        var sentinel = new object();
        bridge.SourceFactory = _ => synthetic;
        bridge.BusOwnerFor = _ => sentinel;

        var pbi = bridge.CreatePbi(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);
        var voice = new WwiseLiveVoice(1, 16);

        int r = bridge.AddSrc(voice, pbi, bActive: true);

        Assert.Equal(1, r);
        Assert.Same(synthetic, voice.Source);                    // voice+0xD4
        Assert.Same(sentinel, voice.BusOwner8);                  // voice+8
        Assert.Same(voice, pbi.Field154);                        // F6: 0xA558F8 before StartStream
        Assert.Equal(0xABCDu, pbi.SourceFormat158);              // B15 source-format write
    }

    [Fact]
    public void AddSrcReturns0x3fWhenTheSourceCodeIs0x3f()
    {
        // 0xA56650: a vt+0x28 result of 0x3F returns 0x3F and leaves [source+0x10] bit0 clear.
        var bridge = new WwisePlaybackBridge();
        var source = new SyntheticSource(0.5f) { StartStreamCode = 0x3F };
        bridge.SourceFactory = _ => source;
        var pbi = bridge.CreatePbi(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);

        Assert.Equal(0x3F, bridge.AddSrc(new WwiseLiveVoice(1, 16), pbi, bActive: true));
        Assert.False(source.StartStreamSucceeded);               // the 0x3F result does not set the bit
    }

    [Fact]
    public void AttachVoiceCreatesAVoiceAndTheDrainKeepsAnAlreadyAttachedNode()
    {
        // B10: no chain match -> allocate 0x540, 0xA548B8 sets voice+0xEC, AddSrc bActive=1.
        // 0x9D36B4/0x9D36A0: a PBI whose +0x154 != 0 is skipped (advanced past), NOT unlinked.
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => new SyntheticSource(0.25f) };
        var engine = new object();
        bridge.LinkEngineA548B8 = v => v.EngineEC = engine;
        bridge.LinkVoiceA42DEC = (_, _) => 1;                    // 0xA42DEC return 1 -> node kept
        var pbi = bridge.CreatePbi(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);

        Assert.Equal(1, bridge.AttachVoice(pbi));                // 0xA430BC/0xA43194 -> 1
        var voice = Assert.Single(bridge.Voices);
        Assert.Same(engine, voice.EngineEC);                     // 0xA548B8
        Assert.Same(voice, pbi.Field154);                        // 0xA558F8

        bridge.StartList.Enqueue(0, pbi, 0);
        Assert.Equal(0, bridge.DrainStartList());                // skipped, not counted as kept
        Assert.Single(bridge.StartList.Nodes);                   // but retained in the list
    }

    [Fact]
    public void TheDrainGateGatesAndAFreshNodeIsKept()
    {
        // F9: 0x9D3CA4 ldrb r3,[r3,#0x28]; cmp r3,#0; beq -> the gate must be set before 0x9D3644 runs.
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => new SyntheticSource(0.25f) };
        bridge.LinkEngineA548B8 = _ => { };
        bridge.LinkVoiceA42DEC = (_, _) => 1;
        var pbi = bridge.CreatePbi(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);

        Assert.Equal(0, bridge.DrainStartList());                // gate clear
        bridge.StartList.Enqueue(0, pbi, 0);
        Assert.Equal(1, bridge.DrainStartList());
        Assert.Single(bridge.StartList.Nodes);
    }

    [Fact]
    public void TheChainMatchReusesAVoiceAndMarksPbi1Ba()
    {
        // F3: 0xA43120 bl 0xA01878 sets pbi+0x1BA bits 3..6 = 3; the match returns 5.
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => new SyntheticSource(0.25f) };
        var pbi = bridge.CreatePbi(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1, ChainId = 0x77 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);
        bridge.Voices.Add(new WwiseLiveVoice(1, 16) { BusOwner8 = "owner" });
        bridge.BusOwnerChainId = _ => 0x77;

        Assert.Equal(5, bridge.AttachVoice(pbi));
        Assert.Equal(0x18, pbi.Flags1BA & 0x78);                 // bits 3..6 = 3
        Assert.Single(bridge.Voices);                            // reused, not a new voice
    }

    [Fact]
    public void TheFadeInBranchRunsTheTransitionSeamAndSetsBit6()
    {
        // F1 / 0xA00694: arg2[0] != 0 -> 0xA366F4/0xA36268, 0xA00814 sets pbi+0x1BE bit6, 0xA00788 zeroes
        // pbi+0x168, stores pbi+0x144, and calls pbi->vt+0x50(pbi,0xe,arg2[0]) (0xA0081C).
        var bridge = new WwisePlaybackBridge();
        bool called = false;
        bridge.SetupFadeInTransition = (_, t) => { called = true; Assert.Equal(250f, t.FadeInTime); return 0xBEEF; };
        var pbi = bridge.CreatePbi(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);

        bridge.PbiPlay(pbi, new WwisePlayInitParams
        {
            PlayingId = 1,
            TargetNodeId = 1,
            Transition = new WwiseFadeInTransition { FadeInTime = 250f, FadeCurve = 3 },
        });

        Assert.True(called);
        Assert.Equal(0f, pbi.Fade168);                           // 0xA00788
        Assert.Equal(0x40, pbi.Flags1BE & 0x40);                 // 0xA00814
        Assert.Equal(0xBEEFu, pbi.Field144);                     // pbi+0x144
        Assert.Single(bridge.StartList.Nodes);
    }

    [Fact]
    public void AddSrcReturnsOneWhenTheSourceWasAlreadyStarted()
    {
        // 0xA5665C: [source+0x10] bit0 set -> 1; the first vt+0x28 result of 1 also returns 1 and sets it.
        var bridge = new WwisePlaybackBridge();
        var source = new SyntheticSource(0.5f);
        bridge.SourceFactory = _ => source;
        var pbi = bridge.CreatePbi(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);

        Assert.Equal(1, bridge.AddSrc(new WwiseLiveVoice(1, 16), pbi, bActive: true));
        Assert.True(source.StartStreamSucceeded);
        Assert.Equal(1, bridge.AddSrc(new WwiseLiveVoice(1, 16), pbi, bActive: false));
    }

    [Fact]
    public void TheFactoryRefusesModeZero()
    {
        // F4: 0xA562D4 cmp r3,#0; 0xA562D8 beq 0xA563B4 returns 0 before the plugin-class dispatch.
        Assert.Null(WwiseSourceFactory.Select(0, 0x00020001));
        Assert.Null(WwiseSourceFactory.Select(0, 0x00040001));
    }

    [Fact]
    public void TheCtorMapsParamsBit4ToPbi1BeBit6Only()
    {
        // F8: 0xA002A8 ubfx r3,r3,#4,#1; 0xA002AC bfi r1,r3,#6,#1.
        var pbi = new WwisePlayingInstance(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1, Flags128 = 0x10 },
            1, new object(), new byte[0x44], null, 0, 0, continuous: false);
        Assert.Equal(0x40, pbi.Flags1BE & 0x40);
        Assert.Equal(0, pbi.Flags1BE & 0x30);
    }

    // ------------------------------------------------------------------ B13/B15: a synthetic render

    [Fact]
    public void ASyntheticSourceRendersThroughTheVoiceAndBusPass()
    {
        // The voice render (V8 0xA44630) executes the source and mixes it through the dry connection.
        var bus = new WwiseMixBus(new WwiseMixBusKey(0, 0, 0, 0), Array.Empty<WwiseBusFxSlot>());
        var voice = new WwiseLiveVoice(1, WwiseRuntimeSettings.SamplesPerFrame)
        {
            Source = new SyntheticSource(0.5f),
        };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1) { HasDry = true });

        voice.Render();

        var synthetic = Assert.IsType<SyntheticSource>(voice.Source);
        Assert.Equal(1, synthetic.RenderCalls);
        Assert.Equal(0x2D, voice.Buffer.Result);
        Assert.Contains(bus.Buffer.Take(WwiseRuntimeSettings.SamplesPerFrame), s => s != 0f);
    }
}