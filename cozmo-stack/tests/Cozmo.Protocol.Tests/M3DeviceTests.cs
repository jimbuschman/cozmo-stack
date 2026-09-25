using System.Net;
using System.Security.Cryptography;
using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Transport;
using Xunit;
using FaceMsg = Cozmo.Protocol.FaceImage;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The M3 device layer against re-analysis/inventory/M3-device.md: every expected value comes from a row of that
/// inventory (Appendix A rows A*, B*, C*; Appendix B rows 1a..1q, 2a..2f, 3a..3b), named in each test, never from what
/// the code returns.
/// </summary>
public class M3DeviceTests
{
    // ================================================================== display: M3-006, M3-007, M3-009

    private const int Rows = 64, Cols = 128;

    private static byte[] Canvas(params (int Row, int Col)[] lit)
    {
        var c = new byte[Rows * Cols];
        foreach (var (r, col) in lit) c[r * Cols + col] = 1;
        return c;
    }

    /// <summary>B13 (from B8): a blank canvas is two skip opcodes, {0x3F, 0x3F}.</summary>
    [Fact]
    public void M3_009_B13_ABlankCanvasEncodesAs3F3F()
    {
        Assert.Equal(new byte[] { 0x3F, 0x3F }, FaceBitmapCodec.EncodeCanvas(new byte[Rows * Cols], Rows, Cols));
        Assert.Equal(new byte[] { 0x3F, 0x3F }, FaceBitmapCodec.Encode(new FaceBitmap()));
    }

    /// <summary>
    /// B10: column 0 with only row 0 lit is pair 0 = 1 (bit0 = row 0), one run: 0x80 | (0 &lt;&lt; 2) | 1 = 0x81; its
    /// trailing pair-0 run is dropped because column 1 is empty (B11). B8: the 127 empty columns that follow are a
    /// skip of 1 + 63 (0x3F, capped at n &lt;= 63) and then 1 + 62 (0x3E, capped at c + n &lt;= 127).
    /// </summary>
    [Fact]
    public void M3_007_B8_B10_B11_OneLitPixelThenSkips()
        => Assert.Equal(new byte[] { 0x81, 0x3F, 0x3E }, FaceBitmapCodec.EncodeCanvas(Canvas((0, 0)), Rows, Cols));

    /// <summary>
    /// B9: every column equal to the first: 0x81 for column 0 (its trailing pair-0 run dropped, the next column being
    /// equal, B11), then repeat 0x40 | 63 for columns 1..64 and 0x40 | 62 for 65..127.
    /// </summary>
    [Fact]
    public void M3_007_B9_EqualColumnsAreRepeats()
    {
        var lit = Enumerable.Range(0, Cols).Select(c => (0, c)).ToArray();
        Assert.Equal(new byte[] { 0x81, 0x7F, 0x7E }, FaceBitmapCodec.EncodeCanvas(Canvas(lit), Rows, Cols));
    }

    /// <summary>
    /// B11: at c == 127 the trailing run is always emitted. Columns 0..126 are empty (skip 0x3F for 0..63, skip 0x3E
    /// for 64..126), column 127 has row 0 lit: 0x81, then its pair-0 run of 31 pairs, 0x80 | (30 &lt;&lt; 2) = 0xF8.
    /// </summary>
    [Fact]
    public void M3_007_B11_TheTrailingRunIsAlwaysEmittedAtColumn127()
        => Assert.Equal(new byte[] { 0x3F, 0x3E, 0x81, 0xF8 }, FaceBitmapCodec.EncodeCanvas(Canvas((0, 127)), Rows, Cols));

    /// <summary>
    /// B11: a trailing pair-0 run is kept when the next column is non-empty and different. Column 0: row 0 (0x81, then
    /// the 31-pair zero run 0xF8, kept). Column 1: row 2 is pair 1 bit 0, so runs pair 0 = 0 (0x80), pair 1 = 1 (0x81),
    /// then a 30-pair zero run dropped because column 2 is empty. Then skip 1 + 63 (0x3F) and 1 + 61 (0x3D).
    /// </summary>
    [Fact]
    public void M3_007_B11_ATrailingZeroRunIsKeptWhenTheNextColumnDiffers()
        => Assert.Equal(new byte[] { 0x81, 0xF8, 0x80, 0x81, 0x3F, 0x3D },
                        FaceBitmapCodec.EncodeCanvas(Canvas((0, 0), (2, 1)), Rows, Cols));

    /// <summary>
    /// B10: pair p is row 2p in bit 0 and row 2p + 1 in bit 1. Column 0 with rows 0 and 1 (pair 0 = 3), row 2 (pair 1 =
    /// 1) and row 5 (pair 2 = bit 1 = 2), in the last column so the trailing zero run is emitted (B11): 0x83, 0x81,
    /// 0x82, then 29 zero pairs 0x80 | (28 &lt;&lt; 2) = 0xF0.
    /// </summary>
    [Fact]
    public void M3_007_B10_PairBitsAreRow2kAndRow2kPlus1()
        => Assert.Equal(new byte[] { 0x3F, 0x3E, 0x83, 0x81, 0x82, 0xF0 },
                        FaceBitmapCodec.EncodeCanvas(Canvas((0, 127), (1, 127), (2, 127), (5, 127)), Rows, Cols));

    /// <summary>
    /// A column of 2k runs, every column different from the one before (pairs alternate v, 0, v, ... with v = 1 on even
    /// columns and 2 on odd ones), so each column's trailing zero run is emitted (B11) and each column costs 2k bytes.
    /// </summary>
    private static byte[] RunColumns(Func<int, int[]> pairsOf)
    {
        var canvas = new byte[Rows * Cols];
        for (int c = 0; c < Cols; c++)
        {
            var pairs = pairsOf(c);
            for (int p = 0; p < 32; p++)
            {
                if ((pairs[p] & 1) != 0) canvas[(2 * p) * Cols + c] = 1;
                if ((pairs[p] & 2) != 0) canvas[(2 * p + 1) * Cols + c] = 1;
            }
        }
        return canvas;
    }

    private static int[] EightRuns(int c)
    {
        int v = c % 2 == 0 ? 1 : 2;
        var p = new int[32];
        p[0] = v; p[2] = v; p[4] = v; p[6] = v;       // v,0,v,0,v,0,v, then 25 zero pairs: 8 runs
        return p;
    }

    private static int[] SevenRunsEndingLit(int c)
    {
        int v = c % 2 == 0 ? 1 : 2;
        var p = new int[32];
        p[0] = v; p[2] = v; p[4] = v; p[31] = v;      // v,0,v,0,v,0(26),v: 7 runs, the last one lit
        return p;
    }

    /// <summary>
    /// B12: the raw fallback applies when the RLE is at least 1024 bytes. 128 columns of 8 bytes is exactly 1024, so
    /// the payload is the 128 little-endian u64 column masks (bit r = row r); with one column of 7 bytes the RLE is
    /// 1023 bytes and is sent as it is.
    /// </summary>
    [Fact]
    public void M3_007_B12_TheRawFallbackStartsAtExactly1024Bytes()
    {
        var at1024 = RunColumns(EightRuns);
        var raw = FaceBitmapCodec.EncodeCanvas(at1024, Rows, Cols)!;
        Assert.Equal(1024, raw.Length);
        for (int c = 0; c < Cols; c++)
        {
            ulong mask = 0;
            for (int r = 0; r < Rows; r++) if (at1024[r * Cols + c] != 0) mask |= 1UL << r;
            Assert.Equal(mask, BitConverter.ToUInt64(raw, c * 8));
        }
        Assert.Equal(at1024, FaceBitmapCodec.DecodeCanvas(raw));        // B14: size 1024 is raw

        var at1023 = RunColumns(c => c == 5 ? SevenRunsEndingLit(c) : EightRuns(c));
        var rle = FaceBitmapCodec.EncodeCanvas(at1023, Rows, Cols)!;
        Assert.Equal(1023, rle.Length);
        Assert.All(rle, b => Assert.True((b & 0x80) != 0, "every byte of this image is a run"));
        Assert.Equal(at1023, FaceBitmapCodec.DecodeCanvas(rle));        // B14 reference decoder
    }

