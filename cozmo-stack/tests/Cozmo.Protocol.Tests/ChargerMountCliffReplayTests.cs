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
    private static string? SessionFive => Environment.GetEnvironmentVariable("COZMO_SESSION5_CHARGER_BUNDLE");
    private static string? SessionSix => Environment.GetEnvironmentVariable("COZMO_SESSION6_CHARGER_BUNDLE");

    [Fact]
    public async Task WifiTimeoutReconnectsThroughReadinessAndPublishesOnlyAfterTheNewHandshake()
    {
        using var f = new Fixture(sourceNvEmpty: true, engineOptions: new CozmoEngineOptions {
            ResourcesPath = PickupPyramidNativeExecutionTests.Resources(), BlockPoolPath = "",
            C2 = CozmoC2Settings.McpDefaults });
        var rig = f.Rig;
        var original = rig.Engine.Robot;
        using var bridge = new CozmoBridge(rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var attempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var recovery = new Cozmo.Mcp.C2LinkRecovery(rig.Robot, bridge, M4ControlTests.Rig.RobotIp,
            _ => { }, (kind, data) => {
                if (kind == "reconnect-gate" && data.GetProperty("ready").GetBoolean()) attempt.TrySetResult();
                if (kind == "reconnected") restored.TrySetResult();
            }, CancellationToken.None);
        rig.Port.TimedOut = true;
        rig.Port.Raise(ReceiverMarker.OnDisconnected, M4ControlTests.Rig.RobotEp);
        rig.Tick();
        await attempt.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var before = bridge.ReadEvents().GetProperty("events").EnumerateArray().ToArray();
        Assert.Single(before, e => e.GetProperty("type").GetString() == "link-lost");
        Assert.DoesNotContain(before, e => e.GetProperty("type").GetString() == "reconnected");
        rig.Port.TimedOut = false;
        rig.Tick();
        rig.Port.Raise(ReceiverMarker.OnConnected, M4ControlTests.Rig.RobotEp);
        rig.Tick();
        rig.Data(new RobotAvailable { SerialNumberHead = 1, HwVersion = 5 });
        rig.Data(new FirmwareVersion { RobotId = 1, Signature = System.Text.Encoding.UTF8.GetBytes(
            """{"version":2381,"time":1546972025,"build":"DEVELOPMENT"}""") });
        rig.Tick();
        rig.Data(new ManufacturingID { SerialNumber = 2, BodyHwVersion = 7, BodyColor = 2 });
        rig.Tick();
        await restored.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.NotSame(original, rig.Engine.Robot);
        var events = Data(await client.Tool("read_events", new { since = 0, types = new[] { "link-lost", "reconnected" } }))
            .GetProperty("events").EnumerateArray().ToArray();
        Assert.Equal(new[] { "link-lost", "reconnected" }, events.Select(e => e.GetProperty("type").GetString()));
        Assert.Equal(new[] { "actions", "map origin", "face IDs" }, events[1].GetProperty("payload")
            .GetProperty("lostState").EnumerateArray().Select(e => e.GetString()));
        Assert.True(Data(await client.Tool("get_state", new { })).GetProperty("connection").GetProperty("connected").GetBoolean());
    }

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

    [Theory]
    [InlineData(4, 602292, 610500, 615450, 615620, 619040, 621950, UnexpectedMovementType.TurnedInOppositeDirection)]
    [InlineData(5, 922300, 922400, 927225, 927280, 931270, 933423, UnexpectedMovementType.TurnedButStopped)]
    [InlineData(6, 1407490, 1407505, 1412795, 1412820, 1416500, 1418800, UnexpectedMovementType.TurnedInOppositeDirection)]
    public async Task RecordedUnexpectedMovementPreemptsTheBackupPath(int session, double alignFrom, double alignTo,
        double resultFrom, double resultTo, double reverseFrom, double reverseTo, UnexpectedMovementType expected)
    {
        if ((session switch { 6 => SessionSix, 5 => SessionFive, _ => Bundle }) is not { Length: > 0 } bundle) return;
        var input = RecordedRobotInput.ReadInbound(bundle, at => at >= reverseFrom, reverseTo, [0xF0])
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
        AdvancePastAlignment(f, root, bundle, alignFrom, alignTo, resultFrom, resultTo);
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
        Assert.Equal(expected, movement.Type);
        Assert.Equal(UnexpectedMovementSide.Back, movement.Side);
        Assert.Equal(ReactionTrigger.UnexpectedMovement, f.Robot.BehaviorManagerOwner.CurrentReactionTrigger);
        Assert.Contains(f.Rig.SentSince(mark), message => message is StopAllMotors);
    }

    [Theory]
    [InlineData(false, 633490, 634580, 633540, 633580, 9, 635415, 636425)]
    [InlineData(true, 554580, 558423, 556940, 556965, 8, 554345, 558195)]
    public async Task CompletedApproachRechecksTheNewChargerPoseAndRejectsItsYOffset(bool sessionFive,
        double poseFrom, double poseTo, double terminalFrom, double terminalTo, int pathId,
        uint initialPoseTimestamp, uint finalPoseTimestamp)
    {
        if ((sessionFive ? SessionFive : Bundle) is not { Length: > 0 } bundle) return;
        var poses = new Dictionary<uint, (Rotation3d Rotation, Vec3 Position)>();
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (at > poseTo) break;
            if (at < poseFrom || row.GetProperty("kind").GetString() != "engine-to-game" ||
                row.GetProperty("tag").GetString() != "0x44") continue;
            var r = new CladReader(Convert.FromHexString(row.GetProperty("bodyHex").GetString()!));
            uint timestamp = r.U32();
            uint family = r.U32(), type = r.U32(), id = r.U32();
            if (family != 4 || type != (uint)ObjectType.Charger_Basic || id != (sessionFive ? 0u : 4u)) continue;
            for (int i = 0; i < 4; i++) r.F32();
            var position = new Vec3(r.F32(), r.F32(), r.F32());
            poses.Add(timestamp, (new Rotation3d(r.F32(), r.F32(), r.F32(), r.F32()), position));
        }
        var terminal = Assert.IsType<PathFollowingEvent>(Assert.Single(RecordedRobotInput.ReadInbound(bundle,
            at => at >= terminalFrom, terminalTo, [0xC6], discardRetries: true)).Message);
        Assert.Equal((pathId, (byte)PathEventType.Completed), ((int)terminal.EventId, terminal.EventType));
        using var f = new Fixture(preloadAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        Setup(f, atPreDock: false);
        var origin = f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId);
        var charger = f.M.World.GetLocatedObjectByIdHelper(4)!;
        // Capture MAIN at DriveToObject.CheckIfDone. The preceding firmware
        // path completed, then the final comparison uses the refreshed pose.
        var original = poses[initialPoseTimestamp];
        using var earlier = new Pose3d(original.Rotation, original.Position, parent: origin);
        using var goal = ChargerGeometry.PreDockPose(earlier);
        if (!sessionFive) Assert.InRange(goal.Translation.Y, -24.20, -24.18);
        var newest = poses[finalPoseTimestamp];
        charger.Pose = new Pose3d(newest.Rotation, newest.Position, parent: origin);
        // Session 5 events.jsonl line 28468 contains the MAIN read before the action ends.
        float yaw = sessionFive ? 3.019374132156372f : 2.8665168f;
        var translation = sessionFive ? new Vec3(197.58929443359375, 29.135700225830078, 1.370034098625183)
            : new Vec3(24.9391556f, -24.0526562f, -.24527323f);
        using var main = new Pose3d(Rotation3d.FromAngleAxis(yaw, new Vec3(0, 0, 1)), translation, parent: origin);
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
        if (sessionFive)
            Assert.Equal(2.22314453125, check.GetProperty("offsetXmm").GetDouble(), 5);
        Assert.Equal(sessionFive ? 40.81589126586914 : 30.09600257873535, check.GetProperty("offsetYmm").GetDouble(), 5);
        Assert.Equal(sessionFive ? 21.42310333251953 : 21.4231014251709, check.GetProperty("thresholdYmm").GetDouble(), 5);
        Assert.False(check.GetProperty("yawTested").GetBoolean());
        Assert.Contains(sessionFive ? "19.39 mm outside" : "8.67 mm outside", why.GetProperty("hint").GetString());
        Assert.Equal(-1, f.Rig.Robot.Cubes.ChargerObjectId); // align child never entered
    }

    [Fact]
    public async Task RecordedTooHighFirmwareResultEndsAlignmentWithoutAReverseOrRetry()
    {
        if (SessionFive is not { Length: > 0 } bundle) return;
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        Setup(f, atPreDock: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        int mark = f.Rig.Mark();
        int request = await client.Send("tools/call", new { name = "mount-charger", arguments = new { chargerId = 4 } });
        var root = Assert.IsType<NativeDriveToMountChargerRunner>(f.Robot.ActionList.GetCurrentAction());
        var firmwareResult = AdvancePastAlignment(f, root, bundle, 570870, 570950, 573240, 573275, success: false);
        Assert.Equal(6, firmwareResult.Field2); // DOCK_FAILURE_TOO_HIGH, DockingResultFromString 0x007C0D34..0x007C0D4A.
        Assert.Equal(0, firmwareResult.Field3);
        for (int i = 0; i < 5 && !root.DestructionObserved; i++) f.Motors();
        Assert.True(root.DestructionObserved);
        var result = Data(await client.Receive(request));
        // 0x00553514..0x005535A0 fails alignment on the clear success byte.
        // Mount CheckIfDone returns that child error before configuring the backup.
        Assert.Equal(0x04000003u, result.GetProperty("engineResult").GetUInt32());
        Assert.Empty(f.Rig.SentSince(mark).OfType<ExecutePath>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReceiveWatchdogReportsWifiTimeoutBeforeTheChargerSleepDeadlines(bool sessionSeven)
    {
        string? bundle = sessionSeven ? Environment.GetEnvironmentVariable("COZMO_SESSION7_CLIFF_BUNDLE") : Bundle;
        if (string.IsNullOrEmpty(bundle)) return;
        double noticeAt = 0;
        double timeoutAt = 0;
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            if (line.Contains("disconnect_reason WifiTimeout", StringComparison.Ordinal))
            {
                using var doc = JsonDocument.Parse(line);
                timeoutAt = doc.RootElement.GetProperty("atMs").GetDouble();
            }
            else if (line.Contains("\"tag\":\"0x2D\"", StringComparison.Ordinal))
            {
                using var doc = JsonDocument.Parse(line);
                noticeAt = doc.RootElement.GetProperty("atMs").GetDouble();
            }
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
        if (sessionSeven)
        {
            // Native charger deadline is 300/330 s (shipped reactToOnCharger.json); the capture
            // ended at the receive watchdog (0x00836080..98), not either sleep deadline.
            Assert.InRange(noticeAt, 272098, 272099);
            Assert.InRange(lastAt - noticeAt, 5900, 6000);
            var sent = new List<(double At, SubMessage Message)>();
            foreach (string line in File.ReadLines(Path.Combine(bundle, "frames.jsonl")))
            {
                using var doc = JsonDocument.Parse(line);
                var row = doc.RootElement;
                double at = row.GetProperty("atMs").GetDouble();
                if (at < noticeAt || !row.GetProperty("outbound").GetBoolean()) continue;
                var frame = FrameCodec.Decode(row.GetProperty("rawBase64").GetBytesFromBase64());
                sent.AddRange(frame.Messages.Select(message => (at, message)));
            }
            // No robot radio/power-down command or deliberate disconnect was sent.
            Assert.DoesNotContain(sent, item => item.Message.Type == ReliableMessageType.DisconnectRequest);
            Assert.DoesNotContain(sent, item => (item.Message.Type is ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage)
                && item.Message.Payload.Length > 0 && item.Message.Payload[0] is 0x52 or 0x53 or 0xA9);
            // Native Update emits idle pings when no reliable messages are pending (0x00836564..90);
            // thereafter resends continue while awaiting acks (0x00836592..A4).
            Assert.Contains(sent, item => item.At > lastAt && item.Message.Type == ReliableMessageType.Ping);
            Assert.Contains(sent, item => item.At > lastAt + 4900 && item.Message.Type == ReliableMessageType.SingleReliableMessage
                && item.Message.Payload[0] == 0x8F);
            Assert.Contains(sent, item => item.At > lastAt + 4900 && item.Message.Type == ReliableMessageType.SingleReliableMessage
                && item.Message.Payload[0] == 0x97);
        }

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

        var contacts = RecordedRobotInput.ReadInbound(bundle, at => at >= (sessionSeven ? 272077 : 683840), sessionSeven ? 272150 : 683980, [0xF0])
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
        int mark = f.Rig.Mark();
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
        Assert.DoesNotContain(f.Rig.SentSince(mark), message => message is EnterSleepMode or PowerState or ShutdownRobot);

        f.Rig.Port.TimedOut = !transport.OfflineTick();
        f.Rig.Port.Raise(ReceiverMarker.OnDisconnected, M4ControlTests.Rig.RobotEp);
        f.Rig.Tick(timeoutAt - lastAt);
        var events = Data(await client.Tool("read_events", new { since = 0,
            types = new[] { "going-to-sleep", "robot-disconnected" } })).GetProperty("events").EnumerateArray().ToArray();
        Assert.Equal(new[] { "going-to-sleep", "robot-disconnected" }, events.Select(e => e.GetProperty("type").GetString()));
        Assert.Equal("WifiTimeout", events[1].GetProperty("payload").GetProperty("reason").GetString());
        Assert.Equal(1, events[1].GetProperty("payload").GetProperty("reasonCode").GetByte());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task C2DockRequiresFusedSightingsAndKeepsOnlyRampReactionLocked(int sightings)
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true,keepNeedsFull:true,
            engineOptions:new CozmoEngineOptions { ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),
                BlockPoolPath="",C2=new CozmoC2Settings { ChargerMemory=true,ChargerDock=true } });
        Setup(f,true);
        f.Motors();
        var owner=f.Robot;
        var origin=owner.GetNativeOrigin(owner.CurrentOriginId);
        var charger=f.M.World.GetLocatedObjectById(4)!;
        uint stamp=owner.StoredState!.Timestamp;
        using(var at=new Pose3d(Rotation3d.Identity,new Vec3(16,0,0),parent:origin))
        {
            for(int i=0;i<sightings;i++)
            {
                charger.LastObservedTimestamp=stamp-(uint)(sightings-1-i);
                owner.Engine.C2ChargerMemory.Observe(owner,charger,4,at);
            }
        }
        var manager=owner.BehaviorManagerOwner!;
        using var cancel=new CancellationTokenSource();
        var resultTask=f.Rig.Robot.Motion.QueueAiMountChargerAsync(4,cancel.Token);
        try
        {
            Assert.False(manager.IsReactionTriggerEnabled(ReactionTrigger.UnexpectedMovement));
            Assert.True(manager.IsReactionTriggerEnabled(ReactionTrigger.CliffDetected));
            if(sightings==3)
            {
                var mount=Assert.IsType<NativeDriveToMountChargerRunner>(owner.ActionList.GetCurrentAction());
                Assert.NotNull(mount.C2Geometry);
                Assert.NotSame(charger,mount.C2Geometry);
                Assert.Equal(0,mount.C2Geometry!.NativePose.Translation.Z);
            }
            else
                Assert.IsType<DriveToPoseAction>(owner.ActionList.GetCurrentAction());
        }
        finally
        {
            cancel.Cancel();
            f.Rig.Tick();
            owner.ActionList.Clear();
        }
        await resultTask.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(manager.IsReactionTriggerEnabled(ReactionTrigger.UnexpectedMovement));
        Assert.True(manager.IsReactionTriggerEnabled(ReactionTrigger.CliffDetected));
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

    private static PickAndPlaceResult AdvancePastAlignment(Fixture f, NativeDriveToMountChargerRunner root, string bundle,
        double alignFrom, double alignTo, double resultFrom, double resultTo, bool success = true)
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
        Assert.Equal(success, result.Field1);
        f.Motors(RobotStatusFlag.IsPickingOrPlacing);
        result.Field0 = f.Robot.StoredState!.Timestamp;
        f.Rig.Data(result);
        f.Motors();
        return result;
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
