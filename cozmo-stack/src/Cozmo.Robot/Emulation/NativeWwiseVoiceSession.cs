using System.Diagnostics;
using Cozmo.Robot.Animation.Wwise;

namespace Cozmo.Robot.Emulation;

/// <summary>Owns the native machine and all device completions on one serialized thread.</summary>
internal sealed class NativeWwiseVoiceSession : IWwiseRobotAudioEngine
{
    private sealed class RetiredGenerationException : InvalidOperationException
    {
        internal RetiredGenerationException() : base("shared voice generation has retired") { }
    }
    private sealed record Command(Action<NativeWwiseVoiceHost> Run, TaskCompletionSource<object?> Completion);
    private readonly object _gate = new();
    private readonly Queue<Command> _commands = new();
    private readonly AutoResetEvent _work = new(false);
    private readonly TaskCompletionSource<object?> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<object?> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private readonly string _libraryDirectory;
    private readonly byte[] _initBank, _voiceBank;
    private readonly byte[]? _musicBank;
    internal bool HasMusic => _musicBank is not null;
    private readonly string? _archivePath;
    private readonly SpeechRobotAudioBuffer _buffer;
    private readonly Func<IPlatformPcmSink> _openSink;
    private readonly Func<double> _wallMilliseconds;
    private static readonly TimeSpan DeviceTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);
    private bool _closing;
    private bool _postAdmissionClosed;
    private Exception? _failure;
    private IWwiseRobotAudioCallback? _callback;
    private bool _stopRequested;
    private long _completionCount;
    private ulong _lastCompletedToken;
    private long _generation;
    private bool _generationOpen;
    private bool _musicPending, _musicTickQueued;
    private long _musicRequestVersion;
    private uint _nextCookie;
    private readonly Dictionary<uint, (long Generation, IWwiseRobotAudioCallback Callback)> _sharedCallbacks = new();

    private NativeWwiseVoiceSession(string libraryDirectory, byte[] initBank, byte[] voiceBank,
        SpeechRobotAudioBuffer buffer, Func<double> wallMilliseconds, Func<IPlatformPcmSink> openSink, string? archivePath = null, byte[]? musicBank = null)
    {
        _libraryDirectory = libraryDirectory;
        _initBank = initBank;
        _voiceBank = voiceBank;
        _archivePath = archivePath;
        _musicBank = musicBank;
        _buffer = buffer;
        _wallMilliseconds = wallMilliseconds;
        _openSink = openSink;
        _thread = new Thread(Run) { IsBackground = true, Name = "Cozmo native voice" };
        _thread.Start();
    }

    internal static async Task<NativeWwiseVoiceSession> OpenSharedAsync(string libraryDirectory, byte[] initBank,
        byte[] voiceBank, string archivePath, SpeechRobotAudioBuffer buffer, Func<double> wallMilliseconds,
        Func<IPlatformPcmSink>? openSink = null, byte[]? musicBank = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(wallMilliseconds);
        var session = new NativeWwiseVoiceSession(libraryDirectory, initBank, voiceBank, buffer,
            wallMilliseconds, openSink ?? (() => new WinMmPcmSink()), archivePath, musicBank);
        try { await session._ready.Task.ConfigureAwait(false); }
        catch { try { await session._finished.Task.ConfigureAwait(false); } catch { } throw; }
        return session;
    }

    internal Task RequestMusicStateAsync(uint state, bool interrupt, uint minimumMilliseconds, Func<bool>? current = null)
    {
        lock (_gate)
        {
            if (_closing || _failure is not null) return Task.FromException(new InvalidOperationException("shared audio owner retired"));
            if (_musicBank is null) return Task.FromException(new InvalidOperationException("MISSING: shared music bank is unavailable"));
            _musicPending = true;
            long version = ++_musicRequestVersion;
            return Enqueue(host =>
            {
                if (current is not null && !current())
                {
                    lock (_gate) if (version == _musicRequestVersion) _musicPending = host.HasPendingMusic;
                    throw new RetiredGenerationException();
                }
                host.RequestMusicState(state, interrupt, minimumMilliseconds);
            });
        }
    }

    internal Task? UpdateMusic(long monotonicMilliseconds)
    {
        lock (_gate)
        {
            if (_closing || _failure is not null || !_musicPending || _musicTickQueued) return null;
            _musicTickQueued = true;
            long version = _musicRequestVersion;
            return Enqueue(host =>
            {
                host.UpdateMusic(monotonicMilliseconds);
                host.Render(0); // controller tail 0x8D2938 follows conductor promotion
                lock (_gate)
                {
                    _musicTickQueued = false;
                    if (version == _musicRequestVersion) _musicPending = host.HasPendingMusic;
                }
            });
        }
    }

    // The clip owns this adapter; ending a clip does not retire the shared bus or sink.
    internal SharedGeneration BeginGeneration()
    {
        lock (_gate)
        {
            if (_archivePath is null || _closing || _failure is not null || _generationOpen)
                throw new InvalidOperationException("shared voice generation cannot be opened");
            _generationOpen = true;
            return new SharedGeneration(this, ++_generation);
        }
    }

    internal sealed class SharedGeneration(NativeWwiseVoiceSession owner, long generation) : IWwiseRobotAudioEngine, IWwiseAtomicAudioEngine, IDisposable
    {
        internal Task OfferAsync(float[] samples, int sampleRate) => owner.SharedTask(generation, h => h.Offer(samples, sampleRate));
        internal Task ConfigureAsync(uint voiceSwitch, float pitch) => owner.SharedTask(generation, h => h.ConfigureVoice(voiceSwitch, pitch));
        internal Task ClearPendingAsync() => owner.SharedCleanup(generation);
        public uint PostEvent(uint eventId, uint gameObjectId, IWwiseRobotAudioCallback callback) =>
            owner.PostShared(generation, eventId, gameObjectId, callback);
        public uint PostAndRender(uint eventId, uint gameObjectId, float volume, IWwiseRobotAudioCallback callback) =>
            owner.PostShared(generation, eventId, gameObjectId, callback, volume);
        public void SetEventVolume(uint playingId, float volume) => owner.SharedCommand(generation, h => h.SetEventVolume(playingId, volume));
        public void RenderAudio() => owner.SharedCommand(generation, h => h.Render(0));
        public void StopAll(uint gameObjectId) => owner.EndGeneration(generation, gameObjectId, true);
        public void Dispose() => owner.EndGeneration(generation, 7, false);
    }

    private Task SharedCleanup(long generation)
    {
        lock (_gate)
        {
            if (!_generationOpen || generation != _generation || _closing || _failure is not null)
                return Task.FromException(new InvalidOperationException("shared voice generation has retired"));
            // Accepted cleanup stays ahead of this generation's stop and any later offer.
            return Enqueue(host => host.ClearPending());
        }
    }

    private Task SharedTask(long generation, Action<NativeWwiseVoiceHost> action)
    {
        lock (_gate)
        {
            if (!_generationOpen || generation != _generation || _closing || _failure is not null)
                return Task.FromException(new InvalidOperationException("shared voice generation has retired"));
            return Enqueue(host =>
            {
                lock (_gate) if (!_generationOpen || generation != _generation)
                    throw new RetiredGenerationException();
                action(host);
            });
        }
    }

    private uint PostShared(long generation, uint eventId, uint gameObjectId, IWwiseRobotAudioCallback callback, float? atomicVolume = null)
    {
        if (gameObjectId != 7) throw new NotSupportedException("Shared voice owns game object 7");
        ArgumentNullException.ThrowIfNull(callback);
        uint playing = 0;
        Task post;
        lock (_gate)
        {
            if (!_generationOpen || generation != _generation || _closing || _failure is not null) return 0;
            post = Enqueue(host =>
            {
                uint cookie = checked(++_nextCookie);
                _sharedCallbacks.Add(cookie, (generation, callback));
                playing = host.PostShared(eventId, cookie);
                if (playing == 0) _sharedCallbacks.Remove(cookie);
                else if (atomicVolume is { } volume) host.SetEventVolume(playing, volume);
                if (atomicVolume is not null) host.Render(0);
            });
        }
        post.GetAwaiter().GetResult();
        return playing;
    }

    private void SharedCommand(long generation, Action<NativeWwiseVoiceHost> action)
    {
        lock (_gate)
        {
            if (!_generationOpen || generation != _generation || _closing || _failure is not null) return;
            // Commands accepted before the retirement fence precede StopAll in
            // the same FIFO, including the event's volume and ProcessEvents.
            _ = Enqueue(action);
        }
    }

    private void EndGeneration(long generation, uint gameObjectId, bool stop)
    {
        if (gameObjectId != 7) throw new NotSupportedException("Shared voice owns game object 7");
        lock (_gate)
        {
            if (!_generationOpen || generation != _generation) return;
            _generationOpen = false;
            if (stop && !_closing && _failure is null) _ = Enqueue(host => { host.Stop(); host.Render(0); });
        }
    }

    internal static async Task<NativeWwiseVoiceSession> OpenAsync(string libraryDirectory, byte[] initBank,
        byte[] voiceBank, SpeechRobotAudioBuffer buffer, Func<double> wallMilliseconds,
        Func<IPlatformPcmSink>? openSink = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(wallMilliseconds);
        var session = new NativeWwiseVoiceSession(libraryDirectory, initBank, voiceBank, buffer,
            wallMilliseconds, openSink ?? (() => new WinMmPcmSink()));
        try { await session._ready.Task.ConfigureAwait(false); }
        catch
        {
            try { await session._finished.Task.ConfigureAwait(false); }
            catch { /* The startup exception remains the caller's failure. */ }
            throw;
        }
        return session;
    }

    internal Task OfferAsync(float[] samples, int sampleRate) => Enqueue(host => host.Offer(samples, sampleRate));
    internal Task ConfigureAsync(uint voiceSwitch, float pitch) => Enqueue(host => host.ConfigureVoice(voiceSwitch, pitch));
    internal Task ClearPendingAsync() => Enqueue(host => host.ClearPending());
    internal Task Finished => _finished.Task;
    internal Exception? Failure { get { lock (_gate) return _failure; } }
    internal (long Count, ulong LastToken) PlatformCompletions
    {
        get { lock (_gate) return (_completionCount, _lastCompletedToken); }
    }

    public uint PostEvent(uint eventId, uint gameObjectId, IWwiseRobotAudioCallback callback)
    {
        if (eventId != 0xBF0BACE1 || gameObjectId != 7)
            throw new NotSupportedException("Native speech supports the direct Sentence event on game object 7");
        ArgumentNullException.ThrowIfNull(callback);
        uint playing = 0;
        Task post;
        lock (_gate)
        {
            if (_postAdmissionClosed) return 0;
            post = Enqueue(host => { _callback = callback; playing = host.PostOffered(); });
        }
        post.GetAwaiter().GetResult();
        return playing;
    }

    public void SetEventVolume(uint playingId, float volume) =>
        _ = Enqueue(host => host.SetEventVolume(playingId, volume));

    public void RenderAudio() => _ = Enqueue(host => host.Render(0));

    // Stopping the owner is queued. The caller does not wait for the native thread while holding a streamer lock.
    public void StopAll(uint gameObjectId)
    {
        if (gameObjectId != 7) throw new NotSupportedException("Native speech owns game object 7");
        lock (_gate)
        {
            _postAdmissionClosed = true;
            if (!_closing && _failure is null)
                _ = Enqueue(host => { if (!_stopRequested) { host.Stop(); _stopRequested = true; } });
        }
    }

    internal Task CloseAsync()
    {
        lock (_gate)
        {
            _postAdmissionClosed = true;
            if (!_closing)
            {
                _closing = true;
                _work.Set();
            }
            return _finished.Task;
        }
    }

    private Task Enqueue(Action<NativeWwiseVoiceHost> action)
    {
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_failure is not null) return Task.FromException(_failure);
            if (_closing) return Task.FromException(new ObjectDisposedException(nameof(NativeWwiseVoiceSession)));
            _commands.Enqueue(new Command(action, completion));
            _work.Set();
        }
        return completion.Task;
    }

    private void Run()
    {
        NativeWwiseVoiceHost? host = null;
        IPlatformPcmSink? sink = null;
        try
        {
            sink = _openSink();
            host = new NativeWwiseVoiceHost(_libraryDirectory, _initBank, _voiceBank, 1024, _archivePath, musicBank: _musicBank);
            host.HijackOpened += () => _buffer.Open(_wallMilliseconds());
            host.HijackFrame += frame => _buffer.Push(frame);
            host.HijackClosed += _buffer.Close;
            host.EventEnded += () =>
            {
                var callback = _callback;
                _callback = null;
                callback?.Invoke(WwiseAudioCallbackType.Complete);
            };
            host.SharedEventEnded += (cookie, _, _) =>
            {
                if (_sharedCallbacks.Remove(cookie, out var entry))
                {
                    bool current;
                    lock (_gate) current = _generationOpen && _generation == entry.Generation;
                    if (current) entry.Callback.Invoke(WwiseAudioCallbackType.Complete);
                }
            };
            var stopwatch = Stopwatch.StartNew();
            long lastTicks = 0;
            TimeSpan lastCompletion = stopwatch.Elapsed;
            TimeSpan? closingSince = null;
            SubmitAvailable(host, sink);
            _ready.TrySetResult(null);
            while (true)
            {
                Command? command = null;
                bool stop;
                lock (_gate)
                {
                    if (_commands.Count != 0) command = _commands.Dequeue();
                    stop = _closing && command is null;
                }
                if (stop && closingSince is null)
                {
                    closingSince = stopwatch.Elapsed;
                    if ((_archivePath is not null || host.HasPosted) && !host.IsQuiescent)
                    {
                        if (!_stopRequested) { host.Stop(); _stopRequested = true; }
                        host.Render(0);
                        SubmitAvailable(host, sink);
                    }
                    else if (!host.HasPosted) host.ClearPending();
                }
                if (stop && host.IsQuiescent) break;
                if (command is not null)
                {
                    try { command.Run(host); command.Completion.TrySetResult(null); }
                    catch (RetiredGenerationException ex) { command.Completion.TrySetException(ex); }
                    catch (Exception ex) { command.Completion.TrySetException(ex); throw; }
                    SubmitAvailable(host, sink);
                    continue;
                }
                TimeSpan now = stopwatch.Elapsed;
                TimeSpan deviceRemaining = DeviceTimeout - (now - lastCompletion);
                TimeSpan shutdownRemaining = closingSince is { } started ? ShutdownTimeout - (now - started) : DeviceTimeout;
                TimeSpan remaining = deviceRemaining < shutdownRemaining ? deviceRemaining : shutdownRemaining;
                if (remaining <= TimeSpan.Zero || WaitHandle.WaitAny(new[] { _work, sink.CompletionReady }, remaining) == WaitHandle.WaitTimeout)
                    throw new TimeoutException("Native voice device completion or shutdown timed out");
                foreach (ulong token in sink.DrainCompleted())
                {
                    lastCompletion = stopwatch.Elapsed;
                    host.AcknowledgeOutput(token);
                    lock (_gate) { _completionCount++; _lastCompletedToken = token; }
                    long ticks = stopwatch.ElapsedTicks;
                    ulong elapsed = (ulong)((ticks - lastTicks) * 1000000.0 / Stopwatch.Frequency);
                    lastTicks = ticks;
                    if (_archivePath is null) host.Render(checked((uint)elapsed));
                    else host.ServiceOutput(checked((uint)elapsed));
                    SubmitAvailable(host, sink);
                }
            }
        }
        catch (Exception ex)
        {
            lock (_gate) _failure = ex;
            _ready.TrySetException(ex);
            try { _callback?.Invoke(WwiseAudioCallbackType.Error); } catch { }
            (long Generation, IWwiseRobotAudioCallback Callback)[] callbacks = _sharedCallbacks.Values.ToArray();
            _sharedCallbacks.Clear();
            foreach (var entry in callbacks)
            {
                bool current;
                lock (_gate) current = _generationOpen && _generation == entry.Generation;
                if (current) { try { entry.Callback.Invoke(WwiseAudioCallbackType.Error); } catch { } }
            }
        }
        finally
        {
            try { sink?.Dispose(); }
            catch (Exception ex) { lock (_gate) _failure ??= ex; }
            try
            {
                host?.Dispose();
                if (host?.ShutdownDiagnostic is not null)
                    lock (_gate) _failure ??= new InvalidOperationException(host.ShutdownDiagnostic);
            }
            catch (Exception ex) { lock (_gate) _failure ??= ex; }
            lock (_gate)
            {
                _closing = true;
                while (_commands.TryDequeue(out var command))
                    command.Completion.TrySetException(_failure ?? new ObjectDisposedException(nameof(NativeWwiseVoiceSession)));
            }
            _work.Dispose();
            if (Failure is { } failure) _finished.TrySetException(failure);
            else _finished.TrySetResult(null);
        }
    }

    private static void SubmitAvailable(NativeWwiseVoiceHost host, IPlatformPcmSink sink)
    {
        while (host.PeekOutputBlock() is { } block)
        {
            sink.Submit(block.Token, block.Data);
            host.MarkOutputSubmitted(block.Token);
        }
    }
}
