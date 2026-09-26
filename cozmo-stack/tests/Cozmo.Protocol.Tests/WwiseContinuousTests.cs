using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-008: continuous RanSeq containers (M6-wwise-bank.md Appendix G §2 and Appendix H §1). Every oracle
/// here comes from those rows or from the shipped-bank census in them, never from the implementation.
/// </summary>
public class WwiseContinuousTests
{
    // ---------------------------------------------------------------- gapF 2.2: loop info

    [Fact]
    public void LoopZeroIsInfiniteAndLoopOneIsASinglePass()
    {
        var rng = new WwiseRng(0);

        Assert.True(new WwiseContinuationLoop(0, 0, 0).Infinite);       // b1
        Assert.True(new WwiseContinuationLoop(0, 0, 0).Enabled);        // b0
        Assert.True(new WwiseContinuationLoop(0, 0, 0).NeverEnds);      // gapF 2.9
        Assert.False(new WwiseContinuationLoop(1, 0, 0).Enabled);       // loop 1 is a single pass
        Assert.False(new WwiseContinuationLoop(1, 0, 0).Infinite);

        // b0 && !b1 → count = loop + loopMin + round(rand·(loopMax−loopMin)), minimum 1.
        Assert.Equal(1, new WwiseContinuationLoop(0, 0, 0).Count(rng));
        Assert.Equal(1, new WwiseContinuationLoop(1, 0, 0).Count(rng));
        Assert.Equal(3, new WwiseContinuationLoop(3, 0, 0).Count(rng));
        Assert.Equal(1, new WwiseContinuationLoop(1, 5, 7).Count(rng));  // loop 1 never modulates
        Assert.Equal(7, new WwiseContinuationLoop(2, 5, 5).Count(rng));  // min = max = 5, span 0
    }

    [Fact]
    public void ALoopAtLeastTwoDrawsItsCountOnlyWhenTheRangeIsNonEmpty()
    {
        // gapF 2.2 (0xA09230..0xA093F0). Seed 7's first draw is 891213470, so fraction ≈ 0.4150036119.
        // draw = (int)(0.5 + 0.4150036119·(6−1)) = (int)2.575018... = 2; count = (short)(3 + 1 + 2) = 6.
        var drawn = new WwiseRng(7);
        Assert.Equal(6, new WwiseContinuationLoop(3, 1, 6).Count(drawn));
        Assert.Equal(427266125u, drawn.Next());                         // exactly one draw consumed

        // Loop 0 and loop 1 take no draw at all (flags & 3 != 1).
        var used = new WwiseRng(7);
        Assert.Equal(1, new WwiseContinuationLoop(0, 0, 0).Count(used));
        Assert.Equal(1, new WwiseContinuationLoop(1, 0, 0).Count(used));

        // A loop >= 2 with an empty range (loopMax − loopMin == 0) also takes no draw (0xA0935C..0xA09368).
        Assert.Equal(3, new WwiseContinuationLoop(3, 0, 0).Count(used));
        Assert.Equal(7, new WwiseContinuationLoop(2, 5, 5).Count(used));  // (short)(2 + 5)

        Assert.Equal(new WwiseRng(7).Next(), used.Next());               // stream unchanged throughout
    }

    [Fact]
    public void AZeroSpanXfadeDoesNotAdvanceTheRng()
    {
        // gapF 2.4 (0xA078E8 / 0xA07978): an empty min..max range skips the whole LCG block.
        var noSpan = SelectorOverPair(new WwiseRng(7));
        var plan = WwiseContinuous.PrepareNextToPlay(
            noSpan, WwiseContinuousTransitionMode.LinearCrossFade, transitionTimeMs: 100);
        Assert.Equal(100.0, plan.XfadeMs, 6);                            // 100 + min 0 + RTPC 0
        Assert.Equal(new WwiseRng(7).Next(), noSpan.Rng.Next());         // no draw consumed

        // A non-empty span still draws exactly once.
        var withSpan = SelectorOverPair(new WwiseRng(7));
        var drawn = WwiseContinuous.PrepareNextToPlay(
            withSpan, WwiseContinuousTransitionMode.LinearCrossFade,
            transitionTimeMs: 100, transitionMinMs: 0, transitionMaxMs: 10);
        Assert.Equal(104.150036119, drawn.XfadeMs, 6);                   // 100 + 0.4150036119·10
        Assert.Equal(427266125u, withSpan.Rng.Next());                   // exactly one draw consumed
    }

