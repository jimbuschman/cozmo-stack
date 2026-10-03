using System.Text;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-024 (C33.1): the Anki resolver, the zip index and the zip registry. Where an expected value is the engine's own, it comes from
/// re-analysis/tools/emu/emu_zip.py (the Thumb functions 0x8DE3BC and 0x8DE1D0 run under Unicorn on the same synthetic archives, with pread and the map's operator[] stood in); the
/// real-archive values come from the shipped AudioAssets.zip read by an independent parser (Python zipfile, header offset + 30 + local name length + local extra length). Each test names
/// the scenario or address it checks. The host is a memory file system implementing the libc calls (stat, fopen, open, pread), so a failing pread or a missing file can be scripted.
/// </summary>
public class WwiseAnkiResolverTests
{
    // ------------------------------------------------------------------ the host

    /// <summary>A scripted file: reads come from the bytes; the first pread calls can be scripted to return (ret, errno).</summary>
    private sealed class MemFile : IWwiseHostFile
    {
        private readonly byte[] _data;
        public readonly Queue<(int Ret, int Errno)> Script = new();
        public readonly List<(int N, uint Off)> Calls = new();
        public bool Closed;
        public int FseekResult;
        public int? FreadOverride;
        public MemFile(byte[] data) => _data = data;
        public int Fseek(long offset) => FseekResult;
        public int Fread(byte[] buffer, int index, int count)
        {
            if (FreadOverride is { } o) return o;
            int n = Math.Min(count, _data.Length);
            Array.Copy(_data, 0, buffer, index, n);
            return n;
        }
        public int Pread(byte[] buffer, int index, int count, uint offset, out int errno)
        {
            errno = 0;
            Calls.Add((count, offset));
            if (Script.Count > 0) { var (ret, err) = Script.Dequeue(); errno = err; return ret; }
            if (offset >= _data.Length) return 0;
            int n = Math.Min(count, _data.Length - (int)offset);
            Array.Copy(_data, (int)offset, buffer, index, n);
            return n;
        }
        public int Close() { Closed = true; return 0; }
    }

    private sealed class MemFs : IWwiseHostFileSystem
    {
        public readonly Dictionary<string, byte[]> Files = new();
        public readonly HashSet<string> Dirs = new();
        public readonly Dictionary<string, MemFile> Opened = new();
        public bool TryStat(string path, out WwiseHostStat stat)
        {
            if (Files.TryGetValue(path, out var d)) { stat = new WwiseHostStat(0x81A4, d.Length); return true; }
            if (Dirs.Contains(path)) { stat = new WwiseHostStat(0x41ED, 4096); return true; }
            stat = default;
            return false;
        }
        public IWwiseHostFile? Fopen(string path, string mode) => OpenReadOnly(path);
        public IWwiseHostFile? OpenReadOnly(string path)
        {
            if (!Files.TryGetValue(path, out var d)) return null;
            var f = new MemFile(d);
            Opened[path] = f;
            return f;
        }
    }

    private sealed class NoIo : IWwiseStreamIoSeam
    {
        public uint StaleQueryWord => 0;
        public int InitIoMemory(WwiseStreamDevice device, WwiseDeviceSettings settings) => 1;
        public int StartIoThread(WwiseStreamDevice device, WwiseDeviceSettings settings) => 1;
        public void DestroyDevice(WwiseStreamDevice device) { }
        public void AtCheckpoint(WwiseStreamDevice device, WwiseAutoStream stream, WwiseIoCheckpoint where) { }
        public bool TryCompleteFromCache(WwiseStreamDevice device, WwiseAutoStream stream) => false;
        public void ReleaseBuffer(WwiseStreamDevice device, WwiseStreamBuffer buffer) { }
    }

