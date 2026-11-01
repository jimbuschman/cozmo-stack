using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>One freeplay decision, for the log and the tests.</summary>
public sealed record FreeplayDecision(double AtSec, string? Activity, string? Behavior, string Reason);

/// <summary>
/// The bounded ActivityFreeplay chooser (0x005AE29C), called by BehaviorManager after reactions.
/// A kept activity may cause one re-pick; the newly picked activity is asked once. Putdown immediately deselects
/// eligible activities but does not calculate the object-derived desired cache. The canonical Look state-6
/// path calculates it through the retained parent; standalone callers still lack that owner join.
/// </summary>
// fidelity: M15-002
// fidelity: M15-006
public sealed class FreeplaySystem : IManagedActivity, IHostReadinessOwner
{
    private readonly BehaviorManager _manager;
    private readonly Action<BehaviorManager.ReactionSwitch> _reactionObserver;
    private readonly BehaviorContext _ctx;
    private readonly IReadOnlyDictionary<string, IBehavior> _bound;
    private bool _requestPending;
    private string? _desiredActivity;
    // Explicit standalone test seam; canonical Look state6 writes the retained parent directly.
    internal void SetDesiredActivityCacheForTest(string? id) => _desiredActivity = ActivityIdOrNull(id);
    internal string? DesiredActivityCacheForTest => _desiredActivity;
    // 005ADF4C: called by retained Freeplay, independent of current high-level selection.
    internal bool CalculateNativeDesired(EngineRobot owner, ManipulationSystem manipulation,Func<bool> stillCurrent)
    {
        if (Freeplay.DesiredActivityNames is not {} names || !ReferenceEquals(owner.Engine.Robot,owner) ||
            !ReferenceEquals(manipulation.Robot.LocalizationWorld,manipulation.World))
        { ReportActivityMissing("M15-008 retained Freeplay desired calculation actual owner/config unavailable"); return false; }
        using var mutation=manipulation.World.EnterPoseMutation();
        uint originId=owner.CurrentOriginId;
        if(!owner.TryGetNativeOriginOwner(originId,out var origin) || origin?.Node is null)
        { ReportActivityMissing("M15-008 desired calculation current origin unavailable; prior cache retained"); return false; }
        using var pose=new Pose3d("");
        bool face=manipulation.Vision.Faces.CopyNativeLastFace(origin,true,pose);
        if(face) { using var root=pose.FindRoot();face=ReferenceEquals(root.Node,origin.Node); }
        var filter=new BlockWorldFilter();filter.AllowedFamilies.Add((ObjectFamily)1);filter.AllowedFamilies.Add((ObjectFamily)2);
        bool cube=manipulation.World.FindLocatedObjectHelper(filter,null,true) is not null;
        if(!stillCurrent() || owner.CurrentOriginId!=originId || !owner.TryGetNativeOriginOwner(originId,out var current) || !ReferenceEquals(current,origin))
        { ReportActivityMissing("M15-008 desired calculation owner changed; prior cache retained");return false; }
        _desiredActivity=ActivityIdOrNull(face ? (cube ? names.FaceAndCube : names.FaceOnly) : (cube ? names.CubeOnly : names.None));
        return true;
    }
    internal byte? SelectedChildActivityId { get; private set; }
    internal string? SelectedChildActivityName { get; private set; }
    /// <summary><c>BehaviorManager+0x65</c>: the invalid-spark latch (C1 §1).</summary>
    private bool _requestedSparkInvalid;

    // The tick in progress: the clock it runs on, whether a decision has been recorded in it, and what the
    // activity pick decided (read back by BehaviorSwitched once the manager has switched).
    private double _tickNowSec;
    private bool _recordedThisTick;
    private string _switchReason = "";
    private string? _switchedFromId;

    public FreeplaySystem(BehaviorManager manager, BehaviorContext ctx, Activity freeplay, IReadOnlyDictionary<string, IBehavior> bound, FreeplayInputs inputs)
        :this(manager,ctx,freeplay,bound,inputs,true) { }
    internal FreeplaySystem(BehaviorManager manager,BehaviorContext ctx,Activity freeplay,IReadOnlyDictionary<string,IBehavior> bound,FreeplayInputs inputs,bool attach)
    {
        _manager = manager; _ctx = ctx; Freeplay = freeplay; _bound = bound; Inputs = inputs;
        foreach (var b in bound.Values) if (manager.Find(b.Id) is null) manager.Add(b);
        // BehaviorManager::GetCurrentActivity (0x005a2f78): the manager ticks this activity, asks it for the
        // desired behaviour behind the reaction check, and reports what a reaction switched to.
        if(attach)manager.Activity = this;
        _reactionObserver=sw => { Record(_tickNowSec, Current?.Id, sw.Behavior, $"reaction {sw.Trigger}"); _recordedThisTick = true; };
        manager.ReactionTriggered += _reactionObserver;
    }

