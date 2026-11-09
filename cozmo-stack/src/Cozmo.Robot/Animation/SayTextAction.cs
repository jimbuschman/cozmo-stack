using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation;

/// <summary>The SayText action up to the unavailable WavePortal-to-robot float output join.</summary>
internal sealed class SayTextAction : ActionRunner
{
    private readonly CozmoRobot _robot;
    private readonly EngineRobot _owner;
    private readonly TextToSpeechComponent _speech;
    private readonly byte _operation;
    internal string Text { get; }
    internal int CannedTracks => _cannedTracks;
    internal float Pitch { get; }
    private volatile bool _retired;
    private bool _taken;
    private readonly bool _direct;
    private readonly bool _fitName;
    private AnimationTrigger? _cannedTrigger;
    private int _cannedTracks;
    private Task<CannedSpeechLease>? _cannedOpening;
    private CannedSpeechLease? _cannedSession;
    private AnimationClip? _fittedClip;
    private float _durationMs;
    private Task? _configuration;
    private readonly object _fitSelectionGate = new();
    private Task<IDirectSpeechSession>? _opening;
    private IDirectSpeechSession? _session;
    private readonly Func<float[], Task<IDirectSpeechSession>>? _openForTest;
    private TriggerAnimationAction? _child;
    internal TaskCompletionSource<(uint Result, bool OwnerRetired)>? HostCompletion;
    internal TriggerAnimationAction? ChildForTest => _child;
    internal void SetCannedTrigger(AnimationTrigger trigger, int tracksToLock = 0)
    { _cannedTrigger = trigger; _cannedTracks = tracksToLock; }

    internal SayTextAction(CozmoRobot robot, EngineRobot owner, string text, byte style, float durationScalar, float pitch, bool direct = false,
        TextToSpeechComponent? speechForTest = null, Func<float[], Task<IDirectSpeechSession>>? openForTest = null, bool fitName = false)
        : base(0x20, 0)
    {
        _robot = robot;
        Text = text;
        _owner = owner;
        _speech = speechForTest ?? owner.SpeechComponent;
        _openForTest = openForTest;
        _direct = direct;
        _fitName = fitName;
        Pitch = pitch;
        SourceLog = (level, channel, key, message) => robot.Engine.EmitSourceLog(level, channel, key, message);
        Log = robot.Engine.EmitStackLog;
        if (style < 4) _operation = _speech.CreateSpeech(text, style, durationScalar);
        else robot.Engine.EmitStackLog("M14-006: SayText voice style is outside the supported engine styles");
    }

    protected override string ActionName => "SayText";
    protected override float EngineClockSeconds => _robot.Engine.Timer.SecondsF;
    // SayTextAction vtable slot +0x14 resolves to 0x0052B0B2, which returns zero.
    public override bool CanInterrupt() => false;
    internal override bool RetireNeverAdmittedTree()
    {
        if (!base.RetireNeverAdmittedTree()) return false;
        _retired = true;
        _speech.ClearOperationData(_operation);
        return true;
    }

