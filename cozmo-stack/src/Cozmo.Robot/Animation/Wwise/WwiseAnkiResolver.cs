// fidelity: M6-024
using System.Text;

namespace Cozmo.Robot.Animation.Wwise;

// The Anki file-location resolver, the zip index and the zip registry of the Wwise runtime (M6-wwise-bank.md C33.1; research live-bodies-3 sections 2 to 4 with the
// verifier's corrections). Every function below was disassembled in libcozmoEngine.so (Thumb) when this file was written; the addresses are in the comments.
//
// Production entry. Engine: CozmoAudioController::CozmoAudioController (0x592BB0) builds the SetupConfig and calls InitializeAudioEngine (0x8D80D8) -> 0x8D81B0 ->
// 0x8D6C02 (the resolver, B1) and 0x8D8228 (the post-init: search path, path mapper, zips, language). The C# counterpart of the engine's resolver object is
// WwiseAnkiResolver (the hook object `this+4` is the same C# object); nothing in production constructs it yet (the Wwise runtime is not wired, B-M6b-4 C30.W), so the tests drive
// it through its public vtable counterparts (OpenByName = vt+8, OpenById = vt+0xC, Read, Close) and through the stream manager's CreateAuto.
//
// The host. The engine calls libc (stat, fopen, fseek, fread, fclose, open, pread, close). The C# host abstraction below is exactly that set of calls. The JNI half of the
// SetupConfig (the OBB directory listing, the APK path, the AAssetManager) is a host input: the zip list a caller passes to AddZipFiles is the vector the constructor fills.

/// <summary>The result of <c>stat</c> the resolver reads: <c>st_mode</c> (<c>[st+0x10]</c>) and <c>st_size</c> (<c>[st+0x30]</c>, 64 bit).</summary>
public readonly record struct WwiseHostStat(uint Mode, long Size)
{
    /// <summary>Bit 15 of <c>st_mode</c>, tested as <c>ldrb [sp+0x39]; lsls #0x18; bmi</c> (0x8D7046..0x8D704C, 0x8D83A6..0x8D83AC): a regular file.</summary>
    public bool IsRegular => (Mode & 0x8000) != 0;

    /// <summary>Bit 14 of <c>st_mode</c>, tested as <c>ldrb [sp+0x11]; lsls #0x19; bmi</c> (0x8DAE30..0x8DAE36): a directory.</summary>
    public bool IsDirectory => (Mode & 0x4000) != 0;
}

/// <summary>A <c>FILE*</c> or a file descriptor: the calls the resolver and the zip index make on it.</summary>
public interface IWwiseHostFile
{
    /// <summary><c>fseek(file, offset, 0)</c> (0x8D6E9C); 0 is success.</summary>
    int Fseek(long offset);

    /// <summary><c>fread(buf, 1, count, file)</c> (0x8D6EAC): the number of bytes read.</summary>
    int Fread(byte[] buffer, int index, int count);

    /// <summary><c>pread(fd, buf, count, offset)</c> (0x8DE3E0): the byte count, 0 at the end of the file, or -1 with <paramref name="errno"/> set. The offset is 32 bit in the engine.</summary>
    int Pread(byte[] buffer, int index, int count, uint offset, out int errno);

    /// <summary><c>fclose</c> / <c>close</c>; 0 is success.</summary>
    int Close();
}

/// <summary>The libc file calls of the resolver (<c>stat</c>, <c>fopen</c>, <c>open(O_RDONLY)</c>).</summary>
public interface IWwiseHostFileSystem
{
    /// <summary><c>stat(path, &amp;st)</c>: false when it fails (a non-zero return).</summary>
    bool TryStat(string path, out WwiseHostStat stat);

    /// <summary><c>fopen(path, mode)</c> (0x8D7154), null on failure. Only the read mode is reachable (see <see cref="WwiseAnkiResolver.FopenA8D713C"/>).</summary>
    IWwiseHostFile? Fopen(string path, string mode);

    /// <summary><c>open(path, O_RDONLY)</c> (0x8DDF54 <c>movs r1,#0</c>): null on a negative descriptor.</summary>
    IWwiseHostFile? OpenReadOnly(string path);
}

/// <summary>The AAssetManager calls of 0x8D7288 and 0x8D6E88 (the asset path is off in the shipped configuration, A9: <c>cfg[0x40]</c> is never written).</summary>
public interface IWwiseHostAssetManager
{
    /// <summary><c>AAssetManager_open(mgr, path, 0)</c>: null on failure.</summary>
    IWwiseHostAsset? Open(string path);
}

/// <summary>One open asset (<c>AAsset_seek</c>, <c>AAsset_read</c>, <c>AAsset_getLength</c>, <c>AAsset_close</c>).</summary>
public interface IWwiseHostAsset
{
    /// <summary><c>AAsset_getLength</c> (sign-extended into <c>iFileSize</c>, 0x8D732A..0x8D733C).</summary>
    int Length { get; }

    /// <summary><c>AAsset_seek(a, offset, 0)</c>: -1 on failure.</summary>
    int Seek(long offset);

    /// <summary><c>AAsset_read(a, buf, count)</c>.</summary>
    int Read(byte[] buffer, int index, int count);

    /// <summary><c>AAsset_close</c>.</summary>
    void Close();
}

/// <summary>The host's disk: the real libc calls through <see cref="System.IO"/>.</summary>
public sealed class WwiseDiskFileSystem : IWwiseHostFileSystem
{
    /// <inheritdoc />
    public bool TryStat(string path, out WwiseHostStat stat)
    {
        try
        {
            if (File.Exists(path)) { stat = new WwiseHostStat(0x81A4, new FileInfo(path).Length); return true; }
            if (Directory.Exists(path)) { stat = new WwiseHostStat(0x41ED, 4096); return true; }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
        stat = default;
        return false;
    }

    /// <inheritdoc />
    public IWwiseHostFile? Fopen(string path, string mode)
    {
        if (mode != "r") throw new NotSupportedException("M6-024 B8: only fopen mode \"r\" is reachable (0x8D739C returns 2 for any open mode other than 0)");
        return OpenReadOnly(path);
    }

    /// <inheritdoc />
    public IWwiseHostFile? OpenReadOnly(string path)
    {
        try { return new DiskFile(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return null; }
    }

    private sealed class DiskFile : IWwiseHostFile
    {
        private readonly FileStream _s;
        public DiskFile(FileStream s) => _s = s;

        public int Fseek(long offset)
        {
            try { _s.Position = offset; return 0; } catch (Exception e) when (e is IOException or ArgumentException) { return -1; }
        }

        public int Fread(byte[] buffer, int index, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = _s.Read(buffer, index + total, count - total);
                if (n <= 0) break;
                total += n;
            }
            return total;
        }

        public int Pread(byte[] buffer, int index, int count, uint offset, out int errno)
        {
            errno = 0;
            try
            {
                lock (_s)
                {
                    _s.Position = offset;
                    return _s.Read(buffer, index, count);
                }
            }
            catch (IOException) { errno = 5; return -1; }
        }

        public int Close() { _s.Dispose(); return 0; }
    }
}

/// <summary>
/// The 0x1C-byte <c>AkFileSystemFlags</c> block the stream manager hands the resolver (the copy of D7 <c>0x960B04</c> moves 0x1C bytes). Field offsets are the engine's: CreateAuto
/// stores <c>+0x12</c> (0x95F380), reads <c>+0x14</c> (0x95F390); the resolver reads <c>+0</c> (0x8D6E28), <c>+4</c> (0x8D6E34) and the byte <c>+0x10</c> (0x8D73F2).
/// </summary>
public sealed class WwiseFileFlags
{
    /// <summary><c>+0</c>: uCompanyID. The resolver accepts 0 and 1 only (<c>0x8D6E2A cmp r0,#1; bhi</c>).</summary>
    public uint CompanyId { get; set; }

    /// <summary><c>+4</c>: uCodecID. Non-zero selects <c>"%u.wem"</c>, zero <c>"%u.bnk"</c> (0x8D6E34..0x8D6E3E).</summary>
    public uint CodecId { get; set; }

