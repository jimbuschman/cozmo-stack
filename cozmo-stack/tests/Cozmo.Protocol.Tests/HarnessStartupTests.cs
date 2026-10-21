using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

// M2-021: StartupManager.cs:322-341 / ConnectionFlowController.cs:670-673, delivered-ready host policy.
// M5-010: 0x0057A0D4..0x0057A282 binds neutral/reset once per Robot. Later explicit clips can still render.
[Collection("M5 process statics")]
public class HarnessStartupTests
{
    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "re-analysis", "fidelity_manifest.json"))) return dir.FullName;
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static HarnessStartup.Gate ReadyGate() => new(HarnessStartup.Outcome.Ready, 0, null, 60000);

    // reach: production startup, shipped loaders, delivered readiness and Robot construction. The callback
    // constructs the real Robot locally rather than opening a robot connection; no robot/network peer is contacted.
    [Fact]
    public async Task DelayedProductionStartupCannotConstructRobotUntilNeutralResourcesAreDelivered()
    {
        using var release = new ManualResetEventSlim();
        using var polled = new ManualResetEventSlim();
        var logs = new ConcurrentQueue<JsonObject>();
        int calls = 0;
        using var robot = CozmoRobot.Create(engineOptions: new CozmoEngineOptions
        {
            ResourcesPath = Path.Combine(RepoRoot(), "re-analysis", "obb", "assets", "cozmo_resources"),
            BlockPoolPath = "", LabLocaleLanguage = "en", LabLocaleCountry = "US",
            BeforeStart = e =>
            {
                EngineEvidence.Subscribe(e, _ => { }, (kind, data) => { if (kind == "engine-log") logs.Enqueue(data); });
                e.BeforeFirstProductionTickForTest = () => release.Wait(TimeSpan.FromSeconds(30));
            },
        });
        Task<HarnessStartup.Gate>? waiting = null;
        try
        {
            waiting = Task.Run(() => HarnessStartup.ConnectAfterReadiness(
                () => { polled.Set(); return robot.Engine.IsReadyDelivered; },
                () => robot.Engine.StartupFailed, () => robot.Engine.StartupFailureReason,
                () => { Assert.True(robot.Engine.IsReadyDelivered); Interlocked.Increment(ref calls); robot.Engine.Robots.AddRobot(1, false); }));
            Assert.True(polled.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(0, Volatile.Read(ref calls));
            Assert.Null(robot.Engine.Robot);
            Assert.False(robot.Engine.IsReadyDelivered);
            release.Set();
            var gate = await waiting.WaitAsync(TimeSpan.FromSeconds(60));
            Assert.True(gate.Ready, gate.Describe());
            Assert.Equal(1, calls);
            Assert.True(HarnessStartup.HasCachedNeutralFace(robot));
            var report = HarnessStartup.Analyse(logs, gate, HarnessStartup.HasCachedNeutralFace(robot));
            Assert.False(report.BadNeutral);
            HarnessStartup.RequireValid(report);
        }
        finally
        {
            release.Set();
            if (waiting is not null) await waiting.WaitAsync(TimeSpan.FromSeconds(60));
        }
    }

    // reach: production host with absent resources; only the connection callback is substituted.
    [Fact]
    public void ARealHostWhoseStartupFailsNeverConnects()
    {
        using var robot = CozmoRobot.Create();
        int connects = 0;
        var gate = HarnessStartup.ConnectAfterReadiness(robot, () => connects++, budgetMs: 30000);
        Assert.Equal(HarnessStartup.Outcome.Failed, gate.Outcome);
        Assert.Contains("ResourcesPath", gate.FailureReason);
        Assert.Equal(0, connects);
        Assert.False(HarnessStartup.Analyse(Array.Empty<JsonObject>(), gate).PositiveVerdictAllowed);
    }

    // reach: unit ordering boundaries; failure/cancellation wins even when readiness is concurrently true.
    [Theory]
    [InlineData(true, false, HarnessStartup.Outcome.Cancelled)]
    [InlineData(false, true, HarnessStartup.Outcome.Failed)]
    [InlineData(true, true, HarnessStartup.Outcome.Cancelled)]
    public void CancellationOrFailureWithReadyNeverConnects(bool cancelled, bool failed, HarnessStartup.Outcome expected)
    {
        int calls = 0;
        var gate = HarnessStartup.ConnectAfterReadiness(() => true, () => failed, () => "failed", () => calls++, () => cancelled);
        Assert.Equal(expected, gate.Outcome);
        Assert.Equal(0, calls);
        Assert.False(HarnessStartup.Analyse(Array.Empty<JsonObject>(), gate).PositiveVerdictAllowed);
    }

    [Fact]
    public void CancellationPublishedDuringReadinessReadNeverConnects()
    {
        bool cancelled = false; int calls = 0;
        var gate = HarnessStartup.ConnectAfterReadiness(() => { cancelled = true; return true; }, () => false, () => null,
            () => calls++, () => cancelled);
        Assert.Equal(HarnessStartup.Outcome.Cancelled, gate.Outcome);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void BudgetWithoutReadinessOrAnUnreachedGateCannotPermitPositiveVerdicts()
    {
        var gate = HarnessStartup.ConnectAfterReadiness(() => false, () => false, () => null,
            () => throw new InvalidOperationException("must not connect"), budgetMs: 0);
        Assert.Equal(HarnessStartup.Outcome.TimedOut, gate.Outcome);
        Assert.False(HarnessStartup.Analyse(Array.Empty<JsonObject>(), gate).PositiveVerdictAllowed);
        Assert.False(HarnessStartup.Analyse(Array.Empty<JsonObject>(), null).PositiveVerdictAllowed);
    }

    // reach: real BeforeStart subscription and early Robot construction before Create can return. A forced host
    // setup interruption proves the captured native-key diagnostic survives the failed Create path.
    [Fact]
    public void BeforeStartCapturesBadNeutralEvenWhenCreateDoesNotReturn()
    {
        var rows = new List<JsonObject>();
        Assert.Throws<IOException>(() => CozmoRobot.Create(engineOptions: new CozmoEngineOptions
        {
            BeforeStart = e =>
            {
                EngineEvidence.Subscribe(e, _ => { }, (kind, data) => { if (kind == "engine-log") rows.Add(data); });
                e.Robots.AddRobot(1, false);
                throw new IOException("injected host setup interruption");
            },
        }));
        var report = HarnessStartup.Analyse(rows, null, interrupted: "injected host setup interruption");
        Assert.True(report.BadNeutral);
        Assert.False(report.PositiveVerdictAllowed);
        Assert.Contains(report.Errors, e => e["name"]!.GetValue<string>() == HarnessStartup.BadNeutralAnimGroup);
        Assert.All(report.Errors, e => Assert.Contains(e["text"]!.GetValue<string>(), report.Describe()));
        Assert.Equal(report.Errors.Length, report.ToJson()["errors"]!.AsArray().Count);
    }

    // reach: coordinator guard used by all three harnesses; setup invalidity prevents the next action.
    // These are host coverage decisions, not native error semantics.
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void BadOrMissingNeutralStopsBeforeNextCheck(bool badNeutral, bool hasNeutral)
    {
        var rows = badNeutral ? new[] { new JsonObject { ["level"] = "error", ["name"] = HarnessStartup.BadNeutralAnimGroup, ["text"] = "neutral group missing" } } : Array.Empty<JsonObject>();
        var report = HarnessStartup.Analyse(rows, ReadyGate(), hasNeutral);
        bool nextCheckReached = false;
        Assert.Throws<InvalidOperationException>(() => { HarnessStartup.RequireValid(report); nextCheckReached = true; });
        Assert.False(nextCheckReached);
    }

    [Fact]
    public void OtherErrorLogsAreVisibleWithTextButDoNotInventAStartupFailure()
    {
        var rows = new[]
        {
            new JsonObject { ["level"] = "error", ["name"] = "Some.Error", ["text"] = "actual failure detail" },
            new JsonObject { ["level"] = "warning", ["name"] = "Some.Warning", ["text"] = "warning detail" },
        };
        var report = HarnessStartup.Analyse(rows, ReadyGate());
        Assert.Single(report.Errors);
        Assert.Contains("actual failure detail", report.Describe());
        Assert.True(report.PositiveVerdictAllowed);
    }

    // Secondary wiring checks; the behavioral join above exercises actual host startup and Robot construction.
    [Theory]
    [InlineData("M5Hw.cs")]
    [InlineData("M3M4Hw.cs")]
    [InlineData("ControlCheck.cs")]
    public void HarnessesCaptureBeforeStartAndStopBeforeChecks(string file)
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "cozmo-stack", "src", "Cozmo.Conformance", file));
        Assert.Contains("BeforeStart =", source);
        Assert.Contains("HarnessStartup.ConnectAfterReadiness(", source);
        Assert.Contains("HarnessStartup.RequireValid(", source);
        Assert.DoesNotContain("HarnessStartup.Demote", source);
        Assert.Contains("HarnessStartup.cs", source);
    }
}
