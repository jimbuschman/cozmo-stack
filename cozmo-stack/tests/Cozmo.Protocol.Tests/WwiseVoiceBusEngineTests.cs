using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-022 (corrections C12/C15): the live voice/bus engine's settled pieces. Every expected value is the
/// row's own value or arithmetic applied by hand, never a value read back from the code:
/// <list type="bullet">
/// <item>V8's render order and V14's voice-&gt;bus mix (0xA44630/0xA4FBEC) render a synthetic source into a
/// bus buffer, and V17 routes the bus output to the device sink (0x9E9E78) — the job's completion bar;</item>
/// <item>V7's callees: 0xA4C584, the 0xA022E8/0xA0228C acquire/release pair, and the E8 state branch;</item>
/// <item>V26's three-component interpolation and the 0x9FD910 completion tail (C15 V26-tail);</item>
/// <item>V28's five LFO shapes, the C15 Y1 coefficient recompute, the pool/compaction and the 0x9E52F8
/// transition ramp;</item>
/// <item>V12-vt's voice insert-FX slot vtable;</item>
/// <item>V18's metering scalar flow with the bit-2 filter identity kept as an explicit non-throwing gap.</item>
/// </list>
/// </summary>
public sealed class WwiseVoiceBusEngineTests
{
    /// <summary>A synthetic mono source: a constant, at the mix rate, so no voice resampler is needed.</summary>
    private sealed class ConstantSource : IWwiseVoiceSource
    {
        private readonly float _value;
        public ConstantSource(float value, int frames) { _value = value; Frames = frames; }
        public int Frames { get; }
        public int Channels => 1;
        public int SampleRate => WwiseRuntimeSettings.MixRateHz;
        public bool StartStreamSucceeded { get; set; } = true;
        public int StartStream(uint arg1DC, uint arg1E0) => 1;
        public int Render(WwiseVoiceBuffer buffer)
        {
            int n = Math.Min(Frames, buffer.MaxFrames);
            for (int i = 0; i < n; i++) buffer.Channels[0][i] = _value;
            for (int i = n; i < buffer.MaxFrames; i++) buffer.Channels[0][i] = 0f;
            buffer.ValidFrames = n;
            return n == buffer.MaxFrames ? 0x2D : 0x2E;
        }
    }

    private sealed class Target : IWwiseV26Target
    {
        public bool GateFlag4 { get; set; }
        public float V0, V1, V2;
        public void Write(float v0, float v1, float v2) { V0 = v0; V1 = v1; V2 = v2; }
    }

    private sealed class Item : IWwiseV26Item
    {
        public float ValueBase { get; set; }
        public float ValueSlope { get; set; }
        public float Out0Base { get; set; }
        public float Out1Base { get; set; }
        public float Out2Base { get; set; }
        public float Out0Slope { get; set; }
        public float Out1Slope { get; set; }
        public float Out2Slope { get; set; }
        public long CompletionTick { get; set; } = long.MaxValue;
        public List<IWwiseV26Target> TargetList { get; } = new();
        public IReadOnlyList<IWwiseV26Target> Targets => TargetList;
    }

    private static void Close(float expected, float actual, float tolerance = 1e-5f)
        => Assert.True(MathF.Abs(expected - actual) <= tolerance, $"expected {expected:R}, got {actual:R}");

    // ---------------------------------------------------------------- V8 / V14: render a voice into a bus

