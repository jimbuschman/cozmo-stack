namespace Cozmo.Robot.Animation;

// fidelity: M5-053, M5-044, M5-056
// X9 4a/4b: sole shared owners; vector destroys back-to-front. Concrete subscription/unlink recipients are separate.
internal sealed class AnimationSubscriptionOwners
{
    private readonly CozmoEngine _engine;
    private readonly List<(byte Tag, bool E2G, IDisposable? Owner)> _owners = new();
    internal AnimationSubscriptionOwners(CozmoEngine engine, IReadOnlyList<byte> tags, bool firstEngineToGame, Action<byte, object?>? callback = null)
    {
        _engine = engine;
        for (int i = 0; i < tags.Count; i++)
        {
            bool e2g = firstEngineToGame && i == 0;
            byte tag = tags[i];
            var owner = engine.AnimationSubscriptionFactory is { } factory
                ? factory(tags[i], e2g) : engine.AnimationMessages.SubscribeMessage(tag, e2g, callback is null ? null : message => callback(tag, message));
            _owners.Add((tags[i], e2g, owner));
            if (engine.AnimationSubscriptionFactory is null && callback is null)
            {
                if(tag is 0xAE or 0xAF or 0xAD or 0xC4)
                    engine.EmitStackLog($"{Owner(tag)} subscription 0x{tag:X2}: {InputOwner(tag)}; concrete ownership/unlink is built (M5-056)");
                else engine.EmitStackLog($"MISSING: {Owner(tag)} subscription 0x{tag:X2} callback recipient; concrete ownership/unlink is built (M5-056)");
            }
        }
    }
    // fidelity: M5-061, M5-062, M5-063, M9-029
    private static string Owner(byte tag) => tag switch
    {
        0x5A or 0xB5 or 0xB6 => "M5-063", 0xB4 => "M5-062",
        0xAE or 0xAF => "M5-027/M5-057", 0xAD => "M5-061", 0xC4 => "M9-029", _ => "M5-056"
    };
    private static string InputOwner(byte tag)=>tag switch
    {
        0xAE or 0xAF=>"idle push/remove inputs supplied by the host API (M5-057)",
        0xAD=>"replay input owned by M5-061",0xC4=>"custom-song input owned by M9-029",_=>"callback input owned by "+Owner(tag)
    };
    internal void Release()
    {
        while (_owners.Count != 0)
        {
            var slot = _owners[^1]; _owners.RemoveAt(_owners.Count - 1);
            if (slot.Owner is { } owner) owner.Dispose();
            else _engine.EmitStackLog($"MISSING: {Owner(slot.Tag)} subscription 0x{slot.Tag:X2} injected ownership recipient; release order applied");
        }
    }
}
