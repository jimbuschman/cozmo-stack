namespace Cozmo.Robot.Manipulation;

// 0x0085C034..0x0085C13E, 0x0085C7C8..0x0085C812.
// CoreTechPrint is a diagnostic callback (0x0084CE24); format its text for
// the existing host output sink without claiming a native DAS event or wire message.
internal static class NativePathDiagnostics
{
    private static string F(float value) => LibcxxPrintf.FormatFixedFloat(value);

    internal static void PrintSegment(PathSegment segment, Action<string>? output)
    {
        if (output is null) return;
        string? text = segment switch
        {
            PathSegment.Line l => $"line: ({F(l.FromX)}, {F(l.FromY)}) to ({F(l.ToX)}, {F(l.ToY)}), speed/accel/decel = ({F(l.SpeedMmps)}, {F(l.AccelMmps2)}, {F(l.DecelMmps2)})\n",
            PathSegment.Arc a => $"arc: centerPt ({F(a.CenterX)}, {F(a.CenterY)}), radius {F(a.RadiusMm)}, startAng {F(a.StartAngleRad)}, sweep {F(a.SweepRad)}, speed/accel/decel = ({F(a.SpeedMmps)}, {F(a.AccelMmps2)}, {F(a.DecelMmps2)})\n",
            // 0x0085C096..0x0085C09E multiplies tolerance by 0x42652EE1
            // in f32; the target angle and speed remain radians.
            PathSegment.PointTurn t => $"ptTurn: x {F(t.X)}, y {F(t.Y)}, targetAngle {F(t.TargetAngleRad)}, tol {F(t.AngleToleranceRad * EngineF.Bits(0x42652EE1))}deg speed/accel/decel = ({F(t.SpeedRadPerSec)}, {F(t.AccelRadPerSec2)}, {F(t.DecelRadPerSec2)})\n",
            _ => null, // Unknown tags return without printing (0x0085C044).
        };
        if (text is not null) output(text);
    }

    internal static void PrintPath(IReadOnlyList<PathSegment> path, Action<string>? output)
    {
        if (output is null) return;
        for (int i = 0; i < unchecked((byte)path.Count); i++)
        {
            output(FormattableString.Invariant($"Path segment {i} - "));
            PrintSegment(path[i], output);
        }
    }
}
