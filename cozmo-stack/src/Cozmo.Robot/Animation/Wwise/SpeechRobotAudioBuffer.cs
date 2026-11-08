namespace Cozmo.Robot.Animation.Wwise;

// One session's controller buffer. HG7/HG8 0x5984E8..59880A: callbacks copy
// each vector into the back stream; the animation consumes the front stream.
internal sealed class SpeechRobotAudioBuffer : IWwiseRobotAudioBuffer
{
    private sealed class Stream(double created)
    {
        internal readonly double Created = created;
        internal readonly Queue<float[]> Frames = new();
        internal bool Complete;
    }
    private readonly object _gate = new();
    private readonly LinkedList<Stream> _streams = new();
    private bool _waitingReset, _retired;

    internal void Open(double createdMilliseconds)
    {
        lock (_gate) { if (!_retired) _streams.AddLast(new Stream(createdMilliseconds)); }
    }
    internal void Push(ReadOnlySpan<float> samples)
    {
        if (samples.Length > CozmoAudio.SamplesPerFrame)
            throw new ArgumentOutOfRangeException(nameof(samples), "Hijack output exceeds its 744-sample quota");
        lock (_gate)
        {
            if (_retired || _waitingReset || _streams.Last is not { Value.Complete: false } back) return;
            back.Value.Frames.Enqueue(samples.ToArray());
        }
    }
    internal void Close()
    {
        lock (_gate)
        {
            if (_retired) return;
            if (_streams.Last is { } back) back.Value.Complete = true;
            _waitingReset = false;
        }
    }
    internal void Retire() { lock (_gate) { _retired = true; _streams.Clear(); } }
    public bool IsWaitingForReset { get { lock (_gate) return _waitingReset; } }
    public bool HasStream { get { lock (_gate) return _streams.Count != 0; } }
    public bool HasData { get { lock (_gate) return _streams.First?.Value.Frames.Count > 0; } }
    public bool IsComplete { get { lock (_gate) return _streams.First?.Value.Complete ?? false; } }
    public double CreatedMilliseconds { get { lock (_gate) return _streams.First?.Value.Created ?? 0; } }
    public void PopAudioBufferStream() { lock (_gate) { if (_streams.First is not null) _streams.RemoveFirst(); } }
    public ReadOnlyMemory<float> PopNextAudioFrameData() => TryPopNextAudioFrameData(out var data) ? data : default;
    public bool TryPopNextAudioFrameData(out ReadOnlyMemory<float> data)
    {
        lock (_gate)
        {
            if (_streams.First is not { } front || !front.Value.Frames.TryDequeue(out var frame)) { data = default; return false; }
            data = frame;
            return true;
        }
    }
    public void ResetAudioBufferAnimationCompleted()
    {
        lock (_gate)
        {
            _waitingReset = _streams.Last is { Value.Complete: false };
            _streams.Clear();
        }
    }
}