    internal void DetachHostHandle()
    {
        _manager.ReactionTriggered-=_reactionObserver;
        if(ReferenceEquals(_manager.Activity,this))_manager.Activity=null;
    }

    public Activity Freeplay { get; }
    public FreeplayInputs Inputs { get; }
    public Activity? Current { get; private set; }
    private bool _changingActivity;
    private bool _childUnavailable;
    public IReadOnlyList<FreeplayDecision> Decisions => _decisions;
    private readonly List<FreeplayDecision> _decisions = new();
    public event Action<FreeplayDecision>? Decided;
    private void ReportActivityMissing(string line)
    {
        if (_ctx.Robot is { } robot) robot.Engine.EmitStackLog(line);
        else Log?.Invoke(line);
    }

    public event Action<string>? Log;
    /// <summary>Debug / test: force the next pick to this activity id (the engine's console var "debug is forcing '%s'").</summary>
    public string? ForcedActivity { get; set; }

    /// <summary>
    /// The Spark freeplay-pause flag, set from the manager's requested raw ID after a changed pick
    /// (0x005AE4E2..0x005AE4F2). The host points this at the tracker.
    /// </summary>
    // fidelity: M15-015
    public Action<bool>? SparkPauseChanged { get; set; }

    internal void ParentDeselected(double nowSec) => EndActivity(nowSec);

    /// <summary>The robot was put back on its treads: the activity is kicked out and re-picked from what is around.</summary>
    // fidelity: M15-060
    public void OnRobotPutDown(double nowSec)
    {
        if (_changingActivity || Current is not { } activity || activity.Id is "NeedsSevereLowEnergy" or "NeedsSevereLowRepair" ||
            activity.Id == ActivityIdOrNull(ForcedActivity) || activity.RequiredSparkId != 0x55) return;
        _changingActivity = true;
        try
        {
            Log?.Invoke($"ActivityFreeplay.RobotOffTreadsStateChanged.KickingOutActivityOnPutDown: Kicking out '{activity.Id}' on put down so we pick up a new one");
            EndActivity(nowSec);
        }
        finally { _changingActivity = false; }
    }

    /// <summary>
    /// The engine's requested-activity path (<c>GetDesiredActiveBehaviorInternal</c> 0x005AE2F4: the byte at +0x90,
    /// "Picking new activity because '%s' was requested"): the current activity is kicked out on the next tick and a
    /// new one picked. The engine sets it when the high-level activity comes back to Freeplay (the app's Feeding,
    /// MeetCozmo and Selection activities run outside freeplay; a feed happens there, not inside a freeplay activity).
    /// </summary>
    public void RequestNewActivity() => _requestPending = true;

    /// <summary>
    /// Legacy host string seam for the requested spark (the native <c>BehaviorManager+0x60</c> is a 32-bit ID).
    /// It mirrors the invalid-spark latch at <c>BehaviorManager+0x65</c> (C1 §1): <c>HandleMessage</c> case tag 0
    /// sets <c>+0x65 = 1</c> only when <c>UnlockId == 0x55</c> (0x005A3C92..0x005A3C9A); the stack's
    /// <c>null</c> is that invalid spark. A non-null request does not clear the host latch.
    /// The native raw-ID picker reads BehaviorManager fields; this legacy string seam is only used by the public host picker.
    /// </summary>
    // fidelity: M15-006
    public void SetRequestedSpark(string? spark)
    {
        Inputs.RequestedSpark = spark;
        if (spark is null) _requestedSparkInvalid = true;
        ReportActivityMissing("MISSING: M15-006 host Spark name to native manager raw requested ID join");
    }

    /// <summary>The invalid-spark latch (<c>BehaviorManager+0x65</c>).</summary>
    // fidelity: M15-006
    public bool RequestedSparkInvalid => _requestedSparkInvalid;

