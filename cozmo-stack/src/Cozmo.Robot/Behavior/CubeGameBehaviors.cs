using Cozmo.Protocol;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// RETIRE-LEGACY-DRIVE-API-1 (operator 2026-11-13): the legacy KnockOverCubesBehavior, PopAWheelieBehavior (drive step: the retired
// DriveAndFlipBlockAction.RunAsync / DriveToObjectAction.RunAsync) are removed; the factory binds NativeKnockOverCubesBehavior and
// NativePopAWheelieBehavior for the configured ids.

/// <summary>
/// <c>BehaviorRamIntoBlock</c> (reactions/ramIntoBlock.json): when carrying, turn towards the target
/// (<c>TurnInPlaceAction</c> by the vector to it) and <c>PlaceObjectOnGroundAction</c>; otherwise
/// <c>TurnTowardsObjectAction</c> and <c>MoveLiftToHeightAction</c> (the low dock preset, INFERRED), then the
/// ram: <c>DriveStraightAction</c> for the distance to the block at 100 mm/s in parallel with
/// <c>TriggerAnimationAction</c> 0x20B <see cref="AnimationTrigger.SoundOnlyRamIntoBlock"/>, then
/// <c>DriveStraightAction(−100)</c>. The rammed block's pose is marked dirty (INFERRED: it moved).
/// </summary>
public sealed class RamIntoBlockBehavior : ManipulationBehavior
{
    public enum Phase { Idle, PuttingDown, Aligning, Ramming, BackingUp }
    public const float RamSpeedMmps = 100f;
    public const double BackUpMm = -100.0;

    public RamIntoBlockBehavior(ManipulationSystem m, string id = "RamIntoBlock") : base(id, "RamIntoBlock", m) { }
    internal void BindManipulation(ManipulationSystem manipulation) => M = manipulation;

    public Phase CurrentPhase { get; private set; }
    public uint? TargetObjectId { get; private set; }

    /// <summary>
    /// The target the NoPreDockPoses reaction hands over, BehaviorRamIntoBlock+0x11C
    /// (ReactionTriggerStrategyNoPreDockPoses::ShouldTriggerBehaviorInternal 0x00610E64).
    /// </summary>
    public uint? PendingTarget { get; set; }

    protected override bool IsRunnableInternal(BehaviorContext context) => ClosestCube() is not null;

    protected override void OnStart()
    {
        Scope.DisableReactions();
        var pending = PendingTarget; PendingTarget = null;
        var t = (pending is { } p ? M.World.GetLocatedObjectById(p) : null) ?? ClosestCube();
        if (t is null) { Finish(); return; }
        TargetObjectId = t.ObjectId;
        if (M.Docking.Carrying.IsCarryingObject)
        {
            var owner = M.Robot.Engine.Robot;
            CurrentPhase = Phase.PuttingDown;
            RunAction("turn and PlaceObjectOnGround", async ct =>
            {
                if (owner is null || owner.Lifetime.Destroying || !ReferenceEquals(M.Robot.Engine.Robot, owner)) return ActionResult.CancelledWhileRunning;
                await M.TurnTowardsObjectAsync(t.ObjectId, Math.PI, ct);
                if (owner.Lifetime.Destroying || !ReferenceEquals(M.Robot.Engine.Robot, owner)) return ActionResult.CancelledWhileRunning;
                return await M.PlaceObjectOnGroundAsync(owner, ct);
            }, _ => { if (owner is not null && !owner.Lifetime.Destroying && ReferenceEquals(M.Robot.Engine.Robot, owner)) Finish(); });
            return;
        }
        CurrentPhase = Phase.Aligning;
        RunAction("turn towards the block, lift low", async ct =>
        {
            await M.TurnTowardsObjectAsync(t.ObjectId, Math.PI, ct);
            return await new MoveLiftToHeightAction(M, LiftPresets.LowDockMm).RunAsync(ct);
        }, _ => Ram());
    }

    private void Ram()
    {
        CurrentPhase = Phase.Ramming;
        var obj = M.World.GetLocatedObjectById(TargetObjectId!.Value); var robot = M.RobotPose();
        if (obj is null || robot is null) { Finish(); return; }
        var d = obj.Pose.Translation - robot.Value.Translation;
        double dist = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        PlayTrigger(AnimationTrigger.SoundOnlyRamIntoBlock, () => { });
        RunAction($"DriveStraight({dist:F0} mm)", ct => new DriveStraightAction(M, dist, RamSpeedMmps).RunAsync(ct), _ =>
        {
            M.World.MarkDirty(TargetObjectId.Value);
            CurrentPhase = Phase.BackingUp;
            RunAction("DriveStraight(-100 mm)", ct => new DriveStraightAction(M, BackUpMm, RamSpeedMmps).RunAsync(ct), _ => { CurrentPhase = Phase.Idle; Finish(); });
        });
    }
}

// RETIRE-LEGACY-DRIVE-API-1 (operator 2026-11-13): the legacy CubeLiftWorkoutBehavior, BuildPyramidBaseBehavior (BuildPyramidBase and
// BuildPyramid) and RespondPossiblyRollBehavior (drive step: the retired DockHelper) are removed. The factory binds NativeCubeLiftWorkoutBehavior,
// NativeBuildPyramidBaseBehavior and NativeBuildPyramidBehavior; RespondPossiblyRoll has no native class (unbound).

/// <summary>
/// <c>BehaviorOnConfigSeen</c> (0x005DB0xx..0x005DB420; shipped as <c>RespondToPyramidBase</c> with
/// <c>configTriggers: [PyramidBase]</c>, <c>animTriggers: [BuildPyramidReactToBase]</c>): runnable when the block
/// configuration cache holds a configuration of a listed type first seen less than 5 s ago (4.99999,
/// 0x409FFFEB) that has not been reacted to; <c>TransitionToPlayAnimationSequence</c> plays the listed
/// lift-safe animations in order.
/// </summary>
public sealed class OnConfigSeenBehavior : ManipulationBehavior
{
    public const double RecentSec = 4.99999;
    private readonly HashSet<string> _reacted = new(); // optional host diagnostic metadata ONLY
    private readonly SortedDictionary<BlockConfigurationType,int> _nativeCounts=new();
    private float _previousEvaluationSeconds, _growthSeconds; // ctor memclr128..13F, 005DB112/114

    public OnConfigSeenBehavior(ManipulationSystem m, string id, IReadOnlyList<BlockConfigurationType> configTriggers, IReadOnlyList<AnimationTrigger> animTriggers)
        : base(id, "OnConfigSeen", m)
    {
        ConfigTriggers = configTriggers; AnimTriggers = animTriggers;
        foreach(var type in configTriggers)_nativeCounts[type]=0; // ReadJson5DB1CE also resets duplicates.
    }

    public static OnConfigSeenBehavior RespondToPyramidBase(ManipulationSystem m) =>
        new(m, "RespondToPyramidBase", new[] { BlockConfigurationType.PyramidBase }, new[] { AnimationTrigger.BuildPyramidReactToBase });

    public IReadOnlyList<BlockConfigurationType> ConfigTriggers { get; }
    public IReadOnlyList<AnimationTrigger> AnimTriggers { get; }
    public BlockConfiguration? Seen { get; private set; }

    private BlockConfiguration? Recent()
    {
        double now = M.ClockSec();
        foreach (var t in ConfigTriggers)
            foreach (var c in M.Configurations.Cache(t))
            {
                var k = c.Type + ":" + string.Join(",", c.BlockIds.OrderBy(i => i));
                if (_reacted.Contains(k)) continue;
                if (M.Configurations.FirstSeenSec(c) is { } seen && now - seen < RecentSec) return c;
            }
        return null;
    }

    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        var native=M.NativeConfigurations;
        if(native is null)return false; // availability guard; preserve native timer/count state for retry.
        float now=context.Robot.Engine.Timer.SecondsF;
        float elapsed=now-_previousEvaluationSeconds;
        _previousEvaluationSeconds=now;
        foreach(var type in _nativeCounts.Keys.ToArray())
        {
            int count=native.Snapshot.Cache(type).Count;
            if(count>_nativeCounts[type])_growthSeconds=now;
            _nativeCounts[type]=count;
        }
        return elapsed<BitConverter.UInt32BitsToSingle(0x409FFFEB)
            &&MathF.Abs(_growthSeconds-now)<BitConverter.UInt32BitsToSingle(0x3727C5AC);
    }

    protected override void OnStart()
    {
        // D1 host extension: this optional view never gates native admission or actions.
        try{Seen=Recent();}
        catch(Exception error){Seen=null;Log("host configuration view unavailable: "+error);}
        Log("native configuration count growth");
        if(Seen is not null)
        {
            _reacted.Add(Seen.Type + ":" + string.Join(",", Seen.BlockIds.OrderBy(i => i)));
            Log($"host configuration view: {Seen.Type} of {string.Join(",", Seen.BlockIds)}");
        }
        int i = 0;
        void Next() { if (i >= AnimTriggers.Count) { Finish(); return; } PlayTrigger(AnimTriggers[i++], Next); }
        Next();
    }
}

/// <summary>
/// <c>BehaviorCantHandleTallStack</c> (0x005ECD64..0x005ED370; config: <c>minimumStackHeight</c> 3,
/// <c>lookingInitialWait_s</c> 1, <c>lookingDownWait_s</c> 1, <c>lookingUpWait_s</c> 1, <c>minBlockMovedThreshold_mm</c> 20):
/// runnable when the tallest stack (<c>GetTallestStack</c>) is at least the minimum height and its bottom
/// block's pose is known (the unlock check is the app's). <c>InitInternal</c> records the stack pose;
/// <c>TransitionToLookingUpAndDown</c>: <c>WaitAction(initial)</c>, <c>MoveHeadToAngleAction(−25° (−0.436332), tol 2°)</c>,
/// <c>WaitAction(down)</c>, <c>MoveHeadToAngleAction(+45° (0.785398))</c>, <c>WaitAction(up)</c>; then
/// <c>TransitionToDisapointment</c> 0x005ED0F0 plays
/// <see cref="AnimationTrigger.CantHandleTallStack"/>: the <c>TriggerAnimationAction</c> at 0x005ED14E
/// carries trigger 0x1B, which is that name's place in the enum, so the guess by name was right.
/// <c>AlwaysHandle</c>: an <c>ObjectMoved</c> of a stack block past the threshold ends it.
/// </summary>
// fidelity: M15-012
public sealed class CantHandleTallStackBehavior : ManipulationBehavior,IDisposable
{
    public enum Phase { Idle, LookingUpAndDown, Disappointment }
    public const double HeadDownRad = -0.436332;
    public const double HeadUpRad = 0.785398;

