using System.Text.Json;
namespace Cozmo.Robot.Behavior;

// fidelity: M11-089, M8-013
/// <summary>Owns checked goal units per native behavior instance; unread lifecycle is an explicit stop.</summary>
public sealed class VisitInterestingEdgeBehavior : SteppedBehavior
{
    public InterestingEdgeGoalSelector Goals {get;}=new();
    public Func<BehaviorContext,InterestingEdgeInputs?>? NativeInputs {get;set;}
    public VisitInterestingEdgeBehavior(string id,InterestingEdgeConfig config):base(id,"VisitInterestingEdge")
    {RequiredProcessId=1;Goals.Config=config;}
    public static VisitInterestingEdgeBehavior FromConfig(JsonElement json)
    {
        var result=new VisitInterestingEdgeBehavior(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString(),InterestingEdgeConfig.Read(json.GetProperty("params")));
        ReportMissing("M11-092 VisitInterestingEdge constructor/lifecycle input joins remain unbuilt; checked goal/config unit only, activation is stopped");
        return result;
    }
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        var input=NativeInputs?.Invoke(context);
        if(input is null)
        {ReportMissing("M11-089 live native border-vector/map/RNG/robot-root input join at 0x005E6520 -> 0x005E682C; M11-092 lifecycle remains unread");return false;}
        return Goals.IsRunnable(input)==true;
    }
    protected override void OnStart()
    {
        ReportMissing("M11-092 VisitInterestingEdge InitInternal/activation lifecycle 0x005E7018..0x005E815A");
        throw new NotSupportedException("M11-092 native VisitInterestingEdge activation is unread");
    }
    protected override void OnStop(BehaviorStopReason reason)
        =>ReportMissing("M11-092 VisitInterestingEdge StopInternal/Flag lifecycle 0x005E7018..0x005E815A");
}
