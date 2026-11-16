using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Vision;

// fidelity: M11-035
internal static class NativeLaserPointDetector
{
    private static readonly float[] RingDirections =
    {
        1f, 0f, Diagonal, Diagonal, 0f, 1f, -Diagonal, Diagonal,
        -1f, 0f, -Diagonal, -Diagonal, 0f, -1f, Diagonal, -Diagonal
    };
    private const float Diagonal = 0.707099974155426f; // 0x3F350481, 0x006A81D6..0x006A81E4.

    // 0x006B3BB8..0x006B3C32 compares current camera settings (+0x98/+0x9C) with the configured minima (+0x8C/+0x90).
    internal static bool AtMinimumExposure(int exposure, float gain, int minExposure, float minGain) =>
        MathF.Abs((float)exposure - minExposure) < 1e-5f && MathF.Abs(gain - minGain) < 1e-5f;

    internal static NativeLaserObservation? Detect(NativeMotionImage half, OverheadEdgeGroundInput ground,
        int fullWidth, int fullHeight, bool minimumExposure, float headAngle, Action<string, string> warn)
    {
        // 0x006A7A20..0x006A7A2A returns success without producing a ground point when the ground is invisible.
        if (!ground.GroundPlaneVisible) return null;

        bool color = half.Channels == 3;
        byte[] gray = color ? ImageCache.FillGray(half.Pixels, half.Columns, half.Rows).Pixels : half.Pixels;
        int low = minimumExposure ? 128 : 235, high = minimumExposure ? 160 : 240;
        int count = checked(half.Rows * half.Columns);
        var mask = new byte[count];
        var peak = new byte[count];
        for (int i = 0; i < count; i++)
        {
            if (color)
            {
                int offset = i * 3;
                byte red = half.Pixels[offset], green = half.Pixels[offset + 1], blue = half.Pixels[offset + 2];
                // The RGB threshold's true arm at 0x00873162..0x0087318C accepts any channel above the threshold.
                peak[i] = Math.Max(red, Math.Max(green, blue));
            }
            else peak[i] = gray[i];
            if (peak[i] > low) mask[i] = 255;
        }

        var components = new List<NativeMotionComponent>();
        NativeMotionComponents.TryAppend(new NativeMotionImage(half.Rows, half.Columns, 1, half.Columns, mask, half.Timestamp),
            components, out var labels); // 0x006A7222, shared Image::GetConnectedComponents.
        var hasPeak = new bool[components.Count];
        for (int i = 0; i < count; i++)
            if (peak[i] > high) hasPeak[labels[i]] = true; // 0x006A72FA..0x006A7332 / 0x006A747E..0x006A74D2.

        uint maxArea = (uint)(count * 0.02556634694337845f), minArea = (uint)(count * 0.0001636246161069721f);
        var h = ground.Homography;
        var quad = NativeGroundPlaneProjection.Project(h, fullWidth, fullHeight);
        for (int i = 0; i < quad.Length; i++) quad[i] = new(quad[i].X * .5f, quad[i].Y * .5f);
        NativeMotionComponent best = default;
        // 0x006A7610..0x006A7622 filters foreground labels by peak and inclusive area.
        // 0x006A7E60..0x006A7EAA keeps the first acceptable component when areas tie.
        for (int label = 1; label < components.Count; label++)
        {
            var candidate = components[label];
            if (!hasPeak[label] || candidate.Area < minArea || candidate.Area > maxArea || candidate.Area <= best.Area) continue;
            if (!Contains(quad, candidate.CenterX, candidate.CenterY) ||
                !DarkSurround(gray, half.Columns, half.Rows, candidate, minimumExposure ? .7f : .9f) ||
                color && !Saturated(half, candidate)) continue;
            best = candidate;
        }
        if (best.Area == 0) return null;

        // 0x006A7B3A..0x006A7BC4 projects the doubled centroid by solving H[x,y,z] = [u,v,1].
        float x = best.CenterX + best.CenterX, y = best.CenterY + best.CenterY;
        Span<float> result = stackalloc float[3];
        if (!NativeMotionGroundMath.TrySolve(h, new float[] { x, y, 1f }, result))
        {
            warn("LaserPointDetector.Detect.LeastSquaresFailed",
                FormattableString.Invariant($"Failed to project laser centroid ({x:F1},{y:F1}) to ground plane"));
            return new(half.Timestamp, 0, 0, 0);
        }
        if (result[2] <= 0)
        {
            warn("LaserPointDetector.Detect.BadProjectedZ",
                FormattableString.Invariant($"z<=0 ({result[2]:F6}) when projecting laser centroid to ground. Bad homography at head angle {headAngle * 57.295780181884766f:F3}deg?"));
            return new(half.Timestamp, 0, 0, 0);
        }
        float inverseZ = 1f / result[2];
        float area = TriangleArea(quad[0], quad[1], quad[3]) + TriangleArea(quad[0], quad[2], quad[3]);
        return new(half.Timestamp, best.Area / area,
            unchecked((short)Round(inverseZ * result[0])), unchecked((short)Round(inverseZ * result[1])));
    }

