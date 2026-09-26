using System.Numerics;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-007: the Wwise random/sequence step selection engine (gapA §3). Every expectation here is derived from
/// the frozen rows — the LCG formula, the sequence step, and the eligibility/avoid rules — not from the
/// implementation. Fixed seeds make the draws reproducible; the original's own seed is time(NULL) and is
/// therefore never reproduced (§3.2).
/// </summary>
public class WwiseSelectionTests
{
    // ---------------------------------------------------------------- §3.1 the LCG

    /// <summary>
    /// The exact LCG the runtime inlines everywhere (§3.1). The expected values are computed independently
    /// with BigInteger arithmetic from the stated formula, so this is not a tautology against the C# operator
    /// precedence.
    /// </summary>
    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(12345UL)]
    [InlineData(0xDEADBEEFUL)]
    public void TheLcgMatchesTheFormulaFromAnIndependentOracle(ulong seed)
    {
        var expected = ExpectedLcg(seed, 8);
        var rng = new WwiseRng(seed);
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], rng.Next());
    }

    /// <summary>A fixed seed's first draws, computed from the formula (seed 0's first state is 1, so it draws 0).</summary>
    [Fact]
    public void TheLcgPinsAFixedSeed()
    {
        uint[] expected = [0, 740882966, 1616430695, 1708849955, 1669437588, 406334850];
        var rng = new WwiseRng(0);
        foreach (uint e in expected) Assert.Equal(e, rng.Next());
    }

    /// <summary>The output is <c>(u32)(s&gt;&gt;32) &gt;&gt; 1</c>, so it is a 31-bit value, never negative.</summary>
    [Fact]
    public void TheDrawIs31Bit()
    {
        var rng = new WwiseRng(7);
        for (int i = 0; i < 1000; i++) Assert.InRange(rng.Next(), 0u, 0x7FFFFFFFu);
    }

    // ---------------------------------------------------------------- §3.5 the single-item rule

    /// <summary>Length 1 is item 0 and must consume no draw.</summary>
    [Fact]
    public void ASingleItemIsChosenWithoutADraw()
    {
        var state = new WwiseSelectionState(WwiseContainerSelectionSettings.Random(1, WwiseRandomMode.Shuffle, 3));
        var used = new WwiseRng(99);
        var reference = new WwiseRng(99);

        Assert.Equal(0, state.SelectNext(used));
        // If SelectNext had drawn, the two streams would diverge.
        Assert.Equal(reference.Next(), used.Next());
    }

    // ---------------------------------------------------------------- §3.7 the sequence step

    [Fact]
    public void ASequenceWrapsWhenNotPingPong()
    {
        var state = new WwiseSelectionState(WwiseContainerSelectionSettings.Sequence(3, pingPong: false));
        var rng = new WwiseRng(0);
        // The first play gives 0, then forward; a sequence never draws.
        int[] expected = [0, 1, 2, 0, 1, 2, 0];
        foreach (int e in expected) Assert.Equal(e, state.SelectNext(rng));
    }

    [Fact]
    public void ASequencePingPongsWhenBankBit2IsSet()
    {
        var state = new WwiseSelectionState(WwiseContainerSelectionSettings.Sequence(3, pingPong: true));
        var rng = new WwiseRng(0);
        // Forward 0,1,2; at len reverse to len-2; backward to 0; at 0 turn forward and play 1.
        int[] expected = [0, 1, 2, 1, 0, 1, 2, 1, 0, 1];
        foreach (int e in expected) Assert.Equal(e, state.SelectNext(rng));
    }

    // ---------------------------------------------------------------- §3.6b/§3.6d standard + avoid

    /// <summary>
    /// The exact trace for a fixed seed. The second pick proves the k-th *eligible* rule: index 0 was just
    /// blocked, so k = 0 selects the first eligible item, which is index 1.
    /// </summary>
    [Fact]
    public void StandardUnweightedPicksTheKthEligibleItem()
    {
        var state = new WwiseSelectionState(Standard(length: 3, avoidRepeat: 1));
        var rng = new WwiseRng(0);

        Assert.Equal(0, state.SelectNext(rng));   // k = 0, all eligible
        Assert.True(state.IsBlocked(0));
        Assert.Equal(1, state.SelectNext(rng));   // k = 0 among {1,2}, since 0 is blocked
        Assert.Equal(2, state.SelectNext(rng));
        Assert.Equal(1, state.SelectNext(rng));   // counter reset; 0 is blocked, picks {0,1}->k=1
        Assert.Equal(0, state.SelectNext(rng));
        Assert.Equal(1, state.SelectNext(rng));
    }

    /// <summary>
    /// An item selected under standard mode is unavailable for the next <c>avoid</c> picks. The avoid list is
    /// capped at <c>min(avoid, len−1)</c> (§3.6d), which is what unblocks it again.
    /// </summary>
    [Theory]
    [InlineData(5, 2)]
    [InlineData(4, 1)]
    [InlineData(7, 3)]
    public void StandardDoesNotRepeatAnItemWithinTheAvoidWindow(int length, int avoid)
    {
        var state = new WwiseSelectionState(Standard(length, avoid));
        var rng = new WwiseRng(12345);

        int[] picks = new int[80];
        for (int i = 0; i < picks.Length; i++) picks[i] = state.SelectNext(rng);

        Assert.All(picks, p => Assert.InRange(p, 0, length - 1));
        for (int i = 0; i < picks.Length; i++)
            for (int d = 1; d <= avoid && i + d < picks.Length; d++)
                Assert.NotEqual(picks[i], picks[i + d]);
    }

    // ---------------------------------------------------------------- §3.6b/§3.6d shuffle

    /// <summary>
    /// Shuffle excludes played items, so the first full cycle is a permutation of the playlist. With seed
    /// 12345 it is also a pinned order.
    /// </summary>
    [Fact]
    public void ShufflePlaysEveryItemOnceBeforeResetting()
    {
        var state = new WwiseSelectionState(Shuffle(length: 5, avoidRepeat: 1));
        var rng = new WwiseRng(12345);

        int[] firstCycle = [2, 1, 3, 4, 0];
        foreach (int e in firstCycle) Assert.Equal(e, state.SelectNext(rng));
        Assert.Equal(firstCycle.OrderBy(x => x), new[] { 0, 1, 2, 3, 4 });
        Assert.Equal(0, state.Counter);
    }

    /// <summary>Shuffle always avoids the previous item, even with avoidRepeat 0 (limit is max(avoid, 1)).</summary>
    [Fact]
    public void ShuffleNeverRepeatsTheImmediatelyPreviousItem()
    {
        var state = new WwiseSelectionState(Shuffle(length: 4, avoidRepeat: 0));
        var rng = new WwiseRng(0);

        int previous = -1;
        for (int i = 0; i < 40; i++)
        {
            int pick = state.SelectNext(rng);
            Assert.NotEqual(previous, pick);
            previous = pick;
        }
    }

    // ---------------------------------------------------------------- §3.6c weighted

    [Fact]
    public void WeightedSelectionNeverPicksAZeroWeightEligibleItem()
    {
        var rng = new WwiseRng(0);
        var neverSecond = new WwiseSelectionState(Weighted([100, 0]));
        var neverFirst = new WwiseSelectionState(Weighted([0, 100]));
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(0, neverSecond.SelectNext(rng));
            Assert.Equal(1, neverFirst.SelectNext(rng));
        }
    }

    /// <summary>The pick is the first eligible index whose running weight sum exceeds r = draw mod remaining weight.</summary>
    [Fact]
    public void WeightedSelectionUsesTheRunningWeightSum()
    {
        var state = new WwiseSelectionState(Weighted([10, 90]));
        var rng = new WwiseRng(0);
        // r = 0 -> index 0; the next five draws are all >= 10 -> index 1.
        int[] expected = [0, 1, 1, 1, 1, 1];
        foreach (int e in expected) Assert.Equal(e, state.SelectNext(rng));
    }

    // ---------------------------------------------------------------- §3.5 playability probe

    /// <summary>
    /// When the chosen node is not playable, shuffle and standard-with-avoid probe forward from idx+1,
    /// skipping played/blocked items (§3.5).
    /// </summary>
    [Fact]
    public void AnUnplayablePickProbesForwardSkippingBlockedItems()
    {
        var state = new WwiseSelectionState(Standard(length: 4, avoidRepeat: 2));
        var rng = new WwiseRng(0);

        // Seed 0 draws k = 0, so index 0 would be chosen; make it unplayable.
        int pick = state.SelectNext(rng, index => index != 0);
        Assert.Equal(1, pick);
    }

    // ---------------------------------------------------------------- §3.4 where the state lives

    [Fact]
    public void SharedStateIsOneStateForEveryGameObject()
    {
        var selection = new WwiseSelection(rng: new WwiseRng(0), sharedAcrossGameObjects: true);
        var settings = Standard(length: 4, avoidRepeat: 1);

        var a = selection.StateFor(containerId: 42, gameObject: 7, settings);
        var b = selection.StateFor(containerId: 42, gameObject: 9, settings);
        Assert.Same(a, b);
    }

    [Fact]
    public void PerObjectStateIsSeparateWhenBankBit4IsClear()
    {
        var selection = new WwiseSelection(rng: new WwiseRng(0), sharedAcrossGameObjects: false);
        var settings = Standard(length: 4, avoidRepeat: 1);

        var a = selection.StateFor(containerId: 42, gameObject: 7, settings);
        var b = selection.StateFor(containerId: 42, gameObject: 9, settings);
        Assert.NotSame(a, b);

        // Each object's stream is independent: selecting on one does not advance the other.
        selection.NextIndex(42, 7, settings);
        Assert.Equal(4, selection.StateFor(42, 9, settings).Remaining); // untouched: still a fresh cycle
    }

    // ---------------------------------------------------------------- helpers

    private static WwiseContainerSelectionSettings Standard(int length, int avoidRepeat)
        => new(length, null, WwiseSelectionMode.Random, WwiseRandomMode.Standard, avoidRepeat, false, false);

    private static WwiseContainerSelectionSettings Shuffle(int length, int avoidRepeat)
        => new(length, null, WwiseSelectionMode.Random, WwiseRandomMode.Shuffle, avoidRepeat, false, false);

    private static WwiseContainerSelectionSettings Weighted(int[] weights)
        => new(weights.Length, weights, WwiseSelectionMode.Random, WwiseRandomMode.Standard, 0, true, false);

    /// <summary>The formula in BigInteger so no C# overflow semantics can hide a mistake.</summary>
    private static uint[] ExpectedLcg(ulong seed, int count)
    {
        var multiplier = new BigInteger(0x5851F42D4C957F2DUL);
        var mask = (BigInteger.One << 64) - 1;
        var state = new BigInteger(seed);
        var output = new uint[count];
        for (int i = 0; i < count; i++)
        {
            state = (state * multiplier + 1) & mask;
            uint high = (uint)(ulong)((state >> 32) & 0xFFFFFFFFUL);
            output[i] = high >> 1;
        }
        return output;
    }
}