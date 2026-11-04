using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static Cozmo.Robot.Emulation.UnicornNative;

namespace Cozmo.Robot.Emulation;

/// <summary>
/// The base bionic stand-ins, ported one for one from <c>emu_elf.Machine._install_libc</c> and its data symbols. Where the
/// Python's behaviour differs from C (memcmp/strcmp return -1/0/1, the printf subset ignores precision for integers and
/// width for strings, string reads stop at 4096 bytes), the port keeps the Python's behaviour, because the Python is the
/// oracle these machines are compared with.
/// </summary>
public sealed unsafe partial class ArmMachine
{
    private const uint M32 = 0xFFFFFFFF;
    private readonly Dictionary<uint, (uint[] Core, ulong[] D)> _jmpbufs = new();
    private readonly Dictionary<uint, uint> _tls = new();
    private uint _nextKey = 1;

    private static int S32(uint v) => unchecked((int)v);

    private static int Cmp(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        int c = a.SequenceCompareTo(b);
        return c == 0 ? 0 : (c < 0 ? -1 : 1);
    }

    private void InstallLibc()
    {
        var L = Libc;
        foreach (var nm in new[] { "pthread_mutex_lock", "pthread_mutex_unlock", "pthread_mutex_init", "pthread_mutex_destroy",
                     "pthread_mutex_trylock", "pthread_mutexattr_init", "pthread_mutexattr_destroy", "pthread_mutexattr_settype",
                     "pthread_cond_init", "pthread_cond_destroy", "pthread_cond_signal", "pthread_cond_broadcast",
                     "pthread_cond_wait", "sched_yield", "__cxa_atexit", "__aeabi_atexit", "__cxa_finalize", "fflush",
                     "pthread_setspecific_dummy" })
            L[nm] = _ => 0;

        L["malloc"] = m => m.Malloc(m.Reg(0));
        L["calloc"] = m =>
        {
            ulong n = (ulong)m.Reg(0) * m.Reg(1);
            uint a = m.Malloc(n);
            if (a != 0) m.Fill(a, (uint)n, 0);
            return a;
        };
        L["free"] = m => { m.Free(m.Reg(0)); return 0; };
        L["realloc"] = m =>
        {
            uint p = m.Reg(0), n = m.Reg(1);
            if (p == 0) return m.Malloc(n);
            uint old = m.BlockSize(p);
            uint a = m.Malloc(n);
            if (a != 0)
            {
                m.Write(a, m.Read(p, Math.Min(old, n)));
                m.Free(p);
            }
            return a;
        };
        L["memcpy"] = m =>
        {
            uint d = m.Reg(0), s = m.Reg(1), n = m.Reg(2);
            m.Copy(d, s, n);
            return d;
        };
        L["memmove"] = L["memcpy"];
        L["__aeabi_memcpy"] = m => { m.Copy(m.Reg(0), m.Reg(1), m.Reg(2)); return StubResult.Void; };
        L["__aeabi_memmove"] = L["__aeabi_memcpy"];
        L["memset"] = m =>
        {
            uint d = m.Reg(0), v = m.Reg(1), n = m.Reg(2);
            m.Fill(d, n, (byte)v);
            return d;
        };
        L["__aeabi_memclr8"] = m => { m.Fill(m.Reg(0), m.Reg(1), 0); return StubResult.Void; };
        L["__aeabi_memclr"] = L["__aeabi_memclr4"] = L["__aeabi_memclr8"];
        L["__aeabi_memset"] = m => { m.Fill(m.Reg(0), m.Reg(1), (byte)m.Reg(2)); return StubResult.Void; };
        L["__aeabi_memset8"] = L["__aeabi_memset4"] = L["__aeabi_memset"];
        L["memcmp"] = m => Cmp(m.Read(m.Reg(0), m.Reg(2)), m.Read(m.Reg(1), m.Reg(2)));
        L["memchr"] = m =>
        {
            uint p = m.Reg(0), n = m.Reg(2);
            int i = m.Read(p, n).AsSpan().IndexOf((byte)m.Reg(1));
            return i < 0 ? 0u : p + (uint)i;
        };
        L["strlen"] = m => (uint)m.CStr(m.Reg(0), 1 << 20).Length;
        L["strcmp"] = m => Cmp(m.CStr(m.Reg(0)), m.CStr(m.Reg(1)));
        L["strncmp"] = m =>
        {
            int n = (int)Math.Min(m.Reg(2), int.MaxValue);
            var a = m.CStr(m.Reg(0)); var b = m.CStr(m.Reg(1));
            return Cmp(a.AsSpan(0, Math.Min(n, a.Length)), b.AsSpan(0, Math.Min(n, b.Length)));
        };
        L["strchr"] = m =>
        {
            uint p = m.Reg(0); byte c = (byte)m.Reg(1);
            var s = m.CStr(p);
            if (c == 0) return p + (uint)s.Length;
            int i = Array.IndexOf(s, c);
            return i < 0 ? 0u : p + (uint)i;
        };
        L["strrchr"] = m =>
        {
            uint p = m.Reg(0); byte c = (byte)m.Reg(1);
            var s = m.CStr(p);
            if (c == 0) return p + (uint)s.Length;
            int i = Array.LastIndexOf(s, c);
            return i < 0 ? 0u : p + (uint)i;
        };
        L["strcpy"] = m =>
        {
            var s = m.CStr(m.Reg(1));
            m.Write(m.Reg(0), s); m.W8(m.Reg(0) + (uint)s.Length, 0);
            return m.Reg(0);
        };
        L["strncpy"] = m =>
        {
            uint n = m.Reg(2);
            var s = m.CStr(m.Reg(1));
            int k = (int)Math.Min((uint)s.Length, n);
            m.Write(m.Reg(0), s.AsSpan(0, k));
            m.Fill(m.Reg(0) + (uint)k, n - (uint)k, 0);
            return m.Reg(0);
        };
        L["strstr"] = m =>
        {
            var h = m.CStr(m.Reg(0), 1 << 20);
            int i = h.AsSpan().IndexOf(m.CStr(m.Reg(1)));
            return i < 0 ? 0u : m.Reg(0) + (uint)i;
        };

        foreach (var nm in new[] { "open", "stat", "fstat", "mkstemp", "dladdr", "closedir", "close", "read" })
            L[nm] = _ => M32;
        foreach (var nm in new[] { "getenv", "opendir", "fopen", "dlopen", "dlsym", "readdir" })
            L[nm] = _ => 0;

        L["abort"] = _ => throw new EmulationAbort("abort() called");
        L["__stack_chk_fail"] = _ => throw new EmulationAbort("__stack_chk_fail");
        L["__errno"] = m => m.ErrnoAddress;
        L["__cxa_throw"] = m =>
        {
            string name = "";
            try { name = Encoding.Latin1.GetString(m.CStr(m.R32(m.Reg(1) + 4))); } catch (EmulationAbort) { }
            throw new EmulatedCxxThrow(name);
        };
        L["__cxa_allocate_exception"] = m => m.Malloc((ulong)m.Reg(0) + 128) + 128;

        L["__android_log_print"] = m => { m.Log.Add($"android_log {Lat(m.CStr(m.Reg(1)))} {m.Format(m.CStr(m.Reg(2)), 3)}"); return 0; };
        L["fprintf"] = m => { m.Log.Add("fprintf " + m.Format(m.CStr(m.Reg(1)), 2)); return 0; };
        L["printf"] = m => { m.Log.Add("printf " + m.Format(m.CStr(m.Reg(0)), 1)); return 0; };
        L["snprintf"] = m => SnWrite(m, m.Format(m.CStr(m.Reg(2)), 3));
        L["vsnprintf"] = m => SnWrite(m, m.Format(m.CStr(m.Reg(2)), 0, m.Reg(3)));
        L["vfprintf"] = m => { m.Log.Add("vfprintf " + m.Format(m.CStr(m.Reg(1)), 0, m.Reg(2))); return 0; };
        L["sprintf"] = m =>
        {
            var s = Encoding.Latin1.GetBytes(m.Format(m.CStr(m.Reg(1)), 2));
            m.Write(m.Reg(0), s); m.W8(m.Reg(0) + (uint)s.Length, 0);
            return (uint)s.Length;
        };

        // setjmp / longjmp: r4-r11, sp, lr and d8-d15 in a host side table keyed by the buffer address
        L["setjmp"] = m =>
        {
            var core = new uint[10];
            for (int i = 0; i < 8; i++) core[i] = (uint)m.ReadReg(UC_ARM_REG_R4 + i);
            core[8] = m.Sp; core[9] = m.Lr;
            var d = new ulong[8];
            for (int i = 0; i < 8; i++) d[i] = m.ReadReg(UC_ARM_REG_D8 + i);
            m._jmpbufs[m.Reg(0)] = (core, d);
            return 0;
        };
        L["_setjmp"] = L["setjmp"];
        L["longjmp"] = m =>
        {
            uint buf = m.Reg(0), val = m.Reg(1);
            if (!m._jmpbufs.TryGetValue(buf, out var s)) throw new EmulationAbort($"longjmp to an unknown buffer 0x{buf:X}");
            for (int i = 0; i < 8; i++) m.WriteReg(UC_ARM_REG_R4 + i, s.Core[i]);
            for (int i = 0; i < 8; i++) m.WriteReg(UC_ARM_REG_D8 + i, s.D[i]);
            m.WriteReg(UC_ARM_REG_SP, s.Core[8]);
            m.SetReg(0, val != 0 ? val : 1);
            m.SetPc(s.Core[9]);
            return StubResult.NoReturn;
        };
        L["_longjmp"] = L["longjmp"];

        L["pthread_key_create"] = m => { m.W32(m.Reg(0), m._nextKey++); return 0; };
        L["pthread_key_delete"] = _ => 0;
        L["pthread_getspecific"] = m => m._tls.TryGetValue(m.Reg(0), out var v) ? v : 0u;
        L["pthread_setspecific"] = m => { m._tls[m.Reg(0)] = m.Reg(1); return 0; };
        L["pthread_once"] = m =>
        {
            uint ctl = m.Reg(0), fn = m.Reg(1);
            if (m.R32(ctl) == 0)
            {
                m.W32(ctl, 1);
                m.SetPc(fn);      // the init routine returns to our caller
                return StubResult.NoReturn;
            }
            return 0;
        };

        // integer helpers from libgcc
        L["__aeabi_idiv"] = m => (uint)TruncDiv(S32(m.Reg(0)), S32(m.Reg(1)));
        L["__aeabi_uidiv"] = m => m.Reg(1) != 0 ? m.Reg(0) / m.Reg(1) : 0u;
        L["__aeabi_idivmod"] = m =>
        {
            long a = S32(m.Reg(0)), b = S32(m.Reg(1));
            long q = TruncDiv(a, b);
            m.SetReg(1, (uint)(a - q * b));
            return (uint)q;
        };
        L["__aeabi_uidivmod"] = m =>
        {
            uint a = m.Reg(0), b = m.Reg(1);
            if (b == 0) { m.SetReg(1, 0); return 0; }
            m.SetReg(1, a % b);
            return a / b;
        };
        L["__aeabi_ldivmod"] = m =>
        {
            long a = (long)(((ulong)m.Reg(1) << 32) | m.Reg(0)), b = (long)(((ulong)m.Reg(3) << 32) | m.Reg(2));
            Int128 q = TruncDiv128(a, b), r = a - q * b;
            m.SetReg(1, (uint)((ulong)(long)q >> 32));
            m.SetReg(2, (uint)(long)r);
            m.SetReg(3, (uint)((ulong)(long)r >> 32));
            return (uint)(long)q;
        };
        L["__aeabi_uldivmod"] = m =>
        {
            ulong a = ((ulong)m.Reg(1) << 32) | m.Reg(0), b = ((ulong)m.Reg(3) << 32) | m.Reg(2);
            ulong q = b != 0 ? a / b : 0, r = b != 0 ? a % b : 0;
            m.SetReg(1, (uint)(q >> 32));
            m.SetReg(2, (uint)r);
            m.SetReg(3, (uint)(r >> 32));
            return (uint)q;
        };
        L["__aeabi_l2d"] = m => { m.SetDbl((long)(((ulong)m.Reg(1) << 32) | m.Reg(0))); return StubResult.Void; };
        L["__aeabi_ul2d"] = m => { m.SetDbl(((ulong)m.Reg(1) << 32) | m.Reg(0)); return StubResult.Void; };
        L["__aeabi_ui2d"] = m => { m.SetDbl(m.Reg(0)); return StubResult.Void; };
        L["__aeabi_dsub"] = m => { m.SetDbl(Dbl(m.Reg(0), m.Reg(1)) - Dbl(m.Reg(2), m.Reg(3))); return StubResult.Void; };

        // libm (softfp: doubles in r0:r1, r2:r3; floats in r0). Host arithmetic, as in the Python; a domain or range error
        // stops the run (Python's math module raises there).
        foreach (var (nm, fn) in new (string, Func<double, double>)[]
                 {
                     ("sqrt", Math.Sqrt), ("floor", Math.Floor), ("ceil", Math.Ceiling), ("exp", Math.Exp), ("log", Math.Log),
                     ("log10", Math.Log10), ("sin", Math.Sin), ("cos", Math.Cos), ("acos", Math.Acos), ("asin", Math.Asin),
                 })
        {
            var f = fn; var n = nm;
            L[nm] = m => { m.SetDbl(MathChecked(n, f, Dbl(m.Reg(0), m.Reg(1)))); return StubResult.Void; };
        }
        L["pow"] = m =>
        {
            double x = Dbl(m.Reg(0), m.Reg(1)), y = Dbl(m.Reg(2), m.Reg(3)), r = Math.Pow(x, y);
            if ((double.IsNaN(r) && !double.IsNaN(x) && !double.IsNaN(y)) || (double.IsInfinity(r) && double.IsFinite(x) && double.IsFinite(y)))
                throw new EmulationAbort($"pow({x}, {y}): math domain/range error");
            m.SetDbl(r);
            return StubResult.Void;
        };
        L["sqrtf"] = m =>
        {
            double x = BitConverter.UInt32BitsToSingle(m.Reg(0));
            return BitConverter.SingleToUInt32Bits((float)MathChecked("sqrtf", Math.Sqrt, x));
        };
    }

