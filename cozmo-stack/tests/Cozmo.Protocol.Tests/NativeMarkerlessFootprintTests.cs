using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class NativeMarkerlessFootprintTests
{
    // reach: concrete markerless vslot adapter, 0x005025F0 and 0x00502C32; expected extents
    // come from shipped object dimensions and the source's separately doubled padding.
    [Theory]
    [InlineData(ObjectType.ProxObstacle, 0f, 5f, 5f)]
    [InlineData(ObjectType.CliffDetection, 0f, 10f, 20f)]
    [InlineData(ObjectType.CollisionObstacle, 0f, 10f, 27.1f)]
    [InlineData(ObjectType.CliffDetection, 10f, 20f, 30f)]
    public void NativeSlotUsesTypeDimensionsAndSymmetricPadding(ObjectType type, float padding, float halfX, float halfY)
    {
        var obj = new ObservableObject(1, type, Array.Empty<KnownMarker>());
        using var pose = new Pose3d(Rotation3d.Identity, new Vec3(100, 200, 7));
        Assert.True(obj.TryObservableBoundingQuad50(pose, padding, out var result));
        Assert.NotNull(result);
        AssertBounds(result!, 100 - halfX, 100 + halfX, 200 - halfY, 200 + halfY);
    }

    // reach: actual +50 adapter with a two-level pose tree, not the pre-existing flat host helper.
    [Fact]
    public void NativeSlotFlattensTheSuppliedPoseBeforeRotationAndTranslation()
    {
        var obj = new ObservableObject(1, ObjectType.CliffDetection, Array.Empty<KnownMarker>());
        using var root = new Pose3d();
        using var parent = new Pose3d(Rotation3d.FromAngleAxis(Radians.Pi / 2f, new Vec3(0, 0, 1)),
            new Vec3(100, 200, 0), parent: root);
        using var pose = new Pose3d(Rotation3d.Identity, new Vec3(10, 0, 5), parent: parent);
        Assert.True(obj.TryObservableBoundingQuad50(pose, 10, out var result));
        Assert.NotNull(result);
        AssertBounds(result!, 70, 130, 190, 230);
    }

    // The 50 mm Z dimension contributes to Y after a 90-degree X tilt.
    [Fact]
    public void NativeSlotProjectsAllThreeDimensionsForTiltedObjects()
    {
        var obj = new ObservableObject(1, ObjectType.CliffDetection, Array.Empty<KnownMarker>());
        double half = Math.Sqrt(0.5);
        using var pose = new Pose3d(new Rotation3d(half, half, 0, 0), new Vec3(100, 200, 0));
        Assert.True(obj.TryObservableBoundingQuad50(pose, 0, out var result));
        Assert.NotNull(result);
        AssertBounds(result!, 90, 110, 175, 225);
    }

    // reach: actual markerless +50 slot and full pose tree. Expected native words are from
    // libcozmoEngine.so 0x00502C32 with the shipped OpenCV and the oracle's host atan2.
    [Fact]
    public void TiltedCollisionQuadMatchesTheShippedArmOracleWords()
    {
        var obj = new ObservableObject(1, ObjectType.CollisionObstacle, Array.Empty<KnownMarker>());
        using var root = new Pose3d();
        using var parent = new Pose3d(new Rotation3d(Math.Cos(Math.PI / 4), 0, 0, Math.Sin(Math.PI / 4)),
            new Vec3(100, 200, 3), parent: root);
        using var pose = new Pose3d(new Rotation3d(Math.Cos(Math.PI / 12), 0, Math.Sin(Math.PI / 12), 0),
            new Vec3(7, 9, 2), parent: parent);
        Assert.True(obj.TryObservableBoundingQuad50(pose, 10, out var quad));
        Assert.NotNull(quad);
        uint[] expected =
        {
            0x42579999, 0x4327C126, 0x42579999, 0x43763EDA,
            0x4300199A, 0x4327C126, 0x4300199A, 0x43763EDA,
        };
        var actual = new List<uint>(8);
        for (int i = 0; i < 4; i++)
        {
            actual.Add(BitConverter.SingleToUInt32Bits(quad![i].X));
            actual.Add(BitConverter.SingleToUInt32Bits(quad[i].Y));
        }
        Assert.Equal(expected, actual);
    }

    private static void AssertBounds(Quadrilateral quad, float minX, float maxX, float minY, float maxY)
    {
        var points = Enumerable.Range(0, 4).Select(i => quad[i]).ToArray();
        Assert.InRange(points.Min(point => point.X), minX - .001f, minX + .001f);
        Assert.InRange(points.Max(point => point.X), maxX - .001f, maxX + .001f);
        Assert.InRange(points.Min(point => point.Y), minY - .001f, minY + .001f);
        Assert.InRange(points.Max(point => point.Y), maxY - .001f, maxY + .001f);
    }
}
