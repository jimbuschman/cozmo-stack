using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// Assembles the autonomy stack the way the engine's <c>BehaviorManager::InitConfiguration</c> does: every
/// implemented shipped behaviour bound by id (<c>BehaviorContainer</c>), the reaction registrations
/// (<c>InitReactionTriggerMap</c>), the needs manager, the activity tree from <c>activities_config.json</c>, and
/// the <see cref="FreeplaySystem"/> over the <see cref="BehaviorManager"/> with the Freeplay activity as the
/// high-level activity (<c>HighLevelActivity::Freeplay</c>).
/// </summary>
public sealed class FreeplayStack : IDisposable
{
    private readonly List<Action> _unsubscribe = new();

    private FreeplayStack(BehaviorManager manager, FreeplaySystem freeplay, IReadOnlyList<Activity> tree, IReadOnlyDictionary<string, IBehavior> bound, NeedsManager needs, BehaviorContext ctx, FreeplayDataTracker tracker, AIComponent ai)
    {
        Manager = manager; Freeplay = freeplay; Tree = tree; Bound = bound; Needs = needs; Context = ctx; DataTracker = tracker; AI = ai;
    }

    public BehaviorManager Manager { get; }
    public FreeplaySystem Freeplay { get; }
    /// <summary>The top-level activities (Selection, MeetCozmo, Feeding, Freeplay).</summary>
    public IReadOnlyList<Activity> Tree { get; }
    public IReadOnlyDictionary<string, IBehavior> Bound { get; }
    public NeedsManager Needs { get; }
    public BehaviorContext Context { get; }
    /// <summary><c>AIComponent</c>'s <c>FreeplayDataTracker</c> (M15-015), created and ticked on this stack's live path.</summary>
    public FreeplayDataTracker DataTracker { get; }
    /// <summary>The <c>AIComponent</c> hosting the <c>BehaviorHelperComponent</c> (M8-011), ticked by the engine's <c>Robot::Update</c> through <c>CozmoEngine.AIComponentUpdate</c>.</summary>
    public AIComponent AI { get; }
    public IReadOnlyList<string> Problems { get; private init; } = Array.Empty<string>();

    /// <summary>Every behaviour id the shipped activity tree names that no implemented behaviour answers to.</summary>
    public IReadOnlyList<string> UnboundIds => Tree.SelectMany(a => a.AllBehaviorIds()).Distinct().Where(id => !Bound.ContainsKey(id)).OrderBy(x => x).ToList();

