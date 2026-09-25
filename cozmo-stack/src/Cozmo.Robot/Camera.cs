using Cozmo.Protocol;
using StbImageSharp;
using StbImageWriteSharp;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot;

/// <summary>Camera resolutions the robot can be asked for (official Anki.Cozmo.ImageResolution).</summary>
public static class CameraResolutions
{
    private static readonly (int w, int h)[] Table =
    {
        (16, 16),    // VerificationSnapshot
        (40, 30),    // QQQQVGA
        (80, 60),    // QQQVGA
        (160, 120),  // QQVGA
        (320, 240),  // QVGA
        (400, 296),  // CVGA
        (640, 480),  // VGA
        (800, 600),  // SVGA
        (1024, 768), // XGA
        (1280, 960), // SXGA
        (1600, 1200),// UXGA
        (2048, 1536),// QXGA
        (3200, 2400),// QUXGA
    };

    public static (int Width, int Height) Size(int resolution) =>
        resolution >= 0 && resolution < Table.Length ? Table[resolution] : (0, 0);
}

/// <summary>One complete camera frame, reassembled from <see cref="ImageChunk"/> messages.</summary>
public sealed class CameraFrame
{
    /// <summary>Robot-assigned image id; increments per frame.</summary>
    public uint ImageId { get; init; }
    /// <summary>The frame's timestamp: the last chunk's (A11, R7).</summary>
    public uint Timestamp { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    /// <summary>Official Anki.Cozmo.ImageEncoding value the engine settled on (8 = JPEGMinimizedGray, 9 = JPEGMinimizedColor).</summary>
    public byte Encoding { get; init; }
    public byte Resolution { get; init; }
    /// <summary>
    /// <c>EncodedImage::IsColor</c> for <see cref="Encoding"/> (A7, <see cref="EncodedImageDecoder.IsColor"/>): true for
    /// 2, 3, 4, 6, 7, 9 and anything above 8. A colour frame (9) is encoded at half the resolution's width.
    /// </summary>
    public bool IsColor { get; init; }
    /// <summary>The second payload byte. Not entropy data; its meaning is not established.</summary>
    public byte StreamMarker { get; init; }
    /// <summary>
    /// True if this frame arrived while the sensor was still settling (policy M3-004: flagged and delivered like any
    /// other; the engine has no warm-up discard). See <see cref="CozmoCamera.WarmUpFrames"/>.
    /// </summary>
    public bool IsWarmUp { get; init; }
    /// <summary>Position of this frame in the stream, counting from the request that started it.</summary>
    public int FrameIndex { get; init; }
    /// <summary>Width the JPEG itself carries: half of <see cref="Width"/> for a colour frame.</summary>
    public int JpegWidth { get; init; }
    public int ChunkCount { get; init; }
    /// <summary>
    /// A standalone JPEG file for encodings 8 and 9 (the header restored and the byte stuffing reinserted,
    /// <see cref="MiniJpeg.ToJpeg"/>); the payload itself for other encodings; empty for a payload that is empty or
    /// all 0xFF (policy M3-020).
    /// </summary>
    public byte[] Jpeg { get; init; } = Array.Empty<byte>();
    /// <summary>The bytes exactly as the robot sent them, before header reconstruction.</summary>
    public byte[] RawPayload { get; init; } = Array.Empty<byte>();
    /// <summary>Gyro rates sampled with this frame, if the matching ImageImuData arrived.</summary>
    public (float X, float Y, float Z)? GyroRates { get; internal set; }
    public DateTime ReceivedUtc { get; init; } = DateTime.UtcNow;

    /// <summary><c>DecodeImageGray</c> (A8, A11): a 320 x 240 gray image, or false with the reason.</summary>
    public bool TryDecodeGray(out GrayImage? image, out string? error) => EncodedImageDecoder.TryDecodeGray(this, out image, out error);

    /// <summary><c>DecodeImageRGB</c> (A9..A11): 320 x 240 RGB, row-major, 3 bytes a pixel, or false with the reason.</summary>
    public bool TryDecodeRgb(out byte[]? rgb, out string? error) => EncodedImageDecoder.TryDecodeRgb(this, out rgb, out error);

    // fidelity: M3-018
    /// <summary>
    /// What <c>EncodedImage::Save</c> writes (A25): encoding 9 is decoded to RGB (A9, A10) and written as a JPEG at
    /// quality 90; encoding 8 is the reconstructed JPEG; any other encoding is the raw bytes. The JPEG writer is
    /// StbImageWriteSharp in place of OpenCV's, so the bytes are not the engine's. Throws
    /// <see cref="InvalidDataException"/> when a colour frame does not decode.
    /// </summary>
    public byte[] PresentationJpeg()
    {
        if (Encoding == MiniJpeg.EncodingJpegMinimizedColor)
        {
            if (!TryDecodeRgb(out var rgb, out var error)) throw new InvalidDataException(error);
            using var output = new MemoryStream();
            new ImageWriter().WriteJpg(rgb!, EncodedImageDecoder.Columns, EncodedImageDecoder.Rows,
                                       StbImageWriteSharp.ColorComponents.RedGreenBlue, output, EncodedImageDecoder.SaveQuality);
            return output.ToArray();
        }
        return Encoding == MiniJpeg.EncodingJpegMinimizedGray ? Jpeg : RawPayload;
    }

    /// <summary>Writes <see cref="PresentationJpeg"/> (A25).</summary>
    public void Save(string path) => File.WriteAllBytes(path, PresentationJpeg());
    public override string ToString() =>
        $"CameraFrame #{ImageId} {Width}x{Height} {(IsColor ? "colour" : "gray")} chunks={ChunkCount} " +
        $"jpeg={Jpeg.Length}B{(IsWarmUp ? " (warm-up, torn)" : "")}";
}

// fidelity: M3-001, M3-018, M3-020
/// <summary>
/// <c>EncodedImage::DecodeImageGray</c> and <c>DecodeImageRGB</c> (M3 inventory A7..A11).
///
/// <list type="bullet">
/// <item><b>Gray (A8, tbh 0x004F2898).</b> 1: the payload copied as the image; 3 and 4: UnsupportedEncoding;
/// 5, 6: decoded as a grayscale JPEG; 7: the same, then a border of 160 zero columns left and right; 8: the
/// reconstructed JPEG decoded; 9: the colour JPEG (built at half width) decoded to gray, then resized to 320 x 240
/// by <see cref="ResizeLinear"/>.</item>
/// <item><b>RGB (A9, tbh 0x004F21AE).</b> 9: the colour JPEG at half width decoded as colour (BGR, then
/// <c>cvtColor(4)</c> BGR2RGB, which is RGB order), then resized to 320 x 240 by <see cref="ResizeLinear"/>
/// (<c>ImageBase&lt;RGB&gt;::Resize(h, w, 1)</c>, A10).</item>
/// <item><b>Check (A11).</b> The result must be 240 rows by 320 columns, otherwise BadDecode.</item>
/// </list>
/// The JPEG entropy decoding is StbImageSharp in place of OpenCV's libjpeg (<c>cv::imdecode</c>): the geometry and
/// the resize are the engine's; pixel values may differ in the last bit where the two decoders' IDCT, chroma
/// upsampling and colour conversion round differently.
/// MISSING: gray case 2 (ToGray of a raw RGB payload: the conversion is not in the rows), the RGB dispatch for any
/// encoding but 9 (A9 names only "the JPEG cases" and case 9), a raw case-1 payload that is not exactly 320 x 240
/// bytes, and encodings 0 and above 9 (outside the tbh table): each is reported as a decode failure.
/// </summary>
public static class EncodedImageDecoder
{
    /// <summary>The only geometry the engine accepts: 240 rows (+0x18), 320 columns (+0x14) (R2, A11).</summary>
    public const int Rows = 240, Columns = 320;

