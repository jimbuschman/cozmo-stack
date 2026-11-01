using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

internal sealed class NativeChooserUnavailable(string reason,string? behaviorId=null):Exception(reason)
{internal string? BehaviorId=>behaviorId;}
// Native ActivityID table 0x01033070, EnumToString 0x00766C50.
internal static class ActivityIdNames
{
    private static readonly string[] Names = { "Invalid", "BuildPyramid", "Feeding", "Freeplay", "GatherCubes", "Hiking", "MeetCozmo", "NeedsSevereLowEnergy", "NeedsSevereLowRepair", "NeedsSevereLowPlayGetIn", "NothingToDo", "PlayAlone", "PlayWithHumans", "PutDownDispatch", "Selection", "Singing", "Socialize", "SparksBuildPyramid", "SparksCozmoSings", "SparksFistBump", "SparksGatherCubes", "SparksKnockOverCubes", "SparksPeekABoo", "SparksPickUpCube", "SparksPopAWheelie", "SparksPounceOnMotion", "SparksRollBlock", "SparksStackBlock", "SparksWorkout", "StrictPriorityFreeplay", "SparksTrackLaser", "SparksFireTruckAlarm" };
    internal static bool TryId(string name, out byte id) { int i=Array.IndexOf(Names,name); id=(byte)(i < 0 ? 0 : i); return i>=0; }
    internal static byte FromName(string name) { TryId(name,out byte id); return id; }
    internal static string Name(byte id)=>Names[id];
}

/// <summary>
/// <c>Anki::Util::GraphEvaluator2d::EvaluateY(float)</c> 0x00804bd0..0x00804c40 in the engine's arithmetic: everything is <c>float</c>. At or below
/// the first node (and for a graph of fewer than two nodes) the first node's y; above the last node the last node's y; between two nodes
/// the <c>vdiv.f32</c> of (x - x0) by the span, then <c>vmul.f32</c> by (y1 - y0), then <c>vadd.f32</c> of y0 - except that a span at or below
/// 1e-5f (0x3727c5ac, <c>ble</c> 0x00804c14..0x00804c1c) returns the <b>left</b> node's y.
/// </summary>
// fidelity: M8-003
internal static class GraphEvaluator
{
    /// <summary>1e-5f, <c>0x3727c5ac</c>.</summary>
    public static readonly float Epsilon = BitConverter.Int32BitsToSingle(0x3727c5ac);

    public static double EvaluateY(IReadOnlyList<(double X, double Y)> nodes, double xValue, double empty)
    {
        if (nodes.Count == 0) return empty;
        float x = (float)xValue;
        if (float.IsNaN(x)) return (float)nodes[^1].Y;                       // every compare fails in the engine: the last node's y
        if (nodes.Count < 2 || x <= (float)nodes[0].X) return (float)nodes[0].Y;
        for (int i = 1; i < nodes.Count; i++)
        {
            float x1 = (float)nodes[i].X;
            if (x > x1) continue;
            float x0 = (float)nodes[i - 1].X, y0 = (float)nodes[i - 1].Y, y1 = (float)nodes[i].Y;
            float span = x1 - x0;
            if (span <= Epsilon) return y0;
            float t = (x - x0) / span;
            float scaled = t * (y1 - y0);
            return y0 + scaled;
        }
        return (float)nodes[^1].Y;
    }
}

/// <summary>A piecewise-linear graph (<c>Anki::Util::GraphEvaluator2d</c>) over sorted nodes.</summary>
public sealed record Graph2d(IReadOnlyList<(double X, double Y)> Nodes)
{
    public double EvaluateY(double x) => GraphEvaluator.EvaluateY(Nodes, x, empty: 0);

    public static Graph2d? FromJson(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("nodes", out var nodes)) return null;
        var list = nodes.EnumerateArray().Select(n => (n.GetProperty("x").GetDouble(), n.GetProperty("y").GetDouble())).ToList();
        return list.Count == 0 ? null : new Graph2d(list);
    }
}

/// <summary>
/// One behaviour's scoring entry in a Scoring chooser. <c>IBehavior::ReadFromScoredJson</c> 0x005BC488
/// reads <c>emotionScorers</c> into a MoodScorer, <c>flatScore</c> into a float, <c>repetitionPenalty</c>
/// into a graph over seconds since the behaviour last ran, and
/// <c>considerThisHasRunForBehaviorObjective</c> into a behaviour objective; the activity configs add
/// <c>runningPenalty</c> and <c>boredomMultiplier</c>. A behaviour whose config carries no scoring keeps
/// the constructor's zero (<c>IBehavior::IBehavior</c> writes 0 to +0x100 at 0x005BBD28), so it scores
/// nothing in a scoring chooser. A missing <c>repetitionPenalty</c> or <c>runningPenalty</c> is a flat graph,
/// <c>AddNode(0.0f, 1.0f, true)</c> (0x005bc55c..0x005bc566, 0x005bc666..0x005bc678): no penalty.
/// </summary>
// fidelity: M8-003
// These inputs belong to the concrete native behavior owners, which are not built yet.
// A supplier must expose their actual state, not a synthetic timer or sector count.
public interface IPounceScoreState
{
    float LastGroundMotionSeconds { get; }
    float NotRunningBoredSeconds { get; }
    float BoredomMultiplier { get; }
}

public interface ILookInPlaceMemoryMapScoreState
{
    byte CompletedSectorCount { get; }
    byte PrioritySectorCount { get; }
    float LowPriorityScoreReduction { get; }
}

// fidelity: M8-003
public sealed record ScoredBehaviorEntry(string BehaviorId, double FlatScore, Graph2d? RepetitionPenalty, Graph2d? RunningPenalty, double? BoredomMultiplier,
                                         IReadOnlyList<EmotionScorer> EmotionScorers)
{
    /// <summary>
    /// <c>IBehavior::EvaluateScore</c> 0x005BEF60 over
    /// <c>IBehavior::EvaluateScoreInternal</c> 0x005BEEC2: the emotion scorers if there are any,
    /// <b>otherwise</b> the flat score - not the two added. The <b>running</b> branch
    /// (<c>ldrb.w r0,[r4,#0xa1]</c> at 0x005bef7a) is <b>not</b> gated on <c>IsRunnable</c>: it is that
    /// score plus the +0x104 running bonus, times the running penalty when +0x111 is set. The
    /// <b>non-running</b> branch tests <c>IsRunnableBase</c> (0x005befa2) and the <c>vtable+0x50</c> gate
    /// (0x005befce), then applies the repetition penalty only when +0x110 is set and
    /// <c>now &gt;= +0x108</c> (0x005befd4/0x005befd8/0x005befe2).
    ///
    /// EvaluateScoreInternal is three instructions: if the MoodScorer's list is not empty it tail-calls
    /// <c>MoodScorer::EvaluateEmotionScore(moodManager)</c>, and only an empty list falls through to the
    /// float at +0x100, the flat score.
    /// </summary>
    /// <param name="defaultPenalty">Unused. It used to supply <c>mood_config.json</c>'s <c>defaultRepetitionPenalty</c> for an entry with
    /// no graph of its own; the engine gives such a behaviour a flat 1.0 graph (<c>AddNode(0.0f, 1.0f, true)</c>,
    /// 0x005bc55c..0x005bc566), so there is no default to supply.</param>
    /// <param name="runningClockSec">The running-penalty clock +0x34 (<c>EvaluateRunningPenalty</c> 0x005bef22 returns 1.0 when it is &lt;= 0).
    /// Null means the caller has no such stamp and the graph is evaluated at <paramref name="runningSec"/>.</param>
    public double Evaluate(IBehavior b, BehaviorContext ctx, double nowSec, double? lastRunSec, double? runningSec, RepetitionPenalty? defaultPenalty,
                           double runningBonus = 0, bool repetitionPenaltyEnabled = true, bool runningPenaltyEnabled = true, bool penaltySuppressed = false,
                           double? runningClockSec = null)
    {
        // The engine computes the score in float: vadd.f32 0x005bef88, vmul.f32 0x005bef98 and 0x005beffe; the penalty inputs are float - float.
        float score = (float)(EmotionScorers.Count > 0 ? EmotionScore(ctx) : FlatScore);
        if (runningSec is { } r)
        {
            // Pounce's running factor is one; Look's running override precedes the common bonus.
            if (b is IPounceScoreState) score = 1f * score; // 0x005f83c4..404
            if (b is ILookInPlaceMemoryMapScoreState look && look.CompletedSectorCount >= look.PrioritySectorCount)
                score -= look.LowPriorityScoreReduction; // 0x005e4f30..48
            // Running branch: EvaluateScoreInternal + the float at +0x104 (vldr s2,[r4,#0x104]
            // 0x005bef80; vadd.f32 0x005bef88), then multiplied by EvaluateRunningPenalty only when the
            // +0x111 enable byte is set (0x005bef84/0x005bef8c). No IsRunnable gate here.
            score += (float)runningBonus;
            if (runningPenaltyEnabled)
            {
                // EvaluateRunningPenalty 0x005bef22..0x005bef5a: 1.0 when +0x34 <= 0 (or NaN), else the +0xf4 graph at
                // float(now) - float(+0x34) (vsub.f32 0x005bef4e); a missing runningPenalty key left that graph at its flat (0.0, 1.0) node.
                float x = runningClockSec is { } clock ? (float)nowSec - (float)clock : (float)r;
                bool noStamp = runningClockSec is { } c2 && !((float)c2 > 0);
                if (!noStamp && RunningPenalty is { } rp) score *= (float)rp.EvaluateY(x);
            }
        }
        else
        {
            // Non-running branch: IsRunnableBase (0x005befa2) then the vtable+0x50 gate (0x005befce).
            // The stack's IBehavior.IsRunnable is that IsRunnableBase + vtable+0x50 combination (M8-001 C1a).
            if (!b.IsRunnable(ctx)) return 0;
            if (b is IPounceScoreState pounce)
            {
                float threshold = pounce.LastGroundMotionSeconds + pounce.NotRunningBoredSeconds;
                float factor = threshold < (float)nowSec ? pounce.BoredomMultiplier : 1f;
                score = factor * score; // 0x005f83be..8404; unordered comparison retains factor one.
            }
            if (repetitionPenaltyEnabled && !penaltySuppressed && lastRunSec is { } last)
            {
                // EvaluateRepetitionPenalty 0x005beee6: 1.0 when +0x30 <= 0 (or NaN), else the +0xe8 graph at float(now) - float(+0x30)
                // (vsub.f32 0x005bef12); a missing repetitionPenalty key left that graph flat at 1.0 (0x005bc55c..0x005bc566).
                float stamp = (float)last;
                if (stamp > 0 && RepetitionPenalty is { } g2) score *= (float)g2.EvaluateY((float)nowSec - stamp);
            }
        }
        return score;
    }

    /// <summary>
    /// <c>MoodScorer::EvaluateEmotionScore</c> 0x0067C9B8: each scorer reads its emotion, takes the graph
    /// at that value, and the result is the <b>mean</b> of those - the sum divided by how many scored
    /// (0x0067CA6E..0x0067CA72). A scorer whose graph comes out within 1e-05 of zero (0x0067CA50) ends the
    /// whole thing at zero, so one emotion out of range vetoes the behaviour. No scorers at all is zero.
    /// </summary>
    private double EmotionScore(BehaviorContext ctx)
    {
        if (ctx.Mood is not { } mood) return 0;
        // MoodScorer::EvaluateEmotionScore 0x0067c9b8 is float: the emotion value is loaded as a float, the graph value is float, the veto test is
        // vcmpe.f32 against the literal 0x3727c5ac (pool 0x0067cab4; 0x0067c9da/0x0067ca50), the sum is vadd.f32 (0x0067ca5a) and the mean is the sum
        // divided by vcvt.f32.u32(count) (vdiv.f32 0x0067ca6e..0x0067ca72).
        float sum = 0;
        uint counted = 0;
        foreach (var scorer in EmotionScorers)
        {
            // fidelity: M13-010 (second copy of MoodScorer::EvaluateEmotionScore 0x0067C9B8; Workouts.cs is the first)
            // fidelity: M8-003
            // A trackDelta entry subtracts Emotion::GetHistoryValueTicksAgo(emotion, 60) from the emotion's value: the value is loaded
            // first (vldr s22,[r0,#0x18] 0x0067c9f0), then movs r1,#0x3c (0x0067c9ee) and the call (0x0067c9f4), then vsub.f32 (0x0067c9fc).
            // The graph is evaluated at that float (0x0067ca14).
            float value = (float)scorer.ValueFor(mood);
            if (scorer.TrackDelta)
                value -= mood.GetHistoryValueTicksAgo(scorer.Emotion, 60);
            float y = (float)scorer.Graph.EvaluateY(value);
            if (Math.Abs(y) < GraphEvaluator.Epsilon) return 0;
            sum += y;
            counted++;
        }
        return counted == 0 ? 0 : sum / (float)counted;
    }

    public static ScoredBehaviorEntry FromJson(JsonElement e)
    {
        string id = e.GetProperty("behaviorID").GetString()!;
        return FromScoringJson(id,e.TryGetProperty("scoring",out var child)?child:default);
    }
    internal static ScoredBehaviorEntry FromScoringJson(string id,JsonElement sc)
    {
        double flat = 0; Graph2d? rep = null, run = null; double? boredom = null;
        var scorers = new List<EmotionScorer>();
        if (sc.ValueKind==JsonValueKind.Object)
        {
            if (sc.TryGetProperty("flatScore", out var f)) flat = f.GetDouble();
            if (sc.TryGetProperty("repetitionPenalty", out var r)) rep = Graph2d.FromJson(r);
            if (sc.TryGetProperty("runningPenalty", out var rn)) run = Graph2d.FromJson(rn);
            if (sc.TryGetProperty("boredomMultiplier", out var bm)) boredom = bm.GetDouble();
            if (sc.TryGetProperty("emotionScorers", out var es))
                foreach (var s in es.EnumerateArray())
                    if (Enum.TryParse<EmotionType>(s.GetProperty("emotionType").GetString(), true, out var et) && s.TryGetProperty("scoreGraph", out var sg) && Graph2d.FromJson(sg) is { } g)
                        scorers.Add(new EmotionScorer(et, g,
                            s.TryGetProperty("trackDelta", out var td) && td.ValueKind == JsonValueKind.True));
        }
        return new ScoredBehaviorEntry(id, flat, rep, run, boredom, scorers);
    }
}