    private static long TruncDiv(long a, long b)
    {
        if (b == 0) return 0;
        long q = (long)(Int128.Abs(a) / Int128.Abs(b));
        return (a < 0) == (b < 0) ? q : -q;
    }

    private static Int128 TruncDiv128(long a, long b)
    {
        if (b == 0) return 0;
        Int128 q = Int128.Abs(a) / Int128.Abs(b);
        return (a < 0) == (b < 0) ? q : -q;
    }

    private static double MathChecked(string name, Func<double, double> f, double x)
    {
        double r = f(x);
        if ((double.IsNaN(r) && !double.IsNaN(x)) || (double.IsInfinity(r) && double.IsFinite(x)))
            throw new EmulationAbort($"{name}({x}): math domain/range error");
        return r;
    }

    private static StubResult SnWrite(ArmMachine m, string text)
    {
        var s = Encoding.Latin1.GetBytes(text);
        uint n = m.Reg(1);
        if (n != 0)
        {
            int k = (int)Math.Min((uint)s.Length, n - 1);
            m.Write(m.Reg(0), s.AsSpan(0, k));
            m.W8(m.Reg(0) + (uint)k, 0);
        }
        return (uint)s.Length;
    }

    internal static string Lat(byte[] b) => Encoding.Latin1.GetString(b);

