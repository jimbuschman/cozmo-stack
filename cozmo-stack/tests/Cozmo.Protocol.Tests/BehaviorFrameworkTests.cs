using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Tests for the reconstructed behaviour framework: scoped resources, the repetition penalty, and
/// score-based selection.
/// </summary>
public class BehaviorFrameworkTests
{
    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "assets", "animationGroups")))
                return r;
            d = d.Parent;
        }
        return null;
    }

    // ------------------------------------------------------------------ scoped resources

    /// <summary>
    /// The engine's Smart* methods exist so a behaviour cannot leak a lock by forgetting to undo it.
    /// Disposing the scope must release everything it took.
    /// </summary>
    [Fact]
    public void AScopeReleasesEverythingItTook()
    {
        var scope = new BehaviorScope();
        scope.LockTracks(AnimationTrack.Head | AnimationTrack.Lift);
        scope.DisableReactions();
        Assert.Equal(AnimationTrack.Head | AnimationTrack.Lift, scope.LockedTracks);
        Assert.True(scope.ReactionsDisabled);

        scope.Dispose();
        Assert.Equal(AnimationTrack.None, scope.LockedTracks);
        Assert.False(scope.ReactionsDisabled);
    }

    [Fact]
    public void AScopeRunsCustomUndoInReverseOrder()
    {
        var order = new List<int>();
        var scope = new BehaviorScope();
        scope.OnRelease(() => order.Add(1));
        scope.OnRelease(() => order.Add(2));
        scope.Dispose();
        Assert.Equal(new[] { 2, 1 }, order);
    }

    /// <summary>Registering after disposal runs the undo immediately rather than leaking it.</summary>
    [Fact]
    public void RegisteringAfterDisposalStillReleases()
    {
        var scope = new BehaviorScope();
        scope.Dispose();
        bool ran = false;
        scope.OnRelease(() => ran = true);
        Assert.True(ran);
    }

    // ------------------------------------------------------------------ repetition penalty

    /// <summary>
    /// mood_config.json's defaultRepetitionPenalty runs from (0 s, 0.0) to (30 s, 1.0): a behaviour that
    /// just ran scores nothing and recovers linearly over thirty seconds.
    /// </summary>
    [Fact]
    public void TheRepetitionPenaltyMatchesTheShippedCurve()
    {
        var p = new RepetitionPenalty();
        Assert.Equal(1.0, p.For("X", 0));
        p.Ran("X", 100);
        Assert.Equal(0.0, p.For("X", 100), 3);
        Assert.Equal(0.5, p.For("X", 115), 2);
        Assert.Equal(1.0, p.For("X", 130), 3);
        Assert.Equal(1.0, p.For("X", 500), 3);
    }

    [Fact]
    public void ForgettingClearsThePenalty()
    {
        var p = new RepetitionPenalty();
        p.Ran("X", 100);
        p.Forget("X");
        Assert.Equal(1.0, p.For("X", 100), 3);
    }

    // ------------------------------------------------------------------ selection

    private sealed class Fake : IBehavior
    {
        public Fake(string id, double score, bool runnable = true)
        { Id = id; Score = score; Runnable = runnable; }
        public string Id { get; }
        public string Class => "Fake";
        public double Score { get; set; }
        public bool Runnable { get; set; }
        public bool Started, Stopped;
        public BehaviorStopReason? Reason;
        public bool IsRunnable(BehaviorContext c) => Runnable;
        public double EvaluateScore(BehaviorContext c) => Score;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t)
        { Started = true; return Task.CompletedTask; }
        public bool Update(BehaviorContext c, double nowMs) => true;
        public void Stop(BehaviorStopReason r) { Stopped = true; Reason = r; }
    }

    private static BehaviorManager Manager(CozmoRobot robot, string obb)
    {
        var ctx = new BehaviorContext
        {
            Robot = robot,
            Triggers = AnimationTriggerMap.Load(obb),
            Random = new Random(5),
        };
        return new BehaviorManager(ctx);
    }

    [Fact]
    public void TheHighestScoringRunnableBehaviourWins()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        using var m = Manager(robot, obb);

        m.Add(new Fake("low", 1));
        m.Add(new Fake("high", 9));
        m.Add(new Fake("higher-but-not-runnable", 99, runnable: false));

        var s = m.ChooseAndSwitch(0);
        Assert.Equal("high", s.Chosen);
        Assert.Contains(s.Scores, x => x.Id == "higher-but-not-runnable" && x.Note == "not runnable");
    }

    [Fact]
    public void ChoosingNothingIsReportedRatherThanSilent()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        using var m = Manager(robot, obb);
        m.Add(new Fake("none", 0));

        var reported = new List<BehaviorSelection>();
        m.Selected += reported.Add;
        var s = m.ChooseAndSwitch(0);
        Assert.Null(s.Chosen);
        Assert.Single(reported);
        Assert.Contains("no behaviour", s.Reason);
    }

    /// <summary>
    /// The point of the penalty: having run, a behaviour stops winning for a while, so a robot with two
    /// similar options alternates rather than repeating one.
    /// </summary>
    [Fact]
    public void ABehaviourThatJustRanLosesToOneThatHasNot()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        using var m = Manager(robot, obb);
        m.Add(new Fake("a", 5));
        m.Add(new Fake("b", 4));

        Assert.Equal("a", m.ChooseAndSwitch(0).Chosen);
        m.Stop(BehaviorStopReason.Completed, 0);
        Assert.Equal("b", m.ChooseAndSwitch(1).Chosen);
        m.Stop(BehaviorStopReason.Completed, 1);
        Assert.Equal("a", m.ChooseAndSwitch(100).Chosen);
    }

    /// <summary>
    /// The engine has StopWithoutImmediateRepetitionPenalty for exactly this: being interrupted is not
    /// the behaviour's fault, so it is not penalised for it.
    /// </summary>
    [Fact]
    public void AnInterruptedBehaviourIsNotPenalised()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        using var m = Manager(robot, obb);
        m.Add(new Fake("a", 5));
        m.ChooseAndSwitch(0);
        m.Stop(BehaviorStopReason.Interrupted, 0);
        Assert.Equal("a", m.ChooseAndSwitch(1).Chosen);
    }

    // ------------------------------------------------------------------ the shipped behaviours

    /// <summary>
    /// The runnable set is small and honest. If this number grows it should be because a blocker was
    /// removed, not because something was stubbed with a fake input.
    /// </summary>
    [Fact]
    public void OnlyTheBehavioursWhoseInputsExistAreBuilt()
    {
        var built = ShippedBehaviors.Implementable();
        Assert.Equal(5, built.Count);
        Assert.Equal(new[] { "Hiccup", "PlayArbitraryAnim", "ReactToCliff", "ReactToObstacle", "ReactToPickup" },
                     built.Select(b => b.Id).OrderBy(x => x));
    }

    /// <summary>Every built behaviour's id and class must match a shipped config, not be made up.</summary>
    [Fact]
    public void EveryBuiltBehaviourMatchesAShippedConfig()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var dir = Path.Combine(obb, "assets", "cozmo_resources", "config", "engine",
                               "behaviorSystem", "behaviors");
        if (!Directory.Exists(dir)) return;

        var shipped = new Dictionary<string, string>();
        foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            var text = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(f), "//[^\n\r]*", "");
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("behaviorID", out var id) &&
                    doc.RootElement.TryGetProperty("behaviorClass", out var cls))
                    shipped[id.GetString()!] = cls.GetString()!;
            }
            catch (System.Text.Json.JsonException) { }
        }

        foreach (var b in ShippedBehaviors.Implementable())
        {
            Assert.True(shipped.ContainsKey(b.Id), $"{b.Id} is not a shipped behaviorID");
            Assert.Equal(shipped[b.Id], b.Class);
        }
    }

    [Fact]
    public void APlayAnimBehaviourResolvesItsTriggerAgainstTheShippedAssets()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext
        {
            Robot = robot,
            Triggers = AnimationTriggerMap.Load(obb),
            Random = new Random(2),
        };
        var b = new PlayAnimBehavior("Hiccup", "PlayAnim", new[] { AnimationTrigger.Hiccup });
        Assert.True(b.IsRunnable(ctx));

        using var scope = new BehaviorScope();
        b.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
        Assert.NotNull(b.LastSelected);
        Assert.True(robot.Animations.Library!.HasClip(b.LastSelected!));
        Assert.NotEqual(AnimationTrack.None, scope.LockedTracks);
    }
}
