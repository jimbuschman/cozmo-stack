using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;

namespace Cozmo.Mcp;

/// <summary>Streaming session evidence in the hardware harness's JSONL format.</summary>
public sealed class SessionEvidence : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };
    private readonly object gate = new();
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly StreamWriter frames;
    private readonly StreamWriter events;
    private readonly StreamWriter stack;
    private readonly DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
    private readonly JsonObject metadata;
    private bool disposed;
    private long frameCount;
    public string DirectoryPath { get; }

    public SessionEvidence(string directory, string[] arguments)
    {
        DirectoryPath = Path.GetFullPath(directory);
        Directory.CreateDirectory(DirectoryPath);
        frames = Writer("frames.jsonl");
        events = Writer("events.jsonl");
        stack = Writer("stack-log.txt");
        string? assembly = typeof(SessionEvidence).Assembly.Location;
        string? version = typeof(SessionEvidence).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        metadata = new()
        {
            ["testId"] = "LLM-SESSION",
            ["commit"] = version?.Split('+').LastOrDefault(),
            ["arguments"] = JsonSerializer.SerializeToNode(arguments),
            ["startedUtc"] = startedUtc,
            ["assemblySha256"] = string.IsNullOrEmpty(assembly) ? null : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),
            ["status"] = "running",
        };
        File.WriteAllText(Path.Combine(DirectoryPath, "result.json"), metadata.ToJsonString(Json));
    }

    private StreamWriter Writer(string name) =>
        new(new FileStream(Path.Combine(DirectoryPath, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(false)) { AutoFlush = true };

    public void Attach(CozmoEngine engine)
    {
        EngineEvidence.Subscribe(engine, Log, (kind, payload) => Record(kind, JsonSerializer.SerializeToElement(payload, Json)));
        engine.EngineToGameMessage += (tag, body) => Record("engine-to-game", JsonSerializer.SerializeToElement(new
        {
            tag = $"0x{tag:X2}", bodyHex = Convert.ToHexString(body.Span),
        }, Json));
        engine.ConnectionResponse += report => Record("connection-response", JsonSerializer.SerializeToElement(report, Json));
    }

    public void Record(string kind, JsonElement payload)
    {
        lock (gate)
        {
            if (disposed) return;
            var row = payload.ValueKind == JsonValueKind.Object
                ? JsonNode.Parse(payload.GetRawText())!.AsObject()
                : new JsonObject { ["payload"] = JsonNode.Parse(payload.GetRawText()) };
            row["atMs"] = elapsed.Elapsed.TotalMilliseconds;
            row["timestampUtc"] = DateTimeOffset.UtcNow;
            row["kind"] = kind;
            events.WriteLine(row.ToJsonString(Json));
        }
    }

    public void Log(string line)
    {
        lock (gate)
        {
            if (disposed) return;
            stack.WriteLine(line);
        }
    }

    public void Frame(FrameEvent frame)
    {
        lock (gate)
        {
            if (disposed) return;
            frameCount++;
            frames.WriteLine(JsonSerializer.Serialize(new
            {
                atMs = elapsed.Elapsed.TotalMilliseconds, outbound = frame.Outbound,
                rawBase64 = Convert.ToBase64String(frame.Raw), error = frame.Error,
            }, Json));
        }
    }

    public void Finish(string status, JsonElement? finalState = null, string? error = null)
    {
        lock (gate)
        {
            if (disposed) return;
            metadata["status"] = status;
            metadata["error"] = error;
            if (finalState is { } state) metadata["finalState"] = JsonNode.Parse(state.GetRawText());
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            frames.Dispose();
            events.Dispose();
            stack.Dispose();
            if (metadata["status"]?.GetValue<string>() == "running") metadata["status"] = "incomplete";
            metadata["endedUtc"] = DateTimeOffset.UtcNow;
            metadata["durationMs"] = elapsed.Elapsed.TotalMilliseconds;
            metadata["frames"] = frameCount;
            using var messages = Writer("messages.jsonl");
            foreach (var message in M5HwAnalysis.ReadWire(ReadFrames()))
                messages.WriteLine(new JsonObject
                {
                    ["atMs"] = message.AtMs, ["outbound"] = message.Outbound,
                    ["tag"] = $"0x{message.Payload[0]:X2}", ["payloadHex"] = Convert.ToHexString(message.Payload),
                    ["frameLine"] = message.FrameLine, ["submessageOrdinal"] = message.SubmessageOrdinal,
                    ["reliableSequence"] = message.ReliableSequence, ["connectionEpoch"] = message.ConnectionEpoch,
                }.ToJsonString(Json));
            File.WriteAllText(Path.Combine(DirectoryPath, "result.json"), metadata.ToJsonString(new(Json) { WriteIndented = true }));
        }
    }

    private IEnumerable<M5HwAnalysis.CapturedFrame> ReadFrames()
    {
        int lineNumber = 0;
        foreach (string line in File.ReadLines(Path.Combine(DirectoryPath, "frames.jsonl")))
        {
            lineNumber++;
            using var document = JsonDocument.Parse(line);
            var row = document.RootElement;
            FrameCodec.TryDecode(row.GetProperty("rawBase64").GetBytesFromBase64(), out var frame, out _);
            yield return new(lineNumber, row.GetProperty("atMs").GetDouble(), row.GetProperty("outbound").GetBoolean(), frame);
        }
    }
}
