using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// The engine's <c>FlipBlockAction</c> (0x0055EC80..0x0055F1C0, a <c>CompoundActionSequential</c>, RobotActionType
/// 0xF). Constructor constants, exactly as stored (M13-002): +0x12C = 150.0 (0x43160000), +0x130 = 20.0
/// (0x41A00000), +0x134 = 45.0 (0x42340000), +0x138 = 40.0 (0x42200000), +0x13C = -1, +0x140 = 1.
///
/// <b>The role of each offset (M13-002, gap pass 3).</b> +0x12C is the drive speed (150 mm/s),
/// +0x130 the drive-past distance (20 mm), +0x134 the approach lift height (45 mm), +0x138 the lift trigger
/// distance (40 mm), +0x13C the queued-lift action id (-1 when none) and +0x140 shouldCheckPreActionPose.
///
/// <c>Init</c> 0x0055EDC8: the object must be located; <c>IDockAction::GetPreActionPoses</c> is called (flag A = <c>+0x140</c>, tolerance 0x3DB2B8C2, M12-031) and only its
/// result code is acted on; reactions are locked; a <c>MoveLiftToHeightAction(45, 5.0, 0)</c>
/// and a <c>DriveStraightAction(distance to the cube's centre + 20, 150, playAnim)</c> are added.
/// <c>CheckIfDone</c> 0x0055F074 (M13-028): the embedded compound's Update first; when it is not RUNNING the object is marked Unknown and the result returned; when RUNNING and the object
/// pose with respect to the robot has a 3-D norm below <b>40 mm</b> and +0x13C is still -1 it queues <c>MoveLiftToHeightAction(carry height, tolerance 5.0)</c> IN_PARALLEL
/// (<c>QueueActionPosition</c> 5) and stores its id at +0x13C. (The "5.0" is the constructor's TOLERANCE, not a speed: 0x40A00000 at 0x0055EF42 / 0x0055F148.)
/// </summary>
// fidelity: M13-002
public sealed class FlipBlockAction
{
    // M13-002: the constructor's exact stores, by offset and role.
    public const double Offset12C = 150.0;      // drive speed (mm/s)
    public const double Offset130 = 20.0;       // drive-past distance (mm)
    public const double Offset134 = 45.0;       // approach lift height (mm)
    public const double Offset138 = 40.0;       // lift trigger distance (mm)
    public const int Offset13C = -1;            // queued-lift action id (-1 = none)
    public const int Offset140 = 1;             // shouldCheckPreActionPose

    public const float DriveSpeedMmps = 150f;
    public const double DrivePastMm = 20.0;
    public const double ApproachLiftHeightMm = 45.0;
    public const double LiftTriggerDistanceMm = 40.0;
    public const double PreActionAngleToleranceRad = 0.0872665;
    /// <summary>The tolerance <c>Init</c> passes <c>GetPreActionPoses</c>: the float 0x3DB2B8C2 (5 degrees; M12-031, 0x0055EE10..0x0055EE1C).</summary>
    // fidelity: M12-031
    public static readonly double InitAngleToleranceRad = BitConverter.UInt32BitsToSingle(0x3DB2B8C2);
    // The MoveLiftToHeightAction(Robot&amp;, height, tolerance, variability) call sites (Init 0x0055EF3A..0x0055EF4A: r2 = [this+0x134] = 45, r3 = 0x40A00000 = 5.0, [sp] = 0; CheckIfDone 0x0055F148
    // the same shape) pass 5.0 as the TOLERANCE in mm, and <c>CozmoMotion.SetLiftHeightAsync</c> already sends the game tolerance 5.0 (GameLiftToleranceMm, M4-016). The action's own speed, acceleration
    // and duration are the MoveLiftToHeightAction constructor's defaults +0x8C = 10.0, +0x90 = 20.0, +0x88 = 0 (0x00548A68..0x00548A78, M4 MA13), which is what
    // <c>SetLiftHeightAsync</c> sends when it is given no speed. M13-002's "speed 5.0" and this stack's 5 rad/s stand-in both misread that tolerance, so no speed is passed here any more.

    /// <summary>
    /// The three-D norm <c>FlipBlockAction::Init</c> (0x0055EED6..0x0055EEFA) and <c>CheckIfDone</c> (0x0055F0E6..0x0055F10A) take of the object pose's translation with respect to the robot
    /// pose, in binary32: <c>vldr s0,[T+0x20]; vmul s0,s0,s0</c>, then <c>s0 + [T+0x24]^2</c>, then <c>+ [T+0x28]^2</c>, <c>vsqrt.f32</c>.
    /// </summary>
    // fidelity: M13-002
    internal static float Norm3(Vec3 t)
    {
        float x = (float)t.X, y = (float)t.Y, z = (float)t.Z;
        float s = x * x;
        s = s + y * y;
        s = s + z * z;
        return MathF.Sqrt(s);
    }

