using System.Text;

namespace Cozmo.Robot.Emulation;

/// <summary>
/// libacattsandroid.so (Acapela TTS for Android, shipped in the APK) loaded at 0x30000000 beside libc++_shared.so, with the
/// host it expects supplied by C# stand-ins, ported from the Acapela probe (<c>.scratch/probes/tts_probe.py</c>):
/// <list type="bullet">
/// <item>a virtual file system: guest paths under <see cref="VirtualRoot"/> map to the host voice directory (read only;
/// fopen/fread/fseek/ftell/fgets/getc/ungetc/fclose, open/read/lseek/close/fdopen, mmap/munmap as a heap copy,
/// opendir/readdir/closedir with bionic's dirent64 and the entries "." and ".." then the names in ordinal order);</item>
/// <item>an sscanf subset (<see cref="ScanfSubset"/>); fscanf stops the run;</item>
/// <item>qsort as a stable sort whose comparator runs as nested emulation on copies of the two elements;</item>
/// <item>time() fixed at 1700000000, clock_gettime from the host clock, usleep a no-op, srand48 ignored, lrand48 the 48-bit
/// LCG from its default state 0x1234ABCD330E;</item>
/// <item>atoi, strdup, strcasecmp, strcat, mbrtowc/wcrtomb (single byte), the stdio writers as no-ops, getenv 0;</item>
/// <item>raise, exit and __cxa_pure_virtual stop the run;</item>
/// <item>a JNIEnv of 240 slots with the 25 functions the library uses, a JavaVM whose GetEnv/AttachCurrentThread return that
/// env, and the Java objects (strings, String[], short[], method ids) held host side. Any other JNI slot stops the run.</item>
/// </list>
/// The JNI natives are exposed one for one; <see cref="AcapelaTts"/> calls them in the app's Java order.
/// </summary>
public sealed class AcapelaNative : IDisposable
{
    public const string Library = "libacattsandroid.so";
    public const string VirtualRoot = "/data/cozmo/tts";
    private const string NativePrefix = "Java_com_acapelagroup_android_tts_acattsandroid_";
    private const int NJni = 240;
    private const uint FakeTime = 1700000000;

    private readonly ArmMachine _m;
    private readonly LoadedLibrary _lib;
    private string? _hostRoot;

    public AcapelaNative(string libDir)
    {
        _m = new ArmMachine(libDir, new[] { "libc++_shared.so", Library });
        try
        {
            InstallHost();
            _m.Relocate();
            _lib = _m.Library(Library);
            _m.RunInitializers(Library);
            InstallJni();
        }
        catch
        {
            _m.Dispose();
            throw;
        }
    }

    public ArmMachine Machine => _m;

    /// <summary>The host directory that <see cref="VirtualRoot"/> maps to (the shipped <c>cozmo_resources/tts</c>).</summary>
    public string? HostRoot { get => _hostRoot; set => _hostRoot = value; }

    /// <summary>Samples delivered through the Java samplesCallback, in order.</summary>
    public List<short> Samples { get; } = new();

    /// <summary>Other Java callbacks (ttsevents and the like): method name and the raw argument words.</summary>
    public List<(string Method, int[] Args)> Events { get; } = new();

    public void Dispose() => _m.Dispose();

    // ---- virtual file system

    private sealed class VFile
    {
        public required byte[] Data;
        public long Pos;
        public required string Path;
    }

    private readonly Dictionary<uint, VFile> _files = new();
    private readonly Dictionary<uint, uint> _fileObj = new();
    private readonly Dictionary<uint, (List<(string Name, byte Type)> Entries, int Index)> _dirs = new();
    private readonly Dictionary<string, byte[]> _cache = new(StringComparer.Ordinal);
    private uint _nextHandle = 0x100;

    private string? HostPath(string p)
    {
        if (_hostRoot is null || !p.StartsWith(VirtualRoot, StringComparison.Ordinal)) return null;
        return _hostRoot + p[VirtualRoot.Length..];
    }

    private byte[]? FsOpen(string path)
    {
        var hp = HostPath(path);
        if (hp is null || !File.Exists(hp)) return null;
        if (!_cache.TryGetValue(hp, out var data)) _cache[hp] = data = File.ReadAllBytes(hp);
        return data;
    }

