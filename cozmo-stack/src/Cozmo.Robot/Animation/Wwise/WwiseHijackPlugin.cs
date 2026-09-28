// fidelity: M6-015
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The static plug-in registration the ARM constructor <c>0x004DD90C</c> prepends to the global
/// <c>g_pAKPluginList</c> (M6-015, gapB R3): <c>{type 3, company 0x12C, id 1, create 0x008DBC71,
/// params 0x008DBD11}</c>. The two native entry points cannot be executed here, so the record's values are
/// carried as data; <see cref="WwiseHijackPlugin.Create"/> is the local create the runtime reaches by that
/// address.
/// </summary>
public readonly record struct WwiseHijackRegistration(int Type, int Company, int Id, uint Create, uint Params);

/// <summary>
/// The Anki Hijack plug-in (M6-015): the effect last in the <c>Robot_Bus_1</c> chain. It resamples the mono
/// float Wwise mix to 22,320 Hz with the runtime's own <see cref="WwiseResampler"/> (M6-004) and hands the
/// robot 744-frame chunks through the registered process callback.
///
/// <para><b>Registration.</b> The ARM static constructor <c>0x004DD90C</c> prepends
/// <see cref="StaticRegistration"/> to the global plug-in list (gapB R3). Separately,
/// <see cref="RegisterPlugin"/> swaps the instance into the global <c>std::function 0x0108D110</c>
/// (gapB R2, <c>0x008DB300</c>); the last caller wins, so on the shipped path it is plug-in B's
/// CozmoAudioController callbacks (gapB R5, <c>0x005942C6..0x00594354</c>). <see cref="Create"/> is the
/// HijackFx create <c>0x008DBC70</c> (gapB R4): it allocates the FX object and, if a plug-in is
/// registered, calls it to bind the 22,320 Hz rate, the 744-frame chunk and the create/process/destroy
/// callbacks (SetupEnginePlugInFx, A14).</para>
///
/// <para><b>Callbacks are caller inputs.</b> The rows settle that Init fires the create callback, Execute
/// fires the process callback per chunk and Term fires the destroy callback; what those callbacks do
/// (PrepareAudioBuffer <c>0x005984E8</c>, UpdateBuffer <c>0x005985FC</c>, CloseAudioBuffer
/// <c>0x0059878C</c>) lives outside this record. They are supplied by the caller and never invented
/// here.</para>
///
/// <para><b>Not wired.</b> This is a standalone class; it is not yet part of <c>WwisePlayback</c>,
/// <c>WwiseAudioSource</c>, <c>WwiseSongRenderer</c> or <c>AnimationScheduler</c>.</para>
/// </summary>
public sealed class WwiseHijackPlugin
{
    /// <summary>
    /// <c>fx+0x0C</c>: the output rate SetupEnginePlugInFx stores (A14), fixed at 22,320 Hz by SetupPlugins
    /// (A25, 0x005942C6). The same 22,320 Hz/744 frames the robot endpoint uses (C3).
    /// </summary>
    public const int OutputSampleRateHz = 22320;

    /// <summary><c>fx+0x10</c>: the chunk size SetupEnginePlugInFx stores (A14), 744 frames (A16, C3).</summary>
    public const int ChunkFrames = 744;

    /// <summary>A15: <c>numChannels · 4096</c> bytes, i.e. 1,024 floats, are allocated per channel.</summary>
    public const int FloatsPerChannel = 1024;

    /// <summary>gapB R3: the registration <c>0x004DD90C</c> puts at the head of <c>g_pAKPluginList</c>.</summary>
    public static WwiseHijackRegistration StaticRegistration { get; } =
        new(Type: 3, Company: 0x12C, Id: 1, Create: 0x008DBC71, Params: 0x008DBD11);

    private static readonly List<WwiseHijackRegistration> PluginList = new();

    /// <summary><c>g_pAKPluginList</c>: the static registration is prepended to it (gapB R3).</summary>
    public static IReadOnlyList<WwiseHijackRegistration> RegisteredPlugins => PluginList;

    static WwiseHijackPlugin() => PluginList.Insert(0, StaticRegistration);

    // The global std::function 0x0108D110 (gapB R2). RegisterPlugin swaps this; the last caller wins.
    private static WwiseHijackPlugin? _registered;

    /// <summary>gapB R2: the plug-in left in the global <c>std::function</c>, i.e. the last registered.</summary>
    public static WwiseHijackPlugin? Registered => _registered;

