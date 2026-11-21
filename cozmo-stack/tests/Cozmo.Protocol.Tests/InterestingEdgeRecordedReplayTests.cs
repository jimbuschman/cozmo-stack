using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class InterestingEdgeRecordedReplayTests(ITestOutputHelper output)
{
    // Private input: the 20261119-182433-LLM-SESSION messages. Start at the first
    // state in origin 9, and replay the camera/pose producers through the recorded
    // Hiking window. The fixture hosts a fresh map and uses its own engine clock.
    [Fact]
    public void RebuildsTheHikingMapFromCapturedCameraAndPose()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_EDGE_REPLAY_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return;

        const double firstOriginState = 241894.4548;
        const double hikingStart = 271738.2185;
        const double hikingEnd = 278877;
        string messages = Path.Combine(bundle, "messages.jsonl");
        using (var input = File.OpenRead(messages))
            output.WriteLine($"messages SHA256={Convert.ToHexString(SHA256.HashData(input))}");

        var packets = new List<(double At, RobotMessage Message)>();
        byte[]? calibration = null;
        foreach (string line in File.ReadLines(messages))
        {
            using var row = JsonDocument.Parse(line);
            var message = row.RootElement;
            double at = message.GetProperty("atMs").GetDouble();
            if (at > hikingEnd) break;
            if (message.GetProperty("outbound").GetBoolean()) continue;

            string? tag = message.GetProperty("tag").GetString();
            if (tag == "0xCD" && at < 30000)
            {
                if (ReadPacket(message) is NVOpResult reply && reply.Tag == CameraCalibration.NvEntryTag && reply.Result == 0)
                    calibration = reply.Data;
            }
            if (at >= firstOriginState && tag is ("0xF0" or "0xF2" or "0xF4" or "0xD1"))
                packets.Add((at, ReadPacket(message)));
        }

        Assert.NotNull(calibration);
        var first = packets.FindIndex(p => p.Message is RobotState { PoseOriginId: 9, PoseFrameId: 8 });
        Assert.True(first >= 0, "The capture must contain the active Hiking origin.");
        Assert.All(packets.Skip(first).Select(p => p.Message).OfType<RobotState>(),
            state => Assert.Equal((9u, 8u), (state.PoseOriginId, state.PoseFrameId)));
        using var f = new Fixture(preloadAnimations: true, sourceNvEmpty: true, sourceNvRaw: tag => tag == CameraCalibration.NvEntryTag ? calibration : null);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        var vision = f.M.Vision;
        var analyzer = f.Runtime.AI.Analyzer;
        var edge = Assert.IsType<VisitInterestingEdgeBehavior>(f.Runtime.Bound["Hiking_VisitInterestingEdge"]);
        var faults = new ConcurrentQueue<Exception>();
        var holds = new ConcurrentQueue<string>();
        f.Rig.Robot.HandlerFaulted += faults.Enqueue;
        vision.Enabled = true;
        int overhead = 0, sampledTicksWithEdges = 0, maxEdges = 0, maxProximity = 0, maxBorders = 0, runnable = 0;
        vision.FrameProcessed += result => { if (result.OverheadEdges is not null) Interlocked.Increment(ref overhead); };
        void RecordHold(string line)
        {
            if (line.Contains("M11-079") || line.Contains("M11-089") || line.Contains("M11-092")) holds.Enqueue(line);
        }

        var initial = (RobotState)packets[first].Message;
        uint offset = initial.Timestamp - f.Robot.StoredState!.Timestamp - 30;
        int index = first;
        bool analyzing = false;
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += RecordHold;
        try
        {
            for (double tick = packets[first].At; index < packets.Count; tick += 60)
            {
                // Replicate the logged Hiking analyzer request at 0x005B31F6. Keep the
                // fixture's behavior selection idle so it cannot move the recorded pose.
                if (!analyzing && tick >= hikingStart)
                {
                    analyzer.AddEnableRequest(1, "Hiking");
                    analyzing = true;
                }
                while (index < packets.Count && packets[index].At <= tick)
                {
                    var message = packets[index++].Message;
                    if (message is RobotState state)
                    {
                        state.Timestamp -= offset;
                        state.PoseOriginId = f.Robot.CurrentOriginId;
                        state.PoseFrameId = f.Robot.PoseFrameId;
                    }
                    else if (message is ImageChunk image && image.FrameTimestamp != 0)
                        image.FrameTimestamp -= offset;
                    f.Rig.Data(message);
                }

                f.Rig.Tick();
                vision.WaitForProcessorIdle();
                f.Rig.Tick(0);
                if (!analyzing) continue;

                var map = f.M.World.Maps.Current!;
                var counts = map.NativeContentCounts;
                int edges = counts.GetValueOrDefault(MemoryMapContentType.InterestingEdge);
                if (edges > 0) sampledTicksWithEdges++;
                maxEdges = Math.Max(maxEdges, edges);
                maxProximity = Math.Max(maxProximity, counts.GetValueOrDefault(MemoryMapContentType.ObstacleProx));
                maxBorders = Math.Max(maxBorders, analyzer.Regions.Count);
                var readiness = edge.EvaluateHostReadiness(f.Runtime.Context);
                Assert.True(readiness.Available, string.Join(Environment.NewLine, holds));
                if (readiness.Runnable) runnable++;
            }
        }
        finally { SteppedBehavior.MissingReported -= RecordHold; }

        var display = bridge.GetMap(2000, 128, render: false).Metadata;
        output.WriteLine($"overhead frames={overhead}; sampled ticks with type 9={sampledTicksWithEdges}; max type 9={maxEdges}; max type 6={maxProximity}; max borders={maxBorders}; runnable ticks={runnable}");
        output.WriteLine($"get_map available={display.GetProperty("available")}; regionCount={display.GetProperty("regionCount")}; truncated={display.GetProperty("truncated")}");
        Assert.True(display.GetProperty("available").GetBoolean());
        Assert.True(overhead > 0, "The replay must exercise the actual DetectingOverheadEdges input.");
        Assert.True(sampledTicksWithEdges > 0, "Recorded camera frames must produce type 9 cells during Hiking.");
        Assert.True(maxBorders > 0, "The sourced analyzer must derive borders from the reconstructed map.");
        Assert.True(runnable > 0, "The owned goal selector must find a runnable vantage during the recorded Hiking window.");
        Assert.Empty(faults);
        Assert.Empty(holds);
    }

    private static RobotMessage ReadPacket(JsonElement row) =>
        RobotMessage.Parse(Convert.FromHexString(row.GetProperty("payloadHex").GetString()!));
}
