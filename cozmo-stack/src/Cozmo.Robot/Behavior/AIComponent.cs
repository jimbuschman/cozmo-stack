using Cozmo.Protocol;
namespace Cozmo.Robot.Behavior;

// fidelity: M8-011
/// <summary>
/// The host of the engine's <c>BehaviorHelperComponent</c>: this stack's minimal <c>AIComponent</c> (the engine's is at <c>[robot+0x264]</c>; its +0x10 is the
/// helper component, constructed at 0x00569aa4..0x00569ab2). No other class in this stack stood for the <c>AIComponent</c> (the whiteboard lives in
/// <c>ManipulationSystem</c> and the <c>FreeplayDataTracker</c> on <see cref="FreeplayStack"/>). This owner also hosts the analyzer at +8.
/// Whiteboard is a no-op; the shared severe-needs owner follows it, and the tracker retains its existing path.
///
/// <see cref="Update"/> is <c>AIComponent::Update</c>'s helper step: 0x00569f3c..0x00569f40 passes <c>[this]</c> (the robot) to
/// <c>BehaviorHelperComponent::Update</c> (<c>[this+0x10]</c>) after <c>SevereNeedsComponent::Update</c>. Its only caller is <c>Robot::Update</c> 0x00513eac, before
/// <c>BehaviorManager::Update</c> (0x00513ee6) and after the first-full-state gate ([robot+0x34e], 0x00513c5c) and the
/// <c>VisionComponent::UpdateAllResults</c> gate (0x00513c76); <c>EngineRobot.Update</c> runs it through <c>CozmoEngine.AIComponentUpdate</c> at that position.
/// </summary>
public sealed class AIComponent
{
    internal NativeAiTeardown NativeTeardown {get;}=new();
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
    public AIComponent(Func<int> worldOriginId, Action<string>? log = null, SourceLogSink? sourceLog = null, SevereNeedsComponent? severeNeeds = null)
    {
        SevereNeeds = severeNeeds ?? new SevereNeedsComponent();
        Helpers = new BehaviorHelperComponent(worldOriginId, log, sourceLog);
        Analyzer = new AIInformationAnalyzer(SourceLogDispatch.GetSink(sourceLog, log));
        foreach(int offset in new[]{0x30,0x20,0x1C,0x18,0x14,0x10,0x8})
        {int child=offset;BindDestructionRecipient(child,()=>NativeTeardown.Destroy(child));}
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
    internal NativePickupResponseDirector PickupResponses {get;}=new();
    // AI+0x1C. Null means the actual configuration could not be loaded.
    internal Cozmo.Robot.Manipulation.WorkoutComponent? Workout {get;set;}
    internal FreeplayDataTracker? Tracker {get;set;}

    // fidelity: M11-079 — native +8, constructor 0x005741A4..0x005741F6.
    public AIInformationAnalyzer Analyzer { get; }
    // fidelity: M15-072 -- AI+0x30. The live stack shares this same owner with every needs consumer.
    public SevereNeedsComponent SevereNeeds { get; }

    /// <summary>Analyzer, the whiteboard's no-op, severe needs (0x00569F38), then helper (0x00569F40).</summary>
    public void Update(CozmoRobot robot)
    {
        Analyzer.Update(); // 0x00569F2C, before the helper at 0x00569F40.
        // Whiteboard's existing Update is a no-op. SevereNeeds follows it at 0x00569F38.
        SevereNeeds.Update();
        Helpers.Update(robot);
        Tracker?.Update(); // 0x00569F44/46, after helpers
    }
}

// fidelity: M15-072, M15-073
public sealed class SevereNeedsComponent : IDisposable
{
    internal const string LockName="severe_need_component_lock";
    private volatile int _selected=3; // 0x00572A0E; readers can observe from host callbacks
    public int Selected=>_selected;
    internal NeedId? SelectedNeed {get{int selected=_selected;return selected==3?null:(NeedId)selected;}}
    internal Func<NeedId,NeedBracketId,bool>? IsAtBracket;
    internal Action<AnimationTrigger,AnimationTrigger,AnimationTrigger,string>? PushDriving;
    internal Action<AnimationTrigger,string>? PushIdle;
    internal Action<string,IReadOnlyList<bool>,bool>? DisableReactions;
    internal Action<string>? RemoveDriving,RemoveIdle,RemoveReactions;
    internal SourceLogSink? SourceLog;
    internal CozmoEngine? Engine;
    private IDisposable? _wake;
    internal void ResetForConstruction(){Dispose();_selected=3;} // new native AI component, not Clear's side effects
    private static readonly bool[] CriticalTable="011111011011101111010".Select(c=>c=='1').ToArray(); // 0x00C59670
    private static readonly bool[] PlayTable=new bool[21]; // 0x00C5969A
    private static void Missing(string step)=>SteppedBehavior.ReportMissing("M15-072 SevereNeeds "+step+": recipient is unbound");
    private void Run(Action action)
    {if(Engine is {} engine)engine.InvokeEngineRequest(()=>{action();return 0;});else action();}
    internal void Set(NeedId need)=>Run(()=>SetCore(need));
    private void SetCore(NeedId need)
    {
        if((uint)need>2)throw new ArgumentOutOfRangeException(nameof(need)); // unsupported host enum, no invented table
        SourceLog?.Invoke(EngineLogLevel.Info,null,"SevereNeedsComponent.SevereNeedExpression.Set",$"Set to {need} (was {(_selected==3?"Count":((NeedId)_selected).ToString())})");
        if(need is NeedId.Energy or NeedId.Repair)
        {
            int start=need==NeedId.Energy?0x134:0x140;
            if(PushDriving is {} driving)driving((AnimationTrigger)start,(AnimationTrigger)(start-1),(AnimationTrigger)(start-2),LockName);
            else Missing("PushDrivingAnimations 0x005730E2");
            if(PushIdle is {} idle)idle((AnimationTrigger)(need==NeedId.Energy?0x137:0x143),LockName);
            else Missing("PushIdleAnimation 0x00573152");
        }
        if(DisableReactions is {} reactions)reactions(LockName,need==NeedId.Play?PlayTable:CriticalTable,true);
        else Missing("DisableReactionsWithLock 0x005731A0");
        _selected=(int)need; // 0x005731B4, after every recipient; repeated Set still pushes
    }
    internal void Clear()=>Run(ClearCore);
    private void ClearCore()
    {
        SourceLog?.Invoke(EngineLogLevel.Info,null,"SevereNeedsComponent.SevereNeedExpression.Clear",$"{(_selected==3?"Count":((NeedId)_selected).ToString())}");
        if(RemoveDriving is {} driving)driving(LockName);else Missing("RemoveDrivingAnimations 0x00572CF6");
        if(RemoveIdle is {} idle)idle(LockName);else Missing("RemoveIdleAnimation 0x00572D26");
        if(RemoveReactions is {} reactions)reactions(LockName);else Missing("RemoveDisableReactionsLock 0x00572D56");
        _selected=3; // 0x00572D6A, including Clear from3
    }
    internal void Update()=>Run(UpdateCore);
    private void UpdateCore()
    {
        if(_selected==3)return;
        if(IsAtBracket is not {} bracket){Missing("IsNeedAtBracket 0x00572BEA");return;}
        if(bracket((NeedId)_selected,NeedBracketId.Critical))return;
        SourceLog?.Invoke(EngineLogLevel.Info,null,"SevereNeedsState.AutoClear",$"{(NeedId)_selected}");
        Clear();
    }
    internal void Wake()=>Run(WakeCore);
    private void WakeCore()
    {
        if(IsAtBracket is not {} bracket){Missing("wake IsNeedAtBracket 0x00572E14/0x00572E20");return;}
        bool repair=bracket(NeedId.Repair,NeedBracketId.Critical);
        bool energy=bracket(NeedId.Energy,NeedBracketId.Critical); // always queried, even when repair is critical
        if(repair)Set(NeedId.Repair);else if(energy)Set(NeedId.Energy);
    }
    internal void Subscribe(CozmoEngine engine,bool hasExternalInterface)
    {if(hasExternalInterface)_wake??=engine.AnimationMessages.SubscribeMessage(166,false,_=>Wake());}
    public void Dispose(){_wake?.Dispose();_wake=null;} // destructor retires subscription; it does not call Clear
}
