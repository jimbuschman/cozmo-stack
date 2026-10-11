using System.Text.RegularExpressions;
using Cozmo.Robot;
namespace Cozmo.Protocol.Tests;

internal static class EngineLogAssertions
{
    // Existing source-row assertions now inspect structured entries, independently of display filtering.
    // Only the warning/error tick prefix is removed here; dedicated logger/display tests assert that prefix.
    internal static void CaptureSourceLogs(this CozmoEngine engine, Action<string> capture)
    {
        engine.LogEntry += entry =>
        {
            string text = entry.Text;
            if (entry.Level is EngineLogLevel.Warning or EngineLogLevel.Error)
                text = Regex.Replace(text, @"^\(tc[0-9]+\) : ", "");
            string level = entry.Level == EngineLogLevel.Warning ? "warning" : entry.Level.ToString().ToLowerInvariant();
            string channel = entry.Channel is null ? "" : "[" + entry.Channel + "] ";
            capture(level + ": " + channel + entry.Name + (text.Length == 0 ? "" : ": " + text));
        };
        // MISSING and other host diagnostics are separate from engine log calls.
        engine.LogLine += line => { if (line.Contains("MISSING:", StringComparison.Ordinal) || !line.Contains(" : ", StringComparison.Ordinal)) capture(line); };
    }
}