    /// <summary><c>CalculateDesiredActivityFromObjects</c>: the configured activity for the faces and cubes known.</summary>
    public string? DesiredActivityFromObjects()
    {
        ReportActivityMissing("MISSING: M15-008 standalone DesiredActivityFromObjects has no retained native Robot/world input; canonical Look state6 uses its actual owner");
        return null;
    }

    /// <summary>
    /// One tick: <c>BehaviorManager::Update</c> in the engine's order (0x005a2f68..0x005a31be) - the activity's own tick,
    /// <c>CheckReactionTriggerStrategies</c> every tick, then <c>ChooseNextScoredBehaviorAndSwitch</c> behind its three
    /// gates, then the running behaviour's <c>Update</c>. The activity pick and the chooser are this class's
    /// <see cref="IManagedActivity.GetDesiredActiveBehavior"/>, which the manager calls at that point, so they no longer
    /// run (with their side effects) before the reaction check.
    /// </summary>
    // fidelity: M8-012
    internal Func<FreeplayDecision>? ObserveOnly;
    internal bool NativeOwner;
    internal Func<bool>? NativeStillCurrent;
    private bool OwnerCurrent()=>!NativeOwner || NativeStillCurrent?.Invoke()==true;
    bool IHostReadinessOwner.ReadinessChecksEnabled=>NativeOwner;
    bool IHostReadinessOwner.EvaluateCandidateReadiness=>Current?.SparkedOwner is not null;
    void IHostReadinessOwner.RetireUnavailable()
    {
        _childUnavailable=true;
        ReportActivityMissing("MISSING: M15-059 Sparked behavior readiness unavailable; parent and child retained without native completion");
    }
    public FreeplayDecision Tick(double nowSec, double nowMs)
    {
        if(ObserveOnly is {} observe)return observe();
        // Mood first, and on this tick's clock. The engine updates it from Robot::Update (0x00513E8A)
        // every tick, whatever the behaviour system then decides, and MoodManager reads its time from
        // BaseStationTimer - the same clock the emotion events are stamped with
        // (GetCurrentTimeInSeconds 0x0067ADA8). Without this the decay only ran when something else
        // happened to advance it, so an emotion stayed wherever an event left it and everything that
        // reads mood - the scoring, the gating - read a value that should long since have decayed.
        bool standaloneMood=!ReferenceEquals(_ctx.Robot.Engine.Robot?.MoodOwner,_ctx.Mood);
        if(standaloneMood)_ctx.Mood?.Advance(nowSec);
        // MoodManager::Update 0x0067b5d4 ends with SendEmotionsToGame (0x0067b6a4): with a robot attached it builds the
        // nine-float MoodState message and raises it on MoodState.MoodStateBroadcast (no engine-to-game sink exists: M7-012).
        if(standaloneMood)_ctx.Mood?.SendEmotionsToGame();
        // The NeedsManager is not ticked here: the engine calls NeedsManager::Update from its own tick,
        // between MessageHandler::ProcessMessages and UpdateAllRobots, on BaseStationTimer seconds
        // (M1-024, 0x004ED640). FreeplayStack.Create hands the manager to CozmoEngine.NeedsUpdate.
        _tickNowSec = nowSec;
        _recordedThisTick = false;
        _manager.Update(nowMs, nowSec);
        if (!_recordedThisTick)
            Record(nowSec, Current?.Id, _manager.Current?.Id,
                   _manager.CurrentReactionTrigger is not null ? "a reaction is running" : "nothing was decided this tick");
        return _decisions[^1];
    }

    /// <summary>
    /// <c>IActivity::Update</c> (<c>vtable+0x20</c>, 0x005a2f84): <c>ActivityFreeplay::Update</c> 0x005ad9d0 forwards to the
    /// running sub-activity's own <c>Update</c>, and the sub-activities that override it (BuildPyramid, Feeding,
    /// GatherCubes, Sparked) are M7/M15 bodies this stack has not built, so there is nothing for it to run.
    /// </summary>
    void IManagedActivity.Update(double nowSec)
    {
        if(_ctx.Robot.Engine.Robot is {} robot)_=UpdateNativeFreeplay(robot); // manager discards native return
        else if(Current is not null)SteppedBehavior.ReportMissing("M15-055 ActivityFreeplay.Update original Robot argument0x005AD9D0 is unbound");
    }
    // fidelity: M15-055
    internal uint? UpdateNativeFreeplay(EngineRobot robot)
    {
        var current=Current; // reload +80 each call
        if(current is null)return 0;
        if(current.SparkedOwner is {} spark){spark.Update();return 0;}
        if(current.NativeUpdate20 is {} update)return update(robot);
        SteppedBehavior.ReportMissing("M15-055 ActivityFreeplay delegate vt+20 at0x005AD9DE for "+current.Id+"; native owner unbound, forwarding unit built");return null;
    }

