namespace Cozmo.Protocol.Tests;

/// <summary>What a successful validation learned about a JPEG file.</summary>
public sealed class JpegInfo
{
    public int Width, Height, Components;
    public int McusDecoded;
    /// <summary>Bytes between the end of the entropy stream and the end-of-image marker.</summary>
    public int TrailingBytes;
    public override string ToString() => $"{Width}x{Height} comps={Components} mcus={McusDecoded} trailing={TrailingBytes}";
}

/// <summary>
/// A baseline JPEG structure checker and Huffman decoder, used to prove the camera really does produce a
/// decodable image rather than merely a plausible byte sequence.
///
/// It parses the markers, builds the Huffman tables the file declares and decodes every MCU of the entropy
/// stream. A wrong reconstructed header, a missed byte-stuffing byte or a lost chunk all make the Huffman
/// decode run off the rails, so a clean decode of exactly the expected number of MCUs is a real check.
/// Coefficients are decoded and discarded: this checks the bitstream, not the picture.
/// </summary>
public static class JpegCheck
{
    private sealed class Huff
    {
        public readonly int[] MinCode = new int[17], MaxCode = new int[17], ValPtr = new int[17];
        public byte[] Symbols = Array.Empty<byte>();

        public static Huff Build(ReadOnlySpan<byte> counts, byte[] symbols)
        {
            var h = new Huff { Symbols = symbols };
            int code = 0, k = 0;
            for (int l = 1; l <= 16; l++)
            {
                h.ValPtr[l] = k;
                h.MinCode[l] = code;
                int n = counts[l - 1];
                code += n; k += n;
                h.MaxCode[l] = n > 0 ? code - 1 : -1;
                code <<= 1;
            }
            return h;
        }
    }

    private sealed class BitReader
    {
        private readonly byte[] _d;
        private int _bits, _count;
        public int Pos;
        public BitReader(byte[] d, int start) { _d = d; Pos = start; }

        public int Bit()
        {
            if (_count == 0)
            {
                if (Pos >= _d.Length) throw new FormatException("entropy stream ended mid-image");
                byte b = _d[Pos++];
                if (b == 0xFF)
                {
                    byte next = Pos < _d.Length ? _d[Pos] : (byte)0xD9;
                    if (next == 0x00) Pos++;                         // stuffed byte, a real 0xFF datum
                    else throw new FormatException($"marker 0xFF{next:X2} inside the entropy stream at offset {Pos - 1}");
                }
                _bits = b; _count = 8;
            }
            _count--;
            return (_bits >> _count) & 1;
        }

        public int Bits(int n) { int v = 0; for (int i = 0; i < n; i++) v = (v << 1) | Bit(); return v; }
        public void Align() => _count = 0;
    }

    private static byte Decode(BitReader r, Huff h)
    {
        int code = 0;
        for (int l = 1; l <= 16; l++)
        {
            code = (code << 1) | r.Bit();
            if (h.MaxCode[l] >= 0 && code <= h.MaxCode[l])
                return h.Symbols[h.ValPtr[l] + code - h.MinCode[l]];
        }
        throw new FormatException("no Huffman code matched; the tables and the bitstream disagree");
    }

