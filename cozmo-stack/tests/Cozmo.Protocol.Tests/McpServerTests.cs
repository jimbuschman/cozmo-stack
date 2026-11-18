using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Cozmo.Bridge;
using Cozmo.Mcp;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class McpServerTests
{
    [Fact]
    public async Task ClientListsSchemasRunsThreeToolsAndReadsRealReports()
    {
        using var f = new Fixture();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        var tools = (await client.Request("tools/list", new { })).GetProperty("result").GetProperty("tools");
        Assert.Equal(26, tools.GetArrayLength());
        foreach (var entry in bridge.GetCommandCatalogue().EnumerateArray())
        {
            var tool = Assert.Single(tools.EnumerateArray(), t => t.GetProperty("name").GetString() == entry.GetProperty("name").GetString());
            Assert.Equal(entry.GetProperty("argumentsSchema").GetRawText(), tool.GetProperty("inputSchema").GetRawText());
        }
        var state = Data(await client.Tool("get_state", new { }));
        Assert.True(state.GetProperty("connection").GetProperty("connected").GetBoolean());

        int mark = f.Rig.Mark();
        int call = await client.Send("tools/call", new { name = "head", arguments = new { radians = 0.23 } });
        f.Rig.Tick();
        var motor = Assert.Single(f.Rig.SentSince(mark).OfType<SetHeadAngle>());
        Assert.Equal(0.23f, motor.AngleRad);
        f.Rig.Data(new MotorActionAck { ActionId = motor.ActionId });
        f.Rig.Tick();
        f.Rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos, head: 0.23f);
        var reply = await client.Receive(call);
        Assert.False(reply.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal(0u, Data(reply).GetProperty("engineResult").GetUInt32());
        Assert.Equal("SUCCESS", Data(reply).GetProperty("engineResultName").GetString());
        var freeplay = Data(await client.Tool("freeplay", new { enabled = false }));
        Assert.Equal("submitted", freeplay.GetProperty("status").GetString());
        f.Rig.Tick();

        // Actual public reports delivered by the offline engine, through the bridge feed.
        var pose = new EngineGamePose(200, 15, 60, 1, 0, 0, 0, f.Robot.CurrentOriginId);
        f.Rig.Engine.PublishRobotObservedFace(new(7, 1001, pose, new FaceRect(100, 50, 50, 50), "Ada", [], [], [], []));
        f.Rig.Engine.DrainAppDelivery();
        var page = Data(await client.Tool("read_events", new { since = 0 }));
        var seen = Assert.Single(page.GetProperty("events").EnumerateArray(), e => e.GetProperty("type").GetString() == "face-recognised");
        Assert.Equal("Ada", seen.GetProperty("payload").GetProperty("name").GetString());
        Assert.Equal(1001u, seen.GetProperty("robotTimestamp").GetUInt32());
        Assert.Empty(Data(await client.Tool("read_events", new { since = page.GetProperty("cursor").GetInt64() })).GetProperty("events").EnumerateArray());
    }

    [Fact]
    public async Task PendingActionDoesNotBlockPollingAndClientCanCancelItsNativeOwner()
    {
        using var f = new Fixture();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        int call = await client.Send("tools/call", new { name = "track-face", arguments = new { faceId = -7 } });
        Assert.IsType<NativeTrackFaceRunner>(f.Robot.ActionList.GetCurrentAction());
        Assert.True((await client.Request("ping", new { })).TryGetProperty("result", out _));
        Assert.True(Data(await client.Tool("get_state", new { })).TryGetProperty("connection", out _));
        await client.Notify("notifications/cancelled", new { requestId = call });
        f.Rig.Tick();
        var reply = await client.Receive(call);
        Assert.True(reply.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("NOT_STARTED", Data(reply).GetProperty("engineResultName").GetString());
        Assert.Null(f.Robot.ActionList.GetCurrentAction());
    }

    [Fact]
    public async Task ClientGetsProtocolErrorsSeparatelyFromToolErrors()
    {
        using var f = new Fixture();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        Assert.Equal(-32600, (await client.Request("tools/list", new { })).GetProperty("error").GetProperty("code").GetInt32());
        await client.Initialize();
        Assert.Equal(-32601, (await client.Request("unavailable", new { })).GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(-32602, (await client.Tool("invented", new { })).GetProperty("error").GetProperty("code").GetInt32());
        foreach (var reply in new[] { await client.Tool("head", new { radians = "bad" }), await client.Tool("read_events", new { since = -1 }), await client.Tool("get_state", new { extra = 2 }) })
            Assert.True(reply.GetProperty("result").GetProperty("isError").GetBoolean());
        await client.Input.Lines.Writer.WriteAsync("{");
        var malformed = await client.Next();
        Assert.Equal(JsonValueKind.Null, malformed.GetProperty("id").ValueKind);
        Assert.Equal(-32700, malformed.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task AnimationCatalogueReadsLoadedLibraryWithoutSelectingAnAnimation()
    {
        using var f = new Fixture();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        f.Rig.Robot.Animations.LoadFrom(PickupPyramidNativeExecutionTests.Resources());
        await using var client = new Client(bridge);
        await client.Initialize();
        var data = Data(await client.Tool("list_animations", new { }));
        Assert.True(data.GetProperty("loaded").GetBoolean());
        Assert.Contains(data.GetProperty("triggers").EnumerateArray(), e => e.GetString() == "CodeLabHappy");
        Assert.Contains(data.GetProperty("clips").EnumerateArray(), e => e.GetString()!.StartsWith("anim_"));
        Assert.Null(f.Robot.ActionList.GetCurrentAction());
    }

    [Fact]
    public void EvidenceContainsProtocolEventsRawFramesDecodedMessagesAndCommit()
    {
        string path = Path.Combine(Path.GetTempPath(), "cozmo-mcp-test-" + Guid.NewGuid().ToString("N"));
        using var f = new Fixture();
        try
        {
            using (var evidence = new SessionEvidence(path, ["--resources", "offline-fixture"]))
            {
                evidence.Attach(f.Rig.Engine);
                f.Rig.Robot.Transport.FrameTrace += evidence.Frame;
                evidence.Record("mcp-input", JsonSerializer.SerializeToElement(new { jsonrpc = "2.0", id = 9, method = "tools/list" }));
                evidence.Log("offline evidence test");
                // The regular fixture's Data helper enters above the UDP/frame layer.
                // Exercise the real incoming-frame hook used by the console instead.
                f.Rig.Robot.Transport.ProcessIncoming(FrameCodec.Encode(Frame.Single(
                    new SubMessage(ReliableMessageType.SingleReliableMessage, new MotorActionAck { ActionId = 37 }.ToBytes(), 900), 1)));
                evidence.Finish("closed");
            }
            using var result = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "result.json")));
            Assert.Equal("LLM-SESSION", result.RootElement.GetProperty("testId").GetString());
            Assert.Equal("closed", result.RootElement.GetProperty("status").GetString());
            Assert.NotEmpty(result.RootElement.GetProperty("commit").GetString()!);
            Assert.Equal(64, result.RootElement.GetProperty("assemblySha256").GetString()!.Length);
            Assert.Contains("offline evidence test", File.ReadAllText(Path.Combine(path, "stack-log.txt")));
            var input = File.ReadLines(Path.Combine(path, "events.jsonl")).Select(line => JsonSerializer.Deserialize<JsonElement>(line))
                .First(row => row.GetProperty("kind").GetString() == "mcp-input");
            Assert.Equal("tools/list", input.GetProperty("method").GetString());
            Assert.True(input.GetProperty("atMs").GetDouble() >= 0);
            Assert.NotEmpty(File.ReadLines(Path.Combine(path, "frames.jsonl")));
            Assert.Contains(File.ReadLines(Path.Combine(path, "messages.jsonl")), line => line.Contains("payloadHex"));
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    private static JsonElement Data(JsonElement reply)
    {
        var result = reply.GetProperty("result");
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(result.GetProperty("structuredContent").GetRawText()),
            System.Text.Json.Nodes.JsonNode.Parse(result.GetProperty("content")[0].GetProperty("text").GetString()!)));
        return result.GetProperty("structuredContent");
    }

    // A duplex, line-oriented in-process MCP client. Every call crosses serialized JSON-RPC.
    // The channel readers remain open while awaiting replies, as a stdio client does.
    private sealed class Client : IAsyncDisposable
    {
        internal readonly LineReader Input = new();
        private readonly LineWriter output = new();
        private readonly Task server;
        private readonly Dictionary<int, JsonElement> deferred = new();
        private int nextId;
        internal Client(CozmoBridge bridge) => server = new CozmoMcpServer(bridge).RunAsync(Input, output);
        internal async Task Initialize()
        {
            var reply = await Request("initialize", new { protocolVersion = "2025-11-25", capabilities = new { }, clientInfo = new { name = "offline-test", version = "1" } });
            Assert.Equal("2025-11-25", reply.GetProperty("result").GetProperty("protocolVersion").GetString());
            await Notify("notifications/initialized", new { });
        }
        internal async Task<int> Send(string method, object parameters)
        {
            int id = ++nextId;
            await Input.Lines.Writer.WriteAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters }));
            // Sending and dispatch run inline up to the native task's first await.
            // Wait for the server to read this request before the fixture advances time.
            await Input.WaitRead();
            return id;
        }
        internal async Task Notify(string method, object parameters)
        {
            await Input.Lines.Writer.WriteAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", method, @params = parameters }));
            await Input.WaitRead();
        }
        internal async Task<JsonElement> Request(string method, object parameters) => await Receive(await Send(method, parameters));
        internal Task<JsonElement> Tool(string name, object arguments) => Request("tools/call", new { name, arguments });
        internal async Task<JsonElement> Next() => JsonSerializer.Deserialize<JsonElement>(await output.Lines.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)));
        internal async Task<JsonElement> Receive(int id)
        {
            if (deferred.Remove(id, out var saved)) return saved;
            while (true)
            {
                var reply = await Next();
                int replyId = reply.GetProperty("id").GetInt32();
                if (replyId == id) return reply;
                deferred.Add(replyId, reply);
            }
        }
        public async ValueTask DisposeAsync()
        {
            Input.Lines.Writer.TryComplete();
            await server.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }

    private sealed class LineReader : TextReader
    {
        internal readonly Channel<string> Lines = Channel.CreateUnbounded<string>(new() { AllowSynchronousContinuations = true });
        private readonly Channel<bool> read = Channel.CreateUnbounded<bool>();
        public override async ValueTask<string?> ReadLineAsync(CancellationToken token)
        {
            // Reaching the next read proves dispatch of the preceding line has completed
            // or suspended on its pending native action.
            read.Writer.TryWrite(true);
            try { return await Lines.Reader.ReadAsync(token); }
            catch (ChannelClosedException) { return null; }
        }
        internal async Task WaitRead()
        {
            // Consume initial read first, then dispatch acknowledgement on next read.
            if (!started) { await read.Reader.ReadAsync(); started = true; }
            await read.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        }
        private bool started;
    }

    private sealed class LineWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        internal readonly Channel<string> Lines = Channel.CreateUnbounded<string>();
        public override Task WriteLineAsync(ReadOnlyMemory<char> value, CancellationToken token = default) => Lines.Writer.WriteAsync(value.ToString(), token).AsTask();
        public override Task FlushAsync(CancellationToken token) => Task.CompletedTask;
    }
}
