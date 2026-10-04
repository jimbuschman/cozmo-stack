namespace Cozmo.Robot.Behavior;

// fidelity: M8-011
/// <summary>
/// The host of the engine's <c>BehaviorHelperComponent</c>: this stack's minimal <c>AIComponent</c> (the engine's is at <c>[robot+0x264]</c>; its +0x10 is the
/// helper component, constructed at 0x00569aa4..0x00569ab2). No other class in this stack stood for the <c>AIComponent</c> (the whiteboard lives in
/// <c>ManipulationSystem</c> and the <c>FreeplayDataTracker</c> on <see cref="FreeplayStack"/>), so this one hosts only the helper component; the other members
/// of <c>AIComponent::Update</c> (<c>AIInformationAnalyzer::Update</c>, <c>AIWhiteboard::Update</c> (a no-op), <c>SevereNeedsComponent::Update</c> and
/// <c>FreeplayDataTracker::Update</c>) are not moved here.
///
/// <see cref="Update"/> is <c>AIComponent::Update</c>'s helper step: 0x00569f3c..0x00569f40 passes <c>[this]</c> (the robot) to
/// <c>BehaviorHelperComponent::Update</c> (<c>[this+0x10]</c>) after <c>SevereNeedsComponent::Update</c>. Its only caller is <c>Robot::Update</c> 0x00513eac, before
/// <c>BehaviorManager::Update</c> (0x00513ee6) and after the first-full-state gate ([robot+0x34e], 0x00513c5c) and the
/// <c>VisionComponent::UpdateAllResults</c> gate (0x00513c76); <c>EngineRobot.Update</c> runs it through <c>CozmoEngine.AIComponentUpdate</c> at that position.
/// </summary>
public sealed class AIComponent
{
    /// <param name="worldOriginId"><c>Robot::GetWorldOriginID</c>.</param>
    /// <param name="log">The engine log line sink.</param>
    public AIComponent(Func<int> worldOriginId, Action<string>? log = null)
    {
        Helpers = new BehaviorHelperComponent(worldOriginId, log);
    }

    // fidelity: M10-009
    /// <summary>
    /// <c>AIComponent+4</c>, the byte <c>StrategyObstacleDetected</c>'s predicate returns (<c>[[robot+0x264]+4]</c>, 0x006143CA; the strategy is
    /// <c>StrategyObstacleDetected</c> 0x006141F8, shipped as <c>ReactToObstacle</c>'s wants-to-run strategy). The component's constructor zeroes it (0x00569A80)
    /// and <b>nothing in this build ever writes it</b> (no store to +4 anywhere in AIComponent code and no function that fetches the component from
    /// Robot+0x264 writes it; M10 inventory). So it is false for the life of the component, and <c>ReactToObstacle</c>, which four freeplay activities list, never runs.
    /// The setter is internal and exists only so a test of the strategy's read can put a value there (the engine has no such writer; nothing in the stack's
    /// production code calls it).
    /// </summary>
    public bool ObstacleDetected { get; internal set; }

    /// <summary><c>AIComponent+0x10</c>.</summary>
    public BehaviorHelperComponent Helpers { get; }

    /// <summary>The helper step of <c>AIComponent::Update</c> (0x00569f40 <c>blx 0x4aca3c</c> = <c>BehaviorHelperComponent::Update</c>).</summary>
    public void Update(CozmoRobot robot) => Helpers.Update(robot);
}
