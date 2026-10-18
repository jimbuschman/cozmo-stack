namespace Cozmo.Robot;
/// <summary>Known action completion fields. NativeCompletionCache exposes the existing runner cache; concrete completion-union recovery remains with the action's owner (for animation, M8-020).</summary>
public sealed record ActionCompletionReport(uint Tag,int ActionType,uint Result,IReadOnlyList<uint> SubActionResults,uint NativeCompletionCache);
// fidelity: M8-015, M8-020
public sealed partial class CozmoEngine
{
    public event Action<ActionCompletionReport>? ActionCompleted;
    internal void BroadcastActionCompletion(RobotCompletedAction record)
    {
        QueueAppBroadcast(ActionCompleted,new ActionCompletionReport(record.Tag,record.ActionType,record.Result,
            Array.AsReadOnly(record.SubActionResults.ToArray()),record.CompletionUnion));
        AnimationMessages.Deliver(RobotCompletedAction.UnionTag,true,record);
    }
}
