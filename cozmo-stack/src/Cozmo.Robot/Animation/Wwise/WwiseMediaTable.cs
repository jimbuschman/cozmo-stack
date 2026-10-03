// fidelity: M6-025, M6-026
namespace Cozmo.Robot.Animation.Wwise;

// The media table (M6-025 C31.1, C32.2): the bank loader's DIDX and DATA handlers for mode 0, the writer 0x9B49A4 that fills the media hash of the
// sound-bank manager BM (the object at 0x108D8D8), the lookup 0xA1EC54 / 0x9BB320 / 0x9BB1F8 and the release 0xA1ECBC / 0x9B65A8, which feed and
// release the pair pbi+0x1DC / pbi+0x1E0 (and pbi+0x108).
//
// Production entry. Engine: Anki LoadSoundbank -> 0x9A33D8 posts command 0 -> BM vt+0x10 = 0x9BB65C -> 0x9BB834 -> 0x9B7CD4 -> the loader 0x9B74D8 (type 0,
// mode 0). The C# counterpart is WwiseBankLoader.LoadMode0, but nothing in production constructs or calls it yet (the bank-load wiring, C30.W, is parked); it is
// exercised by tests only. WwisePbiMedia.StoreSourceInfoA02924 is called by the playback limiter's InsertPbiA0285C (0xA0285C), which has no production caller either
// until the Play path is wired.
//
// What this file does NOT own: the bank object's creation (0x9B7664..0x9B7760), the bank registry lookup 0xA68804, BKHD (0x9B21F4) and the HIRC / INIT / STMG /
// ENVS / PLAT / STID chunk bodies (0x9B3260, 0x9B4048, 0x9B0B14, 0x9B2988, 0x9B2B08, 0x9B2410). They are named seams (WwiseBankLoader.ChunkHandler); a chunk
// reaching an unset seam throws WwiseMissingBehaviourException.

/// <summary>
/// The host allocator the loader and the media table allocate through (<c>0xA7A7F4</c> / <c>0xA7A988</c> / <c>0xA7A914</c>) and the pool services the DATA handler
/// uses (<c>0xA7AC98</c>, <c>0xA7AAE8</c>, <c>0xA7A7C8</c>, <c>0xA7AA9C</c>, <c>0xA7A9FC</c>). An address is not engine behaviour, so it is a synthetic bump counter; what the callers
/// observe is the success or failure of each allocation, which <see cref="AllocationFails"/> drives. The pool bodies are not adopted, so the properties that decide
/// a pool's attributes have no default: reading one that is unset throws.
/// </summary>
public sealed class WwiseBankMemory
{
    private uint _next = 0x20000000;
    private readonly Dictionary<uint, byte[]> _blocks = new();
    private readonly List<(uint Size, uint Block)> _pools = new();

    /// <summary>The <c>0xA7A7F4</c> returning null: true fails that one allocation. Null means no allocation fails. Each call is one allocation attempt, in the engine's order.</summary>
    public Func<bool>? AllocationFails { get; set; }

    /// <summary>The attributes <c>0xA7A7C8</c> returns for a pool created by <c>0xA7AC98(0, size, size, 9, 0x10)</c>; the DATA handler tests bit 3 (<c>0x9B7BDC</c>). The pool body is unread, so unset throws.</summary>
    public uint? PoolAttributes { get; set; }

    /// <summary>The value <c>0xA7AA9C</c> returns (the DATA handler compares the chunk size to it when attributes bit 3 is set). Unset throws.</summary>
    public uint? PoolBlockSize { get; set; }

    /// <summary>The status <c>0xA7AAE8</c> returns for a pool; the DATA handler requires 1 (<c>0x9B7BC4</c>). Defaults to nothing: unset throws.</summary>
    public int? PoolCheckResult { get; set; }

    /// <summary>The id <c>0xA7AC98</c> returns, or -1 for failure (<c>0x9B7C90</c>). Unset throws.</summary>
    public int? PoolCreateResult { get; set; }

    /// <summary>Allocations that have not been freed.</summary>
    public int LiveBlocks => _blocks.Count;

    /// <summary><c>0xA7A7F4(pool, size)</c>: a block of <paramref name="size"/> bytes, or null when the allocation fails.</summary>
    public uint? Allocate(int size)
    {
        if (AllocationFails?.Invoke() == true) return null;
        uint address = _next;
        _next += (uint)((Math.Max(size, 1) + 15) & ~15);
        _blocks[address] = new byte[size];
        return address;
    }

    /// <summary><c>0xA7A988</c> / <c>0xA7A914</c>: returns a block.</summary>
    public void Free(uint address) => _blocks.Remove(address);

    /// <summary>The block's bytes (the buffer the engine's pointer addresses).</summary>
    public byte[] Block(uint address) => _blocks[address];

    /// <summary><c>0xA7AC98(0, size, size, 9, 0x10)</c> (<c>0x9B7C6C..0x9B7C90</c>): the id of a new pool, or -1.</summary>
    public int CreatePool(uint size)
    {
        int id = PoolCreateResult ?? throw new WwiseMissingBehaviourException(
            "M6-025 M5: 0xA7AC98 (the pool create the DATA handler calls at 0x9B7C88) is unread; set WwiseBankMemory.PoolCreateResult");
        if (id != -1) _pools.Add((size, 0));
        return id;
    }

    /// <summary><c>0xA7AAE8(pool)</c>.</summary>
    public int CheckPool(int pool)
        => PoolCheckResult ?? throw new WwiseMissingBehaviourException("M6-025 M5: 0xA7AAE8 is unread; set WwiseBankMemory.PoolCheckResult");

    /// <summary><c>0xA7A7C8(pool)</c>.</summary>
    public uint PoolAttributesOf(int pool)
        => PoolAttributes ?? throw new WwiseMissingBehaviourException("M6-025 M5: 0xA7A7C8 is unread; set WwiseBankMemory.PoolAttributes");

    /// <summary><c>0xA7AA9C(pool)</c>.</summary>
    public uint PoolBlockSizeOf(int pool)
        => PoolBlockSize ?? throw new WwiseMissingBehaviourException("M6-025 M5: 0xA7AA9C is unread; set WwiseBankMemory.PoolBlockSize");
}