    private uint NewHandle(byte[] data, string path)
    {
        uint h = _nextHandle++;
        _files[h] = new VFile { Data = data, Pos = 0, Path = path };
        return h;
    }

    private VFile FileOf(uint fp) =>
        _fileObj.TryGetValue(fp, out var h) && _files.TryGetValue(h, out var f) ? f : throw new EmulationAbort($"stdio call on an unknown FILE* 0x{fp:X}");

    private static int S32(uint v) => unchecked((int)v);

    private static void Seek(VFile f, int off, uint whence) =>
        f.Pos = whence == 0 ? off : whence == 1 ? f.Pos + off : f.Data.Length + off;

    private static ReadOnlySpan<byte> Slice(byte[] d, long from, long count)
    {
        long a = Math.Clamp(from, 0, d.Length), b = Math.Clamp(from + Math.Max(count, 0), 0, d.Length);
        return b > a ? d.AsSpan((int)a, (int)(b - a)) : ReadOnlySpan<byte>.Empty;
    }

    private void InstallHost()
    {
        var L = _m.Libc;
        L["fopen"] = m =>
        {
            string path = ArmMachine.Lat(m.CStr(m.Reg(0))), mode = ArmMachine.Lat(m.CStr(m.Reg(1)));
            m.Log.Add($"fopen {path} {mode}");
            if (mode.Contains('w') || mode.Contains('a')) return 0;
            var data = FsOpen(path);
            if (data is null) return 0;
            uint fp = m.Malloc(0x60);
            m.Fill(fp, 0x60, 0);
            _fileObj[fp] = NewHandle(data, path);
            return fp;
        };
        L["fread"] = m =>
        {
            uint buf = m.Reg(0), sz = m.Reg(1), n = m.Reg(2);
            var f = FileOf(m.Reg(3));
            var chunk = Slice(f.Data, f.Pos, (long)sz * n);
            f.Pos += chunk.Length;
            m.Write(buf, chunk);
            return sz != 0 ? (uint)(chunk.Length / sz) : 0u;
        };
        L["fseek"] = m => { Seek(FileOf(m.Reg(0)), S32(m.Reg(1)), m.Reg(2)); return 0; };
        L["fseeko"] = L["fseek"];
        L["ftell"] = m => (uint)FileOf(m.Reg(0)).Pos;
        L["fclose"] = m => { _fileObj.Remove(m.Reg(0)); return 0; };
        L["fgets"] = m =>
        {
            uint buf = m.Reg(0); int n = S32(m.Reg(1));
            var f = FileOf(m.Reg(2));
            if (f.Pos >= f.Data.Length) return 0;
            long e = f.Pos < 0 ? -1 : Array.IndexOf(f.Data, (byte)10, (int)f.Pos);
            e = e < 0 ? f.Data.Length : e + 1;
            e = Math.Min(e, f.Pos + n - 1);
            var line = Slice(f.Data, f.Pos, e - f.Pos);
            m.Write(buf, line);
            m.W8(buf + (uint)line.Length, 0);
            f.Pos = e;
            return buf;
        };
        L["getc"] = m =>
        {
            var f = FileOf(m.Reg(0));
            if (f.Pos >= f.Data.Length) return 0xFFFFFFFF;
            return (uint)f.Data[f.Pos++];
        };
        L["fgetc"] = L["getc"];
        L["ungetc"] = m => { FileOf(m.Reg(1)).Pos -= 1; return m.Reg(0); };
        L["open"] = m =>
        {
            string path = ArmMachine.Lat(m.CStr(m.Reg(0)));
            m.Log.Add($"open {path} {m.Reg(1)}");
            var data = FsOpen(path);
            return data is null ? 0xFFFFFFFF : NewHandle(data, path);
        };
        L["read"] = m =>
        {
            uint fd = m.Reg(0), buf = m.Reg(1), n = m.Reg(2);
            if (!_files.TryGetValue(fd, out var f)) return 0xFFFFFFFF;
            var chunk = Slice(f.Data, f.Pos, n);
            f.Pos += chunk.Length;
            m.Write(buf, chunk);
            return (uint)chunk.Length;
        };
        L["lseek"] = m =>
        {
            if (!_files.TryGetValue(m.Reg(0), out var f)) throw new EmulationAbort($"lseek on an unknown fd {m.Reg(0)}");
            Seek(f, S32(m.Reg(1)), m.Reg(2));
            return (uint)f.Pos;
        };
        L["close"] = m => { _files.Remove(m.Reg(0)); return 0; };
        L["fdopen"] = m =>
        {
            uint fd = m.Reg(0);
            if (!_files.ContainsKey(fd)) return 0;
            uint fp = m.Malloc(0x60);
            m.Fill(fp, 0x60, 0);
            _fileObj[fp] = fd;
            return fp;
        };
        L["fcntl"] = _ => 0;
        L["mmap"] = m =>
        {
            uint len = m.Reg(1), fd = m.Arg(4), off = m.Arg(5);
            uint a = m.Malloc(len);
            if (a == 0) return 0xFFFFFFFF;
            if (_files.TryGetValue(fd, out var f))
            {
                var d = Slice(f.Data, off, len);
                m.Write(a, d);
                m.Fill(a + (uint)d.Length, len - (uint)d.Length, 0);
            }
            else m.Fill(a, len, 0);
            m.Log.Add($"mmap {len} {fd}");
            return a;
        };
        L["munmap"] = m => { m.Free(m.Reg(0)); return 0; };
        L["opendir"] = m =>
        {
            string path = ArmMachine.Lat(m.CStr(m.Reg(0))).TrimEnd('/');
            m.Log.Add($"opendir {path}");
            var hp = HostPath(path);
            if (hp is null || !Directory.Exists(hp)) return 0;
            uint d = m.Malloc(0x200);
            m.Fill(d, 0x200, 0);
            var names = Directory.EnumerateFileSystemEntries(hp).Select(p => Path.GetFileName(p)!).ToList();
            names.Sort(StringComparer.Ordinal);
            var ents = new List<(string, byte)> { (".", 4), ("..", 4) };
            foreach (var nm in names) ents.Add((nm, Directory.Exists(Path.Combine(hp, nm)) ? (byte)4 : (byte)8));
            _dirs[d] = (ents, 0);
            return d;
        };
        L["readdir"] = m =>
        {
            uint d = m.Reg(0);
            if (!_dirs.TryGetValue(d, out var st)) throw new EmulationAbort($"readdir on an unknown DIR* 0x{d:X}");
            if (st.Index >= st.Entries.Count) return 0;
            var (name, typ) = st.Entries[st.Index];
            _dirs[d] = (st.Entries, st.Index + 1);
            uint ent = d + 0x10;
            m.Fill(ent, 0x118, 0);
            m.W16(ent + 16, 0x118);                  // d_reclen
            m.W8(ent + 18, typ);                     // d_type: DT_DIR 4, DT_REG 8
            var nb = Encoding.UTF8.GetBytes(name);
            m.Write(ent + 19, nb);                   // d_name
            m.W8(ent + 19 + (uint)nb.Length, 0);
            return ent;
        };
        L["closedir"] = _ => 0;

        L["time"] = m => { if (m.Reg(0) != 0) m.W32(m.Reg(0), FakeTime); return FakeTime; };
        L["clock_gettime"] = m =>
        {
            long ticks = DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks;
            m.W32(m.Reg(1), (uint)(ticks / TimeSpan.TicksPerSecond));
            m.W32(m.Reg(1) + 4, (uint)(ticks % TimeSpan.TicksPerSecond * 100));
            return 0;
        };
        L["usleep"] = _ => 0;
        L["srand48"] = _ => 0;
        ulong rand48 = 0x1234ABCD330E;
        L["lrand48"] = _ =>
        {
            rand48 = (0x5DEECE66DUL * rand48 + 0xB) & 0xFFFFFFFFFFFFUL;
            return (uint)(rand48 >> 17);
        };
        L["atoi"] = m => unchecked((uint)Atoi(m, m.CStr(m.Reg(0))));
        L["strdup"] = m =>
        {
            var s = m.CStr(m.Reg(0), 1 << 20);
            uint a = m.Malloc((ulong)s.Length + 1);
            m.Write(a, s);
            m.W8(a + (uint)s.Length, 0);
            return a;
        };
        L["strcasecmp"] = m =>
        {
            var a = AsciiLower(m.CStr(m.Reg(0)));
            var b = AsciiLower(m.CStr(m.Reg(1)));
            int c = a.AsSpan().SequenceCompareTo(b);
            return c == 0 ? 0u : c < 0 ? 0xFFFFFFFF : 1u;
        };
        L["strcat"] = m =>
        {
            uint d = m.Reg(0);
            var s = m.CStr(d, 1 << 20);
            var t = m.CStr(m.Reg(1), 1 << 20);
            m.Write(d + (uint)s.Length, t);
            m.W8(d + (uint)s.Length + (uint)t.Length, 0);
            return d;
        };
        foreach (var nm in new[] { "fputc", "putc", "fputs", "puts", "fflush" }) L[nm] = _ => 0;
        L["fwrite"] = m => m.Reg(2);
        L["sscanf"] = m =>
        {
            string src = ArmMachine.Lat(m.CStr(m.Reg(0), 1 << 16)), fmt = ArmMachine.Lat(m.CStr(m.Reg(1)));
            var (n, vals) = ScanfSubset.Scan(src, fmt);
            for (int k = 0; k < vals.Count; k++) Store(m, m.Arg(2 + k), vals[k]);
            return unchecked((uint)n);
        };
        L["fscanf"] = m => throw new EmulationAbort($"fscanf is not modelled: {ArmMachine.Lat(m.CStr(m.Reg(1)))}");
        L["raise"] = _ => throw new EmulationAbort("raise");
        L["exit"] = _ => throw new EmulationAbort("exit");
        L["__cxa_pure_virtual"] = _ => throw new EmulationAbort("pure virtual");
        L["mbrtowc"] = m =>
        {
            uint pwc = m.Reg(0), s = m.Reg(1);
            if (s == 0) return 0;
            byte b = m.R8(s);
            if (pwc != 0) m.W32(pwc, b);
            return b == 0 ? 0u : 1u;
        };
        L["wcrtomb"] = m => { if (m.Reg(0) != 0) m.W8(m.Reg(0), m.Reg(1) & 0xFF); return 1; };
        L["getenv"] = _ => 0;
        L["qsort"] = Qsort;
    }

