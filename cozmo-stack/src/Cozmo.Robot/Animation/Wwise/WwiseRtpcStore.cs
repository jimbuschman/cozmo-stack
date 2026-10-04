// fidelity: M6-009

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>How a value-store lookup resolved (gapF 1.6–1.9).</summary>
public enum WwiseRtpcValueKind
{
    /// <summary>A valid slot held the value.</summary>
    Stored,

    /// <summary>No slot was valid, so the entry's STMG default (<c>entry+8</c>) is the value (gapF 1.6, 1.8).</summary>
    StmgDefault,

    /// <summary>
    /// The RTPC id is not in the store. <c>0xA17280</c> branches on the subscription type and parameter (C34.2, <c>0xA172EC..0xA17330</c>, <c>0xA17410..0xA17428</c>): type 1, or a parameter other than 0 and 7, returns the result of
    /// <c>0x9E6748</c> (unread); type other than 1 with parameter 0 or 7 returns 1 with the skip flag set and the value 1.0f (the subscription's curve then adds nothing). <see cref="WwiseRtpcStore.A11590"/> follows that flow.
    /// </summary>
    NotInStore,
}

/// <summary>The value that feeds an RTPC curve, and where the store got it (gapF 1.6–1.9).</summary>
public readonly record struct WwiseRtpcValue(WwiseRtpcValueKind Kind, float Value)
{
    /// <summary>Whether the id is in the store at all; false is the not-in-store branch, not a value.</summary>
    public bool Found => Kind != WwiseRtpcValueKind.NotInStore;
}

/// <summary>What a set did (gapF 1.5, gapA 5.1, correction C2).</summary>
public enum WwiseRtpcSetKind
{
    /// <summary>The resulting duration was 0, so the value applies at once (gapF 1.5).</summary>
    Immediate,

    /// <summary>
    /// The resulting duration was positive and the old-value lookup found a stored value, so the
    /// <c>0xA13948 cmp r7,#0</c> bypass applies and the native takes the transition (<c>0xA0E5E4</c>).
    /// </summary>
    Transition,

    /// <summary>
    /// A positive duration whose transition depends on the native <c>0xA1B5FC(entry.id, key)</c> gate
    /// (<c>0xA13948..0xA13A24</c>): the exact written slot had no valid prior value, so <c>r7</c> is 0
    /// even when a less-specific ancestor held the old value (<c>0xA13F74</c>, <c>0xA13F88</c>,
    /// <c>0xA13E0C/0xA13E14</c>). The shipped set path (<c>0xA1404C</c>, arg1 == 0) reaches it. C2 reads
    /// only part of what <c>0xA1B5FC</c> means, so this store does not claim a transition.
    /// </summary>
    GatedTransition,

    /// <summary>SetRTPCValueByPlayingID with an unknown playing id is error 0x1F and sets nothing (gapA 5.1).</summary>
    UnknownPlayingId,
}

/// <summary>
/// The outcome of one set. <see cref="DurationMs"/> is the caller time when the explicit-time byte was set
/// (<c>0xA139A0..0xA139A8</c>), otherwise <c>max(caller time, entry ramp)</c> (gapF 1.5). <see cref="Kind"/>
/// is <see cref="WwiseRtpcSetKind.Immediate"/> for a non-positive duration, otherwise the transition or the
/// unmodelled-gate outcome above. <see cref="InterpolatedMessage"/> records gapA 5.1's message selection —
/// a type-3 message when the caller time is non-zero or bypass is set — which is separate from the outcome.
/// </summary>
public readonly record struct WwiseRtpcSetResult(
    WwiseRtpcSetKind Kind, double DurationMs, float OldValue, float NewValue, byte Curve, bool InterpolatedMessage);

