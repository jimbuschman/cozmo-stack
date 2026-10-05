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
        var deviceState = new WwiseOutputDeviceState();
        var pass = new WwiseVoiceBusPass(buses, deviceState).WithPrePassDoubles();
        pass.PostMixA5495C = _ => { }; pass.PostMixNoDataReadyA55CC4 = (_, _) => { };                  // M6-026 7.2: 0xA55CC4 / 0xA5495C after a mix are unread (test double)
        var owner = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, false);
        pass.SourceOwner = _ => owner;                       // M6-026 7.3: [[voice+0xD4]+0xC], an unmarked PBI (test double)

        var voice = new WwiseLiveVoice(channels: 1, maxFrames: 8)
        {
            Source = new ConstantSource(0.5f, 8),
            StartResampler5321C = () => true,   // the host's own resampler start: this source has no pitch node (0xA5321C is the node's)
            OutputGain = 1f,
            AllowRenderOrderApproximation = true,   // the test source has no pitch node: the earlier render approximation, NOT the engine's order (MISSING)
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

        pass.VoicePass(1);

        Assert.Equal(1, pass.VoicesRendered);
        Assert.Equal(1, bus.State);                                   // MixInput set state 4 -> 1
        for (int i = 0; i < 8; i++) Close(0.5f, bus.Buffer[i]);       // 1.0 mono->mono gain
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
        var deviceState = new WwiseOutputDeviceState();
        var sink = new RecordingSink();
        var device = new SinkDevice { DeviceKey28 = 7, Sink = sink };
        deviceState.AddDevice(device);
        var pass = new WwiseVoiceBusPass(buses, deviceState).WithPrePassDoubles();
        pass.PostMixA5495C = _ => { }; pass.PostMixNoDataReadyA55CC4 = (_, _) => { };                  // M6-026 7.2: 0xA55CC4 / 0xA5495C after a mix are unread (test double)
        var owner = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, false);
        pass.SourceOwner = _ => owner;                       // M6-026 7.3: [[voice+0xD4]+0xC], an unmarked PBI (test double)

        var voice = new WwiseLiveVoice(1, 8) { StartResampler5321C = () => true, Source = new ConstantSource(0.5f, 8), OutputGain = 1f, AllowRenderOrderApproximation = true };   // the earlier render approximation (MISSING), not the engine's order
        var dry = new WwiseVoiceConnection(bus, 1, 1) { TargetGain = 1f };
        dry.Descriptor.Reserve(1, 1);                                // [conn+0x18] != 0 (C24.4)
        dry.Refresh();                                               // past the first update: bit2 is the live fade-in bit
        voice.Connections.Add(dry);
        pass.Voices.Add(voice);

        pass.VoicePass(1);                                            // V8/V14: source -> bus buffer
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

    // ---------------------------------------------------------------- V7: 0xA4BC58 on the owner PBI (C41.1: param_2 = pbi+0xC; the earlier tests read a mix bus as the owner)

    private static WwisePlayingInstance OwnerPbi()
        => new(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, continuous: false);

    private static WwiseVoiceConnection LineConnection(byte flags6C)
        => new(new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8), 1, 1) { Flags6C = flags6C };

    /// <summary>
    /// M6-022 V7-o (C18 V7-o/X3, <c>0xA4BE80..0xA4BF34</c>; C41.1: <c>param_2 = [source+0xC]+0xC = pbi+0xC</c>): the four float minima are zeroed at entry, set to 100.0 when <c>id != 0</c>, then reduced to the running minima of
    /// <c>[conn+0x50/+0x54/+0x58/+0x5C]</c>. The per-connection copy reads <c>[param_2+0x3C]/[param_2+0x40]</c> = <c>[pbi+0x48]/[pbi+0x4C]</c>, not the voice: <c>[conn+0x50]=[pbi+0x48]</c>, <c>[conn+0x58]=[pbi+0x4C]</c>, <c>[conn+0x54]=[conn+0x5C]=0</c>.
    /// </summary>
    [Fact]
    public void TheBc58ConnectionCopyReadsTheOwnerPbiNotTheVoice()
    {
        var pbi = OwnerPbi();
        pbi.Lpf48 = 5f; pbi.Hpf4C = 7f;
        pbi.Flags1BE = 0x04;                                         // voice->vt+0x3C = 0xA55E90 = ([pbi+0x1BE] & 0x14) != 0 (gapE 3.3): vt3c != 0 -> main loop + tail
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 8 };
        var connection = LineConnection(0);                          // sb clear
        voice.Connections.Add(connection);

        bool p2f = WwiseVoiceBusPass.UpdateConnectionGains(voice, pbi, 1f, 0);

        Assert.True(p2f);
        Assert.Equal(5f, connection.C50);                            // [pbi+0x48]
        Assert.Equal(7f, connection.C58);                            // [pbi+0x4C]
        Assert.Equal(0f, connection.C54);
        Assert.Equal(0f, connection.C5C);
        Assert.Equal(5f, voice.OutputMin50[0]);                      // min(100, 5)
        Assert.Equal(0f, voice.OutputMin50[1]);
        Assert.Equal(7f, voice.OutputMin50[2]);                      // min(100, 7)
        Assert.Equal(0f, voice.OutputMin50[3]);
    }

    /// <summary>
    /// M6-022 V7/C1 (0xA4BC58, C12 voice-callees Q4, finding 3): on the <c>!cd8</c> branch with <c>vt3c!=0</c> the native overwrites fp with <c>cd8 &amp; 8</c> (=0) at <c>0xA4C05C</c>, so <c>0xA4BD6C</c> skips both the main loop and the tail. Only <c>SetBit2 = vt3c &amp; 1</c> runs.
    /// The propagated words are <c>[pbi+0xB4..0xC0]</c> to <c>[pbi+0xC4..0xD0]</c> and the cleared bit is bit 4 of <c>[pbi+0xE8]</c> (<c>[param_2+0xDC]</c>).
    /// </summary>
    [Fact]
    public void TheBc58FirstFrameDefersTheGainsAndSkipsTheTail()
    {
        var pbi = OwnerPbi();
        pbi.Lpf48 = 5f; pbi.PanB4 = 7f; pbi.FieldC4 = 0f; pbi.Flags0E8 = 0x10; pbi.Flags1BE = 0x04;
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 0 };
        var connection = LineConnection(0);
        voice.Connections.Add(connection);

        bool p2f = WwiseVoiceBusPass.UpdateConnectionGains(voice, pbi, 1f, 0);

        Assert.False(p2f);                                           // *param_7 = 0
        Assert.Equal(0x04, connection.Flags6C & 0x04);               // 0xA4BD54 bfi = vt3c & 1
        Assert.Equal(0f, voice.OutputMin50[0]);                      // main loop did not run
        Assert.Equal(0f, pbi.FieldC4);                               // tail did not run
        Assert.Equal(0x10, pbi.Flags0E8);                            // bit4 not cleared
    }

    /// <summary>
    /// M6-022 V7/C1 (0xA4BC58, finding 2): the <c>0xA4C080</c> skip is <c>vt3c!=0 &amp;&amp; cd8 &amp;&amp; sb</c>; the other cd8 branches run the tail. With cd8 set and sb clear the main loop and the tail run.
    /// </summary>
    [Fact]
    public void TheBc58TailRunsOnTheCd8SbClearBranch()
    {
        var pbi = OwnerPbi();
        pbi.Lpf48 = 5f; pbi.PanB4 = 7f; pbi.PanB8 = 8f; pbi.PanBC = 9f; pbi.PanC0 = 3; pbi.Flags0E8 = 0x10; pbi.Flags1BE = 0x04;
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 8 };
        var connection = LineConnection(0);                          // sb clear
        voice.Connections.Add(connection);

        bool p2f = WwiseVoiceBusPass.UpdateConnectionGains(voice, pbi, 1f, 0);

        Assert.True(p2f);                                            // -> 0xA4C010 p2f=1
        Assert.Equal(5f, voice.OutputMin50[0]);                      // main loop ran: min(100, param_2+0x3C = [pbi+0x48] = 5)
        Assert.Equal(7f, pbi.FieldC4);                               // 0xA4BFC4 propagated [pbi+0xB4..0xC0] to [pbi+0xC4..0xD0]
        Assert.Equal(8f, pbi.FieldC8);
        Assert.Equal(9f, pbi.FieldCC);
        Assert.Equal(3, pbi.FieldD0);
        Assert.Equal(0, pbi.Flags0E8);                               // 0xA4BFD4 cleared bit4 of [pbi+0xE8]
    }

    /// <summary>
    /// M6-022 V7/C1 (0xA4BC58, finding 2): the <c>0xA4C080</c> skip (<c>vt3c!=0 &amp;&amp; cd8 &amp;&amp; sb</c>) leaves the tail out and the p2f is 0.
    /// </summary>
    [Fact]
    public void TheBc58TailIsSkippedOnTheCd8SbSetBranch()
    {
        var pbi = OwnerPbi();
        pbi.PanB4 = 7f; pbi.FieldC4 = 0f; pbi.Flags0E8 = 0x10; pbi.Flags1BE = 0x04;
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 8 };
        voice.Connections.Add(LineConnection(0x04));                 // sb set

        bool p2f = WwiseVoiceBusPass.UpdateConnectionGains(voice, pbi, 1f, 0);

        Assert.False(p2f);
        Assert.Equal(0f, pbi.FieldC4);
        Assert.Equal(0x10, pbi.Flags0E8);
    }

    /// <summary>
    /// M6-022 V7/C1 (0xA4BC58, finding 2; verification D1): on the <c>vt3c==0, !cd8</c> branch the tail is gated by fp at <c>0xA4BD6C</c>: fp==0 skips it (bit 2 = 1), fp!=0 sets bit 2 to <c>arg5</c> (<c>src-&gt;vt+0x4C</c>, bit 6 of <c>byte [pbi+0x1BE]</c>), runs the main loop and the tail.
    /// </summary>
    [Fact]
    public void TheBc58TailFollowsFpOnTheVt3cZeroFirstFrame()
    {
        var pbi = OwnerPbi();
        pbi.PanB4 = 7f; pbi.FieldC4 = 0f; pbi.Flags0E8 = 0x10;       // [pbi+0x1BE] & 0x14 == 0: vt3c == 0
        // fp == 0: the only connection has bit1 set.
        var voice = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 0 };
        var held = LineConnection(0x02);
        voice.Connections.Add(held);
        WwiseVoiceBusPass.UpdateConnectionGains(voice, pbi, 1f, 0);
        Assert.Equal(0f, pbi.FieldC4);                               // fp==0 -> skip
        Assert.Equal(0x10, pbi.Flags0E8);
        Assert.Equal(0x04, held.Flags6C & 0x04);                     // 0xA4BD40: bit 2 = 1

        // fp != 0: bit1 clear; bit 2 takes arg5.
        var voice2 = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 0 };
        var open = LineConnection(0);
        voice2.Connections.Add(open);
        bool p2f = WwiseVoiceBusPass.UpdateConnectionGains(voice2, pbi, 1f, 0);
        Assert.True(p2f);                                            // p2f = fp
        Assert.Equal(7f, pbi.FieldC4);                               // tail ran
        Assert.Equal(0, pbi.Flags0E8);
        Assert.Equal(0, open.Flags6C & 0x04);                        // 0xA4C024: bit 2 = arg5 = 0
        var voice3 = new WwiseLiveVoice(1, 8) { Word0xF0 = 1, FlagsCD = 0 };
        var open3 = LineConnection(0);
        voice3.Connections.Add(open3);
        WwiseVoiceBusPass.UpdateConnectionGains(voice3, pbi, 1f, 1);
        Assert.Equal(0x04, open3.Flags6C & 0x04);                    // arg5 = 1
    }

    // ---------------------------------------------------------------- V7: the settled ducking threshold

    /// <summary>
    /// M6-022 V7/C11 (0xA4B4B0): the ducking threshold is the shared global [0x1052454], which at every Play is the state after the Init.bnk STMG setter 0x9A080C(-80, 2): <c>0x38D2306A</c> (engine value, emu_aux.py kind S).
    /// A connection gain above it clears <c>[conn+0x6C]</c> bit1; one at or below it sets bit1.
    /// </summary>
    [Fact]
    public void TheDuckingThresholdDefaultsToTheStateAfterTheInitBnkStmgSetter()
    {
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        var voice = new WwiseLiveVoice(1, 8) { OutputDb = 0f, OutputGain = 1f };
        var loud = new WwiseVoiceConnection(bus, 1, 1) { C60 = 1f };
        var between = new WwiseVoiceConnection(bus, 1, 1) { C60 = 0.00005f };     // above 2^-16 (the image value) but below 0x38D2306A
        var quiet = new WwiseVoiceConnection(bus, 1, 1) { C60 = 0.000001f };
        voice.Connections.Add(loud);
        voice.Connections.Add(between);
        voice.Connections.Add(quiet);

        WwiseVoiceBusPass.ApplyDucking(voice, bus);

        Assert.Equal(0x38D2306Au, BitConverter.SingleToUInt32Bits(WwiseVoiceBusPass.DuckingThreshold));
        Assert.False((loud.Flags6C & 0x02) != 0);
        Assert.True((between.Flags6C & 0x02) != 0);
        Assert.True((quiet.Flags6C & 0x02) != 0);
    }

    /// <summary>The pre-load image state (<c>new WwiseSendGlobals()</c>): 0x37800000 / 0xC2C0999A, type gate 3; the engine never plays in it.</summary>
    [Fact]
    public void ThePreLoadImageStateIsAvailableExplicitly()
    {
        var image = new WwiseSendGlobals();
        Assert.Equal(0x37800000u, BitConverter.SingleToUInt32Bits(image.GameLinear));
        Assert.Equal(0xC2C0999Au, BitConverter.SingleToUInt32Bits(image.UserDb));
        Assert.Equal(3, image.TypeGate);
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
    /// M6-022 V7-e (0x9D4228, C40.4 T-A4/T-A6): the merged count is written to <c>[voice+0xCC]</c>, not to <c>[voice+0x14]</c> (<c>0xA4BA7C add r3,r4,#0xcc</c>); the table's count (here 99, so entry 0 is not re-initialised) stays. The earlier test of this name asserted the
    /// <c>GatherAndDispatch</c> model (a gather over the 0x4C-byte table, which is not the array <c>0x9D4228</c> merges into: it merges into <c>voice+0x2C</c>); the engine's values are in <c>WwiseAuxRouteTests</c>.
    /// </summary>
    [Fact]
    public void TheMergeWritesTheCountToVoiceCcNotTheSendTable()
    {
        var go = new WwiseGameObjectRef { Mask22 = 1 };
        go.Aux24[0] = (0xAA, 1f);
        var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 1, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, continuous: false)
        {
            Flags0E8 = 0x5C, GameObjectRef14 = go, Byte94 = 1, Volume3C = 0f, Word64 = 0f,
        };
        var voice = new WwiseLiveVoice(1, 8) { BusOwner8 = pbi, SendTable = new WwiseVoiceSendTable { Count = 99, Capacity = 1 } };
        voice.SendTable.Entries.Add(new WwiseVoiceSendEntry());
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState()) { AuxDispatch9D4108 = (_, _, _) => { } };

        pass.RefreshVoiceGainA4B93C(voice);

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

        pass.VoicePass(1);

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

        Assert.Throws<WwiseMissingBehaviourException>(() => pass.VoicePass(1));

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
        // V7 (0xA54F1C) now runs for every state-1 voice: its build 0xA54A30 and the context call of its tail are test doubles here (the V7 values are WwiseVoiceStateOracleTests').
        pass.StartStreamOverrideA54A30 ??= _ => 1;
        pass.CalcEffectiveParamsVt24 ??= _ => { };
        return pass;
    }
}
