// fidelity: M6-002, M6-025
using System.Buffers.Binary;

namespace Cozmo.Robot.Animation.Wwise;

// The Vorbis stream integration around the bit-exact decode of M6-002 (M6-wwise-bank.md C36.1, C36.4; research live-bodies-6 V01..V13, V23). Every function below was disassembled in libcozmoEngine.so (ARM)
// when this file was written; the addresses are in the comments. What the C# methods are, in the engine's names:
//
//   WwiseVorbisFraming.FrameLoopAB7E40       = 0xAB7E40 (the frame loop: max-packet check, state 4, the status / consumed protocol, the pool block, the second 0xAB3520)
//   WwiseVorbisFraming.DriverAB3520          = 0xAB3520 (the window / overlap driver; its arithmetic is WwiseVorbisNative.WindowOverlap = 0xAB5A94's Combine, M6-002)
//   WwiseVorbisNative.PacketEntry            = 0xAB3780 (the packet entry, M6-002; its work-pointer assignment from the shared block is the DecoderState.WorkAssigned flag)
//   WwiseVorbisNative.StreamReset            = 0xAB3978 (the overlap save, M6-002)
//   WwiseVorbisDsp.AllocateAB3264 / TeardownAB3428 / ResetAB3244 = 0xAB3264 / 0xAB3428 / 0xAB3244 (the decoder state's buffers, the process-wide record 0x108E648, the start / end reset)
//   WwiseVorbisSetupCache.AcquireAB2D74 / ReleaseAB3120 = 0xAB2D74 / 0xAB3120 (the setup cache keyed by the vorb hash; the parse inside is WwiseVorbisNative.ParseSetup = 0xAB6380 + 0xAB63E0, M6-002)
//   WwiseSourceOutput.HandoffA73490          = 0xA73490 (the output hand-off to the voice's io state)
//
// What is not modelled (MISSING, and DEFAULTED here: not a seam, not a throw): the setup parse's bump-allocator accounting inside the decode-alloc size [F+0x70] (0xAB6380 / 0xAB63E0 / 0xAB2E1C..0xAB2EEC can fail
// when the blob needs more than the allocation; the C# parse has no such limit, so it succeeds where the engine's can fail on size, and a parse failure is the C#'s own InvalidDataException only; only a size below
// 0x30 throws). Also not modelled: the contents of the process-wide 0x2000-byte block (allocated and freed, never read by the decode), the pool's addresses (the allocation success or failure is the Func<bool> hook
// the stream manager already carries: true = the allocation succeeds), and the contents of a freshly allocated shared work buffer (zero here).

/// <summary>
/// The process-wide record at <c>0x108E648</c> (GOT <c>0x1040268</c>): <c>+0</c> an 0x2000-byte block, <c>+4</c> the size of the shared work buffer, <c>+8</c> the shared work buffer (the per-channel work pointers of
/// <c>0xAB3780</c> are slices of it), <c>+0xC</c> the number of decoder states that hold an overlap block (V12). The writer is <c>0xAB3264</c> (<c>0xAB3400</c>, <c>0xAB33E4</c>), the freer <c>0xAB3428</c>
/// (<c>0xAB34D0..0xAB3504</c>). The work buffer is ONE block for every decoder state of the process: <c>0xAB3780</c> sets <c>work[ch] = [R+8] + ch * slice</c> on every packet, and the insufficient-data tail
/// of <c>0xAB7E40</c> (<c>0xAB7EF4..0xAB7F14</c> calling <c>0xAB3978</c>) and the restore of <c>0xAB3830</c> read it with no flag test, so another decoder's packet decoded since is what they see. It is modelled as
/// that one buffer (<see cref="Mem"/>, one per <see cref="WwiseVorbisEngineContext"/>); a decoder's <c>Work</c> arrays are windows onto it (<see cref="WwiseVorbisNative.LoadSharedWork"/> /
/// <see cref="WwiseVorbisNative.StoreSharedWork"/>). THREADING CONTRACT: the engine's record is unsynchronised; a context and every source built on it must be driven from one thread at a time (the stack's
/// audio thread), exactly as the engine's single audio thread does. A decoder's work pointers are assigned at each packet (0xAB3780) and never moved by 0xAB3264: when another decoder's allocation replaces the buffer
/// (need > [R+4], 0xAB3324) the first decoder's later 0xAB3978 reads the freed one (use-after-free, contents undefined). That is MISSING and a visible stop here: <see cref="WwiseMissingBehaviourException"/> from the load. The contents of a freshly allocated buffer are zero here (malloc memory the engine has not initialised: not modelled).
/// </summary>
public sealed class WwiseVorbisSharedWork
{
    /// <summary><c>[R+0] != 0</c>: the 0x2000-byte block (allocated 16-aligned at the first <c>0xAB3264</c>).</summary>
    public bool HasBlock0 { get; set; }

    /// <summary><c>[R+4]</c> (signed): the shared work buffer's size, <c>channels &lt;&lt; 14</c> bytes of the largest allocation so far.</summary>
    public int Size4 { get; set; }

    /// <summary><c>[R+8] != 0</c>: the shared work buffer.</summary>
    public bool HasWork => Mem is not null;

    /// <summary><c>[R+8]</c>: the shared work buffer, <c>[R+4] / 4</c> floats.</summary>
    public float[]? Mem { get; set; }

    /// <summary><c>[R+0xC]</c>: the user count.</summary>
    public int Users { get; set; }
}

