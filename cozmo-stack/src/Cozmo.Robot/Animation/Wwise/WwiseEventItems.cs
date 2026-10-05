// fidelity: M6-025, M6-026, M6-017
namespace Cozmo.Robot.Animation.Wwise;

// The playing-id entries of the callback manager (the object <c>*GOT</c> that <c>0xA02910</c>, <c>0xA02C98</c> and <c>0xA56464</c> load as <c>mgr</c>): the PBI's reference <c>0xA04D48</c>, its release
// <c>0xA04DE8</c> and the EndOfEvent / Duration callbacks <c>0xA03618</c> and <c>0xA0393C</c> (C31.1 R1.5, C31.4 R4.1, R4.2, R4.7). The writer of an entry is 0xA03108 (C40.1, WwisePlayingIdTable.CreateEntryA03108), called by the control path's PostEvent (WwiseEventRuntime) with the Post's callback, cookie and flags.

/// <summary>
/// A game object the lookup <c>0xA0C238</c> returns. <see cref="Word7C"/> (<c>[obj+0x7C]</c>: the low 30 bits a reference count, the top two bits kept) is what the EndOfEvent body and the PBI context init touch; the rest is the object's constructor
/// <c>0xA0B340..0xA0B420</c> and the stores of its message handlers (C40.4: <c>0xA0BA3C</c> the aux values <c>[+0x24..+0x43]</c>, <c>0xA0CB78</c> <c>[+0x60]</c> and <c>[+0x78]</c>, <c>0xA0BA28</c> the listener mask <c>[+0x22]</c>).
/// </summary>
// fidelity: M6-010, M6-025, M6-026
public sealed class WwiseGameObjectRef
{
    /// <summary><c>[obj+0x7C]</c>. A plain object has 0; <see cref="Create0A0B340"/> gives the constructor's <c>0xC0000001</c>.</summary>
    public uint Word7C { get; set; }

    /// <summary>The id the registry keys the object on (the hash key of <c>0xA0C238</c>/<c>0xA0CAFC</c>; the id the host registered it with).</summary>
    public uint Id { get; init; }

    /// <summary><c>[obj+0x22]</c> (byte): the listener mask. The constructor stores 1 (<c>0xA0B340 mov r6,#1</c>, <c>0xA0B3CC strb r6,[r4,#0x22]</c>); <see cref="SetListenerMaskA0BA28"/> is the handler writer.</summary>
    public byte Mask22 { get; set; }

    /// <summary><c>[obj+0x7F]</c> (byte): the top byte of <see cref="Word7C"/>; <c>0xA0BA28</c> ORs 0x40 into it.</summary>
    public byte Byte7F => (byte)(Word7C >> 24);

    /// <summary><c>[obj+0x24..+0x43]</c>: the four <c>{bus id, gain}</c> aux pairs (<c>0xA0BA3C</c>; 8 bytes each: the id at <c>+0x24+8i</c>, the float gain at <c>+0x28+8i</c>). All zero from the constructor.</summary>
    public (uint BusId, float Gain)[] Aux24 { get; } = new (uint, float)[4];

    /// <summary><c>[obj+0x60]</c>: the output bus volume (<c>0xA0CB78</c>); the constructor stores 1.0f (<c>0xA0B3D8</c>).</summary>
    public float Volume60 { get; set; } = 1f;

    /// <summary><c>[obj+0x64]</c> (float): the constructor stores 1.0f (<c>0xA0B3DC</c>); the writers other than the constructor are unread.</summary>
    public float Value64 { get; set; } = 1f;

    /// <summary><c>[obj+0x78]</c>: -1 from the constructor (<c>0xA0B3C8</c>) and from the output bus volume handler (<c>0xA0CBEC</c>, the message's <c>[msg+0xC] = -1</c>).</summary>
    public int Key78 { get; set; } = -1;

