using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// Shared plumbing for the manipulation behaviours: run an async manipulation action from a
/// <see cref="SteppedBehavior"/> and post its completion back onto the behaviour's tick, the way the engine's
/// <c>StartActing(action, callback)</c> does. Target selection (the engine's <c>ObjectInteractionInfoCache::
/// GetBestObjectForIntention</c>) is LOCAL: the closest located, upright, unconnected-to-lift cube.
/// </summary>
public abstract class ManipulationBehavior : ActionBehavior
{
    protected readonly ManipulationSystem M;

    protected ManipulationBehavior(string id, string behaviorClass, ManipulationSystem m) : base(id, behaviorClass) => M = m;

    /// <summary>Runs an action; <paramref name="onDone"/> is posted on the behaviour's tick with the result.</summary>
    protected void RunAction(string what, Func<CancellationToken, Task<ActionResult>> action, Action<ActionResult> onDone) =>
        RunAction(what, action, onDone, ActionResult.Abort);

    /// <summary>The closest located cube that is not being carried, or null.</summary>
    protected ObservableObject? ClosestCube(Func<ObservableObject, bool>? filter = null)
    {
        var robot = M.RobotPose();
        if (robot is null) return null;
        return M.World.LocatedObjects
            .Where(o => CubeGeometry.IsCube(o.Type) && !M.Docking.Carrying.IsCarrying(o.ObjectId) && (filter?.Invoke(o) ?? true))
            .OrderBy(o => (o.Pose.Translation - robot.Value.Translation).Length)
            .FirstOrDefault();
    }
}

/// <summary>
/// <c>BehaviorPickUpCube</c> (0x005C64D4..0x005C6F70): runnable with a target cube; <c>InitInternal</c> plays the
/// initial reaction (<c>TriggerLiftSafeAnimationAction</c> with trigger 0x215 <see cref="AnimationTrigger.SparkPickupInitialCubeReaction"/>)
/// then <c>TransitionToPickingUpCube</c> delegates to the pick-up helper (drive to the pre-dock pose, dock,
/// retry); success plays 0x19B <see cref="AnimationTrigger.ReactToBlockPickupSuccess"/>. <c>UpdateInternal</c>
/// stops without a repetition penalty when the target leaves the configured block configuration
/// (<c>ignoreCubesInBlockConfigTypes</c>, not modelled: DEFERRED).
/// </summary>
public sealed class PickUpCubeBehavior : ManipulationBehavior
{
    public enum Phase { Idle, DoingInitialReaction, PickingUpCube, DoingFinalReaction }

    public PickUpCubeBehavior(ManipulationSystem m, string id = "PickUpCube") : base(id, "PickUpCube", m) { }

    public Phase CurrentPhase { get; private set; }
    public uint? TargetObjectId { get; private set; }
    public DockHelper? Helper { get; private set; }

    protected override bool IsRunnableInternal(BehaviorContext context) => !M.Docking.Carrying.IsCarryingObject && ClosestCube() is not null;

    protected override void OnStart()
    {
        Scope.DisableReactions();
        TargetObjectId = ClosestCube()?.ObjectId;
        if (TargetObjectId is null) { Log("no target cube"); Finish(); return; }
        CurrentPhase = Phase.DoingInitialReaction;
        PlayTrigger(AnimationTrigger.SparkPickupInitialCubeReaction, TransitionToPickingUpCube);
    }

    private void TransitionToPickingUpCube()
    {
        CurrentPhase = Phase.PickingUpCube;
        var id = TargetObjectId!.Value;
        Helper = new DockHelper(M);
        RunAction($"PickupBlockHelper({id})", ct => Helper.RunAsync(id, PreActionType.Docking, () => new PickupObjectAction(M, id), ct), r =>
        {
            foreach (var l in Helper.Trace) Log("  " + l);
            if (r == ActionResult.Success) TransitionToSuccessReaction();
            else { Log($"pick-up failed: {r}"); CurrentPhase = Phase.Idle; Finish(); }
        });
    }

