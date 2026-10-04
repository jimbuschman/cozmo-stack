// fidelity: M6-025, M6-024, M6-001
//
// Status of this file (read before relying on it):
// - Nothing in production constructs WwiseBankLoader. The offline WwiseBank.Parse is the live bank reader and is a non-faithful parallel copy of this loader; resolve at wiring.
// - The production wrapper 0x9B7CD4 (called from 0x9BA330 and 0x9BB83C) has NO C#. MISSING. Read from 0x9B7D2C..0x9B7EF0 and 0x9B7E24..0x9B7E98: it calls 0x9B74D8 (0x9B7D28), then
//   * result 1 (0x9B7D38 -> 0x9B7EB8): [bank+0x50] |= 1 and the registry insert 0xA68544(BM+0x44, ...), then 0x9B7EF0 (r4 = 1);
//   * result 0x45 (0x9B7D2C -> 0x9B7EF0): r4 = 1, no other work;
//   * any other result (0x9B7D40..0x9B7E20): with a bank, the HIRC object destructors (vt+0xC loop 0x9B7D50..0x9B7DD4), the array [bank+0x3C] freed and [+0x40] / [+0x3C] / [+0x44] zeroed (0x9B7DE4..0x9B7E08),
//     bank vt+0(1) (0x9B7E20), r4 = result; with no bank r4 = result.
//   * then, on EVERY outcome (0x9B7E24): the type word [r7] picks the argument set (jump table 0x9B7E28..0x9B7E50) and 0xA68150(BM+0x7C, ...) runs (0x9B7E8C) and [BM+0x78] = the 0x9B74D8 result (0x9B7E94);
//     the wrapper returns r4 (1 for success and 0x45, the result for a failure).
// - 0x9B74D8 has two other direct callers, also with no C#: the sibling wrapper 0x9B7F0C (bl at 0x9B8004; called from 0x9B8208, 0x9BA504, 0x9BB9F8) and 0x9B8694. All MISSING / unread.
// - 0x9B2314 (LoadMemoryChunkA9B2314) has no live caller in the image: it is test-only.
// - The type-0 open by name (0x9BC17C) and by id (0x9BC348) are unread: the OpenReader seam throws. IWwiseReaderStream has no production implementation.
// - Real banks cannot load yet: HIRC object types 1..9 and 14..23 (the per-type creators) and DestroyPool's tail (0xA7AEC4 after its check) are unread and throw.
using System.Text;

namespace Cozmo.Robot.Animation.Wwise;

// The bank loader 0x9B74D8 and the chunk handlers of M6-wwise-bank.md C34.4 (rows K1 to K15 of research/20261003-B-M6b-4-live-bodies-4.md with its verification's corrections 9 to 13): the loader's registry
// lookup (K13), the bank object creation (K12), the reader setup and BKHD (K2), the chunk dispatch with the two "mode" arguments (K1), DIDX / DATA (K4, K5, window variants), INIT (K6), ENVS (K7) and the
// writer 0x9CC5A0 it calls, PLAT (K8) and 0x9A6518, HIRC (K9) and its hook (K10); the bank release (K15) is WwiseMediaTable.ReleaseBankA9B47D8 and the writer cleanup (K14) WwiseMediaTable.CleanupA9B45D8.
// Every function was disassembled in libcozmoEngine.so (ARM) when this file was written.
//
// Production entry. Engine: Anki LoadSoundbank -> 0x9A33D8 posts command 0 -> BM vt+0x10 = 0x9BB65C -> 0x9BB834 -> 0x9B7CD4 -> the loader 0x9B74D8 (type 0, mode 0, M2). The C# counterpart is
// WwiseBankLoader.Load9B74D8; nothing in production constructs it yet (the bank-load wiring, C30.W, is parked); it is exercised by tests, with re-analysis/tools/emu/emu_bankload.py as the oracle.
//
// Replaces: WwiseBankLoader.LoadMode0 / ChunkHandler / ReadBkhd (the stand-in seams for BKHD, INIT, ENVS, PLAT and HIRC), WwiseMemoryBankStream and WwiseBankChunk.
//
// Unread, named (throw WwiseMissingBehaviourException when reached unset): the reader's open (0x9BC17C by name, 0x9BC348 by id) and the std stream behind it (IWwiseReaderStream), STMG 0x9B0B14, STID 0x9B2410 (K11,
// not adopted), the streamed DATA load 0x9B6C34, the per-type HIRC creators (0x9B2B98, 0x9B3DF4, ... , K9), the external plug-in loader 0xA57238, 0xA68150, DestroyPool's tail, the bank registry's insertion, the XOR key's
// writer (the key is zero in the image, so no bank is XOR-protected), and the pool ids the pool-creating init sets.

/// <summary>The globals the chunk handlers read and write (the sound engine's <c>0x108D8xx</c> / <c>0x108D9xx</c> block). The engine's initialisers are unread; the host sets what a handler needs.</summary>
public sealed class WwiseBankGlobals
{
    /// <summary><c>[0x108D9A0..0x108D9AF]</c>: the BKHD XOR key; the first word is also the enable (<c>0x9B2268..0x9B2278</c>). Zero in the image; the writer is not found (UNKNOWN), so the default is the engine's: disabled.</summary>
    public uint[] XorKey { get; } = new uint[4];

    /// <summary><c>[0x108D9E8]</c>: the plug-in ids (12-byte records, key at <c>+0</c>) INIT looks up first.</summary>
    public HashSet<uint> PluginIdsE8 { get; } = new();

    /// <summary><c>[0x108D9DC]</c>: the plug-in registry INIT looks up second (25 records in the image, C31.2).</summary>
    public HashSet<uint> PluginIdsDC { get; } = new();

    /// <summary><c>0xA57238(name)</c> (<c>0x9B4120</c>): the external plug-in library load INIT calls for an id found in neither registry. Not adopted; required then.</summary>
    public Action<string?>? ExternalPluginLoadA57238 { get; set; }

    /// <summary>The value of <c>[0x108D904]</c> being non-zero: ENVS returns 2 without it (<c>0x9B2998..0x9B29A4</c>).</summary>
    public bool Envs904Present
    {
        get => _envs904Present ?? throw new WwiseMissingBehaviourException(
            "M6-025 K7: [0x108D904] is the manager pointer the unread init writes (non-zero in a running engine, read at 0x9B2994); supply WwiseBankGlobals.Envs904Present");
        set => _envs904Present = value;
    }
    private bool? _envs904Present;

    /// <summary><c>[0x108D8FC]</c>: the ENVS table ENVS writes into.</summary>
    public WwiseEnvsTable? EnvsTable { get; set; }

    /// <summary><c>[0x108D868+0x7C]</c>: the platform string PLAT compares or stores (<c>0x9A6518</c>); null until the first PLAT.</summary>
    public string? PlatformString { get; set; }

