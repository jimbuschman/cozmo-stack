// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

// The streaming layer of the Wwise runtime (M6-wwise-bank.md C33.2, C33.4; research live-bodies-3 sections 5 to 7 with the verifier's corrections): the stream manager and its device table,
// the blocking device, the auto stream object S (vtable 0x1039240, IAkAutoStream subobject 0x10392D0) and the CreateAuto functions. Every function below was disassembled in
// libcozmoEngine.so (ARM) when this file was written; addresses are in the comments. Slots are named as vptr + offset (the primary vtable of S is 0x1039240; the IAkAutoStream
// slots are thunks `sub r0,r0,#0x30; b impl` into the same implementations).
//
// Production entry. Engine: the source classes call the stream manager through 0xA74564 (CreateAuto by name vt+0x18 / by id vt+0x1C of the manager at [0x108D798+0x10]), and the
// audio thread's voice pass reaches the stream methods through the source's vt+0x28 (StartStream) and vt+0x30 (Execute). The C# counterpart is WwiseStreamManager.CreateAutoById /
// CreateAutoByName, reached from WwiseStreamSourceBase.CreateStreamA74564, which the Vorbis and ADPCM voice sources' StartStream call (WwiseVoiceSourceStart.StartA56650). Nothing in
// production constructs the Wwise runtime yet (B-M6b-4 C30.W); tests drive it through those entries.
//
// THE ONE SEAM (operator ruling 2026-10-03, C33.4): the I/O thread. The engine's device thread (0x97CA90) runs PerformIO (0x966A7C) concurrently with the audio thread, and the audio thread
// sees its progress only through GetBuffer, QueryBufferingStatus and the flags of S. What depends on the phone OS's scheduling is when stream data becomes available: the deferred file
// open (0x963890), the completion of each read, the scheduler's choice (0x963104, 0x962C24, 0x962EA8), the I/O memory manager (0x969E8C, 0x9713B4, 0x9716F0, 0x96FE70, 0x97161C), the stream
// cache (bUseStreamCache = 1: 0x9656AC's neighbour 0x961800) and Query's stale `*out` word. All of that is behind IWwiseStreamIoSeam, recorded by the operator as EQUIVALENT_IMPLEMENTATION.
// Everything on the audio-thread side of the line (the stream methods, every result 0x2D / 0x2E / 0x11 / 2 / 0x3F around the seam, the flags) and everything the thread does to the stream
// when it completes a transfer (0x966644, 0x964914, 0x964A20) is ported exactly. The transfer preparation 0x966BCC (the remainder 0x966F44..0x967100 is unread) is replaced, inside the seam,
// by WwiseSteppedStreamIo.PrepareTransfer: that is where its choices (the transfer size) are made.

/// <summary>The 0x38-byte device settings (<c>AkDeviceSettings</c>); the defaults are <c>0x96060C</c> and Anki changes only <c>+4</c> and the byte <c>+0x30</c> (A7).</summary>
public sealed class WwiseDeviceSettings
{
    /// <summary><c>+0</c>.</summary>
    public uint Word00 { get; set; }

    /// <summary><c>+4</c>: uIOMemorySize (default 0x200000; Anki: <c>cfg[0x64]</c>).</summary>
    public uint IoMemorySize04 { get; set; } = 0x200000;

    /// <summary><c>+8</c>: the alignment (default 4).</summary>
    public uint Word08 { get; set; } = 4;

    /// <summary><c>+0xC</c>.</summary>
    public uint Word0C { get; set; } = 1;

    /// <summary><c>+0x10</c>: uGranularity (default 0x4000); 0 makes <c>0x9616F4</c> return 0x1F.</summary>
    public uint Granularity10 { get; set; } = 0x4000;

    /// <summary><c>+0x14</c>: uSchedulerTypeFlags (default 1); the resolver requires exactly 1, the device factory takes bit 0 (blocking) or bit 1 (deferred).</summary>
    public uint SchedulerFlags14 { get; set; } = 1;

    /// <summary><c>+0x18</c>: the thread priority (<c>sched_get_priority_*</c>, 0x960688 / 0x9606AC: a libc value, passed to the thread seam only).</summary>
    public uint ThreadPriority18 { get; set; }

    /// <summary><c>+0x1C</c>: the thread stack size (0x10000).</summary>
    public uint ThreadStack1C { get; set; } = 0x10000;

    /// <summary><c>+0x20</c>: 1.</summary>
    public uint Word20 { get; set; } = 1;

    /// <summary><c>+0x24</c>: 0xFFFF.</summary>
    public uint Word24 { get; set; } = 0xFFFF;

    /// <summary><c>+0x28</c>: fTargetAutoStmBufferLength, as the engine's float bits (default <c>0x43BE0000</c>).</summary>
    public float TargetAutoStreamBufferLength28 { get; set; } = BitConverter.Int32BitsToSingle(0x43BE0000);

    /// <summary><c>+0x2C</c>: uMaxConcurrentIO (default 8).</summary>
    public uint MaxConcurrentIo2C { get; set; } = 8;

    /// <summary><c>+0x30</c> (byte): bUseStreamCache (default 0; Anki sets <c>cfg[0x71]</c> = 1, A2).</summary>
    public byte UseStreamCache30 { get; set; }

    /// <summary><c>+0x34</c>: uMaxCachePinnedBytes (default -1).</summary>
    public uint MaxCachePinnedBytes34 { get; set; } = 0xFFFFFFFF;

    /// <summary><c>0x96060C</c> with Anki's two changes (A7: <c>[P3+4] = cfg[0x64]</c>, <c>byte [P3+0x30] = cfg[0x71]</c>); Android's <c>cfg[0x64] = 0x180000</c>, <c>cfg[0x71] = 1</c> (A2).</summary>
    public static WwiseDeviceSettings Anki(uint ioMemorySize = 0x180000, byte useStreamCache = 1)
        => new() { IoMemorySize04 = ioMemorySize, UseStreamCache30 = useStreamCache };
}

/// <summary>The 0x10-byte stream heuristics (<c>AkAutoStmHeuristics</c>): <c>+0</c> float throughput, <c>+4</c> loop start, <c>+8</c> loop end, byte <c>+0xC</c> minimum buffers, byte <c>+0xD</c> priority.</summary>
public sealed class WwiseStreamHeuristics
{
    /// <summary><c>+0</c>.</summary>
    public float Throughput { get; set; }

    /// <summary><c>+4</c>.</summary>
    public uint LoopStart { get; set; }

    /// <summary><c>+8</c>.</summary>
    public uint LoopEnd { get; set; }

    /// <summary><c>+0xC</c> (byte).</summary>
    public byte MinNumBuffers { get; set; }

    /// <summary><c>+0xD</c> (byte).</summary>
    public byte Priority { get; set; }
}

/// <summary>The 0xC-byte buffer settings: <c>+0</c> explicit buffer size, <c>+4</c> minimum buffer size, <c>+8</c> block size (<c>0x964590</c>).</summary>
public sealed class WwiseStreamBufferSettings
{
    /// <summary><c>+0</c>.</summary>
    public uint BufferSize { get; set; }

    /// <summary><c>+4</c>.</summary>
    public uint MinBufferSize { get; set; }

    /// <summary><c>+8</c>.</summary>
    public uint BlockSize { get; set; }
}

/// <summary>A transfer buffer: <c>+0</c> the 64-bit file position of its first byte, <c>+8</c> the data, <c>+0x10</c> the size, <c>+0x18</c> the cache id (-1 for none).</summary>
public sealed class WwiseStreamBuffer
{
    /// <summary><c>+0</c>.</summary>
    public ulong Start { get; set; }

    /// <summary><c>+8</c>.</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary><c>+0x10</c>.</summary>
    public uint Size { get; set; }

    /// <summary><c>+0x18</c>.</summary>
    public int CacheId { get; set; } = -1;
}

/// <summary>A list node / transfer record (0x10 bytes): <c>+4</c> the buffer, <c>+8</c> the offset of the unconsumed data in the buffer, byte <c>+0xC</c> the state (bits 0 to 2: 3 ready, 2 cancelled).</summary>
public sealed class WwiseStreamNode
{
    /// <summary><c>+4</c>.</summary>
    public WwiseStreamBuffer? Buffer { get; set; }

    /// <summary><c>+8</c>.</summary>
    public uint Offset { get; set; }

    /// <summary><c>+0xC</c> (byte).</summary>
    public byte State { get; set; }
}

/// <summary>The points at which the audio thread observes the stream and the I/O thread may have progressed (the seam's trigger).</summary>
public enum WwiseIoCheckpoint
{
    /// <summary>Entry of <c>QueryBufferingStatus</c> (0x961C1C).</summary>
    Query,

    /// <summary>Entry of <c>GetBuffer</c> (0x965B1C).</summary>
    GetBuffer,
}

/// <summary>A planned read: the transfer record and the info the hook reads (the product of <c>0x966BCC</c>, replaced by the seam).</summary>
public sealed class WwiseTransferPlan
{
    /// <summary>The record (state, buffer).</summary>
    public required WwiseStreamNode Node { get; init; }

    /// <summary>The hook's transfer info; null when nothing is to be read (a cached block).</summary>
    public WwiseTransferInfo? Info { get; init; }
}

/// <summary>
/// THE ONE SEAM of the streaming layer (C33.4, EQUIVALENT_IMPLEMENTATION). The members stand for what the device thread and its memory manager do; the reference implementation is
/// <see cref="WwiseSteppedStreamIo"/>. Production code must supply one: there is no default.
/// </summary>
public interface IWwiseStreamIoSeam
{
    /// <summary><c>0x969E8C(dev+0x54, settings, dev)</c> (the I/O memory manager's init; the device returns 2 when this is not 1). The body is unread.</summary>
    int InitIoMemory(WwiseStreamDevice device, WwiseDeviceSettings settings);

    /// <summary><c>0x97CC80(dev, settings+0x18)</c> (start of the I/O thread); its result is the device init's result.</summary>
    int StartIoThread(WwiseStreamDevice device, WwiseDeviceSettings settings);

    /// <summary><c>0x97CE74</c> and <c>0x96D624</c> (the device destructor's stop of the thread and the memory manager).</summary>
    void DestroyDevice(WwiseStreamDevice device);

    /// <summary>The audio thread is about to observe <paramref name="stream"/>: the I/O thread may have run since the last time (when and how far is the seam's).</summary>
    void AtCheckpoint(WwiseStreamDevice device, WwiseAutoStream stream, WwiseIoCheckpoint where);

    /// <summary>
    /// Device vt+0x2C (<c>0x961800</c>): under the device lock the transfer is prepared with <c>bSync = 1</c> (<see cref="PrepareTransfer"/>) and completed with success without any hook read;
    /// true when a transfer record was produced. It can only succeed with a block the stream cache holds.
    /// </summary>
    bool TryCompleteFromCache(WwiseStreamDevice device, WwiseAutoStream stream);

    /// <summary>The stack word <c>QueryBufferingStatus</c> leaves in the caller's out variable when the stream is not opened (0x961C58 <c>moveq r8,#0x2e</c>, first write at 0x961C70).</summary>
    uint StaleQueryWord { get; }

    /// <summary><c>0x96FE70(dev+0x54, buffer)</c>: the buffer returns to the I/O memory manager.</summary>
    void ReleaseBuffer(WwiseStreamDevice device, WwiseStreamBuffer buffer);
}