/// <summary>One <c>{bank, data, size}</c> item of a media entry (12 bytes; <c>0x9B49A4</c>, <c>0x9BB1F8</c>).</summary>
public sealed class WwiseMediaItem
{
    /// <summary><c>+0</c>: the bank that wrote it.</summary>
    public required WwiseMediaBank Bank { get; init; }

    /// <summary><c>+4</c>: the pointer to the media bytes in the bank's DATA buffer (DATA + DIDX offset).</summary>
    public uint Data { get; set; }

    /// <summary><c>+8</c>: the size (DIDX size).</summary>
    public uint Size { get; set; }
}

/// <summary>
/// One hash node of the media table (0x24 bytes, <c>0x9B4B24..0x9B4B88</c>): <c>[0]</c> next, <c>[4]</c> key, <c>[8]</c> direct data pointer, <c>[0xC]</c> its size, <c>[0x10]</c> the items array,
/// <c>[0x14]</c> the item count, <c>[0x18]</c> the items capacity, <c>[0x1C]</c> the reference count, <c>[0x20]</c> the last writer's id.
/// </summary>
public sealed class WwiseMediaNode
{
    /// <summary><c>[0]</c>.</summary>
    public WwiseMediaNode? Next { get; set; }

    /// <summary><c>[4]</c>: the media id.</summary>
    public uint Key { get; init; }

    /// <summary><c>[8]</c>: the direct data pointer (0 for every node the writer creates; no writer of it exists in the inventory).</summary>
    public uint DirectData8 { get; set; }

    /// <summary><c>[0xC]</c>: the direct data size.</summary>
    public uint DirectSize0C { get; set; }

    /// <summary><c>[0x10]</c>..<c>[0x14]</c>: the items, newest first (<c>0x9B4C04..0x9B4C6C</c> shifts the old items up and writes at index 0). Empty with capacity 0 is the null array.</summary>
    public List<WwiseMediaItem> Items { get; } = new();

    /// <summary><c>[0x18]</c>: the items capacity (grown by 1, <c>0x9B4EBC</c>).</summary>
    public int ItemCapacity { get; set; }

    /// <summary><c>[0x1C]</c>: the reference count; 1 at creation.</summary>
    public int RefCount1C { get; set; } = 1;

    /// <summary><c>[0x20]</c>: the id the last <c>0x9B4B94</c> stored.</summary>
    public uint LastId20 { get; set; }
}

/// <summary>
/// The sound-bank object the loader creates (0x58 bytes, <c>0x9B7664..0x9B7760</c>), reduced to the fields the DIDX / DATA handlers, the writer, the lookup and the
/// release touch. The creation stores zero in <c>+0x14..+0x44</c>, 1 in <c>+0x48</c>, <c>+0x50</c> bit 0 and bit 1 clear, and <c>[+4]</c> bit 0 clear / bit 1 set; the pool id
/// <c>+0x24</c> is the loader's stack argument <c>[sp+0xA8]</c>, which <c>0x9A33D8</c> fills with its second argument (-1 from LoadSoundbank, M1). That creation is not an
/// adopted row, so the object is constructed by the caller with those values.
/// </summary>
public sealed class WwiseMediaBank
{
    /// <summary><c>+8</c>: the bank id.</summary>
    public uint Id { get; init; }

    /// <summary><c>+4</c> (byte). Bit 0 set skips the bank in a lookup (<c>0x9BB250</c>); bit 1 makes a pick take a reference on <c>+0x48</c> (<c>0x9BB2A8</c>).</summary>
    public byte Flags4 { get; set; } = 2;

    /// <summary><c>+0x14</c>: the DATA buffer.</summary>
    public uint DataBuffer14 { get; set; }

    /// <summary><c>+0x18</c>: the DIDX entries as read (<c>{id, offset, size}</c> u32 each); null when none is loaded.</summary>
    public byte[]? Didx18 { get; set; }

    /// <summary><c>+0x1C</c>: the DATA size.</summary>
    public uint DataSize1C { get; set; }

    /// <summary><c>+0x24</c>: the pool id; -1 for none.</summary>
    public int PoolId24 { get; set; } = -1;

    /// <summary><c>+0x28</c> (byte): set when the DATA handler creates the pool.</summary>
    public byte PoolFlag28 { get; set; }

    /// <summary><c>+0x2C</c>: the number of DIDX entries the writer has processed.</summary>
    public uint Counter2C { get; set; }

    /// <summary><c>+0x30</c>: the number of DIDX entries.</summary>
    public uint Count30 { get; set; }

    /// <summary><c>+0x48</c>: the reference count (atomic in the engine); 1 at creation.</summary>
    public int RefCount48 { get; set; } = 1;

    /// <summary><c>+0x50</c> (byte): bit 1 is set by the DIDX handler.</summary>
    public byte Flags50 { get; set; } = 1;

    /// <summary>The media table the bank is registered in (set by the loader), for <see cref="ReleaseVt0A9B47D8"/>'s lock.</summary>
    internal object? Gate { get; set; }

    /// <summary>
    /// Bank <c>vt+0</c> = <c>0x9B47D8(bank, 0)</c> (the vtable word at <c>0x101C028</c>; <c>pbi+0x108</c> release, <c>0xA02B00..0xA02B0C</c>): under the global lock (<c>0x108E330</c>) the
    /// reference count <c>+0x48</c> is decremented; a result above zero returns (<c>0x9B481C ble</c> not taken). A result of zero or below continues at <c>0x9B4830</c>, the unload path, which the
    /// inventory does not read (M9), so it throws.
    /// </summary>
    public void ReleaseVt0A9B47D8()
    {
        lock (Gate ?? this)
        {
            RefCount48--;
            if (RefCount48 > 0) return;                                           // 0x9B4814 cmp r2,#0; 0x9B481C ble 0x9B4830 not taken
        }
        throw new WwiseMissingBehaviourException(
            "M6-025 M9: the bank object's unload path below 0x9B4830 (taken when [bank+0x48] reaches zero) is unread; the inventory settles only the decrement");
    }
}

