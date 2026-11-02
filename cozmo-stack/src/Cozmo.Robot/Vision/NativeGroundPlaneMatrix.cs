namespace Cozmo.Robot.Vision;

// fidelity: M11-017, M11-033
// Source helpers for the ground-plane lookup carrier (0x004F83F8).
internal static class NativeGroundPlaneMatrix
{
    internal static float[] Multiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != 9 || right.Length != 9)
            throw new ArgumentException("Ground-plane matrices require nine elements.");
        var result = new float[9];
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 3; col++)
        {
            float value = 0f;
            for (int k = 0; k < 3; k++)
                value += left[row * 3 + k] * right[k * 3 + col];
            result[row * 3 + col] = value;
        }
        return result;
    }
}

// The native lookup keeps old keys on calibration updates and copies the selected value.
internal sealed class NativeGroundPlaneLookup
{
    private readonly SortedDictionary<float, float[]> _entries = new();

    internal NativeGroundPlaneLookup Copy()
    {
        var result = new NativeGroundPlaneLookup();
        foreach (var entry in _entries) result.Set(entry.Key, entry.Value);
        return result;
    }

    internal void Set(float angle, ReadOnlySpan<float> matrix)
    {
        if (matrix.Length != 9) throw new ArgumentException("Ground-plane matrices require nine elements.");
        _entries[angle] = matrix.ToArray();
    }

    internal bool TryGet(float head, Span<float> output)
    {
        if (output.Length < 9) throw new ArgumentException("Ground-plane output requires nine elements.");
        if (_entries.Count == 0 || _entries.Last().Key < head) return false;
        foreach (var entry in _entries)
        {
            if (entry.Key < head) continue;
            entry.Value.CopyTo(output);
            return true;
        }
        return false;
    }

    // 0x00651998: successful calibration changes overwrite reached keys, without clearing older keys.
    internal bool Populate(EngineRobot owner, CameraCalibration calibration)
    {
        float[] k = { (float)calibration.FocalLengthX,
            (float)calibration.Skew * (float)calibration.FocalLengthX,
            (float)calibration.CenterX, 0, (float)calibration.FocalLengthY,
            (float)calibration.CenterY, 0, 0, 1 };
        float angle = BitConverter.Int32BitsToSingle(unchecked((int)0xBEDF66F3));
        float step = BitConverter.Int32BitsToSingle(0x3B8EFA35);
        float limit = BitConverter.Int32BitsToSingle(0x3F46D3F2);
        using var state = new HistRobotState();
        state.Pose.Assign(owner.GetCheckedMainPose());
        while (angle <= limit)
        {
            state.State.HeadAngle = angle;
            using var camera = owner.CopyHistoricalCameraPose(state);
            using var relative = new Pose3d();
            // Native 0x00651A4E does not branch on this return value.
            Pose3d.GetWithRespectTo(state.Pose, camera, relative);
            var rotation = relative.Rotation;
            var t = relative.Translation;
            float[] m = { (float)rotation[0,0], (float)rotation[0,1], (float)t.X,
                (float)rotation[1,0], (float)rotation[1,1], (float)t.Y,
                (float)rotation[2,0], (float)rotation[2,1], (float)t.Z };
            float[] h = NativeGroundPlaneMatrix.Multiply(k, m);
            var quad = NativeGroundPlaneProjection.Project(h, calibration.Columns, calibration.Rows);
            static bool InFov(NativeGroundPoint p, CameraCalibration c) =>
                !float.IsNaN(p.X) && !float.IsNaN(p.Y) && p.X >= 0 && p.Y >= 0 &&
                p.X < c.Columns && p.Y < c.Rows;
            if (!InFov(quad[0], calibration) && !InFov(quad[1], calibration)) break;
            Set(angle, h);
            angle += step;
        }
        return true;
    }
}

internal readonly record struct NativeGroundPoint(float X, float Y);

internal static class NativeGroundPlaneProjection
{
    // 0x004F78E0: matrix × static homogeneous ground corners, with f32 reciprocal.
    internal static NativeGroundPoint[] Project(ReadOnlySpan<float> h, int columns, int rows)
    {
        if (h.Length != 9) throw new ArgumentException("Ground-plane matrices require nine elements.");
        (float X, float Y)[] corners = { (190, 75), (40, 20), (190, -75), (40, -20) };
        var result = new NativeGroundPoint[4];
        for (int i = 0; i < corners.Length; i++)
        {
            var (x, y) = corners[i];
            float a = h[0] * x; a += h[1] * y; a += h[2] * 1f;
            float b = h[3] * x; b += h[4] * y; b += h[5] * 1f;
            float c = h[6] * x; c += h[7] * y; c += h[8] * 1f;
            float reciprocal = 1f / c;
            result[i] = new NativeGroundPoint(a * reciprocal, b * reciprocal);
        }
        Clamp(result, columns, rows);
        return result;
    }
    private static bool Segment(NativeGroundPoint p, NativeGroundPoint pend, NativeGroundPoint q, NativeGroundPoint qend, out NativeGroundPoint result)
    {
        float dx = pend.X - p.X, dy = pend.Y - p.Y;
        float ex = qend.X - q.X, ey = qend.Y - q.Y;
        float ux = (p.X + dx) - p.X, uy = (p.Y + dy) - p.Y;
        float vx = (q.X + ex) - q.X, vy = (q.Y + ey) - q.Y;
        float det = ux * vy - uy * vx;
        result = default;
        if ((double)det > -0.0001 && (double)det < 0.0001) return false;
        float ax = p.X - q.X, ay = p.Y - q.Y;
        float t = (ay * vx - ax * vy) / det;
        float s = (ux * ay - uy * ax) / det;
        if (!(t >= 0 && t <= 1 && s >= 0 && s <= 1)) return false;
        float y = p.Y + uy * t, x = p.X + ux * t;
        result = new(x, y);
        return true;
    }

    // 0x004F7B74: the two near corners change only when both intersections exist.
    internal static void Clamp(NativeGroundPoint[] points, int columns, int rows)
    {
        // 0x004F7952..0x004F797C passes (0, rows-1) first, so the extended edge runs left to right.
        var a = new NativeGroundPoint(0, rows - 1);
        var b = new NativeGroundPoint(columns - 1, rows - 1);
        float dx = a.X - b.X, dy = a.Y - b.Y;
        var start = new NativeGroundPoint(a.X + dx, a.Y + dy);
        var end = new NativeGroundPoint(b.X - dx, b.Y - dy);
        bool first = Segment(start, end, points[1], points[0], out var one);
        bool second = Segment(start, end, points[3], points[2], out var three);
        if (first && second) { points[1] = one; points[3] = three; }
    }
}

