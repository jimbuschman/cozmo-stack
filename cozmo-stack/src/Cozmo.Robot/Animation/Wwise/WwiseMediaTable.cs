// fidelity: M6-025, M6-026
namespace Cozmo.Robot.Animation.Wwise;

// The media table (M6-025 C31.1, C32.2): the bank loader's DIDX and DATA handlers for mode 0, the writer 0x9B49A4 that fills the media hash of the
// sound-bank manager BM (the object at 0x108D8D8), the lookup 0xA1EC54 / 0x9BB320 / 0x9BB1F8 and the release 0xA1ECBC / 0x9B65A8, which feed and
// release the pair pbi+0x1DC / pbi+0x1E0 (and pbi+0x108).
//
// Production entry. Engine: Anki LoadSoundbank -> 0x9A33D8 posts command 0 -> BM vt+0x10 = 0x9BB65C -> 0x9BB834 -> 0x9B7CD4 -> the loader 0x9B74D8. The C# counterpart of the inner loader is
// WwiseBankLoader.Load9B74D8, but nothing in production constructs it (see its header: the wrapper 0x9B7CD4, the sibling wrapper 0x9B7F0C and the direct caller 0x9B8694 have no C#: MISSING), and it is exercised by tests only. WwisePbiMedia.StoreSourceInfoA02924
// is called by the playback limiter's InsertPbiA0285C (0xA0285C), which has no production caller either until the Play path is wired.
//
// This file owns the bank object fields, the creation helpers, the registry, the writer 0x9B49A4, its cleanup 0x9B45D8, the release 0x9B47D8, the pool table and the lookup. The chunk handlers
// live in WwiseBankLoader.cs; the unread ones (STMG 0x9B0B14, STID 0x9B2410, the streamed DATA 0x9B6C34, the HIRC creators) are named seams there and throw when reached.

/// <summary>
/// The host allocator the loader and the media table allocate through (<c>0xA7A7F4</c> / <c>0xA7A988</c> / <c>0xA7A914</c>) and the pool services the DATA handler
/// uses (<c>0xA7AC98</c>, <c>0xA7AAE8</c>, <c>0xA7A7C8</c>, <c>0xA7AA9C</c>, <c>0xA7A9FC</c>, <c>0xA7AA48</c>: C34.4 K16, the fixed-block pools the bank loader creates with attributes 9). An address is not engine
/// behaviour, so it is a synthetic bump counter; what the callers observe is the success or failure of each allocation, which <see cref="AllocationFails"/> drives. The heap pools' internals
/// (<c>0xA7B6DC</c>, <c>0xA7B6C4</c>, <c>0xA7B900</c>, <c>0xA7BB58</c>, <c>0xA7BF30</c>) are unread: a heap pool is managed memory with no <c>used</c> accounting, and creating one through <see cref="CreatePoolA7AC98"/> throws.
/// </summary>
public sealed class WwiseBankMemory
{
    private uint _next = 0x20000000;
    private readonly Dictionary<uint, byte[]> _blocks = new();

    /// <summary>The <c>0xA7A7F4</c> returning null: true fails that one allocation. Null means no allocation fails. Each call is one allocation attempt, in the engine's order.</summary>
    public Func<bool>? AllocationFails { get; set; }

    /// <summary>
    /// <c>[0x108E358]</c>: the number of pool descriptors the table <c>[0x108E354]</c> holds (the pool manager's configuration, set by the unread init <c>0xA7AC68</c>). It is 0 before the init, and 0 makes <c>0xA7AC98</c> fail with -1
    /// (<c>0xA7ACD8..0xA7ACE4</c>), so a host that creates pools sets it.
    /// </summary>
    public int MaxPools
    {
        get => _maxPools ?? throw new WwiseMissingBehaviourException(
            "M6-025 K16: [0x108E358] is written by the pool manager init 0xA7A5D8 (0xA7A6BC) from the configured count; supply WwiseBankMemory.MaxPools");
        set => _maxPools = value;
    }
    private int? _maxPools;

