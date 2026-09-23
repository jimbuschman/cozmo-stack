using Cozmo.Protocol;
using StbImageSharp;
using StbImageWriteSharp;

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
    /// <summary>Robot timestamp for the frame, as reported in the chunks (observed as 0 on firmware 2457).</summary>
    public uint Timestamp { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    /// <summary>Official Anki.Cozmo.ImageEncoding value the robot used (8 = JPEGMinimizedGray).</summary>
    public byte Encoding { get; init; }
    public byte Resolution { get; init; }
    /// <summary>
    /// True when the robot flagged the frame as colour in the first payload byte. Colour frames are encoded
    /// at half the resolution's width and have to be stretched back to <see cref="Width"/> when displayed.
    /// </summary>
    public bool IsColor { get; init; }
    /// <summary>The second payload byte. Not entropy data; its meaning is not established.</summary>
    public byte StreamMarker { get; init; }
    /// <summary>
    /// True if this frame arrived while the sensor was still settling, when the picture is torn and rolls
    /// by 8 pixels per frame. Such frames are not usable images. See <see cref="CozmoCamera.WarmUpFrames"/>.
    /// </summary>
    public bool IsWarmUp { get; init; }
    /// <summary>Position of this frame in the stream, counting from the request that started it.</summary>
    public int FrameIndex { get; init; }
    /// <summary>Width the JPEG itself carries: half of <see cref="Width"/> for a colour frame.</summary>
    public int JpegWidth { get; init; }
    public int ChunkCount { get; init; }
    /// <summary>A standalone, decodable JPEG file: header restored and byte stuffing reinserted.</summary>
    public byte[] Jpeg { get; init; } = Array.Empty<byte>();
    /// <summary>The bytes exactly as the robot sent them, before header reconstruction.</summary>
    public byte[] RawPayload { get; init; } = Array.Empty<byte>();
    /// <summary>Gyro rates sampled with this frame, if the matching ImageImuData arrived.</summary>
    public (float X, float Y, float Z)? GyroRates { get; internal set; }
    public DateTime ReceivedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Returns presentation geometry. The encoded colour source remains available in <see cref="Jpeg"/>
    /// at <see cref="JpegWidth"/>; display/save expands it to the nominal resolution width, as the app-side
    /// image pipeline does after decoding a half-width colour frame.
    /// </summary>
    public byte[] PresentationJpeg()
    {
        if (!IsColor || JpegWidth == Width) return Jpeg;
        var decoded = ImageResult.FromMemory(Jpeg, StbImageSharp.ColorComponents.RedGreenBlue);
        if (decoded.Width != JpegWidth || decoded.Height != Height)
            throw new InvalidDataException($"decoded JPEG is {decoded.Width}x{decoded.Height}, expected encoded geometry {JpegWidth}x{Height}");
        var expanded = new byte[Width * Height * 3];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int source = (y * decoded.Width + x * decoded.Width / Width) * 3;
                int target = (y * Width + x) * 3;
                expanded[target] = decoded.Data[source];
                expanded[target + 1] = decoded.Data[source + 1];
                expanded[target + 2] = decoded.Data[source + 2];
            }
        using var output = new MemoryStream();
        new ImageWriter().WriteJpg(expanded, Width, Height, StbImageWriteSharp.ColorComponents.RedGreenBlue, output, 90);
        return output.ToArray();
    }

    /// <summary>Saves presentation geometry; use <see cref="Jpeg"/> when the encoded source representation is required.</summary>
    public void Save(string path) => File.WriteAllBytes(path, PresentationJpeg());
    public override string ToString() =>
        $"CameraFrame #{ImageId} {Width}x{Height} {(IsColor ? "colour" : "gray")} chunks={ChunkCount} " +
        $"jpeg={Jpeg.Length}B{(IsWarmUp ? " (warm-up, torn)" : "")}";
}

