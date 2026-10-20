using System.IO.Compression;

namespace Cozmo.Robot.Animation;

// fidelity: M5-013
/// <summary>
/// The pre-rendered face animations, the assets the <c>faceAnimations</c> track names (<c>FaceAnimationManager</c>).
///
/// <c>FaceAnimationManager::ReadFaceAnimationDir</c> 0x00580048 lists the directories under the
/// <c>faceAnimations</c> resource directory, one per animation, and <c>LoadAnimationImageFrames</c> 0x00580834 reads
/// the images inside each. The shipped tree has two of them, <c>face_bored_event_02</c> and <c>face_bored_event_04</c>,
/// each a run of 128 x 64 eight-bit grayscale PNGs numbered in order.
///
/// Each image is thresholded strictly above 0x80 and stored as <b>two RLE variants</b> (C12, 0x00581254..0x005812C4):
/// one with the even canvas rows cleared and one with the odd rows cleared, each through <c>CompressRLE</c>
/// (<see cref="FaceBitmapCodec.EncodeCanvas"/>). <c>GetFrame</c> picks one by <see cref="FirstScanLine"/>
/// (0x005817B4..0x005817CA, M3 B4; C5 item 5.3): 0 gives the even-rows-cleared variant, any non-zero the
/// odd-rows-cleared one.
/// </summary>
public sealed class FaceAnimationLibrary
{
    /// <summary>
    /// The threshold <c>AddImage</c> applies, 0x80. <c>Image::Threshold</c> calls <c>cv::operator&gt;</c> (CMP_GT), so the
    /// test is strictly above: a pixel equal to 0x80 becomes 0 (C5 item 5.1).
    /// </summary>
    public const byte Threshold = 0x80;

    /// <summary>
    /// <c>FaceAnimationManager::_firstScanLine</c> (.bss 0x0105AB10), 0 at start; InitStream toggles it together with the
    /// drawer's (A11).
    /// </summary>
    public static int FirstScanLine => ScanLineState.Process.FaceAnimation;

    private readonly Dictionary<string, string> _dirs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _mtimes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<FaceBitmap>> _cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<FaceAnimationFrame>> _variants = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private Action<string>? _log;
    private SourceLogSink? _sourceLog;
    public IReadOnlyCollection<string> Names { get { lock (_gate) return _dirs.Keys.ToArray(); } }