    private void TransitionToSuccessReaction()
    {
        CurrentPhase = Phase.DoingFinalReaction;
        PlayTrigger(AnimationTrigger.ReactToBlockPickupSuccess, () => { Log("objective achieved: picked up the cube"); CurrentPhase = Phase.Idle; Finish(); });
    }
}

/// <summary>
/// <c>BehaviorPutDownBlock</c> (0x005C7F98..0x005C8330): runnable while carrying. <c>InitInternal</c> locks
/// reactions and runs a sequential compound: <c>DriveStraightAction</c> backwards by a random 45..75 mm
/// (<c>RandDblInRange(-45, -75)</c>) at 100 mm/s, then <c>TriggerAnimationAction</c> 0x19A
/// <see cref="AnimationTrigger.PutDownBlockPutDown"/> (the animation lowers the lift and releases the cube);
/// then <c>LookDownAtBlock</c>: in parallel <c>MoveHeadToAngleAction(−20°, tol 2°)</c> and <c>DriveStraightAction(−30)</c>,
/// then <c>WaitForImagesAction</c>, 0x199 <see cref="AnimationTrigger.PutDownBlockKeepAlive"/>, and a turn
/// towards a face (max π; needs face tracking, DEFERRED). The carried object is released in the world model
/// at the end of the look-after-place action: the std::function body 0x005C8480 calls SetCarriedObjectAsUnattached(false) at 0x005C84CE when it is still carried.
/// </summary>
// fidelity: M15-012
public sealed class PutDownBlockBehavior : ManipulationBehavior
{
    public enum Phase { Idle, BackingUp, PuttingDown, LookingDown, KeepAlive }

    public PutDownBlockBehavior(ManipulationSystem m, string id = "PutDownBlock") : base(id, "PutDownBlock", m) { }

    public Phase CurrentPhase { get; private set; }
    public double BackUpMm { get; private set; }

    protected override bool IsRunnableInternal(BehaviorContext context) => M.Docking.Carrying.IsCarryingObject;

