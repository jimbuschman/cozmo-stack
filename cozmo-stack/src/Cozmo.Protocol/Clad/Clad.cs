using System.Buffers.Binary;
using System.Text;

namespace Cozmo.Protocol;

/// <summary>Little-endian CLAD reader (CLAD::SafeMessageBuffer semantics: reads past the end throw).</summary>
public sealed class CladReader
{
    private readonly ReadOnlyMemory<byte> _buf;
    public int Position { get; private set; }
    public int Remaining => _buf.Length - Position;
    public CladReader(ReadOnlyMemory<byte> buf) { _buf = buf; }
    private ReadOnlySpan<byte> Take(int n)
    {
        if (Position + n > _buf.Length) throw new FormatException($"CLAD read of {n} bytes at {Position} exceeds {_buf.Length}");
        var s = _buf.Span.Slice(Position, n); Position += n; return s;
    }
    public byte U8() => Take(1)[0];
    public sbyte I8() => (sbyte)Take(1)[0];
    public bool Bool() => Take(1)[0] != 0;
    public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
    public short I16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));
    public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
    public int I32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
    public ulong U64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
    public float F32() => BinaryPrimitives.ReadSingleLittleEndian(Take(4));
    public double F64() => BinaryPrimitives.ReadDoubleLittleEndian(Take(8));
    public byte[] Bytes(int n) => Take(n).ToArray();
    public byte[] Rest() => Take(Remaining).ToArray();
    public ushort[] U16Array(int n) { var a = new ushort[n]; for (int i = 0; i < n; i++) a[i] = U16(); return a; }
    /// <summary>CLAD string[uint_8]: u8 length + bytes.</summary>
    public string String8() { int n = U8(); return Encoding.UTF8.GetString(Take(n)); }
    /// <summary>CLAD string[uint_16]: u16 length + bytes.</summary>
    public string String16() { int n = U16(); return Encoding.UTF8.GetString(Take(n)); }
}

/// <summary>Little-endian CLAD writer.</summary>
public sealed class CladWriter
{
    private readonly List<byte> _b = new(64);
    public int Length => _b.Count;
    public byte[] ToArray() => _b.ToArray();
    private void Put(Span<byte> tmp) { foreach (var x in tmp) _b.Add(x); }
    public CladWriter U8(byte v) { _b.Add(v); return this; }
    public CladWriter I8(sbyte v) { _b.Add((byte)v); return this; }
    public CladWriter Bool(bool v) { _b.Add((byte)(v ? 1 : 0)); return this; }
    public CladWriter U16(ushort v) { Span<byte> t = stackalloc byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(t, v); Put(t); return this; }
    public CladWriter I16(short v) { Span<byte> t = stackalloc byte[2]; BinaryPrimitives.WriteInt16LittleEndian(t, v); Put(t); return this; }
    public CladWriter U32(uint v) { Span<byte> t = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(t, v); Put(t); return this; }
    public CladWriter I32(int v) { Span<byte> t = stackalloc byte[4]; BinaryPrimitives.WriteInt32LittleEndian(t, v); Put(t); return this; }
    public CladWriter F32(float v) { Span<byte> t = stackalloc byte[4]; BinaryPrimitives.WriteSingleLittleEndian(t, v); Put(t); return this; }
    public CladWriter F64(double v) { Span<byte> t = stackalloc byte[8]; BinaryPrimitives.WriteDoubleLittleEndian(t, v); Put(t); return this; }
    public CladWriter Bytes(ReadOnlySpan<byte> v) { foreach (var x in v) _b.Add(x); return this; }
    public CladWriter String8(string s) { var b = Encoding.UTF8.GetBytes(s); if (b.Length > 255) throw new ArgumentException("string too long"); U8((byte)b.Length); return Bytes(b); }
    public CladWriter String16(string s) { var b = Encoding.UTF8.GetBytes(s); U16((ushort)b.Length); return Bytes(b); }
}
