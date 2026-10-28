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

    protected override bool KeepsRunningWithoutAction => base.KeepsRunningWithoutAction || HasLiveHelper;
    internal void StartPlacementFromHelper(PlaceRelObjectAction action, Action<ActionResult> response)
    {
        ReportMissing("M12-042 StartActingWithResponseAnim at 0x005B927A: response-animation wrapper is not built; the existing host action adapter supplies the checked action/result callback, without claiming wrapper animation behavior");
        RunAction("PlaceRelObjectAction", action.RunAsync, response);
    }
    internal Action<PlaceRelObjectHelper>? PlacementHelperCreatedForTest;
    protected bool DelegatePlacement(uint target, bool low, float a, float b, bool skip, Action<CozmoRobot>? success, Action<CozmoRobot>? failure)
    {
        if (Context?.AI is not {} ai)
        {
            ReportMissing("M12-042 PlaceRelObjectHelper factory 0x005B55C8..0x005B5606: no AI helper-component owner is bound to this host behavior; placement delegation is skipped");
            return false;
        }
        var placement = new PlaceRelObjectHelper(M, this, ai.Helpers.Factory, target, low, a, b, skip);
        PlacementHelperCreatedForTest?.Invoke(placement);
        IHelper? raw = placement;
        var helper = ai.Helpers.AddHelperToComponent(ref raw);
        try { return SmartDelegateToHelper(helper, success, failure); }
        finally { helper.Release(); }
    }

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
/// then <c>WaitForImagesAction</c>, a turn towards the last face (max π, sayName=false),
/// then ordinary 0x199 <see cref="AnimationTrigger.PutDownBlockKeepAlive"/>. The carried object is released in the world model
/// at the end of the look-after-place action: the std::function body 0x005C8480 calls SetCarriedObjectAsUnattached(false) at 0x005C84CE when it is still carried.
/// </summary>
// fidelity: M15-012
public sealed class PutDownBlockBehavior : ManipulationBehavior,IDisposable
{
    public enum Phase { Idle, BackingUp, PuttingDown, LookingDown, KeepAlive }

    public PutDownBlockBehavior(ManipulationSystem m, string id = "PutDownBlock") : base(id, "PutDownBlock", m) { }

    private Action? _releaseCompletionOwner;
    private bool _disposed;
    private int _run;
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    private void EnsureNativeCompletionOwner()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        _releaseCompletionOwner??=AcquireActionCompletionOwner(M.Robot);
    }
    public void Dispose(){if(_disposed)return;_disposed=true;Stop(BehaviorStopReason.Interrupted);_releaseCompletionOwner?.Invoke();_releaseCompletionOwner=null;}

    public Phase CurrentPhase { get; private set; }
    public double BackUpMm { get; private set; }

    protected override bool IsRunnableInternal(BehaviorContext context) => M.Docking.Carrying.IsCarryingObject;

    protected override void OnStart()
    {
        EnsureNativeCompletionOwner();_run++;
        Scope.SmartDisableReactionsWithLock(Id,ReactionLockTables.PutDownBlock);
        CurrentPhase=Phase.BackingUp;
        var action=NativePutDownActions.CreateInitial(M,out float backup,()=>{CurrentPhase=Phase.PuttingDown;Log("start PutDownBlockPutDown");});
        BackUpMm=backup;IncreaseScoreWhileActing(5f);StartActing(action,_=>LookDownAtBlock());
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

    // fidelity: M15-012
    internal void LookDownAtBlock()
    {
        EnsureNativeCompletionOwner();int run=_run;CurrentPhase=Phase.LookingDown;
        var action=NativePutDownActions.CreateLook(M,this,true,()=>{CurrentPhase=Phase.KeepAlive;Log("start PutDownBlockKeepAlive");});
        IncreaseScoreWhileActing(5f);
        StartActing(action,completed=>
        {
            if(completed.Result!=0)Log($"the look-after-place compound failed ({(ActionResult)completed.Result}): the image wait and the keep-alive do not run");
            FinishPutDown(run); //005C8480 discards every result before still-carrying release.
        });
    }

    /// <summary>
    /// The std::function LookDownAtBlock gives StartActing (body 0x005C8480): when <c>[[robot+0x284]+8] != -1</c>, <c>SetCarriedObjectAsUnattached(false)</c> (0x005C84CE: the
    /// object stays located and Dirty); it runs when the look-after-place action ends, with any result.
    /// </summary>
    private void FinishPutDown(int run)
    {
        if (M.Docking.Carrying.IsCarryingObject)
        {
            // fidelity: M12-006 -- C2 warning precedes the call.
            Context.Robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"BehaviorPutDownBlock.LookDownAtBlock.DidNotSeeBlock","Forcibly setting carried objects as unattached (See COZMO-2192)");
            M.Docking.ReleaseCarriedObject();
        }
        // Host phase/finalization must not finish a replacement queued by a reentrant release observer.
        if(_run==run&&!HasCurrentAction){CurrentPhase=Phase.Idle;Finish();}
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
        SetStateName("StackingBlock");
        if (M.Docking.Carrying.CarriedObjectId is not {} carried || carried != TopObjectId)
        {
            M.Robot.Engine.EmitSourceLog(EngineLogLevel.Debug,"Unnamed","BehaviorStackBlocks.FailBackToPickup","wanted to stack, but we aren't carrying a block");
            TransitionToPickingUpBlock();return;
        }
        // The checked producer consumes +12C; it does not choose a different nearest target here.
        if (BottomObjectId is null)
            ReportMissing("M12-028 BehaviorStackBlocks +0x12C bottom-target writer before 0x005C956A is not built; the existing invalid ObjectID (-1) is retained rather than adding a closest-target query to this producer");
        DelegatePlacement(BottomObjectId??uint.MaxValue,false,0,0,true,_=>TransitionToPlayingFinalAnim(),_=>TransitionToFailedToStack(ActionResult.Abort));
        IncreaseScoreWhileActing(BitConverter.Int32BitsToSingle(0x3F4CCCCD));
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
public sealed class PickUpAndPutDownCubeBehavior : SteppedBehavior,IDisposable
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

    private bool _disposed;
    public override Task StartAsync(BehaviorContext context,BehaviorScope scope,CancellationToken cancel)
    {ObjectDisposedException.ThrowIf(_disposed,this);return base.StartAsync(context,scope,cancel);}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        Stop(BehaviorStopReason.Interrupted);_putDown.Dispose();
    }
    public IBehavior? Running => _running;
    protected override bool KeepsRunningWithoutAction => _running is not null;
    protected override bool IsRunnableInternal(BehaviorContext context) => _pickUp.IsRunnable(context);

    protected override void OnStart()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
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