/// <summary>
/// <c>BehaviorObjective</c> (IBehavior +0x10c), parsed from <c>considerThisHasRunForBehaviorObjective</c> by
/// <c>BehaviorObjectiveFromString</c> (called at 0x005bc5a0). The ordinals are the
/// <c>EnumToString(BehaviorObjective)</c> 0x769540 string table at 0x1033140: [0]="Unknown",
/// [18]="PerformedWorkout", [22]="PoppedWheelie", [41]="Count" (0x29, the constructor's default sentinel).
/// </summary>
public enum BehaviorObjective
{
    Unknown = 0,
    PerformedWorkout = 18,
    PoppedWheelie = 22,
    Count = 0x29,
    Invalid = Count,
}

/// <summary>The <c>BehaviorObjectiveFromString</c> 0x005bc5a0 mapping for the shipped names.</summary>
public static class BehaviorObjectives
{
    public static BehaviorObjective FromString(string? name) => name switch
    {
        "PerformedWorkout" => BehaviorObjective.PerformedWorkout,
        "PoppedWheelie" => BehaviorObjective.PoppedWheelie,
        _ => BehaviorObjective.Invalid,
    };

    /// <summary>
    /// Every shipped behaviour config's <c>considerThisHasRunForBehaviorObjective</c>, by <c>behaviorID</c>.
    /// The key lives in the behaviour's own config (<c>behaviors/freeplay/popAWheelie.json</c>,
    /// <c>behaviors/freeplay/cubeLiftWorkout.json</c>), read by <c>IBehavior::ReadFromScoredJson</c>
    /// 0x005bc488; it is not an activity chooser entry.
    /// </summary>
    public static IReadOnlyDictionary<string, BehaviorObjective> Load(string obbRoot)
    {
        var map = new Dictionary<string, BehaviorObjective>(StringComparer.Ordinal);
        var dir = Path.Combine(obbRoot, "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "behaviors");
        if (!Directory.Exists(dir)) return map;
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(f), options);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;
                if (root.TryGetProperty("behaviorID", out var id) && id.GetString() is { } bid
                    && root.TryGetProperty("considerThisHasRunForBehaviorObjective", out var ob) && ob.GetString() is { } name)
                    map[bid] = FromString(name);
            }
            catch (JsonException) { }
        }
        return map;
    }
}

/// <summary>
/// One entry of a behaviour's MoodScorer: which emotion, the graph over its value, and whether the value
/// is taken as a change rather than a level. <c>EmotionScorer::ReadFromJson</c> 0x0067AABC reads
/// <c>emotionType</c>, <c>scoreGraph</c> and <c>trackDelta</c>.
///
/// With <c>trackDelta</c> set the engine subtracts the emotion's value sixty ticks ago
/// (<c>Emotion::GetHistoryValueTicksAgo(60)</c> at 0x0067C9F4) from its value now; <see cref="MoodState.GetHistoryValueTicksAgo"/>
/// is that ring (M7-013). Nothing shipped exercises it: not one of the behaviour or activity configs in cozmo_resources carries an
/// <c>emotionScorers</c> block, so every scored behaviour in the app is scored by its flat score alone.
/// </summary>
public sealed record EmotionScorer(EmotionType Emotion, Graph2d Graph, bool TrackDelta)
{
    /// <summary>The value the graph is evaluated at.</summary>
    public double ValueFor(MoodState mood) => mood[Emotion];
}

/// <summary>Why a chooser picked (or kept, or found nothing).</summary>
public sealed record ChooserDecision(IBehavior? Behavior, string Reason, IReadOnlyList<(string Id, double Score, string Note)> Scores)
{
    internal bool InputUnavailable { get; init; }
}

/// <summary><c>Anki::Cozmo::BehaviorChooserType</c> (UNITY): Scoring, Selection, StrictPriority.</summary>
public enum BehaviorChooserType { Scoring, Selection, StrictPriority }

/// <summary>The engine's <c>IBSRunnableChooser</c>: which behaviour an activity wants active, given what runs.</summary>
public interface IBehaviorChooser
{
    BehaviorChooserType Type { get; }
    IReadOnlyList<string> BehaviorIds { get; }
    ChooserDecision GetDesiredActiveBehavior(IBehavior? current, double currentRunningSec, BehaviorContext ctx, double nowSec);
}

/// <summary>
/// The engine's <c>ScoringBSRunnableChooser</c> (0x00609ED8..0x0060A7A0): every listed behaviour's score is
/// evaluated (<c>IBehavior::EvaluateScore</c>), the running one gets <c>ScoreBonusForCurrentBehavior</c> (a graph
/// over its running duration, default none), and a challenger replaces it only when it scores higher
/// ("behavior '%s' has score of %f, so is interrupting running behavior '%s' which scored %f"); each non-running
/// positive candidate draws before comparison, with equal final scores retaining the earlier candidate. Behaviours absent from the bound set are noted,
/// not invented.
/// </summary>
// fidelity: M8-013
// fidelity: M15-002
public sealed class ScoringChooser : IBehaviorChooser
{
    private readonly Dictionary<string, IBehavior> _bound;
    private readonly RepetitionPenalty _penalty;
    private IReadOnlyList<ScoredBehaviorEntry> _evaluationEntries;
    private readonly bool _native;
    private EngineRobot? _ordinaryOwner;
    internal void EnableOrdinaryUnbuiltBoundary(EngineRobot owner)
    {
        _ordinaryOwner=owner;
        _evaluationEntries=Entries.DistinctBy(e=>e.BehaviorId).OrderBy(e=>
            Enum.TryParse<BehaviorID>(e.BehaviorId,false,out var id) && id.ToString()==e.BehaviorId ? (int)id : 256).ToList();
    }
    private enum UnbuiltScore { KnownFalse, ScoringZero, Unavailable, NativeAbsent }
    private UnbuiltScore EvaluateUnbuilt(string id,BehaviorContext ctx,double nowSec)
    {
        var owner=_ordinaryOwner;
        if(owner is null || !ReferenceEquals(owner.Engine.Robot,owner) || owner.NativeBehaviorRuntime is not {} runtime)
            return UnbuiltScore.Unavailable;
        if(owner.Engine.HostBehaviorMap is not {} configured)return UnbuiltScore.Unavailable;
        if(!Enum.TryParse<BehaviorID>(id,false,out var nativeId) || nativeId.ToString()!=id)return UnbuiltScore.Unavailable;
        if(!configured.ContainsKey(nativeId))return UnbuiltScore.NativeAbsent;
        var winner=configured[nativeId];
        if(id=="SparksFireTruckAlarm")
        {
            if(owner.NativeConfiguredScoreFields?.TryGetValue(id,out var fire)!=true || !fire.ConstructorPristine ||
               fire.FireTruckConfig is not {} original || !original.Equals(winner) || fire.FireTruckProgression is not {} progression ||
               !ReferenceEquals(owner.Progression,progression))return UnbuiltScore.Unavailable;
            long epoch=fire.Epoch;
            if(!progression.TryIsUnlocked(83,out bool unlocked) || unlocked)return UnbuiltScore.Unavailable;
            if(fire.ConstructorPristine && fire.Epoch==epoch && ReferenceEquals(owner.Engine.Robot,owner) &&
               ReferenceEquals(owner.NativeBehaviorRuntime,runtime) && ReferenceEquals(owner.Progression,progression) &&
               ReferenceEquals(owner.Engine.HostBehaviorMap,configured) && configured.TryGetValue(nativeId,out var currentConfig) && original.Equals(currentConfig) &&
               owner.NativeConfiguredScoreFields?.TryGetValue(id,out var current)==true && ReferenceEquals(current,fire))return UnbuiltScore.KnownFalse;
            return UnbuiltScore.Unavailable;
        }
        NativeScoredFields? gateFields=null;long gateEpoch=0;
        if(id=="GuardDog")
        {
            if(owner.NativeConfiguredScoreFields?.TryGetValue(id,out gateFields)!=true || !gateFields.ConstructorPristine)return UnbuiltScore.Unavailable;
            gateEpoch=gateFields.Epoch;
        }
        bool GateCurrent()=>gateFields is {ConstructorPristine:true} && gateFields.Epoch==gateEpoch &&
            ReferenceEquals(owner.Engine.Robot,owner) && ReferenceEquals(owner.NativeBehaviorRuntime,runtime) &&
            owner.NativeConfiguredScoreFields?.TryGetValue(id,out var currentGate)==true && ReferenceEquals(currentGate,gateFields);
        if(id=="GuardDog" && winner.TryGetProperty("behaviorClass",out var guardClass) && guardClass.ValueKind==JsonValueKind.String && guardClass.GetString()=="GuardDog" && runtime.Manipulation is {} manipulation &&
           owner.TryGetNativeOriginOwner(owner.CurrentOriginId,out var origin) && origin?.Node is not null)
        {
            var filter=new BlockWorldFilter();filter.AllowedFamilies.Add(ObjectFamily.LightCube);
            filter.Predicates.Add(o=>o.ActiveId>-1);
            int count=manipulation.World.FindLocatedMatchingObjects(filter).Count;
            if(!ReferenceEquals(owner.Engine.Robot,owner) || !ReferenceEquals(owner.NativeBehaviorRuntime,runtime))return UnbuiltScore.Unavailable;
            if(count<3)return GateCurrent()?UnbuiltScore.KnownFalse:UnbuiltScore.Unavailable;
        }
        if(id is "PounceOnMotion_Socialize" or "FPPeekABoo")
        {
            if(id=="FPPeekABoo" && (runtime.Manipulation is not {} faceInput || faceInput.Vision.Faces.Count!=0))return UnbuiltScore.Unavailable;
            if(owner.NativeConfiguredScoreFields?.TryGetValue(id,out var fields)!=true || !fields.ConstructorPristine ||
               !fields.ExplicitFlatReload || fields.Value is not {} score || score.EmotionScorers.Count!=0 ||
               !float.IsFinite((float)score.FlatScore) || (float)score.FlatScore!=0f || !float.IsFinite((float)nowSec))return UnbuiltScore.Unavailable;
            bool FiniteGraph(Graph2d? graph)=>graph is null || graph.Nodes.All(n=>float.IsFinite((float)n.X)&&float.IsFinite((float)n.Y));
            if(!FiniteGraph(score.RepetitionPenalty) || !FiniteGraph(score.RunningPenalty) ||
               (score.BoredomMultiplier is {} multiplier && !float.IsFinite((float)multiplier)))return UnbuiltScore.Unavailable;
            long epoch=fields.Epoch;
            if(id=="PounceOnMotion_Socialize")
            {
                var config=winner;float factor=0.8f;
                if(config.TryGetProperty("boredomMultiplier",out var value) && value.ValueKind!=JsonValueKind.Null &&
                   (value.ValueKind!=JsonValueKind.Number || !value.TryGetSingle(out factor)))return UnbuiltScore.Unavailable;
                if(!float.IsFinite(factor))return UnbuiltScore.Unavailable;
            }
            if(fields.ConstructorPristine && fields.Epoch==epoch && ReferenceEquals(owner.Engine.Robot,owner) &&
               ReferenceEquals(owner.NativeBehaviorRuntime,runtime) && owner.NativeConfiguredScoreFields?.TryGetValue(id,out var current)==true && ReferenceEquals(current,fields))return UnbuiltScore.ScoringZero;
        }
        return UnbuiltScore.Unavailable;
    }

    public ScoringChooser(IReadOnlyList<ScoredBehaviorEntry> entries, IReadOnlyDictionary<string, IBehavior> bound, Graph2d? scoreBonusForCurrent = null, RepetitionPenalty? penalty = null, bool native=false)
    {
        _native=native;
        Entries = entries; ScoreBonusForCurrent = scoreBonusForCurrent; _penalty = penalty ?? new RepetitionPenalty();
        _bound = entries.Where(e => bound.ContainsKey(e.BehaviorId)).DistinctBy(e=>e.BehaviorId).ToDictionary(e => e.BehaviorId, e => bound[e.BehaviorId]);
        // Native map key is the bound behavior's ID byte (0x0060a2c4). Unknown host-only IDs retain input order.
        _evaluationEntries = (_native?entries.Where(e=>_bound.ContainsKey(e.BehaviorId)).DistinctBy(e=>e.BehaviorId):entries).OrderBy(e => _bound.TryGetValue(e.BehaviorId, out var b) &&
            Enum.TryParse<BehaviorID>(b.Id, out var id) && id.ToString() == b.Id ? (int)id : 256).ToList();
    }

    public BehaviorChooserType Type => BehaviorChooserType.Scoring;
    public IReadOnlyList<ScoredBehaviorEntry> Entries { get; }
    public Graph2d? ScoreBonusForCurrent { get; }
    public IReadOnlyList<string> BehaviorIds => Entries.Select(e => e.BehaviorId).ToList();
    public IReadOnlyList<string> Unbound => Entries.Where(e => !_bound.ContainsKey(e.BehaviorId)).Select(e => e.BehaviorId).ToList();
    public Random Random { get; set; } = new();
    internal Animation.EngineRandom? ContextRandom {get;set;}

    /// <summary>
    /// The <c>RandomGenerator::RandDbl</c> draw (0x0060a4a8) added to a non-running challenger's score.
    /// Null uses <see cref="Random"/>; a test can pin it.
    /// </summary>
    public Func<double>? RandomDraw { get; set; }