    public CantHandleTallStackBehavior(ManipulationSystem m, string id = "CantHandleTallStack", int minimumStackHeight = 3,
                                       double lookingInitialWaitSec = 1, double lookingDownWaitSec = 1, double lookingUpWaitSec = 1, double minBlockMovedThresholdMm = 20)
        : base(id, "CantHandleTallStack", m)
    {
        MinimumStackHeight = minimumStackHeight; InitialWait = lookingInitialWaitSec; DownWait = lookingDownWaitSec; UpWait = lookingUpWaitSec; MovedThresholdMm = minBlockMovedThresholdMm;
    }

    private Action? _releaseCompletionOwner;
    private bool _disposed;
    public void Dispose(){if(_disposed)return;_disposed=true;Stop(BehaviorStopReason.Interrupted);_releaseCompletionOwner?.Invoke();_releaseCompletionOwner=null;}
    internal void TransitionToDisappointment()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        _releaseCompletionOwner??=AcquireActionCompletionOwner(M.Robot);
        CurrentPhase=Phase.Disappointment;
        Log("start CantHandleTallStack");
        StartActing(new Animation.TriggerAnimationAction(M.Robot,new(AnimationTrigger.CantHandleTallStack,1,true,0,60f,false)),null);
    }

    public int MinimumStackHeight { get; }
    public double InitialWait { get; } public double DownWait { get; } public double UpWait { get; }
    public double MovedThresholdMm { get; }
    public Phase CurrentPhase { get; private set; }
    private TallStackDelocalizationData _delocalization = new();
    internal void BindDelocalizationData(TallStackDelocalizationData data) => _delocalization = data;
    public StackOfCubes? TargetStack { get => _delocalization.Stack; private set => _delocalization.Stack = value; }
    private Dictionary<uint, Pose3d> _posesAtStart = new();

    private StackOfCubes? Tallest() { var s = M.NativeTallestStack(); return s is not null && s.StackHeight >= MinimumStackHeight && M.World.GetLocatedObjectById(s.BottomBlockId) is not null ? s : null; }

    protected override bool IsRunnableInternal(BehaviorContext context) => Tallest() is not null;
    //1029FEC→5BDA56: native disappointment Update is Running iff84 is nonzero, else Complete.
    protected override bool KeepsRunningWithoutAction => CurrentPhase!=Phase.Disappointment||HasCurrentAction;

    protected override void OnStart()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        TargetStack = Tallest();
        if (TargetStack is null) { Finish(); return; }
        _delocalization.HasStoredPose = true; // M7 checked InitInternal +0x124 write, 0x005ECE74.
        _posesAtStart = TargetStack.BlockIds.Select(id => M.World.GetObjectById(id)).Where(o => o is not null).ToDictionary(o => o!.ObjectId, o => o!.Pose);
        CurrentPhase = Phase.LookingUpAndDown;
        Wait(InitialWait, () =>
        {
            _ = M.Robot.Motion.SetHeadAngleAsync((float)HeadDownRad, CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2, requireCalibration: false);
            Wait(DownWait, () =>
            {
                _ = M.Robot.Motion.SetHeadAngleAsync((float)HeadUpRad, CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2, requireCalibration: false);
                Wait(UpWait, () =>
                {
                    TransitionToDisappointment();
                });
            });
        });
    }

    protected override void OnUpdate()
    {
        if (TargetStack is not null && CurrentPhase == Phase.LookingUpAndDown && M.Configurations.DidAnyObjectsMovePastThreshold(TargetStack, _posesAtStart, MovedThresholdMm))
        {
            Log("a stack block moved past the threshold; stopping");
            Finish();
        }
    }
}

/// <summary>
/// <c>BehaviorCheckForStackAtInterval</c> (0x005D7328..0x005D7BD0; config <c>delayBetweenChecks</c> 15 s):
/// runnable every interval while it knows cubes (<c>UpdateTargetBlocks</c>: the located cubes).
/// <c>TransitionToSetup</c> records the robot pose; <c>TransitionToFacingBlock</c>: <c>TurnTowardsObjectAction</c>
/// at a known block; <c>TransitionToCheckingAboveBlock</c>: a ghost pose one block height above it
/// (<c>ObjectPoseConfirmer::SetGhostObjectPose</c>) and a <c>TurnTowardsObjectAction</c> that looks at it (the
/// head tilts up to the ghost); <c>TransitionToReturnToSearch</c>: a <c>PanAndTiltAction</c> back to the
/// recorded heading.
/// </summary>
public sealed class CheckForStackAtIntervalBehavior : ManipulationBehavior
{
    public enum Phase { Idle, Setup, FacingBlock, CheckingAboveBlock, ReturnToSearch }

    public CheckForStackAtIntervalBehavior(ManipulationSystem m, string id = "SparksCheckForStackAtInterval", double delayBetweenChecksSec = 15)
        : base(id, "CheckForStackAtInterval", m) => DelayBetweenChecksSec = delayBetweenChecksSec;

    public double DelayBetweenChecksSec { get; }
    public double? LastCheckSec { get; private set; }
    public Phase CurrentPhase { get; private set; }
    private Pose3d? _startPose;

    protected override bool IsRunnableInternal(BehaviorContext context) =>
        (LastCheckSec is null || M.ClockSec() - LastCheckSec >= DelayBetweenChecksSec) && ClosestCube() is not null && M.RobotPose() is not null;

    protected override void OnStart()
    {
        CurrentPhase = Phase.Setup;
        _startPose = M.RobotPose();
        var block = ClosestCube();
        if (block is null || _startPose is null) { Finish(); return; }
        CurrentPhase = Phase.FacingBlock;
        RunAction($"TurnTowardsObject({block.ObjectId})", ct => M.TurnTowardsObjectAsync(block.ObjectId, Math.PI, ct).ContinueWith(t => t.Result ? ActionResult.Success : ActionResult.Abort), _ =>
        {
            CurrentPhase = Phase.CheckingAboveBlock;
            var obj = M.World.GetLocatedObjectById(block.ObjectId);
            if (obj is null || M.Vision.Calibration is null) { Log("BehaviorCheckForStackAtInterval.TransitionToCheckingAboveBlock.NullObject"); ReturnToSearch(); return; }
            var ghost = obj.Pose.Translation + new Vec3(0, 0, CubeGeometry.CubeSizeMm);
            var robot = M.RobotPose()!.Value;
            double head = TurnTowardsPose.HeadAngleToSee(M.Vision.Calibration, robot, ghost);
            Log($"looking at the ghost pose above block {block.ObjectId}: head {head * 180 / Math.PI:F0} deg");
            M.Robot.Motion.SetHeadAngleAsync((float)head, CozmoMotion.ActionDefaultHeadSpeedRadPerSec, CozmoMotion.ActionDefaultHeadAccelRadPerSec2, requireCalibration: false);
            // as in PutDownBlock: the wait is for frames that arrive after the head moved, not for any frame ever
            int framesBefore = M.Vision.FramesProcessed;
            WaitUntil(() => M.Vision.FramesProcessed > framesBefore, 1.0, _ => ReturnToSearch(), "a frame looking above the block");
        });
    }

    private void ReturnToSearch()
    {
        CurrentPhase = Phase.ReturnToSearch;
        var robot = M.RobotPose();
        if (robot is null || _startPose is null) { Done(); return; }
        var p = PathMotionProfile.Default;
        var turn = new PathSegment.PointTurn(robot.Value.Translation.X, robot.Value.Translation.Y, _startPose.Value.AngleAroundZ, StraightLinePlanner.PointTurnToleranceRad,
                                             p.PointTurnSpeedRadPerSec, p.PointTurnAccelRadPerSec2, p.PointTurnDecelRadPerSec2, true);
        RunAction("PanAndTilt back", async ct =>
        {
            using var run = M.StartPath(new PathSegment[] { turn });
            var ev = await run.WaitAsync(TimeSpan.FromSeconds(5), ct);
            return ev == PathEventType.Completed ? ActionResult.Success : ActionResult.Timeout;
        }, _ => Done());
    }

    private void Done() { LastCheckSec = M.ClockSec(); CurrentPhase = Phase.Idle; Finish(); }
}

/// <summary>
/// <c>BehaviorReactToPyramid</c> (0x0060832C) and <c>BehaviorReactToStackOfCubes</c> (0x00609898): runnable when
/// the configuration cache holds a pyramid (or a stack) and the cooldown has passed; <c>InitInternal</c> only
/// arms the cooldown, <c>now + 100 s</c> (0x42C80000), and the behaviour completes. NATIVE: neither class plays
/// an animation itself in this build; they exist to mark the reaction for the manager and the app.
/// </summary>
public sealed class ReactToConfigurationBehavior : ManipulationBehavior
{
    public const double CooldownSec = 100.0;

    public ReactToConfigurationBehavior(ManipulationSystem m, string id, string behaviorClass, BlockConfigurationType type) : base(id, behaviorClass, m) => Type = type;
    public static ReactToConfigurationBehavior ReactToPyramid(ManipulationSystem m) => new(m, "ReactToPyramid", "ReactToPyramid", BlockConfigurationType.Pyramid);
    public static ReactToConfigurationBehavior ReactToStackOfCubes(ManipulationSystem m) => new(m, "ReactToStackOfCubes", "ReactToStackOfCubes", BlockConfigurationType.StackOfCubes);

    public BlockConfigurationType Type { get; }
    public double? NextAllowedSec { get; private set; }

    protected override bool IsRunnableInternal(BehaviorContext context) =>
        M.NativeCache(Type).Count > 0 && (NextAllowedSec is null || M.ClockSec() >= NextAllowedSec);

    protected override void OnStart()
    {
        NextAllowedSec = M.ClockSec() + CooldownSec;
        Log($"reacted to a {Type}; next allowed in {CooldownSec} s (the engine's InitInternal only arms this cooldown)");
        Finish();
    }
}

/// <summary>
/// <c>BehaviorThinkAboutBeacons</c> (THINK-ABOUT-BEACONS-1, re-analysis/research/20261029-think-about-beacons-1-read.md; class range
/// 0x005E5AF8..0x005E5F4E, vtable 0x01029A28). Config (L2/L3/L4): <c>newAreaAnimTrigger</c> is kept as a name and resolved only at Init;
/// <c>beaconRadius_mm</c> is an f32 (175 Hiking, 75 Sparks). Runnable iff the whiteboard has no beacon (R5); gates +0x20/+0x24/+0x28 all
/// refuse (R1..R3). Init logs, SelectNewBeacon adds one beacon at the robot's pose w.r.t. its root with the configured radius (I2, S1..S3),
/// then plays the trigger once as a plain TriggerAnimationAction with no completion callback (I3..I5). The base Update ends the behaviour
/// with its action (U1); Stop leaves the beacon (T1); Resume is the base one (P1). Beacon clearing (W1) is not this class's.
/// </summary>
// fidelity: M15-011
// fidelity: M15-084
public sealed class ThinkAboutBeaconsBehavior : ManipulationBehavior, IDisposable
{
    // I3: the literal AnimationTrigger::Count name; AnimationTriggerFromString yields Count (0x23F) for it and Init starts no action.
    private const string CountTriggerName = "Count";

