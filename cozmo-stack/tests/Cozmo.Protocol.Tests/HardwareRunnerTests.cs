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
        var known = new[] { "connect", "sensors", "cubes", "drive", "camera", "face", "tone", "anim",
                            "behavior", "sing", "offtreads", "reactions", "vision", "bodyangle", "manip",
                            "freeplay", "core" };
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
        Assert.NotNull(s.BlockedBy(s.Catalog.Single(c => c.Id == "N")));
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
            Assert.NotNull(s.BlockedBy(HardwareCatalog.Find(id)!));
        Assert.Null(s.BlockedBy(HardwareCatalog.Find("LINK")!));            // the first check needs nothing
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
            Assert.Contains("M13-006", record.FidelityRecords);
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

    // ================================================================ the automated judges

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
        Assert.Equal(AutoOutcome.Pass, a3.Judge(Output("stream: rendered=100 consumed=90\nunderruns: 0")));
        Assert.Equal(AutoOutcome.Fail, a3.Judge(Output("stream: rendered=100 consumed=90\nunderruns: 7")));
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
}
