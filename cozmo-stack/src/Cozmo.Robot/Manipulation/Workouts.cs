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
    /// <summary>The source MoodScorer mean and roundf; delta entries require a supplied history source.</summary>
    public int GetNumStrongLifts(Func<EmotionType, double> mood) => MoodScoreHelper(EvaluateEmotionScore(NumStrongLifts,mood,null));
    public int GetNumWeakLifts(Func<EmotionType, double> mood) => MoodScoreHelper(EvaluateEmotionScore(NumWeakLifts,mood,null));

    /// <summary>
    /// <c>MoodScorer::EvaluateEmotionScore</c> 0x0067C9B8: for each entry <c>x</c> is the emotion's current
    /// value minus its value 60 ticks ago when <c>trackDelta</c>, else the current value, and
    /// <c>y = scoreGraph.Evaluate(x)</c>; if any <c>|y| &lt; 1e-5</c> the whole score is 0.0; otherwise the
    /// arithmetic mean; an empty scorer is 0.0 (M13-010 / Appendix G R3-3/R3-4).
    /// </summary>
    // fidelity: M13-010
    public static double EvaluateEmotionScore(IReadOnlyList<EmotionScorer> entries,
                                              Func<EmotionType, double> current, Func<EmotionType, double>? value60TicksAgo, Func<bool>? ownerCurrent = null)
    {
        if (entries.Count == 0) return 0.0;
        float sum = 0f;
        foreach (var e in entries)
        {
            // A caller without the actual 60-tick history must not substitute a fabricated value.
            if (e.TrackDelta && value60TicksAgo is null)
                throw new NotSupportedException("M13-010: trackDelta needs the actual Emotion::GetHistoryValueTicksAgo (0x006794F8) input");
            float x = (float)current(e.Emotion);
            if(ownerCurrent is not null&&!ownerCurrent())return 0;
            if (e.TrackDelta) { x -= (float)value60TicksAgo!(e.Emotion); if(ownerCurrent is not null&&!ownerCurrent())return 0; }
            float y = (float)e.Graph.EvaluateY(x);
            if (MathF.Abs(y) < GraphEvaluator.Epsilon) return 0.0;
            sum += y;
        }
        return sum / (float)entries.Count;
    }

    /// <summary>
    /// <c>WorkoutConfig::MoodScoreHelper</c> 0x00573B70: an empty scorer is 0; otherwise
    /// <c>max(0, round(EvaluateEmotionScore))</c> (the native <c>roundf</c> then <c>vcvt.u32.f32</c>).
    /// </summary>
    // fidelity: M13-010
    public static int MoodScoreHelper(double score)
    {
        uint result=NativeMoodScoreHelper(score);
        if(result>int.MaxValue)
            throw new NativeActionUnavailable("M13-010 native unsigned workout count exceeds the public signed count view");
        return (int)result;
    }
    internal static uint NativeMoodScoreHelper(double score)
    {
        float value=(float)score;
        if(!float.IsFinite(value)||value>=4294967296f)
            throw new NativeActionUnavailable("M13-010 workout score is outside the checked finite unsigned count range");
        return value<=0f?0u:(uint)MathF.Round(value,MidpointRounding.AwayFromZero);
    }
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
internal sealed class WorkoutConfigurationException(string message, WorkoutComponent partial, Exception inner):Exception(message,inner)
{
    internal WorkoutComponent Partial {get;}=partial;
}

public sealed class WorkoutComponent
{
    public static string ObbRelativePath => Path.Combine("assets", "cozmo_resources", "config", "engine", "behaviorSystem", "workout_config.json");

    public WorkoutComponent(IReadOnlyList<WorkoutConfig> workouts) : this(workouts,true) {}
    private WorkoutComponent(IReadOnlyList<WorkoutConfig> workouts,bool currentValid){Workouts=workouts;_currentValid=currentValid;}
    private readonly bool _currentValid;

    public IReadOnlyList<WorkoutConfig> Workouts { get; }
    /// <summary>Host observation of valid completion calls; not a native WorkoutComponent field.</summary>
    public int CompletedWorkouts { get; private set; }

    /// <summary>Which entry is current: the engine starts at the first and never goes back.</summary>
    public int CurrentIndex { get; private set; }

    /// <summary>
    /// The mood hook <c>CompleteCurrentWorkout</c> uses to fire the finished workout's emotion event
    /// (<c>MoodManager::TriggerEmotionEvent</c> 0x00573E1C, M13-010). The engine's component owns this
    /// call; the C# component has no mood, so the caller wires it. A non-empty event requires this recipient.
    /// </summary>
    public Func<string, double, bool>? TriggerEmotionEvent { get; set; }

    /// <summary>The clock the emotion event is stamped with.</summary>
    public Func<double>? ClockSec { get; set; }
    internal Action? CompletionEventObservedForTest;

    public WorkoutConfig? GetCurrentWorkout() => !_currentValid || Workouts.Count == 0 ? null : Workouts[CurrentIndex];

    /// <summary>
    /// Finishes the current workout: trigger the finished workout's emotion event
    /// (<c>MoodManager::TriggerEmotionEvent</c> 0x00573E1C) and then move to the next, stopping on the last -
    /// the engine's <c>if (current != last) current += 0x40</c> at 0x00573E24 (M13-010).
    /// </summary>
    // fidelity: M13-010
    public void CompleteCurrentWorkout()
        => CompleteCurrentWorkout(null);

