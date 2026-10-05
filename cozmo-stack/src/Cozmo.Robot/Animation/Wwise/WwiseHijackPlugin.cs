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
/// One created Hijack effect instance (M6-015): the <c>0xD8</c>-byte object <c>0x008DBC70</c> allocates (its core is <c>object + 8</c>; the vtable thunks add 8) with the fields of the core ctor <c>0x008DBF44</c>:
/// <c>[core+0]</c> the id the process callback receives (the object itself, <c>0x008DBC82</c>), <c>[core+4]</c> the output rate, <c>u16 [core+8]</c> the chunk frames, <c>[core+0xC]</c> the channels, <c>[core+0x10]</c> the input rate,
/// the resampler <c>R = core+0x14</c>, the output buffer <c>core+0x6C</c> (data, channel word, eState 0x2B, <c>u16 +0x78</c> max, <c>u16 +0x7A</c> valid), the allocation <c>[core+0x7C]</c> and the process functor <c>[core+0x90]</c>.
///
/// <para>Init <c>0x008DBD74</c> stores the input rate (<c>0x008DBF70</c>), runs the core init <c>0x008DBF76</c> (the channel count from <c>byte [fmt+4]</c>, 0 returns 2; a <c>channels * 4096</c>-byte allocation;
/// <c>CAkResampler::Init(fmt, [core+4])</c>; <c>SetPitch(0.0f, false)</c>) and then, whatever the result, the create callback <c>0x008DBDB0</c> (PrepareAudioBuffer). Execute <c>0x008DBFE8</c> returns without touching the buffer
/// when the process functor is unset (<c>[core+0x90] == 0</c>); otherwise it stores the limit <c>[R+0x40] = u16 [core+8]</c> once and loops <c>Execute(R, buf, core+0x6C)</c>: a result of 0x2D or 0x11 calls the process callback
/// with <c>(id, data, u16 [core+0x7A])</c> (a partial count on 0x11, also 0), then resets <c>[R+0x28]</c> and the out buffer's valid count, and the loop repeats while <c>u16 [buf+0xE] != 0</c>. The resampler consumes the whole
/// input, so the bus buffer's valid count is 0 afterwards (the samples are untouched). Term <c>0x008DBDF8</c> fires the destroy callback, then frees the allocation (<c>0x008DBFCC</c>).</para>
///
/// <para>The input buffer is the engine's state (<see cref="WwiseDecodeState"/>: planar <c>float[]</c> data, <c>u16</c> max and valid frames); the 744 / 22,320 figures are the plug-in's (SetupEnginePlugInFx). What the callbacks do
/// (PrepareAudioBuffer, UpdateBuffer, CloseAudioBuffer) lives outside this record.</para>
///
/// <para><b>MISSING.</b> Init's fourth argument (<c>0x8DBD82 str.w r3,[r4,#0xa0]</c>: <c>[obj+0xA0]</c>, core+0x98) is stored by the engine and read by the create, process and destroy callbacks' call sites (<c>0x8DBAA2</c>, <c>0x8DBB5E</c>,
/// <c>0x8DBC36</c>: <c>[[fx]+0xA0]+4</c> at <c>0x8DBAAA</c>, <c>0x8DBB6E</c>, <c>0x8DBC40</c>, passed on to the callbacks); this class neither stores nor passes it (the C# callbacks are <c>(fx, chunk, count)</c>). <see cref="WwiseHijackPlugin.Create"/>
/// has no production caller yet.</para>
/// </summary>
public sealed class WwiseHijackFx
{
    // fidelity: M6-015
    internal Action<WwiseHijackFx>? CreateCallback;
    internal Action<WwiseHijackFx, float[], int>? ProcessCallback;
    internal Action<WwiseHijackFx>? DestroyCallback;

    private readonly WwiseResampler _resampler = new();                       // 0x8DBF58 blx 0xA46D70(core+0x14)
    private readonly WwiseDecodeState _out = new() { Scratch08 = 0x2B };      // 0x8DBF62..0x8DBF66: [core+0x6C] = [core+0x70] = 0, [core+0x74] = 0x2B, [core+0x78] = 0

    internal WwiseHijackFx() { }

    /// <summary><c>[core+4]</c>: the output rate SetupEnginePlugInFx stores (22,320 Hz).</summary>
    public int OutputRateHz { get; internal set; }

    /// <summary><c>u16 [core+8]</c>: the chunk frames SetupEnginePlugInFx stores (744).</summary>
    public int ChunkSize { get; internal set; }

    /// <summary><c>[core+0xC]</c>: the channel count (<c>byte [fmt+4]</c>; 0 fails the init with 2).</summary>
    public int Channels { get; private set; }

    /// <summary><c>[core+0x10]</c>: the input rate (<c>u32 [fmt+0]</c>, stored by <c>0x8DBF70</c>).</summary>
    public uint InputRate10 { get; private set; }

    /// <summary><c>[core+0x7C]</c>: the <c>channels * 4096</c>-byte allocation (1,024 floats per channel) which is also the out buffer's data (<c>[core+0x6C]</c>); null before Init and after Term.</summary>
    public float[]? OutputData => _out.Data as float[];

    /// <summary>Whether the core init returned 1.</summary>
    public bool Initialized { get; private set; }

