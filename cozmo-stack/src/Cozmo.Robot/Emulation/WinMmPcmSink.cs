using System.Runtime.InteropServices;

namespace Cozmo.Robot.Emulation;

/// <summary>The platform output boundary. Only real completed buffers produce tokens.</summary>
internal interface IPlatformPcmSink : IDisposable
{
    WaitHandle CompletionReady { get; }
    void Submit(ulong token, byte[] pcm);
    IReadOnlyList<ulong> DrainCompleted();
}

/// <summary>48 kHz, stereo, signed 16-bit output for the native OpenSL bridge.</summary>
internal sealed class WinMmPcmSink : IPlatformPcmSink
{
    private const uint CallbackEvent = 0x00050000;
    private const uint HeaderDone = 1;
    private const int Capacity = 4;
    private static readonly List<AutoResetEvent> RetainedEvents = new();
    private readonly AutoResetEvent _event = new(false);
    private readonly Queue<Buffer> _buffers = new();
    private nint _device;
    private bool _disposed;

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public ushort FormatTag, Channels;
        public uint SamplesPerSecond, AverageBytesPerSecond;
        public ushort BlockAlign, BitsPerSample, ExtraSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public nint Data;
        public uint BufferLength, BytesRecorded;
        public nuint User;
        public uint Flags, Loops;
        public nint Next;
        public nuint Reserved;
    }

    private sealed record Buffer(ulong Token, nint Header, nint Data);

    [DllImport("winmm.dll")] private static extern uint waveOutOpen(out nint handle, uint device, ref WaveFormat format, nint callback, nuint instance, uint flags);
    [DllImport("winmm.dll")] private static extern uint waveOutPrepareHeader(nint handle, nint header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutWrite(nint handle, nint header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(nint handle, nint header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutReset(nint handle);
    [DllImport("winmm.dll")] private static extern uint waveOutClose(nint handle);

    internal WinMmPcmSink()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Native Wwise playback requires a Windows audio device");
        var format = new WaveFormat { FormatTag = 1, Channels = 2, SamplesPerSecond = 48000,
            AverageBytesPerSecond = 192000, BlockAlign = 4, BitsPerSample = 16 };
        uint result = waveOutOpen(out _device, uint.MaxValue, ref format, _event.SafeWaitHandle.DangerousGetHandle(), 0, CallbackEvent);
        if (result != 0)
        {
            _event.Dispose();
            throw new InvalidOperationException($"Windows audio device open failed ({result})");
        }
    }

    public WaitHandle CompletionReady => _event;

    public void Submit(ulong token, byte[] pcm)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(pcm);
        if (pcm.Length != 4096) throw new ArgumentException("Expected 4096 bytes of stereo PCM", nameof(pcm));
        if (_buffers.Count >= Capacity) throw new InvalidOperationException("Windows audio queue is full");
        nint data = 0;
        nint header = 0;
        bool prepared = false;
        try
        {
            data = Marshal.AllocHGlobal(pcm.Length);
            header = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHeader>());
            Marshal.Copy(pcm, 0, data, pcm.Length);
            Marshal.StructureToPtr(new WaveHeader { Data = data, BufferLength = (uint)pcm.Length }, header, false);
            Check(waveOutPrepareHeader(_device, header, (uint)Marshal.SizeOf<WaveHeader>()), "prepare");
            prepared = true;
            _buffers.Enqueue(new Buffer(token, header, data));
            Check(waveOutWrite(_device, header, (uint)Marshal.SizeOf<WaveHeader>()), "write");
        }
        catch
        {
            // A prepared header stays owned until Dispose resets and unprepares it, even if write failed.
            if (!prepared)
            {
                Marshal.FreeHGlobal(header);
                Marshal.FreeHGlobal(data);
            }
            throw;
        }
    }

    public IReadOnlyList<ulong> DrainCompleted()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var done = new List<ulong>();
        while (_buffers.TryPeek(out var buffer) && (Marshal.PtrToStructure<WaveHeader>(buffer.Header).Flags & HeaderDone) != 0)
        {
            Check(waveOutUnprepareHeader(_device, buffer.Header, (uint)Marshal.SizeOf<WaveHeader>()), "unprepare");
            _buffers.Dequeue();
            Marshal.FreeHGlobal(buffer.Header);
            Marshal.FreeHGlobal(buffer.Data);
            done.Add(buffer.Token);
        }
        return done;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        uint reset = waveOutReset(_device);
        uint failure = reset;
        while (_buffers.TryDequeue(out var buffer))
        {
            uint result = waveOutUnprepareHeader(_device, buffer.Header, (uint)Marshal.SizeOf<WaveHeader>());
            if (failure == 0) failure = result;
            // If unprepare fails the driver may still retain these addresses. Keep them alive.
            if (result == 0) { Marshal.FreeHGlobal(buffer.Header); Marshal.FreeHGlobal(buffer.Data); }
        }
        uint close = waveOutClose(_device);
        if (failure == 0) failure = close;
        if (close == 0)
        {
            _device = 0;
            _event.Dispose();
        }
        else
        {
            // A driver that refuses close may still signal its callback handle.
            lock (RetainedEvents) RetainedEvents.Add(_event);
        }
        Check(failure, "reset or close");
    }

    private static void Check(uint result, string operation)
    {
        if (result != 0) throw new InvalidOperationException($"Windows audio {operation} failed ({result})");
    }
}