/// <summary>
/// The Vorbis engine's process-wide state: the setup cache <c>*0x108E638</c> (<see cref="SetupCache"/>), the shared work record <c>0x108E648</c> (<see cref="Shared"/>) and the packed codebook library the setup parse
/// reads (<see cref="Codebooks"/>, a host input: the engine's static table behind <c>0xAB63E0</c> is shipped data the stack loads from its asset).
/// </summary>
public sealed class WwiseVorbisEngineContext
{
    /// <summary>The setup cache <c>*0x108E638</c> (GOT <c>0x1040230</c>).</summary>
    public WwiseVorbisSetupCache SetupCache { get; } = new();

    /// <summary>The process-wide record <c>0x108E648</c> (GOT <c>0x1040268</c>).</summary>
    public WwiseVorbisSharedWork Shared { get; } = new();

    /// <summary>The packed codebook library; unset throws <see cref="WwiseMissingBehaviourException"/> where a setup is parsed.</summary>
    public WwiseCodebookLibrary? Codebooks { get; set; }

    /// <summary>Instrumentation, no behaviour: called with the setup bytes and the flag byte of every <c>0xAB2D74</c> call of a source of this context (the arguments the engine oracle logs).</summary>
    internal Action<ReadOnlyMemory<byte>, byte>? OnSetupAcquire { get; set; }

    /// <summary>Instrumentation, no behaviour: called with the channel count of every <c>0xAB3264</c> call of a source of this context.</summary>
    internal Action<byte>? OnDspAllocate { get; set; }
}

/// <summary>One setup record of the cache (the 0x1C-byte node of <c>0xAB2D74</c>): key <c>[+0]</c>, chain <c>[+4]</c>, the setup block <c>[+8..+0x14]</c> and the reference count <c>[+0x18]</c>.</summary>
public sealed class WwiseVorbisSetupNode
{
    /// <summary><c>[node+0]</c>: the vorb hash (<c>[F+0x78]</c>).</summary>
    public uint Key { get; set; }

    /// <summary><c>[node+4]</c>: the next node of the bucket's chain.</summary>
    public WwiseVorbisSetupNode? Next { get; set; }

    /// <summary><c>[node+8]</c> = base: the parsed setup (the value callers store at <c>[S+0x80]</c> / <c>[S+0x5C]</c>).</summary>
    public WwiseVorbisSetup? Setup { get; set; }

    /// <summary><c>[node+0x18]</c>: the reference count.</summary>
    public int RefCount { get; set; }
}

/// <summary>
/// The setup cache <c>*0x108E638</c> (GOT <c>0x1040230</c>): <c>+0</c> the bucket array, <c>+4</c> the bucket count, <c>+8</c> the bucket capacity, <c>+0xC</c> the node count. <see cref="AcquireAB2D74"/> is <c>0xAB2D74</c>
/// (lookup by the vorb hash; a miss allocates a node and a setup block, parses, links and grows the table at a load above 0.9f to the next prime of the list at <c>0x10045D0</c>), <see cref="ReleaseAB3120"/> is
/// <c>0xAB3120</c> (decrement; at 0 unlink and free; at an empty table free the bucket array).
/// </summary>
public sealed class WwiseVorbisSetupCache
{
    /// <summary>The prime list at <c>0x10045D0</c> (27 words, then a 0 that the scan never reads: <c>0xAB2F64..0xAB2F70</c> stops at the address <c>list + 0x68</c>).</summary>
    public static IReadOnlyList<uint> Primes { get; } = new uint[]
    {
        29, 53, 97, 193, 389, 769, 1543, 3079, 6151, 12289, 24593, 49157, 98317, 196613, 393241, 786433, 1572869, 3145739, 6291469,
        12582917, 25165843, 50331653, 100663319, 201326611, 402653189, 805306457, 1610612741,
    };

    private WwiseVorbisSetupNode?[]? _buckets;                                  // [T+0]

    /// <summary><c>[T+4]</c>: the bucket count.</summary>
    public uint BucketCount { get; private set; }

    /// <summary><c>[T+8]</c>: the bucket capacity.</summary>
    public uint Capacity { get; private set; }

    /// <summary><c>[T+0xC]</c>: the node count.</summary>
    public uint NodeCount { get; private set; }

    /// <summary>The node of <paramref name="key"/> (test and diagnostic access; the lookup of <c>0xAB2D74</c> without its side effects).</summary>
    internal WwiseVorbisSetupNode? Find(uint key)
    {
        if (BucketCount == 0) return null;
        for (var n = _buckets![key % BucketCount]; n is not null; n = n.Next)
            if (n.Key == key) return n;
        return null;
    }

    /// <summary>Every bucket's chain as <c>key:count,key:count</c> (head first), for diagnostics and the engine comparison.</summary>
    internal IEnumerable<string> DumpChains()
    {
        for (uint b = 0; b < BucketCount; b++)
        {
            var parts = new List<string>();
            for (var n = _buckets![b]; n is not null; n = n.Next) parts.Add($"{n.Key}:{n.RefCount}");
            yield return string.Join(",", parts);
        }
    }

    /// <summary>True while the bucket array is allocated (<c>[T+0] != 0</c>).</summary>
    public bool HasBuckets => _buckets is not null;

