using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-014 / gapD D2.1..D2.9: the bus and Hijack lifetime. The bus is created on demand and reused by key
/// (D2.1), its FX are instantiated lazily at its first GetResultingBuffer so Init runs just before the first
/// Execute (D2.2), the FX run only while the state is 1 and the state machine turns a frame with no mixing
/// into an idle frame (D2.4/D2.5), one empty-input tail frame flushes the partial chunk — possibly 0 frames,
/// which UpdateBuffer still accepts (D2.8/D2.9) — and the bus is destroyed after that idle frame when nothing
/// is connected and it was not reused (D2.6).
///
/// Every expected value below is the row's own constant applied by hand — state 1 and 4, eState 0x2D and
/// 0x11, the 1,024-frame frame, the 0-frame chunk — never a value read back from the class. The addresses
/// are the rows' citations.
/// </summary>
public class WwiseBusLifetimeTests
{
    /// <summary>A stand-in for one insert-FX slot, recording the lifecycle calls the bus makes (D2.2/D2.4/D2.6).</summary>
    private sealed class RecordingFx : IWwiseBusInsertFx
    {
        public RecordingFx(bool isInPlace = true, int state = 1)
        {
            IsInPlace = isInPlace;
            State = state;
        }

        public bool IsInPlace { get; }
        public int State { get; set; }
        public List<string> Calls { get; } = new();
        public List<int> ExecutedValidFrames { get; } = new();
        public float[] Chunk { get; set; } = Array.Empty<float>();

        public void Init() => Calls.Add("init");
        public void Reset() => Calls.Add("reset");

        public void Execute(int validFrames, WwiseUpdateBuffer updateBuffer)
        {
            Calls.Add("execute");
            ExecutedValidFrames.Add(validFrames);
            updateBuffer(Chunk);
        }

        public void Term() => Calls.Add("term");
    }

    /// <summary>Collects every chunk an effect flushed, so an empty one is observable rather than dropped.</summary>
    private sealed class ChunkSink
    {
        public List<float[]> Chunks { get; } = new();
        public void Receive(ReadOnlyMemory<float> chunk) => Chunks.Add(chunk.ToArray());
    }

    private static WwiseMixBus Bus(params WwiseBusFxSlot[] slots) =>
        new(new WwiseMixBusKey(1, 2, 3, 4), slots);

    // ------------------------------------------------------------------ D2.1 creation

    /// <summary>
    /// M6-014 / D2.1 (GetOrCreateMixBus 0xA43438, CreateMixBus 0xA42210): a bus is created on demand and
    /// appended; a second request with the same key reuses the node and marks it touched (<c>+0x1CC b0</c>),
    /// and a different key makes a new one.
    /// </summary>
    [Fact]
    public void ABusIsCreatedOnDemandAndReusedByKey()
    {
        var hierarchy = new WwiseMixBusHierarchy();
        var key = new WwiseMixBusKey(1, 2, 3, 4);

        var first = hierarchy.GetOrCreate(key, () => Bus());
        Assert.Single(hierarchy.Buses);
        Assert.False(first.Touched);

        var again = hierarchy.GetOrCreate(key, () => Bus());
        Assert.Same(first, again);
        Assert.Single(hierarchy.Buses);
        Assert.True(again.Touched);          // D2.1: reused and +0x1CC b0 set

        var other = hierarchy.GetOrCreate(new WwiseMixBusKey(9, 8, 7, 6), () => Bus());
        Assert.NotSame(first, other);
        Assert.Equal(2, hierarchy.Buses.Count);
    }

    // ------------------------------------------------------------------ D2.2 lazy FX

    /// <summary>
    /// M6-014 / D2.2 (GetResultingBuffer 0xA4FEF8, SetInsertFx 0xA4F754): the FX chain is not created until
    /// the first GetResultingBuffer, and that pass does Init then Reset before anything else.
    /// </summary>
    [Fact]
    public void TheFxChainIsInstantiatedLazilyAtTheFirstGetResultingBuffer()
    {
        var fx = new RecordingFx();
        var bus = Bus(new WwiseBusFxSlot(() => fx));
        var sink = new ChunkSink();

        Assert.False(bus.IsFxInstantiated);
        Assert.Empty(fx.Calls);

        bus.GetResultingBuffer(sink.Receive);   // state 4 (nothing mixed): instantiate only, no execute (D2.2/D2.4)

        Assert.True(bus.IsFxInstantiated);
        Assert.Equal(new[] { "init", "reset" }, fx.Calls);
    }

