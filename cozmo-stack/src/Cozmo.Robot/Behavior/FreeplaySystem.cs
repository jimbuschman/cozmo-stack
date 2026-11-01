using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>One freeplay decision, for the log and the tests.</summary>
public sealed record FreeplayDecision(double AtSec, string? Activity, string? Behavior, string Reason);

/// <summary>
/// The standalone host's bounded ActivityFreeplay chooser (0x005AE29C), called by BehaviorManager after reactions.
/// A kept activity may cause one re-pick; the newly picked activity is asked once. Putdown immediately deselects
/// eligible activities but does not calculate the object-derived desired cache. The canonical parent admission,
/// Look state-6 cache supplier, filtered object count, and raw Spark-to-activity join remain missing.
/// </summary>
// fidelity: M15-002
// fidelity: M15-006
public sealed class FreeplaySystem : IManagedActivity
{
    private readonly BehaviorManager _manager;
    private readonly Action<BehaviorManager.ReactionSwitch> _reactionObserver;
    private readonly BehaviorContext _ctx;
    private readonly IReadOnlyDictionary<string, IBehavior> _bound;
    private bool _requestPending;
    private string? _desiredActivity;
    // Explicit test seam for the recovered cache byte. The canonical Look state-6 producer remains unbound.
    internal void SetDesiredActivityCacheForTest(string? id) => _desiredActivity = ActivityIdOrNull(id);
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

    /// <summary>The robot was put back on its treads: the activity is kicked out and re-picked from what is around.</summary>
    public void OnRobotPutDown(double nowSec)
    {
        if (Current is not { } activity || activity.Id is "NeedsSevereLowEnergy" or "NeedsSevereLowRepair" ||
            activity.Id == ActivityIdOrNull(ForcedActivity) || activity.RequireSpark is not null) return;
        Log?.Invoke($"ActivityFreeplay.RobotOffTreadsStateChanged.KickingOutActivityOnPutDown: Kicking out '{activity.Id}' on put down so we pick up a new one");
        EndActivity(nowSec);
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
    /// The native ActivateSpark input now reaches BehaviorManager's separate checked fields;
    /// its raw-ID picker/specialized activity join is still MISSING and does not call this string seam.
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
        ReportActivityMissing("MISSING: M15-006 filtered object count and Look state-6 desired supplier are unbound");
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
            bool retry = decision.Behavior is null;
            if (retry) { Log?.Invoke("ActivityFreeplay.ChooseNextBehavior.NoBehaviorChosenWhileRunning: activity is not allowed to be repicked"); barred = Current; } // askCurrent=false
            if (!retry && !_manager.IsRunning(decision.Behavior!) && !(Inputs.Needs?.SparksRewardPending ?? false))
            {
                retry = Current!.Strategy.WantsToEnd(Inputs, Current.RunningSec(nowSec), out _, nowSec);
                if (retry) Log?.Invoke($"ActivityFreeplay.ChooseNextBehavior.NewBehaviorChosenWhileRunning: '{Current.Id}' wants to end");
            }
            if (retry) pick = true; // non-running candidate: askCurrent=true
        }
        else decision = new ChooserDecision(null, "no chooser", Array.Empty<(string, double, string)>());

        if (pick)
        {
            if (_manager.RequestedSparkId != 0x55 || Inputs.RequestedSpark is not null)
            {
                ReportActivityMissing("MISSING: M15-006 raw requested Spark ID to activity bucket join; retaining current selection");
                return current;
            }
            if (_manager.SparkCancelLatch != 0 && _manager.ActiveSparkId == _manager.RequestedSparkId) barred = Current;
            // The absent bucket returns unchanged (0x005ADCD8..CF6); the outer initial/retry path still applies.
            bool hasBucket = Freeplay.SubActivities.Any(a => a.Type != "Missing" && a.RequireSpark is null);
            string pickReason;
            Activity? picked;
            if (hasBucket) picked = PickNewActivity(nowSec, out pickReason, barred);
            else { picked = Current; pickReason = "no activities for requested spark"; Log?.Invoke("ActivityFreeplay.PickNewActivityForSpark: " + pickReason); }
            _desiredActivity = null;
            bool changed = !ReferenceEquals(picked, Current);
            if (changed)
            {
                EndActivity(nowSec);
                Current = picked;
                if (picked is not null)
                {
                    picked.OnSelected(nowSec, ReportActivityMissing);
                    _ctx.LastActivitySwitchSec = nowSec;
                    Log?.Invoke($"robot.freeplay_goal_started {picked.Id}: {pickReason}");
                }
            }
            if (changed)
            {
                _manager.SwitchToNativeRequestedSpark();
                _requestedSparkInvalid = false;
                SparkPauseChanged?.Invoke(_manager.RequestedSparkId != 0x55);
            }
            if (Current is null)
            {
                Record(nowSec, null, null, $"ActivityFreeplay.NoActivitySelected: Picked no activity ({pickReason})");
                _recordedThisTick = true;
                return null;
            }
            if (initialPick || changed) decision = ChooseActivityBehavior(Current, current, nowSec);
        }

        var desired = decision.Behavior;
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
        if (activity.CachedInterlude is not null && ReferenceEquals(activity.CachedInterlude, current))
            return new ChooserDecision(activity.CachedInterlude, "cached interlude", Array.Empty<(string, double, string)>());
        activity.CachedInterlude = null;
        var decision = activity.Chooser?.GetDesiredActiveBehavior(current, _manager.RunningDurationSec(nowSec), _ctx, nowSec)
            ?? new ChooserDecision(null, "no chooser", Array.Empty<(string, double, string)>());
        if (activity.InterludeChooser is not null && !ReferenceEquals(decision.Behavior, current))
        {
            activity.CachedInterlude = activity.InterludeChooser.GetDesiredActiveBehavior(decision.Behavior, 0, _ctx, nowSec).Behavior;
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
        double ranSec = Current.RunningSec(nowSec);
        Current.OnDeselected(nowSec, ReportActivityMissing, Inputs.Needs);
        // No needs action is reported here. An activity's needsActionID is stored at IActivity+0x1C
        // (ReadConfig, 0x005B2A9C) and never read again anywhere in the engine; the behaviours report their
        // own (IBehavior::NeedActionCompleted 0x005BE40C, see BehaviorNeedsActions). An earlier version of
        // this stack reported the activity's id when the activity ended, which the engine never does.
        Log?.Invoke($"robot.freeplay_goal_ended {Current.Id}: ran {(int)ranSec} s");
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
        string? desiredId = _desiredActivity; // The canonical state-6 supplier is not yet bound.
        var bucket = Freeplay.SubActivities.Where(a => a.Type != "Missing" && a.RequireSpark == Inputs.RequestedSpark).ToList();
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
    {
        // (the drive-off-charger stamp is written only by BehaviorDriveOffCharger::UpdateInternal 0x005C0E08; the former LOCAL stamp on the contacts flag falling had no engine counterpart)
        Inputs.OnCharger = robot.Sensors.OnCharger;
        if (vision is not null && robot.Engine.Robot is { } native &&
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
