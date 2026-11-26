using System.Diagnostics;
using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation;

// The context owns the shared Wwise runtime. Each robot clip receives a generation.
internal sealed class CannedSpeechOwner : IWwiseRobotAudioHost
{
    private readonly object _gate = new();
    private readonly SpeechRobotAudioBuffer _buffer = new();
    private readonly Task<NativeWwiseVoiceSession> _opening;
    private readonly SemaphoreSlim _lease = new(1, 1);
    private readonly Action<string> _report;
    private readonly Queue<(BehaviorAudioRequest Request, Action<BehaviorAudioRequest, uint>? Applied)> _pendingBehaviorAudio = new();
    private bool _reportedBehaviorAudioStartup;
    private bool _retired;
    private bool _leaseRetiring;

    internal CannedSpeechOwner(string library, string? banks, string archive, Action<string> report, Func<IPlatformPcmSink>? sink = null, bool initScene = false)
    {
        _report = report;
        _opening = Open(library, banks, archive, sink, initScene);
    }

    private async Task<NativeWwiseVoiceSession> Open(string library, string? banks, string archive, Func<IPlatformPcmSink>? sink, bool initScene)
    {
        byte[] init = await ReadBankAsync("Init.bnk").ConfigureAwait(false);
        byte[]? music = initScene ? await ReadBankAsync("Music.bnk").ConfigureAwait(false) : ReadMusicBank(archive);
        byte[][]? beforeCozmo = initScene
            ? [await ReadBankAsync("UI.bnk").ConfigureAwait(false), await ReadBankAsync("SFX.bnk").ConfigureAwait(false)]
            : null;
        byte[] english = await ReadBankAsync("English(US)/Cozmo.bnk").ConfigureAwait(false);
        byte[]? debug = initScene ? await ReadBankAsync("Dev_Debug.bnk").ConfigureAwait(false) : null;
        return await NativeWwiseVoiceSession.OpenSharedAsync(library, init, english, archive, _buffer,
            () => Environment.TickCount64, sink, music, beforeCozmo, debug).ConfigureAwait(false);

        async Task<byte[]> ReadBankAsync(string entry)
        {
            try
            {
                string? file = banks is null ? null : Path.Combine(banks, entry);
                return await (File.Exists(file) ? File.ReadAllBytesAsync(file!) : Task.Run(() =>
                    ReadArchivedBank(archive, entry) ?? throw new FileNotFoundException($"Wwise bank {entry} is unavailable", archive))).ConfigureAwait(false);
            }
            catch (Exception error) when (initScene) { error.Data[NativeWwiseVoiceHost.BankFailureName] = Path.GetFileName(entry); throw; }
        }
    }

    internal static byte[]? ReadMusicBank(string archive) => ReadArchivedBank(archive, "Music.bnk");

    private static byte[]? ReadArchivedBank(string archive, string entry)
    {
        var index = new WwiseZipIndex(new WwiseDiskFileSystem());
        try
        {
            if (!index.Open(archive) || !index.Lookup(entry, out uint offset, out uint size)) return null;
            var bytes = new byte[checked((int)size)];
            if (!index.ReadEntry(offset, 0, size, bytes, 0)) throw new IOException($"Could not read {entry} from the local audio archive");
            return bytes;
        }
        finally { index.Close(); }
    }

    internal Task<CannedSpeechLease> Acquire(float[] samples, Func<bool> current) => AcquireCore(samples,current);
    internal Task<CannedSpeechLease> AcquireOrdinary(Func<bool> current) => AcquireCore(null,current);
    internal Task<CannedSpeechLease> AcquireOrdinaryIfAvailable(Func<bool> current) => AcquireCore(null,current, true);
    internal Task Ready => _opening;
    internal bool MusicAvailable => _opening.IsCompletedSuccessfully && _opening.Result.HasMusic;
    internal Task RequestMusicState(uint state, bool interrupt, uint minimumMilliseconds, Func<bool> current, EnrollmentMusicScope? scope = null)
    {
        if (!_opening.IsCompletedSuccessfully) return Task.FromException(new InvalidOperationException("shared music owner is not ready"));
        var native = _opening.Result;
        lock (_gate)
        {
            if (_retired) throw new ObjectDisposedException(nameof(CannedSpeechOwner));
            if (!current()) return Task.FromException(new OperationCanceledException("music request owner retired"));
            DrainBehaviorAudio();
            return native.RequestMusicStateAsync(state, interrupt, minimumMilliseconds, current, scope);
        }
    }

