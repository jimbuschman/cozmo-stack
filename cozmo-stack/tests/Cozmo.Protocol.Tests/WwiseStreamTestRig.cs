using Cozmo.Robot.Animation.Wwise;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The stand-ins of the oracle (re-analysis/tools/emu/emu_stream.py): a resolver whose vt+8 / vt+0xC fill the file descriptor (a deferred open returns 1 with a zero descriptor, as the Anki resolver does;
/// a synchronous one reports the file size), and a low-level hook whose Read fills the buffer from a known content. They are the host's side of the I/O seam, not engine behaviour.
/// </summary>
internal sealed class StubResolver : IWwiseFileLocationResolver, IWwiseLowLevelIoHook
{
    public long FileSize = 1000;
    public bool Defer = true;
    public int DeviceId;
    public byte[]? Content;
    public readonly List<string> Opens = new();

    public static byte Pattern(ulong pos) => (byte)((pos * 7 + 3) & 0xFF);

    public byte[] BytesAt(ulong pos, int size)
    {
        var b = new byte[size];
        for (int i = 0; i < size; i++)
            b[i] = Content is null ? Pattern(pos + (ulong)i) : (pos + (ulong)i < (ulong)Content.Length ? Content[pos + (ulong)i] : (byte)0);
        return b;
    }

    public int OpenById(uint id, uint mode, WwiseFileFlags? flags, ref bool bSyncOpen, WwiseFileDesc fileDesc) => Open("id:" + id, ref bSyncOpen, fileDesc);

    public int OpenByName(string name, uint mode, WwiseFileFlags? flags, ref bool bSyncOpen, WwiseFileDesc fileDesc) => Open("name:" + name, ref bSyncOpen, fileDesc);

    private int Open(string what, ref bool bSyncOpen, WwiseFileDesc fd)
    {
        fd.Clear();
        fd.DeviceId = DeviceId;
        if (!bSyncOpen && Defer) return 1;
        bSyncOpen = true;
        fd.FileSize = FileSize;
        Opens.Add(what);
        return 1;
    }

    public uint GetBlockSize(WwiseFileDesc fileDesc) => 1;

    public int Read(WwiseFileDesc fileDesc, WwiseTransferInfo transfer)
    {
        var bytes = BytesAt(transfer.FilePosition, (int)transfer.RequestedSize);
        Array.Copy(bytes, transfer.Buffer, bytes.Length);
        return 1;
    }

    public int Close(WwiseFileDesc fileDesc) => 1;
}

/// <summary>The I/O seam with no thread: nothing happens unless a test completes a transfer itself (the oracle's "I/O thread has not run").</summary>
internal sealed class ManualIo : IWwiseStreamIoSeam
{
    public uint StaleQueryWord { get; init; } = 0xCAFEF00D;
    public int InitIoMemory(WwiseStreamDevice device, WwiseDeviceSettings settings) => 1;
    public int StartIoThread(WwiseStreamDevice device, WwiseDeviceSettings settings) => 1;
    public void DestroyDevice(WwiseStreamDevice device) { }
    public void AtCheckpoint(WwiseStreamDevice device, WwiseAutoStream stream, WwiseIoCheckpoint where) { }
    public bool TryCompleteFromCache(WwiseStreamDevice device, WwiseAutoStream stream) => false;
    public void ReleaseBuffer(WwiseStreamDevice device, WwiseStreamBuffer buffer) { }
}

internal sealed class StreamRig
{
    public readonly WwiseStreamManager Manager;
    public readonly StubResolver Resolver;
    public readonly WwiseStreamDevice Device;

    public StreamRig(long fileSize = 1000, bool defer = true, IWwiseStreamIoSeam? io = null, byte[]? content = null)
    {
        Manager = new WwiseStreamManager(io ?? new ManualIo());
        Resolver = new StubResolver { FileSize = fileSize, Defer = defer, Content = content };
        Manager.Resolver = Resolver;
        int slot = Manager.CreateDevice(WwiseDeviceSettings.Anki(), Resolver);
        if (slot != 0) throw new InvalidOperationException("the device was not created");
        Device = Manager.Device(slot)!;
    }

    public static WwiseStreamHeuristics Heur(uint throughputBits, uint loopStart = 0, uint loopEnd = 0, byte minBuffers = 0, byte priority = 0) => new()
    {
        Throughput = BitConverter.Int32BitsToSingle(unchecked((int)throughputBits)), LoopStart = loopStart, LoopEnd = loopEnd, MinNumBuffers = minBuffers, Priority = priority,
    };

    public WwiseAutoStream Create(uint id = 5, WwiseStreamHeuristics? heur = null, WwiseStreamBufferSettings? bs = null, WwiseFileFlags? flags = null)
    {
        WwiseAutoStream? s = null;
        int r = Manager.CreateAutoById(id, flags ?? new WwiseFileFlags { CodecId = 1, CacheId = 0xFFFFFFFF }, heur ?? Heur(0x3F800000), bs, ref s, false);
        if (r != 1 || s is null) throw new InvalidOperationException("CreateAuto returned " + r);
        return s;
    }

    /// <summary>The oracle's `deliver`: a transfer record goes in flight, its accounted bytes are added to [S+0x54], and the engine's completion 0x966644 appends it.</summary>
    public bool Deliver(WwiseAutoStream s, ulong start, uint size)
    {
        var buffer = new WwiseStreamBuffer { Start = start, Data = Resolver.BytesAt(start, (int)size), Size = size, CacheId = -1 };
        var node = new WwiseStreamNode { Buffer = buffer, Offset = 0, State = 1 };
        uint loopEnd = s.LoopEnd4C;
        uint accounted = start >= loopEnd ? size : (start + size > loopEnd ? (uint)(loopEnd - start) : size);   // the oracle's python formula, not Accounted()
        s.Buffered54 = unchecked(s.Buffered54 + accounted);
        s.InFlight70 = node;
        return s.CompleteTransfer966644(node, 1, true);
    }

    public (int Result, uint Out) Query(WwiseAutoStream s, uint stale = 0xCAFEF00D)
    {
        uint avail = stale;
        int r = s.Query961C1C(ref avail);
        return (r, avail);
    }

    public int GetBuffer(WwiseAutoStream s, out byte[]? data, out int offset, out uint size) => s.GetBuffer965B1C(out data, out offset, out size, wait: false);
}