    // 0x004DF764..0x004DF7B6 tests both triangles using the three binary32 barycentric coordinates.
    private static bool Contains(NativeGroundPoint[] p, float x, float y) =>
        InTriangle(p[0], p[1], p[3], x, y) || InTriangle(p[0], p[2], p[3], x, y);

    private static bool InTriangle(NativeGroundPoint a, NativeGroundPoint b, NativeGroundPoint c, float x, float y)
    {
        float dx = c.X - b.X, dy = b.Y - c.Y, ax = a.X - c.X, ay = a.Y - c.Y;
        float inverse = 1f / (ax * dy + dx * ay);
        float px = x - c.X, py = y - c.Y;
        float u = inverse * (dy * px + dx * py);
        float v = inverse * ((c.Y - a.Y) * px + ax * py);
        float w = (1f - u) - v;
        const float lower = -1.1920928955078125e-7f, upper = 1.0000001192092896f;
        return u > lower && u < upper && v > lower && v < upper && w > lower && w < upper;
    }

    private static float TriangleArea(NativeGroundPoint a, NativeGroundPoint b, NativeGroundPoint c) =>
        MathF.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) * .5f;

    // 0x006A8170..0x006A82F0 uses eight radial probes, a fixed divisor of eight, and a variance test.
    private static bool DarkSurround(byte[] gray, int width, int height, NativeMotionComponent component, float ratio)
    {
        float x = component.CenterX, y = component.CenterY;
        int maximum = Round(gray[Round(y) * width + Round(x)] * ratio);
        float radius = MathF.Sqrt(component.Area / MathF.PI) * 2.5f;
        int sum = 0, sumSquares = 0;
        for (int i = 0; i < RingDirections.Length; i += 2)
        {
            int px = Round(x + radius * RingDirections[i]), py = Round(y + radius * RingDirections[i + 1]);
            if ((uint)px >= (uint)width || (uint)py >= (uint)height) continue;
            int value = gray[py * width + px];
            if (value > maximum) return false;
            sum += value;
            sumSquares += value * value;
        }
        int mean = sum / 8;
        return sumSquares / 8 - mean * mean < 626;
    }

    // 0x006A834C..0x006A83C6 measures red and green dominance over the whole bounding rectangle.
    private static bool Saturated(NativeMotionImage image, NativeMotionComponent component)
    {
        int redSum = 0, greenSum = 0;
        for (int y = component.Top; y < component.Top + component.Height; y++)
        {
            for (int x = component.Left; x < component.Left + component.Width; x++)
            {
                int offset = y * image.Stride + x * 3;
                int red = image.Pixels[offset], green = image.Pixels[offset + 1], blue = image.Pixels[offset + 2];
                redSum += Math.Max(0, red - Math.Max(green, blue));
                greenSum += Math.Max(0, green - Math.Max(red, blue));
            }
        }
        int area = component.Width * component.Height;
        return (float)redSum / area > 30f || (float)greenSum / area > 15f;
    }

    private static int Round(float value) =>
        AnimationScheduler.VcvtS32(MathF.Round(value, MidpointRounding.AwayFromZero));
}