    /// <summary>B6: CompressRLE needs a 64 x 128 image; anything else gives no face at all.</summary>
    [Fact]
    public void M3_007_B6_OnlyA64By128CanvasIsEncoded()
    {
        Assert.Null(FaceBitmapCodec.EncodeCanvas(new byte[64 * 127], 64, 127));
        Assert.Null(FaceBitmapCodec.EncodeCanvas(new byte[32 * 128], 32, 128));
    }

    /// <summary>B7: bit r of a column mask is set when pixel (r, c) is non-zero; any non-zero value counts.</summary>
    [Fact]
    public void M3_007_B7_AnyNonZeroPixelIsLit()
    {
        var c = new byte[Rows * Cols];
        c[0] = 0x80;                                              // row 0, column 0
        Assert.Equal(new byte[] { 0x81, 0x3F, 0x3E }, FaceBitmapCodec.EncodeCanvas(c, Rows, Cols));
    }

    /// <summary>
    /// B1, B15: the canvas is 64 rows x 128 columns and the wire image 128 columns of 64 rows as 32 two-row pairs. A
    /// pixel on the last canvas row (63) is pair 31, bit 1. This stack's 128 x 32 bitmap puts its row y on canvas row
    /// 2y (the class summary), so its row 31 is pair 31, bit 0.
    /// </summary>
    [Fact]
    public void M3_006_B1_B15_TheWireImageIs128ColumnsOf64RowsIn32Pairs()
    {
        var last = FaceBitmapCodec.EncodeCanvas(Canvas((63, 127)), Rows, Cols)!;
        Assert.Equal(new byte[] { 0x3F, 0x3E, 0x80 | (30 << 2) | 0, 0x80 | (0 << 2) | 2 }, last);

        var bmp = new FaceBitmap();
        bmp[127, 31] = 1;
        Assert.Equal(new byte[] { 0x3F, 0x3E, 0x80 | (30 << 2) | 0, 0x80 | (0 << 2) | 1 }, FaceBitmapCodec.Encode(bmp));
        Assert.Equal(1, FaceBitmapCodec.DecodeCanvas(last)[63 * Cols + 127]);
    }

    // ================================================================== audio encoding: M3-010, M3-011

    /// <summary>
    /// C6: NaN gives 0; f &lt;= -1 gives s = -32767; otherwise s = trunc(min(f, 1) * 32767). So f = 1.5 encodes as 1.0,
    /// f = -5 as -1, and the float path lands on the same byte as the scaled sample. (The segment table 0x00C5C3F0's
    /// values are not in the inventory, so every check here is relative to the table.)
    /// </summary>
    [Fact]
    public void M3_010_C6_TheFloatIsClampedScaledAndTruncated()
    {
        Assert.Equal(0, AnkiMuLaw.Encode(float.NaN));
        Assert.Equal(AnkiMuLaw.Encode(1f), AnkiMuLaw.Encode(1.5f));
        Assert.Equal(AnkiMuLaw.Encode((short)32767), AnkiMuLaw.Encode(1f));
        Assert.Equal(AnkiMuLaw.Encode(-1f), AnkiMuLaw.Encode(-5f));
        Assert.Equal(AnkiMuLaw.Encode((short)-32767), AnkiMuLaw.Encode(-1f));
        Assert.Equal(AnkiMuLaw.Encode((short)16383), AnkiMuLaw.Encode(0.5f));          // trunc(16383.5)
        Assert.Equal(AnkiMuLaw.Encode((short)-16383), AnkiMuLaw.Encode(-0.5f));        // trunc toward zero
    }

    /// <summary>
    /// C6: mag = s ^ (s &gt;&gt; 15); byte = (s &lt; 0 ? 0x80 : 0) | exp &lt;&lt; 4 | mant, with mant = mag &gt;&gt; 4 when
    /// mag &gt;&gt; 8 is 0. So s = -1 has mag 0 and is s = 0 with the sign bit; s = 240 (mag &gt;&gt; 8 = 0) is s = 0 with
    /// mantissa 15; s = -241 has mag 240.
    /// </summary>
    [Fact]
    public void M3_010_C6_SignMagnitudeAndTheLowSegment()
    {
        byte zero = AnkiMuLaw.Encode((short)0);
        Assert.Equal(zero | 0x80, AnkiMuLaw.Encode((short)-1));
        Assert.Equal(zero | 0x0F, AnkiMuLaw.Encode((short)240));
        Assert.Equal(zero | 0x0F | 0x80, AnkiMuLaw.Encode((short)-241));
        Assert.Equal(zero | 0x01, AnkiMuLaw.Encode((short)16));
    }

    /// <summary>C5: a frame shorter than 744 samples is zero-padded, 0x00 being silence in this codec.</summary>
    [Fact]
    public void M3_010_C5_AShortFrameIsPaddedWith00()
    {
        var frames = CozmoAudio.ToFrames(Enumerable.Repeat((short)8000, 10).ToArray());
        var f = Assert.Single(frames);
        Assert.Equal(744, f.Length);
        Assert.All(f.Skip(10), b => Assert.Equal(0x00, b));
    }

    /// <summary>C3: 22320 Hz and 744 samples per frame; 30 Hz is 22320 / 744.</summary>
    [Fact]
    public void M3_011_C3_22320HzAnd744Samples()
    {
        Assert.Equal(22320, CozmoAudio.SampleRate);
        Assert.Equal(744, CozmoAudio.SamplesPerFrame);
        Assert.Equal(30, CozmoAudio.SampleRate / CozmoAudio.SamplesPerFrame);
    }

    // ================================================================== budgets and drain: M3-012..M3-015

    /// <summary>C9: bytes = min(8192 - (streamed - played), 30000), a negative value warning and becoming 0.</summary>
    [Fact]
    public void M3_012_C9_TheByteBudget()
    {
        var s = new StreamSendBuffer();
        var log = new List<string>();
        s.Log = log.Add;
        s.UpdateAmountToSend(0, 0);
        Assert.Equal(8192, s.NumBytesToSend);

        s.SendDirect(1000, false, () => true);
        s.UpdateAmountToSend(400, 0);
        Assert.Equal(8192 - (1000 - 400), s.NumBytesToSend);

        s.UpdateAmountToSend(1000 + 40000, 0);                 // the robot reports more played than streamed
        Assert.Equal(30000, s.NumBytesToSend);

        s.SendDirect(9000, false, () => true);                 // 10000 streamed, none played: 8192 - 10000 < 0
        s.UpdateAmountToSend(0, 0);
        Assert.Equal(0, s.NumBytesToSend);
        Assert.Single(log);
    }

    /// <summary>C9: audio = max(14 - (framesStreamed - framesPlayed), 0).</summary>
    [Fact]
    public void M3_012_C9_TheAudioBudget()
    {
        var s = new StreamSendBuffer();
        for (int i = 0; i < 10; i++) s.SendDirect(new AudioSilence(), _ => true);
        s.UpdateAmountToSend(0, 3);
        Assert.Equal(14 - (10 - 3), s.NumAudioFramesToSend);
        for (int i = 0; i < 10; i++) s.SendDirect(new AudioSilence(), _ => true);
        s.UpdateAmountToSend(0, 0);
        Assert.Equal(0, s.NumAudioFramesToSend);
    }

    /// <summary>
    /// C11, C12: every send adds EngineToRobot::Size() (1 for the tag plus the member) to bytes streamed; AudioSample
    /// (0x8E, 744 bytes, C4), AudioSilence (0x8F) and EndOfAnimation add one frame each; other messages none.
    /// </summary>
    [Fact]
    public void M3_012_C11_C12_WhatEachSendCounts()
    {
        var s = new StreamSendBuffer();
        s.SendDirect(new AudioSample(), _ => true);
        Assert.Equal((1 + 744, 1), (s.BytesStreamed, s.FramesStreamed));
        s.SendDirect(new AudioSilence(), _ => true);
        Assert.Equal((1 + 744 + 1, 2), (s.BytesStreamed, s.FramesStreamed));
        s.SendDirect(new EndOfAnimation(), _ => true);
        Assert.Equal((1 + 744 + 1 + 1, 3), (s.BytesStreamed, s.FramesStreamed));
        s.SendDirect(new FaceMsg { Image = new byte[] { 0x3F, 0x3F } }, _ => true);
        Assert.Equal(3, s.FramesStreamed);                      // a face is not an audio frame
        Assert.True(StreamSendBuffer.IsAudioMessage(RobotMessageId.AnimAudioSample));
        Assert.True(StreamSendBuffer.IsAudioMessage(RobotMessageId.AnimAudioSilence));
        Assert.False(StreamSendBuffer.IsAudioMessage(RobotMessageId.AnimFaceImage));
    }

