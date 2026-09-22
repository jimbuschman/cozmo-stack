using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>One freeplay decision, for the log and the tests.</summary>
public sealed record FreeplayDecision(double AtSec, string? Activity, string? Behavior, string Reason);

/// <summary>
/// The engine's freeplay decision layer, as <c>BehaviorManager::Update</c> (0x005A2F60) runs it: reactions first
/// (<c>CheckReactionTriggerStrategies</c>), then the current activity's <c>GetDesiredActiveBehavior</c>
/// (<c>ChooseNextScoredBehaviorAndSwitch</c> 0x005A2A20 → <c>SwitchToBehaviorBase</c>), then the behaviour's own
/// <c>Update</c>. The activity comes from <c>ActivityFreeplay</c> (0x005AE2xx, "ActivityFreeplay.ChooseNextBehavior"):
/// the current one stays until its strategy wants to end and its behaviour has finished ("Picking new activity
/// because '%s' wants to end, and behavior finished"), a spark is requested, or the robot was put down
/// (<c>RobotOffTreadsStateChanged</c>: "Kicking out '%s' on put down so we pick up a new one", then
/// <c>CalculateDesiredActivityFromObjects</c> picks the <c>desiredActivityNames</c> entry for faces and cubes:
/// "robot.goal_from_face_and_cube"); a new activity is the first sub-activity, in priority order, whose strategy
/// wants to start and whose chooser yields a behaviour ("The new activity '%s' picked no behavior"); after a
/// put-down the desired-from-objects activity is tried first (the config calls <c>desiredActivityNames</c>
/// "parameters to decide between activities on put down"); "There was no activity, and no activity was selected" is an
/// error the engine logs and this reports. An activity's <c>needsActionID</c> is <b>not</b> reported when it
/// ends - it is read into <c>IActivity+0x1C</c> and never looked at again; the behaviours report their own
/// (<see cref="BehaviorNeedsActions"/>).
///
/// <c>GetDesiredActiveBehaviorInternal</c> 0x005AE29C decides twice. Before asking, in order: a debug-forced
/// activity (+0x91) that is not the current one, the requested activity (+0x90) that is not the current one, a
/// spark change or no current activity at all, then - only when no behaviour is running and no sparks reward is
/// waiting - the strategy's <c>WantsToEnd</c>. Then it asks the activity
/// (<c>IActivity::GetDesiredActiveBehavior</c> 0x005B387C) and decides again on what came back
/// (0x005AE68C..0x005AE704): a null pick ends the activity and bars it from the re-pick
/// ("NoBehaviorChosenWhileRunning: ... This activity is not allowed to be repicked", the third argument of
/// <c>PickNewActivityForSpark</c> going false at 0x005AE75C), and a pick that is <b>not</b> the behaviour
/// already running (<c>IBehavior+0xa1</c>, the running flag <c>Init</c> sets and <c>Stop</c> clears) ends it
/// too when the strategy wants to end ("NewBehaviorChosenWhileRunning"), unless the needs manager has a sparks
/// reward still to communicate (+0x3d8). Both loop back to the activity pick, which is why the tick's middle is
/// a loop.
/// </summary>
public sealed class FreeplaySystem
{
    private readonly BehaviorManager _manager;
    private readonly BehaviorContext _ctx;
    private readonly IReadOnlyDictionary<string, IBehavior> _bound;
    private IBehavior? _pendingInterlude;
    private string? _lastBehaviorId, _interludeAfter;
    private bool _putDownPending, _pickDesiredFirst = true, _requestPending;

    public FreeplaySystem(BehaviorManager manager, BehaviorContext ctx, Activity freeplay, IReadOnlyDictionary<string, IBehavior> bound, FreeplayInputs inputs)
    {
        _manager = manager; _ctx = ctx; Freeplay = freeplay; _bound = bound; Inputs = inputs;
        foreach (var b in bound.Values) if (manager.Find(b.Id) is null) manager.Add(b);
    }

    public Activity Freeplay { get; }
    public FreeplayInputs Inputs { get; }
    public Activity? Current { get; private set; }
    public IReadOnlyList<FreeplayDecision> Decisions => _decisions;
    private readonly List<FreeplayDecision> _decisions = new();
    public event Action<FreeplayDecision>? Decided;
    public event Action<string>? Log;
    /// <summary>Debug / test: force the next pick to this activity id (the engine's console var "debug is forcing '%s'").</summary>
    public string? ForcedActivity { get; set; }

