using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// The engine's config-driven <c>PlayAnim</c> class (<c>BehaviorPlayAnimSequence</c>), which names its
/// animations in an <c>animTriggers</c> field rather than in code.
///
/// <b>One trigger</b> - <c>StartPlayingAnimations</c> 0x005C0158 tests the vector's byte length against 4
/// (<c>cmp r1,#4</c> 0x005C0168) - plays it as one <c>TriggerLiftSafeAnimationAction</c>
/// (<c>trigger, numLoops = num_loops, interruptRunning = 1, tracksToLock = 0, timeout = 60.0f, strictCooldown = 0</c>,
/// 0x005C0174..0x005C0190), the loop being inside the action: <c>num_loops</c> 0 plays it until it is cancelled.
/// <b>Any other count</b> goes to <c>StartSequenceLoop</c> 0x005C0294: while the loop index is below
/// <c>num_loops</c> (signed, 0x005C02A8..0x005C02AE) it builds a <c>CompoundActionSequential</c> with one such
/// action per trigger - each <c>numLoops = 1</c>, <c>tracksToLock = 0</c>, timeout 60.0f - added with
/// <c>ignoreFailure = 0</c> (0x005C02F6, 0x005C02FC; <c>AddAction</c> 0x0054EC7C installs the ignore function only
/// for 1), so a failed child ends the sequence (<c>CompoundActionSequential::UpdateInternal</c> 0x0054F70C,
/// 0x0054F81A..0x0054F820), and starts it; its completion calls <c>CallToListeners</c> and then
/// <c>StartSequenceLoop</c> again. A <c>num_loops</c> of 0 or less plays nothing on this path. The
/// <c>num_loops</c> key defaults to 1 (<c>Json::Value::Value(1)</c> 0x005C001C..0x005C0036). All the actions of
/// one loop are built when the loop starts; each resolves its trigger to an animation group when built and to a
/// clip when it starts.
///
/// <c>ResumeInternal</c> (<c>vtable+0x4c</c>) 0x005BFF1E returns 1: the engine never resumes a PlayAnim.
/// <c>IsRunnableInternal</c> 0x005C013A is false for an empty trigger list. This is <b>not</b> the
/// <c>PlayAnimWithFace</c> class: <c>BehaviorPlayAnimSequenceWithFace::InitInternal</c> (0x005C0648) runs a
/// <c>TurnTowardsFaceAction</c> (0x005C0686) before the animation, so that class needs a tracked face.
///
/// It locks no tracks of its own: the action's mask is 0, except that the lift-safe constructor ORs LIFT in while
/// the robot is carrying and on its treads (<see cref="SteppedBehavior.RunTriggerAction"/>), so the engine sends
/// DisableAnimTracks only then.
/// </summary>
// fidelity: M8-005
public sealed class PlayAnimBehavior : SteppedBehavior
{
    /// <summary>
    /// Every shipped <c>PlayAnim</c> behaviour, built the engine's way: <c>RobotDataLoader::LoadBehaviors</c> reads the config corpus
    /// (<see cref="BehaviorConfigLoader"/>) and the <see cref="BehaviorContainer"/> builds each behaviour through
    /// <see cref="BehaviorFactory"/>; this returns the ones whose class is <c>PlayAnim</c>. Warnings and errors the load logs are added to
    /// <paramref name="problems"/>. Empty when the OBB is not there.
    /// </summary>
    // fidelity: M7-018
    public static IReadOnlyList<PlayAnimBehavior> LoadShipped(string obbRoot, List<string>? problems = null)
    {
        var ctx = new BehaviorFactoryContext { Log = line => { if (line.StartsWith("warning:") || line.StartsWith("error:")) problems?.Add(line); } };
        return BehaviorContainer.LoadShipped(obbRoot, ctx).Behaviors.Values.OfType<PlayAnimBehavior>().ToList();
    }