/// <summary>
/// The Wwise 2016.2 RTPC value store (M6-009): the per-entry tree of scoped values, the lookup precedence,
/// the STMG defaults at <c>entry+8</c>, the set path and its ramp, and the sum/product accumulation.
///
/// <para><b>Rows implemented.</b> Entry layout gapF 1.1; STMG defaults gapF 1.2–1.3; set path gapF 1.4;
/// ramp gapF 1.5; lookup gapF 1.6–1.9; accumulation gapA 5.4 / gapE 7.2; the entry points and message
/// selection gapA 5.1–5.2. The curve shapes and post-curve scaling are already in
/// <see cref="WwiseRtpc"/> (the M6-009 curve part).</para>
///
/// <para><b>Deliberately not modelled, and visible rather than defaulted.</b>
/// <list type="bullet">
/// <item><b>The 0xA1B5FC transition gate (correction C2, 0xA13948..0xA13A24).</b> When the exact written
/// slot had no valid prior value, a positive duration takes the transition only if
/// <c>0xA1B5FC(entry.id, key)</c> returns 1. <c>r7</c> is that exact slot's value pointer
/// (<c>0xA13D10</c>/<c>0xA13E5C</c>/<c>0xA13E04</c>), zeroed when it is invalid even though a
/// less-specific ancestor held a value (<c>0xA13F74</c>, <c>0xA13F88</c>, <c>0xA13E0C/0xA13E14</c>); the
/// ancestor value is still the ramp's old value, but the gate sees 0. C2 reads only part of what that
/// call means (it asks whether it is "this game object has an RTPC subscription"), so this store does not
/// guess it: such a set returns <see cref="WwiseRtpcSetKind.GatedTransition"/> instead of claiming a
/// transition. A set whose exact written slot already held a value bypasses the gate
/// (<c>0xA13948 cmp r7,#0</c>) and is an ordinary <see cref="WwiseRtpcSetKind.Transition"/>.</item>
/// <item><b>Not in the store (gapF 1.8, 1.10, 0xA172EC..0xA17304).</b> The native branches on the
/// subscription type: <c>type == 1</c> reads a different manager at <c>0x9E6748</c> (whose identity the
/// rows leave unlabelled), and <c>type != 1</c> gives param 0 or 7 1.0 with a skip flag and otherwise
/// nothing. Both are unreachable for any STMG-listed RTPC. A lookup for an id not in the store returns
/// <see cref="WwiseRtpcValueKind.NotInStore"/>, and <see cref="Evaluate"/> throws for it.</item>
/// <item><b>The transition's value evolution (gapF 1.5).</b> The rows give the duration and that
/// <c>0xA0E5E4</c> is created, not how the value moves across it. The set applies the target value to
/// the slot and reports the duration; it does not interpolate over time.</item>
/// <item><b>WwiseStmgParam.BuiltIn (C2, 0xA0F678).</b> The STMG parameter's built-in flag is dropped:
/// the registration its semantics depend on is UNKNOWN.</item>
/// </list></para>
///
/// <para><b>Ramp duration (0xA137D8..0xA13A60, gapF 1.5, correction C2).</b> Up is chosen when
/// <c>new &gt; old</c>, else down (type 1 <c>0xA139E0</c> → <c>ble 0xA13A4C</c>; type 2
/// <c>0xA13A34 vldrgt up / vldrle down</c>). The computed entry duration is converted with
/// <c>vcvt.s32.f32</c> (0xA13A04/0xA13A40), i.e. truncated toward zero, and the result is the signed
/// <c>max(computed, callerTime)</c> (0xA139BC..0xA139C8). A zero applicable rate for a type-1 ramp skips
/// the whole subtract/divide/multiply (0xA139E8/0xA139F4, 0xA13A50/0xA13A58), so the computed duration
/// stays 0 — no infinity, no NaN, and no forced-immediate. A non-zero explicit-time byte
/// (<c>0xA139A0..0xA139A8</c>) skips the ramp entirely and makes the duration the caller time
/// (<c>0xA1393C</c>); the shipped set path passes 0.</para>
///
/// <para><b>Seam.</b> <see cref="RegisterPlayingId"/> stands in for the native playing-id → game-object
/// map (<c>0xA05044</c>, populated by the callback manager, M6-006 gapA 1.2), which
/// <see cref="SetParameterWithPlayingId"/> resolves through. This store is not wired into production.</para>
/// </summary>
public sealed class WwiseRtpcStore
{
    private readonly Dictionary<uint, Entry> _entries = new();
    private readonly Dictionary<uint, uint> _playingObjects = new();

    /// <summary>An empty store: every lookup is <see cref="WwiseRtpcValueKind.NotInStore"/> until a set or a seed.</summary>
    public WwiseRtpcStore()
    {
    }

    /// <summary>
    /// Seeds the store from the STMG parameter table (gapF 1.2): each listed id gets an entry whose
    /// default is the table value (<c>entry+8</c>) and whose ramp is the table's type/up/down.
    /// </summary>
    public WwiseRtpcStore(WwiseStmg stmg) : this()
    {
        ArgumentNullException.ThrowIfNull(stmg);
        ApplyStmg(stmg);
    }

