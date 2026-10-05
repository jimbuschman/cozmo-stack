// fidelity: M6-011, M6-022
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-011 / correction C43 (<c>re-analysis/research/20261005-B-M6b-4-voice-filter-13.md</c>, its Verification section): the voice filter A/B. The bit-for-bit comparisons with the engine's own code are in
/// <c>WwiseVoiceFilterOracleTests.cs</c> (Unicorn oracle); the tests here are the rows' hand values (A1..A9, K, D1, the concrete Sound of section 4) and the live entries. Every expected value is the report's, never read back from
/// <see cref="WwiseVoiceFilter"/>; the first line of each test names the record id and the citation.
/// </summary>
public partial class WwiseVoiceFilterTests
{
    private static void Close(float expected, float actual, float tolerance)
    {
        Assert.True(MathF.Abs(expected - actual) <= tolerance,
            $"expected {expected:R}, got {actual:R} (tolerance {tolerance:R})");
    }

    private static WwiseVoiceFilter NewFilter(int channels = 1)
    {
        var f = new WwiseVoiceFilter(WwiseVoiceFilterRole.A);
        Assert.Equal(1, f.InitA764D4((uint)channels, 0, null));
        return f;
    }

    private static void SetState(WwiseVoiceFilterBand b, float cur, float target, ushort steps, sbyte countdown, byte dirty, byte first, byte bypass)
    {
        b.Current = cur; b.Target = target; b.Steps = steps; b.Countdown = countdown; b.Dirty = dirty; b.First = first; b.Bypass = bypass;
    }

    /// <summary>Both bands bypassed and idle (steady state, dirty 0): the second band does nothing to the data.</summary>
    private static void Idle(WwiseVoiceFilterBand b) => SetState(b, 0f, 0f, 8, 0, 0, 0, 1);

    private static void Run(WwiseVoiceFilter f, float[] data, int channels, int frames, int stride)
        => f.ProcessA4C60C(new WwiseDecodeState { Data = data, ChannelConfig = (uint)channels, MaxFrames = (ushort)stride, ValidFrames = (ushort)frames });

    // ------------------------------------------------------------------ M1: the cutoff map

    [Fact]
    public void M6_011_M1_The_cutoff_map_pins_the_reports_executed_values()
    {
        // Report section 4 / Verification row verdicts (the real 0xA7A3D8 / 0xA7A4AC executed): LPF 15 -> 13500.0 (0x4652F000), 34 -> 4948.73, 49 -> 1360.44; HPF 15 -> 61.238, 40 -> 526.80 (not the old inventory's ~445).
        Assert.Equal(0x4652F000u, Bits(WwiseVoiceFilterCutoff.LowPass(15f, 48000)));
        Close(4948.73f, WwiseVoiceFilterCutoff.LowPass(34f, 48000), 0.01f);
        Close(1360.44f, WwiseVoiceFilterCutoff.LowPass(49f, 48000), 0.01f);
        Close(61.238f, WwiseVoiceFilterCutoff.HighPass(15f, 48000), 0.001f);
        Close(526.80f, WwiseVoiceFilterCutoff.HighPass(40f, 48000), 0.01f);
    }

    [Fact]
    public void M6_011_M1_The_cutoff_map_caps_at_forty_five_percent_of_the_rate()
    {
        // M1: "Cap=(float)u32[0x105243C]*0.45f; result fc if cap>=fc else cap". v = 0 gives 7000 + 30 * 433.33334 = 20000 Hz, above the 14400 Hz cap at 32 kHz.
        Close(14400f, WwiseVoiceFilterCutoff.LowPass(0f, 32000), 0.001f);
        Close(20000f, WwiseVoiceFilterCutoff.LowPass(0f, 48000), 0.01f);
    }

    [Fact]
    public void M6_011_M1_vcvt_u32_f32_rounds_toward_zero_and_saturates_and_a_NaN_gives_zero()
    {
        // The ARM vcvt.u32.f32 instruction (M1: "u=(u32)bits (vcvt.u32.f32 round to zero)"): the engine's map applies it to 1065353216.0f + (100 - v) * 1042939.9375f; the oracle's Map rows pin the NaN / negative / huge v cases.
        Assert.Equal(0u, WwiseVoiceFilterCutoff.CvtU32(float.NaN));
        Assert.Equal(0u, WwiseVoiceFilterCutoff.CvtU32(-5f));
        Assert.Equal(7u, WwiseVoiceFilterCutoff.CvtU32(7.9f));
        Assert.Equal(uint.MaxValue, WwiseVoiceFilterCutoff.CvtU32(1e20f));
    }

