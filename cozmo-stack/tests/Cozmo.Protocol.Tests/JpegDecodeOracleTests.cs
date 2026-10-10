using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision.Jpeg;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The camera decode (M3-001, M3-018): every expected value in these tests is what the shipped libraries returned when
/// <c>re-analysis/tools/emu/gen_jpeg_decode_fixtures.py</c> ran them under Unicorn (libcozmoEngine's EncodedImage::DecodeImageGray /
/// DecodeImageRGB, libopencv_imgcodecs' imdecode, libopencv_imgproc's resize and cvtColor, libc++_shared): never what the C# returns.
/// The fixtures are Fixtures/jpeg_decode/index.json (cases and expected values) and data.zip (inputs, and the outputs of the imdecode and
/// resize cases). Bytes the shipped code never writes (they differ between a 0x00-poisoned and a 0xFF-poisoned run) are recorded as
/// `excluded` ranges and skipped here; each case was also run with NEON reported present and absent and none differed.
/// Citations: rows J1..J115 of re-analysis/research/20261006-M3M4-rows-extraction.md, and the default-table row of
/// re-analysis/research/20261010-jpeg-defaults-and-save-rows.md.
/// </summary>
[Collection("SteppedBehavior missing-report statics")]       // the unsupported-encoding fixtures set the process-global _errG, which M3DeviceTests also reads
public class JpegDecodeOracleTests
{
    private sealed class Fixture
    {
        public readonly List<JsonElement> Cases = new();
        public readonly Dictionary<string, byte[]> Files = new();
        public JsonElement Root;
        public JsonElement Tables;
        public JsonElement Idct;
    }