    [Fact]
    public void APingPongSequenceCountsCompleteRoundTrips()
    {
        // gapF 2.3 (0xA0863C..0xA08664 forward reversal / 0xA085E4..0xA08630 start-side reversal):
        // playlist 0,1,2 loop 2 ping-pong → 0,1,2,1,0(count 2→1),1,2,1,0(count 1→0) END.
        var selector = new WwiseContinuousSelector(
            new uint[] { 0, 1, 2 }, WwiseSelectionMode.Sequence, pingPong: true,
            new WwiseContinuationLoop(2, 0, 0), new WwiseRng(0));

        int[] expected = [0, 1, 2, 1, 0, 1, 2, 1, 0];
        foreach (int e in expected) Assert.Equal(e, selector.Next());
        Assert.Equal(-1, selector.Next());
    }

    [Fact]
    public void ALoopZeroContainerNeverEndsOnItsOwn()
    {
        var selector = new WwiseContinuousSelector(
            new uint[] { 10, 20 }, WwiseSelectionMode.Sequence, pingPong: false,
            new WwiseContinuationLoop(0, 0, 0), new WwiseRng(0));
        for (int i = 0; i < 50; i++) Assert.InRange(selector.Next(), 0, 1);
    }

    // ---------------------------------------------------------------- gapF 2.9: the shipped n=2 sequence

    [Fact]
    public void AShippedTwoItemLoopOneSequencePlaysBothThenEnds()
    {
        if (WwiseAssets.Library is not { } lib) return;
        var seq = ShippedContinuous(lib)
            .FirstOrDefault(n => n.IsSequence && n.Playlist.Count == 2 && n.LoopCount == 1);
        Assert.NotNull(seq);                                            // the census records nine of these

        var selector = SelectorFor(seq!, new WwiseRng(0));
        Assert.Equal(0, selector.Next());                               // item 0
        Assert.Equal(1, selector.Next());                               // item 1
        Assert.Equal(-1, selector.Next());                              // then the sequence ends (2.9)
    }

    [Fact]
    public void TheNextItemOverlapsTheCurrentByTheXfade()
    {
        if (WwiseAssets.Library is not { } lib) return;
        var seq = ShippedContinuous(lib)
            .FirstOrDefault(n => n.IsSequence && n.Playlist.Count == 2 && n.LoopCount == 1);
        Assert.NotNull(seq);

        // The census: the mode-1 n=2 sequences have a 100 ms transition and 0 min/max.
        var selector = SelectorFor(seq!, new WwiseRng(0));
        var plan = WwiseContinuous.PrepareNextToPlay(
            selector, WwiseContinuousTransitionMode.LinearCrossFade, transitionTimeMs: 100);
        Assert.True(plan.HasNext);
        Assert.Equal(seq!.Playlist[0].ChildId, plan.NextItemId);
        Assert.Equal(100.0, plan.XfadeMs, 6);

        var timing = WwiseContinuous.ScheduleCrossFade(lengthMs: 1000, plan.XfadeMs, plan.HasNext);
        Assert.True(timing.HasScheduledAction);
        Assert.Equal(100.0, timing.XfadeMs, 6);                         // min(stored, length/2)
        Assert.Equal(900.0, timing.StartFromVoiceStartMs, 6);           // xfade before the end
        Assert.Equal(1000.0 - timing.XfadeMs, timing.StartFromVoiceStartMs, 6);
        Assert.Equal(43200L, timing.DelaySamples);                      // round((1000−100)·48000/1000)
    }

    // ---------------------------------------------------------------- gapF 2.4: selection at the voice start

    [Fact]
    public void TheNextItemIsSelectedAtTheVoiceStartNotAtTheTransition()
    {
        if (WwiseAssets.Library is not { } lib) return;
        var seq = ShippedContinuous(lib)
            .FirstOrDefault(n => n.IsSequence && n.Playlist.Count == 2 && n.LoopCount == 1);
        Assert.NotNull(seq);

        var selector = SelectorFor(seq!, new WwiseRng(0));

        // PrepareNextToPlay is the notification the source start posts: it selects now.
        var plan = WwiseContinuous.PrepareNextToPlay(
            selector, WwiseContinuousTransitionMode.LinearCrossFade, transitionTimeMs: 100);
        Assert.True(plan.HasNext);
        Assert.Equal(seq!.Playlist[0].ChildId, plan.NextItemId);

        // If the choice had been deferred to the transition, item 0 would still be next. It is not: the
        // selector has already advanced to item 1.
        Assert.Equal(1, selector.Next());
    }

