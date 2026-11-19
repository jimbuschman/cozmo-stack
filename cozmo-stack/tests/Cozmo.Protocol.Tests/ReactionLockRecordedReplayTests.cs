using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
using Client = Cozmo.Protocol.Tests.McpServerTests.Client;

namespace Cozmo.Protocol.Tests;

public sealed partial class ReactionLockMcpTests
{
    private const double FirstMove = 511894.2744, FirstSight = 523536.393;
    private const double SecondMove = 618393.6554, SecondSight = 619414.5737;
    private const double FirstMoveExit = 519033.7495;
    private const double FirstReacquireEnd = 523536.4488, SecondReacquireEnd = 619414.6265;
    private const double RefusedSpeech = 621915.4257, EndOfReaction = 623193.4586;
    private const string CaptureVariable = "COZMO_REACTION_LOCK_BUNDLE";

    // Replay starts at the reaction ingress: committed BlockWorld observations and
    // the engine's cube radio broadcasts. Captured 0x88 messages are assertions only.
    [Theory]
    [InlineData(FirstMove, FirstSight, FirstReacquireEnd, false)]
    [InlineData(FirstMove, FirstSight, FirstReacquireEnd, true)]
    [InlineData(SecondMove, SecondSight, SecondReacquireEnd, false)]
    [InlineData(SecondMove, SecondSight, SecondReacquireEnd, true)]
    public async Task RecordedCubeUpdatesHonorTheNamedLock(double moveAt, double sightAt, double reactionAt, bool locked)
    {
        string? bundle = Environment.GetEnvironmentVariable(CaptureVariable);
        if (string.IsNullOrEmpty(bundle)) return;
        var evidence = ReadCapture(bundle);
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        await SetUpBehavior(f);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        var manager = f.Robot.BehaviorManagerOwner!;
        var cube = AddPriorObservation(f, evidence, moveAt);

        if (locked) await Lock(client, "stacking", new[] { "CubeMoved", "ObjectPositionUpdated" });
        var observed = new List<byte[]>();
        f.Rig.Engine.EngineToGameMessage += (tag, bytes) => { if (tag == 0x88) observed.Add(bytes.ToArray()); };
        var emissions = evidence.Where(row => row.At >= moveAt && row.At <= reactionAt).ToArray();
        var moved = ObjectMoved.Read(new CladReader(emissions.Single(e => e.At == moveAt).Body));
        if (moveAt == FirstMove)
        {
            const double cubeMoveReactionAt = 512916.7246;
            var state = ReadStateBefore(bundle, cubeMoveReactionAt);
            // Preserve the captured radio-to-state interval while joining the fixture's
            // robot clock. Inserting the absolute capture time would hit the native
            // raw-state history's 5000 ms gap rejection (M11-097).
            uint elapsedSinceMove = state.Timestamp - moved.Timestamp;
            moved.Timestamp = f.Robot.StoredState!.Timestamp + 1;
            state.Timestamp = moved.Timestamp + elapsedSinceMove;
            f.Rig.Engine.AnimationMessages.Deliver(0x0F, true, moved);
            FeedCapturedState(f, state);
            // In the short replay the state tick installs captured odometry first.
            // Separate the manager check from that tick's reaction update.
            manager.HostReactionChecksEnabled = false;
            f.Rig.Tick();
            manager.HostReactionChecksEnabled = true;
            var activation = manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
            f.Rig.Engine.DrainAppDelivery();
            byte[] expected = emissions.Single(e => e.At == cubeMoveReactionAt).Body;
            if (locked) Assert.Empty(observed);
            else
            {
                var strategy = manager.Reactions.Select(r => r.Strategy).OfType<CubeMovedReactionStrategy>().Single();
                var motion = Assert.Single(strategy.Tracker.Entries);
                var locator = new CubeLocator(f.M.Vision);
                Assert.True(activation?.Trigger == ReactionTrigger.CubeMoved,
                    $"Robot pose={state.Pose.X:F2},{state.Pose.Y:F2}, yaw={state.Pose.Angle:F2}; " +
                    $"head={state.HeadAngle:F3}; timestamp={state.Timestamp}, stored={f.Robot.StoredState?.Timestamp}, " +
                    $"radio timestamp={moved.Timestamp}, motion={motion.Moving}/{motion.Observed}, " +
                    $"distance={locator.DistanceFromRobotMm(cube.ObjectId)}, visible={locator.IsVisibleFromCamera(cube.ObjectId)}, " +
                    $"current={manager.Current?.Id}, transitions={string.Join(',', observed.Select(Convert.ToHexString))}.");
                Assert.Equal(expected, Assert.Single(observed));
                // The capture returns to NoneTrigger before the second turn and reacquisition.
                // Restore that captured entry context for the next input window.
                manager.Stop(BehaviorStopReason.Interrupted, f.Rig.Engine.Timer.Seconds);
                f.Rig.Engine.DrainAppDelivery();
                Assert.Equal(emissions.Single(e => e.At == FirstMoveExit).Body, observed[1]);
            }
            Assert.True(await manager.StartAsync("Wait", f.Rig.Engine.Timer.Seconds));
            observed.Clear();
        }
        else f.Rig.Engine.AnimationMessages.Deliver(0x0F, true, moved);

        foreach (var e in emissions.Where(e => e.Tag == "0x10"))
            f.Rig.Engine.AnimationMessages.Deliver(0x10, true, ObjectStoppedMoving.Read(new CladReader(e.Body)));
        ApplyObservation(f, cube, emissions.Single(e => e.At == sightAt));
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        f.Rig.Engine.DrainAppDelivery();
        if (locked)
        {
            Assert.Null(manager.CurrentReactionTrigger);
            Assert.Empty(observed);
        }
        else
        {
            Assert.Equal(ReactionTrigger.ObjectPositionUpdated, manager.CurrentReactionTrigger);
            Assert.Equal(emissions.Single(e => e.At == reactionAt).Body, Assert.Single(observed));
        }
    }