    private readonly ManipulationSystem _m;
    public FlipBlockAction(ManipulationSystem m, uint objectId) { _m = m; ObjectId = objectId; }

    public uint ObjectId { get; }
    /// <summary>
    /// <c>SetShouldCheckPreActionPose</c> (0x0055EDC2), the byte <c>[this+0x140]</c> (constructor default 1): it is FLAG A of the <c>GetPreActionPoses</c> request <c>Init</c> makes
    /// (M12-031), so clearing it stops a too-far robot from being refused; it does NOT skip the call.
    /// </summary>
    public bool CheckPreActionPose { get; set; } = true;
    public bool LiftRaised { get; private set; }
    private Task<MotionOutcome>? _raise;
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    /// <summary>How many M8 calls of <c>Init</c> (DisableReactionsWithLock, IActionRunner::Update) were skipped: they are not modelled (M13-028).</summary>
    public int M8CallsNotModelled { get; private set; }

    /// <summary>
    /// The result of the INITIAL lift move when it did not complete: the engine's own code from <see cref="MotionOutcome.EngineResult"/> (0x04000004 StoppedMakingProgress, 0x03000016 send failed,
    /// 0x03000018 IAction timeout; M4-016 MA17, 0x00540E80/0x005493F6..0x00549508). The engine's MoveLiftToHeightAction carries the default 30.0 s IAction timeout (0x0052B0C2), tested on the
    /// engine clock; when it fires the action fails with 0x03000018, so the flip returns that code for a timeout. The ActionResult.Timeout fallback remains only for an outcome with no engine code
    /// (e.g. a robot-removed wait). The queued carry lift never feeds the flip's result.
    /// </summary>
    // fidelity: M13-028
    public static ActionResult LiftFailureResult(MotionOutcome outcome) => outcome.EngineResult is { } code ? (ActionResult)code : ActionResult.Timeout;

    /// <summary>
    /// How many queued carry lifts were still unfinished when the flip ended, so the destructor's <c>ActionList::Cancel(id)</c> (0x0055ED6C..0x0055ED7A) should have cancelled them. MISSING: the stack has
    /// no handle to cancel a lift move that <c>CozmoMotion.SetLiftHeightAsync</c> is waiting on (the move ends only by its own completion or its engine-clock IAction timeout, which stops a moving lift,
    /// M4-015/M4-016), so the cancel is NOT done and is counted here. This is a limit of this stack, NOT a source gap: the engine's destructor cancels the queued lift promptly at flip end (it is
    /// typically still moving, rising 45 -> 92 mm after a trigger only 40 mm out), whereas here it runs on toward 92 mm and, if it is still not in position when its 30 s engine-clock timeout fires
    /// (Motion.cs StopLift whenever the lift is moving), that late stop could hit a LATER action's lift. An existing Motion stop/cancel API for a pending wait would be the option; none is used because
    /// no record supports it.
    /// </summary>
    public int QueuedLiftCancelsNotModelled { get; private set; }

    private async Task<ActionResult> RunEmbeddedCompound(float driveDistance, CancellationToken cancel)
    {
        var lift = await _m.Robot.Motion.SetLiftHeightAsync((float)ApproachLiftHeightMm, requireCalibration: false);
        if (lift.Result != MotionResult.Acknowledged)
        {
            var failure = LiftFailureResult(lift);
            _trace.Add($"FlipBlockAction: the lift move did not complete ({lift.Result}); the compound ends with {failure}");
            return failure;
        }
        // The engine destroys the embedded compound in the destructor (0x0055ED68 PrepForCompletion on this+0x80, then ~ICompoundAction 0x0055ED8E), so a flip that has already ended never sends the
        // drive. The lift wait cannot be interrupted (SetLiftHeightAsync takes no token), so the stack's stand-in for that destruction is to check the token as soon as the wait returns and before
        // the DriveStraightAction is started: once the flip has ended no path goes on the wire. (Nothing is cancelled inside CheckIfDone itself; 0x0055F18C..0x0055F1CA only warns and returns 0x03000004.)
        if (cancel.IsCancellationRequested)
        {
            _trace.Add("FlipBlockAction: the flip has ended; the embedded compound is not continued (stand-in for its destruction in the destructor, 0x0055ED68)");
            return ActionResult.CancelledWhileRunning;
        }
        return await new DriveStraightAction(_m, driveDistance, DriveSpeedMmps).RunAsync(cancel);
    }