/// <summary>The result of <c>0xA1EC54</c>: the pair for <c>pbi+0x1DC</c>, <c>pbi+0x1E0</c> and the bank (<c>*r3</c>) when the lookup picked one.</summary>
public readonly record struct WwiseMediaLookup(uint Data, uint Size, WwiseMediaBank? Bank);

/// <summary>
/// The media hash of the sound-bank manager BM (<c>[BM+0x34]</c> buckets, <c>[BM+0x38]</c> the bucket count, <c>[BM+0x3C]</c> its capacity, <c>[BM+0x40]</c> the node count), with the writer
/// <c>0x9B49A4</c> and the lookup / release the PBI path uses. The BM constructor <c>0x9B1B50</c> zeroes <c>+0x34..+0x40</c>.
/// </summary>
public sealed class WwiseMediaTable
{
    /// <summary>The prime table at <c>0xFFA248</c> (27 words, read by <c>0x9B4A8C..0x9B4AA8</c>).</summary>
    public static readonly uint[] PrimeTable =
    {
        0x1D, 0x35, 0x61, 0xC1, 0x185, 0x301, 0x607, 0xC07, 0x1807, 0x3001, 0x6011, 0xC005, 0x1800D, 0x30005, 0x60019, 0xC0001, 0x180005, 0x30000B, 0x60000D,
        0xC00005, 0x1800013, 0x3000005, 0x6000017, 0xC000013, 0x18000005, 0x30000059, 0x60000005,
    };

    /// <summary>0.9f, the load-factor limit (<c>0x9B4CF4</c>: <c>0x3F666666</c>).</summary>
    public const int LoadLimitBits = 0x3F666666;

    private readonly object _gate = new();                                         // the BM+0x2C mutex and the global mutex 0x108E330 (one lock: no observable difference single-threaded)
    private WwiseMediaNode?[]? _buckets;                                           // [BM+0x34]

    /// <param name="memory">The host allocator.</param>
    public WwiseMediaTable(WwiseBankMemory memory) => Memory = memory ?? throw new ArgumentNullException(nameof(memory));

    /// <summary>The allocator the table allocates nodes, items and bucket arrays from.</summary>
    public WwiseBankMemory Memory { get; }

    /// <summary><c>[BM+0x38]</c>.</summary>
    public uint BucketCount { get; private set; }

    /// <summary><c>[BM+0x3C]</c>.</summary>
    public uint BucketCapacity { get; private set; }

    /// <summary><c>[BM+0x40]</c>.</summary>
    public uint NodeCount { get; private set; }

    /// <summary>The node for a key, or null (a chain walk, <c>0x9BB370..0x9BB3A0</c>).</summary>
    public WwiseMediaNode? Find(uint key)
    {
        if (BucketCount == 0 || _buckets is null) return null;
        for (var n = _buckets[key % BucketCount]; n is not null; n = n.Next)
            if (n.Key == key) return n;
        return null;
    }

    internal object Gate => _gate;

    // ---------------------------------------------------------------- the writer 0x9B49A4

    /// <summary>
    /// <c>0x9B49A4(BM, dataBase, bank)</c> (C32.2 M6): for each DIDX entry of the bank (<c>{id, offset, size}</c>; an id of 0 is skipped) it finds or creates the hash node for the id and
    /// puts <c>{bank, dataBase + offset, size}</c> at the front of the node's items, overwriting the bank's existing item in place; <c>[bank+0x2C]</c> counts every entry. Returns 1, or an allocation-failure
    /// result as the engine leaves it: <c>[sp+8]</c> starts at 0x34 and a processed entry sets it to 1, so a failure after at least one processed entry leaves 1 (<c>0x9B4C84</c> returns it without the
    /// <c>0x9B45D8</c> cleanup), and a failure before any leaves 0x34, which calls the cleanup. That cleanup body is not adopted: reaching it throws.
    /// </summary>
    public int WriteBankA9B49A4(WwiseMediaBank bank, uint dataBase)
    {
        ArgumentNullException.ThrowIfNull(bank);
        bank.Gate = _gate;
        int ret = 0x34;                                                           // [sp+8], 0x9B4A0C
        if (bank.Didx18 is null) return Cleanup(bank, ret);                      // 0x9B49D0 beq 0x9B4F98
        uint n = bank.Count30;                                                    // r4, 0x9B49D4
        if (n == 0) return 1;                                                     // 0x9B49DC beq 0x9B4E90
        uint i = 0;
        while (true)
        {
            int p = (int)(i * 12);
            uint id = BitConverter.ToUInt32(bank.Didx18, p);                      // 0x9B4A3C ldr r3,[sl]
            if (id != 0)
            {
                bool failed;
                lock (_gate)
                    (ret, failed) = WriteEntry(bank, dataBase, bank.Didx18, p, id, ret);
                if (failed)                                                        // 0x9B4C6C: unlock, r4 = [bank+0x30]
                {
                    if (bank.Count30 == i) return 1;                               // 0x9B4C7C beq 0x9B4E90 (i < n, so not taken)
                    return ret == 1 ? 1 : Cleanup(bank, ret);                      // 0x9B4C88 cmp r3,#1; beq 0x9B4C9C
                }
                n = bank.Count30;                                                  // 0x9B4B1C ldr r4,[r8,#0x30]
            }
            i++;                                                                   // 0x9B4A18
            bank.Counter2C++;                                                      // 0x9B4A1C..0x9B4A28
            if (i >= n) break;                                                     // 0x9B4A20 cmp r7,r4; bhs 0x9B4CE4
        }
        return n == i ? 1 : (ret == 1 ? 1 : Cleanup(bank, ret));                  // 0x9B4CE4 cmp r4,r7; bne 0x9B4C84 / b 0x9B4E90
    }

    private int Cleanup(WwiseMediaBank bank, int ret)
        => throw new WwiseMissingBehaviourException(
            "M6-025 M6: the writer's failure path calls 0x9B45D8 (the cleanup that unregisters the bank's entries); its body is not adopted. " +
            $"The writer's own return value would be {ret:X}");

