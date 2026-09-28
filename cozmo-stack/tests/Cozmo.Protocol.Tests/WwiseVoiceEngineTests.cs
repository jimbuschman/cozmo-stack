using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-022 (correction C12): the settled output-device state/gates, the render-body throttle and bus-pass
/// argument, and the JNI audio-route poll's decision structure. The expected values are the C12 rows and the
/// verifier's corrected instruction citations (G5 countdown/return, G8 <c>[node+0x88]</c>, D1/D2 gate1 and
/// the no-device return, P6's non-null route); the implementation was corrected to match, so the
/// expectations are not read back from the code.
/// </summary>
public sealed class WwiseVoiceEngineTests
{
    private sealed class FakeDevice : IWwiseOutputDevice
    {
        public bool IsReady { get; set; }
        public int Frames { get; set; }
        public int KickCount { get; private set; }
        public bool IsReadyValue => IsReady;
        public int FramesAvailable => Frames;
        public int AdvanceState { get; set; }
        public int Kick() { KickCount++; return IsReady ? 1 : 2; }
    }

    private sealed class RecordingPass : IWwiseVoiceBusPass
    {
        public int VoicePasses;
        public int BusPasses;
        public int Flushes;
        public int LastBusArg = int.MinValue;
        public void VoicePass() => VoicePasses++;
        public void BusPass(int arg) { BusPasses++; LastBusArg = arg; }
        public void FlushPbiNotifications() => Flushes++;
    }

    private sealed class RecordingGroup : IWwisePerformGroupMember
    {
        public readonly List<long> Ticks = new();
        public void Tick(long tick) => Ticks.Add(tick);
    }

    private sealed class FakeRoute : IWwiseJniAudioRoute
    {
        public bool HasJavaVm { get; set; } = true;
        public bool HasContext { get; set; } = true;
        public bool BluetoothActive { get; set; }
    }

    private static WwiseVoiceEngine BuildEngine(
        out WwiseOutputDeviceState state, out RecordingPass pass,
        out WwiseJniAudioRoutePoll? poll, Action? onRouteChanged = null, params IWwisePerformGroupMember[] group)
    {
        state = new WwiseOutputDeviceState();
        pass = new RecordingPass();
        poll = null;
        if (onRouteChanged is not null)
            poll = new WwiseJniAudioRoutePoll(new FakeRoute { BluetoothActive = false }, onRouteChanged);
        return new WwiseVoiceEngine(state, pass, group, poll);
    }

    [Fact] // M6-022 G6 (0x9EADE8): gate2 = 0, countdown = 0, gate3 = 1
    public void TheOutputDeviceInitSetsGate2ZeroAndGate3One()
    {
        var state = new WwiseOutputDeviceState();
        state.Init();
        Assert.Equal(0, state.Gate2);
        Assert.Equal(1, state.Gate3);
        Assert.Equal(0, state.Countdown);
        Assert.Equal(0, state.DeviceCount);
        Assert.Equal(1, state.BusPassArg);          // V4/G10: (gate2==0) ? 1 : gate3
    }

    [Fact] // M6-022 V4/G10: the bus-pass arg is (gate2==0) ? 1 : gate3
    public void TheBusPassArgumentIsOneWhenGate2IsZeroAndGate3Otherwise()
    {
        var state = new WwiseOutputDeviceState();
        state.Init();
        Assert.Equal(1, state.BusPassArg);

        // G5: SetOutputDevice sets gate2 = param_1, gate3 = param_2, then tail-calls 0x9EBA54(param_2)
        // (param_1 != 0); the empty-list path returns 1 (0x9EBB8C/0x9EBBC0).
        int rc = state.SetOutputDevice(2, 3, 0);
        Assert.Equal(1, rc);
        Assert.Equal(2, state.Gate2);
        Assert.Equal(3, state.Gate3);
        Assert.Equal(3, state.BusPassArg);
    }

    [Fact] // M6-022 G5: returns 3 when the (gate2, gate3) pair is unchanged
    public void SetOutputDeviceReturnsThreeWhenThePairIsUnchanged()
    {
        var state = new WwiseOutputDeviceState();
        state.Init();
        state.SetOutputDevice(2, 3, 0);
        Assert.Equal(3, state.SetOutputDevice(2, 3, 0));
    }

