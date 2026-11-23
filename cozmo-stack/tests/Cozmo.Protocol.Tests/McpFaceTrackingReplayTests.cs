using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class McpFaceTrackingReplayTests(ITestOutputHelper output)
{
    private sealed record Picture(uint Timestamp, byte[] Pixels);
    private static JsonElement Data(JsonElement reply) => reply.GetProperty("result").GetProperty("structuredContent");

    [Fact]
    public async Task CapturedPixelsSupplyMcpFaceAndDriveTurnAndTracking()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_FACE_REPLAY_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;
        string path = Path.Combine(bundle, "messages.jsonl");
        using var input = File.OpenRead(path);
        Assert.Equal("c67ba13dec4eae4538958e12cad294883f7eb0c441a8a829e4dcd29bdb33ad63", Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant());
        var pictures = new List<Picture>(); var states = new List<RobotState>();
        byte[]? calibration = null;
        var camera = new CozmoCamera();
        camera.FrameReceived += frame => {
            if ((frame.Timestamp is >= 88245 and <= 89000 or >= 230005 and <= 231510 || frame.Timestamp == 178770) && frame.TryDecodeGray(out var image, out _) && image is not null)
                pictures.Add(new(frame.Timestamp, image.Pixels.ToArray()));
        };
        foreach (string line in File.ReadLines(path))
        {
            using var doc = JsonDocument.Parse(line); var row = doc.RootElement;
            if (row.GetProperty("outbound").GetBoolean()) continue;
            var message = RobotMessage.Parse(Convert.FromHexString(row.GetProperty("payloadHex").GetString()!));
            if (message is NVOpResult { Tag: CameraCalibration.NvEntryTag, Result: 0 } nv && row.GetProperty("atMs").GetDouble() < 30000)
                calibration = nv.Data;
            if (message is RobotState state) states.Add(state);
            if (row.GetProperty("tag").GetString() == "0xF2")
                camera.Handle(message);
        }
        Assert.NotNull(calibration); Assert.NotEmpty(pictures);
        var offCenter = pictures.Where(p => p.Timestamp is >= 88245 and <= 89000).ToArray();
        var centered = pictures.Single(p => p.Timestamp == 178770);
        var moving = pictures.Where(p => p.Timestamp >= 230005).ToArray();
        Assert.NotEmpty(offCenter); Assert.NotEmpty(moving);
        var captureState = states.Last(s => s.Timestamp <= offCenter[0].Timestamp);
        output.WriteLine($"pictures={pictures.Count}; original head={captureState.HeadAngle:R}, yaw={captureState.Pose.Angle:R}");
        using var f = new Fixture(sourceNvEmpty: true, sourceNvRaw: tag => tag == CameraCalibration.NvEntryTag ? calibration : null);
        Assert.IsType<OkaoFaceDetector>(f.M.Vision.FaceDetector);
        f.Rig.Robot.SetEnableFreeplayActivity(false); f.Rig.Tick();
        f.M.Vision.Enabled = true;
        f.Head = captureState.HeadAngle; f.Motors();
        var reports = new List<RobotObservedFaceReport>();
        f.Rig.Engine.RobotObservedFace += reports.Add;
        var missing = new ConcurrentQueue<string>();
        void Missing(string line) { if (line.Contains("MISSING", StringComparison.Ordinal)) missing.Enqueue(line); }
        f.Rig.Engine.StackLogLine += Missing;
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        uint imageId = 70000; int imageIndex = 0, mark = f.Rig.Mark();
        var frames = new List<object>(); var commands = new List<object>();
        var headWire = new List<SetHeadAngle>(); var bodyWire = new List<SetBodyAngle>();
        string phase = "observe";
        void Step(bool image)
        {
            f.Motors();
            if (image)
            {
                var picture = phase == "observe" ? offCenter[imageIndex % offCenter.Length]
                    : phase is "moving" or "lateral" ? moving[imageIndex % moving.Length] : centered;
                imageIndex++;
                f.NativeCameraFrame(imageId++, picture.Pixels); f.Rig.Tick();
                frames.Add(new { phase, imageIndex, picture.Timestamp, f.Head, f.Yaw,
                    faces = f.M.Vision.Faces.Faces.Select(face => new { face.Id, face.Name, face.LastObservedTimestamp, face.Rect,
                        point = face.NativeTrackingTranslation is { } point ? new { point.X, point.Y, point.Z } : null }).ToArray() });
            }
            var sent = f.Rig.SentSince(mark); mark += sent.Count;
            foreach (var message in sent)
            {
                if (message is SetHeadAngle h) { headWire.Add(h); commands.Add(new { phase, imageIndex, kind = "head", target = h.AngleRad, speed = h.MaxSpeedRadPerSec, accel = h.AccelRadPerSec2 }); }
                if (message is SetBodyAngle b) { bodyWire.Add(b); commands.Add(new { phase, imageIndex, kind = "body", target = b.AngleRad, speed = b.MaxSpeedRadPerSec, accel = b.AccelRadPerSec2, b.IsAbsolute }); }
            }
        }
        for (int i = 0; i < 8; i++) Step(true);
        var stateReply = Data(await client.Tool("get_state", new { }));
        output.WriteLine("state=" + stateReply);
        int faceId = stateReply.GetProperty("knownFaces").EnumerateArray().First(face => face.GetProperty("lastObservedTimestamp").ValueKind == JsonValueKind.Number).GetProperty("faceId").GetInt32();
        phase = "turn";
        int turnFaceId = faceId;
        int turnCall = await client.Send("tools/call", new { name = "turn-to-face", arguments = new { faceId, maximumTurnRadians = MathF.PI } });
        var turn = client.Receive(turnCall);
        for (int i = 0; i < 80 && !turn.IsCompleted; i++) { Step(true); await Task.Delay(1); }
        var turnReply = await turn.WaitAsync(TimeSpan.FromSeconds(3));
        int turnCommands = commands.Count;
        phase = "centered";
        var beforeTracking = Data(await client.Tool("get_state", new { }));
        faceId = beforeTracking.GetProperty("knownFaces").EnumerateArray()
            .OrderByDescending(face => face.GetProperty("lastObservedTimestamp").ValueKind == JsonValueKind.Number ? face.GetProperty("lastObservedTimestamp").GetUInt32() : 0)
            .First().GetProperty("faceId").GetInt32();
        int trackId = await client.Send("tools/call", new { name = "track-face", arguments = new { faceId, headOnly = false } });
        for (int i = 0; i < 16; i++) { Step(true); await Task.Delay(1); }
        var tracked = Assert.IsType<NativeTrackFaceRunner>(f.Robot.ActionList.GetCurrentAction());
        int settledId = unchecked((int)tracked.GetCompletionUnion());
        var centeredFace = Assert.IsType<FaceEntry>(f.M.Vision.Faces.GetNativeFace(f.Robot, settledId));
        uint centeredStamp = centeredFace.LastObservedTimestamp;
        int settledCommands = commands.Count;
        for (int i = 0; i < 8; i++) { Step(true); await Task.Delay(1); }
        Assert.True(centeredFace.LastObservedTimestamp > centeredStamp, "Settling must be tested with fresh target observations.");
        Assert.Equal(settledCommands, commands.Count);
        Assert.Equal(1, settledId); // Fresh album's native registration remaps the initial -1; no injected identity.
        phase = "moving";
        int followBodyStart = bodyWire.Count;
        for (int i = 0; i < 16; i++) { Step(true); await Task.Delay(1); }
        int followBodyCommands = bodyWire.Count - followBodyStart;
        phase = "stale";
        int beforeStale = commands.Count;
        for (int i = 0; i < 5; i++) Step(false);
        Assert.Equal(beforeStale, commands.Count);
        var active = Data(await client.Tool("get_state", new { })).GetProperty("activeActions");
        uint actionTag = Assert.Single(active.EnumerateArray()).GetProperty("actionTag").GetUInt32();
        Assert.Equal(tracked.Tag, actionTag);
        using (var otherBridge = new CozmoBridge(f.Rig.Robot))
        {
            Assert.Empty(otherBridge.GetState().GetProperty("activeActions").EnumerateArray());
            var denied = await otherBridge.ExecuteAsync("cancel-action", JsonSerializer.SerializeToElement(new { actionTag }));
            Assert.Equal("not-admitted", denied.GetProperty("status").GetString());
            Assert.Same(tracked, f.Robot.ActionList.GetCurrentAction());
        }
        var cancel = Data(await client.Tool("cancel-action", new { actionTag }));
        Assert.Equal("submitted", cancel.GetProperty("status").GetString());
        for (int i = 0; i < 4; i++) Step(false);
        var trackReply = await client.Receive(trackId);
        Assert.Equal("CANCELLED_WHILE_RUNNING", Data(trackReply).GetProperty("engineResultName").GetString());
        // An unmatched fresh face is not a native ID remap. Ask get_state for the newest ID
        // and make a new request, rather than silently retargeting the first tracker.
        var refreshed = Data(await client.Tool("get_state", new { }));
        int lateralId = refreshed.GetProperty("knownFaces").EnumerateArray()
            .OrderByDescending(face => face.GetProperty("lastObservedTimestamp").GetUInt32()).First().GetProperty("faceId").GetInt32();
        var lateralPoint = f.M.Vision.Faces.GetNativeFace(f.Robot, lateralId)!.NativeTrackingTranslation!.Value;
        // Robot MAIN is at x=y=0 in this fixture. 0x00565EA0..0x00565ECE
        // produces the world bearing of the observation, sent as an absolute angle.
        float lateralBearing = MathF.Atan2((float)lateralPoint.Y, (float)lateralPoint.X);
        phase = "lateral";
        int lateralStart = bodyWire.Count;
        int lateralCall = await client.Send("tools/call", new { name = "track-face", arguments = new { faceId = lateralId, headOnly = false } });
        for (int i = 0; i < 8; i++) { Step(true); await Task.Delay(1); }
        int lateralCommands = bodyWire.Count - lateralStart;
        await client.Notify("notifications/cancelled", new { requestId = lateralCall });
        for (int i = 0; i < 4; i++) Step(false);
        var lateralReply = await client.Receive(lateralCall);
        var report = new { faceId, lateralId, lateralCommands, lateralBearing, followBodyCommands, refreshed, lateralReply, captureHead = captureState.HeadAngle, pictures = pictures.Select(p => new { p.Timestamp, hash = Convert.ToHexString(SHA256.HashData(p.Pixels)) }),
            stateReply, turnReply, trackReply, turnCommands, commands, frames, missing };
        string? reportPath = Environment.GetEnvironmentVariable("COZMO_FACE_TRACK_REPORT");
        if (!string.IsNullOrEmpty(reportPath)) File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        output.WriteLine($"face={faceId}; reports={reports.Count}; turnCommands={turnCommands}; totalCommands={commands.Count}; missing={string.Join(" | ", missing.Distinct())}");
        Assert.True(Data(turnReply).GetProperty("admitted").GetBoolean(), turnReply.ToString());
        Assert.Equal(0u, Data(turnReply).GetProperty("engineResult").GetUInt32());
        Assert.Equal(0x02000000u, Data(trackReply).GetProperty("engineResult").GetUInt32());
        Assert.NotEmpty(headWire);
        Assert.True(headWire[0].AngleRad > captureState.HeadAngle);
        Assert.True(lateralCommands > 0, "The new lateral face must produce an actual body command.");
        Assert.All(bodyWire.Skip(lateralStart), move => Assert.True(move.IsAbsolute));
        Assert.InRange(MathF.Abs(bodyWire[lateralStart].AngleRad - lateralBearing), 0f, 0.001f);
        Assert.True(followBodyCommands > 0, "The retained face must produce a body correction on a later observation.");
        Assert.Contains(reports, report => report.FaceId == turnFaceId);
        Assert.True(Data(turnReply).GetProperty("report").GetProperty("facePoseAvailable").GetBoolean());
        Assert.Equal(turnFaceId, Data(turnReply).GetProperty("report").GetProperty("requestedFaceId").GetInt32());
        Assert.Equal(turnFaceId, Data(turnReply).GetProperty("report").GetProperty("faceId").GetInt32());
        Assert.Equal("completed", Data(turnReply).GetProperty("report").GetProperty("endReason").GetString());
        Assert.Equal(1, Data(trackReply).GetProperty("report").GetProperty("faceId").GetInt32());
        Assert.Equal(faceId, Data(trackReply).GetProperty("report").GetProperty("requestedFaceId").GetInt32());
        Assert.Equal("cancelled-while-running", Data(trackReply).GetProperty("report").GetProperty("endReason").GetString());
        Assert.False(Data(trackReply).GetProperty("report").GetProperty("callerCancellationRequested").GetBoolean());
        Assert.Equal(0x02000000u, Data(lateralReply).GetProperty("engineResult").GetUInt32());
        Assert.Equal(lateralId, Data(lateralReply).GetProperty("report").GetProperty("faceId").GetInt32());
        Assert.True(Data(lateralReply).GetProperty("report").GetProperty("callerCancellationRequested").GetBoolean());
        Assert.DoesNotContain(missing, line => line.Contains("native camera-frame face position unavailable", StringComparison.Ordinal));
    }
}
