namespace Cozmo.Robot.Vision;

internal sealed record NativeFaceFeatures(
    IReadOnlyList<Vec2> LeftEye, IReadOnlyList<Vec2> RightEye,
    IReadOnlyList<Vec2> Nose, IReadOnlyList<Vec2> Mouth)
{
    internal static NativeFaceFeatures Empty { get; } = new(
        Array.Empty<Vec2>(), Array.Empty<Vec2>(), Array.Empty<Vec2>(), Array.Empty<Vec2>());

    internal static NativeFaceFeatures FromPoints((int X, int Y)[] points)
    {
        IReadOnlyList<Vec2> Group(params int[] indices)
        {
            if (indices.Any(i => points[i].X == -1 || points[i].Y == -1)) return Array.Empty<Vec2>();
            return Array.AsReadOnly(indices.Select(i => new Vec2((float)points[i].X, (float)points[i].Y)).ToArray());
        }
        return new(Group(5, 1, 6), Group(9, 10), Group(7, 11, 8, 2, 7), Group(4, 0, 3));
    }
}
