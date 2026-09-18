using System.Globalization;
using System.Text.RegularExpressions;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
using FaceMsg = Cozmo.Protocol.FaceImage;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The M3 device layer: camera reassembly, the face display codec and the audio pipeline.
/// Everything here is deterministic; the capture replay uses the frames a real firmware-2457 robot sent.
/// </summary>
public class DeviceTests
{
    // ---------------------------------------------------------------- audio

    [Fact]
    public void MuLawMatchesTheReferenceEndpoints()
    {
        Assert.Equal(0xFF, MuLaw.Encode(0));              // silence is all ones
        Assert.Equal(0x80, MuLaw.Encode(short.MaxValue)); // full positive scale
        Assert.Equal(0x00, MuLaw.Encode(short.MinValue)); // full negative scale
        Assert.Equal(0x7F, MuLaw.Encode(-1));
        Assert.Equal(0, MuLaw.Decode(0xFF));
        Assert.True(MuLaw.Decode(0x80) > 30000);
        Assert.True(MuLaw.Decode(0x00) < -30000);
    }

    [Fact]
    public void MuLawEncodingIsIdempotentOverEveryCode()
    {
        // Decoding a code and re-encoding it must land on the same code. 0x7F is the exception: mu-law has
        // two encodings of zero, 0xFF and 0x7F, and the encoder always produces the positive one.
        Assert.Equal(0, MuLaw.Decode(0x7F));
        for (int b = 0; b < 256; b++)
        {
            if (b == 0x7F) continue;
            Assert.Equal((byte)b, MuLaw.Encode(MuLaw.Decode((byte)b)));
        }
    }

    [Fact]
    public void MuLawRoundTripStaysWithinItsQuantisationStep()
    {
        for (int s = short.MinValue; s <= short.MaxValue; s += 7)
        {
            int back = MuLaw.Decode(MuLaw.Encode((short)s));
            int magnitude = Math.Abs(s);
            // mu-law step doubles with magnitude; the coarsest segment steps by 1024.
            int allowed = Math.Max(16, magnitude / 32 + 16);
            Assert.True(Math.Abs(back - s) <= allowed, $"sample {s} came back as {back}");
        }
    }

    [Fact]
    public void ToFramesSplitsOnFrameBoundariesAndPadsWithSilence()
    {
        var pcm = new short[CozmoAudio.SamplesPerFrame * 2 + 10];
        for (int i = 0; i < pcm.Length; i++) pcm[i] = 1000;
        var frames = CozmoAudio.ToFrames(pcm);
        Assert.Equal(3, frames.Count);
        Assert.All(frames, f => Assert.Equal(CozmoAudio.SamplesPerFrame, f.Length));
        var last = frames[2];
        Assert.Equal(MuLaw.Encode(1000), last[9]);
        Assert.Equal(MuLaw.Encode(0), last[10]);   // padding starts right after the real samples
        Assert.Equal(MuLaw.Encode(0), last[^1]);
    }

    [Fact]
    public void ToneHasTheRequestedLengthAndFadesInAndOut()
    {
        var pcm = CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(500));
        Assert.Equal(CozmoAudio.SampleRate / 2, pcm.Length);
        Assert.Equal(0, pcm[0]);                       // fade in starts at silence
        Assert.True(Math.Abs((int)pcm[^1]) < 1000);    // and fades back out
        int peak = pcm.Max(s => Math.Abs((int)s));
        Assert.InRange(peak, short.MaxValue / 4, short.MaxValue / 2 + 1);   // amplitude 0.5 of full scale