    /// <summary>
    /// What the freeplay activity answers <c>ChooseNextScoredBehaviorAndSwitch</c> 0x005a2a20 with: the activity pick
    /// (<c>ActivityFreeplay::GetDesiredActiveBehaviorInternal</c> 0x005AE29C) and then the chosen activity's
    /// <c>IActivity::GetDesiredActiveBehavior</c> 0x005B387C with its interlude. The manager calls it only after the
    /// reaction check found nothing, and switches to the answer when it differs from <paramref name="current"/>.
    /// </summary>
    // fidelity: M15-002
    IBehavior? IManagedActivity.GetDesiredActiveBehavior(IBehavior? current, double nowSec)
    {
        if (_changingActivity) return current; // host callback reentry cannot nest an activity transition
        _switchedFromId = null;
        Activity? barred = null;
        bool pick = Current is null;
        if (Current is { } existing)
        {
            var wanted = ActivityIdOrNull(ForcedActivity) ?? _desiredActivity;
            if (wanted is not null) pick = wanted != existing.Id;
            else if (_requestPending || (_manager.ActiveSparkId == 0x55 && _manager.RequestedSparkId != 0x55)) pick = true;
            else if (current is null && !(Inputs.Needs?.SparksRewardPending ?? false))
                pick = existing.Strategy.WantsToEnd(Inputs, existing.RunningSec(nowSec), out _, nowSec);
        }
        _requestPending = false;
        bool initialPick = pick;

        // Only the existing-activity path may return to the picker (0x005AE656..762).
        ChooserDecision decision;
        if (!pick)
        {
            decision = ChooseActivityBehavior(Current!, current, nowSec);
            if(_childUnavailable)return current;
            bool retry = decision.Behavior is null;
            if (retry) { Log?.Invoke("ActivityFreeplay.ChooseNextBehavior.NoBehaviorChosenWhileRunning: activity is not allowed to be repicked"); barred = Current; } // askCurrent=false
            bool running = decision.Behavior is SteppedBehavior stepped ? stepped.EngineRunning : _manager.IsRunning(decision.Behavior!);
            if(NativeOwner && !retry && decision.Behavior is not SteppedBehavior)
            {ReportActivityMissing("MISSING: actual child running flag unavailable");return current;}
            if (!retry && !running && !(Inputs.Needs?.SparksRewardPending ?? false))
            {
                retry = Current!.Strategy.WantsToEnd(Inputs, Current.RunningSec(nowSec), out _, nowSec);
                if (retry) Log?.Invoke($"ActivityFreeplay.ChooseNextBehavior.NewBehaviorChosenWhileRunning: '{Current.Id}' wants to end");
            }
            if (retry) pick = true; // non-running candidate: askCurrent=true
        }
        else decision = new ChooserDecision(null, "no chooser", Array.Empty<(string, double, string)>());

        if (pick)
        {
            if (Inputs.RequestedSpark is not null)
            {
                ReportActivityMissing("MISSING: M15-006 legacy host Spark string to native raw request join; retaining current selection");
                return current;
            }
            int requested = _manager.RequestedSparkId; // picker and pause snapshot, 0x005AE40E/005AE4E8
            if (_manager.SparkCancelLatch != 0 && _manager.ActiveSparkId == requested) barred = Current;
            // Unknown configured members may change the native bucket's result; do not guess past them.
            if (Freeplay.SubActivities.Any(a => a.Type == "Missing"))
            {
                ReportActivityMissing("MISSING: M15-006 configured activity has unknown Spark bucket and type; selection retained");
                return current;
            }
            // A missing map key retains the current activity; an exhausted bucket selects null.
            bool hasBucket = Freeplay.SubActivities.Any(a => a.Type != "Missing" && a.RequiredSparkId == requested);
            string pickReason;
            Activity? picked;
            if (hasBucket) picked = PickNewActivityForId(nowSec, requested, out pickReason, barred);
            else { picked = Current; pickReason = "no activities for requested spark"; Log?.Invoke("ActivityFreeplay.PickNewActivityForSpark: " + pickReason); }
            if (picked is { Type: "Sparked", SparkedOwner: not { CanExecute: true } } && !ReferenceEquals(picked, Current))
            {
                ReportActivityMissing($"MISSING: M15-059 Sparked activity '{picked.Id}' native lifecycle owner is unavailable or cannot execute; selection retained");
                return current;
            }
            bool changed = !ReferenceEquals(picked, Current);
            if (changed)
            {
                _changingActivity = true;
                try
                {
                    EndActivity(nowSec);
                    if(!OwnerCurrent())return current;
                    Current = picked;
                    if (picked is not null)
                    {
                        SelectedChildActivityId = ActivityIdNames.FromName(picked.Id);
                        SelectedChildActivityName = ActivityIdNames.Name(ActivityIdNames.FromName(Freeplay.Id)) + "_" + ActivityIdNames.Name(SelectedChildActivityId.Value);
                        ReportActivityMissing("MISSING: M15-006 SetActivityIDFromSubActivity 0x005AD8C8 parent+92/+94 downstream owner/consumers are unbound");
                        if(!OwnerCurrent() || !ReferenceEquals(Current,picked))return current;
                        picked.OnSelected(nowSec, ReportActivityMissing);
                        if(!OwnerCurrent() || !ReferenceEquals(Current,picked))return current;
                        _ctx.LastActivitySwitchSec = nowSec;
                        Log?.Invoke($"robot.freeplay_goal_started {picked.Id}: {pickReason}");
                        if(!OwnerCurrent() || !ReferenceEquals(Current,picked))return current;
                    }
                }
                finally { _changingActivity = false; }
            }
            if (changed)
            {
                _manager.SwitchToNativeRequestedSpark();
                _requestedSparkInvalid = false;
                SparkPauseChanged?.Invoke(requested != 0x55);
                if(!OwnerCurrent())return current;
            }
            if (Current is null)
            {
                Record(nowSec, null, null, $"ActivityFreeplay.NoActivitySelected: Picked no activity ({pickReason})");
                _desiredActivity = null; // normal pick tail follows the no-current log (0x005AE5A4..5E8)
                _recordedThisTick = true;
                return null;
            }
            if (initialPick || changed) decision = ChooseActivityBehavior(Current, current, nowSec);
            if(_childUnavailable)return current; // missing child input is not a native null/normal tail
            _desiredActivity = null; // after lifecycle, switch, pause and chooser (0x005AE5E0)
        }

        var desired = decision.Behavior;
        if(NativeOwner && desired is NativeLookForFaceAndCubeBehavior look && !look.ExecutionAvailable)
        {
            ReportActivityMissing("MISSING: M15-006 LookForFaceAndCube actual input owner unavailable; retaining current behavior");
            desired=current;
        }
        _switchReason = decision.Reason;
        if (desired is null && current is not null) _switchedFromId = current.Id;
        if (ReferenceEquals(desired, current))
        {
            Record(nowSec, Current!.Id, current?.Id, current is null ? decision.Reason : "keeps running");
            _recordedThisTick = true;
        }
        return desired;
    }