    private static readonly Lazy<Fixture> Data = new(() =>
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "jpeg_decode");
        var f = new Fixture();
        f.Root = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "index.json"))).RootElement;
        f.Cases.AddRange(f.Root.GetProperty("cases").EnumerateArray());
        f.Tables = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "tables.json"))).RootElement;
        using var zip = ZipFile.OpenRead(Path.Combine(dir, "data.zip"));
        foreach (var e in zip.Entries)
        {
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            f.Files[e.FullName] = ms.ToArray();
        }
        f.Idct = JsonDocument.Parse(f.Files["idct.json"]).RootElement;
        return f;
    });

    private static IEnumerable<JsonElement> Of(string kind) => Data.Value.Cases.Where(c => c.GetProperty("kind").GetString() == kind);

    private static byte[] File_(JsonElement c, string prop) => Data.Value.Files[c.GetProperty(prop).GetString()!];

    private static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    /// <summary>Compares a Mat with a case's expected record; returns null when it matches, else what differs.</summary>
    private static string? CompareMat(JsonElement c, CvMat m)
    {
        var exp = c.GetProperty("expected");
        int er = exp.GetProperty("rows").GetInt32(), ec = exp.GetProperty("cols").GetInt32(), ech = exp.GetProperty("channels").GetInt32();
        if (er == 0)
            return m.Rows == 0 ? null : $"expected an empty Mat, got {m.Rows}x{m.Cols}";
        if (m.Rows != er || m.Cols != ec || m.Channels != ech)
            return $"shape expected {er}x{ec}x{ech}, got {m.Rows}x{m.Cols}x{m.Channels}";
        var data = (byte[])m.Data.Clone();
        foreach (var r in exp.GetProperty("excluded").EnumerateArray())
            for (int i = r[0].GetInt32(); i < r[1].GetInt32(); i++) data[i] = 0;
        if (c.TryGetProperty("output", out var outp))
        {
            var expd = Data.Value.Files[outp.GetString()!];
            var skip = new bool[expd.Length];
            foreach (var r in exp.GetProperty("excluded").EnumerateArray())
                for (int i = r[0].GetInt32(); i < r[1].GetInt32(); i++) skip[i] = true;
            int diff = 0, first = -1;
            for (int i = 0; i < expd.Length; i++)
                if (!skip[i] && expd[i] != m.Data[i]) { diff++; if (first < 0) first = i; }
            if (diff != 0) return $"{diff} differing bytes of {expd.Length}, first at {first}: expected {expd[first]}, got {m.Data[first]}";
        }
        return Sha(data) == exp.GetProperty("sha256").GetString() ? null : "sha256 of the decoded bytes differs";
    }

    private static string Id(JsonElement c) => c.GetProperty("id").GetString()!;

    // the shipped log lines of a decode in order, as the stack writes them: "error: Key: text", "warning: Key: text", and the line cv::error() wrote
    // to logcat as "error: cv::error(): OpenCV Error: ..." (a NULL %s is rendered "(null)", bionic's printf: no normalisation here)
    private static List<string> Logs(JsonElement exp) =>
        exp.GetProperty("logs").EnumerateArray().Select(x => x.GetString()!).ToList();

    private static string CvErrorLine(OpenCvException e) => "error: cv::error(): " + e.Message;

    // ---------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// M3-001, J5..J15, J46..J78, J103..J111, J114 and the default-table row: cv::imdecode(buf, flags) of the shipped libopencv_imgcodecs on
    /// natural, encoded, progressive, arithmetic, restarted, scaled, mutated and crafted-header JPEGs (IMREAD_GRAYSCALE and IMREAD_COLOR, the two
    /// flags the engine passes). The expected Mats, the empty Mats of every failing decode, and the cv::error text of the empty-input assertion
    /// (J6, J114, J115) come from the shipped code.
    /// </summary>
    [Fact]
    public void M3_001_J5_J115_ImdecodeMatchesTheShippedLibrary() => CheckImdecode(Of("imdecode"), 800);

    /// <summary>
    /// M3-001, rows L1..L18 of re-analysis/research/20261010-jpeg-lse-logs-oom-rows.md: the LSE marker (0xF8) through cv::imdecode of the shipped
    /// library: the valid 22-byte form (colour and gray output, every sampling layout), each failing check (before SOF, component count, length, id
    /// byte, every payload byte, truncation inside the segment), the non-RGB colour spaces that reach the 0x1C error, rgb1_rgb_convert and
    /// rgb1_gray_convert.
    /// </summary>
    [Fact]
    public void M3_001_L1_L18_TheLseMarkerMatchesTheShippedLibrary()
        => CheckImdecode(Of("imdecode").Where(c => Id(c).StartsWith("lse_", StringComparison.Ordinal)), 250);

    /// <summary>
    /// M3-001, rows O1..O8b of the same file: the Mat the shipped Mat::create cannot allocate. The 32-bit total overflow of setSize is
    /// cv::error(-211, line 323); a total that fits but cannot be allocated is cv::OutOfMemoryError (-4, alloc.cpp line 52, "Failed to allocate N
    /// bytes"). When malloc returns NULL is device state (the phone's memory, a hardware-only boundary): the cases are sizes the emulator's stand-in
    /// heap refuses and a .NET array cannot hold either; a .NET OutOfMemoryException stands for the failed malloc.
    /// </summary>
    [Fact]
    public void M3_001_O1_O8b_TheAllocationFailuresMatchTheShippedLibrary()
    {
        var cases = Of("imdecode").Where(c => Id(c).StartsWith("oom_", StringComparison.Ordinal)).ToList();
        CheckImdecode(cases, 8);
        var texts = cases.Select(c => Logs(c.GetProperty("expected")).Single()).ToList();
        Assert.Contains(texts, t => t.Contains("(Failed to allocate 4290250000 bytes) in void* cv::OutOfMemoryError(size_t)") && t.EndsWith("/alloc.cpp, line 52"));
        Assert.Contains(texts, t => t.Contains("The total matrix size does not fit to \"size_t\" type") && t.EndsWith("/matrix.cpp, line 323"));
    }

    private static void CheckImdecode(IEnumerable<JsonElement> cases, int minimum)
    {
        var failures = new List<string>();
        int n = 0;
        foreach (var c in cases)
        {
            n++;
            var input = File_(c, "input");
            int flags = c.GetProperty("flags").GetInt32();
            var exp = c.GetProperty("expected");
            try
            {
                var m = OpenCvImage.Imdecode(input, flags, out var stderrLine);
                if (exp.TryGetProperty("exception", out _)) { failures.Add($"{Id(c)}: expected a cv::Exception"); continue; }
                var why = CompareMat(c, m);
                if (why != null) failures.Add($"{Id(c)}: {why}");
                // libjpeg's standard error manager prints the first warning of a decode to stderr (the log call inside J11/J26/J27/J30/J50/J51)
                var shippedStderr = exp.GetProperty("stderr").EnumerateArray().Select(x => x.GetString()!).ToList();
                var ours = stderrLine == null ? new List<string>() : new List<string> { stderrLine };
                if (!shippedStderr.SequenceEqual(ours)) failures.Add($"{Id(c)}: stderr [{string.Join("|", ours)}] but the shipped decode printed [{string.Join("|", shippedStderr)}]");
            }
            catch (OpenCvException e)
            {
                if (!exp.TryGetProperty("exception", out _)) { failures.Add($"{Id(c)}: unexpected {e.Message}"); continue; }
                if (!Logs(exp).Contains(CvErrorLine(e))) failures.Add($"{Id(c)}: exception text '{e.Message}' is not the shipped '{string.Join("|", Logs(exp))}'");
            }
        }
        Assert.True(n >= minimum, $"only {n} imdecode fixtures");
        Assert.True(failures.Count == 0, $"{failures.Count} of {n} imdecode fixtures differ:\n" + string.Join("\n", failures.Take(25)));
    }

    /// <summary>
    /// M3-018, J37, J41..J45: cv::resize(INTER_LINEAR) of the shipped libopencv_imgproc for 1- and 3-channel 8-bit images (the engine's 160x240 to
    /// 320x240 among them), with the FPSCR round-to-nearest runtime assumption (J42).
    /// </summary>
    [Fact]
    public void M3_018_J37_J41_J45_ResizeMatchesTheShippedLibrary()
    {
        var failures = new List<string>();
        int n = 0;
        foreach (var c in Of("resize"))
        {
            n++;
            var input = File_(c, "input");
            var src = new CvMat(c.GetProperty("srows").GetInt32(), c.GetProperty("scols").GetInt32(), c.GetProperty("schannels").GetInt32(), input);
            var exp = c.GetProperty("expected");
            try
            {
                var m = OpenCvResize.ResizeLinear(src, c.GetProperty("dcols").GetInt32(), c.GetProperty("drows").GetInt32());
                if (exp.TryGetProperty("exception", out _)) { failures.Add($"{Id(c)}: expected a cv::Exception"); continue; }
                var why = CompareMat(c, m);
                if (why != null) failures.Add($"{Id(c)}: {why}");
            }
            catch (OpenCvException e)
            {
                if (!exp.TryGetProperty("exception", out _)) failures.Add($"{Id(c)}: unexpected {e.Message}");
                else if (!Logs(exp).Contains(CvErrorLine(e))) failures.Add($"{Id(c)}: exception text '{e.Message}' is not the shipped one");
            }
            catch (NotSupportedException)
            {
                failures.Add($"{Id(c)}: unported branch");
            }
        }
        Assert.True(n >= 60, $"only {n} resize fixtures");
        Assert.True(failures.Count == 0, $"{failures.Count} of {n} resize fixtures differ:\n" + string.Join("\n", failures.Take(25)));
    }

    // the log lines the helper wrote, in order, must be exactly the ones the shipped helper wrote (D2..D7 and the cv::error line, O8)
    private static string? CheckEngine(JsonElement c, EncodedImageDecodeResult r, List<string> ourLogs)
    {
        var exp = c.GetProperty("expected");
        var shipped = Logs(exp);
        if (!shipped.SequenceEqual(ourLogs)) return $"logs [{string.Join("|", ourLogs)}], the shipped helper wrote [{string.Join("|", shipped)}]";
        bool shippedFailed = exp.TryGetProperty("exception", out _) || exp.GetProperty("result").GetInt32() != 0;
        if (shippedFailed)
        {
            if (r.Ok) return "the shipped decode failed, the port succeeded";
            return null;
        }
        if (!r.Ok) return $"the shipped decode succeeded, the port failed: {r.Error}";
        return CompareMat(c, r.Image!);
    }

    /// <summary>
    /// M3-001/M3-018, J1..J8, J31..J39: the engine's DecodeImageHelper dispatch (all encoding values, gray and RGB helpers, MiniToJpegHelper, the
    /// BGR2RGB swap, the border, the resize, the size check and its log text) against libcozmoEngine.so, on the real robot frames of the hardware
    /// bundles, synthetic mini frames, random and degenerate payloads, raw frames and JPEG-file encodings.
    /// </summary>
    [Fact]
    public void M3_001_J1_J39_EngineDispatchMatchesTheShippedEngine()
    {
        bool errorFlag = Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try { EngineDispatchBody(); }
        finally { Cozmo.Transport.EngineErrorState.ErrorFlagSet = errorFlag; }
    }

    private static void EngineDispatchBody()
    {
        var failures = new List<string>();
        int n = 0, withLogs = 0;
        foreach (var c in Of("engine"))
        {
            n++;
            byte enc = (byte)c.GetProperty("encoding").GetInt32();
            bool gray = c.GetProperty("gray").GetBoolean();
            var payload = File_(c, "input");
            var jpeg = enc is 8 or 9 ? MiniJpeg.ToJpeg(payload, enc == 9 ? 160 : 320, 240, enc) : payload;
            if (jpeg.Length == 0) continue;                              // policy M3-020: the strip reads data[-1]
            Cozmo.Transport.EngineErrorState.ErrorFlagSet = false;
            var ours = new List<string>();
            var r = gray ? EncodedImageDecode.DecodeGray(enc, payload, jpeg, 240, 320, ours.Add) : EncodedImageDecode.DecodeRgb(enc, payload, jpeg, 240, 320, ours.Add);
            var why = CheckEngine(c, r, ours);
            if (ours.Count > 0) withLogs++;
            if (why != null) failures.Add($"{Id(c)}: {why}");
            // the UnsupportedEncoding paths store _errG = 1 (stores 0x4F22AC RGB / 0x4F297A gray, D4) and nothing else on this path does: the flag the shipped decode left
            if (c.GetProperty("expected").TryGetProperty("err_flag", out var ef) && (ef.GetInt32() != 0) != Cozmo.Transport.EngineErrorState.ErrorFlagSet)
                failures.Add($"{Id(c)}: _errG after the decode is {Cozmo.Transport.EngineErrorState.ErrorFlagSet}, the shipped engine left {ef.GetInt32()}");
        }
        Assert.True(n >= 150, $"only {n} engine fixtures");
        Assert.True(withLogs >= 30, $"only {withLogs} engine fixtures wrote a log line");
        Assert.True(failures.Count == 0, $"{failures.Count} of {n} engine fixtures differ:\n" + string.Join("\n", failures.Take(25)));
    }

    private static CameraFrame? FrameFrom(byte[] payload, byte encoding)
    {
        var cam = new CozmoCamera();
        CameraFrame? got = null;
        cam.FrameReceived += f => got = f;
        const int per = 1000;
        int count = Math.Max(1, (payload.Length + per - 1) / per);
        if (count > 255) return null;
        for (int i = 0; i < count; i++)
        {
            cam.Handle(new ImageChunk
            {
                ImageId = 1, ChunkId = (byte)i, ImageChunkCount = (byte)count, ImageEncoding = encoding, ImageResolution = 4,
                Data = payload.Skip(i * per).Take(per).ToArray(),
            });
        }
        return got;
    }

    /// <summary>
    /// The same engine fixtures through the live entry: ImageChunk messages go to <see cref="CozmoCamera"/> (the engine's AddChunk), the completed
    /// <see cref="CameraFrame"/> is decoded by <see cref="CameraFrame.TryDecodeGray"/> / <see cref="CameraFrame.TryDecodeRgb"/>, and the
    /// result is compared with what the shipped DecodeImageGray / DecodeImageRGB produced for that payload.
    /// </summary>
    [Fact]
    public void M3_001_J1_TheLiveEntryDecodesWhatTheShippedEngineDecodes()
    {
        bool errorFlag = Cozmo.Transport.EngineErrorState.ErrorFlagSet;
        try { LiveEntryBody(); }
        finally { Cozmo.Transport.EngineErrorState.ErrorFlagSet = errorFlag; }
    }

    private static void LiveEntryBody()
    {
        var failures = new List<string>();
        int n = 0;
        foreach (var c in Of("engine"))
        {
            byte enc = (byte)c.GetProperty("encoding").GetInt32();
            var payload = File_(c, "input");
            if (payload.Length == 0) continue;
            var frame = FrameFrom(payload, enc);
            if (frame == null) continue;
            // AddChunk (M3 inventory A5, Camera.cs) re-labels a gray-mini payload whose first byte is non-zero as colour; the
            // fixture decoded it as encoding 8, so the two are not the same frame.
            if (frame.Encoding != enc) continue;
            n++;
            bool gray = c.GetProperty("gray").GetBoolean();
            var exp = c.GetProperty("expected");
            bool shippedFailed = exp.TryGetProperty("exception", out _) || exp.GetProperty("result").GetInt32() != 0;
            CvMat? mat = null;
            string? error;
            bool ok;
            var liveLogs = new List<string>();
            if (gray)
            {
                ok = frame.TryDecodeGray(out var img, out error, liveLogs.Add);
                if (ok) mat = new CvMat(img!.Height, img.Width, 1, img.Pixels);
            }
            else
            {
                ok = frame.TryDecodeRgb(out var rgb, out error, liveLogs.Add);
                if (ok) mat = new CvMat(240, 320, 3, rgb!);
            }
            if (frame.Jpeg.Length == 0 && enc is 8 or 9)
            {
                if (ok || !error!.Contains("M3-020")) failures.Add($"{Id(c)}: an empty reconstruction must fail with the M3-020 policy text");
                continue;
            }
            if (!Logs(exp).SequenceEqual(liveLogs)) { failures.Add($"{Id(c)}: logs [{string.Join("|", liveLogs)}], the shipped helper wrote [{string.Join("|", Logs(exp))}]"); continue; }
            if (shippedFailed)
            {
                if (ok) failures.Add($"{Id(c)}: the shipped decode failed, the live entry succeeded");
                continue;
            }
            if (!ok) { failures.Add($"{Id(c)}: the live entry failed: {error}"); continue; }
            var why = CompareMat(c, mat!);
            if (why != null) failures.Add($"{Id(c)}: {why}");
        }
        Assert.True(n >= 150, $"only {n} engine fixtures reached the live entry");
        Assert.True(failures.Count == 0, $"{failures.Count} of {n} differ:\n" + string.Join("\n", failures.Take(25)));
    }

    /// <summary>
    /// M3-001, J79..J102: the 32 inverse DCTs (squares 1..16, rectangles 2:1) against the shipped bodies called directly with random and extreme
    /// coefficient blocks and quantisers (32-bit wrap included).
    /// </summary>
    [Fact]
    public void M3_001_J79_J102_EveryInverseDctMatchesTheShippedBody()
    {
        var table = new byte[5 * 256 + 128];
        for (int i = 0; i < 256; i++) table[256 + i] = (byte)i;
        for (int i = 512; i < 512 + 384; i++) table[i] = 255;
        for (int i = 0; i < 128; i++) table[1280 + i] = (byte)i;
        var seen = new HashSet<(int, int)>();
        var failures = new List<string>();
        int n = 0;
        foreach (var c in Data.Value.Idct.EnumerateArray())
        {
            int w = c.GetProperty("w").GetInt32(), h = c.GetProperty("h").GetInt32();
            seen.Add((w, h));
            var coef = c.GetProperty("coef").EnumerateArray().Select(x => (short)x.GetInt32()).ToArray();
            var quant = c.GetProperty("quant").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var dst = new byte[w * h];
            JpegIdct.Transform(w, h, coef, 0, quant, table, dst, 0, w);
            var rows = c.GetProperty("rows").EnumerateArray().Select(r => r.EnumerateArray().Select(x => (byte)x.GetInt32()).ToArray()).ToArray();
            n++;
            for (int r = 0; r < h; r++)
                for (int k = 0; k < w; k++)
                    if (rows[r][k] != dst[r * w + k]) { failures.Add($"{w}x{h} vector {n}: row {r} col {k} expected {rows[r][k]}, got {dst[r * w + k]}"); r = h; break; }
        }
        Assert.Equal(32, seen.Count);
        Assert.True(failures.Count == 0, $"{failures.Count} of {n} transforms differ:\n" + string.Join("\n", failures.Take(25)));
    }

    /// <summary>
    /// M3-001, J29, J48, J63, J109, J111 and the default-table row: the static tables the port carries equal the bytes of the shipped
    /// libopencv_imgcodecs.so (the zigzag at 0xCECF0 and the size-specific orders at 0xCE938..0xCEBE8, the arithmetic probability table at 0xCF328,
    /// the zigzag-limit matrices at 0xCEFF0..0xCF228, and the default MJPEG Huffman segment at VA 0x175018).
    /// </summary>
    [Fact]
    public void M3_001_J29_J48_J63_J111_TheStaticTablesEqualTheShippedBytes()
    {
        var t = Data.Value.Tables;
        Assert.Equal(t.GetProperty("natural8").EnumerateArray().Select(x => x.GetInt32()), JpegTables.NaturalOrder8);
        for (int n = 2; n <= 7; n++)
            Assert.Equal(t.GetProperty("natural" + n).EnumerateArray().Select(x => x.GetInt32()), JpegTables.NaturalOrder(n));
        Assert.Equal(t.GetProperty("arith").EnumerateArray().Select(x => (uint)x.GetInt64()), JpegTables.ArithTable);
        for (int n = 2; n <= 8; n++)
        {
            var m = t.GetProperty("limit" + n).EnumerateArray().Select(x => x.GetInt32()).ToArray();
            for (int v = 1; v <= n; v++)
                for (int h = 1; h <= n; h++)
                    Assert.Equal(m[(v - 1) * n + (h - 1)], JpegTables.ZigzagLimit(n, v, h));
        }
        Assert.Equal(t.GetProperty("defaultDht").EnumerateArray().Select(x => (byte)x.GetInt32()), JpegDecoder.DefaultDhtSegment);
    }

    /// <summary>
    /// The fixtures were made from the pinned library builds (SHA-256 of the five .so files the oracle runs, asserted by the oracle before it
    /// runs and recorded in index.json).
    /// </summary>
    [Fact]
    public void M3_001_TheFixturesWereMadeFromThePinnedLibraries()
    {
        var pins = new Dictionary<string, string>
        {
            ["libcozmoEngine.so"] = "02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1",
            ["libopencv_core.so"] = "8b8c9fcdff9028e5c1b58578d6c20040bebfc2f16c2d2d4bf885f79da56cba8d",
            ["libopencv_imgproc.so"] = "3c4e3ff7e639c61e21c5ef91ea2cc4125bb9bd259ff53f5c0b1eeb9a95054830",
            ["libopencv_imgcodecs.so"] = "4bdf5a45023c7938924f6c0018bf20774ebb5f7c0b2a637c93fe9e61b1f87218",
            ["libc++_shared.so"] = "8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a",
        };
        var recorded = Data.Value.Root.GetProperty("library_sha256").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        Assert.Equal(pins.OrderBy(k => k.Key), recorded.OrderBy(k => k.Key));
    }

    /// <summary>
    /// The fixtures themselves: every case was run under 0x00 and 0xFF poisoning and with NEON present and absent; a decoded byte that depended on
    /// uninitialised memory is excluded, and none of the cases in this corpus differed between NEON and scalar (a measurement over this corpus,
    /// not a proof for every input: the phone's NEON/scalar choice stays a runtime assumption). The corpus covers every encoding value, both
    /// helpers, baseline, progressive, arithmetic, restart and scaled files, the default-table branch, the LSE marker and the allocation failures.
    /// </summary>
    [Fact]
    public void M3_001_TheCorpusIsWideAndShowsNoNeonDifference()
    {
        var cases = Data.Value.Cases;
        Assert.DoesNotContain(cases, c => c.GetProperty("expected").TryGetProperty("neon_differences", out _));
        Assert.True(cases.Count >= 1500, $"{cases.Count} cases");
        var ids = cases.Select(Id).ToList();
        foreach (var prefix in new[] { "pil_", "b0_", "rnd_prog_", "rnd_arith_", "rnd_parith_", "rnd_seq_", "mut_", "hdr_default_tables", "hdr_dqt_short_", "scaled_seq_se", "rs_", "hw_", "syn_color_", "syn_gray_", "jpg_7_", "lse_ok_", "lse_trunc_", "oom_" })
            Assert.Contains(ids, i => i.StartsWith(prefix, StringComparison.Ordinal));
        var encodings = cases.Where(c => c.GetProperty("kind").GetString() == "engine").Select(c => c.GetProperty("encoding").GetInt32()).ToHashSet();
        foreach (var e in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 255 }) Assert.Contains(e, encodings);
    }

    /// <summary>
    /// J41: INTER_LINEAR with an exact 2x reduction is switched to INTER_AREA by cv::resize; that kernel is not ported because nothing on the
    /// engine's path reaches it (the resize is 160x240 to 320x240). It is an explicit stub, never a silent default. (The colour_transform
    /// conversions are ported: the LSE marker sets it, L8; see M3_001_L1_L18.)
    /// </summary>
    [Fact]
    public void M3_018_J41_TheUnreachedResizeKernelIsAnExplicitStub()
    {
        var src = new CvMat(30, 40, 1, new byte[1200]);
        Assert.Throws<NotSupportedException>(() => OpenCvResize.ResizeLinear(src, 20, 15));
    }
}