    [Fact]
    public void M6_011_SetRate_chunk_is_floor_rate_times_128_over_48000()
    {
        // Report section 1 (SetRate 0xA1C768..0xA1C794): u32[0x105244C] = ((rate << 7) * 0x057619F1) >> 32 >> 10 = floor(rate * 128 / 48000) = 128 at 48000 (initial value 0x80).
        Assert.Equal(128u, WwiseVoiceFilter.ChunkForRate(48000));
        Assert.Equal(256u, WwiseVoiceFilter.ChunkForRate(96000));
        Assert.Equal(117u, WwiseVoiceFilter.ChunkForRate(44100));
        Assert.Equal(85u, WwiseVoiceFilter.ChunkForRate(32000));
        Assert.Equal(128u, new WwiseVoiceFilter(WwiseVoiceFilterRole.A).RampChunk);
        Assert.Equal(48000u, new WwiseVoiceFilter(WwiseVoiceFilterRole.A).MixRate);
    }

    // ------------------------------------------------------------------ the concrete Sound (report section 4)

    [Fact]
    public void M6_011_C43_4_The_concrete_Sound_LPF_15_designs_the_reports_coefficients()
    {
        // Report section 4 (and the verifier's row verdicts): fc = 13500.0 (0x4652F000), a = (13500.0f / 48000.0f) * pi = 0x3F6231D6, the correctly rounded tan 0x3F9BF7EC; B0 0x3EB4A7B9, B1 0x3F34A7B9, B2 = B0, A1n 0xBE6BECFA, A2n 0xBE3950CB.
        float pi = FromBits(0x40490FDB);
        Assert.Equal(0x3F6231D6u, Bits((13500.0f / 48000.0f) * pi));
        Assert.Equal(0x3F9BF7ECu, Bits(WwiseHostMath.Tanf(FromBits(0x3F6231D6))));

        var f = NewFilter();
        SetState(f.LowPass, 15f, 15f, 8, 0, 1, 1, 1);               // the state the setter leaves for target 15 after the init: first apply
        SetState(f.HighPass, 0f, 0f, 8, 0, 1, 1, 1);
        var data = Floats(0, 77, 1024);
        Run(f, data, 1, 1024, 1024);

        var lp = f.LowPass;
        Assert.Equal(0x3EB4A7B9u, Bits(lp.F[32]));
        Assert.Equal(0x3F34A7B9u, Bits(lp.F[33]));
        Assert.Equal(0x3EB4A7B9u, Bits(lp.F[34]));
        Assert.Equal(0xBE6BECFAu, Bits(lp.F[35]));
        Assert.Equal(0xBE3950CBu, Bits(lp.F[36]));
        Assert.Equal((0, 0, 8), ((int)lp.Bypass, (int)lp.Countdown, (int)lp.Steps));    // engaged at once: no ramp, steps 8
        Assert.Equal((15f, 15f), (lp.Current, lp.Target));
        // HPF target 0 -> bypassed on the first apply and stays so; filter B bypassed (its bands never got a target)
        Assert.Equal(1, f.HighPass.Bypass);
        Assert.Equal(0, f.HighPass.First);
        var b = NewFilter();
        var dataB = Floats(0, 78, 1024);
        var copy = (float[])dataB.Clone();
        Run(b, dataB, 1, 1024, 1024);
        Assert.Equal(1, b.LowPass.Bypass);
        Assert.Equal(1, b.HighPass.Bypass);
        Assert.Equal(copy, dataB);
    }

