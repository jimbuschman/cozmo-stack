// fidelity: M8-026
using System.Buffers.Binary;
using System.Collections;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>Physical quad indices, not perimeter order (4DF764 tests 0,1,3 then0,2,3).</summary>
public readonly record struct WhiteboardClearQuad(Vec2 P0, Vec2 P1, Vec2 P2, Vec2 P3)
{
    private static readonly float Lower = BitConverter.UInt32BitsToSingle(0xB4000000);
    private static readonly float Upper = BitConverter.UInt32BitsToSingle(0x3F800001);
    public bool Contains(Vec2 point) => Triangle(P0, P1, P3, point) || Triangle(P0, P2, P3, point);
    public static bool Triangle(Vec2 a, Vec2 b, Vec2 c, Vec2 point)
    {
        // 4E03B8..4E047E: separately rounded f32 operations; no divide guard.
        float ax = (float)a.X, ay = (float)a.Y, bx = (float)b.X, by = (float)b.Y;
        float cx = (float)c.X, cy = (float)c.Y;
        float x0 = cx - bx, y0 = ay - cy, x1 = ax - cx, y1 = by - cy;
        float d = x1 * y1; d += x0 * y0;
        float inverse = 1f / d;
        float dy = (float)point.Y - cy, dx = (float)point.X - cx;
        float first = y1 * dx; first += x0 * dy; first = inverse * first;
        if (!(first > Lower && first < Upper)) return false;
        float second = (cy - ay) * dx; second += x1 * dy; second = inverse * second;
        if (!(second > Lower && second < Upper)) return false;
        float third = 1f - first; third -= second;
        return third > Lower && third < Upper;
    }
}

