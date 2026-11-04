using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The cube and charger marker detector, from the grey image to the ObservedMarker list (inventory
/// re-analysis/research/20261103-marker-detector-extraction.md, rows 1-38; records M11-002, M11-005, M11-020, M11-021, M11-022, M11-023,
/// M11-026, M11-028). Every expected value is what the engine's own code returned when re-analysis/tools/emu/gen_marker_detector_fixtures.py
/// ran it under Unicorn over the shipped libcozmoEngine.so and OpenCV (emu_marker_detector.py): VisionSystem::ApplyCLAHE 0x006B44E0, the
/// DetectMarkersWithCLAHE pick 0x006B47A8..0x006B47B4 and MarkerDetector::Detect 0x008753B6 for the end-to-end cases;
/// ComputeHomographyFromQuad 0x008C9036 with the validity-6 gate 0x00898E68..0x00898F12 for the homography cases; and
/// VisionMarker::GetProbeValues 0x0089EF40 for the probe cases. Never what the C# returns. Inputs and outputs are bit patterns.
/// The libm boundary: the oracle's exp (double) and atan2f are the host's (Python math); sqrt is IEEE-exact; floorf/ceilf/roundf are exact.
/// </summary>
[Collection("SteppedBehavior missing-report statics")]
public class NativeMarkerDetectorOracleTests
{
    internal sealed record Marker(int Type, uint Ts, uint[] Corners);
    internal sealed record Slot(int Validity, int Type, uint[] Corners, uint[] H);
    internal sealed record Case(string Name, string Image, uint Ts, bool Clahe, uint Ret, int? Dark, string MarkerImageSha, short[][] Quads,
                                Slot[] Slots, Slot[] Final, Marker[] Markers);
    internal sealed record HomographyCase(short[] Quad, int Validity, string? Cause, uint[] H);
    internal sealed record ProbeCase(string Name, string Image, uint[] H, byte[] Probes);
    internal sealed record Data(JsonElement Root, Dictionary<string, GrayImage> Images, Dictionary<string, string?> Jpegs, List<Case> Cases,
                                List<HomographyCase> Homographies, List<ProbeCase> Probes);

    internal static readonly Lazy<Data> Fixture_ = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "marker_detector", "marker_detector_oracle.json");
        var root = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        int rows = root.GetProperty("rows").GetInt32(), cols = root.GetProperty("cols").GetInt32();
        var images = new Dictionary<string, GrayImage>();
        var jpegs = new Dictionary<string, string?>();
        foreach (var im in root.GetProperty("images").EnumerateObject())
        {
            using var z = new ZLibStream(new MemoryStream(Convert.FromBase64String(im.Value.GetProperty("zlib_b64").GetString()!)), CompressionMode.Decompress);
            using var ms = new MemoryStream();
            z.CopyTo(ms);
            var pix = ms.ToArray();
            Assert.Equal(im.Value.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(pix)).ToLowerInvariant());
            images[im.Name] = new GrayImage(cols, rows, pix);
            jpegs[im.Name] = im.Value.GetProperty("jpeg").ValueKind == JsonValueKind.String ? im.Value.GetProperty("jpeg").GetString() : null;
        }
        static uint[] Hexes(JsonElement a) => a.EnumerateArray().Select(v => uint.Parse(v.GetString()!, NumberStyles.HexNumber)).ToArray();
        static Slot S(JsonElement s) => new(s.GetProperty("validity").GetInt32(), s.TryGetProperty("type", out var t) ? t.GetInt32() : -1,
                                            s.TryGetProperty("corners", out var c) ? Hexes(c) : Array.Empty<uint>(), Hexes(s.GetProperty("h")));
        var cases = new List<Case>();
        foreach (var c in root.GetProperty("cases").EnumerateArray())
            cases.Add(new Case(c.GetProperty("name").GetString()!, c.GetProperty("image").GetString()!, c.GetProperty("ts").GetUInt32(),
                c.GetProperty("clahe").GetBoolean(), c.GetProperty("ret").GetUInt32(),
                c.GetProperty("dark").ValueKind == JsonValueKind.Number ? c.GetProperty("dark").GetInt32() : null,
                c.GetProperty("marker_image_sha256").GetString()!,
                c.GetProperty("quads").EnumerateArray().Select(q => q.EnumerateArray().Select(v => (short)v.GetInt32()).ToArray()).ToArray(),
                c.GetProperty("slots").EnumerateArray().Select(S).ToArray(), c.GetProperty("final").EnumerateArray().Select(S).ToArray(),
                c.GetProperty("markers").EnumerateArray().Select(m => new Marker(m.GetProperty("type").GetInt32(), m.GetProperty("ts").GetUInt32(),
                    Hexes(m.GetProperty("corners")))).ToArray()));
        var hs = root.GetProperty("homography_cases").EnumerateArray().Select(h => new HomographyCase(
            h.GetProperty("quad").EnumerateArray().Select(v => (short)v.GetInt32()).ToArray(), h.GetProperty("validity").GetInt32(),
            h.GetProperty("cause").ValueKind == JsonValueKind.String ? h.GetProperty("cause").GetString() : null, Hexes(h.GetProperty("h")))).ToList();
        var ps = root.GetProperty("probe_cases").EnumerateArray().Select(p => new ProbeCase(p.GetProperty("name").GetString()!,
            p.GetProperty("image").GetString()!, Hexes(p.GetProperty("h")), Convert.FromHexString(p.GetProperty("probes").GetString()!))).ToList();
        return new Data(root, images, jpegs, cases, hs, ps);
    });

    private static uint Bits(double v) => BitConverter.SingleToUInt32Bits((float)v);

    /// <summary>The engine's error return when the homography loop meets quad 100 (rows 18, 35): 0x05000000, no markers.</summary>
    private const uint QuadBeyondListReturn = 0x05000000;

    /// <summary>Runs the stack's detector on a case, the way VisionSystem does (SelectMarkerImage, then Detect on that image).</summary>
    internal static (GrayImage Work, MarkerDetector Det, IReadOnlyList<ObservedMarker> Markers) Run(Data d, Case c)
    {
        var img = d.Images[c.Image];
        var work = c.Clahe ? VisionSystem.SelectMarkerImage(img) : img;
        var copy = new GrayImage(work.Width, work.Height, (byte[])work.Pixels.Clone());
        var det = new MarkerDetector();
        var markers = det.Detect(copy, c.Ts);
        return (work, det, markers);
    }

    /// <summary>The first difference between the stack's run and the oracle's case, or null.</summary>
    internal static string? Compare(Case c, GrayImage source, GrayImage work, MarkerDetector det, IReadOnlyList<ObservedMarker> markers)
    {
        string sha = Convert.ToHexString(SHA256.HashData(work.Pixels)).ToLowerInvariant();
        if (c.Clahe && (c.Dark != 0) != VisionSystem.IsDarkForClahe(source)) return $"dark flag {VisionSystem.IsDarkForClahe(source)} != {c.Dark}";
        if (sha != c.MarkerImageSha) return "the marker image (the CLAHE pick) differs";
        var quads = det.LastQuadList;
        if (quads.Count != c.Quads.Length) return $"{quads.Count} quads != {c.Quads.Length}";
        for (int i = 0; i < quads.Count; i++)
            for (int k = 0; k < 4; k++)
                if ((short)quads[i].Corners[k].X != c.Quads[i][2 * k] || (short)quads[i].Corners[k].Y != c.Quads[i][2 * k + 1])
                    return $"quad {i} [{string.Join(",", quads[i].Corners)}] != [{string.Join(",", c.Quads[i])}]";
        if (c.Ret != 0) return null;                    // the M1-034 cases: compared only up to the quads (see the caller)
        if (det.LastSlots.Count != c.Slots.Length) return $"{det.LastSlots.Count} slots != {c.Slots.Length}";
        for (int i = 0; i < c.Slots.Length; i++)
        {
            var s = det.LastSlots[i];
            if (s.Validity != c.Slots[i].Validity) return $"slot {i} validity {s.Validity} != {c.Slots[i].Validity}";
            for (int k = 0; k < 9; k++)
                if (BitConverter.SingleToUInt32Bits(s.H[k]) != c.Slots[i].H[k]) return $"slot {i} H[{k}] {s.H[k]} != {BitConverter.UInt32BitsToSingle(c.Slots[i].H[k])}";
        }
        // the slots after the refine/decode loop (0x00899668): validity, type, corners and the refined H
        if (det.LastFinalSlots.Count != c.Final.Length) return $"{det.LastFinalSlots.Count} final slots != {c.Final.Length}";
        for (int i = 0; i < c.Final.Length; i++)
        {
            var g = det.LastFinalSlots[i];
            var w = c.Final[i];
            if (g.Validity != w.Validity) return $"final slot {i} validity {g.Validity} != {w.Validity}";
            if (g.Type >= 0 && g.Type != w.Type) return $"final slot {i} type {g.Type} != {w.Type}";
            if (g.Type < 0) return $"final slot {i} (validity {g.Validity}) has no modelled type; the engine's is {w.Type}";
            for (int k = 0; k < 4; k++)
                if (Bits(g.Corners[k].X) != w.Corners[2 * k] || Bits(g.Corners[k].Y) != w.Corners[2 * k + 1]) return $"final slot {i} corner {k}";
            for (int k = 0; k < 9; k++)
                if (BitConverter.SingleToUInt32Bits(g.H[k]) != w.H[k]) return $"final slot {i} H[{k}] {g.H[k]} != {BitConverter.UInt32BitsToSingle(w.H[k])}";
        }
        if (markers.Count != c.Markers.Length) return $"{markers.Count} markers [{string.Join(",", markers.Select(m => m.Code))}] != {c.Markers.Length} [{string.Join(",", c.Markers.Select(m => m.Type))}]";
        for (int i = 0; i < markers.Count; i++)
        {
            var w = c.Markers[i];
            var g = markers[i];
            if ((int)g.Code != w.Type) return $"marker {i} type {(int)g.Code} != {w.Type}";
            if (g.Timestamp != w.Ts) return $"marker {i} ts {g.Timestamp} != {w.Ts}";
            for (int k = 0; k < 4; k++)
                if (Bits(g.Corners[k].X) != w.Corners[2 * k] || Bits(g.Corners[k].Y) != w.Corners[2 * k + 1])
                    return $"marker {i} corner {k} ({(float)g.Corners[k].X},{(float)g.Corners[k].Y}) != ({BitConverter.UInt32BitsToSingle(w.Corners[2 * k])},{BitConverter.UInt32BitsToSingle(w.Corners[2 * k + 1])})";
        }
        return null;
    }

    /// <summary>
    /// Rows 1-38: every end-to-end oracle case, bit for bit: the marker image the CLAHE front end picks (row 1), the quad list (rows 7-16),
    /// the slots after the homography loop with their validity and H bits (rows 17-23), and the ObservedMarker list - type, timestamp,
    /// the four corners as binary32 - in list order (rows 24-36). A case whose quads exceed the 100-slot list (rows 17, 18, 35) is
    /// compared up to the quads; the engine's run then wrote past the list (undefined, M1-034) and returned 0x05000000 with no
    /// markers, which the next test records. reach: unit (MarkerDetector.Detect on VisionSystem.SelectMarkerImage's output, as
    /// VisionSystem.ProcessImage calls them; the production join is tested below).
    /// </summary>
    [Fact]
    public void EveryOracleCaseMatchesTheEnginesDetectBitForBit()
    {
        var d = Fixture_.Value;
        var report = new StringBuilder();
        int mismatched = 0;
        foreach (var c in d.Cases)
        {
            var (work, det, markers) = Run(d, c);
            var diff = Compare(c, d.Images[c.Image], work, det, markers);
            if (diff is not null) { mismatched++; if (mismatched <= 25) report.AppendLine($"{c.Name}: {diff}"); }
        }
        Assert.True(mismatched == 0, $"{mismatched}/{d.Cases.Count} differ\n{report}");
    }

    /// <summary>
    /// Rows 17, 18, 35: above 100 quads the engine's homography loop writes slot i·0x40 past the 100-slot list (0x00898E78..0x00898F1A),
    /// which is undefined; under the oracle's allocator the slot-100 header it then reads is not a 3x3 array, EstimateHomography fails
    /// ("homography must be 3x3") and DetectFiducialMarkers returns 0x05000000, so Detect adds no marker (0x00898EA0 -> 0x00898830).
    /// The stack follows the M1-034 standing rule: the smallest unit the original loses is this pass's markers, so it adds none,
    /// as the engine's error return does, and logs the skip naming M1-034 and M11-023. reach: unit.
    /// </summary>
    [Fact]
    public void QuadsBeyondTheMarkerListDropThePassAndAreLogged()
    {
        var d = Fixture_.Value;
        var over = d.Cases.Where(c => c.Quads.Length > 100).ToList();
        Assert.NotEmpty(over);
        foreach (var c in over)
        {
            Assert.Equal(QuadBeyondListReturn, c.Ret);
            Assert.Empty(c.Markers);
            SteppedBehavior.ResetMissingForTests();
            var reports = new List<string>();
            void On(string r) { lock (reports) reports.Add(r); }
            SteppedBehavior.MissingReported += On;
            try
            {
                var (work, det, markers) = Run(d, c);
                Assert.Null(Compare(c, d.Images[c.Image], work, det, markers));
                Assert.Equal(100, det.LastSlots.Count);
                Assert.Empty(markers);
            }
            finally { SteppedBehavior.MissingReported -= On; }
            lock (reports) Assert.Contains(reports, r => r.Contains("M1-034/M11-023") && r.Contains("beyond the 100-slot marker list"));
        }
    }

    /// <summary>
    /// Rows 19-22: <c>ComputeHomographyFromQuad</c> on chosen s16 quads (the oracle wrote them over the engine's own quad list before its
    /// homography loop): the H bits, and validity 6 for a non-convex quad (0x008C91D0), a Cholesky pivot under FLT_EPSILON
    /// (0x008C95EC), a reprojection error above 0.1 (0x008C9774), or H[2][0] / H[2][1] above 8.0. reach: unit
    /// (Homography.ComputeFromQuadS16, which MarkerDetector calls for each quad).
    /// </summary>
    [Fact]
    public void EveryHomographyCaseMatchesBitForBit()
    {
        var d = Fixture_.Value;
        var report = new StringBuilder();
        int bad = 0;
        foreach (var hc in d.Homographies)
        {
            var h = Homography.ComputeFromQuadS16(hc.Quad, out bool failure);
            int validity = failure || h[6] > 8f || h[7] > 8f ? 6 : 4;
            var got = h.Select(BitConverter.SingleToUInt32Bits).ToArray();
            if (validity != hc.Validity || !got.SequenceEqual(hc.H))
            {
                bad++;
                if (bad <= 20) report.AppendLine($"[{string.Join(",", hc.Quad)}] ({hc.Cause}): validity {validity}/{hc.Validity} H [{string.Join(",", h)}] vs [{string.Join(",", hc.H.Select(BitConverter.UInt32BitsToSingle))}]");
            }
        }
        Assert.True(bad == 0, $"{bad}/{d.Homographies.Count} differ\n{report}");
    }

    /// <summary>Row 31: <c>VisionMarker::GetProbeValues</c> 0x0089EF40 called directly, all 1024 probe bytes. reach: unit.</summary>
    [Fact]
    public void EveryProbeCaseMatchesBitForBit()
    {
        if (!MarkerLibrary.IsAvailable) return;
        var d = Fixture_.Value;
        var dec = new MarkerDecoder(MarkerLibrary.Embedded);
        foreach (var pc in d.Probes)
        {
            var h = new Homography(pc.H.Select(b => (double)BitConverter.UInt32BitsToSingle(b)).ToArray());
            var got = dec.GetProbeValuesOrNull(d.Images[pc.Image], h, out _);
            Assert.NotNull(got);
            if (!got!.SequenceEqual(pc.Probes))
                Assert.Fail($"{pc.Name}: first difference at {Enumerable.Range(0, 1024).First(i => got[i] != pc.Probes[i])}");
        }
    }

    /// <summary>
    /// The corpus reaches the rows the extraction marked DIFFERS, NOT IN STACK or UNKNOWN; the oracle's own outputs and probes show it.
    /// For row 31's .5 ties and the edge wrap the probe coordinates are recomputed from the port only to show the corpus contains them.
    /// </summary>
    [Fact]
    public void TheCorpusCoversTheRowsThatDiffer()
    {
        var d = Fixture_.Value;
        var cases = d.Cases;
        Assert.Equal(0, d.Root.GetProperty("neon_differences").GetArrayLength());
        // row 1: both paths of the dark test
        Assert.Contains(cases, c => c.Clahe && c.Dark == 1 && c.Markers.Length > 0);
        Assert.Contains(cases, c => c.Clahe && c.Dark == 0 && c.Markers.Length > 0);
        // real frames and rendered frames
        Assert.True(cases.Count(c => c.Image.StartsWith("jpeg:")) >= 40, "real frames");
        Assert.True(cases.Count(c => c.Image.StartsWith("render:")) >= 60, "rendered frames");
        Assert.True(cases.Where(c => c.Image.StartsWith("jpeg:")).Sum(c => c.Markers.Length) >= 15, "markers in real frames");
        // the codes decoded across the corpus (cubes and the charger)
        var codes = cases.SelectMany(c => c.Markers.Select(m => m.Type)).Distinct().ToList();
        Assert.True(codes.Count >= 15, $"{codes.Count} codes");
        Assert.Contains((int)MarkerType.Charger, codes);
        // rows 17, 18: more than 100 quads; and exactly 100 or under
        Assert.Contains(cases, c => c.Quads.Length > 100);
        Assert.Contains(cases, c => c.Quads.Length is >= 50 and <= 100 && c.Ret == 0);
        // rows 28, 30: refinement failures (7), low contrast (2), rejected decodes (3), decoded (0)
        var finals = cases.SelectMany(c => c.Final.Select(f => f.Validity)).ToHashSet();
        foreach (int v in new[] { 0, 2, 3, 7 }) Assert.Contains(v, finals);
        // rows 19-22: every cause of validity 6, and plain successes
        var hs = d.Homographies;
        Assert.True(hs.Count >= 400, $"{hs.Count} homography cases");
        Assert.Contains(hs, h => h.Validity == 4);
        Assert.Contains(hs, h => h.Cause == "not_convex");
        Assert.Contains(hs, h => h.Cause == "reprojection");
        Assert.Contains(hs, h => h.Cause is null && h.Validity == 6 && BitConverter.UInt32BitsToSingle(h.H[6]) > 8f);
        Assert.Contains(hs, h => h.Cause is null && h.Validity == 6 && BitConverter.UInt32BitsToSingle(h.H[7]) > 8f);
        // row 31: probe coordinates exactly on .5, and probes off the left and right edge read from the neighbouring rows
        Assert.True(d.Probes.Count >= 30);
        if (MarkerLibrary.IsAvailable)
        {
            var lib = MarkerLibrary.Embedded;
            int ties = 0, wrapped = 0;
            float scale = 1f / (1 << MarkerLibrary.NumFractionalBits);
            foreach (var pc in d.Probes)
            {
                var h = pc.H.Select(BitConverter.UInt32BitsToSingle).ToArray();
                for (int i = 0; i < MarkerLibrary.NumProbes; i++)
                    for (int k = 0; k < MarkerLibrary.NumProbePoints; k++)
                    {
                        float x = scale * lib.ProbeCentersX[i] + scale * lib.ProbePointsX[k], y = scale * lib.ProbeCentersY[i] + scale * lib.ProbePointsY[k];
                        float inv = 1f / (h[8] + (h[6] * x + h[7] * y));
                        float u = (h[2] + (h[0] * x + h[1] * y)) * inv, v = (h[5] + (h[3] * x + h[4] * y)) * inv;
                        if (u - MathF.Floor(u) == 0.5f || v - MathF.Floor(v) == 0.5f) ties++;
                        int col = MarkerDecoder.RoundNearestF(u);
                        if (col < 0 || col > 319) wrapped++;
                    }
            }
            Assert.True(ties > 100 && wrapped > 100, $"{ties} ties, {wrapped} wrapped samples");
        }
    }

    /// <summary>The robot's minimized grey JPEG (encoding 8) for a hardware-bundle JPEG: the inverse of MiniJpeg.ToJpeg, checked by a round trip.</summary>
    private static byte[] ToMiniJpeg(byte[] jpeg)
    {
        int header = MiniJpeg.HeaderLength(false);
        var payload = new List<byte> { 0 };
        for (int i = header; i < jpeg.Length - 2; i++)
        {
            payload.Add(jpeg[i]);
            if (jpeg[i] == 0xFF) i++;                       // the stuffed 0x00 the camera strips
        }
        var mini = payload.ToArray();
        Assert.Equal(jpeg, MiniJpeg.ToJpeg(mini, 320, 240, MiniJpeg.EncodingJpegMinimizedGray));
        return mini;
    }

    private static void SendMiniJpegFrame(PickupPyramidNativeExecutionTests.Fixture f, uint id, byte[] mini)
    {
        uint ts = f.Robot.StoredState!.Timestamp;
        f.Rig.Data(new ImageImuData { ImageId = id, RateX = 0, RateY = 0, RateZ = 0, Line2Number = 0 }); f.Rig.Tick();
        const int size = 1200;
        int count = (mini.Length + size - 1) / size;
        for (int i = 0; i < count; i++)
            f.Rig.Data(new ImageChunk { ImageId = id, FrameTimestamp = ts, ImageEncoding = MiniJpeg.EncodingJpegMinimizedGray, ImageResolution = 4,
                                        ImageChunkCount = checked((byte)count), ChunkId = checked((byte)i), Data = mini.Skip(i * size).Take(size).ToArray() });
        f.Rig.Tick();
        f.M.Vision.WaitForProcessorIdle();
        f.Rig.Engine.VisionUpdateAllResultsFailed!();
        f.Rig.Tick();
    }

    /// <summary>
    /// The production entry as the operator's robot drives it: a non-simulated robot (the fixture's development firmware) sends the
    /// hardware bundles' frames as encoding-8 minimized JPEG chunks (M3-050) with an ImageImuData of zero rates; the frame goes
    /// SetNextImage -> the vision worker -> the gray decode -> ApplyCLAHE and the pick -> MarkerDetector.Detect -> the rolling-shutter
    /// correction (row 38) -> the frame result. The result's types and corners are the oracle's Detect output bit for bit.
    /// reach: production (robot messages through the dispatch; supplied inputs: the frames and the zero-rate IMU message).
    /// MISSING (row 38, M11-033): the oracle stops at Detect, so the rolling-shutter correction is not checked against the engine; with
    /// zero rates the stack's correction leaves the markers unchanged, which is what this test sees, not an engine-backed claim.
    /// </summary>
    [Fact]
    public void RealMiniJpegFramesFromAPhysicalRobotGiveTheEnginesMarkers()
    {
        var d = Fixture_.Value;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "re-analysis", "acceptance", "hardware"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var picks = d.Cases.Where(c => c.Clahe && c.Image.StartsWith("jpeg:") && c.Markers.Length > 0).Take(4).ToList();
        Assert.True(picks.Count >= 3);
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var vision = f.M.Vision;
        Assert.True(f.Robot.IsPhysicalRobot);
        vision.SetNativeOuterCalibration(vision.Calibration!);
        vision.Enabled = true;
        vision.ModeEnableMask = 1 << VisionSystem.DetectingMarkers;
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        var results = new List<VisionFrameResult>();
        void OnFrame(VisionFrameResult r) { lock (results) results.Add(r); }
        vision.FrameProcessed += OnFrame;
        try
        {
            uint id = 700;
            foreach (var c in picks)
            {
                lock (results) results.Clear();
                SendMiniJpegFrame(f, id++, ToMiniJpeg(File.ReadAllBytes(Path.Combine(dir!.FullName, d.Jpegs[c.Image]!))));
                VisionFrameResult r;
                lock (results) r = Assert.Single(results);
                Assert.Equal(c.Markers.Length, r.Markers.Count);
                for (int i = 0; i < c.Markers.Length; i++)
                {
                    Assert.Equal(c.Markers[i].Type, (int)r.Markers[i].Code);
                    for (int k = 0; k < 4; k++)
                    {
                        Assert.Equal(c.Markers[i].Corners[2 * k], Bits(r.Markers[i].Corners[k].X));
                        Assert.Equal(c.Markers[i].Corners[2 * k + 1], Bits(r.Markers[i].Corners[k].Y));
                    }
                }
            }
        }
        finally { vision.FrameProcessed -= OnFrame; }
    }

    /// <summary>
    /// Rows 18, 35 through VisionSystem::Update: a frame with more than 100 quads makes Detect return 0x05000000; Update logs the Error
    /// VisionSystem.Update.DetectMarkersFailed (0x006B518A..0x006B51A0), sets _errG (0x006B51DA) and leaves through 0x006B5692 (no marker
    /// processed bit, no later mode, no frame result); the Processor warns VisionComponent.UpdateVisionSystem.UpdateFailed (0x00653D46).
    /// The M1-034 skip is logged. reach: unit (substituted: sim firmware and raw encoding-1 chunks carry the synthetic ring image, which
    /// is not a camera JPEG; the image-processed report is observed on the link).
    /// </summary>
    [Fact]
    public void AFrameWithQuadsBeyondTheListFailsTheWholeUpdate()
    {
        var d = Fixture_.Value;
        var c = d.Cases.First(x => x.Quads.Length > 100 && x.Clahe);
        bool oldFlag = EngineErrorState.ErrorFlagSet;
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var vision = f.M.Vision;
        f.Robot.HandleFirmwareVersion(new FirmwareVersion { Signature = Encoding.UTF8.GetBytes("{\"sim\":true}") });
        vision.SetNativeOuterCalibration(vision.Calibration!);
        vision.Enabled = true;
        vision.ModeEnableMask = (1 << VisionSystem.DetectingMarkers) | (1 << 8);
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        var results = new List<VisionFrameResult>();
        var reports = new List<ProcessedImageReport>();
        var logs = new List<EngineLogEntry>();
        var missing = new List<string>();
        void OnFrame(VisionFrameResult r) { lock (results) results.Add(r); }
        void OnLog(EngineLogEntry e) { lock (logs) logs.Add(e); }
        void OnMissing(string m) { lock (missing) missing.Add(m); }
        vision.FrameProcessed += OnFrame;
        f.Rig.Engine.LogEntry += OnLog;
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += OnMissing;
        using var sub = f.Rig.Engine.AnimationMessages.SubscribeMessage(0x43, true, p => { lock (reports) reports.Add(Assert.IsType<ProcessedImageReport>(p)); });
        try
        {
            EngineErrorState.ErrorFlagSet = false;
            f.NativeCameraFrame(900, d.Images[c.Image].Pixels);
            f.Rig.Engine.VisionUpdateAllResultsFailed!();
            for (int i = 0; i < 5; i++) f.Rig.Tick();
            lock (results) Assert.Empty(results);
            lock (reports) Assert.Empty(reports);
            lock (logs)
            {
                var names = logs.Select(l => l.Name).ToList();
                int e = names.IndexOf("VisionSystem.Update.DetectMarkersFailed"), w = names.IndexOf("VisionComponent.UpdateVisionSystem.UpdateFailed");
                Assert.True(e >= 0 && w > e, string.Join(",", names));
                Assert.Equal(EngineLogLevel.Error, logs[e].Level);
                Assert.Equal(EngineLogLevel.Warning, logs[w].Level);
            }
            Assert.True(EngineErrorState.ErrorFlagSet);
            lock (missing) Assert.Contains(missing, m => m.Contains("M1-034/M11-023") && m.Contains("beyond the 100-slot marker list"));
        }
        finally
        {
            vision.FrameProcessed -= OnFrame;
            f.Rig.Engine.LogEntry -= OnLog;
            SteppedBehavior.MissingReported -= OnMissing;
            EngineErrorState.ErrorFlagSet = oldFlag;
        }
    }

    /// <summary>
    /// Real frames through the robot link with the marker mode on; the frame result's markers are the oracle's engine-path markers bit for
    /// bit. reach: unit (substituted: sim firmware, so the rolling-shutter correction of row 38 is off, and raw encoding-1 chunks instead of
    /// the robot's encoding-8 JPEG; RealMiniJpegFramesFromAPhysicalRobotGiveTheEnginesMarkers is the production-faithful one).
    /// </summary>
    [Fact]
    public void RealFramesThroughTheRobotLinkGiveTheEnginesMarkers()
    {
        var d = Fixture_.Value;
        var picks = d.Cases.Where(c => c.Clahe && c.Image.StartsWith("jpeg:") && c.Markers.Length > 0).Take(4).ToList();
        Assert.True(picks.Count >= 3);
        using var f = new PickupPyramidNativeExecutionTests.Fixture();
        var vision = f.M.Vision;
        f.Robot.HandleFirmwareVersion(new FirmwareVersion { Signature = Encoding.UTF8.GetBytes("{\"sim\":true}") });
        vision.SetNativeOuterCalibration(vision.Calibration!);
        vision.Enabled = true;
        vision.ModeEnableMask = 1 << VisionSystem.DetectingMarkers;
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        var results = new List<VisionFrameResult>();
        void OnFrame(VisionFrameResult r) { lock (results) results.Add(r); }
        vision.FrameProcessed += OnFrame;
        try
        {
            uint id = 500;
            foreach (var c in picks)
            {
                lock (results) results.Clear();
                f.NativeCameraFrame(id++, d.Images[c.Image].Pixels);
                f.Rig.Engine.VisionUpdateAllResultsFailed!();
                f.Rig.Tick();
                VisionFrameResult r;
                lock (results) r = Assert.Single(results);
                Assert.Equal(c.Markers.Length, r.Markers.Count);
                for (int i = 0; i < c.Markers.Length; i++)
                {
                    Assert.Equal(c.Markers[i].Type, (int)r.Markers[i].Code);
                    Assert.Equal(r.Timestamp, r.Markers[i].Timestamp);
                    for (int k = 0; k < 4; k++)
                    {
                        Assert.Equal(c.Markers[i].Corners[2 * k], Bits(r.Markers[i].Corners[k].X));
                        Assert.Equal(c.Markers[i].Corners[2 * k + 1], Bits(r.Markers[i].Corners[k].Y));
                    }
                }
            }
        }
        finally { vision.FrameProcessed -= OnFrame; }
    }

    /// <summary>The real frames are the hardware bundles' grey JPEGs; the stack's decoder (GrayImage.FromJpeg, cv::imdecode IMREAD_GRAYSCALE)
    /// gives the pixels the generator took from the shipped decoder. reach: unit.</summary>
    [Fact]
    public void TheRealFramesDecodeToTheFixturePixels()
    {
        var d = Fixture_.Value;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "re-analysis", "acceptance", "hardware"))) dir = dir.Parent;
        Assert.NotNull(dir);
        int n = 0;
        foreach (var (name, rel) in d.Jpegs)
        {
            if (rel is null) continue;
            var img = GrayImage.FromJpeg(File.ReadAllBytes(Path.Combine(dir!.FullName, rel)));
            Assert.Equal(d.Images[name].Pixels, img.Pixels);
            n++;
        }
        Assert.True(n >= 20, $"{n} real frames");
    }
}