    /// <summary>
    /// <c>BehaviorPlayAnimSequence::BehaviorPlayAnimSequence(Robot&amp;, const Json&amp;, bool)</c> 0x005bff24..0x005c004c (<c>CreateBehavior</c> case 0x26): reads
    /// every <c>animTriggers</c> string, converts it to an <see cref="AnimationTrigger"/> and keeps all values except <c>Count</c>; reads
    /// <c>num_loops</c> (default 1). A shipped config with no usable trigger is still constructed (<c>IsRunnableInternal</c> 0x005c013a is false for an empty
    /// list). A trigger name outside the enum takes <c>AnimationTriggerFromString</c>'s miss path (cerr error, value 0), and the constructor keeps the 0. The common <c>IBehavior::ReadFromJson</c> keys this stack reads for PlayAnim are
    /// <c>wantsToRunStrategyConfig</c> and the three <c>requiredRecent...</c> windows.
    /// </summary>
    // fidelity: M7-018, M8-005
    public static PlayAnimBehavior FromConfig(System.Text.Json.JsonElement root, Action<string>? log = null)
    {
        var id = BehaviorConfigLoader.ExtractIdFromConfig(root).ToString();
        var parsed = new List<AnimationTrigger>();
        if (root.TryGetProperty("animTriggers", out var triggers) && triggers.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var t in triggers.EnumerateArray())
            {
                var name = t.ValueKind == System.Text.Json.JsonValueKind.String ? t.GetString() : null;
                // AnimationTriggerFromString 0x0075E2B0: a miss (0x00764798..0x00764810) writes the cerr error and returns 0; the constructor keeps every
                // value except 0x23F, Count (0x005BFFE8..0x005BFFEE), so a miss pushes trigger 0.
                if (name == "Count") continue;
                if (name is not null && Enum.TryParse<AnimationTrigger>(name, out var trigger) && Enum.IsDefined(trigger)) parsed.Add(trigger);
                else
                {
                    BehaviorClassNames.WriteCerrError(name ?? "", "AnimationTrigger");
                    parsed.Add((AnimationTrigger)0);
                }
            }
        string? strategy = null;
        NeedId? strategyNeed = null;
        NeedBracketId? strategyBracket = null;
        if (root.TryGetProperty("wantsToRunStrategyConfig", out var wtr))
        {
            if (wtr.TryGetProperty("strategyType", out var st)) strategy = st.GetString();
            if (wtr.TryGetProperty("need", out var nd) && Enum.TryParse<NeedId>(nd.GetString(), true, out var parsedNeed))
                strategyNeed = parsedNeed;
            if (wtr.TryGetProperty("needBracket", out var nb) && Enum.TryParse<NeedBracketId>(nb.GetString(), true, out var parsedBracket))
                strategyBracket = parsedBracket;
        }
        double? Sec(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number ? v.GetDouble() : null;
        int loops = root.TryGetProperty("num_loops", out var nl) && nl.ValueKind == System.Text.Json.JsonValueKind.Number
            ? nl.GetInt32() : 1;
        return new PlayAnimBehavior(id, "PlayAnim", parsed)
        {
            NumLoops = loops,
            WantsToRunStrategy = strategy,
            StrategyNeed = strategyNeed,
            StrategyBracket = strategyBracket,
            RequiredRecentDriveOffChargerSec = Sec("requiredRecentDriveOffCharger_sec"),
            RequiredRecentOnTreadsEventSec = Sec("requiredRecentOnTreadsEventSecs"),
            RequiredRecentSwitchToParentSec = Sec("requiredRecentSwitchToParent_sec"),
        };
    }

    private readonly IReadOnlyList<AnimationTrigger> _triggers;
    /// <summary>BehaviorPlayAnimSequence +0x12c: how many times <c>StartSequenceLoop</c> has built the sequence this run.</summary>
    private int _loopIndex;

    // fidelity: M8-004
    // The engine has no in-code score: IBehavior::IBehavior 0x005BBD28 (strd r8,r8,[r4,#0x100], r8 = 0 from
    // 0x005BBD1C) writes zero to the flat score +0x100, and EvaluateScoreInternal 0x005BEEC2 returns +0x100 when the
    // mood-scorer vector +0xDC is empty. This behaviour's score is therefore SteppedBehavior's zero default.
    public PlayAnimBehavior(string id, string behaviorClass, IEnumerable<AnimationTrigger> triggers) : base(id, behaviorClass)
    {
        _triggers = triggers.ToList();
    }

    /// <summary>The animation actually selected on the last start, for tracing.</summary>
    public string? LastSelected { get; private set; }

    /// <summary>
    /// The config's <c>wantsToRunStrategyConfig.strategyType</c> (<c>IBehavior::ReadFromJson</c> ->
    /// <c>WantsToRunStrategyFactory::CreateWantsToRunStrategy</c> 0x00614710; <c>IsRunnableBase</c>
    /// 0x005BD778 asks it <c>WantsToRun</c>).
    ///
    /// The factory dispatches on <c>WantsToRunStrategyType</c>, whose nine names are the table
    /// <c>EnumToString</c> 0x007716A4 indexes at 0x01033820: Invalid, AlwaysRun, ExpressNeedsTransition,
    /// Generic, InNeedsBracket, ObstacleDetected, PlacedOnCharger, RobotPlacedOnSlope, RobotShaken. The M8
    /// inventory gives the bodies of two:
    ///
    /// <list type="bullet">
    /// <item><c>StrategyInNeedsBracket::WantsToRunInternal</c> 0x006141A0 is one call -
    /// <c>NeedsState::IsNeedAtBracket(need, bracket)</c> on the current needs state, with the pair read
    /// from the config's <c>need</c> and <c>needBracket</c>.</item>
    /// <item><c>StrategyExpressNeedsTransition::WantsToRunInternal</c> 0x006136D8 asks
    /// <c>IsNeedAtBracket(need, Critical)</c> - the literal 3 at 0x006136EC - and then is true only if the one
    /// value <c>[[robot+0x264]+0x30]+0x14</c> (<see cref="BehaviorContext.AiExpressedNeedValue"/>) is not that
    /// need (0x006136FC..0x00613704).</item>
    /// </list>
    ///
    /// <c>ObstacleDetected</c> is M10-009's. A behaviour with no <c>wantsToRunStrategyConfig</c> has no
    /// strategy. Any other type has no body in the inventory, so it throws <see cref="NotSupportedException"/>
    /// instead of answering; no shipped PlayAnim config names one (the shipped strategy types on behaviours are
    /// InNeedsBracket and ObstacleDetected).
    /// </summary>
    public string? WantsToRunStrategy { get; init; }

    /// <summary>The <c>need</c> the strategy names, for InNeedsBracket and ExpressNeedsTransition.</summary>
    public NeedId? StrategyNeed { get; init; }

    /// <summary>The <c>needBracket</c> InNeedsBracket wants that need to be in.</summary>
    public NeedBracketId? StrategyBracket { get; init; }

    /// <summary>
    /// <c>IBehavior::IsRunnableBase</c>'s recent-event windows (0x005BD81A..0x005BD862 and 0x005BD8AC..: the config value
    /// minus 1e-5 against now minus the event's timestamp): <c>requiredRecentDriveOffCharger_sec</c>
    /// (Hiking_FirstLookWakeUp 1.0), <c>requiredRecentOnTreadsEventSecs</c>, <c>requiredRecentSwitchToParent_sec</c>
    /// (Hiking_FirstLookIntro 0.25). Null: no window.
    /// </summary>
    public double? RequiredRecentDriveOffChargerSec { get; init; }
    public double? RequiredRecentOnTreadsEventSec { get; init; }
    public double? RequiredRecentSwitchToParentSec { get; init; }

    /// <summary><c>BehaviorPlayAnimSequence::IsRunnableInternal</c> (<c>vtable+0x50</c>) 0x005C013A: false for an empty trigger vector, else the <c>vtable+0x90</c> tail call (0x005C03B4, 1).</summary>
    protected override bool IsRunnableInternal(BehaviorContext context) => _triggers.Count > 0;

    /// <summary><c>BehaviorPlayAnimSequence</c>'s <c>vtable+0x20</c> (0x01026940: <c>IBehavior</c>'s 0x005BF04C, <c>movs r0,#0</c>).</summary>
    protected override bool RunnableGate20(BehaviorContext context) => false;

    /// <summary><c>vtable+0x24</c> (0x01026944: <c>IBehavior</c>'s 0x0059EC12, <c>movs r0,#0</c>).</summary>
    protected override bool RunnableGate24(BehaviorContext context) => false;

    /// <summary><c>vtable+0x28</c> (0x01026948: <c>BehaviorPlayAnimSequence</c>'s 0x005BFF1A, <c>movs r0,#1</c>).</summary>
    protected override bool RunnableGate28(BehaviorContext context) => true;

    /// <summary>What the behaviour's wants-to-run strategy says right now, on its own.</summary>
    public bool WantsToRunNow(BehaviorContext context) => WantsToRun(context);

    protected override bool RecentTimersAllow(BehaviorContext context) => RecentEventsAllow(context);

    /// <summary>
    /// The drive-off-charger part of <c>IsRunnableBase</c> 0x005BD826..0x005BD962 in float: a window below -1e-5f (0xB727C5AC) passes; otherwise a never-stamped whiteboard (-1.0f, here null) or a
    /// stamp below -1e-5f fails, and the rest passes when <c>(window + stamp) + 1e-5f (0x3727C5AC) &gt;= now</c>.
    /// </summary>
    private static bool DriveOffWithin(double? window, float? stamp, double now)
    {
        if (window is not { } w) return true;
        float wf = (float)w;
        if (wf < BitConverter.Int32BitsToSingle(unchecked((int)0xB727C5AC))) return true;
        if (stamp is not { } s || s < BitConverter.Int32BitsToSingle(unchecked((int)0xB727C5AC))) return false;
        float sum = wf + s;
        sum = sum + BitConverter.Int32BitsToSingle(0x3727C5AC);
        return sum >= (float)now;
    }

    private bool RecentEventsAllow(BehaviorContext ctx)
    {
        if (RequiredRecentDriveOffChargerSec is null && RequiredRecentOnTreadsEventSec is null && RequiredRecentSwitchToParentSec is null) return true;
        if (ctx.ClockSec is null) return false;
        double now = ctx.ClockSec();
        static bool Within(double? window, double? stamp, double now) => window is not { } w || w < 0 || (stamp is { } s && now - s <= w + 1e-5);
        return DriveOffWithin(RequiredRecentDriveOffChargerSec, ctx.LastDriveOffChargerSec, now)
            && Within(RequiredRecentOnTreadsEventSec, ctx.LastOnTreadsEventSec, now)
            && Within(RequiredRecentSwitchToParentSec, ctx.LastActivitySwitchSec, now);
    }

    // fidelity: M8-006
    protected override bool WantsToRun(BehaviorContext context) => WantsToRunStrategy switch
    {
        null => true,
        "ObstacleDetected" => context.AI?.ObstacleDetected ?? false,   // fidelity: M10-009 - AIComponent+4 (0x006143CA)
        "InNeedsBracket" => context.Needs is { } needs && StrategyNeed is { } need && StrategyBracket is { } bracket
                            && needs.State.IsNeedAtBracket(need, bracket),
        "ExpressNeedsTransition" => ExpressNeedsTransition(context),
        var other => throw new NotSupportedException(
            $"M8-006: the wants-to-run strategy '{other}' has no body in the M8 inventory (behaviour {Id}); " +
            "the engine's WantsToRunStrategyFactory::CreateWantsToRunStrategy 0x00614710 builds it, but nothing here answers for it."),
    };

    /// <summary>
    /// <c>StrategyExpressNeedsTransition::WantsToRunInternal</c> 0x006136D8: <c>IsNeedAtBracket(need, Critical)</c>
    /// must be 1 (0x006136EA..0x006136F6), and then the result is whether <c>[[robot+0x264]+0x30]+0x14</c> differs
    /// from the strategy's need (<c>ldr r2,[r5,#0x14]; cmp r2,r1; it ne; movne r0,#1</c> 0x006136FC..0x00613704).
    /// That value has no source in this stack (MISSING: M8-006), so asking it without the seam throws.
    /// </summary>
    private bool ExpressNeedsTransition(BehaviorContext context)
    {
        if (context.Needs is not { } needs || StrategyNeed is not { } need) return false;
        if (!needs.State.IsNeedAtBracket(need, NeedBracketId.Critical)) return false;
        // [[robot+0x264]+0x30]+0x14 is the SevereNeedsComponent's current severe NeedId, 3 (none) from its constructor (0x00572A0E); this stack has no component
        // with the engine's writers, so unset it is 3 and the gap is reported once.
        NeedId? expressedValue;
        if (context.AiExpressedNeedValue is { } expressed) expressedValue = expressed();
        else
        {
            SteppedBehavior.ReportMissing("StrategyExpressNeedsTransition [[robot+0x264]+0x30]+0x14 (0x006136DC..0x006136FC): this stack has no SevereNeedsComponent with the engine's writers; the value is its constructed 3 (none)");
            expressedValue = null;
        }
        return expressedValue != need;
    }

    /// <summary>How many times the loop plays: the config's <c>num_loops</c>, default 1 (BehaviorPlayAnimSequence +0x128).</summary>
    public int NumLoops { get; init; } = 1;

    /// <summary>The triggers this behaviour plays, in the order the config lists them.</summary>
    public IReadOnlyList<AnimationTrigger> Triggers => _triggers;

    /// <summary><c>BehaviorPlayAnimSequence::ResumeInternal</c> (<c>vtable+0x4c</c>) 0x005BFF1E: <c>movs r0,#1</c>. The engine never resumes a PlayAnim.</summary>
    protected override int ResumeInternal() => 1;

    /// <summary><c>BehaviorPlayAnimSequence::InitInternal</c> (<c>vtable+0x48</c>) 0x005C014E: <c>StartPlayingAnimations</c>, result always 0.</summary>
    protected override void OnStart() => StartPlayingAnimations();

    protected override void OnTriggerResolved(AnimationTrigger trigger, string clip) => LastSelected = clip;

    /// <summary>
    /// <c>StartPlayingAnimations</c> 0x005C0158: exactly one trigger is one lift-safe action with
    /// <c>numLoops = num_loops</c> (0x005C0174), timeout 60.0f, tracksToLock 0, completed by
    /// <c>CallToListeners</c> (<c>StartActing</c> with the PMF, 0x005C0190); anything else zeroes the loop index
    /// (0x005C01AC) and takes <c>StartSequenceLoop</c>.
    /// </summary>
    // fidelity: M8-005
    private void StartPlayingAnimations()
    {
        if (_triggers.Count == 1)
        {
            ConstructActions();
            int handle = StartActing();
            if (handle == 0) return;
            RunTriggerAction(handle, _triggers[0], _ => CallToListeners(), AnimationTrack.None,
                             TriggerAnimationTimeoutSec, NumLoops, liftSafe: true);
            return;
        }
        _loopIndex = 0;
        StartSequenceLoop();
    }

    /// <summary>
    /// <c>StartSequenceLoop</c> 0x005C0294: nothing while the loop index is at or above <c>num_loops</c>
    /// (signed <c>bge</c>, 0x005C02A8..0x005C02AE); else one compound sequential action of lift-safe actions
    /// (numLoops 1, tracksToLock 0, 60.0f; <c>AddAction(.., ignoreFailure 0, 0)</c>), the index incremented
    /// (0x005C0310..0x005C031A), started with a completion that runs <see cref="CallToListeners"/> and this again.
    /// </summary>
    // fidelity: M8-005
    private void StartSequenceLoop()
    {
        if (_loopIndex >= NumLoops) return;
        ConstructActions();
        int handle = StartActing();
        if (handle == 0) return;
        _loopIndex++;
        RunSequenceChild(handle, 0);
    }

    /// <summary>
    /// Building the lift-safe actions (one for the single-trigger path, one per trigger for a loop, all of them when the loop
    /// starts - 0x005C02CA..0x005C030E): each constructor resolves its trigger to an animation group
    /// (<c>SetAnimGroupFromTrigger</c> 0x0054432C: <c>HasAnimationForTrigger</c> 0x0054433E, <c>GetAnimationForTrigger</c>
    /// 0x00544350) and warns when the group is empty (0x005443AC). The clip is chosen later, when the action starts
    /// (<c>TriggerAnimationAction::Init</c> 0x0054443C), which is <see cref="SteppedBehavior.RunTriggerAction"/>.
    /// </summary>
    private void ConstructActions()
    {
        foreach (var trigger in _triggers)
            if (string.IsNullOrEmpty(Context.Triggers.GroupFor(trigger)))
                Log($"TriggerAnimationAction.SetAnimGroupFromTrigger: the animation group for {trigger} is empty");
    }

    private void RunSequenceChild(int handle, int i)
    {
        if (i >= _triggers.Count) { EndSequence(handle); return; }
        RunTriggerAction(0, _triggers[i], outcome =>
        {
            // ignoreFailure = 0: a failed child ends the sequence, which returns the failure.
            if (!outcome.Success) { EndSequence(handle); return; }
            RunSequenceChild(handle, i + 1);
        }, AnimationTrack.None, TriggerAnimationTimeoutSec, numLoops: 1, liftSafe: true);
    }

    /// <summary>The sequence action has ended (the closure at vtable 0x010269C8, operator() 0x005C0552).</summary>
    private void EndSequence(int handle)
    {
        ActingEnded(handle);
        CallToListeners();
        StartSequenceLoop();
    }

    /// <summary>
    /// <c>BehaviorPlayAnimSequence::CallToListeners</c>: the listener set is at +0x130 and
    /// <c>AddListener</c> (<c>vtable+0x30</c>) fills it. Nothing in this stack registers a listener, so there is
    /// none to call.
    /// </summary>
    private void CallToListeners() { }

    /// <summary>
    /// Ends the animation this behaviour started, and only that one.
    ///
    /// Stopping a behaviour must not leave its animation running, and equally must not cancel an unrelated
    /// animation that has since replaced it. The generation token the scheduler hands back identifies
    /// exactly which animation was started, so StopIfCurrent is a no-op once something else has taken over.
    /// </summary>
    internal static void StopOwnAnimation(ref CozmoAnimations? animations, ref long generation,
                                          ref bool owns, object gate)
    {
        CozmoAnimations? target;
        long gen;
        lock (gate)
        {
            if (!owns) return;
            owns = false;
            target = animations;
            gen = generation;
            animations = null;
        }
        target?.StopIfCurrent(gen);
    }
}

/// <summary>
/// A behaviour that plays whatever animation it is handed.
///
/// The shipped <c>PlayArbitraryAnim</c> config carries no animation at all, because the engine's version
/// is told which one to play by whoever starts it. This keeps that shape: the clip is a property rather
/// than configuration.
/// </summary>
public sealed class PlayArbitraryAnimBehavior : IBehavior
{
    private readonly object _gate = new();
    private CozmoAnimations? _animations;
    private long _generation;
    private bool _owns;
    private volatile bool _finished = true;

