// fidelity: M6-025, M6-024, M6-001
namespace Cozmo.Robot.Animation.Wwise;

// The bank loader's stream reader R (M6-wwise-bank.md C34.4 rows K17, K18, K19 of research/20261003-B-M6b-4-live-bodies-4.md with its verification's corrections 14 and 15): the memory-mode and stream-mode
// reads 0x9BBC14, 0x9BBF64, the skip 0x9BBF9C / 0x9BC140, the window pointer 0x9BBE5C and the small helpers 0x9BBB04, 0x9BBB90, 0x9BBB5C, 0x9BBBA4, 0x9BBBE0, 0x9BBB1C. Every function was disassembled in
// libcozmoEngine.so (ARM) when this file was written.
//
// Production entry. Engine: the loader 0x9B74D8 (type 0, mode 0: LoadSoundbank, M2) and every chunk handler it calls (BKHD 0x9B21F4, INIT 0x9B4048, ENVS 0x9B2988, PLAT 0x9B2B08, HIRC 0x9B3260, the DIDX / DATA
// handlers) read through R. The C# counterpart is WwiseBankReader, which WwiseBankLoader.Load9B74D8 and its chunk handlers read through; nothing in production constructs the loader yet
// (the bank-load wiring, C30.W, is parked).
//
// Replaces: the host-I/O stand-in of the reader (IWwiseBankStream, WwiseMemoryBankStream), whose results are now the engine's.
//
// Unread, named: the stream object's own bodies (IAkStdStream vt+0x1C read, vt+0x34 wait, vt+0x28 skip, vt+0x24 status, vt+8 release, and the open 0x9BC17C that creates it and the window): the
// IWwiseReaderStream seam. The memory mode is complete.

/// <summary>
/// The std-stream object <c>[R+0x1C]</c> the reader calls in stream mode. Its bodies are not adopted (the open <c>0x9BC17C</c> and the device stream behind it are unread), so the host supplies them;
/// the reader's own control flow around the calls is the engine's.
/// </summary>
public interface IWwiseReaderStream
{
    /// <summary>
    /// <c>vt+0x1C(stream, dst, bytes, flag = 1, [sp] = priority, [sp+4] = (float)bytes / throughput, [sp+8] = &amp;actual)</c> (<c>0x9BBD0C</c>, <c>0x9BC0AC</c>): starts a read of <paramref name="bytes"/> bytes into
    /// <paramref name="dst"/>; the result 1 is success, and <paramref name="actual"/> receives the byte count the following <see cref="WaitVt34"/> confirms.
    /// </summary>
    int ReadVt1C(Span<byte> dst, uint bytes, float deadline, sbyte priority, out uint actual);

    /// <summary><c>vt+0x34(stream)</c> (<c>0x9BBD88</c>, <c>0x9BC0D4</c>): waits for the read; 1 is success.</summary>
    int WaitVt34();

    /// <summary><c>vt+0x28(stream, .., bytes, 0, 1, &amp;actual)</c> (<c>0x9BC03C</c>): skips <paramref name="bytes"/> bytes in the stream; 1 is success.</summary>
    int SkipVt28(uint bytes, out uint actual);

    /// <summary><c>vt+0x24(stream, &amp;byte)</c> (<c>0x9BBE00</c>, <c>0x9BC0F0</c>): the status byte the reads fetch and the direct-read path ignores.</summary>
    byte StatusVt24();

    /// <summary><c>vt+8(stream)</c> (<c>0x9BBBBC</c>): the release <c>0x9BBBA4</c> calls.</summary>
    void ReleaseVt8();
}

/// <summary>
/// The reader <c>R</c> (K17): <c>+0</c> the window, <c>+4</c> its cursor, <c>+8</c> the bytes left (the memory mode's too), <c>+0xC</c> the minimum direct-read size, <c>+0x10</c> the block size, <c>+0x14</c> the temporary
/// copy buffer, <c>+0x18</c> the memory cursor, <c>+0x1C</c> the stream (null is memory mode), <c>+0x20</c> the throughput divisor (float), <c>+0x24</c> the priority (signed byte).
/// </summary>
public sealed class WwiseBankReader
{
    private byte[]? _window;               // [R+0]
    private int _cursor;                   // [R+4], an index into _window
    private byte[]? _memory;               // [R+0x18], the array
    private int _memoryIndex;              // [R+0x18], the index

