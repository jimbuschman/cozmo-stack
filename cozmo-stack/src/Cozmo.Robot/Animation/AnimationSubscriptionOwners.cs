namespace Cozmo.Robot.Animation;

// fidelity: M5-053, M5-044
// X9 4a/4b: sole shared owners; vector destroys back-to-front. Concrete subscription/unlink recipients are separate.
internal sealed class AnimationSubscriptionOwners
{
    private readonly CozmoEngine _engine;
    private readonly List<(byte Tag, bool E2G, IDisposable? Owner)> _owners = new();
    internal AnimationSubscriptionOwners(CozmoEngine engine, IReadOnlyList<byte> tags, bool firstEngineToGame)
    {
        _engine = engine;
        for (int i = 0; i < tags.Count; i++)
        {
            bool e2g = firstEngineToGame && i == 0;
            _owners.Add((tags[i], e2g, engine.AnimationSubscriptionFactory?.Invoke(tags[i], e2g)));
        }
    }
    internal void Release()
    {
        while (_owners.Count != 0)
        {
            var slot = _owners[^1]; _owners.RemoveAt(_owners.Count - 1);
            if (slot.Owner is { } owner) owner.Dispose();
            else _engine.EmitStackLog($"MISSING: M5-053/044 {(slot.E2G ? "E2G" : "G2E")} subscription 0x{slot.Tag:X2} concrete ownership/unlink recipient (0x0066411C / 0x00662750); release order applied");
        }
    }
}