    /// <summary>
    /// One call of <c>FlipBlockAction::CheckIfDone</c> 0x0055F074 (M13-028): <paramref name="compoundResult"/> is the embedded compound's Update result (null = RUNNING, 0x01000000). The object is looked
    /// up EVERY tick with <c>GetLocatedObjectByIdHelper(id, -1)</c> (0x0055F090), before the RUNNING test (0x0055F094). RUNNING with no located object: warn and 0x03000004 (0x0055F09C..0x0055F1C4).
    /// Not RUNNING: with no object warn and return the compound's result (0x0055F17A cbz), else <c>MarkObjectUnknown(obj, true)</c> (0x0055F186) and the compound's result. RUNNING with the object: the
    /// object pose with respect to the robot pose has a 3-D norm (0x0055F0E0..0x0055F10A) and, when it is below <see cref="LiftTriggerDistanceMm"/> ([this+0x138], strict, NaN skipped) and
    /// [this+0x13C] == -1 (<see cref="LiftRaised"/> false), the carry-height MoveLiftToHeightAction(preset 2, tolerance 5.0, 0) is queued on the ROBOT'S ActionList (0x0055F160 ldr r0,[r0,#0x250];
    /// 0x0055F164 position 5; 0x0055F16A QueueAction), its id goes to [+0x13C] and the tick returns RUNNING (null). The flip NEVER waits on that lift. Returns null for RUNNING.
    /// </summary>
    // fidelity: M13-028
    internal ActionResult? CheckIfDoneTick(ActionResult? compoundResult)
    {
        var obj = _m.World.GetLocatedObjectById(ObjectId);                                        // per tick, before the RUNNING test
        if (compoundResult is { } done)
        {
            if (obj is null) { _trace.Add($"FlipBlockAction.CheckIfDone: object {ObjectId} is not located (warning); compound result {done}"); return done; }
            _m.World.MarkUnknown(ObjectId);                                                       // MarkObjectUnknown(obj, true), 0x0055F186
            _trace.Add($"FlipBlockAction.CheckIfDone: compound result {done}; object {ObjectId} marked Unknown");
            return done;
        }
        if (obj is null)
        {
            _trace.Add($"FlipBlockAction.CheckIfDone: object {ObjectId} is not located while the compound is running (warning); BadObject");
            return ActionResult.BadObject;
        }
        if (!LiftRaised && _m.RobotPose() is { } now)
        {
            var t = obj.Pose.WithRespectTo(now).Translation;                                      // GetWithRespectTo at 0x0055F0E0
            float norm = Norm3(t);                                                                 // [T+0x20]^2, then [T+0x24]^2 and [T+0x28]^2, vsqrt.f32 (binary32)
            if (norm < (float)LiftTriggerDistanceMm)
            {
                LiftRaised = true;                                                                // [this+0x13C] = the queued action's id
                _trace.Add($"FlipBlockAction.CheckIfDone: within {LiftTriggerDistanceMm} mm (3-D), lift to carry height ({LiftPresets.CarryMm} mm) queued on the robot's action list; the flip does not wait on it");
                // the queued action's byte +0x56 = 1 (0x0055F152..0x0055F154): IActionRunner::Update neither tests nor takes the lift track lock for it (0x00540428..0x00540434), so it
                // runs even while the approach lift move of the embedded compound still holds the track, and its end releases nothing (0x005408EC..0x005408F0)
                _raise = _m.Robot.Motion.SetLiftHeightAsync(LiftPresets.CarryMm, requireCalibration: false, suppressTrackLocking: true,
                                                            position: QueueActionPosition.InParallel);
            }
        }
        return null;
    }

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var target = _m.World.GetLocatedObjectById(ObjectId);
        if (target is null) { _trace.Add("FlipBlockAction.Init.NullObject"); return ActionResult.BadObject; }
        var robot = _m.RobotPose();
        if (robot is null) return ActionResult.Abort;
        // fidelity: M12-031
        // FlipBlockAction::Init 0x0055EDC8 calls IDockAction::GetPreActionPoses at 0x0055EE5E EVEN WHEN +0x140 is 0, with flag A = the byte [this+0x140], tolerance 0x3DB2B8C2, and
        // branches ONLY on the RESULT code (0x0055EE62..0x0055EE66), never on the in-position byte. The request's distanceFromMarker (0) and useApproachAngle (false) are
        // not itemised for this caller (a CHOICE: the values the other flip request, GetPossiblePoses, records).
        var pre = DockPreActionPoses.Get(new PreActionPoseInput(target, PreActionType.Flipping, FlagA: CheckPreActionPose, InitAngleToleranceRad, 0.0, UseApproachAngle: false, 0.0),
                                         robot.Value, _m.Docking.Carrying.CarriedObjectId, () => _m.GetObstacles(robot.Value), _trace);
        if (pre.Result != ActionResult.Success) { _trace.Add($"FlipBlockAction.Init: GetPreActionPoses -> {pre.Result}"); return pre.Result; }
        // fidelity: M13-028
        // 0x0055EEC6 DisableReactionsWithLock and 0x0055EF7E IActionRunner::Update are M8's: NOT modelled here (a counted trace, not a claim). The Update is replaced by awaiting the compound.
        // The destructor's BehaviorManager::RemoveDisableReactionsLock (0x0055ED88) is counted at the end (below).
        M8CallsNotModelled += 2;
        _trace.Add("FlipBlockAction.Init: DisableReactionsWithLock (0x0055EEC6) and IActionRunner::Update (0x0055EF7E) are not modelled (M8); the embedded compound is awaited instead");
        using var compoundCancel = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        try
        {
            // 0x0055EED6..0x0055EEFA: the drive distance is the THREE-D norm of the object pose with respect to the robot pose ([T+0x20]^2, then [T+0x24]^2 and [T+0x28]^2, vsqrt.f32) plus
            // [this+0x130]; the GetWithRespectTo result (0x0055EEAE) is not checked by the engine (a failure leaves the pose default; it cannot fail here).
            var rel = target.Pose.WithRespectTo(robot.Value).Translation;
            // 0x0055EF14..0x0055EF1C: vadd.f32 s0, norm, [this+0x130] (20.0f): the sum is binary32 too
            float dist = Norm3(rel) + (float)DrivePastMm;
            _trace.Add($"FlipBlockAction: driving {dist:F1} mm at {DriveSpeedMmps} mm/s with the lift at {ApproachLiftHeightMm} mm");
            // the embedded CompoundActionSequential at this+0x80 (ctor 0x0055ECE0): MoveLiftToHeightAction FIRST (0x0055EF3A..0x0055EF58), DriveStraightAction SECOND (0x0055EF64..0x0055EF70), both
            // AddAction(..., false, false): the drive starts only after the lift move has completed, and a failed lift move ends the compound with its result (MoveLiftToHeightAction's mapping).
            var compound = RunEmbeddedCompound(dist, compoundCancel.Token);
            // CheckIfDone once per tick (see CheckIfDoneTick); the tick is a 10 ms poll here (the engine's tick period is not in the inventory).
            while (true)
            {
                ActionResult? compoundResult = compound.IsCompleted ? await compound : null;
                if (CheckIfDoneTick(compoundResult) is { } result)
                {
                    if (compoundResult is null) compoundCancel.Cancel();                        // BadObject while RUNNING: stand-in for the destructor's destruction of the compound (the engine does NOT cancel inside CheckIfDone)
                    return result;
                }
                await Task.WhenAny(compound, Task.Delay(10, CancellationToken.None));
            }
        }
        finally
        {
            // ~FlipBlockAction 0x0055ED54: cancel the queued lift by id when [this+0x13C] != -1 (ActionList::Cancel, 0x0055ED6C..0x0055ED7A) and BehaviorManager::RemoveDisableReactionsLock (0x0055ED88)
            if (_raise is { IsCompleted: false })
            {
                QueuedLiftCancelsNotModelled++;
                _trace.Add("FlipBlockAction destructor: the queued carry lift is unfinished; ActionList::Cancel(id) is not modelled (no cancel handle, MISSING)");
            }
            M8CallsNotModelled++;
        }
    }
}

