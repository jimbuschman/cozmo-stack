using System.Text.Json;
using Cozmo.Conformance;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The hardware runner without a robot: the ordering, the dependency gating, the two-part verdict, the
/// evidence bundle and the session file that lets a campaign survive a stop, a reboot or a flat battery.
///
/// None of this needs hardware, which is the point. The parts that must not lose a person's afternoon of
/// testing - what has been recorded, what runs next, what was kept from a run that failed - are exactly the
/// parts that can be tested here, and a hardware check is never marked passed by any of it.
/// </summary>
public class HardwareRunnerTests
{
    private static HardwareSession Session(params HardwareCheck[] catalog) =>
        new(catalog.Length > 0 ? catalog : HardwareCatalog.All, "172.31.1.1", "obb");

    private static HardwareCheck Check(string id, params string[] requires) => new()
    {
        Id = id, Name = $"check {id}", Milestone = "M0", Phase = "0. test", Subsystem = "s", Why = "w",
        Setup = "s", DoThis = "d", Success = "ok", Question = "did it work?", AutoRule = "rule",
        Requires = requires,
        Command = _ => new[] { "sensors", "1.2.3.4" },
        Judge = _ => AutoOutcome.Pass,
    };

    private static HardwareResult Result(string id, AutoOutcome auto, HumanOutcome human) =>
        new() { Id = id, Auto = auto, Human = human };