    private readonly List<WwisePoolDescriptor> _descriptors = new();
    private readonly Dictionary<byte[], uint> _external = new(ReferenceEqualityComparer.Instance);

    /// <summary><c>[0x108E35C]</c>: the pools in use.</summary>
    public int PoolsInUse { get; private set; }

    /// <summary>Allocations that have not been freed.</summary>
    public int LiveBlocks => _blocks.Count;

    /// <summary><c>0xA7A7F4(pool, size)</c> on the default pool <c>0x1052418</c>: a block of <paramref name="size"/> bytes, or null when the allocation fails. The default pool is a heap pool (Anki's init creates it with attributes 1); the heap
    /// internals (<c>0xA7B900</c>, <c>0xA7B6C4</c>, <c>0xA7BF30</c>) and so its <c>used</c> accounting are unread, so the block is managed memory.</summary>
    public uint? Allocate(int size)
    {
        if (size == 0) return null;                                                    // 0xA7A7F4..0xA7A7F8: a size of 0 returns 0 before the heap is asked
        if (AllocationFails?.Invoke() == true) return null;
        uint address = _next;
        _next += (uint)((Math.Max(size, 1) + 15) & ~15);
        _blocks[address] = new byte[size];
        return address;
    }

    /// <summary><c>0xA7A894(pool, size, align)</c> (<c>0x9BBEB0</c>): as <see cref="Allocate"/> with the address aligned to <paramref name="align"/> (the heap's aligned allocation body <c>0xA7BB58</c> is unread; the alignment only shapes the address).</summary>
    public uint? AllocateAlignedA7A894(int size, int align)
    {
        if (size == 0) return null;                                                    // 0xA7A894..0xA7A898
        if (AllocationFails?.Invoke() == true) return null;
        if (align > 1) _next = (uint)((_next + (uint)align - 1) / (uint)align * (uint)align);
        uint address = _next;
        _next += (uint)((Math.Max(size, 1) + 15) & ~15);
        _blocks[address] = new byte[size];
        return address;
    }

    /// <summary><c>0xA7A988</c> / <c>0xA7A914</c>: returns a block.</summary>
    public void Free(uint address) { FreeCalls++; _blocks.Remove(address); }

    /// <summary>The number of <see cref="Free"/> calls (an observation point for the tests).</summary>
    public int FreeCalls { get; private set; }

    /// <summary>The block's bytes (the buffer the engine's pointer addresses).</summary>
    public byte[] Block(uint address) => _blocks[address];

    /// <summary>
    /// The block that contains <paramref name="address"/> and the offset of the address inside it: how a pointer into a block (the engine's <c>DATA + DIDX.offset</c>, <c>[pbi+0x1DC]</c>) is dereferenced. An address
    /// outside every live block is a named stop, as a wild pointer is in the engine.
    /// </summary>
    public (byte[] Block, int Offset) Resolve(uint address)
    {
        foreach (var (start, block) in _blocks)
            if (address >= start && address - start < (uint)block.Length) return (block, (int)(address - start));
        throw new WwiseMissingBehaviourException($"M6-025: the address 0x{address:X8} is not inside a live allocation of the bank memory");
    }

    /// <summary>
    /// Maps host memory the engine would address in place (the bank file's bytes a memory-mode reader serves, <c>0x9BBE5C</c>'s cursor) to a synthetic address, so a pointer into it can be stored (<c>[bank+0x14]</c>, the media items) and resolved
    /// by <see cref="Resolve"/>. Mapping the same array twice returns the same base.
    /// </summary>
    public uint MapExternal(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (_external.TryGetValue(data, out uint existing)) return existing;
        uint address = _next;
        _next += (uint)((Math.Max(data.Length, 1) + 15) & ~15);
        _blocks[address] = data;
        _external[data] = address;
        return address;
    }

