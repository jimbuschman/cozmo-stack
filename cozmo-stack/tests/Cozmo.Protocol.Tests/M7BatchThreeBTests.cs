using System.Text.Json;
using System.Text.RegularExpressions;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// R-BEH2 batch 3b: M7-012 / M7-020 (the MoodState output and the HandleActionEnded caller), M7-002 / M7-018 (the behaviour config loader, the container,
/// the 79-way factory, the reaction map without a fallback, FistBump, ReactToSparked and the PlayAnim half of Hiccup). Every expected value is taken from
/// a row of the approved inventory (M7-behaviour.md Correction A3) or of the extractions it names
/// (<c>20261002-R-BEH2-M7-gap1-extraction.md</c> section 2, <c>20260929-R-BEH2-pre-extraction.md</c> sections 3 and 8 with the corrections of
/// <c>20260930-R-BEH2-pre-extraction-check.md</c>), or from the shipped assets, and is cited per test; none is what the code returned.
/// </summary>
public class M7BatchThreeBTests
{
    private static float F(int bits) => BitConverter.Int32BitsToSingle(bits);

    private static string? ObbRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var r = Path.Combine(d.FullName, "re-analysis", "obb");
            if (Directory.Exists(Path.Combine(r, "assets", "cozmo_resources", "assets", "animationGroups"))) return r;
            d = d.Parent;
        }
        return null;
    }

    private static string? RepoFile(string relative)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var f = Path.Combine(d.FullName, relative);
            if (File.Exists(f)) return f;
            d = d.Parent;
        }
        return null;
    }

    private static JsonElement Json(string text) => JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }).RootElement.Clone();

    private static BehaviorContext Ctx(Rig rig, AnimationTriggerMap? triggers = null) => new()
    {
        Robot = rig.Robot,
        Triggers = triggers ?? new AnimationTriggerMap(),
        Random = new Random(3),
    };

    private static BehaviorManager NewManager(BehaviorContext ctx, params IBehavior[] behaviours)
    {
        var m = new BehaviorManager(ctx);
        foreach (var b in behaviours) m.Add(b);
        return m;
    }

    // =============================================================== M7-012 / M7-020: the MoodState message

    /// <summary>
    /// M7-012 (pre-extraction section 3, "MoodState, field by field"): the payload is <c>uint8 count</c> then <c>count</c> four-byte floats (37 bytes for nine,
    /// 0x00704F76..0x00704F9E, 0x0070FB56..0x0070FB84), and inside <c>MessageEngineToGame</c> a preceding <c>uint16</c> 0x0061 tag (0x0072928C..0x00729298), 39
    /// bytes. The expected bytes are written out from the IEEE-754 encodings of the chosen values (0.5f = 00 00 00 3F; -1.0f = 00 00 80 BF; 1.0f = 00 00 80 3F;
    /// 0.25f = 00 00 80 3E), not produced by the code.
    /// </summary>
    [Fact]
    public void TheMoodStateMessageIsACountAndNineLittleEndianFloatsUnderTheTag0x0061()
    {
        var values = new[] { 0.5f, -1.0f, 1.0f, 0f, 0f, 0.25f, 0f, 0f, 0f };
        var message = new MoodStateMessage(values);
        var expectedBody = new byte[]
        {
            0x09,
            0x00, 0x00, 0x00, 0x3F,   // 0.5
            0x00, 0x00, 0x80, 0xBF,   // -1.0
            0x00, 0x00, 0x80, 0x3F,   // 1.0
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x80, 0x3E,   // 0.25
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        };
        Assert.Equal(37, expectedBody.Length);
        Assert.Equal(expectedBody, message.PackBody());
        var union = message.ToUnionBytes();
        Assert.Equal(39, union.Length);
        Assert.Equal(new byte[] { 0x61, 0x00 }, union[..2]);          // uint16 0x0061, little endian
        Assert.Equal(expectedBody, union[2..]);
        // the inverse reads the one-byte count and then exactly that many floats (0x00704FD6..0x00704FFE)
        Assert.Equal(values, MoodStateMessage.UnpackBody(expectedBody).Emotions);
        Assert.Throws<FormatException>(() => MoodStateMessage.UnpackBody(expectedBody[..^1]));
    }

    /// <summary>M7-012: the pack helper truncates the vector length to one byte (0x00704F76..0x00704F9E).</summary>
    [Fact]
    public void TheCountIsTruncatedToOneByte()
    {
        var message = new MoodStateMessage(new float[257]);
        Assert.Equal(1 + 257 * 4, message.PackBody().Length);
        Assert.Equal(0x01, message.PackBody()[0]);                    // 257 & 0xFF
    }

    /// <summary>M7-012 (0x0067B736..0x0067B73C): with the MoodManager's robot pointer (+0x12C) null, SendEmotionsToGame sends nothing.</summary>
    [Fact]
    public void NothingIsSentWithoutARobot()
    {
        using var rig = new Rig();
        var mood = new MoodState(new MoodModel());
        var sent = new List<MoodStateMessage>();
        mood.MoodStateBroadcast += sent.Add;
        mood.SendEmotionsToGame();
        Assert.Empty(sent);
        mood.AttachRobot(rig.Robot);
        mood.SendEmotionsToGame();
        Assert.Single(sent);
        Assert.Equal(9, sent[0].Emotions.Count);
    }

    /// <summary>
    /// M7-012 / M7-020, through the production entry: <c>FreeplayStack.Create</c> attaches the robot, <c>FreeplayStack.Tick</c> -> <c>FreeplaySystem.Tick</c>
    /// advances the mood and then sends it (<c>MoodManager::Update</c> ends with <c>SendEmotionsToGame</c>, 0x0067B6A4). The nine floats are in EmotionType order
    /// (Happy, Calm, Brave, Confident, Charged, Excited, Social, Winning, WantToPlay; 0x007740E0..0x007742D0): after the shipped event SparkPending (Confident
    /// +0.5, <c>emotionevents/spark_events.json</c>) element 3 is 0.5 and the others are 0. The first Update's step is the 1e-4f floor and the shipped decay graph is
    /// flat at 1 from 0 to 15 s, so the decay ratio is 1 and the value is unchanged.
    /// </summary>
    [Fact]
    public void TheFreeplayTickBroadcastsTheNineEmotionsInEmotionTypeOrder()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = Ctx(rig);
        using var stack = FreeplayStack.Create(obb, rig.Robot, ctx, () => 1.0, rig.Vision, rig.M, withReactions: false, random: new Random(1));
        Assert.Same(rig.Robot, ctx.Mood!.Robot);
        var sent = new List<MoodStateMessage>();
        ctx.Mood.MoodStateBroadcast += sent.Add;
        Assert.True(ctx.Mood.Trigger("SparkPending", 0.0));
        stack.Tick(1.0, 1000, rig.Robot, rig.Vision, rig.M);
        Assert.NotEmpty(sent);
        var nine = sent[0].Emotions;
        Assert.Equal(new[] { 0f, 0f, 0f, 0.5f, 0f, 0f, 0f, 0f, 0f }, nine);
        Assert.Equal(39, sent[0].ToUnionBytes().Length);
        Assert.Equal(new byte[] { 0x61, 0x00, 0x09 }, sent[0].ToUnionBytes()[..3]);
    }

    /// <summary>
    /// M7-020 (A3: <c>MoodManager::HandleActionEnded</c>): an action whose tag is in the +0x140 set is erased from it and produces no event (tail call
    /// 0x008CD6AC, <c>__tree&lt;unsigned&gt;::erase</c>), so the next completion of the same tag does raise its mapped event.
    /// </summary>
    [Fact]
    public void AOneShotSuppressionIsErasedWhenItIsUsed()
    {
        var model = new MoodModel();
        model.AddDecayGraph(new DecayGraph("Confident", new[] { (0.0, 1.0) }));
        model.AddEvent(new EmotionEvent("DrivingActionFailedWithAbort", new[] { new EmotionAffector(EmotionType.Confident, -0.2) }));
        model.AddActionResultEvent("DRIVE_TO_POSE", "ABORT", "DrivingActionFailedWithAbort");
        var mood = new MoodState(model);
        mood.SetMoodEventOnCompletionEnabled("7", false);
        Assert.False(mood.HandleActionEnded("DRIVE_TO_POSE", "ABORT", "7", 0));    // erased, no event
        Assert.Equal(0.0, mood[EmotionType.Confident]);
        Assert.True(mood.HandleActionEnded("DRIVE_TO_POSE", "ABORT", "7", 1));     // the set no longer holds it
        Assert.Equal(-0.2f, (float)mood[EmotionType.Confident]);
    }

    /// <summary>
    /// M7-020 through the production entry (W13/W14/W15/W16): <c>FreeplayStack.Create</c> registers
    /// <c>MoodManager::HandleActionEnded</c> with the robot's ActionList (0x0067AEE8..0x0067AF38) and the watcher
    /// drains it (W11). A completed head action (RobotActionType MOVE_HEAD_TO_ANGLE = 0x12, result SUCCESS) reaches
    /// <c>MoodState.HandleActionEnded</c> and triggers the mapped event. The expected 0.3 is the affector this test
    /// put in its own model, not a value the production code returns.
    /// </summary>
    [Fact]
    public async Task TheProductionRegistrationDrivesHandleActionEnded()
    {
        var obb = ObbRoot()!;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var model = new MoodModel();
        model.AddDecayGraph(new DecayGraph("Confident", new[] { (0.0, 1.0) }));
        model.AddEvent(new EmotionEvent("HeadMoveSucceeded", new[] { new EmotionAffector(EmotionType.Confident, 0.3) }));
        model.AddActionResultEvent("MOVE_HEAD_TO_ANGLE", "SUCCESS", "HeadMoveSucceeded");
        var mood = new MoodState(model);
        var ctx = Ctx(rig);
        ctx.Mood = mood;
        using var stack = FreeplayStack.Create(obb, rig.Robot, ctx, () => 1.0, rig.Vision, rig.M, withReactions: false, random: new Random(1));
        Assert.Same(mood, ctx.Mood);                                     // the production Create kept the supplied model
        Assert.Equal(0.0, mood[EmotionType.Confident]);

        rig.State();                                                     // report the head at rest and in position
        var pending = rig.Robot.Motion.SetHeadAngleAsync(rig.Head + 0.034f);   // within tolerance + 1e-5: in position
        rig.Tick();                                                      // Robot::Update's ActionList step

        var outcome = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(outcome.Ok, outcome.Detail);
        Assert.Equal(0.3f, (float)mood[EmotionType.Confident], 6);       // the production callback fired
    }

    // =============================================================== M7-018: BehaviorClassFromString and the factory table

    /// <summary>
    /// M7-018 (gap pass 1 section 2.1, BehaviorClassFromString miss path, 0x0076AD60..0x0076ADD6): an unknown, empty, absent or non-string
    /// <c>behaviorClass</c> writes <c>error: string '&lt;s&gt;' is not a valid BehaviorClass value</c> and a newline to <c>std::cerr</c> and returns 0, which is Bouncer;
    /// a hit returns its own ordinal and writes nothing.
    /// </summary>
    [Fact]
    public void AnUnknownEmptyAbsentOrNonStringClassWritesToCerrAndIsBouncer()
    {
        var cerr = new StringWriter();
        BehaviorClassNames.CerrOverride = cerr;
        try
        {
            Assert.Equal(BehaviorClass.Bouncer, BehaviorClassNames.ExtractFromConfig(Json("{\"behaviorClass\":\"NoSuchClass\"}")));
            Assert.Equal(BehaviorClass.Bouncer, BehaviorClassNames.ExtractFromConfig(Json("{\"behaviorClass\":\"\"}")));
            Assert.Equal(BehaviorClass.Bouncer, BehaviorClassNames.ExtractFromConfig(Json("{\"behaviorID\":\"Hiccup\"}")));
            Assert.Equal(BehaviorClass.Bouncer, BehaviorClassNames.ExtractFromConfig(Json("{\"behaviorClass\":5}")));
            Assert.Equal("error: string 'NoSuchClass' is not a valid BehaviorClass value\n"
                         + "error: string '' is not a valid BehaviorClass value\n"
                         + "error: string '' is not a valid BehaviorClass value\n"
                         + "error: string '' is not a valid BehaviorClass value\n", cerr.ToString());
            cerr.GetStringBuilder().Clear();
            Assert.Equal(BehaviorClass.FistBump, BehaviorClassNames.ExtractFromConfig(Json("{\"behaviorClass\":\"FistBump\"}")));
            Assert.Equal(BehaviorClass.ReactToUnexpectedMovement, BehaviorClassNames.FromString("ReactToUnexpectedMovement"));
            Assert.Equal("", cerr.ToString());
        }
        finally { BehaviorClassNames.CerrOverride = null; }
    }

    /// <summary>
    /// M7-018 (gap pass 1 section 2.2): the 79-way table of <c>CreateBehavior</c> 0x0059C888, ordinal by ordinal: the engine class, the constructor address, the
    /// <c>operator new</c> size and the case address. The expectation is parsed from the extraction's table, not from the code.
    /// </summary>
    [Fact]
    public void TheFactoryTableIsTheExtractionsTable()
    {
        var doc = RepoFile("re-analysis/research/20261002-R-BEH2-M7-gap1-extraction.md");
        if (doc is null) return;
        var rows = new List<(int Ord, string Cls, uint Case, uint Size, string Eng, uint Ctor)>();
        foreach (var line in File.ReadAllLines(doc))
        {
            var mt = Regex.Match(line, @"^0x([0-9A-F]{2}) (\w+)\s+case@0x([0-9a-f]+) new=0x([0-9a-f]+) (\w+) ctor=0x([0-9a-f]+) plt=0x[0-9a-f]+");
            if (mt.Success)
                rows.Add((Convert.ToInt32(mt.Groups[1].Value, 16), mt.Groups[2].Value, Convert.ToUInt32(mt.Groups[3].Value, 16), Convert.ToUInt32(mt.Groups[4].Value, 16),
                          mt.Groups[5].Value, Convert.ToUInt32(mt.Groups[6].Value, 16)));
        }
        Assert.Equal(79, rows.Count);
        Assert.Equal(79, BehaviorFactory.Table.Count);
        foreach (var r in rows)
        {
            var e = BehaviorFactory.Table[r.Ord];
            Assert.Equal(r.Cls, e.Class.ToString());
            Assert.Equal(r.Ord, (int)e.Class);
            Assert.Equal(r.Eng, e.EngineClass);
            Assert.Equal(r.Ctor, e.ConstructorAddress);
            Assert.Equal(r.Size, e.AllocationSize);
            Assert.Equal(r.Case, e.CaseAddress);
        }
        // the three cases this stack builds from a config (and no others)
        Assert.Equal(new[] { BehaviorClass.FistBump, BehaviorClass.PlayAnim, BehaviorClass.ReactToSparked },
                     BehaviorFactory.Table.Where(e => e.Construct is not null).Select(e => e.Class).OrderBy(c => c).ToArray());
    }

    // =============================================================== M7-018: the loader

    private static string NewBehaviorsDir(out string root)
    {
        root = Path.Combine(Path.GetTempPath(), "cozmo-r-beh2-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "assets", "cozmo_resources", "config", "engine", "behaviorSystem", "behaviors");
        Directory.CreateDirectory(Path.Combine(dir, "sub"));
        return dir;
    }

    /// <summary>
    /// M7-018 (gap pass 1 section 2.1, "Per-file load", 0x005206BC..0x00520808): the directory is read recursively; a file that fails to read logs the warning
    /// "Failed to read '%s'" and the load goes on; an empty value is skipped silently; the config goes into the map only when its ID is absent, so a duplicate keeps
    /// the first and logs nothing; comments and trailing commas are accepted (the shipped files carry them).
    /// </summary>
    [Fact]
    public void TheLoaderReadsRecursivelySkipsEmptyAndKeepsTheFirstDuplicate()
    {
        var dir = NewBehaviorsDir(out var root);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.json"), "{ \"behaviorClass\": \"Wait\", // a comment\n \"behaviorID\": \"Wait\", }");
            File.WriteAllText(Path.Combine(dir, "sub", "b.json"), "{ \"behaviorClass\": \"Bouncer\", \"behaviorID\": \"Wait\" }");   // the duplicate ID: later path
            File.WriteAllText(Path.Combine(dir, "c.json"), "{ not json");
            File.WriteAllText(Path.Combine(dir, "d.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "e.json"), "{ \"behaviorClass\": \"PlayAnim\", \"behaviorID\": \"Hiccup\", \"animTriggers\": [\"Hiccup\"] }");
            File.WriteAllText(Path.Combine(dir, "ignored.txt"), "{ \"behaviorID\": \"Bouncer\" }");
            var log = new List<string>();
            var map = BehaviorConfigLoader.Load(root, log.Add);
            Assert.Equal(new[] { BehaviorID.Hiccup, BehaviorID.Wait }, map.Keys.OrderBy(k => k.ToString(), StringComparer.Ordinal).ToArray());
            Assert.Equal("Wait", map[BehaviorID.Wait].GetProperty("behaviorClass").GetString());     // the first duplicate won
            var warnings = log.Where(l => l.Contains("Failed to read")).ToList();
            Assert.Single(warnings);
            Assert.StartsWith("warning: RobotDataLoader.Behavior: Failed to read '", warnings[0]);
            Assert.EndsWith("c.json'", warnings[0]);
            Assert.Single(log);                      // the empty file and the duplicate logged nothing
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>
    /// M7-018 (gap pass 1 section 2.1, "Shipped file set"): the shipped corpus is 178 configs under the directory, each with a string behaviorClass and behaviorID,
    /// 76 classes; no ID repeats, so the map holds all 178. The class counts are the asset's (obb_filelist.txt, X3 part 3's 178-config table).
    /// </summary>
    [Fact]
    public void TheShippedCorpusIs178ConfigsOver76Classes()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var log = new List<string>();
        var map = BehaviorConfigLoader.Load(obb, log.Add);
        Assert.Empty(log);
        Assert.Equal(178, map.Count);
        var cerr = new StringWriter();
        BehaviorClassNames.CerrOverride = cerr;
        try
        {
            var classes = map.Values.Select(BehaviorClassNames.ExtractFromConfig).ToList();
            Assert.Equal("", cerr.ToString());                     // every shipped class resolves
            Assert.Equal(76, classes.Distinct().Count());
            Assert.Equal(15, classes.Count(c => c == BehaviorClass.PlayAnim));
            Assert.Equal(39, classes.Count(c => c == BehaviorClass.Singing));
            Assert.Equal(2, classes.Count(c => c == BehaviorClass.FistBump));
            Assert.Single(classes.Where(c => c == BehaviorClass.ReactToSparked));
        }
        finally { BehaviorClassNames.CerrOverride = null; }
    }

    // =============================================================== M7-018: the container loop and AddToFactory

    /// <summary>
    /// M7-018 (gap pass 1 section 2.1, "Container load loop" 0x0059C34C..0x0059C428, "CreateBehavior" 0x0059C888): an empty config warns "Failed to read behavior file for
    /// behavior id '%s'" and is skipped; a class the table has no case for (above 0x4E) is the engine's error path; and a class this stack cannot build from a config
    /// is reported "class not implemented" (MISSING) with no engine error line and no stand-in object. A built behaviour is added with the Info line
    /// "Added new behavior '%s' %p".
    /// </summary>
    [Fact]
    public void TheContainerLoopWarnsOnEmptyReportsUnbuiltClassesAndAddsWhatItBuilds()
    {
        var log = new List<string>();
        var reported = new List<string>();
        void On(string s) => reported.Add(s);
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            var configs = new List<KeyValuePair<BehaviorID, JsonElement>>
            {
                new(BehaviorID.Wait, Json("{}")),
                new(BehaviorID.Hiccup, Json("{\"behaviorClass\":\"PlayAnim\",\"behaviorID\":\"Hiccup\",\"animTriggers\":[\"Hiccup\"]}")),
                new(BehaviorID.Bouncer, Json("{\"behaviorClass\":\"Bouncer\",\"behaviorID\":\"Bouncer\"}")),
            };
            var container = new BehaviorContainer(new BehaviorFactory(), new BehaviorFactoryContext { Log = log.Add }, configs);
            Assert.Contains("warning: Robot.LoadBehavior: Failed to read behavior file for behavior id 'Wait'", log);
            Assert.Equal(new[] { BehaviorID.Hiccup }, container.Behaviors.Keys.ToArray());
            Assert.Single(log, l => l.StartsWith("info: [Unnamed] behaviorContainer::AddToFactory: Added new behavior 'Hiccup' 0x"));
            Assert.DoesNotContain(log, l => l.Contains("Robot.LoadBehavior.CreateFailed"));
            Assert.Contains(reported, r => r.Contains("class not implemented") && r.Contains("Bouncer") && r.Contains("BehaviorBouncer") && r.Contains("0x5F12FC"));
            Assert.Equal(1, container.NotCreated);
            // the second container (robot+0x48) is named, not built
            Assert.Contains(reported, r => r.Contains("second BehaviorContainer") && r.Contains("robot+0x48"));

            // a class above 0x4E: the engine's error (0x0059C89A / 0x0059D3FE), no object
            var log2 = new List<string>();
            var ctx2 = new BehaviorFactoryContext { Log = log2.Add };
            var none = new BehaviorFactory().CreateBehavior(container, (BehaviorClass)0x4F, ctx2, Json("{}"), out var status);
            Assert.Null(none);
            Assert.Equal(BehaviorCreateStatus.ClassOutOfRange, status);
            Assert.Single(log2, l => l.Contains("behaviorContainer.CreateBehavior.Failed") && l.Contains("Failed to create Behavior of type"));
        }
        finally { SteppedBehavior.MissingReported -= On; }
    }

    /// <summary>
    /// M7-018 (gap pass 1 section 2.1, AddToFactory 0x0059E90C..0x0059E998): a unique insert by the ID read from the object logs "Added new behavior"; a duplicate logs nothing,
    /// replaces nothing, and the new object is still returned to the caller.
    /// </summary>
    [Fact]
    public void AddToFactoryIsAUniqueInsertAndADuplicateIsSilent()
    {
        var log = new List<string>();
        var container = new BehaviorContainer(new BehaviorFactory(), new BehaviorFactoryContext { Log = log.Add }, Array.Empty<KeyValuePair<BehaviorID, JsonElement>>());
        var first = new ReactToSparkedBehavior("ReactToSparked");
        var second = new ReactToSparkedBehavior("ReactToSparked");
        Assert.Same(first, container.AddToFactory(first));
        Assert.Single(log);
        Assert.Same(second, container.AddToFactory(second));        // returned to the caller ...
        Assert.Single(log);                                          // ... with nothing logged ...
        Assert.Same(first, container.Behaviors[BehaviorID.ReactToSparked]);   // ... and nothing replaced
    }

    // =============================================================== M7-002: the reaction map has no fallback

    /// <summary>
    /// M7-002 (audit, <c>RobotDataLoader::LoadReactionTriggerMap</c> 0x00520BC8: readAsJson 0x00520C2E, sErrorF 0x00520C54, error flag 0x00520C8A): a missing or empty
    /// map leaves the engine's map empty and <c>InitReactionTriggerMap</c> registers nothing. There is no fallback that registers every built reaction.
    /// </summary>
    [Fact]
    public void AMissingOrEmptyReactionMapRegistersNothing()
    {
        using var rig = new Rig();
        Assert.Empty(ShippedBehaviors.Reactions(rig.Robot));                                         // no OBB root at all
        var dir = NewBehaviorsDir(out var root);
        try { Assert.Empty(ShippedBehaviors.Reactions(rig.Robot, obbRoot: root)); }                  // a root with no map file
        finally { Directory.Delete(root, true); }
        var obb = ObbRoot();
        if (obb is null) return;
        var regs = ShippedBehaviors.Reactions(rig.Robot, obbRoot: obb);
        Assert.NotEmpty(regs);                                                                       // the shipped map does register
    }

    // =============================================================== M7-018: Hiccup's PlayAnim half and ReactToSparked, from the production container

    /// <summary>
    /// M7-018 (pre-extraction section 8, "Shipped declarations" and "PlayAnim half"): <c>reactions/hiccup.json</c> declares class PlayAnim with
    /// <c>animTriggers ["Hiccup"]</c>; the generic <c>BehaviorPlayAnimSequence</c> plays the one trigger 0xE1 once (single-trigger path 0x005C0158..0x005C01BC, num_loops
    /// default 1). Driven through the production wiring: <c>FreeplayStack.Create</c> binds the behaviour from the config corpus, and the manager starts it.
    /// </summary>
    [Fact]
    public void HiccupIsTheConfigBuiltPlayAnimAndPlaysTheHiccupTriggerOnce()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = Ctx(rig, AnimationTriggerMap.Load(obb));
        using var stack = FreeplayStack.Create(obb, rig.Robot, ctx, () => 1.0, rig.Vision, rig.M, withReactions: false, random: new Random(1));
        Assert.Equal(0xE1, (int)AnimationTrigger.Hiccup);
        var hiccup = Assert.IsType<PlayAnimBehavior>(stack.Bound["Hiccup"]);
        Assert.Equal(new[] { AnimationTrigger.Hiccup }, hiccup.Triggers);
        Assert.Equal(1, hiccup.NumLoops);
        Assert.True(stack.Manager.StartAsync("Hiccup", 0).GetAwaiter().GetResult());
        Assert.Single(hiccup.Trace, l => l.StartsWith("play Hiccup -> "));
        Assert.True(hiccup.HasCurrentAction);
        stack.Manager.Stop(BehaviorStopReason.Interrupted, 0);
    }

    /// <summary>
    /// M7-018 (pre-extraction section 8, ReactToSparked): always runnable (0x00609864); Init triggers the mood event "SparkPending" (0x006097F8..0x00609838;
    /// Confident +0.5 in the shipped <c>spark_events.json</c>); no UpdateInternal, so with no action the inherited update returns 2 at once. Through the production
    /// container and manager.
    /// </summary>
    [Fact]
    public void ReactToSparkedTriggersSparkPendingAndCompletesAtOnce()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = Ctx(rig, AnimationTriggerMap.Load(obb));
        using var stack = FreeplayStack.Create(obb, rig.Robot, ctx, () => 1.0, rig.Vision, rig.M, withReactions: false, random: new Random(1));
        var sparked = Assert.IsType<ReactToSparkedBehavior>(stack.Bound["ReactToSparked"]);
        Assert.True(sparked.IsRunnable(ctx));
        Assert.Equal(0.0, ctx.Mood![EmotionType.Confident]);
        Assert.True(stack.Manager.StartAsync("ReactToSparked", 0).GetAwaiter().GetResult());
        Assert.Equal(0.5f, (float)ctx.Mood[EmotionType.Confident]);
        Assert.False(sparked.HasCurrentAction);
        stack.Manager.Update(100, 0.1);
        Assert.NotSame(sparked, stack.Manager.Current);          // it ended on the first update (the manager then chose something else)
    }

    /// <summary>
    /// M7-018: both FistBump configs reach the container (class FistBump, case 0x19) with their own parameters: fistBump.json 3.0 s / abort true / update true,
    /// sparksFistBump.json 5.0 s / abort false / update omitted (constructor default false) (pre-extraction section 8, "Shipped declarations"; the files).
    /// </summary>
    [Fact]
    public void BothFistBumpConfigsAreBuiltByTheContainerWithTheirOwnParameters()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        var container = BehaviorContainer.LoadShipped(obb, new BehaviorFactoryContext());
        var fist = Assert.IsType<FistBumpBehavior>(container.Behaviors[BehaviorID.FistBump]);
        Assert.Equal((3.0f, true, true), (fist.MaxTimeToLookForFaceSec, fist.AbortIfNoFaceFound, fist.UpdateLastCompletionTime));
        var sparks = Assert.IsType<FistBumpBehavior>(container.Behaviors[BehaviorID.SparksFistBump]);
        Assert.Equal((5.0f, false, false), (sparks.MaxTimeToLookForFaceSec, sparks.AbortIfNoFaceFound, sparks.UpdateLastCompletionTime));
        // 15 PlayAnim + 2 FistBump + 1 ReactToSparked are built; the other 160 configs name classes this stack cannot build from a config
        Assert.Equal(18, container.Behaviors.Count);
        Assert.Equal(160, container.NotCreated);
    }

    // =============================================================== M7-018: FistBump

    /// <summary>
    /// M7-018 (pre-extraction section 8 with the check's corrections): the constants are the engine's bits. Tilt 0x3F1C61AA (the check's correction of 0.6108652 =
    /// 0x3F1C61A9), pans 0xBE860A92 / 0x3F060A92 (data 0x00C6FDC0), thresholds 0x3C0EFA35 / 0x3E32B8C2 / 0x457A0000, animation numbers 0xC6..0xCA, idle trigger 0x23F.
    /// </summary>
    [Fact]
    public void TheFistBumpConstantsAreTheEnginesBits()
    {
        Assert.Equal(0x3F1C61AA, BitConverter.SingleToInt32Bits(FistBumpBehavior.ScanTiltRad));
        Assert.Equal(unchecked((int)0xBE860A92), BitConverter.SingleToInt32Bits(FistBumpBehavior.ScanPanRad[0]));
        Assert.Equal(0x3F060A92, BitConverter.SingleToInt32Bits(FistBumpBehavior.ScanPanRad[1]));
        Assert.Equal(0x3C0EFA35, BitConverter.SingleToInt32Bits(FistBumpBehavior.LiftBumpRad));
        Assert.Equal(0x3E32B8C2, BitConverter.SingleToInt32Bits(FistBumpBehavior.GyroBumpRadPerSec));
        Assert.Equal(0x457A0000, BitConverter.SingleToInt32Bits(FistBumpBehavior.AccelBumpMmps2));
        Assert.Equal(0x42700000, BitConverter.SingleToInt32Bits(FistBumpBehavior.AnimationTimeoutSec));
        Assert.Equal(0x3F800000, BitConverter.SingleToInt32Bits(FistBumpBehavior.OffTreadsLimitSec));
        Assert.Equal(0x3F000000, BitConverter.SingleToInt32Bits(FistBumpBehavior.SettleWarnSec));
        Assert.Equal(0xC6, (int)AnimationTrigger.FistBumpIdle);
        Assert.Equal(0xC7, (int)AnimationTrigger.FistBumpRequestOnce);
        Assert.Equal(0xC8, (int)AnimationTrigger.FistBumpRequestRetry);
        Assert.Equal(0xC9, (int)AnimationTrigger.FistBumpSuccess);
        Assert.Equal(0xCA, (int)AnimationTrigger.FistBumpLeftHanging);
        Assert.Equal(0x23F, (int)FistBumpBehavior.IdleTriggerCount);
        Assert.Equal(0x0300000Eu, FistBumpBehavior.NoFaceResult);
        Assert.Equal(124u, FistBumpBehavior.RecentFaceShifted);
    }

    private sealed class FistRig : IDisposable
    {
        private readonly SignalTestContext Signals = SignalTestContext.Install();
        public readonly Rig Rig = new();
        public readonly List<string> Power = new();
        public readonly List<(float Pan, float Tilt)> PanTilts = new();
        public readonly List<(bool PanAbs, bool TiltAbs)> PanTiltFlags = new();
        /// <summary>The order of the objective reports and ResetTrigger calls.</summary>
        public readonly List<string> Events = new();
        public readonly List<bool> Listener = new();
        public readonly List<AnimationTrigger> IdlePushes = new();
        public FistBumpBehavior Fist = null!;
        public BehaviorManager Manager = null!;
        public double Ms;

        public int Turns;

        public FistRig(bool realAnimations, Func<CancellationToken, Task<uint>>? turn = null, float maxTime = 30f, bool abort = true, bool update = true, bool withVision = false)
        {
            Rig.Robot.Animations.ManualTicking = true;
            Rig.Robot.Animations.ClockMs = () => Ms;
            var obb = ObbRoot();
            AnimationTriggerMap triggers;
            if (realAnimations)
            {
                Rig.Robot.Animations.LoadFrom(Path.Combine(obb!, "assets", "cozmo_resources", "assets"));
                triggers = AnimationTriggerMap.Load(obb!);
            }
            else
            {
                Rig.Robot.Animations.LoadFrom(Path.Combine(obb!, "assets", "cozmo_resources", "assets"));
                triggers = new AnimationTriggerMap();                    // every trigger fails at once: the action ends with no clip
            }
            var turnSeam = turn ?? (_ => Task.FromResult(0u));
            Fist = new FistBumpBehavior("FistBump", maxTime, abort, update, withVision ? Rig.Vision : null)
            {
                TurnTowardsLastFace = ct => { Interlocked.Increment(ref Turns); return turnSeam(ct); },
                PanAndTilt = (p, t, pa, ta, ct) => { PanTilts.Add((p, t)); PanTiltFlags.Add((pa, ta)); return Task.CompletedTask; },
                EnableLiftPower = on => Power.Add("lift:" + on),
                EnableHeadPower = on => Power.Add("head:" + on),
                SmartPushIdleAnimation = IdlePushes.Add,
            };
            Fist.ActionTaskRunner = SignalTestContext.Schedule;
            Fist.WorkPosted += Signals.Notify;
            Fist.AddListener(u => { Listener.Add(u); Events.Add("reset:" + u); });
            Fist.Step += l => { if (l.StartsWith("BehaviorObjectiveAchieved(")) Events.Add(l); };
            Manager = NewManager(Ctx(Rig, triggers), Fist);
        }

        public void Start() => Assert.True(Manager.StartAsync("FistBump", 0).GetAwaiter().GetResult());

        /// <summary>One modeled manager and animation tick, after signaled action completion has been delivered.</summary>
        public void Step(int n = 1)
        {
            for (int i = 0; i < n; i++)
            {
                Ms += 100;
                if (Fist.AwaitingAsyncCompletion)
                {
                    SignalTestContext.Run(Fist.AsyncInvocationStarted);
                    var invoked = Fist.AsyncInvocationStarted.GetAwaiter().GetResult();
                    if (invoked.IsCompleted) SignalTestContext.Run(Fist.AsyncWorkCompletion);
                }
                bool wasPlaying = Rig.Robot.Animations.IsPlaying;
                Rig.Robot.Animations.Scheduler.Advance(Ms);
                if (wasPlaying && !Rig.Robot.Animations.IsPlaying && Fist.HasCurrentAction) BehaviorTestSignals.WaitForPostedWork(Fist);
                Manager.Update(Ms, Ms / 1000.0);
                SignalTestContext.Drain();          // start newly queued actions after the manager tick returns
            }
        }

        public bool StepUntil(Func<bool> done, int max = 400)
        {
            for (int i = 0; i < max && !done(); i++) Step();
            return done();
        }

        public void Dispose() { Rig.Dispose(); Signals.Dispose(); }
    }

    /// <summary>
    /// M7-018 (Init, 0x005F1ED4..0x005F1F1E): the eight-trigger lock table 0x00C6FDC8 (CubeMoved, FacePositionUpdated, ObjectPositionUpdated, PetInitialDetection,
    /// RobotFalling, RobotPickedUp, ReturnedToTreads, UnexpectedMovement: 1,2,8,10,11,12,14,20) is held through SmartDisableReactionsWithLock under the behaviour's
    /// name, the idle trigger 0x23F is pushed, and the state is 1 when nothing is carried; released again at Stop.
    /// </summary>
    [Fact]
    public void FistBumpInitTakesItsLockTablePushesTheIdleTriggerAndStartsAtOne()
    {
        if (ObbRoot() is null) return;
        using var f = new FistRig(realAnimations: false);
        f.Start();
        Assert.Equal(1, f.Fist.State);
        const string mask = "011000001011101000001";                // 1,2,8,10,11,12,14,20
        for (int t = 0; t < 21; t++) Assert.Equal(mask[t] == '1', f.Manager.HasDisableLock((ReactionTrigger)t, "FistBump_behaviorLock"));
        Assert.Equal(new[] { (AnimationTrigger)0x23F }, f.IdlePushes);
        f.Manager.Stop(BehaviorStopReason.Interrupted, 0);
        for (int t = 0; t < 21; t++) Assert.False(f.Manager.HasDisableLock((ReactionTrigger)t, "FistBump_behaviorLock"));
    }

    /// <summary>M7-018 (Init, 0x005F1F08..0x005F1F18): with an object carried ([[robot+0x284]+8] != -1) the state starts at 0 and PlaceObjectOnGround runs first, then state 1.</summary>
    [Fact]
    public void WhileCarryingFistBumpPutsTheObjectDownFirst()
    {
        if (ObbRoot() is null) return;
        using var f = new FistRig(realAnimations: false);
        f.Rig.Robot.Motion.IsCarryingObject = () => true;
        int placed = 0;
        f.Fist.PlaceObjectOnGround = ct => { placed++; return Task.CompletedTask; };
        f.Start();
        Assert.Equal(0, f.Fist.State);
        f.Step();
        Assert.Equal(1, placed);
        Assert.Equal(1, f.Fist.State);                              // written right after the action starts (0x005F2364)
    }

    /// <summary>
    /// M7-018 (states 1, 2 and the search timeout, 0x005F2070..0x005F24EE; check: the +0x84 gate): the face turn ends NO_FACE (0x0300000E), which stamps the search
    /// start and enters state 2. The search pans -0.2617994 then +0.5235988 with the tilt 0x3F1C61AA (the index wraps to 0 after the second entry: 0x005F24D8..0x005F24EE), each reschedule at
    /// <c>now + RandDblInRange(1.0, 2.0)</c>; and while a pan is in flight (+0x84 != 0) the state switch does not run, so no second pan starts. After
    /// <c>now &gt; start + maxTime</c> with abort-on-no-face true the state is 9: objective 7, ResetTrigger(update = true), terminal.
    /// </summary>
    [Fact]
    public void ANoFaceTurnEntersTheBoundedSearchAndTheTimeoutAborts()
    {
        if (ObbRoot() is null) return;
        var openPan = new TaskCompletionSource();
        using var f = new FistRig(realAnimations: false, turn: _ => Task.FromResult(0x0300000Eu), maxTime: 12f);
        int pans = 0;
        f.Fist.PanAndTilt = (p, t, pa, ta, ct) =>
        {
            f.PanTilts.Add((p, t)); f.PanTiltFlags.Add((pa, ta));
            return ++pans == 2 ? openPan.Task : Task.CompletedTask;       // the second pan stays in flight
        };
        f.Start();
        Assert.True(f.StepUntil(() => f.PanTilts.Count == 1));
        Assert.Equal(2, f.Fist.State);
        Assert.Equal(unchecked((int)0xBE860A92), BitConverter.SingleToInt32Bits(f.PanTilts[0].Pan));
        Assert.Equal(0x3F1C61AA, BitConverter.SingleToInt32Bits(f.PanTilts[0].Tilt));
        Assert.Equal(1, f.Fist.ScanIndex);
        float firstNow = (float)(f.Ms / 1000.0);
        Assert.InRange(f.Fist.NextScanTime, firstNow + 1.0f - 0.2f, firstNow + 2.0f);     // within 1..2 s of a scan taken within the last tick or two

        Assert.True(f.StepUntil(() => f.PanTilts.Count == 2));
        Assert.Equal(0x3F060A92, BitConverter.SingleToInt32Bits(f.PanTilts[1].Pan));
        Assert.Equal(0, f.Fist.ScanIndex);                                                // idx+1 < 2 fails: wraps to 0 (0x005F24D8..0x005F24EE)
        // the second pan is in flight: even long after the next scan time the gate (+0x84) stops a third pan
        f.Step(40);
        Assert.Equal(2, f.PanTilts.Count);
        openPan.SetResult();
        // The pan's wrapper resumes on the worker scheduler. Deliver its queued completion before
        // advancing the modeled search clock; worker load must not consume the search window.
        SignalTestContext.Run(f.Fist.AsyncWorkCompletion);
        Assert.True(f.StepUntil(() => f.PanTilts.Count == 3));
        Assert.Equal(unchecked((int)0xBE860A92), BitConverter.SingleToInt32Bits(f.PanTilts[2].Pan));   // the third pan is entry 0 again
        Assert.All(f.PanTiltFlags, fl => Assert.Equal((false, true), fl));               // PanAndTiltAction(robot, pan, tilt, false, true): 0x005F23BE..0x005F23C6

        // the 12 s search window ends: abort (state 9) -> objective 7, ResetTrigger(true), the terminal 2
        Assert.True(f.StepUntil(() => f.Manager.Current is null));
        // state 9 (0x005F228C): ResetTrigger(+0x148) first, then objective 7; Stop then calls ResetTrigger(false)
        Assert.Equal(new[] { "reset:True", "BehaviorObjectiveAchieved(7)", "reset:False" }, f.Events);
        Assert.Equal(new[] { true, false }, f.Listener);
        Assert.Equal("lift:True", f.Power[^2]);                                           // Stop re-enables lift then head
        Assert.Equal("head:True", f.Power[^1]);
    }

    /// <summary>
    /// M7-018 (search timeout branch, 0x005F1FE2..0x005F2006): with abort-on-no-face false the timeout enters state 3 (the request animation) and goes on without a face,
    /// ending in the left-hanging path; the first idle end increments +0x138 and retries (0xC8, back to 4), the second plays 0xCA and enters 8 (0x005F23FC..0x005F24A2);
    /// state 8 reports objectives 9 and 7 (not the need action), ResetTrigger(update = false) and ends. Lift and head power go off in each state 4 and back on at each
    /// idle end and at Stop (lift first).
    /// </summary>
    [Fact]
    public void WithoutAbortTheSearchEndsInTheRetryAndLeftHangingPath()
    {
        if (ObbRoot() is null) return;
        using var f = new FistRig(realAnimations: false, turn: _ => Task.FromResult(0x0300000Eu), maxTime: 1f, abort: false, update: false);
        f.Start();
        Assert.True(f.StepUntil(() => f.Manager.Current is null));
        Assert.Equal(2, f.Fist.Retries);
        // state 8: objective 9, ResetTrigger(+0x148), objective 7; Stop: ResetTrigger(false)
        Assert.Equal(new[] { "BehaviorObjectiveAchieved(9)", "reset:False", "BehaviorObjectiveAchieved(7)", "reset:False" }, f.Events);
        Assert.Equal(new[] { false, false }, f.Listener);                                 // update-last false from the config; Stop calls ResetTrigger(false)
        Assert.Equal(new[] { "lift:False", "head:False", "lift:True", "head:True", "lift:False", "head:False", "lift:True", "head:True", "lift:True", "head:True" }, f.Power);
    }

    /// <summary>
    /// M7-018 (state 6, 0x005F21B2..0x005F2258; thresholds 0x3C0EFA35 / 0x3E32B8C2 / 0x457A0000, strictly greater): with the idle action still running, exactly the
    /// threshold is not a bump and the next float above it is. A bump stops the idle action, powers the motors back, plays 0xC9 and enters 7; state 7 reports the need
    /// action and objectives 8 then 7, calls ResetTrigger(update) and ends. Driven through the manager with the real animations; the robot states carry the readings.
    /// </summary>
    [Theory]
    [InlineData("accel", 0x457A0000, false)]            // 4000.0: not greater
    [InlineData("accel", 0x457A0001, true)]             // the next float above 4000.0
    [InlineData("gyro", 0x3E32B8C2, false)]             // 0.17453292
    [InlineData("gyro", 0x3E32B8C3, true)]
    [InlineData("lift", 0x3C0EFA35, false)]             // 0.008726646
    [InlineData("lift", 0x3C0EFA36, true)]
    public void TheBumpThresholdsAreStrict(string which, int bits, bool bump)
    {
        if (ObbRoot() is null) return;
        using var f = new FistRig(realAnimations: true);
        f.Start();
        // 1 -> turn (success) -> 3 -> request animation in flight, state 4
        Assert.True(f.StepUntil(() => f.Fist.State == 4));
        f.Rig.Robot.Animations.Stop();                       // the request animation ends: +0x84 clears
        BehaviorTestSignals.WaitForPostedWork(f.Fist);
        // 4 -> idle animation starts (still in flight) -> 5 -> snapshots at 0 -> 6
        Assert.True(f.StepUntil(() => f.Fist.State == 6));
        Assert.Equal(new[] { "lift:False", "head:False" }, f.Power);
        float v = F(bits);
        var s = new RobotState
        {
            Timestamp = f.Rig.T += 33, PoseOriginId = f.Rig.OriginId, Pose = new RobotPose(), HeadAngle = f.Rig.Head,
            LiftAngle = which == "lift" ? v : 0f,
            Status = (uint)(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos),
            Accel = new AccelData { X = which == "accel" ? v : 0f, Z = 9800 },
            Gyro = new GyroData { Y = which == "gyro" ? v : 0f },
        };
        f.Rig.Send(s);
        f.Step();
        if (!bump)
        {
            Assert.Equal(6, f.Fist.State);
            Assert.Equal(new[] { "lift:False", "head:False" }, f.Power);
            return;
        }
        Assert.Equal(7, f.Fist.State);                       // the success animation (0xC9) started
        Assert.Equal(new[] { "lift:False", "head:False", "lift:True", "head:True" }, f.Power);
        f.Rig.Robot.Animations.Stop();                       // the success animation ends
        BehaviorTestSignals.WaitForPostedWork(f.Fist);
        Assert.True(f.StepUntil(() => f.Manager.Current is null));
        // state 7: NeedActionCompleted(0), objective 8, ResetTrigger(+0x148 = true), objective 7 (0x005F2274..0x005F229A); Stop: ResetTrigger(false)
        Assert.Equal(new[] { "BehaviorObjectiveAchieved(8)", "reset:True", "BehaviorObjectiveAchieved(7)", "reset:False" }, f.Events);
        Assert.Equal(new[] { true, false }, f.Listener);
    }

    /// <summary>
    /// M7-018 (persistent off-treads exit, 0x005F1F4A..0x005F1F7A): while the off-treads state is not OnTreads the first tick stamps +0x144 and after strictly more than
    /// 1.0 s the update returns the terminal 2; the behaviour ends (Stop calls ResetTrigger(false)).
    /// </summary>
    [Fact]
    public void BeingLiftedForMoreThanASecondEndsTheBehaviour()
    {
        if (ObbRoot() is null) return;
        using var f = new FistRig(realAnimations: false, turn: _ => new TaskCompletionSource<uint>().Task);     // the turn never ends: the state stays 1
        f.Start();
        f.Rig.Send(new MotorCalibration { MotorID = MotorID.MOTOR_HEAD, CalibStarted = false, AutoStarted = false });
        for (int i = 0; i < 40; i++) f.Rig.State();
        uint flags = (uint)RobotStatusFlag.IsPickedUp;
        for (int i = 0; i < 10; i++)
        {
            f.Rig.T += 33;
            f.Rig.Send(new RobotState { Timestamp = f.Rig.T, PoseOriginId = f.Rig.OriginId, Pose = new RobotPose(), HeadAngle = f.Rig.Head, LiftAngle = 0f, Status = flags,
                                        Accel = new AccelData { Z = 9800 }, Gyro = new GyroData() });
        }
        Assert.Equal(OffTreadsState.InAir, f.Rig.Robot.Sensors.OffTreadsState);
        f.Step();                                            // 0.1 s: stamps
        Assert.NotNull(f.Manager.Current);
        f.Step(9);                                           // now - stamp == 0.9 s
        Assert.NotNull(f.Manager.Current);
        f.Step(2);                                           // 1.1 s: more than 1.0 s
        Assert.Null(f.Manager.Current);
        Assert.Equal(new[] { false }, f.Listener);           // Stop's ResetTrigger(false); the terminal 2 did not come from a state 7/8/9
    }

    /// <summary>
    /// M7-018: an unset seam is reported MISSING and nothing is made up: SmartPushIdleAnimation(0x23F) (UNKNOWN in the inventory), EnableLiftPower/EnableHeadPower and the
    /// PanAndTilt of the search. The behaviour still runs.
    /// </summary>
    [Fact]
    public void UnsetSeamsAreReportedMissing()
    {
        if (ObbRoot() is null) return;
        var reported = new List<string>();
        void On(string s) => reported.Add(s);
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            using var f = new FistRig(realAnimations: false, turn: _ => Task.FromResult(0x0300000Eu), maxTime: 30f);
            f.Fist.SmartPushIdleAnimation = null; f.Fist.EnableLiftPower = null; f.Fist.EnableHeadPower = null; f.Fist.PanAndTilt = null;
            f.Start();
            Assert.True(f.StepUntil(() => f.Fist.State == 2 && f.Fist.ScanIndex == 1));
            f.Manager.Stop(BehaviorStopReason.Interrupted, 0);
            Assert.Contains(reported, r => r.Contains("SmartPushIdleAnimation(0x23F") && r.Contains("UNKNOWN"));
            Assert.Contains(reported, r => r.Contains("PanAndTiltAction"));
            Assert.Contains(reported, r => r.Contains("EnableLiftPower"));
            Assert.Contains(reported, r => r.Contains("EnableHeadPower"));
        }
        finally { SteppedBehavior.MissingReported -= On; }
    }

    // =============================================================== batch 3b verifier fixes

    /// <summary>
    /// BehaviorIDFromString 0x0076B340 (miss 0x0076CDDE..0x0076CE4E, text 0x0076D2BC): a miss writes "error: string '&lt;s&gt;' is not a valid BehaviorID value" and a
    /// newline to cerr and returns 0 (AcknowledgeFace); a missing behaviorID takes "IBeh.NoBehaviorIdSpecified" (0x005BB9DE), which is not one of the names, so
    /// the same path. Nothing throws.
    /// </summary>
    [Fact]
    public void AnUnknownOrMissingBehaviorIdWritesToCerrAndIsAcknowledgeFace()
    {
        var cerr = new StringWriter();
        BehaviorClassNames.CerrOverride = cerr;
        try
        {
            Assert.Equal(BehaviorID.AcknowledgeFace, BehaviorConfigLoader.IdFromString("NoSuchId"));
            Assert.Equal(BehaviorID.AcknowledgeFace, BehaviorConfigLoader.ExtractIdFromConfig(Json("{\"behaviorClass\":\"Wait\"}")));
            Assert.Equal("error: string 'NoSuchId' is not a valid BehaviorID value\n"
                         + "error: string 'IBeh.NoBehaviorIdSpecified' is not a valid BehaviorID value\n", cerr.ToString());
        }
        finally { BehaviorClassNames.CerrOverride = null; }
    }

    /// <summary>
    /// AnimationTriggerFromString 0x0075E2B0 (miss 0x00764798..0x00764810, text 0x0076539C): an unknown animTrigger writes "error: string '&lt;s&gt;' is not a valid
    /// AnimationTrigger value" to cerr and returns 0, and the PlayAnim constructor keeps every value except 0x23F (0x005BFFE8..0x005BFFEE), so the 0 is pushed
    /// and "Count" (0x23F) is not.
    /// </summary>
    [Fact]
    public void AnUnknownAnimTriggerPushesZeroAndCountIsSkipped()
    {
        var cerr = new StringWriter();
        BehaviorClassNames.CerrOverride = cerr;
        try
        {
            var b = PlayAnimBehavior.FromConfig(Json("{\"behaviorClass\":\"PlayAnim\",\"behaviorID\":\"Hiccup\",\"animTriggers\":[\"Hiccup\",\"NoSuchTrigger\",\"Count\"]}"));
            Assert.Equal(new[] { AnimationTrigger.Hiccup, (AnimationTrigger)0 }, b.Triggers);
            Assert.Equal("error: string 'NoSuchTrigger' is not a valid AnimationTrigger value\n", cerr.ToString());
        }
        finally { BehaviorClassNames.CerrOverride = null; }
    }

    /// <summary>LoadReactionTriggerMap's failed read logs sErrorF "Failed to read '%s'" (0x00520C54) and the map stays empty: nothing registers (no fallback).</summary>
    [Fact]
    public void AFailedReactionMapReadIsLogged()
    {
        using var rig = new Rig();
        var lines = new List<string>();
        rig.Robot.Engine.LogLine += lines.Add;
        Assert.Empty(ShippedBehaviors.Reactions(rig.Robot));
        Assert.Contains(lines, l => l.StartsWith("error:") && l.Contains("Failed to read '") && l.Contains("reactionTrigger_behavior_map.json"));
    }

    /// <summary>
    /// FistBump state 2's face branch (0x005F22B4..0x005F2340, 0x005F24F4..0x005F24FC): a recent named face (the robot clock minus its timestamp, shifted right by 3, at most 124)
    /// starts the last-face turn with an EMPTY callback and enters state 3. The turn here ends NO_FACE (0x0300000E): state 1's callback (0x005F27EE) would send the state back
    /// to 2, state 2's has none, so the behaviour goes on to the request animation and the idle pose (state 4).
    /// </summary>
    [Fact]
    public void AFaceFoundInTheSearchStartsTheTurnWithNoCallbackAndGoesToState3()
    {
        if (ObbRoot() is null) return;
        using var f = new FistRig(realAnimations: false, turn: _ => Task.FromResult(0x0300000Eu), maxTime: 30f, withVision: true);
        f.Start();
        Assert.True(f.StepUntil(() => f.Fist.State == 2));                     // state 1's turn ended NO_FACE: the search
        var cam = new CameraModel(CameraCalibration.Nominal(), HeadGeometry.CameraPoseInWorld(Pose3d.Identity, 0.2));
        var px = cam.Project(new Vec3(400, 0, 300))!.Value;
        double eye = 62 * cam.Calibration.FocalLengthX / (new Vec3(400, 0, 300) - cam.Pose.Translation).Length, w = 2 * eye;
        uint ts = f.Rig.T - 100;                                                // (robot clock - ts) >> 3 = 12 <= 124
        var tf = new TrackedFace(new DetectedFace(1, new FaceRect(px.X - w / 2, px.Y + 0.125 * w - w / 2, w, w),
            new Vec2(px.X - eye / 2, px.Y), new Vec2(px.X + eye / 2, px.Y), Name: "Jim", RollRad: 0.0), ts);
        tf.UpdateTranslation(cam);
        Assert.NotNull(f.Rig.Vision.Faces.AddOrUpdateFace(tf, Pose3d.Identity, false));
        f.Rig.State();                                                          // the robot clock is now near the face
        int turnsBefore = f.Turns;
        Assert.True(f.StepUntil(() => f.Fist.State >= 4));                      // 2 -> (turn, empty callback) 3 -> request -> 4: never back to 2
        Assert.Equal(turnsBefore + 1, f.Turns);
        Assert.Single(f.PanTilts);                                              // only the scan taken on entering state 2, before the face existed
    }

    /// <summary>With no vision system, state 2's face check is skipped and reported (it has no source).</summary>
    [Fact]
    public void WithoutVisionTheSearchReportsTheMissingFaceSource()
    {
        if (ObbRoot() is null) return;
        var reported = new List<string>();
        void On(string s) => reported.Add(s);
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += On;
        try
        {
            using var f = new FistRig(realAnimations: false, turn: _ => Task.FromResult(0x0300000Eu), maxTime: 30f);
            f.Start();
            Assert.True(f.StepUntil(() => f.PanTilts.Count == 1));
            Assert.Contains(reported, r => r.Contains("state 2") && r.Contains("GetLastObservedFace"));
        }
        finally { SteppedBehavior.MissingReported -= On; }
    }

    /// <summary>
    /// State 5's settle warning (0x005F2178): event "BehaviorFistBump.UpdateInternal.MotorSettleTimeTooLong" (0x005F25EC), format "%f" (0x005F2624) with the elapsed
    /// time since +0x134, logged when it exceeds 0.5 s (0x005F2160..0x005F2168).
    /// </summary>
    [Fact]
    public void ASlowMotorSettleLogsTheEnginesWarning()
    {
        if (ObbRoot() is null) return;
        using var f = new FistRig(realAnimations: true);
        f.Start();
        Assert.True(f.StepUntil(() => f.Fist.State == 4));
        f.Rig.State(flags: 0);                                                  // not in position: the lift and head read as moving
        f.Rig.Robot.Animations.Stop();
        Assert.True(f.StepUntil(() => f.Fist.State == 5));
        f.Step(8);                                                              // 0.8 s of settling
        Assert.Equal(5, f.Fist.State);
        f.Rig.State();                                                          // at rest
        Assert.True(f.StepUntil(() => f.Fist.State == 6));
        var line = Assert.Single(f.Fist.Trace, l => l.StartsWith("warning: BehaviorFistBump.UpdateInternal.MotorSettleTimeTooLong: "));
        Assert.True(double.Parse(line[(line.LastIndexOf(' ') + 1)..], System.Globalization.CultureInfo.InvariantCulture) > 0.5);
    }

    /// <summary>
    /// FistBump through the production entry: <c>FreeplayStack.Create</c> binds FistBump from its config (vision and manipulation attached), and the manager starts it: the
    /// lock table is held and the state is 1 (nothing carried); Stop releases the lock. (The manager's own tick would replace a hand-started behaviour with the chooser's pick, so the state machine is driven in the tests above.)
    /// </summary>
    [Fact]
    public void FistBumpRunsThroughTheFreeplayStack()
    {
        var obb = ObbRoot();
        if (obb is null) return;
        using var rig = new Rig();
        rig.Robot.Animations.LoadFrom(Path.Combine(obb, "assets", "cozmo_resources", "assets"));
        var ctx = Ctx(rig, AnimationTriggerMap.Load(obb));
        using var stack = FreeplayStack.Create(obb, rig.Robot, ctx, () => 1.0, rig.Vision, rig.M, withReactions: false, random: new Random(1));
        var fist = Assert.IsType<FistBumpBehavior>(stack.Bound["FistBump"]);
        Assert.True(stack.Manager.StartAsync("FistBump", 0).GetAwaiter().GetResult());
        Assert.Equal(1, fist.State);
        Assert.True(stack.Manager.HasDisableLock(ReactionTrigger.CubeMoved, "FistBump_behaviorLock"));
        stack.Manager.Stop(BehaviorStopReason.Interrupted, 0.2);
        Assert.False(stack.Manager.HasDisableLock(ReactionTrigger.CubeMoved, "FistBump_behaviorLock"));
    }
}