    /// <summary>
    /// Builds the stack. <paramref name="vision"/> and <paramref name="m"/> enable the cube, navigation and
    /// face behaviours; without them the config-free and animation-only sets are bound. The reactions are
    /// registered when <paramref name="withReactions"/>.
    /// </summary>
    public static FreeplayStack Create(string obbRoot, CozmoRobot robot, BehaviorContext ctx, Func<double> clockSec, VisionSystem? vision = null, ManipulationSystem? m = null,
                                       NeedsManager? needs = null, bool withReactions = true, Random? random = null, string? needsDirectory = null)
    {
        var problems = new List<string>();
        // J3: the needs device-file directory is the host's; FreeplayTool's --state-dir supplies it.
        needs ??= NeedsManager.FromObb(obbRoot, clockSec, random, needsDirectory);
        // fidelity: M15-014
        // C2 row 10: StartReadFromRobot queues an NVStorage read of key 0x194000 on the connected robot's
        // NV component (robot.Engine.NvStorage, the same owner the camera's calibration read uses).
        needs.NvStorage = robot.Engine.NvStorage;
        // fidelity: M7-017
        // D07/D08/D18: AnimationStreamer::Update (0x0057CE5C) calls TrackLayerComponent::Update (0x0064EDE8) when
        // the last-stream seconds +0x88 > 0; that reads context+0x34 (this NeedsManager) -> +0x3D4 ->
        // GetCurrentDesiredDistortion (0x0063B760) and, when the degree is above 0x3727C5AC, tail-calls
        // AddGlitch. Wire the live path: the component's tick cache is the engine timer's tick count, its
        // samples use the context RNG, and the streamer's seam calls the manager. FromObb has already run the
        // component's Init (the config parse) before this.
        needs.TickCount = () => robot.Engine.Timer.TickCount;
        needs.DistortionRng ??= robot.Animations.Scheduler.ContextRandom;
        robot.Animations.Scheduler.DesiredFaceDistortion = needs.GetCurrentDesiredDistortion;
        var all = new List<IBehavior>();
        // fidelity: M7-018, M7-002
        // RobotDataLoader::LoadBehaviors 0x005206bc reads the config corpus and BehaviorContainer 0x0059c324 builds every behaviour whose class the
        // factory can build from its config (PlayAnim, FistBump, ReactToSparked); the rest is reported MISSING per class and built by hand below. The config-built objects come first: where the code-built set
        // below names the same id (Hiccup, the feeding PlayAnims) the first instance stands, so the config's is the one bound.
        var factoryContext = new BehaviorFactoryContext
        {
            Vision = vision,
            Manipulation = m,
            Log = line =>
            {
                robot.Engine.Log(line);
                if (line.StartsWith("warning:", StringComparison.Ordinal) || line.StartsWith("error:", StringComparison.Ordinal)) problems.Add(line);
            },
        };
        all.AddRange(BehaviorContainer.LoadShipped(obbRoot, factoryContext).Behaviors.Values);
        all.AddRange(ShippedBehaviors.Implementable());
        all.AddRange(ShippedBehaviors.Singing(obbRoot));
        if (m is not null)
        {
            if (m.Planner is null) m.LoadPlanner(obbRoot);
            m.Workouts ??= WorkoutComponent.FromObb(obbRoot);
            all.AddRange(ShippedBehaviors.Manipulation(m));
            all.AddRange(ShippedBehaviors.Navigation(m));
        }
        if (vision is not null)
        {
            all.AddRange(ShippedBehaviors.Faces(vision, m));
            all.AddRange(ExplorerBehaviors.LoadShipped(obbRoot, vision, m, needs, problems));
        }
        else
        {
            all.AddRange(new IBehavior[] { new WaitBehavior("Needs_Wait"), new EarnedSparksBehavior(needs, "EarnedSparks") });
        }
        // the config-free set and the shipped PlayAnim configs overlap (the feeding reactions): the first instance stands
        var bound = new Dictionary<string, IBehavior>(StringComparer.Ordinal);
        foreach (var b in all) bound.TryAdd(b.Id, b);
        // One clock for the whole stack. A stepped behaviour defaults to Environment.TickCount64 for its
        // own timing, and several of them stamp emotion events with Clock() / 1000 - which has to be the
        // same seconds the stack ticks on, or the mood's decay and its events are in different eras and
        // neither the decay nor the repetition penalty means anything. The engine has no such seam: both
        // sides read BaseStationTimer (MoodManager::GetCurrentTimeInSeconds 0x0067ADA8).
        foreach (var b in all.OfType<SteppedBehavior>()) b.Clock = () => clockSec() * 1000.0;

        // IBehavior::ReadFromScoredJson 0x005bc488 reads considerThisHasRunForBehaviorObjective from the
        // behaviour's own config into +0x10c; set each shipped behaviour's objective from it.
        var objectives = BehaviorObjectives.Load(obbRoot);
        foreach (var b in all.OfType<SteppedBehavior>())
            if (objectives.TryGetValue(b.Id, out var objective)) b.BehaviorObjective = objective;

        ctx.ClockSec ??= clockSec;
        // the behaviours report needs actions themselves (IBehavior::NeedActionCompleted 0x005BE40C), so the
        // context carries both the manager and every shipped behaviour's own needsActionID
        ctx.Needs ??= needs;
        // the memory map the face behaviour asks before driving in (MapComponent::GetCurrentMemoryMapHelper)
        if (vision is not null) ctx.Map ??= new MemoryMap();
        ctx.NeedsActionIds ??= BehaviorNeedsActions.Load(obbRoot);
        // fidelity: M7-012
        // MoodManager::Init 0x0067aebc: StaticMoodData::Init reads the shipped mood_config.json and the
        // emotion events; MoodState is the stack's runtime for it. Wired here so every Context.Mood?.Trigger
        // call and FreeplaySystem's per-tick Advance have a live model.
        ctx.Mood ??= new MoodState(MoodModel.Load(obbRoot));
        // The MoodManager's robot pointer (+0x12c): SendEmotionsToGame sends nothing without it (0x0067b736..0x0067b73c).
        if (ctx.Mood.Robot is null) ctx.Mood.AttachRobot(robot);
        // fidelity: M7-020
        // MoodManager::Init registers HandleActionEnded with the robot's ActionList whenever the robot is non-null (0x0067aee8..0x0067af38) and
        // ActionWatcher::Update invokes it for every completed action (0x0054187e..0x005418de). This stack has no ActionList or ActionWatcher, so the
        // callback cannot be registered and no action completion reaches MoodState.HandleActionEnded.
        SteppedBehavior.ReportMissing("MoodManager::Init 0x0067aee8..0x0067af38 registers HandleActionEnded with ActionList (robot+0x250) and ActionWatcher::Update 0x0054187e..0x005418de calls it for every completed RobotCompletedAction: this stack has no ActionList/ActionWatcher and its actions produce no completion record (tag, RobotActionType, 32-bit result), so no action completion reaches MoodState.HandleActionEnded");
        // BehaviorManager::FinishCurrentBehavior switches to the empty running info {none, none, NoneTrigger}
        // (0x005a38f4/0x005a38fe): 0x16 is the ReactionTrigger NoneTrigger, not a behaviour, so nothing is bound here.
        var manager = new BehaviorManager(ctx);
        if (m is not null) m.ReactionLocks = manager;       // M15-022: PlaceObjectOnGroundAction::Init takes its reaction lock on the robot's BehaviorManager for every caller; the newest stack's manager is the robot's
        if (withReactions)
            foreach (var reg in ShippedBehaviors.Reactions(robot, vision?.Locator, clockSec, vision,
                         bound.TryGetValue("RamIntoBlock", out var ram) ? ram as RamIntoBlockBehavior : null, m?.Whiteboard,
                         obbRoot, m))
                manager.AddReaction(reg.Strategy, reg.Behavior);

        // one repetition history for the whole stack: the manager records it, every chooser reads it
        var tree = ActivityTreeLoader.Load(obbRoot, bound, manager.Penalty, random);
        foreach (var s in tree.SelectMany(a => a.SubActivities.Prepend(a)).Select(a => a.Strategy)) s.NeedLevels ??= n => needs.State.GetNeedLevel(n);
        var freeplayActivity = tree.FirstOrDefault(a => a.Id == "Freeplay") ?? throw new InvalidOperationException("activities_config.json has no Freeplay activity");
        // config/features.json, which WantsToStart consults before anything else when an activity names a
        // featureGate (0x005B52A8).
        var inputs = new FreeplayInputs { Needs = needs, Mood = ctx.Mood, Features = FeatureGates.Load(obbRoot) };
        var system = new FreeplaySystem(manager, ctx, freeplayActivity, bound, inputs);
        // M15-015: the AIComponent's FreeplayDataTracker. Created here, ticked in Tick, flushed in Dispose.
        var tracker = new FreeplayDataTracker(clockSec);
        system.SparkPauseChanged = p => tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.Spark, p);
        // fidelity: M8-011
        // AIComponent::AIComponent 0x00569aa4..0x00569ab2 builds the BehaviorHelperComponent at +0x10; Robot::Update 0x00513eac runs AIComponent::Update from the engine tick
        // (the hook below), and IBehavior reaches the component through the context. Robot::GetWorldOriginID is the robot's current pose origin id (Robot::GetPose's VERIFY,
        // 0x004ea3a6..0x004ea3b2, compares the pose root's id with it): EngineRobot.CurrentOriginId.
        var ai = new AIComponent(() => (int)(robot.Engine.Robot?.CurrentOriginId ?? 0), robot.Engine.Log);
        ctx.AI = ai;
        var stack = new FreeplayStack(manager, system, tree, bound, needs, ctx, tracker, ai) { Problems = problems };
        robot.Engine.AIComponentUpdate = () => ai.Update(robot);
        if (m is not null) stack._unsubscribe.Add(() => { if (ReferenceEquals(m.ReactionLocks, manager)) m.ReactionLocks = null; });
        stack._unsubscribe.Add(() => robot.Engine.AIComponentUpdate = null);