    /// <summary><c>ImageRGB::Save(path, 90)</c> (A25, <c>movs r2,#0x5a</c>).</summary>
    public const int SaveQuality = 90;

    /// <summary>cv::INTER_LINEAR, the ResizeMethod value 1 the decode passes (A10).</summary>
    public const int InterLinear = 1;

    // fidelity: M3-018
    /// <summary>
    /// <c>EncodedImage::IsColor</c> (A7, tbb table 0x004F2110): true for 2, 3, 4, 6, 7, 9 and any value above 8; false
    /// for 1, 5 and 8. MISSING: encoding 0 fails a VERIFY in the engine, and what IsColor then returns is not in the
    /// rows; false is returned here.
    /// </summary>
    public static bool IsColor(byte encoding) => encoding switch
    {
        > 8 => true,
        2 or 3 or 4 or 6 or 7 => true,
        _ => false,
    };

    public static bool TryDecodeGray(CameraFrame f, out GrayImage? image, out string? error)
    {
        image = null;
        int h = f.Height, w = f.Width;
        byte[] pixels;
        int rows, cols;
        switch (f.Encoding)
        {
            case 1:
                if (f.RawPayload.Length != w * h)
                    return Fail($"MISSING: a raw gray payload of {f.RawPayload.Length} bytes for {w}x{h} (M3-001)", out error);
                pixels = (byte[])f.RawPayload.Clone(); rows = h; cols = w;
                break;
            case 3 or 4:
                return Fail($"EncodedImage.DecodeImageGray.UnsupportedEncoding: {f.Encoding}", out error);
            case 5 or 6 or 7 or 8 or 9:
                if (!TryJpeg(f.Jpeg, StbImageSharp.ColorComponents.Grey, out pixels, out cols, out rows, out error)) return false;
                if (f.Encoding == 7)
                {
                    // copyMakeBorder(0, 0, 160, 160, BORDER_CONSTANT 0)
                    var padded = new byte[rows * (cols + 320)];
                    for (int y = 0; y < rows; y++) Array.Copy(pixels, y * cols, padded, y * (cols + 320) + 160, cols);
                    pixels = padded; cols += 320;
                }
                else if (f.Encoding == 9)
                {
                    pixels = ResizeLinear(pixels, cols, rows, 1, w, h);
                    cols = w; rows = h;
                }
                break;
            default:
                return Fail($"MISSING: gray decode of encoding {f.Encoding} is not in the rows (M3-001)", out error);
        }
        if (rows != Rows || cols != Columns)
            return Fail($"EncodedImage.DecodeImageGray.BadDecode: Failed to decode {Columns}x{Rows} image. Got {cols}x{rows}", out error);
        image = new GrayImage(cols, rows, pixels);
        error = null;
        return true;
    }

    public static bool TryDecodeRgb(CameraFrame f, out byte[]? rgb, out string? error)
    {
        rgb = null;
        if (f.Encoding != MiniJpeg.EncodingJpegMinimizedColor)
            return Fail($"MISSING: RGB decode of encoding {f.Encoding} is not in the rows (M3-018)", out error);
        if (!TryJpeg(f.Jpeg, StbImageSharp.ColorComponents.RedGreenBlue, out var pixels, out int cols, out int rows, out error))
            return false;
        pixels = ResizeLinear(pixels, cols, rows, 3, f.Width, f.Height);
        cols = f.Width; rows = f.Height;
        if (rows != Rows || cols != Columns)
            return Fail($"EncodedImage.DecodeImageRGB.BadDecode: Failed to decode {Columns}x{Rows} image. Got {cols}x{rows}", out error);
        rgb = pixels;
        return true;
    }

