using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// <c>BehaviorKnockOverCubes</c> (0x005C2EA0..0x005C3C00). Config: <c>minimumStackHeight</c> (3, or 2 for the
/// spark), the five triggers. Runnable with a located stack at least that tall and nothing carried.
/// <c>InitInternal</c> locks reactions and <c>TransitionToReachingForBlock</c> runs a sequential compound:
/// <c>TurnTowardsObjectAction</c> (max π) at the stack's bottom block, a <c>DriveStraightAction</c> to 85 mm
/// (0x42AA0000) from it at 60 mm/s (0x42700000), and the <c>reachForBlockTrigger</c> lift-safe animation;
/// <c>TransitionToKnockingOverStack</c> runs a <c>DriveAndFlipBlockAction</c> on the bottom block; when that
/// fails to drive, <c>TransitionToBlindlyFlipping</c> runs a <c>FlipBlockAction</c> with the pre-action check
/// off and a <c>WaitAction(0.5)</c>; <c>TransitionToPlayingReaction</c> plays the success trigger when the
/// stack came apart (<c>HandleObjectUpAxisChanged</c> on any block, or the blocks no longer stacked) and the
/// failure trigger otherwise; the put-down trigger plays when the robot ends up holding a block. Objective
/// <c>KnockedOverBlocks</c>.
/// </summary>
public sealed class KnockOverCubesBehavior : ManipulationBehavior
{
    public enum Phase { Idle, ReachingForBlock, KnockingOverStack, BlindlyFlipping, PlayingReaction }
    public const double ReachDistanceMm = 85.0;
    public const float ReachSpeedMmps = 60f;
    public const double BlindFlipWaitSec = 0.5;
    /// <summary>The slack added to the block's x before it is compared with 85 mm (vmov.f32 s0, #10.0 at 0x005C3354).</summary>
    public const double ReachSlackMm = 10.0;
    /// <summary>
    /// How many retry-category failures re-run the knock-over before the blind flip: the callback at 0x005C3DCE
    /// re-enters <c>TransitionToKnockingOverStack</c> while the attempt count at this+0x140 is at most 1
    /// (cmp r0, #1; bgt at 0x005C3E0E) and counts up after either branch (0x005C3E20..0x005C3E26), so counts
    /// 0 and 1 re-run it and the third retry result goes to the blind flip.
    /// </summary>
    public const int MaxKnockOverRetries = 2;
    /// <summary>The wait after the flip in both the knock-over and the blind flip (mov.w r2, #0x3f000000 at 0x005C35E6, 0x005C38B0).</summary>
    public const double AfterFlipWaitSec = 0.5;
    public int KnockOverAttempts { get; private set; }

    public KnockOverCubesBehavior(ManipulationSystem m, string id = "KnockOverCubes", int minimumStackHeight = 3) : base(id, "KnockOverCubes", m)
        => MinimumStackHeight = minimumStackHeight;

    public int MinimumStackHeight { get; }
    public AnimationTrigger ReachForBlockTrigger { get; init; } = AnimationTrigger.KnockOverGrabAttempt;
    public AnimationTrigger KnockOverEyesTrigger { get; init; } = AnimationTrigger.KnockOverEyes;
    public AnimationTrigger SuccessTrigger { get; init; } = AnimationTrigger.KnockOverSuccess;
    public AnimationTrigger FailureTrigger { get; init; } = AnimationTrigger.KnockOverFailure;
    public AnimationTrigger PutDownTrigger { get; init; } = AnimationTrigger.PutDownBlockPutDown;
    public Phase CurrentPhase { get; private set; }
    public StackOfCubes? TargetStack { get; private set; }
    public bool? KnockedOver { get; private set; }
    private bool _upAxisChanged;

    private StackOfCubes? Tallest() { var s = M.Configurations.GetTallestStack(); return s is not null && s.StackHeight >= MinimumStackHeight ? s : null; }

    protected override bool IsRunnableInternal(BehaviorContext context) => !M.Docking.Carrying.IsCarryingObject && Tallest() is not null;

    protected override void OnStart()
    {
        Scope.DisableReactions();
        TargetStack = Tallest();
        if (TargetStack is null) { Log("no stack"); Finish(); return; }
        _upAxisChanged = false; KnockedOver = null; KnockOverAttempts = 0;
        M.World.ObjectObserved += OnObserved;
        TransitionToReachingForBlock();
    }

    private void OnObserved(ObjectObservation o)
    {
        // HandleObjectUpAxisChanged: a block of the stack landing on another face means it fell
        if (TargetStack is { } s && s.ContainsBlock(o.Object.ObjectId) && o.Object.UpAxisFromPose() != UpAxisOf(o.PreviousPose)) _upAxisChanged = true;
    }

    private static UpAxis UpAxisOf(Pose3d p)
    {
        var z = p.Rotation * new Vec3(0, 0, 1);
        return Math.Abs(z.Z) >= Math.Abs(z.X) && Math.Abs(z.Z) >= Math.Abs(z.Y) ? (z.Z > 0 ? UpAxis.ZPositive : UpAxis.ZNegative)
             : Math.Abs(z.X) >= Math.Abs(z.Y) ? (z.X > 0 ? UpAxis.XPositive : UpAxis.XNegative) : (z.Y > 0 ? UpAxis.YPositive : UpAxis.YNegative);
    }

    private void TransitionToReachingForBlock()
    {
        CurrentPhase = Phase.ReachingForBlock;
        uint bottom = TargetStack!.BottomBlockId;
        RunAction($"reach for block {bottom}", async ct =>
        {
            await M.TurnTowardsObjectAsync(bottom, Math.PI, ct);
            var obj = M.World.GetLocatedObjectById(bottom); var robot = M.RobotPose();
            if (obj is null || robot is null) return ActionResult.BadObject;
            // 0x005C3346..0x005C33A0: the block's pose with respect to the robot; only when its x plus 10 is
            // beyond 85 mm does a DriveStraightAction(x - 85) at 60 mm/s go into the sequence. Closer than that,
            // nothing drives - the robot never backs up to reach.
            double x = obj.Pose.WithRespectTo(robot.Value).Translation.X;
            return x + ReachSlackMm > ReachDistanceMm ? await new DriveStraightAction(M, x - ReachDistanceMm, ReachSpeedMmps).RunAsync(ct) : ActionResult.Success;
        }, r =>
        {
            // StartActing with a member callback runs it whatever the result (the lambda at 0x005BF8F4 ignores
            // it): a reach that fails skips the rest of its sequence, the grab animation, and still goes on
            if (r != ActionResult.Success) { Log($"reaching failed: {r}"); TransitionToKnockingOverStack(); return; }
            PlayTrigger(ReachForBlockTrigger, TransitionToKnockingOverStack);
        });
    }