    public override int Init()
    {
        _speech.DrainErrors(_robot.Engine.EmitStackLog);
        if (DestructionObserved) return unchecked((int)EngineActionResult.Running);
        if (_retired || _owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, _owner)) return 0x03000000;
        byte state = _speech.GetOperationState(_operation);
        if (state == 0) return 0x03000000;
        if (state != 2) return 0x01000000;
        if (_direct || _cannedTrigger is not null)
        {
            if (!_speech.TakeSpeech(_operation, out var samples)) return 0x03000000;
            _taken = true;
            _durationMs = (float)(((double)samples.Length / 22050.0) * 1000.0);
            if (_cannedTrigger is not null)
            {
                var owner = _owner.GetCannedSpeechOwner();
                if (owner is null)
                {
                    _robot.Engine.EmitStackLog("MISSING VOICE-CANNED-NAME-DELIVERY-1: local shared audio device, archive or robot output is unavailable");
                    return 0x03000000;
                }
                _cannedOpening = owner.Acquire(samples, () => !_retired && !_owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, _owner));
                return 0;
            }
            string? library = Emulation.ShippedLibraries.FindLibDir();
            string? banks = _robot.Engine.Options.SpeechSoundBankDirectory;
            if (_openForTest is { } open) _opening = open(samples);
            else
            {
                if (library is null || banks is null) return 0x03000000;
                _opening = Task.Run<IDirectSpeechSession>(async () => await DirectSpeechSession.Open(library, banks, samples,
                    message => _robot.Engine.PostGameMessage(() => _robot.Engine.EmitStackLog(message))).ConfigureAwait(false));
            }
            return 0;
        }
        // The native path transfers the speech to WavePortal here. Do not claim it when the
        // selected Wwise source has no connected float-frame/RTPC/Hijack production path.
        _robot.Engine.EmitStackLog("MISSING M14-006/M6-018: SayText audio cannot enter the native WavePortal float-frame and robot-output path");
        return 0x03000000;
    }

    public override uint CheckIfDone()
    {
        if (!_direct && _cannedTrigger is null) return EngineActionResult.Running;
        if (DestructionObserved) return EngineActionResult.Running;
        if (_retired || _owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, _owner)) return 0x03000000;
        if (_cannedTrigger is { } trigger)
        {
            if (_cannedSession is null)
            {
                if (_cannedOpening is null || !_cannedOpening.IsCompleted) return EngineActionResult.Running;
                if (!_cannedOpening.IsCompletedSuccessfully)
                {
                    _robot.Engine.EmitStackLog($"M14-006 canned speech initialization failed: {_cannedOpening.Exception?.GetBaseException().Message}");
                    return 0x03000000;
                }
                _cannedSession = _cannedOpening.Result;
                if (_retired || _owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, _owner)) return 0x03000000;
                var child = new TriggerLiftSafeAnimationAction(_robot, new(trigger, 1, true, _cannedTracks, 60f, false));
                if (DestructionObserved || _retired || _owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, _owner))
                {
                    child.RetireNeverAdmittedTree();
                    return EngineActionResult.Cancelled;
                }
                child.SetSpeechPath(_cannedSession.Path, _owner, authored: true);
                child.Watcher = Watcher;
                _child = child;
                _configuration = _cannedSession.Configure(Pitch);
            }
            if (_cannedSession.Failure is { } cannedFailure)
            {
                _robot.Engine.EmitStackLog($"M14-006 canned speech output failed: {cannedFailure.Message}");
                return 0x03000000;
            }
            if (_configuration is null || !_configuration.IsCompleted) return EngineActionResult.Running;
            if (!_configuration.IsCompletedSuccessfully) return 0x03000000;
            return _child!.Update();
        }
        if (_session is null)
        {
            if (_opening is null || !_opening.IsCompleted) return EngineActionResult.Running;
            if (!_opening.IsCompletedSuccessfully)
            {
                _robot.Engine.EmitStackLog($"M14-006 direct speech initialization failed: {_opening.Exception?.GetBaseException().Message}");
                return 0x03000000;
            }
            _session = _opening.Result;
            bool Current() => !_retired && !_owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, _owner);
            if (_fitName)
            {
                if (_robot.Animations.Library is not { } catalog) { _robot.Engine.EmitStackLog("MISSING VOICE-ANIMATION-FIT-1: animation catalog is unavailable"); return 0x03000000; }
                try
                {
                    _fittedClip = SpeechFitBuilder.Build(_durationMs, () =>
                    {
                        if (!Current()) throw new InvalidOperationException("speech owner retired during fit");
                        AnimationClip? chosen;
                        using (LogCallbackDeferral.EnterGated(_fitSelectionGate)) chosen = SpeechFitBuilder.Select(catalog);
                        if (!Current()) throw new InvalidOperationException("speech owner retired during selection");
                        if (chosen is null) _robot.Engine.EmitStackLog("VOICE-ANIMATION-FIT-1: no animation was selected; retaining the partial fit");
                        if (!Current()) throw new InvalidOperationException("speech owner retired during fit diagnostic");
                        return chosen;
                    });
                }
                catch (Exception ex) { if (Current()) _robot.Engine.EmitStackLog($"VOICE-ANIMATION-FIT-1 fit failed: {ex.Message}"); return DestructionObserved ? EngineActionResult.Running : 0x03000000; }
                if (!Current()) return DestructionObserved ? EngineActionResult.Running : 0x03000000;
            }
            var clip = _fittedClip ?? new AnimationClip { Name = "SayTextAnimation", IsLive = true, Tracks = AnimationTrack.Audio,
                Keyframes = new Keyframe[] { new AudioKeyframe(0, new long[] { 0xBF0BACE1 }, 1f, new[] { 1f }, false) } };
            _child = new TriggerAnimationAction(_robot, clip,
                () => !_retired && !_owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, _owner), diagnosticReported: true);
            _child.SetSpeechPath(_session.Path, _owner);
            if (!Current()) return DestructionObserved ? EngineActionResult.Running : 0x03000000;
            _configuration = _session.Configure(_fitName ? 0x2F8B3BF4u : 0x36089802u, _fitName ? Pitch : 0f);
        }
        if (_session.Failure is { } failure)
        {
            _robot.Engine.EmitStackLog($"M14-006 direct speech output failed: {failure.Message}");
            return 0x03000000;
        }
        if (_configuration is null || !_configuration.IsCompleted) return EngineActionResult.Running;
        if (!_configuration.IsCompletedSuccessfully)
        {
            _robot.Engine.EmitStackLog($"M14-006 direct speech configuration failed: {_configuration.Exception?.GetBaseException().Message}");
            return 0x03000000;
        }
        return _child!.Update();
    }

    public override void WatcherEnding()
    {
        if (_retired) return;
        _retired = true;
        if (_direct || _cannedTrigger is not null) { if (!_taken) _speech.ClearOperationData(_operation); }
        else _speech.CleanupAudioEngine(_operation);
        _cannedSession?.ClearPending(); // native cleanup precedes the child destructor
        if (_child is { } child) { child.Prep(); child.WatcherEnding(); _child = null; }
        if (_session is { } session) session.Retire();
        else if (_opening is { } opening)
            _ = opening.ContinueWith(t => { if (t.Status == TaskStatus.RanToCompletion) t.Result.Retire(); else _ = t.Exception; }, TaskScheduler.Default);
        if (_cannedSession is { } canned) canned.Retire();
        else if (_cannedOpening is { } pending)
            _ = pending.ContinueWith(t => { if (t.Status == TaskStatus.RanToCompletion) { t.Result.ClearPending(); t.Result.Retire(); } else _ = t.Exception; }, TaskScheduler.Default);
        HostCompletion?.TrySetResult((State, _owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, _owner)));
        base.WatcherEnding();
    }
}

