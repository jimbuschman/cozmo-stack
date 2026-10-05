// fidelity: M6-012, M6-022, M6-010
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-012 / M6-022 / M6-010 (C44.3): the connection gains and matrices of the concrete Sound through the live entry <c>RunVoiceStateMachine</c> (<c>0xA54F1C</c>, whose two <c>0xA4BC58</c> calls run the real chain) and the mix walk <c>MixConnections</c>:
/// a mono Vorbis source (<c>[voice+0xF0] = 0x4101</c>) with a dry connection to Cozmo_Robot (muted by <c>SetGameObjectOutputBusVolume 0.0</c>: <c>[GO+0x60] = 0</c> makes the table entry's gain 0 and the connection's bit 1 set, pass-16 D1/D2) and an aux connection to a
/// mono Robot_Bus (<c>[conn+0x68] != 0</c>). The expected values are the inventory rows' (F2: <c>[conn+0x14]</c> is 1.0f for aux and the entry gain for dry; F8: mono to mono <c>[1, 0, 0, 0]</c>; B6/B12/B13: the bit-1, flat and ramp cases; G3: 0.99903899 at 0 dB), not the code's.
/// </summary>
public class WwiseConnLiveTests
{
    private const uint Lin0Db = 0x3F7FC105u;            // G3 / M6-010: dBToLin(0 dB) of the fast pow = 0.9990389943

    private sealed class MonoSource : IWwiseVoiceSource
    {
        public int Channels => 1;
        public int SampleRate => 48000;
        public int Render(WwiseVoiceBuffer buffer) => 0x2D;
        public int StartStream(uint a, uint b) => 1;
        public bool StartStreamSucceeded { get; set; } = true;
    }

