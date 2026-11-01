using System.Text.Json;
namespace Cozmo.Robot.Behavior;

// fidelity: M11-089, M8-013
/// <summary>Owns checked goal units per native behavior instance; unread lifecycle is an explicit stop.</summary>
public sealed class VisitInterestingEdgeBehavior : SteppedBehavior
{
    public InterestingEdgeGoalSelector Goals {get;}=new();
    public Func<BehaviorContext,InterestingEdgeInputs?>? NativeInputs {get;set;}
    private EngineRobot? _originalRobot;
    public VisitInterestingEdgeBehavior(string id,InterestingEdgeConfig config):base(id,"VisitInterestingEdge")
    {RequiredProcessId=1;Goals.Config=config;}
    public static VisitInterestingEdgeBehavior FromConfig(JsonElement json,EngineRobot? originalRobot=null)
    {
        var result=new VisitInterestingEdgeBehavior(BehaviorConfigLoader.ExtractIdFromConfig(json).ToString(),InterestingEdgeConfig.Read(json.GetProperty("params")));
        result._originalRobot=originalRobot;
        ReportMissing("M11-092 VisitInterestingEdge constructor/lifecycle input joins remain unbuilt; checked goal/config unit only, activation is stopped");
        return result;
    }
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        if(NativeInputs is null && _originalRobot is {} robot)
        {
            var runtime=robot.NativeBehaviorRuntime;
            if(!ReferenceEquals(robot.Engine.Robot,robot) || runtime is null || !ReferenceEquals(runtime.Context,context) ||
               !runtime.Bound.TryGetValue(Id,out var configured) || !ReferenceEquals(configured,this) ||
               !ReferenceEquals(context.AI,runtime.AI) || runtime.AI.Analyzer.ActualBorderResultEmpty()!=true)
            {MarkReadinessUnavailable();ReportMissing("M11-079/M11-089 original analyzer border result unavailable");return false;}
            if(!Goals.ConsumeKnownEmptyBorders(()=>ReferenceEquals(robot.Engine.Robot,robot) && ReferenceEquals(robot.NativeBehaviorRuntime,runtime) && runtime.AI.Analyzer.ActualBorderResultEmpty()==true))
                MarkReadinessUnavailable();
            return false;
        }
        var input=NativeInputs?.Invoke(context);
        if(input is null)
        {MarkReadinessUnavailable();ReportMissing("M11-089 live native border-vector/map/RNG/robot-root input join at 0x005E6520 -> 0x005E682C; M11-092 lifecycle remains unread");return false;}
        var result=Goals.IsRunnable(input);
        if(result is null)
        {MarkReadinessUnavailable();ReportMissing("M11-089 VisitInterestingEdge goal readiness input unavailable");return false;}
        return result.Value;
    }
    protected override void OnStart()
    {
        ReportMissing("M11-092 VisitInterestingEdge InitInternal/activation lifecycle 0x005E7018..0x005E815A");
        throw new NotSupportedException("M11-092 native VisitInterestingEdge activation is unread");
    }
    protected override void OnStop(BehaviorStopReason reason)
        =>ReportMissing("M11-092 VisitInterestingEdge StopInternal/Flag lifecycle 0x005E7018..0x005E815A; explicit type10 FlagQuadAsNotInterestingEdges 0x0067E6B0/0x0067E71C writer join remains unbuilt");
}
