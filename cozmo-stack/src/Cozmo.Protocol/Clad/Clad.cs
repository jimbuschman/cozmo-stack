using System.Buffers.Binary;
using System.Text;

namespace Cozmo.Protocol;

// fidelity: M2-001, M2-011
/// <summary>
/// Little-endian CLAD reader with the engine's <c>SafeMessageBuffer</c> read semantics (M2 inventory D8..D13):
/// <list type="bullet">
/// <item>every primitive is one all-or-nothing <c>ReadBytes</c> (0x0083C09A..0x0083C0CA): when its bytes are
/// not all there it copies nothing, does not advance and fails, with no sticky error, so a later smaller read
/// can still succeed. A failed read here yields the type's default value and sets <see cref="LastReadFailed"/>;
/// nothing throws;</item>
/// <item><c>Read&lt;bool&gt;</c> stores (byte != 0) (D9, 0x0083C0F0..0x0083C118);</item>
/// <item>variable arrays and strings read their count and then their elements one at a time, stopping at the
/// first failed element (D10), so an over-counted array is returned shorter; fixed arrays have no prefix and
/// stop at the first failure too (D12), leaving the remaining elements at their default;</item>
/// <item>the caller decides what to keep from <see cref="Remaining"/>: the engine keeps a message only when
/// the bytes consumed equal its length (D1, D7, D11).</item>
/// </list>
/// </summary>
public sealed class CladReader
{
    private readonly ReadOnlyMemory<byte> _buf;
    public int Position { get; private set; }
    public int Remaining => _buf.Length - Position;
    /// <summary>Whether the most recent primitive read failed for want of bytes (D8). Not sticky: the next read resets it.</summary>
    public bool LastReadFailed { get; private set; }
    public CladReader(ReadOnlyMemory<byte> buf) { _buf = buf; }
    private bool TryTake(int n, out ReadOnlySpan<byte> s)
    {
        if (n > Remaining) { LastReadFailed = true; s = default; return false; }
        s = _buf.Span.Slice(Position, n); Position += n; LastReadFailed = false; return true;
    }
    // fidelity: M2-017 (a failed read yields 0/false; the engine leaves stale stack bytes, which cannot be reproduced)
    public byte U8() => TryTake(1, out var s) ? s[0] : (byte)0;
    public sbyte I8() => TryTake(1, out var s) ? (sbyte)s[0] : (sbyte)0;
    public bool Bool() => TryTake(1, out var s) && s[0] != 0;
    public ushort U16() => TryTake(2, out var s) ? BinaryPrimitives.ReadUInt16LittleEndian(s) : (ushort)0;
    public short I16() => TryTake(2, out var s) ? BinaryPrimitives.ReadInt16LittleEndian(s) : (short)0;
    public uint U32() => TryTake(4, out var s) ? BinaryPrimitives.ReadUInt32LittleEndian(s) : 0u;
    public int I32() => TryTake(4, out var s) ? BinaryPrimitives.ReadInt32LittleEndian(s) : 0;
    public ulong U64() => TryTake(8, out var s) ? BinaryPrimitives.ReadUInt64LittleEndian(s) : 0ul;
    public float F32() => TryTake(4, out var s) ? BinaryPrimitives.ReadSingleLittleEndian(s) : 0f;
    public double F64() => TryTake(8, out var s) ? BinaryPrimitives.ReadDoubleLittleEndian(s) : 0d;
    public long I64() => TryTake(8, out var s) ? BinaryPrimitives.ReadInt64LittleEndian(s) : 0L;
    /// <summary>One all-or-nothing read of n bytes: n bytes, or n zero bytes and no advance when they are not all there.</summary>
    public byte[] Bytes(int n) => TryTake(n, out var s) ? s.ToArray() : new byte[n];
    public byte[] Rest() { TryTake(Remaining, out var s); return s.ToArray(); }
    public ushort[] U16Array(int n) => Array(n, U16);
    /// <summary>A fixed array (D12): n elements, read in order until the first failed read; the rest stay default.</summary>
    public T[] Array<T>(int n, Func<T> read)
    {
        var a = new T[n];
        for (int i = 0; i < n; i++) { var v = read(); if (LastReadFailed) break; a[i] = v; }
        return a;
    }
    /// <summary>A counted array's elements (D10): up to n, stopping at the first failed element, so the result can be shorter than n.</summary>
    public T[] VarArray<T>(int n, Func<T> read)
    {
        var a = new List<T>(Math.Min(n, Remaining));
        for (int i = 0; i < n; i++) { var v = read(); if (LastReadFailed) break; a.Add(v); }
        return a.ToArray();
    }
    /// <summary>CLAD string[uint_8] (D10, 0x006C235C): u8 count, then one char per read until the first failure.</summary>
    public string String8() { int n = U8(); return LastReadFailed ? string.Empty : Encoding.UTF8.GetString(VarArray(n, U8)); }
    /// <summary>CLAD string[uint_16]: u16 count, then one char per read until the first failure.</summary>
    public string String16() { int n = U16(); return LastReadFailed ? string.Empty : Encoding.UTF8.GetString(VarArray(n, U8)); }
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
    public CladWriter U64(ulong v) { Span<byte> t = stackalloc byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(t, v); Put(t); return this; }
    public CladWriter I64(long v) { Span<byte> t = stackalloc byte[8]; BinaryPrimitives.WriteInt64LittleEndian(t, v); Put(t); return this; }
    public CladWriter F32(float v) { Span<byte> t = stackalloc byte[4]; BinaryPrimitives.WriteSingleLittleEndian(t, v); Put(t); return this; }
    public CladWriter F64(double v) { Span<byte> t = stackalloc byte[8]; BinaryPrimitives.WriteDoubleLittleEndian(t, v); Put(t); return this; }
    public CladWriter Bytes(ReadOnlySpan<byte> v) { foreach (var x in v) _b.Add(x); return this; }
    public CladWriter String8(string s) { var b = Encoding.UTF8.GetBytes(s); if (b.Length > 255) throw new ArgumentException("string too long"); U8((byte)b.Length); return Bytes(b); }
    public CladWriter String16(string s) { var b = Encoding.UTF8.GetBytes(s); U16((ushort)b.Length); return Bytes(b); }
}