/// <summary>
/// The blocking device (<c>0x966FCC</c> ctor, vtable <c>0x1039158</c>, 0x250 bytes): the stream list <c>[+0x48]</c> (newest first, linked through <c>[S+8]</c>), the scheduler counters
/// <c>[+0x14]</c> / <c>[+0x18]</c> / <c>[+0x1C]</c> / <c>[+0x20]</c> and the hook <c>[+0x21C]</c>.
/// </summary>
public sealed class WwiseStreamDevice
{
    /// <summary>The device lock <c>[dev+0xC]</c>.</summary>
    internal readonly object Lock0C = new();

    /// <summary>The stream-list lock <c>[dev+0x50]</c>.</summary>
    internal readonly object Lock50 = new();

    private readonly List<WwiseAutoStream> _streams = new();

    /// <summary><c>[+0x21C]</c>: the I/O hook.</summary>
    public IWwiseLowLevelIoHook Hook { get; }

    /// <summary>The manager the device belongs to (the allocation hook, the seam).</summary>
    public WwiseStreamManager Manager { get; }

    /// <summary><c>[+0x220]</c>: the granularity.</summary>
    public uint Granularity220 { get; private set; }

    /// <summary><c>[+4]</c>: uMaxConcurrentIO (<c>settings[0x2C]</c>).</summary>
    public uint MaxConcurrentIo04 { get; private set; }

    /// <summary><c>[+0x224]</c>: the target buffer length (a float).</summary>
    public float TargetLength224 { get; private set; }

    /// <summary><c>[+0x228]</c>: the device id (the slot).</summary>
    public int Id228 { get; private set; }

    /// <summary><c>[+0x22C]</c>: <c>settings[0x34]</c>.</summary>
    public uint CacheBytes22C { get; private set; }

    /// <summary><c>[+0x14]</c>: tasks waiting for I/O (<c>0x97CF2C</c> increments it; nothing built calls it, the thread is the seam).</summary>
    public int Ready14 { get; internal set; }

    /// <summary><c>[+0x18]</c>: the number of starved streams (<c>0x97CFA0</c> increments, <c>0x97CFF4</c> decrements).</summary>
    public int Starved18 { get; private set; }

    /// <summary><c>[+0x1C]</c> (byte): set by <c>0x97D048</c>, cleared by <c>0x97D020</c>.</summary>
    public bool Flag1C { get; set; }

    /// <summary><c>[+0x20]</c>: the number of transfers in flight (<c>0x97D054</c> / <c>0x97D080</c>).</summary>
    public int InFlight20 { get; internal set; }

    /// <summary><c>[+0x40]</c> (64 bit): the device time stamp: <c>(int64)(int32)clock()</c> (0x9617E4 slot +0x10, 0x96312C). Set by the seam; read by <c>Start</c> into <c>[S+0x18]</c>.</summary>
    public long Time40 { get; set; }

    /// <summary>The streams in list order (<c>[+0x48]</c> head first).</summary>
    public IReadOnlyList<WwiseAutoStream> Streams { get { lock (Lock50) return _streams.ToArray(); } }

    /// <summary><c>0x966FCC(obj, hook)</c> with the base constructor <c>0x962950</c>.</summary>
    internal WwiseStreamDevice(WwiseStreamManager manager, IWwiseLowLevelIoHook hook)
    {
        Manager = manager;
        Hook = hook;
    }

    /// <summary>
    /// <c>0x9616F4(dev, settings, id)</c> (device vt+0x14 = <c>0x96761C</c>): a granularity of 0 returns 0x1F; <c>settings[4] != 0</c> with a negative target length (a float compare, NaN passes) returns 0x1F; a
    /// deferred device (<c>settings[0x14] &amp; 2</c>) with <c>settings[0x2C] - 1 &gt;= 0x400</c> (unsigned) returns 0x1F; the fields are stored; the I/O memory manager (<c>0x969E8C</c>, the seam) not returning 1
    /// returns 2; the I/O thread is started (<c>0x97CC80</c>, the seam) and its result returned.
    /// </summary>
    internal int Init(WwiseDeviceSettings settings, int id)
    {
        if (settings.Granularity10 == 0) return 0x1F;                           // 0x9616F8..0x961704
        if (settings.IoMemorySize04 != 0 && settings.TargetAutoStreamBufferLength28 < 0f) return 0x1F;   // 0x96170C..0x961724 (vcmpe; bmi)
        if ((settings.SchedulerFlags14 & 2) != 0 && settings.MaxConcurrentIo2C - 1 >= 0x400) return 0x1F;   // 0x961730..0x961740
        Granularity220 = settings.Granularity10;                                // 0x961758 str r3,[r5,#0x220]
        MaxConcurrentIo04 = settings.MaxConcurrentIo2C;                         // 0x961760 str lr,[r5,#4]
        TargetLength224 = settings.TargetAutoStreamBufferLength28;              // 0x961764 str r7,[r5,#0x224]
        Id228 = id;                                                             // 0x96176C str ip,[r5,#0x228]
        CacheBytes22C = settings.MaxCachePinnedBytes34;                         // 0x961770 str r6,[r5,#0x22c]
        if (Manager.IoSeam.InitIoMemory(this, settings) != 1) return 2;         // 0x961774..0x961780
        return Manager.IoSeam.StartIoThread(this, settings);                    // 0x961790 b 0x97CC80
    }

    /// <summary>Device vt+0x18 (<c>0x961798</c>): the thread and the memory manager are torn down (the seam), the object freed.</summary>
    internal void Destroy() => Manager.IoSeam.DestroyDevice(this);

    /// <summary><c>0x97CFA0</c>: <c>[+0x18]++</c> under the device lock (the wake-up of the thread belongs to the seam).</summary>
    internal void StarvedUp97CFA0() { lock (Lock0C) Starved18++; }

    /// <summary><c>0x97CFF4</c>: <c>[+0x18]--</c> under the device lock.</summary>
    internal void StarvedDown97CFF4() { lock (Lock0C) Starved18--; }

    /// <summary>
    /// Device vt+0x24 (<c>0x9664B8</c>) and <c>0x962A2C</c>: a 0x78-byte stream is allocated (a failure calls <c>0x9611DC(dev, (sbyte)heur[0xD])</c>, unread, and retries once), constructed
    /// (<c>0x9642F4</c>), and initialised (<c>0x964730</c>); a failure destroys it and returns null. The stream is linked at the head of the device list under <c>[dev+0x50]</c>.
    /// </summary>
    internal WwiseAutoStream? CreateStream(WwiseFileDesc fileDesc, uint cacheId, WwiseStreamHeuristics heur, WwiseStreamBufferSettings? bufferSettings)
    {
        if (!Manager.TryAlloc())                                                // 0x9664EC bl 0xA7A7F4(pool, 0x78)
        {
            throw new WwiseMissingBehaviourException(
                "M6-025 D3: the stream allocation failed; 0x9611DC(dev, priority), which frees streams by priority and lets the allocation retry (0x9664F4..0x966574), is unread (C33 gives no row for it)");
        }
        var stream = new WwiseAutoStream(this);                                     // 0x9664F8 bl 0x9642F4; 0x966504 [S+0x70] = 0; 0x966528 byte [S+0x74] = 0
        int r = stream.Initialize964730(fileDesc, cacheId, heur, bufferSettings, Granularity220);   // 0x966540 bl 0x964730
        if (r != 1)                                                             // 0x966544 cmp r0,#1; bne 0x966590
        {
            stream.Destruct();                                                  // 0x966590..0x9665A8 (vt+0 then the free)
            return null;                                                        // 0x9665B0 mov r0,#0
        }
        lock (Lock50) _streams.Insert(0, stream);                               // 0x962A58..0x962A80 (the head)
        return stream;
    }

    internal void RemoveStream(WwiseAutoStream stream) { lock (Lock50) _streams.Remove(stream); }
}

/// <summary>
/// The stream manager (the object at <c>[0x108D798+0x10]</c>) and the globals of <c>0x108D798</c>: the resolver <c>[+0]</c>, the device array <c>[+4]</c> / count <c>[+8]</c> / capacity <c>[+0xC]</c>
/// and the language state. <see cref="CreateDevice"/> is <c>0x960F34</c>, <see cref="CreateAutoById"/> <c>0x95F334</c> (vt+0x1C), <see cref="CreateAutoByName"/> <c>0x95F0E8</c> (vt+0x18).
/// </summary>
public sealed class WwiseStreamManager
{
    private readonly List<WwiseStreamDevice?> _devices = new();
    private int _capacity;

    /// <summary>The I/O seam (C33.4). Required.</summary>
    public IWwiseStreamIoSeam IoSeam { get; }

    /// <summary><c>[G+0]</c>: the file-location resolver (<c>0x9606B4</c> / <c>0x9606C4</c>).</summary>
    public IWwiseFileLocationResolver? Resolver { get; set; }

    /// <summary>The language state (<c>[G+0x14..0x20]</c>, <c>0x960744</c>).</summary>
    public WwiseLanguageState Language { get; } = new();

    /// <summary>
    /// The pool allocator's failure (<c>0xA7A7F4</c> returning null): true fails that one allocation, in the engine's order (device, file descriptor, stream, open record). Null means none fails.
    /// </summary>
    public Func<bool>? AllocationFails { get; set; }

    /// <summary><c>[G+8]</c>: the device count (slots ever reserved).</summary>
    public int DeviceCount => _devices.Count;

    /// <summary><c>[G+0xC]</c>: the array capacity.</summary>
    public int DeviceCapacity => _capacity;

    /// <summary>Constructs the manager with its seam.</summary>
    public WwiseStreamManager(IWwiseStreamIoSeam ioSeam) => IoSeam = ioSeam ?? throw new ArgumentNullException(nameof(ioSeam));

    /// <summary>The device in a slot, or null.</summary>
    public WwiseStreamDevice? Device(int slot) => (uint)slot < (uint)_devices.Count ? _devices[slot] : null;

    internal bool TryAlloc() => AllocationFails?.Invoke() != true;

