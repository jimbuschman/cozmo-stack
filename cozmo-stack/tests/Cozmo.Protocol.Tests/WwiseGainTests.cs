using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-010 / gapC 1.1..1.11, 2.1..2.8, 3.2 and gapE 1.1: the Wwise gain composition. The parent/output-bus
/// links and the dB/cents sums, the per-voice randomizer, the fast-pow dB→linear conversion, the
/// game-defined aux send, the muted dry path and the bus-volume-after-FX placement.
///
/// Every expected value below is the row's own arithmetic worked out by hand (the fast pow's polynomial,
/// the LCG draw, the send product), not a value read back from <see cref="WwiseGain"/>. The addresses in
/// the comments are the rows' citations.
/// </summary>
public class WwiseGainTests
{
    // Oracles computed from the row formulas:
    //   gapC 1.11 dBToLin: y = 0.05·dB, bits = u32(1065353216 + 27866352·y),
    //   gain = float((bits>>23)<<23)·(0.6530434 + m(0.0208058 + 0.3251898m)), m = mantissa|0x3F800000.
    private const float DbToLin0 = 0.9990389943122864f;         // y = 0
    private const float DbToLinMinus20 = 0.10022906959056854f;  // 0.1 linear
    private const float DbToLinMinus6 = 0.4995194971561432f;    // −6.0206 dB
    private const float DbToLinMinus740 = 9.97856789174302e-38f;

    // The global LCG (M6-007 §3.1) seeded with 12345: s = s·0x5851F42D4C957F2D + 1, draw = (s>>32)>>1.
    private const ulong LcgSeed = 12345;
    private const uint LcgDraw1 = 67366457u;
    private const uint LcgDraw2 = 1092397125u;
    private const uint LcgDraw3 = 70888153u;

    private static void Close(float expected, float actual, float tolerance = 1e-6f)
    {
        Assert.True(MathF.Abs(expected - actual) <= tolerance,
            $"expected {expected:R}, got {actual:R} (tolerance {tolerance:R})");
    }

    private static Dictionary<byte, float> Props(params (byte Id, float Value)[] entries)
    {
        var d = new Dictionary<byte, float>();
        foreach (var (id, value) in entries) d[id] = value;
        return d;
    }

    // ------------------------------------------------------------------ GetAudioParameters sums

    /// <summary>
    /// M6-010 / gapC 1.3 (0x9F06C8..0x9F0890): Volume, Pitch, LPF and HPF are summed in dB/cents across the
    /// parent chain (+0x34), leaf first, and MakeUpGain into io+0x18. A leaf and its parent give
    /// Volume 3 + (−1) = 2 dB, Pitch 100 + 50 = 150 cents, LPF 15 + 5 = 20, HPF 20 + 8 = 28,
    /// MakeUpGain 1.5 + 0.25 = 1.75.
    /// </summary>
    [Fact]
    public void TheNodeChainSumsVolumePitchLpfAndHpfInDbAndCents()
    {
        var parent = new WwiseGainNode
        {
            Id = 2,
            Props = Props((WwiseGainProps.Volume, -1f), (WwiseGainProps.Pitch, 50f),
                          (WwiseGainProps.LowPass, 5f), (WwiseGainProps.HighPass, 8f),
                          (WwiseGainProps.MakeUpGain, 0.25f)),
        };
        var leaf = new WwiseGainNode
        {
            Id = 1,
            Parent = parent,
            Props = Props((WwiseGainProps.Volume, 3f), (WwiseGainProps.Pitch, 100f),
                          (WwiseGainProps.LowPass, 15f), (WwiseGainProps.HighPass, 20f),
                          (WwiseGainProps.MakeUpGain, 1.5f)),
        };

        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(leaf, WwiseParamSelect.NodeParams, ref io);

        Close(2f, io.VolumeDb);
        Close(150f, io.PitchCents);
        Close(20f, io.LowPass);
        Close(28f, io.HighPass);
        Close(1.75f, io.MakeUpGain);
    }

    /// <summary>
    /// M6-010 / gapC 1.3: the paramSelect bits gate which props are summed. With only Volume selected, the
    /// Pitch/LPF/HPF props contribute nothing; MakeUpGain has no named bit and is still added.
    /// </summary>
    [Fact]
    public void TheParamSelectBitsGateThePerNodeSums()
    {
        var node = new WwiseGainNode
        {
            Id = 1,
            Props = Props((WwiseGainProps.Volume, 3f), (WwiseGainProps.Pitch, 100f),
                          (WwiseGainProps.LowPass, 15f), (WwiseGainProps.HighPass, 20f),
                          (WwiseGainProps.MakeUpGain, 1.5f)),
        };

        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(node, WwiseParamSelect.Volume, ref io);

        Close(3f, io.VolumeDb);
        Close(0f, io.PitchCents);
        Close(0f, io.LowPass);
        Close(0f, io.HighPass);
        Close(1.5f, io.MakeUpGain);   // gapC 1.3: no named bit, added whenever present
    }

