using System.Text.Json;

namespace Cozmo.Robot.Behavior;

// Actual configured IBehavior inputs. Standalone host seams remain separate.
// ctor 0x005BBCAC..BE; ReadFromJson 0x005BC118..1B0; RB2-RB4.
internal sealed class NativeBehaviorBaseInputs
{
    private readonly EngineRobot _robot;
    private readonly BehaviorManager? _manager;
    private readonly uint _requiredUnlock;
    internal uint RequiredUnlock=>_requiredUnlock;
    // Captured manager is the Robot+0x44 owner. Never read a replacement or a retired owner.
    internal bool TryGetActiveSparkId(out int id)
    {
        bool actual=ReferenceEquals(_robot.Engine.Robot,_robot) ||
            (_robot.Engine.Robot is null && _robot.NativeBehaviorRuntimeConstructing);
        if(actual && _manager is {} manager && ReferenceEquals(_robot.BehaviorManagerOwner,manager))
        {
            id=manager.ActiveSparkId;
            return true;
        }
        id=0;
        return false;
    }
    // Common +6C uses the native executable name table and Count constructor default.
    internal byte? CheckedExecutableType {get;}
    internal byte CheckedBehaviorClass {get;}
    internal string DisplayNameKey { get; }
    private readonly int _requiredSevere;
    private readonly float _recentDriveOff, _recentSwitch;
    internal NativeBehaviorBaseInputs(EngineRobot robot,JsonElement config)
    {
        _robot=robot;
        CheckedBehaviorClass=(byte)BehaviorClassNames.ExtractFromConfig(config);
        _manager=robot.BehaviorManagerOwner;
        CheckedExecutableType=ExecutableBehaviorNames.FromConfig(config);
        // IBehavior+0x4C starts empty (0x005BBC62..0x005BBC74). ReadFromJson fills it at 0x005BBFC2..0x005BBFDA.
        DisplayNameKey = ReadDisplayNameKey(config);
        _requiredUnlock=config.TryGetProperty("requiredUnlockId",out var unlock)&&unlock.ValueKind!=JsonValueKind.Null?NativeUnlockNames.FromBytes(System.Text.Encoding.UTF8.GetBytes(unlock.GetString()!)):0x55;
        _requiredSevere=config.TryGetProperty("requiredSevereNeedsState",out var need)
            ?need.GetString() switch{"Repair"=>0,"Energy"=>1,"Play"=>2,"Count"=>3,_=>-1}:3;
        _recentDriveOff=config.TryGetProperty("requiredRecentDriveOffCharger_sec",out var drive)?drive.GetSingle():-1f;
        _recentSwitch=config.TryGetProperty("requiredRecentSwitchToParent_sec",out var change)?change.GetSingle():-1f;
    }
    internal static string ReadDisplayNameKey(JsonElement config) =>
        config.TryGetProperty("displayNameKey", out var key) && key.ValueKind == JsonValueKind.String
            ? key.GetString()! : string.Empty;

    internal bool IsCarrying=>_robot.CarryingOwner is {} owner && (owner.CarriedObjectId??uint.MaxValue)!=uint.MaxValue;
    internal bool Allows(BehaviorContext context)
    {
        bool actual=ReferenceEquals(context.Robot.Engine.Robot,_robot) ||
            (context.Robot.Engine.Robot is null && _robot.NativeBehaviorRuntimeConstructing && ReferenceEquals(context.Robot.Engine,_robot.Engine));
        if(!actual || _robot.CarryingOwner is null)
        {SteppedBehavior.MarkReadinessUnavailable();SteppedBehavior.ReportMissing("IBehavior native base admission: actual Robot/Carrying owner unavailable");return false;}
        if(_requiredSevere!=3 && _requiredSevere!=context.Robot.Engine.Needs.SevereNeeds.Selected)return SteppedBehavior.ReadinessBlocked("required severe-needs state is not present (0x005BD7E6..F6)");
        if(_requiredUnlock!=0x55)
        {
            if(_robot.Progression is not {} progression||!progression.TryIsUnlocked(_requiredUnlock,out bool unlocked))
            {SteppedBehavior.MarkReadinessUnavailable();SteppedBehavior.ReportMissing("IBehavior requiredUnlockId005BD810 actual progression override state unavailable; no hardcoded unlock");return false;}
            if(!unlocked)return SteppedBehavior.ReadinessBlocked("required progression unlock is not available (0x005BD806..16)");
        }
        float epsilon=BitConverter.UInt32BitsToSingle(0x3727C5AC);
        float now=context.Robot.Engine.Timer.SecondsF;
        if(!(_recentDriveOff < -epsilon))
        {
            float stamp=_robot.LastDriveOffChargerSeconds;
            if(stamp < -epsilon || !((_recentDriveOff+stamp)+epsilon>=now))return SteppedBehavior.ReadinessBlocked("required recent drive-off-charger event is outside its native window (0x005BD81A..82E)");
        }
        if(!((double)_recentSwitch < BitConverter.Int64BitsToDouble(unchecked((long)0xBEE4F8B588E368F1))))
        {
            if(_manager is not {} manager || !ReferenceEquals(_robot.BehaviorManagerOwner,manager) || !ReferenceEquals(manager.ContextRobot,context.Robot))
            {SteppedBehavior.MarkReadinessUnavailable();SteppedBehavior.ReportMissing("IBehavior requiredRecentSwitchToParent_sec: original manager+0x50 owner unavailable");return false;}
            float limit=_recentSwitch+epsilon;
            float elapsed=now-manager.ParentSwitchTime;
            if(limit<elapsed)return SteppedBehavior.ReadinessBlocked("required recent switch-to-parent event is outside its native window (0x005BD832..62)");
        }
        return true;
    }
}