    /// <param name="memory">The host allocator the temporary copy buffer comes from (<c>0xA7A894(pool, size, 0x20)</c>).</param>
    public WwiseBankReader(WwiseBankMemory? memory = null) => Memory = memory;

    /// <summary>The allocator of the window-pointer function's temporary buffer (<c>0xA7A894</c>, pool <c>0x1052418</c>); required in stream mode.</summary>
    public WwiseBankMemory? Memory { get; }

    /// <summary><c>[R+8]</c>: the bytes left in the window (stream mode) or in the memory (memory mode).</summary>
    public uint Left8 { get; set; }

    /// <summary><c>[R+0xC]</c>: the minimum direct-read size.</summary>
    public uint MinDirectC { get; set; }

    /// <summary><c>[R+0x10]</c>: the block size.</summary>
    public uint BlockSize10 { get; set; }

    /// <summary><c>[R+0x14]</c>: the temporary copy buffer's address (0 for none); the block is <see cref="TempBlock"/>.</summary>
    public uint Temp14 { get; private set; }

    /// <summary>The temporary copy buffer's bytes.</summary>
    public byte[]? TempBlock { get; private set; }

    /// <summary><c>[R+0x1C]</c>: the stream (null is memory mode). Its open is unread, so the host sets it together with <see cref="SetWindow"/>.</summary>
    public IWwiseReaderStream? Stream { get; set; }

    /// <summary><c>[R+0x20]</c>: the throughput divisor.</summary>
    public float Throughput20 { get; set; }

    /// <summary><c>[R+0x24]</c>: the priority.</summary>
    public sbyte Priority24 { get; set; }

    /// <summary>The window array (<c>[R+0]</c>), its cursor index (<c>[R+4]</c>); set by the host's open.</summary>
    public void SetWindow(byte[]? window) { _window = window; _cursor = 0; }

    /// <summary><c>[R+4] - [R]</c>: the window cursor index (an observation point for the tests).</summary>
    internal int WindowCursor4 => _cursor;

    /// <summary><c>[R+0x18]</c> as an index into the memory, or -1 when it is null (an observation point for the tests).</summary>
    internal int MemoryIndex18 => _memory is null ? -1 : _memoryIndex;

    /// <summary>True once <see cref="Release9BBBA4"/> ran.</summary>
    public bool Closed { get; private set; }

    /// <summary><c>0x9BBB04(R)</c>: <c>[R+8] = 0</c>, <c>[R+0x18] = 0</c>, <c>[R+4] = [R]</c>.</summary>
    public void Reset9BBB04()
    {
        Left8 = 0;
        _memory = null;
        _memoryIndex = 0;
        _cursor = 0;
    }

    /// <summary><c>0x9BBB90(R, ptr, n)</c> (<c>0x9B7820..0x9B7830</c>): the memory-mode setup: <c>[R+0x18] = ptr</c>, <c>[R+8] = n</c>; returns 1.</summary>
    public int SetupMemory9BBB90(byte[] memory, int index, uint n)
    {
        ArgumentNullException.ThrowIfNull(memory);
        _memory = memory;
        _memoryIndex = index;
        Left8 = n;
        return 1;
    }

    /// <summary>
    /// <c>0x9BBB5C(R, f, prio)</c>: <c>f &gt;= 0</c> (an unordered compare is not negative) and <c>(byte)prio &lt;= 100</c> store <c>[R+0x20] = f</c>, <c>[R+0x24] = prio</c> and return 1; otherwise 0x1F. The priority byte
    /// is compared unsigned (<c>uxtb; cmp #0x64; bls</c>); the reader loads it signed (<c>ldrsb</c>), verification correction 15.
    /// </summary>
    public int SetHeuristics9BBB5C(float f, byte prio)
    {
        if (f < 0f) return 0x1F;                                                   // 0x9BBB5C..0x9BBB68 vcmpe s15,#0; bmi
        if (prio > 0x64) return 0x1F;                                              // 0x9BBB6C..0x9BBB70
        Priority24 = unchecked((sbyte)prio);                                       // 0x9BBB74
        Throughput20 = f;                                                          // 0x9BBB78
        return 1;
    }

