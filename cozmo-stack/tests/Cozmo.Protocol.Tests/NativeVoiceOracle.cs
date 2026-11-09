using System.Buffers.Binary;

namespace Cozmo.Robot.Emulation;

/// <summary>Bounded, serialized native Wwise oracle. It is not a real-time output device.</summary>
internal sealed class NativeVoiceOracle : IDisposable
{
    private readonly ArmMachine _m;
    private readonly uint _base;
    private readonly Dictionary<uint, int> _semaphores = new();
    private readonly Dictionary<uint, (uint Entry, uint Owner)> _workers = new();
    private uint _nextWorker;
    private uint _clock;
    private readonly List<float[]> _frames = new();
    private readonly uint _interface;
    private bool _posted;
    public (uint Cookie, uint GameObject, uint PlayingId, uint EventId)? EndOfEvent { get; private set; }
    private readonly int _thread = Environment.CurrentManagedThreadId;

    private void RequireOwner()
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("native voice oracle must remain on its owner thread");
    }

    /// <summary>Releases the isolated oracle machine; native Wwise teardown remains a named gap.</summary>
    public void Dispose() { RequireOwner(); _m.Dispose(); }

    public IReadOnlyList<float[]> Frames => _frames;

    /// <summary>Injects a mono provider buffer and posts the authored Sentence event.</summary>
    public uint Post(float[] samples, int sampleRate)
    {
        RequireOwner();
        ArgumentNullException.ThrowIfNull(samples);
        if (_posted) throw new InvalidOperationException("native voice oracle is one-shot");
        _posted = true;
        if (sampleRate <= 0 || samples.Length == 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        uint byteCount = checked((uint)samples.Length * 4);
        uint audio = _m.Malloc(byteCount), data = _m.Malloc(16);
        if (audio == 0 || data == 0) throw new EmulationAbort("WavePortal allocation failed");
        Span<byte> bytes = _m.Span(audio, byteCount);
        for (int i = 0; i < samples.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(i * 4, 4), F(samples[i]));
        _m.W32(data, (uint)sampleRate); _m.W16(data + 4, 1); _m.W16(data + 6, 0);
        _m.W32(data + 8, (uint)samples.Length); _m.W32(data + 12, audio);
        Run(0x8DC1B9, new[] { _interface, data });
        uint callback = _m.Stub("native_voice_end");
        _m.Libc["native_voice_end"] = m =>
        {
            if (m.Reg(0) != 1 || EndOfEvent is not null) throw new EmulationAbort("unexpected native event callback");
            uint p = m.Reg(1);
            EndOfEvent = (m.R32(p), m.R32(p + 4), m.R32(p + 8), m.R32(p + 12));
            return 0;
        };
        uint playing = Run(0x9A6704, new uint[] { 0xBF0BACE1, 7, 1, callback }, new uint[] { 0xC0FFEE, 0, 0, 0 });
        if (playing == 0) throw new EmulationAbort("native event did not return a playing ID");
        Ok(Run(0x99FB14, new uint[] { 0xD2687048, F(1), playing, 0 }, new uint[] { 4, 0 }), "event volume");
        return playing;
    }

    /// <summary>Steps a caller-supplied synthetic Wwise clock; no real-time pacing is implied.</summary>
    public void Render(uint elapsedMicroseconds)
    {
        RequireOwner();
        _clock = checked(_clock + elapsedMicroseconds);
        Run(0x99F130, new uint[] { 1 });
    }

    private static uint F(float value) => BitConverter.SingleToUInt32Bits(value);
    private static float V(uint value) => BitConverter.UInt32BitsToSingle(value);
    private uint A(uint relative) => checked(_base + relative);
    private uint Run(uint relative, uint[]? args = null, uint[]? stack = null) =>
        _m.Call(A(relative), args, stack);
    private void Ok(uint result, string operation)
    {
        if (result != 1) throw new EmulationAbort($"native Wwise {operation} returned {result}");
    }

    public NativeVoiceOracle(string libraryDirectory, byte[] initBank, byte[] englishBank)
    {
        _m = new ArmMachine(libraryDirectory, new[] { "libc++_shared.so", "libcozmoEngine.so" });
        try
        {
            _base = _m.Library("libcozmoEngine.so").Base;
            InstallHost();
            _m.Relocate();
            for (uint p = 0x103E4D8; p < 0x103E59C; p += 4)
                _m.Call(_m.R32(A(p)));
            uint mem = _m.Malloc(0x100), stream = _m.Malloc(0x100), settings = _m.Malloc(0x100), platform = _m.Malloc(0x100);
            _m.W32(mem, 32);
            Ok(Run(0xA7AC68, new[] { mem }), "memory init");
            if (Run(0xA7AC98, new uint[] { 0, 0x1000000, 0x10, 1 }, new uint[] { 0 }) != 0)
                throw new EmulationAbort("native Wwise default pool was not pool 0");
            Run(0x960600, new[] { stream });
            if (Run(0x9604FC, new[] { stream }) == 0) throw new EmulationAbort("native Wwise stream create failed");
            Run(0x99DC68, new[] { settings });
            Run(0x99DCE8, new[] { platform });
            _m.Span(settings + 0x3D, 1)[0] = 0;
            Ok(Run(0x99E3EC, new[] { settings, platform }), "engine init");
            Run(0x99F130, new uint[] { 1 });
            Load(initBank); Load(englishBank);
            _interface = _m.Malloc(0x80);
            Run(0x8DC119, new[] { _interface });
            Run(0x8DC17B, new[] { _interface });
            Run(0x8DC3ED, new uint[] { _interface, 22320, 744 });
            Ok(Run(0x9A3100, new uint[] { 7, 0, 1 }), "register game object");
            uint sends = _m.Malloc(8);
            _m.W32(sends, 0x9FA5953C); _m.W32(sends + 4, F(1));
            Ok(Run(0x9A0354, new uint[] { 7, sends, 1 }), "aux send");
            Ok(Run(0x9A044C, new uint[] { 7, 0, 0 }), "dry level");
            Ok(Run(0x99FE1C, new uint[] { 0x15D4FD9E, 0x36089802, 7 }), "voice switch");
            Ok(Run(0x99FA24, new uint[] { 0x78DEBD05, 0, 7, 0 }, new uint[] { 4, 0 }), "pitch");
            _m.HookAddress(A(0x8DC041), Capture);
        }
        catch { _m.Dispose(); throw; }
    }

    private void Load(byte[] bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (bank.Length == 0) throw new ArgumentException("Bank is empty", nameof(bank));
        uint bytes = _m.Malloc((uint)bank.Length), id = _m.Malloc(4);
        if (bytes == 0 || id == 0) throw new EmulationAbort("native bank allocation failed");
        _m.Write(bytes, bank); // memory-view bank must remain owned until native teardown
        Ok(Run(0x9A35F8, new[] { bytes, (uint)bank.Length, id }), "bank load");
    }

    private StubResult Capture(ArmMachine m)
    {
        uint n = m.Reg(3), ptr = m.Reg(2), fx = m.Reg(1);
        if (n > 744) throw new EmulationAbort("Hijack frame exceeds 744 samples");
        uint parameters = m.R32(checked(fx + 0xA0));
        if (m.R32(checked(parameters + 4)) != 1) throw new EmulationAbort("unexpected Hijack plugin ID");
        if (_frames.Count >= 512) throw new EmulationAbort("native oracle frame limit reached");
        byte[] bytes = m.Read(ptr, checked(n * 4));
        var frame = new float[n];
        for (int i = 0; i < frame.Length; i++) frame[i] = V(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4, 4)));
        _frames.Add(frame);
        return 0;
    }

    private void InstallHost()
    {
        var l = _m.Libc;
        l["sched_get_priority_max"] = _ => 99; l["sched_get_priority_min"] = _ => 1;
        l["sem_init"] = m => { _semaphores.Add(m.Reg(0), checked((int)m.Reg(2))); return 0; };
        l["sem_post"] = m => { uint a = m.Reg(0); _semaphores[a] = checked(_semaphores[a] + 1); return 0; };
        l["sem_wait"] = m =>
        {
            uint a = m.Reg(0);
            if (!_semaphores.TryGetValue(a, out int count)) throw new EmulationAbort("unknown semaphore");
            if (count == 0)
            {
                var ready = _workers.Values.Where(w => w.Entry == A(0xA35324) && _semaphores.GetValueOrDefault(w.Owner + 0x98) > 0).ToArray();
                if (ready.Length != 1) throw new EmulationAbort("bank worker would block or is ambiguous");
                uint owner = ready[0].Owner;
                if (m.R8(owner + 0x9C) != 0) throw new EmulationAbort("bank worker is stopping");
                _semaphores[owner + 0x98]--;
                m.NestedWorkerCall(A(0xA35234), owner, 10_000_000);
                if (_semaphores[a] <= 0) throw new EmulationAbort("bank worker did not signal its waiter");
            }
            _semaphores[a]--; return 0;
        };
        l["sem_destroy"] = m => { if (!_semaphores.Remove(m.Reg(0))) throw new EmulationAbort("unknown semaphore destroy"); return 0; };
        foreach (string n in new[] { "pthread_attr_init", "pthread_attr_destroy", "pthread_attr_setstacksize", "pthread_attr_setdetachstate", "pthread_setschedparam" }) l[n] = _ => 0;
        l["pthread_create"] = m =>
        {
            uint entry = m.Reg(2), owner = m.Reg(3);
            if (entry != A(0xA35324) && entry != A(0x9E9378)) throw new EmulationAbort($"unexpected native worker 0x{entry:X}");
            uint token = ++_nextWorker; _workers.Add(token, (entry, owner)); m.W32(m.Reg(0), token); return 0;
        };
        l["clock"] = _ => _clock;
        l["time"] = m => { uint t = unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()); if (m.Reg(0) != 0) m.W32(m.Reg(0), t); return t; };
        l["slCreateEngine"] = _ => 4; // controlled dummy-device failure; not live-equivalent
        l["__aeabi_l2f"] = m => F((float)unchecked((long)(((ulong)m.Reg(1) << 32) | m.Reg(0))));
        foreach (var (name, fn) in new (string, Func<double, double>)[] { ("expf", Math.Exp), ("floorf", Math.Floor), ("ceilf", Math.Ceiling), ("logf", Math.Log), ("log10f", Math.Log10), ("tanf", Math.Tan), ("sinf", Math.Sin), ("cosf", Math.Cos), ("sqrtf", Math.Sqrt) })
            l[name] = m => F((float)fn(V(m.Reg(0))));
        l["powf"] = m => F((float)Math.Pow(V(m.Reg(0)), V(m.Reg(1))));
        l["fmodf"] = m => F(V(m.Reg(0)) % V(m.Reg(1)));
        l["atan2f"] = m => F((float)Math.Atan2(V(m.Reg(0)), V(m.Reg(1))));
    }
}
