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

    /// <summary>
    /// How much history to keep, in robot milliseconds: <c>RobotStateHistory::CullToWindowSize</c> 0x005309D1 (run from <c>AddRawOdomState</c> 0x00530EBC) erases every key below
    /// (newest RAW key - [this+0x3C]); the default window is 3000 ms (<c>movw r2,#0xBB8</c> at 0x005308BE in the constructor; no <c>SetTimeWindow</c> caller found). M11-044.
    /// </summary>
    // fidelity: M11-044
    public uint WindowMs { get; init; } = 3000;

    public int Count { get { lock (_gate) return _entries.Count; } }

    // M11-051 (IMPLEMENTATION_GAP, NOT BUILT): AddRawOdomState 0x00530CF8 also (a) drops a state whose key is older than newest - window (0x00530D2C..0x00530DAC), (b) on a gap over a global
    // double counts consecutive gaps and destroys the whole raw history on the 6th (0x00530DB2..0x00530E78), (c) drops a state whose pose has a non-root parent (0x00530E82..0x00530EA4) and
    // (d) on a duplicate key warns and does not cull (0x00530EB0..0x00530EC4). None of these is built. The purge of other-origin entries just below is NOT the engine's: the engine has no
    // purge of entries of another origin there (a stand-in this stack keeps, visible here).
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
                // no `_computed.Clear()`: AddRawOdomState (0x00530CF8) does not clear the computed map and Robot::Delocalize does not call the history's Clear (M11-044)
            }
            _entries.Add(new Entry(s.Timestamp, s.Pose, s.HeadAngle, s.LiftAngle, s.Status, s.PoseOriginId));
            while (_entries.Count > 0 && unchecked(s.Timestamp - _entries[0].Timestamp) > WindowMs) _entries.RemoveAt(0);
            // CullToWindowSize (0x005309D1..0x00530BBE) also erases the computed-state map below the same cutoff (newest raw key - window); the vision-only and fourth maps are not built.
            // fidelity: M11-044
            // Source-backed guards: it returns when the raw map has fewer than 2 entries (0x005309DC) and when the newest key is below the window (0x00530A08..0x00530A0A). The count and the newest key
            // here come from this class's list (the engine: the raw map's size and its newest map key), identical for a monotonic single-origin stream.
            if (_entries.Count >= 2 && s.Timestamp >= WindowMs)
            {
                uint cutoff = s.Timestamp - WindowMs;
                foreach (var k in _computed.Keys.Where(k => k < cutoff).ToList()) _computed.Remove(k);
            }
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
            _computed.Clear();
            _originId = 0;
        }
    }

    // fidelity: M1-041
    /// <summary>RobotStateHistory::Clear, which Robot::SyncTime runs (M1 CD18): every state is dropped.</summary>
    internal void Clear()
    {
        lock (_gate) { _entries.Clear(); _computed.Clear(); }
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
            // M11-004 / H2: WasCameraMoving is (status & 0x8200) != 0x200 (0x00642802): head not in position or wheels moving.
            // fidelity: M11-004
            bool cameraMoving = (best.Status & 0x8200u) != 0x200u;
            return new VisionPoseData(best.Timestamp, HeadGeometry.RobotPose(best.Pose), best.HeadAngle, best.LiftAngle, moving, fast, cameraMoving);
        }
    }

    /// <summary>
    /// <c>RobotStateHistory</c>'s computed-state map (this+0x1C): the states <c>ComputeAndInsertStateAt</c> and <c>AddVisionOnlyStateToHistory</c> put in, keyed by timestamp.
    /// The only insertion this stack has is <see cref="ComputeAndInsertStateAt"/>, which <c>VisionComponent::UpdateVisionMarkers</c> runs for a result that has markers
    /// (0x00654D92..0x00654DE6). The vision-only insertion of <c>LocalizeToObject</c> (<c>AddVisionOnlyStateToHistory</c>) is not built. There is no prune here: the map is bounded only by
    /// <c>CullToWindowSize</c> (see <see cref="Add"/>) and emptied by <see cref="Clear"/>.
    /// </summary>
    private readonly SortedDictionary<uint, Pose3d> _computed = new();

    /// <summary>
    /// How many computed states were built from two bracketing raw states whose keys differ from the requested time. The engine interpolates them
    /// (<c>HistRobotState::Interpolate</c>, fraction (t - before.key)/(after.key - before.key)); the blend is M11-052 (RECOVERABLE_GAP, NOT BUILT this round: Interpolate 0x0053068C is described in the record, but the frame of its temporary pose is unread), so this stack stores the
    /// nearer raw state's pose unblended (ties to the later). Each such state is counted here so the stand-in is visible.
    /// </summary>
    public int InterpolationStandIns { get; private set; }

    /// <summary>
    /// <c>RobotStateHistory::ComputeAndInsertStateAt(t, key&amp;, state**, unsigned*, true)</c> as <c>UpdateVisionMarkers</c> calls it (0x00654D92): <c>ComputeStateAt</c> 0x00531785 uses a
    /// vision-only state at exactly t (none exist in this stack, none are built; 0x005317BE..0x005317E6) and otherwise <c>GetRawStateAt(t, ..., true)</c> 0x00531431, which fails (returns 1)
    /// when no raw state at or after t exists (0x0053146A..0x00531472); with two bracketing states it interpolates them (0x005314D0..0x00531534, key out = t, 0x00531558); the result is composed
    /// with the vision-only transforms (none exist: a no-op, 0x0053190A..0x00531AC6) and inserted at key t (0x00531C18..0x00531C86). Returns true on success (the engine's 0). A t below every raw key (no
    /// bracketing state before it) fails as well (0x0053146A..0x00531470, source-backed).
    /// M11-052 (RECOVERABLE_GAP, NOT BUILT): the engine also returns 0x06000000 when the two bracketing states have different origin ids (0x005314BA..0x005314C0) or GetWithRespectTo fails; its
    /// blend is HistRobotState::Interpolate 0x0053068C (see the stand-in below); the frame of the blend's temporary pose is unread.
    /// </summary>
    // fidelity: M11-044
    public bool ComputeAndInsertStateAt(uint timestamp)
    {
        lock (_gate)
        {
            Entry? before = null, after = null;
            foreach (var e in _entries)
            {
                if (e.Timestamp <= timestamp && (before is null || e.Timestamp >= before.Value.Timestamp)) before = e;
                if (e.Timestamp >= timestamp && (after is null || e.Timestamp <= after.Value.Timestamp)) after = e;
            }
            if (after is null) return false;                                   // 0x0053146A..0x00531472
            Entry use;
            if (after.Value.Timestamp == timestamp) use = after.Value;
            else if (before is null) return false;                             // first key > t: return 1 (0x0053146A..0x00531470)
            else
            {
                double fraction = (double)(timestamp - before.Value.Timestamp) / (after.Value.Timestamp - before.Value.Timestamp);
                use = fraction < 0.5 ? before.Value : after.Value;             // STAND-IN for HistRobotState::Interpolate (MISSING)
                InterpolationStandIns++;
            }
            _computed[timestamp] = HeadGeometry.RobotPose(use.Pose);
            return true;
        }
    }

    /// <summary>
    /// LOCAL, no engine counterpart: puts a state the caller measured into the computed map, for <see cref="VisionSystem.ProcessImage(GrayImage, uint, uint, VisionPoseData)"/>, whose caller hands
    /// in the robot's pose for the frame instead of robot states in the history (offline tools and tests).
    /// </summary>
    internal void InsertComputedStateAt(uint timestamp, Pose3d robotPose)
    {
        lock (_gate) _computed[timestamp] = robotPose;
    }

    /// <summary>
    /// <c>RobotStateHistory::GetComputedStateAt(t, &amp;state)</c> 0x00531CCC: a <c>lower_bound</c> over the COMPUTED-state map (0x00531CDC..0x00531CFC); it fails unless a computed state exists
    /// with exactly the requested timestamp (<c>cmp r6,r1; bhi</c> to return 1, 0x00531D04..0x00531D06) and on success returns that state's pose. Null is the failure.
    /// </summary>
    // fidelity: M11-044
    public Pose3d? GetComputedStateAt(uint timestamp)
    {
        lock (_gate) return _computed.TryGetValue(timestamp, out var p) ? p : null;
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
