using System.Text.Json;
namespace Cozmo.Robot.Behavior;

// 0059D086/0059EC00/34/38/3C: no action, persistent own Update.
internal sealed class NativeWaitBehavior(JsonElement config) : SteppedBehavior(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(),"Wait")
{
    protected override bool KeepsRunningWithoutAction=>true;
    public override bool IsRunnable(BehaviorContext context)=>IsRunnableBase(context)&&IsRunnableInternal(context);
    protected override bool IsRunnableInternal(BehaviorContext context)=>true;
    protected override bool RunnableGate20(BehaviorContext context)=>true;
    protected override bool RunnableGate24(BehaviorContext context)=>false;
    protected override bool RunnableGate28(BehaviorContext context)=>true;
    protected override void OnStart() { }
}