    private void TransitionToKnockingOverStack()
    {
        CurrentPhase = Phase.KnockingOverStack;
        uint bottom = TargetStack!.BottomBlockId;
        // the maximum turn towards a face: pi/2 on the first attempt, 0 once the attempt count at +0x140 is
        // above zero (adr/addgt over the table at 0x005C36BC); the trailing 20.0 has no effect
        var flip = new DriveAndFlipBlockAction(M, bottom) { MaxTurnTowardsFaceRad = KnockOverAttempts > 0 ? 0.0 : Math.PI / 2 };
        // 0x005C355C..0x005C35FA: a sequence of TurnTowardsObjectAction (max pi) at the bottom block, the
        // DriveAndFlipBlockAction, and a WaitAction of 0.5 s.
        RunAction($"DriveAndFlipBlockAction({bottom})", async ct =>
        {
            await M.TurnTowardsObjectAsync(bottom, Math.PI, ct);
            var r = await flip.RunAsync(ct);
            if (r != ActionResult.Success) return r;
            await Task.Delay(TimeSpan.FromSeconds(AfterFlipWaitSec), ct);
            return r;
        }, r =>
        {
            foreach (var l in flip.Trace) Log("  " + l);
            // the callback at 0x005C3DCE splits on the result
            if (r == ActionResult.NoPreActionPoses)
            {
                // 0x005C3DDE..0x005C3DEA: the target is written to AIWhiteboard+0x70 and the behaviour ends;
                // the NoPreDockPoses reaction picks it up and rams the block (NoPreDockPosesStrategy)
                M.Whiteboard.NoPreDockPosesObjectId = bottom;
                Log($"no pre-action poses for {bottom}");
                Finish();
                return;
            }
            uint category = (uint)r >> 24;
            if (category == 0) { TransitionToPlayingReaction(); return; }
            if (category == 4)
            {
                bool again = KnockOverAttempts < MaxKnockOverRetries;
                KnockOverAttempts++;
                if (again) TransitionToKnockingOverStack(); else TransitionToBlindlyFlipping();
                return;
            }
            Log($"knock-over failed: {r}");
            Finish();
        });
    }

    private void TransitionToBlindlyFlipping()
    {
        CurrentPhase = Phase.BlindlyFlipping;
        uint bottom = TargetStack!.BottomBlockId;
        var flip = new FlipBlockAction(M, bottom) { CheckPreActionPose = false };
        RunAction("FlipBlockAction (blind)", flip.RunAsync, _ =>
        {
            foreach (var l in flip.Trace) Log("  " + l);
            Wait(BlindFlipWaitSec, TransitionToPlayingReaction);
        });
    }

    private void TransitionToPlayingReaction()
    {
        CurrentPhase = Phase.PlayingReaction;
        M.Configurations.Update();
        var still = M.Configurations.Stacks.FirstOrDefault(s => s.BottomBlockId == TargetStack!.BottomBlockId);
        KnockedOver = _upAxisChanged || still is null || still.StackHeight < TargetStack!.StackHeight;
        Log(KnockedOver.Value ? "the stack came apart" : "the stack is still standing");
        var trigger = KnockedOver.Value ? SuccessTrigger : FailureTrigger;
        // the flag at +0x14c gates both the objective and the needs action (0x005C3950): a stack still
        // standing reports neither
        if (KnockedOver.Value && NeedActionCompleted() is { } action) Log($"needs action {action}");
        PlayTrigger(trigger, () =>
        {
            if (KnockedOver.Value) Log("objective achieved: KnockedOverBlocks");
            if (M.Docking.Carrying.IsCarryingObject) PlayTrigger(PutDownTrigger, () => { M.Docking.ReleaseCarriedObject(); Finish(); });
            else Finish();
        });
    }

    protected override void OnStop(BehaviorStopReason reason) { M.World.ObjectObserved -= OnObserved; CurrentPhase = Phase.Idle; base.OnStop(reason); }
}

/// <summary>
/// <c>BehaviorPopAWheelie</c> (0x005C7430..0x005C7F66): runnable with an upright located cube and nothing carried.
/// <c>TransitionToReactingToBlock</c> plays 0x18A <see cref="AnimationTrigger.PopAWheelieInitial"/>;
/// <c>TransitionToPerformingAction</c> runs a <c>DriveToPopAWheelieAction</c> (drive to the docking pose, then the
/// firmware <c>POP_A_WHEELIE</c> dock); a failure goes through <c>SetupRetryAction</c> ("Retry %d of %d"): the
/// realign animation 0x18D <see cref="AnimationTrigger.PopAWheelieRealign"/> when the dock did not reach the
/// pre-action pose, 0x18E <see cref="AnimationTrigger.PopAWheelieRetry"/> otherwise, then the action again.
/// <c>StopInternal</c> re-enables stop-on-cliff (<c>EnableStopOnCliff</c>): the wheelie lifts the front cliff
/// sensors. Objective <c>PoppedWheelie</c>.
///
/// The completion callback (the <c>StartActing</c> lambda at 0x005C7CBC) splits on the result's category byte
/// (<c>result &gt;&gt; 24</c>): success plays 0x21C <see cref="AnimationTrigger.SuccessfulWheelie"/> (0x005C7D16..0x005C7D30)
/// and reports the objective and the needs action (0x005C7E0C, 0x005C7E14); a Retry-category result retries
/// only while the retry count at this+0x12C is still 0 (0x005C7D44..0x005C7D4A, then <c>SetupRetryAction</c> at
/// 0x005C7DFA) - the count is bumped by <c>TransitionToPerformingAction(robot, true)</c> (0x005C777E) and zeroed
/// otherwise (0x005C780C), so one retry at most; a Retry result with the retry used, or an Abort-category result,
/// marks the cube failed to use for <see cref="ObjectActionFailure.RollOrPopAWheelie"/> (SetFailedToUse(obj, 3) at
/// 0x005C7DAA); anything else logs BehaviorPopAWheelie.FailedPopAction and ends. <c>SetupRetryAction</c> 0x005C79D0
/// plays 0x18D when the result is exactly DidNotReachPreActionPose (0x04000001, 0x005C79F8..0x005C7A00) and 0x18E
/// otherwise, then runs the action again as a retry (0x005C7F66).
/// </summary>
public sealed class PopAWheelieBehavior : ManipulationBehavior
{
    public enum Phase { Idle, ReactingToBlock, PerformingAction, Retrying }
    public const int MaxRetries = 1;