    /// <summary>
    /// Records a completed run in the shared repetition history. The manager already does this for every
    /// behaviour that reaches <c>BehaviorStopReason.Completed</c> — the engine's
    /// <c>StopWithoutImmediateRepetitionPenalty</c> exists precisely so an interrupted one is not penalised —
    /// so this is only for callers driving a chooser without a manager.
    /// </summary>
    public void Ran(string behaviorId, double nowSec) => _penalty.Ran(behaviorId, nowSec);

    /// <summary>
    /// <c>ScoringBSRunnableChooser::GetDesiredActiveBehavior</c> 0x0060a44a: evaluate every listed
    /// behaviour, skip a score &lt;= 0 (<c>vcmpe.f32 s0,#0</c> 0x0060a452; <c>ble</c> 0x0060a462), add the
    /// running-duration graph bonus to the running one (<c>GraphEvaluator2d::EvaluateY</c> 0x0060a474) and a
    /// <c>RandomGenerator::RandDbl</c> draw to a non-running challenger (0x0060a4a8), and keep the maximum.
    /// </summary>
    public ChooserDecision GetDesiredActiveBehavior(IBehavior? current, double currentRunningSec, BehaviorContext ctx, double nowSec)
    {
        var scores = new List<(string, double, string)>();
        IBehavior? best = null; double bestScore = 0; double currentScore = 0;
        foreach (var e in _evaluationEntries)
        {
            if (!_bound.TryGetValue(e.BehaviorId, out var b))
            {
                if(_ordinaryOwner is not null)
                {
                    var state=EvaluateUnbuilt(e.BehaviorId,ctx,nowSec);
                    if(state==UnbuiltScore.Unavailable)
                    {SteppedBehavior.ReportMissing("M8-003 configured scored owner/input unavailable for "+e.BehaviorId);return new ChooserDecision(null,"unavailable scored owner",scores){InputUnavailable=true};}
                    scores.Add((e.BehaviorId,0,state.ToString()));continue;
                }
                scores.Add((e.BehaviorId, 0, "not built")); continue;
            }
            if(_native && (b is not SteppedBehavior native || native.NativeScoring is null || !native.NativeScoringAvailable))
            {SteppedBehavior.ReportMissing("M8-003 actual scored owner unavailable for "+b.Id);return new ChooserDecision(null,"unavailable scored owner",scores){InputUnavailable=true};}
            bool running = _native ? b is SteppedBehavior { EngineRunning:true } : current is not null && current.Id == b.Id;
            double duration=running && _native && b is SteppedBehavior timed ? (float)nowSec-(float)timed.RunningPenaltyClockSec : currentRunningSec;
            var scoreInput=_native && b is SteppedBehavior scored ? scored.NativeScoring ?? e : e;
            // IBehavior +0x104: the running-score bonus IncreaseScoreWhileActing accumulates (M8-003).
            double runningBonus = b is SteppedBehavior sb ? sb.RunningScoreBonus : 0;
            var scoreResult=SteppedBehavior.EvaluateHostOperation(()=>(float)scoreInput.Evaluate(b, ctx, nowSec, _penalty.LastRunSec(b.Id), running ? duration : null, _penalty,
                                  runningBonus: runningBonus, penaltySuppressed: _penalty.IsSuppressed(b.Id, nowSec),
                                  runningClockSec: b is SteppedBehavior clocked ? clocked.RunningPenaltyClockSec : null));
            if(_native && !scoreResult.Available)return new ChooserDecision(null,"unavailable scored readiness",scores){InputUnavailable=true};
            float s=scoreResult.Value;
            if (s <= 0) { scores.Add((b.Id, s, "scored 0")); continue; }
            if (running)
            {
                // 0x0060a466 ldrb +0xa1; 0x0060a474 GraphEvaluator2d::EvaluateY(chooser+0x28, running duration);
                // 0x0060a486 vadd the 0.1 constant; 0x0060a48a floor at 0.01.
                s = (float)(ScoreBonusForCurrent?.EvaluateY(duration) ?? 0)+s; // 0x0060a474
                s += BitConverter.Int32BitsToSingle(0x3DCCCCCD);                  // 0x0060a486
                float floor=BitConverter.Int32BitsToSingle(0x3C23D70A);
                if (!(s > floor)) s = floor; // vmovgt retains floor on unordered.
                currentScore = s;
            }
            else
            {
                double limit=BitConverter.Int64BitsToDouble(0x3FB99999A0000000);
                double draw=RandomDraw is {} substitute?substitute()*limit:ContextRandom?.RandDbl(limit)??Random.NextDouble()*limit;
                s=(float)((double)s+draw); // exact widening, vadd.f64, then one vcvt.f32.f64.
            }
            scores.Add((b.Id, s, running ? "running" : ""));
            if (!(s <= bestScore)) { bestScore = s; best = b; } // BLE: unordered replaces, equality retains.
        }
        if (best is null) return new ChooserDecision(null, "no listed behaviour is runnable and wants to run", scores);
        if (current is not null && current.Id == best.Id) return new ChooserDecision(best, "already running", scores);
        return new ChooserDecision(best, current is null ? $"highest score {bestScore:F2}" : $"behavior '{best.Id}' has score of {bestScore:F2}, so is interrupting running behavior '{current.Id}' which scored {currentScore:F2}", scores);
    }
}

/// <summary>The engine's <c>StrictPriorityBSRunnableChooser</c>: the first runnable behaviour in the list (the running one stays while it is still the first runnable).</summary>
// fidelity: M8-013
public sealed class StrictPriorityChooser : IBehaviorChooser
{
    private readonly IReadOnlyDictionary<string, IBehavior> _bound;
    internal bool StopOnUnavailable { get; set; }
    private EngineRobot? _ordinaryOwner;
    private readonly EngineRobot? _configuredOwner;
    internal void EnableOrdinaryInterludeBoundary(EngineRobot owner)=>_ordinaryOwner=owner;
    private bool ProvedUnbuiltFalse(string id,BehaviorContext ctx)
    {
        var owner=_ordinaryOwner;
        if(owner is null || !ReferenceEquals(owner.Engine.Robot,owner) || owner.NativeBehaviorRuntime is not {} runtime ||
           !ReferenceEquals(ctx.Needs,owner.Engine.Needs) || owner.NativeConfiguredScoreFields?.TryGetValue(id,out var fields)!=true ||
           !fields.ConstructorPristine || owner.Engine.HostBehaviorMap is not {} configs ||
           !Enum.TryParse<BehaviorID>(id,false,out var nativeId) || !configs.TryGetValue(nativeId,out var config))return false;
        long epoch=fields.Epoch;bool result;
        if(id=="EarnedSparks")result=!owner.Engine.Needs.SparksRewardPending;
        else
        {
            var expected=id switch
            {
                "Needs_SevereLowPlayBored" or "Needs_SevereLowPlayRequest" => (NeedId.Play,NeedBracketId.Critical),
                "Needs_MildLowRepairRequest" => (NeedId.Repair,NeedBracketId.Warning),
                "Needs_MildLowEnergyRequest" => (NeedId.Energy,NeedBracketId.Warning),
                "Needs_MildLowPlayRequest" => (NeedId.Play,NeedBracketId.Warning),
                _ => ((NeedId)(-1),(NeedBracketId)(-1))
            };
            if((int)expected.Item1<0 || !config.TryGetProperty("need",out var need) || need.ValueKind!=JsonValueKind.String || need.GetString()!=expected.Item1.ToString() ||
               !config.TryGetProperty("needBracket",out var bracket) || bracket.ValueKind!=JsonValueKind.String || bracket.GetString()!=expected.Item2.ToString() ||
               (config.TryGetProperty("caresAboutExpressedState",out var cares) && cares.ValueKind!=JsonValueKind.False && cares.ValueKind!=JsonValueKind.Null))return false;
            result=!owner.Engine.Needs.State.IsNeedAtBracket(expected.Item1,expected.Item2);
        }
        return result && fields.ConstructorPristine && fields.Epoch==epoch && ReferenceEquals(owner.Engine.Robot,owner) &&
            ReferenceEquals(owner.NativeBehaviorRuntime,runtime) && ReferenceEquals(ctx.Needs,owner.Engine.Needs) &&
            owner.NativeConfiguredScoreFields?.TryGetValue(id,out var current)==true && ReferenceEquals(current,fields);
    }
    private readonly bool _native;
    public StrictPriorityChooser(IReadOnlyList<string> ids, IReadOnlyDictionary<string, IBehavior> bound, bool native=false) : this(ids,bound,native,null) { }
    internal StrictPriorityChooser(IReadOnlyList<string> ids,IReadOnlyDictionary<string,IBehavior> bound,bool native,EngineRobot? owner)
    { BehaviorIds = ids; _bound = native?bound.ToDictionary(e=>e.Key,e=>e.Value):bound;_native=native;_configuredOwner=owner; }
    public BehaviorChooserType Type => BehaviorChooserType.StrictPriority;
    public IReadOnlyList<string> BehaviorIds { get; }
    public IReadOnlyList<string> Unbound => BehaviorIds.Where(i => !_bound.ContainsKey(i)).ToList();

    public ChooserDecision GetDesiredActiveBehavior(IBehavior? current, double currentRunningSec, BehaviorContext ctx, double nowSec)
    {
        var scores = new List<(string, double, string)>();
        if(_native && _configuredOwner is {} owner && !ReferenceEquals(owner.Engine.Robot,owner))
        {SteppedBehavior.ReportMissing("M8-013 strict priority original configured owner unavailable");return new ChooserDecision(null,"unavailable owner",scores){InputUnavailable=true};}
        foreach (var id in BehaviorIds)
        {
            if (!_bound.TryGetValue(id, out var b))
            {
                if(_native && _configuredOwner?.Engine.HostBehaviorMap is {} configs && Enum.TryParse<BehaviorID>(id,false,out var nativeId) && nativeId.ToString()==id && !configs.ContainsKey(nativeId))
                {scores.Add((id,0,"native missing"));continue;}
                if(ProvedUnbuiltFalse(id,ctx)){scores.Add((id,0,"checked false"));continue;}
                scores.Add((id,0,"not built"));
                if(_native || StopOnUnavailable) { SteppedBehavior.ReportMissing("M8-013 strict priority reached unavailable readiness: "+id); return new ChooserDecision(null, "unavailable readiness", scores) { InputUnavailable=true }; }
                continue;
            }
            // NATIVE (StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior 0x0060B23E): the loop reads the
            // candidate's is-running flag (IBehavior +0xA1, the same byte IsRunnableBase logs "Behavior %s is
            // already running" from) at 0x0060B250 and selects it without calling IsRunnable at all. Only a
            // behaviour that is not running is asked whether it is runnable.
            bool running = _native || StopOnUnavailable ? b is SteppedBehavior { EngineRunning:true } : current is not null && current.Id == id;
            if (running) { scores.Add((id,1,"running"));return new ChooserDecision(b,"already running",scores); }
            bool runnable;
            if(StopOnUnavailable && b is SteppedBehavior stepped)
            {
                var evaluation=stepped.EvaluateHostReadiness(ctx);
                if(!evaluation.Available)return new ChooserDecision(null,"unavailable readiness",scores) { InputUnavailable=true };
                runnable=evaluation.Runnable;
            }
            else runnable=b.IsRunnable(ctx);
            if (runnable) { scores.Add((id, 1, "first runnable")); return new ChooserDecision(b, $"first runnable in priority order", scores); }
            scores.Add((id, 0, "not runnable"));
        }
        return new ChooserDecision(null, "nothing in the priority list is runnable", scores);
    }
}

/// <summary>
/// The robot's external interface as <c>SelectionBSRunnableChooser</c>'s constructor sees it (0x0060A87A..0x0060A884): the subscription seam for the
/// game-to-engine <c>MessageGameToEngine</c> tags 0x94 and 0x93. Canonical input uses the owned local message hub.
/// </summary>
// fidelity: M8-013
public interface IChooserExternalInterface
{
    /// <summary>Subscribe the chooser's <c>HandleExecuteBehavior</c> for one <c>MessageGameToEngine</c> union tag.</summary>
    void SubscribeChooserHandler(ushort tag, SelectionChooser chooser);
}

/// <summary>
/// The engine's <c>SelectionBSRunnableChooser</c> 0x0060ad64..0x0060ae6f. +0x2c is the behaviour named by
/// the last <c>ExecuteBehaviorByID</c>/<c>ByExecutableType</c> message (initially null), +0x34 is the
/// <c>Wait</c> behaviour (BehaviorID 0xb2, resolved once in the ctor), +0x3c is the message's
/// <c>numRuns</c> (constructor -1 = unlimited, one decrement per running-&gt;stopped edge) and +0x40 is a
/// byte latch holding the previous call's running state. The requested behaviour is returned while it is
/// running (<c>+0xa1</c>) or <c>IsRunnable</c> and its budget is not spent; otherwise the same rule is
/// applied to <c>Wait</c>, whose countdown block only runs when <c>+0x34 == +0x2c</c>; otherwise null.
/// </summary>
// fidelity: M8-013
public sealed class SelectionChooser : IBehaviorChooser, IDisposable
{
    private readonly IBehavior? _wait;
    private readonly IReadOnlyDictionary<string, IBehavior>? _bound;
    private IBehavior? _requested;   // +0x2c
    private int _numRuns = -1;       // +0x3c
    private bool _latch;             // +0x40
    private readonly List<RetainedSubscription> _subscriptions=new();
    private EngineRobot? _nativeOwner;
    private bool _retired;
    private bool _inputUnavailable;
    private long _selectionEpoch;

    /// <summary>The key <c>SetProcessEnabled</c> (0x0060AE84..0x0060AEEA) files the analyzer enable request under.</summary>
    public const string ProcessKey = "SelectionBSRunnableChooser";

