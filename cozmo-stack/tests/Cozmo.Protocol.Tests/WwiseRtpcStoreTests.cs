using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-009's value store: the lookup precedence with the STMG default at <c>entry+8</c>, the empty-key bus
/// lookup, the sum/product accumulation, the set path and its ramp (gapF 1.1–1.10, gapE 7.1–7.4,
/// gapA 5.1–5.4).
///
/// The oracles are the inventory's: the event_volume curve (points (0, −1, Sine) → (1, 0), scaling 2, so
/// 1 → 0 dB, 0.5 → −3.01 dB, 0 → −764.6 dB, gapA 5.5) and the robot_volume curve (points (0, −200, Exp1)
/// → (1, 0), scaling 0, so 1 → 0 dB, 0 → −200 dB, gapA 5.6). The STMG defaults are 1.0 for both (gapF 1.3).
/// </summary>
public class WwiseRtpcStoreTests
{
    private const uint EventVolume = 0xD2687048;
    private const uint RobotVolume = 0x637C1240;

    private static WwiseStmgParam Param(uint id, float value, uint rampType = 0, float up = 0, float down = 0) =>
        new(id, value, rampType, up, down, false);

    private static WwiseRtpcStore CozmoStore() => new(new[]
    {
        Param(EventVolume, 1.0f),
        Param(RobotVolume, 1.0f),
    });

    /// <summary>event_volume 0xD2687048: (0, −1, Sine) → (1, 0), scaling 2 (gapA 5.5).</summary>
    private static WwiseRtpc EventVolumeCurve() =>
        new(EventVolume, WwiseRtpc.GameParameterSource, 1, 0, 0, 2, new[] { (0f, -1f, 1u), (1f, 0f, 4u) });

    /// <summary>robot_volume 0x637C1240: (0, −200, Exp1) → (1, 0), scaling 0 (gapA 5.6).</summary>
    private static WwiseRtpc RobotVolumeCurve() =>
        new(RobotVolume, WwiseRtpc.GameParameterSource, 1, 5, 0, 0, new[] { (0f, -200f, 6u), (1f, 0f, 4u) });

    private static WwiseRtpc LinearCurve(uint id, byte accumulate = 1) =>
        new(id, WwiseRtpc.GameParameterSource, accumulate, 0, 0, 0, new[] { (0f, 0f, 4u), (1f, 1f, 4u) });

    // ------------------------------------------------------------------ lookup precedence

    /// <summary>
    /// gapF 1.6/1.7/1.8: event_volume set per playing id applies to that voice; a different voice under the
    /// same game object falls through to the STMG default 1.0 and evaluates to 0 dB, not silence.
    /// </summary>
    [Fact]
    public void ThePlayingIdValueAppliesToItsVoiceAndOthersUseTheStmgDefault()
    {
        var store = CozmoStore();
        store.RegisterPlayingId(1001, 7);

        var set = store.SetParameterWithPlayingId(EventVolume, 0.5f, 1001);
        Assert.Equal(WwiseRtpcSetKind.Immediate, set.Kind);
        Assert.Equal(0.5f, set.NewValue);

        var curve = EventVolumeCurve();

        var own = store.Lookup(EventVolume, 7, 1001);
        Assert.Equal(WwiseRtpcValueKind.Stored, own.Kind);
        Assert.Equal(-3.01, curve.EvaluateScaled(own.Value, out _), 2);      // 0.5 → −3.01 dB (gapA 5.5)

        var other = store.Lookup(EventVolume, 7, 2002);
        Assert.Equal(WwiseRtpcValueKind.StmgDefault, other.Kind);
        Assert.Equal(1.0f, other.Value);                                     // gapF 1.3
        Assert.Equal(0.0, curve.EvaluateScaled(other.Value, out _), 3);      // 1.0 → 0 dB, not −764 dB
    }

