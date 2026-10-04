using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// R-FIX3 stream X (M7-003/005/007/013/014/017/018/021, M8-001/002/003/004/005/008/009/013): the defects the Opus verifications of R-BEH2 recorded.
/// Every expected value below is read from libcozmoEngine.so (the cited addresses: instruction literals, vtable relocations, string pools), never from what the code returns.
/// </summary>
public class RFix3XTests
{
    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (File.Exists(Path.Combine(r, "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "activities_config.json"))) return r;
            d = d.Parent;
        }
        return null;
    }

    private sealed class FakeBehavior : IBehavior
    {
        public FakeBehavior(string id, string cls = "Fake") { Id = id; Class = cls; }
        public string Id { get; }
        public string Class { get; }
        public bool IsRunnable(BehaviorContext c) => true;
        public double EvaluateScore(BehaviorContext c) => 1;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) => Task.CompletedTask;
        public bool Update(BehaviorContext c, double nowMs) => false;
        public void Stop(BehaviorStopReason r) { }
    }

    private sealed class Probe : SteppedBehavior
    {
        public Probe(string id, string cls) : base(id, cls) { }
        protected override void OnStart() { }
        public void Name(string state) => SetStateName(state);
        public IReadOnlyList<string> Lines => Trace;
    }

    private static BehaviorContext Ctx(CozmoRobot robot) => new() { Robot = robot, Triggers = new AnimationTriggerMap(), Random = new Random(3) };

    private static ScoredBehaviorEntry Entry(string id, double flat) => new(id, flat, null, null, null, Array.Empty<EmotionScorer>());

    private static int Bits(float f) => BitConverter.SingleToInt32Bits(f);

    // ------------------------------------------------------------------ M8-013 / M8-004: the scoring chooser's arithmetic

    /// <summary>
    /// ScoringBSRunnableChooser::GetDesiredActiveBehavior 0x0060a44a: the literals loaded at 0x0060a422 (vldr d9 = 0x3FB99999A0000000, the RandDbl bound), 0x0060a428 (vldr s20 =
    /// 0x3DCCCCCD, the running bonus) and 0x0060a42c (vldr s22 = 0x3C23D70A, the running floor), and the initial best 0.0f (0x0060a3fc: vldr s24 = 0).
    /// </summary>
    // fidelity: M8-013, M8-004
    [Fact]
    public void TheScoringChoosersLiteralsAreTheEnginesBits()
    {
        Assert.Equal(0x3FB99999A0000000L, BitConverter.DoubleToInt64Bits(ScoringChooser.DrawBound));
        Assert.Equal(0x3DCCCCCD, Bits(ScoringChooser.RunningBonus));
        Assert.Equal(0x3C23D70A, Bits(ScoringChooser.RunningFloor));
    }