    // ---------------------------------------------------------------- gapF 2.6: cross-fade gains and mirror

    [Fact]
    public void ModeOneIsLinearAndModeTwoIsConstantPowerAtTheMidpoint()
    {
        Assert.Equal(0.5, WwiseContinuous.CrossFadeIn(WwiseContinuousTransitionMode.LinearCrossFade, 0.5), 10);
        Assert.Equal(0.5, WwiseContinuous.CrossFadeOut(WwiseContinuousTransitionMode.LinearCrossFade, 0.5), 10);

        double half = Math.Sqrt(2) / 2;
        double inGain = WwiseContinuous.CrossFadeIn(WwiseContinuousTransitionMode.ConstantPowerCrossFade, 0.5);
        double outGain = WwiseContinuous.CrossFadeOut(WwiseContinuousTransitionMode.ConstantPowerCrossFade, 0.5);
        Assert.Equal(half, inGain, 10);
        Assert.Equal(half, outGain, 10);
        Assert.Equal(1.0, inGain * inGain + outGain * outGain, 10);     // constant power
    }

    [Theory]
    [InlineData(4, true, 4)]
    [InlineData(1, true, 7)]
    [InlineData(3, true, 3)]
    [InlineData(5, true, 5)]
    [InlineData(1, false, 1)]
    public void TheMirrorRuleMapsEveryCurveExceptThreeAndFive(byte curve, bool descending, byte expected) =>
        Assert.Equal(expected, WwiseContinuous.MirrorCurve(curve, descending));

    [Fact]
    public void TheTransitionDurationIsCeiledToFrames()
    {
        Assert.Equal(5.0, WwiseContinuous.TransitionFrames(100));       // ceil(100 / 21)
        Assert.Throws<NotSupportedException>(() => WwiseContinuous.TransitionProgress(0, 0, 0));
        Assert.Equal(0.5, WwiseContinuous.TransitionProgress(5, 0, 10));
    }

    // ---------------------------------------------------------------- gapF 2.8: the shipped mode-5 container

    [Fact]
    public void TheShippedModeFiveContainerPlaysOnce()
    {
        if (WwiseAssets.Library is not { } lib) return;
        var node = Assert.IsType<WwiseRandomSequenceNode>(lib.Node(777177819));
        Assert.Equal(5, node.TransitionMode & 0x0F);
        Assert.Single(node.Playlist);
        Assert.Equal(1, node.LoopCount);

        var scheduler = new WwiseContinuous.WwiseMode5Scheduler(SelectorFor(node, new WwiseRng(0)), transitionMs: 90);
        var step = scheduler.Step();

        // The first selection consumes the loop count (1 → 0); the look-ahead B is null, so it plays A once
        // and stops scheduling (gapF 2.8).
        Assert.Equal(node.Playlist[0].ChildId, step.ItemId);
        Assert.True(step.StopScheduling);
        Assert.Equal(0.0, step.ReentrySeconds);
    }

    [Fact]
    public void TheModeFiveReentryPeriodAddsTheStartOffsetOverTheMixRate()
    {
        // gapF 2.8 (0xA0A16C..0xA0A1C0): period = max(transition/1000, 0.022) + startOffset/48000.
        var selector = new WwiseContinuousSelector(
            new uint[] { 10, 20, 30 }, WwiseSelectionMode.Sequence, pingPong: false,
            new WwiseContinuationLoop(0, 0, 0), new WwiseRng(0));
        var scheduler = new WwiseContinuous.WwiseMode5Scheduler(
            selector, transitionMs: 90, startOffsetSamples: 4800);

        var step = scheduler.Step();
        Assert.False(step.StopScheduling);
        Assert.Equal(10u, step.ItemId);
        Assert.Equal(0.19, step.ReentrySeconds, 10);                    // 0.09 + 4800/48000
    }

    // ---------------------------------------------------------------- gapF 2.7 / gapG 1: mode 4

