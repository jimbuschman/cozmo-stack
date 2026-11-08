using Cozmo.Robot.Animation.Wwise;

namespace Cozmo.Robot.Animation;

/// <summary>The engine's byte-keyed speech operation store and asynchronous synthesis queue.</summary>
internal sealed class TextToSpeechComponent
{
    private sealed class Bundle
    {
        internal byte State = 1;
        internal float[]? Samples;
    }
    private readonly object _gate = new();
    private readonly Dictionary<byte, Bundle> _operations = new();
    private readonly Func<string, byte, float, float[]> _synthesize;
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _errors = new();
    private Task _tail = Task.CompletedTask;
    private byte _nextId;
    private bool _retired;

    internal TextToSpeechComponent(Func<string, byte, float, float[]> synthesize)
    {
        _synthesize = synthesize;
    }

    /// <summary>CreateSpeech 0x0069E4C8. Collision returns zero without replacing the existing operation.</summary>
    internal byte CreateSpeech(string text, byte style, float scalar)
    {
        lock (_gate)
        {
            if (_retired) return 0;
            _nextId = unchecked((byte)(_nextId + 1));
            if (_nextId == 0) _nextId = 1;
            byte id = _nextId;
            if (_operations.ContainsKey(id))
            {
                _errors.Enqueue($"TextToSpeechComponent.CreateSpeech duplicate operation id {id}");
                return 0;
            }
            _operations.Add(id, new Bundle());
            _tail = _tail.ContinueWith(_ => Synthesize(id, text, style, scalar), CancellationToken.None,
                TaskContinuationOptions.None, TaskScheduler.Default);
            return id;
        }
    }

    private void Synthesize(byte id, string text, byte style, float scalar)
    {
        lock (_gate) if (_retired || !_operations.ContainsKey(id)) return;
        float[]? samples = null;
        try { samples = _synthesize(text, style, scalar); }
        catch (Exception ex) { _errors.Enqueue($"M14-006 speech synthesis failed: {ex.Message}"); }
        lock (_gate)
        {
            // The original looks up the byte ID after synthesis. A removed bundle discards the data.
            if (!_operations.TryGetValue(id, out var bundle)) return;
            bundle.Samples = samples;
            bundle.State = samples is null ? (byte)0 : (byte)2;
        }
    }

    internal byte GetOperationState(byte id)
    {
        lock (_gate) return _operations.TryGetValue(id, out var bundle) ? bundle.State : (byte)0;
    }

    internal void DrainErrors(Action<string> report)
    {
        while (_errors.TryDequeue(out var error)) report(error);
    }

    internal void ClearOperationData(byte id)
    {
        lock (_gate) _operations.Remove(id);
    }

    /// <summary>PrepareAudioEngine 0x0069E9A8 moves the data to WavePortal and returns its duration in milliseconds.</summary>
    internal bool PrepareAudioEngine(byte id, out float durationMs)
    {
        lock (_gate)
        {
            durationMs = 0;
            if (!_operations.TryGetValue(id, out var bundle) || bundle.Samples is not { } samples) return false;
            durationMs = (float)((double)samples.Length * 1000.0 / AcapelaTtsSampleRate);
            WavePortal.Shared.Give(samples, AcapelaTtsSampleRate);
            _operations.Remove(id);
            return true;
        }
    }

    // Scoped native host owns the moved data directly; no process-global managed WavePortal slot.
    internal bool TakeSpeech(byte id, out float[] samples)
    {
        lock (_gate)
        {
            if (!_operations.TryGetValue(id, out var bundle) || bundle.Samples is not { } data)
            { samples = Array.Empty<float>(); return false; }
            samples = data;
            _operations.Remove(id);
            return true;
        }
    }

    private const int AcapelaTtsSampleRate = 22050;

    internal void CleanupAudioEngine(byte id)
    {
        WavePortal.Shared.Clear();
        if (id != 0) ClearOperationData(id);
    }

    /// <summary>Stop the queue and dispose its provider after the active native call has exited.</summary>
    internal void Retire(Action disposeProvider)
    {
        lock (_gate)
        {
            if (_retired) return;
            _retired = true;
            _operations.Clear();
            _tail = _tail.ContinueWith(_ => disposeProvider(), CancellationToken.None,
                TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }
}
