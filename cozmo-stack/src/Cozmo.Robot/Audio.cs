using System.Runtime.InteropServices;
using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>
/// G.711 mu-law companding, the format the robot's speaker expects.
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

    private readonly Action<RobotMessage> _send;
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

    /// <summary>Splits 16-bit PCM at <see cref="SampleRate"/> into mu-law frames, padding the last one with silence.</summary>
    public static List<byte[]> ToFrames(ReadOnlySpan<short> pcm)
    {
        var frames = new List<byte[]>();
        for (int off = 0; off < pcm.Length; off += SamplesPerFrame)
        {
            var frame = new byte[SamplesPerFrame];
            int n = Math.Min(SamplesPerFrame, pcm.Length - off);
            for (int i = 0; i < n; i++) frame[i] = MuLaw.Encode(pcm[off + i]);
            for (int i = n; i < SamplesPerFrame; i++) frame[i] = MuLaw.Encode(0);
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

    /// <summary>Streams PCM to the speaker in real time, pacing frames at the animation rate.</summary>
    public void Play(ReadOnlySpan<short> pcm)
    {
        var frames = ToFrames(pcm);
        using var _ = new HighResolutionTimer();
        _clock.Restart();
        _scheduled = 0;
        foreach (var f in frames) PlayFramePaced(f);
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
