using StbImageSharp;

namespace Cozmo.Robot.Vision;

/// <summary>An 8-bit grayscale image, row-major. The vision pipeline's working format (the engine's <c>Vision::Image</c>).</summary>
public sealed class GrayImage
{
    public GrayImage(int width, int height, byte[]? pixels = null)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("empty image");
        Width = width; Height = height;
        Pixels = pixels ?? new byte[width * height];
        if (Pixels.Length != width * height) throw new ArgumentException("pixel buffer size");
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public byte this[int x, int y]
    {
        get => Pixels[y * Width + x];
        set => Pixels[y * Width + x] = value;
    }

    public void Fill(byte v) => Array.Fill(Pixels, v);

    /// <summary>
    /// Decodes a JPEG (as the camera layer delivers it, header rebuilt by <c>MiniJpeg</c>) to grayscale. Colour
    /// frames are converted with the usual luma weights. The engine does the same in
    /// <c>EncodedImage::DecodeImageGray</c> before <c>VisionSystem::Update</c>.
    /// </summary>
    public static GrayImage FromJpeg(ReadOnlySpan<byte> jpeg)
    {
        var img = ImageResult.FromMemory(jpeg.ToArray(), ColorComponents.Grey);
        return new GrayImage(img.Width, img.Height, img.Data);
    }

    public static GrayImage FromFrame(CameraFrame frame) => FromJpeg(frame.Jpeg);

    /// <summary>Half-resolution copy by 2x2 box averaging (one pyramid level).</summary>
    public GrayImage Downsample2()
    {
        int w = Width / 2, h = Height / 2;
        var o = new GrayImage(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = 2 * y * Width + 2 * x;
                o.Pixels[y * w + x] = (byte)((Pixels[i] + Pixels[i + 1] + Pixels[i + Width] + Pixels[i + Width + 1] + 2) >> 2);
            }
        return o;
    }

    /// <summary>Writes a binary PGM, for looking at intermediate images from the conformance tool.</summary>
    public void SavePgm(string path)
    {
        using var f = File.Create(path);
        var header = System.Text.Encoding.ASCII.GetBytes($"P5\n{Width} {Height}\n255\n");
        f.Write(header); f.Write(Pixels);
    }
}
