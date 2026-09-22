namespace Cozmo.Robot.Vision;

/// <summary>A 3-vector in double precision. Millimetres for positions.</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vec3 operator *(double s, Vec3 a) => a * s;
    public static Vec3 operator /(Vec3 a, double s) => new(a.X / s, a.Y / s, a.Z / s);
    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
    public Vec3 Cross(Vec3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double Length => Math.Sqrt(Dot(this));
    public Vec3 Normalized() { var l = Length; return l > 0 ? this / l : this; }
    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}

/// <summary>A 2-vector: image coordinates in pixels, or a point on a plane.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSq => X * X + Y * Y;
    public override string ToString() => $"({X:F1}, {Y:F1})";
}

/// <summary>A 3x3 rotation matrix (row-major), the engine's <c>Anki::RotationMatrix3d</c>.</summary>
public readonly struct Mat3
{
    private readonly double[] _m;

    public Mat3(double m00, double m01, double m02, double m10, double m11, double m12, double m20, double m21, double m22)
        => _m = new[] { m00, m01, m02, m10, m11, m12, m20, m21, m22 };

    public Mat3(ReadOnlySpan<double> rowMajor)
    {
        if (rowMajor.Length != 9) throw new ArgumentException("9 values", nameof(rowMajor));
        _m = rowMajor.ToArray();
    }

    public static readonly Mat3 Identity = new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    public double this[int r, int c] => _m[r * 3 + c];

    public Vec3 Row(int r) => new(_m[r * 3], _m[r * 3 + 1], _m[r * 3 + 2]);
    public Vec3 Column(int c) => new(_m[c], _m[3 + c], _m[6 + c]);

    public static Vec3 operator *(Mat3 m, Vec3 v) => new(
        m._m[0] * v.X + m._m[1] * v.Y + m._m[2] * v.Z,
        m._m[3] * v.X + m._m[4] * v.Y + m._m[5] * v.Z,
        m._m[6] * v.X + m._m[7] * v.Y + m._m[8] * v.Z);

    public static Mat3 operator *(Mat3 a, Mat3 b)
    {
        var r = new double[9];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                r[i * 3 + j] = a._m[i * 3] * b._m[j] + a._m[i * 3 + 1] * b._m[3 + j] + a._m[i * 3 + 2] * b._m[6 + j];
        return new Mat3(r);
    }

    public Mat3 Transposed() => new(_m[0], _m[3], _m[6], _m[1], _m[4], _m[7], _m[2], _m[5], _m[8]);

    /// <summary>Rodrigues: rotation of <paramref name="angleRad"/> about a unit <paramref name="axis"/>.</summary>
    public static Mat3 AxisAngle(Vec3 axis, double angleRad)
    {
        var a = axis.Normalized();
        double c = Math.Cos(angleRad), s = Math.Sin(angleRad), t = 1 - c;
        return new Mat3(
            t * a.X * a.X + c, t * a.X * a.Y - s * a.Z, t * a.X * a.Z + s * a.Y,
            t * a.X * a.Y + s * a.Z, t * a.Y * a.Y + c, t * a.Y * a.Z - s * a.X,
            t * a.X * a.Z - s * a.Y, t * a.Y * a.Z + s * a.X, t * a.Z * a.Z + c);
    }

    public static Mat3 AboutX(double a) => AxisAngle(new Vec3(1, 0, 0), a);
    public static Mat3 AboutY(double a) => AxisAngle(new Vec3(0, 1, 0), a);
    public static Mat3 AboutZ(double a) => AxisAngle(new Vec3(0, 0, 1), a);

    /// <summary>Rotation vector (axis * angle) to matrix; the engine's <c>RotationVector3d</c> constructor.</summary>
    public static Mat3 FromRotationVector(Vec3 rv)
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

    /// <summary>The engine's <c>Rotation3d::GetAngleAroundZaxis</c>: atan2 of the rotated X axis.</summary>
    public double AngleAroundZ => Math.Atan2(_m[3], _m[0]);

    /// <summary>Angle in radians between this rotation and another (the geodesic distance).</summary>
    public double AngularDistance(Mat3 other) => (Transposed() * other).ToRotationVector().Length;

    /// <summary>Re-orthonormalise after accumulated numeric error (Gram-Schmidt on the columns).</summary>
    public Mat3 Orthonormalized()
    {
        var c0 = Column(0).Normalized();
        var c1 = (Column(1) - c0 * c0.Dot(Column(1))).Normalized();
        var c2 = c0.Cross(c1);
        return new Mat3(c0.X, c1.X, c2.X, c0.Y, c1.Y, c2.Y, c0.Z, c1.Z, c2.Z);
    }

    public override string ToString() => $"[{Row(0)}; {Row(1)}; {Row(2)}]";
}

/// <summary>
/// A rigid transform: the engine's <c>Anki::Pose3d</c> without the parent chain. <c>p_world = R * p_local + T</c>.
/// Poses in this stack are all expressed with respect to the robot's world origin, so the parent chain the
/// engine keeps (<c>PoseBase::GetWithRespectTo</c>) reduces to plain composition.
/// </summary>
public readonly record struct Pose3d(Mat3 Rotation, Vec3 Translation)
{
    public static readonly Pose3d Identity = new(Mat3.Identity, Vec3.Zero);

    public Pose3d(double angleRad, Vec3 axis, Vec3 translation) : this(Mat3.AxisAngle(axis, angleRad), translation) { }

    /// <summary>Apply: local point to parent frame.</summary>
    public Vec3 Apply(Vec3 p) => Rotation * p + Translation;

    /// <summary>this ∘ other: <c>other</c> expressed in this pose's parent frame.</summary>
    public Pose3d Compose(Pose3d other) => new(Rotation * other.Rotation, Rotation * other.Translation + Translation);

    public Pose3d Inverse()
    {
        var rt = Rotation.Transposed();
        return new Pose3d(rt, -(rt * Translation));
    }

    /// <summary>The engine's <c>GetWithRespectTo</c> for two poses sharing a root: this pose in <paramref name="frame"/>.</summary>
    public Pose3d WithRespectTo(Pose3d frame) => frame.Inverse().Compose(this);

    /// <summary>Angle of the pose's X axis about Z, the robot's heading convention.</summary>
    public double AngleAroundZ => Rotation.AngleAroundZ;

    /// <summary>
    /// The engine's <c>Pose3d::IsSameAs(other, distThreshold, angleThreshold)</c>: translation within the
    /// threshold on every axis and rotation within the angle.
    /// </summary>
    public bool IsSameAs(Pose3d other, double distThresholdMm, double angleThresholdRad)
    {
        var d = Translation - other.Translation;
        if (Math.Abs(d.X) > distThresholdMm || Math.Abs(d.Y) > distThresholdMm || Math.Abs(d.Z) > distThresholdMm) return false;
        return Rotation.AngularDistance(other.Rotation) <= angleThresholdRad;
    }

    public override string ToString() => $"T={Translation} yaw={AngleAroundZ * 180 / Math.PI:F1}deg";
}
