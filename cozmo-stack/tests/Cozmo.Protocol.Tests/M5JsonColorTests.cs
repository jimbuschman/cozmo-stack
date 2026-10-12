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
    [InlineData("OFFWHITE", 0xCCCCCCFFu, 0xE739)]
    [InlineData("bad-name", 0xFFCC00FFu, 0xFF20)]
    public void NamedWordsSurviveJsonLoadingWithoutComponentConversion(string name, uint word, int led)
    {
        // GetColorOptional word copy 0x00840286..88; PLT 0x004CDE44 -> 0x0083F780,
        // miss 0x0083F98C..D0 returns DEFAULT. OFFWHITE at 0xC9745F is cc cc cc ff.
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
        // C17: normalized alpha trunc(0.5f * 255f) = 127. One ColorRGBA is reused
        // (0x004FABA0); Front's size 3 skips alpha (0x00840488), retaining 7f rather than ff.
        WithJson(Frame(Arrays.Replace("\"Back\":[0,0,0]", "\"Back\":[1,1,1,0.5]")), (clip, entries, _) =>
        {
            var frame = Assert.IsType<LightsKeyframe>(Assert.Single(clip.Keyframes));
            Assert.Equal(new uint[] { 0x0000007F, 0x0000007F, 0x0000007F, 0xFFFFFF7F, 0x0000007F }, frame.LoadedColorWords);
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
    [InlineData("{}")] [InlineData("[]")] [InlineData("null")]
    public void EmptyJsonFileSkipsDefinitionSilentlyAndAdvancesProgress(string json)
    {
        // L6.5: Json::Value::empty / cbnz at 0x00521472..78 skips DefineFromJson;
        // L6.6: progress still advances at 0x00521686..0x005216FC.
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(root, "animations"));
        string empty = Path.Combine(root, "animations", "empty.json");
        File.WriteAllText(empty, json);
        bool old = EngineErrorState.ErrorFlagSet;
        try
        {
            var entries = new System.Collections.Concurrent.ConcurrentQueue<EngineLogEntry>();
            var text = new System.Collections.Concurrent.ConcurrentQueue<string>();
            using var robot = CozmoRobot.CreateOffline(); robot.Engine.LogEntry += entries.Enqueue;
            SourceLogSink sourceLog = (l, c, k, t) => robot.Engine.EmitSourceLog(l, c, k, t);
            EngineErrorState.ErrorFlagSet = false;
            Assert.Null(JsonClipLoader.Load(empty, text.Enqueue, sourceLog));
            Assert.Empty(entries); Assert.Empty(text);
            var lib = AnimationLibrary.Open(root, text.Enqueue, sourceLog);
            Assert.Empty(lib.ClipNames);
            Assert.Equal(0.7f, lib.LoadingProgress);
            Assert.False(EngineErrorState.ErrorFlagSet);
            // Loading-file and final loader infos remain; the empty value itself emits nothing.
            Assert.DoesNotContain(entries, e => e.Level is EngineLogLevel.Error or EngineLogLevel.Warning
                || e.Name.StartsWith("CannedAnimationContainer"));
            Assert.Single(entries.Where(e => e.Name == "RobotDataLoader.LoadAnimationFile.LoadingAnimationsFromBinaryOrJson"));
            File.WriteAllText(Path.Combine(root, "animations", "good.json"), "{\"good\":[]}");
            var continued = AnimationLibrary.Open(root, text.Enqueue, sourceLog);
            Assert.Equal(new[] { "good" }, continued.ClipNames);
            Assert.Equal(0.7f, continued.LoadingProgress);
            Assert.False(EngineErrorState.ErrorFlagSet);
            Assert.DoesNotContain(entries, e => e.Name == "CannedAnimationContainer.DefineFromJson.EmptyFile");
        }
        finally { Directory.Delete(root, true); EngineErrorState.ErrorFlagSet = old; }
    }

    [Fact]
    public void DirectContainerDefinitionRetainsEmptyFileError()
    {
        // J1: direct DefineFromJson has its own no-members error, 0x005887DA..0x0058882A.
        bool old = EngineErrorState.ErrorFlagSet;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse("{}");
            var entries = new List<EngineLogEntry>();
            using var robot = CozmoRobot.CreateOffline(); robot.Engine.LogEntry += entries.Add;
            EngineErrorState.ErrorFlagSet = false;
            Assert.Null(JsonClipLoader.LoadDocument(doc, null, (l, c, k, t) => robot.Engine.EmitSourceLog(l, c, k, t)));
            var error = Assert.Single(entries);
            Assert.Equal(EngineLogLevel.Error, error.Level);
            Assert.Equal("CannedAnimationContainer.DefineFromJson.EmptyFile", error.Name);
            Assert.EndsWith("Found no animations in JSON", error.Text);
            Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally { EngineErrorState.ErrorFlagSet = old; }
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