    [Fact]
    public async Task RecordedRefusedSpeechWaitsForTheReactionAndDeliversAudio()
    {
        string? bundle = Environment.GetEnvironmentVariable(CaptureVariable);
        if (string.IsNullOrEmpty(bundle)) return;
        var evidence = ReadCapture(bundle);
        var options = new CozmoEngineOptions
        {
            ResourcesPath = PickupPyramidNativeExecutionTests.Resources(), BlockPoolPath = "",
            SpeechSoundBankDirectory = GraphRig.FindMeta(), LabLocaleLanguage = "en", LabLocaleCountry = "US",
        };
        using var sink = new SpeechLatencyTests.SilentSink();
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true,
            engineOptions: options, engineCreated: e => e.RuntimeAudioSinkForTest = () => sink, keepNeedsFull: true);
        await f.Rig.Engine.RuntimeWwiseAudio!.Ready.WaitAsync(TimeSpan.FromSeconds(30));
        await SetUpBehavior(f);
        var rig = f.Rig;
        var manager = f.Robot.BehaviorManagerOwner!;
        using var bridge = new CozmoBridge(rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();

        var cube = AddPriorObservation(f, evidence, SecondMove);
        var state = ReadStateBefore(bundle, SecondSight);
        uint previousTimestamp = f.Robot.StoredState!.Timestamp;
        state.Timestamp = previousTimestamp + 1;
        FeedCapturedState(f, state);
        rig.Tick();
        var missing = new System.Collections.Concurrent.ConcurrentQueue<string>();
        SteppedBehavior.ResetMissingForTests();
        rig.Engine.StackLogLine += line => { if (line.StartsWith("MISSING:", StringComparison.Ordinal)) missing.Enqueue(line); };
        ApplyObservation(f, cube, evidence.Single(e => e.At == SecondSight));
        Assert.Equal(ReactionTrigger.ObjectPositionUpdated, manager.CheckReactions(rig.Engine.Timer.Seconds)?.Trigger);
        rig.Engine.DrainAppDelivery();
        Assert.True(f.Robot.ActionList.ExternalActionsDisabled);

        // This request is positioned at the recorded refusal, while the second
        // AcknowledgeObject still owns the native admission gate.
        var request = evidence.Single(e => e.At == RefusedSpeech).Input;
        string text = request.GetProperty("arguments").GetProperty("text").GetString()!;
        var speechResults = new List<uint>();
        rig.Engine.ActionCompleted += report =>
        {
            if (report.ActionType == 0x20) speechResults.Add(report.Result);
        };
        int id = await client.Send("tools/call", new { name = "say-text", arguments = new { text, waitAdmittedMs = 30000 } });
        rig.Engine.DrainAppDelivery();
        Assert.Equal(new[] { EngineActionResult.NotStarted }, speechResults);
        int mark = rig.Mark();
        int audioFrames = 0;
        bool deliveredFollowup = false;
        var following = new Queue<Captured>(evidence.Where(e => e.Tag == "0x44" && e.At > SecondSight &&
            e.At < EndOfReaction && ObservationId(e) == cube.ObjectId));
        byte? playing = null;
        Task<JsonElement>? response = null;
        var wall = Stopwatch.StartNew();
        var transitions = new List<byte[]>();
        rig.Engine.EngineToGameMessage += (tag, body) => { if (tag == 0x88) transitions.Add(body.ToArray()); };
        // The firmware model advances by one bounded step per ACK cycle.
        // Host synthesis can take arbitrarily long to schedule in CI.
        rig.DefaultTickMs = 4;
        while (response?.IsCompleted != true && wall.Elapsed < TimeSpan.FromSeconds(120))
        {
            if (manager.CurrentReactionTrigger == ReactionTrigger.ObjectPositionUpdated &&
                rig.Engine.AnimationMessages.Count(0x44, true) > 0 && following.TryDequeue(out var sighting))
            {
                // Deliver the recorded observation through the same 0x44 signal
                // as BlockWorld uses to release VisuallyVerifyObject.
                ApplyObservation(f, cube, sighting);
                deliveredFollowup = true;
            }
            var stream = rig.Robot.Animations.Scheduler.Stream;
            rig.Data(new AnimationState { NumAudioFramesPlayed = stream.FramesStreamed, NumAnimBytesPlayed = stream.BytesStreamed });
            f.Motors();
            var messages = rig.SentSince(mark);
            mark += messages.Count;
            foreach (var message in messages)
            {
                switch (message)
                {
                    case StartOfAnimation a: playing = a.AnimId; rig.Data(new AnimationStarted { AnimId = a.AnimId }); break;
                    case EndOfAnimation when playing is { } tag: rig.Data(new AnimationEnded { AnimId = tag }); playing = null; break;
                    case AudioSample when manager.CurrentReactionTrigger is null:
                        audioFrames++;
                        break;
                }
            }
            if (speechResults.Count > 1 || wall.Elapsed > TimeSpan.FromSeconds(111)) response ??= client.Receive(id);
            await Task.Delay(4);
        }

        foreach (string gap in missing) output.WriteLine(gap);
        Assert.True(deliveredFollowup, "No follow-up captured sighting reached the reaction.");
        Assert.True(response?.IsCompleted == true,
            $"No speech completion after recorded 0x44 updates. Active reaction={manager.CurrentReactionTrigger}; " +
            $"sent audio frames={audioFrames}; transitions={string.Join(',', transitions.Select(Convert.ToHexString))}.");
        var speech = Data(await response!);
        Assert.True(speech.GetProperty("engineResultName").GetString() == "SUCCESS", speech.ToString());
        CheckWait(speech, "ObjectPositionUpdated", timedOut: false);
        Assert.Contains(evidence.Single(e => e.At == EndOfReaction).Body,
            transitions, EqualityComparer<byte[]>.Create((a, b) => a!.AsSpan().SequenceEqual(b)));
        Assert.Equal(new[] { EngineActionResult.NotStarted, EngineActionResult.Success }, speechResults);
        Assert.True(audioFrames > 0);
    }

