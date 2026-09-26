using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The modulators the singing sampler is built on, recovered from the shipped banks (fidelity manifest
/// M9-006 to M9-009 and M9-012).
///
/// Each test checks a value or a consequence that was read out of the bank, not that something happened:
/// the property ids and values of the two singing modulators, the two bindings that say what they drive
/// and how far, the fact that the note-off envelope's whole authority over the level is one decibel, and
/// the fact that the vibrato is silent until a cube is shaken. The bit-vector test is the evidence behind
/// the claim that MIDI note tracking is off everywhere.
/// </summary>
public class WwiseModulatorTests
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

    /// <summary>The blend container the MIDI notes go to, and the two layers under it.</summary>
    private const uint MidiTarget = 110896138, NoteOnLayer = 462443456, NoteOffLayer = 774902407;
    private const uint VibratoLfo = 528935089, NoteOffEnvelope = 381606890;
    /// <summary>FNV-1 of <c>Cozmo_Singing_Vibrato</c>, the parameter BehaviorSinging posts (0x005EF0C8).</summary>
    private const uint VibratoParameter = 0xC20F49DF;

    /// <summary>
    /// The eleven modulator objects read, and the two the singing sampler uses read to their exact
    /// shipped values. <c>cozmo_singing_vibrato_lfo</c> is a 5.5 Hz shape with a 0.2 s attack; its depth
    /// property reads 100, which is its default and not what it runs at, because its own RTPC drives the
    /// depth. <c>cozmo_singing_note_off</c> has every time at zero and a sustain level of 9.5.
    /// </summary>
    [Fact]
    public void TheTwoSingingModulatorsReadOutOfTheBankAtTheirShippedValues()
    {
        if (Library.Value is not { } lib) return;

        var lfo = Assert.IsType<WwiseModulatorNode>(lib.Node(VibratoLfo));
        Assert.True(lfo.IsLfo);
        Assert.Equal(100f, lfo.Value(WwiseModulatorProp.LfoDepth, -1));
        Assert.Equal(0.2f, lfo.Value(WwiseModulatorProp.LfoAttack, -1), 5);
        Assert.Equal(5.5f, lfo.Value(WwiseModulatorProp.LfoFrequency, -1), 5);
        Assert.Equal(50f, lfo.Value(WwiseModulatorProp.LfoPulseWidth, -1));

        var env = Assert.IsType<WwiseModulatorNode>(lib.Node(NoteOffEnvelope));
        Assert.False(env.IsLfo);
        Assert.Equal(0f, env.Value(WwiseModulatorProp.EnvelopeAttackTime, -1));
        Assert.Equal(0f, env.Value(WwiseModulatorProp.EnvelopeDecayTime, -1));
        Assert.Equal(0f, env.Value(WwiseModulatorProp.EnvelopeReleaseTime, -1));
        Assert.Equal(9.5f, env.Value(WwiseModulatorProp.EnvelopeSustainLevel, -1), 5);
        // Set on this modulator and on no other of the eleven.
        Assert.Equal(2u, env.Raw(WwiseModulatorProp.EnvelopeStopPlayback));
    }

    /// <summary>
    /// The vibrato LFO's own RTPC: the game parameter BehaviorSinging posts drives the depth from 0 per
    /// cent at a value of 0 to 100 per cent at a value of 1. This is why a Cozmo that is not being shaken
    /// sings without vibrato — the depth, not the binding, is what the shake moves.
    /// </summary>
    [Fact]
    public void TheCubeShakeParameterDrivesTheVibratoDepthFromNothingToFull()
    {
        if (Library.Value is not { } lib) return;
        var lfo = Assert.IsType<WwiseModulatorNode>(lib.Node(VibratoLfo));

        var own = Assert.Single(lfo.Params.Rtpcs);
        Assert.Equal(VibratoParameter, own.SourceId);
        Assert.Equal(WwiseRtpc.GameParameterSource, own.SourceType);
        Assert.Equal(0u, own.ParamId);
        Assert.Equal(0.0, own.Evaluate(0, out _), 6);
        Assert.Equal(100.0, own.Evaluate(1, out _), 6);
        Assert.Equal(50.0, own.Evaluate(0.5, out _), 6);
    }

    /// <summary>
    /// The two bindings that say what the modulators do, and how far they can go: the LFO drives Pitch on
    /// the MIDI target over 0 to 580 cents, the envelope drives Volume on the note-on layer over 0 to
    /// -1 dB. Neither target node sets the property its modulator drives, so no question of how a bound
    /// value accumulates onto an existing one arises on this path.
    /// </summary>
    [Fact]
    public void TheBindingsBoundTheVibratoTo580CentsAndTheNoteOffEnvelopeToOneDecibel()
    {
        if (Library.Value is not { } lib) return;

        var target = lib.Node(MidiTarget)!;
        var pitch = Assert.Single(target.Params.Rtpcs);
        Assert.Equal(VibratoLfo, pitch.SourceId);
        Assert.Equal(WwiseRtpc.ModulatorSource, pitch.SourceType);
        Assert.Equal((uint)WwiseProp.Pitch, pitch.ParamId);
        Assert.Equal(0.0, pitch.Evaluate(0, out _), 6);
        Assert.Equal(580.0, pitch.Evaluate(1, out _), 6);
        Assert.Null(target.Params.Float(WwiseProp.Pitch));

        var noteOn = lib.Node(NoteOnLayer)!;
        var volume = Assert.Single(noteOn.Params.Rtpcs);
        Assert.Equal(NoteOffEnvelope, volume.SourceId);
        Assert.Equal(WwiseRtpc.ModulatorSource, volume.SourceType);
        Assert.Equal((uint)WwiseProp.Volume, volume.ParamId);
        Assert.Equal(0.0, volume.Evaluate(0, out _), 6);
        Assert.Equal(-1.0, volume.Evaluate(1, out _), 6);
        Assert.Null(noteOn.Params.Float(WwiseProp.Volume));

        // The note-off layer carries its own level instead, and plays at note-off rather than note-on.
        var noteOff = lib.Node(NoteOffLayer)!;
        Assert.Equal(-14f, noteOff.Params.Float(WwiseProp.Volume));
        Assert.Equal(2u, noteOff.Params.Raw(WwiseProp.MidiPlayOnNoteType));
    }

    /// <summary>
    /// A rendered song acts on the note-off envelope on every sung note, and the level change it makes is
    /// the 0.095 dB the shipped sustain level of 9.5 per cent asks for through a curve that reaches -1 dB
    /// at full output. The vibrato contributes nothing, because nothing is shaking a cube.
    ///
    /// This is the measurement behind the correction to WWISE_MUSIC.md: the note-off envelope was recorded
    /// there as the reason notes sustain without release shaping. It cannot be. One decibel is all it has.
    /// </summary>
    [Fact]
    public void TheNoteOffEnvelopeMovesTheLevelByATenthOfADecibelAndTheVibratoByNothing()
    {
        if (Library.Value is not { } lib) return;
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(1));
        var render = source.RenderMusic(WwiseHash.Of("Play__Robot_VO__Cozmo_Singing_80bpm"),
            new Dictionary<uint, uint> { [Cozmo.Robot.Behavior.SingingBehavior.Group80] = 0x852F201Au });

        Assert.Equal(render.NotesPlayed, render.ModulationsApplied);
        Assert.True(render.ModulationsApplied > 0, "the note-on layer binds the envelope, so every sung note carries it");
        Assert.InRange(render.ModulationPeakDb, -0.1, -0.09);
        Assert.Equal(0.0, render.ModulationPeakCents);
        Assert.Empty(render.Problems);
    }

    /// <summary>
    /// Shaking a cube is what makes the vibrato audible: with the parameter at 1 the depth is 100 per cent
    /// and the pitch swings by the binding's full 580 cents. The value posted is the one the engine's
    /// smoothing produces (<c>BehaviorSinging::UpdateInternal</c> 0x005EF0C8).
    /// </summary>
    [Fact]
    public void WithTheShakeParameterAtFullTheVibratoReachesTheBindingsFullRange()
    {
        if (Library.Value is not { } lib) return;
        using var source = new WwiseAudioSource(lib, ownsLibrary: false, random: new Random(1));
        source.SetParameter(VibratoParameter, 1f);
        var render = source.RenderMusic(WwiseHash.Of("Play__Robot_VO__Cozmo_Singing_80bpm"),
            new Dictionary<uint, uint> { [Cozmo.Robot.Behavior.SingingBehavior.Group80] = 0x852F201Au });

        Assert.True(render.ModulationPeakCents > 500,
            $"the vibrato should reach most of its 580 cents, reached {render.ModulationPeakCents:F0}");
        Assert.True(render.ModulationPeakCents <= 580.0001);
    }

    /// <summary>
    /// The evidence for "MIDI note tracking is off everywhere". Across all six banks the node bit vector
    /// takes only three values: nothing, bit 0 on exactly the nodes that set the Priority property, and
    /// bits 2 and 5 together on exactly the singing sampler's two note layers. No node anywhere sets a
    /// tracking root note. So there is no node in any shipped bank on which a note-tracking enable bit
    /// could be set, whichever bit it is.
    /// </summary>
    [Fact]
    public void NoNodeInAnyShippedBankCouldHaveMidiNoteTrackingEnabled()
    {
        if (Library.Value is not { } lib) return;

        var withBit0 = new List<uint>();
        var with0x24 = new List<uint>();
        var withRootNote = new List<uint>();
        var seen = new HashSet<byte>();

        foreach (uint id in lib.AllNodeIds)
        {
            if (lib.Node(id) is not { } n) continue;
            if (n is WwiseModulatorNode) continue;                        // modulators carry no node block
            byte bits = n.Params.Bits;
            seen.Add(bits);
            if (bits == 0x01) withBit0.Add(id);
            if (bits == 0x24) with0x24.Add(id);
            if (n.Params.Raw(WwiseProp.MidiTrackingRootNote) is not null) withRootNote.Add(id);
            if (bits == 0x01)
                Assert.True(n.Params.Float(WwiseProp.Priority) is not null,
                    $"node {id} carries bit 0 without setting Priority, which breaks the reading of that bit");
        }

        Assert.Equal(new byte[] { 0x00, 0x01, 0x24 }, seen.OrderBy(b => b).ToArray());
        Assert.Equal(3, withBit0.Count);
        Assert.Equal(new[] { NoteOnLayer, NoteOffLayer }, with0x24.OrderBy(x => x).ToArray());
        Assert.Empty(withRootNote);
    }
}
