namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// Property ids in a modulator's own property bundle. The numbering is not the one node properties use
/// (<see cref="WwiseProp"/>); it is a bundle of its own, and the shipped banks settle its shape:
///
/// Across all six banks the eleven modulator objects use id 0 on every one of them, ids 2..8 on the four
/// LFOs and nothing else, and ids 9..15 on the seven envelopes and nothing else. That is a clean split of
/// one shared id followed by seven per kind, which is exactly the count of parameters each kind has, and
/// it places <see cref="LfoSmoothing"/> (6) and <see cref="EnvelopeSustainTime"/> (13) on the two ids that
/// appear nowhere — the two parameters whose defaults every shipped modulator keeps. The values agree:
/// <c>cozmo_singing_vibrato_lfo</c> reads depth 100, attack 0.2, frequency 5.5, pulse width 50, and
/// <c>vo_external_french_stretch_envelope_short</c> reads attack 0.553 s, attack curve 31, decay 0.384 s,
/// sustain 25.3, release 1.0 s.
///
/// Id 1 appears on no shipped modulator, so whatever it holds is at its default everywhere.
/// </summary>
public enum WwiseModulatorProp : byte
{
    /// <summary>What the modulator is instanced per. Every shipped modulator carries 1.</summary>
    Scope = 0,
    /// <summary>Per cent. The one the singing vibrato binds a game parameter to.</summary>
    LfoDepth = 2,
    /// <summary>Seconds over which the depth ramps in when the modulator starts.</summary>
    LfoAttack = 3,
    /// <summary>Hertz.</summary>
    LfoFrequency = 4,
    LfoWaveform = 5,
    LfoSmoothing = 6,
    /// <summary>Per cent; 50 is a symmetric wave.</summary>
    LfoPulseWidth = 7,
    /// <summary>Degrees.</summary>
    LfoInitialPhase = 8,
    EnvelopeAttackTime = 9,
    EnvelopeAttackCurve = 10,
    EnvelopeDecayTime = 11,
    /// <summary>Per cent of the peak the envelope holds while the note is held.</summary>
    EnvelopeSustainLevel = 12,
    EnvelopeSustainTime = 13,
    EnvelopeReleaseTime = 14,
    /// <summary>Set only on <c>cozmo_singing_note_off</c>, where it reads 2. See <see cref="WwiseModulatorNode"/>.</summary>
    EnvelopeStopPlayback = 15,
}

