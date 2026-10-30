using System.Text.Json;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// Configured BehaviorCheckForStackAtInterval; shared action/geometry frontiers remain MISSING.
internal sealed class NativeCheckForStackAtIntervalBehavior : SteppedBehavior, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly float _delay;
    private readonly List<uint> _targets = new();
    private uint _ghostId = uint.MaxValue;
    private int _index = -1;
    private float _deadline;
    private long _generation;
    private int _retired;
    private Pose3d? _savedPose;
    // Supplied-child test seam only. Production has no checked custom ghost marker input.
    internal Func<uint, Pose3d, IActionRunner>? SuppliedGhostChildForTest;
    private readonly List<Pose3d> _ghostPoses = new();

    internal IReadOnlyList<uint> Targets => _targets;
    internal uint GhostId => _ghostId;
    internal float Deadline => _deadline;
    internal int Index => _index;

    internal NativeCheckForStackAtIntervalBehavior(BehaviorFactoryContext source, JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(), "CheckForStackAtInterval")
    {
        _m = source.Manipulation!;
        _robot = source.NativeRobot!;
        _delay = config.TryGetProperty("delayBetweenChecks", out var delay) ? delay.GetSingle() : 15f;
    }

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
        return HostExecutionVersionCurrent(generation) && IsRunnableBase(context) && HostExecutionVersionCurrent(generation) && RunnableCore(generation);
    }
    protected override bool IsRunnableInternal(BehaviorContext context) => RunnableCore(CaptureHostExecutionVersion());
    private bool RunnableCore(long generation)
    {
        // 005D732E: ordered f32 comparison; equality and NaN reject.
        if (!IsDue(_robot.Engine.Timer.SecondsF, _deadline)) return false;
        using var mutation = _m.World.EnterPoseMutation();
        var filter = new BlockWorldFilter();
        filter.AllowedFamilies.Add(ObjectFamily.Block);
        filter.AllowedFamilies.Add(ObjectFamily.LightCube);
        var objects = _m.World.FindLocatedMatchingObjects(filter);
        if (!HostExecutionVersionCurrent(generation)) return false;
        foreach (var obj in objects) _targets.Add(obj.ObjectId); // 005D79F0: append, including duplicates.
        return _targets.Count != 0;
    }
    internal static bool IsDue(float now, float deadline) => now > deadline;

    protected override void OnStart()
    {
        long generation = CaptureHostExecutionVersion();
        if (_ghostId == uint.MaxValue) _ghostId = ObjectIdSpace.Set(); // 005D736A; even an empty Init consumes an ID.
        if (_targets.Count == 0) { InitFailed = true; return; }
        _savedPose?.Dispose();
        _savedPose = new Pose3d(_robot.GetCheckedMainPose());
        _index = 0;
        Face(generation);
    }

    private void Face(long generation)
    {
        if (!HostExecutionVersionCurrent(generation)) return;
        var obj = CurrentObject();
        if (obj is null) { Above(generation); return; }
        var action = new NativeTurnTowardsObjectRunner(_m, obj.ObjectId, NativeTurnTowardsObjectRunner.AnyCode, Radians.Pi, false, false);
        StartActing(action, _ => Above(generation), () => HostExecutionVersionCurrent(generation));
    }
    private ObservableObject? CurrentObject() => _index >= 0 && _index < _targets.Count ? _m.World.GetLocatedObjectByIdHelper(_targets[_index]) : null;
    private void Above(long generation)
    {
        if (!HostExecutionVersionCurrent(generation)) return;
        if (CurrentObject() is null)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error, null, "BehaviorCheckForStackAtInterval.TransitionToCheckingAboveBlock.NullObject", $"Target index {_index} is unavailable");
            return; // 005D7618: no action and no deadline.
        }
        var supplied = SuppliedGhostChildForTest;
        if (supplied is null)
        {
            Unavailable(generation, "M12-033 custom ghost marker input / TurnTowardsObjectAction::UseCustomObject 0054A050 is unavailable");
            return;
        }
        using var mutation = _m.World.EnterPoseMutation();
        var obj = CurrentObject();
        if (obj is null) { Above(generation); return; }
        try
        {
            using var flat = obj.NativePose.GetWithRespectToRoot();
            float height = (float)NativeConfigurationGeometry.Dimensions(obj, flat).Z;
            var t = flat.Translation;
            using var root = obj.NativePose.FindRoot();
            var pose = new Pose3d(flat.Quaternion, new Vec3((float)t.X, (float)t.Y, (float)t.Z + height), parent: root);
            _ghostPoses.Add(pose); // Retained at least through every queued supplied child.
            var action = supplied(_ghostId, pose);
            if (!HostExecutionVersionCurrent(generation)) { if (action is ActionRunner stale) stale.RetireNeverAdmittedTree(); return; }
            StartActing(action, _ => AboveComplete(generation), () => HostExecutionVersionCurrent(generation));
        }
        catch (NativeConfigurationUnavailable error) { Unavailable(generation, error.Message); }
    }
    private void AboveComplete(long generation)
    {
        if (!HostExecutionVersionCurrent(generation)) return;
        _index++;
        if (_index >= _targets.Count) ReturnToSearch(generation);
        else Face(generation);
    }
    private void ReturnToSearch(long generation)
    {
        if (!HostExecutionVersionCurrent(generation) || _savedPose is null) return;
        using var flat = _savedPose.GetWithRespectToRoot();
        var parameters = new PanAndTiltAction { PanAngleRad = (float)flat.AngleAroundZ, HeadAngleRad = 0f, PanIsAbsolute = true, HeadIsAbsolute = false };
        var action = new NativePanTiltRunner(_m, parameters);
        StartActing(action, _ => { if (HostExecutionVersionCurrent(generation)) _deadline = _robot.Engine.Timer.SecondsF + _delay; }, () => HostExecutionVersionCurrent(generation));
    }
    private void Unavailable(long generation, string detail)
    {
        var manager = _robot.BehaviorManagerOwner;
        uint tag = CurrentRunnerTag;
        bool originalOwner = ReferenceEquals(manager?.Current, this);
        ReportMissing("CheckForStackAtInterval: " + detail);
        if (!HostExecutionVersionCurrent(generation) || CurrentRunnerTag != tag) return;
        if (originalOwner && !ReferenceEquals(manager?.Current, this)) return;
        _robot.ActionList.DeferHostCleanup(() =>
        {
            if (!HostExecutionVersionCurrent(generation) || CurrentRunnerTag != tag) return;
            if (originalOwner)
            {
                if (ReferenceEquals(_robot.BehaviorManagerOwner, manager) && ReferenceEquals(manager?.Current, this)) manager!.Stop(BehaviorStopReason.Interrupted, _robot.Engine.Timer.SecondsF);
            }
            else if (!ReferenceEquals(_robot.BehaviorManagerOwner?.Current, this)) Stop(BehaviorStopReason.Interrupted);
        });
    }
    protected override void OnStop(BehaviorStopReason reason)
    {
        Interlocked.Increment(ref _generation);
        _index = -1;
        _targets.Clear();
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _retired, 1) != 0) return;
        Interlocked.Increment(ref _generation);
        _savedPose?.Dispose();
        if (!HasCurrentAction) foreach (var pose in _ghostPoses) pose.Dispose();
        else ReportMissing("M8-023 CheckForStackAtInterval: retained ghost pose final release while action remains owned is unavailable; preserve pose");
        RetireActionCompletionOwner();
    }
}
