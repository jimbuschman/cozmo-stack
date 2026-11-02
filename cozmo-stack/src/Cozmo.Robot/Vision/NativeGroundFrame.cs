using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

// fidelity: M11-033, M11-040
// Partial SetNextImage handoff snapshot; the native IMU deque remains unavailable.
// Neither live robot state nor LUT arrays are retained by reference.
internal sealed class NativeGroundFrame : IDisposable
{
    private readonly HistRobotState _history = new();
    private readonly float[] _homography;
    internal NativeGroundFrame(EngineRobot owner, uint timestamp, HistRobotState source, bool groundValid, ReadOnlySpan<float> homography)
    {
        if (homography.Length != 9) throw new ArgumentException("Ground-plane matrices require nine elements.");
        Owner = owner;
        Timestamp = timestamp;
        _history.Assign(source);
        CameraPose = owner.CopyHistoricalCameraPose(_history);
        // 0x006AF396: detach the stored robot pose without flattening its local transform.
        _history.Pose.ClearParent();
        GroundValid = groundValid;
        _homography = homography.ToArray();
    }
    internal EngineRobot Owner { get; }
    internal uint Timestamp { get; }
    internal RobotState State => HistRobotState.Copy(_history.State);
    internal Pose3d RobotPose => _history.Pose;
    internal Pose3d CameraPose { get; }
    internal bool GroundValid { get; }
    internal float[] Homography => (float[])_homography.Clone();
    public void Dispose() { CameraPose.Dispose(); _history.Dispose(); }
}