    /// <summary>
    /// <c>0xAB2D74(T, F, pbi, &amp;{ptr+2, size, flag})</c>: the setup for <paramref name="hash"/> (<c>[F+0x78]</c>) or null. A hit increments the node's count and returns its setup without reading the setup bytes.
    /// A miss allocates the 0x1C-byte node (null fails), then <paramref name="decodeAllocSize"/> (<c>[F+0x70]</c>) bytes for the setup block (null frees the node and fails), parses
    /// (<c>0xAB6380(base, bs0Pow, bs1Pow)</c> then <c>0xAB63E0(base, channels, reader, block)</c>; a failure frees both and returns null), then links the node, growing the table first when its load
    /// <c>NodeCount / BucketCount</c> (binary32) is above 0.9f (<c>0x3F666666</c>) or it has no buckets.
    /// </summary>
    public WwiseVorbisSetup? AcquireAB2D74(uint hash, uint decodeAllocSize, byte channels, byte bs0Pow, byte bs1Pow, ReadOnlyMemory<byte> setupBytes, WwiseCodebookLibrary codebooks, Func<bool> tryAlloc)
    {
        ArgumentNullException.ThrowIfNull(codebooks);
        ArgumentNullException.ThrowIfNull(tryAlloc);
        if (BucketCount != 0)                                                   // 0xAB2D7C..0xAB2D8C
        {
            uint idx = hash % BucketCount;                                      // 0xAB2D98..0xAB2DA0 __aeabi_uidivmod
            for (var n = _buckets![idx]; n is not null; n = n.Next)             // 0xAB2DA4..0xAB2DAC, 0xAB2E88..0xAB2E94
            {
                if (n.Key != hash) continue;                                    // 0xAB2E94..0xAB2E9C
                n.RefCount++;                                                   // 0xAB2EA0..0xAB2EA8
                return n.Setup;                                                 // 0xAB2EA4 add sb,r3,#8: the result's [0] is the base
            }
        }
        if (!tryAlloc()) return null;                                           // 0xAB2DBC bl 0xA7A7F4(pool, 0x1C); 0xAB2DC4..0xAB2DCC
        if (!tryAlloc()) return null;                                           // 0xAB2DFC bl 0xA7A7F4(pool, [F+0x70]); null: 0xAB2E74 frees the node
        if (decodeAllocSize < 0x30)                                             // 0xAB2E20..0xAB2E30: size < 0x30 hands 0xAB6380 a null base
            throw new InvalidOperationException("M6-025 V11: a decode-alloc size below 0x30 makes the engine hand 0xAB6380 a null base (a fault)");
        WwiseVorbisSetup setup;
        try
        {
            setup = WwiseVorbisNative.ParseSetup(new BitReader(setupBytes), codebooks, channels, bs0Pow, bs1Pow);   // 0xAB2E40 0xAB6380; 0xAB2EE4 0xAB63E0
        }
        catch (InvalidDataException)
        {
            return null;                                                        // 0xAB2E4C..0xAB2E84: both blocks freed, null
        }
        var node = new WwiseVorbisSetupNode { Key = hash, Setup = setup, RefCount = 1 };   // 0xAB2EF0..0xAB2F00 [node] = key; [node+0x18] = 1
        if (BucketCount == 0 || (float)NodeCount / (float)BucketCount > BitConverter.Int32BitsToSingle(0x3F666666))   // 0xAB2F04..0xAB2F34 vcvt.f32.u32, vdiv.f32, vcmpe, bgt
        {
            if (!Grow(tryAlloc) && BucketCount == 0) return null;               // 0xAB2F5C..0xAB2F90: no prime and no buckets: 0xAB2E4C (free, null); an allocation failure keeps the old table
        }
        Insert(node);                                                           // 0xAB2F38..0xAB2F58
        return setup;
    }

    private void Insert(WwiseVorbisSetupNode node)
    {
        uint idx = node.Key % BucketCount;                                      // 0xAB2F38 bl 0x4a6694
        node.Next = _buckets![idx];                                             // 0xAB2F3C..0xAB2F48
        _buckets[idx] = node;                                                   // 0xAB2F4C..0xAB2F50
        NodeCount++;                                                            // 0xAB2F40, 0xAB2F54
    }

    /// <summary>
    /// 0xAB2F5C..0xAB3108: the next prime above the bucket count (the scan of the list, ending at its 27th word), a new bucket array of that many buckets (<c>0xA7A7F4(*0x1052418, 4 * prime)</c>; a failure restores the
    /// old table), every node rehashed into it, the old array freed. Returns false when there was no prime above the count or the allocation failed.
    /// </summary>
    private bool Grow(Func<bool> tryAlloc)
    {
        uint prime = 0;
        for (int i = 0; i < Primes.Count; i++)                                  // 0xAB2F64..0xAB2F88
        {
            uint p = Primes[i];
            if (BucketCount >= p) continue;                                     // 0xAB2F7C bhs 0xAB2F70 (the end test at i == 26 sits in that path)
            prime = p;
            break;
        }
        if (prime == 0) return false;                                           // 0xAB2F8C
        var old = _buckets;
        uint oldCount = BucketCount;
        if (!tryAlloc()) return false;                                          // 0xAB2FD0 bl 0xA7A7F4; 0xAB2FDC beq 0xAB30EC: the old values are stored back
        var fresh = new WwiseVorbisSetupNode?[prime];                           // 0xAB3034..0xAB3070 zero every bucket
        _buckets = fresh;
        BucketCount = prime;                                                    // [T+4] counts the zeroed buckets up to prime
        Capacity = prime;                                                       // 0xAB3040 str r6,[r5,#8]
        if (old is not null)                                                    // 0xAB3074..0xAB30D4
        {
            for (uint b = 0; b < oldCount; b++)
            {
                for (var n = old[b]; n is not null;)
                {
                    var next = n.Next;                                          // 0xAB30A8 ldr r3,[fp,#4]
                    uint idx = n.Key % prime;                                   // 0xAB3090..0xAB30A0
                    n.Next = fresh[idx];                                        // 0xAB30B4..0xAB30B8
                    fresh[idx] = n;                                             // 0xAB30BC..0xAB30C0
                    n = next;                                                   // 0xAB30C4
                }
            }
        }
        return true;
    }

