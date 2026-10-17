using Cozmo.Robot.Manipulation;
namespace Cozmo.Robot;

// fidelity: M12-040 — Docking ctor 0x0063BA1C..0x0063BA3E; the existing facade shares these fields.
internal sealed class DockingComponentData
{
    internal byte Active;
    internal bool Success;
    internal uint? TargetId;
    internal ushort MarkerCode = 3;
    // Slots +14/+18/+1C are zeroed by the ctor. Their producers/consumers are outside this lifetime row.
    internal uint Slot14, Slot18, Slot1C;
}
public sealed partial class EngineRobot
{
    internal DockingComponentData? DockingOwner { get; set; }
    internal CarryingComponent? CarryingOwner { get; set; }
    // fidelity: M12-040 — 0x0050FE24..0x0050FE4A, Docking then Carrying, before first Delocalize.
    internal void InitDockingCarryingOwners()
    {
        DockingOwner = new();
        Engine.DockingCarryingOwnerObservedForTest?.Invoke("new Docking");
        CarryingOwner = new();
        CarryingOwner.NativeRobot = this;
        Engine.CarryingOwnerConstructed?.Invoke(CarryingOwner); // host facade names the newly constructed native storage before Delocalize.
        Engine.DockingCarryingOwnerObservedForTest?.Invoke("new Carrying");
    }
}
public sealed partial class CozmoEngine
{
    internal Action<CarryingComponent>? CarryingOwnerConstructed;
    internal Action<string>? DockingCarryingOwnerObservedForTest;
}