    /// <summary>C14: FIFO, and the drain stops at the first message over the byte budget; nothing behind it goes.</summary>
    [Fact]
    public void M3_013_C14_TheDrainStopsAtTheFirstMessageOverTheByteBudget()
    {
        var s = new StreamSendBuffer();
        s.SendDirect(4192, false, () => true);                 // 4192 unplayed: the budget is 8192 - 4192 = 4000
        s.UpdateAmountToSend(0, 0);
        var sent = new List<int>();
        s.Buffer(null, 100, false, () => { sent.Add(100); return true; });
        s.Buffer(null, 5000, false, () => { sent.Add(5000); return true; });
        s.Buffer(null, 10, false, () => { sent.Add(10); return true; });
        Assert.False(s.SendBufferedMessages());
        Assert.Equal(new[] { 100 }, sent);
        Assert.Equal(2, s.Count);
        Assert.Equal(3900, s.NumBytesToSend);
    }

    /// <summary>C14: an audio message stops the drain when the audio budget is 0; the messages before it go.</summary>
    [Fact]
    public void M3_013_C14_AnAudioMessageStopsTheDrainWhenTheAudioBudgetIs0()
    {
        var s = new StreamSendBuffer();
        for (int i = 0; i < 14; i++) s.SendDirect(new AudioSilence(), _ => true);
        s.UpdateAmountToSend(0, 0);
        Assert.Equal(0, s.NumAudioFramesToSend);
        var sent = new List<string>();
        s.Buffer(null, 3, false, () => { sent.Add("face"); return true; });
        s.Buffer(null, 1, true, () => { sent.Add("audio"); return true; });
        s.Buffer(null, 3, false, () => { sent.Add("after"); return true; });
        Assert.False(s.SendBufferedMessages());
        Assert.Equal(new[] { "face" }, sent);
    }

    /// <summary>C12: EndOfAnimation is sent directly and is not budget-gated, even with no budget left.</summary>
    [Fact]
    public void M3_014_C12_EndOfAnimationIsNotBudgetGated()
    {
        var s = new StreamSendBuffer();
        s.UpdateAmountToSend(0, 0);
        for (int i = 0; i < 14; i++) s.SendDirect(new AudioSilence(), _ => true);
        s.UpdateAmountToSend(0, 0);
        bool sent = false;
        Assert.True(s.SendDirect(new EndOfAnimation(), _ => sent = true));
        Assert.True(sent);
        Assert.Equal(15, s.FramesStreamed);
    }

    /// <summary>A sink that stands for a robot: it reports played counters, and logs what the stream sends.</summary>
    private sealed class RobotSink : IAnimationSink
    {
        public int FramesPlayed, BytesPlayed;
        public readonly List<string> Log = new();
        public int? AudioFramesPlayed => FramesPlayed;
        public int? AnimBytesPlayed => BytesPlayed;
        public void Face(FaceBitmap bitmap) => Log.Add("face");
        public void Audio(byte[]? mulawFrame) => Log.Add(mulawFrame is null ? "silence" : "sample");
        public void Head(sbyte angleDeg, uint durationMs) => Log.Add("head");
        public void Lift(byte heightMm, uint durationMs) => Log.Add("lift");
        public void AnimationStarted(byte tag) => Log.Add($"start:{tag}");
        public void AnimationEnded() => Log.Add("end");
        public void Body(BodyKeyframe keyframe) => Log.Add("body");
        public void BodyStop() => Log.Add("bodystop");
        public void Lights(LightsKeyframe keyframe) => Log.Add("lights");
        public void BackpackLights(ushort[] leds) => Log.Add("lights");
        public void Event(string eventId) => Log.Add($"event:{eventId}");
        public void Finished(string clipName, bool completed) => Log.Add("finished");
        public int Count(string what) => Log.Count(e => e == what);
    }

    private static AnimationClip SilentClip(uint durationMs, params Keyframe[] frames) => new()
    {
        Name = "silent",
        Keyframes = frames.ToList(),
        Tracks = AnimationTrack.Event,
        DurationMs = durationMs,
    };

    /// <summary>
    /// C15, A18: one engine Update refreshes the budgets and builds frames one at a time while the buffer is empty. With
    /// nothing played, one Update sends exactly the audio budget, 14 audio frames (C9); the 15th frame is built, its
    /// audio message stops the drain, and it waits in the buffer. The stream time steps 33 ms after every built frame
    /// whose drain returned without a send error, the budget-stopped one included (0x0057CA88..0x0057CA9C): 15 x 33.
    /// When the robot reports 14 played, the next Update flushes the leftover (1 audio frame) and builds 14 more, of
    /// which 13 go and the last waits: 28 audio frames, and the stream time 29 x 33.
    /// </summary>
    [Fact]
    public void M3_013_C15_OneUpdateStreamsFramesUntilTheBudgetStopsTheDrain()
    {
        var sink = new RobotSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.Play(SilentClip(10_000, new EventKeyframe(9_000, "late")), 0);
        s.Advance(0);
        Assert.Equal(14, sink.Count("silence"));
        Assert.True(s.Stream.Count > 0, "the 15th frame waits in the buffer");
        Assert.Equal(15 * 33, s.StreamTimeMs);

        s.Advance(1);                                              // nothing played: nothing more goes
        Assert.Equal(14, sink.Count("silence"));
        Assert.Equal(15 * 33, s.StreamTimeMs);                     // and no frame is built while the leftover waits

        sink.FramesPlayed = 14;
        s.Advance(2);
        Assert.Equal(28, sink.Count("silence"));
        Assert.Equal(29 * 33, s.StreamTimeMs);
    }

    /// <summary>A sound source with non-silent samples.</summary>
    private sealed class ToneSource : IAnimationAudioSource
    {
        public short[]? GetPcm(long eventId, float volume) => Enumerable.Repeat((short)8000, 744 * 40).ToArray();
        public string? NameOf(long eventId) => "tone";
    }

    /// <summary>
    /// C9 with C11: an AudioSample costs 745 bytes. With nothing played the byte budget is 8192, so one Update sends 10
    /// sample frames (7450 bytes) and the 11th (8195 &gt; 8192) waits: the byte budget binds before the audio budget.
    /// </summary>
    [Fact]
    public void M3_012_C9_C11_TheByteBudgetBindsSampleFramesAt10()
    {
        var sink = new RobotSink();
        var s = new AnimationScheduler(sink, new Random(1)) { AudioSource = new ToneSource() };
        var clip = new AnimationClip
        {
            Name = "tone",
            Keyframes = new List<Keyframe> { new AudioKeyframe(0, new long[] { 1 }, 1.0f, new[] { 1.0f }, false) },
            Tracks = AnimationTrack.Audio,
            DurationMs = 5_000,
        };
        s.Play(clip, 0);
        s.Advance(0);
        // M5 A15: the audio animation's Update (which starts the sound) runs before frame 0 is built, so frame 0 already
        // carries samples: 745 + StartOfAnimation 2, then 745 per frame
        Assert.Equal(0, sink.Count("silence"));
        int samples = sink.Count("sample");
        Assert.Equal(1 + (8192 - 745 - 2) / 745, samples);
        Assert.True(s.Stream.BytesStreamed <= 8192);

        sink.BytesPlayed = s.Stream.BytesStreamed;               // the robot plays what it has
        sink.FramesPlayed = s.Stream.FramesStreamed;
        s.Advance(1);
        // the leftover frame (745 B) and then 9 more: 8192 - 745 = 7447 leaves room for 9 of 745, not 10
        Assert.Equal(samples + 10, sink.Count("sample"));
    }

