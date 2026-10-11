using System.Text.Json.Nodes;
using Cozmo.Robot;

namespace Cozmo.Conformance;

/// <summary>Bundle capture uses the pre-display streams, so DAS filtering cannot remove acceptance evidence.</summary>
public static class EngineEvidence
{
    public static void Subscribe(CozmoEngine engine, Action<string> line, Action<string, JsonObject> record)
    {
        engine.LogEntry += entry =>
        {
            string level = entry.Level == EngineLogLevel.Warning ? "warning" : entry.Level.ToString().ToLowerInvariant();
            string rendered = level + ": " + (entry.Channel is null ? "" : "[" + entry.Channel + "] ") + entry.Name + ": " + entry.Text;
            line(rendered);
            var data = new JsonObject();
            foreach (var pair in entry.Data) data[pair.Key] = pair.Value;
            record("engine-log", new JsonObject { ["level"] = level, ["channel"] = entry.Channel, ["name"] = entry.Name,
                ["text"] = entry.Text, ["data"] = data, ["line"] = rendered,
                ["utf8Base64"] = Convert.ToBase64String(entry.TextUtf8.Span) });
        };
        engine.StackLogLine += text => { line(text); record("stack-log", new JsonObject { ["line"] = text }); };
    }
}
