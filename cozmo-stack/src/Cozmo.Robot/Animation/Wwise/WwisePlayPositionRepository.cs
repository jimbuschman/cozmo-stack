// fidelity: M6-025, M6-026
namespace Cozmo.Robot.Animation.Wwise;

// The play-position repository (G = *0x108D8F8, GOT 0x1040150; C31.4 R4.3, R5.7): 32-byte records <c>{playing id, source, int64 stamp, 16 info bytes}</c> at <c>[G]</c>, the count <c>[G+4]</c>, the capacity <c>[G+8]</c>, the lock
// <c>G+0x18</c> and the int64 clock stamp <c>[G+0x20]</c>. The bodies are 0xA05370 (add), 0xA054D8 (remove), 0xA05574 (update) and the voice pass prelude 0xA44948..0xA44968 (the stamp refresh).

/// <summary>One 32-byte record of the repository.</summary>
public sealed class WwisePlayPositionRecord
{
    /// <summary><c>+0</c>: the playing id.</summary>
    public uint PlayingId { get; init; }

    /// <summary><c>+4</c>: the source.</summary>
    public IWwiseVoiceSource? Source { get; init; }

    /// <summary><c>+8</c> (int64): the clock stamp. <c>0xA05370</c> does not store it (uninitialised pool memory in the engine); 0 here until an update stamps it.</summary>
    public long Stamp8 { get; set; }

    /// <summary><c>+0x10</c>: <c>-1</c> at creation.</summary>
    public uint Word10 { get; set; } = 0xFFFFFFFF;

    /// <summary><c>+0x14</c>: 1.0f at creation.</summary>
    public uint Word14 { get; set; } = 0x3F800000;

    /// <summary><c>+0x18</c>: <c>-1</c> at creation.</summary>
    public uint Word18 { get; set; } = 0xFFFFFFFF;

    /// <summary><c>+0x1C</c>: 1 at creation.</summary>
    public uint Word1C { get; set; } = 1;
}

/// <summary>The play-position repository: <c>0xA05370</c>, <c>0xA054D8</c>, <c>0xA05574</c> and the stamp refresh of the voice pass prelude.</summary>
public sealed class WwisePlayPositionRepository
{
    private readonly Func<int> _clock;
    private readonly object _lock = new();                                           // G+0x18

    /// <param name="clock">The host's <c>clock()</c> (<c>0x4D3658</c>): the process CPU time. The engine stores it sign-extended to 64 bits.</param>
    public WwisePlayPositionRepository(Func<int> clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary><c>[G]</c>/<c>[G+4]</c>: the records in order.</summary>
    public List<WwisePlayPositionRecord> Records { get; } = new();

    /// <summary><c>[G+8]</c>: the capacity; grown by 1 when full (<c>0xA05440</c>, <c>0xA056B8</c>).</summary>
    public int Capacity { get; set; }

    /// <summary><c>[G+0x20]</c> (int64): the clock stamp.</summary>
    public long Stamp20 { get; set; }

    /// <summary>The pool allocation failure (<c>0xA7A7F4</c> returning null) of the growth: true fails that one allocation.</summary>
    public Func<bool>? AllocationFails { get; set; }

    private WwisePlayPositionRecord? Find(uint playingId, IWwiseVoiceSource source)
    {
        foreach (var r in Records)
            if (r.PlayingId == playingId && ReferenceEquals(r.Source, source)) return r;
        return null;
    }

    /// <summary>
    /// <c>0xA05370(G, id, source)</c>: a record for the pair returns at once. Otherwise, under the lock, a full table grows by one record (an allocation failure returns without adding) and a record <c>{id, source, +0x10 = -1, +0x14 = 1.0f,
    /// +0x18 = -1, +0x1C = 1}</c> is appended.
    /// </summary>
    public void AddA05370(uint playingId, IWwiseVoiceSource source)
    {
        if (Find(playingId, source) is not null) return;                             // 0xA05370..0xA053C8
        lock (_lock)
        {
            if (!Grow()) return;                                                     // 0xA053E8..0xA05440, 0xA05464
            Records.Add(new WwisePlayPositionRecord { PlayingId = playingId, Source = source });   // 0xA053F8..0xA0542C
        }
    }

    /// <summary>
    /// <c>0xA054D8(G, id, source)</c>: the record for the pair is removed (the records after it shift down) and the count drops, under the lock; no record changes nothing.
    /// </summary>
    public void RemoveA054D8(uint playingId, IWwiseVoiceSource source)
    {
        var rec = Find(playingId, source);
        if (rec is null) return;                                                      // 0xA054DC..0xA05518
        lock (_lock) Records.Remove(rec);                                             // 0xA0551C..0xA05564
    }

    /// <summary>
    /// <c>0xA05574(G, id, info16, source)</c>: a record for the pair takes the 16 info bytes and the current stamp <c>[G+0x20]</c> (no clock call). Otherwise, under the lock, the pair is searched again, and when still absent a record is added
    /// (growing as above; an allocation failure ends the update) with <c>{id, source}</c>; then the count is tested: a non-zero count (always after an add) stores <c>(int64)(int32)clock()</c> to <c>[G+0x20]</c> first, and the record takes the stamp
    /// and the info bytes.
    /// </summary>
    public void UpdateA05574(uint playingId, uint word10, uint word14, uint word18, uint word1C, IWwiseVoiceSource source)
    {
        var rec = Find(playingId, source);
        if (rec is null)
        {
            lock (_lock)
            {
                rec = Find(playingId, source);                                        // 0xA05618..0xA05684
                if (rec is null)
                {
                    if (!Grow()) return;                                              // 0xA0562C..0xA056B0
                    rec = new WwisePlayPositionRecord { PlayingId = playingId, Source = source };   // 0xA05648..0xA05650
                    Records.Add(rec);                                                 // [G+4] = count + 1 (0xA05640)
                }
                if (Records.Count != 0) Stamp20 = unchecked((long)_clock());          // 0xA05694..0xA056A8: vdup.32 / vshr.s64 #0x20 sign-extends the 32-bit clock
            }
        }
        rec.Stamp8 = Stamp20;                                                         // 0xA055E8, 0xA055FC
        rec.Word10 = word10; rec.Word14 = word14; rec.Word18 = word18; rec.Word1C = word1C;   // 0xA055EC..0xA055F4
    }

    /// <summary>
    /// The voice pass prelude (<c>0xA44948..0xA44968</c>, R5.7): with a non-zero record count <c>[G+0x20] = (int64)(int32)clock()</c> (<c>0xA44BD0</c>).
    /// </summary>
    public void PreludeA44948()
    {
        if (Records.Count != 0) Stamp20 = unchecked((long)_clock());
    }

    private bool Grow()
    {
        if (Records.Count < Capacity) return true;                                    // 0xA053EC cmp r7,r3; bhs 0xA05440
        if (AllocationFails?.Invoke() == true) return false;                          // 0xA0545C bl 0xA7A7F4; 0xA05464 beq
        Capacity++;                                                                   // 0xA05444 add r3,r3,#1 ... 0xA054C4 str r3,[r5,#8]
        return true;
    }
}
