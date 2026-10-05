using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

// fidelity: M7-020
/// <summary>
/// The engine's <c>ActionWatcher</c> (20261004-actionlist-extraction.md rows W1..W17, with D3/D5/D6): the node
/// tree over action tags, the destruction-event deque and the callback drain.
///
/// Native offsets are identification, not proposed C# layout: node map, root +0xC, current +0x10, previous +0x14,
/// root-stack map +0x18, callback map +0x24, next handle +0x30, record deque +0x34. A node carries its tag, its
/// embedded <see cref="RobotCompletedAction"/>, a name +0x44, a parent +0x54, a children vector +0x58 and the
/// ending mark +0x50.
/// </summary>
internal sealed class ActionWatcher
{
    /// <summary>W6: a watcher node. The name at +0x44 has no source on <see cref="IActionRunner"/>; it stays null (MISSING, see <see cref="ActionEnding"/>).</summary>
    private sealed class Node
    {
        public required uint Tag { get; init; }
        public RobotCompletedAction Record { get; set; }
        public string? Name { get; set; }                  // +0x44: no source in this stack
        public Node? Parent { get; set; }                  // +0x54
        public List<Node> Children { get; } = new();       // +0x58
        public bool EndingMark { get; set; }               // +0x50
    }

    private readonly Dictionary<uint, Node> _nodes = new();                             // node map
    private readonly Dictionary<uint, Node> _roots = new();                             // root-stack map +0x18
    private readonly List<uint> _rootStack = new();                                     // nesting stack
    private readonly SortedDictionary<int, Action<RobotCompletedAction>> _callbacks = new();   // +0x24, keyed int
    private readonly Queue<RobotCompletedAction> _deque = new();                        // +0x34
    private int _nextHandle = 1;                                                        // +0x30
    private uint _rootTag, _currentTag, _previousTag;                                   // +0xC / +0x10 / +0x14

    /// <summary>D2: the game gate. False in production (the stack has no engine-to-game sink); settable for tests.</summary>
    public bool HasExternalInterface { get; set; }

    /// <summary>
    /// D6: the local stand-in for <c>Robot::Broadcast(MessageEngineToGame(RobotCompletedAction))</c> (0x0053FAAA..0x0053FAB2).
    /// Raised only when <see cref="HasExternalInterface"/> is true; nothing delivers it to an app.
    /// </summary>
    public event Action<RobotCompletedAction>? RobotCompletedActionBroadcast;

    /// <summary>Test/inspection: how many times <see cref="Update"/> ran.</summary>
    public int UpdateCount { get; private set; }

    /// <summary>Test/inspection: the runner whose tag is the selected root (W7). Set by ParentActionUpdating, cleared by Update.</summary>
    internal IActionRunner? Parent { get; private set; }

    /// <summary>Test/inspection: records waiting to drain.</summary>
    public int PendingEventCount => _deque.Count;

    // ------------------------------------------------------------------ W7/W8/W9: nesting

    /// <summary>
    /// W7 ParentActionUpdating 0x00541974..0x005419D6: root = the selected queue action tag, current/previous 0,
    /// ensure the root node. Compound nesting stays under the root.
    /// </summary>
    public void ParentActionUpdating(IActionRunner runner)
    {
        Parent = runner;
        _rootTag = runner.Tag;
        _currentTag = 0;
        _previousTag = 0;
        _roots[runner.Tag] = EnsureNode(runner);
    }

    /// <summary>
    /// W8 ActionStartUpdating 0x00541A40..0x00541B46: previous = old current, current = tag; a new node links to
    /// the previous node when it is found and appends to the parent's child vector; the tag is pushed on the root
    /// stack. Runs before the runner's lock/terminal checks.
    /// </summary>
    public void ActionStartUpdating(IActionRunner runner)
    {
        _previousTag = _currentTag;
        _currentTag = runner.Tag;
        var node = EnsureNode(runner);
        if (_previousTag != 0 && _nodes.TryGetValue(_previousTag, out var prev) && !ReferenceEquals(prev, node))
        {
            node.Parent = prev;
            if (!prev.Children.Contains(node)) prev.Children.Add(node);
        }
        _rootStack.Add(runner.Tag);
    }