    // fidelity: M5-052
    public static FaceAnimationLibrary Open(string assetsRoot, Action<string>? log = null, SourceLogSink? sourceLog = null)
    {
        var lib = new FaceAnimationLibrary { _log = log, _sourceLog = sourceLog };
        var dir = FindDir(assetsRoot);
        if (dir is null) return lib;
        var jobs = new List<string>();
        foreach (string path in Directory.EnumerateDirectories(dir))
        {
            string name = Path.GetFileName(path);
            if (name == JsonClipLoader.ProceduralAnimName)
            { lib.Error("FaceAnimationManager.ReadFaceAnimationDir.ReservedName", "'_PROCEDURAL_' is a reserved face animation name. Ignoring."); continue; }
            var stat = new DirectoryInfo(path); stat.Refresh();
            if (!stat.Exists)
            { SourceLogDispatch.GetSink(sourceLog, log)?.Invoke(EngineLogLevel.Warning, null, "FaceAnimationManager.ReadFaceAnimationDir", $"could not get mtime for {path}"); continue; }
            long mtime = stat.LastWriteTimeUtc.Ticks;
            if (lib._mtimes.TryGetValue(name, out long old) && old >= mtime) continue;
            lib._mtimes[name] = mtime; lib._dirs[name] = path;
            lib._cache[name] = Array.Empty<FaceBitmap>(); lib._variants[name] = Array.Empty<FaceAnimationFrame>();
            jobs.Add(path);
        }
        AnimationLibrary.DispatchFiles(jobs, lib.LoadImageFrames);
        return lib;
    }
    private void Error(string key, string text)
    { SourceLogDispatch.GetSink(_sourceLog, _log)?.Invoke(EngineLogLevel.Error, null, key, text); Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak(); }
    private static string? FindDir(string root)
    {
        if (string.Equals(Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)), "faceAnimations", StringComparison.OrdinalIgnoreCase))
            return Directory.Exists(root) ? root : null;
        foreach (var candidate in new[] { Path.Combine(root, "faceAnimations"), Path.Combine(root, "assets", "faceAnimations") })
            if (Directory.Exists(candidate)) return candidate;
        return null;
    }
    private void LoadImageFrames(string path)
    {
        string name = Path.GetFileName(path);
        var pictures = new List<FaceBitmap>(); var frames = new List<FaceAnimationFrame>();
        void Publish() { lock (_gate) { _cache[name] = pictures.ToArray(); _variants[name] = frames.ToArray(); } }
        foreach (string filename in new DirectoryInfo(path).EnumerateFiles().Where(f => (f.Attributes & FileAttributes.ReparsePoint) == 0).Select(f => f.Name)
            .OrderBy(n => System.Text.Encoding.UTF8.GetBytes(n), Utf8Comparer.Instance))
        {
            int dot = filename.LastIndexOf('.'), underscore = filename.LastIndexOf('_');
            string? failure = dot < 0 ? $"Could not find '.' in frame filename {filename}" : underscore < 0 ? $"Could not find '_' in frame filename {filename}" : dot <= underscore + 1 ? $"Unexpected relative positions for '.' and '_' in frame filename {filename}" : null;
            if (failure is not null) { Error("FaceAnimationManager.ReadFaceAnimationDir", failure); break; }
            string number = filename[(underscore + 1)..dot];
            // The canonical decimal subset is unambiguous; libc++ stoi's remaining parsing rules stay MISSING.
            if (!System.Text.RegularExpressions.Regex.IsMatch(number, @"^[+-]?[0-9]+$"))
            {
                if (number.Length == 0 || !number.Any(char.IsAsciiDigit))
                    Error("FaceAnimationManager.ReadFaceAnimationDir", $"Could not get frame number from substring '{number}' of filename '{filename}'.");
                else _log?.Invoke($"MISSING: M5-052 libc++ stoi parsing of '{number}' in {filename}, 0x00580A0E; animation load stopped");
                break;
            }
            if (!int.TryParse(number, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out int n))
            { _log?.Invoke($"Loader file skipped (M1-034): face frame stoi out_of_range in {filename}"); continue; }
            if (n < 0) { Error("FaceAnimationManager.ReadFaceAnimationDir", $"Found negative frame number ({n}) for filename '{filename}'."); break; }
            if (n >= 2)
                while (frames.Count < n - 1) { frames.Add(new(Array.Empty<byte>(), Array.Empty<byte>())); pictures.Add(new()); }
            string fullPath = Path.Combine(path, filename);
            MiniPng.Gray8 image = default;
            byte[]? png = null;
            try { png = File.ReadAllBytes(fullPath); }
            catch (IOException) { /* A file read failure gives an empty native image. */ }
            if (png is not null)
            {
                try { image = MiniPng.DecodeGray8(png); }
                catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or OverflowException)
                { _log?.Invoke($"MISSING: M5-052 cv::imread PNG path unsupported by host decoder in {fullPath}; {e.GetType().Name}; frame skipped"); continue; }
            }
            if (image.Pixels is null || image.Width != 128 || image.Height != 64)
            { Error("FaceAnimationManager.ReadFaceAnimationDir", $"Image in {fullPath} is {image.Width}x{image.Height} instead of 128x64."); continue; }
            pictures.Add(ToFace(image)); frames.Add(FaceAnimationFrame.FromImage(image));
        }
        Publish();
    }
    private sealed class Utf8Comparer : IComparer<byte[]>
    { internal static readonly Utf8Comparer Instance = new(); public int Compare(byte[]? a, byte[]? b) => a.AsSpan().SequenceCompareTo(b); }
    public bool Has(string name) { lock (_gate) return _dirs.ContainsKey(name); }
    public IReadOnlyList<FaceBitmap>? Frames(string name) { lock (_gate) return _cache.GetValueOrDefault(name); }
    public IReadOnlyList<FaceAnimationFrame>? Variants(string name) { lock (_gate) return _variants.GetValueOrDefault(name); }

    /// <summary>
    /// One 128 x 64 grayscale image as this stack's 128 x 32 picture: lit where the sample is strictly above the
    /// threshold (a pixel equal to 0x80 is dark), the odd canvas row of each pair.
    /// </summary>
    public static FaceBitmap ToFace(MiniPng.Gray8 image)
    {
        var face = new FaceBitmap();
        for (int y = 0; y < FaceBitmap.Height; y++)
        {
            int row = 2 * y + 1;
            if (row >= image.Height) break;
            for (int x = 0; x < FaceBitmap.Width && x < image.Width; x++)
                face[x, y] = (byte)(image.Pixels[row * image.Width + x] > Threshold ? 1 : 0);
        }
        return face;
    }
}

// fidelity: M5-013
/// <summary>
/// One stored face-animation frame (C12): the image thresholded at 0x80 on the 64 x 128 canvas, then two RLE payloads,
/// <see cref="EvenRowsCleared"/> and <see cref="OddRowsCleared"/>. An empty payload is an empty frame, which the track
/// skips.
/// </summary>
public sealed record FaceAnimationFrame(byte[] EvenRowsCleared, byte[] OddRowsCleared)
{
    /// <summary>The variant <c>GetFrame</c> returns for a <c>_firstScanLine</c> value (0: the even rows cleared).</summary>
    public byte[] ForScanLine(int firstScanLine) => firstScanLine == 0 ? EvenRowsCleared : OddRowsCleared;