    /// <summary>
    /// <c>0xAB3120(T, F)</c>: with no buckets nothing happens. The node of <paramref name="hash"/> (<c>[F+0x78]</c>) is found by its chain (a missing one returns); its count is decremented; at 0 or below it is unlinked, the
    /// node count decremented and the setup block and node freed. Then, with the node count 0 and a bucket array, <c>[T+4] = 0</c>, the array is freed and <c>[T+0]</c>, <c>[T+8]</c> cleared.
    /// </summary>
    public void ReleaseAB3120(uint hash)
    {
        if (BucketCount == 0) return;                                           // 0xAB3128..0xAB3138
        uint idx = hash % BucketCount;                                          // 0xAB3144
        var cur = _buckets![idx];
        if (cur is null) return;                                                // 0xAB314C..0xAB3154
        WwiseVorbisSetupNode? prev = null;
        while (cur.Key != hash)                                                 // 0xAB3158..0xAB3180
        {
            prev = cur;
            cur = cur.Next;
            if (cur is null) return;
        }
        cur.RefCount--;                                                         // 0xAB3190..0xAB3198
        if (cur.RefCount <= 0)                                                  // 0xAB319C..0xAB31A0 ble 0xAB31DC
        {
            if (prev is not null) prev.Next = cur.Next;                         // 0xAB31E4
            else _buckets[idx] = cur.Next;                                      // 0xAB31EC
            NodeCount--;                                                        // 0xAB31E8..0xAB31F8
            cur.Setup = null;                                                   // 0xAB3208..0xAB3224 the setup block is freed
        }
        if (NodeCount == 0 && _buckets is not null)                             // 0xAB31A4..0xAB31B8
        {
            BucketCount = 0;                                                    // 0xAB31C0 str r4,[r6,#4]
            _buckets = null;                                                    // 0xAB31D0
            Capacity = 0;                                                       // 0xAB31D4
        }
    }
}

/// <summary>The decoder state's buffers (<c>0xAB3264</c>, <c>0xAB3428</c>) and its start / end reset (<c>0xAB3244</c>), over <see cref="WwiseVorbisNative.DecoderState"/> (the engine's <c>D = F + 0x10</c>).</summary>
internal static class WwiseVorbisDsp
{
    /// <summary>
    /// <c>0xAB3244(D, skip, trim)</c>: <c>u16[D+0x2C] = skip</c>, <c>u16[D+0x2E] = trim</c>, <c>[D+0x1C] = [D+0x20] = -1</c>; returns 0.
    /// </summary>
    internal static int ResetAB3244(WwiseVorbisNative.DecoderState d, ushort skip, ushort trim)
    {
        d.Skip = skip;                                                          // 0xAB3248
        d.Trim = trim;                                                          // 0xAB3254
        d.End = -1;                                                             // 0xAB3258
        d.Start = -1;                                                           // 0xAB325C
        return 0;
    }

    /// <summary>
    /// <c>0xAB3264(D, channels)</c>: <c>[D+0xC] = channels</c>; <c>channels * 8</c> bytes for the two pointer arrays (null: -1, <c>[D+0x14] = 0</c>); the overlap block of
    /// <c>((u32[[D+0x10]+4] &amp; ~3) * channels + 0xF) &amp; ~0xF</c> bytes (0 or null: -1 with <c>[[D+0x18]] = 0</c>); the process-wide record's 0x2000-byte block when absent, its work buffer when
    /// <c>channels &lt;&lt; 14 &gt; [R+4]</c> (signed) or absent (the old one freed); then the overlap block zeroed, <c>[D+0x30] = 1</c>, the previous / current block flags 0 and <c>[R+0xC]++</c>; returns 0.
    /// A failure leaves what was allocated until <c>0xAB3428</c> frees it, as the engine does.
    /// </summary>
    internal static int AllocateAB3264(WwiseVorbisNative.DecoderState d, int channels, WwiseVorbisSharedWork r, Func<bool> tryAlloc)
    {
        d.Channels = channels;                                                  // 0xAB3274 str r1,[r5,#0xc]
        if (!tryAlloc())                                                        // 0xAB3288 bl 0xA7A7F4(pool, channels * 8)
        {
            d.ArraysAllocated = false;                                          // 0xAB3290 str r0,[r5,#0x14]
            return -1;                                                          // 0xAB3408
        }
        d.ArraysAllocated = true;
        d.WorkAssigned = false;                                                 // 0xAB32A4 [[D+0x14]] = 0
        d.WorkBuffer = null;
        d.OverlapAllocated = false;                                             // 0xAB32B0 [[D+0x18]] = 0
        uint block = unchecked((uint)((((uint)d.Setup.BlockSize1 & ~3u) * (uint)channels + 0xFu) & ~0xFu));   // 0xAB32B8..0xAB32C8 bics
        if (block == 0) return -1;                                              // 0xAB32CC..0xAB32DC
        if (!tryAlloc()) return -1;                                             // 0xAB32E8 bl 0xA7A7F4(pool, block); 0xAB32FC beq 0xAB3408
        d.OverlapAllocated = true;                                              // 0xAB32F8 str r0,[r3]
        if (!r.HasBlock0)                                                       // 0xAB3308..0xAB3310
        {
            if (!tryAlloc()) return -1;                                         // 0xAB33F8 bl 0xA7A894(pool, 0x2000, 16); 0xAB3404 bne 0xAB3314
            r.HasBlock0 = true;                                                 // 0xAB3400 str r0,[sb]
        }
        int need = unchecked(channels << 14);                                   // 0xAB3318 lsl sb,r6,#0xe
        if (need > r.Size4 || !r.HasWork)                                       // 0xAB3324 bgt (signed); 0xAB3330 beq
        {
            if (!tryAlloc()) return -1;                                         // 0xAB33B4 bl 0xA7A894(pool, need, 16); 0xAB33BC beq 0xAB3408
            r.Size4 = need;                                                     // 0xAB33E4 stmib r3,{sb,sl}: [R+4], [R+8]
            r.Mem = new float[need / 4];                                        // (the old buffer was freed at 0xAB33D8; the new one's contents are uninitialised in the engine: zero here)
        }
        d.WindowSaved = 1;                                                      // 0xAB3348 strb 1,[r5,#0x30]
        d.PreviousFlag = 0;                                                     // 0xAB338C
        d.CurrentFlag = 0;                                                      // 0xAB3394
        r.Users++;                                                              // 0xAB3398..0xAB33A0
        d.SharedWork = r;                                                       // work[ch] = [R+8] + ch * slice (0xAB3780): the windows onto the shared buffer
        int bs1 = d.Setup.BlockSize1;                                           // the managed buffers (M6-002: Work and Overlap sized bs1, see DecoderState)
        d.Work = new float[channels][];
        d.Overlap = new float[channels][];
        for (int c = 0; c < channels; c++)
        {
            d.Work[c] = new float[bs1];
            d.Overlap[c] = new float[bs1];                                      // 0xAB3338..0xAB3344 memset(overlap, 0, block)
        }
        return 0;
    }