    /// <summary>
    /// <c>0x960F34(mgr, settings, hook)</c> (D1). The first empty slot is used, or the array grows by one when its capacity is not above the count (<c>0x9610A4..0x96111C</c>: the new capacity is capacity + 1);
    /// the count is incremented and the slot zeroed before the device is created (a failure leaves the slot reserved and empty). <c>settings[0x14] &amp; 1</c> creates the blocking device (0x250 bytes, ctor
    /// <c>0x966FCC</c>), <c>&amp; 2</c> the deferred one (0x248 bytes, <c>0x9688B4</c>, not built: Anki's settings are exactly 1), neither returns -1. The device's init (vt+0x14) not returning 1 destroys it and returns -1.
    /// Returns the slot.
    /// </summary>
    public int CreateDevice(WwiseDeviceSettings settings, IWwiseLowLevelIoHook hook)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(hook);
        int slot = -1;
        for (int i = 0; i < _devices.Count; i++)                                // 0x960F4C..0x960F84: the first null slot
            if (_devices[i] is null) { slot = i; break; }
        if (slot == -1)
        {
            if (_capacity <= _devices.Count)                                    // 0x960F94 cmp r4,r6; bls 0x9610A4
            {
                if (!TryAlloc()) return -1;                                     // 0x9610B8 bl 0xA7A7F4((cap+1)*4); 0x9610C0 beq 0x961054
                _capacity++;                                                    // 0x9610A8 add r4,r4,#1 ... 0x96112C str r4,[r3,#0xc]
            }
            slot = _devices.Count;                                              // 0x960FA0 mov r4,r6
            _devices.Add(null);                                                 // 0x960FB8 [G+8] = count + 1; 0x960FC4 arr[slot] = 0
        }
        if ((settings.SchedulerFlags14 & 1) == 0)
        {
            if ((settings.SchedulerFlags14 & 2) == 0) return -1;                // 0x961048 tst r3,#2 -> 0x961054
            throw new WwiseMissingBehaviourException("M6-025 D1: the deferred device (settings[0x14] & 2, ctor 0x9688B4) is not built; Anki's device settings are exactly 1 (A7)");
        }
        if (!TryAlloc()) return -1;                                             // 0x960FEC bl 0xA7A7F4(pool, 0x250); beq 0x961054
        var device = new WwiseStreamDevice(this, hook);                         // 0x960FFC bl 0x966FCC
        if (device.Init(settings, slot) != 1)                                   // 0x961010 blx vt+0x14; 0x961018 cmp r0,#1
        {
            device.Destroy();                                                   // 0x961020..0x96102C vt+0x18
            return -1;                                                          // 0x961030 mvn r0,#0
        }
        _devices[slot] = device;                                                // 0x961094 str r6,[r3,r5]
        return slot;                                                            // 0x961088 mov r0,r4
    }

    /// <summary>
    /// <c>0x95F334(mgr, fileId, flags, heur, bufferSettings, &amp;out, bSyncOpen)</c> (D3, vt+0x1C). A negative throughput or a priority above 0x64 returns 0x1F. A 0x20-byte descriptor is allocated (failure: 2); a non-null
    /// <paramref name="flags"/> gets <c>byte [+0x12] = 1</c> and gives the cache id <c>[+0x14]</c> (otherwise -1); the resolver's <c>vt+0xC</c> opens by id; a result other than 1 frees the descriptor and
    /// returns 0x42 when it was 0x42, else 2. A synchronous open with <c>iFileSize &lt; 1</c> frees the descriptor and returns 2 (no hook close). The device is <c>devices[fileDesc[0x18]]</c> (unsigned bound, null returns
    /// 2); the stream is created through it; a null one closes the file through the hook only for a synchronous open, and returns 2. A synchronous open sets <c>[S+0x2D] |= 0x10</c> and <c>[S+0x10] = fileDesc</c>;
    /// otherwise the deferred open <c>0x96382C</c> runs and its failure sets bit 3, clears bit 7, calls <c>S vt+0xC</c> and returns 2. <paramref name="stream"/> is written only on success.
    /// </summary>
    public int CreateAutoById(uint fileId, WwiseFileFlags? flags, WwiseStreamHeuristics heur, WwiseStreamBufferSettings? bufferSettings,
        ref WwiseAutoStream? stream, bool bSyncOpen)
        => CreateAuto(null, fileId, byName: false, flags, heur, bufferSettings, ref stream, bSyncOpen);

    /// <summary>
    /// <c>0x95F0E8(mgr, name, ...)</c> (vt+0x18): as <see cref="CreateAutoById"/> with the name; a null name returns 0x1F first (0x95F0EC cmp r1,#0; beq), the resolver's <c>vt+8</c> opens by name, and the deferred open is
    /// <c>0x9637F0</c> (the record is built by <c>0x960A08</c>, which copies the name).
    /// </summary>
    public int CreateAutoByName(string? name, WwiseFileFlags? flags, WwiseStreamHeuristics heur, WwiseStreamBufferSettings? bufferSettings,
        ref WwiseAutoStream? stream, bool bSyncOpen)
    {
        if (name is null) return 0x1F;                                          // 0x95F0EC cmp r1,#0; beq 0x95F118
        return CreateAuto(name, 0, byName: true, flags, heur, bufferSettings, ref stream, bSyncOpen);
    }

    private int CreateAuto(string? name, uint fileId, bool byName, WwiseFileFlags? flags, WwiseStreamHeuristics heur, WwiseStreamBufferSettings? bufferSettings,
        ref WwiseAutoStream? stream, bool bSyncOpen)
    {
        ArgumentNullException.ThrowIfNull(heur);
        if (heur.Throughput < 0f || heur.Priority > 0x64) return 0x1F;          // 0x95F340..0x95F35C (vcmpe; bmi / ldrb; cmp #0x64; bls)
        uint cacheId = 0xFFFFFFFF;                                              // 0x95F384 mvneq r8,#0
        if (flags is not null) { flags.Byte12 = 1; cacheId = flags.CacheId; }   // 0x95F37C strbne; 0x95F390 ldrne r8,[r2,#0x14]
        if (!TryAlloc()) return 2;                                              // 0x95F398 bl 0xA7A7F4(pool, 0x20); 0x95F39C beq 0x95F418
        var fileDesc = new WwiseFileDesc();                                     // 0x95F3AC bl memset
        var resolver = Resolver ?? throw new InvalidOperationException("M6-025 D3: no file-location resolver is set ([0x108D798] is null; the call through it crashes in the engine)");
        int r = byName
            ? resolver.OpenByName(name!, 0, flags, ref bSyncOpen, fileDesc)     // 0x95F198 vt+8
            : resolver.OpenById(fileId, 0, flags, ref bSyncOpen, fileDesc);     // 0x95F3DC vt+0xC
        if (r != 1) return r == 0x42 ? 0x42 : 2;                                // 0x95F3E4 cmp r0,#1; 0x95F3F0..0x95F404 (the descriptor is freed first)
        if (bSyncOpen && fileDesc.FileSize < 1) return 2;                       // 0x95F420..0x95F438 (ldrd; cmp r2,#1; sbcs r3,r3,#0; blt 0x95F40C): freed, no hook close (verifier correction 5)
        var device = Device(fileDesc.DeviceId);                                 // 0x95F43C..0x95F47C (unsigned bound)
        if (device is null) return 2;                                           // 0x95F454..0x95F468 (freed)
        var created = device.CreateStream(fileDesc, cacheId, heur, bufferSettings);   // 0x95F4A8 bl 0x962A2C
        if (created is null)                                                    // 0x95F4AC subs r4,r0,#0; beq 0x95F4E4
        {
            if (bSyncOpen) device.Hook.Close(fileDesc);                         // 0x95F4E4..0x95F500 (the hook's vt+8, only for a synchronous open)
            return 2;                                                           // 0x95F518
        }
        if (bSyncOpen)                                                          // 0x95F4B4..0x95F4BC
        {
            created.Flags2D |= 0x10;                                            // 0x95F4C0..0x95F4CC
            created.FileDesc10 = fileDesc;                                      // 0x95F4C4 str sb,[r4,#0x10]
            stream = created;                                                   // 0x95F4D0..0x95F4DC *out = iface
            return 1;
        }
        int d = byName                                                          // 0x95F528..0x95F530
            ? created.DeferOpenByName9637F0(fileDesc, name!, flags)
            : created.DeferOpenById96382C(fileDesc, fileId, flags);
        if (d == 1) { stream = created; return 1; }                             // 0x95F534 cmp r0,#1; beq 0x95F4D0
        created.Flags2D = (byte)((created.Flags2D | 8) & ~0x80);                // 0x95F53C..0x95F550 (bit 3 set, bit 7 cleared)
        created.Kill962028();                                                   // 0x95F554 vt+0xC
        return 2;                                                               // 0x95F55C
    }
}

/// <summary>
/// The auto stream S (0x78 bytes, vptr <c>0x1039240</c>, IAkAutoStream subobject at <c>+0x30</c> with vptr <c>0x10392D0</c>). The fields keep the engine's offsets in their names. The methods are the primary
/// slots (<c>vt+0x44 GetHeuristics</c>, <c>+0x48 SetHeuristics</c>, <c>+0x4C SetMinimalBufferSize</c>, <c>+0x58 Start</c>, <c>+0x5C Stop</c>, <c>+0x60 GetPosition</c>, <c>+0x64 SetPosition</c>,
/// <c>+0x68 GetBuffer</c>, <c>+0x6C ReleaseBuffer</c>, <c>+0x70 Query</c>, <c>+0x24 GetNominalBuffering</c>, <c>+0x74</c> next position, <c>+0x78</c> cancel, <c>+0x7C</c> seek, <c>+0x84</c> loop end).
/// </summary>
public sealed class WwiseAutoStream
{
    private readonly List<WwiseStreamNode> _nodes = new();                      // [S+0x68] head ... [S+0x64] tail, [S+0x60] count

    /// <summary>The lock <c>[S+0x14]</c> (a recursive mutex).</summary>
    internal readonly object Lock14 = new();

    /// <summary>The device (<c>[S+0x20]</c>).</summary>
    public WwiseStreamDevice Device20 { get; }

    /// <summary><c>[S+4]</c>: the waiter flag (<c>0x97D0B4</c> waits on it, <c>0x97D100</c> clears it); GetBuffer's waiting path is not reachable from Anki's sources (bWait is always false).</summary>
    public int Waiter04 { get; set; }

    /// <summary><c>[S+0xC]</c>: the pending open record (<c>0x960B04</c>); null when none.</summary>
    public WwiseOpenRecord? Record0C { get; private set; }

    /// <summary><c>[S+0x10]</c>: the file descriptor.</summary>
    public WwiseFileDesc? FileDesc10 { get; internal set; }

    /// <summary><c>[S+0x18..0x1F]</c>: the device time stamp copied by Start.</summary>
    public long Time18 { get; internal set; }

    /// <summary><c>[S+0x28]</c>: the block size.</summary>
    public uint BlockSize28 { get; private set; }

    /// <summary><c>[S+0x2C]</c> (byte): the priority.</summary>
    public byte Priority2C { get; private set; }

    /// <summary><c>[S+0x2D]</c> (byte): bit 0 (set by the constructor), bit 2 end of file, bit 3 destroy, bit 4 opened, bit 5 starved, bit 6 caching stream, bit 7 ready for I/O.</summary>
    public byte Flags2D { get; internal set; }

    /// <summary><c>[S+0x38]</c> (64 bit): the position the next GetBuffer must deliver.</summary>
    public ulong Position38 { get; private set; }

    /// <summary><c>[S+0x40]</c>: the cache id.</summary>
    public uint CacheId40 { get; private set; } = 0xFFFFFFFF;

    /// <summary><c>[S+0x44]</c> (a float): the throughput.</summary>
    public float Throughput44 { get; private set; }

    /// <summary><c>[S+0x48]</c>: the loop start (aligned to the block size).</summary>
    public uint LoopStart48 { get; private set; }

    /// <summary><c>[S+0x4C]</c>: the loop end.</summary>
    public uint LoopEnd4C { get; private set; }

    /// <summary><c>[S+0x50]</c>: the buffer size.</summary>
    public uint BufferSize50 { get; private set; }

    /// <summary><c>[S+0x54]</c>: the buffered amount (the accounted bytes of the ungranted buffers and the transfer in flight).</summary>
    public uint Buffered54 { get; internal set; }

    /// <summary><c>[S+0x58]</c>: the minimum buffer size.</summary>
    public uint MinBuffer58 { get; private set; }

    /// <summary><c>[S+0x5C]</c>: the block size (copy).</summary>
    public uint BlockSize5C { get; private set; }

