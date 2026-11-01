using System.Text;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Behavior;

public sealed partial class BehaviorManager
{
    private sealed class HiccupSensorSubscription : IDisposable
    {
        private CozmoSensors? _sensors;
        private readonly Action<OffTreadsState, OffTreadsState> _callback;
        internal HiccupSensorSubscription(CozmoSensors sensors, Action<OffTreadsState, OffTreadsState> callback)
        { _sensors = sensors; _callback = callback; sensors.HiccupOffTreadsStateChanged += callback; }
        public void Dispose()
        {
            if (_sensors is { } sensors) { _sensors = null; sensors.HiccupOffTreadsStateChanged -= _callback; }
        }
    }

    private ReactionRegistration? CreateNativeHiccup(ReactionMapEntry row, ManipulationSystem? manipulation,
        IReadOnlyDictionary<string,IBehavior>? bound, EngineRobot? robot)
    {
        var facade = _context.Robot;
        if (_hostDisposed) return null;
        if (robot is null || manipulation is null)
        {
            SteppedBehavior.ReportMissing("M10-003 Hiccup constructor 0x0060FDCA: actual robot/AIWhiteboard unavailable; no strategy or RNG draws invented");
            return null;
        }
        uint unlock = NativeUnlockNames.FromBytes(Encoding.UTF8.GetBytes(row.Hiccup?.HiccupsUnlockId ?? ""));
        var strategy = new HiccupReactionStrategy(row.Hiccup, manipulation.Whiteboard, facade.Engine.ContextRandom,
            () => facade.Engine.Timer.TimeStampMs,
            () => {
                if (facade.Engine.Features is { } features) return features.IsEnabled("Hiccups");
                SteppedBehavior.ReportMissing("M10-003 Hiccup feature gate 0x006106A8 unavailable"); return false;
            },
            () => robot.Progression is { } progression && progression.TryIsUnlocked(unlock, out bool value) ? value : null,
            () => facade.Engine.Needs.SevereNeeds.Selected,
            need => (int)facade.Engine.Needs.State.GetNeedBracket(need),
            action => facade.Engine.Needs.RegisterNeedsActionCompleted(action switch { 0x18 => "HiccupsEndBad", 0x19 => "HiccupsEndGood", _ => "IndividualHiccup" }),
            value => { if (robot.HasExternalInterface) facade.Sensors.BroadcastHiccupsChanged(value); },
            value => facade.Animations.Scheduler.KeepAliveTimeoutSec = value,
            (player, elapsed) => facade.Engine.EmitSourceLog(EngineLogLevel.Event, null, "robot.hiccups.ended", player ? "PLAYER_CURED" : "SELF_CURED",
                new Dictionary<string,string> { ["$data"] = elapsed.ToString(System.Globalization.CultureInfo.InvariantCulture) }),
            SteppedBehavior.ReportMissing,
            robot.HasExternalInterface ? s => facade.Engine.AnimationMessages.SubscribeMessage(0x9E, false, payload => {
                if (payload is byte[] bytes && bytes.Length >= 2 && Enum.TryParse<BehaviorID>(row.BehaviorId, out var id))
                    s.ReceiveExecuteReactionTrigger(bytes[0], bytes[1], (byte)id);
            }) : null,
            () => {
                facade.Engine.EmitSourceLog(EngineLogLevel.Error, null, "ReactionTriggerStrategyHiccup.AlwaysHandleInternal.OverfedButHiccupDisabled",
                    "Cozmo was overfed and should get hiccups, but hiccups are disabled");
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            });
        if (robot.HasExternalInterface)
        {
            strategy.AddSubscription(new HiccupSensorSubscription(facade.Sensors, (_, next) => strategy.OffTreadsChanged((byte)next)));
            strategy.AddSubscription(facade.Engine.AnimationMessages.SubscribeMessage(164, false, _ => strategy.Receive164()));
        }
        if (_hostDisposed) { strategy.Dispose(); return null; }
        strategy.FinishConstruction();
        if (bound is not null && bound.TryGetValue(row.BehaviorId, out var target) && target is PlayAnimBehavior)
            return new ReactionRegistration(strategy, target);
        // InitReactionTriggerMap constructs then destroys a strategy whose configured target is missing.
        strategy.Dispose();
        SteppedBehavior.ReportMissing("M7-018/M10-003 Hiccup configured PlayAnim target unavailable; strategy destroyed at 0x005A1872");
        return null;
    }
}
