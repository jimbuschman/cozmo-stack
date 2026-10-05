using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>
/// The engine's <c>RobotCompletedAction</c> (20261004-actionlist-extraction.md rows D3, D8, D9): the 64-byte native
/// record the game snapshot and the watcher destruction event carry.
///
/// Native offsets are identification, not proposed C# layout: +0 tag uint32, +4 type int32, +8 result uint32,
/// +C vector (12 bytes), +18 union (40 bytes).
///
/// The wire body (<see cref="PackBody"/>) is the shipped CLAD order (unity/.../RobotCompletedAction.cs:174-192):
/// tag4 LE, type4 LE, result4 LE, then a one-byte sub-action count and every sub-action result as 4 LE bytes, then
/// the completion union. The count is narrowed to one byte, not clamped, so a list longer than 255 still packs
/// every element (D8).
///
/// <b>Named gap (report U6).</b> The union's concrete variant is the virtual union getter's body, which this
/// surface does not settle. This type carries only the 4-byte +0x1C cache value (<see cref="CompletionUnion"/>)
/// and packs it as four LE bytes. It does NOT invent an <c>ActionCompletedUnion</c> tag or payload.
/// </summary>
// fidelity: M7-020
internal readonly record struct RobotCompletedAction(
    uint Tag,
    int ActionType,
    uint Result,
    IReadOnlyList<uint> SubActionResults,
    uint CompletionUnion)
{
    /// <summary>The outer <c>MessageEngineToGame</c> union tag for <c>RobotCompletedAction</c>: uint16 0x005A (90, D9/MessageEngineToGame.cs:104).</summary>
    public const ushort UnionTag = 0x005A;

    /// <summary>
    /// D8/D9: the packed body — tag4 LE, type4 LE, result4 LE, one-byte count, all result4 elements, then the
    /// union's 4 LE bytes (the +0x1C cache width; the concrete variant is UNKNOWN, see the type summary).
    /// </summary>
    public byte[] PackBody()
    {
        var subs = SubActionResults;
        var w = new CladWriter().U32(Tag).I32(ActionType).U32(Result).U8(unchecked((byte)subs.Count));
        foreach (var s in subs) w.U32(s);
        w.U32(CompletionUnion);
        return w.ToArray();
    }

    /// <summary>The <c>MessageEngineToGame</c> union member: uint16 0x005A then <see cref="PackBody"/>.</summary>
    public byte[] ToUnionBytes()
    {
        var w = new CladWriter().U16(UnionTag);
        w.Bytes(PackBody());
        return w.ToArray();
    }
}