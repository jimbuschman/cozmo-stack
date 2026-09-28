// fidelity: M6-017
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The sink's signal that the audio thread may render another engine frame (M6-017, gapD D1.8; gapB T2).
/// The audio thread loop is <c>{ Perform; wait on event }</c>, and the OpenSL sink signals that event once
/// its free space reaches one engine frame. The signal is the caller's: this stack has no phone sink, and
/// gapB T2's ring size and pacing are its own HARDWARE_ONLY model, so the driver asks and never paces.
/// </summary>
public interface IWwiseAudioSink
{
    /// <summary>T2/D1.8: free space ≥ one engine frame, so the sink has signalled the audio-thread event.</summary>
    bool HasRoomForFrame { get; }
}

/// <summary>
/// How many engine frames the current Perform renders (M6-017, gapD D1.7; corrected by C11). The branch is
/// dynamic, not static: Perform calls <c>0x9D4778 → 0x9EC38C → 0x9EBE6C(0)</c>, which writes gate1
/// <c>0x108DAF0</c>, and only then reads gate1 at <c>0x9AF900</c> (M6-022 V4/G10). The count is therefore
/// either the device frames needed (the <c>0x9EBE6C</c> minimum over the output devices' <c>vt+0x20</c>) or
/// the clock-paced branch; <c>0x9EBE6C</c>'s internals and the gate writers are settled (M6-022 D1..D4,
/// G1..G10), but the per-device <c>vt+0x20</c> sink value and the OpenSL pacing are <b>HARDWARE_ONLY</b>
/// (M6-022 D4, M6-018). The runtime value is therefore not derivable from the artifact, so the count stays
/// a caller input; the driver neither computes it nor defaults it.
/// </summary>
public interface IWwiseFrameSource
{
    /// <summary>D1.7: the frames this Perform should render. 0 is a valid answer (D1.6's count-0 exit).</summary>
    int FramesToRender();
}

/// <summary>
/// One engine frame's render pass, in the order the row gives it (M6-017, gapD D1.6):
/// <c>buses → LEngine → PBI-notification flush</c>. The row settles only that order and those three names;
/// it does not settle what is inside them, so each is a caller seam rather than an invented default.
///
/// <list type="bullet">
/// <item><b><see cref="RenderBuses"/> — the row's "buses".</b> D1.6's leading render calls
/// <c>0xA36AC4(tick+1)</c>, <c>0x9FF308(tick+1)</c>, <c>0x9D3C98</c>, <c>0x9E6D2C</c>. C11 identifies these
/// as the four group members (M6-022 V25..V28): their Wwise class names are UNKNOWN (no RTTI/symbols) and
/// their callee bodies are M6-022's rows. It is not the M6-014 bus pass: D2.7 puts the bus pass inside
/// LEngine, after the voice pass.</item>
/// <item><b><see cref="RunLEngine"/> — 0xA57FF8.</b> D2.7: the voice pass (0xA44948) first, each voice
/// mixing into its buses; then the bus pass (0xA44C18, GetResultingBuffer + ReleaseBuffer per bus); then
/// idle removal. The M6-022 voice/bus engine is the intended implementation; until it is built this is a
/// seam, and the M6-014 <see cref="WwiseMixBusHierarchy"/> is the intended bus-pass/idle-removal half.</item>
/// <item><b><see cref="FlushPbiNotifications"/> — 0xA38420.</b> D3.4: the deferred PBI teardown queued by
/// the voice Term (0xA38600, reason 4) is handled here; its Term (PBI <c>vt+0x10</c>) drives the playing-id
/// counters and CheckEndOfEvent 0xA03618, so the EndOfEvent callback fires in this step.</item>
/// </list>
/// </summary>
public interface IWwiseFrameRender
{
    /// <summary>D1.6: the render group before LEngine. Its four members are unidentified; see the type.</summary>
    void RenderBuses();

    /// <summary>D1.6/D2.7: the voice pass, then the bus pass, then idle removal. Voice engine unbuilt.</summary>
    void RunLEngine();

    /// <summary>D1.6/D3.4: the deferred PBI teardown, which is what fires CheckEndOfEvent / EndOfEvent.</summary>
    void FlushPbiNotifications();
}

