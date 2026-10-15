using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
namespace Cozmo.Robot.Manipulation;

// fidelity: M12-034, M2-023 — OV03 checked registration and direct handler rows.
internal sealed class DockActionSubscriptions : IDisposable
{
    private readonly CozmoRobot _robot;
    private readonly DockActionBase _action;
    private RetainedSubscription? _c5, _da;
    private readonly SubscriptionOwners _external = new();
    internal DockActionSubscriptions(CozmoRobot robot, DockActionBase action) { _robot = robot; _action = action; }
    internal void Init(DockAction selected) => LogCallbackDeferral.RunGated(_action.NativeDockGate, () => InitUnderGate(selected));
    private void InitUnderGate(DockAction selected)
    {
        var native = _robot.Engine.Robot!;
        RetainedSubscription.Replace(ref _c5, native.SubscribeMessage(RobotMessageId.MovingLiftPostDock,
            message => MovingLift((MovingLiftPostDock)message), "IDockAction"));
        _action.LiftLoadState = 0; // 0x0055173E, before DA Subscribe.
        RetainedSubscription.Replace(ref _da, native.SubscribeMessage(RobotMessageId.LiftLoad,
            message => LogCallbackDeferral.RunGated(_action.NativeDockGate, () =>
                _action.LiftLoadState = ((LiftLoad)message).Field0 ? (byte)1 : (byte)2), "IDockAction"));
        if (native.HasExternalInterface)
            _external.Add(_robot.Engine.AnimationMessages.SubscribeMessage(0x4C, true, payload => LogCallbackDeferral.RunGated(_action.NativeDockGate, () =>
            {
                if (payload is not uint id || id != _action.ObjectId) return;
                _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "Actions", "IDockAction.RobotDeletedLocatedObject", "Dock object was deleted from current origin stopping dock action");
                _action.InvalidateDockTarget(); // no synchronous abort or completion.
            })));
    }
    private void MovingLift(MovingLiftPostDock message) => LogCallbackDeferral.RunGated(_action.NativeDockGate, () => MovingLiftUnderGate(message));
    private void MovingLiftUnderGate(MovingLiftPostDock message)
    {
        int trigger = _action.LiftAnimationTrigger;
        if (trigger == 0x23F) return;
        string name = ((AnimationTrigger)trigger).ToString();
        // Existing checked response map retains string-key existence, not animation selection.
        if (_robot.Animations.DrivingTriggerMap?.GroupFor((AnimationTrigger)trigger) is null)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning, null, "IDockAction.MovingLiftPostDockHandler.InvalidAnimation", "Could not find animation " + name);
            return;
        }
        byte selected=(byte)_action.SelectedDockAction!.Value; // source reads current action+80 at delivery, not a captured Init snapshot.
        if (message.Field0 != selected)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning, null, "IDockAction.MovingLiftPostDockHandler.ActionMismatch", FormattableString.Invariant($"Expected {selected}, got {message.Field0}. Ignoring."));
            return;
        }
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "Actions", "IDockAction.MovingLiftPostDockHandler", "Playing animation " + name + " ");
        SteppedBehavior.ReportMissing("M2-023/M12-034 C5 0x00557CA6..0x00557CFE: TriggerLiftSafeAnimationAction ctor/body 0x00544710 is not built; concurrent QueueAction(5,0) is not replaced by a direct animation play");
    }
    public void Dispose() => LogCallbackDeferral.RunGated(_action.NativeDockGate, DisposeUnderGate);
    private void DisposeUnderGate()
    {
        _external.Retire();
        _da?.Dispose(); _da = null;
        _c5?.Dispose(); _c5 = null;
    }
}
