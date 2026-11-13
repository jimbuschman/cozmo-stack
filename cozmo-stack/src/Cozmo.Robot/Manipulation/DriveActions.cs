using Cozmo.Protocol;
using Cozmo.Transport;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Manipulation;

/// <summary><c>Anki::Cozmo::ActionResult</c> (UNITY values) for the outcomes this stack produces.</summary>
public enum ActionResult : uint
{
    Success = 0, Running = 0x01000000, CancelledWhileRunning = 0x02000000, Abort = 0x03000000,
    BadObject = 0x03000004, BadPose = 0x03000005, NoPreActionPoses = 0x03000010, NotCarryingObjectAbort = 0x03000011,
    PathPlanningFailedAbort = 0x03000013, StillCarryingObject = 0x03000017, Timeout = 0x03000018, VisualObservationFailed = 0x0300001D,
    /// <summary>0x03000014: <c>PickupObjectAction::Verify</c> "ObjectStillMoving" (r4 = 0x03000004 + 0x10 at 0x00553CEA).</summary>
    PickupObjectStillMoving = 0x03000014,
    /// <summary>0x03000015: <c>PickupObjectAction::Verify</c> not seen recently enough (r4 + 0x11 at 0x00553D6E).</summary>
    PickupObjectNotSeenRecently = 0x03000015,
    /// <summary>0x04000005: <c>PickupObjectAction::Verify</c> "expected carrying" (0x00553FC8..0x00554004, r5 = 0x04000005 at 0x00553DE8) and "seeing the object in its original pose" (0x00554116).</summary>
    PickupRetry = 0x04000005,
    Retry = 0x04000000, DidNotReachPreActionPose = 0x04000001, FailedTraversingPath = 0x04000002,
    /// <summary>0x03000008 (<c>0x0055AB7A/0x0055AB7C</c>): <c>DriveToPoseAction::CheckIfDone</c>'s initial result, kept when the arrival test fails while the path ids differ (M12-023).</summary>
    FollowingPathButNotTraversing = 0x03000008,
    /// <summary>0x0300000D: <c>DriveToObjectAction::InitHelper</c> for ActionType 6 with a strictly negative <c>+0x84</c> (0x00558FE2/0x00558FEA; NaN does not, 0x00558FDC..0x0055903E, M12-022).</summary>
    NoDistanceSet = 0x0300000D,
    /// <summary>0x03000002: <c>IDockAction::Init</c>'s "IDockAction.Init.NullDockMarker" (research Q6.8, M12-017).</summary>
    BadMarker = 0x03000002,
    /// <summary>0x04000008: <c>DriveToPlaceCarriedObjectAction::CheckIfDone</c> when the placement goal is not free (0x00559FA4, M12-030).</summary>
    PlacementGoalNotFree = 0x04000008,
    /// <summary>0x04000006: <c>BackupOntoChargerAction::CheckIfDone</c>'s fall-through when the straight drive
    /// finishes, and <c>ConfigureDriveForRetryAction</c>'s result (M13-008).</summary>
    RetryDriveDone = 0x04000006,
    /// <summary>0x04000009: <c>DriveOffChargerContactsAction::CheckIfDone</c> fails with this while the robot
    /// is still on the charger contacts (0x00558344, M13-013).</summary>
    StillOnCharger = 0x04000009,
    /// <summary>0x0400000A: <c>BackupOntoChargerAction::CheckIfDone</c>'s pitch-below result (0x0054E7DE, M13-008).</summary>
    BackupPitchedTooFar = 0x0400000A,
    /// <summary>0x03000016: <c>PlaceObjectOnGroundAction::Init</c> when <c>CarryingComponent::PlaceObjectOnGround</c> fails ("...SendPlaceObjectOnGroundFailed", <c>add.w r8,r8,#5</c> 0x005547F8, M15-022).</summary>
    SendPlaceObjectOnGroundFailed = 0x03000016,
}

/// <summary>
/// <c>ERobotDriveToPoseStatus</c>, the value <c>DriveToPoseAction::CheckIfDone</c> switches on
/// (<c>[[robot+0x5C]+0x38]</c>): the name table at 0x0102F610, indexed by
/// <c>PathComponent::SetDriveToPoseStatus</c> 0x006492DE..0x006492E8 (M12-023).
/// </summary>
public enum DriveToPoseStatus
{
    Failed = 0, ComputingPath = 1, WaitingToBeginPath = 2, FollowingPath = 3, Ready = 4,
    WaitingToCancelPath = 5, WaitingToCancelPathAndSetFailure = 6,
}

/// <summary>
/// The inputs one call of <c>DriveToPoseAction::CheckIfDone</c> 0x0055AB4C reads from outside the action:
/// the path status <c>[[robot+0x5C]+0x38]</c>, the clock, the robot pose, <c>Robot::GetHeight</c> and the two
/// path ids <c>[[robot+0x5C]+0x42]</c> and <c>[[robot+0x5C]+0x44]</c> (M12-023). The engine's names for the
/// two ids are not in the inventory, so they are named by offset.
/// </summary>
public readonly record struct DriveToPoseTick(int PathStatus, double NowSec, Pose3d RobotPose, double RobotHeightMm,
                                              ushort PathIdAt0x42, ushort PathIdAt0x44, sbyte CurrentPathSegment = -1);

/// <summary>
/// The engine's <c>DrivingAnimationHandler</c> (<c>[robot+0x24C]</c>) as <c>DriveToPoseAction::CheckIfDone</c>
/// uses it: the state word it reads first (<c>[[robot+0x24C]] == 3</c> keeps the action RUNNING), the start
/// animation at path state 3 and the end animation at the tail. The shared Robot handler now implements the
/// checked M5-063 bodies and the M12-024 action-side lifecycle calls. The queued
/// TriggerLiftSafeAnimationAction runner remain visible MISSING boundaries.
/// </summary>
// fidelity: M12-024
public interface IDrivingAnimationHandler
{
    /// <summary><c>[[robot+0x24C]]</c> (0x0055AB5C..0x0055AB6A).</summary>
    int State { get; }
    bool Init(byte tracks, uint tag, bool suppressTrackLocking, bool repeatWhilePath) => true;
    bool InputsReady => true;
    void ActionIsBeingDestroyed() { }
    /// <summary><c>DrivingAnimationHandler::PlayStartAnim</c> (PLT 0x004AB134; call 0x0055AC08). Its return value is not used by CheckIfDone.</summary>
    void PlayStartAnim();
    /// <summary><c>DrivingAnimationHandler::PlayEndAnim</c> (PLT 0x004A5764; call 0x0055B0AA). Non-zero keeps CheckIfDone RUNNING (0x0055B0AA..0x0055B0CC).</summary>
    uint PlayEndAnim();
}

/// <summary>
/// Legacy explicit opt-out for callers that cannot supply the driving-handler lifecycle. The default
/// DriveToPoseAction uses the shared M5-063 handler; this adapter retains the historical throwing contract.
/// </summary>
// fidelity: M12-024
public sealed class UnreadDrivingAnimationHandler : IDrivingAnimationHandler
{
    public static readonly UnreadDrivingAnimationHandler Instance = new();
    private const string Gap = "M12-024 RECOVERABLE_GAP: action-side DrivingAnimationHandler Init/start/end lifecycle is not read; handler bodies are M5-063 (PLT 0x004AB134 / 0x004A5764).";
    public int State => throw new NotSupportedException(Gap);
    public void PlayStartAnim() => throw new NotSupportedException(Gap);
    public uint PlayEndAnim() => throw new NotSupportedException(Gap);
}

/// <summary>
/// Checked DriveToPoseAction lifecycle on the existing ActionList runner: handler.Init precedes goal/path
/// checks, CheckIfDone holds the end state and re-evaluates the current inputs after completion, and cleanup
/// follows the native order. Timeout and CanInterrupt use the native base getters. Until B-M12-1,
/// live status reads the M12-032 PathComponent; path creation and motion
/// primitives retain their separate gaps and are not claimed source-complete.
/// </summary>
// fidelity: M12-024
public sealed class DriveToPoseAction : ActionRunner, IDisposable
{
    public static readonly double PathFollowingHeadAngleRad = BitConverter.Int32BitsToSingle(unchecked((int)0xBE860A92));
    /// <summary><c>[this+0x9C]</c>'s constructor default, Radians(0x3E32B8C2) (0.174533).</summary>
    public static readonly double GoalAngleToleranceRad = BitConverter.Int32BitsToSingle(0x3E32B8C2);
    /// <summary><c>[this+0x90..0x98]</c>'s constructor default, 10.0 (inventory G3.9, base ctor 0x0055A238).</summary>
    public const double DefaultGoalDistanceToleranceMm = 10.0;
    /// <summary><c>[this+0xA8]</c>'s constructor default, 4.0 (inventory G3.9): the ComputingPath timeout in seconds.</summary>
    public const double DefaultComputingPathTimeoutSec = 4.0;