    // IActivity::GetDesiredActiveBehavior 0x005B387C caches the interlude itself.
    private ChooserDecision ChooseActivityBehavior(Activity activity, IBehavior? current, double nowSec)
    {
        _childUnavailable=false;
        if (activity.SparkedOwner is {} spark)
        {
            var desired=spark.Desired(current,nowSec,_ctx);
            if(spark.InputUnavailable)
            {
                ReportActivityMissing("MISSING: M15-059 Sparked child input unavailable; current behavior retained");
                _childUnavailable=true;
                return new ChooserDecision(current,"Sparked input unavailable",Array.Empty<(string,double,string)>());
            }
            return new ChooserDecision(desired,"Sparked child",Array.Empty<(string,double,string)>());
        }
        bool checkedNeeds=activity.Type=="NeedsExpression" && (activity.Id is "NeedsSevereLowEnergy" or "NeedsSevereLowRepair" or "NeedsSevereLowPlayGetIn") && activity.NativeUpdate20 is not null;
        if (NativeOwner && activity.Type is not ("BehaviorsOnly" or "Socialize") && !checkedNeeds)
        {
            ReportActivityMissing("MISSING: M8-013 specialized child desired lifecycle "+activity.Id);
            _childUnavailable=true;
            return new ChooserDecision(current,"specialized child unavailable",Array.Empty<(string,double,string)>());
        }
        if(NativeOwner && activity.Chooser?.BehaviorIds.Any(id=>_bound.TryGetValue(id,out var candidate)
            && candidate is NativeLookForFaceAndCubeBehavior look && !look.HasCheckedObjective41)==true)
        {
            ReportActivityMissing("MISSING: M15-006 changed LookForFaceAndCube objective input; retaining current behavior");
            _childUnavailable=true;
            return new ChooserDecision(current,"Look objective unavailable",Array.Empty<(string,double,string)>());
        }
        if (activity.CachedInterlude is not null && ReferenceEquals(activity.CachedInterlude, current))
            return new ChooserDecision(activity.CachedInterlude, "cached interlude", Array.Empty<(string, double, string)>());
        var priorInterlude=activity.CachedInterlude;
        activity.CachedInterlude = null;
        var decision = activity.Chooser?.GetDesiredActiveBehavior(current, _manager.RunningDurationSec(nowSec), _ctx, nowSec)
            ?? new ChooserDecision(null, "no chooser", Array.Empty<(string, double, string)>());
        if(NativeOwner && decision.InputUnavailable)
        {activity.CachedInterlude=priorInterlude;_childUnavailable=true;ReportActivityMissing("MISSING: M8-013 scored chooser input unavailable; retaining current behavior");return decision with {Behavior=current};}
        if(NativeOwner && activity.Type=="Socialize")
        {
            if(activity.ObjectiveOwner is null)
            {activity.CachedInterlude=priorInterlude;_childUnavailable=true;ReportActivityMissing("MISSING: Socialize retained objective owner unavailable");return decision with {Behavior=current,InputUnavailable=true};}
            decision=activity.ObjectiveOwner.Desired(current,decision,_ctx);
            if(decision.InputUnavailable)
            {activity.CachedInterlude=priorInterlude;_childUnavailable=true;ReportActivityMissing("MISSING: Socialize actual child input unavailable; retaining current behavior");return decision with {Behavior=current};}
        }
        if (activity.InterludeChooser is not null && !ReferenceEquals(decision.Behavior, current))
        {
            var interlude=activity.InterludeChooser.GetDesiredActiveBehavior(decision.Behavior, 0, _ctx, nowSec);
            if(NativeOwner && interlude.InputUnavailable)
            {activity.CachedInterlude=priorInterlude;_childUnavailable=true;ReportActivityMissing("MISSING: actual interlude input unavailable; retaining current behavior");return decision with {Behavior=current,InputUnavailable=true};}
            activity.CachedInterlude = interlude.Behavior;
            if (activity.CachedInterlude is not null) return decision with { Behavior = activity.CachedInterlude };
        }
        return decision;
    }
    /// <summary>
    /// What the manager's switch did, recorded as the activity's decision. The interrupted behaviour is NOT recorded as
    /// having run here: the engine keeps StopWithoutImmediateRepetitionPenalty for exactly that case, and the manager
    /// records the repetition for a behaviour it stopped.
    /// </summary>
    void IManagedActivity.BehaviorSwitched(IBehavior? desired, bool started, double nowSec)
    {
        _recordedThisTick = true;
        if (desired is null)
        {
            Record(nowSec, Current?.Id, null, $"{_switchedFromId} no longer runnable");
            return;
        }
        Record(nowSec, Current?.Id, started ? desired.Id : null, started ? _switchReason : $"{desired.Id} refused to start");
    }