    // ---- printf subset (emu_elf.Machine.fmt)

    private static readonly Regex FmtRe = new(@"%([-+ #0]*)(\*|\d+)?(?:\.(\*|\d+))?(hh|h|ll|l|z|j|t)?([diuxXcsfgeEp%])", RegexOptions.CultureInvariant);

    /// <summary>Renders a printf format; arguments come from r0..r3/stack from index <paramref name="argn"/>, or from a
    /// va_list pointer <paramref name="ap"/>.</summary>
    public string Format(byte[] fmt, int argn, uint? ap = null)
    {
        uint Next()
        {
            if (ap is uint p) { uint v = R32(p); ap = p + 4; return v; }
            return Arg(argn++);
        }
        string s = Lat(fmt);
        var res = new StringBuilder();
        int pos = 0;
        foreach (Match mt in FmtRe.Matches(s))
        {
            res.Append(s, pos, mt.Index - pos);
            pos = mt.Index + mt.Length;
            string flags = mt.Groups[1].Value, conv = mt.Groups[5].Value;
            string? width = mt.Groups[2].Success ? mt.Groups[2].Value : null;
            string? prec = mt.Groups[3].Success ? mt.Groups[3].Value : null;
            if (conv == "%") { res.Append('%'); continue; }
            if (width == "*") width = Next().ToString(CultureInfo.InvariantCulture);
            if (prec == "*") prec = Next().ToString(CultureInfo.InvariantCulture);
            int w = width is null ? 0 : int.Parse(width, CultureInfo.InvariantCulture);
            switch (conv[0])
            {
                case 'd': case 'i':
                    res.Append(PyInt(flags, w, 'd', S32(Next())));
                    break;
                case 'u': case 'x': case 'X':
                    res.Append(PyInt(flags, w, conv[0], Next()));
                    break;
                case 'c':
                    res.Append((char)(Next() & 0xFF));
                    break;
                case 's':
                    {
                        uint p = Next();
                        res.Append(p != 0 ? Lat(CStr(p)) : "(null)");
                        break;
                    }
                case 'p':
                    res.Append("0x").Append(Next().ToString("x", CultureInfo.InvariantCulture));
                    break;
                default:
                    {
                        if (ap is uint p) ap = (p + 7) & ~7u;
                        else if (argn % 2 == 1) argn++;
                        uint lo = Next(), hi = Next();
                        res.Append(PyFloat(flags, w, prec is null ? 6 : int.Parse(prec, CultureInfo.InvariantCulture), conv[0], Dbl(lo, hi)));
                        break;
                    }
            }
        }
        res.Append(s, pos, s.Length - pos);
        return res.ToString();
    }

