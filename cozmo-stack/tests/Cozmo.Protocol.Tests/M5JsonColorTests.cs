using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class M5JsonColorTests
{
    private static string Frame(string colors) =>
        "{\"Name\":\"BackpackLightsKeyFrame\",\"triggerTime_ms\":0,\"durationTime_ms\":1," + colors + "}";
    private const string Arrays = "\"Back\":[0,0,0],\"Front\":[0,0,0],\"Middle\":[0,0,0],\"Left\":[0,0,0],\"Right\":[0,0,0]";

    [Theory]
    [InlineData("BLACK", 0x000000FFu, 0x8000)]
    [InlineData("OFFWHITE", 0xCCCCCCCCu, 0xE739)]
    [InlineData("bad-name", 0xFFCC00FFu, 0xFF20)]
    public void NamedWordsSurviveJsonLoadingWithoutComponentConversion(string name, uint word, int led)
    {
        // GetColorOptional word copy 0x00840286..88; PLT 0x004CDE44 -> 0x0083F780,
        // miss 0x0083F98C..D0 returns DEFAULT. OFFWHITE shipped bytes cc cc cc cc.
        WithJson(Frame(string.Join(",", new[] { "Back", "Front", "Middle", "Left", "Right" }
            .Select(key => $"\"{key}\":\"{name}\""))), (clip, entries, _) =>
        {
            Assert.False(clip.LoadTruncated);
            var frame = Assert.IsType<LightsKeyframe>(Assert.Single(clip.Keyframes));
            Assert.Equal(Enumerable.Repeat(word, 5), frame.LoadedColorWords!);
            Assert.Equal(Enumerable.Repeat((ushort)led, 5), frame.EncodedLeds!);
            var warnings = entries.Where(e => e.Level == EngineLogLevel.Warning).ToArray();
            if (name != "bad-name") Assert.Empty(warnings);
            else
            {
                Assert.Equal(5, warnings.Length);
                Assert.All(warnings, e => { Assert.Equal("NamedColors.GetByString", e.Name); Assert.EndsWith("Unknown color name 'bad-name', returning default", e.Text); });
            }
        });
    }

    [Fact]
    public void NamedAlphaCarriesIntoFollowingThreeComponentArray()
    {
        // One reused ColorRGBA: Back named OFFWHITE sets alpha cc; Front's size 3 leaves it untouched.
        WithJson(Frame(Arrays.Replace("\"Back\":[0,0,0]", "\"Back\":\"OFFWHITE\"")), (clip, entries, _) =>
        {
            var frame = Assert.IsType<LightsKeyframe>(Assert.Single(clip.Keyframes));
            Assert.Equal(new uint[] { 0x000000CC, 0x000000CC, 0x000000CC, 0xCCCCCCCC, 0x000000CC }, frame.LoadedColorWords);
            Assert.Empty(entries.Where(e => e.Level == EngineLogLevel.Warning));
        });
    }

    [Theory]
    [InlineData("Back")] [InlineData("Front")] [InlineData("Middle")] [InlineData("Left")] [InlineData("Right")]
    public void MissingColorSkipsHelperWarningAndStopsAtFailedFrame(string key)
    {
        string colors = string.Join(",", new[] { "Back", "Front", "Middle", "Left", "Right" }
            .Where(k => k != key).Select(k => $"\"{k}\":[0,0,0]"));
        AssertRejected(Frame(colors), key, false);
    }

    [Fact]
    public void InvalidColorWarnsBeforeCallerErrorAndSetsErrorFlag()
        => AssertRejected(Frame(Arrays.Replace("\"Back\":[0,0,0]", "\"Back\":null")), "Back", true);

    private static void AssertRejected(string frame, string key, bool warning)
    {
        // Failed color: r3 animation name, stack arg LED key at 0x004FAE4E; errG then return 1.
        WithJson("{\"Name\":\"RecordHeadingKeyFrame\",\"triggerTime_ms\":0}," + frame +
            ",{\"Name\":\"RecordHeadingKeyFrame\",\"triggerTime_ms\":2}", (clip, entries, text) =>
        {
            Assert.True(clip.LoadTruncated);
            Assert.IsType<RecordHeadingKeyframe>(Assert.Single(clip.Keyframes));
            var diagnostics = entries.Where(e => e.Level is EngineLogLevel.Warning or EngineLogLevel.Error).ToArray();
            Assert.Equal(warning ? new[] { "JsonTools.GetColorOptional", "BackpackLightsKeyFrame.SetMembersFromJson", "Animation.DefineFromJson.AddKeyFrameFailure" }
                : new[] { "BackpackLightsKeyFrame.SetMembersFromJson", "Animation.DefineFromJson.AddKeyFrameFailure" }, diagnostics.Select(e => e.Name));
            Assert.EndsWith($"lights: Failed to get '{key}' LED color from Json file", diagnostics[^2].Text);
            Assert.EndsWith("Adding BackpackLightsKeyFrame frame 1 failed.", diagnostics.Last().Text);
            Assert.Equal(EngineLogLevel.Error, diagnostics.Last().Level);
            Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.DoesNotContain(text, t => t.StartsWith("error:") || t.Contains("GetMemberFromJsonMacro"));
        });
    }

    private static void WithJson(string frames, Action<AnimationClip, List<EngineLogEntry>, List<string>> check)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        bool old = EngineErrorState.ErrorFlagSet;
        try
        {
            EngineErrorState.ErrorFlagSet = false;
            File.WriteAllText(path, "{\"lights\":[" + frames + "]}");
            var entries = new List<EngineLogEntry>(); var text = new List<string>();
            using var robot = CozmoRobot.CreateOffline(); robot.Engine.LogEntry += entries.Add;
            var clip = JsonClipLoader.Load(path, text.Add, (l, c, k, t) => robot.Engine.EmitSourceLog(l, c, k, t))!;
            check(clip, entries, text);
        }
        finally { File.Delete(path); EngineErrorState.ErrorFlagSet = old; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void JsonReadFailureKeepsOpenAliveAndAdvancesProgress(bool denyRead)
    {
        // readAsJson false 0x00521470 skips Define and reaches progress 0x00521686.
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(root, "animations"));
        string bad = Path.Combine(root, "animations", "bad.json");
        File.WriteAllText(bad, "{");
        File.WriteAllText(Path.Combine(root, "animations", "good.json"), "{\"good\":[]}");
        bool old = EngineErrorState.ErrorFlagSet;
        try
        {
            using var held = denyRead ? new FileStream(bad, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
            var entries = new System.Collections.Concurrent.ConcurrentQueue<EngineLogEntry>();
            var text = new System.Collections.Concurrent.ConcurrentQueue<string>();
            using var robot = CozmoRobot.CreateOffline(); robot.Engine.LogEntry += entries.Enqueue;
            EngineErrorState.ErrorFlagSet = false;
            var lib = AnimationLibrary.Open(root, text.Enqueue, (l, c, k, t) => robot.Engine.EmitSourceLog(l, c, k, t));
            Assert.True(lib.HasClip("good")); Assert.Single(lib.ClipNames);
            Assert.Equal(0.7f, lib.LoadingProgress);
            var error = Assert.Single(entries.Where(e => e.Name == "DataPlatform.readAsJson"));
            Assert.Equal(EngineLogLevel.Error, error.Level); Assert.EndsWith($"Failed to read [{bad}]", error.Text);
            Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.Contains(text, t => t.StartsWith("MISSING: M5-001 DataPlatform.readAsJson"));
        }
        finally { Directory.Delete(root, true); EngineErrorState.ErrorFlagSet = old; }
    }
}
