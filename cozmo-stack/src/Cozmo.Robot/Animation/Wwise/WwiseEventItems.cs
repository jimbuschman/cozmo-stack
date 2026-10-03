// fidelity: M6-025, M6-026, M6-017
namespace Cozmo.Robot.Animation.Wwise;

// The playing-id entries of the callback manager (the object <c>*GOT</c> that <c>0xA02910</c>, <c>0xA02C98</c> and <c>0xA56464</c> load as <c>mgr</c>): the PBI's reference <c>0xA04D48</c>, its release
// <c>0xA04DE8</c> and the EndOfEvent / Duration callbacks <c>0xA03618</c> and <c>0xA0393C</c> (C31.1 R1.5, C31.4 R4.1, R4.2, R4.7). The writer of the entry's callback fields (the PostEvent flag
// registration) is unread; the entries are created by the control path (WwiseEventRuntime) and by a host that registers callbacks.

/// <summary>A game object the lookup <c>0xA0C238</c> returns: only the reference word <c>[obj+0x7C]</c> (low 30 bits a count, the top two bits kept) is touched here.</summary>
public sealed class WwiseGameObjectRef
{
    /// <summary><c>[obj+0x7C]</c>.</summary>
    public uint Word7C { get; set; }
}

/// <summary>The info block <c>0xA03618</c> builds at <c>sp+0</c> for the EndOfEvent callback (type 1): cookie, game object, playing id, event id, then the looked-up game object and <c>[item+0x3C]</c>.</summary>
/// <param name="Cookie"><c>[item+0x44]</c>.</param>
/// <param name="GameObject"><c>[item+0x24]</c>.</param>
/// <param name="PlayingId">The playing id.</param>
/// <param name="EventId"><c>[item+0x20]</c>.</param>
/// <param name="GameObjectRef">The game object <c>0xA0C238</c> found (<c>[sp+0x10]</c>), or null.</param>
/// <param name="Key3C"><c>[item+0x3C]</c> (<c>[sp+0x14]</c>).</param>
public sealed record WwiseEndOfEventInfo(uint Cookie, uint GameObject, uint PlayingId, uint EventId, WwiseGameObjectRef? GameObjectRef, uint Key3C);

/// <summary>The info block <c>0xA0393C</c> builds for the Duration callback (type 8).</summary>
public sealed record WwiseDurationInfo(uint Cookie, uint GameObject, uint PlayingId, uint EventId, float Duration, float EstimatedDuration, uint NodeId, uint MediaId, bool Streaming);

/// <summary>One playing-id entry (<c>0x3C</c> key, <c>0x18</c> and <c>0x1C</c> counters, <c>0x20</c> event id, <c>0x24</c> game object, <c>0x28</c> object, <c>0x40</c> callback, <c>0x44</c> cookie, <c>0x48</c> flags).</summary>
public sealed class WwiseEventItem
{
    /// <summary><c>+0x3C</c>: the playing id.</summary>
    public uint PlayingId { get; init; }

    /// <summary><c>+0x18</c>: references the PBIs hold (<c>0xA04D48</c> increments, <c>0xA04DE8</c> decrements).</summary>
    public int Count18 { get; set; }

    /// <summary><c>+0x1C</c>: outstanding messages and actions (<c>0xA04EDC</c> / <c>0xA04F54</c>).</summary>
    public int Count1C { get; set; }

    /// <summary><c>+0x20</c>: the event id.</summary>
    public uint EventId20
    {
        get => _eventId20 ?? throw new WwiseMissingBehaviourException("M6-026 R4.1: [item+0x20] (the event id) has no adopted writer (C31.4 R4.1 gives only readers); set WwiseEventItem.EventId20 from the host seam");
        set => _eventId20 = value;
    }
    private uint? _eventId20;

    /// <summary><c>+0x24</c>: the game object id.</summary>
    public uint GameObject24
    {
        get => _gameObject24 ?? throw new WwiseMissingBehaviourException("M6-026 R4.1: [item+0x24] (the game object id) has no adopted writer (C31.4 R4.1 gives only readers); set WwiseEventItem.GameObject24 from the host seam");
        set => _gameObject24 = value;
    }
    private uint? _gameObject24;

    /// <summary><c>+0x28</c>: an object <c>0x9A6988</c> releases at the end (null is none).</summary>
    public object? Object28 { get; set; }

    /// <summary><c>+0x40</c>: the callback; null when none was registered.</summary>
    public Action<int, object>? Callback40 { get; set; }

    /// <summary><c>+0x44</c>: the callback cookie.</summary>
    public uint Cookie44 { get; set; }

    /// <summary>
    /// <c>+0x48</c>: the callback flags (the Wwise <c>AkCallbackType</c> values): bit 0 (0x1) EndOfEvent, bit 3 (0x8) Duration, bit 4 (0x10) speaker-volume matrix, 0x100000 get-source-play-position,
    /// 0x400000 stream buffering (R4.2). Its writer is unread.
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

    /// <summary>The entry for a playing id, created when absent (the PostEvent registration, whose writer is unread).</summary>
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
        uint cookie = item.Cookie44;                                                // 0xA0365C
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

    private static WwiseMissingBehaviourException Missing(string what, string at)
        => new($"M6-026 R4.1: {what} (called at {at} by 0xA03618) is unread; supply the matching WwiseEndOfEventSeams member");
}
