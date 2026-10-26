using System.Text.Json;

namespace Cozmo.Robot.Behavior;

// Bounded actual activity-owned objective recipient, not a complete specialized Activity body.
internal sealed class NativeActivityObjectiveOwner : IDisposable
{
    private readonly Activity _activity;
    private readonly EngineRobot _robot;
    private readonly RetainedSubscription? _subscription;
    private readonly bool _objective4Absent;
    private bool _selectedInternalObserved;
    internal NativeActivityObjectiveOwner(Activity activity,EngineRobot robot,JsonElement config)
    {
        _activity=activity;_robot=robot;
        if(activity.Type=="Sparked")
            _objective4Absent=config.TryGetProperty("behaviorObjective",out var objective) && objective.ValueKind==JsonValueKind.String && objective.GetString()!="DroveAsIntended";
        else
            _objective4Absent=config.TryGetProperty("requiredObjectives",out var objectives) && objectives.ValueKind==JsonValueKind.Array &&
                objectives.EnumerateArray().All(e=>e.TryGetProperty("objective",out var value) && value.ValueKind==JsonValueKind.String && value.GetString()!="DroveAsIntended");
        // Native constructor subscription gates use the actual owner at construction, not facade creation.
        if(robot.HasExternalInterface)
            _subscription=robot.Engine.AnimationMessages.SubscribeMessage(0x8B,true,Receive);
    }
    internal void OnSelectedInternal()
    {
        // 0x005AFF60/62 writes phase0 BEFORE repopulation. This records only the
        // defined initialization fact; no invented phase/map or unrelated Socialize body.
        if(_activity.Type=="Socialize")_selectedInternalObserved=true;
    }
    private void Receive(object? payload)
    {
        if(payload is not uint objective)return;
        if(_activity.Type=="Socialize" && !_selectedInternalObserved)
        {
            _robot.Engine.EmitStackLog("M1-034 Socialize objective recipient 0x005B07C2: skip pre-first-selection uninitialized phase read for "+_activity.Id);
            return;
        }
        // Sparked 0x005B181E filters before mutation; Socialize's initialized map/phase
        // invariant survives deselection/reselection. Only the checked objective4 miss is closed.
        if(objective==4 && _objective4Absent)return;
        SteppedBehavior.ReportMissing($"{_activity.Type} objective recipient {_activity.Id}: objective {objective}/changed configuration is outside the checked objective4 miss; no state mutation invented");
    }
    public void Dispose()=>_subscription?.Dispose();
}
