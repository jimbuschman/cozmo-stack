using Cozmo.Protocol;
using Cozmo.Transport;

namespace Cozmo.Robot;

/// <summary>
/// A 128x32 one-bit image for Cozmo's OLED face.
///
/// The panel is physically 128x64, but the engine draws interlaced: only every other row is lit on a
/// given frame, so the usable image is 128x32 (PyCozmo documents the same, and the official animation
/// face keyframes are 128x32). Pixels are stored row-major, one byte per pixel (0 or 1) for easy editing.
/// </summary>
public sealed class FaceBitmap
{
    public const int Width = 128;
    public const int Height = 32;

    private readonly byte[] _px = new byte[Width * Height];

    public byte this[int x, int y]
    {
        get => (uint)x < Width && (uint)y < Height ? _px[y * Width + x] : (byte)0;
        set { if ((uint)x < Width && (uint)y < Height) _px[y * Width + x] = value; }
    }

    public ReadOnlySpan<byte> Pixels => _px;

    public void Clear() => Array.Clear(_px);

    public void Fill() => Array.Fill(_px, (byte)1);

    /// <summary>Builds an image from 32 lines of 128 characters; any character other than space or '.' is lit.</summary>
    public static FaceBitmap FromText(string art)
    {
        var img = new FaceBitmap();
        int y = 0;
        foreach (var line in art.Split('\n'))
        {
            var l = line.TrimEnd('\r');
            if (l.Length == 0) continue;
            for (int x = 0; x < Math.Min(Width, l.Length); x++)
                img[x, y] = (byte)(l[x] is ' ' or '.' ? 0 : 1);
            if (++y >= Height) break;
        }
        return img;
    }