    /// <summary><c>[S+0x6C]</c> (byte): the number of buffers granted to the client.</summary>
    public byte Granted6C { get; private set; }

    /// <summary><c>[S+0x6D]</c> (byte).</summary>
    public byte Byte6D { get; private set; }

    /// <summary><c>[S+0x6E]</c> (byte): bit 0 started, bit 1 error, bit 2.</summary>
    public byte Flags6E { get; internal set; }

    /// <summary><c>[S+0x70]</c>: the transfer in flight (a record), or null.</summary>
    public WwiseStreamNode? InFlight70 { get; internal set; }

    /// <summary><c>[S+0x74]</c> (byte).</summary>
    public byte Byte74 { get; internal set; }

    /// <summary>The ungranted and granted buffers (<c>[S+0x60]</c> count, head first).</summary>
    public IReadOnlyList<WwiseStreamNode> Nodes => _nodes;

    /// <summary><c>0x9642F4</c> and the stores of <c>0x9664B8</c>: the lists empty, <c>[S+0x2D] = 1</c> (bit 0 set, bit 1 clear, the rest zero), <c>[S+0x6E] &amp; ~7</c> (the other bits are pool garbage, taken as 0),
    /// <c>[S+0x40] = -1</c>, <c>[S+0x70] = 0</c>, <c>[S+0x74] = 0</c>.</summary>
    internal WwiseAutoStream(WwiseStreamDevice device)
    {
        Device20 = device;
        Flags2D = 1;
    }

    // ---------------------------------------------------------------- creation: D5, D6, D7

    /// <summary>
    /// D6 <c>0x964730(S, dev, fileDesc, cacheId, heur, bufferSettings, granularity)</c>. The device is stored; a negative file size sets bit 3, clears bit 7 and returns 0x1F. The block size is the hook's
    /// (<c>vt+0xC</c>, 1); 0, above the granularity, or not dividing it, sets bit 3, clears bit 7 and returns 2. Then <c>[S+0x28] = [S+0x5C] = blockSize</c>; the throughput is <c>max(heur[0], 1.0f)</c> (NaN gives 1.0f);
    /// <c>[S+0x48] = loopStart - loopStart % blockSize</c>; <c>[S+0x4C] = min(fileSize, loopEnd)</c>; the byte <c>[S+0x6D]</c> is <c>heur[0xC]</c> or 1; the position is 0; <c>[S+0x2C] = heur[0xD]</c>; a file size of 0 sets
    /// bit 2 and clears bit 7. <c>0x964590</c> not returning 1 sets bit 3 and clears bit 7. Returns its result.
    /// </summary>
    internal int Initialize964730(WwiseFileDesc fileDesc, uint cacheId, WwiseStreamHeuristics heur, WwiseStreamBufferSettings? bufferSettings, uint granularity)
    {
        if (fileDesc.FileSize < 0)                                              // 0x964738..0x964748
        {
            Flags2D = (byte)((Flags2D | 8) & ~0x80);
            return 0x1F;
        }
        CacheId40 = cacheId;                                                    // 0x96476C str r3,[ip,#0x40]
        uint blockSize = Device20.Hook.GetBlockSize(fileDesc);                  // 0x964780 blx hook vt+0xC
        if (blockSize == 0 || blockSize > granularity || granularity % blockSize != 0)   // 0x964794..0x9647CC
        {
            Flags2D = (byte)((Flags2D | 8) & ~0x80);
            return 2;
        }
        BlockSize28 = blockSize;                                                // 0x9647D8
        BlockSize5C = blockSize;                                                // 0x9647E8
        float t = heur.Throughput;                                              // 0x9647E4 vldr s15,[r3]
        Throughput44 = t <= BitConverter.Int32BitsToSingle(0x3F800000) || float.IsNaN(t) ? BitConverter.Int32BitsToSingle(0x3F800000) : t;   // 0x9647EC..0x9647F8 (vcmpe; vmovle)
        uint rem = heur.LoopStart % blockSize;                                  // 0x964804
        long fileSize = fileDesc.FileSize;
        LoopEnd4C = fileSize < heur.LoopEnd ? unchecked((uint)fileSize) : heur.LoopEnd;   // 0x964814..0x96482C (signed 64-bit compare, movlt)
        LoopStart48 = heur.LoopStart - rem;                                     // 0x964838
        Byte6D = heur.MinNumBuffers == 0 ? (byte)1 : heur.MinNumBuffers;        // 0x96483C..0x96484C
        Position38 = 0;                                                         // 0x964858 vstr d16,[r4,#0x38]
        Priority2C = heur.Priority;                                             // 0x96485C
        if (fileSize == 0) Flags2D = (byte)((Flags2D | 4) & ~0x80);             // 0x964860..0x964878
        int r = SetBufferGeometry964590(bufferSettings, granularity);           // 0x96487C bl 0x964590
        if (r != 1) Flags2D = (byte)((Flags2D | 8) & ~0x80);                    // 0x964880..0x964890
        return r;
    }

    /// <summary>
    /// D5 <c>0x964590(S, bufferSettings, granularity)</c>. Without settings: <c>[S+0x58] = blockSize</c>, <c>[S+0x50] = g - g % blockSize</c>. With settings and a zero <c>+8</c>: <c>[S+0x58] = blockSize</c>,
    /// <c>[S+0x50]</c> as above; a non-zero <c>+0</c> must not exceed the granularity and must be a multiple of the block size (else 2) and then sets both; a <c>+4</c> of 0 or at most the block size keeps the block
    /// size, otherwise <c>[S+0x58] = ceil(min / blockSize) * blockSize</c> and a <c>[S+0x50]</c> below it returns 2. A non-zero <c>+8</c> (an explicit block size) is read in the engine
    /// (<c>0x964620..0x9646DC</c>) but no source passes one, so it is not built.
    /// </summary>
    private int SetBufferGeometry964590(WwiseStreamBufferSettings? bs, uint granularity)
    {
        if (bs is null)                                                         // 0x964594 subs r7,r1,#0; beq 0x9646E0
        {
            uint block = BlockSize28;                                           // 0x9646E0 ldr r4,[r0,#0x28]
            MinBuffer58 = block;                                                // 0x9646F0
            BufferSize50 = granularity - granularity % block;                   // 0x9646F8
            return 1;
        }
        if (bs.BlockSize != 0)                                                  // 0x9645A4..0x9645AC
            throw new WwiseMissingBehaviourException("M6-025 D5: the explicit block size branch 0x964620..0x9646DC is read but unused: no source passes a block size");
        uint r5 = BlockSize5C;                                                  // 0x9645B0
        MinBuffer58 = r5;                                                       // 0x9645C4
        uint size50 = granularity - granularity % r5;                           // 0x9645BC..0x9645CC
        BufferSize50 = size50;
        if (bs.BufferSize != 0)                                                 // 0x9645C8 cmp r4,#0; beq 0x9645E8
        {
            if (granularity < bs.BufferSize) return 2;                          // 0x9645D8 cmp r8,r4; bhs 0x964704 (not taken) -> 2
            if (bs.BufferSize % r5 != 0) return 2;                              // 0x96470C..0x964714
            BufferSize50 = bs.BufferSize;                                       // 0x964718
            MinBuffer58 = bs.BufferSize;                                        // 0x964720
            return 1;
        }
        uint min = bs.MinBufferSize;                                            // 0x9645E8
        if (min == 0 || min <= r5) return 1;                                    // 0x9645EC..0x9645F4 (cmpne; bls)
        uint rounded = (min - 1 + r5) / r5 * r5;                                // 0x9645F8..0x964608
        MinBuffer58 = rounded;                                                  // 0x964610
        return size50 < rounded ? 2 : 1;                                        // 0x96460C..0x964614
    }

    /// <summary>
    /// D7 <c>0x96382C(S, fileDesc, fileId, flags, 0)</c>: <c>[S+0x10] = fileDesc</c>; bit 4 of <c>[S+0x2D]</c> is cleared; the open record is built (<c>0x960B04</c>: a 0x28-byte record with the id, a copy of the flags and bit 1
    /// set when flags were given, mode 0); an allocation failure returns 2, otherwise 1.
    /// </summary>
    internal int DeferOpenById96382C(WwiseFileDesc fileDesc, uint fileId, WwiseFileFlags? flags)
    {
        FileDesc10 = fileDesc;                                                  // 0x96383C
        Flags2D = (byte)(Flags2D & ~0x10);                                      // 0x963844 bfc ip,#4,#1
        if (!Device20.Manager.TryAlloc()) { Record0C = null; return 2; }        // 0x960B24 bl 0xA7A7F4(pool, 0x28); beq 0x960B70
        Record0C = new WwiseOpenRecord(fileId, null, flags?.Clone(), mode: 0);  // 0x960B30..0x960B6C
        return 1;                                                               // 0x96385C movne r0,#1
    }

    /// <summary>D7 by name <c>0x9637F0</c> with the record builder <c>0x960A08</c> (a copy of the name).</summary>
    internal int DeferOpenByName9637F0(WwiseFileDesc fileDesc, string name, WwiseFileFlags? flags)
    {
        FileDesc10 = fileDesc;                                                  // 0x9637FC
        Flags2D = (byte)(Flags2D & ~0x10);                                      // 0x963808
        if (!Device20.Manager.TryAlloc()) { Record0C = null; return 2; }        // 0x960A28 bl 0xA7A7F4(pool, 0x28)
        if (!Device20.Manager.TryAlloc()) { Record0C = null; return 2; }        // 0x960A80..0x960A90 the name's copy (strlen + 1)
        Record0C = new WwiseOpenRecord(0, name, flags?.Clone(), mode: 0);       // 0x960A34..0x960AC8
        return 1;
    }

    /// <summary>
    /// <c>0x963890(S)</c> (run by the I/O thread): a missing record or bit 3 returns 1; the record is opened with the resolver (<c>0x960BDC</c>: <c>bSyncOpen = 1</c>, by id <c>vt+0xC</c> or by name <c>vt+8</c>, a result other
    /// than 1 returns 2; an <c>iFileSize &lt; 1</c> with open mode 0 returns 2; a local <c>bSyncOpen</c> of 0 returns 2). A failure frees the record and returns its code. A success runs <c>vt+0x10</c>
    /// (<c>0x961A6C</c>: bit 4 set, the loop end clamped to the file size) and frees the record.
    /// </summary>
    internal int RunOpen963890()
    {
        var record = Record0C;
        if (record is null || (Flags2D & 8) != 0) return 1;                     // 0x963898..0x9638AC
        var target = FileDesc10 ?? throw new InvalidOperationException("M6-025 D7: the open record has no descriptor");
        int r = record.Open960BDC(Device20.Manager.Resolver ?? throw new InvalidOperationException("M6-025 D7: no resolver"), target);   // 0x9638C0 bl 0x960BDC
        if (r != 1)                                                             // 0x9638C4..0x9638CC
        {
            Record0C = null;                                                    // 0x9638DC bl 0x960B8C; 0x9638E8 str 0
            return r;
        }
        PostOpen961A6C();                                                       // 0x9638F8..0x963904 vt+0x10
        Record0C = null;                                                        // 0x9638D0..0x9638E8
        return 1;
    }

