using System.Collections.ObjectModel;
using System.Globalization;

namespace Cozmo.Robot;

// fidelity: M3-048
public enum EngineLogLevel { Debug, Info, Event, Warning, Error }

/// <summary>One engine logger call, before any libDAS processing or display.</summary>
public sealed record EngineLogEntry(EngineLogLevel Level, string? Channel, string? Name, string Text,
    IReadOnlyDictionary<string, string> Data);

public sealed partial class CozmoEngine
{
    public event Action<EngineLogEntry>? LogEntry;
    // fidelity: M4-035
    public event Action<BlockPoolDataMessage>? BlockPoolData;
    internal void DeliverBlockPoolData(BlockPoolDataMessage m) => FanOut(BlockPoolData, m);
    internal Func<bool>? HasCameraCalibration { get; set; }

    // fidelity: M3-048
    internal void EmitEngineLog(EngineLogLevel level, string? channel, string? name, string text,
        IReadOnlyDictionary<string, string>? data = null, string? legacyLine = null)
    {
        if (text.Length > 1023) text = text[..1023];
        if (level is EngineLogLevel.Warning or EngineLogLevel.Error)
            text = "(tc" + unchecked((uint)Timer.TickCount).ToString("D4", CultureInfo.InvariantCulture) + ") : " + text;
        var pairs = new ReadOnlyDictionary<string, string>(data is null ? new() : new Dictionary<string, string>(data));
        var entry = new EngineLogEntry(level, channel, name, text, pairs);
        if (LogEntry is { } entries)
            foreach (Action<EngineLogEntry> subscriber in entries.GetInvocationList())
                try { subscriber(entry); } catch (Exception e) { ReportFault(e); }
        DisplayLogLine(legacyLine ?? $"{level.ToString().ToLowerInvariant()}: {(channel is null ? "" : "[" + channel + "] ")}{name}: {text}");
    }

    // The existing Action<string> component boundary funnels each call into one structured entry.
    internal void Log(string line) => LogWithChannel(line, null);

    internal void LogWithChannel(string line, string? suppliedChannel)
    {
        int split = line.IndexOf(": ", StringComparison.Ordinal);
        string levelName = split < 0 ? "info" : line[..split];
        EngineLogLevel level = levelName switch { "debug" => EngineLogLevel.Debug, "info" => EngineLogLevel.Info,
            "event" => EngineLogLevel.Event, "warning" => EngineLogLevel.Warning, "error" => EngineLogLevel.Error, _ => EngineLogLevel.Info };
        // Host diagnostics, including MISSING, remain visible and are not engine logger calls.
        if (line.Contains("MISSING:", StringComparison.Ordinal) || levelName is not ("debug" or "info" or "event" or "warning" or "error"))
        { DisplayLogLine(line); return; }
        string rest = line[(split + 2)..];
        string? channel = level is EngineLogLevel.Debug or EngineLogLevel.Info ? suppliedChannel : null;
        if (rest.StartsWith('[') && rest.IndexOf("] ", StringComparison.Ordinal) is var end && end > 0)
        { channel = rest[1..end]; rest = rest[(end + 2)..]; }
        int keyEnd = rest.IndexOf(": ", StringComparison.Ordinal);
        string key = keyEnd < 0 ? rest : rest[..keyEnd];
        string text = keyEnd < 0 ? "" : rest[(keyEnd + 2)..];
        EmitEngineLog(level, channel, key, text, legacyLine: line);
    }
}