    /// <summary>
    /// M6-010 / gapC 1.4: the node+0x18 (gated by +0x46 bit0), node+0x24 and node+0x48 bundles add props
    /// 0/2/3/4 into the same slots; the game-object bundle is keyed by the game object in the RTPC key. The
    /// base (node+0x3C) and gated (node+0x18) readers never search prop 0xB (0x9F06C8/0x9F05F8/0x9F050C/
    /// 0x9F043C and 0x9F9CDC), so a MuteRatio they carry is not inserted.
    /// </summary>
    [Fact]
    public void TheAdditiveBundlesAddIntoTheSameSlotsAndBaseGatedDoNotInsertMuteRatio()
    {
        var node = new WwiseGainNode
        {
            Id = 1,
            Props = Props((WwiseGainProps.Volume, 1f), (WwiseGainProps.MuteRatio, 0.5f)),
            GatedBundleEnabled = true,
            GatedBundle = Props((WwiseGainProps.Volume, 2f), (WwiseGainProps.MuteRatio, 0.5f)),
            GlobalBundle = Props((WwiseGainProps.Volume, 3f)),
            ObjectBundles = new Dictionary<uint, IReadOnlyDictionary<byte, float>>
            {
                [7] = Props((WwiseGainProps.Volume, 4f)),
                [8] = Props((WwiseGainProps.Volume, 100f)),
            },
        };

        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(node, WwiseParamSelect.Volume, ref io,
            key: new WwiseGainRtpcKey(7, 0));

        Close(1f + 2f + 3f + 4f, io.VolumeDb);   // the game-object-8 bundle is not selected
        Assert.Empty(io.MutedMap);               // the base/gated prop 0xB is never read
        Close(1f, io.MutedProduct());
    }

    /// <summary>
    /// M6-010 / gapC 1.4 (0x9EF494 global bit0 = 1, 0x9F02E4 object bit0 = 0): one node carrying MuteRatio
    /// in both its global and object bundles is two entries keyed (node, 1) and (node, 0), so they multiply:
    /// 0.5 × 0.5 = 0.25.
    /// </summary>
    [Fact]
    public void AGlobalAndObjectMuteRatioOnOneNodeMultiply()
    {
        var node = new WwiseGainNode
        {
            Id = 1,
            GlobalBundle = Props((WwiseGainProps.MuteRatio, 0.5f)),
            ObjectBundles = new Dictionary<uint, IReadOnlyDictionary<byte, float>>
            {
                [7] = Props((WwiseGainProps.MuteRatio, 0.5f)),
            },
        };

        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(node, WwiseParamSelect.NodeParams, ref io,
            key: new WwiseGainRtpcKey(7, 0));

        Assert.Equal(2, io.MutedMap.Count);
        Assert.Equal(0.5f, io.MutedMap[new WwiseMutedKey(1, WwiseMutedKey.GlobalFlag)]);
        Assert.Equal(0.5f, io.MutedMap[new WwiseMutedKey(1, WwiseMutedKey.ObjectFlag)]);
        Close(0.25f, io.MutedProduct());
    }

    /// <summary>
    /// M6-010 / gapC 1.4, 1.11 (0x9FFDB0..0x9FFDC0): the muted map is keyed by the node, so two different
    /// nodes each carrying MuteRatio 0.5 give the product 0.25, not the last one.
    /// </summary>
    [Fact]
    public void DifferentNodesMuteRatiosMultiply()
    {
        var parent = new WwiseGainNode
        {
            Id = 2,
            GlobalBundle = Props((WwiseGainProps.MuteRatio, 0.5f)),
        };
        var leaf = new WwiseGainNode
        {
            Id = 1,
            Parent = parent,
            GlobalBundle = Props((WwiseGainProps.MuteRatio, 0.5f)),
        };

        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(leaf, WwiseParamSelect.NodeParams, ref io);

        Assert.Equal(2, io.MutedMap.Count);
        Assert.Equal(0.5f, io.MutedMap[new WwiseMutedKey(1, WwiseMutedKey.GlobalFlag)]);
        Assert.Equal(0.5f, io.MutedMap[new WwiseMutedKey(2, WwiseMutedKey.GlobalFlag)]);
        Close(0.25f, io.MutedProduct());
    }