    [Fact] // M6-022 G5 (0x9EC44C..0x9EC53C): 21<max path returns 1 with gates unchanged when gate2==0
    public void SetOutputDeviceWithACountdownOver21KeepsTheGatesWhenGate2IsZero()
    {
        var state = new WwiseOutputDeviceState();
        state.Init();                               // gate2 = 0, gate3 = 1

        int rc = state.SetOutputDevice(2, 3, 25);   // max(25,0)=25 -> countdown 4; gate2==0 -> return 1

        Assert.Equal(1, rc);
        Assert.Equal(4, state.Countdown);
        Assert.Equal(0, state.Gate2);               // the 21<max path does not write param_1/param_2
        Assert.Equal(1, state.Gate3);
    }

    [Fact] // M6-022 G5: the 21<max path with gate2!=0 && gate3==0 sets both gates to 1 and advances
    public void SetOutputDeviceWithACountdownOver21AndGate3ZeroSetsBothGates()
    {
        var state = new WwiseOutputDeviceState();
        state.Init();
        state.SetOutputDevice(5, 0, 0);             // gate2 = 5, gate3 = 0 (else path, param1 != 0)
        Assert.Equal(5, state.Gate2);
        Assert.Equal(0, state.Gate3);

        int rc = state.SetOutputDevice(9, 9, 25);   // max(25,0)=25 -> countdown 4; gate2!=0 && gate3==0

        Assert.Equal(1, rc);
        Assert.Equal(4, state.Countdown);
        Assert.Equal(1, state.Gate2);               // 0x9EC574
        Assert.Equal(5, state.Gate3);               // 0x9EC56C sets 1, then AdvanceFrame(old gate2=5) sets 5
        Assert.Equal(1, state.Gate1);               // no devices -> all idle
    }

    [Fact] // M6-022 G5: the inline walk sets [node+0x88] = 2 only for states not in {1,2}
    public void SetOutputDeviceInlineWalkSetsBusyOnlyForStatesOutsideOneAndTwo()
    {
        var state = new WwiseOutputDeviceState();
        state.Init();
        var d0 = new FakeDevice { Frames = 1, AdvanceState = 0 };
        var d1 = new FakeDevice { Frames = 1, AdvanceState = 1 };
        var d2 = new FakeDevice { Frames = 1, AdvanceState = 2 };
        var d3 = new FakeDevice { Frames = 1, AdvanceState = 3 };
        state.AddDevice(d0);
        state.AddDevice(d1);
        state.AddDevice(d2);
        state.AddDevice(d3);

        int posts = 0;
        int rc = state.SetOutputDevice(0, 7, 0,
            deviceStarted: _ => true, semPost: () => posts++);

        Assert.Equal(1, rc);
        Assert.Equal(2, d0.AdvanceState);           // 0 -> 2
        Assert.Equal(1, d1.AdvanceState);           // in {1,2}: unchanged
        Assert.Equal(2, d2.AdvanceState);           // in {1,2}: unchanged
        Assert.Equal(2, d3.AdvanceState);           // 3 -> 2
        Assert.Equal(2, posts);                     // only the two nodes set to 2
        Assert.Equal(0, state.Gate2);               // (param1|gate2)==0 -> gate2 = 0
        Assert.Equal(1, state.Gate3);               // ... and gate3 = 1
    }

    [Fact] // M6-022 G5: the (param1|gate2)==0 branch writes gate2=0, gate3=1
    public void SetOutputDeviceWithBothParam1AndGate2ZeroResetsTheGates()
    {
        var state = new WwiseOutputDeviceState();
        state.Init();

        int rc = state.SetOutputDevice(0, 7, 0);

        Assert.Equal(1, rc);
        Assert.Equal(0, state.Gate2);
        Assert.Equal(1, state.Gate3);
    }

    [Fact] // M6-022 G8: gate1 = all devices idle ? 1 : 0; the [node+0x88] byte is reset
    public void AdvanceFrameWritesGate1FromTheListNodeIdleState()
    {
        var state = new WwiseOutputDeviceState();
        Assert.Equal(1, state.AdvanceFrame(1));     // empty list -> 1
        Assert.Equal(1, state.Gate1);

        var busy = new FakeDevice { AdvanceState = 2 };
        state.AddDevice(busy);
        Assert.Equal(1, state.AdvanceFrame(1));     // non-zero +0x88 -> gate1 = 0
        Assert.Equal(0, state.Gate1);
        Assert.Equal(0, busy.AdvanceState);         // the walk resets it

        Assert.Equal(1, state.AdvanceFrame(1));     // now idle -> 1
        Assert.Equal(1, state.Gate1);
    }

