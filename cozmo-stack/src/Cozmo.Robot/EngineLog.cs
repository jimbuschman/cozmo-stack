using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Cozmo.Robot;

// fidelity: M3-048
public enum EngineLogLevel { Debug, Info, Event, Warning, Error }

/// <summary>One engine logger call, before any libDAS processing or display.</summary>
public sealed record EngineLogEntry(EngineLogLevel Level, string? Channel, string? Name, string Text,
    IReadOnlyDictionary<string, string> Data)
{
    /// <summary>The vsnprintf output bytes, including a possible incomplete UTF-8 sequence at the bound.</summary>
    public ReadOnlyMemory<byte> TextUtf8 { get; init; }
}

public sealed partial class CozmoEngine
{
    public event Action<EngineLogEntry>? LogEntry;
    /// <summary>Stack diagnostics, including isolation and MISSING; independent of DAS level filtering.</summary>
    public event Action<string>? StackLogLine;
    internal void EmitStackLog(string line)
    {
        FanOut(StackLogLine, line);
        DisplayLogLine(line);
    }
    // fidelity: M3-053, M3-054
    public event Action<DisplayedEngineLog>? DisplayedLog;
    public bool VerboseLogDisplay { get; set; }
    private readonly DasLogDisplay _logDisplay = new();
    internal DasLogDisplay LogDisplay => _logDisplay;
    private void DisplayEngineEntry(EngineLogEntry entry)
    {
        var line = _logDisplay.Render(entry, VerboseLogDisplay, EmitStackLog);
        if (line is null) return;
        if (DisplayedLog is { } displayed)
            foreach (Action<DisplayedEngineLog> subscriber in displayed.GetInvocationList())
                try { subscriber(line); } catch (Exception e) { ReportFault(e); }
        DisplayLogLine(line.Line);
    }
    // fidelity: M4-035
    public event Action<BlockPoolDataMessage>? BlockPoolData;
    internal void DeliverBlockPoolData(BlockPoolDataMessage m) => FanOut(BlockPoolData, m);
    internal Func<bool>? HasCameraCalibration { get; set; }

    // fidelity: M3-048
    internal void EmitEngineLog(EngineLogLevel level, string? channel, string? name, string text,
        IReadOnlyDictionary<string, string>? data = null)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(text);
        if (utf8.Length > 1023) utf8 = utf8[..1023];
        text = Encoding.UTF8.GetString(utf8);
        if (level is EngineLogLevel.Warning or EngineLogLevel.Error)
        {
            string prefix = "(tc" + unchecked((uint)Timer.TickCount).ToString("D4", CultureInfo.InvariantCulture) + ") : ";
            text = prefix + text;
            utf8 = Encoding.UTF8.GetBytes(prefix).Concat(utf8).ToArray();
        }
        var pairs = new ReadOnlyDictionary<string, string>(data is null ? new() : new Dictionary<string, string>(data));
        var entry = new EngineLogEntry(level, channel, name, text, pairs) { TextUtf8 = utf8 };
        if (LogEntry is { } entries)
            foreach (Action<EngineLogEntry> subscriber in entries.GetInvocationList())
                try { subscriber(entry); } catch (Exception e) { ReportFault(e); }
        DisplayEngineEntry(entry);
    }

    // Legacy delegate for existing source ports (including higher-layer ports outside this job).
    // Host diagnostics must use EmitStackLog at their call site; key spelling is never provenance.
    internal void Log(string line) => EmitSourceLog(line);

    // Explicit boundary for source-ported Action<string> component delegates.
    // Callers must already own a cited engine log call; this only decodes their legacy representation.
    internal void EmitSourceLog(string line, string? suppliedChannel = null)
    {
        int split = line.IndexOf(": ", StringComparison.Ordinal);
        string levelName = split < 0 ? "" : line[..split];
        EngineLogLevel level = levelName switch { "debug" => EngineLogLevel.Debug, "info" => EngineLogLevel.Info,
            "event" => EngineLogLevel.Event, "warning" => EngineLogLevel.Warning, "error" => EngineLogLevel.Error, _ => EngineLogLevel.Info };
        // Host diagnostics, including MISSING, remain visible and are not engine logger calls.
        if (line.Contains("MISSING:", StringComparison.Ordinal) || line.StartsWith("event handler threw:", StringComparison.Ordinal) || levelName is not ("debug" or "info" or "event" or "warning" or "error"))
        { EmitStackLog(line); return; }
        string rest = line[(split + 2)..];
        string? channel = level is EngineLogLevel.Debug or EngineLogLevel.Info ? suppliedChannel : null;
        if (rest.StartsWith('[') && rest.IndexOf("] ", StringComparison.Ordinal) is var end && end > 0)
        { channel = rest[1..end]; rest = rest[(end + 2)..]; }
        int keyEnd = rest.IndexOf(": ", StringComparison.Ordinal);
        string key = keyEnd < 0 ? rest : rest[..keyEnd];
        string text = keyEnd < 0 ? "" : rest[(keyEnd + 2)..];
        EmitEngineLog(level, channel, key, text);
    }
}