    private readonly EngineRobot? _originalRobot;
    private long _generation;
    private int _retired;
    public ThinkAboutBeaconsBehavior(ManipulationSystem m, string id = "Hiking_ThinkAboutBeacons", float beaconRadiusMm = 175f, string newAreaAnimTrigger = "HikingReactToNewArea")
        :this(m,id,beaconRadiusMm,newAreaAnimTrigger,m.Robot.Engine.Robot) {}
    internal ThinkAboutBeaconsBehavior(ManipulationSystem m,string id,float beaconRadiusMm,string newAreaAnimTrigger,EngineRobot? originalRobot)
        : base(id, "ThinkAboutBeacons", m)
    {
        _originalRobot=originalRobot;
        // L2 0x005E5BB4..0x005E5BEA: the trigger string at +0x11C (C1 starts it empty); L3 0x005E5BEC..0x005E5BFA: f32 radius at +0x128.
        NewAreaAnimTrigger = newAreaAnimTrigger;
        BeaconRadiusMm = beaconRadiusMm;
        // I5: StartActing's completion arrives through the IBehavior ctor's RobotCompletedAction subscription (0x005BBD5E..7A).
        if(originalRobot is not null)BindActionCompletionOwner(m.Robot,originalRobot);
    }
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>M.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting()=>Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion()=>Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==version && _originalRobot is not null && ReferenceEquals(M.Robot.Engine.Robot,_originalRobot);
    protected override bool HostStopContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0 && Interlocked.Read(ref _generation)==unchecked(version+1) && ReferenceEquals(M.Robot.Engine.Robot,_originalRobot);
    protected override bool HostResumeContinuationCurrent(long version)=>Volatile.Read(ref _retired)==0 && (Interlocked.Read(ref _generation)==version || Interlocked.Read(ref _generation)==unchecked(version+1)) && ReferenceEquals(M.Robot.Engine.Robot,_originalRobot);
    protected override void OnStop(BehaviorStopReason reason)=>Interlocked.Increment(ref _generation);
    public void Dispose(){if(Interlocked.Exchange(ref _retired,1)!=0)return;Interlocked.Increment(ref _generation);RetireActionCompletionOwner();}

    /// <summary>L3: <c>beaconRadius_mm</c>, the f32 at +0x128.</summary>
    public float BeaconRadiusMm { get; }
    /// <summary>L2: <c>newAreaAnimTrigger</c>, the string at +0x11C; resolved only at Init (I3).</summary>
    public string NewAreaAnimTrigger { get; }
    /// <summary>The beacon the last Init added (S2), for diagnostics.</summary>
    public AIBeacon? Selected { get; private set; }

    // R1: base +0x20 0x005BF04C returns 0 (not runnable off treads). R2: base +0x24 0x0059EC12 returns 0 (not runnable while robot+0x34A).
    // R3: TAB's +0x28 0x005E5F4C returns 0 (not runnable while carrying).
    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => false;
    // U1: base UpdateInternal 0x005BDA56..0x005BDA62: Running while there is an action, else Complete.
    protected override bool KeepsRunningWithoutAction => HasCurrentAction;

    // R5 0x005E5CC2..0x005E5CD8: runnable iff GetActiveBeacon(whiteboard) is null; nothing else is read.
    protected override bool IsRunnableInternal(BehaviorContext context) => M.Whiteboard.GetActiveBeacon() is null;
    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner=EnterHostExecutionOwner(context);long generation=CaptureHostExecutionVersion();
        if(!HostExecutionVersionCurrent(generation)){MarkReadinessUnavailable();ReportMissing("M15-084 Think original Robot owner unavailable");return false;}
        bool result=base.IsRunnable(context);
        if(!HostExecutionVersionCurrent(generation)){MarkReadinessUnavailable();return false;}
        return result;
    }

    protected override void OnStart()
    {
        long generation=CaptureHostExecutionVersion();
        // fidelity: M15-011, M15-084
        // I1 0x005E5CF8..0x005E5D52: Info "Behaviors", name + ".InitInternal", "Selecting new beacon" (best effort).
        Log($"{Id}.InitInternal: Selecting new beacon");
        if(!HostExecutionVersionCurrent(generation))return;
        // I2 0x005E5D94..0x005E5D98: SelectNewBeacon, unconditionally.
        SelectNewBeacon(generation);
        if(!HostExecutionVersionCurrent(generation))return;
        // I3 0x005E5D9C..0x005E5DF4: an empty string or Count (0x23F) starts no action; Init still returns 0 (I6) and U1 completes on the next Update.
        if (ResolveTrigger() is { } trigger)
        {
            if(!HostExecutionVersionCurrent(generation))return;
            // I4 0x005E5DF6..0x005E5E18: TriggerAnimationAction(robot, trigger, 1, interruptRunning true, tracks 0, 60.0f 0x42700000, false); not lift-safe.
            var action = new TriggerAnimationAction(M.Robot, new(trigger, 1, true, 0, TriggerAnimationTimeoutSec, false));
            if(!HostExecutionVersionCurrent(generation)){action.RetireNeverAdmittedTree();return;}
            // I5 0x005E5E1C..0x005E5E3C: the RobotCompletedAction StartActing overload with an empty function: no callback.
            StartActing(action, null,()=>HostExecutionVersionCurrent(generation));
        }
        // I6 0x005E5E4A..0x005E5E50: Init returns 0 on every path.
    }

    // S1..S3, SelectNewBeacon 0x005E5F0C..0x005E5F3C: Robot::GetPose (0x004EA398, called at 0x005E5F1A), then GetWithRespectToRoot
    // (0x004DF628, called at 0x005E5F24), then AddBeacon(wb, pose, [this+0x128]) (0x005E5F28..0x005E5F36); AddBeacon 0x0056C39C stores
    // failure time 0 at +0x10. S5: no RNG, distance, memory map or limit.
    private void SelectNewBeacon(long generation)
    {
        if (_originalRobot is not { } owner || !HostExecutionVersionCurrent(generation))
        {
            ReportMissing("M15-084 S1 Robot::GetPose 0x004EA398 at 0x005E5F1A: no native Robot owner bound to this ManipulationSystem; no beacon added");
            return;
        }
        using var main = owner.CopyCheckedMainPose();
        if(!HostExecutionVersionCurrent(generation))return;
        using var root = main.GetWithRespectToRoot();
        if(!HostExecutionVersionCurrent(generation))return;
        var beacon=M.Whiteboard.AddBeacon(root, BeaconRadiusMm);
        if(HostExecutionVersionCurrent(generation))Selected=beacon;
    }

    private AnimationTrigger? ResolveTrigger()
    {
        // I3: empty string -> no action.
        if (NewAreaAnimTrigger.Length == 0) return null;
        // I3: Count (0x23F) -> no action.
        if (NewAreaAnimTrigger == CountTriggerName) return null;
        if (Enum.IsDefined(typeof(AnimationTrigger), NewAreaAnimTrigger))
            return Enum.Parse<AnimationTrigger>(NewAreaAnimTrigger);
        ReportMissing($"M15-084 I3 0x005E5D9C..0x005E5DF4: AnimationTriggerFromString's result for the unknown name '{NewAreaAnimTrigger}' is not settled by the THINK-ABOUT-BEACONS-1 read; no action started");
        return null;
    }
}