    private static bool TryJpeg(byte[] jpeg, StbImageSharp.ColorComponents components, out byte[] pixels,
                                out int width, out int height, out string? error)
    {
        pixels = Array.Empty<byte>(); width = height = 0;
        // policy M3-020: an empty or all-0xFF payload has no JPEG at all
        if (jpeg.Length == 0) return Fail("EncodedImage.Decode: empty payload (policy M3-020)", out error);
        try
        {
            var img = ImageResult.FromMemory(jpeg, components);
            pixels = img.Data; width = img.Width; height = img.Height;
            error = null;
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException)
        {
            return Fail($"EncodedImage.Decode: the JPEG did not decode ({e.Message})", out error);
        }
    }

    private static bool Fail(string why, out string? error) { error = why; return false; }

    // fidelity: M3-018
    /// <summary>
    /// <c>cv::resize(src, dst, Size(dstCols, dstRows), 0, 0, INTER_LINEAR)</c> for 8-bit images (A10), as OpenCV's
    /// generic fixed-point path computes it:
    /// <list type="bullet">
    /// <item>pixel-centre mapping, <c>f = (d + 0.5) * (src / dst) - 0.5</c> in float, <c>s = floor(f)</c>, weight
    /// <c>f - s</c>;</item>
    /// <item>horizontally, a source index below 0 or at or past the last column is clamped with weight 0;
    /// vertically the two rows are clamped into the image and the weights kept;</item>
    /// <item>weights scaled by 2048 (INTER_RESIZE_COEF_BITS 11) and rounded to the nearest short, half to even;</item>
    /// <item>the horizontal pass in int, the vertical pass <c>(b0 * r0 + b1 * r1 + (1 &lt;&lt; 21)) &gt;&gt; 22</c>,
    /// saturated to 0..255.</item>
    /// </list>
    /// OpenCV's SIMD vertical pass rounds differently for weights that are not exact in 11 bits. The engine's resize
    /// here is 160 → 320 columns and 240 → 240 rows, whose weights (0.75/0.25 and 1/0) are exact, and for which every
    /// path gives <c>(3a + b + 2) &gt;&gt; 2</c> between neighbours and the edge pixel itself at the first and last column.
    /// </summary>
    public static byte[] ResizeLinear(byte[] src, int srcCols, int srcRows, int channels, int dstCols, int dstRows)
    {
        const int CoefScale = 1 << 11;
        double scaleX = (double)srcCols / dstCols, scaleY = (double)srcRows / dstRows;

        var xofs = new int[dstCols];
        var ax = new short[dstCols * 2];
        for (int dx = 0; dx < dstCols; dx++)
        {
            float fx = (float)((dx + 0.5) * scaleX - 0.5);
            int sx = (int)Math.Floor(fx);
            fx -= sx;
            if (sx < 0) { fx = 0; sx = 0; }
            if (sx >= srcCols - 1) { fx = 0; sx = srcCols - 1; }
            xofs[dx] = sx;
            ax[2 * dx] = SatShort((1f - fx) * CoefScale);
            ax[2 * dx + 1] = SatShort(fx * CoefScale);
        }

        var dst = new byte[dstCols * dstRows * channels];
        var r0 = new int[dstCols * channels];
        var r1 = new int[dstCols * channels];
        for (int dy = 0; dy < dstRows; dy++)
        {
            float fy = (float)((dy + 0.5) * scaleY - 0.5);
            int sy = (int)Math.Floor(fy);
            fy -= sy;
            short b0 = SatShort((1f - fy) * CoefScale), b1 = SatShort(fy * CoefScale);
            HResize(src, srcCols, Math.Clamp(sy, 0, srcRows - 1), channels, xofs, ax, r0);
            HResize(src, srcCols, Math.Clamp(sy + 1, 0, srcRows - 1), channels, xofs, ax, r1);
            int o = dy * dstCols * channels;
            for (int i = 0; i < r0.Length; i++)
            {
                int v = (b0 * r0[i] + b1 * r1[i] + (1 << 21)) >> 22;
                dst[o + i] = (byte)Math.Clamp(v, 0, 255);
            }
        }
        return dst;
    }

    private static void HResize(byte[] src, int srcCols, int row, int cn, int[] xofs, short[] ax, int[] outRow)
    {
        int rowBase = row * srcCols * cn;
        for (int dx = 0; dx < xofs.Length; dx++)
        {
            int sx = xofs[dx], sx1 = Math.Min(sx + 1, srcCols - 1);
            for (int c = 0; c < cn; c++)
                outRow[dx * cn + c] = src[rowBase + sx * cn + c] * ax[2 * dx] + src[rowBase + sx1 * cn + c] * ax[2 * dx + 1];
        }
    }

    private static short SatShort(float v) => (short)Math.Clamp(Math.Round(v, MidpointRounding.ToEven), short.MinValue, short.MaxValue);
}

/// <summary>
/// Reassembles <see cref="ImageChunk"/> messages into complete frames and rebuilds a decodable JPEG.
///
/// The robot sends "minimized" JPEG (official ImageEncoding 8 = JPEGMinimizedGray, 9 = JPEGMinimizedColor):
/// the entire JFIF header is stripped, and the 0x00 that JPEG requires after every 0xFF in entropy-coded
/// data is removed. The first payload byte is not entropy data at all: it is a flag saying whether the
/// frame is colour. Restoring a frame therefore means prepending the fixed header the encoder used,
/// patching the height/width into it, dropping that first byte, re-inserting a 0x00 after each 0xFF and
/// appending the end-of-image marker (<see cref="MiniJpeg"/>).
/// </summary>
public sealed class CozmoCamera
{
    /// <summary>
    /// The largest chunk payload the engine will take: <c>cmp.w r1, #0x4b0</c> at 0x004F1CF0, warning
    /// "EncodedImage.AddChunk.ChunkTooBig", "Expecting chunks of size no more than %d, got %zu." A chunk
    /// over it is thrown away where it stands, before any bookkeeping, so the next chunk is out of order.
    /// </summary>
    public const int MaxChunkBytes = 0x4B0;