    /// <summary>The out buffer <c>core+0x6C</c> (data, channel word, eState, <c>u16</c> max <c>+0x78</c> and valid <c>+0x7A</c> frames).</summary>
    public WwiseDecodeState OutputBuffer => _out;

    /// <summary>The resampler <c>R = core+0x14</c> (M6-004).</summary>
    public WwiseResampler Resampler => _resampler;

    /// <summary>
    /// Hijack Init <c>0x008DBD74</c> (A15): <c>[core+0x10] = rate</c>, the core init <c>0x008DBF76</c> and then the create callback (whatever the result). Returns the core init's result: 1, or 2 for a channel count of 0.
    /// </summary>
    public int Init(WwiseResamplerFormat fmt)
    {
        InputRate10 = unchecked((uint)fmt.SampleRate);                          // 0x8DBD8A bl 0x8DBF70
        int result = InitCore(fmt);                                              // 0x8DBD94 bl 0x8DBF76
        CreateCallback?.Invoke(this);                                            // 0x8DBD9A..0x8DBDA6: [obj+0xB8] != 0 -> 0x8DBDB0(obj+0xA8, obj)
        return result;                                                           // 0x8DBDAA mov r0,r5
    }

    private int InitCore(WwiseResamplerFormat fmt)
    {
        Channels = (byte)fmt.Channels;                                           // 0x8DBF7C ldrb r0,[r4,#4]; 0x8DBF7E str r0,[r5,#0xc]
        if (Channels == 0) return 2;                                             // 0x8DBF80 cbz r0, 0x8DBFBE
        var allocation = new float[Channels * 1024];                             // 0x8DBF86..0x8DBF8E allocator->vt+8(channels << 12): 1,024 floats per channel
        _out.ValidFrames = 0;                                                    // 0x8DBF9A
        _out.MaxFrames = unchecked((ushort)ChunkSize);                           // 0x8DBF96, 0x8DBFA0
        _out.Data = allocation;                                                  // 0x8DBFA4 strd r0,ip,[r5,#0x6C]
        _out.ChannelConfig = fmt.ChannelWord;                                    // ... ip = u32 [fmt+4]
        _resampler.Init(fmt, unchecked((uint)OutputRateHz));                     // 0x8DBFAC bl 0xA47038(R, fmt, [core+4])
        _resampler.SetPitch(0f, false);                                          // 0x8DBFB6 bl 0xA47384(R, 0.0f, 0)
        Initialized = true;
        return 1;                                                                // 0x8DBFBA
    }

    /// <summary>
    /// Hijack Execute <c>0x008DBFE8</c> (A16, B-07): see the class summary. <paramref name="buffer"/> is the bus buffer; its valid count is 0 afterwards.
    /// </summary>
    public void Execute(WwiseDecodeState buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (ProcessCallback is null) return;                                     // 0x8DBFF2..0x8DBFF6 ldr r0,[r5,#0x90]; cbz -> 0x8DC03A: the buffer is untouched
        _resampler.Limit40 = unchecked((uint)(ushort)ChunkSize);                 // 0x8DC004..0x8DC00A [core+0x54] = u16 [core+8]
        do                                                                       // 0x8DC00C
        {
            int r0 = _resampler.Execute(buffer, _out);                           // 0x8DC012 blx 0xA47178(R, buf, core+0x6C)
            if (r0 == WwiseResampler.DataReady || r0 == WwiseResampler.NoMoreData)   // 0x8DC016..0x8DC01C
            {
                Flush(_out.ValidFrames);                                         // 0x8DC028 bl 0x8DC040(core+0x80, [core], [core+0x6C], u16 [core+0x7A])
                _resampler.OutputOffset28 = 0;                                   // 0x8DC02C str sb,[r5,#0x3c]
                _out.ValidFrames = 0;                                            // 0x8DC030 strh sb,[r5,#0x7a]
            }
        }
        while (buffer.ValidFrames != 0);                                         // 0x8DC034..0x8DC038 ldrh r0,[r4,#0xe]; bne 0x8DC00C
    }

    /// <summary>Hijack Term <c>0x008DBDF8</c> (A18): the destroy callback (<c>0x8DBDFE..0x8DBE0A</c>), then the allocation is released and the out buffer's pointers cleared (<c>0x8DBFCC</c>).</summary>
    public void Term()
    {
        DestroyCallback?.Invoke(this);                                           // 0x8DBDFE..0x8DBE0A
        _out.MaxFrames = 0;                                                      // 0x8DBFD2 str r5,[r4,#0x78]
        _out.Data = null;                                                        // 0x8DBFD4 strd r5,r5,[r4,#0x6c]
        _out.ChannelConfig = 0;
        Initialized = false;
    }

    /// <summary><c>Reset</c> <c>0x008DBFC2</c> (vt+0xC): <c>[core+0x3C] = 0</c> (<c>R+0x28</c>) and <c>u16 [core+0x7A] = 0</c>.</summary>
    public void Reset()
    {
        _resampler.OutputOffset28 = 0;
        _out.ValidFrames = 0;
    }

    private void Flush(int count)
    {
        // The engine hands over (id = this object, [core+0x6C], count); the callback gets the first channel's `count` frames.
        var chunk = new float[count];
        if (count != 0) Array.Copy(OutputData!, 0, chunk, 0, count);
        ProcessCallback!.Invoke(this, chunk, count);
    }
}
