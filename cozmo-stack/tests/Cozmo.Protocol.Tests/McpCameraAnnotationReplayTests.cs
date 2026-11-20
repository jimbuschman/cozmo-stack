using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using StbImageSharp;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class McpCameraAnnotationReplayTests(ITestOutputHelper output)
{
    private sealed record Packet(ushort Tag, double AtMs, byte[] Body)
    {
        internal uint Timestamp => BinaryPrimitives.ReadUInt32LittleEndian(Body.AsSpan(Tag == 0x46 ? 4 : 0));
    }

    [Fact]
    public void SavedCameraWindowsReplayExactImagesAndVisionCommits()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_CAMERA_ANNOTATION_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;
        (uint Id, uint Timestamp, int CallId)[] images =
            [(19527,26100,9), (24059,332785,19), (27624,573875,41), (32783,922480,58), (34091,1010915,74)];
        string framesPath = Path.Combine(bundle, "frames.jsonl"), eventsPath = Path.Combine(bundle, "events.jsonl");
        VerifyHash(framesPath, "45EA1D616B8AAE6AB3AE0E2693E10091523974EBCBD5FF061C6ADEF6A4FD8003");
        VerifyHash(eventsPath, "7AFBA9FC39FD138B20DFDC85B890A53B63E918BF76D995E1CB422B4BEBC8F7F0");

        var reports = new List<Packet>();
        var calls = new Dictionary<int, double>();
        foreach (string line in File.ReadLines(eventsPath))
        {
            using var document = JsonDocument.Parse(line);
            var row = document.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (row.GetProperty("kind").GetString() == "engine-to-game")
            {
                ushort tag = row.GetProperty("tag").GetString() switch { "0x43" => 0x43, "0x44" => 0x44, "0x46" => 0x46, _ => 0 };
                if (tag != 0) reports.Add(new(tag, at, Convert.FromHexString(row.GetProperty("bodyHex").GetString()!)));
            }
            else if (row.GetProperty("kind").GetString() == "mcp-input" &&
                     row.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number &&
                     id.TryGetInt32(out int number) &&
                     images.Any(image => image.CallId == number))
            {
                Assert.Equal("get_camera_image", row.GetProperty("params").GetProperty("name").GetString());
                calls.Add(number, at);
            }
        }

        var windows = images.Select(image =>
        {
            Packet earlier = reports.Last(p => p.Tag == 0x43 && p.AtMs < calls[image.CallId]);
            Packet exact = Assert.Single(reports.Where(p => p.Tag == 0x43 && p.Timestamp == image.Timestamp));
            Assert.True(earlier.Timestamp < exact.Timestamp && exact.AtMs > calls[image.CallId]);
            return (image.Id, Earlier: earlier, Exact: exact);
        }).ToArray();
        var stamps = windows.SelectMany(w => new[] { w.Earlier.Timestamp, w.Exact.Timestamp }).ToHashSet();
        var chunksByImage = new Dictionary<uint, List<ImageChunk>>();
        using var receiver = ReliableTransport.CreateOffline();
        receiver.OfflineConnect();
        receiver.Received += frame =>
        {
            if (frame.Marker != ReceiverMarker.Data || frame.Data is not { Length: > 0 } bytes ||
                bytes[0] != (byte)RobotMessageId.Image) return;
            var chunk = (ImageChunk)RobotMessage.Parse(bytes);
            if (!chunksByImage.TryGetValue(chunk.ImageId, out var chunks))
                chunksByImage.Add(chunk.ImageId, chunks = []);
            chunks.Add(chunk);
        };
        var captured = new Dictionary<uint, ImageChunk[]>();
        foreach (string line in File.ReadLines(framesPath))
        {
            using var document = JsonDocument.Parse(line);
            var packet = document.RootElement;
            if (!packet.GetProperty("outbound").GetBoolean())
                receiver.ProcessIncoming(Convert.FromBase64String(packet.GetProperty("rawBase64").GetString()!));
            if (chunksByImage.Count == 0) continue;
            foreach (var entry in chunksByImage.ToArray())
            {
                var last = entry.Value[^1];
                if (last.ImageChunkCount == 0) continue;
                if (stamps.Contains(last.FrameTimestamp)) captured.Add(last.FrameTimestamp, entry.Value.ToArray());
                chunksByImage.Remove(entry.Key);
            }
            // The robot leaves the timestamp and chunk count at zero on non-final chunks.
            foreach (uint stale in chunksByImage.Keys.Where(id => id + 4 < chunksByImage.Keys.Max()).ToArray())
                chunksByImage.Remove(stale);
            if (captured.Count == stamps.Count) break;
        }
        Assert.Equal(stamps.Count, captured.Count);

        using var f = new Fixture();
        f.M.Vision.Enabled = false;
        using var bridge = new CozmoBridge(f.Rig.Robot);
        foreach (var window in windows)
        {
            var earlier = DeliverImage(window.Earlier.Timestamp);
            Commit(earlier);
            var exact = DeliverImage(window.Exact.Timestamp);
            Assert.Equal(window.Id, exact.ImageId);
            var waiting = bridge.GetCameraImage(annotate: true);
            Assert.Equal(earlier.ImageId, waiting.Metadata.GetProperty("imageId").GetUInt32());
            Assert.Equal(exact.ImageId, waiting.Metadata.GetProperty("latestReceivedImageId").GetUInt32());
            Assert.Equal(exact.ImageId, bridge.GetCameraImage().Metadata.GetProperty("imageId").GetUInt32());
            Assert.Empty(bridge.GetCameraImage().Metadata.GetProperty("objects").EnumerateArray());

            Commit(exact);
            var image = bridge.GetCameraImage(annotate: true);
            var meta = image.Metadata;
            Assert.Equal(exact.ImageId, meta.GetProperty("imageId").GetUInt32());
            Assert.Equal(exact.Timestamp, meta.GetProperty("robotTimestamp").GetUInt32());
            Assert.True(meta.GetProperty("visionProcessed").GetBoolean());
            var boxes = meta.GetProperty("faces").EnumerateArray().Concat(meta.GetProperty("objects").EnumerateArray()).ToArray();
            Assert.NotEmpty(boxes);
            var pixels = ImageResult.FromMemory(image.Png, ColorComponents.RedGreenBlue);
            foreach (var box in boxes)
            {
                var rect = box.GetProperty("rect");
                int x = (int)Math.Clamp(rect.GetProperty("x").GetDouble(), 0, 319);
                int y = (int)Math.Clamp(rect.GetProperty("y").GetDouble(), 0, 239);
                int offset = (y * 320 + x) * 3;
                byte[] paint = box.TryGetProperty("faceId", out _) ? [0,255,0] : [255,180,0];
                Assert.Equal(paint, pixels.Data[offset..(offset + 3)]);
            }
            output.WriteLine($"Image {exact.ImageId} timestamp {exact.Timestamp} has {boxes.Length} box(es). At the captured request, image {earlier.ImageId} was the latest processed.");
        }

        CameraFrame DeliverImage(uint timestamp)
        {
            foreach (var chunk in captured[timestamp]) f.Rig.Data(chunk);
            f.Rig.Tick();
            var frame = Assert.IsType<CameraFrame>(f.Rig.Robot.Camera.LastFrame);
            Assert.Equal(timestamp, frame.Timestamp);
            return frame;
        }

        void Commit(CameraFrame frame)
        {
            // Replay the recorded 0x43 commit and its 0x44/0x46 observations.
            // The production path uses the same bridge handler when the native
            // result is consumed. Other tests exercise the real detector path.
            var observations = reports.Where(p => p.Timestamp == frame.Timestamp).ToArray();
            Assert.Single(observations.Where(p => p.Tag == 0x43));
            var faces = observations.Where(p => p.Tag == 0x46).Select(p => ReadFace(p.Body)).ToArray();
            var objects = observations.Where(p => p.Tag == 0x44).Select(p => ReadObject(p.Body)).ToArray();
            using var pose = new Pose3d();
            bridge.VisionProcessed(new(frame.ImageId, frame.Timestamp,
                new VisionPoseData(frame.Timestamp, pose, 0, 0, false, false), [], [], [], TimeSpan.Zero)
            {
                CameraCapture = new(f.Robot, frame, faces, objects),
            });
        }
    }

    private static void VerifyHash(string path, string expected)
    {
        using var input = File.OpenRead(path);
        Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(input)));
    }

    private static EngineGamePose ReadPose(CladReader r) =>
        new(r.F32(),r.F32(),r.F32(),r.F32(),r.F32(),r.F32(),r.F32(),r.U32());

    private static RobotObservedFaceReport ReadFace(byte[] bytes)
    {
        var r = new CladReader(bytes);
        int id = r.I32();
        uint timestamp = r.U32();
        var pose = ReadPose(r);
        var rect = new FaceRect(r.F32(), r.F32(), r.F32(), r.F32());
        return new(id, timestamp, pose, rect, r.String8(), [], [], [], []);
    }

    private static RobotObservedObjectReport ReadObject(byte[] bytes)
    {
        var r = new CladReader(bytes);
        return new(r.U32(),r.U32(),r.U32(),r.U32(),r.F32(),r.F32(),r.F32(),r.F32(),ReadPose(r),r.F32(),r.Bool());
    }
}