    /// <summary>Renders as text, for tests and terminal preview.</summary>
    public string ToText(char on = '#', char off = '.')
    {
        var sb = new System.Text.StringBuilder((Width + 1) * Height);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++) sb.Append(this[x, y] != 0 ? on : off);
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public void DrawRect(int x0, int y0, int x1, int y1, bool filled = false)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (filled || x == x0 || x == x1 || y == y0 || y == y1) this[x, y] = 1;
    }

    public void DrawLine(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        while (true)
        {
            this[x0, y0] = 1;
            if (x0 == x1 && y0 == y1) break;
            int e2 = err * 2;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>A simple recognisable test pattern: border, diagonals and a centred filled block.</summary>
    public static FaceBitmap TestPattern()
    {
        var img = new FaceBitmap();
        img.DrawRect(0, 0, Width - 1, Height - 1);
        img.DrawLine(0, 0, Width - 1, Height - 1);
        img.DrawLine(Width - 1, 0, 0, Height - 1);
        img.DrawRect(56, 12, 71, 19, filled: true);
        return img;
    }
}

/// fidelity: M3-006, M3-007, M3-009
/// <summary>
/// The face wire format: <c>FaceAnimationManager::CompressRLE</c> 0x00581904 and the engine's reference decoder
/// <c>DrawFaceRLE</c> / <c>FaceDisplayDecode</c> (M3 inventory B6..B15).
///
/// The engine encodes a <b>64-row by 128-column canvas</b> (B1, B6). The wire image is 128 columns of 64 rows,
/// walked as 32 two-row pairs per column (B10, B15). Pair <c>k</c> of a column carries canvas row <c>2k</c> in
/// bit 0 and row <c>2k + 1</c> in bit 1 (B10). Opcodes, column by column (B8..B11):
/// <list type="bullet">
/// <item><c>0b00nnnnnn</c> skip: an empty column plus the <c>n</c> further empty columns
/// (<c>c + n &lt;= 127</c>, <c>n &lt;= 63</c>);</item>
/// <item><c>0x40 | k</c> repeat: a column equal to the previous one (<c>c &gt; 0</c>) plus <c>k</c> further
/// equal columns, with the same limits;</item>
/// <item><c>0x80 | ((len - 1) &lt;&lt; 2) | pair</c> run: <c>len</c> (1..32) equal pairs down the column.</item>
/// </list>
/// A column's trailing run is always emitted at <c>c == 127</c> or when its pair is non-zero; a trailing pair-0
/// run is dropped only when the next column is empty or equal to this one (B11). An RLE of <b>1024 bytes or
/// more</b> is replaced by the 128 little-endian u64 column masks, bit <c>r</c> = canvas row <c>r</c> (B12). A
/// blank canvas is <c>{0x3F, 0x3F}</c> (B13).
///
/// <see cref="FaceBitmap"/> (128 x 32) is this stack's picture type (MD3 for the raw-bitmap API; the
/// procedural and sprite drawers are M5's). <see cref="Encode(FaceBitmap)"/> puts its row <c>y</c> on canvas
/// row <c>2y</c>. Which row of a pair the engine lights is the M5 drawer's scan-line parity (B2..B4, MD5); this
/// stack's bitmap does not carry it, so the even row is kept, which is the wire bytes it has always sent.
/// </summary>
public static class FaceBitmapCodec
{
    /// <summary>The canvas the engine encodes: <c>Image(64 rows, 128 cols)</c> (B1, B6).</summary>
    public const int CanvasRows = 64, CanvasColumns = 128;

    /// <summary>Two-row pairs per column (B10, B15).</summary>
    public const int PairsPerColumn = CanvasRows / 2;

    /// <summary>1024: an RLE this long or longer is sent as the raw column masks instead (B12, <c>cmp.w r1,r0,lsr #10</c>).</summary>
    public const int RawFrameSize = 0x400;

    /// <summary>The highest column a skip or repeat may reach (B8, B9: <c>c + n &lt;= 127</c>).</summary>
    public const int LastColumn = CanvasColumns - 1;

    /// <summary>The largest further-column count a skip or repeat carries (B8, B9: <c>n &lt;= 63</c>).</summary>
    public const int MaxRunCount = 0x3F;

    /// <summary>
    /// <c>CompressRLE</c> over a canvas given row-major, one byte per pixel, any non-zero value lit (B7). A canvas
    /// that is not 64 x 128 is refused with null, so no face is sent for it (B6, B5).
    /// </summary>
    public static byte[]? EncodeCanvas(ReadOnlySpan<byte> pixels, int rows, int columns)
    {
        if (rows != CanvasRows || columns != CanvasColumns || pixels.Length != rows * columns) return null;   // B6

        // B7: one u64 mask per column, bit r set when pixel (r, c) is non-zero
        var mask = new ulong[CanvasColumns];
        for (int c = 0; c < CanvasColumns; c++)
        {
            ulong m = 0;
            for (int r = 0; r < CanvasRows; r++) if (pixels[r * CanvasColumns + c] != 0) m |= 1UL << r;
            mask[c] = m;
        }

        var o = new List<byte>(256);
        for (int c = 0; c < CanvasColumns; )
        {
            if (mask[c] == 0)
            {
                // B8: this empty column and n further empty ones, c + n <= 127 and n <= 63
                int n = 0;
                while (c + n + 1 <= LastColumn && n + 1 <= MaxRunCount && mask[c + n + 1] == 0) n++;
                o.Add((byte)n);
                c += n + 1;
                continue;
            }
            if (c > 0 && mask[c] == mask[c - 1])
            {
                // B9: this column, equal to the previous one, and k further equal ones, same limits
                int k = 0;
                while (c + k + 1 <= LastColumn && k + 1 <= MaxRunCount && mask[c + k + 1] == mask[c]) k++;
                o.Add((byte)(0x40 | k));
                c += k + 1;
                continue;
            }

            // B10: runs of equal pairs; pair p is bit 2p (row 2p) | bit 2p+1 (row 2p+1) << 1
            int value = (int)(mask[c] & 3), len = 1;
            for (int p = 1; p < PairsPerColumn; p++)
            {
                int v = (int)((mask[c] >> (2 * p)) & 3);
                if (v == value) { len++; continue; }
                o.Add(RunByte(len, value));
                value = v;
                len = 1;
            }
            // B11: the trailing run always goes at c == 127 or when its pair is non-zero; a pair-0 run is
            // dropped only when the next column is empty or equal to this one
            bool nextEmptyOrEqual = c < LastColumn && (mask[c + 1] == 0 || mask[c + 1] == mask[c]);
            if (c == LastColumn || value != 0 || !nextEmptyOrEqual) o.Add(RunByte(len, value));
            c++;
        }

        if (o.Count >= RawFrameSize)
        {
            // B12: resize to 1024 and copy the 128 little-endian u64 column masks
            var raw = new byte[RawFrameSize];
            for (int c = 0; c < CanvasColumns; c++)
                for (int b = 0; b < 8; b++) raw[c * 8 + b] = (byte)(mask[c] >> (8 * b));
            return raw;
        }
        return o.ToArray();
    }

    /// <summary>B10: <c>0x80 | ((len - 1) &lt;&lt; 2) | pair</c>, len 1..32.</summary>
    internal static byte RunByte(int len, int pair) => (byte)(0x80 | ((len - 1) << 2) | pair);

    /// <summary>
    /// Encodes this stack's 128 x 32 bitmap: its row <c>y</c> goes on canvas row <c>2y</c> (see the class
    /// summary), then <see cref="EncodeCanvas"/>.
    /// </summary>
    public static byte[] Encode(FaceBitmap image)
    {
        var canvas = new byte[CanvasRows * CanvasColumns];
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = 0; x < FaceBitmap.Width; x++)
                if (image[x, y] != 0) canvas[2 * y * CanvasColumns + x] = 1;
        return EncodeCanvas(canvas, CanvasRows, CanvasColumns)!;
    }

    /// <summary>
    /// The engine's reference decoder, into a 64 x 128 canvas given row-major (B14). A payload of exactly 1024
    /// bytes is the raw masks. Otherwise, per column, a skip or repeat byte covers its columns as B8/B9 say, and
    /// runs add <c>table[len - 1] * pair &lt;&lt; row</c> (the table 1, 5, 0x15, ... repeats the pair every two
    /// rows) and advance <c>row</c> by <c>2 * len</c> until it reaches 64; a non-run byte ends the column without
    /// being consumed. The firmware's own decoder is HARDWARE_ONLY (M3-008); this is the engine's model of it.
    /// </summary>
    public static byte[] DecodeCanvas(ReadOnlySpan<byte> buffer)
    {
        var mask = new ulong[CanvasColumns];
        if (buffer.Length == RawFrameSize)
        {
            for (int c = 0; c < CanvasColumns; c++)
                for (int b = 0; b < 8; b++) mask[c] |= (ulong)buffer[c * 8 + b] << (8 * b);
        }
        else
        {
            int i = 0, c = 0;
            while (c < CanvasColumns && i < buffer.Length)
            {
                byte b = buffer[i];
                int op = b >> 6;
                if (op == 0)
                {
                    i++;
                    c += (b & 0x3F) + 1;                                   // empty columns
                }
                else if (op == 1)
                {
                    i++;
                    ulong prev = c > 0 ? mask[c - 1] : 0;
                    for (int j = 0; j <= (b & 0x3F) && c < CanvasColumns; j++) mask[c++] = prev;
                }
                else
                {
                    ulong m = 0;
                    int row = 0;
                    while (row < CanvasRows && i < buffer.Length && (buffer[i] & 0x80) != 0)
                    {
                        int len = ((buffer[i] >> 2) & 0x1F) + 1, pair = buffer[i] & 3;
                        for (int j = 0; j < len && row < CanvasRows; j++, row += 2) m |= (ulong)pair << row;
                        i++;
                    }
                    mask[c++] = m;
                }
            }
        }
        var canvas = new byte[CanvasRows * CanvasColumns];
        for (int c = 0; c < CanvasColumns; c++)
            for (int r = 0; r < CanvasRows; r++)
                if (((mask[c] >> r) & 1) != 0) canvas[r * CanvasColumns + c] = 1;
        return canvas;
    }

    /// <summary>
    /// Decodes a payload into this stack's 128 x 32 bitmap: pixel <c>(x, y)</c> is lit when either row of pair
    /// <c>y</c> is (<see cref="DecodeCanvas"/>). Used to round-trip-test the encoder.
    /// </summary>
    public static FaceBitmap Decode(ReadOnlySpan<byte> buffer)
    {
        var canvas = DecodeCanvas(buffer);
        var img = new FaceBitmap();
        for (int y = 0; y < FaceBitmap.Height; y++)
            for (int x = 0; x < FaceBitmap.Width; x++)
                if (canvas[2 * y * CanvasColumns + x] != 0 || canvas[(2 * y + 1) * CanvasColumns + x] != 0) img[x, y] = 1;
        return img;
    }
}

