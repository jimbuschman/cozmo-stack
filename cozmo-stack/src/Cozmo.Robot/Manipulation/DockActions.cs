using Cozmo.Protocol;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// The engine's <c>IDockAction</c> (0x005502D8..0x00552560) as the base of the dock actions. <c>Init</c>: the
/// object must be located ("Dock object is null"); unless the action was told to skip it, the robot must be
/// close enough to one of the object's pre-action poses (<c>GetPreActionPoses</c> → <c>IsCloseEnoughToPreActionPose</c>,
/// 100 mm box (0x42C80000) and 30 degrees (0x3F060A92)) or the action fails with
/// <c>DidNotReachPreActionPose</c>; then <c>SetupTurnAndVerifyAction</c> turns towards the object and visually
/// verifies it (a <c>VisuallyVerifyObjectAction</c>, or for a place a <c>VisuallyVerifyNoObjectAtPoseAction</c>),
/// reactions are locked out ("dockActions"), the object's cube lights play, and <c>CheckIfDone</c> begins the
/// dock ("Docking with marker %d (%s) using action %s") with the closest visible marker. The subclass's
/// <c>SelectDockAction</c> picks the firmware action and <c>Verify</c> judges the result. The default
/// pre-action angle tolerance is 0.1309 rad (7.5 degrees) from the constructor.
/// </summary>
public abstract class DockActionBase
{
    public const double CloseEnoughDistanceMm = 100.0;
    public const double CloseEnoughAngleRad = 0.523599;

    protected readonly ManipulationSystem M;
    protected readonly List<string> _trace = new();

    protected DockActionBase(ManipulationSystem m, uint objectId) { M = m; ObjectId = objectId; }

    public uint ObjectId { get; }
    public PathMotionProfile Profile { get; set; } = PathMotionProfile.Default;
    /// <summary>The engine's <c>_shouldCheckPreActionPose</c> (set false by the helpers when they just drove there).</summary>
    public bool CheckPreActionPose { get; set; } = true;
    public DockAction? SelectedDockAction { get; protected set; }
    public DockResult? Result { get; protected set; }
    public IReadOnlyList<string> Trace => _trace;

    protected abstract PreActionType PreActionType { get; }
    protected abstract DockAction? SelectDockAction(ObservableObject target);
    protected abstract ActionResult Verify(ObservableObject? target, DockResult result);
    protected virtual (double X, double Y, double Angle) PlacementOffset => (0, 0, 0);
    protected virtual bool DoLiftLoadCheck => false;

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var target = M.World.GetLocatedObjectById(ObjectId);
        if (target is null) { _trace.Add("IDockAction.NullDockObject: Dock object is null"); return ActionResult.BadObject; }
        var robot = M.RobotPose();
        if (robot is null) return ActionResult.Abort;
        if (CheckPreActionPose)
        {
            var poses = CubePreActionPoses.For(target, PreActionType);
            bool near = poses.Any(p =>
            {
                var d = robot.Value.Translation - p.WorldPose.Translation;
                return Math.Abs(d.X) <= CloseEnoughDistanceMm && Math.Abs(d.Y) <= CloseEnoughDistanceMm
                       && Math.Abs(StraightLinePlanner.Wrap(robot.Value.AngleAroundZ - p.WorldPose.AngleAroundZ)) <= CloseEnoughAngleRad;
            });
            if (!near) { _trace.Add("IDockAction.Init: not within (100mm, 30deg) of a pre-action pose"); return ActionResult.DidNotReachPreActionPose; }
            _trace.Add("IDockAction.Init.BeginDockingFromPreActionPose");
        }
        var action = SelectDockAction(target);
        if (action is null) { _trace.Add("IDockAction.Init.DockActionSelectionFailure"); return ActionResult.Abort; }
        SelectedDockAction = action;

        // SetupTurnAndVerifyAction: face the object and see it (two images, as VisuallyVerifyObjectAction)
        await M.TurnTowardsObjectAsync(ObjectId, Math.PI, cancel);
        var marker = await M.WaitForVisibleMarkerAsync(ObjectId, TimeSpan.FromSeconds(2), cancel);
        if (marker is null) { _trace.Add("IDockAction.CheckIfDone.VisualVerifyFailed: VisualVerification of object failed, stopping IDockAction."); return ActionResult.VisualObservationFailed; }
        _trace.Add($"IDockAction.DockWithObjectHelper.BeginDocking: Docking with marker {marker.Code} using action {action}.");
        var (ox, oy, oa) = PlacementOffset;
        var result = await M.Docking.DockAsync(target, marker, action.Value, Profile, ox, oy, oa, numRetries: 0, DoLiftLoadCheck, cancel: cancel);
        if (result is null) return cancel.IsCancellationRequested ? ActionResult.CancelledWhileRunning : ActionResult.Timeout;
        Result = result;
        _trace.Add($"dock result {result}");
        var verified = Verify(M.World.GetObjectById(ObjectId), result);
        _trace.Add($"Verify -> {verified}");
        return verified;
    }
}

