namespace Cozmo.Robot.Vision.Jpeg;

// fidelity: M3-001, M3-018
// EncodedImage::DecodeImageHelper<Image> (0x004F287C, tbh 0x004F2898) and DecodeImageHelper<ImageRGB> (0x004F2184, tbh 0x004F21AE):
// the engine's dispatch around the shipped OpenCV calls (J1..J8, J31..J39). The JPEG bodies are OpenCvImage.Imdecode /
// OpenCvResize, the BGR2RGB swap (cvtColor code 4, J40) and the border (copyMakeBorder, J39) are here.
internal sealed class EncodedImageDecodeResult
{
    public bool Ok;
    public CvMat? Image;
    /// <summary>The engine's warning/error text, or the shipped OpenCV exception text.</summary>
    public string? Error;
}

internal static class EncodedImageDecode
{
    /// <summary>
    /// <paramref name="encoding"/> is EncodedImage+0x20, <paramref name="payload"/> the reassembled data vector,
    /// <paramref name="jpeg"/> the vector the encoding's reconstruction produced (MiniGrayToJpeg / MiniColorToJpeg for 8 and 9,
    /// the payload itself for 5, 6 and 7), <paramref name="rows"/> and <paramref name="cols"/> EncodedImage+0x18 and +0x14.
    /// <paramref name="log"/> receives the engine's own log lines in the stack's level-prefix form ("error: Key: text",
    /// "warning: Key: text"), at the points the helper raises them (D2..D7), and the line cv::error prints when an OpenCV call throws (O8).
    /// A null sink is the engine's null gLoggerProvider: nothing is formatted (D6).
    /// </summary>
    public static EncodedImageDecodeResult DecodeGray(byte encoding, byte[] payload, byte[] jpeg, int rows, int cols, Action<string>? log = null)
    {
        CvMat m;
        try
        {
            switch (encoding)
            {
                case 1:                                     // Y2: Image(rows, cols, data), rows*cols bytes copied (policy M3-037)
                    m = new CvMat(rows, cols, 1, CopyRaw(payload, rows * cols));
                    break;
                case 2:                                     // Y3, Y4: ImageRGB(rows, cols, data) then ToGray (cvtColor code 7)
                    m = new CvMat(rows, cols, 1, RgbToGray(CopyRaw(payload, rows * cols * 3), rows * cols));
                    break;
                case 5 or 6:                                // Y5: imdecode(flags 0)
                    m = OpenCvImage.Imdecode(jpeg, 0);
                    break;
                case 7:                                     // Y6: imdecode(flags 0) then copyMakeBorder(0, 0, 160, 160)
                    m = AddZeroColumns(OpenCvImage.Imdecode(jpeg, 0), 160);
                    break;
                case 8:                                     // Y7: MiniGrayToJpeg then imdecode(flags 0)
                    m = OpenCvImage.Imdecode(jpeg, 0);
                    break;
                case 9:                                     // Y7: MiniColorToJpeg, imdecode(flags 0), Resize(1)
                    m = Resize(OpenCvImage.Imdecode(jpeg, 0), cols, rows);
                    break;
                default:                                    // Y0, Y1: 0, 3, 4, 10..255
                    return Unsupported(encoding, log);
            }
        }
        catch (OpenCvException e)
        {
            return OpenCvFailed(e, log);
        }
        return Check(m, rows, cols, log);
    }