    internal void CompleteCurrentWorkout(Func<bool>? current)
    {
        var finished = GetCurrentWorkout();
        if (finished is null)
        {
            if(!_currentValid && Workouts.Count!=0)
                SteppedBehavior.ReportMissing("M1-034/M13-010: skip CompleteCurrentWorkout null-current dereference at 0x00573DFC after failed configuration; prior state retained");
            return;
        }
        if (finished.EmotionEventOnComplete.Length != 0)
        {
            if (TriggerEmotionEvent is null) throw new NativeActionUnavailable("M13-010 actual workout completion MoodManager recipient unavailable");
            if (ClockSec is null) throw new NativeActionUnavailable("M13-010 actual workout completion MoodManager clock unavailable");
            double now=ClockSec();
            if(current is not null && !current()) return;
            TriggerEmotionEvent(finished.EmotionEventOnComplete, now);
            CompletionEventObservedForTest?.Invoke();
        }
        CompletedWorkouts++;
        if (CurrentIndex < Workouts.Count - 1) CurrentIndex++;
    }

    /// <summary>
    /// <c>ShouldPlayEightiesMusic</c> 0x00573E30: return the cached answer once evaluated (+0x11), else score
    /// the current workout's <b>numStrongLifts</b> MoodScorer (workout+0x18) through
    /// <c>MoodScoreHelper</c> 0x00573B70 -> <c>EvaluateEmotionScore</c> 0x0067C9B8, return false when that
    /// score is 0, otherwise <c>RandDbl(1.0) &lt; 0.1</c>; cache the answer at +0x10 and the evaluated flag
    /// at +0x11 (M13-010 / Appendix G R3-1..R3-6).
    ///
    /// <paramref name="current"/> and <paramref name="value60TicksAgo"/> are the actual MoodState values and
    /// history when called by the robot-owned component. The audio-client and ordinary workout callers share
    /// this cache and publish their respective stage values through the native public-state owner.
    /// </summary>
    // fidelity: M13-010
    public bool ShouldPlayEightiesMusic(Func<EmotionType, double> current, Func<EmotionType, double>? value60TicksAgo, Func<double> randDbl, Func<bool>? ownerCurrent = null)
    {
        if (_eightiesEvaluated) return _eightiesAnswer;
        var workout = GetCurrentWorkout();
        if(workout is null)
        {
            SteppedBehavior.ReportMissing("M1-034/M13-010: skip uncached music decision null-current read 0x00573B70 via 0x00573E42; no cache or RNG write");
            return false; // host policy for the skipped native fault, not a native result.
        }
        uint score = WorkoutConfig.NativeMoodScoreHelper(WorkoutConfig.EvaluateEmotionScore(workout.NumStrongLifts, current, value60TicksAgo,ownerCurrent));
        if(ownerCurrent is not null&&!ownerCurrent())return false;
        bool answer = score != 0 && randDbl() < 0.1;
        if(ownerCurrent is not null&&!ownerCurrent())return answer;
        _eightiesAnswer = answer;
        _eightiesEvaluated = true;
        return answer;
    }

    private bool _eightiesEvaluated;
    private bool _eightiesAnswer;

    // 005D8198 clears only the cache-valid byte.
    internal void ResetEightiesMusicDecision() => _eightiesEvaluated = false;

    public static WorkoutComponent? FromObb(string obbRoot)
    {
        var p = Path.Combine(obbRoot, ObbRelativePath);
        return File.Exists(p) ? Parse(File.ReadAllText(p)) : null;
    }

    public static WorkoutComponent Parse(string json,Action<string>? diagnostic=null)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var list = new List<WorkoutConfig>();
        foreach (var w in doc.RootElement.GetProperty("workouts").EnumerateArray())
        {
            AnimationTrigger T(string k,bool required=false)
            {
                if(!w.TryGetProperty(k,out var value)||value.ValueKind==JsonValueKind.Null)
                {
                    if(required)throw new JsonException("Workout required animation trigger missing: "+k);
                    return (AnimationTrigger)0x23F;
                }
                string? name=value.GetString();
                if(name is not null&&Enum.TryParse<AnimationTrigger>(name,false,out var parsed)&&Enum.GetName(parsed)==name)return parsed;
                diagnostic?.Invoke("Workout configuration has unknown animation trigger '"+name+"' for "+k+"; native enum conversion returns 0");
                return (AnimationTrigger)0;
            }
            IReadOnlyList<EmotionScorer> Graphs(string k) => w.GetProperty(k).EnumerateArray().Select(g =>
                new EmotionScorer(Enum.Parse<EmotionType>(g.GetProperty("emotionType").GetString()!),
                    Graph2d.FromJson(g.GetProperty("scoreGraph")) ?? new Graph2d(Array.Empty<(double, double)>()),
                    g.TryGetProperty("trackDelta", out var td) && td.GetBoolean())).ToList();
            try
            {
                var pre=T("preLiftAnim");var post=T("postLiftAnim");var transition=T("transitionAnim");var putDown=T("putDownAnim");
                var strong=T("strongLiftAnim",true);var weak=T("weakLiftAnim",true);
                list.Add(new WorkoutConfig(pre,post,strong,transition,weak,putDown,Graphs("numStrongLifts"), Graphs("numWeakLifts"),
                                       w.TryGetProperty("emotionEventOnComplete", out var ev) ? ev.GetString() ?? "" : "",
                                       w.TryGetProperty("additionalBehaviorObjectiveOnComplete", out var ob) ? ob.GetString() ?? "" : ""));
            }
            catch(Exception error) when(error is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or FormatException)
            {
                throw new WorkoutConfigurationException("Workout configuration entry failed; prior entries retained without a current workout",new WorkoutComponent(list.ToArray(),false),error);
            }
        }
        return new WorkoutComponent(list);
    }
}