    /// <summary>Python's <c>'%' + flags + width + conv</c> for an integer (u is d; '#' gives 0x/0X even for zero).</summary>
    private static string PyInt(string flags, int width, char conv, long v)
    {
        bool neg = v < 0;
        ulong mag = neg ? (ulong)(-v) : (ulong)v;
        string digits = conv switch
        {
            'x' => mag.ToString("x", CultureInfo.InvariantCulture),
            'X' => mag.ToString("X", CultureInfo.InvariantCulture),
            _ => mag.ToString(CultureInfo.InvariantCulture),
        };
        string sign = neg ? "-" : flags.Contains('+') ? "+" : flags.Contains(' ') ? " " : "";
        string prefix = flags.Contains('#') && conv is 'x' or 'X' ? (conv == 'x' ? "0x" : "0X") : "";
        return Pad(flags, width, sign + prefix, digits);
    }

    private static string Pad(string flags, int width, string head, string body)
    {
        int len = head.Length + body.Length;
        if (len >= width) return head + body;
        if (flags.Contains('-')) return head + body + new string(' ', width - len);
        if (flags.Contains('0')) return head + new string('0', width - len) + body;
        return new string(' ', width - len) + head + body;
    }

    /// <summary>C/Python formatting of a double for f, e, E, g.</summary>
    private static string PyFloat(string flags, int width, int prec, char conv, double d)
    {
        bool neg = double.IsNegative(d) && !double.IsNaN(d);
        double a = Math.Abs(d);
        string body;
        if (double.IsNaN(d)) { body = "nan"; neg = false; }
        else if (double.IsInfinity(d)) body = "inf";
        else
        {
            bool alt = flags.Contains('#');
            switch (conv)
            {
                case 'f':
                    body = a.ToString("F" + prec, CultureInfo.InvariantCulture);
                    if (alt && prec == 0) body += ".";
                    break;
                case 'e': case 'E':
                    body = ExpForm(a, prec, conv, alt);
                    break;
                default:
                    {
                        int p = prec == 0 ? 1 : prec;
                        int x = a == 0 ? 0 : ExponentOf(a, p - 1);
                        body = p > x && x >= -4 ? a.ToString("F" + (p - 1 - x), CultureInfo.InvariantCulture) : ExpForm(a, p - 1, 'e', alt);
                        if (!alt) body = StripZeros(body);
                        else if (!body.Contains('.')) body = InsertPoint(body);
                        break;
                    }
            }
        }
        string sign = neg ? "-" : flags.Contains('+') ? "+" : flags.Contains(' ') ? " " : "";
        if (double.IsNaN(d) || double.IsInfinity(d)) return Pad(flags.Replace("0", ""), width, sign, body);
        return Pad(flags, width, sign, body);
    }