    private static HardwareToolRun Output(string text) => new(0, text, null, "x.log", null);

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "cozmo-hw-" + Guid.NewGuid().ToString("n"));

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    // ================================================================ the catalog

    [Fact]
    public void TheCatalogKeepsThePlansIdentifiersAndAddsTheNewOnes()
    {
        var ids = HardwareCatalog.All.Select(c => c.Id).ToList();
        foreach (var expected in new[] { "A", "A2", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L",
                                         "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" })
            Assert.Contains(expected, ids);                       // the original A-Z plan, traceable
        foreach (var added in new[] { "LINK", "FD", "AUD", "MOV", "IDL", "A3", "Z2", "CR1", "CR2", "CR3", "CR4", "CR7", "CR8" })
            Assert.Contains(added, ids);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void TheCampaignRunsInPhaseOrderFromConnectionToTheLongRun()
    {
        var ids = HardwareCatalog.All.Select(c => c.Id).ToList();
        Assert.Equal("LINK", ids[0]);                              // nothing below the link means anything
        Assert.Equal("Z2", ids[^2]);                              // the long run is the last thing actually run
        Assert.Equal("Y", ids[^1]);                               // and the blocked one is parked at the end

        // each phase's checks are contiguous, and the phases appear in their numbered order
        var phases = HardwareCatalog.All.Select(c => c.Phase).ToList();
        Assert.Equal(phases.Distinct().Count(), phases.Distinct().Count());
        var seen = new List<string>();
        foreach (var p in phases) if (seen.LastOrDefault() != p) { Assert.DoesNotContain(p, seen); seen.Add(p); }
        var numbers = seen.Select(p => int.Parse(p.Split('.')[0])).ToList();
        Assert.Equal(numbers.OrderBy(n => n).ToList(), numbers);
    }

    /// <summary>
    /// The charger docking geometry changed in the core review, so a failure there has to be found early
    /// rather than after an hour of manipulation checks.
    /// </summary>
    [Fact]
    public void TheChargerRunsBeforeTheManipulationSequence()
    {
        var ids = HardwareCatalog.All.Select(c => c.Id).ToList();
        foreach (var manip in new[] { "N", "O", "P", "R", "S", "T", "U" })
            Assert.True(ids.IndexOf("V") < ids.IndexOf(manip), $"the charger check should come before {manip}");
        Assert.Contains("CORE-005", HardwareCatalog.Find("V")!.CoreRegressions);
    }

    [Fact]
    public void NothingMovesBeforeTheConnectionAndTheBasicMotionCheckExceptTheAnimationItself()
    {
        var ids = HardwareCatalog.All.Select(c => c.Id).ToList();
        foreach (var c in HardwareCatalog.All.Where(c => c.MovesRobot))
            Assert.True(ids.IndexOf("LINK") < ids.IndexOf(c.Id), $"{c.Id} moves the robot and must follow LINK");

        // everything that drives across the floor waits for the small, deliberate motion check
        foreach (var driving in new[] { "CR1", "CR4", "L", "Q", "N", "V" })
            Assert.True(ids.IndexOf("MOV") < ids.IndexOf(driving), $"{driving} should follow MOV");
    }

    [Fact]
    public void EveryCheckTellsThePersonEverythingTheyNeed()
    {
        foreach (var c in HardwareCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Phase));
            Assert.False(string.IsNullOrWhiteSpace(c.Subsystem));
            Assert.False(string.IsNullOrWhiteSpace(c.Why));
            Assert.False(string.IsNullOrWhiteSpace(c.Setup));
            Assert.False(string.IsNullOrWhiteSpace(c.DoThis));
            Assert.False(string.IsNullOrWhiteSpace(c.Success));
            Assert.False(string.IsNullOrWhiteSpace(c.Cleanup));
            Assert.True(c.Timeout > TimeSpan.Zero);
            if (c.Runnable)
            {
                Assert.EndsWith("?", c.Question.Trim());
                Assert.NotEmpty(c.Evidence);                       // something is kept from every check that runs
                Assert.False(string.IsNullOrWhiteSpace(c.ExpectedTelemetry ?? c.AutoRule));
            }
        }
    }

    [Fact]
    public void EveryCommandNamesAToolTheRunnerCanCall()
    {
        var known = new[] { "connect", "sensors", "cubes", "calibrate", "drive", "camera", "face", "tone",
                            "anim", "behavior", "sing", "offtreads", "reactions", "vision", "bodyangle",
                            "manip", "freeplay", "core" };
        var o = new HardwareRunOptions { Ip = "172.31.1.1", Obb = "obb", EvidenceDirectory = TempDir() };
        foreach (var c in HardwareCatalog.All)
        {
            var args = c.Command(o);
            Assert.NotEmpty(args);
            Assert.Contains(args[0], known);
            Assert.Equal("172.31.1.1", args[1]);
            // a check that says it needs the assets actually reaches for them, by --obb or by a path under it
            if (c.NeedsObb) Assert.Contains(args, x => x == "--obb" || x.Contains("obb", StringComparison.OrdinalIgnoreCase));
        }
        if (Directory.Exists(o.EvidenceDirectory)) Directory.Delete(o.EvidenceDirectory, true);
    }

    /// <summary>
    /// The core-review corrections that hardware can say something about have a check; the two it cannot -
    /// a disposed component's callbacks and an event fan-out - deliberately have none, and no check pretends
    /// otherwise.
    /// </summary>
    [Fact]
    public void TheCoreReviewCorrectionsHardwareCanSpeakToAreCovered()
    {
        var covered = HardwareCatalog.All.SelectMany(c => c.CoreRegressions).Distinct().ToList();
        foreach (var id in new[] { "CORE-001", "CORE-002", "CORE-003", "CORE-004", "CORE-005",
                                   "CORE-006", "CORE-007", "CORE-008", "CORE-009" })
            Assert.Contains(id, covered);
        Assert.DoesNotContain("CORE-010", covered);
        Assert.DoesNotContain("CORE-011", covered);
    }

    /// <summary>
    /// Every check's evidence goes in a directory named after it, and Windows cannot create a directory
    /// called CON, PRN, AUX, NUL, COM1 or LPT1. A campaign that dies on its first check because of a
    /// filename is a silly way to lose an afternoon, so the ids are kept clear of them.
    /// </summary>
    [Fact]
    public void NoCheckIsNamedAfterSomethingTheFilesystemRefusesToCreate()
    {
        var reserved = new[] { "CON", "PRN", "AUX", "NUL", "CLOCK$",
                               "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                               "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        foreach (var c in HardwareCatalog.All)
        {
            Assert.DoesNotContain(c.Id.ToUpperInvariant(), reserved);
            Assert.Equal(-1, c.Id.IndexOfAny(Path.GetInvalidFileNameChars()));
        }
    }

    [Fact]
    public void TheThingsAPersonHasToFetchAreDeclared()
    {
        Assert.Contains("V", HardwareCatalog.All.Where(c => c.NeedsCharger).Select(c => c.Id));
        Assert.Contains("W", HardwareCatalog.All.Where(c => c.NeedsCharger).Select(c => c.Id));
        Assert.Contains("K", HardwareCatalog.All.Where(c => c.NeedsCube).Select(c => c.Id));
        Assert.Contains("N", HardwareCatalog.All.Where(c => c.NeedsCube).Select(c => c.Id));
        Assert.Contains("G", HardwareCatalog.All.Where(c => c.NeedsHandling).Select(c => c.Id));
        Assert.Contains("CR8", HardwareCatalog.All.Where(c => c.NeedsHandling).Select(c => c.Id));
        // a check that needs a cube depends on the cube checks, directly or through something that does
        bool DependsOnACubeCheck(HardwareCheck c, int depth = 0) =>
            c.Id is "B" or "K" || (depth < 6 && c.Requires.Any(r => HardwareCatalog.Find(r) is { } p && DependsOnACubeCheck(p, depth + 1)));
        foreach (var c in HardwareCatalog.All.Where(c => c.NeedsCube && c.Runnable))
            Assert.True(DependsOnACubeCheck(c), $"{c.Id} needs a cube but does not depend on the cube checks");
    }

    [Fact]
    public void TheVisionCheckDoesNotClaimToCloseTheOpenSourceGap()
    {
        var k = HardwareCatalog.Find("K")!;
        Assert.Contains("M11-005", k.FidelityRecords);
        Assert.NotNull(k.KnownIssue);
        Assert.Contains("does not close that", k.KnownIssue!, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================ the two-part verdict

    [Theory]
    [InlineData(AutoOutcome.Pass, HumanOutcome.Pass, CheckStatus.Passed)]
    [InlineData(AutoOutcome.Fail, HumanOutcome.Fail, CheckStatus.Failed)]
    [InlineData(AutoOutcome.Error, HumanOutcome.Fail, CheckStatus.Failed)]
    [InlineData(AutoOutcome.Pass, HumanOutcome.Fail, CheckStatus.Partial)]
    [InlineData(AutoOutcome.Fail, HumanOutcome.Pass, CheckStatus.Partial)]
    [InlineData(AutoOutcome.Pass, HumanOutcome.Unsure, CheckStatus.Unsure)]
    [InlineData(AutoOutcome.Fail, HumanOutcome.Unsure, CheckStatus.Unsure)]
    [InlineData(AutoOutcome.Pass, HumanOutcome.NotAsked, CheckStatus.Pending)]
    [InlineData(AutoOutcome.Pass, HumanOutcome.Skipped, CheckStatus.Skipped)]
    public void TheTwoHalvesOfAVerdictAreKeptApart(AutoOutcome auto, HumanOutcome human, CheckStatus expected) =>
        Assert.Equal(expected, HardwareSession.StatusOf(Result("X", auto, human)));

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

    /// <summary>
    /// Unsure is a real answer. It is not a pass, it does not satisfy a dependant, and it stays on the list
    /// of things to run again - but it is not a failure either, because nothing was actually observed to be
    /// wrong.
    /// </summary>
    [Fact]
    public void UnsureIsRecordedAsItselfAndSatisfiesNothing()
    {
        var s = Session(Check("K"), Check("N", "K"));
        s.Record(Result("K", AutoOutcome.Pass, HumanOutcome.Unsure));
        Assert.Equal(CheckStatus.Unsure, s.StatusOf("K"));
        Assert.Equal(1, s.Tally().Unsure);
        Assert.Equal(0, s.Tally().Failed);
        Assert.Equal(0, s.Tally().Passed);
        Assert.False(s.Satisfied("K"));
        Assert.NotNull(s.GatedBy(s.Catalog.Single(c => c.Id == "N")));
        Assert.Contains("K", s.Unresolved().Select(c => c.Id));
    }

    /// <summary>
    /// A check that did not finish says nothing about the behaviour under test. It is not a failure, it is
    /// not done, and resuming picks it up rather than walking past it.
    /// </summary>
    [Fact]
    public void AnInterruptedCheckIsNotAFailureAndIsPickedUpAgain()
    {
        var s = Session(Check("A"), Check("B"));
        s.Record(new HardwareResult { Id = "A", Auto = AutoOutcome.NotRun, Human = HumanOutcome.NotAsked, InterruptedReason = "the link went" });
        Assert.Equal(CheckStatus.Interrupted, s.StatusOf("A"));
        Assert.Equal(0, s.Tally().Failed);
        Assert.Equal(1, s.Tally().Interrupted);
        Assert.Equal(2, s.Tally().Outstanding);                    // the interrupted one and the one never run
        Assert.Equal("A", s.Next()!.Id);                           // it is the next thing to do, not skipped
        Assert.Contains("A", s.Unresolved().Select(c => c.Id));
    }

    [Fact]
    public void TheDisconnectCheckIsTheOneWhereALostLinkIsTheSubject()
    {
        Assert.True(HardwareCatalog.Find("CR2")!.ExpectsDisconnect);
        Assert.False(HardwareCatalog.Find("CR1")!.ExpectsDisconnect);
        Assert.Contains("CORE-002", HardwareCatalog.Find("CR2")!.CoreRegressions);
    }

    // ================================================================ dependencies

    [Fact]
    public void ADependantWaitsForItsPrerequisiteAndIsBlockedWhenItFails()
    {
        var s = Session(Check("K"), Check("N", "K"));
        var n = s.Catalog.Single(c => c.Id == "N");

        Assert.NotNull(s.GatedBy(n));                                   // K has not run
        Assert.Contains("has not run yet", s.GatedBy(n)!.Reason);

        s.Record(Result("K", AutoOutcome.Fail, HumanOutcome.Fail));
        Assert.Contains("failed", s.GatedBy(n)!.Reason);

        s.Clear("K");
        s.Record(Result("K", AutoOutcome.Pass, HumanOutcome.Pass));
        Assert.Null(s.GatedBy(n));                                      // only an outright pass unblocks it
    }

    [Fact]
    public void AnInconclusivePrerequisiteDoesNotUnblockItsDependant()
    {
        var s = Session(Check("K"), Check("N", "K"));
        s.Record(Result("K", AutoOutcome.Pass, HumanOutcome.Fail));
        Assert.Contains("inconclusive", s.GatedBy(s.Catalog.Single(c => c.Id == "N"))!.Reason);
    }

    [Fact]
    public void TheShippedPlanGatesManipulationOnTheVisionCheck()
    {
        var s = Session();
        foreach (var id in new[] { "N", "O", "P", "Q", "R", "S", "T", "U", "V", "X", "Z", "M" })
            Assert.NotNull(s.GatedBy(HardwareCatalog.Find(id)!));
        Assert.Null(s.GatedBy(HardwareCatalog.Find("LINK")!));            // the first check needs nothing
    }

    [Fact]
    public void TheFaceCheckIsBlockedByTheDetectorBoundaryWhateverElsePassed()
    {
        var s = Session();
        foreach (var c in HardwareCatalog.All) s.Record(Result(c.Id, AutoOutcome.Pass, HumanOutcome.Pass));
        var y = HardwareCatalog.Find("Y")!;
        Assert.False(y.Runnable);
        Assert.Contains("OKAO", y.BlockedReason!);
        Assert.Contains("BLOCKED_EXTERNAL", y.BlockedReason!);
        Assert.Contains("OKAO", s.StaticBlock(y)!.Reason);
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

    [Fact]
    public void AHandPickedCheckWarnsAboutAnUnprovenPrerequisiteButStillRuns()
    {
        var s = Session(Check("K"), Check("N", "K"));
        s.Only = new[] { "N" };
        var n = s.Catalog.Single(c => c.Id == "N");

        Assert.Null(s.GatedBy(n));                               // not blocked: it was asked for by name
        Assert.Equal(new[] { "K" }, s.UnprovenPrerequisites["N"]);  // but the runner is told to say so

        s.Record(Result("K", AutoOutcome.Fail, HumanOutcome.Fail));
        Assert.NotNull(s.GatedBy(n));                            // a real failure still stops it
        Assert.Contains("failed", s.GatedBy(n)!.Reason);
    }

    [Fact]
    public void AWholeRunStillBlocksOnAPrerequisiteThatHasNotRun()
    {
        var s = Session(Check("K"), Check("N", "K"));
        Assert.False(s.DebugSelection);
        Assert.NotNull(s.GatedBy(s.Catalog.Single(c => c.Id == "N")));
        Assert.Empty(s.UnprovenPrerequisites);
    }

    // ================================================================ ordering and selection

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
    public void RerunningOneCheckKeepsEverythingElseThatWasRecorded()
    {
        var s = Session(Check("A"), Check("B"), Check("C"));
        s.Record(Result("A", AutoOutcome.Pass, HumanOutcome.Pass));
        s.Record(Result("B", AutoOutcome.Fail, HumanOutcome.Fail));
        s.Record(Result("C", AutoOutcome.Pass, HumanOutcome.Pass));

        s.Clear("B");
        s.Only = new[] { "B" };
        Assert.Equal("B", s.Next()!.Id);
        Assert.Equal(CheckStatus.Passed, s.StatusOf("A"));      // untouched
        Assert.Equal(CheckStatus.Passed, s.StatusOf("C"));
    }

    [Fact]
    public void RunningTheUnresolvedOnesAgainPicksUpFailuresUnsuresAndInterruptions()
    {
        var s = Session(Check("A"), Check("B"), Check("C"), Check("D"), Check("E"));
        s.Record(Result("A", AutoOutcome.Pass, HumanOutcome.Pass));
        s.Record(Result("B", AutoOutcome.Fail, HumanOutcome.Fail));
        s.Record(Result("C", AutoOutcome.Pass, HumanOutcome.Fail));                 // inconclusive
        s.Record(Result("D", AutoOutcome.Pass, HumanOutcome.Unsure));
        s.Record(new HardwareResult { Id = "E", InterruptedReason = "the link went" });

        Assert.Equal(new[] { "B", "C", "D", "E" }, s.Unresolved().Select(c => c.Id));
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

    // ================================================================ resume

    [Fact]
    public void ASessionSurvivesBeingSavedAndReloaded()
    {
        var dir = TempDir();
        var file = Path.Combine(dir, "session.json");
        try
        {
            var s = Session();
            s.EvidenceDirectory = Path.Combine(dir, "run-1");
            s.Record(new HardwareResult
            {
                Id = "D", Auto = AutoOutcome.Pass, Human = HumanOutcome.Pass,
                AcceptanceRecord = "acceptance.json", LogPath = "console.log",
            });
            s.Record(new HardwareResult { Id = "B", Auto = AutoOutcome.Fail, Human = HumanOutcome.Fail, HumanNote = "no cube connected" });
            s.Record(new HardwareResult { Id = "E", InterruptedReason = "the battery went flat" });
            s.RecordBlocked("Y", "no detector");
            s.From = "D";
            s.Save(file);

            var back = HardwareSession.Load(file);
            Assert.NotNull(back);
            Assert.Equal("172.31.1.1", back!.Ip);
            Assert.Equal("obb", back.Obb);
            Assert.Equal("D", back.From);
            Assert.Equal(Path.Combine(dir, "run-1"), back.EvidenceDirectory);   // the campaign keeps one directory
            Assert.Equal(CheckStatus.Passed, back.StatusOf("D"));
            Assert.Equal(CheckStatus.Failed, back.StatusOf("B"));
            Assert.Equal(CheckStatus.Interrupted, back.StatusOf("E"));
            Assert.Equal(CheckStatus.Blocked, back.StatusOf("Y"));
            Assert.Equal("no cube connected", back.Results["B"].HumanNote);
            Assert.Equal("the battery went flat", back.Results["E"].InterruptedReason);

            // and it carries on where it stopped rather than starting over: the first check with no result,
            // and the interrupted one is still waiting rather than counted as done
            Assert.Equal("F", back.Next()!.Id);          // --from D, and D itself is recorded
            Assert.DoesNotContain("D", back.Unresolved().Select(c => c.Id));
            Assert.Contains("E", back.Unresolved().Select(c => c.Id));
            back.Only = new[] { "D", "E" };
            Assert.Equal("E", back.Next()!.Id);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadingAMissingSessionReturnsNullSoTheRunnerStartsFresh() =>
        Assert.Null(HardwareSession.Load(Path.Combine(TempDir(), "s.json")));

    [Fact]
    public void SavingIsAtomicSoAnInterruptedWriteCannotCorruptTheSession()
    {
        var dir = TempDir();
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

    // ================================================================ evidence

    [Fact]
    public void EachCheckKeepsItsEvidenceInItsOwnDirectory()
    {
        var root = TempDir();
        try
        {
            var o = new HardwareRunOptions { Ip = "1.2.3.4", EvidenceDirectory = root };
            var a = o.TestDirectory("LINK");
            var b = o.TestDirectory("V");
            Assert.NotEqual(a, b);
            Assert.True(Directory.Exists(a));
            Assert.StartsWith(a, o.Acceptance("LINK"));
            Assert.StartsWith(a, o.Frames("LINK"));
            Assert.StartsWith(a, o.FrameLog("LINK"));
            Assert.StartsWith(Path.Combine(root, "tests"), a);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void TheEvidenceKeptIsWhatTheCheckAskedForAndNotTheWholeTranscript()
    {
        var check = Check("X") with
        {
            Evidence = new[]
            {
                new EvidenceItem("docking error signal", "DockingErrorSignal") { Limit = 2 },
                new EvidenceItem("contacts", "on charger"),
                new EvidenceItem("never happens", "zzz"),
            },
        };
        var output = string.Join('\n', new[]
        {
            "DockingErrorSignal x=1", "noise", "DockingErrorSignal x=2", "DockingErrorSignal x=3",
            "on charger: True", "more noise",
        });

        var captured = HardwareEvidence.Capture(check, output);
        Assert.Equal(new[] { "DockingErrorSignal x=1", "DockingErrorSignal x=2", "... 1 more line(s) matching 'DockingErrorSignal' in the log" },
                     captured["docking error signal"]);
        Assert.Equal(new[] { "on charger: True" }, captured["contacts"]);
        Assert.False(captured.ContainsKey("never happens"));
        Assert.DoesNotContain("noise", string.Join(" ", captured.Values.SelectMany(v => v)));
    }

    [Fact]
    public void TroubleIsKeptWhetherOrNotTheCheckThoughtToAskForIt()
    {
        var warnings = HardwareEvidence.Warnings(string.Join('\n', new[]
        {
            "all fine",
            "warning: the cube battery is low",
            "UNSAFE: camera calibration not read",
            "System.InvalidOperationException: not connected",
            "refusing to run autonomous freeplay on a made-up geometry",
        }));
        Assert.Equal(4, warnings.Length);
        Assert.DoesNotContain("all fine", warnings);
    }

    [Fact]
    public void ACheckRecordsWhatItWasWhatHappenedAndWhatWasKept()
    {
        var root = TempDir();
        try
        {
            var o = new HardwareRunOptions { Ip = "1.2.3.4", Obb = "obb", EvidenceDirectory = root };
            var check = HardwareCatalog.Find("V")!;
            var dir = o.TestDirectory(check.Id);
            File.WriteAllText(Path.Combine(dir, "console.log"), "log");

            var result = new HardwareResult
            {
                Id = check.Id, Auto = AutoOutcome.Fail, Human = HumanOutcome.Fail,
                HumanNote = "he stopped short of the contacts", Attempts = 2,
                StartedUtc = DateTime.UtcNow.AddMinutes(-2), FinishedUtc = DateTime.UtcNow,
            };
            var run = Output("DockingErrorSignal x=1\non charger: False\nwarning: marker lost");
            var record = HardwareEvidence.WriteRecord(check, result, run, o, dir);

            Assert.Equal("V", record.Id);
            Assert.Equal(check.Phase, record.Phase);
            Assert.Equal(check.Why, record.Purpose);
            Assert.Equal(check.Success, record.ExpectedPhysical);
            Assert.Contains("CORE-005", record.CoreRegressions);
            Assert.Contains("M13-008", record.FidelityRecords);
            Assert.Equal("Failed", record.Status);
            Assert.Equal("he stopped short of the contacts", record.HumanNote);
            Assert.Contains("docking error signal", record.Captured.Keys);
            Assert.Contains(record.Warnings, w => w.Contains("marker lost"));
            Assert.Contains("console.log", record.Files);
            Assert.NotNull(record.ElapsedSeconds);

            // and it is on disk, as JSON, beside the log
            var onDisk = JsonSerializer.Deserialize<EvidenceRecord>(File.ReadAllText(Path.Combine(dir, "record.json")), Json);
            Assert.Equal("V", onDisk!.Id);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ABlockedCheckStillGetsARecordWithItsReason()
    {
        var root = TempDir();
        try
        {
            var o = new HardwareRunOptions { Ip = "1.2.3.4", EvidenceDirectory = root };
            var y = HardwareCatalog.Find("Y")!;
            var record = HardwareEvidence.WriteRecord(y, new HardwareResult { Id = "Y", BlockedReason = y.BlockedReason }, null, o, o.TestDirectory("Y"));
            Assert.Equal("Blocked", record.Status);
            Assert.Contains("OKAO", record.BlockedReason!);
            Assert.Empty(record.Captured);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void TheRunEndsWithSomethingForAMachineAndSomethingForAPerson()
    {
        var root = TempDir();
        try
        {
            var o = new HardwareRunOptions { Ip = "172.31.1.1", Obb = "obb", EvidenceDirectory = root };
            var s = Session();
            s.Only = new[] { "LINK", "V", "Y" };
            s.Record(new HardwareResult { Id = "LINK", Auto = AutoOutcome.Pass, Human = HumanOutcome.Pass });
            s.Record(new HardwareResult { Id = "V", Auto = AutoOutcome.Fail, Human = HumanOutcome.Fail, HumanNote = "stopped short of the contacts" });
            s.RecordBlocked("Y", HardwareCatalog.Find("Y")!.BlockedReason!);

            var (resultsPath, summaryPath) = HardwareEvidence.WriteRun(s, o);
            var results = JsonSerializer.Deserialize<HardwareEvidence.RunResults>(File.ReadAllText(resultsPath), Json)!;
            Assert.Equal("172.31.1.1", results.Robot);
            Assert.Equal(3, results.Tests.Length);
            Assert.Equal(1, results.Tally.Passed);
            Assert.Equal(1, results.Tally.Failed);
            Assert.Equal(1, results.Tally.Blocked);

            var summary = File.ReadAllText(summaryPath);
            Assert.Contains("# Cozmo hardware acceptance", summary);
            Assert.Contains("1 passed, 1 failed", summary);
            Assert.Contains("Investigation items", summary);
            Assert.Contains("stopped short of the contacts", summary);
            Assert.Contains("CORE-005", summary);
            Assert.Contains("does not change any fidelity record", summary);
            Assert.Contains("OKAO", summary);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    /// <summary>
    /// The plan document and the executable campaign are the same campaign. The document's table is generated
    /// from the catalog, so this is what stops the two drifting apart when a check is added or renamed.
    /// </summary>
    [Fact]
    public void ThePlanDocumentAndTheCatalogAreTheSameCampaign()
    {
        var doc = PlanDocument();
        if (doc is null) return;                        // the document is not beside the test assembly
        var text = File.ReadAllText(doc);

        foreach (var c in HardwareCatalog.All)
        {
            Assert.Contains($"| {c.Id} | **{c.Name}**", text);
            if (!c.Runnable) Assert.Contains("BLOCKED_EXTERNAL", text);
        }

        var rows = text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                       .Where(l => l.StartsWith("| ") && l.Contains(" | **"))
                       .Select(l => l.Split('|')[1].Trim())
                       .ToList();
        Assert.NotEmpty(rows);
        foreach (var id in rows) Assert.NotNull(HardwareCatalog.Find(id));
        Assert.Equal(HardwareCatalog.All.Count, rows.Count);

        // the promises the document makes about what hardware does not do
        Assert.Contains("It does not upgrade provenance", text);
        Assert.Contains("M11-005", text);
        Assert.Contains("CORE-010", text);
        Assert.Contains("CORE-011", text);
    }

    [Fact]
    public void TheStaticPathAuditClassifiesEveryCheckAndLeavesNoBrokenPath()
    {
        var plan = PlanDocument();
        if (plan is null) return;
        var audit = Path.Combine(Path.GetDirectoryName(plan)!, "HARDWARE_PATH_AUDIT.md");
        Assert.True(File.Exists(audit));
        var text = File.ReadAllText(audit);
        Assert.DoesNotContain("BROKEN_TEST |", text);
        Assert.DoesNotContain("BROKEN_PRODUCTION_INTEGRATION |", text);
        foreach (var c in HardwareCatalog.All)
        {
            string verdict = c.Runnable ? "VALID" : "BLOCKED_EXTERNAL";
            var row = text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                          .SingleOrDefault(l => l.StartsWith($"| {c.Id} |", StringComparison.Ordinal));
            Assert.NotNull(row);
            Assert.EndsWith($"| {verdict} |", row);
        }
        var rows = text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                       .Where(l => HardwareCatalog.All.Any(c => l.StartsWith($"| {c.Id} |", StringComparison.Ordinal)))
                       .ToList();
        Assert.Equal(HardwareCatalog.All.Count, rows.Count);
    }

    private static string? PlanDocument()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var p = Path.Combine(d.FullName, "re-analysis", "HARDWARE_TEST_PLAN.md");
            if (File.Exists(p)) return p;
            d = d.Parent;
        }
        return null;
    }

    // ================================================================ the command line, with no robot

    /// <summary>
    /// The three entry points that never touch the robot, driven the way a person drives them. They are the
    /// ones used when the robot is not to hand - between sittings, or when writing up - so they must work
    /// with nothing plugged in.
    /// </summary>
    [Fact]
    public async Task ListingTheCampaignNeedsNoRobot()
    {
        var output = new StringWriter();
        var was = Console.Out;
        try { Console.SetOut(output); Assert.Equal(0, await HardwareRunner.Run(new[] { "hardware-test", "--list" })); }
        finally { Console.SetOut(was); }

        var text = output.ToString();
        Assert.Contains("COZMO HARDWARE ACCEPTANCE", text);
        Assert.Contains("LINK", text);
        Assert.Contains("charger", text);
        Assert.Contains("BLOCKED", text);                     // Y is shown as blocked rather than offered
        foreach (var c in HardwareCatalog.All) Assert.Contains(c.Id, text);
    }

    [Fact]
    public async Task StatusAndExportWorkFromASavedSessionAlone()
    {
        var root = TempDir();
        var sessionFile = Path.Combine(root, "session.json");
        try
        {
            var s = Session();
            s.EvidenceDirectory = Path.Combine(root, "run");
            s.Only = new[] { "LINK", "V", "Y" };
            s.Record(new HardwareResult { Id = "LINK", Auto = AutoOutcome.Pass, Human = HumanOutcome.Pass });
            s.Record(new HardwareResult { Id = "V", Auto = AutoOutcome.Fail, Human = HumanOutcome.Fail, HumanNote = "stopped short" });
            s.RecordBlocked("Y", HardwareCatalog.Find("Y")!.BlockedReason!);
            s.Save(sessionFile);

            var output = new StringWriter();
            var was = Console.Out;
            try
            {
                Console.SetOut(output);
                Assert.Equal(0, await HardwareRunner.Run(new[] { "hardware-test", "172.31.1.1", "--session", sessionFile, "--status" }));
                Assert.Equal(0, await HardwareRunner.Run(new[] { "hardware-test", "172.31.1.1", "--session", sessionFile, "--export" }));
            }
            finally { Console.SetOut(was); }

            var text = output.ToString();
            Assert.Contains("STATUS", text);
            Assert.Contains("PASSED", text);
            Assert.Contains("FAILED", text);
            Assert.Contains("BLOCKED", text);
            Assert.DoesNotContain("next:", text);      // all three were recorded, so there is nothing next

            // the export goes to the run directory the session was started with, not a new one
            Assert.True(File.Exists(Path.Combine(root, "run", HardwareEvidence.ResultsFile)));
            Assert.True(File.Exists(Path.Combine(root, "run", HardwareEvidence.SummaryFile)));
            Assert.Contains("stopped short", File.ReadAllText(Path.Combine(root, "run", HardwareEvidence.SummaryFile)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // ================================================================ each check isolates what it names

    /// <summary>
    /// The complaint that started this: three checks - the derived-state reactions, the calibration request
    /// and the unexpected-movement detector - all ran the same generic listener for two minutes and passed on
    /// any reaction at all. The campaign felt like it kept running the same test because it was, and one of
    /// them (the calibration) passed on reports the robot sends on every connection whatever anybody does.
    ///
    /// A check now names the reaction it is about, the tool opens a window for it and says what to do, and
    /// nothing that happens outside that window - or that was not asked for - can satisfy it.
    /// </summary>
    [Fact]
    public void AReactionCheckIsNotSatisfiedByAReactionItDidNotAskFor()
    {
        // a transcript full of other reactions, and the summary line saying what was actually asked for
        string Unrelated(string missing) => string.Join('\n', new[]
        {
            "REACTION RobotPickedUp -> ReactToRobotPickedUp",
            "REACTION ReturnedToTreads -> ReactToReturnedToTreads",
            "reactions fired: RobotPickedUp, ReturnedToTreads",
            $"expected reactions: {missing}=no",
            $"expected reactions missing: {missing}",
        });

        foreach (var (id, trigger) in new[] { ("H", "RobotOnBack"), ("C", "RobotFalling"),
                                              ("J", "UnexpectedMovement"), ("M", "CubeMoved") })
        {
            var c = HardwareCatalog.Find(id)!;
            Assert.Equal(AutoOutcome.Fail, c.Judge(Output(Unrelated(trigger))));
            Assert.Equal(AutoOutcome.Pass, c.Judge(Output($"window {trigger}: fired\nexpected reactions: all seen")));
        }
    }

    [Fact]
    public void EveryReactionCheckNamesTheReactionsItIsAboutAndTheyDiffer()
    {
        var o = new HardwareRunOptions { Ip = "172.31.1.1", Obb = "obb", EvidenceDirectory = TempDir() };
        var expectations = new Dictionary<string, string>();
        foreach (var id in new[] { "H", "C", "J", "M" })
        {
            var args = HardwareCatalog.Find(id)!.Command(o).ToList();
            int at = args.IndexOf("--expect");
            Assert.True(at >= 0, $"{id} should name the reactions it is about");
            expectations[id] = args[at + 1];
        }
        Assert.Equal("RobotFalling", expectations["C"]);                       // the drop, not being picked up
        Assert.Equal("UnexpectedMovement", expectations["J"]);
        Assert.Contains("RobotOnBack", expectations["H"]);
        Assert.Contains("CubeMoved", expectations["M"]);
        Assert.Equal(expectations.Count, expectations.Values.Distinct().Count());   // no two are the same test

        // and the one that needs the wheels turning makes them turn
        Assert.Contains("--provoke-movement", HardwareCatalog.Find("J")!.Command(o));
        if (Directory.Exists(o.EvidenceDirectory)) Directory.Delete(o.EvidenceDirectory, true);
    }

    /// <summary>
    /// The calibration check used to watch a two-minute reaction run and pass on the word "MotorCalibration",
    /// which the robot sends on every connection. It now asks for one and judges only the answer.
    /// </summary>
    [Fact]
    public void TheCalibrationCheckAsksForOneAndJudgesOnlyWhatCameBack()
    {
        var o = new HardwareRunOptions { Ip = "172.31.1.1", Obb = "obb", EvidenceDirectory = TempDir() };
        var i = HardwareCatalog.Find("I")!;
        Assert.Equal("calibrate", i.Command(o)[0]);

        // the connection-time reports, which arrive whatever anybody does
        Assert.Equal(AutoOutcome.Fail, i.Judge(Output(
            "  [  0.30s] MotorCalibration motor=MOTOR_HEAD started=True auto=True   (connection-time)\n" +
            "  [  1.90s] MotorCalibration motor=MOTOR_HEAD started=False auto=True   (connection-time)\n" +
            "calibration honoured: NO")));
        Assert.Equal(AutoOutcome.Pass, i.Judge(Output(
            "  ASKING NOW: StartMotorCalibration head=1 lift=0\n" +
            "  [  6.10s] MotorCalibration motor=MOTOR_HEAD started=True auto=False   <- after the request\n" +
            "calibration honoured: yes (started at 6.10s, finished at 7.40s, 1.30s of movement)")));
        if (Directory.Exists(o.EvidenceDirectory)) Directory.Delete(o.EvidenceDirectory, true);
    }

    /// <summary>
    /// The lift check drives both source-backed endpoints through the production API. Merely printing the
    /// two values is not enough: both actions and both telemetry comparisons must succeed.
    /// </summary>
    [Fact]
    public void TheLiftCheckIsGuidedAndNeedsBothEndsOfTheTravel()
    {
        var o = new HardwareRunOptions { Ip = "172.31.1.1", EvidenceDirectory = TempDir() };
        var d = HardwareCatalog.Find("D")!;
        Assert.Contains("--guide-lift", d.Command(o));
        Assert.Equal(AutoOutcome.Fail, d.Judge(Output("lift sequence: down seen=yes, raised seen=NO, actions completed=yes, both endpoints validated=NO")));
        Assert.Equal(AutoOutcome.Pass, d.Judge(Output("lift sequence: down seen=yes, raised seen=yes, actions completed=yes, both endpoints validated=yes")));
        Assert.Contains("production motion API", d.DoThis);
        if (Directory.Exists(o.EvidenceDirectory)) Directory.Delete(o.EvidenceDirectory, true);
    }

    [Fact]
    public void TheOffTreadsCheckNeedsMoreThanASingleTransition()
    {
        var g = HardwareCatalog.Find("G")!;
        Assert.Equal(AutoOutcome.Fail, g.Judge(Output("off-treads OnTreads -> InAir")));
        Assert.Equal(AutoOutcome.Pass, g.Judge(Output("off-treads OnTreads -> InAir\noff-treads InAir -> OnBack")));
    }

    /// <summary>
    /// Q and X drive to the same pose with the same command. What makes X the lattice-planner check is the
    /// cube in the way, so its judge requires a plan that actually had an obstacle in it; Q's output, with
    /// nothing in the way, must not satisfy it.
    /// </summary>
    [Fact]
    public void ThePlannerCheckNeedsAPlanThatWentRoundSomething()
    {
        var x = HardwareCatalog.Find("X")!;
        Assert.Equal(AutoOutcome.Fail, x.Judge(Output("DriveToObject success=yes")));
        Assert.Equal(AutoOutcome.Fail, x.Judge(Output("lattice plan: 4 primitive(s), 0 obstacle(s)\nDriveToObject success=yes")));
        Assert.Equal(AutoOutcome.Pass, x.Judge(Output("lattice plan: 7 primitive(s), 1 obstacle(s)\nDriveToObject success=yes")));
    }

    [Fact]
    public void TheIdleCheckNeedsIdleToHaveDoneSomething()
    {
        var idl = HardwareCatalog.Find("IDL")!;
        Assert.Equal(AutoOutcome.Fail, idl.Judge(Output("idle actions taken: 0")));
        Assert.Equal(AutoOutcome.Pass, idl.Judge(Output("idle actions taken: 7")));
    }

    /// <summary>
    /// Every check that waits for the person to do something says what to do. The two the campaign found
    /// unanswerable - the lift reading and the disconnect - say it in the words someone holding the robot
    /// needs, and what they should see afterwards.
    /// </summary>
    [Fact]
    public void AChecksInstructionsSayWhatToDoAndWhatItLooksLike()
    {
        foreach (var c in HardwareCatalog.All.Where(c => c.Runnable))
        {
            Assert.True(c.DoThis.Length > 25, $"{c.Id} does not say enough about what to do");
            Assert.True(c.Success.Length > 25, $"{c.Id} does not say enough about what a pass looks like");
        }
        Assert.Contains("production motion API", HardwareCatalog.Find("D")!.DoThis);
        Assert.Contains("stop", HardwareCatalog.Find("CR2")!.Success, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cuts the link", HardwareCatalog.Find("CR2")!.DoThis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prompt", HardwareCatalog.Find("H")!.DoThis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DROP", HardwareCatalog.Find("C")!.DoThis);
        Assert.Contains("HOLD", HardwareCatalog.Find("J")!.DoThis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ASKING NOW", HardwareCatalog.Find("I")!.DoThis);
    }

    // ================================================================ gated is not blocked

    /// <summary>
    /// The regression for what the first campaign did to itself. D was run and answered unsure - a real
    /// observation - and MOV, which depends on it, was written down as "BLOCKED: D has not run yet". D had
    /// run. The entry was not about MOV at all, and it would have outlived the state that produced it: rerun
    /// D, pass it, and MOV would still have been blocked by a sentence that was no longer true.
    ///
    /// A prerequisite that is unresolved gates its dependants. It does not decide anything about them, so
    /// nothing is written down: they stay pending and become eligible by themselves.
    /// </summary>
    [Fact]
    public void AnUnsurePrerequisiteLeavesItsDependantPendingRatherThanBlocked()
    {
        var s = Session();
        s.Record(Result("LINK", AutoOutcome.Pass, HumanOutcome.Pass));
        s.Record(Result("D", AutoOutcome.Pass, HumanOutcome.Unsure));

        var mov = HardwareCatalog.Find("MOV")!;
        Assert.Null(s.StaticBlock(mov));                              // nothing about the build stops MOV
        Assert.NotNull(s.GatedBy(mov));                               // it is simply not its turn
        Assert.Contains("was unsure", s.GatedBy(mov)!.Reason);        // and the wording says so
        Assert.DoesNotContain("has not run yet", s.GatedBy(mov)!.Reason);

        Assert.Equal(CheckStatus.Pending, s.StatusOf("MOV"));
        Assert.False(s.Results.ContainsKey("MOV"));                   // nothing written down
        Assert.Contains("MOV", s.Gated().Select(g => g.Check.Id));
        Assert.DoesNotContain(HardwareCatalog.Find("MOV"), s.Selected().Where(c => s.Next() == c));
    }

    [Fact]
    public void RerunningThePrerequisiteAndPassingItMakesTheDependantTheNextThingToDo()
    {
        var s = Session();
        s.Only = null;
        s.Record(Result("LINK", AutoOutcome.Pass, HumanOutcome.Pass));
        s.Record(Result("D", AutoOutcome.Pass, HumanOutcome.Unsure));
        foreach (var id in new[] { "F", "FD", "AUD", "A", "A2", "A3" })
            s.Record(Result(id, AutoOutcome.Pass, HumanOutcome.Pass));
        Assert.NotEqual("MOV", s.Next()?.Id);                         // gated: not offered

        s.Clear("D");                                                 // --rerun D
        s.Record(Result("D", AutoOutcome.Pass, HumanOutcome.Pass));
        Assert.Null(s.GatedBy(HardwareCatalog.Find("MOV")!));
        Assert.Equal("MOV", s.Next()!.Id);                            // eligible by itself, nothing to clean up
    }

    [Fact]
    public void AnInconclusivePrerequisiteLeavesTheSongsPending()
    {
        var s = Session();
        foreach (var id in new[] { "LINK", "D", "F", "FD", "AUD" }) s.Record(Result(id, AutoOutcome.Pass, HumanOutcome.Pass));
        s.Record(Result("A", AutoOutcome.Pass, HumanOutcome.Fail));   // the tool is happy, the listener is not

        foreach (var id in new[] { "A2", "A3" })
        {
            var c = HardwareCatalog.Find(id)!;
            Assert.Null(s.StaticBlock(c));
            Assert.Contains("was inconclusive", s.GatedBy(c)!.Reason);
            Assert.Equal(CheckStatus.Pending, s.StatusOf(id));
            Assert.False(s.Results.ContainsKey(id));
        }

        s.Clear("A");
        s.Record(Result("A", AutoOutcome.Pass, HumanOutcome.Pass));
        Assert.Null(s.GatedBy(HardwareCatalog.Find("A2")!));
        Assert.Null(s.GatedBy(HardwareCatalog.Find("A3")!));
        Assert.Equal("A2", s.Next()!.Id);
    }

    [Fact]
    public void AnUnsureCubeCheckLeavesTheVisionCheckPendingUntilItPasses()
    {
        var s = Session();
        s.Record(Result("LINK", AutoOutcome.Pass, HumanOutcome.Pass));
        s.Record(Result("B", AutoOutcome.Fail, HumanOutcome.Unsure));  // cube seen, never connected

        var k = HardwareCatalog.Find("K")!;
        Assert.Equal(CheckStatus.Pending, s.StatusOf("K"));
        Assert.False(s.Results.ContainsKey("K"));
        Assert.Contains("was unsure", s.GatedBy(k)!.Reason);

        // and everything behind K is waiting on K, not written off
        foreach (var id in new[] { "V", "M", "Q", "N", "Z" })
        {
            Assert.Equal(CheckStatus.Pending, s.StatusOf(id));
            Assert.False(s.Results.ContainsKey(id));
        }

        s.Clear("B");
        s.Record(Result("B", AutoOutcome.Pass, HumanOutcome.Pass));
        Assert.Null(s.GatedBy(k));
        Assert.Contains("K", s.Selected().Where(c => s.GatedBy(c) is null && !s.Results.ContainsKey(c.Id)).Select(c => c.Id));
    }

    /// <summary>
    /// The one kind of block that is real and is written down: this build cannot run the check at all, and
    /// nothing anyone does today changes that.
    /// </summary>
    [Fact]
    public void TheDetectorBoundaryIsARealBlockAndIsRecordedAsOne()
    {
        var s = Session();
        var y = HardwareCatalog.Find("Y")!;
        Assert.NotNull(s.StaticBlock(y));
        Assert.Contains("BLOCKED_EXTERNAL", s.StaticBlock(y)!.Reason);

        s.RecordBlocked("Y", s.StaticBlock(y)!.Reason);
        Assert.Equal(CheckStatus.Blocked, s.StatusOf("Y"));
        Assert.Equal(1, s.Tally().Blocked);
    }

    [Fact]
    public void TheDifferenceSurvivesBeingSavedAndReloaded()
    {
        var dir = TempDir();
        var file = Path.Combine(dir, "session.json");
        try
        {
            var s = Session();
            s.Record(Result("LINK", AutoOutcome.Pass, HumanOutcome.Pass));
            s.Record(Result("D", AutoOutcome.Pass, HumanOutcome.Unsure));
            s.RecordBlocked("Y", HardwareCatalog.Find("Y")!.BlockedReason!);
            s.Save(file);

            var back = HardwareSession.Load(file)!;
            Assert.Equal(CheckStatus.Unsure, back.StatusOf("D"));      // the observation is kept
            Assert.Equal(CheckStatus.Blocked, back.StatusOf("Y"));     // the real block is kept
            Assert.Equal(CheckStatus.Pending, back.StatusOf("MOV"));   // the gated one is still just waiting
            Assert.False(back.Results.ContainsKey("MOV"));
            Assert.Contains("was unsure", back.GatedBy(HardwareCatalog.Find("MOV")!)!.Reason);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    /// <summary>
    /// An older session, written when a dependency gate was mistaken for a permanent block, is repaired on
    /// the way in - and only in that one respect. Everything anybody actually observed is left alone.
    /// </summary>
    [Fact]
    public void LoadingAnOlderSessionClearsTheFalseBlocksAndNothingElse()
    {
        var dir = TempDir();
        var file = Path.Combine(dir, "session.json");
        try
        {
            var s = Session();
            // what the campaign really saw
            s.Record(new HardwareResult { Id = "LINK", Auto = AutoOutcome.Pass, Human = HumanOutcome.Pass });
            s.Record(new HardwareResult { Id = "A", Auto = AutoOutcome.Pass, Human = HumanOutcome.Fail, HumanNote = "the tune sounded wrong" });
            s.Record(new HardwareResult { Id = "E", Auto = AutoOutcome.Pass, Human = HumanOutcome.Fail, HumanNote = "colour image looked glitched" });
            s.Record(new HardwareResult { Id = "B", Auto = AutoOutcome.Fail, Human = HumanOutcome.Unsure, HumanNote = "cube seen but did not connect" });
            s.Record(new HardwareResult { Id = "D", Auto = AutoOutcome.Pass, Human = HumanOutcome.Unsure, HumanNote = "did not know what to do" });
            s.Record(new HardwareResult { Id = "J", InterruptedReason = "stopped part way (emergency stop or Ctrl+C)" });
            s.RecordBlocked("Y", HardwareCatalog.Find("Y")!.BlockedReason!);
            // and what it wrote down that was never observed
            s.RecordBlocked("MOV", "D (Lift position readout) has not run yet, and MOV depends on it");
            s.RecordBlocked("A2", "A (Cozmo sings) was inconclusive, and A2 depends on it");
            s.RecordBlocked("K", "B (Cube telemetry) has not run yet, and K depends on it");
            s.RecordBlocked("V", "K (Camera calibration and cube localisation) has not run yet, and V depends on it");
            s.Save(file);

            var back = HardwareSession.Load(file)!;

            Assert.Equal(new[] { "MOV", "A2", "K", "V" }.OrderBy(x => x), back.Migrated.OrderBy(x => x));
            foreach (var id in new[] { "MOV", "A2", "K", "V" })
            {
                Assert.False(back.Results.ContainsKey(id));
                Assert.Equal(CheckStatus.Pending, back.StatusOf(id));
            }

            // every real observation is exactly as it was
            Assert.Equal(CheckStatus.Passed, back.StatusOf("LINK"));
            Assert.Equal(CheckStatus.Partial, back.StatusOf("A"));
            Assert.Equal("the tune sounded wrong", back.Results["A"].HumanNote);
            Assert.Equal(CheckStatus.Partial, back.StatusOf("E"));
            Assert.Equal(CheckStatus.Unsure, back.StatusOf("B"));
            Assert.Equal("cube seen but did not connect", back.Results["B"].HumanNote);
            Assert.Equal(CheckStatus.Unsure, back.StatusOf("D"));
            Assert.Equal(CheckStatus.Interrupted, back.StatusOf("J"));
            Assert.Equal(CheckStatus.Blocked, back.StatusOf("Y"));     // the one real block survives

            // and the campaign can carry on: rerun what was unsure and the rest follows
            back.Clear("B");
            back.Record(Result("B", AutoOutcome.Pass, HumanOutcome.Pass));
            Assert.Null(back.GatedBy(HardwareCatalog.Find("K")!));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void ACampaignWithSomethingStillWaitingIsNotFinished()
    {
        var s = Session(Check("A"), Check("B", "A"));
        s.Record(Result("A", AutoOutcome.Pass, HumanOutcome.Unsure));
        Assert.Null(s.Next());                       // B is gated, so there is nothing to offer
        Assert.False(s.Complete);                    // but the campaign is not done either
        Assert.Single(s.Gated());
    }

    // ================================================================ when a check counts as cut short

    /// <summary>
    /// The regression for the bug the first real campaign found on its first check. LINK connected, the
    /// handshake and the identity arrived, telemetry ran at 30 Hz, the smoke test printed PASS - and then the
    /// tool closed its own connection, as every one of these tools does, and announced it: "disconnected:
    /// requested". The runner read that line, decided the link had been lost, and recorded a successful check
    /// as INTERRUPTED. Twice.
    ///
    /// The tool's own cleanup, after its work is done, is not a lost link. This is that exact sequence.
    /// </summary>
    [Fact]
    public async Task ACommandsOwnCleanupDisconnectDoesNotTurnAPassIntoAnInterruption()
    {
        var root = TempDir();
        try
        {
            var link = HardwareCatalog.Find("LINK")!;
            var o = new HardwareRunOptions { Ip = "172.31.1.1", EvidenceDirectory = root };

            var run = await HardwareRunner.Execute(link, o, CancellationToken.None, (_, _) =>
            {
                Console.WriteLine("connecting to 172.31.1.1:5551 (ConnectionRequest, reliable seq 1) ...");
                Console.WriteLine("connected in 82 ms (ConnectionResponse received)");
                Console.WriteLine("firmware: v2457 e2r=0x1a r2e=0x2b build=ok");
                Console.WriteLine("mfg: ESN 0045f00d   syncTimeAck=True   states so far=14");
                Console.WriteLine("telemetry: 615 RobotState at 30.1 Hz; 640 messages total");
                Console.WriteLine("transport: frames sent=210 resent=0 dupsDropped=0 pending=0 lastRTT=4.2ms");
                Console.WriteLine("SMOKE TEST: PASS");
                Console.WriteLine("  disconnected: requested");          // the tool's own cleanup, after the verdict
                Console.WriteLine("frame log written to: frames.log  (send this file plus the console output)");
                return Task.FromResult(0);
            });

            Assert.Equal(RunEnding.Completed, run.Ending);
            Assert.Null(run.InterruptedReason);
            Assert.Equal(AutoOutcome.Pass, link.Judge(run));            // the automated result is preserved

            // and recorded, it is waiting for the person's verdict rather than being written off as cut short
            var recorded = new HardwareResult
            {
                Id = link.Id, Auto = link.Judge(run), Human = HumanOutcome.NotAsked,
                InterruptedReason = run.InterruptedReason,
            };
            Assert.Equal(CheckStatus.Pending, recorded.Status);
            Assert.Equal(CheckStatus.Passed, (recorded with { Human = HumanOutcome.Pass }).Status);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    /// <summary>
    /// The other half, which must keep working: the robot goes away while the check is still doing its work.
    /// The tool never returns a result - it throws what the transport throws on a closed link - and that is
    /// an interruption, not a failure, because nothing was observed either way.
    /// </summary>
    [Fact]
    public async Task ALinkLostWhileTheCheckIsStillRunningIsStillAnInterruption()
    {
        var root = TempDir();
        try
        {
            var link = HardwareCatalog.Find("LINK")!;
            var o = new HardwareRunOptions { Ip = "172.31.1.1", EvidenceDirectory = root };

            var run = await HardwareRunner.Execute(link, o, CancellationToken.None, (_, _) =>
            {
                Console.WriteLine("connected in 80 ms (ConnectionResponse received)");
                Console.WriteLine("  t+ 2.0s states=60 rate=30.0Hz");
                return Task.FromException<int>(new InvalidOperationException("not connected"));
            });

            Assert.Equal(RunEnding.Faulted, run.Ending);
            Assert.Equal("the link to the robot was lost while the check was running", run.InterruptedReason);
            Assert.Equal(CheckStatus.Interrupted,
                         new HardwareResult { Id = link.Id, InterruptedReason = run.InterruptedReason }.Status);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task StoppingACheckPartWayIsAnInterruptionAndSaysSo()
    {
        var root = TempDir();
        try
        {
            var o = new HardwareRunOptions { Ip = "172.31.1.1", EvidenceDirectory = root };
            using var stop = new CancellationTokenSource();

            var run = await HardwareRunner.Execute(Check("X"), o, stop.Token, async (_, ct) =>
            {
                Console.WriteLine("half way through something");
                stop.Cancel();                                   // the person hits Ctrl+C
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
            });

            Assert.Equal(RunEnding.Cancelled, run.Ending);
            Assert.Contains("stopped part way", run.InterruptedReason!);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ACheckThatNeverReturnsIsCutShortByItsOwnAllowance()
    {
        var root = TempDir();
        try
        {
            var o = new HardwareRunOptions { Ip = "172.31.1.1", EvidenceDirectory = root };
            var check = Check("X") with { Timeout = TimeSpan.FromMilliseconds(200) };

            var run = await HardwareRunner.Execute(check, o, CancellationToken.None, async (_, ct) =>
            {
                Console.WriteLine("still going");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return 0;
            });

            Assert.Equal(RunEnding.TimedOut, run.Ending);
            Assert.Contains("allowance", run.InterruptedReason!);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    /// <summary>
    /// The rule itself, stated without a tool: what was printed does not decide this. The same transcript -
    /// one that ends with the tool announcing its own disconnect - is an interruption or not according to
    /// whether the tool was still running when it happened.
    /// </summary>
    [Fact]
    public void WhatWasPrintedDoesNotDecideWhetherACheckWasCutShort()
    {
        var check = Check("X");
        const string transcript = "SMOKE TEST: PASS\n  disconnected: requested\nnot connected\nconnection lost";

        HardwareToolRun Ending(RunEnding e, Exception? error = null) =>
            new(0, transcript, null, "console.log", error) { Ending = e };

        Assert.Null(HardwareRunner.Interruption(Ending(RunEnding.Completed), check));
        Assert.NotNull(HardwareRunner.Interruption(Ending(RunEnding.Cancelled), check));
        Assert.NotNull(HardwareRunner.Interruption(Ending(RunEnding.TimedOut), check));
        Assert.NotNull(HardwareRunner.Interruption(Ending(RunEnding.Faulted, new InvalidOperationException("not connected")), check));

        // a tool that threw something that is not the link going is an error for the judge, not an interruption
        Assert.Null(HardwareRunner.Interruption(Ending(RunEnding.Faulted, new FormatException("bad json")), check));

        // and the check whose subject is the link going judges it like any other result
        var expectsIt = Check("X") with { ExpectsDisconnect = true };
        Assert.Null(HardwareRunner.Interruption(Ending(RunEnding.Faulted, new InvalidOperationException("not connected")), expectsIt));
    }

    /// <summary>
    /// Every check in the campaign runs a tool that opens its own connection and closes it again, so every
    /// one of them could have suffered the same misreading. None of them can now.
    /// </summary>
    [Fact]
    public void NoCheckInTheCampaignIsCutShortByItsOwnToolsCleanup()
    {
        foreach (var c in HardwareCatalog.All)
        {
            var cleanup = new HardwareToolRun(0,
                "the check did its work\n  disconnected: requested\nframe log written to: frames.log",
                null, "console.log", null) { Ending = RunEnding.Completed };
            Assert.Null(HardwareRunner.Interruption(cleanup, c));
        }
    }

    // ================================================================ the automated judges

    [Fact]
    public void TheCubeCheckNeedsAConnectionAndTelemetryNotJustDiscovery()
    {
        var b = HardwareCatalog.Find("B")!;
        Assert.Equal(AutoOutcome.Fail, b.Judge(Output("2 cube(s) heard, 0 connected")));
        Assert.Equal(AutoOutcome.Fail, b.Judge(Output("1 cube(s) heard, 1 connected\n  cube 7")));   // connected, silent
        Assert.Equal(AutoOutcome.Fail, b.Judge(Output("1 cube(s) heard, 1 connected\n  tapped cube 7"))); // bypassed the production connect path
        Assert.Equal(AutoOutcome.Pass, b.Judge(Output("auto block pool enabled: True; SetPropSlot sent: 1 (0x12345678->slot0)\n1 cube(s) heard, 1 connected\n  tapped cube 7")));
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
    public void TheConnectionCheckReadsTheSmokeTestsOwnVerdict()
    {
        var con = HardwareCatalog.Find("LINK")!;
        Assert.Equal(AutoOutcome.Pass, con.Judge(Output("telemetry: 600 RobotState at 30.1 Hz\nSMOKE TEST: PASS")));
        Assert.Equal(AutoOutcome.Fail, con.Judge(Output("SMOKE TEST: FAIL")));
    }

    [Fact]
    public void TheCoreCheckReadsItsOwnVerdictLine()
    {
        foreach (var id in new[] { "CR1", "CR2", "CR3", "CR4", "CR7", "CR8" })
        {
            var c = HardwareCatalog.Find(id)!;
            Assert.Equal(AutoOutcome.Pass, c.Judge(Output("automated: PASS - it stopped on its own")));
            Assert.Equal(AutoOutcome.Fail, c.Judge(Output("automated: FAIL - the wheels were still turning")));
        }
    }

    [Fact]
    public void TheSustainedSingingCheckFailsOnAnUnderrun()
    {
        var a3 = HardwareCatalog.Find("A3")!;
        Assert.Equal(AutoOutcome.Fail, a3.Judge(new HardwareToolRun(3, "stream: rendered=100 consumed=90\nunderruns: 0\nAUTOMATED CHECKS FAILED (sing).", null, "x.log", null)));
        Assert.Equal(AutoOutcome.Pass, a3.Judge(Output("vibrato posted\nunderruns: 0\nAUTOMATED CHECKS PASSED (sing)")));
        Assert.Equal(AutoOutcome.Fail, a3.Judge(Output("stream: rendered=100 consumed=90\nunderruns: 7")));
    }

    [Fact]
    public void TheAnimationCheckRequiresTheAnimationToolsOwnSuccessfulVerdict()
    {
        var f = HardwareCatalog.Find("F")!;
        Assert.Equal(AutoOutcome.Fail, f.Judge(Output("keyframes fired: 27 of 27")));
        Assert.Equal(AutoOutcome.Fail, f.Judge(new HardwareToolRun(30, "AUTOMATED CHECKS PASSED (animation)", null, "x.log", null)));
        Assert.Equal(AutoOutcome.Pass, f.Judge(Output("AUTOMATED CHECKS PASSED (animation): every keyframe fired and the timeline ran to length.")));
    }

    [Fact]
    public void TheMotionCheckRequiresItsCompleteTerminalVerdict()
    {
        var mov = HardwareCatalog.Find("MOV")!;
        Assert.Equal(AutoOutcome.Fail, mov.Judge(Output("MotorActionAck acknowledged\nstop all: Success\nAUTOMATED CHECKS FAILED (motion)")));
        Assert.Equal(AutoOutcome.Pass, mov.Judge(Output("MOTION AUTOMATED CHECKS PASSED: every head, lift, wheel and stop action completed successfully.")));
    }

    [Fact]
    public void TheRollCheckRequiresBothActionSuccessAndAChangedUpAxis()
    {
        var p = HardwareCatalog.Find("P")!;
        Assert.Equal(AutoOutcome.Fail, p.Judge(Output("Roll success=no; action=Retry; up axis changed=yes")));
        Assert.Equal(AutoOutcome.Fail, p.Judge(Output("Roll success=no; action=Success; up axis changed=no")));
        Assert.Equal(AutoOutcome.Pass, p.Judge(Output("Roll success=yes; action=Success; up axis changed=yes")));
    }

    [Fact]
    public void TheWheelieCheckRequiresSuccessAndCliffStopRestoration()
    {
        var u = HardwareCatalog.Find("U")!;
        Assert.Equal(AutoOutcome.Fail, u.Judge(Output("objective achieved: PoppedWheelie")));
        Assert.Equal(AutoOutcome.Fail, u.Judge(Output("PopAWheelie success=yes; PoppedWheelie=True; cliff stop restored=no")));
        Assert.Equal(AutoOutcome.Pass, u.Judge(Output("EnableStopOnCliff(true)\nPopAWheelie success=yes; PoppedWheelie=True; cliff stop restored=yes")));
        Assert.Contains("retries once", u.Success);
    }

    [Fact]
    public void ManipulationJudgesRequireTheSameExplicitTerminalSuccessAsTheirAcceptanceRecords()
    {
        var cases = new[]
        {
            ("V", "MountCharger success=yes; action=Success; on charger: True", "MountCharger success=no; action=Retry; on charger: True"),
            ("W", "DriveOffCharger success=yes; on charger: False", "DriveOffCharger success=no; on charger: False"),
            ("Q", "DriveToObject success=yes; action=Success", "DriveToObject success=no; action=Retry"),
            ("N", "Pickup success=yes; action=Success; carrying=7", "Pickup success=no; action=Retry; carrying="),
            ("O", "PlaceObjectOnGround success=yes; action=Success; carrying=False", "PlaceObjectOnGround success=no; action=Retry; carrying=False"),
            ("P", "Roll success=yes; action=Success; up axis changed=yes", "Roll success=no; action=Success; up axis changed=no"),
            ("R", "StackBlocks success=yes; carrying=False", "StackBlocks success=no; carrying=False"),
            ("S", "DriveAndFlipBlock success=yes; action=Success", "DriveAndFlipBlock success=no; action=Retry"),
            ("T", "KnockOver success=yes; knockedOver=True", "KnockOver success=no; knockedOver=False"),
            ("U", "EnableStopOnCliff(true)\nPopAWheelie success=yes; PoppedWheelie=True; cliff stop restored=yes",
                  "objective achieved: PoppedWheelie\nPopAWheelie success=no; PoppedWheelie=True; cliff stop restored=no"),
        };
        foreach (var (id, pass, fail) in cases)
        {
            var check = HardwareCatalog.Find(id)!;
            Assert.Equal(AutoOutcome.Pass, check.Judge(Output(pass)));
            Assert.Equal(AutoOutcome.Fail, check.Judge(Output(fail)));
        }
    }

    [Fact]
    public void Core007RequiresProcessedFramesActualEdgePointsAndMapContent()
    {
        Assert.False(CoreChecks.Core007HasEvidence(1, 0, 4));
        Assert.False(CoreChecks.Core007HasEvidence(1, 2, 0));
        Assert.False(CoreChecks.Core007HasEvidence(0, 2, 4));
        Assert.True(CoreChecks.Core007HasEvidence(1, 1, 1));
    }

    [Fact]
    public void EveryRunnableCheckRejectsANonzeroToolExitBeforeItsJudge()
    {
        foreach (var check in HardwareCatalog.All.Where(c => c.Runnable))
        {
            var successLooking = new HardwareToolRun(7,
                "SMOKE TEST: PASS\nautomated: PASS\nAUTOMATED CHECKS PASSED\nSuccess\nyes\nTrue",
                null, "x.log", null);
            Assert.Equal(AutoOutcome.Fail, HardwareRunner.Evaluate(check, successLooking));
        }
    }

    [Fact]
    public void TheFreeplayCheckFailsOnTheErrorTheEngineLogsWhenItCannotChoose()
    {
        var z = HardwareCatalog.Find("Z")!;
        Assert.Equal(AutoOutcome.Fail, z.Judge(Output("robot.freeplay_goal_started Hiking: priority 16")));
        Assert.Equal(AutoOutcome.Pass, z.Judge(Output("FREEPLAY AUTOMATED CHECKS PASSED: activities selected, behaviours actually started, and no fatal activity error occurred.")));
        Assert.Equal(AutoOutcome.Fail, z.Judge(Output("robot.freeplay_goal_started Hiking\nActivityFreeplay.NoActivityAvailableError")));
    }

    [Fact]
    public void AToolThatThrewIsAnErrorNotAFailure()
    {
        var run = new HardwareToolRun(0, "", null, "x.log", new InvalidOperationException("boom"));
        Assert.NotNull(run.Error);
        Assert.Equal(CheckStatus.Partial, HardwareSession.StatusOf(Result("X", AutoOutcome.Error, HumanOutcome.Pass)));
    }
}