    private readonly ManipulationSystem _m;
    private EngineRobot? _scopedOwner;
    private Func<bool>? _scopedParentCurrent;
    private readonly object _scopedCleanupGate=new();

    // RETIRE-LEGACY-DRIVE-API-1 (operator 2026-11-13): the public (m) constructor and its legacy Init are retired; every drive is built
    // through the native (m, head, manual) constructors and gets its goals only through SetNativeGoals.
    private DriveToPoseAction(ManipulationSystem m) : base(12, 4)
    {
        _m = m; AbortPath = () => _m.Paths.Abort();
        DrivingAnimations = m.DrivingAnimationsForTest?.Invoke() ?? m.Robot.Animations.DrivingAnimations;
        SourceLog = (l,c,k,t) => m.Robot.Engine.EmitSourceLog(l,c,k,t);
        Log = m.Robot.Engine.EmitStackLog;
    }
    private bool _nativeHead,_nativeManual,_nativeGoalsSet;
    private List<Pose3d> _ownedGoals=new();
    internal DriveToPoseAction(ManipulationSystem m,bool head,bool manual):this(m)
    {_nativeHead=head;_nativeManual=manual;RequiredTrackMask=manual?0u:4u;SeedNativeEmptyCompletion();}
    internal DriveToPoseAction(ManipulationSystem m,bool head,bool manual,EngineRobot owner,Func<bool>? parentCurrent=null):this(m,head,manual)
    {
        _scopedOwner=owner;
        _scopedParentCurrent=parentCurrent;
        AbortPath=()=>
        {
            long generation=_m.Robot.Motion.Path.ScopeGeneration;
            _m.Paths.AbortOwned(owner,()=>OwnerCurrent()&&_m.Robot.Motion.Path.ScopeGeneration==generation);
        };
    }
    private bool OwnerCurrent()=>_scopedOwner is null || !_scopedOwner.Lifetime.Destroying && ReferenceEquals(_m.Robot.Engine.Robot,_scopedOwner);
    private bool ExecutionCurrent()=>_scopedOwner is null || OwnerCurrent()&&!DestructionObserved&&!_destroyed&&(_scopedParentCurrent?.Invoke()??true);
    internal bool SetNativeGoals(IReadOnlyList<Pose3d> goals)
    {
        if(State!=EngineActionResult.NotStarted)return false;
        foreach(var old in _ownedGoals)old.Dispose();
        _ownedGoals=goals.Select(p=>new Pose3d(p)).ToList();Goals=_ownedGoals;Goal=_ownedGoals.FirstOrDefault();_nativeGoalsSet=true;return true;
    }
    private readonly PathGoalIndex _goalIndex=new();
    internal ushort? SourcePathIdForTest => _m.Robot.Motion.Path.Field42;
    private bool _destroyed;
    private readonly HashSet<string> _missingMessages=new();
    private TaskCompletionSource<ActionResult>? _done;
    internal Action<string>? LifecycleCallObserved;
    protected override float EngineClockSeconds => _m.Robot.Engine.Timer.SecondsF;
    protected override bool AreAnyTracksLocked(uint mask) => OwnerCurrent() && _m.Robot.Motion.AreAnyTracksLocked((byte)mask);
    protected override void LockTracks(uint mask, string owner) { if(_scopedOwner is null)_m.Robot.Motion.LockTracks((byte)mask,owner,"DriveToPose");else if(OwnerCurrent())_m.Robot.Motion.LockTracksOwned((byte)mask,owner,"DriveToPose",_scopedOwner); }
    protected override void UnlockTracksInternal(uint mask, string owner)
    {if(_scopedOwner is null)_m.Robot.Motion.UnlockTracks((byte)mask,owner);else LogCallbackDeferral.RunGated(_scopedCleanupGate,()=>{if(OwnerCurrent())_m.Robot.Motion.UnlockTracksOwned((byte)mask,owner,_scopedOwner);});}
    protected override bool AreAllTracksLockedBy(uint mask, string owner) => OwnerCurrent() && _m.Robot.Motion.AreAllTracksLockedBy((byte)mask, owner);
    protected override void StopBodyTrack()
    {if(_scopedOwner is not null)LogCallbackDeferral.RunGated(_scopedCleanupGate,()=>{if(OwnerCurrent())_m.Robot.Motion.StopBodyOwned(_scopedOwner);});}
    protected override string ActionName => "DriveToPose"; // 0x0055A374.
    public override bool CanInterrupt() => false; // vptr+0x14 -> 0x0052B0B3.
    public override uint CheckIfDone()
    {
        if(!ExecutionCurrent())return EngineActionResult.Cancelled;
        if (_m.DriveTickForTest is { } input) return (uint)CheckIfDone(input(this));
        using var mutation=_m.World.EnterPoseMutation();
        var owner=_scopedOwner??_m.Robot.Engine.Robot??throw new NativeActionUnavailable("M12-023 actual Robot owner unavailable");
        var nativePath=_m.Robot.Motion.Path;
        return (uint)CheckIfDone(new DriveToPoseTick(nativePath.DriveToPoseStatus,_m.Robot.Engine.Timer.SecondsF,owner.GetCheckedMainPose(),_m.NativeHeight(owner),nativePath.Field42,nativePath.Field44,unchecked((sbyte)nativePath.Field40)));
    }
    private void MissingSource(string text)
    { if(!_missingMessages.Add(text))return;string line="MISSING: M12-024 | "+text; _trace.Add(line); _m.Robot.Engine.EmitStackLog(line); }

    public override int Init()=>InitNative();
    private int InitNative()
    {
        if(!ExecutionCurrent())return unchecked((int)EngineActionResult.Cancelled);
        using var mutation=_m.World.EnterPoseMutation();
        if(_scopedOwner is {} scoped && DrivingAnimations is Animation.DrivingAnimationHandler actual)
            actual.InitOwned(scoped,ExecutionCurrent,(byte)RequiredTrackMask,Tag,SuppressTrackLocking,false);
        else DrivingAnimations.Init((byte)RequiredTrackMask,Tag,SuppressTrackLocking,false);
        if(!ExecutionCurrent())return unchecked((int)EngineActionResult.Cancelled);
        DeadlineSec=-1;
        if(!_nativeGoalsSet)return 0x0300000F;
        var owner=_scopedOwner??_m.Robot.Engine.Robot??throw new NativeActionUnavailable("M12-023 actual Robot owner unavailable");
        if(!owner.TryGetNativeOriginOwner(owner.CurrentOriginId,out var origin)||origin is null)
            throw new NativeActionUnavailable("M12-023 current world origin owner unavailable");
        foreach(var goal in _ownedGoals)if(!goal.TryWithRespectTo(origin,goal))return 0x03000005;
        _goalIndex.Value=0;
        var native=_m.Robot.Motion.Path;
        int result=native.StartNativeDrivingToPose(_m,_ownedGoals,_goalIndex,_nativeManual,_scopedOwner,_scopedOwner is null?null:ExecutionCurrent);
        if(!ExecutionCurrent())return unchecked((int)EngineActionResult.Cancelled);
        if(result!=0)return 0x03000013;
        if(_nativeHead&&_m.Robot.Motion.MoveHeadToAngle(BitConverter.UInt32BitsToSingle(0xBE860A92),2f,5f,0f,out _,_scopedOwner)!=0)return 0x03000016;
        if(!ExecutionCurrent())return unchecked((int)EngineActionResult.Cancelled);
        return 0;
    }
    private void CleanupSource()
    {
        if (_destroyed) return; _destroyed=true;
        long pathGeneration=_m.Robot.Motion.Path.ScopeGeneration;
        bool CleanupCurrent()=>OwnerCurrent()&&(_scopedOwner is null||_m.Robot.Motion.Path.ScopeGeneration==pathGeneration);
        bool active=CleanupCurrent()&&(_m.PathIsActiveForTest?.Invoke()??_m.Robot.Motion.Path.IsActive);
        if(active){LifecycleCallObserved?.Invoke("Abort");if(CleanupCurrent())AbortPath();} // M12-032 P3/P30.

        foreach (bool which in new[]{true,false})
        {
            if(!CleanupCurrent())break;
            LifecycleCallObserved?.Invoke("ErasePlanner:"+which);
            if(!CleanupCurrent())break;
            if(_m.ErasePlannerObstaclesForTest is { } erase)erase(which);
            else MissingSource("EraseAllPlannerObstacles("+(which?"1":"0")+") 0x0055A626/0x0055A632 visualizer handoff absent");
        }
        if(CleanupCurrent())
        {
            LifecycleCallObserved?.Invoke("DestroyDriving");
            if(_scopedOwner is {} scoped && DrivingAnimations is Animation.DrivingAnimationHandler actual)actual.ActionIsBeingDestroyed(scoped,CleanupCurrent);
            else if(CleanupCurrent())DrivingAnimations.ActionIsBeingDestroyed();
        }
        foreach(var goal in _ownedGoals)goal.Dispose();_ownedGoals.Clear();PreActionObjectPose?.Dispose();PreActionObjectPose=null;
    }
    public void Dispose() { if(_destroyed)return;CleanupSource();base.WatcherEnding(); }
    internal override bool RetireNeverAdmittedTree()
    {
        if(!base.RetireNeverAdmittedTree())return false;
        foreach(var goal in _ownedGoals)goal.Dispose();
        _ownedGoals.Clear();PreActionObjectPose?.Dispose();PreActionObjectPose=null;
        return true;
    }
    public override void WatcherEnding()
    {
        Dispose();
        // Stored Cancel bypasses IAction::UpdateInternal's callbacks. The queue's destruction
        // is the terminal boundary for the host task; no handler end call is added.
        // DeleteActionAndIter snapshots the stored full result, including RUNNING when QueueNow deletes without Cancel.
        if(_done?.TrySetResult((ActionResult)State)==true)_m.DriveResultObservedForTest?.Invoke((ActionResult)State);
    }


