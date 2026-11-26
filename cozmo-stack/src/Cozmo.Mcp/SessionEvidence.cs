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
    private readonly StreamWriter messages;
    private readonly M5HwAnalysis.WireDecoder wire = new();
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
        messages = Writer("messages.jsonl");
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
        WriteResult(DirectoryPath, metadata);
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
            double atMs = elapsed.Elapsed.TotalMilliseconds;
            // Raw frame first. If the worker dies between the two writes, its supervisor can recover
            // the complete message rows from the raw journal, including the last flushed packet.
            frames.WriteLine(JsonSerializer.Serialize(new
            {
                atMs, outbound = frame.Outbound,
                rawBase64 = Convert.ToBase64String(frame.Raw), error = frame.Error,
            }, Json));
            FrameCodec.TryDecode(frame.Raw, out var decoded, out _);
            foreach (var message in wire.ReadFrame(new(checked((int)frameCount), atMs, frame.Outbound, decoded)))
                WriteMessage(messages, message);
        }
    }

    public void Finish(string status, JsonElement? finalState = null, string? error = null, int? exitCode = null)
    {
        lock (gate)
        {
            if (disposed) return;
            metadata["status"] = status;
            metadata["error"] = error;
            if (exitCode.HasValue) metadata["exitCode"] = exitCode.Value;
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
            messages.Dispose();
            WriteResult(DirectoryPath, metadata);
        }
    }

    private static void WriteMessage(StreamWriter writer, M5Hw.WireMessage message) =>
        writer.WriteLine(new JsonObject
        {
            ["atMs"] = message.AtMs, ["outbound"] = message.Outbound,
            ["tag"] = $"0x{message.Payload[0]:X2}", ["payloadHex"] = Convert.ToHexString(message.Payload),
            ["frameLine"] = message.FrameLine, ["submessageOrdinal"] = message.SubmessageOrdinal,
            ["reliableSequence"] = message.ReliableSequence, ["connectionEpoch"] = message.ConnectionEpoch,
        }.ToJsonString(Json));

    internal static void WriteResult(string directory, JsonObject result)
    {
        // A hard exit must not leave an overwritten half-JSON result file.
        string temp = Path.Combine(directory, $"result-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, result.ToJsonString(new(Json) { WriteIndented = true }));
            File.Move(temp, Path.Combine(directory, "result.json"), overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    // Only the supervisor runs recovery, after the child has exited. Use the same wire decoder;
    // keep line numbers even if a hard failure left an incomplete last raw JSONL line.
    internal static (int Frames, int InvalidRows) RecoverMessages(string directory)
    {
        int lineNumber = 0, invalid = 0;
        string rawPath = Path.Combine(directory, "frames.jsonl");
        string temp = Path.Combine(directory, $"messages-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var writer = new StreamWriter(temp, false, new UTF8Encoding(false)))
            {
                var decoder = new M5HwAnalysis.WireDecoder();
                if (File.Exists(rawPath)) foreach (string line in File.ReadLines(rawPath))
                {
                    lineNumber++;
                    M5HwAnalysis.CapturedFrame? capture = null;
                    try
                    {
                        using var document = JsonDocument.Parse(line);
                        var row = document.RootElement;
                        FrameCodec.TryDecode(row.GetProperty("rawBase64").GetBytesFromBase64(), out var frame, out _);
                        capture = new(lineNumber, row.GetProperty("atMs").GetDouble(), row.GetProperty("outbound").GetBoolean(), frame);
                    }
                    catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
                    { invalid++; }
                    if (capture is not null) foreach (var message in decoder.ReadFrame(capture)) WriteMessage(writer, message);
                }
            }
            File.Move(temp, Path.Combine(directory, "messages.jsonl"), overwrite: true);
            return (lineNumber - invalid, invalid);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}