using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class BridgeCommandTests
{
    [Fact]
    public async Task MissingFaceSuccessExplainsThatNoTurnTargetWasAvailable()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        int mark = f.Rig.Mark();
        var task = Invoke(bridge, "turn-to-face", """{"faceId":1,"maximumTurnRadians":3.14}""");
        f.Rig.Tick();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0u, result.GetProperty("engineResult").GetUInt32());
        Assert.False(result.GetProperty("report").GetProperty("facePoseAvailable").GetBoolean());
        Assert.Contains("no turn target was available", result.GetProperty("detail").GetString());
        Assert.DoesNotContain(f.Rig.SentSince(mark), message => message is Cozmo.Protocol.SetHeadAngle or Cozmo.Protocol.SetBodyAngle);
    }

    [Theory]
    [InlineData("head", """{"radians":0.18}""", "MoveAction", 18)]
    [InlineData("lift", """{"heightMm":45}""", "MoveAction", 19)]
    [InlineData("drive-straight", """{"distanceMm":-125,"speedMmps":35,"shouldPlayAnimation":false}""", "DriveStraightAction", 8)]
    [InlineData("turn-in-place", """{"angleRadians":-1.2,"speedRadPerSec":1.4,"accelRadPerSec2":0.8}""", "NativeTurnInPlaceRunner", 40)]
    [InlineData("turn-to-face", """{"faceId":-7,"maximumTurnRadians":2}""", "NativeLastFaceRunner", 41)]
    [InlineData("turn-to-cube", """{"objectId":23,"maximumTurnRadians":2}""", "NativeTurnTowardsObjectRunner", 44)]
    [InlineData("track-face", """{"faceId":-7,"headOnly":true}""", "NativeTrackFaceRunner", 35)]
    [InlineData("animation", """{"trigger":"CodeLabHappy","loops":2,"useSafeLiftMotion":false,"ignoreHeadTrack":true}""", "TriggerAnimationAction", 26)]
    [InlineData("pickup", """{"objectId":23,"driveToObject":false}""", "NativePickupObjectRunner", 21)]
    [InlineData("put-down", "{}", "NativePlaceGroundRunner", 24)]
    [InlineData("roll", """{"objectId":23}""", "NativeDriveToRollRunner", 31)]
    [InlineData("stack", """{"objectId":23}""", "NativeDriveToPlaceOnBeaconRunner", 21)]
    [InlineData("flip", """{"objectId":23}""", "NativeDriveAndFlipRunner", 15)]
    [InlineData("wheelie", """{"objectId":23}""", "NativeDriveToPopAWheelieRunner", 29)]
    [InlineData("go-to-pose", """{"xMm":200,"yMm":54,"angleRadians":1.4}""", "DriveToPoseAction", 12)]
    [InlineData("mount-charger", """{"chargerId":27}""", "NativeDriveToMountChargerRunner", -2)]
    public async Task CommandsReachTheExistingActionListAndKeepTheNativeResult(string command, string json, string runnerName, int type)
    {
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        using var cancel = new CancellationTokenSource();

        var task = Invoke(bridge, command, json, cancel.Token);
        var runner = Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(runnerName, runner.GetType().Name);
        Assert.Equal(type, runner.Type);
        switch (runner)
        {
            case Robot.Manipulation.DriveStraightAction drive:
                Assert.False(drive.ShouldPlayAnimation);
                Assert.Equal((-125d, 35f), (drive.DistanceMm, drive.SpeedMmps));
                break;
            case NativeTurnInPlaceRunner turn:
                Assert.Equal(-1.2f, turn.Parameters.Angle0x88);
                break;
            case NativeLastFaceRunner face:
                Assert.Equal(-7, face.RequestedId);
                Assert.Equal(2f, face.MaximumTurn);
                break;
            case NativeTrackFaceRunner track:
                Assert.Equal(1u, track.RequiredTrackMask);
                break;
            case Robot.Manipulation.NativePickupObjectRunner pickup:
                Assert.Equal(23u, pickup.TargetId);
                break;
            case Robot.Manipulation.NativeDriveToRollRunner roll:
                Assert.Equal(23u, roll.Roll.Action.ObjectId);
                break;
            case Robot.Manipulation.NativeDriveToPlaceOnBeaconRunner stack:
                Assert.Equal(23u, stack.Drive!.Action.ObjectId);
                break;
            case Robot.Manipulation.DriveToPoseAction goTo:
                var goal = Assert.Single(goTo.Goals!);
                Assert.Equal(new Vec3(200, 54, 0), goal.Translation);
                Assert.Equal(1.4f, (float)goal.AngleAroundZ, 5);
                break;
            case TriggerAnimationAction animation:
                Assert.Equal((AnimationTrigger.CodeLabHappy, 2, 1), (animation.Request.Trigger, animation.Request.Argument2, animation.Request.Argument4));
                break;
            case Robot.Manipulation.NativeDriveAndFlipRunner flip:
                Assert.Equal(23u, flip.Flip.Target);
                break;
            case Robot.Manipulation.NativeDriveToPopAWheelieRunner wheelie:
                Assert.Equal(23u, wheelie.Pop.Action.ObjectId);
                break;
            case Robot.Manipulation.NativeDriveToMountChargerRunner mount:
                Assert.Equal(27u, mount.Drive.Action.ObjectId);
                break;
        }
        cancel.Cancel();
        f.Rig.Tick();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0x02000001u, result.GetProperty("engineResult").GetUInt32());
        Assert.Equal("NOT_STARTED", result.GetProperty("engineResultName").GetString());
    }

    [Fact]
    public async Task SayTextAdmitsTheExistingSpeechAction()
    {
        if (!OperatingSystem.IsWindows() || Robot.Emulation.ShippedLibraries.FindLibDir() is null) return;
        string banks = GraphRig.FindMeta() ?? throw new InvalidOperationException("Shipped banks missing");
        var options = new CozmoEngineOptions
        {
            ResourcesPath = PickupPyramidNativeExecutionTests.Resources(), SpeechSoundBankDirectory = banks,
            LabLocaleLanguage = "en", BlockPoolPath = "",
        };
        using var f = new Fixture(engineOptions: options, sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        using var cancel = new CancellationTokenSource();

        var task = Invoke(bridge, "say-text", """{"text":"Hello Cozmo"}""", cancel.Token);
        Assert.Equal("Hello Cozmo", Assert.IsType<SayTextAction>(f.Robot.ActionList.GetCurrentAction()).Text);

        cancel.Cancel();
        f.Rig.Tick();
        var reply = await task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(reply.GetProperty("admitted").GetBoolean());
        Assert.Equal("NOT_STARTED", reply.GetProperty("engineResultName").GetString());
    }

    [Fact]
    public async Task ACompletedHeadCommandReportsTheMotorResult()
    {
        using var f = new Fixture();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        var rig = f.Rig;
        int mark = rig.Mark();
        var task = Invoke(bridge, "head", """{"radians":0.23}""");
        rig.Tick();
        var motor = Assert.Single(rig.SentSince(mark).OfType<SetHeadAngle>());
        Assert.Equal(0.23f, motor.AngleRad);
        rig.Data(new MotorActionAck { ActionId = motor.ActionId });
        rig.Tick();
        rig.State(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos, head: 0.23f);

        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(result.GetProperty("admitted").GetBoolean());
        Assert.Equal(0u, result.GetProperty("engineResult").GetUInt32());
        Assert.Equal("SUCCESS", result.GetProperty("engineResultName").GetString());
    }

    [Fact]
    public async Task FaceCommandsUseTheNativeGameMessagesAndWaitForMatchingReports()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        var engine = f.Rig.Engine;
        var requests = new List<object>();
        using var enroll = engine.AnimationMessages.SubscribeMessage(0x87, false, message => requests.Add(message));
        using var rename = engine.AnimationMessages.SubscribeMessage(0x8C, false, message => requests.Add(message));
        using var erase = engine.AnimationMessages.SubscribeMessage(0x89, false, message => requests.Add(message));
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var teaching = Invoke(bridge, "teach-face", """{"name":"Ada","saveId":7}""", cancel.Token);
        f.Rig.Tick();
        var enrollRequest = Assert.IsType<NativeEnrollmentRequest>(Assert.Single(requests));
        Assert.NotNull(enrollRequest.MusicScope);
        Assert.Equal(new NativeEnrollmentRequest("Ada", 0, 7, true, true, true), enrollRequest with { MusicScope = null });
        Assert.Equal("MeetCozmo", bridge.GetState().GetProperty("activity").GetString());
        f.Robot.PublishEnrollmentResult(0, 8, "Unrelated");
        engine.DrainAppDelivery();
        Assert.False(teaching.IsCompleted);
        f.Robot.PublishEnrollmentResult(3, 7, "Ada");
        engine.DrainAppDelivery();
        var enrollment = await teaching;
        Assert.Equal(3u, enrollment.GetProperty("engineResult").GetUInt32());
        Assert.Equal("TimedOut", enrollment.GetProperty("engineResultName").GetString());
        Assert.Equal(7, enrollment.GetProperty("report").GetProperty("id").GetInt32());
        Assert.Equal("failed", enrollment.GetProperty("faceOperation").GetProperty("outcome").GetString());

        var renaming = Invoke(bridge, "rename-face", """{"faceId":7,"oldName":"Ada","newName":"Grace"}""", cancel.Token);
        f.Rig.Tick();
        Assert.Equal(new NativeRenameFaceRequest(7, "Ada", "Grace"), requests[1]);
        f.Robot.PublishFaceRenamed(7, "Unrelated");
        engine.DrainAppDelivery();
        Assert.False(renaming.IsCompleted);
        f.Robot.PublishFaceRenamed(7, "Grace");
        engine.DrainAppDelivery();
        var renamed = await renaming;
        Assert.Equal("confirmed", renamed.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, renamed.GetProperty("engineResult").ValueKind);
        Assert.Equal("Grace", renamed.GetProperty("report").GetProperty("name").GetString());
        Assert.Equal("renamed", renamed.GetProperty("faceOperation").GetProperty("outcome").GetString());

        var erasing = Invoke(bridge, "erase-face", """{"faceId":7}""", cancel.Token);
        f.Rig.Tick();
        var request = Assert.IsType<NativeEraseFaceRequest>(requests[2]);
        Assert.Equal(7, request.Id);
        Assert.Same(f.Robot, request.Owner);
        engine.DeliverAnimationReportToApp(0x57, new byte[] { 7, 0, 0, 0, 0 }, engine.CaptureFaceErased(new(7, "")));
        engine.DrainAppDelivery();
        var erased = await erasing;
        Assert.Equal(7, erased.GetProperty("report").GetProperty("id").GetInt32());
        Assert.Equal("confirmed", erased.GetProperty("status").GetString());
        Assert.Null(erased.GetProperty("engineResultName").GetString());
        Assert.Equal("erased", erased.GetProperty("faceOperation").GetProperty("outcome").GetString());
        Assert.Equal("", erased.GetProperty("report").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, erased.GetProperty("faceOperation").GetProperty("lastKnownName").ValueKind);

        var wrongFace = Invoke(bridge, "teach-face", """{"name":"Ada"}""", cancel.Token);
        f.Rig.Tick();
        f.Robot.PublishEnrollmentResult(1, 8, "Grace");
        engine.DrainAppDelivery();
        var refused = await wrongFace;
        Assert.Equal("SawWrongFace", refused.GetProperty("engineResultName").GetString());
        Assert.Equal(8, refused.GetProperty("report").GetProperty("id").GetInt32());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FreeplayPostsThePublicActivityRequest(bool enabled)
    {
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        f.Rig.Robot.SetEnableFreeplayActivity(!enabled);
        f.Rig.Tick();

        var result = await Invoke(bridge, "freeplay", enabled ? """{"enabled":true}""" : """{"enabled":false}""");
        f.Rig.Tick();
        Assert.Equal("submitted", result.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("engineResult").ValueKind);
        Assert.Equal(enabled, !f.Runtime.Tracker.PausedFlags.Contains(FreeplayPauseFlag.GameControl));
    }

    [Fact]
    public void CatalogueExposesAllCommandsAndTheirArgumentSchemas()
    {
        using var robot = CozmoRobot.CreateOffline();
        using var bridge = new CozmoBridge(robot);
        var entries = bridge.GetCommandCatalogue().EnumerateArray().ToArray();
        Assert.Equal(new[]
        {
            "head", "lift", "drive-straight", "turn-in-place", "turn-to-face", "turn-to-cube", "track-face",
            "animation", "say-text", "pickup", "put-down", "roll", "stack", "flip", "wheelie", "go-to-pose",
            "mount-charger", "teach-face", "enroll-face", "rename-face", "erase-face", "set_camera_color",
            "set-backpack-lights", "set-cube-lights", "lock-reactions", "unlock-reactions", "wake", "cancel-action", "freeplay", "set_needs_mode", "set_needs",
        }, entries.Select(entry => entry.GetProperty("name").GetString()));
        Assert.All(entries, entry =>
        {
            var schema = entry.GetProperty("argumentsSchema");
            Assert.Equal("object", schema.GetProperty("type").GetString());
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            foreach (var name in schema.GetProperty("required").EnumerateArray())
                Assert.True(schema.GetProperty("properties").TryGetProperty(name.GetString()!, out _));
        });
        var animation = entries[7].GetProperty("argumentsSchema").GetProperty("properties");
        Assert.Contains(animation.GetProperty("trigger").GetProperty("enum").EnumerateArray(), name => name.GetString() == "CodeLabHappy");
        Assert.Equal(1, animation.GetProperty("loops").GetProperty("default").GetInt32());
    }

    [Theory]
    [InlineData("unknown", "{}", "unknown-command")]
    [InlineData("head", "[]", "invalid-arguments")]
    [InlineData("head", "{}", "invalid-arguments")]
    [InlineData("head", """{"radians":0.2,"radians":0.3}""", "invalid-arguments")]
    [InlineData("head", """{"radians":1e40}""", "invalid-arguments")]
    [InlineData("head", """{"radians":"0.2"}""", "invalid-arguments")]
    [InlineData("head", """{"radians":0.2,"misspelling":true}""", "invalid-arguments")]
    [InlineData("roll", """{"objectId":-1}""", "invalid-arguments")]
    [InlineData("roll", """{"objectId":2.3}""", "invalid-arguments")]
    [InlineData("animation", """{"trigger":"12"}""", "invalid-arguments")]
    [InlineData("freeplay", """{"enabled":1}""", "invalid-arguments")]
    [InlineData("say-text", """{"text":""}""", "invalid-arguments")]
    [InlineData("say-text", """{"text":"\ud800"}""", "invalid-arguments")]
    public async Task InvalidRequestsFailBeforeTouchingTheApi(string command, string json, string code)
    {
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        var initial = f.Robot.ActionList.GetCurrentAction();
        var result = await Invoke(bridge, command, json);
        Assert.Equal(code, result.GetProperty("error").GetProperty("code").GetString());
        Assert.Same(initial, f.Robot.ActionList.GetCurrentAction());
    }

    internal static Task<JsonElement> Invoke(CozmoBridge bridge, string command, string args, CancellationToken token = default)
    {
        using var json = JsonDocument.Parse(args);
        return bridge.ExecuteAsync(command, json.RootElement, token);
    }
}
