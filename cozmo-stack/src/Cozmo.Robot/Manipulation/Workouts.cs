using System.Text.Json;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Manipulation;

/// <summary>
/// One workout from ASSET <c>config/engine/behaviorSystem/workout_config.json</c> (<c>WorkoutConfig</c>): the
/// six animation triggers, the strong/weak lift scorers, the emotion event fired on completion and the extra
/// behaviour objective. The scorers are the same <see cref="Cozmo.Robot.Behavior.EmotionScorer"/>
/// (<c>emotionType</c>, <c>scoreGraph</c>, <c>trackDelta</c>) the mood model uses
/// (<c>EmotionScorer::ReadFromJson</c> 0x0067AABC; M13-010 / Appendix G R3-2).
/// </summary>
public sealed record WorkoutConfig(AnimationTrigger PreLift, AnimationTrigger PostLift, AnimationTrigger StrongLift, AnimationTrigger Transition,
                                   AnimationTrigger WeakLift, AnimationTrigger PutDown,
                                   IReadOnlyList<EmotionScorer> NumStrongLifts,
                                   IReadOnlyList<EmotionScorer> NumWeakLifts,
                                   string EmotionEventOnComplete, string AdditionalObjectiveOnComplete)
{
    /// <summary><c>WorkoutConfig::GetNumStrongLifts</c>: the graphs summed over the current mood, rounded.</summary>
    public int GetNumStrongLifts(Func<EmotionType, double> mood) => (int)Math.Round(NumStrongLifts.Sum(g => g.Graph.EvaluateY(mood(g.Emotion))));
    public int GetNumWeakLifts(Func<EmotionType, double> mood) => (int)Math.Round(NumWeakLifts.Sum(g => g.Graph.EvaluateY(mood(g.Emotion))));

    /// <summary>
    /// <c>MoodScorer::EvaluateEmotionScore</c> 0x0067C9B8: for each entry <c>x</c> is the emotion's current
    /// value minus its value 60 ticks ago when <c>trackDelta</c>, else the current value, and
    /// <c>y = scoreGraph.Evaluate(x)</c>; if any <c>|y| &lt; 1e-5</c> the whole score is 0.0; otherwise the
    /// arithmetic mean; an empty scorer is 0.0 (M13-010 / Appendix G R3-3/R3-4).
    /// </summary>
    // fidelity: M13-010
    public static double EvaluateEmotionScore(IReadOnlyList<EmotionScorer> entries,
                                              Func<EmotionType, double> current, Func<EmotionType, double>? value60TicksAgo)
    {
        if (entries.Count == 0) return 0.0;
        double sum = 0;
        foreach (var e in entries)
        {
            // M13-010 / M7-mood: Emotion::GetHistoryValueTicksAgo 0x006794F8 needs the per-emotion ring buffer
            // (capacity 0x80, initial {0,0} sample, one sample per Emotion::Update), which this stack's MoodState
            // does not keep. No caller may pass a stand-in: a trackDelta entry without the source is refused.
            if (e.TrackDelta && value60TicksAgo is null)
                throw new NotSupportedException("M13-010: trackDelta needs Emotion::GetHistoryValueTicksAgo (0x006794F8), the M7-mood history ring buffer, which is not built");
            double x = e.TrackDelta ? current(e.Emotion) - value60TicksAgo!(e.Emotion) : current(e.Emotion);
            double y = e.Graph.EvaluateY(x);
            if (Math.Abs(y) < 1e-5) return 0.0;
            sum += y;
        }
        return sum / entries.Count;
    }

    /// <summary>
    /// <c>WorkoutConfig::MoodScoreHelper</c> 0x00573B70: an empty scorer is 0; otherwise
    /// <c>max(0, round(EvaluateEmotionScore))</c> (the native <c>roundf</c> then <c>vcvt.u32.f32</c>).
    /// </summary>
    // fidelity: M13-010
    public static int MoodScoreHelper(double score) => Math.Max(0, (int)Math.Round(score, MidpointRounding.AwayFromZero));
}

/// <summary>
/// The engine's <c>WorkoutComponent</c> (<c>InitConfiguration</c>, <c>GetCurrentWorkout</c>,
/// <c>CompleteCurrentWorkout</c>, <c>ShouldPlayEightiesMusic</c>): holds the shipped workouts and the current
/// one. The shipped file has four entries (high, medium and two weak-energy variants; only the first
/// names an extra objective).
///
/// The engine does not pick one: it walks them. <c>GetCurrentWorkout</c> 0x00573DE8 returns a pointer
/// held at +0xC, and <c>CompleteCurrentWorkout</c> 0x00573DEC fires the finished workout's emotion
/// event through <c>MoodManager::TriggerEmotionEvent</c> and then steps that pointer on by one entry -
/// 0x40 bytes - unless it is already the last (<c>r1 = end - 0x40; if (current != last) current +=
/// 0x40</c> at 0x00573E24). So the workouts run in file order and the last one repeats for ever.
///
/// <c>ShouldPlayEightiesMusic</c> 0x00573E30 caches its answer in a flag at +0x11: the first time it is
/// asked it scores the current workout's mood scorer and, if that passes, rolls <c>RandDbl(1.0)</c>
/// against a constant.
/// </summary>
public sealed class WorkoutComponent
{
    public static string ObbRelativePath => Path.Combine("assets", "cozmo_resources", "config", "engine", "behaviorSystem", "workout_config.json");

