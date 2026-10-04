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

        // The read half (no element is created): the exact written slot's valid flag before the write (the 0xA13948 gate input) and the old value the transition starts from. The write half is below, after the 0xA1B5FC gate.
        float oldValue;
        bool slotValid;
        bool groupG;      // 0xA13A88's byte [sp+0x1a] (C37.1): the functor G of 0xA114D8 exists when the written scope already has children (0xA13CF0, 0xA13DEC, 0xA13E40)
        GameObjectElement? element = null;
        if (gameObject == 0)
        {
            // gapF 1.4: gameObj = 0 and an empty key marks the root.
            slotValid = entry.RootValid;
            oldValue = entry.RootValid ? entry.RootValue : entry.Default;
            groupG = entry.GameObjects.Count != 0;                                       // 0xA13DEC ldr r3,[r4,#0x28]; adds sl,r3,#0; movne sl,#1
        }
        else
        {
            element = FindGameObject(entry, gameObject);
            if (playingId != 0)
            {
                // gapF 1.4: a non-zero playing id inserts a sub-element and marks only that valid.
                var sub = element is null ? null : FindPlayingId(element, playingId);
                slotValid = sub is { Valid: true };
                oldValue = sub is { Valid: true } ? sub.Value
                    : element is { Valid: true } ? element.Value
                    : entry.RootValid ? entry.RootValue
                    : entry.Default;
                groupG = false;                                                          // 0xA13E40: the sub-element's own child count, which this store has none of
            }
            else
            {
                // gapF 1.4: playingID = 0 marks the game-object element itself.
                slotValid = element is { Valid: true };
                oldValue = element is { Valid: true } ? element.Value
                    : entry.RootValid ? entry.RootValue
                    : entry.Default;
                groupG = element is not null && element.PlayingIds.Count != 0;           // 0xA13CF0 ldr sl,[r2,#0x10]
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

        // C37.1: an immediate set reaches 0xA12CA0 and from it 0xA114D8 (the array A walk) when the entry has subscribers. What 0xA137D8 / 0xA12CA0 do around that call and the inventory does not settle is a visible stop, but only where
        // it changes something an observer can see: a PBI child that would receive a delta, or a subscription of a type whose body is not adopted. With neither, the engine's 0xA114D8 evaluates curves and calls an empty fan-out.
        bool deliver = kind == WwiseRtpcSetKind.Immediate && HasSubscribers(rtpcId);
        if (deliver && ObservableDelivery(rtpcId))
        {
            if (timeMs != 0 || bypass || explicitTime)
                throw new WwiseMissingBehaviourException(
                    "M6-009 C37.1: the value-change delivery is adopted for the shipped set path 0xA1404C (caller time 0, no bypass, explicit-time byte 0, key {object, playing id, 0, 0xFF, 0xFF, 0}); the type-3 message path 0x9AF158 with its caller struct is not read");
            if (slotValid && oldValue == value)
                throw new WwiseMissingBehaviourException(
                    "M6-009 C37.1: 0xA137D8 returns without calling 0xA12CA0 when the exact slot already holds the new value (0xA13808..0xA13914, which also unlinks a pending transition); that branch is not adopted");
            if (!slotValid && !(TransitionGateA1B5FC ?? throw new WwiseMissingBehaviourException(
                    "M6-009 C37.1: 0xA12CA0 with no exact cell (the first set of a game object or playing id) stores the value and calls 0xA114D8 only when 0xA1B5FC(entry id, key) returns 1 (0xA12E80..0xA12E98); 0xA1B5FC is not read: supply WwiseRtpcStore.TransitionGateA1B5FC"))(rtpcId, gameObject, playingId))
            {
                // 0xA12E90 cmp r0,#1; bne 0xA12CE8: no store, no delivery, return 1.
                return new WwiseRtpcSetResult(kind, duration, oldValue, value, curve, interpolated);
            }
        }
        else if (kind != WwiseRtpcSetKind.Immediate && HasSubscribers(rtpcId) && ObservableDelivery(rtpcId))
            throw new WwiseMissingBehaviourException(
                "M6-009 C37.1: a positive-duration set creates a transition (0xA13948..0xA13968 -> 0xA0E5E4) whose per-frame deliveries to the registered PBI are not read; only immediate sets are delivered");
        else if (deliver && (timeMs != 0 || bypass || explicitTime || (slotValid && oldValue == value)))
            deliver = false;                                                             // the engine's 0xA137D8 / type-3 path never reaches 0xA114D8 here, and nothing observable depends on it

        // The write half (gapF 1.4).
        if (gameObject == 0)
        {
            entry.RootValue = value;
            entry.RootValid = true;
        }
        else
        {
            element ??= GetOrCreateGameObject(entry, gameObject);
            if (playingId != 0)
            {
                var sub = GetOrCreatePlayingId(element, playingId);
                sub.Value = value;
                sub.Valid = true;
            }
            else
            {
                element.Value = value;
                element.Valid = true;
            }
        }

        if (deliver)
            DeliverValueChangeA114D8(rtpcId, oldValue, value, new WwiseGainRtpcKey(gameObject, playingId), groupG);   // 0xA12CE4 bl 0xA114D8(entry, [cell before], new, key, byte)
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
    /// Evaluates a subscription's curves for a key: each curve's stored value is looked up, its curve is evaluated and scaled, and the results are accumulated (gapA 5.4, gapE 7.2). An id not in the store
    /// throws rather than contributing a default.
    /// <para>Batch 5d (C35): the per-curve step is the engine's <c>0xA14E28</c> in binary32 (<see cref="CurveA14E28"/>, the exact port; the double-width <see cref="WwiseRtpc.EvaluateScaled"/> is no longer used here) and the
    /// accumulation is the engine's binary32 sum from <c>0.0f</c> / product from <c>1.0f</c> (<c>0xA17878</c> / <c>0xA17724</c>, <c>vadd.f32</c> / <c>vmul.f32</c> in curve order); the result is widened to <c>double</c> for the existing signature.
    /// <c>reduced</c> is always false: the engine has no such notion (an interp code of 10 or more is y = 0 then the scaling stage, L5-25).</para>
    /// <para>QUEUED / MISSING, resolve at wiring: this legacy path (used by WwiseGain; nothing in production constructs a <see cref="WwiseRtpcStore"/>) is still a NON-FAITHFUL PARALLEL COPY of 0xA17878 / 0xA17724 in its lookups: it falls back
    /// to the raw id when <c>ParamId</c> is not below 64 (the engine has no such fallback), and it uses the per-curve <c>SourceType</c> where the engine uses the entry's <c>[e+0x24]</c>. A NotInStore id throws NotSupportedException
    /// (the engine's R3 flow is only in <see cref="A11590"/>, the engine-shaped path). A curve with no points throws (the engine would read past the buffer; the loader refuses such a curve).</para>
    /// </summary>
    public double Evaluate(byte accumulate, IReadOnlyList<WwiseRtpc> curves,
                           uint gameObject, uint playingId, out bool reduced)
    {
        ArgumentNullException.ThrowIfNull(curves);
        reduced = false;
        float acc = accumulate == 2 ? 1f : 0f;                                            // 0xA17754 vmov.f32 s16,#1.0 / 0xA178A8 vldr s16,[pc]
        foreach (var curve in curves)
        {
            var v = Lookup(curve.SourceId, gameObject, playingId);
            if (v.Kind == WwiseRtpcValueKind.NotInStore)
                throw new NotSupportedException(
                    $"M6-009: RTPC 0x{curve.SourceId:X8} is not in the store. gapF 1.8/1.10's not-in-store " +
                    "branches are type == 1 -> 0x9E6748 and type != 1 -> param 0/7 1.0 + skip; both are " +
                    "unreachable for STMG-listed RTPCs and are not modelled here.");
            float y = Curve(curve, v.Value);
            acc = accumulate == 2 ? acc * y : acc + y;                                    // 0xA17854 vmul.f32 / 0xA179A8 vadd.f32
        }
        return acc;
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
        if (ListenerCount(subscription.Key1) != 0)
            throw new WwiseMissingBehaviourException(
                "M6-009 C37.1: a subscription added while listeners are registered at its holder is applied once through 0xA11624 (0xA11B3C..0xA11B48; type 2: node vt+4 -> vt+0xC8), which is not read; add subscriptions before listeners register");
        if (!_subscriptions.TryAdd((subscription.Key1, subscription.Param), subscription))
            throw new InvalidOperationException("M6-009 R1: a subscription for this (key, parameter) exists; the engine's chain order for duplicates is not modelled");
    }

    /// <summary>
    /// The curve evaluation <c>0xA14E28(curve, x, 0, &amp;idx)</c> that <c>0xA17878</c> / <c>0xA17724</c> call per curve (<c>0xA17990..0xA179A0</c>, <c>0xA1783C..0xA1784C</c>; hint 0 at every call site, C35.3). The default is the
    /// engine's function ported in binary32, <see cref="WwiseRtpcCurveA14E28.Evaluate(WwiseRtpc, float)"/> (C35.1). The property stays settable only as a seam for a host or test that wants to observe or wrap the evaluation.
    /// </summary>
    public Func<WwiseRtpc, float, float> CurveA14E28 { get; set; } = static (curve, x) => WwiseRtpcCurveA14E28.Evaluate(curve, x);

    private float Curve(WwiseRtpc curve, float x) => CurveA14E28(curve, x);

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

    // ---------------------------------------------------------------- the value-change delivery (C37.1)

    /// <summary>
    /// <c>0xA1B5FC(entry id, key)</c> (<c>0xA12E88..0xA12E98</c>, <c>0xA13A10..0xA13A24</c>), the gate <c>0xA12CA0</c> consults when the exact written slot has no cell: only a result of 1 stores the value and calls <c>0xA114D8</c>. The body is not read
    /// (C2 describes only part of it), so it is a required seam when a delivery depends on it: the arguments are the RTPC id, the game object and the playing id of the key.
    /// </summary>
    // fidelity: M6-009
    public Func<uint, uint, uint, bool>? TransitionGateA1B5FC { get; set; }

    /// <summary>
    /// The two bytes <c>0x97E570(0)</c> writes (<c>0x97E570..0x97E584</c>): <c>byte [0x108D7D8+0xC] = 0</c> and <c>byte [0x108D7D8] = 1</c>, the whole effect of a node subscription of parameter 0xD (<c>0x9868B0</c>: <c>cmp r1,#0xd</c>, no fan-out).
    /// What those bytes mean is not read.
    /// </summary>
    // fidelity: M6-009
    public byte Byte108D7D8 { get; private set; }

    /// <summary>See <see cref="Byte108D7D8"/>: <c>byte [0x108D7D8+0xC]</c>.</summary>
    // fidelity: M6-009
    public byte Byte108D7D8_0C { get; private set; }

    private readonly Dictionary<uint, List<WwiseRtpcListener>> _registries = new();

    /// <summary>
    /// <c>0xA19ECC(ctx, node, {mask}, 1)</c> -> <c>0x9F7390(node, ctx, mask, 1)</c> (the PBI Init <c>0xA0285C</c> -> <c>0x9BC5A8</c> at <c>0xA02868</c>), the non-bus loop <c>0x9F7DA8..0x9F82D4</c> as the verifier read it. Per node <c>sb</c>, from the PBI's node up
    /// <c>sb = [sb+0x34]</c> (<see cref="WwiseRoutingNode.Parent"/>): <c>W = ~satisfied &amp; listener</c> (<c>0x9F7E30</c>); <c>n40 = (u64)[node+0x40] &lt;&lt; 17</c> (<see cref="WwiseRoutingNode.Node40"/>), with <c>0x7E3FFFE0000</c> ORed in for the top node (no
    /// parent, <c>0x9F8758</c>); <c>allowed = W &amp; (0x127DF | n40)</c> (<c>0x9F7E1C..0x9F7E48</c>). The first holder <c>node+0x10</c> registers only if its registry exists (<c>[node+0x14] != 0</c>, <see cref="WwiseRoutingNode.SubscriptionMask14"/> not null; <c>0x9F7E4C</c>):
    /// <c>inter = allowed &amp; mask A</c> (<c>0x9F7E50..0x9F7E5C</c>); a zero <c>inter</c> removes the context from that registry (<c>0xA198A4</c>, <c>0x9F86F8</c>), otherwise the child record stores <b>inter</b> (<c>0xA1973C</c>, <c>0x9F7E9C</c>). Then <c>satisfied |= n40</c>
    /// (<c>0x9F8248..0x9F8258</c>); while no bus has been found the node's output bus <c>[node+0x38]</c> is taken and a non-zero <c>0x9C54E8(bus)</c> (<see cref="WwiseRoutingNode.A9C54E8"/>, C34.1 B7) ORs <c>0x20</c> into <c>satisfied</c> (<c>0x9F8260..0x9F8288</c>); the loop goes on to the
    /// parent while <c>(~satisfied | 0x127DF) &amp; listener != 0</c> (<c>0x9F82B4..0x9F82D0</c>) and the parent exists. The child's key is the PBI's key (the pull path's <see cref="WwiseGainRtpcKey"/>, see the report: its words beyond A are unread) and <c>[ctx+0x20] = node</c> when it was zero (<c>0xA19EF4</c>).
    /// <para>UNREAD, so a visible stop (<see cref="WwiseMissingBehaviourException"/>): a node whose <see cref="WwiseRoutingNode.Node40"/> is not supplied; the second holder <c>node+0x1C</c> when it has a registry (<see cref="WwiseRoutingNode.SecondHolderMask20"/>); and the bus branch
    /// <c>0x9F73DC..</c> (constant <c>0x1003F</c>, sites <c>0x9F74CC</c>, <c>0x9F76A8</c>), which the engine enters after the loop with the first output bus found: it is not read, so when that bus or one of its parent buses (<c>[bus+0x38]</c>) has a registry (a subscription targets a bus, e.g.
    /// robot_volume on Bus 1723505802) the registration throws. A bus chain with no registry is let through on the INFERENCE that the bus branch shares the registry-exists gate of the non-bus loop (<c>0x9F7484 cmp r4,#0; beq</c> mirrors <c>0x9F7E28/0x9F7E4C</c>); that is not a read.
    /// Not modelled and unobservable on the Play path: the first-holder activation (<c>0x9F7E68..0x9F7E88</c>: with <c>[registry+0x14] == 0</c> the engine calls <c>0xA1008C(*mgr, node+0x10, n40 | 0x127DF)</c>, moving array B to A), the dormant array B and the registry's mask B and the registry's mask B (<c>[registry+8]</c>, the AND of the child masks), which the fan-out does not need.</para>
    /// </summary>
    // fidelity: M6-009
    public void RegisterListenerA19ECC(WwisePlayingInstance pbi, WwiseRoutingNode node, ulong mask)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        ArgumentNullException.ThrowIfNull(node);
        var key = pbi.RtpcKey14 is WwiseGainRtpcKey k ? k
            : throw new WwiseMissingBehaviourException("M6-009 C37.1: the PBI key [pbi+0x14] (0x9BC90C / 0xA19CDC) is not a WwiseGainRtpcKey; its layout beyond word 0 is unread");
        if ((node.Byte46 & 4) != 0)                                         // 0x9F73AC..0x9F73C4 ldrb r1,[r0,#0x46]; and r3,r1,#4; beq 0x9F7DA8: bit 2 takes the bus-category loop 0x9F73C8.. (constant 0x1003F)
            throw new WwiseMissingBehaviourException(
                $"M6-009 C37.1: the start node {node.Id} has bit 2 of [node+0x46] set; the bus-category loop 0x9F73C8.. is not read");
        const ulong C127DF = 0x127DFUL;
        ulong satisfied = 0;
        WwiseRoutingNode? bus = null;
        if (mask == 0) return;                                                            // 0x9F7DA8..0x9F7DB4: a zero listener returns
        for (var sb = node; sb is not null;)
        {
            ulong w = ~satisfied & mask;                                                  // 0x9F7E30 and sl,sl,r6 (sl = ~satisfied)
            ulong n40 = (ulong)(sb.Node40 ?? throw new WwiseMissingBehaviourException(
                $"M6-009 C37.1: the listener walk reads [node+0x40] (0x9F7DF8) of node {sb.Id}, which the routing node cannot give; set WwiseRoutingNode.Node40")) << 17;   // 0x9F7E00..0x9F7E10
            if (sb.Parent is null) n40 |= 0x7E3FFFE0000UL;                                // 0x9F7E14 beq 0x9F8758: orr (0xFFFE0000, 0x7E3)
            ulong allowed = w & (C127DF | n40);                                           // 0x9F7E1C..0x9F7E48
            if (sb.SubscriptionMask14 is { } regA)                                        // 0x9F7E28 cmp r4,#0; 0x9F7E4C beq 0x9F8038
            {
                ulong inter = allowed & regA;                                             // 0x9F7E50..0x9F7E5C
                if (inter == 0) RemoveChildA198A4(sb.SubscriptionKey10, pbi);             // 0x9F7E64 beq 0x9F86F8 -> 0xA198A4
                else AddChildA1973C(sb.SubscriptionKey10, pbi, key, inter);               // 0x9F7E9C ldrd r2,r3,[sp,#0x30]; 0xA1973C
            }
            if (sb.SecondHolderMask20 is not null)                                        // 0x9F8038 ldr r4,[sb,#0x20]; cmp r4,#0; bne 0x9F8044
                throw new WwiseMissingBehaviourException(
                    $"M6-009 C37.1: node {sb.Id} has a second holder registry [node+0x20] (0x9F8038..0x9F8098, activation site 0x9F8080); its registration is not modelled");
            satisfied |= n40;                                                             // 0x9F8248..0x9F8258 orr [sp+8],[sp+0x10]
            if (bus is null && sb.OutputBus is { } ob)                                    // 0x9F8230..0x9F8264 (flag & 1, no bus yet), [sb+0x38]
            {
                bus = ob;
                if (ob.A9C54E8()) satisfied |= 0x20;                                      // 0x9F8274..0x9F8284 0x9C54E8(bus) != 0
            }
            sb = sb.Parent;                                                               // 0x9F8290 ldr sb,[sb,#0x34]
            if (sb is null) break;                                                        // 0x9F8298 beq 0x9F82D4
            if (((~satisfied | C127DF) & mask) == 0) break;                               // 0x9F82B4..0x9F82D0
        }
        if (bus is not null)                                                              // 0x9F82D4: a bus was found -> 0x9F73DC (the bus branch), unread
        {
            if (!AllowBusBranchRegistryInference)
                throw new WwiseMissingBehaviourException(
                    $"M6-009 C37.1: the listener walk found output bus {bus.Id} and the engine then enters the bus branch 0x9F73DC.. (constant 0x1003F, activation sites 0x9F74CC, 0x9F76A8), which is not read; a separate extraction will read it");
            for (var b = bus; b is not null; b = b.OutputBus)                             // the inference only: a bus chain with a registry still stops
                if (b.SubscriptionMask14 is not null || b.SecondHolderMask20 is not null)
                    throw new WwiseMissingBehaviourException(
                        $"M6-009 C37.1: the bus branch 0x9F73DC.. with bus {b.Id}, whose holder has a registry (a subscription targets this bus), is not read");
        }
        if (pbi.Ctx20Node is null) pbi.Ctx20Node = node;                                  // 0xA19EEC..0xA19EF4 ldr r3,[r4,#0x20]; streq r5,[r4,#0x20] (after 0x9F7390 returns)
    }

    /// <summary>
    /// NOT engine-derived: a test/host opt-in (default false; nothing production-constructed may set it). It lets <see cref="RegisterListenerA19ECC"/> pass the unread bus branch <c>0x9F73DC..</c> on the assumption that the branch shares the registry-exists gate
    /// (<c>0x9F7484</c> vs <c>0x9F7E28/0x9F7E4C</c>), so that the shipped event_volume shape reaches a PBI; a bus chain with a registry still stops. Without it any found output bus stops the registration.
    /// </summary>
    public bool AllowBusBranchRegistryInference { get; set; }

    /// <summary>The child insertion <c>0xA1973C</c> (<c>0x9F7E9C</c>): the PBI's context becomes a child of the holder's registry with <paramref name="mask"/> (the intersected mask). Hosts and tests may call it directly to place a child with a chosen mask. A second record for the same PBI at the same holder (the engine's find <c>0xA19778</c> and its update) is not read: a visible stop.</summary>
    // fidelity: M6-009
    public void AddChildA1973C(uint holderKey10, WwisePlayingInstance pbi, WwiseGainRtpcKey key, ulong mask)
    {
        if (!_registries.TryGetValue(holderKey10, out var list)) _registries[holderKey10] = list = new List<WwiseRtpcListener>();
        if (list.Any(l => ReferenceEquals(l.Pbi, pbi)))
            throw new WwiseMissingBehaviourException("M6-009 C37.1: a second registration of one PBI at one holder takes the find/update path of 0xA19778 (0x9F7EC0..), which is not read");
        list.Add(new WwiseRtpcListener(pbi, key, mask));
    }

    /// <summary>The removal <c>0xA198A4(registry+0x10, ctx)</c> (<c>0x9F86F8</c>): the PBI's record at the holder, if any, is dropped.</summary>
    private void RemoveChildA198A4(uint holderKey10, WwisePlayingInstance pbi)
    {
        if (_registries.TryGetValue(holderKey10, out var list)) list.RemoveAll(l => ReferenceEquals(l.Pbi, pbi));
    }

    /// <summary>The mask stored in the PBI's child record at a holder (the intersected mask of <c>0x9F7E5C</c>), or null when it is not a child there.</summary>
    public ulong? ListenerMask(uint holderKey10, WwisePlayingInstance pbi)
        => _registries.TryGetValue(holderKey10, out var l) && l.FirstOrDefault(c => ReferenceEquals(c.Pbi, pbi)) is { } c ? c.Mask : null;

    /// <summary>Whether the PBI is a child of any registry (it registered a listener at Init and has not been removed).</summary>
    public bool HasListener(WwisePlayingInstance pbi) => _registries.Values.Any(l => l.Any(c => ReferenceEquals(c.Pbi, pbi)));

    /// <summary>
    /// HOST / TEST implementation of the removal the engine performs at the listener's destruction (listener dtor <c>0xA19D44</c> via <c>0x9F9064</c> to <c>0xA198A4</c>, with <c>[ctx+0x20]</c>): it drops the PBI's child records from every registry. It is NOT engine-derived
    /// (<c>0xA198A4</c> and the destructor are unread); <see cref="WwisePlaybackLimiter.RemoveRtpcListenerA198A4"/> is the required seam a host points at it explicitly.
    /// </summary>
    public void UnregisterListener(WwisePlayingInstance pbi)
    {
        foreach (var list in _registries.Values) list.RemoveAll(l => ReferenceEquals(l.Pbi, pbi));
    }

    /// <summary>The number of children registered at a holder (<c>node+0x10</c>); the engine's <c>[registry+0x14]</c>.</summary>
    public int ListenerCount(uint holderKey10) => _registries.TryGetValue(holderKey10, out var l) ? l.Count : 0;

    /// <summary>
    /// Whether delivering a set of <paramref name="rtpcId"/> is observable: a type-2 subscription whose holder's registry has a child (a PBI that would receive the delta), or a subscription whose type is not adopted (it would act on something this model has
    /// no object for, and <see cref="ApplySubscriptionA10C84"/> stops there). A type-0 subscription is adopted only with its context (<see cref="WwiseRtpcSubscription.TargetPbi"/>).
    /// </summary>
    private bool ObservableDelivery(uint rtpcId)
    {
        foreach (var e in _subscriptions.Values)
        {
            bool onId = false;
            foreach (var c in e.Curves) if (c.SourceId == rtpcId) { onId = true; break; }
            if (!onId) continue;
            if (e.Type == 2) { if (ListenerCount(e.Key1) != 0) return true; }
            else if (e.Type != 0 || e.TargetPbi is null) return true;
        }
        return false;
    }

    private bool HasSubscribers(uint rtpcId)
    {
        foreach (var e in _subscriptions.Values)
            foreach (var c in e.Curves)
                if (c.SourceId == rtpcId) return true;
        return false;
    }

    /// <summary>
    /// <c>0xA114D8(entry, oldX, newX, key, byte)</c> (C37.1 L7-03): <c>count = [entry+0x38]</c> zero returns; otherwise every subscription of array A (<c>[entry+0x34]</c>; array B is never walked, and the A/B split is unobservable on the
    /// Play path) gets <c>0xA10C84(e, rtpcId, oldX, newX, key, G, entry+0x18)</c>. A subscription belongs to the array of every RTPC id one of its curves names. The engine's array order (sorted by <c>[e+0x24]</c>, then the pointer) is not
    /// modelled: the order is the order the subscriptions were added (MISSING: it only matters when two subscriptions feed one PBI parameter, where float addition is not associative). <paramref name="groupG"/> is whether the byte argument was non-zero (the functor G).
    /// </summary>
    // fidelity: M6-009
    private void DeliverValueChangeA114D8(uint rtpcId, float oldX, float newX, WwiseGainRtpcKey key, bool groupG)
    {
        var inArrayA = new List<WwiseRtpcSubscription>();
        foreach (var e in _subscriptions.Values)                                          // the engine's memcpy of [entry+0x34] first (0xA11550), then the loop 0xA1155C..0xA11580
            foreach (var c in e.Curves)
                if (c.SourceId == rtpcId) { inArrayA.Add(e); break; }
        if (!AllowSubscriptionOrderApproximation)
        {
            // The engine's array A is sorted by ([e+0x24], pointer); this store uses insertion order. Float addition is not associative, so two subscriptions feeding one PBI parameter in one delivery are a visible stop.
            var seen = new HashSet<(WwisePlayingInstance, uint)>();
            foreach (var e in inArrayA)
                if (e.Type == 2 && _registries.TryGetValue(e.Key1, out var kids))
                    foreach (var child in kids)
                        if (ReceivesDelta(child, e.Param, key) && !seen.Add((child.Pbi, e.Param)))
                            throw new WwiseMissingBehaviourException(
                                "M6-009 C37.1: two subscriptions would add to one PBI parameter in this delivery; the engine's array A order ([e+0x24], pointer) is not modelled and float addition is not associative; set AllowSubscriptionOrderApproximation to accept insertion order");
        }
        foreach (var e in inArrayA) ApplySubscriptionA10C84(e, rtpcId, oldX, newX, key, groupG);
    }

    /// <summary>
    /// A host's opt-in to the delivery order of insertion where the engine's array A is sorted by <c>([e+0x24], pointer)</c> (pool addresses, not reproducible). Without it two subscriptions feeding one PBI parameter in one delivery stop visibly. The store is single-threaded:
    /// the engine takes a lock on the RTPC manager around the set (<c>0xA13A88</c>) and its readers; this class has none.
    /// </summary>
    public bool AllowSubscriptionOrderApproximation { get; set; }

    private static bool ReceivesDelta(WwiseRtpcListener child, uint paramId, WwiseGainRtpcKey key)
    {
        int shift = (sbyte)(paramId & 0xFF);
        ulong bit = shift >= 0 && shift < 64 ? 1UL << shift : 0UL;
        if ((child.Mask & bit) == 0) return false;
        bool keyed = key.GameObject != 0 || key.PlayingId != 0;
        if (!keyed) return true;
        if (child.Key.GameObject != key.GameObject) return false;
        return key.PlayingId == 0 || child.Key.PlayingId == key.PlayingId;
    }

    /// <summary>
    /// <c>0xA10C84(e, rtpcId, oldX, newX, F, G, ...)</c> (L7-04a): <c>[e] == 0</c> returns; the dispatch on <c>[e+0x24]</c> (2 -> <c>0xA10CF0</c>, 1 -> <c>0xA10E80</c>, 0 -> <c>0xA10CC8</c>, 3 -> <c>0xA11348</c>, 5 -> <c>0xA110E8</c>, every
    /// other value <c>0xA10DAC</c>). Types 2 and 0 are adopted (L7-04b, L7-04d); the others are RECOVERABLE_GAP or only dispatch-verified and throw.
    /// </summary>
    // fidelity: M6-009
    private void ApplySubscriptionA10C84(WwiseRtpcSubscription e, uint rtpcId, float oldX, float newX, WwiseGainRtpcKey key, bool groupG)
    {
        switch (e.Type)
        {
            case 2:
                ApplyNodeType2A10CF0(e, rtpcId, oldX, newX, key, groupG);
                return;
            case 0:
                ApplyContextType0A10CC8(e, rtpcId, newX, key, groupG);
                return;
            case 1:
                throw new WwiseMissingBehaviourException("M6-009 L7-04c: a type-1 (FX and plug-in parameter) subscription's value formulas (0xA10E80..0xA114D4) were only spot-checked, not adopted");
            case 3:
                throw new WwiseMissingBehaviourException("M6-009 L7-04e: a type-3 (modulator-owned parameter) subscription calls 0x9DBF78, whose body (0x9DBF78..0x9DCDC8) is RECOVERABLE_GAP");
            case 5:
                throw new WwiseMissingBehaviourException("M6-009 L7-04f: a type-5 subscription calls 0xA32D24, whose body (0xA32D24..0xA32DE8) is RECOVERABLE_GAP");
            default:
                throw new WwiseMissingBehaviourException($"M6-009 L7-04g: a type-{e.Type} subscription (0xA10DAC: 0xA6D434 / 0xA6CE3C / 0xA6DFE8) is not adopted (0xA6DFE8 and the child vt+0x5C class are RECOVERABLE_GAP)");
        }
    }

    /// <summary>
    /// Type 2 (nodes and buses, L7-04b, <c>0xA10CF0..0xA10E7C</c>, <c>0xA11340</c>): no key filter. <c>s16</c> is the sum, from 0.0f, over the curve slots whose <c>[slot+4]</c> is the RTPC id of <c>0xA14E28(curve, oldX, 0, &amp;idx)</c>; <c>s17</c> the same at
    /// <c>newX</c> (<c>vadd.f32</c>, one slot at a time: old first, then new). Then <c>target-&gt;vt+0(target, [e+4], F, s17, s17 - s16, G)</c> (<c>0xA10E6C</c>): the node subscriber's thunk <c>0x9868E4</c> -> <c>0x9868B0</c>. With no matching slot
    /// both sums are 0.0f and the call still happens.
    /// </summary>
    // fidelity: M6-009
    private void ApplyNodeType2A10CF0(WwiseRtpcSubscription e, uint rtpcId, float oldX, float newX, WwiseGainRtpcKey key, bool groupG)
    {
        float s16 = 0f;                                                                   // 0xA10CF8 vldr s16,[pc]
        float s17 = 0f;                                                                   // 0xA10D14 vmov.f32 s17,s16 (0xA11340 for an empty slot list)
        foreach (var c in e.Curves)                                                       // 0xA10D34..0xA10D3C [slot+4] == rtpcId
        {
            if (c.SourceId != rtpcId) continue;
            s16 = s16 + Curve(c, oldX);                                                   // 0xA10D60 bl 0xA14E28 (hint 0); 0xA10D7C vadd.f32 s16,s16,s15
            s17 = s17 + Curve(c, newX);                                                   // 0xA10D80 bl 0xA14E28; 0xA10D94 vadd.f32 s17,s17,s15
        }
        float delta = s17 - s16;                                                          // 0xA10E44 vsub.f32 s16,s17,s16
        NodeSetRtpcA9868B0(e.Key1, e.Param, key, s17, delta, groupG);                     // 0xA10E6C ldr ip,[ip]; blx ip
    }

    /// <summary>
    /// Type 0 (context level, L7-04d, <c>0xA10CC8</c>, <c>0xA10FF4..0xA110C8</c>): the subscription's scope key must match the set key in every non-wild field, <c>G != 0</c> and <c>G-&gt;vt+0(G, e+0xC)</c> non-zero returns (G's body is unread: a stop);
    /// otherwise <c>s = 0xA0E81C(e, rtpcId, newX)</c> (the sum of the matching slots' curves at <c>newX</c>, from 0.0f) and <c>0x9BD100(target, (int16)[e+4], &amp;s, 4)</c>, which stores only into the <c>[target+0xD0]</c> object (for parameters 0x1A, 0x1B, 0x1C).
    /// The subscription's scope key words <c>[e+0xC..0x20]</c> are {A, B, C, D, E, F} (<see cref="WwiseRtpcSubscription.ScopeKey"/>); the target is <see cref="WwiseRtpcSubscription.TargetPbi"/>. The context object at <c>[ctx+0xD0]</c> is null in this model
    /// (<see cref="WwisePlayingInstance.CtxD0"/>): with it the engine's call changes nothing, and a non-null one is a visible stop.
    /// </summary>
    // fidelity: M6-009
    private void ApplyContextType0A10CC8(WwiseRtpcSubscription e, uint rtpcId, float newX, WwiseGainRtpcKey key, bool groupG)
    {
        var sk = e.ScopeKey;
        if (key.GameObject != 0 && key.GameObject != sk.A) return;                        // 0xA10CC8..0xA10CE0 (F.A != 0 must equal [e+0xC])
        if (key.PlayingId != 0 && key.PlayingId != sk.B) return;                          // 0xA110CC..0xA110D8 (F.B != 0 must equal [e+0x10])
        // F.C == 0, F.D == F.E == 0xFF, F.F == 0 for every key this store builds (0xA1404C): wild, no compare (0xA11004..0xA11074).
        if (groupG)
            throw new WwiseMissingBehaviourException("M6-009 L7-04d: G != 0 asks G->vt+0(G, e+0xC) (0xA11074..0xA11098, the functor vptr 0x101C2E8) whose body is not read");
        float s = 0f;                                                                     // 0xA0E84C vldr s16,[pc]
        foreach (var c in e.Curves)                                                       // 0xA0E81C..0xA0E8C8
            if (c.SourceId == rtpcId) s = s + Curve(c, newX);                             // 0xA0E89C bl 0xA14E28; 0xA0E8B0 vadd.f32
        var target = e.TargetPbi ?? throw new WwiseMissingBehaviourException("M6-009 L7-04d: a type-0 subscription's target [e] is a context (PBI); WwiseRtpcSubscription.TargetPbi is not set");
        if (target.CtxD0 is not null)                                                     // 0x9BD100: ldr r3,[r0,#0xd0]; cmp r3,#0; beq 0x9BD130
            throw new WwiseMissingBehaviourException("M6-009 L7-04d: 0x9BD100 stores s into the [ctx+0xD0] object (+0xC, +0x10, +0x14 for parameters 0x1A, 0x1B, 0x1C); that object is not modelled");
        _ = s;                                                                            // [ctx+0xD0] == 0: returns 1 with nothing stored
    }

    /// <summary>
    /// The node subscriber's <c>vt+0</c> (<c>0x9868E4</c>: <c>sub r0,r0,#0x10; b 0x9868B0</c>), <c>0x9868B0(this, paramId, F, value, delta, G)</c> (L7-08): parameter 0xD (<c>cmp r1,#0xd</c>) runs <c>0x97E570(0)</c> and nothing else; otherwise the
    /// fan-out <c>0xA1B254(sub, paramId, F, value, delta, G)</c>.
    /// </summary>
    // fidelity: M6-009
    private void NodeSetRtpcA9868B0(uint holder, uint paramId, WwiseGainRtpcKey key, float value, float delta, bool groupG)
    {
        if (paramId == 0xD)                                                               // 0x9868B0 cmp r1,#0xd; beq 0x9868D8
        {
            Byte108D7D8_0C = 0;                                                           // 0x97E57C strb r0,[r3,#0xc] (r0 = 0)
            Byte108D7D8 = 1;                                                              // 0x97E580 strb r2,[r3] (r2 = 1)
            return;
        }
        FanOutA1B254(holder, paramId, key, value, delta, groupG);                         // 0x9868D4 b 0xA1B254 with r0 + 0x10
    }

    /// <summary>
    /// <c>0xA1B254(sub, paramId, F, value, delta, G)</c> (L7-08): the registry <c>[sub+4]</c> holds 0x28-byte child records {key 0x18, u64 mask <c>+0x18</c>, object <c>+0x20</c>}. A child is called <c>child-&gt;vt+8(child, paramId, value, delta)</c> when its mask has
    /// bit <c>paramId</c> (the NEON <c>vshl.u64</c> of 1 by the low byte of <c>paramId</c> as a signed count: 0 outside 0..63). F keyed (any of F.A, F.B, F.C non-zero, F.D or F.E not 0xFF, F.F non-zero): with <c>G == 0</c> the keyed scan <c>0xA1A6A4</c>: the children
    /// sorted by key from the first not below F, each tested field by field, the first mismatch ending the scan, those whose mask lacks the bit skipped; with <c>G != 0</c> <c>0xA1AD68</c>. F all wild: <c>G != 0</c> <c>0xA1AC40</c>, else the loop
    /// <c>0xA1B308..0xA1B3C8</c> over every child. The keyed scan is modelled for the keys <c>0xA1404C</c> builds (F.A != 0; F.C == 0, F.D == F.E == 0xFF, F.F == 0; F.B any), where the matching children are the contiguous run with the same A (and the same B when F.B != 0).
    /// MISSING (visible stops): <c>G != 0</c> with a child to consider (G's body is unread) and a key with F.A == 0 and F.B != 0 (the scan's lower bound then starts at the first child; not a shape any adopted entry builds).
    /// </summary>
    // fidelity: M6-009
    private void FanOutA1B254(uint holder, uint paramId, WwiseGainRtpcKey key, float value, float delta, bool groupG)
    {
        if (!_registries.TryGetValue(holder, out var children) || children.Count == 0) return;     // [registry+0x14] == 0: no child is called
        int shift = (sbyte)(paramId & 0xFF);                                              // 0xA1B314 vmov.32 d17[0],lr: the shift count is the low byte of the lane, signed
        ulong bit = shift >= 0 && shift < 64 ? 1UL << shift : 0UL;                        // 0xA1B31C vshl.u64 d16,d16,d17
        bool keyed = key.GameObject != 0 || key.PlayingId != 0;                           // 0xA1B26C..0xA1B2EC (the other key fields are wild)
        if (groupG)
            throw new WwiseMissingBehaviourException(
                keyed ? "M6-009 L7-08: G != 0 with a keyed set takes 0xA1AD68, which asks G->vt+0(G, child) (0xA1B1F8..0xA1B20C); G's body (vptr 0x101C2E8) is not read"
                      : "M6-009 L7-08: G != 0 with an all-wild set takes 0xA1AC40, which asks G->vt+0(G, child) (0xA1AC94..0xA1ACA4); G's body (vptr 0x101C2E8) is not read");
        if (keyed && key.GameObject == 0)
            throw new WwiseMissingBehaviourException("M6-009 L7-08: a keyed set with F.A == 0 and F.B != 0 is not a key 0xA1404C builds; the scan 0xA1A6A4 over it is not modelled");
        var ordered = children.OrderBy(c => c.Key.GameObject).ThenBy(c => c.Key.PlayingId).ToArray();   // the registry is kept sorted by key (the binary search 0xA1A6C8..0xA1A820)
        foreach (var child in ordered)
        {
            if (keyed)
            {
                if (child.Key.GameObject != key.GameObject) continue;                     // 0xA1A844..0xA1A84C (the scan only reaches the run with this A)
                if (key.PlayingId != 0 && child.Key.PlayingId != key.PlayingId) continue; // 0xA1A858..0xA1A86C
            }
            if ((child.Mask & bit) == 0) continue;                                        // 0xA1B360..0xA1B370 / 0xA1AAD8..0xA1AAE8
            WwisePlayPath.DeliverRtpcA02CE4(child.Pbi, paramId, value, delta);            // 0xA1B38C blx [vt+8] -> 0xA02EC0 -> 0xA02CE4
        }
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

    /// <summary>The scope key <c>[e+0xC..0x23]</c> {A, B, C, D, E, F}; the node subscriptions made at bank load carry <c>{0, 0, 0, 0xFF, 0xFF, 0}</c> (<c>0xA1A3E8</c>, C37.1). Only a type-0 subscription's filter reads it (L7-04d).</summary>
    public WwiseRtpcScopeKey ScopeKey { get; init; } = WwiseRtpcScopeKey.Wild;

    /// <summary>For a type-0 (context) subscription: the context <c>[e]</c>, as the PBI that owns it; null for the node subscriptions (their <see cref="Key1"/> is the holder address).</summary>
    public WwisePlayingInstance? TargetPbi { get; init; }
}

/// <summary>The six words of a scope key <c>[e+0xC..0x23]</c> (<c>A</c> u32, <c>B</c> u32, <c>C</c> u32, <c>D</c> u8, <c>E</c> u8, <c>F</c> u32).</summary>
public readonly record struct WwiseRtpcScopeKey(uint A, uint B, uint C, byte D, byte E, uint F)
{
    /// <summary>The wild key <c>{0, 0, 0, 0xFF, 0xFF, 0}</c>.</summary>
    public static WwiseRtpcScopeKey Wild { get; } = new(0, 0, 0, 0xFF, 0xFF, 0);
}

/// <summary>A child record of a node holder's registry (0x28 bytes natively): the PBI context, its key and its listener mask (C37.1).</summary>
public sealed record WwiseRtpcListener(WwisePlayingInstance Pbi, WwiseGainRtpcKey Key, ulong Mask);