    public static EncodedImageDecodeResult DecodeRgb(byte encoding, byte[] payload, byte[] jpeg, int rows, int cols, Action<string>? log = null)
    {
        CvMat m;
        try
        {
            switch (encoding)
            {
                case 1:                                     // Z7: Image(rows, cols, data) then ImageRGB(const Image&): cvtColor code 8
                    m = new CvMat(rows, cols, 3, GrayToRgb(CopyRaw(payload, rows * cols), rows * cols));
                    break;
                case 2:                                     // Z6: ImageRGB(rows, cols, data) then CopyTo, no swap
                    m = new CvMat(rows, cols, 3, CopyRaw(payload, rows * cols * 3));
                    break;
                case 5 or 6:                                // Z2: imdecode(flags 1) then cvtColor code 4
                    m = SwapRb(OpenCvImage.Imdecode(jpeg, 1));
                    break;
                case 7:                                     // Z3: imdecode(flags 1), cvtColor code 4, copyMakeBorder(0, 0, 160, 160)
                    m = AddZeroColumns(SwapRb(OpenCvImage.Imdecode(jpeg, 1)), 160);
                    break;
                case 8:                                     // Z4: MiniGrayToJpeg, imdecode(flags 1), cvtColor code 4
                    m = SwapRb(OpenCvImage.Imdecode(jpeg, 1));
                    break;
                case 9:                                     // Z5: MiniColorToJpeg, imdecode(flags 1), cvtColor code 4, Resize(1)
                    m = Resize(SwapRb(OpenCvImage.Imdecode(jpeg, 1)), cols, rows);
                    break;
                default:                                    // Z0, Z1: 0, 3, 4, 10..255
                    return Unsupported(encoding, log);
            }
        }
        catch (OpenCvException e)
        {
            return OpenCvFailed(e, log);
        }
        return Check(m, rows, cols, log);
    }

    // O8: cv::error prints "OpenCV Error: ..." to stderr and to logcat (tag cv::error(), priority ERROR) when it throws, before the exception
    // unwinds. One line is emitted here, in the log-prefix form with the logcat tag as the key; the stderr copy is the same text.
    // The exception itself leaves the engine's helper (O9, the catcher above it is an M11 matter); this port reports a failed decode.
    private static EncodedImageDecodeResult OpenCvFailed(OpenCvException e, Action<string>? log)
    {
        log?.Invoke("error: cv::error(): " + e.Message);
        return new EncodedImageDecodeResult { Ok = false, Error = e.Message };
    }

    // the size check 0x004F2CDA..0x004F2D34 / 0x004F2658..0x004F2674 (J38, D7): rows against EncodedImage+0x18, cols against +0x14;
    // a mismatch is sWarningF("EncodedImage.DecodeImageRGB.BadDecode" in both helpers), expected width x height then got cols x rows,
    // with no _errG store and no break.
    private static EncodedImageDecodeResult Check(CvMat m, int rows, int cols, Action<string>? log)
    {
        if (m.Rows != rows || m.Cols != cols)
        {
            const string key = "EncodedImage.DecodeImageRGB.BadDecode";
            string text = $"Failed to decode {cols}x{rows} image from buffer. Got {m.Cols}x{m.Rows}";
            log?.Invoke($"warning: {key}: {text}");
            return new EncodedImageDecodeResult { Ok = false, Error = $"{key}: {text}" };
        }
        return new EncodedImageDecodeResult { Ok = true, Image = m };
    }

    /// <summary>
    /// Y1/Z1, D1..D4: the unsupported encodings (0, 3, 4, 10..255) call sErrorF with the key <c>EncodedImage.DecodeImageRGB.UnsupportedEncoding</c>
    /// (the literal says DecodeImageRGB in the gray helper too) and the format "Encoding %s not yet supported for decoding image chunks",
    /// then store <c>_errG = 1</c> and run the <c>_errBreakOnError</c> gate (stores 0x4F22AC RGB / 0x4F297A gray, breaks 0x4F22B4 / 0x4F2982);
    /// the helper returns failure with no image. <c>EnumToString</c> returns NULL above 9 (D5); a NULL <c>%s</c> is rendered "(null)", which
    /// is the phone libc's printf (bionic) and so a system-library boundary, not a value the shipped files settle.
    /// </summary>
    private static EncodedImageDecodeResult Unsupported(byte encoding, Action<string>? log)
    {
        const string key = "EncodedImage.DecodeImageRGB.UnsupportedEncoding";
        string text = $"Encoding {EnumToString(encoding)} not yet supported for decoding image chunks";
        log?.Invoke($"error: {key}: {text}");                 // sErrorF first (D2/D3) ...
        Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();  // ... then _errG = 1 and the break gate (D4)
        return new EncodedImageDecodeResult { Ok = false, Error = $"{key}: {text}" };
    }