    [Fact] // M6-022 N3: store when last==0, skip when 0 < tick-last <= 8, else store
    public void TheRenderBodyThrottleSkipsWithinEightTicks()
    {
        var engine = BuildEngine(out var state, out var pass, out _);

        state.ManagerTick = 100;
        engine.RenderBody();                        // last == 0 -> store 100, runs
        Assert.Equal(1, pass.VoicePasses);

        state.ManagerTick = 108;                    // delta 8 -> skip
        engine.RenderBody();
        Assert.Equal(1, pass.VoicePasses);
        Assert.Equal(1, engine.ThrottledFrames);

        state.ManagerTick = 109;                    // delta 9 -> store, runs
        engine.RenderBody();
        Assert.Equal(2, pass.VoicePasses);

        state.ManagerTick = 117;                    // delta 8 -> skip
        engine.RenderBody();
        Assert.Equal(2, pass.VoicePasses);
        Assert.Equal(2, engine.ThrottledFrames);
    }

    [Fact] // M6-022 V3/N1 (voice-callees Q1): pre-loop calls vt+0x2c, then vt+0x30 when non-zero
    public void ThePreLoopKicksOnlyReadyDevices()
    {
        var state = new WwiseOutputDeviceState();
        var ready = new FakeDevice { IsReady = true, Frames = 7 };
        var notReady = new FakeDevice { IsReady = false, Frames = 7 };
        state.AddDevice(ready);
        state.AddDevice(notReady);

        state.PreLoop();

        Assert.Equal(1, ready.KickCount);
        Assert.Equal(0, notReady.KickCount);
    }

    [Fact] // M6-022 D1/D2: no devices -> gate1 = 1 but return 0; else the min, gate1 = 0 (device path)
    public void TheDeviceAdvanceTakesTheMinimumFrameCountAndWritesGate1()
    {
        var state = new WwiseOutputDeviceState();
        Assert.Equal(0, state.AdvanceDevices(0));   // 0x9EC1BC mov r0,#0
        Assert.Equal(1, state.Gate1);               // 0x9EC1A4..0x9EC1B4

        state.AddDevice(new FakeDevice { Frames = 9 });
        state.AddDevice(new FakeDevice { Frames = 4 });
        Assert.Equal(4, state.AdvanceDevices(0));
        Assert.Equal(0, state.Gate1);               // 0x9EC15C..0x9EC170 mov sl,#0; 0x9EC00C strb

        state.AddDevice(new FakeDevice { Frames = 0 });
        Assert.Equal(0, state.AdvanceDevices(0));
        Assert.Equal(0, state.Gate1);
    }

    [Fact] // M6-022 P1..P7: gate on JVM/context and once per 64 ticks; notify on a change
    public void TheJniRoutePollRunsOncePer64TicksAndNotifiesOnChange()
    {
        var route = new FakeRoute { BluetoothActive = true };
        int changes = 0;
        var poll = new WwiseJniAudioRoutePoll(route, () => changes++);

        poll.Poll(63);                              // not a 64th tick
        Assert.Equal(0, poll.Polls);

        poll.Poll(64);                              // runs, false -> true is a change
        Assert.Equal(1, poll.Polls);
        Assert.True(poll.BluetoothActive);
        Assert.Equal(1, changes);

        poll.Poll(128);                             // runs, unchanged -> no notify
        Assert.Equal(2, poll.Polls);
        Assert.Equal(1, changes);

        route.HasJavaVm = false;
        poll.Poll(192);
        Assert.Equal(2, poll.Polls);                // gate blocks
    }

    [Fact] // M6-022 V29: RenderBuses -> RunLEngine (voice then bus with V4 arg) -> FlushPbi, tick++
    public void PerformRunsTheGroupThenTheVoiceAndBusPassesThenTheFlush()
    {
        var g0 = new RecordingGroup();
        var g1 = new RecordingGroup();
        var g2 = new RecordingGroup();
        var g3 = new RecordingGroup();
        var engine = BuildEngine(out var state, out var pass, out _, null, g0, g1, g2, g3);
        state.Init();

        engine.Perform();

        Assert.Equal(new long[] { 1 }, g0.Ticks);   // V29: tick+1
        Assert.Equal(new long[] { 1 }, g1.Ticks);
        Assert.Equal(new long[] { 1 }, g2.Ticks);
        Assert.Equal(new long[] { 1 }, g3.Ticks);
        Assert.Equal(1, pass.VoicePasses);
        Assert.Equal(1, pass.BusPasses);
        Assert.Equal(1, pass.LastBusArg);           // V4: gate2 == 0 -> 1
        Assert.Equal(1, pass.Flushes);
        Assert.Equal(1, engine.PerformTick);
    }
}