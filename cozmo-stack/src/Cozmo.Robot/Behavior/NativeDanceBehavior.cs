using System.Numerics;
using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

internal interface INativeDanceListener { void OnDanceAnimationComplete(EngineRobot robot); }

// fidelity: M15-090
// DANCE-MAMBO-1 D1-D12: 0x005ED438..0x005EDFCA; shared owners retain their gaps.
internal sealed class NativeDanceBehavior : SteppedBehavior, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly List<AnimationTrigger> _triggers = new();
    private readonly Dictionary<uint, uint> _choices = new();
    private readonly SortedDictionary<uint, WeakReference<INativeDanceListener>> _listeners = new();
    internal void AddNativeListener(uint pointerIdentity, INativeDanceListener listener) => _listeners.TryAdd(pointerIdentity, new WeakReference<INativeDanceListener>(listener));
    private readonly int _loops;
    private int _loopIndex, _retired;
    private long _generation;
    internal IReadOnlyDictionary<uint,uint> Choices => _choices;
    internal Action<uint,uint,int,Action>? LightPlayObservedForTest;

    internal NativeDanceBehavior(BehaviorFactoryContext source, JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(), "Dance")
    {
        _m = source.Manipulation!; _robot = source.NativeRobot!;
        if (config.TryGetProperty("animTriggers", out var triggers) && triggers.ValueKind == JsonValueKind.Array)
            foreach (var value in triggers.EnumerateArray())
            {
                string? name = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (name == "Count") continue;
                if (name is not null && Enum.TryParse<AnimationTrigger>(name, out var parsed) && Enum.IsDefined(parsed)) _triggers.Add(parsed);
                else { BehaviorClassNames.WriteCerrError(name ?? "", "AnimationTrigger"); _triggers.Add((AnimationTrigger)0); }
            }
        _loops = config.TryGetProperty("num_loops", out var loops) && loops.ValueKind == JsonValueKind.Number ? loops.GetInt32() : 1;
    }
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => _m.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion() => Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version) => Volatile.Read(ref _retired) == 0 && Interlocked.Read(ref _generation) == version;
    protected override bool HostStopContinuationCurrent(long version) => Volatile.Read(ref _retired) == 0 && Interlocked.Read(ref _generation) == unchecked(version + 1);
    protected override bool HostResumeContinuationCurrent(long version) => Volatile.Read(ref _retired) == 0 && (Interlocked.Read(ref _generation) == version || Interlocked.Read(ref _generation) == unchecked(version + 1));
    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => true;
    protected override bool IsRunnableInternal(BehaviorContext context) => _triggers.Count != 0;
    protected override bool KeepsRunningWithoutAction => HasCurrentAction;
    protected override int ResumeInternal() => 1;
    private bool Current(long epoch) => HostExecutionVersionCurrent(epoch);
    private List<ObservableObject> Connected()
    {
        var filter = new BlockWorldFilter(); filter.AllowedFamilies.Add(ObjectFamily.LightCube);
        var result = new List<ObservableObject>(); _m.World.FindConnectedObjectHelper(filter, result.Add, false); return result;
    }
    private static CubeLightModifier Zero() => new(new LedPattern[4], 0, 0, Vector2.Zero);
    private uint Select(uint candidate)
    {
        var used = new HashSet<uint>(_choices.Values);
        for (int attempt = 1; used.Contains(candidate); attempt++)
        {
            candidate = unchecked((uint)(4 + _robot.Engine.ContextRandom.RandInt(6)));
            if (attempt == 100) { ReportMissing("BehaviorDance.GetRandomAnimTrigger 0x005ED8B4: 100 attempts exhausted"); break; }
        }
        return candidate;
    }
    private void Play(uint id, uint trigger, int rawOffset, Action callback, Func<bool>? current)
    {
        LightPlayObservedForTest?.Invoke(id, trigger, rawOffset, callback);
        if (current is not null && !current()) return;
        string? name = NativeCubeLightTriggers.Name(trigger);
        if (name is not null) _m.Robot.Lights.Cubes.PlayLightAnimForObjectId(id, name, CubeLightComponent.EngineLayer, false, Zero(), rawOffset, current, callback);
    }
    private void CubeComplete(uint id)
    {
        // The native entry checks only the current active byte, even for an old callback after re-init.
        if (Volatile.Read(ref _retired) != 0) { ReportMissing("M1-034 Dance callback 0x005EDC46: expired behavior owner; skip callback"); return; }
        if (!EngineRunning) return;
        long epoch = CaptureHostExecutionVersion();
        if (!_choices.TryGetValue(id, out uint old)) _choices[id] = old = 0;
        uint next = Select(old);
        Play(id, next, 0, () => CubeComplete(id), () => Current(epoch));
        if (Current(epoch)) _choices[id] = next;
    }
    protected override void OnStart()
    {
        long epoch = CaptureHostExecutionVersion();
        ReportMissing("DANCE-MAMBO-1: actual Dance_Mambo request/voice caller is UNKNOWN; supplied selection does not establish automatic admission");
        if (!Current(epoch)) return;
        if (!Scope.SmartDisableReactionsWithLockGuarded(Id, ReactionLockTables.Dance, () => Current(epoch))) return;
        var cubes = Connected(); if (!Current(epoch)) return;
        int offset = 0;
        foreach (var cube in cubes)
        {
            uint id = cube.ObjectId; _choices[id] = 0x28;
            uint selected = Select(0x28);
            Play(id, selected, offset, () => CubeComplete(id), () => Current(epoch));
            if (!Current(epoch)) return;
            _choices[id] = selected; offset = unchecked(offset - 0x148);
        }
        if (_triggers.Count == 1) { StartActing(Action(_triggers[0], _loops), _ => Listeners(), () => Current(epoch)); return; }
        _loopIndex = 0; NextLoop(epoch);
    }
    private TriggerLiftSafeAnimationAction Action(AnimationTrigger trigger, int loops) => new(_m.Robot, new(trigger, loops, true, 0, 60f, false));
    private void Listeners()
    {
        // 0x005C0254..292: unsigned native pointer order, successor read after callback.
        ReportMissing("M8-005 BehaviorDance: actual native listener producer and pointer identity are unavailable");
        if (Volatile.Read(ref _retired) != 0) return;
        uint? previous = null;
        while (true)
        {
            var next = _listeners.FirstOrDefault(pair => previous is null || pair.Key > previous.Value);
            if (next.Value is null) return;
            previous = next.Key;
            if (!next.Value.TryGetTarget(out var listener))
            { ReportMissing("M1-034 M8-005 listener 0x005C0270: expired listener owner; skip callback"); if (Volatile.Read(ref _retired) != 0) return; continue; }
            listener.OnDanceAnimationComplete(_robot);
            if (Volatile.Read(ref _retired) != 0) return;
        }
    }
    private void NextLoop(long epoch)
    {
        if (!Current(epoch) || _loopIndex >= _loops) return;
        var sequence = new CompoundActionSequential(() => _robot.Engine.Timer.SecondsF);
        foreach (var trigger in _triggers) sequence.AddAction(Action(trigger, 1), false, false);
        _loopIndex = unchecked(_loopIndex + 1);
        StartActing(sequence, _ => { Listeners(); if (Current(epoch)) NextLoop(epoch); }, () => Current(epoch));
    }
    protected override void OnStop(BehaviorStopReason reason)
    {
        long stopped = Interlocked.Increment(ref _generation);
        bool CurrentStop() => Current(stopped);
        _robot.BehaviorPublicState.UpdateBroadcastBehaviorStage(4, 0, CurrentStop);
        if (!CurrentStop()) return;
        var cubes = Connected(); if (!CurrentStop()) return;
        var lights = _m.Robot.Lights.Cubes;
        foreach (var cube in cubes)
        {
            Play(cube.ObjectId, 10, 0, FadeCompletion(lights), CurrentStop);
            if (!CurrentStop()) return;
        }
    }
    private static Action FadeCompletion(CubeLightComponent lights) => () => lights.StopAllAnimsOnLayer(CubeLightComponent.EngineLayer, null);
    public void Dispose() { if (Interlocked.Exchange(ref _retired, 1) != 0) return; Interlocked.Increment(ref _generation); _listeners.Clear(); RetireActionCompletionOwner(); }
}