    /// <summary>The robot was put back on its treads: the activity is kicked out and re-picked from what is around.</summary>
    public void OnRobotPutDown(double nowSec)
    {
        Inputs.LastOnTreadsEventSec = nowSec; _ctx.LastOnTreadsEventSec = nowSec;
        if (Current is not null) Log?.Invoke($"ActivityFreeplay.RobotOffTreadsStateChanged.KickingOutActivityOnPutDown: Kicking out '{Current.Id}' on put down so we pick up a new one");
        _putDownPending = true;
    }

    /// <summary>
    /// The engine's requested-activity path (<c>GetDesiredActiveBehaviorInternal</c> 0x005AE2F4: the byte at +0x90,
    /// "Picking new activity because '%s' was requested"): the current activity is kicked out on the next tick and a
    /// new one picked. The engine sets it when the high-level activity comes back to Freeplay (the app's Feeding,
    /// MeetCozmo and Selection activities run outside freeplay; a feed happens there, not inside a freeplay activity).
    /// </summary>
    public void RequestNewActivity() => _requestPending = true;

    /// <summary><c>CalculateDesiredActivityFromObjects</c>: the configured activity for the faces and cubes known.</summary>
    public string? DesiredActivityFromObjects()
    {
        if (Freeplay.DesiredActivityNames is not { } names) return null;
        bool face = Inputs.FaceKnown, cube = Inputs.LocatedCubes > 0;
        var pick = face && cube ? names.FaceAndCube : face ? names.FaceOnly : cube ? names.CubeOnly : names.None;
        Log?.Invoke($"robot.goal_from_face_and_cube {(face ? 1 : 0)}:{(cube ? 1 : 0)} -> {pick}");
        return pick;
    }