/// <summary>
/// <c>BehaviorExploreBringCubeToBeacon</c> (exports: <c>GetCandidate</c>, <c>TransitionToPickUpObject</c>,
/// <c>TransitionToObjectPickedUp</c>, <c>FindFreeCubeToStackOn</c>, <c>TryToStackOn</c>, <c>FindFreePoseInBeacon</c>,
/// <c>TryToPlaceAt</c>, <c>FireEmotionEvents</c>; config <c>recentFailureCooldown_sec</c> 45 hiking / 5 sparks):
/// runnable with an active beacon, not inside the failure cooldown of <c>AIBeacon::FailedToFindLocation</c> (M15-023) and a usable cube outside every beacon
/// (<c>FindUsableCubesOutOfBeacons</c>, excluding cubes that failed within the cooldown). Pick the cube up (<c>TransitionToPickUpObject</c> and its completion lambda: retry up to three
/// attempts, <c>SetFailedToUse</c> on an abort), then <c>TransitionToObjectPickedUp</c> 0x005DF3E4 decides: <c>FindFreeCubeToStackOn</c> first; a cube found
/// is stacked on (<c>TryToStackOn</c>), otherwise <c>NeedActionCompleted(PickupCube)</c> (M15-025) and the floor: <c>FindFreePoseInBeacon</c> (M15-019, M15-020), then
/// <c>TryToPlaceAt</c> (M15-021, M15-022) or the NoFreePoses branch (M15-023); the whiteboard's failure memory is M15-024.
///
/// MISSING (not built; each is reported through SteppedBehavior.ReportMissing, once per message per process, not once per behaviour instance):
/// <list type="bullet">
/// <item><c>DriveToPickupObjectAction</c> (ctor PLT 0x004A93B8, 0x100 bytes, ObjectID at this+0x128, trigger 0x23F; started by <c>TransitionToPickUpObject</c> 0x005DF95E..0x005DF98A through
/// <c>IBehavior::StartActing</c> PLT 0x004B01C8): this legacy host class starts no pick-up (its DockHelper stand-in was retired, RETIRE-LEGACY-DRIVE-API-1, operator 2026-11-13);
/// <see cref="TransitionToPickUpObject"/> reports it and starts nothing. The completion lambda 0x005E0FD8 (<see cref="PickUpCompleted"/>) is kept; NativeBringCubeToBeaconBehavior owns the live flow;</item>
/// <item><c>DriveToPlaceOnObjectAction</c> (0x005DFE0E; no record owns it) behind <c>TryToStackOn</c>; this legacy class starts nothing there either (stand-in retired);</item>
/// <item>the unread bodies the floor path reaches: <c>BlockWorld::ClearLocatedObjectByIDInCurOrigin</c> (after a failed verify) and <c>RotationMatrix3d::Renormalize</c> in the candidate pose's matrix.</item>
/// </list>
/// </summary>
// fidelity: M15-009, M15-008, M15-019, M15-020, M15-021, M15-022, M15-023, M15-024, M15-025
public sealed class BringCubeToBeaconBehavior : ManipulationBehavior
{
    private EngineRobot? _placeOwner;
    private Pose3d? _placePose;
    private Action? _releasePlaceCompletion;
    private long _placeGeneration;
    internal Action? PlacementPoseRetiredForTest;
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context)=>M.Robot.Engine.EnterSynchronousOwner();
    protected override bool KeepsRunningWithoutAction=>base.KeepsRunningWithoutAction || HasCurrentAction;
    public enum Phase { Idle, PickingUp, StackingOn, PlacingAt }

    public BringCubeToBeaconBehavior(ManipulationSystem m, string id = "Hiking_BringCubeToBeacon", double recentFailureCooldownSec = 45)
        : base(id, "BringCubeToBeacon", m) => RecentFailureCooldownSec = recentFailureCooldownSec;

    public double RecentFailureCooldownSec { get; }
    public Phase CurrentPhase { get; private set; }
    public uint? Candidate { get; private set; }

    /// <summary>
    /// The failure kinds <c>IsRunnableInternal</c> filters candidates by: the set built from the first two words of the table at 0x00C6D6A0 (= {0, 3, 1, 2}), i.e. {PickUpObject, RollOrPopAWheelie}
    /// (0x005DF1BA..0x005DF21C). The engine never filters candidates on PlaceObjectAt or StackOnObject.
    /// </summary>
    // fidelity: M15-024
    public static readonly ObjectActionFailure[] CandidateFailureSet = { ObjectActionFailure.PickUpObject, ObjectActionFailure.RollOrPopAWheelie };

    /// <summary>
    /// The candidate cubes of <c>IsRunnableInternal</c> 0x005DF1AA..0x005DF296: <c>AIWhiteboard::FindUsableCubesOutOfBeacons</c> (M15-020), each kept unless
    /// <c>DidFailToUse(id, {PickUpObject, RollOrPopAWheelie}, [this+0x130], its pose, 20.0f 0x41A00000, Radians 0x3EC90FDB)</c> (0x005DF250): only the failures recorded for that cube's id are consulted
    /// (id = [ObjectInfo+4], 0x005DF24E), and one matches by age and by POSE (within 20 mm and pi/8 of the cube's current pose).
    /// </summary>
    // fidelity: M15-020, M15-024
    private List<ObservableObject> UsableCandidates() =>
        M.Whiteboard.FindUsableCubesOutOfBeacons()
            .Where(o => !M.Whiteboard.DidFailToUse(unchecked((int)o.ObjectId), CandidateFailureSet, (float)RecentFailureCooldownSec, o.Pose, StackFilterDistMm, StackFilterAngleRad))
            .ToList();

    /// <summary>
    /// The vector at <c>[this+0x11C]</c>: cleared at the top of every <c>IsRunnableInternal</c> (0x005DF10C..0x005DF132) and filled by its candidate loop (0x005DF1AA..0x005DF296); <c>InitInternal</c> and
    /// <c>TransitionToPickUpObject</c> read the vector the latest runnable test built.
    /// </summary>
    // fidelity: M15-020
    private List<ObservableObject> _candidates = new();

    /// <summary><c>GetCandidate(blockWorld, index)</c> 0x005DFDA4: the located object of the vector's entry <paramref name="index"/> (null past the end or when the lookup fails).</summary>
    // fidelity: M15-020
    private ObservableObject? GetCandidate(uint index) =>
        index < _candidates.Count ? M.World.GetLocatedObjectById(_candidates[(int)index].ObjectId) : null;

    /// <summary>
    /// The emotion event a placed cube fires when others are still out:
    /// <c>BehaviorExploreBringCubeToBeacon::FireEmotionEvents</c> 0x005E002C takes this branch when
    /// <c>AIWhiteboard::AreAllCubesInBeacons</c> says no (0x005E0068).
    /// </summary>
    public const string CubeEmotionEvent = "HikingBroughtCubeToBeacon";

    /// <summary>And the one it fires when that was the last cube (0x005E0046).</summary>
    public const string LastCubeEmotionEvent = "HikingBroughtLastCubeToBeacon";

    /// <summary>
    /// <c>IsRunnableInternal</c> 0x005DF0FC: no active beacon is not runnable (0x005DF144); with <c>t = [beacon+0x10]</c> (the f32 stamp of <c>AIBeacon::FailedToFindLocation</c>) and <c>|t| &gt;= 1e-5f</c>
    /// (0x005DF14C..0x005DF16E), the behaviour is not runnable while <c>(t + [this+0x130]) + (-1e-5f) &gt; now</c> (0x005DF170..0x005DF194, f32). M15-023.
    /// </summary>
    // fidelity: M15-023, M15-020
    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        _candidates = new List<ObservableObject>();                                                           // 0x005DF10C..0x005DF132: the vector is cleared first
        var beacon = M.Whiteboard.GetActiveBeacon();
        if (beacon is null) return false;
        float t = beacon.FailedToFindLocationTimeSec;
        if (!(MathF.Abs(t) < BeaconFloorGeometry.OneEm5))
        {
            float now = (float)M.ClockSec();
            float limit = t + (float)RecentFailureCooldownSec;
            limit += BitConverter.Int32BitsToSingle(unchecked((int)0xB727C5AC));
            if (limit > now) return false;
        }
        // 0x005DF1A4..0x005DF2A6: FindUsableCubesOutOfBeacons, then the failure filter over its vector; runnable iff the filtered vector is non-empty. There is NO carrying test: while a cube is carried the
        // usable set is that cube (FindUsableCubesOutOfBeacons 0x0056AEA6..0x0056AEF2) and InitInternal resumes at the placement phase.
        _candidates = UsableCandidates();
        return _candidates.Count > 0;
    }

    /// <summary>
    /// <c>InitInternal</c> 0x005DF348 (M15-020): <c>[this+0x12C] = -1</c>; when <c>[[robot+0x284]+8] != -1</c> the target is <c>vector[0].id</c> (0x005DF364..0x005DF36C) and
    /// <c>TransitionToObjectPickedUp</c> runs DIRECTLY (0x005DF372): the behaviour resumes at the placement phase with no pick-up; otherwise <c>TransitionToPickUpObject(robot, 1)</c> (0x005DF37C). The result
    /// (0x005DF382..0x005DF3DA) is non-zero (the manager's InitFailed) when no action was started and the active beacon was not stamped by <c>FailedToFindLocation</c> just now
    /// (<c>|now - [beacon+0x10]| &gt;= 1e-5f</c>); a started action, or a NoFreePoses stamp made in this very call, is 0.
    /// </summary>
    protected override void OnStart()
    {
        _placeOwner=M.Robot.Engine.Robot;
        Interlocked.Increment(ref _placeGeneration);
        Scope.DisableReactions();
        Candidate = null;                                                                                    // [this+0x12C] = -1
        int startedBefore = _actionsStarted;
        if (_candidates.Count == 0) _candidates = UsableCandidates();                                          // CHOICE: the engine's vector is the runnable test's; a start without a non-empty one rebuilds it
        if (M.Docking.Carrying.CarriedObjectId is not null)
        {
            if (_candidates.Count > 0)
            {
                Candidate = _candidates[0].ObjectId;                                                          // 0x005DF364..0x005DF36C
                TransitionToObjectPickedUp();                                                                // 0x005DF372
            }
            else Finish();
        }
        else TransitionToPickUpObject(1);                                                                    // 0x005DF37C
        // 0x005DF382..0x005DF3DA
        if (_actionsStarted == startedBefore)
        {
            float stamp = M.Whiteboard.GetActiveBeacon()?.FailedToFindLocationTimeSec ?? float.NaN;
            float diff = MathF.Abs((float)M.ClockSec() - stamp);
            if (!(diff < BeaconFloorGeometry.OneEm5)) InitFailed = true;
        }
    }

    private int _actionsStarted;

    /// <summary>
    /// <c>TransitionToPickUpObject(robot, attempt)</c> 0x005DF8C0 (M15-020): an empty vector is the "NoCandidates" error (0x005DF9C6). With the target already set (<c>[this+0x12C] != -1</c>, the retry from
    /// the completion lambda, 0x005DF8E6) it logs ".TransitionToPickUpObject.Retry" "Trying to pick up '%d' again" (0x005DF8F2..0x005DF92A) and starts the action again. Otherwise (as <c>InitInternal</c> calls
    /// it with the target unset) the nearest candidate by the pose with respect to the robot's pose, <c>(x*x + y*y) + z*z</c> in f32 with no square root (0x005DFB3A..0x005DFB5E), starting from FLT_MAX
    /// (0x005DFCDC = 0x7F7FFFFF) and replacing when <c>best + (-1e-5f) &gt; d2</c> (0x005DFB66..0x005DFB84; a failed lookup or pose gives FLT_MAX); when nothing beat FLT_MAX (<c>vcmpe; bpl</c> 0x005DFB94) it
    /// is the "InvalidCandidates" error (0x005DFA18); else <c>[this+0x12C]</c> is that candidate's id ("Going to pick up '%d'", 0x005DFBB0). The action is then started with the completion lambda 0x005E0FD8
    /// (<see cref="PickUpCompleted"/>); see the class header for the unbuilt <c>DriveToPickupObjectAction</c>.
    /// </summary>
    private void TransitionToPickUpObject(int attempt)
    {
        if (_candidates.Count == 0)
        {
            Log($"error: BehaviorExploreBringCubeToBeacon.TransitionToPickUpObject.NoCandidates: Can't run with no selected objects");
            Finish(); return;
        }
        uint id;
        if (Candidate is { } already)
        {
            id = already;
            Log($"info: Behaviors.{Id}.TransitionToPickUpObject.Retry: Trying to pick up '{id}' again");
        }
        else
        {
            using var robot = M.CopyPreActionRobotPose();
            float best = BeaconFloorGeometry.FltMax;
            int bestIndex = 0;
            for (uint i = 0; i < _candidates.Count; i++)
            {
                float d2 = BeaconFloorGeometry.FltMax;
                if (GetCandidate(i) is { } o && robot is { } r)
                {
                    var t = o.Pose.WithRespectTo(r).Translation;
                    float x = (float)t.X, y = (float)t.Y, z = (float)t.Z;
                    d2 = x * x;
                    d2 += y * y;
                    d2 += z * z;
                }
                float limit = best + BitConverter.Int32BitsToSingle(unchecked((int)0xB727C5AC));
                if (limit > d2) { best = d2; bestIndex = (int)i; }
            }
            if (!(best < BeaconFloorGeometry.FltMax))
            {
                Log("error: BehaviorExploreBringCubeToBeacon.TransitionToPickUpObject.InvalidCandidates: Could not pick candidate");
                Finish(); return;
            }
            id = _candidates[bestIndex].ObjectId;
            Candidate = id;
            Log($"info: Behaviors.{Id}.TransitionToPickUpObject.Selected: Going to pick up '{id}'");
        }
        // RETIRE-LEGACY-DRIVE-API-1 (operator 2026-11-13): the legacy DockHelper stand-in for DriveToPickupObjectAction is retired. This legacy class is
        // kept for FindFreePoseInBeacon and its constants (NativeBringCubeToBeaconBehavior's floor search); the configured ids are bound to
        // NativeBringCubeToBeaconBehavior. No pick-up action is started here: the gap is reported, the phase and attempt count stay as StartActing would
        // leave them, and with no action the behaviour ends on its next Update (KeepsRunningWithoutAction); the completion lambda is not called.
        CurrentPhase = Phase.PickingUp;
        _actionsStarted++;
        SteppedBehavior.ReportMissing($"BehaviorExploreBringCubeToBeacon::TransitionToPickUpObject 0x005DF95E..0x005DF98A: the legacy host BringCubeToBeaconBehavior has no drive (its DockHelper stand-in was retired, RETIRE-LEGACY-DRIVE-API-1); no pick-up of '{id}' (attempt {attempt}) is started; NativeBringCubeToBeaconBehavior owns this flow");
    }

    /// <summary>
    /// The completion lambda 0x005E0FD8 of the pick-up action, by result category (the top byte, 0x005E0FE6..0x005E0FF6; the closure holds the behaviour, the robot and the attempt): 0 logs
    /// ".onPickUpActionResult.Done" "Picked up '%d'" and runs <c>TransitionToObjectPickedUp</c> (0x005E0FFA..0x005E10F4). 4 (RETRY): with the carried id equal to the target (strict [+8], not -1) it logs
    /// ".RetryOk" "We do have '%d' picked up, so pretend we are fine" and goes on to <c>TransitionToObjectPickedUp</c> (0x005E1086..0x005E10F4); else with <c>attempt &lt;= 2</c> (0x005E118E, signed) it logs
    /// ".RetryMaybe" "Let's try to pick up '%d' again (%d tries out of %d)" (3 is the limit) and calls <c>TransitionToPickUpObject(attempt + 1)</c> (0x005E1210); with the attempts spent it logs ".Fail"
    /// "Not trying to pick up '%d' again. Failing". 3 (ABORT) logs ".NoRetry" "Failed to pick up '%d', action does not retry." (0x005E10FA..0x005E1160). Both failures end in
    /// <c>SetFailedToUse(obj, PickUpObject = 0)</c> when the target is still located (0x005E1166..0x005E1186, the 2-argument form: the cube's own pose). Categories 1 and 2 do nothing (0x005E0FF4 -> 0x005E1214).
    /// </summary>
    // fidelity: M15-020, M15-024
    internal void PickUpCompleted(ActionResult r, int attempt)
    {
        uint category = (uint)r >> 24;
        uint? target = Candidate;
        if (category == 0)
        {
            Log($"info: Behaviors.{Id}.onPickUpActionResult.Done: Picked up '{target}'");
            TransitionToObjectPickedUp();
            return;
        }
        if (category == 4)
        {
            uint? carried = M.Docking.Carrying.CarriedObjectId;
            if (carried is not null && carried == target)
            {
                Log($"info: Behaviors.{Id}.onPickUpActionResult.RetryOk: We do have '{target}' picked up, so pretend we are fine");
                TransitionToObjectPickedUp();
                return;
            }
            if (attempt <= 2)
            {
                Log($"info: Behaviors.{Id}.onPickUpActionResult.RetryMaybe: Let's try to pick up '{target}' again ({attempt} tries out of 3)");
                TransitionToPickUpObject(attempt + 1);
                return;
            }
            Log($"info: Behaviors.{Id}.onPickUpActionResult.Fail: Not trying to pick up '{target}' again. Failing");
        }
        else if (category == 3) Log($"info: Behaviors.{Id}.onPickUpActionResult.NoRetry: Failed to pick up '{target}', action does not retry.");
        else return;                                                                                          // categories 1 and 2: nothing (0x005E1214); this stack's behaviour is then not acting
        if (target is { } id && M.World.GetLocatedObjectById(id) is { } obj) M.Whiteboard.SetFailedToUse(obj, ObjectActionFailure.PickUpObject);       // 0x005E1166..0x005E1186
        CurrentPhase = Phase.Idle; Finish();
    }

    /// <summary>The unlock id <c>FindFreeCubeToStackOn</c> asks for (0x005E0108..0x005E010C: <c>IsUnlocked(robot+0x448, 10, true)</c>): Anki.Cozmo.UnlockId.StackTwoCubes.</summary>
    public const int StackTwoCubesUnlockId = 0xA;

    /// <summary>The ProgressionUnlockComponent seam (<c>IsUnlocked(StackTwoCubes, true)</c>); null: no component in this stack, treated as unlocked and reported MISSING.</summary>
    public Func<int, bool>? IsUnlocked { get; set; }

    /// <summary>The distance <c>FindFreeCubeToStackOn</c> adds to the carried cube's X dimension before the beacon test (<c>vmov.f32 s2, #10.0</c> 0x005E01F2, <c>vadd.f32</c> 0x005E0210).</summary>
    public const float StackBeaconMarginMm = 10.0f;

    /// <summary>
    /// <c>BehaviorExploreBringCubeToBeacon::TransitionToObjectPickedUp</c> 0x005DF3E4, in the engine's order (the VizManager::EraseSegments at the top, 0x005DF412, is not drawn):
    /// the carried object must be the candidate (<c>[[robot+0x284]+8] == [this+0x12C]</c>, 0x005DF424..0x005DF436), else the <c>NotPickedUp</c> error (0x005DF4F8..0x005DF548); the candidate
    /// is looked up (<c>GetLocatedObjectByIdHelper</c> 0x005DF440), else the <c>ObjectIsNull</c> error (0x005DF556..0x005DF59A); with no active beacon nothing is done (0x005DF460);
    /// <b><c>FindFreeCubeToStackOn</c> comes next</b> (0x005DF46A): a cube found takes the stack branch, <c>TryToStackOn(target, 1)</c> (0x005DF4F2), and nothing else runs;
    /// otherwise <c>NeedActionCompleted(PickupCube 0x1F)</c> (0x005DF59C..0x005DF5A0) and then the floor placement.
    /// The engine returns without ending the behaviour on its error and no-beacon branches; this stack ends it (a stepped behaviour with no action pending would never end otherwise).
    /// </summary>
    private void TransitionToObjectPickedUp()
    {
        if (Candidate is not { } candidate || M.Docking.Carrying.CarriedObjectId != candidate)       // [[robot+0x284]+8] == [this+0x12C], strict
        {
            Log("error: BehaviorExploreBringCubeToBeacon.TransitionToObjectPickedUp.NotPickedUp: We do not have the cube picked up, we should not have transitioned here.");
            Finish(); return;
        }
        var carried = M.World.GetLocatedObjectById(candidate);
        if (carried is null)
        {
            Log($"error: BehaviorExploreBringCubeToBeacon.TransitionToObjectPickedUp.ObjectIsNull: Could not obtain obj from ID '{candidate}'");
            Finish(); return;
        }
        var beacon = M.Whiteboard.GetActiveBeacon();
        if (beacon is null) { Finish(); return; }
        var stackOn = FindFreeCubeToStackOn(carried, beacon);
        if (stackOn is not null)
        {
            Log($"info: Behaviors.{Id}.TransitionToObjectPickedUp: Decided to place '{candidate}' on top of '{stackOn.ObjectId}'");
            TryToStackOn(stackOn.ObjectId, 1);
            return;
        }
        // 0x005DF59C..0x005DF5A0 names the action outright: PickupCube (0x1F), whatever the config says; it runs BEFORE the pose search, whatever the search returns (M15-025)
        // fidelity: M15-025
        if (NeedActionCompleted("PickupCube") is { } action) Log($"needs action {action}");
        // 0x005DF5D0..0x005DF5E0: FindFreePoseInBeacon(carried, beacon, robot, &pose, [this+0x130])
        // fidelity: M15-019, M15-023
        if (M.RobotPose() is not { } robotPose)
        {
            SteppedBehavior.ReportMissing("BehaviorExploreBringCubeToBeacon::TransitionToObjectPickedUp 0x005DF5D0: invented gate: the engine always has Robot::GetPose(); this stack has no robot pose yet, so the search is skipped and the behaviour ends without placing");
            CurrentPhase = Phase.Idle; Finish(); return;
        }
        if (FindFreePoseInBeacon(carried, beacon, robotPose, (float)RecentFailureCooldownSec, out var pose))      // 0x005DF5E4 cmp r0,#1
        {
            Log(FormattableString.Invariant($"info: Behaviors.{Id}.TransitionToObjectPickedUp: Decided to place '{candidate}' on the floor at [{pose.Translation.X:F2},{pose.Translation.Y:F2},{pose.Translation.Z:F2}]"));
            TryToPlaceAt(pose, 1);                                                                                // 0x005DF694 movs r3,#1
            return;
        }
        // NoFreePoses, 0x005DF69C..0x005DF73A, in this order: the stamp on the beacon (AIWhiteboard::FailedToFindLocationInBeacon -> AIBeacon::FailedToFindLocation + UpdateBeaconRender), the log, the
        // mood event; no action is started.
        // fidelity: M15-023
        M.Whiteboard.FailedToFindLocationInBeacon(beacon);
        Log($"info: Behaviors.{Id}.TransitionToObjectPickedUp.NoFreePoses: Could not decide where to drop the cube in the beacon (all poses failed)");
        TriggerEmotion(NoLocationEmotionEvent);
        CurrentPhase = Phase.Idle;
        Finish();
    }

    /// <summary>The emotion event the NoFreePoses branch triggers (24 characters, <c>movs r2,#0x18</c> 0x005DF718, string at 0x005DF87C).</summary>
    // fidelity: M15-023
    public const string NoLocationEmotionEvent = "HikingNoLocationAtBeacon";

    /// <summary><c>MoodManager([robot+0x440])-&gt;TriggerEmotionEvent(name, MoodManager::GetCurrentTimeInSeconds())</c>.</summary>
    private void TriggerEmotion(string ev)
    {
        bool known = Context.Mood?.Trigger(ev, Clock() / 1000.0) ?? false;
        Log($"emotion event {ev}: " + (Context.Mood is null ? "no mood attached" : known ? "applied" : "not in the loaded mood model"));
    }

    /// <summary>The distance (20.0f = 0x41A00000, 0x005E1D14) and angle (Radians 0x3EC90FDB, 0x005E1CFC..0x005E1D0C) <c>FindFreeCubeToStackOn</c>'s filter passes to <c>DidFailToUse</c> (M15-024).</summary>
    // fidelity: M15-024
    public static readonly float StackFilterDistMm = BitConverter.Int32BitsToSingle(0x41A00000), StackFilterAngleRad = BitConverter.Int32BitsToSingle(0x3EC90FDB);

    /// <summary>Test seam: the <see cref="NativePlaceGroundRunner"/> the latest <c>TryToPlaceAt</c> ran (not engine behaviour).</summary>
    internal NativePlaceGroundRunner? LastPlaceAction { get; private set; }

    /// <summary>Test seam: when set, every candidate <c>(i, j)</c> the search tests is appended in the order tested (not engine behaviour).</summary>
    internal List<(int I, int J)>? CandidateLog { get; set; }

    /// <summary>
    /// <c>BehaviorExploreBringCubeToBeacon::FindFreePoseInBeacon(carried, beacon, robot, &amp;out, cooldown)</c> 0x005E0378..0x005E0666 (static; M15-019, M15-020), every number in binary32 in the
    /// engine's operation order. The frame <c>rot</c> (a <c>Rotation3d</c>, a double quaternion) is, in order: <c>Rotation3d(Radians(0), Z)</c> (0x005E038A..0x005E03A2); when
    /// <c>FindCubesInBeacon</c> finds cubes, the rotation about Z of the nearest one (<c>CalculateDirectionalityClosest</c> 0x005E07D8, called at 0x005E03C6); otherwise the heading from the robot to the
    /// beacon centre (0x005E03D0..0x005E0512: <c>v = B - R</c>, z = 0, <c>MakeUnitLength</c>, kept at angle 0 when <c>|len| &lt; 1e-5f</c>, else <c>acosf(v.X)</c> negated when <c>v.(-Y) &gt;= 0</c>).
    /// Then <c>S = size.x + 10.0f</c>, <c>N = (int)(radius / S)</c> (negative: no candidate), and the candidates in the engine's order (phase A: i = 0, -1, ..., -N; phase B: i = 1..N; per i: j = 0, then
    /// -1, +1, -2, +2, ..., -N, +N), the first accepted one wins. <c>*out</c> holds the last candidate tested, accepted or not.
    /// </summary>
    // fidelity: M15-019, M15-020
    internal bool FindFreePoseInBeacon(ObservableObject carried, AIBeacon beacon, Pose3d robotPose, float cooldown, out Pose3d result,
        Func<AIBeacon,IReadOnlyList<ObservableObject>>? nativeCubes = null, Func<ObservableObject,Pose3d,bool>? nativeFree = null, Func<bool>? current = null)
    {
        Pose3d last = default;
        SteppedBehavior.ReportMissing("BehaviorExploreBringCubeToBeacon::FindFreePoseInBeacon libm: MathF.Cos/MathF.Sin/MathF.Acos/MathF.Atan2 stand in for bionic cosf 0x004A415C, sinf 0x004A4168, acosf 0x004ACC4C and atan2f 0x004A4510, and re-analysis/evidence/m15-floor-placement/emulate_candidates.py takes the expected bits (e.g. 0x42380001) from Python's libm, not bionic, so a last-bit difference in those functions is not excluded. 0x005E0A96: the candidate pose's rotation matrix is the quaternion-to-matrix of Rotation3d::GetRotationMatrix 0x0084AAE0 WITHOUT its closing RotationMatrix3d::Renormalize 0x008494E0 (unread: its tolerances and RenormalizeUnconditional 0x004CE3C0), and Pose3d::GetWithRespectTo is the stack's double composition narrowed to f32; both can differ from the engine in the last bits");
        if(current is not null && !current())throw new NativeActionUnavailable("stale Bring floor search");
        var rot = BeaconFloorGeometry.RotationAboutAxis(0.0f, 0.0f, 0.0f, 1.0f);                                  // 0x005E038A..0x005E03A2
        bool haveFrame = false;
        var cubes = nativeCubes?.Invoke(beacon) ?? M.Whiteboard.FindCubesInBeacon(beacon);                                                        // 0x005E03AC..0x005E03B6
        if(current is not null && !current())throw new NativeActionUnavailable("stale Bring floor search");
        if (cubes.Count > 0 && BeaconFloorGeometry.CalculateDirectionalityClosest(cubes, beacon) is { } closest) { rot = closest; haveFrame = true; }     // 0x005E03BA..0x005E03CC
        float bx = (float)beacon.Pose.Translation.X, by = (float)beacon.Pose.Translation.Y, bz = (float)beacon.Pose.Translation.Z;
        if (!haveFrame) rot = BeaconFloorGeometry.FrameFromRobot(bx, by, bz, (float)robotPose.Translation.X, (float)robotPose.Translation.Y, (float)robotPose.Translation.Z, rot);
        float radius = (float)beacon.RadiusMm;                                                                      // 0x005E0538 vldr s16,[r7,#0xc]
        float radius2 = radius * radius;                                                                            // 0x005E0542
        float size = (float)CubeGeometry.SizeOf(carried.Type).X;                                                    // vtable[0x2C]()[0] (Block +0x88; 44.0f for LIGHTCUBE1..3, 0x004E4CD6..)
        float spacing = size + BeaconFloorGeometry.Ten;                                                             // 0x005E0564..0x005E056C
        int n = BeaconFloorGeometry.TruncToInt(radius / spacing);                                                   // 0x005E0570..0x005E0578
        bool Candidate(int i, int j)
        {
            if(current is not null && !current())throw new NativeActionUnavailable("stale Bring floor search");
            CandidateLog?.Add((i, j));
            // 0x005E09E0..0x005E0DF2
            var (tx, ty, tz, rotated) = BeaconFloorGeometry.CandidateTranslation(rot, spacing, i, j, bx, by, bz);
            var pose = last = Pose3d.FromFlatRotation(new Rotation3d(rot.W,rot.X,rot.Y,rot.Z),new Vec3(tx,ty,tz));                      // *out = Pose3d(rot, t, origin, ""), BEFORE any test (0x005E0A96..0x005E0A9E)
            pose.SetParent(M.World.GetNativeOrigin(M.World.CurrentOriginId)); // checked R2 row1.7: Robot::GetWorldOrigin
            float d2 = rotated.X * rotated.X;
            d2 += rotated.Y * rotated.Y;
            d2 += rotated.Z * rotated.Z;
            if (d2 > radius2) return false;                                                                         // 0x005E0ADE..0x005E0AE6 ble: NaN passes
            // 0x005E0B4A..0x005E0B7E: DidFailToUse(-1, PlaceObjectAt = 2, cooldown, *out, 100.0f, Radians(pi))
            bool failed=M.Whiteboard.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, cooldown, pose, BeaconFloorGeometry.FarDistance, BeaconFloorGeometry.Pi);
            if(current is not null && !current())throw new NativeActionUnavailable("stale Bring floor search");
            if(failed)return false;
            // 0x005E0B84..0x005E0C32: FindLocatedIntersectingObjects(quad of the carried object at *out, padding 10.0f), the carried id ignored; free iff empty
            return nativeFree?.Invoke(carried, pose) ?? DriveToPlaceCarriedObjectAction.StaticFindLocatedIntersectingObjectsIsEmpty(M, carried, pose, BeaconFloorGeometry.Ten, null);
        }
        bool Row(int i)
        {
            if (Candidate(i, 0)) return true;                                                                       // 0x005E0594 / 0x005E05E6
            if (n == 0) return false;                                                                               // 0x005E059C cmp r8,#0; beq
            for (int k = 1; k <= n; k++)                                                                            // 0x005E05A2..0x005E05CA: j = -1, +1, -2, +2, ..., -N, +N
            {
                if (Candidate(i, -k)) return true;
                if (Candidate(i, k)) return true;
            }
            return false;
        }
        bool Search()
        {
            if (n < 0) return false;                                                                                // 0x005E0580 blt 0x005E0628
            for (int i = 0; i >= -n; i--) if (Row(i)) return true;                                                  // phase A 0x005E0588..0x005E05D2
            for (int i = 1; i <= n; i++) if (Row(i)) return true;                                                   // phase B 0x005E05D4..0x005E0626 (skipped for N < 1)
            return false;                                                                                           // 0x005E0628 / 0x005E062E
        }
        bool found = Search();
        result = last;
        return found;
    }

    /// <summary>
    /// <c>TryToPlaceAt(robot, pose, attempt)</c> 0x005DFEF4..0x005DFF80 (M15-021): <c>StartActing(new PlaceObjectOnGroundAtPoseAction(robot, pose, false, false, true, 10.0f), callback)</c> (the return
    /// value is ignored); the callback 0x005E188C is <see cref="PlaceCompleted"/>. <c>PlaceObjectOnGroundAtPoseAction</c> (ctor 0x00554AFC, M15-022) is a <c>CompoundActionSequential</c> of
    /// <c>DriveToPlaceCarriedObjectAction(robot, pose, true, 0, 0, 1, 10.0f)</c> (0x00554B22..0x00554B56: <c>+0x178 = 0</c>, <c>useManualSpeed = 0</c>, <c>+0x179 = 1</c>, padding 0x41200000) and then
    /// <c>PlaceObjectOnGroundAction</c> (0x00554B76..0x00554B96). <c>CompoundActionSequential::UpdateInternal</c> 0x0054F70C moves on after a sub-action's success and ends with the failing
    /// sub-action's result otherwise (a category-4 result takes the retry path only when the compound's own <c>RetriesRemain()</c> is 1; the default byte is 0, 0x0053FDD8, so it never does).
    /// </summary>
    // fidelity: M15-021, M15-022
    private void TryToPlaceAt(Pose3d pose, int attempt)
    {
        CurrentPhase = Phase.PlacingAt;
        _actionsStarted++;
        var owner=_placeOwner;
        long generation=Interlocked.Read(ref _placeGeneration);
        bool Current()=>owner is not null && !owner.Lifetime.Destroying && ReferenceEquals(M.Robot.Engine.Robot,owner) && Interlocked.Read(ref _placeGeneration)==generation && EngineRunning;
        Log(FormattableString.Invariant($"start PlaceObjectOnGroundAtPose({pose.Translation.X:F2},{pose.Translation.Y:F2},{pose.Translation.Z:F2}) attempt {attempt}"));
        if(!Current()){ReportMissing("M15-022 ground-at-pose original owner unavailable");return;}
        Pose3d next;
        using(M.World.EnterPoseMutation())
        {
            next=new Pose3d(pose);var previous=_placePose;_placePose=next;previous?.Dispose();
            if(previous is not null && PlacementPoseRetiredForTest is {} hook)LogCallbackDeferral.Run(hook);
        }
        if(!Current())return;
        if(_releasePlaceCompletion is null)
        {
            var lease=AcquireActionCompletionOwner(M.Robot,owner);
            if(!Current()){lease?.Invoke();return;}
            _releasePlaceCompletion=lease;
        }
        NativePlaceGroundAtBeaconRunner action;
        try { action=new NativePlaceGroundAtBeaconRunner(M,owner!,next,Current); }
        catch(NativeActionUnavailable error)
        {
            ReportMissing("M15-022 ground-at-pose admission unavailable: "+error.Message);
            if(Current())Finish();
            return;
        }
        if(!Current()){action.RetireNeverAdmittedTree();return;}
        LastPlaceAction=action.Ground;
        action.Unavailable=error=>
        {
            if(!Current())return;
            uint tag=CurrentRunnerTag;
            ReportMissing("M15-022 ground-at-pose unavailable: "+error.Message);
            if(!Current())return;
            owner!.ActionList.DeferHostCleanup(()=>{if(Current() && CurrentRunnerTag==tag)Stop(BehaviorStopReason.Interrupted);});
        };
        action.Drive.Unavailable=action.Unavailable;
        StartActing(action,done=>{if(Current())PlaceCompleted((ActionResult)done.Result,next,attempt,Current);},Current);
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        Interlocked.Increment(ref _placeGeneration);
        using(M.World.EnterPoseMutation()){_placePose?.Dispose();_placePose=null;}_placeOwner=null;
        _releasePlaceCompletion?.Invoke();_releasePlaceCompletion=null;
        base.OnStop(reason);
    }

    /// <summary>
    /// The completion callback 0x005E188C by result category (the top byte, 0x005E18A0..0x005E18A8). 0 (success): "Successfully placed cube" and <c>FireEmotionEvents</c> (0x005E1916), with NO
    /// <c>NeedActionCompleted</c> and no <c>SetFailedToUse</c> (M15-025). 4 (retry): with the carried id equal to the target (not -1) and <c>attempt &lt;= 2</c> (0x005E193E, signed) the SAME pose is
    /// tried again with <c>attempt + 1</c> (no new <c>FindFreePoseInBeacon</c>); otherwise "CannotRetry" (max 3). Every other non-zero category: "NoRetryAllowed". Both failures end in the tail
    /// 0x005E1B32..0x005E1B56: when the target is still located, <c>SetFailedToUse(obj, PlaceObjectAt = 2, pose)</c> with the CANDIDATE pose.
    /// </summary>
    // fidelity: M15-021, M15-024, M15-025
    internal void PlaceCompleted(ActionResult r, Pose3d pose, int attempt, Func<bool>? current=null)
    {
        if(current is not null&&!current())return;
        uint category = (uint)r >> 24;
        if (category == 0)
        {
            Log($"info: Behaviors.{Id}.onPlaceActionResult.Done: Successfully placed cube");
            if(current is not null&&!current())return;
            FireEmotionEvents();
            if(current is not null&&!current())return;
            CurrentPhase = Phase.Idle; Finish();
            return;
        }
        if (category == 4)
        {
            uint? carried = current is null ? M.Docking.Carrying.CarriedObjectId : _placeOwner?.CarryingOwner?.CarriedObjectId;                    // [[robot+0x284]+8]; null = -1
            bool carryingTarget = carried is not null && carried == Candidate;                                      // 0x005E1928..0x005E1944
            if (carryingTarget && attempt <= 2)
            {
                Log(FormattableString.Invariant($"info: Behaviors.{Id}.onPlaceActionResult.Done.CanRetry: Failed to place '{Candidate}' at pose [{pose.Translation.X:F2},{pose.Translation.Y:F2},{pose.Translation.Z:F2}]"));
                if(current is not null&&!current())return;
                TryToPlaceAt(pose, attempt + 1);                                                                    // 0x005E19F8 adds r3,r0,#1
                return;
            }
            Log(FormattableString.Invariant($"info: Behaviors.{Id}.onPlaceActionResult.CannotRetry: Failed to place '{Candidate}' at pose [{pose.Translation.X:F2},{pose.Translation.Y:F2},{pose.Translation.Z:F2}] (attempt={attempt}/3) (carrying={(carryingTarget ? "yes" : "no")})"));
        }
        else Log($"info: Behaviors.{Id}.onPlaceActionResult.NoRetryAllowed: Failed to place (no retry allowed by action)");
        if(current is not null&&!current())return;
        if (Candidate is { } id && M.World.GetLocatedObjectByIdHelper(id) is { } obj) M.Whiteboard.SetFailedToUse(obj, ObjectActionFailure.PlaceObjectAt, pose);   // 0x005E1B32..0x005E1B56
        CurrentPhase = Phase.Idle; Finish();
    }

    /// <summary>
    /// <c>BehaviorExploreBringCubeToBeacon::FindFreeCubeToStackOn(carried, beacon, robot)</c> 0x005E00EC: returns null unless <c>ProgressionUnlockComponent::IsUnlocked(StackTwoCubes, true)</c>
    /// (0x005E0108..0x005E0114). Otherwise the first located object (<c>BlockWorld::FindLocatedObjectHelper</c> 0x005E0246, returnFirst = true) of family Block or LightCube (the table at
    /// 0x00C6D6A8, words 1 and 2) that the filter lambda 0x005E1CC6 accepts, in order: not the carried object itself; pose state Known (<c>[+0x24] == 1</c>); not <c>DidFailToUse</c> (reason 1
    /// StackOnObject, the behaviour's cooldown, its pose, 20.0f and Radians 0x3EC90FDB: the pose-aware whiteboard memory, M15-024); <c>AIBeacon::IsLocWithinBeacon(its pose, carried X dimension + 10.0f)</c>; and
    /// <c>DockingComponent::CanStackOnTopOfObject</c>. The iteration order of <c>FindLocatedObjectHelper</c> is taken as the ordered maps' (family, type, id).
    /// </summary>
    public ObservableObject? FindFreeCubeToStackOn(ObservableObject carried, AIBeacon beacon)
    {
        if (IsUnlocked is { } unlocked) { if (!unlocked(StackTwoCubesUnlockId)) return null; }
        else SteppedBehavior.ReportMissing("BehaviorExploreBringCubeToBeacon::FindFreeCubeToStackOn 0x005E010C: ProgressionUnlockComponent::IsUnlocked(StackTwoCubes) has no component in this stack; live-path default: treated as unlocked");
        float margin = (float)RotatedParentAxis.DimInParentFrame(carried, 0) + StackBeaconMarginMm;
        return M.World.LocatedObjects
            .Where(o => o.Family is ObjectFamily.Block or ObjectFamily.LightCube)
            .OrderBy(o => (int)o.Family).ThenBy(o => (int)o.Type).ThenBy(o => o.ObjectId)
            .FirstOrDefault(o => !ReferenceEquals(o, carried)
                                 && o.PoseState == PoseState.Known
                                 && !M.Whiteboard.DidFailToUse(unchecked((int)o.ObjectId), ObjectActionFailure.StackOnObject, (float)RecentFailureCooldownSec, o.Pose, StackFilterDistMm, StackFilterAngleRad)    // 0x005E1D24: (id, 1, cooldown, pose, 20.0f, Radians(pi/8))
                                 && beacon.IsLocWithinBeacon(o.Pose, margin)
                                 && M.Docking.CanStackOnTopOfObject(o));
    }

    /// <summary>
    /// <c>TryToStackOn(robot, target, attempt)</c> 0x005DFDD4 starts <c>DriveToPlaceOnObjectAction(robot, target, ...)</c> (0x005DFE0E; not built, no record owns it) with the completion
    /// lambda 0x005E142C. This stack runs the pre-batch <c>PlaceRelObjectHelper</c> action as the labelled stand-in for that action alone. The lambda's branches are the engine's: result
    /// category 0 (success): <c>NeedActionCompleted(StackCube 0x2A)</c> (0x005E14B6) then <c>FireEmotionEvents</c> (0x005E14BE); category 4 (retry) while the cube is still the carried one and
    /// the attempt number is at most 2 (0x005E14DC..0x005E14EA): <c>TryToStackOn(target, attempt + 1)</c> (0x005E1568); category 3, and category 4 when the retry test fails: <c>SetFailedToUse(target, StackOnObject)</c> (0x005E1680) when the
    /// target is still located (0x005E166C..0x005E1672); every other category does nothing (<see cref="StackCompleted"/>).
    /// </summary>
    private void TryToStackOn(uint target, int attempt)
    {
        // RETIRE-LEGACY-DRIVE-API-1 (operator 2026-11-13): the legacy DockHelper stand-in for DriveToPlaceOnObjectAction is retired (see TransitionToPickUpObject).
        CurrentPhase = Phase.StackingOn;
        _actionsStarted++;
        SteppedBehavior.ReportMissing($"BehaviorExploreBringCubeToBeacon::TryToStackOn 0x005DFDD4: the legacy host BringCubeToBeaconBehavior has no drive (its DockHelper stand-in for DriveToPlaceOnObjectAction 0x005DFE0E was retired, RETIRE-LEGACY-DRIVE-API-1); no stack on '{target}' (attempt {attempt}) is started; NativeBringCubeToBeaconBehavior owns this flow");
    }

    /// <summary>
    /// The completion lambda 0x005E142C by result category (the top byte of the result, 0x005E1434..0x005E144A): 0 success; 4 retry; 3 abort; every other category (1 running,
    /// 2 cancelled, ...) branches to 0x005E1684 and returns: no log, no SetFailedToUse, and the behaviour is not ended here.
    /// </summary>
    internal void StackCompleted(ActionResult r, uint target, int attempt)
    {
        uint category = (uint)r >> 24;
        bool failed;
        if (category == 0)
        {
            Log($"info: Behaviors.{Id}.TryToStackOn: stacked on '{target}'");
            if (NeedActionCompleted("StackCube") is { } action) Log($"needs action {action}");             // 0x005E14B6: StackCube (0x2A)
            FireEmotionEvents();                                                                           // 0x005E14BE
            CurrentPhase = Phase.Idle; Finish();
            return;
        }
        if (category == 4)
        {
            if (Candidate is { } c && M.Docking.Carrying.CarriedObjectId == c && attempt <= 2)                    // 0x005E14DC..0x005E14EA
            {
                Log($"info: Behaviors.{Id}.TryToStackOn: retrying ({attempt + 1})");
                TryToStackOn(target, attempt + 1);                                                         // 0x005E1568
                return;
            }
            failed = true;                                                                                 // 0x005E15D8
        }
        else failed = category == 3;                                                                       // 0x005E156E
        if (!failed) return;                                                                               // 0x005E1684
        Log($"info: Behaviors.{Id}.TryToStackOn: stacking failed: {r}");
        if (M.World.GetLocatedObjectById(target) is not null) M.Whiteboard.SetFailedToUse(target, ObjectActionFailure.StackOnObject);   // 0x005E1660..0x005E1680
        CurrentPhase = Phase.Idle; Finish();
    }

    /// <summary>
    /// <c>BehaviorExploreBringCubeToBeacon::FireEmotionEvents(robot)</c> 0x005E002C: <c>AIWhiteboard::AreAllCubesInBeacons</c> (0x005E0038) picks the 29-character
    /// "HikingBroughtLastCubeToBeacon" (0x005E004A, string 0x005E00CC) when every cube is in a beacon, else the 25-character "HikingBroughtCubeToBeacon" (0x005E006C, 0x005E00B0), triggered on
    /// the mood manager at Robot+0x440 with <c>MoodManager::GetCurrentTimeInSeconds</c>.
    /// </summary>
    private void FireEmotionEvents()
    {
        string ev = M.Whiteboard.AreAllCubesInBeacons() ? LastCubeEmotionEvent : CubeEmotionEvent;
        TriggerEmotion(ev);
    }
}


