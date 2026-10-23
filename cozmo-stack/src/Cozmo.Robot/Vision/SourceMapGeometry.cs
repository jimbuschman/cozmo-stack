using Cozmo.Robot.Manipulation;
using Cozmo.Transport;

namespace Cozmo.Robot.Vision;

// fidelity: M11-045, M11-048 — 0x00842740..0x008427A2, 0x0084292C..0x00842B64.
internal readonly struct SourceLineSegment
{
    internal readonly P2f A, B;
    internal readonly float MinX, MaxX, MinY, MaxY, Dx, Dy;
    internal SourceLineSegment(P2f a, P2f b)
    {
        A = a; B = b;
        MinX = float.MinNumber(a.X, b.X); MaxX = float.MaxNumber(a.X, b.X);
        MinY = float.MinNumber(a.Y, b.Y); MaxY = float.MaxNumber(a.Y, b.Y);
        Dx = b.X - a.X; Dy = b.Y - a.Y;
    }
    internal int Orientation(P2f p)
    {
        float x = p.X - A.X, y = p.Y - A.Y;
        float first = Dy * x, second = Dx * y;
        float t = first - second;
        float abs = t < 0 ? -t : t;
        float e = BitConverter.UInt32BitsToSingle(0x3727C5AC);
        if (abs < e) return 0;
        return t >= -e ? 1 : 2; // unordered -> 2
    }
    private bool OnSegment(P2f p) => p.X <= MaxX && p.X >= MinX && p.Y <= MaxY && p.Y >= MinY;
    internal bool IntersectsWith(SourceLineSegment other)
    {
        int a = Orientation(other.A);
        if (a == 0 && OnSegment(other.A)) return true;
        int b = Orientation(other.B);
        if (b == 0 && OnSegment(other.B)) return true;
        if (a == b) return false;
        int c = other.Orientation(A);
        if (c == 0 && other.OnSegment(A)) return true;
        int d = other.Orientation(B);
        if (d == 0 && other.OnSegment(B)) return true;
        return c != d;
    }
}

// fidelity: M11-045, M11-048 — ResetBoundingBox 0x0068698C, AxisAlignedQuad 0x00686890.
internal readonly struct SourceQuadBounds
{
    internal readonly float MinX, MinY, MaxX, MaxY;
    internal readonly SourceLineSegment Diagonal0, Diagonal1;
    internal SourceQuadBounds(float x, float y, float side)
    {
        float half = side * .5f;
        float ax = x - half, ay = y - half, bx = half + x, by = half + y;
        MinX = float.MinNumber(ax, bx); MinY = float.MinNumber(ay, by);
        MaxX = float.MaxNumber(ax, bx); MaxY = float.MaxNumber(ay, by);
        Diagonal0 = new(new(MinX, MinY), new(MaxX, MaxY));
        Diagonal1 = new(new(MaxX, MinY), new(MinX, MaxY));
    }
    internal bool Contains(P2f p)
    {
        float e = BitConverter.UInt32BitsToSingle(0x3727C5AC);
        float minX = MinX - e, maxX = MaxX + e, minY = MinY - e, maxY = MaxY + e;
        return minX <= p.X && maxX >= p.X && minY <= p.Y && maxY >= p.Y;
    }
}

// fidelity: M11-045, M11-046, M11-048 — FastPolygon 0x008416F0 and callees; overlap 0x00688290.
// Map-specific port: preserve four independent bounds scans and the stored polygon-edge receiver order.
// The planner's FastPolygonF remains unchanged.
internal sealed class SourceFastPolygon
{
    internal readonly P2f[] Points;
    internal readonly SourceLineSegment[] Edges;
    internal readonly (float X, float Y)[] Normals;
    internal readonly float CenterX, CenterY, MinX, MaxX, MinY, MaxY;
    internal readonly float OuterSquared, InnerSquared;
    internal bool Valid => Points.Length != 0;

