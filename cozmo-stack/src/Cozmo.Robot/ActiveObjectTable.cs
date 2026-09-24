namespace Cozmo.Robot;

/// <summary>
/// The engine's advertisement table at Robot+0x47C: an <c>std::__ndk1::unordered_map&lt;u32, ActiveObjectInfo&gt;</c>
/// (libc++, <c>std::hash&lt;unsigned&gt;</c> - the key is its own hash). Its iteration order is what decides a tie
/// between two cubes of one type at the same RSSI byte, so it is reproduced as the instantiations in
/// libcozmoEngine.so implement it rather than as an ordered list:
///
/// <list type="bullet">
/// <item>a bucket is <c>h &amp; (n - 1)</c> when the bucket count is a power of two, <c>h % n</c> otherwise
/// (<c>operator[]</c> 0x00537ABC..0x00537AE8);</item>
/// <item>inserting a new key first grows the table when it is empty or when <c>n * max_load_factor &lt; size + 1</c>
/// (max_load_factor 1.0), to <c>max((2n) | (n &lt; 3 || n is not a power of two), ceil((size + 1) / mlf))</c>
/// (0x00537B2C..0x00537B90); <c>rehash</c> 0x00537C80 turns 1 into 2 and anything not a power of two into the next
/// prime, so the counts run 2, 5, 11, 23, ...;</item>
/// <item>the node goes in after its bucket's head when the bucket is in use, and otherwise at the front of the
/// whole list, the bucket then pointing at the list head and the bucket of the node it now precedes pointing at it
/// (0x00537BAE..0x00537BF8);</item>
/// <item><c>__rehash</c> 0x00537D10 rebuilds the buckets by walking the list, splicing each node whose bucket is
/// already in use to just after that bucket's head;</item>
/// <item>erasing unlinks the node and repairs the two bucket pointers it may have carried
/// (<c>remove</c> 0x00518FDA).</item>
/// </list>
/// </summary>
internal sealed class ActiveObjectTable<T>
{
    private sealed class Node
    {
        public uint Key;
        public T Value = default!;
        public Node? Next;
    }

    private readonly Node _head = new();       // __p1_: the before-begin node
    private Node?[] _buckets = Array.Empty<Node?>();
    private int _size;
    private const float MaxLoadFactor = 1.0f;

    public int Count => _size;

    private static bool IsPowerOfTwo(uint n) => n > 2 && (n & (n - 1)) == 0;
    private uint BucketCount => (uint)_buckets.Length;
    private static uint Constrain(uint h, uint n) => (n & (n - 1)) == 0 ? h & (n - 1) : h % n;

    public bool TryGet(uint key, out T value)
    {
        var n = Find(key);
        value = n is null ? default! : n.Value;
        return n is not null;
    }

    private Node? Find(uint key)
    {
        uint bc = BucketCount;
        if (bc == 0) return null;
        uint idx = Constrain(key, bc);
        var p = _buckets[idx];
        if (p is null) return null;
        for (var n = p.Next; n is not null && Constrain(n.Key, bc) == idx; n = n.Next)
            if (n.Key == key) return n;
        return null;
    }

    /// <summary><c>operator[]</c>: the existing entry is updated in place; a new key is inserted.</summary>
    public void Set(uint key, T value)
    {
        var existing = Find(key);
        if (existing is not null) { existing.Value = value; return; }

        var node = new Node { Key = key, Value = value };
        uint bc = BucketCount;
        if (bc == 0 || bc * MaxLoadFactor < _size + 1)
        {
            uint grow = (2 * bc) | (bc < 3 || (bc & (bc - 1)) != 0 ? 1u : 0u);
            uint need = (uint)MathF.Ceiling((_size + 1) / MaxLoadFactor);
            Rehash(Math.Max(grow, need));
            bc = BucketCount;
        }
        uint idx = Constrain(key, bc);
        var pn = _buckets[idx];
        if (pn is null)
        {
            node.Next = _head.Next;
            _head.Next = node;
            _buckets[idx] = _head;
            if (node.Next is not null) _buckets[Constrain(node.Next.Key, bc)] = node;
        }
        else
        {
            node.Next = pn.Next;
            pn.Next = node;
        }
        _size++;
    }

