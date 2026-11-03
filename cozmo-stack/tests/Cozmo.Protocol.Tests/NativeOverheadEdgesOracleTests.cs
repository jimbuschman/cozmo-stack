using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Robot.Manipulation;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M11-017, the overhead-edge detector <c>OverheadEdgesDetector::Detect</c> 0x006ABE34 (inventory
/// re-analysis/research/20261103-visit-edge-detector-extraction.md, rows 1-31). Every expected value is what the engine's own Detect returned
/// when re-analysis/tools/emu/gen_overhead_edges_fixtures.py ran it under Unicorn over the shipped libcozmoEngine.so and OpenCV
/// (emu_overhead_edges.py), never what the C# returns. Inputs and outputs are bit patterns.
/// The oracle runs with a null camera calibration, so both lift points fail to project and the lift gate always proceeds (row 9): the
/// lift gate (OverheadEdgesDetector.LiftIsOutOfTheWay, Project3dPoint polynomial RECOVERABLE_GAP) is OUTSIDE this proof, and the
/// comparisons call the detector with no gate.
/// </summary>
[Collection("SteppedBehavior missing-report statics")]
public class NativeOverheadEdgesOracleTests
{
    internal sealed record Point(uint X, uint Y, uint C0, uint C1, uint C2);
    internal sealed record Chain(int Kind, Point[] Points);
    internal sealed record Frame(uint Ts, int Flag, uint[] Quad, Chain[] Chains);
    internal sealed record Probe(int Hits, int HitProjectFailed, int[] Clamp, int BadProjectedZ);
    internal sealed record Case(string Name, string Image, string HName, float[] H, uint Lift, uint Status, bool Visible, uint Ts, int Ret,
                                Probe Probe, Frame[] Frames);

    internal sealed record Data(JsonElement Root, Dictionary<string, GrayImage> Images, Dictionary<string, string?> Jpegs, List<Case> Cases);

    internal static readonly Lazy<Data> Fixture_ = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "overhead_edges", "overhead_edges_oracle.json");
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
        var cases = new List<Case>();
        foreach (var c in root.GetProperty("cases").EnumerateArray())
        {
            var frames = c.GetProperty("frames").EnumerateArray().Select(f => new Frame(
                f.GetProperty("ts").GetUInt32(), f.GetProperty("flag").GetInt32(),
                f.GetProperty("quad").EnumerateArray().Select(Hex).ToArray(),
                f.GetProperty("chains").EnumerateArray().Select(ch => new Chain(ch.GetProperty("kind").GetInt32(),
                    ch.GetProperty("points").EnumerateArray().Select(p =>
                    {
                        var s = p.GetString()!;
                        uint W(int i) => uint.Parse(s.AsSpan(i * 8, 8), NumberStyles.HexNumber);
                        return new Point(W(0), W(1), W(2), W(3), W(4));
                    }).ToArray())).ToArray())).ToArray();
            var pr = c.GetProperty("probe");
            cases.Add(new Case(c.GetProperty("name").GetString()!, c.GetProperty("image").GetString()!, c.GetProperty("h_name").GetString()!,
                c.GetProperty("h").EnumerateArray().Select(v => BitConverter.UInt32BitsToSingle(Hex(v))).ToArray(),
                Hex(c.GetProperty("lift")), c.GetProperty("status").GetUInt32(), c.GetProperty("visible").GetInt32() != 0,
                c.GetProperty("ts").GetUInt32(), c.GetProperty("ret").GetInt32(),
                new Probe(pr.GetProperty("hits").GetInt32(), pr.GetProperty("hit_project_failed").GetInt32(),
                          pr.GetProperty("clamp").EnumerateArray().Select(v => v.GetInt32()).ToArray(), pr.GetProperty("bad_projected_z_logs").GetInt32()),
                frames));
        }
        return new Data(root, images, jpegs, cases);
    });

    private static uint Hex(JsonElement v) => uint.Parse(v.GetString()!, NumberStyles.HexNumber);

    private static uint Bits(double v) => BitConverter.SingleToUInt32Bits((float)v);

    /// <summary>Compares one stack frame (or its absence) with the oracle's; returns the first difference, or null.</summary>
    internal static string? Compare(Case c, OverheadEdgeFrame? got)
    {
        if (c.Frames.Length == 0) return got is null ? null : "the engine appended no frame; the stack returned one";
        if (got is null) return "the engine appended a frame; the stack returned none";
        var want = c.Frames[0];
        if (want.Ts != got.Timestamp) return $"ts {got.Timestamp} != {want.Ts}";
        if ((want.Flag != 0) != got.GroundPlaneValid) return $"flag {got.GroundPlaneValid} != {want.Flag}";
        for (int i = 0; i < 4; i++)
        {
            if (Bits(got.GroundQuad[i].X) != want.Quad[2 * i] || Bits(got.GroundQuad[i].Y) != want.Quad[2 * i + 1])
                return $"quad[{i}] ({got.GroundQuad[i].X},{got.GroundQuad[i].Y}) != ({BitConverter.UInt32BitsToSingle(want.Quad[2 * i])},{BitConverter.UInt32BitsToSingle(want.Quad[2 * i + 1])})";
        }
        if (want.Chains.Length != got.Chains.Count) return $"{got.Chains.Count} chains != {want.Chains.Length} ({string.Join(",", got.Chains.Select(x => x.Points.Count))} vs {string.Join(",", want.Chains.Select(x => x.Points.Length))})";
        for (int k = 0; k < want.Chains.Length; k++)
        {
            var wc = want.Chains[k];
            var gc = got.Chains[k];
            if ((wc.Kind == 1) != gc.IsBorder) return $"chain {k} kind {gc.IsBorder} != {wc.Kind}";
            if (wc.Points.Length != gc.Points.Count) return $"chain {k}: {gc.Points.Count} points != {wc.Points.Length}";
            for (int p = 0; p < wc.Points.Length; p++)
            {
                var w = wc.Points[p];
                var g = gc.Points[p];
                uint r = Bits(g.Gradient);
                if (Bits(g.Ground.X) != w.X || Bits(g.Ground.Y) != w.Y || r != w.C0 || r != w.C1 || r != w.C2)
                    return $"chain {k} point {p}: ({g.Ground.X},{g.Ground.Y},{g.Gradient}) != ({BitConverter.UInt32BitsToSingle(w.X)},{BitConverter.UInt32BitsToSingle(w.Y)},{BitConverter.UInt32BitsToSingle(w.C0)})";
            }
        }
        return null;
    }

    /// <summary>M11-017 rows 4-8, 10-31: every oracle case, bit for bit (frame presence, flag, timestamp, quad, chain kinds, points and the
    /// response copied three times). reach: unit (NativeOverheadEdges.Detect called directly with the case's VisionPoseData fields and no lift
    /// gate; the production join is tested below).</summary>
    [Fact]
    public void EveryOracleCaseMatchesTheEnginesDetectBitForBit()
    {
        var d = Fixture_.Value;
        var report = new StringBuilder();
        int mismatched = 0;
        foreach (var c in d.Cases)
        {
            Assert.Equal(0, c.Ret);
            int ret = NativeOverheadEdges.Detect(d.Images[c.Image], c.H, c.Visible, c.Status, c.Ts, null, out var frame);
            string? diff = ret != c.Ret ? $"ret {ret}" : Compare(c, frame);
            if (diff is not null) { mismatched++; if (mismatched <= 25) report.AppendLine($"{c.Name}: {diff}"); }
        }
        Assert.True(mismatched == 0, $"{mismatched}/{d.Cases.Count} differ\n{report}");
    }

    /// <summary>The public detector entry gives the same frames (no lift angle, so no gate). reach: unit.</summary>
    [Fact]
    public void ThePublicDetectorEntryGivesTheSameFrames()
    {
        var d = Fixture_.Value;
        var det = new OverheadEdgesDetector();
        var cam = new CameraModel(CameraCalibration.Nominal(), HeadGeometry.CameraPoseInRobot(0));
        int n = 0;
        foreach (var c in d.Cases.Where((_, i) => i % 7 == 0))
        {
            var frame = det.Detect(d.Images[c.Image], new OverheadEdgeGroundInput(c.H, c.Visible, c.Status), cam, Pose3d.Identity, c.Ts, null);
            Assert.Null(Compare(c, frame));
            n++;
        }
        Assert.True(n > 20, $"{n} cases");
    }

    /// <summary>
    /// The corpus reaches every row the extraction marked DIFFERS or NOT IN STACK. The oracle's own probes (hit at 0x006AD218, ClampQuad
    /// result at 0x004F7812, the BadProjectedZ warning) and the expected frames show it; for the int16 rounding (row 17) the float sums are
    /// read from the port only to show the corpus has sums in (50, 50.5], where the old double detector found an edge and the engine does not.
    /// </summary>
    [Fact]
    public void TheCorpusCoversTheRowsThatDiffer()
    {
        var d = Fixture_.Value;
        var cases = d.Cases;
        Assert.True(cases.Count >= 150, $"{cases.Count} cases");
        Assert.Equal(0, d.Root.GetProperty("neon_differences").GetArrayLength());
        // row 4: ground plane not visible -> flag 0, zero quad, no chains
        Assert.Contains(cases, c => !c.Visible && c.Frames.Length == 1 && c.Frames[0].Flag == 0 && c.Frames[0].Quad.All(b => b == 0));
        // row 5: the status gate -> no frame; and statuses that pass
        Assert.Contains(cases, c => c.Visible && (c.Status & 0x102) != 0x100 && c.Frames.Length == 0);
        Assert.Contains(cases, c => c.Visible && (c.Status & 0x102) == 0x100 && c.Status != 0x100 && c.Frames.Length == 1);
        // rows 8, 28: clamp fired and not fired
        Assert.Contains(cases, c => c.Probe.Clamp.SequenceEqual(new[] { 1 }) && c.Frames[0].Chains.Length > 0);
        Assert.Contains(cases, c => c.Probe.Clamp.SequenceEqual(new[] { 0 }) && c.Frames[0].Chains.Length > 0);
        // row 11: a clipped rect with no hits -> flag 1, no chains. The empty-rect branch itself (head up more than about 10 degrees,
        // Array2d.GetROI.EmptyRect) is not in this corpus; the POSE-EDGE verifier's sweep matched it in 80 cases. Queued: add such a case.
        Assert.Contains(cases, c => c.HName is "looking_up" or "yaw_away" && c.Frames.Length == 1 && c.Frames[0].Flag == 1 && c.Frames[0].Chains.Length == 0 && c.Probe.Hits == 0);
        // row 25: a hit whose projection fails, in a frame that still has clear reports
        Assert.Contains(cases, c => c.Probe.HitProjectFailed > 0 && c.Frames[0].Chains.Any(ch => ch.Kind == 0));
        // row 24: BadProjectedZ
        Assert.Contains(cases, c => c.Probe.BadProjectedZ > 0);
        // row 26: quad[0].x > quad[2].x (mirrored) -> no clear report at all
        Assert.Contains(cases, c => c.HName == "mirrored" && c.Image == "flat120" && c.Frames[0].Chains.Length == 0);
        // row 18: slanted quads (rolled cameras) with edges
        Assert.Contains(cases, c => c.HName.StartsWith("roll") && c.Frames[0].Chains.Any(ch => ch.Kind == 1));
        // row 20: a rank-2 H. Its f32 determinant is -0.015625, so the zero-matrix branch 0x004F8230 is not reached here; that branch
        // matches the binary by reading.
        Assert.Contains(cases, c => c.HName == "rank2");
        // real camera frames
        Assert.True(cases.Count(c => c.Image.StartsWith("jpeg:")) >= 20);
        // row 17: sums in (50, 50.5] and (50.5, 51) inside the scanned rect of a case whose frame has chains
        int lowBand = 0, highBand = 0;
        foreach (var c in cases.Where(c => c.Image.StartsWith("thresh_")))
        {
            var img = d.Images[c.Image];
            for (int y = 100; y < img.Height; y++)
                for (int x = 0; x < img.Width; x += 3)
                {
                    float s = MathF.Abs(NativeOverheadEdges.RawResponse(img, x, y));
                    if (s > 50f && s <= 50.5f) lowBand++;
                    else if (s > 50.5f && s < 51f) highBand++;
                }
        }
        Assert.True(lowBand > 0 && highBand > 0, $"{lowBand} sums in (50, 50.5], {highBand} in (50.5, 51)");
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
        Assert.True(n >= 5, $"{n} real frames");
    }

    /// <summary>
    /// The production join: VisionSystem reads Detect's inputs from the frame's captured ground carrier (VisionPoseData H +0x7C, visible
    /// +0x78, status +0x5C; rows 4-6) and sets the processed-mode bit 0x10 on Detect's zero return whether or not a frame was appended
    /// (row 2, 0x006B540A..0x006B5416). Expected frames are the oracle's. The lift gate is live on this path; the test first checks it lets the frame
    /// through (that check is the stack's own gate, outside the proof). reach: unit (ProcessNativeCapturedForTest supplies the carrier and
    /// the image; production builds the carrier in SetNextImage from the LUT).
    /// </summary>
    [Fact]
    public void TheCapturedCarrierReachesDetectAndTheProcessedBitIsSet()
    {
        var d = Fixture_.Value;
        using var f = new Fixture();
        var vision = f.M.Vision;
        var owner = f.Robot;
        var engine = f.Rig.Engine;
        vision.ModeEnableMask = 1 << (int)VisionMode.DetectingOverheadEdges;
        vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
        vision.OverheadEdges ??= new OverheadEdgesDetector();
        var reports = new List<ProcessedImageReport>();
        var results = new List<VisionFrameResult>();
        using var sub = engine.AnimationMessages.SubscribeMessage(0x43, true, p => reports.Add(Assert.IsType<ProcessedImageReport>(p)));
        void OnFrame(VisionFrameResult r) => results.Add(r);
        vision.FrameProcessed += OnFrame;
        try
        {
            float lift = (float)Math.Asin((LiftPresets.CarryMm - 45.0) / LiftGeometry.ArmLengthMm);
            uint id = 1;
            foreach (var name in new[] { "probe_clamp__step_probe", "not_visible__step_probe", "status_00000102", "status_00000300" })
            {
                var c = d.Cases.Single(x => x.Name == name);
                using var pose = new Pose3d(Rotation3d.Identity, Vec3.Zero);
                var raw = new RobotState { Timestamp = c.Ts, HeadAngle = -0.15f, LiftAngle = lift, Status = c.Status, Pose = new RobotPose(), Accel = new AccelData(), Gyro = new GyroData(), CliffDataRaw = new ushort[4] };
                using var history = HistRobotState.From(pose, raw);
                using var ground = new NativeGroundFrame(owner, c.Ts, history, c.Visible, c.H);
                reports.Clear();
                results.Clear();
                vision.ProcessNativeCapturedForTest(owner, ground, d.Images[c.Image], id++, c.Ts);
                engine.VisionUpdateAllResultsFailed!();
                var report = Assert.Single(reports);
                Assert.Contains(4u, report.Modes);                                   // row 2: bit 0x10 on the zero return
                var result = Assert.Single(results);
                if (c.Frames.Length == 1 && c.Frames[0].Flag == 1)
                {
                    // the stack's lift gate on this frame's carrier pose: outside the proof, checked so the comparison is meaningful
                    using var cameraPose = HeadGeometry.CameraPoseInWorld(pose, raw.HeadAngle);
                    var cam = new CameraModel(vision.Calibration!, cameraPose);
                    var rect = Rect(c.H);
                    Assert.True(OverheadEdgesDetector.LiftIsOutOfTheWay(cam, pose, lift, rect.Top, rect.Bottom));
                }
                Assert.Null(Compare(c, result.OverheadEdges));
            }
        }
        finally { vision.FrameProcessed -= OnFrame; }
    }

    private static (int Top, int Bottom) Rect(float[] h)
    {
        var quad = NativeGroundPlaneProjection.Project(h, 320, 240);
        int minY = quad.Min(p => NativeOverheadEdges.TruncS32(p.Y)), maxY = quad.Max(p => NativeOverheadEdges.TruncS32(p.Y));
        return (minY, maxY);
    }

    /// <summary>
    /// The production entry: a camera frame through the robot link (CameraGrayFrame -> SetNextImage -> the worker) with the lift not in
    /// position. Row 5 (0x006AC090..0x006AC09C): (status &amp; 0x102) != 0x100 returns 0 with no frame, and row 2 still sets the
    /// processed bit 0x10 (0x006B540A..0x006B5416). reach: production (robot state and image chunks through the link, the calibrated LUT carrier).
    /// </summary>
    [Fact]
    public void ALiftNotInPositionFrameIsProcessedButAppendsNoEdgeFrame()
    {
        using var f = new Fixture();
        var vision = f.M.Vision;
        var engine = f.Rig.Engine;
        Assert.True(SpinWait.SpinUntil(() => vision.Enabled, TimeSpan.FromSeconds(5)));
        var reports = new List<ProcessedImageReport>();
        var results = new List<VisionFrameResult>();
        using var sub = engine.AnimationMessages.SubscribeMessage(0x43, true, p => reports.Add(Assert.IsType<ProcessedImageReport>(p)));
        void OnFrame(VisionFrameResult r) { lock (results) results.Add(r); }
        vision.FrameProcessed += OnFrame;
        try
        {
            var state = f.Rig.MakeState(RobotStatusFlag.HeadInPos, origin: f.Robot.CurrentOriginId, head: -.15f);
            state.Timestamp = 1000;
            f.Rig.Data(state); f.Rig.Tick();
            vision.ModeEnableMask = 1 << (int)VisionMode.DetectingOverheadEdges;
            vision.Schedules = AllVisionModesSchedule.InitDefaultSchedules();
            vision.OverheadEdges ??= new OverheadEdgesDetector();
            var pixels = new byte[320 * 240];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i / 320 > 170 ? 30 : 150);
            f.Rig.CameraGrayFrame(42, 1000, pixels);
            vision.WaitForProcessorIdle();
            for (int i = 0; i < 5 && reports.Count == 0; i++) f.Rig.Tick();
            var report = Assert.Single(reports);
            Assert.Contains(4u, report.Modes);
            lock (results) Assert.All(results, r => Assert.Null(r.OverheadEdges));
        }
        finally { vision.FrameProcessed -= OnFrame; }
    }
}
