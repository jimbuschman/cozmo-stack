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
        // Production audio startup is attempted before Robot construction. The native
        // controller ignores InitScene failure (0x005935EA..F0); voice initialization
        // still belongs to the Robot constructor. Asset-free offline fixtures stay lazy.
        if (Engine.RuntimeAudioStarted && Engine.Options.ResourcesPath is not null && Engine.Options.LabLocaleLanguage is not null)
        {
            try { GetSpeechProvider(); }
            catch (Exception error) { ReportVoiceInitializationFailure(error); }
        }
    }

    private void ReportVoiceInitializationFailure(Exception error)
    {
        // Java doLoadVoice maps license/load failures to RESULT_FAIL_INVALID_PARAMETER
        // (50331648); the native provider logs that return at 0x0069FDD6..0x0069FE32.
        if (error is AcapelaTtsException { Step: "nSetLicense" or "nLoadVoice" })
        {
            Engine.EmitSourceLog(EngineLogLevel.Error, null, "TextToSpeechProvider.Initialize.LoadVoice", "Unable to load voice (error=50331648)");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
        }
        // Missing host files or emulator/platform failures are operational errors,
        // not an unimplemented engine step. Later synthesis failure sets state 0.
        Engine.EmitStackLog("M14-006 speech voice initialization failed: " + error.Message);
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
    internal string? RuntimeAudioStartupFailure { get; private set; }
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
            RuntimeAudioStartupFailure = problem;
            EmitStackLog("M6-024 InitScene failed: " + problem);
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
            string? failedBank = error.Data[NativeWwiseVoiceHost.BankFailureName] as string;
            RuntimeAudioStartupFailure = failedBank is null ? error.Message : $"{failedBank}: {error.Message}";
            // The native LoadAudioScene returns false at 0x008D2FAE;
            // CozmoAudioController ctor ignores that result at 0x005935EA..F0.
            if (failedBank is not null)
                EmitSourceLog(EngineLogLevel.Info, "AudioEngine", "AudioEngineController.LoadSoundbank", $"Failed to load soundbank '{failedBank}'"); // 0x008D311C..22
            message = "M6-024 InitScene failed: " + RuntimeAudioStartupFailure;
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
            (!Engine.RuntimeAudioStarted && (banks is null || !File.Exists(Path.Combine(banks, "Init.bnk")) ||
                  !File.Exists(Path.Combine(banks, "English(US)", "Cozmo.bnk")) || ShippedLibraries.FindLibDir() is null)) ||
            owner is null || owner.Lifetime.Destroying ||
            !ReferenceEquals(Engine.Robot, owner) || !Engine.IsReadyDelivered || Engine.ConnectionState != 2 ||
            Engine.RobotValidated != true || !owner.FirstFullStateHandled || !owner.ReadyToStream ||
            owner.AudioOutputSource != RobotAudioOutputSource.PlayOnRobot)
            return Task.FromResult(new AiSpeechActionResult(false, null, "Direct English robot speech is not ready"));
        // With a ready native robot, a failed shared audio initialization is
        // a real SayText preparation failure, not an unadmitted command: see
        // SayTextAction.Init 0x005614B8..BE -> 0x005615E4..0x00561630.
        byte style = 3;
        float duration = 1f, pitch = 0f;
        if (fitName)
        {
            if (Animations.Library is null)
                return Task.FromResult(new AiSpeechActionResult(false, null, "Speech animation catalog is unavailable (host fitted-name request)"));
            try
            {
                var metadata = SpeechMetadata.Load(Engine.Options.ResourcesPath);
                if (!metadata.TrySelect("Name_Normal", text, Engine.ContextRandom, out var selected))
                    return Task.FromResult(new AiSpeechActionResult(false, null, "Name_Normal intent unavailable (M1-034: no undefined intent read at 0x00561186)"));
                if (selected.Style != 1)
                    return Task.FromResult(new AiSpeechActionResult(false, null, "Name_Normal configuration does not select the requested host name voice style"));
                style = selected.Style; duration = selected.Duration; pitch = selected.Pitch;
            }
            catch (Exception ex)
            {
                return Task.FromResult(new AiSpeechActionResult(false, null, $"Speech intent configuration failed: {ex.Message}"));
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
        IDisposable? freeplay = null;
        // Host speech follows the same command bracket as motion: G2E 0x92
        // (0x005A4FA2) and Selection Wait 0x93 (0x0060AAAE), then QueueSingleAction
        // (SayText factory 0x0052AB00; external tag/position 0x00527A74..0x00527A8C).
        try { if (!disabled) freeplay = owner.NativeBehaviorRuntime?.BeginAiCommand(); }
        catch
        {
            action.RetireNeverAdmittedTree();
            throw;
        }
        try
        {
            if (!Current() || cancellationToken.IsCancellationRequested)
            {
                action.RetireNeverAdmittedTree();
                return Task.FromResult(new AiSpeechActionResult(false, null, "Robot changed or request cancelled before admission", !Current()));
            }
            disabled = owner.ActionList.ExternalActionsDisabled;
            int queued = owner.ActionList.QueueActionGuardedAtEnd(action, 0, Current);
            if (disabled || queued != 0)
            {
                if (action.CanDetachWatcherForRetirement())
                {
                    action.Watcher = null;
                    action.RetireNeverAdmittedTree();
                }
                return Task.FromResult(new AiSpeechActionResult(false, null, "Speech action was not admitted", !Current()));
            }
            action.HostCaller = ActionRunner.CallerContext.Value;
            var task = AwaitSpeech(action, owner, cancellationToken, freeplay);
            freeplay = null;
            return task;
        }
        finally { freeplay?.Dispose(); }
    }

    private async Task<AiSpeechActionResult> AwaitSpeech(SayTextAction action, EngineRobot owner, CancellationToken token, IDisposable? freeplay)
    {
        using var registration = token.Register(() => Engine.PostGameMessage(() =>
        {
            if (owner.Lifetime.Destroying || !ReferenceEquals(Engine.Robot, owner)) return;
            if (owner.ActionList.ContainsRunner(action)) owner.ActionList.Cancel(action.Tag);
        }));
        try
        {
            var ended = await action.HostCompletion!.Task.ConfigureAwait(false);
            return new AiSpeechActionResult(true, ended.Result, action.HostFailureDetail is { } failure ? $"Native speech action ended: {failure}" : "Native speech action ended", ended.OwnerRetired);
        }
        finally
        {
            // The action watcher precedes unlink, so resume after the queue gate releases.
            using var lease = Engine.EnterSynchronousOwner();
            freeplay?.Dispose();
        }
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
                Engine.EmitStackLog("M14-006: SayText intent resources are unavailable (host configuration)");
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
