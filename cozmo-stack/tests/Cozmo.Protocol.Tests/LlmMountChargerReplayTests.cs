using System.Text.Json;
using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class LlmMountChargerReplayTests(ITestOutputHelper output)
{
    private static JsonDocument Inputs() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "mount_charger_llm_native.json")));

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task McpExplainsRecordedFinalApproachFailure(int index)
    {
        using var inputs = Inputs();
        using var f = new Fixture(sourceNvEmpty: true);
        var charger = Install(f, inputs.RootElement.GetProperty("rows")[index]);
        using var bridge = new Cozmo.Bridge.CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        int request = await client.Send("tools/call", new { name = "mount-charger", arguments = new { chargerId = charger.ObjectId } });
        var root = Assert.IsType<NativeDriveToMountChargerRunner>(f.Robot.ActionList.GetCurrentAction());
        // Recorded post-route boundary: skip only the already completed approach
        // drive. The actual native final pose comparison and action retirement run.
        root.Drive.Initialized = true;
        for (int i = 0; i < 8; i++) f.Rig.Tick();
        var reply = (await client.Receive(request)).GetProperty("result");
        Assert.True(reply.GetProperty("isError").GetBoolean());
        var data = reply.GetProperty("structuredContent");
        Assert.Equal(0x04000001u, data.GetProperty("engineResult").GetUInt32());
        var why = data.GetProperty("why");
        Assert.Equal(data.GetProperty("engineResultName").GetString(), why.GetProperty("engineResultName").GetString());
        var pose = why.GetProperty("snapshot").GetProperty("poseCheck");
        Assert.Equal(index == 3, pose.GetProperty("yawTested").GetBoolean());
        if (index == 0)
        {
            Assert.InRange(pose.GetProperty("offsetYmm").GetDouble() - pose.GetProperty("thresholdYmm").GetDouble(), 12.2, 12.3);
            Assert.Contains("12.22 mm", why.GetProperty("hint").GetString());
        }
        else
        {
            Assert.Contains("-10.8 degrees", why.GetProperty("hint").GetString());
            Assert.Contains("7.5 degrees", why.GetProperty("hint").GetString());
        }
        Assert.Equal(data.GetRawText(), JsonDocument.Parse(reply.GetProperty("content")[0].GetProperty("text").GetString()!).RootElement.GetRawText());
    }

    [Fact]
    public async Task McpExplainsRecordedBackupPitchAfterNativeRetryDrive()
    {
        using var inputs = Inputs();
        var row = inputs.RootElement.GetProperty("rows")[2];
        using var f = new Fixture(sourceNvEmpty: true);
        var charger = Install(f, row);
        var state = row.GetProperty("state");
        var packet = f.Rig.MakeState((RobotStatusFlag)state.GetProperty("status").GetUInt32(),
            origin: f.Robot.CurrentOriginId, frame: f.Robot.PoseFrameId);
        packet.Pose.Pitch = state.GetProperty("pitch").GetSingle();
        f.Rig.Data(packet); f.Rig.Tick();
        using (var main = Main(f, state)) f.Robot.SetPose(main);
        using var bridge = new Cozmo.Bridge.CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        int request = await client.Send("tools/call", new { name = "mount-charger", arguments = new { chargerId = charger.ObjectId } });
        var root = Assert.IsType<NativeDriveToMountChargerRunner>(f.Robot.ActionList.GetCurrentAction());
        // This numeric fixture starts after alignment/turn, at the recorded backup
        // comparison. Install that boundary without inventing a firmware dock result.
        // Real BackupRunner, retry decision, retry drive and parent retirement run.
        root.Drive.Initialized = true;
        root.Drive.Action.CheckAtEnd = false;
        Assert.Equal(ActionResult.Success, root.Mount.Initialize());
        var mountRunner = root.Children[1];
        mountRunner.Initialized = true;
        var backupType = typeof(NativeDriveToMountChargerRunner).GetNestedType("BackupRunner", BindingFlags.NonPublic)!;
        var backup = (ActionRunner)Activator.CreateInstance(backupType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, [f.M, f.Robot, root.Mount], null)!;
        backup.Initialized = true;
        var phaseType = mountRunner.GetType().GetNestedType("Stage", BindingFlags.NonPublic)!;
        mountRunner.GetType().GetMethod("SetStage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
            mountRunner, [Enum.Parse(phaseType, "Mount"), new ActionRunner[] { backup }]);
        int mark = f.Rig.Mark();
        for (int i = 0; i < 10 && !f.Rig.SentSince(mark).OfType<ExecutePath>().Any(); i++) f.Rig.Tick();
        var execute = Assert.Single(f.Rig.SentSince(mark).OfType<ExecutePath>());
        f.Rig.Data(new PathFollowingEvent { EventId = execute.EventId, EventType = 0 }); f.Rig.Tick();
        f.Rig.Data(new PathFollowingEvent { EventId = execute.EventId, EventType = 2 }); f.Rig.Tick();
        for (int i = 0; i < 5; i++) f.Rig.Tick();
        var reply = (await client.Receive(request)).GetProperty("result");
        Assert.True(reply.GetProperty("isError").GetBoolean());
        var data = reply.GetProperty("structuredContent");
        Assert.Equal(inputs.RootElement.GetProperty("backupExpected").GetProperty("retryResult").GetUInt32(), data.GetProperty("engineResult").GetUInt32());
        var why = data.GetProperty("why");
        Assert.Equal(packet.Pose.Pitch, why.GetProperty("snapshot").GetProperty("backup").GetProperty("pitchRad").GetDouble(), 6);
        Assert.Contains("-15.9 degrees", why.GetProperty("hint").GetString());
        Assert.Contains("-15.0 degree cutoff", why.GetProperty("hint").GetString());
        Assert.NotEmpty(why.GetProperty("snapshot").GetProperty("pathPlanner").EnumerateArray());
    }

    [Fact]
    public async Task QueuedActionCannotInheritAnotherActionsFailedCheck()
    {
        using var inputs = Inputs();
        using var f = new Fixture(sourceNvEmpty: true);
        var charger = Install(f, inputs.RootElement.GetProperty("rows")[0]);
        using var bridge = new Cozmo.Bridge.CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        int first = await client.Send("tools/call", new { name = "mount-charger", arguments = new { chargerId = charger.ObjectId } });
        var root = Assert.IsType<NativeDriveToMountChargerRunner>(f.Robot.ActionList.GetCurrentAction());
        int second = await client.Send("tools/call", new { name = "pickup", arguments = new { objectId = 999 } });
        root.Drive.Initialized = true;
        // The first command's real final pose failure precedes the queued
        // pickup's BAD_OBJECT. Neither tags nor call time windows identify ownership.
        for (int i = 0; i < 8; i++) f.Rig.Tick();
        var firstData = (await client.Receive(first)).GetProperty("result").GetProperty("structuredContent");
        var secondData = (await client.Receive(second)).GetProperty("result").GetProperty("structuredContent");
        Assert.True(firstData.GetProperty("why").GetProperty("snapshot").TryGetProperty("poseCheck", out _));
        Assert.False(secondData.GetProperty("why").GetProperty("snapshot").TryGetProperty("poseCheck", out _));
        Assert.False(secondData.GetProperty("why").GetProperty("checkAvailable").GetBoolean());
        Assert.Equal((uint)ActionResult.BadObject, secondData.GetProperty("engineResult").GetUInt32());
    }

    // Post-vision boundary, not a camera/localisation or physical mounting oracle.
    // Expected geometry and decisions come from emu_mount_charger_llm.py running
    // 4E9FB0/50DF00/550098/550FD2..551196 in the shipped engine.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void RecordedFinalPoseChecksMatchNativeGeometryAndFailureReason(int index)
    {
        using var inputs = Inputs();
        var row = inputs.RootElement.GetProperty("rows")[index];
        var expected = row.GetProperty("expected");
        using var f = new Fixture(sourceNvEmpty: true);
        var charger = Install(f, row);
        using var main = f.Robot.CopyCheckedMainPose();
        var pre = DockPreActionPoses.Get(new(charger, PreActionType.Docking, false,
            DriveToObjectAction.PreActionAngleToleranceRad, 0, false, 0),
            main, null, () => Array.Empty<PreActionObstacle>());
        try
        {
            Assert.Equal(ActionResult.Success, pre.Result);
            var goal = Assert.Single(pre.Poses).WorldPose;
            Near(expected.GetProperty("goal")[4].GetDouble(), goal.Translation.X);
            Near(expected.GetProperty("goal")[5].GetDouble(), goal.Translation.Y);
            Near(expected.GetProperty("goal")[6].GetDouble(), goal.Translation.Z);
            Near(expected.GetProperty("offset")[0].GetDouble(), pre.OffsetX);
            Near(expected.GetProperty("offset")[1].GetDouble(), pre.OffsetY);
            Near(expected.GetProperty("threshold")[0].GetDouble(), pre.ThresholdX);
            Near(expected.GetProperty("threshold")[1].GetDouble(), pre.ThresholdY);
            bool inPosition = expected.GetProperty("inPosition").GetInt32() != 0;
            Assert.Equal(inPosition, pre.InPosition);
            if (index == 0)
            {
                Assert.True(pre.OffsetY > pre.ThresholdY);
                Assert.Null(pre.CheckedYawRad); // native skips the angle test outside the position box
            }
            else
            {
                Assert.NotNull(pre.CheckedYawRad);
                Near(expected.GetProperty("yaw").GetDouble(), pre.CheckedYawRad!.Value, .00001);
                if (index == 3) Assert.True(Math.Abs(pre.CheckedYawRad.Value) >
                    DriveToObjectAction.PreActionAngleToleranceRad + DockPreActionPoses.YawSlack);
            }

            var logs = new List<EngineLogEntry>();
            f.Rig.Engine.LogEntry += logs.Add;
            var runner = new NativeDriveToObjectRunner(f.M, f.Robot, charger.ObjectId, PreActionType.Docking);
            try
            {
                Assert.Equal(inPosition ? 0u : 0x04000001u, runner.CheckIfDone());
                if (!inPosition)
                {
                    Assert.Contains(logs, e => e.Name == "DriveToObjectAction.CheckIfDone" &&
                        e.Text.Contains("Robot not in position, will return FAILURE_RETRY."));
                    var diagnostic = Assert.Single(logs, e => e.Name == "CozmoStack.DriveToObject.PoseCheck");
                    Assert.Contains("offsetMm=(", diagnostic.Text);
                    Assert.Contains("thresholdMm=(", diagnostic.Text);
                    Assert.Contains(index == 0 ? "yawRad=not-tested" : "yawRad=-0.188", diagnostic.Text);
                    Assert.Contains("MAIN=", diagnostic.Text);
                    output.WriteLine(diagnostic.Text);
                }
                else Assert.DoesNotContain(logs, e => e.Name == "DriveToObjectAction.CheckIfDone");
            }
            finally { runner.WatcherEnding(); }
        }
        finally { foreach (var pose in pre.Poses) pose.WorldPose.Dispose(); }
    }

    // 54E7A8 reads Robot+304 via GetPitchAngle 51237E; contacts win, then
    // pitch < BE860A92 (-15deg) fails before DriveStraight::CheckIfDone.
    [Fact]
    public void RecordedBackupPitchFailsBeforeDriveCompletionAndSelectsRetryWindow()
    {
        using var inputs = Inputs();
        var row = inputs.RootElement.GetProperty("rows")[2];
        using var f = new Fixture(sourceNvEmpty: true);
        var charger = Install(f, row);
        var state = row.GetProperty("state");
        var packet = f.Rig.MakeState((RobotStatusFlag)state.GetProperty("status").GetUInt32(),
            origin: f.Robot.CurrentOriginId, frame: f.Robot.PoseFrameId);
        packet.Pose.Pitch = state.GetProperty("pitch").GetSingle();
        f.Rig.Data(packet); // pitch/status are the recorded fields; fixture frame/origin route to its current owner
        f.Rig.Tick();
        using (var main = Main(f, state)) f.Robot.SetPose(main);
        var mount = new MountChargerAction(f.M, charger.ObjectId);
        int baseCalls = 0;
        uint result = mount.BackupCheckIfDone(() => { baseCalls++; return 0x01000000; }, f.Robot);
        var expected = inputs.RootElement.GetProperty("backupExpected");
        Assert.Equal(expected.GetProperty("result").GetUInt32(), result);
        Assert.Equal(expected.GetProperty("baseDriveCalls").GetInt32(), baseCalls);
        Assert.False(f.Rig.Robot.Sensors.OnCharger);
        Assert.True(packet.Pose.Pitch < MountChargerAction.MaxBackupPitchRad);
        Assert.False(mount.InsideRetryWindow(f.Robot));
        Assert.Equal(expected.GetProperty("retryResult").GetUInt32(), (uint)MountChargerAction.RetryDriveResult);
        output.WriteLine(FormattableString.Invariant(
            $"pitch={packet.Pose.Pitch:R}; limit={MountChargerAction.MaxBackupPitchRad:R}; native retry result=0x{expected.GetProperty("retryResult").GetUInt32():X8}"));
    }

    [Fact]
    public void PrivateSessionBackupPacketReachesTheSameNativePitchDecision()
    {
        string? bundle = Environment.GetEnvironmentVariable("COZMO_LLM_SESSION_BUNDLE");
        if (string.IsNullOrEmpty(bundle)) return; // optional private input, never a committed hardware bundle
        RobotState? packet = null;
        int lastLine = 0, lineNumber = 0;
        // The operator's capture process may still hold its writer open.
        using var source = new StreamReader(new FileStream(Path.Combine(bundle, "frames.jsonl"),
            FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        while (source.ReadLine() is { } line)
        {
            lineNumber++;
            using var document = JsonDocument.Parse(line);
            var row = document.RootElement;
            if (row.GetProperty("atMs").GetDouble() > 612214.56) break;
            if (row.GetProperty("outbound").GetBoolean()) continue;
            byte[] raw = Convert.FromBase64String(row.GetProperty("rawBase64").GetString()!);
            if (!FrameCodec.TryDecode(raw, out var frame, out _)) continue;
            foreach (var message in frame!.Messages)
                if (message.Type is ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage &&
                    message.Payload is { Length: > 0 } data && data[0] == 0xF0)
                { packet = Assert.IsType<RobotState>(RobotMessage.Parse(data)); lastLine = lineNumber; }
        }
        Assert.NotNull(packet);
        using var inputs = Inputs();
        var selected = inputs.RootElement.GetProperty("rows")[2];
        var state = selected.GetProperty("state");
        Assert.Equal(state.GetProperty("line").GetInt32(), lastLine);
        Assert.Equal(state.GetProperty("pitch").GetSingle(), packet.Pose.Pitch);
        Assert.Equal(state.GetProperty("status").GetUInt32(), packet.Status);
        using var f = new Fixture(sourceNvEmpty: true);
        var charger = Install(f, selected);
        // Only the fixture's routing IDs are substituted; the actual recorded
        // pitch/status/odometry bytes reach the normal packet handler and tick.
        packet.PoseFrameId = f.Robot.PoseFrameId;
        packet.PoseOriginId = f.Robot.CurrentOriginId;
        f.Rig.Data(packet); f.Rig.Tick();
        using (var main = Main(f, state)) f.Robot.SetPose(main);
        var mount = new MountChargerAction(f.M, charger.ObjectId);
        int calls = 0;
        Assert.Equal(inputs.RootElement.GetProperty("backupExpected").GetProperty("result").GetUInt32(),
            mount.BackupCheckIfDone(() => { calls++; return 0x01000000; }, f.Robot));
        Assert.Equal(0, calls);
        Assert.False(mount.InsideRetryWindow(f.Robot));
    }

    [Fact]
    public void FalsePoseFlagKeepsNativeFailureAndRestoresItsLogEvenWhenPoseFunctionFails()
    {
        using var inputs = Inputs();
        using var f = new Fixture(sourceNvEmpty: true);
        var charger = Install(f, inputs.RootElement.GetProperty("rows")[0]);
        var runner = new NativeDriveToObjectRunner(f.M, f.Robot, charger.ObjectId, PreActionType.Docking);
        var logs = new List<string>();
        runner.SourceLog = (_, _, key, _) => logs.Add(key);
        runner.Action.LastPreActionCheckForDiagnostics = "stale snapshot";
        runner.Action.PosesFunction = (ObservableObject _, out IReadOnlyList<PreActionPose> poses, ref bool inPosition) =>
        { poses = Array.Empty<PreActionPose>(); return ActionResult.BadPose; };
        try
        {
            // 559AA0 preserves function result only when the in-position byte is true.
            Assert.Equal(0x04000001u, runner.CheckIfDone());
            Assert.Equal(new[] { "DriveToObjectAction.CheckIfDone" }, logs);
        }
        finally { runner.WatcherEnding(); }
    }

    [Fact]
    public void NativeFailureLogCannotContinueDiagnosticsAfterTheParentRetires()
    {
        using var inputs = Inputs();
        using var f = new Fixture(sourceNvEmpty: true);
        var charger = Install(f, inputs.RootElement.GetProperty("rows")[0]);
        bool current = true;
        var runner = new NativeDriveToObjectRunner(f.M, f.Robot, charger.ObjectId, PreActionType.Docking, () => current);
        var logs = new List<string>();
        runner.SourceLog = (_, _, key, _) =>
        {
            logs.Add(key);
            if (key == "DriveToObjectAction.CheckIfDone") current = false;
        };
        try
        {
            Assert.Equal(EngineActionResult.Cancelled, runner.CheckIfDone());
            Assert.Equal(new[] { "DriveToObjectAction.CheckIfDone" }, logs);
        }
        finally { runner.WatcherEnding(); }
    }

    private static ObservableObject Install(Fixture f, JsonElement row)
    {
        var c = row.GetProperty("charger");
        var xyz = c.GetProperty("xyz");
        var q = c.GetProperty("q");
        var charger = new ObservableObject(0, ObjectType.Charger_Basic, ChargerGeometry.Markers)
        {
            Pose = new Pose3d(new Rotation3d(q[0].GetDouble(), q[1].GetDouble(), q[2].GetDouble(), q[3].GetDouble()),
                new Vec3(xyz[0].GetDouble(), xyz[1].GetDouble(), xyz[2].GetDouble()),
                parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId)),
            OriginId = f.Robot.CurrentOriginId,
            PoseState = PoseState.Known,
        };
        f.M.World.AddLocatedObject(charger);
        using var main = Main(f, row.GetProperty("state"));
        f.Robot.SetPose(main);
        return charger;
    }

    private static Pose3d Main(Fixture f, JsonElement state) => new(
        Rotation3d.FromAngleAxis(state.GetProperty("yaw").GetSingle(), new Vec3(0, 0, 1)),
        new Vec3(state.GetProperty("x").GetSingle(), state.GetProperty("y").GetSingle(), 0),
        parent: f.Robot.GetNativeOrigin(f.Robot.CurrentOriginId));

    private static void Near(double expected, double actual, double tolerance = .003) =>
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
}