    /// <summary>Seeds the store from an explicit parameter list; the same write as gapF 1.2.</summary>
    public WwiseRtpcStore(IEnumerable<WwiseStmgParam> parameters) : this()
    {
        ArgumentNullException.ThrowIfNull(parameters);
        foreach (var p in parameters) Apply(p);
    }

    /// <summary>Applies the STMG parameter table to the store (gapF 1.2). Later writes win.</summary>
    public void ApplyStmg(WwiseStmg stmg)
    {
        ArgumentNullException.ThrowIfNull(stmg);
        foreach (var p in stmg.Params.Values) Apply(p);
    }

    /// <summary>
    /// Applies one STMG parameter: get-or-create, default at <c>+8</c>, ramp at <c>+0xC..+0x14</c>
    /// (gapF 1.2). <c>BuiltIn</c> (the <c>0xA0F678</c> registration, C2) is dropped: its semantics are
    /// UNKNOWN.
    /// </summary>
    public void Apply(WwiseStmgParam parameter)
    {
        var e = GetOrCreate(parameter.Id);
        e.Default = parameter.Value;      // +8
        e.RampType = parameter.RampType;  // +0xC
        e.RampUp = parameter.RampUp;      // +0x10
        e.RampDown = parameter.RampDown;  // +0x14
    }

    /// <summary>Whether an entry exists for the id (a set creates one with default 0, gapF 1.1).</summary>
    public bool Contains(uint rtpcId) => _entries.ContainsKey(rtpcId);

    /// <summary>The STMG default for an id, when the entry exists (gapF 1.2).</summary>
    public bool TryGetDefault(uint rtpcId, out float value)
    {
        if (_entries.TryGetValue(rtpcId, out var e)) { value = e.Default; return true; }
        value = 0;
        return false;
    }

    // ---------------------------------------------------------------- the playing-id seam

    /// <summary>
    /// Registers a playing id's game object, the map <c>0xA05044</c> holds natively (gapA 5.1). A seam:
    /// the native map is populated by the callback manager (M6-006, gapA 1.2), which this store has no
    /// other way to reach.
    /// </summary>
    public void RegisterPlayingId(uint playingId, uint gameObject) => _playingObjects[playingId] = gameObject;

    /// <summary>Removes a playing id from the seam; <see cref="SetParameterWithPlayingId"/> then reports 0x1F.</summary>
    public bool UnregisterPlayingId(uint playingId) => _playingObjects.Remove(playingId);

    /// <summary>The game object a registered playing id resolves to, or null (gapA 5.1).</summary>
    public uint? GameObjectOfPlayingId(uint playingId) =>
        _playingObjects.TryGetValue(playingId, out uint g) ? g : null;

    // ---------------------------------------------------------------- the set path

    /// <summary>
    /// Anki's SetParameter (robot_volume): time 0, curve 0 (gapA 5.2). With the Cozmo params' ramp 0 that
    /// is the immediate type-2 path (gapA 5.1, gapF 1.5).
    /// </summary>
    public WwiseRtpcSetResult SetParameter(uint rtpcId, float value, uint gameObject) =>
        SetRtpcs(rtpcId, value, gameObject, 0, 0, 0, bypass: false);

    /// <summary>
    /// Anki's SetCozmoEventParameter (event_volume): the playing id resolves its game object through the
    /// seam, then time 0, curve 0 (gapA 5.1–5.2). An unknown playing id is error 0x1F.
    /// </summary>
    public WwiseRtpcSetResult SetParameterWithPlayingId(uint rtpcId, float value, uint playingId)
    {
        if (!_playingObjects.TryGetValue(playingId, out uint gameObject))
            return new WwiseRtpcSetResult(WwiseRtpcSetKind.UnknownPlayingId, 0, 0, value, 0, false);
        return SetRtpcs(rtpcId, value, gameObject, playingId, 0, 0, bypass: false);
    }

