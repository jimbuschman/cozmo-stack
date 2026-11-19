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
                            capabilities = new { tools = new { listChanged = false }, resources = new { } },
                            serverInfo = new { name = "cozmo-mcp", version = "1.0.0" },
                            instructions = "Read the cozmo-guide resource at cozmo://guide. Check get_state readiness and get_camera_image before acting. " +
                                "Use world object IDs, inspect during/why and animation truncatedByRobot, and wait with read_events since/waitMs. Tracking can remain pending until cancelled.",
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
                    case "resources/list" when ready:
                        ResourceListParameters(parameters);
                        result = new { resources = new[] { new { uri = CozmoOperatingGuide.ResourceUri,
                            name = "cozmo-guide", title = "Operating Cozmo", mimeType = "text/markdown",
                            description = "Operating steps, state fields, native result meanings and charger approach evidence." } } };
                        break;
                    case "resources/templates/list" when ready:
                        ResourceListParameters(parameters);
                        result = new { resourceTemplates = Array.Empty<object>() };
                        break;
                    case "resources/read" when ready:
                        if (parameters.ValueKind != JsonValueKind.Object ||
                            !parameters.TryGetProperty("uri", out var resourceUri) || resourceUri.ValueKind != JsonValueKind.String)
                            throw new ProtocolException(-32602, "resources/read requires a URI string.");
                        if (!string.Equals(resourceUri.GetString(), CozmoOperatingGuide.ResourceUri, StringComparison.Ordinal))
                            throw new ProtocolException(-32002, "Resource not found.");
                        result = new { contents = new[] { new { uri = CozmoOperatingGuide.ResourceUri,
                            mimeType = "text/markdown", text = CozmoOperatingGuide.Markdown } } };
                        break;
                    case "tools/list" or "tools/call" or "resources/list" or "resources/read" or "resources/templates/list":
                        throw new ProtocolException(-32600, "Complete MCP initialization before requesting tools or resources.");
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

    // Static, read-only guide: no subscriptions, templates, arbitrary file paths or further pages.
    // MCP resources contract: https://modelcontextprotocol.io/specification/2025-11-25/server/resources
    private static void ResourceListParameters(JsonElement parameters)
    {
        if (parameters.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return;
        if (parameters.ValueKind != JsonValueKind.Object)
            throw new ProtocolException(-32602, "Resource list parameters must be an object.");
        if (parameters.TryGetProperty("cursor", out _))
            throw new ProtocolException(-32602, "This resource list has no further pages.");
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
            Tool("get_state", "Read timestampUtc, connection (connected/ready/link), batteryVolts, isCarrying/carriedObjectId, isOnCharger/isCharging, activity/highLevelActivity/behavior/reaction, reactionLocks, needs and knownFaces/knownCubes/knownChargers. Success: a latest-state snapshot; cozmo-guide defines fields and units. Failure: disconnected or not-ready flags and null/empty observations mean unavailable data; tool errors set isError. This read does not imply action success.", """{"type":"object","properties":{},"additionalProperties":false}"""),
            Tool("get_camera_image", "See the latest PNG in the frame's received colour or greyscale format; annotate draws exact-timestamp face/object boxes. Success: image, isColor, effective encoding, and exposure-time headAngleRad in structuredContent. Failure: isError when no frame exists or decode fails. Empty boxes/observationReportsAvailable=false mean no matching reports; headAngleRad can be null. This read does not enable a vision mode.",
                """{"type":"object","properties":{"annotate":{"type":"boolean","default":false}},"additionalProperties":false}"""),
            Tool("read_events", "Read a page after since and reuse its cursor. types and excludeTypes filter event names. limit counts matching events before compaction. Success: a page, with gap=true if older events were evicted. An empty page after waiting is a timeout. Failure: isError for invalid arguments or a cursor from another feed.",
                """{"type":"object","properties":{"since":{"type":"integer","minimum":0,"default":0},"waitMs":{"type":"integer","minimum":0,"maximum":30000,"default":0},"types":{"type":"array","items":{"type":"string","minLength":1}},"excludeTypes":{"type":"array","items":{"type":"string","minLength":1}},"limit":{"type":"integer","minimum":1,"default":100},"detail":{"type":"string","enum":["compact","full"],"default":"compact"}},"additionalProperties":false}"""),
            Tool("list_animations", "Read loaded, triggers and clips without playing an animation. Success: the loaded catalogue; use case-sensitive trigger names with animation. Failure: loaded=false/empty lists mean the library is unavailable; invalid arguments or a read error set isError.", """{"type":"object","properties":{},"additionalProperties":false}"""),
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
                    if (args.EnumerateObject().Any(key => key.Name is not ("since" or "waitMs" or "types" or "excludeTypes" or "limit" or "detail")))
                        throw new ArgumentException("Unknown read_events argument.");
                    long since = 0;
                    if (args.TryGetProperty("since", out var cursor) &&
                        (cursor.ValueKind != JsonValueKind.Number || !cursor.TryGetInt64(out since) || since < 0))
                        throw new ArgumentException("'since' must be a nonnegative integer.");
                    int waitMs = 0;
                    if (args.TryGetProperty("waitMs", out var wait) &&
                        (wait.ValueKind != JsonValueKind.Number || !wait.TryGetInt32(out waitMs) || waitMs < 0 || waitMs > 30000))
                        throw new ArgumentException("'waitMs' must be an integer between 0 and 30000.");
                    int limit = 100;
                    if (args.TryGetProperty("limit", out var size) &&
                        (size.ValueKind != JsonValueKind.Number || !size.TryGetInt32(out limit) || limit <= 0))
                        throw new ArgumentException("'limit' must be a positive integer.");
                    bool full = false;
                    if (args.TryGetProperty("detail", out var detail))
                    {
                        if (detail.ValueKind != JsonValueKind.String || detail.GetString() is not ("compact" or "full"))
                            throw new ArgumentException("'detail' must be 'compact' or 'full'.");
                        full = detail.GetString() == "full";
                    }
                    result = await bridge.ReadEventPageAsync(since, waitMs, limit,
                        EventTypes(args, "types"), EventTypes(args, "excludeTypes"), full, token).ConfigureAwait(false);
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

    private static string[]? EventTypes(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var list)) return null;
        if (list.ValueKind != JsonValueKind.Array || list.EnumerateArray().Any(item =>
                item.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(item.GetString())))
            throw new ArgumentException($"'{name}' must be an array of nonempty event names.");
        return list.EnumerateArray().Select(item => item.GetString()!).ToArray();
    }

    private static void NoArguments(JsonElement arguments)
    {
        if (arguments.EnumerateObject().Any()) throw new ArgumentException("This tool takes no arguments.");
    }

    private static object ToolResult(JsonElement result, bool error)
    {
        if (error && !result.TryGetProperty("why", out _))
        {
            var enriched = JsonNode.Parse(result.GetRawText())!.AsObject();
            enriched["why"] = new JsonObject
            {
                ["engineResultName"] = null,
                ["snapshot"] = new JsonObject(),
                ["checkAvailable"] = false,
                ["hint"] = result.TryGetProperty("error", out var failure) ? failure.ToString() : "No engine failure check was captured.",
            };
            result = JsonSerializer.SerializeToElement(enriched, Json);
        }
        return new
        {
            content = new[] { new { type = "text", text = result.GetRawText() } },
            structuredContent = result,
            isError = error,
        };
    }

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
