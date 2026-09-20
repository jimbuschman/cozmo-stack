using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

/// <summary>
/// A behaviour that plays one animation, chosen from the shipped trigger map.
///
/// This is the reconstruction of the engine's config-driven <c>PlayAnim</c> class
/// (<c>BehaviorPlayAnimSequence</c>), which names its animation in an <c>animTriggers</c> field rather than
/// in code. Where a config lists more than one trigger the first that resolves is used. It is <b>not</b> the
/// <c>PlayAnimWithFace</c> class: <c>BehaviorPlayAnimSequenceWithFace::InitInternal</c> (0x005C0648) runs a
/// <c>TurnTowardsFaceAction</c> (0x005C0686) before the animation, so that class needs a tracked face and is
/// left to the vision milestone. An earlier version of this comment claimed both; M10 read the binary.
///
/// It claims the tracks its clip touches for as long as it runs, through the scope, so the idle layer
/// yields those tracks and only those, which is what the engine's <c>SmartLockTracks</c> does.
/// </summary>
public sealed class PlayAnimBehavior : IBehavior
{
    /// <summary>
    /// Every shipped <c>PlayAnim</c> config with an <c>animTriggers</c> list, built from the OBB. A trigger
    /// name the generated <see cref="AnimationTrigger"/> enum does not know is skipped and reported in
    /// <paramref name="problems"/> rather than guessed at.
    /// </summary>
    public static IReadOnlyList<PlayAnimBehavior> LoadShipped(string obbRoot, List<string>? problems = null)
    {
        var list = new List<PlayAnimBehavior>();
        var dir = Path.Combine(obbRoot, "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "behaviors");
        if (!Directory.Exists(dir)) return list;
        foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
        {
            var text = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(f), "//[^\n\r]*", "");
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (!root.TryGetProperty("behaviorClass", out var cls) || cls.GetString() != "PlayAnim") continue;
                if (!root.TryGetProperty("animTriggers", out var triggers) || triggers.ValueKind != System.Text.Json.JsonValueKind.Array) continue;
                var id = root.GetProperty("behaviorID").GetString()!;
                var parsed = new List<AnimationTrigger>();
                foreach (var t in triggers.EnumerateArray())
                {
                    var name = t.GetString();
                    if (name is not null && Enum.TryParse<AnimationTrigger>(name, out var trigger)) parsed.Add(trigger);
                    else problems?.Add($"{id}: animTrigger '{name}' is not in the AnimationTrigger enum");
                }
                if (parsed.Count == 0) { problems?.Add($"{id}: no usable animTriggers"); continue; }
                list.Add(new PlayAnimBehavior(id, "PlayAnim", parsed));
            }
            catch (System.Text.Json.JsonException e) { problems?.Add($"{Path.GetFileName(f)}: {e.Message}"); }
        }
        return list;
    }

    private readonly IReadOnlyList<AnimationTrigger> _triggers;
    private readonly object _gate = new();
    private CozmoAnimations? _animations;
    private long _generation;
    private bool _owns;
    private volatile bool _finished;

    public PlayAnimBehavior(string id, string behaviorClass, IEnumerable<AnimationTrigger> triggers,
                            double score = 1.0)
    {
        Id = id;
        Class = behaviorClass;
        _triggers = triggers.ToList();
        Score = score;
    }

    public string Id { get; }
    public string Class { get; }

    /// <summary>How much this wants to run. Configs carry no score, so a caller sets it.</summary>
    public double Score { get; set; }

    /// <summary>The animation actually selected on the last start, for tracing.</summary>
    public string? LastSelected { get; private set; }

    public bool IsRunnable(BehaviorContext context) =>
        context.Robot.Animations.Library is not null && _triggers.Count > 0;

    public double EvaluateScore(BehaviorContext context) => Score;

    public Task StartAsync(BehaviorContext context, BehaviorScope scope, CancellationToken cancel)
    {
        _finished = false;
        LastSelected = null;
        var lib = context.Robot.Animations.Library;
        if (lib is null) { _finished = true; return Task.CompletedTask; }

        foreach (var trigger in _triggers)
        {
            var resolved = context.Triggers.Resolve(trigger, lib, context.Random);
            if (!resolved.Resolved) continue;

            var clip = lib.GetClip(resolved.Selected!);
            scope.LockTracks(clip.Tracks);
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

        // Nothing resolved. Finishing immediately is the honest outcome; it is not an error and it is
        // not a reason to play something else.
        _finished = true;
        return Task.CompletedTask;
    }

    public bool Update(BehaviorContext context, double nowMs) => !_finished;

    public void Stop(BehaviorStopReason reason)
    {
        _finished = true;
        StopOwnAnimation(ref _animations, ref _generation, ref _owns, _gate);
    }

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

    /// <summary>How much this wants to run when it has a clip.</summary>
    public double Score { get; set; } = 1.0;

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

    public ReactBehavior(string id, string behaviorClass, ReactionTrigger trigger,
                         Func<CozmoRobot, bool> condition, ReactionTable? table = null, double score = 5.0)
    {
        Id = id;
        Class = behaviorClass;
        Trigger = trigger;
        _condition = condition;
        _table = table ?? ReactionTable.Default;
        Score = score;
    }

    public string Id { get; }
    public string Class { get; }
    public ReactionTrigger Trigger { get; }

    /// <summary>Reactions outscore ordinary behaviours by default, as the engine's preempt them.</summary>
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
        // A reaction should not be interrupted by another reaction part way through.
        scope.DisableReactions();
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
    /// The engine's RobotPickedUp reaction fires on the derived off-treads state being InAir (factory lambda
    /// 0x0060DDCE), not on the raw IS_PICKED_UP flag. Until the classifier is running (it waits for the head
    /// calibration report, as the engine's does) the raw flag stands in, and says so (LOCAL_POLICY fallback).
    /// </summary>
    public static bool PickedUpForReaction(CozmoRobot r) =>
        r.Sensors.OffTreadsClassifierEnabled ? r.Sensors.OffTreadsState == OffTreadsState.InAir : r.Sensors.PickedUp;

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
        new PlayAnimBehavior("ReactToObstacle", "PlayAnim", new[] { AnimationTrigger.ReactToObstacle }),
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

        // Reactions whose cause M4 reports.
        new ReactBehavior("ReactToCliff", "ReactToCliff", ReactionTrigger.CliffDetected,
                          r => r.Sensors.CliffDetectedNow),
        new ReactBehavior("ReactToPickup", "ReactToPickup", ReactionTrigger.RobotPickedUp, PickedUpForReaction),

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

    /// <summary>
    /// The shipped reaction map (<c>reactionTrigger_behavior_map.json</c>) as registrations for a
    /// <see cref="BehaviorManager"/>: each trigger's engine strategy paired with the behaviour the map names,
    /// and its <c>shouldResumeLast</c>. Only triggers whose input this stack has are included; the cube-moved
    /// entry appears when a <paramref name="cubes"/> world model is attached.
    /// </summary>
    public static IReadOnlyList<BehaviorManager.ReactionRegistration> Reactions(CozmoRobot robot, ICubeLocator? cubes = null,
                                                                                Func<double>? clockSec = null, Cozmo.Robot.Vision.VisionSystem? vision = null)
    {
        var strategies = ShippedReactionStrategies.ForRobot(robot, clockSec).ToDictionary(s => s.Trigger);
        var frustration = (FrustrationStrategy)strategies[ReactionTrigger.Frustration];
        var sensors = robot.Sensors;

        var list = new List<BehaviorManager.ReactionRegistration>
        {
            // CliffDetected: shouldResumeLast true; the engine latches CliffEvent / RobotStopped (tags 34, 52)
            new(new LatchedEventStrategy(ReactionTrigger.CliffDetected, latch =>
                {
                    void on(CliffReport _) => latch();
                    sensors.CliffDetected += on;
                    return () => sensors.CliffDetected -= on;
                }, "CreateReactionTriggerStrategy 0x0060D6A4 -> ConfigureRelevantEvents({CliffEvent, RobotStopped}), filter lambda 0x0060DC76", clockSec: clockSec),
                new ReactBehavior("ReactToCliff", "ReactToCliff", ReactionTrigger.CliffDetected, r => r.Sensors.CliffDetectedNow),
                ResumeLast: true),
            new(strategies[ReactionTrigger.RobotPickedUp],
                new ReactBehavior("ReactToPickup", "ReactToPickup", ReactionTrigger.RobotPickedUp, PickedUpForReaction),
                ResumeLast: false),
            new(strategies[ReactionTrigger.RobotOnBack], new ReactToRobotOnBackBehavior(), ResumeLast: false),
            new(strategies[ReactionTrigger.RobotOnFace], new ReactToRobotOnFaceBehavior(), ResumeLast: false),
            new(strategies[ReactionTrigger.RobotOnSide], new ReactToRobotOnSideBehavior(), ResumeLast: false),
            new(strategies[ReactionTrigger.RobotPlacedOnSlope], new ReactToPlacedOnSlopeBehavior(), ResumeLast: false),
            new(strategies[ReactionTrigger.ReturnedToTreads], new ReactToReturnedToTreadsBehavior(), ResumeLast: false),
            new(strategies[ReactionTrigger.RobotShaken], new ReactToRobotShakenBehavior(), ResumeLast: false),
            new(strategies[ReactionTrigger.UnexpectedMovement], new ReactToUnexpectedMovementBehavior(), ResumeLast: true),
            new(strategies[ReactionTrigger.MotorCalibration], new ReactToMotorCalibrationBehavior(), ResumeLast: true),
            new(frustration, ReactToFrustrationBehavior.Minor(frustration), ResumeLast: false),
        };

        if (cubes is null && vision is not null) cubes = vision.Locator;
        if (cubes is not null)
        {
            var behavior = new AcknowledgeCubeMovedBehavior(cubes);
            list.Add(new(new CubeMovedReactionStrategy(robot, behavior, cubes), behavior, ResumeLast: false));
        }
        if (vision is not null)
        {
            // ObjectPositionUpdated -> AcknowledgeObject (shouldResumeLast false in the shipped map)
            var ack = new AcknowledgeObjectBehavior(vision.World, vision.Locator);
            list.Add(new(new ObjectPositionUpdatedStrategy(vision.World, ack), ack, ResumeLast: false));
        }
        return list;
    }
}