    /// <summary>
    /// <c>IActivity::OnDeselected</c> 0x005B33B8, in its order: stamp the end time (+0x58, the cooldown's
    /// origin), tell the chooser (its vtable +0x24 - only <c>SelectionBSRunnableChooser::OnDeselected</c>
    /// 0x0060AF64 does anything, disabling its behaviour's info-analyzer process), remove the idle animation
    /// from the streamer and the driving animations from the handler, drop the info-analyzer enable request,
    /// release every <c>SmartDisableReactions</c> lock and clear the set, clear the current-behaviour pointer
    /// (+0x2c), tell the needs manager the sparks reward was communicated if one was pending (+0x3d8,
    /// <c>NeedsManager::SparksRewardCommunicatedToUser</c>), and log <c>robot.freeplay_goal_ended</c> with the
    /// duration in whole seconds - a negative one being the error "IActivity.Exit.NegativeDuration".
    /// The scoped acquisitions are the behaviour scope's business here, and the idle and driving animations are
    /// not modelled at the activity level; the rest is this.
    /// </summary>
    private void EndActivity(double nowSec)
    {
        if (Current is null) return;
        var ending = Current;
        double ranSec = ending.RunningSec(nowSec);
        ending.OnDeselected(nowSec, ReportActivityMissing, Inputs.Needs);
        if(!OwnerCurrent() || !ReferenceEquals(Current,ending))return;
        // No needs action is reported here. An activity's needsActionID is stored at IActivity+0x1C
        // (ReadConfig, 0x005B2A9C) and never read again anywhere in the engine; the behaviours report their
        // own (IBehavior::NeedActionCompleted 0x005BE40C, see BehaviorNeedsActions). An earlier version of
        // this stack reported the activity's id when the activity ended, which the engine never does.
        Log?.Invoke($"robot.freeplay_goal_ended {ending.Id}: ran {(int)ranSec} s");
        Current = null;
        // The engine only touches the Spark pause flag (flag 1) after a pick (row 20), never here.
    }

