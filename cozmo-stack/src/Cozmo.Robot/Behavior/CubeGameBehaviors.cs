using Cozmo.Protocol;
using Cozmo.Robot.Animation;
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
// fidelity: M13-014
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
    /// <summary>+0xD9: the JSON <c>alwaysStreamline</c> key (M13-014).</summary>
    public bool AlwaysStreamline { get; init; }
    /// <summary>
    /// +0xD8: the runtime soft-spark-switch flag (<c>IBehavior::Init</c> 0x005BCCAA computes it from the
    /// BehaviorManager's switch mode; 1 = soft). The stack has no spark-switch notion, so it is settable.
    /// </summary>
    public bool SoftSparkSwitch { get; set; }
    public Phase CurrentPhase { get; private set; }
    public StackOfCubes? TargetStack { get; private set; }
    public bool? KnockedOver { get; private set; }
    /// <summary>
    /// The tipped-object set at BehaviourKnockOverCubes+0x144: <c>HandleObjectUpAxisChanged</c> 0x005C3A98
    /// inserts every object whose up axis changed (the message tag is 0x11, ObjectUpAxisChanged), and
    /// <c>TransitionToPlayingReaction</c> 0x005C3908 picks the success or failure trigger by its size at
    /// +0x14C (0x005C3950..0x005C3972). This is not restricted to the target stack's blocks.
    /// </summary>
    private readonly HashSet<uint> _tipped = new();

    private StackOfCubes? Tallest() { var s = M.Configurations.GetTallestStack(); return s is not null && s.StackHeight >= MinimumStackHeight ? s : null; }

    protected override bool IsRunnableInternal(BehaviorContext context) => !M.Docking.Carrying.IsCarryingObject && Tallest() is not null;

    protected override void OnStart()
    {
        TargetStack = Tallest();
        if (TargetStack is null) { Log("no stack"); Finish(); return; }
        // fidelity: M7-014
        // BehaviorKnockOverCubes::InitInternal 0x005C31A2 calls InitializeMemberVars (0x005C31D8) first. When the target stack is there (the weak pointer at +0x120 locks and +0x11C is non-null,
        // 0x005C31DC..0x005C31EE) it takes SmartDisableReactionsWithLock(own name, table 0x00C67CB2) at 0x005C31FA, before it resets the tipped set (0x005C3206..0x005C3214); with no stack it returns 0
        // with no lock and InitInternal reports failure. The former arbiter-wide Scope.DisableReactions() had no engine counterpart.
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.KnockOverCubes);
        _tipped.Clear(); KnockedOver = null; KnockOverAttempts = 0;
        M.World.ObjectObserved += OnObserved;
        // InitInternal 0x005C31A2: run the reach unless +0xD9 (alwaysStreamline) or +0xD8 (soft spark
        // switch) is set; streamline goes straight to knocking over (0x005C31B4..0x005C31D0).
        if (AlwaysStreamline || SoftSparkSwitch) TransitionToKnockingOverStack();
        else TransitionToReachingForBlock();
    }

    internal void OnObserved(ObjectObservation o)
    {
        // HandleObjectUpAxisChanged 0x005C3A98: any object whose up axis changed is inserted into the
        // tipped set; it is not restricted to the target stack.
        if (o.Object.UpAxisFromPose() != UpAxisOf(o.PreviousPose)) _tipped.Add(o.Object.ObjectId);
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
        PrepareForKnockOverAttempt();
        uint bottom = TargetStack!.BottomBlockId;
        // the maximum turn towards a face: pi/2 on the first attempt, 0 once the attempt count at +0x140 is
        // above zero, and 0 when +0xD9/+0xD8 is set (0x005C34EA..0x005C3504); the trailing 20.0 has no effect
        var flip = new DriveAndFlipBlockAction(M, bottom) { MaxTurnTowardsFaceRad = (AlwaysStreamline || SoftSparkSwitch || KnockOverAttempts > 0) ? 0.0 : Math.PI / 2 };
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
        PrepareForKnockOverAttempt();
        uint bottom = TargetStack!.BottomBlockId;
        var flip = new FlipBlockAction(M, bottom) { CheckPreActionPose = false };
        RunAction("FlipBlockAction (blind)", flip.RunAsync, _ =>
        {
            foreach (var l in flip.Trace) Log("  " + l);
            Wait(BlindFlipWaitSec, TransitionToPlayingReaction);
        });
    }

    /// <summary>
    /// <c>PrepareForKnockOverAttempt</c> 0x005C3780: zero the tipped-object set at +0x144/+0x14C at the
    /// start of every knock-over attempt (called at 0x005C3606 inside <c>TransitionToKnockingOverStack</c>,
    /// and from <c>TransitionToBlindlyFlipping</c>). A Retry must not carry a prior attempt's tipped object
    /// into <c>TransitionToPlayingReaction</c>, or it would report success where the engine reports failure.
    /// </summary>
    // fidelity: M13-014
    private void PrepareForKnockOverAttempt()
    {
        _tipped.Clear();
        // fidelity: M7-014
        // 0x005C37A8: IncreaseScoreWhileActing(10.0f) (PLT 0x4B01BC, r1 = 0x41200000 = 10.0f exactly), after the tipped set is cleared (0x005C378A..0x005C37A4).
        // fidelity: M8-003
        IncreaseScoreWhileActing(10.0);
        // 0x005C37AE..0x005C37F0: SmartRemoveDisableReactionsLock("preparingToKnockOverDisable", 0x005C37C2) and then SmartDisableReactionsWithLock of the same name with the all-zero table 0x00C67CDC (0x005C37F0).
        Scope.SmartRemoveDisableReactionsLock(ReactionLockTables.KnockOverCubesPreparingName);
        Scope.SmartDisableReactionsWithLock(ReactionLockTables.KnockOverCubesPreparingName, ReactionLockTables.KnockOverCubesPreparing);
    }

    private void TransitionToPlayingReaction()
    {
        CurrentPhase = Phase.PlayingReaction;
        // TransitionToPlayingReaction 0x005C3946..0x005C394E sets robot+0x34->+0x94->+0xC = 1, i.e.
        // BlockWorld's BlockConfigurationManager dirty flag, forcing all configurations to recompute on
        // the next Update.
        M.Configurations.ForceUpdate = true;
        M.Configurations.Update();
        // TransitionToPlayingReaction 0x005C3908 selects the success trigger (+0x15C) or the failure
        // trigger (+0x160) by the tipped-object set size at +0x14C != 0. The earlier stack-height check
        // was not the engine's selector.
        KnockedOver = _tipped.Count != 0;
        Log(KnockedOver.Value ? "the stack came apart" : "the stack is still standing");
        var trigger = KnockedOver.Value ? SuccessTrigger : FailureTrigger;
        // the flag at +0x14c gates both the objective and the needs action (0x005C3950): a stack still
        // standing reports neither
        if (KnockedOver.Value && NeedActionCompleted() is { } action) Log($"needs action {action}");
        // when +0xD9/+0xD8 is set the reaction animation is skipped (0x005C3972..0x005C397C)
        if (AlwaysStreamline || SoftSparkSwitch)
        {
            if (KnockedOver.Value) Log("objective achieved: KnockedOverBlocks");
            CurrentPhase = Phase.Idle;
            Finish();
            return;
        }
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
/// otherwise (0x005C780C), so one retry at most; a Retry result with the retry used, or a category-3
/// (Abort) result, marks the cube failed to use for <see cref="ObjectActionFailure.RollOrPopAWheelie"/>
/// (SetFailedToUse(obj, 3) at 0x005C7DAA); anything else logs BehaviorPopAWheelie.FailedPopAction and ends. <c>SetupRetryAction</c> 0x005C79D0
/// plays 0x18D when the result is exactly DidNotReachPreActionPose (0x04000001, 0x005C79F8..0x005C7A00) and 0x18E
/// otherwise, then runs the action again as a retry (0x005C7F66).
/// </summary>
// fidelity: M13-015
public sealed class PopAWheelieBehavior : ManipulationBehavior
{
    public enum Phase { Idle, ReactingToBlock, PerformingAction, Retrying }
    public const int MaxRetries = 1;

    /// <summary>The <c>BehaviorObjective</c> the success path reports: 0x16 = 22 PoppedWheelie (<c>movs r1,#0x16</c> at 0x005C7E08).</summary>
    public const int ObjectivePoppedWheelie = 0x16;
    /// <summary><c>SetFailedToUse(obj, 3)</c>'s failure kind (<c>movs r2,#3</c> at 0x005C7DA2): <see cref="ObjectActionFailure.RollOrPopAWheelie"/>.</summary>
    public const ObjectActionFailure FailureKind = ObjectActionFailure.RollOrPopAWheelie;

    public PopAWheelieBehavior(ManipulationSystem m, string id = "PopAWheelie") : base(id, "PopAWheelie", m) { }

    public Phase CurrentPhase { get; private set; }
    public uint? TargetObjectId { get; private set; }
    /// <summary>The retry count at +0x12C: bumped by <c>TransitionToPerformingAction(robot, true)</c> (0x005C777E), zeroed by the non-retry call (0x005C780C).</summary>
    public int Retries { get; private set; }
    public bool Succeeded { get; private set; }
    /// <summary>+0x128: the success path stores -1 (0x005C7CF6/0x005C7CFA); its readers are not in the inventory.</summary>
    public int Field0x128 { get; private set; }

    private ObservableObject? Target() => ClosestCube(o => o.UpAxisFromPose() is UpAxis.ZPositive or UpAxis.ZNegative);

    protected override bool IsRunnableInternal(BehaviorContext context) => !M.Docking.Carrying.IsCarryingObject && Target() is not null;

    protected override void OnStart()
    {
        // fidelity: M7-014
        // BehaviorPopAWheelie::InitInternal 0x005C7496 takes no reaction lock (it is [+0xD8]/[+0xD9]/[+0x120]/[+0x128] tests and a tail call, 0x005C7496..0x005C74BC); the lock is taken by the
        // pre-dock callback, PreDockCallback below. The arbiter-wide Scope.DisableReactions() that stood here had no engine counterpart.
        TargetObjectId = Target()?.ObjectId;
        if (TargetObjectId is null) { Finish(); return; }
        Retries = 0; Succeeded = false;
        CurrentPhase = Phase.ReactingToBlock;
        PlayTrigger(AnimationTrigger.PopAWheelieInitial, () => TransitionToPerformingAction(retry: false));
    }

    /// <summary>
    /// <c>TransitionToPerformingAction(Robot&amp;, bool)</c> 0x005C7758: the retry count is bumped when called as a retry and zeroed otherwise; the action
    /// runs under <c>StartActing</c> and its completion goes to <see cref="OnActionComplete"/> (the lambda 0x005C7CBC).
    /// </summary>
    // fidelity: M13-015
    private void TransitionToPerformingAction(bool retry)
    {
        CurrentPhase = Phase.PerformingAction;
        uint id = TargetObjectId!.Value;
        if (retry) { Retries++; Log($"info: BehaviorPopAWheelie.TransitionToPerformingAction.Retrying: Retry {Retries} of {MaxRetries}"); }   // adds r1,#1 at 0x005C777E
        else Retries = 0;                                                                                           // str.w r0,[r4,#0x12c] at 0x005C780C
        SteppedBehavior.ReportMissing("BehaviorPopAWheelie::TransitionToPerformingAction (0x005C7758..): the [+0x120] == -1 warning gate, the [+0xD8]/[+0xD9] branch (0x005C7810..0x005C781C), the DriveToPopAWheelieAction's say-name triggers 0x18B/0x18C and the IDriveToInteractWithObject turn-towards-face sub-actions are not in the M13 inventory; the drive and the pop run as this stack's two async actions");
        int handle = StartActing();
        if (handle == 0) return;
        int epoch = CallbackEpoch;
        var scope = Scope;                       // the run this action belongs to: a stale action must not reach the next run's scope
        int run;
        lock (_cliffGate) run = _run;
        RunAction($"DriveToPopAWheelieAction({id})", async ct =>
        {
            var drive = new DriveToObjectAction(M, id, PreActionType.Docking);
            var d = await drive.RunAsync(ct);
            foreach (var l in drive.Trace) Log("  " + l);
            if (d != ActionResult.Success) return d;
            if (ct.IsCancellationRequested) return ActionResult.CancelledWhileRunning;     // a cancelled list never runs the WaitForLambdaAction
            PreDockCallback(scope, run, ct);
            var pop = new PopAWheelieAction(M, id) { CheckPreActionPose = false };
            var r = await pop.RunAsync(ct);
            foreach (var l in pop.Trace) Log("  " + l);
            return r;
        }, r =>
        {
            ActingEnded(handle);                                   // HandleActionComplete clears +0x84 before the callback (0x005BE1FC)
            if (!CallbackMayRun(epoch)) return;
            OnActionComplete(id, r);
        });
    }

    /// <summary>
    /// The std::function body 0x005C7BC8 that TransitionToPerformingAction stores at the DriveToPopAWheelieAction's +0xE0 (0x005C786C..0x005C7880). Its invoker is the lambda $_3 of
    /// IDriveToInteractWithObject::AddDockAction (operator() 0x0055E068: it calls the function at +0xE0 with the robot at 0x0055E0C8..0x0055E0D4 and returns true), which AddDockAction
    /// (0x0055B7AC) wraps in a WaitForLambdaAction (constructor via 0x0055B554) and adds to the sequence BEFORE the dock action (0x0055B7F2..0x0055B802, then the dock action at 0x0055B838): it
    /// runs after the drive and before the pop. The body: SmartDisableReactionsWithLock(own name, table 0x00C6883D) (0x005C7BE8), [+0x130] = 1 (0x005C7BF0), then
    /// <c>EnableStopOnCliff(false)</c> through Robot::SendMessage(msg, 1, 0) (0x005C7BF4..0x005C7C0A).
    /// </summary>
    // fidelity: M7-014, M13-015
    private void PreDockCallback(BehaviorScope scope, int run, CancellationToken ct)
    {
        // The engine runs this inside the tick-driven action list: a stop or a cancel means it never runs. Here it runs on the action's pool thread, so the run is checked and the
        // +0x130 flag is set together with the send, under one monitor that OnStop also takes: a stop either sees the flag and restores, or has already ended the run and nothing is sent.
        lock (_cliffGate)
        {
            if (ct.IsCancellationRequested || run != _run) return;
            _stopOnCliffDisabled = true;
            M.Robot.SendMessage(new EnableStopOnCliff { Enable = false }, flush: true);
            Log("EnableStopOnCliff(false)");
        }
        // The engine body (0x005C7BC8) takes the lock first, then sets the flag and sends; here the flag and send come first, because the lock goes through the manager OUTSIDE that monitor (the manager's stop path holds its monitor and then calls OnStop); the captured scope is disposed with its run, which
        // takes the lock back if the stop came in between.
        scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.PopAWheelie);
    }

    private readonly object _cliffGate = new();
    private int _run;

    /// <summary>BehaviorPopAWheelie +0x130: set by <see cref="PreDockCallback"/>, cleared (with EnableStopOnCliff(true)) by ResetBehavior 0x005C76B0 at StopInternal.</summary>
    private bool _stopOnCliffDisabled;

    /// <summary>
    /// The completion lambda 0x005C7CBC, in its order: a non-zero result first removes the behaviour's reaction lock (<c>SmartRemoveDisableReactionsLock</c>, 0x005C7CD4..0x005C7CDA);
    /// then the category byte (<c>result &gt;&gt; 24</c>): 4 with the retry count at most 0 (<c>ble</c> at 0x005C7D4A, signed) goes to <c>SetupRetryAction</c> (0x005C7DFA); 4 with the retry used
    /// and 3 log "BehaviorPopAWheelie.FailedAbort" and, when the object at +0x11C is still in the world (<c>GetLocatedObjectByIdHelper</c>, family -1), call
    /// <c>AIWhiteboard::SetFailedToUse(obj, 3)</c>; 0 stores +0x128 = -1, starts the 0x21C animation, THEN reports objective 0x16 and the needs action; any other category logs
    /// "BehaviorPopAWheelie.FailedPopAction".
    /// </summary>
    // fidelity: M13-015
    private void OnActionComplete(uint id, ActionResult r)
    {
        // 0x005C7CD4..0x005C7CDA: a non-zero result removes the callback's lock (SmartRemoveDisableReactionsLock, own name).
        if (r != ActionResult.Success) Scope.SmartRemoveDisableReactionsLock(Id);
        uint category = (uint)r >> 24;
        if (category == 4 && Retries <= 0) { SetupRetryAction(r); return; }
        if (category is 4 or 3)
        {
            Log($"info: BehaviorPopAWheelie.FailedAbort: Failed to pop with {r}, searching for block");
            if (M.World.GetLocatedObjectById(id) is not null)
            {
                M.Whiteboard.SetFailedToUse(id, FailureKind);
                Log($"giving up: {r}; SetFailedToUse");
            }
            Finish();
            return;
        }
        if (category == 0)
        {
            Field0x128 = -1;
            Succeeded = true;
            PlayTrigger(AnimationTrigger.SuccessfulWheelie, Finish);                 // StartActing(TriggerAnimationAction 0x21C) first (0x005C7D22..0x005C7D30)
            Log($"objective achieved: PoppedWheelie (BehaviorObjectiveAchieved(0x{ObjectivePoppedWheelie:X}, true))");   // then 0x005C7E0C
            if (NeedActionCompleted() is { } action) Log($"needs action {action}");                                          // then 0x005C7E14
            return;
        }
        Log($"info: BehaviorPopAWheelie.FailedPopAction: action failed with {r}, behavior ending");
        Finish();
    }

    /// <summary>
    /// <c>SetupRetryAction(Robot&amp;, RobotCompletedAction const&amp;)</c> 0x005C79D0: a virtual call through slot +0x90 first (0x005C79E6..0x005C79EC, not in the inventory), then a
    /// <c>TriggerLiftSafeAnimationAction(0x18D)</c> when the result is exactly 0x04000001 and <c>(0x18E)</c> otherwise (60.0f timeout), run with <c>StartActing</c> and a callback
    /// that performs the action again as a retry (0x005C7A5E, then the lambda).
    /// </summary>
    // fidelity: M13-015
    private void SetupRetryAction(ActionResult r)
    {
        SteppedBehavior.ReportMissing("BehaviorPopAWheelie::SetupRetryAction 0x005C79E6: the virtual call through vtable slot +0x90 before the animation is not in the M13 inventory; it is not made");
        CurrentPhase = Phase.Retrying;
        int handle = StartActing();
        if (handle == 0) return;
        var trigger = r == ActionResult.DidNotReachPreActionPose ? AnimationTrigger.PopAWheelieRealign : AnimationTrigger.PopAWheelieRetry;
        RunTriggerAction(handle, trigger, _ => TransitionToPerformingAction(retry: true), AnimationTrack.None, TriggerAnimationTimeoutSec, numLoops: 1, liftSafe: true);
    }

    protected override void OnStop(BehaviorStopReason reason)
    {
        // StopInternal 0x005C76AC tail-calls ResetBehavior 0x005C76B0: only when +0x130 is set (0x005C76C8..0x005C76D0) is it cleared and EnableStopOnCliff(true) sent (0x005C76DA..0x005C76F0).
        lock (_cliffGate)
        {
            _run++;                                          // ends this run for any callback still in flight
            if (_stopOnCliffDisabled)
            {
                _stopOnCliffDisabled = false;
                M.Robot.SendMessage(new EnableStopOnCliff { Enable = true }, flush: true);
                Log("EnableStopOnCliff(true)");
            }
        }
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
        // fidelity: M7-014
        // BehaviorRamIntoBlock::InitInternal 0x00604764 only picks TransitionToPuttingDownBlock (carrying) or TransitionToTurningToBlock, with no lock; the engine's lock is the first call of
        // TransitionToRammingIntoBlock (0x00604A44), taken in Ram() below.
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
        // fidelity: M7-014
        // BehaviorRamIntoBlock::TransitionToRammingIntoBlock 0x00604A20: SmartDisableReactionsWithLock(own name, table 0x00C73520) is its first call (0x00604A44), before anything else.
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.RamIntoBlock);
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
        // fidelity: M7-014
        // BehaviorCubeLiftWorkout::InitInternal 0x005d7eb8: SmartDisableReactionsWithLock(name, table 0x00c6b7e0) first (0x005d7ec6..0x005d7ec8), then SmartPushIdleAnimation(Count 0x23f).
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.CubeLiftWorkout);
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
            // M13-010: CompleteCurrentWorkout itself fires the finished workout's emotion event
            // (MoodManager::TriggerEmotionEvent 0x00573E1C) before advancing; wire the mood here.
            M.Workouts!.TriggerEmotionEvent = (ev, now) =>
            {
                bool known = Context.Mood?.Trigger(ev, now) ?? false;
                Log($"emotion event {ev}: {(Context.Mood is null ? "no mood attached" : known ? "applied" : "not in the loaded mood model")}");
                return known;
            };
            M.Workouts.ClockSec = () => Clock() / 1000.0;
            M.Workouts!.CompleteCurrentWorkout();
            // EndIteration reports the behaviour's own action here (0x005D8A18): Workout, or Workout_Sparked
            if (NeedActionCompleted() is { } action) Log($"needs action {action}");
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
        // fidelity: M7-014
        // BehaviorBuildPyramidBase has no SmartDisableReactionsWithLock call site (the full caller list of IBehavior::SmartDisableReactionsWithLock has only BehaviorBuildPyramid::
        // TransitionToPlacingTopBlock 0x005DC0D6 for this pair), so Start takes no lock; the arbiter-wide Scope.DisableReactions() that stood here had no engine counterpart.
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
        // fidelity: M7-014
        // BehaviorBuildPyramid::TransitionToPlacingTopBlock 0x005DC08C: SetState_internal(5, state name) (0x005DC0BA), then SmartDisableReactionsWithLock(own name, table 0x00C6C691) (0x005DC0D6).
        // Only the BuildPyramid class (buildTop) reaches this method.
        Scope.SmartDisableReactionsWithLock(Id, ReactionLockTables.BuildPyramid);
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
        // fidelity: M7-014
        // BehaviorRespondPossiblyRoll has no SmartDisableReactionsWithLock call site in the engine (every call to its PLT stub 0x004B28EC was listed: IBehavior::Init/Resume and 27 class sites, none in
        // this class), so it takes no lock; the arbiter-wide Scope.DisableReactions() that stood here had no engine counterpart.
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
// fidelity: M15-012
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
/// plays.
/// </summary>
// fidelity: M15-011
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
        // fidelity: M15-011
        // SelectNewBeacon 0x005E5F0C: Robot::GetPose, copied (Pose3d::Pose3d 0x005E5F1A), and AddBeacon(copy, [this+0x128]) (0x005E5F28..0x005E5F30): the whole robot pose, rotation and
        // translation including z, not a planar pose rebuilt from the yaw.
        Selected = M.Whiteboard.AddBeacon(robot.Value, BeaconRadiusMm);
        Log($"SelectNewBeacon: beacon at {Selected.Pose.Translation} radius {BeaconRadiusMm}");
        PlayTrigger(NewAreaAnim, Finish);
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
/// <c>IBehavior::StartActing</c> PLT 0x004B01C8): this stack runs ONE attempt of its drive-to-pre-action-pose plus <c>PickupObjectAction</c> (<c>DockHelper</c> with an attempt limit of 1 and no search) in its
/// place, so the <c>IDriveToInteractWithObject</c> extras (the face turn, the name animation and the 0x23F trigger) are not played. The completion lambda 0x005E0FD8 and the retry path 0x005DF8E6 ARE
/// built (<see cref="PickUpCompleted"/>, <see cref="TransitionToPickUpObject"/>);</item>
/// <item><c>DriveToPlaceOnObjectAction</c> (0x005DFE0E; no record owns it) behind <c>TryToStackOn</c>; the stack action in <see cref="TryToStackOn"/> is the stand-in for it alone and is labelled as such;</item>
/// <item>the unread bodies the floor path reaches: <c>BlockWorld::ClearLocatedObjectByIDInCurOrigin</c> (after a failed verify) and <c>RotationMatrix3d::Renormalize</c> in the candidate pose's matrix.</item>
/// </list>
/// </summary>
// fidelity: M15-009, M15-008, M15-019, M15-020, M15-021, M15-022, M15-023, M15-024, M15-025
public sealed class BringCubeToBeaconBehavior : ManipulationBehavior
{
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
        // fidelity: M7-014
        // BehaviorExploreBringCubeToBeacon has no IBehavior::SmartDisableReactionsWithLock call site in the engine (every call to its PLT stub 0x004B28EC was listed: IBehavior::Init/Resume and 27 class sites, none in this class),
        // so it takes no reaction lock; the arbiter-wide Scope.DisableReactions() that stood here had no engine counterpart.
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
            var robot = M.RobotPose();
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
        CurrentPhase = Phase.PickingUp;
        _actionsStarted++;
        SteppedBehavior.ReportMissing("BehaviorExploreBringCubeToBeacon::TransitionToPickUpObject 0x005DF95E..0x005DF98A: DriveToPickupObjectAction (ctor PLT 0x004A93B8, trigger 0x23F, started through IBehavior::StartActing PLT 0x004B01C8) is not built; one attempt of this stack's drive-to-pre-action-pose plus PickupObjectAction (DockHelper, attempt limit 1, no search) runs in its place and its result goes to the completion lambda 0x005E0FD8");
        var helper = new DockHelper(M) { AttemptLimit = 1, SearchOnFailure = false };
        RunAction($"DriveToPickupObject({id}) attempt {attempt}", ct => helper.RunAsync(id, PreActionType.Docking, () => new PickupObjectAction(M, id), ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            PickUpCompleted(r, attempt);
        });
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

