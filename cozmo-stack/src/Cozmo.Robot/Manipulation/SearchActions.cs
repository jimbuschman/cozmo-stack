using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// The engine's <c>SearchForNearbyObjectAction</c> (constructor 0x005469C0, <c>Init</c> 0x00546C14): back
/// off a little, drop the head and look from side to side, giving the vision system a chance to pick the
/// target up again.
///
/// <c>Init</c> draws five numbers before it builds anything — three waits from
/// <see cref="WaitMinSec"/>..<see cref="WaitMaxSec"/> and two angles from
/// <see cref="SearchAngleMinRad"/>..<see cref="SearchAngleMaxRad"/> — plus a coin
/// (<c>RandDbl(1.0)</c> against 0.5 at 0x00546CCE) that decides which way it looks first. Then it
/// appends, in order:
///
/// <list type="number">
/// <item><c>WaitAction(w1)</c>;</item>
/// <item>a <c>CompoundActionParallel</c> of <c>DriveStraightAction(distance, speed)</c> and
/// <c>MoveHeadToAngleAction(headAngle, tolerance 0.0349066)</c>. The speed is compared with 100 and the
/// two-argument constructor used when it matches (0x00546DE8), which is the default-speed path;</item>
/// <item><c>WaitAction(w1)</c> again — the same draw, not a new one (both use the value in <c>sb</c>);</item>
/// <item><c>TurnInPlaceAction(sign * a1)</c>, relative, tolerance 0.0698132 (4 degrees);</item>
/// <item><c>WaitAction(w2)</c>;</item>
/// <item><c>TurnInPlaceAction(-sign * (a1 + a2))</c>, same tolerance — back past where it started;</item>
/// <item><c>WaitAction(w3)</c>.</item>
/// </list>
///
/// The defaults the constructor writes are the ones <c>SetSearchAngle</c> and <c>SetSearchWaitTime</c>
/// would replace; nothing in the shipped build calls either.
/// </summary>
public sealed class SearchForNearbyObjectAction
{
    /// <summary>0.261799 rad, 15 degrees (0x3E860A92 at 0x00546A52).</summary>
    public const double SearchAngleMinRad = 0.261799;
    /// <summary>0.349066 rad, 20 degrees (0x3EB2B8C2).</summary>
    public const double SearchAngleMaxRad = 0.349066;
    /// <summary>0.8 s (0x3F4CCCCD).</summary>
    public const double WaitMinSec = 0.8;
    /// <summary>1.2 s (0x3F99999A).</summary>
    public const double WaitMaxSec = 1.2;
    /// <summary>0.0698132 rad, 4 degrees: the tolerance on each look-around turn.</summary>
    public const double TurnToleranceRad = 0.0698132;
    /// <summary>0.0349066 rad, 2 degrees: the head move's tolerance.</summary>
    public const double HeadToleranceRad = 0.0349066;
    /// <summary>The speed at which the constructor uses DriveStraightAction's default-speed form.</summary>
    public const float DefaultDriveSpeedMmps = 100f;

    private readonly ManipulationSystem _m;
    private readonly Random _random;

    /// <summary>How the waits are taken. Replaced in tests so a search does not sit out its own seconds.</summary>
    public Func<TimeSpan, CancellationToken, Task> Wait { get; init; } = (t, c) => Task.Delay(t, c);

    public SearchForNearbyObjectAction(ManipulationSystem m, uint objectId, double distanceMm,
                                       float speedMmps, double headAngleRad, Random? random = null)
    {
        _m = m;
        ObjectId = objectId;
        DistanceMm = distanceMm;
        SpeedMmps = speedMmps;
        HeadAngleRad = headAngleRad;
        _random = random ?? new Random();
    }

    public uint ObjectId { get; }
    public double DistanceMm { get; }
    public float SpeedMmps { get; }
    public double HeadAngleRad { get; }

    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    /// <summary>The waits and turns this run drew, in the order Init draws them.</summary>
    public (double W1, double W2, double W3, double A1, double A2, double Sign) Draw { get; private set; }

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        double w1 = Between(WaitMinSec, WaitMaxSec);
        double coin = _random.NextDouble();
        double a1 = Between(SearchAngleMinRad, SearchAngleMaxRad);
        double w2 = Between(WaitMinSec, WaitMaxSec);
        double a2 = Between(SearchAngleMinRad, SearchAngleMaxRad);
        double w3 = Between(WaitMinSec, WaitMaxSec);
        double sign = coin > 0.5 ? 1.0 : -1.0;
        Draw = (w1, w2, w3, a1, a2, sign);
        _trace.Add($"SearchForNearbyObjectAction: {(sign > 0 ? "left" : "right")} {a1 * 180 / Math.PI:F1} deg " +
                   $"then {-(a1 + a2) * 180 / Math.PI:F1} deg, waits {w1:F2}/{w2:F2}/{w3:F2} s");