    public static FaceAnimationFrame FromImage(MiniPng.Gray8 image)
    {
        var canvas = new byte[FaceBitmapCodec.CanvasRows * FaceBitmapCodec.CanvasColumns];
        for (int r = 0; r < FaceBitmapCodec.CanvasRows && r < image.Height; r++)
            for (int c = 0; c < FaceBitmapCodec.CanvasColumns && c < image.Width; c++)
                if (image.Pixels[r * image.Width + c] > FaceAnimationLibrary.Threshold)
                    canvas[r * FaceBitmapCodec.CanvasColumns + c] = 255;
        return FromCanvas(canvas);
    }

    /// <summary>The two variants of a 64 x 128 canvas (row-major, non-zero lit).</summary>
    public static FaceAnimationFrame FromCanvas(byte[] canvas)
    {
        var even = (byte[])canvas.Clone();
        var odd = (byte[])canvas.Clone();
        for (int r = 0; r < FaceBitmapCodec.CanvasRows; r++)
            Array.Clear((r & 1) == 0 ? even : odd, r * FaceBitmapCodec.CanvasColumns, FaceBitmapCodec.CanvasColumns);
        return new FaceAnimationFrame(
            FaceBitmapCodec.EncodeCanvas(even, FaceBitmapCodec.CanvasRows, FaceBitmapCodec.CanvasColumns)!,
            FaceBitmapCodec.EncodeCanvas(odd, FaceBitmapCodec.CanvasRows, FaceBitmapCodec.CanvasColumns)!);
    }
}

/// <summary>
/// Just enough PNG to read the face animation assets: eight-bit grayscale, no interlacing, which is what
/// every file in <c>assets/faceAnimations</c> is. Anything else is refused rather than guessed at.
/// </summary>
public static class MiniPng
{
    /// <summary>A decoded grayscale image, one byte per pixel, row major.</summary>
    public readonly record struct Gray8(int Width, int Height, byte[] Pixels);

    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static Gray8 DecodeGray8(ReadOnlySpan<byte> png)
    {
        if (png.Length < 8 || !png[..8].SequenceEqual(Signature))
            throw new InvalidDataException("not a PNG file");

        int width = 0, height = 0, depth = 0;
        using var idat = new MemoryStream();
        int at = 8;
        while (at + 8 <= png.Length)
        {
            int length = ReadBe32(png, at);
            var type = png.Slice(at + 4, 4);
            int data = at + 8;
            if (length < 0 || data + length > png.Length) throw new InvalidDataException("truncated PNG chunk");

            if (type.SequenceEqual("IHDR"u8))
            {
                width = ReadBe32(png, data);
                height = ReadBe32(png, data + 4);
                depth = png[data + 8]; byte colour = png[data + 9], interlace = png[data + 12];
                if ((depth != 1 && depth != 8) || colour != 0 || interlace != 0)
                    throw new InvalidDataException(
                        $"only 1-bit or 8-bit non-interlaced grayscale PNG is supported (depth {depth}, colour type {colour}, interlace {interlace})");
            }
            else if (type.SequenceEqual("IDAT"u8)) idat.Write(png.Slice(data, length));
            else if (type.SequenceEqual("IEND"u8)) break;

            at = data + length + 4;                       // skip the CRC
        }
        if (width <= 0 || height <= 0) throw new InvalidDataException("PNG has no IHDR");

        idat.Position = 0;
        int rowBytes = checked((width * depth + 7) / 8);
        var raw = new byte[checked((rowBytes + 1) * height)];
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
            z.ReadExactly(raw);

        // Undo the per-scanline filters. One byte per pixel, so the filter's "previous pixel" is one byte
        // back and its "above" is one scanline back.
        var pixels = new byte[checked(rowBytes * height)];
        for (int y = 0; y < height; y++)
        {
            int src = y * (rowBytes + 1);
            byte filter = raw[src];
            int dst = y * rowBytes;
            for (int x = 0; x < rowBytes; x++)
            {
                int value = raw[src + 1 + x];
                byte a = x > 0 ? pixels[dst + x - 1] : (byte)0;
                byte b = y > 0 ? pixels[dst - rowBytes + x] : (byte)0;
                byte c = x > 0 && y > 0 ? pixels[dst - rowBytes + x - 1] : (byte)0;
                value += filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => throw new InvalidDataException($"unknown PNG filter {filter}"),
                };
                pixels[dst + x] = (byte)value;
            }
        }
        if (depth == 1)
        {
            var unpacked = new byte[checked(width * height)];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                unpacked[y * width + x] = (byte)(((pixels[y * rowBytes + x / 8] >> (7 - x % 8)) & 1) * 255);
            pixels = unpacked;
        }
        return new Gray8(width, height, pixels);
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static int ReadBe32(ReadOnlySpan<byte> s, int at) =>
        (s[at] << 24) | (s[at + 1] << 16) | (s[at + 2] << 8) | s[at + 3];
}
