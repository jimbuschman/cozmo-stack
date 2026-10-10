using System.Text;

namespace Cozmo.Robot;

// fidelity: M3-041
/// <summary>
/// The number text the IMU logger's stream insertions produce: the packaged libc++_shared.so printf, not .NET's G6.
/// libc++ num_put formats "%.*g" (double, 0x4A3D8 __format_float, default floatfield) and "%ld" (long, 0x49AD4
/// __format_int) and calls __libcpp_snprintf_l 0x49B34, which calls the shipped vsnprintf 0x82528, whose core is the
/// printf core 0x813B0 (L27, L28, L30). to_string(unsigned) (0x660E8, format "%u") reaches the same core through 0x7E270.
///
/// This is a port of that core's decimal path, written from its instructions and checked against the engine's own code
/// run under re-analysis/tools/emu/emu_libcxx_printf.py (fixture Fixtures/m3_041_libcxx_printf_oracle.json). It is NOT
/// correctly rounded in every case: the shipped core is an older musl-style fmt_fp, and the oracle found these
/// differences from an exact conversion, each kept here:
///   - the rounding quotient parity at a limb boundary ignores the previous limb (999999.5 prints 999999, 100001.5
///     prints 100001): 0x81FEE..0x82000;
///   - the digit expansion keeps only p/9+2 limbs while shifting right (0x8198E..0x81A2A), so some wide-precision
///     digits are truncated rather than rounded;
///   - the trailing-zero limb strip stops at the leading limb (0x81CA0..0x81CB6) and is skipped when no rounding
///     block runs (0x81A98).
/// Only what the logger reaches is ported: conversion g with no flag, width 0 and a precision (the stream default is 6;
/// a negative precision selects 6, 0x816F2..0x816FA); conversions e, f, a, o, x, c, s, p and field padding are never
/// requested by a handler (the stream's flags stay 0x1002, width 0).
/// </summary>
internal static class LibcxxPrintf
{
    /// <summary>"%ld" of a 32-bit long (signed, sign-extended to 64 bits, 0x81552..0x81AEC, digits 0x8267C).</summary>
    internal static string FormatLong(int value)
    {
        long x = value;
        bool negative = x < 0;
        ulong magnitude = negative ? (ulong)(-x) : (ulong)x;
        string digits = magnitude == 0 ? "0" : Digits(magnitude);
        return negative ? "-" + digits : digits;
    }

    /// <summary>"%u" (to_string(unsigned), 0x660E8 format at 0x661AC).</summary>
    internal static string FormatUnsigned(uint value) => value == 0 ? "0" : Digits(value);

    /// <summary>The float insertion: the engine widens f32 to f64 (0x4E37A4..0x4E37B6) and num_put prints "%.*g" at precision 6.</summary>
    internal static string FormatFloat(float value) => FormatG((double)value, DefaultPrecision);

    /// <summary>The stream's initial precision (L2).</summary>
    internal const int DefaultPrecision = 6;

    // The printf core's constants as the engine holds them: d10 = 2^28 (0x41B0000000000000), d11 = 1e9
    // (0x41CDCD6500000000) from the literals at 0x81790/0x81798, and the rounding probe 2^53 / 2^53+2 (0x824D8, 0x824D0).
    private static readonly double Two28 = BitConverter.Int64BitsToDouble(0x41B0000000000000);
    private static readonly double OneE9 = BitConverter.Int64BitsToDouble(0x41CDCD6500000000);
    private static readonly double RoundEven = BitConverter.Int64BitsToDouble(0x4340000000000000);
    private static readonly double RoundOdd = BitConverter.Int64BitsToDouble(0x4340000000000001);

    private static string Digits(ulong v)
    {
        var chars = new char[20];
        int i = chars.Length;
        while (v != 0) { chars[--i] = (char)('0' + (int)(v % 10)); v /= 10; }
        return new string(chars, i, chars.Length - i);
    }

    private static string DigitsOrEmpty(uint v) => v == 0 ? "" : Digits(v);   // fmt_u: zero gives no digit (0x8267C)