    /// <summary>
    /// gapF 1.9: robot_volume set on game object 7 marks only element 7 valid; the bus evaluates with the
    /// empty key {0, 0}, which reaches the root only, so it reads the STMG default 1.0 → 0 dB whatever was set.
    /// </summary>
    [Fact]
    public void AGameObjectValueIsNotVisibleToTheBusEmptyKey()
    {
        var store = CozmoStore();
        var set = store.SetParameter(RobotVolume, 0.0f, 7);
        Assert.Equal(WwiseRtpcSetKind.Immediate, set.Kind);

        var onObject = store.Lookup(RobotVolume, 7, 0);
        Assert.Equal(WwiseRtpcValueKind.Stored, onObject.Kind);
        Assert.Equal(0.0f, onObject.Value);

        var bus = store.Lookup(RobotVolume, 0, 0);                           // the bus key
        Assert.Equal(WwiseRtpcValueKind.StmgDefault, bus.Kind);
        Assert.Equal(1.0f, bus.Value);

        var curve = RobotVolumeCurve();
        Assert.Equal(0.0, curve.EvaluateScaled(bus.Value, out _), 3);        // 1.0 → 0 dB
        Assert.Equal(-200.0, curve.EvaluateScaled(0.0, out _), 3);           // 0.0 would be −200 dB
    }

    /// <summary>gapF 1.4/1.9: a root set (game object 0, empty key) is what a lookup with no valid element reaches.</summary>
    [Fact]
    public void ARootValueIsUsedWhenNoGameObjectSlotIsValid()
    {
        var store = CozmoStore();
        store.SetParameter(EventVolume, 0.25f, 0);

        var lookup = store.Lookup(EventVolume, 7, 0);
        Assert.Equal(WwiseRtpcValueKind.Stored, lookup.Kind);
        Assert.Equal(0.25f, lookup.Value);
    }

    /// <summary>gapF 1.8/1.10: an id not in the store is surfaced, never defaulted.</summary>
    [Fact]
    public void AnIdNotInTheStoreIsSurfacedRatherThanDefaulted()
    {
        var store = CozmoStore();
        var lookup = store.Lookup(0xDEADBEEF, 7, 0);
        Assert.Equal(WwiseRtpcValueKind.NotInStore, lookup.Kind);
        Assert.False(lookup.Found);

        Assert.Throws<NotSupportedException>(() =>
            store.Evaluate(1, new[] { LinearCurve(0xDEADBEEF) }, 7, 0, out _));
    }

    // ------------------------------------------------------------------ accumulation

    /// <summary>gapA 5.4 / gapE 7.2: acc 2 is the product (start 1.0), anything else the sum (start 0.0).</summary>
    [Fact]
    public void AccumulationSumsOrMultipliesByTheBankByte()
    {
        Assert.Equal(5.0, WwiseRtpcStore.Accumulate(1, new[] { 2.0, 3.0 }), 6);
        Assert.Equal(6.0, WwiseRtpcStore.Accumulate(2, new[] { 2.0, 3.0 }), 6);

        var store = new WwiseRtpcStore(new[] { Param(0x1111, 0f), Param(0x2222, 0f) });
        store.SetParameter(0x1111, 0.25f, 7);
        store.SetParameter(0x2222, 0.5f, 7);

        var a = LinearCurve(0x1111);
        var b = LinearCurve(0x2222);
        Assert.Equal(0.75, store.Evaluate(1, new[] { a, b }, 7, 0, out _), 6);
        Assert.Equal(0.125, store.Evaluate(2, new[] { a, b }, 7, 0, out _), 6);

        // the convenience overload takes the accumulate byte from the first binding (gapA 5.4)
        Assert.Equal(0.125, store.Evaluate(new[] { a with { Accumulate = 2 }, b }, 7, 0, out _), 6);
    }

    // ------------------------------------------------------------------ ramps