    /// <summary>One DIDX entry of the writer, with the BM lock held (0x9B4A48..0x9B4CE0). Returns the new <c>[sp+8]</c> and whether the entry jumped to the failure exit <c>0x9B4C6C</c>.</summary>
    private (int Ret, bool Failed) WriteEntry(WwiseMediaBank bank, uint dataBase, byte[] didx, int p, uint id, int ret)
    {
        uint offset = BitConverter.ToUInt32(didx, p + 4);
        uint size = BitConverter.ToUInt32(didx, p + 8);
        uint count = BucketCount;                                                  // r3
        bool rehash = count == 0 || (float)NodeCount / (float)count > BitConverter.Int32BitsToSingle(LoadLimitBits);   // 0x9B4A60..0x9B4A84
        if (rehash)
        {
            uint prime = 0;
            foreach (uint candidate in PrimeTable)                                 // 0x9B4A8C..0x9B4AA8: the first prime above the count
                if (count < candidate) { prime = candidate; break; }
            if (prime != 0)
                count = Rehash(prime, count);                                      // 0x9B4D04..0x9B5038, 0x9B4E68
            if (count == 0) return (ret, true);                                    // 0x9B4AB0 cmp r3,#0; beq 0x9B4C6C
        }

        uint index = id % count;                                                   // 0x9B4AC0 __aeabi_uidivmod
        var node = _buckets![index];
        while (node is not null && node.Key != id) node = node.Next;               // 0x9B4AD0..0x9B4AF0
        bool existing = node is not null;
        if (existing && node!.DirectData8 != 0)                                    // 0x9B4AF4: e[0] != 0 only counts a reference
        {
            node.RefCount1C++;                                                     // 0x9B4B08..0x9B4B10
            return (ret, false);
        }
        if (!existing)                                                             // 0x9B4B24
        {
            if (Memory.Allocate(0x24) is null) return (ret, true);                 // 0x9B4B38..0x9B4B40
            node = new WwiseMediaNode { Key = id, Next = _buckets[index] };        // [0] = old head, [4] = key, the rest as 0x9B4B60..0x9B4B84
            _buckets[index] = node;                                                // 0x9B4B88
            NodeCount++;                                                           // 0x9B4B8C
        }
        node!.LastId20 = id;                                                       // 0x9B4B94..0x9B4BA0: [e+0x18] = id
        uint data = dataBase + offset;                                             // 0x9B4BC0
        var items = node.Items;
        var existingItem = items.FirstOrDefault(it => ReferenceEquals(it.Bank, bank));   // 0x9B4BCC..0x9B4BF4
        if (existingItem is not null)
        {
            existingItem.Data = data;                                              // 0x9B4CAC..0x9B4CB8
            existingItem.Size = size;
        }
        else
        {
            if (items.Count >= node.ItemCapacity)                                  // 0x9B4BF8 cmp r5,r2; bhs 0x9B4EBC (and 0x9B4EA8 for the empty array)
            {
                if (Memory.Allocate((node.ItemCapacity + 1) * 12) is null)         // 0x9B4EE4
                    return RemoveNodeAfterFailure(id);                             // 0x9B4EF0 beq 0x9B4FA4
                node.ItemCapacity++;                                               // 0x9B4F78 (the old array is copied and freed, 0x9B4F0C..0x9B4F68)
            }
            items.Insert(0, new WwiseMediaItem { Bank = bank, Data = data, Size = size });   // 0x9B4C04..0x9B4C6C
        }
        if (existing) node.RefCount1C++;                                           // 0x9B4CCC..0x9B4CDC: ip == 1 (a node that already existed) counts a reference
        return (1, false);                                                         // 0x9B4CD4 / 0x9B4CC4: [sp+8] = 1
    }

    /// <summary>
    /// The growth failure (<c>0x9B4FA4..0x9B5094</c>): the node for the entry's id is unlinked and freed with its items (<c>[BM+0x40]--</c>) and <c>[sp+8] = 0x34</c> is stored (<c>0x9B5084</c>, or
    /// <c>0x9B5000</c> when no node is found), then the failure exit.
    /// </summary>
    private (int Ret, bool Failed) RemoveNodeAfterFailure(uint id)
    {
        if (BucketCount != 0 && _buckets is not null)
        {
            uint index = id % BucketCount;
            WwiseMediaNode? prev = null;
            for (var n = _buckets[index]; n is not null; prev = n, n = n.Next)
            {
                if (n.Key != id) continue;
                if (prev is null) _buckets[index] = n.Next; else prev.Next = n.Next;   // 0x9B50A0..0x9B50AC / 0x9B503C..0x9B5044
                n.Items.Clear();                                                        // 0x9B505C: [+0x14] = 0 and the array is freed
                NodeCount--;                                                            // 0x9B5088
                break;
            }
        }
        return (0x34, true);
    }

    /// <summary>
    /// The rehash <c>0x9B4D04..0x9B5038</c>: a new bucket array of <paramref name="prime"/> slots; an allocation failure restores the old table (<c>0x9B5008..0x9B5038</c>, which leaves the old bucket count to
    /// the caller's zero test). The old nodes are re-inserted bucket by bucket at the front of their new chain (<c>0x9B4E14..0x9B4E48</c>). Returns the bucket count in effect.
    /// </summary>
    private uint Rehash(uint prime, uint oldCount)
    {
        var oldBuckets = _buckets;
        if (Memory.Allocate((int)(prime * 4)) is null)                             // 0x9B4D40 bl 0xA7A7F4
            return oldCount;                                                       // 0x9B5008: [BM+0x38], [+0x3C], [+0x34] restored, then 0x9B4AB0
        _buckets = new WwiseMediaNode?[prime];                                     // 0x9B4DA8..0x9B4DE4: zeroed
        BucketCapacity = prime;
        BucketCount = prime;
        if (oldCount != 0 && oldBuckets is not null)
        {
            for (uint b = 0; b < oldCount; b++)                                    // 0x9B4E14..0x9B4E4C
            {
                var n = oldBuckets[b];
                while (n is not null)
                {
                    var next = n.Next;                                              // 0x9B4E30 ldr r3,[r7]
                    uint idx = n.Key % prime;                                       // 0x9B4E28
                    n.Next = _buckets[idx];                                         // 0x9B4E38
                    _buckets[idx] = n;                                              // 0x9B4E40
                    n = next;
                }
            }
        }
        return prime;
    }

