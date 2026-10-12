using System.Text.Json;
using System.Text.RegularExpressions;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public class SourceLogMigrationTests
{
    private sealed class Runner : ActionRunner
    {
        public Runner() : base(0, 0) { }
        public override uint CheckIfDone() => 0;
        public override bool CanInterrupt() => true;
    }

    [Fact]
    public void ExistingPortsKeepTheirFieldsAcrossEveryMigratedLayerAndTextNeverCreatesEntries()
    {
        using var robot = CozmoRobot.CreateOffline();
        var engine = robot.Engine;
        var entries = new List<EngineLogEntry>();
        var stack = new List<string>();
        engine.LogEntry += entries.Add;
        engine.StackLogLine += stack.Add;
        SourceLogSink source = (level, channel, key, text) => engine.EmitSourceLog(level, channel, key, text);

        void Check(Action produce, EngineLogLevel level, string? channel, string key, string text)
        {
            entries.Clear(); stack.Clear();
            produce();
            var entry = Assert.Single(entries);
            Assert.Equal(level, entry.Level);
            Assert.Equal(channel, entry.Channel);
            Assert.Equal(key, entry.Name);
            Assert.Equal(text, Regex.Replace(entry.Text, @"^\(tc[0-9]+\) : ", ""));
            Assert.Empty(entry.Data);
            Assert.Empty(stack);
            // The same spelling on the text API still has no authority to create an entry.
            entries.Clear();
            string line = level.ToString().ToLowerInvariant() + ": " +
                (channel is null ? "" : "[" + channel + "] ") + key + ": " + text;
            engine.Log(line);
            Assert.Empty(entries);
            Assert.Equal(line, Assert.Single(stack));
        }

        // M3: existing GetBaseEntryTag warning, 0x00644274. No channel was encoded.
        Check(() => NvStorageComponent.GetBaseEntryTag(0x198000, engine.Log, source),
            EngineLogLevel.Warning, null, "NVStorageComponent.GetBaseEntryTag.TagIsTooSmall", "0x198000");
        // M4-031: PrintLockState 0x00641326..0x0064134A; empty text is still a call.
        Check(robot.Motion.PrintLockState, EngineLogLevel.Debug, "Unnamed", "MovementComponent.LockState", "");
        // M5: the production stream's typed seam (ClearSendBuffer, existing M5-024 port).
        Check(() =>
        {
            robot.Animations.Scheduler.Stream.Buffer(null, 1, false, () => true, "AnimHead");
            robot.Animations.Scheduler.Stream.ClearSendBuffer();
        }, EngineLogLevel.Debug, "Unnamed", "AnimationStreamer.ClearSendBuffer", "Clearing unsent AnimHead");
        // M6: existing NaN warning in the MuLaw encoder. Arithmetic and output remain unchanged.
        Check(() => AnkiMuLaw.Encode(float.NaN, engine.Log, source), EngineLogLevel.Warning, null,
            "RobotAudioAnimationOnRobot.encodeMuLaw.sampleNaN", "Audio sample from current stream is NaN");
        // M7: factory's out-of-range error, 0x0059C888; same key/text and error-flag path.
        var factory = new BehaviorFactory();
        var context = new BehaviorFactoryContext { Log = engine.Log, SourceLog = source };
        var container = new BehaviorContainer(factory, context, Array.Empty<KeyValuePair<BehaviorID, JsonElement>>());
        using var json = JsonDocument.Parse("{}");
        Check(() => factory.CreateBehavior(container, (BehaviorClass)255, context, json.RootElement, out _),
            EngineLogLevel.Error, null, "behaviorContainer.CreateBehavior.Failed", "Failed to create Behavior of type '255'");
        // M8: SetTag's invalid-tag error, 0x00540098..0x00540192; channel stays null.
        var runner = new Runner { Log = engine.Log, SourceLog = source };
        Check(() => runner.SetTag(0), EngineLogLevel.Error, null, "IActionRunner.SetTag.InvalidTag", "Tag [0] is invalid");
        // M10: live Sensors handler, 0x00534F4C..0x00534F9E, before FallingStarted delivery.
        Check(() => robot.Sensors.Handle(new FallingStarted { Unknown = 42 }), EngineLogLevel.Info, null,
            "RobotImplMessaging.HandleFallingStarted", "timestamp 42");
        // M11: the existing unsupported-encoding error and its unchanged flag/break gate.
        Check(() => EncodedImageDecoder.TryDecodeGray(new CameraFrame { Encoding = 0 }, out _, out _, engine.Log, source),
            EngineLogLevel.Error, null, "EncodedImage.DecodeImageRGB.UnsupportedEncoding",
            "Encoding NoneImageEncoding not yet supported for decoding image chunks");
        // M15: existing NeedsState warning, with typed forwarding through the manager.
        var needs = new NeedsManager(() => 0) { SourceLog = source };
        needs.Log += engine.Log;
        Check(() => needs.State.GetNeedBracket((NeedId)(-1)), EngineLogLevel.Warning, null,
            "NeedsState.GetNeedBracket", "invalid need -1");
    }

    [Fact]
    public void B8_UnmatchedCubePatternAndUnknownPathStatusStayOnTheStackStream()
    {
        using var robot = CozmoRobot.CreateOffline();
        var entries = new List<EngineLogEntry>(); var stack = new List<string>();
        robot.Engine.LogEntry += entries.Add; robot.Engine.StackLogLine += stack.Add;
        using var json = JsonDocument.Parse("{\"bad\":[{}]}");
        var lights = new CubeLightAnimations();
        lights.Define(json.RootElement, robot.Engine.Log,
            (level, channel, key, text) => robot.Engine.EmitSourceLog(level, channel, key, text));
        Assert.Empty(entries);
        Assert.Contains(stack, s => s.Contains("a pattern lacks a required field"));
        Assert.Contains(stack, s => s.StartsWith("MISSING: M4-044 | CubeLightAnimationContainer.ParseJsonToAnim.InvalidPattern"));
        entries.Clear(); stack.Clear();
        robot.Motion.Path.DriveToPoseStatus = 7;
        robot.Motion.Path.Abort(_ => true);
        Assert.DoesNotContain(entries, e => e.Name == "PathComponent.Abort");
        Assert.DoesNotContain(entries, e => e.Text.Contains("<MISSING:"));
        Assert.Contains(stack, s => s.StartsWith("MISSING: ERobotDriveToPoseStatus name for status 7"));
    }

    [Fact]
    public void BufferedPortsRetainCallOrderAndEmitAfterTheBudgetLock()
    {
        using var robot = CozmoRobot.CreateOffline();
        var stream = robot.Animations.Scheduler.Stream;
        var entries = new List<EngineLogEntry>();
        var budgets = new List<int>();
        robot.Engine.LogEntry += entry =>
        {
            entries.Add(entry);
            // Re-entry observes the updated budget, as the old text callbacks did.
            var read = Task.Run(() => stream.NumBytesToSend);
            budgets.Add(read.Wait(TimeSpan.FromSeconds(2)) ? read.Result : -1);
        };
        stream.UpdateAmountToSend(-9000, 0);
        var entry = Assert.Single(entries);
        Assert.Equal(new[] { 0 }, budgets);
        Assert.Equal(EngineLogLevel.Warning, entry.Level);
        Assert.Null(entry.Channel);
        Assert.Equal("AnimationStreamer.UpdateAmountToSend.NegativeMinBytesFreeInRobot", entry.Name);
        Assert.Equal("minBytesFree:-808 numBytesStreamed:0 numBytesPlayed:-9000",
            Regex.Replace(entry.Text, @"^\(tc[0-9]+\) : ", ""));
    }
}