    protected override void OnStart()
    {
        // fidelity: M7-014
        // BehaviorPutDownBlock::InitInternal 0x005c7fd0: SmartDisableReactionsWithLock(name, table 0x00c68b20) is its first call (0x005c7fe0..0x005c7fe2); the table has no trigger set.
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.PutDownBlock);
        // IBehavior::GetRNG()->RandDblInRange(-45.0, -75.0) (literals 0xC0468000 / 0xC052C000, vldr at 0x005c7fec; 0x005c80d0) on the robot's context RNG, a + u*(b - a).
        BackUpMm = (float)Context.Robot.Animations.Scheduler.ContextRandom.RandDblInRange(-45.0, -75.0);   // vcvt.f32.f64 s0,d0 (0x005c801e): the float goes to DriveStraightAction
        CurrentPhase = Phase.BackingUp;
        RunAction($"DriveStraight({BackUpMm:F0} mm)", ct => new DriveStraightAction(M, BackUpMm, 100f).RunAsync(ct), _ =>
        {
            CurrentPhase = Phase.PuttingDown;
            // InitInternal 0x005C7FD0: DriveStraight, the 0x19A put-down animation, then StartActing(LookDownAtBlock). Nothing clears the carried id during the animation
            // (the SetCarriedObjectAsUnattached callers are HandlePickAndPlaceResult 0x005338A2, CheckAndUpdateTreadsState 0x0051210C, HandleMotorCalibration 0x00536BC4, HandleMotorAutoEnabled 0x00536E0E, PickupObjectAction::Verify 0x00553CAA/0x00553D2A, the std::function 0x005C84CE, CubeLiftWorkout::EndIteration 0x005D8A06 and ReactToPickup::StartAnim 0x00607844),
            // so LookDownAtBlock runs with the id still set and the release comes at the end of the look-after-place action (FinishPutDown).
            PlayTrigger(AnimationTrigger.PutDownBlockPutDown, LookDownAtBlock);
        });
    }

    /// <summary>
    /// <c>WaitForImagesAction</c>'s frame count: 2, the <c>movs r2, #2</c> at 0x005C8242 in
    /// <c>BehaviorPutDownBlock::CreateLookAfterPlaceAction</c>, which builds
    /// <c>WaitForImagesAction(robot, 2, VisionMode 1, ...)</c> into the sequence after the head-down and
    /// back-up pair. It matches <c>acknowledgeObject.json</c>'s <c>NumImagesToWaitFor</c>, which is what
    /// this stack had stood in for it.
    /// </summary>
    public const int ImagesToWaitFor = 2;

    /// <summary>The look-after-place head angle: binary32 0xBEB2B8C2 (movw/movt 0x005C81A6..0x005C81AC, the Radians argument of <c>MoveHeadToAngleAction</c>), not -0.349066f (0xBEB2B8C7).</summary>
    // fidelity: M15-012
    public static readonly float LookDownHeadAngleRad = BitConverter.UInt32BitsToSingle(0xBEB2B8C2);

    /// <summary>
    /// The engine's <c>CompoundActionParallel</c> of <c>MoveHeadToAngleAction(LookDownHeadAngleRad, 0x3D0EFA35, 0)</c> and <c>DriveStraightAction(-30.0f, default speed)</c>
    /// (0x005C8196..0x005C8210, list {head, drive}, 0xC1F00000 at 0x005C81EE), added to the look-after-place sequential compound with <c>AddAction(.., ignoreFailure = 0, ..)</c>
    /// (0x005C8220/0x005C8222). Head and drive run together and the compound ends when both have ended; the first failed child ends it at once with its failure, as
    /// <c>CompoundActionParallel::UpdateInternal</c> 0x0054FAB0..0x0054FBD0 does for an empty ignore-failure map (<see cref="SteppedBehavior"/>'s ParallelAction.ChildDone, same record). The
    /// child that has not ended is cancelled here (the drive; the head move has no cancel): how the engine's deletion cancels it is not in the inventory (MISSING).
    /// </summary>
    private async Task<ActionResult> HeadAndDriveAsync(CancellationToken cancel)
    {
        using var legs = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        var head = HeadLegAsync();
        // DriveStraightAction(robot, -30.0f) is the TWO-argument constructor (0x4ABF68 -> 0x005470F0): its default speed for a distance < 0 is 0xC2A00000, -80 mm/s
        // (literal pair 0x00547268/0x0054726C, `it ge; addge r1,#4` at 0x00547174); this stack's DriveStraightAction takes the magnitude and signs it by the distance.
        var drive = new DriveStraightAction(M, -30, 80f).RunAsync(legs.Token);
        var pending = new List<Task<ActionResult>> { head, drive };
        while (pending.Count > 0)
        {
            var ended = await Task.WhenAny(pending);
            pending.Remove(ended);
            var result = await ended;
            if (result != ActionResult.Success) { legs.Cancel(); return result; }
        }
        return ActionResult.Success;
    }

    private async Task<ActionResult> HeadLegAsync()
    {
        try
        {
            var outcome = await M.Robot.Motion.SetHeadAngleAsync(LookDownHeadAngleRad, CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2, requireCalibration: false);
            // CHOICE: the engine's result for a failed head move is the move's own code (MotionOutcome.EngineResult when it has one); any other failure is Abort.
            return outcome.Ok ? ActionResult.Success : outcome.EngineResult is { } code ? (ActionResult)code : ActionResult.Abort;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { return ActionResult.Abort; }
    }

    // fidelity: M15-012
    internal void LookDownAtBlock()
    {
        CurrentPhase = Phase.LookingDown;
        // CreateLookAfterPlaceAction 0x005C8174: `ldr r0,[r5,#0x284]; ldr r0,[r0,#8]; adds r0,#1; beq 0x005C8264` (0x005C818C..0x005C8194): [[robot+0x284]+8] is the carried
        // ObjectID (CarryingComponent, -1 when not carrying). When it is -1 the head+drive CompoundActionParallel AND the WaitForImagesAction (0x005C8196..0x005C825E) are
        // skipped and only the keep-alive remains.
        // LookDownAtBlock 0x005C80E0 calls CreateLookAfterPlaceAction(robot, true) with the carried id still set after the put-down animation, so the block is built in the live flow.
        if (!M.Docking.Carrying.IsCarryingObject)
        {
            CurrentPhase = Phase.KeepAlive;
            PlayTrigger(AnimationTrigger.PutDownBlockKeepAlive, () => { Log("TurnTowardsFace skipped (no face tracking; DEFERRED)"); FinishPutDown(); });
            return;
        }
        // WaitForImagesAction waits for images that arrive *after* it starts. Taking the count now and
        // comparing against it is the difference between waiting for the placed cube to be re-observed and
        // waiting for nothing at all, because by this point in a run frames have always been processed.
        int framesBefore = M.Vision.FramesProcessed;
        RunAction("CompoundActionParallel(MoveHeadToAngleAction, DriveStraightAction(-30 mm))", HeadAndDriveAsync, r =>
        {
            // The sequential compound CreateLookAfterPlaceAction builds adds the parallel with ignoreFailure = 0, so a failed head or drive ends it: the image wait and the
            // keep-alive after it do not run (CompoundActionSequential::UpdateInternal 0x0054F70C returns the failed child's result). What the behaviour's completion
            // callback does with a failure is not in the inventory (MISSING); the behaviour finishes.
            if (r != ActionResult.Success) { Log($"the look-after-place compound failed ({r}): the image wait and the keep-alive do not run"); FinishPutDown(); return; }
            WaitUntil(() => M.Vision.FramesProcessed >= framesBefore + ImagesToWaitFor, 1.0, _ =>
            {
                CurrentPhase = Phase.KeepAlive;
                PlayTrigger(AnimationTrigger.PutDownBlockKeepAlive, () => { Log("TurnTowardsFace skipped (no face tracking; DEFERRED)"); FinishPutDown(); });
            }, $"{ImagesToWaitFor} image(s) after the place");
        });
    }

    /// <summary>
    /// The std::function LookDownAtBlock gives StartActing (body 0x005C8480): when <c>[[robot+0x284]+8] != -1</c>, <c>SetCarriedObjectAsUnattached(false)</c> (0x005C84CE: the
    /// object stays located and Dirty); it runs when the look-after-place action ends, with any result.
    /// </summary>
    private void FinishPutDown()
    {
        if (M.Docking.Carrying.IsCarryingObject)
        {
            M.Docking.ReleaseCarriedObject();
            Log("put down: SetCarriedObjectAsUnattached(false) after the look-after-place action (0x005C8480..0x005C84CE)");
        }
        CurrentPhase = Phase.Idle;
        Finish();
    }
}

