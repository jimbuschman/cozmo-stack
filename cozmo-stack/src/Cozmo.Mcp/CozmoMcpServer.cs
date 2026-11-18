using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Bridge;

namespace Cozmo.Mcp;

/// <summary>The MCP stdio protocol over the transport-neutral Cozmo bridge.</summary>
public sealed class CozmoMcpServer(CozmoBridge bridge, Action<string>? diagnostic = null,
    Action<string, JsonElement>? record = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] Versions = ["2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05"];
    private readonly SemaphoreSlim outputGate = new(1);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> requests = new();
    private bool initialized;
    private bool ready;

    /// <summary>Reads newline-delimited UTF-8 JSON-RPC. EOF cancels outstanding calls.</summary>
    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken token = default)
    {
        using var session = CancellationTokenSource.CreateLinkedTokenSource(token);
        var pending = new List<Task>();
        try
        {
            while (await input.ReadLineAsync(session.Token).ConfigureAwait(false) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                pending.RemoveAll(task => task.IsCompletedSuccessfully);
                pending.Add(ReceiveAsync(line, output, session.Token));
            }
        }
        catch (OperationCanceledException) when (session.IsCancellationRequested) { }
        finally
        {
            session.Cancel();
            foreach (var request in requests.Values)
            {
                try { request.Cancel(); }
                catch (ObjectDisposedException) { }
            }
            try { await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false); }
            catch (TimeoutException) { diagnostic?.Invoke("MCP shutdown timed out while retiring pending calls."); }
        }
    }

    private async Task ReceiveAsync(string line, TextWriter output, CancellationToken session)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(line); }
        catch (JsonException)
        {
            await Reply(output, null, null, -32700, "Invalid JSON.", CancellationToken.None).ConfigureAwait(false);
            return;
        }
        using (document)
        {
            var message = document.RootElement;
            record?.Invoke("mcp-input", message);
            if (message.ValueKind != JsonValueKind.Object)
            {
                await Reply(output, null, null, -32600, "A JSON-RPC message must be an object.", CancellationToken.None).ConfigureAwait(false);
                return;
            }
            bool hasId = message.TryGetProperty("id", out var id);
            bool validId = hasId && id.ValueKind is JsonValueKind.String or JsonValueKind.Number;
            if (!message.TryGetProperty("jsonrpc", out var rpc) || rpc.ValueKind != JsonValueKind.String || rpc.GetString() != "2.0" ||
                !message.TryGetProperty("method", out var method) || method.ValueKind != JsonValueKind.String ||
                hasId && !validId)
            {
                await Reply(output, validId ? id : null, null, -32600, "Invalid JSON-RPC request.", CancellationToken.None).ConfigureAwait(false);
                return;
            }
            string name = method.GetString()!;
            var parameters = message.TryGetProperty("params", out var supplied) ? supplied : default;
            if (!hasId)
            {
                if (name == "notifications/initialized" && initialized) ready = true;
                if (name == "notifications/cancelled" && parameters.ValueKind == JsonValueKind.Object &&
                    parameters.TryGetProperty("requestId", out var cancelledId) &&
                    requests.TryGetValue(cancelledId.GetRawText(), out var cancelled))
                {
                    try { cancelled.Cancel(); }
                    catch (ObjectDisposedException) { }
                }
                return;
            }

            string key = id.GetRawText();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(session);
            if (!requests.TryAdd(key, cancellation))
            {
                await Reply(output, id, null, -32600, "Request ID is already in use.", CancellationToken.None).ConfigureAwait(false);
                return;
            }
            try
            {
                object result;
                switch (name)
                {
                    case "initialize":
                        if (initialized) throw new ProtocolException(-32600, "The session is already initialized.");
                        if (parameters.ValueKind != JsonValueKind.Object ||
                            !parameters.TryGetProperty("protocolVersion", out var version) || version.ValueKind != JsonValueKind.String)
                            throw new ProtocolException(-32602, "initialize requires protocolVersion.");
                        initialized = true;
                        result = new
                        {
                            protocolVersion = Versions.Contains(version.GetString(), StringComparer.Ordinal) ? version.GetString() : Versions[0],
                            capabilities = new { tools = new { listChanged = false } },
                            serverInfo = new { name = "cozmo-mcp", version = "1.0.0" },
                            instructions = "Read get_state before moving. Object IDs are engine world IDs. Use list_animations for trigger names. " +
                                "Action results include during events. Use get_camera_image to see; read_events supports waitMs up to 30000. Track-face runs until completion or cancellation.",
                        };
                        break;
                    case "ping":
                        result = new { };
                        break;
                    case "tools/list" when ready:
                        if (parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("cursor", out _))
                            throw new ProtocolException(-32602, "This tool list has no further pages.");
                        result = new { tools = Tools() };
                        break;
                    case "tools/call" when ready:
                        result = await CallTool(parameters, cancellation.Token).ConfigureAwait(false);
                        break;
                    case "tools/list" or "tools/call":
                        throw new ProtocolException(-32600, "Complete MCP initialization before requesting tools.");
                    default:
                        throw new ProtocolException(-32601, $"Unknown method '{name}'.");
                }
                await Reply(output, id, result, null, null, CancellationToken.None).ConfigureAwait(false);
            }
            catch (ProtocolException error)
            {
                await Reply(output, id, null, error.Code, error.Message, CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                await Reply(output, id, null, -32800, "Request cancelled.", CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                diagnostic?.Invoke(error.ToString());
                await Reply(output, id, null, -32603, "MCP request failed. See the session evidence.", CancellationToken.None).ConfigureAwait(false);
            }
            finally { requests.TryRemove(key, out _); }
        }
    }

    private object[] Tools()
    {
        var catalogue = bridge.GetCommandCatalogue().EnumerateArray().Select(entry => (object)new
        {
            name = entry.GetProperty("name").GetString(),
            description = entry.GetProperty("description").GetString(),
            inputSchema = entry.GetProperty("argumentsSchema"),
        });
        return catalogue.Concat(
        [
            Tool("get_state", "Read connection, battery, carrying, charger, activity, faces and cubes.", """{"type":"object","properties":{},"additionalProperties":false}"""),
            Tool("get_camera_image", "Read the latest camera image with matching observation boxes and exposure-time head angle.",
                """{"type":"object","properties":{"annotate":{"type":"boolean","default":false}},"additionalProperties":false}"""),
            Tool("read_events", "Read buffered events since a cursor; waitMs waits for the next event, up to 30 seconds. A gap means older events were evicted.",
                """{"type":"object","properties":{"since":{"type":"integer","minimum":0,"default":0},"waitMs":{"type":"integer","minimum":0,"maximum":30000,"default":0}},"additionalProperties":false}"""),
            Tool("list_animations", "List loaded authored animation trigger names and clip names.", """{"type":"object","properties":{},"additionalProperties":false}"""),
        ]).ToArray();
    }

    private static object Tool(string name, string description, string schema) => new
    {
        name, description, inputSchema = JsonSerializer.Deserialize<JsonElement>(schema),
    };

    private async Task<object> CallTool(JsonElement parameters, CancellationToken token)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
            throw new ProtocolException(-32602, "tools/call requires a tool name.");
        string tool = name.GetString()!;
        JsonElement args = parameters.TryGetProperty("arguments", out var input) ? input : JsonSerializer.SerializeToElement(new { });
        if (args.ValueKind != JsonValueKind.Object) throw new ProtocolException(-32602, "Tool arguments must be an object.");
        try
        {
            JsonElement result;
            switch (tool)
            {
                case "get_camera_image":
                    if (args.EnumerateObject().Any(key => key.Name != "annotate"))
                        throw new ArgumentException("get_camera_image accepts only 'annotate'.");
                    bool annotate = false;
                    if (args.TryGetProperty("annotate", out var flag))
                    {
                        if (flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                            throw new ArgumentException("'annotate' must be a boolean.");
                        annotate = flag.GetBoolean();
                    }
                    var image = bridge.GetCameraImage(annotate);
                    return new
                    {
                        content = new object[] { new { type = "text", text = image.Metadata.GetRawText() },
                            new { type = "image", mimeType = "image/png", data = Convert.ToBase64String(image.Png) } },
                        structuredContent = image.Metadata,
                        isError = false,
                    };
                case "get_state":
                    NoArguments(args);
                    result = bridge.GetState();
                    break;
                case "list_animations":
                    NoArguments(args);
                    result = bridge.ListAnimations();
                    break;
                case "read_events":
                    var keys = args.EnumerateObject().ToArray();
                    if (keys.Any(key => key.Name is not ("since" or "waitMs")))
                        throw new ArgumentException("read_events accepts only 'since' and 'waitMs'.");
                    long since = 0;
                    if (args.TryGetProperty("since", out var cursor) &&
                        (cursor.ValueKind != JsonValueKind.Number || !cursor.TryGetInt64(out since) || since < 0))
                        throw new ArgumentException("'since' must be a nonnegative integer.");
                    int waitMs = 0;
                    if (args.TryGetProperty("waitMs", out var wait) &&
                        (wait.ValueKind != JsonValueKind.Number || !wait.TryGetInt32(out waitMs) || waitMs < 0 || waitMs > 30000))
                        throw new ArgumentException("'waitMs' must be an integer between 0 and 30000.");
                    result = await bridge.ReadEventsAsync(since, waitMs, token).ConfigureAwait(false);
                    break;
                default:
                    result = await bridge.ExecuteAsync(tool, args, token).ConfigureAwait(false);
                    if (result.TryGetProperty("error", out var error) && error.GetProperty("code").GetString() == "unknown-command")
                        throw new ProtocolException(-32602, $"Unknown tool '{tool}'.");
                    break;
            }
            bool failed = result.TryGetProperty("truncatedByRobot", out var truncated) && truncated.ValueKind == JsonValueKind.True ||
                result.TryGetProperty("error", out _) ||
                result.TryGetProperty("admitted", out var admitted) && admitted.ValueKind == JsonValueKind.False ||
                result.TryGetProperty("status", out var status) && status.GetString() is "no-report" or "owner-retired" ||
                result.TryGetProperty("engineResult", out var code) && code.ValueKind == JsonValueKind.Number && code.GetUInt32() != 0;
            return ToolResult(result, failed);
        }
        catch (ArgumentException error) { return ToolResult(JsonSerializer.SerializeToElement(new { error = error.Message }), true); }
        catch (InvalidOperationException error)
        {
            diagnostic?.Invoke(error.ToString());
            return ToolResult(JsonSerializer.SerializeToElement(new { error = error.Message }), true);
        }
    }

    private static void NoArguments(JsonElement arguments)
    {
        if (arguments.EnumerateObject().Any()) throw new ArgumentException("This tool takes no arguments.");
    }

    private static object ToolResult(JsonElement result, bool error) => new
    {
        content = new[] { new { type = "text", text = result.GetRawText() } },
        structuredContent = result,
        isError = error,
    };

    private async Task Reply(TextWriter output, JsonElement? id, object? result, int? code, string? message, CancellationToken token)
    {
        JsonObject reply = new() { ["jsonrpc"] = "2.0", ["id"] = id is { } key ? JsonNode.Parse(key.GetRawText()) : null };
        if (code is { } error) reply["error"] = new JsonObject { ["code"] = error, ["message"] = message };
        else reply["result"] = JsonSerializer.SerializeToNode(result, Json);
        record?.Invoke("mcp-output", JsonSerializer.SerializeToElement(reply, Json));
        await outputGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await output.WriteLineAsync(reply.ToJsonString(Json).AsMemory(), token).ConfigureAwait(false);
            await output.FlushAsync(token).ConfigureAwait(false);
        }
        finally { outputGate.Release(); }
    }

    private sealed class ProtocolException(int code, string message) : Exception(message)
    {
        internal int Code { get; } = code;
    }
}