    /// <summary>One tick: reactions, activity selection, behaviour selection, behaviour update.</summary>
    public FreeplayDecision Tick(double nowSec, double nowMs)
    {
        // Mood first, and on this tick's clock. The engine updates it from Robot::Update (0x00513E8A)
        // every tick, whatever the behaviour system then decides, and MoodManager reads its time from
        // BaseStationTimer - the same clock the emotion events are stamped with
        // (GetCurrentTimeInSeconds 0x0067ADA8). Without this the decay only ran when something else
        // happened to advance it, so an emotion stayed wherever an event left it and everything that
        // reads mood - the scoring, the gating - read a value that should long since have decayed.
        _ctx.Mood?.Advance(nowSec);
        Inputs.Needs?.Update();
        var reaction = _manager.CheckReactions(nowSec);
        if (reaction is not null) { Record(nowSec, Current?.Id, reaction.Behavior, $"reaction {reaction.Trigger}"); _manager.Update(nowMs, nowSec); return _decisions[^1]; }
        if (_manager.CurrentReactionTrigger is not null) { _manager.Update(nowMs, nowSec); return Record(nowSec, Current?.Id, _manager.Current?.Id, "a reaction is running"); }

        var current = _manager.Current;
        // the activity: keep, end, or pick
        if (Current is not null)
        {
            bool wantsEnd = Current.Strategy.WantsToEnd(Inputs, Current.RunningSec(nowSec), out var endReason);
            bool behaviorFinished = current is null;
            if (_putDownPending || _requestPending || (wantsEnd && behaviorFinished) || (Inputs.RequestedSpark is not null && Current.RequireSpark != Inputs.RequestedSpark) || (ForcedActivity is not null && ForcedActivity != Current.Id))
            {
                var why = _putDownPending ? "put down" : _requestPending ? $"'{Current.Id}' was requested" : ForcedActivity is not null ? $"debug is forcing '{ForcedActivity}'" : Inputs.RequestedSpark is not null ? $"to match spark '{Inputs.RequestedSpark}'" : $"'{Current.Id}' wants to end ({endReason}), and behavior finished";
                Log?.Invoke($"ActivityFreeplay.ChooseNextBehavior: Picking new activity because {why}");
                EndActivity(nowSec);
            }
        }
        if (_putDownPending) _pickDesiredFirst = true;
        _putDownPending = false; _requestPending = false;

        // Pick an activity, ask it for a behaviour, and decide again on the answer - the engine's second gate
        // (0x005AE68C..0x005AE704), which sends both of its outcomes back to the activity pick. The bound is
        // the tree: each pass either settles or ends an activity.
        ChooserDecision decision;
        IBehavior? desired;
        Activity? barred = null;
        for (int pass = 0; ; pass++)
        {
            if (Current is null)
            {
                var picked = PickNewActivity(nowSec, out var pickReason, barred);
                if (picked is null) return Record(nowSec, null, null, $"ActivityFreeplay.NoActivitySelected: Picked no activity ({pickReason})");
                Current = picked; Current.OnSelected(nowSec); _ctx.LastActivitySwitchSec = nowSec;
                Log?.Invoke($"robot.freeplay_goal_started {Current.Id}: {pickReason}");
                // EndActivity stopped whatever was running, so the local snapshot taken above is stale. Handing it
                // to the new activity's chooser would present a stopped behaviour as running, and a behaviour id
                // that both activities name would be treated as "already running" while the manager has nothing.
                current = _manager.Current;
            }

            // the behaviour the activity wants
            decision = Current.Chooser?.GetDesiredActiveBehavior(current, _manager.RunningDurationSec(nowSec), _ctx, nowSec) ?? new ChooserDecision(null, "no chooser", Array.Empty<(string, double, string)>());
            desired = decision.Behavior;
            if (pass >= Freeplay.SubActivities.Count) break;                       // the tree is finite; settle

            if (desired is null)
            {
                Log?.Invoke($"ActivityFreeplay.ChooseNextBehavior.NoBehaviorChosenWhileRunning: Picking new activity because '{Current.Id}' chose behavior 'NULL'. This activity is not allowed to be repicked.");
                if (current is not null && !current.IsRunnable(_ctx) && Current.Chooser is ScoringChooser ran) ran.Ran(current.Id, nowSec);
                barred = Current;
                EndActivity(nowSec);
                current = _manager.Current;
                continue;
            }
            // the activity that chose a behaviour it is not already running ends instead, if it wants to end
            bool rewardPending = Inputs.Needs?.SparksRewardPending ?? false;
            if (!rewardPending && (current is null || current.Id != desired.Id)
                && Current.Strategy.WantsToEnd(Inputs, Current.RunningSec(nowSec), out var switchEndReason))
            {
                Log?.Invoke($"ActivityFreeplay.ChooseNextBehavior.NewBehaviorChosenWhileRunning: Picking new activity because '{Current.Id}' wants to end ({switchEndReason}), and behavior finished");
                // PickNewActivityForSpark only re-picks the activity that is still running when its strategy
                // does *not* want to end (0x005ADCA2..0x005ADCB0). EndActivity has already let go of it here,
                // so that exclusion has to be said out loud.
                barred = Current;
                EndActivity(nowSec);
                current = _manager.Current;
                continue;
            }
            break;
        }
        if (desired is not null && (current is null || current.Id != desired.Id))
        {
            // IActivity::ChooseInterludeBehavior: between two different behaviours the interlude chooser gets a turn, once
            string? previous = current?.Id ?? _lastBehaviorId;
            if (Current.InterludeChooser is not null && _pendingInterlude is null && previous is not null && previous != desired.Id && _interludeAfter != previous)
            {
                var interlude = Current.InterludeChooser.GetDesiredActiveBehavior(null, 0, _ctx, nowSec).Behavior;
                if (interlude is not null && interlude.Id != desired.Id && interlude.Id != previous)
                {
                    Log?.Invoke($"IActivity.ChooseInterludeBehavior: Activity {Current.Id} is inserting interlude {interlude.Id} between behaviors {previous} and {desired.Id}");
                    _pendingInterlude = desired; _interludeAfter = previous; desired = interlude;
                }
            }
            if (_pendingInterlude is not null && desired.Id == _pendingInterlude.Id) _pendingInterlude = null;
            bool started = _manager.StartAsync(desired.Id, nowSec).GetAwaiter().GetResult();
            // The interrupted behaviour is NOT recorded as having run: the engine keeps
            // StopWithoutImmediateRepetitionPenalty for exactly this case, and the manager records the
            // repetition only for a behaviour that reached Completed.
            if (_pendingInterlude is null) _lastBehaviorId = desired.Id;
            Record(nowSec, Current.Id, started ? desired.Id : null, started ? decision.Reason : $"{desired.Id} refused to start");
        }
        else if (desired is null && current is not null)
        {
            // Only the last pass of the loop above reaches here with nothing chosen: every earlier one ended the
            // activity and picked again. ChooseNextScoredBehaviorAndSwitch (0x005A2A74) switches whenever the
            // chooser's pick differs from the running behaviour, a null pick included, so the behaviour stops.
            Log?.Invoke($"BehaviorManager.ChooseNextScoredBehaviorAndSwitch: '{current.Id}' is no longer runnable and the chooser picked nothing; stopping it");
            _manager.Stop(BehaviorStopReason.Interrupted, nowSec);
            if (Current.Chooser is ScoringChooser sc3) sc3.Ran(current.Id, nowSec);
            Record(nowSec, Current.Id, null, $"{current.Id} no longer runnable");
        }
        else Record(nowSec, Current.Id, current?.Id, current is null ? decision.Reason : "keeps running");

        var before = _manager.Current;
        _manager.Update(nowMs, nowSec);
        if (before is not null && _manager.Current is null)
            // the manager has already recorded the completion in the shared repetition history
            Log?.Invoke($"BehaviorManager.Update.BehaviorComplete: Behavior '{before.Id}' returned Status::Complete");
        return _decisions[^1];
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
        if (_manager.Current is not null) _manager.Stop(BehaviorStopReason.Interrupted, nowSec);
        double ranSec = Current.RunningSec(nowSec);
        Current.OnDeselected(nowSec);
        if (Inputs.Needs is { SparksRewardPending: true } needs)
        {
            Log?.Invoke($"IActivity.Exit.SparksRewardCommunicated {Current.Id}");
            needs.SparksRewardCommunicatedToUser();
        }
        // No needs action is reported here. An activity's needsActionID is stored at IActivity+0x1C
        // (ReadConfig, 0x005B2A9C) and never read again anywhere in the engine; the behaviours report their
        // own (IBehavior::NeedActionCompleted 0x005BE40C, see BehaviorNeedsActions). An earlier version of
        // this stack reported the activity's id when the activity ended, which the engine never does.
        Log?.Invoke($"robot.freeplay_goal_ended {Current.Id}: ran {(int)ranSec} s");
        Current = null; _pendingInterlude = null;
    }

