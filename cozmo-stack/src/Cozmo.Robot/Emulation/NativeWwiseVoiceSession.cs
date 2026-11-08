using System.Diagnostics;
using Cozmo.Robot.Animation.Wwise;

namespace Cozmo.Robot.Emulation;

/// <summary>Owns the native machine and all device completions on one serialized thread.</summary>
internal sealed class NativeWwiseVoiceSession : IWwiseRobotAudioEngine
{
    private sealed record Command(Action<NativeWwiseVoiceHost> Run, TaskCompletionSource<object?> Completion);
    private readonly object _gate = new();
    private readonly Queue<Command> _commands = new();
    private readonly AutoResetEvent _work = new(false);
    private readonly TaskCompletionSource<object?> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<object?> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private readonly string _libraryDirectory;
    private readonly byte[] _initBank, _voiceBank;
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

    private NativeWwiseVoiceSession(string libraryDirectory, byte[] initBank, byte[] voiceBank,
        SpeechRobotAudioBuffer buffer, Func<double> wallMilliseconds, Func<IPlatformPcmSink> openSink)
    {
        _libraryDirectory = libraryDirectory;
        _initBank = initBank;
        _voiceBank = voiceBank;
        _buffer = buffer;
        _wallMilliseconds = wallMilliseconds;
        _openSink = openSink;
        _thread = new Thread(Run) { IsBackground = true, Name = "Cozmo native voice" };
        _thread.Start();
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
            host = new NativeWwiseVoiceHost(_libraryDirectory, _initBank, _voiceBank, 1024);
            host.HijackOpened += () => _buffer.Open(_wallMilliseconds());
            host.HijackFrame += frame => _buffer.Push(frame);
            host.HijackClosed += _buffer.Close;
            host.EventEnded += () =>
            {
                var callback = _callback;
                _callback = null;
                callback?.Invoke(WwiseAudioCallbackType.Complete);
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
                    if (host.HasPosted && !host.IsQuiescent)
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
                    host.Render(checked((uint)elapsed));
                    SubmitAvailable(host, sink);
                }
            }
        }
        catch (Exception ex)
        {
            lock (_gate) _failure = ex;
            _ready.TrySetException(ex);
            _callback?.Invoke(WwiseAudioCallbackType.Error);
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