    /// <summary>
    /// M6-014 / D2.2: because the instantiation happens in the same bus pass that first executes the FX,
    /// Init (Hijack Init → PrepareAudioBuffer) runs just before the first Execute, in that order.
    /// </summary>
    [Fact]
    public void InitRunsJustBeforeTheFirstExecute()
    {
        var fx = new RecordingFx();
        var bus = Bus(new WwiseBusFxSlot(() => fx));

        bus.MixInput();                          // D2.5: state 4 -> 1
        bus.GetResultingBuffer(new ChunkSink().Receive);

        Assert.Equal(new[] { "init", "reset", "execute" }, fx.Calls);
    }

    // ------------------------------------------------------------------ D2.4 FX run only while state is 1

    /// <summary>
    /// M6-014 / D2.4 (0xA4FD84): the FX run only when <c>state +0x1BC == 1</c>. An idle bus (state 4) runs
    /// none; the frame after mixing runs them; the idle frame after that runs none again.
    /// </summary>
    [Fact]
    public void TheFxRunOnlyWhileTheStateIsOne()
    {
        var fx = new RecordingFx();
        var bus = Bus(new WwiseBusFxSlot(() => fx));
        var sink = new ChunkSink();

        bus.GetResultingBuffer(sink.Receive);    // state 4
        Assert.DoesNotContain("execute", fx.Calls);

        bus.MixInput();                          // state 1
        bus.GetResultingBuffer(sink.Receive);
        Assert.Single(fx.ExecutedValidFrames);
        bus.ReleaseBuffer();
        Assert.Equal(1, bus.State);              // D2.5: mixed frame releases to 1

        bus.GetResultingBuffer(sink.Receive);    // state still 1
        Assert.Equal(2, fx.ExecutedValidFrames.Count);
        bus.ReleaseBuffer();
        Assert.Equal(4, bus.State);              // D2.5: no mixing, eState was 0x11 -> 4

        bus.GetResultingBuffer(sink.Receive);    // state 4 again
        Assert.Equal(2, fx.ExecutedValidFrames.Count);
    }

    // ------------------------------------------------------------------ D2.5 state machine

    /// <summary>
    /// M6-014 / D2.5 (0xA4F9E0/0xA4FBEC, ReleaseBuffer 0xA4F36C): mixing sets eState 0x2D, state 4 → 1 and
    /// the frame count to max; ReleaseBuffer then sets state to 4 only when eState is 0x11, sets eState 0x11,
    /// clears the frame count and zeroes the buffer.
    /// </summary>
    [Fact]
    public void ReleaseBufferSetsStateFromEStateAndZeroesTheBuffer()
    {
        var bus = Bus();
        Array.Fill(bus.Buffer, 1f);

        bus.MixInput();
        Assert.Equal(0x2D, bus.EState);
        Assert.Equal(1, bus.State);
        Assert.Equal(1024, bus.Frames);

        bus.ReleaseBuffer();
        Assert.Equal(1, bus.State);              // eState was 0x2D, not 0x11
        Assert.Equal(0x11, bus.EState);
        Assert.Equal(0, bus.Frames);
        Assert.All(bus.Buffer, v => Assert.Equal(0f, v));

        // The next frame has no mixing, so eState is already 0x11: this frame goes idle.
        bus.ReleaseBuffer();
        Assert.Equal(4, bus.State);
        Assert.Equal(0x11, bus.EState);
    }

    /// <summary>
    /// M6-014 / D2.5: an out-of-place, non-bypassed FX slot overrides the released state with its own state
    /// (the last such slot wins); an in-place slot does not, which is the shipped Hijack's case (D2.8).
    /// </summary>
    [Fact]
    public void AnOutOfPlaceSlotStateOverridesTheBusState()
    {
        var outOfPlace = new RecordingFx(isInPlace: false, state: 7);
        var outBus = Bus(new WwiseBusFxSlot(() => outOfPlace));
        outBus.MixInput();
        outBus.GetResultingBuffer(new ChunkSink().Receive);
        outBus.ReleaseBuffer();
        Assert.Equal(7, outBus.State);

        var inPlace = new RecordingFx(isInPlace: true, state: 7);
        var inBus = Bus(new WwiseBusFxSlot(() => inPlace));
        inBus.MixInput();
        inBus.GetResultingBuffer(new ChunkSink().Receive);
        inBus.ReleaseBuffer();
        Assert.Equal(1, inBus.State);
    }

    // ------------------------------------------------------------------ D2.3 dropped slot