/// <summary>
/// <c>BehaviorRollBlock</c> (0x005C8598..0x005C8D00): runnable with a target block; <c>InitInternal</c> notes the
/// target's up axis and <c>TransitionToPerformingAction</c> delegates to the roll helper (drive, <c>RollObjectAction</c>);
/// <c>UpdateInternal</c> watches the target's pose: when its up axis has changed it stops acting and
/// <c>TransitionToRollSuccess</c> fires the emotion event "RollSucceeded", plays 0x1FB
/// <see cref="AnimationTrigger.RollBlockSuccess"/> and reports the objective. A target that moved more than
/// 120 mm (0x46610000 = 14400 mm²) since the start is re-evaluated. Config <c>isBlockRotationImportant</c>
/// (the shipped configs roll a cube lying on its side) is honoured as: prefer a cube whose up axis is not Z.
/// </summary>
public sealed class RollBlockBehavior : ManipulationBehavior
{
    public enum Phase { Idle, RollingBlock, CelebratingRoll }
    public const double MovedTooFarMm2 = 14400;

    public RollBlockBehavior(ManipulationSystem m, string id = "RollBlockOnSide", bool blockRotationImportant = true) : base(id, "RollBlock", m)
        => BlockRotationImportant = blockRotationImportant;

    public bool BlockRotationImportant { get; }
    public Phase CurrentPhase { get; private set; }
    public uint? TargetObjectId { get; private set; }
    public UpAxis? StartUpAxis { get; private set; }