/// <summary>
/// The binary32 geometry of <c>BehaviorExploreBringCubeToBeacon::FindFreePoseInBeacon</c> (M15-019) and <c>CalculateDirectionalityClosest</c> (M15-020), in the engine's operation order.
/// A <c>Rotation3d</c> is a double quaternion (w, x, y, z): the constructor from (Radians, axis) 0x0084A506 is <c>half = angle * 0.5f; q = (cosf(half), axis * sinf(half))</c> (f32 products widened) and
/// <c>UnitQuaternion_&lt;double&gt;::Normalize</c> 0x00849DB8; <c>Rotation3d::operator*(Point3f)</c> 0x0084ACA6 narrows the quaternion to f32 and runs <c>UnitQuaternion_&lt;float&gt;::operator*</c> 0x00849C1C.
/// </summary>
// fidelity: M15-019, M15-020
public static class BeaconFloorGeometry
{
    private static float F(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));

    /// <summary>10.0f = 0x41200000 (<c>vmov.f32 s0,#10.0</c> 0x005E0564, 0x005E09F8; the intersect padding 0x005E0C1C; TryToPlaceAt's padding 0x005DFF02).</summary>
    public static readonly float Ten = F(0x41200000);
    /// <summary>1.0e-5f = 0x3727C5AC (<c>vldr s2,[pc]</c> 0x005E0446 and 0x0059C2C2).</summary>
    public static readonly float OneEm5 = F(0x3727C5AC);
    /// <summary>100.0f = 0x42C80000 (<c>movt r0,#0x42c8</c> 0x005E0B68): the distance of the floor path's <c>DidFailToUse</c>.</summary>
    public static readonly float FarDistance = F(0x42C80000);
    /// <summary>pi = 0x40490FDB (<c>movw/movt</c> 0x005E0B4A..0x005E0B4E): the angle of the floor path's <c>DidFailToUse</c>.</summary>
    public static readonly float Pi = F(0x40490FDB);
    /// <summary>FLT_MAX = 0x7F7FFFFF (0x005E0998): the start of the closest-cube search.</summary>
    public static readonly float FltMax = F(0x7F7FFFFF);
    /// <summary>The "already unit length" tolerance of <c>UnitQuaternion_&lt;double&gt;::Normalize</c> (the double literal at 0x00849E58 = 0x3E56A09E19D672B6).</summary>
    public static readonly double NormalizeTolerance = BitConverter.Int64BitsToDouble(0x3E56A09E19D672B6);

    /// <summary>A <c>Rotation3d</c>: the double quaternion at +0..+0x1F.</summary>
    public readonly record struct Quat(double W, double X, double Y, double Z);

    /// <summary><c>vcvt.s32.f32</c> (round toward zero, saturating, NaN to 0).</summary>
    public static int TruncToInt(float v)
    {
        if (float.IsNaN(v)) return 0;
        if (v >= 2147483648.0f) return int.MaxValue;
        if (v <= -2147483648.0f) return int.MinValue;
        return (int)v;
    }

    /// <summary><c>Rotation3d::Rotation3d(Radians, Point3f const&amp;)</c> 0x0084A506..0x0084A596 (cosf and sinf are <see cref="MathF"/>, the stack's libm policy).</summary>
    public static Quat RotationAboutAxis(float angle, float ax, float ay, float az)
    {
        var q=Rotation3d.FromAngleAxis(angle,new Vec3(ax,ay,az));return new(q.W,q.X,q.Y,q.Z);
    }

    /// <summary><c>UnitQuaternion_&lt;double&gt;::Normalize</c> 0x00849DB8..0x00849E52.</summary>
    public static Quat Normalize(double w, double x, double y, double z)
    {
        var q=new Rotation3d(w,x,y,z).Normalized();return new(q.W,q.X,q.Y,q.Z);
    }

    /// <summary><c>Rotation3d::operator*(Point3f const&amp;)</c> 0x0084ACA6 into <c>UnitQuaternion_&lt;float&gt;::operator*(Point3f)</c> 0x00849C1C, each f32 operation in the listed order.</summary>
    public static (float X, float Y, float Z) Rotate(Quat q, float px, float py, float pz)
    {
        var p=new Rotation3d(q.W,q.X,q.Y,q.Z).Rotate(new Vec3(px,py,pz));return ((float)p.X,(float)p.Y,(float)p.Z);
    }

    /// <summary><c>Rotation3d::GetRotationMatrix</c> 0x0084AAE0 (R17: the matrix entries, then the closing <c>RotationMatrix3d::Renormalize</c> 0x008494E0, R18; both applied by <see cref="Rotation3d.Matrix"/>).</summary>
    public static Mat3 RotationMatrix(Quat q)
    {
        return new Rotation3d(q.W,q.X,q.Y,q.Z).Matrix();
    }

    /// <summary><c>Rotation3d::GetAngleAroundZaxis</c> 0x0084AA1C from the matrix entries (f32): <c>atan2f(R10, R00)</c> when <c>R10^2 + R00^2 &gt; R01^2 + R11^2</c>, else <c>atan2f(-R01, R11)</c>; then <c>Radians(float)</c>.</summary>
    public static float AngleAroundZ(Mat3 r)
    {
        return (float)r.AngleAroundZ;
    }

    /// <summary>
    /// <c>CalculateDirectionalityClosest(vec, blockWorld, beacon, &amp;rot)</c> 0x005E07D8..0x005E0948: of the cubes (in the vector's order) the one whose pose with respect to the beacon has the strictly smallest
    /// <c>x*x + y*y + z*z</c> (f32, the sum in that order, from FLT_MAX; NaN never wins); none found returns null; else <c>Rotation3d(Radians(GetAngleAroundZaxis(closest pose)), Z)</c>.
    /// The pose with respect to the beacon is the stack's double composition narrowed to f32 (the engine composes in f32).
    /// </summary>
    public static Quat? CalculateDirectionalityClosest(IReadOnlyList<ObservableObject> cubes, AIBeacon beacon)
    {
        float best = FltMax;
        ObservableObject? closest = null;
        foreach (var o in cubes)
        {
            var t = o.Pose.WithRespectTo(beacon.Pose).Translation;
            float x = (float)t.X, y = (float)t.Y, z = (float)t.Z;
            float d2 = x * x;
            d2 += y * y;
            d2 += z * z;
            if (d2 < best) { best = d2; closest = o; }                                  // 0x005E0886 vcmpe s0,s16; itt mi
        }
        if (closest is null) return null;
        return RotationAboutAxis((float)closest.Pose.AngleAroundZ, 0.0f, 0.0f, 1.0f);
    }

    /// <summary>
    /// The frame from the robot, 0x005E03D0..0x005E0512: <c>v = B - R</c> per axis, <c>v.z = 0</c>, <c>len = MakeUnitLength(v)</c> (0x0050E0C0: <c>s2 = x*x; s2 += y*y; s2 += z*z</c>; for <c>s2 &gt; 0</c> <c>len = sqrtf(s2)</c>
    /// and every component is multiplied by <c>1.0f / len</c>, else 0); <c>|len| &lt; 1e-5f</c> keeps <paramref name="current"/>; else <c>a = acosf(((v.x*X.x) + v.y*X.y) + v.z*X.z)</c>, negated when
    /// <c>((v.x*(-Y.x)) + v.y*(-Y.y)) + v.z*(-Y.z) &gt;= 0</c>, and <c>Rotation3d(Radians(a), Z)</c>. X_AXIS_3D = (1,0,0), Y_AXIS_3D = (0,1,0).
    /// </summary>
    public static Quat FrameFromRobot(float bx, float by, float bz, float rx, float ry, float rz, Quat current)
    {
        float vx = bx - rx, vy = by - ry, vz = bz - rz;
        vz = 0.0f;                                                                      // 0x005E0424 str r0,[sp,#0x48]
        float s2 = vx * vx;
        s2 += vy * vy;
        s2 += vz * vz;
        float len;
        if (s2 > 0f)
        {
            len = MathF.Sqrt(s2);
            float inv = 1.0f / len;
            vx = inv * vx; vy = inv * vy; vz = inv * vz;
        }
        else len = 0.0f;
        if (MathF.Abs(len) < OneEm5) return current;                                    // 0x005E0446..0x005E0452 bmi 0x005E0514
        float dotX = vx * 1.0f;
        dotX += vy * 0.0f;
        dotX += vz * 0.0f;
        float dotNegY = vx * -0.0f;
        dotNegY += vy * -1.0f;
        dotNegY += vz * -0.0f;
        float a = MathF.Acos(dotX);                                                     // 0x005E04D6
        if (dotNegY >= 0f) a = -a;                                                      // 0x005E04DA..0x005E04EC
        return RotationAboutAxis(EngineRadians32.Rescale(a), 0.0f, 0.0f, 1.0f);
    }

    /// <summary>
    /// The candidate position, 0x005E09E0..0x005E0A66: <c>off = (S*(float)i, S*(float)j, 0)</c>, <c>p = rot * off</c>, <c>t = p + B</c> (f32 per axis). Returns <c>t</c> and <c>p</c> (the radius test reads <c>p</c>).
    /// </summary>
    public static (float X, float Y, float Z, (float X, float Y, float Z) Rotated) CandidateTranslation(Quat rot, float spacing, int i, int j, float bx, float by, float bz)
    {
        float ox = spacing * (float)i;
        float oy = spacing * (float)j;
        var p = Rotate(rot, ox, oy, 0.0f);
        return (p.X + bx, p.Y + by, p.Z + bz, p);
    }
}