    private static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);

    private static uint[] Words(ReadOnlySpan<float> half) => half.ToArray().Select(Bits).ToArray();

    private static readonly uint[] MonoToMono = { 0x3F800000, 0, 0, 0 };

    private sealed class Rig
    {
        public WwisePlayingInstance Pbi = null!;
        public WwiseLiveVoice Voice = null!;
        public WwiseVoiceBusPass Pass = null!;
        public WwiseVoiceConnection Dry = null!, Aux = null!;
        public WwiseMixBus CozmoRobot = null!, RobotBus = null!;
    }

    private static Rig Build()
    {
        var r = new Rig();
        r.Pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 7, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, false);
        r.Pbi.Flags0E8 = 0x5C;                                       // the 2D path (pass-16 F6: the shipped chain's top positioning node is the root, [P+0xDC] & 3 == 0 after 0x9BEB30)
        r.Pbi.StartOffset = 0xFFFFFFFF;                              // no budget
        r.Voice = new WwiseLiveVoice(1, 8) { Source = new MonoSource(), BusOwner8 = r.Pbi, StartResampler5321C = () => true, OutputGain = 0.5f, Word0xF0 = 0x4101 };
        r.Voice.SendTable = new WwiseVoiceSendTable { Capacity = 1 };
        r.Voice.SendTable.Entries.Add(new WwiseVoiceSendEntry { SendGain = 0f });   // D1: [table+0x34] = lin(...) * [GO+0x60] = 0 (SetGameObjectOutputBusVolume 0.0)
        r.CozmoRobot = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8) { Format64 = 0x4101 };
        r.RobotBus = new WwiseMixBus(new WwiseMixBusKey(), Array.Empty<WwiseBusFxSlot>(), 8) { Format64 = 0x4101 };
        // The connection constructor 0xA6F90C: conn+8..+0x14 = 1.0f, bit 0 of conn+0x6C set. D2: the muted dry connection has bit 1 set (conn+0x60 <= the threshold).
        r.Dry = new WwiseVoiceConnection(r.CozmoRobot, 1, 1) { Arg68 = 0, Flags6C = 0x03, C08 = 1f, C0C = 1f, C10 = 1f, C14 = 1f };
        r.Aux = new WwiseVoiceConnection(r.RobotBus, 1, 1) { Arg68 = 1, HasAux = true, Flags6C = 0x01, C08 = 1f, C0C = 1f, C10 = 1f, C14 = 1f };
        r.Voice.Connections.Add(r.Dry);
        r.Voice.Connections.Add(r.Aux);
        r.Pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState())
        {
            SourceOwner = _ => r.Pbi,
            CalcEffectiveParamsVt24 = _ => { },
            StartStreamOverrideA54A30 = _ => 1,                      // the FX build is not under test (WwiseVoiceStateTests)
        };
        return r;
    }

    [Fact]
    public void M6_012_F2_F8_B6_B12_B13_The_concrete_Sound_connections_after_frame_1_and_frame_2_through_V7()
    {
        var r = Build();
        float g = BitConverter.UInt32BitsToSingle(Lin0Db);          // V7's gain: dBToLin(0 dB) (pbi+0x54 + make-up = 0); the voice gain [voice+0x1C] is 0.5f
        float c0c1 = 0.5f * g;

        // ---- frame 1 (V7 calls 0xA4BC58 twice: [voice+0xCD] bit 3 is clear for both; B13: the first update is flat)
        Assert.True(r.Pass.RunVoiceStateMachine(r.Voice));

        // dry (bit 1 set, D2): B6 - no 0xA5975C, [conn+0xC] = 0, the matrices keep the zero fill; [conn+0x14] is the zero the re-init stored (B2), the table entry's gain 0.0 is not read.
        Assert.Equal(0u, Bits(r.Dry.C0C));
        Assert.Equal(0u, Bits(r.Dry.C14));
        Assert.Equal(0u, Bits(r.Dry.C08));
        Assert.Equal(0u, Bits(r.Dry.C10));
        Assert.Equal(new uint[] { 0, 0, 0, 0 }, Words(r.Dry.Descriptor.NextMatrix));
        Assert.Equal(new uint[] { 0, 0, 0, 0 }, Words(r.Dry.Descriptor.PrevMatrix));
        Assert.Equal(1, r.Dry.C64);

        // aux: [conn+0xC] = [voice+0x1C] * gain; F2: [conn+0x14] = 1.0f for aux; F8 mono to mono; flat: [conn+8] = [conn+0xC], [conn+0x10] = [conn+0x14], prev = next
        Assert.Equal(Bits(c0c1), Bits(r.Aux.C0C));
        Assert.Equal(0x3F800000u, Bits(r.Aux.C14));
        Assert.Equal(Bits(c0c1), Bits(r.Aux.C08));
        Assert.Equal(0x3F800000u, Bits(r.Aux.C10));
        Assert.Equal(MonoToMono, Words(r.Aux.Descriptor.NextMatrix));
        Assert.Equal(MonoToMono, Words(r.Aux.Descriptor.PrevMatrix));
        Assert.Equal(32, r.Aux.Descriptor.Size);                     // ((1 + 3) >> 2) * (1 << 5)
        Assert.Equal(1, r.Aux.C64);
        Assert.Equal(0, r.Voice.FlagsCD & 4);                        // B14
        Assert.Equal(8, r.Voice.FlagsCD & 8);                        // 0xA55294: bit 3 set after the first update
        Assert.True(r.Voice.P2F);

        // ---- frame 2 (bit 3 set: B12 the ramping case; the voice gain changes, the pan inputs are unchanged)
        r.Voice.OutputGain = 0.25f;
        r.Voice.PitchNode.Pbi = r.Pbi;                               // [voice+0x1B4] != 0 after the build: V7 does not build again and does not run the second call
        Assert.True(r.Pass.RunVoiceStateMachine(r.Voice));

        Assert.Equal(Bits(c0c1), Bits(r.Aux.C08));                   // B5: [conn+8] = the old [conn+0xC], unpredicated, at the start
        Assert.Equal(Bits(0.25f * g), Bits(r.Aux.C0C));
        Assert.Equal(0x3F800000u, Bits(r.Aux.C10));                  // [conn+0x10] = the old [conn+0x14]
        Assert.Equal(0x3F800000u, Bits(r.Aux.C14));
        Assert.Equal(MonoToMono, Words(r.Aux.Descriptor.NextMatrix));   // F5: the unchanged pan copies prev to next
        Assert.Equal(MonoToMono, Words(r.Aux.Descriptor.PrevMatrix));
        Assert.Equal(0u, Bits(r.Dry.C0C));                           // still muted: B6
        Assert.Equal(0u, Bits(r.Dry.C08));
        Assert.False(r.Voice.Run2E);                                 // S2E = r5 = 0: a connection has bit 1 clear and vt+0x3C returned 0
        Assert.True(r.Voice.P2F);
    }

    [Fact]
    public void M6_012_B8_B11_F2_The_3D_branch_param_12_and_a_missing_table_entry_are_visible_stops()
    {
        // B8: [P+0xDC] & 3 != 0 calls 0xA5B9D0 / 0xA5993C (RECOVERABLE_GAP); B11: a non-null param_12 calls 0xA5D70C (dead on Cozmo: PostEvent flags {0,1,5,9,13}); F2: 0xA5975C reads [[voice+0x10]+0x34] for a dry connection.
        var r = Build();
        r.Pbi.Flags0E8 = 0x5D;                                       // the ctx constructor's value before 0x9BEB30 clears bits 0-1
        var ex = Assert.Throws<WwiseMissingBehaviourException>(() => WwiseVoiceBusPass.UpdateConnectionGains(r.Voice, r.Pbi, 1f, 0));
        Assert.Contains("0xA5B9D0", ex.Message);

        r = Build();
        ex = Assert.Throws<WwiseMissingBehaviourException>(() => WwiseVoiceBusPass.UpdateConnectionGains(r.Voice, r.Pbi, 1f, 0, new WwiseGainArg12(1, 2)));
        Assert.Contains("0xA5D70C", ex.Message);

        r = Build();
        r.Voice.SendTable = null;
        r.Dry.Flags6C = 0x01;                                        // an audible dry connection runs 0xA5975C (a muted one, bit 1 set, does not; an aux connection does not read the table)
        ex = Assert.Throws<WwiseMissingBehaviourException>(() => WwiseVoiceBusPass.UpdateConnectionGains(r.Voice, r.Pbi, 1f, 0));
        Assert.Contains("[voice+0x10]", ex.Message);
    }

    [Fact]
    public void M6_012_F7_The_unread_channel_config_arms_stop_the_connection_update_visibly()
    {
        // A stereo voice into a stereo line is standard-to-standard with two outputs: the pan arithmetic of 0xA1F79C (RECOVERABLE_GAP) is a required stop, not a guessed matrix.
        var r = Build();
        r.Voice.Word0xF0 = 0x3102;
        r.CozmoRobot.Format64 = 0x3102;
        r.RobotBus.Format64 = 0x3102;
        var ex = Assert.Throws<WwiseMissingBehaviourException>(() => WwiseVoiceBusPass.UpdateConnectionGains(r.Voice, r.Pbi, 1f, 0));
        Assert.Contains("0xA1F79C", ex.Message);
    }

    [Fact]
    public void M6_012_D3_D4_The_mix_of_the_concrete_Sound_is_silent_dry_and_ramps_aux_with_the_gain_composition()
    {
        var r = Build();
        Assert.True(r.Pass.RunVoiceStateMachine(r.Voice));          // frame 1
        r.Voice.OutputGain = 0.25f;
        r.Voice.PitchNode.Pbi = r.Pbi;
        Assert.True(r.Pass.RunVoiceStateMachine(r.Voice));          // frame 2: aux start gain (conn+0x10 * conn+8) = 0.5 * g, end gain (conn+0x14 * conn+0xC) = 0.25 * g

        float g = BitConverter.UInt32BitsToSingle(Lin0Db);
        var S = r.Voice.Buffer.State;
        S.Data = Enumerable.Repeat(0.5f, 8).ToArray();
        S.ChannelConfig = 0x4101;
        S.MaxFrames = 8;
        S.ValidFrames = 8;
        r.Voice.FilterA.InitA764D4(1, 0, null);
        r.Voice.FilterB.InitA764D4(1, 0, null);
        r.Voice.CountCC = 1;
        r.Voice.AuxEntries2C[0].Id = r.RobotBus.Context.Key;        // 0xA68A2C(line+0x4C): the line's key
        r.Voice.AuxEntries2C[0].Current = 1f;                        // g = {sum of the entries' current, sum of their target} (C40.4 T-A8)
        r.Voice.AuxEntries2C[0].Target = 1f;
        r.RobotBus.ReleaseBuffer(); r.CozmoRobot.ReleaseBuffer();
        r.RobotBus.State = 4; r.CozmoRobot.State = 4;

        r.Voice.MixConnections(true);

        // dry (D1/D2): start = (conn+0x10 * conn+8) * 1 = 0 and end = (conn+0x14 * conn+0xC) * 1 = 0: 0xA46668 with inc == 0 and start == 0 returns, the bus stays silent
        Assert.All(r.CozmoRobot.Buffer, v => Assert.Equal(0u, Bits(v)));
        // aux: start = (1.0f * (0.5 * g)) * 1, end = (1.0f * (0.25 * g)) * 1, matrix [1, 0, 0, 0] both: lane 0 of the ramp is the start gain itself (0xA46720 str r2,[r3])
        float start = (1f * (0.5f * g)) * 1f, end = (1f * (0.25f * g)) * 1f;
        Assert.Equal(Bits(0.5f * start), Bits(r.RobotBus.Buffer[0]));
        Assert.True(r.RobotBus.Buffer[7] < r.RobotBus.Buffer[0] && r.RobotBus.Buffer[7] > 0.5f * end, "the gain ramps down from the start to the end gain across the frame");
        Assert.Equal(8, r.RobotBus.Frames);
        Assert.Equal(WwiseMixBus.EStateMixed, r.RobotBus.EState);
    }
}