    /// <summary>
    /// The constructor <c>0xA0B340..0xA0B420</c> (C40.4 T-A2a): <c>[+0x7C]</c> low 30 bits 1 with <c>[+0x7F] = (top byte | 0xC0)</c> (the word <c>0xC0000001</c>), <c>[+0x22] = 1</c>, <c>[+0x24..+0x43]</c> and <c>[+0x28]</c>..zero, <c>[+0x60] = [+0x64] = 1.0f</c>, <c>[+0x78] = -1</c>.
    /// The other constructor stores (<c>+0x18..+0x20</c>, <c>+0x44..+0x5F</c>, <c>+0x68..+0x74</c>, <c>+0x80</c>) are not modelled: no adopted row reads them.
    /// </summary>
    public static WwiseGameObjectRef Create0A0B340(uint id) => new()
    {
        Id = id,
        Word7C = 0xC0000001u,                                  // 0xA0B34C bfi lr,r6,#0,#0x1e; 0xA0B3D0 strb r1,[r4,#0x7f] with r1 = (lr >> 24) | 0xC0 (lr[29:24] are 0 after the bfi)
        Mask22 = 1,                                            // 0xA0B3CC
    };

    /// <summary><c>0xA0BA28(obj, mask)</c>: <c>[obj+0x22] = mask</c> and <c>[obj+0x7F] |= 0x40</c> (a registration handler stores it; Anki's registration posts mask 1, <c>0x8D8CA0 movs r2,#1</c>).</summary>
    public void SetListenerMaskA0BA28(byte mask)
    {
        Mask22 = mask;                                         // 0xA0BA2C strb r1,[r0,#0x22]
        Word7C |= 0x40000000u;                                 // 0xA0BA30 orr r3,r3,#0x40; 0xA0BA34 strb r3,[r0,#0x7f]
    }

    /// <summary>
    /// <c>0xA0BA3C(obj, values, n)</c> (C40.4 T-A1c): <c>n &gt; 4</c> returns 2 and changes nothing; a null <paramref name="values"/> or <c>n == 0</c> zeroes <c>[+0x24..+0x43]</c>; otherwise the pairs are compacted in order, an input pair is kept when its id is non-zero and its gain is
    /// <c>&gt; 0</c> (<c>vcmpe s15,#0; ble</c>: a NaN gain is dropped), stored at slot <c>i</c>, and the remaining slots are zeroed. Returns 1.
    /// </summary>
    public int SetAuxValuesA0BA3C(IReadOnlyList<(uint BusId, float Gain)>? values, int n)
    {
        if (n > 4) return 2;                                   // 0xA0BA3C cmp r2,#4; bls
        int kept = 0;
        if (values is not null && n != 0)                      // 0xA0BA4C..0xA0BA60 beq 0xA0BBB8 (zero all)
        {
            for (int i = 0; i < n && kept < 4; i++)
            {
                var (id, gain) = values[i];
                if (id != 0 && gain > 0f) Aux24[kept++] = (id, gain);   // 0xA0BA68..0xA0BA90 and the repeats; the id is tested first, then the float
            }
        }
        for (int i = kept; i < 4; i++) Aux24[i] = (0u, 0f);    // 0xA0BB50..0xA0BBB0 (nothing to zero when all four are kept)
        return 1;
    }
}

/// <summary>The info block <c>0xA03618</c> builds at <c>sp+0</c> for the EndOfEvent callback (type 1): cookie, game object, playing id, event id, then the looked-up game object and <c>[item+0x3C]</c>.</summary>
/// <param name="Cookie"><c>[item+0x44]</c>.</param>
/// <param name="GameObject"><c>[item+0x24]</c>.</param>
/// <param name="PlayingId">The playing id.</param>
/// <param name="EventId"><c>[item+0x20]</c>.</param>
/// <param name="GameObjectRef">The game object <c>0xA0C238</c> found (<c>[sp+0x10]</c>), or null.</param>
/// <param name="Key3C"><c>[item+0x3C]</c> (<c>[sp+0x14]</c>).</param>
public sealed record WwiseEndOfEventInfo(object? Cookie, uint GameObject, uint PlayingId, uint EventId, WwiseGameObjectRef? GameObjectRef, uint Key3C);

