using System.Text.Json;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

/// <summary>A piecewise-linear graph (<c>Anki::Util::GraphEvaluator2d</c>) over sorted nodes.</summary>
public sealed record Graph2d(IReadOnlyList<(double X, double Y)> Nodes)
{
    public double EvaluateY(double x)
    {
        if (Nodes.Count == 0) return 0;
        if (x <= Nodes[0].X) return Nodes[0].Y;
        for (int i = 1; i < Nodes.Count; i++)
            if (x <= Nodes[i].X)
            {
                double dx = Nodes[i].X - Nodes[i - 1].X;
                return dx <= 0 ? Nodes[i].Y : Nodes[i - 1].Y + (x - Nodes[i - 1].X) / dx * (Nodes[i].Y - Nodes[i - 1].Y);
            }
        return Nodes[^1].Y;
    }

    public static Graph2d? FromJson(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("nodes", out var nodes)) return null;
        var list = nodes.EnumerateArray().Select(n => (n.GetProperty("x").GetDouble(), n.GetProperty("y").GetDouble())).ToList();
        return list.Count == 0 ? null : new Graph2d(list);
    }
}

/// <summary>
/// One behaviour's scoring entry in a Scoring chooser (<c>IBehavior::ReadFromScoredJson</c>, 0x005BC4xx:
/// <c>flatScore</c>, <c>emotionScorers</c>, <c>repetitionPenalty</c> (a graph over seconds since the behaviour
/// last ran, 0..1), <c>runningPenalty</c> (a graph over the running duration), <c>boredomMultiplier</c>,
/// <c>considerThisHasRunForBehaviorObjective</c>).
/// </summary>
public sealed record ScoredBehaviorEntry(string BehaviorId, double FlatScore, Graph2d? RepetitionPenalty, Graph2d? RunningPenalty, double? BoredomMultiplier,
                                         IReadOnlyList<(EmotionType Emotion, Graph2d Graph)> EmotionScorers)
{
    /// <summary>
    /// <c>IBehavior::EvaluateScore</c> (0x005BEF60): (flat score + emotion scorers) × running penalty (when running)
    /// × repetition penalty (from the last run), 0 when not runnable.
    /// </summary>
    public double Evaluate(IBehavior b, BehaviorContext ctx, double nowSec, double? lastRunSec, double? runningSec, RepetitionPenalty? defaultPenalty)
    {
        if (!b.IsRunnable(ctx)) return 0;
        double score = FlatScore;
        if (ctx.Mood is { } mood) foreach (var (e, g) in EmotionScorers) score += g.EvaluateY(mood[e]);
        if (runningSec is { } r && RunningPenalty is { } rp) score *= rp.EvaluateY(r);
        if (lastRunSec is { } last)
        {
            double since = nowSec - last;
            score *= RepetitionPenalty is { } g2 ? g2.EvaluateY(since) : defaultPenalty?.For(BehaviorId, nowSec) ?? 1.0;
        }
        return score;
    }

    public static ScoredBehaviorEntry FromJson(JsonElement e)
    {
        string id = e.GetProperty("behaviorID").GetString()!;
        double flat = 0; Graph2d? rep = null, run = null; double? boredom = null;
        var scorers = new List<(EmotionType, Graph2d)>();
        if (e.TryGetProperty("scoring", out var sc))
        {
            if (sc.TryGetProperty("flatScore", out var f)) flat = f.GetDouble();
            if (sc.TryGetProperty("repetitionPenalty", out var r)) rep = Graph2d.FromJson(r);
            if (sc.TryGetProperty("runningPenalty", out var rn)) run = Graph2d.FromJson(rn);
            if (sc.TryGetProperty("boredomMultiplier", out var bm)) boredom = bm.GetDouble();
            if (sc.TryGetProperty("emotionScorers", out var es))
                foreach (var s in es.EnumerateArray())
                    if (Enum.TryParse<EmotionType>(s.GetProperty("emotionType").GetString(), true, out var et) && s.TryGetProperty("scoreGraph", out var sg) && Graph2d.FromJson(sg) is { } g)
                        scorers.Add((et, g));
        }
        return new ScoredBehaviorEntry(id, flat, rep, run, boredom, scorers);
    }
}

