using Cozmo.Robot.Animation;
using Cozmo.Robot.Emulation;

namespace Cozmo.Robot;

public sealed partial class EngineRobot
{
    private SpeechProvider? _speechProvider;
    private CannedSpeechOwner? _cannedSpeechOwner;
    internal Func<IPlatformPcmSink>? CannedSpeechSinkForTest;
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
            _cannedSpeechOwner?.Retire(); _cannedSpeechOwner = null;
            SpeechComponent.Retire(() => { _speechProvider?.Dispose(); _speechProvider = null; });
            Lifetime.Missing(address, 0x268);
        });
    }

    internal CannedSpeechOwner? GetCannedSpeechOwner()
    {
        string? library = ShippedLibraries.FindLibDir();
        string? banks = Engine.Options.SpeechSoundBankDirectory;
        string? archive = Engine.Options.SpeechAudioArchivePath ?? (banks is null ? null : Path.Combine(banks, "AudioAssets.zip"));
        if (!OperatingSystem.IsWindows() || Engine.Options.LabLocaleLanguage != "en" || library is null || banks is null || archive is null ||
            !File.Exists(Path.Combine(banks, "Init.bnk")) || !File.Exists(Path.Combine(banks, "English(US)", "Cozmo.bnk")) || !File.Exists(archive) ||
            Lifetime.Destroying || !ReferenceEquals(Engine.Robot, this) || AudioOutputSource != RobotAudioOutputSource.PlayOnRobot)
            return null;
        return _cannedSpeechOwner ??= new CannedSpeechOwner(library, banks, archive,
            message => Engine.PostGameMessage(() => Engine.EmitStackLog(message)), CannedSpeechSinkForTest);
    }
}

public sealed partial class CozmoRobot
{
    private uint _nextSpeechTag;
    internal TextToSpeechComponent? DirectSpeechComponentForTest;
    internal Func<float[], Task<IDirectSpeechSession>>? DirectSpeechOpenForTest;

    /// <summary>Requests direct English robot speech. Completion reports the native action result after the robot's animation completion.</summary>
    public Task<AiSpeechActionResult> SayEnglishSentenceAsync(string text, CancellationToken cancellationToken = default) =>
        QueueEnglishSpeech(text, false, cancellationToken);

    /// <summary>Requests the host-selected Name_Normal intent fitted to CozmoSaysSpeakLoop.</summary>
    public Task<AiSpeechActionResult> SayEnglishNameWithAnimationAsync(string text, CancellationToken cancellationToken = default) =>
        QueueEnglishSpeech(text, true, cancellationToken);

    private Task<AiSpeechActionResult> QueueEnglishSpeech(string text, bool fitName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        var owner = Engine.Robot;
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(new AiSpeechActionResult(false, null, "Request cancelled before admission"));
        using var lease = Engine.EnterSynchronousOwner();
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(new AiSpeechActionResult(false, null, "Request cancelled before admission"));
        string? banks = Engine.Options.SpeechSoundBankDirectory;
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(text) || Engine.Options.LabLocaleLanguage != "en" ||
            Engine.Options.ResourcesPath is null || banks is null ||
            !File.Exists(Path.Combine(banks, "Init.bnk")) || !File.Exists(Path.Combine(banks, "English(US)", "Cozmo.bnk")) ||
            ShippedLibraries.FindLibDir() is null || owner is null || owner.Lifetime.Destroying ||
            !ReferenceEquals(Engine.Robot, owner) || !Engine.IsReadyDelivered || Engine.ConnectionState != 2 ||
            Engine.RobotValidated != true || !owner.FirstFullStateHandled || !owner.ReadyToStream ||
            owner.AudioOutputSource != RobotAudioOutputSource.PlayOnRobot)
            return Task.FromResult(new AiSpeechActionResult(false, null, "Direct English robot speech is not ready"));
        byte style = 3;
        float duration = 1f, pitch = 0f;
        if (fitName)
        {
            if (Animations.Library is null)
                return Task.FromResult(new AiSpeechActionResult(false, null, "MISSING VOICE-ANIMATION-FIT-1: animation catalog is unavailable"));
            try
            {
                var metadata = SpeechMetadata.Load(Engine.Options.ResourcesPath);
                if (!metadata.TrySelect("Name_Normal", text, Engine.ContextRandom, out var selected) || selected.Style != 1)
                    return Task.FromResult(new AiSpeechActionResult(false, null, "MISSING VOICE-ANIMATION-FIT-1: Name_Normal style is unavailable"));
                style = selected.Style; duration = selected.Duration; pitch = selected.Pitch;
            }
            catch (Exception ex)
            {
                return Task.FromResult(new AiSpeechActionResult(false, null, $"MISSING VOICE-ANIMATION-FIT-1: intent configuration failed: {ex.Message}"));
            }
            if (owner.Lifetime.Destroying || !ReferenceEquals(Engine.Robot, owner))
                return Task.FromResult(new AiSpeechActionResult(false, null, "Robot changed before admission", true));
        }
        uint tag = 0;
        for (int i = 0; i < 1_000_000; i++)
        {
            _nextSpeechTag = _nextSpeechTag == 1_000_000 ? 1 : _nextSpeechTag + 1;
            if (!ActionRunnerTagCounter.Global.IsInUse(_nextSpeechTag)) { tag = _nextSpeechTag; break; }
        }
        if (tag == 0) return Task.FromResult(new AiSpeechActionResult(false, null, "No external action tag is available"));
        var action = new SayTextAction(this, owner, text, style, duration, pitch, direct: true,
            speechForTest: DirectSpeechComponentForTest, openForTest: DirectSpeechOpenForTest, fitName: fitName)
            { HostCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        if (!action.SetTag(tag))
        {
            action.Prep(); action.WatcherEnding();
            return Task.FromResult(new AiSpeechActionResult(false, null, "Action tag was unavailable"));
        }
        if (cancellationToken.IsCancellationRequested)
        {
            action.RetireNeverAdmittedTree();
            return Task.FromResult(new AiSpeechActionResult(false, null, "Request cancelled before admission"));
        }
        bool Current() => !owner.Lifetime.Destroying && ReferenceEquals(Engine.Robot, owner);
        bool disabled = owner.ActionList.ExternalActionsDisabled;
        int queued = owner.ActionList.QueueActionGuardedAtEnd(action, 0, Current);
        if (disabled || queued != 0)
        {
            action.RetireNeverAdmittedTree();
            return Task.FromResult(new AiSpeechActionResult(false, null, "Speech action was not admitted"));
        }
        return AwaitSpeech(action, owner, cancellationToken);
    }

    private async Task<AiSpeechActionResult> AwaitSpeech(SayTextAction action, EngineRobot owner, CancellationToken token)
    {
        using var registration = token.Register(() => Engine.PostGameMessage(() =>
        {
            if (owner.Lifetime.Destroying || !ReferenceEquals(Engine.Robot, owner)) return;
            if (owner.ActionList.ContainsRunner(action)) owner.ActionList.Cancel(action.Tag);
        }));
        var ended = await action.HostCompletion!.Task.ConfigureAwait(false);
        using (Engine.EnterSynchronousOwner()) { }
        return new AiSpeechActionResult(true, ended.Result, "Native speech action ended", ended.OwnerRetired);
    }

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

/// <summary>Admission and native action result for direct English speech. OwnerRetired records whether
/// the original robot was being destroyed or replaced when its action ended.</summary>
public sealed record AiSpeechActionResult(bool Admitted, uint? EngineResult, string Detail, bool OwnerRetired = false);