    /// <summary>
    /// gapF 1.5: event_volume and robot_volume have ramp 0 and Anki passes time 0 (gapA 5.2), so both set
    /// immediately with duration 0.
    /// </summary>
    [Fact]
    public void TheCozmoParamsApplyImmediatelyBecauseTheirRampIsZero()
    {
        var store = CozmoStore();

        var e = store.SetParameter(EventVolume, 0.5f, 7);
        Assert.Equal(WwiseRtpcSetKind.Immediate, e.Kind);
        Assert.Equal(0.0, e.DurationMs);
        Assert.False(e.InterpolatedMessage);

        var r = store.SetParameter(RobotVolume, 0.5f, 7);
        Assert.Equal(WwiseRtpcSetKind.Immediate, r.Kind);
        Assert.Equal(0.0, r.DurationMs);

        var direct = store.SetRtpcs(RobotVolume, 0.25f, 7, 0, 0, 0, bypass: false);
        Assert.Equal(WwiseRtpcSetKind.Immediate, direct.Kind);
        Assert.Equal(0.0, direct.DurationMs);
        Assert.False(direct.InterpolatedMessage);
    }

    /// <summary>
    /// gapF 1.5 / correction C2: a type-1 ramp's duration is |Δ| / rate · 1000 ms, rate = up when new &gt;
    /// old, down otherwise. Each set establishes a stored old value first so the 0xA13948 gate is bypassed
    /// (<c>0xA13948 cmp r7,#0</c>) and the transition is the ordinary one.
    /// </summary>
    [Fact]
    public void ATypeOneRampDurationIsTheDeltaOverTheRate()
    {
        const uint id = 0x0A0A;

        var down = new WwiseRtpcStore(new[] { Param(id, 1.0f, rampType: 1, up: 2.0f, down: 0.25f) });
        down.SetParameter(id, 1.0f, 7);                                      // establish the old value
        var downSet = down.SetParameter(id, 0.5f, 7);                        // Δ = −0.5, down 0.25
        Assert.Equal(WwiseRtpcSetKind.Transition, downSet.Kind);
        Assert.Equal(2000.0, downSet.DurationMs, 6);

        var up = new WwiseRtpcStore(new[] { Param(id, 1.0f, rampType: 1, up: 2.0f, down: 0.25f) });
        up.SetParameter(id, 1.0f, 7);
        var upSet = up.SetParameter(id, 2.0f, 7);                            // Δ = +1.0, up 2.0
        Assert.Equal(500.0, upSet.DurationMs, 6);
    }

    /// <summary>
    /// gapF 1.5 / 0xA139E8/0xA139F4: a type-1 ramp whose applicable rate is 0.0 skips the whole
    /// subtract/divide/multiply, so the duration is max(0, caller time) — immediate at caller time 0,
    /// not an exception.
    /// </summary>
    [Fact]
    public void ATypeOneRampWithAZeroRateContributesNoDuration()
    {
        const uint id = 0x0A0A;

        var immediate = new WwiseRtpcStore(new[] { Param(id, 1.0f, rampType: 1, up: 2.0f, down: 0.0f) })
            .SetParameter(id, 0.5f, 7);                                      // Δ = −0.5, applicable rate down = 0.0
        Assert.Equal(WwiseRtpcSetKind.Immediate, immediate.Kind);
        Assert.Equal(0.0, immediate.DurationMs);

        var caller = new WwiseRtpcStore(new[] { Param(id, 1.0f, rampType: 1, up: 2.0f, down: 0.0f) });
        caller.SetParameter(id, 1.0f, 7);                                    // establish the old value
        var callerSet = caller.SetRtpcs(id, 0.5f, 7, 0, timeMs: 300, curve: 0, bypass: false);
        Assert.Equal(WwiseRtpcSetKind.Transition, callerSet.Kind);
        Assert.Equal(300.0, callerSet.DurationMs, 6);

        // A type-2 zero rate is 0.0 × 1000 = 0 (gapF 1.5).
        var typeTwo = new WwiseRtpcStore(new[] { Param(id, 1.0f, rampType: 2, up: 0.0f, down: 0.0f) })
            .SetParameter(id, 0.5f, 7);
        Assert.Equal(WwiseRtpcSetKind.Immediate, typeTwo.Kind);
        Assert.Equal(0.0, typeTwo.DurationMs);

        // vcvt.s32.f32 truncates toward zero: 0.5 / 0.3 · 1000 = 1666.67 → 1666 (0xA13A04/0xA13A40).
        var truncated = new WwiseRtpcStore(new[] { Param(id, 1.0f, rampType: 1, up: 0.3f, down: 0.3f) })
            .SetParameter(id, 0.5f, 7);
        Assert.Equal(1666.0, truncated.DurationMs, 6);
    }