    /// <summary><c>+8</c>: the custom parameter size.</summary>
    public uint CustomParamSize { get; set; }

    /// <summary><c>+0xC</c>: the custom parameter pointer.</summary>
    public uint CustomParam { get; set; }

    /// <summary><c>+0x10</c> (byte): bIsLanguageSpecific (<c>0x8D73F2 ldrb r0,[r6,#0x10]</c>).</summary>
    public byte LanguageSpecific { get; set; }

    /// <summary><c>+0x11</c> (byte): the third flag byte the Anki source stores (0xA745D0 <c>ubfx r4,r2,#1,#1</c>, <c>strb r4,[sp,#0x35]</c>).</summary>
    public byte Byte11 { get; set; }

    /// <summary><c>+0x12</c> (byte): set to 1 by CreateAuto (0x95F380 <c>strbne r0,[r2,#0x12]</c>).</summary>
    public byte Byte12 { get; set; }

    /// <summary><c>+0x14</c>: the cache id (0x95F390 <c>ldrne r8,[r2,#0x14]</c>).</summary>
    public uint CacheId { get; set; }

    /// <summary><c>+0x18</c>: the prefetch byte count (zero from the Anki source, 0xA745F0 <c>str ip,[sp,#0x3c]</c>).</summary>
    public uint PrefetchBytes { get; set; }

    /// <summary>The 0x1C-byte copy of D7 (<c>0x960B54..0x960B64</c> <c>ldm/stm</c> of seven words).</summary>
    public WwiseFileFlags Clone() => (WwiseFileFlags)MemberwiseClone();
}

/// <summary>
/// The 0x20-byte file descriptor <c>AkFileDesc</c>: <c>+0</c> <c>iFileSize</c> (64 bit), <c>+8</c> and <c>+0xC</c> words, <c>+0x10</c> the tag, <c>+0x14</c> the handle (a <c>FILE*</c>, the 8-byte zip
/// record or an <c>AAsset*</c>), <c>+0x18</c> the device id. The Anki resolver clears it first (0x8D6CD0 <c>blx memclr(fd, 0x20)</c>).
/// </summary>
public sealed class WwiseFileDesc
{
    /// <summary>The tag of a zip entry: <c>0x7A6970</c> = "zip" (0x8D7228..0x8D722C).</summary>
    public const uint TagZip = 0x7A6970;

    /// <summary>The tag of an asset: <c>0x61736D</c> = "asm" (0x8D732E..0x8D7334).</summary>
    public const uint TagAsset = 0x61736D;

    /// <summary><c>+0</c> (and <c>+4</c>): iFileSize, signed 64 bit (the stream manager tests <c>&lt; 1</c> with <c>sbcs</c>).</summary>
    public long FileSize { get; set; }

    /// <summary><c>+8</c>: zeroed by the resolver on a successful open (0x8D7230, 0x8D7342).</summary>
    public uint Word08 { get; set; }

    /// <summary><c>+0xC</c>.</summary>
    public uint Word0C { get; set; }

    /// <summary><c>+0x10</c>: 0 for a <c>FILE*</c>, <see cref="TagZip"/>, <see cref="TagAsset"/>.</summary>
    public uint Tag { get; set; }

    /// <summary><c>+0x14</c>: the handle.</summary>
    public object? Handle { get; set; }

    /// <summary><c>+0x18</c>: the device id the resolver stores first (<c>0x8D6CE2 str r1,[r4,#0x18]</c> from <c>[this+0x48]</c>).</summary>
    public int DeviceId { get; set; }

    /// <summary><c>+0x1C</c>.</summary>
    public uint Word1C { get; set; }

    /// <summary>The <c>memclr(fd, 0x20)</c> of 0x8D6CD0 / 0x8D6DD0.</summary>
    public void Clear()
    {
        FileSize = 0; Word08 = 0; Word0C = 0; Tag = 0; Handle = null; DeviceId = 0; Word1C = 0;
    }
}

/// <summary>
/// The transfer descriptor <c>AkIoTransferInfo</c> the device's I/O pass hands the hook (0x8D6E88): <c>+0</c> the file position (the low word is the seek offset), <c>+8</c> the buffer size, <c>+0xC</c>
/// the requested size, <c>+0x10</c> the buffer (<c>pUserData</c>, 0x966B3C..: <c>info[0x10]</c>).
/// </summary>
public sealed class WwiseTransferInfo
{
    /// <summary><c>+0</c>.</summary>
    public ulong FilePosition { get; set; }

    /// <summary><c>+8</c>.</summary>
    public uint BufferSize { get; set; }

    /// <summary><c>+0xC</c>.</summary>
    public uint RequestedSize { get; set; }

    /// <summary><c>+0x10</c>.</summary>
    public byte[] Buffer { get; set; } = Array.Empty<byte>();
}

/// <summary>The resolver as the stream manager sees it: vtable <c>0x1038518</c> slots +8 and +0xC (CreateAuto calls <c>vt+0xC</c> by id, 0x95F3DC, and <c>vt+8</c> by name, 0x95F198).</summary>
public interface IWwiseFileLocationResolver
{
    /// <summary>vt+8: <c>0x8D6CC7(this, name, mode, flags, &amp;bSyncOpen, fileDesc)</c>.</summary>
    int OpenByName(string name, uint mode, WwiseFileFlags? flags, ref bool bSyncOpen, WwiseFileDesc fileDesc);

    /// <summary>vt+0xC: <c>0x8D6DBD(this, id, mode, flags, &amp;bSyncOpen, fileDesc)</c>.</summary>
    int OpenById(uint id, uint mode, WwiseFileFlags? flags, ref bool bSyncOpen, WwiseFileDesc fileDesc);
}

/// <summary>The low-level I/O hook the device calls (vtable <c>0x1038548</c>: +8 Close, +0xC GetBlockSize, +0x18 Read).</summary>
public interface IWwiseLowLevelIoHook
{
    /// <summary>hook vt+0xC (<c>0x8D6F9B</c>): the block size, 1.</summary>
    uint GetBlockSize(WwiseFileDesc fileDesc);

    /// <summary>hook vt+0x18 (<c>0x8D6F0D</c> -&gt; <c>0x8D6E88</c>): 1 on success, 2 on failure.</summary>
    int Read(WwiseFileDesc fileDesc, WwiseTransferInfo transfer);

    /// <summary>hook vt+8 (<c>0x8D6F93</c> -&gt; <c>0x8D6F46</c>): 1 on success, 2 on failure.</summary>
    int Close(WwiseFileDesc fileDesc);
}

/// <summary>
/// The zip index of one registered archive (<c>0x8DDE66</c> init, <c>0x8DDECC</c> open, <c>0x8DE1D0</c> parse, <c>0x8DE39A</c> lookup, <c>0x8DE654</c> read, <c>0x8DE3BC</c> pread loop). Layout:
/// <c>[0]</c> fd (-1 = none), <c>[4]</c> the base offset of the (nested) archive, <c>[8]</c> its size, <c>[0xC]</c> the name map.
/// </summary>
public sealed class WwiseZipIndex
{
    private readonly IWwiseHostFileSystem _fs;
    private readonly Dictionary<string, (uint Offset, uint Size)> _map = new(StringComparer.Ordinal);

    /// <summary><c>[0]</c>: the descriptor, null for -1.</summary>
    public IWwiseHostFile? Fd { get; private set; }

    /// <summary><c>[4]</c>: the base offset (the offset of the nested archive inside the outer file).</summary>
    public uint Base { get; private set; }

    /// <summary><c>[8]</c>: the size (the low word of <c>st_size</c>, or the nested entry's size).</summary>
    public uint Size { get; private set; }

    /// <summary>The entries as <c>{dataOffset, size}</c> (the map nodes' <c>[+0x14]</c> and <c>[+0x18]</c>).</summary>
    public int EntryCount => _map.Count;

    /// <summary>The map's keys (names as the engine stores them: one char per byte).</summary>
    public IEnumerable<string> Names => _map.Keys;