    /// <summary>The synthetic address of a pointer into a mapped array (<see cref="MapExternal"/>); an unmapped array is mapped.</summary>
    public uint AddressOf(WwiseBytePtr ptr)
        => ptr.IsNull ? 0 : MapExternal(ptr.Array!) + (uint)ptr.Index;

    // ------------------------------------------------------------------ the pool table (C34.4 K16)

    /// <summary>
    /// <c>0xA7AC98(ptr, size, blockSize, attr, align)</c> (K16), for a pool the engine allocates itself (<c>ptr == 0</c>, <c>attr</c> bit 0 set) with attribute bit 3 (a fixed-block pool): -1 when <c>ptr == 0</c> and attribute bit 0 is clear
    /// (<c>0xA7ACAC</c>), when the pools in use are not below <see cref="MaxPools"/> or it is not positive, or no descriptor is free; otherwise the first descriptor whose block count is 0 is taken, <c>n = size / blockSize</c>,
    /// <c>total = n * blockSize</c>, <c>total + align</c> bytes are allocated and aligned up, the <c>n</c> blocks are threaded on the free list in address order, and the descriptor stores <c>[+4] = blockSize</c>, <c>[+0] = n</c>,
    /// <c>[+0x28] = align</c>, <c>[+0x18] = attr</c>, <c>[+0x30] = total</c>; the pools in use grow by one and the descriptor's index is returned. A caller-supplied block, a heap pool (attribute bit 3 clear: <c>0xA7B6DC</c>,
    /// <c>0xA7B6D4</c> are unread) and a block size of 0 are not modelled and throw. The allocation failure of the external allocator (the Thumb thunk <c>0x8D7FFC</c>) is <see cref="AllocationFails"/>.
    /// </summary>
    public int CreatePoolA7AC98(uint ptr, uint size, uint blockSize, uint attr, uint align)
    {
        bool ptrNull = ptr == 0;                                                       // 0xA7AC9C clz r7,r0; lsr
        if (ptrNull && (attr & 1) == 0) return -1;                                     // 0xA7ACAC bics lr,r7,r3; bne 0xA7ADF4
        if (PoolsInUse >= MaxPools) return -1;                                         // 0xA7ACC4..0xA7ACD0 [0x108E35C] >= [0x108E358]
        if (MaxPools <= 0) return -1;                                                  // 0xA7ACD8..0xA7ACE4
        int index = -1;
        for (int i = 0; i < MaxPools; i++)                                             // 0xA7AD1C..0xA7AD40
        {
            if (i >= _descriptors.Count) _descriptors.Add(new WwisePoolDescriptor());
            if (_descriptors[i].BlockCount0 == 0) { index = i; break; }                // 0xA7AD30 cmp r1,#0; bne next
        }
        if (index < 0) return -1;                                                      // 0xA7AD18 ble 0xA7ADF4
        if (!ptrNull) throw new WwiseMissingBehaviourException("M6-025 K16: a caller-supplied pool block (ptr != 0) is not modelled");
        if (blockSize == 0) throw new InvalidOperationException("0xA7AD4C: the division by the block size faults");
        if ((attr & 8) == 0)
            throw new WwiseMissingBehaviourException("M6-025 K16: a heap pool (attribute bit 3 clear) needs 0xA7B6DC / 0xA7B6D4, which are unread");
        uint n = size / blockSize;                                                     // 0xA7AD4C bl 0x4BE310
        uint total = blockSize * n;                                                    // 0xA7AD58 mul
        if (total + align == 0) throw new WwiseMissingBehaviourException("M6-025 K16: a pool of size 0 reads the descriptor's stale raw pointer (0xA7AE38)");
        if (AllocationFails?.Invoke() == true) return -1;                              // 0xA7AE88 blx 0x8D7FFC returns null -> 0xA7AEA8
        var d = _descriptors[index];
        d.Raw8 = _next;                                                                // [desc+8] = the raw block
        uint raw = _next;
        _next += (uint)(((long)total + align + 15) & ~15L);
        uint aligned = align != 0 && raw % align != 0 ? raw + (align - raw % align) : raw;   // 0xA7AE5C..0xA7AE70
        d.Aligned0C = aligned;                                                         // 0xA7AE40, 0xA7AE70
        d.Flags1C |= 1;                                                                // 0xA7AE74..0xA7AE80: the memory is owned
        d.Capacity30 = total;                                                          // 0xA7AD7C
        d.Attr18 = attr;                                                               // 0xA7AD84
        d.FreeList.Clear();
        for (uint k = 0; k < n; k++)                                                   // 0xA7AD90..0xA7ADBC: [blk] = 0 and appended at the tail
        {
            uint block = aligned + k * blockSize;
            _blocks[block] = new byte[blockSize];
            d.FreeList.Enqueue(block);
        }
        d.BlockSize4 = blockSize;                                                      // 0xA7ADD0
        d.BlockCount0 = n;                                                             // 0xA7ADD4
        d.Align28 = align;                                                             // 0xA7ADDC
        PoolsInUse++;                                                                  // 0xA7ADE0
        return index;
    }

