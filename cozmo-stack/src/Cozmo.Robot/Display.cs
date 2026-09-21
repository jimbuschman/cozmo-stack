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
    /// <summary>
    /// Decodes a payload back into an image. Used to round-trip-test the encoder.
    ///
    /// A payload of exactly <see cref="RawFrameSize"/> bytes is the raw column-mask buffer, not an RLE
    /// stream: <c>CompressRLE</c> emits its RLE only while it is under that size (<c>size >> 10</c> zero
    /// at 0x00581B7E), so nothing else can be exactly 1024 bytes long and the length is the only
    /// discriminator either side has.
    /// </summary>
    public static FaceBitmap Decode(ReadOnlySpan<byte> buffer)
    {
        var img = new FaceBitmap();
        if (buffer.Length == RawFrameSize)
        {
            for (int col = 0; col < FaceBitmap.Width; col++)
                for (int row = 0; row < FaceBitmap.Height; row++)
                    if ((buffer[col * 8 + row / 8] & (1 << (row % 8))) != 0) img[col, row] = 1;
            return img;
        }
        int x = 0, y = 0;
        // Whether the column the runs were filling is finished: a run that reached the bottom advanced x
        // itself, and nothing else has. Both the skip and the repeat command have to move past an
        // unfinished column, which is what makes the encoder's dropped trailing blank run work - and
        // dropping it is exactly what CompressRLE does (0x00581ABC). PyCozmo's transcription kept two
        // flags here and had the skip command consult the wrong one; the 28 sequences Cozmo itself
        // produced decode identically either way, and a column that ends on a drawn run followed by an
        // empty column is the case that tells them apart.
        bool columnFinished = true;
        foreach (var b in buffer)
        {
            int cmd = (b & 0xC0) >> 6, cnt = b & 0x3F;
            switch (cmd)
            {
                case 0:
                    cnt += 1;
                    if (!columnFinished) x++;
                    x += cnt; y = 0; columnFinished = true;
                    break;
                case 1:
                    cnt += 1;
                    if (!columnFinished) x++;
                    for (int i = 0; i < cnt; i++)
                    {
                        for (int row = 0; row < FaceBitmap.Height; row++)
                            if (x < FaceBitmap.Width && x > 0) img[x, row] = img[x - 1, row];
                        x++;
                    }
                    y = 0; columnFinished = true;
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
                    if (y > FaceBitmap.Height - 1) { columnFinished = true; x++; y -= FaceBitmap.Height; }
                    else columnFinished = false;
                    break;
            }
            if (x >= FaceBitmap.Width) break;
        }
        return img;
    }

    /// <summary>
    /// Encodes an image into the robot's format, as <c>FaceAnimationManager::CompressRLE</c> 0x00581904
    /// does.
    ///
    /// The engine builds one 64-bit mask per column of its 128 x 64 canvas, bit <c>r</c> for canvas row
    /// <c>r</c> (the <c>^ 0x3F</c> at 0x005819A0 is part of computing the two shift amounts, not a flip:
    /// row 0 lands on bit 0 and row 63 on bit 63). Then, column by column:
    ///
    /// <list type="bullet">
    /// <item><b>An empty column</b> (0x00581B0A) counts the consecutive empty columns that follow, capped
    /// so the run ends by column 127 and its count fits six bits, and emits the count alone - command
    /// 00, whose decoder adds one.</item>
    /// <item><b>A column equal to the one before it</b> (0x00581A0A) counts the following identical
    /// columns the same way and emits <c>0x40 | (count - 1)</c> - command 01 (0x00581B54, where the
    /// <c>adds r0, #0xff</c> is the minus one).</item>
    /// <item><b>Otherwise</b> it walks the mask two bits at a time - one robot pixel per pair of canvas
    /// rows - and runs of equal pairs become <c>0x7C + 4 * length</c> or'd with the pair and with 0x80
    /// (0x00581A6C). That arithmetic is the two run commands: length 1..16 gives 0x80 | (length-1) &lt;&lt; 2,
    /// and length 17..32 gives 0xC0 | (length-17) &lt;&lt; 2, both with the pair in the low two bits.</item>
    /// <item><b>A trailing blank run is dropped</b> unless the next column is both non-empty and
    /// different from this one (0x00581ABC..0x00581AE2). That is what lets the following skip or repeat
    /// command do the column advance, which is exactly what the decoder's "last draw" bookkeeping
    /// expects.</item>
    /// <item><b>Above 1024 bytes the whole thing is thrown away</b> and the raw 1024-byte mask buffer is
    /// sent instead (0x00581B76): <c>size >> 10</c> non-zero, then a resize to 0x400 and a byte copy.</item>
    /// </list>
    ///
    /// Two things about the pair bits are worth stating. The engine blanks alternate canvas rows before
    /// compressing, so only one of a pair is ever set and which one alternates with
    /// <c>_firstScanLine</c>; this encoder always uses the low bit. The robot's decoder as PyCozmo
    /// recovered it lights the pixel for either bit, which is the reading this relies on; whether the
    /// firmware also uses the bit position to choose a physical OLED row is not established.
    /// </summary>
    public static byte[] Encode(FaceBitmap image)
    {
        // one mask per column, bit r for row r
        var mask = new uint[FaceBitmap.Width];
        for (int x = 0; x < FaceBitmap.Width; x++)
        {
            uint m = 0;
            for (int y = 0; y < FaceBitmap.Height; y++) if (image[x, y] != 0) m |= 1u << y;
            mask[x] = m;
        }

        var outBuf = new List<byte>(256);
        for (int x = 0; x < FaceBitmap.Width; )
        {
            if (mask[x] == 0)
            {
                int more = 0;
                while (x + more <= MaxColumnRun && more <= MaxRunCount && x + more + 1 < FaceBitmap.Width
                       && mask[x + more + 1] == 0) more++;
                outBuf.Add((byte)more);                       // command 00: skip more + 1 columns
                x += more + 1;
                continue;
            }
            if (x > 0 && mask[x] == mask[x - 1])
            {
                int more = 0;
                while (x + more <= MaxColumnRun && more <= MaxRunCount && x + more + 1 < FaceBitmap.Width
                       && mask[x + more + 1] == mask[x]) more++;
                outBuf.Add((byte)(0x40 | (more & 0x3F)));     // command 01: repeat more + 1 columns
                x += more + 1;
                continue;
            }

            uint bits = mask[x];
            int value = -1, run = 0;
            for (int pair = 0; pair < FaceBitmap.Height; pair++)
            {
                int v = (int)(bits & 1);                      // one robot row is one canvas pair
                bits >>= 1;
                if (v == value) { run++; continue; }
                if (run >= 1) outBuf.Add(RunByte(run, value));
                run = 1;
                value = v;
            }
            // the last run: a blank one is dropped unless the next column is non-empty and different
            bool nextDiffers = x + 1 < FaceBitmap.Width && mask[x + 1] != 0 && mask[x + 1] != mask[x];
            if (value != 0 || x + 1 >= FaceBitmap.Width || nextDiffers) outBuf.Add(RunByte(run, value));
            x++;
        }

        if (outBuf.Count >= RawFrameSize)
        {
            // the engine gives up on the RLE and sends the mask buffer itself
            var raw = new byte[RawFrameSize];
            for (int x = 0; x < FaceBitmap.Width; x++)
                for (int b = 0; b < 8; b++)
                    raw[x * 8 + b] = b < 4 ? (byte)(mask[x] >> (8 * b)) : (byte)0;
            return raw;
        }
        return outBuf.ToArray();
    }

    /// <summary>0x7C + 4 * length, or'd with the pair and with 0x80 (0x00581A6C).</summary>
    private static byte RunByte(int length, int value) => (byte)((0x7C + (length << 2)) | value | 0x80);

    /// <summary>The highest column a skip or repeat run may reach: the <c>cmp r2, #0x7e</c> bound.</summary>
    public const int MaxColumnRun = 0x7E;

    /// <summary>The highest count a skip or repeat may carry: the <c>cmp r3, #0x3e</c> bound.</summary>
    public const int MaxRunCount = 0x3E;

    /// <summary>1024 bytes: the size at which the engine sends the raw mask buffer instead (0x00581B76).</summary>
    public const int RawFrameSize = 0x400;
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

    /// <summary>Bytes a face message costs on top of its payload: the CLAD tag and the 16-bit array count.</summary>
    public const int MessageOverhead = 3;

    /// <summary>
    /// Largest encoded face that fits one reliable-layer frame, from
    /// <see cref="TransportOptions.MaxFramePayloadBytes"/> less <see cref="MessageOverhead"/>.
    ///
    /// Anything larger would have to be split across a multipart message. The robot's multipart receive path
    /// has never been exercised in either direction, so this refuses to send rather than depend on it.
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
}
