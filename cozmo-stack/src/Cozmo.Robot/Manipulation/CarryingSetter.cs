using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
namespace Cozmo.Robot.Manipulation;

public sealed partial class CarryingComponent
{
    // fidelity: M12-046
    internal EngineRobot? NativeRobot;
    internal SourceLogSink? SourceLog;

    /// <summary>Set the carried ID and marker using the engine's located-object lookup and carry-state send.</summary>
    internal void SetCarryingObject(uint objectId, ushort markerCode)
    {
        if (NativeRobot?.Engine.DelocalizationWorld is { } world) SetCarryingObject(world, objectId, markerCode);
        else SteppedBehavior.ReportMissing("M12-046 SetCarryingObject 0x00633E88: native Robot/BlockWorld owner is unbound; preserve carrying state");
    }

    // 0x00633E78..0x00633F0E takes an ObjectID and a marker, not an object pointer.
    // The null test is on the located lookup result. A miss preserves all carry fields.
    internal bool SetCarryingObject(BlockWorld world, uint id, ushort marker)
    {
        using var mutation = world.EnterPoseMutation();
        if (world.GetLocatedObjectByIdHelper(id) is null)
        {
            string text = FormattableString.Invariant($"Object {unchecked((int)id)} no longer exists in the world. Can't set it as robot's carried object.");
            if (NativeRobot is { } robot) robot.Engine.EmitSourceLog(EngineLogLevel.Error, null, "Robot.SetCarryingObject.NullCarryObject", text);
            else SourceLog?.Invoke(EngineLogLevel.Error, null, "Robot.SetCarryingObject.NullCarryObject", text);
            EngineErrorState.StoreAndMaybeBreak();
            return false;
        }
        lock (_gate) { NativeMarkerCode = marker; _carried = id; }
        if (world.LocalizedToObjectId == id) world.SetLocalizedTo(null);
        uint? top;
        lock (_gate) top = _onTopId;
        SendSetCarryState(top is not null && top != uint.MaxValue ? CarryState.CARRY_2_BLOCK : CarryState.CARRY_1_BLOCK);
        Changed?.Invoke(id);
        return true;
    }

    internal void SendSetCarryState(CarryState state)
    {
        // 0x00632C44..0x00632C6E: EngineToRobot tag 0x49, one byte, SendMessage(true,false).
        if (NativeRobot is { } robot) robot.SendChecked(new CarryStateUpdate { State = state });
        else SteppedBehavior.ReportMissing("M12-046 SendSetCarryState 0x00632C6E: native Robot send recipient is absent; no fabricated send");
    }
}