    /// <summary>The descriptor of a pool slot, or null when the slot was never touched (an observation point for the tests).</summary>
    internal WwisePoolDescriptor? DescriptorOrNull(int pool) => pool >= 0 && pool < _descriptors.Count ? _descriptors[pool] : null;

    private WwisePoolDescriptor Descriptor(int pool)
    {
        while (pool >= 0 && pool < MaxPools && pool >= _descriptors.Count) _descriptors.Add(new WwisePoolDescriptor());   // the table [0x108E354] holds [0x108E358] zeroed descriptors
        if (pool < 0 || pool >= _descriptors.Count)
            throw new InvalidOperationException($"the pool id {pool} indexes outside the descriptor table (the engine reads out of range)");
        return _descriptors[pool];
    }

    /// <summary><c>0xA7AAE8(pool)</c> (K16): <c>0xE</c> when <c>[0x108E358] &lt;= pool</c> (signed) or the descriptor's block count <c>[+0]</c> is 0, else 1.</summary>
    public int PoolCheckA7AAE8(int pool)
    {
        if (MaxPools <= pool) return 0xE;                                              // 0xA7AAF4..0xA7AAFC cmp r2,r0; bgt
        return Descriptor(pool).BlockCount0 == 0 ? 0xE : 1;                            // 0xA7AB10..0xA7AB1C
    }

    /// <summary><c>0xA7A7C8(pool)</c>: <c>[desc+0x18]</c>.</summary>
    public uint PoolAttributesA7A7C8(int pool) => Descriptor(pool).Attr18;

    /// <summary><c>0xA7AA9C(pool)</c>: <c>[desc+4]</c>.</summary>
    public uint PoolBlockSizeA7AA9C(int pool) => Descriptor(pool).BlockSize4;

    /// <summary>
    /// <c>0xA7A9FC(pool)</c> (K16): the head of the free list is taken (0 when it is empty, <c>0xA7AA18..0xA7AA1C</c>), <c>used += blockSize</c>, and the tail is cleared with the last block (<c>0xA7AA2C..0xA7AA3C</c>).
    /// </summary>
    public uint PopBlockA7A9FC(int pool)
    {
        var d = Descriptor(pool);
        if (d.FreeList.Count == 0) return 0;
        uint block = d.FreeList.Dequeue();
        d.Used2C += d.BlockSize4;
        return block;
    }

    /// <summary><c>0xA7AA48(pool, block)</c> (K16): <c>used -= blockSize</c>, the block's link is cleared and it is appended at the tail of the free list.</summary>
    public void PushBlockA7AA48(int pool, uint block)
    {
        var d = Descriptor(pool);
        d.Used2C = unchecked(d.Used2C - d.BlockSize4);
        d.FreeList.Enqueue(block);
    }

    /// <summary><c>[desc+0x2C]</c>: the bytes handed out.</summary>
    public uint PoolUsed(int pool) => Descriptor(pool).Used2C;

