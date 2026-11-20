using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;
using Client = Cozmo.Protocol.Tests.McpServerTests.Client;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class McpSession4Tests
{
    [Fact]
    public async Task LiftRegionFollowsTheReturnedImageAndItsExposurePose()
    {
        byte[] calibration = CameraCalibration.Nominal().ToBytes();
        using var f = new Fixture(sourceNvEmpty: true, sourceNvRaw: tag => tag == CameraCalibration.NvEntryTag ? calibration : null);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();

        SetPose(-.4f);
        f.NativeCameraFrame(40, 180);
        var low = Data(await client.Tool("get_camera_image", new { }));
        Assert.Equal(40u, low.GetProperty("imageId").GetUInt32());
        Assert.Equal(-.4, low.GetProperty("headAngleRad").GetDouble(), 5);
        Assert.True(low.GetProperty("liftInView").GetBoolean(), low.GetRawText());
        var region = low.GetProperty("liftRegion");
        double x = region.GetProperty("x").GetDouble(), y = region.GetProperty("y").GetDouble();
        Assert.InRange(x, 0, 159);
        Assert.InRange(y, 0, 239);
        Assert.InRange(x + region.GetProperty("width").GetDouble(), 160, 320);
        Assert.InRange(y + region.GetProperty("height").GetDouble(), y + 1, 240);

        SetPose(.65f);
        var same = Data(await client.Tool("get_camera_image", new { }));
        Assert.Equal(region.GetRawText(), same.GetProperty("liftRegion").GetRawText());
        f.NativeCameraFrame(41, 180);
        var high = Data(await client.Tool("get_camera_image", new { }));
        Assert.Equal(41u, high.GetProperty("imageId").GetUInt32());
        Assert.False(high.GetProperty("liftInView").GetBoolean());
        Assert.Equal(JsonValueKind.Null, high.GetProperty("liftRegion").ValueKind);

        f.Rig.CameraGrayFrame(42, f.Robot.StoredState!.Timestamp + 1_000, new byte[320 * 240]);
        var unknown = Data(await client.Tool("get_camera_image", new { }));
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("liftInView").ValueKind);
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("liftRegion").ValueKind);

        void SetPose(float head)
        {
            var state = f.Rig.MakeState(RobotStatusFlag.HeadInPos | RobotStatusFlag.LiftInPos,
                origin: f.Robot.CurrentOriginId, frame: f.Robot.PoseFrameId, head: head);
            f.Rig.Data(state);
            f.Rig.Tick();
        }
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(false, false)]
    public async Task StraightDriveForwardsTheShippedDefaultAndExplicitAnimationSetting(bool? supplied, bool expected)
    {
        using var f = new Fixture(preloadAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.Motors();
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        var args = new Dictionary<string, object> { ["distanceMm"] = 60, ["speedMmps"] = 30 };
        if (supplied.HasValue) args["shouldPlayAnimation"] = supplied.Value;
        int request = await client.Send("tools/call", new { name = "drive-straight", arguments = args });
        var action = Assert.IsType<DriveStraightAction>(f.Robot.ActionList.GetCurrentAction());
        Assert.Equal(expected, action.ShouldPlayAnimation); // G2E DriveStraight.cs and 0x00529F0A.
        Assert.Equal(60, action.DistanceMm);
        Assert.Equal(30, action.SpeedMmps);
        f.Robot.ActionList.Cancel(-1);
        f.Rig.Tick();
        await client.Receive(request);
    }

    [Fact]
    public async Task TurnSchemaUsesOnlyTheShippedTurnFields()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new Client(bridge);
        await client.Initialize();
        int request = await client.Send("tools/list", new { });
        var list = (await client.Receive(request)).GetProperty("result").GetProperty("tools");
        var turn = list.EnumerateArray().Single(item => item.GetProperty("name").GetString() == "turn-in-place");
        var properties = turn.GetProperty("inputSchema").GetProperty("properties");
        Assert.True(properties.TryGetProperty("absolute", out _));
        Assert.False(properties.TryGetProperty("shouldPlayAnimation", out _));
    }

    private static JsonElement Data(JsonElement reply)
    {
        var result = reply.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean(), result.GetRawText());
        return result.GetProperty("structuredContent");
    }
}
