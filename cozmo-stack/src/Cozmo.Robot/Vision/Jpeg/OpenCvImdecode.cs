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
        // findDecoder: the JPEG decoder's signature is FF D8 FF; the signature string is space-padded, so fewer than 3 bytes never match.
        if (!(buf.Length >= 3 && buf[0] == 0xFF && buf[1] == 0xD8 && buf[2] == 0xFF))
            return CvMat.Empty;      // no registered decoder matched (other formats' decoders are outside the inventory rows)

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