        await Wait(TimeSpan.FromSeconds(w1), cancel);

        // the drive and the head move run together
        var head = _m.Robot.Motion.SetHeadAngleAsync((float)HeadAngleRad, CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2, requireCalibration: false);
        var back = new DriveStraightAction(_m, DistanceMm, SpeedMmps).RunAsync(cancel);
        var r = await back;
        try { await head; } catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
        if (r != ActionResult.Success) { _trace.Add($"the backing-off drive: {r}"); return r; }

        await Wait(TimeSpan.FromSeconds(w1), cancel);
        if (await TurnAsync(sign * a1, cancel) is { } bad1) return bad1;
        await Wait(TimeSpan.FromSeconds(w2), cancel);
        if (await TurnAsync(-sign * (a1 + a2), cancel) is { } bad2) return bad2;
        await Wait(TimeSpan.FromSeconds(w3), cancel);
        return ActionResult.Success;
    }

    private async Task<ActionResult?> TurnAsync(double relativeRad, CancellationToken cancel)
    {
        var pose = _m.RobotPose();
        if (pose is null) return ActionResult.Abort;
        double target = StraightLinePlanner.Wrap(pose.Value.AngleAroundZ + relativeRad);
        var turn = new PathSegment.PointTurn(pose.Value.Translation.X, pose.Value.Translation.Y, target,
                                             (float)TurnToleranceRad,
                                             PathMotionProfile.Default.PointTurnSpeedRadPerSec,
                                             PathMotionProfile.Default.PointTurnAccelRadPerSec2,
                                             PathMotionProfile.Default.PointTurnDecelRadPerSec2, true);
        using var run = _m.StartPath(new PathSegment[] { turn });
        var ev = await run.WaitAsync(TimeSpan.FromSeconds(8), cancel);
        if (ev == PathEventType.Completed) return null;
        _trace.Add($"the look-around turn did not complete: {ev}");
        return ev is null && cancel.IsCancellationRequested
            ? ActionResult.CancelledWhileRunning
            : ActionResult.FailedTraversingPath;
    }

    private double Between(double a, double b) => a + _random.NextDouble() * (b - a);
}

/// <summary>
/// The engine's <c>SearchForBlockHelper</c> (<c>SearchForBlock</c> 0x005BAF24): what the app does when the
/// cube it was about to work with is no longer where it thought.
///
/// <c>SearchForBlock</c> is a three-state machine on a counter the helper keeps at +0x10C. Two things
/// end it before the states run out. The first is at the top (0x005BAF3E): when the target id is set and
/// <c>BlockWorld::GetLocatedObjectByIdHelper</c> returns nothing, the helper clears its delegate and
/// returns (0x005BB05E) - there is nothing to search for by id once the world has dropped the object.
/// The second is <c>SearchFinishedWithoutInterruption</c> 0x005BB478, which looks the target up again and
/// carries on only when it is there with the byte at object+0x24 equal to 1 - the pose state, whose
/// Known is 1. So the search is for an object whose recorded pose is suspect, and it ends when the pose
/// is known again.
///
/// <list type="bullet">
/// <item><b>State 0</b> (0x005BAF9A): a <see cref="SearchForNearbyObjectAction"/> with
/// <c>-20 mm</c>, <c>100 mm/s</c> and a head angle of <c>-0.0872665</c> (-5 degrees), followed by a
/// <c>TurnTowardsObjectAction(target, Radians(pi))</c> when the target id is set.</item>
/// <item><b>State 1</b> (0x005BB1AA): <c>DriveStraightAction(-20 mm, 20 mm/s)</c>, two
/// <c>TurnInPlaceAction(+0.785398)</c> - 45 degrees each, so 90 in all - then the same nearby search,
/// then the drive again.</item>
/// <item><b>State 2</b> (0x005BB066): the mirror of state 1, with <c>-0.785398</c>.</item>
/// </list>
///
/// Whether searching is worth starting at all is <c>ShouldBeAbleToFindTarget</c> 0x005BB73C, which asks
/// <c>ObservableObject::IsVisibleFromWithReason</c> with 0.785398 rad and checks the reason against 7.
/// </summary>
public sealed class SearchForBlockHelper
{
    /// <summary>-20 mm: the distance every stage backs off (0xC1A00000).</summary>
    public const double BackOffMm = -20.0;
    /// <summary>100 mm/s for the nearby search's own drive.</summary>
    public const float SearchSpeedMmps = 100f;
    /// <summary>20 mm/s for the drives that bracket states 1 and 2 (0x41A00000).</summary>
    public const float StageSpeedMmps = 20f;
    /// <summary>-0.0872665 rad, -5 degrees: the head angle the nearby search drops to.</summary>
    public const double SearchHeadAngleRad = -0.0872665;
    /// <summary>0.785398 rad, 45 degrees: each of the two turns in states 1 and 2.</summary>
    public const double StageTurnRad = 0.785398;
    /// <summary>Three states, and the helper gives up when the counter passes the last.</summary>
    public const int StateCount = 3;

