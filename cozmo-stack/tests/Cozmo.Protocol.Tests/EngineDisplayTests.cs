using Cozmo.Robot;
using Xunit;
namespace Cozmo.Protocol.Tests;
public class EngineDisplayTests
{
    [Fact]
    public void VerboseToggleChangesDisplayedLinesButNotStructuredCalls()
    {
        using var robot = CozmoRobot.CreateOffline();
        var entries = new List<EngineLogEntry>();
        var lines = new List<string>();
        robot.Engine.LogEntry += entries.Add;
        robot.Engine.LogLine += lines.Add;
        robot.Engine.EmitEngineLog(EngineLogLevel.Info, "VisionSystem", "camera.info", "same");
        Assert.Empty(lines);
        robot.Engine.VerboseLogDisplay = true;
        robot.Engine.EmitEngineLog(EngineLogLevel.Info, "VisionSystem", "camera.info", "same");
        Assert.Equal(new[] { "camera.info : [VisionSystem] same " }, lines);
        Assert.Equal(2, entries.Count);
        Assert.Equal(entries[0].Level, entries[1].Level);
        Assert.Equal(entries[0].Channel, entries[1].Channel);
        Assert.Equal(entries[0].Name, entries[1].Name);
        Assert.Equal(entries[0].Text, entries[1].Text);
        Assert.Empty(entries[0].Data);
        Assert.Empty(entries[1].Data);
    }

    [Fact]
    public void ShippedConfigDropsInfoAndDebugButKeepsEntriesAndSinkMetadata()
    {
        using var robot = CozmoRobot.CreateOffline();
        var entries = new List<EngineLogEntry>();
        var lines = new List<string>();
        var displayed = new List<DisplayedEngineLog>();
        robot.Engine.LogEntry += entries.Add;
        robot.Engine.LogLine += lines.Add;
        robot.Engine.DisplayedLog += displayed.Add;
        robot.Engine.EmitEngineLog(EngineLogLevel.Debug, "Camera", "camera.debug", "hidden");
        robot.Engine.EmitEngineLog(EngineLogLevel.Info, "Camera", "camera.info", "hidden");
        robot.Engine.EmitEngineLog(EngineLogLevel.Event, null, "camera.event", "observed \t\r\n");
        Assert.Equal(3, entries.Count);
        Assert.Equal("observed \t\r\n", entries[2].Text); // display trims; entry unchanged
        Assert.Equal(new[] { "camera.event : observed " }, lines);
        Assert.Equal(new DisplayedEngineLog(4, "das", "camera.event : observed "), Assert.Single(displayed));
        Assert.False(robot.Engine.VerboseLogDisplay);
    }

    [Theory]
    [InlineData(EngineLogLevel.Debug, 3)]
    [InlineData(EngineLogLevel.Info, 4)]
    [InlineData(EngineLogLevel.Event, 4)]
    [InlineData(EngineLogLevel.Warning, 5)]
    [InlineData(EngineLogLevel.Error, 6)]
    public void PriorityAndVerboseSwitchChangeOnlyDisplay(EngineLogLevel level, int priority)
    {
        var display = new DasLogDisplay();
        display.Configure(DasLogDisplay.ShippedConfiguration(), _ => { });
        Assert.Equal((2, 2, 2, 15, 150000, 5000), (display.LocalLevel, display.RemoteLevel, display.GameLevel, display.FlushInterval, display.MaxLogLength, display.MaxLogFiles)); // assets/DASConfig.json
        var entry = new EngineLogEntry(level, "VisionSystem", "camera.check", "text ", new Dictionary<string,string>());
        var normal = display.Render(entry, false, _ => { });
        if ((int)level < 2) Assert.Null(normal);
        else Assert.Equal(new DisplayedEngineLog(priority, "das", "camera.check : text "), normal);
        Assert.Equal(new DisplayedEngineLog(priority, "das", "camera.check : [VisionSystem] text "), display.Render(entry, true, _ => { }));
        Assert.Equal("text ", entry.Text);
        Assert.Equal("VisionSystem", entry.Channel);
    }

    [Fact]
    public void ConfigureDefaultsAndDotSegmentOverridesAreIndependentOfRemoteSinks()
    {
        var display = new DasLogDisplay();
        display.Configure("{}", _ => { });
        Assert.Equal((1, 2, 1, 5, 0x19000, 0x190), (display.LocalLevel, display.RemoteLevel, display.GameLevel, display.FlushInterval, display.MaxLogLength, display.MaxLogFiles));
        display.Configure("{\"dasConfig\":{\"localMinLogLevel\":\"event\",\"localMinLogLevelOverrides\":{\"robot.camera\":\"debug\",\"robot.camera.bad\":\"error\"}}}", _ => { });
        Assert.Equal(0, display.GetLevel("robot.camera"));
        Assert.Equal(4, display.GetLevel("robot.camera.bad"));
        Assert.Equal(2, display.GetLevel("robot.cameras.capture")); // no partial segment match
    }

    [Fact]
    public void ParseFailurePreservesDataDefaultsAndReportsUnreadReaderArgument()
    {
        var display = new DasLogDisplay();
        var diagnostics = new List<DisplayedEngineLog>();
        display.Configure("{", diagnostics.Add);
        Assert.Equal((5, 2, 1), (display.LocalLevel, display.RemoteLevel, display.GameLevel));
        Assert.Equal(3, Assert.Single(diagnostics).Priority);
        Assert.Equal("daslib", diagnostics[0].Tag);
        Assert.Contains("Failed to parse configuration: %s", diagnostics[0].Line);
        Assert.StartsWith("MISSING: M3-053", diagnostics[0].Line);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullNameKeepsEntryAndUsesNonameWithAnExplicitMissingLiteral(string? name)
    {
        var display = new DasLogDisplay(); display.Configure("{}", _ => { });
        var missing = new List<string>();
        var entry = new EngineLogEntry(EngineLogLevel.Event, null, name, "text", new Dictionary<string,string>());
        Assert.Equal("noname : text ", display.Render(entry, false, missing.Add)!.Line);
        Assert.StartsWith("MISSING: M3-053", Assert.Single(missing));
        Assert.Equal(name, entry.Name);
    }
}
