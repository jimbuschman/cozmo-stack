namespace Cozmo.Robot;

// fidelity: M1-044
// Checked T6–T8: interfaces only. A missing higher-layer owner is UNKNOWN,
// distinct from an explicitly bound null owner. No managed reset stands in for it.
internal sealed class RobotLifetime
{
    private readonly EngineRobot _robot;
    private readonly Dictionary<int, Action<uint>?> _owners = new();
    internal RobotLifetime(EngineRobot robot) => _robot = robot;
    internal void Bind(int offset, Action<uint>? release) => _owners[offset] = release;
    internal bool IsKnownNull(int offset) => _owners.TryGetValue(offset, out var value) && value is null;
    private void Missing(uint address, int offset) => _robot.Engine.Log($"MISSING: Robot lifetime owner +0x{offset:X} at 0x{address:X8}");
    private void Clear(int offset)
    {
        _owners[offset] = null;
        if (offset == 0x250) _robot.ClearActionListOwner();
    }
    private void Release(int offset, uint address, bool clearBefore = true)
    {
        bool known = _owners.TryGetValue(offset, out var callback);
        if (clearBefore) Clear(offset);
        if (!known) Missing(address, offset);
        else callback?.Invoke(address);
    }
    private void Member(int offset, uint address)
    {
        if (!_owners.TryGetValue(offset, out var callback) || callback is null) Missing(address, offset);
        else callback(address);
    }
    internal void Destroy()
    {
        Member(-1, 0x005110EE); // destructor event
        Member(-2, 0x0051111A); // FreeplayDataTracker.ForceUpdate
        Member(-3, 0x00511120); // AbortAll, result ignored
        Release(0x44, 0x0051112C);
        Release(0x48, 0x0051113A);
        _robot.ActionList.Clear(); // unconditional call at 0x00511146
        Release(0x250, 0x00511156);
        Release(0x258, 0x0051116C);
        Release(0x390, 0x00511180);
        Release(0x440, 0x005111B6, false);
        Clear(0x440); // 0x005111C4, before progression's release
        Release(0x448, 0x005111CE, false);
        Clear(0x448); // 0x005111EE
        Release(0x450, 0x005111F8, false);
        Clear(0x450); // 0x00511220
        Release(0x44C, 0x0051122A, false);
        Clear(0x44C); // 0x00511250
        Release(0x26C, 0x00511260);
        Release(0x264, 0x00511276);
        Release(0x5C, 0x00511284);
        Release(0x3C, 0x00511298);
        Release(0x38, 0x005112AC);
        Release(0x34, 0x005112CE);
        Release(0x260, 0x005112E4);
        Release(0x51C, 0x005112F2);
        Release(0x518, 0x00511304);
        Member(0x47C, 0x00511310);
        Release(0x444, 0x00511328);
        Member(0x394, 0x0051133A); // history buffer's internal predicates stay M11
        Release(0x390, 0x00511362); // normal earlier null skips this release
        Member(0x2F0, 0x00511396);
        Member(0x2E4, 0x0051139E);
        Member(0x2D8, 0x005113A6);
        Member(0x2CC, 0x005113AE);
        Member(0x2A4, 0x005113B6);
        Member(0x298, 0x005113BE);
        Release(0x294, 0x005113CC);
        Release(0x28C, 0x005113E0);
        Release(0x288, 0x005113F2);
        Release(0x284, 0x00511406); // raw delete, not an invented second abort
        Release(0x280, 0x00511414);
        Release(0x27C, 0x00511424);
        Release(0x278, 0x00511436);
        Release(0x274, 0x00511444);
        Release(0x270, 0x00511478);
        Release(0x26C, 0x0051149C);
        Release(0x268, 0x005114B0);
        Release(0x264, 0x005114C4);
        Release(0x260, 0x005114DA);
        Release(0x25C, 0x005114E8);
        Release(0x258, 0x005114FE);
        Release(0x254, 0x00511510);
        Release(0x250, 0x0051151C);
        Release(0x24C, 0x00511534);
        Member(0x60, 0x0051154A); // embedded AnimationStreamer: unconditional
        Release(0x5C, 0x00511554);
        Release(0x58, 0x00511568);
        Member(0x4C, 0x0051156A); // string/storage predicate is an owned member interface
        Release(0x48, 0x00511580);
        Release(0x44, 0x0051158E);
        Release(0x40, 0x0051159E);
        Release(0x3C, 0x005115B0);
        Release(0x38, 0x005115C6);
        Release(0x34, 0x005115E6);
        Member(4, 0x005115F0); // final base/member destructor
    }
}

public sealed partial class EngineRobot
{
    internal RobotLifetime Lifetime { get; private set; } = null!;
    private ActionList? _actionList;
    internal ActionList? ActionListOwner => _actionList;
    internal void ClearActionListOwner() => _actionList = null;
    internal void InitLifetime()
    {
        Lifetime = new(this);
        var actions = ActionList;
        Lifetime.Bind(0x250, _ => actions.Dispose());
        InitSubscriptions();
        // fidelity: M4-026, M4-027
        // The host binds the Robot components it carries (AbortAll, the movement, light, CubeAccel and tap deletions).
        Engine.BindRobotLifetime?.Invoke(this);
    }
}

public sealed partial class CozmoEngine
{
    // The M1 interface calls are ordered; recipient semantics belong to the split
    // higher-layer records. Missing recipients are visible instead of silent no-ops.
    // fidelity: M1-050
    // The live slot +0x30 is UiMessageHandler::OnRobotDisconnected (ARM_ABS32
    // 0x0102FE4C). It only exits active SDK mode, which this stack does not support.
    // This nullable internal observer records the call position in tests; it is not an API event.
    internal Action<uint>? ExternalRobotDisconnectCallObserved;
    internal Action? NeedsRobotDisconnected, PerfRobotDisconnected;
    internal Action<bool>? DasPauseUploading;
    internal Action<string>? ClearDasGlobal;
    internal Action<uint>? RobotStorageFreed;
    internal Func<EngineRobot, IActionRunner?>? CreateGoToSleepSequence;
    // fidelity: M4-026, M4-027
    /// <summary>Called by <c>EngineRobot.InitLifetime</c> for each Robot built: the host binds its components' Lifetime slots.</summary>
    internal Action<EngineRobot>? BindRobotLifetime;
    private void Required(Action? action, string missing)
    {
        if (action is null) Log($"MISSING: {missing}");
        else action();
    }
    // fidelity: M1-050
    internal void NotifyExternalRobotDisconnected(uint id) => ExternalRobotDisconnectCallObserved?.Invoke(id);
    internal void ClearGlobal(string name) => Required(
        ClearDasGlobal is { } callback ? () => callback(name) : null, $"DAS clear {name}");
    internal void NotifyDisconnectServices()
    {
        Required(NeedsRobotDisconnected, "NeedsManager.OnRobotDisconnected 0x0052F2E0");
        Required(PerfRobotDisconnected, "PerfMetric.OnRobotDisconnected 0x0052F2E8");
        Required(DasPauseUploading is { } das ? () => das(false) : null, "DASPauseUploadingToServer(0) 0x0052F2EE");
    }
    internal void DestroyRobot(EngineRobot robot)
    {
        robot.Lifetime.Destroy();
        RobotStorageFreed?.Invoke(RobotId); // managed object lifetime replaces native operator delete storage release.
    }
}
