// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// THE ONE HOST SEAM of the streaming layer, reference implementation (C33.4, recorded by the operator as EQUIVALENT_IMPLEMENTATION; the manifest record is added by the manager when it is built).
///
/// <para>What it stands for. In the engine the device thread (<c>0x97CA90</c>) runs <c>PerformIO</c> (<c>0x966A7C</c>) concurrently with the audio thread: it chooses a task (<c>0x963104</c>), opens the
/// deferred file (<c>0x963890</c>), prepares a transfer (<c>0x966BCC</c>, remainder unread), reads through the hook, and completes the transfer (<c>0x966644</c>). The audio thread sees only the result,
/// at its own check points (GetBuffer, QueryBufferingStatus). When the thread has run is the one OS-dependent fact (scheduling, I/O latency, the stream cache). This class decides it as follows, and
/// nothing else:</para>
/// <list type="bullet">
/// <item><see cref="Step"/> is "the I/O thread ran": every device is serviced once. The host calls it between voice passes (one per audio frame is the natural cadence); with
/// <see cref="Immediate"/> set it also runs at every check point, which is the extreme of an I/O thread that is never late.</item>
/// <item>One service of a stream runs the engine's own steps in the engine's order: flagged streams are destroyed when <c>vt+8</c> allows it; a ready stream (bit 7) opens its deferred file
/// (<c>0x963890</c>, exact: a failure completes the transfer with 2); <see cref="PrepareTransfer"/> plans a read; the hook reads; <c>0x966644</c> completes it (exact). It repeats until no transfer is planned.</item>
/// <item><b>The transfer plan</b> (the engine's <c>0x966BCC</c>; its remainder after <c>0x966F44</c> is unread). From the engine, exactly: the next position is the stream's own <c>vt+0x74</c>; a non-zero loop end
/// <c>[S+0x4C]</c> at or below it wraps the position to the loop start <c>[S+0x48]</c> (<c>0x966C4C..0x966C74</c>; there is NO stop at the loop end); the size is <c>min([S+0x50], fileSize - position)</c>
/// (<c>0x966C7C</c>, <c>0x966D38..0x966D4C</c>); the accounting <c>[S+0x54] += accounted bytes</c>, the in-flight record <c>[S+0x70]</c>, the status update <c>0x964B8C</c> and <c>[S+0x18] = [dev+0x40]</c>
/// follow (<c>0x966E30..0x966E88</c>). The seam's own choices (the engine's code for them is unread): a stream whose buffered amount <c>[S+0x54]</c> has reached its nominal buffering (<c>vt+0x24</c>; the
/// starved bit of <c>0x964B8C</c>), whose next position is at or past the file end, or which is not opened, is not read.</item>
/// <item>Locks as the engine takes them: the stream's <c>[S+0x14]</c> lock around the whole preparation (<c>0x966C00</c>, released at <c>0x966C1C</c>) and the device's <c>[dev+0xC]</c> lock around the buffer
/// allocation (<c>0x966D50..0x966E2C</c>), in that order; <c>0x963890</c> and <c>0x961A6C</c> take none. <b>Thread contract:</b> <see cref="Step"/> (and, with <see cref="Immediate"/>, the check points) is driven
/// by ONE thread at a time (the host's I/O thread, or the audio thread when Immediate); a second thread entering while another is servicing throws <see cref="InvalidOperationException"/>. The engine runs exactly
/// one I/O thread per device (<c>0x97CA90</c>).</item>
/// <item>The stream cache (<c>bUseStreamCache = 1</c>) is not modelled: <see cref="TryCompleteFromCache"/> returns false, as for a block that no earlier play left behind. <b>It cannot express what the engine's
/// cache fetch (<c>0x961800</c>) does</b>: a hit always finishes with <c>0x966644(S, xfer-or-NULL, 1, 0)</c> and so <c>0x964A20</c> (<c>0x961838</c>, <c>0x96184C</c>).</item>
/// <item>The stale word <c>QueryBufferingStatus</c> leaves in its caller's out variable for an unopened stream is <see cref="StaleQueryWord"/>, a constructor argument.</item>
/// </list>
/// <para>MISSING (the defaults here are NOT engine facts): <see cref="InitIoMemoryResult"/> (0x969E8C unread; 1 assumed), <see cref="StartThreadResult"/> (0x97CC80 unread; 1 assumed), the
/// <c>AllocationFails</c> default (null = success; the allocator 0x9713B4/0x96FE70 is unread), the plan's stop rules above, and the cache (0x961800, 0x9656AC callers). The
/// configuration without a JNIEnv (0x592FFE..0x59303C: the zip is <c>soundDir + "AudioAssets.zip"</c> when it exists, else the 'Audio Assets not found' error) is a host input of
/// <c>WwiseAnkiResolverConfig</c>, not built.</para>
/// </summary>
public sealed class WwiseSteppedStreamIo : IWwiseStreamIoSeam
{
    private readonly List<WwiseStreamDevice> _devices = new();
    private long _clock;

