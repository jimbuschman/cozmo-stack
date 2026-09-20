namespace Cozmo.Robot.Vision;

/// <summary>
/// Object pose from point correspondences, standing in for the <c>cv::solvePnP</c> call inside
/// <c>Vision::Camera::ComputeObjectPoseHelper</c>. LOCAL numerics, NATIVE role: the engine hands solvePnP the
/// marker's 3-D corners on the object and the observed 2-D corners and takes back the object pose in the
/// camera frame. Here: a planar homography decomposition for the initial estimate, then Gauss-Newton on the
/// pixel reprojection error over all correspondences (which may span several markers of one object).
/// </summary>
public static class PoseEstimation
{
    public sealed record Result(Pose3d ObjectInCamera, double RmsReprojectionPx, int Iterations);

    /// <summary>
    /// Solves for the pose of the frame the <paramref name="objectPoints"/> are expressed in, relative to the camera.
    /// Needs at least 4 points; the initial estimate assumes they are (nearly) coplanar, which marker corners are.
    /// </summary>
    public static Result? Solve(CameraModel cam, IReadOnlyList<Vec3> objectPoints, IReadOnlyList<Vec2> imagePoints)
    {
        if (objectPoints.Count != imagePoints.Count || objectPoints.Count < 4) return null;
        var init = InitialFromPlane(cam, objectPoints, imagePoints);
        if (init is null) return null;
        var (pose, iters) = Refine(cam, objectPoints, imagePoints, init.Value);
        double rms = Rms(cam, objectPoints, imagePoints, pose);
        return new Result(pose, rms, iters);
    }

    /// <summary>Planar initialisation: object points onto a plane basis, homography to normalised image coordinates, decomposition.</summary>
    private static Pose3d? InitialFromPlane(CameraModel cam, IReadOnlyList<Vec3> obj, IReadOnlyList<Vec2> img)
    {
        var o = obj[0];
        Vec3 e1 = Vec3.Zero, e2 = Vec3.Zero;
        for (int i = 1; i < obj.Count && e1.Length < 1e-9; i++) e1 = (obj[i] - o).Normalized();
        for (int i = 1; i < obj.Count && e2.Length < 1e-9; i++)
        {
            var d = obj[i] - o;
            var perp = d - e1 * e1.Dot(d);
            if (perp.Length > 1e-6 * Math.Max(1, d.Length)) e2 = perp.Normalized();
        }
        if (e1.Length < 1e-9 || e2.Length < 1e-9) return null;
        var e3 = e1.Cross(e2);

        var plane = new Vec2[obj.Count];
        var norm = new Vec2[obj.Count];
        for (int i = 0; i < obj.Count; i++)
        {
            var d = obj[i] - o;
            plane[i] = new Vec2(d.Dot(e1), d.Dot(e2));
            norm[i] = cam.Undistort(img[i]);
        }
        Homography h;
        try { h = Homography.FromPoints(plane, norm); } catch (InvalidOperationException) { return null; }
        var m = h.H;
        var h1 = new Vec3(m[0], m[3], m[6]); var h2 = new Vec3(m[1], m[4], m[7]); var h3 = new Vec3(m[2], m[5], m[8]);
        double lambda = Math.Sqrt(h1.Length * h2.Length);
        if (lambda < 1e-12) return null;
        var r1 = h1 / lambda; var r2 = h2 / lambda; var t = h3 / lambda;
        if (t.Z < 0) { r1 = -r1; r2 = -r2; t = -t; }
        var r3 = r1.Cross(r2);
        var rPlane = new Mat3(r1.X, r2.X, r3.X, r1.Y, r2.Y, r3.Y, r1.Z, r2.Z, r3.Z).Orthonormalized();
        // camera <- plane basis, plane basis <- object: object point p = o + B (a,b,c)  =>  a,b,c = B^T (p - o)
        var bT = new Mat3(e1.X, e1.Y, e1.Z, e2.X, e2.Y, e2.Z, e3.X, e3.Y, e3.Z);   // rows e1,e2,e3 = B^T
        var rot = rPlane * bT;
        var trans = t - rot * o;
        return new Pose3d(rot, trans);
    }