    /// <summary>
    /// M6-010 / gapC 1.4 (0x9EF2D8..0x9EF454, 0x9F011C..0x9F0228): the global and game-object bundles add
    /// props 0/2/3/4 with no select test and never prop 6. With only Volume selected, the global Pitch and
    /// the object LowPass still land; the global/object MakeUpGain values do not. The object bundle also
    /// inserts its MuteRatio (0x9F02C0..0x9F0378).
    /// </summary>
    [Fact]
    public void TheGlobalAndObjectBundlesAreUngatedAndCarryNoMakeUpGain()
    {
        var node = new WwiseGainNode
        {
            Id = 1,
            Props = Props((WwiseGainProps.Volume, 1f), (WwiseGainProps.MakeUpGain, 10f)),
            GlobalBundle = Props((WwiseGainProps.Volume, 2f), (WwiseGainProps.Pitch, 7f),
                                 (WwiseGainProps.MakeUpGain, 99f)),
            ObjectBundles = new Dictionary<uint, IReadOnlyDictionary<byte, float>>
            {
                [7] = Props((WwiseGainProps.Volume, 3f), (WwiseGainProps.LowPass, 4f),
                            (WwiseGainProps.MakeUpGain, 99f), (WwiseGainProps.MuteRatio, 0.4f)),
            },
        };

        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(node, WwiseParamSelect.Volume, ref io,
            key: new WwiseGainRtpcKey(7, 0));

        Close(6f, io.VolumeDb);      // 1 base + 2 global + 3 object
        Close(7f, io.PitchCents);    // the global Pitch lands despite the Volume-only select
        Close(4f, io.LowPass);       // the object LowPass lands despite the Volume-only select
        Close(10f, io.MakeUpGain);   // base only; the global/object prop 6 is never searched

        Assert.Single(io.MutedMap);
        Assert.Equal(0.4f, io.MutedMap[new WwiseMutedKey(1, 0)]);   // the object insert
        Close(0.4f, io.MutedProduct());
    }

    /// <summary>M6-010 / gapC 1.4: the node+0x18 bundle is read only while node+0x46 bit0 is set.</summary>
    [Fact]
    public void TheGatedBundleIsReadOnlyWhenItsBitIsSet()
    {
        var node = new WwiseGainNode
        {
            Id = 1,
            Props = Props((WwiseGainProps.Volume, 1f)),
            GatedBundle = Props((WwiseGainProps.Volume, 2f)),
        };

        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(node, WwiseParamSelect.Volume, ref io);
        Close(1f, io.VolumeDb);

        var gated = new WwiseGainNode
        {
            Id = 1,
            Props = Props((WwiseGainProps.Volume, 1f)),
            GatedBundleEnabled = true,
            GatedBundle = Props((WwiseGainProps.Volume, 2f)),
        };
        var io2 = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(gated, WwiseParamSelect.Volume, ref io2);
        Close(3f, io2.VolumeDb);
    }

    // ------------------------------------------------------------------ links and the bus chain

    /// <summary>
    /// M6-010 / gapC 1.2 (0x9EFDC8..0x9F0114): the lowest node that overrides the bus supplies the
    /// output-bus values (props 0x18/0x1A/0x19 to io+0x28/0x2C/0x30), and the bus chain follows. A parent's
    /// output-bus props are not read because that recursion runs with bDoBusCheck = 0.
    /// </summary>
    [Fact]
    public void TheLowestOverridingNodeSuppliesTheOutputBusValues()
    {
        var bus = new WwiseGainBus { Id = 900, Props = Props((WwiseGainProps.Volume, -2f), (WwiseGainProps.BusVolume, -5f)) };
        var parent = new WwiseGainNode
        {
            Id = 2,
            Props = Props((WwiseGainProps.Volume, 1f), (WwiseGainProps.OutputBusVolume, -99f)),
            OutputBus = bus,
        };
        var leaf = new WwiseGainNode
        {
            Id = 1,
            Parent = parent,
            OutputBus = bus,
            Props = Props((WwiseGainProps.OutputBusVolume, -3f), (WwiseGainProps.OutputBusLowPass, 10f),
                          (WwiseGainProps.OutputBusHighPass, 12f)),
        };

        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(leaf, WwiseParamSelect.NodeParams, ref io);

        Close(-3f, io.OutputBusVolumeDb);
        Close(10f, io.OutputBusLowPass);
        Close(12f, io.OutputBusHighPass);
        // The bus chain ran: its prop 0 (Voice Volume) is summed, its prop 5 (Bus Volume) is not.
        Close(1f + (-2f), io.VolumeDb);
    }

