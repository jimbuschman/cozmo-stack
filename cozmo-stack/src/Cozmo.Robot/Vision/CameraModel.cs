using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

/// <summary>
/// Where the camera sits on the robot, from the engine's <c>Robot::Robot</c> constructor and
/// <c>Robot::GetCameraPose(float headAngle)</c> (0x004A7870 PLT), all NATIVE:
///
/// * the neck joint "RobotNeck" is at (−13.0, 0, 49.0) mm in the robot frame (X forward, Y left, Z up);
/// * the camera "RobotHeadCam" is at (17.52, 0, 17.52) mm from the neck, rotated by
///   <c>Robot::_kDefaultHeadCamRotation</c>, the nine floats at 0x00C4A854:
///   <c>[0, −0.0698, 0.9976; −1, 0, 0; 0, −0.9976, −0.0698]</c>, which maps the camera's optical frame
///   (X right, Y down, Z forward) into the head frame with the optical axis 4 degrees below the head's X axis;
/// * <c>GetCameraPose</c> rotates the camera pose about the neck's Y axis by −headAngle.
///
/// The robot pose comes from <c>RobotState.Pose</c>. Its pitch is not applied to the camera pose here
/// (LOCAL: the engine builds the robot pose with pitch when it is off level; on flat ground it is ~0).
/// </summary>
public static class HeadGeometry
{
    public static readonly Vec3 NeckPositionMm = new(-13.0, 0.0, 49.0);
    public static readonly Vec3 HeadCamPositionMm = new(17.52, 0.0, 17.52);
    public static readonly Mat3 DefaultHeadCamRotation = new(
        0.0, -0.0697999969124794, 0.9976000189781189,
        -1.0, 0.0, 0.0,
        0.0, -0.9976000189781189, -0.0697999969124794);

    /// <summary>The camera pose in the robot frame for a head angle (radians, positive = up).</summary>
    public static Pose3d CameraPoseInRobot(double headAngleRad)
    {
        var neck = new Pose3d(Mat3.AboutY(-headAngleRad), NeckPositionMm);
        var cam = new Pose3d(DefaultHeadCamRotation, HeadCamPositionMm);
        return neck.Compose(cam);
    }

    /// <summary>The robot's pose in its world origin from a state message: X, Y, Z and yaw about Z.</summary>
    public static Pose3d RobotPose(in RobotPose p) => new(Mat3.AboutZ(p.Angle), new Vec3(p.X, p.Y, p.Z));

    /// <summary>The camera pose in the world for a robot pose and head angle.</summary>
    public static Pose3d CameraPoseInWorld(Pose3d robotPose, double headAngleRad) => robotPose.Compose(CameraPoseInRobot(headAngleRad));

    /// <summary>The engine's head range used by <c>TurnTowardsPoseAction::Init</c>: −25 deg to 44.5 deg (0xBEDF66F3, 0x3F46D3F2).</summary>
    public const double MinHeadAngleRad = -0.436332, MaxHeadAngleRad = 0.776672;
}

/// <summary>
/// The engine's <c>Anki::Vision::Camera</c>: a calibration plus a pose, projecting world points into pixels
/// with OpenCV's radial-tangential distortion model and answering <c>IsWithinFieldOfView</c>.
/// </summary>
public sealed class CameraModel
{
    public CameraModel(CameraCalibration calibration, Pose3d pose)
    {
        Calibration = calibration;
        Pose = pose;
    }

    public CameraCalibration Calibration { get; }
    /// <summary>Camera pose in the world: camera frame X right, Y down, Z forward.</summary>
    public Pose3d Pose { get; }

    public CameraModel WithPose(Pose3d pose) => new(Calibration, pose);

    /// <summary>A world point in the camera frame.</summary>
    public Vec3 ToCamera(Vec3 world) => Pose.Inverse().Apply(world);