    /// <summary>Test seam: the <see cref="PlaceObjectOnGroundAction"/> the latest <c>TryToPlaceAt</c> ran (not engine behaviour).</summary>
    internal PlaceObjectOnGroundAction? LastPlaceAction { get; private set; }

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
    internal bool FindFreePoseInBeacon(ObservableObject carried, AIBeacon beacon, Pose3d robotPose, float cooldown, out Pose3d result)
    {
        Pose3d last = default;
        SteppedBehavior.ReportMissing("BehaviorExploreBringCubeToBeacon::FindFreePoseInBeacon libm: MathF.Cos/MathF.Sin/MathF.Acos/MathF.Atan2 stand in for bionic cosf 0x004A415C, sinf 0x004A4168, acosf 0x004ACC4C and atan2f 0x004A4510, and re-analysis/evidence/m15-floor-placement/emulate_candidates.py takes the expected bits (e.g. 0x42380001) from Python's libm, not bionic, so a last-bit difference in those functions is not excluded. 0x005E0A96: the candidate pose's rotation matrix is the quaternion-to-matrix of Rotation3d::GetRotationMatrix 0x0084AAE0 WITHOUT its closing RotationMatrix3d::Renormalize 0x008494E0 (unread: its tolerances and RenormalizeUnconditional 0x004CE3C0), and Pose3d::GetWithRespectTo is the stack's double composition narrowed to f32; both can differ from the engine in the last bits");
        var rot = BeaconFloorGeometry.RotationAboutAxis(0.0f, 0.0f, 0.0f, 1.0f);                                  // 0x005E038A..0x005E03A2
        bool haveFrame = false;
        var cubes = M.Whiteboard.FindCubesInBeacon(beacon);                                                        // 0x005E03AC..0x005E03B6
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
            CandidateLog?.Add((i, j));
            // 0x005E09E0..0x005E0DF2
            var (tx, ty, tz, rotated) = BeaconFloorGeometry.CandidateTranslation(rot, spacing, i, j, bx, by, bz);
            var pose = last = new Pose3d(BeaconFloorGeometry.RotationMatrix(rot), new Vec3(tx, ty, tz));                      // *out = Pose3d(rot, t, origin, ""), BEFORE any test (0x005E0A96..0x005E0A9E)
            float d2 = rotated.X * rotated.X;
            d2 += rotated.Y * rotated.Y;
            d2 += rotated.Z * rotated.Z;
            if (d2 > radius2) return false;                                                                         // 0x005E0ADE..0x005E0AE6 ble: NaN passes
            // 0x005E0B4A..0x005E0B7E: DidFailToUse(-1, PlaceObjectAt = 2, cooldown, *out, 100.0f, Radians(pi))
            if (M.Whiteboard.DidFailToUse(-1, ObjectActionFailure.PlaceObjectAt, cooldown, pose, BeaconFloorGeometry.FarDistance, BeaconFloorGeometry.Pi)) return false;
            // 0x005E0B84..0x005E0C32: FindLocatedIntersectingObjects(quad of the carried object at *out, padding 10.0f), the carried id ignored; free iff empty
            return DriveToPlaceCarriedObjectAction.StaticFindLocatedIntersectingObjectsIsEmpty(M, carried, pose, BeaconFloorGeometry.Ten, null);
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
        var trace = new List<string>();
        RunAction(FormattableString.Invariant($"PlaceObjectOnGroundAtPose({pose.Translation.X:F2},{pose.Translation.Y:F2},{pose.Translation.Z:F2}) attempt {attempt}"), async ct =>
        {
            var drive = new DriveToPlaceCarriedObjectAction(M, pose, true, false, false, true, BeaconFloorGeometry.Ten);
            var r = await drive.RunAsync(ct);
            trace.AddRange(drive.Trace.Select(l => "  " + l));
            if (r != ActionResult.Success) { trace.Add($"Current action DriveToPlaceCarriedObject[0] failed with {r}"); return r; }       // 0x0054F7F2..0x0054F8F4
            var place = new PlaceObjectOnGroundAction(M) { ReactionLocks = Scope.Manager };
            LastPlaceAction = place;
            r = await place.RunAsync(ct);
            trace.AddRange(place.Trace.Select(l => "  " + l));
            if (r != ActionResult.Success) trace.Add($"Current action PlaceObjectOnGround[1] failed with {r}");
            return r;
        }, r =>
        {
            foreach (var l in trace) Log(l);
            PlaceCompleted(r, pose, attempt);
        });
    }