    /// <summary><c>0x8DDE66</c>: <c>[0] = -1</c>, <c>[4..0x1B] = 0</c>.</summary>
    public WwiseZipIndex(IWwiseHostFileSystem fs) => _fs = fs ?? throw new ArgumentNullException(nameof(fs));

    /// <summary>
    /// <c>0x8DDEA6</c>: <c>[4] = [8] = 0</c>; a valid descriptor is closed and set to -1; the map is cleared (<c>0x8DE714</c>).
    /// </summary>
    public void Close()
    {
        Base = 0; Size = 0;                                                    // 0x8DDEAE strd r1,r1,[r4,#4]
        if (Fd is not null)                                                    // 0x8DDEB2 cmp r0,#0; blt
        {
            Fd.Close();                                                        // 0x8DDEB6 blx close
            Fd = null;                                                         // 0x8DDEBE str -1
        }
        _map.Clear();                                                          // 0x8DDEC4 b.w 0x8DE714
    }

    /// <summary>
    /// <c>0x8DDECC(index, path)</c>. The path is split at <c>'?'</c> with <c>std::getline</c> (<c>0x8DDFEC</c>; the loop stops on fail or bad, so a final empty token is not produced). <c>stat(parts[0])</c> failing, or
    /// <c>open(parts[0], O_RDONLY)</c> failing, returns 0. Otherwise <c>[0] = fd</c>, <c>[4] = 0</c>, <c>[8] = st_size</c> (32 bit) and the archive is parsed (<c>0x8DE1D0</c>); a failed parse closes it. With two or more parts
    /// the second names an entry of the parsed archive (<c>0x8DEA20</c>): a miss closes and returns 0, a hit adds its offset to <c>[4]</c>, sets <c>[8]</c> to its size, clears the map and parses again (the nested archive;
    /// a failure closes). Parts beyond the second are ignored.
    /// </summary>
    public bool Open(string path)
    {
        Close();                                                               // 0x8DDED4 bl 0x8DDEA6
        var parts = SplitGetline(path);                                        // 0x8DDEDC bl 0x8DDFEC
        if (parts.Count == 0)
            throw new InvalidOperationException("M6-024 C1: 0x8DDEEA ldrb r1,[r0] reads the first token of an empty vector (undefined in the engine)");
        string first = parts[0];
        if (!_fs.TryStat(first, out var st)) return false;                     // 0x8DDF24 stat; 0x8DDF28 cbz r0 (non-zero -> return 0)
        var fd = _fs.OpenReadOnly(first);                                      // 0x8DDF54..0x8DDF5E open; blt -> 0
        if (fd is null) return false;
        Fd = fd; Base = 0; Size = unchecked((uint)st.Size);                    // 0x8DDF60..0x8DDF66: [0] = fd, [4] = 0, [8] = st_size low word
        bool ok = Parse();                                                     // 0x8DDF6A bl 0x8DE1D0
        if (!ok) Close();                                                      // 0x8DDF70..0x8DDF74
        if (parts.Count < 2) return ok;                                        // 0x8DDF8A cmp r1,#2; blo 0x8DDF2C (returns r5)
        if (!_map.TryGetValue(parts[1], out var entry))                        // 0x8DDF98 bl 0x8DEA20
        {
            Close();                                                           // 0x8DDFBC
            return false;                                                      // 0x8DDF2A
        }
        Base = unchecked(Base + entry.Offset);                                 // 0x8DDF9E..0x8DDFA6 [4] += off
        Size = entry.Size;                                                     // [8] = size
        _map.Clear();                                                          // 0x8DDFAC bl 0x8DE714
        if (Parse()) return true;                                              // 0x8DDFB2 bl 0x8DE1D0; cbz -> 0x8DDFBC
        Close();
        return false;
    }

    /// <summary>
    /// <c>std::getline(ss, tok, '?')</c> in a loop that stops when the stream's state has bit 0 or bit 2 set (<c>0x8DE0D4 tst r0,#5</c>): <c>"a?b"</c> gives <c>a</c>, <c>b</c>; <c>"a?"</c> gives <c>a</c>; <c>"a??b"</c> gives
    /// <c>a</c>, an empty token, <c>b</c>. A final token that ends at the end of the input is stored (only a getline that extracted nothing fails).
    /// </summary>
    internal static List<string> SplitGetline(string s)
    {
        var parts = new List<string>();
        int pos = 0;
        while (pos < s.Length)                                                 // getline extracts at least one character (a delimiter counts) or fails
        {
            int q = s.IndexOf('?', pos);
            if (q < 0) { parts.Add(s.Substring(pos)); break; }
            parts.Add(s.Substring(pos, q - pos));
            pos = q + 1;
        }
        return parts;
    }

    /// <summary>
    /// <c>0x8DE3BC(index, buf, offset, n)</c>: false when the descriptor is invalid (<c>0x8DE3CA blt 0x8DE404</c>); true at once for <c>n == 0</c> (<c>0x8DE3CC cbz</c>); otherwise a <c>pread</c> loop at <c>[4] + offset</c> (32 bit):
    /// a positive count advances, <b>0 returns false</b> (<c>0x8DE3EC cbnz; b 0x8DE404</c>), -1 with <c>errno == 4</c> retries, and <b>any other -1 returns true</b> (<c>0x8DE3F8 bne 0x8DE400</c> lands on
    /// <c>movs r0,#1</c>) with the buffer untouched. (The research text says "other errors fail"; the instruction at 0x8DE400 is the success return.)
    /// </summary>
    public bool ReadAt(byte[] buffer, int index, uint offset, uint n)
    {
        if (Fd is null) return false;                                          // 0x8DE3C8 cmp r0,#0; 0x8DE3CA blt
        if (n == 0) return true;                                               // 0x8DE3CC cbz r4
        uint pos = unchecked(Base + offset);                                   // 0x8DE3D0 adds r7,r1,r2
        uint remaining = n;
        while (true)
        {
            int ret = Fd.Pread(buffer, index, (int)remaining, pos, out int errno);   // 0x8DE3E0 blx pread
            if (ret <= -1)                                                     // 0x8DE3EA ble 0x8DE3F0
            {
                if (errno != 4) return true;                                   // 0x8DE3F6..0x8DE3F8 bne 0x8DE400
                ret = 0;                                                       // 0x8DE3FA movs r1,#0 (EINTR: retry the same range)
            }
            else if (ret == 0) return false;                                   // 0x8DE3EC cbnz r1; 0x8DE3EE b 0x8DE404
            remaining -= (uint)ret;                                            // 0x8DE3FC subs r4,r4,r1
            if (remaining == 0) return true;                                   // 0x8DE3FE bne 0x8DE3D4
            index += ret; pos = unchecked(pos + (uint)ret);                    // 0x8DE3D4 add r5,r1; 0x8DE3D8 add r7,r1
        }
    }

