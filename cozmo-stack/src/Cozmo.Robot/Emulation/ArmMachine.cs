using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;
using static Cozmo.Robot.Emulation.UnicornNative;

namespace Cozmo.Robot.Emulation;

/// <summary>What an import stand-in does when it returns: write r0 (<see cref="Value"/>), leave r0 alone
/// (<see cref="Void"/>, Python's <c>None</c>), or leave the pc alone because the handler already redirected it
/// (<see cref="NoReturn"/>, Python's <c>NORET</c>). Mirrors <c>emu_elf.Machine._stub_hook</c>.</summary>
public readonly struct StubResult
{
    internal enum K : byte { Value, Void, NoReturn }
    internal readonly K Kind;
    internal readonly uint Value;
    private StubResult(K kind, uint value) { Kind = kind; Value = value; }
    public static readonly StubResult Void = new(K.Void, 0);
    public static readonly StubResult NoReturn = new(K.NoReturn, 0);
    public static implicit operator StubResult(uint v) => new(K.Value, v);
    public static implicit operator StubResult(int v) => new(K.Value, unchecked((uint)v));
}

public delegate StubResult ImportHandler(ArmMachine m);

/// <summary>A shipped library mapped into the machine.</summary>
public sealed class LoadedLibrary
{
    internal LoadedLibrary(string name, uint @base, ElfImage elf)
    {
        Name = name; Base = @base; Elf = elf;
        foreach (var s in elf.DynamicSymbols)
            if (s.SectionIndex != 0 && s.Name.Length > 0)
                Symbols[s.Name] = @base + s.Value;
    }
    public string Name { get; }
    public uint Base { get; }
    public ElfImage Elf { get; }
    /// <summary>Exported definitions: name to absolute address (Thumb bit kept, as the symbol value has it).</summary>
    public Dictionary<string, uint> Symbols { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// A port of <c>re-analysis/tools/emu/emu_elf.py</c>'s <c>Machine</c> to C# over the vendored Unicorn 2.1.4.
///
/// <para>Maps the shipped libraries side by side at their pinned bases (<see cref="ShippedLibraries"/>), applies their
/// R_ARM_RELATIVE / ABS32 / GLOB_DAT / JUMP_SLOT relocations, and binds each import to the first loaded library that defines
/// it. Everything else (bionic libc, libm, pthread, liblog, and the consumer's own stand-ins such as JNI) is a C# handler
/// registered by name in <see cref="Libc"/>; an import with no definition and no handler becomes a stub that stops the run
/// with its name (<see cref="EmulationAbort"/>), so nothing is silently faked.</para>
///
/// <para>Boundaries, all as in the Python: the heap is first-fit with 16-byte granules and fresh memory filled with
/// <see cref="Poison"/>; setjmp/longjmp, pthread, libm, string.h and printf are host code; a C++ throw stops the run
/// (<see cref="EmulatedCxxThrow"/>); an unmapped access stops the run. NEON/VFP are enabled (CPACR cp10/cp11, FPEXC.EN);
/// Thumb/ARM interworking follows the address's low bit. Guest memory is host memory mapped with <c>uc_mem_map_ptr</c>,
/// so the stand-ins read and write it directly (implementation freedom: the guest sees the same bytes).</para>
///
/// <para>Not thread-safe: one machine runs on one thread at a time.</para>
/// </summary>
public sealed unsafe partial class ArmMachine : IDisposable
{
    public const uint StubBase = 0x7F000000;
    public const uint StubSize = 0x100000;
    public const uint RetAddr = 0x7F100000;
    public const uint HeapBase = 0x40000000;
    public const uint HeapSize = 0x08000000;
    public const uint StackBase = 0x50000000;
    public const uint StackSize = 0x00200000;
    public const uint StaticBase = 0x60000000;
    public const uint StaticSize = 0x00100000;

    private struct Region { public uint Base, Size; public byte* Ptr; public void* Alloc; }

    private nint _uc;
    private GCHandle _self;
    private readonly List<Region> _regions = new();
    private Region _lastRegion;
    private readonly List<LoadedLibrary> _libs = new();
    private readonly Dictionary<uint, string> _stubNames = new();
    private readonly Dictionary<string, uint> _stubAddr = new(StringComparer.Ordinal);
    private readonly Dictionary<string, uint> _dataAddr = new(StringComparer.Ordinal);
    private readonly Dictionary<uint, ImportHandler> _addrHooks = new();
    private uint _nextStub = StubBase;
    private uint _staticTop = StaticBase;
    private Exception? _pending;
    private string _fault = "";
    private bool _disposed;
    private Exception? _executionFailure;
    private long _deadline;
    private uint _activeCall;
    private int _runDepth;

    /// <summary>Maximum elapsed time for guest execution and returning imports, shared with nested calls. Must be positive.
    /// A managed import that never returns cannot be interrupted by the guest execution hook.</summary>
    public TimeSpan CallTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Import stand-ins by symbol name (bionic and anything else not shipped). A consumer adds its own before
    /// <see cref="Relocate"/>, or after it for names bound to stubs.</summary>
    public Dictionary<string, ImportHandler> Libc { get; } = new(StringComparer.Ordinal);
    /// <summary>Handlers that win over any definition, including one inside a loaded library.</summary>
    public Dictionary<string, ImportHandler> Overrides { get; } = new(StringComparer.Ordinal);
    /// <summary>Captured log / printf output.</summary>
    public List<string> Log { get; } = new();
    public byte Poison { get; }
    public IReadOnlyDictionary<string, LoadedLibrary> Libraries => _libs.ToDictionary(l => l.Name);
    public LoadedLibrary Library(string name) => _libs.First(l => l.Name == name);
    public uint ErrnoAddress { get; }

    /// <summary>Maps the named shipped libraries from <paramref name="libDir"/>, each checked against its SHA-256 pin.</summary>
    public ArmMachine(string libDir, IEnumerable<string> libs, byte poison = 0x00)
    {
        EnsureVersion();
        Poison = poison;
        nint uc;
        Check(uc_open(UC_ARCH_ARM, UC_MODE_ARM, &uc), "uc_open");
        _uc = uc;
        _self = GCHandle.Alloc(this, GCHandleType.WeakTrackResurrection);
        try
        {
            Map(StubBase, StubSize);
            Map(RetAddr, 0x1000);
            Map(HeapBase, HeapSize);
            Map(StackBase, StackSize);
            Map(StaticBase, StaticSize);
            WriteReg(UC_ARM_REG_C1_C0_2, ReadReg(UC_ARM_REG_C1_C0_2) | (0xFUL << 20));
            WriteReg(UC_ARM_REG_FPEXC, 0x40000000);
            var wanted = new HashSet<string>(libs, StringComparer.Ordinal);
            foreach (var pin in ShippedLibraries.All)
            {
                if (!wanted.Remove(pin.Name)) continue;
                var elf = new ElfImage(ShippedLibraries.ReadPinned(libDir, pin.Name));
                var lib = new LoadedLibrary(pin.Name, pin.Base, elf);
                _libs.Add(lib);
                uint size = (elf.End + 0xFFF) & ~0xFFFu;
                var r = Map(pin.Base, size);
                foreach (var s in elf.Loads)
                    elf.Raw.AsSpan((int)s.FileOffset, (int)s.FileSize).CopyTo(new Span<byte>(r.Ptr + s.VirtualAddress, (int)s.FileSize));
            }
            if (wanted.Count > 0) throw new ArgumentException($"not pinned shipped libraries: {string.Join(", ", wanted)}");
            ErrnoAddress = StaticAlloc(16);
            InstallLibc();
            var stub = Ptr(StubBase, StubSize);
            for (uint i = 0; i < StubSize; i += 4) *(uint*)(stub + i) = 0xE12FFF1E;     // bx lr
            nuint hh;
            Check(uc_hook_add(_uc, &hh, UC_HOOK_CODE, (delegate* unmanaged[Cdecl]<nint, ulong, uint, nint, void>)&StubThunk,
                (void*)GCHandle.ToIntPtr(_self), StubBase, StubBase + StubSize - 1), "hook stubs");
            Check(uc_hook_add(_uc, &hh, UC_HOOK_BLOCK, (delegate* unmanaged[Cdecl]<nint, ulong, uint, nint, void>)&DeadlineThunk,
                (void*)GCHandle.ToIntPtr(_self), 1, 0), "hook deadline");
            Check(uc_hook_add(_uc, &hh, UC_HOOK_MEM_READ_UNMAPPED | UC_HOOK_MEM_WRITE_UNMAPPED | UC_HOOK_MEM_FETCH_UNMAPPED,
                (delegate* unmanaged[Cdecl]<nint, int, ulong, int, long, nint, byte>)&UnmappedThunk,
                (void*)GCHandle.ToIntPtr(_self), 1, 0), "hook unmapped");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private Region Map(uint address, uint size)
    {
        var alloc = NativeMemory.AllocZeroed((nuint)size + 0x1000);
        var ptr = (byte*)(((nuint)alloc + 0xFFF) & ~(nuint)0xFFF);
        var r = new Region { Base = address, Size = size, Ptr = ptr, Alloc = alloc };
        _regions.Add(r);
        Check(uc_mem_map_ptr(_uc, address, size, UC_PROT_ALL, ptr), $"map 0x{address:X8}");
        return r;
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_runDepth != 0) throw new InvalidOperationException("cannot dispose an executing emulator");
        _disposed = true;
        if (_uc != 0) { uc_close(_uc); _uc = 0; }
        foreach (var r in _regions) NativeMemory.Free(r.Alloc);
        _regions.Clear();
        _lastRegion = default;
        if (_self.IsAllocated) _self.Free();
        GC.SuppressFinalize(this);
    }

    ~ArmMachine() => Dispose();

    // ---- memory helpers

    /// <summary>Host pointer to guest [address, address+length); stops the run if any of it is unmapped.</summary>
    public byte* Ptr(uint address, uint length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var r = _lastRegion;
        if (r.Ptr != null && address >= r.Base && (ulong)address + length <= (ulong)r.Base + r.Size) return r.Ptr + (address - r.Base);
        foreach (var x in _regions)
            if (address >= x.Base && (ulong)address + length <= (ulong)x.Base + x.Size)
            {
                _lastRegion = x;
                return x.Ptr + (address - x.Base);
            }
        throw new EmulationAbort($"unmapped host access 0x{address:X8} length {length}");
    }

    public uint R32(uint a) => Unsafe.ReadUnaligned<uint>(Ptr(a, 4));
    public void W32(uint a, uint v) => Unsafe.WriteUnaligned(Ptr(a, 4), v);
    public ushort R16(uint a) => Unsafe.ReadUnaligned<ushort>(Ptr(a, 2));
    public void W16(uint a, uint v) => Unsafe.WriteUnaligned(Ptr(a, 2), (ushort)v);
    public byte R8(uint a) => *Ptr(a, 1);
    public void W8(uint a, uint v) => *Ptr(a, 1) = (byte)v;
    public byte[] Read(uint a, uint n) => n == 0 ? Array.Empty<byte>() : new ReadOnlySpan<byte>(Ptr(a, n), (int)n).ToArray();
    public Span<byte> Span(uint a, uint n) => n == 0 ? Span<byte>.Empty : new Span<byte>(Ptr(a, n), (int)n);
    public void Write(uint a, ReadOnlySpan<byte> data) { if (data.Length > 0) data.CopyTo(Span(a, (uint)data.Length)); }
    public void Fill(uint a, uint n, byte v) { if (n > 0) Span(a, n).Fill(v); }
    /// <summary>Copies n bytes from s to d as a read followed by a write (memmove semantics, as the Python's rd/wr).</summary>
    public void Copy(uint d, uint s, uint n)
    {
        if (n == 0) return;
        var src = Span(s, n);
        src.CopyTo(Span(d, n));
    }

    /// <summary>NUL-terminated bytes at <paramref name="a"/>, at most <paramref name="limit"/> (the Python cstr's default
    /// limit is 4096; strings that long are cut there, as the Python cuts them).</summary>
    public byte[] CStr(uint a, int limit = 4096)
    {
        var buf = new List<byte>();
        while (buf.Count < limit)
        {
            uint addr = a + (uint)buf.Count;
            uint pageLeft = 0x1000 - (addr & 0xFFF);
            uint n = (uint)Math.Min(pageLeft, limit - buf.Count);
            var s = new ReadOnlySpan<byte>(Ptr(addr, n), (int)n);
            int z = s.IndexOf((byte)0);
            if (z >= 0) { buf.AddRange(s[..z].ToArray()); return buf.ToArray(); }
            buf.AddRange(s.ToArray());
        }
        return buf.ToArray();
    }

    public ulong ReadReg(int id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ulong v = 0;
        Check(uc_reg_read(_uc, id, &v), $"reg_read {id}");
        return v;
    }

    public void WriteReg(int id, ulong v)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Check(uc_reg_write(_uc, id, &v), $"reg_write {id}");
    }

    public uint Reg(int n) => (uint)ReadReg(UC_ARM_REG_R0 + n);
    public void SetReg(int n, uint v) => WriteReg(UC_ARM_REG_R0 + n, v);
    public uint Sp => (uint)ReadReg(UC_ARM_REG_SP);
    public uint Lr => (uint)ReadReg(UC_ARM_REG_LR);
    public uint Pc => (uint)ReadReg(UC_ARM_REG_PC);
    public void SetPc(uint v) => WriteReg(UC_ARM_REG_PC, v);
    public void SetLr(uint v) => WriteReg(UC_ARM_REG_LR, v);

    /// <summary>The n-th 32-bit argument of a hooked call (r0..r3 then the stack).</summary>
    public uint Arg(int n) => n < 4 ? Reg(n) : R32(Sp + 4u * (uint)(n - 4));

    public static double Dbl(uint lo, uint hi) => BitConverter.UInt64BitsToDouble(((ulong)hi << 32) | lo);

    public void SetDbl(double v)
    {
        ulong b = BitConverter.DoubleToUInt64Bits(v);
        SetReg(0, (uint)b);
        SetReg(1, (uint)(b >> 32));
    }

    public uint StaticAlloc(uint size, uint align = 16)
    {
        uint a = (_staticTop + align - 1) & ~(align - 1);
        _staticTop = a + size;
        if (_staticTop > StaticBase + StaticSize) throw new EmulationAbort("static area exhausted");
        return a;
    }

    // ---- heap (fixture boundary, as emu_elf: first fit, 16-byte granules, fresh memory filled with Poison)

    private readonly List<(uint A, uint S)> _freeList = new();
    private readonly Dictionary<uint, uint> _blocks = new();
    private uint _heapTop = HeapBase;
    private uint _heapMark;
    private List<(uint A, uint S)>? _freeMark;
    private Dictionary<uint, uint>? _blocksMark;

    public uint Malloc(ulong request)
    {
        ulong size = Math.Max((request + 15) & ~15UL, 16);
        for (int i = 0; i < _freeList.Count; i++)
        {
            var (a, s) = _freeList[i];
            if (s >= size)
            {
                _freeList.RemoveAt(i);
                if (s > size) _freeList.Add((a + (uint)size, s - (uint)size));
                _blocks[a] = (uint)size;
                return a;
            }
        }
        uint top = _heapTop;
        if (top + size > (ulong)HeapBase + HeapSize) return 0;
        _heapTop += (uint)size;
        Fill(top, (uint)size, Poison);
        _blocks[top] = (uint)size;
        return top;
    }

    public void Free(uint a)
    {
        if (a != 0 && _blocks.Remove(a, out var size)) _freeList.Add((a, size));
    }

    /// <summary>Size of a live heap block (0 if <paramref name="a"/> is not one).</summary>
    public uint BlockSize(uint a) => _blocks.TryGetValue(a, out var s) ? s : 0;

    /// <summary>Remember the heap after library initialisation; <see cref="ResetHeap"/> returns to it.</summary>
    public void MarkHeapBase()
    {
        _heapMark = _heapTop;
        _freeMark = new(_freeList);
        _blocksMark = new(_blocks);
    }

    /// <summary>Forget every allocation since <see cref="MarkHeapBase"/> and refill that memory with the poison byte.</summary>
    public void ResetHeap()
    {
        if (_freeMark is null || _blocksMark is null) throw new InvalidOperationException("MarkHeapBase was not called");
        if (_heapTop > _heapMark) Fill(_heapMark, _heapTop - _heapMark, Poison);
        foreach (var (a, s) in _freeMark) Fill(a, s, Poison);
        _heapTop = _heapMark;
        _freeList.Clear(); _freeList.AddRange(_freeMark);
        _blocks.Clear(); foreach (var kv in _blocksMark) _blocks[kv.Key] = kv.Value;
    }

    // ---- relocation / symbol binding

    /// <summary>Applies every library's relocations, libraries in resolution order and relocations in DT_REL then DT_JMPREL
    /// order (stub and data-symbol addresses are handed out in that order, as in the Python).</summary>
    public void Relocate()
    {
        foreach (var lib in _libs)
        {
            var syms = lib.Elf.DynamicSymbols;
            foreach (var rel in lib.Elf.Relocations)
            {
                uint a = lib.Base + rel.Offset;
                if (rel.Type == ElfImage.R_ARM_RELATIVE)
                {
                    W32(a, R32(a) + lib.Base);
                    continue;
                }
                if (rel.SymbolIndex == 0)
                    throw new InvalidDataException($"{lib.Name}: relocation type {rel.Type} without a symbol at 0x{rel.Offset:X}");
                var target = Resolve(syms[(int)rel.SymbolIndex].Name, lib);
                if (rel.Type == ElfImage.R_ARM_ABS32) W32(a, target + R32(a));
                else if (rel.Type is ElfImage.R_ARM_GLOB_DAT or ElfImage.R_ARM_JUMP_SLOT) W32(a, target);
                else throw new InvalidDataException($"{lib.Name}: unsupported relocation type {rel.Type}");
            }
        }
    }

    private static readonly HashSet<string> PreferHook = new(StringComparer.Ordinal) { "__cxa_throw" };

    public uint Resolve(string name, LoadedLibrary owner)
    {
        if (Overrides.ContainsKey(name) || PreferHook.Contains(name)) return Stub(name);
        if (owner.Symbols.TryGetValue(name, out var own) && own != 0) return own;
        foreach (var l in _libs)
            if (l.Symbols.TryGetValue(name, out var v)) return v;
        if (Libc.ContainsKey(name)) return Stub(name);
        if (DataSymbols.TryGetValue(name, out var make))
        {
            if (!_dataAddr.TryGetValue(name, out var d)) _dataAddr[name] = d = make(this);
            return d;
        }
        return Stub(name);
    }

    /// <summary>The stub address for an import name (ARM-mode <c>bx lr</c>, 4-byte aligned).</summary>
    public uint Stub(string name)
    {
        if (!_stubAddr.TryGetValue(name, out var a))
        {
            a = _nextStub;
            _nextStub += 4;
            if (_nextStub > StubBase + StubSize) throw new EmulationAbort("stub area exhausted");
            _stubAddr[name] = a;
            _stubNames[a] = name;
        }
        return a;
    }

    public uint Symbol(string name)
    {
        foreach (var l in _libs)
            if (l.Symbols.TryGetValue(name, out var v)) return v;
        throw new KeyNotFoundException(name);
    }

    /// <summary>Runs a library's DT_INIT, then its DT_INIT_ARRAY entries (raw values plus base; 0 and -1 skipped).</summary>
    public void RunInitializers(string libName)
    {
        var lib = Library(libName);
        var fns = new List<uint>();
        if (lib.Elf.Init != 0) fns.Add(lib.Elf.Init);
        fns.AddRange(lib.Elf.InitArray);
        foreach (var f in fns)
            if (f != 0 && f != 0xFFFFFFFF) Call(lib.Base + f);
    }

    // ---- hook dispatch

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void DeadlineThunk(nint uc, ulong address, uint size, nint user)
    {
        var m = (ArmMachine)GCHandle.FromIntPtr(user).Target!;
        if (m._executionFailure is not null)
        {
            m._pending ??= new EmulationAbort("emulator is faulted after a previous call failed", m._executionFailure);
            uc_emu_stop(uc);
        }
        else if (m._deadline != 0 && Stopwatch.GetTimestamp() >= m._deadline)
        {
            m._pending ??= new EmulationAbort($"M1-034: call to 0x{m._activeCall:X} exceeded its emulation timeout at pc 0x{address:X}");
            uc_emu_stop(uc);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void StubThunk(nint uc, ulong address, uint size, nint user)
    {
        var m = (ArmMachine)GCHandle.FromIntPtr(user).Target!;
        try
        {
            m.OnStub((uint)address);
        }
        catch (Exception e)
        {
            m._pending ??= e;
            uc_emu_stop(uc);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void AddrThunk(nint uc, ulong address, uint size, nint user)
    {
        var m = (ArmMachine)GCHandle.FromIntPtr(user).Target!;
        try
        {
            if (m._addrHooks.TryGetValue((uint)address, out var fn)) m.Finish(fn(m));
        }
        catch (Exception e)
        {
            m._pending ??= e;
            uc_emu_stop(uc);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static byte UnmappedThunk(nint uc, int type, ulong address, int size, long value, nint user)
    {
        var m = (ArmMachine)GCHandle.FromIntPtr(user).Target!;
        try
        {
            m._fault = $"unmapped access 0x{address:X} size {size} access {type} at pc 0x{m.Pc:X} lr 0x{m.Lr:X}";
        }
        catch
        {
        }
        return 0;
    }

    /// <summary>Import stand-in calls per name since the machine was made (diagnostics, for speed work).</summary>
    public Dictionary<string, long> StubCalls { get; } = new(StringComparer.Ordinal);

    private void OnStub(uint address)
    {
        if (!_stubNames.TryGetValue(address, out var name)) return;
        StubCalls[name] = StubCalls.GetValueOrDefault(name) + 1;
        if (!Overrides.TryGetValue(name, out var fn) && !Libc.TryGetValue(name, out fn))
            throw new EmulationAbort($"unmodelled import {name} (lr 0x{Lr:X})");
        Finish(fn(this));
    }

    private void Finish(StubResult r)
    {
        if (r.Kind == StubResult.K.NoReturn) return;
        if (r.Kind == StubResult.K.Value) SetReg(0, r.Value);
        SetPc(Lr);
    }

    /// <summary>Runs <paramref name="fn"/> instead of the code at <paramref name="address"/> (returns to lr).</summary>
    public void HookAddress(uint address, ImportHandler fn)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint a = address & ~1u;
        _addrHooks[a] = fn;
        nuint hh;
        Check(uc_hook_add(_uc, &hh, UC_HOOK_CODE, (delegate* unmanaged[Cdecl]<nint, ulong, uint, nint, void>)&AddrThunk,
            (void*)GCHandle.ToIntPtr(_self), a, a), "hook address");
    }

    // ---- running

    /// <summary>Calls <paramref name="addr"/> (Thumb bit included for Thumb code) with up to four register arguments and
    /// <paramref name="stack"/> words above sp; returns r0. r0..r12 start at zero; sp starts at the top of the stack area
    /// less 0x100; lr is the return sentinel. <paramref name="count"/> 0 means no instruction budget. Unicorn's count is
    /// not aggregated across nested calls; <see cref="CallTimeout"/> is shared across the whole call tree.</summary>
    public uint Call(uint addr, uint[]? args = null, uint[]? stack = null, ulong count = 0, uint fpscr = 0)
    {
        EnsureRunnable();
        if (_runDepth != 0) throw new InvalidOperationException("use NestedCall inside an emulated call");
        _runDepth++;
        try
        {
            StartDeadline(addr);
            args ??= Array.Empty<uint>();
            stack ??= Array.Empty<uint>();
            if (args.Length > 4) throw new ArgumentException("pass arguments beyond r3 on the stack");
            for (int n = 0; n <= 12; n++) SetReg(n, 0);
            uint sp = StackBase + StackSize - 0x100;
            sp -= 4u * (uint)stack.Length;
            sp &= ~7u;
            for (int i = 0; i < stack.Length; i++) W32(sp + 4u * (uint)i, stack[i]);
            for (int i = 0; i < args.Length; i++) SetReg(i, args[i]);
            WriteReg(UC_ARM_REG_SP, sp);
            SetLr(RetAddr);
            WriteReg(UC_ARM_REG_FPSCR, fpscr);
            _pending = null;
            _fault = "";
            int err = uc_emu_start(_uc, addr, RetAddr, 0, (nuint)count);
            TakePending();
            EnsureRunnable();
            CheckDeadline();
            if (err != 0)
                throw new EmulationAbort($"unicorn: {ErrorText(err)} {_fault} at pc 0x{Pc:X} lr 0x{Lr:X}");
            if (Pc != RetAddr)
                throw new EmulationAbort($"call to 0x{addr:X} stopped at pc 0x{Pc:X} (instruction budget?)");
            return Reg(0);
        }
        catch (Exception e)
        {
            _executionFailure ??= e;
            throw;
        }
        finally
        {
            _deadline = 0;
            _runDepth--;
            GC.KeepAlive(this);
        }
    }

    private void EnsureRunnable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_executionFailure is not null)
            throw new EmulationAbort("emulator is faulted after a previous call failed", _executionFailure);
    }

    private void StartDeadline(uint address)
    {
        if (CallTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(CallTimeout));
        double ticks = CallTimeout.TotalSeconds * Stopwatch.Frequency;
        long now = Stopwatch.GetTimestamp();
        _deadline = ticks >= long.MaxValue - now ? long.MaxValue : now + Math.Max(1, (long)ticks);
        _activeCall = address;
    }

    private void CheckDeadline()
    {
        if (_deadline != 0 && Stopwatch.GetTimestamp() >= _deadline)
            throw new EmulationAbort($"M1-034: call to 0x{_activeCall:X} exceeded its emulation timeout at pc 0x{Pc:X}");
    }

    private void TakePending()
    {
        var p = _pending;
        _pending = null;
        if (p is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(p).Throw();
    }

    private static readonly int[] SavedRegs = BuildSavedRegs();

    private static int[] BuildSavedRegs()
    {
        var r = new List<int>();
        for (int i = 0; i <= 12; i++) r.Add(UC_ARM_REG_R0 + i);
        r.AddRange(new[] { UC_ARM_REG_SP, UC_ARM_REG_LR, UC_ARM_REG_PC, UC_ARM_REG_CPSR });
        return r.ToArray();
    }

    /// <summary>Calls emulated code from inside a stand-in (for example a qsort comparator): saves r0..r12, sp, lr, pc and
    /// cpsr, runs <paramref name="fn"/> on the stack 0x800 below the current sp, and restores them. Mirrors the Acapela
    /// probe's <c>nested_call</c>.</summary>
    public uint NestedCall(uint fn, params uint[] args)
    {
        EnsureRunnable();
        bool ownsDeadline = _deadline == 0;
        uint previousCall = _activeCall;
        _runDepth++;
        try
        {
            if (ownsDeadline) StartDeadline(fn);
            else _activeCall = fn;
            const uint ret2 = RetAddr + 0x100;
            var saved = new ulong[SavedRegs.Length];
            for (int i = 0; i < SavedRegs.Length; i++) saved[i] = ReadReg(SavedRegs[i]);
            for (int k = 0; k < args.Length; k++) SetReg(k, args[k]);
            WriteReg(UC_ARM_REG_SP, (saved[13] - 0x800) & ~7UL);
            SetLr(ret2);
            int err = uc_emu_start(_uc, fn, ret2, 0, 0);
            TakePending();
            EnsureRunnable();
            CheckDeadline();
            if (err != 0)
                throw new EmulationAbort($"unicorn (nested call 0x{fn:X}): {ErrorText(err)} {_fault} at pc 0x{Pc:X} lr 0x{Lr:X}");
            if (Pc != ret2)
                throw new EmulationAbort($"nested call to 0x{fn:X} stopped at pc 0x{Pc:X}");
            uint r = Reg(0);
            for (int i = 0; i < SavedRegs.Length; i++) WriteReg(SavedRegs[i], saved[i]);
            return r;
        }
        catch (Exception e)
        {
            _executionFailure ??= e;
            throw;
        }
        finally
        {
            if (ownsDeadline) _deadline = 0;
            _activeCall = previousCall;
            _runDepth--;
            GC.KeepAlive(this);
        }
    }
}
