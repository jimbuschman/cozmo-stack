using Cozmo.Robot;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    // Host-only nonblocking admission. The native TrackFace action remains owned by the
    // ActionList (0x0052ACC0..0x0052ACE6, 0x00527A74..0x00527A8C) until its real terminal event.
    private async Task<CommandResult> TrackFace(int faceId, bool headOnly, CancellationToken token)
    {
        uint? admittedTag = null;
        var completion = Robot.Motion.QueueAiTrackFaceAsync(faceId, headOnly, token, tag => admittedTag = tag);
        if (admittedTag is not { } tag) return await FromAction(completion).ConfigureAwait(false);
        _ = ObserveTrackFaceCompletion(completion);
        return new CommandResult("submitted", true, null, null, "Native face tracking admitted; completion will be reported in events",
            Report: new { actionTag = tag, requestedFaceId = faceId });
    }

    private async Task ObserveTrackFaceCompletion(Task<AiTrackFaceActionResult> completion)
    {
        try { await completion.ConfigureAwait(false); }
        catch (Exception error) { Robot.Engine.EmitStackLog("Track-face completion observation failed: " + error.Message); }
    }

    private Task<CommandResult> CancelAction(uint tag, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var engine = Robot.Engine;
        var owner = engine.Robot;
        if (owner is null || !engine.IsReadyDelivered) return Task.FromResult(CommandResult.NotReady());
        var result = engine.InvokeEngineRequest(() =>
        {
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(engine.Robot, owner) || owner.Lifetime.Destroying) return CommandResult.NotReady();
            return owner.ActionList.CancelHostAction(tag, eventCaller)
                ? CommandResult.Posted() with { Report = new { actionTag = tag } }
                : new CommandResult("not-admitted", false, null, null, "No active action with this tag belongs to this caller.");
        });
        return Task.FromResult(result);
    }

    private async Task<CommandResult> PutDown(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var engine = Robot.Engine;
        var owner = engine.Robot;
        if (owner is null || engine.ConnectionState != 2 || !engine.IsReadyDelivered || engine.RobotValidated != true ||
            owner.Lifetime.Destroying || !owner.FirstFullStateHandled)
            return CommandResult.NotReady();

        if (owner.NativeBehaviorRuntime?.Manipulation is not { } manipulation)
            return CommandResult.Action(false, null, "MISSING: M15-022 original ground-placement owners unavailable", false);

        NativePlaceGroundRunner? runner = null;
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCompletion(ActionCompletionReport report)
        {
            // The bridge's permanent subscriber has appended this report first.
            if (runner is { } action && report.Tag == action.Tag && report.ActionType == action.Type)
                delivered.TrySetResult();
        }
        void OnDisposed() => delivered.TrySetCanceled();
        lock (eventGate)
        {
            ThrowIfDisposed();
            engine.ActionCompleted += OnCompletion;
            Disposing += OnDisposed;
        }
        try
        {
            string detail = "Native placement action ended";
            try
            {
                await manipulation.PlaceObjectOnGroundAsync(owner, token,
                    (action, _) => { runner = action; action.HostCaller = eventCaller; }).ConfigureAwait(false);
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("MISSING:", StringComparison.Ordinal))
            {
                detail = error.Message;
            }
            if (runner is null)
            {
                bool retired = owner.Lifetime.Destroying || !ReferenceEquals(engine.Robot, owner);
                if (token.IsCancellationRequested) detail = "Request cancelled before admission";
                else if (retired) detail = "Robot owner retired before admission";
                return CommandResult.Action(false, null, detail, retired);
            }

            var ended = await runner.AiNativeEnded!.Task.ConfigureAwait(false);
            // Queue destruction completes AiNativeEnded before D6 broadcasts the
            // public root completion. Wait for that actual event, when D2 sends it.
            if (!ended.OwnerRetired && owner.HasExternalInterface && ended.Result != EngineActionResult.Interrupted)
                await delivered.Task.ConfigureAwait(false);
            return CommandResult.Action(true, ended.Result, runner.GetTrackLockDetail(ended.Result) ?? detail, ended.OwnerRetired) with
            {
                OwnedCompletionTag = runner.Tag,
            };
        }
        finally
        {
            engine.ActionCompleted -= OnCompletion;
            Disposing -= OnDisposed;
        }
    }

    private async Task<CommandResult> SayText(string text, CancellationToken token)
    {
        bool reactionDisabled = Robot.Engine.Robot?.ActionList.ExternalActionsDisabled == true;
        var result = await Robot.SayEnglishSentenceAsync(text, token).ConfigureAwait(false);
        string detail = result.Detail == "Speech action was not admitted"
            ? reactionDisabled ? "External actions are disabled by a reaction" : "Action admission failed"
            : result.Detail;
        return CommandResult.Action(result.Admitted, result.EngineResult, detail, result.OwnerRetired);
    }
}