    /// <summary>The first owned goal; goals enter only through <see cref="SetNativeGoals"/> (RETIRE-LEGACY-DRIVE-API-1).</summary>
    public Pose3d? Goal { get; private set; }
    /// <summary>The action-owned goal vector +0x7C (SetGoals 0x0055A6D0 copy); set only through <see cref="SetNativeGoals"/>.</summary>
    public IReadOnlyList<Pose3d>? Goals { get; private set; }
    /// <summary>The engine's <c>idx</c>, the byte at <c>[[this+0x88]]</c> that selects <c>goals[idx]</c> (0x0055ACCE..0x0055ACEA).</summary>
    public int GoalIndex { get=>_goalIndex.Value; set=>_goalIndex.Value=unchecked((byte)value); }
    /// <summary><c>[this+0x90]</c>/<c>[this+0x94]</c>: the x and y position tolerance when <see cref="PreActionObjectPose"/> is null.</summary>
    public double DistanceToleranceMm { get; set; } = DefaultGoalDistanceToleranceMm;
    /// <summary>
    /// <c>[this+0xB4]</c> with <c>[this+0xC0] != 0</c> (M12-020, M12-023): the object pose the goal belongs to.
    /// When set, the Ready-state tolerance x/y come from <c>ComputePreActionPoseDistThreshold(out, goals[idx],
    /// objectPose, [this+0x9C])</c> (0x0055ACF0). The engine sets <c>+0xC0 = 1</c> only in
    /// <c>DriveToObjectAction::InitHelper</c> (0x0055933E) and 0 in the constructor (0x0055A31E).
    /// </summary>
    public Pose3d? PreActionObjectPose { get; set; }
    /// <summary><c>[this+0x9C]</c>: the one angle tolerance, used both by the threshold call (0x0055ACF0) and by <c>IsSameAs</c> (0x0055ADA8).</summary>
    public double AngleToleranceRad { get; set; } = GoalAngleToleranceRad;
    /// <summary><c>[this+0xA8]</c>, seconds.</summary>
    public double PlanningTimeoutSec { get; set; } = DefaultComputingPathTimeoutSec;
    /// <summary><c>[this+0xB0]</c>: the ComputingPath deadline in seconds, -1.0 when unset.</summary>
    public double DeadlineSec { get; private set; } = -1.0;
    /// <summary><c>[this+0xC4]</c>, incremented at path state 3.</summary>
    public int FollowingPathTicks { get; private set; }
    // fidelity: M5-063, M12-024 — the same shared handler is used by Init and every lifecycle tick.
    public IDrivingAnimationHandler DrivingAnimations { get; set; }
    /// <summary><c>PathComponent::Abort</c> (0x0055AD5C).</summary>
    public Action AbortPath { get; set; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    private Pose3d GoalAtIndex()
    {
        if (Goals is { Count: > 0 } g && GoalIndex >= 0 && GoalIndex < g.Count) return g[GoalIndex];
        return Goal ?? throw new InvalidOperationException("DriveToPoseAction has no goal");
    }

    /// <summary>
    /// <c>DriveToPoseAction::CheckIfDone</c> 0x0055AB4C, one call (M12-023). The ActionList entry delegates here
    /// when native inputs exist; the explicit fixture provider is not a production PathComponent substitute.
    /// <list type="bullet">
    /// <item><c>[[robot+0x24C]] == 3</c> returns RUNNING (0x0055AB5C..0x0055AB6A); a path status above 4 returns RUNNING (0x0055AB74).</item>
    /// <item>The initial result is 0x03000008 (0x0055AB7A).</item>
    /// <item>State 0 Failed: <c>[+0xB0] = -1</c>, 0x03000013, no end animation. State 1 ComputingPath: an unset deadline becomes
    ///   now+<c>[+0xA8]</c> and returns RUNNING, a future deadline returns RUNNING, otherwise <c>PathComponent::Abort</c>,
    ///   deadline -1 and 0x03000013. State 2 returns RUNNING. State 3: <c>PlayStartAnim</c>, deadline -1, <c>[+0xC4]++</c>, RUNNING.</item>
    /// <item>State 4 Ready: see <see cref="ReadyState"/>.</item>
    /// <item>Tail (0x0055B0AA..0x0055B0CC): a result other than RUNNING and 0x03000013 calls <c>PlayEndAnim</c>; non-zero keeps RUNNING.</item>
    /// </list>
    /// </summary>
    // fidelity: M12-023
    public ActionResult CheckIfDone(DriveToPoseTick tick)
    {
        if(!ExecutionCurrent())return ActionResult.CancelledWhileRunning;
        var result = ActionResult.FollowingPathButNotTraversing;
        int drivingState=DrivingAnimations.State;
        if(!ExecutionCurrent())return ActionResult.CancelledWhileRunning;
        if (drivingState == 3) return ActionResult.Running;
        if ((uint)tick.PathStatus > 4) return ActionResult.Running;
        switch ((DriveToPoseStatus)tick.PathStatus)
        {
            case DriveToPoseStatus.Failed:
                DeadlineSec = -1.0;
                _m.Robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","DriveToPoseAction.CheckIfDone.Failure","Robot driving to pose failed");
                return ActionResult.PathPlanningFailedAbort;
            case DriveToPoseStatus.ComputingPath:
                if (DeadlineSec < 0) { DeadlineSec = (float)tick.NowSec + (float)PlanningTimeoutSec; return ActionResult.Running; }
                if ((float)tick.NowSec < (float)DeadlineSec) return ActionResult.Running;
                _m.Robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","DriveToPoseAction.CheckIfDone.ComputingPathTimeout",
                    FormattableString.Invariant($"Robot has been planning for more than {(float)PlanningTimeoutSec:F6} seconds, aborting"));
                if(!ExecutionCurrent())return ActionResult.CancelledWhileRunning;
                AbortPath();
                if(!ExecutionCurrent())return ActionResult.CancelledWhileRunning;
                DeadlineSec = -1.0;
                return ActionResult.PathPlanningFailedAbort;
            case DriveToPoseStatus.WaitingToBeginPath:
                return ActionResult.Running;
            case DriveToPoseStatus.FollowingPath:
                if(_scopedOwner is {} scoped && DrivingAnimations is Animation.DrivingAnimationHandler actual)actual.PlayStartAnimOwned(scoped,ExecutionCurrent);
                else DrivingAnimations.PlayStartAnim();
                if(!ExecutionCurrent())return ActionResult.CancelledWhileRunning;
                DeadlineSec = -1.0;
                int previous=FollowingPathTicks; FollowingPathTicks=unchecked(previous+1);
                if(previous%10==0)
                    _m.Robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","DriveToPoseAction.CheckIfDone.WaitingForPathCompletion",
                        $"[{unchecked((int)Tag)}] Waiting for robot to complete its path traversal ({FollowingPathTicks}), _currPathSegment={tick.CurrentPathSegment}, _lastSentPathID={tick.PathIdAt0x42}, _lastRecvdPathID={tick.PathIdAt0x44}.");
                return ActionResult.Running;
            default:
                result = ReadyState(GoalAtIndex(), tick.RobotPose, tick.RobotHeightMm, tick.PathIdAt0x42, tick.PathIdAt0x44);
                if(!ExecutionCurrent())return ActionResult.CancelledWhileRunning;
                break;
        }
        if (!DrivingAnimations.InputsReady)
        { MissingSource("M5-063 current driving-animation selection is unavailable; end-call decision stopped"); return ActionResult.Running; }
        if (result != ActionResult.Running && result != ActionResult.PathPlanningFailedAbort)
        {
            uint end=_scopedOwner is {} scoped && DrivingAnimations is Animation.DrivingAnimationHandler actual
                ? actual.PlayEndAnimOwned(scoped,ExecutionCurrent) : DrivingAnimations.PlayEndAnim();
            if(!ExecutionCurrent())return ActionResult.CancelledWhileRunning;
            if(end!=0)result=ActionResult.Running;
        }
        return result;
    }

    /// <summary>
    /// State 4 (Ready) of <c>DriveToPoseAction::CheckIfDone</c> (0x0055ACA2..0x0055B0A6), before the tail:
    /// <c>[+0xB0] = -1</c>; the tolerance <c>Point3 {[+0x90], [+0x94], Robot::GetHeight}</c>; if <c>[+0xC0]</c> the x and y are
    /// replaced by BOTH outputs of <c>ComputePreActionPoseDistThreshold(out, goals[idx], this+0xB4, this+0x9C)</c>
    /// (0x0055ACF0, goal first and object second; the -1.0 sentinel pair is used as it comes back); arrival is
    /// <c>Pose3d::IsSameAs(robotPose, goals[idx], tolerance, [+0x9C])</c>; true gives 0, false gives 0x04000002 when the two
    /// path ids are equal and otherwise keeps 0x03000008 (0x0055AEC2..0x0055B0A6).
    /// </summary>
    // fidelity: M12-020, M12-023
    internal ActionResult ReadyState(Pose3d goal, Pose3d robotPose, double heightMm, ushort pathIdAt0x42, ushort pathIdAt0x44)
    {
        DeadlineSec = -1.0;
        double tx = DistanceToleranceMm, ty = DistanceToleranceMm;
        if (PreActionObjectPose is { } objectPose)
            CubePreActionPoses.DistanceThresholdMm(goal, objectPose, AngleToleranceRad, out tx, out ty);
        var tol = new Vec3(tx, ty, heightMm);
        // Pose3d::IsSameAs 0x00846EA4 uses a default-constructed empty RotationAmbiguities and compares
        // the full relative rotation (GetAngleDiffFrom 0x0084A694), so the full goal pose is passed.
        if (robotPose.IsSameAs(goal, tol, AngleToleranceRad, out _))
        {
            MissingSource("native Success info at 0x0055AE94: Robot ID and float translation-difference log inputs have no checked mapping; source log not fabricated");
            return ActionResult.Success;
        }
        if (pathIdAt0x42 != pathIdAt0x44)
        {
            _m.Robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"DriveToPoseAction.CheckIfDone.Failure","Robot is not at the goal and did not receive the last path");
            EngineErrorState.StoreAndMaybeBreak();
            return ActionResult.FollowingPathButNotTraversing;
        }
        MissingSource("native DoneNotInPlace info at 0x0055B030: complete float pose/angle/distance log argument mapping is unchecked; source log not fabricated");
        return ActionResult.FailedTraversingPath;
    }

    // fidelity: M12-024 — the real ActionList supplies TIMEOUT-before-check and category-4 retry/Init ordering.
    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        TimeoutSeconds=_m.DriveTimeoutForTest?.Invoke(this)??BitConverter.Int32BitsToSingle(0x41F00000);
        _done=new(TaskCreationOptions.RunContinuationsAsynchronously);
        AddCompletionCallback(result=>{if(result!=EngineActionResult.Running){_m.DriveResultObservedForTest?.Invoke((ActionResult)result);_done.TrySetResult((ActionResult)result);}});
        if(_m.Robot.Engine.Robot is not {} owner){MissingSource("ActionList recipient unavailable");throw new InvalidOperationException("MISSING: DriveToPose ActionList owner");}
        owner.ActionList.QueueAction(QueueActionPosition.Now,this,0);
        // Register AFTER queueing: even an already-cancelled token deletes the pending action under the list gate.
        using var cancellation=cancel.Register(()=>owner.ActionList.Cancel(Tag));
        return await _done.Task;
    }

    private double RobotHeightMm() => _m.RobotHeightMm();

}