    /// <summary>
    /// The constructor (0x0060A87C..0x0060A99E): when the robot has an external interface (<paramref name="external"/> non-null,
    /// 0x0060A87A..0x0060A884) it subscribes the one bound <c>HandleExecuteBehavior</c> to game-to-engine tag 0x94
    /// (<c>ExecuteBehaviorByID</c>, 0x0060A898..0x0060A8B4) and then 0x93 (<c>ExecuteBehaviorByExecutableType</c>, 0x0060A91C..0x0060A938);
    /// with none it subscribes nothing. Either way it then resolves <c>Wait</c> (0x0060A984..0x0060A99E). Canonical construction
    /// uses its original Robot's local message hub; this external interface remains a standalone test seam.
    /// </summary>
    public SelectionChooser(IReadOnlyDictionary<string, IBehavior>? bound = null, IChooserExternalInterface? external = null, EngineRobot? nativeOwner = null)
    {
        _bound = bound;
        _nativeOwner=nativeOwner;
        if (external is not null)
        {
            external.SubscribeChooserHandler(Cozmo.Protocol.ExecuteBehaviorByIDMessage.UnionTag, this);               // 0x0060a898..0x0060a8b4
            external.SubscribeChooserHandler(Cozmo.Protocol.ExecuteBehaviorByExecutableTypeMessage.UnionTag, this);   // 0x0060a91c..0x0060a938
        }
        if(nativeOwner?.HasExternalInterface==true)
        {
            foreach(ushort tag in new ushort[]{0x94,0x93})
                _subscriptions.Add(nativeOwner.Engine.AnimationMessages.SubscribeMessage(tag,false,payload=>
                {
                    using var owner=nativeOwner.Engine.EnterSynchronousOwner();
                    if(_retired || !ReferenceEquals(nativeOwner.Engine.Robot,nativeOwner))return;
                    if(payload is byte[] body && body.Length==5)HandleMessage(tag,body);
                    else SteppedBehavior.ReportMissing("M8-013 Selection input requires exactly five payload bytes");
                }));
        }
        // Native constructor subscribes first, then resolves Wait.
        _wait = bound is not null && bound.TryGetValue("Wait", out var w) ? w : null;
    }

    public BehaviorChooserType Type => BehaviorChooserType.Selection;
    public IReadOnlyList<string> BehaviorIds => _requested is null ? Array.Empty<string>() : new[] { _requested.Id };

    /// <summary>+0x2c: the behaviour the last ExecuteBehavior message named, or null.</summary>
    public IBehavior? Requested => _requested;
    /// <summary>+0x34: the Wait behaviour (BehaviorID 0xb2), or null when the bound set has none.</summary>
    public IBehavior? Wait => _wait;
    /// <summary>+0x3c: the remaining runs; -1 is unlimited.</summary>
    public int NumRuns => _numRuns;
    internal bool RunningLatch=>_latch;
    internal void OnSelected(){if(!_retired)SetProcessEnabled(_requested,true);}
    internal void OnDeselected(){if(!_retired)SetProcessEnabled(_requested,false);}
    public void Dispose(){if(_retired)return;_retired=true;++_selectionEpoch;_requested=null;foreach(var subscription in _subscriptions)subscription.Dispose();_subscriptions.Clear();_nativeOwner=null;}

    /// <summary>Log lines (the handler's warnings and the unknown-tag error).</summary>
    public event Action<string>? Log;
    public SourceLogSink? SourceLog { get; set; }

    /// <summary>
    /// <c>BehaviorManager::FindBehaviorByExecutableType</c> (0x005A418C / container 0x0059C836). Canonical construction binds
    /// the retained configured owner; standalone callers may supply their own finder.
    /// </summary>
    public Func<byte, IBehavior?>? FindBehaviorByExecutableType { get; set; }

    /// <summary>
    /// The analyzer enable request <c>SetProcessEnabled</c> 0x0060AE84..0x0060AEEA makes under <see cref="ProcessKey"/>
    /// (<c>AddEnableRequest</c> 0x4B1DA0 / <c>RemoveEnableRequest</c> 0x4B1DAC): (behaviour, key, enabled). FreeplayStack binds the owned analyzer;
    /// when unset, a handoff that reaches a behaviour with a process reports MISSING once and makes no request. MISSING: M8-013.
    /// </summary>
    public Action<IBehavior, string, bool>? SetAnalyzerEnabled { get; set; }

    /// <summary>
    /// Whether the behaviour's process field (+0x1C, an EProcess id, 0x005BD7D4) is nonzero. The default reads RequiredProcessId and makes no
    /// request and reports MISSING once when a handoff is attempted for a non-stepped behaviour.
    /// </summary>
    public Func<IBehavior, bool> HasProcessField { get; set; } = DefaultHasProcessField;

    private static readonly Func<IBehavior, bool> DefaultHasProcessField = b => b is SteppedBehavior { RequiredProcessId: not 0 };

    private bool _reportedTypeNameMissing, _reportedProcessMissing, _reportedSinkMissing, _reportedFinderMissing;

    /// <summary>
    /// <c>SetProcessEnabled(behaviour, enabled)</c> 0x0060AE84..0x0060AEEA: only a non-null behaviour whose process field is non-null
    /// changes the analyzer's enable request, filed under <see cref="ProcessKey"/>.
    /// </summary>
    private void SetProcessEnabled(IBehavior? behavior, bool enabled)
    {
        if (behavior is null) return;
        if (!_reportedProcessMissing && ReferenceEquals(HasProcessField, DefaultHasProcessField) && behavior is not SteppedBehavior)
        {
            _reportedProcessMissing = true;
            Log?.Invoke("MISSING: M8-013: the process field (+0x1C, an EProcess id) of a non-stepped behaviour cannot be determined here; SetProcessEnabled is decided by HasProcessField");
        }
        if (!HasProcessField(behavior)) return;
        if (SetAnalyzerEnabled is not { } sink)
        {
            if (!_reportedSinkMissing) { _reportedSinkMissing = true; SteppedBehavior.ReportMissing("M8-013: SetProcessEnabled 0x0060AE84 needs the owned AIInformationAnalyzer request sink; standalone chooser has no owner; no request made"); }
            return;
        }
        sink(behavior, ProcessKey, enabled);
    }

    /// <summary>
    /// The tail <c>HandleExecuteBehavior</c> shares for every outcome (0x0060ABDC..0x0060AC44): when the selected pointer differs from the
    /// current +0x2C, <c>SetProcessEnabled(old, false)</c> and <c>SetProcessEnabled(new, true)</c> (0x0060ABDC..0x0060AC24); then the shared
    /// pointer at +0x2C/+0x30 is replaced (0x0060AC2C..0x0060AC44).
    /// </summary>
    private void Select(IBehavior? selected)
    {
        long epoch=++_selectionEpoch;
        if (!ReferenceEquals(selected, _requested))
        {
            SetProcessEnabled(_requested, false);
            if(_retired || epoch!=_selectionEpoch)return;
            SetProcessEnabled(selected, true);
            if(_retired || epoch!=_selectionEpoch)return;
        }
        _requested = selected;
    }

    /// <summary>
    /// <c>HandleExecuteBehavior</c> 0x0060AA58, tag 0x94 (<c>ExecuteBehaviorByID</c>, 0x0060AA6E..0x0060AA88 / 0x0060AB4E..0x0060AB7A):
    /// <c>FindBehaviorByID(payload[0])</c>, <c>numRuns</c> stored at +0x3C whether or not the lookup succeeds. A hit logs the Info line
    /// "selecting behavior name '%s'" (event <c>SelectionBSRunnableChooser.HandleExecuteBehaviorByName.SelectBehavior</c>); a miss warns
    /// "Unknown behavior %s" with the <c>BehaviorIDToString</c> name (event <c>...HandleExecuteBehaviorByName.UnknownBehavior</c>) and selects null.
    /// </summary>
    public void HandleExecuteBehavior(Cozmo.Protocol.ExecuteBehaviorByIDMessage message)
    {
        if(_retired)return;
        long epoch=++_selectionEpoch;
        // FindBehaviorByID: the container map keyed by BehaviorID (the enum is a byte); an undefined id is a miss.
        IBehavior? found = null;
        bool defined = Enum.IsDefined(typeof(BehaviorID), message.BehaviorID);
        string name = defined ? ((BehaviorID)message.BehaviorID).ToString() : message.BehaviorID.ToString();
        if (defined && _bound is not null) _bound.TryGetValue(name, out found);
        if(found is null && defined && _nativeOwner?.Engine.HostBehaviorMap?.ContainsKey((BehaviorID)message.BehaviorID)==true)
        { _nativeOwner.InvalidateConfiguredScoreHistory(name);SteppedBehavior.MarkReadinessUnavailable();SteppedBehavior.ReportMissing("M8-013 configured behavior owner "+name+" is unbuilt");return; }
        _numRuns = message.NumRuns;                                                        // 0x0060AA88 / 0x0060AB7A
        if (found is null) SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Warning, null, "SelectionBSRunnableChooser.HandleExecuteBehaviorByName.UnknownBehavior", $"Unknown behavior {name}");
        else SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Info, "Unnamed", "SelectionBSRunnableChooser.HandleExecuteBehaviorByName.SelectBehavior", $"selecting behavior name '{name}'");
        if(_retired || epoch!=_selectionEpoch)return;
        Select(found);
    }

    /// <summary>
    /// <c>HandleExecuteBehavior</c> 0x0060AA58, tag 0x93 (<c>ExecuteBehaviorByExecutableType</c>, 0x0060AAAE..0x0060AAC8):
    /// <c>FindBehaviorByExecutableType(payload[0])</c>, <c>numRuns</c> stored at +0x3C whether or not the lookup succeeds. A hit logs the Info line
    /// "selecting behavior '%s' exec type '%s'" (event <c>...ExecuteBehaviorByExecutableType.SelectBehavior</c>), a miss warns
    /// "No behavior for exec type %s" (event <c>...ExecuteBehaviorByExecutableType.NoBehavior</c>) and selects null. The engine prints the type through
    /// <c>EnumToString(ExecutableBehaviorType)</c> (PLT 0x4B6CA8, 0x0060AAEC / 0x0060ABA0) and the hit line's first %s is the behaviour's name string (+0x40).
    /// Canonical construction uses the recovered native name table; standalone test callers retain the numeric diagnostic.
    /// </summary>
    public void HandleExecuteBehavior(Cozmo.Protocol.ExecuteBehaviorByExecutableTypeMessage message)
    {
        if(_retired)return;
        long epoch=++_selectionEpoch;
        if (FindBehaviorByExecutableType is not { } find)
        {
            if (!_reportedFinderMissing) { _reportedFinderMissing = true; Log?.Invoke("MISSING: M8-013: BehaviorManager::FindBehaviorByExecutableType's body is not in the inventory; the executable-type message is skipped"); }
            return;
        }
        IBehavior? found;
        try { found=find(message.ExecutableBehaviorType); }
        catch(NativeChooserUnavailable error) { if(error.BehaviorId is {} id)_nativeOwner?.InvalidateConfiguredScoreHistory(id);SteppedBehavior.MarkReadinessUnavailable();SteppedBehavior.ReportMissing("M8-013 "+error.Message);return; }
        if(_retired || epoch!=_selectionEpoch)return;
        _numRuns = message.NumRuns;                                                        // 0x0060AAC8
        string typeName;
        if(_nativeOwner is not null)typeName=message.ExecutableBehaviorType<ExecutableBehaviorNames.Names.Length?ExecutableBehaviorNames.Names[message.ExecutableBehaviorType]:"(null)";
        else
        {
            if (!_reportedTypeNameMissing) { _reportedTypeNameMissing = true; Log?.Invoke("MISSING: M8-013: standalone executable type-name input unavailable; number logged"); }
            typeName=message.ExecutableBehaviorType.ToString();
        }
        if(_retired || epoch!=_selectionEpoch)return;
        if (found is null) SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Warning, null, "SelectionBSRunnableChooser.ExecuteBehaviorByExecutableType.NoBehavior", $"No behavior for exec type {typeName}");
        else SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Info, "Unnamed", "SelectionBSRunnableChooser.ExecuteBehaviorByExecutableType.SelectBehavior", $"selecting behavior '{found.Id}' exec type '{typeName}'");
        if(_retired || epoch!=_selectionEpoch)return;
        Select(found);
    }

    /// <summary>
    /// The subscription callback: dispatch on the game-to-engine union tag. 0x94 and 0x93 are the two handled above; any other tag logs
    /// <c>SelectionBSRunnableChooser.HandleMessage.UnknownTag</c> (0x0060AB28..0x0060AB7A) and selects null. The engine's error/debug-break
    /// path after that log is not modelled; <c>numRuns</c> is not written (there is no payload).
    /// </summary>
    public void HandleMessage(ushort tag, ReadOnlyMemory<byte> payload)
    {
        if(_retired)return;
        switch (tag)
        {
            case Cozmo.Protocol.ExecuteBehaviorByIDMessage.UnionTag:
                HandleExecuteBehavior(Cozmo.Protocol.ExecuteBehaviorByIDMessage.UnpackBody(payload)); break;
            case Cozmo.Protocol.ExecuteBehaviorByExecutableTypeMessage.UnionTag:
                HandleExecuteBehavior(Cozmo.Protocol.ExecuteBehaviorByExecutableTypeMessage.UnpackBody(payload)); break;
            default:
                long epoch=++_selectionEpoch;
                SourceLogDispatch.GetSink(SourceLog, Log)?.Invoke(EngineLogLevel.Error, null, "SelectionBSRunnableChooser.HandleMessage.UnknownTag", "got a tag we didn't subscribe to");   // sErrorF 0x0060AB3A..0x0060AB74
                if(!_retired && epoch==_selectionEpoch)Select(null);
                break;
        }
    }

    /// <summary>
    /// The handler's effect for a caller that already holds the resolved behaviour: +0x3c = numRuns, then the same handoff and store as the
    /// handler (<see cref="Select"/>). The messages themselves reach the chooser through <see cref="HandleMessage"/>.
    /// </summary>
    public void RequestBehavior(IBehavior? behavior, int numRuns = -1)
    {
        if(_retired)return;
        _numRuns = numRuns;
        Select(behavior);
    }

    public ChooserDecision GetDesiredActiveBehavior(IBehavior? current, double currentRunningSec, BehaviorContext ctx, double nowSec)
    {
        if(_retired)return new ChooserDecision(null,"chooser retired",Array.Empty<(string,double,string)>()){InputUnavailable=true};
        _inputUnavailable=false;
        var scores = new List<(string, double, string)>();
        if (Candidate(_requested, current, ctx, scores, updateCountdown: true))
            return new ChooserDecision(_requested, "the requested behaviour", scores);
        if(_inputUnavailable)return new ChooserDecision(null,"unavailable readiness",scores){InputUnavailable=true};
        if (Candidate(_wait, current, ctx, scores, updateCountdown: ReferenceEquals(_wait, _requested)))
            return new ChooserDecision(_wait, "the Wait fallback", scores);
        if(_inputUnavailable)return new ChooserDecision(null,"unavailable readiness",scores){InputUnavailable=true};
        return new ChooserDecision(null, "no requested behaviour and no Wait are runnable", scores);
    }

    /// <summary>
    /// One candidate's rule (0x0060ad6e..0x0060add8 for +0x2c, 0x0060ade2..0x0060ae4a for +0x34):
    /// running or runnable, then the countdown/latch block when <paramref name="updateCountdown"/>, then
    /// return it when the flag is 1.
    /// </summary>
    private bool Candidate(IBehavior? candidate, IBehavior? current, BehaviorContext ctx,
                           List<(string, double, string)> scores, bool updateCountdown)
    {
        if(_nativeOwner is not null && candidate is not null && candidate is not SteppedBehavior)
        { _inputUnavailable=true;SteppedBehavior.ReportMissing("M8-013 Selection actual running/readiness owner unavailable for "+candidate.Id);return false; }
        bool running = candidate is SteppedBehavior stepped ? stepped.EngineRunning : candidate is not null && ReferenceEquals(current,candidate);
        bool runnable=running;
        if(!running && candidate is SteppedBehavior nativeCandidate && _nativeOwner is not null)
        {
            var readiness=nativeCandidate.EvaluateHostReadiness(ctx);
            if(!readiness.Available){_inputUnavailable=true;return false;}
            runnable=readiness.Runnable;
        }
        else if(!running && candidate is not null)runnable=candidate.IsRunnable(ctx);
        if (updateCountdown)
        {
            if (_numRuns == 0) { runnable = false; goto finishedCountdown; }       // count zero preserves latch
            else if (_numRuns >= 1 && !running && _latch)                          // 0x0060ada8..0x0060adbc
            {
                _numRuns--;                                                        // 0x0060adbe/0x0060adc0
                if (_numRuns == 0) runnable = false;                               // 0x0060adc2..0x0060adc6
            }
            _latch = running;                                                      // 0x0060adc8 / 0x0060ae3a
        }
        finishedCountdown:
        if(candidate is null)return false;
        scores.Add((candidate.Id, runnable ? 1 : 0, running ? "running" : runnable ? "selected" : "not runnable"));
        return runnable;
    }
}

