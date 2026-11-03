namespace Cozmo.Robot.Vision;

/// <summary>
/// Object pose from point correspondences: the stack's side of <c>Vision::Camera::ComputeObjectPoseHelper</c>. The numbers come from the
/// port of the shipped <c>cv::solvePnP</c> (<see cref="NativeSolvePnP"/>); this class is the engine's call around it: the corners narrowed
/// to Point3f and Point2f in input order (0x0085DF98..0x0085E0BD), K and the distortion vector as binary32 (0x0085D77E..0x0085D89E), and each
/// output component narrowed to binary32 before the pose is built (vcvt.f32.f64 at 0x0085D8AC..0x0085D8E2; RotationVector3d 0x4B2538, Pose3d 0x4B2544).
/// The engine tests neither the solver's return value nor any residual on this path (rows E05, E06, Q03).
/// </summary>
// fidelity: M11-099
public static class PoseEstimation
{
    /// <summary>
    /// <see cref="RmsReprojectionPx"/> is a stack diagnostic, not an engine value: the reprojection error of the solver's own double
    /// output, which BlockWorld's existing gates read. <see cref="Iterations"/> is the LM iteration count.
    /// </summary>
    public sealed record Result(Pose3d ObjectInCamera, double RmsReprojectionPx, int Iterations);

    /// <summary>
    /// Solves for the pose of the frame the <paramref name="objectPoints"/> are expressed in, relative to the camera. Null for a count
    /// mismatch or fewer than 4 points (the shipped solver asserts on the first and solves the second; no stack caller passes either),
    /// and for a planar set above four points, whose homography refinement has no inventory row (MISSING, reported).
    /// </summary>
    public static Result? Solve(CameraModel cam, IReadOnlyList<Vec3> objectPoints, IReadOnlyList<Vec2> imagePoints)
    {
        if (objectPoints.Count != imagePoints.Count || objectPoints.Count < 4) return null;
        int n = objectPoints.Count;
        var obj = new float[n * 3]; var img = new float[n * 2];
        for (int i = 0; i < n; i++)
        {
            obj[i * 3] = (float)objectPoints[i].X; obj[i * 3 + 1] = (float)objectPoints[i].Y; obj[i * 3 + 2] = (float)objectPoints[i].Z;
            img[i * 2] = (float)imagePoints[i].X; img[i * 2 + 1] = (float)imagePoints[i].Y;
        }
        var cal = cam.Calibration;
        // GetCalibrationMatrix<float>: [fx, skew, cx; 0, fy, cy; 0, 0, 1]. Neither cvUndistortPoints nor cvProjectPoints2 reads the skew (U04, P03).
        float[] k = { (float)cal.FocalLengthX, (float)cal.Skew, (float)cal.CenterX, 0f, (float)cal.FocalLengthY, (float)cal.CenterY, 0f, 0f, 1f };
        var dist = Array.ConvertAll(cal.DistortionCoefficients, c => (float)c);
        NativeSolvePnP.Output solved;
        try { solved = NativeSolvePnP.Solve(obj, img, k, dist); }
        catch (NotSupportedException e) { PoseDiagnostics.Missing(e.Message); return null; }
        double[] r = solved.Rvec, t = solved.Tvec;
        var pose = Pose3d.FromObservedPoseInCamera(Rotation3d.FromVector(new Vec3((float)r[0], (float)r[1], (float)r[2])), new Vec3((float)t[0], (float)t[1], (float)t[2]));
        return new Result(pose, Rms(cam, objectPoints, imagePoints, solved), solved.Iterations);
    }

    private static double Rms(CameraModel cam, IReadOnlyList<Vec3> obj, IReadOnlyList<Vec2> img, NativeSolvePnP.Output solved)
    {
        var r = NativeSolvePnP.RotationMatrix(solved.Rvec);
        double[] t = solved.Tvec;
        double sum = 0;
        for (int i = 0; i < obj.Count; i++)
        {
            var p = obj[i];
            double x = r[0] * p.X + r[1] * p.Y + r[2] * p.Z + t[0];
            double y = r[3] * p.X + r[4] * p.Y + r[5] * p.Z + t[1];
            double z = r[6] * p.X + r[7] * p.Y + r[8] * p.Z + t[2];
            if (z < 1e-6) z = 1e-6;
            var d = cam.Distort(new Vec2(x / z, y / z)) - img[i];
            sum += d.X * d.X + d.Y * d.Y;
        }
        return Math.Sqrt(sum / obj.Count);
    }
}
