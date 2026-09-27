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
/// How many engine frames the current Perform renders (M6-017, gapD D1.7). <b>RECOVERABLE_GAP:</b> D1.7
/// gives two computations (the device frames needed <c>0x9D4778 → 0x9EBE6C(0)</c>, or a clock-paced count
/// with a carried fraction at <c>+0x70</c>, capped) and says the writer of the gating flag and
/// <c>0x9EBE6C</c>'s internals were not read. The count therefore stays a caller input; the driver neither
/// computes it nor defaults it.
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
/// <c>0xA36AC4(tick+1)</c>, <c>0x9FF308(tick+1)</c>, <c>0x9D3C98</c>, <c>0x9E6D2C</c>. Those four functions
/// are not identified in the rows, so this step is a seam. It is not the M6-014 bus pass: D2.7 puts the bus
/// pass inside LEngine, after the voice pass.</item>
/// <item><b><see cref="RunLEngine"/> — 0xA57FF8.</b> D2.7: the voice pass (0xA44948) first, each voice
/// mixing into its buses; then the bus pass (0xA44C18, GetResultingBuffer + ReleaseBuffer per bus); then
/// idle removal. The voice engine is not built, so this is a seam; the M6-014
/// <see cref="WwiseMixBusHierarchy"/> is the intended bus-pass/idle-removal half.</item>
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
/// <para><b>Wiring.</b> The message pump and the pending-action drain are
/// <see cref="WwiseEventRuntime.PumpMessages"/> and <see cref="WwiseEventRuntime.DrainDueActions"/> from
/// M6-006, which owns the queue and the <c>+0x4C</c> tick. The render is a caller seam
/// (<see cref="IWwiseFrameRender"/>); the M6-008…M6-016 modules plug into it. The sink and the frame source
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
