namespace Cozmo.Robot.Animation;

// fidelity: M5-053, M5-044, M5-056
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
            var owner = engine.AnimationSubscriptionFactory is { } factory
                ? factory(tags[i], e2g) : engine.AnimationMessages.Subscribe(tags[i], e2g);
            _owners.Add((tags[i], e2g, owner));
            if (engine.AnimationSubscriptionFactory is null)
                engine.EmitStackLog($"MISSING: M5-053/044 subscription 0x{tags[i]:X2} callback recipient; concrete signal ownership/unlink is built (M5-056)");
        }
    }
    internal void Release()
    {
        while (_owners.Count != 0)
        {
            var slot = _owners[^1]; _owners.RemoveAt(_owners.Count - 1);
            if (slot.Owner is { } owner) owner.Dispose();
            else _engine.EmitStackLog($"MISSING: M5-053/044 subscription 0x{slot.Tag:X2} injected ownership recipient; release order applied");
        }
    }
}