/// <summary>
/// <c>PickupObjectAction</c> (0x00553648): <c>SelectDockAction</c> compares the object's height with 33.85
/// (0x42076666): a block resting on the ground (its centre at 22 mm) is a <c>PickupLow</c>, one on top of
/// another (66 mm) a <c>PickupHigh</c>; already carrying → "Already carrying object. Can't pickup object."
/// <c>Verify</c>: the robot must think it is carrying the object ("Expecting robot to think it's carrying an
/// object at this point"), and seeing the object still in its original pose means the pick-up failed
/// ("Object pick-up FAILED! (Still seeing object in same place.)"); otherwise "Object pick-up SUCCEEDED!".
/// The lift-load timeout check and the "object didn't move as expected" accelerometer check are DEFERRED.
/// </summary>
public sealed class PickupObjectAction : DockActionBase
{
    public const double HighDockHeightMm = 33.85;
    private Pose3d _originalPose;

    public PickupObjectAction(ManipulationSystem m, uint objectId) : base(m, objectId) { }
    protected override PreActionType PreActionType => PreActionType.Docking;
    protected override bool DoLiftLoadCheck => true;

    protected override DockAction? SelectDockAction(ObservableObject target)
    {
        if (M.Docking.Carrying.IsCarryingObject) { _trace.Add("PickupObjectAction.SelectDockAction.CarryingObject: Already carrying object. Can't pickup object. Aborting."); return null; }
        _originalPose = target.Pose;
        return target.Pose.Translation.Z > HighDockHeightMm ? DockAction.PickupHigh : DockAction.PickupLow;
    }

    protected override ActionResult Verify(ObservableObject? target, DockResult result)
    {
        if (!result.Succeeded) return ActionResult.Retry;
        if (!M.Docking.Carrying.IsCarrying(ObjectId)) { _trace.Add("PickupObjectAction.Verify.ExpectedCarryingObject"); return ActionResult.Retry; }
        if (target is { IsLocated: true } && target.LastObservedTimestamp > result.Timestamp && target.Pose.IsSameAs(_originalPose, 20, 0.35))
        {
            _trace.Add("PickupObjectAction.Verify.SeeingCarriedObjectInOrigPose: Object pick-up FAILED! (Still seeing object in same place.)");
            M.Docking.Carrying.UnsetCarrying();
            return ActionResult.Retry;
        }
        _trace.Add("PickupObjectAction.Verify.Success: Object pick-up SUCCEEDED!");
        return ActionResult.Success;
    }
}

/// <summary>
/// <c>PlaceRelObjectAction</c> (0x00554BE0): place the carried object relative to a target object (on top of it
/// for a stack: <c>PlaceHigh</c>; beside it: <c>PlaceLow</c>). "Not carrying object" aborts. Its
/// <c>TransformPlacementOffsetsRelativeObject</c> requires the robot and block within 0.261799 rad (15 degrees)
/// of alignment ("Robot and block are not within alignment threshold") and a non-negative x offset. Success
/// when the robot reports <c>BlockPlaced</c> and the carried object is no longer carried.
/// </summary>
public sealed class PlaceRelObjectAction : DockActionBase
{
    public PlaceRelObjectAction(ManipulationSystem m, uint targetObjectId, bool onTop = true) : base(m, targetObjectId) => OnTop = onTop;
    public bool OnTop { get; }
    protected override PreActionType PreActionType => PreActionType.PlaceRelative;

    protected override DockAction? SelectDockAction(ObservableObject target)
    {
        if (!M.Docking.Carrying.IsCarryingObject) { _trace.Add("PlaceRelObjectAction.SelectDockAction.NotCarryingObject"); return null; }
        return OnTop ? DockAction.PlaceHigh : DockAction.PlaceLow;
    }