    /// <summary>
    /// <c>[0x108D968]</c> (<c>[0x108D868+0x100]</c>): the HIRC hook (<c>0x9B3EBC</c>). The music engine's init <c>0x97D72C</c> installs <c>0x984930</c> (K10); null is the engine's state before that init, so the music objects
    /// (types 10..13) are skipped. <c>(type, size, bank, id) -&gt; result</c>.
    /// </summary>
    public Func<byte, uint, WwiseMediaBank, uint, int>? HircHook
    {
        get => _hircHookSet ? _hircHook : throw new WwiseMissingBehaviourException(
            "M6-025 K9: [0x108D968] is installed by the music-engine init 0x97D72C (0x984930) and is null only before it; supply WwiseBankGlobals.HircHook (null to say it is not installed)");
        set { _hircHook = value; _hircHookSet = true; }
    }
    private Func<byte, uint, WwiseMediaBank, uint, int>? _hircHook;
    private bool _hircHookSet;

    /// <summary><c>[0x108D90C+0x14]</c> (InitSettings): the pool id a mode != 0 bank creation stores in <c>[bank+0x24]</c> (<c>0x9B764C..0x9B765C</c>). Written by the unread init, so there is no default: a mode != 0 creation without it stops.</summary>
    public int? MediaPoolId14 { get; set; }
}

/// <summary>
/// The ENVS table <c>[0x108D8FC]</c> (K7, <c>0x9B2988</c>, <c>0x9CC5A0</c>): bytes 0..5 the enabled flags (index <c>3 * type + idx</c>), then six entries of 12 bytes from <c>+8</c>: the points pointer, the count <c>+4</c>
/// and the scaling <c>+8</c>. Points are 12 bytes (<c>{float x, float y, u32 interp}</c>).
/// </summary>
public sealed class WwiseEnvsTable
{
    /// <summary>The enabled bytes (<c>[table + 3 * type + idx]</c>).</summary>
    public byte[] Enabled { get; } = new byte[6];

    /// <summary>The points of each (type, idx), <c>12 * count</c> bytes, or null.</summary>
    public byte[]?[] Points { get; } = new byte[6][];

    /// <summary>The point counts (<c>entry+4</c>).</summary>
    public uint[] Count { get; } = new uint[6];

    /// <summary>The scalings (<c>entry+8</c>).</summary>
    public uint[] Scaling { get; } = new uint[6];

    /// <summary>
    /// <c>0x9CC5A0(table, type, idx, n, buf, scaling)</c>: an old points block is freed and cleared; a null buffer or <paramref name="n"/> of 0 stores count 0 and scaling 0 and returns 0x1F; otherwise <c>12 * n</c> bytes
    /// are allocated (<paramref name="allocationFails"/> returns 0x34 with count 0), the points copied, the count and the scaling stored, and for <c>idx == 0</c> a scaling of 0 becomes 4 and a scaling of 2 adds 1.0f to the second
    /// float of each point and stores 0 (<c>0x9CC5A0..0x9CC71C</c>). Returns 1.
    /// </summary>
    public int Write9CC5A0(int type, int idx, uint n, byte[]? buf, byte scaling, Func<bool>? allocationFails)
    {
        int k = 3 * type + idx;                                                    // 0x9CC5A4..0x9CC5C8
        Points[k] = null;                                                          // 0x9CC5CC..0x9CC5F4: the old block is freed
        Count[k] = 0;                                                              // 0x9CC618..0x9CC624: the count and the scaling are zeroed before anything else
        Scaling[k] = 0;                                                            // 0x9CC628
        if (buf is null || n == 0) return 0x1F;                                    // 0x9CC600..0x9CC620
        if (allocationFails?.Invoke() == true) return 0x34;                        // 0x9CC658..0x9CC668 (points, count already 0)
        var copy = new byte[12 * n];
        Array.Copy(buf, copy, copy.Length);                                        // 0x9CC678 bl memcpy
        Points[k] = copy;
        Count[k] = n;                                                              // 0x9CC684
        Scaling[k] = scaling;                                                      // 0x9CC68C
        if (idx != 0) return 1;                                                    // 0x9CC680 cmp r7,#0; bne 0x9CC6A4
        if (scaling == 0) { Scaling[k] = 4; return 1; }                            // 0x9CC694..0x9CC6C0
        if (scaling == 2)                                                          // 0x9CC69C..0x9CC6A0
        {
            for (uint i = 0; i < n; i++)                                           // 0x9CC6E4..0x9CC704
            {
                int o = (int)(12 * i + 4);
                float y = BitConverter.ToSingle(copy, o) + 1f;                     // 0x9CC6F0..0x9CC6F4 vadd.f32 s15,s15,#1.0
                BitConverter.GetBytes(y).CopyTo(copy, o);                          // 0x9CC6FC
            }
            Scaling[k] = 0;                                                        // 0x9CC70C..0x9CC718
        }
        return 1;
    }
}

/// <summary>The BKHD header the loader reads (<c>0x9B21F4</c>): version, bank id, language id, alignment, device-allocated and project id.</summary>
public sealed class WwiseBkhdHeader
{
    /// <summary><c>+0</c>.</summary>
    public uint Version { get; set; }

    /// <summary><c>+4</c>.</summary>
    public uint BankId { get; set; }

    /// <summary><c>+8</c>.</summary>
    public uint Language { get; set; }

    /// <summary><c>+0xC</c> (u16).</summary>
    public ushort Alignment { get; set; }

    /// <summary><c>+0xE</c> (u16).</summary>
    public ushort DeviceAllocated { get; set; }

    /// <summary><c>+0x10</c>.</summary>
    public uint ProjectId { get; set; }
}

/// <summary>How the loader opens its reader when it is not the memory setup: by file name (<c>0x9BC17C</c>) or by id (<c>0x9BC348</c>). Both bodies are unread.</summary>
public enum WwiseReaderOpen
{
    /// <summary><c>0x9BC17C(R, name, 0, flags)</c> (<c>0x9B75F0..0x9B7600</c>).</summary>
    ByName9BC17C,

    /// <summary><c>0x9BC348(R, id, ...)</c> (<c>0x9B7B84..0x9B7BA0</c>, <c>0x9B7838..0x9B7854</c>).</summary>
    ById9BC348,
}

/// <summary>The arguments of <c>0x9B74D8</c> (r1..r3 and the stack words the loader reads).</summary>
public sealed class WwiseBankLoadRequest
{
    /// <summary><c>r2</c>: the bank id (<c>[bank+8]</c>, the registry key).</summary>
    public uint Id { get; init; }

    /// <summary><c>r3</c>: true when the file-name argument is non-zero.</summary>
    public bool HasName { get; init; }

    /// <summary>Stack word 2 (<c>[sp+0xA0]</c>, stored at <c>[sp+8]</c>): the open flags the file open takes.</summary>
    public uint OpenFlags { get; init; }

    /// <summary>Stack word 3 (<c>[sp+0xA4]</c>, <c>r7</c>): the load type; 1 opens by id, 2 and 3 are memory loads, 2 also keys the registry by <see cref="LanguageOrMemory"/> and maps the window pointers.</summary>
    public int Type { get; init; }

