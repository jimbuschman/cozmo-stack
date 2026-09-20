using System.Text.Json;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Manipulation;

/// <summary>A piecewise-linear score graph from the workout config (<c>Anki::Util::GraphEvaluator2d</c>).</summary>
public sealed record ScoreGraph(IReadOnlyList<(double X, double Y)> Nodes)
{
    public double Evaluate(double x)
    {
        if (Nodes.Count == 0) return 0;
        if (x <= Nodes[0].X) return Nodes[0].Y;
        for (int i = 1; i < Nodes.Count; i++)
            if (x <= Nodes[i].X)
            {
                double t = (x - Nodes[i - 1].X) / (Nodes[i].X - Nodes[i - 1].X);
                return Nodes[i - 1].Y + t * (Nodes[i].Y - Nodes[i - 1].Y);
            }
        return Nodes[^1].Y;
    }
}

/// <summary>
/// One workout from ASSET <c>config/engine/behaviorSystem/workout_config.json</c> (<c>WorkoutConfig</c>): the
/// six animation triggers, the strong/weak lift counts as score graphs over an emotion (Confident in every
/// shipped entry), the emotion event fired on completion and the extra behaviour objective.
/// </summary>
public sealed record WorkoutConfig(AnimationTrigger PreLift, AnimationTrigger PostLift, AnimationTrigger StrongLift, AnimationTrigger Transition,
                                   AnimationTrigger WeakLift, AnimationTrigger PutDown,
                                   IReadOnlyList<(EmotionType Emotion, ScoreGraph Graph)> NumStrongLifts,
                                   IReadOnlyList<(EmotionType Emotion, ScoreGraph Graph)> NumWeakLifts,
                                   string EmotionEventOnComplete, string AdditionalObjectiveOnComplete)
{
    /// <summary><c>WorkoutConfig::GetNumStrongLifts</c>: the graphs summed over the current mood, rounded.</summary>
    public int GetNumStrongLifts(Func<EmotionType, double> mood) => (int)Math.Round(NumStrongLifts.Sum(g => g.Graph.Evaluate(mood(g.Emotion))));
    public int GetNumWeakLifts(Func<EmotionType, double> mood) => (int)Math.Round(NumWeakLifts.Sum(g => g.Graph.Evaluate(mood(g.Emotion))));
}

/// <summary>
/// The engine's <c>WorkoutComponent</c> (<c>InitConfiguration</c>, <c>GetCurrentWorkout</c>,
/// <c>CompleteCurrentWorkout</c>, <c>ShouldPlayEightiesMusic</c>): holds the shipped workouts and the current
/// one. The shipped file has four entries (high, medium and two weak-energy variants; only the first names an extra objective). How the engine picks the current one
/// was not read (INFERRED: from the energy need); <see cref="Selector"/> chooses by index, medium by default
/// (LOCAL_POLICY). <c>CompleteCurrentWorkout</c> advances a completion count.
/// </summary>
public sealed class WorkoutComponent
{
    public static string ObbRelativePath => Path.Combine("assets", "cozmo_resources", "config", "engine", "behaviorSystem", "workout_config.json");

    public WorkoutComponent(IReadOnlyList<WorkoutConfig> workouts) => Workouts = workouts;

    public IReadOnlyList<WorkoutConfig> Workouts { get; }
    public Func<int> Selector { get; set; } = () => 1;
    public int CompletedWorkouts { get; private set; }

    public WorkoutConfig? GetCurrentWorkout() => Workouts.Count == 0 ? null : Workouts[Math.Clamp(Selector(), 0, Workouts.Count - 1)];
    public void CompleteCurrentWorkout() => CompletedWorkouts++;

    public static WorkoutComponent? FromObb(string obbRoot)
    {
        var p = Path.Combine(obbRoot, ObbRelativePath);
        return File.Exists(p) ? Parse(File.ReadAllText(p)) : null;
    }

    public static WorkoutComponent Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var list = new List<WorkoutConfig>();
        foreach (var w in doc.RootElement.GetProperty("workouts").EnumerateArray())
        {
            AnimationTrigger T(string k) => Enum.Parse<AnimationTrigger>(w.GetProperty(k).GetString()!);
            IReadOnlyList<(EmotionType, ScoreGraph)> Graphs(string k) => w.GetProperty(k).EnumerateArray().Select(g =>
                (Enum.Parse<EmotionType>(g.GetProperty("emotionType").GetString()!),
                 new ScoreGraph(g.GetProperty("scoreGraph").GetProperty("nodes").EnumerateArray().Select(n => (n.GetProperty("x").GetDouble(), n.GetProperty("y").GetDouble())).ToList()))).ToList();
            list.Add(new WorkoutConfig(T("preLiftAnim"), T("postLiftAnim"), T("strongLiftAnim"), T("transitionAnim"), T("weakLiftAnim"), T("putDownAnim"),
                                       Graphs("numStrongLifts"), Graphs("numWeakLifts"),
                                       w.TryGetProperty("emotionEventOnComplete", out var ev) ? ev.GetString() ?? "" : "",
                                       w.TryGetProperty("additionalBehaviorObjectiveOnComplete", out var ob) ? ob.GetString() ?? "" : ""));
        }
        return new WorkoutComponent(list);
    }
}