/// <summary>std::list values retain node identity. Size decrements before each value destructor.</summary>
internal sealed class PossibleObjectStorage : IReadOnlyList<PossibleObject>
{
    internal sealed class Node(PossibleObject value)
    {
        internal PossibleObject Value = value;
        internal Node? Previous, Next;
    }
    internal Node? Head, Tail;
    public int Count { get; private set; }
    internal Action<PossibleObject>? BeforeDestroyForTest;
    public PossibleObject this[int index]
    {
        get
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            var node = Head; while (index-- > 0 && node is not null) node = node.Next;
            return node?.Value ?? throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
    public void Add(PossibleObject value)
    {
        // Native list value owns its PoseBase copy (M11-067), not the caller's wrapper.
        var node = new Node(new PossibleObject(new Pose3d(value.Pose), value.Type)) { Previous = Tail };
        if (Tail is null) Head = node; else Tail.Next = node;
        Tail = node; Count++;
    }
    public void RemoveAt(int index)
    {
        var node = Head;
        for (int i = 0; i < index && node is not null; i++) node = node.Next;
        if (node is null || index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        Erase(node, node.Next);
    }
    public void RemoveAll(Predicate<PossibleObject> predicate)
    {
        for (var node = Head; node is not null;)
        {
            var next = node.Next; if (predicate(node.Value)) Erase(node, next); node = next;
        }
    }
    internal Node? Erase(Node? first, Node? end)
    {
        if (ReferenceEquals(first, end)) return end;
        if (first is null) return end;
        var previous = first.Previous;
        if (previous is null) Head = end; else previous.Next = end;
        if (end is null) Tail = previous; else end.Previous = previous;
        // 56CD9A..CDBC: unlink whole range first, then count--/destroy/delete for each node.
        for (var node = first; !ReferenceEquals(node, end) && node is not null;)
        {
            var next = node.Next; Count--;
            BeforeDestroyForTest?.Invoke(node.Value);
            node.Value.Pose.Dispose(); node.Previous = null; node.Next = null;
            node = next;
        }
        return end;
    }
    public IEnumerator<PossibleObject> GetEnumerator()
    { for (var node = Head; node is not null; node = node.Next) yield return node.Value; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal readonly record struct WhiteboardSegment(string Group, uint Color, Point3f Start, Point3f End, bool ClearPrevious);

public sealed partial class AIWhiteboard
{
    internal PossibleObjectStorage PossibleObjectStorage { get; } = new();
    public IReadOnlyList<PossibleObject> PossibleObjects => PossibleObjectStorage;
    // Robot::GetWorldOrigin input (4A4C54). No unframed identity or guessed root is substituted.
    internal Func<Pose3d?>? ClearQuadWorldOrigin;
    internal Action<Pose3d>? ClearQuadPredicateForTest;
    internal Action<string>? ClearQuadRenderForTest;
    internal Action<WhiteboardSegment>? SegmentConstructedForTest;
    internal Func<Pose3d, Pose3d, Pose3d, int>? RelativePoseForTest;
    private const string PossibleGroup = "AIWhiteboard.PossibleObjects";

    public void ProcessClearQuad(WhiteboardClearQuad quad)
    {
        using var logScope = Pose3d.UseSourceLog((level, channel, key, text) =>
            _world.SourceLog?.Invoke(level, channel, key, text), _world.NativeForceFlush);
        if (ClearQuadWorldOrigin?.Invoke() is not { } origin)
        {
            SteppedBehavior.ReportMissing("M8-026 GetWorldOrigin 0x004A4C54 at 0x0056AC92 has no bound Robot/world-origin provider; ProcessClearQuad unit is built");
            return;
        }
        if (origin.Node is null)
        {
            StackDiagnostics.Write("M1-034 M8-026 null origin pose at 0x0056ACA2: skip ProcessClearQuad; prior list retained");
            return;
        }
        using var capture = new Pose3d(origin); // 56ACA2
        int size = PossibleObjectStorage.Count;
        using (var predicateCapture = new Pose3d(capture)) // 56ACC2, destroyed before size/render check
        {
            for (var node = PossibleObjectStorage.Head; node is not null;)
            {
                if (Matches(node.Value.Pose, predicateCapture, quad))
                {
                    var end = node.Next;
                    while (end is not null && Matches(end.Value.Pose, predicateCapture, quad)) end = end.Next;
                    node = PossibleObjectStorage.Erase(node, end);
                    if (node is not null) node = node.Next; // first nonmatch already tested, do not retest
                }
                else node = node.Next;
            }
        }
        if (size != PossibleObjectStorage.Count) UpdatePossibleObjectRender();
    }
    private int Relative(Pose3d from, Pose3d frame, Pose3d output)
        => RelativePoseForTest?.Invoke(from, frame, output) ?? (Pose3d.GetWithRespectTo(from, frame, output) ? 1 : 0);
    private bool Matches(Pose3d pose, Pose3d frame, WhiteboardClearQuad quad)
    {
        if (pose.Node is null)
        {
            StackDiagnostics.Write("M1-034 M8-026 null possible-object pose at 0x0056CD36: skip this predicate; prior entry retained");
            return false;
        }
        ClearQuadPredicateForTest?.Invoke(pose);
        using var relative = new Pose3d();
        if (Relative(pose, frame, relative) == 0) return false;
        var p = relative.Translation;
        return quad.Contains(new Vec2((float)p.X, (float)p.Y)); // center only; ignores Z/type/extent/age
    }
    private void UpdatePossibleObjectRender()
    {
        if (ClearQuadWorldOrigin?.Invoke() is not { } origin)
        {
            SteppedBehavior.ReportMissing("M8-026 render GetWorldOrigin/Viz owner 0x0056A6E2..0x0056A6F4 absent");
            return;
        }
        ErasePossibleSegments();
        float liftX = 0f * 35f, liftY = 0f * 35f, liftZ = 1f * 35f;
        foreach (var possible in PossibleObjectStorage)
        {
            if (possible.Pose.Node is null || origin.Node is null)
            {
                StackDiagnostics.Write("M1-034 M8-026 null render pose at 0x0056A7AE: skip this render entry; prior list retained");
                continue;
            }
            using var relative = new Pose3d();
            if (Relative(possible.Pose, origin, relative) != 1) continue;
            var corners = new[] { new Vec3(50, 50, 0), new Vec3(-50, 50, 0), new Vec3(50, -50, 0), new Vec3(-50, -50, 0) };
            foreach (int i in new[] { 0, 2, 1, 3 })
            {
                ClearQuadRenderForTest?.Invoke("apply" + i);
                corners[i] = relative.Transform.Apply(corners[i]); // M11-065, same ApplyTo body, physical order
            }
            for (int i = 0; i < 4; i++) corners[i] = Lift(corners[i]);
            DrawSegment(corners[0], corners[2], 0xFF007FFF, false);
            DrawSegment(corners[2], corners[3], 0xFF007FFF, false);
            DrawSegment(corners[3], corners[1], 0xFF007FFF, false);
            DrawSegment(corners[1], corners[0], 0xFF007FFF, false);
            float x = 1f * 50f, y = 0f * 50f, z = 0f * 50f;
            x *= 0.5f; y *= 0.5f; z *= 0.5f;
            var rotated = relative.Quaternion.Rotate(new Vec3(x, y, z));
            var t = relative.Translation;
            var direction = new Vec3((float)rotated.X + (float)t.X, (float)rotated.Y + (float)t.Y, (float)rotated.Z + (float)t.Z);
            DrawSegment(Lift(t), Lift(direction), 0xFF00FFFF, false);
        }
        Vec3 Lift(Vec3 p) => new(liftX + (float)p.X, liftY + (float)p.Y, liftZ + (float)p.Z);
    }
    private void ErasePossibleSegments()
    {
        // 6BFCD8: temporary EraseSegmentPrimitives only; no send or VizManager field mutation.
        ClearQuadRenderForTest?.Invoke("erase");
    }
    private void DrawSegment(Vec3 start, Vec3 end, uint color, bool clearPrevious)
    {
        float scale = BitConverter.UInt32BitsToSingle(0x3A83126F);
        Point3f Scale(Vec3 p) => new((float)p.X * scale, (float)p.Y * scale, ((float)p.Z + 0f) * scale);
        var segment = new WhiteboardSegment(PossibleGroup, BinaryPrimitives.ReverseEndianness(color), Scale(start), Scale(end), clearPrevious);
        SegmentConstructedForTest?.Invoke(segment);
        SendMessage(segment);
    }
    private static void SendMessage(WhiteboardSegment segment) { } // 6BE610 bx lr
}