    /// <summary>
    /// The only resolution <c>AddChunk</c> accepts: <c>cmp r0, #4</c> at 0x004F1D4E, after which it writes
    /// 320 x 240 into the image (<c>movs r0, #0xf0</c>, <c>mov.w r1, #0x140</c>). Anything else warns with
    /// the resolution name from <c>EnumToString(ImageResolution)</c> and the chunk is dropped.
    /// </summary>
    public const int Resolution = 4;

    /// <summary>
    /// What the engine reserves for one image: <c>reserve(0x38400)</c> at 0x004F1DA6, 320 * 240 * 3. Kept
    /// as a note - nothing here needs to preallocate.
    /// </summary>
    public const int ReserveBytes = 0x38400;

    // fidelity: M3-005
    /// <summary>
    /// At most this many completed images per event time go on to vision (A3): HandleImageChunk keys the counter at
    /// RobotToEngineImplMessaging+0x128 to the event time against +0x130; a new time zeroes it, and within the same time
    /// the counter is incremented and the image passes while it is below 3. The 4th and later are dropped with
    /// "Ignoring %dth image (with t=%u) received during basestation tick at %fsec".
    /// </summary>
    public const int MaxImagesPerTick = 3;

    /// <summary>R10: the constructor's image id, 0xFFFFFFFF (0x004F1A8A..0x004F1A8E).</summary>
    public const uint NoImageId = 0xFFFFFFFF;

    private readonly Dictionary<uint, (float, float, float)> _imu = new();
    /// <summary>Reassembly runs on the engine's dispatch thread while callers may restart it.</summary>
    private readonly object _gate = new();

    // The engine's EncodedImage, field for field (M2 inventory App. C §3): the image id it is collecting (+0x1C,
    // 0xFFFFFFFF from the constructor), whether that image is still valid (+0x22), the chunk id it expects next
    // (+0x21), how many chunks it has taken (+0x23), the encoding it settled on (+0x20), the current and previous
    // timestamps (+0xC and +0x10) and the geometry (+0x14, +0x18).
    private readonly List<byte> _buffer = new();
    private uint _imageId = NoImageId;
    private bool _valid;
    /// <summary>Whether the image in hand was already handed out, so it is finished rather than abandoned (diagnostics).</summary>
    private bool _delivered;
    private byte _expectedChunk;
    private byte _chunksTaken;
    private byte _encoding;
    private uint _timestamp, _previousTimestamp;
    private int _width, _height;
    // A3: the per-event-time counter (+0x128) and the time it is keyed to (+0x130)
    private int _imagesThisTick;
    private double _tickTime = double.NaN;

    /// <summary>Raised once per complete frame.</summary>
    public event Action<CameraFrame>? FrameReceived;
    /// <summary>Raised when a frame had to be abandoned (missing or out-of-order chunks).</summary>
    public event Action<uint, string>? FrameDropped;
    /// <summary>
    /// The frames handed on to vision (<c>VisionComponent::SetNextImage</c>, A3, H4): every completed frame except the
    /// 4th and later of one event time (<see cref="MaxImagesPerTick"/>). Raised after <see cref="FrameReceived"/>.
    /// </summary>
    public event Action<CameraFrame>? FrameForVision;
    /// <summary>The camera's warnings (the per-tick cap).</summary>
    public event Action<string>? Log;

    // fidelity: M3-002
    /// <summary>
    /// H1/A1: HandleImageChunk does nothing unless robot+0x29 (time synced, set by SyncTimeAck) is set (0x00535A7A).
    /// <see cref="CozmoRobot"/> points this at the engine's Robot; left null (a camera on its own) nothing is gated.
    /// </summary>
    public Func<bool>? TimeSynced { get; set; }

    // fidelity: M3-005
    /// <summary>
    /// The time of the event a chunk arrived in (A3: the double at [event+0]). <see cref="CozmoRobot"/> gives the
    /// engine tick's BaseStationTimer seconds, which every message dispatched in one tick shares. Left null (a camera
    /// on its own) the cap is not applied.
    /// </summary>
    public Func<double>? EventTime { get; set; }

    /// <summary>
    /// Frames to flag as warm-up after the camera is started (policy M3-004). The sensor takes about eleven frames
    /// to lock: until then every picture is torn. The engine has no such idea - it hands every image it reassembles
    /// to <c>VisionComponent::SetNextImage</c> - so these frames are flagged and still delivered.
    /// </summary>
    public int WarmUpFrames { get; set; } = 15;

    public CameraFrame? LastFrame { get; private set; }
    /// <summary>Frames completed since <see cref="Restart"/>, warm-up included.</summary>
    public int FrameIndex { get; private set; }
    /// <summary>True once the sensor has settled and frames are usable.</summary>
    public bool Settled => FrameIndex > WarmUpFrames;
    public int FramesCompleted { get; private set; }
    public int FramesDropped { get; private set; }
    public int ChunksReceived { get; private set; }
    /// <summary>Chunks thrown away for being larger than <see cref="MaxChunkBytes"/>.</summary>
    public int ChunksRejected { get; private set; }
    /// <summary>Chunks ignored because the robot was not time synced yet (A1).</summary>
    public int ChunksIgnoredBeforeSync { get; private set; }
    /// <summary>Completed frames not handed to vision because of the per-tick cap (A3).</summary>
    public int FramesOverTickCap { get; private set; }