    /// <summary>The names of <c>Anki.Cozmo.ActivityID</c> (unity/scripts/csharp/Anki.Cozmo/ActivityID.cs), Invalid (0) excluded: what <c>ActivityIDFromString</c> 0x00766C68 knows.</summary>
    private static readonly HashSet<string> KnownActivityIds = new(StringComparer.Ordinal)
    {
        "BuildPyramid", "Feeding", "Freeplay", "GatherCubes", "Hiking", "MeetCozmo", "NeedsSevereLowEnergy", "NeedsSevereLowRepair", "NeedsSevereLowPlayGetIn", "NothingToDo",
        "PlayAlone", "PlayWithHumans", "PutDownDispatch", "Selection", "Singing", "Socialize", "SparksBuildPyramid", "SparksCozmoSings", "SparksFistBump", "SparksGatherCubes",
        "SparksKnockOverCubes", "SparksPeekABoo", "SparksPickUpCube", "SparksPopAWheelie", "SparksPounceOnMotion", "SparksRollBlock", "SparksStackBlock", "SparksWorkout",
        "StrictPriorityFreeplay", "SparksTrackLaser", "SparksFireTruckAlarm",
    };

    private static string? ActivityIdOrNull(string? name) => name is not null && KnownActivityIds.Contains(name) ? name : null;

    /// <summary>
    /// <c>ActivityFreeplay::PickNewActivityForSpark(now, spark, askCurrent)</c> 0x005ADC44, the selection (the engine's <c>OnDeselected</c>/<c>OnSelected</c> and the pick log
    /// stay with the caller): the activities are bucketed by the spark they require (<c>CreateFromConfig</c> 0x005AD478 files each at <c>[activity+0x4C]</c> in the config's
    /// order, 0x005AD676..0x005AD69E), and the bucket for <paramref name="spark"/> is walked in that order (0x005ADC4E..0x005ADC68); a spark with no bucket warns and picks nothing
    /// (0x005ADCD8). When the debug-forced id (+0x91, <see cref="ForcedActivity"/>) or the desired-from-objects id (+0x90, <c>CalculateDesiredActivityFromObjects</c> 0x005ADF4C: the
    /// activity <c>desiredActivityNames</c> names for the faces and cubes known) is set, the only activity taken is the one with that id, +0x91 first (0x005ADC6A..0x005ADC7E): no
    /// strategy is asked. Otherwise an activity other than the current one is taken when its strategy <c>WantsToStart</c> (0x005ADC8C..0x005ADC9E). The engine's fourth argument (askCurrent) is not a
    /// parameter here: with it false the current activity is skipped (0x005ADCA2..0x005ADCA4), which is <paramref name="barred"/>; otherwise the current activity is retained if it does not want to end. The canonical desired-from-objects supplier remains unbound.
    /// </summary>
    // fidelity: M15-013
    public Activity? PickNewActivity(double nowSec, out string reason, Activity? barred = null)
    {
        var bucket = Freeplay.SubActivities.Where(a => a.Type != "Missing" && a.RequireSpark == Inputs.RequestedSpark).ToList();
        return PickFromBucket(nowSec, bucket, out reason, barred);
    }