    private ObservableObject? PickTarget() => BlockRotationImportant
        ? ClosestCube(o => o.UpAxisFromPose() is not (UpAxis.ZPositive or UpAxis.ZNegative)) ?? ClosestCube()
        : ClosestCube();

    protected override bool IsRunnableInternal(BehaviorContext context) => !M.Docking.Carrying.IsCarryingObject && PickTarget() is not null;

    protected override void OnStart()
    {
        Scope.DisableReactions();
        var target = PickTarget();
        if (target is null) { Log("BehaviorRollBlock.NoBlockID"); Finish(); return; }
        TargetObjectId = target.ObjectId;
        StartUpAxis = target.UpAxisFromPose();
        TransitionToPerformingAction();
    }

    private void TransitionToPerformingAction()
    {
        CurrentPhase = Phase.RollingBlock;
        var id = TargetObjectId!.Value;
        var helper = new DockHelper(M) { AttemptLimit = DockHelper.MaxRollAttempts };
        RunAction($"RollBlockHelper({id})", ct => helper.RunAsync(id, PreActionType.Rolling, () => new RollObjectAction(M, id), ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            if (CurrentPhase != Phase.RollingBlock) return;
            if (r == ActionResult.Success && UpAxisChanged()) TransitionToRollSuccess();
            else { Log($"roll did not change the up axis ({r})"); CurrentPhase = Phase.Idle; Finish(); }
        });
    }

    private bool UpAxisChanged() =>
        TargetObjectId is { } id && M.World.GetObjectById(id) is { } o && StartUpAxis is { } start && o.UpAxisFromPose() != start;

    protected override void OnUpdate()
    {
        if (CurrentPhase == Phase.RollingBlock && UpAxisChanged())
        {
            CancelAction();
            StopActing();
            TransitionToRollSuccess();
        }
    }

    private void TransitionToRollSuccess()
    {
        CurrentPhase = Phase.CelebratingRoll;
        double nowSec = Clock() / 1000.0;
        bool known = Context.Mood?.Trigger("RollSucceeded", nowSec) ?? false;
        Log($"emotion event RollSucceeded: {(Context.Mood is null ? "no mood attached" : known ? "applied" : "not in the loaded mood model")}");
        // TransitionToRollSuccess reports the behaviour's own needs action here (0x005C8C1A): RollACube for
        // the three freeplay configs, RollACube_Sparked for the spark.
        if (NeedActionCompleted() is { } action) Log($"needs action {action}");
        PlayTrigger(AnimationTrigger.RollBlockSuccess, () => { Log("objective achieved: rolled the block"); CurrentPhase = Phase.Idle; Finish(); });
    }
}

/// <summary>
/// <c>BehaviorStackBlocks</c> (0x005C93D0..0x005CA0C0): needs a block to carry and a bottom block. Not carrying →
/// <c>TransitionToPickingUpBlock</c> (pick-up helper); carrying → <c>TransitionToStackingBlock</c>
/// (<c>PlaceRelObjectHelper</c> onto the closest valid bottom, <c>GetClosestValidBottom</c>); success →
/// <c>TransitionToPlayingFinalAnim</c> with 0x21A <see cref="AnimationTrigger.StackBlocksSuccess"/> and the
/// objective; a failed stack → <c>TransitionToFailedToStack</c>: drive straight back and
/// <c>PlaceObjectOnGroundAction</c>.
///
/// What makes a bottom valid is <c>DockingComponent::CanStackOnTopOfObject</c> 0x0063C5C4, and it is not
/// an uprightness flag or a progression unlock. It is
/// <c>CanInteractWithObjectHelper</c> 0x0063C654 - the object located, and
/// <c>IsRestingFlat(Radians(0.174533))</c>, ten degrees - followed by
/// <c>!IsPoseTooHigh(pose, 1.0, 15.0, 0.5)</c>. Resting flat is measured against the object's own Z
/// against the nearest parent axis with the sign thrown away, so a cube on any of its six faces
/// qualifies and only one balanced on an edge does not.
/// </summary>
public sealed class StackBlocksBehavior : ManipulationBehavior
{
    // fidelity: M12-012
    public enum Phase { Idle, PickingUpBlock, StackingBlock, PlayingFinalAnim, FailedToStack }

