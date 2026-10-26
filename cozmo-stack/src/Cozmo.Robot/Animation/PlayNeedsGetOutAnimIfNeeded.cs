using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation;

// fidelity: M15-074, M8-020, M8-021
// Checked row 73; shares playback, callbacks, completion and cleanup with the existing trigger runner.
internal sealed class PlayNeedsGetOutAnimIfNeeded : TriggerAnimationAction
{
    private readonly CozmoRobot _robot;
    private bool _cleared, _destroyed;

    internal PlayNeedsGetOutAnimIfNeeded(CozmoRobot robot)
        : base(robot, new((AnimationTrigger)0x23F, 1, true, 0,
            BitConverter.Int32BitsToSingle(0x42700000), false)) => _robot = robot;

    protected override string ActionName => "PlayNeedsGetOut";

    public override int Init()
    {
        // 0x005450BA: selection is read now, not saved by construction.
        int selected = _robot.Engine.Needs.SevereNeeds.Selected;
        if (selected == 3)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info, "Actions",
                "PlayNeedsGetOutAnimIfNeeded.NoTransitionNeeded",
                $"[{Tag}] {ActionName}: no transition animation needed");
            return 0;
        }
        if ((uint)selected > 3) return 0; // 0x005450BE -> 0x00545134
        SetAnimGroupFromTrigger((AnimationTrigger)(selected == 0 ? 0x141 : selected == 1 ? 0x135 : 0x13A));
        return base.Init();
    }

    public override uint CheckIfDone()
    {
        if ((int)Request.Trigger == 0x23F) return 0; // before reading the inherited animation flags
        if (!_cleared && HasStarted)
        {
            _robot.Engine.Needs.SevereNeeds.Clear();
            _cleared = true;
        }
        return HasEnded ? 0u : Failed ? 0x03000001u : EngineActionResult.Running;
    }

    public override void WatcherEnding()
    {
        if (_destroyed) return;
        _destroyed = true;
        // 0x0054504C..62: current selection, including cancellation before Init/start.
        if (_robot.Engine.Needs.SevereNeeds.Selected != 3 && !_cleared) _robot.Engine.Needs.SevereNeeds.Clear();
        base.WatcherEnding();
    }
}