    /// <summary>
    /// <c>0xAB3428(D)</c>: with the arrays allocated, a non-null overlap block is freed (<c>[R+0xC]--</c>) and the array block freed, <c>[D+0x14] = [D+0x18] = 0</c>; then, when <c>[R+0xC] == 0</c>, the
    /// shared work buffer is freed (<c>[R+4] = [R+8] = 0</c>) and the 0x2000-byte block freed (<c>[R+0] = 0</c>).
    /// </summary>
    internal static void TeardownAB3428(WwiseVorbisNative.DecoderState d, WwiseVorbisSharedWork r)
    {
        if (d.ArraysAllocated)                                                  // 0xAB3428..0xAB3440
        {
            if (d.OverlapAllocated)                                             // 0xAB3448..0xAB3450
            {
                d.OverlapAllocated = false;                                     // 0xAB347C
                r.Users--;                                                      // 0xAB3484..0xAB348C
            }
            d.ArraysAllocated = false;                                          // 0xAB349C
            d.WorkAssigned = false;
            d.SharedWork = null;
            d.WorkBuffer = null;
            d.Work = Array.Empty<float[]>();
            d.Overlap = Array.Empty<float[]>();
        }
        if (r.Users != 0) return;                                               // 0xAB34AC..0xAB34B4
        if (r.HasWork)                                                          // 0xAB34B8..0xAB34C0
        {
            r.Mem = null;                                                       // 0xAB34D4
            r.Size4 = 0;                                                        // 0xAB34D8
        }
        if (r.HasBlock0) r.HasBlock0 = false;                                   // 0xAB34E4..0xAB3504
    }
}

/// <summary>
/// The Vorbis frame block <c>F</c> (the streamed class's <c>S+0x60..</c>, the in-memory class's <c>S+0x3C..</c>): what <c>0xAB7E40</c> reads and writes. <c>F+0x10</c> is the decoder state <c>D</c>
/// (<see cref="Dsp"/>, <c>F+0x1C</c> is <c>D+0xC</c>, the channel count; <c>F+0x24</c> is <c>D+0x14</c>; <c>F+0x2C</c> / <c>F+0x30</c> are <c>D+0x1C</c> / <c>D+0x20</c>, the start and end).
/// </summary>
internal sealed class WwiseVorbisFrameBlock
{
    /// <summary><c>[F+0x00]</c>: the frames the last call produced (<c>0xA73490</c> uses its low u16).</summary>
    public uint Frames { get; set; }

    /// <summary><c>[F+0x04]</c>: the status (0x2D data ready, 0x2E insufficient data, 0x11 last, 0x2B, 2).</summary>
    public int Status { get; set; }

    /// <summary><c>[F+0x08]</c>: the header state (0 before the header, 1 seek table, 2 setup, 3 running); <c>0xAB7E40</c> writes 4 when it has seen the last packet.</summary>
    public int State { get; set; }

    /// <summary><c>[F+0x0C]</c>: the bytes consumed.</summary>
    public uint Consumed { get; set; }

    /// <summary><c>F+0x10</c>: the decoder state <c>D</c>.</summary>
    public WwiseVorbisNative.DecoderState Dsp { get; } = new() { Start = 0, End = 0 };      // the engine's D is zeroed with the object; 0xAB3244 sets -1 / -1 before the first use

    /// <summary><c>[F+0x44]</c>: the output block of the last call (planar floats, <c>channels * n</c>), or null.</summary>
    public float[]? Output { get; set; }

    /// <summary><c>[F+0x48]</c>: the AkChannelConfig word (the raw u32 at <c>fmt+0x14</c>).</summary>
    public uint ChannelConfig { get; set; }

    /// <summary><c>[F+0x4C]</c>: the requested frame count of the last call.</summary>
    public uint FramesCopy { get; set; }

    /// <summary><c>[F+0x50]</c>: the bytes available at the packet pointer.</summary>
    public uint Avail { get; set; }

    /// <summary><c>byte [F+0x54]</c>: the ready flag (bit 0: the available bytes end at the stream's end).</summary>
    public byte Ready { get; set; }
}