    private static byte[] BuildZip(IEnumerable<(string Name, byte[] Payload)> entries, byte[]? comment = null)
    {
        // the layout of emu_zip.py build_zip: stored entries; a 4-byte extra field in the LOCAL header of an entry whose name ends ".extra"; a 3-byte comment on every central entry
        var list = entries.ToList();
        var ms = new MemoryStream();
        var cd = new MemoryStream();
        foreach (var (name, payload) in list)
        {
            byte[] nb = Encoding.Latin1.GetBytes(name);
            byte[] extra = name.EndsWith(".extra", StringComparison.Ordinal) ? new byte[] { 0x55, 0x54, 0, 0 } : Array.Empty<byte>();
            uint lho = (uint)ms.Length;
            var w = new BinaryWriter(ms);
            w.Write(0x04034B50u); w.Write((ushort)20); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0);
            w.Write(0u); w.Write((uint)payload.Length); w.Write((uint)payload.Length); w.Write((ushort)nb.Length); w.Write((ushort)extra.Length);
            w.Write(nb); w.Write(extra); w.Write(payload);
            var c = new BinaryWriter(cd);
            c.Write(0x02014B50u); c.Write((ushort)20); c.Write((ushort)20); c.Write((ushort)0); c.Write((ushort)0); c.Write((ushort)0); c.Write((ushort)0);
            c.Write(0u); c.Write((uint)payload.Length); c.Write((uint)payload.Length); c.Write((ushort)nb.Length); c.Write((ushort)0); c.Write((ushort)3);
            c.Write((ushort)0); c.Write((ushort)0); c.Write(0u); c.Write(lho); c.Write(nb); c.Write(new byte[] { (byte)'a', (byte)'b', (byte)'c' });
        }
        uint cdo = (uint)ms.Length;
        cd.WriteTo(ms);
        var e = new BinaryWriter(ms);
        e.Write(0x06054B50u); e.Write((ushort)0); e.Write((ushort)0); e.Write((ushort)list.Count); e.Write((ushort)list.Count); e.Write((uint)cd.Length); e.Write(cdo);
        byte[] cm = comment ?? Array.Empty<byte>();
        e.Write((ushort)cm.Length); e.Write(cm);
        return ms.ToArray();
    }

    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    private static (WwiseZipIndex Index, MemFs Fs, bool Ok) Open(byte[] zip, string path = "a.zip")
    {
        var fs = new MemFs();
        fs.Files[path] = zip;
        var idx = new WwiseZipIndex(fs);
        return (idx, fs, idx.Open(path));
    }

    private static readonly (string Name, byte[] Payload)[] PlainEntries =
    {
        ("English(US)/", Array.Empty<byte>()), ("English(US)/10.wem", Ascii("hello")), ("20.wem", Ascii("world!!")), ("x.extra", Ascii("zz")), ("dup", Ascii("one")), ("dup", Ascii("twotwo")),
    };

    // ------------------------------------------------------------------ the zip index

    [Fact]
    public void Z1_ThePlainArchiveIndexesEveryNonEmptyEntryAtItsDataOffsetAndSkipsTheZeroSizeOne()
    {
        // M6-024 C33.1 C3 (0x8DE246..0x8DE368): emu_zip.py case "plain": result 1; "English(US)/10.wem" -> {90, 5}, "20.wem" -> {131, 7}, "x.extra" -> {179, 2} (the local extra field is added to the data
        // offset), "dup" -> {250, 6} (the later duplicate overwrites); the zero-size "English(US)/" is not inserted (0x8DE288 cmp r6,#0; beq 0x8DE310).
        var (idx, _, ok) = Open(BuildZip(PlainEntries));
        Assert.True(ok);
        Assert.Equal(4, idx.EntryCount);
        Assert.True(idx.Lookup("English(US)/10.wem", out uint off, out uint size)); Assert.Equal((90u, 5u), (off, size));
        Assert.True(idx.Lookup("20.wem", out off, out size)); Assert.Equal((131u, 7u), (off, size));
        Assert.True(idx.Lookup("x.extra", out off, out size)); Assert.Equal((179u, 2u), (off, size));
        Assert.True(idx.Lookup("dup", out off, out size)); Assert.Equal((250u, 6u), (off, size));
        Assert.False(idx.Lookup("English(US)/", out off, out size)); Assert.Equal((0u, 0u), (off, size));   // 0x8DE3A4: both outputs are zeroed first
        Assert.False(idx.Lookup("20.WEM", out _, out _));                                                  // the compare is case sensitive (0x8DEA9A..0x8DEACA)
    }

    [Theory]
    [InlineData(0x300, true)]     // emu_zip.py comment_below_the_scan_limit: result 1
    [InlineData(1024, true)]      // comment_1024: result 1 (the scan makes at most min(size - 0x16, 0x400) further reads)
    [InlineData(1025, false)]     // comment_1025: result 0
    [InlineData(0x500, false)]    // comment_beyond_the_scan_limit: result 0
    public void Z2_TheEndOfCentralDirectoryIsFoundWithinTheScanLimitOnly(int commentLength, bool expected)
    {
        // M6-024 C33.1 C2 (0x8DE1D0..0x8DE244): the comment length field is never checked; the scan goes back one byte at a time at most 0x400 reads past the first.
        var (idx, _, ok) = Open(BuildZip(new[] { ("a", Ascii("abc")) }, new string('c', commentLength).Select(c => (byte)c).ToArray()));
        Assert.Equal(expected, ok);
        if (expected)
        {
            Assert.True(idx.Lookup("a", out uint off, out uint size));
            Assert.Equal((31u, 3u), (off, size));                                                           // emu_zip.py: "a" -> {31, 3}
        }
        else Assert.Equal(0, idx.EntryCount);
    }

    [Fact]
    public void Z3_BadSignaturesFailTheWholeIndexAndAZeroSizeEntryIsNeverReadBack()
    {
        // M6-024 C33.1 C3: emu_zip.py bad_central_signature -> 0 (0x8DE27E cmp r0,sl; bne 0x8DE23E); bad_local_signature_nonempty -> 0 (0x8DE2DE cmp r1,r2; the tst at 0x8DE2E8);
        // bad_local_signature (the first local header belongs to the skipped zero-size entry) -> 1 with the same four entries; empty_archive -> 1 (count 0, 0x8DE36A); too_short -> 0 (0x8DE1E4 blo).
        byte[] plain = BuildZip(PlainEntries);
        uint cdOff = BitConverter.ToUInt32(plain, plain.Length - 6);
        var bad = (byte[])plain.Clone(); bad[cdOff] ^= 0xFF;
        Assert.False(Open(bad).Ok);
        var badLocal = (byte[])plain.Clone(); badLocal[42] ^= 0xFF;
        Assert.False(Open(badLocal).Ok);
        var badEmpty = (byte[])plain.Clone(); badEmpty[0] ^= 0xFF;
        var (idx, _, ok) = Open(badEmpty);
        Assert.True(ok);
        Assert.Equal(4, idx.EntryCount);
        var (empty, _, okEmpty) = Open(BuildZip(Array.Empty<(string, byte[])>()));
        Assert.True(okEmpty);
        Assert.Equal(0, empty.EntryCount);
        Assert.False(Open(Ascii("PKPKPKPKPK")).Ok);
    }

    [Fact]
    public void Z4_ThePreadLoopHasTheOraclesResults()
    {
        // M6-024 C33.1 C5 (0x8DE3BC..0x8DE408): emu_zip.py: whole -> 1 (one pread of 16 at 8); partial_then_rest -> 1 (preads 16@8 then 11@13: a short read advances); eof -> 0 (a pread of 0);
        // eintr_then_ok -> 1 (the same range is read again); other_error_is_success -> 1 with the buffer UNTOUCHED (0x8DE3F8 bne 0x8DE400 is `movs r0,#1`); zero_length -> 1 with no pread.
        // (The research text said "other errors fail"; the instruction at 0x8DE400 is the success return.)
        var (idx, fs, ok) = Open(BuildZip(PlainEntries));
        Assert.True(ok);
        var file = fs.Opened["a.zip"];
        var buf = new byte[16]; Array.Fill(buf, (byte)0xAA);

        file.Calls.Clear();
        Assert.True(idx.ReadAt(buf, 0, 8, 16));
        Assert.Equal(new[] { (16, 8u) }, file.Calls);

        file.Script.Enqueue((5, 0)); file.Calls.Clear();
        Assert.True(idx.ReadAt(buf, 0, 8, 16));
        Assert.Equal(new[] { (16, 8u), (11, 13u) }, file.Calls);

        file.Calls.Clear();
        Assert.False(idx.ReadAt(buf, 0, (uint)fs.Files["a.zip"].Length + 4, 16));                          // eof: a pread returning 0
        Assert.Equal(1, file.Calls.Count);

        file.Script.Enqueue((-1, 4)); file.Calls.Clear();
        Assert.True(idx.ReadAt(buf, 0, 8, 16));                                                            // EINTR: retried
        Assert.Equal(new[] { (16, 8u), (16, 8u) }, file.Calls);

        Array.Fill(buf, (byte)0xAA);
        file.Script.Enqueue((-1, 5)); file.Calls.Clear();
        Assert.True(idx.ReadAt(buf, 0, 8, 16));                                                            // EIO: success, buffer untouched
        Assert.All(buf, b => Assert.Equal(0xAA, b));
        Assert.Single(file.Calls);

        file.Calls.Clear();
        Assert.True(idx.ReadAt(buf, 0, 8, 0));
        Assert.Empty(file.Calls);
    }

    [Fact]
    public void Z5_TheEntryReadIsBoundedByTheArchiveSizeNotTheEntrySize()
    {
        // M6-024 C33.1 C5 (0x8DE654..0x8DE668): entryOffset + pos + size (32 bit) must be <= [idx+8] (the archive size, unsigned `ls`); the entry's own size is not checked.
        // emu_zip.py entry_*: inside -> 1 (pread 7 @ 131); one_past_the_entry -> 1 (8 @ 131); past_the_archive -> 0 (no pread); exactly_to_the_end -> 1; one_past_the_end -> 0;
        // wraps_32_bits (entryOffset 0xFFFFFFFF, pos 2, size 1: the sum wraps to 2, which is <= the size) -> 1 with a pread of 1 @ 1.
        byte[] zip = BuildZip(PlainEntries);
        var (idx, fs, ok) = Open(zip);
        Assert.True(ok);
        var file = fs.Opened["a.zip"];
        var buf = new byte[8];
        Assert.True(idx.ReadEntry(131, 0, 7, buf, 0));
        Assert.Equal("world!!", Encoding.ASCII.GetString(buf, 0, 7));
        Assert.True(idx.ReadEntry(131, 0, 8, buf, 0));
        file.Calls.Clear();
        Assert.False(idx.ReadEntry(131, (uint)zip.Length, 1, buf, 0));
        Assert.Empty(file.Calls);
        Assert.True(idx.ReadEntry(0, (uint)zip.Length - 4, 4, buf, 0));
        Assert.False(idx.ReadEntry(0, (uint)zip.Length - 4, 5, buf, 0));
        file.Calls.Clear();
        Assert.True(idx.ReadEntry(0xFFFFFFFF, 2, 1, buf, 0));
        Assert.Equal(new[] { (1, 1u) }, file.Calls);
    }

    [Fact]
    public void Z6_ThePathIsSplitAtQuestionMarksAndANestedArchiveIsIndexedAtItsOffset()
    {
        // M6-024 C33.1 C1 (0x8DDECC..0x8DDFC2): "outer?inner" opens the outer file, parses it, finds the entry "inner" (stored), adds its data offset to [4], sets [8] to its size, clears the map and parses
        // the inner archive; a missing entry returns 0; parts beyond the second are ignored; getline gives no final empty token ("a?" is one part).
        byte[] inner = BuildZip(new[] { ("in1", Ascii("abcd")), ("in2.extra", Ascii("xy")) });
        byte[] outer = BuildZip(new[] { ("pre", Ascii("0123456789")), ("assets/inner.zip", inner) });
        var fs = new MemFs();
        fs.Files["outer.zip"] = outer;
        var idx = new WwiseZipIndex(fs);
        Assert.True(idx.Open("outer.zip?assets/inner.zip"));
        // the outer data offset of "assets/inner.zip" by the zip format: 30 + 3 + 10 (the first entry) + 30 + 16 (the second local header)
        uint innerStart = 30 + 3 + 10 + 30 + 16;
        Assert.Equal(innerStart, idx.Base);
        Assert.Equal((uint)inner.Length, idx.Size);
        Assert.Equal(2, idx.EntryCount);
        Assert.True(idx.Lookup("in1", out uint off, out uint size));
        Assert.Equal((30u + 3, 4u), (off, size));                                                          // relative to the inner archive
        var buf = new byte[4];
        Assert.True(idx.ReadEntry(off, 0, 4, buf, 0));
        Assert.Equal("abcd", Encoding.ASCII.GetString(buf));                                               // read through [4] + offset
        Assert.False(new WwiseZipIndex(fs).Open("outer.zip?assets/missing.zip"));
        Assert.True(new WwiseZipIndex(fs).Open("outer.zip?assets/inner.zip?ignored?parts"));
        Assert.True(new WwiseZipIndex(fs).Open("outer.zip?"));                                             // "?": one part, "outer.zip", the trailing empty token is not produced
        Assert.Equal(new[] { "a" }, WwiseZipIndex.SplitGetline("a?"));
        Assert.Equal(new[] { "a", "", "b" }, WwiseZipIndex.SplitGetline("a??b"));
        Assert.Equal(new[] { "a", "b" }, WwiseZipIndex.SplitGetline("a?b"));
        Assert.Equal(new[] { "" }, WwiseZipIndex.SplitGetline("?"));
        Assert.Empty(WwiseZipIndex.SplitGetline(""));
    }

    [Fact]
    public void Z7_TheShippedArchiveIndexesToTheValuesOfAnIndependentParse()
    {
        // The shipped re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip: 2229 entries, one of them the zero-size "English(US)/" directory, so 2228 are indexed; each {offset, size}
        // is header offset + 30 + local name length + local extra length (an independent parse with Python zipfile); the first bytes are the RIFF headers of the entries.
        if (WwiseAssets.SoundDir is not { } dir) { Assert.Fail("the shipped re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip is not present: this test does not pass silently without it"); return; }
        var idx = new WwiseZipIndex(new WwiseDiskFileSystem());
        Assert.True(idx.Open(Path.Combine(dir, "AudioAssets.zip")));
        Assert.Equal(2228, idx.EntryCount);
        foreach (var (name, offset, size, head) in new[]
        {
            ("English(US)/1000221881.wem", 106185034u, 16196u, "524946463c3f000057415645666d7420"),
            ("English(US)/99908739.wem", 131021776u, 9289u, "524946464124000057415645666d7420"),
            ("1000323750.wem", 44u, 4120216u, "5249464690de3e0057415645666d7420"),
            ("998061257.wem", 106168717u, 10216u, "52494646e027000057415645666d7420"),
        })
        {
            Assert.True(idx.Lookup(name, out uint off, out uint sz));
            Assert.Equal((offset, size), (off, sz));
            var bytes = new byte[16];
            Assert.True(idx.ReadEntry(off, 0, 16, bytes, 0));
            Assert.Equal(head, Convert.ToHexString(bytes).ToLowerInvariant());
        }
        Assert.False(idx.Lookup("English(US)/", out _, out _));
    }

    // ------------------------------------------------------------------ the resolver

    private static (WwiseStreamManager Manager, WwiseAnkiResolver Resolver, MemFs Fs) NewResolver(MemFs? fs = null)
    {
        fs ??= new MemFs();
        var mgr = new WwiseStreamManager(new NoIo());
        var resolver = new WwiseAnkiResolver(mgr, fs);
        return (mgr, resolver, fs);
    }

    private static WwiseFileFlags Flags(uint codec = 1, byte lang = 0, uint company = 0) => new() { CodecId = codec, LanguageSpecific = lang, CompanyId = company };

    [Fact]
    public void R1_InitRefusesAnythingButSchedulerFlagsOneAndCreatesTheDevice()
    {
        // M6-024 C33.1 B1 (0x8D6C02..0x8D6C36): settings[0x14] != 1 returns 2; the first resolver becomes the global one (0x9606B4, 0x9606C4); the device id is stored at [this+0x48] and 2 returns when it is
        // -1; the deferral flag is stored first (0x8D6C12). The device: 0x960F34 takes the first empty slot (the count grows by one, 0x9610A4).
        var (mgr, resolver, _) = NewResolver();
        Assert.Equal(2, resolver.Init(new WwiseDeviceSettings { SchedulerFlags14 = 2 }, true));
        Assert.Null(mgr.Resolver);
        Assert.Equal(1, resolver.Init(WwiseDeviceSettings.Anki(), true));
        Assert.Same(resolver, mgr.Resolver);
        Assert.Equal(0, resolver.DeviceId);
        Assert.True(resolver.DeferOpen);
        Assert.Equal(1, mgr.DeviceCount);
        Assert.Equal(1, mgr.DeviceCapacity);
        Assert.NotNull(mgr.Device(0));
        var second = new WwiseAnkiResolver(mgr, new MemFs());
        Assert.Equal(1, second.Init(WwiseDeviceSettings.Anki(), false));
        Assert.Same(resolver, mgr.Resolver);                                                              // not replaced
        Assert.Equal(1, second.DeviceId);
        Assert.False(second.DeferOpen);
        Assert.Equal(2, mgr.DeviceCount);
        // a device that cannot initialise (granularity 0, 0x9616F8..0x961704) returns -1 and 2, leaving the slot reserved and empty
        var third = new WwiseAnkiResolver(mgr, new MemFs());
        Assert.Equal(2, third.Init(new WwiseDeviceSettings { Granularity10 = 0 }, true));
        Assert.Equal(-1, third.DeviceId);
        Assert.Equal(3, mgr.DeviceCount);
        Assert.Null(mgr.Device(2));
        var fourth = new WwiseAnkiResolver(mgr, new MemFs());
        Assert.Equal(1, fourth.Init(WwiseDeviceSettings.Anki(), true));
        Assert.Equal(2, fourth.DeviceId);                                                                 // the empty slot is used again
    }

    [Fact]
    public void R2_ADeferredOpenReturnsOneAtOnceWithAZeroDescriptor()
    {
        // M6-024 C33.1 B2 / B4 (0x8D6DBC..0x8D6E0A, 0x8D6CC6..0x8D6D14): the descriptor is cleared and its +0x18 set to the device id; a zero bSyncOpen with the deferral flag set returns 1 and leaves bSyncOpen 0;
        // otherwise bSyncOpen becomes 1 and the search runs.
        var (mgr, resolver, _) = NewResolver();
        resolver.Init(WwiseDeviceSettings.Anki(), true);
        var fd = new WwiseFileDesc { FileSize = 99, Tag = 5 };
        bool sync = false;
        Assert.Equal(1, resolver.OpenById(1234, 0, Flags(), ref sync, fd));
        Assert.False(sync);
        Assert.Equal((0L, 0u, 0), (fd.FileSize, fd.Tag, fd.DeviceId));
        Assert.Null(fd.Handle);
        bool sync2 = true;
        Assert.Equal(2, resolver.OpenById(1234, 0, Flags(), ref sync2, fd));                              // nothing registered: 2, not 0x42
        Assert.True(sync2);
        var undeferred = new WwiseAnkiResolver(mgr, new MemFs());
        undeferred.Init(WwiseDeviceSettings.Anki(), false);
        bool sync3 = false;
        Assert.Equal(2, undeferred.OpenByName("x", 0, Flags(), ref sync3, fd));
        Assert.True(sync3);                                                                               // set to 1 before the search
    }

    private static MemFs ShippedLikeFs(out string zipPath)
    {
        var fs = new MemFs();
        zipPath = "/obb/main.obb?assets/cozmo_resources/sound/AudioAssets.zip";
        byte[] inner = BuildZip(new[]
        {
            ("English(US)/42.wem", Ascii("englishUS42")), ("42.wem", Ascii("root42")), ("English(US)/7.bnk", Ascii("bank7")), ("English(US)/", Array.Empty<byte>()),
        });
        fs.Files["/obb/main.obb"] = BuildZip(new[] { ("assets/cozmo_resources/sound/AudioAssets.zip", inner) });
        fs.Dirs.Add("/sound/");
        return fs;
    }

    [Fact]
    public void R3_TheNameIsBuiltFromTheIdTheCodecAndTheLanguageFolder()
    {
        // M6-024 C33.1 B3 + B6 (0x8D6E0C..0x8D6E56, 0x8D739C..0x8D745E) and A9 (0x8D8228): "%u.wem" for a non-zero codec, "%u.bnk" for zero; a language-specific file is looked up as
        // "<language>/<name>" (the language is set from the config byte by 0x8D85FC / 0x960744: 0 = "English(US)"); the search order is the loose directory node (the mapper), then the zips.
        var fs = ShippedLikeFs(out string zip);
        var (mgr, resolver, _) = NewResolver(fs);
        resolver.Init(WwiseDeviceSettings.Anki(), false);
        resolver.PostInit(new WwiseAnkiResolverConfig { SoundDirectory = "/sound/", PathMapper = WwiseAnkiResolverConfig.MapSoundPath("/sound/"), ZipPaths = new[] { zip }, Language = 0 });
        Assert.Equal("English(US)", mgr.Language.Current);
        var fd = new WwiseFileDesc();
        bool sync = true;
        Assert.Equal(1, resolver.OpenById(42, 0, Flags(1, lang: 1), ref sync, fd));
        Assert.Equal((WwiseFileDesc.TagZip, 11L), (fd.Tag, fd.FileSize));                                // "English(US)/42.wem"
        Assert.Equal(1, resolver.OpenById(42, 0, Flags(1, lang: 0), ref sync, fd));
        Assert.Equal(6L, fd.FileSize);                                                                     // "42.wem" at the root
        Assert.Equal(1, resolver.OpenById(7, 0, Flags(0, lang: 1), ref sync, fd));
        Assert.Equal(5L, fd.FileSize);                                                                     // "English(US)/7.bnk"
        Assert.Equal(2, resolver.OpenById(7, 0, Flags(1, lang: 1), ref sync, fd));                        // "English(US)/7.wem" is not there: the zip miss falls to the asset path, 2
        Assert.Equal(2, resolver.OpenById(42, 1, Flags(1, lang: 1), ref sync, fd));                       // a non-zero open mode is refused by the name builder (0x8D73E8 cbnz r7)
        Assert.Equal(2, resolver.OpenById(42, 0, Flags(1, company: 2), ref sync, fd));                    // flags[0] > 1 (0x8D6E2A cmp r0,#1; bhi)
        Assert.Equal(2, resolver.OpenById(42, 0, null, ref sync, fd));                                    // null flags (0x8D6E1E)
        // the read goes to the zip entry at the archive offset
        Assert.Equal(1, resolver.OpenById(42, 0, Flags(1, lang: 1), ref sync, fd));
        var buf = new byte[11];
        Assert.Equal(1, resolver.Read(fd, new WwiseTransferInfo { FilePosition = 0, RequestedSize = 11, Buffer = buf }));
        Assert.Equal("englishUS42", Encoding.ASCII.GetString(buf));
        Assert.Equal(1, resolver.Read(fd, new WwiseTransferInfo { FilePosition = 7, RequestedSize = 4, Buffer = buf }));
        Assert.Equal("US42", Encoding.ASCII.GetString(buf, 0, 4));
        Assert.Equal(1, resolver.Close(fd));                                                               // "zip": the record is released, +0x14 cleared, 1
        Assert.Null(fd.Handle);
        // a non-0 language byte maps as 0x8D85FC: 1 German, 2 French(France), 3 Japanese, anything else English(US)
        Assert.Equal(new[] { "English(US)", "German", "French(France)", "Japanese", "English(US)" },
            new byte[] { 0, 1, 2, 3, 9 }.Select(WwiseLanguageState.NameForByte));
    }

    [Fact]
    public void R4_TheLooseFileBranchIsLiveWhenTheMapperIsBoundToANonEmptyDirectory()
    {
        // M6-024 C33.1 B7 (0x8D6FD6..0x8D70FE) with the verifier's correction 1: the std::function is bound to the non-empty sound directory (0x593BD8 returns false only for an empty bound string,
        // else out = bound + name), so a regular file at <dir>/<built name> is opened with fopen (0x8D713C): tag 0, iFileSize = st_size. A directory at that path, or a missing file, falls through to the zips.
        var (mgr, resolver, fs) = NewResolver();
        resolver.Init(WwiseDeviceSettings.Anki(), false);
        fs.Files["/sound/English(US)/9.wem"] = Ascii("loosefile!");
        fs.Dirs.Add("/sound/"); fs.Dirs.Add("/sound/English(US)/5.wem");
        resolver.PostInit(new WwiseAnkiResolverConfig { SoundDirectory = "/sound/", PathMapper = WwiseAnkiResolverConfig.MapSoundPath("/sound/"), Language = 0 });
        bool sync = true;
        var fd = new WwiseFileDesc();
        Assert.Equal(1, resolver.OpenById(9, 0, Flags(1, lang: 1), ref sync, fd));
        Assert.Equal((0u, 10L), (fd.Tag, fd.FileSize));
        Assert.IsAssignableFrom<IWwiseHostFile>(fd.Handle);
        var buf = new byte[4];
        Assert.Equal(1, resolver.Read(fd, new WwiseTransferInfo { FilePosition = 2, RequestedSize = 4, Buffer = buf }));   // MemFile.Fread reads from the start; the call is fseek (ok) + fread (4)
        Assert.Equal(1, resolver.Close(fd));                                                               // fclose == 0 -> 1
        Assert.True(fs.Opened["/sound/English(US)/9.wem"].Closed);
        Assert.Equal(2, resolver.OpenById(5, 0, Flags(1, lang: 1), ref sync, fd));                        // a directory is not a regular file (st_mode bit 15): falls through, 2
        Assert.True(WwiseAnkiResolverConfig.MapSoundPath("")("x") is (false, ""));                         // an empty bound string: false (0x593BF6..0x593C02)
        Assert.Equal((true, "dir/x"), WwiseAnkiResolverConfig.MapSoundPath("dir/")("x"));
        // with no mapper function the node's string and the name are concatenated and appended (0x8D705C..0x8D70DC); a name that makes the path 512 characters or more returns 2
        var (_, bare, bareFs) = NewResolver();
        bare.Init(WwiseDeviceSettings.Anki(), false);
        bareFs.Files["/n/English(US)/9.wem"] = Ascii("viaNode"); bareFs.Dirs.Add("/n/");
        bare.PostInit(new WwiseAnkiResolverConfig { SoundDirectory = "/n/", PathMapper = null, Language = 0 });
        Assert.Equal(1, bare.OpenById(9, 0, Flags(1, lang: 1), ref sync, fd));
        Assert.Equal(7L, fd.FileSize);
        Assert.Equal(0x1F, bare.BuildNameA8D739C(new string('n', 520), Flags(1), 0, out _));              // 0x8D743A movs r5,#0x1f
        Assert.Equal(0x1F, bare.BuildNameA8D739C(null, Flags(1), 0, out string empty));                   // a null name (0x8D73DC)
        Assert.Equal("", empty);
        Assert.Equal(2, bare.BuildNameA8D739C("x", null, 0, out _));
    }

    [Fact]
    public void R5_TheFopenResultsAndTheReadAndCloseRules()
    {
        // M6-024 C33.1 B8, B11, B12 (0x8D713C..0x8D7186, 0x8D6E88..0x8D6F0A, 0x8D6F46..0x8D6F90).
        var (_, resolver, fs) = NewResolver();
        resolver.Init(WwiseDeviceSettings.Anki(), false);
        var fd = new WwiseFileDesc();
        Assert.Equal(0x1F, resolver.FopenA8D713C(null, 0, fd));                                           // 0x8D7144 cbz r5
        fd.Handle = new object();
        Assert.Equal(0x1F, resolver.FopenA8D713C("x", 4, fd));                                            // mode >= 4: +0x14 = 0, 0x1F
        Assert.Null(fd.Handle);
        Assert.Equal(0x42, resolver.FopenA8D713C("missing", 0, fd));                                      // fopen NULL
        fs.Files["f"] = Ascii("0123456789");
        Assert.Equal(1, resolver.FopenA8D713C("f", 0, fd));
        Assert.Equal(10L, fd.FileSize);
        var file = fs.Opened["f"];
        var buf = new byte[4];
        // tag 0: fseek failing, or a count of 0, returns 2 (it falls to the tag test); a short read counts
        file.FseekResult = -1;
        Assert.Equal(2, resolver.Read(fd, new WwiseTransferInfo { FilePosition = 1, RequestedSize = 4, Buffer = buf }));
        file.FseekResult = 0; file.FreadOverride = 0;
        Assert.Equal(2, resolver.Read(fd, new WwiseTransferInfo { FilePosition = 1, RequestedSize = 4, Buffer = buf }));
        file.FreadOverride = 1;
        Assert.Equal(1, resolver.Read(fd, new WwiseTransferInfo { FilePosition = 1, RequestedSize = 4, Buffer = buf }));
        // an unknown tag reads and closes as 2
        var odd = new WwiseFileDesc { Tag = 0x1234 };
        Assert.Equal(2, resolver.Read(odd, new WwiseTransferInfo { Buffer = buf }));
        Assert.Equal(2, resolver.Close(odd));
        // the asset tag (inactive in the shipped configuration): Close returns 2 after closing, Read compares the count
        var asset = new FakeAsset(5);
        var withAssets = new WwiseAnkiResolver(new WwiseStreamManager(new NoIo()), new MemFs());
        var assetFd = new WwiseFileDesc { Tag = WwiseFileDesc.TagAsset, Handle = asset };
        Assert.Equal(1, withAssets.Read(assetFd, new WwiseTransferInfo { FilePosition = 0, RequestedSize = 5, Buffer = buf = new byte[8] }));
        Assert.Equal(2, withAssets.Read(assetFd, new WwiseTransferInfo { FilePosition = 0, RequestedSize = 6, Buffer = buf }));
        asset.SeekResult = -1;
        Assert.Equal(2, withAssets.Read(assetFd, new WwiseTransferInfo { FilePosition = 0, RequestedSize = 5, Buffer = buf }));
        Assert.Equal(2, withAssets.Close(assetFd));
        Assert.True(asset.Closed);
        Assert.Equal(1u, resolver.GetBlockSize(fd));                                                       // hook vt+0xC returns 1 (0x8D6F9B)
    }

    private sealed class FakeAsset : IWwiseHostAsset
    {
        public FakeAsset(int length) => Length = length;
        public int Length { get; }
        public int SeekResult;
        public bool Closed;
        public int Seek(long offset) => SeekResult;
        public int Read(byte[] buffer, int index, int count) => Math.Min(count, Length);
        public void Close() => Closed = true;
    }

    [Fact]
    public void R6_TheAssetPathIsTriedAfterTheZipsAndNeedsAManager()
    {
        // M6-024 C33.1 B10 (0x8D7288..0x8D7368): no asset manager returns 2; the path is the base, "/" when the base is non-empty, then the built name; a failed open returns 0x42 (a zip miss followed by
        // a failed asset open still returns 2 from the search, B5); success stores tag "asm", iFileSize = length, +8 = 0.
        var (_, resolver, _) = NewResolver();
        resolver.Init(WwiseDeviceSettings.Anki(), false);
        var seen = new List<string>();
        var manager = new FakeAssetManager(seen);
        resolver.PostInit(new WwiseAnkiResolverConfig { SoundDirectory = "/s/", AssetManager = manager, AssetBase = "snd", Language = 0 });
        bool sync = true;
        var fd = new WwiseFileDesc();
        Assert.Equal(1, resolver.OpenById(5, 0, Flags(1, lang: 1), ref sync, fd));
        Assert.Equal(new[] { "snd/English(US)/5.wem" }, seen);
        Assert.Equal((WwiseFileDesc.TagAsset, 5L), (fd.Tag, fd.FileSize));
        Assert.Equal(2, resolver.OpenById(404, 0, Flags(1), ref sync, fd));
        Assert.Equal(0x42, resolver.AssetOpenA8D7288("404.wem", Flags(1), 0, fd));
        var (_, none, _) = NewResolver();
        Assert.Equal(2, none.AssetOpenA8D7288("1.wem", Flags(1), 0, fd));
    }

    private sealed class FakeAssetManager : IWwiseHostAssetManager
    {
        private readonly List<string> _seen;
        public FakeAssetManager(List<string> seen) => _seen = seen;
        public IWwiseHostAsset? Open(string path)
        {
            _seen.Add(path);
            return path.Contains("404") ? null : new FakeAsset(5);
        }
    }

    [Fact]
    public void R7_TheRegistryOrdersApkBeforeObbAndReturnsTheRegistrationCodes()
    {
        // M6-024 C33.1 A10, A11 (0x8D8320, 0x8D843C..0x8D85A6): AddZipFiles registers each string with 0x8D843C(path, 0), which inserts at the FRONT, so the vector (OBB, then APK, 0x592FDA, 0x5932AA) ends as
        // APK first; an empty path returns 0x1F; a registered path (equal bytes) 0x28; an index that fails to open removes the object again and returns 2; success 1. AddZipFiles' result is ignored.
        var fs = new MemFs();
        fs.Files["/obb/main.obb"] = BuildZip(new[] { ("a", Ascii("x")) });
        fs.Files["/apk/base.apk"] = BuildZip(new[] { ("b", Ascii("y")) });
        var (_, resolver, _) = NewResolver(fs);
        resolver.AddZipFiles(new[] { "/obb/main.obb", "/apk/base.apk", "/missing.zip" });
        Assert.Equal(new[] { "/apk/base.apk", "/obb/main.obb" }, resolver.Zips.Select(z => z.Path));
        Assert.Equal(0x28, resolver.RegisterZip("/obb/main.obb", atEnd: false));
        Assert.Equal(0x1F, resolver.RegisterZip("", atEnd: false));
        Assert.Equal(2, resolver.RegisterZip("/nope", atEnd: true));
        Assert.Equal(2, resolver.Zips.Count);
        fs.Files["/z"] = BuildZip(new[] { ("c", Ascii("z")) });
        Assert.Equal(1, resolver.RegisterZip("/z", atEnd: true));
        Assert.Equal("/z", resolver.Zips[^1].Path);                                                       // flag != 0 appends
        // the next AddZipFiles first walks the zips registered so far (0x8D834C): one whose file has gone goes to 0x8DA938, which is unread
        fs.Files.Remove("/z");
        Assert.Throws<WwiseMissingBehaviourException>(() => resolver.AddZipFiles(Array.Empty<string>()));
        var seen = new List<string>();
        resolver.MissingArchiveHandler8DA938 = seen.Add;
        resolver.AddZipFiles(Array.Empty<string>());
        Assert.Equal(new[] { "/z" }, seen);
    }

    [Fact]
    public void R8_ASearchPathNodeIsKeptEvenWhenItIsNotADirectory()
    {
        // M6-024 C33.1 A9 (0x8DADAA..0x8DAE40): a path of length + language length + 1 >= 512 returns 0x1F (nothing stored); otherwise the node is pushed at the head and the result is 1 for a directory
        // (st_mode bit 14), else 0x24 (also when stat fails); the language length counts the current language.
        var (mgr, resolver, fs) = NewResolver();
        fs.Dirs.Add("/d"); fs.Files["/f"] = Ascii("x");
        Assert.Equal(1, resolver.AddSearchPath("/d"));
        Assert.Equal(0x24, resolver.AddSearchPath("/f"));
        Assert.Equal(0x24, resolver.AddSearchPath("/gone"));
        Assert.Equal(new[] { "/gone", "/f", "/d" }, resolver.SearchPaths);
        mgr.Language.Set("English(US)");                                                                  // 11 characters
        Assert.Equal(0x1F, resolver.AddSearchPath(new string('p', 500)));                                 // 500 + 11 + 1 = 512
        Assert.Equal(3, resolver.SearchPaths.Count);
        Assert.NotEqual(0x1F, resolver.AddSearchPath(new string('p', 499)));
    }

    [Fact]
    public void R9_SetCurrentLanguageHasTheEngineRulesAndCallsTheCallbacksLastFirst()
    {
        // M6-024 C33.1 B13 (0x960744..0x960806): a null name or a length above 0x1F returns 2; a last character '/' (0x2F) or '\' (0x5C) returns 2; an empty name is stored; every registered
        // callback runs from the last to the first; the result is 1.
        var lang = new WwiseLanguageState();
        var calls = new List<string>();
        lang.Callbacks.Add(s => calls.Add("first:" + s));
        lang.Callbacks.Add(s => calls.Add("second:" + s));
        Assert.Equal(2, lang.Set(null));
        Assert.Equal(2, lang.Set(new string('x', 0x20)));
        Assert.Equal(2, lang.Set("a/"));
        Assert.Equal(2, lang.Set("a\\"));
        Assert.Empty(calls);
        Assert.Equal(1, lang.Set(new string('x', 0x1F)));
        Assert.Equal(new[] { "second:" + new string('x', 0x1F), "first:" + new string('x', 0x1F) }, calls);
        Assert.Equal(1, lang.Set(""));
        Assert.Equal("", lang.Current);
    }
}
