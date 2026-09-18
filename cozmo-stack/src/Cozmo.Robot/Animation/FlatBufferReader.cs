using System.Buffers.Binary;

namespace Cozmo.Robot.Animation;

/// <summary>
/// A read-only FlatBuffers table reader, enough for the <c>CozmoAnim</c> schema the robot's animation
/// assets use.
///
/// FlatBuffers is a small format and only the reading half is needed here, so this avoids taking a
/// dependency for it. Layout: a table is preceded by a signed offset to its vtable; the vtable holds its
/// own size, the table's size, then one 16-bit offset per field, zero meaning "absent, use the default".
/// Vectors and strings are reached through unsigned offsets and start with a 32-bit length.
///
/// Everything is bounds-checked. A truncated or malformed file raises <see cref="InvalidDataException"/>
/// rather than reading past the end or returning quietly wrong numbers.
/// </summary>
public readonly struct FlatTable
{
    private readonly byte[] _buf;
    private readonly int _pos;

    internal FlatTable(byte[] buf, int pos)
    {
        _buf = buf; _pos = pos;
        if (pos < 0 || pos + 4 > buf.Length) throw new InvalidDataException($"table offset {pos} is outside the file");
    }

    public bool IsEmpty => _buf is null;

    /// <summary>The root table of a FlatBuffers file.</summary>
    public static FlatTable Root(byte[] buf)
    {
        if (buf.Length < 8) throw new InvalidDataException("too short to be a FlatBuffers file");
        return new FlatTable(buf, checked((int)BinaryPrimitives.ReadUInt32LittleEndian(buf)));
    }

    /// <summary>Byte offset of a field inside the table, or 0 when the field is absent.</summary>
    private int FieldOffset(int fieldId)
    {
        int vtable = _pos - BinaryPrimitives.ReadInt32LittleEndian(_buf.AsSpan(_pos));
        if (vtable < 0 || vtable + 4 > _buf.Length) throw new InvalidDataException("vtable offset is outside the file");
        int vtableSize = BinaryPrimitives.ReadUInt16LittleEndian(_buf.AsSpan(vtable));
        int slot = 4 + fieldId * 2;
        if (slot + 2 > vtableSize) return 0;                     // the writer did not emit this field
        int off = BinaryPrimitives.ReadUInt16LittleEndian(_buf.AsSpan(vtable + slot));
        if (off == 0) return 0;
        if (_pos + off > _buf.Length) throw new InvalidDataException("field offset is outside the file");
        return _pos + off;
    }

    private void Need(int at, int bytes)
    {
        if (at + bytes > _buf.Length) throw new InvalidDataException("field runs past the end of the file");
    }

    public bool Has(int fieldId) => FieldOffset(fieldId) != 0;

    public byte U8(int fieldId, byte dflt = 0)
    {
        int o = FieldOffset(fieldId); if (o == 0) return dflt; Need(o, 1); return _buf[o];
    }

    public sbyte I8(int fieldId, sbyte dflt = 0)
    {
        int o = FieldOffset(fieldId); if (o == 0) return dflt; Need(o, 1); return (sbyte)_buf[o];
    }

    public bool Bool(int fieldId, bool dflt = false)
    {
        int o = FieldOffset(fieldId); if (o == 0) return dflt; Need(o, 1); return _buf[o] != 0;
    }

    public short I16(int fieldId, short dflt = 0)
    {
        int o = FieldOffset(fieldId); if (o == 0) return dflt; Need(o, 2);
        return BinaryPrimitives.ReadInt16LittleEndian(_buf.AsSpan(o));
    }

    public ushort U16(int fieldId, ushort dflt = 0)
    {
        int o = FieldOffset(fieldId); if (o == 0) return dflt; Need(o, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(_buf.AsSpan(o));
    }

    public uint U32(int fieldId, uint dflt = 0)
    {
        int o = FieldOffset(fieldId); if (o == 0) return dflt; Need(o, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(_buf.AsSpan(o));
    }

    public float F32(int fieldId, float dflt = 0f)
    {
        int o = FieldOffset(fieldId); if (o == 0) return dflt; Need(o, 4);
        return BinaryPrimitives.ReadSingleLittleEndian(_buf.AsSpan(o));
    }

    public string? String(int fieldId)
    {
        int o = FieldOffset(fieldId); if (o == 0) return null;
        Need(o, 4);
        int at = o + checked((int)BinaryPrimitives.ReadUInt32LittleEndian(_buf.AsSpan(o)));
        Need(at, 4);
        int len = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(_buf.AsSpan(at)));
        Need(at + 4, len);
        return System.Text.Encoding.UTF8.GetString(_buf, at + 4, len);
    }

    /// <summary>Start and length of a vector field, or (0, 0) when absent.</summary>
    private (int At, int Len) Vector(int fieldId)
    {
        int o = FieldOffset(fieldId); if (o == 0) return (0, 0);
        Need(o, 4);
        int at = o + checked((int)BinaryPrimitives.ReadUInt32LittleEndian(_buf.AsSpan(o)));
        Need(at, 4);
        int len = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(_buf.AsSpan(at)));
        if (len < 0) throw new InvalidDataException("negative vector length");
        return (at + 4, len);
    }

    public int VectorLength(int fieldId) => Vector(fieldId).Len;

    public float[] Floats(int fieldId)
    {
        var (at, len) = Vector(fieldId);
        if (len == 0) return Array.Empty<float>();
        Need(at, len * 4);
        var v = new float[len];
        for (int i = 0; i < len; i++) v[i] = BinaryPrimitives.ReadSingleLittleEndian(_buf.AsSpan(at + i * 4));
        return v;
    }

    public long[] Longs(int fieldId)
    {
        var (at, len) = Vector(fieldId);
        if (len == 0) return Array.Empty<long>();
        Need(at, len * 8);
        var v = new long[len];
        for (int i = 0; i < len; i++) v[i] = BinaryPrimitives.ReadInt64LittleEndian(_buf.AsSpan(at + i * 8));
        return v;
    }

    /// <summary>A nested table field.</summary>
    public FlatTable Table(int fieldId)
    {
        int o = FieldOffset(fieldId); if (o == 0) return default;
        Need(o, 4);
        return new FlatTable(_buf, o + checked((int)BinaryPrimitives.ReadUInt32LittleEndian(_buf.AsSpan(o))));
    }

    /// <summary>One element of a vector of tables.</summary>
    public FlatTable TableAt(int fieldId, int index)
    {
        var (at, len) = Vector(fieldId);
        if (index < 0 || index >= len) throw new ArgumentOutOfRangeException(nameof(index));
        int o = at + index * 4;
        Need(o, 4);
        return new FlatTable(_buf, o + checked((int)BinaryPrimitives.ReadUInt32LittleEndian(_buf.AsSpan(o))));
    }

    /// <summary>Every element of a vector of tables.</summary>
    public IEnumerable<FlatTable> Tables(int fieldId)
    {
        int n = VectorLength(fieldId);
        for (int i = 0; i < n; i++) yield return TableAt(fieldId, i);
    }
}
