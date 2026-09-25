using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

/// <summary>
/// The engine's <c>Anki::Vision::CameraCalibration</c>, persisted on the robot as the CLAD struct
/// <c>CameraCalibration</c> (decompiled Unity <c>Anki.Cozmo.CameraCalibration</c>): focal lengths, principal
/// point, skew, image size and eight OpenCV distortion coefficients (k1, k2, p1, p2, k3, k4, k5, k6).
///
/// The engine never ships default numbers: on <c>RobotConnectionResponse</c> (0x006583EE) it reads NV entry
/// <see cref="NvEntryTag"/> through <c>NVStorageComponent::Read</c> and <c>VisionSystem::Update</c> refuses to
/// run until a calibration is set ("Must be initialized and have calibrated camera to Update", 0xC016DC).
/// This stack does the same: no calibration, no localisation. <see cref="Nominal"/> exists for offline
/// tests and tools only and is labelled as such.
/// </summary>
public sealed record CameraCalibration
{
    /// <summary><c>NVEntry_CameraCalib</c> = 0x80000001, the tag the engine reads on connection (NATIVE).</summary>
    public const uint NvEntryTag = 0x80000001;

    /// <summary>Wire size of the CLAD struct: 5 floats, 2 u16, 8 floats.</summary>
    public const int WireSize = 5 * 4 + 2 * 2 + 8 * 4;

    public required double FocalLengthX { get; init; }
    public required double FocalLengthY { get; init; }
    public required double CenterX { get; init; }
    public required double CenterY { get; init; }
    public double Skew { get; init; }
    public required int Rows { get; init; }
    public required int Columns { get; init; }
    /// <summary>k1, k2, p1, p2, k3, k4, k5, k6 in OpenCV's order. All zero means no distortion.</summary>
    public double[] DistortionCoefficients { get; init; } = new double[8];

    /// <summary>Where these numbers came from, so a log line can say whether they are the robot's own.</summary>
    public string Provenance { get; init; } = "unspecified";

    /// <summary>
    /// Parses the CLAD struct as the robot returns it in <see cref="NVOpResult.Data"/>.
    /// </summary>
    public static CameraCalibration Parse(ReadOnlySpan<byte> data, string provenance = "robot NV storage")
    {
        if (data.Length < WireSize) throw new FormatException($"camera calibration needs {WireSize} bytes, got {data.Length}");
        var r = new CladReader(data.ToArray().AsMemory());
        float fx = r.F32(), fy = r.F32(), cx = r.F32(), cy = r.F32(), skew = r.F32();
        int rows = r.U16(), cols = r.U16();
        var dist = new double[8];
        for (int i = 0; i < 8; i++) dist[i] = r.F32();
        if (fx <= 0 || fy <= 0 || rows <= 0 || cols <= 0 || !float.IsFinite(fx) || !float.IsFinite(fy))
            throw new FormatException($"camera calibration is not plausible: fx={fx} fy={fy} {cols}x{rows}");
        return new CameraCalibration
        {
            FocalLengthX = fx, FocalLengthY = fy, CenterX = cx, CenterY = cy, Skew = skew,
            Rows = rows, Columns = cols, DistortionCoefficients = dist, Provenance = provenance,
        };
    }

    // fidelity: M3-022
    /// <summary>
    /// The NV callback's unpack (1j): the CLAD struct read as it stands, with no plausibility check of its own. The
    /// size check (56 bytes) is the caller's (<see cref="Cozmo.Robot.CameraSettings"/>).
    /// </summary>
    public static CameraCalibration Unpack(ReadOnlySpan<byte> data, string provenance = "robot NV storage")
    {
        var r = new CladReader(data.ToArray().AsMemory());
        float fx = r.F32(), fy = r.F32(), cx = r.F32(), cy = r.F32(), skew = r.F32();
        int rows = r.U16(), cols = r.U16();
        var dist = new double[8];
        for (int i = 0; i < 8; i++) dist[i] = r.F32();
        return new CameraCalibration
        {
            FocalLengthX = fx, FocalLengthY = fy, CenterX = cx, CenterY = cy, Skew = skew,
            Rows = rows, Columns = cols, DistortionCoefficients = dist, Provenance = provenance,
        };
    }

    /// <summary>Serialises to the CLAD layout (used by tests and by the fake robot).</summary>
    public byte[] ToBytes()
    {
        var w = new CladWriter();
        w.F32((float)FocalLengthX); w.F32((float)FocalLengthY); w.F32((float)CenterX); w.F32((float)CenterY); w.F32((float)Skew);
        w.U16((ushort)Rows); w.U16((ushort)Columns);
        for (int i = 0; i < 8; i++) w.F32((float)(i < DistortionCoefficients.Length ? DistortionCoefficients[i] : 0));
        return w.ToArray();
    }

    /// <summary>Whether any distortion coefficient is non-zero.</summary>
    public bool HasDistortion => DistortionCoefficients.Any(c => c != 0);

    /// <summary>Horizontal field of view in radians, from the focal length and width.</summary>
    public double HorizontalFovRad => 2 * Math.Atan2(Columns / 2.0, FocalLengthX);
    public double VerticalFovRad => 2 * Math.Atan2(Rows / 2.0, FocalLengthY);

    /// <summary>
    /// A calibration scaled to another image size (the engine's <c>CameraCalibration::GetScaled</c>): the
    /// robot's stored calibration is for the full 320x240 frame.
    /// </summary>
    public CameraCalibration Scaled(int columns, int rows)
    {
        double sx = (double)columns / Columns, sy = (double)rows / Rows;
        return this with
        {
            FocalLengthX = FocalLengthX * sx, FocalLengthY = FocalLengthY * sy,
            CenterX = CenterX * sx, CenterY = CenterY * sy, Skew = Skew * sx, Rows = rows, Columns = columns,
        };
    }

    /// <summary>
    /// A stand-in for offline tests and tools: a 320x240 pinhole camera with a 290 px focal length (about a
    /// 58 degree horizontal field of view). LOCAL_POLICY: the number comes from no Anki source and is never
    /// used for a real robot, which must supply its own through <see cref="NvEntryTag"/>.
    /// </summary>
    public static CameraCalibration Nominal(int columns = 320, int rows = 240) => new()
    {
        FocalLengthX = 290.0 * columns / 320, FocalLengthY = 290.0 * rows / 240,
        CenterX = columns / 2.0, CenterY = rows / 2.0, Skew = 0, Rows = rows, Columns = columns,
        Provenance = "nominal (LOCAL_POLICY stand-in for offline use; not the robot's)",
    };

    public override string ToString() =>
        $"fx={FocalLengthX:F1} fy={FocalLengthY:F1} c=({CenterX:F1},{CenterY:F1}) {Columns}x{Rows} " +
        $"dist=[{string.Join(",", DistortionCoefficients.Select(d => d.ToString("G4")))}] ({Provenance})";
}