/// <summary>
/// The engine's <c>DriveAndFlipBlockAction</c> (0x0055E208): an <c>IDriveToInteractWithObject</c> of a
/// <c>FlipBlockAction</c>: drive to a Flipping pre-action pose, then flip. M12-035 (re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q3,
/// verified in 20260929-R-VIS-verify-M12-gap3.md Q3): the constructor (ActionType 5, name "DriveToAndFlipBlock") creates the <c>FlipBlockAction</c> (0x144 bytes, <see cref="Flip"/>,
/// its <c>+0x140 = 1</c>) and installs <b>L1</b> (0x0055E2CA; operator() 0x0055F366) as the <c>DriveToObjectAction</c>'s <c>+0x150</c> pose function; <see cref="ShouldDriveToClosestPreActionPose"/>
/// (0x0055E3B0) installs <b>L2</b> (0x0055E3E0; 0x0055F4C6), L1 with <c>closestOnly = b</c>. Both call <see cref="GetPossiblePoses"/> (0x0055E438) last. The flip
/// itself runs with the <c>+0x140</c> flag L1 leaves it (below), NOT with the check switched off: the earlier <c>CheckPreActionPose = false</c> here had no source.
///
/// <para>L1 / L2: <c>if (!*inPos &amp;&amp; [this+0x100] &gt;= 0) { r = helper(robot, poses, [this+0x100]); *inPos = r; flip-&gt;[+0x140] = !r; }</c> and then, always,
/// <c>GetPossiblePoses(robot, obj, poses, inPos, closestOnly)</c>. The helper (0x0055F3F8) is true only when <c>threshold &gt;= 0</c> and some pose of the vector is nearer the robot than
/// the threshold; the vector is EMPTY at the recorded <c>InitHelper</c> call (0x00559048..0x00559054), so the helper returns 0. <b>INDETERMINATE SOURCE:</b> <c>[this+0x100]</c>
/// (<see cref="Field0x100"/>) is never written (uninitialised heap of <c>operator new(0x108)</c>), so whether the block runs is indeterminate; this stack does NOT invent a value for it
/// (null). With the empty vector the block, if it runs, leaves <c>*inPos = 0</c> and <c>flip+0x140 = 1</c>, and if it does not run leaves <c>+0x140</c> at the constructor's 1: BOTH
/// leave <c>+0x140 = 1</c>, so the block is not modelled for null and <see cref="Flip"/> keeps <see cref="FlipBlockAction.CheckPreActionPose"/> true. When the weak lock of
/// <c>+0xF8</c> is null the engine stores through the absolute address 0x140 (0x0055F3B6, 0x0055F516): a null dereference. That crash is NOT reproduced; <see cref="Flip"/> is never
/// null here.</para>
///
/// <para><c>DriveToObjectAction::CheckIfDone</c> (0x00559A86..0x00559AE6) hands the function a FRESH EMPTY vector and an in-position bool of 0 (0x00559A9C). L1..L4 never write that
/// bool (the helper over the empty vector returns 0 and <c>GetPossiblePoses</c> never touches it), so the flag stays 0 and the drive ends with 0x04000001 (DidNotReachPreActionPose)
/// after the drive, whatever the function returned. The outer compound IGNORES that failure (<c>AddAction(inner, true, false)</c>, 0x0055B370), so <see cref="RunAsync"/> goes on to the
/// turns and the flip and returns the flip's result; the 0x04000001 is never the action's result.</para>
/// </summary>
// fidelity: M12-035
public sealed class DriveAndFlipBlockAction
{
    private readonly ManipulationSystem _m;
    public DriveAndFlipBlockAction(ManipulationSystem m, uint objectId)
    {
        _m = m; ObjectId = objectId;
        Flip = new FlipBlockAction(m, objectId);          // 0x0055E27C; FlipBlockAction's own constructor sets +0x140 = 1
    }
    public uint ObjectId { get; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();
    public FlipBlockAction Flip { get; }

    /// <summary><c>DriveAndFlipBlockAction+0x100</c>: never written by the engine; null is "indeterminate" (see the class remarks). Setting it makes L1/L2 run the helper block.</summary>
    public float? Field0x100 { get; set; }

    private bool? _closestOnly;     // null: L1 is installed; a value: L2 with closestOnly = b

    /// <summary><c>DriveAndFlipBlockAction::ShouldDriveToClosestPreActionPose(bool b)</c> 0x0055E3B0: installs L2 (closestOnly = <paramref name="b"/>) when the inner drive exists.</summary>
    public void ShouldDriveToClosestPreActionPose(bool b) => _closestOnly = b;

    /// <summary>
    /// The maximum turn towards the last observed face after the drive, the constructor's Radians argument.
    /// <c>IDriveToInteractWithObject</c> 0x0055B1F4 adds a <c>TurnTowardsLastFacePoseAction</c> with it (and the
    /// say-name flag) after the drive when it is greater than zero (0x0055B380..0x0055B3C0), with its failure
    /// ignored (AddAction(action, true) at 0x0055B3D4); zero adds nothing. The constructor's trailing float is
    /// never read (0x0055E208 loads its stack arguments only up to +0x7C).
    /// </summary>
    public double MaxTurnTowardsFaceRad { get; init; }

    /// <summary>The say-name bool the constructor forwards to <c>IDriveToInteractWithObject</c> (0x0055E23E); <c>BehaviorKnockOverCubes</c> passes 0 (0x005C3532/0x005C353A).</summary>
    // fidelity: M13-014
    public bool SayName { get; init; }

    /// <summary>The installed L1 / L2 function as a delegate (a seam for tests and for a caller that wants to install it on its own drive).</summary>
    public PosesFunction PosesFunctionL1L2 => InstalledPosesFunction;

    /// <summary>
    /// L1 (<c>closestOnly</c> false) or L2 (<c>closestOnly = b</c>): the pose function the drive uses. See the class remarks for the helper block and why it changes nothing modelled.
    /// </summary>
    // fidelity: M12-035
    private ActionResult InstalledPosesFunction(ObservableObject obj, out IReadOnlyList<PreActionPose> poses, ref bool inPos)
    {
        if (!inPos && Field0x100 is { } threshold && threshold >= 0)
        {
            var robot = _m.RobotPose();
            bool r = robot is { } rp && PoseNearerThan(rp, Array.Empty<Pose3d>(), threshold);     // the local vector is empty at every call this stack models
            inPos = r;
            Flip.CheckPreActionPose = !r;                                                          // flip->[+0x140] = !r
        }
        return GetPossiblePoses(_m, obj, out poses, _closestOnly ?? false, _trace);
    }

    /// <summary>
    /// The helper 0x0055F3F8: true when <paramref name="thresholdSq"/> &gt;= 0 and some pose's <c>ComputeDistanceSQBetween(Robot::GetPose(), pose)</c> is below it (its return is ignored).
    /// Over an empty vector it is false. The body of <c>ComputeDistanceSQBetween</c> is not in the inventory (MISSING), so a non-empty vector throws <see cref="NotSupportedException"/>.
    /// </summary>
    // fidelity: M12-035
    public static bool PoseNearerThan(Pose3d robot, IReadOnlyList<Pose3d> poses, float thresholdSq)
    {
        if (!(thresholdSq >= 0)) return false;
        if (poses.Count == 0) return false;
        throw new NotSupportedException("MISSING: ComputeDistanceSQBetween's body (planar or 3-D, the frames) is not in the inventory (M12-035 helper 0x0055F3F8).");
    }

    /// <summary>
    /// <c>DriveAndFlipBlockAction::GetPossiblePoses(robot, obj, poses, bool&amp;, closestOnly)</c> 0x0055E438 (static; M12-035): the request is {obj, ActionType 5, flag A 0, tolerance 0x3DB2B8C2,
    /// distance 0, useApproachAngle 0} for <c>IDockAction::GetPreActionPoses</c>; a non-zero result is returned; <c>FaceWorld::GetLastObservedFace(pose, true)</c> (the engine's second
    /// bool is mapped to <c>namedOnly</c>, the way M14 maps its <c>false</c>: a CHOICE); an empty list is 0x03000010; <c>closestOnly</c> pushes the WORLD pose of
    /// <c>poses[closestIndex]</c> and returns 0; otherwise every pose is taken with respect to the robot pose and the nearest A and second-nearest B by 3-D distance are kept
    /// (FLT_MAX start, strict <c>&lt;</c>): one pose gives A; a known face with both poses expressed against it gives A when dist(A, face) &gt; dist(B, face) else B (a tie or NaN
    /// gives B); otherwise A when <c>A.y &gt;= B.y</c> (robot frame) else B. The chosen pose is pushed and 0 returned. The <c>bool&amp;</c> is neither read nor written.
    /// </summary>
    // fidelity: M12-035
    public static ActionResult GetPossiblePoses(ManipulationSystem m, ObservableObject obj, out IReadOnlyList<PreActionPose> posesOut, bool closestOnly, List<string>? trace = null)
    {
        posesOut = Array.Empty<PreActionPose>();
        var robotOpt = m.RobotPose();
        if (robotOpt is null) return ActionResult.Abort;
        var robot = robotOpt.Value;
        var output = DockPreActionPoses.Get(new PreActionPoseInput(obj, PreActionType.Flipping, FlagA: false, FlipBlockAction.InitAngleToleranceRad, 0.0, UseApproachAngle: false, 0.0),
                                            robot, m.Docking.Carrying.CarriedObjectId, () => m.GetObstacles(robot), trace);
        if (output.Result != ActionResult.Success) { trace?.Add($"DriveAndFlipBlockAction.GetPossiblePoses: GetPreActionPoses -> {output.Result}"); return output.Result; }
        var face = m.Vision.Faces.GetLastObservedFace(namedOnly: true);
        if (output.Poses.Count == 0) { trace?.Add("DriveAndFlipBlockAction.GetPossiblePoses: no pre-action poses"); return ActionResult.NoPreActionPoses; }
        if (closestOnly)
        {
            posesOut = new[] { output.Poses[output.ClosestIndex] };
            return ActionResult.Success;
        }
        float bestA = float.MaxValue, bestB = float.MaxValue; int a = -1, b = -1;
        for (int i = 0; i < output.Poses.Count; i++)
        {
            var t = output.Poses[i].WorldPose.WithRespectTo(robot).Translation;
            float d = MathF.Sqrt((float)(t.X * t.X + t.Y * t.Y + t.Z * t.Z));
            if (d < bestA) { bestB = bestA; b = a; bestA = d; a = i; }
            else if (d < bestB) { bestB = d; b = i; }
        }
        if (a < 0) throw new NotSupportedException("the engine reads an uninitialised nearest pose when every distance is NaN (0x0055E688..0x0055E75C); not reproduced");
        int chosen;
        if (output.Poses.Count == 1) chosen = a;
        else
        {
            if (b < 0) throw new NotSupportedException("the engine reads an uninitialised second-nearest pose (0x0055E688..0x0055E75C); not reproduced");
            var pa = output.Poses[a].WorldPose; var pb = output.Poses[b].WorldPose;
            if (face is not null)
            {
                float da = Distance(pa, face.HeadPose), db = Distance(pb, face.HeadPose);
                chosen = da > db ? a : b;                                                             // ties and NaN give B
            }
            else
            {
                float ya = (float)pa.WithRespectTo(robot).Translation.Y, yb = (float)pb.WithRespectTo(robot).Translation.Y;
                chosen = ya >= yb ? a : b;
            }
        }
        posesOut = new[] { output.Poses[chosen] };
        return ActionResult.Success;
    }

    private static float Distance(Pose3d pose, Pose3d frame)
    {
        var t = pose.WithRespectTo(frame).Translation;
        return MathF.Sqrt((float)(t.X * t.X + t.Y * t.Y + t.Z * t.Z));
    }

    /// <summary><c>Radians::operator&gt;(maxTurn, Radians(0))</c> (0x0055B38C, M13-028): the rescaled difference is positive and not within the 1e-5 epsilon 0x3727C5AC.</summary>
    // fidelity: M13-028
    public static bool MaxTurnIsPositive(double maxTurnRad) =>
        EngineRadians.Rescale(maxTurnRad) > 0 && !EngineRadians.IsNear(maxTurnRad, 0.0, BitConverter.UInt32BitsToSingle(0x3727C5AC));

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        // fidelity: M13-028
        // IDriveToInteractWithObject ctor 0x0055B258..0x0055B264: when the robot is carrying the object's id it warns and adds NO drive, wait-lambda or turn actions (branch to 0x0055B45E); the
        // derived constructor still adds the FlipBlockAction, whose own Init then answers 0x03000004 for a carried object (GetPreActionPoses, M12-031).
        if (_m.Docking.Carrying.IsCarrying(ObjectId))
        {
            _trace.Add("DriveAndFlipBlockAction: the robot is carrying the object: no drive, wait or turn actions are added (0x0055B258..0x0055B264)");
            var carried = await Flip.RunAsync(cancel);
            _trace.AddRange(Flip.Trace);
            return carried;
        }
        var drive = new DriveToObjectAction(_m, ObjectId, PreActionType.Flipping)
        {
            PosesFunction = InstalledPosesFunction,
        };
        var d = await drive.RunAsync(cancel);
        _trace.AddRange(drive.Trace);
        // fidelity: M12-035, M13-014
        // IDriveToInteractWithObject 0x0055B1F4: the drive and a WaitForLambda action (type 0x33, ctor 0x0055B554, timeout FLT_MAX) are added to an INNER sequential compound with
        // AddAction(action, false, false) (0x0055B2E8, 0x0055B356); the inner compound is added to the outer one with AddAction(inner, true, false) (0x0055B370: the first bool,
        // ignoreFailure, is 1), and so are the two turns. The outer compound therefore IGNORES a failed drive and goes on to the turns and the flip; the inner compound being sequential,
        // a failed drive means the wait-lambda is not reached. STAND-IN, MISSING (M8): the WaitForLambda body (its lambda's condition) and ICompoundAction's ignoreFailure handling in its
        // UpdateInternal are unread, so the drive's result is simply ignored here and the wait is not modelled. The result of the action is the flip's (the last action); the derived
        // constructor adds the FlipBlockAction with (false, false) (0x0055E2EE movs r3,#0; 0x0055E2F0), so a failed flip fails the compound and is returned.
        if (d != ActionResult.Success) _trace.Add($"DriveToObjectAction -> {d}, ignored by the outer compound (AddAction ignoreFailure = 1); WaitForLambda not reached/modelled (M8 MISSING)");
        // fidelity: M13-014
        // IDriveToInteractWithObject 0x0055B1F4 adds TWO actions when maxTurn > 0: a
        // TurnTowardsLastFacePoseAction (vtable overwritten from TurnTowardsFaceAction at
        // 0x0055B3C4..0x0055B3D8) and a TurnTowardsObjectAction (0x0055B42E/0x0055B43C), both with
        // failure ignored (AddAction(action, true)). The sayName argument is the caller's
        // (DriveAndFlipBlockAction forwards its own at 0x0055E23E).
        if (MaxTurnIsPositive(MaxTurnTowardsFaceRad))
        {
            using var face = new TurnTowardsLastFacePoseAction(_m.Vision, MaxTurnTowardsFaceRad, SayName);
            var f = await face.RunAsync(cancel);
            _trace.Add($"TurnTowardsLastFacePose (max {MaxTurnTowardsFaceRad:F3} rad): {f}, ignored");
            bool t = await _m.TurnTowardsObjectAsync(ObjectId, MaxTurnTowardsFaceRad, cancel);
            _trace.Add($"TurnTowardsObjectAction (max {MaxTurnTowardsFaceRad:F3} rad): {t}, ignored");
        }
        var r = await Flip.RunAsync(cancel);
        _trace.AddRange(Flip.Trace);
        return r;
    }
}