    [Fact]
    public void M6_011_C43_3_The_matrix_rows_are_the_reports_section_5_formulas_of_the_designed_coefficients()
    {
        // Report section 5 (all 28 values bit-exact against the real function): R0 = splat(B0), R1 = (0,0,0,P1), R2 = (0,0,P1,Q2), R3 = (0,P1,Q2,Q3), R4 = (B1,M1,M2,M3), R5 = (B0,N1,N2,N3), R6 = (A1,K1,K2,K3), R7 = (A2,L1,L2,L3),
        // transcribed here from the report's text over the concrete Sound's bits (B0 0x3EB4A7B9, B1 0x3F34A7B9, A1n 0xBE6BECFA, A2n 0xBE3950CB).
        var f = NewFilter();
        SetState(f.LowPass, 15f, 15f, 8, 0, 1, 1, 1);
        SetState(f.HighPass, 0f, 0f, 8, 0, 1, 1, 1);
        Run(f, new float[1024], 1, 1024, 1024);
        float B0 = FromBits(0x3EB4A7B9), B1 = FromBits(0x3F34A7B9), A1 = FromBits(0xBE6BECFA), A2 = FromBits(0xBE3950CB);
        float D = B0 + B0;
        float P1 = B1 + (B0 * A1);
        float Q2 = (B0 + (A1 * P1)) + (B0 * A2);
        float Q3 = ((B0 * (A1 * A2)) + (A1 * Q2)) + (B1 * A2);
        float M1 = B0 + (B1 * A1);
        float M2 = (B1 * A2) + (A1 * M1);
        float M3 = ((B0 * A2) + (A1 * M2)) + (A1 * (B1 * A2));
        float N1 = B0 * A1;
        float N2 = (B0 * A2) + (A1 * N1);
        float N3 = (A1 * (A2 * D)) + (A1 * (A1 * N1));
        float K1 = (A1 * A1) + A2;
        float K2 = (A1 * (A1 * A1)) + (A1 * (A2 + A2));
        float K3 = ((A1 * (A1 * (A2 * 3.0f))) + (A1 * (A1 * (A1 * A1)))) + (A2 * A2);
        float L1 = A1 * A2;
        float L2 = (A2 * A2) + (A1 * (A1 * A2));
        float L3 = (A1 * (A2 * (A2 + A2))) + (A1 * (A1 * (A1 * A2)));
        float[] expected =
        {
            B0, B0, B0, B0,  0, 0, 0, P1,  0, 0, P1, Q2,  0, P1, Q2, Q3,
            B1, M1, M2, M3,  B0, N1, N2, N3,  A1, K1, K2, K3,  A2, L1, L2, L3,
        };
        for (int i = 0; i < 32; i++)
            Assert.True(Bits(expected[i]) == Bits(f.LowPass.F[i]) || (expected[i] == 0f && f.LowPass.F[i] == 0f), $"F[{i}] expected {Bits(expected[i]):X8} got {Bits(f.LowPass.F[i]):X8}");
    }

    // ------------------------------------------------------------------ A1..A3: the entry

    [Fact]
    public void M6_011_A2_A_first_apply_with_target_at_or_below_0_1_bypasses_and_leaves_the_countdown_alone()
    {
        // A2: first != 0: first = 0, steps = 8, cur = target; target <= 0.1f (vcmpe; bls): bypass = 1, [B+0xA] NOT written. A9: the last two samples go into the history, the data is not modified.
        var f = NewFilter();
        SetState(f.LowPass, 0f, 0.05f, 0, 3, 1, 1, 0);
        Idle(f.HighPass);
        var data = new float[] { 1f, 2f, 3f, 4f };
        Run(f, data, 1, 4, 4);
        Assert.Equal((0.05f, (ushort)8, (sbyte)3, (byte)0, (byte)0, (byte)1), (f.LowPass.Current, f.LowPass.Steps, f.LowPass.Countdown, f.LowPass.Dirty, f.LowPass.First, f.LowPass.Bypass));
        Assert.Equal(new float[] { 4f, 3f, 4f, 3f }, f.LowPass.History);        // {x1, x2, y1, y2} = {last, second-last, last, second-last}
        Assert.Equal(new float[] { 1f, 2f, 3f, 4f }, data);
    }

