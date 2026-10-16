namespace Cozmo.Robot.Behavior;

// fidelity: M8-011
/// <summary>
/// The host of the engine's <c>BehaviorHelperComponent</c>: this stack's minimal <c>AIComponent</c> (the engine's is at <c>[robot+0x264]</c>; its +0x10 is the
/// helper component, constructed at 0x00569aa4..0x00569ab2). No other class in this stack stood for the <c>AIComponent</c> (the whiteboard lives in
/// <c>ManipulationSystem</c> and the <c>FreeplayDataTracker</c> on <see cref="FreeplayStack"/>). This owner also hosts the analyzer at +8.
/// Whiteboard (a no-op), severe needs and tracker ownership/update joins remain separate gaps or existing paths.
///
/// <see cref="Update"/> is <c>AIComponent::Update</c>'s helper step: 0x00569f3c..0x00569f40 passes <c>[this]</c> (the robot) to
/// <c>BehaviorHelperComponent::Update</c> (<c>[this+0x10]</c>) after <c>SevereNeedsComponent::Update</c>. Its only caller is <c>Robot::Update</c> 0x00513eac, before
/// <c>BehaviorManager::Update</c> (0x00513ee6) and after the first-full-state gate ([robot+0x34e], 0x00513c5c) and the
/// <c>VisionComponent::UpdateAllResults</c> gate (0x00513c76); <c>EngineRobot.Update</c> runs it through <c>CozmoEngine.AIComponentUpdate</c> at that position.
/// </summary>
public sealed class AIComponent
{
    private readonly Dictionary<int,Action?> _destructionRecipients=new();
    internal void BindDestructionRecipient(int offset,Action? recipient)=>_destructionRecipients[offset]=recipient;
    internal bool IsChildRetired(int offset)=>_destructionRecipients.TryGetValue(offset,out var value)&&value is null;
    // fidelity: M15-027 — checked direct slots only; recursive missing recipients remain M15-045.
    internal void DestroyDirect()
    {
        foreach(int offset in new[]{0x30,0x2C,0x28,0x24,0x20,0x1C,0x18,0x14,0x10,0xC,0x8})
        {
            bool known=_destructionRecipients.TryGetValue(offset,out var recipient);
            _destructionRecipients[offset]=null;
            if(known)recipient?.Invoke();
            else SteppedBehavior.ReportMissing($"M15-027/M15-045 AIComponent child +0x{offset:X} direct null-before-delete at 0x00569CB2..0x00569DAC: concrete owner/recursive destruction recipient is absent; no guessed callback or raw deletion");
        }
    }
    /// <param name="worldOriginId"><c>Robot::GetWorldOriginID</c>.</param>
    /// <param name="log">The engine log line sink.</param>
    public AIComponent(Func<int> worldOriginId, Action<string>? log = null, SourceLogSink? sourceLog = null)
    {
        Helpers = new BehaviorHelperComponent(worldOriginId, log, sourceLog);
        Analyzer = new AIInformationAnalyzer(SourceLogDispatch.GetSink(sourceLog, log));
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

    // fidelity: M11-079 — native +8, constructor 0x005741A4..0x005741F6.
    public AIInformationAnalyzer Analyzer { get; }

    /// <summary>Analyzer first (0x00569F2C), then the built helper step (0x00569F40); other AI steps are not claimed here.</summary>
    public void Update(CozmoRobot robot)
    {
        Analyzer.Update(); // 0x00569F2C, before the helper at 0x00569F40.
        Helpers.Update(robot);
    }
}