    private static int ExponentOf(double a, int digitsAfterPoint)
    {
        string e = a.ToString("E" + digitsAfterPoint, CultureInfo.InvariantCulture);
        return int.Parse(e[(e.IndexOf('E') + 1)..], CultureInfo.InvariantCulture);
    }

    private static string ExpForm(double a, int prec, char conv, bool alt)
    {
        string e = a.ToString("E" + prec, CultureInfo.InvariantCulture);
        int k = e.IndexOf('E');
        string mant = e[..k];
        if (alt && prec == 0) mant += ".";
        int exp = int.Parse(e[(k + 1)..], CultureInfo.InvariantCulture);
        return mant + (conv == 'E' ? "E" : "e") + (exp < 0 ? "-" : "+") + Math.Abs(exp).ToString("00", CultureInfo.InvariantCulture);
    }

    private static string StripZeros(string body)
    {
        int k = body.IndexOfAny(new[] { 'e', 'E' });
        string mant = k < 0 ? body : body[..k], tail = k < 0 ? "" : body[k..];
        if (mant.Contains('.')) mant = mant.TrimEnd('0').TrimEnd('.');
        return mant + tail;
    }

    private static string InsertPoint(string body)
    {
        int k = body.IndexOfAny(new[] { 'e', 'E' });
        return k < 0 ? body + "." : body[..k] + "." + body[k..];
    }