    /// <summary>
    /// M6-010 / gapC 1.2: when a leaf has no output bus the bDoBusCheck flag passes through, so the nearest
    /// ancestor with one supplies the output-bus values.
    /// </summary>
    [Fact]
    public void ABusOverrideOnAnAncestorIsUsedWhenTheLeafHasNone()
    {
        var bus = new WwiseGainBus { Id = 900 };
        var parent = new WwiseGainNode
        {
            Id = 2,
            OutputBus = bus,
            Props = Props((WwiseGainProps.OutputBusVolume, -3f)),
        };
        var leaf = new WwiseGainNode { Id = 1, Parent = parent };

        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(leaf, WwiseParamSelect.NodeParams, ref io);

        Close(-3f, io.OutputBusVolumeDb);
    }

    /// <summary>
    /// M6-010 / gapC 1.9: the bus method clears paramSelect bit 0x10 (Bus Volume), so a bus's prop 5 is not
    /// in the GetAudioParameters sum; its props 0/2/3/4 and RTPCs are, and it recurses into the parent bus.
    /// The native bus search never reads prop 6 or prop 0xB, so neither MakeUpGain nor MuteRatio is taken.
    /// </summary>
    [Fact]
    public void TheBusClearsBusVolumeAndSumsItsPropsUpTheParentChain()
    {
        Assert.False(WwiseGain.BusParamSelect(WwiseParamSelect.All).HasFlag(WwiseParamSelect.BusVolume));

        var parentBus = new WwiseGainBus { Id = 901, Props = Props((WwiseGainProps.Volume, -1f)) };
        var bus = new WwiseGainBus
        {
            Id = 900,
            ParentBus = parentBus,
            Props = Props((WwiseGainProps.Volume, -2f), (WwiseGainProps.BusVolume, -5f),
                          (WwiseGainProps.LowPass, 3f), (WwiseGainProps.MakeUpGain, 5f),
                          (WwiseGainProps.MuteRatio, 0.5f)),
        };

        var io = WwiseAudioParameters.Create();
        WwiseGain.GetBusAudioParameters(bus, WwiseParamSelect.All, ref io);

        Close(-3f, io.VolumeDb);        // −2 + −1; the −5 Bus Volume is excluded
        Close(3f, io.LowPass);
        Close(0f, io.MakeUpGain);       // the bus search never reads prop 6
        Close(1f, io.MutedProduct());   // ... nor prop 0xB
    }

    /// <summary>
    /// M6-010 / gapC 1.9: the bus volume also gets <c>max(+0x6C, Σ ducking)</c>, with +0x6C initialized to
    /// −96.3. With no ducking the max is 0 and nothing is added; −3 dB of ducking adds −3; −200 dB is
    /// floored at the −96.3 initializer.
    /// </summary>
    [Fact]
    public void TheBusAddsMaxOfDuckAndTheInitFloor()
    {
        var quiet = new WwiseGainBus { Id = 900, Props = Props((WwiseGainProps.Volume, -2f)) };
        var noDuck = WwiseAudioParameters.Create();
        WwiseGain.GetBusAudioParameters(quiet, WwiseParamSelect.All, ref noDuck);
        Close(-2f, noDuck.VolumeDb);   // max(−96.3, 0) = 0

        var ducked = new WwiseGainBus { Id = 900, Props = Props((WwiseGainProps.Volume, -2f)), DuckingDb = -3f };
        var someDuck = WwiseAudioParameters.Create();
        WwiseGain.GetBusAudioParameters(ducked, WwiseParamSelect.All, ref someDuck);
        Close(-5f, someDuck.VolumeDb);   // max(−96.3, −3) = −3

        var deep = new WwiseGainBus { Id = 900, Props = Props((WwiseGainProps.Volume, -2f)), DuckingDb = -200f };
        var deepIo = WwiseAudioParameters.Create();
        WwiseGain.GetBusAudioParameters(deep, WwiseParamSelect.All, ref deepIo);
        Close(-2f + WwiseGain.BusVolumeInitDb, deepIo.VolumeDb);   // max(−96.3, −200) = −96.3
    }

    // ------------------------------------------------------------------ randomizer

