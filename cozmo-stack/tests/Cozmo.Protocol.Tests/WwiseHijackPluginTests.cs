using System.Runtime.InteropServices;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-015 / the rows M6 A13..A18 and gapB R1..R5, and C38 (B-06, B-07): the Anki Hijack plug-in.
///
/// Every expected value below is the row's own value or arithmetic applied by hand, or the engine's own result under Unicorn (<c>emu_pitch.py</c>), never a value read back from
/// <see cref="WwiseHijackPlugin"/>:
/// <list type="bullet">
/// <item>the static registration <c>{type 3, company 0x12C, id 1, create 0x008DBC71, params 0x008DBD11}</c>
/// (R3, <c>0x004DD90C</c>);</item>
/// <item>1,024 floats per channel and Init(fmt, 22320) then SetPitch(0) (A15);</item>
/// <item>the 48 kHz to 22,320 Hz step <c>round(48000/22320 * 65536)</c> = 140938 (gapB P8 / M6 0.11);</item>
/// <item>744-frame chunks with a persistent partial count flushed by the empty-input tail (A16/C10). The output count for N input frames is
/// <c>ceil(65536 * (N-1)/140938)</c>, from the mono float kernel's phase walk (C38.2 P1-18: 1024 input frames give 476, 12288 give 5714).</item>
/// <item>the process-functor gate (<c>[core+0x90] == 0</c> returns without touching the buffer) and the bus buffer's valid count going to 0 (B-07).</item>
/// </list>
/// The addresses are the rows' native citations.
/// </summary>
public class WwiseHijackPluginTests
{
    private static WwiseResamplerFormat FloatMono(int rate) =>
        new(WwiseResampler.FormatFloat, 1, rate);

    /// <summary>The Hijack parameter object Init's fourth argument carries (<c>[4]</c> = the bus number): the bank's four FxCustom blocks hold 1..4.</summary>
    private static WwiseHijackParams Param(uint n)
    {
        var p = WwiseHijackParams.Create(new WwisePluginAllocator())!;
        p.SetParamsBlockOrDefaults(BitConverter.GetBytes(n), 4);
        return p;
    }

    /// <summary>The bus buffer as the engine hands it to the Hijack: planar float data, u16 max and valid frames (<c>0x8DBFE8</c>'s <c>r1</c>).</summary>
    private static WwiseDecodeState Bus(int frames, int channels = 1)
        => new() { Data = new float[Math.Max(frames, 1) * channels], ChannelConfig = (uint)channels, MaxFrames = (ushort)Math.Max(frames, 1), ValidFrames = (ushort)frames };

    /// <summary>Collects the chunk lengths and buffers the process callback receives (A16).</summary>
    private sealed class Chunks
    {
        public List<int> Lengths { get; } = new();
        public List<float[]> Buffers { get; } = new();
        public void Receive(uint value, float[] buffer, int count)
        {
            Lengths.Add(count);
            Buffers.Add(buffer);
        }
    }

    // ---------------------------------------------------------------- R3 static registration

    /// <summary>
    /// M6-015 / gapB R3 (0x004DD90C): the static registration is <c>{type 3, company 0x12C, id 1,
    /// create 0x008DBC71, params 0x008DBD11}</c> and is prepended to <c>g_pAKPluginList</c>.
    /// </summary>
    [Fact]
    public void TheStaticRegistrationHeadsThePluginList()
    {
        var registration = WwiseHijackPlugin.StaticRegistration;
        Assert.Equal(3, registration.Type);
        Assert.Equal(0x12C, registration.Company);
        Assert.Equal(1, registration.Id);
        Assert.Equal(0x008DBC71u, registration.Create);
        Assert.Equal(0x008DBD11u, registration.Params);

        Assert.NotEmpty(WwiseHijackPlugin.RegisteredPlugins);
        Assert.Equal(registration, WwiseHijackPlugin.RegisteredPlugins[0]);   // prepended
    }

    // ---------------------------------------------------------------- R2/R5 last caller wins