    /// <summary><c>0x9BBBA4(R)</c>: a non-null stream gets <c>vt+8</c> and is cleared; <c>[R+0x18] = 0</c>, <c>[R+8] = 0</c>; returns 1.</summary>
    public int Release9BBBA4()
    {
        if (Stream is { } stream)                                                  // 0x9BBBAC..0x9BBBB0
        {
            stream.ReleaseVt8();                                                   // 0x9BBBB8..0x9BBBC0
            Stream = null;                                                         // 0x9BBBC8
        }
        _memory = null;                                                            // 0x9BBBD4
        _memoryIndex = 0;
        Left8 = 0;                                                                 // 0x9BBBD8
        Closed = true;
        return 1;
    }

    /// <summary><c>0x9BBBE0(R)</c>: a non-null <c>[R+0x14]</c> is freed (<c>0xA7A914</c>) and cleared.</summary>
    public void FreeTemp9BBBE0()
    {
        if (Temp14 == 0) return;
        Memory?.Free(Temp14);
        Temp14 = 0;
        TempBlock = null;
    }

    /// <summary>
    /// <c>0x9BBC14(R, dst, n, &amp;got)</c> (K18). <c>*got = 0</c>. Memory mode (<c>[R+0x1C] == 0</c>, <c>0x9BBE14..0x9BBE58</c>): <c>got = min(n, [R+8])</c> (the compare is <c>n &gt;= left</c>), <c>[R+8] -= got</c>, the bytes are copied from the
    /// memory cursor, which advances; returns 1. Stream mode: <c>n == 0</c> returns 1; loop: window bytes left are copied (<c>min(left, n)</c>) and the window cursor advances; an empty window with <c>[R+0xC] &lt;= n</c> reads
    /// <c>block * (n / block)</c> bytes straight into <c>dst</c> through stream <c>vt+0x1C</c> (deadline <c>(float)bytes / [R+0x20]</c>, priority <c>[R+0x24]</c>, flag 1), a result other than 1 is returned, then <c>vt+0x34</c> must
    /// return 1 and the actual count is added; a remainder at or above <c>[R+0xC]</c> after a direct read returns 2 (<c>0x9BBDF4..0x9BBE08</c>: the <c>vt+0x24</c> status is fetched and ignored, verification correction 14);
    /// with <c>[R+0xC] &gt; n</c> the window is refilled the same way (<c>block * ([R+0xC] / block)</c> bytes into <c>[R]</c>, the actual count in <c>[R+8]</c>, the cursor reset). Returns 1 when <paramref name="dst"/> is full.
    /// </summary>
    public int Read9BBC14(Span<byte> dst, out int got)
    {
        got = 0;                                                                   // 0x9BBC38 str r3,[r7]
        uint n = (uint)dst.Length;
        if (Stream is null)                                                        // 0x9BBC3C beq 0x9BBE14
        {
            uint take = n >= Left8 ? Left8 : n;                                    // 0x9BBE20..0x9BBE28: cmp r2,r3; movhs r5,r3
            got = (int)take;                                                       // 0x9BBE28
            Left8 -= take;                                                         // 0x9BBE30..0x9BBE34
            if (take != 0)
            {
                if (_memory is null) throw new InvalidOperationException("the memory cursor [R+0x18] is null while the reader has bytes left");
                _memory.AsSpan(_memoryIndex, (int)take).CopyTo(dst);               // 0x9BBE3C bl memcpy
            }
            _memoryIndex += (int)take;                                             // 0x9BBE4C..0x9BBE50
            return 1;
        }
        var stream = Stream;
        if (n == 0) return 1;                                                      // 0x9BBC40..0x9BBC44
        int dstPos = 0;
        while (true)
        {
            if (Left8 != 0)                                                        // 0x9BBC50..0x9BBC58: [R+8] != 0 copies from the window
            {
                do
                {
                    uint left = Left8;
                    uint k = n >= left ? left : n;                                 // 0x9BBC5C..0x9BBC6C movhs/movlo
                    n -= k;                                                        // 0x9BBC74
                    if (_window is null) throw new InvalidOperationException("the window [R] is null while the reader has bytes left");
                    _window.AsSpan(_cursor, (int)k).CopyTo(dst.Slice(dstPos));     // 0x9BBC78 bl memcpy
                    dstPos += (int)k;                                              // 0x9BBC84
                    got += (int)k;                                                 // 0x9BBC88, 0x9BBC94
                    _cursor += (int)k;                                             // 0x9BBC8C
                    Left8 -= k;                                                    // 0x9BBC98..0x9BBCA0
                    if (n == 0) return 1;                                          // 0x9BBCA4..0x9BBCA8 beq 0x9BBDAC
                } while (Left8 != 0);                                              // 0x9BBCAC..0x9BBCB4
            }
            uint minDirect = MinDirectC;                                           // 0x9BBCB8
            uint block = BlockSize10;                                              // 0x9BBCBC
            if (minDirect > n)                                                     // 0x9BBCC0..0x9BBCC4 bhi 0x9BBD2C: refill the window
            {
                uint bytes = block * (minDirect / block);                          // 0x9BBD30..0x9BBD54: uidiv; mul
                if (_window is null) throw new InvalidOperationException("the window [R] is null");
                int r = stream.ReadVt1C(_window.AsSpan(), bytes, (float)bytes / Throughput20, Priority24, out uint actual);   // 0x9BBD6C..0x9BBD70 vt+0x1C(dst = [R], .., &[R+8])
                Left8 = actual;                                                    // the actual count is stored through [sp+8] = &[R+8]
                if (r != 1) return r;                                              // 0x9BBD74..0x9BBD7C
                int w = stream.WaitVt34();                                         // 0x9BBD80..0x9BBD8C
                if (w != 1) return r;                                              // 0x9BBD90..0x9BBD94 bne 0x9BBD20: mov r0,sl returns the READ's result (1), not the wait's
                _cursor = 0;                                                       // 0x9BBD9C..0x9BBDA4 [R+4] = [R]
                if (Left8 == 0) return 1;                                          // 0x9BBDA0..0x9BBDA8 (bne 0x9BBC54 else 0x9BBDAC)
                continue;                                                          // the window has bytes: copy them
            }
            // 0x9BBCC8..0x9BBD10: a direct read of block * (n / block) bytes into dst
            uint direct = block * (n / block);                                     // 0x9BBCC8..0x9BBCF4
            int rd = stream.ReadVt1C(dst.Slice(dstPos), direct, (float)direct / Throughput20, Priority24, out uint act);   // 0x9BBD0C..0x9BBD10
            if (rd != 1) return rd;                                                // 0x9BBD14..0x9BBD1C
            int wait = stream.WaitVt34();                                          // 0x9BBDB8..0x9BBDC4
            if (wait != 1) return rd;                                              // 0x9BBDC8..0x9BBDCC bne 0x9BBD20: mov r0,sl returns the READ's result (1), not the wait's
            n -= act;                                                              // 0x9BBDD8
            dstPos += (int)act;                                                    // 0x9BBDDC
            got += (int)act;                                                       // 0x9BBDE0..0x9BBDE4
            if (n >= MinDirectC)                                                   // 0x9BBDE8..0x9BBDF0 blo 0x9BBCA4
            {
                _ = stream.StatusVt24();                                           // 0x9BBDF4..0x9BBE04 (fetched and ignored)
                return 2;                                                          // 0x9BBE08
            }
            if (n == 0) return 1;                                                  // 0x9BBCA4..0x9BBCA8
            // 0x9BBCAC..0x9BBCB4: left == 0 (the window is empty) -> the refill / direct logic again
        }
    }