    public StackBlocksBehavior(ManipulationSystem m, string id = "StackBlocks") : base(id, "StackBlocks", m) { }

    public Phase CurrentPhase { get; private set; }
    public uint? TopObjectId { get; private set; }
    public uint? BottomObjectId { get; private set; }
    /// <summary>True only after PlaceRelObjectAction succeeded and the success phase was entered.</summary>
    public bool StackedSuccessfully { get; private set; }

    /// <summary>Ten degrees: <c>CanInteractWithObjectHelper</c> passes Radians(binary32 0x3E32B8C2) (movw/movt 0x0063C66C..0x0063C670), not the double 0.174533 (0x3E32B8C7).</summary>
    // fidelity: M12-012
    public static readonly double RestingFlatToleranceRad = BitConverter.UInt32BitsToSingle(0x3E32B8C2);

    // The pick-up target and the runnable test keep the selection this behaviour already had (located, resting flat
    // within ten degrees, and !IsPoseTooHigh(pose, 1.0, 15.0, 0.5)); the inventory has no row that puts the
    // CanInteractWithObjectHelper "nothing on top within 15" step on those two (R-VIS M13 fix round, objection 2).
    private ObservableObject? ClosestUpright(uint? except = null) =>
        ClosestCube(o => o.ObjectId != except && o.IsRestingFlat(RestingFlatToleranceRad)
                         && !CubeGeometry.IsPoseTooHigh(o, 1.0, 15.0, 0.5));

