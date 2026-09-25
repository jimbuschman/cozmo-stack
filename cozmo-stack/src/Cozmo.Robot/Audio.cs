using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Transport;

namespace Cozmo.Robot;

/// <summary>
/// Standard G.711 mu-law. The robot does <b>not</b> use this: see <see cref="AnkiMuLaw"/>.
///
/// The engine streams one <see cref="AudioSample"/> message per animation frame carrying exactly 744
/// 8-bit mu-law samples at 22320 Hz (<see cref="CozmoAudio.SampleRate"/>), which is 33.33 ms of audio per
/// 30 Hz animation frame.
/// </summary>
public static class MuLaw
{
    private const int Bias = 132;
    /// <summary>Largest magnitude that still fits in 15 bits once the bias is added.</summary>
    private const int Clip = 0x7FFF - Bias;

    public static byte Encode(short sample)
    {
        int v = sample;
        int sign = 0;
        if (v < 0) { sign = 0x80; v = -v; }     // widened to int first: -short.MinValue would overflow a short
        if (v > Clip) v = Clip;                 // clip before the bias, or full scale wraps past bit 14
        v += Bias;
        int exponent = 7;
        for (int mask = 0x4000; (v & mask) == 0 && exponent > 0; exponent--, mask >>= 1) { }
        int mantissa = (v >> (exponent + 3)) & 0x0F;
        return (byte)~(sign | (exponent << 4) | mantissa);
    }

    public static short Decode(byte value)
    {
        int u = ~value & 0xFF;
        int sign = u & 0x80;
        int exponent = (u >> 4) & 0x07;
        int mantissa = u & 0x0F;
        int sample = ((mantissa << 3) + Bias) << exponent;
        sample -= Bias;
        return (short)(sign != 0 ? -sample : sample);
    }

    public static byte[] Encode(ReadOnlySpan<short> pcm)
    {
        var o = new byte[pcm.Length];
        for (int i = 0; i < pcm.Length; i++) o[i] = Encode(pcm[i]);
        return o;
    }

    public static short[] Decode(ReadOnlySpan<byte> mulaw)
    {
        var o = new short[mulaw.Length];
        for (int i = 0; i < mulaw.Length; i++) o[i] = Decode(mulaw[i]);
        return o;
    }
}

/// <summary>How 16-bit samples are packed into the robot's 8-bit audio frames.</summary>
public enum AudioCodec
{
    /// <summary>What the robot actually uses. See <see cref="AnkiMuLaw"/>.</summary>
    AnkiMuLaw,
    /// <summary>Standard G.711 mu-law. Cozmo does <b>not</b> use this; kept for comparison.</summary>
    StandardMuLaw,
    /// <summary>Plain 8-bit unsigned PCM, silence at 0x80.</summary>
    UnsignedPcm8,
    /// <summary>Plain 8-bit signed PCM, silence at 0x00.</summary>
    SignedPcm8,
}

/// <summary>
/// The companding the robot's speaker actually expects, taken from the engine rather than assumed.
///
/// Transcribed from <c>Anki::Cozmo::Audio::encodeMuLaw(float)</c> at 0x00597AD8 in libcozmoEngine.so. It is
/// mu-law in shape but differs from G.711 in two ways that matter:
///
/// <list type="bullet">
/// <item>no 132 bias is added to the magnitude before the segment is chosen;</item>
/// <item><b>the result is not complemented.</b> Silence encodes to 0x00, not 0xFF, and every code is the
/// bitwise inverse of what a standard encoder produces.</item>
/// </list>
///
/// Sending standard G.711 instead is heard as a loud buzz at roughly the right pitch, which is exactly what
/// the first hardware runs produced. PyCozmo also omits the complement, so it is closer to this than to
/// G.711, though it still adds the bias.
///
/// The segment table below is copied byte for byte from the engine's .rodata at 0xC5C3F0.
/// </summary>
public static class AnkiMuLaw
{
    /// <summary>Segment exponent indexed by the top 7 bits of the magnitude (engine .rodata 0xC5C3F0).</summary>
    private static readonly byte[] Segment =
    {
        0, 1, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4,
        5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
    };

