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
    internal static string NameOf(uint id) => "Origin" + id.ToString(CultureInfo.InvariantCulture);
}