    public string Id => "PlayArbitraryAnim";
    public string Class => "PlayArbitraryAnim";

    /// <summary>The clip to play. Nothing runs until this is set.</summary>
    public string? ClipName { get; set; }

    /// <summary>The flat score (+0x100); zero unless a caller sets it (<c>IBehavior::IBehavior</c> 0x005BBD28).</summary>
    // fidelity: M8-004
    public double Score { get; set; }

    public bool IsRunnable(BehaviorContext context) =>
        ClipName is not null && context.Robot.Animations.Library?.HasClip(ClipName) == true;

    public double EvaluateScore(BehaviorContext context) => IsRunnable(context) ? Score : 0;

    public Task StartAsync(BehaviorContext context, BehaviorScope scope, CancellationToken cancel)
    {
        _finished = false;
        var lib = context.Robot.Animations.Library;
        if (ClipName is null || lib is null || !lib.HasClip(ClipName))
        {
            _finished = true;
            return Task.CompletedTask;
        }
        scope.LockTracks(lib.GetClip(ClipName).Tracks);
        var ticket = context.Robot.Animations.PlayTracked(ClipName);
        if (ticket is null) { _finished = true; return Task.CompletedTask; }
        lock (_gate)
        {
            _animations = context.Robot.Animations;
            _generation = ticket.Generation;
            _owns = true;
        }
        ticket.Completion.ContinueWith(_ =>
        {
            lock (_gate) _owns = false;
            _finished = true;
        }, TaskScheduler.Default);
        return Task.CompletedTask;
    }