    /// <summary>The free blocks of a pool, in list order.</summary>
    public IReadOnlyCollection<uint> PoolFreeList(int pool) => Descriptor(pool).FreeList;

    /// <summary>
    /// <c>0xA7AEC4(pool)</c>: the check <c>0xA7AAE8</c> must give 1 (else that code is returned), then the descriptor is released. The tail (<c>0xA7AF08..0xA7AF54</c>: the free list cleared for a fixed-block pool, the raw block returned through the
    /// thunk <c>0x8D8000</c>, the descriptor reset by <c>0xA7A588</c>, the pools in use decremented) is not adopted by C34.4 (K15 lists it as RECOVERABLE_GAP), so it throws after the check.
    /// </summary>
    public int DestroyPoolA7AEC4(int pool)
    {
        int check = PoolCheckA7AAE8(pool);
        if (check != 1) return check;
        throw new WwiseMissingBehaviourException("M6-025 K15: DestroyPool's tail (0xA7AEC4 after the check: 0xA7AF08..0xA7AF54, 0xA7A588, the thunk 0x8D8000) is not adopted by C34.4");
    }
}

/// <summary>One pool descriptor (0x34 bytes, <c>0xA7AC98</c>): block count <c>+0</c>, block size <c>+4</c>, raw block <c>+8</c>, aligned block <c>+0xC</c>, free list <c>+0x10</c> / <c>+0x14</c>, attributes <c>+0x18</c>, flags <c>+0x1C</c> (bit 0: memory owned), alignment <c>+0x28</c>, used <c>+0x2C</c>, capacity <c>+0x30</c>.</summary>
public sealed class WwisePoolDescriptor
{
    /// <summary><c>+0</c>.</summary>
    public uint BlockCount0 { get; set; }

    /// <summary><c>+4</c>.</summary>
    public uint BlockSize4 { get; set; }

    /// <summary><c>+8</c>.</summary>
    public uint Raw8 { get; set; }

    /// <summary><c>+0xC</c>.</summary>
    public uint Aligned0C { get; set; }

    /// <summary><c>+0x14</c> head to <c>+0x10</c> tail: the free blocks in order.</summary>
    public Queue<uint> FreeList { get; } = new();

    /// <summary><c>+0x18</c>.</summary>
    public uint Attr18 { get; set; } = 1;

    /// <summary><c>+0x1C</c>.</summary>
    public byte Flags1C { get; set; }

    /// <summary><c>+0x28</c>.</summary>
    public uint Align28 { get; set; }

    /// <summary><c>+0x2C</c>.</summary>
    public uint Used2C { get; set; }

    /// <summary><c>+0x30</c>.</summary>
    public uint Capacity30 { get; set; }
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

    /// <summary>The media table (the BM) the loader created the bank under: its lock, its media hash and its allocator serve <see cref="ReleaseVt0A9B47D8"/>.</summary>
    internal WwiseMediaTable? Table { get; set; }

    /// <summary>The synthetic address of the DIDX block (<c>[bank+0x14]</c> for the data, <c>[bank+0x18]</c> here): freed by the release when the bank owns it (<c>[bank+0x50]</c> bit 1).</summary>
    public uint Didx18Address { get; set; }

    /// <summary><c>+0x34</c>: the record a registered callback keeps; a non-zero value makes the release call <c>0xA68150</c> (unread).</summary>
    public uint Word34 { get; set; }

    /// <summary><c>+0x38</c>: the argument <c>0xA68150</c> takes with <see cref="Word34"/>.</summary>
    public uint Word38 { get; set; }

    /// <summary><c>+0xC</c>: the language (the creation stores the loader's language argument only for type 2).</summary>
    public uint Language0C { get; set; }

    /// <summary><c>+0x4C</c>.</summary>
    public int Word4C { get; set; }

    /// <summary><c>+0x54</c>.</summary>
    public uint Word54 { get; set; }

