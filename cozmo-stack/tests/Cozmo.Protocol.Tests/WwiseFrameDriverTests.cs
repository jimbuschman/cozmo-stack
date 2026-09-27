using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-017 / gapD D1.6..D1.8 and D3.1..D3.5: the audio-thread frame model. One engine frame is messages,
/// then the pending-action drain, then the render pass (<c>buses → LEngine → PBI-notification flush</c>),
/// then <c>tick++</c>; the sink gates frame production; and EndOfEvent fires after the last voice's
/// final-frame bus pass and before the next frame's partial flush.
///
/// The expected orders and counts below are the rows' own: D1.6's four steps, D1.7's count-0 exit pass,
/// D2.7's voice-then-bus pass inside LEngine, and D3.5's EndOfEvent position. None is read back from the
/// driver; the recording render seam observes the order the driver calls it in, and the executed-action
/// count and tick come from the frozen M6-006 runtime.
/// </summary>
public class WwiseFrameDriverTests
{
    // ------------------------------------------------------------------ builders

    private static void U32(List<byte> b, uint v) => b.AddRange(BitConverter.GetBytes(v));
    private static void U16(List<byte> b, ushort v) => b.AddRange(BitConverter.GetBytes(v));

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
        U32(bkhd, 120);            // version
        U32(bkhd, bankId);
        U32(bkhd, 0);
        U32(bkhd, 0);              // feedback flag clear
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

    /// <summary>The 28-byte empty node block the shipped reader consumes.</summary>
    private static void NodeBlock(List<byte> b, uint parent)
    {
        b.Add(0); b.Add(0); b.Add(0);   // fx byte, no fx, overrideAttachment
        U32(b, 0); U32(b, parent);      // bus, parent
        b.Add(0);                       // bits
        b.Add(0);                       // property count
        b.Add(0);                       // ranged count
        b.Add(0);                       // positioning
        b.Add(0);                       // aux
        b.AddRange(new byte[6]);        // advanced settings
        U32(b, 0);                      // state-group count
        U16(b, 0);                      // RTPC count
    }