    /// <summary>Call when the camera is (re)started, so the warm-up count begins again.</summary>
    public void Restart()
    {
        lock (_gate)
        {
            _imu.Clear();
            _buffer.Clear();
            _imageId = NoImageId;
            _valid = false;
            _delivered = false;
            _expectedChunk = 0;
            _chunksTaken = 0;
            _timestamp = _previousTimestamp = 0;
            FrameIndex = 0;
        }
    }

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26, CC27): no image in hand, no
    /// counts, no last frame. <see cref="WarmUpFrames"/>, <see cref="TimeSynced"/> and <see cref="EventTime"/> are the
    /// owner's settings and are kept, as are subscribers.
    /// </summary>
    internal void ResetToConstructed()
    {
        lock (_gate)
        {
            _imu.Clear();
            _buffer.Clear();
            _imageId = NoImageId;
            _valid = false;
            _delivered = false;
            _expectedChunk = 0;
            _chunksTaken = 0;
            _encoding = 0;
            _timestamp = _previousTimestamp = 0;
            _width = _height = 0;
            _imagesThisTick = 0;
            _tickTime = double.NaN;
            LastFrame = null;
            FrameIndex = 0;
            FramesCompleted = 0;
            FramesDropped = 0;
            ChunksReceived = 0;
            ChunksRejected = 0;
            ChunksIgnoredBeforeSync = 0;
            FramesOverTickCap = 0;
        }
    }

    /// <summary>Feed every robot message here; the camera ignores the ones it does not care about.</summary>
    public void Handle(RobotMessage m)
    {
        switch (m)
        {
            case ImageChunk c:
                // fidelity: M3-002
                if (TimeSynced is { } synced && !synced()) { lock (_gate) ChunksIgnoredBeforeSync++; break; }   // A1, H1
                Add(c);
                break;
            case ImageImuData d: lock (_gate) _imu[d.ImageId] = (d.RateX, d.RateY, d.RateZ); break;
        }
    }

    private void Add(ImageChunk c)
    {
        CameraFrame? completed;
        bool toVision = false;
        string? capWarning = null;
        var dropped = new List<(uint Id, string Why)>();
        lock (_gate)
        {
            completed = AddLocked(c, dropped);
            if (completed is not null) toVision = PassesTickCapLocked(completed, out capWarning);
        }
        foreach (var d in dropped) FrameDropped?.Invoke(d.Id, d.Why);
        if (completed is null) return;
        FrameReceived?.Invoke(completed);
        if (capWarning is not null) Log?.Invoke(capWarning);
        if (toVision) FrameForVision?.Invoke(completed);
    }

    // fidelity: M3-005
    /// <summary>
    /// A3 (0x00535B0E, 0x00535B28..0x00535B52): a new event time zeroes the counter and the image passes; within the
    /// same time the counter is incremented and the image passes while it is below 3.
    /// </summary>
    private bool PassesTickCapLocked(CameraFrame f, out string? warning)
    {
        warning = null;
        if (EventTime is not { } time) return true;
        double t = time();
        if (t != _tickTime)
        {
            _tickTime = t;
            _imagesThisTick = 0;
            return true;
        }
        _imagesThisTick++;
        if (_imagesThisTick < MaxImagesPerTick) return true;
        FramesOverTickCap++;
        warning = $"warning: Ignoring {_imagesThisTick + 1}th image (with t={f.Timestamp}) received during basestation tick at {t:F6}sec";
        return false;
    }