    public bool Update(BehaviorContext context, double nowMs) => !_finished;

    public void Stop(BehaviorStopReason reason)
    {
        _finished = true;
        PlayAnimBehavior.StopOwnAnimation(ref _animations, ref _generation, ref _owns, _gate);
    }
}

/// <summary>
/// A behaviour that reacts to something the robot reports about itself.
///
/// Only the reactions M4 actually detects are built this way — cliff, pick-up and charger — because a
/// reaction whose cause nothing reports could never run. It defers to <see cref="ReactionTable"/> for
/// which animation to play, so it inherits that table's recorded uncertainty rather than adding one.
/// </summary>
public sealed class ReactBehavior : IBehavior
{
    private readonly ReactionTable _table;
    private readonly Func<CozmoRobot, bool> _condition;
    private readonly object _gate = new();
    private CozmoAnimations? _animations;
    private long _generation;
    private bool _owns;
    private volatile bool _finished = true;

    // fidelity: M8-004
    // No in-code score: the engine's default is zero (IBehavior::IBehavior 0x005BBD28; EvaluateScoreInternal 0x005BEEC2).
    public ReactBehavior(string id, string behaviorClass, ReactionTrigger trigger,
                         Func<CozmoRobot, bool> condition, ReactionTable? table = null)
    {
        Id = id;
        Class = behaviorClass;
        Trigger = trigger;
        _condition = condition;
        _table = table ?? ReactionTable.Default;
    }

