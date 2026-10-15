namespace Cozmo.Robot;

// fidelity: M15-033, M15-043
// Checked identity hash, mlf=1, traversal and rehash: 0x0051B3D2..0x0051B530, 0x0051DD18..0x0051DEC0.
internal sealed class BackupNativeMap
{
    internal readonly List<(uint Tag, byte[] Data)> Nodes = new();
    internal uint Buckets { get; private set; }
    internal BackupNativeMap(uint buckets = 0) => Buckets = buckets;
    private uint Bucket(uint key) => (Buckets & (Buckets - 1)) == 0 ? key & (Buckets - 1) : key % Buckets;
    internal void Clear() => Nodes.Clear(); // clear retains bucket allocation.
    internal byte[]? Find(uint key) => Nodes.FirstOrDefault(n => n.Tag == key).Data;
    internal void Put(uint key, byte[] data)
    {
        int old = Nodes.FindIndex(n => n.Tag == key);
        if (old >= 0) { Nodes[old] = (key, data.ToArray()); return; }
        if (Buckets == 0 || (float)(Nodes.Count + 1) > (float)Buckets)
        {
            bool hashPower2 = Buckets > 2 && (Buckets & (Buckets - 1)) == 0;
            uint requested = Math.Max(unchecked(2 * Buckets + (hashPower2 ? 0u : 1u)), (uint)Nodes.Count + 1);
            Buckets = requested == 1 ? 2 : (requested & (requested - 1)) == 0 ? requested : NextPrime(requested);
            var previous = Nodes.ToArray(); Nodes.Clear();
            uint last = uint.MaxValue;
            foreach (var node in previous)
            {
                uint bucket = Bucket(node.Tag);
                int first = Nodes.FindIndex(n => Bucket(n.Tag) == bucket);
                if (bucket == last || first < 0) { Nodes.Add(node); last = bucket; }
                else Nodes.Insert(first, node); // __rehash splices a seen bucket before its first node.
            }
        }
        int position = Nodes.FindIndex(n => Bucket(n.Tag) == Bucket(key));
        Nodes.Insert(position < 0 ? 0 : position, (key, data.ToArray()));
    }
    private static readonly uint[] Small = {0,2,3,5,7,11,13,17,19,23,29,31,37,41,43,47,53,59,61,67,71,73,79,83,89,97,101,103,107,109,113,127,131,137,139,149,151,157,163,167,173,179,181,191,193,197,199,211};
    private static readonly uint[] Wheel = {1,11,13,17,19,23,29,31,37,41,43,47,53,59,61,67,71,73,79,83,89,97,101,103,107,109,113,121,127,131,137,139,143,149,151,157,163,167,169,173,179,181,187,191,193,197,199,209};
    // libc++ 0x0003B038..0x0003B656; unsigned division tests precede multiplication, no floating sqrt.
    internal static uint NextPrime(uint n)
    {
        if (n <= 211) return Small.First(p => p >= n);
        if (n >= 0xFFFFFFFC) throw new OverflowException("__next_prime overflow");
        uint q = n / 210;
        int i = Array.FindIndex(Wheel, r => r >= n % 210);
        if (i < 0) { ++q; i = 0; }
        while (true)
        {
            uint candidate = unchecked(210 * q + Wheel[i]);
            bool rejected = false;
            for (int j = 5; j <= 46; ++j)
            {
                uint divisor = Small[j], quotient = candidate / divisor;
                if (quotient < divisor) return candidate;
                if (quotient * divisor == candidate) { rejected = true; break; }
            }
            if (!rejected)
            {
                uint group = 1; int residue = 0;
                while (true)
                {
                    uint divisor = 210 * group + Wheel[residue], quotient = candidate / divisor;
                    if (quotient < divisor) return candidate;
                    if (quotient * divisor == candidate) break;
                    if (++residue == Wheel.Length) { residue = 0; ++group; }
                }
            }
            if (++i == Wheel.Length) { i = 0; ++q; }
        }
    }
}