    // ---------------------------------------------------------------- the lookup 0xA1EC54 / 0x9BB320 / 0x9BB1F8

    /// <summary>
    /// <c>0xA1EC54(sourceBlock, &amp;1DC, &amp;1E0, &amp;108)</c> for a Sound's source block (R1.6, R1.7, R1.10): <c>[obj+0x10]</c> is 0 (<c>0xA1EA68</c> stores it, <c>0xA1EA94/0xA1EA9C</c>), so the media table is
    /// searched with <c>key = [obj]</c> (<c>0x9BB320</c>, which takes the global lock <c>0x108E330</c> and the BM lock): a bucket walk by <c>key % [BM+0x38]</c> (an empty table or a miss gives <c>(0, 0)</c>);
    /// a hit runs <c>0x9BB1F8</c> on <c>node+8</c>. The result is ignored by the caller; <c>*r3</c> is written only when an item is picked.
    /// </summary>
    public WwiseMediaLookup LookupA1EC54(uint key, uint sizeAt8, uint pointerAt10)
    {
        if (pointerAt10 != 0)                                                      // 0xA1EC58 cmp ip,#0; beq 0xA1EC74 not taken
            return new WwiseMediaLookup(pointerAt10, sizeAt8, null);               // 0xA1EC60..0xA1EC6C: *r2 = [obj+8], *r1 = [obj+0x10]; r3 untouched
        lock (_gate)                                                               // 0x9BB350, 0x9BB358
        {
            var node = Find(key);                                                  // 0x9BB35C..0x9BB3A0
            return node is null ? new WwiseMediaLookup(0, 0, null) : PickA9BB1F8(node);
        }
    }

    /// <summary>
    /// <c>0x9BB1F8(e = node+8, out, &amp;bank)</c> (R1.9): the reference count <c>[e+0x14]</c> (= <c>node+0x1C</c>) is incremented first; a direct pointer is returned as it is. Otherwise every item whose bank has
    /// <c>[bank+4]</c> bit 0 clear and whose size is above the best so far (signed, starting at -1) replaces the pick; the first of equal sizes wins. A pick whose bank has <c>[bank+4]</c> bit 1 takes a
    /// reference on <c>[bank+0x48]</c>. With no pick the result is <c>(0, 0)</c> and the reference count is restored.
    /// </summary>
    private static WwiseMediaLookup PickA9BB1F8(WwiseMediaNode node)
    {
        int saved = node.RefCount1C;                                               // 0x9BB208
        node.RefCount1C = saved + 1;                                               // 0x9BB210..0x9BB214
        if (node.DirectData8 != 0) return new WwiseMediaLookup(node.DirectData8, node.DirectSize0C, null);   // 0x9BB21C..0x9BB220
        WwiseMediaItem? pick = null;
        int best = -1;                                                             // 0x9BB240 mvn sl,#0
        foreach (var item in node.Items)                                           // 0x9BB244..0x9BB298
        {
            if ((item.Bank.Flags4 & 1) != 0) continue;                              // 0x9BB24C..0x9BB254
            if (!(unchecked((int)item.Size) > best)) continue;                      // 0x9BB258..0x9BB260 ble
            pick = item;
            best = unchecked((int)item.Size);
        }
        if (pick is null)
        {
            node.RefCount1C = saved;                                               // 0x9BB2E8 str r6,[ip,#0x14]
            return new WwiseMediaLookup(0, 0, null);                               // 0x9BB2D8..0x9BB2E4 (the tail 0x9BB2F0 is dead: [e] is 0)
        }
        if ((pick.Bank.Flags4 & 2) != 0) pick.Bank.RefCount48++;                    // 0x9BB2A8..0x9BB2D0 (atomic in the engine; the caller holds the table lock)
        return new WwiseMediaLookup(pick.Data, pick.Size, pick.Bank);
    }

    // ---------------------------------------------------------------- the release 0xA1ECBC / 0x9B65A8

    /// <summary>
    /// <c>0xA1ECBC(sourceBlock)</c> (R1.13): nothing when <c>[obj+0x10]</c> is non-zero; otherwise <c>0x9B65A8(BM, [obj])</c>: under both locks the node of the key loses a reference (<c>[node+0x1C]--</c>); at
    /// zero the direct data is freed (<c>0xA7A914</c>), and when the count is still zero afterwards the node is unlinked, its items array and the node freed and <c>[BM+0x40]--</c>.
    /// </summary>
    public void ReleaseA1ECBC(uint key, uint pointerAt10)
    {
        if (pointerAt10 != 0) return;                                              // 0xA1ECC0 bxne lr
        lock (_gate)
        {
            if (BucketCount == 0 || _buckets is null) return;                      // 0x9B65DC beq 0x9B662C
            uint index = key % BucketCount;                                        // 0x9B65E8
            WwiseMediaNode? prev = null;
            var node = _buckets[index];
            while (node is not null && node.Key != key) { prev = node; node = node.Next; }   // 0x9B6600..0x9B6628
            if (node is null) return;
            node.RefCount1C--;                                                     // 0x9B6648..0x9B6650
            if (node.RefCount1C != 0) return;                                      // 0x9B6654 bne 0x9B662C
            if (node.DirectData8 != 0)                                             // 0x9B665C..0x9B6674
            {
                Memory.Free(node.DirectData8);
                node.DirectData8 = 0;                                              // 0x9B667C: [+8] = [+0xC] = 0 (r6 == 0)
                node.DirectSize0C = 0;
                if (node.RefCount1C != 0) return;                                  // 0x9B6678..0x9B6688
            }
            if (prev is null) _buckets[index] = node.Next; else prev.Next = node.Next;   // 0x9B668C..0x9B6698, 0x9B66E0
            node.Items.Clear();                                                    // 0x9B669C..0x9B66C0
            NodeCount--;                                                           // 0x9B66D0..0x9B66D8
        }
    }
}