/// <summary>The info block <c>0xA0428C</c> builds at <c>sp+0</c> for the type-0x20 callback: cookie, game object, playing id, event id.</summary>
public sealed record WwiseNodeNotifyInfo(object? Cookie, uint GameObject, uint PlayingId, uint EventId);

/// <summary>The info block <c>0xA0393C</c> builds for the Duration callback (type 8).</summary>
public sealed record WwiseDurationInfo(object? Cookie, uint GameObject, uint PlayingId, uint EventId, float Duration, float EstimatedDuration, uint NodeId, uint MediaId, bool Streaming);

/// <summary>
/// The refcounted external-source holder an entry's <c>[+0x28]</c> points at (<c>0xA031B8..0xA031D8</c>: the creation increments the word at the holder's address). The Cozmo path never has one: Anki's wrapper passes no external sources, so <c>0x9A6704</c> reaches <c>0x9A0EF8</c> with
/// <c>numExternals == 0</c>, which zeroes the block that supplies it (<c>0x9A10A0..0x9A10AC</c>). The body that builds a holder, <c>0x9A65C0</c>, is unread.
/// </summary>
public sealed class WwiseExternalSourceHolder
{
    /// <summary>The word at the holder's address (<c>[[P+0x10]]</c>).</summary>
    public uint RefCount { get; set; }
}

/// <summary>One playing-id entry (<c>0x3C</c> key, <c>0x18</c> and <c>0x1C</c> counters, <c>0x20</c> event id, <c>0x24</c> game object, <c>0x28</c> object, <c>0x30..0x38</c> external-source words, <c>0x40</c> callback, <c>0x44</c> cookie, <c>0x48</c> flags).</summary>
public sealed class WwiseEventItem
{
    /// <summary><c>+0x3C</c>: the playing id.</summary>
    public uint PlayingId { get; init; }

    /// <summary><c>+0x18</c>: references the PBIs hold (<c>0xA04D48</c> increments, <c>0xA04DE8</c> decrements).</summary>
    public int Count18 { get; set; }

    /// <summary><c>+0x1C</c>: outstanding messages and actions (<c>0xA04EDC</c> / <c>0xA04F54</c>).</summary>
    public int Count1C { get; set; }

    /// <summary><c>+0x20</c>: the event id, written by <c>0xA03108</c> (<c>0xA03190</c>, C40.1). An entry built another way (a test, a host) that never set it throws when it is read.</summary>
    public uint EventId20
    {
        get => _eventId20 ?? throw new WwiseMissingBehaviourException("M6-026 R4.1: [item+0x20] (the event id) was never written; 0xA03108 (WwisePlayingIdTable.CreateEntryA03108) writes it");
        set => _eventId20 = value;
    }
    private uint? _eventId20;

    /// <summary><c>+0x24</c>: the game object id as the Post gave it, written by <c>0xA03108</c> (<c>0xA03194</c>, C40.1). A Post made with no game object id leaves it unset (the engine's value for that is not in the inventory), and a read then throws.</summary>
    public uint GameObject24
    {
        get => _gameObject24 ?? throw new WwiseMissingBehaviourException("M6-026 R4.1: [item+0x24] (the game object id) was never written (the Post had no game object id; the engine's value for none is not in the inventory)");
        set => _gameObject24 = value;
    }
    private uint? _gameObject24;

    /// <summary><c>+0x28</c>: an object <c>0x9A6988</c> releases at the end (null is none); <c>0xA03108</c> stores the external-source holder here (<see cref="WwiseExternalSourceHolder"/>).</summary>
    public object? Object28 { get; set; }

    /// <summary><c>+0x30</c>, <c>+0x34</c>, <c>+0x38</c>: the external-source block words <c>[P+0x18]</c>, <c>[P+0x1C]</c>, <c>[P+0x20]</c> (<c>0xA031A8..0xA031BC</c>); 0 with no external sources.</summary>
    public uint Word30 { get; set; }

    /// <summary>See <see cref="Word30"/>.</summary>
    public uint Word34 { get; set; }

    /// <summary>See <see cref="Word30"/>.</summary>
    public uint Word38 { get; set; }