/// <summary>
/// The C# stand-in for the <c>std::function</c> at <c>DriveToObjectAction+0x150</c> (M12-011, M12-029, M12-035): called with
/// the object, it returns the action result, the pre-action poses (the engine's vector arrives empty at both call sites this stack models) and the in-position flag
/// (<c>bool&amp;</c> at 0x00559A9C: an IN/OUT argument, because the flip-block installers read it, M12-035). The elements are <see cref="PreActionPose"/>s whose
/// <see cref="PreActionPose.WorldPose"/> is the engine's <c>Pose3d</c>.
/// </summary>
public delegate ActionResult PosesFunction(ObservableObject obj, out IReadOnlyList<PreActionPose> poses, ref bool alreadyInPosition);

/// <summary>
/// The engine's <c>DriveToObjectAction</c> (0x00558524..0x00559A00): <c>Init</c> looks the object up in the
/// world ("block world does not have an ActionableObject with ID"), <c>InitHelper</c> takes the object's
/// pre-action poses for the action type from the <c>std::function</c> at <c>+0x150</c> (<see cref="GetPossiblePoses"/> by default, M12-029), and unless the
/// robot is already in position runs a <c>DriveToPoseAction</c> to ALL the returned poses, then a <c>TurnTowardsObjectAction</c> unless the robot is carrying
/// the object (research Q12.8). The pre-action angle tolerance is the constructor's 7.5 degrees (0x3E060A92 at <c>+0x14C</c>).
///
/// This class is the native action's data holder: <c>NativeDriveToObjectRunner</c> runs it on the ActionList. The legacy public flow (RunAsync,
/// InitHelperAsync, the 4-arg ActionType-6 constructor and FromGotoObject, the legacy CheckIfDone, Chosen, ExcludePoses and Profile) was retired with the
/// legacy drive API (operator 2026-11-13, RETIRE-LEGACY-DRIVE-API-1). ActionType 6 (the 4-arg constructor 0x005587B8, M12-022/M12-011) has no built form.
/// </summary>
public sealed class DriveToObjectAction
{
    internal EngineRobot? NativeOwner;
    // fidelity: M12-011
    /// <summary><c>+0x14C</c>: Radians 0x3E060A92 (7.5 degrees), the ctor's <c>GetPossiblePoses</c> angle tolerance (research Q12.6, 0x0055850C).</summary>
    public static readonly double PreActionAngleToleranceRad = BitConverter.UInt32BitsToSingle(0x3E060A92);
    /// <summary>
    /// The ActionType <c>DriveToPlaceCarriedObjectAction</c>'s constructor 0x00559CD4 passes to the 7-arg base: 2 when
    /// its first bool is non-zero (0x00559CDE/0x00559CE4/0x00559CE8); its only caller passes 1 there
    /// (0x00554B40/0x00554B42), so production is always 2 (M12-022; the class is <see cref="DriveToPlaceCarriedObjectAction"/>, M12-030).
    /// </summary>
    public const PreActionType DriveToPlaceCarriedObjectActionType = PreActionType.PlaceOnGround;

    private readonly ManipulationSystem _m;

