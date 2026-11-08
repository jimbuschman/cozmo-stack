using Cozmo.Robot.Animation;
using Cozmo.Robot.Emulation;

namespace Cozmo.Robot;

public sealed partial class EngineRobot
{
    private SpeechProvider? _speechProvider;
    internal TextToSpeechComponent SpeechComponent { get; private set; } = null!;

    internal void InitSpeechComponent()
    {
        SpeechComponent = new TextToSpeechComponent((text, style, scalar) =>
        {
            string resources = Engine.Options.ResourcesPath ?? throw new InvalidOperationException("M14-006 speech resources are unavailable");
            string language = Engine.Options.LabLocaleLanguage ?? throw new InvalidOperationException("M14-006 speech locale input is unavailable");
            string libDir = ShippedLibraries.FindLibDir() ?? throw new InvalidOperationException("M14-006 shipped ARM libraries are unavailable");
            _speechProvider ??= new SpeechProvider(resources, libDir, language);
            return _speechProvider.Synthesize(text, style, scalar);
        });
        Lifetime.Bind(0x268, address =>
        {
            SpeechComponent.Retire(() => { _speechProvider?.Dispose(); _speechProvider = null; });
            Lifetime.Missing(address, 0x268);
        });
    }
}

public sealed partial class CozmoRobot
{
    /// <summary>Internal admission used until the public SayText API and float output join are available.</summary>
    internal void QueueSayTextForNativePath(string text, byte style, float durationScalar, float pitch = 0)
    {
        var owner = Engine.Robot;
        Engine.PostGameMessage(() =>
        {
            if (owner is null || owner.Lifetime.Destroying || !ReferenceEquals(Engine.Robot, owner)) return;
            var action = new SayTextAction(this, owner, text, style, durationScalar, pitch);
            owner.ActionList.QueueAction(QueueActionPosition.Now, action, 0);
        });
    }

    internal void QueueSayTextIntentForNativePath(string text, string intent)
    {
        var owner = Engine.Robot;
        Engine.PostGameMessage(() =>
        {
            if (owner is null || owner.Lifetime.Destroying || !ReferenceEquals(Engine.Robot, owner)) return;
            if (Engine.Options.ResourcesPath is not { } resources)
            {
                Engine.EmitStackLog("MISSING M14-006: SayText intent resources are unavailable");
                return;
            }
            var metadata = SpeechMetadata.Load(resources);
            if (!metadata.TrySelect(intent, text, Engine.ContextRandom, out var selection))
            {
                // 0x5610A4 logs CanNotFind.SayTextIntentConfig, then 0x561186 reads an
                // uninitialized style. M1-034 skips the action before synthesis.
                Engine.EmitStackLog($"M1-034 M14-006 CanNotFind.SayTextIntentConfig at 0x005610A4; missing intent {intent}; skipped uninitialized style read at 0x00561186");
                return;
            }
            var action = new SayTextAction(this, owner, text, selection.Style, selection.Duration, selection.Pitch);
            owner.ActionList.QueueAction(QueueActionPosition.Now, action, 0);
        });
    }
}