    /// <summary>gapF 1.5: a type-2 ramp's duration is the up/down field × 1000 ms.</summary>
    [Fact]
    public void ATypeTwoRampDurationIsTheUpOrDownTime()
    {
        const uint id = 0x0B0B;
        var store = new WwiseRtpcStore(new[] { Param(id, 0f, rampType: 2, up: 2.0f, down: 3.0f) });
        store.SetParameter(id, 0f, 7);                                       // establish the old value

        var up = store.SetParameter(id, 1.0f, 7);                            // Δ = +1.0, up 2.0 s
        Assert.Equal(WwiseRtpcSetKind.Transition, up.Kind);
        Assert.Equal(2000.0, up.DurationMs, 6);

        var down = store.SetParameter(id, 0.0f, 7);                          // Δ = −1.0, down 3.0 s
        Assert.Equal(3000.0, down.DurationMs, 6);
    }

    /// <summary>
    /// Correction C2: Δ = 0 takes the down branch (type 2 <c>0xA13A34 vldrgt up / vldrle down</c>), which
    /// is observable only when up ≠ down: up 1.0 s / down 5.0 s at Δ = 0 → 5000 ms, not 1000.
    /// </summary>
    [Fact]
    public void TheUpDownSelectionAtZeroDeltaIsDown()
    {
        const uint id = 0x0B0C;
        var store = new WwiseRtpcStore(new[] { Param(id, 1.0f, rampType: 2, up: 1.0f, down: 5.0f) });

        var atZero = store.SetParameter(id, 1.0f, 7);                        // Δ = 0, down 5.0 s
        Assert.Equal(5000.0, atZero.DurationMs, 6);
    }

    /// <summary>gapF 1.5: duration = max(caller time, entry ramp), and a non-zero caller time selects the interpolated message (gapA 5.1).</summary>
    [Fact]
    public void TheCallerTimeIsTheFloorOfTheDuration()
    {
        const uint id = 0x0C0C;

        var caller = new WwiseRtpcStore(new[] { Param(id, 0f) });
        caller.SetParameter(id, 0f, 7);                                      // establish the old value
        var callerSet = caller.SetRtpcs(id, 1.0f, 7, 0, timeMs: 250, curve: 0, bypass: false);
        Assert.Equal(WwiseRtpcSetKind.Transition, callerSet.Kind);
        Assert.Equal(250.0, callerSet.DurationMs, 6);
        Assert.True(callerSet.InterpolatedMessage);

        var entryWins = new WwiseRtpcStore(new[] { Param(id, 0f, rampType: 2, up: 0.5f, down: 0.5f) });
        entryWins.SetParameter(id, 0f, 7);
        var entryWinsSet = entryWins.SetRtpcs(id, 1.0f, 7, 0, timeMs: 100, curve: 0, bypass: false);
        Assert.Equal(500.0, entryWinsSet.DurationMs, 6);
    }

