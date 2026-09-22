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

    private FreeplayStack(BehaviorManager manager, FreeplaySystem freeplay, IReadOnlyList<Activity> tree, IReadOnlyDictionary<string, IBehavior> bound, NeedsManager needs, BehaviorContext ctx)
    {
        Manager = manager; Freeplay = freeplay; Tree = tree; Bound = bound; Needs = needs; Context = ctx;
    }

    public BehaviorManager Manager { get; }
    public FreeplaySystem Freeplay { get; }
    /// <summary>The top-level activities (Selection, MeetCozmo, Feeding, Freeplay).</summary>
    public IReadOnlyList<Activity> Tree { get; }
    public IReadOnlyDictionary<string, IBehavior> Bound { get; }
    public NeedsManager Needs { get; }
    public BehaviorContext Context { get; }
    public IReadOnlyList<string> Problems { get; private init; } = Array.Empty<string>();

    /// <summary>Every behaviour id the shipped activity tree names that no implemented behaviour answers to.</summary>
    public IReadOnlyList<string> UnboundIds => Tree.SelectMany(a => a.AllBehaviorIds()).Distinct().Where(id => !Bound.ContainsKey(id)).OrderBy(x => x).ToList();

    /// <summary>
    /// Builds the stack. <paramref name="vision"/> and <paramref name="m"/> enable the cube, navigation and
    /// face behaviours; without them the config-free and animation-only sets are bound. The reactions are
    /// registered when <paramref name="withReactions"/>.
    /// </summary>
    public static FreeplayStack Create(string obbRoot, CozmoRobot robot, BehaviorContext ctx, Func<double> clockSec, VisionSystem? vision = null, ManipulationSystem? m = null,
                                       NeedsManager? needs = null, bool withReactions = true, Random? random = null)
    {
        var problems = new List<string>();
        needs ??= NeedsManager.FromObb(obbRoot, clockSec, random);
        var all = new List<IBehavior>();
        all.AddRange(ShippedBehaviors.Implementable());
        all.AddRange(ShippedBehaviors.PlayAnims(obbRoot, problems));
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

        ctx.ClockSec ??= clockSec;
        // the behaviours report needs actions themselves (IBehavior::NeedActionCompleted 0x005BE40C), so the
        // context carries both the manager and every shipped behaviour's own needsActionID
        ctx.Needs ??= needs;
        // the memory map the face behaviour asks before driving in (MapComponent::GetCurrentMemoryMapHelper)
        if (vision is not null) ctx.Map ??= new MemoryMap();
        ctx.NeedsActionIds ??= BehaviorNeedsActions.Load(obbRoot);
        var manager = new BehaviorManager(ctx);
        if (withReactions)
            foreach (var reg in ShippedBehaviors.Reactions(robot, vision?.Locator, clockSec, vision)) manager.AddReaction(reg.Strategy, reg.Behavior, reg.ResumeLast);

        // one repetition history for the whole stack: the manager records it, every chooser reads it
        var tree = ActivityTreeLoader.Load(obbRoot, bound, manager.Penalty, random);
        foreach (var s in tree.SelectMany(a => a.SubActivities.Prepend(a)).Select(a => a.Strategy)) s.NeedLevels ??= n => needs.State.GetNeedLevel(n);
        var freeplayActivity = tree.FirstOrDefault(a => a.Id == "Freeplay") ?? throw new InvalidOperationException("activities_config.json has no Freeplay activity");
        // config/features.json, which WantsToStart consults before anything else when an activity names a
        // featureGate (0x005B52A8).
        var inputs = new FreeplayInputs { Needs = needs, Mood = ctx.Mood, Features = FeatureGates.Load(obbRoot) };
        var system = new FreeplaySystem(manager, ctx, freeplayActivity, bound, inputs);
        var stack = new FreeplayStack(manager, system, tree, bound, needs, ctx) { Problems = problems };

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
        return Freeplay.Tick(nowSec, nowMs);
    }

    public void Dispose()
    {
        foreach (var off in _unsubscribe) { try { off(); } catch { } }
        _unsubscribe.Clear();
        Manager.Dispose();
    }
}