/// <summary>
/// The engine's <c>IActivityStrategy</c> (constructor 0x005B4EDA..0x005B5260 reads <c>activityCanEndDurationSecs</c>,
/// <c>activityShouldEndDurationSecs</c> (−1: never), <c>cooldownBaseSecs</c>, <c>cooldownRandomnessSecs</c>,
/// <c>startInCooldown</c>, <c>requiredRecentOnTreadsEventSecs</c>, <c>requiredMinStartMoodScore</c> +
/// <c>startMoodScorer</c>, <c>featureGate</c>, <c>wantsToRunStrategyConfig</c>). <c>WantsToStart</c> (0x005B529C):
/// not in cooldown (randomised per <c>RandomizeCooldown</c>), mood score at least the minimum, an on-treads
/// event recent enough, the feature enabled, and the subclass's rule; <c>WantsToEnd</c> (0x005B5444): past the
/// should-end duration, or the subclass's rule. Subclasses: Simple (nothing more), Needs (an
/// <c>InNeedsBracket</c> wants-to-run strategy: start while the need is in the bracket, end when it leaves),
/// SevereNeedTransition (<c>ExpressNeedsTransition</c>: start when the need has just become Critical and the
/// get-in has not been expressed; ends at once), Pyramid (the unlock, at least three located cubes and no
/// pyramid yet, a 300 s (0x43960000) cooldown), FPPlayWithHumans (<c>RequestGameComponent::IdentifyNextGameTypeToRequest</c>
/// finds a game: the app's, so never here), Spark (the app requested the spark: never here).
/// </summary>
// fidelity: M15-003
public sealed class ActivityStrategy : IDisposable
{
    // Test-only collaborator for exercising distinct native picker calls; never set by production configuration.
    internal Func<bool>? WantsToEndForTest;
    private IDisposable? _needsSubscription, _objectiveSubscription;
    internal Func<double,double>? Draw;
    internal Func<float>? TickDuration;
    internal Func<bool?>? PyramidUnlocked;
    internal Func<int?>? PyramidCandidates, PyramidBases, PyramidFull;
    private float _nextRoll;
    private float _defaultBase, _defaultRandomness;
    private bool _rollLatch, _built;
    private bool _hasCurrentCooldown;
    private double _currentCooldownSec;
    internal string? HigherType, HigherNeed, HigherBracket;
    // fidelity: M15-098
    internal void BindNative(EngineRobot owner)
    {
        _defaultBase=(float)CooldownBaseSec; _defaultRandomness=(float)CooldownRandomnessSec;
        Draw=x=>owner.Engine.ContextRandom.RandDbl(x);
        TickDuration=()=> (float)owner.Engine.Timer.DeltaSeconds;
        PyramidUnlocked=()=>owner.Progression is {} p && p.TryIsUnlocked(3,out bool unlocked) ? unlocked : null;
        // fidelity: M15-097
        if(Type=="Pyramid")
        {
            // The runtime is assigned after tree construction; resolve the actual owner on each query.
            ManipulationSystem? Actual()
            {
                var runtime=owner.NativeBehaviorRuntime;
                var m=runtime?.Manipulation;
                if(!ReferenceEquals(owner.Engine.Robot,owner) || m is null ||
                   !ReferenceEquals(m.Robot.LocalizationWorld,m.World) ||
                   !ReferenceEquals(m.World.NativeConfigurations,runtime!.Configurations) ||
                   !runtime.Configurations.InputsAvailable)
                {
                    SteppedBehavior.ReportMissing("M15-097/M12-049 actual pyramid world or native configuration producer unavailable");
                    return null;
                }
                return m;
            }
            int? Count(Func<ManipulationSystem,int> query)
            {
                var m=Actual(); if(m is null)return null;
                try{return query(m);}
                catch(NativeConfigurationUnavailable error)
                {SteppedBehavior.ReportMissing("M15-097/M12-049 native configuration unavailable: "+error.Message);return null;}
            }
            // 0x005B4994..4A40: default current-origin filter, allowed families 2 and 1.
            PyramidCandidates=()=>Count(m=>
            {
                var filter=new BlockWorldFilter();
                filter.AllowedFamilies.Add(ObjectFamily.LightCube);filter.AllowedFamilies.Add(ObjectFamily.Block);
                return m.World.FindLocatedMatchingObjects(filter).Count;
            });
            // 0x005B4AA6/4AC2 copy every strong entry into the respective weak vector.
            PyramidBases=()=>Count(m=>m.NativePyramidBases.Count);
            PyramidFull=()=>Count(m=>m.NativePyramids.Count);
        }
        if(!owner.HasExternalInterface)return;
        if(Type=="NeedBasedCooldown") _needsSubscription=owner.Engine.AnimationMessages.SubscribeMessage(0x62,true,ReceiveNeeds);
        if(Type=="Pyramid") _objectiveSubscription=owner.Engine.AnimationMessages.SubscribeMessage(0x8B,true,p=>{if(p is uint value && value==3)_built=true;});
    }
    // fidelity: M15-098
    private void ReceiveNeeds(object? payload)
    {
        if(payload is not IReadOnlyList<uint> words || Need is not {} n || (uint)n>=3)
        {SteppedBehavior.ReportMissing("M15-098 needs level projection or needId unavailable");return;}
        float level=BitConverter.Int32BitsToSingle(unchecked((int)words[(int)n]));
        float baseValue=NeedCooldownGraph is {} graph ? (float)graph.EvaluateY(level) : _defaultBase;
        float randomValue=NeedCooldownRandomnessGraph is {} randomGraph ? (float)randomGraph.EvaluateY(level) : _defaultRandomness;
        SetCooldown(baseValue,randomValue);
    }
    public void Dispose(){_needsSubscription?.Dispose();_needsSubscription=null;_objectiveSubscription?.Dispose();_objectiveSubscription=null;}
    public string Type { get; init; } = "Simple";
    public double CanEndDurationSec { get; init; } = -1;
    public double ShouldEndDurationSec { get; init; } = 60;
    public double CooldownBaseSec { get; set; } = -1;
    public double CooldownRandomnessSec { get; set; }
    public bool StartInCooldown { get; init; }
    public double RequiredRecentOnTreadsEventSec { get; init; } = -1;
    /// <summary>
    /// <c>requiredMinStartMoodScore</c>, the constructor's <c>+0x30</c> default <c>-1</c> (row 25): a score
    /// below <c>-1</c> is impossible, so an absent key is no minimum.
    /// </summary>
    public double RequiredMinStartMoodScore { get; init; } = -1;
    public IReadOnlyList<(EmotionType Emotion, Graph2d Graph)> StartMoodScorer { get; init; } = Array.Empty<(EmotionType, Graph2d)>();
    /// <summary>
    /// The config's <c>featureGate</c>: the name of a feature that has to be enabled before the activity
    /// may start. <c>WantsToStart</c> 0x005B529C tests the flag at strategy+0x38 first and, when it is
    /// set, asks <c>CozmoFeatureGate::IsFeatureEnabled(featureType)</c> and refuses outright if the answer
    /// is no (0x005B52A8..0x005B52BC). Two shipped activities use it, both naming Singing.
    /// </summary>
    public string? FeatureGate { get; init; }

    public string? WantsToRunStrategyType { get; init; }
    public NeedId? Need { get; init; }
    public NeedBracketId? NeedBracket { get; init; }
    public string? HigherPriorityStrategy { get; init; }
    /// <summary>
    /// <c>ActivityStrategyNeedBasedCooldown</c> (0x005B4298: "needId", "needCooldownGraph",
    /// "needCooldownRandomnessGraph"; the graphs are evaluated at the need's level on <c>NeedsState</c>): the
    /// cooldown is the graph at the level plus a random share of the randomness graph (Singing).
    /// </summary>
    public Graph2d? NeedCooldownGraph { get; init; }
    public Graph2d? NeedCooldownRandomnessGraph { get; init; }
    /// <summary>The need levels the need-based cooldown reads (set by the loader / the stack).</summary>
    public Func<NeedId, double>? NeedLevels { get; set; }

    public double? LastEndedSec { get; private set; }
    /// <summary>
    /// <c>IActivityStrategy</c> +0x20: the cooldown actually in force. The constructor sets it to
    /// <c>cooldownBaseSecs</c> (0x005B5004) and <c>WantsToStart</c> re-randomises it (inline
    /// <c>RandomizeCooldown</c>, 0x005B5336..0x005B5360) every time the cooldown check passes — not when the
    /// activity ends.
    /// </summary>
    public double CurrentCooldownSec
    {
        get => _hasCurrentCooldown ? _currentCooldownSec : (float)CooldownBaseSec;
        private set { _currentCooldownSec=value; _hasCurrentCooldown=true; }
    }
    public Random Random { get; set; } = new();

    /// <summary>The engine's float epsilon in these comparisons (0x3727C5AC).</summary>
    public static readonly float Epsilon = BitConverter.Int32BitsToSingle(0x3727C5AC);

