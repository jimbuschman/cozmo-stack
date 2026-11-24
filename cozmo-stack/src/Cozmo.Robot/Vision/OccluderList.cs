namespace Cozmo.Robot.Vision;

/// <summary>
/// Native OccluderList stores image-space bounding rectangles keyed by binary32 depth.
/// Both the point-vector and quadrilateral entries take their axis-aligned bounds
/// (0x00878128 and 0x0087804C), keeping the larger area at an equal depth.
/// </summary>
// fidelity: M11-010
public sealed class OccluderList
{
    private readonly List<Occluder> _entries = new();

    public IReadOnlyList<Occluder> Entries => _entries;
    public int Count => _entries.Count;

    public void Clear() => _entries.Clear();

    internal OccluderList Copy()
    {
        var copy = new OccluderList();
        foreach (var entry in _entries)
            copy._entries.Add(new Occluder((Vec2[])entry.Quad.Clone(), entry.DepthMm));
        return copy;
    }

    public void Add(Vec2[] points, double depthMm)
    {
        if (points.Length == 0 || !double.IsFinite(depthMm)) return;
        var rectangle = Bounds.From(points);
        if (!rectangle.IsFinite) return;
        float depth = (float)depthMm;
        int i = 0;
        while (i < _entries.Count && _entries[i].DepthMm < depth) i++;
        var entry = new Occluder(rectangle.Corners(), depth);
        if (i < _entries.Count && _entries[i].DepthMm == depth)
        {
            // 0x00878174–0x008781A6. The first rectangle wins an equal-area tie.
            if (rectangle.Area > Bounds.From(_entries[i].Quad).Area) _entries[i] = entry;
            return;
        }
        _entries.Insert(i, entry);
    }

    /// <summary>0x00878278 tests strictly closer rectangles with half-open image boundaries.</summary>
    public bool IsOccluded(Vec2 p, double depthMm)
    {
        float depth = (float)depthMm, x = (float)p.X, y = (float)p.Y;
        foreach (var o in _entries)
        {
            if (o.DepthMm >= depth) break;
            if (x >= o.Quad[0].X && x < o.Quad[2].X && y >= o.Quad[0].Y && y < o.Quad[2].Y)
                return true;
        }
        return false;
    }

    /// <summary>0x008783C8 starts at the first strictly deeper rectangle and requires positive intersection area.</summary>
    public bool IsAnythingBehind(Vec2[] quad, double depthMm)
    {
        float depth = (float)depthMm;
        var query = Bounds.From(quad);
        foreach (var o in _entries)
        {
            if (o.DepthMm <= depth) continue;
            float width = MathF.Min(query.Right, (float)o.Quad[2].X) - MathF.Max(query.Left, (float)o.Quad[0].X);
            float height = MathF.Min(query.Bottom, (float)o.Quad[2].Y) - MathF.Max(query.Top, (float)o.Quad[0].Y);
            if (width > 0f && height > 0f && width * height > 0f) return true;
        }
        return false;
    }

    private readonly record struct Bounds(float Left, float Top, float Width, float Height)
    {
        internal float Right => Left + Width;
        internal float Bottom => Top + Height;
        internal float Area => Width * Height;
        internal bool IsFinite => float.IsFinite(Left) && float.IsFinite(Top) &&
                                  float.IsFinite(Width) && float.IsFinite(Height);

        // Rectangle<float>::InitFromPointContainer (0x004E81B4 and 0x0087855C).
        internal static Bounds From(Vec2[] points)
        {
            if (points.Length == 0) return default;
            float left = (float)points[0].X, top = (float)points[0].Y;
            float right = left, bottom = top;
            foreach (var point in points.AsSpan(1))
            {
                float x = (float)point.X, y = (float)point.Y;
                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
            return new(left, top, right - left, bottom - top);
        }

        internal Vec2[] Corners() => [new(Left, Top), new(Right, Top), new(Right, Bottom), new(Left, Bottom)];
    }
}