    /// <summary>The names in <c>EnumToString(ImageEncoding)</c> for 0..9 (pointer table 0x01034A60); NULL above 9.</summary>
    internal static string EnumToString(byte encoding) => encoding switch
    {
        0 => "NoneImageEncoding",
        1 => "RawGray",
        2 => "RawRGB",
        3 => "YUYV",
        4 => "BAYER",
        5 => "JPEGGray",
        6 => "JPEGColor",
        7 => "JPEGColorHalfWidth",
        8 => "JPEGMinimizedGray",
        9 => "JPEGMinimizedColor",
        _ => "(null)",        // NULL %s: bionic printf renders "(null)" (phone libc boundary)
    };

    // fidelity: M3-037
    /// <summary>
    /// SD2 policy M3-037: the engine reads <paramref name="need"/> bytes from the vector start with no length check
    /// (Y2, Y3, Z6, Z7). A short payload's missing bytes are stale or uninitialised heap, which no shipped artifact
    /// can derive, so they read as 0 here; a long payload's extra bytes are ignored.
    /// </summary>
    private static byte[] CopyRaw(byte[] payload, int need)
    {
        var dst = new byte[need];
        Array.Copy(payload, dst, Math.Min(payload.Length, need));
        return dst;
    }

    /// <summary>
    /// Y4: cvtColor code 7 (COLOR_RGB2GRAY), 8U, the static coefficient triple at rodata 0xE2AB0 = {4899, 9617, 1868} (J112):
    /// <c>Y = (4899*R + 9617*G + 1868*B + 8192) &gt;&gt; 14</c> with an arithmetic shift, R the source byte 0.
    /// </summary>
    private static byte[] RgbToGray(byte[] rgb, int pixels)
    {
        var gray = new byte[pixels];
        for (int i = 0; i < pixels; i++)
        {
            int r = rgb[3 * i], g = rgb[3 * i + 1], b = rgb[3 * i + 2];
            gray[i] = (byte)((4899 * r + 9617 * g + 1868 * b + 8192) >> 14);
        }
        return gray;
    }

    /// <summary>Z7: cvtColor code 8 (COLOR_GRAY2BGR) replicates the gray byte into all three channels.</summary>
    private static byte[] GrayToRgb(byte[] gray, int pixels)
    {
        var rgb = new byte[pixels * 3];
        for (int i = 0; i < pixels; i++)
        {
            byte v = gray[i];
            rgb[3 * i] = v; rgb[3 * i + 1] = v; rgb[3 * i + 2] = v;
        }
        return rgb;
    }

    /// <summary>cvtColor code 4 (COLOR_BGR2RGB), 8U, 3 channels (J40): each pixel becomes (src[2], src[1], src[0]); an empty Mat stays empty.</summary>
    private static CvMat SwapRb(CvMat src)
    {
        if (src.IsEmpty) return src;
        var d = new byte[src.Data.Length];
        for (int i = 0; i + 2 < d.Length; i += 3)
        {
            d[i] = src.Data[i + 2];
            d[i + 1] = src.Data[i + 1];
            d[i + 2] = src.Data[i];
        }
        return new CvMat(src.Rows, src.Cols, src.Channels, d);
    }

    /// <summary>copyMakeBorder(src, dst, 0, 0, each, each, BORDER_CONSTANT, Scalar(0,0,0,0)) (J39); the destination is created even for an empty source.</summary>
    private static CvMat AddZeroColumns(CvMat src, int each)
    {
        int newCols = src.Cols + 2 * each;
        if (src.IsEmpty) return new CvMat(src.Rows, newCols, src.Channels, Array.Empty<byte>());
        int cn = src.Channels;
        var dst = new byte[(long)src.Rows * newCols * cn];
        int rowBytes = src.Cols * cn, newRowBytes = newCols * cn, pad = each * cn;
        for (int y = 0; y < src.Rows; y++)
            Array.Copy(src.Data, y * rowBytes, dst, y * newRowBytes + pad, rowBytes);
        return new CvMat(src.Rows, newCols, cn, dst);
    }

    /// <summary>The engine's Resize wrapper (J37): cv::resize with INTER_LINEAR to (cols, rows); skipped when the sizes already match.</summary>
    private static CvMat Resize(CvMat src, int cols, int rows)
    {
        return OpenCvResize.ResizeLinear(src, cols, rows);
    }
}
