using System.Text.Json;

namespace Cozmo.Robot.Behavior;

// Actual configured IBehavior inputs. Standalone host seams remain separate.
// ctor 0x005BBCAC..BE; ReadFromJson 0x005BC118..1B0; RB2-RB4.
internal sealed class NativeBehaviorBaseInputs
{
    private readonly EngineRobot _robot;
    private readonly uint _requiredUnlock;
    internal uint RequiredUnlock=>_requiredUnlock;
    // Checked common+6C metadata for the supported Stack configs only. Other explicit names
    // remain unavailable here; this is not an executable finder or a general enum parser.
    internal byte? CheckedExecutableType {get;}
    private readonly int _requiredSevere;
    private readonly float _recentDriveOff, _recentSwitch;
    internal NativeBehaviorBaseInputs(EngineRobot robot,JsonElement config)
    {
        _robot=robot;
        CheckedExecutableType=!config.TryGetProperty("executableBehaviorType",out var executable)||executable.ValueKind!=JsonValueKind.String
            ?(byte)12:executable.GetString()=="StackBlocks"?(byte)9:null; // ctor005BBCA2/A4; parser005BC1BE..C4, checked supplement.
        _requiredUnlock=config.TryGetProperty("requiredUnlockId",out var unlock)&&unlock.ValueKind!=JsonValueKind.Null?NativeUnlockNames.FromBytes(System.Text.Encoding.UTF8.GetBytes(unlock.GetString()!)):0x55;
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
        if(_requiredUnlock!=0x55)
        {
            if(_robot.Progression is not {} progression||!progression.TryIsUnlocked(_requiredUnlock,out bool unlocked))
            {SteppedBehavior.ReportMissing("IBehavior requiredUnlockId005BD810 actual progression override state unavailable; no hardcoded unlock");return false;}
            if(!unlocked)return false;
        }
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
