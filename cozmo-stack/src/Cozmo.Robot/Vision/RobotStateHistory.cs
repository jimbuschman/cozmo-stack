using Cozmo.Protocol;

namespace Cozmo.Robot.Vision;

/// <summary>The engine's <c>VisionPoseData</c>: the robot at the moment a frame was taken.</summary>
/// <param name="CameraMoving">
/// <c>MovementComponent::WasCameraMoving(ts)</c> (M11-004, H2): <c>(status &amp; 0x8200) != 0x200</c> on the state nearest the timestamp
/// (0x00642802), i.e. moving unless HeadInPos (bit 9) is set and AreWheelsMoving (bit 15) is clear. False when a caller builds the data by hand.
/// </param>
public readonly record struct VisionPoseData(uint Timestamp, Pose3d RobotPose, double HeadAngleRad, double LiftAngleRad, bool Moving, bool RotatingTooFast, bool CameraMoving = false)
{
    public Pose3d CameraPose => HeadGeometry.CameraPoseInWorld(RobotPose, HeadAngleRad);
}

// RobotStateHistory is implemented in NativeRobotStateHistory.cs from the adopted query/admission rows.

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
