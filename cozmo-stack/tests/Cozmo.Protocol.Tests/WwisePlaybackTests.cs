using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// What one play of an ordinary audio event actually does (fidelity manifest M6-006 and M6-007).
///
/// Before the M9 fidelity pass an event's Play target was flattened to every Sound beneath it and the
/// first one that decoded was played, every time. These tests pin the three things that were lost:
/// a random container draws one child rather than the library handing over all of them, a continuous
/// sequence container plays its items one after another rather than one of them, and the level and pitch
/// a node carries reach the recording under it.
/// </summary>
public class WwisePlaybackTests
{
    private static readonly Lazy<WwiseSoundLibrary?> Library = new(() => WwiseAssets.Library);

    private static WwisePlaybackPlan Plan(WwiseSoundLibrary lib, string eventName, int seed) =>
        WwisePlayback.Resolve(lib, lib.IdOf(eventName)!.Value, new Dictionary<uint, uint>(),
                              new WwiseSelection(new WwiseRng((ulong)seed), sharedAcrossGameObjects: true));

    /// <summary>
    /// The get-in the singing behaviour plays first. Its target is a random container of three phrases;
    /// each phrase is a sequence container whose play mode is continuous, holding two per-key containers;
    /// each of those holds three takes of that note. One play is therefore two recordings, one after the
    /// other — a two-part phrase, not a syllable.
    /// </summary>
    [Fact]
    public void TheGetInEventPlaysATwoPartPhraseRatherThanOneRecording()
    {
        if (Library.Value is not { } lib) return;
        foreach (var name in new[] { "Play__Robot_VO__Singing_Getin_1", "Play__Robot_VO__Singing_Getin_2", "Play__Robot_VO__Singing_Getin_3" })
        {
            var plan = Plan(lib, name, 1);
            Assert.Empty(plan.Problems);
            var sequence = Assert.IsType<WwisePlaySequence>(plan.Root);
            Assert.Equal(2, sequence.Parts.Count);
            Assert.All(sequence.Parts, p => Assert.IsType<WwisePlaySound>(p));
            Assert.Equal(2, plan.Sounds.Count);
            Assert.NotEqual(plan.Sounds[0].MediaId, plan.Sounds[1].MediaId);
        }
    }

    /// <summary>
    /// The draw is a draw. Eighteen recordings are reachable from the first get-in; different seeds reach
    /// different ones, where the old walk always produced the same first decodable file.
    /// </summary>
    [Fact]
    public void DifferentDrawsReachDifferentRecordings()
    {
        if (Library.Value is not { } lib) return;
        var reached = new HashSet<uint>();
        for (int seed = 0; seed < 12; seed++)
            foreach (var s in Plan(lib, "Play__Robot_VO__Singing_Getin_1", seed).Sounds)
                reached.Add(s.MediaId);
        Assert.True(reached.Count > 2, $"twelve draws reached only {reached.Count} recordings");

        // and every one of them is one the library says is reachable from that target
        var all = lib.ResolveMediaIds(lib.IdOf("Play__Robot_VO__Singing_Getin_1")!.Value).ToHashSet();
        Assert.Equal(18, all.Count);
        Assert.All(reached, m => Assert.Contains(m, all));
    }

