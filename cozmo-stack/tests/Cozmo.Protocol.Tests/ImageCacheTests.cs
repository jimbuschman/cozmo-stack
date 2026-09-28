using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M11-033 (C2.2): the <c>ImageCache</c> grey/RGB members and valid flags, the colour/grey dispatch, and
/// <c>ImageRGB::FillGray</c> = <c>cv::cvtColor(rgb, gray, COLOR_RGB2GRAY=7, 0)</c>. Expected values come
/// from the shipped OpenCV 3.1.0 fixed point (coefficients B 1868 / G 9617 / R 4899, rounding 8192, shift
/// 14), never from the code under test. The shipped camera is grey, so the RGB branch is inert on the live
/// path; these tests exercise it directly.
/// </summary>
public class ImageCacheTests
{
    /// <summary>
    /// The OpenCV 3.1.0 8U RGB2GRAY fixed point: <c>(R*4899 + G*9617 + B*1868 + (1&lt;&lt;13)) &gt;&gt; 14</c>.
    /// The coefficients are the shipped table at 0x2C798 and the rounding constant is 0x2000.
    /// </summary>
    [Fact]
    public void TheRgbToGrayConversionIsTheShippedOpenCvFixedPoint()
    {
        // the coefficients sum to 2^14, so white is exactly 255 and black is 0
        Assert.Equal(255, ImageCache.FillGray(new byte[] { 255, 255, 255 }, 1, 1).Pixels[0]);
        Assert.Equal(0, ImageCache.FillGray(new byte[] { 0, 0, 0 }, 1, 1).Pixels[0]);

        // pure red: (255*4899 + 8192) >> 14 = 76
        Assert.Equal(76, ImageCache.FillGray(new byte[] { 255, 0, 0 }, 1, 1).Pixels[0]);
        // pure green: (255*9617 + 8192) >> 14 = 150
        Assert.Equal(150, ImageCache.FillGray(new byte[] { 0, 255, 0 }, 1, 1).Pixels[0]);
        // pure blue: (255*1868 + 8192) >> 14 = 29
        Assert.Equal(29, ImageCache.FillGray(new byte[] { 0, 0, 255 }, 1, 1).Pixels[0]);

        // a 2-pixel row keeps the row-major RGB order
        var row = ImageCache.FillGray(new byte[] { 255, 0, 0, 0, 0, 255 }, 2, 1);
        Assert.Equal(76, row.Pixels[0]);
        Assert.Equal(29, row.Pixels[1]);
    }

    /// <summary>
    /// The <c>ImageCache</c> layout (C2.2): a grey member (+0x14) with its valid flag (+0x94), an RGB
    /// member (+0x54) with its flag (+0x95). Reset fills one and clears the other; GetGray returns the grey
    /// member, converting an RGB entry through FillGray.
    /// </summary>
    [Fact]
    public void TheImageCacheHoldsGreyOrRgbWithItsValidFlag()
    {
        var cache = new ImageCache();
        var gray = new GrayImage(2, 1, new byte[] { 7, 9 });
        cache.Reset(gray);
        Assert.True(cache.GrayValid);
        Assert.False(cache.RgbValid);
        Assert.Same(gray, cache.GetGray());

        cache.Reset(new byte[] { 255, 0, 0, 0, 0, 255 }, 2, 1);
        Assert.False(cache.GrayValid);
        Assert.True(cache.RgbValid);
        var converted = cache.GetGray();
        Assert.Equal(76, converted.Pixels[0]);
        Assert.Equal(29, converted.Pixels[1]);
    }

    /// <summary>
    /// C2.2: <c>EncodedImage::IsColor</c> is the encoding byte - 2, 3, 4, 6 and 7 are colour, 0, 1, 5 and
    /// 8 are grey. (The M3 decoder also treats 9 and values above 8 as colour.)
    /// </summary>
    [Fact]
    public void IsColorFollowsTheEncodingByte()
    {
        foreach (byte e in new byte[] { 2, 3, 4, 6, 7 }) Assert.True(EncodedImageDecoder.IsColor(e), $"encoding {e}");
        foreach (byte e in new byte[] { 0, 1, 5, 8 }) Assert.False(EncodedImageDecoder.IsColor(e), $"encoding {e}");
    }
}