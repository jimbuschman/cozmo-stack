using Cozmo.Robot.Behavior;
using Cozmo.Protocol;

namespace Cozmo.Robot;

/// <summary>Operator-approved extensions beyond the original engine. Native conformance callers
/// default to Baseline; the MCP executable opts into McpDefaults unless --baseline is supplied.</summary>
public sealed record CozmoC2Settings
{
    public const string CliffHoldStillName = "C2:cliff-hold-still";
    public bool CliffHoldStill { get; init; }
    public static CozmoC2Settings Baseline { get; } = new();
    public static CozmoC2Settings McpDefaults { get; } = new() { CliffHoldStill = true };
    public IReadOnlyList<string> ActiveExtensions => CliffHoldStill ? [CliffHoldStillName] : [];
}

public sealed partial class EngineRobot
{
    // C2:cliff-hold-still is a host safety extension, not native behavior.
    // Keep the latch on the Robot lifetime so a replacement does not inherit it.
    private volatile bool _c2CliffHoldActive;
    private bool _c2CliffDetected, _c2CliffDecisionPending, _c2CliffReactionStarted;
    private long _c2CliffEpisode;
    internal long C2CliffReactionStarted()
    {
        if (C2Current && _c2CliffHoldActive) _c2CliffReactionStarted = true;
        return _c2CliffEpisode;
    }
    public bool C2CliffHoldActive => _c2CliffHoldActive;

    // Native STOP handler 0x0053542E..80: the same enabled-cliff branch ends the
    // current behavior, cancels actions and raises RobotStopped. Hold before those
    // callbacks can start a new animation. The C0 event supplies the actual flag.
    internal void C2ObserveRobotStopped(CozmoRobot host)
    {
        if (!C2Current) return;
        // The next stop may interrupt an older reaction; its stop callback must
        // not release a hold belonging to this newer episode.
        _c2CliffEpisode++;
        _c2CliffReactionStarted = false;
        _c2CliffDecisionPending = true;
        UpdateCliffHold(host, "RobotStopped while cliff detection enabled");
    }

    // 0x00535548..0x005356E2: accept CliffEvent and update detected-by-event.
    // A clear report during an active cliff reaction does not release until that
    // reaction has made its native retreat/finish decision.
    internal void C2ObserveCliffEvent(CozmoRobot host, bool detected)
    {
        if (!C2Current) return;
        if (detected && !_c2CliffHoldActive)
        {
            _c2CliffEpisode++;
            _c2CliffReactionStarted = false;
            _c2CliffDecisionPending = true;
            // A direct request could have started this reaction before a cliff report.
            if (BehaviorManagerOwner?.Current is ReactToCliffBehavior reaction)
            {
                _c2CliffReactionStarted = true;
                reaction.AdoptC2CliffEpisode(_c2CliffEpisode);
            }
        }
        _c2CliffDetected = detected;
        if (!detected && !_c2CliffReactionStarted && BehaviorManagerOwner?.Current is not ReactToCliffBehavior)
            _c2CliffDecisionPending = false;
        UpdateCliffHold(host, detected ? "CliffEvent detected" : "CliffEvent undetected");
    }

    // 0x00605318..0x006053A0: native back-up selection. Do not lock the
    // action-arbitration body track: its DriveStraight(-60,100,true) must run.
    // On native stop/quit, the pending reaction has ended without a retreat.
    internal void C2CliffReactionDecision(CozmoRobot host, long episode, string reason)
    {
        if (!C2Current || episode != _c2CliffEpisode) return;
        _c2CliffDecisionPending = false;
        _c2CliffReactionStarted = false;
        UpdateCliffHold(host, reason);
    }

    private bool C2Current => Engine.Options.C2.CliffHoldStill && !Lifetime.Destroying
        && ReferenceEquals(Engine.Robot, this);

    private void UpdateCliffHold(CozmoRobot host, string reason)
    {
        bool hold = _c2CliffDetected || _c2CliffDecisionPending;
        if (_c2CliffHoldActive == hold) return;
        _c2CliffHoldActive = hold;
        // 0x0057CA48..7A: body and recorded-heading turn enter the streamer FIFO.
        // Drop only unsent animation motion; do not disturb audio/face/lift/head.
        int discarded = hold ? host.Animations.Scheduler.Stream.DiscardAnimationBodyMotion() : 0;
        // 0x006400D0..0x00640188 / 0x0063FF96: firmware animation track masks.
        // This separate overlay protects motion already streamed to the device,
        // without taking a native action lock that would reject the backup.
        bool sent = host.Motion.SetC2CliffAnimationTrack(this, hold);
        Engine.EmitStackLog($"{CozmoC2Settings.CliffHoldStillName}: {(hold ? "holding" : "released")} animation body; {reason}; discarded {discarded} buffered motion messages; firmware mask {(sent ? "applied or already locked" : "send failed")}");
    }
}
