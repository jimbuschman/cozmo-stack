using System.Security.Cryptography;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using StbImageSharp;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
using Client = Cozmo.Protocol.Tests.McpServerTests.Client;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class ColorCameraMcpTests
{
    [Fact]
    public async Task ToggleOnlySendsTheNativeColorFlagAndRgbMarkerFrameKeepsItsAnnotation()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        int mark = f.Rig.Mark();
        int modes = f.M.Vision.ModeEnableMask;
        Assert.True(Data(await client.Tool("set_camera_color", new { enabled = true })).GetProperty("report").GetProperty("enabled").GetBoolean());
        Assert.Equal(new byte[] { 0x66, 1 }, Assert.Single(f.Rig.SentSince(mark).OfType<EnableColorImages>()).ToBytes());
        Assert.DoesNotContain(f.Rig.SentSince(mark), message => message is ImageRequest);
        Assert.Equal(modes, f.M.Vision.ModeEnableMask);

        f.Add();
        var reports = new List<RobotObservedObjectReport>();
        f.Rig.Engine.RobotObservedObject += reports.Add;
        // The source pose confirmer needs repeated sightings before 0x44 publishes.
        for (uint frameId = 188; frameId < 198 && reports.Count == 0; frameId++)
        {
            f.Motors();
            f.Image(frameId, cube: true, color: true);
        }
        Assert.NotEmpty(f.M.Vision.LastResult!.Markers);
        var frame = await client.Tool("get_camera_image", new { annotate = true });
        var metadata = Data(frame);
        Assert.True(metadata.GetProperty("isColor").GetBoolean());
        Assert.Equal(2, metadata.GetProperty("encoding").GetInt32());
        Assert.Contains(reports, report => report.Timestamp == metadata.GetProperty("robotTimestamp").GetUInt32());
        Assert.NotEmpty(metadata.GetProperty("objects").EnumerateArray());
        Assert.True(f.M.Vision.EncodedFrameCache.RgbValid);
        var png = Png(frame);
        Assert.Equal((320, 240), (png.Width, png.Height));
        Assert.Equal(new byte[] { 200, 150, 150 }, png.Data[..3]);
        Assert.Contains(Enumerable.Range(0, png.Data.Length / 3), i =>
            png.Data[3 * i] == 255 && png.Data[3 * i + 1] == 180 && png.Data[3 * i + 2] == 0);

        mark = f.Rig.Mark();
        Assert.False(Data(await client.Tool("set_camera_color", new { enabled = false })).GetProperty("report").GetProperty("enabled").GetBoolean());
        Assert.Equal(new byte[] { 0x66, 0 }, Assert.Single(f.Rig.SentSince(mark).OfType<EnableColorImages>()).ToBytes());
        Assert.DoesNotContain(f.Rig.SentSince(mark), message => message is ImageRequest);
        f.Motors();
        f.Image(210, cube: true);
        Assert.False(Data(await client.Tool("get_camera_image", new { })).GetProperty("isColor").GetBoolean());
    }

    [Fact]
    public async Task HeadlightToolSendsTheRobotSetHeadlightMessage()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        int mark = f.Rig.Mark();
        Assert.True(Data(await client.Tool("set-headlight", new { enabled = true })).GetProperty("report").GetProperty("enabled").GetBoolean());
        Assert.True(Assert.Single(f.Rig.SentSince(mark).OfType<SetHeadlight>()).Enable);
        Assert.True(f.Rig.Robot.Lights.HeadlightOn);

        mark = f.Rig.Mark();
        Assert.False(Data(await client.Tool("set-headlight", new { enabled = false })).GetProperty("report").GetProperty("enabled").GetBoolean());
        Assert.False(Assert.Single(f.Rig.SentSince(mark).OfType<SetHeadlight>()).Enable);
        Assert.False(f.Rig.Robot.Lights.HeadlightOn);
    }

    [Fact]
    public async Task RecordedColorScanReassemblesAsEncodingNineAndReturnsAColorPng()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_COLOR_REPLAY_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;
        byte[] payload = File.ReadAllBytes(Path.Combine(bundle, "camera", "P1-enable-color-02350.raw.bin"));
        Assert.Equal("73D4721510600E6E664FBC1245AB35D8EDD6FF06F1EB5641A5DDB1159A00000D",
            Convert.ToHexString(SHA256.HashData(payload)));
        Assert.Equal(1, payload[0]);
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        await client.Tool("set_camera_color", new { enabled = true });
        f.Rig.CameraImageFrame(2350, f.Robot.StoredState!.Timestamp, 8, payload);
        var response = await client.Tool("get_camera_image", new { });
        var metadata = Data(response);
        Assert.Equal(9, metadata.GetProperty("encoding").GetInt32());
        Assert.True(metadata.GetProperty("isColor").GetBoolean());
        Assert.Equal(160, f.Rig.Robot.Camera.LastFrame!.JpegWidth);
        var png = Png(response);
        Assert.Equal((320, 240), (png.Width, png.Height));
        Assert.Contains(Enumerable.Range(0, png.Data.Length / 3),
            i => Math.Abs(png.Data[3 * i] - png.Data[3 * i + 2]) >= 12);
    }

    [Fact]
    public async Task StockFaceDetectorStillReportsAFaceFromTheGreyChannelOfRgbChunks()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_FACE_REPLAY_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;
        var input = new CozmoCamera();
        byte[]? pixels = null, calibration = null;
        float head = 0;
        input.FrameReceived += frame =>
        {
            if (frame.Timestamp == 178770 && frame.TryDecodeGray(out var gray, out _))
                pixels = gray!.Pixels.ToArray();
        };
        foreach (string line in File.ReadLines(Path.Combine(bundle, "messages.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            if (row.GetProperty("outbound").GetBoolean()) continue;
            var message = RobotMessage.Parse(Convert.FromHexString(row.GetProperty("payloadHex").GetString()!));
            if (message is NVOpResult { Tag: CameraCalibration.NvEntryTag, Result: 0 } nv)
                calibration ??= nv.Data;
            else if (message is RobotState state && state.Timestamp <= 178770)
                head = state.HeadAngle;
            else if (message is ImageChunk)
                input.Handle(message);
            if (pixels is not null) break;
        }
        Assert.NotNull(pixels);
        Assert.NotNull(calibration);

        using var f = new Fixture(sourceNvEmpty: true, sourceNvRaw: tag => tag == CameraCalibration.NvEntryTag ? calibration : null);
        Assert.IsType<OkaoFaceDetector>(f.M.Vision.FaceDetector);
        f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.Rig.Tick();
        f.M.Vision.Enabled = true;
        f.Head = head;
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        await client.Tool("set_camera_color", new { enabled = true });
        var reports = new List<RobotObservedFaceReport>();
        f.Rig.Engine.RobotObservedFace += reports.Add;
        for (uint i = 0; i < 12 && reports.Count == 0; i++)
        {
            f.Motors();
            f.NativeCameraFrame(800 + i, pixels, color: true);
            f.Rig.Tick();
        }

        Assert.NotEmpty(reports);
        var image = Data(await client.Tool("get_camera_image", new { annotate = true }));
        Assert.True(image.GetProperty("isColor").GetBoolean());
        uint frameTimestamp = image.GetProperty("robotTimestamp").GetUInt32();
        Assert.Contains(reports, report => report.Timestamp == frameTimestamp);
        Assert.NotEmpty(image.GetProperty("faces").EnumerateArray());
    }

    private static JsonElement Data(JsonElement reply)
    {
        var result = reply.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean(), result.GetRawText());
        return result.GetProperty("structuredContent");
    }

    private static ImageResult Png(JsonElement reply) => ImageResult.FromMemory(
        Convert.FromBase64String(reply.GetProperty("result").GetProperty("content")[1].GetProperty("data").GetString()!),
        ColorComponents.RedGreenBlue);
}
