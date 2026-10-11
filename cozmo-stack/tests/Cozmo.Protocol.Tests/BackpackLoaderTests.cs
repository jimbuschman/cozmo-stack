using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
public class BackpackLoaderTests
{
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
            File.WriteAllText(Path.Combine(dir, "02-good.json"), "{\"charging\":" + Pattern(0, 1) + ",\"charged\":" + Pattern(0, 1) + ",\"badCharger\":" + Pattern(0, 1) + "}");
            var logs = new List<string>();
            var patterns = BackpackLightAnimations.Load(root, logs.Add);
            Assert.Equal(new[] { "OffCharger", "Charging", "Charged", "BadCharger" }, patterns.Names);
            Assert.Equal(stateValue ? LedColor.Red : LedColor.Green, patterns.Get("Charging")![0].OnColor);
            Assert.Equal(LedColor.Green, patterns.Get("BadCharger")![0].OnColor);
            Assert.Contains("warning: event handler threw: JsonLogicError: in Json::Value::find(key, end, found): requires objectValue or nullValue", logs);
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
            Assert.Equal("info: [Animations] RobotDataLoader.CollectAnimFiles.Results: Found 2 animation files of type 3", lines[0]);
            Assert.Equal(2, lines.Count); // no InvalidJson for empty roots, just results and elapsed time
            Assert.StartsWith("info: [Animations] RobotDataLoader.LoadBackpackLightAnimations.LoadTime: Time to load backpack light animations = ", lines[1]);
        }
        finally { Directory.Delete(root, true); }
    }
}