    public WorkoutComponent(IReadOnlyList<WorkoutConfig> workouts) => Workouts = workouts;

    public IReadOnlyList<WorkoutConfig> Workouts { get; }
    public int CompletedWorkouts { get; private set; }

    /// <summary>Which entry is current: the engine starts at the first and never goes back.</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>
    /// The mood hook <c>CompleteCurrentWorkout</c> uses to fire the finished workout's emotion event
    /// (<c>MoodManager::TriggerEmotionEvent</c> 0x00573E1C, M13-010). The engine's component owns this
    /// call; the C# component has no mood, so the caller wires it. Null means the event is not fired.
    /// </summary>
    public Func<string, double, bool>? TriggerEmotionEvent { get; set; }

    /// <summary>The clock the emotion event is stamped with.</summary>
    public Func<double> ClockSec { get; set; } = () => 0;

    public WorkoutConfig? GetCurrentWorkout() => Workouts.Count == 0 ? null : Workouts[CurrentIndex];

    /// <summary>
    /// Finishes the current workout: trigger the finished workout's emotion event
    /// (<c>MoodManager::TriggerEmotionEvent</c> 0x00573E1C) and then move to the next, stopping on the last -
    /// the engine's <c>if (current != last) current += 0x40</c> at 0x00573E24 (M13-010).
    /// </summary>
    // fidelity: M13-010
    public void CompleteCurrentWorkout()
    {
        var finished = GetCurrentWorkout();
        CompletedWorkouts++;
        if (finished is not null && TriggerEmotionEvent is not null)
            TriggerEmotionEvent(finished.EmotionEventOnComplete, ClockSec());
        if (CurrentIndex < Workouts.Count - 1) CurrentIndex++;
    }

    /// <summary>
    /// <c>ShouldPlayEightiesMusic</c> 0x00573E30: return the cached answer once evaluated (+0x11), else score
    /// the current workout's <b>numStrongLifts</b> MoodScorer (workout+0x18) through
    /// <c>MoodScoreHelper</c> 0x00573B70 -> <c>EvaluateEmotionScore</c> 0x0067C9B8, return false when that
    /// score is 0, otherwise <c>RandDbl(1.0) &lt; 0.1</c>; cache the answer at +0x10 and the evaluated flag
    /// at +0x11 (M13-010 / Appendix G R3-1..R3-6).
    ///
    /// <paramref name="current"/> and <paramref name="value60TicksAgo"/> are the emotion values the scorer
    /// reads; the stack's <c>MoodState</c> keeps no history, so the 60-ticks-ago source is M7-mood's ring buffer
    /// and is null here: a workout whose scorer has <c>trackDelta</c> then throws <see cref="NotSupportedException"/>
    /// (no shipped workout scorer has it). The engine's two callers - <c>Audio::BehaviorAudioClient::HandleSparkUpdates</c>
    /// 0x00592416 and <c>BehaviorCubeLiftWorkout::TransitionToPostLiftAnim</c> 0x005D8014 - are not wired: each
    /// feeds <c>PublicStateBroadcaster::UpdateBroadcastBehaviorStage(3, ...)</c>, which does not exist here.
    /// </summary>
    // fidelity: M13-010
    public bool ShouldPlayEightiesMusic(Func<EmotionType, double> current, Func<EmotionType, double>? value60TicksAgo, Func<double> randDbl)
    {
        if (_eightiesEvaluated) return _eightiesAnswer;
        var workout = GetCurrentWorkout();
        int score = workout is null ? 0 : WorkoutConfig.MoodScoreHelper(WorkoutConfig.EvaluateEmotionScore(workout.NumStrongLifts, current, value60TicksAgo));
        bool answer = score != 0 && randDbl() < 0.1;
        _eightiesAnswer = answer;
        _eightiesEvaluated = true;
        return answer;
    }

    private bool _eightiesEvaluated;
    private bool _eightiesAnswer;

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
            IReadOnlyList<EmotionScorer> Graphs(string k) => w.GetProperty(k).EnumerateArray().Select(g =>
                new EmotionScorer(Enum.Parse<EmotionType>(g.GetProperty("emotionType").GetString()!),
                    Graph2d.FromJson(g.GetProperty("scoreGraph")) ?? new Graph2d(Array.Empty<(double, double)>()),
                    g.TryGetProperty("trackDelta", out var td) && td.GetBoolean())).ToList();
            list.Add(new WorkoutConfig(T("preLiftAnim"), T("postLiftAnim"), T("strongLiftAnim"), T("transitionAnim"), T("weakLiftAnim"), T("putDownAnim"),
                                       Graphs("numStrongLifts"), Graphs("numWeakLifts"),
                                       w.TryGetProperty("emotionEventOnComplete", out var ev) ? ev.GetString() ?? "" : "",
                                       w.TryGetProperty("additionalBehaviorObjectiveOnComplete", out var ob) ? ob.GetString() ?? "" : ""));
        }
        return new WorkoutComponent(list);
    }
}
