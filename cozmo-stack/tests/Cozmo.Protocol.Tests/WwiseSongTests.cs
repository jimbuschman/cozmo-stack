using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M9, second increment: songs rendered to sound, the switch-state seam, and the Singing behaviour.
///
/// The renderer's dispatch rules are Wwise runtime semantics taken from public documentation
/// (WWISE_MUSIC.md labels them CORROBORATED); what these tests pin is that the shipped data goes through
/// those rules to a complete, non-clipping rendering of every one of the 39 songs, deterministically, and
/// that the behaviour does what the engine's BehaviorSinging disassembly says. Asset tests skip without
/// the OBB; the pure-function tests always run.
/// </summary>
public class WwiseSongTests
{
    private static IEnumerable<string> ObbRoots()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            yield return Path.Combine(d.FullName, "re-analysis", "obb");
            d = d.Parent;
        }
    }

    private static string? Obb() => ObbRoots().FirstOrDefault(r => File.Exists(Path.Combine(r, "assets", "cozmo_resources", "sound", "AudioAssets.zip")));

    /// <summary>The one shipped library every Wwise test class shares; see <see cref="WwiseAssets"/>.</summary>
    private static readonly Lazy<WwiseSoundLibrary?> Library = new(() => WwiseAssets.Library);

    private const uint Group80 = SingingBehavior.Group80, AbaDaba = 0x852F201Au;

    // ------------------------------------------------------------------ pure functions

    /// <summary>
    /// The loop rule: once, N times, or — for the Loop = 0 the whole note-on layer carries — for exactly as
    /// long as the note is held. The 5.79-second average recording under a 187-millisecond note is why
    /// this matters; see <see cref="WwiseSongRenderer.LoopedLength"/>. The last three rows are the ones
    /// that changed: before the M9 fidelity pass they expected a whole iteration of the recording.
    /// </summary>
    [Theory]
    [InlineData(null, 500.0, 2000.0, 500.0)]      // no Loop property: plays once, however long the note
    [InlineData(1u, 500.0, 2000.0, 500.0)]
    [InlineData(3u, 500.0, 100.0, 1500.0)]        // a finite count plays out
    [InlineData(0u, 5790.0, 187.5, 187.5)]        // the shipped case: a 5.79 s vowel sounds for the note
    [InlineData(0u, 500.0, 1200.0, 1200.0)]       // a note longer than the recording sustains by looping
    [InlineData(0u, 500.0, 1000.0, 1000.0)]       // released exactly at an iteration boundary
    [InlineData(0u, 500.0, 0.0, 0.0)]             // a zero-length hold sounds for no time at all
    public void ALoopingSoundSoundsForAsLongAsTheNoteIsHeld(uint? loop, double sampleMs, double heldMs, double expected) =>
        Assert.Equal(expected, WwiseSongRenderer.LoopedLength(loop, sampleMs, heldMs), 6);

    /// <summary>
    /// The shape of the shipped sampler, which is what makes the loop rule above the one it has to be:
    /// every recording under the note-on layer is a sustained vowel of several seconds that loops until
    /// stopped, every recording under the note-off layer is about half a second and does not loop, and the
    /// notes in the songs are a fraction of a second. Read from the bank and the media headers, so a
    /// change to any of the three shows up here rather than only in how a rendered song sounds.
    /// </summary>
    [Fact]
    public void TheSustainRecordingsAreSecondsLongAndLoop_TheReleaseRecordingsAreShortAndDoNot()
    {
        if (Library.Value is not { } lib) return;

        (int Count, double Min, double Max, int Looping) Layer(uint id)
        {
            int count = 0, looping = 0; double min = double.MaxValue, max = 0;
            void Walk(uint node)
            {
                if (lib.Node(node) is not { } n) return;
                if (n is WwiseSoundNode s)
                {
                    var bytes = lib.ReadMedia(s.MediaId, out _);
                    if (bytes is null) return;
                    var m = WwiseMedia.Parse(bytes);
                    if (m.SampleCount is not { } samples || m.SampleRate == 0) return;
                    double ms = samples * 1000.0 / m.SampleRate;
                    count++; min = Math.Min(min, ms); max = Math.Max(max, ms);
                    if (n.Params.Raw(WwiseProp.Loop) == 0) looping++;
                    return;
                }
                foreach (var c in n.Children) Walk(c);
            }
            Walk(id);
            return (count, min, max, looping);
        }

        var on = Layer(462443456);
        Assert.Equal(42, on.Count);
        Assert.Equal(42, on.Looping);                       // every sustain recording loops until stopped
        Assert.True(on.Min > 4000, $"the shortest sustain recording is {on.Min:F0} ms");
        Assert.True(on.Max < 8000, $"the longest sustain recording is {on.Max:F0} ms");

        var off = Layer(774902407);
        Assert.Equal(42, off.Count);
        Assert.Equal(0, off.Looping);                       // release tails do not loop
        Assert.True(off.Max < 1000, $"the longest release recording is {off.Max:F0} ms");

        // and the notes those recordings have to serve
        var ev = lib.IdOf("Play__Robot_VO__Cozmo_Singing_80bpm")!.Value;
        var plan = lib.ResolveMusic(ev, new Dictionary<uint, uint> { [Group80] = AbaDaba });
        var clip = plan.Segments[0].Clips[0];
        var midi = WwiseMidi.Parse(lib.ReadMedia(clip.SourceId, out _)!);
        var notes = midi.NotesAt(plan.Segments[0].TempoBpm).ToList();
        Assert.Equal(42, notes.Count);
        double longest = notes.Max(n => n.LengthMs);
        Assert.True(longest < on.Min,
            $"the longest note in Aba Daba is {longest:F0} ms and the shortest sustain recording {on.Min:F0} ms; " +
            "every note in the song is shorter than every recording that sings it, which is why the note, not " +
            "the recording, has to set how long the voice sounds");
        Assert.True(notes.Count(n => n.LengthMs < 400) > notes.Count / 2,
            "most of the notes are a fraction of a second long");
    }

    /// <summary>
    /// The one thing about the render that the shipped data cannot settle, kept in plain sight instead of
    /// buried in the mix: the get-in branch is a child of the MIDI target and carries no filter of its
    /// own, so under the container rules a note reaches it, and on Aba Daba it contributes about one extra
    /// voice per note. Whether Wwise's MIDI dispatch really routes notes there is Wwise runtime behaviour
    /// and no Wwise runtime ships in the package (fidelity manifest M9-013). The render reports each
    /// branch's share, and a branch can be left out so the two readings can be heard side by side.
    /// </summary>
    [Fact]
    public void TheRenderSaysHowManyVoicesEachBranchOfTheSamplerContributed()
    {
        if (Library.Value is not { } lib) return;
        var ev = lib.IdOf("Play__Robot_VO__Cozmo_Singing_80bpm")!.Value;
        var switches = new Dictionary<uint, uint> { [Group80] = AbaDaba };
        const uint noteOn = 462443456, noteOff = 774902407, getIn = 403781184;

        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(7));
        var all = source.RenderMusic(ev, switches);
        Assert.Equal(all.NotesPlayed, all.VoicesByBranch[noteOn]);
        Assert.Equal(all.NoteOffsPlayed, all.VoicesByBranch[noteOff]);
        Assert.True(all.VoicesByBranch[getIn] > 0,
            "under the container rules the get-in branch does receive notes; if that ever stops being true, M9-013 has been decided");

        using var without = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(7))
        {
            ExcludeBranches = new HashSet<uint> { getIn },
        };
        var trimmed = without.RenderMusic(ev, switches);
        Assert.False(trimmed.VoicesByBranch.ContainsKey(getIn));
        Assert.Equal(all.VoicesByBranch[noteOn], trimmed.VoicesByBranch[noteOn]);
        Assert.Equal(all.VoicesByBranch[noteOff], trimmed.VoicesByBranch[noteOff]);
        Assert.True(trimmed.PreLimitPeak < all.PreLimitPeak,
            "leaving the branch out takes voices out of the mix, so the sum cannot be louder");
        // The settled part of the mix - the two note layers alone - lands just under full scale, whatever
        // the draw, because nothing in it is drawn: one sustain and one release per note.
        Assert.InRange(trimmed.PreLimitPeak / short.MaxValue, 0.5, 1.05);
    }

    /// <summary>BehaviorSinging::UpdateInternal at 0x005EF0C8: half the old value plus half the shake clamped to 0..1 after dividing by 3000.</summary>
    [Theory]
    [InlineData(0f, 3000f, 0.5f)]
    [InlineData(0f, 1500f, 0.25f)]
    [InlineData(0f, 9000f, 0.5f)]                 // clamped
    [InlineData(0.5f, 0f, 0.25f)]                 // decays by half per tick with no shake
    [InlineData(1f, 3000f, 1f)]
    public void TheVibratoParameterFollowsTheEnginesSmoothing(float previous, float shake, float expected) =>
        Assert.Equal(expected, SingingBehavior.NextVibrato(previous, shake), 6);

    /// <summary>The constructor's group table (0x005EE9A0..): the tempo trigger per group, and the fallback to 80 bpm with switch 0.</summary>
    [Fact]
    public void TheTempoTriggerAndFallbackFollowTheEnginesConstructor()
    {
        Assert.Equal(AnimationTrigger.Singing_80bpm, SingingBehavior.TempoTriggerFor(SingingBehavior.Group80));
        Assert.Equal(AnimationTrigger.Singing_100bpm, SingingBehavior.TempoTriggerFor(SingingBehavior.Group100));
        Assert.Equal(AnimationTrigger.Singing_120bpm, SingingBehavior.TempoTriggerFor(SingingBehavior.Group120));
        Assert.Equal((SingingBehavior.Group80, 0u), SingingBehavior.EffectiveSwitch(12345, 678));
        Assert.Equal((SingingBehavior.Group100, 5u), SingingBehavior.EffectiveSwitch(SingingBehavior.Group100, 5));
        Assert.Equal(new[] { AnimationTrigger.Singing_GetIn, AnimationTrigger.Singing_120bpm, AnimationTrigger.Singing_GetOut },
                     SingingBehavior.Sequence(SingingBehavior.Group120));

        var b = new SingingBehavior("Singing_Nonsense", "Not_A_Group", "Not_A_Switch");
        Assert.Equal(SingingBehavior.Group80, b.SwitchGroupId);
        Assert.Equal(0u, b.SwitchId);
        Assert.Equal(AnimationTrigger.Singing_80bpm, b.TempoTrigger);
    }

    // ------------------------------------------------------------------ rendering the shipped songs

    /// <summary>
    /// Aba Daba Honeymoon at 80 bpm: 12 s of sound, every note in the window sung (its keys 53..55 are all
    /// in the voice's range), a note-off for each, no clipping, and the same output for the same seed.
    /// </summary>
    [Fact]
    public void AbaDabaRendersToTwelveSecondsOfSungNotes()
    {
        if (Library.Value is not { } lib) return;
        var ev = lib.IdOf("Play__Robot_VO__Cozmo_Singing_80bpm")!.Value;
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(7));
        var r = source.RenderMusic(ev, new Dictionary<uint, uint> { [Group80] = AbaDaba });

        Assert.Empty(r.Problems);
        Assert.Equal(12000.0, r.DurationMs);
        Assert.Equal(12000 * CozmoAudio.SampleRate / 1000, r.Pcm.Length);
        Assert.Equal(42, r.NotesInWindow);
        Assert.Equal(42, r.NotesPlayed);
        Assert.Equal(0, r.NotesSilent);
        Assert.Equal(42, r.NoteOffsPlayed);
        Assert.Equal(0, r.ClippedSamples);
        // The level the shipped mix produces once a note sounds for its own length: within a few decibels
        // of full scale, where a mix feeding a bus limiter whose threshold is -1 dB belongs. Before the M9
        // fidelity pass every note played out a whole 5.79-second recording and the sum ran about 13 dB
        // over, which is what the output stage was quietly pulling down. How far over full scale it lands
        // now depends on the draw, because the get-in branch's share does (M9-013); the settled part of
        // the mix is measured in TheRenderSaysHowManyVoicesEachBranchOfTheSamplerContributed.
        Assert.InRange(r.PreLimitPeak / short.MaxValue, 0.7, 2.0);
        // and where the robot bus's own limiter leaves it: just under full scale, not pinned to it as the
        // local peak normalisation used to leave every song
        Assert.InRange(r.Peak, 28000, short.MaxValue);
        Assert.NotNull(r.BusChain);
        Assert.True(r.BusChain!.LimiterReductionDb < 0, "the sum was over the limiter's threshold, so it acted");
        Assert.Contains(r.Pcm.Take(CozmoAudio.SampleRate / 2), s => Math.Abs(s) > 500);   // sound in the first half second

        using var again = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(7));
        Assert.Equal(r.Pcm, again.RenderMusic(ev, new Dictionary<uint, uint> { [Group80] = AbaDaba }).Pcm);
    }

    /// <summary>
    /// Whole-library validation: every one of the 39 shipped songs renders to exactly its segment's length
    /// with at least one sung note, no problems and no clipping. Notes outside the voice's 48..61 range are
    /// counted, not hidden; some songs have them.
    /// </summary>
    [Fact]
    public void EveryShippedSongRendersCompletely()
    {
        if (Library.Value is not { } lib || Obb() is not { } obb) return;
        var songs = SingingBehavior.LoadShipped(obb);
        Assert.Equal(39, songs.Count);
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(3));
        int silentTotal = 0;
        foreach (var b in songs)
        {
            var ev = lib.IdOf("Play__Robot_VO__Cozmo_Singing_" + b.SwitchGroupName["Cozmo_Sings_".Length..].ToLowerInvariant())!.Value;
            var r = source.RenderMusic(ev, new Dictionary<uint, uint> { [b.SwitchGroupId] = b.SwitchId });
            Assert.True(r.Problems.Count == 0, $"{b.Id}: {string.Join("; ", r.Problems)}");
            // four bars at the group's tempo for 36 songs (8000, 9600 or 12000 ms; Ta-Ra-Ra-Boom's segment is
            // 9631 ms), and two full-length sequences (Bingo and Tisket Tasket, 462 s) that the animation's
            // Stop event cuts at the clip's end on the robot
            Assert.True(r.DurationMs >= 8000, $"{b.Id}: {r.DurationMs} ms");
            var plan = lib.ResolveMusic(ev, new Dictionary<uint, uint> { [b.SwitchGroupId] = b.SwitchId });
            Assert.Equal(plan.Segments.Sum(sg => sg.DurationMs), r.DurationMs);
            Assert.Equal((int)Math.Round(r.DurationMs * CozmoAudio.SampleRate / 1000), r.Pcm.Length);
            Assert.True(r.NotesPlayed > 0, $"{b.Id}: nothing sung");
            Assert.Equal(r.NotesInWindow, r.NotesPlayed + r.NotesSilent);
            Assert.Equal(0, r.ClippedSamples);
            Assert.True(r.OutputGainDb <= 0 && r.OutputGainDb > -30, $"{b.Id}: output gain {r.OutputGainDb:F1} dB");
            silentTotal += r.NotesSilent;
        }
        Assert.True(silentTotal < 400, $"{silentTotal} notes fell outside the voice's range across all songs");
    }

    /// <summary>
    /// The seam: the same GetPcm the scheduler calls returns the selected song once the switch is set, the
    /// default song when it is not; the Stop event is recognised as a Stop action that covers the song (it
    /// produces no PCM, and the scheduler ends the streaming song on it, see <see cref="TheSingingStopEventEndsTheSongOnTheScheduler"/>).
    /// </summary>
    [Fact]
    public void TheAudioSourcePlaysTheSongTheSwitchSelects()
    {
        if (Library.Value is not { } lib) return;
        var ev = lib.IdOf("Play__Robot_VO__Cozmo_Singing_80bpm")!.Value;
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(1));
        Assert.True(source.IsMusicEvent(ev));
        Assert.False(source.IsMusicEvent(lib.IdOf("Play__Robot_Sfx__Scrn_Happy")!.Value));

        // GetPcm runs on the animation scheduler's thread and never renders there: an unprepared song comes
        // back silent and starts its render on a worker, so the caller prewarms first (as SingingBehavior does)
        Assert.Null(source.GetPcm(ev, 1f));
        Assert.Equal(1, source.UnpreparedMusicEvents);
        source.Prewarm(ev).Wait();
        var byDefault = source.GetPcm(ev, 1f);                          // key-0 path: Yankee Doodle
        Assert.NotNull(byDefault);
        source.SetSwitch(Group80, AbaDaba);
        Assert.Equal(AbaDaba, source.Switches[Group80]);
        source.Prewarm(ev).Wait();
        var abaDaba = source.GetPcm(ev, 1f);
        Assert.NotNull(abaDaba);
        Assert.Equal(12000 * CozmoAudio.SampleRate / 1000, abaDaba!.Length);
        Assert.False(byDefault!.SequenceEqual(abaDaba));                // a different song, the same length: both are 80 bpm
        Assert.NotNull(source.LastMusicRender);

        var stop = lib.IdOf("Stop__Robot_VO__Cozmo_Singing_Stop")!.Value;
        Assert.Null(source.GetPcm(stop, 1f));
        Assert.True(source.IsStopEvent(stop));
        Assert.False(source.IsStopEvent(ev));
        Assert.True(source.StopAffects(stop, ev));                       // the Stop targets the singing container the song plays under
        Assert.False(source.StopAffects(stop, lib.IdOf("Play__Robot_Sfx__Scrn_Happy")!.Value));
    }

    /// <summary>
    /// The regression the Stop fix is for: the tempo clip raises the song at its start and
    /// <c>Stop__Robot_VO__Cozmo_Singing_Stop</c> at its end. Before, the Stop resolved to no PCM, was skipped as
    /// a silent alternative, and a 462 s Bingo kept streaming after the animation. Now it ends the song.
    /// </summary>
    [Fact]
    public void TheSingingStopEventEndsTheSongOnTheScheduler()
    {
        if (Library.Value is not { } lib) return;
        var ev = lib.IdOf("Play__Robot_VO__Cozmo_Singing_100bpm")!.Value;
        var stop = lib.IdOf("Stop__Robot_VO__Cozmo_Singing_Stop")!.Value;
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(2));
        source.SetSwitch(SingingBehavior.Group100, WwiseHash.Of("Cozmo_Sings_Bingo"));
        source.Prewarm(ev).Wait();                                     // the behaviour's prewarm; rendered off the scheduler
        var sink = new CountingSink();
        var s = new AnimationScheduler(sink) { AudioSource = source };
        var clip = new AnimationClip
        {
            Name = "sing", Tracks = AnimationTrack.Audio, DurationMs = 9800,
            Keyframes = new Keyframe[]
            {
                new AudioKeyframe(0, new long[] { ev }, 1f, Array.Empty<float>(), false),
                new AudioKeyframe(9800, new long[] { stop }, 1f, Array.Empty<float>(), false),
            },
        };
        s.Play(clip, 0);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        s.Advance(0);
        Assert.True(sw.ElapsedMilliseconds < 500, $"the first frame took {sw.ElapsedMilliseconds} ms: the song was rendered on the scheduler thread");
        Assert.True(s.AudioStreaming);
        for (double t = 33; t <= 9700; t += 33) s.Advance(t);
        Assert.True(s.AudioStreaming);                                 // still singing just before the Stop
        for (double t = 9733; t <= 10100; t += 33) s.Advance(t);
        Assert.False(s.AudioStreaming);                                // the 462 s render is cut at the clip's Stop
        Assert.Equal(1, s.AudioStops);
        Assert.InRange(sink.Frames, 290, 300);                         // about 9.8 s of frames carried sound
    }

    /// <summary>
    /// Preparation happens on a worker and the scheduler's GetPcm is cheap, which is the property the
    /// animation timeline depends on: a song that took seconds to produce on the scheduler's thread
    /// stalled it at the tempo clip's first audio frame.
    ///
    /// Bingo is the case that makes the point. It is a 462-second sequence, and preparing it now costs
    /// what its recordings cost to decode rather than what 462 seconds cost to mix, because the mixing
    /// happens as it plays. Calling the prewarm twice before the song starts prepares it once.
    /// </summary>
    [Fact]
    public void PreparingASongHappensOffTheSchedulerPathAndGetPcmIsCheap()
    {
        if (Library.Value is not { } lib) return;
        var ev = lib.IdOf("Play__Robot_VO__Cozmo_Singing_100bpm")!.Value;
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(5));
        source.SetSwitch(SingingBehavior.Group100, WwiseHash.Of("Cozmo_Sings_Bingo"));

        var prewarm = source.Prewarm(ev);
        Assert.Same(prewarm, source.Prewarm(ev));                      // one preparation per song, not one per call
        prewarm.Wait();
        Assert.True(source.Prewarm(ev).IsCompleted);                   // and still one, until it has played
        var prepare = source.LastMusicRenderTime;
        Assert.True(prepare > TimeSpan.Zero);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var pcm = source.GetPcm(ev, 1f);
        Assert.NotNull(pcm);
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"GetPcm after a prewarm took {sw.ElapsedMilliseconds} ms (preparing took {prepare.TotalMilliseconds:F0} ms)");
        Assert.Equal((int)(462000L * CozmoAudio.SampleRate / 1000), pcm!.Length);

        // the first block is ready before the song starts, and the rest is not yet mixed
        var stream = source.StreamFor(ev)!;
        Assert.InRange(stream.Ready, 1, pcm.Length - 1);
    }

    /// <summary>
    /// Wwise draws which of a note's three recordings sounds again on every play, and so does this now: a
    /// stream is good for one play, and preparing the same song a second time prepares it again rather
    /// than replaying the samples the first one produced. Before the M9 fidelity pass the final PCM was
    /// cached, which froze the draws for the life of the source.
    ///
    /// Within one play the buffer is of course the same buffer — the scheduler reads it as it fills.
    /// </summary>
    [Fact]
    public void EachPlayOfASongDrawsItsRecordingsAfresh()
    {
        if (Library.Value is not { } lib) return;
        var ev = lib.IdOf("Play__Robot_VO__Cozmo_Singing_80bpm")!.Value;
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(9));
        source.SetSwitch(Group80, AbaDaba);

        source.Prewarm(ev).Wait();
        var first = source.GetPcm(ev, 1f)!;
        Assert.Same(first, source.GetPcm(ev, 1f));                     // one play, one buffer
        source.StreamFor(ev)!.RenderAll();
        var firstPlay = first.ToArray();

        source.Prewarm(ev).Wait();                                     // a second play prepares again
        var second = source.GetPcm(ev, 1f)!;
        Assert.NotSame(first, second);
        source.StreamFor(ev)!.RenderAll();

        Assert.Equal(firstPlay.Length, second.Length);
        Assert.False(firstPlay.SequenceEqual(second),
            "the second play rendered the same recordings; the draw is frozen again");
    }

    private sealed class CountingSink : IAnimationSink
    {
        public int Frames, Silent;
        public void Face(FaceBitmap bitmap) { }
        public void Audio(byte[]? mulawFrame) { if (mulawFrame is null) Silent++; else Frames++; }
        public void Head(sbyte angleDeg, uint durationMs) { }
        public void Lift(byte heightMm, uint durationMs) { }
        public void Body(BodyKeyframe k) { }
        public void AnimationStarted(byte tag) { }
        public void AnimationEnded() { }
        public void BodyStop() { }
        public void Lights(LightsKeyframe k) { }
        public void Event(string eventId) { }
        public void Finished(string clipName, bool completed) { }
    }

    // ------------------------------------------------------------------ the behaviour

    /// <summary>The 39 shipped configs build 39 behaviours whose ids, class and switches are the configs' own.</summary>
    [Fact]
    public void TheThirtyNineShippedSingingBehavioursBuildFromTheirConfigs()
    {
        if (Obb() is not { } obb) return;
        var songs = SingingBehavior.LoadShipped(obb);
        Assert.Equal(39, songs.Count);
        Assert.All(songs, b => Assert.Equal("Singing", b.Class));
        Assert.Equal(39, songs.Select(b => b.Id).Distinct().Count());
        var aba = Assert.Single(songs, b => b.Id == "Singing_AbaDaba");
        Assert.Equal((Group80, AbaDaba), (aba.SwitchGroupId, aba.SwitchId));
        Assert.Equal(AnimationTrigger.Singing_80bpm, aba.TempoTrigger);
        Assert.Equal(12, songs.Count(b => b.TempoTrigger == AnimationTrigger.Singing_80bpm));
        Assert.Equal(17, songs.Count(b => b.TempoTrigger == AnimationTrigger.Singing_100bpm));
        Assert.Equal(10, songs.Count(b => b.TempoTrigger == AnimationTrigger.Singing_120bpm));
        Assert.Equal(39, ShippedBehaviors.Singing(obb).Count);
    }

    /// <summary>
    /// Starting the behaviour does what InitInternal does, in order: the switch is posted before any
    /// animation, reactions are held off, and the first animation is a get-in clip. Stopping it stops that
    /// animation and resets the vibrato parameter.
    /// </summary>
    [Fact]
    public void StartingTheBehaviourPostsTheSwitchThenPlaysTheGetIn()
    {
        if (Library.Value is not { } lib || Obb() is not { } obb) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(1));
        robot.Animations.AudioSource = source;
        var ctx = new BehaviorContext { Robot = robot, Triggers = AnimationTriggerMap.Load(obb), Random = new Random(4) };
        var b = new SingingBehavior("Singing_AbaDaba", "Cozmo_Sings_80Bpm", "Cozmo_Sings_Aba_Daba");
        var trace = new List<string>();
        b.Trace += trace.Add;
        Assert.True(b.IsRunnable(ctx));

        using var scope = new BehaviorScope();
        b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();

        Assert.Equal(AbaDaba, source.Switches[Group80]);
        Assert.StartsWith("switch Cozmo_Sings_80Bpm = Cozmo_Sings_Aba_Daba posted", trace[0]);
        Assert.True(scope.ReactionsDisabled);
        var first = Assert.Single(b.Steps);
        Assert.StartsWith("anim_cozmosings_getin_", first);
        Assert.Equal(first, robot.Animations.Playing);
        Assert.True(b.Update(ctx, 0));

        b.ShakeInput = 3000;
        b.Update(ctx, 33);
        Assert.Equal(0.5f, b.Vibrato, 5);
        b.Stop(BehaviorStopReason.Cancelled);
        Assert.Equal(0f, b.Vibrato);
        Assert.False(b.Update(ctx, 66));
        Assert.False(robot.Animations.IsPlaying);
    }

    /// <summary>Without a switch-capable source the behaviour still runs its animations and says why the song will not resolve.</summary>
    [Fact]
    public void WithoutASwitchCapableSourceTheBehaviourSaysSo()
    {
        if (Obb() is not { } obb) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext { Robot = robot, Triggers = AnimationTriggerMap.Load(obb), Random = new Random(4) };
        var b = new SingingBehavior("Singing_Bingo", "Cozmo_Sings_100Bpm", "Cozmo_Sings_Bingo");
        var trace = new List<string>();
        b.Trace += trace.Add;
        using var scope = new BehaviorScope();
        b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.Contains(trace, t => t.StartsWith("no switch-capable audio source"));
        Assert.Single(b.Steps);
        b.Stop(BehaviorStopReason.Cancelled);
    }
}
