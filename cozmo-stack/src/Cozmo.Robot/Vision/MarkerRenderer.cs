using Cozmo.Protocol;
namespace Cozmo.Robot.Vision;

/// <summary>
/// Draws fiducials from the library's own probe images, for offline tests and the conformance self-test. A
/// rendered marker is the unit square: a black border, a white ring, and the 32x32 pattern the library row
/// describes in the probe region (0.209..0.791 of the square). The border width (0.1) is LOCAL: the engine's
/// probes only pin it between 0.0125 (dark) and 0.15 (bright).
/// </summary>
public static class MarkerRenderer
{
    public const double BorderFraction = 0.10;

    /// <summary>The intensity of the marker at a unit-square position, from a library row.</summary>
    public static byte Sample(MarkerLibrary lib, int row, double u, double v)
    {
        if (u < 0 || v < 0 || u > 1 || v > 1) return 255;
        if (u < BorderFraction || v < BorderFraction || u > 1 - BorderFraction || v > 1 - BorderFraction) return 0;
        // probe grid: index k = col*32 + row with x = centres[k] (column), y = centres[k] (row); equal spacing
        double first = MarkerLibrary.Fixed(lib.ProbeCentersY[0]), last = MarkerLibrary.Fixed(lib.ProbeCentersY[MarkerLibrary.GridSize - 1]);
        double step = (last - first) / (MarkerLibrary.GridSize - 1);
        double lo = first - step / 2, hi = last + step / 2;
        if (u < lo || v < lo || u > hi || v > hi) return 255;
        int col = Math.Clamp((int)((u - lo) / step), 0, MarkerLibrary.GridSize - 1);
        int r = Math.Clamp((int)((v - lo) / step), 0, MarkerLibrary.GridSize - 1);
        // find the probe whose centre is (col, r): the table is column-major (x constant over 32 consecutive entries)
        int k = col * MarkerLibrary.GridSize + r;
        return lib.Image(row)[k];
    }

    /// <summary>A square image of one library row's marker.</summary>
    public static GrayImage Render(MarkerLibrary lib, int row, int sizePx)
    {
        var img = new GrayImage(sizePx, sizePx);
        for (int y = 0; y < sizePx; y++)
            for (int x = 0; x < sizePx; x++)
                img[x, y] = Sample(lib, row, (x + 0.5) / sizePx, (y + 0.5) / sizePx);
        return img;
    }

    /// <summary>
    /// Paints a marker into a frame at the quad <paramref name="corners"/> (TL, BL, TR, BR in pixels), by inverse
    /// mapping every covered pixel to the unit square and supersampling 2x2.
    /// </summary>
    public static void Draw(GrayImage frame, MarkerLibrary lib, int row, Vec2[] corners)
    {
        var h = Homography.FromUnitSquare(corners).Inverse();
        int minX = (int)Math.Floor(corners.Min(c => c.X)) - 1, maxX = (int)Math.Ceiling(corners.Max(c => c.X)) + 1;
        int minY = (int)Math.Floor(corners.Min(c => c.Y)) - 1, maxY = (int)Math.Ceiling(corners.Max(c => c.Y)) + 1;
        for (int y = Math.Max(0, minY); y <= Math.Min(frame.Height - 1, maxY); y++)
            for (int x = Math.Max(0, minX); x <= Math.Min(frame.Width - 1, maxX); x++)
            {
                int sum = 0, n = 0;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        var uv = h.Apply(new Vec2(x + 0.25 + 0.5 * sx, y + 0.25 + 0.5 * sy));
                        if (uv.X < 0 || uv.Y < 0 || uv.X > 1 || uv.Y > 1) { sum += frame[x, y]; n++; continue; }
                        sum += Sample(lib, row, uv.X, uv.Y); n++;
                    }
                frame[x, y] = (byte)(sum / n);
            }
    }

    /// <summary>The library row (orientation 0) that carries a marker code, or −1.</summary>
    public static int RowForCode(MarkerLibrary lib, MarkerType code, float orientationDeg = 0)
    {
        foreach (var label in lib.LabelsForCode(code))
            if (lib.OrientationDeg[label] == orientationDeg)
                return lib.RowsForLabel(label).First();
        return -1;
    }

    /// <summary>
    /// Renders a cube's visible markers into a frame as the camera would see them: each marker whose face turns
    /// towards the camera is projected and painted with its library image. Returns the codes drawn.
    /// </summary>
    public static IReadOnlyList<MarkerType> DrawCube(GrayImage frame, MarkerLibrary lib, CameraModel camera, ObjectType type, Pose3d cubePose)
    {
        var drawn = new List<MarkerType>();
        foreach (var m in CubeGeometry.CubeMarkers(type))
        {
            var world = m.CornersInWorld(cubePose);
            var centre = (world[0] + world[1] + world[2] + world[3]) / 4;
            var normal = (cubePose.Rotation * m.NormalOnObject).Normalized();
            if (normal.Dot((camera.Pose.Translation - centre).Normalized()) <= 0.05) continue;   // facing away
            var px = new Vec2[4];
            bool ok = true;
            for (int i = 0; i < 4; i++) { var p = camera.Project(world[i]); if (p is null) { ok = false; break; } px[i] = p.Value; }
            if (!ok) continue;
            int row = RowForCode(lib, m.Code);
            if (row < 0) continue;
            Draw(frame, lib, row, px);
            drawn.Add(m.Code);
        }
        return drawn;
    }
}