    /// <summary>
    /// M6-015 / gapB R2, R5 (RegisterPlugin 0x008DB300, SetupPlugins 0x005942C6..0x00594354): RegisterPlugin
    /// is called four times and the last caller wins, so a created FX is bound to plug-in B's callbacks, not
    /// plug-in A's.
    /// </summary>
    [Fact]
    public void TheLastRegisteredPluginBindsTheCreatedFx()
    {
        int aCreates = 0, bCreates = 0;
        var a = new WwiseHijackPlugin(createCallback: _ => aCreates++);
        var b = new WwiseHijackPlugin(createCallback: _ => bCreates++);

        WwiseHijackPlugin.RegisterPlugin(a);
        WwiseHijackPlugin.RegisterPlugin(b);              // R2/R5: B is the live registration
        Assert.Same(b, WwiseHijackPlugin.Registered);

        var fx = WwiseHijackPlugin.Create();              // R4: Create calls the global it(fx)
        fx.Init(FloatMono(48000), Param(1));

        Assert.Equal(0, aCreates);                        // A's setup did not survive B's
        Assert.Equal(1, bCreates);
    }

    // ---------------------------------------------------------------- A15 init

    /// <summary>
    /// M6-015 / A15 (0x008DBD74, 0x008DBF76): Init reads <c>numChannels</c> from <c>byte [fmt+4]</c> and allocates <c>numChannels * 4096</c>
    /// bytes, i.e. 1,024 floats per channel (one block). A channel count of 0 returns 2 (<c>0x8DBF80 cbz r0, 0x8DBFBE</c>), and the create
    /// callback still fires (<c>0x8DBD9A..0x8DBDA6</c>: it does not test the result).
    /// </summary>
    [Fact]
    public void InitAllocatesOneThousandTwentyFourFloatsPerChannel()
    {
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin());

        var mono = WwiseHijackPlugin.Create();
        Assert.Equal(1, mono.Init(FloatMono(48000), Param(1)));
        Assert.Equal(1, mono.Channels);
        Assert.Equal(1024, mono.OutputData!.Length);

        var stereo = WwiseHijackPlugin.Create();
        stereo.Init(new WwiseResamplerFormat(WwiseResampler.FormatFloat, 2, 48000), Param(2));
        Assert.Equal(2, stereo.Channels);
        Assert.Equal(2048, stereo.OutputData!.Length);