    /// <summary>
    /// M6-022 V8 (0xA44630) + V14 (0xA4FBEC): a live mono voice with a dry connection renders its source
    /// into the bus buffer at gain 1.0 (M6-012 mono-&gt;mono). The voice's state machine (V7) is on its
    /// settled trivial branch (A=0, E4=0, E8=0, SRC10=1), so the V8 dispatcher runs.
    /// </summary>
    [Fact]
    public void TheVoicePassRendersASyntheticSourceIntoItsDryBus()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        bus.FrameBudget = -1;                                        // V7 0xA55228: a caller budget that does not clear r5
        var deviceState = new WwiseOutputDeviceState();
        var pass = new WwiseVoiceBusPass(buses, deviceState).WithPrePassDoubles();
        pass.PostMixA5495C = _ => { }; pass.PostMixNoDataReadyA55CC4 = (_, _) => { };                  // M6-026 7.2: 0xA55CC4 / 0xA5495C after a mix are unread (test double)
        var owner = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, false);
        pass.SourceOwner = _ => owner;                       // M6-026 7.3: [[voice+0xD4]+0xC], an unmarked PBI (test double)

        var voice = new WwiseLiveVoice(channels: 1, maxFrames: 8)
        {
            Source = new ConstantSource(0.5f, 8),
            OutputGain = 1f,
        };
        var connection = new WwiseVoiceConnection(bus, inputChannels: 1, outputChannels: 1)
        {
            HasAux = false,
            TargetGain = 1f,
        };
        connection.Descriptor.Reserve(1, 1);                          // [conn+0x18] != 0 (C24.4): the descriptor is sized
        // The frame pass sets conn+0x6C bit2 (vt+0x3C default 1), and FadeIn is that live bit (M6-012 2.5): a first
        // update would ramp from 0. This test is about the mix, so the connection is past its first update
        // (the fade-in itself is covered by WwiseVoiceLinkerTests.FadeInIsTheLiveConnectionBit2...).
        connection.Refresh();
        voice.Connections.Add(connection);
        pass.Voices.Add(voice);

        pass.VoicePass();

        Assert.Equal(1, pass.VoicesRendered);
        Assert.Equal(1, bus.State);                                   // MixInput set state 4 -> 1
        for (int i = 0; i < 8; i++) Close(0.5f, bus.Buffer[i]);       // 1.0 mono->mono gain
    }

    /// <summary>
    /// M6-022 V7 (0xA54F1C, C15 V7-a/V7-order): the trivial live branch returns true; the E8 branch runs the
    /// settled <c>0xA553A4</c> path and <c>0xA4C584</c> sets bit2 of every connection's <c>+0x6C</c> from the
    /// saved <c>bus-&gt;vt+0x3C</c> return (0xA554D0), not from <c>voice-&gt;vt+0x58</c> (0xA554D8, discarded).
    /// </summary>
    [Fact]
    public void TheVoiceStateMachineRunsTheSettledE8Path()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        bus.FrameBudget = -1;                                        // V7 0xA55228: a caller budget that does not clear r5
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8) { Source = new ConstantSource(0.5f, 8) };
        var connection = new WwiseVoiceConnection(bus, 1, 1);
        voice.Connections.Add(connection);
        bus.SourceRequest3C = _ => 1;                                // 0xA553A4 accepted
        voice.VoiceBit58 = () => false;                              // 0xA554D8 return is discarded

        Assert.True(pass.RunVoiceStateMachine(voice));

        voice.FlagE8 = true;                                         // 0xA55218 -> 0xA553A4
        Assert.True(pass.RunVoiceStateMachine(voice));
        Assert.True((connection.Flags6C & 0x04) != 0);               // 0xA4C584 bit2 from the bus return, not vt+0x58
        Assert.False(voice.FlagE8);                                  // 0xA553C8 cleared
    }

    // ---------------------------------------------------------------- V18: the metering gap is explicit

    /// <summary>
    /// M6-022 V18 tail (C12 X1, C15 residual, missing-bodies item 3): the scalar flow is modelled and the
    /// bit-2 filter identity is an explicit gap that does not throw. With no meter attached the tail is
    /// skipped, as the native does when <c>[out+0x18]</c> is null.
    /// </summary>
    [Fact]
    public void TheBusMeteringModelsTheScalarFlowAndKeepsTheFilterGap()
    {
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        WwiseBusMetering.Run(bus);                                   // no meter: skipped, no throw

        bus.Buffer[0] = 0.5f; bus.Buffer[1] = -0.25f;
        bus.MixInput();                                              // Frames = MaxFrames (D2.5)
        var meter = new WwiseBusMeter
        {
            Channels = 1,
            Flags = WwiseBusMetering.PeakFlag | WwiseBusMetering.FilteredPeakFlag,
        };
        bus.Meter = meter;
        bus.OutputGain = 1f;
        WwiseBusMetering.Run(bus);                                   // 8 valid frames

        // bit0 peak: max(|min|,max)*gain*1.0009619, the source literal at 0xA50FD4.
        Close(0.5f * 1.0009619f, meter.Peak[0], 1e-6f);
        // bit1 filtered peak: no kernel -> the documented gap, no throw, value 0.
        Assert.True(meter.FilterKernelMissing);
        Assert.Equal(0f, meter.FilteredPeak[0]);
    }

    // ---------------------------------------------------------------- V21: the PBI Term code

    /// <summary>
    /// M6-022 V21 (0xA38420, C12 X6): the flush tests the message <b>code</b> (<c>[item+8]</c>), and code 4
    /// is Term; the reason is <c>[item+0xC]</c>. The Term path runs unlink/0x9D3470/vt+0x10/vt+4/free.
    /// </summary>
    [Fact]
    public void ThePbiFlushTerminatesOnlyCodeFourNotifications()
    {
        // The queue Q is the engine's (its push is checked against the engine in WwiseNotificationQueueTests); its init is unread, so the test sets it up.
        var queue = new WwiseNotificationQueue(new WwiseBankMemory(), 2, 8, new[] { 0, 1 });
        var handled = new List<int>();
        var terminated = new List<int>();
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState())
        {
            Notifications = queue,
            NotificationHandlerA0188C = (_, _, r2, _) => handled.Add(r2),
            TerminateNotifiedPbiA384C8 = p => terminated.Add((int)p!),
        };

        queue.Push(1, WwisePbiNotification.TermCode, 7, 0);
        queue.Push(2, 9, 3, 0);

        pass.FlushPbiNotifications();

        Assert.Equal(new[] { 7, 3 }, handled);
        Assert.Equal(new[] { 1 }, terminated);                       // only code 4
    }

    // ---------------------------------------------------------------- V26: the three-component interpolation

    /// <summary>
    /// M6-022 V26 (0x9FDD90, C12 bus-group Q5): <c>val = [obj+0x40] + tick*[obj+0x3c]</c>, clamped to
    /// [0,1]; <c>out_i = base_i + t*slope_i</c>; a target with <c>([target+0x3c] &amp; 4) != 0</c> is not
    /// written.
    /// </summary>
    [Fact]
    public void TheV26MemberInterpolatesAndSkipsGatedTargets()
    {
        var item = new Item
        {
            ValueBase = 0.25f,
            ValueSlope = 0.5f,
            Out0Base = 1f, Out0Slope = 10f,
            Out1Base = 2f, Out1Slope = 20f,
            Out2Base = 3f, Out2Slope = 30f,
        };
        var written = new Target();
        var gated = new Target { GateFlag4 = true };
        item.TargetList.Add(written);
        item.TargetList.Add(gated);

        var member = new WwiseGroupMemberV26();
        member.Items.Add(item);

        member.Tick(1);                                              // t = 0.25 + 0.5 = 0.75

        Close(1f + 0.75f * 10f, written.V0);
        Close(2f + 0.75f * 20f, written.V1);
        Close(3f + 0.75f * 30f, written.V2);
        Assert.Equal(0f, gated.V0);                                  // gated: untouched

        // The upper clamp: at tick 4, val = 0.25+2 = 2.25 -> t = 1.
        member.Tick(4);
        Close(11f, written.V0);
    }

    /// <summary>
    /// M6-022 V26 tail (0x9FD910, C15 V26-tail, missing-bodies item 5): on completion the member picks the
    /// next table row, sets the base from the row plus the per-component jitter, the slopes to
    /// <c>target-base</c>, the segment length <c>max(1,(G+e3-1)/G)</c> and the rate <c>1/len</c>. The
    /// divisor identity is UNKNOWN, so 0 selects the documented G=1 gap.
    /// </summary>
    [Fact]
    public void TheV26CompletionTailAdvancesTheRow()
    {
        var item = new CompletionItem
        {
            CompletionTick = 5,
            Threshold34 = 5f,
            RowIndex14 = 0,
        };
        item.SetTable(new V26Table(2, (1f, 2f, 3f, 4f), (5f, 6f, 7f, 8f)));
        var member = new WwiseGroupMemberV26();
        member.Items.Add(item);

        member.Tick(4);
        Assert.Equal(0, item.RowIndex14);                            // not due

        member.Tick(5);                                              // due: 0x9FD910

        Assert.Equal(1, item.RowIndex14);                            // advanced past row 0
        Assert.Equal(4, item.SegmentLength38);                       // max(1,(1+4-1)/1)
        Close(0.25f, item.Rate3c);                                   // 1/len
        // slopes = target - base; base = row 0 (zero table slopes) -> (5-1,6-2,7-3).
        Close(4f, item.Out0Slope);
        Close(4f, item.Out1Slope);
        Close(4f, item.Out2Slope);
        Assert.True(member.CompletionDivisorGap);                    // G identity UNKNOWN
    }

    // ---------------------------------------------------------------- V28: the five shapes

    /// <summary>
    /// M6-022 V28 (0x9E416C, modulator-evaluator Q1.7): the sine polynomial is
    /// <c>0.5*(1+sin)</c>; at phase 0 it is 0.5, at pi/2 it is 1, at pi it is 0.5, at 3pi/2 it is 0.
    /// </summary>
    [Fact]
    public void TheLfoSineShapeIsTheFoldedPolynomial()
    {
        Close(0.5f, WwiseLfoShapes.Sine(0.0));
        Close(1.0f, WwiseLfoShapes.Sine(Math.PI / 2), 1e-4f);
        Close(0.5f, WwiseLfoShapes.Sine(Math.PI), 1e-4f);
        Close(0.0f, WwiseLfoShapes.Sine(3.0 * Math.PI / 2), 1e-4f);
    }

    /// <summary>M6-022 V28 (0x9E4024/0x9E3ECC/0x9E3E1C/0x9E36D4): the four normalized-phase shapes.</summary>
    [Fact]
    public void TheFourNormalizedShapesMatchTheRows()
    {
        Close(0.0f, WwiseLfoShapes.Triangle(0.0));
        Close(1.0f, WwiseLfoShapes.Triangle(0.5));
        Close(1.0f, WwiseLfoShapes.Square(0.25));
        Close(0.0f, WwiseLfoShapes.Square(0.75));
        Close(0.25f, WwiseLfoShapes.SawUp(0.25));
        Close(0.75f, WwiseLfoShapes.SawDown(0.25));
    }

    /// <summary>
    /// M6-022 V28 (0x9E35C0..0x9E3664, C15 Y1): the coefficient recompute is now settled. Damping 0 is the
    /// pass-through (1,0) (0x9E5004); for <c>f=24000, damping=1</c> the hand arithmetic gives
    /// <c>t=1</c>, <c>w=pi</c>, <c>cos w=-1</c>, <c>a=3</c>, <c>c2=sqrt(8)-3</c>, <c>c1=c2+1</c>.
    /// </summary>
    [Fact]
    public void TheLfoCoefficientRecomputeMatchesC15()
    {
        var zero = WwiseLfoCoefficients.FromFrequencyAndDamping(440f, 0f);
        Close(1f, zero.C1);
        Close(0f, zero.C2);

        var c = WwiseLfoCoefficients.FromFrequencyAndDamping(24000f, 1f);
        float c2 = (float)(Math.Sqrt(8.0) - 3.0);                    // a=3, arg=a*a-1=8
        Close(c2, c.C2, 1e-6f);
        Close(c2 + 1f, c.C1, 1e-6f);
    }

    /// <summary>
    /// M6-022 V28 (modulator-evaluator Q1.7): the recurrence is
    /// <c>out[n] = gain[n] * shape(phase) * c1 - out[n-1] * c2</c>. With <c>c1 = 1, c2 = 0</c> and gain 1
    /// it is the shape itself, so the saw-up shape gives the phase sequence.
    /// </summary>
    [Fact]
    public void TheLfoRecurrenceIsTheSettledScalarForm()
    {
        var lfo = new WwiseLfoEvaluator { Coefficients = new WwiseLfoCoefficients(1f, 0f) };
        var output = new float[4];
        lfo.Evaluate(WwiseLfoShape.SawUp, phaseIncrement: 0.25, gainStart: 1f, gainStep: 0f, output);

        Close(0.0f, output[0]);
        Close(0.25f, output[1]);
        Close(0.5f, output[2]);
        Close(0.75f, output[3]);
    }

    // ---------------------------------------------------------------- the completion bar: render to the device

    /// <summary>
    /// The job's completion bar: a posted event's synthetic source is rendered through the voice/bus pass and
    /// routed to the robot's output buffer. The bus has no output bus, so the V17 device path runs
    /// <c>0x9E9E78</c> and the device sink receives the mixed frame (never decoded Vorbis).
    /// </summary>
    [Fact]
    public void TheBusPassRoutesTheRenderedVoiceToTheRobotOutputBuffer()
    {
        var buses = new WwiseMixBusHierarchy();
        var key = new WwiseMixBusKey(0, 0, 7, 0);
        var bus = buses.GetOrCreate(key, () => new WwiseMixBus(key, Array.Empty<WwiseBusFxSlot>(), 8));
        bus.FrameBudget = -1;                                        // V7 0xA55228: a caller budget that does not clear r5
        var deviceState = new WwiseOutputDeviceState();
        var sink = new RecordingSink();
        var device = new SinkDevice { DeviceKey28 = 7, Sink = sink };
        deviceState.AddDevice(device);
        var pass = new WwiseVoiceBusPass(buses, deviceState).WithPrePassDoubles();
        pass.PostMixA5495C = _ => { }; pass.PostMixNoDataReadyA55CC4 = (_, _) => { };                  // M6-026 7.2: 0xA55CC4 / 0xA5495C after a mix are unread (test double)
        var owner = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, false);
        pass.SourceOwner = _ => owner;                       // M6-026 7.3: [[voice+0xD4]+0xC], an unmarked PBI (test double)

        var voice = new WwiseLiveVoice(1, 8) { Source = new ConstantSource(0.5f, 8), OutputGain = 1f };
        var dry = new WwiseVoiceConnection(bus, 1, 1) { TargetGain = 1f };
        dry.Descriptor.Reserve(1, 1);                                // [conn+0x18] != 0 (C24.4)
        dry.Refresh();                                               // past the first update: bit2 is the live fade-in bit
        voice.Connections.Add(dry);
        pass.Voices.Add(voice);

        pass.VoicePass();                                            // V8/V14: source -> bus buffer
        pass.BusPass(deviceState.BusPassArg);                        // V17: bus -> 0x9E9E78 -> device sink

        Assert.Equal(1, pass.VoicesRendered);
        Assert.Equal(8, sink.Frames);
        Close(0.5f, sink.Samples[0]);
        Close(0.5f, sink.Samples[7]);
        Assert.True(device.ReleaseCalls > 0);                        // 0x9E9F08 walk
    }

    // ---------------------------------------------------------------- V28 pool/record layer

    /// <summary>
    /// M6-022 V28 (0x9E2BD0, modulator-evaluator Q1.1/Q1.9): the pool clears the used counts and the
    /// per-call needed length, grows the float buffer lazily, and the tail compaction frees a node when
    /// <c>used &lt; capacity/2</c> (0x9E2F4C..0x9E3150).
    /// </summary>
    [Fact]
    public void TheModulatorPoolCompactsUnderHalfCapacity()
    {
        var pool = new WwiseModulatorPool();
        pool.Clear();                                                // 0x9E2BDC: reset used/needed
        var record = new WwiseModulatorRecord0
        {
            C1_34 = 1f, C2_38 = 0f, Shape48 = (int)WwiseLfoShape.SawUp, PhaseInc44 = 0.25f, F30 = 1f,
        };
        pool.AddType0(record);

        pool.EvaluateType0(4);

        Close(0.0f, record.Output[0]);
        Close(0.25f, record.Output[1]);
        Close(0.5f, record.Output[2]);
        Close(0.75f, record.Output[3]);

        // One used record in a 16-slot chunk is below half capacity, so the compaction frees the node.
        Assert.Equal(1, pool.Compact());
        Assert.Null(pool.Type0Head);
    }

    /// <summary>
    /// M6-022 V28 (0x9E52F8, modulator-evaluator Q2, C15 Y2): the type-1 ramp is scalar VFP (no NEON lane
    /// order); the five settled segments are <c>L0/L1/L2/L3/L4</c> and the curve completes when the call's
    /// sample count is consumed (<c>n == 0</c>) or <c>t &gt;= L4</c>. With <c>L0=4</c> and 4 samples the
    /// stage-A zero fill consumes the block, so the state ends complete.
    /// </summary>
    [Fact]
    public void TheTransitionRampWritesTheSegments()
    {
        var record = new WwiseModulatorRecord1
        {
            Count04 = 4,
            Count08 = 4,
            Time0c = 0,
            I1c = 4,
            Breakpoint20 = 1f,
            Sustain24 = 0.5f,
            Count28 = 4,
            Count2c = 4,
            Start18 = 0f,
        };
        var output = new float[4];
        WwiseTransitionRamp.Evaluate(record, 4, output);

        // L0 = 4, so the first block is stage A zeros; n is consumed, so the curve is complete.
        Close(0f, output[0]);
        Close(0f, output[1]);
        Close(0f, output[2]);
        Close(0f, output[3]);
        Assert.Equal(3, record.State.Status);                        // 0x9E57E8 n == 0
    }

    // ---------------------------------------------------------------- V7 callees

    /// <summary>
    /// M6-022 V7-b (C15 V7-b): <c>0xA0228C</c> sets bit5 and increments the counters; <c>0xA022E8</c> is the
    /// exact inverse and clears bit5.
    /// </summary>
    [Fact]
    public void TheBusRefAcquireReleasePairInverts()
    {
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8)
        {
            RefCountArray1EC = new[] { 0, 0 },
            RefCount1F0 = 2,
        };
        WwiseVoiceBusPass.BusRefGlobal = 0;                          // process-wide native global; isolate

        WwiseVoiceBusPass.AcquireBusRef(bus);
        Assert.True((bus.Flags1BE & 0x20) != 0);
        Assert.Equal(new[] { 1, 1 }, bus.RefCountArray1EC);
        Assert.Equal(1, WwiseVoiceBusPass.BusRefGlobal);

        WwiseVoiceBusPass.ReleaseBusRef(bus);
        Assert.False((bus.Flags1BE & 0x20) != 0);
        Assert.Equal(new[] { 0, 0 }, bus.RefCountArray1EC);
        Assert.Equal(0, WwiseVoiceBusPass.BusRefGlobal);
    }

    /// <summary>
    /// M6-022 V12-vt (C15 V12-vt): the voice insert-FX slot stores vtable <c>0x103DC38</c> when a plug-in is
    /// present and <c>0x103DB98</c> otherwise.
    /// </summary>
    [Fact]
    public void TheVoiceInsertFxSlotStoresTheC15Vtable()
    {
        var slot = new WwiseVoiceInsertFxSlot();
        Assert.Equal(WwiseVoiceInsertFxSlot.NoPluginVtable, slot.Vtable);
        slot.HasPlugin = true;
        Assert.Equal(WwiseVoiceInsertFxSlot.PluginVtable, slot.Vtable);
        slot.Initialise();
        Assert.True(slot.Initialised);
    }

    // ---------------------------------------------------------------- V7: the P2F==0 branch 0xA5532C

    /// <summary>
    /// M6-022 V7 (0xA5532C, report voice-callees Q4 lines 218-227): the <c>P2F==0</c> path reached from
    /// <c>0xA550C8</c>. A first-frame voice (cd8 clear, so <c>0xA4BC58</c> returns P2F=0) with <c>E4==1</c>
    /// runs <c>voice-&gt;vt+0x48</c>, takes the shared tail (<c>[voice+0xCD] |= 8</c>) and returns 0.
    /// </summary>
    [Fact]
    public void TheP2FZeroPathStopsOnE4One()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8) { Source = new ConstantSource(0.5f, 8), E4 = 1 };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1));
        bool stopped = false;
        voice.VoiceStop48 = () => stopped = true;

        Assert.False(pass.RunVoiceStateMachine(voice));              // 0xA55344 -> 0xA5521C tail
        Assert.True(stopped);
        Assert.True((voice.FlagsCD & 8) != 0);                       // [voice+0xCD] |= 8
    }

    /// <summary>
    /// M6-022 V7 (0xA5532C, report Q4 lines 218-227): <c>E4==2</c>, <c>A==0</c>, <c>[voice+0xE0]==1</c>:
    /// when <c>[bus+0x1D8] &lt; s</c> the path calls <c>voice+0x1C0-&gt;vt+0x10(&amp;s)</c> with the
    /// <c>0xA55090</c> sample count; then the shared tail. Here s = round(4 * 1.0) = 4 and the budget is 2.
    /// </summary>
    [Fact]
    public void TheP2FZeroPathCallsFilterRequest10WithTheSampleCount()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8)
        {
            Source = new ConstantSource(0.5f, 8),
            E4 = 2,
            E0 = 1,
        };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1));
        voice.Buffer.ValidFrames = 4;                                // params+0xC
        bus.SampleScale164 = 1f;                                     // bus+0x164
        bus.FrameBudget = 2;                                         // bus+0x1D8 < s
        int? got = null;
        voice.FilterRequest10 = v => { got = v; return v; };          // seam stores params+0x28

        Assert.False(pass.RunVoiceStateMachine(voice));
        Assert.Equal(4, got);
        Assert.Equal(4, voice.Buffer.Result);                        // params+0x28 = vt+0x10 return
        Assert.True((voice.FlagsCD & 8) != 0);
    }

    /// <summary>
    /// M6-022 V7 (0xA5532C, report Q4 lines 218-227): <c>E4==2</c>, <c>A=[voice+0xCD]&amp;1</c> set:
    /// <c>voice+0x1C0-&gt;vt+0x14(E0)</c> runs, and <c>voice+0x1C0-&gt;vt+0xC</c> runs only when
    /// <c>[voice+0xE0] != 2</c>.
    /// </summary>
    [Fact]
    public void TheP2FZeroPathRunsTheFilterRequestsInOrder()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8)
        {
            Source = new ConstantSource(0.5f, 8),
            E4 = 2,
            E0 = 5,
            FlagsCD = 1,                                             // A set, cd8 clear
        };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1));
        bool req14 = false, req0c = false;
        voice.FilterRequest14 = _ => { req14 = true; return 0; };
        voice.FilterRequest0C = () => req0c = true;

        Assert.False(pass.RunVoiceStateMachine(voice));
        Assert.True(req14);
        Assert.True(req0c);

        req14 = req0c = false;
        voice.E0 = 2;                                                // ==2: only vt+0x14
        voice.FlagsCD = 1;
        Assert.False(pass.RunVoiceStateMachine(voice));
        Assert.True(req14);
        Assert.False(req0c);
    }

    /// <summary>
    /// M6-022 V7 (0xA5532C, report Q4 lines 218-227): <c>E4</c> not 1 and not 2 continues at
    /// <c>0xA55218</c> with <c>r5=1</c>, so the voice is live. The budget is set so the later step does not
    /// clear r5 (C16 V7-i).
    /// </summary>
    [Fact]
    public void TheP2FZeroPathContinuesForTheOtherE4Codes()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        bus.FrameBudget = -1;
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8) { Source = new ConstantSource(0.5f, 8), E4 = 0 };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1));

        Assert.True(pass.RunVoiceStateMachine(voice));               // -> 0xA55218
    }

    /// <summary>
    /// M6-022 V7-l (C17 V7-l): the <c>P2F==0</c> path joins the common continuation at <c>0xA5521C</c>, so
    /// the E8 gate <c>0xA553A4</c> runs for it too. A first-frame voice (cd8 clear, connection bit1 set so
    /// <c>fp=0</c> gives <c>P2F=0</c>) with E8 set calls <c>bus-&gt;vt+0x3C</c> and clears E8 bit0; the
    /// return-1 path sets connection bit2 from that bus return even though <c>voice-&gt;vt+0x58</c> is false.
    /// </summary>
    [Fact]
    public void TheP2FZeroPathRunsTheCommonContinuationE8Gate()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8) { Source = new ConstantSource(0.5f, 8), E4 = 1, FlagE8 = true };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0x02 });  // fp = 0 -> P2F = 0
        voice.VoiceRequest3C = () => 0;
        bool called = false;
        bus.SourceRequest3C = _ => { called = true; return 1; };      // 0xA553A4/0xA553B4
        voice.VoiceBit58 = () => false;

        Assert.False(pass.RunVoiceStateMachine(voice));              // E4==1 -> r5=0

        Assert.True(called);                                         // the E8 gate ran on the P2F==0 path
        Assert.True((voice.Connections[0].Flags6C & 0x04) != 0);     // 0xA4C584 bit2 from the bus return
        Assert.False(voice.FlagE8);                                  // 0xA553C8 cleared bit0
        Assert.True((voice.FlagsCD & 8) != 0);                       // 0xA5528C tail
    }

    /// <summary>
    /// M6-022 V7-l (C18, 0xA555A0/0xA555B4/0xA553C8): the E8 gate's <c>bus-&gt;vt+0x3C</c> return-2 path
    /// calls <c>voice-&gt;vt+0x48</c> and sets <c>r5=0</c>, and it still reaches the <c>0xA553C8</c> clear
    /// that drops E8 bit0 before the common continuation.
    /// </summary>
    [Fact]
    public void TheE8GateReturnTwoStopsTheVoiceAndClearsE8()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        bus.FrameBudget = -1;                                        // the budget step does not clear r5
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8) { Source = new ConstantSource(0.5f, 8), FlagE8 = true };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1));
        bool stopped = false;
        voice.VoiceStop48 = () => stopped = true;                    // 0xA555A0 voice->vt+0x48
        bus.SourceRequest3C = _ => 2;                                // 0xA553B4 return 2

        Assert.False(pass.RunVoiceStateMachine(voice));              // 0xA555A0 r5 = 0

        Assert.True(stopped);                                        // the stop path ran
        Assert.False(voice.FlagE8);                                  // 0xA555B4 -> 0xA553C8 cleared bit0
    }

    /// <summary>
    /// M6-022 V7-k (C17 item 3): after the A-set <c>0xA55534</c> branch calls <c>vt+0xC</c>,
    /// <c>0xA55498</c> re-checks <c>[voice+0xE0]==1</c> and branches to <c>0xA5556C</c>, so <c>vt+0x10</c>
    /// runs and its return is stored in <c>params+0x28</c>.
    /// </summary>
    [Fact]
    public void TheP2FZeroPathRechecksE0OneAfterTheASetFilterRequest()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8)
        {
            Source = new ConstantSource(0.5f, 8),
            E4 = 2,
            E0 = 1,
            FlagsCD = 1,                                             // A set
        };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1));
        voice.Buffer.ValidFrames = 4;                                // params+0xC
        bus.SampleScale164 = 1f;                                     // s = 4
        bus.FrameBudget = 2;                                         // [bus+0x1D8] < s
        bool req14 = false, req0c = false;
        voice.FilterRequest14 = _ => { req14 = true; return 0; };
        voice.FilterRequest0C = () => req0c = true;
        voice.FilterRequest10 = v => { Assert.Equal(4, v); return 7; };

        Assert.False(pass.RunVoiceStateMachine(voice));

        Assert.True(req14);                                          // 0xA55544/48
        Assert.True(req0c);                                          // 0xA55560/64 -> 0xA55498
        Assert.Equal(7, voice.Buffer.Result);                        // params+0x28 = vt+0x10 return
    }

    // ---------------------------------------------------------------- V7-m: the four parameter ramps

    /// <summary>
    /// M6-022 V7-m (C17 V7-m): the four parameter ramps run only when <c>P2F!=0</c> and
    /// <c>[voice+0x28]!=0</c>. Each record whose clamped target differs from its stored target sets
    /// <c>flag=1</c>, stores the clamped target and applies <c>cur += (target_old-cur)*0.125*rate</c>.
    /// Ramps 2/4 first <c>max(target,[bus+0x68]/[bus+0x6C])</c>; all clamp to 100 and floor at 0.
    /// </summary>
    [Fact]
    public void TheFourParameterRampsFollowC17V7m()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8) { Source = new ConstantSource(0.5f, 8) };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0 });  // fp = 1 -> P2F = 1
        voice.VoiceRequest3C = () => 0;                              // vt3c==0

        // [sp+0x2e]==0: the copy is skipped and the targets are the 0xA4BC58 minima. Here id==0, so the
        // per-connection loop did not run and the minima are the entry zeros. Ramp 1: 0 != 50 ->
        // cur += (50-10)*0.125*8 = 50.
        voice.Ramp340.Target = 50f; voice.Ramp340.Current = 10f; voice.Ramp340.Rate = 8;
        // Ramp 2: max(0,[bus+0x68]=200) = 200, clamp to 100 -> cur += (50-10)*0.125*8 = 50.
        voice.Ramp510.Target = 50f; voice.Ramp510.Current = 10f; voice.Ramp510.Rate = 8;
        bus.RampFloor68 = 200f;

        Assert.False(pass.RunVoiceStateMachine(voice));              // r5 cleared by the budget step (s=0)

        Assert.Equal(1, voice.Ramp340.Flag);
        Assert.Equal(0f, voice.Ramp340.Target);
        Close(50f, voice.Ramp340.Current);
        Assert.Equal(1, voice.Ramp510.Flag);
        Assert.Equal(100f, voice.Ramp510.Target);
        Close(50f, voice.Ramp510.Current);
        Assert.Equal(0, voice.Ramp350.Flag);                         // target 0 == stored 0: untouched
        Assert.Equal(0, voice.Ramp520.Flag);
    }

    /// <summary>
    /// M6-022 V7-m/V7-p (C17 V7-m, C18 V7-p, <c>0xA5505C..0xA5508C</c>): when
    /// <see cref="WwiseLiveVoice.Run2E"/> (<c>[sp+0x2e]</c>) is non-zero the ramp target is the voice
    /// target field itself, so an out-of-range target is clamped in place (150 -&gt; 100) and the ramp uses
    /// the pre-store target as <c>target_old</c>: <c>10 + (150-10)*0.125*8 = 150</c>. Here the connection's
    /// bit1 is set and <c>cd8</c> is set, so <c>0xA4BC58</c> leaves the aggregate run flag 1 and returns
    /// <c>P2F=1</c>.
    /// </summary>
    [Fact]
    public void TheFourParameterRampsCopyTheVoiceTargetsWhen2EIsSet()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8)
        {
            Source = new ConstantSource(0.5f, 8),
            FlagsCD = 8,                                             // cd8 set
        };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0x02 });  // bit1 set -> run stays 1
        voice.VoiceRequest3C = () => 0;                              // vt3c == 0
        voice.Ramp340.Target = 150f; voice.Ramp340.Current = 10f; voice.Ramp340.Rate = 8;

        Assert.False(pass.RunVoiceStateMachine(voice));

        Assert.True(voice.Run2E);                                    // [sp+0x2e] != 0
        Assert.Equal(1, voice.Ramp340.Flag);
        Assert.Equal(100f, voice.Ramp340.Target);                    // clamped in place
        Close(150f, voice.Ramp340.Current);                          // 10 + (150-10)*1
    }

    /// <summary>
    /// M6-022 V7-m (C17 V7-m): the ramps are gated on <c>P2F!=0</c>. On the P2F==0 path a set target is
    /// left untouched.
    /// </summary>
    [Fact]
    public void TheFourParameterRampsDoNotRunWhenP2FIsZero()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8) { Source = new ConstantSource(0.5f, 8), E4 = 1 };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0x02 });  // fp = 0 -> P2F = 0
        voice.VoiceRequest3C = () => 0;
        voice.Ramp340.Target = 50f; voice.Ramp340.Current = 10f; voice.Ramp340.Rate = 8;

        pass.RunVoiceStateMachine(voice);

        Assert.Equal(0, voice.Ramp340.Flag);
        Assert.Equal(50f, voice.Ramp340.Target);
        Assert.Equal(10f, voice.Ramp340.Current);
    }

    /// <summary>
    /// M6-022 V7-o (C18 V7-o/X3, <c>0xA4BE80..0xA4BF34</c>): the four float minima are zeroed at entry,
    /// set to 100.0 when <c>id != 0</c>, then reduced to the running minima of
    /// <c>[conn+0x50/+0x54/+0x58/+0x5C]</c>. The per-connection copy reads <c>param_2 = [source+0xC]+0xC</c>
    /// (the bus's <c>+0x48/+0x4C</c>), not the voice: <c>[conn+0x50]=[bus+0x48]</c>,
    /// <c>[conn+0x58]=[bus+0x4C]</c>, <c>[conn+0x54]=[conn+0x5C]=0</c>.
    /// </summary>
    [Fact]
    public void TheBc58ConnectionCopyReadsParam2NotTheVoice()
    {
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8) { Param2_3C = 5f, Param2_40 = 7f };
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 8 };
        var connection = new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0 };  // sb clear
        voice.Connections.Add(connection);
        voice.VoiceRequest3C = () => 1;                              // vt3c != 0 -> main loop + tail

        bool p2f = WwiseVoiceBusPass.UpdateConnectionGains(voice, bus, 1f);

        Assert.True(p2f);
        Assert.Equal(5f, connection.C50);                            // [bus+0x48]
        Assert.Equal(7f, connection.C58);                            // [bus+0x4C]
        Assert.Equal(0f, connection.C54);
        Assert.Equal(0f, connection.C5C);
        Assert.Equal(5f, voice.OutputMin50[0]);                      // min(100, 5)
        Assert.Equal(0f, voice.OutputMin50[1]);
        Assert.Equal(7f, voice.OutputMin50[2]);                      // min(100, 7)
        Assert.Equal(0f, voice.OutputMin50[3]);
    }

    /// <summary>
    /// M6-022 V7-p (C18 V7-p): when <c>[sp+0x2e]==0</c> the copy at <c>0xA55068..0xA5508C</c> is skipped
    /// and the four ramp targets are the <c>0xA4BC58</c> minima, not zero. Here <c>id=1</c>, the single
    /// connection's bit1 is clear and <c>vt3c==0</c>, so <c>Run2E=0</c> and the minima are
    /// <c>[5,0,7,0]</c> from <c>[bus+0x48]/[bus+0x4C]</c>.
    /// </summary>
    [Fact]
    public void TheFourParameterRampsUseTheConnectionMinimaWhen2EIsZero()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        bus.Param2_3C = 5f;
        bus.Param2_40 = 7f;
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8) { Source = new ConstantSource(0.5f, 8), Word0xF0 = 1 };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0 });  // bit1 clear
        voice.VoiceRequest3C = () => 0;                              // vt3c == 0 -> Run2E = run = 0
        voice.Ramp340.Target = 50f; voice.Ramp340.Current = 10f; voice.Ramp340.Rate = 8;
        voice.Ramp350.Target = 40f; voice.Ramp350.Current = 10f; voice.Ramp350.Rate = 8;

        pass.RunVoiceStateMachine(voice);

        Assert.False(voice.Run2E);
        Assert.Equal(5f, voice.OutputMin50[0]);
        Assert.Equal(7f, voice.OutputMin50[2]);
        Assert.Equal(5f, voice.Ramp340.Target);                      // the minimum, not 0
        Close(50f, voice.Ramp340.Current);                           // 10 + (50-10)*0.125*8
        Assert.Equal(7f, voice.Ramp350.Target);
        Close(40f, voice.Ramp350.Current);                           // 10 + (40-10)*0.125*8
    }

    /// <summary>
    /// M6-022 V7-q (C18 V7-q, <c>0xA5572C</c>): the second <c>0xA4BC58</c> call on the <c>0xA555F0</c>
    /// completion path passes the same <c>&amp;sp+0x2e</c>/<c>&amp;sp+0x2f</c> but its float outputs go to
    /// <c>sp+0x40</c>, so it rewrites <c>Run2E</c>/<c>P2F</c> after the ramps without touching
    /// <c>OutputMin50</c>. Here the first call sees <c>vt3c=1</c> (<c>Run2E=1</c>, <c>P2F=0</c>) and the
    /// second sees <c>vt3c=0</c> with the connection's bit1 clear (<c>Run2E=0</c>, <c>P2F=1</c>).
    /// </summary>
    [Fact]
    public void TheSecondBc58CallRewritesRun2EAndP2FAfterTheRamps()
    {
        var buses = new WwiseMixBusHierarchy();
        var bus = buses.GetOrCreate(default, () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        bus.FrameBudget = -1;                                        // the budget step does not clear r5
        bus.FlagsE8 = 0x20;                                          // the 0xA555F0 state-0x11 tail runs
        bus.FlagsE9 = 1;
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        var voice = new WwiseLiveVoice(1, 8) { Source = new ConstantSource(0.5f, 8), E4 = 0 };
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0, C60 = 1f });
        int calls = 0;
        voice.VoiceRequest3C = () => calls++ == 0 ? 1 : 0;           // first call Run2E=1, second Run2E=0

        Assert.True(pass.RunVoiceStateMachine(voice));

        Assert.Equal(2, calls);                                      // the 0xA5572C call ran
        Assert.False(voice.Run2E);                                   // rewritten by the second call
        Assert.True(voice.P2F);                                      // rewritten by the second call
    }

    // ---------------------------------------------------------------- V7: 0xA4BC58 tail gating

    /// <summary>
    /// M6-022 V7/C1 (0xA4BC58, C12 voice-callees Q4, finding 3): on the <c>!cd8</c> branch with
    /// <c>vt3c!=0</c> the native overwrites fp with <c>cd8 &amp; 8</c> (=0) at <c>0xA4C05C</c>, so
    /// <c>0xA4BD6C</c> skips both the main loop and the tail. Only <c>SetBit2 = vt3c &amp; 1</c> runs.
    /// </summary>
    [Fact]
    public void TheBc58FirstFrameDefersTheGainsAndSkipsTheTail()
    {
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8) { Param2_3C = 5f };
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 0 };
        bus.ParamsA8B4[0] = 7f;
        bus.Param2_DC = 0x10;                                        // [param_2+0xDC] bit4
        var connection = new WwiseVoiceConnection(bus, 1, 1);
        voice.Connections.Add(connection);
        voice.VoiceRequest3C = () => 1;                              // vt3c != 0

        bool p2f = WwiseVoiceBusPass.UpdateConnectionGains(voice, bus, 1f);

        Assert.False(p2f);                                           // *param_7 = 0
        Assert.Equal(0x04, connection.Flags6C & 0x04);               // 0xA4BD54 bfi = vt3c & 1
        Assert.Equal(0f, voice.OutputMin50[0]);                      // main loop did not run
        Assert.Equal(0f, bus.ParamsB8C4[0]);                         // tail did not run
        Assert.Equal(0x10, bus.Param2_DC);                           // bit4 not cleared
    }

    /// <summary>
    /// M6-022 V7/C1 (0xA4BC58, finding 2): the <c>0xA4C080</c> skip is
    /// <c>vt3c!=0 &amp;&amp; cd8 &amp;&amp; sb</c>; the other cd8 branches run the tail. With cd8 set and
    /// sb clear the main loop and the tail run.
    /// </summary>
    [Fact]
    public void TheBc58TailRunsOnTheCd8SbClearBranch()
    {
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8) { Param2_3C = 5f };
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 8 };
        bus.ParamsA8B4[0] = 7f;
        bus.Param2_DC = 0x10;
        var connection = new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0 };  // sb clear
        voice.Connections.Add(connection);
        voice.VoiceRequest3C = () => 1;

        bool p2f = WwiseVoiceBusPass.UpdateConnectionGains(voice, bus, 1f);

        Assert.True(p2f);                                            // -> 0xA4C010 p2f=1
        Assert.Equal(5f, voice.OutputMin50[0]);                      // main loop ran: min(100, param_2+0x3C=5)
        Assert.Equal(7f, bus.ParamsB8C4[0]);                         // 0xA4BFC4 propagated
        Assert.Equal(0, bus.Param2_DC);                              // 0xA4BFD4 cleared bit4
    }

    /// <summary>
    /// M6-022 V7/C1 (0xA4BC58, finding 2): the <c>0xA4C080</c> skip
    /// (<c>vt3c!=0 &amp;&amp; cd8 &amp;&amp; sb</c>) leaves the tail out and the p2f is 0.
    /// </summary>
    [Fact]
    public void TheBc58TailIsSkippedOnTheCd8SbSetBranch()
    {
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 8 };
        bus.ParamsA8B4[0] = 7f;
        bus.Param2_DC = 0x10;
        var connection = new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0x04 };  // sb set
        voice.Connections.Add(connection);
        voice.VoiceRequest3C = () => 1;

        bool p2f = WwiseVoiceBusPass.UpdateConnectionGains(voice, bus, 1f);

        Assert.False(p2f);
        Assert.Equal(0f, bus.ParamsB8C4[0]);
        Assert.Equal(0x10, bus.Param2_DC);
    }

    /// <summary>
    /// M6-022 V7/C1 (0xA4BC58, finding 2): on the <c>vt3c==0, !cd8</c> branch the tail is gated by fp at
    /// <c>0xA4BD6C</c>: fp==0 skips it, fp!=0 runs the main loop and the tail.
    /// </summary>
    [Fact]
    public void TheBc58TailFollowsFpOnTheVt3cZeroFirstFrame()
    {
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        // fp == 0: the only connection has bit1 set.
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 0 };
        bus.ParamsA8B4[0] = 7f;
        bus.Param2_DC = 0x10;
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0x02 });
        voice.VoiceRequest3C = () => 0;
        WwiseVoiceBusPass.UpdateConnectionGains(voice, bus, 1f);
        Assert.Equal(0f, bus.ParamsB8C4[0]);                         // fp==0 -> skip
        Assert.Equal(0x10, bus.Param2_DC);

        // fp != 0: bit1 clear.
        var voice2 = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 0 };
        voice2.Connections.Add(new WwiseVoiceConnection(bus, 1, 1) { Flags6C = 0 });
        voice2.VoiceRequest3C = () => 0;
        bool p2f = WwiseVoiceBusPass.UpdateConnectionGains(voice2, bus, 1f);
        Assert.True(p2f);                                            // p2f = fp
        Assert.Equal(7f, bus.ParamsB8C4[0]);                         // tail ran
        Assert.Equal(0, bus.Param2_DC);
    }

    // ---------------------------------------------------------------- V7: the settled ducking threshold

    /// <summary>
    /// M6-022 V7/C11 (0xA4B4B0, inventory M6-wwise-bank.md row 3.3/C10): the ducking threshold is the
    /// Init.bnk -80 dB linear value <c>0x38D1B717 = 0.0001f</c>, not an unset input. A connection gain
    /// above it clears <c>[conn+0x6C]</c> bit1; one at or below it sets bit1.
    /// </summary>
    [Fact]
    public void TheDuckingThresholdDefaultsToTheInitBnkValue()
    {
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        var voice = new WwiseLiveVoice(1, 8) { OutputDb = 0f, OutputGain = 1f };
        var loud = new WwiseVoiceConnection(bus, 1, 1) { C60 = 1f };
        var quiet = new WwiseVoiceConnection(bus, 1, 1) { C60 = 0.00005f };
        voice.Connections.Add(loud);
        voice.Connections.Add(quiet);

        WwiseVoiceBusPass.ApplyDucking(voice, bus);

        Assert.Equal(0.0001f, WwiseVoiceBusPass.DuckingThreshold);
        Assert.False((loud.Flags6C & 0x02) != 0);                    // 1.0 > 0.0001
        Assert.True((quiet.Flags6C & 0x02) != 0);                    // 5e-5 <= 0.0001
    }

    // ---------------------------------------------------------------- V17/C7: the silent-bus gates

    /// <summary>
    /// M6-022 V17 (0xA44C18, B1-V17): the device path <c>0x9E9E78</c> runs only when the bus output's
    /// valid frames are non-zero (<c>0xA44CB8/0xA44D2C</c>). A silent bus (Frames == 0) is not routed to
    /// the sink.
    /// </summary>
    [Fact]
    public void TheBusPassDoesNotRouteASilentBusToTheSink()
    {
        var buses = new WwiseMixBusHierarchy();
        var key = new WwiseMixBusKey(0, 0, 7, 0);
        var bus = buses.GetOrCreate(key, () => new WwiseMixBus(key, Array.Empty<WwiseBusFxSlot>(), 8));
        var deviceState = new WwiseOutputDeviceState();
        var sink = new RecordingSink();
        deviceState.AddDevice(new SinkDevice { DeviceKey28 = 7, Sink = sink });
        var pass = new WwiseVoiceBusPass(buses, deviceState);

        Assert.Equal(0, bus.Frames);                                 // no voice mixed in
        pass.BusPass(1);

        Assert.False(sink.Consumed);
    }

    /// <summary>
    /// M6-022 V17/C7 (0xA4F9E0): a source buffer with no valid frames returns at once
    /// (<c>ldrh ip,[r1,#0xe]; cmp ip,#0; bxeq lr</c>), before the mix object and the kernel.
    /// </summary>
    [Fact]
    public void MixOutputBusReturnsWhenTheSourceHasNoValidFrames()
    {
        var buses = new WwiseMixBusHierarchy();
        var outputBus = buses.GetOrCreate(new WwiseMixBusKey(1, 0, 0, 0),
            () => new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8));
        var sourceBus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        var pass = new WwiseVoiceBusPass(buses, new WwiseOutputDeviceState());
        bool mixed = false;
        pass.OutputBusMix = (_, _, _) => mixed = true;

        Assert.Equal(0, sourceBus.Frames);
        pass.MixOutputBus(outputBus, sourceBus.Buffer, sourceBus);
        Assert.False(mixed);

        sourceBus.MixInput();                                        // Frames = MaxFrames
        pass.MixOutputBus(outputBus, sourceBus.Buffer, sourceBus);
        Assert.True(mixed);
    }

    // ---------------------------------------------------------------- V7-e: the count byte

    /// <summary>
    /// M6-022 V7-e (0x9D4228, missing-bodies item 1.5 step 4/6): the gathered count is written to
    /// <c>[voice+0xCC]</c>, not to <c>[voice+0x14]</c> (<c>0xA4BA7C add r3,r4,#0xcc</c>).
    /// </summary>
    [Fact]
    public void TheGatherWritesTheCountToVoiceCcNotTheSendTable()
    {
        var voice = new WwiseLiveVoice(1, 8)
        {
            SendTable = new WwiseVoiceSendTable { Count = 99 },
        };
        voice.SendTable.Entries.Add(new WwiseVoiceSendEntry { Value = 1f });
        voice.SendTable.Entries.Add(new WwiseVoiceSendEntry { Value = 0f });

        int n = WwiseVoiceBusPass.GatherAndDispatch(voice);

        Assert.Equal(1, n);
        Assert.Equal(1, voice.CountCC);                              // [voice+0xCC]
        Assert.Equal(99, voice.SendTable.Count);                     // [voice+0x14] untouched
    }

    // ---------------------------------------------------------------- C8: the [device+0x7C] branch

    /// <summary>
    /// M6-022 C8-seam (0x9E9E78, C17 C8-seam): the <c>[device+0x7C]</c> branch runs
    /// <c>0xA1C9CC(arg, [device+0x80], arg+0x10, arg+0x14)</c> and copies the u16 at <c>arg+0xe</c> to
    /// <c>[device+0x80]+0xe</c>, before the <c>[device+0x70]-&gt;vt+0x24</c> sink. The second argument is
    /// <c>[device+0x80]</c>, not a frame count; the objects are UNKNOWN, so this is the named
    /// <see cref="IWwiseOutputDevice.Route7CSeam"/>.
    /// </summary>
    [Fact]
    public void TheRouteBusRunsTheC8Route7CBranchBeforeTheSink()
    {
        var key = new WwiseMixBusKey(0, 0, 7, 0);
        var bus = new WwiseMixBus(key, Array.Empty<WwiseBusFxSlot>(), 8);
        bus.MixInput();                                              // Frames = MaxFrames
        var deviceState = new WwiseOutputDeviceState();
        var sink = new RecordingSink();
        var device80 = new object();
        var device = new SinkDevice { DeviceKey28 = 7, Sink = sink, OutputObject80 = device80 };
        var order = new List<string>();
        device.Route7CSeam = (arg, arg80, argE, arg10, arg14) =>
        {
            order.Add("route7c");
            Assert.Same(bus.Buffer, arg);
            Assert.Same(device80, arg80);                            // [device+0x80], not a frame count
            Assert.Equal(bus.MaxFrames, argE);                       // arg+0xe
            Assert.Equal(1f, arg10);                                 // [device+0x74]*0x108DAF4
            Assert.Equal(1f, arg14);                                 // [device+0x78]*0x108DAF8
        };
        sink.OnConsume = () => order.Add("sink");
        deviceState.AddDevice(device);

        Assert.True(deviceState.RouteBus(bus));
        Assert.Equal(new[] { "route7c", "sink" }, order);
    }

    private sealed class RecordingSink : IWwiseOutputSink
    {
        public float[] Samples = Array.Empty<float>();
        public int Frames;
        public float Gain74 = 1f;
        public float Gain78 = 1f;
        public bool Consumed;
        public Action? OnConsume;
        public void Consume(ReadOnlySpan<float> samples, int frames, float gain74, float gain78)
        {
            Consumed = true;
            Frames = frames;
            Gain74 = gain74;
            Gain78 = gain78;
            Samples = samples.Slice(0, frames).ToArray();
            OnConsume?.Invoke();
        }
    }

    private sealed class SinkDevice : IWwiseOutputDevice
    {
        public bool IsReady => false;
        public int FramesAvailable => 0;
        public int AdvanceState { get; set; }
        public int Kick() => 2;
        public int DeviceKey28 { get; set; }
        public int DeviceKey2C { get; set; }
        public float MasterGain74 => 1f;
        public float MasterGain78 => 1f;
        public IWwiseOutputSink? Sink { get; set; }
        public object? OutputObject80 { get; set; }
        public Action<float[], object?, int, float, float>? Route7CSeam { get; set; }
        public int ReleaseCalls { get; private set; }
        public void ReleaseFrame() => ReleaseCalls++;
    }

    private sealed class V26Table : IWwiseV26Table
    {
        private readonly (float, float, float, float)[] _rows;
        public V26Table(int count, params (float, float, float, float)[] rows) { Count = count; _rows = rows; }
        public int Count { get; }
        public (float E0, float E1, float E2, float E3) Row(int index) => _rows[index];
        public (float S0, float S1, float S2) Slopes => (0f, 0f, 0f);
    }

    private sealed class CompletionItem : IWwiseV26CompletionItem
    {
        public float ValueBase { get; set; }
        public float ValueSlope { get; set; }
        public float Out0Base { get; private set; }
        public float Out1Base { get; private set; }
        public float Out2Base { get; private set; }
        public float Out0Slope { get; private set; }
        public float Out1Slope { get; private set; }
        public float Out2Slope { get; private set; }
        public long CompletionTick { get; set; } = long.MaxValue;
        public IReadOnlyList<IWwiseV26Target> Targets => Array.Empty<IWwiseV26Target>();
        public IWwiseV26Table Table { get; private set; } = new V26Table(0);
        public IList<byte> ShuffleBag { get; } = new List<byte>();
        public int Index10 { get; set; }
        public int IndexLimit12 { get; set; }
        public int RowIndex14 { get; set; }
        public int Flags18 { get; set; }
        public byte Byte1D { get; set; }
        public float PrevThreshold30 { get; set; }
        public float Threshold34 { get; set; }
        public int SegmentLength38 { get; set; }
        public float Rate3c { get; set; }
        public float TStart40 { get; set; }
        public void SetTable(IWwiseV26Table table) => Table = table;
        public void SetBase(float b0, float b1, float b2) { Out0Base = b0; Out1Base = b1; Out2Base = b2; }
        public void SetSlopes(float s0, float s1, float s2) { Out0Slope = s0; Out1Slope = s1; Out2Slope = s2; }
    }
}

public class WwiseVoiceBusPassNextSourceTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(9, 2)]
    public void NextSourceCode3IsMappedThroughVt120To1Or2AndTheRawThreeIsNeverStored_C27Step4(int vt120, int stored)
    {
        // C27 step 4 further facts (0xA017A4 cmp r0,#3; 0xA017C8..0xA017E4): a 0x9EEDA4 result of 3 is mapped through
        // vt+0x120: 0 gives 1, otherwise 2; only the mapped value is stored.
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8)
        {
            NextSourceEda = _ => (3, 4),
            E0Arg14C = 0x14C,
        };
        int? seen = null;
        bus.E0Vt120 = a => { seen = a; return vt120; };

        int code = WwiseVoiceBusPass.NextSource(bus, out int index);

        Assert.Equal(stored, code);
        Assert.Equal(4, index);
        Assert.Equal(0x14C, seen);
        Assert.Equal(stored, (bus.NextSource1BB >> 3) & 0xF);
        Assert.NotEqual(3, (bus.NextSource1BB >> 3) & 0xF);
    }

    [Fact]
    public void NextSourceSeamsAreRequiredAndAThrowCachesNothing_C27Step4()
    {
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseVoiceBusPass.NextSource(bus, out _));
        Assert.Equal(0, bus.NextSource1BB);

        bus.NextSourceEda = _ => (3, 0);
        Assert.Throws<WwiseMissingBehaviourException>(() => WwiseVoiceBusPass.NextSource(bus, out _));
        Assert.Equal(0, bus.NextSource1BB);

        bus.NextSourceEda = _ => (2, 1);                                     // code 2: vt+0x120 is not reached
        Assert.Equal(2, WwiseVoiceBusPass.NextSource(bus, out int index));
        Assert.Equal(1, index);
    }

    // ------------------------------------------------------------------ the voice pass's required collaborators (M6-022 V5, C30)

    /// <summary>A source that is never rendered (the voice is in state 0 and is stopped by the pass).</summary>
    private sealed class StubSource : IWwiseVoiceSource
    {
        public int Channels => 1;
        public int SampleRate => 48000;
        public int Render(WwiseVoiceBuffer buffer) => 0x2D;
        public int StartStream(uint arg1DC, uint arg1E0) => 1;
        public bool StartStreamSucceeded { get; set; } = true;
    }

    private static WwiseVoiceBusPass PassWithAMarkedVoice(List<string> log, out WwiseLiveVoice voice)
    {
        // A state-0 voice whose owner PBI has bit5 of +0x1BC set and +0x1F8 == -1 is stopped by the pass (M6-026 7.3) and destroyed (7.7):
        // the "destroy" entry marks the point at which the voice walk has run.
        var marked = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, false)
        { Flags1BC = 0x20 };
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState())
        {
            SourceOwner = _ => marked,
            DestroyVoiceA9D40C4 = _ => log.Add("destroy"),
        };
        voice = new WwiseLiveVoice(1, 8) { State = 0, Source = new StubSource() };
        pass.Voices.Add(voice);
        return pass;
    }

    [Fact]
    public void TheVoicePassCallsThe9D3CC0Then0xA43D24Then0xA39564CollaboratorsBeforeAnyVoice()
    {
        // The engine's voice pass 0xA44948 calls 0x9D3CC0 (0xA44978), 0xA43D24 (0xA4497C) and 0xA39564 (0xA44980) in this order, then walks the
        // voices from the head (0xA44984..). Verified by disassembly of 0xA44948..0xA44990.
        var log = new List<string>();
        var pass = PassWithAMarkedVoice(log, out var voice);
        pass.AdvanceTickCounters = () => log.Add("9D3CC0");
        pass.DuckPrePass = () => log.Add("A43D24");
        pass.NodeCleanup = () => log.Add("A39564");

        pass.VoicePass();

        Assert.Equal(new[] { "9D3CC0", "A43D24", "A39564", "destroy" }, log);
        Assert.Equal(2, voice.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AnUnsetCollaboratorThrowsAndNothingAfterItRuns(int unset)
    {
        // The engine runs all three on every pass, so an unset one (an unread body, or the unwired walk) is a visible stop, never a skip: the
        // collaborators before it have run, the ones after it and the voice walk have not.
        var log = new List<string>();
        var pass = PassWithAMarkedVoice(log, out var voice);
        if (unset != 0) pass.AdvanceTickCounters = () => log.Add("9D3CC0");
        if (unset != 1) pass.DuckPrePass = () => log.Add("A43D24");
        if (unset != 2) pass.NodeCleanup = () => log.Add("A39564");

        Assert.Throws<WwiseMissingBehaviourException>(() => pass.VoicePass());

        Assert.Equal(new[] { "9D3CC0", "A43D24", "A39564" }.Take(unset).ToArray(), log);   // only the collaborators before the unset one ran
        Assert.Equal(0, voice.State);                                  // the voice walk did not run
    }

    [Fact]
    public void TheEnginesRenderBodyReachesTheRequiredCollaboratorsThroughTheLiveEntry()
    {
        // Through the entry the live path uses: WwiseVoiceEngine.RunLEngine -> RenderBody -> IWwiseVoiceBusPass.VoicePass (0xA57FF8 -> 0xA44D4C ->
        // 0xA44948). With the pass's collaborators unwired the frame throws; with them wired it runs them once per frame, in order.
        var log = new List<string>();
        var pass = PassWithAMarkedVoice(log, out _);
        var state = new WwiseOutputDeviceState();
        state.Init();
        var engine = new WwiseVoiceEngine(state, pass);
        Assert.Throws<WwiseMissingBehaviourException>(() => engine.RunLEngine());

        pass.AdvanceTickCounters = () => log.Add("9D3CC0");
        pass.DuckPrePass = () => log.Add("A43D24");
        pass.NodeCleanup = () => log.Add("A39564");
        pass.Voices.Clear();
        engine.RunLEngine();
        Assert.Equal(new[] { "9D3CC0", "A43D24", "A39564" }, log);
    }
}

/// <summary>
/// Test doubles for the three collaborators the voice pass calls on every pass (M6-022 V5 / M6-025 C24.1 / M6-026 E1: <c>0x9D3CC0</c> at
/// <c>0xA44978</c>, <c>0xA43D24</c> at <c>0xA4497C</c>, <c>0xA39564</c> at <c>0xA44980</c>). The pass requires them (it throws when one is unset);
/// a test that is about something else supplies no-ops and says so by calling this. Whatever is already set is kept.
/// </summary>
internal static class WwisePrePassTestDoubles
{
    public static WwiseVoiceBusPass WithPrePassDoubles(this WwiseVoiceBusPass pass)
    {
        pass.AdvanceTickCounters ??= () => { };
        pass.DuckPrePass ??= () => { };
        pass.NodeCleanup ??= () => { };
        return pass;
    }
}