    public PopAWheelieBehavior(ManipulationSystem m, string id = "PopAWheelie") : base(id, "PopAWheelie", m) { }

    public Phase CurrentPhase { get; private set; }
    public uint? TargetObjectId { get; private set; }
    public int Retries { get; private set; }
    public bool Succeeded { get; private set; }

    private ObservableObject? Target() => ClosestCube(o => o.UpAxisFromPose() is UpAxis.ZPositive or UpAxis.ZNegative);

    protected override bool IsRunnableInternal(BehaviorContext context) => !M.Docking.Carrying.IsCarryingObject && Target() is not null;

    protected override void OnStart()
    {
        Scope.DisableReactions();
        TargetObjectId = Target()?.ObjectId;
        if (TargetObjectId is null) { Finish(); return; }
        Retries = 0; Succeeded = false;
        CurrentPhase = Phase.ReactingToBlock;
        PlayTrigger(AnimationTrigger.PopAWheelieInitial, TransitionToPerformingAction);
    }

    private void TransitionToPerformingAction()
    {
        CurrentPhase = Phase.PerformingAction;
        uint id = TargetObjectId!.Value;
        RunAction($"DriveToPopAWheelieAction({id})", async ct =>
        {
            var drive = new DriveToObjectAction(M, id, PreActionType.Docking);
            var d = await drive.RunAsync(ct);
            foreach (var l in drive.Trace) Log("  " + l);
            if (d != ActionResult.Success) return d;
            var pop = new PopAWheelieAction(M, id) { CheckPreActionPose = false };
            var r = await pop.RunAsync(ct);
            foreach (var l in pop.Trace) Log("  " + l);
            return r;
        }, r =>
        {
            uint category = (uint)r >> 24;
            if (category == 0)
            {
                Succeeded = true;
                Log("objective achieved: PoppedWheelie");
                // 0x005C7E14, right after the objective
                if (NeedActionCompleted() is { } action) Log($"needs action {action}");
                PlayTrigger(AnimationTrigger.SuccessfulWheelie, Finish);
                return;
            }
            if (category == 4 && Retries < MaxRetries) { /* retry below */ }
            else
            {
                if (category is 3 or 4)
                {
                    M.Whiteboard.SetFailedToUse(id, ObjectActionFailure.RollOrPopAWheelie);
                    Log($"giving up: {r}; SetFailedToUse");
                }
                else Log($"BehaviorPopAWheelie.FailedPopAction: {r}");
                Finish();
                return;
            }
            Retries++;
            Log($"Retry {Retries} of {MaxRetries}");
            CurrentPhase = Phase.Retrying;
            PlayTrigger(r == ActionResult.DidNotReachPreActionPose ? AnimationTrigger.PopAWheelieRealign : AnimationTrigger.PopAWheelieRetry, TransitionToPerformingAction);
        });
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        M.Robot.SendMessage(new EnableStopOnCliff { Enable = true }, flush: true);
        Log("EnableStopOnCliff(true)");
        CurrentPhase = Phase.Idle;
        base.OnStop(reason);
    }
}

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
            CurrentPhase = Phase.PuttingDown;
            RunAction("turn and PlaceObjectOnGround", async ct =>
            {
                await M.TurnTowardsObjectAsync(t.ObjectId, Math.PI, ct);
                return await new PlaceObjectOnGroundAction(M).RunAsync(ct);
            }, _ => Finish());
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

/// <summary>
/// <c>BehaviorCubeLiftWorkout</c> (0x005D7E50..0x005D8A80) over the <see cref="WorkoutComponent"/>: runnable with
/// a workout, a target cube and nothing carried. <c>InitInternal</c> pushes the idle animation (none, 0x23F) and
/// <c>TransitionToPickingUpCube</c> uses the pick-up helper; then the pre-lift animation,
/// <c>TransitionToStrongLifts</c> (the strong-lift animation <c>GetNumStrongLifts</c> times, from the Confident
/// emotion through the config's score graph), <c>TransitionToWeakPose</c> (the transition animation),
/// <c>TransitionToWeakLifts</c> (the weak-lift animation the weak count of times), <c>TransitionToPuttingDown</c>
/// (the put-down animation releases the cube), <c>TransitionToCheckPutDown</c> (still carrying →
/// <c>TransitionToManualPutDown</c>: <c>PlaceObjectOnGroundAction</c>), <c>TransitionToPostLiftAnim</c>, and
/// <c>EndIteration</c>: <c>WorkoutComponent::CompleteCurrentWorkout</c>, the config's emotion event and the
/// objectives <c>PerformedWorkout</c> plus the config's additional one.
/// </summary>
public sealed class CubeLiftWorkoutBehavior : ManipulationBehavior
{
    public enum Phase { Idle, PickingUpCube, PreLift, StrongLifts, WeakPose, WeakLifts, PuttingDown, CheckPutDown, ManualPutDown, PostLift }

    public CubeLiftWorkoutBehavior(ManipulationSystem m, string id = "CubeLiftWorkout") : base(id, "CubeLiftWorkout", m) { }

    public Phase CurrentPhase { get; private set; }
    public WorkoutConfig? Workout { get; private set; }
    public int StrongLiftsPlanned { get; private set; }
    public int WeakLiftsPlanned { get; private set; }
    public int LiftsDone { get; private set; }

