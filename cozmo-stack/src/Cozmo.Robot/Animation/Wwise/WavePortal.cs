namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The engine's WavePortal pending ownership slot and source instance. The bridge transfers its one
/// pending buffer to a newly initialized voice at 0x008DD636. A later offer replaces an unclaimed buffer.
/// </summary>
public sealed class WavePortal
{
    public const uint PluginId = 0x006412C2;
    public const int DataReady = 0x2D;
    public const int NoMoreData = 0x11;
    private readonly object _gate = new();
    private (float[] Samples, int Rate)? _pending;

    /// <summary>The process-wide bridge used by the engine's source plug-in.</summary>
    public static WavePortal Shared { get; } = new();

    /// <summary>Transfers the samples to the pending slot, replacing an earlier unclaimed offer.</summary>
    public void Give(float[] samples, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);
        lock (_gate) _pending = (samples, sampleRate);
    }

    /// <summary>CleanupAudioEngine clears the pending slot even when the operation id is zero.</summary>
    public void Clear()
    {
        lock (_gate) _pending = null;
    }

    /// <summary>Source Init claims the slot. Result 2 means there was no pending buffer.</summary>
    public (int Result, Voice? Source) Initialize()
    {
        lock (_gate)
        {
            if (_pending is not { } data) return (2, null);
            _pending = null;
            return (1, new Voice(data.Samples, data.Rate));
        }
    }

    /// <summary>An initialized source owns its samples independently of subsequent pending offers.</summary>
    public sealed class Voice
    {
        private readonly float[] _samples;
        private int _cursor;
        internal Voice(float[] samples, int sampleRate) { _samples = samples; SampleRate = sampleRate; }
        public int SampleRate { get; }

        /// <summary>Fills the supplied frame and pads its final partial frame with zeroes.</summary>
        public int Execute(Span<float> destination)
        {
            int count = Math.Min(destination.Length, _samples.Length - _cursor);
            _samples.AsSpan(_cursor, count).CopyTo(destination);
            _cursor += count;
            if (count == destination.Length) return DataReady;
            destination[count..].Clear();
            return NoMoreData;
        }

        public void Reset() => _cursor = 0;
    }
}
