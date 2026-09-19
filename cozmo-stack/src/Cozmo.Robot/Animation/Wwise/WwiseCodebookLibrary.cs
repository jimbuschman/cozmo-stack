namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The packed Vorbis codebook library that Wwise streams index into.
///
/// Wwise strips the codebooks out of the Vorbis streams it encodes, leaving 10-bit indices into an external
/// library. This class loads that library and expands one of its packed codebooks back into a standard
/// Vorbis codebook.
///
/// Ported from ww2ogg's <c>codebook_library</c> (BSD-3-Clause). The packed data file itself is vendored at
/// <c>third-party/ww2ogg/packed_codebooks_aoTuV_603.bin</c>; see the README there for provenance and hash.
/// It is generic codec data, not a Cozmo asset.
///
/// The packed form differs from the standard one in three ways, which is what <see cref="Rebuild"/> undoes:
/// the identifier and the dimension and entry widths are shortened, unordered codeword lengths are packed
/// into a variable number of bits rather than five, and the lookup type is one bit rather than four.
/// </summary>
public sealed class WwiseCodebookLibrary
{
    private readonly byte[] _data;
    private readonly int[] _offsets;

    /// <summary>How many codebooks the library holds.</summary>
    public int Count => Math.Max(0, _offsets.Length - 1);

    private WwiseCodebookLibrary(byte[] data, int[] offsets)
    {
        _data = data;
        _offsets = offsets;
    }

    /// <summary>
    /// Loads a packed codebook file. The last four bytes give the offset of the offset table; the table
    /// holds one little-endian 32-bit offset per codebook, and codebook <c>i</c> spans
    /// <c>offsets[i]..offsets[i+1]</c>.
    /// </summary>
    public static WwiseCodebookLibrary Load(string path) => Parse(File.ReadAllBytes(path), path);

    /// <summary>Parses an already-loaded packed codebook file, checking it rather than trusting it.</summary>
    public static WwiseCodebookLibrary Parse(byte[] file, string what = "codebook library")
    {
        if (file.Length < 8) throw new InvalidDataException($"{what}: too short to be a codebook library");
        int offsetOffset = (int)ReadLe32(file, file.Length - 4);
        if (offsetOffset < 0 || offsetOffset > file.Length - 4 || (file.Length - offsetOffset) % 4 != 0)
            throw new InvalidDataException($"{what}: the offset table does not fit the file");

        int count = (file.Length - offsetOffset) / 4;
        var offsets = new int[count];
        for (int i = 0; i < count; i++)
        {
            offsets[i] = (int)ReadLe32(file, offsetOffset + 4 * i);
            if (offsets[i] < 0 || offsets[i] > offsetOffset)
                throw new InvalidDataException($"{what}: codebook offset {i} is outside the data");
            if (i > 0 && offsets[i] < offsets[i - 1])
                throw new InvalidDataException($"{what}: the offset table is not ascending at {i}");
        }
        return new WwiseCodebookLibrary(file, offsets);
    }