    protected override bool IsRunnableInternal(BehaviorContext context) =>
        M.Workouts?.GetCurrentWorkout() is not null && !M.Docking.Carrying.IsCarryingObject && ClosestCube(o => o.UpAxisFromPose() is UpAxis.ZPositive or UpAxis.ZNegative) is not null;

    protected override void OnStart()
    {
        Scope.DisableReactions();
        Workout = M.Workouts?.GetCurrentWorkout();
        var target = ClosestCube(o => o.UpAxisFromPose() is UpAxis.ZPositive or UpAxis.ZNegative);
        if (Workout is null || target is null) { Finish(); return; }
        double Mood(EmotionType e) => Context.Mood is { } m ? m[e] : 0;
        StrongLiftsPlanned = Math.Max(1, Workout.GetNumStrongLifts(Mood));
        WeakLiftsPlanned = Math.Max(0, Workout.GetNumWeakLifts(Mood));
        Log($"workout: {StrongLiftsPlanned} strong, {WeakLiftsPlanned} weak lifts (Confident={Mood(EmotionType.Confident):F2})");
        CurrentPhase = Phase.PickingUpCube;
        var helper = new DockHelper(M);
        uint id = target.ObjectId;
        RunAction($"PickupBlockHelper({id})", ct => helper.RunAsync(id, PreActionType.Docking, () => new PickupObjectAction(M, id), ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            if (r != ActionResult.Success) { Log($"pick-up failed: {r}"); Finish(); return; }
            CurrentPhase = Phase.PreLift;
            PlayTrigger(Workout.PreLift, () => { LiftsDone = 0; StrongLift(); });
        });
    }

    private void StrongLift()
    {
        CurrentPhase = Phase.StrongLifts;
        if (LiftsDone >= StrongLiftsPlanned) { CurrentPhase = Phase.WeakPose; PlayTrigger(Workout!.Transition, () => { LiftsDone = 0; WeakLift(); }); return; }
        PlayTrigger(Workout!.StrongLift, () => { LiftsDone++; StrongLift(); });
    }

    private void WeakLift()
    {
        CurrentPhase = Phase.WeakLifts;
        if (LiftsDone >= WeakLiftsPlanned) { PuttingDown(); return; }
        PlayTrigger(Workout!.WeakLift, () => { LiftsDone++; WeakLift(); });
    }

    private void PuttingDown()
    {
        CurrentPhase = Phase.PuttingDown;
        PlayTrigger(Workout!.PutDown, () =>
        {
            CurrentPhase = Phase.CheckPutDown;
            // the put-down animation lowers the lift and releases the cube; the engine learns the carry state
            // from the robot, this stack from the animation's completion (INFERRED, as in PutDownBlock)
            M.Docking.ReleaseCarriedObject();
            if (M.Docking.Carrying.IsCarryingObject)
            {
                CurrentPhase = Phase.ManualPutDown;
                RunAction("PlaceObjectOnGround", ct => new PlaceObjectOnGroundAction(M).RunAsync(ct), _ => PostLift());
            }
            else PostLift();
        });
    }

    private void PostLift()
    {
        CurrentPhase = Phase.PostLift;
        PlayTrigger(Workout!.PostLift, () =>
        {
            M.Workouts!.CompleteCurrentWorkout();
            // EndIteration reports the behaviour's own action here (0x005D8A18): Workout, or Workout_Sparked
            if (NeedActionCompleted() is { } action) Log($"needs action {action}");
            double nowSec = Clock() / 1000.0;
            bool known = Context.Mood?.Trigger(Workout.EmotionEventOnComplete, nowSec) ?? false;
            Log($"emotion event {Workout.EmotionEventOnComplete}: {(Context.Mood is null ? "no mood attached" : known ? "applied" : "not in the loaded mood model")}");
            Log($"objectives achieved: PerformedWorkout, {Workout.AdditionalObjectiveOnComplete}");
            CurrentPhase = Phase.Idle;
            Finish();
        });
    }
}

/// <summary>
/// <c>BehaviorBuildPyramidBase</c> (0x005DCA41..0x005DDA60) and its subclass <c>BehaviorBuildPyramid</c>
/// (0x005DBC89..0x005DC690). <c>UpdatePyramidTargets</c> asks the object-interaction cache for the best base,
/// static and top blocks (here: the located upright cubes not already in a base or pyramid, LOCAL selection);
/// runnable with at least two for the base, three for the pyramid. States (<c>SetState_internal</c> names):
/// DrivingToBaseBlock (pick-up helper on the base block), PlacingBaseBlock (<c>PlaceRelObjectHelper</c> beside
/// the static block with <c>UpdateBlockPlacementOffsets</c>: the block's dimension plus 12 mm (0x41400000),
/// checked free with <c>CheckBaseBlockPoseIsFree</c>), ObservingBase (<c>DriveStraightAction(−40, 40)</c> then
/// 0x14 <see cref="AnimationTrigger.BuildPyramidReactToBase"/>); the pyramid adds DrivingToTopBlock (pick-up
/// helper), PlacingTopBlock (<c>PlaceRelObjectHelper</c> at the base's interior midpoint, high) and
/// ReactingToPyramid (objective <c>BuiltPyramid</c>, 0x17 <see cref="AnimationTrigger.BuildPyramidSuccess"/>).
/// <c>UpdateInternal</c> stops without a repetition penalty when the targets stop being valid.
/// </summary>
public class BuildPyramidBaseBehavior : ManipulationBehavior
{
    public enum Phase { Idle, DrivingToBaseBlock, PlacingBaseBlock, ObservingBase, DrivingToTopBlock, PlacingTopBlock, ReactingToPyramid }
    public const double BasePlacementGapMm = 12.0;
    public const double ObserveBackUpMm = -40.0;
    public const float ObserveSpeedMmps = 40f;

    public BuildPyramidBaseBehavior(ManipulationSystem m, string id = "BuildPyramidBase", bool buildTop = false, string? behaviorClass = null)
        : base(id, behaviorClass ?? (buildTop ? "BuildPyramid" : "BuildPyramidBase"), m) => BuildTop = buildTop;