    /// <summary>
    /// SetRTPCValue (gapA 5.1): key {gameObject, playingId, 0, FF, FF, 0}. The most specific slot is marked
    /// valid (gapF 1.4): the playing-id sub-element when <paramref name="playingId"/> is non-zero, the
    /// game-object element when it is zero, and the root when <paramref name="gameObject"/> is zero.
    ///
    /// <para>The old value the transition starts from is the slot value, else the nearest less-specific
    /// valid value, else <c>entry+8</c> (gapF 1.4). <paramref name="explicitTime"/> is the explicit-time
    /// byte (correction C2, <c>0xA139A0..0xA139A8</c>): when set, the ramp is skipped and the duration is
    /// the caller time (<c>0xA1393C</c>). Otherwise the duration is <c>max(caller time, entry ramp)</c>
    /// (gapF 1.5): type 1 is <c>|Δ|/rate·1000</c> ms, type 2 is the up/down field × 1000 ms, and anything
    /// else is 0. A non-positive duration is immediate. A positive duration is a transition only when the
    /// exact written slot already held a value (the <c>0xA13948 cmp r7,#0</c> bypass; <c>r7</c> is that
    /// slot's pointer, 0 when it was invalid even if an ancestor held a value); when it was invalid the
    /// outcome depends on the unmodelled <c>0xA1B5FC</c> gate and is reported as
    /// <see cref="WwiseRtpcSetKind.GatedTransition"/>.</para>
    ///
    /// <para>The shipped set path (<c>0xA1404C</c>) passes explicitTime 0, which is the default.</para>
    /// </summary>
    public WwiseRtpcSetResult SetRtpcs(uint rtpcId, float value, uint gameObject, uint playingId,
                                       double timeMs, byte curve, bool bypass, bool explicitTime = false)
    {
        var entry = GetOrCreate(rtpcId);

        float oldValue;
        bool slotValid;   // the exact written slot's valid flag before the write: the 0xA13948 gate input
        if (gameObject == 0)
        {
            // gapF 1.4: gameObj = 0 and an empty key marks the root.
            slotValid = entry.RootValid;
            oldValue = entry.RootValid ? entry.RootValue : entry.Default;
            entry.RootValue = value;
            entry.RootValid = true;
        }
        else
        {
            var element = GetOrCreateGameObject(entry, gameObject);
            if (playingId != 0)
            {
                // gapF 1.4: a non-zero playing id inserts a sub-element and marks only that valid.
                var sub = GetOrCreatePlayingId(element, playingId);
                slotValid = sub.Valid;
                oldValue = sub.Valid ? sub.Value
                    : element.Valid ? element.Value
                    : entry.RootValid ? entry.RootValue
                    : entry.Default;
                sub.Value = value;
                sub.Valid = true;
            }
            else
            {
                // gapF 1.4: playingID = 0 marks the game-object element itself.
                slotValid = element.Valid;
                oldValue = element.Valid ? element.Value
                    : entry.RootValid ? entry.RootValue
                    : entry.Default;
                element.Value = value;
                element.Valid = true;
            }
        }

        // C2: a non-zero explicit-time byte skips the ramp and makes the duration the caller time.
        double duration = explicitTime ? timeMs : RampDuration(entry, value - oldValue, timeMs);

        // gapA 5.1 selects the message (type 2 when time 0 and no bypass, else type 3), which is separate
        // from the outcome. C2: <= 0 is immediate; a positive duration is a transition when the exact
        // written slot already held a value, otherwise the 0xA1B5FC gate applies and is unmodelled.
        bool interpolated = timeMs != 0 || bypass;
        var kind = duration <= 0 ? WwiseRtpcSetKind.Immediate
            : slotValid ? WwiseRtpcSetKind.Transition
            : WwiseRtpcSetKind.GatedTransition;
        return new WwiseRtpcSetResult(kind, duration, oldValue, value, curve, interpolated);
    }