    /// <summary>
    /// The live get-in draw is the recovered Wwise selection and LCG, not <see cref="Random"/>. The root
    /// container is a shuffle random step container of three unweighted phrases (bank: id 399004754,
    /// randomMode 1, avoidRepeat 1), so on a fresh state every phrase is eligible and the first draw is the
    /// LCG's first output modulo the three phrases (M6-007 §3.1/§3.6b, M9-022). The expected phrase is
    /// derived <b>independently</b>: the 64-bit LCG is hand-rolled here rather than called through
    /// <see cref="WwiseRng"/> or <see cref="WwiseSelectionState"/>, and the fresh-state eligibility rule is
    /// applied directly. The test fails if the production LCG, the fresh-state eligibility, or the
    /// container's play mode is wrong.
    /// </summary>
    [Fact]
    public void TheGetInDrawIsTheRecoveredLcgSelection()
    {
        if (Library.Value is not { } lib) return;
        uint ev = lib.IdOf("Play__Robot_VO__Singing_Getin_1")!.Value;
        uint target = lib.Resolve(ev).Actions
            .Where(a => a.ActionType == WwiseBank.PlayAction).Select(a => a.Target).First();
        var root = Assert.IsType<WwiseRandomSequenceNode>(lib.Node(target));
        // The shipped root's own fields, read from the bank: shuffle, avoid 1, three unweighted phrases.
        Assert.False(root.IsSequence);
        Assert.Equal(1, root.RandomMode);
        Assert.Equal(3, root.Playlist.Count);
        Assert.All(root.Playlist, p => Assert.Equal(WwiseSelectionRows.DefaultWeight, p.Weight));

        var reached = new HashSet<uint>();
        for (ulong seed = 0; seed < 8; seed++)
        {
            // The recovered Wwise LCG, hand-rolled from the inventory: s = s*0x5851F42D4C957F2D + 1,
            // output = (u32)(s >> 32) >> 1 (M6-007 §3.1; M9-022 0x0098A780..0x0098A7B8). With nothing
            // played or blocked on a fresh state, the k-th eligible phrase is the k-th phrase.
            ulong s = unchecked(seed * 0x5851F42D4C957F2DUL + 1UL);
            uint draw = (uint)(s >> 32) >> 1;
            int expected = (int)(draw % (uint)root.Playlist.Count);
            uint chosen = root.Playlist[expected].ChildId;
            reached.Add(chosen);

            var plan = Plan(lib, "Play__Robot_VO__Singing_Getin_1", (int)seed);
            Assert.NotEmpty(plan.Sounds);
            Assert.True(Under(lib, chosen, plan.Sounds[0].SoundId),
                $"seed {seed}: the recovered draw chose phrase {chosen}, but the first recording {plan.Sounds[0].SoundId} is not under it");
        }
        Assert.True(reached.Count > 1, "the eight seeds all named one phrase; the oracle is not exercising the draw");

        // Recurses a node's children and a RanSeq's playlist; a node is its own ancestor.
        static bool Under(WwiseSoundLibrary lib, uint root, uint target)
        {
            if (root == target) return true;
            if (lib.Node(root) is not { } n) return false;
            foreach (var c in n.Children) if (Under(lib, c, target)) return true;
            if (n is WwiseRandomSequenceNode rs)
                foreach (var (c, _) in rs.Playlist) if (Under(lib, c, target)) return true;
            return false;
        }
    }

    /// <summary>
    /// The container fields this rests on, read from the bank: the phrase containers are sequences with
    /// the continuous play mode set, and the per-key containers under them are random and are not
    /// continuous, so they draw one take instead of playing all three.
    /// </summary>
    [Fact]
    public void ThePhraseContainersAreContinuousSequencesAndTheTakeContainersAreStepRandoms()
    {
        if (Library.Value is not { } lib) return;
        int takeContainers = 0, withKeyRange = 0;
        var getIn = Assert.IsType<WwiseRandomSequenceNode>(lib.Node(403781184));
        Assert.False(getIn.IsSequence);
        Assert.Equal(0, getIn.Flags & WwisePlayback.ContinuousFlag);

        foreach (uint phraseGroup in getIn.Children)
        {
            var group = Assert.IsType<WwiseRandomSequenceNode>(lib.Node(phraseGroup));
            foreach (uint phrase in group.Children)
            {
                var seq = Assert.IsType<WwiseRandomSequenceNode>(lib.Node(phrase));
                Assert.True(seq.IsSequence, $"{phrase} should be a sequence container");
                Assert.NotEqual(0, seq.Flags & WwisePlayback.ContinuousFlag);
                Assert.Equal(2, seq.Playlist.Count);
                foreach (var (take, _) in seq.Playlist)
                {
                    var takes = Assert.IsType<WwiseRandomSequenceNode>(lib.Node(take));
                    Assert.False(takes.IsSequence);
                    Assert.Equal(0, takes.Flags & WwisePlayback.ContinuousFlag);
                    Assert.Equal(3, takes.Playlist.Count);
                    takeContainers++;
                    if (takes.Params.Raw(WwiseProp.MidiKeyRangeMin) is not null) withKeyRange++;
                }
            }
        }
        Assert.Equal(18, takeContainers);
        // Most, not all, of them carry a MIDI key range; which of them do decides how far a MIDI note can
        // reach into this branch. See the fidelity manifest, M9-013.
        Assert.InRange(withKeyRange, 1, takeContainers);
    }

