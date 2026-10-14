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
    // MISSING: M11-054 native solvePnP remains unbuilt. Keep the existing LOCAL double estimator
    // independent of engine RotationVector/Radians zero thresholds and f32 pose storage.
    // These are its prior numerical candidates, not a second engine pose implementation.
    private readonly record struct LocalPose(LocalMatrix Rotation,Vec3 Translation)
    {
        internal LocalPose Value=>this;
        internal Vec3 Apply(Vec3 p)=>Rotation*p+Translation;
    }

    public sealed record Result(Pose3d ObjectInCamera, double RmsReprojectionPx, int Iterations);

    /// <summary>
    /// Solves for the pose of the frame the <paramref name="objectPoints"/> are expressed in, relative to the camera.
    /// Needs at least 4 points; the initial estimate assumes they are (nearly) coplanar, which marker corners are.
    /// </summary>
    public static Result? Solve(CameraModel cam, IReadOnlyList<Vec3> objectPoints, IReadOnlyList<Vec2> imagePoints)
    {
        if (objectPoints.Count != imagePoints.Count || objectPoints.Count < 4) return null;
        PoseDiagnostics.Missing("M11-054 native solvePnP remains unbuilt; existing local double estimator retained behind the engine pose output boundary");
        var init = InitialFromPlane(cam, objectPoints, imagePoints);
        if (init is null) return null;
        var (pose, iters) = Refine(cam, objectPoints, imagePoints, init.Value);
        double rms = Rms(cam, objectPoints, imagePoints, pose);
        var rows=Enumerable.Range(0,9).Select(i=>pose.Rotation[i/3,i%3]).ToArray();
        return new Result(new Pose3d(new Mat3(rows),pose.Translation),rms,iters);
    }

    /// <summary>Planar initialisation: object points onto a plane basis, homography to normalised image coordinates, decomposition.</summary>
    private static LocalPose? InitialFromPlane(CameraModel cam, IReadOnlyList<Vec3> obj, IReadOnlyList<Vec2> img)
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
        var rPlane = new LocalMatrix(r1.X, r2.X, r3.X, r1.Y, r2.Y, r3.Y, r1.Z, r2.Z, r3.Z).Orthonormalized();
        // camera <- plane basis, plane basis <- object: object point p = o + B (a,b,c)  =>  a,b,c = B^T (p - o)
        var bT = new LocalMatrix(e1.X, e1.Y, e1.Z, e2.X, e2.Y, e2.Z, e3.X, e3.Y, e3.Z);   // rows e1,e2,e3 = B^T
        var rot = rPlane * bT;
        var trans = t - rot * o;
        return new LocalPose(rot, trans);
    }

    private static (LocalPose Pose, int Iterations) Refine(CameraModel cam, IReadOnlyList<Vec3> obj, IReadOnlyList<Vec2> img, LocalPose start)
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

    private static LocalPose Perturb(LocalPose p, int k, double eps)
    {
        var d = new double[6]; d[k] = eps;
        return Apply(p, d);
    }

    /// <summary>Left-multiplicative rotation update and additive translation.</summary>
    private static LocalPose Apply(LocalPose p, double[] d)
    {
        var dR = LocalMatrix.FromRotationVector(new Vec3(d[0], d[1], d[2]));
        return new LocalPose(dR * p.Rotation, p.Translation + new Vec3(d[3], d[4], d[5]));
    }

    private static Vec2 Project(CameraModel cam, LocalPose objectInCamera, Vec3 p)
    {
        var c = objectInCamera.Apply(p);
        if (c.Z < 1e-6) c = c with { Z = 1e-6 };
        return cam.Distort(new Vec2(c.X / c.Z, c.Y / c.Z));
    }

    private static double Sse(CameraModel cam, IReadOnlyList<Vec3> obj, IReadOnlyList<Vec2> img, LocalPose pose)
    {
        double s = 0;
        for (int i = 0; i < obj.Count; i++) { var d = Project(cam, pose, obj[i]) - img[i]; s += d.X * d.X + d.Y * d.Y; }
        return s;
    }

    private static double Rms(CameraModel cam, IReadOnlyList<Vec3> obj, IReadOnlyList<Vec2> img, LocalPose pose)
        => Math.Sqrt(Sse(cam, obj, img, pose) / obj.Count);
    // Original estimator-only matrix arithmetic retained from the pre-job candidate.
    private readonly struct LocalMatrix
    {
        private readonly double[] _m;

        public LocalMatrix(double m00, double m01, double m02, double m10, double m11, double m12, double m20, double m21, double m22)
            => _m = new[] { m00, m01, m02, m10, m11, m12, m20, m21, m22 };

        public LocalMatrix(ReadOnlySpan<double> rowMajor)
        {
            if (rowMajor.Length != 9) throw new ArgumentException("9 values", nameof(rowMajor));
            _m = rowMajor.ToArray();
        }

        public static readonly LocalMatrix Identity = new(1, 0, 0, 0, 1, 0, 0, 0, 1);

        public double this[int r, int c] => _m[r * 3 + c];

        public Vec3 Row(int r) => new(_m[r * 3], _m[r * 3 + 1], _m[r * 3 + 2]);
        public Vec3 Column(int c) => new(_m[c], _m[3 + c], _m[6 + c]);

        public static Vec3 operator *(LocalMatrix m, Vec3 v) => new(
            m._m[0] * v.X + m._m[1] * v.Y + m._m[2] * v.Z,
            m._m[3] * v.X + m._m[4] * v.Y + m._m[5] * v.Z,
            m._m[6] * v.X + m._m[7] * v.Y + m._m[8] * v.Z);

        public static LocalMatrix operator *(LocalMatrix a, LocalMatrix b)
        {
            var r = new double[9];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    r[i * 3 + j] = a._m[i * 3] * b._m[j] + a._m[i * 3 + 1] * b._m[3 + j] + a._m[i * 3 + 2] * b._m[6 + j];
            return new LocalMatrix(r);
        }

        public LocalMatrix Transposed() => new(_m[0], _m[3], _m[6], _m[1], _m[4], _m[7], _m[2], _m[5], _m[8]);

        /// <summary>Rodrigues: rotation of <paramref name="angleRad"/> about a unit <paramref name="axis"/>.</summary>
        public static LocalMatrix AxisAngle(Vec3 axis, double angleRad)
        {
            var a = axis.Normalized();
            double c = Math.Cos(angleRad), s = Math.Sin(angleRad), t = 1 - c;
            return new LocalMatrix(
                t * a.X * a.X + c, t * a.X * a.Y - s * a.Z, t * a.X * a.Z + s * a.Y,
                t * a.X * a.Y + s * a.Z, t * a.Y * a.Y + c, t * a.Y * a.Z - s * a.X,
                t * a.X * a.Z - s * a.Y, t * a.Y * a.Z + s * a.X, t * a.Z * a.Z + c);
        }

        public static LocalMatrix AboutX(double a) => AxisAngle(new Vec3(1, 0, 0), a);
        public static LocalMatrix AboutY(double a) => AxisAngle(new Vec3(0, 1, 0), a);
        public static LocalMatrix AboutZ(double a) => AxisAngle(new Vec3(0, 0, 1), a);

        /// <summary>Local estimator rotation-vector candidate, preserving its finite-difference step.</summary>
        public static LocalMatrix FromRotationVector(Vec3 rv)
        {
            double angle = rv.Length;
            return angle < 1e-12 ? Identity : AxisAngle(rv / angle, angle);
        }

        /// <summary>Inverse of <see cref="FromRotationVector"/>.</summary>
        public Vec3 ToRotationVector()
        {
            double tr = _m[0] + _m[4] + _m[8];
            double cos = Math.Clamp((tr - 1) / 2, -1, 1);
            double angle = Math.Acos(cos);
            if (angle < 1e-12) return Vec3.Zero;
            var axis = new Vec3(_m[7] - _m[5], _m[2] - _m[6], _m[3] - _m[1]);
            if (Math.PI - angle < 1e-6)
            {
                // near pi the antisymmetric part vanishes; take the axis from the symmetric part
                var d = new Vec3(Math.Sqrt(Math.Max(0, (_m[0] + 1) / 2)), Math.Sqrt(Math.Max(0, (_m[4] + 1) / 2)), Math.Sqrt(Math.Max(0, (_m[8] + 1) / 2)));
                if (_m[1] < 0) d = d with { Y = -d.Y };
                if (_m[2] < 0) d = d with { Z = -d.Z };
                return d.Normalized() * angle;
            }
            return axis.Normalized() * angle;
        }

        /// <summary>Local estimator yaw candidate.</summary>
        public double AngleAroundZ => Math.Atan2(_m[3], _m[0]);

        /// <summary>Angle in radians between this rotation and another (the geodesic distance).</summary>
        public double AngularDistance(LocalMatrix other) => (Transposed() * other).ToRotationVector().Length;

        /// <summary>Re-orthonormalise after accumulated numeric error (Gram-Schmidt on the columns).</summary>
        public LocalMatrix Orthonormalized()
        {
            var c0 = Column(0).Normalized();
            var c1 = (Column(1) - c0 * c0.Dot(Column(1))).Normalized();
            var c2 = c0.Cross(c1);
            return new LocalMatrix(c0.X, c1.X, c2.X, c0.Y, c1.Y, c2.Y, c0.Z, c1.Z, c2.Z);
        }

        public override string ToString() => $"[{Row(0)}; {Row(1)}; {Row(2)}]";
    }

}