    /// <summary>Stack word 4 (<c>[sp+0xA8]</c>): the pool id a mode-0 creation stores in <c>[bank+0x24]</c> (-1 from LoadSoundbank).</summary>
    public int PoolArg { get; init; } = -1;

    /// <summary>Stack word 5 (<c>[sp+0xAC]</c>, <c>sb</c>): the language key for type 2 (also the memory pointer of the memory setup).</summary>
    public uint LanguageOrMemory { get; init; }

    /// <summary>The memory a type 2 or 3 load of mode 0 reads from (<c>0x9BBB90(R, sb, [sp+0xB0])</c>: the bytes and the length stack word 6).</summary>
    public byte[]? Memory { get; init; }

    /// <summary>Stack word 6 (<c>[sp+0xB0]</c>): the memory length.</summary>
    public uint MemoryLength { get; init; }

    /// <summary>Stack word 10 (<c>[sp+0xC0]</c>): the load mode (the DIDX / DATA tables: 0, 1 and 3 load a DIDX, 2 skips it; DATA 0 loads in memory, 1 and 3 stream, 2 skips).</summary>
    public int Mode { get; init; }

    /// <summary>Stack word 11 (<c>[sp+0xC4]</c>, byte): the reference flag (<c>[bank+0x54]++</c> on an existing bank, <c>[bank+0x54]</c> on a mode != 0 creation).</summary>
    public bool AddRef { get; init; }

    /// <summary>Stack word 12 (<c>[sp+0xC8]</c>, byte): the flag the streamed DATA load <c>0x9B6C34</c> takes.</summary>
    public byte Flag12 { get; init; }
}

/// <summary>The bank loader <c>0x9B74D8</c> (C34.4).</summary>
public sealed class WwiseBankLoader
{
    /// <summary>'STMG' (<c>0x9B77A8</c>).</summary>
    public const uint Stmg = 0x474D5453;
    /// <summary>'PLAT' (<c>0x9B77AC</c>).</summary>
    public const uint Plat = 0x54414C50;
    /// <summary>'ENVS' (<c>0x9B77BC</c>).</summary>
    public const uint Envs = 0x53564E45;
    /// <summary>'INIT' (<c>0x9B77B0</c>).</summary>
    public const uint Init = 0x54494E49;
    /// <summary>'DIDX' (<c>0x9B77B4</c>).</summary>
    public const uint Didx = 0x58444944;
    /// <summary>'HIRC' (<c>0x9B7970</c>).</summary>
    public const uint Hirc = 0x43524948;
    /// <summary>'STID' (<c>0x9B7980</c>).</summary>
    public const uint Stid = 0x44495453;
    /// <summary>'DATA' (<c>0x9B7990</c>).</summary>
    public const uint Data = 0x41544144;
    /// <summary>'BKHD' (<c>0x9B2244</c>).</summary>
    public const uint Bkhd = 0x44484B42;

    /// <param name="table">The media table BM.</param>
    /// <param name="globals">The sound-engine globals the handlers use.</param>
    public WwiseBankLoader(WwiseMediaTable table, WwiseBankGlobals? globals = null)
    {
        Table = table ?? throw new ArgumentNullException(nameof(table));
        Globals = globals ?? new WwiseBankGlobals();
        Reader = new WwiseBankReader(table.Memory);
    }

    /// <summary>The media table.</summary>
    public WwiseMediaTable Table { get; }

    /// <summary>The globals.</summary>
    public WwiseBankGlobals Globals { get; }

    /// <summary>The reader <c>R = BM+4</c> every handler reads through.</summary>
    public WwiseBankReader Reader { get; }

    /// <summary><c>[BM+0x64]</c> (byte): <c>(u16 at hdr+0xC != 0)</c> BKHD stores on every exit (<c>0x9B2224..0x9B2230</c>).</summary>
    public byte BkhdAlignedFlag64 { get; private set; }

    /// <summary>The reader open of every load that is not the memory setup (<c>0x9BC17C</c>, <c>0x9BC348</c>): sets the reader's stream and window and returns 1. Unread; required then.</summary>
    public Func<WwiseBankReader, WwiseBankLoadRequest, WwiseReaderOpen, int>? OpenReader { get; set; }

    /// <summary>STMG <c>0x9B0B14(BM)</c> (<c>0x9B7AD8</c>), called for a non-zero chunk size. Not adopted; required then.</summary>
    public Func<WwiseBankReader, int>? StmgA9B0B14 { get; set; }

    /// <summary>STID <c>0x9B2410(BM, size, bank)</c> (<c>0x9B7A98</c>). K11 is not adopted; required then.</summary>
    public Func<WwiseBankReader, uint, WwiseMediaBank, int>? StidA9B2410 { get; set; }

    /// <summary>The streamed DATA load <c>0x9B6C34(BM, bank, size, flag)</c> (<c>0x9B79DC</c>, modes 1 and 3). Not adopted; required then.</summary>
    public Func<WwiseMediaBank, uint, byte, int>? DataStreamedA9B6C34 { get; set; }

    // ------------------------------------------------------------------ the loader

