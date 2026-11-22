using System.Buffers.Binary;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class MapStateMcpTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(.15f, 0u)]
    [InlineData(.19f, 0x04000002u)]
    public async Task GoToPoseUsesThePublishedWorldAndChecksArrivalAfterFollowingThePath(float yawError, uint expectedResult)
    {
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        f.X = 120;
        f.Y = -60;
        f.Motors();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var startMap = Data(await client.Tool("get_map", new { }));
        Assert.True(startMap.GetProperty("available").GetBoolean());
        var start = startMap.GetProperty("pose");
        uint origin = start.GetProperty("originId").GetUInt32();
        float targetX = start.GetProperty("xMm").GetSingle() + 170;
        float targetY = start.GetProperty("yMm").GetSingle();
        const float targetYaw = 0;
        int mark = f.Rig.Mark();
        var completions = new List<RobotCompletedAction>();
        f.Robot.ActionList.RegisterActionEndedCallback(completions.Add);
        var diagnostics = new System.Collections.Concurrent.ConcurrentQueue<string>();
        f.Rig.Engine.StackLogLine += line =>
        {
            if (line.Contains("MISSING", StringComparison.Ordinal)) diagnostics.Enqueue(line);
        };
        int animationMark = mark;
        byte? animationTag = null;
        void TickFirmware()
        {
            var stream = f.Rig.Robot.Animations.Scheduler.Stream;
            f.Rig.Data(new AnimationState { NumAudioFramesPlayed = stream.FramesStreamed, NumAnimBytesPlayed = stream.BytesStreamed });
            var messages = f.Rig.SentSince(animationMark);
            animationMark = f.Rig.Mark();
            foreach (var message in messages)
            {
                if (message is StartOfAnimation started)
                {
                    animationTag = started.AnimId;
                    f.Rig.Data(new AnimationStarted { AnimId = started.AnimId });
                }
                else if (message is EndOfAnimation && animationTag is { } ended)
                {
                    f.Rig.Data(new AnimationEnded { AnimId = ended });
                    animationTag = null;
                }
            }
            f.Motors();
        }

        int request = await client.Send("tools/call", new
        {
            name = "go-to-pose",
            arguments = new { xMm = targetX, yMm = targetY, angleRadians = targetYaw },
        });
        var action = Assert.IsType<DriveToPoseAction>(f.Robot.ActionList.GetCurrentAction());
        for (int i = 0; i < 65 && !f.Rig.SentSince(mark).OfType<ExecutePath>().Any(); i++)
            TickFirmware();
        var sent = f.Rig.SentSince(mark);
        var path = Assert.Single(sent.OfType<ExecutePath>());
        var line = sent.OfType<AppendPathSegmentLine>().Last();
        Assert.Equal(targetX - 20f, line.XEndMm, 3); // The path runs in drive-center coordinates.
        Assert.Equal(targetY, line.YEndMm, 3);

        f.Rig.Data(new PathFollowingEvent { EventId = path.EventId, EventType = (byte)PathEventType.Started });
        TickFirmware();
        f.X = targetX + 4f;
        f.Y = targetY - 5f;
        f.Yaw = targetYaw + yawError;
        TickFirmware();
        f.Rig.Data(new PathFollowingEvent { EventId = path.EventId, EventType = (byte)PathEventType.Completed });
        for (int i = 0; i < 180 && !completions.Any(c => c.Tag == action.Tag); i++)
            TickFirmware();

        // Main pose near the goal with the same reported XYZ offsets in both cases.
        // The Ready-state call at 0x0055ADA8 also checks the default 0x3E32B8C2 rad
        // tolerance. Firmware Completed alone cannot produce engine SUCCESS.
        var completion = Assert.Single(completions, c => c.Tag == action.Tag);
        Assert.Equal((12, expectedResult), (completion.ActionType, completion.Result));
        var result = Data(await client.Receive(request));
        Assert.True(result.GetProperty("admitted").GetBoolean());
        Assert.Equal(expectedResult, result.GetProperty("engineResult").GetUInt32());

        var arrival = Data(await client.Tool("get_map", new { })).GetProperty("pose");
        Assert.Equal(origin, arrival.GetProperty("originId").GetUInt32());
        Assert.Equal(targetX + 4f, arrival.GetProperty("xMm").GetSingle(), 3);
        Assert.Equal(targetY - 5f, arrival.GetProperty("yMm").GetSingle(), 3);
        Assert.Equal(targetYaw + yawError, arrival.GetProperty("headingRad").GetSingle(), 4);
        Assert.DoesNotContain(diagnostics, line => line.Contains("M13-033", StringComparison.Ordinal) &&
            !line.Contains("0x00500532", StringComparison.Ordinal) &&
            !line.Contains("0x0050058A", StringComparison.Ordinal) &&
            !line.Contains("0x004FFC76", StringComparison.Ordinal));
        output.WriteLine($"goal=({targetX}, {targetY}, {targetYaw}) arrival=({f.X}, {f.Y}, {f.Yaw}) result=0x{expectedResult:X8}");
        foreach (string diagnostic in diagnostics.Distinct()) output.WriteLine(diagnostic);
    }

    [Fact]
    public async Task StateAndMapReadTheCurrentOriginAndReturnAnOptionalPng()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        f.X = 120;
        f.Y = -60;
        f.Yaw = MathF.PI / 2;
        f.Motors();
        var cube = f.Add(id: 12, x: 170);
        f.M.World.SetLocalizedTo(cube);
        var charger = new ObservableObject(13, ObjectType.Charger_Basic, CubeGeometry.MarkersFor(ObjectType.Charger_Basic))
        {
            OriginId = f.Robot.CurrentOriginId,
            PoseState = PoseState.Known,
            Pose = new Pose3d(Rotation3d.Identity, new Vec3(80, -60, 0), parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),
        };
        f.M.World.AddLocatedObject(charger);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var state = Data(await client.Tool("get_state", new { })).GetProperty("pose");
        Assert.Equal(120f, state.GetProperty("xMm").GetSingle(), 3);
        Assert.Equal(-60f, state.GetProperty("yMm").GetSingle(), 3);
        Assert.Equal(0f, state.GetProperty("zMm").GetSingle(), 3);
        Assert.Equal(MathF.PI / 2, state.GetProperty("headingRad").GetSingle(), 4);
        Assert.Equal(f.Robot.CurrentOriginId, state.GetProperty("originId").GetUInt32());
        Assert.Equal(f.Robot.PoseFrameId, state.GetProperty("poseFrameId").GetUInt32());
        Assert.Equal(cube.ObjectId, state.GetProperty("localizedToObjectId").GetUInt32());
        Assert.True(state.GetProperty("robotTimestamp").GetUInt32() > 0);

        double age = state.GetProperty("ageMs").GetDouble();
        f.Rig.Tick(125);
        var newer = Data(await client.Tool("get_state", new { })).GetProperty("pose");
        Assert.Equal(age + 125, newer.GetProperty("ageMs").GetDouble(), 3);

        var map = f.M.World.Maps.Current!;
        var polygon = new[] { new Vec2(140, -40), new Vec2(171, -40), new Vec2(171, -14), new Vec2(140, -14) };
        map.InsertCliff(polygon, new CliffMapData(new Vec2(0, 1), f.Robot.StoredState!.Timestamp));
        var response = await client.Tool("get_map", new { render = true, radiusMm = 250, limit = 128 });
        var result = response.GetProperty("result");
        var image = result.GetProperty("content")[1];
        Assert.Equal("image/png", image.GetProperty("mimeType").GetString());
        byte[] png = Convert.FromBase64String(image.GetProperty("data").GetString()!);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal(192, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
        Assert.Equal(192, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
        var pixels = StbImageSharp.ImageResult.FromMemory(png, StbImageSharp.ColorComponents.RedGreenBlue).Data;
        Assert.Equal(new byte[] { 255, 255, 255 }, pixels.AsSpan((95 * 192 + 96) * 3, 3).ToArray()); // robot
        Assert.Equal(new byte[] { 244, 228, 255 }, pixels.AsSpan((72 * 192 + 115) * 3, 3).ToArray()); // cube
        Assert.Equal(new byte[] { 86, 232, 255 }, pixels.AsSpan((95 * 192 + 80) * 3, 3).ToArray()); // charger

        var read = Data(response);
        Assert.True(read.GetProperty("available").GetBoolean());
        Assert.Contains(read.GetProperty("objects").EnumerateArray(), o => o.GetProperty("worldObjectId").GetUInt32() == cube.ObjectId);
        var cliff = read.GetProperty("regions").EnumerateArray().First(cell => cell.GetProperty("type").GetString() == "Cliff");
        var world = cliff.GetProperty("worldBoundsMm").EnumerateArray().Select(v => v.GetDouble()).ToArray();
        var relative = cliff.GetProperty("robotBoundsMm").EnumerateArray().Select(v => v.GetDouble()).ToArray();
        Assert.Equal(new[] { world[1] + 60, 120 - world[2], world[3] + 60, 120 - world[0] }, relative);
        var limited = await client.Tool("get_map", new { limit = 1 });
        Assert.Equal(1, limited.GetProperty("result").GetProperty("content").GetArrayLength());
        Assert.Single(Data(limited).GetProperty("regions").EnumerateArray());
        Assert.True(Data(limited).GetProperty("truncated").GetBoolean());
        Assert.True((await client.Tool("get_map", new { radiusMm = 1 })).GetProperty("result").GetProperty("isError").GetBoolean());

        f.Robot.Delocalize(false);
        var replacement = Data(await client.Tool("get_map", new { }));
        Assert.False(replacement.GetProperty("regions").EnumerateArray().Any(cell => cell.GetProperty("type").GetString() == "Cliff"));
    }

    [Fact]
    public async Task SessionFourTableEdgeCliffAppearsBeforeDocking()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_CHARGER_CLIFF_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;
        // Raw F0 states and C0 report from 15:52:49 UTC. The first dock tool
        // request is at 602292 ms, far after this bounded input window.
        const double stop = 349350;
        var input = RecordedRobotInput.ReadInbound(bundle, at => at >= 348000, stop, [0xF0, 0xC0],
            discardRetries: true).ToArray();
        var recorded = Assert.Single(input, row => row.Message is CliffEvent { DetectedFlags: 3 });
        var cliff = Assert.IsType<CliffEvent>(recorded.Message);
        Assert.True(cliff.DidStopForCliff);
        uint firstTimestamp = input.Select(row => row.Message).OfType<RobotState>().First().Timestamp;

        using var f = new Fixture(sourceNvEmpty: true, keepNeedsFull: true);
        f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.Rig.Tick();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        Assert.DoesNotContain(Data(await client.Tool("get_map", new { })).GetProperty("regions").EnumerateArray(),
            cell => cell.GetProperty("type").GetString() == "Cliff");

        var detections = new List<EngineLogEntry>();
        f.Rig.Engine.LogEntry += entry =>
        {
            if (entry.Name == "RobotImplMessaging.HandleCliffEvent.Detected") detections.Add(entry);
        };
        uint start = f.Robot.StoredState!.Timestamp + 30;
        double previousAt = input[0].At;
        foreach (var (at, message) in input)
        {
            if (message is RobotState state)
            {
                state.Timestamp = start + state.Timestamp - firstTimestamp;
                state.PoseFrameId = f.Robot.PoseFrameId;
                state.PoseOriginId = f.Robot.CurrentOriginId;
            }
            else if (message is CliffEvent edge)
                edge.Timestamp = start + edge.Timestamp - firstTimestamp;
            f.Rig.Data(message);
            f.Rig.Tick(at - previousAt);
            previousAt = at;
        }
        Assert.Equal("at -83.047,-322.345. DetectedFlags = 0x03", Assert.Single(detections).Text);
        var read = Data(await client.Tool("get_map", new { radiusMm = 250, limit = 128 }));
        Assert.True(read.GetProperty("available").GetBoolean());
        Assert.Contains(read.GetProperty("regions").EnumerateArray(), cell =>
        {
            if (cell.GetProperty("type").GetString() != "Cliff") return false;
            var bounds = cell.GetProperty("worldBoundsMm");
            return bounds[0].GetDouble() <= -83.047 && bounds[2].GetDouble() >= -83.047 &&
                bounds[1].GetDouble() <= -322.345 && bounds[3].GetDouble() >= -322.345;
        });
    }

    private static System.Text.Json.JsonElement Data(System.Text.Json.JsonElement reply) =>
        reply.GetProperty("result").GetProperty("structuredContent");
}