/// <summary>
/// An LFO (HIRC type 21) or envelope (type 22) modulator, read from a bank.
///
/// The object is an id, a property bundle, a ranged-property bundle and an RTPC list — the same three
/// blocks a node carries, without the routing, parent and children a node has. All eleven shipped
/// modulators consume their payload exactly under this layout, which is what
/// <c>WwiseTests.EveryHierarchyObjectInTheShippedBanksConsumesExactly</c> checks.
///
/// A modulator produces a value while a note or an event lives; an RTPC on some other node names it as a
/// source (source type 2) and maps that value through a curve onto one of that node's properties. So the
/// modulator says *what shape*, and the binding on the target node says *what it does and how much*.
/// For the singing sampler there are two, and both bindings were read from the bank:
///
/// * <c>cozmo_singing_vibrato_lfo</c> (528935089) is bound to Pitch on the blend container the MIDI notes
///   go to, over a curve from 0 to 580 cents. Its own RTPC binds the game parameter
///   <c>cozmo_singing_vibrato</c> — the one <c>BehaviorSinging</c> posts from the cube shake — to its
///   depth, over a curve from 0 to 100 per cent. With no shake the depth is 0, so the LFO contributes
///   nothing at all; that is why a Cozmo that is not being shaken sings without vibrato.
/// * <c>cozmo_singing_note_off</c> (381606890) is bound to Volume on the note-on layer, over a curve from
///   0 to <b>-1 dB</b>. Whatever shape it produces, one decibel is the whole of its authority over the
///   level. Its times are all zero and its sustain level is 9.5.
///
/// Neither target node sets the property its modulator drives — the blend container sets no Pitch, the
/// note-on layer sets no Volume — so the question of how a bound value accumulates onto an existing
/// property value does not arise anywhere on the singing path.
/// </summary>
public sealed record WwiseModulatorNode(uint Id, WwiseObjectType Type, string Bank, WwiseNodeParams Params)
    : WwiseNode(Id, Type, Bank, Params, Array.Empty<uint>())
{
    public bool IsLfo => Type == WwiseObjectType.LfoModulator;

    /// <summary>A property as a float, or <paramref name="fallback"/> when the modulator does not set it.</summary>
    public float Value(WwiseModulatorProp p, float fallback) =>
        Params.Props.TryGetValue((byte)p, out var v) ? BitConverter.Int32BitsToSingle((int)v) : fallback;

    /// <summary>A property as the raw 32-bit word, or null when the modulator does not set it.</summary>
    public uint? Raw(WwiseModulatorProp p) => Params.Props.TryGetValue((byte)p, out var v) ? v : null;

    /// <summary>
    /// The value of this modulator at <paramref name="tSeconds"/> after the note started, for a note held
    /// <paramref name="heldSeconds"/>, with <paramref name="depth"/> as its depth in per cent (LFOs only;
    /// the depth may have been driven by a game parameter, which is why it is passed in rather than read).
    ///
    /// The result is in 0..1, the range both shipped bindings define their curves over, and it is 0 when
    /// the modulator can have no effect. <b>An LFO with zero depth returns 0 at every instant</b>, which is
    /// the only part of its output this package can establish: the shape Wwise gives an LFO between its
    /// extremes is in the Wwise runtime, which does not ship in the APK. See the fidelity manifest, M9-008.
    /// </summary>
    public double ValueAt(double tSeconds, double heldSeconds, double depth)
    {
        if (IsLfo)
        {
            if (depth <= 0) return 0;
            double attack = Value(WwiseModulatorProp.LfoAttack, 0);
            double ramp = attack > 0 ? Math.Min(1.0, tSeconds / attack) : 1.0;
            double f = Value(WwiseModulatorProp.LfoFrequency, 0);
            double phase = Value(WwiseModulatorProp.LfoInitialPhase, 0) / 360.0;
            double s = 0.5 * (1 + Math.Sin(2 * Math.PI * (f * tSeconds + phase)));
            return Math.Clamp(depth / 100.0, 0, 1) * ramp * s;
        }

        // Envelope: attack to 1, decay to the sustain level, hold, release to 0. Times in seconds,
        // sustain level in per cent. The attack curve (10) shapes the attack segment; with an attack time
        // of 0, as cozmo_singing_note_off has, it cannot be exercised and is not applied.
        double a = Value(WwiseModulatorProp.EnvelopeAttackTime, 0);
        double d = Value(WwiseModulatorProp.EnvelopeDecayTime, 0);
        double sustain = Math.Clamp(Value(WwiseModulatorProp.EnvelopeSustainLevel, 100) / 100.0, 0, 1);
        double sustainTime = Value(WwiseModulatorProp.EnvelopeSustainTime, 0);
        double r = Value(WwiseModulatorProp.EnvelopeReleaseTime, 0);

        double holdEnd = sustainTime > 0 ? Math.Min(heldSeconds, a + d + sustainTime) : heldSeconds;
        if (tSeconds < 0) return 0;
        if (tSeconds < a) return a > 0 ? tSeconds / a : 1;
        if (tSeconds < a + d) return d > 0 ? 1 - (1 - sustain) * (tSeconds - (a + d) + d) / d : sustain;
        if (tSeconds < holdEnd) return sustain;
        if (r <= 0) return 0;
        double since = tSeconds - holdEnd;
        return since >= r ? 0 : sustain * (1 - since / r);
    }
}
