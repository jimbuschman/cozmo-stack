namespace Cozmo.Robot.Animation;

/// <summary>The SayText action up to the unavailable WavePortal-to-robot float output join.</summary>
internal sealed class SayTextAction : ActionRunner
{
    private readonly CozmoRobot _robot;
    private readonly EngineRobot _owner;
    private readonly TextToSpeechComponent _speech;
    private readonly byte _operation;
    internal float Pitch { get; }
    private bool _retired;
    private bool _taken;
    private readonly bool _direct;
    private Task<IDirectSpeechSession>? _opening;
    private IDirectSpeechSession? _session;
    private readonly Func<float[], Task<IDirectSpeechSession>>? _openForTest;
    private TriggerAnimationAction? _child;
    internal TaskCompletionSource<(uint Result, bool OwnerRetired)>? HostCompletion;

    internal SayTextAction(CozmoRobot robot, EngineRobot owner, string text, byte style, float durationScalar, float pitch, bool direct = false,
        TextToSpeechComponent? speechForTest = null, Func<float[], Task<IDirectSpeechSession>>? openForTest = null)
        : base(0x20, 0)
    {
        _robot = robot;
        _owner = owner;
        _speech = speechForTest ?? owner.SpeechComponent;
        _openForTest = openForTest;
        _direct = direct;
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
        if (_retired || _owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, _owner)) return 0x03000000;
        byte state = _speech.GetOperationState(_operation);
        if (state == 0) return 0x03000000;
        if (state != 2) return 0x01000000;
        if (_direct)
        {
            if (!_speech.TakeSpeech(_operation, out var samples)) return 0x03000000;
            _taken = true;
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
        if (!_direct) return EngineActionResult.Running;
        if (_retired || _owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, _owner)) return 0x03000000;
        if (_session is null)
        {
            if (_opening is null || !_opening.IsCompleted) return EngineActionResult.Running;
            if (!_opening.IsCompletedSuccessfully)
            {
                _robot.Engine.EmitStackLog($"M14-006 direct speech initialization failed: {_opening.Exception?.GetBaseException().Message}");
                return 0x03000000;
            }
            _session = _opening.Result;
            var clip = new AnimationClip { Name = "SayTextAnimation", IsLive = true, Tracks = AnimationTrack.Audio,
                Keyframes = new Keyframe[] { new AudioKeyframe(0, new long[] { 0xBF0BACE1 }, 1f, new[] { 1f }, false) } };
            _child = new TriggerAnimationAction(_robot, clip,
                () => !_retired && !_owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, _owner), diagnosticReported: true);
            _child.SetSpeechPath(_session.Path, _owner);
        }
        if (_session.Failure is { } failure)
        {
            _robot.Engine.EmitStackLog($"M14-006 direct speech output failed: {failure.Message}");
            return 0x03000000;
        }
        return _child!.Update();
    }

    public override void WatcherEnding()
    {
        if (_retired) return;
        _retired = true;
        if (_direct) { if (!_taken) _speech.ClearOperationData(_operation); }
        else _speech.CleanupAudioEngine(_operation);
        if (_child is { } child) { child.Prep(); child.WatcherEnding(); _child = null; }
        if (_session is { } session) session.Retire();
        else if (_opening is { } opening)
            _ = opening.ContinueWith(t => { if (t.Status == TaskStatus.RanToCompletion) t.Result.Retire(); else _ = t.Exception; }, TaskScheduler.Default);
        HostCompletion?.TrySetResult((State, _owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, _owner)));
        base.WatcherEnding();
    }
}