    /// <summary>The 7-arg constructor 0x0055850C: <paramref name="type"/> is <c>+0x80</c>, <c>+0x84</c> is -1.0 and <c>+0x148</c> is 1 (inventory G3.9); the default <c>+0x150</c> function is installed (0x005585F8).</summary>
    public DriveToObjectAction(ManipulationSystem m, uint objectId, PreActionType type)
    {
        _m = m; ObjectId = objectId; ActionType = type;
        PosesFunction = GetPossiblePoses;
    }

    public uint ObjectId { get; internal set; }
    /// <summary><c>+0x80</c>.</summary>
    public PreActionType ActionType { get; }
    public PreActionType Type => ActionType;
    /// <summary><c>+0x8C</c>. The native runner passes it as the inner DriveToPoseAction's manual flag (D4 0x005592E8..0x005592FE). The internal setter is a test substitution only:
    /// no native constructor takes the 7-arg ctor's useManualSpeed argument.</summary>
    public bool UseManualSpeed { get; internal set; }
    /// <summary>
    /// <c>+0x88</c>, <c>+0x13C</c>, <c>+0x140</c>: the 7-arg constructor's distanceFromMarker, useApproachAngle and approach angle, which feed
    /// <c>PreActionPoseInput+0x10/+0x14/+0x18</c> (research Q12.6). The callers' values are not itemised (M12-022 (b)); the C# callers build the action
    /// without them, so they default to 0, false and 0 as they always did.
    /// </summary>
    public double DistanceFromMarkerMm { get; set; }
    public bool UseApproachAngle { get; set; }
    public double ApproachAngleRad { get; set; }
    /// <summary><c>+0x148</c>: 1 from both constructors (inventory G3.9; 11.1). Zero makes <c>NativeDriveToObjectRunner.CheckIfDone</c> return 0.</summary>
    public bool CheckAtEnd { get; set; } = true;
    /// <summary>The <c>std::function</c> at <c>+0x150</c> (M12-011). The default is <see cref="GetPossiblePoses"/> (M12-029).</summary>
    // fidelity: M12-028
    public PosesFunction PosesFunction { get; set; }
    /// <summary>
    /// The <c>DriveToHelper</c> functor (0x005B58C2 installs it, operator() 0x005B61A2; M12-029): <see cref="GetPossiblePoses"/>, then, when it returns 0,
    /// <c>RemoveMatchingPredockPose</c> for the pose at <c>DriveToHelper+0x114</c> with NO minimum pose count, clearing the in-position flag if it removed something.
    /// NO PRODUCTION CALLER: <c>DriveToHelper</c> (M8) is not built here, so only a test sets it.
    /// </summary>
    // fidelity: M12-029
    public Pose3d? DriveToHelperMatchPose
    {
        get => _driveToHelperMatch;
        set { _driveToHelperMatch = value; PosesFunction = _driveToHelperMatch is not null ? PosesWithExclusions : GetPossiblePoses; }
    }
    private Pose3d? _driveToHelperMatch;
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    /// <summary>
    /// <c>DriveToObjectAction::GetPossiblePoses(obj, poses, inPos&amp;)</c> 0x00558C80 (M12-029; the default functors 0x0055DB72 / 0x0055DBDE tail-call it): builds
    /// the <c>PreActionPoseInput</c> {obj, ActionType, flag A 0, <c>[+0x14C]</c>, <c>[+0x88]</c>, <c>[+0x13C]</c>, <c>[+0x140]</c>} and calls
    /// <c>IDockAction::GetPreActionPoses</c> (M12-031); a non-zero result is returned; an empty pose list warns "did not return any pre-action poses" and returns
    /// 0x03000010; <c>*inPos</c> is the output's in-position byte (<c>out+0x1C</c>); every pose is pushed in order (with <c>inPos</c> set the engine only logs
    /// "close enough ... using current robot pose as goal" and replaces no pose); returns 0.
    /// </summary>
    // fidelity: M12-029, M12-031
    public ActionResult GetPossiblePoses(ObservableObject obj, out IReadOnlyList<PreActionPose> posesOut, ref bool alreadyInPosition)
    {
        posesOut = Array.Empty<PreActionPose>();
        // Q6 (RETIRE-LEGACY-DRIVE-API-1): only the native inputs remain; the legacy CopyPreActionRobotPose/GetObstacles branch had no caller left.
        using var robot = _m.NativeMainSnapshot(NativeOwner);
        if (robot is null) return ActionResult.Abort;
        _trace.Add("NativePreActionInputs MAIN="+NativePoseTrace(robot)+" object="+NativePoseTrace(obj.NativePose));
        _m.World.SelectedObjectId=obj.ObjectId;
        var output = DockPreActionPoses.Get(new PreActionPoseInput(obj, ActionType, FlagA: false, PreActionAngleToleranceRad, DistanceFromMarkerMm, UseApproachAngle, ApproachAngleRad),
                                            robot.Value, (NativeOwner??_m.Robot.Engine.Robot)!.CarryingOwner!.CarriedObjectId,
                                            () => _m.NativeObstacles(robot.Value,NativeOwner), _trace);
        if (output.Result != ActionResult.Success) return output.Result;
        if (output.Poses.Count == 0)
        {
            _trace.Add($"DriveToObjectAction.CheckPreconditions.NoPreActionPoses: ActionableObject {obj.ObjectId} did not return any pre-action poses with action type {(int)ActionType}.");
            return ActionResult.NoPreActionPoses;
        }
        alreadyInPosition = output.InPosition;
        _trace.Add("NativePreActionGoals "+string.Join(";",output.Poses.Select(p=>NativePoseTrace(p.WorldPose))));
        _trace.Add($"GetPreActionPoses: {output.Poses.Count} pose(s), closest #{output.ClosestIndex} at |d|=({output.OffsetX:F1},{output.OffsetY:F1}), threshold=({output.ThresholdX:F1},{output.ThresholdY:F1}), inPosition={output.InPosition}");
        if (alreadyInPosition) _trace.Add("DriveToObjectAction.GetPossiblePoses.UseRobotPose: Robot's current pose is close enough to preAction pose");
        posesOut = output.Poses.ToList();
        return ActionResult.Success;
    }

    private static string NativePoseTrace(Pose3d pose)
    {
        using var root=pose.GetWithRespectToRoot();
        var t=pose.Translation;var r=root.Translation;
        return FormattableString.Invariant($"local({t.X:R},{t.Y:R},{t.Z:R}) root({r.X:R},{r.Y:R},{r.Z:R}) yaw{pose.AngleAroundZ:R} ")+pose.GetNamedPathToRoot(false);
    }

    /// <summary>The <c>+0x150</c> function when <see cref="DriveToHelperMatchPose"/> is set: <see cref="GetPossiblePoses"/> then the removal described there.</summary>
    // fidelity: M12-029
    private ActionResult PosesWithExclusions(ObservableObject obj, out IReadOnlyList<PreActionPose> posesOut, ref bool alreadyInPosition)
    {
        var result = GetPossiblePoses(obj, out posesOut, ref alreadyInPosition);
        if (result != ActionResult.Success) return result;
        var list = posesOut.ToList();
        bool removed = false;
        if (_driveToHelperMatch is { } match) removed |= DockPreActionPoses.RemoveMatchingPredockPose(match, list);
        if (removed) { alreadyInPosition = false; _trace.Add("Trying again with a different predock pose"); }
        posesOut = list;
        return result;
    }

}