    public bool BuildTop { get; }
    public Phase CurrentPhase { get; protected set; }
    public uint? StaticBlockId { get; private set; }
    public uint? BaseBlockId { get; private set; }
    public uint? TopBlockId { get; private set; }

    /// <summary><c>UpdatePyramidTargets</c>: static, base and top candidates among the free upright cubes.</summary>
    protected (uint? Static, uint? Base, uint? Top) Targets()
    {
        var cfg = M.Configurations;
        var robot = M.RobotPose();
        var free = M.World.LocatedObjects.Where(o => CubeGeometry.IsCube(o.Type) && o.UpAxisFromPose() is UpAxis.ZPositive or UpAxis.ZNegative
                                                     && !cfg.IsObjectPartOfConfigurationType(o.ObjectId, BlockConfigurationType.Pyramid)
                                                     && !cfg.IsObjectPartOfConfigurationType(o.ObjectId, BlockConfigurationType.StackOfCubes))
                                        .OrderBy(o => robot is null ? 0 : (o.Pose.Translation - robot.Value.Translation).Length).ToList();
        var existingBase = cfg.PyramidBases.FirstOrDefault();
        uint? carried = M.Docking.Carrying.CarriedObjectId;
        if (existingBase is not null)
        {
            var top = free.FirstOrDefault(o => !existingBase.ContainsBlock(o.ObjectId) && (carried is null || o.ObjectId == carried));
            return (existingBase.LeftBlockId, existingBase.RightBlockId, top?.ObjectId);
        }
        var st = free.FirstOrDefault(o => o.ObjectId != carried);
        var bs = carried is { } c ? free.FirstOrDefault(o => o.ObjectId == c) : free.FirstOrDefault(o => o.ObjectId != st?.ObjectId);
        var tp = free.FirstOrDefault(o => o.ObjectId != st?.ObjectId && o.ObjectId != bs?.ObjectId);
        return (st?.ObjectId, bs?.ObjectId, tp?.ObjectId);
    }

    protected override bool IsRunnableInternal(BehaviorContext context)
    {
        var (s, b, t) = Targets();
        bool baseDone = M.Configurations.PyramidBases.Count > 0;
        if (BuildTop) return s is not null && b is not null && t is not null && M.Configurations.Pyramids.Count == 0;
        return !baseDone && s is not null && b is not null;
    }

    protected override void OnStart()
    {
        Scope.DisableReactions();
        var (s, b, t) = Targets();
        StaticBlockId = s; BaseBlockId = b; TopBlockId = t;
        if (BuildTop && M.Configurations.PyramidBases.Count > 0)
        {
            if (M.Docking.Carrying.IsCarrying(t ?? uint.MaxValue)) TransitionToPlacingTopBlock(); else TransitionToDrivingToTopBlock();
            return;
        }
        if (s is null || b is null) { Finish(); return; }
        if (M.Docking.Carrying.IsCarrying(b.Value)) TransitionToPlacingBaseBlock(); else TransitionToDrivingToBaseBlock();
    }

    private void TransitionToDrivingToBaseBlock()
    {
        CurrentPhase = Phase.DrivingToBaseBlock;
        uint id = BaseBlockId!.Value;
        var helper = new DockHelper(M);
        RunAction($"PickupBlockHelper({id})", ct => helper.RunAsync(id, PreActionType.Docking, () => new PickupObjectAction(M, id), ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            if (r == ActionResult.Success) TransitionToPlacingBaseBlock(); else { Log($"could not pick up the base block: {r}"); Finish(); }
        });
    }

    private void TransitionToPlacingBaseBlock()
    {
        CurrentPhase = Phase.PlacingBaseBlock;
        uint target = StaticBlockId!.Value;
        var stat = M.World.GetLocatedObjectById(target);
        if (stat is null) { Log($"BehaviorBuildPyramidBase.TransitionToPlacingBaseBlock.NullObject: Object {target} is NULL"); Finish(); return; }
        // UpdateBlockPlacementOffsets: beside the static block, one block dimension plus the gap, on the side the robot approaches from
        double offset = CubeGeometry.CubeSizeMm + BasePlacementGapMm;
        var helper = new DockHelper(M);
        RunAction($"PlaceRelObjectHelper({target}, beside)", ct => helper.RunAsync(target, PreActionType.PlaceRelative,
            () => new PlaceRelObjectAction(M, target, onTop: false) { Offsets = (0, offset, 0) }, ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            if (r != ActionResult.Success) { Log($"placing the base block failed: {r}"); Finish(); return; }
            TransitionToObservingBase();
        });
    }

    private void TransitionToObservingBase()
    {
        CurrentPhase = Phase.ObservingBase;
        RunAction("DriveStraight(-40 mm @ 40)", ct => new DriveStraightAction(M, ObserveBackUpMm, ObserveSpeedMmps).RunAsync(ct), _ =>
        {
            PlayTrigger(AnimationTrigger.BuildPyramidReactToBase, () =>
            {
                M.Configurations.Update();
                if (BuildTop && TopBlockId is not null && M.Configurations.PyramidBases.Count > 0) TransitionToDrivingToTopBlock();
                else { CurrentPhase = Phase.Idle; Finish(); }
            });
        });
    }

    private void TransitionToDrivingToTopBlock()
    {
        CurrentPhase = Phase.DrivingToTopBlock;
        uint id = TopBlockId!.Value;
        var helper = new DockHelper(M);
        RunAction($"PickupBlockHelper({id})", ct => helper.RunAsync(id, PreActionType.Docking, () => new PickupObjectAction(M, id), ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            if (r == ActionResult.Success) TransitionToPlacingTopBlock(); else { Log($"could not pick up the top block: {r}"); Finish(); }
        });
    }