    /// <summary>
    /// <c>ActivityFreeplay::PickNewActivity</c>: priority order. On the first pick and after a put-down the
    /// desired-from-objects activity is tried ahead of the freeplay chain (<c>CalculateDesiredActivityFromObjects</c>
    /// runs from the off-treads handler and on start; the config calls its names "parameters to decide between
    /// activities on put down"); the sparks and the needs activities keep their priorities ahead of it
    /// (INFERRED: the exact interleaving was not traced).
    /// </summary>
    public Activity? PickNewActivity(double nowSec, out string reason, Activity? barred = null)
    {
        var order = Freeplay.SubActivities.OrderBy(a => a.Priority).ToList();
        if (ForcedActivity is { } forced && order.FirstOrDefault(a => a.Id == forced) is { } f) { reason = $"debug is forcing '{forced}'"; return f; }
        var desiredId = _pickDesiredFirst ? DesiredActivityFromObjects() : null;
        _pickDesiredFirst = false;
        var candidates = new List<Activity>();
        if (desiredId is not null && order.FirstOrDefault(a => a.Id == desiredId) is { } d)
        {
            static bool KeepsPriority(Activity a) => a.RequireSpark is not null || a.Strategy.Type is "Needs" or "SevereNeedTransition" or "Spark";
            candidates.AddRange(order.Where(a => a != d && KeepsPriority(a)));
            candidates.Add(d);
        }
        candidates.AddRange(order.Where(a => !candidates.Contains(a)));
        var notes = new List<string>();
        foreach (var a in candidates)
        {
            if (a == barred) { notes.Add($"{a.Id}: chose no behaviour and may not be repicked"); continue; }
            if (a.Type == "Missing") { notes.Add($"{a.Id}: config missing"); continue; }
            if (a.RequireSpark is not null && a.RequireSpark != Inputs.RequestedSpark) { notes.Add($"{a.Id}: needs spark {a.RequireSpark}"); continue; }
            if (!a.Strategy.WantsToStart(Inputs, nowSec, out var why)) { notes.Add($"{a.Id}: {why}"); continue; }
            if (a.Chooser is null) { notes.Add($"{a.Id}: no chooser"); continue; }
            var pick = a.Chooser.GetDesiredActiveBehavior(null, 0, _ctx, nowSec);
            if (pick.Behavior is null) { notes.Add($"{a.Id}: picked no behavior ({pick.Reason})"); continue; }
            reason = (a.Id == desiredId ? "desired from faces and cubes; " : $"priority {a.Priority}; ") + why + "; " + string.Join(", ", notes);
            return a;
        }
        reason = string.Join("; ", notes);
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
        // the drive-off-charger stamp for requiredRecentDriveOffCharger_sec: the IS_ON_CHARGER flag falling (LOCAL source)
        if (Inputs.OnCharger && !robot.Sensors.OnCharger) _ctx.LastDriveOffChargerSec = nowSec;
        Inputs.OnCharger = robot.Sensors.OnCharger;
        if (vision is not null)
        {
            Inputs.LocatedCubes = vision.World.LocatedObjects.Count(o => CubeGeometry.IsCube(o.Type));
            Inputs.FaceKnown = vision.Faces.HasAnyFaces();
        }
        if (m is not null) Inputs.PyramidBuilt = m.Configurations.Pyramids.Count > 0;
    }
}