    internal SourceFastPolygon(IEnumerable<Vec2> vertices)
    {
        Points = vertices.Select(p => new P2f((float)p.X, (float)p.Y)).ToArray();
        int n = Points.Length;
        Edges = n < 2 ? Array.Empty<SourceLineSegment>() : new SourceLineSegment[n];
        Normals = new (float, float)[Edges.Length];
        for (int i = 0; i < Edges.Length; i++)
        {
            var a = Points[i]; var b = Points[(i + 1) % n];
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float squareX = dx * dx, squareY = dy * dy;
            float length = MathF.Sqrt(squareX + squareY);
            float inverse = 1f / length;
            Edges[i] = new(a, b);
            Normals[i] = (-(inverse * dy), inverse * dx);
        }
        if (n != 0)
        {
            float inverse = 1f / (float)(uint)n;
            foreach (var p in Points)
            {
                float weightedX = inverse * p.X, weightedY = inverse * p.Y;
                CenterX = CenterX + weightedX; CenterY = CenterY + weightedY;
            }
        }
        MinX = float.MaxValue; MaxX = -float.MaxValue; MinY = float.MaxValue; MaxY = -float.MaxValue;
        foreach (var p in Points) if (p.X < MinX) MinX = p.X;
        foreach (var p in Points) if (p.X > MaxX) MaxX = p.X;
        foreach (var p in Points) if (p.Y < MinY) MinY = p.Y;
        foreach (var p in Points) if (p.Y > MaxY) MaxY = p.Y;
        InnerSquared = float.MaxValue;
        if (n == 0)
        {
            StackDiagnostics.Write("M1-034 / M11-045: skip empty FastPolygon operation; undefined vertex read at 0x00841A94/0x00841A9C; map state retained");
            return;
        }
        if (n < 3)
        {
            float dx = Points[n == 1 ? 0 : 1].X - Points[0].X;
            float dy = Points[n == 1 ? 0 : 1].Y - Points[0].Y;
            OuterSquared = MixedSquare(dx, dy); InnerSquared = 0;
            return;
        }
        for (int i = 0; i < n; i++)
        {
            float dx = Points[i].X - CenterX, dy = Points[i].Y - CenterY;
            float distance = MixedSquare(dx, dy);
            if (OuterSquared < distance) OuterSquared = distance;
            float cx = CenterX - Points[i].X, cy = CenterY - Points[i].Y;
            float first = Normals[i].X * cx, second = Normals[i].Y * cy;
            float dot = first + second, squared = dot * dot;
            if (InnerSquared > squared) InnerSquared = squared;
        }
        if (InnerSquared > OuterSquared)
            StackDiagnostics.Write(FormattableString.Invariant($"ERROR: inscribed radius of {(double)InnerSquared:F6} is >= circumscribed radius of {(double)OuterSquared:F6}\n"));
    }
    private static float MixedSquare(float dx, float dy)
    {
        double x = (double)dx * dx, y = (double)dy * dy;
        return (float)(x + y);
    }
    internal bool Contains(float x, float y)
    {
        if (MinX > x || MaxX < x || MinY > y || MaxY < y) return false;
        float dx = x - CenterX, dy = y - CenterY;
        float squareY = dy * dy, squareX = dx * dx, radius = squareY + squareX;
        if (radius > OuterSquared) return false;
        if (radius < InnerSquared) return true;
        for (int i = 0; i < Normals.Length; i++)
        {
            float px = x - Points[i].X, py = y - Points[i].Y;
            float first = Normals[i].X * px, second = Normals[i].Y * py;
            float dot = first + second;
            if (!(dot > 0)) continue; // ARM LE includes unordered
            return false;
        }
        return true;
    }
    internal int GetOverlapType(SourceQuadNode node)
    {
        bool allInside = true, anyInside = false;
        foreach (var p in Points)
        {
            bool inside = node.Bounds.Contains(p);
            allInside &= inside; anyInside |= inside;
            if (!allInside && anyInside) return 1;
        }
        if (allInside) return 3;
        if (Points.Length < 2) return 0;
        foreach (var diagonal in new[] { node.Bounds.Diagonal0, node.Bounds.Diagonal1 })
            foreach (var edge in Edges)
                if (edge.IntersectsWith(diagonal)) return 1;
        return Contains(node.Bounds.MinX, node.Bounds.MinY) ? 2 : 0;
    }
}

