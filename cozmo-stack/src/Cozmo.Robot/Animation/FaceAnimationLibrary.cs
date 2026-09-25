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
/// Each image is thresholded at 0x80 and stored as <b>two RLE variants</b> (C12, 0x00581254..0x005812C4): one with the
/// even canvas rows cleared and one with the odd rows cleared, each through <c>CompressRLE</c>
/// (<see cref="FaceBitmapCodec.EncodeCanvas"/>). <c>GetFrame</c> picks one by <see cref="FirstScanLine"/>
/// (0x005817B4..0x005817CA, M3 B4). Which stored variant belongs to which value is not in the row; this takes the
/// even-rows-cleared variant for 0, the drawer's convention (E2: 0 clears even rows).
/// </summary>
public sealed class FaceAnimationLibrary
{
    /// <summary>The threshold <c>AddImage</c> applies, 0x80 (a sample at or above it is lit).</summary>
    public const byte Threshold = 0x80;

    /// <summary>
    /// <c>FaceAnimationManager::_firstScanLine</c> (.bss 0x0105AB10), 0 at start; InitStream toggles it together with the
    /// drawer's (A11).
    /// </summary>
    public static int FirstScanLine => ScanLineState.Process.FaceAnimation;

    private readonly Dictionary<string, string> _dirs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<FaceBitmap>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<FaceAnimationFrame>> _variants = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    /// <summary>The animation names found, whether or not their frames have been read yet.</summary>
    public IReadOnlyCollection<string> Names { get { lock (_gate) return _dirs.Keys.ToArray(); } }

    /// <summary>
    /// Opens a library over an unpacked <c>cozmo_resources</c> tree. Point it at the directory that holds
    /// <c>assets/faceAnimations</c>, or directly at that directory. A tree without one gives an empty
    /// library rather than an error: most of them have no face animations at all.
    /// </summary>
    public static FaceAnimationLibrary Open(string assetsRoot)
    {
        var lib = new FaceAnimationLibrary();
        var dir = FindDir(assetsRoot);
        if (dir is null) return lib;
        foreach (var d in Directory.EnumerateDirectories(dir))
            lib._dirs[Path.GetFileName(d)] = d;
        return lib;
    }

    private static string? FindDir(string root)
    {
        if (string.Equals(Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)), "faceAnimations",
                          StringComparison.OrdinalIgnoreCase))
            return Directory.Exists(root) ? root : null;
        foreach (var candidate in new[] { Path.Combine(root, "faceAnimations"), Path.Combine(root, "assets", "faceAnimations") })
            if (Directory.Exists(candidate)) return candidate;
        return null;
    }

    /// <summary>True when an animation of this name is present.</summary>
    public bool Has(string name) { lock (_gate) return _dirs.ContainsKey(name); }

    private IReadOnlyList<MiniPng.Gray8>? Images(string name)
    {
        if (!_dirs.TryGetValue(name, out var dir)) return null;
        return Directory.EnumerateFiles(dir, "*.png")
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .Select(f => MiniPng.DecodeGray8(File.ReadAllBytes(f)))
                        .ToArray();
    }

    /// <summary>
    /// The frames of one animation as this stack's 128 x 32 pictures (the odd canvas row of each pair), or null when
    /// there is no such animation. For inspection; the stream uses <see cref="Variants"/>.
    /// </summary>
    public IReadOnlyList<FaceBitmap>? Frames(string name)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(name, out var cached)) return cached;
            var images = Images(name);
            if (images is null) return null;
            var frames = images.Select(ToFace).ToArray();
            _cache[name] = frames;
            return frames;
        }
    }

    /// <summary>The stored frames of one animation, each with its two RLE variants (C12), or null when there is none.</summary>
    public IReadOnlyList<FaceAnimationFrame>? Variants(string name)
    {
        lock (_gate)
        {
            if (_variants.TryGetValue(name, out var cached)) return cached;
            var images = Images(name);
            if (images is null) return null;
            var frames = images.Select(FaceAnimationFrame.FromImage).ToArray();
            _variants[name] = frames;
            return frames;
        }
    }

    /// <summary>
    /// One 128 x 64 grayscale image as this stack's 128 x 32 picture: lit where the sample is at or above the
    /// threshold, the odd canvas row of each pair.
    /// </summary>
    public static FaceBitmap ToFace(MiniPng.Gray8 image)
    {
        var face = new FaceBitmap();
        for (int y = 0; y < FaceBitmap.Height; y++)
        {
            int row = 2 * y + 1;
            if (row >= image.Height) break;
            for (int x = 0; x < FaceBitmap.Width && x < image.Width; x++)
                face[x, y] = (byte)(image.Pixels[row * image.Width + x] >= Threshold ? 1 : 0);
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
                if (image.Pixels[r * image.Width + c] >= FaceAnimationLibrary.Threshold)
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

        int width = 0, height = 0;
        var idat = new MemoryStream();
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
                byte depth = png[data + 8], colour = png[data + 9], interlace = png[data + 12];
                if (depth != 8 || colour != 0 || interlace != 0)
                    throw new InvalidDataException(
                        $"only 8-bit non-interlaced grayscale PNG is supported (depth {depth}, colour type {colour}, interlace {interlace})");
            }
            else if (type.SequenceEqual("IDAT"u8)) idat.Write(png.Slice(data, length));
            else if (type.SequenceEqual("IEND"u8)) break;

            at = data + length + 4;                       // skip the CRC
        }
        if (width <= 0 || height <= 0) throw new InvalidDataException("PNG has no IHDR");

        idat.Position = 0;
        var raw = new byte[(width + 1) * height];
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
            z.ReadExactly(raw);

        // Undo the per-scanline filters. One byte per pixel, so the filter's "previous pixel" is one byte
        // back and its "above" is one scanline back.
        var pixels = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            int src = y * (width + 1);
            byte filter = raw[src];
            int dst = y * width;
            for (int x = 0; x < width; x++)
            {
                int value = raw[src + 1 + x];
                byte a = x > 0 ? pixels[dst + x - 1] : (byte)0;
                byte b = y > 0 ? pixels[dst - width + x] : (byte)0;
                byte c = x > 0 && y > 0 ? pixels[dst - width + x - 1] : (byte)0;
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
