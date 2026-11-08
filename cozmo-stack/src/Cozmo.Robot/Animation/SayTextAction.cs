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

    internal SayTextAction(CozmoRobot robot, EngineRobot owner, string text, byte style, float durationScalar, float pitch)
        : base(0x20, 0)
    {
        _robot = robot;
        _owner = owner;
        _speech = owner.SpeechComponent;
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

    public override int Init()
    {
        _speech.DrainErrors(_robot.Engine.EmitStackLog);
        if (_retired || _owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot, _owner)) return 0x03000000;
        byte state = _speech.GetOperationState(_operation);
        if (state == 0) return 0x03000000;
        if (state != 2) return 0x01000000;
        // The native path transfers the speech to WavePortal here. Do not claim it when the
        // selected Wwise source has no connected float-frame/RTPC/Hijack production path.
        _robot.Engine.EmitStackLog("MISSING M14-006/M6-018: SayText audio cannot enter the native WavePortal float-frame and robot-output path");
        return 0x03000000;
    }

    public override uint CheckIfDone() => EngineActionResult.Running;

    public override void WatcherEnding()
    {
        if (_retired) return;
        _retired = true;
        _speech.CleanupAudioEngine(_operation);
        base.WatcherEnding();
    }
}