    /// <summary><c>rehash</c> 0x00537C80, called here only to grow.</summary>
    private void Rehash(uint n)
    {
        if (n == 1) n = 2;
        else if ((n & (n - 1)) != 0) n = NextPrime(n);
        uint bc = BucketCount;
        if (n > bc) RehashImpl(n);
        else if (n < bc)
        {
            uint m = (uint)MathF.Ceiling(_size / MaxLoadFactor);
            m = IsPowerOfTwo(bc) ? (m < 2 ? m : 1u << (32 - System.Numerics.BitOperations.LeadingZeroCount(m - 1))) : NextPrime(m);
            n = Math.Max(n, m);
            if (n < bc) RehashImpl(n);
        }
    }

    /// <summary><c>__rehash</c> 0x00537D10.</summary>
    private void RehashImpl(uint n)
    {
        _buckets = new Node?[n];
        var pp = _head;
        var cp = pp.Next;
        if (cp is null) return;
        uint phash = Constrain(cp.Key, n);
        _buckets[phash] = pp;
        pp = cp;
        for (cp = cp.Next; cp is not null; cp = pp.Next)
        {
            uint chash = Constrain(cp.Key, n);
            if (chash == phash) { pp = cp; continue; }
            if (_buckets[chash] is null)
            {
                _buckets[chash] = pp;
                pp = cp;
                phash = chash;
                continue;
            }
            var np = cp;
            while (np.Next is not null && np.Next.Key == cp.Key) np = np.Next;
            pp.Next = np.Next;
            np.Next = _buckets[chash]!.Next;
            _buckets[chash]!.Next = cp;
        }
    }

    public bool Remove(uint key)
    {
        var n = Find(key);
        if (n is null) return false;
        Unlink(n);
        return true;
    }

    /// <summary>Walks the table in its iteration order and erases what the predicate selects.</summary>
    public void RemoveWhere(Func<T, bool> predicate)
    {
        for (var n = _head.Next; n is not null;)
        {
            var next = n.Next;
            if (predicate(n.Value)) Unlink(n);
            n = next;
        }
    }

    // fidelity: M1-025, M1-015
    /// <summary>
    /// Back to a newly constructed table, bucket array included, for a removed robot (CB33, CC26: the Robot and this
    /// map are deleted; CC27: the next one is built afresh). Erasing every entry would keep the grown bucket count,
    /// and with it a different iteration order from a fresh map's.
    /// </summary>
    public void Clear()
    {
        _head.Next = null;
        _buckets = Array.Empty<Node?>();
        _size = 0;
    }

    /// <summary><c>remove</c> 0x00518FDA.</summary>
    private void Unlink(Node cn)
    {
        uint bc = BucketCount;
        uint chash = Constrain(cn.Key, bc);
        var pn = _buckets[chash]!;
        while (pn.Next != cn) pn = pn.Next!;
        if (pn == _head || Constrain(pn.Key, bc) != chash)
        {
            if (cn.Next is null || Constrain(cn.Next.Key, bc) != chash) _buckets[chash] = null;
        }
        if (cn.Next is not null)
        {
            uint nhash = Constrain(cn.Next.Key, bc);
            if (nhash != chash) _buckets[nhash] = pn;
        }
        pn.Next = cn.Next;
        cn.Next = null;
        _size--;
    }

    /// <summary>The values in the table's iteration order.</summary>
    public IEnumerable<T> Values
    {
        get { for (var n = _head.Next; n is not null; n = n.Next) yield return n.Value; }
    }

    private static uint NextPrime(uint n)
    {
        if (n <= 2) return 2;
        for (uint c = n; ; c++)
        {
            bool prime = c % 2 != 0;
            for (uint d = 3; prime && (ulong)d * d <= c; d += 2) if (c % d == 0) prime = false;
            if (prime) return c;
        }
    }
}