    /// <summary>
    /// The completion callback 0x005E188C by result category (the top byte, 0x005E18A0..0x005E18A8). 0 (success): "Successfully placed cube" and <c>FireEmotionEvents</c> (0x005E1916), with NO
    /// <c>NeedActionCompleted</c> and no <c>SetFailedToUse</c> (M15-025). 4 (retry): with the carried id equal to the target (not -1) and <c>attempt &lt;= 2</c> (0x005E193E, signed) the SAME pose is
    /// tried again with <c>attempt + 1</c> (no new <c>FindFreePoseInBeacon</c>); otherwise "CannotRetry" (max 3). Every other non-zero category: "NoRetryAllowed". Both failures end in the tail
    /// 0x005E1B32..0x005E1B56: when the target is still located, <c>SetFailedToUse(obj, PlaceObjectAt = 2, pose)</c> with the CANDIDATE pose.
    /// </summary>
    // fidelity: M15-021, M15-024, M15-025
    internal void PlaceCompleted(ActionResult r, Pose3d pose, int attempt)
    {
        uint category = (uint)r >> 24;
        if (category == 0)
        {
            Log($"info: Behaviors.{Id}.onPlaceActionResult.Done: Successfully placed cube");
            FireEmotionEvents();
            CurrentPhase = Phase.Idle; Finish();
            return;
        }
        if (category == 4)
        {
            uint? carried = M.Docking.Carrying.CarriedObjectId;                                                     // [[robot+0x284]+8]; null = -1
            bool carryingTarget = carried is not null && carried == Candidate;                                      // 0x005E1928..0x005E1944
            if (carryingTarget && attempt <= 2)
            {
                Log(FormattableString.Invariant($"info: Behaviors.{Id}.onPlaceActionResult.Done.CanRetry: Failed to place '{Candidate}' at pose [{pose.Translation.X:F2},{pose.Translation.Y:F2},{pose.Translation.Z:F2}]"));
                TryToPlaceAt(pose, attempt + 1);                                                                    // 0x005E19F8 adds r3,r0,#1
                return;
            }
            Log(FormattableString.Invariant($"info: Behaviors.{Id}.onPlaceActionResult.CannotRetry: Failed to place '{Candidate}' at pose [{pose.Translation.X:F2},{pose.Translation.Y:F2},{pose.Translation.Z:F2}] (attempt={attempt}/3) (carrying={(carryingTarget ? "yes" : "no")})"));
        }
        else Log($"info: Behaviors.{Id}.onPlaceActionResult.NoRetryAllowed: Failed to place (no retry allowed by action)");
        if (Candidate is { } id && M.World.GetLocatedObjectById(id) is { } obj) M.Whiteboard.SetFailedToUse(obj, ObjectActionFailure.PlaceObjectAt, pose);   // 0x005E1B32..0x005E1B56
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
        CurrentPhase = Phase.StackingOn;
        _actionsStarted++;
        SteppedBehavior.ReportMissing("BehaviorExploreBringCubeToBeacon::TryToStackOn 0x005DFDD4 starts DriveToPlaceOnObjectAction (0x005DFE0E), which is not built; the stack runs PlaceRelObjectHelper as a labelled stand-in (M15-008)");
        var helper = new DockHelper(M);
        RunAction($"PlaceRelObjectHelper({target}, on top; stand-in for DriveToPlaceOnObjectAction)", ct => helper.RunAsync(target, PreActionType.PlaceRelative, () => new PlaceRelObjectAction(M, target, onTop: true), ct), r =>
        {
            foreach (var l in helper.Trace) Log("  " + l);
            StackCompleted(r, target, attempt);
        });
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
        float half = angle * 0.5f;                       // vmov.f32 s0,#0.5; vmul.f32 0x0084A526..0x0084A52E
        float c = MathF.Cos(half), s = MathF.Sin(half);  // 0x0084A538, 0x0084A540
        double w = c, x = s * ax, y = s * ay, z = s * az; // vmul.f32 s6 * axis, 0x0084A558..0x0084A562; vcvt.f64.f32 0x0084A568..0x0084A574
        return Normalize(w, x, y, z);
    }

