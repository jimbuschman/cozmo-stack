using System.Runtime.InteropServices;
using Cozmo.Robot.Emulation;

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>Runs an in-place voice effect from the pinned engine library in an ARM machine.</summary>
public sealed class WwiseNativeVoiceEffect : IWwiseEffectPlugin, IDisposable
{
    public const uint HarmonizerId = 0x008A0003;
    public const uint ExpanderId = 0x006D0003;
    private static readonly byte[] EnglishHarmonizer = Convert.FromHexString("0100007a440000000004000000000000000000fa450000803f0100007a44000080c0050000000000000000c050440000803f000000000000c0c200000000000400000000");
    private static readonly byte[] EnglishExpander = Convert.FromHexString("00003cc26666d6409eef273de17a943e000000000000");

    private readonly ArmMachine _machine;
    private readonly uint _base;
    private readonly uint _allocator;
    private readonly uint _context;
    private readonly uint _effect;
    private readonly uint _parameters;
    private readonly uint _frame;
    private readonly uint _block;
    private readonly uint _format;
    private readonly uint _info;
    private readonly uint _initAddress;
    private readonly uint _resetAddress;
    private readonly uint _executeAddress;
    private readonly uint _termAddress;
    private readonly uint _infoAddress;
    private readonly IWwisePluginMemAlloc _hostAllocator;
    private readonly HashSet<uint> _allocations = new();
    private bool _disposed;
    private bool _initialized;

    /// <summary>
    /// Creates an effect. Init takes the cloned bank parameter bytes from the voice's FX holder.
    /// The mapped engine code owns the DSP arithmetic and the host supplies bionic libm.
    /// </summary>
    public WwiseNativeVoiceEffect(string libDir, uint pluginId, IWwisePluginMemAlloc allocator)
    {
        ArgumentNullException.ThrowIfNull(allocator);
        bool harmonizer = pluginId == HarmonizerId;
        if (!harmonizer && pluginId != ExpanderId) throw new ArgumentOutOfRangeException(nameof(pluginId));
        _hostAllocator = allocator;
        _machine = new ArmMachine(libDir, new[] { "libc++_shared.so", "libcozmoEngine.so" }, poison: 0xCD);
        try
        {
            _machine.Relocate();
            _base = _machine.Library("libcozmoEngine.so").Base;
            InstallMathHooks();

            _allocator = _machine.StaticAlloc(16);
            uint allocatorVtable = _machine.StaticAlloc(16);
            _machine.W32(_allocator, allocatorVtable);
            _machine.Libc["voice_fx_alloc"] = m =>
            {
                if (!_hostAllocator.Allocate(checked((int)m.Reg(1)))) return 0u;
                uint pointer = m.Malloc(m.Reg(1));
                if (pointer != 0) _allocations.Add(pointer);
                else _hostAllocator.Free(pointer);
                return pointer;
            };
            _machine.Libc["voice_fx_free"] = m =>
            {
                uint pointer = m.Reg(1);
                m.Free(pointer);
                if (_allocations.Remove(pointer)) _hostAllocator.Free(pointer);
                return 1;
            };
            _machine.W32(allocatorVtable + 8, _machine.Stub("voice_fx_alloc"));
            _machine.W32(allocatorVtable + 12, _machine.Stub("voice_fx_free"));

            // The voice FX wrapper constructs this context through 0xA6C22C. On the tested
            // voice path the only invoked method is +0x2C, the fixed-zero function 0xA6B554.
            _context = _machine.StaticAlloc(0x18);
            Call(0xA6C22Cu, _context, 0, 0);
            uint originalVtable = _machine.R32(_context);
            uint guardedVtable = _machine.StaticAlloc(0x60);
            _machine.Libc["voice_fx_context_missing"] = _ => throw new WwiseMissingBehaviourException("M6-022/M14-006: another voice FX context method was reached and has not been mapped.");
            uint missingContextMethod = _machine.Stub("voice_fx_context_missing");
            for (uint offset = 0; offset < 0x60; offset += 4)
                _machine.W32(guardedVtable + offset, offset == 0x2C ? _machine.R32(originalVtable + offset) : missingContextMethod);
            _machine.W32(_context, guardedVtable);
            _parameters = Call(harmonizer ? 0xAA5BC4u : 0xABCF50u, _allocator);
            if (_parameters == 0) throw new InvalidOperationException("The voice effect parameter allocation failed.");
            _effect = Call(harmonizer ? 0xAA32BCu : 0xABCC84u, _allocator);
            if (_effect == 0) throw new InvalidOperationException("The voice effect allocation failed.");
            _frame = _machine.StaticAlloc(0x40);
            _block = _machine.StaticAlloc(68);
            _format = _machine.StaticAlloc(12);
            _info = _machine.StaticAlloc(16);
            _initAddress = harmonizer ? 0xAA440Cu : 0xABC278u;
            _resetAddress = harmonizer ? 0xAA42E4u : 0xABC1B8u;
            _executeAddress = harmonizer ? 0xAA4674u : 0xABC3C0u;
            _termAddress = harmonizer ? 0xAA45A0u : 0xABC164u;
            _infoAddress = harmonizer ? 0xAA3224u : 0xABC23Cu;
            PluginId = pluginId;
        }
        catch
        {
            _machine.Dispose();
            foreach (uint allocation in _allocations) _hostAllocator.Free(allocation);
            _allocations.Clear();
            throw;
        }
    }