    /// <summary>
    /// <c>0x961A6C(S)</c> (vt+0x10): <c>[S+0x2D] |= 0x10</c>; when the file size (64 bit signed) is below the loop end <c>[S+0x4C]</c> the heuristics are read (<c>vt+0x44</c>) and written back (<c>vt+0x48</c>) with the loop end
    /// replaced by the file size's low word.
    /// </summary>
    private void PostOpen961A6C()
    {
        Flags2D |= 0x10;                                                        // 0x961A84..0x961A88
        long size = FileDesc10!.FileSize;                                       // 0x961A8C ldrd r6,r7,[r1]
        if (size < LoopEnd4C)                                                   // 0x961A90..0x961A98 (cmp; sbcs; bge)
        {
            var h = new WwiseStreamHeuristics();
            GetHeuristics961664(h);                                             // 0x961AA8 vt+0x44
            h.LoopEnd = unchecked((uint)size);                                  // 0x961AC8 str r2,[sp,#8]
            SetHeuristics964E4C(h);                                             // 0x961AC4 vt+0x48
        }
    }

    // ---------------------------------------------------------------- the nodes: accounting

    /// <summary>
    /// The accounted bytes of a node (<c>0x96747C</c>'s loop body, <c>0x9653E0..0x9654B8</c>, <c>0x965BCC..0x965BF8</c>): <c>size - offset</c> when <c>start + offset</c> is at or above the loop end (the loop end is
    /// zero-extended, so 0 means everything), otherwise the bytes up to the loop end when the buffer ends beyond it, else <c>size - offset</c>.
    /// </summary>
    internal uint Accounted(WwiseStreamNode node)
    {
        var buf = node.Buffer!;
        ulong startOff = buf.Start + node.Offset;
        ulong loopEnd = LoopEnd4C;
        if (startOff >= loopEnd) return buf.Size - node.Offset;
        ulong end = buf.Start + buf.Size;
        if (end > loopEnd) return unchecked((uint)(loopEnd - startOff));
        return buf.Size - node.Offset;
    }

    private WwiseStreamNode? NodeAt(int index) => index < _nodes.Count ? _nodes[index] : null;

    private int TailIndex => _nodes.Count - 1;

    // ---------------------------------------------------------------- the small slots

    /// <summary>vt+0x44 GetHeuristics (<c>0x961664</c>): throughput, loop start, loop end, the bytes <c>[S+0x6D]</c> and <c>[S+0x2C]</c>.</summary>
    public void GetHeuristics961664(WwiseStreamHeuristics h)
    {
        h.Throughput = Throughput44;                                            // 0x961668..0x961670
        h.LoopStart = LoopStart48;                                              // 0x96166C
        h.LoopEnd = LoopEnd4C;                                                  // 0x961664
        h.MinNumBuffers = Byte6D;                                               // 0x961678
        h.Priority = Priority2C;                                                // 0x961680
    }

    private static float CvtF32(uint v) => v;                                   // vcvt.f32.u32

    /// <summary>
    /// vt+0x24 GetNominalBuffering (<c>0x961AD8</c>): bit 6 of <c>[S+0x2D]</c> returns <c>[S+0x44]</c> as an integer; otherwise <c>(u32)(throughput * [dev+0x224])</c>, a single-precision multiply converted with
    /// <c>vcvt.u32.f32</c> (round toward zero, saturating, NaN to 0).
    /// </summary>
    public uint GetNominalBuffering961AD8()
    {
        if ((Flags2D & 0x40) != 0) return BitConverter.SingleToUInt32Bits(Throughput44);   // 0x961ADC..0x961AE8 ldrne r0,[r0,#0x44]
        float p = Throughput44 * Device20.TargetLength224;                      // 0x961AF0 vmul.f32
        return CvtU32(p);                                                       // 0x961AF4 vcvt.u32.f32
    }

    internal static uint CvtU32(float v)
    {
        if (float.IsNaN(v) || v <= 0f) return 0;
        if (v >= 4294967296f) return uint.MaxValue;
        return (uint)v;
    }

    /// <summary>vt+0x74 (<c>0x9666E8</c>): the position after the last buffer: the transfer in flight's, else <c>[S+0x38]</c> when every buffer is granted, else the tail's.</summary>
    internal ulong NextPosition9666E8()
    {
        if (InFlight70 is { } flight) { var b = flight.Buffer!; return b.Start + b.Size; }                  // 0x9666E8..0x966708
        if (Granted6C >= _nodes.Count) return Position38;                       // 0x96670C..0x966720
        var tail = _nodes[TailIndex].Buffer!;                                   // 0x966724 ldr r3,[r0,#0x64]
        return tail.Start + tail.Size;
    }

    /// <summary>vt+8 (<c>0x965F5C</c>): under the lock, 0 when a transfer is in flight, else <c>[S+0x74] ^ 1</c>.</summary>
    internal bool TryDestroy965F5C() { lock (Lock14) return InFlight70 is null && Byte74 == 0; }

    /// <summary>
    /// vt+0x60 GetPosition (<c>0x961870</c>): the first node's <c>start + offset</c>, else <c>[S+0x38]</c>; the optional flag is <c>fileSize &lt;= position</c>.
    /// </summary>
    public ulong GetPosition961870(out bool atEnd)
    {
        lock (Lock14)                                                           // 0x961884
        {
            ulong pos;
            if (_nodes.Count != 0) { var n = _nodes[0]; pos = n.Buffer!.Start + n.Offset; }   // 0x961894..0x9618A0
            else pos = Position38;                                              // 0x9618DC
            atEnd = (ulong)FileDesc10!.FileSize <= pos;                         // 0x9618AC..0x9618C0 (the flag is only stored when the pointer is non-null)
            return pos;
        }
    }

    /// <summary>vt+0x84 (<c>0x96757C</c>): <c>[S+0x4C] = loopEnd</c>; <c>[S+0x54]</c> is recomputed (<c>0x96747C</c>).</summary>
    internal void SetLoopEnd96757C(uint loopEnd)
    {
        LoopEnd4C = loopEnd;
        Buffered54 = Recompute96747C();
    }

    /// <summary>
    /// <c>0x96747C(S)</c>: the sum of <see cref="Accounted"/> over the nodes from the first ungranted one, plus the transfer in flight's accounted bytes (the same formula).
    /// </summary>
    private uint Recompute96747C()
    {
        uint sum = 0;
        for (int i = Granted6C; i < _nodes.Count; i++) sum = unchecked(sum + Accounted(_nodes[i]));   // 0x9674A4..0x967508 (the walk skips Granted6C nodes)
        if (InFlight70 is { } f) sum = unchecked(sum + Accounted(f));          // 0x96750C..0x96754C
        return sum;
    }

    /// <summary>
    /// vt+0x78 (<c>0x96672C</c>): a transfer in flight is cancelled: its state becomes 2, its accounted bytes leave <c>[S+0x54]</c>, its offset becomes its size, <c>[S+0x70] = 0</c> and <c>[S+0x74] = 1</c>.
    /// </summary>
    internal void CancelInFlight96672C()
    {
        if (InFlight70 is not { } f) return;                                    // 0x96672C cmp r3,#0; bxeq lr
        f.State = (byte)((f.State & ~7) | 2);                                   // 0x966764 bfi r1,sl,#0,#3
        Buffered54 = unchecked(Buffered54 - Accounted(f));                      // 0x966780..0x966790
        f.Offset = f.Buffer!.Size;                                              // 0x966794 str r1,[r3,#8]
        InFlight70 = null;                                                      // 0x966798
        Byte74 = 1;                                                             // 0x96679C
    }

    /// <summary>
    /// vt+0x7C (<c>0x9667C0(S, position)</c>): with no transfer in flight nothing happens; a transfer whose buffer ends at <paramref name="position"/> is kept; any other is cancelled as
    /// <see cref="CancelInFlight96672C"/> does.
    /// </summary>
    internal void Seek9667C0(ulong position)
    {
        if (InFlight70 is not { } f) return;                                    // 0x9667C8..0x9667D0
        var b = f.Buffer!;
        if (b.Start + f.Offset == position) return;                             // 0x9667DC..0x9667F0 beq 0x966848 (start + offset equals the target)
        CancelInFlight96672C();                                                 // 0x9667F4..0x966840 (the same stores)
    }

    // ---------------------------------------------------------------- UpdateStatus 0x964B8C / 0x964A20

    /// <summary>
    /// <c>0x964B8C</c> (and the tail of <c>0x964A20</c>, <paramref name="wake"/>): the end-of-file, readiness and starvation bookkeeping. With <c>[S+0x4C] == 0</c> and the next position at or past the file size and the file
    /// opened, bit 2 is set and bit 7 cleared; otherwise bit 2 is cleared and bit 7 becomes <c>started &amp;&amp; bit 3 clear</c>. A ready stream whose buffered amount is below its nominal buffering becomes starved
    /// (bit 5 set, <c>0x97CFA0</c>); a stream that is not ready and is flagged for destroy is starved while it cannot be destroyed; a stream that is not starved-eligible clears bit 5 (<c>0x97CFF4</c>).
    /// </summary>
    internal void UpdateStatus964B8C(bool wake = false)
    {
        bool endOfFile = false;
        if (LoopEnd4C == 0)                                                     // 0x964B94..0x964B9C
        {
            ulong pos = NextPosition9666E8();                                   // 0x964C0C blx vt+0x74
            if (pos >= (ulong)FileDesc10!.FileSize && (Flags2D & 0x10) != 0)    // 0x964C14..0x964C30
            {
                Flags2D = (byte)((Flags2D | 4) & ~0x80);                        // 0x964C34..0x964C3C
                endOfFile = true;
            }
        }
        if (!endOfFile)
        {
            bool started = (Flags6E & 1) != 0;                                  // 0x964BA0..0x964BA8
            Flags2D = (byte)(Flags2D & ~4);                                     // 0x964BAC bfc r2,#2,#1
            bool ready = started && (Flags2D & 8) == 0;                         // 0x964BF8..0x964C04 (eor #8; ubfx #3)
            Flags2D = (byte)((Flags2D & ~0x80) | (ready ? 0x80 : 0));           // 0x964BBC bfi r2,r3,#7,#1
        }
        bool starve = false;
        bool clear5 = false;
        if ((Flags2D & 0x80) != 0)                                              // 0x964BC4..0x964BC8 / 0x964C40..0x964C48
        {
            uint nominal = GetNominalBuffering961AD8();                         // 0x964C58 vt+0x24
            if (Buffered54 < nominal) starve = true;                            // 0x964C60 cmp r5,r0; bhs 0x964CB0
        }
        if (!starve)
        {
            if ((Flags2D & 8) != 0)                                             // 0x964BD0 tst r3,#8
            {
                if (TryDestroy965F5C()) starve = true;                          // 0x964C88..0x964C9C (vt+8 non-zero)
                else if ((Flags2D & 0x20) != 0) clear5 = true;                  // 0x964CA0..0x964CAC
            }
            else if ((Flags2D & 0x20) != 0) clear5 = true;                      // 0x964BD8..0x964BDC
        }
        if (starve)
        {
            if ((Flags2D & 0x20) == 0)                                          // 0x964C68..0x964C74
            {
                Flags2D |= 0x20;                                                // 0x964C74 orr r3,r3,#0x20
                Device20.StarvedUp97CFA0();                                     // 0x964C84 b 0x97CFA0
            }
        }
        else if (clear5)
        {
            Flags2D = (byte)(Flags2D & ~0x20);                                  // 0x964BE8
            Device20.StarvedDown97CFF4();                                       // 0x964BF4 b 0x97CFF4
        }
        if (wake && Waiter04 != 0) WakeWaiters97D100();                         // 0x964A7C..0x964A94
    }