    private static byte[] AsciiLower(byte[] s)
    {
        var r = (byte[])s.Clone();
        for (int i = 0; i < r.Length; i++) if (r[i] >= 'A' && r[i] <= 'Z') r[i] += 32;
        return r;
    }

    /// <summary>Bionic atoi delegates to 32-bit strtol, including its overflow and errno behavior.</summary>
    private static int Atoi(ArmMachine m, byte[] s)
    {
        int i = 0;
        while (i < s.Length && s[i] is 9 or 10 or 11 or 12 or 13 or 32) i++;
        bool neg = false;
        if (i < s.Length && s[i] is (byte)'+' or (byte)'-') { neg = s[i] == '-'; i++; }
        uint v = 0;
        uint limit = neg ? 2147483648u : 2147483647u;
        bool overflow = false;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            uint digit = (uint)(s[i++] - '0');
            if (v > (limit - digit) / 10) overflow = true;
            if (!overflow) v = v * 10 + digit;
        }
        if (overflow)
        {
            m.W32(m.ErrnoAddress, 34);
            return neg ? int.MinValue : int.MaxValue;
        }
        return neg ? unchecked(-(int)v) : (int)v;
    }

    private static void Store(ArmMachine m, uint a, ScanfSubset.Value v)
    {
        switch (v.Kind)
        {
            case ScanfSubset.Kind.Int32: m.W32(a, (uint)(v.Integer & uint.MaxValue)); break;
            case ScanfSubset.Kind.Int16: m.W16(a, (uint)(v.Integer & ushort.MaxValue)); break;
            case ScanfSubset.Kind.Int64:
                {
                    if (v.Integer < long.MinValue || v.Integer > long.MaxValue) throw new EmulationAbort("sscanf: %lld out of range");
                    m.Write(a, BitConverter.GetBytes((long)v.Integer));
                    break;
                }
            case ScanfSubset.Kind.Float:
                {
                    float f = (float)v.Real;
                    if (float.IsInfinity(f) && !double.IsInfinity(v.Real)) throw new EmulationAbort("sscanf: %f out of float range");
                    m.W32(a, BitConverter.SingleToUInt32Bits(f));
                    break;
                }
            case ScanfSubset.Kind.Double: m.Write(a, BitConverter.GetBytes(v.Real)); break;
            case ScanfSubset.Kind.String:
                {
                    var b = Encoding.Latin1.GetBytes(v.Text);
                    m.Write(a, b);
                    m.W8(a + (uint)b.Length, 0);
                    break;
                }
            case ScanfSubset.Kind.Chars: m.Write(a, Encoding.Latin1.GetBytes(v.Text)); break;
        }
    }

    /// <summary>qsort(base, n, size, cmp): the elements are read out, sorted stably with the guest comparator called on copies
    /// placed in one scratch block (malloc(2*size), as the probe), and written back.</summary>
    private StubResult Qsort(ArmMachine m)
    {
        uint b = m.Reg(0), n = m.Reg(1), sz = m.Reg(2), cmp = m.Reg(3);
        uint lr = m.Lr;
        var items = new byte[n][];
        for (uint k = 0; k < n; k++) items[k] = m.Read(b + k * sz, sz);
        uint ta = m.Malloc(2UL * sz);
        int Compare(byte[] x, byte[] y)
        {
            m.Write(ta, x);
            m.Write(ta + sz, y);
            return S32(m.NestedCall(cmp, ta, ta + sz));
        }
        var sorted = StableSort(items, Compare);
        for (uint k = 0; k < n; k++) m.Write(b + k * sz, sorted[k]);
        m.SetLr(lr);
        return 0;
    }

    private static byte[][] StableSort(byte[][] a, Func<byte[], byte[], int> cmp)
    {
        if (a.Length < 2) return a;
        int mid = a.Length / 2;
        var l = StableSort(a[..mid], cmp);
        var r = StableSort(a[mid..], cmp);
        var o = new byte[a.Length][];
        int i = 0, j = 0, k = 0;
        while (i < l.Length && j < r.Length) o[k++] = cmp(r[j], l[i]) < 0 ? r[j++] : l[i++];
        while (i < l.Length) o[k++] = l[i++];
        while (j < r.Length) o[k++] = r[j++];
        return o;
    }

    // ---- JNI

    private abstract record JObj;
    private sealed record JString(string Value) : JObj;
    private sealed record JStringArray(uint[] Items) : JObj;
    private sealed record JShortArray(List<short> Items) : JObj;
    private sealed record JMethod(string Name, string Signature) : JObj;
    private sealed record JPlain(string What) : JObj;

    private readonly Dictionary<uint, JObj> _objs = new();
    private readonly Dictionary<uint, string> _methods = new();
    private uint _nextObj = 0x1000;
    private uint _env, _vm, _this;

    private uint NewObj(JObj o)
    {
        uint h = _nextObj;
        _nextObj += 4;
        _objs[h] = o;
        return h;
    }

    private JObj Obj(uint h) => _objs.TryGetValue(h, out var o) ? o : throw new EmulationAbort($"JNI: unknown object handle 0x{h:X}");
    private string Str(uint h) => Obj(h) is JString s ? s.Value : throw new EmulationAbort($"JNI: handle 0x{h:X} is not a String");

    public uint NewString(string s) => NewObj(new JString(s));
    public uint NewStringArray(params string[] items)
    {
        var hs = items.Select(NewString).ToArray();
        return NewObj(new JStringArray(hs));
    }
    public string? StringOf(uint h) => _objs.TryGetValue(h, out var o) && o is JString s ? s.Value : null;

    private void InstallJni()
    {
        var L = _m.Libc;
        uint jtab = _m.StaticAlloc(4 * NJni);
        for (int i = 0; i < NJni; i++) _m.W32(jtab + 4 * (uint)i, _m.Stub($"JNI_{i}"));
        _env = _m.StaticAlloc(16);
        _m.W32(_env, jtab);
        _this = NewObj(new JPlain("acattsandroid"));
        uint cls = NewObj(new JPlain("class acattsandroid"));
        for (int i = 0; i < NJni; i++)
        {
            int k = i;
            L[$"JNI_{i}"] = m => throw new EmulationAbort($"JNI function {k} not modelled (lr 0x{m.Lr:X})");
        }
        void J(int i, ImportHandler h) => L[$"JNI_{i}"] = h;
        J(31, _ => cls);                                             // GetObjectClass
        J(6, _ => cls);                                              // FindClass
        ImportHandler getMethodId = m =>
        {
            string name = Encoding.UTF8.GetString(m.CStr(m.Reg(2))), sig = Encoding.UTF8.GetString(m.CStr(m.Reg(3)));
            uint h = NewObj(new JMethod(name, sig));
            _methods[h] = name;
            return h;
        };
        J(33, getMethodId);                                          // GetMethodID
        J(113, getMethodId);                                         // GetStaticMethodID
        J(21, m => m.Reg(1));                                        // NewGlobalRef
        J(22, _ => 0);                                               // DeleteGlobalRef
        J(23, _ => 0);                                               // DeleteLocalRef
        J(228, _ => 0);                                              // ExceptionCheck
        J(15, _ => 0);                                               // ExceptionOccurred
        J(17, _ => 0);                                               // ExceptionClear
        J(167, m => NewObj(new JString(Encoding.UTF8.GetString(m.CStr(m.Reg(1), 1 << 16)))));   // NewStringUTF
        J(169, m =>                                                  // GetStringUTFChars
        {
            var s = Encoding.UTF8.GetBytes(Str(m.Reg(1)));
            uint a = m.Malloc((ulong)s.Length + 1);
            m.Write(a, s);
            m.W8(a + (uint)s.Length, 0);
            if (m.Reg(2) != 0) m.W8(m.Reg(2), 0);
            return a;
        });
        J(170, _ => 0);                                              // ReleaseStringUTFChars
        J(168, m => (uint)Encoding.UTF8.GetByteCount(Str(m.Reg(1))));                          // GetStringUTFLength
        J(171, m => Obj(m.Reg(1)) switch                             // GetArrayLength
        {
            JStringArray a => (uint)a.Items.Length,
            JShortArray a => (uint)a.Items.Count,
            _ => throw new EmulationAbort("JNI GetArrayLength on a non-array"),
        });
        J(173, m => Obj(m.Reg(1)) is JStringArray a ? a.Items[m.Reg(2)] : throw new EmulationAbort("JNI GetObjectArrayElement on a non-object array"));
        J(178, m => NewObj(new JShortArray(new List<short>(new short[m.Reg(1)]))));            // NewShortArray
        J(210, m =>                                                  // SetShortArrayRegion
        {
            if (Obj(m.Reg(1)) is not JShortArray arr) throw new EmulationAbort("JNI SetShortArrayRegion on a non-short array");
            int start = (int)m.Reg(2), len = (int)m.Reg(3);
            var raw = m.Read(m.Arg(4), 2u * (uint)len);
            var vals = new short[len];
            Buffer.BlockCopy(raw, 0, vals, 0, raw.Length);
            // Python slice assignment arr[start:start+len] = vals (clipped to the list, which may grow)
            int a = Math.Min(start, arr.Items.Count), e = Math.Min(start + len, arr.Items.Count);
            if (e > a) arr.Items.RemoveRange(a, e - a);
            arr.Items.InsertRange(a, vals);
            return 0;
        });
        ImportHandler callMethod = m =>                              // CallVoidMethod / CallLongMethod / CallIntMethod
        {
            string name = _methods.TryGetValue(m.Reg(2), out var nm) ? nm : "?";
            if (name == "samplesCallback")
            {
                if (Obj(m.Reg(3)) is not JShortArray arr) throw new EmulationAbort("samplesCallback without a short[]");
                Deliver(arr, m.Arg(4));
            }
            else Events.Add((name, Enumerable.Range(3, 9).Select(k => S32(m.Arg(k))).ToArray()));
            return 0;
        };
        ImportHandler callMethodV = m =>                             // the ...MethodV forms: arguments behind a va_list
        {
            string name = _methods.TryGetValue(m.Reg(2), out var nm) ? nm : "?";
            uint ap = m.Reg(3);
            if (name == "samplesCallback")
            {
                if (Obj(m.R32(ap)) is not JShortArray arr) throw new EmulationAbort("samplesCallback without a short[]");
                uint q = (ap + 4 + 7) & ~7u;                         // the jlong count is 8-byte aligned
                Deliver(arr, m.R32(q));
            }
            else
            {
                uint q = (ap + 7) & ~7u;
                Events.Add((name, Enumerable.Range(0, 5).Select(k => S32(m.R32(q + 8 * (uint)k))).ToArray()));
            }
            return 0;
        };
        J(53, callMethodV); J(62, callMethodV); J(50, callMethodV);
        J(61, callMethod); J(52, callMethod); J(49, callMethod);

        uint vmtab = _m.StaticAlloc(4 * 8);
        for (int i = 0; i < 8; i++) _m.W32(vmtab + 4 * (uint)i, _m.Stub($"VM_{i}"));
        _vm = _m.StaticAlloc(16);
        _m.W32(_vm, vmtab);
        ImportHandler vmEnv = m => { m.W32(m.Reg(1), _env); return 0; };
        L["VM_4"] = vmEnv;                                           // AttachCurrentThread
        L["VM_6"] = vmEnv;                                           // GetEnv
        L["VM_7"] = vmEnv;                                           // AttachCurrentThreadAsDaemon
        L["VM_5"] = _ => 0;                                          // DetachCurrentThread
        L["VM_3"] = _ => 0;                                          // DestroyJavaVM
        J(219, m => { m.W32(m.Reg(1), _vm); return 0; });            // GetJavaVM
        J(4, _ => 0x10006);                                          // GetVersion
    }

    private void Deliver(JShortArray arr, uint count)
    {
        int n = (int)Math.Min(count, (uint)arr.Items.Count);
        for (int i = 0; i < n; i++) Samples.Add(arr.Items[i]);
    }

    // ---- the JNI natives (com.acapelagroup.android.tts.acattsandroid), called as Dalvik calls them: (env, this, args...)

    private uint Native(string name, uint[]? args = null, uint[]? stack = null)
    {
        if (!_lib.Symbols.TryGetValue(NativePrefix + name, out var fn)) throw new EmulationAbort($"{Library} has no native {name}");
        var a = new uint[2 + (args?.Length ?? 0)];
        a[0] = _env; a[1] = _this;
        args?.CopyTo(a, 2);
        return _m.Call(fn, a, stack);
    }

    private static uint F32(float f) => BitConverter.SingleToUInt32Bits(f);

    public int InitCallbacks() => S32(Native("nInitCallbacks"));
    public string? GetVersion() => StringOf(Native("nGetVersion"));
    public int GetVoiceList(params string[] voiceDirPaths) => S32(Native("nGetVoiceList", new[] { NewStringArray(voiceDirPaths) }));
    public string? GetVoiceInfo(string iniPath, string key) => StringOf(Native("nGetVoiceInfo", new[] { NewString(iniPath), NewString(key) }));
    /// <summary>nSetLicense(long userid, long password, String license): userid in r2:r3, password and the licence on the
    /// stack (the jlong 8-byte aligned).</summary>
    public int SetLicense(uint userId, uint password, byte[] license)
    {
        uint lic = NewString(Encoding.Latin1.GetString(license));
        return S32(Native("nSetLicense", new[] { userId, 0u }, new[] { password, 0u, lic }));
    }
    public int LoadVoice(string voice, string mode) => S32(Native("nLoadVoice", new[] { NewString(voice), NewString(mode) }));
    public int SetTtsSetting(string name, int value) => S32(Native("nSetTTSSetting", new[] { NewString(name), unchecked((uint)value) }));
    public int SetSpeechRate(float rate) => S32(Native("nSetSpeechRate", new[] { F32(rate) }));
    public int SetShape(float shape) => S32(Native("nSetShape", new[] { F32(shape) }));
    public int Speak(string text) => S32(Native("nSpeak", new[] { NewString(text) }));
    public int Stop() => S32(Native("nStop"));
    public int GetLastError() => S32(Native("nGetLastError"));
}