/// <summary>The in-memory stream the loader reads from; the engine's file reader <c>0x9BBC14</c> / <c>0x9BBF64</c> / <c>0x9BBF9C</c> is host I/O.</summary>
public interface IWwiseBankStream
{
    /// <summary><c>0x9BBC14(stream, buf, n, &amp;read)</c>: reads up to <paramref name="dest"/>.Length bytes; returns 1 and the count read (0 at the end).</summary>
    int Read(Span<byte> dest, out int read);

    /// <summary><c>0x9BBF64</c>: a read whose result is 0x38 when fewer bytes were read than asked.</summary>
    int ReadRaw(Span<byte> dest);

    /// <summary><c>0x9BBF9C(stream, n, &amp;skipped)</c>: skips up to <paramref name="n"/> bytes.</summary>
    int Skip(uint n, out uint skipped);

    /// <summary><c>0x9BBBA4</c>: the loader closes the stream before every return.</summary>
    void Close();
}

/// <summary>An in-memory <see cref="IWwiseBankStream"/>.</summary>
public sealed class WwiseMemoryBankStream : IWwiseBankStream
{
    private readonly byte[] _data;
    private int _position;

    /// <param name="data">The bank file's bytes.</param>
    public WwiseMemoryBankStream(byte[] data) => _data = data ?? throw new ArgumentNullException(nameof(data));

    /// <summary>Whether <see cref="Close"/> ran.</summary>
    public bool Closed { get; private set; }

    /// <inheritdoc />
    public int Read(Span<byte> dest, out int read)
    {
        read = Math.Min(dest.Length, _data.Length - _position);
        _data.AsSpan(_position, read).CopyTo(dest);
        _position += read;
        return 1;
    }

    /// <inheritdoc />
    public int ReadRaw(Span<byte> dest)
    {
        int result = Read(dest, out int read);
        return result == 1 && read != dest.Length ? 0x38 : result;
    }

    /// <inheritdoc />
    public int Skip(uint n, out uint skipped)
    {
        skipped = (uint)Math.Min((long)n, _data.Length - _position);
        _position += (int)skipped;
        return 1;
    }

    /// <inheritdoc />
    public void Close() => Closed = true;
}

/// <summary>A chunk the loader hands to a handler it does not own (<c>0x9B7860..0x9B7ABC</c>).</summary>
public sealed record WwiseBankChunk(string FourCc, uint Size, IWwiseBankStream Stream, WwiseMediaBank Bank);

/// <summary>
/// The bank loader <c>0x9B74D8</c> for type 0, mode 0 (LoadSoundbank, M2, M3): after BKHD it reads 8-byte chunk headers until the end of data. DIDX is handled by <see cref="HandleDidx"/> (<c>0x9B78E0..0x9B796C</c>), DATA
/// by <see cref="HandleData"/> (<c>0x9B79F0..0x9B7A38</c>, <c>0x9B7BAC..0x9B7CBC</c>) which then runs the writer when the bank has DIDX entries that were not processed yet; every other chunk goes to a named handler or is
/// skipped. Results: 1 success, 7 a short chunk header or skip, 0x34 an allocation failure, otherwise the handler's code.
/// </summary>
public sealed class WwiseBankLoader
{
    /// <summary>'STMG' (<c>0x9B77A8</c>).</summary>
    public const uint Stmg = 0x474D5453;
    /// <summary>'PLAT' (<c>0x9B77AC</c>).</summary>
    public const uint Plat = 0x54414C50;
    /// <summary>'ENVS' (<c>0x9B77BC</c>).</summary>
    public const uint Envs = 0x56534E45;
    /// <summary>'INIT' (<c>0x9B77B0</c>).</summary>
    public const uint Init = 0x54494E49;
    /// <summary>'DIDX' (<c>0x9B77B4</c>).</summary>
    public const uint Didx = 0x58444944;
    /// <summary>'HIRC' (<c>0x9B7970</c>).</summary>
    public const uint Hirc = 0x43524948;
    /// <summary>'STID' (<c>0x9B7980</c>).</summary>
    public const uint Stid = 0x44495453;
    /// <summary>'DATA' (<c>0x9B7990</c>).</summary>
    public const uint Data = 0x41544144;

    /// <param name="table">The media table BM.</param>
    public WwiseBankLoader(WwiseMediaTable table) => Table = table ?? throw new ArgumentNullException(nameof(table));

    /// <summary>The media table.</summary>
    public WwiseMediaTable Table { get; }

    /// <summary>
    /// The bodies of BKHD (<c>0x9B21F4</c>, must return 1) and of the chunks the loader does not own (INIT <c>0x9B4048</c>, STMG <c>0x9B0B14</c>, ENVS <c>0x9B2988</c>, PLAT <c>0x9B2B08</c>, HIRC <c>0x9B3260</c>, STID <c>0x9B2410</c>).
    /// A chunk of one of those kinds reaching an unset seam throws; the return value of a handler is the loader's (1 continues).
    /// </summary>
    public Func<WwiseBankChunk, int>? ChunkHandler { get; set; }

    /// <summary>The BKHD read (<c>0x9B7764..0x9B7778</c>): the loader reads no chunk before it returns 1. Unset throws.</summary>
    public Func<IWwiseBankStream, WwiseMediaBank, int>? ReadBkhd { get; set; }