    /// <summary>
    /// The ramp duration (0xA137D8, gapF 1.5, correction C2): the signed <c>max(entry ramp, caller time)</c>
    /// (0xA139BC..0xA139C8). Up is chosen when <c>delta &gt; 0</c>, else down (type 1 <c>0xA139E0</c> →
    /// <c>ble 0xA13A4C</c>; type 2 <c>0xA13A34 vldrgt up / vldrle down</c>), so Δ = 0 uses down. The entry
    /// ramp is <c>|Δ|/rate·1000</c> ms for type 1 and the up/down field × 1000 ms for type 2; either way
    /// the float is converted with <c>vcvt.s32.f32</c> (0xA13A04/0xA13A40), truncating toward zero. A zero
    /// rate on a type-1 ramp skips the whole subtract/divide/multiply (0xA139E8/0xA139F4,
    /// 0xA13A50/0xA13A58), so the computed duration stays 0 — no infinity, no NaN, no forced-immediate. A
    /// result <c>&gt; 0</c> is a transition, otherwise immediate (0xA13940..0xA13944).
    /// </summary>
    private static double RampDuration(Entry entry, float delta, double timeMs)
    {
        int computed = 0;
        switch (entry.RampType)
        {
            case 1:
                float rate = delta > 0 ? entry.RampUp : entry.RampDown;
                // 0xA139E8/0xA139F4 and 0xA13A50/0xA13A58: a zero rate skips the whole computation, so the
                // computed duration stays 0. (int) truncates toward zero, as vcvt.s32.f32 does.
                if (rate != 0f) computed = (int)(Math.Abs((double)delta) / rate * 1000.0);
                break;
            case 2:
                float seconds = delta > 0 ? entry.RampUp : entry.RampDown;
                computed = (int)(seconds * 1000.0);   // 0.0 * 1000 = 0
                break;
        }
        return Math.Max(computed, timeMs);
    }

    // ---------------------------------------------------------------- the lookup

    /// <summary>
    /// The value-store lookup 0xA17280 (gapF 1.6–1.9): exact game object, then within it the exact playing
    /// id, else playing id 0, else the element's own value; a game object not in the tree falls to the
    /// game-object-0 element; otherwise the root's valid flag; otherwise <c>entry+8</c>. A
    /// <c>gameObject</c> of 0 goes straight to the root (gapF 1.9). An id not in the store returns
    /// <see cref="WwiseRtpcValueKind.NotInStore"/>.
    /// </summary>
    public WwiseRtpcValue Lookup(uint rtpcId, uint gameObject, uint playingId)
    {
        if (!_entries.TryGetValue(rtpcId, out var entry))
            return new WwiseRtpcValue(WwiseRtpcValueKind.NotInStore, 0);

        // gapF 1.9: gameObj = 0 with an empty key goes straight to the root.
        if (gameObject == 0) return RootOr(entry);

        var element = FindGameObject(entry, gameObject);
        if (element is null)
        {
            // gapE 7.3 step 3: the game object is not in the tree, so search the game-object-0 element.
            element = FindGameObject(entry, 0);
            if (element is null) return RootOr(entry);
        }

        // gapE 7.3 step 2: exact playing id, else playing id 0, else the element's own value.
        if (playingId != 0)
        {
            var sub = FindPlayingId(element, playingId);
            if (sub is { Valid: true }) return new WwiseRtpcValue(WwiseRtpcValueKind.Stored, sub.Value);
        }
        var zero = FindPlayingId(element, 0);
        if (zero is { Valid: true }) return new WwiseRtpcValue(WwiseRtpcValueKind.Stored, zero.Value);
        if (element.Valid) return new WwiseRtpcValue(WwiseRtpcValueKind.Stored, element.Value);
        return RootOr(entry);
    }

    private static WwiseRtpcValue RootOr(Entry entry) => entry.RootValid
        ? new WwiseRtpcValue(WwiseRtpcValueKind.Stored, entry.RootValue)
        : new WwiseRtpcValue(WwiseRtpcValueKind.StmgDefault, entry.Default);

    // ---------------------------------------------------------------- accumulation and curve evaluation

    /// <summary>
    /// The subscription's accumulated curve value (gapA 5.4, gapE 7.2): acc == 2 is the product of the
    /// curve values (start 1.0, <c>0xA17724</c>), anything else the sum (start 0.0, <c>0xA17878</c>).
    /// </summary>
    public static double Accumulate(byte accumulate, IReadOnlyList<double> values)
    {
        double acc = accumulate == 2 ? 1.0 : 0.0;
        foreach (double v in values) acc = accumulate == 2 ? acc * v : acc + v;
        return acc;
    }