    /// <summary>
    /// M6-010 / gapC 1.5, 1.6: the ranged value is
    /// <c>min + ((LCG_hi&gt;&gt;1)/2147483647.0)·(max−min)</c>, and it is drawn <b>once per voice</b>:
    /// <c>pbi+0x1BC</c> bit0 gates the ranges pointer. Seed 12345's first draw is 67366457, so a
    /// (−150, 150) pitch range gives −150 + (67366457/2147483647)·300 = −140.58901977539062. The second
    /// pass draws nothing, so the next LCG output is the second (1092397125), not the third.
    /// </summary>
    [Fact]
    public void TheRandomizerIsDrawnOncePerVoice()
    {
        var rng = new WwiseRng(LcgSeed);
        var randomizer = new WwiseVoiceRandomizer();
        var node = new WwiseGainNode
        {
            Id = 1,
            RangedProps = new Dictionary<byte, (float Min, float Max)>
            {
                [WwiseGainProps.Pitch] = (-150f, 150f),
            },
        };

        var ranges = randomizer.BeginPass();
        Assert.NotNull(ranges);
        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(node, WwiseParamSelect.None, ref io, ranges, rng: rng);
        randomizer.EndPass();

        Close(-140.58901977539062f, randomizer.Ranges.Pitch);
        Assert.Equal(LcgDraw2, rng.Next());   // exactly one draw consumed

        var secondPass = randomizer.BeginPass();
        Assert.Null(secondPass);
        var io2 = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(node, WwiseParamSelect.None, ref io2, secondPass, rng: rng);
        Close(-140.58901977539062f, randomizer.Ranges.Pitch);
        Assert.Equal(LcgDraw3, rng.Next());   // the second pass consumed no draw
    }

    /// <summary>
    /// M6-010 / gapC 1.5, gapE 3.2 (0x9EF5D8..0x9EF968): the native draws Volume (+0), Pitch (+8), LPF
    /// (+0xC), HPF (+0x10) and MakeUpGain (+4) <b>last</b> (<c>0x9EF8C8 cmp ip,#6</c>,
    /// <c>0x9EF968 vstr s15,[r3,#4]</c>). With a Pitch and a MakeUpGain range, seed 12345's first draw
    /// (67366457) lands on Pitch and its second (1092397125) on MakeUpGain:
    /// Pitch = −150 + (67366457/2147483647)·300 = −140.58901977539062 and
    /// MakeUpGain = 0 + (1092397125/2147483647)·1 = 0.5086870789527893.
    /// </summary>
    [Fact]
    public void TheRandomizerDrawsPitchBeforeMakeUpGain()
    {
        var rng = new WwiseRng(LcgSeed);
        var ranges = new WwiseGainRanges();
        var node = new WwiseGainNode
        {
            Id = 1,
            RangedProps = new Dictionary<byte, (float Min, float Max)>
            {
                [WwiseGainProps.Pitch] = (-150f, 150f),
                [WwiseGainProps.MakeUpGain] = (0f, 1f),
            },
        };

        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(node, WwiseParamSelect.None, ref io, ranges, rng: rng);

        Close(-140.58901977539062f, ranges.Pitch);       // the first draw
        Close(0.5086870789527893f, ranges.MakeUpGain);   // the second draw, not the first
        Assert.Equal(LcgDraw3, rng.Next());
    }

    /// <summary>
    /// M6-010 / gapC 1.5: a zero range adds its min with no LCG draw (gapE 3.2). The draw for the pitch
    /// range after it is therefore still the first LCG output.
    /// </summary>
    [Fact]
    public void AZeroRangeAddsMinWithNoDraw()
    {
        var rng = new WwiseRng(LcgSeed);
        var ranges = new WwiseGainRanges();
        var node = new WwiseGainNode
        {
            Id = 1,
            RangedProps = new Dictionary<byte, (float Min, float Max)>
            {
                [WwiseGainProps.Volume] = (2f, 2f),      // no draw
                [WwiseGainProps.Pitch] = (-150f, 150f),  // the first draw
            },
        };

        // Exercise the private-ish accumulation through GetAudioParameters (the ranges object is the same).
        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(node, WwiseParamSelect.None, ref io, ranges, rng: rng);

        Close(2f, ranges.Volume);
        Close(-150f + (LcgDraw1 / 2147483647f) * 300f, ranges.Pitch, 1e-4f);
        Assert.Equal(LcgDraw2, rng.Next());
    }

    // ------------------------------------------------------------------ dBToLinear

    /// <summary>
    /// M6-010 / gapC 1.11: the fast pow. The row's formula gives 0.99903899 at 0 dB (the polynomial's
    /// offset, not exactly 1), 0.10022907 at −20 dB, and 0 at −741 dB; −740 dB is the branch boundary
    /// (<c>y = −37</c>) and is still computed, so it is tiny but nonzero.
    /// </summary>
    [Fact]
    public void TheFastPowDbToLinearMatchesTheRowFormula()
    {
        Close(DbToLin0, WwiseGain.DbToLinear(0f));
        Close(DbToLinMinus20, WwiseGain.DbToLinear(-20f));
        Close(DbToLinMinus6, WwiseGain.DbToLinear(-6.0206f));

        // The branch: y < −37 (dB < −740) gives exactly 0.
        Assert.Equal(0f, WwiseGain.DbToLinear(-741f));
        float atBoundary = WwiseGain.DbToLinear(-740f);
        Assert.True(atBoundary > 0f && atBoundary < 1e-30f);
        Close(DbToLinMinus740, atBoundary, 1e-45f);

        // Sanity: the fast pow tracks the true 10^(dB/20) for the range it is used in.
        Close(0.1f, WwiseGain.DbToLinear(-20f), 1e-3f);
    }