    /// <summary>
    /// And the audible consequence: the PCM the scheduler gets for a get-in is about as long as the two
    /// recordings together, not as long as one of them.
    /// </summary>
    [Fact]
    public void TheProducedPcmIsAsLongAsThePhrase()
    {
        if (Library.Value is not { } lib) return;
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new WwiseRng(1));
        uint ev = lib.IdOf("Play__Robot_VO__Singing_Getin_1")!.Value;
        var pcm = source.GetPcm(ev, 1f);
        Assert.NotNull(pcm);
        double seconds = pcm!.Length / (double)CozmoAudio.SampleRate;
        // the recordings under this event are 0.28 to 0.75 s each, so one is under 0.8 s and two are not
        Assert.True(seconds > 0.8, $"a two-part phrase came out {seconds:F2} s long");
        Assert.True(seconds < 2.0, $"a two-part phrase came out {seconds:F2} s long");
        Assert.Empty(source.Misses);
    }

    /// <summary>
    /// A plain event whose target is a random container of single recordings still produces one recording,
    /// and the level its container carries reaches it. This is the ordinary case, which the phrase work
    /// must not have disturbed.
    /// </summary>
    [Fact]
    public void AnOrdinaryVoiceEventStillProducesOneRecording()
    {
        if (Library.Value is not { } lib) return;
        uint ev = lib.IdOf("Play__Robot_Vo__Shared_Happy_Short")
                  ?? lib.IdOf("Play__Robot_Sfx__Scrn_Happy")
                  ?? lib.EventIds.First();
        var plan = WwisePlayback.Resolve(lib, ev, new Dictionary<uint, uint>(),
                                         new WwiseSelection(new WwiseRng(1), sharedAcrossGameObjects: true));
        if (plan.Root is null) return;                       // an event whose media are not in this bank set
        Assert.All(plan.Sounds, s => Assert.NotEqual(0u, s.MediaId));
    }

    /// <summary>
    /// Every event in the library resolves to a plan without the resolver falling over, and no plan reaches
    /// a recording the library does not also list as reachable. This is the whole-library check that makes
    /// the walk trustworthy rather than trustworthy-for-the-get-in.
    /// </summary>
    [Fact, Trait("Category", "Exhaustive")]
    public void EveryEventResolvesToAPlanDrawnFromItsOwnReachableRecordings()
    {
        if (Library.Value is not { } lib) return;
        int planned = 0, empty = 0;
        var selection = new WwiseSelection(new WwiseRng(5), sharedAcrossGameObjects: true);
        foreach (uint ev in lib.EventIds)
        {
            var plan = WwisePlayback.Resolve(lib, ev, new Dictionary<uint, uint>(), selection);
            if (plan.Root is null) { empty++; continue; }
            planned++;
            var reachable = lib.ResolveMediaIds(ev).ToHashSet();
            foreach (var s in plan.Sounds)
                Assert.True(reachable.Contains(s.MediaId),
                    $"event {ev} planned media {s.MediaId}, which is not reachable from its target");
        }
        Assert.True(planned > 550, $"only {planned} of {lib.EventIds.Count} events planned anything");
        // the rest are Stop and state events, music, and the 44 whose targets are not in these banks
        Assert.True(empty < 300, $"{empty} events planned nothing");
    }
}