    public static ActivityStrategy FromJson(JsonElement e)
    {
        double D(string k, double d) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : d;
        var scorers = new List<(EmotionType, Graph2d)>();
        if (e.TryGetProperty("startMoodScorer", out var sms))
            foreach (var s in sms.EnumerateArray())
                if (Enum.TryParse<EmotionType>(s.GetProperty("emotionType").GetString(), true, out var et) && s.TryGetProperty("scoreGraph", out var sg) && Graph2d.FromJson(sg) is { } g) scorers.Add((et, g));
        string? wtr = null; NeedId? need = null; NeedBracketId? bracket = null;
        if (e.TryGetProperty("wantsToRunStrategyConfig", out var w))
        {
            wtr = w.TryGetProperty("strategyType", out var st) ? st.GetString() : null;
            if (w.TryGetProperty("need", out var n) && Enum.TryParse<NeedId>(n.GetString(), true, out var nid)) need = nid;
            if (w.TryGetProperty("needBracket", out var nb) && Enum.TryParse<NeedBracketId>(nb.GetString(), true, out var b)) bracket = b;
        }
        string? higher = e.TryGetProperty("higherPriorityStrategyConfig", out var hp) && hp.TryGetProperty("need", out var hn) ? hn.GetString() : null;
        if (e.TryGetProperty("needId", out var nidEl) && Enum.TryParse<NeedId>(nidEl.GetString(), true, out var nid2)) need ??= nid2;
        Graph2d? cd = e.TryGetProperty("needCooldownGraph", out var cdg) ? Graph2d.FromJson(cdg) : null;
        Graph2d? cdr = e.TryGetProperty("needCooldownRandomnessGraph", out var cdrg) ? Graph2d.FromJson(cdrg) : null;
        var result = new ActivityStrategy
        {
            Type = e.TryGetProperty("type", out var t) ? t.GetString() ?? "Simple" : "Simple",
            // JsonTools::GetValueOptional<float> 0x004FA580 overwrites only when the member exists, so an
            // absent key keeps the IActivityStrategy constructor default (row 25): canEnd -1, shouldEnd 60,
            // cooldownBase -1, cooldownRandomness 0, requiredRecentOnTreads -1, requiredMinStartMoodScore -1.
            // fidelity: M15-100
            CanEndDurationSec = D("activityCanEndDurationSecs", -1), ShouldEndDurationSec = e.TryGetProperty("type",out var sparkType) && sparkType.GetString()=="Spark" ? float.MaxValue : D("activityShouldEndDurationSecs", 60),
            CooldownBaseSec = D("cooldownBaseSecs", -1), CooldownRandomnessSec = D("cooldownRandomnessSecs", 0),
            StartInCooldown = e.TryGetProperty("startInCooldown", out var sic) && sic.ValueKind == JsonValueKind.True,
            RequiredRecentOnTreadsEventSec = D("requiredRecentOnTreadsEventSecs", -1), RequiredMinStartMoodScore = D("requiredMinStartMoodScore", -1),
            StartMoodScorer = scorers, WantsToRunStrategyType = wtr, Need = need, NeedBracket = bracket, HigherPriorityStrategy = higher,
            FeatureGate = e.TryGetProperty("featureGate", out var fg) ? fg.GetString() : null,
            NeedCooldownGraph = cd, NeedCooldownRandomnessGraph = cdr,
        };
        if(e.TryGetProperty("higherPriorityStrategyConfig",out var hcfg) && hcfg.ValueKind==JsonValueKind.Object)
        {
            result.HigherType=hcfg.TryGetProperty("strategyType",out var ht)?ht.GetString():null;
            result.HigherNeed=hcfg.TryGetProperty("need",out var hneed)?hneed.GetString():null;
            result.HigherBracket=hcfg.TryGetProperty("needBracket",out var hb)?hb.GetString():null;
        }
        return result;
    }

    /// <summary>The activity ended: only the end time is recorded (the engine re-randomises on the next start).</summary>
    public void OnEnded(double nowSec) => LastEndedSec = (float)nowSec;

    /// <summary><c>RandomizeCooldown</c> (0x005B5408): base plus a random share of the randomness.</summary>
    public void RandomizeCooldown()
    {
        float baseSec=(float)CooldownBaseSec, randomness=(float)CooldownRandomnessSec;
        double draw=Draw is {} native ? native(randomness) : Random.NextDouble()*randomness;
        CurrentCooldownSec=(float)((double)baseSec+draw);
    }

    /// <summary>The cooldown in force, the constructor's <c>cooldownBaseSecs</c> until the first randomisation.</summary>
    public double EffectiveCooldownSec => CurrentCooldownSec;

    /// <summary>
    /// <c>IActivityStrategy::SetCooldown</c> 0x005B54E4: <c>+0x1c</c> (the base) and <c>+0x20</c> (the current
    /// cooldown) become <paramref name="baseSec"/>, and <c>+0x24</c> (the randomness) becomes
    /// <paramref name="randomnessSec"/>. This is the explicit cooldown setter beside
    /// <see cref="RandomizeCooldown"/>.
    /// </summary>
    // fidelity: M15-003, M15-098
    public void SetCooldown(double baseSec, double randomnessSec)
    {
        CooldownBaseSec = (float)baseSec;
        CurrentCooldownSec = (float)baseSec;
        CooldownRandomnessSec = (float)randomnessSec;
    }

    /// <summary>
    /// The flat cooldown an activity gets when its last run lasted no longer than two ticks: 3 seconds
    /// (<c>vmov.f32 s0, #3.0</c> at 0x005B5312).
    /// </summary>
    public const double ShortRunCooldownSec = 3.0;

    /// <summary>
    /// One basestation tick, the engine's <c>GetTimeSinceLastTickInSeconds</c>. The comparison at
    /// 0x005B5308 is against twice it, so an activity that ran for at most two ticks counts as having
    /// ended the moment it started.
    /// </summary>
    public const double TickSec = 1.0 / 30;

    /// <summary>When the activity last started, which with the end time gives the length of the last run.</summary>
    public double? LastStartedSec { get; private set; }

    /// <summary>The activity started: the engine keeps this at +0x54 beside the end time at +0x58.</summary>
    public void OnStarted(double nowSec) => LastStartedSec = (float)nowSec;

    /// <summary>
    /// <c>WantsToStart</c>'s cooldown test (0x005B52C8..0x005B5334). It applies while the cooldown is above
    /// zero and either the activity has ended before or <c>startInCooldown</c> is set, and it measures from
    /// the last end time - which is 0 for an activity that has never run, so <c>startInCooldown</c> holds
    /// the activity back for the first cooldown of the session.
    ///
    /// The two times it is given are the activity's own, read together at the call site
    /// (<c>ldrd r3, r2, [r1, #0x54]</c> at 0x005B26FE): the start at +0x54 and the end at +0x58. Their
    /// difference is how long the last run lasted, and when that is positive but no more than two
    /// basestation ticks (0x005B52EA..0x005B5316) the engine throws the configured cooldown away and uses
    /// a flat 3 seconds instead - an activity that ended as soon as it started waits three seconds before
    /// it may try again.
    /// </summary>
    // fidelity: M15-005
    public bool InCooldown(double nowSec)
    {
        float cooldown=(float)EffectiveCooldownSec, ended=(float)(LastEndedSec??0);
        if (!(cooldown>Epsilon) || (!(ended>Epsilon) && !StartInCooldown)) return false;
        float ran=ended-(float)(LastStartedSec??0), tick=TickDuration?.Invoke()??(float)TickSec;
        if(ran>0 && ran<=tick+tick)cooldown=3f;
        return (float)nowSec < ended+cooldown;
    }

    public bool WantsToStart(FreeplayInputs inputs, double nowSec, out string reason)
    {
        // fidelity: M15-007
        // The engine asks the feature gate first (0x005B52A8), before it looks at the cooldown.
        if (FeatureGate is { } gate && inputs.Features is { } features && !features.IsEnabled(gate))
        {
            reason = $"the {gate} feature is off";
            return false;
        }
        if (InCooldown(nowSec)) { reason = $"in cooldown ({EffectiveCooldownSec:F0} s)"; return false; }
        float ended=(float)(LastEndedSec??0);
        if((float)EffectiveCooldownSec>Epsilon && (ended>Epsilon || StartInCooldown))RandomizeCooldown();
        if (StartMoodScorer.Count > 0)
        {
            double score = inputs.Mood is { } mood ? StartMoodScorer.Sum(s => s.Graph.EvaluateY(mood[s.Emotion])) : 0;
            if (!((float)score >= (float)RequiredMinStartMoodScore-Epsilon)) { reason = $"mood score {score:F2} below {RequiredMinStartMoodScore:F2}"; return false; }
        }
        float window=(float)RequiredRecentOnTreadsEventSec;
        if(window>Epsilon)
        {
            float t=(float)(inputs.LastOnTreadsEventSec??0), selected=(float)(LastStartedSec??0), now=(float)nowSec;
            if(!(t>Epsilon) || t-Epsilon<=selected || window-Epsilon<=now-t)
            {reason="no recent on-treads event";return false;}
        }
        switch (Type)
        {
            case "Needs":
                if (Need is not { } n || NeedBracket is not { } b || inputs.Needs is null) { reason = "no needs state"; return false; }
                if (!inputs.Needs.State.IsNeedAtBracket(n, b)) { reason = $"{n} not {b}"; return false; }
                reason = $"{n} is {b}"; return true;
            case "SevereNeedTransition":
                if (Need is not { } sn || inputs.Needs is null) { reason = "no needs state"; return false; }
                // Native activity entry 0x005B4D88 delegates IWantsToRunStrategy; the expression predicate is 0x006136D8.
                if (!inputs.Needs.State.IsNeedAtBracket(sn, NeedBracketId.Critical)) { reason = $"{sn} not Critical"; return false; }
                if (inputs.Needs.SevereNeeds.Selected==(int)sn) { reason = $"{sn} severe state already expressed"; return false; }
                reason = $"{sn} just became Critical"; return true;
            case "Pyramid":
                bool? unlocked=PyramidUnlocked?.Invoke();
                if(unlocked is null){SteppedBehavior.ReportMissing("M15-097 progression unlock3 unavailable");reason="pyramid unlock unavailable";return false;}
                if(!unlocked.Value){reason="pyramid locked";return false;}
                int? candidates=PyramidCandidates?.Invoke();
                if(candidates is null)SteppedBehavior.ReportMissing("M15-097/M12-049 pyramid located filter unavailable");
                float now=(float)nowSec;
                if(now>_nextRoll){double draw=Draw is {} native?native(1d):Random.NextDouble();_rollLatch=draw<0.4;_nextRoll=now+300f;}
                int? bases=PyramidBases?.Invoke(), full=PyramidFull?.Invoke();
                if(bases is null || full is null){SteppedBehavior.ReportMissing("M15-097/M12-049 pyramid weak caches unavailable");reason="pyramid caches unavailable";return false;}
                if(candidates is null && full==0 && bases==0 && _rollLatch){SteppedBehavior.ReportMissing("M15-097/M12-049 pyramid located filter unavailable");reason="pyramid candidates unavailable";return false;}
                bool allowed=full==0 && (bases>0 || (_rollLatch && candidates>=3));
                if(allowed)_built=false;
                reason=allowed?"pyramid eligible":"pyramid ineligible";return allowed;
            // fidelity: M15-096
            case "PlayWithHumans" or "FPPlayWithHumans":
                reason = "the game request component is the app's"; return false;
            // fidelity: M15-100
            case "Spark":
                reason = "spark strategy starts"; return true;
            default:
                reason = "wants to start"; return true;
        }
    }

    /// <summary>
    /// <c>IActivityStrategy::WantsToEnd(robot, startTime)</c> (0x005B5444), in its three steps:
    /// <c>activityCanEndDurationSecs</c> is a floor — before it the activity does not want to end whatever the
    /// subclass thinks (0x005B5450..0x005B548A); <c>activityShouldEndDurationSecs</c> is a ceiling — past it it
    /// does (0x005B548C..0x005B54BC); in between, the subclass's <c>WantsToEndInternal</c> decides
    /// (the vtable +0x0C tail call at 0x005B54C0).
    /// </summary>
    public bool WantsToEnd(FreeplayInputs inputs, double runningSec, out string reason, double? absoluteNow=null)
    {
        if (WantsToEndForTest is { } supplied) { reason="supplied test predicate"; return supplied(); }
        float selected=(float)(LastStartedSec??0), now=absoluteNow is {} t?(float)t:(float)((float)runningSec+selected);
        float can=(float)CanEndDurationSec, should=(float)ShouldEndDurationSec;
        if(can>Epsilon && (can+selected)-Epsilon>now){reason="minimum duration";return false;}
        if(should>Epsilon && (should+selected)+Epsilon<now){reason=$"ran {runningSec:F0} s, should end after {ShouldEndDurationSec:F0}";return true;}
        switch (Type)
        {
            case "Needs":
                if(Need is not {} n || NeedBracket is not {} b || inputs.Needs is null){reason="missing primary needs strategy";return true;}
                if(HigherType is not null)
                {
                    if(HigherType!="InNeedsBracket" || !Enum.TryParse<NeedId>(HigherNeed,true,out var hn) || !Enum.TryParse<NeedBracketId>(HigherBracket,true,out var hb))
                    {SteppedBehavior.ReportMissing("M15-099 higher priority strategy recipient unavailable");reason="higher strategy unavailable";return false;}
                    if(inputs.Needs.State.IsNeedAtBracket(hn,hb))
                    {if(inputs.Needs.SevereNeeds.Selected!=3)inputs.Needs.SevereNeeds.Clear();reason="higher priority";return true;}
                }
                bool atBracket=inputs.Needs.State.IsNeedAtBracket(n,b);
                reason=atBracket?$"{n} remains {b}":$"{n} left {b}";return !atBracket;
            case "SevereNeedTransition":
                if(Need is not {} sn || inputs.Needs is null){reason="missing primary transition strategy";return true;}
                reason="transition predicate";return !(inputs.Needs.State.IsNeedAtBracket(sn,NeedBracketId.Critical) && inputs.Needs.SevereNeeds.Selected!=(int)sn);
            case "Pyramid": reason="pyramid objective";return _built;
        }
        // fidelity: M15-100 -- Spark's internal end predicate returns false.
        reason = "keeps running"; return false;
    }
}

/// <summary>What the strategies and the freeplay chooser read about the world each tick.</summary>
public sealed class FreeplayInputs
{
    public NeedsManager? Needs { get; init; }
    public MoodState? Mood { get; init; }
    public double? LastOnTreadsEventSec { get; set; }
    public bool PyramidBuilt { get; set; }
    public bool FaceKnown { get; set; }
    public bool OnCharger { get; set; }
    public string? RequestedSpark { get; set; }

    /// <summary>
    /// The feature gates, from <c>config/features.json</c>. Null means no gate is consulted, which is
    /// what a stack with no config loaded can honestly say.
    /// </summary>
    public FeatureGates? Features { get; init; }
}

/// <summary>
/// The engine's <c>CozmoFeatureGate</c> as the activities use it: a name from
/// <c>config/features.json</c> - the path the binary holds at 0x00BE8449 - and whether it is enabled.
/// <c>IsFeatureEnabled</c> 0x006A679C turns the enum into its name, lowercases it and looks it up, so the
/// comparison is case-insensitive here too.
///
/// The shipped file lists seventeen features, of which Invalid, SparksGatherCubes and Bouncer are off and
/// the rest, Singing among them, are on.
/// </summary>
public sealed class FeatureGates
{
    private readonly Dictionary<string, bool> _byName = new(StringComparer.OrdinalIgnoreCase);