/// <summary>
/// The engine's <c>DriveToFlipBlockPoseAction</c> (ctor 0x0055EB04, M12-035): a <c>DriveToObjectAction(robot, id, ActionType 5, 0.0, false, 0.0, false)</c> whose <c>[this+0x44] = 9</c>
/// (<see cref="Field0x44"/>) and whose <c>+0x150</c> function is <b>L3</b> (0x0055EB74; operator() 0x0055F592): <see cref="DriveAndFlipBlockAction.GetPossiblePoses"/> with
/// <c>closestOnly = 0</c> (the third argument is overwritten with 0; the function never uses the flag). <see cref="ShouldDriveToClosestPreActionPose"/> (0x0055EC04) installs
/// <b>L4</b> (0x0055EC2A; 0x0055F60E): the same with <c>closestOnly = b</c> and the in-position flag passed through untouched. NO C# CALLER: nothing in this stack builds the action
/// (its engine users are not built), so it is reached only from tests. As with <see cref="DriveAndFlipBlockAction"/>, the drive's CheckIfDone ends 0x04000001 (the flag stays 0, see <see cref="DriveAndFlipBlockAction"/>).
/// </summary>
// fidelity: M12-035
public sealed class DriveToFlipBlockPoseAction
{
    private readonly ManipulationSystem _m;