/// <summary>
/// <c>DriveToPlaceCarriedObjectAction</c> (M12-030; re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q13, verified in
/// 20260929-R-VIS-verify-M12-gap2.md): drives to where a carried object is to be put down.
///
/// <para>Constructor 0x00559CD4: base <c>DriveToObjectAction(robot, carried id, &amp;type, 0.0f, false, 0.0f, useManualSpeed)</c> with ActionType 2 when the first bool
/// is non-zero and 1 otherwise (0x00559CDE..0x00559CE8; production is always 2, M12-022), <c>+0x16C</c> the pose, <c>+0x178</c>, <c>+0x179</c> and <c>+0x17C</c> the
/// remaining arguments. The PlaceObjectOnGround wire fields are 0x44 level, 0x45 useManualSpeed, 0x46 useExactRotation, 0x47 checkDestinationFree; those names are
/// INFERRED from the CLAD field order (research 13.1), so the C# names its arguments by engine offset: <c>+0x178</c> (<c>useExactRotation</c> by inference) and
/// <c>+0x179</c> (<c>checkDestinationFree</c> by inference). The carried id at construction is <c>[robot+0x284]+8</c>, or the invalid id when nothing is carried.</para>
///
/// <para><c>Init</c> 0x00559DB8: (1) no carried object is 0x03000011; (2) <c>[+0x7C]</c> becomes the carried id and the object is looked up by the constructor's id
/// (<c>+0x78</c>): missing is 0x03000004; (3) with <c>+0x178</c> the approach angle comes from <c>ComputePlacementApproachAngle</c> 0x005504A0: the OV05 checked calculation is built through <see cref="PlacementApproachAngle"/>; native IAction state/tag integration remains explicitly MISSING;
/// (4) a clone of the object is put at the placement pose (<c>InitPose</c>, Known) and <c>DriveToObjectAction::InitHelper(clone)</c> runs; <c>DriveToObjectAction::Init</c> is
/// NOT called (so no cube light). <c>ObservableObject::Clone</c> (ActiveCube thunk 0x004E3954 -&gt; 0x004E38D4) is <c>new ActiveCube(ObjectType)</c> and keeps NOTHING: the clone has
/// ObjectID -1 (here <see cref="uint.MaxValue"/>), ActiveID -1 and FactoryID 0 (0x0087660E, 0x004E0C4C..0x004E0C58), so the inner <c>GetPreActionPoses</c> does not see a carried
/// object (0x00550904 compares -1 with the carried id) and answers with the poses of the clone at the placement pose.</para>
///
/// <para><c>CheckIfDone</c> 0x00559FA4: the compound's result, replaced by 0x04000008 (PLACEMENT_GOAL_NOT_FREE) when <c>+0x179</c> is set and
/// <see cref="IsPlacementGoalFree"/> is false, on every tick regardless of state. <c>DriveToObjectAction::CheckIfDone</c> is not called, so there is no
/// in-position check afterwards.</para>
///
/// <para>The legacy async RunAsync (Init plus the legacy DriveToObjectAction.InitHelperAsync drive) was retired with the legacy drive API
/// (operator 2026-11-13, RETIRE-LEGACY-DRIVE-API-1); it had no caller. The constructor, <see cref="IsPlacementGoalFree"/>, <see cref="CheckIfDone"/> and the
/// static intersecting-objects query stay.</para>
/// </summary>
// fidelity: M12-030
public sealed class DriveToPlaceCarriedObjectAction
{
    /// <summary>
    /// <c>BlockWorld::FindLocatedIntersectingObjects(quad, out, padding, filter)</c> as <c>IsPlacementGoalFree</c> uses it (0x0055A070): true iff the result is empty. The default is
    /// <see cref="EngineFindLocatedIntersectingObjectsIsEmpty"/>; a test can supply another as an input.
    /// </summary>
    public delegate bool PlacementGoalQuery(ObservableObject carried, Pose3d placementPose, double padding);

    private readonly ManipulationSystem _m;

    public DriveToPlaceCarriedObjectAction(ManipulationSystem m, Pose3d placementPose, bool placeOnGroundType, bool flagAt0x178,
                                           bool useManualSpeed, bool flagAt0x179, double paddingAt0x17C)
    {
        _m = m;
        CarriedIdAtConstruction = m.Docking.Carrying.CarriedObjectId ?? uint.MaxValue;
        ActionType = placeOnGroundType ? PreActionType.PlaceOnGround : PreActionType.PlaceRelative;
        PlacementPose = placementPose; FlagAt0x178 = flagAt0x178; FlagAt0x179 = flagAt0x179; PaddingAt0x17C = paddingAt0x17C;
        FindLocatedIntersectingObjectsIsEmpty = EngineFindLocatedIntersectingObjectsIsEmpty;
    }

    /// <summary>
    /// <c>BlockWorld::FindLocatedIntersectingObjects</c> 0x00626870 with the default filter <c>IsPlacementGoalFree</c> passes (M12-030): the located objects of the current origin
    /// (no family, type or PoseState restriction, 0x0061EBF2..0x0061EE8A) except the ignored id (the carried object's); the reference quad is
    /// <c>carried-&gt;vtbl[0x50](carried, placementPose, 0.0)</c> and the predicate 0x0062750E is <c>q = o-&gt;vtbl[0x50](o, o-&gt;GetPose(), padding); q.Intersects(*ref)</c>, so the
    /// padding inflates the CANDIDATE's quad, and there is NO Z test. Returns true iff nothing intersects (the engine's <c>out.empty()</c>). Footprints are M13-007's
    /// (<see cref="Footprint"/>); a pair for which one of them cannot be computed (a non-cube, or a cube whose pose is not yaw-only) is decided by M13-023's LABELLED STAND-IN,
    /// <see cref="BlockConfigurationManager.PlanarStandIn"/> with its height test neutralised (this query has none): centres within half a cube in x, y. That differs from the engine.
    /// </summary>
    // fidelity: M12-030, M13-007, M13-023
    public bool EngineFindLocatedIntersectingObjectsIsEmpty(ObservableObject carried, Pose3d placementPose, double padding) =>
        StaticFindLocatedIntersectingObjectsIsEmpty(_m, carried, placementPose, padding, _trace);

    /// <summary>
    /// The same query as a static: <c>FindFreePoseInBeacon</c>'s obstacle test (0x005E0BEA..0x005E0C32, M15-019) is the same <c>vtable[0x50](carried, pose, 0.0f)</c> quad, a default filter ignoring the
    /// carried id, and <c>FindLocatedIntersectingObjects(quad, vec, 10.0f, filter)</c>, empty or not.
    /// </summary>
    // fidelity: M12-030, M15-019
    public static bool StaticFindLocatedIntersectingObjectsIsEmpty(ManipulationSystem m, ObservableObject carried, Pose3d placementPose, double padding, List<string>? trace)
    {
        bool refOk = Footprint.TryGetBoundingQuadXY(carried, placementPose, 0f, out var refQuad);
        var atPlacement = new ObservableObject(carried.ObjectId, carried.Type, carried.Markers) { Pose = placementPose, PoseState = PoseState.Known };
        foreach (var o in m.World.LocatedObjects)
        {
            if (o.ObjectId == carried.ObjectId) continue;                       // ignoreIDs {carried id}
            bool hit;
            if (refOk && Footprint.TryGetBoundingQuadXY(o, o.Pose, (float)padding, out var candQuad)) hit = candQuad!.Intersects(refQuad!);
            else
            {
                hit = BlockConfigurationManager.PlanarStandIn(atPlacement, o, onTop: true, double.PositiveInfinity);
                trace?.Add($"IsPlacementGoalFree: object {o.ObjectId} decided by the M13-023 planar stand-in ({(hit ? "intersects" : "clear")})");
            }
            if (hit) return false;
        }
        return true;
    }

    /// <summary><c>+0x78</c>: the carried object's id when the action was built.</summary>
    public uint CarriedIdAtConstruction { get; }
    /// <summary>The base constructor's ActionType: 2 (<see cref="PreActionType.PlaceOnGround"/>) when the first bool was set, else 1.</summary>
    public PreActionType ActionType { get; }
    /// <summary><c>+0x16C</c>.</summary>
    public Pose3d PlacementPose { get; }
    /// <summary><c>+0x178</c>: when set, <c>Init</c> asks <c>ComputePlacementApproachAngle</c> (OV05 / M12-039).</summary>
    public bool FlagAt0x178 { get; }
    /// <summary><c>+0x179</c>: when set, <c>CheckIfDone</c> fails with 0x04000008 while the placement goal is not free.</summary>
    public bool FlagAt0x179 { get; }
    /// <summary><c>+0x17C</c>: the padding <c>IsPlacementGoalFree</c> passes.</summary>
    public double PaddingAt0x17C { get; }
    /// <summary>The query behind <see cref="IsPlacementGoalFree"/>; the default is the visible stub.</summary>
    public PlacementGoalQuery FindLocatedIntersectingObjectsIsEmpty { get; set; }
    public IReadOnlyList<string> Trace => _trace;
    private readonly List<string> _trace = new();

    /// <summary><c>IsPlacementGoalFree</c> 0x0055A070: the carried object (missing: true), a default filter ignoring its own id, its quad at the placement pose with the padding; true iff nothing intersects.</summary>
    public bool IsPlacementGoalFree()
    {
        if (_m.Docking.Carrying.CarriedObjectId is not { } id || _m.World.GetLocatedObjectById(id) is not { } carried) return true;
        return FindLocatedIntersectingObjectsIsEmpty(carried, PlacementPose, PaddingAt0x17C);
    }

