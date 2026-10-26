using System.Text.Json;

namespace Cozmo.Robot.Behavior;

// Actual configured IBehavior inputs. Standalone host seams remain separate.
// ctor 0x005BBCAC..BE; ReadFromJson 0x005BC118..1B0; RB2-RB4.
internal sealed class NativeBehaviorBaseInputs
{
    private readonly EngineRobot _robot;
    private readonly string? _requiredUnlock;
    private readonly int _requiredSevere;
    private readonly float _recentDriveOff, _recentSwitch;
    internal NativeBehaviorBaseInputs(EngineRobot robot,JsonElement config)
    {
        _robot=robot;
        _requiredUnlock=config.TryGetProperty("requiredUnlockId",out var unlock)?unlock.GetString():null;
        _requiredSevere=config.TryGetProperty("requiredSevereNeedsState",out var need)
            ?need.GetString() switch{"Repair"=>0,"Energy"=>1,"Play"=>2,"Count"=>3,_=>-1}:3;
        _recentDriveOff=config.TryGetProperty("requiredRecentDriveOffCharger_sec",out var drive)?drive.GetSingle():-1f;
        _recentSwitch=config.TryGetProperty("requiredRecentSwitchToParent_sec",out var change)?change.GetSingle():-1f;
    }
    internal bool IsCarrying=>_robot.CarryingOwner is {} owner && (owner.CarriedObjectId??uint.MaxValue)!=uint.MaxValue;
    internal bool Allows(BehaviorContext context)
    {
        if(!ReferenceEquals(context.Robot.Engine.Robot,_robot) || _robot.CarryingOwner is null)
        {SteppedBehavior.ReportMissing("IBehavior native base admission: actual Robot/Carrying owner unavailable");return false;}
        if(_requiredSevere!=3 && _requiredSevere!=context.Robot.Engine.Needs.SevereNeeds.Selected)return false;
        if(_requiredUnlock is not null)
        {SteppedBehavior.ReportMissing("IBehavior requiredUnlockId 0x005BD810: non-sentinel progression input is not bound");return false;}
        float epsilon=BitConverter.UInt32BitsToSingle(0x3727C5AC);
        float now=context.Robot.Engine.Timer.SecondsF;
        if(!(_recentDriveOff < -epsilon))
        {
            float stamp=_robot.LastDriveOffChargerSeconds;
            if(stamp < -epsilon || !((_recentDriveOff+stamp)+epsilon>=now))return false;
        }
        // Shipped DriveOff leaves this at -1. A host activity timestamp is not
        // evidence of the native manager+0x50 producer (RB3).
        if(!((double)_recentSwitch < BitConverter.Int64BitsToDouble(unchecked((long)0xBEE4F8B588E368F1))))
        {SteppedBehavior.ReportMissing("IBehavior requiredRecentSwitchToParent_sec 0x005BD84A: native manager+0x50 writer is not bound");return false;}
        return true;
    }
}