    /// <summary>
    /// Runs the loader's chunk loop for type 0 / mode 0 on <paramref name="bank"/> (<c>0x9B7764..0x9B7860</c>). The stream is closed before every return (<c>0x9B7628</c>, <c>0x9B7814</c>).
    /// </summary>
    public int LoadMode0(IWwiseBankStream stream, WwiseMediaBank bank)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(bank);
        bank.Gate = Table.Gate;
        int result = Run(stream, bank);
        stream.Close();                                                            // 0x9B762C / 0x9B7814
        return result;
    }

    private int Run(IWwiseBankStream stream, WwiseMediaBank bank)
    {
        int r = (ReadBkhd ?? throw new WwiseMissingBehaviourException(
            "M6-025 M3: the BKHD read 0x9B21F4 (called at 0x9B776C) is not adopted; set WwiseBankLoader.ReadBkhd"))(stream, bank);
        if (r != 1) return r;                                                      // 0x9B7770 cmp r0,#1; bne 0x9B7628
        var header = new byte[8];
        while (true)
        {
            int rr = stream.Read(header, out int read);                            // 0x9B77EC bl 0x9BBC14(stream, hdr, 8, &read)
            if (rr != 1) return rr;                                                // 0x9B77F8 bne 0x9B79E8
            if (read != 8) return read == 0 ? 1 : 7;                              // 0x9B77FC..0x9B7810
            uint fourcc = BitConverter.ToUInt32(header, 0);
            uint size = BitConverter.ToUInt32(header, 4);
            int handled;
            switch (fourcc)
            {
                case Didx:
                    handled = HandleDidx(stream, bank, size);                      // 0x9B7898..0x9B78AC (mode 0 -> 0x9B78E0)
                    break;
                case Data:
                    handled = HandleData(stream, bank, size);                      // 0x9B79A0..0x9B79B0 (mode 0 -> 0x9B79F0)
                    break;
                case Stmg:                                                         // 0x9B7AC8: a zero-size STMG is skipped
                    if (size == 0) { handled = 1; break; }
                    handled = Chunk(fourcc, "STMG", size, stream, bank);
                    break;
                case Plat:
                    handled = Chunk(fourcc, "PLAT", size, stream, bank);
                    break;
                case Envs:
                    handled = Chunk(fourcc, "ENVS", size, stream, bank);
                    break;
                case Init:
                    handled = Chunk(fourcc, "INIT", size, stream, bank);
                    break;
                case Hirc:
                    handled = Chunk(fourcc, "HIRC", size, stream, bank);
                    break;
                case Stid:
                    handled = Chunk(fourcc, "STID", size, stream, bank);
                    break;
                default:                                                           // 0x9B78B8: an unknown chunk is skipped by its size
                    stream.Skip(size, out uint skipped);                           // 0x9B78C4 bl 0x9BBF9C
                    handled = size == skipped ? 1 : 7;                             // 0x9B78D0..0x9B78D8
                    break;
            }
            if (handled != 1) return handled;                                      // 0x9B79E0 cmp r0,#1; beq 0x9B77D4 / 0x9B79E8
        }
    }

    private int Chunk(uint fourcc, string name, uint size, IWwiseBankStream stream, WwiseMediaBank bank)
        => (ChunkHandler ?? throw new WwiseMissingBehaviourException(
            $"M6-025 M3: the {name} chunk body (called at 0x9B7A80..0x9B7AC4) is not adopted; set WwiseBankLoader.ChunkHandler"))(
            new WwiseBankChunk(name, size, stream, bank));

    /// <summary>
    /// The DIDX handler for mode 0 (<c>0x9B78E0..0x9B796C</c>, M4): when the bank has processed entries (<c>[bank+0x2C] != 0</c>) the chunk is read and discarded (<c>0x9B7B74</c>). Otherwise <c>n = size / 12</c>
    /// (<c>0xAAAAAAAB</c> multiply), a block of <c>n * 12</c> bytes is allocated into <c>[bank+0x18]</c> (null returns 0x34, <c>0x9B7C64</c>), <c>[bank+0x50]</c> bit 1 is set, the bytes are read raw and
    /// <c>[bank+0x30] = n</c>. A chunk size that is not a multiple of 12 leaves its remainder unread (the engine reads only <c>n * 12</c> bytes).
    /// </summary>
    public int HandleDidx(IWwiseBankStream stream, WwiseMediaBank bank, uint size)
    {
        if (bank.Counter2C != 0)                                                   // 0x9B78EC..0x9B78F4
        {
            stream.Skip(size, out _);                                              // 0x9B7B74..0x9B7B7C
            return 1;
        }
        uint n = (uint)(((ulong)size * 0xAAAAAAABUL) >> 35);                       // 0x9B78FC umull; 0x9B7908 lsr #3
        uint bytes = n * 12;                                                       // 0x9B790C..0x9B7910
        if (bytes == 0)
            throw new WwiseMissingBehaviourException("M6-025 M4: 0xA7A7F4 with a zero size (a DIDX chunk smaller than 12 bytes) is not adopted");
        var address = Table.Memory.Allocate((int)bytes);                            // 0x9B7930
        if (address is null) { bank.Didx18 = null; return 0x34; }                  // 0x9B793C str r0,[fp,#0x18]; 0x9B7940 beq 0x9B7C64
        bank.Didx18 = Table.Memory.Block(address.Value);
        bank.Flags50 = (byte)(bank.Flags50 | 2);                                   // 0x9B7944..0x9B7954
        stream.ReadRaw(bank.Didx18);                                               // 0x9B7958 bl 0x9BBF64 (the result is not tested)
        if (bank.Didx18 is not null) bank.Count30 = n;                             // 0x9B7964..0x9B7968
        return 1;
    }

    /// <summary>
    /// The DATA handler for mode 0 (<c>0x9B79F0..0x9B7A38</c>, <c>0x9B7BAC..0x9B7CBC</c>, M5). A chunk size of 0 reads nothing. Otherwise: with no pool (<c>[bank+0x24] == -1</c>) one is created (<c>0xA7AC98(0, size, size, 9, 0x10)</c>,
    /// -1 returns 0x34), <c>[bank+0x28] = 1</c>; the pool must check as 1 (<c>0xA7AAE8</c>, else that code is returned); attributes (<c>0xA7A7C8</c>) bit 3 clear allocates the chunk size from the pool, bit 3 set takes one block
    /// (<c>0xA7A9FC</c>) when the size does not exceed the block size (<c>0xA7AA9C</c>); a null buffer returns 0x34; the chunk is read (<c>0x9BBC14</c>) and a length other than the size returns 7. Then, when the bank has
    /// DIDX entries (<c>[bank+0x30] != 0</c>) none of which were processed (<c>[bank+0x2C] == 0</c>), the writer <c>0x9B49A4(BM, [bank+0x14], bank)</c> runs and its result is the handler's.
    /// </summary>
    public int HandleData(IWwiseBankStream stream, WwiseMediaBank bank, uint size)
    {
        var memory = Table.Memory;
        if (size != 0)
        {
            if (bank.PoolId24 == -1)                                               // 0x9B7BAC..0x9B7BB4
            {
                int id = memory.CreatePool(size);                                  // 0x9B7C88
                if (id == -1) return 0x34;                                         // 0x9B7C90..0x9B7C94
                bank.PoolId24 = id;                                                // 0x9B7C9C
                bank.PoolFlag28 = 1;                                               // 0x9B7CA0
            }
            int check = memory.CheckPool(bank.PoolId24);                           // 0x9B7BBC
            if (check != 1) return check;                                          // 0x9B7BC4..0x9B7BC8 -> 0x9B79E8
            uint attributes = memory.PoolAttributesOf(bank.PoolId24);              // 0x9B7BD4
            if ((attributes & 8) == 0)                                             // 0x9B7BDC tst r0,#8; beq 0x9B7CA8
            {
                bank.DataBuffer14 = memory.Allocate((int)size) ?? 0;               // 0x9B7CB0                                   // 0x9B7CB8 str r0,[fp,#0x14]
            }
            else
            {
                if (size <= memory.PoolBlockSizeOf(bank.PoolId24))                 // 0x9B7BE8..0x9B7BF4 bhi 0x9B7C08
                {
                    bank.DataBuffer14 = memory.Allocate((int)memory.PoolBlockSizeOf(bank.PoolId24)) ?? 0;   // 0x9B7BFC bl 0xA7A9FC: one block, 0x9B7C04
                }
                // size above the block size: [bank+0x14] stays as it was (0x9B7C08 reloads it)
            }
            if (bank.DataBuffer14 == 0) return 0x34;                               // 0x9B7C0C..0x9B7C10
            bank.DataSize1C = size;                                                // 0x9B7C18
            var buffer14 = memory.Block(bank.DataBuffer14);
            if (buffer14.Length < size)
                throw new WwiseMissingBehaviourException(
                    "M6-025 M5: the chunk size exceeds the pool block size while [bank+0x14] already holds a smaller buffer; the engine then reads past it (0x9B7C08..0x9B7C30), which the inventory does not settle");
            int rr = stream.Read(buffer14.AsSpan(0, (int)size), out int read);     // 0x9B7C30 bl 0x9BBC14
            if (rr == 1 && size != (uint)read) rr = 7;                             // 0x9B7C38..0x9B7C48
            if (rr != 1) return rr;                                                // 0x9B7C5C..0x9B7C60 -> 0x9B79E8
        }
        if (bank.Counter2C == 0 && bank.Count30 != 0)                              // 0x9B7A14..0x9B7A28
            return Table.WriteBankA9B49A4(bank, bank.DataBuffer14);                // 0x9B7A34
        return 1;
    }
}