    /// <summary>
    /// C4, A16, A20 (M3-015): exactly one audio message per frame, first; StartOfAnimation once, after the first
    /// frame's audio; the keyframes after it. With no frames left and the buffer empty, EndOfAnimation goes directly
    /// and nothing follows it (0x0057CB6E..0x0057CBAE: no AudioSilence after the end).
    /// </summary>
    [Fact]
    public void M3_015_C4_A20_OneAudioMessagePerFrameFirstAndNothingAfterTheEnd()
    {
        var sink = new RobotSink { FramesPlayed = 0 };
        var s = new AnimationScheduler(sink, new Random(1));
        s.Play(SilentClip(66, new EventKeyframe(0, "a"), new EventKeyframe(33, "b"), new EventKeyframe(66, "c")), 0);
        s.Advance(0);
        Assert.Equal(new[] { "silence", "start:1", "event:a", "silence", "event:b", "silence", "event:c", "end" },
                     sink.Log);
        s.Advance(1);
        Assert.Equal("finished", sink.Log[^1]);                    // the next Update completes it (M5 A13), sending nothing
        s.Advance(2);
        Assert.Equal(9, sink.Log.Count);                           // still nothing after the end
    }

    /// <summary>
    /// A16 (0x0057C94E..0x0057CA7A): within a frame, audio, StartOfAnimation, head, lift, event, the face, backpack
    /// lights, body, whatever order the clip lists its keyframes in.
    /// </summary>
    [Fact]
    public void M3_015_A16_TheFrameIsBuiltInTheEnginesTrackOrder()
    {
        var sink = new RobotSink { FramesPlayed = 0 };
        var s = new AnimationScheduler(sink, new Random(1));
        var clip = new AnimationClip
        {
            Name = "order",
            Keyframes = new List<Keyframe>
            {
                new BodyKeyframe(0, 0, "STRAIGHT", 0),
                new LightsKeyframe(0, 0, new float[4], new float[4], new float[4], new float[4], new float[4]),
                new FaceKeyframe(0, ProceduralFacePose.ShippedNeutral()),
                new EventKeyframe(0, "e"),
                new LiftKeyframe(0, 100, 50, 0),
                new HeadKeyframe(0, 100, 5, 0),
            },
            Tracks = AnimationTrack.All,
            DurationMs = 1_000,
        };
        s.Play(clip, 0);
        s.Advance(0);
        Assert.Equal(new[] { "silence", "start:1", "head", "lift", "event:e", "face", "lights", "body" }, sink.Log.Take(8));
    }

    /// <summary>
    /// A16 (7), A17: the procedural face goes only when no face-animation frame was buffered and the face-animation track
    /// is at its end; a faceAnimations keyframe not yet due already holds it back. Here the face pose is at 0 and a
    /// two-frame face animation at 66: frames 0 and 33 carry no face (the pose, the only procedural keyframe, is consumed
    /// on frame 0 while the sprite is pending, M5 C7), 66 and 99 the animation's frames; then the clip has no frames left
    /// and ends (M5 A20), so no procedural face ever goes.
    /// </summary>
    [Fact]
    public void M3_015_A17_APendingFaceAnimationHoldsTheProceduralFaceBack()
    {
        var sink = new RobotSink { FramesPlayed = 0 };
        var s = new AnimationScheduler(sink, new Random(1))
        {
            FaceAnimations = _ => new[] { new FaceBitmap(), new FaceBitmap() },
        };
        s.Play(new AnimationClip
        {
            Name = "faces",
            Keyframes = new List<Keyframe>
            {
                new FaceKeyframe(0, ProceduralFacePose.ShippedNeutral()),
                new FaceAnimationKeyframe(66, "anim"),
            },
            Tracks = AnimationTrack.Face,
            DurationMs = 200,
        }, 0);
        s.Advance(0);
        var frames = new List<List<string>>();
        foreach (var e in sink.Log)
        {
            if (e is "silence" or "sample") frames.Add(new List<string>());
            frames[^1].Add(e);
        }
        Assert.DoesNotContain("face", frames[0]);
        Assert.DoesNotContain("face", frames[1]);
        Assert.Contains("face", frames[2]);
        Assert.Contains("face", frames[3]);
        Assert.Equal(4, frames.Count);
        Assert.Equal(2, sink.Count("face"));
    }

    /// <summary>
    /// A24, A25 (M3-013): a cancel does not clear the send buffer; the next Update flushes what the cancelled clip left,
    /// within the budget, and no EndOfAnimation follows because startSent is cleared.
    /// </summary>
    [Fact]
    public void M3_013_A24_A25_ACancelledClipsLeftoversAreFlushedWithNoEnd()
    {
        var sink = new RobotSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.Play(SilentClip(10_000, new EventKeyframe(9_000, "late")), 0);
        s.Advance(0);
        Assert.True(s.Stream.Count > 0, "a frame waits in the buffer");
        Assert.True(s.Stop());
        Assert.True(s.Stream.Count > 0, "the cancel keeps the buffer");
        sink.FramesPlayed = 14;
        s.Advance(1);
        Assert.Equal(15, sink.Count("silence"));                  // the leftover went
        Assert.Equal(0, s.Stream.Count);
        Assert.Equal(0, sink.Count("end"));
    }

    /// <summary>
    /// A12, A24, A28, gap4 L8 (M3-013, M5 fix B1): with the ProceduralLive idle on top of the stack, a cancelled clip's
    /// leftovers are dropped, not flushed: a top other than Count goes straight to the idle (0x0057D03A..0x0057D04C), which
    /// neither flushes nor sends an End, and the live idle's InitStream(live, 0xFF) (the previous idle was not the live one,
    /// 0x0057D3F0..0x0057D3FE) drops them (ClearSendBuffer, A12). No EndOfAnimation is sent.
    /// </summary>
    [Fact]
    public void M3_013_A13_A29_A12_WithTheLiveStreamActiveACancelledClipsLeftoversAreDropped()
    {
        var sink = new RobotSink();
        var s = new AnimationScheduler(sink, new Random(1));
        Assert.True(s.StreamLive(new HeadKeyframe(0, 100, 5, 0), 0));      // ProceduralLive on top
        sink.FramesPlayed = 1;
        s.Play(SilentClip(10_000, new EventKeyframe(9_000, "late")), 0);
        s.Advance(0);
        Assert.True(s.Stream.Count > 0, "a frame of the clip waits in the buffer");
        int silences = sink.Count("silence");
        Assert.True(s.Stop());
        Assert.True(s.Stream.Count > 0, "the cancel itself keeps the buffer (A24)");

        sink.FramesPlayed = 100;                                               // room enough to flush, were it flushed
        sink.Log.Clear();
        s.Advance(1);
        Assert.Empty(sink.Log);                                                // dropped, not sent
        Assert.Equal(0, s.Stream.Count);
        Assert.Equal(0, sink.Count("end"));
        Assert.True(silences > 0);
    }

    /// <summary>A12: a replacement's InitStream drops what the replaced clip left buffered (ClearSendBuffer, 0x0057B7CE).</summary>
    [Fact]
    public void M3_013_A12_AReplacementDropsTheLeftovers()
    {
        var sink = new RobotSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.Play(SilentClip(10_000, new EventKeyframe(9_000, "late")), 0);
        s.Advance(0);
        Assert.True(s.Stream.Count > 0);
        s.Play(SilentClip(10_000, new EventKeyframe(9_000, "late")), 1);
        Assert.Equal(0, s.Stream.Count);
    }

    /// <summary>A12, A13: an empty clip (no keyframes, no length) has endSent from InitStream and completes sending nothing.</summary>
    [Fact]
    public void M3_013_A12_AnEmptyClipSendsNothing()
    {
        var sink = new RobotSink();
        var s = new AnimationScheduler(sink, new Random(1));
        var done = s.Play(new AnimationClip { Name = "empty", Keyframes = new List<Keyframe>(), DurationMs = 0 }, 0)!;
        s.Advance(0);
        Assert.Equal(AnimationEndReason.Completed, done.Completion.Result);
        Assert.Equal(new[] { "finished" }, sink.Log);
    }

    /// <summary>C14, 0x0057BFAE: a send that fails stops the drain and is not counted; the message stays at the front.</summary>
    [Fact]
    public void M3_012_C14_AFailedSendIsNotCounted()
    {
        var s = new StreamSendBuffer();
        s.UpdateAmountToSend(0, 0);
        Assert.False(s.SendDirect(new AudioSilence(), _ => false));
        Assert.Equal((0, 0), (s.BytesStreamed, s.FramesStreamed));
        s.Buffer(null, 1, true, () => false);
        Assert.Equal(StreamSendBuffer.DrainResult.SendError, s.Drain());
        Assert.Equal(1, s.Count);
        Assert.Equal((0, 0), (s.BytesStreamed, s.FramesStreamed));
    }