    /// <summary>
    /// <c>0x9B74D8</c> (K1, K12, K13). Under the global lock the registry (<see cref="WwiseMediaTable.Registry"/>) is searched by (<c>Id</c>, <c>Type == 2 ? LanguageOrMemory : 0</c>). A hit with <c>[bank+0x50]</c> bit 0 set returns 0x45 (the
    /// bank is untouched); another hit becomes the result (<c>[bank+0x54]++</c> with <see cref="WwiseBankLoadRequest.AddRef"/>). A miss creates a 0x58-byte bank (K12; an allocation failure returns 0x34 with a null result and a reset reader);
    /// mode 0 stores the pool argument, <c>[+0x50]</c> bit 0 = 1, <c>[+0x48]</c> = 1, <c>[+0x4C]</c> = 0, <c>[+0x54]</c> = 0; any other mode stores the media pool, bit 0 = 0, <c>[+0x48]</c> = 0, <c>[+0x4C]</c> = 1, <c>[+0x54]</c> = the flag.
    /// Then the reader is reset (<c>0x9BBB04</c>) and set up: mode 0 with type 1 opens by id, with type 2 or 3 the memory setup <c>0x9BBB90</c>; every other combination opens by name when there is one, else by id; a result other than 1
    /// releases the reader and returns it. BKHD (K2) must give 1; then the chunk loop runs and every exit releases the reader (<c>0x9BBBA4</c>).
    /// </summary>
    public int Load9B74D8(WwiseBankLoadRequest req, out WwiseMediaBank? bank)
    {
        ArgumentNullException.ThrowIfNull(req);
        bank = null;
        uint key2 = req.Type == 2 ? req.LanguageOrMemory : 0;                      // 0x9B7528..0x9B753C moveq r3,sb; movne r3,#0
        WwiseMediaBank? existing;
        lock (Table.Gate)                                                          // 0x9B7524 bl 0x4D3064(0x108E330)
            existing = Table.Registry.Find(req.Id, key2);                          // 0x9B7548 bl 0xA68804
        if (existing is not null)
        {
            if ((existing.Flags50 & 1) != 0) return 0x45;                          // 0x9B7554..0x9B7568
            bank = existing;                                                       // 0x9B7584..0x9B758C
            if (req.AddRef) existing.Word54++;                                     // 0x9B7590..0x9B7598
        }
        else
        {
            if (Table.Memory.Allocate(0x58) is null)                               // 0x9B76A0 bl 0xA7A7F4(pool, 0x58)
            {
                bank = null;                                                       // 0x9B7A3C..0x9B7A4C: *out = 0
                Reader.Reset9BBB04();                                              // 0x9B7A50
                Reader.Release9BBBA4();                                            // 0x9B7628
                return 0x34;                                                       // 0x9B7A44
            }
            bool modeZero = req.Mode == 0;                                         // 0x9B7650..0x9B7660
            bank = new WwiseMediaBank
            {
                Id = req.Id,                                                       // 0x9B7740 str r8,[sl,#8]
                Language0C = req.Type == 2 ? req.LanguageOrMemory : 0,             // 0x9B7690 moveq fp,sb; 0x9B7748 str fp,[sl,#0xc]
                PoolId24 = modeZero ? req.PoolArg : Globals.MediaPoolId14 ?? throw new WwiseMissingBehaviourException(
                    "M6-025 K12: a mode != 0 bank creation stores [[0x108D90C]+0x14] in [bank+0x24]; its init is unread, supply WwiseBankGlobals.MediaPoolId14"),         // 0x9B7664 ldr ip,[sp,#0xa8] / 0x9B765C ldr ip,[r2,#0x14]
                Word4C = modeZero ? 0 : 1,                                         // 0x9B76BC [sp+0x20]
                RefCount48 = modeZero ? 1 : 0,                                     // 0x9B76D8 [sp+0x18]
                Word54 = modeZero ? 0u : (req.AddRef ? 1u : 0u),                   // 0x9B76E0 [sp+0x1c] (the flag byte on mode != 0)
                Flags4 = 2,                                                        // 0x9B76FC..0x9B7750: (old & 0xFE) | 2
                Flags50 = (byte)(modeZero ? 1 : 0),                                // 0x9B76C0..0x9B76E4: bit 0 = [sp+0x14], bits 1 and 2 cleared
                PoolFlag28 = 0,                                                    // 0x9B7708
                Table = Table,
            };
        }
        Reader.Reset9BBB04();                                                      // 0x9B75B8 bl 0x9BBB04
        bool type23 = req.Type is 2 or 3;                                          // 0x9B75B4..0x9B75C8 (fp = type - 2; sl = fp <= 1 unsigned)
        bool windowMode = req.Type == 2;                                           // [sp+0x10] (0x9B75A0..0x9B75B0)
        int r;
        if (req.Mode == 0 && req.Type == 1)                                        // 0x9B75D4..0x9B75D8 beq 0x9B7838
            r = OpenReaderSeam(req, WwiseReaderOpen.ById9BC348);
        else if (req.Mode == 0 && type23)                                          // 0x9B75DC..0x9B75E0 bne 0x9B7820
            r = Reader.SetupMemory9BBB90(req.Memory ?? throw new WwiseMissingBehaviourException(
                "M6-025 K1: a type 2 or 3 load of mode 0 reads the memory the caller passes (0x9BBB90(R, sb, [sp+0xB0])); supply WwiseBankLoadRequest.Memory"), 0, req.MemoryLength);
        else
            r = OpenReaderSeam(req, req.HasName ? WwiseReaderOpen.ByName9BC17C : WwiseReaderOpen.ById9BC348);   // 0x9B75E4..0x9B7600, 0x9B7B84..0x9B7BA0
        if (r != 1)                                                                // 0x9B7608 cmp fp,#1; bne 0x9B7628
            return Release(r);
        var header = new WwiseBkhdHeader();                                        // [sp+0x4C..0x5C] zeroed (0x9B7610..0x9B7620)
        int b = ReadBkhdA9B21F4(header);                                           // 0x9B776C bl 0x9B21F4
        if (b != 1) return Release(b);                                             // 0x9B7770..0x9B7778
        return Release(RunChunks(req, bank!, windowMode));                         // the chunk loop 0x9B77D4..0x9B79E8
    }

    private int OpenReaderSeam(WwiseBankLoadRequest req, WwiseReaderOpen how)
        => (OpenReader ?? throw new WwiseMissingBehaviourException(
            $"M6-025 K1: the reader open {(how == WwiseReaderOpen.ByName9BC17C ? "0x9BC17C (by name)" : "0x9BC348 (by id)")} and the std stream behind it are not adopted; supply WwiseBankLoader.OpenReader"))(Reader, req, how);

    private int Release(int result)
    {
        Reader.Release9BBBA4();                                                    // 0x9B7628 bl 0x9BBBA4
        return result;
    }

    private int RunChunks(WwiseBankLoadRequest req, WwiseMediaBank bank, bool windowMode)
    {
        var header = new byte[8];
        while (true)
        {
            int rr = Reader.Read9BBC14(header, out int read);                      // 0x9B77EC bl 0x9BBC14(R, hdr, 8, &read)
            if (rr != 1) return rr;                                                // 0x9B77F8 bne 0x9B79E8
            if (read != 8) return read == 0 ? 1 : 7;                              // 0x9B77FC..0x9B7810
            uint tag = BitConverter.ToUInt32(header, 0);
            uint size = BitConverter.ToUInt32(header, 4);
            int handled;
            switch (tag)
            {
                case Didx:
                    switch (req.Mode)                                              // 0x9B789C..0x9B78B4 (jump table on [sp+0xC0])
                    {
                        case 0 or 1 or 3: handled = HandleDidx(bank, size, windowMode); break;       // 0x9B78E0
                        case 2: handled = SkipChunk(size); break;                  // 0x9B78B8
                        default: handled = 1; break;                               // 0x9B78A4 b 0x9B77D4: the chunk is left unread
                    }
                    break;
                case Data:
                    switch (req.Mode)                                              // 0x9B79A0..0x9B79BC
                    {
                        case 0: handled = HandleData(bank, size, windowMode); break;                 // 0x9B79F0
                        case 1 or 3:                                               // 0x9B79C0
                            if (size == 0) { handled = 1; break; }                 // 0x9B79C8..0x9B79D0
                            handled = (DataStreamedA9B6C34 ?? throw new WwiseMissingBehaviourException(
                                "M6-025 K1: the streamed DATA load 0x9B6C34 (0x9B79DC, modes 1 and 3) is not adopted; supply WwiseBankLoader.DataStreamedA9B6C34"))(bank, size, req.Flag12);
                            break;
                        case 2: handled = SkipChunk(size); break;                  // 0x9B78B8
                        default: handled = 1; break;                               // 0x9B79AC b 0x9B77D4
                    }
                    break;
                case Stmg:                                                         // 0x9B7AC8: a zero-size STMG is skipped
                    handled = size == 0 ? 1 : (StmgA9B0B14 ?? throw new WwiseMissingBehaviourException(
                        "M6-025 K1: STMG 0x9B0B14 (0x9B7AD8) is not adopted by C34.4; supply WwiseBankLoader.StmgA9B0B14"))(Reader);
                    break;
                case Plat: handled = PlatA9B2B08(size); break;                     // 0x9B7AE0..0x9B7AE8
                case Envs: handled = EnvsA9B2988(size); break;                     // 0x9B7A78..0x9B7A80
                case Init: handled = InitA9B4048(size); break;                     // 0x9B7AA0..0x9B7AA8
                case Hirc: handled = HircA9B3260(bank, bank.Id); break;            // 0x9B7AB0..0x9B7AC0 (no size: HIRC reads its own sizes)
                case Stid:
                    handled = (StidA9B2410 ?? throw new WwiseMissingBehaviourException(
                        "M6-025 K11: STID 0x9B2410 (0x9B7A98) is not adopted by C34.4; supply WwiseBankLoader.StidA9B2410"))(Reader, size, bank);
                    break;
                default: handled = SkipChunk(size); break;                         // 0x9B78B8..0x9B78D8
            }
            if (handled != 1) return handled;                                      // 0x9B79E0 cmp r0,#1; beq 0x9B77D4 / 0x9B79E8
        }
    }