    // fidelity: M3-002
    /// <summary>
    /// <c>EncodedImage::AddChunk</c> 0x004F1CE0, step for step (M2 inventory App. C R1..R10).
    ///
    /// One image is collected at a time. A chunk whose image id differs from the one in hand starts a new image there
    /// and then (R2): the id is stored first, then the resolution is checked; a non-QVGA resolution returns with
    /// nothing else reset (R3). A new QVGA image is valid only if its first chunk is chunk 0, and its encoding is the
    /// chunk's own except that JPEGMinimizedGray with a non-zero first payload byte is JPEGMinimizedColor (A14).
    /// Every accepted chunk must carry the id the image expects, or the image is invalidated (R4..R6). The last
    /// chunk is the one with <c>chunkCount - 1 == chunkId</c> in int arithmetic, so a count of 0 never completes
    /// (R7, R10); on it the u8 counter must equal the count, then the timestamps move (even when the check that
    /// follows fails) and a previous timestamp above the current one invalidates (R7). Data is appended only while
    /// the image is valid (R8). The constructor's id 0xFFFFFFFF means a first chunk carrying that id takes the
    /// same-image path with the image invalid (R10).
    /// </summary>
    private CameraFrame? AddLocked(ImageChunk c, List<(uint Id, string Why)> dropped)
    {
        ChunksReceived++;
        if (c.Data.Length > MaxChunkBytes)
        {
            // R1 ChunkTooBig: the chunk is refused before any bookkeeping, which leaves the image expecting
            // this chunk id, so the next one is out of order and takes the image down with it.
            ChunksRejected++;
            dropped.Add((c.ImageId, $"chunk of {c.Data.Length} bytes, more than {MaxChunkBytes}"));
            return null;
        }

        if (c.ImageId != _imageId)
        {
            if (_valid && !_delivered && _chunksTaken > 0)
            {
                FramesDropped++;
                dropped.Add((_imageId, "superseded before all chunks arrived"));
            }
            _imageId = c.ImageId;
            if (c.ImageResolution != Resolution)
            {
                // R3: the new id is recorded and nothing else is touched
                dropped.Add((c.ImageId, $"resolution {c.ImageResolution}, and the engine only takes {Resolution}"));
                return null;
            }
            (_width, _height) = (EncodedImageDecoder.Columns, EncodedImageDecoder.Rows);
            _valid = c.ChunkId == 0;
            _delivered = false;
            _expectedChunk = 0;
            _chunksTaken = 0;
            _encoding = (byte)c.ImageEncoding;
            if (_encoding == MiniJpeg.EncodingJpegMinimizedGray && c.Data.Length > 0 && c.Data[0] != 0)
                _encoding = MiniJpeg.EncodingJpegMinimizedColor;
            _buffer.Clear();
        }

        if (c.ChunkId != _expectedChunk)
            Invalidate(dropped, $"chunk {c.ChunkId} arrived where chunk {_expectedChunk} was expected");
        _expectedChunk = (byte)(c.ChunkId + 1);
        _chunksTaken++;

        bool isLast = c.ImageChunkCount - 1 == c.ChunkId;     // R7, R10: int arithmetic, a count of 0 never completes
        if (isLast)
        {
            if (_chunksTaken != c.ImageChunkCount)
                Invalidate(dropped, $"expected {c.ImageChunkCount} chunks, received {_chunksTaken}");
            else
            {
                _previousTimestamp = _timestamp;
                _timestamp = c.FrameTimestamp;
                if (_previousTimestamp > _timestamp)
                    Invalidate(dropped, $"timestamp {_timestamp} is behind the previous {_previousTimestamp}");
            }
        }

        if (!_valid) return null;                          // invalidated: the data is not even kept
        _buffer.AddRange(c.Data);
        if (!isLast) return null;

        var payload = _buffer.ToArray();
        bool halfWidth = _encoding == MiniJpeg.EncodingJpegMinimizedColor;
        int jpegWidth = halfWidth ? _width / 2 : _width;
        var frame = new CameraFrame
        {
            ImageId = _imageId, Timestamp = _timestamp, Width = _width, Height = _height,
            Encoding = _encoding, Resolution = (byte)c.ImageResolution,
            IsColor = EncodedImageDecoder.IsColor(_encoding), JpegWidth = jpegWidth,
            StreamMarker = payload.Length > 1 ? payload[1] : (byte)0,
            FrameIndex = FrameIndex, IsWarmUp = FrameIndex < WarmUpFrames,
            ChunkCount = c.ImageChunkCount, RawPayload = payload,
            Jpeg = MiniJpeg.ToJpeg(payload, jpegWidth, _height, _encoding),
        };
        if (_imu.Remove(_imageId, out var g)) frame.GyroRates = g;
        _delivered = true;
        FrameIndex++;
        LastFrame = frame;
        FramesCompleted++;
        return frame;
    }

    /// <summary>Takes the image out of play, once, the way each of the engine's warnings clears +0x22.</summary>
    private void Invalidate(List<(uint Id, string Why)> dropped, string why)
    {
        if (!_valid) return;
        _valid = false;
        FramesDropped++;
        dropped.Add((_imageId, why));
    }
}

/// <summary>The CurrentCameraParams the engine broadcasts to the game after SetCameraSettings (A20): {gain, exposure, auto-exposure}.</summary>
public readonly record struct CurrentCameraParams(float Gain, ushort ExposureMs, bool AutoExposureEnabled);

// fidelity: M3-019, M3-021, M3-022, M3-023
/// <summary>
/// The parts of the engine's VisionComponent and VisionSystem that the device layer owns: the camera's exposure and
/// gain limits and settings (M3-021), the colour flag (M3-023), and the connection-time steps (M3-019, M3-022) that
/// read the robot's calibration and enable vision. The vision gates, the mailbox and auto-exposure are M11's (MD5).
/// </summary>
public sealed class CameraSettings
{
    /// <summary>VisionSystem constructor (1a, A21; 0x006B002A..0x006B004A): max exposure 66 ms (+0x88).</summary>
    public const ushort ConstructorMaxExposureMs = 66;
    /// <summary>Min exposure 1 ms (+0x8C).</summary>
    public const ushort ConstructorMinExposureMs = 1;
    /// <summary>Min gain 0.1 (+0x90).</summary>
    public const float ConstructorMinGain = 0.1f;
    /// <summary>Max gain 4.0 (+0x94).</summary>
    public const float ConstructorMaxGain = 4.0f;
    /// <summary>Current exposure 16 ms (+0x98).</summary>
    public const ushort ConstructorExposureMs = 16;
    /// <summary>Current gain 2.0 (+0x9C).</summary>
    public const float ConstructorGain = 2.0f;
    /// <summary>The min gain SetCameraExposureParams is given on DefaultCameraParams (1k): 0.1f.</summary>
    public const float DefaultParamsMinGain = 0.1f;

    /// <summary>
    /// The initial exposure (.data 0x01051054, 1f): .data-initialised to 16, and overwritten by VisionComponent::Init
    /// from vision_config.json ImageQuality.InitialExposureTime_ms, which the shipped file sets to 16. This stack does
    /// not load vision_config.json, so it is the constant 16 (the .data value and the shipped file's).
    /// </summary>
    public const ushort InitialExposureTimeMs = 16;

    /// <summary>The NV calibration size check: MakeWordAligned(CameraCalibration::Size()) = 56 (A17, 1j).</summary>
    public const int CalibrationBytes = (Vision.CameraCalibration.WireSize + 3) & ~3;

    private readonly CozmoRobot _robot;
    private readonly Func<RobotMessage, bool> _send;
    private readonly object _gate = new();
    private Vision.NvCalibrationReader? _calibrationRead;

    internal CameraSettings(CozmoRobot robot, Func<RobotMessage, bool> send)
    {
        _robot = robot;
        _send = send;
    }

