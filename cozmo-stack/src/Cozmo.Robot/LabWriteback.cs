using Cozmo.Transport;
namespace Cozmo.Robot;

internal sealed record LabAssignmentDef(byte[] Key, byte[] User, byte[] Variation);
internal sealed record LabAssignmentPair(byte[] Key, byte[] Variation);

// fidelity: M15-050 — checked OV59. Raw byte vectors, not decoded/normalized strings.
internal sealed partial class CozmoExperiments
{
    private IReadOnlyList<LabAssignmentDef>? _activeAssignments;
    private IReadOnlyList<LabAssignmentPair>? _cachedAssignments;
    // Explicit input boundary: native activation/restore producers remain M15-052/M15-036.
    // Tests may supply their vector values; normal startup never substitutes invented empty assignments.
    internal void SetAssignmentVectorsForTest(IReadOnlyList<LabAssignmentDef> active, IReadOnlyList<LabAssignmentPair> cache)
    { lock (_gate) { _activeAssignments = active; _cachedAssignments = cache; } }
    internal IReadOnlyList<LabAssignmentPair>? CachedAssignments { get { lock (_gate) return _cachedAssignments; } }

    internal void PossiblyWriteLabAssignmentsToRobot()
    {
        IReadOnlyList<LabAssignmentDef>? active; IReadOnlyList<LabAssignmentPair>? cache;
        lock (_gate) { active = Lab.Assignments40?.ToArray() ?? _activeAssignments; cache = _cachedAssignments; }
        if (active is null || cache is null)
        {
            Missing("M15-050/M15-052/M15-036 PossiblyWriteLabAssignmentsToRobot0x006A5DE4: active assignment and restored cache vector producers are unbuilt; no empty vector or selected variation fabricated");
            return;
        }
        bool equal = active.Count == cache.Count;
        for (int i = 0; equal && i < active.Count; i++)
            equal = cache[i].Key.AsSpan().SequenceEqual(active[i].Key) && cache[i].Variation.AsSpan().SequenceEqual(active[i].Variation);
        if (equal) return;
        _engine.EmitSourceLog(EngineLogLevel.Info, "Unnamed", "CozmoExperiments.PossiblyWriteLabAssignmentsToRobot", "Writing updated lab assignments to robot");
        WriteLabAssignmentsToRobot(active);
        // 0x006A5F62..0x006A6014: even no robot or immediate NV rejection refreshes cache; no ack controls this.
        var next = active.Select(a => new LabAssignmentPair(a.Key.ToArray(), a.Variation.ToArray())).ToArray();
        lock (_gate) _cachedAssignments = next;
    }
    internal void WriteLabAssignmentsToRobot(IReadOnlyList<LabAssignmentDef> active)
    {
        var first = _engine.Robots.GetFirstRobot();
        if (first is null) return; // before projection/size/pack
        if (!ReferenceEquals(first, _engine.Robot))
        { Missing("M15-050 first robot+0x260 NV owner at0x006A58C0: only the host's primary robot NV component is represented; no other robot's write is routed through that owner"); return; }
        byte[] payload = PackAssignments(active);
        if (_engine.NvStorage is not { } nv)
        { Missing("M15-050 first robot+0x260 NV owner at0x006A58C0 is absent; no fabricated Write result"); return; }
        if (nv.Write(0x196000, payload, null, false) != 0) return;
        _engine.EmitSourceLog(EngineLogLevel.Error, null, "CozmoExperiments.WriteLabAssignmentsToRobot.Failed", "Write failed");
        EngineErrorState.StoreAndMaybeBreak();
    }
    internal static byte[] PackAssignments(IReadOnlyList<LabAssignmentDef> active)
    {
        uint size = 1;
        foreach (var a in active) size = unchecked(size + 2u + (uint)a.Key.Length + (uint)a.Variation.Length);
        var result = new byte[size]; // native Size uses full lengths; allocated tail is zero.
        int offset = 0; result[offset++] = unchecked((byte)active.Count);
        void Text(byte[] bytes)
        {
            int count = unchecked((byte)bytes.Length);
            result[offset++] = (byte)count;
            bytes.AsSpan(0, count).CopyTo(result.AsSpan(offset)); offset += count;
        }
        foreach (var a in active) { Text(a.Key); Text(a.Variation); }
        return result; // full allocation length, not truncated Pack written count
    }
}