    private int SkipChunk(uint size)
    {
        Reader.Skip9BBF9C(size, out uint skipped);                                 // 0x9B78C4 bl 0x9BBF9C(R, size, &[sp+0x34]): the result is not tested
        return size == skipped ? 1 : 7;                                            // 0x9B78C8..0x9B78D8
    }

    // ------------------------------------------------------------------ BKHD (K2)

    /// <summary>
    /// <c>0x9B21F4(BM, hdr)</c> (K2, verification correction 12): 8 bytes <c>{tag, size}</c> through <c>0x9BBF64</c> (a failure returns 7); the tag must be 'BKHD' (else 7); then 0x14 bytes into <paramref name="hdr"/> (a failure returns that result). When
    /// the first word of the 16-byte key at <c>0x108D9A0</c> is non-zero the header's first 16 bytes are XORed with the key (the project id at <c>+0x10</c> is not). <c>size - 0x14</c> bytes are skipped through <c>0x9BBF9C</c> (a failure returns
    /// its result, a short skip 0x38). The result is 1 when the (decrypted) version is 0x78, else 0x40. Every exit stores <c>[BM+0x64] = (u16 at hdr+0xC != 0)</c>.
    /// </summary>
    public int ReadBkhdA9B21F4(WwiseBkhdHeader hdr)
    {
        int result = ReadBkhdBody(hdr);
        BkhdAlignedFlag64 = (byte)(hdr.Alignment != 0 ? 1 : 0);                    // 0x9B2224..0x9B2230
        return result;
    }

    private int ReadBkhdBody(WwiseBkhdHeader hdr)
    {
        var chunk = new byte[8];
        if (Reader.ReadRaw9BBF64(chunk) != 1) return 7;                            // 0x9B2214..0x9B2220
        if (BitConverter.ToUInt32(chunk, 0) != Bkhd) return 7;                     // 0x9B223C..0x9B224C
        uint size = BitConverter.ToUInt32(chunk, 4);                               // [sp+0xC]
        var raw = new byte[0x14];
        int r = Reader.ReadRaw9BBF64(raw);                                         // 0x9B225C bl 0x9BBF64(hdr, 0x14)
        if (r != 1) return r;                                                      // 0x9B2260..0x9B2264
        uint v0 = BitConverter.ToUInt32(raw, 0), v1 = BitConverter.ToUInt32(raw, 4), v2 = BitConverter.ToUInt32(raw, 8);
        ushort a = BitConverter.ToUInt16(raw, 0xC), d = BitConverter.ToUInt16(raw, 0xE);
        uint proj = BitConverter.ToUInt32(raw, 0x10);
        var key = Globals.XorKey;
        if (key[0] != 0)                                                           // 0x9B2270..0x9B2278 beq 0x9B22C0
        {
            v0 ^= key[0];                                                          // 0x9B22AC..0x9B22BC
            v1 ^= key[1];                                                          // 0x9B228C, 0x9B22B0
            v2 ^= key[2];                                                          // 0x9B22A0, 0x9B22B4
            a = (ushort)(a ^ (ushort)key[3]);                                      // 0x9B2298, 0x9B22A8
            d = (ushort)(d ^ (ushort)(key[3] >> 16));                              // 0x9B22A4, 0x9B22B8
        }
        hdr.Version = v0; hdr.BankId = v1; hdr.Language = v2; hdr.Alignment = a; hdr.DeviceAllocated = d; hdr.ProjectId = proj;
        uint extra = unchecked(size - 0x14);                                       // 0x9B22C4 subs r7,r3,#0x14
        if (extra != 0)
        {
            int s = Reader.Skip9BBF9C(extra, out uint skipped);                    // 0x9B22E0 bl 0x9BBF9C
            if (s != 1) return s;                                                  // 0x9B22E4..0x9B22E8
            if (skipped != extra) return 0x38;                                     // 0x9B22EC..0x9B22F8
        }
        return hdr.Version == 0x78 ? 1 : 0x40;                                     // 0x9B22FC..0x9B2308
    }

    // ------------------------------------------------------------------ DIDX, DATA (K4, K5)