    /// <summary><c>CheckIfDone</c> 0x00559FA4 over the compound's result <paramref name="compoundResult"/>.</summary>
    public ActionResult CheckIfDone(ActionResult compoundResult)
    {
        if (FlagAt0x179 && !IsPlacementGoalFree())
        {
            _trace.Add("Placement goal is not free to drop the cube, failing with retry.");
            return ActionResult.PlacementGoalNotFree;
        }
        return compoundResult;
    }

}

/// <summary>
/// The angle helper of <c>ComputePlacementApproachAngle</c> 0x00550858..0x005508C4 (M12-030): <c>axis = RotationMatrix3d::GetRotatedParentAxis&lt;'Z'&gt;(R)</c> (+-1 X, +-2 Y, +-3 Z,
/// <see cref="RotatedParentAxis"/>), then <c>tbb</c> on <c>axis + 3</c> (table 04 0A 10 1A 1A 1F 24 at 0x00550872; an index above 6 takes the default arm): -3 gives
/// <c>-GetAngleAroundZaxis</c>, -2 <c>-GetAngleAroundYaxis</c>, -1 <c>-GetAngleAroundXaxis</c>, 0 and 1 <c>+GetAngleAroundXaxis</c>, 2 <c>+GetAngleAroundYaxis</c>, 3
/// <c>+GetAngleAroundZaxis</c>, any other value <c>+GetAngleAroundXaxis</c>. <c>GetAngleAroundZaxis</c> is M12-033's (<see cref="EngineRadians.GetAngleAroundZaxis"/>); the bodies
/// of <c>RotationMatrix3d::GetAngleAroundXaxis</c> and <c>GetAngleAroundYaxis</c> are built under M11-068 and used by default.
/// </summary>
// fidelity: M12-030, M12-039
public static class PlacementApproachAngle
{
    // fidelity: M12-039 — OV05 B1/B2; the output word is written only on the success tail.
    public static ActionResult Compute(ManipulationSystem m, Pose3d target, ref float output)
    {
        var engine = m.Robot.Engine;
        using var mutation = m.World.EnterPoseMutation();
        using var logs = Pose3d.UseSourceLog((l,c,k,t) => engine.EmitSourceLog(l,c,k,t), engine.Das.ForceFlushNow);
        if (m.Docking.Carrying.CarriedObjectId is not {} id)
        {
            engine.EmitSourceLog(EngineLogLevel.Info, "Actions", "ComputePlacementApproachAngle.NoCarriedObject", "");
            return (ActionResult)0x03000011;
        }
        var carried = m.World.GetLocatedObjectById(id);
        if (carried is null)
        {
            engine.EmitSourceLog(EngineLogLevel.Warning, null, "DriveToActions.ComputePlacementApproachAngle.NullObject", FormattableString.Invariant($"ObjectID={unchecked((int)id)}"));
            return (ActionResult)0x03000004;
        }
        int targetAxis = RotatedParentAxis.Get(target.Rotation, 2);
        int carriedAxis = RotatedParentAxis.Get(carried.NativePose.Rotation, 2);
        if (carriedAxis != targetAxis)
        {
            engine.EmitSourceLog(EngineLogLevel.Warning, null, "ComputePlacementApproachAngle.MismatchedUpAxes", FormattableString.Invariant($"Carried up axis: {carriedAxis} , target up axis: {targetAxis}"));
            return (ActionResult)0x0300000B;
        }
        using var relative = new Pose3d("");
        var robot = engine.Robot!;
        if (!Pose3d.GetWithRespectTo(carried.NativePose, robot.GetCheckedMainPose(), relative))
        {
            engine.EmitSourceLog(EngineLogLevel.Warning, null, "ComputePlacementApproachAngle.FailedToComputeObjectWrtRobotPose", "");
            return (ActionResult)0x03000005;
        }
        using var inverse = relative.Inverse();
        inverse.PreComposeWith(target);
        output = (float)Helper(inverse.Rotation);
        return ActionResult.Success;
    }

    public static double Helper(Mat3 r, Func<Mat3, double>? angleAroundX = null, Func<Mat3, double>? angleAroundY = null)
    {
        angleAroundX ??= matrix => matrix.AngleAroundX; // M11-068 checked RotationMatrix3d, 0x0084B370.
        angleAroundY ??= matrix => matrix.AngleAroundY; // M11-068 checked RotationMatrix3d, 0x0084B3CA.
        int axis = RotatedParentAxis.Get(r, 2);
        return axis switch
        {
            -3 => Radians.Rescale(-(float)EngineRadians.GetAngleAroundZaxis(r)),
            -2 => Radians.Rescale(-(float)angleAroundY(r)),
            -1 => Radians.Rescale(-(float)angleAroundX(r)),
            2 => angleAroundY(r),
            3 => EngineRadians.GetAngleAroundZaxis(r),
            _ => angleAroundX(r),                                            // 0, 1 and every other value
        };
    }
}