        // 440 Hz over 0.5 s is 220 cycles, so 440 zero crossings.
        int crossings = 0;
        for (int i = 1; i < pcm.Length; i++)
            if ((pcm[i - 1] < 0) != (pcm[i] < 0)) crossings++;
        Assert.InRange(crossings, 435, 445);
    }

    [Fact]
    public void AudioFramesAreSentAsAudioSampleMessagesThatReEncodeExactly()
    {
        var sent = new List<RobotMessage>();
        var audio = new CozmoAudio(sent.Add);
        audio.SetVolume(0x4000);
        foreach (var f in CozmoAudio.ToFrames(CozmoAudio.Tone(880, TimeSpan.FromMilliseconds(100))))
            audio.SendFrame(f);
        audio.SendSilence();

        Assert.IsType<SetAudioVolume>(sent[0]);
        Assert.Equal(0x4000, ((SetAudioVolume)sent[0]).Level);
        var samples = sent.OfType<AudioSample>().ToList();
        Assert.Equal(3, samples.Count);                       // 2205 samples over 744-sample frames
        Assert.Single(sent.OfType<AudioSilence>());
        Assert.Equal(samples.Count + 1, audio.FramesSent);
        foreach (var m in sent)
        {
            var bytes = m.ToBytes();
            Assert.Equal(bytes, RobotMessage.Parse(bytes).ToBytes());
        }
        Assert.Equal(1 + CozmoAudio.SamplesPerFrame, samples[0].ToBytes().Length);
    }

    /// <summary>
    /// The robot buffers only about 14 audio frames, so a whole tone has to be fed at the animation tick
    /// rather than pushed at once. The first hardware run dumped 61 frames in 2 ms and the robot played 14.
    /// The opening frames are deliberately sent back to back to fill that buffer before pacing starts.
    /// </summary>
    [Fact]
    public void PlayPrimesTheRobotBufferThenPacesAtTheAnimationTick()
    {
        var sent = new List<RobotMessage>();
        var audio = new CozmoAudio(sent.Add);
        var pcm = CozmoAudio.Tone(440, TimeSpan.FromSeconds(1));
        int expected = CozmoAudio.ToFrames(pcm).Count;
        int prime = audio.PrimeFrames;
        Assert.True(prime < CozmoAudio.RobotBufferFrames, "priming must not overrun the robot's buffer");
        Assert.True(expected > prime + 5, "the tone must be long enough to exercise pacing");

        var ticks = new List<TimeSpan>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        audio.OnFrameSent = () => ticks.Add(sw.Elapsed);
        audio.Play(pcm);
        sw.Stop();

        Assert.Equal(expected, sent.Count);
        Assert.Equal(expected, ticks.Count);
        // the priming burst goes out at once
        Assert.True(ticks[prime - 1] < CozmoAudio.FrameInterval,
            $"the {prime} priming frames took {ticks[prime - 1].TotalMilliseconds:F1} ms");
        var floor = CozmoAudio.FrameInterval * (expected - 1 - prime) * 0.8;
        Assert.True(sw.Elapsed >= floor,
            $"{expected} frames went out in {sw.ElapsedMilliseconds} ms, which is faster than the robot can consume them");

        // Every paced frame must land near its slot. Windows quantises Thread.Sleep to about 15.6 ms, half a
        // frame, so a pacer built on it drifts audibly; this is what catches that.
        for (int i = prime; i < ticks.Count; i++)
        {
            var due = CozmoAudio.FrameInterval * (i - prime);
            var error = (ticks[i] - due).Duration();
            Assert.True(error < TimeSpan.FromMilliseconds(8),
                $"frame {i} went out {error.TotalMilliseconds:F1} ms away from its slot at {due.TotalMilliseconds:F1} ms");
        }
    }

    [Fact]
    public void PlayRestartsItsScheduleSoASecondCallIsNotABurst()
    {
        var audio = new CozmoAudio(_ => { });
        audio.PrimeFrames = 0;
        var pcm = CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(150));
        int n = CozmoAudio.ToFrames(pcm).Count;
        audio.Play(pcm);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        audio.Play(pcm);
        sw.Stop();
        Assert.True(sw.Elapsed >= CozmoAudio.FrameInterval * (n - 1) * 0.8,
            $"the second call took only {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void SendFrameRejectsAFrameThatIsNotExactlyOneAnimationTick()
    {
        var audio = new CozmoAudio(_ => { });
        Assert.Throws<ArgumentException>(() => audio.SendFrame(new byte[100]));
        Assert.Throws<ArgumentException>(() => audio.SendFrame(new byte[CozmoAudio.SamplesPerFrame + 1]));
    }

    // -------------------------------------------------------------- display

    public static IEnumerable<object[]> FaceBitmaps()
    {
        yield return new object[] { "blank", new FaceBitmap() };
        var full = new FaceBitmap(); full.Fill();
        yield return new object[] { "full", full };
        yield return new object[] { "test pattern", FaceBitmap.TestPattern() };
        var dot = new FaceBitmap(); dot[63, 16] = 1;
        yield return new object[] { "single pixel", dot };
        var corners = new FaceBitmap();
        corners[0, 0] = 1; corners[FaceBitmap.Width - 1, 0] = 1; corners[0, FaceBitmap.Height - 1] = 1; corners[FaceBitmap.Width - 1, FaceBitmap.Height - 1] = 1;
        yield return new object[] { "corners", corners };
        var stripes = new FaceBitmap();
        for (int x = 0; x < FaceBitmap.Width; x += 2)
            for (int y = 0; y < FaceBitmap.Height; y++) stripes[x, y] = 1;
        yield return new object[] { "alternating columns", stripes };
        var rows = new FaceBitmap();
        for (int y = 0; y < FaceBitmap.Height; y += 2)
            for (int x = 0; x < FaceBitmap.Width; x++) rows[x, y] = 1;
        yield return new object[] { "alternating rows", rows };
        var noise = new FaceBitmap();
        var rnd = new Random(20260918);
        for (int x = 0; x < FaceBitmap.Width; x++)
            for (int y = 0; y < FaceBitmap.Height; y++) noise[x, y] = (byte)rnd.Next(2);
        yield return new object[] { "noise", noise };
        var eyes = new FaceBitmap();
        eyes.DrawRect(24, 6, 48, 25, filled: true);
        eyes.DrawRect(80, 6, 104, 25, filled: true);
        yield return new object[] { "two eyes", eyes };
    }

    [Theory]
    [MemberData(nameof(FaceBitmaps))]
    public void FaceImageCodecRoundTripsEveryPixel(string name, FaceBitmap image)
    {
        var payload = FaceBitmapCodec.Encode(image);
        var back = FaceBitmapCodec.Decode(payload);
        Assert.True(image.ToText() == back.ToText(), $"'{name}' did not survive the round trip\nin:\n{image.ToText()}\nout:\n{back.ToText()}");
    }

    [Fact]
    public void TypicalFaceImagesFitInOneNetworkFrame()
    {
        // The transport's MaxNetMessageSize is 1420 bytes and a face goes out as a single message.
        foreach (var row in FaceBitmaps())
        {
            // "noise" and "alternating rows" need a run command per pixel row; see the next test.
            if ((string)row[0] is "noise" or "alternating rows") continue;
            var payload = FaceBitmapCodec.Encode((FaceBitmap)row[1]);
            Assert.True(payload.Length <= CozmoDisplay.MaxPayload, $"'{row[0]}' encoded to {payload.Length} bytes");
        }
    }

    [Fact]
    public void AFaceTooComplexToEncodeIsRejectedRatherThanTruncated()
    {
        // Per-pixel noise needs 32 run commands per column, which no single message can carry. Real faces
        // are nothing like this, but the display must say so instead of sending a partial image.
        var noise = (FaceBitmap)FaceBitmaps().First(r => (string)r[0] == "noise")[1];
        Assert.True(FaceBitmapCodec.Encode(noise).Length > CozmoDisplay.MaxPayload);
        var display = new CozmoDisplay(_ => { });
        var ex = Assert.Throws<ArgumentException>(() => display.Show(noise));
        Assert.Contains("too complex", ex.Message);
    }

    /// <summary>
    /// The 28 image/byte-sequence pairs PyCozmo captured from Cozmo itself. These are the only samples of
    /// the face format produced by Anki's own encoder that we have, so decoding them correctly is the real
    /// check on the opcode meanings; our encoder only has to round-trip.
    /// </summary>
    public static IEnumerable<object[]> CozmoFaceFixtures()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "face_images.json");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        foreach (var e in doc.RootElement.EnumerateObject())
        {
            var art = string.Join("\n", e.Value.GetProperty("image").EnumerateArray().Select(x => x.GetString()!));
            yield return new object[] { e.Name, art, e.Value.GetProperty("seq").GetString()! };
        }
    }

    [Theory]
    [MemberData(nameof(CozmoFaceFixtures))]
    public void SequencesProducedByCozmoDecodeToTheExpectedImage(string name, string art, string seq)
    {
        var payload = seq.Split(':').Select(h => Convert.ToByte(h, 16)).ToArray();
        var decoded = FaceBitmapCodec.Decode(payload);
        Assert.True(FaceBitmap.FromText(art).ToText() == decoded.ToText(),
            $"'{name}' decoded differently\nexpected:\n{FaceBitmap.FromText(art).ToText()}\nactual:\n{decoded.ToText()}");
    }

    [Theory]
    [MemberData(nameof(CozmoFaceFixtures))]
    public void OurEncoderRoundTripsEveryImageCozmoProduced(string name, string art, string seq)
    {
        _ = seq;
        var image = FaceBitmap.FromText(art);
        var payload = FaceBitmapCodec.Encode(image);
        Assert.True(payload.Length <= CozmoDisplay.MaxPayload, $"'{name}' encoded to {payload.Length} bytes");
        Assert.True(image.ToText() == FaceBitmapCodec.Decode(payload).ToText(), $"'{name}' did not survive the round trip");
    }

    [Fact]
    public void FaceImageTextArtRoundTrips()
    {
        var art = string.Join('\n', Enumerable.Range(0, FaceBitmap.Height)
            .Select(y => new string(Enumerable.Range(0, FaceBitmap.Width).Select(x => (x + y) % 5 == 0 ? '#' : '.').ToArray())));
        var img = FaceBitmap.FromText(art);
        Assert.Equal(art + "\n", img.ToText());
        Assert.Equal(art + "\n", FaceBitmapCodec.Decode(FaceBitmapCodec.Encode(img)).ToText());
    }

    [Fact]
    public void UniformFacesEncodeAsOneFullColumnRunEach()
    {
        // Every column is one 32-pixel run, so a uniform image is 128 identical bytes.
        var blank = FaceBitmapCodec.Encode(new FaceBitmap());
        Assert.Equal(FaceBitmap.Width, blank.Length);
        Assert.All(blank, b => Assert.Equal(0xFC, b));      // extended run, length 32, both draw bits clear

        var full = new FaceBitmap(); full.Fill();
        var solid = FaceBitmapCodec.Encode(full);
        Assert.Equal(FaceBitmap.Width, solid.Length);
        Assert.All(solid, b => Assert.Equal(0xFF, b));      // the same run with both draw bits set
    }

    [Fact]
    public void DisplaySendsAnimFaceImageAndPacesAtTheAnimationRate()
    {
        var sent = new List<RobotMessage>();
        var display = new CozmoDisplay(sent.Add);
        var image = FaceBitmap.TestPattern();

        var start = DateTime.UtcNow;
        display.Show(image);
        display.Show(image);
        var elapsed = DateTime.UtcNow - start;

        Assert.Equal(2, sent.Count);
        Assert.Equal(2, display.FramesSent);
        Assert.All(sent, m => Assert.Equal(RobotMessageId.AnimFaceImage, m.Id));
        var msg = Assert.IsType<FaceMsg>(sent[0]);
        Assert.Equal(FaceBitmapCodec.Encode(image), msg.Image);
        Assert.Equal(msg.ToBytes(), RobotMessage.Parse(msg.ToBytes()).ToBytes());
        Assert.True(elapsed >= CozmoDisplay.MinInterval, $"two frames went out in {elapsed.TotalMilliseconds:F1} ms");
    }

    // --------------------------------------------------------------- camera

    private static ImageChunk Chunk(uint imageId, byte chunkId, byte[] data, byte total = 0) => new()
    {
        ImageId = imageId,
        ChunkId = chunkId,
        ImageChunkCount = total,
        ImageEncoding = (sbyte)MiniJpeg.EncodingJpegMinimizedGray,
        ImageResolution = 4,          // QVGA, what firmware 2457 sends
        Data = data,
    };

    [Fact]
    public void CameraReassemblesChunksIntoOneFrame()
    {
        var cam = new CozmoCamera();
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;

        cam.Handle(Chunk(7, 0, new byte[] { 0xAA, 0x01, 0x02 }));
        cam.Handle(Chunk(7, 1, new byte[] { 0x03, 0x04 }));
        cam.Handle(Chunk(7, 2, new byte[] { 0x05 }, total: 3));

        var f = Assert.Single(frames);
        Assert.Equal(7u, f.ImageId);
        Assert.Equal(320, f.Width);
        Assert.Equal(240, f.Height);
        Assert.Equal(3, f.ChunkCount);
        Assert.Equal(new byte[] { 0xAA, 0x01, 0x02, 0x03, 0x04, 0x05 }, f.RawPayload);
        Assert.Equal(1, cam.FramesCompleted);
        Assert.Equal(0, cam.FramesDropped);
        Assert.Same(f, cam.LastFrame);
    }

    [Fact]
    public void CameraAttachesTheGyroSampleThatArrivedWithTheFrame()
    {
        var cam = new CozmoCamera();
        CameraFrame? got = null;
        cam.FrameReceived += f => got = f;
        cam.Handle(new ImageImuData { ImageId = 3, RateX = 0.25f, RateY = -0.5f, RateZ = 1.5f });
        cam.Handle(Chunk(3, 0, new byte[] { 0x00, 0x11 }, total: 1));
        Assert.NotNull(got);
        Assert.Equal((0.25f, -0.5f, 1.5f), got!.GyroRates);
    }

    [Fact]
    public void CameraDropsAnImageThatIsSupersededBeforeItCompletes()
    {
        var cam = new CozmoCamera();
        var dropped = new List<uint>();
        var frames = new List<CameraFrame>();
        cam.FrameDropped += (id, _) => dropped.Add(id);
        cam.FrameReceived += frames.Add;

        cam.Handle(Chunk(1, 0, new byte[] { 1 }));          // image 1 never finishes
        cam.Handle(Chunk(2, 0, new byte[] { 2 }));
        cam.Handle(Chunk(3, 0, new byte[] { 3 }));
        cam.Handle(Chunk(4, 0, new byte[] { 4 }, total: 1)); // image 4 completes and retires image 1

        Assert.Equal(new uint[] { 1 }, dropped);
        Assert.Single(frames);
        Assert.Equal(1, cam.FramesDropped);
    }

    [Fact]
    public void CameraRejectsAFrameWithAMissingChunk()
    {
        var cam = new CozmoCamera();
        string? reason = null;
        var frames = new List<CameraFrame>();
        cam.FrameDropped += (_, r) => reason = r;
        cam.FrameReceived += frames.Add;

        cam.Handle(Chunk(9, 0, new byte[] { 1 }));
        cam.Handle(Chunk(9, 2, new byte[] { 3 }));
        cam.Handle(Chunk(9, 3, new byte[] { 4 }, total: 3));  // three chunks arrived, but chunk 1 is missing

        Assert.Empty(frames);
        Assert.Equal(1, cam.FramesDropped);
        Assert.Contains("expected chunks 0..2", reason);
    }

    [Fact]
    public void MiniJpegPatchesTheFrameSizeAndRestoresByteStuffing()
    {
        // payload[0] is dropped, each 0xFF gains a 0x00, trailing 0xFF padding goes away, EOI is appended.
        var payload = new byte[] { 0x99, 0x12, 0xFF, 0x34, 0xFF, 0xFF, 0xFF };
        var jpeg = MiniJpeg.ToJpeg(payload, 320, 240, MiniJpeg.EncodingJpegMinimizedGray);
        int header = MiniJpeg.HeaderLength(color: false);
        Assert.Equal(new byte[] { 0x12, 0xFF, 0x00, 0x34, 0xFF, 0xD9 }, jpeg[header..]);
        Assert.Equal(0xFF, jpeg[0]); Assert.Equal(0xD8, jpeg[1]);

        // the SOF0 segment must carry the size we asked for, big-endian height then width
        int sof = IndexOfMarker(jpeg, 0xC0);
        Assert.Equal(240, (jpeg[sof + 5] << 8) | jpeg[sof + 6]);
        Assert.Equal(320, (jpeg[sof + 7] << 8) | jpeg[sof + 8]);
        Assert.Equal(1, jpeg[sof + 9]);          // one component: grayscale
    }

    [Fact]
    public void MiniJpegPassesThroughEncodingsThatAreAlreadyCompleteFiles()
    {
        var already = new byte[] { 0xFF, 0xD8, 0x01, 0x02, 0xFF, 0xD9 };
        Assert.Equal(already, MiniJpeg.ToJpeg(already, 320, 240, MiniJpeg.EncodingJpegGray));
    }

    private static int IndexOfMarker(byte[] j, byte marker)
    {
        for (int i = 0; i < j.Length - 1; i++) if (j[i] == 0xFF && j[i + 1] == marker) return i;
        throw new InvalidOperationException($"marker 0xFF{marker:X2} not found");
    }

    [Fact]
    public void TheDisplayPairsEachFaceFrameWithAnAudioFrameWhenAskedTo()
    {
        var sent = new List<RobotMessage>();
        var display = new CozmoDisplay(sent.Add) { BeforeFrame = () => sent.Add(new AudioSilence()) };
        display.Show(FaceBitmap.TestPattern());
        Assert.Equal(2, sent.Count);
        Assert.IsType<AudioSilence>(sent[0]);       // the engine sends audio first on each animation tick
        Assert.IsType<FaceMsg>(sent[1]);
    }

    [Fact]
    public void AudioReportsWhetherItIsStreaming()
    {
        var audio = new CozmoAudio(_ => { });
        Assert.False(audio.Busy);
        audio.SendSilence();
        Assert.True(audio.Busy);
    }

    // ------------------------------------------------- capture replay (real robot data)

    private static readonly Regex Line = new(@"^(\S+) (TX|RX) ((?:[0-9a-f]{2} ?)+)$", RegexOptions.Compiled);

    /// <summary>
    /// Every message the robot delivered during a capture, in order, fed through the real receive path so
    /// that the duplicates it resends until acked are dropped exactly as they are on a live connection.
    /// </summary>
    private static List<RobotMessage> Replay(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", file);
        var clock = new ManualClock { NowMs = 1000 };
        var transport = ReliableTransport.CreateOffline(TransportOptions.EngineDefaults, clock);
        var got = new List<RobotMessage>();
        transport.DataReceived += d => got.Add(RobotMessage.Parse(d));
        transport.OfflineConnect();
        foreach (var l in File.ReadAllLines(path))
        {
            var m = Line.Match(l.Trim('﻿', ' '));
            if (!m.Success || m.Groups[2].Value != "RX") continue;
            clock.Advance(33);
            transport.ProcessIncoming(Hex.Parse(m.Groups[3].Value));
        }
        return got;
    }

    /// <summary>
    /// The acceptance test for the camera pipeline, run offline: take the image chunks a real robot sent
    /// during the 2026-09-18 probe, reassemble them and fully Huffman-decode the result. Every frame has to
    /// come out as a 320x240 single-component baseline JPEG whose entropy stream decodes to exactly 1200 MCUs.
    /// </summary>
    [Fact]
    public void CapturedChunksReassembleIntoDecodableQvgaJpegs()
    {
        var cam = new CozmoCamera();
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        foreach (var m in Replay("hw_fw2457_probe.log")) cam.Handle(m);

        Assert.True(frames.Count >= 20, $"only {frames.Count} frames reassembled from the capture");
        Assert.All(frames, f =>
        {
            Assert.Equal(MiniJpeg.EncodingJpegMinimizedGray, f.Encoding);
            Assert.Equal(4, f.Resolution);                 // QVGA
            Assert.False(f.IsColor);                       // the probe asked for grayscale
            Assert.Equal(320, f.JpegWidth);
            // The frame shrinks as auto-exposure settles, so the chunk count varies; each chunk is ~1 kB.
            Assert.InRange(f.ChunkCount, 3, 8);
            Assert.InRange(f.RawPayload.Length, 3000, 12000);
            var info = JpegCheck.Validate(f.Jpeg);
            Assert.Equal(320, info.Width);
            Assert.Equal(240, info.Height);
            Assert.Equal(1, info.Components);
            Assert.Equal(40 * 30, info.McusDecoded);       // 320x240 in 8x8 blocks
            Assert.Equal(0, info.TrailingBytes);           // the stream ends exactly where the last MCU does
        });
    }

    /// <summary>
    /// The first payload byte is the colour flag, so on this grayscale capture it has to be zero. A JPEG
    /// entropy stream re-synchronises after a shift, so decoding alone cannot tell the byte apart from
    /// data; this is what pins it down.
    /// </summary>
    [Fact]
    public void TheFirstPayloadByteIsTheColourFlagAndIsClearOnAGrayscaleCapture()
    {
        var cam = new CozmoCamera();
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        foreach (var m in Replay("hw_fw2457_probe.log")) cam.Handle(m);
        Assert.NotEmpty(frames);
        Assert.All(frames, f => Assert.Equal(0, f.RawPayload[0]));
    }

    [Fact]
    public void AColourFlaggedFrameIsRebuiltAtHalfWidthWithThreeComponents()
    {
        var cam = new CozmoCamera();
        CameraFrame? got = null;
        cam.FrameReceived += f => got = f;
        // Take a real grayscale payload and only flip the colour flag: the header and geometry must change.
        var source = new CozmoCamera();
        CameraFrame? gray = null;
        source.FrameReceived += f => gray ??= f;
        foreach (var m in Replay("hw_fw2457_probe.log")) source.Handle(m);
        var payload = (byte[])gray!.RawPayload.Clone();
        payload[0] = 1;
        cam.Handle(Chunk(1, 0, payload, total: 1));

        Assert.True(got!.IsColor);
        Assert.Equal(320, got.Width);        // the resolution is still QVGA
        Assert.Equal(160, got.JpegWidth);    // but the encoded image is half as wide
        int sof = IndexOfMarker(got.Jpeg, 0xC0);
        Assert.Equal(240, (got.Jpeg[sof + 5] << 8) | got.Jpeg[sof + 6]);
        Assert.Equal(160, (got.Jpeg[sof + 7] << 8) | got.Jpeg[sof + 8]);
        Assert.Equal(3, got.Jpeg[sof + 9]);
    }

    /// <summary>
    /// The second payload byte is captured, not interpreted. It is not entropy data: it changes by small
    /// amounts from frame to frame while the picture rotates, and pinning down what it means is what will
    /// let the rotation be corrected. See re-analysis/DEVICE_LAYER.md.
    /// </summary>
    [Fact]
    public void TheStreamMarkerIsRecordedAndIsNotConstant()
    {
        var cam = new CozmoCamera();
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        foreach (var m in Replay("hw_fw2457_probe.log")) cam.Handle(m);
        Assert.True(frames.Count >= 4);
        Assert.Equal(frames[0].RawPayload[1], frames[0].StreamMarker);
        Assert.True(frames.Select(f => f.StreamMarker).Distinct().Count() > 1);
    }

    /// <summary>
    /// The sensor takes about eleven frames to lock after the camera starts; during that time each picture
    /// is torn and rolls by one macroblock row per frame. Those frames decode perfectly but are not usable
    /// images, so the camera flags them and callers skip them.
    /// </summary>
    [Fact]
    public void WarmUpFramesAreFlaggedAndUsableFramesFollow()
    {
        var cam = new CozmoCamera();
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        foreach (var m in Replay("hw_fw2457_probe.log")) cam.Handle(m);

        Assert.True(frames.Count > cam.WarmUpFrames, "the capture must run past the warm-up");
        Assert.True(cam.Settled);
        for (int i = 0; i < frames.Count; i++)
        {
            Assert.Equal(i, frames[i].FrameIndex);
            Assert.Equal(i < cam.WarmUpFrames, frames[i].IsWarmUp);
        }
        Assert.Contains(frames, f => !f.IsWarmUp);
    }

    [Fact]
    public void RestartingTheCameraBeginsTheWarmUpAgain()
    {
        var cam = new CozmoCamera { WarmUpFrames = 2 };
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        for (uint id = 1; id <= 4; id++) cam.Handle(Chunk(id, 0, new byte[] { 0, 0x11, 0x22 }, total: 1));
        Assert.Equal(new[] { true, true, false, false }, frames.Select(f => f.IsWarmUp));
        Assert.True(cam.Settled);

        cam.Restart();
        Assert.False(cam.Settled);
        cam.Handle(Chunk(5, 0, new byte[] { 0, 0x11, 0x22 }, total: 1));
        Assert.True(frames[^1].IsWarmUp);
    }

    [Fact]
    public void ImageChunksFromTheCaptureCarryTheExpectedChunkNumbering()
    {
        var chunks = Replay("hw_fw2457_probe.log").OfType<ImageChunk>().ToList();
        Assert.NotEmpty(chunks);
        byte expected = 0;
        int images = 0;
        foreach (var c in chunks)
        {
            Assert.Equal(expected, c.ChunkId);             // chunks arrive strictly in order
            if (c.ImageChunkCount == 0) { expected++; continue; }
            // the total is reported only on the final chunk, and it agrees with the chunk numbering
            Assert.Equal(c.ChunkId + 1, c.ImageChunkCount);
            expected = 0;
            images++;
        }
        Assert.True(images >= 20, $"only {images} complete images in the capture");
    }

    // ----------------------------------------------------------- robot state

    [Fact]
    public void RobotStateTrackerFollowsTheTelemetryStream()
    {
        var state = new RobotStateTracker();
        foreach (var m in Replay("hw_fw2457_full.log")) state.Handle(m);

        Assert.Equal(2457, state.FirmwareVersionNumber);
        Assert.Equal(0x41d04d9du, state.SerialNumber);
        Assert.NotNull(state.Manufacturing);
        Assert.True(state.TimeSynced);
        Assert.True(state.CalibrationSeen);
        Assert.False(state.CalibratingMotors);             // calibration finished during the run
        Assert.True(state.StateCount > 500);
        Assert.True(state.OnCharger);
        Assert.NotNull(state.BatteryVolts);
        Assert.InRange(state.BatteryVolts!.Value, 3.5f, 5.0f);
        Assert.Equal(state.StateCount, state.Histogram[RobotMessageId.State]);
        Assert.True(state.Histogram.Count >= 8, "the capture should exercise more than a handful of message types");
    }

    [Fact]
    public void RobotStateTrackerRaisesAnEventPerStateMessage()
    {
        var state = new RobotStateTracker();
        int events = 0;
        state.StateUpdated += _ => events++;
        foreach (var m in Replay("hw_fw2457_first120.log")) state.Handle(m);
        Assert.Equal(state.StateCount, events);
        Assert.True(events > 10);
    }
}