    /// <summary>
    /// 0x0060a49e..0x0060a4bc: a non-running challenger gets GetRNG()->RandDbl(0.1f as double) added in DOUBLE ((double)score + draw, vadd.f64 0x0060a4b8) and narrowed with vcvt.f32.f64
    /// (0x0060a4bc): a draw in [0, 0.1), never the old Random.NextDouble() in [0, 1). The expectation replays the same seeded generator with the engine's formula.
    /// </summary>
    // fidelity: M8-013, M8-004
    [Fact]
    public void ANonRunningChallengerGetsADrawInZeroToPointOneAddedInDoubleAndNarrowedToFloat()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Ctx(robot);
        var b = new FakeBehavior("a");
        var chooser = new ScoringChooser(new[] { Entry("a", 0.5) }, new Dictionary<string, IBehavior> { ["a"] = b }) { Rng = new EngineRandom(42u) };
        var twin = new EngineRandom(42u);
        double bound = BitConverter.Int64BitsToDouble(0x3FB99999A0000000L);
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < 400; i++)
        {
            float got = (float)chooser.GetDesiredActiveBehavior(null, 0, ctx, 0).Scores.Single().Score;
            float expected = (float)(twin.RandDbl(bound) + (double)0.5f);
            Assert.Equal(Bits(expected), Bits(got));
            Assert.InRange(got, 0.5f, 0.6f);                       // [0.5, 0.6): never up to 1.5
            min = Math.Min(min, got); max = Math.Max(max, got);
        }
        Assert.True(max - min > 0.05f, "the draws must spread over the 0.1 range");
        Assert.True(max < 0.6f);
    }

    /// <summary>
    /// The production default is the robot's context RNG (0x0060a49e: the robot argument, then GetRNG 0x004aa6a8), the same one the animation layer holds (M5 R3). The scheduler's context
    /// generator is replaced with a seeded one (its field is readonly and entropy-seeded in production), and the chooser's draws must be that generator's RandDbl(0.1f as double) in order.
    /// </summary>
    // fidelity: M8-013, M8-004
    [Fact]
    public void WithoutASeamTheDrawComesFromTheRobotsContextRng()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Ctx(robot);
        var field = typeof(AnimationScheduler).GetField("_contextRng", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        field.SetValue(robot.Animations.Scheduler, new EngineRandom(1234u));
        var twin = new EngineRandom(1234u);
        var chooser = new ScoringChooser(new[] { Entry("a", 0.5) }, new Dictionary<string, IBehavior> { ["a"] = new FakeBehavior("a") });
        Assert.Null(chooser.Rng);
        double bound = BitConverter.Int64BitsToDouble(0x3FB99999A0000000L);
        for (int i = 0; i < 5; i++)
        {
            float got = (float)chooser.GetDesiredActiveBehavior(null, 0, ctx, 0).Scores.Single().Score;
            Assert.Equal(Bits((float)(twin.RandDbl(bound) + (double)0.5f)), Bits(got));
        }
    }

    /// <summary>
    /// 0x0060a524..0x0060a594: each behaviour's own running flag decides; a second running behaviour with a positive score warns "BehaviorChooser.MultipleRunningBehaviors" (0x0060a548;
    /// "Looks like more than one behavior returned IsRunning(). One of them is '%s'"); a running behaviour that is not the best logs "BehaviorChooser.SwitchBehaviors" (0x0060a61c, channel "Unnamed",
    /// "behavior '%s' has score of %f, so is interrupting running behavior '%s' which scored %f", %f six decimals, 'null' for no best).
    /// </summary>
    // fidelity: M8-013
    [Fact]
    public void TheChooserWarnsOfTwoRunningBehavioursAndLogsTheSwitchWithTheEnginesText()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var logs = new List<string>();
        robot.Engine.LogLine += l => { lock (logs) logs.Add(l); };
        var ctx = Ctx(robot);
        var a = new Probe("a", "Wait"); var b = new Probe("b", "Wait");
        a.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        b.StartAsync(ctx, new BehaviorScope(), default).GetAwaiter().GetResult();
        Assert.True(a.EngineRunning && b.EngineRunning);
        var chooser = new ScoringChooser(new[] { Entry("a", 1.0), Entry("b", 1.0) }, new Dictionary<string, IBehavior> { ["a"] = a, ["b"] = b }) { Rng = new EngineRandom(1u) };
        var d = chooser.GetDesiredActiveBehavior(null, 0, ctx, 0);
        lock (logs) Assert.Contains("warning: BehaviorChooser.MultipleRunningBehaviors: Looks like more than one behavior returned IsRunning(). One of them is 'b'", logs);
        // both running with 1.0 + 0.1f: the first listed stays best (strict >), the later running one (b) is the remembered running behaviour, and a switch is logged
        Assert.Same(a, d.Behavior);
        lock (logs) Assert.Contains(logs, l => l.StartsWith("info: [Unnamed] BehaviorChooser.SwitchBehaviors: behavior 'a' has score of 1.100000, so is interrupting running behavior 'b' which scored 1.100000"));
        Assert.StartsWith("behavior 'a' has score of 1.100000, so is interrupting running behavior 'b' which scored 1.100000", d.Reason);
    }

    /// <summary>
    /// The running challenger: (bonus + score) then + 0.1f (vadd.f32 0x0060a482, 0x0060a486), all f32, and the 0.01f floor (0x0060a48a..0x0060a498): the result is the floor unless the sum
    /// is strictly greater, so a NaN sum (unordered compare) keeps 0.01f.
    /// </summary>
    // fidelity: M8-013, M8-004
    [Theory]
    [InlineData(0.25, 0.5, 0)]            // (0.25f + 0.5f) + 0.1f
    [InlineData(-0.6, 0.5, 1)]            // the sum falls below 0.01f: the floor
    [InlineData(double.NaN, 0.5, 1)]      // NaN: vmovgt not taken, the floor
    public void TheRunningBehavioursScoreIsBonusPlusScorePlusPointOneWithAFloor(double bonusY, double flat, int floor)
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Ctx(robot);
        var b = new FakeBehavior("a");
        var chooser = new ScoringChooser(new[] { Entry("a", flat) }, new Dictionary<string, IBehavior> { ["a"] = b },
                                         scoreBonusForCurrent: new Graph2d(new[] { (0.0, bonusY) }));
        var d = chooser.GetDesiredActiveBehavior(b, 5, ctx, 100);
        var score = (float)d.Scores.Single().Score;
        float expected = floor == 1
            ? BitConverter.Int32BitsToSingle(0x3C23D70A)
            : (0.25f + 0.5f) + BitConverter.Int32BitsToSingle(0x3DCCCCCD);
        Assert.Equal(Bits(expected), Bits(score));
        Assert.Same(b, d.Behavior);
    }

    /// <summary>A NaN score is unordered: vcmpe.f32 s0,#0 then ble (0x0060a452..0x0060a462) takes the skip branch, so nothing is chosen.</summary>
    // fidelity: M8-013
    [Fact]
    public void ANaNScoreIsSkipped()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var chooser = new ScoringChooser(new[] { Entry("a", double.NaN) }, new Dictionary<string, IBehavior> { ["a"] = new FakeBehavior("a") }) { Rng = new EngineRandom(1u) };
        Assert.Null(chooser.GetDesiredActiveBehavior(null, 0, Ctx(robot), 0).Behavior);
    }

    // ------------------------------------------------------------------ M8-001: the runnable-gate slots

    private const string EngineGates =
        "Wait,1,0,1;Dance,0,0,1;Bouncer,0,0,0;Singing,0,0,0;FistBump,0,0,1;GuardDog,0,0,0;PeekABoo,0,0,0;DrivePath,0,0,0;FindFaces,0,0,fn;" +
        "RollBlock,0,0,1;EnrollFace,0,0,0;FeedingEat,0,0,0;LookAround,0,0,0;PickUpCube,0,0,0;ReactToPet,0,0,0;TrackLaser,0,0,0;" +
        "TurnToFace,0,0,0;DriveToFace,0,0,0;FactoryTest,0,0,1;PopAWheelie,0,0,0;StackBlocks,0,0,1;BuildPyramid,0,0,1;EarnedSparks,0,0,1;" +
        "ExpressNeeds,0,0,0;LiftLoadTest,0,0,0;OnConfigSeen,0,0,0;PutDownBlock,0,0,1;RamIntoBlock,0,0,1;ReactToCliff,0,0,1;ReactToImpact,0,0,0;" +
        "ReactToPickup,1,0,1;SearchForFace,0,0,0;FireTruckAlarm,0,0,0;KnockOverCubes,0,0,0;PounceOnMotion,0,0,0;ReactToPyramid,0,0,0;" +
        "ReactToSparked,1,0,0;AcknowledgeFace,0,0,0;CubeLiftWorkout,0,0,1;DriveOffCharger,1,1,0;PyramidThankYou,0,0,0;BuildPyramidBase,0,0,1;" +
        "PlayAnimSequence,0,0,1;ReactToOnCharger,1,1,1;AcknowledgeObject,0,0,1;DockingTestSimple,0,0,0;InteractWithFaces,0,0,0;" +
        "PlayArbitraryAnim,0,0,1;RequestGameSimple,0,0,1;ThinkAboutBeacons,0,0,0;DevTurnInPlaceTest,0,0,0;DriveInDesperation,1,0,0;" +
        "LookForFaceAndCube,0,0,0;OnboardingShowCube,0,1,1;ReactToFrustration,0,0,1;ReactToRobotOnBack,1,0,1;ReactToRobotOnFace,1,0,1;" +
        "ReactToRobotOnSide,1,0,1;ReactToRobotShaken,1,0,1;CantHandleTallStack,0,0,0;ReactToStackOfCubes,0,0,0;RespondPossiblyRoll,0,0,0;" +
        "RespondToRenameFace,0,0,0;AcknowledgeCubeMoved,0,0,1;FeedingSearchForCube,0,0,0;LookInPlaceMemoryMap,0,0,0;PickUpAndPutDownCube,0,0,0;" +
        "ReactToPlacedOnSlope,1,0,1;VisitInterestingEdge,0,0,0;PlayAnimOnNeedsChange,0,0,1;CheckForStackAtInterval,0,0,0;ReactToMotorCalibration,0,0,1;" +
        "ReactToReturnedToTreads,1,0,1;ExploreBringCubeToBeacon,0,0,1;ExploreLookAroundInPlace,0,0,fn;FactoryCentroidExtractor,0,0,1;" +
        "PlayAnimSequenceWithFace,0,0,1;ReactToUnexpectedMovement,0,0,1;ExploreVisitPossibleMarker,0,0,0;";

    /// <summary>
    /// M8-001: the vtable +0x20/+0x24/+0x28 slot of every one of the 79 behaviour classes, read from their vtables (relocation at vtable start + 8 + slot; the tabulated values are the
    /// two-instruction bodies <c>movs r0,#N; bx lr</c>; "fn" is 0x005c1d76, <c>ldrb r0,[r0,#0x120]</c>). The engine class of each BehaviorClass comes from CreateBehavior's own table.
    /// </summary>
    // fidelity: M8-001
    [Fact]
    public void EveryBehaviourClassHasTheValuesItsOwnVtableGives()
    {
        var expected = EngineGates.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Split(',')).ToDictionary(p => p[0], p => (p[1], p[2], p[3]));
        Assert.Equal(79, expected.Count);
        Assert.Equal(79, BehaviorFactory.Table.Count);
        foreach (var entry in BehaviorFactory.Table)
        {
            string key = entry.EngineClass == "IBehavior" ? "Wait" : entry.EngineClass["Behavior".Length..];
            var (s20, s24, s28) = expected[key];
            Assert.True(EngineRunnableGates.Table.TryGetValue(entry.Class.ToString(), out var got), entry.Class.ToString());
            Assert.Equal(s20 == "1", got.Slot20);
            Assert.Equal(s24 == "1", got.Slot24);
            if (s28 == "fn") Assert.Null(got.Slot28); else Assert.Equal(s28 == "1", got.Slot28);
        }
    }

    /// <summary>
    /// IsRunnableBase 0x005bd864..0x005bd89c consults [vptr+0x20] when robot+0x355 is set, [vptr+0x24] when robot+0x34a is set and [vptr+0x28] while carrying: ReactToImpact's slots are
    /// 0, 0, 0 (it refuses all three), ReactToPickup's are 1, 0, 1, DriveOffCharger's 1, 1, 0, and a class the engine table does not have gets the base's 0 and 0 and reports the
    /// pure-virtual +0x28 MISSING.
    /// </summary>
    // fidelity: M8-001
    [Theory]
    [InlineData("ReactToImpact", false, false, false)]
    [InlineData("ReactToPickup", true, false, true)]
    [InlineData("DriveOffCharger", true, true, false)]
    [InlineData("ReactToOnCharger", true, true, true)]
    [InlineData("OnboardingShowCube", false, true, true)]
    [InlineData("Wait", true, false, true)]
    public void IsRunnableBaseAsksTheClassesOwnSlots(string cls, bool s20, bool s24, bool s28)
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Ctx(robot);
        bool Run(bool r355, bool r34a, bool carrying)
        {
            var p = new Probe("p", cls) { RobotState355 = () => r355, RobotState34a = () => r34a, RobotComponent284 = () => carrying };
            return p.IsRunnableBase(ctx);
        }
        Assert.True(Run(false, false, false));
        Assert.Equal(s20, Run(true, false, false));
        Assert.Equal(s24, Run(false, true, false));
        Assert.Equal(s28, Run(false, false, true));
    }

    // fidelity: M8-001
    [Fact]
    public void AClassOutsideTheEngineTableGetsTheBaseSlotsAndReportsMissing()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Ctx(robot);
        var reports = new List<string>();
        void On(string m) { lock (reports) reports.Add(m); }
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            bool Run(bool r355, bool r34a, bool carrying) =>
                new Probe("p", "RFix3NotAnEngineClass") { RobotState355 = () => r355, RobotState34a = () => r34a, RobotComponent284 = () => carrying }.IsRunnableBase(ctx);
            Assert.True(Run(false, false, false));
            Assert.False(Run(true, false, false));        // base +0x20: movs r0,#0 (0x005bf04c)
            Assert.False(Run(false, true, false));        // base +0x24: movs r0,#0 (0x0059ec12)
            Assert.False(Run(false, false, true));        // base +0x28: __cxa_pure_virtual: no value, answered false
        }
        finally { SteppedBehavior.MissingReported -= On; }
        lock (reports)
        {
            Assert.Contains(reports, m => m.Contains("RFix3NotAnEngineClass") && m.Contains("vtable+0x28") && m.Contains("pure_virtual"));
            Assert.Contains(reports, m => m.Contains("RFix3NotAnEngineClass") && m.Contains("vtable+0x20"));
        }
    }

    /// <summary>FindFaces and ExploreLookAroundInPlace share the +0x28 function 0x005c1d76 (<c>ldrb.w r0,[r0,#0x120]</c>), the byte the constructor stores from <c>behavior_CanCarryCube</c> (0x005e1efe).</summary>
    // fidelity: M8-001
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheExploreClassesCarryGateIsTheirCanCarryCubeConfigByte(bool canCarry)
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Ctx(robot);
        var b = new ExploreLookAroundInPlaceBehavior(null!, "ExploreLookAroundInPlace", new LookAroundParams { CanCarryCube = canCarry })
        { RobotState355 = () => false, RobotState34a = () => false, RobotComponent284 = () => true };
        Assert.Equal(canCarry, b.IsRunnableBase(ctx));
    }

    // ------------------------------------------------------------------ M8-003: the score slots a class overrides

    /// <summary>The five classes whose vtable overrides +0x84 and/or +0x88 (every other class points at IBehavior's 0x005beedf / 0x005beec3).</summary>
    // fidelity: M8-003
    [Fact]
    public void ExactlyFiveClassesOverrideTheScoreSlotsAndAScoredOneIsReportedMissing()
    {
        Assert.Equal(new[] { "DrivePath", "LookAround", "LookInPlaceMemoryMap", "PounceOnMotion", "RequestGameSimple" }, EngineScoreOverrides.Table.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Contains("+0x88, 0x005f83bf", EngineScoreOverrides.Table["PounceOnMotion"]);
        Assert.Contains("+0x84", EngineScoreOverrides.Table["LookAround"]);
        Assert.Contains("+0x84", EngineScoreOverrides.Table["RequestGameSimple"]);
        Assert.Contains("+0x88", EngineScoreOverrides.Table["RequestGameSimple"]);

        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var reports = new List<string>();
        void On(string m) { lock (reports) reports.Add(m); }
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            var b = new FakeBehavior("pounce", "PounceOnMotion");
            var chooser = new ScoringChooser(new[] { Entry("pounce", 1.0) }, new Dictionary<string, IBehavior> { ["pounce"] = b }) { Rng = new EngineRandom(1u) };
            chooser.GetDesiredActiveBehavior(null, 0, Ctx(robot), 0);
            new ScoringChooser(new[] { Entry("f", 1.0) }, new Dictionary<string, IBehavior> { ["f"] = new FakeBehavior("f", "SomethingElse") }) { Rng = new EngineRandom(1u) }.GetDesiredActiveBehavior(null, 0, Ctx(robot), 0);
        }
        finally { SteppedBehavior.MissingReported -= On; }
        lock (reports)
        {
            Assert.Single(reports, m => m.Contains("PounceOnMotion") && m.Contains("EvaluateScoreInternal"));
            Assert.DoesNotContain(reports, m => m.Contains("SomethingElse"));
        }
    }

    // ------------------------------------------------------------------ M8-002 / M8-003: the bad-penalty warnings

    /// <summary>
    /// IBehavior::ReadFromScoredJson 0x005bc488: a PRESENT (non-null) repetitionPenalty (0x005bc4ee..0x005bc50a) or runningPenalty (0x005bc602..0x005bc61c) whose GraphEvaluator2d::ReadFromJson
    /// (0x00804dac) fails warns on "IScoredBehavior.BadRepetitionPenalty" (0x005bc524) / "IBehavior.BadRunningPenalty" (0x005bc638) with "Behavior '%s': %s failed to read" (0x005bc718), after
    /// ReadFromJson's own warning. ReadFromJson tests only isNull: nodes (0x00804dc6), the node (0x00804e5c), x (0x00804e7a), y (0x00804e84), then asFloat.
    /// </summary>
    // fidelity: M8-002, M8-003
    [Fact]
    public void AnUnreadablePenaltyGraphWarnsWithTheEnginesChannelsAndKeepsTheNodesReadSoFar()
    {
        var lines = new List<string>();
        ScoredBehaviorEntry Read(string rep, string run = "null") =>
            ScoredBehaviorEntry.FromJson(JsonDocument.Parse("{\"behaviorID\":\"Wait\",\"scoring\":{\"flatScore\":1,\"repetitionPenalty\":" + rep + ",\"runningPenalty\":" + run + "}}").RootElement, lines.Add);
        const string badRep = "warning: IScoredBehavior.BadRepetitionPenalty: Behavior 'Wait': repetitionPenalty failed to read";

        Assert.Null(Read("{\"foo\":1}").RepetitionPenalty);
        Assert.Equal(new[] { "warning: GraphEvaluator2d.ReadFromJson.NoNodes: Missing entry for 'nodes' key", badRep }, lines);
        lines.Clear();
        Assert.Null(Read("{\"nodes\":[null]}").RepetitionPenalty);
        Assert.Equal(new[] { "warning: GraphEvaluator2d.ReadFromJson.BadNode: Node 0 failed to read", badRep }, lines);
        lines.Clear();
        Assert.Null(Read("{\"nodes\":[{\"y\":1}]}").RepetitionPenalty);
        Assert.Equal(new[] { "warning: GraphEvaluator2d.ReadFromJson.BadX: Node 0 failed to read 'x'", badRep }, lines);
        lines.Clear();
        Assert.Null(Read("{\"nodes\":[{\"x\":1}]}").RepetitionPenalty);
        Assert.Equal(new[] { "warning: GraphEvaluator2d.ReadFromJson.BadY: Node 0 failed to read 'y'", badRep }, lines);
        lines.Clear();
        // the nodes read before the failure are KEPT: two good nodes, then a node without y
        var partial = Read("{\"nodes\":[{\"x\":0,\"y\":1},{\"x\":10,\"y\":0.5},{\"x\":20}]}").RepetitionPenalty;
        Assert.Equal(new[] { (0.0, 1.0), (10.0, 0.5) }, partial!.Nodes);
        Assert.Equal(new[] { "warning: GraphEvaluator2d.ReadFromJson.BadY: Node 2 failed to read 'y'", badRep }, lines);
        lines.Clear();
        Read("null", "{\"nodes\":[{\"x\":0}]}");
        Assert.Equal(new[] { "warning: GraphEvaluator2d.ReadFromJson.BadY: Node 0 failed to read 'y'", "warning: IBehavior.BadRunningPenalty: Behavior 'Wait': runningPenalty failed to read" }, lines);
        lines.Clear();

        // only isNull is tested: a non-null scalar nodes has size() 0 (success, empty), an empty array succeeds, a bool x or y goes through asFloat (1.0 / 0.0)
        Assert.Null(Read("{\"nodes\":5}").RepetitionPenalty);
        Assert.Null(Read("{\"nodes\":[]}").RepetitionPenalty);
        Assert.Equal(new[] { (1.0, 0.0) }, Read("{\"nodes\":[{\"x\":true,\"y\":false}]}").RepetitionPenalty!.Nodes);
        Assert.Equal(new[] { (0.5, 0.25) }, Read("{\"nodes\":[{\"x\":0.5,\"y\":0.25}]}").RepetitionPenalty!.Nodes);
        Assert.Empty(lines);
        ScoredBehaviorEntry.FromJson(JsonDocument.Parse("{\"behaviorID\":\"Wait\",\"scoring\":{\"flatScore\":1}}").RootElement, lines.Add);
        Assert.Empty(lines);
    }

    // ------------------------------------------------------------------ M8-009: the lock key is the plain name

    /// <summary>SmartLockTracks 0x005be5bc locks on the MovementComponent under the plain key (0x005be618..0x005be624); the destructor path (0x005bd16e..0x005bd174) and SmartUnLockTracks (0x005be6fc..0x005be706) release under that plain key.</summary>
    // fidelity: M8-009
    [Fact]
    public void ANamedTrackLockIsTakenAndReleasedUnderThePlainName()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        byte head = CozmoMotion.MaskFor(AnimationTrack.Head);
        Assert.NotEqual(0, head);
        var scope = new BehaviorScope(motion: robot.Motion);
        Assert.True(scope.SmartLockTracks("rfix3Key", AnimationTrack.Head));
        Assert.True(robot.Motion.AreAllTracksLockedBy(head, "rfix3Key"));
        Assert.False(robot.Motion.AreAllTracksLockedBy(head, "scope-1:rfix3Key"));
        scope.Dispose();
        Assert.False(robot.Motion.AreAnyTracksLocked(head));

        var second = new BehaviorScope(motion: robot.Motion);
        Assert.True(second.SmartLockTracks("rfix3Other", AnimationTrack.Head));
        Assert.True(second.SmartUnLockTracks("rfix3Other"));
        Assert.False(robot.Motion.AreAnyTracksLocked(head));
        second.Dispose();
    }

    // ------------------------------------------------------------------ M7-013: Emotion::Add on NaN, BadTimeStep

    /// <summary>Emotion::Add 0x00679618: the sum is compared with 1.0f and with -1.0f, and "hi" (greater OR UNORDERED, 0x0067965A vmovhi) stores the sum: a NaN sum is stored as NaN, not -1.</summary>
    // fidelity: M7-013
    [Fact]
    public void AnEmotionAddOfNaNKeepsNaN()
    {
        var model = new MoodModel();
        model.AddEvent(new EmotionEvent("nan", new[] { new EmotionAffector(EmotionType.Happy, double.NaN) }));
        model.AddEvent(new EmotionEvent("minus", new[] { new EmotionAffector(EmotionType.Calm, -5.0) }));
        var mood = new MoodState(model);
        Assert.True(mood.Trigger("nan", 1.0));
        Assert.True(double.IsNaN(mood[EmotionType.Happy]));
        Assert.True(mood.Trigger("minus", 1.0));
        Assert.Equal(-1.0, mood[EmotionType.Calm]);        // a finite sum below -1 still clamps
    }

    /// <summary>
    /// MoodManager::Update 0x0067b5d4: a step below 1e-4f logs sWarningF("MoodManager.BadTimeStep", "TimeStep %f (%f-%f) is &lt; %f - clamping!", dt, t, last, 1e-4) (0x0067b62e..0x0067b644;
    /// strings 0x0067b6f8 and 0x0067b6e0), the arguments as doubles, six decimals each.
    /// </summary>
    // fidelity: M7-013
    [Fact]
    public void ATooSmallStepLogsTheEnginesBadTimeStepText()
    {
        var mood = new MoodState(new MoodModel());
        var lines = new List<string>();
        mood.Log = lines.Add;
        mood.Advance(2.5);                      // last == 0: the step is the 1e-4f floor itself: no warning
        Assert.Empty(lines);
        mood.Advance(2.5);                      // dt == 0 < 1e-4f
        Assert.Equal("warning: MoodManager.BadTimeStep: TimeStep 0.000000 (2.500000-2.500000) is < 0.000100 - clamping!", Assert.Single(lines));
    }

    // ------------------------------------------------------------------ M7-014: no arbiter-wide fallback

    /// <summary>
    /// The engine only calls IBehavior::SmartDisableReactionsWithLock (0x005bce3c) with a class's own table: a ReactBehavior whose class has none takes no reaction lock and reports it MISSING;
    /// a class with a table (ReactToCliff, 0xC73746) takes it.
    /// </summary>
    // fidelity: M7-014
    [Fact]
    public async Task AReactBehaviourWithoutALockTableTakesNoLockAndReportsMissing()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext { Robot = robot, Triggers = AnimationTriggerMap.Load(obb), Random = new Random(4) };
        var reports = new List<string>();
        void On(string m) { lock (reports) reports.Add(m); }
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            using var none = new BehaviorScope();
            var noTable = new ReactBehavior("rfix3NoTable", "RFix3NoTableClass", ReactionTrigger.CliffDetected, _ => true);
            await noTable.StartAsync(ctx, none, default);
            Assert.NotNull(noTable.LastSelected);                     // the animation resolved and started, so the lock step was reached
            Assert.False(none.ReactionsDisabled);

            using var table = new BehaviorScope();
            var withTable = new ReactBehavior("rfix3Table", "ReactToCliff", ReactionTrigger.CliffDetected, _ => true);
            await withTable.StartAsync(ctx, table, default);
            Assert.NotNull(withTable.LastSelected);
            Assert.True(table.ReactionsDisabled);
        }
        finally { SteppedBehavior.MissingReported -= On; }
        lock (reports) Assert.Single(reports, m => m.Contains("RFix3NoTableClass") && m.Contains("no recovered reaction-lock table"));
    }

    // ------------------------------------------------------------------ M7-021: the state-name log's behaviour id

    /// <summary>
    /// The state-name helper 0x005c0ca8 prints "Behavior:%s" with EnumToString(BehaviorID at +0x3C) (0x005c0cb8/0x005c0cbc): a behaviour whose id is one of the BehaviorID names prints
    /// that name; one that is not has no such value, prints its id string and reports MISSING.
    /// </summary>
    // fidelity: M7-021
    [Fact]
    public void TheStateNameLogPrintsTheBehaviourIdsEnumName()
    {
        var reports = new List<string>();
        void On(string m) { lock (reports) reports.Add(m); }
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            var known = new Probe("ReactToCliff", "ReactToCliff");
            known.Name("PlayingStopReaction");
            Assert.Contains("info: [Behaviors] Behavior.TransitionToState: Behavior:ReactToCliff, FromState: ToState:PlayingStopReaction", known.Lines);
            known.Name("PlayingCliffReaction");
            Assert.Contains("info: [Behaviors] Behavior.TransitionToState: Behavior:ReactToCliff, FromState:PlayingStopReaction ToState:PlayingCliffReaction", known.Lines);

            var unknown = new Probe("rfix3NotABehaviorId", "ReactToCliff");
            unknown.Name("S");
            Assert.Contains("info: [Behaviors] Behavior.TransitionToState: Behavior:rfix3NotABehaviorId, FromState: ToState:S", unknown.Lines);
        }
        finally { SteppedBehavior.MissingReported -= On; }
        lock (reports)
        {
            Assert.Single(reports, m => m.Contains("rfix3NotABehaviorId") && m.Contains("not a BehaviorID name"));
            Assert.DoesNotContain(reports, m => m.Contains("'ReactToCliff' is not a BehaviorID"));
        }
    }

    // ------------------------------------------------------------------ M8-008: the CalibrateMotorAction texts

    private sealed class CalibrationProbe : SteppedBehavior
    {
        public CalibrationProbe() : base("calibrationProbe", "ReactToMotorCalibration") { }
        public bool Done;
        protected override void OnStart() => CalibrateHead(() => Done = true);
        protected override bool KeepsRunningWithoutAction => true;
        public IReadOnlyList<string> Lines => Trace;
    }

    /// <summary>
    /// IAction::UpdateInternal 0x00540e80..0x00540eb6 on expiry: sWarningF("IAction.Update.TimedOut", "%s timed out after %.1f seconds.", the action name, the +0x2c slot's 30.0f);
    /// the name is "CalibrateMotor-" + "Head" for (head, !lift) (CalibrateMotorAction::CalibrateMotorAction 0x00547a9c..0x00547ac6). The action is not hosted by an action runner
    /// here and that is reported MISSING.
    /// </summary>
    // fidelity: M8-008
    [Fact]
    public void ACalibrationThatNeverReportsTimesOutWithTheEnginesText()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Ctx(robot);
        var reports = new List<string>();
        void On(string m) { lock (reports) reports.Add(m); }
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            var probe = new CalibrationProbe();
            using var scope = new BehaviorScope();
            probe.StartAsync(ctx, scope, default).GetAwaiter().GetResult();
            probe.Update(ctx, 0);
            probe.Update(ctx, 30_000);
            Assert.True(probe.Done);
            Assert.Contains("warning: IAction.Update.TimedOut: CalibrateMotor-Head timed out after 30.0 seconds.", probe.Lines);
            Assert.DoesNotContain(probe.Lines, l => l.Contains("CheckIfDone.Done"));
        }
        finally { SteppedBehavior.MissingReported -= On; }
        lock (reports) Assert.Contains(reports, m => m.Contains("M8-008") && m.Contains("not hosted by an action runner"));
    }

    // ------------------------------------------------------------------ M7-018: AnimationTriggerFromString is an exact name lookup

    /// <summary>AnimationTriggerFromString (0x0075E2B0) is a name table: Enum.TryParse's numbers and comma lists are misses (0x00764798..0x00764810: the cerr error and trigger 0).</summary>
    // fidelity: M7-018
    [Fact]
    public void ATriggerNameInAPlayAnimConfigIsLookedUpExactly()
    {
        var cfg = JsonDocument.Parse("{\"behaviorID\":\"Wait\",\"behaviorClass\":\"PlayAnim\",\"animTriggers\":[\"Hiccup\",\"1\",\"hiccup\",\"Hiccup,Hiccup\"]}").RootElement;
        var b = PlayAnimBehavior.FromConfig(cfg, _ => { });
        Assert.Equal(new[] { AnimationTrigger.Hiccup, (AnimationTrigger)0, (AnimationTrigger)0, (AnimationTrigger)0 }, b.Triggers);
    }

    // ------------------------------------------------------------------ M7-005/007/016/017: the unset DesiredFaceDistortion is visible

    /// <summary>TrackLayerComponent::Update (0x0057cf7a) reads DesiredFaceDistortion every tick; with no source here the unset seam is reported MISSING (once), not defaulted silently.</summary>
    // fidelity: M7-017
    [Fact]
    public void AnUnsetDesiredFaceDistortionIsReportedMissing()
    {
        var reports = new List<string>();
        void On(string m) { lock (reports) reports.Add(m); }
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            var tlc = new TrackLayerComponent(new EngineRandom(1u), new ScanLineState());
            tlc.Update();
            tlc.Update();
        }
        finally { SteppedBehavior.MissingReported -= On; }
        lock (reports) Assert.Single(reports, m => m.Contains("DesiredFaceDistortion") && m.Contains("0x0057cf7a"));
    }

    // ------------------------------------------------------------------ M8-001: the gates' production sources

    private static void SetOffTreads(CozmoRobot robot, OffTreadsState state) =>
        typeof(OffTreadsClassifier).GetProperty("Current")!.GetSetMethod(true)!.Invoke(robot.Sensors.OffTreads, new object[] { state });

    /// <summary>
    /// IsRunnableBase reads robot+0x355 (Sensors.OffTreadsState), robot+0x34a (Sensors.OnChargerPlatform, SetOnChargerPlatform 0x00511d4c..0x00511d6a) and carrying (Motion.IsCarryingObject)
    /// with no seam set: ReactToImpact (0, 0, 0) is refused by each, DriveOffCharger (1, 1, 0) is refused only while carrying, ReactToOnCharger (1, 1, 1) never.
    /// </summary>
    // fidelity: M8-001
    [Fact]
    public void TheGateInputsDefaultToTheProductionSources()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Ctx(robot);
        Probe P(string cls) => new("p", cls);
        Assert.True(P("ReactToImpact").IsRunnableBase(ctx));
        robot.Sensors.SetOnChargerPlatform(true);
        Assert.False(P("ReactToImpact").IsRunnableBase(ctx));
        Assert.True(P("DriveOffCharger").IsRunnableBase(ctx));
        Assert.True(P("ReactToOnCharger").IsRunnableBase(ctx));
        robot.Sensors.SetOnChargerPlatform(false);
        Assert.True(P("ReactToImpact").IsRunnableBase(ctx));
        robot.Motion.IsCarryingObject = () => true;
        Assert.False(P("ReactToImpact").IsRunnableBase(ctx));
        Assert.False(P("DriveOffCharger").IsRunnableBase(ctx));
        Assert.True(P("ReactToOnCharger").IsRunnableBase(ctx));
        robot.Motion.IsCarryingObject = null;
        SetOffTreads(robot, OffTreadsState.InAir);
        Assert.False(P("ReactToImpact").IsRunnableBase(ctx));
        Assert.True(P("ReactToPickup").IsRunnableBase(ctx));          // slot 0x20 is 1
        Assert.True(P("DriveOffCharger").IsRunnableBase(ctx));
    }

    /// <summary>Singing (0, 0, 0) and PlayArbitraryAnim (0, 0, 1) own their IsRunnable but IsRunnableBase's gates still apply first (their vtable slots).</summary>
    // fidelity: M8-001
    [Fact]
    public void TheNonSteppedBehavioursApplyTheirGatesFirst()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Ctx(robot);
        Assert.True(EngineRunnableGates.Allows(ctx, "Singing"));
        robot.Motion.IsCarryingObject = () => true;
        Assert.False(EngineRunnableGates.Allows(ctx, "Singing"));            // slot 0x28 = 0
        Assert.True(EngineRunnableGates.Allows(ctx, "PlayArbitraryAnim"));   // slot 0x28 = 1
        robot.Motion.IsCarryingObject = null;
        robot.Sensors.SetOnChargerPlatform(true);
        Assert.False(EngineRunnableGates.Allows(ctx, "PlayArbitraryAnim"));  // slot 0x24 = 0
        robot.Sensors.SetOnChargerPlatform(false);
        SetOffTreads(robot, OffTreadsState.OnBack);
        Assert.False(EngineRunnableGates.Allows(ctx, "Singing"));            // slot 0x20 = 0
        Assert.False(EngineRunnableGates.Allows(ctx, "RFix3Nope"));          // outside the table: the base's 0
    }

    /// <summary>
    /// GraphEvaluator2d::AddNode(x, y, true) 0x00804a60: a node is appended when the vector is empty or last.x &lt;= x (0x00804a8e vcmpe, ble 0x00804b18), so an equal x is kept; a lower x logs
    /// sErrorF "GraphEvaluator2d.AddNode.OORange" "new node (%f, %f) has x &lt;= than last node %zu = (%f, %f)" (0x00804ad6) and is dropped; ReadFromJson ignores the result (0x00804ea2), so
    /// the read continues and later nodes are still added and the read is a success.
    /// </summary>
    // fidelity: M8-002, M8-003
    [Fact]
    public void AGraphNodeWithADecreasingXIsDroppedWithTheEnginesErrorAndTheReadContinues()
    {
        var lines = new List<string>();
        var e = ScoredBehaviorEntry.FromJson(JsonDocument.Parse("{\"behaviorID\":\"Wait\",\"scoring\":{\"repetitionPenalty\":{\"nodes\":[{\"x\":0,\"y\":1},{\"x\":10,\"y\":0.5},{\"x\":10,\"y\":0.25},{\"x\":5,\"y\":0.75},{\"x\":20,\"y\":0}]}}}").RootElement, lines.Add);
        Assert.Equal(new[] { (0.0, 1.0), (10.0, 0.5), (10.0, 0.25), (20.0, 0.0) }, e.RepetitionPenalty!.Nodes);
        Assert.Equal(new[] { "error: GraphEvaluator2d.AddNode.OORange: new node (5.000000, 0.750000) has x <= than last node 2 = (10.000000, 0.250000)" }, lines);   // read ok: no BadRepetitionPenalty
    }

    /// <summary>IsRunnableBase's first test is the running flag +0xa1 (0x005bd780), before the gates: a running Singing / PlayArbitraryAnim / ReactBehavior stays runnable when a gate input flips.</summary>
    // fidelity: M8-001
    [Fact]
    public void ARunningBehaviourIsRunnableBeforeAnyGate()
    {
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        var ctx = Ctx(robot);
        robot.Motion.IsCarryingObject = () => true;
        Assert.False(EngineRunnableGates.Allows(ctx, "Singing"));
        Assert.True(EngineRunnableGates.Allows(ctx, "Singing", running: true));
        robot.Motion.IsCarryingObject = null;
        robot.Sensors.SetOnChargerPlatform(true);
        Assert.False(EngineRunnableGates.Allows(ctx, "PlayArbitraryAnim"));
        Assert.True(EngineRunnableGates.Allows(ctx, "PlayArbitraryAnim", running: true));
    }

    /// <summary>The same through the class's own IsRunnable: a ReactBehavior that is playing stays runnable while the robot is carried (its not-running self is refused).</summary>
    // fidelity: M8-001
    [Fact]
    public async Task AReactBehaviourThatIsRunningStaysRunnableWhenCarried()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = new BehaviorContext { Robot = robot, Triggers = AnimationTriggerMap.Load(obb), Random = new Random(4) };
        var b = new ReactBehavior("rfix3Running", "ReactToImpact", ReactionTrigger.CliffDetected, _ => true);   // ReactToImpact: slots 0, 0, 0
        Assert.True(b.IsRunnable(ctx));
        await b.StartAsync(ctx, new BehaviorScope(), default);
        Assert.True(b.Update(ctx, 0));                                // running
        robot.Motion.IsCarryingObject = () => true;
        Assert.True(b.IsRunnable(ctx));
        b.Stop(BehaviorStopReason.Cancelled);
        Assert.False(b.IsRunnable(ctx));                              // not running any more: slot 0x28 = 0 refuses
    }
}