    private static uint ReadLe32(byte[] b, int at) =>
        (uint)(b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24));

    /// <summary>Expands packed codebook <paramref name="id"/> into standard Vorbis form.</summary>
    internal void Rebuild(int id, OggWriter os)
    {
        if (id < 0 || id >= Count) throw new InvalidDataException($"no codebook {id} in the library");
        int start = _offsets[id], end = _offsets[id + 1];
        var slice = new ReadOnlyMemory<byte>(_data, start, end - start);
        Rebuild(new BitReader(slice), end - start, os);
    }

    /// <summary>
    /// Expands one packed codebook. <paramref name="size"/> is the packed length, checked at the end so a
    /// codebook that decodes to the wrong length is rejected rather than corrupting the stream; pass 0 to
    /// skip that check for an inline bitstream.
    /// </summary>
    internal static void Rebuild(BitReader bis, long size, OggWriter os)
    {
        uint dimensions = bis.Read(4);
        uint entries = bis.Read(14);

        // the standard form: 24-bit 'BCV' identifier, 16-bit dimensions, 24-bit entry count
        os.Write(0x564342, 24);
        os.Write(dimensions, 16);
        os.Write(entries, 24);

        bool ordered = bis.ReadBit();
        os.PutBit(ordered);
        if (ordered)
        {
            os.Write(bis.Read(5), 5);                       // initial length
            uint current = 0;
            int guard = 0;
            while (current < entries)
            {
                int bits = ILog(entries - current);
                uint number = bis.Read(bits);
                os.Write(number, bits);
                current += number;
                // A run of zero-length groups would never reach the entry count. Real data always
                // advances; garbage does not, so this refuses rather than spinning.
                if (++guard > entries + 64) throw new InvalidDataException("codebook: ordered lengths do not advance");
            }
            if (current > entries) throw new InvalidDataException("codebook: entry count overran");
        }
        else
        {
            int lengthBits = (int)bis.Read(3);
            bool sparse = bis.ReadBit();
            if (lengthBits is 0 or > 5) throw new InvalidDataException("codebook: nonsense codeword length");
            os.PutBit(sparse);

            for (uint i = 0; i < entries; i++)
            {
                bool present = true;
                if (sparse)
                {
                    present = bis.ReadBit();
                    os.PutBit(present);
                }
                // packed in lengthBits, written out in the standard five
                if (present) os.Write(bis.Read(lengthBits), 5);
            }
        }

        // the packed form uses one bit for the lookup type where the standard uses four
        uint lookupType = bis.Read(1);
        os.Write(lookupType, 4);
        if (lookupType == 1)
        {
            uint min = bis.Read(32), max = bis.Read(32);
            uint valueLength = bis.Read(4);
            bool sequence = bis.ReadBit();
            os.Write(min, 32); os.Write(max, 32); os.Write(valueLength, 4); os.PutBit(sequence);

            uint quantvals = QuantVals(entries, dimensions);
            for (uint i = 0; i < quantvals; i++) os.Write(bis.Read((int)valueLength + 1), (int)valueLength + 1);
        }
        else if (lookupType != 0)
        {
            throw new InvalidDataException($"codebook: lookup type {lookupType} is not supported");
        }

        // If every bit of the last byte was used there is one extra zero byte, which is why this is +1.
        if (size != 0 && bis.BitsRead / 8 + 1 != size)
            throw new InvalidDataException($"codebook: read {bis.BitsRead / 8 + 1} bytes of {size}");
    }

    /// <summary>Number of bits needed to represent a value, as Vorbis defines it.</summary>
    internal static int ILog(uint v)
    {
        int n = 0;
        while (v != 0) { n++; v >>= 1; }
        return n;
    }

    /// <summary>
    /// The number of quantised values a lookup-type-1 codebook holds: the largest integer whose
    /// <c>dimensions</c>-th power does not exceed <c>entries</c>. From Vorbis's own
    /// <c>_book_maptype1_quantvals</c>.
    /// </summary>
    internal static uint QuantVals(uint entries, uint dimensions)
    {
        if (dimensions == 0) return 0;
        int bits = ILog(entries);
        var vals = (uint)(entries >> (int)((bits - 1) * (dimensions - 1) / dimensions));
        for (int guard = 0; ; guard++)
        {
            if (guard > 1000) throw new InvalidDataException("codebook: quantvals did not converge");
            // Both products must be computed in full. Stopping early leaves partial products, and the
            // convergence test below then never settles, so vals oscillates forever.
            ulong acc = 1, acc1 = 1;
            for (uint i = 0; i < dimensions; i++)
            {
                acc *= vals;
                acc1 *= vals + 1;
            }
            if (acc <= entries && acc1 > entries) return vals;
            if (acc > entries) vals--; else vals++;
        }
    }
}