    /// <summary>
    /// <c>0x8DE1D0</c>. Needs a non-zero size (<c>[8]</c>), a valid descriptor and at least 0x16 bytes. Reads the last 0x16 bytes, then scans backward one byte at a time, at most
    /// <c>min(size - 0x16, 0x400)</c> more reads, for <c>0x06054B50</c>; the comment length is not checked. The entry count is the u16 at <c>EOCD+0xA</c>, the directory offset the u32 at <c>EOCD+0x10</c>; a count of 0 succeeds.
    /// Each entry reads 0x2E bytes and needs <c>0x02014B50</c>; only the uncompressed size (+0x18), name length (+0x1C), extra length (+0x1E), comment length (+0x20) and local offset (+0x2A) are used. The compression method,
    /// the compressed size and the CRC are never read. A zero size skips the entry; otherwise the name is read, 0x1E bytes at the local offset need <c>0x04034B50</c>, and
    /// <c>map[name] = {localOff + 0x1E + u16[lh+0x1A] + u16[lh+0x1C], size}</c> (a later duplicate overwrites). A failed read or signature fails the whole index.
    /// </summary>
    private bool Parse()
    {
        uint size = Size;
        if (size == 0 || Fd is null || size < 0x16) return false;              // 0x8DE1D8..0x8DE1E4
        var eocd = new byte[0x16];                                             // 0x8DE1EA blx memclr
        uint tail = size - 0x16;                                               // 0x8DE1F0 subs r5,#0x16
        bool ok = ReadAt(eocd, 0, tail, 0x16);                                 // 0x8DE1FA bl 0x8DE3BC
        uint limit = tail >= 0x400 ? 0x400 : tail;                             // 0x8DE1FE..0x8DE204 (unsigned)
        if (!ok) return false;                                                 // 0x8DE208 cmp r0,#1; bne 0x8DE23E
        uint steps = 0;
        int back = -0x17;                                                      // 0x8DE216 mvn r6,#0x16
        while (BitConverter.ToUInt32(eocd, 0) != 0x06054B50)                   // 0x8DE21E..0x8DE222
        {
            if (steps >= limit) return false;                                  // 0x8DE224 cmp r7,r5; bhs
            ok = ReadAt(eocd, 0, unchecked((uint)((int)Size + back)), 0x16);   // 0x8DE228..0x8DE232
            back--; steps++;                                                   // 0x8DE236, 0x8DE238
            if (!ok) return false;                                             // 0x8DE23C cmp r0,#0; bne
        }
        uint count = BitConverter.ToUInt16(eocd, 0xA);                         // 0x8DE246 ldrh r1,[sp,#0x82]
        if (count == 0) return true;                                           // 0x8DE250 beq 0x8DE36A
        uint dir = BitConverter.ToUInt32(eocd, 0x10);                          // 0x8DE254 ldr r5,[sp,#0x88]
        var cen = new byte[0x2E];
        var lh = new byte[0x1E];
        uint idx = 0;                                                          // fp
        while (true)
        {
            if (!ReadAt(cen, 0, dir, 0x2E) || BitConverter.ToUInt32(cen, 0) != 0x02014B50) return false;   // 0x8DE274..0x8DE280
            uint uncompressed = BitConverter.ToUInt32(cen, 0x18);              // 0x8DE282 ldr r6,[sp,#0x60]
            uint nameLen = BitConverter.ToUInt16(cen, 0x1C);                   // 0x8DE28C ldrh r5,[sp,#0x64]
            uint nameOff = dir + 0x2E;                                         // 0x8DE284 add sb,r5,#0x2e
            if (uncompressed != 0)                                             // 0x8DE288 cmp r6,#0; beq 0x8DE310
            {
                var name = new byte[nameLen];                                  // 0x8DE29A bl 0x8DE40A (a vector of nameLen zero bytes)
                if (!ReadAt(name, 0, nameOff, nameLen)) return false;          // 0x8DE2AA..0x8DE2AE cbz -> fail
                uint localOff = BitConverter.ToUInt32(cen, 0x2A);              // 0x8DE2C2 ldr r8,[sp,#0x72]
                if (!ReadAt(lh, 0, localOff, 0x1E) || BitConverter.ToUInt32(lh, 0) != 0x04034B50) return false;   // 0x8DE2D0..0x8DE2EA
                uint dataOff = unchecked(localOff + BitConverter.ToUInt16(lh, 0x1A) + BitConverter.ToUInt16(lh, 0x1C) + 0x1E);   // 0x8DE2EC..0x8DE304
                _map[Latin1(name)] = (dataOff, uncompressed);                  // 0x8DE2FA bl 0x8DE454 (operator[]); 0x8DE306 strd r1,r6,[r0]
            }
            dir = unchecked(nameOff + nameLen + BitConverter.ToUInt16(cen, 0x1E) + BitConverter.ToUInt16(cen, 0x20));   // 0x8DE348..0x8DE35A
            idx++;
            if ((ushort)idx >= (ushort)count) return true;                     // 0x8DE35C uxth fp; 0x8DE362 cmp r1,r2; blo 0x8DE26C
        }
    }

    /// <summary>The engine's <c>std::string</c> holds bytes; one char per byte keeps the compare byte-exact (<c>0x8DEA9A..0x8DEACA</c>).</summary>
    internal static string Latin1(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length);
        foreach (byte b in bytes) sb.Append((char)b);
        return sb.ToString();
    }

    /// <summary><c>0x8DE39A(index, key, &amp;off, &amp;size)</c>: both outputs are zeroed first; a hit (<c>0x8DEA20</c>, case sensitive) returns the entry's <c>{dataOffset, size}</c>.</summary>
    public bool Lookup(string key, out uint offset, out uint size)
    {
        offset = 0; size = 0;                                                  // 0x8DE3A4, 0x8DE3A6
        if (!_map.TryGetValue(key, out var e)) return false;                   // 0x8DE3A8 bl 0x8DEA20; cbz
        offset = e.Offset; size = e.Size;                                      // 0x8DE3AE ldrd r1,r0,[r0,#0x14]
        return true;
    }

    /// <summary>
    /// <c>0x8DE654(index, entryOffset, pos, size, buf)</c>: when <c>entryOffset + pos + size</c> (32 bit) is at most <c>[8]</c> (the archive's size, unsigned <c>ls</c>) the range is read with <see cref="ReadAt"/> at
    /// <c>entryOffset + pos</c>; otherwise 0. The bound is the archive's, not the entry's.
    /// </summary>
    public bool ReadEntry(uint entryOffset, uint pos, uint size, byte[] buffer, int index)
    {
        uint start = unchecked(entryOffset + pos);                             // 0x8DE654 add r2,r1
        uint end = unchecked(start + size);                                    // 0x8DE65A adds r1,r2,r3
        if (end > Size) return false;                                          // 0x8DE65C cmp r1,ip; itt ls
        return ReadAt(buffer, index, start, size);                             // 0x8DE662 b.w 0x8DE3BC
    }
}

/// <summary>One registered archive (<c>0x2C</c> bytes at <c>0x8D843C</c>): the path string at <c>+0</c> and the index at <c>+0xC</c>.</summary>
public sealed class WwiseZipArchive
{
    /// <summary>The registration string, <c>"path"</c> or <c>"path?inner"</c>.</summary>
    public string Path { get; }

    /// <summary>The index.</summary>
    public WwiseZipIndex Index { get; }

    internal WwiseZipArchive(string path, WwiseZipIndex index) { Path = path; Index = index; }
}

/// <summary>The language state of the stream manager's globals (<c>0x108D798+0x14..0x20</c>): the name, and the callbacks <c>0x960744</c> calls.</summary>
public sealed class WwiseLanguageState
{
    /// <summary><c>0x108D7B8</c> (0x20 bytes, zero before the first <c>SetCurrentLanguage</c>).</summary>
    public string Current { get; private set; } = "";

    /// <summary>The registered callbacks (<c>[G+0x14]</c> vector of <c>{fn, cookie}</c>); <c>0x960744</c> calls them from the last to the first.</summary>
    public List<Action<string>> Callbacks { get; } = new();

    /// <summary>
    /// <c>0x960744(name)</c>: a null name or a length above 0x1F returns 2; an empty name stores the empty string; a last character of <c>'/'</c> (0x2F) or <c>'\\'</c> (0x5C) returns 2; otherwise the name is
    /// copied (at most 0x1F characters plus the terminator) and the callbacks run, last first. Returns 1.
    /// </summary>
    public int Set(string? name)
    {
        if (name is null) return 2;                                            // 0x960748 subs r3,r0,#0; beq
        if (name.Length > 0x1F) return 2;                                      // 0x960758 cmp r0,#0x1f; bls
        if (name.Length != 0)
        {
            char last = name[name.Length - 1];                                 // 0x9607E0..0x9607E4
            if (last == '/' || last == '\\') return 2;                         // 0x9607E8..0x9607F0
        }
        Current = name;                                                        // 0x960774..0x9607A4 (the copy of len+1 bytes, or 0x1F when len+1 == 0x20)
        for (int i = Callbacks.Count - 1; i >= 0; i--) Callbacks[i](Current);  // 0x9607B0..0x9607D0
        return 1;
    }