    [Fact]
    public void M6_011_A2_A_first_apply_above_0_1_zeroes_the_countdown_and_engages_at_the_target()
    {
        // A2: otherwise [B+0xA] = 0, [B+0xD] = 0, design at cur (= target).
        var f = NewFilter();
        SetState(f.LowPass, 0f, 15f, 0, 3, 1, 1, 1);
        Idle(f.HighPass);
        Run(f, new float[8], 1, 8, 8);
        Assert.Equal((15f, (ushort)8, (sbyte)0, (byte)0, (byte)0, (byte)0), (f.LowPass.Current, f.LowPass.Steps, f.LowPass.Countdown, f.LowPass.Dirty, f.LowPass.First, f.LowPass.Bypass));
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)0xFF)]
    public void M6_011_A3_A_change_between_two_values_at_or_below_0_1_bypasses_at_once_without_touching_cur_or_the_countdown(byte dirty)
    {
        // A3: not first, cur <= 0.1f and not target > 0.1f: [B+0xD] = the dirty byte (strb r5), u16[B+8] = 8; [B+0] (cur) and [B+0xA] are NOT written.
        var f = NewFilter();
        SetState(f.LowPass, 0.05f, 0.02f, 5, 2, dirty, 0, 0);
        Idle(f.HighPass);
        var data = new float[] { 5f, 6f, 7f, 8f };
        Run(f, data, 1, 4, 4);
        Assert.Equal((0.05f, (ushort)8, (sbyte)2, (byte)0, dirty), (f.LowPass.Current, f.LowPass.Steps, f.LowPass.Countdown, f.LowPass.Dirty, f.LowPass.Bypass));
        Assert.Equal(new float[] { 8f, 7f, 8f, 7f }, f.LowPass.History);
        Assert.Equal(new float[] { 5f, 6f, 7f, 8f }, data);
    }

    [Theory]
    [InlineData(float.NaN, 100f)]
    [InlineData(50f, float.NaN)]
    [InlineData(0.05f, 15f)]
    [InlineData(50f, 100f)]
    public void M6_011_A3_Otherwise_a_ramp_starts_at_step_zero_and_a_NaN_cur_or_target_takes_the_ramp_path(float cur, float target)
    {
        // A3 ramp start: [B+0xA] = 0, [B+0xD] = the first byte (0), u16[B+8] = 0; "NaN cur or target takes the ramp path" (the compares bls / bhi are on the unordered flags).
        var f = NewFilter();
        SetState(f.LowPass, cur, target, 8, 2, 1, 0, 0);
        Idle(f.HighPass);
        Run(f, new float[128], 1, 128, 128);
        Assert.Equal(1, f.LowPass.Steps);                               // one 128-sample chunk: one step
        Assert.Equal(0, f.LowPass.Bypass);
        Assert.Equal(0, f.LowPass.Countdown);
    }

    // ------------------------------------------------------------------ A7: the ramp

    [Fact]
    public void M6_011_A7_The_ramp_takes_one_step_per_chunk_across_calls_and_cur_moves_only_when_the_eighth_step_is_done()
    {
        // A7: n = min(frames - pos, u32[0x105244C]); if steps <= 7: steps += 1, design at cur + ((float)steps * diff) * 0.125f; at the end, steps > 7: cur = target ("A ramp spanning calls keeps steps in [B+8]; cur changes only at the end").
        var f = NewFilter();
        SetState(f.LowPass, 50f, 100f, 8, 0, 1, 0, 0);
        Idle(f.HighPass);
        Run(f, new float[128], 1, 128, 128);
        Assert.Equal((1, 50f), ((int)f.LowPass.Steps, f.LowPass.Current));
        Run(f, new float[128], 1, 128, 128);
        Assert.Equal((2, 50f), ((int)f.LowPass.Steps, f.LowPass.Current));
        Run(f, new float[640], 1, 640, 640);                           // steps 3..7 on 5 chunks of 128
        Assert.Equal((7, 50f), ((int)f.LowPass.Steps, f.LowPass.Current));
        Run(f, new float[100], 1, 100, 100);                           // the eighth step on a short chunk
        Assert.Equal((8, 100f), ((int)f.LowPass.Steps, f.LowPass.Current));
        Assert.Equal(0, f.LowPass.Countdown);                           // target above 0.1f: no countdown
    }

    [Fact]
    public void M6_011_A7_A_ramp_ending_at_or_below_0_1_arms_the_countdown_and_the_filter_runs_four_more_calls()
    {
        // A7 end: steps > 7: cur = target and, target <= 0.1f, [B+0xA] = 4. A8 tail: cd > 0: cd -= 1; the result 0 sets bypass = 1 ("filter runs 4 calls after the ramp end, bypass from the 5th", report section 6 item 13).
        var f = NewFilter();
        SetState(f.LowPass, 50f, 0.05f, 8, 0, 1, 0, 0);
        Idle(f.HighPass);
        Run(f, new float[1024], 1, 1024, 1024);                         // 8 chunks
        Assert.Equal((0.05f, 4), (f.LowPass.Current, (int)f.LowPass.Countdown));
        Assert.Equal(0, f.LowPass.Bypass);
        for (int expectedCd = 3; expectedCd >= 0; expectedCd--)
        {
            Run(f, new float[256], 1, 256, 256);                        // steady
            Assert.Equal(expectedCd, (int)f.LowPass.Countdown);
            Assert.Equal(expectedCd == 0 ? 1 : 0, (int)f.LowPass.Bypass);
        }
        var data = new float[] { 1f, 2f, 3f, 4f };
        Run(f, data, 1, 4, 4);                                         // the 5th call: bypassed, data unchanged
        Assert.Equal(new float[] { 1f, 2f, 3f, 4f }, data);
    }

    [Fact]
    public void M6_011_A7_A_ramp_with_no_frames_changes_nothing()
    {
        // A7: "frames==0: return, nothing changes (0xA76E00)".
        var f = NewFilter();
        SetState(f.LowPass, 50f, 100f, 0, 0, 0, 0, 0);
        Idle(f.HighPass);
        Run(f, new float[4], 1, 0, 4);
        Assert.Equal((0, 50f), ((int)f.LowPass.Steps, f.LowPass.Current));
    }

    // ------------------------------------------------------------------ A8, K1..K3

    [Fact]
    public void M6_011_A8_The_steady_state_does_not_redesign_and_filters_every_channel_with_its_own_history_over_u16_S_0xE_frames()
    {
        // A6/A8: steps >= 8 and not bypassed: the stored F is used as it is (no redesign); ch = byte[S+4] channels, frames = u16[S+0xE] (not the plane stride u16[S+0xC]), plane c at c * stride, history c at 16 * c bytes.
        // K3: with only B0 = 2 the scalar tail gives y = 2 * x (frames 3 < 4: no NEON block).
        var f = NewFilter(2);
        f.LowPass.F[32] = 2f;
        SetState(f.LowPass, 15f, 15f, 8, 0, 0, 0, 0);
        Idle(f.HighPass);
        var data = new float[16];
        data[0] = 1; data[1] = 2; data[2] = 3; data[3] = 99;
        data[8] = 4; data[9] = 5; data[10] = 6; data[11] = 99;
        Run(f, data, 2, 3, 8);
        Assert.Equal(new float[] { 2, 4, 6, 99, 0, 0, 0, 0, 8, 10, 12, 99, 0, 0, 0, 0 }, data);
        Assert.Equal(new float[] { 3, 2, 6, 4, 6, 5, 12, 10 }, f.LowPass.History);       // per channel {x1, x2, y1, y2}
        Assert.Equal(2f, f.LowPass.F[32]);                                               // not redesigned
        Assert.Equal(15f, f.LowPass.Current);
    }

    [Fact]
    public void M6_011_D1_The_NEON_block_flushes_denormals_to_zero_and_the_VFP_tail_keeps_them()
    {
        // D1: the aligned 4-sample block is NEON vmul.f32 / vadd.f32 (flush-to-zero inputs and outputs); the scalar tail is VFP and follows the process FPSCR (Android default FZ = 0: denormals kept; BLOCKED_EXTERNAL for a given phone).
        // R0 = splat(1e-30f) and B0 = 1e-30f: a sample of 1e-10f gives the denormal 1e-40f. Five samples: lanes 0..3 are the block (0), sample 4 is the tail.
        var f = NewFilter();
        for (int k = 0; k < 4; k++) f.LowPass.F[k] = 1e-30f;
        f.LowPass.F[32] = 1e-30f;
        SetState(f.LowPass, 15f, 15f, 8, 0, 0, 0, 0);
        Idle(f.HighPass);
        var data = new float[] { 1e-10f, 1e-10f, 1e-10f, 1e-10f, 1e-10f };
        Run(f, data, 1, 5, 5);
        for (int i = 0; i < 4; i++) Assert.Equal(0u, Bits(data[i]));
        Assert.True(Bits(data[4]) != 0 && Bits(data[4]) < 0x00800000u, $"tail sample {Bits(data[4]):X8}");
    }

    [Fact]
    public void M6_011_K1_A_buffer_that_is_not_16_byte_aligned_runs_a_scalar_head_before_the_blocks()
    {
        // K1: (p & 0xF) != 0: a scalar head of min((16 - (p & 0xF)) >> 2, n) samples, then blocks of four and the tail n' & 3. With the denormal setup of the FTZ test and the buffer at 8 bytes past a 16-byte boundary the
        // head is 2 samples (kept: VFP), then one block (flushed) and a tail of one (kept).
        var f = NewFilter();
        for (int k = 0; k < 4; k++) f.LowPass.F[k] = 1e-30f;
        f.LowPass.F[32] = 1e-30f;
        SetState(f.LowPass, 15f, 15f, 8, 0, 0, 0, 0);
        Idle(f.HighPass);
        var data = new float[] { 1e-10f, 1e-10f, 1e-10f, 1e-10f, 1e-10f, 1e-10f, 1e-10f };
        f.Process(data, 1, 7, 7, 8);
        bool[] kept = { true, true, false, false, false, false, true };
        for (int i = 0; i < 7; i++) Assert.Equal(kept[i], Bits(data[i]) != 0);
    }

    // ------------------------------------------------------------------ A9: the bypass history copy

    [Fact]
    public void M6_011_A9_The_bypass_history_copy_needs_two_frames_and_a_channel_and_does_not_modify_the_data()
    {
        // A9: frames <= 1: return, nothing written; ch == 0: return; else per channel p = base + c * stride + (frames - 2): [h+8] = p[1], [h+0xC] = p[0], [h+0] = p[1], [h+4] = p[0].
        var f = NewFilter(2);
        Idle(f.HighPass);
        SetState(f.LowPass, 0f, 0f, 8, 0, 0, 0, 1);
        f.LowPass.History = new float[] { 9, 9, 9, 9, 9, 9, 9, 9 };
        Run(f, new float[8], 2, 1, 4);
        Assert.Equal(new float[] { 9, 9, 9, 9, 9, 9, 9, 9 }, f.LowPass.History);       // one frame: nothing written
        Run(f, new float[8], 0, 3, 4);
        Assert.Equal(new float[] { 9, 9, 9, 9, 9, 9, 9, 9 }, f.LowPass.History);       // no channel: nothing written
        var data = new float[] { 1, 2, 3, 0, 4, 5, 6, 0 };
        Run(f, data, 2, 3, 4);
        Assert.Equal(new float[] { 3, 2, 3, 2, 6, 5, 6, 5 }, f.LowPass.History);
        Assert.Equal(new float[] { 1, 2, 3, 0, 4, 5, 6, 0 }, data);
    }

    // ------------------------------------------------------------------ the init and the Reset

    [Fact]
    public void M6_011_I2_The_Reset_sets_first_on_both_bands_and_zeroes_the_histories_and_touches_nothing_else()
    {
        // Report I2 / verifier item 6: 0xA7666C sets [+0x17C] = 1 and [+0x18C] = 1 and zeroes both histories; cur, target, steps, countdown, dirty and bypass are not touched. A null history is skipped (0xA76680 cmp r0,#0).
        var f = NewFilter(2);
        SetState(f.LowPass, 1f, 2f, 3, 4, 0, 0, 0);
        SetState(f.HighPass, 5f, 6f, 7, 8, 0, 0, 1);
        Array.Fill(f.LowPass.History!, 7f);
        Array.Fill(f.HighPass.History!, 8f);
        f.ResetA7666C();
        Assert.Equal((1, 0, 0, 1), (f.LowPass.First, f.LowPass.Dirty, f.LowPass.Bypass, f.HighPass.First));
        Assert.Equal((1f, 2f, (ushort)3, (sbyte)4), (f.LowPass.Current, f.LowPass.Target, f.LowPass.Steps, f.LowPass.Countdown));
        Assert.Equal((5f, 6f, (ushort)7, (sbyte)8, (byte)0, (byte)1), (f.HighPass.Current, f.HighPass.Target, f.HighPass.Steps, f.HighPass.Countdown, f.HighPass.Dirty, f.HighPass.Bypass));
        Assert.All(f.LowPass.History!, x => Assert.Equal(0f, x));
        Assert.All(f.HighPass.History!, x => Assert.Equal(0f, x));
        var none = new WwiseVoiceFilter(WwiseVoiceFilterRole.B);
        none.ResetA7666C();                                              // never initialised: no history to clear, no fault
        Assert.Equal(1, none.LowPass.First);
    }

    [Fact]
    public void M6_011_I2_The_holder_vt_0x14_resets_filter_A_through_the_live_entry()
    {
        // 0xA4C5D8 (row 2.10): bl 0xA7666C(voice+0x1C0+0x10) then the upstream node's vt+0x14. Live entry: the V7 path 0xA55534 (S2F == 0, [voice+0xE4] == 2, previous S2F in [voice+0xCD] bit 0, [voice+0xE0] == 2) reaches HolderVt14.
        var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 7, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, continuous: false) { StartOffset = 0xFFFFFFFF };
        var voice = new WwiseLiveVoice(1, 8) { Source = new StubSource(), BusOwner8 = pbi, E0 = 2, E4 = 2, FlagsCD = 1 };
        voice.PitchNode.Pbi = pbi;
        voice.FilterA.InitA764D4(1, 0, null);
        voice.FilterB.InitA764D4(1, 0, null);
        SetState(voice.FilterA.LowPass, 15f, 15f, 8, 0, 0, 0, 0);
        Array.Fill(voice.FilterA.LowPass.History!, 3f);
        Array.Fill(voice.FilterB.LowPass.History!, 3f);
        voice.FilterB.LowPass.First = 0;
        int pitchVt14 = 0;
        voice.PitchNodeVt14 = _ => pitchVt14++;
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1));
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState())
        {
            SourceOwner = _ => pbi,
            Limiter = new WwisePlaybackLimiter(_ => null),
            CalcEffectiveParamsVt24 = _ => { },
            DuckingOverrideA4B4B0 = _ => { },
            ConnectionGainsOverrideA4BC58 = (v, p, gain, arg5, arg12, outs) => { v.Run2E = false; v.P2F = false; return false; },   // S2F == 0
        };
        pass.RunVoiceStateMachine(voice);
        Assert.Equal(1, pitchVt14);
        Assert.Equal(1, voice.FilterA.LowPass.First);
        Assert.Equal(1, voice.FilterA.HighPass.First);
        Assert.All(voice.FilterA.LowPass.History!, x => Assert.Equal(0f, x));
        Assert.Equal((15f, (ushort)8, (byte)0, (byte)0), (voice.FilterA.LowPass.Current, voice.FilterA.LowPass.Steps, voice.FilterA.LowPass.Dirty, voice.FilterA.LowPass.Bypass));
        Assert.Equal(0, voice.FilterB.LowPass.First);                   // only filter A (voice+0x1C0) is reset
        Assert.All(voice.FilterB.LowPass.History!, x => Assert.Equal(3f, x));
    }

    private sealed class StubSource : IWwiseVoiceSource
    {
        public int Channels => 1;
        public int SampleRate => 48000;
        public int Render(WwiseVoiceBuffer buffer) => 0x2D;
        public int StartStream(uint a, uint b) => 1;
        public bool StartStreamSucceeded { get; set; } = true;
    }

    // ------------------------------------------------------------------ the live entry: V7 writes the filter bands

    [Fact]
    public void M6_011_C43_1_The_V7_target_writes_hit_the_filter_bands_and_the_concrete_Sound_engages_the_LPF_at_the_first_pull()
    {
        // C43.1 / report T1..T6 and section 4: 0xA54F1C writes A-LPF, B-LPF, A-HPF, B-HPF into voice+0x340 / 0x510 / 0x350 / 0x520, which are the filter bands (node+0x170 / 0x180); for the concrete Sound S2F = 1, S2E = 0, f30 = 15.0, f38 = 0, f34 = f3C = 0.
        // The setter on the init state (cur 0, steps 8): target = 15.0, dirty = 1, cur = 0 + ((0 - 0) * 0.125f) * 8 = 0. A target equal to the stored one (0) runs no setter.
        var pbi = new WwisePlayingInstance(new WwisePlayInitParams { PlayingId = 7, TargetNodeId = 1 }, 1, new object(), new byte[0x44], null, continuous: false) { StartOffset = 0xFFFFFFFF };
        var voice = new WwiseLiveVoice(1, 8) { Source = new StubSource(), BusOwner8 = pbi, E0 = 0, E4 = 0, FlagsCD = 0, Word0xF0 = 1 };
        voice.PitchNode.Pbi = pbi;                                      // [voice+0x1B4] != 0: the build 0xA54A30 has run (the filters are initialised)
        voice.FilterA.InitA764D4(1, 0, null);
        voice.FilterB.InitA764D4(1, 0, null);
        voice.FilterB.LowPass.Dirty = 0; voice.FilterB.HighPass.Dirty = 0;          // a setter would set the dirty byte again: the markers tell which bands it touched
        var bus = new WwiseMixBus(default, Array.Empty<WwiseBusFxSlot>(), 8);
        voice.Connections.Add(new WwiseVoiceConnection(bus, 1, 1));
        var pass = new WwiseVoiceBusPass(new WwiseMixBusHierarchy(), new WwiseOutputDeviceState())
        {
            SourceOwner = _ => pbi,
            Limiter = new WwisePlaybackLimiter(_ => null),
            CalcEffectiveParamsVt24 = _ => { },
            DuckingOverrideA4B4B0 = _ => { },
            ConnectionGainsOverrideA4BC58 = (v, p, gain, arg5, arg12, outs) =>
            {
                v.Run2E = false; v.P2F = true;
                outs[0] = 15f; outs[1] = 0f; outs[2] = 0f; outs[3] = 0f;           // f30 (A-LPF), f34 (B-LPF), f38 (A-HPF), f3C (B-HPF)
                return true;
            },
        };
        pass.RunVoiceStateMachine(voice);

        Assert.Equal((15f, 1, 0f), (voice.FilterA.LowPass.Target, (int)voice.FilterA.LowPass.Dirty, voice.FilterA.LowPass.Current));
        Assert.Equal(0f, voice.FilterA.HighPass.Target);
        Assert.Equal((0f, 0, 0f, 0), (voice.FilterB.LowPass.Target, (int)voice.FilterB.LowPass.Dirty, voice.FilterB.HighPass.Target, (int)voice.FilterB.HighPass.Dirty));
        Assert.Same(voice.FilterA.LowPass, voice.Ramp340);                // the same record, not a copy
        Assert.Same(voice.FilterB.HighPass, voice.Ramp520);

        // E1 then runs the filter on the pass block: the LPF's first apply designs at 15.0 with no ramp.
        var state = voice.Buffer.State;
        var data = Floats(0, 4242, 1024);
        var input = (float[])data.Clone();
        state.Data = data; state.ChannelConfig = 1; state.MaxFrames = 1024; state.ValidFrames = 1024;
        voice.FilterA.ProcessA4C60C(state);
        var afterA = (float[])data.Clone();
        voice.FilterB.ProcessA4C60C(state);                              // E2: filter B is bypassed
        Assert.Equal(afterA, data);
        var lp = voice.FilterA.LowPass;
        Assert.Equal((0x3EB4A7B9u, 0x3F34A7B9u, 0x3EB4A7B9u, 0xBE6BECFAu, 0xBE3950CBu), (Bits(lp.F[32]), Bits(lp.F[33]), Bits(lp.F[34]), Bits(lp.F[35]), Bits(lp.F[36])));
        Assert.Equal((0, 8), ((int)lp.Bypass, (int)lp.Steps));
        Assert.Equal((1, 0), ((int)voice.FilterA.HighPass.Bypass, (int)voice.FilterA.HighPass.First));   // HPF target 0: bypassed at its first apply
        Assert.Equal(1, voice.FilterB.LowPass.Bypass);
        Assert.Equal(1, voice.FilterB.HighPass.Bypass);
        Assert.NotEqual(input, data);                                    // filtered by the LPF
    }

    [Fact]
    public void M6_011_C43_1_The_target_setter_runs_only_on_a_change_and_folds_the_old_target_into_cur()
    {
        // T5: the setter runs only when the target differs (a NaN always differs): dirty = 1, target = new, cur = cur + ((oldTarget - cur) * 0.125f) * (float)(s32)u16 steps (vsub; vmul; vcvt.f32.s32; vmla, non-fused).
        var a = new WwiseVoiceFilterBand(WwiseVoiceFilterKind.LowPass);
        SetState(a, 8f, 16f, 4, 0, 0, 0, 0);
        a.SetTarget(16f);
        Assert.Equal((8f, (byte)0), (a.Current, a.Dirty));              // equal: no setter
        a.SetTarget(40f);
        Assert.Equal((byte)1, a.Dirty);
        Assert.Equal(40f, a.Target);
        Assert.Equal(8f + ((16f - 8f) * 0.125f) * 4f, a.Current);        // 8 + 4 = 12
        Assert.Equal(12f, a.Current);
        a.Dirty = 0;
        a.SetTarget(float.NaN);
        Assert.Equal((byte)1, a.Dirty);                                  // a NaN differs from everything
    }

    // ------------------------------------------------------------------ the composition (unchanged by C43)

    [Fact]
    public void TheCompositionSumsTheNodeChainAndRandomizerAndStartsAtOneHundred()
    {
        // M6-011 gapE 1.1: the effective LPF/HPF is node-chain + randomizer (+ pbi+0xA0/+0xA8, zero in the shipped data). gapE 1.2: A is the minimum over the connections, starting at 100.
        var none = WwiseVoiceFilterComposer.Compose(0f, 0f, 0f, 0f, Array.Empty<WwiseVoiceFilterConnection>());
        Assert.Equal(100f, none.LowPassA);
        Assert.Equal(100f, none.HighPassA);

        var twoD = new[] { new WwiseVoiceFilterConnection(Is3D: false) };
        var composed = WwiseVoiceFilterComposer.Compose(15f, 0f, 0f, 0f, twoD);
        Assert.Equal(15f, composed.LowPassA);
        Assert.Equal(0f, composed.HighPassA);

        // The randomizer draw is added to the node-chain sum (0x9FFD14..0x9FFD74).
        var withRandom = WwiseVoiceFilterComposer.Compose(15f, 0f, 5f, 2f, twoD);
        Assert.Equal(20f, withRandom.LowPassA);
        Assert.Equal(2f, withRandom.HighPassA);
    }

    [Fact]
    public void TheThreeDConnectionTakesTheGreaterOfContextAndAttenuation()
    {
        // M6-011 gapE 1.2: the 3D path takes max(ctx, attenuation) (0x00A5C740..0x00A5C774). The attenuation's own source is outside this record, so it is supplied by the caller here.
        var threeD = new[] { new WwiseVoiceFilterConnection(Is3D: true, AttenuationLpf: 40f, AttenuationHpf: 10f) };
        var composed = WwiseVoiceFilterComposer.Compose(15f, 20f, 0f, 0f, threeD);
        Assert.Equal(40f, composed.LowPassA);    // max(15, 40)
        Assert.Equal(20f, composed.HighPassA);   // max(20, 10)
    }

    [Fact]
    public void TheCompositionTakesTheMinimumAndClampsTheOutputBus()
    {
        // M6-011 gapE 1.2, 1.3: A is the minimum over connections; B is max(min(B), outputBus) and is clamped to 0..100.
        var connections = new[]
        {
            new WwiseVoiceFilterConnection(Is3D: true, AttenuationLpf: 40f, AttenuationHpf: 0f),
            new WwiseVoiceFilterConnection(Is3D: false),
        };
        var composed = WwiseVoiceFilterComposer.Compose(15f, 30f, 0f, 0f, connections,
            outputBusLpf: 20f, outputBusHpf: 200f);
        Assert.Equal(15f, composed.LowPassA);    // min(max(15,40), 15)
        Assert.Equal(30f, composed.HighPassA);   // min(max(30,0), 30)
        Assert.Equal(20f, composed.LowPassB);    // max(0, 20)
        Assert.Equal(100f, composed.HighPassB);  // max(0, 200) clamped
    }
}