    public string Id { get; }
    public string Class { get; }
    public ReactionTrigger Trigger { get; }

    /// <summary>The flat score (+0x100); zero unless a caller sets it, as <c>IBehavior::IBehavior</c> 0x005BBD28 leaves it.</summary>
    // fidelity: M8-004
    public double Score { get; set; }

    public string? LastSelected { get; private set; }

    public bool IsRunnable(BehaviorContext context) =>
        _table.For(Trigger) is not null
        && context.Robot.Animations.Library is not null
        && _condition(context.Robot);

    public double EvaluateScore(BehaviorContext context) => IsRunnable(context) ? Score : 0;

    public Task StartAsync(BehaviorContext context, BehaviorScope scope, CancellationToken cancel)
    {
        _finished = false;
        LastSelected = null;
        var entry = _table.For(Trigger);
        var lib = context.Robot.Animations.Library;
        if (entry is null || lib is null) { _finished = true; return Task.CompletedTask; }

        var resolved = context.Triggers.Resolve(entry.Animation, lib, context.Random);
        if (!resolved.Resolved) { _finished = true; return Task.CompletedTask; }

        scope.LockTracks(lib.GetClip(resolved.Selected!).Tracks);
        // A reaction should not be interrupted by another reaction part way through. Where the shipped
        // class has its own 21-byte lock table (M7-014), take exactly that set through the manager; a
        // class without one falls back to the scope's arbiter-wide lock.
        var lockTable = ReactionLockTables.For(Class);
        if (lockTable is not null) scope.SmartDisableReactionsWithLock(Id, lockTable);
        else scope.DisableReactions();
        LastSelected = resolved.Selected;
        var ticket = context.Robot.Animations.PlayTracked(resolved.Selected!);
        if (ticket is null) { _finished = true; return Task.CompletedTask; }
        lock (_gate)
        {
            _animations = context.Robot.Animations;
            _generation = ticket.Generation;
            _owns = true;
        }
        ticket.Completion.ContinueWith(_ =>
        {
            lock (_gate) _owns = false;
            _finished = true;
        }, TaskScheduler.Default);
        return Task.CompletedTask;
    }

    public bool Update(BehaviorContext context, double nowMs) => !_finished;

    public void Stop(BehaviorStopReason reason)
    {
        _finished = true;
        PlayAnimBehavior.StopOwnAnimation(ref _animations, ref _generation, ref _owns, _gate);
    }
}

/// <summary>
/// The behaviours this stack can actually run, built from the shipped configs.
///
/// Twenty of the 178 shipped behaviours are built here without an OBB: eight play one animation named in
/// their config, two react to raw robot reports, and ten are the M10 reactions to derived robot state
/// (the off-treads classifier, the shake detector, the unexpected-movement detector, the robot's own
/// calibration reports and the mood). The 39 <c>Singing</c> behaviours are built from their shipped configs
/// by <see cref="Singing"/>. The cube-moved reaction is built by <see cref="Reactions"/> only when a world
/// model is attached, because every step of it needs the cube's located pose. The rest are blocked on
/// cubes, vision or navigation; see `BEHAVIOR_INVENTORY.md` for each one's blocker. Nothing is stubbed with a
/// fake input to make this list longer.
/// </summary>
public static class ShippedBehaviors
{
    /// <summary>The 39 Singing behaviours, from the OBB's behaviour configs. Empty when the OBB is not there.</summary>
    public static IReadOnlyList<IBehavior> Singing(string obbRoot) => SingingBehavior.LoadShipped(obbRoot);

    /// <summary>Every shipped PlayAnim config with triggers, from the OBB. Empty when the OBB is not there.</summary>
    public static IReadOnlyList<IBehavior> PlayAnims(string obbRoot, List<string>? problems = null) =>
        PlayAnimBehavior.LoadShipped(obbRoot, problems);

