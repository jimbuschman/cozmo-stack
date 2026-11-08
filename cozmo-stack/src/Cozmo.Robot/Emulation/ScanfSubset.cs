using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace Cozmo.Robot.Emulation;

/// <summary>
/// A C scanf subset for the sscanf stand-in, ported from the Acapela probe's <c>scanf_impl.scan</c>: whitespace, literals,
/// %d %i %u %x %s %c %f %e %g %[set] %n %%, with widths, * and the h/l/ll modifiers. Like the probe, %i reads decimal only and
/// a float is parsed as a double and then rounded to float for %f. Unlike the probe (Python's <c>str.isspace</c>), whitespace
/// is C's: tab, LF, VT, FF, CR and space. Anything else stops the run.
/// </summary>
public static class ScanfSubset
{
    public enum Kind { Int32, Int16, Int64, Float, Double, String, Chars }
    public readonly record struct Value(Kind Kind, BigInteger Integer, double Real, string Text);

    private static readonly Regex Dec = new(@"\G[+-]?[0-9]+", RegexOptions.CultureInvariant);
    private static readonly Regex Hex = new(@"\G[+-]?(0[xX])?[0-9a-fA-F]+", RegexOptions.CultureInvariant);
    private static readonly Regex Flt = new(@"\G[+-]?([0-9]+[.]?[0-9]*|[.][0-9]+)([eE][+-]?[0-9]+)?", RegexOptions.CultureInvariant);

    public static bool IsCSpace(char c) => c is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';

    /// <summary>Returns the conversion count (-1 for input failure before the first conversion) and the values to store,
    /// in order (%n included).</summary>
    public static (int Count, List<Value> Values) Scan(string src, string fmt)
    {
        int i = 0, j = 0, n = 0;
        var outv = new List<Value>();
        char F(int k) => k < fmt.Length ? fmt[k] : throw new EmulationAbort($"sscanf: format ends inside a conversion: {fmt}");
        while (j < fmt.Length)
        {
            char c = fmt[j];
            if (IsCSpace(c))
            {
                while (i < src.Length && IsCSpace(src[i])) i++;
                j++;
                continue;
            }
            if (c != '%')
            {
                if (i < src.Length && src[i] == c) { i++; j++; continue; }
                if (i == src.Length) return (n != 0 ? n : -1, outv);
                break;
            }
            j++;
            bool skip = false;
            var w = new StringBuilder();
            while (F(j) == '*' || char.IsAsciiDigit(F(j)))
            {
                if (fmt[j] == '*') skip = true;
                else w.Append(fmt[j]);
                j++;
            }
            int? width = w.Length > 0 ? int.Parse(w.ToString(), CultureInfo.InvariantCulture) : null;
            var mods = new StringBuilder();
            while ("hlLqjzt".Contains(F(j))) { mods.Append(fmt[j]); j++; }
            char conv = F(j);
            j++;
            string md = mods.ToString();
            if (conv == '%')
            {
                if (i < src.Length && src[i] == '%') { i++; continue; }
                if (i == src.Length) return (n != 0 ? n : -1, outv);
                break;
            }
            if (conv == 'n')
            {
                if (!skip) outv.Add(new Value(Kind.Int32, i, 0, ""));
                continue;
            }
            if (conv == '[')
            {
                bool neg = false;
                if (F(j) == '^') { neg = true; j++; }
                var st = new StringBuilder();
                if (F(j) == ']') { st.Append(']'); j++; }
                while (F(j) != ']') { st.Append(fmt[j]); j++; }
                j++;
                var set = new HashSet<char>();
                string ss = st.ToString();
                for (int k = 0; k < ss.Length;)
                {
                    if (k + 2 < ss.Length && ss[k + 1] == '-')
                    {
                        for (int o = ss[k]; o <= ss[k + 2]; o++) set.Add((char)o);
                        k += 3;
                    }
                    else { set.Add(ss[k]); k++; }
                }
                int s0 = i;
                if (i == src.Length) return (n != 0 ? n : -1, outv);
                while (i < src.Length && (set.Contains(src[i]) != neg) && (width is null || i - s0 < width)) i++;
                if (i == s0) break;
                if (!skip) { outv.Add(new Value(Kind.String, 0, 0, src[s0..i])); n++; }
                continue;
            }
            if (conv == 'c')
            {
                int wd = width is null or 0 ? 1 : width.Value;
                if (i == src.Length) return (n != 0 ? n : -1, outv);
                wd = Math.Min(wd, src.Length - i);
                if (!skip) { outv.Add(new Value(Kind.Chars, 0, 0, src.Substring(i, wd))); n++; }
                i += wd;
                continue;
            }
            while (i < src.Length && IsCSpace(src[i])) i++;
            if (i >= src.Length) return (n != 0 ? n : -1, outv);
            int start = i;
            int lim = width is null ? src.Length : Math.Min(src.Length, i + width.Value);
            string window = src[..lim];
            if (conv == 's')
            {
                while (i < lim && !IsCSpace(src[i])) i++;
                if (!skip) { outv.Add(new Value(Kind.String, 0, 0, src[start..i])); n++; }
                continue;
            }
            if (conv is 'd' or 'i' or 'u')
            {
                var mt = Dec.Match(window, i);
                if (!mt.Success) break;
                i += mt.Length;
                var k = md.Contains("ll") ? Kind.Int64 : md == "h" ? Kind.Int16 : Kind.Int32;
                if (!skip) { outv.Add(new Value(k, BigInteger.Parse(mt.Value, CultureInfo.InvariantCulture), 0, "")); n++; }
                continue;
            }
            if (conv is 'x' or 'X')
            {
                var mt = Hex.Match(window, i);
                if (!mt.Success) break;
                i += mt.Length;
                if (!skip) { outv.Add(new Value(Kind.Int32, ParseHex(mt.Value), 0, "")); n++; }
                continue;
            }
            if (conv is 'f' or 'e' or 'E' or 'g' or 'G')
            {
                var mt = Flt.Match(window, i);
                if (!mt.Success) break;
                i += mt.Length;
                if (!skip)
                {
                    outv.Add(new Value(md.Contains('l') ? Kind.Double : Kind.Float, 0,
                        double.Parse(mt.Value, NumberStyles.Float, CultureInfo.InvariantCulture), ""));
                    n++;
                }
                continue;
            }
            throw new EmulationAbort($"sscanf: conversion %{conv} is not modelled");
        }
        return (n, outv);
    }

    private static BigInteger ParseHex(string s)
    {
        bool neg = s.StartsWith('-');
        string t = s.TrimStart('+', '-');
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        var v = BigInteger.Parse("0" + t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        return neg ? -v : v;
    }
}