    /// <summary>
    /// M6-014 / D2.3: an unregistered plug-in's create returns nothing and the slot is dropped (InsertFx
    /// 0xA4E974 at 0xA4EA90..0xA4EAA0), so the bus instantiates no effect and never crashes on it.
    /// </summary>
    [Fact]
    public void AnUnregisteredSlotIsDropped()
    {
        var live = new RecordingFx();
        var bus = Bus(new WwiseBusFxSlot(() => null), new WwiseBusFxSlot(() => live));

        bus.MixInput();
        bus.GetResultingBuffer(new ChunkSink().Receive);

        Assert.True(bus.IsFxInstantiated);
        Assert.Single(live.ExecutedValidFrames);   // only the registered slot ran
    }

    /// <summary>
    /// M6-014 / D2.4: a bypassed slot (<c>slot b0</c> or <c>+0x1B8 b0</c>) is not executed; it is Reset once
    /// on the transition (gapC 3.1's slot bit1 latch), so repeated bypassed frames do not keep resetting it.
    /// </summary>
    [Fact]
    public void ABypassedSlotIsNotExecutedAndIsResetOnce()
    {
        var fx = new RecordingFx();
        var bus = Bus(new WwiseBusFxSlot(() => fx, Bypassed: true));

        bus.MixInput();
        bus.GetResultingBuffer(new ChunkSink().Receive);
        Assert.DoesNotContain("execute", fx.Calls);
        int resetsAfterFirst = fx.Calls.Count(c => c == "reset");
        Assert.True(resetsAfterFirst >= 1);        // D2.2's instantiation Reset at least

        bus.GetResultingBuffer(new ChunkSink().Receive);
        Assert.Equal(resetsAfterFirst, fx.Calls.Count(c => c == "reset"));
    }

    // ------------------------------------------------------------------ D2.8/D2.9 tail

    /// <summary>
    /// M6-014 / D2.8, D2.9 (Hijack Execute 0x8DBFE8, CAkResampler 0xA47178, UpdateBuffer 0x005985FC): after
    /// the last voice-data frame, one more frame runs the FX with empty input; the Hijack flushes its partial
    /// chunk (here 0 frames) to UpdateBuffer, which delivers it rather than dropping it, and ReleaseBuffer
    /// then takes the state to 4.
    /// </summary>
    [Fact]
    public void TheEmptyInputTailFrameFlushesThePartialChunkIncludingZeroFrames()
    {
        var fx = new RecordingFx { Chunk = new float[744] };
        var bus = Bus(new WwiseBusFxSlot(() => fx));
        var sink = new ChunkSink();

        // Frame N: the last voice data. The voice is connected while it renders, then gone.
        bus.Connect();
        bus.MixInput();
        bus.GetResultingBuffer(sink.Receive);
        bus.ReleaseBuffer();
        bus.Disconnect();
        Assert.Equal(new[] { 1024 }, fx.ExecutedValidFrames);
        Assert.Single(sink.Chunks);
        Assert.Equal(744, sink.Chunks[0].Length);
        Assert.False(bus.RemoveIdle());            // D2.6: state 1, kept

        // Frame N+1: nothing mixed, state is still 1, so the FX run once with 0 valid frames and flush 0.
        fx.Chunk = Array.Empty<float>();
        bus.GetResultingBuffer(sink.Receive);
        Assert.Equal(new[] { 1024, 0 }, fx.ExecutedValidFrames);
        Assert.Equal(2, sink.Chunks.Count);
        Assert.Empty(sink.Chunks[1]);              // D2.9: n = 0 is delivered, not dropped

        bus.ReleaseBuffer();
        Assert.Equal(4, bus.State);                // D2.8: ReleaseBuffer sets state 4
    }

    // ------------------------------------------------------------------ D2.6 destruction

    /// <summary>
    /// M6-014 / D2.6 (0xA43F64, teardown 0xA4ECE4): after a frame with state ≠ 1, no connections and b0
    /// clear, the bus is destroyed and every instantiated slot is Terminated (the Hijack's CloseAudioBuffer).
    /// </summary>
    [Fact]
    public void TheBusIsDestroyedAfterTheIdleFrameWithNoConnections()
    {
        var fx = new RecordingFx();
        var hierarchy = new WwiseMixBusHierarchy();
        var key = new WwiseMixBusKey(1, 2, 3, 4);
        var bus = hierarchy.GetOrCreate(key, () => Bus(new WwiseBusFxSlot(() => fx)));

        // Frame N: mixed, connected, reused (touched) — kept even though it releases to state 1.
        bus.Connect();
        bus.MixInput();
        bus.GetResultingBuffer(new ChunkSink().Receive);
        bus.ReleaseBuffer();
        bus.Disconnect();
        Assert.Equal(0, hierarchy.RemoveIdle());
        Assert.False(bus.Touched);                 // D2.6: kept, b0 cleared

        // Frame N+1: no mixing, no connections, not reused — destroyed, Term called.
        bus.GetResultingBuffer(new ChunkSink().Receive);
        bus.ReleaseBuffer();
        Assert.Equal(1, hierarchy.RemoveIdle());
        Assert.Empty(hierarchy.Buses);
        Assert.Contains("term", fx.Calls);
    }