/// <summary>The Vorbis frame loop and window / overlap driver (<c>0xAB7E40</c>, <c>0xAB3520</c>).</summary>
internal static class WwiseVorbisFraming
{
    /// <summary>
    /// <c>0xAB3520(D, out, n, lfeIdx)</c>: with <c>[D+0x1C] &gt;= [D+0x20]</c> (signed) 0; with no output buffer <c>end - start</c> and nothing else; else <c>frames = min(n, end - start)</c> (signed) frames
    /// per channel through the window combine (<see cref="WwiseVorbisNative.WindowOverlap"/>, <c>0xAB5A94</c> and the overlap write-back), channel <c>c</c> to the plane
    /// <c>c</c> below <paramref name="lfeIdx"/>, the last plane at <paramref name="lfeIdx"/> and <c>c - 1</c> above it (<c>0xAB3610..0xAB3620</c>, <c>0xAB374C..0xAB3750</c>), each plane <paramref name="n"/> floats
    /// apart; <c>[D+0x1C] += frames</c>, <c>[D+0x30] = 1</c>; returns the frames.
    /// </summary>
    internal static int DriverAB3520(WwiseVorbisNative.DecoderState d, float[]? output, int n, int lfeIdx)
    {
        int start = d.Start, end = d.End;                                       // 0xAB3528..0xAB3530
        if (start >= end) return 0;                                             // 0xAB3534..0xAB353C
        if (output is null) return end - start;                                 // 0xAB3540..0xAB3550
        int frames = Math.Min(n, end - start);                                  // 0xAB3558..0xAB3560
        int channels = d.Channels;
        var block = WwiseVorbisNative.WindowOverlap(d, frames, channels);       // 0xAB5A94 per channel, the overlap save 0xAB3668..0xAB3698, [D+0x1C] += frames, [D+0x30] = 1
        for (int ch = 0; ch < channels; ch++)
        {
            int plane = ch < lfeIdx ? ch : (ch == lfeIdx ? channels - 1 : ch - 1);   // 0xAB3610 sub ip,r5,#1; ble 0xAB374C; mov ip,r5; subeq ip,sl,#1
            Array.Copy(block, ch * frames, output, plane * n, frames);          // 0xAB3624 mul ip,n,ip: the plane is n floats apart
        }
        return frames;
    }

    /// <summary>
    /// <c>0xAB7E40(F, maxPacket, buf, &amp;F.Output)</c>: <c>[F] = 0</c>; per packet at <c>off</c> (<c>u16 size</c> then the body): fewer than <c>off + 2</c> bytes available, or the state 4, or fewer than
    /// <c>off + 2 + size</c> available, go to the insufficient-data tail (<c>[F+0xC] = off</c>, status 0x2E, and <c>0xAB3978</c> when <c>[[D+0x14]] != 0</c>); a size above <paramref name="maxPacket"/> (unsigned) gives
    /// <c>[F] = 0</c>, status 2. The packet is the last when the available bytes end with it and bit 0 of <c>[F+0x54]</c> is set (then <c>[F+8] = 4</c>); <c>0xAB3780</c> runs on it. <c>n = [D+0x20] - [D+0x1C]</c> of 0
    /// goes to the next packet; otherwise <c>[F+0xC] = end</c>, a <c>channels * n * 4</c>-byte 16-aligned pool block (null: <c>[F+0x4C] = 0</c>, status 2), <c>0xAB3520(D, block, n, lfe)</c> with <c>lfe</c> from the
    /// channel mask (<c>[F+0x48] &gt;&gt; 12</c>: bit 3 set gives <c>bit0 + bit1 + bit2</c>, else <c>channels + 1</c>), <c>[F+0x4C] = [F] = n</c>; state 4 then calls <c>0xAB3520(D, 0, 0, 0)</c> (0: status 0x11, else
    /// 0x2D if <c>[F] != 0</c> else 0x2E), otherwise status 0x2D.
    /// </summary>
    internal static void FrameLoopAB7E40(WwiseVorbisFrameBlock f, ushort maxPacket, WwiseBytePtr buf, Func<bool> tryAlloc)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(tryAlloc);
        var d = f.Dsp;
        f.Frames = 0;                                                           // 0xAB7E68 str r0,[r8]
        uint avail = f.Avail;                                                   // 0xAB7E54
        uint off = 0;
        while (true)
        {
            uint afterSize = unchecked(off + 2);                                // 0xAB7EE0
            byte ready = f.Ready;                                               // 0xAB7EE4
            if (avail >= afterSize)                                             // 0xAB7EE8..0xAB7EF0 bhs 0xAB7E78
            {
                ushort size = buf.U16((int)off);                                // 0xAB7E78
                if (maxPacket < size)                                           // 0xAB7E7C..0xAB7E84 blo 0xAB7F20
                {
                    f.Frames = 0;                                               // 0xAB7F20..0xAB7F28 stm r4,{r2,r3}
                    f.Status = 2;
                    return;                                                     // 0xAB7F2C
                }
                uint end = unchecked(afterSize + size);                         // 0xAB7E80
                if (f.State != 4 && avail >= end)                               // 0xAB7E88..0xAB7E94 beq 0xAB7EF4; 0xAB7E98..0xAB7EA4 blo 0xAB7EF4
                {
                    bool last = avail == end && (ready & 1) != 0;               // 0xAB7E98..0xAB7EA0 andeq r1,r1,#1; movne r1,#0
                    if (last) f.State = 4;                                      // 0xAB7EB8 strne sl,[r4,#8]
                    d.WorkAssigned = true;                                      // 0xAB3780: [D+0x14][i] = the shared block's slices
                    WwiseVorbisNative.PacketEntry(d, new ReadOnlyMemory<byte>(buf.Array!, buf.Index + (int)off + 2, size), last);   // 0xAB7EC8 bl 0xAB3780(D, {ptr, size, flag})
                    int n = d.End - d.Start;                                    // 0xAB7ECC..0xAB7ED8 subs ip,ip,r2
                    if (n == 0)                                                 // 0xAB7EDC bne 0xAB7F34
                    {
                        off = end;                                              // 0xAB7ED4 mov r3,r5: the next packet
                        continue;
                    }
                    f.Consumed = end;                                           // 0xAB7F40
                    if (n < 0) throw new WwiseMissingBehaviourException("M6-002 V02: [D+0x20] - [D+0x1C] is negative: 0xAB7F44..0xAB7F58 passes the wrapped size to the pool allocator 0xA7A894, whose body is not read");
                    if (!tryAlloc())                                            // 0xAB7F58 bl 0xA7A894(pool, channels * n * 4, 16)
                    {
                        f.Output = null;                                        // 0xAB7F68 str r0,[r3]
                        f.FramesCopy = 0;                                       // 0xAB7F6C
                        f.Status = 2;                                           // 0xAB7F74
                        return;
                    }
                    f.Output = new float[d.Channels * n];                       // 0xAB7F68
                    uint cfg = (f.ChannelConfig >> 12) & 0xFFFFF;               // 0xAB7F84 ubfx r3,r3,#0xc,#0x14
                    int lfe = (cfg & 8) != 0 ? (int)(((cfg >> 2) & 1) + (cfg & 1) + ((cfg >> 1) & 1)) : d.Channels + 1;   // 0xAB7F8C..0xAB7FA4
                    DriverAB3520(d, f.Output, n, lfe);                          // 0xAB7FB4 bl 0xAB3520(D, out, n, lfe)
                    f.FramesCopy = (uint)n;                                     // 0xAB7FC4
                    f.Frames = (uint)n;                                         // 0xAB7FCC
                    if (f.State != 4) { f.Status = 0x2D; return; }              // 0xAB7FB8..0xAB7FD4
                    int rest = DriverAB3520(d, null, 0, 0);                     // 0xAB7FF0 bl 0xAB3520(D, 0, 0, 0)
                    if (rest == 0) f.Status = 0x11;                             // 0xAB7FF4..0xAB7FFC
                    else f.Status = f.Frames != 0 ? 0x2D : 0x2E;                // 0xAB8000..0xAB800C (unreachable: the second call returns 0 after the first)
                    return;
                }
            }
            f.Consumed = off;                                                   // 0xAB7EF4..0xAB7EFC
            f.Status = 0x2E;                                                    // 0xAB7F00
            if (!d.ArraysAllocated)                                             // 0xAB7F04 ldr r3,[F+0x24]; ldr r3,[r3]: with the work-pointer array null (0xAB3264 failed or never ran) the engine loads from address 0
                throw new InvalidOperationException("M6-002 V03: [F+0x24] is null at 0xAB7F04 (the engine faults on the load through it)");
            if (d.WorkAssigned)                                                 // 0xAB7F0C cmp r3,#0: [work[0]] != 0
                WwiseVorbisNative.StreamReset(d);                               // 0xAB7F14 bl 0xAB3978
            return;
        }
    }
}