    private readonly ManipulationSystem _m;
    private readonly Random _random;

    public SearchForBlockHelper(ManipulationSystem m, uint objectId, Random? random = null)
    {
        _m = m;
        ObjectId = objectId;
        _random = random ?? new Random();
    }

    public uint ObjectId { get; }
    public int State { get; private set; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    /// <summary>Whether the world still has the target at all; when it does not, the search stops.</summary>
    public bool TargetIsLocated => _m.World.GetLocatedObjectById(ObjectId) is not null;

    /// <summary>The condition SearchFinishedWithoutInterruption checks: located, with a known pose.</summary>
    public bool TargetPoseIsKnown =>
        _m.World.GetLocatedObjectById(ObjectId) is { PoseState: PoseState.Known };

    /// <summary>How the waits inside each look-around are taken; passed on to the nearby search.</summary>
    public Func<TimeSpan, CancellationToken, Task> Wait { get; init; } = (t, c) => Task.Delay(t, c);

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        for (State = 0; State < StateCount; State++)
        {
            if (cancel.IsCancellationRequested) return ActionResult.CancelledWhileRunning;
            if (!TargetIsLocated)
            {
                _trace.Add($"SearchForBlockHelper: object {ObjectId} is not in the world any more, so there is nothing to search for");
                return ActionResult.BadObject;
            }

            _trace.Add($"SearchForBlockHelper: state {State}");
            var r = State switch
            {
                0 => await LookAroundAsync(cancel),
                1 => await SweepAsync(+StageTurnRad, cancel),
                _ => await SweepAsync(-StageTurnRad, cancel),
            };
            if (r is ActionResult.CancelledWhileRunning or ActionResult.Abort) return r;

            if (TargetPoseIsKnown)
            {
                _trace.Add($"SearchForBlockHelper: object {ObjectId} has a known pose again");
                return ActionResult.Success;
            }
        }
        _trace.Add("SearchForBlockHelper: the search ran out of states");
        return ActionResult.VisualObservationFailed;
    }

    /// <summary>State 0.</summary>
    private async Task<ActionResult> LookAroundAsync(CancellationToken cancel)
    {
        var search = new SearchForNearbyObjectAction(_m, ObjectId, BackOffMm, SearchSpeedMmps,
                                                     SearchHeadAngleRad, _random) { Wait = Wait };
        var r = await search.RunAsync(cancel);
        _trace.AddRange(search.Trace);
        if (r != ActionResult.Success) return r;
        // the engine appends the turn only when the target id is set
        await _m.TurnTowardsObjectAsync(ObjectId, Math.PI, cancel);
        return ActionResult.Success;
    }

    /// <summary>States 1 and 2: back off, turn twice one way, look around, then back off again.</summary>
    private async Task<ActionResult> SweepAsync(double turnRad, CancellationToken cancel)
    {
        var r = await new DriveStraightAction(_m, BackOffMm, StageSpeedMmps).RunAsync(cancel);
        if (r != ActionResult.Success) { _trace.Add($"the sweep's first drive: {r}"); return r; }

        for (int i = 0; i < 2; i++)
        {
            var t = await TurnAsync(turnRad, cancel);
            if (t is { } bad) { _trace.Add($"the sweep's turn {i + 1}: {bad}"); return bad; }
        }

        var search = new SearchForNearbyObjectAction(_m, ObjectId, BackOffMm, SearchSpeedMmps,
                                                     SearchHeadAngleRad, _random) { Wait = Wait };
        var s = await search.RunAsync(cancel);
        _trace.AddRange(search.Trace);
        if (s != ActionResult.Success) return s;

        return await new DriveStraightAction(_m, BackOffMm, StageSpeedMmps).RunAsync(cancel);
    }

    private async Task<ActionResult?> TurnAsync(double relativeRad, CancellationToken cancel)
    {
        var pose = _m.RobotPose();
        if (pose is null) return ActionResult.Abort;
        double target = StraightLinePlanner.Wrap(pose.Value.AngleAroundZ + relativeRad);
        var turn = new PathSegment.PointTurn(pose.Value.Translation.X, pose.Value.Translation.Y, target,
                                             StraightLinePlanner.PointTurnToleranceRad,
                                             PathMotionProfile.Default.PointTurnSpeedRadPerSec,
                                             PathMotionProfile.Default.PointTurnAccelRadPerSec2,
                                             PathMotionProfile.Default.PointTurnDecelRadPerSec2, true);
        using var run = _m.StartPath(new PathSegment[] { turn });
        var ev = await run.WaitAsync(TimeSpan.FromSeconds(8), cancel);
        return ev == PathEventType.Completed ? null : ActionResult.FailedTraversingPath;
    }
}
