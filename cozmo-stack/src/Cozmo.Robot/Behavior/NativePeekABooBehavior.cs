using System.Globalization;
using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

internal sealed class NativePeekABooBehavior : SteppedBehavior, IDisposable
{
    private enum Phase { Intro, Turn, Request, WaitForHide, WaitForReturn, Returned, NoInteraction, Exit }

    private readonly ManipulationSystem _manipulation;
    private readonly EngineRobot _robot;
    private readonly FeatureGates? _features;
    private readonly int _minimum, _maximum, _idleCount, _requests;
    private readonly bool _requireConfirmation, _playIntro, _alwaysStreamline;
    private readonly float _maxFaceAgeMs, _cooldown;
    private readonly SortedList<uint, int> _hiddenFrames = new();
    private Phase _phase;
    private int _target, _failedTurns;
    private uint _remaining, _total = 1;
    private float _nextAllowed, _requestStart;
    private bool _alreadyAskedAgain, _faceWasVisibleAtRequestEnd;
    private long _generation;
    private bool _retired;

    private CozmoRobot Facade => _manipulation.Robot;
    private FaceWorld Faces => _manipulation.Vision.Faces;
    private float Now => _robot.Engine.Timer.SecondsF;
    private uint ImageTime => Facade.CameraSettings.LastProcessedImageTimestamp;

    // Factory 0x0059CDC4, ctor 0x005F62D0..0x005F64DC. Both shipped configs use these fields.
    internal NativePeekABooBehavior(BehaviorFactoryContext source, JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(), "PeekABoo")
    {
        _manipulation = source.Manipulation!;
        _robot = source.NativeRobot!;
        _features = source.Features;
        JsonElement Field(string key) => config.TryGetProperty(key, out var value) ? value : default;
        int Int(string key, int fallback) => Field(key) is { ValueKind: JsonValueKind.Number } v ? v.GetInt32() : fallback;
        float Float(string key, float fallback) => Field(key) is { ValueKind: JsonValueKind.Number } v ? v.GetSingle() : fallback;
        bool Flag(string key, bool fallback) => Field(key).ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };

