using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
namespace Cozmo.Robot.Vision;

// fidelity: M11-013 — manager-corrected OV85, 0x00876DA8..0x00876FF8.
public sealed partial class ObservableObject
{
    internal readonly Dictionary<short,List<KnownMarker>> MarkerCodeIndex = new();
    internal void SetMarkerAsObserved(ObservedMarker observed, uint timestamp, float tolerance, CameraModel camera,
        SourceLogSink? source = null, Func<short,string?>? getCodeName = null)
    {
        using var relative = new Pose3d("");
        if (observed.Corners.Length < 4)
        { SteppedBehavior.ReportMissing("M11-013 SetMarkerAsObserved: native observed quad requires four corners; incomplete model input withheld"); return; }
        var centroid = MarkerCentroid(observed.Corners);
        float observedArea = MarkerQuadArea(observed.Corners);
        short code = unchecked((short)observed.Code);
        void Warning(bool unknownCode)
        {
            string? name = getCodeName?.Invoke(code);
            if (name is null)
            {
                SteppedBehavior.ReportMissing("M11-013 SetMarkerAsObserved warning argument GetCodeName0x0087E1D0 -> GetNameForCode0x0087E0D4 has no checked/bound recipient; no Enum.ToString or invented name/native warning substituted");
                return;
            }
            source?.Invoke(EngineLogLevel.Warning, null, unknownCode ? "ObservableObject.SetMarkerAsObserved" : "ObservableObject.SetMarkerAsObserved.NoMatchingMarkerFound",
                unknownCode ? FormattableString.Invariant($"No markers found with code ({code}:{name}) of given 'nearestTo' observed marker") : "Marker:" + name);
        }
        if (!MarkerCodeIndex.TryGetValue(code, out var entries)) { Warning(true); return; }
        if (entries.Count == 0) { Warning(false); return; }
        float inverse = 1f / observedArea, best = BitConverter.UInt32BitsToSingle(0x7F7FFFFF);
        KnownMarker? bestMarker = null; bool unknownPose = false;
        foreach (var marker in entries)
        {
            // m+0x28 is its Camera; Camera+0xC is the pose at m+0x34 (M14-001).
            // The legacy flat adapter cannot supply native shared-parent identity. Do not turn that into a false frame gate.
            if (!Pose3d.GetWithRespectTo(marker.PoseOnObject, camera.Pose, relative))
            {
                if (marker.PoseOnObject.HasLegacyFrameInput || camera.Pose.HasLegacyFrameInput) unknownPose = true;
                continue;
            }
            float width = (float)marker.SizeMm, height = (float)(double.IsNaN(marker.HeightMm) ? marker.SizeMm : marker.HeightMm);
            var projected = new Vec2[4];
            for (int i = 0; i < 4; i++)
            {
                var c = KnownMarker.CanonicalCorners[i];
                var point = relative.Apply(new Vec3((float)c.X * width, 0f, (float)c.Z * height));
                if (!((float)point.Z <= 0f)) // including NaN: only this branch reads calibration/distortion.
                {
                    if (camera.Calibration is null)
                    { SteppedBehavior.ReportMissing("M11-013/M11-042 SetMarkerAsObserved projection0x00876E84: null calibration recipient/crash boundary is not established; selection withheld"); return; }
                    int coefficients = camera.Calibration.DistortionCoefficients.Length;
                    if (coefficients is > 0 and < 4)
                    {
                        StackDiagnostics.Write("M1-034/M11-013: skip marker selection, preserve timestamps; Camera::Project3dPoint heap read beyond1..3 coefficients at0x0085E2AE..0x0085E33A");
                        return;
                    }
                }
                ProjectPoint(camera.Calibration, (float)point.X, (float)point.Y, (float)point.Z, out float x, out float y);
                projected[i] = new Vec2(x,y);
            }
            var quad = new Quadrilateral(new((float)projected[0].X,(float)projected[0].Y), new((float)projected[1].X,(float)projected[1].Y),
                new((float)projected[2].X,(float)projected[2].Y), new((float)projected[3].X,(float)projected[3].Y));
            if (!quad.Contains(new(centroid.X,centroid.Y))) continue;
            var center = MarkerCentroid(projected);
            float dx = center.X - centroid.X, dy = center.Y - centroid.Y;
            float squared = dx * dx; squared += dy * dy;
            if (!(squared < best)) continue; // equality/unordered skip, before area computation
            float ratio = inverse * MarkerQuadArea(projected); ratio += -1f;
            if (ratio < 0f) ratio = -ratio; // native MI; NaN is not changed and fails the next compare
            if (!(ratio < tolerance)) continue;
            best = squared; bestMarker = marker;
        }
        if (unknownPose)
        {
            SteppedBehavior.ReportMissing("M11-013 SetMarkerAsObserved0x00876E56: native KnownMarker/object/Camera parent-root binding is absent for legacy flat inputs; selection withheld and prior timestamps preserved, not a native no-match result");
            return;
        }
        if (bestMarker is not null) bestMarker.LastObservedTimestamp = timestamp; // KnownMarker+48, 0x00876F86
        else Warning(false);
    }
    internal static Point2f MarkerCentroid(IReadOnlyList<Vec2> p)
    {
        float x = (float)p[2].X + (float)p[0].X, y = (float)p[2].Y + (float)p[0].Y;
        x = (float)p[1].X + x; y = (float)p[1].Y + y;
        x = (float)p[3].X + x; y = (float)p[3].Y + y;
        float quarter = BitConverter.UInt32BitsToSingle(0x3E800000);
        return new(x * quarter, y * quarter);
    }
    internal static float MarkerQuadArea(IReadOnlyList<Vec2> p)
    {
        float x0=(float)p[0].X,y0=(float)p[0].Y;
        float x3=(float)p[3].X-x0,x2=(float)p[2].X-x0,y2=(float)p[2].Y-y0,y3=(float)p[3].Y-y0;
        float x1=(float)p[1].X-x0,y1=(float)p[1].Y-y0;
        float a=x1*y3-y1*x3,b=y3*x2-x3*y2;
        float half = BitConverter.UInt32BitsToSingle(0x3F000000);
        return MathF.Abs(a)*half + MathF.Abs(b)*half;
    }
}
