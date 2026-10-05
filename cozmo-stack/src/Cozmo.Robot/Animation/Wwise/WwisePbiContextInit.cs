// fidelity: M6-025
namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The references the PBI context init <c>0x9BC90C</c> takes on a node (C40.3): <c>[ctx+0xD4]-&gt;vt+8</c> (<c>0x9BCA30..0x9BCA38</c>), the NodeBase AddRef <c>0x9F1CBC</c> (<c>[node+0xC]++</c> under the registry lock; the lock is bit 2 of <c>[node+0x46]</c>'s choice and not observable here).
/// The node's own count before the first PBI is the node loader's (unread), so this table holds only the references the PBIs hold: it is <c>[node+0xC]</c> minus that start value. The balancing release is the node <c>vt+0xC = 0x9F500C</c> (<c>[node+0xC]--</c>, the node freed at zero) that
/// <c>0x9BDC8C</c> makes at the end of Term (<c>0x9BDD20..0x9BDD2C</c>); that release, with the game object's decrement at <c>0x9BDCF8..0x9BDD14</c>, is read but not in an adopted row, so no C# Term makes it (MISSING, reported).
/// </summary>
public sealed class WwiseNodeRefTable
{
    private readonly Dictionary<uint, int> _extra = new();

    /// <summary><c>0x9F1CBC(node)</c>: <c>[node+0xC]++</c>; returns the references held by PBIs on that node after it.</summary>
    public int AddRef9F1CBC(WwiseNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _extra[node.Id] = _extra.GetValueOrDefault(node.Id) + 1;
        return _extra[node.Id];
    }

    /// <summary>The references PBIs hold on a node (the AddRefs <c>0x9BC90C</c> made; nothing releases them yet).</summary>
    public int ReferencesOf(uint nodeId) => _extra.GetValueOrDefault(nodeId);
}

/// <summary>The PBI context init <c>0x9BC90C</c>'s chain test and the reference stores (C40.3).</summary>
public static class WwisePbiContextInit
{
    /// <summary>
    /// <c>0x9BC9FC..0x9BCA1C</c>: the walk from the PBI's node. A node with <c>[x+0x40] &amp; 0xE0000</c> ends it with <c>true</c> (<c>[ctx+0xDD]</c> bit 3 set, <c>0x9BCA08..0x9BCA18</c>); otherwise it moves to <c>[x+0x38]</c> (the output bus) when that is non-null, else to
    /// <c>[x+0x34]</c> (the parent), and a node with neither ends it with <c>false</c> (bit 3 cleared, <c>0x9BCA54..0x9BCA60</c>). The only writer of <c>0xE0000</c> is the Bus loader (<c>0x9C6540/44</c>: byte C bit 0), so a non-bus node's bits are 0 (C40.3 T-N1: the writer scan of every ARM orr/bic/bfi on a [x+0x40] word found no other writer) unless the caller supplied
    /// its <see cref="WwiseRoutingNode.Node40"/>; a bus reads <see cref="WwiseRoutingNode.Word40"/>. All 15 shipped buses have byte C = 2, so the result is false for every shipped chain.
    /// </summary>
    public static bool ChainFlag9BC9FC(WwiseRoutingNode start)
    {
        ArgumentNullException.ThrowIfNull(start);
        var x = start;
        for (int guard = 0; guard < 4096; guard++)
        {
            uint word40 = x.IsBus ? x.Word40 : x.Node40 ?? 0u;                        // 0x9BC9FC ldr r2,[r3,#0x40]
            if ((word40 & 0xE0000) != 0) return true;                                  // 0x9BCA00 tst r2,#0xe0000; beq 0x9BC9EC; else 0x9BCA08
            var next = x.OutputBus ?? x.Parent;                                        // 0x9BC9EC ldr r2,[r3,#0x38]; 0x9BCA54 ldr r3,[r3,#0x34]
            if (next is null) return false;                                            // 0x9BCA58 cmp r3,#0; bne 0x9BC9FC; b 0x9BCA0C (r3 = 0)
            x = next;
        }
        throw new InvalidOperationException("M6-025 C40.3: the node chain of 0x9BC9FC does not end (a cycle in [node+0x38] / [node+0x34])");
    }

    /// <summary>
    /// <c>0x9BCA20..0x9BCA2C</c>: <c>[GO+0x7C]</c> low 30 bits <c>+= 1</c> (<c>add r1,r3,#1; bfi r3,r1,#0,#0x1e</c>: the top two bits kept, the carry out of bit 29 dropped).
    /// </summary>
    public static void AddGameObjectReference(WwiseGameObjectRef go)
    {
        ArgumentNullException.ThrowIfNull(go);
        go.Word7C = (go.Word7C & 0xC0000000u) | (unchecked(go.Word7C + 1u) & 0x3FFFFFFFu);
    }
}