    /// <summary>
    /// The DIDX handler (<c>0x9B78E0..0x9B796C</c>, K5): a bank with processed entries (<c>[bank+0x2C] != 0</c>) has the chunk skipped (<c>0x9B7B74</c>: <c>0x9BBF9C</c>, the result is not tested). Otherwise <c>n = size / 12</c> and the window-pointer variant
    /// (<paramref name="windowMode"/>, <c>[sp+0x10]</c>: type 2) takes <c>12 n</c> bytes through <c>0x9BBE5C</c> into <c>[bank+0x18]</c> when it was null (the block is not owned: <c>[bank+0x50]</c> bit 1 stays clear), frees the reader's
    /// temporary copy (<c>0x9BBBE0</c>) and stores <c>[bank+0x30] = n</c> when <c>[bank+0x18]</c> is non-null; the other variant allocates <c>12 n</c> bytes (null returns 0x34), sets <c>[bank+0x50]</c> bit 1, reads them raw (<c>0x9BBF64</c>,
    /// not tested) and stores <c>[bank+0x30] = n</c> when the block is non-null. A chunk size that is not a multiple of 12 leaves its remainder unread.
    /// </summary>
    public int HandleDidx(WwiseMediaBank bank, uint size, bool windowMode)
    {
        if (bank.Counter2C != 0)                                                   // 0x9B78EC..0x9B78F4
        {
            Reader.Skip9BBF9C(size, out _);                                        // 0x9B7B74..0x9B7B7C
            return 1;
        }
        uint n = (uint)(((ulong)size * 0xAAAAAAABUL) >> 35);                       // 0x9B78FC umull; 0x9B7908 lsr #3
        uint bytes = n * 12;                                                       // 0x9B790C..0x9B7910
        if (windowMode)                                                            // 0x9B7914 bne 0x9B7B08
        {
            var ptr = Reader.Window9BBE5C(bytes);                                  // 0x9B7B14 bl 0x9BBE5C
            if (bank.Didx18 is null && !ptr.IsNull)                                // 0x9B7B18..0x9B7B28 streq r0,[fp,#0x18]
            {
                var copy = new byte[bytes];
                Array.Copy(ptr.Array!, ptr.Index, copy, 0, (int)bytes);
                bank.Didx18 = copy;
                bank.Didx18Address = 0;
            }
            Reader.FreeTemp9BBBE0();                                               // 0x9B7B30 bl 0x9BBBE0
            if (bank.Didx18 is not null) bank.Count30 = n;                         // 0x9B7960..0x9B7968
            return 1;
        }
        var address = Table.Memory.Allocate((int)bytes);                           // 0x9B7930
        if (address is null) { bank.Didx18 = null; return 0x34; }                  // 0x9B793C str r0,[fp,#0x18]; 0x9B7940 beq 0x9B7C64
        bank.Didx18 = Table.Memory.Block(address.Value);
        bank.Didx18Address = address.Value;
        bank.Flags50 = (byte)(bank.Flags50 | 2);                                   // 0x9B7944..0x9B7954
        Reader.ReadRaw9BBF64(bank.Didx18);                                         // 0x9B7958 bl 0x9BBF64 (the result is not tested)
        bank.Count30 = n;                                                          // 0x9B7964..0x9B7968 (the block is non-null)
        return 1;
    }

    /// <summary>
    /// The DATA handler of mode 0 (<c>0x9B79F0..0x9B7A38</c>, <c>0x9B7B3C..0x9B7B70</c>, <c>0x9B7BAC..0x9B7CBC</c>, K3, K4). With <paramref name="windowMode"/> (type 2) the chunk is taken in place: <c>0x9BBE5C</c> gives the pointer,
    /// <c>[bank+0x1C] = size</c>, the temporary copy is freed, and the writer gets the pointer. Otherwise: a size of 0 reads nothing; else with no pool (<c>[bank+0x24] == -1</c>) <c>0xA7AC98(0, size, size, 9, 0x10)</c> creates one (-1 returns 0x34),
    /// <c>[bank+0x28] = 1</c>; the pool must check as 1 (<c>0xA7AAE8</c>, else that code is returned); attributes (<c>0xA7A7C8</c>) bit 3 clear allocates the size, bit 3 set takes one block (<c>0xA7A9FC</c>) when the size does not exceed the
    /// block size (<c>0xA7AA9C</c>); a null buffer returns 0x34; the chunk is read (<c>0x9BBC14</c>) and a length other than the size returns 7. Then, when the bank has DIDX entries (<c>[bank+0x30] != 0</c>) none of which were processed
    /// (<c>[bank+0x2C] == 0</c>), the writer <c>0x9B49A4(BM, data, bank)</c> runs and its result is the handler's.
    /// </summary>
    public int HandleData(WwiseMediaBank bank, uint size, bool windowMode)
    {
        var memory = Table.Memory;
        uint dataBase;
        if (windowMode)                                                            // 0x9B79F0..0x9B79F8 bne 0x9B7B3C
        {
            var ptr = Reader.Window9BBE5C(size);                                   // 0x9B7B44
            bank.DataSize1C = size;                                                // 0x9B7B54
            Reader.FreeTemp9BBBE0();                                               // 0x9B7B60
            dataBase = ptr.IsNull ? 0 : memory.AddressOf(ptr);                     // 0x9B7B68..0x9B7B70 (r1 = the pointer)
        }
        else
        {
            if (size != 0)
            {
                int rr = ReadIntoBankPool(bank, size);                             // 0x9B7BAC..0x9B7C60
                if (rr != 1) return rr;
            }
            dataBase = bank.DataBuffer14;                                          // 0x9B7A0C ldreq r1,[fp,#0x14] / 0x9B7C58 ldr r1,[fp,#0x14]
        }
        if (bank.Counter2C == 0 && bank.Count30 != 0)                              // 0x9B7A14..0x9B7A28
            return Table.WriteBankA9B49A4(bank, dataBase);                         // 0x9B7A34
        return 1;
    }

    /// <summary>
    /// The pool read the DATA handler (<c>0x9B7BAC..0x9B7C60</c>) and <c>0x9B2314</c> share (K3, K4): with no pool (<c>[bank+0x24] == -1</c>) <c>0xA7AC98(0, size, size, 9, 0x10)</c> creates one (-1 returns 0x34),
    /// <c>[bank+0x28] = 1</c>; the pool must check as 1 (<c>0xA7AAE8</c>, else that code is returned); attributes (<c>0xA7A7C8</c>) bit 3 clear allocates the size, bit 3 set takes one block (<c>0xA7A9FC</c>) when the size does not
    /// exceed the block size (<c>0xA7AA9C</c>); a null buffer returns 0x34; <c>[bank+0x1C] = size</c>, the chunk is read (<c>0x9BBC14</c>) and a length other than the size returns 7.
    /// </summary>
    private int ReadIntoBankPool(WwiseMediaBank bank, uint size)
    {
        var memory = Table.Memory;
        if (bank.PoolId24 == -1)                                                   // 0x9B7BAC..0x9B7BB4 / 0x9B2334..0x9B2338
        {
            int id = memory.CreatePoolA7AC98(0, size, size, 9, 0x10);              // 0x9B7C88 / 0x9B2368 bl 0xA7AC98(0, size, size, 9, 0x10)
            if (id == -1) return 0x34;                                             // 0x9B7C90..0x9B7C94 / 0x9B236C, 0x9B2380
            bank.PoolId24 = id;                                                    // 0x9B7C9C / 0x9B2370
            bank.PoolFlag28 = 1;                                                   // 0x9B7CA0 / 0x9B2378
        }
        int check = memory.PoolCheckA7AAE8(bank.PoolId24);                         // 0x9B7BBC / 0x9B233C
        if (check != 1) return check;                                              // 0x9B7BC4..0x9B7BC8 / 0x9B2340..0x9B2348
        uint attributes = memory.PoolAttributesA7A7C8(bank.PoolId24);              // 0x9B7BD4 / 0x9B238C
        if ((attributes & 8) == 0)                                                 // 0x9B7BDC tst r0,#8; beq 0x9B7CA8 / 0x9B2390..0x9B2398
        {
            bank.DataBuffer14 = memory.Allocate((int)size) ?? 0;                   // 0x9B7CB0 bl 0xA7A7F4(pool, size); 0x9B7CB8 str r0,[fp,#0x14]
        }
        else if (size <= memory.PoolBlockSizeA7AA9C(bank.PoolId24))                // 0x9B7BE8..0x9B7BF4 bhi 0x9B7C08 / 0x9B239C..0x9B23A4
        {
            bank.DataBuffer14 = memory.PopBlockA7A9FC(bank.PoolId24);              // 0x9B7BFC bl 0xA7A9FC; 0x9B7C04 str r0,[fp,#0x14]
        }
        // size above the block size: [bank+0x14] stays as it was (0x9B7C08 / 0x9B23A8 reloads it)
        if (bank.DataBuffer14 == 0) return 0x34;                                   // 0x9B7C0C..0x9B7C10 / 0x9B23AC..0x9B23B0
        bank.DataSize1C = size;                                                    // 0x9B7C18 / 0x9B23BC
        var buffer14 = memory.Block(bank.DataBuffer14);
        if (buffer14.Length < size)
            throw new WwiseMissingBehaviourException(
                "M6-025 M5: the chunk size exceeds the pool block size while [bank+0x14] already holds a smaller buffer; the engine then reads past it (0x9B7C08..0x9B7C30), which the inventory does not settle");
        int rr = Reader.Read9BBC14(buffer14.AsSpan(0, (int)size), out int read);   // 0x9B7C30 / 0x9B23CC bl 0x9BBC14
        if (rr == 1 && size != (uint)read) rr = 7;                                 // 0x9B7C38..0x9B7C48 / 0x9B23D8..0x9B23E0
        return rr;                                                                 // 0x9B7C5C..0x9B7C60 / 0x9B2348
    }