    public uint PluginId { get; }

    private uint Call(uint address, params uint[] args) => _machine.Call(_base + address, args);

    private void InstallMathHooks()
    {
        void Unary(uint address, Func<float, float> function) => _machine.HookAddress(_base + address, m =>
            BitConverter.SingleToUInt32Bits(function(BitConverter.UInt32BitsToSingle(m.Reg(0)))));
        Unary(0x4D0058, WwiseHostMath.Expf);
        Unary(0x4AB038, WwiseHostMath.Tanf);
        Unary(0x4A4168, WwiseHostMath.Sinf);
        Unary(0x4A415C, WwiseHostMath.Cosf);
        Unary(0x4A4078, x => (float)Math.Sqrt(x));
        Unary(0x4A4084, MathF.Floor);
        _machine.HookAddress(_base + 0x4D6778, m => BitConverter.SingleToUInt32Bits(WwiseHostMath.Powf(
            BitConverter.UInt32BitsToSingle(m.Reg(0)), BitConverter.UInt32BitsToSingle(m.Reg(1)))));
        void DoubleMath(uint address, Func<double, double> function) => _machine.HookAddress(_base + address, m =>
        {
            m.SetDbl(function(ArmMachine.Dbl(m.Reg(0), m.Reg(1))));
            return StubResult.Void;
        });
        DoubleMath(0x4D0070, Math.Cos);
        DoubleMath(0x4D007C, Math.Sin);
        DoubleMath(0x4A9F04, Math.Floor);
    }