        // fidelity: M1-024
        // CD6..CD11: CozmoEngine::Update state 3 calls NeedsManager::Update between UpdateRobotConnection ->
        // MessageHandler::ProcessMessages and UpdateAllRobots -> Robot::Update, with BaseStationTimer::
        // GetCurrentTimeInSeconds (0x004ED632..0x004ED640). The engine owns the tick and this stack owns the
        // NeedsManager, so the manager is handed over through the engine hook; FreeplaySystem.Tick no longer
        // calls it.
        robot.Engine.NeedsUpdate = needs.Update;
        stack._unsubscribe.Add(() => robot.Engine.NeedsUpdate = null);

        // fidelity: M15-016
        // J13: CozmoEngine::HandleMessage<ConnectToRobot> 0x004ED018..0x004ED11C calls
        // NeedsManager::InitAfterConnection 0x004ED10E unconditionally after AddRobot (the AddRobot-failed
        // and the success path both reach 0x004ED10A), then DASPauseUploadingToServer(1). The engine owns the
        // ConnectToRobot handling, so it exposes the edge; this stack, which owns the NeedsManager,
        // subscribes. It comes before the serial edge because the engine's order is ConnectToRobot then the
        // mfgId tag-0xED callback. A stack created after the handshake (Robot 1 already exists) replays the
        // edge once, as the serial edge does below.
        void onConnect() => needs.InitAfterConnection();
        robot.Engine.ConnectToRobotHandled += onConnect;
        stack._unsubscribe.Add(() => robot.Engine.ConnectToRobotHandled -= onConnect);
        if (robot.Engine.Robot is not null) onConnect();

