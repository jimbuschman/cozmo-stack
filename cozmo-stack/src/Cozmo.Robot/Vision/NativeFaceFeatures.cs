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
        // DetectFaceParts selects slots 0, 1, 7 and 4 at 0x0086CDFC, 0x0086CE52,
        // 0x0086CEA8 and 0x0086CEFC. FaceWorld copies them into CLAD in that order
        // (0x004F4A7C..0x004F4AF8); 0x0086D130..0x0086D164 keeps image X then Y.
        return new(Group(4, 0, 3), Group(5, 1, 6), Group(9, 10), Group(7, 11, 8, 2, 7));
    }
}
