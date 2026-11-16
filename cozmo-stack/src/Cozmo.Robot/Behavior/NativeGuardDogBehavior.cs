using System.Globalization;
using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// fidelity: M15-056, M15-067, M11-093
internal sealed class NativeGuardDogBehavior : SteppedBehavior, IDisposable
{
    private enum Phase
    {
        Init, SetupInterrupted, DriveToBlocks, SettleIn, StartSleeping, Sleeping,
        Fakeout, Busted, BlockDisconnected, Timeout, PlayerSuccess, Complete
    }

    private sealed class CubeState
    {
        internal float Score, MaxScore, MoveTime, FlipTime;
        internal bool Moved, Flipped;
        internal uint Light = 40;
        internal byte Axis = 5;
        internal NativeCubeMovementListener? Listener;
        internal uint RadioId;
    }

    private const ushort StartTag = 0x8D, EndTag = 0x8E;
    private readonly EngineRobot _robot;
    private readonly ManipulationSystem _manipulation;
    private readonly FeatureGates? _features;
    private readonly SubscriptionOwners _subscriptions = new();
    private readonly BlockWorldFilter _filter = new();
    private readonly SortedDictionary<int, CubeState> _cubes = new();
    private Phase _state;
    private float _sleepStart, _sleepDuration, _lastActivity;
    private bool _monitoring, _retired;
    private int _moved, _flipped;
    private uint _publicStage = 5;
    private string _result = "None";
    private long _generation;
    private float Now => _robot.Engine.Timer.SecondsF;
    private CozmoRobot Facade => _manipulation.Robot;
    private bool OwnerCurrent => !_retired && !_robot.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot, _robot);

    // 0x0059CCA4 -> 0x005F28DC; active LightCube filter at 0x005F290E/0x005F57C2.
    internal NativeGuardDogBehavior(BehaviorFactoryContext source)
        : base("GuardDog", "GuardDog")
    {
        _robot = source.NativeRobot!;
        _manipulation = source.Manipulation!;
        _features = source.Features;
        _filter.AllowedFamilies.Add(ObjectFamily.LightCube);
        _filter.Predicates.Add(obj => obj.ActiveId > -1);
        if (!_robot.HasExternalInterface) return;

        // 0x005F2946, table 0x00C6FF00. The concrete handlers run only with A1 set (0x005BF268..0x005BF27C).
        Subscribe<ObjectConnectionStateMessage>(0x0D, Connection);
        Subscribe<ObjectMoved>(0x0F, moved => UnexpectedMovement(moved.ObjectID, "HandleObjectMoved"));
        Subscribe<CubeUpAxisEvent>(0x11, UpAxisChanged);
    }

    private void Subscribe<T>(ushort tag, Action<T> receive)
    {
        _subscriptions.Add(_robot.Engine.AnimationMessages.SubscribeMessage(tag, true, payload =>
        {
            using var owner = _robot.Engine.EnterSynchronousOwner();
            if (OwnerCurrent && EngineRunning && payload is T typed) receive(typed);
        }));
    }

    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => _robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => ++_generation;
    protected override long CaptureHostExecutionVersion() => _generation;
    protected override bool HostExecutionVersionCurrent(long version) => OwnerCurrent && _generation == version;
    protected override bool HostStopContinuationCurrent(long version) => OwnerCurrent && _generation == version + 1;
    protected override bool KeepsRunningWithoutAction => true;
    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => false;
    protected override int ResumeInternal() => 1; // 0x005F575E.

    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        if (_features?.IsEnabled("GuardDog") != true) return false; // 0x005F2AAA.
        if (_manipulation.Whiteboard.HiccupsActive) return false; // 0x005F2AB8 reads +0x74.
        using var poses = _manipulation.World.EnterPoseMutation();
        var matched = _manipulation.World.FindLocatedMatchingObjects(_filter);
        if (matched.Count != 3) return false; // 0x005F2AE0.
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = -float.MaxValue, maxY = -float.MaxValue;
        foreach (var obj in matched)
        {
            var pose = obj.NativePose;
            if (RotatedParentAxis.GetNativeZ(pose.Rotation) != 3) return false; // 0x005F2B0C.
            float x = (float)pose.Translation.X, y = (float)pose.Translation.Y;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        float spreadX = maxX - minX, spreadY = maxY - minY;
        return (spreadX < spreadY ? spreadY : spreadX) < 100f; // 0x005F2B3E..0x005F2C50.
    }

    protected override void OnStart()
    {
        long version = _generation;
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.GuardDog); // 0x005F2D02, table 0x00C6FF06.
        if (!HostExecutionVersionCurrent(version)) return;
        _cubes.Clear();
        _sleepStart = _sleepDuration = _lastActivity = 0f;
        _monitoring = false;
        _moved = _flipped = 0;
        _publicStage = 5;
        _result = "None";
        foreach (var cube in _manipulation.World.FindLocatedMatchingObjects(_filter))
            _cubes.TryAdd(unchecked((int)cube.ObjectId), new CubeState());
        StopLights(version); // 0x005F2DAE.
        if (HostExecutionVersionCurrent(version)) Transition(Phase.Init);
    }

    private bool Transition(Phase next)
    {
        long version = _generation;
        _state = next;
        return SetStateName(next.ToString(), () => HostExecutionVersionCurrent(version));
    }

    private void InterruptTo(Phase next, long version)
    {
        // 0x005BD34C: the retained cancellation callback may set an intermediate state first.
        StopActing(keepAction: true, viaCallback: false);
        if (HostExecutionVersionCurrent(version)) Transition(next);
    }

    protected override void OnUpdate()
    {
        long version = _generation;
        float now = Now;
        if (_monitoring && _flipped == 3) InterruptTo(Phase.PlayerSuccess, version); // 0x005F2E98.
        else if (_monitoring && _moved == 3) InterruptTo(Phase.Busted, version); // 0x005F2ED4.
        else if (_state == Phase.Sleeping && (now - _sleepStart > 120f ||
            (now - _sleepStart > 60f && now - _lastActivity > 20f))) InterruptTo(Phase.Timeout, version); // 0x005F2F08.
        if (!HostExecutionVersionCurrent(version)) return;

        if (_state is Phase.Sleeping or Phase.Fakeout)
        {
            uint stage = _cubes.Values.Any(cube => cube.Score > 40f) ? 1u : 0u;
            if (stage != _publicStage) SetPublicStage(stage, version); // 0x005F2F94.
        }
        if (!HostExecutionVersionCurrent(version) || HasCurrentAction) return; // 0x005F3002.

        switch (_state)
        {
            case Phase.Init: // 0x005F304E.
                Run(Animation(0xDB), () => Transition(Phase.DriveToBlocks));
                if (HostExecutionVersionCurrent(version)) PlayLights(0x15, version);
                break;
            case Phase.SetupInterrupted: // 0x005F3332.
                Record("SetupInterrupted");
                if (HostExecutionVersionCurrent(version)) Finish();
                break;
            case Phase.DriveToBlocks:
                DriveToBlocks(version);
                break;
            case Phase.SettleIn: // 0x005F31C0, callback 0x005F5C0C.
                Run(Animation(0xDC), () => EnterSleep(version));
                break;
            case Phase.StartSleeping: // 0x005F3216.
                Run(Animation(0xDD, 0, 120f));
                if (HostExecutionVersionCurrent(version)) Transition(Phase.Sleeping);
                break;
            case Phase.Fakeout: // 0x005F3364.
                Run(Animation(0xD8), () => Transition(Phase.StartSleeping));
                break;
            case Phase.Busted: // 0x005F33A8.
                Record("Busted");
                if (!HostExecutionVersionCurrent(version)) return;
                PlayLights(0x13, version);
                if (!HostExecutionVersionCurrent(version)) return;
                Monitor(false, version);
                if (!HostExecutionVersionCurrent(version)) return;
                BroadcastEnd(false);
                if (!HostExecutionVersionCurrent(version)) return;
                Run(Animation(0xD6), () => Transition(Phase.Complete));
                if (HostExecutionVersionCurrent(version)) SetPublicStage(2, version);
                break;
            case Phase.BlockDisconnected: // 0x005F353E.
                Record("BlockDisconnected");
                if (!HostExecutionVersionCurrent(version)) return;
                BroadcastEnd(false);
                if (HostExecutionVersionCurrent(version)) Run(Animation(0xD7), () => Transition(Phase.Complete));
                break;
            case Phase.Timeout:
            case Phase.PlayerSuccess:
                QueueReturn(_state == Phase.PlayerSuccess, version);
                break;
            case Phase.Complete:
                Finish();
                break;
        }
    }

    private TriggerAnimationAction Animation(int trigger, int loops = 1, float timeout = 60f) =>
        new(Facade, new((AnimationTrigger)trigger, loops, true, 0, timeout, false));

    private void Run(ActionRunner action, Action? callback = null)
    {
        long version = _generation;
        StartActing(action, callback is null ? null : _ => callback(), () => HostExecutionVersionCurrent(version));
    }

    private void EnterSleep(long version)
    {
        PlayLights(0x16, version);
        if (!HostExecutionVersionCurrent(version)) return;
        Monitor(true, version);
        if (!HostExecutionVersionCurrent(version)) return;
        _sleepStart = Now;
        if (_robot.HasExternalInterface)
        {
            _robot.Engine.DeliverAnimationReportToApp(StartTag, ReadOnlyMemory<byte>.Empty);
            _robot.Engine.AnimationMessages.Deliver(StartTag, true, null); // empty union tag at 0x007286D6.
        }
        if (HostExecutionVersionCurrent(version)) Transition(Phase.StartSleeping);
    }

    private void BroadcastEnd(bool success)
    {
        if (!_robot.HasExternalInterface) return;
        _robot.Engine.DeliverAnimationReportToApp(EndTag, new[] { success ? (byte)1 : (byte)0 });
        _robot.Engine.AnimationMessages.Deliver(EndTag, true, success); // 0x00728740, one byte.
    }

    private void SetPublicStage(uint stage, long version)
    {
        Info("Unnamed", "GuardDog.UpdatePublicBehaviorStage", $"Updating public behavior stage to {stage}");
        if (!HostExecutionVersionCurrent(version)) return;
        _robot.BehaviorPublicState.UpdateBroadcastBehaviorStage(1, stage, () => HostExecutionVersionCurrent(version));
        if (HostExecutionVersionCurrent(version)) _publicStage = stage; // 0x005F3B84..0x005F3BDE.
    }

    private void Record(string result)
    {
        long version = _generation;
        _result = result;
        _sleepDuration = _sleepStart == 0f ? 0f : Now - _sleepStart;
        string need = result switch
        {
            "PlayerSuccess" => nameof(NeedsActionId.GuardDogWin),
            "TimeoutCubesUntouched" => nameof(NeedsActionId.GuardDogNoInteraction),
            _ => nameof(NeedsActionId.GuardDogLose)
        };
        NeedActionCompleted(need); // 0x005F3CAC..0x005F3DDA.
        if (HostExecutionVersionCurrent(version))
            Info("Behaviors", "GuardDog.RecordResult", FormattableString.Invariant($"GuardDog result = {result}, sleepingDuration = {_sleepDuration:F2}"));
    }

    private void Info(string? channel, string key, string format) => _robot.Engine.EmitSourceLog(EngineLogLevel.Info, channel, key, format);
    private void Warning(string key, string format) => _robot.Engine.EmitSourceLog(EngineLogLevel.Warning, null, key, format);
    // 0x005F4454..0x005F4604. The native stream is acquired and released per connected radio.
    private void Monitor(bool enabled, long version)
    {
        if (enabled == _monitoring) return;
        Info("Unnamed", "BehaviorGuardDog.StartMonitoringCubeMotion",
            $"{(enabled ? "Enabling" : "Disabling")} monitoring of cube accelerometer data.");
        foreach (var (id, data) in _cubes.ToArray())
        {
            if (!HostExecutionVersionCurrent(version)) return;
            if (enabled)
            {
                var connected = _manipulation.World.GetConnectedActiveObjectByIdHelper(unchecked((uint)id));
                if (connected is null || connected.ActiveId < 0)
                {
                    Warning("CubeAccelComponent.AddListener.InvalidObject", $"Object id {id} is not connected");
                    continue;
                }
                data.RadioId = unchecked((uint)connected.ActiveId);
                var listener = new NativeCubeMovementListener(score => Accel(id, score, version));
                data.Listener = listener;
                Facade.CubeAccel.AddMovementListener(data.RadioId, listener);
            }
            else if (data.Listener is { } listener)
            {
                data.Listener = null;
                Facade.CubeAccel.RemoveMovementListener(data.RadioId, listener);
            }
        }
        if (HostExecutionVersionCurrent(version)) _monitoring = enabled;
    }

    private void StopLights(long version) => Facade.Lights.Cubes.StopAllAnimsOnLayer(CubeLightComponent.EngineLayer,
        null, () => HostExecutionVersionCurrent(version));

    // 0x005F3C5C breaks at the first failed light request.
    private void PlayLights(uint trigger, long version)
    {
        foreach (int id in _cubes.Keys.ToArray())
            if (!HostExecutionVersionCurrent(version) || !PlayLight(id, trigger, version)) return;
    }

    private bool PlayLight(int id, uint trigger, long version)
    {
        var cube = _cubes[id];
        Info("Unnamed", "BehaviorGuardDog.StartLightCubeAnim.StartingAnim",
            $"Object {id}: Playing light cube animation {NativeCubeLightTriggers.Name(trigger)} (previous was {NativeCubeLightTriggers.Name(cube.Light)})");
        if (!HostExecutionVersionCurrent(version)) return false;
        uint objectId = unchecked((uint)id);
        var lights = Facade.Lights.Cubes;
        var modifier = BehaviorStateLightInfo.Zero();
        bool Current() => HostExecutionVersionCurrent(version);
        bool played = cube.Light == 40
            ? lights.PlayBehaviorStateLight(objectId, trigger, false, modifier, Current)
            : lights.StopAndPlayBehaviorStateLight(objectId, cube.Light, trigger, false, modifier, Current);
        if (!Current()) return false;
        if (played) cube.Light = trigger;
        else Warning("BehaviorGuardDog.StartLightCubeAnim.PlayAnimFailed",
            $"Failed to play light cube anim trigger {NativeCubeLightTriggers.Name(trigger)} on object with ID {id}");
        return played; // 0x005F5258..0x005F53C2.
    }

    private void Connection(ObjectConnectionStateMessage message)
    {
        if (message.ConnectedByte != 0) return;
        long version = _generation;
        Warning("BehaviorGuardDog.HandleObjectConnectionState",
            $"Object with ID {unchecked((int)message.ObjectID)} and type {CubeConnections.ObjectTypeName(message.ObjectType)} has disconnected");
        if (HostExecutionVersionCurrent(version)) InterruptTo(Phase.BlockDisconnected, version); // 0x005F4E82.
    }

    private void UnexpectedMovement(uint id, string handler)
    {
        if (_monitoring || _sleepStart != 0f) return;
        long version = _generation;
        string name = handler == "HandleObjectMoved" ? "ObjectMoved" : "ObjectUpAxisChanged";
        Info("Unnamed", $"BehaviorGuardDog.{handler}.UnexpectedBlockMovement",
            $"Received {name} message for Object with ID {unchecked((int)id)} even though we're not currently monitoring for movement and not yet sleeping!");
        if (HostExecutionVersionCurrent(version)) InterruptTo(Phase.SetupInterrupted, version); // 0x005F4FB8 / 0x005F5176.
    }

    private void UpAxisChanged(CubeUpAxisEvent report)
    {
        if (!_monitoring) { UnexpectedMovement(unchecked((uint)report.ObjectID), "HandleObjectUpAxisChanged"); return; }
        if (!_cubes.TryGetValue(report.ObjectID, out var cube))
        {
            Verify("BehaviorGuardDog.HandleObjectUpAxisChanged.UnknownObject", $"Received an UpAxisChanged message from an unknown object (objectId = {report.ObjectID})");
            return;
        }
        long version = _generation;
        cube.Axis = (byte)report.UpAxis;
        if (cube.Flipped) return;
        float now = Now;
        if (report.UpAxis == UpAxis.ZNegative)
        {
            cube.FlipTime = now;
            cube.Flipped = true;
            _flipped++;
            PlayLight(report.ObjectID, 0x17, version);
        }
        if (HostExecutionVersionCurrent(version)) _lastActivity = now; // 0x005F506C..0x005F51A8.
    }

    private void Accel(int id, float score, long version)
    {
        using var owner = _robot.Engine.EnterSynchronousOwner();
        if (!HostExecutionVersionCurrent(version) || !_monitoring) return;
        if (!_cubes.TryGetValue(id, out var cube))
        {
            Verify("BehaviorGuardDog.HandleObjectAccel.UnknownObject", $"Received acceleration from unknown object {id}");
            return;
        }
        if (cube.Flipped) { cube.Score = 0f; return; }
        cube.Score = score;
        if (score > cube.MaxScore) cube.MaxScore = score;
        float now = Now;
        if (score >= 80f && _sleepStart > 0f && now - _sleepStart > 10f)
        {
            InterruptTo(Phase.Busted, version);
            return;
        }
        if (score > 40f)
        {
            if (!cube.Moved)
            {
                cube.MoveTime = now;
                cube.Moved = true;
                _moved++;
                InterruptTo(Phase.Fakeout, version);
            }
            if (HostExecutionVersionCurrent(version) && cube.Light != 0x12) PlayLight(id, 0x12, version);
            if (HostExecutionVersionCurrent(version)) _lastActivity = now;
        }
        else if (cube.Light != 0x16) PlayLight(id, 0x16, version); // 0x005F54BC..0x005F5642.
    }

    private void Verify(string key, string message)
    {
        _robot.Engine.EmitSourceLog(EngineLogLevel.Error, null, key, message);
        Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        long version = ++_generation;
        Monitor(false, version);
        if (!HostExecutionVersionCurrent(version)) return;
        StopLights(version);
        if (!HostExecutionVersionCurrent(version)) return;
        _robot.BehaviorPublicState.UpdateBroadcastBehaviorStage(4, 0, () => HostExecutionVersionCurrent(version));
        if (!HostExecutionVersionCurrent(version)) return;
        _publicStage = 5;
        LogRun();
        if (HostExecutionVersionCurrent(version)) BroadcastEnd(_state == Phase.Complete); // 0x005F4718..0x005F4772.
    }

    public void Dispose()
    {
        using var owner = _robot.Engine.EnterSynchronousOwner();
        if (_retired) return;
        _retired = true;
        _subscriptions.Retire();
        RetireActionCompletionOwner();
        bool send = !_robot.Lifetime.Destroying && ReferenceEquals(Facade.Engine.Robot, _robot);
        foreach (var cube in _cubes.Values)
            if (cube.Listener is { } listener)
                Facade.CubeAccel.RemoveMovementListener(cube.RadioId, listener, send);
        _cubes.Clear();
    }

    private void DriveToBlocks(long version)
    {
        using var pose = StartingPose();
        if (pose is null || !HostExecutionVersionCurrent(version)) return;
        var drive = new DriveToPoseAction(_manipulation, false, false, _robot, () => HostExecutionVersionCurrent(version))
        {
            DistanceToleranceMm = 10f,
            AngleToleranceRad = BitConverter.UInt32BitsToSingle(0x3E32B8C2),
            PlanningTimeoutSec = 4f
        };
        drive.SetNativeGoals(new[] { pose });
        var retry = new RetryDrive(_robot, drive); // 0x005F3300, Count and two retries.
        StartActing(retry, report =>
        {
            if (report.Result != 0)
                Warning("BehaviorGuardDog.UpdateInternal.DriveToStartingPoseFailed",
                    $"Failed to drive to starting pose (result = 0x{report.Result:X8})! Continuing behavior from current pose.");
            if (HostExecutionVersionCurrent(version)) Transition(Phase.SettleIn);
        }, () => HostExecutionVersionCurrent(version)); // 0x005F5A9C.
    }

    // ComputeStartingPose 0x005F3E8C..0x005F4214.
    private Pose3d? StartingPose()
    {
        using var poses = _manipulation.World.EnterPoseMutation();
        var robotPose = _robot.GetCheckedMainPose();
        var origin = _robot.GetNativeOrigin(_robot.CurrentOriginId);
        var cubes = _manipulation.World.FindLocatedMatchingObjects(_filter);
        float cx = 0f, cy = 0f;
        if (cubes.Count != 0)
        {
            float inv = 1f / cubes.Count;
            foreach (var cube in cubes)
            {
                var p = cube.NativePose.Translation;
                cx += inv * (float)p.X;
                cy += inv * (float)p.Y;
            }
        }
        float robotAngle = MathF.Atan2((float)robotPose.Translation.Y - cy, (float)robotPose.Translation.X - cx);
        using var face = new Pose3d("");
        bool sawFace = _manipulation.Vision.Faces.CopyNativeLastFaceTimestamp(origin, false, face) != 0;
        float faceAngle = sawFace
            ? MathF.Atan2((float)face.Translation.Y - cy, (float)face.Translation.X - cx)
            : 0f;
        float turn = Radians.Rescale(robotAngle - faceAngle);
        float searchAngle = sawFace ? Radians.Rescale(faceAngle + (turn > 0 ? MathF.PI / 2f : -MathF.PI / 2f)) : robotAngle;
        float x = cx, y = cy;
        float dx = 10f * MathF.Cos(searchAngle), dy = 10f * MathF.Sin(searchAngle);
        for (int distance = 10; distance <= 250; distance += 10)
        {
            x += dx;
            y += dy;
            if (distance == 250)
            {
                Verify("BehaviorGuardDog.ComputeStartingPose.TranslatedTooFar", "Candidate starting pose has been translated way too far (250.0 mm, max is 250.0 mm)");
                break;
            }
            using var candidate = new Pose3d(Rotation3d.FromAngleAxis(searchAngle, new Vec3(0, 0, 1)), new Vec3(x, y, 0));
            var body = ObjectFootprint.RobotBoundingQuadXY(candidate, 0f); // 0x00514D74.
            bool blocked = false;
            foreach (var cube in cubes)
            {
                if (!cube.TryObservableBoundingQuad50(cube.NativePose, 0f, out var quad))
                {
                    ReportMissing("M11-093 GuardDog ComputeStartingPose cube footprint at 0x005F40D4 unavailable");
                    return null;
                }
                if (quad!.Intersects(body)) { blocked = true; break; }
            }
            if (!blocked) break;
        }
        float finalAngle = sawFace ? faceAngle : Radians.Rescale(robotAngle + MathF.PI / 2f);
        Info("Unnamed", "BehaviorGuardDog.ComputeStartingPose.Pose",
            FormattableString.Invariant($"Starting pose computed {(sawFace ? "using" : "without using")} last observed face: ({x:F1}, {y:F1}, 0.0 : {finalAngle * (180f / MathF.PI):F2} deg)"));
        return new Pose3d(Rotation3d.FromAngleAxis(finalAngle, new Vec3(0, 0, 1)), new Vec3(x, y, 0), parent: origin);
    }

    // 0x005F3460..0x005F3946. The closest-cube turn and reverse drive may fail independently.
    private void QueueReturn(bool won, long version)
    {
        bool Current() => HostExecutionVersionCurrent(version);
        if (won)
        {
            Record("PlayerSuccess");
            if (!Current()) return;
        }
        Monitor(false, version);
        if (!Current()) return;
        if (won) SetPublicStage(0, version);
        if (!Current()) return;
        var sequence = new CompoundActionSequential(() => Now);
        sequence.SeedNativeEmptyCompletion();
        sequence.AddAction(Animation(0xDE), false, false);
        using (var poses = _manipulation.World.EnterPoseMutation())
        {
            var closest = ClosestCube();
            if (closest is not null)
                sequence.AddAction(new NativeTurnTowardsPoseRunner(_manipulation, closest.NativePose, Radians.Pi)
                    { ScopedOwner = _robot, ScopedParentCurrent = Current }, true, false);
            else Verify($"BehaviorGuardDog.UpdateInternal.{(won ? "PlayerSuccess" : "Timeout")}.NoClosestBlock", "No closest block");
        }
        if (!Current()) { sequence.RetireNeverAdmittedTree(); return; }
        sequence.AddAction(new DriveStraightAction(_manipulation, _robot, -80f, 150f, true), true, false);
        sequence.AddAction(new StageAction(_manipulation, _robot, Current, () => SetPublicStage(won ? 4u : 3u, version)), false, false);
        if (!won)
        {
            bool touched = _moved != 0 || _flipped != 0;
            Record(touched ? "TimeoutCubesTouched" : "TimeoutCubesUntouched");
            if (!Current()) { sequence.RetireNeverAdmittedTree(); return; }
            sequence.AddAction(Animation(touched ? 0xDF : 0xE0), false, false);
        }
        else sequence.AddAction(Animation(0xDA), false, false);
        BroadcastEnd(won);
        if (Current()) Run(sequence, () => Transition(Phase.Complete));
        else sequence.RetireNeverAdmittedTree();
    }

    private ObservableObject? ClosestCube() =>
        NativeClosestSpatialQuery.Find(_manipulation.World, _robot.GetCheckedMainPose(),
            new Vec3(float.MaxValue, float.MaxValue, float.MaxValue), _filter); // 0x0061F8EC.

    private sealed class StageAction : NativeVisionRunner
    {
        private readonly Action _publish;
        protected override string ActionName => "WaitForLambda";

        internal StageAction(ManipulationSystem m, EngineRobot owner, Func<bool> current, Action publish) : base(m, 0x33, 0)
        {
            ScopedOwner = owner;
            ScopedParentCurrent = current;
            TimeoutSeconds = float.MaxValue;
            _publish = publish;
        }
        public override uint CheckIfDone()
        {
            if (!ScopedCurrent) return EngineActionResult.Cancelled;
            _publish();
            return ScopedCurrent ? EngineActionResult.Success : EngineActionResult.Cancelled;
        }
    }

    // RetryWrapper profile with the shipped Count trigger, which suppresses the retry animation (0x0055FDD0).
    private sealed class RetryDrive : CompoundAction
    {
        private readonly DriveToPoseAction _drive;
        private int _attempts;
        protected override string ActionName => "Retry[DriveToPose]";
        public override string NativeName => ActionName;

        internal RetryDrive(EngineRobot owner, DriveToPoseAction drive)
            : base(() => owner.Engine.Timer.SecondsF, null, (level, channel, key, message) => owner.Engine.EmitSourceLog(level, channel, key, message))
        {
            _drive = drive;
            AddAction(drive, false, false);
            SetCompletionUnionProxy(drive.Tag);
        }

        protected override uint UpdateInternal()
        {
            uint result = _drive.Update();
            if (DestructionObserved) return State;
            Type = _drive.Type;
            if (result is EngineActionResult.Success or EngineActionResult.Running or EngineActionResult.Cancelled or EngineActionResult.Interrupted)
                return result;
            _drive.Reset(true); // 0x0055FD6E..0x0055FD76.
            return _attempts++ < 2 ? EngineActionResult.Running : result; // 0x0055FE10..0x0055FE26.
        }
    }

    // fidelity: M15-070
    private void LogRun()
    {
        // 0x005F47AC..0x005F4BD4: roundf; then ascending ID, comma between entries.
        long version = _generation;
        static string Number(float value) => ((int)MathF.Round(value, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
        string SinceSleep(float when) => when == 0f ? "0" : Number((when - _sleepStart) * 1000f);
        void Event(string name, string value, string? data = null) =>
            _robot.Engine.EmitSourceLog(EngineLogLevel.Event, null, "robot.guarddog." + name, value,
                data is null ? null : new Dictionary<string, string> { ["$data"] = data });
        Event("result", _result, Number(_sleepDuration * 1000f));
        if (!HostExecutionVersionCurrent(version)) return;
        Event("cubes_moved", Number(_moved), Number(_flipped));
        if (!HostExecutionVersionCurrent(version)) return;
        Event("cubes_moved_times", string.Join(",", _cubes.Values.Select(cube => SinceSleep(cube.MoveTime))),
            string.Join(",", _cubes.Values.Select(cube => SinceSleep(cube.FlipTime))));
        if (HostExecutionVersionCurrent(version))
            Event("cube_movement_scores", string.Join(",", _cubes.Values.Select(cube => Number(cube.MaxScore))));
    }
}