        _minimum = Int("minTimesPeekBeforeQuit", 1);
        _maximum = Int("maxTimesPeekBeforeQuit", 3);
        _idleCount = Int("noUserInteractionTimeout_numIdles", 3);
        _requests = Int("numReRequestsPerTimeout", 0);
        _requireConfirmation = Flag("requireFaceConfirmBeforeRequest", true);
        _playIntro = Flag("playGetIn", true);
        _alwaysStreamline = Flag("alwaysStreamline", false);
        _cooldown = Float("minCooldown_Sec", 0f);
        _maxFaceAgeMs = Float("maxTimeOldestFaceToConsider_Sec", 60f) * 1000f;
        if (unchecked((uint)_idleCount) <= unchecked((uint)_requests))
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning, "Behaviors", "BehaviorPeekABoo.Config.InvalidTimeouts",
                $"Doing {_requests} re-requests, but only supposed to wait {_idleCount} idles before timing out. This won't work");
            _idleCount = 3;
            _requests = 2;
        }
        NativeScoredFields.SparkPeekDeadline = 0f;
    }

    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => _robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => ++_generation;
    protected override long CaptureHostExecutionVersion() => _generation;
    protected override bool HostExecutionVersionCurrent(long version) =>
        !_retired && _generation == version && ReferenceEquals(_robot.Engine.Robot, _robot);
    protected override bool HostStopContinuationCurrent(long version) => !_retired && _generation == version + 1;
    protected override bool HostResumeContinuationCurrent(long version) =>
        !_retired && (_generation == version || _generation == version + 1);
    // The UpdateInternal tail at 0x005BDA56 keeps the behavior active while the owned action tag is set.
    protected override bool KeepsRunningWithoutAction => HasCurrentAction;
    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => false;

    // 0x005F65EC. The hard-spark deadline precedes the normal cooldown, face and feature gates.
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        using var lease = EnterHostExecutionOwner(context);
        if (_robot.BehaviorManagerOwner is { ActiveSparkId: 0x25, ActiveSparkSoft: 0 } &&
            NativeScoredFields.SparkPeekDeadline is > 0f and var deadline && Now > deadline)
        {
            NativeScoredFields.SparkPeekDeadline = -1f;
            return true;
        }

        _target = 0;
        if (!(Now > _nextAllowed)) return false;
        try
        {
            if (InteractionFace() == 0) return false;
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("MISSING:", StringComparison.Ordinal))
        {
            MarkReadinessUnavailable();
            ReportMissing(error.Message);
            return false;
        }
        if (_features is not null) return _features.IsEnabled("PeekABoo"); // FeatureType 4, 0x005F6630.
        MarkReadinessUnavailable();
        ReportMissing("PeekABoo feature 4 owner unavailable at 0x005F6638");
        return false;
    }

    // 0x005F6666. The cached id stays only while it still belongs to the current-origin set.
    private int InteractionFace()
    {
        var ids = Faces.GetNativeFaceIds(_robot, ToUnsigned((float)ImageTime - _maxFaceAgeMs));
        if (!ids.Contains(_target))
            _target = BestFaceToTrack.GetNative(ids, _manipulation, _robot);
        return _target;
    }

    // ARM VCVT.U32.F32 saturates before truncation.
    private static uint ToUnsigned(float value) => value > 0f ? value >= 4294967296f ? uint.MaxValue : (uint)value : 0;

    protected override void OnStart()
    {
        if ((_alwaysStreamline || _robot.BehaviorManagerOwner is { ActiveSparkId: not 0x55, ActiveSparkSoft: 0 }) &&
            NativeScoredFields.SparkPeekDeadline == -1f)
        {
            NativeScoredFields.SparkPeekDeadline = 0f;
            Run(new TriggerAnimationAction(Facade, new((AnimationTrigger)0x210, 1, true, 0, 60f, false)));
            return;
        }

        _failedTurns = 0;
        _alreadyAskedAgain = false;
        _hiddenFrames.Clear();
        _remaining = _total = unchecked((uint)_robot.Engine.ContextRandom.RandIntInRange(_minimum, _maximum)); // 0x005F678A.
        long generation = _generation;
        Scope.AcquireScopedIdle(Facade.Animations.Scheduler, (AnimationTrigger)0x23F, Id, () => _robot.Engine.Timer.TimeStampMs);
        if (!HostExecutionVersionCurrent(generation)) return;
        Scope.AcquireScopedReactions(Id, ReactionLockTables.PeekABoo);
        if (!HostExecutionVersionCurrent(generation)) return;
        if (!_playIntro) { Turn(); return; }
        if (State(Phase.Intro, "DoingInitialReaction")) Run(Animation(0x177), Turn); // 0x005F6818.
    }

    private bool State(Phase phase, string name)
    {
        long generation = _generation;
        _phase = phase;
        return SetStateName(name, () => HostExecutionVersionCurrent(generation)) && HostExecutionVersionCurrent(generation);
    }

    private NativeLastFaceRunner TurnAction(bool confirm = false) =>
        new(_manipulation, _robot, InteractionFace()) { RequireVerifiedFace = confirm };

    private TriggerLiftSafeAnimationAction Animation(int trigger, int loops = 1, int tracks = 0) =>
        new(Facade, new((AnimationTrigger)trigger, loops, true, tracks, 60f, false));

    private void Run(ActionRunner runner, Action? next = null)
    {
        long generation = _generation;
        StartActing(runner, next is null ? null : _ => next(), () => HostExecutionVersionCurrent(generation));
    }

    private void Turn()
    {
        if (!State(Phase.Turn, "TurningToFace")) return;
        long generation = _generation;
        // 0x005F6934: face id, pi, sayName=false. Only this turn overrides confirmation (0x005F6942).
        StartActing(TurnAction(_requireConfirmation), completion =>
        {
            uint result = (uint)completion.Result;
            if (result == EngineActionResult.Success)
            {
                _failedTurns = 0;
                Request();
            }
            else if (_failedTurns++ >= 3 || ((result & 0xFF000000u) != 0x04000000u && InteractionFace() == 0))
                Exit();
            else
                Turn();
        }, () => HostExecutionVersionCurrent(generation)); // 0x005F794A.
    }

    private void Request()
    {
        if (!State(Phase.Request, "RequestPeekaBooAnim")) return;
        _requestStart = Now;

        // 0x005F6FCC / 0x005F71B8; single-precision progress, strict comparisons.
        float progress = 1f - (float)_remaining / _total;
        int trigger = _remaining == _total && _alreadyAskedAgain ? 0x17F :
            progress < 0.3f ? 0x17C : progress < 0.6f ? 0x17D : 0x17A;
        var sequence = new CompoundActionSequential(() => Now);
        sequence.SeedNativeEmptyCompletion();
        sequence.AddAction(Animation(trigger), false, false);
        sequence.AddAction(TurnAction(), false, false);
        Run(sequence, () =>
        {
            if (!TryFaceHidden(out bool hidden)) return;
            _faceWasVisibleAtRequestEnd = !hidden; // 0x005F7A02.
            if (hidden) WaitForReturn();
            else WaitForHide();
        });
    }

    // 0x005F6A36: sample the current-origin observations when the image timestamp changes.
    // TrackedFace+0x44 is the left-eye contour vector, which is distinct from the eye centres.
    private bool TryFaceHidden(out bool hidden)
    {
        hidden = false;
        uint timestamp = ImageTime;
        while (_hiddenFrames.Count > 50) _hiddenFrames.RemoveAt(0);
        if (_hiddenFrames.Count == 0 || _hiddenFrames.Keys[^1] != timestamp)
        {
            bool visible = false;
            bool unavailable = false;
            foreach (int id in Faces.GetFaceIDsObservedSince(timestamp, requireId: true))
            {
                if (Faces.GetNativeFace(_robot, id) is not { } face) continue;
                if (face.NativeFeatures is not { } features) { unavailable = true; continue; }
                if (features.LeftEye.Count == 0) continue;
                visible = true;
                break;
            }
            if (!visible && unavailable)
            {
                ReportMissing("M14-010 PeekABoo left-eye contour input unavailable at 0x005F6AC4");
                return false;
            }
            int previous = _hiddenFrames.Count == 0 ? 0 : _hiddenFrames.Values[^1];
            int absent = previous == int.MaxValue ? previous : previous + 1;
            _hiddenFrames.TryAdd(timestamp, visible ? 0 : absent);
        }

        hidden = _hiddenFrames.Keys[^1] >= timestamp && _hiddenFrames.Values[^1] > 6; // 0x005F6BA0..0x005F6C50.
        return true;
    }

    protected override void OnUpdate()
    {
        long generation = _generation;
        if (!TryFaceHidden(out bool hidden) || !HostExecutionVersionCurrent(generation)) return;
        // 0x005F69D8: cancel the active wait without invoking its timeout continuation.
        if ((_phase == Phase.WaitForHide && hidden) || (_phase == Phase.WaitForReturn && !hidden))
        {
            StopActing(keepAction: false, viaCallback: false);
            if (!HostExecutionVersionCurrent(generation)) return;
            if (_phase == Phase.WaitForHide) WaitForReturn();
            else Returned();
        }
    }

    private void WaitForHide()
    {
        if (!State(Phase.WaitForHide, "WaitingToHideFace")) return;
        Run(TurnAction(), () =>
        {
            var parallel = new CompoundActionParallel(() => Now);
            parallel.SeedNativeEmptyCompletion();
            var track = new NativeTrackFaceRunner(_manipulation, _robot, InteractionFace());
            var idle = IdleAndRequests(lockHead: false);
            // 0x005F7AA0..0x005F7B42: the parallel, tracker and idle are constructed in that order.
            track.StopAfterOtherAction(idle.Tag);
            parallel.AddAction(track, false, false);
            parallel.AddAction(idle, false, false);
            Run(parallel, () => Timeout("robot.peekaboo_face_never_hidden"));
        });
    }

    private void WaitForReturn()
    {
        if (!State(Phase.WaitForReturn, "WaitingToSeeFace")) return;
        Run(TurnAction(), () => Run(IdleAndRequests(lockHead: true),
            () => Timeout("robot.peekaboo_face_never_came_back"))); // 0x005F6C54, 0x005F7D08, 0x005F7DEC.
    }

    private CompoundActionSequential IdleAndRequests(bool lockHead)
    {
        // 0x005F7328: lift bit below the head-angle literal, head bit when waiting for the face.
        int tracks = lockHead ? 1 : 0;
        if (Facade.Motion.HeadAngleRad < BitConverter.UInt32BitsToSingle(0x3EC49809)) tracks |= 2;
        var sequence = new CompoundActionSequential(() => Now);
        sequence.SeedNativeEmptyCompletion();
        for (uint i = 0; i < unchecked((uint)_requests); i++)
        {
            sequence.AddAction(Animation(0x17B, tracks: tracks), false, false);
            sequence.AddAction(Animation(0x17F, tracks: tracks), false, false);
        }
        if (unchecked((uint)_idleCount) > unchecked((uint)_requests))
            sequence.AddAction(Animation(0x17B, unchecked(_idleCount - _requests), tracks == 0 ? 0 : 1), false, false);
        return sequence;
    }

    private void Returned()
    {
        if (!State(Phase.Returned, "ReactingToPeekABooReturned")) return;
        --_remaining;
        long generation = _generation;
        Event(_faceWasVisibleAtRequestEnd ? "robot.single_peekaboo_success.face_noface_face" :
            "robot.single_peekaboo_success.noface_timepass_face", ToUnsigned(Now - _requestStart));
        if (!HostExecutionVersionCurrent(generation)) return;
        if (_remaining == 0) Exit();
        else Run(Animation(0x180), Turn); // 0x005F6E2A.
    }

    private void Timeout(string name)
    {
        long generation = _generation;
        Event(name, _remaining);
        if (!HostExecutionVersionCurrent(generation) || !State(Phase.NoInteraction, "ReactingToNoUserInteraction")) return;
        bool giveAnotherTry = _remaining == _total && !_alreadyAskedAgain;
        _alreadyAskedAgain = true;
        if (giveAnotherTry)
            Run(new TriggerAnimationAction(Facade, new((AnimationTrigger)0x17E, 1, true, 0, 60f, false)), Turn);
        else
            Exit(); // 0x005F7700.
    }

    private void Exit()
    {
        if (!State(Phase.Exit, "DoingFinalReaction")) return;
        long generation = _generation;
        bool success = _remaining != _total;
        Run(Animation(success ? 0x178 : 0x179)); // 0x005F75DC.
        if (!HostExecutionVersionCurrent(generation)) return;
        _robot.Engine.ReportBehaviorObjective(16, "PeekABooComplete", Id);
        if (!success || !HostExecutionVersionCurrent(generation)) return;
        _robot.Engine.ReportBehaviorObjective(17, "PeekABooSuccess", Id);
        if (HostExecutionVersionCurrent(generation)) NeedActionCompleted(); // 0x005F7692, configured action.
    }

    private void Event(string name, uint value) =>
        _robot.Engine.EmitSourceLog(EngineLogLevel.Event, null, name, value.ToString(CultureInfo.InvariantCulture));

    protected override void OnStop(BehaviorStopReason reason)
    {
        ++_generation;
        _nextAllowed = Now + _cooldown; // 0x005F6F20.
    }

    public void Dispose()
    {
        if (_retired) return;
        _retired = true;
        RetireActionCompletionOwner();
    }
}
