using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

// fidelity: M10-014
public sealed partial class EngineRobot
{
    internal CubeConnections? BlockFilterOwner;
    internal RobotGyroDriftDetector? GyroDetector;
    private readonly SubscriptionOwners _blockFilterHandles = new();
    private void InitCheckedDerivedOwners()
    {
        Engine.BeforeDerivedConstructionForTest?.Invoke(this);
        GyroDetector = new RobotGyroDriftDetector(this);
        Lifetime.Bind(0x27C, _ => Engine.DerivedRetirementObservedForTest?.Invoke("gyro raw delete"));
        var filter = Engine.BlockFilterFactory?.Invoke() ?? new CubeConnections(m=>{Engine.SendMessage(m);},()=>Engine.Timer.SecondsF);
        BlockFilterOwner=filter;filter.ConstructFilterStorage();
        if(HasExternalInterface)
            foreach(ushort tag in new ushort[]{0xE7,0xE5,0xE6,0xE9,0xEA,0xEB})
                _blockFilterHandles.Add(Engine.AnimationMessages.Subscribe(tag,false,()=>
                    SteppedBehavior.ReportMissing($"M10-014 BlockFilter.HandleGameEvents tag {tag:X2}, 0x00619E7C..0x0061A156: operational body out of this lifetime job; no guessed mutation")));
        Lifetime.Bind(0x44C, _ =>
        {
            _blockFilterHandles.Retire(); // EB,EA,E9,E6,E5,E7; destroy callable, never invoke handler.
            Engine.DerivedRetirementObservedForTest?.Invoke("filter subscriptions retired");
            filter.RetireFilterStorage(Engine.DerivedRetirementObservedForTest);
            Engine.DerivedRetirementObservedForTest?.Invoke("filter allocation retired");
        });
    }
}

public sealed partial class CozmoEngine
{
    internal Func<CubeConnections>? BlockFilterFactory;
    internal Action<string>? DerivedRetirementObservedForTest;
    internal Action<EngineRobot>? BeforeDerivedConstructionForTest;
}