    protected override ActionResult Verify(ObservableObject? target, DockResult result)
    {
        if (!result.Succeeded || result.Status != BlockStatus.BlockPlaced) return ActionResult.Retry;
        return M.Docking.Carrying.IsCarryingObject ? ActionResult.StillCarryingObject : ActionResult.Success;
    }
}

/// <summary>
/// <c>RollObjectAction</c>: <c>RollLow</c> (or <c>DeepRollLow</c> when deep roll is enabled). The behaviour that
/// uses it judges success by the object's up axis changing; here the dock result is returned and the
/// caller compares up axes.
/// </summary>
public sealed class RollObjectAction : DockActionBase
{
    public RollObjectAction(ManipulationSystem m, uint objectId) : base(m, objectId) { }
    public bool DeepRoll { get; set; }
    protected override PreActionType PreActionType => PreActionType.Rolling;
    protected override DockAction? SelectDockAction(ObservableObject target) =>
        M.Docking.Carrying.IsCarryingObject ? null : DeepRoll ? DockAction.DeepRollLow : DockAction.RollLow;
    protected override ActionResult Verify(ObservableObject? target, DockResult result) => result.Succeeded ? ActionResult.Success : ActionResult.Retry;
}

/// <summary><c>PopAWheelieAction</c>: <c>PopAWheelie</c> against a cube's face.</summary>
public sealed class PopAWheelieAction : DockActionBase
{
    public PopAWheelieAction(ManipulationSystem m, uint objectId) : base(m, objectId) { }
    protected override PreActionType PreActionType => PreActionType.Docking;
    protected override DockAction? SelectDockAction(ObservableObject target) => M.Docking.Carrying.IsCarryingObject ? null : DockAction.PopAWheelie;
    protected override ActionResult Verify(ObservableObject? target, DockResult result) => result.Succeeded ? ActionResult.Success : ActionResult.Retry;
}

/// <summary>
/// <c>PlaceObjectOnGroundAction</c> (0x00554638): not a dock. "executing PlaceObjectOnGroundAction but not
/// carrying object" aborts; otherwise <c>CarryingComponent::PlaceObjectOnGround</c> sends
/// <c>PlaceObjectOnGround</c> (0x44) and the action waits for the robot's <c>PickAndPlaceResult</c> with
/// <c>BlockPlaced</c>; a following face-and-verify failure just clears the object from the world. Message
/// field order INFERRED from the packing at 0x00632BAE..0x00632BEE: {speed, accel, decel, rel_x, rel_y,
/// rel_angle, useApproachAngle} (hardware item O).
/// </summary>
public sealed class PlaceObjectOnGroundAction
{
    private readonly ManipulationSystem _m;
    public PlaceObjectOnGroundAction(ManipulationSystem m) => _m = m;
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    public static PlaceObjectOnGround Message(PathMotionProfile p) => new()
    {
        Field0 = BitConverter.SingleToUInt32Bits(p.DockSpeedMmps), Field1 = BitConverter.SingleToUInt32Bits(p.DockAccelMmps2), Field2 = BitConverter.SingleToUInt32Bits(p.DockDecelMmps2),
        Field3 = 0, Field4 = 0, Field5 = 0, Field6 = 0,
    };

    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        if (!_m.Docking.Carrying.IsCarryingObject) { _trace.Add("PlaceObjectOnGroundAction.CheckPreconditions.NotCarryingObject"); return ActionResult.NotCarryingObjectAbort; }
        var result = await _m.Docking.PlaceOnGroundAsync(Message(PathMotionProfile.Default), TimeSpan.FromSeconds(10), cancel);
        if (result is null) return ActionResult.Timeout;
        _trace.Add($"PlaceObjectOnGround result {result}");
        return result.Status == BlockStatus.BlockPlaced && !_m.Docking.Carrying.IsCarryingObject ? ActionResult.Success : ActionResult.StillCarryingObject;
    }
}

/// <summary><c>MoveLiftToHeightAction</c>: a lift preset through <c>SetLiftHeight</c>.</summary>
public sealed class MoveLiftToHeightAction
{
    private readonly ManipulationSystem _m;
    public MoveLiftToHeightAction(ManipulationSystem m, float heightMm) { _m = m; HeightMm = heightMm; }
    public float HeightMm { get; }
    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        var o = await _m.Robot.Motion.SetLiftHeightAsync(HeightMm, requireCalibration: false);
        return o.Result == MotionResult.Acknowledged ? ActionResult.Success : ActionResult.Timeout;
    }
}