    private static (Pose3d Pose, int Iterations) Refine(CameraModel cam, IReadOnlyList<Vec3> obj, IReadOnlyList<Vec2> img, Pose3d start)
    {
        var pose = start;
        double lambda = 1e-3;
        double err = Sse(cam, obj, img, pose);
        int it = 0;
        for (; it < 30; it++)
        {
            int n = obj.Count;
            var jac = new double[2 * n, 6];
            var res = new double[2 * n];
            for (int i = 0; i < n; i++)
            {
                var p = Project(cam, pose, obj[i]);
                res[2 * i] = img[i].X - p.X; res[2 * i + 1] = img[i].Y - p.Y;
                for (int k = 0; k < 6; k++)
                {
                    double eps = k < 3 ? 1e-6 : 1e-3;
                    var pp = Perturb(pose, k, eps);
                    var q = Project(cam, pp, obj[i]);
                    jac[2 * i, k] = (q.X - p.X) / eps; jac[2 * i + 1, k] = (q.Y - p.Y) / eps;
                }
            }
            // Levenberg-Marquardt step
            var jtj = new double[6, 6]; var jtr = new double[6];
            for (int a = 0; a < 6; a++)
            {
                for (int b = 0; b < 6; b++) { double s = 0; for (int r = 0; r < 2 * n; r++) s += jac[r, a] * jac[r, b]; jtj[a, b] = s; }
                double t = 0; for (int r = 0; r < 2 * n; r++) t += jac[r, a] * res[r]; jtr[a] = t;
            }
            bool improved = false;
            for (int tries = 0; tries < 8 && !improved; tries++)
            {
                var damped = (double[,])jtj.Clone();
                for (int a = 0; a < 6; a++) damped[a, a] *= 1 + lambda;
                double[] delta;
                try { delta = LinearAlgebra.Solve(damped, jtr); } catch (InvalidOperationException) { lambda *= 10; continue; }
                var candidate = Apply(pose, delta);
                double e2 = Sse(cam, obj, img, candidate);
                if (e2 < err) { pose = candidate; err = e2; lambda = Math.Max(1e-9, lambda / 3); improved = true; if (delta.Sum(d => Math.Abs(d)) < 1e-9) return (pose, it + 1); }
                else lambda *= 10;
            }
            if (!improved) break;
            if (err < 1e-8) break;
        }
        return (pose, it);
    }

    private static Pose3d Perturb(Pose3d p, int k, double eps)
    {
        var d = new double[6]; d[k] = eps;
        return Apply(p, d);
    }

    /// <summary>Left-multiplicative rotation update and additive translation.</summary>
    private static Pose3d Apply(Pose3d p, double[] d)
    {
        var dR = Mat3.FromRotationVector(new Vec3(d[0], d[1], d[2]));
        return new Pose3d(dR * p.Rotation, p.Translation + new Vec3(d[3], d[4], d[5]));
    }

    private static Vec2 Project(CameraModel cam, Pose3d objectInCamera, Vec3 p)
    {
        var c = objectInCamera.Apply(p);
        if (c.Z < 1e-6) c = c with { Z = 1e-6 };
        return cam.Distort(new Vec2(c.X / c.Z, c.Y / c.Z));
    }

    private static double Sse(CameraModel cam, IReadOnlyList<Vec3> obj, IReadOnlyList<Vec2> img, Pose3d pose)
    {
        double s = 0;
        for (int i = 0; i < obj.Count; i++) { var d = Project(cam, pose, obj[i]) - img[i]; s += d.X * d.X + d.Y * d.Y; }
        return s;
    }

    private static double Rms(CameraModel cam, IReadOnlyList<Vec3> obj, IReadOnlyList<Vec2> img, Pose3d pose)
        => Math.Sqrt(Sse(cam, obj, img, pose) / obj.Count);
}