    /// <summary>
    /// Evaluates a subscription's curves for a key: each curve's stored value is looked up, its curve is
    /// evaluated and scaled, and the results are accumulated (gapA 5.4, gapE 7.2). An id not in the store
    /// throws rather than contributing a default.
    /// <para>QUEUED / MISSING, resolve at wiring: this legacy path (used by WwiseGain) is a NON-FAITHFUL PARALLEL COPY of 0xA17878 / 0xA17724. It evaluates the curve in double width
    /// (the engine's 0xA14E28 is single-precision), falls back to the raw id when <c>ParamId</c> is not below 64 (the engine has no such fallback), and uses the per-curve <c>SourceType</c> where the
    /// engine uses the entry's <c>[e+0x24]</c>. Its behaviour is exactly HEAD's (a NotInStore id throws NotSupportedException; the engine's R3 flow is only in <see cref="A11590"/>, the engine-shaped path).</para>
    /// </summary>
    public double Evaluate(byte accumulate, IReadOnlyList<WwiseRtpc> curves,
                           uint gameObject, uint playingId, out bool reduced)
    {
        ArgumentNullException.ThrowIfNull(curves);
        reduced = false;
        var values = new List<double>(curves.Count);
        foreach (var curve in curves)
        {
            var v = Lookup(curve.SourceId, gameObject, playingId);
            if (v.Kind == WwiseRtpcValueKind.NotInStore)
                throw new NotSupportedException(
                    $"M6-009: RTPC 0x{curve.SourceId:X8} is not in the store. gapF 1.8/1.10's not-in-store " +
                    "branches are type == 1 -> 0x9E6748 and type != 1 -> param 0/7 1.0 + skip; both are " +
                    "unreachable for STMG-listed RTPCs and are not modelled here.");
            values.Add(curve.EvaluateScaled(v.Value, out bool r));
            reduced |= r;
        }
        return Accumulate(accumulate, values);
    }

    /// <summary>
    /// Evaluates a node's RTPC bindings for a key, taking the accumulate byte from the first binding. The
    /// native subscription carries one accumulate byte (gapA 5.4); the stack keeps the bank byte on each
    /// binding, so a caller whose bindings share it passes either.
    /// </summary>
    public double Evaluate(IReadOnlyList<WwiseRtpc> curves, uint gameObject, uint playingId, out bool reduced)
    {
        ArgumentNullException.ThrowIfNull(curves);
        byte accumulate = curves.Count > 0 ? curves[0].Accumulate : (byte)1;
        return Evaluate(accumulate, curves, gameObject, playingId, out reduced);
    }

    // ---------------------------------------------------------------- the subscription table and 0xA11590 (C34.2 R1, R2)

    private readonly Dictionary<(uint Key1, uint Param), WwiseRtpcSubscription> _subscriptions = new();

    /// <summary>
    /// Adds a subscription to the table <c>0xA11590</c> reads (<c>[mgr+0x10]</c> buckets, <c>[mgr+0x14]</c> count). The engine's registrar <c>0xA19ECC</c> (the limiter's <c>RtpcSubscribeA19ECC</c> seam) is not adopted, so this is the host's way
    /// to fill the table; the hash's chain order and growth are not modelled, and a second subscription with the same (key, parameter) is refused (the engine's chain would find the first).
    /// </summary>
    public void AddSubscription(WwiseRtpcSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        if (!_subscriptions.TryAdd((subscription.Key1, subscription.Param), subscription))
            throw new InvalidOperationException("M6-009 R1: a subscription for this (key, parameter) exists; the engine's chain order for duplicates is not modelled");
    }

    /// <summary>
    /// The curve evaluation <c>0xA14E28(curve, x, 0, &amp;idx)</c> that <c>0xA17878</c> / <c>0xA17724</c> call per curve (<c>0xA17990..0xA179A0</c>, <c>0xA1783C..0xA1784C</c>). Its body is unread
    /// (the engine works in single-precision polynomial approximations, e.g. 0xA14ED4..0xA14F2C for scaling 3; the double-width <see cref="WwiseRtpc.EvaluateScaled"/> is NOT the engine's curve), so there is no default: the host or
    /// test must supply it, and a test double is a double, not engine numerics.
    /// </summary>
    public Func<WwiseRtpc, float, float>? CurveA14E28 { get; set; }

    private float Curve(WwiseRtpc curve, float x)
        => (CurveA14E28 ?? throw new WwiseMissingBehaviourException(
            "MISSING 0xA14E28 curve evaluation | RTPC accumulate (0xA179A0, 0xA1784C) | unread; the double-width EvaluateScaled is not the engine's float curve"))(curve, x);

