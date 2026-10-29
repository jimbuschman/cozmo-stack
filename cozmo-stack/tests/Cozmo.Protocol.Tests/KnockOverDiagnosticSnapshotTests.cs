using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>Test-only failure formatting; an optional observation cannot discard mandatory diagnostics.</summary>
internal static class KnockOverDiagnosticSnapshot
{
    internal static string Format(Func<string[]> trace, Func<string[]> journal,
        Func<string> optionalTask, Func<string> optionalState)
    {
        string Capture(string label, Func<string> read)
        {
            try { return read(); }
            catch (Exception error) { return label + " failed: " + error; }
        }

        // Capture and format each mandatory producer independently before optional observations.
        string traceText = Capture("Trace snapshot", () => string.Join(Environment.NewLine, trace()));
        string journalText = Capture("Journal snapshot", () => string.Join(Environment.NewLine, journal()));
        string taskText = Capture("Optional task observation", optionalTask);
        string stateText = Capture("Optional state observation", optionalState);
        return "TRACE" + Environment.NewLine + traceText
            + Environment.NewLine + "JOURNAL" + Environment.NewLine + journalText
            + Environment.NewLine + taskText + Environment.NewLine + stateText;
    }
}

public sealed class KnockOverDiagnosticSnapshotTests
{
    // reach: unit (failure reporting only; deliberately failed optional formatter, no action/result injection).
    [Fact]
    public void OptionalStateFailureRetainsTheFullTraceAndJournal()
    {
        string[] trace = { "trace-start", new string('T', 18000), "trace-end" };
        string[] journal = { "journal-start", new string('J', 18000), "journal-end" };
        string snapshot = KnockOverDiagnosticSnapshot.Format(() => trace, () => journal,
            () => "first observation unavailable",
            () => throw new InvalidOperationException("deliberate optional formatter failure"));

        Assert.Contains(string.Join(Environment.NewLine, trace), snapshot);
        Assert.Contains(string.Join(Environment.NewLine, journal), snapshot);
        Assert.Contains("first observation unavailable", snapshot);
        Assert.Contains("Optional state observation failed:", snapshot);
        Assert.Contains("deliberate optional formatter failure", snapshot);
    }
}