    /// <summary><c>0x8D85FC(byte)</c> (the static map of four names; any other value gives "English(US)", 0x8D86F0..0x8D86FA).</summary>
    public static string NameForByte(byte language) => language switch
    {
        0 => "English(US)",
        1 => "German",
        2 => "French(France)",
        3 => "Japanese",
        _ => "English(US)",
    };
}

/// <summary>The inputs of <c>0x8D8228(this, cfg)</c> (the part of the SetupConfig the post-init reads).</summary>
public sealed class WwiseAnkiResolverConfig
{
    /// <summary><c>cfg[0]</c>: the sound directory, <c>pathToResource(scope, "sound/")</c> (0x592C3A..0x592C52, 0x592CD2).</summary>
    public string SoundDirectory { get; init; } = "";

    /// <summary>
    /// <c>cfg+0x10</c>: the path mapper, a <c>std::function&lt;bool(const string&amp;, string&amp;)&gt;</c> bound to <c>0x593BD8</c> with the sound directory (<see cref="MapSoundPath"/>). Null is the empty function
    /// (<c>0x8D8250..0x8D8262</c> copies it as it is; a call through it throws <c>bad_function_call</c>).
    /// </summary>
    public Func<string, (bool Ok, string Mapped)>? PathMapper { get; init; }

    /// <summary><c>cfg+0x28</c>: the zip registration strings in vector order (the OBB first, then the APK, 0x592FDA, 0x5932AA).</summary>
    public IReadOnlyList<string> ZipPaths { get; init; } = Array.Empty<string>();

    /// <summary><c>cfg[0x40]</c>: the AAssetManager; never written by the shipped constructor, so null (A9).</summary>
    public IWwiseHostAssetManager? AssetManager { get; init; }

    /// <summary><c>cfg+0x44</c>: the asset base string (used only with an asset manager).</summary>
    public string AssetBase { get; init; } = "";

    /// <summary><c>cfg byte 0x72</c>: the language selector (<see cref="WwiseLanguageState.NameForByte"/>).</summary>
    public byte Language { get; init; }

    /// <summary>
    /// <c>0x593BD8(bound, name, out)</c>: <c>out</c> is cleared; an empty bound string returns false; otherwise <c>out = bound + name</c> and the result is <c>out.size != 0</c> (0x593BF6..0x593C46). The shipped
    /// constructor binds the non-empty sound directory (verifier correction 1 to A5/B7: 0x592CE2..0x592CF2), so the loose-file branch of the resolver is live.
    /// </summary>
    public static Func<string, (bool Ok, string Mapped)> MapSoundPath(string bound) => name =>
    {
        if (bound.Length == 0) return (false, "");                             // 0x593BF6..0x593C02 cbz r0 -> 0x593C18
        string mapped = bound + name;                                          // 0x593C08 blx operator+
        return (mapped.Length != 0, mapped);                                   // 0x593C34..0x593C46
    };

    /// <summary>The APK / OBB registration string: <c>path + "?" + "assets/cozmo_resources/sound/AudioAssets.zip"</c> (0x592FDA, 0x5932AA).</summary>
    public static string NestedZip(string archivePath) => archivePath + "?assets/cozmo_resources/sound/AudioAssets.zip";
}

/// <summary>
/// The Anki resolver object (<c>0x8D6B6C</c> ctor, vtable <c>0x1038518</c>) with its package struct P and the zip registry. <see cref="DeviceId"/> is <c>[this+0x48]</c> and <see cref="DeferOpen"/> is the byte
/// <c>[this+0x4C]</c>. The object is also the I/O hook (the subobject at <c>this+4</c>, vtable <c>0x1038548</c>).
/// </summary>
public sealed class WwiseAnkiResolver : IWwiseFileLocationResolver, IWwiseLowLevelIoHook
{
    private readonly IWwiseHostFileSystem _fs;
    private readonly WwiseStreamManager _manager;
    private readonly object _packageLock = new();                              // [P+0x2C]
    private readonly List<string> _searchPaths = new();                        // [P]: nodes {next, path}; the newest is the head
    private readonly List<WwiseZipArchive> _zips = new();                      // [P+0x20..0x24]
    private Func<string, (bool Ok, string Mapped)>? _pathMapper;              // [P+0x18]
    private IWwiseHostAssetManager? _assetManager;                             // [P+0x30]
    private string _assetBase = "";                                            // [P+0x34]

    /// <summary><c>[this+0x48]</c>: the device id (initially -1).</summary>
    public int DeviceId { get; private set; } = -1;

    /// <summary><c>[this+0x4C]</c> (byte): the deferred-open flag (initially 0; <c>0x8D81F0</c> passes 1).</summary>
    public bool DeferOpen { get; private set; }

    /// <summary>The registered archives in search order (the first is searched first).</summary>
    public IReadOnlyList<WwiseZipArchive> Zips => _zips;

    /// <summary>The search-path list in search order (newest first).</summary>
    public IReadOnlyList<string> SearchPaths => _searchPaths;

    /// <summary>
    /// The result of <c>0x8DA938(this, missingPath)</c> for a registered archive whose <c>stat</c> fails or is not a regular file (0x8D834C). The body is unread (C33 still-open list), so it has no default: the
    /// callback is required and an unset one throws. It is never reached while no archive is registered (the call at <c>0x8D8280</c> in the initialisation runs before any registration).
    /// </summary>
    public Action<string>? MissingArchiveHandler8DA938 { get; set; }