    private static byte[] SoundPayload(uint id)
    {
        var b = new List<byte>();
        U32(b, id);
        U32(b, 0x00040001);   // codec plug-in (low nibble 1)
        b.Add(0);             // stream type
        U32(b, 12345);        // media id
        U32(b, 0);            // in-memory size
        b.Add(0);             // source bits
        NodeBlock(b, 0);
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

    /// <summary>The common Action layout (gapA 1.7) for a zero-delay Play 0x0403.</summary>
    private static byte[] PlayActionPayload(uint id, uint target)
    {
        var b = new List<byte>();
        U32(b, id);
        U16(b, 0x0403);              // Play, object scope
        U32(b, target);
        b.Add(0);                    // isBus
        b.Add(0);                    // property count
        b.Add(0);                    // ranged count
        b.Add(0);                    // Play fade curve
        U32(b, 0);                   // Play bank id
        return b.ToArray();
    }

    /// <summary>A bank with one Sound target and one Event whose single Play action has no delay.</summary>
    private static WwiseBank OnePlayBank()
    {
        const uint target = 500;
        return WwiseBank.Parse(File(1,
            Hirc(
                (2, SoundPayload(target)),
                (3, PlayActionPayload(100, target)),
                (4, EventPayload(900, 100)))),
            "t.bnk");
    }

    // ------------------------------------------------------------------ seams

    /// <summary>The caller's sink-room signal (D1.8); settable in a test.</summary>
    private sealed class TestSink : IWwiseAudioSink
    {
        public bool HasRoomForFrame { get; set; }
    }

    /// <summary>The caller's frames-per-Perform count (D1.7), which the rows leave RECOVERABLE_GAP.</summary>
    private sealed class TestFrameSource : IWwiseFrameSource
    {
        public int Count { get; set; }
        public int FramesToRender() => Count;
    }

    /// <summary>
    /// Records the render sub-steps the driver calls, and the control-path state at the moment of each
    /// (D1.6): the executed-action count and the tick. The render does no work, as the built voice/bus
    /// engine does not exist yet.
    /// </summary>
    private sealed class StepRecorder : IWwiseFrameRender
    {
        private readonly WwiseEventRuntime _runtime;
        public List<string> Steps { get; } = new();
        public List<(string Step, int Executed, long Tick)> Observed { get; } = new();

        public StepRecorder(WwiseEventRuntime runtime) => _runtime = runtime;

        private void Record(string step)
        {
            Steps.Add(step);
            Observed.Add((step, _runtime.ExecutedActions.Count, _runtime.CurrentTick));
        }

        public void RenderBuses() => Record("buses");
        public void RunLEngine() => Record("engine");
        public void FlushPbiNotifications() => Record("flush");
    }

    /// <summary>
    /// A render seam that embodies the row's internal frame timing: LEngine carries the bus pass (D2.7),
    /// the PBI flush carries EndOfEvent (D3.4/D3.5), and the second frame's bus pass is the empty-input
    /// partial flush (D2.8).
    /// </summary>
    private sealed class BusPassRecorder : IWwiseFrameRender
    {
        private int _engineCalls;
        private int _flushes;
        public List<string> Steps { get; } = new();

        public void RenderBuses() => Steps.Add("buses");
        public void RunLEngine() => Steps.Add(_engineCalls++ == 0 ? "busPass" : "partialFlush");

        public void FlushPbiNotifications()
        {
            Steps.Add("flush");
            // D3.4: the flush handles a queued PBI teardown, so EndOfEvent fires only on the frame that had
            // the last voice's teardown queued, not on an empty flush.
            if (_flushes++ == 0) Steps.Add("endOfEvent");
        }
    }

    private static WwiseFrameDriver Driver(WwiseEventRuntime runtime, IWwiseFrameRender render,
                                           TestSink sink, TestFrameSource? source = null) =>
        new(runtime, render, sink, source ?? new TestFrameSource());

    private static (WwiseEventRuntime Runtime, StepRecorder Render, TestSink Sink) Posted()
    {
        var runtime = new WwiseEventRuntime(new[] { OnePlayBank() }, new WwiseRng(1));
        runtime.RegisterGameObject(7);
        runtime.PostEvent(900, 7);
        return (runtime, new StepRecorder(runtime), new TestSink());
    }

    // ------------------------------------------------------------------ D1.6 Perform order

    /// <summary>
    /// M6-017 / gapD D1.6: one frame is messages, then the pending-action drain, then the render, then
    /// <c>tick++</c>. At every render sub-step the event's action has already executed (so messages+drain
    /// ran first) and the tick is still T (so tick++ has not run), and after the frame the tick is T+1.
    /// </summary>
    [Fact]
    public void M6_017_D1_6_ThePerformOrderIsMessagesDrainRenderThenTick()
    {
        var (runtime, render, sink) = Posted();
        sink.HasRoomForFrame = true;
        var driver = Driver(runtime, render, sink);

        Assert.True(driver.RunFrame());

        // The render seam is called in the row's order: buses, LEngine, PBI-notification flush.
        Assert.Equal(new[] { "buses", "engine", "flush" }, render.Steps);

        // Messages+drain ran before any render step: the action is already executed.
        Assert.All(render.Observed, o => Assert.Equal(1, o.Executed));
        // The tick is unchanged during the render: tick++ is after it (D1.6 step 3).
        Assert.All(render.Observed, o => Assert.Equal(0, o.Tick));
        // And after the frame the tick has advanced exactly once.
        Assert.Equal(1, driver.CurrentTick);
    }

    // ------------------------------------------------------------------ D1.8 sink gate

    /// <summary>
    /// M6-017 / gapD D1.8 (audio thread 0xA4087C, gapB T2): the sink signals when there is room for a
    /// frame, and Perform runs only then. With no room no frame is produced (no render, no tick); once the
    /// sink signals, the requested frame is produced.
    /// </summary>
    [Fact]
    public void M6_017_D1_8_NoFrameIsProducedUntilTheSinkHasRoom()
    {
        var (runtime, render, sink) = Posted();
        var source = new TestFrameSource { Count = 1 };
        sink.HasRoomForFrame = false;
        var driver = Driver(runtime, render, sink, source);

        Assert.Equal(0, driver.Perform());
        Assert.Empty(render.Steps);                 // no render step ran
        Assert.Equal(0, driver.CurrentTick);        // and no tick advanced

        sink.HasRoomForFrame = true;
        Assert.Equal(1, driver.Perform());
        Assert.Equal(new[] { "buses", "engine", "flush" }, render.Steps);
        Assert.Equal(1, driver.CurrentTick);
    }

    // ------------------------------------------------------------------ D1.6 count-0 exit pass

    /// <summary>
    /// M6-017 / gapD D1.6: "when the count is 0, the loop exits after one more messages+drain pass". A
    /// Perform with a 0 frame count still pumps and drains — the queued action runs — but produces no frame
    /// and advances no tick.
    /// </summary>
    [Fact]
    public void M6_017_D1_6_AZeroCountPerformStillPumpsAndDrainsOnceAndRendersNoFrame()
    {
        var (runtime, render, sink) = Posted();
        sink.HasRoomForFrame = true;
        var driver = Driver(runtime, render, sink, new TestFrameSource { Count = 0 });

        Assert.Equal(0, driver.Perform());

        var executed = Assert.Single(runtime.ExecutedActions);
        Assert.Equal(100u, executed.ActionId);
        Assert.Empty(render.Steps);                 // no frame rendered
        Assert.Equal(0, driver.CurrentTick);        // no tick++
    }

    // ------------------------------------------------------------------ D3.5 EndOfEvent position

    /// <summary>
    /// M6-017 / gapD D3.5 (D2.7, D3.3, D3.4): EndOfEvent fires after the last voice's final-frame bus pass
    /// and before the next frame's partial-chunk flush. The driver's per-frame order is LEngine (the bus
    /// pass) then the PBI-notification flush (which drives CheckEndOfEvent); the next frame's LEngine
    /// carries the empty-input partial flush.
    /// </summary>
    [Fact]
    public void M6_017_D3_5_EndOfEventFiresAfterTheLastFramesBusPassAndBeforeTheNextPartialFlush()
    {
        var (runtime, _, sink) = Posted();
        sink.HasRoomForFrame = true;
        var render = new BusPassRecorder();
        var driver = Driver(runtime, render, sink);

        Assert.True(driver.RunFrame());             // frame N: the last voice's final samples
        Assert.True(driver.RunFrame());             // frame N+1: the empty-input tail

        Assert.Equal(new[] { "buses", "busPass", "flush", "endOfEvent", "buses", "partialFlush", "flush" },
            render.Steps);

        int busPass = render.Steps.IndexOf("busPass");
        int endOfEvent = render.Steps.IndexOf("endOfEvent");
        int partialFlush = render.Steps.IndexOf("partialFlush");
        Assert.True(busPass >= 0 && endOfEvent > busPass, "EndOfEvent must fire after the bus pass");
        Assert.True(partialFlush > endOfEvent, "EndOfEvent must fire before the next frame's partial flush");
    }
}
