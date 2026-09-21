using System.Text.Json;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M9, first increment: the music hierarchy and the Cozmo_Sings chain, read from the shipped banks.
///
/// What is asserted here was recovered, not designed: the engine's <c>BehaviorSinging</c> posts the
/// behaviour file's switch (<c>RobotAudioClient::PostRobotSwitchState</c> at 0x0059EEB4E) and plays the
/// <c>Singing_GetIn</c>, tempo and <c>Singing_GetOut</c> animation triggers in sequence; the three
/// <c>Play__Robot_VO__Cozmo_Singing_*bpm</c> events target music switch containers whose decision trees
/// are keyed by the switch ids the decompiled <c>Anki.AudioMetaData.SwitchState</c> enums carry; each leaf
/// is a playlist of one segment holding one MIDI clip; the MIDI notes go to a blend container of per-note
/// vocal samples. The synthetic tests at the end run without the assets.
/// </summary>
public class WwiseMusicTests
{
    // ------------------------------------------------------------------ asset locations

    private static IEnumerable<string> ObbRoots()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            yield return Path.Combine(d.FullName, "re-analysis", "obb");
            d = d.Parent;
        }
    }

    /// <summary>The directory holding AudioAssets.zip (banks, text tables and media all live inside it), or null.</summary>
    private static string? SoundDir()
    {
        foreach (var root in ObbRoots())
        {
            var sound = Path.Combine(root, "assets", "cozmo_resources", "sound");
            if (File.Exists(Path.Combine(sound, "AudioAssets.zip"))) return sound;
        }
        return null;
    }

    private static string? SingingConfigDir()
    {
        foreach (var root in ObbRoots())
        {
            var dir = Path.Combine(root, "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "behaviors", "freeplay", "singing");
            if (Directory.Exists(dir)) return dir;
        }
        return null;
    }

    /// <summary>The one shipped library every Wwise test class shares; see <see cref="WwiseAssets"/>.</summary>
    private static readonly Lazy<WwiseSoundLibrary?> Library = new(() => WwiseAssets.Library);

    // ------------------------------------------------------------------ the shipped banks

    /// <summary>
    /// The layout check: every object of the fourteen hierarchy types, in all six banks, consumes its
    /// payload to the last byte. A field of the wrong width anywhere in the shared node block would fail
    /// hundreds of these at once. The counts are the banks' own. The modulator, bus and effect types were
    /// added to the reader in the M9 fidelity pass, and the fifteen buses are what settled the two
    /// variable parts of a bus's layout.
    /// </summary>
    [Fact]
    public void EveryHierarchyObjectInTheShippedBanksConsumesExactly()
    {
        if (Library.Value is not { } lib) return;
        var report = lib.CheckHierarchy();
        var expected = new Dictionary<WwiseObjectType, int>
        {
            [WwiseObjectType.Sound] = 2360, [WwiseObjectType.RandomSequenceContainer] = 468,
            [WwiseObjectType.SwitchContainer] = 21, [WwiseObjectType.ActorMixer] = 31,
            [WwiseObjectType.BlendContainer] = 6, [WwiseObjectType.MusicSegment] = 209,
            [WwiseObjectType.MusicTrack] = 258, [WwiseObjectType.MusicSwitchContainer] = 14,
            [WwiseObjectType.MusicPlaylistContainer] = 123,
            [WwiseObjectType.LfoModulator] = 4, [WwiseObjectType.EnvelopeModulator] = 7,
            [WwiseObjectType.AudioBus] = 15, [WwiseObjectType.FxShareSet] = 23, [WwiseObjectType.FxCustom] = 65,
        };
        foreach (var (type, count, exact, problems) in report)
        {
            Assert.True(expected.TryGetValue(type, out var n), $"unexpected type {type} in the report");
            Assert.Equal(n, count);
            Assert.True(exact == count, $"{type}: {exact} of {count} consumed exactly; {string.Join(" | ", problems)}");
        }
        Assert.Equal(expected.Count, report.Count);
    }

    /// <summary>
    /// A blend container can group its children into blend tracks, each crossfaded by an RTPC, and the
    /// reader walks past that block without keeping it. Nothing is lost by that here: not one of the six
    /// blend containers in any of the six shipped banks has a single blend track, so every blend container
    /// plays all of its children at the level their own properties give. (Fidelity manifest M9-019.)
    /// </summary>
    [Fact]
    public void NoBlendContainerInAnyShippedBankHasABlendTrack()
    {
        if (Library.Value is not { } lib) return;
        int blends = 0;
        foreach (uint id in lib.AllNodeIds)
        {
            if (lib.Node(id) is not WwiseBlendNode blend) continue;
            blends++;
            Assert.Equal(0, blend.BlendTracks);
            Assert.NotEmpty(blend.Children);
        }
        Assert.Equal(6, blends);
    }

    /// <summary>The archive alone is a complete library: banks, names and media all come out of AudioAssets.zip.</summary>
    [Fact]
    public void TheBanksAndNameTablesLoadFromTheShippedArchive()
    {
        if (Library.Value is not { } lib) return;
        Assert.Equal(6, lib.Banks.Count);
        Assert.All(lib.Banks, b => Assert.Equal(120u, b.Version));
        Assert.Equal(835, lib.EventIds.Count);
        Assert.True(lib.MediaFileCount > 2000);
        // the game parameter the engine's BehaviorSinging drives from cube shaking, by its Unity enum value
        Assert.Equal("cozmo_singing_vibrato", lib.Names.GameParameters[0xC20F49DFu], ignoreCase: true);
        Assert.Equal(0xC20F49DFu, lib.Names.GameParameterId("cozmo_singing_vibrato"));
        // the bus the singing containers route to
        Assert.Equal("Master Audio Bus", lib.Names.Buses[3803692087u]);
    }

    /// <summary>
    /// The Cozmo_Sings names are not in the bank text tables (those list only the voice-processing,
    /// mood and music groups), so they reach the bank through the FNV-1 hash, which the decompiled
    /// Anki.AudioMetaData.SwitchState enums confirm value for value.
    /// </summary>
    [Theory]
    [InlineData("Cozmo_Sings_80Bpm", 0xC8A59578u)]
    [InlineData("Cozmo_Sings_100Bpm", 0xE017E775u)]
    [InlineData("Cozmo_Sings_120Bpm", 0xB215BB17u)]
    [InlineData("Cozmo_Sings_Aba_Daba", 0x852F201Au)]
    [InlineData("Cozmo_Sings_Yankee_Doodle", 0xE8DF384Cu)]
    [InlineData("Cozmo_Sings_Twinkle_Twinkle", 0xAE66CCD2u)]
    [InlineData("Cozmo_Singing_Vibrato", 0xC20F49DFu)]
    public void TheSingingNamesHashToTheUnityEnumValues(string name, uint id) => Assert.Equal(id, WwiseHash.Of(name));

    /// <summary>
    /// The three tempo events the singing animations raise each target a music switch container whose
    /// one argument is the matching Cozmo_Sings group, whose tempo is the group's, and whose MIDI target
    /// (property 56) is the same blend container, 110896138.
    /// </summary>
    [Theory]
    [InlineData("Play__Robot_VO__Cozmo_Singing_80bpm", 914766641u, 0xC8A59578u, 80f, 12000.0)]
    [InlineData("Play__Robot_VO__Cozmo_Singing_100bpm", 139286641u, 0xE017E775u, 100f, 9600.0)]
    [InlineData("Play__Robot_VO__Cozmo_Singing_120bpm", 602865028u, 0xB215BB17u, 120f, 8000.0)]
    public void TheSingingEventsTargetTheirTempoSwitchContainer(string eventName, uint containerId, uint groupId, float tempo, double gridMs)
    {
        if (Library.Value is not { } lib) return;
        var eventId = lib.IdOf(eventName);
        Assert.NotNull(eventId);
        var plan = lib.ResolveMusic(eventId!.Value, new Dictionary<uint, uint>());
        Assert.Equal(containerId, plan.TargetId);
        Assert.Equal(WwiseObjectType.MusicSwitchContainer, plan.TargetType);
        var sw = Assert.IsType<WwiseMusicSwitchNode>(plan.Switch);
        Assert.Equal(new[] { groupId }, sw.Arguments.Select(a => a.GroupId));
        Assert.Equal(tempo, sw.Meter.TempoBpm);
        Assert.Equal(gridMs, sw.Meter.GridPeriodMs);
        Assert.Equal(110896138u, sw.MidiTargetNode);
        Assert.Equal(110896138u, plan.MidiTargetNodeId);
        // with no switch set the key-0 entry is taken and a song still results
        Assert.Null(plan.Problem);
        Assert.Single(plan.Segments);
    }

    /// <summary>
    /// An event can fire more than one Play action, and all of them play. The nine layers of
    /// <c>Play__Codelab__Music_Tiny_Orchestra_Init</c> are nine Play actions on one event; the resolver
    /// used to take the first and drop the other eight.
    /// </summary>
    [Fact]
    public void AnEventWithSeveralPlayActionsResolvesAllOfThem()
    {
        if (Library.Value is not { } lib) return;
        var eventId = lib.IdOf("Play__Codelab__Music_Tiny_Orchestra_Init");
        if (eventId is null) return;
        var plan = lib.ResolveMusic(eventId.Value, new Dictionary<uint, uint>());
        Assert.Equal(9, plan.Targets.Count);
        Assert.Equal(8, plan.AdditionalPlays.Count);
        Assert.Equal(plan.Targets[0], plan.TargetId);
        Assert.All(plan.AdditionalPlays, p => Assert.Contains(p.TargetId, plan.Targets));
        // every layer reaches segments of its own
        Assert.All(plan.AdditionalPlays, p => Assert.NotEmpty(p.Segments));

        // and a single-Play event is unchanged
        var singing = lib.IdOf("Play__Robot_VO__Cozmo_Singing_100bpm");
        Assert.NotNull(singing);
        var one = lib.ResolveMusic(singing!.Value, new Dictionary<uint, uint>());
        Assert.Single(one.Targets);
        Assert.Empty(one.AdditionalPlays);
    }

    /// <summary>
    /// Every one of the 39 shipped Singing behaviours resolves: its audioSwitchGroup names a container, its
    /// audioSwitch is a key in that container's decision tree (not the key-0 fallback), the leaf is a
    /// playlist of exactly one segment, and that segment holds exactly one clip, which is MIDI and exactly
    /// as long as the segment.
    /// </summary>
    [Fact]
    public void EverySingingBehaviourResolvesToOneMidiSegment()
    {
        if (Library.Value is not { } lib || SingingConfigDir() is not { } cfg) return;
        var files = Directory.GetFiles(cfg, "*.json");
        Assert.Equal(39, files.Length);
        var leaves = new HashSet<uint>();
        foreach (var f in files)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            string group = doc.RootElement.GetProperty("audioSwitchGroup").GetString()!;
            string sw = doc.RootElement.GetProperty("audioSwitch").GetString()!;
            uint groupId = WwiseHash.Of(group), switchId = WwiseHash.Of(sw);
            string eventName = "Play__Robot_VO__Cozmo_Singing_" + group["Cozmo_Sings_".Length..].ToLowerInvariant();
            var eventId = lib.IdOf(eventName);
            Assert.True(eventId.HasValue, $"{f}: no event {eventName}");

            var plan = lib.ResolveMusic(eventId!.Value, new Dictionary<uint, uint> { [groupId] = switchId });
            Assert.True(plan.Problem is null, $"{Path.GetFileName(f)}: {plan.Problem}");
            var tree = plan.Switch!.Tree;
            var leaf = tree.FirstOrDefault(n => n.Key == switchId);
            Assert.True(leaf.Key == switchId, $"{Path.GetFileName(f)}: {sw} ({switchId}) is not a key in the {group} tree");
            Assert.Equal(leaf.AudioNodeId, plan.SelectedNodeId);
            leaves.Add(leaf.AudioNodeId);

            var seg = Assert.Single(plan.Segments);
            var clip = Assert.Single(seg.Clips);
            Assert.True(clip.IsMidi, $"{Path.GetFileName(f)}: clip source is plugin 0x{clip.PluginId:X}");
            Assert.Equal(seg.DurationMs, clip.Clip.LengthMs, 1);           // the segment is exactly the trimmed clip
            Assert.False(plan.HasRandomChoice);
            Assert.NotNull(lib.ReadMedia(clip.SourceId, out _));
        }
        Assert.Equal(39, leaves.Count);                     // every behaviour sings a different song
    }

    /// <summary>
    /// The MIDI timing model, checked against every MIDI track in Cozmo.bnk: the header division is 9600
    /// ticks per beat, and the source duration Wwise wrote into each clip equals the end-of-track tick
    /// count at that division and the effective hierarchy tempo (the nearest ancestor whose meter overrides
    /// its parent's). The file header's tempo does not fit, and neither does the segment's stored 120 bpm
    /// nor, on its own, the switch container's: the data decides between them.
    /// </summary>
    [Fact]
    public void SongMidiSourcesUseNineThousandSixHundredTicksPerBeatAtTheEffectiveHierarchyTempo()
    {
        if (Library.Value is not { } lib) return;
        int midiTracks = 0, headerDiffers = 0, playlistOverrides = 0, containerDecides = 0;
        foreach (var bank in lib.Banks)
            foreach (var o in bank.Objects.Values.Where(o => o.Type == WwiseObjectType.MusicTrack))
            {
                if (lib.Node(o.Id) is not WwiseMusicTrackNode track || !track.HasMidiSource) continue;
                midiTracks++;
                var clip = Assert.Single(track.Clips);
                var bytes = lib.ReadMedia(clip.SourceId, out _);
                Assert.NotNull(bytes);
                var midi = WwiseMidi.Parse(bytes!);
                Assert.Equal(9600, midi.TicksPerBeat);
                Assert.True(midi.Notes.Count >= 11, $"track {o.Id}: only {midi.Notes.Count} notes");

                var seg = Assert.IsType<WwiseMusicSegmentNode>(lib.Node(track.Params.ParentId));
                float tempo = WwiseMusic.EffectiveTempo(lib, seg.Id);
                Assert.Equal(midi.EndTick * midi.MsPerTick(tempo), clip.SourceDurationMs, 1);
                Assert.Equal(seg.DurationMs, clip.LengthMs, 1);
                Assert.False(seg.Meter.OverridesParent);                          // segments inherit
                if (Math.Abs(midi.FileTempoBpm - tempo) > 0.01) headerDiffers++;
                var pl = Assert.IsType<WwiseMusicPlaylistNode>(lib.Node(seg.Params.ParentId));
                if (pl.Meter.OverridesParent) playlistOverrides++;
                else if (lib.Node(pl.Params.ParentId) is WwiseMusicSwitchNode sw)
                {
                    Assert.True(sw.Meter.OverridesParent);
                    Assert.Equal(sw.Meter.TempoBpm, tempo);
                    containerDecides++;
                }
            }
        Assert.Equal(46, midiTracks);
        Assert.Equal(12, playlistOverrides);
        Assert.Equal(34, containerDecides);
        Assert.True(headerDiffers >= 9, $"only {headerDiffers} headers disagree with the duration, fewer than the data shows");
    }

    /// <summary>
    /// Where the notes go: the MIDI target is a blend container whose first child holds one random
    /// container per MIDI key from 48 to 61, each of three Vorbis recordings of Cozmo singing that note,
    /// whose second child plays at note-off (MidiPlayOnNoteType 2) 14 dB down, and which carries the
    /// vibrato LFO (Cozmo.txt: cozmo_singing_vibrato_lfo, 528935089) as an RTPC on pitch.
    /// </summary>
    [Fact]
    public void TheMidiTargetIsABlendOfPerNoteVocalSamples()
    {
        if (Library.Value is not { } lib) return;
        var target = Assert.IsType<WwiseBlendNode>(lib.Node(110896138));
        Assert.Equal(3, target.Children.Count);
        var vibrato = Assert.Single(target.Params.Rtpcs);
        Assert.Equal(528935089u, vibrato.SourceId);
        Assert.Equal((byte)WwiseProp.Pitch, vibrato.ParamId);

        // three children: the get-in sequences (a random container), the note-on blend, the note-off blend
        var blends = target.Children.Select(lib.Node).OfType<WwiseBlendNode>().ToList();
        Assert.Equal(2, blends.Count);
        Assert.Single(target.Children.Select(lib.Node).OfType<WwiseRandomSequenceNode>());
        var noteOff = Assert.Single(blends, b => b.Params.Raw(WwiseProp.MidiPlayOnNoteType) == 2u);
        var noteOn = Assert.Single(blends, b => b.Params.Raw(WwiseProp.MidiPlayOnNoteType) is null);
        Assert.Equal(-14f, noteOff.Params.Float(WwiseProp.Volume));

        var keys = new SortedSet<uint>();
        foreach (var cid in noteOn.Children)
        {
            var c = Assert.IsType<WwiseRandomSequenceNode>(lib.Node(cid));
            uint? min = c.Params.Raw(WwiseProp.MidiKeyRangeMin), max = c.Params.Raw(WwiseProp.MidiKeyRangeMax);
            Assert.Equal(min, max);
            Assert.True(min.HasValue);
            keys.Add(min!.Value);
            Assert.False(c.IsSequence);
            Assert.Equal(3, c.Children.Count);
            foreach (var sid in c.Children)
            {
                var s = Assert.IsType<WwiseSoundNode>(lib.Node(sid));
                Assert.Equal(0x00040001u, s.PluginId);               // Vorbis
                Assert.NotNull(lib.ReadMedia(s.MediaId, out _));
            }
        }
        Assert.Equal(Enumerable.Range(48, 14).Select(k => (uint)k), keys);
    }

    // ------------------------------------------------------------------ synthetic

    /// <summary>
    /// The decision tree rule, on a hand-built container: the child keyed by the group's current value is
    /// taken; a value with no key, or no value at all, takes the key-0 child; a tree without a key-0 child
    /// selects nothing for an unknown value.
    /// </summary>
    [Fact]
    public void ADecisionTreeTakesTheKeyedChildAndFallsBackToKeyZero()
    {
        const uint group = 77, keyed = 1234, a = 1000, b = 2000;
        var empty = new WwiseNodeParams(0, 0, 0, new Dictionary<byte, uint>(), new Dictionary<byte, (float, float)>(),
                                        Array.Empty<WwiseRtpc>(), Array.Empty<(uint, byte, IReadOnlyList<(uint, uint)>)>());
        var tree = new[]
        {
            new WwiseDecisionNode(0, 0x00020001, 1, 2, 50, 100),   // root: children at index 1, two of them
            new WwiseDecisionNode(0, a, 0, 0, 50, 100),
            new WwiseDecisionNode(keyed, b, 0, 0, 50, 100),
        };
        var sw = new WwiseMusicSwitchNode(1, "t", empty, new uint[] { a, b }, 0, new WwiseMeter(1000, 0, 120, 4, 4, true), true,
                                          new[] { (group, (byte)0) }, 0, tree);
        Assert.Equal(b, sw.Select(new Dictionary<uint, uint> { [group] = keyed }));
        Assert.Equal(a, sw.Select(new Dictionary<uint, uint>()));
        Assert.Equal(a, sw.Select(new Dictionary<uint, uint> { [group] = 999 }));

        var noDefault = sw with { Tree = new[] { tree[0] with { ChildCount = 1, ChildIndex = 2 }, tree[1], tree[2] } };
        Assert.Null(noDefault.Select(new Dictionary<uint, uint> { [group] = 999 }));
        Assert.Equal(b, noDefault.Select(new Dictionary<uint, uint> { [group] = keyed }));
    }

    /// <summary>A hand-built MIDI source: the six-byte header, then running status, a note, and end-of-track.</summary>
    [Fact]
    public void AMidiSourceParsesItsHeaderNotesAndEndOfTrack()
    {
        var blob = new byte[]
        {
            0x25, 0x80,                     // division 9600, big-endian
            0x00, 0x00, 0xC8, 0x42,         // tempo 100.0, little-endian float
            0x00, 0x90, 0x3C, 0x40,         // t=0: note on, key 60, velocity 64
            0x81, 0x40, 0x3C, 0x00,         // t=+192: running status, velocity 0 = note off
            0x00, 0xFF, 0x2F, 0x00,         // end of track
        };
        var midi = WwiseMidi.Parse(blob);
        Assert.Equal(9600, midi.TicksPerBeat);
        Assert.Equal(100f, midi.FileTempoBpm);
        Assert.Equal(192u, midi.EndTick);
        var note = Assert.Single(midi.Notes);
        Assert.Equal((0u, 192u, (byte)0, (byte)60, (byte)64), (note.StartTick, note.LengthTicks, note.Channel, note.Key, note.Velocity));
        var at120 = Assert.Single(midi.NotesAt(120f));
        Assert.Equal(0.0, at120.StartMs);
        Assert.Equal(192 * 60000.0 / 120 / 9600, at120.LengthMs, 9);   // 100 ms
    }

    [Fact]
    public void AMidiSourceWithABadDivisionIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => WwiseMidi.Parse(new byte[] { 0x00, 0x00, 0, 0, 0, 0, 0 }));
        Assert.Throws<InvalidDataException>(() => WwiseMidi.Parse(new byte[] { 0xE7, 0x28, 0, 0, 0, 0, 0 }));   // SMPTE division
    }
}