    public FeatureGates(IEnumerable<(string Name, bool Enabled)> features)
    {
        foreach (var (name, enabled) in features) _byName[name] = enabled;
    }

    /// <summary>Whether a feature is on. A name the file does not list is off, as the engine's lookup is.</summary>
    public bool IsEnabled(string feature) => _byName.TryGetValue(feature, out var on) && on;

    /// <summary>The names the file carried, for diagnostics.</summary>
    public IReadOnlyCollection<string> Names => _byName.Keys;

    /// <summary>Reads <c>config/features.json</c> under an unpacked OBB; null when it is not there.</summary>
    public static FeatureGates? Load(string obbRoot)
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(obbRoot, "assets", "cozmo_resources", "config", "features.json"),
                     Path.Combine(obbRoot, "config", "features.json"),
                     Path.Combine(obbRoot, "features.json"),
                 })
        {
            if (!File.Exists(candidate)) continue;
            using var doc = JsonDocument.Parse(File.ReadAllText(candidate),
                                               new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var list = new List<(string, bool)>();
            foreach (var f in doc.RootElement.EnumerateArray())
                if (f.TryGetProperty("feature", out var n) && n.GetString() is { } name)
                    list.Add((name, f.TryGetProperty("enabled", out var e) && e.ValueKind == JsonValueKind.True));
            return new FeatureGates(list);
        }
        return null;
    }
}

/// <summary>
/// The engine's <c>IActivity</c> (<c>ActivityBehaviorsOnly</c> and the typed ones read the same base config:
/// <c>activityID</c>, <c>activityType</c>, <c>behaviorChooser</c>, <c>interludeBehaviorChooser</c>,
/// <c>activityStrategy</c>, <c>driveStartAnimTrigger</c> / <c>driveLoopAnimTrigger</c> / <c>driveEndAnimTrigger</c>,
/// <c>idleAnimTrigger</c>, <c>infoAnalyzerProcess</c>, <c>requireSpark</c>, <c>needsActionID</c>).
/// <c>GetDesiredActiveBehavior</c> asks the chooser; between two behaviours an interlude from the interlude
/// chooser runs when one is runnable ("Activity %s is inserting interlude %s between behaviors %s and %s").
/// <c>OnSelected</c> stores time, starts the strategy, updates public activity state, then gates driving and idle calls;
/// <c>OnDeselected</c> removes it and starts the strategy's cooldown.
/// </summary>
public sealed class Activity
{
    internal Action? NativePublicStateSelected;
    internal NeedsManager? NativeNeeds;
    internal IBehavior? CachedInterlude;
    // IActivity::GetDesiredActiveBehavior wrapper for the checked GatherCubes delegate.
    internal ChooserDecision NativeGatherDesired(IBehavior? current,double running,BehaviorContext context,double now,Func<bool> currentOwner)
    {
        if(GatherOwner is null)return new ChooserDecision(null,"delegate unavailable",Array.Empty<(string,double,string)>()){InputUnavailable=true};
        if(CachedInterlude is {} cached && ReferenceEquals(cached,current))return new ChooserDecision(cached,"cached interlude",Array.Empty<(string,double,string)>());
        var previous=CachedInterlude;CachedInterlude=null;
        var decision=Chooser?.GetDesiredActiveBehavior(current,running,context,now) ?? new ChooserDecision(null,"no chooser",Array.Empty<(string,double,string)>());
        if(!currentOwner())return decision with {InputUnavailable=true};
        if(decision.InputUnavailable){CachedInterlude=previous;return decision;}
        if(InterludeChooser is {} interlude&&!ReferenceEquals(decision.Behavior,current))
        {
            var next=interlude.GetDesiredActiveBehavior(decision.Behavior,0,context,now);
            if(!currentOwner())return next with {InputUnavailable=true};
            if(next.InputUnavailable){CachedInterlude=previous;return next;}
            CachedInterlude=next.Behavior;
            if(CachedInterlude is {} selected)return next with {Behavior=selected};
        }
        return decision;
    }
    internal NativeActivityObjectiveOwner? ObjectiveOwner;
    internal NativeSparkedActivity? SparkedOwner;
    // Common OnDeselected's derived virtual hook, after common cleanup (005B3658).
    internal Action<double>? NativeDeselectedHook;
    internal NativeGatherCubesActivity? GatherOwner;
    internal Action? NativeCommonDrivingPush, NativeCommonDrivingRemove;
    internal CozmoRobot? NativeDrivingFacade;
    internal bool GuardNativeLifecycle;
    private long _nativeLifecycleEpoch;
    private ScopedAcquisition? _nativeDrivingLease;
    // fidelity: M15-055, M15-056 — explicit native delegate and checked Feeding owner; enclosing lifecycle remains M15-061.
    internal Func<EngineRobot,uint>? NativeUpdate20;
    internal FeedingDisconnectUnit? FeedingDisconnect;
    public required string Id { get; init; }
    public string Type { get; init; } = "BehaviorsOnly";
    public int Priority { get; init; }
    public required ActivityStrategy Strategy { get; init; }
    public IBehaviorChooser? Chooser { get; init; }
    public IBehaviorChooser? InterludeChooser { get; init; }
    public AnimationTrigger? DriveStartAnim { get; init; }
    public AnimationTrigger? DriveLoopAnim { get; init; }
    public AnimationTrigger? DriveEndAnim { get; init; }
    public AnimationTrigger? IdleAnim { get; init; }
    private string? _requireSpark;
    internal int RequiredSparkId { get; private set; } = 0x55;
    public string? RequireSpark { get => _requireSpark; init { _requireSpark = value; RequiredSparkId = value is null ? 0x55 : unchecked((int)NativeUnlockNames.FromBytes(System.Text.Encoding.UTF8.GetBytes(value))); } }
    // fidelity: M11-079 — IActivity+0x48, read 0x005B2D26..0x005B2D7C.
    public int InfoAnalyzerProcess { get; init; }
    internal AIInformationAnalyzer? Analyzer { get; set; }

    /// <summary>
    /// The config's <c>needsActionID</c>, kept because the shipped activities carry it - but nothing reads
    /// it. <c>IActivity::ReadConfig</c> parses it into <c>IActivity+0x1C</c> (0x005B2A9C) and no code in the
    /// build loads that word again: the needs actions are reported by the behaviours
    /// (<see cref="BehaviorNeedsActions"/>) and, in one case, by <c>ActivityGatherCubes</c> naming
    /// <c>GatherCubes</c> itself.
    /// </summary>
    public string? NeedsActionId { get; init; }
    public IReadOnlyList<Activity> SubActivities { get; init; } = Array.Empty<Activity>();
    /// <summary>Freeplay's <c>desiredActivityNames</c>: face+cube, face only, cube only, neither.</summary>
    public (string FaceAndCube, string FaceOnly, string CubeOnly, string None)? DesiredActivityNames { get; init; }

    public double? SelectedAtSec { get; private set; }
    // fidelity: M5-063, M15-006 — caller triplet/lock inputs remain unread; no unmatched push/remove is fabricated.
    public void OnSelected(double nowSec, Action<string>? missing = null)
    {
        long epoch=++_nativeLifecycleEpoch;
        bool Current()=>!GuardNativeLifecycle || epoch==_nativeLifecycleEpoch;
        SelectedAtSec = nowSec; Strategy.OnStarted(nowSec);
        if(Chooser is SelectionChooser selection)selection.OnSelected();
        if(!Current())return;
        if(!Current())return;
        if(NativePublicStateSelected is {} publish)publish();
        else ReportActivityMissing("PublicStateBroadcaster::UpdateActivity 0x005B3154", missing);
        if(!Current())return;
        if (DriveStartAnim is { } drive && (int)drive != 0x23F) { if(NativeCommonDrivingPush is {} push)push(); else ReportDrivingMissing("PushDrivingAnimations 0x005B319A", missing); }
        if(!Current())return;
        if (IdleAnim is { } idle && (int)idle != 0x23F) ReportActivityMissing("SmartPushIdleAnimation 0x005B31BC", missing);
        if(!Current())return;
        SetAnalyzerRequest(true);if(!Current())return;
        FeedingDisconnect?.OnSelected();if(!Current())return;
        ObjectiveOwner?.OnSelectedInternal();if(!Current())return;
        SparkedOwner?.Selected();if(!Current())return;
        GatherOwner?.Selected();
    }
    public void OnDeselected(double nowSec, Action<string>? missing = null, NeedsManager? needs = null)
    {
        long epoch=++_nativeLifecycleEpoch;
        if(Chooser is SelectionChooser selection){SelectedAtSec=null;Strategy.OnEnded(nowSec);selection.OnDeselected();if(GuardNativeLifecycle && epoch!=_nativeLifecycleEpoch)return;}
        bool Current()=>!GuardNativeLifecycle || epoch==_nativeLifecycleEpoch;
        var oldSpark=GuardNativeLifecycle?SparkedOwner?.DetachGather():null;
        var oldDriving=GuardNativeLifecycle?_nativeDrivingLease:null;
        if(GuardNativeLifecycle)_nativeDrivingLease=null;
        void ReleaseDriving()
        {
            if(oldDriving is null)return;
            var lease=oldDriving;oldDriving=null;lease.Cancel();
            var facade=NativeDrivingFacade??throw new InvalidOperationException("MISSING: actual activity driving owner unavailable during retirement");
            facade.Animations.DrivingAnimations.RemoveOwned(lease);
        }
        try
        {
            SelectedAtSec = null; Strategy.OnEnded(nowSec);
            if(!Current())return;
            if (IdleAnim is { } idle && (int)idle != 0x23F) ReportActivityMissing("RemoveIdleAnimation 0x005B340C", missing);
            if(!Current())return;
            if (DriveStartAnim is { } drive && (int)drive != 0x23F)
            {
                if(GuardNativeLifecycle)
                {
                    var facade=NativeDrivingFacade??throw new InvalidOperationException("MISSING: actual activity driving owner unavailable during retirement");
                    var result=facade.Animations.DrivingAnimations.RemoveScoped(Id,Current);
                    if(result.Completed)
                    {
                        if(ReferenceEquals(result.Owner,oldDriving))oldDriving=null;
                        oldSpark?.CommonRemoved(result.Owner);
                    }
                }
                else if(NativeCommonDrivingRemove is {} remove)remove();
                else ReportDrivingMissing("RemoveDrivingAnimations 0x005B3450", missing);
            }
            if(!Current())return;
            SetAnalyzerRequest(false);if(!Current())return;
            CachedInterlude = null;
            var reward = needs ?? NativeNeeds;
            if (reward is { SparksRewardPending: true })
            {
                reward.SparksRewardCommunicatedToUser();
                SteppedBehavior.ReportMissing("M15-071 SparksRewardCommunicatedToUser 0x00696E64/0x00696E90: pending flag and existing queued notification delivered; native reward amount +0x3DC and raw FreeplaySparksAwarded broadcast are unbuilt");
            }
            if(!Current())return;
            FeedingDisconnect?.OnDeselected();if(!Current())return;
            if(oldSpark is not null)SparkedOwner!.DeselectedGather(oldSpark);
            else SparkedOwner?.Deselected();
            if(!Current())return;
            GatherOwner?.Deselected();
            if(Current())NativeDeselectedHook?.Invoke(nowSec);
            if(Current())oldDriving=null; // Native calls completed; do not add an identity removal.
        }
        finally { ReleaseDriving();oldSpark?.ReleaseAll(); }
    }
    internal void RetireHostResources()
    {
        ++_nativeLifecycleEpoch;
        // Sparked's destructor releases owned references, not selected-state outputs.
        if(SparkedOwner is not null)return;
        var oldSpark=SparkedOwner?.DetachGather();
        var oldDriving=_nativeDrivingLease;_nativeDrivingLease=null;
        try { oldSpark?.ReleaseAll(); }
        finally
        {
            if(oldDriving is not null)
            {
                oldDriving.Cancel();
                var facade=NativeDrivingFacade??throw new InvalidOperationException("MISSING: actual activity driving owner unavailable during retirement");
                facade.Animations.DrivingAnimations.RemoveOwned(oldDriving);
            }
        }
    }
    internal void PushOwnedDriving(EngineRobot owner,DrivingAnimations animations)
    {
        var lease=new ScopedAcquisition();_nativeDrivingLease=lease;
        NativeDrivingFacade!.Animations.DrivingAnimations.PushOwned(animations,Id,lease);
    }
    internal void RemoveOwnedDriving(EngineRobot owner)
    {
        var lease=_nativeDrivingLease;_nativeDrivingLease=null;
        if(lease is null)return;lease.Cancel();NativeDrivingFacade!.Animations.DrivingAnimations.RemoveOwned(lease);
    }
    private void SetAnalyzerRequest(bool enable)
    {
        if (InfoAnalyzerProcess == 0) return;
        if (Analyzer is null) { SteppedBehavior.ReportMissing("M11-079 IActivity analyzer request 0x005B31F6/0x005B349A has no AI owner"); return; }
        if (enable) Analyzer.AddEnableRequest(InfoAnalyzerProcess, Id);
        else Analyzer.RemoveEnableRequest(InfoAnalyzerProcess, Id);
    }
    private void ReportActivityMissing(string call, Action<string>? sink)
    {
        string text=$"M15-006 IActivity::{call} for {Id} is unbuilt";
        if(sink is not null)sink("MISSING: "+text);else SteppedBehavior.ReportMissing(text);
    }
    private void ReportDrivingMissing(string call, Action<string>? sink)
    {
        string text = $"M5-063 / M15-006 IActivity::{call} caller triplet/lock lifecycle for {Id} is unbuilt";
        if (sink is not null) sink("MISSING: " + text); else SteppedBehavior.ReportMissing(text);
    }
    public double RunningSec(double nowSec) => SelectedAtSec is { } s ? nowSec - s : 0;