    /// <summary>
    /// M6-010 / gapC 1.11: pbi+0x40 = max(0, Π muted ratios · pbi+0x168 · pbi+0x16C) and
    /// voice+0x1C = dBToLin(pbi+0x3C) · pbi+0x40. The muted map is keyed by the node, so a repeated node
    /// key overwrites (0x9EF484..0x9EF528) and the product is taken across the distinct nodes.
    /// </summary>
    [Fact]
    public void TheMuteFadeProductIsAppliedOnTopOfTheVoiceGain()
    {
        Close(0.5f, WwiseGain.MuteFade(0.5f, 1f, 1f));
        Close(0.25f, WwiseGain.MuteFade(1f, 0.5f, 0.5f));
        Close(0f, WwiseGain.MuteFade(-1f, 1f, 1f));

        var io = WwiseAudioParameters.Create();
        io.VolumeDb = -20f;
        io.MutedMap[new WwiseMutedKey(1, 0)] = 0.5f;
        var effective = WwiseGain.Effective(io, new WwiseGainRanges(), 1f, 1f);

        Close(-20f, effective.VolumeDb);
        Close(0.5f, effective.MuteFade);
        Close(DbToLinMinus20 * 0.5f, effective.VoiceGain);

        var muted = WwiseAudioParameters.Create();
        muted.VolumeDb = -20f;
        muted.MutedMap[new WwiseMutedKey(1, 0)] = -1f;
        Close(0f, WwiseGain.Effective(muted, new WwiseGainRanges(), 1f, 1f).VoiceGain);

        // The map overwrites per node key, then multiplies across distinct nodes.
        var keyed = WwiseAudioParameters.Create();
        keyed.MutedMap[new WwiseMutedKey(1, 0)] = 0.5f;
        keyed.MutedMap[new WwiseMutedKey(1, 0)] = 0.25f;   // same node: the later value wins
        Close(0.25f, keyed.MutedProduct());
        keyed.MutedMap[new WwiseMutedKey(2, 0)] = 0.5f;    // a second node multiplies in
        Close(0.125f, keyed.MutedProduct());
    }

    /// <summary>
    /// M6-010 / gapC 1.6: the effective volume/pitch are the sums plus the stored randomizer draw, and the
    /// LPF/HPF keep the randomizer terms separate for M6-011's filter composition.
    /// </summary>
    [Fact]
    public void TheEffectiveValuesAddTheStoredRandomizerDraw()
    {
        var io = WwiseAudioParameters.Create();
        io.VolumeDb = -20f;
        io.PitchCents = 100f;
        io.LowPass = 15f;
        io.HighPass = 4f;
        io.MakeUpGain = 1f;

        var ranges = new WwiseGainRanges { Volume = 3f, Pitch = -50f, LowPass = 2f, HighPass = -1f, MakeUpGain = 0.5f };
        var effective = WwiseGain.Effective(io, ranges, 1f, 1f);

        Close(-17f, effective.VolumeDb);
        Close(50f, effective.PitchCents);
        Close(17f, effective.LowPass);
        Close(3f, effective.HighPass);
        Close(1.5f, effective.MakeUpGain);
        Close(WwiseGain.DbToLinear(-17f), effective.VoiceGain);
    }

    // ------------------------------------------------------------------ aux send

    /// <summary>
    /// M6-010 / gapC 2.3, 2.6: the game-defined send gain is
    /// <c>dBToLin(GameAuxSendVolume) · the game object's send value</c>. 0 dB is the fast pow's 0.99903899,
    /// so a control value of 1.0 gives that and 0.5 gives half of it.
    /// </summary>
    [Fact]
    public void TheGameAuxSendIsTheSendVolumeTimesTheGameObjectValue()
    {
        var io = WwiseAudioParameters.Create();
        io.UseGameAux = true;
        io.GameAuxDecided = true;
        io.GameAuxSendVolumeDb = 0f;

        var sends = new[]
        {
            new WwiseGameAuxSend(100, 1.0f),
            new WwiseGameAuxSend(101, 0.5f),
        };
        var built = WwiseAuxSendBuilder.BuildGameSends(io, sends, 0f);

        Assert.Equal(2, built.Count);
        Close(DbToLin0, built[0].Gain);
        Close(DbToLin0 * 0.5f, built[1].Gain);
        Assert.Equal(1, built[0].Type);
        Assert.Equal(100u, built[0].BusId);

        // A −6.0206 dB send volume is its own fast-pow factor.
        io.GameAuxSendVolumeDb = -6.0206f;
        var lower = WwiseAuxSendBuilder.BuildGameSends(io, sends, 0f);
        Close(DbToLinMinus6, lower[0].Gain);
    }

