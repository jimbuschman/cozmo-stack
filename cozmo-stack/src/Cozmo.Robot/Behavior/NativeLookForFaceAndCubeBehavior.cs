using System.Text.Json;

namespace Cozmo.Robot.Behavior;

// C7 LC1-LC9: concrete retained nonrunning candidate. Selected action execution
// is unavailable; NativeBehaviorRuntime guards that boundary before Init/Resume.
internal sealed class NativeLookForFaceAndCubeBehavior : SteppedBehavior, IDisposable
{
    internal JsonElement RootConfig { get; }
    internal bool ObjectiveInputsAvailable {get;private set;}=true;
    private string _unsupportedObjectiveInput="";
    internal bool HasCheckedObjective41=>ObjectiveInputsAvailable&&BehaviorObjective==BehaviorObjective.Invalid;
    internal void CheckObjectiveInput(JsonElement scoring,string location)
    {
        if(scoring.ValueKind!=JsonValueKind.Object || !scoring.TryGetProperty("considerThisHasRunForBehaviorObjective",out var value) || value.ValueKind==JsonValueKind.Null)return; //005BC574..57A isNull preserves41.
        ObjectiveInputsAvailable=false;_unsupportedObjectiveInput=location;
        ReportMissing("LookForFaceAndCube changed-config objective input at "+location+"="+value.GetRawText()+
            "; source string mapping/stopstamp mutation unavailable, shipped absent-key invalid41 proof cannot be used");
    }
    private RetainedSubscription? _inactiveFaces,_objective;
    internal NativeLookForFaceAndCubeBehavior(BehaviorFactoryContext source, JsonElement root)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(root).ToString(), "LookForFaceAndCube")
    {
        RootConfig=root.Clone();
        CheckObjectiveInput(root,"behavior root "+Id);
        // C7.1: IBehavior tail unconditionally calls ScoredConstructor. Both
        // shipped root and activity scoring omit objective, retaining invalid41;
        // actual5BF00C returns BEFORE payload/time access for that value.
        if(source.NativeRobot is {HasExternalInterface:true} && source.Manipulation is {} objectiveOwner)
            _objective=objectiveOwner.Robot.Engine.AnimationMessages.SubscribeMessage(0x8B,true,payload=>
            {
                if(!ObjectiveInputsAvailable)
                {
                    ReportMissing("LookForFaceAndCube objective callback unavailable for explicit raw input at "+_unsupportedObjectiveInput+"; no silent invalid41 filter substituted");
                    return;
                }
                if(BehaviorObjective==BehaviorObjective.Invalid)return; // FIRST guard005BF014/16.
                if(payload is not uint raw || raw!=(uint)BehaviorObjective)return;
                ReportMissing("LookForFaceAndCube changed-config matching objective stopstamp005BF028 producer unavailable; shipped invalid41 unit does not certify this mutation");
            });
        // Shipped verifySeenFaces=true makes the actual constructor subscribe46.
        bool subscribes=root.TryGetProperty("params",out var parameters) &&
            new[]{"verifySeenFaces","stopBehaviorOnAnyFace","stopBehaviorOnNamedFace"}.Any(key=>
                parameters.TryGetProperty(key,out var value) && value.ValueKind==JsonValueKind.True);
        if(subscribes && source.NativeRobot is {HasExternalInterface:true} && source.Manipulation is {} m)
            _inactiveFaces=m.Robot.Engine.AnimationMessages.SubscribeMessage(0x46,true,_=>
            {
                // Actual inactive virtual target0059EC48 is BX LR.
                if(EngineRunning)ReportMissing("LookForFaceAndCube running tag46 handler005F087C unavailable");
            });
    }
    public override bool IsRunnable(BehaviorContext context)=>IsRunnableBase(context)&&IsRunnableInternal(context);
    protected override bool IsRunnableInternal(BehaviorContext context)=>true; //005EFD54
    protected override bool RunnableGate20(BehaviorContext context)=>false;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>false;
    protected override void OnStart()=>throw new InvalidOperationException("MISSING: LookForFaceAndCube selected Init/Resume/action body; nonrunning candidate only");
    protected override void OnStop(BehaviorStopReason reason){} //005F0878 BX LR
    public void Dispose(){_inactiveFaces?.Dispose();_inactiveFaces=null;_objective?.Dispose();_objective=null;}
}
