using Cozmo.Conformance;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The hardware runner's state machine: the ordering, the dependency gating, the two-part verdict and the
/// session file that lets a run survive a disconnect. None of this needs a robot, which is the point — the
/// parts that must not lose a person's afternoon of testing are the parts that can be tested here.
/// </summary>
public class HardwareRunnerTests
{
    private static HardwareSession Session(params HardwareCheck[] catalog) =>
        new(catalog.Length > 0 ? catalog : HardwareCatalog.All, "172.31.1.1", "obb");

    private static HardwareCheck Check(string id, params string[] requires) => new()
    {
        Id = id, Name = $"check {id}", Milestone = "M0", Subsystem = "s", Why = "w",
        Setup = "s", DoThis = "d", Success = "ok", Question = "did it work?", AutoRule = "rule",
        Requires = requires,
        Command = _ => new[] { "sensors", "1.2.3.4" },
        Judge = _ => AutoOutcome.Pass,
    };

    private static HardwareResult Result(string id, AutoOutcome auto, HumanOutcome human) =>
        new() { Id = id, Auto = auto, Human = human };

    // ------------------------------------------------------------------ the catalog

    [Fact]
    public void TheCatalogCoversThePlanAndOrdersItSafely()
    {
        var ids = HardwareCatalog.All.Select(c => c.Id).ToList();
        foreach (var expected in new[] { "A", "A2", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L",
                                         "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" })
            Assert.Contains(expected, ids);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        // the cheap, passive checks come before anything that drives itself
        Assert.False(HardwareCatalog.Find("D")!.MovesRobot);
        Assert.False(HardwareCatalog.Find("B")!.MovesRobot);
        Assert.False(HardwareCatalog.Find("K")!.MovesRobot);
        int firstMover = ids.FindIndex(i => HardwareCatalog.Find(i)!.MovesRobot);
        foreach (var passive in new[] { "D", "B", "G", "E", "K" })
            Assert.True(ids.IndexOf(passive) < firstMover, $"{passive} should come before anything that moves");

        // freeplay runs last: it exercises everything else
        Assert.Equal("Z", ids[^1]);

        // every check tells the person what to do and what a pass looks like, and asks a real question
        foreach (var c in HardwareCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Why));
            Assert.False(string.IsNullOrWhiteSpace(c.Setup));
            Assert.False(string.IsNullOrWhiteSpace(c.DoThis));
            Assert.False(string.IsNullOrWhiteSpace(c.Success));
            if (c.BlockedReason is null) Assert.EndsWith("?", c.Question.Trim());
        }
    }

    [Fact]
    public void EveryCommandNamesAToolTheRunnerCanCall()
    {
        var known = new[] { "sensors", "cubes", "camera", "anim", "behavior", "sing", "offtreads",
                            "reactions", "vision", "bodyangle", "manip", "freeplay" };
        var o = new HardwareRunOptions { Ip = "172.31.1.1", Obb = "obb", EvidenceDirectory = "ev" };
        foreach (var c in HardwareCatalog.All)
        {
            var args = c.Command(o);
            Assert.NotEmpty(args);
            Assert.Contains(args[0], known);
            Assert.Equal("172.31.1.1", args[1]);
        }
    }

    // ------------------------------------------------------------------ the two-part verdict

    [Theory]
    [InlineData(AutoOutcome.Pass, HumanOutcome.Pass, CheckStatus.Passed)]
    [InlineData(AutoOutcome.Fail, HumanOutcome.Fail, CheckStatus.Failed)]
    [InlineData(AutoOutcome.Error, HumanOutcome.Fail, CheckStatus.Failed)]
    [InlineData(AutoOutcome.Pass, HumanOutcome.Fail, CheckStatus.Partial)]
    [InlineData(AutoOutcome.Fail, HumanOutcome.Pass, CheckStatus.Partial)]
    [InlineData(AutoOutcome.Pass, HumanOutcome.NotAsked, CheckStatus.Pending)]
    [InlineData(AutoOutcome.Pass, HumanOutcome.Skipped, CheckStatus.Skipped)]
    public void TheTwoHalvesOfAVerdictAreKeptApart(AutoOutcome auto, HumanOutcome human, CheckStatus expected) =>
        Assert.Equal(expected, HardwareSession.StatusOf(Result("X", auto, human)));

    /// <summary>
    /// The singing case: the tool is satisfied and the listener is not. That must not read as a pass, and
    /// must not read as an ordinary failure either — it is the disagreement that carries the information.
    /// </summary>
    [Fact]
    public void AToolThatPassesWhileThePersonFailsItStaysInconclusive()
    {
        var s = Session();
        s.Record(Result("A", AutoOutcome.Pass, HumanOutcome.Fail));
        Assert.Equal(CheckStatus.Partial, s.StatusOf("A"));
        Assert.Equal(1, s.Tally().Partial);
        Assert.Equal(0, s.Tally().Passed);
        Assert.False(s.Satisfied("A"));
        Assert.NotNull(HardwareCatalog.Find("A")!.KnownIssue);      // and the person is warned before it runs
    }

    // ------------------------------------------------------------------ dependencies

    [Fact]
    public void ADependantWaitsForItsPrerequisiteAndIsBlockedWhenItFails()
    {
        var s = Session(Check("K"), Check("N", "K"));
        var n = s.Catalog.Single(c => c.Id == "N");

        Assert.NotNull(s.BlockedBy(n));                                   // K has not run
        Assert.Contains("has not run yet", s.BlockedBy(n)!.Reason);

        s.Record(Result("K", AutoOutcome.Fail, HumanOutcome.Fail));
        Assert.Contains("failed", s.BlockedBy(n)!.Reason);

        s.Clear("K");
        s.Record(Result("K", AutoOutcome.Pass, HumanOutcome.Pass));
        Assert.Null(s.BlockedBy(n));                                      // only an outright pass unblocks it
    }

    [Fact]
    public void AnInconclusivePrerequisiteDoesNotUnblockItsDependant()
    {
        var s = Session(Check("K"), Check("N", "K"));
        s.Record(Result("K", AutoOutcome.Pass, HumanOutcome.Fail));
        Assert.Contains("inconclusive", s.BlockedBy(s.Catalog.Single(c => c.Id == "N"))!.Reason);
    }

    [Fact]
    public void TheShippedPlanGatesManipulationOnTheVisionCheck()
    {
        var s = Session();
        foreach (var id in new[] { "N", "O", "P", "Q", "R", "S", "T", "U", "V", "X", "Z", "M" })
        {
            var c = HardwareCatalog.Find(id)!;
            Assert.NotNull(s.BlockedBy(c));
        }
        // K itself needs nothing
        Assert.Null(s.BlockedBy(HardwareCatalog.Find("K")!));
    }

    [Fact]
    public void TheFaceCheckIsBlockedByTheDetectorBoundaryWhateverElsePassed()
    {
        var s = Session();
        foreach (var c in HardwareCatalog.All) s.Record(Result(c.Id, AutoOutcome.Pass, HumanOutcome.Pass));
        var y = HardwareCatalog.Find("Y")!;
        Assert.NotNull(y.BlockedReason);
        Assert.Contains("OKAO", s.BlockedBy(y)!.Reason);
    }

    [Fact]
    public void ABlockedCheckIsRecordedAsBlockedNotFailed()
    {
        var s = Session();
        s.RecordBlocked("Y", "no detector");
        Assert.Equal(CheckStatus.Blocked, s.StatusOf("Y"));
        Assert.Equal(1, s.Tally().Blocked);
        Assert.Equal(0, s.Tally().Failed);
    }

    /// <summary>
    /// --only and --from exist to run one check on its own. A prerequisite that has simply not run must not
    /// stop that, or the flag would be useless; a prerequisite that failed still must.
    /// </summary>
    [Fact]
    public void AHandPickedCheckWarnsAboutAnUnprovenPrerequisiteButStillRuns()
    {
        var s = Session(Check("K"), Check("N", "K"));
        s.Only = new[] { "N" };
        var n = s.Catalog.Single(c => c.Id == "N");

        Assert.Null(s.BlockedBy(n));                               // not blocked: it was asked for by name
        Assert.Equal(new[] { "K" }, s.UnprovenPrerequisites["N"]);  // but the runner is told to say so

        s.Record(Result("K", AutoOutcome.Fail, HumanOutcome.Fail));
        Assert.NotNull(s.BlockedBy(n));                            // a real failure still stops it
        Assert.Contains("failed", s.BlockedBy(n)!.Reason);
    }

    [Fact]
    public void AWholeRunStillBlocksOnAPrerequisiteThatHasNotRun()
    {
        var s = Session(Check("K"), Check("N", "K"));
        Assert.False(s.DebugSelection);
        Assert.NotNull(s.BlockedBy(s.Catalog.Single(c => c.Id == "N")));
        Assert.Empty(s.UnprovenPrerequisites);
    }

    // ------------------------------------------------------------------ ordering and selection

    [Fact]
    public void NextWalksTheCatalogAndStopsWhenEverySelectedCheckIsRecorded()
    {
        var s = Session(Check("A"), Check("B"), Check("C"));
        Assert.Equal("A", s.Next()!.Id);
        s.Record(Result("A", AutoOutcome.Pass, HumanOutcome.Pass));
        Assert.Equal("B", s.Next()!.Id);
        s.Record(Result("B", AutoOutcome.Skipped, HumanOutcome.Skipped));
        Assert.Equal("C", s.Next()!.Id);
        s.Record(Result("C", AutoOutcome.Pass, HumanOutcome.Pass));
        Assert.Null(s.Next());
        Assert.True(s.Complete);
    }

    [Fact]
    public void OnlyAndFromNarrowTheRun()
    {
        var s = Session(Check("A"), Check("B"), Check("C"), Check("D"));
        s.Only = new[] { "B", "D" };
        Assert.Equal(new[] { "B", "D" }, s.Selected().Select(c => c.Id));

        var t = Session(Check("A"), Check("B"), Check("C"), Check("D"));
        t.From = "C";
        Assert.Equal(new[] { "C", "D" }, t.Selected().Select(c => c.Id));
        Assert.Equal("C", t.Next()!.Id);
        Assert.Equal(4, t.Catalog.Count);                       // the catalog is untouched; only the run narrows
    }

    [Fact]
    public void ARetryReplacesTheEarlierResultAndCountsTheAttempts()
    {
        var s = Session(Check("A"));
        s.Record(Result("A", AutoOutcome.Fail, HumanOutcome.Fail));
        s.Clear("A");
        var second = s.Record(Result("A", AutoOutcome.Pass, HumanOutcome.Pass));
        Assert.Equal(CheckStatus.Passed, s.StatusOf("A"));
        Assert.Equal(1, second.Attempts);                        // cleared first, so this is attempt one again

        var third = s.Record(Result("A", AutoOutcome.Pass, HumanOutcome.Pass));
        Assert.Equal(2, third.Attempts);                         // recorded over the top: a second attempt
    }

    // ------------------------------------------------------------------ resume

    [Fact]
    public void ASessionSurvivesBeingSavedAndReloaded()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cozmo-hw-" + Guid.NewGuid().ToString("n"));
        var file = Path.Combine(dir, "session.json");
        try
        {
            var s = Session();
            s.Record(new HardwareResult
            {
                Id = "D", Auto = AutoOutcome.Pass, Human = HumanOutcome.Pass,
                AcceptanceRecord = "acceptance-D.json", LogPath = "D.log",
            });
            s.Record(new HardwareResult { Id = "B", Auto = AutoOutcome.Fail, Human = HumanOutcome.Fail, HumanNote = "no cube connected" });
            s.RecordBlocked("Y", "no detector");
            s.From = "D";
            s.Save(file);

            var back = HardwareSession.Load(file);
            Assert.NotNull(back);
            Assert.Equal("172.31.1.1", back!.Ip);
            Assert.Equal("obb", back.Obb);
            Assert.Equal("D", back.From);
            Assert.Equal(CheckStatus.Passed, back.StatusOf("D"));
            Assert.Equal(CheckStatus.Failed, back.StatusOf("B"));
            Assert.Equal(CheckStatus.Blocked, back.StatusOf("Y"));
            Assert.Equal("no cube connected", back.Results["B"].HumanNote);
            Assert.Equal("acceptance-D.json", back.Results["D"].AcceptanceRecord);

            // and it carries on where it stopped rather than starting over
            Assert.NotEqual("D", back.Next()!.Id);
            Assert.NotEqual("B", back.Next()!.Id);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadingAMissingSessionReturnsNullSoTheRunnerStartsFresh() =>
        Assert.Null(HardwareSession.Load(Path.Combine(Path.GetTempPath(), "cozmo-hw-missing-" + Guid.NewGuid().ToString("n"), "s.json")));

    [Fact]
    public void SavingIsAtomicSoAnInterruptedWriteCannotCorruptTheSession()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cozmo-hw-" + Guid.NewGuid().ToString("n"));
        var file = Path.Combine(dir, "session.json");
        try
        {
            var s = Session();
            s.Record(Result("D", AutoOutcome.Pass, HumanOutcome.Pass));
            s.Save(file);
            s.Record(Result("B", AutoOutcome.Pass, HumanOutcome.Pass));
            s.Save(file);                                            // over the top of an existing file
            Assert.False(File.Exists(file + ".tmp"));
            Assert.Equal(2, HardwareSession.Load(file)!.Results.Count);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ------------------------------------------------------------------ the automated judges

    [Fact]
    public void TheCubeCheckNeedsAConnectionAndTelemetryNotJustDiscovery()
    {
        var b = HardwareCatalog.Find("B")!;
        Assert.Equal(AutoOutcome.Fail, b.Judge(Output("2 cube(s) heard, 0 connected")));
        Assert.Equal(AutoOutcome.Fail, b.Judge(Output("1 cube(s) heard, 1 connected\n  cube 7")));   // connected, silent
        Assert.Equal(AutoOutcome.Pass, b.Judge(Output("1 cube(s) heard, 1 connected\n  tapped cube 7")));
    }

    [Fact]
    public void TheVisionCheckNeedsTheRobotsOwnCalibrationAndALocatedCube()
    {
        var k = HardwareCatalog.Find("K")!;
        Assert.Equal(AutoOutcome.Fail, k.Judge(Output("camera calibration: NOT READ from NV storage")));
        Assert.Equal(AutoOutcome.Fail, k.Judge(Output("camera calibration from robot: fx=290")));
        Assert.Equal(AutoOutcome.Pass, k.Judge(Output("camera calibration from robot: fx=290\nnew object 1 Block_LIGHTCUBE1 Known at (200, 0, 22)")));
    }

    [Fact]
    public void TheFreeplayCheckFailsOnTheErrorTheEngineLogsWhenItCannotChoose()
    {
        var z = HardwareCatalog.Find("Z")!;
        Assert.Equal(AutoOutcome.Pass, z.Judge(Output("robot.freeplay_goal_started Hiking: priority 16")));
        Assert.Equal(AutoOutcome.Fail, z.Judge(Output("robot.freeplay_goal_started Hiking\nActivityFreeplay.NoActivityAvailableError")));
    }

    [Fact]
    public void AToolThatThrewIsAnErrorNotAFailure()
    {
        var run = new HardwareToolRun(0, "", null, "x.log", new InvalidOperationException("boom"));
        Assert.NotNull(run.Error);
        Assert.Equal(CheckStatus.Partial, HardwareSession.StatusOf(Result("X", AutoOutcome.Error, HumanOutcome.Pass)));
    }

    private static HardwareToolRun Output(string text) => new(0, text, null, "x.log", null);
}