    public DriveToFlipBlockPoseAction(ManipulationSystem m, uint objectId)
    {
        _m = m;
        Drive = new DriveToObjectAction(m, objectId, PreActionType.Flipping);
        Drive.PosesFunction = (ObservableObject obj, out IReadOnlyList<PreActionPose> poses, ref bool inPos) =>
            DriveAndFlipBlockAction.GetPossiblePoses(_m, obj, out poses, closestOnly: false, null);
    }

    /// <summary><c>[this+0x44] = 9</c> (0x0055EB66); its meaning is not in the inventory.</summary>
    public int Field0x44 => 9;
    public DriveToObjectAction Drive { get; }
    public IReadOnlyList<string> Trace => Drive.Trace;

    /// <summary><c>ShouldDriveToClosestPreActionPose(bool b)</c> 0x0055EC04: installs L4.</summary>
    public void ShouldDriveToClosestPreActionPose(bool b) =>
        Drive.PosesFunction = (ObservableObject obj, out IReadOnlyList<PreActionPose> poses, ref bool inPos) =>
            DriveAndFlipBlockAction.GetPossiblePoses(_m, obj, out poses, closestOnly: b, null);

    public Task<ActionResult> RunAsync(CancellationToken cancel) => Drive.RunAsync(cancel);
}
