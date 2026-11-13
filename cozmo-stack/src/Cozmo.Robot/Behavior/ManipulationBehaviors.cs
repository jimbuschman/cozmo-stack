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
    protected ManipulationSystem M;

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

// RETIRE-LEGACY-DRIVE-API-1 (operator 2026-11-13): the legacy PickUpCubeBehavior, RollBlockBehavior, StackBlocksBehavior and
// PickUpAndPutDownCubeBehavior (their drive step was the retired DockHelper) are removed. The configured ids are bound by the factory's
// NativePickUpCubeBehavior / NativeRollBlockBehavior / NativeStackBlocksBehavior; PickUpAndPutDownCube has no native class (unbound).

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