    /// <summary>
    /// M6-010 / gapC 2.3 (0x9BD368): the send gain does not include OutputBusVolume or the game object's
    /// output-bus volume. Changing the output-bus values leaves every send gain identical.
    /// </summary>
    [Fact]
    public void TheGameAuxSendExcludesOutputBusVolume()
    {
        var io = WwiseAudioParameters.Create();
        io.UseGameAux = true;
        io.GameAuxDecided = true;
        io.GameAuxSendVolumeDb = 0f;
        io.OutputBusVolumeDb = -20f;
        io.OutputBusLowPass = 15f;
        io.OutputBusHighPass = 80f;

        var sends = new[] { new WwiseGameAuxSend(100, 1.0f) };
        var built = WwiseAuxSendBuilder.BuildGameSends(io, sends, 0f);

        Close(DbToLin0, built[0].Gain);   // unchanged by the output-bus values
        Assert.Single(built);
    }

    /// <summary>M6-010 / gapC 2.3: use-game-aux unset builds no send, and an entry at or below the emit threshold is dropped.</summary>
    [Fact]
    public void TheGameSendRespectsUseGameAuxAndTheEmitThreshold()
    {
        var sends = new[] { new WwiseGameAuxSend(100, 1.0f) };

        var off = WwiseAudioParameters.Create();
        off.GameAuxDecided = true;
        off.GameAuxSendVolumeDb = 0f;
        Assert.Empty(WwiseAuxSendBuilder.BuildGameSends(off, sends, 0f));

        var on = WwiseAudioParameters.Create();
        on.UseGameAux = true;
        on.GameAuxDecided = true;
        on.GameAuxSendVolumeDb = 0f;
        // The emit threshold's value is not in the rows; passing it above the gain drops the send.
        Assert.Empty(WwiseAuxSendBuilder.BuildGameSends(on, sends, DbToLin0 + 0.01f));
    }

    /// <summary>
    /// M6-010 / gapC 1.8: user sends use the slot's dB volume directly through dBToLin (no game-object
    /// value). No shipped node has aux bit3, so this is inert in the shipped data.
    /// </summary>
    [Fact]
    public void TheUserAuxSendIsTheSlotVolumeThroughDbToLin()
    {
        var io = WwiseAudioParameters.Create();
        io.UserAuxDecided = true;
        io.UserAuxVolumesDb[0] = 0f;
        io.UserAuxVolumesDb[1] = -20f;
        io.UserAuxIds[0] = 77;
        io.UserAuxIds[1] = 78;

        var built = WwiseAuxSendBuilder.BuildUserSends(io, -80f);
        Assert.Equal(2, built.Count);
        Close(DbToLin0, built[0].Gain);
        Close(DbToLinMinus20, built[1].Gain);
        Assert.Equal(77u, built[0].Id);
    }

    // ------------------------------------------------------------------ dry path and bus placement

    /// <summary>
    /// M6-010 / gapC 2.2, 2.4: the robot game objects set SetGameObjectOutputBusVolume 0.0, so the dry entry
    /// gain is <c>0.0 × dBToLin(OutputBusVolume)</c> = 0 whatever the output-bus volume is.
    /// </summary>
    [Fact]
    public void TheDryPathIsMutedForGameObjectSeven()
    {
        Assert.Equal(0f, WwiseGain.DryPathGain(0f, -6.0206f));
        Assert.Equal(0f, WwiseGain.DryPathGain(0f, -20f));

        // A caller that set the control value would hear the output-bus volume through this path.
        Close(DbToLinMinus20, WwiseGain.DryPathGain(1f, -20f));
        Close(DbToLinMinus20 * 0.5f, WwiseGain.DryPathGain(0.5f, -20f));
    }

    /// <summary>
    /// M6-010 / gapC 2.7: a collapsed bus folds its Bus Volume into the dry OutputBusVolume, additively in
    /// dB. It stays on the dry path, so it never reaches the aux send (the send builder takes no
    /// OutputBusVolume).
    /// </summary>
    [Fact]
    public void TheCollapsedBusVolumeFoldsIntoTheDryPathOnly()
    {
        Assert.Equal(-8f, WwiseGain.FoldCollapsedBusVolume(-5f, -3f));

        var io = WwiseAudioParameters.Create();
        io.UseGameAux = true;
        io.GameAuxDecided = true;
        io.GameAuxSendVolumeDb = 0f;
        io.OutputBusVolumeDb = WwiseGain.FoldCollapsedBusVolume(-5f, -3f);   // the fold touched the dry path
        var sends = new[] { new WwiseGameAuxSend(100, 1.0f) };
        Close(DbToLin0, WwiseAuxSendBuilder.BuildGameSends(io, sends, 0f)[0].Gain);
    }

