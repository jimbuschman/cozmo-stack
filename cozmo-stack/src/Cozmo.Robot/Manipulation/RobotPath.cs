using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// <c>Anki::Cozmo::PathMotionProfile</c>, defaults from the decompiled Unity class (UNITY): drive 100 mm/s,
/// 200 / 500 mm/s² accel / decel; point turns 2 rad/s, 10 rad/s²; docking 60 mm/s, 200 / 500; reverse 80 mm/s.
/// </summary>
public sealed record PathMotionProfile
{
    public float SpeedMmps { get; init; } = 100f;
    public float AccelMmps2 { get; init; } = 200f;
    public float DecelMmps2 { get; init; } = 500f;
    public float PointTurnSpeedRadPerSec { get; init; } = 2f;
    public float PointTurnAccelRadPerSec2 { get; init; } = 10f;
    public float PointTurnDecelRadPerSec2 { get; init; } = 10f;
    public float DockSpeedMmps { get; init; } = 60f;
    public float DockAccelMmps2 { get; init; } = 200f;
    public float DockDecelMmps2 { get; init; } = 500f;
    public float ReverseSpeedMmps { get; init; } = 80f;
    public static readonly PathMotionProfile Default = new();
}

/// <summary>One segment of a robot path, as <c>Anki::Planning::PathSegment</c> holds it.</summary>
public abstract record PathSegment
{
    /// <summary>A straight drive; negative speed drives it backwards.</summary>
    public sealed record Line(double FromX, double FromY, double ToX, double ToY, float SpeedMmps, float AccelMmps2, float DecelMmps2) : PathSegment;
    /// <summary>A circular arc about a centre.</summary>
    public sealed record Arc(double CenterX, double CenterY, double RadiusMm, double StartAngleRad, double SweepRad, float SpeedMmps, float AccelMmps2, float DecelMmps2) : PathSegment;
    /// <summary>A turn in place at (x, y) to an absolute heading.</summary>
    public sealed record PointTurn(double X, double Y, double TargetAngleRad, double AngleToleranceRad, float SpeedRadPerSec, float AccelRadPerSec2, float DecelRadPerSec2, bool UseShortestDirection) : PathSegment;
}

/// <summary><c>PathEventType</c> (UNITY): the robot's <c>PathFollowingEvent</c> kinds.</summary>
public enum PathEventType : byte { Started = 0, Interrupted = 1, Completed = 2 }

/// <summary>
/// The engine's <c>PathDolerOuter</c> + <c>PathComponent::ExecutePath</c>: a path is cleared, appended one
/// segment at a time and executed by id; the robot follows it and reports <c>PathFollowingEvent</c>s. Wire
/// layouts from the doler's packing (0x00507F40..0x00508056, NATIVE field order; names from PyCozmo where it
/// has them): line {from_x, from_y, to_x, to_y, speed, accel, decel}; arc {center_x, center_y, radius,
/// start_angle, sweep, speed, accel, decel}; point turn {x, y, target_angle, angle_tolerance, speed, accel,
/// decel, use_shortest_direction}; <c>ExecutePath {pathID u16, manualSpeed bool}</c> (0x0064A426);
/// <c>ClearPath {pathID u16}</c>.
/// </summary>
public sealed class PathSender
{
    private readonly CozmoRobot _robot;
    private readonly object _gate = new();
    private ushort _pathId;

    public PathSender(CozmoRobot robot) => _robot = robot;

    /// <summary>The id of the last path sent (the engine's <c>_lastSentPathID</c>).</summary>
    public ushort LastPathId { get { lock (_gate) return _pathId; } }
    /// <summary>Every message sent, for tests and the conformance log.</summary>
    public List<RobotMessage> Sent { get; } = new();

    private void Send(RobotMessage m) { Sent.Add(m); _robot.Transport.Send(m, flush: true); }

    private static uint F(double v) => BitConverter.SingleToUInt32Bits((float)v);

