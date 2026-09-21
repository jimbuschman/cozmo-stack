namespace Cozmo.Robot.Vision;

/// <summary>
/// The engine's <c>Anki::Vision::OccluderList</c>: the quads the camera actually saw this frame, in image
/// space, each with the depth it sits at, kept in depth order.
///
/// Two questions are asked of it, both from <c>KnownMarker::IsVisibleFrom</c>:
///
/// <list type="bullet">
/// <item><c>IsOccluded(point, depth)</c> 0x00878278 - is there an entry nearer than this point that
/// contains it? A marker any of whose four corners is occluded is <see cref="NotVisibleReason.Occluded"/>
/// (0x0087E836).</item>
/// <item><c>IsAnythingBehind(quad, depth)</c> 0x008783C8 - walking the list in depth order and stopping
/// at the first entry deeper than the query (<c>bhi</c> at 0x008783EC), is there an entry that overlaps
/// the quad? When there is not, the marker is <see cref="NotVisibleReason.NothingBehind"/>
/// (0x0087E87A), which is what <c>BlockWorld::CheckForUnobservedObjects</c> requires before it forgets
/// a Dirty object it could not see.</item>
/// </list>
///
/// The list is filled from observed markers: <c>Camera::AddOccluder(KnownMarker)</c> 0x0085E76C takes
/// the marker's 3D corners with respect to the camera, projects them and adds the projected quad with
/// its depth.
/// </summary>
public sealed class OccluderList
{
    private readonly List<Occluder> _entries = new();

    public IReadOnlyList<Occluder> Entries => _entries;
    public int Count => _entries.Count;

    /// <summary><c>OccluderList::Clear</c>, which UpdateObservedMarkers calls at the top of a frame.</summary>
    public void Clear() => _entries.Clear();

    /// <summary><c>OccluderList::AddOccluder</c>: the quad and its depth, kept in depth order.</summary>
    public void Add(Vec2[] quad, double depthMm)
    {
        if (quad.Length < 3) return;
        int i = 0;
        while (i < _entries.Count && _entries[i].DepthMm <= depthMm) i++;
        _entries.Insert(i, new Occluder(quad, depthMm));
    }

    /// <summary>Whether something nearer than <paramref name="depthMm"/> covers the point.</summary>
    public bool IsOccluded(Vec2 p, double depthMm)
    {
        foreach (var o in _entries)
        {
            if (o.DepthMm > depthMm) return false;        // the list is in depth order
            if (Contains(o.Quad, p)) return true;
        }
        return false;
    }

    /// <summary>Whether anything the camera saw overlaps this quad from no further away than it.</summary>
    public bool IsAnythingBehind(Vec2[] quad, double depthMm)
    {
        foreach (var o in _entries)
        {
            if (o.DepthMm > depthMm) return false;
            if (Overlaps(o.Quad, quad)) return true;
        }
        return false;
    }

    /// <summary>Point in convex polygon, by consistent cross-product sign.</summary>
    internal static bool Contains(Vec2[] poly, Vec2 p)
    {
        bool? positive = null;
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Length];
            double cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            if (Math.Abs(cross) < 1e-9) continue;
            bool pos = cross > 0;
            if (positive is null) positive = pos;
            else if (positive != pos) return false;
        }
        return true;
    }

    /// <summary>Two convex polygons overlap when either has a vertex inside the other, or their edges cross.</summary>
    internal static bool Overlaps(Vec2[] a, Vec2[] b)
    {
        foreach (var p in a) if (Contains(b, p)) return true;
        foreach (var p in b) if (Contains(a, p)) return true;
        for (int i = 0; i < a.Length; i++)
            for (int j = 0; j < b.Length; j++)
                if (Crosses(a[i], a[(i + 1) % a.Length], b[j], b[(j + 1) % b.Length])) return true;
        return false;
    }

    private static bool Crosses(Vec2 p1, Vec2 p2, Vec2 q1, Vec2 q2)
    {
        static double Side(Vec2 a, Vec2 b, Vec2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        double d1 = Side(p1, p2, q1), d2 = Side(p1, p2, q2), d3 = Side(q1, q2, p1), d4 = Side(q1, q2, p2);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }
}