    /// <summary><c>UnitQuaternion_&lt;double&gt;::Normalize</c> 0x00849DB8..0x00849E52.</summary>
    public static Quat Normalize(double w, double x, double y, double z)
    {
        double n2 = w * w;
        n2 += x * x;
        n2 += y * y;
        n2 += z * z;
        if (Math.Abs(1.0 - n2) < NormalizeTolerance)    // vcmpe d3,d4; bpl -> the sqrt path (so NaN takes it)
        {
            double scale = 2.0 / (n2 + 1.0);             // vadd, vdiv 0x00849DF4..0x00849DFE
            return new Quat(scale * w, scale * x, scale * y, scale * z);
        }
        double len = Math.Sqrt(n2);
        return new Quat(w / len, x / len, y / len, z / len);
    }

    /// <summary><c>Rotation3d::operator*(Point3f const&amp;)</c> 0x0084ACA6 into <c>UnitQuaternion_&lt;float&gt;::operator*(Point3f)</c> 0x00849C1C, each f32 operation in the listed order.</summary>
    public static (float X, float Y, float Z) Rotate(Quat q, float px, float py, float pz)
    {
        float a = (float)q.W, b = (float)q.X, c = (float)q.Y, d = (float)q.Z;     // vcvt.f32.f64 0x0084ACCC
        float aa = a * a, bb = b * b, ab = a * b, ac = a * c, ad = a * d, bd = b * d, bc = b * c, cd = c * d;   // 0x00849C24..0x00849C54
        float aa_bb = aa - bb;
        float aa_pl_bb = aa + bb;
        float cc = c * c;
        float ad2 = ad + ad;
        float dd = d * d;
        float bc2 = bc + bc;
        float cd2 = cd + cd;
        float ab2 = ab + ab;
        float ac2 = ac + ac;
        float bd2 = bd + bd;
        float m3 = aa_bb + cc;
        float m8 = aa_pl_bb - cc;
        float m4 = aa_bb - cc;
        float s5 = bc2 + ad2;
        float s0 = bc2 - ad2;
        float s7 = ab2 + cd2;
        float s9 = bd2 - ac2;
        m3 = m3 - dd;
        float s2 = m8 - dd;
        m4 = m4 + dd;
        float s6 = cd2 - ab2;
        float t10 = px * s5;
        float t8 = py * s7;
        float t7 = px * s9;
        float t12 = m3 * py;
        float t14 = ac2 + bd2;
        float t0 = py * s0;
        float t2 = px * s2;
        float t4 = m4 * pz;
        float t6 = s6 * pz;
        t8 = t7 + t8;
        t10 = t12 + t10;
        t12 = t14 * pz;
        t0 = t2 + t0;
        t2 = t4 + t8;
        t4 = t6 + t10;
        t0 = t12 + t0;
        return (t0, t4, t2);                                                     // stored [r0], [r0+4], [r0+8]
    }

