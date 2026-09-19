namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// Reads a little-endian bit stream, least significant bit of each byte first, which is how Vorbis packs
/// its headers.
///
/// Ported from ww2ogg's <c>Bit_stream</c> (BSD-3-Clause; see third-party/ww2ogg/LICENSE).
/// </summary>
internal sealed class BitReader
{
    private readonly ReadOnlyMemory<byte> _data;
    private int _bytePos;
    private int _bitPos;

    public BitReader(ReadOnlyMemory<byte> data) => _data = data;

    /// <summary>How many bits have been consumed, which the setup rebuild checks against the packet size.</summary>
    public long BitsRead { get; private set; }

    public bool ReadBit()
    {
        if (_bytePos >= _data.Length) throw new InvalidDataException("bit stream ran out");
        bool bit = (_data.Span[_bytePos] & (1 << _bitPos)) != 0;
        if (++_bitPos == 8) { _bitPos = 0; _bytePos++; }
        BitsRead++;
        return bit;
    }

    /// <summary>Reads up to 32 bits, least significant first.</summary>
    public uint Read(int bits)
    {
        if (bits is < 0 or > 32) throw new ArgumentOutOfRangeException(nameof(bits));
        uint v = 0;
        for (int i = 0; i < bits; i++) if (ReadBit()) v |= 1u << i;
        return v;
    }
}

/// <summary>
/// Writes a Vorbis bit stream and frames it into Ogg pages.
///
/// Ported from ww2ogg's <c>Bit_oggstream</c> (BSD-3-Clause; see third-party/ww2ogg/LICENSE). The page
/// layout, the lacing rules and the CRC are Ogg's, not Wwise's.
/// </summary>
internal sealed class OggWriter
{
    private const int HeaderBytes = 27;
    private const int MaxSegments = 255;
    private const int SegmentSize = 255;

    private readonly List<byte> _out = new();
    private readonly byte[] _payload = new byte[SegmentSize * MaxSegments];
    private int _payloadBytes;
    private byte _bitBuffer;
    private int _bitsStored;
    private bool _first = true;
    private bool _continued;
    private uint _granule;
    private uint _seqno;

    public void SetGranule(uint g) => _granule = g;

    public void PutBit(bool bit)
    {
        if (bit) _bitBuffer |= (byte)(1 << _bitsStored);
        if (++_bitsStored == 8) FlushBits();
    }

    /// <summary>Writes the low <paramref name="bits"/> bits of a value, least significant first.</summary>
    public void Write(uint value, int bits)
    {
        for (int i = 0; i < bits; i++) PutBit((value & (1u << i)) != 0);
    }

    public void FlushBits()
    {
        if (_bitsStored == 0) return;
        if (_payloadBytes == _payload.Length)
            throw new InvalidDataException("ran out of space in an Ogg packet");
        _payload[_payloadBytes++] = _bitBuffer;
        _bitsStored = 0;
        _bitBuffer = 0;
    }

    /// <summary>Closes the current page. Vorbis puts each header packet on a page of its own.</summary>
    public void FlushPage(bool nextContinued = false, bool last = false)
    {
        if (_payloadBytes != _payload.Length) FlushBits();
        if (_payloadBytes == 0) { _continued = nextContinued; return; }

        int segments = (_payloadBytes + SegmentSize) / SegmentSize;      // rounds up on purpose
        if (segments == MaxSegments + 1) segments = MaxSegments;         // at the maximum, no trailing zero

        var page = new byte[HeaderBytes + segments + _payloadBytes];
        page[0] = (byte)'O'; page[1] = (byte)'g'; page[2] = (byte)'g'; page[3] = (byte)'S';
        page[4] = 0;
        page[5] = (byte)((_continued ? 1 : 0) | (_first ? 2 : 0) | (last ? 4 : 0));
        WriteLe32(page, 6, _granule);
        WriteLe32(page, 10, _granule == 0xFFFFFFFF ? 0xFFFFFFFF : 0);
        WriteLe32(page, 14, 1);                                          // stream serial number
        WriteLe32(page, 18, _seqno);
        WriteLe32(page, 22, 0);                                          // checksum, filled in below
        page[26] = (byte)segments;

        for (int i = 0, left = _payloadBytes; i < segments; i++)
        {
            if (left >= SegmentSize) { page[27 + i] = SegmentSize; left -= SegmentSize; }
            else page[27 + i] = (byte)left;
        }
        Array.Copy(_payload, 0, page, HeaderBytes + segments, _payloadBytes);
        WriteLe32(page, 22, Crc(page));

        _out.AddRange(page);
        _seqno++;
        _first = false;
        _continued = nextContinued;
        _payloadBytes = 0;
    }

    public byte[] ToArray()
    {
        FlushPage(false, true);
        return _out.ToArray();
    }

    private static void WriteLe32(byte[] b, int at, uint v)
    {
        b[at] = (byte)v; b[at + 1] = (byte)(v >> 8); b[at + 2] = (byte)(v >> 16); b[at + 3] = (byte)(v >> 24);
    }

    // Ogg's CRC-32: polynomial 0x04C11DB7, no reflection, no final xor.
    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint r = i << 24;
            for (int j = 0; j < 8; j++) r = (r & 0x80000000) != 0 ? (r << 1) ^ 0x04C11DB7 : r << 1;
            t[i] = r;
        }
        return t;
    }

    private static uint Crc(byte[] data)
    {
        uint crc = 0;
        foreach (byte b in data) crc = (crc << 8) ^ CrcTable[((crc >> 24) & 0xFF) ^ b];
        return crc;
    }
}