/// <summary>
/// The engine's <c>DriveStraightAction(robot, distance_mm, speed_mmps, shouldPlayAnimation)</c>: one line
/// segment along the robot's heading, executed as a path. A negative distance drives backwards.
/// </summary>
public readonly record struct DriveStraightTick(bool LastPathFailed, bool HasPathToFollow, bool IsActive);
// fidelity: M13-013 — S1/S2 checked handler user. Path creation/getters retain their separate gaps.
public sealed class DriveStraightAction : ActionRunner, IDisposable
{
    private readonly ManipulationSystem _m;
    private readonly EngineRobot? _nativeOwner;
    internal ushort? SourcePathIdForTest => _m.Robot.Motion.Path.Field42;
    private bool _started, _destroyed;
    private readonly float _nativeSpeed;
    internal TaskCompletionSource<(uint Result, bool OwnerRetired)>? AiNativeEnded;
    private readonly object _cleanupGate=new();
    private readonly HashSet<string> _missingMessages=new();
    private TaskCompletionSource<ActionResult>? _done;
    public DriveStraightAction(ManipulationSystem m, double distanceMm)
        : this(m,distanceMm,distanceMm<0?-80f:100f,true,false) { }
    public DriveStraightAction(ManipulationSystem m, double distanceMm, bool shouldPlayAnimation)
        :this(m,distanceMm,distanceMm<0?-80f:100f,shouldPlayAnimation,false){}
    public DriveStraightAction(ManipulationSystem m, double distanceMm, float speedMmps, bool shouldPlayAnimation = true)
        :this(m,distanceMm,speedMmps,shouldPlayAnimation,true){}
    private DriveStraightAction(ManipulationSystem m,double distanceMm,float speedMmps,bool shouldPlayAnimation,bool explicitSpeed) : base(8,4)
    { _m=m;DistanceMm=distanceMm;SpeedMmps=speedMmps;ShouldPlayAnimation=shouldPlayAnimation;DrivingAnimations=m.DrivingAnimationsForTest?.Invoke()??m.Robot.Animations.DrivingAnimations;SourceLog=(l,c,k,t)=>m.Robot.Engine.EmitSourceLog(l,c,k,t);Log=m.Robot.Engine.EmitStackLog;
      float speed=explicitSpeed?speedMmps:(float)distanceMm<0?-80f:100f;
      if(explicitSpeed){float epsilon=BitConverter.UInt32BitsToSingle(0x3727C5AC);
        if(speed < -epsilon){EmitSource(EngineLogLevel.Warning,null,"DriveStraightAction.Constructor.NegativeSpeed","");speed=-speed;}
        if((float)distanceMm < -epsilon)speed=-speed;}
      _nativeSpeed=speed; }
    internal DriveStraightAction(ManipulationSystem m,EngineRobot owner,double distanceMm,float speedMmps,bool shouldPlayAnimation=true)
        :this(m,distanceMm,speedMmps,shouldPlayAnimation){_nativeOwner=owner;}
    private bool NativeOwnerCurrent()=>_nativeOwner is null || !_nativeOwner.Lifetime.Destroying && ReferenceEquals(_m.Robot.Engine.Robot,_nativeOwner);
    private bool ExecutionCurrent()=>!DestructionObserved && !_destroyed && NativeOwnerCurrent();
    public double DistanceMm { get; }
    public float SpeedMmps { get; }
    public bool ShouldPlayAnimation { get; }
    public IDrivingAnimationHandler DrivingAnimations { get; set; }
    protected override float EngineClockSeconds=>_m.Robot.Engine.Timer.SecondsF;
    protected override bool AreAnyTracksLocked(uint mask)=>NativeOwnerCurrent()&&_m.Robot.Motion.AreAnyTracksLocked((byte)mask);
    protected override void LockTracks(uint mask,string owner){if(NativeOwnerCurrent())_m.Robot.Motion.LockTracks((byte)mask,owner,"DriveStraight");}
    protected override void UnlockTracksInternal(uint mask,string owner)
    {if(_nativeOwner is null)_m.Robot.Motion.UnlockTracks((byte)mask,owner);else LogCallbackDeferral.RunGated(_cleanupGate,()=>{if(NativeOwnerCurrent())_m.Robot.Motion.UnlockTracks((byte)mask,owner);});}
    protected override bool AreAllTracksLockedBy(uint mask,string owner)=>NativeOwnerCurrent()&&_m.Robot.Motion.AreAllTracksLockedBy((byte)mask,owner);
    protected override bool BodyTrackMoving=>_m.Robot.Motion.BodyMoving;
    protected override void StopBodyTrack()
    {if(_nativeOwner is null)_m.Robot.Motion.StopBody();else LogCallbackDeferral.RunGated(_cleanupGate,()=>{if(NativeOwnerCurrent())_m.Robot.Motion.StopBody();});}
    protected override string ActionName => "DriveStraight"; // thirteen bytes at 0x00BEA0DC.
    public override byte? GetCompletionVariant()=>6; // type8 ctor 0x0053FF0E..14, Set_defaultCompleted.
    public override bool CanInterrupt()=>false; // 0x0052B0B3.
    private void Missing(string text){if(_missingMessages.Add(text))_m.Robot.Engine.EmitStackLog("MISSING: M13-013 | "+text);}
    public override int Init()
    {
        if(!ExecutionCurrent())return 0x03000016;
        DrivingAnimations.Init((byte)RequiredTrackMask,Tag,SuppressTrackLocking,false);_started=false;
        if(!ExecutionCurrent())return 0x03000016;
        if(MathF.Abs((float)DistanceMm)<BitConverter.Int32BitsToSingle(0x3727C5AC)){_started=true;return 0;} // 0x0054761E..26.
        var robot=_nativeOwner??_m.Robot.Engine.Robot;
        if(robot is null){Missing("actual pose/path Init owner unavailable");return 0;}
        float x,y,endX,endY;
        using(LogCallbackDeferral.EnterGated(robot.PoseTreeGate))
        {
            // 0x0054762A/3C: main-pose yaw, independently stored forward-pose XY.
            float yaw=(float)robot.GetCheckedMainPose().AngleAroundZ, distance=(float)DistanceMm;
            var forward=robot.GetCheckedForwardPose();x=(float)forward.Translation.X;y=(float)forward.Translation.Y;
            float dx=MathF.Cos(yaw)*distance,dy=MathF.Sin(yaw)*distance;
            endX=x+dx;endY=y+dy;
        }
        if(!ExecutionCurrent())return 0x03000016;
        // Fresh native Path count0, one AppendLine: the signed-negative-count branch
        // is unreachable for this producer (0x005476AE), without a fabricated failure.
        Func<RobotMessage,bool>? send=_nativeOwner is {} captured?message=>_m.Paths.SendOwned(captured,ExecutionCurrent,message):null;
        int result=_m.Robot.Motion.Path.ExecuteCustomPath(new PathSegment[]{new PathSegment.Line(x,y,endX,endY,_nativeSpeed,200f,500f)},false,current:ExecutionCurrent,send:send);
        return result==0?0:0x03000016; // P14.
    }
    public ActionResult CheckIfDone(DriveStraightTick tick) => CheckIfDone(()=>tick);
    private ActionResult CheckIfDone(Func<DriveStraightTick> read)
    {
        if(!ExecutionCurrent())return ActionResult.Abort;
        if(DrivingAnimations.State==3)return ActionResult.Running;
        if(read().LastPathFailed)return ActionResult.FailedTraversingPath;
        if(!_started)
        {
            EmitSource(EngineLogLevel.Info,"Actions","DriveStraightAction.CheckIfDone.WaitingForPathStart","");
            if(!ExecutionCurrent())return ActionResult.Abort;
            _started=read().HasPathToFollow;
            if(_started){EmitSource(EngineLogLevel.Debug,"Actions","DriveStraightAction.CheckIfDone.PathJustStarted","");if(!ExecutionCurrent())return ActionResult.Abort;}
            if(_started && ShouldPlayAnimation){DrivingAnimations.PlayStartAnim();if(!ExecutionCurrent())return ActionResult.Abort;}
            if(!_started)return ActionResult.Running;
        }
        if(read().IsActive)return ActionResult.Running;
        EmitSource(EngineLogLevel.Debug,"Actions","DriveStraightAction.CheckIfDone.PathJustCompleted","");
        if(!ExecutionCurrent())return ActionResult.Abort;
        if(ShouldPlayAnimation)
        {
            if(!DrivingAnimations.InputsReady){Missing("M5-063 driving selection unavailable");return ActionResult.Running;}
            uint end=DrivingAnimations.PlayEndAnim();if(!ExecutionCurrent())return ActionResult.Abort;
            if(end!=0)return ActionResult.Running;
        }
        return ActionResult.Success;
    }
    public override uint CheckIfDone()
    {
        if(!ExecutionCurrent())return (uint)ActionResult.Abort;
        if(_m.StraightTickForTest is {} input)return (uint)CheckIfDone(input(this));
        var path=_m.Robot.Motion.Path;
        return (uint)CheckIfDone(()=>new DriveStraightTick(path.LastPathFailed,path.HasPathToFollow,path.IsActive));
    }
    public async Task<ActionResult> RunAsync(CancellationToken cancel)
    {
        TimeoutSeconds=_m.StraightTimeoutForTest?.Invoke(this)??BitConverter.Int32BitsToSingle(0x41F00000);
        _done=new(TaskCreationOptions.RunContinuationsAsynchronously);AddCompletionCallback(r=>{if(r!=EngineActionResult.Running)_done.TrySetResult((ActionResult)r);});
        var owner = _nativeOwner ?? _m.Robot.Engine.Robot;
        if(owner is null){Missing("ActionList owner absent");throw new InvalidOperationException("MISSING: DriveStraight ActionList owner");}
        if (_nativeOwner is not null)
        {
            if (!ExecutionCurrent()) { RetireNeverAdmittedTree(); return ActionResult.Abort; }
            if (owner.ActionList.QueueActionGuardedNow(this, 0, ExecutionCurrent) != 0)
            {
                if (!DestructionObserved && CanDetachWatcherForRetirement()) RetireNeverAdmittedTree();
                return ActionResult.Abort;
            }
        }
        else owner.ActionList.QueueAction(QueueActionPosition.Now,this,0);
        using var cancellation=cancel.Register(()=>
        {
            if (_nativeOwner is null) owner.ActionList.Cancel(Tag);
            else
            {
                using var lease = owner.Engine.EnterSynchronousOwner();
                if (NativeOwnerCurrent() && owner.ActionList.ContainsRunner(this)) owner.ActionList.Cancel(Tag);
            }
        });
        return await _done.Task;
    }

    public void Dispose()
    {
        if(_destroyed)return;_destroyed=true;
        if(NativeOwnerCurrent() && (_m.PathIsActiveForTest?.Invoke()??_m.Robot.Motion.Path.IsActive))
        {if(_nativeOwner is {} owner)_m.Paths.AbortOwned(owner,NativeOwnerCurrent);else _m.Paths.Abort();}

        if(NativeOwnerCurrent())
        {
            if(_nativeOwner is {} owner && DrivingAnimations is Animation.DrivingAnimationHandler actual)
                actual.ActionIsBeingDestroyed(owner,NativeOwnerCurrent);
            else DrivingAnimations.ActionIsBeingDestroyed();
        }
        base.WatcherEnding();
    }
    public override void WatcherEnding()
    {
        Dispose();
        _done?.TrySetResult((ActionResult)State);
        if(AiNativeEnded is { } ended && _nativeOwner is { } owner)
            ended.TrySetResult((State,owner.Lifetime.Destroying || !ReferenceEquals(_m.Robot.Engine.Robot,owner)));
    }
}
