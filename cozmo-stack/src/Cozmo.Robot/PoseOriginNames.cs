using System.Globalization;
using Cozmo.Transport;
namespace Cozmo.Robot;
// fidelity: M4-037
internal sealed class PoseTreeNameNode(string name)
{
    internal string Name = name;
    internal PoseTreeNameNode? Parent;
    internal PoseTreeNameNode FindRoot(SourceLogSink? log)
    {
        var node = Parent ?? this;
        int steps = 0;
        while (node.Parent is not null)
        {
            node = node.Parent;
            if (++steps == 1000)
            {
                log?.Invoke(EngineLogLevel.Error, null, "LoopBoundOverflow", "../../../../coretech/common/include/anki/common/basestation/math/poseTreeNode.h:325");
                EngineErrorState.StoreAndMaybeBreak();
                break;
            }
        }
        return node;
    }
}
// fidelity: M4-037
internal sealed class PoseOriginNames
{
    private readonly Dictionary<uint, PoseTreeNameNode> _origins = new();
    internal Action<string>? Missing;
    internal uint NextId { get; private set; } = 1;
    internal uint CurrentId { get; private set; }
    internal bool AddOriginWithId(uint id, SourceLogSink? log)
    {
        if (_origins.ContainsKey(id))
        {
            // The checked row settles the verify-failed return, not its diagnostic format.
            Missing?.Invoke("MISSING: M4-037 duplicate origin sVerifyFailedReturnFalse 0x00847B7A diagnostic recipient; duplicate refused");
            return false;
        }
        _origins.Add(id, new("Origin" + id.ToString(CultureInfo.InvariantCulture)));
        CurrentId = id; NextId = Math.Max(unchecked(id + 1), NextId);
        return true;
    }
    internal PoseTreeNameNode? Get(uint id) => _origins.GetValueOrDefault(id);
    // fidelity: M11-044 - Rejigger 0x00847E48..0x00847E7E: the old current origin's node is parented to the new one and renamed + "_REJ"; the new one becomes current.
    internal void Rejigger(uint oldId, uint newId)
    {
        if (Get(oldId) is { } old) { old.Parent = Get(newId); old.Name += "_REJ"; }
        CurrentId = newId;
    }
    // fidelity: M11-044 - Flatten 0x00847F70..0x00847F8C: a flattened origin is parented to the target and renamed + "_FLT".
    internal void Flatten(uint id, uint targetId)
    {
        if (Get(id) is { } node) { node.Parent = Get(targetId); node.Name += "_FLT"; }
    }
    internal static string NameOf(uint id) => "Origin" + id.ToString(CultureInfo.InvariantCulture);
}