/// <summary>
/// The audio-thread frame model (M6-017, gapD D1.6..D1.8 and D3.1..D3.5). One engine frame is, in order:
/// the message pump, the pending-action drain, the render pass (<c>buses → LEngine → PBI-notification
/// flush</c>), then <c>tick++</c> on the audio manager's <c>+0x4C</c>. The sink gates frame production: no
/// frame is produced until it signals room. The PBI-notification flush runs after LEngine's bus pass and
/// before the next frame's render, which is where D3.5 puts EndOfEvent (after the last voice's final-frame
/// bus pass, before the next frame's partial-chunk flush and CloseAudioBuffer).
///
/// <para><b>The audio-thread lifecycle</b> (M6-017, C11 amendment; Appendix L E1..E7) is part of this
/// record's production path: <c>SoundEngine::Init 0x0099E3EC</c> sets the thread-active byte
/// <c>0x0108D949</c> and calls <c>FUN_009B0200</c>, which posts a type-0x36 init message and calls
/// <c>FUN_00A40940(engine+0x54)</c>; that <c>sem_init</c>s the semaphore and
/// <c>pthread_create</c>s entry <c>0x00A4087C</c>, whose loop is <c>Perform 0x9AF8A8</c> then
/// <c>sem_wait</c>. <c>RenderAudio</c> is the veneer <c>0x0099F130</c> -> <c>0x9AFD10(engine,1)</c>: if the
/// thread-active flag is set it <c>sem_post</c>s <c>engine+0x54</c> (<c>FUN_00A40924</c>), else it runs
/// <c>Perform</c> synchronously. This stack has no phone sink, so which of those two the caller uses is a
/// caller decision (Appendix L open question 1); the driver models the per-Perform frame body.</para>
///
/// <para><b>Wiring.</b> The message pump and the pending-action drain are
/// <see cref="WwiseEventRuntime.PumpMessages"/> and <see cref="WwiseEventRuntime.DrainDueActions"/> from
/// M6-006, which owns the queue and the <c>+0x4C</c> tick. The render is a caller seam
/// (<see cref="IWwiseFrameRender"/>); the M6-008…M6-016 modules plug into it, and the M6-022 voice/bus
/// engine is the intended implementation of the LEngine step. The sink and the frame source
/// are caller inputs. <b>Not wired:</b> this is a standalone class; it is not yet part of
/// <c>WwisePlayback</c>, <c>WwiseAudioSource</c>, <c>WwiseSongRenderer</c> or <c>AnimationScheduler</c>.</para>
///
/// <para><b>The tick, exactly.</b> D1.6 advances <c>mgr+0x4C</c> after the render. M6-006's public surface
/// advances that tick only inside <see cref="WwiseEventRuntime.AdvanceFrame"/>, which also pumps and drains;
/// after this driver's explicit pump and drain, and because the render step does not enqueue control-path
/// messages, those two calls are no-ops, so the order stays messages → drain → render → tick. A future
/// M6-006 change that exposes the tick on its own would let this call be direct; until then the render seam
/// must not post events (it runs on the audio thread, as the row's render does).</para>
/// </summary>
public sealed class WwiseFrameDriver
{
    private readonly WwiseEventRuntime _eventRuntime;
    private readonly IWwiseFrameRender _render;
    private readonly IWwiseAudioSink _sink;
    private readonly IWwiseFrameSource _frameSource;

    /// <param name="eventRuntime">The M6-006 control path: its queue is the pending-action list and its
    /// <c>CurrentTick</c> is the audio manager's <c>+0x4C</c>.</param>
    /// <param name="render">The per-frame render pass, in the row's three-step order.</param>
    /// <param name="sink">The room-for-a-frame signal (D1.8).</param>
    /// <param name="frameSource">The frames-per-Perform count, which D1.7 leaves RECOVERABLE_GAP.</param>
    public WwiseFrameDriver(
        WwiseEventRuntime eventRuntime,
        IWwiseFrameRender render,
        IWwiseAudioSink sink,
        IWwiseFrameSource frameSource)
    {
        _eventRuntime = eventRuntime ?? throw new ArgumentNullException(nameof(eventRuntime));
        _render = render ?? throw new ArgumentNullException(nameof(render));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _frameSource = frameSource ?? throw new ArgumentNullException(nameof(frameSource));
    }

    /// <summary>The audio manager tick (<c>+0x4C</c>), read from the M6-006 runtime.</summary>
    public long CurrentTick => _eventRuntime.CurrentTick;

    /// <summary>How many frames this driver has produced across its Performs (each one advanced the tick).</summary>
    public int FramesRendered { get; private set; }

    /// <summary>
    /// One engine frame (D1.6): messages, drain, render (<c>buses → LEngine → PBI-notification flush</c>),
    /// then <c>tick++</c>. The sink gates it: when it has no room, nothing runs and false is returned. This
    /// is D1.6's per-frame body without the Perform loop's frame count or its trailing messages+drain pass.
    /// </summary>
    /// <returns>True when a frame was produced, false when the sink had no room.</returns>
    public bool RunFrame()
    {
        if (!_sink.HasRoomForFrame) return false;                       // D1.8: Perform only when signalled

        _eventRuntime.PumpMessages();                                   // D1.6 step 1
        _eventRuntime.DrainDueActions();                                // D1.6 step 2

        _render.RenderBuses();                                          // D1.6 step 3, the row's "buses"
        _render.RunLEngine();                                           // 0xA57FF8: voice + bus + idle (D2.7)
        _render.FlushPbiNotifications();                                // 0xA38420: PBI teardown → EndOfEvent (D3.4)

        AdvanceTick();                                                  // D1.6 step 3: mgr+0x4C++
        FramesRendered++;
        return true;
    }