    /// <summary><c>+0x3C</c> (array), <c>+0x40</c> (count), <c>+0x44</c> (capacity): the objects the HIRC chunk appended (<c>0x9B3260</c>).</summary>
    public List<object> HircObjects3C { get; } = new();

    /// <summary><c>+0x3C</c>: the array block's address (0 for none).</summary>
    public uint HircArray3C { get; set; }

    /// <summary><c>+0x40</c>: the object count (the number of pointers the array holds).</summary>
    public uint HircCount40 => (uint)HircObjects3C.Count;

    /// <summary><c>+0x44</c>: the array's capacity.</summary>
    public uint HircCapacity44 { get; set; }

    /// <summary>The registered state of the bank object: set once the loader has created it (<c>0x9B7760</c>) and cleared when the release frees it (<c>0x9B48C8</c>).</summary>
    public bool Freed { get; private set; }

    /// <summary>
    /// Bank <c>vt+0</c> = <c>0x9B47D8(bank, 0)</c> (the vtable word at <c>0x101C028</c>; <c>pbi+0x108</c> release, <c>0xA02B00..0xA02B0C</c>), see <see cref="WwiseMediaTable.ReleaseBankA9B47D8"/> (K15).
    /// </summary>
    public void ReleaseVt0A9B47D8()
    {
        if (Table is { } table) { table.ReleaseBankA9B47D8(this, force: false); return; }
        lock (this)
        {
            RefCount48--;                                                          // 0x9B47F8..0x9B4810
            if (RefCount48 > 0) return;                                            // 0x9B4814..0x9B481C
        }
        throw new WwiseMissingBehaviourException(
            "M6-025 K15: the bank's reference count reached zero but the bank was not created by the loader, so its media table (BM) is unknown; the unload below 0x9B4830 needs it");
    }

    internal void MarkFreed() => Freed = true;
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
        bank.Table = this;                                                        // the BM this bank is written under
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
    {
        CleanupA9B45D8(bank);                                                      // 0x9B4C98 / 0x9B4C94 bl 0x9B45D8(BM, bank)
        return ret;                                                                // 0x9B4C9C ldr r0,[sp,#8]
    }

    /// <summary>
    /// <c>0x9B45D8(BM, bank)</c> (C34.4 K14), the inverse of the writer: nothing when <c>[bank+0x18] == 0</c>. Under the BM lock, while <c>[bank+0x2C] != 0</c> the counter drops by one and the DIDX entry at that index
    /// (<c>[bank+0x18] + 12 * counter</c>) is looked at: an id of 0, an empty media hash (<c>[BM+0x38] == 0</c>) or an id with no node is skipped. In the node's items the first one whose bank is this bank is removed (the
    /// last item is copied over it when more than one remains, the count drops; no match removes nothing); the node loses a reference (<c>[node+0x1C]--</c>), and at zero its direct data is freed (<c>[+8] = [+0xC] = 0</c>),
    /// the node is unlinked from its chain, its items array freed and the node freed, and <c>[BM+0x40]--</c>.
    /// </summary>
    public void CleanupA9B45D8(WwiseMediaBank bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (bank.Didx18 is null) return;                                            // 0x9B45D8..0x9B45E0
        lock (_gate)                                                                // 0x9B4604 bl 0x4D3064(BM+0x2C)
        {
            while (bank.Counter2C != 0)                                             // 0x9B4608..0x9B4610
            {
                bank.Counter2C--;                                                   // 0x9B4614..0x9B461C
                uint id = BitConverter.ToUInt32(bank.Didx18, (int)(bank.Counter2C * 12));   // 0x9B4618..0x9B4624
                if (id == 0) continue;                                              // 0x9B4628..0x9B462C
                if (BucketCount == 0 || _buckets is null) continue;                 // 0x9B4630..0x9B4638
                uint index = id % BucketCount;                                      // 0x9B463C..0x9B4650
                WwiseMediaNode? prev = null;
                var node = _buckets[index];
                while (node is not null && node.Key != id) { prev = node; node = node.Next; }   // 0x9B465C..0x9B4688
                if (node is null) continue;                                         // 0x9B4658 / 0x9B467C
                int item = node.Items.FindIndex(x => ReferenceEquals(x.Bank, bank));   // 0x9B468C..0x9B46C8
                if (item >= 0)                                                      // 0x9B4790 / 0x9B4794
                {
                    if (node.Items.Count > 1) node.Items[item] = node.Items[^1];    // 0x9B4794..0x9B479C ldmdbhi / stmhi
                    node.Items.RemoveAt(node.Items.Count - 1);                      // 0x9B47A0..0x9B47A4 [node+0x14] = count - 1
                }
                node.RefCount1C--;                                                  // 0x9B46D0..0x9B46D8
                if (node.RefCount1C != 0) continue;                                 // 0x9B46DC..0x9B46E0
                if (node.DirectData8 != 0)                                          // 0x9B46E4..0x9B46EC
                {
                    Memory.Free(node.DirectData8);                                  // 0x9B4700 bl 0xA7A914
                    node.DirectData8 = 0;                                           // 0x9B4710..0x9B4714
                    node.DirectSize0C = 0;
                    if (node.RefCount1C != 0) continue;                             // 0x9B4704..0x9B4718 (the count is still 0)
                }
                if (prev is null) _buckets[index] = node.Next; else prev.Next = node.Next;   // 0x9B4738..0x9B4744, 0x9B47B4..0x9B47B8
                node.Items.Clear();                                                 // 0x9B4748..0x9B4770 (the items array and the node are freed)
                NodeCount--;                                                        // 0x9B477C..0x9B4788
            }
        }
    }

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