    private Activity? PickNewActivityForId(double nowSec, int requested, out string reason, Activity? barred) =>
        PickFromBucket(nowSec, Freeplay.SubActivities.Where(a => a.Type != "Missing" && a.RequiredSparkId == requested).ToList(), out reason, barred);

    private Activity? PickFromBucket(double nowSec, List<Activity> bucket, out string reason, Activity? barred)
    {
        string? desiredId = _desiredActivity; // Canonical state6 or the explicit standalone test seam.
        if (bucket.Count == 0) { reason = $"no activities for the requested spark '{Inputs.RequestedSpark ?? "none"}'"; return null; }
        var notes = new List<string>();
        // +0x91 first, then +0x90 (0x005ADC6A..0x005ADC76); a name the ActivityID enum does not know is 0 and forces nothing (ActivityIDFromString 0x0076706C..0x007670D4)
        string? wanted = ActivityIdOrNull(ForcedActivity) ?? ActivityIdOrNull(desiredId);
        foreach (var a in bucket)
        {
            if (wanted is not null)
            {
                if (a.Id == wanted) { reason = (ForcedActivity is not null ? $"debug is forcing '{wanted}'" : "desired from faces and cubes") + "; " + string.Join(", ", notes); return a; }
                continue;
            }
            if (a == barred) { notes.Add($"{a.Id}: may not be repicked"); continue; }
            if (ReferenceEquals(a, Current))
            {
                if (!a.Strategy.WantsToEnd(Inputs, a.RunningSec(nowSec), out _, nowSec)) { reason = "current activity continues"; return a; }
                continue;
            }
            if (!a.Strategy.WantsToStart(Inputs, nowSec, out var why)) { notes.Add($"{a.Id}: {why}"); continue; }
            reason = $"priority {a.Priority}; " + why + "; " + string.Join(", ", notes);
            return a;
        }
        reason = wanted is not null ? $"the wanted activity '{wanted}' is not in the bucket for the requested spark" : string.Join("; ", notes);
        return null;
    }

    private FreeplayDecision Record(double nowSec, string? activity, string? behavior, string reason)
    {
        var d = new FreeplayDecision(nowSec, activity, behavior, reason);
        if (_decisions.Count == 0 || _decisions[^1] with { AtSec = nowSec } != d) { _decisions.Add(d); Decided?.Invoke(d); }
        return d;
    }

    /// <summary>
    /// Refreshes the inputs from the robot, the world model and the face world (the engine reads these through
    /// its components each tick).
    /// </summary>
    public void RefreshInputs(CozmoRobot robot, VisionSystem? vision, ManipulationSystem? m, double nowSec)
        =>RefreshInputs(robot,vision,m,nowSec,robot.Engine.Robot);
    internal void RefreshInputs(CozmoRobot robot, VisionSystem? vision, ManipulationSystem? m, double nowSec,EngineRobot? originalRobot)
    {
        // (the drive-off-charger stamp is written only by BehaviorDriveOffCharger::UpdateInternal 0x005C0E08; the former LOCAL stamp on the contacts flag falling had no engine counterpart)
        Inputs.OnCharger = robot.Sensors.OnCharger;
        if (vision is not null && originalRobot is { } native &&
            native.TryGetNativeOriginOwner(native.CurrentOriginId, out var origin) && origin is not null &&
            vision.Faces.NativePoseScope is not null)
        {
            using var mutation = vision.World.EnterPoseMutation();
            using var face = new Pose3d("");
            Inputs.FaceKnown = vision.Faces.CopyNativeLastFace(origin, true, face);
        }
        else ReportActivityMissing("MISSING: M14-001/M14-008 and M15-006 native face cache/current-origin provider unavailable");
        if (m is not null) { Inputs.LastOnTreadsEventSec = m.Whiteboard.OffTreadsStateChangedAtSec; _ctx.LastOnTreadsEventSec = Inputs.LastOnTreadsEventSec; }
        else ReportActivityMissing("MISSING: M15-006 shared whiteboard on-treads timestamp unavailable");
        if (m is not null) Inputs.PyramidBuilt = m.NativePyramids.Count > 0;
    }
}
