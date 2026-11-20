using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

public sealed class McpLightsTests
{
    [Fact]
    public async Task BackpackRequestUsesFiveAppSlotsAndNativeBodyLightPriority()
    {
        using var fixture = new Fixture(sourceNvEmpty: true);
        var rig = fixture.Rig;
        using var bridge = new CozmoBridge(rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();

        var body = rig.Robot.Lights.Body;
        var overrideLocator = new BodyLightComponent.Locator();
        body.StartLooping(Enumerable.Repeat(LedPattern.Solid(LedColor.White), 5).ToArray(), 1, overrideLocator);
        fixture.Motors();
        int mark = rig.Mark();
        var report = Submitted(await client.Tool("set-backpack-lights", new
        {
            ledMask = 18, onColor = "#123456", offColor = "00FF00", onMs = 31, offMs = 60,
            transitionOnMs = 1, transitionOffMs = 91,
        }));
        Assert.Equal("SetBackpackLEDs", report.GetProperty("gameMessage").GetString());
        Assert.Equal("engine-light-layer", report.GetProperty("delivery").GetString());
        Assert.Equal(5, report.GetProperty("leds").GetArrayLength());
        fixture.Motors();
        Assert.Empty(BackpackPackets(rig, mark)); // priority 1 still owns the backpack at 0x00631D54..0x00631DDE

        Assert.True(body.StopLooping(overrideLocator));
        fixture.Motors();
        string light = "CA88E083020201040000";
        string dark = "00000000000000000000";
        Assert.Equal(new[] { "03" + light + dark + dark + "00", "11" + dark + light + "00" },
            BackpackPackets(rig, mark)); // source 2 splits LED 1..3 and 0,4 at 0x00631F3A..0x006321BE

        mark = rig.Mark();
        Submitted(await client.Tool("set-backpack-lights", new { ledMask = 4, onColor = "#0000FF" }));
        fixture.Motors();
        Assert.Equal(new[] { "03" + light + "1F800000FF0000000000" + dark + "00", "11" + dark + light + "00" },
            BackpackPackets(rig, mark)); // Unity sends all slots, keeping its earlier front and right values.
    }

    [Fact]
    public async Task CubeWorldIdSelectsTheRadioSlotAndKeepsTheActiveLightLayer()
    {
        using var fixture = new Fixture(sourceNvEmpty: true);
        var rig = fixture.Rig;
        const ObjectType type = ObjectType.Block_LIGHTCUBE1;
        M4ControlTests.ConnectCube(rig, slot: 2, type: type);
        uint id = Assert.Single(rig.Robot.LocalizationWorld.ConnectedObjects, cube => cube.Type == type).ObjectId;
        var component = rig.Robot.Lights.Cubes;
        Assert.True(component.PlayLightAnim(type, "WakeUp", CubeLightComponent.EngineLayer));
        var animation = component.PlayingForTest(type, CubeLightComponent.EngineLayer);
        Assert.NotNull(animation);
        using var bridge = new CozmoBridge(rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();

        int mark = rig.Mark();
        var report = Submitted(await client.Tool("set-cube-lights", new
        {
            objectId = id, ledMask = 5, onColor = "#FF6432", offColor = "#0080FF", onMs = 31, offMs = 60,
            transitionOnMs = 1, transitionOffMs = 91,
        }));
        Assert.Equal("SetAllActiveObjectLEDs", report.GetProperty("gameMessage").GetString());
        Assert.Equal(id, report.GetProperty("objectId").GetUInt32());
        Assert.Equal("robot-send-queue", report.GetProperty("delivery").GetString());
        string color = "E3FC1F82020201040000"; // native white balance: (255,100,50) → (255,60,30)
        string dark = "00000000454500000000"; // zero/zero period normalization at 0x004E48B0..0x004E49A2
        Assert.Equal(new[] { "100200000000", "04" + color + dark + color + dark }, CubePackets(rig, mark));
        Assert.Equal(animation, component.PlayingForTest(type, CubeLightComponent.EngineLayer));

        mark = rig.Mark();
        Submitted(await client.Tool("set-cube-lights", new { objectId = id, ledMask = 2, onColor = "#0000FF" }));
        Assert.Equal(new[] { "100200000000", "04" + color + "1F801F80FF4500000000" + color + dark }, CubePackets(rig, mark));
        Assert.Equal(animation, component.PlayingForTest(type, CubeLightComponent.EngineLayer));

        mark = rig.Mark();
        var missing = await client.Tool("set-cube-lights", new { objectId = 1999999999, onColor = "FFFFFF" });
        Assert.True(missing.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Empty(CubePackets(rig, mark));
    }

    [Theory]
    [InlineData("set-backpack-lights", """{"ledMask":32,"onColor":"FFFFFF"}""")]
    [InlineData("set-backpack-lights", """{"onColor":"##FFFFFF"}""")]
    [InlineData("set-backpack-lights", """{"onColor":"FFFFFF","transitionOffMs":4294967296}""")]
    [InlineData("set-cube-lights", """{"objectId":4,"ledMask":0,"onColor":"FFFFFF"}""")]
    public async Task InvalidLightRequestsDoNotSend(string name, string args)
    {
        using var fixture = new Fixture(sourceNvEmpty: true);
        using var bridge = new CozmoBridge(fixture.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        using var request = JsonDocument.Parse(args);
        int mark = fixture.Rig.Mark();
        var reply = await client.Tool(name, request.RootElement);
        Assert.True(reply.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Empty(BackpackPackets(fixture.Rig, mark));
        Assert.Empty(CubePackets(fixture.Rig, mark));
    }

    private static string[] BackpackPackets(M4ControlTests.Rig rig, int mark) =>
        rig.SentSince(mark).Where(m => m is BackpackLightsMiddle or BackpackLightsTurnSignals)
            .Select(m => Convert.ToHexString(m.ToBytes())).ToArray();

    private static string[] CubePackets(M4ControlTests.Rig rig, int mark) =>
        rig.SentSince(mark).Where(m => m is SetCubeGamma or CubeID or CubeLights)
            .Select(m => Convert.ToHexString(m.ToBytes())).ToArray();

    private static JsonElement Submitted(JsonElement response)
    {
        var result = response.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean(), result.ToString());
        var content = result.GetProperty("structuredContent");
        Assert.Equal("submitted", content.GetProperty("status").GetString());
        return content.GetProperty("report");
    }
}