    /// <summary>Projects a camera-frame point; null when it is at or behind the camera.</summary>
    public Vec2? ProjectCameraPoint(Vec3 c)
    {
        if (c.Z <= 1e-6) return null;
        return Distort(new Vec2(c.X / c.Z, c.Y / c.Z));
    }

    /// <summary>Projects a world point into pixels; null when it is behind the camera.</summary>
    public Vec2? Project(Vec3 world) => ProjectCameraPoint(ToCamera(world));

    /// <summary>Normalised image coordinates to pixels through the distortion model and intrinsics.</summary>
    public Vec2 Distort(Vec2 n)
    {
        double x = n.X, y = n.Y;
        var d = Calibration.DistortionCoefficients;
        if (Calibration.HasDistortion)
        {
            double k1 = d[0], k2 = d[1], p1 = d[2], p2 = d[3], k3 = d.Length > 4 ? d[4] : 0;
            double k4 = d.Length > 5 ? d[5] : 0, k5 = d.Length > 6 ? d[6] : 0, k6 = d.Length > 7 ? d[7] : 0;
            double r2 = x * x + y * y, r4 = r2 * r2, r6 = r4 * r2;
            double radial = (1 + k1 * r2 + k2 * r4 + k3 * r6) / (1 + k4 * r2 + k5 * r4 + k6 * r6);
            double xd = x * radial + 2 * p1 * x * y + p2 * (r2 + 2 * x * x);
            double yd = y * radial + p1 * (r2 + 2 * y * y) + 2 * p2 * x * y;
            x = xd; y = yd;
        }
        return new Vec2(Calibration.FocalLengthX * x + Calibration.Skew * y + Calibration.CenterX,
                        Calibration.FocalLengthY * y + Calibration.CenterY);
    }

    /// <summary>Pixels to normalised image coordinates, undoing distortion iteratively (cv::undistortPoints' scheme).</summary>
    public Vec2 Undistort(Vec2 px)
    {
        double y = (px.Y - Calibration.CenterY) / Calibration.FocalLengthY;
        double x = (px.X - Calibration.CenterX - Calibration.Skew * y) / Calibration.FocalLengthX;
        if (!Calibration.HasDistortion) return new Vec2(x, y);
        var d = Calibration.DistortionCoefficients;
        double k1 = d[0], k2 = d[1], p1 = d[2], p2 = d[3], k3 = d.Length > 4 ? d[4] : 0;
        double k4 = d.Length > 5 ? d[5] : 0, k5 = d.Length > 6 ? d[6] : 0, k6 = d.Length > 7 ? d[7] : 0;
        double x0 = x, y0 = y;
        for (int i = 0; i < 10; i++)
        {
            double r2 = x * x + y * y, r4 = r2 * r2, r6 = r4 * r2;
            double icdist = (1 + k4 * r2 + k5 * r4 + k6 * r6) / (1 + k1 * r2 + k2 * r4 + k3 * r6);
            double dx = 2 * p1 * x * y + p2 * (r2 + 2 * x * x);
            double dy = p1 * (r2 + 2 * y * y) + 2 * p2 * x * y;
            x = (x0 - dx) * icdist; y = (y0 - dy) * icdist;
        }
        return new Vec2(x, y);
    }

    /// <summary>
    /// <c>Camera::IsWithinFieldOfView(point, xPad, yPad)</c>: the projected point lies inside the image
    /// grown by the padding, and in front of the camera.
    /// </summary>
    public bool IsWithinFieldOfView(Vec3 world, double padPixels = 0)
    {
        var p = Project(world);
        return p is { } q && q.X >= -padPixels && q.Y >= -padPixels
               && q.X <= Calibration.Columns + padPixels && q.Y <= Calibration.Rows + padPixels;
    }

    /// <summary>The ray through a pixel, in world coordinates (origin, unit direction).</summary>
    public (Vec3 Origin, Vec3 Direction) Ray(Vec2 px)
    {
        var n = Undistort(px);
        var dir = Pose.Rotation * new Vec3(n.X, n.Y, 1).Normalized();
        return (Pose.Translation, dir);
    }
}
