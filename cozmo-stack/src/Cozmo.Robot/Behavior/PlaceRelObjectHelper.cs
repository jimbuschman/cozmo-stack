using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot.Behavior;

// fidelity: M12-042 — OV02 F1-F5 and checked Respond/MarkFailed rows.
public sealed class PlaceRelObjectHelper : IHelper
{
    private readonly ManipulationSystem _m;
    private readonly ManipulationBehavior _behavior;
    public uint Target { get; }
    public bool Low { get; }
    public float A { get; }
    public float B { get; }
    public bool SkipTransform { get; }
    public uint Attempts { get; private set; }
    internal Func<ActionResult>? PrecheckForTest;
    internal Action<PlaceRelObjectAction>? StartActionForTest;
    public PlaceRelObjectHelper(ManipulationSystem m, ManipulationBehavior behavior, BehaviorHelperFactory factory,
        uint target, bool low, float a, float b, bool skipTransform)
        : base(m.Robot, "PlaceRelObject", behavior, factory)
    { _m=m; _behavior=behavior; Target=target; Low=low; A=a; B=b; SkipTransform=skipTransform; }
    public override bool ShouldCancelDelegates(CozmoRobot robot) => false; // checked M8 inventory: all six concrete helpers return 0.
    protected override BehaviorStatus Init(CozmoRobot robot)
    {
        Attempts=0;
        if (Precheck()==ActionResult.Success) StartPlaceRelObject();
        else DelegateDrive();
        return Status;
    }
    protected override BehaviorStatus UpdateWhileActiveInternal(CozmoRobot robot)
    {
        SteppedBehavior.ReportMissing("M12-042 PlaceRelObjectHelper active-update virtual 0x005B93A4..0x005B93A8 (vtable +0x30) is not part of OV02's ctor/Init/Start/Respond checked rows; current callback-written status is retained, with no invented action/drive update");
        return Status;
    }
    private ActionResult Precheck()
    {
        if (PrecheckForTest is {} check) return check();
        if (_m.RobotPose() is not {} pose || _m.Vision.Calibration is not {} calibration)
        {
            SteppedBehavior.ReportMissing("M12-042 pre-action check 0x005B9008/0x005B91B6: host pose/calibration input is unavailable; no successful precheck is invented");
            return ActionResult.Running;
        }
        return PlaceRelObjectOffsetPoses.IsAtPreActionPoseWithVisualVerification(_m.World.GetLocatedObjectById(Target),
            PreActionType.PlaceRelative,A,B,_m.Robot.Engine.Robot?.StoredState?.Timestamp??0,pose,
            _m.Docking.Carrying.CarriedObjectId,calibration,ObjectPoseQueries.Engine,()=>_m.GetObstacles(pose));
    }
    private void DelegateDrive()
    {
        // Checked tuple {type=1, byte4=0, word8=0, byte12=0, A,B}; failure function empty.
        // The DriveToHelper body is explicitly unread in this job; do not substitute a DriveToObjectAction.
        SteppedBehavior.ReportMissing("M12-042/M12-022 DriveToHelper at 0x005B9038/0x005B91E6: checked delegate tuple (1,0,0,0,A,B), empty failure function and success re-entry to StartPlaceRelObject are known, but DriveToHelper body is unread; this delegation stops here without a replacement drive");
    }
    internal void StartPlaceRelObject()
    {
        if (Attempts>=3u) { MarkFailedToStackOrPlace(); StoreStatus(BehaviorStatus.Failure); return; }
        ++Attempts;
        if (Precheck()!=ActionResult.Success) { DelegateDrive(); return; }
        var action = new PlaceRelObjectAction(_m,Target,Low,A,B,false,SkipTransform);
        if (StartActionForTest is {} start) start(action);
        else _behavior.StartPlacementFromHelper(action,RespondToPlaceRelResult);
    }
    internal void RespondToPlaceRelResult(ActionResult result)
    {
        switch ((uint)result)
        {
            case 0: StoreStatus(BehaviorStatus.Complete); break;
            case 0x02000000: break;
            case 0x03000000:
            case 0x03000004: StoreStatus(BehaviorStatus.Failure); break;
            case 0x03000010: _m.Whiteboard.NoPreDockPosesObjectId=Target; break;
            default: StartPlaceRelObject(); break;
        }
    }
    internal void MarkFailedToStackOrPlace()
    {
        var target=_m.World.GetLocatedObjectById(Target);
        var carried=_m.Docking.Carrying.CarriedObjectId is {} id ? _m.World.GetLocatedObjectById(id) : null;
        if (target is null || carried is null) return;
        if (!Low) _m.Whiteboard.SetFailedToUse(target,ObjectActionFailure.StackOnObject);
        else
        {
            using var pose=new Pose3d(Rotation3d.FromRotationVector(new RotationVector3d(new Radians(1f),new Vec3(0,0,1))),new Vec3(A,B,0),"",target.NativePose);
            _m.Whiteboard.SetFailedToUse(carried,ObjectActionFailure.PlaceObjectAt,pose);
        }
    }
}