        int creates = 0;
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(createCallback: _ => creates++));
        var none = WwiseHijackPlugin.Create();
        Assert.Equal(2, none.Init(new WwiseResamplerFormat(WwiseResampler.FormatFloat, 0, 48000), Param(1)));
        Assert.False(none.Initialized);
        Assert.Equal(1, creates);
    }

    /// <summary>
    /// M6-015 / A15, gapB P8 (fx+0x0C = 22320, 0x008DBD74): Init starts the CAkResampler at 22,320 Hz and
    /// pitches it to 0 cents. The step is <c>round(48000/22320 * 65536)</c> = 140,938, fixed pitch (mode 1).
    /// </summary>
    [Fact]
    public void InitRunsTheResamplerAt22320WithZeroPitch()
    {
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin());
        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000), Param(1));

        Assert.Equal(22320, fx.OutputRateHz);
        Assert.Equal(744, fx.ChunkSize);
        Assert.Equal(0x4009A269u, BitConverter.SingleToUInt32Bits(fx.Resampler.Ratio4C));   // C38.2: the ratio bits
        Assert.Equal(140938u, fx.Resampler.TargetStep);   // gapB P8's value
        Assert.Equal(1, fx.Resampler.Mode);               // fixed pitch, not bypass (step != 0x10000)
        Assert.Equal(48000u, fx.InputRate10);             // 0x8DBF70 [core+0x10] = [fmt]
    }

    // ---------------------------------------------------------------- A15/A16/A18 callbacks

    /// <summary>
    /// M6-015 / A15, A16, A18: the registered callbacks are the effect's create, process and destroy. Init
    /// fires the create (PrepareAudioBuffer), each chunk fires the process (UpdateBuffer), and Term fires the
    /// destroy (CloseAudioBuffer).
    /// </summary>
    [Fact]
    public void InitProcessAndTermFireTheRegisteredCallbacks()
    {
        var calls = new List<string>();
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(
            createCallback: _ => calls.Add("prepareAudioBuffer"),
            processCallback: (_, _, _) => calls.Add("updateBuffer"),
            destroyCallback: _ => calls.Add("closeAudioBuffer")));

        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000), Param(1));
        fx.Execute(Bus(1601));                            // one 744-frame chunk
        fx.Term();

        Assert.Equal(new[] { "prepareAudioBuffer", "updateBuffer", "closeAudioBuffer" }, calls);
    }

    // ---------------------------------------------------------------- A16 execute / chunking

    /// <summary>
    /// M6-015 / A16 (0x008DBFE8): an input whose resampled output is exactly 744 frames fires the process
    /// callback once with 744. <c>ceil(65536 * 1600/140938) = ceil(743.9977) = 744</c>.
    /// </summary>
    [Fact]
    public void ExecuteEmitsA744FrameChunk()
    {
        var chunks = new Chunks();
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(processCallback: chunks.Receive));
        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000), Param(1));

        fx.Execute(Bus(1601));

        Assert.Equal(new[] { 744 }, chunks.Lengths);
        Assert.Equal(744, chunks.Buffers[0].Length);
    }

    /// <summary>
    /// M6-015 / A16, C10: an input whose resampled output is 1,000 frames fires one full 744-frame chunk
    /// and retains 256 frames in core+0x7A. The empty-input tail (D2.8) then flushes those 256.
    /// </summary>
    [Fact]
    public void ExecuteAccumulatesThePartialUntilTheEmptyInputTail()
    {
        var chunks = new Chunks();
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(processCallback: chunks.Receive));
        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000), Param(1));

        fx.Execute(Bus(2151));

        Assert.Equal(new[] { 744 }, chunks.Lengths);
        Assert.Equal(744, chunks.Buffers[0].Length);
        fx.Execute(Bus(0));
        Assert.Equal(new[] { 744, 256 }, chunks.Lengths);
        Assert.Equal(256, chunks.Buffers[1].Length);
    }

    /// <summary>
    /// M6-015 / C10 (0x008DBFE8..0x008DC034): core+0x7A survives engine frames. Two calls whose
    /// resampled results total one chunk emit only when the second call completes 744 frames.
    /// </summary>
    [Fact]
    public void ExecuteAccumulatesAcrossCallsUntilAFullChunk()
    {
        var chunks = new Chunks();
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(processCallback: chunks.Receive));
        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000), Param(1));

        fx.Execute(Bus(801));
        Assert.Empty(chunks.Lengths);
        fx.Execute(Bus(801));

        Assert.Equal(new[] { 744 }, chunks.Lengths);
    }

    /// <summary>
    /// M6-015 / A16, D2.8: an empty input is the NoMoreData (17) case; the process callback still fires, with
    /// 0 frames, because UpdateBuffer accepts a zero-length chunk.
    /// </summary>
    [Fact]
    public void ExecuteOnEmptyInputFlushesZeroFrames()
    {
        var chunks = new Chunks();
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(processCallback: chunks.Receive));
        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000), Param(1));

        fx.Execute(Bus(0));

        Assert.Equal(new[] { 0 }, chunks.Lengths);
        Assert.Empty(chunks.Buffers[0]);
    }

    /// <summary>
    /// M6-015 / A16: Execute needs the resampler Init and SetPitch that Init performs; the constructor leaves
    /// R+0x54..+0x56 unset (0xA46D70), so Execute before Init is a visible stop, not a run with a garbage kernel type.
    /// </summary>
    [Fact]
    public void ExecuteBeforeInitIsRefused()
    {
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(processCallback: (_, _, _) => { }));
        var fx = WwiseHijackPlugin.Create();
        Assert.Throws<WwiseMissingBehaviourException>(() => fx.Execute(Bus(744)));
    }

    /// <summary>
    /// M6-015 (C38.2, B-07, 0x8DBFF2..0x8DBFF6): <c>ldr r0,[r5,#0x90]; cbz r0, 0x8DC03A</c>: with no process functor Execute returns without touching the buffer.
    /// </summary>
    [Fact]
    public void ExecuteWithNoProcessCallbackReturnsWithoutTouchingTheBuffer()
    {
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin());
        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000), Param(1));
        var bus = Bus(1024);
        fx.Execute(bus);
        Assert.Equal(1024, bus.ValidFrames);
        Assert.Equal(0u, fx.Resampler.OutputOffset28);
    }

    /// <summary>
    /// M6-015 (C38.2, B-07): the resampler consumes the whole input, so the bus buffer's valid count is 0 after the Hijack (the samples are untouched).
    /// The 1024 input frames of a bus pass give 476 outputs (P1-18): a partial chunk, no delivery.
    /// </summary>
    [Fact]
    public void TheBusBufferHasNoValidFramesAfterTheHijack()
    {
        var chunks = new Chunks();
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(processCallback: chunks.Receive));
        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000), Param(1));
        var bus = Bus(1024);
        for (int i = 0; i < 1024; i++) ((float[])bus.Data!)[i] = i;
        fx.Execute(bus);
        Assert.Equal(0, bus.ValidFrames);
        Assert.Equal(Enumerable.Range(0, 1024).Select(i => (float)i), (float[])bus.Data!);
        Assert.Empty(chunks.Lengths);
        Assert.Equal(476, fx.OutputBuffer.ValidFrames);
    }

    /// <summary>
    /// M6-015 / M6-004 (C38.2 P1-18, the verifier's engine run with the real Thumb Execute): 1024-frame bus buffers at 48000 Hz into 22320 Hz give 476 output frames per buffer (<c>ceil(1023 * 65536 / 140938)</c>), so the pending count after each of the first 12
    /// buffers is 476, 208 (one delivery), 685, 417 (one), 149 (one), 625, 357 (one), 89 (one), 565, 298 (one), 30 (one), 506; 12288 input frames give 5714 outputs; 40 buffers give 25 deliveries, every one of 744 frames.
    /// </summary>
    [Fact]
    public void TheHijackPendingCountsAndDeliveriesFollowTheEngineRun()
    {
        var chunks = new Chunks();
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(processCallback: chunks.Receive));
        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000), Param(1));
        var expected = new[] { 476, 208, 685, 417, 149, 625, 357, 89, 565, 298, 30, 506 };
        var deliveriesAfter = new[] { 0, 1, 1, 2, 3, 3, 4, 5, 5, 6, 7, 7 };
        for (int k = 0; k < 12; k++)
        {
            fx.Execute(Bus(1024));
            Assert.Equal(expected[k], fx.OutputBuffer.ValidFrames);
            Assert.Equal(deliveriesAfter[k], chunks.Lengths.Count);
        }
        Assert.Equal(5714, 744 * chunks.Lengths.Count + fx.OutputBuffer.ValidFrames);                      // 12288 input frames
        for (int k = 12; k < 40; k++) fx.Execute(Bus(1024));
        Assert.Equal(25, chunks.Lengths.Count);
        Assert.All(chunks.Lengths, n => Assert.Equal(744, n));
    }

    /// <summary>
    /// M6-015 / M6-004 (C38.2, B-06, B-07, P1-18): the Hijack core under the real Thumb Init <c>0x8DBF76</c> and Execute <c>0x8DBFE8</c> (Unicorn, <c>emu_pitch.py</c>): 400 lives of 1..8 Execute calls on 1..2 channel float buses (0 channels fail the init
    /// with 2), 48000 / 44100 / 32000 Hz into 22320 Hz with chunks of 744: the deliveries (count and a hash of the delivered frames: a partial count on an empty input, also 0), the bus buffer's valid count, the out buffer's valid count and the
    /// resampler's state. With the process functor unset (about 1 in 12) Execute returns without touching the buffer.
    /// </summary>
    [Fact]
    public void TheHijackMatchesTheEnginesThumbCodeOn400Lives()
    {
        Assert.True(WwisePitchOracle.Hijack.Length >= 400);
        foreach (var row in WwisePitchOracle.Hijack)
        {
            var parts = row.Split(" | ");
            var h = parts[0].Split(' ');
            int rate = int.Parse(h[0]), ch = int.Parse(h[1]);
            bool gate = h[2] == "1";
            var deliveries = new List<string>();
            WwiseHijackPlugin.RegisterPlugin(gate
                ? new WwiseHijackPlugin(processCallback: (_, chunk, count) => deliveries.Add(count == 0 ? "0:-" : $"{count}:{WwiseResamplerTests.Sha(MemoryMarshal.AsBytes(chunk.AsSpan(0, count)))}"))
                : new WwiseHijackPlugin());
            var fx = WwiseHijackPlugin.Create();
            uint chWord = ch == 0 ? 0u : ch == 2 ? 0x3102u : 0x4101u;
            int rc = fx.Init(new WwiseResamplerFormat(WwiseResamplerTests.FmtWord(true, Math.Max(ch, 1)), ch, rate, chWord), Param((uint)(1 + ch)));
            Assert.Equal(parts[1], $"init={rc}");
            if (rc != 1)
            {
                Assert.Equal(parts[2], WwiseResamplerTests.Snap(fx.Resampler));
                continue;
            }
            var calls = parts[2].Split(" ;; ");
            Assert.Equal(int.Parse(h[3]), calls.Length);
            for (int k = 0; k < calls.Length; k++)
            {
                var c = calls[k].Split(' ');
                var vs = c[0].Split('.');
                int valid = int.Parse(vs[0]);
                uint seedk = uint.Parse(vs[1]);
                var d = new Xs(seedk);
                var data = new float[1024 * ch];
                for (int i = 0; i < data.Length; i++) data[i] = d.F32();
                var bus = new WwiseDecodeState { Data = data, ChannelConfig = chWord, Scratch08 = 0x2D, MaxFrames = 1024, ValidFrames = (ushort)valid };
                deliveries.Clear();
                fx.Execute(bus);
                string got = $"{(deliveries.Count == 0 ? "-" : string.Join(",", deliveries))} {bus.ValidFrames:X4} {fx.OutputBuffer.ValidFrames:X4} {WwiseResamplerTests.Snap(fx.Resampler)}";
                string expected = string.Join(" ", c.Skip(1));
                Assert.True(expected == got, $"{parts[0]} call {k}: engine [{expected}] C# [{got}]");
            }
        }
    }

    // ---------------------------------------------------------------- C45.5 the parameter object and the callback value

    /// <summary>
    /// M6-015 / C45.5 (research bus-fx-17 5.2, 5.3): Init stores its fourth argument at <c>[obj+0xA0]</c> and the three user callbacks receive <c>[[obj+0xA0]+4]</c> (the bus number 1..4) as their first argument; the process callback also gets (data, count). The value is read at the time of each call
    /// (the trampolines <c>0x8DBAA2</c>, <c>0x8DBB5E</c>, <c>0x8DBC36</c> load it then), so a SetParam between the calls shows.
    /// </summary>
    [Fact]
    public void TheCallbacksReceiveTheBusNumberOfTheParameterObjectAtCallTime()
    {
        var seen = new List<string>();
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(
            createCallback: n => seen.Add($"create {n}"),
            processCallback: (n, data, count) => seen.Add($"process {n} {count} {data.Length}"),
            destroyCallback: n => seen.Add($"destroy {n}")));
        var param = Param(3);
        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000), param);
        Assert.Same(param, fx.Param);
        fx.Execute(Bus(1601));
        Assert.Equal(1, param.SetParam(0, BitConverter.GetBytes(4u)));            // read at the time of the call: the destroy callback sees 4
        fx.Term();
        Assert.Equal(new[] { "create 3", "process 3 744 744", "destroy 4" }, seen);
    }

    /// <summary>
    /// M6-015 / C45.5: the four Robot_Bus customs carry the bus number 1..4 (Init.bnk: 0x18955E1F, 0x0FA2C884, 0x397369E7, 0x04FC11BD). A callback reached with no parameter object faults in the engine (the trampoline dereferences <c>[obj+0xA0]</c> = 0).
    /// </summary>
    [Fact]
    public void ACallbackWithNoParameterObjectStopsLikeTheEnginesNullDereference()
    {
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(createCallback: _ => { }));
        var fx = WwiseHijackPlugin.Create();
        Assert.Throws<InvalidOperationException>(() => fx.Init(FloatMono(48000), null));
        // with no callback bound there is nothing to dereference
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin());
        var quiet = WwiseHijackPlugin.Create();
        Assert.Equal(1, quiet.Init(FloatMono(48000), null));
        Assert.Null(quiet.Param);
    }

    /// <summary>
    /// M6-015 / C45.5, verifier V1: the core constructor <c>0x8DBF44</c> stores <c>[core+4] = 0</c> and <c>u16 [core+8] = 0</c> (<c>0x8DBF4C movs r5,#0; 0x8DBF4E strd r1,r5,[r4]; 0x8DBF52 strh r5,[r4,#8]</c>): a created effect has no output rate and no chunk size until the SetupEnginePlugInFx functor
    /// (a seam: its veneer <c>0xAE3090</c> is unread) writes them.
    /// </summary>
    [Fact]
    public void ACreatedEffectHasNoOutputRateOrChunkSizeUntilTheSetupFunctorBindsThem()
    {
        var fx = new WwiseHijackFx();
        Assert.Equal(0, fx.OutputRateHz);
        Assert.Equal(0, fx.ChunkSize);
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin());
        var bound = WwiseHijackPlugin.Create();
        Assert.Equal(22320, bound.OutputRateHz);       // rows A14/A25 (the seam's values)
        Assert.Equal(744, bound.ChunkSize);
    }

    /// <summary>
    /// M6-015 / research 2.9 (Thumb <c>0x8DBD10</c>, <c>0x8DC090..0x8DC110</c>): the parameter object is 8 bytes; its constructor leaves <c>[4] = 0</c>; SetParam answers 1 for every id and stores only id 0; the wrapper <c>vt+0x10</c> with size 0 returns 1 and leaves <c>[4]</c>, otherwise it reads 4 bytes of the block whatever
    /// the size says; Clone copies <c>[4]</c>; a failing allocation gives null.
    /// </summary>
    [Fact]
    public void TheHijackParameterObjectIsEightBytesAndStoresOnlyIdZero()
    {
        var alloc = new WwisePluginAllocator();
        var p = WwiseHijackParams.Create(alloc)!;
        Assert.Equal(new[] { 8 }, alloc.Sizes);
        Assert.Equal(0u, p.Value);
        Assert.Equal(1, p.SetParamsBlockOrDefaults(null, 0));
        Assert.Equal(0u, p.Value);
        Assert.Equal(1, p.SetParam(0, BitConverter.GetBytes(2u)));
        Assert.Equal(2u, p.Value);
        Assert.Equal(1, p.SetParam(1, BitConverter.GetBytes(9u)));
        Assert.Equal(1, p.SetParam(0xFFFFFFFF, BitConverter.GetBytes(9u)));
        Assert.Equal(2u, p.Value);
        Assert.Equal(1, p.SetParamsBlockOrDefaults(new byte[] { 4, 0, 0, 0 }, 4));
        Assert.Equal(4u, p.Value);
        Assert.Equal(1, p.SetParamsBlockOrDefaults(new byte[] { 1, 0, 0, 0 }, 99));   // the size is unchecked
        Assert.Equal(1u, p.Value);
        var c = p.Clone(alloc)!;
        Assert.Equal(1u, c.Value);
        Assert.Equal(8, alloc.Sizes[^1]);
        Assert.Null(p.Clone(new WwisePluginAllocator { FailAt = 1 }));
        Assert.Null(WwiseHijackParams.Create(new WwisePluginAllocator { FailAt = 1 }));
        Assert.Equal(1, WwiseHijackParams.Destroy(null, alloc));
        Assert.Equal(1, WwiseHijackParams.Destroy(c, alloc));
        Assert.Same(c, alloc.Freed[^1]);
        Assert.Throws<WwiseMissingBehaviourException>(() => p.SetParamsBlockOrDefaults(new byte[2], 2));
    }

    // ---------------------------------------------------------------- the plug-in interface (vtable 0x10387F4)

    /// <summary>
    /// M6-015 / research 5.1: the vtable's slots: +0x14 / +0x18 are 0x8DBF38 / 0x8DBF3C (return 0), +0x24 is 0x8DBF40 (returns 0x2D), Reset (0x8DBE2E thunk -> 0x8DBFC2) returns 1; GetPluginInfo (0x8DBE3A) is named without the words it writes, so it is a required stop.
    /// </summary>
    [Fact]
    public void TheHijackSlotsReturnWhatTheBinaryReturnsAndInfoIsARequiredStop()
    {
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin());
        IWwiseEffectPlugin plug = WwiseHijackPlugin.Create();
        Assert.Equal(0, plug.Slot14());
        Assert.Equal(0, plug.Slot18());
        Assert.Equal(0x2D, plug.Slot24());
        Assert.Equal(1, plug.Reset());
        Assert.Throws<WwiseMissingBehaviourException>(() => plug.GetPluginInfo(out _));
    }

    /// <summary>
    /// M6-015 / research 5.2, 5.4, 5.5: Init through the interface allocates <c>channels &lt;&lt; 12</c> bytes (1,024 floats per channel), stores the parameter object, runs the create callback with the bus number; Term(alloc) runs the destroy callback, frees the block and then the object and returns 1.
    /// The format word <c>u16 [fmt+8]</c> reaches the resampler (float, 1 channel: 0x120).
    /// </summary>
    [Fact]
    public void InitAndTermThroughTheInterfaceAllocateAndFreeTheBlocks()
    {
        var calls = new List<string>();
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(createCallback: n => calls.Add($"create {n}"), destroyCallback: n => calls.Add($"destroy {n}")));
        var alloc = new WwisePluginAllocator();
        var param = Param(2);
        IWwiseEffectPlugin plug = WwiseHijackPlugin.Create();
        var fx = (WwiseHijackFx)plug;
        Assert.Equal(1, plug.Init(alloc, null, param, new WwiseEffectFormat(48000, 0x4101, WwiseResamplerTests.FmtWord(true, 1))));
        Assert.Equal(new[] { 1 << 12 }, alloc.Sizes);
        Assert.Equal(1024, fx.OutputData!.Length);
        Assert.Equal(1, fx.Channels);
        Assert.Equal(48000u, fx.InputRate10);
        Assert.Equal(1, plug.Term(alloc));
        Assert.Equal(new[] { "create 2", "destroy 2" }, calls);
        Assert.Equal(2, alloc.Freed.Count);
        Assert.Same(fx, alloc.Freed[^1]);
        Assert.Null(fx.OutputData);

        // a channel count of 0 fails with 2 without allocating (0x8DBF80 cbz r0); an allocation the engine does not check is a visible stop
        var alloc2 = new WwisePluginAllocator();
        var zero = WwiseHijackPlugin.Create();
        Assert.Equal(2, zero.Init(alloc2, null, param, new WwiseEffectFormat(48000, 0x0000, WwiseResamplerTests.FmtWord(true, 1))));
        Assert.Empty(alloc2.Sizes);
        var failing = new WwisePluginAllocator { FailAt = 1 };
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseHijackPlugin.Create().Init(failing, null, param, new WwiseEffectFormat(48000, 0x4101, WwiseResamplerTests.FmtWord(true, 1))));
    }
}