/// <summary>
/// Cozmo's face display.
///
/// The engine streams face images as animation keyframes, one per 33 ms stream frame; the robot shows the most
/// recent image until a new one arrives, so a still picture only needs to be re-sent to keep it alive. This raw-bitmap
/// API has no engine counterpart (MD3): <see cref="MinInterval"/> is its own pacing, one frame of robot audio, and this
/// class will not send faster than that.
/// </summary>
public sealed class CozmoDisplay
{
    private readonly Action<RobotMessage> _send;
    private DateTime _last = DateTime.MinValue;

    /// <summary>
    /// 33.3 ms between raw frames: this stack's pacing for the raw-bitmap API (MD3), which has no engine counterpart. It
    /// is one frame of robot audio, 744 / 22320 s, so a long Hold does not outrun the robot.
    /// </summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(33.3);

    /// <summary>Bytes a face message costs on top of its payload: the CLAD tag and the 16-bit array count.</summary>
    public const int MessageOverhead = 3;

    /// <summary>
    /// Largest encoded face that fits one reliable-layer frame, from
    /// <see cref="TransportOptions.MaxFramePayloadBytes"/> less <see cref="MessageOverhead"/>.
    ///
    /// Anything larger would have to be split across a multipart message. The robot's multipart receive path
    /// has never been exercised in either direction, so this refuses to send rather than depend on it.
    ///
    /// The refusal cannot fire on anything the engine would send. A face payload is at most
    /// <see cref="FaceBitmapCodec.RawFrameSize"/>: <c>CompressRLE</c> gives up on its RLE at that size or above (B12)
    /// and sends the 1024-byte buffer instead, and the message the engine builds for it is a fixed 0x408
    /// bytes (<c>AnimationStreamer::BufferFaceToSend</c> 0x0057C2C2). 1024 plus the three bytes of
    /// overhead is well under the 1403 an engine-default frame carries. A face the engine could not encode
    /// at all is dropped at 0x0057C272 with "Failed to get RLE frame from procedural face" and nothing is
    /// sent in its place - which is also what happens here.
    /// </summary>
    public int MaxPayload { get; }