/// <summary>
/// Reassembles <see cref="ImageChunk"/> messages into complete frames and rebuilds a decodable JPEG.
///
/// The robot sends "minimized" JPEG (official ImageEncoding 8 = JPEGMinimizedGray, 9 = JPEGMinimizedColor):
/// the entire JFIF header is stripped, and the 0x00 that JPEG requires after every 0xFF in entropy-coded
/// data is removed. The first payload byte is not entropy data at all: it is a flag saying whether the
/// frame is colour. Restoring a frame therefore means prepending the fixed header the encoder used,
/// patching the height/width into it, dropping that first byte, re-inserting a 0x00 after each 0xFF and
/// appending the end-of-image marker.
///
/// Verified against 28 frames captured from a firmware-2457 robot on 2026-09-18: the reconstruction
/// decodes as a 320x240 grayscale baseline JPEG of exactly 1200 MCUs.
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

    /// <summary>
    /// How many finished images the engine will hand to vision within one basestation tick.
    /// <c>HandleImageChunk</c> 0x00535A64 compares the event time with the last one it saw (the double at
    /// +0x130); a new tick zeroes the counter at +0x128, and within one tick the counter is incremented and
    /// an image whose count reaches 3 is dropped with "Ignoring %dth image (with t=%u) received during
    /// basestation tick at %fsec". At the 15 frames a second this camera produces against a 30 Hz tick, a
    /// second image inside one tick never happens, so nothing here counts them.
    /// </summary>
    public const int MaxImagesPerTick = 3;

    private readonly Dictionary<uint, (float, float, float)> _imu = new();
    /// <summary>Reassembly runs on the transport's dispatch thread while callers may restart it.</summary>
    private readonly object _gate = new();

    // The engine's EncodedImage, field for field: the image id it is collecting (+0x1C), whether that image
    // is still valid (+0x22), the chunk id it expects next (+0x21), how many chunks it has taken (+0x23),
    // the encoding it settled on (+0x20), and the current and previous timestamps (+0xC and +0x10).
    private readonly List<byte> _buffer = new();
    private bool _started;
    private uint _imageId;
    private bool _valid;
    /// <summary>Whether the image in hand was already handed out, so it is finished rather than abandoned.</summary>
    private bool _delivered;
    private byte _expectedChunk;
    private byte _chunksTaken;
    private byte _encoding;
    private uint _timestamp, _previousTimestamp;
    private int _width, _height;

    /// <summary>Raised once per complete frame.</summary>
    public event Action<CameraFrame>? FrameReceived;
    /// <summary>Raised when a frame had to be abandoned (missing or out-of-order chunks).</summary>
    public event Action<uint, string>? FrameDropped;

    /// <summary>
    /// Frames to flag as warm-up after the camera is started. The sensor takes about eleven frames to lock:
    /// until then every picture is torn and rolls by exactly one macroblock row per frame, and after it
    /// every picture is clean. Measured on the firmware-2457 capture of 2026-09-18 and reproduced on a live
    /// run. The engine has no such idea - it hands every image it reassembles to
    /// <c>VisionComponent::SetNextImage</c> - so these frames are flagged and still delivered; only
    /// <c>CozmoRobot.CaptureFrameAsync</c> waits past them, at its caller's option.
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

    /// <summary>Call when the camera is (re)started, so the warm-up count begins again.</summary>
    public void Restart()
    {
        lock (_gate)
        {
            _imu.Clear();
            _buffer.Clear();
            _started = false;
            _valid = false;
            _delivered = false;
            _expectedChunk = 0;
            _chunksTaken = 0;
            _timestamp = _previousTimestamp = 0;
            FrameIndex = 0;
        }
    }

    /// <summary>Feed every robot message here; the camera ignores the ones it does not care about.</summary>
    public void Handle(RobotMessage m)
    {
        switch (m)
        {
            case ImageChunk c: Add(c); break;
            case ImageImuData d: lock (_gate) _imu[d.ImageId] = (d.RateX, d.RateY, d.RateZ); break;
        }
    }

    private void Add(ImageChunk c)
    {
        CameraFrame? completed = null;
        var dropped = new List<(uint Id, string Why)>();
        lock (_gate) completed = AddLocked(c, dropped);
        foreach (var d in dropped) FrameDropped?.Invoke(d.Id, d.Why);
        if (completed is not null) FrameReceived?.Invoke(completed);
    }

    /// <summary>
    /// <c>EncodedImage::AddChunk</c> 0x004F1CE0, step for step.
    ///
    /// One image is collected at a time. A chunk whose image id differs from the one in hand starts a new
    /// image there and then, so a frame that never finished is simply gone; there is no queue of partial
    /// images to age out. The new image is valid only if its first chunk is chunk 0 (0x004F1D68), and its
    /// encoding is the chunk's own except that JPEGMinimizedGray with a non-zero first payload byte is
    /// really JPEGMinimizedColor (0x004F1D8C).
    ///
    /// After that every chunk must carry the id the image expects, or it warns "ChunkOutOfOrder" and the
    /// image is invalidated (0x004F1DB2). The last chunk is the one whose id is the chunk count minus one,
    /// and on it the engine checks that it took exactly that many chunks ("UnexpectedNumberOfChunks") and
    /// that the timestamp did not go backwards ("TimestampNotIncreasing"). Data is appended only while the
    /// image is still valid, and the function reports a complete image only for a valid last chunk; a last
    /// chunk on an invalidated image is the "Received last chunk of invalidated image" note instead.
    /// </summary>
    private CameraFrame? AddLocked(ImageChunk c, List<(uint Id, string Why)> dropped)
    {
        ChunksReceived++;
        if (c.Data.Length > MaxChunkBytes)
        {
            // ChunkTooBig: the chunk is refused before any bookkeeping, which leaves the image expecting
            // this chunk id, so the next one is out of order and takes the image down with it.
            ChunksRejected++;
            dropped.Add((c.ImageId, $"chunk of {c.Data.Length} bytes, more than {MaxChunkBytes}"));
            return null;
        }

        if (!_started || c.ImageId != _imageId)
        {
            if (_started && _valid && !_delivered && _chunksTaken > 0)
            {
                FramesDropped++;
                dropped.Add((_imageId, "superseded before all chunks arrived"));
            }
            _started = true;
            _imageId = c.ImageId;
            if (c.ImageResolution != Resolution)
            {
                // The engine records the new id and returns, without touching anything else: whatever it
                // was collecting stays in hand. Nothing this stack talks to sends another resolution.
                dropped.Add((c.ImageId, $"resolution {c.ImageResolution}, and the engine only takes {Resolution}"));
                return null;
            }
            (_width, _height) = (320, 240);
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

        bool isLast = c.ChunkId == (byte)(c.ImageChunkCount - 1);
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
        bool color = _encoding == MiniJpeg.EncodingJpegMinimizedColor;
        int jpegWidth = color ? _width / 2 : _width;
        var frame = new CameraFrame
        {
            ImageId = _imageId, Timestamp = _timestamp, Width = _width, Height = _height,
            Encoding = _encoding, Resolution = (byte)c.ImageResolution,
            IsColor = color, JpegWidth = jpegWidth,
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
