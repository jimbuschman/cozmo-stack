using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-009 (C35.1, L5-22..L5-30 of re-analysis/research/20261004-B-M6b-4-live-bodies-5.md as verified): the binary32 port of the engine's RTPC curve evaluation 0xA14E28. Every expected value is the engine's
/// own output: re-analysis/tools/emu/emu_curve.py runs the real 0xA14E28 (and, for the live entry, 0xA11590 / 0xA17878 / 0xA17724 / 0xA17280) under Unicorn and writes WwiseCurveOracle.cs. Results are compared as
/// bits (a NaN result as NaN only) and the idx output exactly. The named vectors are the verified research vectors (section 3 of the report), also engine output.
/// </summary>
public class WwiseRtpcCurveTests
{
    private static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);
    private static float F(uint b) => BitConverter.UInt32BitsToSingle(b);

    private static IReadOnlyList<(float From, float To, uint Interp)> Points(uint[] flat)
    {
        var pts = new List<(float, float, uint)>();
        for (int i = 0; i < flat.Length; i += 3) pts.Add((F(flat[i]), F(flat[i + 1]), flat[i + 2]));
        return pts;
    }

    private static bool Same(uint expected, float actual) => float.IsNaN(F(expected)) ? float.IsNaN(actual) : expected == Bits(actual);

    [Fact]
    public void C35_1_ThePoolConstantsAreTheWordsOfTheLiteralPoolAt0xA15248()
    {
        // The oracle reads the 31 words 0xA15248..0xA152C0 from libcozmoEngine.so; the port's constants are the same bit patterns (the checklist: engine constants as bits).
        Assert.Equal(31, WwiseCurveOracle.Pool.Length);
        Assert.Equal(WwiseCurveOracle.Pool, WwiseRtpcCurveA14E28.PoolBits);
    }

    [Fact]
    public void C35_1_EveryOracleCaseMatchesTheEnginesResultBitsAndIdx()
    {
        // L5-22..L5-30: the interp matrix (every code 0..9, 10, 11, 0xFFFFFFFF x every scaling 0..5 and 7 x two y ranges x x at/below/between/above/NaN), the scaling stages on one-point curves for ~170 y values
        // (+-0, +-inf, NaN, +-1, +-37, +-740, the neighbours of 38.83, denormals, random patterns), random sorted/unsorted/duplicate curves of 1..6 points with hints, and the 64 shipped curves at >= 50 x each.
        Assert.True(WwiseCurveOracle.Cases.Length >= 1500);
        var bad = new List<string>();
        foreach (var (curve, scaling, hint, x, result, idx) in WwiseCurveOracle.Cases)
        {
            float r = WwiseRtpcCurveA14E28.Evaluate(Points(WwiseCurveOracle.Curves[curve]), scaling, F(x), hint, out uint i);
            if (!Same(result, r) || idx != i)
                bad.Add($"curve {curve} scaling {scaling} hint {hint} x 0x{x:X8}: engine 0x{result:X8} idx {idx}, C# 0x{Bits(r):X8} idx {i}");
        }
        Assert.True(bad.Count == 0, $"{bad.Count} of {WwiseCurveOracle.Cases.Length} differ:\n" + string.Join("\n", bad.Take(15)));
    }

    [Fact]
    public void C35_4_TheShippedCurvesAreEvaluatedAtFiftyOrMoreXEach()
    {
        // 64 RTPC entries in the six shipped banks (the verifier's HIRC census, C35.4): scalings 0 and 2 only, 2..6 points. The oracle walks the banks itself (emu_curve.py) and evaluates each curve at >= 50 x.
        Assert.Equal(64, WwiseCurveOracle.Shipped.Length);
        Assert.All(WwiseCurveOracle.Shipped, s => Assert.True(s.Scaling is 0 or 2));
        foreach (var s in WwiseCurveOracle.Shipped)
        {
            int n = WwiseCurveOracle.Cases.Count(c => c.Curve == s.Curve && c.Scaling == s.Scaling && c.Hint == 0);
            Assert.True(n >= 50, $"{s.Bank} rtpc 0x{s.Rtpc:X8}: only {n} cases");
        }
    }

    [Fact]
    public void C35_4_TheOraclesShippedCurvesAreTheBankCurvesTheStackParses()
    {
        // The oracle's own HIRC walk and the stack's hierarchy reader find the same RTPC entries (curve id, scaling, points as bits) in the shipped banks, so the shipped-curve cases are the real curves.
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        string? meta = null;
        while (d is not null && meta is null)
        {
            var candidate = Path.Combine(d.FullName, "re-analysis", "obb", "sound_meta");
            if (Directory.Exists(candidate)) meta = candidate;
            d = d.Parent;
        }
        if (meta is null) return;

        var parsed = new List<(string, uint, byte, string)>();
        foreach (var f in Directory.EnumerateFiles(meta, "*.bnk", SearchOption.AllDirectories))
        {
            var bank = WwiseBank.Parse(File.ReadAllBytes(f), Path.GetFileName(f));
            foreach (var o in bank.Objects.Values)
            {
                var node = WwiseHierarchy.TryRead(o, out _);
                if (node is null) continue;
                foreach (var r in node.Params.Rtpcs)
                    parsed.Add((Path.GetFileName(f), r.CurveId, r.Scaling, string.Join(",", r.Points.Select(p => $"{Bits(p.From):X8}:{Bits(p.To):X8}:{p.Interp}"))));
            }
        }
        // The stack's ReadEffect (object types 18 and 19) reads and discards the RTPC list of an effect object, so the two shipped entries on effects (Cozmo.bnk 55303899, Dev_Debug.bnk) are not in the parsed list.
        Assert.Equal(2, WwiseCurveOracle.Shipped.Count(s => s.ObjType is 18 or 19));
        var oracle = WwiseCurveOracle.Shipped
            .Where(s => s.ObjType is not (18 or 19))
            .Select(s => (s.Bank, s.CurveId, s.Scaling, string.Join(",", Points(WwiseCurveOracle.Curves[s.Curve]).Select(p => $"{Bits(p.From):X8}:{Bits(p.To):X8}:{p.Interp}"))))
            .OrderBy(x => x.ToString()).ToList();
        Assert.Equal(oracle, parsed.OrderBy(x => x.ToString()).ToList());
    }

    [Fact]
    public void C35_1_TheLiveEntryA11590GivesTheEnginesAccumulatedResult()
    {
        // L5-10 / L5-11 / L5-12: 0xA11590 -> 0xA17878 (sum from 0.0f) or 0xA17724 (product from 1.0f, [e+0x28] == 2) -> 0xA17280 (the stored root value is x) -> 0xA14E28. The engine's result for one curve
        // per subscription at 1965 x values: the shipped curves (sum and product) and 120 of the matrix / random curves. NaN results compare as NaN.
        Assert.NotEmpty(WwiseCurveOracle.Live);
        var store = new WwiseRtpcStore();                                   // the production default curve evaluation
        var subs = new Dictionary<(int, uint, byte), (uint Key, uint Param, uint Rtpc)>();
        var bad = new List<string>();
        foreach (var (curve, scaling, acc, x, result) in WwiseCurveOracle.Live)
        {
            Assert.True(scaling < 256);
            if (!subs.TryGetValue((curve, scaling, acc), out var s))
            {
                s = (0x4000, (uint)subs.Count, 5000u + (uint)subs.Count);
                subs[(curve, scaling, acc)] = s;
                store.Apply(new WwiseStmgParam(s.Rtpc, 0f, 0, 0f, 0f, false));
                store.AddSubscription(new WwiseRtpcSubscription
                {
                    Key1 = s.Key, Param = s.Param, Type = 2, Accumulate = acc,       // [e+0x24] = 2: a node subscription (C37.1); with no listener registered a set delivers nothing
                    Curves = new[] { new WwiseRtpc(s.Rtpc, 0, acc, s.Param, 0, (byte)scaling, Points(WwiseCurveOracle.Curves[curve])) },
                });
            }
            store.SetParameter(s.Rtpc, F(x), 0);
            float r = store.A11590(s.Key, s.Param, WwiseGainRtpcKey.Empty);
            if (!Same(result, r)) bad.Add($"curve {curve} scaling {scaling} acc {acc} x 0x{x:X8}: engine 0x{result:X8}, C# 0x{Bits(r):X8}");
        }
        Assert.True(bad.Count == 0, $"{bad.Count} of {WwiseCurveOracle.Live.Length} differ:\n" + string.Join("\n", bad.Take(15)));
    }

    [Fact]
    public void C35_1_TheLegacyEvaluateUsedByWwiseGainGivesTheEnginesValueNotTheIdealCurve()
    {
        // Batch 5d: WwiseRtpcStore.Evaluate (the legacy path WwiseGain.cs reaches with a store) takes the per-curve value from the exact 0xA14E28 port and accumulates in binary32 (0xA17878 / 0xA17724). Before, it used the
        // double-width WwiseRtpc.EvaluateScaled, e.g. event_volume at x = 0.5 gave -3.0103 dB (ideal) where the engine gives 0xC040B146 = -3.0108199. Expected: the engine's live results, as in the A11590 test.
        var store = new WwiseRtpcStore();
        var made = new Dictionary<(int, uint, byte), uint>();
        var bad = new List<string>();
        foreach (var (curve, scaling, acc, x, result) in WwiseCurveOracle.Live)
        {
            if (!made.TryGetValue((curve, scaling, acc), out uint rtpc))
            {
                rtpc = 7000u + (uint)made.Count;
                made[(curve, scaling, acc)] = rtpc;
                store.Apply(new WwiseStmgParam(rtpc, 0f, 0, 0f, 0f, false));
            }
            store.SetParameter(rtpc, F(x), 0);
            var binding = new WwiseRtpc(rtpc, 0, acc, 0, 0, (byte)scaling, Points(WwiseCurveOracle.Curves[curve]));
            float r = (float)store.Evaluate(acc, new[] { binding }, 0, 0, out bool reduced);
            Assert.False(reduced);
            if (!Same(result, r)) bad.Add($"curve {curve} scaling {scaling} acc {acc} x 0x{x:X8}: engine 0x{result:X8}, C# 0x{Bits(r):X8}");
        }
        Assert.True(bad.Count == 0, $"{bad.Count} of {WwiseCurveOracle.Live.Length} differ:\n" + string.Join("\n", bad.Take(15)));
    }

    // ------------------------------------------------------------------ named vectors (research live-bodies-5 section 3, engine output)

    private static uint Run(float[] xy, uint[] interps, uint scaling, float x, out uint idx, uint hint = 0)
    {
        var pts = new List<(float, float, uint)>();
        for (int i = 0; i < interps.Length; i++) pts.Add((xy[2 * i], xy[2 * i + 1], interps[i]));
        return Bits(WwiseRtpcCurveA14E28.Evaluate(pts, scaling, x, hint, out idx));
    }

    [Fact]
    public void L5_24_EventVolumeAtHalfIsMinus3_0108dBAndAtThreeQuartersMinus0_6964()
    {
        // L5-01 (event_volume 0xD2687048: (0,-1,interp 1)->(1,0,interp 4), scaling 2) with L5-24 shape 1 and L5-29: the engine's values 0xC040B146 and 0xBF324588, not the ideal -3.0103 / -0.6877 dB.
        Assert.Equal(0xC040B146u, Run(new[] { 0f, -1f, 1f, 0f }, new[] { 1u, 4u }, 2, 0.5f, out uint idx));
        Assert.Equal(0u, idx);
        Assert.Equal(0xBF324588u, Run(new[] { 0f, -1f, 1f, 0f }, new[] { 1u, 4u }, 2, 0.75f, out _));
    }

    [Fact]
    public void L5_27_Scaling3Of10ToTheMinus30IsTheSchraudolphValueNotMathPow()
    {
        // L5-27: (0,-60,4),(100,0,4) at x = 50 gives y = -30 and pow10core(-30) = 0x0DA22560 (9.993018535662365e-31).
        Assert.Equal(0x0DA22560u, Run(new[] { 0f, -60f, 100f, 0f }, new[] { 4u, 4u }, 3, 50f, out _));
    }

    [Fact]
    public void L5_27_Pow10CoreOfZeroIs0x3F7FC105NotOne()
    {
        // L5-27: (0,y,4) one point, y = 0 / -6 / -37 / -40 with scaling 3: 0x3F7FC105, 0x3585F38C, 0x0207D23C, 0 (y < -37.0 is +0.0). Scaling 4 at y = 0 / -20 / -60 / -740 / -800: 0x3F7FC105, 0x3DCD44E6, 0x3A82DD8E, 0x0207D23C, 0.
        uint[] one = { 4u };
        Assert.Equal(new[] { 0x3F7FC105u, 0x3585F38Cu, 0x0207D23Cu, 0u }, new[] { 0f, -6f, -37f, -40f }.Select(y => Run(new[] { 0f, y }, one, 3, 0.5f, out _)).ToArray());
        Assert.Equal(new[] { 0x3F7FC105u, 0x3DCD44E6u, 0x3A82DD8Eu, 0x0207D23Cu, 0u }, new[] { 0f, -20f, -60f, -740f, -800f }.Select(y => Run(new[] { 0f, y }, one, 4, 0.5f, out _)).ToArray());
    }

    [Fact]
    public void L5_27_C5_Pow10CoreAbove38_83WrapsTheExponentAndSetsTheSignBit()
    {
        // C5: y = 38.0 -> 9.98e37, 38.8 -> +inf, 39.0 -> -0.0, 40 -> -8.64e-38 (u >> 23 >= 256 sets the sign bit; no upper clamp).
        uint[] one = { 4u };
        Assert.Equal(0x80000000u, Run(new[] { 0f, 39f }, one, 3, 0.5f, out _));
        Assert.Equal(0x7F800000u, Run(new[] { 0f, 38.8f }, one, 3, 0.5f, out _));
        Assert.True(float.IsNegative(F(Run(new[] { 0f, 40f }, one, 3, 0.5f, out _))));
    }

    [Fact]
    public void L5_29_Scaling2IsTheSignedTwoTermLogWithTheBinary32Clamp()
    {
        // L5-29: (0,y,4) with scaling 2: y = 0.25 / 0.999 / 1.0 / 1.5 / 0.001 -> 0x401FFEAE / 0x4270001E / 0x443F2770 (+764.6162109375) / 0x443F2770 / 0x3CC59EF8; y = 0 gives -0.0; y = -1 gives the lo clamp 0xC43F2770.
        uint[] one = { 4u };
        Assert.Equal(new[] { 0x401FFEAEu, 0x4270001Eu, 0x443F2770u, 0x443F2770u, 0x3CC59EF8u },
            new[] { 0.25f, 0.999f, 1.0f, 1.5f, 0.001f }.Select(y => Run(new[] { 0f, y }, one, 2, 0.5f, out _)).ToArray());
        Assert.Equal(0x80000000u, Run(new[] { 0f, 0f }, one, 2, 0.5f, out _));
        Assert.Equal(0xC43F2770u, Run(new[] { 0f, -1f }, one, 2, 0.5f, out _));
    }

    [Fact]
    public void L5_25_AnInterpCodeOfTenOrMoreGivesYZeroThenTheScalingStage()
    {
        // L5-25: (0,5,10),(1,7,4) at x = 0.5: scaling 0 -> +0.0, 2 -> -0.0 (0x80000000), 3 -> 0x3F7FC105, 4 -> 0x3F7FC105 (not linear, no "reduced").
        float[] xy = { 0f, 5f, 1f, 7f };
        uint[] interps = { 10u, 4u };
        Assert.Equal(new[] { 0u, 0x80000000u, 0x3F7FC105u, 0x3F7FC105u }, new uint[] { 0, 2, 3, 4 }.Select(sc => Run(xy, interps, sc, 0.5f, out _)).ToArray());
    }

    [Fact]
    public void L5_22_XAtAnInteriorAbscissaIsThatPointsYExactlyAndNaNXIsTheLastY()
    {
        // L5-22: (0,0,interp 5),(1,1,interp 5),(2,0,interp 4) at x = 1.0 gives exactly 0x3F800000 with idx 1 (the shape 5 value at t = 1 would be 1.0006968); NaN x on (0,0),(1,5),(2,2) gives 2.0f with idx 2.
        float[] xy = { 0f, 0f, 1f, 1f, 2f, 0f };
        Assert.Equal(0x3F800000u, Run(xy, new[] { 5u, 5u, 4u }, 0, 1f, out uint idx));
        Assert.Equal(1u, idx);
        Assert.Equal(0x40000000u, Run(new[] { 0f, 0f, 1f, 5f, 2f, 2f }, new[] { 4u, 4u, 4u }, 0, float.NaN, out idx));
        Assert.Equal(2u, idx);
    }

    [Fact]
    public void L5_22_DuplicateAbscissaeTheFirstDuplicatesYAtTheAbscissaAndNoDivisionByZero()
    {
        // L5-22: (1,3),(1,9),(2,5) at x = 1.0 gives 3.0 (0x40400000: the first x <= x_i wins); at x = 1.5 the segment (1,9)->(2,5) gives 7.0 (0x40E00000).
        float[] xy = { 1f, 3f, 1f, 9f, 2f, 5f };
        uint[] interps = { 4u, 4u, 4u };
        Assert.Equal(0x40400000u, Run(xy, interps, 0, 1f, out _));
        Assert.Equal(0x40E00000u, Run(xy, interps, 0, 1.5f, out _));
    }

    [Fact]
    public void L5_22_TheHintStartsTheSearchAndABeyondTheEndHintGivesTheLastYWithIdxEqualToTheHint()
    {
        // L5-22 (verification): i starts at the hint; a hint at or past count - 1 returns y_(count-1) with idx = hint (unsigned compare, so 0xFFFFFFFF is past the end); a hint of 1 skips segment 0.
        float[] xy = { 0f, 10f, 1f, 20f, 2f, 30f };
        uint[] interps = { 4u, 4u, 4u };
        Assert.Equal(0x41F00000u, Run(xy, interps, 0, 0.5f, out uint idx, hint: 7));
        Assert.Equal(7u, idx);
        Assert.Equal(0x41F00000u, Run(xy, interps, 0, 0.5f, out idx, hint: 0xFFFFFFFFu));
        Assert.Equal(0xFFFFFFFFu, idx);
        Assert.Equal(0x41A00000u, Run(xy, interps, 0, 0.5f, out idx, hint: 1));      // x <= x_1 at i = 1 gives y_1 = 20.0f
        Assert.Equal(1u, idx);
    }

    [Fact]
    public void L5_06_ACountOfZeroNeverReachesTheFunctionAndIsAVisibleError()
    {
        // L5-06: a count-0 curve makes the loader fail (0x1F); 0xA14E28 would read pts[0] / pts[-1] past the buffer, so the port throws rather than returning a value.
        Assert.Throws<InvalidOperationException>(() => WwiseRtpcCurveA14E28.Evaluate(Array.Empty<(float, float, uint)>(), 0, 0f, 0, out _));
    }

    [Theory]
    [InlineData(0x00000000u, 0u)]
    [InlineData(0xBF800000u, 0u)]
    [InlineData(0x7FC00000u, 0u)]
    [InlineData(0x4F800000u, 0xFFFFFFFFu)]
    [InlineData(0x40A00000u, 5u)]
    [InlineData(0x3F7FFFFFu, 0u)]
    public void L5_27_VcvtU32F32TruncatesAndSaturatesWithNaNToZero(uint floatBits, uint expected)
    {
        // The ARM definition of vcvt.u32.f32 (round toward zero; negative to 0; >= 2^32 to 0xFFFFFFFF; NaN to 0), used by pow10core 0xA14EFC.
        Assert.Equal(expected, WwiseRtpcCurveA14E28.CvtU32(F(floatBits)));
    }
}
