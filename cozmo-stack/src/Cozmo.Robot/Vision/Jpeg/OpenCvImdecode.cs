using System.Buffers.Binary;

namespace Cozmo.Robot.Vision.Jpeg;

// fidelity: M3-001, M3-018
// OpenCV 3.1's cv::imdecode and its JpegDecoder wrapper as shipped in libopencv_imgcodecs.so (J5, J6, J9..J15, J114, J115, and
// the default-table row of 20261010-jpeg-defaults-and-save-rows.md). The decoder underneath is JpegDecoder (libjpeg 9).

/// <summary>An 8-bit cv::Mat: rows x cols x channels, continuous. An empty Mat has Rows == 0.</summary>
internal sealed class CvMat
{
    public static readonly CvMat Empty = new(0, 0, 1, Array.Empty<byte>());

    public CvMat(int rows, int cols, int channels, byte[] data)
    {
        Rows = rows;
        Cols = cols;
        Channels = channels;
        Data = data;
    }

    public int Rows { get; }
    public int Cols { get; }
    public int Channels { get; }
    public byte[] Data { get; }
    public bool IsEmpty => Rows == 0 || Cols == 0;
}

/// <summary>cv::Exception thrown by cv::error (J115): an assertion or error inside a shipped OpenCV function.</summary>
internal sealed class OpenCvException : Exception
{
    // cv::error(const Exception&) prints sprintf("OpenCV Error: %s (%s) in %s, file %s, line %d", cvErrorStr(code), err, func, file, line)
    // (O8); cvErrorStr texts are the ones in libopencv_core for the three codes this port raises (0xE29CD, 0xE272D, 0xE288F).
    private static string ErrorStr(int code) => code switch
    {
        -215 => "Assertion failed",
        -4 => "Insufficient memory",
        -211 => "One of arguments' values is out of range",
        _ => throw new NotSupportedException($"cvErrorStr({code}) is not carried by this port"),
    };

    public OpenCvException(int code, string err, string func, string file, int line)
        : base($"OpenCV Error: {ErrorStr(code)} ({err}) in {func}, file {file}, line {line}")
    {
        Code = code;
        Err = err;
        Func = func;
        File = file;
        Line = line;
    }

    public int Code { get; }
    public string Err { get; }
    public string Func { get; }
    public string File { get; }
    public int Line { get; }
}

internal static class OpenCvImage
{
    private const string ImdecodeFile = "/Users/build/buildAgent/work/2b4c7202ccc51830/opencv-3.1.0/modules/imgcodecs/src/loadsave.cpp";
    private const string ImdecodeFunc = "void* cv::imdecode_(const cv::Mat&, int, int, cv::Mat*)";

    /// <summary>
    /// <c>cv::imdecode(buf, flags, dst)</c> for flags 0 (IMREAD_GRAYSCALE) and 1 (IMREAD_COLOR), the two values the engine passes
    /// (J2, J36). Returns an empty Mat when the decoder fails (J114). Throws <see cref="OpenCvException"/> for the empty-input
    /// assertion (J6, J114) and <see cref="NotSupportedException"/> where the shipped code does something this port does not carry.
    /// </summary>
    public static CvMat Imdecode(byte[] buf, int flags) => Imdecode(buf, flags, out _);