    private void WakeWaiters97D100() { Waiter04 = 0; }                          // 0x97D100: [S+4] = 0 and the condition is broadcast

    // ---------------------------------------------------------------- buffer lists: 0x965364, 0x964914, 0x964D64, Destroy

    /// <summary>
    /// <c>0x965364(S)</c>: the transfer in flight is cancelled (<c>vt+0x78</c>); then every ungranted buffer is unlinked, its accounted bytes leave <c>[S+0x54]</c>, its buffer returns to the memory manager
    /// (<c>0x96FE70</c>) and the node is freed, under the device lock; <c>0x964B8C</c> runs last.
    /// </summary>
    internal void ReleaseAll965364()
    {
        CancelInFlight96672C();                                                 // 0x965370 blx vt+0x78
        if (Granted6C < _nodes.Count)                                           // 0x965384 cmp r2,r3; bhs 0x9654C4
        {
            lock (Device20.Lock0C)                                              // 0x9653C4
            {
                while (_nodes.Count > Granted6C)                                // 0x96543C..0x965438
                {
                    var node = _nodes[Granted6C];
                    _nodes.RemoveAt(Granted6C);                                 // 0x965440..0x96547C (unlink; count--)
                    Buffered54 = unchecked(Buffered54 - Accounted(node));       // 0x9653FC..0x965410
                    var buffer = node.Buffer!;
                    node.Buffer = null;                                         // 0x96541C str r3,[sl,#4]
                    Device20.Manager.IoSeam.ReleaseBuffer(Device20, buffer);    // 0x965420 bl 0x96FE70
                }
            }
        }
        UpdateStatus964B8C();                                                   // 0x9654D0 b 0x964B8C
    }

    /// <summary>
    /// <c>0x964914(S, node, flag)</c>: with <paramref name="flag"/> non-zero, bit 3 clear and bit 1 of <c>[S+0x6E]</c> clear, the node becomes state 3 and is appended; every other case releases it under the device lock:
    /// its accounted bytes leave <c>[S+0x54]</c>, <c>[node+4] = 0</c> and the buffer returns to the memory manager.
    /// </summary>
    internal void AppendOrRelease964914(WwiseStreamNode node, bool flag)
    {
        if (flag && (Flags2D & 8) == 0 && (Flags6E & 2) == 0)                   // 0x964914..0x964938
        {
            node.State = (byte)((node.State & ~7) | 3);                         // 0x964940..0x964954 bfi r3,r2,#0,#3
            _nodes.Add(node);                                                   // 0x964958..0x964970
            return;
        }
        lock (Device20.Lock0C)                                                  // 0x964988
        {
            Buffered54 = unchecked(Buffered54 - Accounted(node));               // 0x9649B8..0x9649D0
            var buffer = node.Buffer!;
            node.Buffer = null;                                                 // 0x9649D8 str r2,[r5,#4]
            Device20.Manager.IoSeam.ReleaseBuffer(Device20, buffer);            // 0x9649DC bl 0x96FE70
        }
    }

    /// <summary>
    /// vt+0x44 / IAkAutoStream+0x44 ReleaseBuffer (<c>0x964D64</c>): 2 when no buffer is granted; otherwise under the device lock the first node leaves the list, its buffer returns to the memory manager,
    /// <c>[S+0x6C]--</c>, <c>0x964B8C</c>, and 1.
    /// </summary>
    public int ReleaseBuffer964D64()
    {
        lock (Lock14)                                                           // 0x964D74
        {
            if (Granted6C == 0) return 2;                                       // 0x964D7C cmp r3,#0; moveq r6,#2
            lock (Device20.Lock0C)                                              // 0x964DA8
            {
                var node = _nodes.Count > 0 ? _nodes[0] : null;                 // 0x964D9C ldr r7,[r4,#0x68]
                if (node is not null) _nodes.RemoveAt(0);                       // 0x964DAC..0x964DD0 (pop the head; count--)
                var buffer = node?.Buffer;
                if (node is not null) node.Buffer = null;                       // 0x964DD8 str r3,[r7,#4]
                if (buffer is not null) Device20.Manager.IoSeam.ReleaseBuffer(Device20, buffer);   // 0x964DF0 bl 0x96FE70 (or the path 0x964E2C)
            }
            Granted6C--;                                                        // 0x964E10..0x964E20
            UpdateStatus964B8C();                                               // 0x964E24
            return 1;                                                           // 0x964E18 mov r6,#1
        }
    }

    /// <summary>
    /// IAkAutoStream+8 Destroy (<c>0x9654E4</c>): under the lock bit 3 is set and bit 7 cleared; the granted buffers' accounting is undone (<c>[S+0x54] += accounted</c> for each, <c>[S+0x6C]--</c>); the ungranted buffers
    /// are released (<c>0x965364</c>) and the list is emptied. The stream object stays in the device list until the I/O thread destroys it (<see cref="TryDestroy965F5C"/>).
    /// </summary>
    public void Destroy9654E4()
    {
        lock (Lock14)                                                           // 0x9654F4
        {
            Flags2D = (byte)((Flags2D | 8) & ~0x80);                            // 0x965508..0x965510
            // 0x965518..0x96557C: for each granted node from the head ([S+0x68], then [r2]) the node's accounted bytes (the formula of 0x9653E0) are added back to [S+0x54] and [S+0x6C] is decremented.
            for (int i = 0; Granted6C != 0 && i < _nodes.Count; i++)
            {
                Buffered54 = unchecked(Buffered54 + Accounted(_nodes[i]));      // 0x965560..0x96556C
                Granted6C--;                                                    // 0x965564..0x965574
            }
            ReleaseAll965364();                                                 // 0x965588 bl 0x965364
            _nodes.Clear();                                                     // 0x96558C..0x96559C: [S+0x68] = [S+0x64] = [S+0x60] = 0 (the granted nodes are dropped, not freed)
        }
    }

    // ---------------------------------------------------------------- Start, Stop, SetPosition

    /// <summary>
    /// IAkAutoStream+0x30 Start (<c>0x964CB8</c>): bit 0 of <c>[S+0x6E]</c> already set returns 2 when bit 1 is set, else 1. Otherwise under the lock bit 0 is set; bit 7 of <c>[S+0x2D]</c> becomes
    /// <c>([S+0x2D] &amp; 0xC) == 0</c>; <c>0x964B8C</c>; bit 2 of <c>[S+0x6E]</c> is set; the device time stamp is copied to <c>[S+0x18]</c>; then under the device lock <c>0x97D020</c> runs; the result is 2 when bit 1 of
    /// <c>[S+0x6E]</c> is set, else 1.
    /// </summary>
    public int Start964CB8()
    {
        if ((Flags6E & 1) != 0) return (Flags6E & 2) != 0 ? 2 : 1;              // 0x964CBC..0x964CD0
        lock (Lock14)                                                           // 0x964CE4
        {
            Flags6E |= 1;                                                       // 0x964CF8
            bool ready = (Flags2D & 0xC) == 0;                                  // 0x964CF4 tst r3,#0xc
            Flags2D = (byte)((Flags2D & ~0x80) | (ready ? 0x80 : 0));           // 0x964D08
            UpdateStatus964B8C();                                               // 0x964D10
            Flags6E |= 4;                                                       // 0x964D20
            Time18 = Device20.Time40;                                           // 0x964D28..0x964D2C
        }
        lock (Device20.Lock0C)                                                  // 0x964D40
        {
            if (Device20.Flag1C)                                                // 0x97D020 ldrb r3,[r0,#0x1c]; cmp; bxeq
            {
                Device20.Flag1C = false;                                        // 0x97D034 strb r2,[r0,#0x1c]
                // 0x97D038..0x97D044: [dev+0x18] != 0 signals the thread's condition (the seam's business)
            }
        }
        return (Flags6E & 2) != 0 ? 2 : 1;                                      // 0x964CC8..0x964CD0
    }

    /// <summary>IAkAutoStream+0x34 Stop (<c>0x9655B0</c>): bit 0 of <c>[S+0x6E]</c> and bit 7 of <c>[S+0x2D]</c> are cleared, the ungranted buffers released, and 1 returned.</summary>
    public int Stop9655B0()
    {
        lock (Lock14)                                                           // 0x9655C0
        {
            Flags6E = (byte)(Flags6E & ~1);                                     // 0x9655D0
            Flags2D = (byte)(Flags2D & ~0x80);                                  // 0x9655D8
            ReleaseAll965364();                                                 // 0x9655E0
        }
        return 1;                                                               // 0x9655EC
    }

    /// <summary>
    /// vt+0xC (<c>0x962028</c>), the kill after a failed open: bit 1 of <c>[S+0x6E]</c> is set, the stream is stopped (<c>vt+0x5C</c>), and the status is updated with the waiters woken.
    /// </summary>
    internal void Kill962028()
    {
        lock (Lock14)                                                           // 0x962038
        {
            Flags6E |= 2;                                                       // 0x962048
            Stop9655B0();                                                       // 0x962050 vt+0x5C
            UpdateStatus964B8C(wake: true);                                     // 0x962058..0x9620BC
        }
    }

    /// <summary>
    /// IAkAutoStream+0x3C SetPosition (<c>0x9657E0(S, offset, method, &amp;real)</c>): method 0 is the begin, 1 adds <c>GetPosition</c>, 2 adds the file size, anything else returns 0x1F; a negative result returns 0x1F;
    /// the position is aligned down to the block size; <paramref name="real"/> is the aligned offset (relative for 1 and 2). Under the lock <c>[S+0x38]</c> is stored; with every buffer granted <c>vt+0x7C</c> runs,
    /// otherwise the buffers are kept when the first ungranted one starts at the position and released (<c>0x965364</c>) when not; <c>0x964B8C</c>; returns 1.
    /// </summary>
    public int SetPosition9657E0(long offset, int method, out long real)
    {
        real = 0;                                                               // 0x965800 vstr d16,[r5]
        long off = offset;
        switch (method)
        {
            case 0: break;
            case 1: off += (long)GetPosition961870(out _); break;               // 0x96593C vt+0x60
            case 2: off += FileDesc10!.FileSize; break;                         // 0x965828..0x965834
            default: return 0x1F;                                               // 0x965820
        }
        if (off < 0) return 0x1F;                                               // 0x965838..0x965840
        ulong rem = (ulong)off % BlockSize28;                                   // 0x965854 bl uldivmod
        if (rem != 0) off -= (long)rem;                                         // 0x96590C
        if (method == 1) real = off - (long)GetPosition961870(out _);           // 0x96595C..0x965978
        else if (method == 0) real = off;                                       // 0x965870 strdlo
        else real = off - FileDesc10!.FileSize;                                 // 0x965880..0x965890
        lock (Lock14)                                                           // 0x96589C
        {
            Position38 = (ulong)off;                                            // 0x9658A8
            if (Granted6C >= _nodes.Count)                                      // 0x9658AC cmp r1,r3; bhs 0x965918
            {
                Seek9667C0((ulong)off);                                         // 0x965928 vt+0x7C
                UpdateStatus964B8C();                                           // 0x965934
            }
            else
            {
                var node = _nodes[Granted6C];                                   // 0x9658B4..0x9658D4
                if (node.Buffer!.Start + node.Offset == (ulong)off) UpdateStatus964B8C();   // 0x9658F4 beq 0x965934
                else ReleaseAll965364();                                        // 0x9658F8 bl 0x965364 (which ends with 0x964B8C)
            }
        }
        return 1;                                                               // 0x965904 mov r0,#1
    }