    /// <summary><c>+0x40</c>: the callback; null when none was registered.</summary>
    public Action<int, object>? Callback40 { get; set; }

    /// <summary><c>+0x44</c>: the callback cookie, a word the Post supplied: for Anki's wrapper the pointer to the callback context (<see cref="WwiseAudioCallbackContextObject"/> here), 0 (null) with none; a host that posts a number boxes it.</summary>
    public object? Cookie44 { get; set; }

    /// <summary>
    /// <c>+0x48</c>: the callback flags (the Wwise <c>AkCallbackType</c> values): bit 0 (0x1) EndOfEvent, bit 3 (0x8) Duration, bit 4 (0x10) speaker-volume matrix, 0x100000 get-source-play-position,
    /// 0x400000 stream buffering (R4.2). Written by <c>0xA03108</c> (<c>0xA031E0</c>, C40.1): the Post's flags, with the callback-less mask (<c>0xFD000 | 0xFF0 | 0xB</c> cleared) when the callback is null.
    /// </summary>
    public uint Flags48 { get; set; }
}

/// <summary>The bodies <c>0xA03618</c> calls that no row reads. Each is required when reached.</summary>
public sealed class WwiseEndOfEventSeams
{
    /// <summary><c>0xA05934(*GOT)</c> (<c>0xA037F4</c>): run first when <c>[item+0x48] &amp; 0x400000</c>. Unread.</summary>
    public Action? A05934 { get; set; }

    /// <summary><c>0xA0C238(registry, [item+0x24])</c> (<c>0xA036F8</c>): the game-object lookup. Unread (the control path models only registered-or-not).</summary>
    public Func<uint, WwiseGameObjectRef?>? GameObjectLookupA0C238 { get; set; }

    /// <summary><c>0xA0B600</c> (<c>0xA0381C</c>): the game object's teardown when its reference count reaches zero, before it is freed. Unread.</summary>
    public Action<WwiseGameObjectRef>? A0B600 { get; set; }

    /// <summary><c>0xA1C660(item, &amp;info+0x10)</c> (<c>0xA03734</c>). Unread.</summary>
    public Action<WwiseEventItem>? A1C660 { get; set; }

    /// <summary><c>0x9A6988([item+0x28])</c> (<c>0xA03748</c>), run when <c>[item+0x28] != 0</c>. Unread.</summary>
    public Action<object>? A9A6988 { get; set; }

    /// <summary><c>0xA1C65C(item)</c> (<c>0xA03750</c>). Unread.</summary>
    public Action<WwiseEventItem>? A1C65C { get; set; }
}

/// <summary>
/// The playing-id table of the callback manager: <c>mgr+4</c> bucket count, <c>mgr+0xC</c> the entry count, the lock <c>mgr+0x10</c>, the second lock <c>mgr+0x14</c> with the byte <c>mgr+0x1C</c> and the condition
/// <c>mgr+0x18</c> that bracket a callback. Entries are looked up by playing id.
/// </summary>
public sealed class WwisePlayingIdTable
{
    private readonly Dictionary<uint, WwiseEventItem> _items = new();
    private readonly object _lock = new();                                          // mgr+0x10

    /// <summary><c>[mgr+0xC]</c>: the number of entries.</summary>
    public int Count0C => _items.Count;

    /// <summary><c>[mgr+0x1C]</c> (byte): 0 while a callback runs (<c>0xA037A4</c>), 1 after it (<c>0xA037D0</c>).</summary>
    public bool CallbackIdle1C { get; private set; } = true;

    /// <summary>The number of times <c>pthread_cond_broadcast(mgr+0x18)</c> ran (<c>0xA037D4</c>, <c>0xA03A48</c>).</summary>
    public int Broadcasts { get; private set; }

    /// <summary>The bodies <c>0xA03618</c> reaches that no row reads.</summary>
    public WwiseEndOfEventSeams Seams { get; set; } = new();

    /// <summary>The entry for a playing id, or null (<c>0xA04D68..0xA04DB4</c>: the bucket walk by <c>id % [mgr+4]</c>).</summary>
    public WwiseEventItem? Find(uint playingId)
    {
        lock (_lock) return _items.TryGetValue(playingId, out var item) ? item : null;
    }

