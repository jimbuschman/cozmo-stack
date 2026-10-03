namespace Cozmo.Robot.Behavior;

/// <summary>
/// <c>BehaviorReactToSparked</c> (<c>CreateBehavior</c> case 0x4C, constructor 0x006097E0, 0x120 bytes: the base <c>IBehavior</c> size). It adds no
/// data fields beyond <c>IBehavior</c>: the constructor only installs the derived vtable (0x006097E0..0x006097F0) and <c>IsRunnableInternal</c>
/// returns true (0x00609864..0x00609866).
///
/// <c>InitInternal</c> (0x006097F8..0x00609838) reads the current time in seconds, constructs the 12-byte event name <c>SparkPending</c> and calls
/// <c>MoodManager::TriggerEmotionEvent(robot+0x440, name, now)</c>. It has no <c>UpdateInternal</c> and no <c>StopInternal</c>: with no action
/// started by Init, the inherited <c>IBehavior::UpdateInternal</c> returns the terminal value 2 immediately (<c>+0x84</c> is zero; it would return 1
/// if an action were active), 0x005BDA56..0x005BDA62. Here that is the framework's own rule for a behaviour that starts nothing: it completes
/// at once (<see cref="SteppedBehavior.KeepsRunningWithoutAction"/> is false).
///
/// The shipped config (<c>behaviors/reactions/reactToSparked.json</c>) carries only <c>behaviorClass</c> and <c>behaviorID</c>. The trigger
/// strategy that dispatches it for <c>ReactionTrigger.Sparked</c> is M10's.
/// </summary>
// fidelity: M7-018
public sealed class ReactToSparkedBehavior : SteppedBehavior
{
    /// <summary>The emotion event Init triggers (the 12-byte literal "SparkPending"; <c>emotionevents/spark_events.json</c> defines it).</summary>
    public const string SparkPendingEvent = "SparkPending";

    public ReactToSparkedBehavior(string id = "ReactToSparked") : base(id, "ReactToSparked") { }

    /// <summary><c>IsRunnableInternal</c> 0x00609864: always true.</summary>
    protected override bool IsRunnableInternal(BehaviorContext context) => true;

    /// <summary><c>InitInternal</c> 0x006097F8: <c>TriggerEmotionEvent("SparkPending", now)</c> on the robot's mood manager.</summary>
    protected override void OnStart()
    {
        double now = Context.ClockSec?.Invoke() ?? Clock() / 1000.0;     // BaseStationTimer::GetCurrentTimeInSeconds
        bool known = Context.Mood?.Trigger(SparkPendingEvent, now) ?? false;
        Log($"emotion event {SparkPendingEvent}: {(Context.Mood is null ? "no mood attached" : known ? "applied" : "not in the loaded mood model")}");
    }
}
