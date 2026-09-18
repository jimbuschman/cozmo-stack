using Cozmo.Protocol;

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

/// <summary>
/// The run-length format the robot's face accepts (payload of animFaceImage, tag 0x97).
///
/// Encoding is column-major over the 128x32 image. Each byte is a 2-bit command and a 6-bit count:
///   00 nnnnnn  skip n+1 whole columns
///   01 nnnnnn  repeat the previous column n+1 times
///   10 ccccdd  run of (cccc)+1 pixels down the current column; dd non-zero means draw, zero means skip
///   11 ccccdd  the same but with 16 added to the length, so runs of 17..32
/// </summary>
public static class FaceBitmapCodec
{
    /// <summary>Decodes a payload back into an image. Used to round-trip-test the encoder.</summary>
    public static FaceBitmap Decode(ReadOnlySpan<byte> buffer)
    {
        var img = new FaceBitmap();
        int x = 0, y = 0;
        bool lastDraw = false, repeatShift = false;
        foreach (var b in buffer)
        {
            int cmd = (b & 0xC0) >> 6, cnt = b & 0x3F;
            switch (cmd)
            {
                case 0:
                    cnt += 1;
                    if (lastDraw) x++;
                    x += cnt; y = 0; lastDraw = false; repeatShift = false;
                    break;
                case 1:
                    cnt += 1;
                    if (!repeatShift) x++;
                    for (int i = 0; i < cnt; i++)
                    {
                        for (int row = 0; row < FaceBitmap.Height; row++)
                            if (x < FaceBitmap.Width && x > 0) img[x, row] = img[x - 1, row];
                        x++;
                    }
                    y = 0; lastDraw = false; repeatShift = true;
                    break;
                default:
                    bool draw = (cnt & 1) != 0;
                    cnt >>= 1;
                    draw |= (cnt & 1) != 0;
                    cnt >>= 1;
                    cnt += 1;
                    if (cmd == 3) cnt += 16;
                    if (draw)
                        for (int i = 0; i < cnt; i++) { if (y < FaceBitmap.Height) img[x, y] = 1; y++; }
                    else y += cnt;
                    if (y > FaceBitmap.Height - 1) { repeatShift = true; x++; y -= FaceBitmap.Height; }
                    else repeatShift = false;
                    lastDraw = cmd == 2;
                    break;
            }
            if (x >= FaceBitmap.Width) break;
        }
        return img;
    }

    /// <summary>
    /// Encodes an image into the robot's format.
    ///
    /// Only the two run commands are used, and every column emits runs summing to exactly 32 rows, so the
    /// decoder advances to the next column on its own. The skip-column and repeat-column commands are
    /// deliberately avoided: their interaction with the decoder's "last draw" and "repeat shift" state is
    /// position-dependent, and the saving (a blank image is 128 bytes instead of 2) is irrelevant next to
    /// the 1420-byte message limit. The result is exact for every possible image.
    /// </summary>
    public static byte[] Encode(FaceBitmap image)
    {
        var outBuf = new List<byte>(256);
        for (int x = 0; x < FaceBitmap.Width; x++)
        {
            int y = 0;
            while (y < FaceBitmap.Height)
            {
                byte color = image[x, y];
                int run = 1;
                while (y + run < FaceBitmap.Height && image[x, y + run] == color) run++;
                y += run;
                bool draw = color != 0;
                bool endsColumn = y == FaceBitmap.Height;
                // Cozmo's own encoder sets both draw bits on the run that finishes a column; the decoder
                // treats either bit as "draw", so this only keeps our output shaped like the robot's.
                int bits = draw ? (endsColumn ? 0x03 : 0x01) : 0x00;
                outBuf.Add(run <= 16
                    ? (byte)(0x80 | ((run - 1) << 2) | bits)
                    : (byte)(0xC0 | ((run - 17) << 2) | bits));
            }
        }
        return outBuf.ToArray();
    }
}

/// <summary>
/// Cozmo's face display.
///
/// The engine streams face images as animation keyframes at the animation tick (about 30 Hz); the robot
/// shows the most recent image until a new one arrives, so a still picture only needs to be re-sent to
/// keep it alive. <see cref="MinInterval"/> is the engine's frame spacing and this class will not send
/// faster than that.
/// </summary>
public sealed class CozmoDisplay
{
    private readonly Action<RobotMessage> _send;
    private DateTime _last = DateTime.MinValue;

    /// <summary>33.3 ms: one animation frame, matching the engine's streaming rate.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(33.3);

    /// <summary>
    /// Largest encoded face the transport can carry in one message: the engine's 1420-byte limit less the
    /// reliable header, the message tag and the 16-bit array count.
    /// </summary>
    public const int MaxPayload = 1420 - 14 - 1 - 2;

    public int FramesSent { get; private set; }
    public byte[]? LastPayload { get; private set; }

    /// <summary>
    /// Invoked immediately before each face frame goes out. The engine pairs every face keyframe with an
    /// audio frame on the same animation tick, so <see cref="CozmoRobot"/> uses this to send silence when
    /// nothing is playing. Leave it null to send face frames on their own.
    /// </summary>
    public Action? BeforeFrame { get; set; }

    public CozmoDisplay(Action<RobotMessage> send) => _send = send;

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
                $"this face is too complex to send: it encodes to {payload.Length} bytes and one message holds {MaxPayload}",
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
}
