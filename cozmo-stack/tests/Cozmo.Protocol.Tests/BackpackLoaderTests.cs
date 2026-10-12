using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
public class BackpackLoaderTests
{
    // Json::Reader first-value parse (FirmwareJson.cs; 0x008E0B4C..0x008E0C2C): suffix tokens do not change success.
    [Theory]
    [InlineData("{} trailing garbage", true)]
    [InlineData("/* comment */ {} {}", true)]
    [InlineData("{\"charging\":{},}", false)]
    public void B9_LoaderUsesJsonCppFirstValueAcceptance(string json, bool accepted)
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string dir = Path.Combine(root, BackpackLightAnimations.ResourceDirectory); Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "test.json"), json);
            var entries = new List<EngineLogEntry>(); var lines = new List<string>();
            BackpackLightAnimations.Load(root, lines.Add, (level, channel, key, text) => entries.Add(new EngineLogEntry(level, channel, key, text, new Dictionary<string, string>())));
            Assert.Equal(!accepted, entries.Any(e => e.Name == "DataPlatform.readAsJson"));
            if (!accepted) Assert.Contains(lines, l => l.StartsWith("MISSING: M5-051 G5b"));
        }
        finally { Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData("[1]", false)]
    [InlineData("\"bad\"", false)]
    [InlineData("7", false)]
    [InlineData("[1]", true)]
    [InlineData("\"bad\"", true)]
    [InlineData("7", true)]
    public void NonObjectThrowIsIsolatedPerFileAndKeepsEarlierStates(string value, bool stateValue)
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string dir = Path.Combine(root, BackpackLightAnimations.ResourceDirectory);
        Directory.CreateDirectory(dir);
        static string Pattern(int red, int green) => System.Text.Json.JsonSerializer.Serialize(new
        {
            onColors = Enumerable.Repeat(new[] { red, green, 0, 1 }, 5),
            offColors = Enumerable.Repeat(new[] { 0, 0, 0, 1 }, 5),
            onPeriod_ms = new int[5], offPeriod_ms = new int[5],
            transitionOnPeriod_ms = new int[5], transitionOffPeriod_ms = new int[5], offset = new int[5]
        });
        try
        {
            File.WriteAllText(Path.Combine(dir, "01-bad.json"), stateValue
                ? "{\"charging\":" + Pattern(1, 0) + ",\"charged\":" + value + "}" : value);
            // With one file the earlier valid state survives its later exception, independent of NTFS order.
            var partial = BackpackLightAnimations.Load(root);
            if (stateValue) Assert.Equal(LedColor.Red, partial.Get("Charging")![0].OnColor);
            else Assert.Null(partial.Get("Charging"));
            File.WriteAllText(Path.Combine(dir, "02-good.json"), "{\"charging\":" + Pattern(0, 1) + ",\"charged\":" + Pattern(0, 1) + ",\"badCharger\":" + Pattern(0, 1) + "}");
            var logs = new List<string>();
            var patterns = BackpackLightAnimations.Load(root, logs.Add);
            Assert.Equal(new[] { "BadCharger", "Charged", "Charging", "OffCharger" }, patterns.Names.Order());
            if (stateValue) Assert.Contains(patterns.Get("Charging")![0].OnColor, new[] { LedColor.Red, LedColor.Green });
            else Assert.Equal(LedColor.Green, patterns.Get("Charging")![0].OnColor);
            Assert.Equal(LedColor.Green, patterns.Get("BadCharger")![0].OnColor);
            Assert.Contains("event handler threw: JsonLogicError: in Json::Value::find(key, end, found): requires objectValue or nullValue", logs);
            Assert.DoesNotContain(logs, line => line.Contains("InvalidJson")); // throw precedes the first isMember error report
        }
        finally { Directory.Delete(root, true); }
    }

    // Sections 4/13.3: all fields read, two size failures then one InvalidJson per state.
    [Fact]
    public void InvalidPatternReadsBothColorsBeforeOneStateError()
    {
        var lines = new List<string>();
        var patterns = new BackpackLightAnimations();
        using var json = JsonDocument.Parse("{\"charging\":{\"onColors\":[],\"offColors\":[]}}");
        patterns.DefineFromJson(json.RootElement, lines.Add);
        Assert.Equal(new[] {
            "error: JsonTools.GetColorValuesToArrayOptional.DiffSizes: The json array and destination array are different sizes",
            "error: JsonTools.GetColorValuesToArrayOptional.DiffSizes: The json array and destination array are different sizes",
            "error: BackpackLightAnimationContainer.AddBackpackLightStateValues.InvalidJson: Missing member field in pattern definition for BackpackLightsState Charging",
            "error: BackpackLightAnimationContainer.AddBackpackLightStateValues.InvalidJson: Missing member field in pattern definition for BackpackLightsState Charged",
            "error: BackpackLightAnimationContainer.AddBackpackLightStateValues.InvalidJson: Missing member field in pattern definition for BackpackLightsState BadCharger"
        }, lines);
        Assert.Equal(new[] { "OffCharger" }, patterns.Names);
        Assert.True(EngineErrorState.ErrorFlagSet);
    }

    [Fact]
    public void LoaderUsesRecursiveCaseSensitiveSuffixesAndSkipsEmptyRoots()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string dir = Path.Combine(root, BackpackLightAnimations.ResourceDirectory);
        Directory.CreateDirectory(Path.Combine(dir, "nested"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "no-dot-json"), "{}");
            File.WriteAllText(Path.Combine(dir, "nested", "empty.bin"), "[]");
            File.WriteAllText(Path.Combine(dir, "ignored.JSON"), "not JSON");
            var lines = new List<string>();
            var patterns = BackpackLightAnimations.Load(root, lines.Add);
            Assert.Equal(new[] { "OffCharger" }, patterns.Names);
            // B-M5-4: Results belongs to the single CollectAnimFiles loop (0x0051F6A4..0x0051F710),
            // never to this per-type loader. Recursive empty roots emit only the elapsed-time line.
            Assert.StartsWith("info: [Animations] RobotDataLoader.LoadBackpackLightAnimations.LoadTime: Time to load backpack light animations = ", Assert.Single(lines));
        }
        finally { Directory.Delete(root, true); }
    }
}