    /// <summary>
    /// RegisterPlugin <c>0x008DB300</c> (gapB R2): swaps the calling plug-in's setup lambda into the global
    /// <c>std::function 0x0108D110</c>. There are four callers in SetupPlugins (A25, gapB R5); the last,
    /// plug-in B's third <c>Set*Callback</c>, is what remains.
    /// </summary>
    public static void RegisterPlugin(WwiseHijackPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        _registered = plugin;                              // R2: the last caller wins
    }

    /// <summary>SetupEnginePlugInFx binds the create callback (the CozmoAudioController lambda → PrepareAudioBuffer).</summary>
    public Action<WwiseHijackFx>? CreateCallback { get; }

    /// <summary>SetupEnginePlugInFx binds the process callback (the lambda → RobotAudioBuffer::UpdateBuffer).</summary>
    public Action<WwiseHijackFx, float[], int>? ProcessCallback { get; }

    /// <summary>SetupEnginePlugInFx binds the destroy callback (the lambda → CloseAudioBuffer).</summary>
    public Action<WwiseHijackFx>? DestroyCallback { get; }

    /// <summary>
    /// Builds a plug-in holding the callbacks SetupEnginePlugInFx will bind (A14, gapB R5). SetupPlugins
    /// constructs each with the 22,320 Hz rate and 744-frame chunk; those are the row values and are not
    /// constructor inputs.
    /// </summary>
    public WwiseHijackPlugin(
        Action<WwiseHijackFx>? createCallback = null,
        Action<WwiseHijackFx, float[], int>? processCallback = null,
        Action<WwiseHijackFx>? destroyCallback = null)
    {
        CreateCallback = createCallback;
        ProcessCallback = processCallback;
        DestroyCallback = destroyCallback;
    }

    /// <summary>
    /// HijackFx create <c>0x008DBC70</c> (gapB R4): allocate the FX object and run its ctor, then, if the
    /// global is set, call it with the object (the registering plug-in's SetupEnginePlugInFx) to bind the
    /// rate, the chunk and the three callbacks.
    /// </summary>
    public static WwiseHijackFx Create()
    {
        var fx = new WwiseHijackFx();
        _registered?.Bind(fx);                             // R4: if the global is set, call it(fx)
        return fx;
    }

    /// <summary>SetupEnginePlugInFx(this, fx) through the lambda's operator() <c>0x008DB94A</c> (A14, gapB R4).</summary>
    internal void Bind(WwiseHijackFx fx)
    {
        fx.OutputRateHz = OutputSampleRateHz;
        fx.ChunkSize = ChunkFrames;
        fx.CreateCallback = CreateCallback;
        fx.ProcessCallback = ProcessCallback;
        fx.DestroyCallback = DestroyCallback;
    }
}

/// <summary>
/// One created Hijack effect instance (M6-015): the <c>0xD8</c>-byte object <c>0x008DBC70</c> allocates
/// and <see cref="Init"/> brings up.
///
/// <para>Init <c>0x008DBD74</c> (A15) allocates 1,024 floats per channel from the format's channel count,
/// then <c>CAkResampler::Init(fmt, 22320)</c> and <c>SetPitch(0)</c>, then fires the create callback
/// (PrepareAudioBuffer). Execute <c>0x008DBFE8</c> (A16) resamples the caller's mono float mix to 744-frame
/// chunks and fires the process callback per chunk (UpdateBuffer). Term <c>0x008DBDF8</c> (A18) fires the
/// destroy callback (CloseAudioBuffer).</para>
///
/// <para>The input float source is the caller's: the class never reads the bank or the bus itself. What the
/// 1,024-float-per-channel allocation is used for is not in the rows, so it is exposed and left untouched.
/// </para>
/// </summary>
public sealed class WwiseHijackFx
{
    internal Action<WwiseHijackFx>? CreateCallback;
    internal Action<WwiseHijackFx, float[], int>? ProcessCallback;
    internal Action<WwiseHijackFx>? DestroyCallback;

    private readonly WwiseResampler _resampler = new();
    private float[] _output = Array.Empty<float>();
    private int _validFrames;

    internal WwiseHijackFx() { }

    /// <summary><c>fx+0x0C</c> (A14/A15): the resampler's output rate, bound by the registering plug-in.</summary>
    public int OutputRateHz { get; internal set; }

    /// <summary><c>fx+0x10</c> (A14/A16): the output chunk size in frames, bound by the registering plug-in.</summary>
    public int ChunkSize { get; internal set; }