    /// <summary>
    /// M6-010 / gapC 3.2 (0xA4FEF8 → 0xA4D994): GetResultingBuffer runs the FX loop, then applies the bus
    /// gain dBToLin(Bus Volume) to that output. With a squaring FX at 3 and −20 dB bus volume the result is
    /// 9 × 0.10022907 = 0.90206163; applying the volume before the FX would give (3 × 0.10022907)² instead,
    /// so the placement is observable.
    /// </summary>
    [Fact]
    public void TheBusVolumeIsAppliedAfterTheFx()
    {
        float AfterFx(float sample) => WwiseGain.ApplyBusVolumeAfterFx(x => x * x, sample, -20f);
        Close(9f * DbToLinMinus20, AfterFx(3f));

        float before = 3f * DbToLinMinus20;
        Assert.True(MathF.Abs(AfterFx(3f) - before * before) > 0.1f);

        Close(9f * DbToLin0, WwiseGain.ApplyBusVolumeAfterFx(x => x * x, 3f, 0f));
    }

    // ------------------------------------------------------------------ Sound override and RTPC pull

    /// <summary>
    /// M6-010 / gapC 1.10 (0xA1DAB8): the Sound override adds <c>100·(note − root note)</c> only when the
    /// MIDI key byte is not 0xFF. The root note's source (0x9FAE18) is RECOVERABLE_GAP, so it is a caller
    /// input.
    /// </summary>
    [Fact]
    public void TheSoundOverrideAppliesOnlyWhenTheKeyByteIsNotFf()
    {
        Assert.Equal(0f, WwiseGain.SoundOverridePitchCents(WwiseGain.NoMidiKey, 60f, 60f));
        Assert.Equal(200f, WwiseGain.SoundOverridePitchCents(60, 62f, 60f));
        Assert.Equal(-100f, WwiseGain.SoundOverridePitchCents(60, 59f, 60f));

        var io = WwiseAudioParameters.Create();
        io.PitchCents = 50f;
        WwiseGain.ApplySoundOverride(ref io, 60, 62f, 60f);
        Close(250f, io.PitchCents);
        WwiseGain.ApplySoundOverride(ref io, WwiseGain.NoMidiKey, 62f, 60f);   // 0xFF leaves it alone
        Close(250f, io.PitchCents);
    }

    /// <summary>
    /// M6-010 / gapC 1.5: the ranges are drawn from the global LCG, so a non-null ranges object with no LCG
    /// is a caller error and is refused rather than silently skipping the draw.
    /// </summary>
    [Fact]
    public void TheRandomizerRangesRequireTheLcg()
    {
        var node = new WwiseGainNode { Id = 1 };
        void Call()
        {
            var io = WwiseAudioParameters.Create();
            WwiseGain.GetAudioParameters(node, WwiseParamSelect.None, ref io, new WwiseGainRanges());
        }
        Assert.Throws<ArgumentNullException>(Call);
    }

    /// <summary>
    /// M6-010 / gapC 1.3 (<c>0xA11590</c>, M6-009): each prop adds its RTPC from the value store under the
    /// voice's key. A volume binding whose curve rises 0→5 over the store's default 1.0 adds 5, so the prop's
    /// 2 dB becomes 7 dB.
    /// </summary>
    [Fact]
    public void TheNodePropsAddTheirRtpcThroughTheM6Store()
    {
        var store = new WwiseRtpcStore(new[] { new WwiseStmgParam(0x1111, 1.0f, 0, 0, 0, false) });
        var rtpc = new WwiseRtpc(0x1111, WwiseRtpc.GameParameterSource, 1, WwiseGainProps.Volume, 0, 0,
            new[] { (0f, 0f, 4u), (1f, 5f, 4u) });
        var node = new WwiseGainNode
        {
            Id = 1,
            Props = Props((WwiseGainProps.Volume, 2f)),
            Rtpcs = new[] { rtpc },
        };

        var io = WwiseAudioParameters.Create();
        WwiseGain.GetAudioParameters(node, WwiseParamSelect.Volume, ref io, store: store,
            key: new WwiseGainRtpcKey(7, 0));

        Close(7f, io.VolumeDb);
    }
}