    /// <summary>
    /// <c>0x9B2314(BM, size, _, bank)</c> (K3): a size of 0 returns 1; otherwise the pool read of the DATA handler (<see cref="ReadIntoBankPool"/>) without the writer. No <c>bl</c> reaches it in the image, so its production caller is not
    /// known; it is built because C34.4 adopts it.
    /// </summary>
    public int LoadMemoryChunkA9B2314(uint size, WwiseMediaBank bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (size == 0) return 1;                                                   // 0x9B2314..0x9B2318
        return ReadIntoBankPool(bank, size);
    }

    // ------------------------------------------------------------------ INIT, ENVS, PLAT (K6, K7, K8)

    /// <summary>
    /// INIT <c>0x9B4048(BM, size)</c> (K6): the window pointer (<c>0x9BBE5C(R, size)</c>) of the chunk; null returns 2. <c>count = u32</c>; per plug-in <c>{u32 id, u32 len, char[len]}</c>: an id found in <c>[0x108D9E8]</c> or in the registry
    /// <c>[0x108D9DC]</c> is skipped, any other calls <c>0xA57238(name)</c> (the name, or null for a length of 0); it never fails the bank. The scan of <c>[0x108DAFC+8]</c> after the loop (<c>0x9B412C..0x9B415C</c>) has no effect. Returns 1.
    /// </summary>
    public int InitA9B4048(uint size)
    {
        var ptr = Reader.Window9BBE5C(size);                                       // 0x9B4050 bl 0x9BBE5C
        if (ptr.IsNull) return 2;                                                  // 0x9B4054..0x9B4058
        uint count = ptr.U32(0);                                                   // 0x9B4060 ldr r5,[r4],#4
        int pos = 4;
        for (uint i = 0; i < count; i++)                                           // 0x9B4064 cmp r5,#0 ... 0x9B4124 subs r5,r5,#1
        {
            uint id = ptr.U32(pos);                                                // 0x9B408C ldr lr,[r4,#-8]
            uint len = ptr.U32(pos + 4);                                           // 0x9B407C ldr r3,[r4,#-4]
            pos += 8;
            string? name = null;
            if (len != 0)                                                          // 0x9B4094 movne r0,r4; addne r4,r4,r3
            {
                int end = pos;
                while (end < pos + len && ptr[end] != 0) end++;
                name = Encoding.ASCII.GetString(ptr.Array!, ptr.Index + pos, end - pos);
                pos += (int)len;
            }
            if (Globals.PluginIdsE8.Contains(id) || Globals.PluginIdsDC.Contains(id)) continue;   // 0x9B40A8..0x9B40D4 / 0x9B40F4..0x9B4120, found -> 0x9B4168 / 0x9B4174 -> 0x9B4124
            (Globals.ExternalPluginLoadA57238 ?? throw new WwiseMissingBehaviourException(
                "M6-025 K6: 0xA57238 (0x9B4120) loads a plug-in library for an id found in neither registry and is not adopted; supply WwiseBankGlobals.ExternalPluginLoadA57238"))(name);
        }
        return 1;                                                                  // 0x9B4160
    }

    /// <summary>
    /// ENVS <c>0x9B2988(BM, size)</c> (K7): a size of 0 or <c>[0x108D904] == 0</c> returns 2. Two outer iterations (type 0, 1) of three inner (idx 0..2): a byte <c>enabled</c> (stored as <c>enabled != 0</c> at <c>[table + 3 * type + idx]</c>),
    /// a byte scaling, a u16 <c>n</c>, <c>12 n</c> bytes of points (an allocation failure returns 0x34); <c>0x9CC5A0(table, type, idx, n, buf, scaling)</c> copies them (<see cref="WwiseEnvsTable.Write9CC5A0"/>, its result is not tested). Any read result other
    /// than 1 is returned. Returns 1.
    /// </summary>
    public int EnvsA9B2988(uint size)
    {
        if (size == 0 || !Globals.Envs904Present) return 2;                        // 0x9B2998..0x9B29A4
        var table = Globals.EnvsTable ?? throw new InvalidOperationException("[0x108D904] is set but the table [0x108D8FC] is null: the engine dereferences it (0x9B2A14)");
        for (int type = 0; type < 2; type++)                                       // 0x9B29B4 r5, 0x9B2ADC
        {
            for (int idx = 0; idx < 3; idx++)                                      // 0x9B29C4 r4, 0x9B2AD4
            {
                var one = new byte[1];
                int r = Reader.ReadRaw9BBF64(one);                                 // 0x9B29D4 bl 0x9BBF64(hdr+0x14, 1)
                if (r != 1) return r;                                              // 0x9B29D8..0x9B29E4
                table.Enabled[3 * type + idx] = (byte)(one[0] != 0 ? 1 : 0);       // 0x9B2A0C..0x9B2A1C
                var scalingByte = new byte[1];
                r = Reader.ReadRaw9BBF64(scalingByte);                             // 0x9B2A20 bl 0x9BBF64(sp+0x15, 1)
                if (r != 1) return r;
                var nBytes = new byte[2];
                r = Reader.ReadRaw9BBF64(nBytes);                                  // 0x9B2A40 bl 0x9BBF64(sp+0x16, 2)
                if (r != 1) return r;
                uint n = BitConverter.ToUInt16(nBytes, 0);
                if (Table.Memory.Allocate((int)(12 * n)) is not { } address) return 0x34;   // 0x9B2A64 bl 0xA7A7F4; 0x9B2A74 beq 0x9B2AF4
                var buf = Table.Memory.Block(address);
                r = Reader.ReadRaw9BBF64(buf);                                     // 0x9B2A84 bl 0x9BBF64(buf, 12n)
                if (r != 1) { Table.Memory.Free(address); return r; }              // 0x9B2A98..0x9B2AA4
                table.Write9CC5A0(type, idx, n, buf, scalingByte[0], Table.Memory.AllocationFails);   // 0x9B2AC4 bl 0x9CC5A0 (the result is not tested)
                Table.Memory.Free(address);                                        // 0x9B2AD0 bl 0xA7A988
            }
        }
        return 1;                                                                  // 0x9B2AE8
    }