    /// <summary>Removes the entry (the M6-006 control path's model of <c>0xA03618</c>'s unlink); the engine's own body is <see cref="EndOfEventA03618"/>.</summary>
    public void Remove(uint playingId)
    {
        lock (_lock) _items.Remove(playingId);
    }

    /// <summary>The pool allocation <c>0xA7A7F4</c> of the 0x50-byte entry in <see cref="CreateEntryA03108"/> (<c>0xA03140</c>); true fails it. Null: it never fails.</summary>
    public Func<bool>? AllocationFails { get; set; }

    /// <summary>
    /// The mask <c>0xA03108</c> applies to the flags when the callback is null (<c>0xA031C8 cmp sb,#0; biceq sl,sl,#0xfd000; biceq #0xff0; biceq #0xb</c>, C40.1).
    /// </summary>
    public const uint CallbacklessFlagMask = 0xFD000u | 0xFF0u | 0xBu;

    /// <summary>
    /// <c>0xA03108(mgr, P, callback, cookie, flags, eventId)</c> (C40.1, T-E1d), the writer of a playing-id entry. Under the manager lock <c>mgr+0x10</c> it allocates the 0x50-byte entry (<c>0xA7A7F4</c>; null returns 2), zero-fills it and calls <c>0xA1C63C(item)</c> (0xA1C63C..0xA1C658 only zeroes <c>[+0..+0x14]</c>, which the zero-fill already did: nothing to model), then writes <c>[+0x3C]</c> the playing id, <c>[+0x20]</c> the event id, <c>[+0x24]</c> the game object id, <c>[+0x18] = 0</c>, <c>[+0x1C] = 1</c> (the in-flight message), <c>[+0x30/+0x34/+0x38]</c> the external-source words, <c>[+0x28]</c> the holder (its
    /// reference word incremented when non-null), <c>[+0x44]</c> the cookie, <c>[+0x40]</c> the callback and <c>[+0x48]</c> the flags (with <see cref="CallbacklessFlagMask"/> cleared when the callback is null), and links it into the bucket table (here a dictionary: the bucket order and the growth
    /// <c>0xA03244..0xA03294</c>, <c>0xA031F4..0xA033E0</c> are not observable). Returns 1. A growth allocation failure (<c>0xA033E0</c>) leaves the entry linked and still returns 1 (the table keeps its old buckets), which a dictionary cannot show.
    /// </summary>
    /// <param name="playingId"><c>[P+8]</c>, the id the Post's atomic counter produced.</param>
    /// <param name="eventId"><c>[sp+0x34]</c>, <c>[[msg+0x28]+8]</c> in <c>0x9A0EF8</c>.</param>
    /// <param name="gameObjectId"><c>[P]</c>; null leaves <c>[+0x24]</c> unset (see <see cref="WwiseEventItem.GameObject24"/>).</param>
    /// <param name="callback"><c>[+0x40]</c>.</param>
    /// <param name="cookie"><c>[+0x44]</c>.</param>
    /// <param name="flags"><c>[+0x48]</c> before the mask.</param>
    /// <param name="holder"><c>[P+0x10]</c> (<c>[+0x28]</c>): null on every Cozmo Post.</param>
    /// <param name="externalWords"><c>[P+0x18]</c>, <c>[P+0x1C]</c>, <c>[P+0x20]</c>: all 0 on every Cozmo Post.</param>
    // fidelity: M6-006, M6-016
    public int CreateEntryA03108(uint playingId, uint eventId, uint? gameObjectId, Action<int, object>? callback, object? cookie, uint flags,
                                 WwiseExternalSourceHolder? holder = null, (uint W30, uint W34, uint W38) externalWords = default)
    {
        lock (_lock)
        {
            if (AllocationFails?.Invoke() == true) return 2;                        // 0xA03140 bl 0xA7A7F4; 0xA03144 subs r4,r0,#0; 0xA03148 moveq r7,#2
            var item = new WwiseEventItem { PlayingId = playingId };               // zero-filled 0x50 bytes (0xA03150..0xA03158); 0xA1C63C(item) zeroes [+0..+0x14] again (0xA1C63C..0xA1C658)
            item.EventId20 = eventId;                                              // 0xA03190 str lr,[r4,#0x20]
            if (gameObjectId is { } go) item.GameObject24 = go;                    // 0xA03194 str ip,[r4,#0x24]
            item.Count18 = 0;                                                      // 0xA03198
            item.Count1C = 1;                                                      // 0xA0319C
            item.Word30 = externalWords.W30;                                       // 0xA031BC str r1,[r4,#0x30]
            item.Word34 = externalWords.W34;                                       // 0xA031B4 str r3,[r4,#0x34]
            item.Word38 = externalWords.W38;                                       // 0xA031B0 str r0,[r4,#0x38]
            if (holder is not null) holder.RefCount++;                             // 0xA031B8..0xA031C4 ldrne r3,[r2]; addne r3,r3,#1; strne r3,[r2]
            item.Object28 = holder;                                                // 0xA031D8 str r2,[r4,#0x28]
            item.Cookie44 = cookie;                                                // 0xA031D0
            item.Callback40 = callback;                                            // 0xA031E8
            item.Flags48 = callback is null ? flags & ~CallbacklessFlagMask : flags;   // 0xA031C8..0xA031E0
            _items[playingId] = item;                                              // 0xA03244..0xA03294: link into the bucket id mod [mgr+4]; [mgr+0xC]++
            return 1;
        }
    }