/// <summary>
/// The marker entry the window builder <c>0x9D4C24</c> writes (20 bytes at <c>[state+0x14]</c>): the PBI, the offset of the marker in the delivered block, and the 12-byte chunk entry
/// <c>{id, position, label}</c>.
/// </summary>
public readonly record struct WwiseMarkerWindowEntry(WwisePlayingInstance Pbi, uint Offset, uint Id, uint Position, uint Label);

/// <summary>
/// The 0x28-byte io state a source fills for the voice's pitch node (P01), at the engine's offsets: <c>+0x00</c> data (planar floats per channel for Vorbis, interleaved int16 for ADPCM and PCM), <c>+0x04</c> the
/// AkChannelConfig, <c>+0x0C</c> the maximum frames (the voice sets it before the call; the sources then overwrite it with the delivered count), <c>+0x0E</c> the valid frames, <c>+0x10</c> / <c>+0x14</c> the
/// marker count and array, <c>+0x18</c> the position of the first frame, <c>+0x20</c> the total, <c>+0x24</c> the media sample rate, <c>+0x28</c> the result.
/// </summary>
public sealed class WwiseDecodeState
{
    /// <summary><c>[state+0x00]</c>: the data (a block the source owns; the pitch node releases it through the source's <c>vt+0xC</c>), or null.</summary>
    public Array? Data { get; set; }

    /// <summary><c>[state+0x04]</c>: the AkChannelConfig word.</summary>
    public uint ChannelConfig { get; set; }

    /// <summary><c>[state+0x08]</c>: the plug-in result scratch (no adopted source body writes it).</summary>
    public uint Scratch08 { get; set; }

    private uint _word1C;

    /// <summary>
    /// <c>[state+0x1C]</c>: a float word (1.0f in the plug-in source's default state; no adopted source body writes it). The voice pass block the engine builds per voice (<c>0xA44A00..0xA44A48</c>) stores
    /// <c>[state+0x00..0x14]</c>, <c>+0x28</c>, <c>+0x2C</c> and not <c>+0x1C</c>, so unless a source writes it the word is uninitialised stack; <see cref="Word1CWritten"/> tells whether anything did.
    /// </summary>
    public uint Word1C
    {
        get => _word1C;
        set { _word1C = value; Word1CWritten = true; }
    }

    /// <summary>True once <see cref="Word1C"/> has been assigned. The engine's word is otherwise uninitialised stack (the play-position update <c>0xA05574</c> copies it, <c>0xA548C0</c>).</summary>
    public bool Word1CWritten { get; private set; }

    /// <summary><c>u16 [state+0x0C]</c>: the maximum frames, then the delivered count.</summary>
    public ushort MaxFrames { get; set; }

    /// <summary><c>u16 [state+0x0E]</c>: the valid frames.</summary>
    public ushort ValidFrames { get; set; }

    /// <summary><c>u16 [state+0x10]</c>: the marker count.</summary>
    public ushort MarkerCount { get; set; }

    /// <summary><c>[state+0x14]</c>: the marker window (null is the zero pointer).</summary>
    public WwiseMarkerWindowEntry[]? Markers { get; set; }

