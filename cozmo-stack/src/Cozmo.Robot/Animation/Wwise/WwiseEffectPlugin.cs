// fidelity: M6-013, M6-022
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The 12-byte audio format an effect plug-in's <c>Init</c> receives as its fifth argument (<c>fmt</c>, the stack argument at <c>[sp+0x20]</c> of the Compressor's <c>0xA9FB28</c>): <c>u32 [fmt+0]</c> is the sample rate and <c>byte [fmt+4]</c> the channel count (the whole word
/// <c>u32 [fmt+4]</c> is the channel configuration). The other bytes are not read by the Compressor.
/// </summary>
/// <param name="SampleRate"><c>u32 [fmt+0]</c>.</param>
/// <param name="ChannelWord"><c>u32 [fmt+4]</c>; its low byte is the channel count.</param>
public readonly record struct WwiseEffectFormat(uint SampleRate, uint ChannelWord)
{
    /// <summary><c>byte [fmt+4]</c> (<c>0xA9FB48 ldrb r2,[r2,#4]</c>).</summary>
    public byte Channels => (byte)(ChannelWord & 0xFF);
}

/// <summary>
/// What <c>GetPluginInfo</c> (<c>vt+0x10</c>, the Compressor's <c>0xA9FAEC</c>) writes: <c>[info+0] = 3</c>, <c>[info+4] = 0x7E002</c>, <c>byte [info+8] = 1</c> and <c>byte [info+0xA] = 0</c>. The bytes at <c>+9</c> and <c>+0xB</c> are not written.
/// </summary>
/// <param name="Word0"><c>[info+0]</c>.</param>
/// <param name="Word4"><c>[info+4]</c>.</param>
/// <param name="Byte8"><c>byte [info+8]</c>.</param>
/// <param name="ByteA"><c>byte [info+0xA]</c>.</param>
public readonly record struct WwisePluginInfo(uint Word0, uint Word4, byte Byte8, byte ByteA);

/// <summary>
/// The plug-in memory allocator the engine passes to a plug-in (<c>alloc-&gt;vt+8(alloc, size)</c> allocates, <c>alloc-&gt;vt+0xC(alloc, block)</c> frees: <c>0xAA0538</c>, <c>0xAA0808</c>, <c>0xAA05BC</c>, <c>0xA9FBF0</c>, <c>0xA9FA38</c>). The engine's allocator is a pool; the managed
/// stand-in only answers whether the allocation succeeds (<c>false</c> is the engine's null) and records the frees.
/// </summary>
public interface IWwisePluginMemAlloc
{
    /// <summary><c>vt+8</c>: allocates <paramref name="size"/> bytes; <c>false</c> is the engine's null result.</summary>
    bool Allocate(int size);

    /// <summary><c>vt+0xC</c>: frees a block (the object or its state array).</summary>
    void Free(object block);
}

/// <summary>A counting allocator for tests and for a host that has no pool: every allocation succeeds except the n-th when <see cref="FailAt"/> is set (1-based); the sizes and the frees are recorded.</summary>
public sealed class WwisePluginAllocator : IWwisePluginMemAlloc
{
    /// <summary>The 1-based allocation call that fails, or <c>null</c> for none.</summary>
    public int? FailAt { get; set; }

    /// <summary>The sizes of the allocation calls so far, in order (a failed call is included).</summary>
    public List<int> Sizes { get; } = new();

    /// <summary>The blocks freed so far, in order.</summary>
    public List<object> Freed { get; } = new();

    /// <inheritdoc />
    public bool Allocate(int size)
    {
        Sizes.Add(size);
        return FailAt != Sizes.Count;
    }

    /// <inheritdoc />
    public void Free(object block) => Freed.Add(block);
}

/// <summary>
/// An effect plug-in object as the voice's insert-FX wrapper and the bus FX slot hold it (the plug-in vtable, <c>0x103DF28</c> for the Compressor): <c>vt+8</c> Term, <c>vt+0xC</c> Reset, <c>vt+0x10</c> GetPluginInfo, <c>vt+0x14</c> and <c>vt+0x18</c> (the shared stubs
/// <c>0x8DBF38</c> / <c>0x8DBF3C</c>, which return 0), <c>vt+0x1C</c> Init, <c>vt+0x20</c> Execute and <c>vt+0x24</c> (<c>0xAA05B0</c>, which returns 0x2D). The slot names are the repo's; the engine's artifact carries none.
/// </summary>
public interface IWwiseEffectPlugin
{
    /// <summary><c>vt+8</c>: Term(alloc): frees the plug-in's state and the object; returns 1.</summary>
    int Term(IWwisePluginMemAlloc alloc);

    /// <summary><c>vt+0xC</c>: Reset; returns 1.</summary>
    int Reset();

    /// <summary><c>vt+0x10</c>: GetPluginInfo; returns 1.</summary>
    int GetPluginInfo(out WwisePluginInfo info);

    /// <summary><c>vt+0x14</c> (<c>0x8DBF38</c>: <c>movs r0, #0; bx lr</c>).</summary>
    int Slot14();

    /// <summary><c>vt+0x18</c> (<c>0x8DBF3C</c>: <c>movs r0, #0; bx lr</c>).</summary>
    int Slot18();

    /// <summary><c>vt+0x1C</c>: Init(alloc, ctx, params, fmt); 1 on success.</summary>
    int Init(IWwisePluginMemAlloc alloc, object? ctx, object? parameters, WwiseEffectFormat fmt);

    /// <summary><c>vt+0x20</c>: Execute(S) on the engine's 0x28-byte audio state, in place.</summary>
    void Execute(WwiseDecodeState state);

    /// <summary><c>vt+0x24</c>: <c>0xAA05B0</c> returns 0x2D.</summary>
    int Slot24();
}