    /// <summary><c>0x8D6B6C</c>: the vtable words, <c>[0x48] = -1</c> and <c>[0x4C] = 0</c>.</summary>
    public WwiseAnkiResolver(WwiseStreamManager manager, IWwiseHostFileSystem fs)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
    }

    // ---------------------------------------------------------------- B1 and the post-init

    /// <summary>
    /// B1 <c>0x8D6C02(this, settings, flag)</c>: <c>settings[0x14] != 1</c> returns 2; <c>[this+0x4C] = flag</c>; when no resolver is set (<c>0x9606B4</c>) this becomes it (<c>0x9606C4</c>); the device is created
    /// (<c>0x961168</c> -&gt; <c>0x960F34</c>) with this object as the hook and its id stored at <c>[this+0x48]</c>; -1 returns 2, otherwise 1.
    /// </summary>
    public int Init(WwiseDeviceSettings settings, bool deferOpen)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchedulerFlags14 != 1) return 2;                          // 0x8D6C08..0x8D6C10
        DeferOpen = deferOpen;                                                 // 0x8D6C12 strb.w r2,[r4,#0x4c]
        if (_manager.Resolver is null) _manager.Resolver = this;               // 0x8D6C16 blx 0x9606B4; cbnz; 0x8D6C1E blx 0x9606C4
        int id = _manager.CreateDevice(settings, this);                        // 0x8D6C26 blx 0x961168
        DeviceId = id;                                                         // 0x8D6C2A str r0,[r4,#0x48]
        return id == -1 ? 2 : 1;                                               // 0x8D6C2C..0x8D6C34
    }

    /// <summary>
    /// <c>0x8D8228(this, cfg)</c> (A9): <c>AddSearchPath(cfg[0])</c> (result ignored); the path mapper is copied to <c>[Y+0x10]</c>; <c>AddZipFiles(cfg+0x28)</c> (<c>bl 0x8D8320</c> at <c>0x8D8280</c>); a non-null
    /// asset manager and its base string are stored; <c>SetCurrentLanguage(map(cfg byte 0x72))</c> (its result ignored). Returns 1.
    /// </summary>
    public int PostInit(WwiseAnkiResolverConfig cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        AddSearchPath(cfg.SoundDirectory);                                     // 0x8D824A bl 0x8DADAA
        _pathMapper = cfg.PathMapper;                                          // 0x8D8250..0x8D8262 (0x8D85D2 copies the std::function; 0x8DAE44 stores it at [Y+0x10])
        AddZipFiles(cfg.ZipPaths);                                             // 0x8D8280 bl 0x8D8320
        if (cfg.AssetManager is not null)                                      // 0x8D8284 ldr r0,[r4,#0x40]; cbz
        {
            _assetManager = cfg.AssetManager;                                  // 0x8D828A str r0,[r1,#0x38]
            _assetBase = cfg.AssetBase;                                        // 0x8D8294 bl 0x4E462A (string assign)
        }
        _manager.Language.Set(WwiseLanguageState.NameForByte(cfg.Language));   // 0x8D829C..0x8D82B4
        return 1;
    }

    /// <summary>
    /// <c>0x8DADAA(P, path)</c>: <c>strlen(path) + strlen(language) + 1 &gt;= 512</c> returns 0x1F (<c>lsr #9</c>); the node is pushed at the head of the list even when the <c>stat</c> then fails; the result is 1 for a directory
    /// (<c>st_mode</c> bit 14) and 0x24 otherwise.
    /// </summary>
    public int AddSearchPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (((uint)(path.Length + _manager.Language.Current.Length + 1) >> 9) != 0) return 0x1F;   // 0x8DADB6..0x8DADD0
        lock (_packageLock) _searchPaths.Insert(0, path);                      // 0x8DAE04..0x8DAE1A (the head)
        if (!_fs.TryStat(path, out var st) || !st.IsDirectory) return 0x24;    // 0x8DAE2A..0x8DAE38
        return 1;                                                              // 0x8DAE3C
    }

    /// <summary>
    /// <c>0x8D8320(this, vector)</c> (<c>AddZipFiles</c>, whose exported entry <c>0x8D1E3E</c> has no caller): <c>0x8D834C</c> first walks the zips registered so far (<c>stat</c> of each first token; a failure or a
    /// non-regular file puts it in a missing list handed to <c>0x8DA938</c>, unread); then each string is registered with <c>0x8D843C(path, 0)</c>, results ignored. The order after the call is the reverse of the
    /// vector: each string is inserted at the front.
    /// </summary>
    public void AddZipFiles(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        CheckRegisteredZips();                                                 // 0x8D832A bl 0x8D834C
        foreach (var p in paths) RegisterZip(p, atEnd: false);                 // 0x8D8338..0x8D8348
    }

    private void CheckRegisteredZips()
    {
        List<string> missing = new();
        lock (_packageLock)                                                    // 0x8D8362 blx pthread_mutex_lock([P+0x2C])
        {
            foreach (var z in _zips)                                           // 0x8D8366..0x8D83EE
            {
                var parts = WwiseZipIndex.SplitGetline(z.Path);                // 0x8D837E bl 0x8DDFEC
                if (parts.Count == 0) continue;                                // 0x8D8388 beq 0x8D83E4
                if (!_fs.TryStat(parts[0], out var st) || !st.IsRegular) missing.Add(z.Path);   // 0x8D83A0..0x8D83AC (stat != 0, or bit 15 clear)
            }
        }
        foreach (var m in missing)                                             // 0x8D83F6..0x8D840A
            (MissingArchiveHandler8DA938 ?? throw new WwiseMissingBehaviourException(
                $"M6-024 A10: 0x8DA938 (the handling of a registered archive that is missing, \"{m}\") is unread (C33 still-open list)")).Invoke(m);
    }

    /// <summary>
    /// <c>0x8D843C(P, path, flag)</c> (A11): under <c>[P+0x2C]</c>; an empty path returns 0x1F; an already registered path (equal bytes) returns 0x28; otherwise a 0x2C-byte object is created, the path copied and the
    /// object inserted at the front (<paramref name="atEnd"/> false, <c>0x8DAA34</c>) or appended (true), the index opened (<c>0x8DDECC(obj+0xC, path)</c>); a failed open removes the object again and returns 2; success returns 1.
    /// </summary>
    public int RegisterZip(string path, bool atEnd)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_packageLock)
        {
            if (path.Length == 0) return 0x1F;                                 // 0x8D8462 cbz r4 -> 0x8D84D0
            foreach (var z in _zips)                                           // 0x8D846A..0x8D84C4
                if (string.Equals(z.Path, path, StringComparison.Ordinal)) return 0x28;   // 0x8D84CA movs r5,#0x28
            var index = new WwiseZipIndex(_fs);                                // 0x8D84D6..0x8D84E8 (alloc, memclr, 0x8DDE66)
            var archive = new WwiseZipArchive(path, index);                    // 0x8D84F2 copy of the path
            if (atEnd) _zips.Add(archive); else _zips.Insert(0, archive);      // 0x8D84F8..0x8D851C
            if (index.Open(path)) return 1;                                    // 0x8D852C bl 0x8DDECC
            if (atEnd) _zips.RemoveAt(_zips.Count - 1);                        // 0x8D8536..0x8D853C
            else _zips.RemoveAt(0);                                            // 0x8D8540..0x8D8572 (erase element 0)
            index.Close();                                                     // 0x8D857E bl 0x8DDE82 (the destructor)
            return 2;                                                          // 0x8D8594 movs r5,#2
        }
    }

    // ---------------------------------------------------------------- B2 to B7

    /// <summary>
    /// vt+8, B4 <c>0x8D6CC6</c>: <c>fileDesc</c> is cleared and its <c>+0x18</c> set to the device id; a zero <c>bSyncOpen</c> with the deferral flag set returns 1 at once (the descriptor is otherwise zero and
    /// <c>bSyncOpen</c> stays 0); otherwise <c>bSyncOpen</c> becomes 1 and <c>0x8D6D18(P, name, mode, flags, 0, fileDesc)</c> runs.
    /// </summary>
    public int OpenByName(string name, uint mode, WwiseFileFlags? flags, ref bool bSyncOpen, WwiseFileDesc fileDesc)
    {
        fileDesc.Clear();                                                      // 0x8D6CDA blx memclr(fd, 0x20)
        fileDesc.DeviceId = DeviceId;                                          // 0x8D6CE2 str r1,[r4,#0x18]
        if (!bSyncOpen && DeferOpen) return 1;                                 // 0x8D6CE4..0x8D6D0E
        bSyncOpen = true;                                                      // 0x8D6CEC strb r1,[r0]
        return SearchA8D6D18(name, mode, flags, fileDesc);                     // 0x8D6CFC bl 0x8D6D18
    }

    /// <summary>vt+0xC, B2 <c>0x8D6DBC</c>: as <see cref="OpenByName"/>, through B3 (<c>0x8D6E0C</c>).</summary>
    public int OpenById(uint id, uint mode, WwiseFileFlags? flags, ref bool bSyncOpen, WwiseFileDesc fileDesc)
    {
        fileDesc.Clear();                                                      // 0x8D6DD0
        fileDesc.DeviceId = DeviceId;                                          // 0x8D6DD8
        if (!bSyncOpen && DeferOpen) return 1;                                 // 0x8D6DDA..0x8D6E06
        bSyncOpen = true;                                                      // 0x8D6DE2
        return IdToNameA8D6E0C(id, mode, flags, fileDesc);                     // 0x8D6DF2 bl 0x8D6E0C
    }

    /// <summary>
    /// B3 <c>0x8D6E0C</c>: null flags return 2; <c>flags[0] &gt; 1</c> (unsigned) returns 2; the name is <c>"%u.wem"</c> when <c>flags[4] != 0</c> and <c>"%u.bnk"</c> otherwise (<c>snprintf</c> into 15 bytes, which holds any
    /// 32-bit id), then B5.
    /// </summary>
    internal int IdToNameA8D6E0C(uint id, uint mode, WwiseFileFlags? flags, WwiseFileDesc fileDesc)
    {
        if (flags is null) return 2;                                           // 0x8D6E1E cmp r4,#0; beq 0x8D6E56
        if (flags.CompanyId > 1) return 2;                                     // 0x8D6E28..0x8D6E2C
        string name = id.ToString(System.Globalization.CultureInfo.InvariantCulture) + (flags.CodecId != 0 ? ".wem" : ".bnk");   // 0x8D6E34..0x8D6E40
        return SearchA8D6D18(name, mode, flags, fileDesc);                     // 0x8D6E50 bl 0x8D6D18
    }

    /// <summary>
    /// B5 <c>0x8D6D18</c>: for each search-path node (newest first) the candidate path <c>0x8D6FD6</c> is built and, when it returns 1, opened with <c>0x8D713C</c>; a 1 returns 1. After the list the zip
    /// archives are searched (<c>0x8D718C</c>) and then the asset manager (<c>0x8D7288</c>); a miss returns 2 (not 0x42).
    /// </summary>
    internal int SearchA8D6D18(string name, uint mode, WwiseFileFlags? flags, WwiseFileDesc fileDesc)
    {
        List<string> nodes;
        lock (_packageLock) nodes = new List<string>(_searchPaths);            // 0x8D6D36 ldr r6,[r7]; 0x8D6D38 cbz
        foreach (var node in nodes)                                            // 0x8D6D40..0x8D6D68
        {
            int r = CandidatePathA8D6FD6(name, flags, mode, node, out string path);   // 0x8D6D4C bl 0x8D6FD6
            if (r != 1) continue;                                              // 0x8D6D50 cmp r0,#1; bne 0x8D6D64
            if (FopenA8D713C(path, mode, fileDesc) == 1) return 1;             // 0x8D6D5C bl 0x8D713C; 0x8D6D62 beq 0x8D6D7E
        }
        if (ZipLookupA8D718C(name, flags, mode, fileDesc) == 1) return 1;      // 0x8D6D76 bl 0x8D718C; 0x8D6D7C
        return AssetOpenA8D7288(name, flags, mode, fileDesc) == 1 ? 1 : 2;     // 0x8D6D8E bl 0x8D7288; 0x8D6D94 it ne; movne r0,#2
    }

    /// <summary>
    /// B6 <c>0x8D739C(P, name, flags, mode, &amp;out)</c>: <c>out</c> is cleared; a null name returns 0x1F; null flags or a non-zero mode return 2; a language-specific file (<c>flags byte +0x10</c>) is prefixed with
    /// <c>GetCurrentLanguage() + "/"</c>; a long string of 512 characters or more returns 0x1F (the length is tested only in the heap form, which is the only form a 512-character string takes); otherwise 1.
    /// </summary>
    internal int BuildNameA8D739C(string? name, WwiseFileFlags? flags, uint mode, out string built)
    {
        built = "";                                                            // 0x8D73B8..0x8D73D8
        if (name is null) return 0x1F;                                         // 0x8D73DC cmp.w r8,#0; beq 0x8D743E
        if (flags is null || mode != 0) return 2;                              // 0x8D73E6 cbz r6; 0x8D73E8 cbnz r7
        string s = "";
        if (flags.LanguageSpecific != 0)                                       // 0x8D73F2 ldrb r0,[r6,#0x10]; cbz
            s = _manager.Language.Current + "/";                               // 0x8D73F6 blx 0x960808; 0x8D7408; 0x8D7412
        s += name;                                                             // 0x8D7422
        if (((uint)s.Length >> 9) != 0) return 0x1F;                           // 0x8D7430..0x8D743A
        built = s;                                                             // 0x8D7446 bl 0x4E462A (assign)
        return 1;
    }

    /// <summary>
    /// B7 <c>0x8D6FD6(P, name, flags, mode, node, buf)</c>: <c>buf[0] = 0</c>; the name is built (B6; a failure returns its code). With the mapper function set (<c>[P+0x18] != 0</c>) it is called with the built name
    /// (<c>0x8D7480</c> throws <c>bad_function_call</c> when null); false returns 2, otherwise <c>strncpy(buf, out, 0x200)</c> and <c>stat(buf)</c>: success and a regular file (<c>st_mode</c> bit 15) return 1, else 2.
    /// Without the function the path is <c>node + name</c>; 512 characters or more (heap form only) return 2, otherwise it is appended with <c>strncat(buf, path, 0x1FF - strlen(buf))</c> and 1 is returned.
    /// </summary>
    internal int CandidatePathA8D6FD6(string name, WwiseFileFlags? flags, uint mode, string node, out string path)
    {
        path = "";                                                             // 0x8D6FE0..0x8D6FE2
        int r = BuildNameA8D739C(name, flags, mode, out string built);         // 0x8D6FF2 bl 0x8D739C
        if (r != 1) return r;                                                  // 0x8D6FF8 cmp r5,#1; bne 0x8D70E8
        if (_pathMapper is not null)                                           // 0x8D6FFC ldr r0,[r6,#0x18]; cbz
        {
            var (ok, mapped) = _pathMapper(built);                             // 0x8D7012 bl 0x8D7480
            if (!ok) return 2;                                                 // 0x8D7016 cbz r0 -> 0x8D704E
            path = mapped.Length > 0x200 ? mapped.Substring(0, 0x200) : mapped;   // 0x8D702E blx strncpy(buf, out, 0x200)
            if (_fs.TryStat(path, out var st) && st.IsRegular) return 1;       // 0x8D7040..0x8D704C
            return 2;                                                          // 0x8D704E movs r5,#2
        }
        string full = node + built;                                            // 0x8D705C..0x8D7090 (the node's string and the name)
        if (((uint)full.Length >> 9) != 0) return 2;                           // 0x8D709C..0x8D70A8
        path = full.Length > 0x1FF ? full.Substring(0, 0x1FF) : full;          // 0x8D70C4..0x8D70D4 strncat(buf, path, 0x1FF - strlen(buf)) onto an empty buf
        return 1;                                                              // 0x8D70DC
    }

    /// <summary>
    /// B8 <c>0x8D713C(path, mode, _, fileDesc)</c>: a null path returns 0x1F; a mode of 4 or more sets <c>fileDesc[0x14] = 0</c> and returns 0x1F; <c>fopen(path, table[mode])</c> with the table <c>"r" "w" "w+" "a"</c>
    /// (0x1038598) stores the handle first (a null one returns 0x42); <c>stat</c> failing returns 2 (the stream stays open); otherwise <c>iFileSize = st_size</c> and 1. The tag stays 0.
    /// </summary>
    internal int FopenA8D713C(string? path, uint mode, WwiseFileDesc fileDesc)
    {
        if (path is null) return 0x1F;                                         // 0x8D7144 cbz r5 -> 0x8D7170
        if (mode >= 4) { fileDesc.Handle = null; return 0x1F; }                // 0x8D7146..0x8D716E
        string m = mode switch { 0 => "r", 1 => "w", 2 => "w+", _ => "a" };    // 0x1038598 table
        var file = _fs.Fopen(path, m);                                         // 0x8D7154 blx fopen
        fileDesc.Handle = file;                                                // 0x8D715A str r0,[r4,#0x14]
        if (file is null) return 0x42;                                         // 0x8D715C beq 0x8D7176
        if (!_fs.TryStat(path, out var st)) return 2;                          // 0x8D7162..0x8D7168
        fileDesc.FileSize = st.Size;                                           // 0x8D717A ldrd r0,r1,[sp,#0x30]; strd r0,r1,[r4]
        return 1;
    }

    /// <summary>
    /// B9 <c>0x8D718C(P, name, flags, mode, fileDesc)</c>: the name is built (B6); no registered archive returns 2; the archives are searched in vector order (<c>0x8DE39A</c>); the first hit stores an 8-byte record
    /// <c>{index, entryOffset}</c> as the handle, <c>iFileSize = size</c>, <c>+8 = 0</c>, the tag "zip" and returns 1; none returns 0x42.
    /// </summary>
    internal int ZipLookupA8D718C(string name, WwiseFileFlags? flags, uint mode, WwiseFileDesc fileDesc)
    {
        int r = BuildNameA8D739C(name, flags, mode, out string key);           // 0x8D71A2 bl 0x8D739C
        if (r != 1) return r;                                                  // 0x8D71A8 cmp r5,#1; bne 0x8D7246
        List<WwiseZipArchive> zips;
        lock (_packageLock) zips = new List<WwiseZipArchive>(_zips);
        if (zips.Count == 0) return 2;                                         // 0x8D71B0 cmp r5,r6; beq 0x8D71CE
        foreach (var z in zips)                                                // 0x8D71F2..0x8D720C
        {
            if (!z.Index.Lookup(key, out uint off, out uint size)) continue;   // 0x8D7200 bl 0x8DE39A; cmp r0,#1
            fileDesc.Handle = new ZipHandle(z.Index, off);                     // 0x8D7212..0x8D721E: the 8-byte object {index = z+0xC, off}
            fileDesc.FileSize = size;                                          // 0x8D7222..0x8D7224 strd r1,r2,[sl] (high word 0)
            fileDesc.Word08 = 0;                                               // 0x8D7230 str.w r2,[sl,#8]
            fileDesc.Tag = WwiseFileDesc.TagZip;                               // 0x8D7234 strd r1,r0,[sl,#0x10]
            return 1;
        }
        return 0x42;                                                           // 0x8D720E movs r5,#0x42
    }

    /// <summary>
    /// B10 <c>0x8D7288</c>: the name is built (B6); no asset manager returns 2; the path is the asset base, <c>"/"</c> when the base is not empty, then the name; a failed open returns 0x42; otherwise the handle, the
    /// tag "asm", <c>iFileSize = AAsset_getLength</c> (sign-extended) and <c>+8 = 0</c>, and 1.
    /// </summary>
    internal int AssetOpenA8D7288(string name, WwiseFileFlags? flags, uint mode, WwiseFileDesc fileDesc)
    {
        int r = BuildNameA8D739C(name, flags, mode, out string built);         // 0x8D729C bl 0x8D739C
        if (r != 1) return r;                                                  // 0x8D72A2 cmp r5,#1; bne 0x8D7356
        if (_assetManager is null) return 2;                                   // 0x8D72A6 ldr r0,[r4,#0x30]; cbz r0 -> 0x8D72C8
        string path = _assetBase + (_assetBase.Length != 0 ? "/" : "") + built;   // 0x8D72D6..0x8D7308 (the "/" at 0x8D72E8 adr r1,#0xac, one character)
        var asset = _assetManager.Open(path);                                  // 0x8D7320 blx AAssetManager_open(mgr, path, 0)
        if (asset is null) return 0x42;                                        // 0x8D7324 cbz r0 -> 0x8D7346
        fileDesc.Handle = asset;                                               // 0x8D7328 str r0,[r4,#0x14]
        fileDesc.Tag = WwiseFileDesc.TagAsset;                                 // 0x8D733A str r2,[r4,#0x10]
        fileDesc.FileSize = asset.Length;                                      // 0x8D733C asrs r2,r0,#0x1f; strd r0,r2,[r4]
        fileDesc.Word08 = 0;                                                   // 0x8D7342 str r1,[r4,#8]
        return 1;
    }

    // ---------------------------------------------------------------- B11, B12 and the hook

    /// <summary>The zip handle (the 8-byte object of <c>0x8D7212</c>): the index and the entry's data offset.</summary>
    internal sealed record ZipHandle(WwiseZipIndex Index, uint EntryOffset);

    /// <summary>
    /// B11 <c>0x8D6E88(this, fileDesc, heur, buf, xfer)</c>. Tag 0 (a <c>FILE*</c>): <c>fseek(file, xfer[0], 0)</c>; when that is 0, <c>fread(buf, 1, xfer[0xC], file)</c> and any count above 0 returns 1 (a short read
    /// counts); a failed seek or a count of 0 falls to the tag test and returns 2. Tag "zip": <c>0x8DE654(index, entryOffset, xfer[0], xfer[0xC], buf)</c> non-zero returns 1, else 2. Tag "asm": <c>AAsset_seek</c> (-1
    /// returns 2) and <c>AAsset_read == xfer[0xC]</c> returns 1, else 2. Any other tag returns 2.
    /// </summary>
    public int Read(WwiseFileDesc fileDesc, WwiseTransferInfo transfer)
    {
        ArgumentNullException.ThrowIfNull(fileDesc);
        ArgumentNullException.ThrowIfNull(transfer);
        uint pos = unchecked((uint)transfer.FilePosition);                     // 0x8D6E96 ldr r1,[r7] (the low word)
        if (fileDesc.Tag == 0)                                                 // 0x8D6E90 ldr r0,[r5,#0x10]; cbnz
        {
            var file = fileDesc.Handle as IWwiseHostFile
                ?? throw new InvalidOperationException("M6-024 B11: a tag-0 descriptor without a FILE* (fread on a null FILE crashes in the engine)");
            if (file.Fseek(pos) == 0)                                          // 0x8D6E9C blx fseek; 0x8D6EA0 cbnz
            {
                int n = file.Fread(transfer.Buffer, 0, (int)transfer.RequestedSize);   // 0x8D6EAC blx fread(buf, 1, xfer[0xC], file)
                if (n != 0) return 1;                                          // 0x8D6EB0 cbnz r0 -> 0x8D6F06 (r6 = 1)
            }
        }
        if (fileDesc.Tag == WwiseFileDesc.TagAsset)                            // 0x8D6EB2..0x8D6EBE
        {
            var asset = (IWwiseHostAsset)fileDesc.Handle!;
            if (asset.Seek(pos) == -1) return 2;                               // 0x8D6EE8 blx AAsset_seek; adds r0,#1; beq 0x8D6F04
            return asset.Read(transfer.Buffer, 0, (int)transfer.RequestedSize) == (int)transfer.RequestedSize ? 1 : 2;   // 0x8D6EF6..0x8D6EFE
        }
        if (fileDesc.Tag == WwiseFileDesc.TagZip)                              // 0x8D6EC0..0x8D6ECA
        {
            var z = (ZipHandle)fileDesc.Handle!;                               // 0x8D6ECC ldr r1,[r5,#0x14]; ldrd r0,r1,[r1]
            return z.Index.ReadEntry(z.EntryOffset, pos, transfer.RequestedSize, transfer.Buffer, 0) ? 1 : 2;   // 0x8D6ED8 bl 0x8DE654; cbnz -> 1
        }
        return 2;                                                              // 0x8D6F04 movs r6,#2
    }

    /// <summary>
    /// B12 <c>0x8D6F46</c>: tag 0 closes the <c>FILE*</c> (<c>fclose == 0</c> returns 1, else 2); "zip" releases the 8-byte record, stores 0 at <c>+0x14</c> and returns 1; "asm" closes the asset and returns 2 (the
    /// <c>movs r0,#2</c> at 0x8D6F8E is reached by the fall-through); any other tag returns 2.
    /// </summary>
    public int Close(WwiseFileDesc fileDesc)
    {
        ArgumentNullException.ThrowIfNull(fileDesc);
        if (fileDesc.Tag == WwiseFileDesc.TagZip)                              // 0x8D6F4A..0x8D6F56
        {
            fileDesc.Handle = null;                                            // 0x8D6F7A..0x8D6F82 (delete [0x14]; store 0)
            return 1;                                                          // 0x8D6F84
        }
        if (fileDesc.Tag == WwiseFileDesc.TagAsset)                            // 0x8D6F58..0x8D6F62
        {
            ((IWwiseHostAsset)fileDesc.Handle!).Close();                       // 0x8D6F8A blx AAsset_close
            return 2;                                                          // 0x8D6F8E movs r0,#2
        }
        if (fileDesc.Tag != 0) return 2;                                       // 0x8D6F64 cbnz r0 -> 0x8D6F8E
        var file = fileDesc.Handle as IWwiseHostFile ?? throw new InvalidOperationException("M6-024 B12: fclose of a null FILE");
        return file.Close() == 0 ? 1 : 2;                                      // 0x8D6F68..0x8D6F76
    }

    /// <summary>hook vt+0xC (<c>0x8D6F9B</c>, <c>movs r0,#1; bx lr</c>): the block size is 1.</summary>
    public uint GetBlockSize(WwiseFileDesc fileDesc) => 1;

    int IWwiseLowLevelIoHook.Read(WwiseFileDesc fileDesc, WwiseTransferInfo transfer) => Read(fileDesc, transfer);

    int IWwiseLowLevelIoHook.Close(WwiseFileDesc fileDesc) => Close(fileDesc);
}
