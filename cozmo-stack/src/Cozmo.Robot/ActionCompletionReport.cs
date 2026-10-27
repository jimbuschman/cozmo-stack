namespace Cozmo.Robot;
/// <summary>Known action completion fields. NativeCompletionCache exposes the existing runner cache; concrete completion-union recovery remains with the action's owner (for animation, M8-020).</summary>
public sealed record ActionCompletionReport(uint Tag,int ActionType,uint Result,IReadOnlyList<uint> SubActionResults,uint NativeCompletionCache)
{
    public string? AnimationName {get;init;}
    public byte? CompletionVariant {get;init;}
    public ObjectInteractionCompletion? ObjectInteraction {get;init;}
}
// fidelity: M8-015, M8-020
public sealed partial class CozmoEngine
{
    public event Action<ActionCompletionReport>? ActionCompleted;
    internal void BroadcastActionCompletion(RobotCompletedAction record)
    {
        var typed=new ActionCompletionReport(record.Tag,record.ActionType,record.Result,
            Array.AsReadOnly(record.SubActionResults.ToArray()),record.CompletionUnion)
            {AnimationName=record.AnimationName,CompletionVariant=record.CompletionVariant,ObjectInteraction=record.ObjectInteraction};
        if(record.ObjectInteraction is {IsPayloadDefined:false})
            Behavior.SteppedBehavior.ReportMissing("M1-034 Pickup completion5537D4 undefined words/count: public 5A publication skipped; defined internal flag/result retained");
        else if(record.ObjectInteraction is not null||record.CompletionVariant is 1 or 6)
            DeliverAnimationReportToApp(RobotCompletedAction.UnionTag,record.PackBody(),CaptureAppEvent(ActionCompleted,typed));
        else QueueAppBroadcast(ActionCompleted,typed); // Other concrete unions retain their named raw-cache gap.
        AnimationMessages.Deliver(RobotCompletedAction.UnionTag,true,record);
    }
}
