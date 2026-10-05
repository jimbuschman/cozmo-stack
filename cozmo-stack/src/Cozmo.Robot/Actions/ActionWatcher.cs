namespace Cozmo.Robot;

/// <summary>
/// The engine's <c>ActionWatcher</c> (20261004-actionlist-extraction.md rows W1..W13). The full node tree, the
/// destruction-event deque and the callback drain are batch 4; batch 1 needs only the two calls the list and queue
/// tick make (T5/T6), so this records them. No fidelity claim is placed on the batch-4 behaviour.
/// </summary>
internal sealed class ActionWatcher
{
    /// <summary>W7: ParentActionUpdating sets the root to the selected queue action. Batch 1 records the call.</summary>
    public IActionRunner? Parent { get; private set; }

    /// <summary>T5/W11: ActionList::Update calls the watcher after every queue, including an empty map.</summary>
    public int UpdateCount { get; private set; }

    /// <summary>W7 ParentActionUpdating 0x00541974..0x005419D6, called by ActionQueue::Update before the runner.</summary>
    public void ParentActionUpdating(IActionRunner runner) => Parent = runner;

    /// <summary>W11 ActionWatcher::Update 0x0053F5E6, called by ActionList::Update after the queues.</summary>
    public void Update()
    {
        UpdateCount++;
        Parent = null;
    }
}
