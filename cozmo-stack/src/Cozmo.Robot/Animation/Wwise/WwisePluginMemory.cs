// fidelity: M6-013, M6-015
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The pool memory of an engine object, as a byte array with a "has been stored" mask. The engine's plug-in allocator (<c>alloc-&gt;vt+8</c>) returns uninitialised memory and the creators store only some fields (the EQ parameter object creator <c>0xAA3178</c> stores the vptr and the three dirty bytes, the limiter
/// parameter object creator <c>0xAA2280</c> only the vptr); the code that follows reads fields by offset. A read of a byte nothing has stored is the engine reading uninitialised pool memory: it stops visibly with <see cref="WwiseMissingBehaviourException"/> instead of returning a made-up value.
/// </summary>
internal sealed class WwisePluginMemory
{
    private readonly byte[] _bytes;
    private readonly bool[] _defined;
    private readonly string _owner;

    internal WwisePluginMemory(int size, string owner)
    {
        _bytes = new byte[size];
        _defined = new bool[size];
        _owner = owner;
    }

    internal int Size => _bytes.Length;

    private void Need(int offset, int count)
    {
        for (int i = 0; i < count; i++)
            if (!_defined[offset + i])
                throw new WwiseMissingBehaviourException($"M6-013: the {_owner} byte at +0x{offset + i:X} was read before any store (uninitialised pool memory)");
    }

    internal bool IsDefined(int offset, int count)
    {
        for (int i = 0; i < count; i++) if (!_defined[offset + i]) return false;
        return true;
    }

    internal uint U32(int offset)
    {
        Need(offset, 4);
        return BitConverter.ToUInt32(_bytes, offset);
    }

    internal float F32(int offset) => BitConverter.UInt32BitsToSingle(U32(offset));

    internal byte U8(int offset)
    {
        Need(offset, 1);
        return _bytes[offset];
    }

    internal void SetU32(int offset, uint value)
    {
        BitConverter.TryWriteBytes(_bytes.AsSpan(offset, 4), value);
        for (int i = 0; i < 4; i++) _defined[offset + i] = true;
    }

    internal void SetU8(int offset, byte value)
    {
        _bytes[offset] = value;
        _defined[offset] = true;
    }

    /// <summary>A word copy of <paramref name="count"/> bytes (the engine's <c>memcpy</c> / <c>ldm</c> + <c>stm</c>): the mask travels with the bytes.</summary>
    internal void CopyFrom(WwisePluginMemory source, int sourceOffset, int offset, int count)
    {
        Array.Copy(source._bytes, sourceOffset, _bytes, offset, count);
        Array.Copy(source._defined, sourceOffset, _defined, offset, count);
    }
}