    /// <summary>The limit for a transport left on engine defaults.</summary>
    public static int DefaultMaxPayload => TransportOptions.EngineDefaults.MaxFramePayloadBytes - MessageOverhead;

    public int FramesSent { get; private set; }
    public byte[]? LastPayload { get; private set; }

    /// <summary>
    /// Invoked immediately before each face frame goes out. The engine pairs every face keyframe with an
    /// audio frame on the same animation tick, so <see cref="CozmoRobot"/> uses this to send silence when
    /// nothing is playing. Leave it null to send face frames on their own.
    /// </summary>
    public Action? BeforeFrame { get; set; }

    /// <param name="maxPayload">
    /// Largest encoded face to send in one message. Pass the owning transport's
    /// <see cref="TransportOptions.MaxFramePayloadBytes"/> less <see cref="MessageOverhead"/>.
    /// </param>
    public CozmoDisplay(Action<RobotMessage> send, int? maxPayload = null)
    {
        _send = send;
        MaxPayload = maxPayload ?? DefaultMaxPayload;
    }

    /// <summary>Sends one face image, honouring the minimum interval.</summary>
    public void Show(FaceBitmap image)
    {
        var payload = FaceBitmapCodec.Encode(image);
        SendRaw(payload);
    }

    /// <summary>Sends an already-encoded payload.</summary>
    public void SendRaw(byte[] payload)
    {
        if (payload.Length > MaxPayload)
            throw new ArgumentException(
                $"this face is too complex to send: it encodes to {payload.Length} bytes and one frame holds " +
                $"{MaxPayload}. Splitting it would need the robot's multipart path, which is unverified.",
                nameof(payload));
        var wait = MinInterval - (DateTime.UtcNow - _last);
        if (wait > TimeSpan.Zero) Thread.Sleep(wait);
        BeforeFrame?.Invoke();
        _send(new Protocol.FaceImage { Image = payload });
        LastPayload = payload;
        FramesSent++;
        _last = DateTime.UtcNow;
    }

    /// <summary>Holds one image on the face for a duration by re-sending it at the animation rate.</summary>
    public void Hold(FaceBitmap image, TimeSpan duration)
    {
        var payload = FaceBitmapCodec.Encode(image);
        var end = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < end) SendRaw(payload);
    }

    public void Clear() => Show(new FaceBitmap());

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to the state right after construction, for a removed robot (CB33, CC26, CC27): nothing sent, no last face.
    /// The payload limit and <see cref="BeforeFrame"/> are set by the owner and kept.
    /// </summary>
    internal void ResetToConstructed()
    {
        _last = DateTime.MinValue;
        FramesSent = 0;
        LastPayload = null;
    }
}