    // fidelity: M3-010
    /// <summary>
    /// The segment step of <c>encodeMuLaw</c> (C6) on an already-scaled sample: <c>mag = s ^ (s &gt;&gt; 15)</c>,
    /// <c>exp = seg[mag &gt;&gt; 8]</c> (table 0x00C5C3F0), <c>mant = (mag &gt;&gt; 8) == 0 ? mag &gt;&gt; 4 :
    /// (mag &gt;&gt; (exp + 3)) &amp; 0xF</c>, byte = sign 0x80 | exp &lt;&lt; 4 | mant. -32768 is clamped to -32767 as
    /// the float path's clamp gives.
    /// </summary>
    public static byte Encode(short sample)
    {
        int s = sample < -32767 ? -32767 : sample;      // the engine clamps at -32767, not -32768
        int mag = s ^ (s >> 15);                        // s for positive, ~s for negative
        int hi = mag >> 8;
        int exp = Segment[hi];
        int mantissa = hi == 0 ? mag >> 4 : (mag >> (exp + 3)) & 0x0F;
        return (byte)((s < 0 ? 0x80 : 0) | (exp << 4) | mantissa);
    }

    // fidelity: M3-010
    /// <summary>
    /// <c>Audio::encodeMuLaw(float)</c> (C6, 0x00597AD8..0x00597B8E; 32767.0 at 0x00597C18): NaN gives 0 (the engine
    /// also warns); <c>s = f &lt;= -1 ? -32767 : trunc(min(f, 1) * 32767)</c>; then <see cref="Encode(short)"/>'s
    /// segment step. No volume is applied on this path (C7). The product is taken in single precision.
    /// </summary>
    public static byte Encode(float sample)
    {
        if (float.IsNaN(sample)) return 0;
        if (sample <= -1f) return Encode((short)-32767);
        return Encode((short)(int)((sample >= 1f ? 1f : sample) * 32767f));
    }

    /// <summary>Approximate inverse, for writing a stream out to listen to it locally.</summary>
    public static short Decode(byte value)
    {
        int exp = (value >> 4) & 0x07, mantissa = value & 0x0F;
        int mag = exp == 0 ? (mantissa << 4) | 0x08
                           : (1 << (exp + 7)) | (mantissa << (exp + 3)) | (1 << (exp + 2));
        return (short)((value & 0x80) != 0 ? -mag : mag);
    }
}