    /// <summary>The entry for a playing id, created when absent (a host or test that makes an entry without the Post; the Post itself is <see cref="CreateEntryA03108"/>).</summary>
    public WwiseEventItem GetOrCreate(uint playingId)
    {
        lock (_lock)
        {
            if (!_items.TryGetValue(playingId, out var item))
                _items[playingId] = item = new WwiseEventItem { PlayingId = playingId };
            return item;
        }
    }

    /// <summary>
    /// <c>0xA04D48(mgr, id, pbi, &amp;pbi+4)</c> (R1.5): a null PBI returns 2. Otherwise, under the lock, an entry for the playing id gets <c>[item+0x18]++</c> and its <c>[item+0x48]</c> is stored to <c>pbi+4</c>; a missing entry changes
    /// nothing. It returns 1 either way.
    /// </summary>
    public int RegisterPbiA04D48(WwisePlayingInstance? pbi)
    {
        if (pbi is null) return 2;                                                  // 0xA04D48 cmp r2,#0; beq 0xA04DC8
        lock (_lock)
        {
            if (_items.TryGetValue(pbi.PlayingId, out var item))                    // 0xA04D6C..0xA04DB4
            {
                item.Count18++;                                                     // 0xA04DD4..0xA04DDC
                pbi.Flags4 = item.Flags48;                                          // 0xA04DD0, 0xA04DE0 str r1,[r6]
            }
        }
        return 1;                                                                   // 0xA04DC0
    }

    /// <summary>
    /// <c>0xA04DE8(mgr, id)</c> (Term step 5, <c>0xA02C98</c>): an entry for the playing id gets <c>[item+0x18]--</c> and the tail call <c>0xA03618(mgr, id, item)</c>; a missing entry only unlocks.
    /// </summary>
    public void ReleaseA04DE8(uint playingId)
    {
        WwiseEventItem? item;
        lock (_lock) _items.TryGetValue(playingId, out item);
        if (item is null) return;                                                   // 0xA04E00..0xA04E24
        item.Count18--;                                                             // 0xA04E48..0xA04E5C
        EndOfEventA03618(item, playingId);                                          // 0xA04E64 b 0xA03618
    }

