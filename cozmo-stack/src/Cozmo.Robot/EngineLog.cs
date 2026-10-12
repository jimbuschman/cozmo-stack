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
    public string? SourceFile { get; init; }
    public string? SourceFunction { get; init; }
    public int SourceLine { get; init; }
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
        var line = _logDisplay.Render(entry, VerboseLogDisplay, DisplayDiagnostic);
        if (line is null) return;
        DisplayDiagnostic(line);
    }
    private void DisplayDiagnostic(DisplayedEngineLog line)
    {
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
        => EmitEngineLogBytes(level, channel, name, Encoding.UTF8.GetBytes(text), data);

    private void EmitEngineLogBytes(EngineLogLevel level, string? channel, string? name, byte[] utf8,
        IReadOnlyDictionary<string, string>? data = null)
    {
        if (utf8.Length > 1023) utf8 = utf8[..1023];
        string text = Encoding.UTF8.GetString(utf8);
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

    // M5-008: MultiLoggerProvider::Flush -> DasLoggerProvider::Flush -> DASForceFlushNow.
    internal Action? LoggerProviderFlush;
    internal Func<byte, bool, IDisposable?>? AnimationSubscriptionFactory;
    internal void FlushSourceLogger()
    {
        if (LoggerProviderFlush is { } flush) flush();
        else EmitStackLog("MISSING: M5-008 -> M3-051 DASForceFlushNow body (libDAS 0x7168); logger provider flush at 0x0080DA24");
    }

    // Text delegates carry stack diagnostics only; spelling and level prefixes are not provenance.
    internal void Log(string line) => EmitStackLog(line);

    // Source ports provide the logger fields explicitly at their cited call sites.
    internal void EmitSourceLog(EngineLogLevel level, string? channel, string key, string text,
        IReadOnlyDictionary<string, string>? data = null) => EmitEngineLog(level, channel, key, text, data);
    // fidelity: M4-011, M3-048 — %s from a raw C string, using the same logger byte bound/fan-out.
    internal void EmitSourceLog(EngineLogLevel level, string? channel, string key, ReadOnlyMemory<byte> utf8,
        IReadOnlyDictionary<string, string>? data = null) => EmitEngineLogBytes(level, channel, key, utf8.ToArray(), data);

}