    /// <summary>The bank registry of the BM (<c>[BM+0x44]</c>, <c>[BM+0x48]</c>: K13): banks keyed by (id, language).</summary>
    public WwiseBankRegistry Registry { get; } = new();

    /// <summary>
    /// <c>0xA68150(BM+0x7C, id, language, 1, pool, [bank+0x38])</c> (<c>0x9B4938</c>, <c>0x9B4988</c>): run when <c>[bank+0x34] != 0</c> at the release. Not adopted (K15 lists it as RECOVERABLE_GAP); required then.
    /// </summary>
    public Action<WwiseMediaBank>? BankCallbackRecordA68150 { get; set; }

    /// <summary>
    /// Bank release <c>0x9B47D8(bank, force)</c> (C34.4 K15, verification correction 11). Under the global lock <c>[bank+0x48]</c> is decremented atomically; above zero returns. At zero or below: a non-null <c>[bank+0x14]</c> is returned to its
    /// pool (attributes bit 3 clear: <c>0xA7A988(pool, ptr)</c>; set: <c>0xA7AA48(pool, block)</c>), cleared, and a set byte <c>[bank+0x28]</c> destroys the pool (<c>0xA7AEC4</c>, tail unread: throws) and sets <c>[bank+0x24] = -1</c>;
    /// then the cleanup <c>0x9B45D8(BM, bank)</c> runs. With <c>[bank+0x4C] &gt; 0</c>: unlock, and with <paramref name="force"/> clear and <c>[bank+0x34] != 0</c> <c>0xA68150</c> runs and <c>[bank+0x34]</c> is cleared; no object is freed.
    /// With <c>[bank+0x4C] &lt;= 0</c>: unlock; <paramref name="force"/> clear with <c>[bank+0x34] != 0</c> runs <c>0xA68150</c> and clears it; then the object is freed (vptr reset, the DIDX block freed when <c>[bank+0x50]</c> bit 1 is set).
    /// </summary>
    public void ReleaseBankA9B47D8(WwiseMediaBank bank, bool force)
    {
        ArgumentNullException.ThrowIfNull(bank);
        bool freeObject;
        lock (_gate)                                                               // 0x9B47F4 bl 0x4D3064 ... unlock 0x9B489C (w4c > 0) / 0x9B48BC (w4c <= 0) / 0x9B482C (ref > 0)
        {
            bank.RefCount48--;                                                     // 0x9B47F8..0x9B4810 ldrex / sub / strex
            if (bank.RefCount48 > 0) return;                                       // 0x9B4814 cmp r2,#0; 0x9B481C ble 0x9B4830 not taken: unlock (0x9B482C), return
            if (bank.DataBuffer14 != 0)                                            // 0x9B4830..0x9B4838
            {
                if ((Memory.PoolAttributesA7A7C8(bank.PoolId24) & 8) == 0)         // 0x9B483C..0x9B4848
                    Memory.Free(bank.DataBuffer14);                                // 0x9B4854 bl 0xA7A988
                else
                    Memory.PushBlockA7AA48(bank.PoolId24, bank.DataBuffer14);      // 0x9B48B4 bl 0xA7AA48
                bank.DataBuffer14 = 0;                                             // 0x9B4860
                if (bank.PoolFlag28 != 0)                                          // 0x9B4864..0x9B4868
                {
                    int r = Memory.DestroyPoolA7AEC4(bank.PoolId24);               // 0x9B4870 bl 0xA7AEC4 (the tail is unread: throws)
                    _ = r;
                    bank.PoolId24 = -1;                                            // 0x9B4874..0x9B4878
                }
            }
            CleanupA9B45D8(bank);                                                  // 0x9B487C..0x9B488C
            freeObject = bank.Word4C <= 0;                                         // 0x9B4890..0x9B4898 ble 0x9B48BC
        }                                                                          // the lock is released here, before 0xA68150 and the object free
        if (!force && bank.Word34 != 0)                                            // 0x9B48A4 / 0x9B48C0: force == 0 and [bank+0x34] != 0
        {
            (BankCallbackRecordA68150 ?? throw new WwiseMissingBehaviourException(
                "M6-025 K15: 0xA68150 (0x9B4938, 0x9B4988) is not adopted by C34.4; supply WwiseMediaTable.BankCallbackRecordA68150"))(bank);
            bank.Word34 = 0;                                                       // 0x9B493C / 0x9B498C str r6,[r5,#0x34]
        }
        if (!freeObject) return;                                                   // 0x9B48AC
        if ((bank.Flags50 & 2) != 0 && bank.Didx18Address != 0)                    // 0x9B48D8 tst r1,#2; 0x9B4944..0x9B494C
            Memory.Free(bank.Didx18Address);
        bank.MarkFreed();                                                          // 0x9B48E8..0x9B4900: vptr reset and the object freed
    }
}

