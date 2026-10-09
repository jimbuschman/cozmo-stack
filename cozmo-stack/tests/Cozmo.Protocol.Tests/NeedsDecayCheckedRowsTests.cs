using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>Oracles from manager-checked D1–D18, not the implementation's outputs.</summary>
public sealed class NeedsDecayCheckedRowsTests
{
    private static float F(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));
    private static uint Bits(double value) => unchecked((uint)BitConverter.SingleToInt32Bits((float)value));
    private static DecayConfig Rates(params (double, double)[] bands)
    {
        var table = new Dictionary<NeedId, IReadOnlyList<(double, double)>> { [NeedId.Play] = bands };
        return new(table, table);
    }

    [Theory]
    [InlineData(0x3F800000u, 0x43340000u, 0x3F000000u)] // 1 -> .5 over three minutes
    [InlineData(0x3F400000u, 0x42700000u, 0x3F200000u)] // equal first floor: zero crossing -> .625
    [InlineData(0x3F800000u, 0x42700000u, 0x3F400000u)] // equal crossing stops at .75
    [InlineData(0x3F800000u, 0x44700000u, 0x00000000u)] // remaining time discarded at final floor
    public void D8_D14_ManagerWalksCurrentFloors(uint start, uint elapsed, uint expected)
    {
        var decay = Rates((F(0x3F400000), F(0x3E800000)), (F(0x3E800000), F(0x3E000000)), (0, F(0x3D800000)));
        var manager = new NeedsManager(() => 0, NeedsConfig.Default with { MinimumNeedLevel = 0 }, decay);
        manager.SetLevel(NeedId.Play, F(start));
        manager.ApplyDecayAllNeeds(true, F(elapsed));
        Assert.Equal(expected, Bits(manager.State.GetNeedLevel(NeedId.Play)));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0xBF800000u)]
    [InlineData(0x7FC00000u)]
    public void D9_D17_MatchedNonpositiveOrUnorderedTimeStillStoresClampsAndDamages(uint elapsed)
    {
        var cfg = NeedsConfig.Default with
        { MinimumNeedLevel = .25, InitialLevels = new Dictionary<NeedId, double> { [NeedId.Repair] = .125 } };
        var table = new Dictionary<NeedId, IReadOnlyList<(double, double)>> { [NeedId.Repair] = new[] { (0.0, .125) } };
        var state = new NeedsState(cfg);
        var calls = new List<NeedsActionId>();
        state.PossiblyDamageParts = calls.Add;
        state.ApplyDecay(NeedId.Repair, new(table, table), F(elapsed), true, 1f);
        Assert.Equal(0x3E800000u, Bits(state.GetNeedLevel(NeedId.Repair)));
        Assert.Equal(new[] { NeedsActionId.Decay }, calls);
    }

    [Fact]
    public void D7_D8_NoMatchSkipsTheDamageCallbackAndStillAdvancesCallerClock()
    {
        var manager = new NeedsManager(() => 0, NeedsConfig.Default, Rates((.75, .25)));
        manager.SetLevel(NeedId.Play, .5);
        manager.ApplyDecayAllNeeds(true, 60f); // no match, but last-decay becomes 60
        manager.SetLevel(NeedId.Play, 1);
        manager.ApplyDecayAllNeeds(true, 120f);
        Assert.Equal(0x3F400000u, Bits(manager.State.GetNeedLevel(NeedId.Play)));
        var calls = 0;
        manager.State.PossiblyDamageParts = _ => calls++;
        manager.ApplyDecayAllNeeds(true, 180f);
        Assert.Equal(0, calls); // Repair has no rate vector
    }

    [Fact]
    public void D1_SnapshotsCurrentBracketsBeforeApplyingDecay()
    {
        var manager = new NeedsManager(() => 0, NeedsConfig.Default, Rates((0, .3)));
        manager.State.SetNeedLevel(NeedId.Play, .4); // Warning before this decay pass.
        var changes = new List<(NeedId Need, NeedBracketId Previous, NeedBracketId Current)>();
        manager.BracketChanged += (need, previous, current, _) => changes.Add((need, previous, current));

        manager.ApplyDecayAllNeeds(true, 60f); // .4 -> .1, crossing Warning -> Critical.

        Assert.Contains((NeedId.Play, NeedBracketId.Warning, NeedBracketId.Critical), changes);
        Assert.DoesNotContain(changes, change => change.Need == NeedId.Play && change.Previous == NeedBracketId.Full);
    }

    [Theory]
    [InlineData(0u, 0x3F800000u)]
    [InlineData(0xBF800000u, 0x3F800000u)]
    [InlineData(0x7FC00000u, 0x7FC00000u)]
    public void D11_BlsRejectsNonpositiveRateButNotNaN(uint rate, uint expected)
    {
        var manager = new NeedsManager(() => 0, NeedsConfig.Default, Rates((0, F(rate))));
        manager.ApplyDecayAllNeeds(true, 60f);
        var actual = manager.State.GetNeedLevel(NeedId.Play);
        if (expected == 0x7FC00000u) Assert.True(double.IsNaN(actual)); // payload propagation not recovered
        else Assert.Equal(expected, Bits(actual));
    }

    [Fact]
    public void D8_NaNThresholdDoesNotMatchAndD16HasNoMaximumClamp()
    {
        var state = new NeedsState(NeedsConfig.Default with
        { InitialLevels = new Dictionary<NeedId, double> { [NeedId.Play] = 2 } });
        state.ApplyDecay(NeedId.Play, Rates((double.NaN, .25)), 60f, true, 1f);
        Assert.Equal(0x40000000u, Bits(state.GetNeedLevel(NeedId.Play)));
        state.ApplyDecay(NeedId.Play, Rates((0, .25)), 0f, true, 1f);
        Assert.Equal(0x40000000u, Bits(state.GetNeedLevel(NeedId.Play)));
    }

    [Fact]
    public void D2_UnorderedModifierThresholdIsSkipped()
    {
        var decay = Rates((0, .125)) with
        {
            Modifiers = new Dictionary<NeedId, IReadOnlyList<DecayModifierEntry>>
            { [NeedId.Repair] = new[] { new DecayModifierEntry(double.NaN, new[] { (NeedId.Play, 8.0) }), new DecayModifierEntry(0, new[] { (NeedId.Play, 2.0) }) } }
        };
        var manager = new NeedsManager(() => 0, NeedsConfig.Default, decay);
        manager.ApplyDecayAllNeeds(true, 60f);
        Assert.Equal(0x3F400000u, Bits(manager.State.GetNeedLevel(NeedId.Play)));
    }
}