    /// <summary><c>Rotation3d::GetRotationMatrix</c> 0x0084AAE0 (the matrix entries; the closing <c>RotationMatrix3d::Renormalize</c> 0x008494E0 is NOT applied: unread, MISSING).</summary>
    public static Mat3 RotationMatrix(Quat q)
    {
        double w = q.W, x = q.X, y = q.Y, z = q.Z;
        float zz = (float)(z * z), yy = (float)(y * y), zw = (float)(z * w), xx = (float)(x * x), xy = (float)(x * y), xw = (float)(x * w), yz = (float)(y * z);
        float yw = (float)(y * w), xz = (float)(x * z);
        float s3 = yy + zz;
        float s4 = xx + zz;
        float s6 = xx + yy;
        float s12 = xz + yw;
        float s14 = xz - yw;
        float s7 = xy - zw;
        float s8 = xy + zw;
        float s10 = yz - xw;
        float s0 = yz + xw;
        float s1 = s3 * -2.0f;
        s4 = s4 + s4;
        s6 = s6 + s6;
        float m01 = s7 + s7;
        float m02 = s12 + s12;
        float m10 = s8 + s8;
        float m12 = s10 + s10;
        float m20 = s14 + s14;
        float m21 = s0 + s0;
        float m00 = s1 + 1.0f;
        float m11 = 1.0f - s4;
        float m22 = 1.0f - s6;
        return new Mat3(m00, m01, m02, m10, m11, m12, m20, m21, m22);
    }

    /// <summary><c>Rotation3d::GetAngleAroundZaxis</c> 0x0084AA1C from the matrix entries (f32): <c>atan2f(R10, R00)</c> when <c>R10^2 + R00^2 &gt; R01^2 + R11^2</c>, else <c>atan2f(-R01, R11)</c>; then <c>Radians(float)</c>.</summary>
    public static float AngleAroundZ(Mat3 r)
    {
        float r00 = (float)r[0, 0], r10 = (float)r[1, 0], r01 = (float)r[0, 1], r11 = (float)r[1, 1];
        float s8 = r01 * r01;
        s8 = s8 + r11 * r11;
        float s10 = r10 * r10;
        s10 = s10 + r00 * r00;
        float angle = s10 > s8 ? MathF.Atan2(r10, r00) : MathF.Atan2(-r01, r11);
        return EngineRadians32.Rescale(angle);
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
        return RotationAboutAxis(AngleAroundZ(closest.Pose.Rotation), 0.0f, 0.0f, 1.0f);
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