    /// <summary>
    /// <c>0xA03618(mgr, id, item)</c> (R4.1): nothing when <c>[item+0x18]</c> or <c>[item+0x1C]</c> is non-zero. Otherwise: flags <c>[item+0x48]</c> (with <c>0x400000</c> <c>0xA05934</c> runs first and the flags are re-read); the entry is unlinked and
    /// <c>[mgr+0xC]</c> decremented; the info <c>{cookie, game object, id, event id}</c> is built; the game object is looked up (<c>0xA0C238</c>) and, when found, its low 30 bits at <c>[obj+0x7C]</c> are decremented (<c>(x - 1) &amp; 0x3FFFFFFF</c>,
    /// the top two bits kept); a count of zero runs <c>0xA0B600</c> and frees the object; the entry is torn down (<c>0xA1C660</c>, <c>0x9A6988([item+0x28])</c> when non-null, <c>0xA1C65C</c>, the free); and with flag bit 0 the callback
    /// <c>[item+0x40](1, &amp;info)</c> runs between the manager's two locks (<c>[mgr+0x1C] = 0</c> before, 1 and a broadcast after).
    /// </summary>
    public void EndOfEventA03618(WwiseEventItem item, uint playingId)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Count18 != 0 || item.Count1C != 0) return;                         // 0xA03618..0xA03644 -> 0xA03768
        uint flags = item.Flags48;                                                  // 0xA03648
        if ((flags & 0x400000) != 0)                                                // 0xA03650 tst r7,#0x400000
        {
            (Seams.A05934 ?? throw Missing("0xA05934", "0xA037F4"))();
            flags = item.Flags48;                                                   // 0xA037F8
        }
        // 0xA03658..0xA03678: the info block {cookie, game object, id, event id} and the callback are loaded BEFORE the unlink and the teardown; an unset field throws here with the table unchanged.
        object? cookie = item.Cookie44;                                                // 0xA0365C
        uint gameObject = item.GameObject24;                                        // 0xA03660
        uint eventId = item.EventId20;                                              // 0xA03668
        var callback = item.Callback40;                                             // 0xA03678
        uint itemPlayingId = item.PlayingId;                                        // 0xA036C4 ldr ip,[r4,#0x3c] (info +0x14)
        lock (_lock) _items.Remove(playingId);                                  // 0xA03680..0xA03788: the unlink by the id argument (r5, 0xA03680 mov r0,r5) and [mgr+0xC]--
        var found = (Seams.GameObjectLookupA0C238 ?? throw Missing("0xA0C238 (the game-object lookup)", "0xA036F8"))(gameObject);   // 0xA036F8 (r1 = the loaded [item+0x24])
        if (found is not null)                                                      // 0xA03700 subs r5,r0,#0; beq 0xA03800
        {
            uint word = found.Word7C;                                               // 0xA03704
            uint count = unchecked(word - 1u) & 0x3FFFFFFFu;                        // 0xA0370C..0xA03710
            found.Word7C = (word & 0xC0000000u) | count;                            // 0xA03714 bfi r3,r2,#0,#0x1e; 0xA03718
            if (count == 0)                                                         // 0xA03720 cmp r2,#0; beq 0xA03818
                (Seams.A0B600 ?? throw Missing("0xA0B600", "0xA0381C"))(found);
        }
        (Seams.A1C660 ?? throw Missing("0xA1C660", "0xA03734"))(item);              // 0xA03734
        if (item.Object28 is { } o)                                                 // 0xA03738..0xA03748
            (Seams.A9A6988 ?? throw Missing("0x9A6988", "0xA03748"))(o);
        (Seams.A1C65C ?? throw Missing("0xA1C65C", "0xA03750"))(item);              // 0xA03750 (then the pool free)
        if ((flags & 1) == 0) return;                                               // 0xA03760 tst r7,#1; bne 0xA03790
        if (callback is null) throw new InvalidOperationException("M6-026 R4.1: [item+0x48] bit 0 is set and [item+0x40] is null; the engine calls it (0xA037BC blx r8)");
        var info = new WwiseEndOfEventInfo(cookie, gameObject, playingId, eventId, found, itemPlayingId);   // the pre-teardown snapshot (sp+0..+0x14)
        CallbackIdle1C = false;                                                     // 0xA0379C..0xA037A4
        callback(1, info);                                                          // 0xA037B4..0xA037BC blx r8(1, &info), outside both locks
        CallbackIdle1C = true;                                                      // 0xA037C8..0xA037D0
        Broadcasts++;                                                               // 0xA037D4 pthread_cond_broadcast
    }

    /// <summary>
    /// <c>0xA0393C(mgr, id, duration, estimated, nodeId, mediaId, streaming)</c> (R4.7): nothing unless an entry for the id has <c>[item+0x48]</c> bit 3; then the callback <c>[item+0x40](8, &amp;info)</c>
    /// with the info <c>{cookie, game object, id, event id, duration, estimated duration, node id, media id, streaming}</c> runs between the manager's locks, as <see cref="EndOfEventA03618"/> does.
    /// </summary>
    public void DurationA0393C(uint playingId, float duration, float estimated, uint nodeId, uint mediaId, bool streaming)
    {
        WwiseEventItem? item;
        lock (_lock) _items.TryGetValue(playingId, out item);
        if (item is null || (item.Flags48 & 8) == 0) return;                        // 0xA03988..0xA039B4
        var callback = item.Callback40 ?? throw new InvalidOperationException("M6-026 R4.7: [item+0x48] bit 3 is set and [item+0x40] is null; the engine calls it (0xA03A30 blx r4)");
        var info = new WwiseDurationInfo(item.Cookie44, item.GameObject24, playingId, item.EventId20, duration, estimated, nodeId, mediaId, streaming);   // 0xA039C8..0xA03A08
        CallbackIdle1C = false;                                                     // 0xA03A10..0xA03A18
        callback(8, info);                                                          // 0xA03A28..0xA03A30
        CallbackIdle1C = true;                                                      // 0xA03A3C..0xA03A44
        Broadcasts++;                                                               // 0xA03A48
    }

    /// <summary>
    /// <c>0xA0428C(mgr, playingId)</c> (C40.1 T-E4c; the call <c>0xA55C14</c> / <c>0xA54580</c> / <c>0xA545B0</c> make; the third register is not read): under <c>mgr+0x10</c> it looks the entry up; no entry, a null <c>[+0x40]</c> or <c>[+0x48] &amp; 0x20 == 0</c> returns without calling anything. Otherwise
    /// it builds <c>{cookie [+0x44], game object [+0x24], playing id, event id [+0x20]}</c> and calls <c>callback(0x20, &amp;info)</c> between the manager's two locks (<c>[mgr+0x1C] = 0</c> before, 1 and a broadcast after), as <see cref="DurationA0393C"/> does. Anki never sets
    /// <c>0x20</c> (its flags are 0, 1, 5, 9 or 13), so on the Cozmo path this never calls.
    /// </summary>
    // fidelity: M6-025
    public void NodeNotificationA0428C(uint playingId)
    {
        WwiseEventItem? item;
        lock (_lock) _items.TryGetValue(playingId, out item);
        if (item is null) return;                                                   // 0xA042C4 cmp r3,#0; beq 0xA04308 (the bucket walk 0xA042A8..0xA042EC)
        if (item.Callback40 is not { } callback) return;                            // 0xA042F0..0xA042F8 ldr r7,[r3,#0x40]; cmp r7,#0; beq
        if ((item.Flags48 & 0x20) == 0) return;                                     // 0xA042FC..0xA04304 tst r2,#0x20; bne 0xA04318
        var info = new WwiseNodeNotifyInfo(item.Cookie44, item.GameObject24, playingId, item.EventId20);   // 0xA04318..0xA04330
        CallbackIdle1C = false;                                                     // 0xA04338..0xA04340 strb r3,[r5,#0x1c] (r3 = 0)
        callback(0x20, info);                                                       // 0xA04350..0xA04358 mov r0,#0x20; blx r7, outside both locks
        CallbackIdle1C = true;                                                      // 0xA04364..0xA0436C
        Broadcasts++;                                                               // 0xA04370 pthread_cond_broadcast
    }

    private static WwiseMissingBehaviourException Missing(string what, string at)
        => new($"M6-026 R4.1: {what} (called at {at} by 0xA03618) is unread; supply the matching WwiseEndOfEventSeams member");
}