    // ---------------------------------------------------------------- SetHeuristics, SetMinimalBufferSize

    /// <summary>
    /// IAkAutoStream+0x18 SetHeuristics (<c>0x964E4C</c>): a priority above 0x64 returns 0x1F. The priority is stored; the throughput is raised to 1.0f; a file size below the loop end replaces the loop end with the
    /// file size when the file is opened. With an unchanged loop range only the throughput and <c>[S+0x6D]</c> (the minimum buffers, or 1) are stored when they differ (<c>0x9651D8</c>). Otherwise under the lock:
    /// throughput, <c>[S+0x6D]</c>, <c>[S+0x48] = loopStart - loopStart % blockSize</c>, the loop end by <c>vt+0x84</c>; the buffers that are not contiguous with the expected position are unlinked and released
    /// (accounting undone), the position expected after the granted data being the end of the last granted buffer (or the loop start when the loop end is not above it); <c>vt+0x7C</c> repositions at the end of
    /// the contiguous run; <c>0x964B8C</c>; returns 1.
    /// </summary>
    public int SetHeuristics964E4C(WwiseStreamHeuristics h)
    {
        if (h.Priority > 0x64) return 0x1F;                                     // 0x964E4C..0x964E54
        Priority2C = h.Priority;                                                // 0x964E68
        float s16 = h.Throughput;
        float one = BitConverter.Int32BitsToSingle(0x3F800000);
        if (s16 <= one || float.IsNaN(s16)) s16 = one;                          // 0x964E80..0x964E88 (vcmpe; vmovle)
        uint r8 = h.LoopEnd;
        long fileSize = FileDesc10!.FileSize;
        if (fileSize < r8 && (Flags2D & 0x10) != 0) r8 = unchecked((uint)fileSize);   // 0x964E8C..0x964E94; 0x965028..0x965034 (movne r8,r2)
        if (LoopEnd4C == r8 && LoopStart48 == h.LoopStart)                      // 0x964EA4..0x964EB8
        {
            byte min = h.MinNumBuffers == 0 ? (byte)1 : h.MinNumBuffers;       // 0x9651DC..0x9651E8
            bool same = BitConverter.SingleToUInt32Bits(s16) == BitConverter.SingleToUInt32Bits(Throughput44) || s16 == Throughput44;   // 0x9651E4 vcmp.f32 (equal)
            if (!(same && Byte6D == min))                                       // 0x9651F0..0x9651FC
            {
                lock (Lock14)                                                   // 0x965208
                {
                    Byte6D = min;                                               // 0x965210
                    Throughput44 = s16;                                         // 0x965214
                    UpdateStatus964B8C();                                       // 0x965218
                }
            }
            return 1;                                                           // 0x965224
        }
        lock (Lock14)                                                           // 0x964EC8
        {
            Throughput44 = s16;                                                 // 0x964ECC
            uint block = BlockSize28;                                           // 0x964ED0
            Byte6D = h.MinNumBuffers == 0 ? (byte)1 : h.MinNumBuffers;          // 0x964ED4..0x964EE0
            uint alignedStart = h.LoopStart - h.LoopStart % block;              // 0x964EE4..0x964EFC
            LoopStart48 = alignedStart;                                         // 0x964F04
            SetLoopEnd96757C(r8);                                               // 0x964F08 blx vt+0x84
            ulong expected = Position38;                                        // 0x964F10 ldrd r2,r3,[r4,#0x38]
            int granted = Granted6C;
            if (granted != 0)                                                   // 0x964F14..0x964F4C
            {
                int last = Math.Min(granted, _nodes.Count) - 1;
                var lb = _nodes[last].Buffer!;
                expected = lb.Start + lb.Size;                                  // 0x964F4C adds r0,sl,r3 (the last granted buffer's start + size)
            }
            var removed = new List<WwiseStreamNode>();
            int idx = granted;                                                  // the first ungranted node
            if (r8 != 0)                                                        // 0x964F58 cmp r8,#0; beq 0x965134
            {
                ulong loopEnd = r8;
                if (loopEnd <= expected) expected = alignedStart;               // 0x964F6C..0x964F74 bls 0x9651C8 (and 0x9651C8..0x9651D0)
                while (idx < _nodes.Count)                                      // 0x964F78..0x964FD0
                {
                    var node = _nodes[idx];
                    var b = node.Buffer!;
                    if (b.Start + node.Offset != expected)                      // 0x964FD8..0x964FF0 bne 0x964F98
                    {
                        _nodes.RemoveAt(idx);                                   // 0x964F98..0x964FC8 (unlink, count--, push on the removed list)
                        removed.Add(node);
                    }
                    else
                    {
                        expected = b.Start + b.Size;                            // 0x965000..0x965004
                        if (expected >= loopEnd) expected = alignedStart;       // 0x965008..0x965018 (movhs sl,ip; movhs fp,lr)
                        idx++;
                    }
                }
            }
            else
            {
                while (idx < _nodes.Count)                                      // 0x965134..0x9651C0
                {
                    var node = _nodes[idx];
                    var b = node.Buffer!;
                    if (b.Start + node.Offset != expected)                      // 0x965194..0x96519C bne 0x965144
                    {
                        _nodes.RemoveAt(idx);                                   // 0x965144..0x965178
                        removed.Add(node);
                    }
                    else
                    {
                        expected = b.Start + b.Size;                            // 0x9651AC..0x9651B0
                        idx++;
                    }
                }
            }
            if (removed.Count != 0)                                             // 0x96503C cmp r8,#0; beq 0x965100
            {
                lock (Device20.Lock0C)                                          // 0x965058
                {
                    foreach (var node in removed)                               // 0x965074..0x9650F8 (the removed list, newest first)
                    {
                        Buffered54 = unchecked(Buffered54 - Accounted(node));   // 0x9650BC..0x9650CC (with the updated loop end)
                        var buffer = node.Buffer!;
                        node.Buffer = null;                                     // 0x9650D8
                        Device20.Manager.IoSeam.ReleaseBuffer(Device20, buffer);   // 0x9650DC bl 0x96FE70
                    }
                }
            }
            Seek9667C0(expected);                                               // 0x965100..0x965110 vt+0x7C
            UpdateStatus964B8C();                                               // 0x965118
        }
        return 1;                                                               // 0x965124
    }

    /// <summary>
    /// IAkAutoStream+0x1C SetMinimalBufferSize (<c>0x965244</c>): under the lock <c>[S+0x50] = g - g % [S+0x5C]</c>; a size of 0 or at most <c>[S+0x5C]</c> sets <c>[S+0x58] = [S+0x5C]</c>, otherwise
    /// <c>ceil(size / [S+0x5C]) * [S+0x5C]</c>. When <c>[S+0x50]</c> is below the new <c>[S+0x58]</c>: bit 1 of <c>[S+0x6E]</c> is set, the stream stopped, <c>0x964B8C</c>, <c>0x97D100</c> when <c>[S+4] != 0</c>,
    /// and 2. When the old <c>[S+0x58]</c> is at least the new one the result is 1 unchanged; otherwise <c>vt+0x80</c> (<c>0x965F94</c>, unread) and <c>0x964B8C</c>, and 1.
    /// </summary>
    public int SetMinimalBufferSize965244(uint size)
    {
        lock (Lock14)                                                           // 0x96525C
        {
            uint old58 = MinBuffer58;                                           // 0x965250
            uint block = BlockSize5C;                                           // 0x965264
            uint g = Device20.Granularity220;                                   // 0x965268
            BufferSize50 = g - g % block;                                       // 0x965274..0x965284
            uint new58 = block;                                                 // 0x9652F0 str r6,[r4,#0x58]
            if (size != 0 && size > block)                                      // 0x965278..0x965288 (cmpne; bls)
            {
                new58 = (block - 1 + size) / block * block;                     // 0x96528C..0x96529C
                MinBuffer58 = new58;
                if (BufferSize50 < new58)                                       // 0x9652A0 cmp r5,r6; bhs 0x9652F4
                {
                    Flags6E |= 2;                                               // 0x9652B8
                    Stop9655B0();                                               // 0x9652C0 vt+0x5C
                    UpdateStatus964B8C();                                       // 0x9652CC
                    if (Waiter04 != 0) WakeWaiters97D100();                     // 0x9652D0..0x96532C
                    return 2;                                                   // 0x9652DC / 0x965330
                }
            }
            else MinBuffer58 = new58;
            if (old58 >= new58) return 1;                                       // 0x9652F4..0x9652FC
            throw new WwiseMissingBehaviourException(
                "M6-025 D5: SetMinimalBufferSize grows the minimum buffer size past the old one; the body of vt+0x80 (0x965F94, which drops the buffers smaller than the new minimum) is not read");
        }
    }

    // ---------------------------------------------------------------- Query, GetBuffer

    /// <summary>
    /// IAkAutoStream+0x28 QueryBufferingStatus (<c>0x961C1C</c>): bit 1 of <c>[S+0x6E]</c> returns 2. Under the lock a stream that is not opened (bit 4 clear) returns 0x2E <b>without writing</b>
    /// <paramref name="available"/>; otherwise it is set to 0 and then to the running sum of <c>buffer.size - node.offset</c> over the nodes from the first ungranted one (no nodes gives 0x2E, else 0x2D). When
    /// bit 5 is clear and the sum is at least <c>[S+0x54]</c> the result is 0x11. Otherwise the device's <c>vt+0x2C</c> (the seam: <see cref="IWwiseStreamIoSeam.TryCompleteFromCache"/>) runs: non-zero repeats
    /// the computation from the start, zero returns 0x11 when the device byte <c>[dev+0x1C]</c> is set and the result otherwise.
    /// </summary>
    public int Query961C1C(ref uint available)
    {
        Device20.Manager.IoSeam.AtCheckpoint(Device20, this, WwiseIoCheckpoint.Query);   // the I/O thread may have run (C33.4)
        if ((Flags6E & 2) != 0) return 2;                                       // 0x961C20..0x961C34
        lock (Lock14)                                                           // 0x961C4C
        {
            if ((Flags2D & 0x10) == 0) return 0x2E;                             // 0x961C54..0x961C58: *out is not written
            while (true)
            {
                available = 0;                                                  // 0x961C70 str r4,[r6]
                int result;
                uint sum = 0;
                if (Granted6C >= _nodes.Count) result = 0x2E;                   // 0x961C98 cmp r3,#0; beq 0x961D18
                else
                {
                    for (int i = Granted6C; i < _nodes.Count; i++)              // 0x961CA4..0x961CC0
                    {
                        var n = _nodes[i];
                        sum = unchecked(sum + (n.Buffer!.Size - n.Offset));
                        available = sum;                                        // 0x961CBC str ip,[r6]
                    }
                    result = 0x2D;                                              // 0x961CC4
                }
                if ((Flags2D & 0x20) == 0 && sum >= Buffered54) return 0x11;    // 0x961CC8..0x961CE0 (unsigned; blo skips)
                if (Device20.Manager.IoSeam.TryCompleteFromCache(Device20, this)) continue;   // 0x961CE8..0x961D00 vt+0x2C; bne 0x961C70
                if (Device20.Flag1C) return 0x11;                               // 0x961D04..0x961D10
                return result;
            }
        }
    }

