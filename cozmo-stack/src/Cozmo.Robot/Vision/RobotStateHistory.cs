using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

/// <summary>The engine's <c>VisionPoseData</c>: the robot at the moment a frame was taken.</summary>
public readonly record struct VisionPoseData(uint Timestamp, Pose3d RobotPose, double HeadAngleRad, double LiftAngleRad, bool Moving, bool RotatingTooFast)
{
    public Pose3d CameraPose => HeadGeometry.CameraPoseInWorld(RobotPose, HeadAngleRad);
}

/// <summary>
/// The engine's <c>RobotStateHistory</c>: recent robot states keyed by their timestamp so a camera frame, which
/// carries the robot timestamp of its exposure, can be paired with the pose and head angle at that moment
/// (<c>Robot::GetComputedStateAt</c>). Also answers whether the robot was moving around that time.
/// </summary>
public sealed class RobotStateHistory
{
    private readonly record struct Entry(uint Timestamp, RobotPose Pose, float HeadAngle, float LiftAngle, uint Status, float GyroZ,
                                        uint OriginId);

    private readonly List<Entry> _entries = new();
    private readonly object _gate = new();

    /// <summary>How much history to keep, in robot milliseconds (2 s; the engine keeps a bounded window too).</summary>
    public uint WindowMs { get; init; } = 2000;

    /// <summary>Movement is looked for this many ms around the frame time (LOCAL: one state period each side).</summary>
    public uint MovingWindowMs { get; init; } = 66;

    public int Count { get { lock (_gate) return _entries.Count; } }

    public void Add(RobotState s)
    {
        lock (_gate)
        {
            // A pose means nothing without the frame it was measured in. The robot reports the origin its
            // pose is relative to in every state, and the engine resolves it - PoseOriginList::GetOriginByID
            // at 0x00512C54, right after the state goes into history - so a pose from a previous origin is
            // in a different coordinate frame, not merely old.
            if (s.PoseOriginId != _originId)
            {
                _originId = s.PoseOriginId;
                _entries.RemoveAll(e => e.OriginId != _originId);
            }
            _entries.Add(new Entry(s.Timestamp, s.Pose, s.HeadAngle, s.LiftAngle, s.Status, s.Gyro.Z, s.PoseOriginId));
            while (_entries.Count > 0 && unchecked(s.Timestamp - _entries[0].Timestamp) > WindowMs) _entries.RemoveAt(0);
        }
    }

    private uint _originId;

    /// <summary>The origin the robot's poses are currently reported in, from its state stream.</summary>
    public uint OriginId { get { lock (_gate) return _originId; } }

    /// <summary>The state nearest to a timestamp (null when nothing has been recorded).</summary>
    public VisionPoseData? At(uint timestamp)
    {
        lock (_gate)
        {
            if (_entries.Count == 0) return null;
            Entry best = _entries[0]; long bestD = long.MaxValue;
            foreach (var e in _entries)
            {
                long d = Math.Abs((long)e.Timestamp - timestamp);
                if (d <= bestD) { bestD = d; best = e; }     // ties go to the later state
            }
            bool moving = false, fast = false;
            foreach (var e in _entries)
            {
                if (Math.Abs((long)e.Timestamp - timestamp) > MovingWindowMs) continue;
                var f = (RobotStatusFlag)e.Status;
                if ((f & (RobotStatusFlag.IsMoving | RobotStatusFlag.AreWheelsMoving)) != 0) moving = true;
                if (Math.Abs(e.GyroZ) > BlockWorld.MaxRotationRateRadPerSec) fast = true;
            }
            return new VisionPoseData(best.Timestamp, HeadGeometry.RobotPose(best.Pose), best.HeadAngle, best.LiftAngle, moving, fast);
        }
    }

    public VisionPoseData? Latest { get { lock (_gate) return _entries.Count == 0 ? null : At(_entries[^1].Timestamp); } }
}