    /// <summary>
    /// PLAT <c>0x9B2B08(BM, size)</c> (K8): a size of 0 returns 1. <c>len = u32</c> of the window pointer; a length of 0 returns 1 (after freeing the temporary copy); else <c>len</c> bytes are copied, NUL-terminated, and
    /// <see cref="PlatformStringA9A6518"/> decides; the temporary copy is freed and its result returned.
    /// </summary>
    public int PlatA9B2B08(uint size)
    {
        if (size == 0) return 1;                                                   // 0x9B2B08..0x9B2B1C
        var ptr = Reader.Window9BBE5C(size);                                       // 0x9B2B30
        uint len = ptr.U32(0);                                                     // 0x9B2B34 ldr r5,[r0]
        int result = 1;
        if (len != 0)                                                              // 0x9B2B38..0x9B2B3C
        {
            int end = 4;
            while (end < 4 + len && ptr[end] != 0) end++;                          // strncpy semantics + the terminator at [len]
            var text = Encoding.ASCII.GetString(ptr.Array!, ptr.Index + 4, end - 4);
            result = PlatformStringA9A6518(text);                                  // 0x9B2B8C bl 0x9A6518
        }
        Reader.FreeTemp9BBBE0();                                                   // 0x9B2B48 bl 0x9BBBE0
        return result;                                                             // 0x9B2B4C
    }

    /// <summary>
    /// <c>0x9A6518(str)</c> (K8): with a stored platform string <c>[0x108D868+0x7C]</c> the strings are compared (<c>strcmp</c>): equal 1, different 0x53. Without one it is stored (a copy of <c>strlen + 1</c> bytes from the default pool; a
    /// failed allocation returns 0x34) and 1 returned.
    /// </summary>
    public int PlatformStringA9A6518(string text)
    {
        if (Globals.PlatformString is { } stored)                                  // 0x9A6524..0x9A6528
            return string.CompareOrdinal(text, stored) != 0 ? 0x53 : 1;            // 0x9A6530..0x9A653C
        if (Table.Memory.Allocate(text.Length + 1) is null) return 0x34;           // 0x9A655C bl 0xA7A7F4; 0x9A6568 beq 0x9A657C
        Globals.PlatformString = text;                                             // 0x9A6570 bl strcpy
        return 1;
    }

    // ------------------------------------------------------------------ HIRC (K9, K10)

    /// <summary>
    /// HIRC <c>0x9B3260(BM, bank, id)</c> (K9, verification correction 10): <c>count = u32</c> (0 returns 1); the bank's object array grows by <c>count</c> (a block of <c>4 * (capacity + count)</c> bytes: a failed allocation returns 2). Per object a
    /// 5-byte header <c>{u8 type, u32 size}</c>: types 1..9 and 14..23 go to their creators (not adopted: they throw); types 10..13 and any type above 23 go to the hook (<c>0x9B3EBC</c>): with a hook <c>hook(type, size, bank, id)</c>: 1 continues,
    /// 3 skips <c>size</c> bytes and ends the load with 3 (a short skip gives 7), any other result ends it with that result and no skip; without a hook the object is skipped (<c>size</c> bytes through <c>0x9BBF9C</c>, a short skip gives 7 and
    /// ends the load) and a music type sets a flag (<c>[sp+0x24]</c>, no other reader). The loop runs while the result is 1 and objects remain; the result is the last one.
    /// </summary>
    public int HircA9B3260(WwiseMediaBank bank, uint id)
    {
        var four = new byte[4];
        int r = Reader.ReadRaw9BBF64(four);                                        // 0x9B3294 bl 0x9BBF64(R, &count, 4)
        if (r != 1) return r;                                                      // 0x9B329C..0x9B32A8
        uint count = BitConverter.ToUInt32(four, 0);
        if (count == 0) return r;                                                  // 0x9B32B4..0x9B32BC
        uint newCapacity = unchecked(count + bank.HircCapacity44);                 // 0x9B32C8..0x9B32D0
        if (Table.Memory.Allocate((int)(4 * newCapacity)) is not { } newArray) return 2;   // 0x9B32D4..0x9B32E4 -> 0x9B3F00
        if (bank.HircArray3C != 0) Table.Memory.Free(bank.HircArray3C);            // 0x9B3330..0x9B3334 bl 0xA7A988 (the old array, copied first)
        bank.HircArray3C = newArray;                                               // 0x9B3344 str r8,[r2,#0x3c]
        bank.HircCapacity44 = newCapacity;                                         // 0x9B3348
        bool musicSeen = false;                                                    // [sp+0x24]
        uint index = 0;
        int result = r;                                                            // r4
        while (true)
        {
            var header = new byte[5];
            result = Reader.ReadRaw9BBF64(header);                                 // 0x9B3370 bl 0x9BBF64(R, hdr, 5)
            if (result != 1) return result;                                        // 0x9B3374..0x9B337C
            byte type = header[0];
            uint size = BitConverter.ToUInt32(header, 1);
            bool cont;
            if (type is >= 1 and <= 9 or >= 14 and <= 23)                          // 0x9B3384..0x9B338C: the jump table
                throw new WwiseMissingBehaviourException(
                    $"M6-001 K9: the creator of HIRC object type {type} (jump table 0x9B338C) is not adopted by C34.4");
            if (Globals.HircHook is { } hook)                                      // 0x9B3EBC..0x9B3ECC
            {
                result = hook(type, size, bank, id);                               // 0x9B3EDC blx r3
                if (result == 3) cont = false;                                     // 0x9B3EE0..0x9B3EEC: r6 = 0 and the skip below
                else { cont = result == 1; goto tail; }                            // 0x9B3EF0..0x9B3EFC
            }
            else
            {
                cont = true;                                                       // 0x9B3F18 / 0x9B3F24 r6 = 1
                if (!musicSeen) musicSeen = type is >= 10 and <= 13;               // 0x9B3F10..0x9B3F34
            }
            Reader.Skip9BBF9C(size, out uint skipped);                             // 0x9B3F38..0x9B3F4C bl 0x9BBF9C
            if (skipped != size) { cont = false; result = 7; }                     // 0x9B3F50..0x9B3F60
        tail:
            index++;                                                               // 0x9B3688
            if (index >= count) cont = false;                                      // 0x9B368C..0x9B3690
            if (!cont) return result;                                              // 0x9B3698..0x9B36A0
        }
    }
}