    /// <summary>VisionSystem +0x88.</summary>
    public int MaxExposureMs { get; private set; } = ConstructorMaxExposureMs;
    /// <summary>VisionSystem +0x8C.</summary>
    public int MinExposureMs { get; private set; } = ConstructorMinExposureMs;
    /// <summary>VisionSystem +0x90.</summary>
    public float MinGain { get; private set; } = ConstructorMinGain;
    /// <summary>VisionSystem +0x94.</summary>
    public float MaxGain { get; private set; } = ConstructorMaxGain;
    /// <summary>VisionSystem +0x98: written only by VisionSystem::Update from the pending params (1d, M11).</summary>
    public int CurrentExposureMs { get; private set; } = ConstructorExposureMs;
    /// <summary>VisionSystem +0x9C (1d, M11).</summary>
    public float CurrentGain { get; private set; } = ConstructorGain;
    /// <summary>
    /// VisionSystem +0xA0..+0xA8: the params SetNextCameraParams queued (1d). Applying them to the current exposure
    /// and gain is VisionSystem::Update's (M11) and is not done here.
    /// </summary>
    public (int ExposureMs, float Gain)? NextCameraParams { get; private set; }
    /// <summary>The gamma table the last DefaultCameraParams gave SetGammaTable (17 bytes; its use is M11's ImagingPipeline).</summary>
    public byte[] GammaTable { get; private set; } = Array.Empty<byte>();
    /// <summary>VisionComponent +0x329: auto-exposure, 1 from the constructor (1n: <c>strh #0x100</c> at +0x328).</summary>
    public bool AutoExposureEnabled { get; private set; } = true;
    /// <summary>
    /// VisionSystem::IsInitialized (+0x58, 1g): set when VisionComponent::Init has read vision_config.json. This stack
    /// has no config load that can fail, so it counts as initialised from construction.
    /// </summary>
    public bool VisionSystemInitialized => true;
    /// <summary>VisionComponent +0x32A: the colour flag (A24, 3a), 0 from the constructor (0x00650180).</summary>
    public bool ColorImagesEnabled { get; private set; }
    /// <summary>VisionComponent +0x48: 0 from the constructor (2a); set to 1 only by the NV calibration callback, on every path (1j, 2d).</summary>
    public bool VisionEnabled { get; private set; }
    /// <summary>The calibration the NV read installed (SetCameraCalibration, 1j), or null.</summary>
    public Vision.CameraCalibration? Calibration { get; private set; }

    /// <summary>The NV callback installed a calibration (1j success path).</summary>
    public event Action<Vision.CameraCalibration>? CalibrationInstalled;
    /// <summary>The NV callback set vision enabled (1j, all three paths).</summary>
    public event Action? VisionEnabledSet;
    /// <summary>The CurrentCameraParams broadcast to the game after a SetCameraSettings that sent (A20).</summary>
    public event Action<CurrentCameraParams>? CurrentCameraParamsChanged;
    /// <summary>The engine log lines of these steps.</summary>
    public event Action<string>? Log;

    // fidelity: M3-023
    /// <summary>
    /// VisionComponent::EnableColorImages (A24, 3a; 0x006582CC..0x00658314): stores the flag at +0x32A and sends
    /// EnableColorImages {enable}, reliable, not hot. It is never sent at connection. Nothing in vision reads the
    /// flag: decoding follows each image's encoding (3b).
    /// </summary>
    public void EnableColorImages(bool enable)
    {
        ColorImagesEnabled = enable;
        _send(new Protocol.EnableColorImages { Enable = enable });
    }

    // fidelity: M3-021
    /// <summary>
    /// VisionComponent::SetCameraSettings (A20, 1l): only when IsExposureValid (<c>min &lt;= e &lt;= max</c>, else the
    /// warning "Exposure %dms not in range") and IsGainValid (<c>minGain &lt;= g &lt;= maxGain</c>, NaN invalid), it
    /// sends SetCameraParams {f32 gain, u16 exposure, false} reliable, not hot, queues SetNextCameraParams(e, g) and
    /// broadcasts CurrentCameraParams {g, e, auto-exposure}. Returns whether it sent. VizManager::SendCameraInfo is
    /// the engine's visualiser and has no counterpart here.
    /// </summary>
    public bool SetCameraSettings(ushort exposureMs, float gain)
    {
        CurrentCameraParams info;
        lock (_gate)
        {
            if (!(MaxExposureMs >= exposureMs && MinExposureMs <= exposureMs))
            {
                Emit($"warning: VisionSystem.IsExposureValid: Exposure {exposureMs}ms not in range {MinExposureMs} to {MaxExposureMs}");
                return false;
            }
            if (!(gain >= MinGain && gain <= MaxGain)) return false;      // IsGainValid: vcmpe, NaN fails both
            _send(new SetCameraParams { Gain = gain, ExposureMs = exposureMs, AutoExposureEnabled = false });
            SetNextCameraParamsLocked(exposureMs, gain);
            info = new CurrentCameraParams(gain, exposureMs, AutoExposureEnabled);
        }
        CurrentCameraParamsChanged?.Invoke(info);
        return true;
    }

    /// <summary>VisionSystem::SetNextCameraParams (1d): queues the params, warning when some are already pending.</summary>
    private void SetNextCameraParamsLocked(int exposureMs, float gain)
    {
        if (NextCameraParams is not null) Emit("warning: VisionSystem.SetNextCameraParams.OverwritingPreviousParams");
        NextCameraParams = (exposureMs, gain);
    }