    /// <summary>
    /// The 13 shipped manipulation behaviours (M12), by their config ids: two PickUpCube, four PutDownBlock,
    /// four RollBlock, two StackBlocks and the PickUpAndPutDownCube spark. Their parameters beyond the class
    /// (block configurations to ignore, isBlockRotationImportant) are read where the class uses them.
    /// </summary>
    public static IReadOnlyList<IBehavior> Manipulation(Cozmo.Robot.Manipulation.ManipulationSystem m) => new IBehavior[]
    {
        new PickUpCubeBehavior(m, "SparksPickupSingleCubeForPyramid"),
        new PickUpCubeBehavior(m, "SparksPickupSingleCubeToStack"),
        new PutDownBlockBehavior(m, "PutDownBlock"),
        new PutDownBlockBehavior(m, "PutDownBlockNothingToDo"),
        new PutDownBlockBehavior(m, "PyramidPutDownBlock"),
        new PutDownBlockBehavior(m, "SparksPutDownBlock"),
        new RollBlockBehavior(m, "RollBlockOnSide"),
        new RollBlockBehavior(m, "RollBlockOnSideLowScore"),
        new RollBlockBehavior(m, "Hiking_RollCube", blockRotationImportant: false),
        new RollBlockBehavior(m, "SparksRollBlock", blockRotationImportant: false),
        new StackBlocksBehavior(m, "StackBlocks"),
        new StackBlocksBehavior(m, "SparksStackBlock"),
        new PickUpAndPutDownCubeBehavior(m, "SparksPickUpCube"),
    };

    /// <summary>
    /// The M13 behaviours on the planner, the flip action, the block configurations, the whiteboard, the
    /// workouts and the charger: 24 shipped configs. The workout behaviours are runnable only when
    /// <see cref="Cozmo.Robot.Manipulation.ManipulationSystem.Workouts"/> is loaded from the OBB.
    /// </summary>
    public static IReadOnlyList<IBehavior> Navigation(Cozmo.Robot.Manipulation.ManipulationSystem m) => new IBehavior[]
    {
        new KnockOverCubesBehavior(m, "KnockOverCubes", minimumStackHeight: 3),
        new KnockOverCubesBehavior(m, "SparksKnockOverCubes", minimumStackHeight: 2),
        new PopAWheelieBehavior(m, "PopAWheelie"),
        new PopAWheelieBehavior(m, "SparksPopAWheelie"),
        new RamIntoBlockBehavior(m, "RamIntoBlock"),
        new CubeLiftWorkoutBehavior(m, "CubeLiftWorkout"),
        new CubeLiftWorkoutBehavior(m, "SparksCubeLiftWorkout"),
        new BuildPyramidBaseBehavior(m, "BuildPyramidBase"),
        new BuildPyramidBaseBehavior(m, "BuildPyramid", buildTop: true),
        new RespondPossiblyRollBehavior(m, "PyramidRespondPossiblyRoll"),
        OnConfigSeenBehavior.RespondToPyramidBase(m),
        new CantHandleTallStackBehavior(m, "CantHandleTallStack"),
        new CheckForStackAtIntervalBehavior(m, "SparksCheckForStackAtInterval", 15),
        ReactToConfigurationBehavior.ReactToPyramid(m),
        ReactToConfigurationBehavior.ReactToStackOfCubes(m),
        new ThinkAboutBeaconsBehavior(m, "Hiking_ThinkAboutBeacons", 175),
        new ThinkAboutBeaconsBehavior(m, "SparksThinkAboutBeacons", 75),
        new BringCubeToBeaconBehavior(m, "Hiking_BringCubeToBeacon", 45),
        new BringCubeToBeaconBehavior(m, "SparksBringCubeToBeacon", 5),
        new DriveOffChargerBehavior(m, "DriveOffCharger", 60),
        new DriveOffChargerBehavior(m, "Hiking_DriveOffCharger", 45),
        new ReactToOnChargerBehavior("ReactToOnCharger", 300, 330),
        new ReactToOnChargerBehavior("VC_GoToSleep", 300, 330, triggeredFromVoiceCommand: true),
        new MountChargerBehavior(m, "DockingTestSimple"),
        ReactToFrustrationBehavior.Major(m),
    };

    /// <summary>
    /// The M14 face behaviours: 14 shipped configs transcribed on <see cref="Cozmo.Robot.Vision.FaceWorld"/> and
    /// the face actions. They are runnable only while a face detector is attached to the vision system; the
    /// stock detector is Omron's OKAO library, which this stack does not have, so the inventory counts them
    /// separately from the implemented set.
    /// </summary>
    public static IReadOnlyList<IBehavior> Faces(Cozmo.Robot.Vision.VisionSystem v, Cozmo.Robot.Manipulation.ManipulationSystem? m = null)
    {
        var list = new List<IBehavior>
        {
            new PlayAnimWithFaceBehavior(v, "FeedingPlayRequestAtFace", new[] { AnimationTrigger.NeedsMildLowEnergyRequest }),
            new PlayAnimWithFaceBehavior(v, "FeedingPlayRequestAtFace_Severe", new[] { AnimationTrigger.NeedsSevereLowEnergyRequest }),
            new PlayAnimWithFaceBehavior(v, "VC_AlrightyResponse", new[] { AnimationTrigger.VC_Alrighty }),
            new PlayAnimWithFaceBehavior(v, "VC_HowAreYouDoing_AllGood", new[] { AnimationTrigger.VC_HowAreYouDoing_AllGood }),
            // the three needs variants ship with NeutralFace and are "overridden programmatically" from the needs level (the needs system is M15's)
            new PlayAnimWithFaceBehavior(v, "VC_HowAreYouDoing_Energy", new[] { AnimationTrigger.NeutralFace }),
            new PlayAnimWithFaceBehavior(v, "VC_HowAreYouDoing_Play", new[] { AnimationTrigger.NeutralFace }),
            new PlayAnimWithFaceBehavior(v, "VC_HowAreYouDoing_Repair", new[] { AnimationTrigger.NeutralFace }),
            new AcknowledgeFaceBehavior(v, "AcknowledgeFace"),
            new InteractWithFacesBehavior(v, "InteractWithFaces", m),
            new InteractWithFacesBehavior(v, "MeetCozmo_InteractWithFaces", m),
            new SearchForFaceBehavior(v, "VC_SearchForFace"),
            new ReactToPetBehavior(v, "ReactToPet"),
        };
        if (m is not null)
        {
            list.Add(new DriveToFaceBehavior(v, m, "VC_ComeHere"));
            list.Add(new PyramidThankYouBehavior(v, m, "PyramidThankYou"));
        }
        return list;
    }

