using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// A supplier represents the native weak pair and its lock operation. It must not retain a
// managed snapshot or infer expiry from GC. Production has no supplier until that join is read.
internal interface ITallStackWeakTarget
{
    bool TryLock(out (uint Bottom, int Height)? value);
}

// Proposed lifecycle owner; M15-012 owns only the disappointment trigger/update slice.
internal sealed class NativeCantHandleTallStackBehavior : SteppedBehavior, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly TallStackDelocalizationData? _constructorState;
    private TallStackDelocalizationData? _state;
    private readonly float _initial, _down, _up, _moved;
    private readonly byte _minimum;
    private long _generation;
    private int _retired;
    internal CompoundActionSequential? LookingSequence { get; private set; }
    internal TallStackDelocalizationData? State => _state;
    internal (float Initial, float Down, float Up, float Moved, byte Minimum) Config => (_initial, _down, _up, _moved, _minimum);
    internal NativeCantHandleTallStackBehavior(BehaviorFactoryContext source, JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(), "CantHandleTallStack")
    {
        _m = source.Manipulation!; _robot = source.NativeRobot!;
        _state = source.TallStackData;
        _constructorState = source.TallStackData;
        _initial = Float(config, "lookingInitialWait_s");
        _down = Float(config, "lookingDownWait_s");
        _up = Float(config, "lookingUpWait_s");
        _moved = Float(config, "minBlockMovedThreshold_mm");
        _minimum = unchecked((byte)(config.TryGetProperty("minimumStackHeight", out var minimum) ? minimum.GetInt32() : 3));
    }
    private static float Float(JsonElement config, string name) => config.TryGetProperty(name, out var value) ? value.GetSingle() : 3f;
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => _m.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion() => Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version) => Volatile.Read(ref _retired) == 0 && Interlocked.Read(ref _generation) == version;
    protected override bool HostStopContinuationCurrent(long version) => Volatile.Read(ref _retired) == 0 && Interlocked.Read(ref _generation) == unchecked(version + 1);
    protected override bool HostResumeContinuationCurrent(long version) => Volatile.Read(ref _retired) == 0 && (Interlocked.Read(ref _generation) == version || Interlocked.Read(ref _generation) == unchecked(version + 1));
    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => false;
    protected override bool KeepsRunningWithoutAction => HasCurrentAction;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner = EnterHostExecutionOwner(context);
        long generation = CaptureHostExecutionVersion();
        if (!HostExecutionVersionCurrent(generation) || !IsRunnableBase(context) || !HostExecutionVersionCurrent(generation)) return false;
        return RunnableCore(generation);
    }
    protected override bool IsRunnableInternal(BehaviorContext context) => RunnableCore(CaptureHostExecutionVersion());
    private TallStackDelocalizationData? ResolveState()
    {
        var supplied = _constructorState ?? _robot.NativeBehaviorRuntime?.TallStackOwnerInput;
        if (supplied is null || (_state is not null && !ReferenceEquals(_state, supplied)))
        { MarkReadinessUnavailable(); ReportMissing("CantHandleTallStack configured owner to delocalization recipient join unavailable or changed"); return null; }
        return _state ??= supplied;
    }
    private bool TryLock(ITallStackWeakTarget? target, long generation, out (uint Bottom, int Height)? value)
    {
        if (target is null) { value = null; return true; }
        bool available = target.TryLock(out value);
        if (!HostExecutionVersionCurrent(generation)) return false;
        if (available) return true;
        MarkReadinessUnavailable(); ReportMissing("CantHandleTallStack selected weak lock unavailable at 005ECD8C/005ECE48"); return false;
    }
    private bool RunnableCore(long generation)
    {
        if (_robot.Progression is not {} progression || !progression.TryIsUnlocked(5, out bool unlocked))
        {
            MarkReadinessUnavailable(); ReportMissing("CantHandleTallStack unlock5 input unavailable at 005ECD74"); return false;
        }
        if (unlocked) return false; // 005ECD78: do not refresh target.
        var state = ResolveState();
        if (state is null) return false;
        var supplier = _robot.NativeBehaviorRuntime?.TallStackWeakInput;
        if (supplier is null)
        {
            state.TargetKnown = false;
            MarkReadinessUnavailable(); ReportMissing("CantHandleTallStack canonical weak-lock and last-strong-owner input unavailable at 005ED200; no GC or snapshot expiry substituted"); return false;
        }
        using var mutation = _m.World.EnterPoseMutation();
        var target = supplier();
        if (!HostExecutionVersionCurrent(generation)) return false;
        state.Target = target;
        state.TargetKnown = true;
        if (!TryLock(target, generation, out var locked)) return false;
        if (locked is not {} stack || stack.Height < _minimum) return false;
        var bottom = _m.World.GetLocatedObjectByIdHelper(stack.Bottom);
        if (bottom is null) return false;
        if (!state.HasStoredPose) return true;
        if (state.StoredPose is not {} pose)
        { MarkReadinessUnavailable(); ReportMissing("CantHandleTallStack stored bottom pose unavailable despite pose flag"); return false; }
        return !pose.IsSameAs(bottom.NativePose, new Vec3(_moved, _moved, 10f), BitConverter.UInt32BitsToSingle(0x40490FDB), out _);
    }
    protected override void OnStart()
    {
        long generation = CaptureHostExecutionVersion();
        using var mutation = _m.World.EnterPoseMutation();
        var state = ResolveState();
        if (state is null || !state.TargetKnown)
        { MarkReadinessUnavailable(); ReportMissing("CantHandleTallStack Init has no supplied canonical weak target; host admission unavailable"); InitFailed = true; return; }
        if (!TryLock(state.Target, generation, out var locked))
        { if (HostExecutionVersionCurrent(generation)) InitFailed = true; return; }
        if (locked is not {} stack || _m.World.GetLocatedObjectByIdHelper(stack.Bottom) is not {} bottom)
        { InitFailed = true; return; }
        state.StoredPose?.Dispose();
        state.StoredPose = new Pose3d(bottom.NativePose);
        state.HasStoredPose = true;
        if (!SetStateName("DebugState::LookingUpAndDown", () => HostExecutionVersionCurrent(generation))) return;
        if (!HostExecutionVersionCurrent(generation)) return;
        var sequence = new CompoundActionSequential(() => _robot.Engine.Timer.SecondsF);
        sequence.SeedNativeEmptyCompletion();
        // 00547F14..28 initializes speed 15, acceleration 20, duration 0; the
        // caller's third Radians is variability 0 (005480B0..C2 skips its RNG).
        sequence.AddAction(new NativeTimedWaitRunner(_m, _initial), false, false);
        sequence.AddAction(_m.Robot.Motion.CreateNativeHeadChild(BitConverter.UInt32BitsToSingle(0xBEDF66F3), BitConverter.UInt32BitsToSingle(0x3D0EFA35), 15f, 20f), false, false);
        sequence.AddAction(new NativeTimedWaitRunner(_m, _down), false, false);
        sequence.AddAction(_m.Robot.Motion.CreateNativeHeadChild(BitConverter.UInt32BitsToSingle(0x3F490FDB), BitConverter.UInt32BitsToSingle(0x3D0EFA35), 15f, 20f), false, false);
        sequence.AddAction(new NativeTimedWaitRunner(_m, _up), false, false);
        LookingSequence = sequence;
        StartActing(sequence, _ => Disappointment(generation), () => HostExecutionVersionCurrent(generation));
    }
    // fidelity: M15-012 — 005ED0F0..184, the narrow disappointment trigger.
    private void Disappointment(long generation)
    {
        if (!HostExecutionVersionCurrent(generation)) return;
        if (!SetStateName("DebugState::Disapiontment", () => HostExecutionVersionCurrent(generation))) return;
        if (!HostExecutionVersionCurrent(generation)) return;
        StartActing(new TriggerAnimationAction(_m.Robot, new(AnimationTrigger.CantHandleTallStack, 1, true, 0, 60f, false)), null, () => HostExecutionVersionCurrent(generation));
    }
    protected override void OnStop(BehaviorStopReason reason)
    {
        Interlocked.Increment(ref _generation);
        _state?.ClearStack(); // 005ED064: pose flag and pose survive.
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _retired, 1) != 0) return;
        Interlocked.Increment(ref _generation);
        _state?.ClearStack();
        if (_state is {} state) { state.StoredPose?.Dispose(); state.StoredPose = null; }
        RetireActionCompletionOwner();
    }
}
