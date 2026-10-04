using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// R-FIX2 stream B: M10-002, M10-009, M10-010, M10-011, M15-004, M15-008, M15-009, M15-011, M15-013, M15-015, M3-024, M6-003.
/// Expected values come from the cited instructions in libcozmoEngine.so, never from the implementation's own output.
/// </summary>
public class RFix2BTests
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);

    private static RobotState State(uint t, float gz, float left, float right, RobotStatusFlag flags = 0)
    {
        var s = new RobotState
        {
            Timestamp = t, Status = (uint)flags,
            Accel = new AccelData { X = 0, Y = 0, Z = 9800 }, Gyro = new GyroData { X = 0, Y = 0, Z = gz },
            Pose = new RobotPose(), LwheelSpeedMmps = left, RwheelSpeedMmps = right,
        };
        return s;
    }

    private static UnexpectedMovementDetector Mov() => new() { IsPhysical = true };

    // ---------------------------------------------------------------- M10-002

    /// <summary>
    /// M10-002, 0x0063DAB0/0x0063DAB6: MovementComponent's constructor stores movw 0xB8C2 / movt 0x3E32 as the gyro threshold
    /// (+0xA4), compared by vcmpe.f32 at 0x0063E4B6..0x0063E4C2 (bpl: |gz| &gt;= threshold takes the active-gyro path).
    /// </summary>
    [Fact]
    public void M10_002_GyroThresholdIsTheEngineBinary32()
    {
        Assert.Equal(0x3E32B8C2u, Bits(UnexpectedMovementDetector.GyroTurnThresholdRadps));
        Assert.NotEqual(Bits(0.174533f), Bits(UnexpectedMovementDetector.GyroTurnThresholdRadps));   // 0x3E32B8C7 is the old decimal
    }

    /// <summary>
    /// Wheels l=-50, r=50 (opposite signs): a quiet gyro (|gz| below the threshold) adds 1 (0x0063E4C4 onward); an active gyro of the same
    /// sign as (r-l) is a decay (count stays 0). So the two ULPs 0x3E32B8C1 (quiet) and 0x3E32B8C2 (active, equal) and 0x3E32B8C3 (active; the old
    /// decimal's 0x3E32B8C7 would have called it quiet) separate the engine's threshold from the decimal one.
    /// </summary>
    [Theory]
    [InlineData(0x3E32B8C1u, 1)]
    [InlineData(0x3E32B8C2u, 0)]
    [InlineData(0x3E32B8C3u, 0)]
    [InlineData(0x3E32B8C6u, 0)]
    public void M10_002_QuietVersusActiveGyroAtTheBinary32Boundary(uint gzBits, int expectedCount)
    {
        var d = Mov();
        d.Update(State(100, F(gzBits), -50, 50));
        Assert.Equal(expectedCount, d.Count);
        var neg = Mov();                                       // the sign test is on the value, the magnitude is |gz|
        neg.Update(State(100, -F(gzBits), -50, 50));
        Assert.Equal(expectedCount == 1 ? 1 : 2, neg.Count);   // active with the opposite sign of (r-l) adds 2 (0x0063E524)
    }

    // ---------------------------------------------------------------- M15-004

    private static DecayConfig DecayWith(params DecayModifierEntry[] entries) =>
        new(new Dictionary<NeedId, IReadOnlyList<(double, double)>>(), new Dictionary<NeedId, IReadOnlyList<(double, double)>>(),
            new Dictionary<NeedId, IReadOnlyList<DecayModifierEntry>> { [NeedId.Repair] = entries });
    private static DecayModifierEntry E(double threshold, params (NeedId, double)[] pairs) => new(threshold, pairs);

    /// <summary>
    /// M15-004, GetDecayMultipliers 0x0069C214: the out array starts at 1.0f (0x0069C222), the level and threshold compare as binary32
    /// (vcmpe.f32 0x0069C270 + bge 0x0069C278), the multiply is vmul.f32 (0x0069C2A4). The level 0.2999999999 (a double below 0.3) is the
    /// float 0x3E99999A, equal to the float threshold 0.3f, so the engine matches the entry; the double compare would not.
    /// </summary>
    [Fact]
    public void M15_004_DecayMultipliersCompareInBinary32()
    {
        var decay = DecayWith(E(0.3, (NeedId.Play, 2.0)));
        var m = decay.DecayMultipliers(n => n == NeedId.Repair ? 0.2999999999 : 1.0);
        Assert.Equal(0x40000000u, Bits(m[NeedId.Play]));          // x2.0f
        Assert.Equal(0x3F800000u, Bits(m[NeedId.Energy]));
        Assert.Equal(0x3F800000u, Bits(m[NeedId.Repair]));
        // one float ULP below the threshold the entry is not matched (bge not taken: level < threshold)
        var below = decay.DecayMultipliers(n => n == NeedId.Repair ? (double)F(0x3E999999) : 1.0);
        Assert.Equal(0x3F800000u, Bits(below[NeedId.Play]));
    }

    /// <summary>
    /// Two affected-need pairs of one matched entry both apply (the inner loop 0x0069C294..0x0069C2AE), each as the f32 product
    /// out[other] = multiplier * out[other]: 0.1f * 1.0f = 0x3DCCCCCD, then 0.3f * that = 0x3CF5C290 (numpy float32; the double product
    /// 0.1 * 0.3 rounds to 0x3CF5C28F).
    /// </summary>
    [Fact]
    public void M15_004_DecayMultipliersMultiplyInBinary32()
    {
        var decay = DecayWith(E(0.5, (NeedId.Play, 0.1), (NeedId.Play, 0.3)));
        var m = decay.DecayMultipliers(n => n == NeedId.Repair ? 0.5 : 1.0);
        Assert.Equal(0x3CF5C290u, Bits(m[NeedId.Play]));
        var one = DecayWith(E(0.5, (NeedId.Play, 0.1))).DecayMultipliers(n => 0.5);
        Assert.Equal(0x3DCCCCCDu, Bits(one[NeedId.Play]));
    }

    /// <summary>
    /// M15-004: one JSON entry is one engine entry (16 bytes: threshold + vector of pairs, 0x0069C27C); only the FIRST matching entry applies. Two entries with the same threshold
    /// (as the shipped Repair list has at 0) are two entries: the second never applies; the pairs inside the first entry all apply. A merge-by-threshold reading would give Play 2.0 * 3.0.
    /// </summary>
    [Fact]
    public void M15_004_EntriesWithTheSameThresholdAreSeparateAndOnlyTheFirstApplies()
    {
        var decay = DecayWith(E(0.4, (NeedId.Play, 2.0)), E(0.4, (NeedId.Play, 3.0), (NeedId.Energy, 5.0)));
        var m = decay.DecayMultipliers(n => n == NeedId.Repair ? 0.5 : 1.0);
        Assert.Equal(0x40000000u, Bits(m[NeedId.Play]));          // 2.0f only
        Assert.Equal(0x3F800000u, Bits(m[NeedId.Energy]));        // the second entry's Energy x5 never applies
    }

    /// <summary>The first (largest) threshold at or below the level is the only entry applied (0x0069C278 bge ends the scan).</summary>
    [Fact]
    public void M15_004_OnlyTheFirstDescendingEntryApplies()
    {
        var decay = DecayWith(E(0.5, (NeedId.Play, 3.0)), E(0.3, (NeedId.Play, 2.0)), E(0.0, (NeedId.Play, 5.0)));
        Assert.Equal(0x40400000u, Bits(decay.DecayMultipliers(n => 0.9)[NeedId.Play]));          // 0.5 entry: x3
        Assert.Equal(0x40000000u, Bits(decay.DecayMultipliers(n => 0.4)[NeedId.Play]));          // 0.3 entry: x2
        Assert.Equal(0x40A00000u, Bits(decay.DecayMultipliers(n => 0.1)[NeedId.Play]));          // 0 entry: x5
    }

    /// <summary>
    /// NumDamagedPartsForRepairLevel 0x0069CCAC: vcmpe.f32 s2(threshold), s0(level), 'mi' = threshold &lt; level returns the count
    /// (0x0069CCC8..0x0069CCD2). Thresholds 0.98, 0.6, 0.3 as floats; the level 0.6f (0x3F19999A) equals the second threshold as a float, so
    /// two parts count (the double compare of (double)0.6f against 0.6 would stop at one).
    /// </summary>
    [Fact]
    public void M15_004_DamagedPartsCompareInBinary32()
    {
        var state = new NeedsState(NeedsConfig.Default);
        Assert.Equal(2, state.NumDamagedPartsForRepairLevel(F(0x3F19999A)));
        Assert.Equal(1, state.NumDamagedPartsForRepairLevel(F(0x3F19999B)));   // one ULP above 0.6f: 0.6f < level, stop
        Assert.Equal(3, state.NumDamagedPartsForRepairLevel(F(0x3E99999A)));   // 0.3f: all three
        Assert.Equal(0, state.NumDamagedPartsForRepairLevel(1.0f));            // 0.98 < 1.0
        Assert.Equal(1, state.NumDamagedPartsForRepairLevel(0.98f));           // threshold float == level float counts
    }

    /// <summary>Through the live entry: NeedsManager.ApplyDecayAllNeeds asks the f32 multipliers (Repair 0.2999999999 -> Play x2.0f).</summary>
    [Fact]
    public void M15_004_TheManagerDecayUsesTheBinary32Multiplier()
    {
        var rates = new Dictionary<NeedId, IReadOnlyList<(double, double)>>
        { [NeedId.Play] = new[] { (0.0, 0.1) }, [NeedId.Repair] = new[] { (0.0, 0.0) }, [NeedId.Energy] = new[] { (0.0, 0.0) } };
        var decay = new DecayConfig(rates, rates, new Dictionary<NeedId, IReadOnlyList<DecayModifierEntry>>
            { [NeedId.Repair] = new[] { E(0.3, (NeedId.Play, 2.0)) } });
        double clock = 0;
        var mgr = new NeedsManager(() => clock, NeedsConfig.Default, decay);
        mgr.SetLevel(NeedId.Repair, 0.2999999999);
        mgr.SetLevel(NeedId.Play, 1.0);
        mgr.ApplyDecayAllNeeds(true, 60f);
        // rate 0.1f * 2.0f = 0.2f, elapsed 60f: delta = 0.2f * 60f / 60f
        float delta = 0.1f * 2.0f * 60f / 60f;
        Assert.Equal(1.0 - delta, mgr.State.GetNeedLevel(NeedId.Play), 6);
    }

    // ---------------------------------------------------------------- M15-015

    private sealed class NanoClock { public ulong Ns; }

    /// <summary>
    /// M15-015, SendData 0x0056EC48: the accumulated and resume times are u64 nanoseconds (0x0056EC72..0x0056EC84; the setter 0x0056EF00..0x0056EF10)
    /// and the report is <c>(int)round(acc / 1e9)</c> (ul2d, vdiv.f64 by the 1.0e9 literal at 0x0056EE28, round, vcvt.s32.f64), so 12.5 s reports
    /// 13 (C round() is half away from zero). A GameControl pause then clear (the InitConfiguration / SetCurrentActivity pair, 0x0056EFF8) stamps the
    /// resume time at 5000.000000123 s (5_000_000_000_123 ns), kept exactly as a u64.
    /// </summary>
    [Fact]
    public void M15_015_TheTrackerKeepsU64NanosAndRoundsHalfAwayFromZero()
    {
        var clk = new NanoClock { Ns = 5_000_000_000_123UL };
        var tracker = new FreeplayDataTracker(() => clk.Ns);
        Assert.Equal(5030f, tracker.NextSendSec);                     // f32(5000.000000123) + 30.0f (0x0056EBF6..0x0056EC04)
        Assert.Equal(0UL, tracker.LastResumeNanos);                   // +0x10/+0x14 = 0 from the constructor (0x0056EBE8)
        tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl, true);
        tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl, false);
        Assert.Equal(5_000_000_000_123UL, tracker.LastResumeNanos);   // the clear stamps now when the set becomes empty (0x0056F082..0x0056F096)

        var reports = new List<double>();
        tracker.ActiveFreeplayTime += reports.Add;
        clk.Ns += 12_500_000_000UL;
        tracker.ForceUpdate();                                        // SendData
        Assert.Equal(new[] { 13.0 }, reports);                        // round(12.5) = 13
        Assert.Equal(0UL, tracker.AccumulatedNanos);                  // zeroed (0x0056EDBC)
        Assert.Equal(5_012_500_000_123UL, tracker.LastResumeNanos);   // stamped now while unpaused (0x0056EDC4)
        Assert.Equal(5042.5f, tracker.NextSendSec);                   // f32(5012.500000123) + 30.0f = 5012.5f + 30.0f
    }

    /// <summary>
    /// Update 0x0056EC1A compares the f32 GetCurrentTimeInSeconds with the f32 +0x18 (vcmpe.f32 0x0056EC2E; <c>it lt</c> returns). At
    /// 5042.4995 s the f32 clock is 0x459D... = 5042.49951171875 &lt; 5042.5f (no send); at 5042.4998 s it rounds to 5042.5f and sends. A double compare
    /// of 5042.4998 against 5042.5 would not send.
    /// </summary>
    [Fact]
    public void M15_015_TheSendDeadlineIsAnF32SecondsCompare()
    {
        var clk = new NanoClock { Ns = 5_000_000_000_123UL };
        var tracker = new FreeplayDataTracker(() => clk.Ns);
        tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl, true);
        tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl, false);
        clk.Ns = 5_012_500_000_123UL; tracker.ForceUpdate();           // next send 5042.5f
        var reports = new List<double>();
        tracker.ActiveFreeplayTime += reports.Add;

        clk.Ns = 5_042_499_500_000UL; tracker.Update();                // f32(5042.4995) = 5042.49951171875 < 5042.5
        Assert.Empty(reports);
        Assert.Equal(5042.5f, tracker.NextSendSec);
        clk.Ns = 5_042_499_800_000UL; tracker.Update();                // f32(5042.4998) = 5042.5, not below
        Assert.Single(reports);
        Assert.Equal(30.0, reports[0]);                                // 5012.500000123 .. 5042.4998 = 29.9998 s of accumulated nanos, rounded to 30
    }

    /// <summary>
    /// A pause while the stored resume time is zero flushes nothing (the u64 test at 0x0056EEF8: <c>orrs r2, r1, r0; beq</c>), and the accumulator
    /// is not touched; SendData with the set non-empty adds nothing (0x0056EC5E: <c>cbz [+8]</c> skips the add).
    /// </summary>
    [Fact]
    public void M15_015_PauseBeforeAnyResumeStampFlushesNothing()
    {
        var clk = new NanoClock { Ns = 1_000_000_000_000UL };
        var tracker = new FreeplayDataTracker(() => clk.Ns);
        clk.Ns += 7_000_000_000UL;
        tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.OffTreads, true);
        Assert.Equal(0UL, tracker.AccumulatedNanos);
        clk.Ns += 9_000_000_000UL;
        var reports = new List<double>();
        tracker.ActiveFreeplayTime += reports.Add;
        tracker.ForceUpdate();                                         // paused: no add, acc 0: nothing reported
        Assert.Empty(reports);
        Assert.Equal(0UL, tracker.LastResumeNanos);                    // not stamped while paused (0x0056EDC0 cbnz)
    }

    // ---------------------------------------------------------------- M6-003

    // Expected samples for the three cases below were produced by running the shipped decoder FUN_00a7a194 (0x00A7A194, ARM) in Unicorn over
    // libcozmoEngine.so exactly as its callers do (0x00A725F8..0x00A72624): one call per channel c with the input at c * 36 bytes, the output at
    // 2 * c, the block count, the block stride (blockAlign) and the channel count as the fifth argument. The inputs set the header step index
    // of each sub-block to values above 88 (200, 255, 89, 91, 150, 120), which the engine does not clamp (0x00A7A1F4 ldrb [+2]; the first nibble
    // reads ldrsh [0x00FFD650 + 2 * index], 0x00A7A228).
    // case A: 3 channels, 2 blocks, block stride 108; the output is the emulated routine's, interleaved
    private static readonly string AdpcmInA = "5dffc800a8aa20303a93a301033a83aa9023a98120009008933109183a832191a123308a3affff0091a902299938a81922388a830a13a9331982a9990293081aa823933020a9883882ff590081011aa81883a22823899830aa9aa8818a31302a8881332a2380888a32081a1907010300180a8aaa2aa009a030a02a21a0182210211829993229330032321a029218380abaff64003001199398831aa989139830892a9903291120a02092312801a8801a38a1aa9add005b0030211098833190233820a82310a1a2a8211392a083a289a31890001181212a88";
    private static readonly short[] AdpcmOutA = { -163, -198, -126, 2908, 5494, -126, -17571, -6793, -4221, -32768, -17965, 6951, -32768, -32768, 10336, -29691, -17379, -5053, -15701, -14581, 3341, -13158, -22212, 798, 3029, -10650, -10764, -7482, -16956, -12866, 5895, -22689, -7133, 18056, -24426, 5028, 13318, -13371, 3449, 23369, -14806, 10628, 16843, -21332, 4102, 20402, -24891, 2916, 21480, -21655, 8309, 28344, -16752, 15173, 29235, -12295, 19630, 25183, -13105, 17199, 30340, -7948, 16463, 32767, -11296, 15794, 32159, -11904, 13968, 29392, -8030, 14521, 26876, -8533, 18043, 27333, -10820, 15756, 26086, -10405, 13677, 28732, -7759, 11787, 30450, -6729, 10757, 29513, -7666, 10445, 28093, -9086, 9025, 28867, -7279, 9799, 28633, -5636, 9565, 28846, -6276, 8499, 29816, -5694, 8305, 29992, -4813, 8834, 30152, -4973, 9955, 30297, -5410, 10100, 29900, -6072, 11027, 29780, -6433, 10426, 29889, -6761, 10973, 30585, -6264, 10874, 30314, -6174, 10784, 30560, -5599, 11030, 31083, -5823, 10956, 30879, -5891, 11432, 30940, -5830, 11864, 30884, -6110, 11584, 31037, -5957, 11839, 30806, -6003, 12163, 31100, -6213, 12373, 31368, -5945, 12411, 31334, -5771, 12377, 31428, -5550, 12346, 31571, -5636, 12318, 31649, -5610, 12188, 31578, -5444, 12165, 31642, -5423, 12273, 31544, -5325, 12410, 31669, -5378, 12393, 31750, -5459, 12409, 31764, -5473, 12336, 31857, -5486, 12376, 31797, -5498, 12340, 263, -70, 221, 262, -70, 221, 265, 28601, 28892, 260, 32767, 32767, 260, 32767, 32767, 256, 23534, 32767, 256, 31928, 32767, 252, 32767, 30224, 248, 25830, 23287, 244, 23728, 32767, 248, 17995, 30856, 248, 30156, 32767, 244, 28577, 32767, 242, 21398, 32767, 242, 25313, 28852, 242, 21754, 32767, 238, 16361, 32767, 238, 13420, 31787, 244, 12529, 32767, 244, 18202, 32767, 240, 20412, 32767, 236, 19743, 32098, 240, 17917, 29054, 242, 18470, 32767, 246, 21992, 32767, 246, 20620, 32767, 242, 20205, 32767, 242, 18315, 32767, 244, 20033, 31049, 248, 19096, 32610, 252, 18244, 31190, 252, 20051, 30932, 254, 20285, 29759, 256, 19645, 30399, 260, 20615, 31369, 260, 21144, 32603, 262, 21624, 32767, 260, 21769, 32767, 264, 22431, 32370, 262, 22551, 32490, 260, 22004, 31943, 264, 22103, 32639, 270, 22555, 32549, 268, 22966, 32767, 272, 22742, 32394, 278, 22946, 32190, 284, 23378, 32129, 284, 23322, 32521, 284, 23577, 32266, 288, 23716, 32220, 294, 23758, 32346, 298, 23720, 32384, 304, 23546, 32280, 300, 23577, 32311, 302, 23549, 32339, 306, 23419, 32417, 306, 23490, 32488, 310, 23469, 32552, 308, 23606, 32533, 308, 23659, 32586, 310, 23578, 32667, 310, 23505, 32594, 316, 23439, 32660, 312, 23379, 32648 };
    // case B: 1 channels, 2 blocks, block stride 40; the output is the emulated routine's, interleaved
    private static readonly string AdpcmInB = "0dff960000a2919a2a8281801993a3a9283828a0202333181811101281289818a393a8a2000000003e005800a2133a9993182383a82a392398983319a2a981a223a2a98888893892312889a000000000";
    private static readonly short[] AdpcmOutB = { -243, -243, 3852, 22473, 5545, 14778, 6384, -6334, -13271, -23782, -14227, -5541, -7120, -2813, -4118, -2932, -4010, -6951, -4277, 1396, -814, 3874, 830, -830, -3346, -3803, -1724, -2102, 303, -9, 1411, 1669, 496, 709, 1679, 2913, 3714, 4734, 5661, 5541, 5869, 5770, 6041, 6287, 6511, 6579, 6764, 7044, 7197, 7336, 7294, 7256, 7430, 7399, 7313, 7287, 7358, 7509, 7411, 7536, 7488, 7474, 7408, 7468, 62, 20541, 1920, 25620, 32767, 18777, 32767, 25830, 19524, 32767, 27555, 25976, 30283, 32767, 32767, 32767, 31787, 30896, 26844, 23161, 26509, 24683, 28557, 32079, 32767, 32352, 31218, 30875, 29938, 31926, 32767, 32063, 32703, 32767, 31886, 31406, 30678, 31075, 30955, 31502, 31005, 31638, 32049, 32422, 32082, 31897, 31617, 31566, 31520, 31478, 31440, 31336, 31305, 31277, 31459, 31577, 31513, 31571, 31696, 31680, 31753, 31713, 31701, 31712 };
    // case C: 2 channels, 2 blocks, block stride 72; the output is the emulated routine's, interleaved
    private static readonly string AdpcmInC = "c7ff00008821388980a0231891838a33a911198a93900aa108a2a02a8339a933a93821005fff58001392a3a932a88382381892a08299912a880899918a2200a339200a1320a3a30002ff78009328281820008008318212092a22a133a338a99808a828939921232828a083025500ff0088199029232998920308092239822821212a8a223aa00a981982912a21919309";
    private static readonly short[] AdpcmOutC = { -57, -161, -57, 28510, -57, 32767, -55, 32767, -51, 23534, -51, 32767, -45, 20049, -47, 13112, -47, 2601, -47, 12156, -47, 24317, -47, 22738, -51, 15559, -45, 24695, -41, 23509, -41, 28902, -39, 27922, -37, 27031, -39, 32704, -33, 31968, -33, 32767, -37, 32767, -37, 31107, -31, 31610, -25, 29323, -27, 31402, -31, 31024, -29, 29994, -27, 29057, -29, 29909, -27, 29135, -31, 27962, -31, 29028, -25, 28834, -27, 28658, -27, 28498, -29, 28643, -33, 28246, -33, 27885, -31, 28213, -35, 27915, -35, 27463, -35, 27381, -31, 27754, -35, 28094, -35, 28155, -39, 28211, -43, 28568, -39, 28337, -33, 28211, -33, 28479, -35, 28513, -29, 28671, -31, 28528, -35, 28554, -29, 28720, -23, 28784, -25, 28803, -29, 28892, -29, 29005, -23, 28932, -21, 29025, -17, 28965, -17, 28976, -254, 85, -254, -1812, -12541, -5907, -16265, -17079, 663, -6922, -2414, -3845, 11576, -12239, 9033, -19870, 15970, -8308, 18072, 6407, 27627, 15962, 29364, 10750, 30943, 18646, 32378, 17211, 31073, 13296, 29887, 19229, 30965, 15993, 32767, 22857, 32767, 23748, 32767, 22938, 32031, 23674, 32767, 21665, 32767, 22273, 31107, 25040, 31610, 27556, 29323, 26184, 31402, 29095, 32767, 30985, 32767, 30642, 32767, 30330, 31347, 31750, 32767, 32524, 32767, 32767, 32767, 32767, 31797, 32767, 31621, 31886, 32742, 32687, 32305, 31959, 31643, 31827, 31523, 32428, 31195, 32767, 31096, 32270, 31186, 32767, 31104, 32767, 30731, 32394, 30663, 32054, 30971, 32115, 31363, 32059, 31210, 31906, 31071, 31767, 30945, 31893, 31060, 32084, 31234, 32050, 31455, 32144, 31598, 32058, 31572, 31928, 31690, 32046, 31669, 32110, 31767, 32208, 31784, 32261, 31703, 32213, 31806, 32316, 31793, 32276, 31853, 32240 };

    private static byte[] Hex(string s) => Convert.FromHexString(s);

    private static short[] DecodeAdpcm(int channels, int blockAlign, byte[] data) =>
        Cozmo.Robot.Animation.Wwise.WwiseAdpcm.Decode(new Cozmo.Robot.Animation.Wwise.WwiseMedia
        {
            Codec = Cozmo.Robot.Animation.Wwise.WwiseCodec.Adpcm, FormatTag = 2, Channels = channels, SampleRate = 44100,
            BlockAlign = blockAlign, AvgBytesPerSecond = 0, Data = data,
        });

    /// <summary>M6-003: a three-channel ADPCM file decodes (the engine gates only on format tag 2, 0x00A72704/0x00A73B2C; the decoder is generic in the channel count) with unclamped header step indices.</summary>
    [Fact]
    public void M6_003_ThreeChannelsAndUnclampedHeaderStepIndicesMatchTheEmulatedEngine()
        => Assert.Equal(AdpcmOutA, DecodeAdpcm(3, 108, Hex(AdpcmInA)));

    /// <summary>M6-003: mono with a block stride (blockAlign 40) larger than the 36 bytes the decoder consumes per channel block (the stride is the caller's r3, 0x00A72610), header indices 150 and 88.</summary>
    [Fact]
    public void M6_003_AMonoBlockStrideLargerThanThirtySixIsTheCallersStride()
        => Assert.Equal(AdpcmOutB, DecodeAdpcm(1, 40, Hex(AdpcmInB)));

    /// <summary>M6-003: stereo with header indices 0, 88, 120 and 255 (0x00A7A1F4 / 0x00A7A228).</summary>
    [Fact]
    public void M6_003_StereoHeaderIndicesAreUsedAsStored()
        => Assert.Equal(AdpcmOutC, DecodeAdpcm(2, 72, Hex(AdpcmInC)));

    // ---------------------------------------------------------------- M3-024

    private sealed class AudioSink : IAnimationSink
    {
        public int Samples, Silences, Ends;
        public void Face(FaceBitmap bitmap) { }
        public void Audio(byte[]? mulawFrame) { if (mulawFrame is null) Silences++; else Samples++; }
        public void Head(sbyte angleDeg, uint durationMs) { }
        public void Lift(byte heightMm, uint durationMs) { }
        public void AnimationStarted(byte tag) { }
        public void AnimationEnded() => Ends++;
        public void Body(BodyKeyframe keyframe) { }
        public void BodyStop() { }
        public void Lights(LightsKeyframe keyframe) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }

    private sealed class ToneSource : IAnimationAudioSource
    {
        private readonly int _n;
        public ToneSource(int samples) => _n = samples;
        public short[]? GetPcm(long eventId, float volume) => Enumerable.Repeat((short)8000, _n).ToArray();
        public string? NameOf(long eventId) => "tone";
    }

    private static AnimationClip SoundClip()
    {
        var frames = new List<Keyframe> { new AudioKeyframe(0, new long[] { 1 }, 1f, new[] { 1f }, false) };
        return new AnimationClip { Name = "sound", Keyframes = frames, Tracks = frames[0].Track, DurationMs = frames[0].EndTimeMs };
    }

    private static (AudioSink Sink, List<string> Log) RunSound(Func<RobotAudioOutputSource?>? source)
    {
        var sink = new AudioSink();
        var log = new List<string>();
        var s = new AnimationScheduler(sink) { AudioSource = new ToneSource(744 * 10), OutputSource = source, Log = log.Add };
        s.Play(SoundClip(), 0);
        for (double t = 0; t <= 1200; t += 33) s.Advance(t);
        return (sink, log);
    }

    /// <summary>
    /// M3-024, CreateAudioAnimation 0x00599FF0 (0x0059A070..0x0059A0B6): source 2 constructs RobotAudioAnimationOnRobot, which streams the sound to the robot
    /// (ten 744-sample frames of the 7440-sample tone); a scheduler with no client keeps that stand-in.
    /// </summary>
    [Fact]
    public void M3_024_SourceTwoStreamsTheAnimationAudioToTheRobot()
    {
        var (sink, _) = RunSound(() => RobotAudioOutputSource.PlayOnRobot);
        Assert.Equal(10, sink.Samples);
        Assert.Equal(1, sink.Ends);
        var (bare, _) = RunSound(null);
        Assert.Equal(10, bare.Samples);
    }

    /// <summary>
    /// M3-024: source 1 constructs RobotAudioAnimationOnDevice, whose PopRobotAudioMessage returns null first (0x00597530): nothing is streamed to the robot,
    /// and its InitAnimation runs the RobotAudio track to the end (0x00596898..0x00596914) so the animation still ends. The device playback it would schedule
    /// (Dispatch::After 0x005975A2) is not built and is reported MISSING once.
    /// </summary>
    [Fact]
    public void M3_024_SourceOneStreamsNothingToTheRobotAndReportsTheDevicePlaybackMissing()
    {
        var (sink, log) = RunSound(() => RobotAudioOutputSource.PlayOnDevice);
        Assert.Equal(0, sink.Samples);
        Assert.True(sink.Silences > 0);
        Assert.Equal(1, sink.Ends);
        Assert.Single(log, l => l.Contains("MISSING: RobotAudioClient::CreateAudioAnimation"));
    }

    /// <summary>
    /// M3-024: source 0 (the client constructor's value until a firmware version is handled, 0x005994E6) clears the animation's RobotAudio track and creates nothing
    /// (0x0059A11C..0x0059A12E); a null source from the live robot is that 0.
    /// </summary>
    [Fact]
    public void M3_024_SourceZeroClearsTheRobotAudioTrack()
    {
        var (sink, log) = RunSound(() => null);
        Assert.Equal(0, sink.Samples);
        Assert.Equal(1, sink.Ends);
        Assert.DoesNotContain(log, l => l.Contains("MISSING"));
        var (none, _) = RunSound(() => RobotAudioOutputSource.None);
        Assert.Equal(0, none.Samples);
    }

    /// <summary>
    /// M3-024 / M10-010 through the live robot: the scheduler the robot owns reads the output source HandleFirmwareVersion (0x005368F4) set. Before any firmware
    /// version it is the client's 0. A parse failure does nothing at all (cbz 0x00536932, no log); a null root is turned into an object by resolveReference (0x008EACCE),
    /// so "sim" is null and the robot is physical with source 2; an array or number root takes Json::throwLogicError (0x008EADC2..0x008EAE64) out of the handler, which
    /// this stack reports MISSING once (where the engine catches it is not established) and sets nothing; "sim" non-null is source 1.
    /// </summary>
    [Fact]
    public void M3_024_M10_010_TheRobotsSchedulerFollowsTheFirmwareVersionHandler()
    {
        using var rig = new Rig();
        var robot = rig.Robot;
        var engineRobot = robot.Engine.Robot!;
        var provider = robot.Animations.Scheduler.OutputSource;
        Assert.NotNull(provider);
        Assert.Null(provider!());
        var log = new List<string>();
        robot.Engine.LogLine += log.Add;
        FirmwareVersion Fw(string json) => new() { Signature = System.Text.Encoding.UTF8.GetBytes(json) };

        engineRobot.HandleFirmwareVersion(Fw("{not json"));                       // parse failure: nothing, no log
        Assert.Null(provider());
        Assert.False(engineRobot.IsPhysicalRobot);
        Assert.False(engineRobot.PhysicalRobotRecorded);
        Assert.Empty(log);

        engineRobot.HandleFirmwareVersion(Fw("[1, 2]"));                           // array root: throwLogicError out of the handler
        engineRobot.HandleFirmwareVersion(Fw("7"));
        Assert.Null(provider());
        Assert.False(engineRobot.PhysicalRobotRecorded);
        Assert.Single(log, l => l.Contains("MISSING: HandleFirmwareVersion"));        // once

        engineRobot.HandleFirmwareVersion(Fw("null"));                             // null root becomes an object: sim is null
        Assert.Equal(RobotAudioOutputSource.PlayOnRobot, provider());
        Assert.True(engineRobot.IsPhysicalRobot);

        engineRobot.HandleFirmwareVersion(Fw("{\"sim\": true}"));
        Assert.Equal(RobotAudioOutputSource.PlayOnDevice, provider());
        Assert.False(engineRobot.IsPhysicalRobot);

        engineRobot.HandleFirmwareVersion(Fw("{\"version\": 2381}"));            // no "sim": a null entry
        Assert.Equal(RobotAudioOutputSource.PlayOnRobot, provider());
        Assert.True(engineRobot.IsPhysicalRobot);
    }

    /// <summary>M6-003: no channels (the caller's loop is skipped, 0x00A725DC) or a block too small for the channels decode to nothing and do not throw, so the voice sources' StartStream/Render cannot be broken by them.</summary>
    [Fact]
    public void M6_003_UndecodableGeometryDecodesToNothingWithoutThrowing()
    {
        Assert.Empty(DecodeAdpcm(0, 36, new byte[72]));
        Assert.Empty(DecodeAdpcm(2, 40, new byte[80]));
    }
}