    /// <summary>
    /// Clears the robot's path, appends the segments and starts execution. Returns the path id.
    ///
    /// <paramref name="reserve"/> is called with the new id before anything is sent, so a caller can register
    /// for the path's terminal event before the robot can possibly report it.
    /// </summary>
    public ushort Execute(IReadOnlyList<PathSegment> path, Action<ushort>? reserve = null)
    {
        // The clear, the segments and the execute are one installation. Two callers interleaving them
        // would leave the robot holding half of each path under one id, so the whole sequence - and the
        // id it is installed under - is taken together.
        lock (_gate)
        {
            _pathId++;
            if (_pathId == 0) _pathId = 1;
            reserve?.Invoke(_pathId);
            Send(new ClearPath { Unknown = _pathId });
            foreach (var s in path)
            {
                switch (s)
                {
                    case PathSegment.Line l:
                        Send(new AppendPathSegmentLine { XStartMm = (float)l.FromX, YStartMm = (float)l.FromY,
                                                         XEndMm = (float)l.ToX, YEndMm = (float)l.ToY,
                                                         Speed = new PathSegmentSpeed { SpeedMmps = l.SpeedMmps, AccelMmps2 = l.AccelMmps2, DecelMmps2 = l.DecelMmps2 } });
                        break;
                    case PathSegment.Arc a:
                        Send(new AppendPathSegmentArc { XCenterMm = (float)a.CenterX, YCenterMm = (float)a.CenterY,
                                                        RadiusMm = (float)a.RadiusMm, StartRad = (float)a.StartAngleRad, SweepRad = (float)a.SweepRad,
                                                        Speed = new PathSegmentSpeed { SpeedMmps = a.SpeedMmps, AccelMmps2 = a.AccelMmps2, DecelMmps2 = a.DecelMmps2 } });
                        break;
                    case PathSegment.PointTurn t:
                        Send(new AppendPathSegmentPointTurn { XMm = (float)t.X, YMm = (float)t.Y,
                                                              TargetAngleRad = (float)t.TargetAngleRad, AngleToleranceRad = (float)t.AngleToleranceRad,
                                                              Speed = new PathSegmentSpeed { SpeedMmps = t.SpeedRadPerSec, AccelMmps2 = t.AccelRadPerSec2, DecelMmps2 = t.DecelRadPerSec2 },
                                                              UseShortestDirection = (byte)(t.UseShortestDirection ? 1 : 0) });
                        break;
                }
            }
            Send(new ExecutePath { EventId = _pathId, Unknown = false });
            return _pathId;
        }
    }

    /// <summary>
    /// <c>PathComponent::Abort</c>: clear the robot's current path, whatever it is. The engine has one
    /// path component and one path, so its own abort is unqualified like this.
    /// </summary>
    public void Abort() { lock (_gate) Send(new ClearPath { Unknown = _pathId }); }

    /// <summary>
    /// Clears the robot's path only while <paramref name="pathId"/> is still the one installed, and says
    /// whether it did.
    ///
    /// This stack owns paths per action rather than globally: a <see cref="PathRun"/> that is cancelled or
    /// times out clears the path so firmware motion does not outlive the action. That cleanup can be late -
    /// a cancelled wait finishes after another action has already installed its own path - and an
    /// unqualified clear would then stop the path that replaced it. Ownership is the path id: an abort that
    /// no longer owns the robot's path sends nothing.
    /// </summary>
    public bool AbortIfCurrent(ushort pathId)
    {
        lock (_gate)
        {
            if (_pathId != pathId) return false;
            Send(new ClearPath { Unknown = _pathId });
            return true;
        }
    }
}

/// <summary>
/// LOCAL planner standing in for the engine's lattice planner (<c>xythetaPlanner</c>, <c>PathComponent::SelectPlanner</c>):
/// a point turn towards the goal, a straight line to it, and a point turn to the goal heading. No obstacle
/// avoidance; the engine's planner is DEFERRED. Short goals (under <see cref="MinLineMm"/>) skip the line.
/// The point-turn tolerance is the engine's <c>TurnInPlaceAction</c> 2 degrees (0x3D0EFA35).
/// </summary>
public static class StraightLinePlanner
{
    public const double MinLineMm = 5.0;
    public const double PointTurnToleranceRad = 0.0349066;

    public static IReadOnlyList<PathSegment> Plan(Pose3d robot, Pose3d goal, PathMotionProfile? profile = null, bool allowReverse = false)
    {
        var p = profile ?? PathMotionProfile.Default;
        var path = new List<PathSegment>();
        double dx = goal.Translation.X - robot.Translation.X, dy = goal.Translation.Y - robot.Translation.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double heading = robot.AngleAroundZ;
        if (dist >= MinLineMm)
        {
            double travel = Math.Atan2(dy, dx);
            bool reverse = allowReverse && Math.Abs(Wrap(travel - heading)) > Math.PI / 2;
            double face = reverse ? Wrap(travel + Math.PI) : travel;
            if (Math.Abs(Wrap(face - heading)) > PointTurnToleranceRad)
                path.Add(new PathSegment.PointTurn(robot.Translation.X, robot.Translation.Y, face, PointTurnToleranceRad, p.PointTurnSpeedRadPerSec, p.PointTurnAccelRadPerSec2, p.PointTurnDecelRadPerSec2, true));
            float speed = reverse ? -p.ReverseSpeedMmps : p.SpeedMmps;
            path.Add(new PathSegment.Line(robot.Translation.X, robot.Translation.Y, goal.Translation.X, goal.Translation.Y, speed, p.AccelMmps2, p.DecelMmps2));
            heading = face;
        }
        if (Math.Abs(Wrap(goal.AngleAroundZ - heading)) > PointTurnToleranceRad)
            path.Add(new PathSegment.PointTurn(goal.Translation.X, goal.Translation.Y, goal.AngleAroundZ, PointTurnToleranceRad, p.PointTurnSpeedRadPerSec, p.PointTurnAccelRadPerSec2, p.PointTurnDecelRadPerSec2, true));
        return path;
    }