    /// <summary>Parses and fully Huffman-decodes the file. Throws <see cref="FormatException"/> if it is not decodable.</summary>
    public static JpegInfo Validate(byte[] j)
    {
        if (j.Length < 4 || j[0] != 0xFF || j[1] != 0xD8) throw new FormatException("missing start-of-image marker");
        var dc = new Huff?[4];
        var ac = new Huff?[4];
        var quant = new bool[4];
        int width = 0, height = 0;
        (int Id, int H, int V, int Tq)[] comps = Array.Empty<(int, int, int, int)>();
        (int Cs, int Td, int Ta)[] scan = Array.Empty<(int, int, int)>();
        int p = 2, entropy = -1;

        while (p < j.Length - 1)
        {
            if (j[p] != 0xFF) throw new FormatException($"expected a marker at offset {p}, found 0x{j[p]:X2}");
            while (p < j.Length && j[p] == 0xFF) p++;
            byte marker = j[p++];
            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) continue;
            if (marker == 0xD9) break;
            if (p + 2 > j.Length) throw new FormatException("truncated segment length");
            int len = (j[p] << 8) | j[p + 1];
            int seg = p + 2, end = p + len;
            if (end > j.Length) throw new FormatException($"segment 0x{marker:X2} runs past the end of the file");

            switch (marker)
            {
                case 0xDB:                                   // quantisation tables
                    while (seg < end)
                    {
                        int pq = j[seg] >> 4, tq = j[seg] & 15; seg++;
                        quant[tq] = true;
                        seg += pq == 0 ? 64 : 128;
                    }
                    break;
                case 0xC4:                                   // Huffman tables
                    while (seg < end)
                    {
                        int tc = j[seg] >> 4, th = j[seg] & 15; seg++;
                        var counts = j.AsSpan(seg, 16); seg += 16;
                        int total = 0;
                        foreach (var c in counts) total += c;
                        var syms = j[seg..(seg + total)]; seg += total;
                        var h = Huff.Build(counts, syms);
                        if (tc == 0) dc[th] = h; else ac[th] = h;
                    }
                    break;
                case 0xC0:                                   // baseline frame header
                    height = (j[seg + 1] << 8) | j[seg + 2];
                    width = (j[seg + 3] << 8) | j[seg + 4];
                    int n = j[seg + 5];
                    comps = new (int, int, int, int)[n];
                    for (int i = 0; i < n; i++)
                        comps[i] = (j[seg + 6 + i * 3], j[seg + 7 + i * 3] >> 4, j[seg + 7 + i * 3] & 15, j[seg + 8 + i * 3]);
                    break;
                case 0xC2:
                    throw new FormatException("progressive JPEG is not expected from the robot");
                case 0xDA:                                   // start of scan
                    int ns = j[seg];
                    scan = new (int, int, int)[ns];
                    for (int i = 0; i < ns; i++)
                        scan[i] = (j[seg + 1 + i * 2], j[seg + 2 + i * 2] >> 4, j[seg + 2 + i * 2] & 15);
                    entropy = end;
                    break;
            }
            if (entropy >= 0) break;
            p = end;
        }

        if (entropy < 0) throw new FormatException("no start-of-scan marker");
        if (width == 0 || height == 0) throw new FormatException("no frame header, or a zero-sized frame");
        foreach (var c in comps)
            if (!quant[c.Tq])
                throw new FormatException($"component {c.Id} uses quantisation table {c.Tq}, which the file never defines");

        int hmax = comps.Max(c => c.H), vmax = comps.Max(c => c.V);
        int mcusX = (width + 8 * hmax - 1) / (8 * hmax);
        int mcusY = (height + 8 * vmax - 1) / (8 * vmax);
        var pred = new int[comps.Length];
        var r = new BitReader(j, entropy);

        for (int mcu = 0; mcu < mcusX * mcusY; mcu++)
        {
            for (int ci = 0; ci < comps.Length; ci++)
            {
                var c = comps[ci];
                var s = scan.FirstOrDefault(x => x.Cs == c.Id, (Cs: -1, Td: 0, Ta: 0));
                if (s.Cs < 0) throw new FormatException($"component {c.Id} is in the frame but not in the scan");
                var dcTable = dc[s.Td] ?? throw new FormatException($"the scan uses DC table {s.Td}, which the file never defines");
                var acTable = ac[s.Ta] ?? throw new FormatException($"the scan uses AC table {s.Ta}, which the file never defines");

                for (int b = 0; b < c.H * c.V; b++)
                {
                    int t = Decode(r, dcTable);
                    if (t > 16) throw new FormatException($"DC category {t} is out of range");
                    if (t > 0) pred[ci] += Extend(r.Bits(t), t);
                    for (int k = 1; k < 64;)
                    {
                        int rs = Decode(r, acTable);
                        int run = rs >> 4, size = rs & 15;
                        if (size == 0)
                        {
                            if (run == 15) { k += 16; continue; }
                            break;
                        }
                        k += run;
                        if (k > 63) throw new FormatException("an AC coefficient index ran past the end of the block");
                        r.Bits(size);
                        k++;
                    }
                }
            }
        }

        r.Align();
        int q = r.Pos;
        while (q < j.Length && j[q] != 0xFF) q++;
        if (q + 1 >= j.Length || j[q + 1] != 0xD9)
            throw new FormatException($"expected end-of-image after the last MCU, found 0x{(q + 1 < j.Length ? j[q + 1] : 0):X2} at offset {q}");

        return new JpegInfo
        {
            Width = width,
            Height = height,
            Components = comps.Length,
            McusDecoded = mcusX * mcusY,
            TrailingBytes = q - r.Pos,
        };
    }

    private static int Extend(int v, int t) => v < (1 << (t - 1)) ? v - (1 << t) + 1 : v;
}