    /// <summary>
    /// <c>0x9656AC(S, &amp;size)</c>: the data pointer of the next ungranted buffer, or null. With every buffer granted it returns null with the size 0. A node whose <c>start + offset</c> is not the position
    /// <c>[S+0x38]</c> is stale: <c>vt+0x84(0)</c> (the loop end is cleared and <c>[S+0x54]</c> recomputed), the ungranted buffers are released and null is returned. Otherwise the node is granted
    /// (<c>[S+0x6C]++</c>), <c>[S+0x38]</c> becomes the end of its buffer, the size is <c>buffer.size - offset</c>, its accounted bytes leave <c>[S+0x54]</c> and <c>0x964B8C</c> runs.
    /// </summary>
    internal (byte[]? Data, int Index) TakeNext9656AC(out uint size)
    {
        size = 0;
        if (Granted6C >= _nodes.Count) return (null, 0);                        // 0x9656B8 cmp r2,r3; bhs 0x9657A4
        var node = _nodes[Granted6C];                                           // 0x9656C4..0x9656DC
        var buf = node.Buffer!;
        if (Position38 != buf.Start + node.Offset)                              // 0x9656F0..0x965708
        {
            SetLoopEnd96757C(0);                                                // 0x9657B4..0x9657C4 vt+0x84(0)
            ReleaseAll965364();                                                 // 0x9657C8
            return (null, 0);                                                   // 0x9657D0..0x9657D8
        }
        Granted6C++;                                                            // 0x96570C..0x965710
        Position38 = buf.Start + buf.Size;                                      // 0x965724..0x965730
        size = buf.Size - node.Offset;                                          // 0x965728, 0x965734
        Buffered54 = unchecked(Buffered54 - Accounted(node));                   // 0x965738..0x96576C
        UpdateStatus964B8C();                                                   // 0x965770
        return (buf.Data, checked((int)node.Offset));                           // 0x965774..0x965784: buffer data + offset
    }

    /// <summary>
    /// IAkAutoStream+0x40 GetBuffer (<c>0x965B1C(S, &amp;buf, &amp;size, bWait)</c>). The outputs are zeroed; under the lock the next node is taken as <see cref="TakeNext9656AC"/> does. An error flag (bit 1 of <c>[S+0x6E]</c>)
    /// returns 2. A buffer returns 0x2D, except bit 2 of <c>[S+0x2D]</c> set with <c>[S+0x38]</c> at or past the file size, which returns 0x11. With no buffer: when the stream is opened (bit 4) the fetch is retried
    /// (<c>0x9656AC</c>), then the device's <c>vt+0x2C</c> (the seam), then the fetch again when that returned non-zero; with <paramref name="wait"/> false and still no buffer the result is 0x2E when bit 2 is clear,
    /// 0x2E when bit 2 is set and <c>[S+0x38] &lt; fileSize</c>, else 0x11. The waiting path (<paramref name="wait"/> true) is never used by Anki's sources (0xA749D0, 0xAB20AC, 0xA74CE0 pass 0) and is not built.
    /// </summary>
    public int GetBuffer965B1C(out byte[]? data, out int offset, out uint size, bool wait)
    {
        if (wait) throw new WwiseMissingBehaviourException("M6-025 E6: GetBuffer's waiting path (0x965CE4..0x965D20, 0x97CB9C, 0x97D0B4) is never used by Anki's sources (bWait is always 0) and is not built");
        Device20.Manager.IoSeam.AtCheckpoint(Device20, this, WwiseIoCheckpoint.GetBuffer);   // the I/O thread may have run (C33.4)
        data = null; offset = 0; size = 0;                                      // 0x965B30, 0x965B38
        byte[]? got;
        int gotOffset = 0;
        lock (Lock14)                                                           // 0x965B48
        {
            (got, gotOffset) = TakeNext9656AC(out size);                        // 0x965B4C..0x965C30 (the inlined take; the first test is `granted >= count`)
            if (got is null && (Flags6E & 2) == 0)                              // 0x965CC0..0x965CC8
            {
                if ((Flags2D & 0x10) != 0)                                      // 0x965CCC..0x965CD0
                {
                    (got, gotOffset) = TakeNext9656AC(out size);                // 0x965D9C bl 0x9656AC
                    if (got is null)                                            // 0x965DA8 cmp r0,#0; bne 0x965C34
                    {
                        if (Device20.Manager.IoSeam.TryCompleteFromCache(Device20, this))   // 0x965DB4..0x965DC8 vt+0x2C
                            (got, gotOffset) = TakeNext9656AC(out size);        // 0x965DE0..0x965DEC bl 0x9656AC
                    }
                }
            }
        }
        if ((Flags6E & 2) != 0) return 2;                                       // 0x965C3C..0x965C44
        if (got is not null)                                                    // 0x965C48..0x965C54
        {
            data = got; offset = gotOffset;
            if ((Flags2D & 4) == 0) return 0x2D;                                // 0x965C58..0x965C5C
            return Position38 < (ulong)FileDesc10!.FileSize ? 0x2D : 0x11;      // 0x965C64..0x965C7C (movlo 0x2d; movhs 0x11)
        }
        if ((Flags2D & 4) == 0) return 0x2E;                                    // 0x965D70..0x965D74
        return Position38 < (ulong)FileDesc10!.FileSize ? 0x2E : 0x11;          // 0x965D7C..0x965D98
    }

    // ---------------------------------------------------------------- the I/O thread's side of a transfer: E5

    /// <summary>
    /// E5 <c>0x966644(S, xfer, result, flag)</c> (task vt+0x18): under the lock a non-null transfer with result 1, a transfer in flight, and a state other than 2 is appended (state 3 when <paramref name="flag"/> is
    /// false) through <c>0x964914</c>; every other transfer is released through it (flag 0); then <c>[S+0x70] = 0</c> and <c>[S+0x74] = 0</c> on both the append and the release path (the append path joins the release path at 0x966678; 0x966688..0x966690). <c>0x964A20(S, result)</c>
    /// runs last. Returns non-zero when a transfer was given (the transfer record, or 1 after a release).
    /// </summary>
    internal bool CompleteTransfer966644(WwiseStreamNode? xfer, int result, bool flag)
    {
        lock (Lock14)                                                           // 0x966660
        {
            if (xfer is not null)                                               // 0x966664 cmp r5,#0; beq 0x966694
            {
                bool append = result == 1 && InFlight70 is not null && (xfer.State & 7) != 2;   // 0x96666C..0x9666C8
                if (append)
                {
                    if (!flag) xfer.State = (byte)((xfer.State & ~7) | 3);      // 0x9666CC..0x9666E0
                    AppendOrRelease964914(xfer, flag: true);                    // 0x966678 with r2 = result (1): the append
                }
                else
                {
                    AppendOrRelease964914(xfer, flag: false);                   // 0x966674..0x966684 mov r2,#0; bl 0x964914
                }
                InFlight70 = null;                                              // 0x96668C str r3,[r4,#0x70]
                Byte74 = 0;                                                     // 0x966690 strb r3,[r4,#0x74]
            }
            CompleteStatus964A20(result);                                       // 0x966698 bl 0x964A20
            return xfer is not null;                                            // 0x9666A8 mov r0,r5 (r5 = xfer, or 1 after 0x966680)
        }
    }

    /// <summary>
    /// <c>0x964A20(S, result)</c>: result 2 sets bit 1 of <c>[S+0x6E]</c> and stops the stream (<c>vt+0x5C</c>); then the status update of <see cref="UpdateStatus964B8C"/>, ending with the waiters woken
    /// (<c>0x97D100</c> when <c>[S+4] != 0</c>).
    /// </summary>
    internal void CompleteStatus964A20(int result)
    {
        if (result == 2)                                                        // 0x964A20 cmp r1,#2; beq 0x964B70
        {
            Flags6E |= 2;                                                       // 0x964B7C
            Stop9655B0();                                                       // 0x964B80 vt+0x5C
        }
        UpdateStatus964B8C(wake: true);                                         // 0x964A30..0x964A94
    }

    // ---------------------------------------------------------------- the open record's resolver call and the destructor

    /// <summary>
    /// The destructor (<c>0x96449C</c>, vt+0): bit 5 starved calls <c>0x97CFF4</c>; an opened stream (bit 4) closes its file through the hook (<c>[dev+0x21C]</c> vt+8); a pending record is freed; the descriptor is
    /// freed; the mutex destroyed.
    /// </summary>
    internal void Destruct()
    {
        if ((Flags2D & 0x20) != 0) Device20.StarvedDown97CFF4();                // 0x9644A4..0x9644CC
        if ((Flags2D & 0x10) != 0 && FileDesc10 is not null)                    // 0x9644D8..0x9644E8; 0x96455C..0x964570
            Device20.Hook.Close(FileDesc10);
        Record0C = null;                                                        // 0x9644EC..0x964500
        FileDesc10 = null;                                                      // 0x964520..0x964538 (freed)
    }
}

/// <summary>The open record of a deferred open (<c>0x960B04</c> by id, <c>0x960A08</c> by name; 0x28 bytes).</summary>
public sealed class WwiseOpenRecord
{
    /// <summary><c>+0</c>: the file id (by id) or 0.</summary>
    public uint FileId { get; }

    /// <summary>The copied name (by name), or null (by id, bit 0 of <c>[+0x24]</c>).</summary>
    public string? Name { get; }

    /// <summary><c>+4..+0x1F</c>: the copy of the flags, present when bit 1 of <c>[+0x24]</c> is set.</summary>
    public WwiseFileFlags? Flags { get; }

    /// <summary><c>+0x20</c>: the open mode.</summary>
    public uint Mode { get; }

    internal WwiseOpenRecord(uint fileId, string? name, WwiseFileFlags? flags, uint mode)
    {
        FileId = fileId; Name = name; Flags = flags; Mode = mode;
    }

    /// <summary>
    /// <c>0x960BDC(record, fileDesc)</c>: <c>bSyncOpen = 1</c>; by name the resolver's <c>vt+8</c>, by id <c>vt+0xC</c>, with the copied flags (null when bit 1 is clear) and the open mode; a result other than 1 returns
    /// 2. An <c>iFileSize &lt; 1</c> (signed 64 bit) with open mode 0 returns 2; then the local <c>bSyncOpen</c> must be non-zero (the resolver set it) else 2; success is 1.
    /// </summary>
    internal int Open960BDC(IWwiseFileLocationResolver resolver, WwiseFileDesc fileDesc)
    {
        bool bSync = true;                                                      // 0x960BE8 strb r3,[sp,#0xf]
        int r = Name is not null
            ? resolver.OpenByName(Name, Mode, Flags, ref bSync, fileDesc)       // 0x960C30 vt+8
            : resolver.OpenById(FileId, Mode, Flags, ref bSync, fileDesc);      // 0x960C74 vt+0xC
        if (r != 1) return 2;                                                   // 0x960C38..0x960C40 / 0x960C78..0x960C7C
        if (fileDesc.FileSize < 1 && Mode == 0) return 2;                       // 0x960C80..0x960CB0
        return bSync ? 1 : 2;                                                   // 0x960C90..0x960C9C
    }
}
