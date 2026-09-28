namespace Cozmo.Robot.Vision;

/// <summary>
/// The engine's <c>ImageCache</c> (M11-033, C2.2): a grey image member at <c>+0x14</c>, an RGB image
/// member at <c>+0x54</c>, and the valid flags <c>+0x94</c> (grey) and <c>+0x95</c> (RGB). One of them
/// is populated by <c>Reset</c>: <c>VisionSystem::Update(PoseData, EncodedImage)</c> 0x006B4B68 resets
/// the cache with <c>DecodeImageRGB</c>'s result for a colour encoding and with <c>DecodeImageGray</c>'s
/// otherwise. <c>GetGray</c> returns the grey member, converting an RGB entry through
/// <c>ImageRGB::FillGray</c>.
///
/// The shipped camera is grey (M3: 320x240), so <see cref="IsColor"/> is false, only the grey member is
/// populated, <see cref="RgbValid"/> stays false and <see cref="FillGray"/> is never called. The colour
/// branch exists for fidelity, not for the shipped path.
/// </summary>
// fidelity: M11-033
public sealed class ImageCache
{
    /// <summary>The grey image at <c>+0x14</c>, or null.</summary>
    public GrayImage? Gray { get; private set; }
    /// <summary>The RGB image at <c>+0x54</c> (row-major, 3 bytes a pixel), or null.</summary>
    public byte[]? Rgb { get; private set; }
    /// <summary>The grey valid flag at <c>+0x94</c>.</summary>
    public bool GrayValid { get; private set; }
    /// <summary>The RGB valid flag at <c>+0x95</c>.</summary>
    public bool RgbValid { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary><c>ImageCache::Reset(Image const&amp;)</c> 0x0087459E: the grey entry, grey valid, RGB clear.</summary>
    public void Reset(GrayImage gray)
    {
        Gray = gray;
        GrayValid = true;
        Rgb = null;
        RgbValid = false;
        Width = gray.Width;
        Height = gray.Height;
    }

    /// <summary><c>ImageCache::Reset(ImageRGB const&amp;)</c> 0x0087459E: the RGB entry at +0x54, RGB valid.</summary>
    public void Reset(byte[] rgb, int width, int height)
    {
        Rgb = rgb;
        RgbValid = true;
        Gray = null;
        GrayValid = false;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// <c>ImageCache::GetGray</c> 0x0087465C: the grey member when it is valid, otherwise the RGB member
    /// through <see cref="FillGray"/>.
    /// </summary>
    public GrayImage GetGray()
    {
        if (GrayValid && Gray is not null) return Gray;
        if (!RgbValid || Rgb is null) throw new InvalidOperationException("ImageCache has no valid image");
        return FillGray(Rgb, Width, Height);
    }

    /// <summary>
    /// <c>ImageRGB::FillGray</c> 0x00872998 -> <c>cv::cvtColor(rgb, gray, COLOR_RGB2GRAY=7, 0)</c>
    /// 0x008729CA. The shipped OpenCV 3.1.0 8U fixed point is
    /// <c>(R*4899 + G*9617 + B*1868 + (1&lt;&lt;13)) &gt;&gt; 14</c>; the coefficients are the table at
    /// <c>libopencv_imgproc.so</c> 0x2C798 (B 0x074C, G 0x2591, R 0x1323, four lanes each) and the rounding
    /// constant 0x2000 is loaded in the same routine. For 8-bit inputs the result is always 0..255, so the
    /// cast cannot overflow.
    /// </summary>
    public static GrayImage FillGray(byte[] rgb, int width, int height)
    {
        var gray = new GrayImage(width, height);
        for (int i = 0, p = 0; i < width * height; i++, p += 3)
        {
            int r = rgb[p], g = rgb[p + 1], b = rgb[p + 2];
            gray.Pixels[i] = (byte)((r * 4899 + g * 9617 + b * 1868 + (1 << 13)) >> 14);
        }
        return gray;
    }
}