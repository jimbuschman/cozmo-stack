using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

// ClampPoseToFlat 0x00877330..0x008774EC. MathF transcendental calls retain the
// M13-023(e) unshipped bionic-libm boundary; matrix and quaternion operations use the native f32 owners.
internal static class NativeDockingGeometry
{
    private static readonly float Limit = BitConverter.UInt32BitsToSingle(0x3F32B8C2);
    private static readonly float Epsilon = BitConverter.UInt32BitsToSingle(0x3727C5AC);

    internal static Rotation3d Clamp(Rotation3d local, Rotation3d root)
    {
        Mat3 rootMatrix = root.Matrix();
        int axis = RotatedParentAxis.GetNativeZ(rootMatrix);
        int column = Math.Abs(axis) - 1;
        float dominant = (float)rootMatrix[2, column];
        if (!(new Radians(MathF.Acos(MathF.Abs(dominant))) < new Radians(Limit))) return local;

        Mat3 matrix = local.Matrix();
        float sign = axis > 0 ? 1f : -1f;
        float zero = axis > 0 ? 0f : -0f;
        float x = (float)matrix[0, column], y = (float)matrix[1, column], z = (float)matrix[2, column];
        float dot = x * zero;
        dot += y * zero;
        dot += z * sign;
        float angle = new Radians(MathF.Acos(dot)).Value;
        if (MathF.Abs(angle) < Epsilon) return local;
        float crossX = y * sign - z * zero;
        float crossY = z * zero - x * sign;
        float crossZ = x * zero - y * zero;
        var unit = RotationVector3d.MakeUnit(new Vec3(crossX, crossY, crossZ));
        if (unit.Length < Epsilon) return local;
        return Rotation3d.FromAngleAxis(angle, unit.Axis) * local;
    }
}