    public static double Wrap(double a) => Math.Atan2(Math.Sin(a), Math.Cos(a));
}

/// <summary>Waits for the robot's <c>PathFollowingEvent</c> for a path id.</summary>
public sealed class PathFollower : IDisposable
{
    /// <summary>How many recently finished path ids keep their terminal event for a late waiter.</summary>
    public const int RetainedTerminalEvents = 16;

    private readonly CozmoRobot _robot;
    private readonly object _gate = new();
    private readonly Dictionary<ushort, TaskCompletionSource<PathEventType>> _waits = new();
    private readonly Dictionary<ushort, PathEventType> _finished = new();
    private readonly Queue<ushort> _finishedOrder = new();

    public PathFollower(CozmoRobot robot) { _robot = robot; robot.Message += OnMessage; }

    public event Action<ushort, PathEventType>? Event;

    private void OnMessage(RobotMessage m)
    {
        if (m is not PathFollowingEvent e) return;
        var type = (PathEventType)e.EventType;
        Event?.Invoke(e.EventId, type);
        if (type == PathEventType.Started) return;
        lock (_gate)
        {
            if (_waits.Remove(e.EventId, out var tcs)) { tcs.TrySetResult(type); return; }
            // Nobody is waiting yet. A path can finish before the caller that started it has awaited, so the
            // terminal event is retained for a short while instead of being dropped on the floor.
            if (!_finished.ContainsKey(e.EventId)) _finishedOrder.Enqueue(e.EventId);
            _finished[e.EventId] = type;
            while (_finishedOrder.Count > RetainedTerminalEvents) _finished.Remove(_finishedOrder.Dequeue());
        }
    }

    /// <summary>
    /// Registers interest in a path id before the path is started, so its terminal event cannot arrive
    /// before there is somewhere to put it. Dispose removes the registration.
    /// </summary>
    public Reservation Reserve(ushort pathId)
    {
        var tcs = new TaskCompletionSource<PathEventType>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_finished.Remove(pathId, out var already)) tcs.TrySetResult(already);
            else _waits[pathId] = tcs;
        }
        return new Reservation(this, pathId, tcs);
    }

    private void Release(ushort pathId, TaskCompletionSource<PathEventType> tcs)
    {
        lock (_gate) if (_waits.TryGetValue(pathId, out var held) && ReferenceEquals(held, tcs)) _waits.Remove(pathId);
    }

    /// <summary>A registered interest in one path id's terminal event.</summary>
    public sealed class Reservation : IDisposable
    {
        private readonly PathFollower _follower;
        private readonly TaskCompletionSource<PathEventType> _tcs;
        private int _disposed;

        internal Reservation(PathFollower follower, ushort pathId, TaskCompletionSource<PathEventType> tcs)
        { _follower = follower; PathId = pathId; _tcs = tcs; }

        public ushort PathId { get; }

        /// <summary>Completes with Completed or Interrupted; null on timeout or cancellation.</summary>
        public async Task<PathEventType?> WaitAsync(TimeSpan timeout, CancellationToken cancel)
        {
            using var reg = cancel.Register(() => _tcs.TrySetCanceled());
            var done = await Task.WhenAny(_tcs.Task, Task.Delay(timeout, CancellationToken.None));
            if (done != _tcs.Task || _tcs.Task.IsCanceled) { Dispose(); return null; }
            return _tcs.Task.Result;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _follower.Release(PathId, _tcs);
        }
    }

    /// <summary>
    /// Completes with Completed or Interrupted, or null on timeout. Registering after the path has started
    /// is safe for a short window (see <see cref="RetainedTerminalEvents"/>), but a caller that owns the path
    /// should <see cref="Reserve"/> before starting it.
    /// </summary>
    public async Task<PathEventType?> WaitForEndAsync(ushort pathId, TimeSpan timeout, CancellationToken cancel)
    {
        using var reservation = Reserve(pathId);
        return await reservation.WaitAsync(timeout, cancel);
    }

    public void Dispose() => _robot.Message -= OnMessage;
}