    /// <summary>
    /// The audio thread's Perform (D1.6/D1.8): read the frame source (D1.7), render that many frames, each
    /// gated by the sink, then run D1.6's final messages+drain pass — "when the count is 0, the loop exits
    /// after one more messages+drain pass". A count of 0 therefore still pumps once and renders nothing.
    /// </summary>
    /// <returns>How many frames were produced (the count, less any the sink refused).</returns>
    public int Perform()
    {
        int frames = _frameSource.FramesToRender();                     // D1.7 (RECOVERABLE_GAP; caller input)
        int rendered = 0;
        for (int i = 0; i < frames; i++)
        {
            if (!RunFrame()) break;                                     // D1.8: the sink stops the loop
            rendered++;
        }

        _eventRuntime.PumpMessages();                                   // D1.6: the exit pass when the count is 0
        _eventRuntime.DrainDueActions();
        return rendered;
    }

    /// <summary>
    /// D1.6 step 3: <c>mgr+0x4C++</c>. The tick lives in M6-006, whose only public advance is
    /// <see cref="WwiseEventRuntime.AdvanceFrame"/>; the frame's explicit pump and drain have already run
    /// and the render seam does not enqueue, so its two calls are no-ops here and this is the tick++.
    /// </summary>
    private void AdvanceTick() => _eventRuntime.AdvanceFrame();
}

/// <summary>
/// The audio-thread signal (M6-017, C11 amendment; Appendix L E1..E7): the semaphore at
/// <c>engine+0x54</c>. <c>SoundEngine::Init 0x0099E3EC</c> sets the thread-active byte
/// <c>0x0108D949</c>; <c>FUN_009B0200</c> calls <c>FUN_00A40940</c>, which <c>sem_init</c>s and
/// <c>pthread_create</c>s entry <c>0x00A4087C</c>. The thread loop is <c>Perform</c> then <c>sem_wait</c>;
/// <c>FUN_00A40924</c> is <c>sem_post</c> when the thread flag is set, and the OpenSL sink posts the same
/// semaphore.
///
/// <para>This stack has no phone sink, so whether it runs a real thread or renders synchronously is a
/// caller decision (Appendix L open question 1); the recovered decision structure is modelled and the
/// semaphore is a caller seam.</para>
/// </summary>
public interface IWwiseAudioThreadSignal
{
    /// <summary>E1/E2: the audio-thread-active byte <c>0x0108D949</c>.</summary>
    bool ThreadActive { get; }

    /// <summary>E7 <c>FUN_00A40924</c>: <c>sem_post(engine+0x54)</c> when the thread flag is set.</summary>
    void Post();

    /// <summary>
    /// E4: one <c>sem_wait</c> on the thread's semaphore. Returns true when the loop should stop (the
    /// semaphore's stop flag is set); false otherwise.
    /// </summary>
    bool Wait();
}

/// <summary>
/// The audio thread's <c>RenderAudio</c> decision and its <c>Perform</c>/<c>sem_wait</c> loop (M6-017,
/// C11 amendment; E4/E6/E7). The public <c>RenderAudio</c> veneer <c>0x0099F130</c> tail-branches to
/// <c>0x9AFD10(engine,1)</c>: if the thread-active flag is set it <c>sem_post</c>s <c>engine+0x54</c>,
/// otherwise it runs <c>Perform</c> synchronously. The thread entry <c>0x00A4087C</c> loops
/// <c>Perform</c> then <c>sem_wait</c>. The stack does not spawn the thread; <see cref="RenderAudio"/> and
/// <see cref="DrainThreadOnce"/> expose both halves so the caller can choose (Appendix L Q1).
/// </summary>
public sealed class WwiseAudioThread
{
    private readonly WwiseFrameDriver _driver;
    private readonly IWwiseAudioThreadSignal _signal;

    /// <param name="driver">The M6-017 frame driver whose <see cref="WwiseFrameDriver.Perform"/> is the loop body.</param>
    /// <param name="signal">The semaphore/thread-active seam.</param>
    public WwiseAudioThread(WwiseFrameDriver driver, IWwiseAudioThreadSignal signal)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _signal = signal ?? throw new ArgumentNullException(nameof(signal));
    }

    /// <summary>E6: <c>0x9AFD10(engine,1)</c>. Posts when the thread is active, else performs synchronously.</summary>
    /// <returns>The frames rendered, or 0 when the frame was handed to the audio thread.</returns>
    public int RenderAudio()
    {
        if (_signal.ThreadActive)
        {
            _signal.Post();                             // E6/E7: 0x00A40924
            return 0;
        }
        return _driver.Perform();                       // E6: the synchronous fallback
    }

    /// <summary>
    /// E4: one turn of the thread loop — <c>Perform</c> then <c>sem_wait</c>. Returns false when the
    /// semaphore signals stop, so a caller running a real thread can exit.
    /// </summary>
    public bool DrainThreadOnce()
    {
        _driver.Perform();
        return !_signal.Wait();
    }
}