    private sealed record Captured(double At, string Tag, byte[] Body, JsonElement Input = default);

    private static List<Captured> ReadCapture(string bundle)
    {
        string path = Path.Combine(bundle, "events.jsonl");
        using var input = File.OpenRead(path);
        Assert.Equal("7AFBA9FC39FD138B20DFDC85B890A53B63E918BF76D995E1CB422B4BEBC8F7F0",
            Convert.ToHexString(SHA256.HashData(input)));
        input.Position = 0;
        using var reader = new StreamReader(input);
        var result = new List<Captured>();
        while (reader.ReadLine() is { } line)
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (at > EndOfReaction) break;
            if (at == RefusedSpeech)
                result.Add(new(at, "mcp-input", [], row.GetProperty("params").Clone()));
            if (row.GetProperty("kind").GetString() != "engine-to-game") continue;
            string tag = row.GetProperty("tag").GetString()!;
            if (tag is "0x44" or "0x0F" or "0x10" or "0x88")
                result.Add(new(at, tag, Convert.FromHexString(row.GetProperty("bodyHex").GetString()!)));
        }
        return result;
    }

    private static RobotState ReadStateBefore(string bundle, double limit)
    {
        using var transport = ReliableTransport.CreateOffline();
        transport.OfflineConnect();
        RobotState? last = null;
        transport.Received += e =>
        {
            if (e.Data is { Length: > 0 } data && data[0] == (byte)RobotMessageId.State)
                last = Assert.IsType<RobotState>(RobotMessage.Parse(data));
        };
        foreach (string line in File.ReadLines(Path.Combine(bundle, "frames.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            if (row.GetProperty("atMs").GetDouble() > limit) break;
            if (!row.GetProperty("outbound").GetBoolean())
                transport.ProcessIncoming(Convert.FromBase64String(row.GetProperty("rawBase64").GetString()!));
        }
        return Assert.IsType<RobotState>(last);
    }

    private static void FeedCapturedState(Fixture f, RobotState state)
    {
        state.PoseFrameId = f.Robot.PoseFrameId;
        state.PoseOriginId = f.Robot.CurrentOriginId;
        f.X = state.Pose.X;
        f.Y = state.Pose.Y;
        f.Yaw = state.Pose.Angle;
        f.Head = state.HeadAngle;
        f.Lift = state.LiftAngle;
        f.Rig.Data(state);
    }

    private static uint ObservationId(Captured observation) =>
        System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(observation.Body.AsSpan(12));

    private static ObservableObject AddPriorObservation(Fixture f, List<Captured> evidence, double before)
    {
        var prior = evidence.Last(e => e.Tag == "0x44" && e.At < before && ObservationId(e) == 1);
        var type = (ObjectType)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(prior.Body.AsSpan(8));
        var cube = new ObservableObject(ObservationId(prior), type, CubeGeometry.MarkersFor(type))
        {
            OriginId = f.Robot.CurrentOriginId,
            PoseState = PoseState.Known,
        };
        f.M.World.AddLocatedObject(cube);
        ApplyObservation(f, cube, prior);
        // Restore the entry state from the earlier cube acknowledgment.
        f.Robot.BehaviorManagerOwner!.Reactions.Select(r => r.Strategy).OfType<ObjectPositionUpdatedStrategy>().Single()
            .RobotReactedToId(cube.ObjectId);
        return cube;
    }

    private static void ApplyObservation(Fixture f, ObservableObject cube, Captured observation)
    {
        var reader = new CladReader(observation.Body);
        uint timestamp = reader.U32(), family = reader.U32(), type = reader.U32(), id = reader.U32();
        for (int i = 0; i < 4; i++) reader.F32(); // image rect
        var position = new Vec3(reader.F32(), reader.F32(), reader.F32());
        var rotation = new Rotation3d(reader.F32(), reader.F32(), reader.F32(), reader.F32());
        Assert.Equal(cube.ObjectId, id);
        Assert.Equal((uint)cube.Type, type);
        using var pose = new Pose3d(rotation, position, parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));
        cube.Pose = pose;
        cube.LastObservedTimestamp = timestamp;
        f.Rig.Robot.CameraSettings.LastProcessedImageTimestamp = timestamp;
        f.Rig.Engine.AnimationMessages.Deliver(0x44, true, new NativeObservedObject(timestamp, family, type, id));
        Observe(f, cube, timestamp);
    }
}