    /// <summary><c>[state+0x18]</c>: the position of the first frame (<c>[S+0x18]</c> before the increment).</summary>
    public uint Position { get; set; }

    /// <summary><c>[state+0x20]</c>: the total samples (<c>[S+0x14]</c>).</summary>
    public uint Total { get; set; }

    /// <summary><c>[state+0x24]</c>: the media sample rate.</summary>
    public uint Rate { get; set; }

    /// <summary><c>[state+0x28]</c>: the result code (0x2D data ready, 0x2E no data now, 0x2B data needed, 0x11 no more data, 2 failure ...).</summary>
    public int Code28 { get; set; } = 0xABCD;
}

/// <summary>What a source object has to offer to the shared output hand-off <c>0xA73490</c> (every codec class calls it).</summary>
public interface IWwiseSourceCommon
{
    /// <summary><c>[S+0xC]</c>: the owner PBI.</summary>
    WwisePlayingInstance Pbi { get; }

    /// <summary><c>[S+0x14]</c>: the total samples.</summary>
    uint TotalSamples14 { get; }

    /// <summary><c>[S+0x18]</c>: the position of the next frame.</summary>
    uint SampleBase18 { get; set; }

    /// <summary><c>u16 [S+0x38]</c>: the loop count.</summary>
    ushort LoopCount38 { get; set; }

    /// <summary><c>[S+0x24]</c>: the loop start sample.</summary>
    uint Word24 { get; }

    /// <summary><c>[S+0x28]</c>: the loop end sample.</summary>
    uint Word28 { get; }

    /// <summary><c>[S+0x2C]</c>: the marker container.</summary>
    WwiseChunkContainer Container2C { get; }

    /// <summary>The allocation hook of the pool (true = the allocation succeeds).</summary>
    Func<bool> TryAlloc { get; }

    /// <summary><c>vt+0x74(S, r1)</c>: the loop / end slot (streamed Vorbis <c>0xAB1138</c>, in-memory Vorbis <c>0xAB0374</c>, ADPCM and PCM <c>0xA742C8</c>); returns 0x11 or 0x2D.</summary>
    int LoopOrEnd74(int r1);
}

/// <summary>The output hand-off <c>0xA73490</c>.</summary>
public static class WwiseSourceOutput
{
    /// <summary>
    /// <c>0xA73490(S, buf, frames, rate, chcfg, state)</c>: <c>frames == 0</c> sets <c>u16[state+0xE] = 0</c> and <c>[state+0x28] = 0x2E</c>. Else <c>[state+0] = buf</c>, <c>u16[state+0xC] = u16[state+0xE] = frames</c>,
    /// <c>[state+4] = chcfg</c>; <c>pos = [S+0x18]</c>; the marker window <c>0x9D4C24(S+0x2C, pbi, state, pos)</c> (the PBI is never null here); <c>[state+0x24] = rate</c>, <c>[state+0x18] = pos</c>,
    /// <c>[state+0x20] = [S+0x14]</c>, <c>[S+0x18] = pos + frames</c>. With the loop count 1 the result is 0x2D when <c>total &gt; newpos</c>, else <c>vt+0x74(S, 1)</c>; otherwise <c>newpos &gt; [S+0x28]</c> sets
    /// <c>[S+0x18] = [S+0x24]</c> and the result is <c>vt+0x74(S, 0)</c>, else 0x2D. The result is stored at <c>[state+0x28]</c> (<c>0xA73514</c>). No sample is copied or clipped.
    /// </summary>
    public static void HandoffA73490(IWwiseSourceCommon s, Array? buf, uint frames, uint rate, uint chcfg, WwiseDecodeState state)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(state);
        if (frames == 0)                                                        // 0xA73494..0xA7349C
        {
            state.ValidFrames = 0;                                              // 0xA73520
            state.Code28 = 0x2E;                                                // 0xA73524
            return;
        }
        state.Data = buf;                                                       // 0xA734B4
        state.MaxFrames = unchecked((ushort)frames);                            // 0xA734B8
        state.ChannelConfig = chcfg;                                            // 0xA734BC
        state.ValidFrames = unchecked((ushort)frames);                          // 0xA734C0
        uint pos = s.SampleBase18;                                              // 0xA734C4
        s.Container2C.BuildWindow9D4C24(s.Pbi, state, pos, s.TryAlloc);         // 0xA734CC..0xA734D8 (the PBI test at 0xA734B0 / 0xA734C8: a PBI is always present in the C#)
        pos = s.SampleBase18;                                                   // 0xA734DC
        ushort loops = s.LoopCount38;                                           // 0xA734E0
        uint newPos = unchecked(pos + frames);                                  // 0xA734E4
        uint total = s.TotalSamples14;                                          // 0xA734E8
        state.Rate = rate;                                                      // 0xA734F0
        state.Position = pos;                                                   // 0xA734F4
        state.Total = total;                                                    // 0xA734F8
        s.SampleBase18 = newPos;                                                // 0xA734FC
        int result;
        if (loops == 1)                                                         // 0xA734EC cmp r1,#1; 0xA73500 beq 0xA7352C
        {
            result = total > newPos ? 0x2D : s.LoopOrEnd74(1);                  // 0xA7352C..0xA73544 (bhi 0xA73510; vt+0x74(S, 1))
        }
        else if (newPos > s.Word28)                                             // 0xA73504..0xA7350C bhi 0xA73548
        {
            s.SampleBase18 = s.Word24;                                          // 0xA73548..0xA73558
            result = s.LoopOrEnd74(0);                                          // 0xA7355C..0xA73560
        }
        else
        {
            result = 0x2D;                                                      // 0xA73510
        }
        state.Code28 = result;                                                  // 0xA73514 str r0,[r4,#0x28]
    }
}