    /// <summary>
    /// M6-014 / D2.6: a bus that is still connected, or was reused this frame, is kept even after an idle
    /// frame; the destruction predicate needs all three of state ≠ 1, connections == 0 and b0 clear.
    /// </summary>
    [Fact]
    public void AConnectedOrReusedBusSurvivesTheIdleFrame()
    {
        var connected = Bus();
        connected.Connect();
        connected.MixInput();
        connected.GetResultingBuffer(new ChunkSink().Receive);
        connected.ReleaseBuffer();
        connected.GetResultingBuffer(new ChunkSink().Receive);   // idle tail, state still 1 -> then 4
        connected.ReleaseBuffer();
        Assert.False(connected.RemoveIdle());                    // connections != 0

        var reused = Bus();
        reused.MixInput();
        reused.GetResultingBuffer(new ChunkSink().Receive);
        reused.ReleaseBuffer();
        reused.MarkTouched();                                    // D2.1: GetOrCreateMixBus reused it this frame
        reused.GetResultingBuffer(new ChunkSink().Receive);
        reused.ReleaseBuffer();
        Assert.False(reused.RemoveIdle());                       // b0 set
        Assert.False(reused.Touched);                            // cleared by the keep
        Assert.True(reused.RemoveIdle());                        // now the predicate holds
    }

    /// <summary>
    /// M6-014 / D2.1, D2.6: a child bus connects to its parent (parent <c>+0x1C0++</c>) and destruction
    /// disconnects from the parent (<c>+0x1C0--</c>).
    /// </summary>
    [Fact]
    public void AChildBusDisconnectsFromItsParentWhenDestroyed()
    {
        var parent = Bus();
        var child = Bus();
        child.ConnectToParent(parent);
        Assert.Equal(1, parent.Connections);

        child.MixInput();
        child.GetResultingBuffer(new ChunkSink().Receive);
        child.ReleaseBuffer();
        Assert.False(child.RemoveIdle());          // state 1, kept

        child.GetResultingBuffer(new ChunkSink().Receive);
        child.ReleaseBuffer();
        Assert.True(child.RemoveIdle());           // D2.6: disconnect from parent then destroy
        Assert.Equal(0, parent.Connections);
        Assert.Null(child.Parent);
    }

    // ------------------------------------------------------------------ D2.7 full-frame driver

    /// <summary>
    /// M6-014 / D2.7, D2.8: the per-frame order the class exposes — the voice pass mixes (state 4 → 1), the
    /// bus pass runs GetResultingBuffer then ReleaseBuffer, and the idle sweep destroys the bus only after
    /// the empty-input tail frame. Two overlapping events on one bus share the single instance, and the next
    /// activity opens a new one.
    /// </summary>
    [Fact]
    public void OneBusServesContiguousVoiceActivityAndIsRecreatedAfterAnIdleFrame()
    {
        var hierarchy = new WwiseMixBusHierarchy();
        var key = new WwiseMixBusKey(1, 2, 3, 4);
        int made = 0;
        WwiseMixBus Make() { made++; return Bus(); }

        var first = hierarchy.GetOrCreate(key, Make);
        first.Connect();
        first.MixInput();                                  // voice A
        first.MixInput();                                  // voice B overlaps: same instance
        first.GetResultingBuffer(new ChunkSink().Receive);
        first.ReleaseBuffer();
        Assert.Equal(0, hierarchy.RemoveIdle());
        Assert.Equal(1, made);

        first.Disconnect();                                // the last voice ends
        first.GetResultingBuffer(new ChunkSink().Receive); // the tail frame
        first.ReleaseBuffer();
        Assert.Equal(1, hierarchy.RemoveIdle());
        Assert.Equal(0, hierarchy.Buses.Count);

        var second = hierarchy.GetOrCreate(key, Make);     // a new activity opens a new instance
        Assert.NotSame(first, second);
        Assert.Equal(2, made);
    }
}