    /// <summary>
    /// As <see cref="Imdecode(byte[], int)"/>, also returning the line libjpeg's standard error manager wrote to stderr (its first warning, or
    /// null); the phone shows stderr nowhere, so nothing else consumes it.
    /// </summary>
    public static CvMat Imdecode(byte[] buf, int flags, out string? stderrLine)
    {
        stderrLine = null;
        if (flags != 0 && flags != 1)
            throw new NotSupportedException("imdecode flags other than 0 and 1 are not used on the engine's path (J2, J36)");
        // CV_Assert(!buf.empty() && buf.isContinuous()) (the data pointer is null only for an empty vector, cbz at 0xF6C8)
        if (buf.Length == 0)
            throw new OpenCvException(-215, "!buf.empty() && buf.isContinuous()", ImdecodeFunc, ImdecodeFile, 490);
        // fidelity: M3-045
        // C.1 corrects A3: allocate 32 bytes, fill with spaces, copy min(32,size). Every check sees length 32.
        var signature = new byte[32];
        Array.Fill(signature, (byte)0x20);
        buf.AsSpan(0, Math.Min(32, buf.Length)).CopyTo(signature);
        var window = signature.AsSpan();
        if (window.StartsWith("BM"u8)) return Unported("BMP");
        if (window.StartsWith("#?RGBE"u8) || window.StartsWith("#?RADIANCE"u8)) return Unported("HDR");
        if (!window.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            // C.4: WebPDecoder is identified by RTTI at 0xCE2D8. C.2: probe status 0 alone matches.
            if (WebPProbe(signature, 0x201) == 0) return Unported("WebP");
            if (window.StartsWith(new byte[] { 0x59, 0xA6, 0x6A, 0x95 })) return Unported("SunRaster");
            // 0x17A04: the phone libc _ctype_ isspace check (GOT 0x174934, R_ARM_GLOB_DAT; libc is unshipped), including the signed-char EOF exclusion.
            if (window.Length > 2 && window[0] == 'P' && window[1] is >= (byte)'1' and <= (byte)'6'
                && window[2] is 9 or 10 or 11 or 12 or 13 or 32) return Unported("PxM");
            if (window.StartsWith(new byte[] { 0x49, 0x49, 0x2A, 0 })
                || window.StartsWith(new byte[] { 0x4D, 0x4D, 0, 0x2A })) return Unported("TIFF");
            if (window.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 13, 10, 0x1A, 10 })) return Unported("PNG");
            if (window.StartsWith(new byte[] { 0, 0, 0, 12, 0x6A, 0x50, 32, 32, 13, 10, 0x87, 10 })) return Unported("JPEG2000");
            if (window.StartsWith(new byte[] { 0x76, 0x2F, 0x31, 1 })) return Unported("EXR");
            return CvMat.Empty; // A5: no match returns the prebuilt empty Mat silently.
        }

        var dec = new JpegDecoder(buf);
        try
        {
            return ImdecodeWith(dec, flags);
        }
        finally
        {
            stderrLine = dec.FirstWarning;
        }
    }

    // fidelity: M3-045
    private static CvMat Unported(string decoder)
    {
        Behavior.SteppedBehavior.ReportMissing($"M3-045 | {decoder} signature matched: decoder body is not ported");
        return CvMat.Empty;
    }

    // fidelity: M3-045
    // C.2/C.3 P1..P13: 0x375A6 -> 0x37088 -> 0x36DC0. The feature struct is a non-null local on the
    // signature path; its values are discarded by checkSignature. Only the returned status is observed.
    internal static int WebPProbe(byte[]? data, int version, bool featuresPresent = true)
    {
        if ((version >> 8) != 2 || !featuresPresent || data is null) return 2; // P1
        // P2 zeroes the local feature struct (no output is consumed by checkSignature).
        int p = 0, n = data.Length;
        if (n <= 11) return 7; // P3
        uint riff = 0;
        if (Tag(data, p, "RIFF"u8)) // P4
        {
            if (!Tag(data, 8, "WEBP"u8)) return 3;
            riff = U32(data, 4);
            if (riff < 12 || riff > 0xFFFFFFF6u) return 3;
            p = 12;
            n -= 12;
        }
        bool foundRiff = riff != 0;
        if (n <= 7) return 7; // P5
        if (Tag(data, p, "VP8X"u8)) // P6
        {
            if (U32(data, p + 4) != 10) return 3;
            if (n <= 17) return 7;
            ulong width = U24(data, p + 12) + 1u, height = U24(data, p + 15) + 1u;
            if (width * height >= 0x100000000UL || !foundRiff) return 3;
            return 0; // headers == NULL: neither flags nor later chunks affect this result
        }
        if (n <= 3) return 7; // P7
        if (!foundRiff && Tag(data, p, "ALPH"u8))
        {
            // P8: total-against-riff check is unreachable on this path. ALPH metadata is discarded by the caller.
            while (true)
            {
                if (n <= 7) return 7;
                uint c = U32(data, p + 4);
                if (c > 0xFFFFFFF6u) return 3;
                uint d = (c + 9u) & ~1u;
                if (Tag(data, p, "VP8 "u8) || Tag(data, p, "VP8L"u8)) break;
                if ((uint)n < d) return 7;
                p += (int)d;
                n -= (int)d;
            }
        }
        // P9: both memcmps run before the remaining-size gate. An out-of-window comparison cannot affect status 7.
        bool vp8 = Tag(data, p, "VP8 "u8), vp8l = Tag(data, p, "VP8L"u8);
        if (n <= 7) return 7;
        uint cs;
        bool lossless;
        if (vp8 || vp8l)
        {
            cs = U32(data, p + 4);
            if (riff > 11 && cs > riff - 12u) return 3;
            lossless = vp8l;
            p += 8;
            n -= 8;
        }
        else
        {
            cs = (uint)n;
            lossless = n != 0 && data[p] == 0x2F;
        }
        if (cs > 0xFFFFFFF6u) return 3; // P10
        if (lossless) // P11: no dimension, version or bit-reader-error rejection
        {
            if (n <= 4) return 7;
            return data[p] == 0x2F ? 0 : 3;
        }
        if (n <= 9) return 7; // P12
        if (!Tag(data, p + 3, new byte[] { 0x9D, 1, 0x2A })) return 3;
        uint bits = U24(data, p);
        if ((bits & 1) != 0 || ((bits >> 1) & 7) > 3 || (bits & 16) == 0 || (bits >> 5) >= cs) return 3;
        return 0; // P13; zero width/height are accepted, and alpha/features are unconsumed
    }

    private static bool Tag(byte[] data, int p, ReadOnlySpan<byte> tag) => p <= data.Length - tag.Length && data.AsSpan(p, tag.Length).SequenceEqual(tag);
    private static uint U32(byte[] data, int p) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(p, 4));
    private static uint U24(byte[] data, int p) => (uint)(data[p] | data[p + 1] << 8 | data[p + 2] << 16);

    private static CvMat ImdecodeWith(JpegDecoder dec, int flags)
    {
        // JpegDecoder::readHeader (J12, J13)
        int width, height, components;
        try
        {
            dec.ReadHeader(out width, out height, out components);
        }
        catch (JpegFatalException)
        {
            return CvMat.Empty;
        }
        int type = components > 1 ? 3 : 1;
        // flags != IMREAD_UNCHANGED: 8-bit depth, 3 channels for IMREAD_COLOR (1), 1 for IMREAD_GRAYSCALE (0)
        int channels = (flags & 1) != 0 ? 3 : 1;
        _ = type;
        byte[] data = CreateMatData(height, width, channels);
        if (!ReadData(dec, width, height, channels, data)) return CvMat.Empty;
        return new CvMat(height, width, channels, data);
    }

    private const string CoreSrc = "/Users/build/buildAgent/work/2b4c7202ccc51830/opencv-3.1.0/modules/core/src/";

    // Mat::create(rows, cols, type) as imdecode_ calls it (O1..O6): setSize's 32-bit size arithmetic first (O3), then StdMatAllocator::allocate
    // (O4) -> fastMalloc (O5), whose failure is cv::OutOfMemoryError (O6).
    // fidelity: M3-001
    private static byte[] CreateMatData(int rows, int cols, int elemSize)
    {
        // O3 (CORE 0x80F98): total starts at the element size and is multiplied by cols, then rows (i = d-1 .. 0); a 64-bit product with a non-zero
        // high word is cv::error(-211, ..., line 323).
        ulong total = (ulong)elemSize;
        foreach (int s in new[] { cols, rows })
        {
            ulong total1 = total * (ulong)s;
            if ((total1 >> 32) != 0)
                throw new OpenCvException(-211, "The total matrix size does not fit to \"size_t\" type",
                    "void cv::setSize(cv::Mat&, int, const int*, const size_t*, bool)", CoreSrc + "matrix.cpp", 323);
            total = (uint)total1;
        }
        if (total == 0) return Array.Empty<byte>();
        // O5: fastMalloc(size) calls malloc(size + 0x14) in 32 bits with no check, so a size of 0xFFFFFFEC or more wraps to a tiny request and the
        // decode then writes past it; that memory corruption is not reproduced.
        if (total >= 0xFFFFFFECUL)
            throw new NotSupportedException("O5: fastMalloc(size + 0x14) wraps in 32 bits for sizes >= 0xFFFFFFEC (the original corrupts memory)");
        try
        {
            if (total > (ulong)Array.MaxLength) throw new OutOfMemoryException();       // a .NET array cannot hold it: the allocation fails
            return new byte[total];
        }
        catch (OutOfMemoryException)
        {
            // O6/O8b: when malloc returns NULL is device state (HARDWARE_ONLY: the phone's memory); a .NET allocation failure stands for it.
            throw new OpenCvException(-4, $"Failed to allocate {total} bytes", "void* cv::OutOfMemoryError(size_t)", CoreSrc + "alloc.cpp", 52);
        }
    }

    // JpegDecoder::readData (J14, J15, J56, J71, J72)
    private static bool ReadData(JpegDecoder dec, int width, int height, int channels, byte[] data)
    {
        bool result = false;
        bool color = channels > 1;
        if (width == 0 || height == 0) return false;        // m_state && m_width && m_height (J14)
        var buffer = new byte[(long)width * 4];                // alloc_sarray(m_width * 4, 1): never zero-filled by the allocator (J72)
        try
        {
            // check if this is an MJPEG image (no Huffman tables at all): load the default tables (J47 default-table row)
            dec.LoadDefaultTablesIfMissing();
            if (color)
            {
                dec.OutColorSpace = dec.NumComponents != 4 ? JpegColorSpace.Rgb : JpegColorSpace.Cmyk;
                dec.OutColorComponents = dec.NumComponents != 4 ? 3 : 4;
            }
            else
            {
                dec.OutColorSpace = dec.NumComponents != 4 ? JpegColorSpace.Grayscale : JpegColorSpace.Cmyk;
                dec.OutColorComponents = dec.NumComponents != 4 ? 1 : 4;
            }
            dec.StartDecompress();
            int step = width * channels;
            int outPos = 0;
            for (int rows = height; rows-- > 0; outPos += step)
            {
                dec.ReadScanline(buffer);
                if (color)
                {
                    if (dec.OutColorComponents == 3)
                    {
                        // icvCvt_RGB2BGR_8u_C3R
                        for (int i = 0; i < width; i++)
                        {
                            data[outPos + 3 * i] = buffer[3 * i + 2];
                            data[outPos + 3 * i + 1] = buffer[3 * i + 1];
                            data[outPos + 3 * i + 2] = buffer[3 * i];
                        }
                    }
                    else
                    {
                        for (int i = 0; i < width; i++)
                        {
                            CmykToRgb(buffer, 4 * i, out int r, out int g, out int b);
                            data[outPos + 3 * i] = (byte)b;
                            data[outPos + 3 * i + 1] = (byte)g;
                            data[outPos + 3 * i + 2] = (byte)r;
                        }
                    }
                }
                else
                {
                    if (dec.OutColorComponents == 1)
                    {
                        Array.Copy(buffer, 0, data, outPos, width);
                    }
                    else
                    {
                        for (int i = 0; i < width; i++)
                        {
                            CmykToRgb(buffer, 4 * i, out int r, out int g, out int b);
                            data[outPos + i] = (byte)((4899 * r + 9617 * g + 1868 * b + 8192) >> 14);
                        }
                    }
                }
            }
            result = true;
            dec.FinishDecompress();
        }
        catch (JpegFatalException)
        {
            // longjmp to the setjmp: result keeps the value it had (true once all rows were read, J15)
        }
        return result;
    }

    // icvCvt_CMYK2BGR_8u_C4C3R (J56): c = k - ((255 - c) * k >> 8) per channel, no division by 255
    private static void CmykToRgb(byte[] p, int o, out int r, out int g, out int b)
    {
        int c = p[o], m = p[o + 1], y = p[o + 2], k = p[o + 3];
        r = k - ((k * (255 - c)) >> 8);
        g = k - ((k * (255 - m)) >> 8);
        b = k - ((k * (255 - y)) >> 8);
    }
}
