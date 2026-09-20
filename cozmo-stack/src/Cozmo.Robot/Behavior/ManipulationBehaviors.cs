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
/// when the put-down animation completes (INFERRED: the engine learns it from the robot's carry state).
/// </summary>
public sealed class PutDownBlockBehavior : ManipulationBehavior
{
    public enum Phase { Idle, BackingUp, PuttingDown, LookingDown, KeepAlive }

    public PutDownBlockBehavior(ManipulationSystem m, string id = "PutDownBlock") : base(id, "PutDownBlock", m) { }

    public Phase CurrentPhase { get; private set; }
    public double BackUpMm { get; private set; }

    protected override bool IsRunnableInternal(BehaviorContext context) => M.Docking.Carrying.IsCarryingObject;

    protected override void OnStart()
    {
        Scope.DisableReactions();
        BackUpMm = -(45 + Context.Random.NextDouble() * 30);
        CurrentPhase = Phase.BackingUp;
        RunAction($"DriveStraight({BackUpMm:F0} mm)", ct => new DriveStraightAction(M, BackUpMm, 100f).RunAsync(ct), _ =>
        {
            CurrentPhase = Phase.PuttingDown;
            PlayTrigger(AnimationTrigger.PutDownBlockPutDown, () =>
            {
                M.Docking.Carrying.UnsetCarrying();
                Log("put down: the carried object is released (INFERRED: from the robot's carry state in the engine)");
                LookDownAtBlock();
            });
        });
    }

    /// <summary>
    /// <c>WaitForImagesAction</c>'s frame count. The engine takes it as a constructor argument and the value
    /// this behaviour passes was not read; <c>acknowledgeObject.json</c>'s <c>NumImagesToWaitFor</c> is 2, so 2
    /// stands in (INFERRED).
    /// </summary>
    public const int ImagesToWaitFor = 2;

    private void LookDownAtBlock()
    {
        CurrentPhase = Phase.LookingDown;
        _ = M.Robot.Motion.SetHeadAngleAsync(-0.349066f, requireCalibration: false);
        // WaitForImagesAction waits for images that arrive *after* it starts. Taking the count now and
        // comparing against it is the difference between waiting for the placed cube to be re-observed and
        // waiting for nothing at all, because by this point in a run frames have always been processed.
        int framesBefore = M.Vision.FramesProcessed;
        RunAction("DriveStraight(-30 mm)", ct => new DriveStraightAction(M, -30, 100f).RunAsync(ct), _ =>
        {
            WaitUntil(() => M.Vision.FramesProcessed >= framesBefore + ImagesToWaitFor, 1.0, _ =>
            {
                CurrentPhase = Phase.KeepAlive;
                PlayTrigger(AnimationTrigger.PutDownBlockKeepAlive, () => { Log("TurnTowardsFace skipped (no face tracking; DEFERRED)"); CurrentPhase = Phase.Idle; Finish(); });
            }, $"{ImagesToWaitFor} image(s) after the place");
        });
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
        var helper = new DockHelper(M);
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
        PlayTrigger(AnimationTrigger.RollBlockSuccess, () => { Log("objective achieved: rolled the block"); CurrentPhase = Phase.Idle; Finish(); });
    }
}

/// <summary>
/// <c>BehaviorStackBlocks</c> (0x005C93D0..0x005CA0C0): needs a block to carry and a bottom block. Not carrying →
/// <c>TransitionToPickingUpBlock</c> (pick-up helper); carrying → <c>TransitionToStackingBlock</c>
/// (<c>PlaceRelObjectHelper</c> onto the closest valid bottom, <c>GetClosestValidBottom</c>); success →
/// <c>TransitionToPlayingFinalAnim</c> with 0x21A <see cref="AnimationTrigger.StackBlocksSuccess"/> and the
/// objective; a failed stack → <c>TransitionToFailedToStack</c>: drive straight back and
/// <c>PlaceObjectOnGroundAction</c>. Non-upright bottoms need the progression unlock (DEFERRED: uprights only).
/// </summary>
public sealed class StackBlocksBehavior : ManipulationBehavior
{
    public enum Phase { Idle, PickingUpBlock, StackingBlock, PlayingFinalAnim, FailedToStack }

    public StackBlocksBehavior(ManipulationSystem m, string id = "StackBlocks") : base(id, "StackBlocks", m) { }

    public Phase CurrentPhase { get; private set; }
    public uint? TopObjectId { get; private set; }
    public uint? BottomObjectId { get; private set; }

    private ObservableObject? ClosestUpright(uint? except = null) =>
        ClosestCube(o => o.ObjectId != except && o.UpAxisFromPose() == UpAxis.ZPositive && o.Pose.Translation.Z < PickupObjectAction.HighDockHeightMm);

    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        if (M.Docking.Carrying.IsCarryingObject) return ClosestUpright() is not null;
        var top = ClosestUpright();
        return top is not null && ClosestUpright(top.ObjectId) is not null;
    }

    protected override void OnStart()
    {
        Scope.DisableReactions();
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
        var bottom = ClosestUpright(TopObjectId);
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
        CurrentPhase = Phase.PlayingFinalAnim;
        Log("objective achieved: stacked");
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