    /// <summary><c>0x9BBF64(R, dst, n)</c>: <c>0x9BBC14</c> with a local count; a result of 1 with <c>got != n</c> returns 0x38 (<c>0x9BBF64..0x9BBF98</c>).</summary>
    public int ReadRaw9BBF64(Span<byte> dst)
    {
        int r = Read9BBC14(dst, out int got);                                      // 0x9BBF7C
        if (r == 1 && dst.Length != got) return 0x38;                              // 0x9BBF80..0x9BBF90
        return r;
    }

    /// <summary>
    /// <c>0x9BBF9C(R, n, &amp;skipped)</c> (K19). Memory mode (<c>0x9BC140</c>): skips <c>n &gt;= left ? left : n</c> bytes (the cursor advances, <c>[R+8]</c> drops), returns 1. Stream mode: <c>n == 0</c> returns 1;
    /// window bytes are consumed first; an empty window with <c>n &gt; [R+0xC]</c> skips through stream <c>vt+0x28</c> and accumulates the actual count; with <c>n &lt;= [R+0xC]</c> the window is refilled by one direct read of
    /// <c>block * ([R+0xC] / block)</c> bytes, the wait <c>vt+0x34</c> must return 1, a non-zero status byte (<c>vt+0x24</c>) with fewer than <c>n</c> bytes in the window returns 2 (<c>0x9BC174</c>), else <c>n</c> bytes are consumed.
    /// </summary>
    public int Skip9BBF9C(uint n, out uint skipped)
    {
        skipped = 0;                                                               // 0x9BBFBC
        if (Stream is null)                                                        // 0x9BBFC0 beq 0x9BC140
        {
            uint take = n >= Left8 ? Left8 : n;                                    // 0x9BC14C..0x9BC150
            skipped = take;                                                        // 0x9BC154
            Left8 -= take;                                                         // 0x9BC158..0x9BC160
            _memoryIndex += (int)take;                                             // 0x9BC164..0x9BC16C
            return 1;
        }
        var stream = Stream;
        while (n != 0)                                                             // 0x9BBFC4 cmp r1,r2; 0x9BC010
        {
            if (Left8 != 0)                                                        // 0x9BBFD4..0x9BBFDC
            {
                uint k = n >= Left8 ? Left8 : n;                                   // 0x9BBFE4..0x9BBFEC cmp r5,r3; movlo r3,r5
                skipped += k;                                                      // 0x9BBFF0, 0x9BBFFC
                _cursor += (int)k;                                                 // 0x9BBFF4..0x9BBFF8
                n -= k;                                                            // 0x9BC000
                Left8 -= k;                                                        // 0x9BC004..0x9BC00C
                continue;
            }
            uint minDirect = MinDirectC;                                           // 0x9BC020
            if (minDirect < n)                                                     // 0x9BC028 cmp r0,r5; bhs 0x9BC064 not taken: n > [R+0xC]
            {
                int r = stream.SkipVt28(n, out uint actual);                       // 0x9BC030..0x9BC040
                if (r != 1) return r;                                              // 0x9BC044..0x9BC048
                n -= actual;                                                       // 0x9BC04C..0x9BC054
                skipped += actual;                                                 // 0x9BC050, 0x9BC058..0x9BC05C
                continue;
            }
            uint block = BlockSize10;                                              // 0x9BC064
            uint bytes = block * (minDirect / block);                              // 0x9BC068..0x9BC094
            if (_window is null) throw new InvalidOperationException("the window [R] is null");
            int rd = stream.ReadVt1C(_window.AsSpan(), bytes, (float)bytes / Throughput20, Priority24, out uint act);   // 0x9BC0AC..0x9BC0B0 (the actual count lands in [R+8])
            Left8 = act;
            if (rd != 1) return rd;                                                // 0x9BC0B4..0x9BC0BC
            int w = stream.WaitVt34();                                             // 0x9BC0CC..0x9BC0D8
            if (w != 1) return rd;                                                 // 0x9BC0DC..0x9BC0E0 bne 0x9BC0C0: mov r0,sb returns the READ's result (1), not the wait's
            byte status = stream.StatusVt24();                                     // 0x9BC0E4..0x9BC0F8
            if (status != 0 && Left8 < n) return 2;                                // 0x9BC0FC..0x9BC10C blo 0x9BC174
            skipped += n;                                                          // 0x9BC110..0x9BC11C
            _cursor = (int)n;                                                      // 0x9BC118..0x9BC124 [R+4] = [R] + n
            Left8 -= n;                                                            // 0x9BC12C..0x9BC134
            return 1;                                                              // 0x9BC114
        }
        return 1;                                                                  // 0x9BC018
    }

