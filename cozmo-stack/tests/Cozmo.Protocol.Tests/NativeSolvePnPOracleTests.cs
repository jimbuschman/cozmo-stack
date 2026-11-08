using System.Globalization;
using System.Text;
using System.Text.Json;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M11-054, cv::solvePnP as the engine calls it (engine 0x0085D89E..0x0085D8A2, rows E02..E06 of
/// re-analysis/research/20261004-solvepnp-extraction.md). Every expected value is what the shipped libopencv_calib3d, imgproc and core
/// returned when re-analysis/tools/emu/gen_solvepnp_fixtures.py ran them under Unicorn with the engine's own wrappers (emu_solvepnp.py):
/// never what the C# returns. Inputs and outputs are hex bit patterns. reach: unit (the solver alone; PoseEstimation.Solve's join is tested below).
/// </summary>
[Collection("SteppedBehavior missing-report statics")]
public class NativeSolvePnPOracleTests
{
    private sealed record Case(string Name, float[] Obj, float[] Img, float[] K, float[] Dist, ulong[] Rvec, ulong[] Tvec, Dictionary<(string, ulong), ulong> Libm);

    private static readonly Lazy<(JsonElement Root, List<Case> Cases)> Data = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "solvepnp", "solvepnp_oracle.json");
        var root = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        var cases = new List<Case>();
        foreach (var c in root.GetProperty("cases").EnumerateArray())
            cases.Add(new Case(c.GetProperty("name").GetString()!, Floats(c, "obj"), Floats(c, "img"), Floats(c, "k"), Floats(c, "dist"), Doubles(c, "rvec"), Doubles(c, "tvec"), LibmTable(c)));
        return (root, cases);
    });

    private static float[] Floats(JsonElement c, string key) =>
        c.GetProperty(key).EnumerateArray().Select(v => BitConverter.UInt32BitsToSingle(uint.Parse(v.GetString()!, NumberStyles.HexNumber))).ToArray();

    private static ulong[] Doubles(JsonElement c, string key) =>
        c.GetProperty(key).EnumerateArray().Select(v => ulong.Parse(v.GetString()!, NumberStyles.HexNumber)).ToArray();

    // The oracle's host-libm results for this case (gen_solvepnp_fixtures.py records every cos, sin, acos and exp call). Replaying them makes the
    // comparison independent of this host's libm, which differs in the last bit between Windows builds (two cases failed on another PC's .NET
    // 9.0.3 host, 2026-11-07); libm itself is the M11-099 EQUIVALENT_IMPLEMENTATION boundary, everything else stays bit-exact.
    private static Dictionary<(string, ulong), ulong> LibmTable(JsonElement c)
    {
        var table = new Dictionary<(string, ulong), ulong>();
        if (c.TryGetProperty("libm", out var libm))
            foreach (var e in libm.EnumerateArray())
            {
                var row = e.EnumerateArray().Select(v => v.GetString()!).ToArray();
                table[(row[0], ulong.Parse(row[1], NumberStyles.HexNumber))] = ulong.Parse(row[2], NumberStyles.HexNumber);
            }
        return table;
    }

    // A call the oracle never made means the arithmetic before it already diverged: that is a failure, never a host-libm fallback.
    private static T WithOracleLibm<T>(Case c, Func<T> run)
    {
        NativeSolvePnP.LibmOverride = (name, x) => c.Libm.TryGetValue((name, BitConverter.DoubleToUInt64Bits(x)), out var y)
            ? BitConverter.UInt64BitsToDouble(y)
            : throw new InvalidOperationException($"{name}({x:R}, 0x{BitConverter.DoubleToUInt64Bits(x):X16}) was never called by the oracle");
        try { return run(); }
        finally { NativeSolvePnP.LibmOverride = null; }
    }

    private static long Ulps(ulong expected, double actual)
    {
        static long Ordered(ulong b) => (b >> 63) != 0 ? -(long)(b & 0x7FFFFFFFFFFFFFFF) : (long)b;
        return Math.Abs(Ordered(expected) - Ordered(BitConverter.DoubleToUInt64Bits(actual)));
    }

    [Fact]
    public void TheCorpusCoversTheBranchesTheJobAsksFor()
    {
        var cases = Data.Value.Cases;
        Assert.True(cases.Count >= 40, $"{cases.Count} cases");
        Assert.Contains(cases, c => c.Obj.Length == 12);                                  // four coplanar corners
        Assert.Contains(cases, c => c.Obj.Length == 18);
        Assert.Contains(cases, c => c.Obj.Length == 24);                                  // two faces
        Assert.Contains(cases, c => c.Obj.Length == 36);                                  // three faces
        Assert.Equal(0, Data.Value.Root.GetProperty("neon_differences").GetArrayLength());
    }

    /// <summary>M11-054 rows E05, O07, I01..I07, U01..U09, H01..H07, J01..J07, S01..S13, B01..B09, G01..G14, N01..N05, L01..L15, P01..P09, R02..R08.</summary>
    [Fact]
    public void EveryOracleCaseMatchesTheShippedSolverBitForBit()
    {
        var report = new StringBuilder();
        long worst = 0; int mismatched = 0;
        foreach (var c in Data.Value.Cases)
        {
            NativeSolvePnP.Output r;
            try { r = WithOracleLibm(c, () => NativeSolvePnP.Solve(c.Obj, c.Img, c.K, c.Dist)); }
            catch (Exception e) { mismatched++; report.AppendLine($"{c.Name}: {e.GetType().Name} {e.Message}"); continue; }
            long caseWorst = 0;
            for (int i = 0; i < 3; i++)
            {
                caseWorst = Math.Max(caseWorst, Ulps(c.Rvec[i], r.Rvec[i]));
                caseWorst = Math.Max(caseWorst, Ulps(c.Tvec[i], r.Tvec[i]));
            }
            worst = Math.Max(worst, caseWorst);
            if (caseWorst != 0)
            {
                mismatched++;
                report.AppendLine($"{c.Name}: {caseWorst} ulp; rvec {string.Join(",", r.Rvec)} vs {string.Join(",", c.Rvec.Select(BitConverter.UInt64BitsToDouble))}; tvec {string.Join(",", r.Tvec)} vs {string.Join(",", c.Tvec.Select(BitConverter.UInt64BitsToDouble))}");
            }
        }
        Assert.True(mismatched == 0, $"{mismatched}/{Data.Value.Cases.Count} differ, worst {worst} ulp\n{report}");
    }

    /// <summary>M11-054 rows E02, E04, E06 (engine 0x0085DF98..0x0085E0BD, 0x0085D8AC..0x0085D8E2): the live entry narrows the inputs to binary32 and
    /// each output component to binary32. Expected: the oracle's doubles, narrowed here. reach: unit (the stack's own entry, called directly; the
    /// production joins into BlockWorld and Docking are exercised by their own tests).</summary>
    [Fact]
    public void TheLiveEntryPassesTheEnginesBitsAndNarrowsTheOutput()
    {
        int tested = 0;
        foreach (var c in Data.Value.Cases.Where(c => c.Obj.Length == 12 && c.Dist.Length == 8))
        {
            var cal = new CameraCalibration
            {
                FocalLengthX = c.K[0], FocalLengthY = c.K[4], CenterX = c.K[2], CenterY = c.K[5], Skew = c.K[1], Rows = 240, Columns = 320,
                DistortionCoefficients = Array.ConvertAll(c.Dist, v => (double)v),
            };
            var obj = Enumerable.Range(0, c.Obj.Length / 3).Select(i => new Vec3(c.Obj[i * 3], c.Obj[i * 3 + 1], c.Obj[i * 3 + 2])).ToList();
            var img = Enumerable.Range(0, c.Img.Length / 2).Select(i => new Vec2(c.Img[i * 2], c.Img[i * 2 + 1])).ToList();
            var r = WithOracleLibm(c, () => PoseEstimation.Solve(new CameraModel(cal, Pose3d.Identity), obj, img));
            Assert.NotNull(r);
            float[] tvec = c.Tvec.Select(b => (float)BitConverter.UInt64BitsToDouble(b)).ToArray();
            Assert.Equal(tvec[0], (float)r!.ObjectInCamera.Translation.X);
            Assert.Equal(tvec[1], (float)r.ObjectInCamera.Translation.Y);
            Assert.Equal(tvec[2], (float)r.ObjectInCamera.Translation.Z);
            var rvec = c.Rvec.Select(b => (float)BitConverter.UInt64BitsToDouble(b)).ToArray();
            Assert.Equal(Rotation3d.FromVector(new Vec3(rvec[0], rvec[1], rvec[2])), r.ObjectInCamera.Quaternion);
            tested++;
        }
        Assert.True(tested > 100, $"{tested} cases");
    }

    /// <summary>M11-054 row H03: a planar set above four points reaches the homography's LM refinement, which has no row. The stack must say so, not guess.
    /// reach: unit (the only stack caller that can pass more than four points is BlockWorld's joint refine, and its corners span faces).</summary>
    [Fact]
    public void APlanarSetAboveFourPointsIsReportedMissingNotGuessed()
    {
        var plane = Enumerable.Range(0, 6).Select(i => new Vec3(i * 10.0 - 25.0, (i % 3) * 7.0, 0)).ToList();
        var image = plane.Select(p => new Vec2(160 + p.X * 2, 120 + p.Y * 2)).ToList();
        var cam = new CameraModel(CameraCalibration.Nominal(), Pose3d.Identity);
        var missing = new List<string>();
        void Record(string text) => missing.Add(text);
        SteppedBehavior.MissingReported += Record;
        try { Assert.Null(PoseEstimation.Solve(cam, plane, image)); }
        finally { SteppedBehavior.MissingReported -= Record; }
        Assert.Contains(missing, t => t.Contains("M11-054") && t.Contains("H03"));
    }

    /// <summary>M11-054 rows Q01, E02: below four points and on a count mismatch the stack's entry returns null. No caller passes either: a marker has four corners.</summary>
    [Fact]
    public void TheEntryRefusesCountsNoCallerProduces()
    {
        var cam = new CameraModel(CameraCalibration.Nominal(), Pose3d.Identity);
        var three = new List<Vec3> { new(0, 0, 0), new(10, 0, 0), new(0, 10, 0) };
        Assert.Null(PoseEstimation.Solve(cam, three, new List<Vec2> { new(1, 1), new(2, 2), new(3, 1) }));
        Assert.Null(PoseEstimation.Solve(cam, three, new List<Vec2> { new(1, 1) }));
    }
}