    /// <summary>Every behaviour id this activity (and its sub-activities) can name.</summary>
    public IEnumerable<string> AllBehaviorIds() =>
        (Chooser?.BehaviorIds ?? Array.Empty<string>()).Concat(InterludeChooser?.BehaviorIds ?? Array.Empty<string>()).Concat(SubActivities.SelectMany(a => a.AllBehaviorIds()));

    public override string ToString() => $"{Id} ({Type}, {Strategy.Type}{(Chooser is null ? "" : ", " + Chooser.Type)})";
}

/// <summary>
/// Loads the shipped activity tree: <c>behaviorSystem/activities_config.json</c> lists the top-level activities
/// (Selection, MeetCozmo, Feeding, Freeplay), and Freeplay's <c>subActivities</c> name the activities under
/// <c>activities/**</c> by <c>activityID</c>. <c>ActivityFreeplay::CreateFromConfig</c> parses
/// <c>activityPriority</c> and discards it (0x005AD63C/0x005AD640), so the child order is the JSON array
/// order, not a priority sort. Behaviour ids are bound to the
/// implemented set; ids with no implementation are kept in the choosers as "not built" so the tree is the shipped
/// one, not a trimmed copy.
/// </summary>
// fidelity: M15-013
public static class ActivityTreeLoader
{
    public static string ActivitiesDir(string obbRoot) => Path.Combine(obbRoot, "assets", "cozmo_resources", "config", "engine", "behaviorSystem");

    public static IReadOnlyList<Activity> Load(string obbRoot, IReadOnlyDictionary<string, IBehavior> bound, RepetitionPenalty? penalty = null, Random? random = null)
        =>LoadDirectory(ActivitiesDir(obbRoot),bound,penalty,random,null);
    internal static IReadOnlyList<Activity> LoadResources(string resources, IReadOnlyDictionary<string,IBehavior> bound,
        RepetitionPenalty penalty,EngineRobot owner,Random? random=null)
        =>LoadDirectory(Path.Combine(resources,"config","engine","behaviorSystem"),bound,penalty,random,owner);
    private static IReadOnlyList<Activity> LoadDirectory(string dir,IReadOnlyDictionary<string,IBehavior> bound,RepetitionPenalty? penalty,Random? random,EngineRobot? owner)
    {
        var byId = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var docs = new List<JsonDocument>();
        foreach (var f in Directory.EnumerateFiles(Path.Combine(dir, "activities"), "*.json", SearchOption.AllDirectories))
        {
            var doc = JsonDocument.Parse(File.ReadAllText(f), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            docs.Add(doc);
            if (doc.RootElement.TryGetProperty("activityID", out var id) && id.GetString() is { } s) byId[s] = doc.RootElement;
        }
        var top = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "activities_config.json")), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        docs.Add(top);
        var result = new List<Activity>();
        foreach (var a in top.RootElement.EnumerateArray()) result.Add(Build(a, byId, bound, penalty, random, 0,owner));
        return result;
    }

    private static Activity Build(JsonElement e, Dictionary<string, JsonElement> byId, IReadOnlyDictionary<string, IBehavior> bound, RepetitionPenalty? penalty, Random? random, int priority,EngineRobot? owner)
    {
        string id = e.GetProperty("activityID").GetString()!;
        var subs = new List<Activity>();
        AnimationTrigger? Trig(string k) => e.TryGetProperty(k, out var t) && Enum.TryParse<AnimationTrigger>(t.GetString(), out var tr) ? tr : null;
        (string, string, string, string)? desired = null;
        if (e.TryGetProperty("desiredActivityNames", out var dn))
            desired = (dn.GetProperty("faceAndCubeActivityName").GetString()!, dn.GetProperty("faceOnlyActivityName").GetString()!, dn.GetProperty("cubeOnlyActivityName").GetString()!, dn.GetProperty("noFaceNoCubeActivityName").GetString()!);
        var normalChooser=e.TryGetProperty("behaviorChooser",out var bc) ? BuildChooser(bc,bound,penalty,random,owner) : e.TryGetProperty("universalChooser",out var uc) ? BuildChooser(uc,bound,penalty,random,owner) : null;
        if(owner is not null && normalChooser is ScoringChooser ordinary)ordinary.EnableOrdinaryUnbuiltBoundary(owner);
        var interludeChooser=e.TryGetProperty("interludeBehaviorChooser",out var ic) ? BuildChooser(ic,bound,penalty,random,owner) : null;
        if(owner is not null && (id is "Socialize" or "PlayAlone") && interludeChooser is StrictPriorityChooser ordinaryInterlude)
        {ordinaryInterlude.EnableOrdinaryInterludeBoundary(owner);ordinaryInterlude.StopOnUnavailable=true;}
        var strategy = e.TryGetProperty("activityStrategy", out var st) ? ActivityStrategy.FromJson(st) : new ActivityStrategy();
        // fidelity: M15-059
        if (e.TryGetProperty("activityType",out var sparkedType) && sparkedType.GetString()=="Sparked" && !(owner is not null&&bound.TryGetValue("PlayArbitraryAnim",out var nativeArbitrary)&&nativeArbitrary is NativePlayArbitraryAnimBehavior))
            SteppedBehavior.ReportMissing("M15-059 "+id+": actual original Robot or retained configured PlayArbitraryAnim 3D is unavailable; Sparked owner not constructed.");
        if(id=="SparksGatherCubes")
            SteppedBehavior.ReportMissing("M15-008 behavior-owned needs action reporters PeekABoo, TrackLaser and GuardDog remain unbuilt");
        var activity=new Activity
        {
            InfoAnalyzerProcess = e.TryGetProperty("infoAnalyzerProcess", out var ip) && ip.GetString() == "CalculateInterestingRegions" ? 1 : 0,
            Id = id, Type = e.TryGetProperty("activityType", out var ty) ? ty.GetString() ?? "BehaviorsOnly" : "BehaviorsOnly", Priority = priority, Strategy = strategy,
            Chooser = normalChooser,
            InterludeChooser = interludeChooser,
            DriveStartAnim = Trig("driveStartAnimTrigger"), DriveLoopAnim = Trig("driveLoopAnimTrigger"), DriveEndAnim = Trig("driveEndAnimTrigger"), IdleAnim = Trig("idleAnimTrigger"),
            RequireSpark = e.TryGetProperty("requireSpark", out var rs) ? rs.GetString() : null,
            NeedsActionId = e.TryGetProperty("needsActionID", out var na) ? na.GetString() : null,
            SubActivities = subs, DesiredActivityNames = desired,
        };
        if (random is not null) strategy.Random = random;
        if(owner is not null)strategy.BindNative(owner);
        if (e.TryGetProperty("subActivities", out var sa))
            foreach (var s in sa.EnumerateArray())
            {
                string sid=s.GetProperty("activityID").GetString()!;
                if(byId.TryGetValue(sid,out var se))subs.Add(Build(se,byId,bound,penalty,random,0,owner));
                else subs.Add(new Activity { Id=sid,Type="Missing",Priority=0,Strategy=new ActivityStrategy { Type="Missing" } });
            }
        if(owner is not null)
        {
            byte? nativeId = ActivityIdNames.TryId(id, out var mapped) ? mapped : null;
            if(nativeId is {} value)activity.NativePublicStateSelected=()=>owner.BehaviorPublicState.UpdateActivity(value);
            activity.NativeNeeds = owner.Engine.Needs;
            if (id == "Hiking" && activity.DriveStartAnim is {} hikingStart && activity.DriveLoopAnim is {} hikingLoop && activity.DriveEndAnim is {} hikingEnd)
            {
                activity.GuardNativeLifecycle = true;
                activity.NativeDrivingFacade = owner.BehaviorManagerOwner!.ContextRobot;
                activity.NativeCommonDrivingPush = () => activity.PushOwnedDriving(owner, new(hikingStart, hikingLoop, hikingEnd));
                activity.NativeCommonDrivingRemove = () => activity.RemoveOwnedDriving(owner);
            }
            if(activity.Type=="BehaviorsOnly")activity.NativeUpdate20=_=>0; // vtable10252F8 -> 005A7373.
            foreach(var chooser in new[]{activity.Chooser,activity.InterludeChooser})
                if(chooser is ScoringChooser scoring)scoring.ContextRandom=owner.Engine.ContextRandom;
            // C7.1: the checked Look invalid41 filter requires BOTH its root and
            // the actual chooser entry scoring to omit the objective override.
            foreach(string chooserKey in new[]{"behaviorChooser","universalChooser","interludeBehaviorChooser"})
                if(e.TryGetProperty(chooserKey,out var rawChooser) && rawChooser.TryGetProperty("behaviors",out var rawEntries)
                    &&rawEntries.ValueKind==JsonValueKind.Array)
                    foreach(var rawEntry in rawEntries.EnumerateArray())
                        if(rawEntry.ValueKind==JsonValueKind.Object &&rawEntry.TryGetProperty("behaviorID",out var rawId)
                            &&rawId.ValueKind==JsonValueKind.String &&bound.TryGetValue(rawId.GetString()!,out var candidate)
                            &&candidate is NativeLookForFaceAndCubeBehavior look &&rawEntry.TryGetProperty("scoring",out var rawScoring))
                            look.CheckObjectiveInput(rawScoring,"activity "+id+" "+chooserKey);
            if(activity.Type=="Sparked"&&bound.TryGetValue("PlayArbitraryAnim",out var arbitrary)&&arbitrary is NativePlayArbitraryAnimBehavior)
            {
                Activity? nested=null;
                if(id=="SparksGatherCubes" && e.TryGetProperty("subActivityDelegate",out var nestedConfig))
                {
                    nested=Build(nestedConfig,byId,bound,penalty,random,0,owner);
                    if(nested.Chooser is StrictPriorityChooser strict)strict.StopOnUnavailable=true;
                }
                // P3: unavailable earlier Spark workout candidates are not sourced false gates.
                if(activity.Chooser is StrictPriorityChooser workoutChooser)workoutChooser.StopOnUnavailable=true;
                Activity? CreateSparkDelegate(JsonElement config)
                {
                    if(!config.TryGetProperty("activityType",out var type) || type.ValueKind!=JsonValueKind.String || type.GetString()!="GatherCubes")return null;
                    var created=Build(config,byId,bound,penalty,random,0,owner);
                    if(created.Chooser is StrictPriorityChooser strict)strict.StopOnUnavailable=true;
                    return created.GatherOwner is not null?created:null;
                }
                activity.SparkedOwner=new(activity,owner,e,bound,nested,CreateSparkDelegate);
                activity.GuardNativeLifecycle=true;activity.NativeDrivingFacade=owner.BehaviorManagerOwner!.ContextRobot;
                if(id=="SparksGatherCubes" && activity.DriveStartAnim is {} start && activity.DriveLoopAnim is {} loop && activity.DriveEndAnim is {} end)
                {
                    activity.NativeCommonDrivingPush=()=>activity.PushOwnedDriving(owner,new(start,loop,end));
                    activity.NativeCommonDrivingRemove=()=>activity.RemoveOwnedDriving(owner);
                }
            }
            else if(id=="GatherCubes" && activity.Type=="GatherCubes") { activity.GuardNativeLifecycle=true;activity.GatherOwner=new(owner); }
            else if(activity.Type=="Socialize")activity.ObjectiveOwner=new(activity,owner,e,bound);
            if(activity.Type=="Socialize") { activity.NativeUpdate20=_=>0;activity.GuardNativeLifecycle=true;activity.NativeDrivingFacade=owner.BehaviorManagerOwner!.ContextRobot; }
            if(activity.Type=="NeedsExpression" && (id is "NeedsSevereLowEnergy" or "NeedsSevereLowRepair" or "NeedsSevereLowPlayGetIn"))
            {activity.NativeUpdate20=_=>0;activity.GuardNativeLifecycle=true;activity.NativeDrivingFacade=owner.BehaviorManagerOwner!.ContextRobot;if(activity.Chooser is StrictPriorityChooser needsChooser)needsChooser.StopOnUnavailable=true;}
        }
        return activity;
    }

    public static IBehaviorChooser BuildChooser(JsonElement c, IReadOnlyDictionary<string, IBehavior> bound, RepetitionPenalty? penalty, Random? random, EngineRobot? owner=null)
    {
        string type = c.TryGetProperty("type", out var t) ? t.GetString() ?? "Scoring" : "Scoring";
        switch (type)
        {
            case "StrictPriority":
                return new StrictPriorityChooser(c.TryGetProperty("behaviors", out var b) ? b.EnumerateArray().Select(x => x.GetString()!).ToList() : new List<string>(), bound,owner is not null,owner);
            case "Selection":
                return new SelectionChooser(bound,nativeOwner:owner);
            default:
                var entries = c.TryGetProperty("behaviors", out var bs) ? bs.EnumerateArray().Select(ScoredBehaviorEntry.FromJson).ToList() : new List<ScoredBehaviorEntry>();
                if(owner is not null && c.TryGetProperty("behaviors",out var nativeEntries))
                    foreach(var entry in nativeEntries.EnumerateArray())
                    {
                        var id=entry.GetProperty("behaviorID").GetString()!;
                        var scoring=entry.TryGetProperty("scoring",out var scored)?scored:default;
                        if(bound.TryGetValue(id,out var candidate))
                        {
                            if(candidate is SteppedBehavior stepped)stepped.ReadNativeScoring(scoring);
                            else SteppedBehavior.ReportMissing("M8-003 native shared scoring owner unavailable for "+id);
                        }
                        else if(owner.NativeConfiguredScoreFields?.TryGetValue(id,out var fields)==true)fields.Reload(id,scoring);
                    }
                var bonus = c.TryGetProperty("scoreBonusForCurrentBehavior", out var sb) ? Graph2d.FromJson(sb) : null;
                var sc = new ScoringChooser(entries, bound, bonus, penalty,owner is not null);
                if (random is not null) sc.Random = random;
                return sc;
        }
    }
}