    // The bottom block is validated by DockingComponent::CanStackOnTopOfObject 0x0063C5C4 (M12-012), which is
    // CanInteractWithObjectHelper 0x0063C654 followed by !IsPoseTooHigh(pose, 1.0, 15.0, 0.5); the helper's
    // FindObjectOnTopOrUnderneath(obj, 15.0, onTop) step (M13-007 caller 0x0063C730) lives in DockingSystem.
    // fidelity: M12-012, M13-007
    private ObservableObject? ClosestValidBottom(uint? except) =>
        ClosestCube(o => o.ObjectId != except && M.Docking.CanStackOnTopOfObject(o));

    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        if (M.Docking.Carrying.IsCarryingObject) return ClosestUpright() is not null;
        var top = ClosestUpright();
        return top is not null && ClosestUpright(top.ObjectId) is not null;
    }

    protected override void OnStart()
    {
        Scope.DisableReactions();
        StackedSuccessfully = false;
        if (M.Docking.Carrying.IsCarryingObject) { TopObjectId = M.Docking.Carrying.CarriedObjectId; TransitionToStackingBlock(); }
        else TransitionToPickingUpBlock();
    }

    private void TransitionToPickingUpBlock()
    {
        CurrentPhase = Phase.PickingUpBlock;
        var top = ClosestUpright();
        if (top is null) { Log("BehaviorStackBlocks.UpdateInternal.BlocksInvalid"); Finish(); return; }
        TopObjectId = top.ObjectId;
        var helper = new DockHelper(M);
        RunAction($"PickupBlockHelper({top.ObjectId})", ct => helper.RunAsync(top.ObjectId, PreActionType.Docking, () => new PickupObjectAction(M, top.ObjectId), ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            if (r == ActionResult.Success) TransitionToStackingBlock();
            else { Log($"pick-up failed: {r}"); CurrentPhase = Phase.Idle; Finish(); }
        });
    }

    private void TransitionToStackingBlock()
    {
        CurrentPhase = Phase.StackingBlock;
        if (!M.Docking.Carrying.IsCarryingObject) { Log("BehaviorStackBlocks.FailBackToPickup: wanted to stack, but we aren't carrying a block"); TransitionToPickingUpBlock(); return; }
        var bottom = ClosestValidBottom(TopObjectId);
        if (bottom is null) { Log("no valid bottom block"); CurrentPhase = Phase.Idle; Finish(); return; }
        BottomObjectId = bottom.ObjectId;
        var helper = new DockHelper(M);
        RunAction($"PlaceRelObjectHelper({bottom.ObjectId})", ct => helper.RunAsync(bottom.ObjectId, PreActionType.PlaceRelative, () => new PlaceRelObjectAction(M, bottom.ObjectId, onTop: true), ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            if (r == ActionResult.Success) TransitionToPlayingFinalAnim(); else TransitionToFailedToStack(r);
        });
    }

    private void TransitionToPlayingFinalAnim()
    {
        StackedSuccessfully = true;
        CurrentPhase = Phase.PlayingFinalAnim;
        Log("objective achieved: stacked");
        // 0x005C9F14, beside the objective: StackCube, or StackCube_Sparked for the spark
        if (NeedActionCompleted() is { } action) Log($"needs action {action}");
        PlayTrigger(AnimationTrigger.StackBlocksSuccess, () => { CurrentPhase = Phase.Idle; Finish(); });
    }

    private void TransitionToFailedToStack(ActionResult why)
    {
        CurrentPhase = Phase.FailedToStack;
        Log($"failed to stack ({why}): backing up and placing the block on the ground");
        RunAction("DriveStraight(-40 mm)", ct => new DriveStraightAction(M, -40, 100f).RunAsync(ct), _ =>
            RunAction("PlaceObjectOnGround", ct => new PlaceObjectOnGroundAction(M).RunAsync(ct), _ => { CurrentPhase = Phase.Idle; Finish(); }));
    }
}

/// <summary>
/// <c>PickUpAndPutDownCube</c> (the shipped <c>SparksPickUpCube</c>): the class name's two halves in sequence,
/// <see cref="PickUpCubeBehavior"/> then <see cref="PutDownBlockBehavior"/>. The engine's class was not
/// disassembled separately (INFERRED composition from its name and the two transcribed classes).
/// </summary>
public sealed class PickUpAndPutDownCubeBehavior : SteppedBehavior
{
    private readonly PickUpCubeBehavior _pickUp;
    private readonly PutDownBlockBehavior _putDown;
    private IBehavior? _running;

    public PickUpAndPutDownCubeBehavior(ManipulationSystem m, string id = "SparksPickUpCube") : base(id, "PickUpAndPutDownCube")
    {
        _pickUp = new PickUpCubeBehavior(m, id + ".PickUp");
        _putDown = new PutDownBlockBehavior(m, id + ".PutDown");
        _pickUp.Step += l => Log("pickup: " + l);
        _putDown.Step += l => Log("putdown: " + l);
    }

    public IBehavior? Running => _running;
    protected override bool KeepsRunningWithoutAction => _running is not null;
    protected override bool IsRunnableInternal(BehaviorContext context) => _pickUp.IsRunnable(context);

    protected override void OnStart()
    {
        _running = _pickUp;
        _pickUp.StartAsync(Context, Scope, CancellationToken.None).GetAwaiter().GetResult();
    }

    protected override void OnUpdate()
    {
        if (_running is null) return;
        if (_running.Update(Context, NowMs)) return;
        _running.Stop(BehaviorStopReason.Completed);
        if (_running == _pickUp && _putDown.IsRunnable(Context))
        {
            _running = _putDown;
            _putDown.StartAsync(Context, Scope, CancellationToken.None).GetAwaiter().GetResult();
            return;
        }
        _running = null;
        Finish();
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        if (reason != BehaviorStopReason.Completed) _running?.Stop(reason);
        _running = null;
    }
}