/// <summary>
/// Cozmo's speaker.
///
/// Audio is streamed as fixed 744-sample mu-law frames at 22320 Hz (M3-011). The engine plays audio only through
/// the animation stream (C4); <see cref="Play"/> and the tone generators are this stack's test API (policy M3-017),
/// and <see cref="Play"/> feeds its frames through the engine's own send buffer and budget
/// (<see cref="StreamSendBuffer"/>, M3-012/M3-013): at most 14 unplayed audio frames and 8192 unplayed bytes,
/// refreshed once per engine Update from what the robot's AnimationState reports.
/// </summary>
public sealed class CozmoAudio
{
    /// <summary>Samples in one audio message (official AudioSample is a fixed 744-byte array).</summary>
    public const int SamplesPerFrame = 744;
    // fidelity: M3-011
    /// <summary>
    /// The robot audio sample rate: <c>HijackAudioPlugIn(22320, 744)</c> and
    /// <c>SetupHijackAudioPlugInAndRobotAudioBuffers(22320, 744)</c> (C3, 0x005942CE..0x005942EA). 744 samples at
    /// 22320 Hz is one 30 Hz frame; the 30 Hz is derived from these two, not a constant of its own.
    /// </summary>
    public const int SampleRate = 22320;
    /// <summary>One frame of audio, 744 / 22320 s: the derived 30 Hz (C3).</summary>
    public static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / 30);
    /// <summary>Audio carried by one frame at <see cref="SampleRate"/>: 744 / 22320 s = 33.33 ms.</summary>
    public static readonly TimeSpan FrameDuration = TimeSpan.FromSeconds(SamplesPerFrame / (double)SampleRate);
    /// <summary>
    /// The engine's audio budget: 14 unplayed frames (<see cref="StreamSendBuffer.AudioFramesAhead"/>, C9). The robot's
    /// real buffer size is firmware (C18).
    /// </summary>
    public const int RobotBufferFrames = StreamSendBuffer.AudioFramesAhead;

    /// <summary>
    /// No longer used. The engine's budget (14 unplayed audio frames and 8192 unplayed bytes, M3-012) replaced the
    /// earlier TargetInFlight model, which was not the engine's; setting this changes nothing.
    /// </summary>
    [Obsolete("The engine's budget (StreamSendBuffer, M3-012) replaced it; the value is ignored.")]
    public int TargetInFlight { get; set; } = 10;

    /// <summary>
    /// For a <see cref="CozmoAudio"/> built on its own (not attached to a robot's stream): the robot's
    /// numAudioFramesPlayed, which the budget is refreshed from. Null counts as 0, the Robot constructor's value.
    /// A robot's own <see cref="CozmoRobot.Audio"/> uses the engine's counters instead.
    /// </summary>
    public Func<int>? PlayedFrames { get; set; }

    /// <summary>As <see cref="PlayedFrames"/>, for numAnimBytesPlayed. Null counts as 0.</summary>
    public Func<int>? PlayedBytes { get; set; }

    /// <summary>
    /// Policy M3-017: how long <see cref="Play"/> waits with none of its frames going out before it gives up and
    /// takes the rest out of the buffer, rather than wait forever for a robot that reports nothing played. It
    /// never sends past the budget.
    /// </summary>
    public static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(1);

    /// <summary>How often a stand-alone <see cref="Play"/> runs the engine Update's refresh and drain: the 60 ms engine tick (B24).</summary>
    internal static readonly TimeSpan StandaloneUpdatePeriod = TimeSpan.FromMilliseconds(60);

    private readonly Action<RobotMessage> _send;
    private StreamSendBuffer? _stream;
    private Action? _kick;

    /// <summary>How samples are packed into a frame.</summary>
    public AudioCodec Codec { get; set; } = AudioCodec.AnkiMuLaw;
    public int FramesSent { get; private set; }
    /// <summary>When the last frame went out.</summary>
    public DateTime LastSentUtc { get; private set; } = DateTime.MinValue;
    /// <summary>True while a <see cref="Play"/> is running.</summary>
    public bool Busy => Volatile.Read(ref _playing) > 0;
    private int _playing;

    /// <summary>
    /// Invoked after each frame of a <see cref="Play"/> goes out.
    /// </summary>
    public event Action? OnFrameSent;

    /// <summary>
    /// A message buffered behind each frame of a <see cref="Play"/>, taken when the frame is buffered.
    /// <see cref="CozmoRobot"/> puts the face there, so a tone keeps the face on screen (policy M3-017).
    /// </summary>
    internal Func<RobotMessage?>? PairedMessage { get; set; }

    /// <summary>
    /// The robot's send, with its result, when this device is on a robot: the stream counts only a send that went
    /// out (0x0057BFAE, 0x0057C47C). Null: every send counts as sent.
    /// </summary>
    internal Func<RobotMessage, bool>? TrySend { get; set; }

    private bool SendNow(RobotMessage m)
    {
        if (TrySend is { } t) return t(m);
        _send(m);
        return true;
    }

    public CozmoAudio(Action<RobotMessage> send) => _send = send;

    /// <summary>
    /// Puts this device on a robot's stream: <see cref="Play"/> buffers into <paramref name="stream"/>, which the
    /// engine Update drains within its budget, and the single-frame sends are counted in its stream counters.
    /// <paramref name="kick"/> makes sure a drain runs.
    /// </summary>
    internal void AttachStream(StreamSendBuffer stream, Action kick)
    {
        _stream = stream;
        _kick = kick;
    }

    /// <summary>Sets speaker volume. The official range is not documented; 0 is silent and larger is louder.</summary>
    public void SetVolume(ushort level) => _send(new SetAudioVolume { Level = level });

    /// <summary>
    /// Sends one already-companded frame at once. Must be exactly <see cref="SamplesPerFrame"/> bytes. On a robot it
    /// counts in the stream counters (C11) but is not held to the budget.
    /// </summary>
    public void SendFrame(byte[] mulawFrame)
    {
        if (mulawFrame.Length != SamplesPerFrame)
            throw new ArgumentException($"an audio frame must be exactly {SamplesPerFrame} samples", nameof(mulawFrame));
        SendCounted(new AudioSample { Samples = mulawFrame });
        FramesSent++;
        LastSentUtc = DateTime.UtcNow;
    }

    /// <summary>Sends one AudioSilence at once, counted like <see cref="SendFrame"/>.</summary>
    public void SendSilence() { SendCounted(new AudioSilence()); FramesSent++; LastSentUtc = DateTime.UtcNow; }

    private void SendCounted(RobotMessage m)
    {
        if (_stream is { } s) s.SendDirect(m, SendNow);
        else SendNow(m);
    }

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26, CC27; the engine's AudioComponent
    /// is destroyed with the Robot, so nothing more of what it was playing goes out). A <see cref="Play"/> running on
    /// another thread ends at its next check without sending another frame: the removal count it captured no longer
    /// matches, the check and the send are one step under <see cref="_playGate"/>, and the robot's stream buffer is
    /// emptied by the same removal. Nothing is sent. <see cref="Codec"/>, <see cref="PlayedFrames"/> and
    /// <see cref="PlayedBytes"/> are settings and are kept, as are subscribers and the stream attachment.
    /// </summary>
    internal void ResetToConstructed()
    {
        lock (_playGate)
        {
            _removals++;
            FramesSent = 0;
            LastSentUtc = DateTime.MinValue;
        }
    }

    /// <summary>Makes a removal and a Play's send one step apart from each other (see <see cref="ResetToConstructed"/>).</summary>
    private readonly object _playGate = new();
    /// <summary>Bumped by every removal; a Play started before one sends nothing more.</summary>
    private int _removals;

    private bool RemovedSince(int removal) => Volatile.Read(ref _removals) != removal;

    /// <summary>Sends one frame of a Play unless the robot was removed since it started (then the drain stops at it).</summary>
    private bool SendPlayFrame(byte[] frame, int removal)
    {
        lock (_playGate)
        {
            if (_removals != removal) return false;
            if (!SendNow(new AudioSample { Samples = frame })) return false;
            FramesSent++;
            LastSentUtc = DateTime.UtcNow;
        }
        OnFrameSent?.Invoke();
        return true;
    }

    /// <summary>Packs one sample with the chosen law.</summary>
    public static byte Pack(short sample, AudioCodec codec) => codec switch
    {
        AudioCodec.StandardMuLaw => MuLaw.Encode(sample),
        AudioCodec.UnsignedPcm8 => (byte)((sample >> 8) + 128),
        AudioCodec.SignedPcm8 => (byte)(sample >> 8),
        _ => AnkiMuLaw.Encode(sample),
    };

    /// <summary>Unpacks one sample, so a stream can be written out and listened to locally.</summary>
    public static short Unpack(byte value, AudioCodec codec) => codec switch
    {
        AudioCodec.StandardMuLaw => MuLaw.Decode(value),
        AudioCodec.UnsignedPcm8 => (short)((value - 128) << 8),
        AudioCodec.SignedPcm8 => (short)((sbyte)value << 8),
        _ => AnkiMuLaw.Decode(value),
    };

    // fidelity: M3-010
    /// <summary>
    /// Splits 16-bit PCM at <see cref="SampleRate"/> into frames. A last frame shorter than 744 samples is padded with
    /// the codec's zero, which for the engine's codec is 0x00 (C5).
    /// </summary>
    public static List<byte[]> ToFrames(ReadOnlySpan<short> pcm, AudioCodec codec = AudioCodec.AnkiMuLaw)
    {
        var frames = new List<byte[]>();
        for (int off = 0; off < pcm.Length; off += SamplesPerFrame)
        {
            var frame = new byte[SamplesPerFrame];
            int n = Math.Min(SamplesPerFrame, pcm.Length - off);
            for (int i = 0; i < n; i++) frame[i] = Pack(pcm[off + i], codec);
            for (int i = n; i < SamplesPerFrame; i++) frame[i] = Pack(0, codec);
            frames.Add(frame);
        }
        return frames;
    }

    /// <summary>A sine tone. Amplitude is 0..1 of full scale; a short fade avoids the click of a hard edge.</summary>
    public static short[] Tone(double frequencyHz, TimeSpan duration, double amplitude = 0.5)
    {
        int n = (int)(duration.TotalSeconds * SampleRate);
        var pcm = new short[n];
        int fade = Math.Min(SampleRate / 100, n / 2);   // 10 ms
        for (int i = 0; i < n; i++)
        {
            double env = 1.0;
            if (i < fade) env = i / (double)fade;
            else if (i >= n - fade) env = (n - 1 - i) / (double)fade;
            pcm[i] = (short)(Math.Sin(2 * Math.PI * frequencyHz * i / SampleRate) * amplitude * env * short.MaxValue);
        }
        return pcm;
    }

    /// <summary>
    /// A run of separated beeps. Counting them is an objective test of whether playback is continuous: the
    /// listener does not have to judge tone quality, only whether the number of beeps is right.
    /// </summary>
    public static short[] Beeps(int count, double frequencyHz = 880, double onSeconds = 0.25,
                               double offSeconds = 0.25, double amplitude = 0.5)
    {
        var on = Tone(frequencyHz, TimeSpan.FromSeconds(onSeconds), amplitude);
        int off = (int)(offSeconds * SampleRate);
        var pcm = new short[count * (on.Length + off)];
        for (int i = 0; i < count; i++) on.CopyTo(pcm, i * (on.Length + off));
        return pcm;
    }

    /// <summary>
    /// A glide from one pitch to another. A continuous stream is heard as one smooth rise; a stream that
    /// breaks up is heard as steps, which is easier to notice than roughness in a steady tone.
    /// </summary>
    public static short[] Sweep(double fromHz, double toHz, TimeSpan duration, double amplitude = 0.5)
    {
        int n = (int)(duration.TotalSeconds * SampleRate);
        var pcm = new short[n];
        int fade = Math.Min(SampleRate / 100, n / 2);
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double f = fromHz + (toHz - fromHz) * i / n;
            phase += 2 * Math.PI * f / SampleRate;
            double env = i < fade ? i / (double)fade : i >= n - fade ? (n - 1 - i) / (double)fade : 1.0;
            pcm[i] = (short)(Math.Sin(phase) * amplitude * env * short.MaxValue);
        }
        return pcm;
    }

    // fidelity: M3-017
    /// <summary>
    /// Streams PCM to the speaker and returns when every frame has gone out (policy M3-017: the engine has no such
    /// call). The frames go into the engine's send buffer and are drained within the engine's budget
    /// (<see cref="StreamSendBuffer"/>, M3-012/M3-013): on a robot by its engine Update, on a stand-alone device by
    /// this call itself once per <see cref="StandaloneUpdatePeriod"/>, from <see cref="PlayedFrames"/> and
    /// <see cref="PlayedBytes"/>. With nothing going out for <see cref="StallTimeout"/> (policy M3-017) the rest is
    /// taken back out of the buffer and this returns. A removal of the robot ends it too.
    /// </summary>
    public void Play(ReadOnlySpan<short> pcm)
    {
        var frames = ToFrames(pcm, Codec);
        int removal = Volatile.Read(ref _removals);   // fidelity: M1-025, M1-015 (a removal ends this Play)
        var stream = _stream;
        bool standalone = stream is null;
        stream ??= new StreamSendBuffer();
        var token = new object();
        Interlocked.Increment(ref _playing);
        try
        {
            foreach (var f in frames)
            {
                var frame = f;
                stream.Buffer(token, StreamSendBuffer.SizeOf(new AudioSample { Samples = frame }), true,
                              () => SendPlayFrame(frame, removal));
                if (PairedMessage?.Invoke() is { } partner) stream.Buffer(token, partner, SendNow);
            }
            _kick?.Invoke();

            using var _ = new HighResolutionTimer();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int lastPending = int.MaxValue;
            var lastProgress = TimeSpan.Zero;
            while (true)
            {
                if (RemovedSince(removal)) { stream.Remove(o => ReferenceEquals(o, token)); return; }
                if (standalone)
                {
                    // one engine Update: UpdateAmountToSend (C9), then SendBufferedMessages (C14)
                    stream.UpdateAmountToSend(PlayedBytes?.Invoke() ?? 0, PlayedFrames?.Invoke() ?? 0);
                    stream.SendBufferedMessages();
                }
                int pending = stream.PendingFor(token);
                if (pending == 0) return;
                if (pending != lastPending) { lastPending = pending; lastProgress = clock.Elapsed; }
                else if (clock.Elapsed - lastProgress > StallTimeout)
                {
                    stream.Remove(o => ReferenceEquals(o, token));
                    return;
                }
                Thread.Sleep(standalone ? StandaloneUpdatePeriod : TimeSpan.FromMilliseconds(2));
            }
        }
        finally { Interlocked.Decrement(ref _playing); }
    }
}
