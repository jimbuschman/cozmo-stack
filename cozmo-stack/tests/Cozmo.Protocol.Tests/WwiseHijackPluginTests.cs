using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-015 / the rows M6 A13..A18 and gapB R1..R5: the Anki Hijack plug-in.
///
/// Every expected value below is the row's own value or arithmetic applied by hand, never a value read
/// back from <see cref="WwiseHijackPlugin"/>:
/// <list type="bullet">
/// <item>the static registration <c>{type 3, company 0x12C, id 1, create 0x008DBC71, params 0x008DBD11}</c>
/// (R3, <c>0x004DD90C</c>);</item>
/// <item>1,024 floats per channel and Init(fmt, 22320) then SetPitch(0) (A15);</item>
/// <item>the 48 kHz→22,320 Hz step <c>round(48000/22320·65536)</c> = 140938 (gapB P8 / M6 0.11);</item>
/// <item>744-frame chunks and a partial final chunk (A16). The output count for N input frames is
/// <c>ceil(65536·(N−1)/140938)</c>, from the mono float kernel's phase walk: output k needs input index
/// <c>((0x10000 + k·step)&gt;&gt;16)−1</c> plus its next sample, i.e. <c>k·step &lt; 65536·(N−1)</c>
/// (M6 0.11, 0x00A49E40).</item>
/// </list>
/// The addresses are the rows' native citations.
/// </summary>
public class WwiseHijackPluginTests
{
    private static WwiseResamplerFormat FloatMono(int rate) =>
        new(WwiseResampler.FormatFloat, 1, rate);

    /// <summary>Collects the chunk lengths and buffers the process callback receives (A16).</summary>
    private sealed class Chunks
    {
        public List<int> Lengths { get; } = new();
        public List<float[]> Buffers { get; } = new();
        public void Receive(WwiseHijackFx _, float[] buffer, int count)
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
        fx.Init(FloatMono(48000));

        Assert.Equal(0, aCreates);                        // A's setup did not survive B's
        Assert.Equal(1, bCreates);
    }

    // ---------------------------------------------------------------- A15 init

    /// <summary>
    /// M6-015 / A15 (0x008DBD74): Init reads <c>numChannels</c> from the format's channel-config byte and
    /// allocates <c>numChannels · 4096</c> bytes, i.e. 1,024 floats per channel. A channel count of 0 fails
    /// the native init with error 2.
    /// </summary>
    [Fact]
    public void InitAllocatesOneThousandTwentyFourFloatsPerChannel()
    {
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin());

        var mono = WwiseHijackPlugin.Create();
        mono.Init(FloatMono(48000));
        Assert.Equal(1, mono.Channels);
        Assert.Single(mono.ChannelBuffers);
        Assert.Equal(1024, mono.ChannelBuffers[0].Length);

        var stereo = WwiseHijackPlugin.Create();
        stereo.Init(new WwiseResamplerFormat(WwiseResampler.FormatFloat, 2, 48000));
        Assert.Equal(2, stereo.Channels);
        Assert.Equal(2, stereo.ChannelBuffers.Length);
        Assert.All(stereo.ChannelBuffers, buffer => Assert.Equal(1024, buffer.Length));

        var none = WwiseHijackPlugin.Create();
        Assert.Throws<NotSupportedException>(() =>
            none.Init(new WwiseResamplerFormat(WwiseResampler.FormatFloat, 0, 48000)));
        Assert.False(none.Initialized);
    }

    /// <summary>
    /// M6-015 / A15, gapB P8 (fx+0x0C = 22320, 0x008DBD74): Init starts the CAkResampler at 22,320 Hz and
    /// pitches it to 0 cents. The step is <c>round(48000/22320·65536)</c> = 140,938, fixed pitch (mode 1).
    /// </summary>
    [Fact]
    public void InitRunsTheResamplerAt22320WithZeroPitch()
    {
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin());
        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000));

        Assert.Equal(22320, fx.OutputRateHz);
        Assert.Equal(744, fx.ChunkSize);
        Assert.Equal(48000f / 22320f, fx.Resampler.Ratio);
        Assert.Equal(140938u, fx.Resampler.TargetStep);   // gapB P8's value, recomputed by hand
        Assert.Equal(1, fx.Resampler.Mode);               // fixed pitch, not bypass (step != 0x10000)
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
        fx.Init(FloatMono(48000));
        fx.Execute(new float[1601]);                      // one 744-frame chunk
        fx.Term();

        Assert.Equal(new[] { "prepareAudioBuffer", "updateBuffer", "closeAudioBuffer" }, calls);
    }

    // ---------------------------------------------------------------- A16 execute / chunking

    /// <summary>
    /// M6-015 / A16 (0x008DBFE8): an input whose resampled output is exactly 744 frames fires the process
    /// callback once with 744. <c>ceil(65536·1600/140938) = ceil(743.9977) = 744</c>.
    /// </summary>
    [Fact]
    public void ExecuteEmitsA744FrameChunk()
    {
        var chunks = new Chunks();
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(processCallback: chunks.Receive));
        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000));

        fx.Execute(new float[1601]);

        Assert.Equal(new[] { 744 }, chunks.Lengths);
        Assert.Equal(744, chunks.Buffers[0].Length);
    }

    /// <summary>
    /// M6-015 / A16, D2.8: an input whose resampled output is 1,000 frames fires the process callback with a
    /// full 744-frame chunk and a 256-frame partial final chunk. <c>ceil(65536·2150/140938) = ceil(999.7475)
    /// = 1000 = 744 + 256</c>.
    /// </summary>
    [Fact]
    public void ExecuteEmitsThePartialFinalChunk()
    {
        var chunks = new Chunks();
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin(processCallback: chunks.Receive));
        var fx = WwiseHijackPlugin.Create();
        fx.Init(FloatMono(48000));

        fx.Execute(new float[2151]);

        Assert.Equal(new[] { 744, 256 }, chunks.Lengths);
        Assert.Equal(744, chunks.Buffers[0].Length);
        Assert.Equal(256, chunks.Buffers[1].Length);
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
        fx.Init(FloatMono(48000));

        fx.Execute(ReadOnlySpan<float>.Empty);

        Assert.Equal(new[] { 0 }, chunks.Lengths);
        Assert.Empty(chunks.Buffers[0]);
    }

    /// <summary>
    /// M6-015 / A16: Execute needs the resampler Init and SetPitch that Init performs; before that the FX is
    /// not brought up and Execute refuses rather than running an uninitialised resampler.
    /// </summary>
    [Fact]
    public void ExecuteBeforeInitIsRefused()
    {
        WwiseHijackPlugin.RegisterPlugin(new WwiseHijackPlugin());
        var fx = WwiseHijackPlugin.Create();
        Assert.Throws<InvalidOperationException>(() => fx.Execute(new float[744]));
    }
}