    /// <summary>
    /// W9 ActionEndUpdating 0x00541B48..0x00541BB2: pop the root stack; current = the new back, previous = the one
    /// before it, absent =&gt; 0.
    /// </summary>
    public void ActionEndUpdating()
    {
        if (_rootStack.Count > 0) _rootStack.RemoveAt(_rootStack.Count - 1);
        _currentTag = _rootStack.Count > 0 ? _rootStack[^1] : 0;
        _previousTag = _rootStack.Count > 1 ? _rootStack[^2] : 0;
    }

    // ------------------------------------------------------------------ W3/W4/W5/W10: destruction

    /// <summary>
    /// W3/W4/W5/W10 ActionEnding 0x00541238 / 0x00541BD8..0x00541C04: the destruction event. It is independent of
    /// the game gate (W10: no game-interface filter) and can carry RUNNING, CANCELLED, NOT_STARTED or INTERRUPTED.
    ///
    /// W3 order: read the scalar tag/type/result, the virtual completion union, THEN <see cref="GetSubActionResults"/>,
    /// then push the record on the deque. W4: ensure the ending node; a newly created node attaches to the current
    /// root when it is found/nonzero and is marked +0x50. W5: an ending tag that is a root deletes the whole action
    /// tree and erases its root-stack entry; otherwise the tag-map entry is erased and a child with a parent is
    /// retained in the parent's child vector (a parentless node is disposed).
    /// </summary>
    public void ActionEnding(IActionRunner runner)
    {
        uint tag = runner.Tag;
        int type = runner.Type;
        uint result = runner.State;

        var node = EnsureNode(runner);
        if (node.Parent is null && node.Children.Count == 0 && _rootTag != 0 && _rootTag != tag &&
            _roots.TryGetValue(_rootTag, out var root) && !ReferenceEquals(root, node))
        {
            node.Parent = root;                            // W4: a newly created node attaches to the root ([this+0xC]), not the current ([this+0x10])
            if (!root.Children.Contains(node)) root.Children.Add(node);
        }

        uint union = runner.GetCompletionUnion();          // W3: virtual union BEFORE GetSubActionResults
        var subs = new List<uint>();
        GetSubActionResults(tag, subs);                    // W3: then the descendants
        node.Record = new RobotCompletedAction(tag, type, result, subs, union);
        node.EndingMark = true;                            // +0x50
        _deque.Enqueue(node.Record);                       // W3: deque push

        if (_roots.ContainsKey(tag))                       // W5: a root ends the whole tree
        {
            DeleteActionTree(node);
            _roots.Remove(tag);
        }
        else
        {
            _nodes.Remove(tag);                            // W5: erase the tag-map entry; a child with a parent stays in the parent's vector
        }
    }

    /// <summary>
    /// W11 ActionWatcher::Update 0x0053F5E6 / 0x0054187E..0x005418DE: while the deque is nonempty, every callback-map
    /// entry in signed ascending handle order receives the front record, then the front is popped. A record appended
    /// during the drain is delivered in the same Update.
    /// </summary>
    public void Update()
    {
        UpdateCount++;
        while (_deque.Count > 0)
        {
            var record = _deque.Peek();
            foreach (var callback in _callbacks.Values) callback(record);
            _deque.Dequeue();
        }
        Parent = null;
    }

    // ------------------------------------------------------------------ W1/W2: descendant results

    /// <summary>
    /// W1 GetSubActionResults 0x00541EAC..0x00541F68: a tag the node map does not hold leaves
    /// <paramref name="output"/> untouched. A present tag clears the output and inserts the unique descendant full
    /// results in set order; the root's own result is excluded. Not a completion-order list and not child ids.
    /// </summary>
    public void GetSubActionResults(uint tag, ICollection<uint> output)
    {
        if (!_nodes.TryGetValue(tag, out var node)) return;
        var set = new SortedSet<uint>();
        Collect(node, set);
        output.Clear();
        foreach (var value in set) output.Add(value);
    }

