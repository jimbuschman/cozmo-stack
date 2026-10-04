using Cozmo.Robot.Animation;
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
    // Every constant is the engine's binary32 (checklist section 4), widened exactly; none is a rounded decimal.
    private static double Bits(uint b) => BitConverter.UInt32BitsToSingle(b);

    // fidelity: M12-010
    /// <summary>15 degrees as binary32 0x3E860A92 (movw/movt 0x00546A3E, 0x00546A52; the ctor's default stored at +0x144).</summary>
    public static readonly double SearchAngleMinRad = Bits(0x3E860A92);
    /// <summary>20 degrees as binary32 0x3EB2B8C2 (0x00546A4E, 0x00546A56; +0x148).</summary>
    public static readonly double SearchAngleMaxRad = Bits(0x3EB2B8C2);
    /// <summary>0.8 s as binary32 0x3F4CCCCD (0x00546A20, 0x00546A38; +0x13C).</summary>
    public static readonly double WaitMinSec = Bits(0x3F4CCCCD);
    /// <summary>1.2 s as binary32 0x3F99999A (0x00546A60, 0x00546A6C; +0x140).</summary>
    public static readonly double WaitMaxSec = Bits(0x3F99999A);
    /// <summary>The look-around turns' tolerance, binary32 0x3D8EFA35 (0x00546ED2..0x00546EDA and 0x00546F20..0x00546F28; Radians ctor, then SetTolerance).</summary>
    public static readonly double TurnToleranceRad = Bits(0x3D8EFA35);
    /// <summary>The head move's tolerance, binary32 0x3D0EFA35 (0x00546E56..0x00546E60, MoveHeadToAngleAction's tolerance argument).</summary>
    public static readonly double HeadToleranceRad = Bits(0x3D0EFA35);
    /// <summary>The speed at which the constructor uses DriveStraightAction's default-speed form.</summary>
    public const float DefaultDriveSpeedMmps = 100f;

    private readonly ManipulationSystem _m;
    private readonly EngineRandom _random;

    /// <summary>How the waits are taken. Replaced in tests so a search does not sit out its own seconds.</summary>
    public Func<TimeSpan, CancellationToken, Task> Wait { get; init; } = (t, c) => Task.Delay(t, c);

    /// <summary>
    /// <paramref name="random"/> is the generator <c>Init</c> draws from: <c>IAction::GetRNG</c> 0x00540D10 is <c>Robot::GetRNG</c> 0x005126B6
    /// (<c>[[robot]+0x14]</c>), the CONTEXT RNG (M5 inventory R3: context+0x14), which this stack holds as <c>Animations.Scheduler.ContextRandom</c>.
    /// The default is that one; a test passes a seeded <see cref="EngineRandom"/>.
    /// </summary>
    // fidelity: M12-010
    public SearchForNearbyObjectAction(ManipulationSystem m, uint objectId, double distanceMm,
                                       float speedMmps, double headAngleRad, EngineRandom? random = null)
    {
        _m = m;
        ObjectId = objectId;
        DistanceMm = distanceMm;
        SpeedMmps = speedMmps;
        HeadAngleRad = headAngleRad;
        _random = random ?? m.Robot.Animations.Scheduler.ContextRandom;
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
        // Init 0x00546C14: each draw is IAction::GetRNG() then RandDblInRange / RandDbl (0x00546C34, 0x00546C5C, 0x00546C72.. in this order):
        // w1 (0x00546C3A..0x00546C52), the coin RandDbl(1.0) (0x00546C68), a1 (0x00546C76..0x00546C8E), w2 (0x00546C9C..0x00546CC0),
        // a2 (0x00546CDA..0x00546D00) and w3 (0x00546D16..0x00546D42). The bounds are the binary32 members widened to double
        // (vcvt.f64.f32); RandDblInRange 0x0082FA48 is (max - min) * u + min.
        double w1d = _random.RandDblInRange(WaitMinSec, WaitMaxSec);
        double coin = _random.RandDbl(1.0);
        double a1d = _random.RandDblInRange(SearchAngleMinRad, SearchAngleMaxRad);
        double w2d = _random.RandDblInRange(WaitMinSec, WaitMaxSec);
        double a2d = _random.RandDblInRange(SearchAngleMinRad, SearchAngleMaxRad);
        double w3d = _random.RandDblInRange(WaitMinSec, WaitMaxSec);
        // 0x00546CCE vcmpe.f64 coin, 0.5 (gt -> +1.0, else -1.0); then in binary32/binary64 exactly as the engine stores them:
        // s20 = (float)(sign * a1) (0x00546CF8..0x00546CFC), s16 = -s20, d2 = (double)s16 - sign*a2 -> s18 (0x00546D0A..0x00546D36),
        // the waits are (float)w (0x00546D4E, 0x00546D6A, 0x00546D66)
        double sign = coin > 0.5 ? 1.0 : -1.0;
        float w1 = (float)w1d, w2 = (float)w2d, w3 = (float)w3d;
        float turn1 = (float)(sign * a1d);
        float turn2 = (float)((double)(-turn1) - sign * a2d);
        double a1 = a1d, a2 = a2d;
        Draw = (w1, w2, w3, a1, a2, sign);
        _trace.Add($"SearchForNearbyObjectAction: {(sign > 0 ? "left" : "right")} {a1 * 180 / Math.PI:F1} deg " +
                   $"then {-(a1 + a2) * 180 / Math.PI:F1} deg, waits {w1:F2}/{w2:F2}/{w3:F2} s");

        await Wait(TimeSpan.FromSeconds(w1), cancel);

        // the drive and the head move run together
        var head = _m.Robot.Motion.SetHeadAngleAsync((float)HeadAngleRad, CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2, requireCalibration: false);
        // 0x00546DDE..0x00546E34: |100.0 - speed| < 1e-5 (0x3727C5AC, 0x00547048) selects the TWO-argument DriveStraightAction(robot, distance) (0x4AB098), whose speed is the literal pair at
        // 0x00547268/0x0054726C picked by `it ge; addge r1,#4` at 0x00547174: 0xC2A00000 (-80 mm/s) for a distance < 0, 0x42C80000 (+100) otherwise; any other speed uses the 4-argument form.
        float driveSpeed = MathF.Abs(DefaultDriveSpeedMmps - SpeedMmps) < BitConverter.UInt32BitsToSingle(0x3727C5AC)
            ? (float)DistanceMm >= 0f ? 100f : 80f : SpeedMmps;
        var back = new DriveStraightAction(_m, DistanceMm, driveSpeed).RunAsync(cancel);
        var r = await back;
        try { await head; } catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
        if (r != ActionResult.Success) { _trace.Add($"the backing-off drive: {r}"); return r; }

        await Wait(TimeSpan.FromSeconds(w1), cancel);
        if (await TurnAsync(turn1, cancel) is { } bad1) return bad1;
        await Wait(TimeSpan.FromSeconds(w2), cancel);
        if (await TurnAsync(turn2, cancel) is { } bad2) return bad2;
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
/// <item><b>State 0</b> (0x005BAF9A): a <c>TurnTowardsObjectAction(target, Radians(pi))</c> when the target
/// id is set, <b>then</b> a <see cref="SearchForNearbyObjectAction"/> with <c>-20 mm</c>, <c>100 mm/s</c>
/// and a head angle of <c>-0.0872665</c> (-5 degrees) (C-E3: the turn is added at 0x005BB024 before the
/// search at 0x005BB03E).</item>
/// <item><b>State 1</b> (0x005BB1AA): <c>DriveStraightAction(-20 mm, 20 mm/s)</c>, two
/// <c>TurnInPlaceAction(+0.785398)</c> - 45 degrees each, so 90 in all - then the same nearby search,
/// then a second <c>DriveStraightAction</c>, <c>TurnInPlaceAction(-3pi/4)</c> (0x005BB312),
/// <c>TurnInPlaceAction(-0.785398)</c> (0x005BB34A) and a second nearby search (0x005BB39A).</item>
/// <item><b>State 2</b> (0x005BB066): a <b>two-iteration loop</b> (r8 = -1 at 0x005BB078, <c>add r8,#1</c>
/// at 0x005BB16E, <c>blt</c> at 0x005BB176) of <c>DriveStraightAction(-20 mm, 20 mm/s)</c>, two
/// <c>TurnInPlaceAction(-0.785398)</c> and the nearby search. It has no state-1 tail (C-E3/E5).</item>
/// </list>
///
/// Whether searching is worth starting at all is <c>ShouldBeAbleToFindTarget</c> 0x005BB73C, which asks
/// <c>ObservableObject::IsVisibleFromWithReason</c> with 0.785398 rad and checks the reason against 7.
/// </summary>
public sealed class SearchForBlockHelper
{
    // fidelity: M12-010
    /// <summary>-20 mm: the distance every stage backs off (0xC1A00000).</summary>
    public const double BackOffMm = -20.0;
    /// <summary>100 mm/s for the nearby search's own drive.</summary>
    public const float SearchSpeedMmps = 100f;
    /// <summary>20 mm/s for the drives that bracket states 1 and 2 (0x41A00000).</summary>
    public const float StageSpeedMmps = 20f;
    /// <summary>-5 degrees as binary32 0xBDB2B8C2 (movw/movt 0x005BAFA4..0x005BAFAC): the head angle the nearby search drops to.</summary>
    public static readonly double SearchHeadAngleRad = BitConverter.UInt32BitsToSingle(0xBDB2B8C2);
    /// <summary>45 degrees as binary32 0x3F490FDB (0x005BB214/0x005BB24C; negated 0xBF490FDB at 0x005BB0C4..0x005BB0FC, 0x005BB34A): each of the two turns in states 1 and 2.</summary>
    public static readonly double StageTurnRad = BitConverter.UInt32BitsToSingle(0x3F490FDB);
    /// <summary>-3pi/4 as binary32 0xC016CBE4 (0x005BB312): state 1's first tail turn; NOT 3 * the 45 degree double.</summary>
    public static readonly double StageTailTurnRad = BitConverter.UInt32BitsToSingle(0xC016CBE4);
    /// <summary>Three states, and the helper gives up when the counter passes the last.</summary>
    public const int StateCount = 3;

    private readonly ManipulationSystem _m;
    private readonly EngineRandom? _random;

    /// <summary><paramref name="random"/> is passed to each nearby search; null leaves the search on the context RNG (the engine's helper owns no generator).</summary>
    public SearchForBlockHelper(ManipulationSystem m, uint objectId, EngineRandom? random = null)
    {
        _m = m;
        ObjectId = objectId;
        _random = random;
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
                1 => await SweepState1Async(cancel),
                _ => await SweepState2Async(cancel),
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

    /// <summary>
    /// State 0: <c>TurnTowardsObjectAction(target, pi)</c> first, then the nearby search (C-E3). The turn is
    /// added only when the target id is set (0x005BAFDA) and the search unconditionally (0x005BB03E).
    /// </summary>
    private async Task<ActionResult> LookAroundAsync(CancellationToken cancel)
    {
        // The turn's result is discarded on purpose: 0x005BB020 (movs r3, #1) / 0x005BB024 (blx [vtbl+0x20] = ICompoundAction::AddAction(action,
        // bool ignoreFailure, bool emit), vt.CompoundActionSequential+0x28) adds the TurnTowardsObjectAction with ignoreFailure = 1 (the nearby
        // search at 0x005BB038 has ignoreFailure = 0), and CompoundActionSequential::UpdateInternal 0x0054F70C asks ShouldIgnoreFailure 0x0054F171
        // for a failed child and moves on when it is set. So a failed turn does not end the compound.
        await _m.TurnTowardsObjectAsync(ObjectId, EngineRadians.Pi, cancel);
        return await NearbySearchAsync(cancel);
    }

    /// <summary>
    /// State 1: <c>DriveStraight(-20, 20)</c>, two <c>TurnInPlace(+0.785398)</c>, the nearby search, a
    /// second <c>DriveStraight</c>, <c>TurnInPlace(-3pi/4)</c> (0x005BB312), <c>TurnInPlace(-0.785398)</c>
    /// (0x005BB34A) and a second nearby search (0x005BB39A) (C-E3/G7.8).
    /// </summary>
    // fidelity: M12-010
    private async Task<ActionResult> SweepState1Async(CancellationToken cancel)
    {
        // Every one of the eight children is added with ICompoundAction::AddAction(action, ignoreFailure = 1, ..): movs r3,#1 at 0x005BB1E6, 0x005BB21E, 0x005BB256,
        // 0x005BB2AA, 0x005BB2E4, 0x005BB31C, 0x005BB354, 0x005BB3A8 ([vtbl+0x20] is AddAction, the CompoundActionSequential vtable relocation). CompoundActionSequential::UpdateInternal
        // 0x0054F70C on a failed child runs RunCallbacks, ShouldIgnoreFailure 0x0054F171 and, when it holds, MoveToNextAction (0x0054F808..0x0054F822): the compound goes on after
        // ANY failed child. An ignored child with ANY failed result, a cancel category included, continues: the tbb table at 0x0054F802 sends categories 2 and 3 to 0x0054F808, 4 (Retry) to 0x0054F904 (RetriesRemain(), falling to 0x0054F808 when none remain), and ShouldIgnoreFailure 0x0054F171 finds the ignoreFailure=1 functor (`movs r0,#1; bx lr` at 0x0054FE4E), true for any result, so MoveToNextAction 0x004AB86C runs; only ignoreFailure=0 ends the compound (0x0054F88A). The helper ends early only when its own token is cancelled (CHOICE: a cancelled token ends this stack's run).
        async Task<bool> Step(Func<Task<ActionResult?>> child, string what)
        {
            var failed = await child();
            if (failed is { } bad) _trace.Add($"{what}: {bad} (ignored: AddAction ignoreFailure = 1)");
            return !cancel.IsCancellationRequested;
        }
        if (!await Step(() => DriveAsync(cancel), "the sweep's drive 1")) return ActionResult.CancelledWhileRunning;
        for (int i = 0; i < 2; i++)
            if (!await Step(() => TurnAsync(+StageTurnRad, cancel), $"the sweep's turn {i + 1}")) return ActionResult.CancelledWhileRunning;
        if (!await Step(NearbyAsChild, "the first nearby search")) return ActionResult.CancelledWhileRunning;
        if (!await Step(() => DriveAsync(cancel), "the sweep's drive 2")) return ActionResult.CancelledWhileRunning;
        if (!await Step(() => TurnAsync(StageTailTurnRad, cancel), "the sweep's tail turn 1")) return ActionResult.CancelledWhileRunning;
        if (!await Step(() => TurnAsync(-StageTurnRad, cancel), "the sweep's tail turn 2")) return ActionResult.CancelledWhileRunning;
        if (!await Step(NearbyAsChild, "the second nearby search")) return ActionResult.CancelledWhileRunning;
        return ActionResult.Success;

        async Task<ActionResult?> NearbyAsChild() { var r = await NearbySearchAsync(cancel); return r == ActionResult.Success ? null : r; }
    }

    /// <summary>
    /// State 2: a <b>two-iteration loop</b> (r8 = -1 at 0x005BB078, add r8,#1 at 0x005BB16E, blt at
    /// 0x005BB176; all four children ignoreFailure = 0: r3 = 0 at 0x005BB0A8, 0x005BB0D8, 0x005BB10C, 0x005BB15E, so the first failure ends the compound) of <c>DriveStraight(-20, 20)</c>, <c>TurnInPlace(-0.785398)</c>,
    /// <c>TurnInPlace(-0.785398)</c>, the nearby search. It has no state-1 tail (C-E3/E5).
    /// </summary>
    private async Task<ActionResult> SweepState2Async(CancellationToken cancel)
    {
        for (int i = 0; i < 2; i++)
        {
            if (await DriveAsync(cancel) is { } d) return d;
            for (int j = 0; j < 2; j++)
                if (await TurnAsync(-StageTurnRad, cancel) is { } bad) { _trace.Add($"the sweep's turn {j + 1}: {bad}"); return bad; }
            var s = await NearbySearchAsync(cancel);
            if (s != ActionResult.Success) return s;
        }
        return ActionResult.Success;
    }

    private async Task<ActionResult?> DriveAsync(CancellationToken cancel)
    {
        var r = await new DriveStraightAction(_m, BackOffMm, StageSpeedMmps).RunAsync(cancel);
        if (r != ActionResult.Success) { _trace.Add($"the sweep's drive: {r}"); return r; }
        return null;
    }

    private async Task<ActionResult> NearbySearchAsync(CancellationToken cancel)
    {
        var search = new SearchForNearbyObjectAction(_m, ObjectId, BackOffMm, SearchSpeedMmps,
                                                     SearchHeadAngleRad, _random) { Wait = Wait };
        var r = await search.RunAsync(cancel);
        _trace.AddRange(search.Trace);
        return r;
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
