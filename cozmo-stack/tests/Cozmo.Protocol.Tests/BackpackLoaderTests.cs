using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
public class BackpackLoaderTests
{
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