    private void TransitionToPlacingTopBlock()
    {
        CurrentPhase = Phase.PlacingTopBlock;
        var pyramidBase = M.Configurations.PyramidBases.FirstOrDefault();
        if (pyramidBase is null) { Log("BehaviorBuildPyramid.TransitionToPlacingTopBlock.NullObject"); Finish(); return; }
        // the top block goes over the base's interior midpoint: place relative to the nearer base block, offset half a block sideways towards the midpoint
        var robot = M.RobotPose();
        var left = M.World.GetLocatedObjectById(pyramidBase.LeftBlockId); var right = M.World.GetLocatedObjectById(pyramidBase.RightBlockId);
        if (left is null || right is null || robot is null) { Finish(); return; }
        var target = (left.Pose.Translation - robot.Value.Translation).Length <= (right.Pose.Translation - robot.Value.Translation).Length ? left : right;
        var toMid = pyramidBase.InteriorMidpoint - target.Pose.Translation;
        double side = Math.Sqrt(toMid.X * toMid.X + toMid.Y * toMid.Y);
        var helper = new DockHelper(M);
        RunAction($"PlaceRelObjectHelper({target.ObjectId}, on the base midpoint)", ct => helper.RunAsync(target.ObjectId, PreActionType.PlaceRelative,
            () => new PlaceRelObjectAction(M, target.ObjectId, onTop: true) { Offsets = (0, side, 0) }, ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            if (r != ActionResult.Success) { Log($"placing the top block failed: {r}"); Finish(); return; }
            TransitionToReactingToPyramid();
        });
    }

    private void TransitionToReactingToPyramid()
    {
        CurrentPhase = Phase.ReactingToPyramid;
        Log("objective achieved: BuiltPyramid");
        // 0x005DBFDA calls the hook with Invalid, and no pyramid behaviour config carries a needsActionID,
        // so nothing is reported - the activity's "PyramidCompleted" is read into IActivity+0x1C and never
        // looked at again.
        if (NeedActionCompleted() is { } action) Log($"needs action {action}");
        PlayTrigger(AnimationTrigger.BuildPyramidSuccess, () => { CurrentPhase = Phase.Idle; Finish(); });
    }
}

/// <summary>
/// <c>BehaviorRespondPossiblyRoll</c> (0x005DE4B6..0x005DEB90, config <c>PyramidRespondPossiblyRoll</c>, the
/// pyramid activity's "prepare cubes" step): <c>DetermineNextResponse</c> reads the target cube's rotated Z
/// axis: upright → <c>TurnAndRespondPositively</c> (turn towards it, then the lift-safe animation indexed by
/// how many pyramid blocks are placed: <see cref="AnimationTrigger.BuildPyramidFirstBlockUpright"/>, Second…,
/// Third…), on its side → <c>TurnAndRespondNegatively</c> (…BlockOnSide) and then <c>DelegateToRollHelper</c>
/// (the roll-block helper); <c>UpdateInternal</c> re-responds negatively after a roll result of 5 (a failed
/// roll). Targets: located cubes not already in a pyramid base (LOCAL selection).
/// </summary>
public sealed class RespondPossiblyRollBehavior : ManipulationBehavior
{
    public enum Phase { Idle, RespondingPositively, RespondingNegatively, RollingObject }
    private static readonly AnimationTrigger[] Upright = { AnimationTrigger.BuildPyramidFirstBlockUpright, AnimationTrigger.BuildPyramidSecondBlockUpright, AnimationTrigger.BuildPyramidThirdBlockUpright };
    private static readonly AnimationTrigger[] OnSide = { AnimationTrigger.BuildPyramidFirstBlockOnSide, AnimationTrigger.BuildPyramidSecondBlockOnSide, AnimationTrigger.BuildPyramidThirdBlockOnSide };

    public RespondPossiblyRollBehavior(ManipulationSystem m, string id = "PyramidRespondPossiblyRoll") : base(id, "RespondPossiblyRoll", m) { }

    public Phase CurrentPhase { get; private set; }
    public uint? TargetObjectId { get; private set; }
    private readonly HashSet<uint> _responded = new();

    private ObservableObject? Target() => ClosestCube(o => !_responded.Contains(o.ObjectId) && !M.Configurations.IsObjectPartOfConfigurationType(o.ObjectId, BlockConfigurationType.PyramidBase));

    protected override bool IsRunnableInternal(BehaviorContext context) => !M.Docking.Carrying.IsCarryingObject && Target() is not null;

    protected override void OnStart()
    {
        Scope.DisableReactions();
        var t = Target();
        if (t is null) { Finish(); return; }
        TargetObjectId = t.ObjectId;
        _responded.Add(t.ObjectId);
        int placed = Math.Clamp(M.Configurations.PyramidBases.Count > 0 ? 2 : Math.Min(2, _responded.Count - 1), 0, 2);
        if (t.UpAxisFromPose() is UpAxis.ZPositive or UpAxis.ZNegative)
        {
            CurrentPhase = Phase.RespondingPositively;
            RunAction("TurnTowardsObject", ct => M.TurnTowardsObjectAsync(t.ObjectId, Math.PI, ct).ContinueWith(x => x.Result ? ActionResult.Success : ActionResult.Abort), _ =>
                PlayTrigger(Upright[placed], () => { CurrentPhase = Phase.Idle; Finish(); }));
        }
        else
        {
            CurrentPhase = Phase.RespondingNegatively;
            RunAction("TurnTowardsObject", ct => M.TurnTowardsObjectAsync(t.ObjectId, Math.PI, ct).ContinueWith(x => x.Result ? ActionResult.Success : ActionResult.Abort), _ =>
                PlayTrigger(OnSide[placed], DelegateToRollHelper));
        }
    }