    /// <summary>
    /// <c>0xA11590(mgr, key1, param, key)</c> (R1): an empty table (<c>[mgr+0x14] == 0</c>) returns 0.0f; the subscription with <c>[e] == key1 &amp;&amp; [e+4] == param</c> is found by the hash <c>(key1 + param) % n</c> (a dictionary here); not found
    /// returns 0.0f; found with <c>[e+0x28] == 2</c> tail-calls the product <c>0xA17724</c>, else the sum <c>0xA17878</c> (<c>0xA11590..0xA11620</c>). The result is a single-precision float.
    /// </summary>
    public float A11590(uint key1, uint param, WwiseGainRtpcKey key)
    {
        if (_subscriptions.Count == 0) return 0f;                                         // 0xA11598..0xA115A0
        if (!_subscriptions.TryGetValue((key1, param), out var e)) return 0f;             // 0xA1161C
        return e.Accumulate == 2 ? Accumulate0A17724(e, key) : Accumulate0A17878(e, key); // 0xA115F0..0xA11618
    }

    /// <summary>
    /// <c>0xA17878(mgr, e, key)</c> (R2): the sum of the subscription's curves, single precision from 0.0f (an empty curve list returns the literal 0.0f at <c>0xA179CC</c>). Per curve <c>0xA17280(mgr, [c+4], [e+4], [e+0x24], ...)</c> on a copy of the
    /// key decides: a stored value (or the entry's STMG default, <c>[entry+8]</c>, which is the second lookup of <c>0xA17924..0xA179AC</c>) is <c>x</c>; an id absent from the store follows R3 (<see cref="CurveInput"/>); the curve
    /// evaluated at <c>x</c> is added unless the skip flag is set (<c>0xA178C0..0xA178CC</c>).
    /// </summary>
    private float Accumulate0A17878(WwiseRtpcSubscription e, WwiseGainRtpcKey key)
    {
        float s16 = 0f;                                                                   // 0xA178A8 vldr s16,[pc,#0x11c]
        foreach (var curve in e.Curves)                                                   // 0xA178E8..0xA178E0
        {
            if (!CurveInput(e, curve, key, out float x)) continue;                        // skip flag set: 0xA178CC
            s16 = s16 + Curve(curve, x);                                            // 0xA179A4..0xA179A8 vadd.f32 s16,s16,s15
        }
        return s16;
    }

    /// <summary><c>0xA17724(mgr, e, key)</c> (R2): the same with <c>vmul.f32</c> from 1.0f (an empty list returns 1.0f, <c>0xA17864</c>).</summary>
    private float Accumulate0A17724(WwiseRtpcSubscription e, WwiseGainRtpcKey key)
    {
        float s16 = 1f;                                                                   // 0xA17754 vmov.f32 s16,#1.0
        foreach (var curve in e.Curves)
        {
            if (!CurveInput(e, curve, key, out float x)) continue;                        // 0xA1776C..0xA17774
            s16 = s16 * Curve(curve, x);                                            // 0xA17850..0xA17854 vmul.f32 s16,s16,s15
        }
        return s16;
    }

    /// <summary>
    /// The per-curve input of <c>0xA17878</c> / <c>0xA17724</c> (R2, R3). <c>0xA17280</c> finds the id in the store: the stored value, or when none is valid the entry's default (<c>[entry+8]</c>) is <c>x</c>. Absent from the store: type 1
    /// (<c>[e+0x24]</c>) or a parameter (<c>[e+4]</c>) other than 0 and 7 returns <c>0x9E6748</c>'s result (unread: throws); type other than 1 with parameter 0 or 7 sets the skip flag (<c>0xA17410..0xA17424</c>: flag 1, value 1.0f), so the curve adds nothing.
    /// </summary>
    private bool CurveInput(WwiseRtpcSubscription e, WwiseRtpc curve, WwiseGainRtpcKey key, out float x)
    {
        var v = Lookup(curve.SourceId, key.GameObject, key.PlayingId);
        if (v.Kind != WwiseRtpcValueKind.NotInStore) { x = v.Value; return true; }
        if (e.Type != 1 && (e.Param == 0 || e.Param == 7)) { x = 1f; return false; }      // 0xA172EC..0xA172F8, 0xA17304 beq 0xA17410
        throw new WwiseMissingBehaviourException(
            $"M6-009 R3: RTPC 0x{curve.SourceId:X8} is not in the store (type {e.Type}, parameter {e.Param}); 0xA17280 returns the result of 0x9E6748 (a locked hash lookup in the manager at *0x10400E8), whose body is not adopted");
    }