/// <summary>
/// The PBI's media pair (M6-025 C31.1): <c>0xA02924..0xA02934</c> in the PBI's <c>vt+0xC</c> (<c>0xA0285C</c>) calls <c>0xA1EC54([pbi+0x150], pbi+0x1DC, pbi+0x1E0, pbi+0x108)</c> and Term (<c>0xA02AD8..0xA02B14</c>) releases it.
/// </summary>
public static class WwisePbiMedia
{
    private static (uint Key, uint Size8, uint Pointer10) SourceBlock(WwisePlayingInstance pbi)
    {
        if (pbi.SourceDescriptor is WwiseSourceDescriptor d)
            return (d.SourceId, d.InMemorySize, 0);                                // R1.10: 0xA1EA68 stores [0] = srcId, [8] = size, [0x10] = 0
        throw new WwiseMissingBehaviourException(
            "M6-025 R1.10: only the Sound source block (0xA1EA68, [obj+0x10] = 0) is read; another source block shape (0xA1EAE0, the music path) is not adopted");
    }

    /// <summary>
    /// <c>0xA02924..0xA02934</c>: the lookup's pair is stored to <c>pbi+0x1DC</c> / <c>pbi+0x1E0</c> (always, a miss stores <c>(0, 0)</c>) and the picked bank, when there is one, to <c>pbi+0x108</c> (a miss leaves it).
    /// </summary>
    public static void StoreSourceInfoA02924(WwisePlayingInstance pbi, WwiseMediaTable table)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        ArgumentNullException.ThrowIfNull(table);
        var (key, size8, pointer10) = SourceBlock(pbi);
        var result = table.LookupA1EC54(key, size8, pointer10);
        pbi.Word1DC = result.Data;                                                 // 0xA02928: *r1
        pbi.Word1E0 = result.Size;                                                 // 0xA0292C: *r2
        if (result.Bank is not null) pbi.Obj108 = result.Bank;                     // 0xA02930: *r3, written only on a pick
    }

    /// <summary>
    /// The release in Term (<c>0xA02AD8..0xA02B14</c>): when <c>pbi+0x1DC</c> is non-zero <c>0xA1ECBC([pbi+0x150])</c> runs and <c>pbi+0x1DC</c> is set to 0 (<c>pbi+0x1E0</c> is kept); when <c>pbi+0x108</c> is non-null its
    /// <c>vt+0</c> (<c>0x9B47D8</c>) runs and it is set to 0.
    /// </summary>
    public static void ReleaseInTerm(WwisePlayingInstance pbi, WwiseMediaTable? table)
    {
        ArgumentNullException.ThrowIfNull(pbi);
        if (pbi.Word1DC != 0)                                                      // 0xA02ADC cmp r3,#0
        {
            var (key, _, pointer10) = SourceBlock(pbi);
            (table ?? throw new WwiseMissingBehaviourException("M6-025 R1.13: 0xA1ECBC needs the media table")).ReleaseA1ECBC(key, pointer10);                                   // 0xA02AE8
            pbi.Word1DC = 0;                                                       // 0xA02AF0
        }
        if (pbi.Obj108 is { } bank)                                                // 0xA02AF8 cmp r0,#0
        {
            bank.ReleaseVt0A9B47D8();                                              // 0xA02B0C blx [vt+0](obj, 0)
            pbi.Obj108 = null;                                                     // 0xA02B14 (after the call; a throw leaves it set)
        }
    }
}