    /// <summary>Creates the config-free runnable set, matching the shipped configs' ids and classes.</summary>
    public static IReadOnlyList<IBehavior> Implementable() => new IBehavior[]
    {
        // PlayAnim behaviours: the config names the trigger, so these are faithful to the shipped data.
        new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup }),
        new PlayAnimBehavior("ReactToObstacle", "PlayAnim", new[] { AnimationTrigger.ReactToObstacle }) { WantsToRunStrategy = "ObstacleDetected" },
        new PlayArbitraryAnimBehavior(),
        // The feeding game's reaction animations: PlayAnim configs under feeding/feedingAnims/. They mention a
        // cube in their names, but each one only plays the trigger it names; the game that decides when is
        // the app's, not this stack's.
        new PlayAnimBehavior("FeedingReactCubeShake", "PlayAnim", new[] { AnimationTrigger.FeedingReactToShake_Normal }),
        new PlayAnimBehavior("FeedingReactCubeShake_Severe", "PlayAnim", new[] { AnimationTrigger.FeedingReactToShake_Severe }),
        new PlayAnimBehavior("FeedingReactFullCube", "PlayAnim", new[] { AnimationTrigger.FeedingReactToFullCube_Normal }),
        new PlayAnimBehavior("FeedingReactFullCube_Severe", "PlayAnim", new[] { AnimationTrigger.FeedingReactToFullCube_Severe }),
        new PlayAnimBehavior("FeedingReactSeeCharged", "PlayAnim", new[] { AnimationTrigger.FeedingReactToSeeCube_Normal }),
        new PlayAnimBehavior("FeedingReactSeeCharged_Severe", "PlayAnim", new[] { AnimationTrigger.FeedingReactToSeeCube_Severe }),

        // fidelity: M7-019, M7-015
        // The engine's BehaviorReactToCliff and BehaviorReactToPickup state machines (CliffPickupBehaviors.cs), not a one-animation stand-in.
        new ReactToCliffBehavior(),
        new ReactToPickupBehavior(),

