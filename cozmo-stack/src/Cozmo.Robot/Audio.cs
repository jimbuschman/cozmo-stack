using System.Runtime.InteropServices;
using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>
/// Standard G.711 mu-law. The robot does <b>not</b> use this: see <see cref="AnkiMuLaw"/>.
///
/// The engine streams one <see cref="AudioSample"/> message per animation frame carrying exactly 744
/// 8-bit mu-law samples; at the animation rate of about 30 frames per second that is roughly 22 kHz,
/// which matches the 22.05 kHz voices in the app's own TTS assets.
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

    public static byte Encode(short sample)
    {
        int s = sample < -32767 ? -32767 : sample;      // the engine clamps at -32767, not -32768
        int mag = s ^ (s >> 15);                        // s for positive, ~s for negative
        int hi = mag >> 8;
        int exp = Segment[hi];
        int mantissa = hi == 0 ? mag >> 4 : (mag >> (exp + 3)) & 0x0F;
        return (byte)((s < 0 ? 0x80 : 0) | (exp << 4) | mantissa);
    }

    /// <summary>The engine's own entry point takes a float in -1..1; this matches it exactly.</summary>
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
/// Audio is streamed as fixed 744-sample mu-law frames, one per animation tick. Sending frames faster
/// than the robot consumes them overruns its buffer, so <see cref="Play"/> paces them at the frame
/// interval; <see cref="AudioSilence"/> is what the engine sends when a frame carries no sound.
/// </summary>
public sealed class CozmoAudio
{
    /// <summary>Samples in one audio message (official AudioSample is a fixed 744-byte array).</summary>
    public const int SamplesPerFrame = 744;
    /// <summary>Nominal sample rate: 744 samples at the 30 Hz animation tick.</summary>
    public const int SampleRate = 22050;
    /// <summary>
    /// The animation tick the engine runs at, 30 per second. Frames are sent on this schedule, which is very
    /// slightly faster than the 33.74 ms of audio a frame actually holds, so the robot's short buffer stays
    /// topped up rather than running dry.
    /// </summary>
    public static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / 30);
    /// <summary>Audio actually carried by one frame at <see cref="SampleRate"/>: 33.74 ms.</summary>
    public static readonly TimeSpan FrameDuration = TimeSpan.FromSeconds(SamplesPerFrame / (double)SampleRate);
    /// <summary>
    /// Frames the robot will hold. Measured at about 14 on firmware 2457: sending a whole tone at once made
    /// it play the first 14 frames and silently discard the rest, with its own drop counter still at zero.
    /// </summary>
    public const int RobotBufferFrames = 14;
    /// <summary>
    /// Frames sent back to back at the start of a stream, before pacing begins. Feeding an empty robot at
    /// exactly the rate it drains leaves no slack: one late frame is an underrun, which is heard as a
    /// stutter. Priming builds about a third of a second of cushion first, so scheduling jitter stops
    /// mattering. Kept below <see cref="RobotBufferFrames"/> so nothing is dropped on the way in.
    /// </summary>
    public int PrimeFrames { get; set; } = 10;
    /// <summary>
    /// How many frames the robot should have queued at any moment while a stream is running. Kept under
    /// <see cref="RobotBufferFrames"/> so nothing is dropped going in, and high enough that network jitter
    /// and a lost frame or two cannot empty it.
    /// </summary>
    public int TargetInFlight { get; set; } = 10;
    /// <summary>
    /// Reports how many audio frames the robot says it has played, from its AnimationState stream. When this
    /// is set, <see cref="Play"/> feeds the robot at the rate it actually drains rather than at a fixed
    /// schedule. It does not drain at the rate arithmetic suggests: measured on firmware 2457 it takes a
    /// frame every 28.6 ms, not the 33.3 ms of an animation tick, so a fixed schedule slowly starves it and
    /// the sound breaks into a dashed tone. Leave null to fall back to clock pacing.
    /// </summary>
    public Func<int>? PlayedFrames { get; set; }

    private readonly Action<RobotMessage> _send;
    /// <summary>How samples are packed into a frame. See <see cref="AudioCodec"/>: this is not settled.</summary>
    public AudioCodec Codec { get; set; } = AudioCodec.AnkiMuLaw;
    public int FramesSent { get; private set; }
    /// <summary>When the last frame went out, so a caller can tell whether audio is currently streaming.</summary>
    public DateTime LastSentUtc { get; private set; } = DateTime.MinValue;
    /// <summary>True while frames are actively being streamed.</summary>
    public bool Busy => DateTime.UtcNow - LastSentUtc < TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Invoked after each paced frame. The engine fills every animation tick with both an audio frame and a
    /// face keyframe, so <see cref="CozmoRobot"/> uses this to keep the face alive while sound is playing.
    /// </summary>
    public event Action? OnFrameSent;

    public CozmoAudio(Action<RobotMessage> send) => _send = send;

    /// <summary>Sets speaker volume. The official range is not documented; 0 is silent and larger is louder.</summary>
    public void SetVolume(ushort level) => _send(new SetAudioVolume { Level = level });

    /// <summary>Sends one already-companded frame. Must be exactly <see cref="SamplesPerFrame"/> bytes.</summary>
    public void SendFrame(byte[] mulawFrame)
    {
        if (mulawFrame.Length != SamplesPerFrame)
            throw new ArgumentException($"an audio frame must be exactly {SamplesPerFrame} samples", nameof(mulawFrame));
        _send(new AudioSample { Samples = mulawFrame });
        FramesSent++;
        LastSentUtc = DateTime.UtcNow;
    }

    public void SendSilence() { _send(new AudioSilence()); FramesSent++; LastSentUtc = DateTime.UtcNow; }

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

    /// <summary>Splits 16-bit PCM at <see cref="SampleRate"/> into frames, padding the last one with silence.</summary>
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
    /// listener does not have to judge tone quality, only whether the number of beeps is right. Breaks in
    /// the stream show up as extra beeps or as beeps that arrive at the wrong time.
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

    /// <summary>Streams PCM to the speaker in real time, pacing frames at the animation rate.</summary>
    public void Play(ReadOnlySpan<short> pcm)
    {
        var frames = ToFrames(pcm, Codec);
        using var _ = new HighResolutionTimer();
        _clock.Restart();
        _scheduled = 0;
        if (PlayedFrames is null) { foreach (var f in frames) PlayFramePaced(f); return; }
        PlayWithFeedback(frames);
    }

    /// <summary>
    /// Feeds the robot from its own report of what it has played, keeping <see cref="TargetInFlight"/>
    /// frames queued. This tracks whatever rate the robot really drains at instead of assuming one.
    /// </summary>
    private void PlayWithFeedback(List<byte[]> frames)
    {
        int baseline = PlayedFrames!();
        int sent = 0;
        var lastProgress = _clock.Elapsed;
        int lastPlayed = 0;

        foreach (var f in frames)
        {
            while (true)
            {
                int played = PlayedFrames() - baseline;
                if (played != lastPlayed) { lastPlayed = played; lastProgress = _clock.Elapsed; }
                if (sent - played < TargetInFlight) break;
                // If the robot stops reporting progress it is not playing; send anyway rather than hang.
                if (_clock.Elapsed - lastProgress > TimeSpan.FromSeconds(1)) break;
                Thread.Sleep(2);
            }
            SendFrame(f);
            OnFrameSent?.Invoke();
            sent++;
        }
    }

    /// <summary>
    /// Raises the system timer resolution to 1 ms for as long as it is held.
    ///
    /// Windows schedules sleeps on a 15.6 ms tick by default, which is half an audio frame, so without this
    /// every frame lands up to half a slot late and the tone stutters audibly. Does nothing off Windows,
    /// where sleeps are already fine-grained.
    /// </summary>
    private readonly struct HighResolutionTimer : IDisposable
    {
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint BeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint EndPeriod(uint ms);

        private readonly bool _raised;
        public HighResolutionTimer()
        {
            _raised = false;
            if (!OperatingSystem.IsWindows()) return;
            try { _raised = BeginPeriod(1) == 0; } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
        }
        public void Dispose()
        {
            if (!_raised) return;
            try { EndPeriod(1); } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
        }
    }

    public void PlayTone(double frequencyHz, TimeSpan duration, double amplitude = 0.5)
        => Play(Tone(frequencyHz, duration, amplitude));

    private readonly System.Diagnostics.Stopwatch _clock = new();
    private long _scheduled;

    private void PlayFramePaced(byte[] frame)
    {
        if (!_clock.IsRunning) { _clock.Restart(); _scheduled = 0; }
        // The first PrimeFrames go out at once to fill the robot's buffer; the rest are paced.
        var due = TimeSpan.FromTicks(FrameInterval.Ticks * Math.Max(0, _scheduled - PrimeFrames));
        WaitUntil(due);
        SendFrame(frame);
        OnFrameSent?.Invoke();
        _scheduled++;
        // If something stalled us badly, start a fresh schedule rather than firing a burst to catch up.
        if (_clock.Elapsed - due > FrameInterval * 4) { _clock.Restart(); _scheduled = 0; }
    }

    /// <summary>
    /// Waits until the frame is due, on the high-resolution clock.
    ///
    /// Thread.Sleep and DateTime.UtcNow are both quantised to about 15.6 ms on Windows, which is half a
    /// frame, so scheduling on them makes the stream audibly stutter. The bulk of the wait still goes to
    /// Sleep to keep the thread off the CPU; only the last couple of milliseconds are spun.
    /// </summary>
    private void WaitUntil(TimeSpan due)
    {
        while (true)
        {
            var remaining = due - _clock.Elapsed;
            if (remaining <= TimeSpan.Zero) return;
            if (remaining > TimeSpan.FromMilliseconds(3)) Thread.Sleep(remaining - TimeSpan.FromMilliseconds(2));
            else Thread.SpinWait(50);
        }
    }
}