    // ---------------------------------------------------------------- the entry tree

    private Entry GetOrCreate(uint rtpcId)
    {
        if (!_entries.TryGetValue(rtpcId, out var e)) _entries[rtpcId] = e = new Entry();
        return e;
    }

    private static GameObjectElement GetOrCreateGameObject(Entry entry, uint key)
    {
        int i = LowerBound(entry.GameObjects, key, static g => g.Key);
        if (i < entry.GameObjects.Count && entry.GameObjects[i].Key == key) return entry.GameObjects[i];
        var element = new GameObjectElement { Key = key };
        entry.GameObjects.Insert(i, element);
        return element;
    }

    private static PlayingIdElement GetOrCreatePlayingId(GameObjectElement element, uint key)
    {
        int i = LowerBound(element.PlayingIds, key, static p => p.Key);
        if (i < element.PlayingIds.Count && element.PlayingIds[i].Key == key) return element.PlayingIds[i];
        var sub = new PlayingIdElement { Key = key };
        element.PlayingIds.Insert(i, sub);
        return sub;
    }

    private static GameObjectElement? FindGameObject(Entry entry, uint key)
    {
        int i = LowerBound(entry.GameObjects, key, static g => g.Key);
        return i < entry.GameObjects.Count && entry.GameObjects[i].Key == key ? entry.GameObjects[i] : null;
    }

    private static PlayingIdElement? FindPlayingId(GameObjectElement element, uint key)
    {
        int i = LowerBound(element.PlayingIds, key, static p => p.Key);
        return i < element.PlayingIds.Count && element.PlayingIds[i].Key == key ? element.PlayingIds[i] : null;
    }

    /// <summary>The first index whose key is not below <paramref name="key"/>; keeps the array sorted for the binary search.</summary>
    private static int LowerBound<T>(List<T> list, uint key, Func<T, uint> selector)
    {
        int lo = 0, hi = list.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (selector(list[mid]) < key) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    /// <summary>One RTPC id's entry (0x4C bytes natively): default, ramp, root, and the game-object array (gapF 1.1).</summary>
    private sealed class Entry
    {
        public float Default;                 // +8
        public uint RampType;                 // +0xC
        public float RampUp;                  // +0x10
        public float RampDown;                // +0x14
        public float RootValue;               // +0x1C
        public bool RootValid;                // +0x20
        public readonly List<GameObjectElement> GameObjects = new();   // +0x24 sorted by key
    }

    /// <summary>A game-object element (0x1C bytes): key, value/valid, and its playing-id sub-array (gapF 1.1).</summary>
    private sealed class GameObjectElement
    {
        public uint Key;                      // +0x18
        public float Value;                   // +4
        public bool Valid;                    // +8
        public readonly List<PlayingIdElement> PlayingIds = new();     // +0xC.. sorted by key
    }

    /// <summary>A playing-id sub-element, the same 0x1C shape (gapF 1.1).</summary>
    private sealed class PlayingIdElement
    {
        public uint Key;                      // +0x18
        public float Value;                   // +4
        public bool Valid;                    // +8
    }
}

/// <summary>
/// A subscription entry of the table <c>0xA11590</c> reads (C34.2 R1, R2): <c>[e]</c> the key (the address <c>node+0x10</c>), <c>[e+4]</c> the parameter (the bank parameter mapped through <see cref="WwiseBusWalk.ParamBitTable"/>), <c>[e+0x24]</c> the
/// type (1 is the MIDI parameter kind; the bank's source type), <c>[e+0x28]</c> the accumulate word (2 multiplies), and the curves (<c>[e+0x2C]</c>, 20 bytes each; <c>[c+4]</c> the RTPC id).
/// </summary>
public sealed class WwiseRtpcSubscription
{
    /// <summary><c>[e+0]</c>.</summary>
    public uint Key1 { get; init; }

    /// <summary><c>[e+4]</c>.</summary>
    public uint Param { get; init; }

    /// <summary><c>[e+0x24]</c>.</summary>
    public uint Type { get; init; }

    /// <summary><c>[e+0x28]</c>.</summary>
    public uint Accumulate { get; init; }

    /// <summary>The curves, in order; <see cref="WwiseRtpc.SourceId"/> is <c>[c+4]</c>.</summary>
    public IReadOnlyList<WwiseRtpc> Curves { get; init; } = Array.Empty<WwiseRtpc>();
}
