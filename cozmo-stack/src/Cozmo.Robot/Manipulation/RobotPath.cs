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
    private ushort _pathId;

    public PathSender(CozmoRobot robot) => _robot = robot;

    /// <summary>The id of the last path sent (the engine's <c>_lastSentPathID</c>).</summary>
    public ushort LastPathId => _pathId;
    /// <summary>Every message sent, for tests and the conformance log.</summary>
    public List<RobotMessage> Sent { get; } = new();

    private void Send(RobotMessage m) { Sent.Add(m); _robot.Transport.Send(m, flush: true); }

    private static uint F(double v) => BitConverter.SingleToUInt32Bits((float)v);

    /// <summary>Clears the robot's path, appends the segments and starts execution. Returns the path id.</summary>
    public ushort Execute(IReadOnlyList<PathSegment> path)
    {
        _pathId++;
        if (_pathId == 0) _pathId = 1;
        Send(new ClearPath { Unknown = _pathId });
        foreach (var s in path)
        {
            switch (s)
            {
                case PathSegment.Line l:
                    Send(new AppendPathSegmentLine { Field0 = F(l.FromX), Field1 = F(l.FromY), Field2 = F(l.ToX), Field3 = F(l.ToY),
                                                     Field4 = new PathSegmentSpeed { SpeedMmps = l.SpeedMmps, AccelMmps2 = l.AccelMmps2, DecelMmps2 = l.DecelMmps2 } });
                    break;
                case PathSegment.Arc a:
                    Send(new AppendPathSegmentArc { Field0 = F(a.CenterX), Field1 = F(a.CenterY), Field2 = F(a.RadiusMm), Field3 = F(a.StartAngleRad), Field4 = F(a.SweepRad),
                                                    Field5 = new PathSegmentSpeed { SpeedMmps = a.SpeedMmps, AccelMmps2 = a.AccelMmps2, DecelMmps2 = a.DecelMmps2 } });
                    break;
                case PathSegment.PointTurn t:
                    Send(new AppendPathSegmentPointTurn { Field0 = F(t.X), Field1 = F(t.Y), Field2 = F(t.TargetAngleRad), Field3 = F(t.AngleToleranceRad),
                                                          Field4 = new PathSegmentSpeed { SpeedMmps = t.SpeedRadPerSec, AccelMmps2 = t.AccelRadPerSec2, DecelMmps2 = t.DecelRadPerSec2 },
                                                          Field5 = (byte)(t.UseShortestDirection ? 1 : 0) });
                    break;
            }
        }
        Send(new ExecutePath { EventId = _pathId, Unknown = false });
        return _pathId;
    }

    /// <summary><c>PathComponent::Abort</c>: clear the robot's current path.</summary>
    public void Abort() => Send(new ClearPath { Unknown = _pathId });
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
    private readonly CozmoRobot _robot;
    private readonly object _gate = new();
    private readonly Dictionary<ushort, TaskCompletionSource<PathEventType>> _waits = new();

    public PathFollower(CozmoRobot robot) { _robot = robot; robot.Message += OnMessage; }

    public event Action<ushort, PathEventType>? Event;

    private void OnMessage(RobotMessage m)
    {
        if (m is not PathFollowingEvent e) return;
        var type = (PathEventType)e.EventType;
        Event?.Invoke(e.EventId, type);
        if (type == PathEventType.Started) return;
        lock (_gate) if (_waits.Remove(e.EventId, out var tcs)) tcs.TrySetResult(type);
    }

    /// <summary>Completes with Completed or Interrupted, or null on timeout.</summary>
    public async Task<PathEventType?> WaitForEndAsync(ushort pathId, TimeSpan timeout, CancellationToken cancel)
    {
        var tcs = new TaskCompletionSource<PathEventType>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate) _waits[pathId] = tcs;
        using var reg = cancel.Register(() => tcs.TrySetCanceled());
        var done = await Task.WhenAny(tcs.Task, Task.Delay(timeout, CancellationToken.None));
        if (done != tcs.Task) { lock (_gate) _waits.Remove(pathId); return null; }
        if (tcs.Task.IsCanceled) return null;
        return tcs.Task.Result;
    }

    public void Dispose() => _robot.Message -= OnMessage;
}