    public int Init(IWwisePluginMemAlloc alloc, object? ctx, object? parameters, WwiseEffectFormat fmt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized) throw new InvalidOperationException("The voice effect has already been initialized.");
        if (!ReferenceEquals(alloc, _hostAllocator)) throw new ArgumentException("The effect must retain its creating allocator.", nameof(alloc));
        if (ctx is not WwiseVoiceFxContext voiceContext)
            throw new WwiseMissingBehaviourException("M6-022/M14-006: the native effect adapter has only recovered the voice FX context.");
        if (fmt.SampleRate != 48000 || fmt.ChannelWord != 0x4101)
            throw new WwiseMissingBehaviourException("M6-022/M14-006: native voice effects have only been checked for mono 0x4101 at 48000 Hz.");
        int blockSize = PluginId == HarmonizerId ? 68 : 22;
        if (parameters is not byte[] block || block.Length != blockSize)
            throw new WwiseMissingBehaviourException($"M6-022/M14-006: the effect requires its complete {blockSize}-byte bank parameter block.");
        byte[] supportedBlock = PluginId == HarmonizerId ? EnglishHarmonizer : EnglishExpander;
        if (!block.AsSpan().SequenceEqual(supportedBlock))
            throw new WwiseMissingBehaviourException("M6-022/M14-006: this voice effect parameter block has not been checked against the engine oracle.");
        _machine.W32(_context + 4, unchecked((uint)voiceContext.Index));
        _machine.Write(_block, block);
        int parameterResult = unchecked((int)Call(PluginId == HarmonizerId ? 0xAA5BA0u : 0xABCEE8u, _parameters, _allocator, _block, (uint)block.Length));
        if (parameterResult != 1) return parameterResult;
        _machine.W32(_format, fmt.SampleRate);
        _machine.W32(_format + 4, fmt.ChannelWord);
        _machine.W32(_format + 8, unchecked((uint)fmt.RawFormat));
        int result = unchecked((int)_machine.Call(_base + _initAddress, new[] { _effect, _allocator, _context, _parameters }, new[] { _format }));
        _initialized = result == 1;
        return result;
    }

    public int Reset()
    {
        EnsureInitialized();
        return unchecked((int)Call(_resetAddress, _effect));
    }

    private void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized) throw new InvalidOperationException("The voice effect has not been initialized.");
    }

    public int GetPluginInfo(out WwisePluginInfo info)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int result = unchecked((int)Call(_infoAddress, _effect, _info));
        info = new WwisePluginInfo(_machine.R32(_info), _machine.R32(_info + 4), _machine.R8(_info + 8), _machine.R8(_info + 10));
        return result;
    }

    public int Slot14() => 0;
    public int Slot18() => 0;
    public int Slot24() => 0x2D;

    public void Execute(WwiseDecodeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureInitialized();
        if (state.ChannelConfig != 0x4101)
            throw new WwiseMissingBehaviourException("M6-022/M14-006: native voice effects have only been checked with mono 0x4101 buffers.");
        if (state.Data is not float[] samples) throw new ArgumentException("The effect requires planar float samples.", nameof(state));
        int channels = (int)(state.ChannelConfig & 0xFF);
        int count = checked(channels * state.MaxFrames);
        if (channels == 0 || state.ValidFrames > state.MaxFrames || count > samples.Length)
            throw new ArgumentException("The channel configuration or frame sizes do not match the sample buffer.", nameof(state));
        uint data = _machine.Malloc((uint)(count * sizeof(float)));
        if (data == 0) throw new InvalidOperationException("The voice effect sample allocation failed.");
        try
        {
            _machine.Write(data, MemoryMarshal.AsBytes(samples.AsSpan(0, count)));
            _machine.W32(_frame, data);
            _machine.W32(_frame + 4, state.ChannelConfig);
            _machine.W32(_frame + 8, state.Scratch08);
            _machine.W16(_frame + 12, state.MaxFrames);
            _machine.W16(_frame + 14, state.ValidFrames);
            Call(_executeAddress, _effect, _frame);
            _machine.Read(data, (uint)(count * sizeof(float))).AsSpan().CopyTo(MemoryMarshal.AsBytes(samples.AsSpan(0, count)));
            state.Scratch08 = _machine.R32(_frame + 8);
            state.MaxFrames = _machine.R16(_frame + 12);
            state.ValidFrames = _machine.R16(_frame + 14);
        }
        finally { _machine.Free(data); }
    }

    public int Term(IWwisePluginMemAlloc alloc)
    {
        if (_disposed) return 1;
        if (!ReferenceEquals(alloc, _hostAllocator)) throw new ArgumentException("The effect must retain its creating allocator.", nameof(alloc));
        try
        {
            int result = unchecked((int)Call(_termAddress, _effect, _allocator));
            uint parameterTerm = _machine.R32(_machine.R32(_parameters) + 0x14);
            _machine.Call(parameterTerm, new[] { _parameters, _allocator });
            return result;
        }
        finally { Dispose(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _machine.Dispose();
        foreach (uint allocation in _allocations) _hostAllocator.Free(allocation);
        _allocations.Clear();
    }
}
