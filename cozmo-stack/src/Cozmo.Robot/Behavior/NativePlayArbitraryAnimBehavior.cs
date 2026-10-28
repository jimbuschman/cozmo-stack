using System.Text.Json;
using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

// fidelity: M7-018, M8-005
// P1-P6: 005C0784..08CC and shared sequence 005C0158..035A.
internal sealed class NativePlayArbitraryAnimBehavior : SteppedBehavior
{
    private readonly List<AnimationTrigger> _triggers=new();
    private int _loops=-1,_index;
    internal IReadOnlyList<AnimationTrigger> Triggers=>_triggers;
    internal int Loops=>_loops;
    internal bool TriggerSet {get;private set;}
    internal NativePlayArbitraryAnimBehavior(JsonElement json):base(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString(),"PlayArbitraryAnim"){}
    internal void SetAnimationTrigger(AnimationTrigger trigger,int loops)=>SetAnimationTriggers(new[]{trigger},loops);
    internal void SetAnimationTriggers(IEnumerable<AnimationTrigger> triggers,int loops)
    {_triggers.Clear();_triggers.AddRange(triggers);TriggerSet=true;_loops=loops;}
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>true;
    protected override bool IsRunnableInternal(BehaviorContext context)=>_loops>=0;
    public override bool IsRunnable(BehaviorContext context)=>IsRunnableBase(context)&&IsRunnableInternal(context);
    protected override bool KeepsRunningWithoutAction=>HasCurrentAction;
    protected override int ResumeInternal()=>0;
    protected override void OnStop(BehaviorStopReason reason){TriggerSet=false;_triggers.Clear();}
    protected override void OnStart()
    {
        TriggerSet=false;
        if(_triggers.Count==1){StartActing(Action(_triggers[0],_loops),_=>CallToListeners());return;}
        _index=0;NextLoop();
    }
    // P6: this actual ActivitySparked caller registers no listener. Preserve the
    // native callable even with its empty set; general listener registration is M8-005.
    private void CallToListeners(){}
    private TriggerLiftSafeAnimationAction Action(AnimationTrigger trigger,int loops)=>new(Context.Robot,new(trigger,loops,true,0,60f,false));
    private void NextLoop()
    {
        if(_index>=_loops)return;
        var sequence=new CompoundActionSequential(()=>Context.Robot.Engine.Timer.SecondsF);
        foreach(var trigger in _triggers)sequence.AddAction(Action(trigger,1),false,false);
        _index=unchecked(_index+1);
        // P5: multi completion starts the next loop; no single-trigger listener tail.
        StartActing(sequence,_=>NextLoop());
    }
}
