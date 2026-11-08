namespace Cozmo.Robot.Vision;

// fidelity: M11-033, M11-098
// VisionSystem-owned anchor and last two admitted carrier copies (0x6B24D0).
internal sealed class NativeMotionPoseState : IDisposable
{
    private readonly Pose3d _anchor=new("VisionSystemOrigin");
    internal NativeGroundFrame? Current { get; private set; }
    internal NativeGroundFrame? Previous { get; private set; }
    internal bool HasCurrent { get; private set; }
    internal bool HasPrevious { get; private set; }
    internal uint FrameCount { get; private set; }
    internal void Admit(NativeGroundFrame input)
    {
        var copy=input.Copy();copy.RobotPose.SetParent(_anchor);
        Previous?.Dispose();Previous=Current;Current=copy;
        if(HasCurrent)HasPrevious=true;else HasCurrent=true;
        FrameCount=unchecked(FrameCount+1);
    }
    internal static bool HeadSame(NativeGroundFrame current,NativeGroundFrame previous, float angle = 0.001745329238474369f)
    {
        return MathF.Abs(current.State.HeadAngle-previous.State.HeadAngle)<angle;
    }
    internal static bool BodySame(NativeGroundFrame current,NativeGroundFrame previous, float distance = .5f, float angle = 0.001745329238474369f)
    {
        var a=current.RobotPose;var b=previous.RobotPose;
        return MathF.Abs((float)a.Translation.X-(float)b.Translation.X)<distance && MathF.Abs((float)a.Translation.Y-(float)b.Translation.Y)<distance &&
            MathF.Abs(a.Quaternion.AngleAroundZ-b.Quaternion.AngleAroundZ)<angle;
    }
    public void Dispose(){Current?.Dispose();Previous?.Dispose();_anchor.Dispose();}
}