    /// <summary>
    /// M3-017 (policy) through the engine's budget (C9, C14): a stand-alone Play with a robot that reports nothing played
    /// sends exactly what the byte budget allows, 10 frames of 745 bytes, and then gives up after the stall timeout
    /// instead of sending past the budget.
    /// </summary>
    [Fact]
    public void M3_017_C9_APlayNeverSendsPastTheBudget()
    {
        var sent = new List<RobotMessage>();
        var audio = new CozmoAudio(sent.Add) { PlayedFrames = () => 0, PlayedBytes = () => 0 };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        audio.Play(CozmoAudio.Tone(440, TimeSpan.FromSeconds(1)));
        Assert.Equal(8192 / 745, sent.OfType<AudioSample>().Count());
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// M3-017 through the budget with a robot that plays: every frame goes, and at no point are more than 14 frames or
    /// 8192 bytes unplayed (C9).
    /// </summary>
    [Fact]
    public void M3_017_C9_APlayFollowsTheRobotAndKeepsWithinBothBudgets()
    {
        var sent = new List<RobotMessage>();
        int played = 0, maxUnplayed = 0;
        var audio = new CozmoAudio(m => { lock (sent) { sent.Add(m); maxUnplayed = Math.Max(maxUnplayed, sent.Count - Volatile.Read(ref played)); } })
        {
            PlayedFrames = () => Volatile.Read(ref played),
            PlayedBytes = () => Volatile.Read(ref played) * 745,
        };
        var stop = false;
        var robot = new Thread(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                Thread.Sleep(10);
                lock (sent) if (played < sent.Count) Interlocked.Increment(ref played);
            }
        }) { IsBackground = true };
        robot.Start();
        var pcm = CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(600));
        audio.Play(pcm);
        Volatile.Write(ref stop, true);
        robot.Join();
        Assert.Equal(CozmoAudio.ToFrames(pcm).Count, sent.Count);
        Assert.True(maxUnplayed <= 8192 / 745, $"{maxUnplayed} frames were unplayed at once");
    }

    // ================================================================== camera: M3-001, M3-002, M3-003, M3-005, M3-018, M3-020

    private static ImageChunk Chunk(uint imageId, byte chunkId, byte[] data, byte total = 0, byte encoding = 8) => new()
    {
        ImageId = imageId, ChunkId = chunkId, ImageChunkCount = total, ImageEncoding = encoding,
        ImageResolution = 4, Data = data,
    };

    /// <summary>A1/H1: HandleImageChunk exits unless robot+0x29 (SyncTimeAck received): chunks before it never reach AddChunk.</summary>
    [Fact]
    public void M3_002_A1_ChunksBeforeSyncTimeAckAreIgnored()
    {
        bool synced = false;
        var cam = new CozmoCamera { TimeSynced = () => synced };
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        cam.Handle(Chunk(1, 0, new byte[] { 0, 1 }, total: 1));
        Assert.Empty(frames);
        Assert.Equal(0, cam.ChunksReceived);
        Assert.Equal(1, cam.ChunksIgnoredBeforeSync);
        synced = true;
        cam.Handle(Chunk(2, 0, new byte[] { 0, 1 }, total: 1));
        Assert.Single(frames);
    }

    /// <summary>R10: the constructor's id is 0xFFFFFFFF with valid 0, so a first chunk carrying that id takes the same-image path and is never delivered.</summary>
    [Fact]
    public void M3_002_R10_AFirstChunkWithTheConstructorsIdIsInvalid()
    {
        var cam = new CozmoCamera();
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        cam.Handle(Chunk(0xFFFFFFFF, 0, new byte[] { 0, 1 }, total: 1));
        Assert.Empty(frames);
        cam.Handle(Chunk(3, 0, new byte[] { 0, 1 }, total: 1));
        Assert.Single(frames);
    }

    /// <summary>R7/R10: the last chunk is chunkCount - 1 == chunkId in int arithmetic, so a count of 0 never completes, even at chunk 255.</summary>
    [Fact]
    public void M3_002_R10_AChunkCountOf0NeverCompletes()
    {
        var cam = new CozmoCamera();
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        for (int i = 0; i <= 255; i++) cam.Handle(Chunk(5, (byte)i, new byte[] { 1 }, total: 0));
        Assert.Empty(frames);
    }

    /// <summary>R7: a previous timestamp above the current one invalidates; equal timestamps are accepted (prev &lt;= cur passes).</summary>
    [Fact]
    public void M3_002_R7_AnEqualTimestampIsAccepted()
    {
        var cam = new CozmoCamera();
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        var a = Chunk(1, 0, new byte[] { 0, 1 }, total: 1); a.FrameTimestamp = 500;
        var b = Chunk(2, 0, new byte[] { 0, 1 }, total: 1); b.FrameTimestamp = 500;
        cam.Handle(a);
        cam.Handle(b);
        Assert.Equal(2, frames.Count);
        Assert.Equal(500u, frames[1].Timestamp);
    }

    /// <summary>
    /// A3: at most 3 completed images per event time go on to vision; the 4th and later are dropped with a warning. A
    /// new event time resets the count.
    /// </summary>
    [Fact]
    public void M3_005_A3_AtMostThreeImagesPerEventTimeReachVision()
    {
        double t = 1.0;
        var cam = new CozmoCamera { EventTime = () => t };
        int received = 0, toVision = 0;
        var log = new List<string>();
        cam.FrameReceived += _ => received++;
        cam.FrameForVision += _ => toVision++;
        cam.Log += log.Add;
        for (uint id = 1; id <= 5; id++) cam.Handle(Chunk(id, 0, new byte[] { 0, 1 }, total: 1));
        Assert.Equal(5, received);
        Assert.Equal(3, toVision);
        Assert.Equal(2, log.Count);
        Assert.Contains("4th image", log[0]);
        t = 1.06;
        cam.Handle(Chunk(9, 0, new byte[] { 0, 1 }, total: 1));
        Assert.Equal(4, toVision);
    }

    private static string Sha16(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant()[..16];

    private static byte[] Patched(byte[] header, int h, int w)
    {
        var c = (byte[])header.Clone();
        c[0x5E] = (byte)(h >> 8); c[0x5F] = (byte)h; c[0x60] = (byte)(w >> 8); c[0x61] = (byte)w;
        return c;
    }

    /// <summary>
    /// A13: gray header 324 bytes (table 0x00C48C40, SHA-256 prefix c44b69c9f614304a as the engine stores it, 296 x
    /// 400), SOF0 at 0x59 with 1 component sampled 0x11; colour 334 bytes (0x00C48D84, 106a14ba4dd23964), SOF0 at 0x59
    /// with 3 components, Y sampled 0x21 (2 x 1).
    /// </summary>
    [Fact]
    public void M3_001_A13_TheHeaderTablesAreTheEngines()
    {
        var gray = MiniJpeg.HeaderTable(color: false);
        var colour = MiniJpeg.HeaderTable(color: true);
        Assert.Equal(324, gray.Length);
        Assert.Equal(334, colour.Length);
        Assert.Equal("c44b69c9f614304a", Sha16(Patched(gray, 296, 400)));
        Assert.Equal("106a14ba4dd23964", Sha16(Patched(colour, 240, 320)));
        Assert.Equal(new byte[] { 0xFF, 0xC0 }, gray[0x59..0x5B]);
        Assert.Equal(new byte[] { 0xFF, 0xC0 }, colour[0x59..0x5B]);
        Assert.Equal(1, gray[0x62]);
        Assert.Equal(0x11, gray[0x64]);
        Assert.Equal(3, colour[0x62]);
        Assert.Equal(0x21, colour[0x64]);
    }

    /// <summary>A12: height big-endian at 0x5E/0x5F and width at 0x60/0x61; colour is built at half width (A9: w / 2).</summary>
    [Fact]
    public void M3_001_A12_TheSizeFieldsAreBigEndian()
    {
        var j = MiniJpeg.ToJpeg(new byte[] { 1, 2, 3 }, 160, 240, MiniJpeg.EncodingJpegMinimizedColor);
        Assert.Equal(new byte[] { 0x00, 0xF0, 0x00, 0xA0 }, j[0x5E..0x62]);
        var g = MiniJpeg.ToJpeg(new byte[] { 0, 2, 3 }, 320, 240, MiniJpeg.EncodingJpegMinimizedGray);
        Assert.Equal(new byte[] { 0x00, 0xF0, 0x01, 0x40 }, g[0x5E..0x62]);
    }

    /// <summary>
    /// A12: trailing 0xFF stripped, the first (flag) byte dropped, 0x00 stuffed after each 0xFF, FF D9 appended; with
    /// fewer than 2 bytes left the copy is skipped.
    /// </summary>
    [Fact]
    public void M3_003_A12_StripDropStuffAndEoi()
    {
        int hdr = MiniJpeg.HeaderLength(color: false);
        var j = MiniJpeg.ToJpeg(new byte[] { 0x01, 0xAA, 0xFF, 0xBB, 0xFF, 0xFF }, 320, 240, 8);
        Assert.Equal(new byte[] { 0xAA, 0xFF, 0x00, 0xBB, 0xFF, 0xD9 }, j[hdr..]);
        var one = MiniJpeg.ToJpeg(new byte[] { 0x05, 0xFF }, 320, 240, 8);
        Assert.Equal(new byte[] { 0xFF, 0xD9 }, one[hdr..]);
    }

    /// <summary>
    /// M3-020 (policy, MD2): an empty or all-0xFF payload, where the engine's strip reads data[-1], is a decode failure.
    /// </summary>
    [Fact]
    public void M3_020_AnEmptyOrAll0xFFPayloadIsADecodeFailure()
    {
        Assert.Empty(MiniJpeg.ToJpeg(Array.Empty<byte>(), 320, 240, 8));
        Assert.Empty(MiniJpeg.ToJpeg(new byte[] { 0xFF, 0xFF, 0xFF }, 320, 240, 8));
        var cam = new CozmoCamera();
        CameraFrame? f = null;
        cam.FrameReceived += x => f = x;
        cam.Handle(Chunk(1, 0, new byte[] { 0xFF, 0xFF }, total: 1));
        Assert.NotNull(f);
        Assert.False(f!.TryDecodeGray(out _, out var error));
        Assert.Contains("M3-020", error);
    }

    private static byte[] Jpeg(int w, int h, int channels, Func<int, int, int, byte> px)
    {
        var data = new byte[w * h * channels];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) for (int c = 0; c < channels; c++) data[(y * w + x) * channels + c] = px(x, y, c);
        using var o = new MemoryStream();
        new StbImageWriteSharp.ImageWriter().WriteJpg(data, w, h,
            channels == 1 ? StbImageWriteSharp.ColorComponents.Grey : StbImageWriteSharp.ColorComponents.RedGreenBlue, o, 95);
        return o.ToArray();
    }

    /// <summary>A11: a decoded frame must be 240 rows by 320 columns, otherwise BadDecode.</summary>
    [Fact]
    public void M3_001_A11_OnlyA240By320DecodeIsAccepted()
    {
        var small = new CameraFrame { Encoding = 5, Width = 320, Height = 240, Jpeg = Jpeg(100, 50, 1, (x, y, c) => 128) };
        Assert.False(small.TryDecodeGray(out _, out var error));
        Assert.Contains("BadDecode", error);
        var right = new CameraFrame { Encoding = 5, Width = 320, Height = 240, Jpeg = Jpeg(320, 240, 1, (x, y, c) => 128) };
        Assert.True(right.TryDecodeGray(out var img, out _));
        Assert.Equal((320, 240), (img!.Width, img.Height));
    }

    /// <summary>A7: IsColor is true for 2, 3, 4, 6, 7, 9 and anything above 8; false for 1, 5 and 8.</summary>
    [Fact]
    public void M3_018_A7_IsColor()
    {
        foreach (byte e in new byte[] { 2, 3, 4, 6, 7, 9, 10, 200, 255 }) Assert.True(EncodedImageDecoder.IsColor(e), $"encoding {e}");
        foreach (byte e in new byte[] { 1, 5, 8 }) Assert.False(EncodedImageDecoder.IsColor(e), $"encoding {e}");
    }

    /// <summary>
    /// A10: Resize is cv::resize with INTER_LINEAR. For the engine's 160 → 320 columns and 240 → 240 rows, OpenCV's
    /// pixel-centre mapping gives dst[0] = p[0], dst[2k] = (p[k-1] + 3 p[k] + 2) &gt;&gt; 2, dst[2k+1] = (3 p[k] + p[k+1] +
    /// 2) &gt;&gt; 2 and dst[319] = p[159], every row copied as it is.
    /// </summary>
    [Fact]
    public void M3_018_A10_InterLinear160To320()
    {
        var src = new byte[160 * 240];
        var rnd = new Random(7);
        rnd.NextBytes(src);
        var dst = EncodedImageDecoder.ResizeLinear(src, 160, 240, 1, 320, 240);
        for (int y = 0; y < 240; y++)
        {
            byte P(int k) => src[y * 160 + k];
            for (int x = 0; x < 320; x++)
            {
                int k = x / 2;
                int expected = x == 0 ? P(0)
                             : x == 319 ? P(159)
                             : x % 2 == 0 ? (P(k - 1) + 3 * P(k) + 2) >> 2
                             : (3 * P(k) + P(k + 1) + 2) >> 2;
                Assert.Equal(expected, dst[y * 320 + x]);
            }
        }
    }

    /// <summary>
    /// A9, A10, A11, A25: a colour frame is a half-width JPEG decoded as colour, resized to 320 x 240; Save writes it as a
    /// JPEG of that size (quality 90). A gray decode of it is resized the same way (A8 case 9).
    /// </summary>
    [Fact]
    public void M3_018_A9_A25_AColourFrameDecodesTo320By240()
    {
        var f = new CameraFrame
        {
            Encoding = MiniJpeg.EncodingJpegMinimizedColor, Width = 320, Height = 240, JpegWidth = 160, IsColor = true,
            Jpeg = Jpeg(160, 240, 3, (x, y, c) => c == 0 ? (byte)200 : (byte)30),
        };
        Assert.True(f.TryDecodeRgb(out var rgb, out _));
        Assert.Equal(320 * 240 * 3, rgb!.Length);
        Assert.True(rgb[0] > rgb[2] + 100, "red stays red");
        Assert.True(f.TryDecodeGray(out var gray, out _));
        Assert.Equal((320, 240), (gray!.Width, gray.Height));
        var saved = StbImageSharp.ImageResult.FromMemory(f.PresentationJpeg(), StbImageSharp.ColorComponents.RedGreenBlue);
        Assert.Equal((320, 240), (saved.Width, saved.Height));
        Assert.Equal(90, EncodedImageDecoder.SaveQuality);

        var grayFrame = new CameraFrame { Encoding = 8, Jpeg = new byte[] { 1, 2 }, RawPayload = new byte[] { 3 } };
        Assert.Equal(grayFrame.Jpeg, grayFrame.PresentationJpeg());       // A25: encoding 8 writes the rebuilt JPEG
        var other = new CameraFrame { Encoding = 1, Jpeg = new byte[] { 1, 2 }, RawPayload = new byte[] { 3 } };
        Assert.Equal(other.RawPayload, other.PresentationJpeg());          // any other encoding: the raw bytes
    }

    // ================================================================== connection and camera settings: M3-019, M3-021..M3-024

    private sealed class FakePort : IEngineTransport
    {
        public readonly List<byte[]> Sent = new();
        public bool TimedOut { get; set; }
        public event Action<ReceiverEvent>? Received;
        public void Start() { }
        public void Connect(IPAddress ip, bool isSimulated) { }
        public void Disconnect(IPEndPoint address) { }
        public void SendData(byte[] clad) { lock (Sent) Sent.Add(clad); }
        public void Raise(ReceiverMarker m, IPEndPoint? a, byte[]? d = null) => Received?.Invoke(new ReceiverEvent(m, a, d));
        public List<RobotMessage> Messages() { lock (Sent) return Sent.Select(b => RobotMessage.Parse(b)).ToList(); }
    }

    private sealed class Rig : IDisposable
    {
        public long NowNs = 1_000_000_000;
        public readonly FakePort Port = new();
        public readonly CozmoRobot Robot;
        public CozmoEngine Engine => Robot.Engine;
        public static readonly IPAddress RobotIp = IPAddress.Parse("172.31.1.1");
        public static readonly IPEndPoint RobotEp = new(RobotIp, 5551);
        public const string ShippedFw = "{\"version\": 2381, \"time\": 1546972025, \"build\": \"DEVELOPMENT\"}";

        public Rig() => Robot = CozmoRobot.CreateForTest(Port, () => NowNs, new CozmoEngineOptions { BlockPoolPath = "" });
        public void Tick() { NowNs += 60_000_000; Engine.Tick(); }
        public void Data(RobotMessage m) => Port.Raise(ReceiverMarker.Data, RobotEp, m.ToBytes());

        public void ToSuccess(string fw = ShippedFw)
        {
            Engine.ConnectToRobot(RobotIp);
            Tick();
            Port.Raise(ReceiverMarker.OnConnected, RobotEp);
            Tick();
            Data(new RobotAvailable { SerialNumberHead = 0x1234, HwVersion = 5 });
            Data(new FirmwareVersion { RobotId = 1, Signature = Encoding.UTF8.GetBytes(fw) });
            Tick();
            Data(new ManufacturingID { SerialNumber = 0xABCD, BodyHwVersion = 7, BodyColor = 2 });
            Tick();
        }

        public void Dispose() => Robot.Dispose();
    }

    /// <summary>
    /// 1h, 1i, 1o, A17, A18 (M3-019 policy MD1, M3-022): at a Success response the VisionComponent's subscriber runs after
    /// Robot::SyncTime's sends and before TracePrinter's: it queues the NV CameraCalib read (tag 0x80000001) and sends
    /// SetCameraParams {f32 0.0, u16 0, bool 1}, whose request goes out after it. A24/3a (M3-023): no EnableColorImages at
    /// connection.
    /// </summary>
    [Fact]
    public void M3_019_M3_022_M3_023_1h_TheConnectionSendsSetCameraParamsAndQueuesTheCalibrationRead()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        var sent = rig.Port.Messages();
        int sync = sent.FindIndex(m => m is SyncTime);
        int camera = sent.FindIndex(m => m is SetCameraParams);
        int nv = sent.FindIndex(m => m is NVCommand);
        int trace = sent.FindIndex(m => m is SetAppRunID);
        Assert.True(sync >= 0 && camera > sync && nv > camera && trace > camera, $"order: sync {sync}, camera {camera}, nv {nv}, trace {trace}");
        Assert.Equal(new byte[] { 0x57, 0, 0, 0, 0, 0, 0, 1 }, sent[camera].ToBytes());
        var read = (NVCommand)sent[nv];
        Assert.Equal(0x80000001u, read.Tag);
        Assert.DoesNotContain(sent, m => m is EnableColorImages);
        Assert.Single(sent.OfType<SetCameraParams>());
    }

    private static byte[] Calibration56()
    {
        var w = new CladWriter();
        foreach (var f in new[] { 290f, 291f, 160f, 120f, 0f }) w.F32(f);
        w.U16(240); w.U16(320);
        for (int i = 0; i < 8; i++) w.F32(0.01f * (i + 1));
        return w.ToArray();
    }

    /// <summary>
    /// 1j, 2a, 2d (M3-022): vision starts disabled (+0x48 = 0) and the NV callback enables it on all three paths:
    /// NVResult ≠ 0, a size other than 56 (MakeWordAligned(CameraCalibration::Size())), and success, which also installs
    /// the calibration as read (the robot+0x24 &lt;= 6 zeroing is MISSING and not applied) and hands it to vision.
    /// </summary>
    [Theory]
    [InlineData(-1, 56, false)]
    [InlineData(0, 40, false)]
    [InlineData(0, 56, true)]
    public void M3_022_1j_TheCalibrationCallbackEnablesVisionOnEveryPath(int result, int size, bool installs)
    {
        using var rig = new Rig();
        using var vision = new Cozmo.Robot.Vision.VisionSystem(rig.Robot) { Enabled = false };
        rig.ToSuccess();
        Assert.False(rig.Robot.CameraSettings.VisionEnabled);
        var data = Calibration56()[..Math.Min(size, 56)];
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = (sbyte)result, Length = data.Length, Data = data });
        rig.Tick();
        Assert.True(rig.Robot.CameraSettings.VisionEnabled);
        Assert.True(vision.Enabled);
        if (installs)
        {
            var cal = rig.Robot.CameraSettings.Calibration!;
            Assert.Equal((290.0, 291.0, 320, 240), (cal.FocalLengthX, cal.FocalLengthY, cal.Columns, cal.Rows));
            Assert.Equal(0.01, cal.DistortionCoefficients[0], 6);
            Assert.Same(cal, vision.Calibration);
        }
        else
        {
            Assert.Null(rig.Robot.CameraSettings.Calibration);
            Assert.Null(vision.Calibration);
        }
    }

    private static DefaultCameraParams Defaults(float maxGain, float gain, ushort min, ushort max) => new()
    {
        Field0 = maxGain, Field1 = gain, Field2 = min, Field3 = max,
        Field4 = Enumerable.Range(0, 17).Select(i => (byte)(i * 10)).ToArray(),
    };

    private static List<SetCameraParams> CameraParamsAfter(Rig rig, int from)
        => rig.Port.Messages().Skip(from).OfType<SetCameraParams>().ToList();

    /// <summary>
    /// 1k, A19, A22 (M3-021): DefaultCameraParams, with no time-sync gate, and 16 in [min, max]: SetCameraSettings(16,
    /// gain) first, sending {gain, 16, false}; then the limits max, min, 0.1, maxGain and the gamma table installed.
    /// </summary>
    [Fact]
    public void M3_021_1k_DefaultCameraParamsSetsTheInitialExposureThenInstallsTheLimits()
    {
        using var rig = new Rig();
        rig.ToSuccess();                                          // no SyncTimeAck: the handler is not gated
        int before = rig.Port.Messages().Count;
        rig.Data(Defaults(3.0f, 1.5f, 1, 50));
        rig.Tick();
        var sent = Assert.Single(CameraParamsAfter(rig, before));
        Assert.Equal((1.5f, (ushort)16, false), (sent.Gain, sent.ExposureMs, sent.AutoExposureEnabled));
        var s = rig.Robot.CameraSettings;
        Assert.Equal((50, 1, 0.1f, 3.0f), (s.MaxExposureMs, s.MinExposureMs, s.MinGain, s.MaxGain));
        Assert.Equal(17, s.GammaTable.Length);
        Assert.Equal((16, 1.5f), s.NextCameraParams!.Value);
    }

    /// <summary>A19/1k: 16 outside [msg+8, msg+0xA] is BadInitialExposureTime: nothing sent and nothing installed.</summary>
    [Fact]
    public void M3_021_1k_AnInitialExposureOutsideTheRangeDoesNothing()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        int before = rig.Port.Messages().Count;
        rig.Data(Defaults(3.0f, 1.5f, 20, 50));
        rig.Tick();
        Assert.Empty(CameraParamsAfter(rig, before));
        var s = rig.Robot.CameraSettings;
        Assert.Equal((66, 1, 0.1f, 4.0f), (s.MaxExposureMs, s.MinExposureMs, s.MinGain, s.MaxGain));
    }

    /// <summary>
    /// 1e, 1k, 1l (M3-021): the first SetCameraSettings is checked against the constructor's limits (gain 0.1..4.0), so a
    /// gain of 5.0 sends nothing, but the robot's limits (maxGain 8.0) are still installed. A min of 0 is stored as 1 (1c).
    /// </summary>
    [Fact]
    public void M3_021_1l_TheFirstSettingsAreCheckedAgainstTheConstructorLimits()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        int before = rig.Port.Messages().Count;
        rig.Data(Defaults(8.0f, 5.0f, 0, 60));
        rig.Tick();
        Assert.Empty(CameraParamsAfter(rig, before));
        var s = rig.Robot.CameraSettings;
        Assert.Equal((60, 1, 8.0f), (s.MaxExposureMs, s.MinExposureMs, s.MaxGain));
    }

    /// <summary>1a, 1l, A20 (M3-021): exposure valid when min &lt;= e &lt;= max (1..66), gain when 0.1 &lt;= g &lt;= 4.0, NaN invalid.</summary>
    [Fact]
    public void M3_021_1l_SetCameraSettingsRangeChecks()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        var s = rig.Robot.CameraSettings;
        Assert.False(s.SetCameraSettings(67, 2.0f));
        Assert.False(s.SetCameraSettings(0, 2.0f));
        Assert.False(s.SetCameraSettings(16, 4.01f));
        Assert.False(s.SetCameraSettings(16, 0.09f));
        Assert.False(s.SetCameraSettings(16, float.NaN));
        int before = rig.Port.Messages().Count;
        CurrentCameraParams? info = null;
        s.CurrentCameraParamsChanged += p => info = p;
        Assert.True(s.SetCameraSettings(66, 4.0f));
        Assert.True(s.SetCameraSettings(1, 0.1f));
        Assert.Equal(2, CameraParamsAfter(rig, before).Count);
        Assert.Equal(new CurrentCameraParams(0.1f, 1, true), info);      // auto-exposure +0x329 starts 1 (1n)
    }

    /// <summary>A24, 3a (M3-023): EnableColorImages stores the flag and sends EnableColorImages {b}.</summary>
    [Fact]
    public void M3_023_A24_EnableColorImagesStoresAndSends()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        Assert.False(rig.Robot.CameraSettings.ColorImagesEnabled);
        rig.Robot.CameraSettings.EnableColorImages(true);
        Assert.True(rig.Robot.CameraSettings.ColorImagesEnabled);
        Assert.True(Assert.IsType<EnableColorImages>(rig.Port.Messages()[^1]).Enable);
    }

    /// <summary>C1 (M3-024): "sim" null in the firmware JSON: physical robot, output source 2 (PlayOnRobot); otherwise 1.</summary>
    [Theory]
    [InlineData(Rig.ShippedFw, RobotAudioOutputSource.PlayOnRobot, true)]
    [InlineData("{\"version\": 2381, \"time\": 1546972025, \"sim\": null}", RobotAudioOutputSource.PlayOnRobot, true)]
    [InlineData("{\"version\": 2381, \"time\": 1546972025, \"sim\": true}", RobotAudioOutputSource.PlayOnDevice, false)]
    public void M3_024_C1_TheAudioOutputSourceComesFromSim(string fw, RobotAudioOutputSource source, bool physical)
    {
        using var rig = new Rig();
        rig.ToSuccess(fw);
        Assert.Equal(source, rig.Engine.Robot!.AudioOutputSource);
        Assert.Equal(physical, rig.Engine.Robot.IsPhysicalRobot);
    }

    /// <summary>C10 (M3-012): the played counters are written only by the AnimationState handler, and only once time synced.</summary>
    [Fact]
    public void M3_012_C10_ThePlayedCountersComeOnlyFromATimeSyncedAnimationState()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Data(new AnimationState { NumAnimBytesPlayed = 500, NumAudioFramesPlayed = 5, Tag = 3 });
        rig.Tick();
        Assert.Equal((0, 0), (rig.Engine.Robot!.NumAnimBytesPlayed, rig.Engine.Robot.NumAudioFramesPlayed));
        rig.Data(new SyncTimeAck());
        rig.Data(new AnimationState { NumAnimBytesPlayed = 500, NumAudioFramesPlayed = 5, Tag = 3 });
        rig.Tick();
        Assert.Equal((500, 5), (rig.Engine.Robot.NumAnimBytesPlayed, rig.Engine.Robot.NumAudioFramesPlayed));
    }

    /// <summary>CD12, C15 (M3-013): Robot::Update runs the streamer only while synced and ready to stream, once per tick.</summary>
    [Fact]
    public void M3_013_CD12_TheEngineTickRunsTheStreamerOnlyWhileStreamingIsOpen()
    {
        using var rig = new Rig();
        int updates = 0;
        rig.Engine.AnimationStreamerUpdate = () => updates++;
        rig.ToSuccess();
        rig.Tick();
        Assert.Equal(0, updates);                                  // not time synced, no full state yet
        rig.Data(new SyncTimeAck());
        rig.Data(new RobotState { Timestamp = 10, PoseOriginId = 1 });
        rig.Tick();
        Assert.Equal(1, updates);
        rig.Tick();
        Assert.Equal(2, updates);
    }

    /// <summary>
    /// CD12, C15, A20 (M3-013, M3-014), the production wiring: the engine's own 60 ms thread (StartProduction) runs
    /// Robot::Update, which runs the streamer (AnimationStreamerUpdate → CozmoAnimations.EngineUpdate →
    /// AnimationScheduler.Advance) while streaming is open; no 30 Hz tick loop thread is started. A clip streams
    /// within the budget the robot's AnimationState reports, completes, and ends with EndOfAnimation, with nothing
    /// after it.
    /// </summary>
    [Fact]
    public void M3_013_CD12_OnAProductionEngineTheEngineTickStreamsAClipToItsEnd()
    {
        var port = new FakePort();
        using var robot = CozmoRobot.CreateForTest(port, EngineTickRunner.HostNowNs, new CozmoEngineOptions { BlockPoolPath = "" });
        robot.Engine.StartProduction();
        Assert.True(robot.Animations.EngineDriven);
        void Data(RobotMessage m) => port.Raise(ReceiverMarker.Data, Rig.RobotEp, m.ToBytes());
        bool Sent(Func<RobotMessage, bool> p) => port.Messages().Any(p);

        robot.Engine.ConnectToRobot(Rig.RobotIp);
        Assert.True(SpinWait.SpinUntil(() => robot.Engine.ConnectionState == 1, 3000), "no connect");
        port.Raise(ReceiverMarker.OnConnected, Rig.RobotEp);
        Assert.True(SpinWait.SpinUntil(() => robot.Engine.ConnectionState == 2, 3000), "not connected");
        Data(new RobotAvailable { SerialNumberHead = 0x1234, HwVersion = 5 });
        Data(new FirmwareVersion { RobotId = 1, Signature = Encoding.UTF8.GetBytes(Rig.ShippedFw) });
        Assert.True(SpinWait.SpinUntil(() => Sent(m => m is GetManufacturingInfo), 3000), "no GetManufacturingInfo");
        Data(new ManufacturingID { SerialNumber = 0xABCD, BodyHwVersion = 7, BodyColor = 2 });
        Assert.True(SpinWait.SpinUntil(() => Sent(m => m is SyncTime), 3000), "no SyncTime");
        Data(new SyncTimeAck());
        Data(new RobotState { Timestamp = 10, PoseOriginId = 1 });
        Assert.True(SpinWait.SpinUntil(() => robot.AnimationStreamingOpen, 3000), "streaming never opened");

        int before = port.Messages().Count;
        var clip = new AnimationClip
        {
            Name = "head",
            Keyframes = new List<Keyframe> { new HeadKeyframe(0, 100, 10, 0) },
            Tracks = AnimationTrack.Head,
            DurationMs = 300,
        };
        var done = robot.Animations.Play(clip)!;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        uint ts = 20;
        while (!done.IsCompleted && sw.Elapsed < TimeSpan.FromSeconds(10))
        {
            // the robot plays everything it was given: its AnimationState reports what the stream sent
            var sent = port.Messages().Skip(before).ToList();
            int frames = sent.Count(m => m is AudioSample or AudioSilence or EndOfAnimation);
            int bytes = sent.Where(m => (byte)m.Id >= 0x8E && (byte)m.Id <= 0x9B).Sum(m => m.ToBytes().Length);
            Data(new AnimationState { Timestamp = ts++, NumAudioFramesPlayed = frames, NumAnimBytesPlayed = bytes, Tag = 1 });
            Thread.Sleep(30);
        }
        Assert.True(done.IsCompleted, "the clip never completed");
        Assert.Equal(AnimationEndReason.Completed, done.Result);

        var after = port.Messages().Skip(before).ToList();
        int start = after.FindIndex(m => m is StartOfAnimation s && s.AnimId == 1);
        int end = after.FindIndex(m => m is EndOfAnimation);
        Assert.True(start >= 0 && end > start, $"start {start}, end {end}");
        Assert.Contains(after.Take(end), m => m is Protocol.HeadAngle);
        Assert.Equal(1, after.Count(m => m is EndOfAnimation));
        Assert.DoesNotContain(after.Skip(end + 1), m => m is AudioSample or AudioSilence);    // A20: nothing after the end

        var ticker = typeof(CozmoAnimations).GetField("_ticker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Null(ticker.GetValue(robot.Animations));                                        // no 30 Hz tick loop
    }
}
