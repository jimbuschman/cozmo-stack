using Cozmo.Robot.Animation;
using Cozmo.Robot.Emulation;

namespace Cozmo.Robot;

public sealed partial class EngineRobot
{
    private SpeechProvider? _speechProvider;
    private CannedSpeechOwner? _cannedSpeechOwner;
    internal void UpdateFixtureMusicAtEngineTail() => _cannedSpeechOwner?.UpdateMusicAtEngineTail();
    internal Func<IPlatformPcmSink>? CannedSpeechSinkForTest;
    internal TextToSpeechComponent SpeechComponent { get; private set; } = null!;

    internal void InitSpeechComponent()
    {
        SpeechComponent = new TextToSpeechComponent((text, style, scalar) =>
        {
            return GetSpeechProvider().Synthesize(text, style, scalar);
        });
        Lifetime.Bind(0x268, address =>
        {
            _cannedSpeechOwner?.Retire(); _cannedSpeechOwner = null;
            SpeechComponent.Retire(() => { _speechProvider?.Dispose(); _speechProvider = null; });
            Lifetime.Missing(address, 0x268);
        });
        // Robot ctor 0x0050FDB4 -> component ctor 0x0069E3FC -> provider ctor 0x0069F7FE.
        // The impl loads the voice synchronously at 0x0069FDCE, before any CreateSpeech.
        // Production audio is initialized before Robot construction. Keep asset-free offline
        // fixtures lazy, but do not charge a real robot's first utterance for loading its voice.
        if (Engine.RuntimeWwiseAudio?.Ready.IsCompletedSuccessfully == true)
        {
            try { GetSpeechProvider(); }
            catch (Exception error) { Engine.EmitStackLog("MISSING M14-006 speech voice initialization failed: " + error.Message); }
        }
    }

    private SpeechProvider GetSpeechProvider()
    {
        string resources = Engine.Options.ResourcesPath ?? throw new InvalidOperationException("M14-006 speech resources are unavailable");
        string language = Engine.Options.LabLocaleLanguage ?? throw new InvalidOperationException("M14-006 speech locale input is unavailable");
        string libDir = ShippedLibraries.FindLibDir() ?? throw new InvalidOperationException("M14-006 shipped ARM libraries are unavailable");
        return _speechProvider ??= new SpeechProvider(resources, libDir, language);
    }

    private CannedSpeechOwner? GetBehaviorAudioOwner()
    {
        if (Engine.RuntimeWwiseAudio is { } runtime) return runtime;
        if (Engine.RuntimeAudioStarted) return null;
        if (!OperatingSystem.IsWindows() && CannedSpeechSinkForTest is null) return null;
        if (_cannedSpeechOwner is not null) return _cannedSpeechOwner;

        string? library = ShippedLibraries.FindLibDir();
        string? banks = Engine.Options.SpeechSoundBankDirectory;
        string? archive = Engine.ResolveSpeechAudioArchive(banks);
        if (library is null || archive is null || !File.Exists(archive)) return null;

        return _cannedSpeechOwner = new CannedSpeechOwner(library, banks, archive,
            message => Engine.PostGameMessage(() => Engine.EmitStackLog(message)), CannedSpeechSinkForTest);
    }

    internal CannedSpeechOwner? GetCannedSpeechOwner()
    {
        if (Lifetime.Destroying || !ReferenceEquals(Engine.Robot, this) ||
            AudioOutputSource != RobotAudioOutputSource.PlayOnRobot) return null;
        if (Engine.RuntimeAudioStarted) return Engine.RuntimeWwiseAudio;
        if (Engine.Options.LabLocaleLanguage != "en") return null;
        string? library = ShippedLibraries.FindLibDir();
        string? banks = Engine.Options.SpeechSoundBankDirectory;
        string? archive = Engine.ResolveSpeechAudioArchive(banks);
        if (!OperatingSystem.IsWindows() || library is null || banks is null || archive is null ||
            !File.Exists(Path.Combine(banks, "Init.bnk")) || !File.Exists(Path.Combine(banks, "English(US)", "Cozmo.bnk")) || !File.Exists(archive))
            return null;
        return _cannedSpeechOwner ??= new CannedSpeechOwner(library, banks, archive,
            message => Engine.PostGameMessage(() => Engine.EmitStackLog(message)), CannedSpeechSinkForTest);
    }
}

public sealed partial class CozmoEngine
{
    internal CannedSpeechOwner? RuntimeWwiseAudio { get; private set; }
    internal bool RuntimeAudioStarted { get; private set; }
    internal Func<IPlatformPcmSink>? RuntimeAudioSinkForTest;

    internal string? ResolveSpeechAudioArchive(string? banks)
    {
        if (Options.SpeechAudioArchivePath is { } explicitPath) return explicitPath;
        if (banks is not null)
        {
            string bankArchive = Path.Combine(banks, "AudioAssets.zip");
            if (File.Exists(bankArchive)) return bankArchive;
        }
        // Resource scope uses sound/ at 0x00592C3A..0x00592C52, with the shipped AudioAssets.zip.
        return Options.ResourcesPath is { } resources ? Path.Combine(resources, "sound", "AudioAssets.zip") : null;
    }

    // fidelity: M6-024 — context constructs the audio controller at 0x004EA86E before any Robot.
    private void StartRuntimeAudio()
    {
        if (RuntimeAudioStarted) return;
        RuntimeAudioStarted = true;
        string? archive = ResolveSpeechAudioArchive(Options.SpeechSoundBankDirectory);
        string? library = ShippedLibraries.FindLibDir();
        if (archive is null || !File.Exists(archive) || library is null ||
            (!OperatingSystem.IsWindows() && RuntimeAudioSinkForTest is null))
        {
            string problem = archive is null || !File.Exists(archive)
                ? $"Wwise archive {archive ?? "(no resource path)"} is unavailable"
                : library is null ? "shipped ARM Wwise libraries are unavailable" : "local PCM output is unavailable";
            EmitStackLog("MISSING M6-024 InitScene: " + problem);
            return;
        }
        var audio = new CannedSpeechOwner(library, Options.SpeechSoundBankDirectory, archive,
            message => PostGameMessage(() => EmitStackLog(message)), RuntimeAudioSinkForTest, initScene: true);
        RuntimeWwiseAudio = audio;
        // Context InitScene returns before ConnectToRobot can dispatch (0x004ED020..0x004ED07C).
        // Load on the Wwise worker and join here, before the engine/transport threads start.
        string message;
        try
        {
            audio.Ready.GetAwaiter().GetResult();
            message = "Audio engine loaded InitScene (Init, Music, UI, SFX, Cozmo, Dev_Debug)";
        }
        catch (Exception error)
        {
            message = "MISSING M6-024 InitScene: " + error.Message;
        }
        PostGameMessage(() => EmitStackLog(message));
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
            Engine.Options.ResourcesPath is null ||
            (Engine.RuntimeAudioStarted
                ? Engine.RuntimeWwiseAudio?.Ready.IsCompletedSuccessfully != true
                : banks is null || !File.Exists(Path.Combine(banks, "Init.bnk")) ||
                  !File.Exists(Path.Combine(banks, "English(US)", "Cozmo.bnk")) || ShippedLibraries.FindLibDir() is null) ||
            owner is null || owner.Lifetime.Destroying ||
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
        action.HostCaller = ActionRunner.CallerContext.Value;
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