    [Fact]
    public void ModeFourChainsImmediatelyWithNoPreviousPbiAndNoFade()
    {
        var action = WwiseContinuous.Mode4PlayAndContinue(currentChainId: 42, engineCommandType1: false);
        Assert.False(action.HasPreviousPbi);
        Assert.Equal(0.0, action.XfadeMs);
        Assert.Equal(0L, action.DelaySamples);
        Assert.Equal(0L, action.StartOffsetSamples);
        Assert.Equal(42u, action.ChainId);

        var pending = WwiseContinuous.AttachPending(
            nextItemId: 7, action.StartOffsetSamples, action.ChainId, freshChainId: 99);
        Assert.Equal(7u, pending.ItemId);
        Assert.Equal(0L, pending.StartOffsetSamples);                   // first sample right after the last
        Assert.Equal(42u, pending.ChainId);                             // same voice
    }

    // ---------------------------------------------------------------- the shipped-bank census

    /// <summary>GapF §2 and gapD D4.1: the 25 continuous containers, all in Cozmo.bnk.</summary>
    [Fact]
    public void TheShippedContinuousCensusMatchesTheInventory()
    {
        if (WwiseAssets.Library is not { } lib) return;
        var continuous = ShippedContinuous(lib).ToList();

        Assert.Equal(25, continuous.Count);
        var byMode = continuous.GroupBy(n => n.TransitionMode & 0x0F).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(15, byMode[1]);
        Assert.Equal(7, byMode[2]);
        Assert.Equal(2, byMode[4]);
        Assert.Equal(1, byMode[5]);

        // All 25 set bank bit1 (fresh state per play, +0x91 bit4) and none sets the ping-pong bit (+0x91 bit5).
        Assert.All(continuous, n =>
        {
            Assert.True(WwiseContinuous.IsFreshStatePerPlay(n.Flags));
            Assert.Equal(0, n.Flags & WwiseContinuous.PingPongFlag);
        });

        // Mode 1: nine n=2 loop-1 sequences and six loop-0 random containers (gapF §2 census).
        Assert.Equal(9, continuous.Count(n =>
            (n.TransitionMode & 0x0F) == 1 && n.IsSequence && n.Playlist.Count == 2 && n.LoopCount == 1));
        Assert.Equal(6, continuous.Count(n =>
            (n.TransitionMode & 0x0F) == 1 && !n.IsSequence && n.LoopCount == 0));

        // Mode 4: the random and the n=2 sequence, both loop 0 and loop 1 respectively.
        Assert.Contains(continuous, n => (n.TransitionMode & 0x0F) == 4 && !n.IsSequence && n.LoopCount == 0);
        Assert.Contains(continuous, n => (n.TransitionMode & 0x0F) == 4 && n.IsSequence && n.Playlist.Count == 2 && n.LoopCount == 1);
    }

    // ---------------------------------------------------------------- helpers

    private static IEnumerable<WwiseRandomSequenceNode> ShippedContinuous(WwiseSoundLibrary lib) =>
        lib.AllNodeIds.Select(lib.Node).OfType<WwiseRandomSequenceNode>()
            .Where(n => (n.Flags & WwiseContinuous.ContinuousFlag) != 0);

    private static WwiseContinuousSelector SelectorOverPair(WwiseRng rng) =>
        new(new uint[] { 1, 2 }, WwiseSelectionMode.Sequence, pingPong: false,
            new WwiseContinuationLoop(1, 0, 0), rng);

    private static WwiseContinuousSelector SelectorFor(WwiseRandomSequenceNode node, WwiseRng rng)
    {
        var playlist = node.Playlist.Select(p => p.ChildId).ToList();
        var mode = node.IsSequence ? WwiseSelectionMode.Sequence : WwiseSelectionMode.Random;
        bool pingPong = (node.Flags & WwiseContinuous.PingPongFlag) != 0;
        var loop = new WwiseContinuationLoop(node.LoopCount, 0, 0);
        WwiseSelectionState? state = mode == WwiseSelectionMode.Random && playlist.Count > 1
            ? new WwiseSelectionState(WwiseContainerSelectionSettings.Random(
                playlist.Count, WwiseRandomMode.Standard, node.AvoidRepeatCount))
            : null;
        return new WwiseContinuousSelector(playlist, mode, pingPong, loop, rng, state);
    }
}