namespace Cozmo.Robot.Vision;

// fidelity: M11-033, M11-098
// VisionSystem-owned anchor and last two admitted carrier copies (0x6B24D0).
internal sealed class NativeMotionPoseState : IDisposable
{
    private readonly Pose3d _anchor=new("VisionSystemOrigin");
    // VisionSystem ctor 0x006B00A2..B2 constructs both VisionPoseData carriers
    // through 0x006503A8. HistRobotState 0x0053044C uses identity pose and
    // GetDefaultRobotState 0x005182B0 (head angle zero), before any admission.
    private readonly HistRobotState _initial = new();
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
        return BodySame(current.RobotPose,previous.RobotPose,distance,angle);
    }
    private static bool BodySame(Pose3d a,Pose3d b,float distance,float angle)
    {
        return MathF.Abs((float)a.Translation.X-(float)b.Translation.X)<distance && MathF.Abs((float)a.Translation.Y-(float)b.Translation.Y)<distance &&
            MathF.Abs(a.Quaternion.AngleAroundZ-b.Quaternion.AngleAroundZ)<angle;
    }
    // DetectFaces 0x006B3576..0x006B35F4 compares the carriers even on the
    // first image. The default previous carrier is an input, not a gap.
    internal bool ResetFaceTracking()
    {
        var current=Current ?? throw new InvalidOperationException("No admitted face pose");
        float angle=BitConverter.UInt32BitsToSingle(0x3E0EFA35);
        if(Previous is {} previous)return !HeadSame(current,previous,angle)||!BodySame(current,previous,10f,angle);
        return !(MathF.Abs(current.State.HeadAngle-_initial.State.HeadAngle)<angle) ||
            !BodySame(current.RobotPose,_initial.Pose,10f,angle);
    }
    public void Dispose(){Current?.Dispose();Previous?.Dispose();_initial.Dispose();_anchor.Dispose();}
}