/// <summary>Why a chooser picked (or kept, or found nothing).</summary>
public sealed record ChooserDecision(IBehavior? Behavior, string Reason, IReadOnlyList<(string Id, double Score, string Note)> Scores);

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
/// ("behavior '%s' has score of %f, so is interrupting running behavior '%s' which scored %f"); exact ties are
/// broken at random (<c>RandDbl</c>). Behaviours named in the config but absent from the bound set are noted,
/// not invented.
/// </summary>
public sealed class ScoringChooser : IBehaviorChooser
{
    private readonly Dictionary<string, IBehavior> _bound;
    private readonly RepetitionPenalty _penalty;

    public ScoringChooser(IReadOnlyList<ScoredBehaviorEntry> entries, IReadOnlyDictionary<string, IBehavior> bound, Graph2d? scoreBonusForCurrent = null, RepetitionPenalty? penalty = null)
    {
        Entries = entries; ScoreBonusForCurrent = scoreBonusForCurrent; _penalty = penalty ?? new RepetitionPenalty();
        _bound = entries.Where(e => bound.ContainsKey(e.BehaviorId)).ToDictionary(e => e.BehaviorId, e => bound[e.BehaviorId]);
    }

    public BehaviorChooserType Type => BehaviorChooserType.Scoring;
    public IReadOnlyList<ScoredBehaviorEntry> Entries { get; }
    public Graph2d? ScoreBonusForCurrent { get; }
    public IReadOnlyList<string> BehaviorIds => Entries.Select(e => e.BehaviorId).ToList();
    public IReadOnlyList<string> Unbound => Entries.Where(e => !_bound.ContainsKey(e.BehaviorId)).Select(e => e.BehaviorId).ToList();
    public Random Random { get; set; } = new();

    /// <summary>
    /// Records a completed run in the shared repetition history. The manager already does this for every
    /// behaviour that reaches <c>BehaviorStopReason.Completed</c> — the engine's
    /// <c>StopWithoutImmediateRepetitionPenalty</c> exists precisely so an interrupted one is not penalised —
    /// so this is only for callers driving a chooser without a manager.
    /// </summary>
    public void Ran(string behaviorId, double nowSec) => _penalty.Ran(behaviorId, nowSec);

    public ChooserDecision GetDesiredActiveBehavior(IBehavior? current, double currentRunningSec, BehaviorContext ctx, double nowSec)
    {
        var scores = new List<(string, double, string)>();
        IBehavior? best = null; double bestScore = 0; double currentScore = 0;
        foreach (var e in Entries)
        {
            if (!_bound.TryGetValue(e.BehaviorId, out var b)) { scores.Add((e.BehaviorId, 0, "not built")); continue; }
            bool running = current is not null && current.Id == b.Id;
            double s = e.Evaluate(b, ctx, nowSec, _penalty.LastRunSec(b.Id), running ? currentRunningSec : null, _penalty);
            if (running && s > 0 && ScoreBonusForCurrent is { } bonus) s += bonus.EvaluateY(currentRunningSec);
            scores.Add((b.Id, s, running ? "running" : s <= 0 ? (b.IsRunnable(ctx) ? "scored 0" : "not runnable") : ""));
            if (running) currentScore = s;
            if (s > bestScore || (s == bestScore && s > 0 && best is not null && Random.NextDouble() < 0.5)) { bestScore = s; best = b; }
        }
        if (best is null) return new ChooserDecision(null, "no listed behaviour is runnable and wants to run", scores);
        if (current is not null && current.Id != best.Id && currentScore > 0 && bestScore <= currentScore)
            return new ChooserDecision(current, "the running behaviour keeps its place", scores);
        if (current is not null && current.Id == best.Id) return new ChooserDecision(best, "already running", scores);
        return new ChooserDecision(best, current is null ? $"highest score {bestScore:F2}" : $"behavior '{best.Id}' has score of {bestScore:F2}, so is interrupting running behavior '{current.Id}' which scored {currentScore:F2}", scores);
    }
}

/// <summary>The engine's <c>StrictPriorityBSRunnableChooser</c>: the first runnable behaviour in the list (the running one stays while it is still the first runnable).</summary>
public sealed class StrictPriorityChooser : IBehaviorChooser
{
    private readonly IReadOnlyDictionary<string, IBehavior> _bound;
    public StrictPriorityChooser(IReadOnlyList<string> ids, IReadOnlyDictionary<string, IBehavior> bound) { BehaviorIds = ids; _bound = bound; }
    public BehaviorChooserType Type => BehaviorChooserType.StrictPriority;
    public IReadOnlyList<string> BehaviorIds { get; }
    public IReadOnlyList<string> Unbound => BehaviorIds.Where(i => !_bound.ContainsKey(i)).ToList();

