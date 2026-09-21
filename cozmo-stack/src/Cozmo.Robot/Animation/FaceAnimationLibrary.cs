using System.IO.Compression;

namespace Cozmo.Robot.Animation;

/// <summary>
/// The pre-rendered face animations, the assets the <c>faceAnimations</c> track names.
///
/// <c>FaceAnimationManager::ReadFaceAnimationDir</c> 0x00580048 lists the directories under the
/// <c>faceAnimations</c> resource directory, one per animation, and
/// <c>LoadAnimationImageFrames</c> 0x00580834 reads the images inside each. The shipped tree has two of
/// them, <c>face_bored_event_02</c> and <c>face_bored_event_04</c>, each a run of 128 x 64 eight-bit
/// grayscale PNGs numbered in order.
///
/// <c>FaceAnimationManager::AddImage</c> 0x00581440 turns one of those images into what goes on the wire:
/// <c>Image::Threshold(0x80)</c> at 0x00581462, then the helper at 0x00581254 clears every even canvas row
/// (the <c>memclr</c> loop stepping by two from zero) and hands the result to <c>CompressRLE</c> - the same
/// encoder <see cref="FaceBitmapCodec.Encode"/> implements. Clearing the even rows is what makes the
/// canvas's 64 rows into the display's 32: the odd row of each pair survives, and that is the row taken
/// here.
/// </summary>
public sealed class FaceAnimationLibrary
{
    /// <summary>The threshold <c>AddImage</c> applies, 0x80.</summary>
    public const byte Threshold = 0x80;

    private readonly Dictionary<string, string> _dirs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<FaceBitmap>> _cache = new(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    /// The frames of one animation, in order, or null when there is no such animation. Read once and kept:
    /// the engine holds them in its manager the same way.
    /// </summary>
    public IReadOnlyList<FaceBitmap>? Frames(string name)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(name, out var cached)) return cached;
            if (!_dirs.TryGetValue(name, out var dir)) return null;
            var frames = Directory.EnumerateFiles(dir, "*.png")
                                  .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                  .Select(f => ToFace(MiniPng.DecodeGray8(File.ReadAllBytes(f))))
                                  .ToArray();
            _cache[name] = frames;
            return frames;
        }
    }

    /// <summary>
    /// One 128 x 64 grayscale image as the display sees it: lit where the sample is at or above the
    /// threshold, and only the odd canvas row of each pair, which is the row <c>AddImage</c> leaves alone.
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