    // ---- data symbols (emu_elf.DATA_SYMBOLS)

    private static readonly Dictionary<string, Func<ArmMachine, uint>> DataSymbols = new(StringComparer.Ordinal)
    {
        ["__stack_chk_guard"] = m => { uint a = m.StaticAlloc(16); m.W32(a, 0x12345678); return a; },
        ["_ctype_"] = m =>
        {
            uint table = m.StaticAlloc(1024);
            m.W8(table, 0);
            for (int c = 0; c < 256; c++) m.W8(table + 1 + (uint)c, CtypeFlag(c));
            uint a = m.StaticAlloc(16);
            m.W32(a, table);
            return a;
        },
        ["_tolower_tab_"] = m => CaseTable(m, x => x >= 65 && x <= 90 ? x + 32 : x),
        ["_toupper_tab_"] = m => CaseTable(m, x => x >= 97 && x <= 122 ? x - 32 : x),
        ["__sF"] = m => m.StaticAlloc(0x400),
    };

    private static uint CaseTable(ArmMachine m, Func<int, int> f)
    {
        uint table = m.StaticAlloc(1024);
        m.W16(table, 0);
        for (int x = 0; x < 256; x++) m.W16(table + 2 + 2 * (uint)x, (uint)(ushort)f(x));
        uint a = m.StaticAlloc(16);
        m.W32(a, table);
        return a;
    }

    /// <summary>bionic _ctype_ flags: _U 1, _L 2, _N 4, _S 8, _P 0x10, _C 0x20, _X 0x40, _B 0x80.</summary>
    private static byte CtypeFlag(int c)
    {
        int f = 0;
        bool upper = c >= 'A' && c <= 'Z', lower = c >= 'a' && c <= 'z', digit = c >= '0' && c <= '9';
        if (upper) f |= 1;
        if (lower) f |= 2;
        if (digit) f |= 4;
        if (c is 9 or 10 or 11 or 12 or 13 or 32) f |= 8;
        if (c >= 33 && c <= 126 && !(upper || lower || digit)) f |= 0x10;
        if (c < 32 || c == 127) f |= 0x20;
        if (digit || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')) f |= 0x40;
        if (c == 32) f |= 0x80;
        return (byte)f;
    }
}