    public ChooserDecision GetDesiredActiveBehavior(IBehavior? current, double currentRunningSec, BehaviorContext ctx, double nowSec)
    {
        var scores = new List<(string, double, string)>();
        foreach (var id in BehaviorIds)
        {
            if (!_bound.TryGetValue(id, out var b)) { scores.Add((id, 0, "not built")); continue; }
            // NATIVE (StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior 0x0060B23E): the loop reads the
            // candidate's is-running flag (IBehavior +0xA1, the same byte IsRunnableBase logs "Behavior %s is
            // already running" from) at 0x0060B250 and selects it without calling IsRunnable at all. Only a
            // behaviour that is not running is asked whether it is runnable.
            bool running = current is not null && current.Id == id;
            if (running || b.IsRunnable(ctx)) { scores.Add((id, 1, running ? "running" : "first runnable")); return new ChooserDecision(b, running ? "already running" : $"first runnable in priority order", scores); }
            scores.Add((id, 0, "not runnable"));
        }
        return new ChooserDecision(null, "nothing in the priority list is runnable", scores);
    }
}

/// <summary>The engine's <c>SelectionBSRunnableChooser</c>: the app names the behaviour (<c>ExecuteBehavior</c>); nothing to choose here.</summary>
public sealed class SelectionChooser : IBehaviorChooser
{
    public BehaviorChooserType Type => BehaviorChooserType.Selection;
    public IReadOnlyList<string> BehaviorIds => Array.Empty<string>();
    public ChooserDecision GetDesiredActiveBehavior(IBehavior? current, double currentRunningSec, BehaviorContext ctx, double nowSec) =>
        new(current, "Selection: the app chooses (no request)", Array.Empty<(string, double, string)>());
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
public sealed class ActivityStrategy
{
    public string Type { get; init; } = "Simple";
    public double CanEndDurationSec { get; init; } = -1;
    public double ShouldEndDurationSec { get; init; } = -1;
    public double CooldownBaseSec { get; init; }
    public double CooldownRandomnessSec { get; init; }
    public bool StartInCooldown { get; init; }
    public double RequiredRecentOnTreadsEventSec { get; init; } = -1;
    public double RequiredMinStartMoodScore { get; init; } = double.NaN;
    public IReadOnlyList<(EmotionType Emotion, Graph2d Graph)> StartMoodScorer { get; init; } = Array.Empty<(EmotionType, Graph2d)>();
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
    public double CurrentCooldownSec { get; private set; } = double.NaN;
    public Random Random { get; set; } = new();

    /// <summary>The engine's float epsilon in these comparisons (0x3727C5AC).</summary>
    public const double Epsilon = 1e-5;

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
        return new ActivityStrategy
        {
            Type = e.TryGetProperty("type", out var t) ? t.GetString() ?? "Simple" : "Simple",
            CanEndDurationSec = D("activityCanEndDurationSecs", -1), ShouldEndDurationSec = D("activityShouldEndDurationSecs", -1),
            CooldownBaseSec = D("cooldownBaseSecs", 0), CooldownRandomnessSec = D("cooldownRandomnessSecs", 0),
            StartInCooldown = e.TryGetProperty("startInCooldown", out var sic) && sic.ValueKind == JsonValueKind.True,
            RequiredRecentOnTreadsEventSec = D("requiredRecentOnTreadsEventSecs", -1), RequiredMinStartMoodScore = D("requiredMinStartMoodScore", double.NaN),
            StartMoodScorer = scorers, WantsToRunStrategyType = wtr, Need = need, NeedBracket = bracket, HigherPriorityStrategy = higher,
            NeedCooldownGraph = cd, NeedCooldownRandomnessGraph = cdr,
        };
    }

    /// <summary>The activity ended: only the end time is recorded (the engine re-randomises on the next start).</summary>
    public void OnEnded(double nowSec) => LastEndedSec = nowSec;

    /// <summary><c>RandomizeCooldown</c> (0x005B5408): base plus a random share of the randomness.</summary>
    public void RandomizeCooldown()
    {
        double baseSec = CooldownBaseSec, randSec = CooldownRandomnessSec;
        if (Type == "NeedBasedCooldown" && Need is { } n && NeedLevels is { } levels)
        {
            double level = levels(n);
            if (NeedCooldownGraph is { } g) baseSec = g.EvaluateY(level);
            if (NeedCooldownRandomnessGraph is { } r) randSec = r.EvaluateY(level);
        }
        CurrentCooldownSec = baseSec + Random.NextDouble() * randSec;
    }