    // fidelity: M3-021
    /// <summary>
    /// VisionComponent::HandleDefaultCameraParams (A19, 1k; 0x00657CA0..0x00657D34), with no time-sync gate
    /// (0x00537114..0x0053712A). It needs VisionSystem::IsInitialized; the initial exposure (16) must lie in
    /// [msg+8, msg+0xA], otherwise BadInitialExposureTime and nothing more. Then SetCameraSettings(16, gain = f32
    /// msg+4) first, against the limits in force (the constructor's on the first message), and only after it
    /// SetCameraExposureParams(16, min msg+8, max msg+0xA, gain msg+4, minGain 0.1, maxGain f32 msg+0, gamma msg+0xC):
    /// max at +0x88, min at +0x8C (1 when 0 or less), the gains at +0x90/+0x94, SetGammaTable, then
    /// SetNextCameraParams(16, gain) (A22, 1c). Its SetFailed error path is unreachable (1m).
    /// </summary>
    internal void Handle(DefaultCameraParams m)
    {
        if (!VisionSystemInitialized) { Emit("error: VisionComponent.HandleDefaultCameraParams: VisionSystem not initialized"); return; }
        ushort min = m.Field2, max = m.Field3;
        if (!(min <= InitialExposureTimeMs && InitialExposureTimeMs <= max))
        {
            Emit($"error: VisionComponent.HandleDefaultCameraParams.BadInitialExposureTime: {InitialExposureTimeMs} not in [{min}, {max}]");
            return;
        }
        SetCameraSettings(InitialExposureTimeMs, m.Field1);
        lock (_gate)
        {
            MaxExposureMs = max;
            MinExposureMs = min <= 0 ? 1 : min;
            MinGain = DefaultParamsMinGain;
            MaxGain = m.Field0;
            GammaTable = (byte[])m.Field4.Clone();
            SetNextCameraParamsLocked(InitialExposureTimeMs, m.Field1);
        }
    }

    // fidelity: M3-019, M3-022
    /// <summary>
    /// VisionComponent's RobotConnectionResponse subscriber, for response 0 (1h, A17, A18; 0x006583BA..0x0065842C):
    /// it queues NVStorageComponent::Read(0x80000001, callback) and then sends SetCameraParams, reliable, not hot.
    /// Policy M3-019 (MD1): the engine's f32 @0 and u16 @4 are stale stack bytes (1i), so this stack sends 0.0 and 0;
    /// the bool @6 is 1, as in the engine. The read is this stack's NV path (<see cref="Vision.NvCalibrationReader"/>),
    /// whose wire exchange is the NV subsystem's: the engine only queues it here, so its request goes out after the
    /// SetCameraParams. It has no timeout: the callback runs when the robot answers.
    /// </summary>
    internal void OnRobotConnected()
    {
        Vision.NvCalibrationReader reader;
        lock (_gate)
        {
            _calibrationRead?.Dispose();
            reader = _calibrationRead = new Vision.NvCalibrationReader(_robot);
        }
        reader.Completed += (result, data) => OnCalibrationRead(reader, result, data);
        _send(new SetCameraParams { Gain = 0.0f, ExposureMs = 0, AutoExposureEnabled = true });
        reader.Request();
    }

    // fidelity: M3-022
    /// <summary>
    /// The NV callback (1j; 0x0065AB68): NVResult ≠ 0 logs "ReadCameraCalibration.Failed"; a size other than
    /// <see cref="CalibrationBytes"/> logs "SizeMismatch"; otherwise it unpacks, logs "…Recvd" and installs the
    /// calibration (SetCameraCalibration, which starts processing). All three paths then set vision enabled
    /// (+0x48 = 1, 0x0065AE7E/0x0065AE80).
    /// MISSING: when robot+0x24 &lt;= 6 the engine zeroes the distortion coefficients ("IgnoringDistCoeffs"); what
    /// robot+0x24 holds is not established (gap pass open question 4), so the coefficients are installed as read.
    /// </summary>
    private void OnCalibrationRead(Vision.NvCalibrationReader reader, sbyte result, byte[] data)
    {
        Vision.CameraCalibration? installed = null;
        lock (_gate)
        {
            if (!ReferenceEquals(_calibrationRead, reader)) return;       // a removal (or a newer read) replaced it
            _calibrationRead = null;
            if (result != 0) Emit($"warning: VisionComponent.ReadCameraCalibration.Failed: {result}");
            else if (data.Length != CalibrationBytes)
                Emit($"warning: VisionComponent.ReadCameraCalibration.SizeMismatch: {data.Length} bytes, expected {CalibrationBytes}");
            else
            {
                installed = Vision.CameraCalibration.Unpack(data);
                Emit($"info: VisionComponent.ReadCameraCalibration.Recvd: {installed}");
                Calibration = installed;
            }
            VisionEnabled = true;
        }
        reader.Dispose();
        if (installed is not null) CalibrationInstalled?.Invoke(installed);
        VisionEnabledSet?.Invoke();
    }

    private void Emit(string line) => Log?.Invoke(line);

    // fidelity: M1-025, M1-015
    /// <summary>The as-constructed state, for a removed robot (the VisionComponent and VisionSystem are built afresh). Subscribers are kept.</summary>
    internal void ResetToConstructed()
    {
        Vision.NvCalibrationReader? pending;
        lock (_gate)
        {
            pending = _calibrationRead;
            _calibrationRead = null;
            MaxExposureMs = ConstructorMaxExposureMs;
            MinExposureMs = ConstructorMinExposureMs;
            MinGain = ConstructorMinGain;
            MaxGain = ConstructorMaxGain;
            CurrentExposureMs = ConstructorExposureMs;
            CurrentGain = ConstructorGain;
            NextCameraParams = null;
            GammaTable = Array.Empty<byte>();
            AutoExposureEnabled = true;
            ColorImagesEnabled = false;
            VisionEnabled = false;
            Calibration = null;
        }
        pending?.Dispose();
    }
}
