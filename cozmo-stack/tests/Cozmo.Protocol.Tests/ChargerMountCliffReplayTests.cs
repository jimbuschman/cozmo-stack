using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class ChargerMountCliffReplayTests
{
    private static string? Bundle => Environment.GetEnvironmentVariable("COZMO_CHARGER_CLIFF_BUNDLE");

    [Theory]
    [InlineData(false, 677360, 677875, 14)]
    [InlineData(true, 668850, 669390, 13)]
    public async Task RecordedRobotStopCancelsTheLiveMountBeforeTheCliffReport(bool retry, double start, double end, ushort pathId)
    {
        if (Bundle is not { Length: > 0 } bundle) return;
        var feedback = RecordedRobotInput.ReadInbound(bundle, at => at >= start, end, [0xC0, 0xC6, 0xD4, 0xF0],
            discardRetries: true).ToArray();
        var stop = Assert.IsType<RobotStopped>(Assert.Single(feedback.Where(row => row.Message is RobotStopped)).Message);
        var interrupted = Assert.IsType<PathFollowingEvent>(Assert.Single(feedback.Where(row => row.Message is PathFollowingEvent)).Message);
        Assert.Equal(pathId, interrupted.EventId);
        Assert.Equal((byte)PathEventType.Interrupted, interrupted.EventType);
        var cliffs = feedback.Where(row => row.Message is CliffEvent).Select(row => (CliffEvent)row.Message).ToArray();
        Assert.Equal(new byte[] { 3, 0 }, cliffs.Select(cliff => cliff.DetectedFlags));
        Assert.True(cliffs[0].DidStopForCliff);
        Assert.True(feedback.First(row => row.Message is RobotStopped).At < feedback.First(row => row.Message is CliffEvent).At);
        Assert.Empty(RecordedRobotInput.ReadOutbound(bundle, at => at >= (retry ? 650247 : 676347), end, [0x60]));

        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        Setup(f, retry);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        int mark = f.Rig.Mark();
        int request = await client.Send("tools/call", new { name = "mount-charger", arguments = new { chargerId = 4 } });
        var root = Assert.IsType<NativeDriveToMountChargerRunner>(f.Robot.ActionList.GetCurrentAction());
        if (retry) AdvancePastAlignment(f, root, bundle, 650247, 656000, 660000, 660500);
        WaitFor(f, mark, sent => sent.OfType<ExecutePath>().Any());
        if (retry)
        {
            var reverse = Assert.Single(f.Rig.SentSince(mark).OfType<ExecutePath>());
            Assert.Equal(-30, Assert.Single(f.Rig.SentSince(mark).OfType<AppendPathSegmentLine>()).Speed.SpeedMmps);
            DeliverPath(f, reverse.EventId, PathEventType.Started);
            f.X += 120;
            f.Motors();
            DeliverPath(f, reverse.EventId, PathEventType.Completed);
            WaitFor(f, mark, sent => sent.OfType<ExecutePath>().Count() == 2);
            Assert.Equal(100, f.Rig.SentSince(mark).OfType<AppendPathSegmentLine>().Last().Speed.SpeedMmps);
        }
        var path = f.Rig.SentSince(mark).OfType<ExecutePath>().Last();
        DeliverPath(f, path.EventId, PathEventType.Started);
        Assert.True(f.Robot.BehaviorManagerOwner!.IsReactionTriggerEnabled(ReactionTrigger.CliffDetected));
        Assert.True(f.Robot.BehaviorManagerOwner.IsReactionTriggerEnabled(ReactionTrigger.UnexpectedMovement));
        Assert.True(f.Rig.Robot.Motion.Path.IsActive);
        Assert.False(root.DestructionObserved);

        interrupted.EventId = path.EventId;
        f.Rig.Data(interrupted);
        f.Rig.Data(stop);
        f.Rig.Tick();
        Assert.True(root.DestructionObserved); // 0x0053546E cancels before C0.
        var result = Data(await client.Receive(request));
        Assert.Equal(EngineActionResult.Cancelled, result.GetProperty("engineResult").GetUInt32());
        Assert.DoesNotContain(result.GetProperty("during").GetProperty("events").EnumerateArray(),
            e => e.GetProperty("type").GetString() == "cliff-detected");

        foreach (var cliff in cliffs)
        {
            cliff.Timestamp = f.Robot.StoredState!.Timestamp;
            f.Rig.Data(cliff);
        }
        f.Rig.Tick();
        var events = Data(await client.Tool("read_events", new { since = 0, types = new[] { "cliff-detected" } }));
        var report = Assert.Single(events.GetProperty("events").EnumerateArray()).GetProperty("payload");
        Assert.Equal((byte)(CliffSensors.FrontLeft | CliffSensors.FrontRight), report.GetProperty("sensors").GetByte());
        Assert.True(report.GetProperty("stoppedForCliff").GetBoolean());
        Assert.Empty(f.Rig.SentSince(mark).OfType<EnableStopOnCliff>());
    }

    [Fact]
    public async Task RecordedUnexpectedMovementPreemptsTheBackupPath()
    {
        if (Bundle is not { Length: > 0 } bundle) return;
        var input = RecordedRobotInput.ReadInbound(bundle, at => at >= 619040, 621950, [0xF0])
            .Select(row => (row.At, State: Assert.IsType<RobotState>(row.Message))).ToArray();
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        Setup(f, atPreDock: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var reports = new List<UnexpectedMovementReport>();
        f.Rig.Robot.Sensors.UnexpectedMovementDetected += reports.Add;
        int mark = f.Rig.Mark();
        int request = await client.Send("tools/call", new { name = "mount-charger", arguments = new { chargerId = 4 } });
        var root = Assert.IsType<NativeDriveToMountChargerRunner>(f.Robot.ActionList.GetCurrentAction());
        AdvancePastAlignment(f, root, bundle, 602292, 610500, 615450, 615620);
        WaitFor(f, mark, sent => sent.OfType<ExecutePath>().Any());
        var path = Assert.Single(f.Rig.SentSince(mark).OfType<ExecutePath>());
        Assert.Equal(-30, Assert.Single(f.Rig.SentSince(mark).OfType<AppendPathSegmentLine>()).Speed.SpeedMmps);
        DeliverPath(f, path.EventId, PathEventType.Started);
        Assert.False(f.Robot.BehaviorManagerOwner!.HasDisableLock(ReactionTrigger.UnexpectedMovement, "dockActions"));
        Assert.True(f.Robot.BehaviorManagerOwner.IsReactionTriggerEnabled(ReactionTrigger.UnexpectedMovement));
        uint captureStart = input[0].State.Timestamp;
        uint replayStart = f.Robot.StoredState!.Timestamp + 30;
        double time = input[0].At;
        foreach (var (at, state) in input)
        {
            Rebase(f, state, replayStart + state.Timestamp - captureStart);
            f.Rig.Data(state);
            f.Rig.Tick(at - time);
            time = at;
            if (root.DestructionObserved) break;
        }
        Assert.True(root.DestructionObserved);
        var result = Data(await client.Receive(request));
        Assert.Equal(EngineActionResult.Cancelled, result.GetProperty("engineResult").GetUInt32());
        var movement = Assert.Single(reports);
        Assert.Equal(UnexpectedMovementType.TurnedInOppositeDirection, movement.Type);
        Assert.Equal(UnexpectedMovementSide.Back, movement.Side);
        Assert.Equal(ReactionTrigger.UnexpectedMovement, f.Robot.BehaviorManagerOwner.CurrentReactionTrigger);
        Assert.Contains(f.Rig.SentSince(mark), message => message is StopAllMotors);
    }

    [Fact]
    public async Task CompletedApproachRechecksTheNewChargerPoseAndRejectsItsYOffset()
    {
        if (Bundle is not { Length: > 0 } bundle) return;
        var poses = new Dictionary<uint, (Rotation3d Rotation, Vec3 Position)>();
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (at > 634580) break;
            if (at < 633490 || row.GetProperty("kind").GetString() != "engine-to-game" ||
                row.GetProperty("tag").GetString() != "0x44") continue;
            var r = new CladReader(Convert.FromHexString(row.GetProperty("bodyHex").GetString()!));
            uint timestamp = r.U32();
            uint family = r.U32(), type = r.U32(), id = r.U32();
            if (family != 4 || type != (uint)ObjectType.Charger_Basic || id != 4) continue;
            for (int i = 0; i < 4; i++) r.F32();
            var position = new Vec3(r.F32(), r.F32(), r.F32());
            poses.Add(timestamp, (new Rotation3d(r.F32(), r.F32(), r.F32(), r.F32()), position));
        }
        var terminal = Assert.IsType<PathFollowingEvent>(Assert.Single(RecordedRobotInput.ReadInbound(bundle,
            at => at >= 633540, 633580, [0xC6], discardRetries: true)).Message);
        Assert.Equal((9, (byte)PathEventType.Completed), ((int)terminal.EventId, terminal.EventType));
        using var f = new Fixture(preloadAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        Setup(f, atPreDock: false);
        var origin = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        var charger = f.M.World.GetLocatedObjectByIdHelper(4)!;
        // Capture MAIN at DriveToObject.CheckIfDone. The preceding firmware
        // path completed, then the final comparison uses the refreshed pose.
        var original = poses[635415];
        using var earlier = new Pose3d(original.Rotation, original.Position, parent: origin);
        using var goal = ChargerGeometry.PreDockPose(earlier);
        Assert.InRange(goal.Translation.Y, -24.20, -24.18);
        var newest = poses[636425];
        charger.Pose = new Pose3d(newest.Rotation, newest.Position, parent: origin);
        using var main = new Pose3d(Rotation3d.FromAngleAxis(2.8665168f, new Vec3(0, 0, 1)),
            new Vec3(24.9391556f, -24.0526562f, -.24527323f), parent: origin);
        f.Robot.SetPose(main);

        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        int request = await client.Send("tools/call", new { name = "mount-charger", arguments = new { chargerId = 4 } });
        var root = Assert.IsType<NativeDriveToMountChargerRunner>(f.Robot.ActionList.GetCurrentAction());
        root.Drive.Initialized = true; // archived post-path boundary, no supplied pose verdict
        for (int i = 0; i < 5 && !root.DestructionObserved; i++) f.Rig.Tick();
        Assert.True(root.DestructionObserved);
        var result = Data(await client.Receive(request));
        Assert.Equal((uint)ActionResult.DidNotReachPreActionPose, result.GetProperty("engineResult").GetUInt32());
        var why = result.GetProperty("why");
        var check = why.GetProperty("snapshot").GetProperty("poseCheck");
        Assert.Equal(30.09600257873535, check.GetProperty("offsetYmm").GetDouble(), 5);
        Assert.Equal(21.4231014251709, check.GetProperty("thresholdYmm").GetDouble(), 5);
        Assert.False(check.GetProperty("yawTested").GetBoolean());
        Assert.Contains("8.67 mm outside", why.GetProperty("hint").GetString());
        Assert.Equal(-1, f.Rig.Robot.Cubes.ChargerObjectId); // align child never entered
    }

    [Fact]
    public async Task ReceiveWatchdogReportsWifiTimeoutBeforeTheChargerSleepDeadlines()
    {
        if (Bundle is not { Length: > 0 } bundle) return;
        double timeoutAt = 0;
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            if (!line.Contains("disconnect_reason WifiTimeout", StringComparison.Ordinal)) continue;
            using var doc = JsonDocument.Parse(line);
            timeoutAt = doc.RootElement.GetProperty("atMs").GetDouble();
        }
        Assert.True(timeoutAt > 0);
        byte[]? last = null;
        double lastAt = 0;
        foreach (string line in File.ReadLines(Path.Combine(bundle, "frames.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            if (row.GetProperty("outbound").GetBoolean()) continue;
            lastAt = row.GetProperty("atMs").GetDouble();
            last = Convert.FromBase64String(row.GetProperty("rawBase64").GetString()!);
        }
        Assert.NotNull(last);
        Assert.InRange(timeoutAt - lastAt, 5000, 5060);

        // The offline transport advances the real receive watchdog. Relay its
        // timeout into the engine fixture at the same RCM transport boundary.
        var clock = new ManualClock { NowMs = lastAt };
        using var transport = ReliableTransport.CreateOffline(clock: clock);
        transport.OfflineConnect();
        transport.OfflineAcceptConnection();
        transport.ProcessIncoming(last);
        clock.NowMs = lastAt + 5000;
        Assert.True(transport.OfflineTick());
        clock.NowMs = timeoutAt;
        Assert.False(transport.OfflineTick());

        var contacts = RecordedRobotInput.ReadInbound(bundle, at => at >= 683840, 683980, [0xF0])
            .Select(row => (row.At, State: (RobotState)row.Message))
            .First(row => row.State.Has(RobotStatusFlag.IsOnCharger));
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        Setup(f, atPreDock: false);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        int head = await client.Send("tools/call", new { name = "head", arguments = new { radians = .25 } });
        for (int i = 0; i < 8; i++) f.Motors();
        Assert.Equal(0u, Data(await client.Receive(head)).GetProperty("engineResult").GetUInt32());
        // The native placed-on-charger strategy sets its first-check deadline
        // to now + 20 s (0x006143F8..0x006144D0). The capture was past it.
        f.Rig.Tick(21000);
        var owner = f.Robot;
        Rebase(f, contacts.State, owner.StoredState!.Timestamp + 30);
        f.Rig.Data(contacts.State);
        f.Rig.Tick();
        Assert.True(f.Rig.Robot.Sensors.OnChargerContacts);
        Assert.Equal(RobotDisconnectReason.SleepPlacedOnCharger, f.Rig.Engine.DisconnectReason);
        float armedAt = f.Rig.Engine.Timer.SecondsF;
        Assert.Equal(300, owner.Idle.FaceOffDeadline - armedAt, 2);
        Assert.Equal(330, owner.Idle.DisconnectDeadline - armedAt, 2);
        f.Rig.Tick(lastAt - contacts.At);
        Assert.Equal(2, f.Rig.Engine.ConnectionState);
        Assert.True(f.Rig.Engine.Timer.SecondsF < owner.Idle.FaceOffDeadline);

        f.Rig.Port.TimedOut = !transport.OfflineTick();
        f.Rig.Port.Raise(ReceiverMarker.OnDisconnected, M4ControlTests.Rig.RobotEp);
        f.Rig.Tick(timeoutAt - lastAt);
        var events = Data(await client.Tool("read_events", new { since = 0,
            types = new[] { "going-to-sleep", "robot-disconnected" } })).GetProperty("events").EnumerateArray().ToArray();
        Assert.Equal(new[] { "going-to-sleep", "robot-disconnected" }, events.Select(e => e.GetProperty("type").GetString()));
        Assert.Equal("WifiTimeout", events[1].GetProperty("payload").GetProperty("reason").GetString());
        Assert.Equal(1, events[1].GetProperty("payload").GetProperty("reasonCode").GetByte());
    }

    private static JsonElement Data(JsonElement reply) => reply.GetProperty("result").GetProperty("structuredContent");

    private static void Setup(Fixture f, bool atPreDock)
    {
        f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.X = atPreDock ? 16 : 0;
        f.Motors();
        f.M.World.AddLocatedObject(new ObservableObject(4, ObjectType.Charger_Basic, ChargerGeometry.Markers)
        {
            Pose = new Pose3d(Rotation3d.Identity, new Vec3(atPreDock ? 180 : 340, 0, 0),
                parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),
            OriginId = f.Robot.CurrentOriginId,
            PoseState = PoseState.Known,
        });
    }

    private static void AdvancePastAlignment(Fixture f, NativeDriveToMountChargerRunner root, string bundle,
        double alignFrom, double alignTo, double resultFrom, double resultTo)
    {
        // Use a supplied in-position pre-dock boundary. The actual queued
        // mount still performs alignment, turns, raises its lift and reverses.
        root.Drive.Initialized = true;
        root.Drive.Action.CheckAtEnd = false;
        int mark = f.Rig.Mark();
        bool DockSent() => f.Rig.SentSince(mark).OfType<DockWithObject>().Any();
        for (int i = 0; i < 30 && !DockSent() && !root.DestructionObserved; i++)
        {
            f.Motors(RobotStatusFlag.IsBodyAccMode);
            if (i % 2 == 0)
                f.Image((uint)(54000 + i), cube: true, cubeX: 180, markerObjectType: ObjectType.Charger_Basic);
        }
        Assert.True(DockSent(), string.Join("\n", f.Rig.Log.TakeLast(35)));
        var firmware = Assert.IsType<FirmwareVersion>(Assert.Single(RecordedRobotInput.ReadInbound(bundle,
            _ => true, 30000, [0xEE], discardRetries: true)).Message);
        f.Robot.HandleFirmwareVersion(firmware);
        Assert.True(f.Robot.IsPhysicalRobot);
        var captured = Assert.IsType<DockWithObject>(Assert.Single(
            RecordedRobotInput.ReadOutbound(bundle, at => at >= alignFrom, alignTo, [0x42])).Message);
        Assert.Equal(captured.ToBytes(), Assert.Single(f.Rig.SentSince(mark).OfType<DockWithObject>()).ToBytes());
        var result = Assert.IsType<PickAndPlaceResult>(Assert.Single(
            RecordedRobotInput.ReadInbound(bundle, at => at >= resultFrom, resultTo, [0xB8], discardRetries: true)).Message);
        Assert.True(result.Field1);
        f.Motors(RobotStatusFlag.IsPickingOrPlacing);
        result.Field0 = f.Robot.StoredState!.Timestamp;
        f.Rig.Data(result);
        f.Motors();
    }

    private static void WaitFor(Fixture f, int mark, Func<IReadOnlyList<RobotMessage>, bool> done)
    {
        for (int i = 0; i < 100 && !done(f.Rig.SentSince(mark)); i++)
        {
            if (f.M.GetNativePlanners().Lattice is { CheckPlanningStatus: 1 } planner)
                Assert.True(SpinWait.SpinUntil(() => planner.CheckPlanningStatus != 1, TimeSpan.FromSeconds(10)));
            f.Motors();
        }
        Assert.True(done(f.Rig.SentSince(mark)), string.Join("\n", f.Rig.Log.TakeLast(22)));
    }

    private static void DeliverPath(Fixture f, ushort id, PathEventType kind)
    {
        f.Rig.Data(new PathFollowingEvent { EventId = id, EventType = (byte)kind });
        f.Motors();
    }

    private static void Rebase(Fixture f, RobotState state, uint timestamp)
    {
        state.Timestamp = timestamp;
        state.PoseOriginId = f.Robot.CurrentOriginId;
        state.PoseFrameId = f.Robot.PoseFrameId;
    }
}
