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

        /// <summary>The vt+0x28 raw result: 1 by default, 0x3F for a streamed source (M6-025 C27 step 7: any int is possible).</summary>
        public int StartStreamCode { get; init; } = 1;

        /// <summary>[source+0x10] bit0, written only by 0xA56650 (WwiseVoiceSourceStart), not by the source.</summary>
        public bool StartStreamSucceeded { get; set; }

        /// <summary>The arguments vt+0x28 received (C30.1(d): [owner+0x1DC], [owner+0x1E0]).</summary>
        public List<(uint, uint)> StartStreamCalls { get; } = new();

        public int StartStream(uint arg1DC, uint arg1E0)
        {
            StartStreamCalls.Add((arg1DC, arg1E0));
            return StartStreamCode;
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

        var runtime = WwiseEndOfEventDoubles.Runtime(new[] { bank }, new WwiseRng(1));
        var bridge = new WwisePlaybackBridge
        {
            Limiter = WwisePlaybackLimiterTestDoubles.Create(runtime.FindNode),   // M6-026: node->vt+0x90 is the walker, not a seam
        }.WithTestSeams();
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
        Assert.Equal(0x45, pbi.Flags1BD);                       // +0x1BD = (continuous)<<7 | 0x44 at creation (0xA00288), |= 1 by 0xA00618 (0xA00640)
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
        var pbi = new WwisePlayingInstance(p, 1, new object(), new byte[0x44], null, continuous: false);
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

    // ------------------------------------------------------------------ B10/B11: AddSrc and attach

    [Fact]
    public void AddSrcSetsVoiceSourceAndBusOwnerAndReturnsOne()
    {
        // B11: on StartStream success store voice+0xD4 (bActive=1) and voice+8 = [source+0xC]+0xC = pbi+0xC
        // (C24 header, C25.4). 0xA56650: a vt+0x28 result of 1 returns 1 and sets [source+0x10] bit0.
        var bridge = new WwisePlaybackBridge().WithTestSeams();
        var synthetic = new SyntheticSource(0.5f, format: 0xABCD);
        bridge.SourceFactory = _ => synthetic;

        var pbi = bridge.CreatePbiWithMediaWords(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);
        var voice = new WwiseLiveVoice(1, 16);

        int r = bridge.AddSrc(voice, pbi, bActive: true);

        Assert.Equal(1, r);
        Assert.Same(synthetic, voice.Source);                    // voice+0xD4
        Assert.Same(pbi, voice.BusOwner8);                       // voice+8 = pbi+0xC
        Assert.Same(voice, pbi.Field154);                        // F6: 0xA558F8 before StartStream
        // C27 further facts: AddSrc stores nothing to pbi+0x158 (its only PBI stores are +0x154 and +0x1BE).
        Assert.Equal(0u, pbi.SourceFormat158);
    }

    [Fact]
    public void AddSrcReturns0x3fWhenTheSourceCodeIs0x3f()
    {
        // 0xA56650: a vt+0x28 result of 0x3F returns 0x3F and leaves [source+0x10] bit0 clear.
        var bridge = new WwisePlaybackBridge().WithTestSeams();
        var source = new SyntheticSource(0.5f) { StartStreamCode = 0x3F };
        bridge.SourceFactory = _ => source;
        var pbi = bridge.CreatePbiWithMediaWords(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);

        Assert.Equal(0x3F, bridge.AddSrc(new WwiseLiveVoice(1, 16), pbi, bActive: true));
        Assert.False(source.StartStreamSucceeded);               // the 0x3F result does not set the bit
    }

    /// <summary>A linker over no output device: 0xA42DEC reaches only the init gate and the list insert.</summary>
    private static WwiseVoiceLinker EmptyDeviceLinker(WwisePlaybackBridge bridge) =>
        new(new WwiseMixBusHierarchy(), new WwiseOutputDeviceList(), bridge.Voices,
            _ => new WwisePbiRouting { Node = new WwiseRoutingNode { Id = 1 } },   // vt+0x88 returns 0: no bus (C24.9)
            new WwiseVoiceLinkSeams { InitVoiceA54A30 = _ => 1 });

    [Fact]
    public void AttachVoiceCreatesAVoiceAndTheDrainKeepsAnAlreadyAttachedNode()
    {
        // B10: no chain match -> allocate 0x540, 0xA548B8 sets voice+0xEC, AddSrc bActive=1.
        // 0x9D36B4/0x9D36A0: a PBI whose +0x154 != 0 is skipped (advanced past), NOT unlinked.
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => new SyntheticSource(0.25f) }.WithTestSeams();
        var engine = new object();
        bridge.LinkEngineA548B8 = v => v.EngineEC = engine;
        bridge.Linker = EmptyDeviceLinker(bridge);               // 0xA42DEC with no output device -> 1, node kept
        var pbi = bridge.CreatePbiWithMediaWords(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);

        Assert.Equal(1, bridge.AttachVoice(pbi));                // 0xA430BC/0xA43194 -> 1
        var voice = Assert.Single(bridge.Voices);
        Assert.Same(engine, voice.EngineEC);                     // 0xA548B8
        Assert.Same(voice, pbi.Field154);                        // 0xA558F8

        bridge.StartList.Enqueue(0, pbi, 0);
        Assert.Equal(0, bridge.RunStartListPass1());             // pass 1 0x9D3644: skipped, not counted as kept
        Assert.Single(bridge.StartList.Nodes);                   // but retained in the list
    }

    [Fact]
    public void TheDrainGateGatesPass1AndAFreshNodeIsAttachedThenDispatchedByPass2()
    {
        // F9: 0x9D3CA4 ldrb r3,[r3,#0x28]; cmp r3,#0; beq -> the gate must be set before 0x9D3644 runs, and it is
        // cleared at its end (0x9D3764..0x9D3770). C24.7 / research 6.6: for a Sound Play (state 0, AddSrc == 1)
        // pass 1 attaches and 0xA42DEC sets node+0xD bit0, and pass 2 of the same 0x9D3C98 runs 0xA54480(voice),
        // the source start 0xA56478, then frees the node (0x9D3AD4..0x9D3B4C).
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => new SyntheticSource(0.25f) }.WithTestSeams();
        var started = new List<WwiseLiveVoice>();
        // C25.1: the voice ctor stores [voice+0xDC] = 0 (0xA54798); 0xA548B8 (voice+0xEC = engine) does not touch it.
        bridge.LinkEngineA548B8 = v => v.EngineEC = new object();
        bridge.StartSourceA56478 = started.Add;
        bridge.Linker = EmptyDeviceLinker(bridge);
        var pbi = bridge.CreatePbiWithMediaWords(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);

        Assert.Equal(0, bridge.DrainStartList());                // gate clear, empty list
        bridge.StartList.Enqueue(0, pbi, 0);
        Assert.True(bridge.StartList.Gate);
        Assert.Equal(1, bridge.DrainStartList());                // pass 1 kept the node
        Assert.False(bridge.StartList.Gate);
        Assert.Empty(bridge.StartList.Nodes);                    // pass 2 dispatched and freed it
        var voice = Assert.Single(started);
        Assert.Equal(1, voice.State);                            // 0xA54480: [voice+0xDC] = 1
        Assert.Contains(voice, bridge.Voices);                   // linked by 0xA42DEC
    }

    [Fact]
    public void TheChainMatchReusesAVoiceAndMarksPbi1Ba()
    {
        // F3: 0xA43120 bl 0xA01878 sets pbi+0x1BA bits 3..6 = 3; the match returns 5.
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => new SyntheticSource(0.25f) }.WithTestSeams();
        var pbi = bridge.CreatePbiWithMediaWords(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1, ChainId = 0x77 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);
        // C25.4: [voice+8] = the owner's pbi+0xC, so [voice+8]+0x1BC is the owner's pbi+0x1C8 (0xA430E8).
        var owner = bridge.CreatePbiWithMediaWords(
            new WwisePlayInitParams { PlayingId = 2, TargetNodeId = 1, ChainId = 0x77 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);
        var voice = new WwiseLiveVoice(1, 16) { BusOwner8 = owner };
        bridge.Voices.Add(voice);

        Assert.Equal(5, bridge.AttachVoice(pbi));
        Assert.Equal(0x18, pbi.Flags1BA & 0x78);                 // bits 3..6 = 3
        Assert.Single(bridge.Voices);                            // reused, not a new voice
        Assert.Same(owner, voice.BusOwner8);                     // C25.4: the reuse path leaves voice+8
        Assert.NotNull(voice.Pending);                           // voice+0xD8
    }

    [Fact]
    public void ANonMatchingChainIdCreatesANewVoiceAndALiveVoiceWithoutAnOwnerIsRefused()
    {
        // 0xA430E8: no owner chain equals pbi+0x1C8 -> a new voice. A live voice always has [voice+8]; the native
        // dereferences it, so a live voice with no owner PBI is not skipped silently (C25.4).
        var bridge = new WwisePlaybackBridge { SourceFactory = _ => new SyntheticSource(0.25f) }.WithTestSeams();
        bridge.LinkEngineA548B8 = _ => { };
        bridge.Linker = EmptyDeviceLinker(bridge);
        var other = bridge.CreatePbiWithMediaWords(
            new WwisePlayInitParams { PlayingId = 2, TargetNodeId = 1, ChainId = 0x11 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);
        var pbi = bridge.CreatePbiWithMediaWords(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1, ChainId = 0x77 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);
        bridge.Voices.Add(new WwiseLiveVoice(1, 16) { BusOwner8 = other });

        Assert.Equal(1, bridge.AttachVoice(pbi));
        Assert.Equal(2, bridge.Voices.Count);
        Assert.Same(pbi, bridge.Voices[0].BusOwner8);            // inserted at the head (row 20)

        bridge.Voices.Insert(0, new WwiseLiveVoice(1, 16));
        Assert.Throws<InvalidOperationException>(() => bridge.AttachVoice(pbi));
    }

    [Fact]
    public void TheFadeInBranchCreatesTheTransitionItemAndSetsBit6()
    {
        // P10 (0xA0067C) with a fade time word != 0 and no transition at pbi+0x144: pbi+0x168 = 0.0f (0xA00788), 0xA36268(mgr, {pbi+8, 0x1000000, 0.0f, 1.0f, time, curve, 0, 1, 0}, 1, 0), 1BE |= 0x40
        // (0xA00814), pbi+0x144 = the item (0xA00824). P11: the item's state is 1 (the start argument) and it is in the manager's first vector.
        var bridge = new WwisePlaybackBridge().WithTestSeams();
        bridge.Limiter = WwisePlaybackLimiterTestDoubles.Create(_ => null);
        WwiseTransitionInfo? seen = null;
        bridge.PlayPath!.Seams.TransitionInit9A35D44 = (_, info, tick) => { seen = info; return 1; };
        var manager = new WwiseTransitionManager(bridge.PlayPath.Seams);
        bridge.PlayPath.Transitions = manager;
        var pbi = bridge.CreatePbiWithMediaWords(
            new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1,
            new WwiseSourceDescriptor(WwiseSourceFactory.AdpcmPlugin, 1, 1, 0, 0), continuous: false);

        Assert.Equal(1, bridge.PbiPlay(pbi, new WwisePlayInitParams
        {
            PlayingId = 1,
            TargetNodeId = 1,
            Transition = new WwiseFadeInTransition { FadeInTime = 250f, FadeCurve = 3 },
        }));

        var item = manager.Item(pbi.Field144);
        Assert.NotNull(item);
        Assert.Equal(1, item!.State30);                          // 0xA362EC..0xA362FC
        Assert.Equal(0x20000000u, item.Word0);                   // 0xA35858..0xA35860
        Assert.Same(item, Assert.Single(manager.ListA));         // 0xA362F0
        Assert.Equal(0f, pbi.Fade168);                           // 0xA00788
        Assert.Equal(0x40, pbi.Flags1BE & 0x40);                 // 0xA00814
        Assert.Equal(0x1000000u, seen!.Id);                      // 0xA007E0
        Assert.Equal(0f, seen.From);                             // [sp+0x1C] = 0.0f (0xA007BC)
        Assert.Equal(1f, seen.To);                               // [sp+0x20] = 1.0f (0xA007B8)
        Assert.Equal(BitConverter.SingleToUInt32Bits(250f), seen.TimeBits);   // 0xA007F0
        Assert.Equal(3u, seen.Curve);                            // 0xA007C0
        Assert.Equal((byte)1, seen.Byte1);                       // 0xA007F4
        Assert.Single(bridge.StartList.Nodes);
    }

    [Fact]
    public void AddSrcReturnsOneWhenTheSourceWasAlreadyStarted()
    {
        // 0xA5665C: [source+0x10] bit0 set -> 1; the first vt+0x28 result of 1 also returns 1 and sets it.
        var bridge = new WwisePlaybackBridge().WithTestSeams();
        var source = new SyntheticSource(0.5f);
        bridge.SourceFactory = _ => source;
        var pbi = bridge.CreatePbiWithMediaWords(
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
            1, new object(), new byte[0x44], null, continuous: false);
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
        var dry = new WwiseVoiceConnection(bus, 1, 1);
        dry.Descriptor.Reserve(1, 1);                            // [conn+0x18] != 0 (C24.4)
        voice.Connections.Add(dry);

        voice.Render();

        var synthetic = Assert.IsType<SyntheticSource>(voice.Source);
        Assert.Equal(1, synthetic.RenderCalls);
        Assert.Equal(0x2D, voice.Buffer.Result);
        Assert.Contains(bus.Buffer.Take(WwiseRuntimeSettings.SamplesPerFrame), s => s != 0f);
    }
}
/// <summary>
/// Test doubles for the bridge's required seams whose bodies the inventory leaves unread (C26.4, C26.5). They are
/// no-ops: they claim nothing about the source classes or the pre-step bodies. The tests that check a seam is
/// required build a bridge without this helper.
/// </summary>
internal static class WwiseBridgeTestSeams
{
    /// <summary>
    /// <c>CreatePbi</c> and the unread writer of <c>pbi+0x1DC/+0x1E0</c> (<c>0xA1EC54</c> through <c>pbi vt+0xC</c>, M6-026 6.1): the values are
    /// the test doubles (0x11112222, 0x33334444) of re-analysis/tools/emu/emu_notready.py; they claim nothing about the media.
    /// </summary>
    public static WwisePlayingInstance CreatePbiWithMediaWords(
        this WwisePlaybackBridge bridge, WwisePlayInitParams p, uint targetNodeId, object sourceDescriptor, bool continuous)
    {
        var pbi = bridge.CreatePbi(p, targetNodeId, sourceDescriptor, continuous);
        pbi.Word1DC = 0x11112222;
        pbi.Word1E0 = 0x33334444;
        return pbi;
    }