/// <summary>
/// The bank registry of the BM (C34.4 K13, <c>0xA68804(table, key1, key2)</c>): <c>n = [table+4]</c>; <c>n == 0</c> or no match returns null; the bucket <c>(key1 + key2) % n</c> chain is walked by <c>[e+0x10]</c> for
/// <c>[e+8] == key1 &amp;&amp; [e+0xC] == key2</c> (<c>0xA68804..0xA68898</c>). The key is (bank id, language). The loader only looks entries up (<c>0x9B7548</c>): the insertion is not in any adopted row, so the host registers a bank.
/// </summary>
public sealed class WwiseBankRegistry
{
    private readonly Dictionary<(uint Id, uint Language), WwiseMediaBank> _banks = new();

    /// <summary>The bank for (id, language), or null.</summary>
    public WwiseMediaBank? Find(uint id, uint language) => _banks.TryGetValue((id, language), out var bank) ? bank : null;

    /// <summary>Registers a bank (the engine's insertion is unread). A second bank with the same key is refused: the engine's chain order for duplicates is not modelled.</summary>
    public void Add(WwiseMediaBank bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (!_banks.TryAdd((bank.Id, bank.Language0C), bank)) throw new InvalidOperationException("a bank with this (id, language) is registered");
    }

    /// <summary>The number of registered banks.</summary>
    public int Count => _banks.Count;
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