    // ---- frexp 0x835D6 -> 0x84DF0 (exact bit-field implementation) -----------------------------------------------
    private static double Frexp(double x, out int exp)
    {
        long bits = BitConverter.DoubleToInt64Bits(x);
        int ee = (int)((bits >> 52) & 0x7FF);
        if (ee == 0)
        {
            if (x != 0)
            {
                x = Frexp(x * 18446744073709551616.0, out exp);
                exp -= 64;
                return x;
            }
            exp = 0;
            return x;
        }
        exp = ee - 1022;
        bits = (bits & unchecked((long)0x800FFFFFFFFFFFFFUL)) | (1022L << 52);
        return BitConverter.Int64BitsToDouble(bits);
    }

    /// <summary>"%.*g" with these arguments: the shipped printf core, decimal path (0x815AC..0x82060, 0x81CB8..0x81FD2).</summary>
    internal static string FormatG(double y, int precision)
    {
        var sb = new StringBuilder();
        int p = precision;

        // (a) __signbit: the sign is dropped from y and printed as "-" (no '+' or ' ' flag).
        bool pl = double.IsNegative(y);
        if (pl) y = -y;

        // (b) non-finite: "inf" / "nan" (lower case for conversion g); a NaN drops its sign (0x81694).
        if (!double.IsFinite(y))
        {
            if (double.IsNaN(y)) { pl = false; return "nan"; }
            return pl ? "-inf" : "inf";
        }

        // (c) frexp, y = 2*mantissa, e2 -= 1 for a nonzero value.
        y = Frexp(y, out int e2) * 2;
        if (y != 0) e2--;

        // (d) a negative precision is 6; y *= 2^28, e2 -= 28.
        if (p < 0) p = 6;
        if (y != 0) { y *= Two28; e2 -= 28; }

        // Base-1e9 limbs. Index space is generous in both directions: the carry out of the first limb writes below r.
        var big = new uint[2400];
        int r = 1000;
        int a = r, z = r;

        // (e) expansion of y into limbs: x = (uint32)y; y = 1e9 * (y - x) until y == 0 (0x818C6..0x818E6).
        do
        {
            uint v = (uint)y;
            big[z++] = v;
            y = OneE9 * (y - v);
        } while (y != 0);

        while (e2 > 0)
        {
            uint carry = 0;
            int sh = Math.Min(29, e2);
            for (int d = z - 1; d >= a; d--)
            {
                ulong x = ((ulong)big[d] << sh) + carry;
                big[d] = (uint)(x % 1000000000UL);
                carry = (uint)(x / 1000000000UL);
            }
            if (carry != 0) big[--a] = carry;
            while (z > a && big[z - 1] == 0) z--;
            e2 -= sh;
        }
        while (e2 < 0)
        {
            uint carry = 0;
            int sh = Math.Min(9, -e2);
            int need = p / 9 + 2;                       // 0x8198E..0x819A2: p/9 + 2 limbs
            for (int d = a; d < z; d++)
            {
                uint rm = big[d] & ((1u << sh) - 1);
                big[d] = (big[d] >> sh) + carry;
                carry = (1000000000u >> sh) * rm;
            }
            if (big[a] == 0) a++;
            if (carry != 0) big[z++] = carry;
            int b = a;                                  // conversion g: b = a (0x81A14..0x81A1A)
            if (z - b > need) z = b + need;
            e2 += sh;
        }

        // (f) the decimal exponent of the top limb.
        int e;
        if (a < z)
        {
            uint i = 10;
            e = 9 * (r - a);
            while (big[a] >= i) { i *= 10; e++; }
        }
        else e = 0;

        // (g) rounding: j is the precision after the radix.
        int j = p - e - (p != 0 ? 1 : 0);
        if (j < 9 * (z - r - 1))
        {
            int d = r + 1 + ((j + 9216) / 9 - 1024);
            int jj = (j + 9216) % 9;
            uint i = 10;
            for (jj++; jj < 9; i *= 10, jj++) { }
            uint x = big[d] % i;
            if (x != 0 || d + 1 != z)
            {
                double small;
                double round = ((big[d] / i) & 1) != 0 ? RoundOdd : RoundEven;
                if (x < i / 2) small = 0.5;
                else if (x == i / 2 && d + 1 == z) small = 1.0;
                else small = 1.5;
                if (pl) { round = -round; small = -small; }
                big[d] -= x;
                if (round + small != round)
                {
                    big[d] += i;
                    if (big[d] > 999999999)
                    {
                        // Carry loop, engine 0x821E4..0x821F0: when the carry leaves the leading limb it reads the limb
                        // below `a`. That word is uninitialised stack on the phone; this port reads 0 (the zero-filled
                        // array), which matches the emulator oracle with a zeroed stack. With residue there, e.g. f32
                        // 999999552 prints differently.
                        int q = d - 1;
                        uint v;
                        do
                        {
                            v = big[q] + 1;
                            big[q] = v;
                            big[q + 1] = 0;
                            q--;
                        } while (v > 999999999);
                        int top = q + 1;
                        if (top < a) a = top;
                    }
                    if (a < z)
                    {
                        uint i2 = 10;
                        e = 9 * (r - a);
                        while (big[a] >= i2) { i2 *= 10; e++; }
                    }
                    else e = 0;
                }
            }
            // 0x81C90..0x81CB6: z = min(z, d + 1), then the strip loop that stops at the leading limb.
            if (z > d + 1) z = d + 1;
            for (;;)
            {
                int q = z - 1;
                uint v = big[q];
                z = q;
                if (q <= a || v != 0) break;
            }
            z += 1;
        }

        // (h) the %g choice and trailing-zero removal (no '#' flag).
        int P = p == 0 ? 1 : p;
        bool fStyle;
        if (P > e && e >= -4) { fStyle = true; p = P - 1 - e; }
        else { fStyle = false; p = P - 1; }
        int jz;
        if (z > a && big[z - 1] != 0)
        {
            jz = 0;
            uint v = big[z - 1];
            if (v % 10 == 0)
            {
                uint ii = 10;
                do { ii *= 10; jz++; } while (v % ii == 0);
            }
        }
        else jz = 9;
        int count = 9 * (z - r - 1) + (fStyle ? 0 : e) - jz;
        if (count < 0) count = 0;
        if (p > count) p = count;

        if (pl) sb.Append('-');
        if (fStyle)
        {
            int a2 = a > r ? r : a;
            int d = a2;
            for (; d <= r; d++)
            {
                string s = DigitsOrEmpty(big[d]);
                if (d != a2) s = s.PadLeft(9, '0');
                else if (s.Length == 0) s = "0";
                sb.Append(s);
            }
            if (p != 0) sb.Append('.');
            for (; d < z && p > 0; d++, p -= 9)
            {
                string s = DigitsOrEmpty(big[d]).PadLeft(9, '0');
                sb.Append(s, 0, Math.Min(9, p));
            }
            if (p > 0) sb.Append('0', p);                 // pad(f, '0', p + 9, 9)
        }
        else
        {
            if (z <= a) z = a + 1;
            for (int d = a; d < z && p >= 0; d++)
            {
                string s = DigitsOrEmpty(big[d]);
                if (s.Length == 0) s = "0";
                if (d != a) s = s.PadLeft(9, '0');
                else
                {
                    sb.Append(s[0]);
                    s = s.Substring(1);
                    if (p > 0) sb.Append('.');
                }
                sb.Append(s, 0, Math.Min(s.Length, p));
                p -= s.Length;
            }
            if (p > 0) sb.Append('0', p);                 // pad(f, '0', p + 18, 18)
            string exp = Digits((ulong)Math.Abs(e)).PadLeft(2, '0');
            sb.Append('e').Append(e < 0 ? '-' : '+').Append(exp);
        }
        return sb.ToString();
    }
}
