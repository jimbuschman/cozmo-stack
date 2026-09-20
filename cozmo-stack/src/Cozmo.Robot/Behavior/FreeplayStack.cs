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

        ctx.ClockSec ??= clockSec;
        var manager = new BehaviorManager(ctx);
        if (withReactions)
            foreach (var reg in ShippedBehaviors.Reactions(robot, vision?.Locator, clockSec, vision)) manager.AddReaction(reg.Strategy, reg.Behavior, reg.ResumeLast);

        // one repetition history for the whole stack: the manager records it, every chooser reads it
        var tree = ActivityTreeLoader.Load(obbRoot, bound, manager.Penalty, random);
        foreach (var s in tree.SelectMany(a => a.SubActivities.Prepend(a)).Select(a => a.Strategy)) s.NeedLevels ??= n => needs.State.GetNeedLevel(n);
        var freeplayActivity = tree.FirstOrDefault(a => a.Id == "Freeplay") ?? throw new InvalidOperationException("activities_config.json has no Freeplay activity");
        var inputs = new FreeplayInputs { Needs = needs, Mood = ctx.Mood };
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
        return stack;
    }

    /// <summary>One tick with the inputs refreshed from the robot and the world.</summary>
    public FreeplayDecision Tick(double nowSec, double nowMs, CozmoRobot robot, VisionSystem? vision, ManipulationSystem? m)
    {
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
