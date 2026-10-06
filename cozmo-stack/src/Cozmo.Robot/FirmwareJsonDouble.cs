// Numerical port of David M. Gay / Android support strtod; shipped binary ordering wins.
/****************************************************************
 *
 * The author of this software is David M. Gay.
 *
 * Copyright (c) 1991 by AT&T.
 *
 * Permission to use, copy, modify, and distribute this software for any
 * purpose without fee is hereby granted, provided that this entire notice
 * is included in all copies of any software which is or includes a copy
 * or modification of this software and in all copies of the supporting
 * documentation for such software.
 *
 * THIS SOFTWARE IS BEING PROVIDED "AS IS", WITHOUT ANY EXPRESS OR IMPLIED
 * WARRANTY.  IN PARTICULAR, NEITHER THE AUTHOR NOR AT&T MAKES ANY
 * REPRESENTATION OR WARRANTY OF ANY KIND CONCERNING THE MERCHANTABILITY
 * OF THIS SOFTWARE OR ITS FITNESS FOR ANY PARTICULAR PURPOSE.
 *
 ***************************************************************/


using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Cozmo.Robot;

// fidelity: M1-029
// Adopted 20261005-M1-029-converter-build-rows.md S1-S21/W1.
// Runtime assumption: nearest rounding and C locale (operator, 2026-10-05).
internal static class FirmwareJsonDouble
{
    private const ulong Fraction = 0x000FFFFFFFFFFFFFUL;
    private const ulong Exponent = 0x7FF0000000000000UL;
    private const ulong Maximum = 0x7FEFFFFFFFFFFFFFUL;
    private static readonly double[] Tens = Make(new ulong[] {
        0x3FF0000000000000,0x4024000000000000,0x4059000000000000,0x408F400000000000,
        0x40C3880000000000,0x40F86A0000000000,0x412E848000000000,0x416312D000000000,
        0x4197D78400000000,0x41CDCD6500000000,0x4202A05F20000000,0x42374876E8000000,
        0x426D1A94A2000000,0x42A2309CE5400000,0x42D6BCC41E900000,0x430C6BF526340000,
        0x4341C37937E08000,0x4376345785D8A000,0x43ABC16D674EC800,0x43E158E460913D00,
        0x4415AF1D78B58C40,0x444B1AE4D6E2EF50,0x4480F0CF064DD592 });
    private static readonly double[] Large = Make(new ulong[] {
        0x4341C37937E08000,0x4693B8B5B5056E17,0x4D384F03E93FF9F5,0x5A827748F9301D32,0x75154FDD7F73BF3C });
    private static readonly double[] Tiny = Make(new ulong[] {
        0x3C9CD2B297D889BC,0x3949F623D5A8A733,0x32A50FFD44F4A73D,0x255BBA08CF8C979D,0x0AC8062864AC6F43 });
    private static double[] Make(ulong[] bits) => Array.ConvertAll(bits, FromBits);
    private static double FromBits(ulong bits) => BitConverter.Int64BitsToDouble(unchecked((long)bits));
    private static ulong Bits(double value) => unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
    // Keep separate native vmul/vadd operations; prevent contraction through the JIT.
    [MethodImpl(MethodImplOptions.NoInlining)] private static double Multiply(double a, double b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static double Add(double a, double b) => a + b;

    // Input is the Reader's decimal token after C-locale num_get normalization.
    // This is deliberately not a general raw strtod entry (hex/inf/nan never enter from that lexer).
    internal static bool TryConvert(ReadOnlySpan<byte> token, out double value)
    {
        value = 0;
        int p = 0;
        bool negative = token.Length > 0 && token[0] == '-';
        if (negative) p++;
        var digits = new StringBuilder();
        int fractionDigits = 0;
        while (p < token.Length && Digit(token[p])) digits.Append((char)token[p++]);
        if (p < token.Length && token[p] == '.')
        {
            p++;
            while (p < token.Length && Digit(token[p])) { digits.Append((char)token[p++]); fractionDigits++; }
            // Native nz postpones trailing fractional zeros, so they don't count in nd/nf.
            while (fractionDigits > 0 && digits.Length > 0 && digits[^1] == '0') { digits.Length--; fractionDigits--; }
        }
        int e = 0;
        if (p < token.Length && (token[p] == 'e' || token[p] == 'E'))
        {
            p++;
            bool minus = p < token.Length && token[p] == '-';
            if (p < token.Length && (token[p] == '+' || token[p] == '-')) p++;
            int begin = p;
            while (p < token.Length && Digit(token[p])) { e = Math.Min(19999, e * 10 + token[p++] - '0'); }
            if (p == begin) return false; // W1 endpoint mismatch, even if raw prefix converts.
            if (minus) e = -e;
        }
        if (p != token.Length) return false;
        string significant = digits.ToString().TrimStart('0');
        if (significant.Length == 0)
        {
            // Lexer gives bare '-' to integer decoding, not this entry.
            if (digits.Length == 0 && fractionDigits == 0 && token.IndexOf((byte)'0') < 0) return false;
            value = FromBits(negative ? 0x8000000000000000UL : 0); return true;
        }
        e -= fractionDigits;
        int nd = significant.Length, k = Math.Min(nd, 16);
        uint y = 0, z = 0;
        for (int i = 0; i < Math.Min(k, 9); i++) y = y * 10 + (uint)(significant[i] - '0');
        for (int i = 9; i < k; i++) z = z * 10 + (uint)(significant[i] - '0');
        double rv = y;
        if (k > 9) rv = Add(Multiply(Tens[k - 9], rv), z);
        bool success = true;
        if (nd <= 15 && e == 0) return Finish(rv, negative, true, out value);
        if (nd <= 15 && e > 0 && e <= 22) return Finish(Multiply(rv, Tens[e]), negative, true, out value);
        if (nd <= 15 && e < 0 && e >= -22) return Finish(rv / Tens[-e], negative, true, out value);
        if (nd <= 15 && e > 0 && e <= 22 + 15 - nd)
            return Finish(Multiply(Multiply(rv, Tens[15 - nd]), Tens[e - (15 - nd)]), negative, true, out value);
        int e1 = e + nd - k;
        if (e1 > 0)
        {
            if ((e1 & 15) != 0) rv = Multiply(rv, Tens[e1 & 15]);
            if ((e1 & ~15) != 0)
            {
                if ((e1 & ~15) > 308) return Finish(double.PositiveInfinity, negative, false, out value);
                int n = e1 >> 4, j = 0;
                for (; n > 1; n >>= 1, j++) if ((n & 1) != 0) rv = Multiply(rv, Large[j]);
                rv = Multiply(FromBits(Bits(rv) - 0x0350000000000000UL), Large[j]);
                ulong exponent = Bits(rv) & Exponent;
                if (exponent > 0x7CA0000000000000UL) return Finish(double.PositiveInfinity, negative, false, out value);
                rv = exponent > 0x7C90000000000000UL ? FromBits(Maximum) : FromBits(Bits(rv) + 0x0350000000000000UL);
            }
        }
        else if (e1 < 0)
        {
            int n = -e1;
            if ((n & 15) != 0) rv /= Tens[n & 15];
            if ((n & ~15) != 0)
            {
                n >>= 4;
                if (n >= 32) return Finish(0, negative, false, out value);
                int j = 0;
                for (; n > 1; n >>= 1, j++) if ((n & 1) != 0) rv = Multiply(rv, Tiny[j]);
                double before = rv;
                rv = Multiply(rv, Tiny[j]);
                if (rv == 0)
                {
                    rv = Multiply(Add(before, before), Tiny[j]);
                    if (rv == 0) return Finish(0, negative, false, out value);
                    rv = FromBits(1);
                }
            }
        }
        BigInteger decimalInteger = BigInteger.Zero;
        foreach (char c in significant) decimalInteger = decimalInteger * 10 + c - '0';
        for (;;)
        {
            ulong rb = Bits(rv);
            int encoded = (int)((rb & Exponent) >> 52);
            BigInteger bb = (rb & Fraction) | (encoded == 0 ? 0UL : 0x0010000000000000UL);
            int bbe = encoded == 0 ? -1074 : encoded - 1075;
            while (bb.IsEven) { bb >>= 1; bbe++; }
            int bbbits = (int)bb.GetBitLength();
            int bb2 = e >= 0 ? 0 : -e, bb5 = bb2;
            int bd2 = e >= 0 ? e : 0, bd5 = bd2;
            if (bbe >= 0) bb2 += bbe; else bd2 -= bbe;
            int bs2 = bb2;
            int j = bbe + bbbits - 1 < -1022 ? bbe + 1075 : 54 - bbbits;
            bb2 += j; bd2 += j;
            int common = Math.Min(Math.Min(bb2, bd2), bs2);
            if (common > 0) { bb2 -= common; bd2 -= common; bs2 -= common; }
            BigInteger bs = BigInteger.One, bd = decimalInteger;
            if (bb5 > 0) { bs *= BigInteger.Pow(5, bb5); bb *= bs; }
            if (bb2 > 0) bb <<= bb2;
            if (bd5 > 0) bd *= BigInteger.Pow(5, bd5);
            if (bd2 > 0) bd <<= bd2;
            if (bs2 > 0) bs <<= bs2;
            BigInteger delta = bb - bd;
            bool below = delta.Sign < 0;
            delta = BigInteger.Abs(delta);
            int compare = delta.CompareTo(bs);
            if (compare < 0)
            {
                if (!below && (rb & Fraction) == 0 && (delta << 1) > bs) rv = Predecessor(rb);
                break;
            }
            if (compare == 0)
            {
                if (below && (rb & Fraction) == Fraction) { rv = FromBits((rb & Exponent) + 0x0010000000000000UL); break; }
                if (!below && (rb & Fraction) == 0) { rv = Predecessor(rb); break; }
                if ((rb & 1) == 0) break;
                rv = below ? Add(rv, Ulp(rv)) : rv - Ulp(rv);
                if (rv == 0) success = false;
                break;
            }
            // C:7EE9E: normalized quotient BEFORE the saved exponent-word edits.
            double ratio = Approximate(delta) / Approximate(bs), aadj, step;
            if (ratio <= 2)
            {
                if (below) aadj = step = 1;
                else if ((rb & Fraction) != 0)
                {
                    if (rb == 1) return Finish(0, negative, false, out value);
                    aadj = 1; step = -1;
                }
                else { aadj = ratio < 1 ? FromBits(0x3FE0000000000000) : Multiply(ratio, FromBits(0x3FE0000000000000)); step = -aadj; }
            }
            else { aadj = Multiply(ratio, FromBits(0x3FE0000000000000)); step = below ? aadj : -aadj; }
            ulong oldExponent = rb & Exponent;
            if (oldExponent == 0x7FE0000000000000UL)
            {
                double scaled = FromBits(rb - 0x0350000000000000UL);
                rv = Add(scaled, Multiply(step, Ulp(scaled)));
                if ((Bits(rv) & Exponent) >= 0x7CA0000000000000UL)
                {
                    if (rb == Maximum) return Finish(double.PositiveInfinity, negative, false, out value);
                    rv = FromBits(Maximum); continue;
                }
                rv = FromBits(Bits(rv) + 0x0350000000000000UL);
            }
            else
            {
                if (oldExponent <= 0x0340000000000000UL && aadj >= 1) { step = (int)Add(aadj, FromBits(0x3FE0000000000000)); if (!below) step = -step; }
                rv = Add(rv, Multiply(step, Ulp(rv)));
            }
            if (oldExponent != (Bits(rv) & Exponent)) continue;
            double part = aadj - (int)aadj;
            if (!below && (Bits(rv) & Fraction) == 0)
            {
                if (part < FromBits(0x3FCFFFFF94A03595)) break;
            }
            else if (part < FromBits(0x3FDFFFFF94A03595) || part > FromBits(0x3FE0000035AFE535)) break;
        }
        return Finish(rv, negative, success, out value);
    }
    private static bool Digit(byte c) => c >= '0' && c <= '9';
    private static bool Finish(double magnitude, bool negative, bool success, out double value)
    { value = FromBits(Bits(magnitude) ^ (negative ? 0x8000000000000000UL : 0)); return success; }
    private static double Predecessor(ulong bits) => FromBits(((bits & Exponent) - 0x0010000000000000UL) | Fraction);
    private static double Approximate(BigInteger integer)
    {
        int bits = (int)integer.GetBitLength();
        ulong top = (ulong)(bits > 53 ? integer >> (bits - 53) : integer << (53 - bits));
        return FromBits(0x3FF0000000000000UL | (top & Fraction));
    }
    private static double Ulp(double value)
    {
        int exponent = (int)((Bits(value) & Exponent) >> 52);
        return exponent > 52 ? FromBits((ulong)(exponent - 52) << 52) : FromBits(1UL << Math.Max(0, exponent - 1));
    }
}
