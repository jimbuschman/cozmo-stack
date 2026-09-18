using Cozmo.Protocol;

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

    public void Save(string path) => File.WriteAllBytes(path, Jpeg);
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
    private readonly Dictionary<uint, PartialImage> _pending = new();
    private readonly Dictionary<uint, (float, float, float)> _imu = new();

    /// <summary>Raised once per complete frame.</summary>
    public event Action<CameraFrame>? FrameReceived;
    /// <summary>Raised when a frame had to be abandoned (missing or out-of-order chunks).</summary>
    public event Action<uint, string>? FrameDropped;

    /// <summary>
    /// Frames to discard after the camera is started. The sensor takes about eleven frames to lock: until
    /// then every picture is torn and rolls by exactly one macroblock row per frame, and after it every
    /// picture is clean. Measured on the firmware-2457 capture of 2026-09-18 and reproduced on a live run.
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

    private sealed class PartialImage
    {
        public readonly SortedDictionary<byte, byte[]> Chunks = new();
        public uint Timestamp;
        public byte Encoding, Resolution;
        public int Expected = -1;
        public DateTime Started = DateTime.UtcNow;
    }

    /// <summary>Call when the camera is (re)started, so the warm-up count begins again.</summary>
    public void Restart()
    {
        _pending.Clear();
        _imu.Clear();
        FrameIndex = 0;
    }

    /// <summary>Feed every robot message here; the camera ignores the ones it does not care about.</summary>
    public void Handle(RobotMessage m)
    {
        switch (m)
        {
            case ImageChunk c: Add(c); break;
            case ImageImuData d: _imu[d.ImageId] = (d.RateX, d.RateY, d.RateZ); break;
        }
    }

    private void Add(ImageChunk c)
    {
        ChunksReceived++;
        if (!_pending.TryGetValue(c.ImageId, out var p))
        {
            // A new image starts; anything older than the newest two is never going to complete.
            foreach (var stale in _pending.Keys.Where(k => k + 2 < c.ImageId).ToList())
            {
                _pending.Remove(stale);
                FramesDropped++;
                FrameDropped?.Invoke(stale, "superseded before all chunks arrived");
            }
            p = new PartialImage { Timestamp = c.FrameTimestamp, Encoding = (byte)c.ImageEncoding, Resolution = (byte)c.ImageResolution };
            _pending[c.ImageId] = p;
        }
        p.Chunks[c.ChunkId] = c.Data;
        // The robot reports the total only in the final chunk (firmware 2457, confirmed in capture).
        if (c.ImageChunkCount > 0) p.Expected = c.ImageChunkCount;

        if (p.Expected > 0 && p.Chunks.Count >= p.Expected)
        {
            _pending.Remove(c.ImageId);
            if (p.Chunks.Keys.First() != 0 || p.Chunks.Keys.Last() != p.Expected - 1)
            {
                FramesDropped++;
                FrameDropped?.Invoke(c.ImageId, $"expected chunks 0..{p.Expected - 1}, got [{string.Join(",", p.Chunks.Keys)}]");
                return;
            }
            var payload = p.Chunks.Values.SelectMany(b => b).ToArray();
            var (w, h) = CameraResolutions.Size(p.Resolution);
            // Byte 0 flags colour; a colour frame is encoded at half width and stretched back on display.
            bool color = payload.Length > 0 && payload[0] != 0;
            int jpegWidth = color ? w / 2 : w;
            byte encoding = color ? MiniJpeg.EncodingJpegMinimizedColor : p.Encoding;
            var frame = new CameraFrame
            {
                ImageId = c.ImageId, Timestamp = p.Timestamp, Width = w, Height = h,
                Encoding = p.Encoding, Resolution = p.Resolution,
                IsColor = color, JpegWidth = jpegWidth,
                StreamMarker = payload.Length > 1 ? payload[1] : (byte)0,
                FrameIndex = FrameIndex, IsWarmUp = FrameIndex < WarmUpFrames,
                ChunkCount = p.Expected, RawPayload = payload,
                Jpeg = MiniJpeg.ToJpeg(payload, jpegWidth, h, encoding),
            };
            if (_imu.Remove(c.ImageId, out var g)) frame.GyroRates = g;
            FrameIndex++;
            LastFrame = frame;
            FramesCompleted++;
            FrameReceived?.Invoke(frame);
        }
    }
}