    /// <summary>
    /// <c>0x9BBE5C(R, n)</c> (K19): the pointer to the next <paramref name="n"/> bytes. Memory mode returns the memory cursor and advances by <c>min(n, left)</c> (<c>0x9BBF20..0x9BBF44</c>). Stream mode: a window with <c>[R+8] &gt;= n</c> returns the
    /// cursor and advances (<c>0x9BBF00..0x9BBF1C</c>); otherwise a block of <c>n + pad</c> bytes (<c>pad = 0x20 - ([R+8] &amp; 0x1F)</c> when that remainder is non-zero, with the size <c>n + 0x1F</c>; else <c>n</c>) is allocated aligned to 0x20
    /// into <c>[R+0x14]</c> (<c>0xA7A894</c>; a failure returns null), <c>n</c> bytes are read into <c>block + pad</c> through <c>0x9BBC14</c>, and the pointer is returned when the read gave 1 and exactly <c>n</c> bytes; else the block is freed,
    /// <c>[R+0x14] = 0</c> and null is returned (<c>0x9BBE5C..0x9BBF5C</c>).
    /// </summary>
    public WwiseBytePtr Window9BBE5C(uint n)
    {
        if (Stream is null)                                                        // 0x9BBE64 beq 0x9BBF20
        {
            var ptr = new WwiseBytePtr(_memory, _memoryIndex);                     // 0x9BBF24 r0 = [R+0x18]
            uint take = n >= Left8 ? Left8 : n;                                    // 0x9BBF28..0x9BBF30 cmp r1,r2; movhs r1,r2
            Left8 -= take;                                                         // 0x9BBF34..0x9BBF3C
            _memoryIndex += (int)take;                                             // 0x9BBF38..0x9BBF40
            return ptr;
        }
        if (Left8 >= n)                                                            // 0x9BBE74..0x9BBE7C bhs 0x9BBF00
        {
            var ptr = new WwiseBytePtr(_window, _cursor);                          // 0x9BBF00 r4 = [R+4]
            Left8 -= n;                                                            // 0x9BBF04..0x9BBF08
            _cursor += (int)n;                                                     // 0x9BBF0C..0x9BBF10
            return ptr;
        }
        uint rem = Left8 & 0x1F;                                                   // 0x9BBE84 ands r2,r2,#0x1f
        uint pad = rem != 0 ? 0x20 - rem : 0;                                      // 0x9BBE88 rsbne r6,r2,#0x20; moveq r6,r2
        uint size = rem != 0 ? n + 0x1F : n;                                       // 0x9BBE8C addne r2,r1,#0x1f; moveq r2,r1
        var memory = Memory ?? throw new WwiseMissingBehaviourException("M6-025 K19: 0x9BBE5C in stream mode allocates its copy buffer through 0xA7A894; supply WwiseBankReader.Memory");
        uint? address = memory.AllocateAlignedA7A894((int)size, 0x20);             // 0x9BBEB0 bl 0xA7A894(pool, size, 0x20)
        Temp14 = address ?? 0;                                                     // 0x9BBEB8 str r0,[r8,#0x14]
        if (address is null) return default;                                       // 0x9BBEBC beq 0x9BBEF4 (null)
        TempBlock = memory.Block(address.Value);
        var buffer = TempBlock.AsSpan((int)pad, (int)n);                           // 0x9BBEC0 add r4,r0,r6
        int r = Read9BBC14(buffer, out int got);                                   // 0x9BBED4 bl 0x9BBC14(R, r4, n, &got)
        if (r == 1 && got == (int)n) return new WwiseBytePtr(TempBlock, (int)pad); // 0x9BBED8, 0x9BBF48..0x9BBF54
        memory.Free(address.Value);                                                // 0x9BBEE0..0x9BBEE8 0xA7A914
        Temp14 = 0;                                                                // 0x9BBEF0
        TempBlock = null;
        return default;                                                            // 0x9BBEF4
    }
}