    /// <summary>W2 recursive collector 0x00543902..0x00543982: each child vector entry's embedded result, then recurse. No filter.</summary>
    private static void Collect(Node node, SortedSet<uint> set)
    {
        foreach (var child in node.Children)
        {
            set.Add(child.Record.Result);
            Collect(child, set);
        }
    }

    // ------------------------------------------------------------------ W12: callbacks

    /// <summary>
    /// W12 0x0054179C..0x005417D2 / 0x0054182C..0x0054187C: move a callback into the watcher and return an integer
    /// handle. Handles increment from 1; the map is keyed int. No HasExternalInterface gate.
    /// </summary>
    public int RegisterCallback(Action<RobotCompletedAction> callback)
    {
        int handle = _nextHandle;
        _nextHandle = unchecked(_nextHandle + 1);
        _callbacks[handle] = callback;
        return handle;
    }

    /// <summary>W12: erase the matching handle and return whether it was found.</summary>
    public bool UnregisterCallback(int handle) => _callbacks.Remove(handle);

    // ------------------------------------------------------------------ D3/D6: the game snapshot

    /// <summary>
    /// D3 0x0053FA4C..0x0053FA66 / 0x00540AA8..0x00540B64: the game snapshot, built BEFORE the destructor. D2: the
    /// game gate is <see cref="HasExternalInterface"/>, and the snapshot is suppressed when the stored state is
    /// 0x03000009 (INTERRUPTED). No success-only/external-tag-only/started-only filter. Order: GetSubActionResults
    /// first, then the virtual union. Returns null when the gate suppresses the snapshot.
    /// </summary>
    public RobotCompletedAction? BuildCompletedAction(IActionRunner runner)
    {
        if (!HasExternalInterface) return null;                              // D2: game gate
        if (runner.State == EngineActionResult.Interrupted) return null;     // D2: suppress iff 0x03000009
        var subs = new List<uint>();
        GetSubActionResults(runner.Tag, subs);                               // D3: subresults first
        uint union = runner.GetCompletionUnion();                            // D3: then the virtual union
        return new RobotCompletedAction(runner.Tag, runner.Type, runner.State, subs, union);
    }

    /// <summary>
    /// D6 0x0053FAAA..0x0053FAB2: the engine's <c>MessageEngineToGame(RobotCompletedAction&amp;&amp;)</c> then the
    /// external virtual +0x1C Broadcast. No send-result check or retry. This stack has no engine-to-game sink, so
    /// the record is raised on <see cref="RobotCompletedActionBroadcast"/> and the gap is reported once.
    /// </summary>
    public void SendGameMessage(RobotCompletedAction record)
    {
        if (!HasExternalInterface) return;                  // D2: the external broadcast only runs with the interface
        SteppedBehavior.ReportMissing("Robot::Broadcast(MessageEngineToGame(RobotCompletedAction)) 0x0053FAAA..0x0053FAB2: this stack has no engine-to-game message sink (nothing consumes CozmoEngine.PostGameMessage and no app channel exists); the RobotCompletedAction is built and raised on ActionWatcher.RobotCompletedActionBroadcast only");
        RobotCompletedActionBroadcast?.Invoke(record);
    }

    // ------------------------------------------------------------------ helpers

    private Node EnsureNode(IActionRunner runner)
    {
        if (_nodes.TryGetValue(runner.Tag, out var existing)) return existing;
        var node = new Node
        {
            Tag = runner.Tag,
            Record = new RobotCompletedAction(runner.Tag, runner.Type, runner.State, Array.Empty<uint>(), 0),
        };
        _nodes[runner.Tag] = node;
        return node;
    }

    /// <summary>W5: descendants before the root; every subtree node leaves the node map.</summary>
    private void DeleteActionTree(Node node)
    {
        foreach (var child in node.Children.ToArray()) DeleteActionTree(child);
        node.Children.Clear();
        _nodes.Remove(node.Tag);
    }
}