using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

public sealed record HostNeedsStatus(bool KeepFull, bool Loaded, bool Applied, bool Paused, float Repair, float Energy, float Play);

// fidelity: M15-112
public sealed partial class CozmoEngine
{
    private bool _keepNeedsFull, _hostNeedsApplied, _hostOwnsNeedsPause;
    private bool _hostNeedsModeReported;
    private long _hostNeedsGeneration;
    private EngineRobot? _hostNeedsRobot;

    public HostNeedsStatus GetNeedsStatus() => InvokeEngineRequest(ReadHostNeedsStatus);

    /// <summary>Supply the app's refill/pause input, or resume native needs. Runs on the existing engine owner.</summary>
    public HostNeedsStatus SetNeedsMode(bool keepFull) => InvokeEngineRequest(() =>
    {
        if (_keepNeedsFull == keepFull) return ReadHostNeedsStatus();
        _keepNeedsFull = keepFull;
        _hostNeedsGeneration++;
        _hostNeedsApplied = false;
        _hostNeedsModeReported = false;
        if (!keepFull && _hostOwnsNeedsPause)
        {
            _hostOwnsNeedsPause = false;
            AnimationMessages.Deliver(0xC9, false, false);
        }
        UpdateHostNeeds();
        return ReadHostNeedsStatus();
    });

    /// <summary>Force the three levels through G2E C6 in real mode; native clamping and reporting are retained.</summary>
    public HostNeedsStatus SetNeedsLevels(float repair, float energy, float play) => InvokeEngineRequest(() =>
    {
        if (!float.IsFinite(repair) || !float.IsFinite(energy) || !float.IsFinite(play))
            throw new ArgumentException("Needs levels must be finite.");
        if (_keepNeedsFull) throw new InvalidOperationException("Select real needs mode before setting individual levels.");
        var needs = Needs;
        if (Robot is not { Lifetime.Destroying: false } owner || !needs.NativeLevelsLoaded)
            throw new InvalidOperationException("Stored needs are not loaded for this connection.");
        // The synchronous C6 recipient stores the three clamped levels and publishes
        // NeedsState(NoAction) at 0x00697DA4..0x00697EC6, even when decay is paused.
        AnimationMessages.Deliver(0xC6, false, new[] { repair, energy, play });
        return ReadHostNeedsStatus() with
        {
            Applied = ReferenceEquals(Robot, owner) && !owner.Lifetime.Destroying &&
                ReferenceEquals(Needs, needs) && needs.NativeLevelsLoaded
        };
    });

    private HostNeedsStatus ReadHostNeedsStatus()
    {
        bool loaded = Needs.NativeLevelsLoaded;
        bool paused = Needs.IsPaused; // C9 recipient SetPaused at 0x00695E04.
        bool applied = loaded && Robot is { Lifetime.Destroying: false } &&
            (_keepNeedsFull ? _hostNeedsApplied && ReferenceEquals(_hostNeedsRobot, Robot) && paused : !paused);
        return new(_keepNeedsFull, loaded, applied, paused, (float)Needs.State.GetNeedLevel(NeedId.Repair),
            (float)Needs.State.GetNeedLevel(NeedId.Energy), (float)Needs.State.GetNeedLevel(NeedId.Play));
    }

    private void UpdateHostNeeds()
    {
        if (!_hostNeedsModeReported)
        {
            _hostNeedsModeReported = true;
            EmitStackLog($"Host needs mode: {(_keepNeedsFull ? "full" : "real")} (M15-112)");
        }
        if (!_keepNeedsFull) return;
        var robot = Robot;
        if (robot is null || !Needs.NativeLevelsLoaded)
        {
            _hostNeedsApplied = false;
            _hostNeedsRobot = robot;
            return;
        }
        var needs = Needs;
        long generation = _hostNeedsGeneration;
        bool Current() => _keepNeedsFull && generation == _hostNeedsGeneration && ReferenceEquals(Needs, needs)
            && ReferenceEquals(Robot, robot) && needs.NativeLevelsLoaded;
        if (!_hostNeedsApplied || !ReferenceEquals(_hostNeedsRobot, robot))
        {
            _hostNeedsRobot = robot;
            // Operator policy, 2026-11-17. Existing handler logs, clamps, marks dirty and sends NoAction
            // at 0x00697DA4..0x00697EC6; it does not write robot NV.
            AnimationMessages.Deliver(0xC6, false, new[] { 1f, 1f, 1f });
            if (!Current()) return;
        }
        if (!needs.IsPaused)
        {
            _hostOwnsNeedsPause = true;
            // C9's global pause skips Update's decay/notifications/device-write tail (0x00695CA4..0x00695CFA)
            // and action deltas/rewards (0x006960E0..0x006960EE), so no periodic refill is necessary.
            // SetPaused(true) itself writes the PC copy (0x00695ECA -> 0x00693BB0), never robot NV.
            AnimationMessages.Deliver(0xC9, false, true);
            if (!Current()) return;
        }
        _hostNeedsApplied = true;
    }
}