    public static WwisePlaybackBridge WithTestSeams(this WwisePlaybackBridge bridge)
    {
        bridge.SourceFormatWriter15C ??= (_, _) => { };      // leaves the ctor default 0x4101 (C26.5 gap, not a source claim)
        // C27 seams with unread bodies. The defaults make no source claim: 0x9EEDA4 returns (0, 0) so [voice+0xE4] = 0 and
        // the 0x9BCA68 gate is not reached unless a test sets a code; the send-table allocation succeeds; the source close and free
        // do nothing.
        bridge.SourceDestructAndPoolFree ??= _ => { };
        bridge.PositionRepository ??= new WwisePlayPositionRepository(() => 0);
        // Test double: the routing node [pbi+0xE0] comes from a minimal linker; 0x9EEDA4 returns code 0, so vt+0x120
        // (NodeVt120, left unset: required) is not reached unless a test sets a code of 3.
        bridge.Linker ??= new WwiseVoiceLinker(
            new WwiseMixBusHierarchy(), new WwiseOutputDeviceList(), new List<WwiseLiveVoice>(),
            _ => new WwisePbiRouting { Node = new WwiseRoutingNode { Id = 1 } }, new WwiseVoiceLinkSeams());
        bridge.NextSource9EEDA4 ??= (WwiseNode? _, out int index) => { index = 0; return 0; };   // test double: an override of the read 0x9EEDA4 body (code 0, index 0)
        bridge.TailA023D4 ??= (_, _) => { };                 // test doubles: the 0xA37FE8..0xA38070 tail bodies are unread
        bridge.TailA01918 ??= (_, _, _) => { };
        bridge.TailA9E85C8 ??= (_, _, _) => { };
        bridge.SourceClose2C ??= _ => { };                   // test double: src vt+0x2C is unread
        // The shipped Play path (C32.1) with doubles for the unread callees: every node has an output bus (the node itself), the bus has no
        // contribution of its own, node vt+0xAC accumulates nothing, and no node has an RTPC the doubles would have to evaluate.
        bridge.CtxNodeChainFlag9BC90C ??= _ => false;        // test double: the writers of [node+0x40] bits 17..19 are unread
        bridge.PlayPath ??= new WwisePlayPath(n => bridge.Limiter?.ParentNode(n), new WwisePlaySeams
        {
            FirstOutputBus9F4BB8 = n => n,
            BusFlag9C54E8 = _ => true,
            NodeVtAC = _ => { },
        });
        bridge.NewVoiceAllocSendTable4C ??= _ => new WwiseVoiceSendTable();
        return bridge;
    }
}
