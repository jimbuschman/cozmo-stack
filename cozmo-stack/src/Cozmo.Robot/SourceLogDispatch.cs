namespace Cozmo.Robot;

/// <summary>Typed logger seam for source ports; text sinks remain available for standalone component diagnostics.</summary>
public delegate void SourceLogSink(EngineLogLevel level, string? channel, string key, string text);

internal static class SourceLogDispatch
{
    // Conditional invocation keeps message construction behind the original observer gate.
    // The fallback renders supplied fields for standalone observers; no text is parsed into an entry.
    internal static SourceLogSink? GetSink(SourceLogSink? sourceLog, Action<string>? log) =>
        sourceLog ?? (log is null ? null : (level, channel, key, text) => Write(null, log, level, channel, key, text));

    internal static void Write(SourceLogSink? sourceLog, Action<string>? log,
        EngineLogLevel level, string? channel, string key, string text)
    {
        if (sourceLog is not null) sourceLog(level, channel, key, text);
        else log?.Invoke(level.ToString().ToLowerInvariant() + ": " +
            (channel is null ? "" : "[" + channel + "] ") + key +
            (text.Length == 0 ? "" : ": " + text));
    }
}

/// <summary>Host diagnostics and approved policy skips: no DAS fields, MISSING prefix or deduplication.</summary>
internal static class StackDiagnostics
{
    internal static event Action<string>? Reported;
    internal static void Write(string text) => LogCallbackDeferral.Run(() => Reported?.Invoke(text));
}