        // fidelity: M15-014, M3-033
        // C2 rows 5-9: the mfgId tag-0xED callback calls ConnectRobotToNeedsManager(mfgId word 0), whose
        // wrapper chain ends at NeedsManager::InitAfterSerialNumberAcquired. The engine owns the handshake and
        // queues the Needs read 0x194000 from that handler; this stack, which owns the NeedsManager, attaches to
        // that read so InitAfterSerialNumberAcquired adopts it instead of queueing a second one. FreeplayTool.OnRobot
        // connects before it creates the stack, so a serial that is already known is replayed here at creation.
        // The RIC's tag-0xED subscription is persistent and the engine clears its recorded serial on removal, so
        // every mfgId (including a reconnect) runs the edge again.
        needs.AttachConnectionRead(robot.Engine);
        stack._unsubscribe.Add(needs.DetachConnectionRead);
        void onSerial(uint serial) => needs.InitAfterSerialNumberAcquired(serial);
        robot.Engine.SerialNumberAcquired += onSerial;
        stack._unsubscribe.Add(() => robot.Engine.SerialNumberAcquired -= onSerial);
        if (robot.Engine.AcquiredSerialNumber is { } already) onSerial(already);

        // The four pause sources. OffTreads (flag 2) is Robot::CheckAndUpdateTreadsState's seam
        // (0x005121F4); OnCharger (flag 3) is Robot::SetOnChargerPlatform (0x00511DB0). GameControl (flag 0)
        // belongs to BehaviorManager::SetCurrentActivity (0x005A106C) when the high-level activity is not
        // Freeplay. C1 §5: BehaviorManager::InitConfiguration (0x005A0DFC) also sets flag 0 paused, and only
        // SetCurrentActivity(1 Freeplay) clears it; this stack is created as Freeplay starts, so clearing it
        // here is the equivalent end state.
        robot.Sensors.OffTreads.SetFreeplayPauseFlagOffTreads = p => tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.OffTreads, p);
        stack._unsubscribe.Add(() => robot.Sensors.OffTreads.SetFreeplayPauseFlagOffTreads = null);
        void onCharger(bool on) => tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.OnCharger, on);
        robot.Sensors.OnChargerPlatformChanged += onCharger;
        stack._unsubscribe.Add(() => robot.Sensors.OnChargerPlatformChanged -= onCharger);
        // fidelity: M15-015
        // The engine's tracker exists from AIComponent construction, so its pause set already reflects the robot when the freeplay activity starts:
        // BehaviorManager::InitConfiguration pauses GameControl (0x005A0E48..0x005A0E56, inventory M15 row 37), the robot's own OffTreads and OnCharger
        // transitions (0x005121F4, 0x00511DB0) have set or cleared their flags as they happened, and SetCurrentActivity(Freeplay) (0x005A120A..0x005A1220)
        // clears GameControl. This stack is created after the robot's state may already be off treads or on the charger, so those two flags are seeded
        // from the robot's current state between the GameControl pause and its clear. Clearing GameControl last also stamps the resume time when the set
        // becomes empty (ClearFreeplayPauseFlag 0x0056EFF8), which the earlier never-paused GameControl left at zero.
        tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl, true);
        if (robot.Sensors.OffTreads.Current != OffTreadsState.OnTreads) tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.OffTreads, true);
        if (robot.Sensors.OnChargerPlatform) tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.OnCharger, true);
        tracker.SetFreeplayPauseFlag(FreeplayPauseFlag.GameControl, false);

        // fidelity: M10-011
        // RobotToEngineImplMessaging::HandleFallingStopped 0x00535040: intensity > 1000.0f calls NeedsManager::RegisterNeedsActionCompleted(NeedsActionId 17)
        // (0x005350AA..0x005350C2) before the DAS event and the FallingStopped broadcast (0x00535186..0x0053519C). The engine's NeedsManager is the stack's;
        // Sensors raises NeedsActionCompleted(17) at that point in the handler and this stack's manager takes it. The ordinal's name is the Unity
        // Anki.Cozmo.NeedsActionId[17] = Fall (the engine's enum-name pool at 0x00C1C690 lists ... DizzySoft, Fall, FistBump_Sparked ... in that order) and the
        // shipped needs_action_config.json row "Fall".
        void onNeedsAction(int ordinal)
        {
            if (ordinal == CozmoSensors.FallNeedsActionId) needs.RegisterNeedsActionCompleted("Fall");
        }
        robot.Sensors.NeedsActionCompleted += onNeedsAction;
        stack._unsubscribe.Add(() => robot.Sensors.NeedsActionCompleted -= onNeedsAction);

        // M15-016: the live removal path drives the NeedsManager's disconnect transition.
        // fidelity: M15-016
        // OnRobotDisconnected 0x00695908 is reached from RobotManager::RemoveRobot 0x0052F2DC..0x0052F2E0 in
        // both branches - whether or not the connection manager answered the disconnect - not only from the
        // RobotDisconnected game broadcast (CozmoEngine.cs raises that broadcast only when it was not
        // answered). CozmoRobot.RobotRemoved is raised by ResetDevices from CozmoEngine.RemoveRobot on every
        // removal (CozmoEngine.cs:1320), so it is the stack's always-fired removal edge. It fires after the
        // device reset, which is after the RobotDisconnected broadcast the engine sends before deleting the
        // Robot, matching the engine's order (broadcast, then RemoveRobot's OnRobotDisconnected).
        void onRemoved() => needs.OnRobotDisconnected();
        robot.RobotRemoved += onRemoved;
        stack._unsubscribe.Add(() => robot.RobotRemoved -= onRemoved);
        // M15-016: the ConnectToRobot -> InitAfterConnection edge is now wired above (J13): CozmoEngine
        // exposes ConnectToRobotHandled and this stack subscribes, with a replay when Robot 1 already exists
        // at creation. What remains unbuilt, so the record stays IMPLEMENTATION_GAP: the SetPaused
        // game-message callers (SetGameBeingPaused tag 85, SetNeedsPauseState tag 201, RegisterOnboardingComplete
        // tag 200, EnterSdkMode/ExitSdkMode tags 241/242) - this stack has no game-message channel for them;
        // the +0x1F0 per-need pause-start consumer (HandleMessage<SetNeedsPauseStates> 0x00698918) and the
        // +0x214 DAS-elapsed consumer (the app-facing DAS wire, which the host sees through BracketChanged).

        // ActivityFreeplay::HandleMessage<RobotOffTreadsStateChanged>: being put back down kicks the activity
        // out and re-picks from what is around. That is part of what this stack assembles, so it is wired here
        // rather than left to whichever tool happens to remember it.
        void onTreads(OffTreadsState from, OffTreadsState to)
        {
            if (to == OffTreadsState.OnTreads && from != OffTreadsState.OnTreads) system.OnRobotPutDown(clockSec());
        }
        robot.Sensors.OffTreadsStateChanged += onTreads;
        stack._unsubscribe.Add(() => robot.Sensors.OffTreadsStateChanged -= onTreads);

        // The ground in front of the robot reaches the map here, which is the join the engine makes in
        // VisionComponent::UpdateOverheadEdges 0x006553FC -> MapComponent::ProcessVisionOverheadEdges
        // 0x0067F7AC. The detector runs inside the vision system on each frame's own pose data, and the
        // frame is put in the map against the robot pose of that same frame - the engine looks that pose
        // up by the frame's timestamp, and here it arrives with the frame.
        if (vision is not null && ctx.Map is { } theMap)
        {
            vision.OverheadEdges ??= new OverheadEdgesDetector();
            void onFrame(VisionFrameResult r)
            {
                if (r.OverheadEdges is { } edges) theMap.AddVisionOverheadEdges(edges, r.PoseData.RobotPose);
            }
            vision.FrameProcessed += onFrame;
            stack._unsubscribe.Add(() => vision.FrameProcessed -= onFrame);

            // A delocalization puts the robot in a new origin, and the engine gives that origin a map of
            // its own (MapComponent::CreateLocalizedMemoryMap, called from BlockWorld::OnRobotDelocalized
            // 0x006249D6). Everything in the old map is in a frame that has gone, so it starts empty.
            void onDelocalized(uint origin) => theMap.Clear();
            vision.RobotDelocalized += onDelocalized;
            stack._unsubscribe.Add(() => vision.RobotDelocalized -= onDelocalized);
        }
        return stack;
    }

    /// <summary>One tick with the inputs refreshed from the robot and the world.</summary>
    public FreeplayDecision Tick(double nowSec, double nowMs, CozmoRobot robot, VisionSystem? vision, ManipulationSystem? m)
    {
        // the object content of the memory map follows the world model, as MapComponent's
        // AddObservableObject / RemoveObservableObject keep it following BlockWorld
        if (Context.Map is { } map)
        {
            if (vision?.World is { } world) map.SyncFromWorld(world, robot.State.Latest?.Timestamp ?? 0);
            // MapComponent::UpdateRobotPose: the ground the robot has been over, once it has moved far enough
            if (m?.RobotPose() is { } here)
                map.UpdateRobotPose(here, robot.Sensors.CliffDetectedNow, robot.State.Latest?.Timestamp ?? 0);
        }
        Freeplay.RefreshInputs(robot, vision, m, nowSec);
        DataTracker.Update();   // reads its own clock (0x0056EC1E..0x0056EC22)
        return Freeplay.Tick(nowSec, nowMs);
    }

    public void Dispose()
    {
        foreach (var off in _unsubscribe) { try { off(); } catch { } }
        _unsubscribe.Clear();
        DataTracker.ForceUpdate();      // the BehaviorSystemManager destructor's ForceUpdate (0x005110D4)
        Manager.Dispose();
    }
}