    internal void StopMusicState(uint state, EnrollmentMusicScope scope)
    {
        if (!_opening.IsCompletedSuccessfully) return;
        var stop = _opening.Result.StopMusicStateAsync(state, scope);
        _ = stop.ContinueWith(failed => _report("MISSING M1-064 enrollment music cleanup failed: " +
            failed.Exception!.GetBaseException().Message), CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    internal void SubmitBehaviorAudio(BehaviorAudioRequest request, Action<BehaviorAudioRequest, uint>? applied)
    {
        lock (_gate)
        {
            if (_retired) return;
            _pendingBehaviorAudio.Enqueue((request, applied));
            DrainBehaviorAudio();
        }
    }

    private void DrainBehaviorAudio()
    {
        if (_opening.IsFaulted)
        {
            _pendingBehaviorAudio.Clear();
            if (!_reportedBehaviorAudioStartup)
            {
                _reportedBehaviorAudioStartup = true;
                _report("MISSING M13-039 Freeplay Wwise startup: " + _opening.Exception!.GetBaseException().Message);
            }
            return;
        }
        if (!_opening.IsCompletedSuccessfully) return;
        var native = _opening.Result;
        while (_pendingBehaviorAudio.TryDequeue(out var entry))
            _ = native.SubmitBehaviorAudio(entry.Request, (request, result) =>
            {
                if (request.Slot == 8 ? result == 0 : result != 1)
                    _report($"Audio M13-039 Wwise rejected request 0x{request.Id:X8} (result {result})");
                entry.Applied?.Invoke(request, result);
            }).ContinueWith(task =>
                _report("MISSING M13-039 Freeplay Wwise request: " + task.Exception!.GetBaseException().Message),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    // Engine audio tail: the worker callback only queues audio-path callbacks.
    // Never wait for startup or for a platform completion on the engine thread.
    internal void UpdateMusicAtEngineTail()
    {
        Task? tick;
        lock (_gate)
        {
            if (_retired) return;
            if (_pendingBehaviorAudio.Count != 0) DrainBehaviorAudio();
            if (!_opening.IsCompletedSuccessfully) return;
            tick = _opening.Result.UpdateMusic(Environment.TickCount64);
        }
        if (tick is null) return;
        try
        {
            if (!tick.Wait(TimeSpan.FromSeconds(2)))
                _report("MISSING MEET-COZMO-NAMED-ENROLLMENT-1: music audio-tail worker timed out");
        }
        catch (AggregateException ex)
        {
            _report("MISSING MEET-COZMO-NAMED-ENROLLMENT-1: music audio-tail failed: " + ex.GetBaseException().Message);
        }
    }
    private async Task<CannedSpeechLease> AcquireCore(float[]? samples, Func<bool> current, bool refuseBusy = false)
    {
        if (refuseBusy)
        {
            bool acquired;
            lock (_gate)
            {
                acquired=_lease.Wait(0);
                if (!acquired && !_leaseRetiring)
                    throw new InvalidOperationException("MISSING: shared authored audio is already owned; predecessor handoff is not established");
            }
            if (!acquired && !await _lease.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false))
                throw new TimeoutException("MISSING: shared authored audio retirement did not complete");
        }
        else await _lease.WaitAsync().ConfigureAwait(false);
        NativeWwiseVoiceSession.SharedGeneration? generation = null;
        try
        {
            lock (_gate) if (_retired) throw new ObjectDisposedException(nameof(CannedSpeechOwner));
            var native = await _opening.ConfigureAwait(false);
            lock (_gate) if (_retired) throw new ObjectDisposedException(nameof(CannedSpeechOwner));
            if (!current()) throw new OperationCanceledException("speech action retired before offer");
            generation = native.BeginGeneration();
            if (!current()) throw new OperationCanceledException("speech action retired before offer");
            if (samples is not null) await generation.OfferAsync(samples, 22050).ConfigureAwait(false);
            return new CannedSpeechLease(this, native, generation);
        }
        catch
        {
            generation?.Dispose();
            Released();
            throw;
        }
    }

    internal void MarkLeaseRetiring() { lock (_gate) _leaseRetiring=true; }
    internal void Released() { lock (_gate) { _leaseRetiring=false; _lease.Release(); } }
    internal void Observe(Task task) => _ = task.ContinueWith(t =>
    {
        if (t.Exception is { } error) _report("M14-006 shared audio: " + error.GetBaseException().Message);
    }, TaskScheduler.Default);

    internal void Retire()
    {
        lock (_gate)
        {
            if (_retired) return;
            _retired = true;
            _pendingBehaviorAudio.Clear();
        }
        _buffer.Retire();
        _ = _opening.ContinueWith(t =>
        {
            if (t.Status == TaskStatus.RanToCompletion) Observe(t.Result.CloseAsync());
            else if (t.Exception is { } e) _report("M14-006 shared audio startup: " + e.GetBaseException().Message);
        }, TaskScheduler.Default);
    }

    public void SetGameObjectAuxSendValues(uint gameObject, uint busId, float sendGain) { }
    public void SetGameObjectOutputBusVolume(uint gameObject, float volume) { }
    public void RegisterRobotAudioBuffer(uint gameObject, int pluginId) { }
    public IWwiseRobotAudioBuffer? GetRobotAudioBuffer(uint gameObject) => gameObject == 7 ? _buffer : null;
}

internal sealed class CannedSpeechLease : IDirectSpeechSession
{
    private readonly CannedSpeechOwner _owner;
    private readonly NativeWwiseVoiceSession _native;
    private readonly NativeWwiseVoiceSession.SharedGeneration _generation;
    private Exception? _dispatchFailure;
    private readonly object _pathGate = new();
    private int _retired;
    private WwiseRobotAudioPath _path;
    internal WwiseRobotAudioPath Path { get { lock (_pathGate) return _path; } }
    internal Exception? Failure => _native.Failure ?? Volatile.Read(ref _dispatchFailure);
    internal CannedSpeechLease(CannedSpeechOwner owner, NativeWwiseVoiceSession native, NativeWwiseVoiceSession.SharedGeneration generation)
    {
        _owner = owner; _native = native; _generation = generation;
        _path=NewPath();
    }
    private WwiseRobotAudioPath NewPath() => new WwiseRobotAudioPath(_owner, _generation, new SerialAudioDispatchFactory(error =>
        {
            Interlocked.CompareExchange(ref _dispatchFailure, error, null);
            _owner.Observe(Task.FromException(error));
        }), (_, _) => 0);
    internal WwiseRobotAudioPath NextOrdinaryPath()
    {
        lock (_pathGate)
        {
            if (_retired != 0) throw new ObjectDisposedException(nameof(CannedSpeechLease));
            _path=NewPath();
            return _path;
        }
    }
    internal Task Configure(uint voiceSwitch, float pitch) => _generation.ConfigureAsync(voiceSwitch, pitch);
    WwiseRobotAudioPath IDirectSpeechSession.Path => Path;
    Exception? IDirectSpeechSession.Failure => Failure;
    Task IDirectSpeechSession.Configure(uint voiceSwitch, float pitch) => Configure(voiceSwitch, pitch);
    void IDirectSpeechSession.Retire() { ClearPending(); Retire(); }
    internal void ClearPending() => _owner.Observe(_generation.ClearPendingAsync());
    internal void Retire()
    {
        WwiseRobotAudioPath path;
        lock (_pathGate) { if (_retired != 0) return; _retired=1; path=_path; }
        _owner.MarkLeaseRetiring();
        Task stopped = path.RetireWithoutAbort();
        _owner.Observe(stopped);
        _ = stopped.ContinueWith(_ => { _generation.Dispose(); _owner.Released(); }, TaskScheduler.Default);
    }
}

// 0x7FB926..0x7FC270: one executor serializes distinct due times. Stop is
// nonblocking under the streamer lock; the path's epoch fences detached work.
internal sealed class SerialAudioDispatchFactory : IWwiseDispatchFactory
{
    private readonly Action<Exception>? _failure;
    internal SerialAudioDispatchFactory(Action<Exception>? failure = null) => _failure = failure;
    public IWwiseDispatchQueue Create(int priority) => new Queue(_failure);
    private sealed class Queue : IWwiseDispatchQueue, IWwiseDispatchStopFence
    {
        private readonly object _gate = new();
        private readonly List<(long Due, Action Action)> _pending = new();
        private readonly AutoResetEvent _wake = new(false);
        private bool _stopped;
        private Action? _tail;
        private readonly Action<Exception>? _failure;
        internal Queue(Action<Exception>? failure) { _failure = failure; new Thread(Run) { IsBackground = true, Name = "Cozmo audio dispatch" }.Start(); }
        public void After(double delayMs, Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (!double.IsFinite(delayMs) || delayMs < 0) throw new ArgumentOutOfRangeException(nameof(delayMs));
            long due = checked(Stopwatch.GetTimestamp() + (long)(delayMs * Stopwatch.Frequency / 1000.0));
            lock (_gate) { if (_stopped) return; _pending.Add((due, action)); _wake.Set(); }
        }
        public void Stop() { lock (_gate) { if (_stopped) return; _stopped = true; _pending.Clear(); _wake.Set(); } }
        public void StopAfterCurrent(Action tail)
        {
            lock (_gate)
            {
                if (_stopped) return;
                _stopped = true; _pending.Clear(); _tail = tail; _wake.Set();
            }
        }
        private void Run()
        {
            try
            {
                while (true)
                {
                    Action? action = null;
                    int wait = Timeout.Infinite;
                    lock (_gate)
                    {
                        if (_stopped) { action = _tail; _tail = null; if (action is null) return; }
                        else if (_pending.Count != 0)
                        {
                            int i = 0;
                            for (int j = 1; j < _pending.Count; j++) if (_pending[j].Due < _pending[i].Due) i = j;
                            long remaining = _pending[i].Due - Stopwatch.GetTimestamp();
                            if (remaining <= 0) { action = _pending[i].Action; _pending.RemoveAt(i); }
                            else wait = (int)Math.Min(int.MaxValue, Math.Ceiling(remaining * 1000.0 / Stopwatch.Frequency));
                        }
                    }
                    if (action is not null) { try { action(); } catch (Exception e) { if (_failure is not null) _failure(e); else Trace.TraceError("M14-006 audio dispatch: " + e.Message); } }
                    else _wake.WaitOne(wait);
                }
            }
            finally { _wake.Dispose(); }
        }
    }
}
