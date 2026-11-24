namespace Cozmo.Robot;

// fidelity: M8-015
// The engine visits keys in ascending order on Update (0x0053F580), but destroys
// left, right, then node on Clear (0x0053FB5C..0x0053FB82). The insert and erase
// rotations determine the order of the synchronous completion callbacks on Clear.
internal sealed class NativeActionQueueMap
{
    private sealed class Node(int key, ActionQueue queue)
    {
        internal int Key { get; } = key;
        internal ActionQueue Queue { get; } = queue;
        internal Node? Parent;
        internal Node? Left;
        internal Node? Right;
        internal bool Black;

        internal Node? Child(bool right) => right ? Right : Left;
        internal void SetChild(bool right, Node? child)
        {
            if (right) Right = child;
            else Left = child;
            if (child is not null) child.Parent = this;
        }
    }

    private Node? _root;
    internal int Count { get; private set; }
    internal IEnumerable<KeyValuePair<int, ActionQueue>> Entries => InOrder(_root);
    internal IEnumerable<ActionQueue> DestructionOrder => PostOrder(_root);

    internal bool ContainsKey(int key) => Find(key) is not null;
    internal bool TryGetValue(int key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ActionQueue? value)
    {
        value = Find(key)?.Queue;
        return value is not null;
    }

    private Node? Find(int key)
    {
        var current = _root;
        while (current is not null && current.Key != key)
            current = current.Child(key > current.Key);
        return current;
    }

    private static IEnumerable<KeyValuePair<int, ActionQueue>> InOrder(Node? node)
    {
        if (node is null) yield break;
        foreach (var entry in InOrder(node.Left)) yield return entry;
        yield return new(node.Key, node.Queue);
        foreach (var entry in InOrder(node.Right)) yield return entry;
    }

    private static IEnumerable<ActionQueue> PostOrder(Node? node)
    {
        if (node is null) yield break;
        foreach (var queue in PostOrder(node.Left)) yield return queue;
        foreach (var queue in PostOrder(node.Right)) yield return queue;
        yield return node.Queue;
    }

    // 0x0053FC4C links a leaf, then calls __tree_balance_after_insert at 0x004E0836.
    internal void Add(int key, ActionQueue queue)
    {
        Node? parent = null;
        bool right = false;
        for (var current = _root; current is not null; current = current.Child(right))
        {
            if (key == current.Key) throw new ArgumentException("The action queue key is already present.", nameof(key));
            parent = current;
            right = key > current.Key;
        }
        var node = new Node(key, queue);
        if (parent is null) _root = node;
        else parent.SetChild(right, node);
        Count++;

        while (node.Parent is { Black: false } redParent && redParent.Parent is { } grandparent)
        {
            bool parentIsRight = ReferenceEquals(redParent, grandparent.Right);
            if (grandparent.Child(!parentIsRight) is { Black: false } uncle)
            {
                redParent.Black = uncle.Black = true;
                grandparent.Black = false;
                node = grandparent;
                continue;
            }
            if (ReferenceEquals(node, redParent.Child(!parentIsRight)))
                redParent = Rotate(redParent, !parentIsRight);
            redParent.Black = true;
            grandparent.Black = false;
            Rotate(grandparent, parentIsRight);
            break;
        }
        _root!.Black = true;
    }

    // 0x0053FCA8 calls __tree_remove. 0x004F6310..0x004F6392 removes either
    // the requested node or its in-order successor, then transplants the latter.
    internal void Remove(int key)
    {
        if (Find(key) is not { } node) return;
        var removed = node;
        if (node.Left is not null && node.Right is { } successor)
        {
            while (successor.Left is { } left) successor = left;
            removed = successor;
        }
        var child = removed.Left ?? removed.Right;
        var parent = removed.Parent;
        var sibling = parent?.Child(ReferenceEquals(parent.Left, removed));
        bool wasBlack = removed.Black;
        Replace(removed, child);
        if (!ReferenceEquals(removed, node))
        {
            Replace(node, removed);
            removed.SetChild(false, node.Left);
            removed.SetChild(true, node.Right);
            removed.Black = node.Black;
        }
        Count--;
        if (!wasBlack || _root is null) return;
        if (child is not null) child.Black = true;
        else if (sibling is not null) BalanceBlackRemoval(sibling);
    }

    // 0x004F63AE..0x004F656C fixes a missing black leaf using the surviving sibling.
    private void BalanceBlackRemoval(Node sibling)
    {
        while (sibling.Parent is { } parent)
        {
            bool right = ReferenceEquals(sibling, parent.Right);
            if (!sibling.Black)
            {
                sibling.Black = true;
                parent.Black = false;
                Rotate(parent, right);
                sibling = parent.Child(right)!;
            }
            if (sibling.Left is not { Black: false } && sibling.Right is not { Black: false })
            {
                sibling.Black = false;
                if (!parent.Black || parent.Parent is not { } grandparent)
                {
                    parent.Black = true;
                    return;
                }
                sibling = grandparent.Child(ReferenceEquals(parent, grandparent.Left))!;
                continue;
            }
            if (sibling.Child(right) is not { Black: false })
            {
                sibling.Child(!right)!.Black = true;
                sibling.Black = false;
                sibling = Rotate(sibling, !right);
            }
            sibling.Black = parent.Black;
            parent.Black = true;
            sibling.Child(right)!.Black = true;
            Rotate(parent, right);
            return;
        }
    }

    private Node Rotate(Node node, bool promoteRight)
    {
        var pivot = node.Child(promoteRight)!;
        node.SetChild(promoteRight, pivot.Child(!promoteRight));
        Replace(node, pivot);
        pivot.SetChild(!promoteRight, node);
        return pivot;
    }

    private void Replace(Node node, Node? replacement)
    {
        if (node.Parent is { } parent) parent.SetChild(ReferenceEquals(parent.Right, node), replacement);
        else
        {
            _root = replacement;
            if (replacement is not null) replacement.Parent = null;
        }
    }

    internal void Clear()
    {
        _root = null;
        Count = 0;
    }
}