    /// <summary>When true the I/O thread "runs" at every check point of the audio thread (<see cref="WwiseIoCheckpoint"/>) as well as in <see cref="Step"/>.</summary>
    public bool Immediate { get; set; }

    /// <summary>The result <c>0x969E8C</c> reports (1 for the I/O memory manager's success; the body is unread).</summary>
    public int InitIoMemoryResult { get; set; } = 1;

    /// <summary>The result <c>0x97CC80</c> reports (1: the thread started).</summary>
    public int StartThreadResult { get; set; } = 1;

    /// <inheritdoc />
    public uint StaleQueryWord { get; }

    /// <summary>The number of transfers read so far (for tests and diagnostics).</summary>
    public int TransfersRead { get; private set; }

    /// <summary>Creates the seam; <paramref name="staleQueryWord"/> is the uninitialised stack word of <c>0x961C70</c> (no engine value exists; the caller chooses it).</summary>
    public WwiseSteppedStreamIo(uint staleQueryWord) => StaleQueryWord = staleQueryWord;

    /// <inheritdoc />
    public int InitIoMemory(WwiseStreamDevice device, WwiseDeviceSettings settings)
    {
        if (InitIoMemoryResult == 1 && !_devices.Contains(device)) _devices.Add(device);
        return InitIoMemoryResult;
    }

    /// <inheritdoc />
    public int StartIoThread(WwiseStreamDevice device, WwiseDeviceSettings settings) => StartThreadResult;

    /// <inheritdoc />
    public void DestroyDevice(WwiseStreamDevice device) => _devices.Remove(device);

    /// <inheritdoc />
    public void AtCheckpoint(WwiseStreamDevice device, WwiseAutoStream stream, WwiseIoCheckpoint where)
    {
        if (Immediate) Service(device);
    }

    /// <inheritdoc />
    public bool TryCompleteFromCache(WwiseStreamDevice device, WwiseAutoStream stream) => false;

    /// <inheritdoc />
    public void ReleaseBuffer(WwiseStreamDevice device, WwiseStreamBuffer buffer) { }

    /// <summary>The I/O thread ran: every device is serviced once.</summary>
    public void Step()
    {
        foreach (var d in _devices.ToArray()) Service(d);
    }

    private int _servicingThread;   // the managed id of the thread inside Service (0 = none): one servicing thread at a time
    internal Action? ServiceEntered { get; set; }

    private void Service(WwiseStreamDevice device)
    {
        int me = Environment.CurrentManagedThreadId;
        int owner = Interlocked.CompareExchange(ref _servicingThread, me, 0);
        if (owner != 0 && owner != me)
            throw new InvalidOperationException("M6-025: the stream I/O seam is driven by one thread at a time (the engine has one I/O thread per device, 0x97CA90)");
        try { ServiceEntered?.Invoke(); ServiceCore(device); }
        finally { if (owner == 0) Interlocked.Exchange(ref _servicingThread, 0); }
    }

    private void ServiceCore(WwiseStreamDevice device)
    {
        device.Time40 = ++_clock;                                              // 0x96312C: the device time stamp (the process clock is a counter here)
        foreach (var stream in device.Streams.ToArray())                       // the list is head first (newest first)
        {
            if ((stream.Flags2D & 8) != 0)                                     // 0x963104: flagged streams are destroyed when vt+8 says so
            {
                if (stream.TryDestroy965F5C()) { device.RemoveStream(stream); stream.Destruct(); }
                continue;
            }
            if ((stream.Flags2D & 0x80) == 0) continue;                        // only ready streams are served (E3)
            PerformIo(device, stream);
        }
    }