    /// <summary>The cooldown in force, the constructor's <c>cooldownBaseSecs</c> until the first randomisation.</summary>
    public double EffectiveCooldownSec => double.IsNaN(CurrentCooldownSec) ? CooldownBaseSec : CurrentCooldownSec;

    /// <summary>
    /// <c>WantsToStart</c>'s cooldown test (0x005B52C8..0x005B5334): it applies while the cooldown is above
    /// zero and either the activity has ended before or <c>startInCooldown</c> is set, and it measures from the
    /// last end time — which is 0 for an activity that has never run, so <c>startInCooldown</c> holds the
    /// activity back for the first cooldown of the session.
    ///
    /// Not reproduced: the engine substitutes a flat 3 s when the activity ended within the last two
    /// base-station ticks (0x005B52EA..0x005B5316); that needs the tick length and the second time argument,
    /// whose meaning was not traced.
    /// </summary>
    public bool InCooldown(double nowSec)
    {
        double cooldown = EffectiveCooldownSec;
        if (cooldown <= Epsilon) return false;
        double ended = LastEndedSec ?? 0;
        if (ended <= Epsilon && !StartInCooldown) return false;
        return nowSec < ended + cooldown;
    }

    public bool WantsToStart(FreeplayInputs inputs, double nowSec, out string reason)
    {
        if (InCooldown(nowSec)) { reason = $"in cooldown ({EffectiveCooldownSec:F0} s)"; return false; }
        RandomizeCooldown();     // the engine randomises here, once the cooldown has passed
        if (RequiredRecentOnTreadsEventSec > 0 && (inputs.LastOnTreadsEventSec is not { } t || nowSec - t > RequiredRecentOnTreadsEventSec)) { reason = "no recent on-treads event"; return false; }
        if (!double.IsNaN(RequiredMinStartMoodScore) && StartMoodScorer.Count > 0)
        {
            double score = inputs.Mood is { } mood ? StartMoodScorer.Sum(s => s.Graph.EvaluateY(mood[s.Emotion])) : 0;
            if (score < RequiredMinStartMoodScore) { reason = $"mood score {score:F2} below {RequiredMinStartMoodScore:F2}"; return false; }
        }
        switch (Type)
        {
            case "Needs":
                if (Need is not { } n || NeedBracket is not { } b || inputs.Needs is null) { reason = "no needs state"; return false; }
                if (!inputs.Needs.State.IsNeedAtBracket(n, b)) { reason = $"{n} not {b}"; return false; }
                if (HigherPriorityStrategy is { } h && Enum.TryParse<NeedId>(h, true, out var hn) && inputs.Needs.State.IsNeedAtBracket(hn, NeedBracketId.Critical)) { reason = $"{hn} is Critical and takes priority"; return false; }
                reason = $"{n} is {b}"; return true;
            case "SevereNeedTransition":
                if (Need is not { } sn || inputs.Needs is null) { reason = "no needs state"; return false; }
                if (!inputs.Needs.State.IsNeedAtBracket(sn, NeedBracketId.Critical)) { reason = $"{sn} not Critical"; return false; }
                if (inputs.Needs.IsSevereExpressed(sn)) { reason = $"{sn} severe state already expressed"; return false; }
                reason = $"{sn} just became Critical"; return true;
            case "Pyramid":
                if (inputs.LocatedCubes < 3) { reason = $"{inputs.LocatedCubes} cube(s) located, a pyramid needs 3"; return false; }
                if (inputs.PyramidBuilt) { reason = "a pyramid stands"; return false; }
                reason = "three cubes and no pyramid"; return true;
            case "PlayWithHumans" or "FPPlayWithHumans":
                reason = "the game request component is the app's"; return false;
            case "Spark":
                if (inputs.RequestedSpark is { } spark) { reason = $"spark '{spark}' requested"; return true; }
                reason = "no spark requested"; return false;
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
    public bool WantsToEnd(FreeplayInputs inputs, double runningSec, out string reason)
    {
        if (CanEndDurationSec > Epsilon && runningSec < CanEndDurationSec - Epsilon)
        { reason = $"ran {runningSec:F0} s, cannot end before {CanEndDurationSec:F0}"; return false; }
        if (ShouldEndDurationSec > Epsilon && runningSec > ShouldEndDurationSec + Epsilon) { reason = $"ran {runningSec:F0} s, should end after {ShouldEndDurationSec:F0}"; return true; }
        switch (Type)
        {
            case "Needs":
                if (Need is { } n && NeedBracket is { } b && inputs.Needs is not null && !inputs.Needs.State.IsNeedAtBracket(n, b)) { reason = $"{n} left {b}"; return true; }
                break;
            case "SevereNeedTransition":
                reason = "the transition is a get-in"; return true;
            case "Spark":
                if (inputs.RequestedSpark is null) { reason = "the spark ended"; return true; }
                break;
        }
        reason = "keeps running"; return false;
    }
}

/// <summary>What the strategies and the freeplay chooser read about the world each tick.</summary>
public sealed class FreeplayInputs
{
    public NeedsManager? Needs { get; init; }
    public MoodState? Mood { get; init; }
    public double? LastOnTreadsEventSec { get; set; }
    public int LocatedCubes { get; set; }
    public bool PyramidBuilt { get; set; }
    public bool FaceKnown { get; set; }
    public bool OnCharger { get; set; }
    public string? RequestedSpark { get; set; }
}

/// <summary>
/// The engine's <c>IActivity</c> (<c>ActivityBehaviorsOnly</c> and the typed ones read the same base config:
/// <c>activityID</c>, <c>activityType</c>, <c>behaviorChooser</c>, <c>interludeBehaviorChooser</c>,
/// <c>activityStrategy</c>, <c>driveStartAnimTrigger</c> / <c>driveLoopAnimTrigger</c> / <c>driveEndAnimTrigger</c>,
/// <c>idleAnimTrigger</c>, <c>infoAnalyzerProcess</c>, <c>requireSpark</c>, <c>needsActionID</c>).
/// <c>GetDesiredActiveBehavior</c> asks the chooser; between two behaviours an interlude from the interlude
/// chooser runs when one is runnable ("Activity %s is inserting interlude %s between behaviors %s and %s").
/// <c>OnSelected</c> pushes the idle animation and starts the clock ("robot.freeplay_goal_started");
/// <c>OnDeselected</c> removes it and starts the strategy's cooldown.
/// </summary>
public sealed class Activity
{
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
    public string? RequireSpark { get; init; }
    public string? NeedsActionId { get; init; }
    public IReadOnlyList<Activity> SubActivities { get; init; } = Array.Empty<Activity>();
    /// <summary>Freeplay's <c>desiredActivityNames</c>: face+cube, face only, cube only, neither.</summary>
    public (string FaceAndCube, string FaceOnly, string CubeOnly, string None)? DesiredActivityNames { get; init; }

    public double? SelectedAtSec { get; private set; }
    public void OnSelected(double nowSec) => SelectedAtSec = nowSec;
    public void OnDeselected(double nowSec) { SelectedAtSec = null; Strategy.OnEnded(nowSec); }
    public double RunningSec(double nowSec) => SelectedAtSec is { } s ? nowSec - s : 0;

    /// <summary>Every behaviour id this activity (and its sub-activities) can name.</summary>
    public IEnumerable<string> AllBehaviorIds() =>
        (Chooser?.BehaviorIds ?? Array.Empty<string>()).Concat(InterludeChooser?.BehaviorIds ?? Array.Empty<string>()).Concat(SubActivities.SelectMany(a => a.AllBehaviorIds()));

    public override string ToString() => $"{Id} ({Type}, {Strategy.Type}{(Chooser is null ? "" : ", " + Chooser.Type)})";
}

/// <summary>
/// Loads the shipped activity tree: <c>behaviorSystem/activities_config.json</c> lists the top-level activities
/// (Selection, MeetCozmo, Feeding, Freeplay), and Freeplay's <c>subActivities</c> name the activities under
/// <c>activities/**</c> by <c>activityID</c> with their <c>activityPriority</c> (<c>ActivityFreeplay::CreateFromConfig</c>:
/// "ActivityFreeplay.CreateFromConfig.ActivityID.KeyMissing", "activityPriority"). Behaviour ids are bound to the
/// implemented set; ids with no implementation are kept in the choosers as "not built" so the tree is the shipped
/// one, not a trimmed copy.
/// </summary>
public static class ActivityTreeLoader
{
    public static string ActivitiesDir(string obbRoot) => Path.Combine(obbRoot, "assets", "cozmo_resources", "config", "engine", "behaviorSystem");

    public static IReadOnlyList<Activity> Load(string obbRoot, IReadOnlyDictionary<string, IBehavior> bound, RepetitionPenalty? penalty = null, Random? random = null)
    {
        var dir = ActivitiesDir(obbRoot);
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
        foreach (var a in top.RootElement.EnumerateArray()) result.Add(Build(a, byId, bound, penalty, random, 0));
        return result;
    }

    private static Activity Build(JsonElement e, Dictionary<string, JsonElement> byId, IReadOnlyDictionary<string, IBehavior> bound, RepetitionPenalty? penalty, Random? random, int priority)
    {
        string id = e.GetProperty("activityID").GetString()!;
        var subs = new List<Activity>();
        if (e.TryGetProperty("subActivities", out var sa))
            foreach (var s in sa.EnumerateArray())
            {
                string sid = s.GetProperty("activityID").GetString()!;
                int p = s.TryGetProperty("activityPriority", out var pr) ? pr.GetInt32() : 0;
                if (byId.TryGetValue(sid, out var se)) subs.Add(Build(se, byId, bound, penalty, random, p));
                else subs.Add(new Activity { Id = sid, Type = "Missing", Priority = p, Strategy = new ActivityStrategy { Type = "Missing" } });
            }
        AnimationTrigger? Trig(string k) => e.TryGetProperty(k, out var t) && Enum.TryParse<AnimationTrigger>(t.GetString(), out var tr) ? tr : null;
        (string, string, string, string)? desired = null;
        if (e.TryGetProperty("desiredActivityNames", out var dn))
            desired = (dn.GetProperty("faceAndCubeActivityName").GetString()!, dn.GetProperty("faceOnlyActivityName").GetString()!, dn.GetProperty("cubeOnlyActivityName").GetString()!, dn.GetProperty("noFaceNoCubeActivityName").GetString()!);
        var strategy = e.TryGetProperty("activityStrategy", out var st) ? ActivityStrategy.FromJson(st) : new ActivityStrategy();
        if (random is not null) strategy.Random = random;
        return new Activity
        {
            Id = id, Type = e.TryGetProperty("activityType", out var ty) ? ty.GetString() ?? "BehaviorsOnly" : "BehaviorsOnly", Priority = priority, Strategy = strategy,
            Chooser = e.TryGetProperty("behaviorChooser", out var bc) ? BuildChooser(bc, bound, penalty, random) : e.TryGetProperty("universalChooser", out var uc) ? BuildChooser(uc, bound, penalty, random) : null,
            InterludeChooser = e.TryGetProperty("interludeBehaviorChooser", out var ic) ? BuildChooser(ic, bound, penalty, random) : null,
            DriveStartAnim = Trig("driveStartAnimTrigger"), DriveLoopAnim = Trig("driveLoopAnimTrigger"), DriveEndAnim = Trig("driveEndAnimTrigger"), IdleAnim = Trig("idleAnimTrigger"),
            RequireSpark = e.TryGetProperty("requireSpark", out var rs) ? rs.GetString() : null,
            NeedsActionId = e.TryGetProperty("needsActionID", out var na) ? na.GetString() : null,
            SubActivities = subs.OrderBy(s => s.Priority).ToList(), DesiredActivityNames = desired,
        };
    }

    public static IBehaviorChooser BuildChooser(JsonElement c, IReadOnlyDictionary<string, IBehavior> bound, RepetitionPenalty? penalty, Random? random)
    {
        string type = c.TryGetProperty("type", out var t) ? t.GetString() ?? "Scoring" : "Scoring";
        switch (type)
        {
            case "StrictPriority":
                return new StrictPriorityChooser(c.TryGetProperty("behaviors", out var b) ? b.EnumerateArray().Select(x => x.GetString()!).ToList() : new List<string>(), bound);
            case "Selection":
                return new SelectionChooser();
            default:
                var entries = c.TryGetProperty("behaviors", out var bs) ? bs.EnumerateArray().Select(ScoredBehaviorEntry.FromJson).ToList() : new List<ScoredBehaviorEntry>();
                var bonus = c.TryGetProperty("scoreBonusForCurrentBehavior", out var sb) ? Graph2d.FromJson(sb) : null;
                var sc = new ScoringChooser(entries, bound, bonus, penalty);
                if (random is not null) sc.Random = random;
                return sc;
        }
    }
}
