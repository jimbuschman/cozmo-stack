using System.Text;

namespace Cozmo.Protocol;

public static class Hex
{
    public static string Dump(ReadOnlySpan<byte> b, int max = int.MaxValue)
    {
        var sb = new StringBuilder();
        int n = Math.Min(b.Length, max);
        for (int i = 0; i < n; i++) { if (i > 0) sb.Append(' '); sb.Append(b[i].ToString("x2")); }
        if (n < b.Length) sb.Append($" …(+{b.Length - n})");
        return sb.ToString();
    }

    /// <summary>Accepts "0a 0b", "0a0b", "\\x0a\\x0b", "0x0a,0x0b" and Python bytes literals like b'COZ\x03RE\x01'.</summary>
    public static byte[] Parse(string s)
    {
        s = s.Trim();
        if ((s.StartsWith("b'") && s.EndsWith("'")) || (s.StartsWith("b\"") && s.EndsWith("\"")))
            return ParsePythonBytes(s[2..^1]);
        var clean = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length && (s[i + 1] == 'x' || s[i + 1] == 'X')) { i++; continue; }
            if (c == '0' && i + 1 < s.Length && (s[i + 1] == 'x' || s[i + 1] == 'X')) { i++; continue; }
            if (Uri.IsHexDigit(c)) clean.Append(c);
            else if (c is ' ' or ',' or ':' or '-' or '\n' or '\r' or '\t') continue;
            else throw new FormatException($"unexpected character '{c}' in hex input");
        }
        if (clean.Length % 2 != 0) throw new FormatException("odd number of hex digits");
        var out_ = new byte[clean.Length / 2];
        for (int i = 0; i < out_.Length; i++) out_[i] = Convert.ToByte(clean.ToString(i * 2, 2), 16);
        return out_;
    }

    private static byte[] ParsePythonBytes(string body)
    {
        var o = new List<byte>();
        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (c != '\\') { o.Add((byte)c); continue; }
            char n = body[++i];
            switch (n)
            {
                case 'x': o.Add(Convert.ToByte(body.Substring(i + 1, 2), 16)); i += 2; break;
                case 'n': o.Add((byte)'\n'); break;
                case 'r': o.Add((byte)'\r'); break;
                case 't': o.Add((byte)'\t'); break;
                case '0': o.Add(0); break;
                case '\\': o.Add((byte)'\\'); break;
                case '\'': o.Add((byte)'\''); break;
                case '"': o.Add((byte)'"'); break;
                default: throw new FormatException($"unknown escape \\{n}");
            }
        }
        return o.ToArray();
    }

    /// <summary>Byte-by-byte comparison; returns human-readable list of differing offsets (empty when identical).</summary>
    public static List<string> Diff(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, string nameA = "A", string nameB = "B")
    {
        var res = new List<string>();
        int n = Math.Max(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            string va = i < a.Length ? a[i].ToString("x2") : "--";
            string vb = i < b.Length ? b[i].ToString("x2") : "--";
            if (va != vb) res.Add($"offset {i,5} (0x{i:x4}): {nameA}={va} {nameB}={vb}  [{FieldHint(i)}]");
        }
        if (a.Length != b.Length) res.Add($"length: {nameA}={a.Length} {nameB}={b.Length}");
        return res;
    }

    private static string FieldHint(int off) => off switch
    {
        < 4 => "udp prefix COZ\\x03",
        < 7 => "reliable prefix RE\\x01",
        7 => "frame type",
        8 or 9 => "seqMin",
        10 or 11 => "seqMax",
        12 or 13 => "ack (seqLastReceived)",
        _ => "body",
    };
}
