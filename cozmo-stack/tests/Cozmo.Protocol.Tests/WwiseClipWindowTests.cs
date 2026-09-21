using Cozmo.Robot;
using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Two claims the M9 re-audit found were being asserted rather than measured, pinned to the numbers the
/// shipped data actually produces (fidelity manifest M9-014 and M9-020).
///
/// The manifest used to say the clip-window rule was "barely exercised" because every clip has PlayAt 0
/// and BeginTrim 0. The start of the window is indeed never moved — but the <em>end</em> of it is what
/// turns a MIDI source minutes long into a twelve-second song, and it discards more notes than the songs
/// contain. And velocity was called ignored without saying what in the data makes it ignorable.
/// </summary>
public class WwiseClipWindowTests
{
    private static readonly Lazy<WwiseSoundLibrary?> Library = new(() => WwiseAssets.Library);

    private const uint SamplerTarget = 110896138;

    /// <summary>One shipped song, by the event that names it, with no switch to get wrong.</summary>
    private static WwiseRenderedMusic Song(WwiseSoundLibrary lib, string songEvent)
    {
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(7));
        return source.RenderMusic(lib.IdOf(songEvent)!.Value, new Dictionary<uint, uint>());
    }

    /// <summary>
    /// The clip end decides most of what is sung. William Tell's MIDI source holds a full-length
    /// rendition; the clip takes the first twelve seconds of it, so 762 of its 803 notes never start.
    /// This is not a rounding detail at the edge of a song — it is the rule that makes the song.
    /// </summary>
    [Fact]
    public void TheClipEndDecidesWhichNotesAreSungAtAll()
    {
        if (Library.Value is not { } lib) return;

        var r = Song(lib, "Play__Robot_VO__Singing_William_Tell");
        Assert.Equal(41, r.NotesInWindow);
        Assert.Equal(41, r.NotesPlayed);
        Assert.Equal(762, r.NotesOutsideWindow);
        Assert.Equal(12000, r.DurationMs, 0);
    }

    /// <summary>
    /// And the other half of the rule — a note still held when its clip ends is released there — reaches
    /// exactly one note in this song, which is one of only two in the whole product. Its release lands on
    /// the last sample of the segment, so whatever it would have sounded past that point falls outside the
    /// rendered song either way: the two readings of the rule cannot be told apart here.
    /// </summary>
    [Fact]
    public void ANoteHeldAtTheClipEndIsReleasedThereAndTheSongEndsAtTheSameInstant()
    {
        if (Library.Value is not { } lib) return;

        var r = Song(lib, "Play__Robot_VO__Singing_Take_Me_Out_Ballgame");
        Assert.Equal(1, r.NotesCutByClipEnd);
        Assert.Equal(33, r.NotesPlayed);
        // the clip ends where the segment ends, so the buffer stops at the same instant the note is released
        Assert.Equal(12000, r.DurationMs, 0);
        Assert.Equal(CozmoAudio.SampleRate * 12, r.Pcm.Length);
    }

    /// <summary>
    /// A song whose notes all fall inside its clip loses nothing, so the counts above are the data's doing
    /// and not the renderer dropping notes it should be playing.
    /// </summary>
    [Fact]
    public void ASongThatFitsInsideItsClipLosesNothing()
    {
        if (Library.Value is not { } lib) return;

        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(7));
        var r = source.RenderMusic(lib.IdOf("Play__Robot_VO__Cozmo_Singing_80bpm")!.Value,
            new Dictionary<uint, uint> { [SingingBehavior.Group80] = 0x852F201Au });
        Assert.Equal(0, r.NotesOutsideWindow);
        Assert.Equal(0, r.NotesCutByClipEnd);
        Assert.Equal(42, r.NotesPlayed);
    }

    /// <summary>
    /// Why velocity is ignored, as the data rather than as an opinion: of every node under the singing
    /// sampler, not one carries a velocity range, and the only RTPC binding anywhere under it is the
    /// vibrato modulator. There is nothing for a note's velocity to reach.
    ///
    /// It is not that velocity is uniform — the songs vary it — which is why M9-014 stays an open question
    /// about the Wwise runtime rather than a settled one.
    /// </summary>
    [Fact]
    public void NothingUnderTheSamplerAsksForTheNoteVelocity()
    {
        if (Library.Value is not { } lib) return;

        int nodes = 0, withVelocityRange = 0, bindings = 0, nonModulatorBindings = 0;
        // the three layers and everything below them; the target itself carries the MIDI routing, not a note
        void Walk(uint id)
        {
            if (lib.Node(id) is not { } n) return;
            nodes++;
            var p = n.Params;
            if (p.Raw(WwiseProp.MidiVelocityRangeMin) is not null || p.Raw(WwiseProp.MidiVelocityRangeMax) is not null)
                withVelocityRange++;
            foreach (var r in p.Rtpcs)
            {
                bindings++;
                if (r.SourceType != WwiseRtpc.ModulatorSource) nonModulatorBindings++;
            }
            foreach (var kid in n.Children) Walk(kid);
        }
        foreach (var child in lib.Node(SamplerTarget)!.Children) Walk(child);

        Assert.Equal(199, nodes);
        Assert.Equal(0, withVelocityRange);
        Assert.Equal(1, bindings);              // the vibrato LFO on the sampler's pitch, and nothing else
        Assert.Equal(0, nonModulatorBindings);
    }

    /// <summary>
    /// And that the question is worth leaving open: the shipped songs do vary velocity, by about twenty
    /// units, so an implicit velocity-to-level mapping in the Wwise runtime would be audible if it exists.
    /// </summary>
    [Fact]
    public void TheShippedSongsDoVaryTheirNoteVelocities()
    {
        if (Library.Value is not { } lib) return;
        if (WwiseAssets.ObbRoot is not { } obb) return;

        byte low = 127, high = 0;
        int songsThatVary = 0;
        foreach (var b in SingingBehavior.LoadShipped(obb))
        {
            if (lib.IdOf(SingingBehavior.TempoEventName(b.SwitchGroupId)) is not { } ev) continue;
            var plan = lib.ResolveMusic(ev, new Dictionary<uint, uint> { [b.SwitchGroupId] = b.SwitchId });
            byte songLow = 127, songHigh = 0;
            foreach (var seg in plan.Segments)
            foreach (var clip in seg.Clips)
            {
                if (!clip.IsMidi || lib.ReadMedia(clip.SourceId, out _) is not { } bytes) continue;
                foreach (var n in WwiseMidi.Parse(bytes).NotesAt(seg.TempoBpm))
                {
                    if (n.StartMs < clip.Clip.BeginTrimMs || n.StartMs >= clip.Clip.BeginTrimMs + clip.Clip.LengthMs) continue;
                    songLow = Math.Min(songLow, n.Velocity); songHigh = Math.Max(songHigh, n.Velocity);
                }
            }
            if (songHigh == 0) continue;
            if (songHigh > songLow) songsThatVary++;
            low = Math.Min(low, songLow); high = Math.Max(high, songHigh);
        }

        Assert.True(songsThatVary > 10, $"only {songsThatVary} shipped songs vary their note velocity");
        Assert.True(high - low >= 20, $"the shipped velocities span only {high - low} units ({low}..{high})");
    }
}