    /// <summary>E2 <c>0x966A7C</c> for one task, repeated while a transfer is planned.</summary>
    private void PerformIo(WwiseStreamDevice device, WwiseAutoStream stream)
    {
        if (stream.RunOpen963890() != 1)                                       // E2: 0x963890(task) != 1
        {
            stream.CompleteTransfer966644(null, 2, false);                     // E2: vt+0x18(task, 0, 2, 0)
            return;
        }
        while (true)
        {
            var plan = PrepareTransfer(device, stream, bSync: false);                  // vt+0x14 (the seam's replacement of 0x966BCC)
            if (plan is null)
            {
                stream.CompleteTransfer966644(null, 0x35, false);              // E2: vt+0x18(task, 0, 0x35, 0)
                return;
            }
            int result = plan.Info is { } info ? device.Hook.Read(stream.FileDesc10!, info) : 1;   // E2 (0x966A7C..0x966BC8): hook vt+0x18 (Read)
            TransfersRead++;
            stream.CompleteTransfer966644(plan.Node, result, plan.Info is not null);   // E2: task vt+0x18(task, xfer, result, info != 0)
            if (result != 1) return;
        }
    }

    /// <summary>
    /// The transfer plan (the seam's choice; see the class remarks). Returns null when the stream is flagged for destroy, not ready, already has a transfer in flight, has no further data to read, or has buffered its
    /// nominal amount. The record is the stream's transfer in flight and its accounted bytes are added to <c>[S+0x54]</c>.
    /// </summary>
    internal WwiseTransferPlan? PrepareTransfer(WwiseStreamDevice device, WwiseAutoStream stream, bool bSync)
    {
        lock (stream.Lock14)                                                   // 0x966C00 (released at 0x966C1C on every path)
        {
            if ((stream.Flags2D & 8) != 0 || (stream.Flags2D & 0x80) == 0 || stream.InFlight70 is not null) return null;   // 0x966C04..0x966C38: bit 3, bit 7 clear, [S+0x70] != 0 return 0
            if (bSync) return null;                                            // a cached block only: the cache is not modelled
            if ((stream.Flags2D & 0x10) == 0 || stream.FileDesc10 is null) return null;
            if (stream.Buffered54 >= stream.GetNominalBuffering961AD8()) return null;   // the seam's choice (the starved bit of 0x964B8C)
            ulong position = stream.NextPosition9666E8();                      // 0x966C3C..0x966C48 vt+0x74
            if (stream.LoopEnd4C != 0 && position >= stream.LoopEnd4C)         // 0x966C4C..0x966C74: [S+0x4C] != 0 and position >= [S+0x4C] (unsigned 64-bit)
                position = stream.LoopStart48;                                 // movhs: the position becomes the loop start [S+0x48]; the read continues
            ulong fileSize = (ulong)stream.FileDesc10.FileSize;
            if (position >= fileSize) return null;                             // 0x966C98..0x966CB8 (not cached: return 0)
            uint size = (uint)Math.Min((ulong)stream.BufferSize50, fileSize - position);   // 0x966C7C, 0x966D38..0x966D4C
            if (size == 0) return null;                                        // 0x966D00..0x966D08
            WwiseStreamBuffer buffer;
            lock (device.Lock0C)                                               // 0x966D50..0x966E2C (the allocation; the memory manager 0x9713B4 is unread)
            {
                buffer = new WwiseStreamBuffer { Start = position, Data = new byte[size], Size = size, CacheId = -1 };
            }
            var node = new WwiseStreamNode { Buffer = buffer, Offset = 0, State = 1 };
            stream.InFlight70 = node;                                          // 0x966E40
            stream.Buffered54 = unchecked(stream.Buffered54 + stream.Accounted(node));   // 0x966E30..0x966E74
            stream.UpdateStatus964B8C();                                       // 0x966E78
            stream.Time18 = device.Time40;                                     // 0x966E7C..0x966E84 ([S+0x18] = [dev+0x40])
            return new WwiseTransferPlan
            {
                Node = node,
                Info = new WwiseTransferInfo { FilePosition = position, BufferSize = size, RequestedSize = size, Buffer = buffer.Data },
            };
        }
    }
}
