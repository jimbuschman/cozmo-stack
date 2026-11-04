using System.Buffers.Binary;
using System.Text;

namespace Cozmo.Robot.Emulation;

/// <summary>
/// The parts of a 32-bit little-endian ARM ELF shared object the loader needs, read through the dynamic segment: the PT_LOAD
/// segments, the dynamic symbols (count from DT_HASH), the DT_REL and DT_JMPREL relocations in that order (lief's order, which
/// <c>emu_elf.py</c> iterates), and DT_INIT / DT_INIT_ARRAY.
/// </summary>
public sealed class ElfImage
{
    public const int R_ARM_ABS32 = 2;
    public const int R_ARM_GLOB_DAT = 21;
    public const int R_ARM_JUMP_SLOT = 22;
    public const int R_ARM_RELATIVE = 23;

    public readonly record struct Segment(uint VirtualAddress, uint MemorySize, uint FileOffset, uint FileSize);
    public readonly record struct Symbol(string Name, uint Value, ushort SectionIndex);
    public readonly record struct Relocation(uint Offset, int Type, uint SymbolIndex);

    public byte[] Raw { get; }
    public IReadOnlyList<Segment> Loads { get; }
    public IReadOnlyList<Symbol> DynamicSymbols { get; }
    public IReadOnlyList<Relocation> Relocations { get; }
    /// <summary>DT_INIT (0 when absent) and the raw, unrelocated DT_INIT_ARRAY entries.</summary>
    public uint Init { get; }
    public IReadOnlyList<uint> InitArray { get; }
    /// <summary>Highest vaddr + memsz over the PT_LOAD segments.</summary>
    public uint End { get; }

    public ElfImage(byte[] raw)
    {
        Raw = raw;
        if (raw.Length < 52 || raw[0] != 0x7F || raw[1] != (byte)'E' || raw[2] != (byte)'L' || raw[3] != (byte)'F')
            throw new InvalidDataException("not an ELF file");
        if (raw[4] != 1 || raw[5] != 1) throw new InvalidDataException("not a 32-bit little-endian ELF");
        if (U16(18) != 40) throw new InvalidDataException("not an ARM ELF");
        uint phoff = U32(28);
        int phentsize = U16(42), phnum = U16(44);
        var loads = new List<Segment>();
        uint dynOff = 0, dynSize = 0;
        for (int i = 0; i < phnum; i++)
        {
            int p = (int)phoff + i * phentsize;
            uint type = U32(p), off = U32(p + 4), va = U32(p + 8), filesz = U32(p + 16), memsz = U32(p + 20);
            if (type == 1) loads.Add(new Segment(va, memsz, off, filesz));
            else if (type == 2) { dynOff = off; dynSize = filesz; }
        }
        Loads = loads;
        End = loads.Max(s => s.VirtualAddress + s.MemorySize);

        var dyn = new Dictionary<int, uint>();
        for (uint p = dynOff; p + 8 <= dynOff + dynSize; p += 8)
        {
            int tag = (int)U32((int)p);
            if (tag == 0) break;
            dyn.TryAdd(tag, U32((int)p + 4));
        }
        uint Get(int tag) => dyn.TryGetValue(tag, out var v) ? v : 0;
        const int DT_HASH = 4, DT_STRTAB = 5, DT_SYMTAB = 6, DT_INIT = 12, DT_REL = 17, DT_RELSZ = 18, DT_PLTRELSZ = 2,
            DT_JMPREL = 23, DT_INIT_ARRAY = 25, DT_INIT_ARRAYSZ = 27;

        if (Get(DT_HASH) == 0) throw new InvalidDataException("ELF has no DT_HASH (symbol count unknown)");
        uint nsyms = U32((int)FileOffset(Get(DT_HASH)) + 4);
        int symtab = (int)FileOffset(Get(DT_SYMTAB)), strtab = (int)FileOffset(Get(DT_STRTAB));
        var syms = new Symbol[nsyms];
        for (int i = 0; i < nsyms; i++)
        {
            int s = symtab + 16 * i;
            syms[i] = new Symbol(CString(strtab + (int)U32(s)), U32(s + 4), U16(s + 14));
        }
        DynamicSymbols = syms;

        var rels = new List<Relocation>();
        void ReadRel(uint va, uint size)
        {
            if (va == 0) return;
            int f = (int)FileOffset(va);
            for (int k = 0; k < size / 8; k++)
            {
                uint off = U32(f + 8 * k), info = U32(f + 8 * k + 4);
                rels.Add(new Relocation(off, (int)(info & 0xFF), info >> 8));
            }
        }
        ReadRel(Get(DT_REL), Get(DT_RELSZ));
        ReadRel(Get(DT_JMPREL), Get(DT_PLTRELSZ));
        Relocations = rels;

        Init = Get(DT_INIT);
        var init = new List<uint>();
        if (Get(DT_INIT_ARRAY) != 0)
        {
            int f = (int)FileOffset(Get(DT_INIT_ARRAY));
            for (int k = 0; k < Get(DT_INIT_ARRAYSZ) / 4; k++) init.Add(U32(f + 4 * k));
        }
        InitArray = init;
    }

    /// <summary>File offset of a virtual address inside a PT_LOAD segment's file image.</summary>
    public uint FileOffset(uint va)
    {
        foreach (var s in Loads)
            if (va >= s.VirtualAddress && va < s.VirtualAddress + s.FileSize)
                return va - s.VirtualAddress + s.FileOffset;
        throw new InvalidDataException($"virtual address 0x{va:X} is not in a loaded file segment");
    }

    private ushort U16(int o) => BinaryPrimitives.ReadUInt16LittleEndian(Raw.AsSpan(o));
    private uint U32(int o) => BinaryPrimitives.ReadUInt32LittleEndian(Raw.AsSpan(o));

    private string CString(int o)
    {
        int e = Array.IndexOf(Raw, (byte)0, o);
        return Encoding.Latin1.GetString(Raw, o, e - o);
    }
}