        // M10: reactions to derived robot state, transcribed from the engine's BehaviorReactToX classes.
        new ReactToRobotOnBackBehavior(),
        new ReactToRobotOnFaceBehavior(),
        new ReactToRobotOnSideBehavior(),
        new ReactToPlacedOnSlopeBehavior(),
        new ReactToReturnedToTreadsBehavior(),
        new ReactToRobotShakenBehavior(),
        new ReactToUnexpectedMovementBehavior(),
        new ReactToMotorCalibrationBehavior(),
        ReactToFrustrationBehavior.Minor(),
    };

    // fidelity: M10-003, M10-004, M7-002
    /// <summary>
    /// The reaction registrations for a <see cref="BehaviorManager"/>, driven by the shipped
    /// <c>reactionTrigger_behavior_map.json</c> (M7-002): each map
    /// entry's <c>reactionTrigger</c> and <c>behaviorID</c> are looked up in the behaviours and strategies the
    /// stack builds, so the trigger -> behaviour dispatch comes from the shipped file rather than a hard-coded
    /// list. An entry whose behaviour class is not built is reported in <paramref name="unbound"/> and not
    /// registered.
    ///
    /// Each trigger's engine strategy (C12, gap1 8, gap2) is paired with the behaviour the map names. The
    /// cube-moved entry needs a world model, the face and pet entries a vision system. Not registered: FistBump,
    /// Hiccup and Sparked (their behaviours exist, built from their configs by <see cref="BehaviorFactory"/>, but the trigger strategies
    /// ReactionTriggerStrategyFistBump, ...Hiccup and ...Sparked are M10's and have no C# counterpart).
    ///
    /// A missing or empty map registers nothing (<c>LoadReactionTriggerMap</c> 0x00520bc8 leaves it empty on a failed read, and
    /// <c>InitReactionTriggerMap</c> 0x005a16e4 walks nothing), so <paramref name="obbRoot"/> is required to register any reaction. The map's
    /// own parameters (<c>genericStrategyParams</c>, <c>frustrationParams</c>, <c>behaviorObjectiveTriggerParams</c>, <c>hiccupParams</c>) are
    /// parsed by <see cref="ReactionTriggerMap"/> but the strategies here are still built from literals: which parameter feeds which strategy
    /// field is not settled by the M7 rows (the strategy classes are M10's), so it is MISSING, not wired.
    /// </summary>
    public static IReadOnlyList<BehaviorManager.ReactionRegistration> Reactions(CozmoRobot robot, ICubeLocator? cubes = null,
                                                                                Func<double>? clockSec = null, Cozmo.Robot.Vision.VisionSystem? vision = null,
                                                                                RamIntoBlockBehavior? ramIntoBlock = null, Cozmo.Robot.Manipulation.AIWhiteboard? whiteboard = null,
                                                                                string? obbRoot = null, Cozmo.Robot.Manipulation.ManipulationSystem? m = null,
                                                                                List<string>? unbound = null)
    {
        var strategies = ShippedReactionStrategies.ForRobot(robot, clockSec).ToDictionary(s => s.Trigger);
        var frustration = (FrustrationStrategy)strategies[ReactionTrigger.Frustration];

        // behaviourID -> (trigger, behaviour, strategy). One row per shipped config id this stack builds.
        var built = new List<(string Id, ReactionTrigger Trigger, IBehavior Behavior, IReactionTriggerStrategy Strategy)>
        {
            // fidelity: M7-019, M7-015, M7-021
            ("ReactToCliff", ReactionTrigger.CliffDetected,
                new ReactToCliffBehavior(robot)
                {
                    // M13's DriveStraightAction(robot, distance, speed) runs the back-up drive when a manipulation system is attached.
                    DriveStraight = m is null ? null : (distance, speed, ct) => new Cozmo.Robot.Manipulation.DriveStraightAction(m, distance, speed).RunAsync(ct),
                },
                strategies[ReactionTrigger.CliffDetected]),
            ("ReactToPickup", ReactionTrigger.RobotPickedUp,
                new ReactToPickupBehavior(vision)
                {
                    // CarryingComponent::SetCarriedObjectAsUnattached(true): the manipulation system's wiring on the sensors (ManipulationSystem.cs:47)
                    SetCarriedObjectAsUnattached = () => robot.Sensors.UnattachCarriedObjectIfCarrying?.Invoke(),
                },
                strategies[ReactionTrigger.RobotPickedUp]),
            ("ReactToRobotOnBack", ReactionTrigger.RobotOnBack, new ReactToRobotOnBackBehavior(), strategies[ReactionTrigger.RobotOnBack]),
            ("ReactToRobotOnFace", ReactionTrigger.RobotOnFace, new ReactToRobotOnFaceBehavior(), strategies[ReactionTrigger.RobotOnFace]),
            ("ReactToRobotOnSide", ReactionTrigger.RobotOnSide, new ReactToRobotOnSideBehavior(), strategies[ReactionTrigger.RobotOnSide]),
            ("ReactToPlacedOnSlope", ReactionTrigger.RobotPlacedOnSlope, new ReactToPlacedOnSlopeBehavior(), strategies[ReactionTrigger.RobotPlacedOnSlope]),
            ("ReactToReturnedToTreads", ReactionTrigger.ReturnedToTreads, new ReactToReturnedToTreadsBehavior(), strategies[ReactionTrigger.ReturnedToTreads]),
            ("ReactToRobotShaken", ReactionTrigger.RobotShaken, new ReactToRobotShakenBehavior(), strategies[ReactionTrigger.RobotShaken]),
            ("ReactToUnexpectedMovement", ReactionTrigger.UnexpectedMovement, new ReactToUnexpectedMovementBehavior(), strategies[ReactionTrigger.UnexpectedMovement]),
            ("ReactToMotorCalibration", ReactionTrigger.MotorCalibration, new ReactToMotorCalibrationBehavior(), strategies[ReactionTrigger.MotorCalibration]),
            ("ReactToFrustrationMinor", ReactionTrigger.Frustration, ReactToFrustrationBehavior.Minor(frustration), frustration),
            ("ReactToImpact", ReactionTrigger.RobotFalling, new ReactToImpactBehavior(robot), strategies[ReactionTrigger.RobotFalling]),
            ("ReactToOnCharger", ReactionTrigger.PlacedOnCharger, new ReactToOnChargerBehavior(), strategies[ReactionTrigger.PlacedOnCharger]),
        };
        if (m is not null)
        {
            var major = new FrustrationStrategy(maxConfidence: -0.9f, cooldownSec: 0f, clockSec);
            built.Add(("ReactToFrustrationMajor", ReactionTrigger.Frustration, ReactToFrustrationBehavior.Major(m, major), major));
        }

        if (cubes is null && vision is not null) cubes = vision.Locator;
        if (cubes is not null)
        {
            var behavior = new AcknowledgeCubeMovedBehavior(cubes);
            built.Add((behavior.Id, ReactionTrigger.CubeMoved, behavior,
                       new CubeMovedReactionStrategy(robot, behavior, cubes, vision?.World)));
        }
        if (vision is not null)
        {
            var ack = new AcknowledgeObjectBehavior(vision.World, vision.Locator);
            // Robot::GetLastImageTimeStamp is the vision system's last raw frame timestamp (M11 interface); the carried and
            // docking object ids are M12's and not attached here.
            built.Add((ack.Id, ReactionTrigger.ObjectPositionUpdated, ack,
                       new ObjectPositionUpdatedStrategy(vision.World, ack, robot)
                       {
                           LastImageTimestamp = () => vision.LastRawFrameTimestamp ?? 0,
                       }));

            // FacePositionUpdated -> AcknowledgeFace and PetInitialDetection -> ReactToPet. With no face detector the
            // worlds stay empty and neither ever fires (the OKAO boundary).
            var ackFace = new AcknowledgeFaceBehavior(vision);
            built.Add((ackFace.Id, ReactionTrigger.FacePositionUpdated, ackFace,
                       new FacePositionUpdatedStrategy(vision.Faces, ackFace, () => vision.History.Latest?.RobotPose, clockSec ?? (() => robot.Engine.Timer.Seconds))));
            var reactToPet = new ReactToPetBehavior(vision);
            built.Add((reactToPet.Id, ReactionTrigger.PetInitialDetection, reactToPet,
                       new PetInitialDetectionStrategy(vision.Pets, reactToPet, clockSec ?? (() => robot.Engine.Timer.Seconds))));
        }
        if (ramIntoBlock is not null && whiteboard is not null)
            built.Add(("RamIntoBlock", ReactionTrigger.NoPreDockPoses, ramIntoBlock,
                       new NoPreDockPosesStrategy(whiteboard, ramIntoBlock)));

        // RobotDataLoader::LoadReactionTriggerMap 0x00520bc8 reads the map through readAsJson (0x00520c2e); on a failed read it logs sErrorF
        // "Failed to read '%s'" (0x00520c54), sets the error flag (0x00520c8a) and leaves the map empty, and BehaviorManager::InitReactionTriggerMap
        // 0x005a16e4 then iterates nothing: a missing or empty map registers NO reaction. There is no fallback that registers every built one.
        var map = obbRoot is null ? Array.Empty<ReactionMapEntry>() : ReactionTriggerMap.Load(obbRoot);
        if (map.Count == 0)
        {
            // sErrorF "Failed to read '%s'" (0x00520c54); the engine's event name is not in the inventory, so the line carries none
            robot.Engine.Log($"error: Failed to read '{ReactionTriggerMap.RelativePath}'");
            return new List<BehaviorManager.ReactionRegistration>();
        }

        // Bind from the map: only the entries the stack actually built, in the map's own order.
        var byId = built.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.ToList());
        var registrations = new List<BehaviorManager.ReactionRegistration>();
        foreach (var e in map)
        {
            var match = byId.TryGetValue(e.BehaviorId, out var candidates)
                ? candidates.FirstOrDefault(c => c.Trigger == e.Trigger)
                : default;
            if (match.Behavior is not null)
                registrations.Add(new BehaviorManager.ReactionRegistration(match.Strategy, match.Behavior));
            else
                unbound?.Add($"{e.Trigger} -> {e.BehaviorId}");
        }
        return registrations;
    }
}