    /// <summary>
    /// Correction C2: the explicit-time byte (<c>0xA139A0..0xA139A8</c>) skips the ramp entirely and makes
    /// the duration the caller time (<c>0xA1393C</c>), not max(ramp, time).
    /// </summary>
    [Fact]
    public void TheExplicitTimeGateReplacesTheRampWithTheCallerTime()
    {
        const uint id = 0x0D0D;

        // Without the byte: Δ = +1.0, up 0.25 → 4000 ms entry ramp.
        var ramp = new WwiseRtpcStore(new[] { Param(id, 0f, rampType: 1, up: 0.25f, down: 0.25f) })
            .SetRtpcs(id, 1.0f, 7, 0, timeMs: 100, curve: 0, bypass: false);
        Assert.Equal(4000.0, ramp.DurationMs, 6);

        // With the byte: the ramp is skipped and the duration is the caller time.
        var explicitTime = new WwiseRtpcStore(new[] { Param(id, 0f, rampType: 1, up: 0.25f, down: 0.25f) })
            .SetRtpcs(id, 1.0f, 7, 0, timeMs: 100, curve: 0, bypass: false, explicitTime: true);
        Assert.Equal(100.0, explicitTime.DurationMs, 6);
    }

    /// <summary>
    /// Correction C2: a positive duration with no stored old value reaches the unmodelled
    /// <c>0xA1B5FC</c> transition gate, so the store reports <see cref="WwiseRtpcSetKind.GatedTransition"/>
    /// rather than claiming a transition; once an old value is stored the gate is bypassed.
    /// </summary>
    [Fact]
    public void APositiveDurationWithoutAPriorValueIsGatedNotClaimed()
    {
        const uint id = 0x0E0E;
        var store = new WwiseRtpcStore(new[] { Param(id, 0f, rampType: 2, up: 1.0f, down: 1.0f) });

        var gated = store.SetParameter(id, 1.0f, 7);                         // Δ = +1.0, no stored old value
        Assert.Equal(WwiseRtpcSetKind.GatedTransition, gated.Kind);
        Assert.Equal(1000.0, gated.DurationMs, 6);

        var bypassed = store.SetParameter(id, 2.0f, 7);                      // now the old value is stored
        Assert.Equal(WwiseRtpcSetKind.Transition, bypassed.Kind);
    }

    /// <summary>
    /// Correction C2 / 0xA13948: the gate input is the exact written slot's valid flag, not the old-value
    /// lookup. A valid ancestor (the root) still supplies the ramp's old value, but a new element-7 slot
    /// has r7 = 0, so the set is gated; once element 7 itself is valid the gate is bypassed. This is the
    /// case the native reaches with a value on a parent/subscriber but none on the written key.
    /// </summary>
    [Fact]
    public void TheGateSeesOnlyTheExactWrittenSlotNotAnAncestor()
    {
        const uint id = 0x0F0F;
        var store = new WwiseRtpcStore(new[] { Param(id, 0f, rampType: 2, up: 1.0f, down: 1.0f) });

        store.SetParameter(id, 0.5f, 0);                                     // root valid (gameObj 0, empty key)
        Assert.Equal(0.5f, store.Lookup(id, 0, 0).Value);

        // element 7 is a new slot; the ancestor root holds 0.5, which is the ramp's old value.
        var gated = store.SetParameter(id, 1.0f, 7);                         // type 2: up 1.0 s = 1000 ms
        Assert.Equal(WwiseRtpcSetKind.GatedTransition, gated.Kind);
        Assert.Equal(1000.0, gated.DurationMs, 6);
        Assert.Equal(0.5f, gated.OldValue);

        var again = store.SetParameter(id, 2.0f, 7);                         // element 7 is now the written slot
        Assert.Equal(WwiseRtpcSetKind.Transition, again.Kind);
    }

    // ------------------------------------------------------------------ entry points

    /// <summary>gapA 5.1: SetRTPCValueByPlayingID with an unknown playing id is error 0x1F and sets nothing.</summary>
    [Fact]
    public void AnUnknownPlayingIdSetsNothing()
    {
        var store = CozmoStore();
        var result = store.SetParameterWithPlayingId(EventVolume, 0.5f, 999);
        Assert.Equal(WwiseRtpcSetKind.UnknownPlayingId, result.Kind);
        Assert.Equal(WwiseRtpcValueKind.StmgDefault, store.Lookup(EventVolume, 7, 999).Kind);
    }
}
