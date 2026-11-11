using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

// ClampPoseToFlat 0x00877330..0x008774EC with the docking error signal's 40-degree limit (0x0063C182..0x0063C188, call 0x0063C194).
// The arithmetic is the shared port BlockWorld.ClampRotationToFlat (M11-006); MathF transcendental calls retain the
// M13-023(e) unshipped bionic-libm boundary; matrix and quaternion operations use the native f32 owners.
internal static class NativeDockingGeometry
{
    private static readonly float Limit = BitConverter.UInt32BitsToSingle(0x3F32B8C2);

    // Returns the local rotation unchanged when the engine's clamp returns false.
    internal static Rotation3d Clamp(Rotation3d local, Rotation3d root)
        => BlockWorld.ClampRotationToFlat(local, root, new Radians(Limit), out var result) ? result : local;
}