    /// <summary>A15: <c>numChannels</c>, the format's channel-config byte (0 fails the native init with error 2).</summary>
    public int Channels { get; private set; }

    /// <summary>A15: <c>numChannels</c> buffers of 1,024 floats. The rows settle only their size and count.</summary>
    public float[][] ChannelBuffers { get; private set; } = Array.Empty<float[]>();

    /// <summary>Whether <see cref="Init"/> has run.</summary>
    public bool Initialized { get; private set; }

    /// <summary>
    /// The runtime resampler started at Init (M6-004), exposed for the step/rate read-back the record
    /// settles (Init(fmt, 22320) then SetPitch(0)).
    /// </summary>
    public WwiseResampler Resampler => _resampler;

    /// <summary>
    /// Hijack Init <c>0x008DBD74</c> (A15): read the channel count from the format, allocate 1,024 floats
    /// per channel, <c>CAkResampler::Init(fmt, 22320)</c>, <c>SetPitch(0)</c>, then the create callback →
    /// PrepareAudioBuffer.
    /// </summary>
    /// <exception cref="NotSupportedException">The format's channel count is 0, which the native fails with error 2.</exception>
    public void Init(WwiseResamplerFormat fmt)
    {
        if (fmt.Channels <= 0)
            throw new NotSupportedException("Hijack Init: fmt channel count 0 fails with error 2 (M6-015 A15)");

        Channels = fmt.Channels;
        ChannelBuffers = new float[fmt.Channels][];
        for (int c = 0; c < fmt.Channels; c++)
            ChannelBuffers[c] = new float[WwiseHijackPlugin.FloatsPerChannel];

        _resampler.Init(fmt, OutputRateHz);               // A15: Init(fmt, 22320)
        _resampler.SetPitch(0);                            // A15: SetPitch(0 cents)
        _validFrames = 0;                                  // C10: core+0x7A starts at zero

        Initialized = true;
        CreateCallback?.Invoke(this);                      // A15: create → PrepareAudioBuffer
    }

    /// <summary>
    /// Hijack Execute <c>0x008DBFE8</c> (A16): resample the caller's float input to 22,320 Hz and fire the
    /// process callback for every full 744-frame chunk. C10: the native output buffer and its
    /// <c>uValidFrames</c> at core+0x7A persist across Execute calls; a partial result remains until a later
    /// call completes the chunk. Empty input is <c>NoMoreData</c> (17) and flushes that persistent count,
    /// including zero (A16, D2.8). The input is not modified.
    /// </summary>
    public void Execute(ReadOnlySpan<float> input)
    {
        if (!Initialized)
            throw new InvalidOperationException("Hijack Execute needs Init first (M6-015 A15/A16)");

        if (input.Length == 0)
        {
            // C10: result 17 delivers persistent uValidFrames, then 0x8DC02C resets it.
            Flush(ChannelBuffers[0], 0, _validFrames);
            _validFrames = 0;
            return;
        }

        // One resampler pass consumes the whole input, so size the output for the downsampling ratio
        // (mix rate → 22,320 Hz). The runtime's own buffer would be the caller's; this is scratch.
        float ratio = _resampler.Ratio;
        int capacity = (int)MathF.Ceiling(input.Length / ratio) + 2;
        if (_output.Length < capacity) _output = new float[capacity];

        _resampler.Execute(input, _output, _output.Length);
        int produced = _resampler.LastProducedFrames;      // A16: the validFrames count

        int offset = 0;
        while (offset < produced)
        {
            int copy = Math.Min(ChunkSize - _validFrames, produced - offset);
            Array.Copy(_output, offset, ChannelBuffers[0], _validFrames, copy);
            offset += copy;
            _validFrames += copy;
            if (_validFrames != ChunkSize) continue;

            Flush(ChannelBuffers[0], 0, ChunkSize);        // A16: 0x2D DataReady → 744 frames
            _validFrames = 0;                              // 0x008DC02C..0x008DC030
        }
    }

    /// <summary>Hijack Term <c>0x008DBDF8</c> (A18): the destroy callback → CloseAudioBuffer.</summary>
    public void Term() => DestroyCallback?.Invoke(this);

    private void Flush(float[] buffer, int offset, int count)
    {
        var chunk = new float[count];
        Array.Copy(buffer, offset, chunk, 0, count);
        ProcessCallback?.Invoke(this, chunk, count);       // A16: process callback(fx, outBuf, validFrames)
    }
}
