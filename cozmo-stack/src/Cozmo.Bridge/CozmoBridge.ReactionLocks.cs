using System.Diagnostics;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    private static bool IsQueuedAction(string name) => name is
        "head" or "lift" or "drive-straight" or "turn-in-place" or "turn-to-face" or "turn-to-cube" or
        "track-face" or "animation" or "say-text" or "pickup" or "put-down" or "roll" or "stack" or
        "flip" or "wheelie" or "go-to-pose" or "mount-charger";

    private static BridgeCommand AddAdmissionWaitParameter(BridgeCommand command) => IsQueuedAction(command.Name)
        ? command with
        {
            Parameters = [..command.Parameters, Argument.Integer("waitAdmittedMs",
                "Wait up to this many milliseconds for a reaction to release a refused action, then retry admission.", 0, 30000, 0)],
        }
        : command;

    private Task<CommandResult> ChangeReactionLock(string id, JsonElement? selection, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // ExecuteAsync owns the engine while dispatching. These are the same native manager recipients
        // as G2E 0x9F (0x005A4D2A), 0xA0 (0x005A4EA6), and 0xA1 (0x005A4F20).
        var engine = Robot.Engine;
        var owner = engine.Robot;
        if (engine.ConnectionState != 2 || !engine.IsReadyDelivered || engine.RobotValidated != true ||
            owner is null || owner.Lifetime.Destroying || !owner.FirstFullStateHandled ||
            owner.BehaviorManagerOwner is not { } manager)
            return Task.FromResult(CommandResult.NotReady());

        if (selection is null)
            manager.RemoveDisableReactionsLock(id);
        else if (selection.Value.ValueKind == JsonValueKind.String)
            manager.DisableAllReactionsWithLock(id);
        else
        {
            var flags = new bool[BehaviorManager.TriggerCount];
            foreach (var trigger in selection.Value.EnumerateArray())
                flags[(int)Enum.Parse<ReactionTrigger>(trigger.GetString()!)] = true;
            manager.DisableReactionsWithLock(id, flags);
        }

        if (owner.Lifetime.Destroying || !ReferenceEquals(engine.Robot, owner))
            return Task.FromResult(CommandResult.Action(false, null, "Robot owner retired during the lock request", true));
        return Task.FromResult(CommandResult.Confirmed(new { id, reactionLocks = manager.GetReactionLocks() }));
    }

    private sealed record AdmissionWait(double WaitedMs, string[] Reactions, bool TimedOut);

    private static bool IsReactionRefusal(CommandResult result) =>
        result is { Admitted: false, EngineResult: null, OwnerRetired: not true } &&
        result.Detail == "External actions are disabled by a reaction";

    private async Task<(CommandResult Result, AdmissionWait? Wait)> ExecuteWithAdmissionWait(
        BridgeCommand command, BridgeArguments args, ActionWhyCapture capture, CancellationToken token)
    {
        int budget = IsQueuedAction(command.Name) ? args.Int("waitAdmittedMs") : 0;
        var reactions = new List<string>();
        var clock = new Stopwatch();
        EngineRobot? waitingOwner = null;
        CommandResult? lastRefusal = null;
        void RecordReaction(ReactionTrigger trigger)
        {
            string reaction = trigger.ToString();
            if (!reactions.Contains(reaction, StringComparer.Ordinal)) reactions.Add(reaction);
        }
        while (true)
        {
            Task<CommandResult> pending;
            EngineRobot? owner;
            ReactionTrigger? blockingReaction;
            using (Robot.Engine.EnterSynchronousOwner())
            {
                owner = Robot.Engine.Robot;
                if (activeIntent.Value?.Closed.Task.IsCompleted == true)
                    return (new CommandResult("unknown", null, null, null, "intent closed before admission"), null);
                if (waitingOwner is not null && (!ReferenceEquals(owner, waitingOwner) || waitingOwner.Lifetime.Destroying))
                    return (CommandResult.Action(false, null, "Robot owner retired while waiting for admission", true),
                        new(clock.Elapsed.TotalMilliseconds, reactions.ToArray(), false));
                if (waitingOwner is not null && clock.Elapsed.TotalMilliseconds >= budget)
                    return (lastRefusal!,
                        new(clock.Elapsed.TotalMilliseconds, reactions.ToArray(), true));

                blockingReaction = budget > 0 && owner?.ActionList.ExternalActionsDisabled == true
                    ? owner.BehaviorManagerOwner?.CurrentReactionTrigger : null;
                capture.BeforeAdmission();
                pending = command.Execute(this, args, token);
                capture.AfterAdmission();
                if (budget > 0 && blockingReaction is null && owner?.ActionList.ExternalActionsDisabled == true)
                    blockingReaction = owner.BehaviorManagerOwner?.CurrentReactionTrigger;
            }

            var result = await pending.ConfigureAwait(false);
            if (budget == 0 || !IsReactionRefusal(result) || blockingReaction is not { } trigger || owner is null)
                return (result, budget == 0 ? null : new(clock.Elapsed.TotalMilliseconds, reactions.ToArray(), false));

            waitingOwner = owner;
            lastRefusal = result;
            RecordReaction(trigger);
            clock.Start();
            try
            {
                while (true)
                {
                    Task signal;
                    lock (eventGate)
                    {
                        ThrowIfDisposed();
                        signal = eventAvailable.Task;
                    }
                    using (Robot.Engine.EnterSynchronousOwner())
                    {
                        if (owner.Lifetime.Destroying || !ReferenceEquals(Robot.Engine.Robot, owner))
                            return (CommandResult.Action(false, null, "Robot owner retired while waiting for admission", true),
                                new(clock.Elapsed.TotalMilliseconds, reactions.ToArray(), false));
                        var current = owner.BehaviorManagerOwner?.CurrentReactionTrigger;
                        if (current is null && !owner.ActionList.ExternalActionsDisabled) break;
                        if (current is { } active) RecordReaction(active);
                    }
                    var remaining = TimeSpan.FromMilliseconds(budget) - clock.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        return (result, new(clock.Elapsed.TotalMilliseconds, reactions.ToArray(), true));
                    try { await signal.WaitAsync(remaining, token).ConfigureAwait(false); }
                    catch (TimeoutException)
                    {
                        return (result, new(clock.Elapsed.TotalMilliseconds, reactions.ToArray(), true));
                    }
                }
            }
            finally { clock.Stop(); }
            token.ThrowIfCancellationRequested();
        }
    }
}
