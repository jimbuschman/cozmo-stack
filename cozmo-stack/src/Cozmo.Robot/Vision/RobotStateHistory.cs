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
    private readonly record struct Entry(uint Timestamp, RobotPose Pose, float HeadAngle, float LiftAngle, uint Status,
                                        uint OriginId);

    private readonly List<Entry> _entries = new();
    private readonly object _gate = new();

    /// <summary>How much history to keep, in robot milliseconds (2 s; the engine keeps a bounded window too).</summary>
    public uint WindowMs { get; init; } = 2000;

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
            _entries.Add(new Entry(s.Timestamp, s.Pose, s.HeadAngle, s.LiftAngle, s.Status, s.PoseOriginId));
            while (_entries.Count > 0 && unchecked(s.Timestamp - _entries[0].Timestamp) > WindowMs) _entries.RemoveAt(0);
        }
    }

    private uint _originId;

    // fidelity: M1-025, M1-015
    /// <summary>Back to the state right after construction, for a removed robot (CB33, CC26, CC27): no states, origin 0.</summary>
    internal void ResetToConstructed()
    {
        lock (_gate)
        {
            _entries.Clear();
            _originId = 0;
        }
    }

    // fidelity: M1-041
    /// <summary>RobotStateHistory::Clear, which Robot::SyncTime runs (M1 CD18): every state is dropped.</summary>
    internal void Clear()
    {
        lock (_gate) _entries.Clear();
    }

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
            // M11-004 / H2: MovementComponent::WasMoving(timestamp) is not a window or a velocity test; it
            // is the IS_MOVING bit (bit 0) of the HistRobotState nearest the timestamp (lambda 0x00642672,
            // `HistRobotState[+0x58] & 1`; +0x58 is RobotState.status + 0x4C).
            // fidelity: M11-004
            bool moving = ((RobotStatusFlag)best.Status & RobotStatusFlag.IsMoving) != 0;
            // RotatingTooFast is not computed here: the engine's WasRotatingTooFast is a VisionComponent
            // method over the ImuDataHistory (C3.3), so VisionSystem fills it in from ImuDataHistory.
            bool fast = false;
            return new VisionPoseData(best.Timestamp, HeadGeometry.RobotPose(best.Pose), best.HeadAngle, best.LiftAngle, moving, fast);
        }
    }

    public VisionPoseData? Latest { get { lock (_gate) return _entries.Count == 0 ? null : At(_entries[^1].Timestamp); } }
}

/// <summary>
/// The engine's <c>ImuData</c> (C3.3/H4.5): timestamp +0, rateX +4, rateY +8, rateZ +0xC, u8 +0x10
/// (<c>ImuDataHistory::AddImuData</c> 0x00538B24).
/// </summary>
public readonly record struct ImuData(uint Timestamp, float RateX, float RateY, float RateZ, byte Flags);

/// <summary>
/// The engine's <c>ImuDataHistory</c>, which lives at <c>VisionComponent+0xb0</c> (C3.3). It is filled by
/// <c>RobotToEngineImplMessaging::HandleImageImuData</c> 0x00535C20, which calls
/// <c>ImuDataHistory::AddImuData(timestamp, rateX, rateY, rateZ, u8)</c> 0x00538B24 on every image IMU
/// message. <c>VisionComponent::WasHeadRotatingTooFast</c> 0x00656260 and
/// <c>WasBodyRotatingTooFast</c> 0x00656384 take the samples immediately before and after the timestamp:
/// <c>|before.rate| &gt; threshold</c> or <c>|after.rate| &gt; threshold</c> is true (head uses
/// <c>rateY</c>, body uses <c>rateZ</c>), and a missing bracket is also true (fail-safe;
/// 0x00656306..0x00656342 and 0x00656464).
/// </summary>
public sealed class ImuDataHistory
{
    private readonly List<ImuData> _samples = new();
    private readonly object _gate = new();

    /// <summary>How much history to keep, in robot milliseconds (the engine keeps a bounded window too).</summary>
    public uint WindowMs { get; init; } = 2000;

    public int Count { get { lock (_gate) return _samples.Count; } }

    // fidelity: M11-004
    public void Add(uint timestamp, float rateX, float rateY, float rateZ, byte flags)
    {
        lock (_gate)
        {
            _samples.Add(new ImuData(timestamp, rateX, rateY, rateZ, flags));
            while (_samples.Count > 0 && unchecked(timestamp - _samples[0].Timestamp) > WindowMs) _samples.RemoveAt(0);
        }
    }

    // fidelity: M1-025, M1-015
    /// <summary>Back to as-constructed for a removed robot.</summary>
    internal void ResetToConstructed() { lock (_gate) _samples.Clear(); }

    // fidelity: M11-004
    public bool WasHeadRotatingTooFast(uint timestamp, double threshold) => IsRotatingTooFast(timestamp, threshold, head: true);

    // fidelity: M11-004
    public bool WasBodyRotatingTooFast(uint timestamp, double threshold) => IsRotatingTooFast(timestamp, threshold, head: false);

    // fidelity: M11-004
    /// <summary><c>VisionComponent::WasRotatingTooFast</c> 0x0065359C: head OR body.</summary>
    public bool WasRotatingTooFast(uint timestamp, double threshold) =>
        WasHeadRotatingTooFast(timestamp, threshold) || WasBodyRotatingTooFast(timestamp, threshold);

    private bool IsRotatingTooFast(uint timestamp, double threshold, bool head)
    {
        ImuData before, after;
        lock (_gate)
        {
            if (_samples.Count == 0) return true;          // no IMU data at all -> fail-safe true
            ImuData? b = null, a = null;
            foreach (var s in _samples)
            {
                if (s.Timestamp <= timestamp && (b is null || s.Timestamp >= b.Value.Timestamp)) b = s;
                if (s.Timestamp >= timestamp && (a is null || s.Timestamp <= a.Value.Timestamp)) a = s;
            }
            if (b is null || a is null) return true;       // no bracket -> fail-safe true
            before = b.Value; after = a.Value;
        }
        double rb = head ? before.RateY : before.RateZ;
        double ra = head ? after.RateY : after.RateZ;
        return Math.Abs(rb) > threshold || Math.Abs(ra) > threshold;
    }
}