    private void DelegateToRollHelper()
    {
        CurrentPhase = Phase.RollingObject;
        uint id = TargetObjectId!.Value;
        var helper = new DockHelper(M) { AttemptLimit = DockHelper.MaxRollAttempts };
        RunAction($"RollBlockHelper({id})", ct => helper.RunAsync(id, PreActionType.Rolling, () => new RollObjectAction(M, id), ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            Log(r == ActionResult.Success ? "rolled" : $"roll failed: {r}");
            CurrentPhase = Phase.Idle; Finish();
        });
    }
}

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
    private readonly HashSet<string> _reacted = new();

    public OnConfigSeenBehavior(ManipulationSystem m, string id, IReadOnlyList<BlockConfigurationType> configTriggers, IReadOnlyList<AnimationTrigger> animTriggers)
        : base(id, "OnConfigSeen", m) { ConfigTriggers = configTriggers; AnimTriggers = animTriggers; }

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

    protected override bool IsRunnableInternal(BehaviorContext context) => Recent() is not null;

    protected override void OnStart()
    {
        Seen = Recent();
        if (Seen is null) { Finish(); return; }
        _reacted.Add(Seen.Type + ":" + string.Join(",", Seen.BlockIds.OrderBy(i => i)));
        Log($"configuration seen: {Seen.Type} of {string.Join(",", Seen.BlockIds)}");
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
public sealed class CantHandleTallStackBehavior : ManipulationBehavior
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

    public int MinimumStackHeight { get; }
    public double InitialWait { get; } public double DownWait { get; } public double UpWait { get; }
    public double MovedThresholdMm { get; }
    public Phase CurrentPhase { get; private set; }
    public StackOfCubes? TargetStack { get; private set; }
    private Dictionary<uint, Pose3d> _posesAtStart = new();

    private StackOfCubes? Tallest() { var s = M.Configurations.GetTallestStack(); return s is not null && s.StackHeight >= MinimumStackHeight && M.World.GetLocatedObjectById(s.BottomBlockId) is not null ? s : null; }

    protected override bool IsRunnableInternal(BehaviorContext context) => Tallest() is not null;
    protected override bool KeepsRunningWithoutAction => true;

    protected override void OnStart()
    {
        TargetStack = Tallest();
        if (TargetStack is null) { Finish(); return; }
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
                    CurrentPhase = Phase.Disappointment;
                    PlayTrigger(AnimationTrigger.CantHandleTallStack, () => { CurrentPhase = Phase.Idle; Finish(); });
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
        M.Configurations.Cache(Type).Count > 0 && (NextAllowedSec is null || M.ClockSec() >= NextAllowedSec);

    protected override void OnStart()
    {
        NextAllowedSec = M.ClockSec() + CooldownSec;
        Log($"reacted to a {Type}; next allowed in {CooldownSec} s (the engine's InitInternal only arms this cooldown)");
        Finish();
    }
}

/// <summary>
/// <c>BehaviorThinkAboutBeacons</c> (config: <c>newAreaAnimTrigger</c> HikingReactToNewArea, <c>beaconRadius_mm</c>
/// 175 hiking / 75 sparks): runnable when the whiteboard has no active beacon; <c>SelectNewBeacon</c> adds one
/// at the robot's pose with the configured radius - <c>BehaviorThinkAboutBeacons::SelectNewBeacon</c>
/// 0x005E5F0C takes the robot's pose, copies it, and calls
/// <c>AIWhiteboard::AddBeacon(pose, radius)</c> with the float at behaviour+0x128 (0x005E5F28), so the
/// centre is wherever the robot stood - and the new-area animation
/// plays. INFERRED: the beacon is centred on the robot (the engine's selection logic was not read further).
/// </summary>
public sealed class ThinkAboutBeaconsBehavior : ManipulationBehavior
{
    public ThinkAboutBeaconsBehavior(ManipulationSystem m, string id = "Hiking_ThinkAboutBeacons", double beaconRadiusMm = 175, AnimationTrigger newAreaAnim = AnimationTrigger.HikingReactToNewArea)
        : base(id, "ThinkAboutBeacons", m) { BeaconRadiusMm = beaconRadiusMm; NewAreaAnim = newAreaAnim; }

    public double BeaconRadiusMm { get; }
    public AnimationTrigger NewAreaAnim { get; }
    public AIBeacon? Selected { get; private set; }

    protected override bool IsRunnableInternal(BehaviorContext context) => M.Whiteboard.GetActiveBeacon() is null && M.RobotPose() is not null;

    protected override void OnStart()
    {
        var robot = M.RobotPose();
        if (robot is null) { Finish(); return; }
        Selected = M.Whiteboard.AddBeacon(new Pose3d(Mat3.AboutZ(robot.Value.AngleAroundZ), robot.Value.Translation with { Z = 0 }), BeaconRadiusMm);
        Log($"SelectNewBeacon: beacon at {Selected.Pose.Translation} radius {BeaconRadiusMm}");
        PlayTrigger(NewAreaAnim, Finish);
    }
}

/// <summary>
/// <c>BehaviorExploreBringCubeToBeacon</c> (exports: <c>GetCandidate</c>, <c>TransitionToPickUpObject</c>,
/// <c>TransitionToObjectPickedUp</c>, <c>FindFreeCubeToStackOn</c>, <c>TryToStackOn</c>, <c>FindFreePoseInBeacon</c>,
/// <c>TryToPlaceAt</c>, <c>FireEmotionEvents</c>; config <c>recentFailureCooldown_sec</c> 45 hiking / 5 sparks):
/// runnable with an active beacon and a usable cube outside every beacon (<c>FindUsableCubesOutOfBeacons</c>,
/// excluding cubes that failed within the cooldown). Pick the cube up (pick-up helper; failure →
/// <c>SetFailedToUse</c>), then either stack it on a free upright cube already in the beacon
/// (<c>PlaceRelObjectHelper</c>) or place it at a free pose inside the beacon (drive there, <c>PlaceObjectOnGround</c>).
/// The free-pose search is LOCAL: the beacon centre, then a ring of candidates at half the radius, keeping
/// 60 mm from every located cube. The emotion events' names were not read (DEFERRED).
/// </summary>
public sealed class BringCubeToBeaconBehavior : ManipulationBehavior
{
    public enum Phase { Idle, PickingUp, StackingOn, PlacingAt }
    public const double FreePoseClearanceMm = 60.0;

    public BringCubeToBeaconBehavior(ManipulationSystem m, string id = "Hiking_BringCubeToBeacon", double recentFailureCooldownSec = 45)
        : base(id, "BringCubeToBeacon", m) => RecentFailureCooldownSec = recentFailureCooldownSec;

    public double RecentFailureCooldownSec { get; }
    public Phase CurrentPhase { get; private set; }
    public uint? Candidate { get; private set; }
    public Vec3? PlacedAt { get; private set; }

    private ObservableObject? GetCandidate()
    {
        var robot = M.RobotPose();
        return M.Whiteboard.FindUsableCubesOutOfBeacons(ObjectActionFailure.Any, RecentFailureCooldownSec)
            .Where(o => !M.Docking.Carrying.IsCarrying(o.ObjectId))
            .OrderBy(o => robot is null ? 0 : (o.Pose.Translation - robot.Value.Translation).Length).FirstOrDefault();
    }

    /// <summary>
    /// The emotion event a placed cube fires when others are still out:
    /// <c>BehaviorExploreBringCubeToBeacon::FireEmotionEvents</c> 0x005E002C takes this branch when
    /// <c>AIWhiteboard::AreAllCubesInBeacons</c> says no (0x005E0068).
    /// </summary>
    public const string CubeEmotionEvent = "HikingBroughtCubeToBeacon";

    /// <summary>And the one it fires when that was the last cube (0x005E0046).</summary>
    public const string LastCubeEmotionEvent = "HikingBroughtLastCubeToBeacon";

    protected override bool IsRunnableInternal(BehaviorContext context) => M.Whiteboard.GetActiveBeacon() is not null && !M.Docking.Carrying.IsCarryingObject && GetCandidate() is not null;

    protected override void OnStart()
    {
        Scope.DisableReactions();
        var c = GetCandidate();
        if (c is null) { Finish(); return; }
        Candidate = c.ObjectId;
        CurrentPhase = Phase.PickingUp;
        var helper = new DockHelper(M);
        RunAction($"PickupBlockHelper({c.ObjectId})", ct => helper.RunAsync(c.ObjectId, PreActionType.Docking, () => new PickupObjectAction(M, c.ObjectId), ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            if (r != ActionResult.Success) { M.Whiteboard.SetFailedToUse(c.ObjectId, ObjectActionFailure.PickUpObject); Log($"pick-up failed: {r}; SetFailedToUse"); Finish(); return; }
            TransitionToObjectPickedUp();
        });
    }

    private void TransitionToObjectPickedUp()
    {
        // 0x005DF5A0 names the action outright: PickupCube (0x1F), whatever the config says
        if (NeedActionCompleted("PickupCube") is { } action) Log($"needs action {action}");
        var beacon = M.Whiteboard.GetActiveBeacon();
        if (beacon is null) { Finish(); return; }
        var stackOn = M.Whiteboard.FindCubesInBeacon(beacon)
            .FirstOrDefault(o => o.ObjectId != Candidate && o.UpAxisFromPose() is UpAxis.ZPositive or UpAxis.ZNegative
                                 && !M.Configurations.IsObjectPartOfConfigurationType(o.ObjectId, BlockConfigurationType.StackOfCubes));
        if (stackOn is not null) { TryToStackOn(stackOn.ObjectId); return; }
        var pose = FindFreePoseInBeacon(beacon);
        if (pose is null) { beacon.FailedToFindLocation(); Log("FindFreePoseInBeacon: none free"); Finish(); return; }
        TryToPlaceAt(pose.Value);
    }

    private void TryToStackOn(uint target)
    {
        CurrentPhase = Phase.StackingOn;
        var helper = new DockHelper(M);
        RunAction($"PlaceRelObjectHelper({target}, on top)", ct => helper.RunAsync(target, PreActionType.PlaceRelative, () => new PlaceRelObjectAction(M, target, onTop: true), ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            if (r != ActionResult.Success) M.Whiteboard.SetFailedToUse(target, ObjectActionFailure.StackOnObject);
            Log(r == ActionResult.Success ? "stacked in the beacon" : $"stacking failed: {r}");
            CurrentPhase = Phase.Idle; Finish();
        });
    }

    /// <summary>A pose inside the beacon at least <see cref="FreePoseClearanceMm"/> from every located cube (LOCAL search).</summary>
    public Vec3? FindFreePoseInBeacon(AIBeacon beacon)
    {
        var cubes = M.World.LocatedObjects.Where(o => CubeGeometry.IsCube(o.Type) && !M.Docking.Carrying.IsCarrying(o.ObjectId)).ToList();
        bool Free(Vec3 p) => cubes.All(o => { var d = o.Pose.Translation - p; return Math.Sqrt(d.X * d.X + d.Y * d.Y) >= FreePoseClearanceMm; });
        var centre = beacon.Pose.Translation with { Z = 0 };
        if (Free(centre)) return centre;
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4;
            var p = centre + new Vec3(Math.Cos(a), Math.Sin(a), 0) * (beacon.RadiusMm / 2);
            if (Free(p)) return p;
        }
        return null;
    }

    private void TryToPlaceAt(Vec3 target)
    {
        CurrentPhase = Phase.PlacingAt;
        RunAction($"drive to {target} and place", async ct =>
        {
            var robot = M.RobotPose();
            if (robot is null) return ActionResult.Abort;
            // stand so the carried cube (about one cube ahead of the robot's origin) lands on the target
            var dir = (target - robot.Value.Translation) with { Z = 0 };
            double heading = Math.Atan2(dir.Y, dir.X);
            var stand = target - new Vec3(Math.Cos(heading), Math.Sin(heading), 0) * (CubeGeometry.CubeSizeMm + FlipBlockAction.DrivePastMm);
            var drive = new DriveToPoseAction(M) { Goal = new Pose3d(Mat3.AboutZ(heading), stand), IgnoreObstacleIds = Candidate is { } c ? new[] { c } : null };
            var d = await drive.RunAsync(ct);
            foreach (var l in drive.Trace) Log("  " + l);
            if (d != ActionResult.Success) return d;
            return await new PlaceObjectOnGroundAction(M).RunAsync(ct);
        }, r =>
        {
            if (r == ActionResult.Success)
            {
                PlacedAt = target;
                // FireEmotionEvents 0x005E002C: one name or the other, on the mood manager at Robot+0x440.
                string ev = M.Whiteboard.AreAllCubesInBeacons() ? LastCubeEmotionEvent : CubeEmotionEvent;
                bool known = Context.Mood?.Trigger(ev, Clock() / 1000.0) ?? false;
                Log($"placed in the beacon; emotion event {ev}: " +
                    (Context.Mood is null ? "no mood attached" : known ? "applied" : "not in the loaded mood model"));
            }
            else if (Candidate is { } c) { M.Whiteboard.SetFailedToUse(c, ObjectActionFailure.PlaceObjectAt); Log($"placing failed: {r}"); }
            CurrentPhase = Phase.Idle; Finish();
        });
    }
}
